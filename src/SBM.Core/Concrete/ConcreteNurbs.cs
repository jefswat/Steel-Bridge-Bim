using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Concrete;

/// <summary>Exact-geometry option: deck slab, haunches and barriers as NURBS, their meshes tessellated from the NURBS.</summary>
public sealed partial class ConcreteGenerator
{
    private Project.GeometrySettings? _geo;
    private double _fitTol;

    /// <summary>Columns where the deck top may kink across (edges, crown / pivot, gutter lines, kinked-end break points), left to right.</summary>
    private List<Func<double, double>> HardColumns()
    {
        var list = new List<Func<double, double>>();
        if (!Deck.VariableWidth)
        {
            double l = Deck.LeftEdgeOffset, r = Deck.RightEdgeOffset;
            var offs = new List<double> { l, r };
            foreach (double piv in PivotColumns()) if (piv > l && piv < r) offs.Add(piv);
            offs.AddRange(StripLines(l, r));
            foreach (var v in KinkVertices()) offs.Add(_a.Inverse(v).Offset);
            var sorted = offs.OrderBy(x => x).ToList();
            var clean = new List<double>();
            foreach (double o in sorted) if (clean.Count == 0 || o - clean[^1] > 1e-6) clean.Add(o);
            return clean.Select(o => (Func<double, double>)(_ => o)).ToList();
        }
        list.Add(LeftAtD);
        if (Deck.EdgeStrip is { LowSideLevel: true, Width: > 0 } es)
        {
            double w = es.Width;
            list.Add(d => LeftAtD(d) + w);
            list.Add(d => RightAtD(d) - w);
        }
        list.Add(RightAtD);
        foreach (var v in KinkVertices())
        {
            var so = _a.Inverse(v);
            double dv = so.Distance, ov = so.Offset;
            for (int j = 0; j + 1 < list.Count; j++)
            {
                double a = list[j](dv), b = list[j + 1](dv);
                if (ov <= a + 1e-6 || ov >= b - 1e-6) continue;
                var ca = list[j]; var cb = list[j + 1];
                double f = (ov - a) / (b - a);
                list.Insert(j + 1, d => ca(d) + (cb(d) - ca(d)) * f);
                break;
            }
        }
        return list;
    }

    /// <summary>
    /// Deck slab from NURBS: fits the bands between the hard columns to the exact top (alignment offset point + finished grade, ends where
    /// each row crosses the deck-end lines) and fills <paramref name="top"/> / <paramref name="bot"/> for the generator's columns at the
    /// u samples. Returns the slab (null = keep today's grid; the reason is reported).
    /// </summary>
    private NurbsSlab? NurbsDeck(List<Func<double, double>> cols, ref int ni, out Vec3[,] top, out Vec3[,] bot)
    {
        top = bot = new Vec3[0, 0];
        var hard = HardColumns();
        int bands = hard.Count - 1;
        Func<double, Vec3> Row(int b, double v)
        {
            var ha = hard[b]; var hb = hard[b + 1];
            Func<double, double> off = d => ha(d) + (hb(d) - ha(d)) * v;
            var (db, de) = EndsVar(off, InsetB, InsetE);
            return u =>
            {
                double d = db + (de - db) * u, o = off(d);
                return _a.PointAtDistance(d, o).WithZ(Top(d, o));
            };
        }
        var slab = NurbsSlab.Create(bands, Row, Deck.Thickness, _fitTol, out string reason);
        if (slab == null) { _nurbs!.Fallback("deck slab", reason); return null; }
        int n0 = ni;
        var us = _geo!.ChordTolerance > 0 ? slab.Samples(_geo.ChordTolerance) : Enumerable.Range(0, n0).Select(i => (double)i / (n0 - 1)).ToList();
        ni = us.Count;
        var (d0, d1) = EndsVar(hard[0], InsetB, InsetE);
        double dm = (d0 + d1) / 2;
        var where = cols.Select(c =>
        {
            double o = c(dm);
            for (int b = 0; b < bands; b++)
            {
                double a = hard[b](dm), e = hard[b + 1](dm);
                if (o <= e + 1e-7 || b == bands - 1) return (b, Math.Clamp((o - a) / (e - a), 0, 1));
            }
            return (0, 0.0);
        }).ToList();
        top = new Vec3[ni, cols.Count];
        bot = new Vec3[ni, cols.Count];
        for (int k = 0; k < cols.Count; k++)
            for (int i = 0; i < ni; i++)
            {
                var p = slab.TopAt(where[k].Item1, where[k].Item2, us[i]);
                top[i, k] = p;
                bot[i, k] = new Vec3(p.X, p.Y, p.Z - Deck.Thickness);
            }
        _nurbs!.Built("deck slab", slab.MaxDeviation);
        return slab;
    }

