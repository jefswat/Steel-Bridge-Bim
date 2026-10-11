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

    /// <summary>Stations where finished grade may kink or change curvature along the alignment: cross-slope and pivot table rows,
    /// grade breaks (VPIs without a curve) and vertical curve ends.</summary>
    public static List<double> KinkStations(Alignments.Alignment a)
    {
        var sta = a.CrossSlope.Rows.Select(r => r.Station).ToList();
        if (a.CrossSlope.PivotOffsets is { } po) sta.AddRange(po.Select(r => r.Station));
        foreach (var v in a.Profile.Vpis)
        {
            if (v.CurveLength <= 0) sta.Add(v.Station);
            else { sta.Add(v.Station - v.LengthIn); sta.Add(v.Station + v.LengthOut); }
        }
        return sta;
    }

    /// <summary>Alignment distances where the deck top kinks across its whole width (cross-slope / pivot / edge table stations) and
    /// whose radial line crosses every column between the deck ends: the panel boundaries of the NURBS slab.</summary>
    private List<double> KinkDistances(List<Func<double, double>> hard)
    {
        var sta = KinkStations(_a);
        if (Deck.LeftEdgeTable is { } lt) sta.AddRange(lt.Select(r => r.Station));
        if (Deck.RightEdgeTable is { } rt) sta.AddRange(rt.Select(r => r.Station));
        var ends = hard.Select(h => EndsVar(h, InsetB, InsetE)).ToList();
        double lo = ends.Max(e => e.b) + 0.05, hi = ends.Min(e => e.e) - 0.05;
        var list = new List<double>();
        foreach (double d in sta.Select(s => _a.StationToDistance(s)).Where(d => d > lo && d < hi).OrderBy(d => d))
            if (list.Count == 0 || d - list[^1] > 0.05) list.Add(d);
        return list;
    }

    /// <summary>
    /// Deck slab from NURBS: fits the panels x bands (between the hard columns and the kink stations) to the exact top (alignment offset
    /// point + finished grade, rows ending where they cross the deck-end lines) and fills <paramref name="top"/> / <paramref name="bot"/>
    /// for the generator's columns. Returns the slab (null = keep today's grid; the reason is reported).
    /// </summary>
    private NurbsSlab? NurbsDeck(List<Func<double, double>> cols, ref int ni, out Vec3[,] top, out Vec3[,] bot)
    {
        top = bot = new Vec3[0, 0];
        var hard = HardColumns();
        int bands = hard.Count - 1;
        var kinks = KinkDistances(hard);
        int panels = kinks.Count + 1;
        Func<double, Vec3> Row(int pn, int b, double v)
        {
            var ha = hard[b]; var hb = hard[b + 1];
            Func<double, double> off = d => ha(d) + (hb(d) - ha(d)) * v;
            var (db, de) = EndsVar(off, InsetB, InsetE);
            double d0 = pn == 0 ? db : kinks[pn - 1], d1 = pn == panels - 1 ? de : kinks[pn];
            return u =>
            {
                double d = d0 + (d1 - d0) * u, o = off(d);
                return _a.PointAtDistance(d, o).WithZ(Top(d, o));
            };
        }
        var slab = NurbsSlab.Create(panels, bands, Row, Deck.Thickness, _fitTol, out string reason);
        if (slab == null) { _nurbs!.Fallback("deck slab", reason); return null; }
        var (e0, e1) = EndsVar(hard[0], InsetB, InsetE);
        var bnd = new List<double> { e0 };
        bnd.AddRange(kinks);
        bnd.Add(e1);
        // u samples per panel: the chord tolerance, or today's spacing spread over the panels
        var us = new List<(int pn, double u)>();
        for (int pn = 0; pn < panels; pn++)
        {
            List<double> s;
            if (_geo!.ChordTolerance > 0) s = slab.Samples(pn, _geo.ChordTolerance);
            else
            {
                int n = Math.Max(1, (int)Math.Round((ni - 1) * (bnd[pn + 1] - bnd[pn]) / (e1 - e0)));
                s = Enumerable.Range(0, n + 1).Select(i => (double)i / n).ToList();
            }
            foreach (double u in s) if (pn == 0 || u > 0) us.Add((pn, u));
        }
        ni = us.Count;
        double dm = (e0 + e1) / 2;
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
                var p = slab.TopAt(us[i].pn, where[k].Item1, where[k].Item2, us[i].u);
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
        // Per-span layouts may jog the path (points 0.01 mm apart): separate solids on each side, as one loft cannot be C0 there.
        var runs = new List<(int a, int b)>();
        int r0 = 0;
        for (int i = 0; i + 1 < ss.Count; i++)
            if (ss[i + 1] - ss[i] < 1e-4) { runs.Add((r0, i)); r0 = i + 1; }
        runs.Add((r0, ss.Count - 1));
        var sweeps = new List<NurbsSweep>();
        foreach (var (ia, ib) in runs)
        {
            if (ib <= ia) continue;
            var sw = NurbsSweep.Create(4, Exact, ss[ia], ss[ib], breaks, _fitTol, out string reason);
            if (sw == null) { _nurbs!.Fallback("haunch", $"{g.Name}: {reason}"); return false; }
            sweeps.Add(sw);
        }
        var sec = new[] { new Vec2(-1, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(-1, 1) };
        foreach (var sw in sweeps)
        {
            var us = _geo!.ChordTolerance > 0 ? sw.Samples(_geo.ChordTolerance) : ss.Where(s => s >= sw.U0 - 1e-9 && s <= sw.U1 + 1e-9).ToList();
            int tri0 = mesh.TriangleCount;
            MeshBuilder.AddLoft(mesh, sec, us.Select(sw.Ring).ToList());
            mesh.AddNurbs(sw.ToSolid("haunch"), tri0);
            _nurbs!.Built("haunch", sw.MaxDeviation);
        }
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