    /// <summary>Haunch along one girder as a NURBS sweep (null = today's loft). Corners: flange edges at top of flange, deck bottom above them.</summary>
    private bool NurbsHaunch(Mesh mesh, Framing.GirderResult g, Func<double, (double hw, double tf)> seg)
    {
        var path = g.Path;
        var ss = path.S;
        Vec3[] Ring(double s, (double hw, double tf) sg)
        {
            var (p, t) = path.ExactAt(s);
            var right = new Vec3(t.Right.X, t.Right.Y, 0);
            var pl = (p + right * -sg.hw).XY;
            var pr = (p + right * sg.hw).XY;
            double ztf = p.Z + sg.tf;
            return new[] { pl.WithZ(ztf), pr.WithZ(ztf), pr.WithZ(DeckBottomAt(pr)), pl.WithZ(DeckBottomAt(pl)) };
        }
        // Where the flange size changes between two path samples, today's loft blends the two rings: so does the exact function.
        var segAt = ss.Select(s => seg(s)).ToArray();
        var breaks = path.ExactBreaks.ToList();
        if (path.Exact == null) breaks.AddRange(ss);
        for (int i = 0; i + 1 < ss.Count; i++) if (segAt[i] != segAt[i + 1]) { breaks.Add(ss[i]); breaks.Add(ss[i + 1]); }
        Vec3[] Exact(double s)
        {
            int i = ss.BinarySearch(s);
            if (i < 0) i = ~i - 1;
            i = Math.Clamp(i, 0, ss.Count - 2);
            if (segAt[i] == segAt[i + 1]) return Ring(s, segAt[i]);
            var a = Ring(ss[i], segAt[i]);
            var b = Ring(ss[i + 1], segAt[i + 1]);
            double f = (s - ss[i]) / (ss[i + 1] - ss[i]);
            return a.Select((q, k) => Vec3.Lerp(q, b[k], f)).ToArray();
        }
        foreach (double s in ss)
        {
            var r = Exact(s);
            if (r[3].Z < r[0].Z + 1e-3 || r[2].Z < r[1].Z + 1e-3) { _nurbs!.Fallback("haunch", $"{g.Name}: haunch thinner than 1 mm"); return false; }
        }
        var sw = NurbsSweep.Create(4, Exact, ss[0], ss[^1], breaks, _fitTol, out string reason);
        if (sw == null) { _nurbs!.Fallback("haunch", $"{g.Name}: {reason}"); return false; }
        var us = _geo!.ChordTolerance > 0 ? sw.Samples(_geo.ChordTolerance) : ss.ToList();
        var sec = new[] { new Vec2(-1, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(-1, 1) };
        int tri0 = mesh.TriangleCount;
        MeshBuilder.AddLoft(mesh, sec, us.Select(sw.Ring).ToList());
        mesh.AddNurbs(sw.ToSolid("haunch"), tri0);
        _nurbs!.Built("haunch", sw.MaxDeviation);
        return true;
    }

    /// <summary>Barrier as a NURBS sweep of its profile (u = 0..1 between each profile point's crossings of the deck ends).</summary>
    private Mesh? NurbsBarrier(Vec2[] prof, Func<double, int, Vec3> exact, int ni)
    {
        var sw = NurbsSweep.Create(prof.Length, u => Enumerable.Range(0, prof.Length).Select(k => exact(u, k)).ToArray(), 0, 1, Array.Empty<double>(), _fitTol,
            out string reason, 0.1);
        if (sw == null) { _nurbs!.Fallback("barrier", reason); return null; }
        var us = _geo!.ChordTolerance > 0 ? sw.Samples(_geo.ChordTolerance) : Enumerable.Range(0, ni).Select(i => (double)i / (ni - 1)).ToList();
        var mesh = new Mesh();
        MeshBuilder.AddLoft(mesh, prof, us.Select(sw.Ring).ToList());
        mesh.AddNurbs(sw.ToSolid("barrier"), 0);
        _nurbs!.Built("barrier", sw.MaxDeviation);
        return mesh;
    }

    private NurbsReport? _nurbs;
}
