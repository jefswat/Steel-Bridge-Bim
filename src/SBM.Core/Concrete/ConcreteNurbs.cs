using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Concrete;

/// <summary>Exact-geometry option: deck slab, haunches and barriers as NURBS, their meshes tessellated from the NURBS.</summary>
public sealed partial class ConcreteGenerator
{
    private Project.GeometrySettings? _geo;
    private double _fitTol;

    /// <summary>Columns where the deck top may kink across (edges, crown / pivot, gutter lines, kinked-end break points), left to right.</summary>
    /// <summary>Break points of the deck-end lines as the slab is cut (inset by the joint blockouts) between the deck edges.</summary>
    private IEnumerable<Vec2> CutKinkVertices()
    {
        foreach (var (begin, inset) in new[] { (true, InsetB), (false, InsetE) })
            if (EndPoly(begin, inset) is { } p) foreach (var v in p.VerticesBetween(_a, LeftAtD, RightAtD)) yield return v;
    }

    /// <summary>Offsets of the cross-slope break lines (gores): pivot -/+ every break distance of the table (constant pivot only).</summary>
    private List<double> BreakOffsets()
    {
        var cs = _a.CrossSlope;
        var list = new List<double>();
        if (cs.VariablePivot) return list;
        foreach (var r in cs.Rows)
        {
            if (r.LeftBreak > 0) list.Add(cs.PivotOffset - r.LeftBreak);
            if (r.RightBreak > 0) list.Add(cs.PivotOffset + r.RightBreak);
            if (r.LeftMoreBreaks != null) list.AddRange(r.LeftMoreBreaks.Where(b => b.Distance > 0).Select(b => cs.PivotOffset - b.Distance));
            if (r.RightMoreBreaks != null) list.AddRange(r.RightMoreBreaks.Where(b => b.Distance > 0).Select(b => cs.PivotOffset + b.Distance));
        }
        return list.Distinct().ToList();
    }

    private List<Func<double, double>> HardColumns()
    {
        var list = new List<Func<double, double>>();
        if (!Deck.VariableWidth)
        {
            double l = Deck.LeftEdgeOffset, r = Deck.RightEdgeOffset;
            var offs = new List<double> { l, r };
            foreach (double piv in PivotColumns()) if (piv > l && piv < r) offs.Add(piv);
            offs.AddRange(StripLines(l, r));
            offs.AddRange(BreakOffsets().Where(o => o > l && o < r));
            foreach (var v in CutKinkVertices()) offs.Add(_a.Inverse(v).Offset);
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
        // the crown (cross-slope pivot) and the cross-slope break lines where they lie inside the deck
        var cs = _a.CrossSlope;
        var (pb, pe) = EndsVar(LeftAtD, InsetB, InsetE);
        var extra = new List<Func<double, double>> { d => cs.PivotOffsetAt(_a.DistanceToStation(d)) };
        foreach (double bo in BreakOffsets()) extra.Add(_ => bo);
        foreach (var c in extra)
            if (new[] { pb, (pb + pe) / 2, pe }.All(d => c(d) > LeftAtD(d) + 1e-3 && c(d) < RightAtD(d) - 1e-3))
            {
                double dm = (pb + pe) / 2;
                int at = list.FindIndex(h => h(dm) > c(dm));
                if (at > 0 && Math.Abs(list[at](dm) - c(dm)) > 1e-3 && Math.Abs(list[at - 1](dm) - c(dm)) > 1e-3) list.Insert(at, c);
            }
        foreach (var v in CutKinkVertices())
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
    private (List<double> kinks, List<double>[] roots) KinkDistances(List<Func<double, double>> hard)
    {
        var sta = KinkStations(_a);
        if (Deck.LeftEdgeTable is { } lt) sta.AddRange(lt.Select(r => r.Station));
        if (Deck.RightEdgeTable is { } rt) sta.AddRange(rt.Select(r => r.Station));
        var ends = hard.Select(h => EndsVar(h, InsetB, InsetE)).ToList();
        double lo = ends.Min(e => e.b) + 0.01, hi = ends.Max(e => e.e) - 0.01;
        // edge strip: the level strip switches on / off where the slope at the gutter line changes sign
        if (Deck.EdgeStrip is { LowSideLevel: true, Width: > 0 } es && _a.CrossSlope.Rows.Count > 0)
        {
            (bool, bool) Level(double d)
            {
                double st = _a.DistanceToStation(d), piv = _a.CrossSlope.PivotOffsetAt(st);
                var (l, r) = _a.CrossSlope.SidesAt(st);
                double gr = Deck.RightAt(st) - es.Width, gl = Deck.LeftAt(st) + es.Width;
                return (gr > piv && r.SlopeAt(gr - piv) < 0, gl < piv && l.SlopeAt(piv - gl) < 0);
            }
            var prev = Level(lo);
            for (double d = lo, step = 0.25; d < hi; d += step)
            {
                double dn = Math.Min(d + step, hi);
                var cur = Level(dn);
                if (cur != prev)
                {
                    double a = d, b = dn;
                    while (b - a > 1e-7) { double m = (a + b) / 2; if (Level(m) == prev) a = m; else b = m; }
                    sta.Add(_a.DistanceToStation((a + b) / 2));
                }
                prev = cur;
            }
        }
        double fullLo = ends.Max(e => e.b), fullHi = ends.Min(e => e.e);
        var list = new List<double>();
        foreach (double d in sta.Select(s => _a.StationToDistance(s)).Where(d => d > lo && d < hi).OrderBy(d => d))
            if (list.Count == 0 || d - list[^1] > 0.05) list.Add(d);
        // A kink line that meets a (skewed / kinked) deck end inside the deck: the panel boundary follows the end beyond that point, so the
        // point where the radial line meets the end is a column of the two panels next to that line.
        var roots = new List<double>[list.Count];
        for (int k = 0; k < list.Count; k++)
        {
            double dk = list[k];
            roots[k] = new List<double>();
            bool begin = dk < fullLo + 1e-6, end = dk > fullHi - 1e-6;
            foreach (bool atBegin in new[] { true, false })
            {
                if (atBegin ? !begin : !end) continue;
                double inset = atBegin ? InsetB : InsetE;
                var poly = EndPoly(atBegin, inset);
                var nrm = atBegin ? DR.BeginLineDirection.Left : -DR.EndLineDirection.Left;
                var lp = (atBegin ? DR.BeginLinePoint : DR.EndLinePoint) + nrm * inset;
                double G(double o) { var p = _a.PointAtDistance(dk, o); return poly != null ? poly.SignedDistance(p) : Vec2.Dot(p - lp, nrm); }
                double l = hard[0](dk), r = hard[^1](dk);
                foreach (double o in Numerics.FindRoots(G, l, r, 400))
                    if (!hard.Any(h => Math.Abs(h(dk) - o) < 1e-3)) roots[k].Add(o);
            }
        }
        return (list, roots);
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
        var (kinks, roots) = KinkDistances(hard);
        int panels = kinks.Count + 1;
        // columns per panel: the hard columns plus the end-meeting points of its two kink lines (clamped inside the deck)
        var pcols = new List<Func<double, double>>[panels];
        var pmid = new double[panels];
        var (e0, e1) = EndsVar(hard[0], InsetB, InsetE);
        var bnd = new List<double> { e0 };
        bnd.AddRange(kinks.Select(k => Math.Clamp(k, e0, e1)));
        bnd.Add(e1);
        for (int pn = 0; pn < panels; pn++)
        {
            var list = hard.ToList();
            pmid[pn] = (bnd[pn] + bnd[pn + 1]) / 2;
            foreach (int k in new[] { pn - 1, pn })
            {
                if (k < 0 || k >= kinks.Count) continue;
                double dk = kinks[k];
                foreach (double o in roots[k])
                {
                    int at = list.FindIndex(h => h(dk) > o);
                    if (at > 0 && Math.Abs(list[at](dk) - o) > 1e-3 && Math.Abs(list[at - 1](dk) - o) > 1e-3)
                        list.Insert(at, d => Math.Clamp(o, list[0](d), list[^1](d)));
                }
            }
            pcols[pn] = list;
        }
        Func<double, Vec3> Row(int pn, int b, double v)
        {
            var ha = pcols[pn][b]; var hb = pcols[pn][b + 1];
            Func<double, double> off = d => ha(d) + (hb(d) - ha(d)) * v;
            var (db, de) = EndsVar(off, InsetB, InsetE);
            // panel boundaries at the kink lines, clamped to this row's ends (rows beyond a skewed end have no length in that panel)
            double d0 = pn == 0 ? db : Math.Clamp(kinks[pn - 1], db, de), d1 = pn == panels - 1 ? de : Math.Clamp(kinks[pn], db, de);
            return u =>
            {
                double d = d0 + (d1 - d0) * u, o = off(d);
                return _a.PointAtDistance(d, o).WithZ(Top(d, o));
            };
        }
        var slab = NurbsSlab.Create(panels, pn => pcols[pn].Count - 1, Row, Deck.Thickness, _fitTol, out string reason);
        if (slab == null) { _nurbs!.Fallback("deck slab", reason); return null; }
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
        // each generator column: its band and v in every panel (columns are affine in the hard columns, so v is constant along a panel)
        var where = new (int b, double v)[panels][];
        for (int pn = 0; pn < panels; pn++)
        {
            var hc = pcols[pn];
            double dm = pmid[pn];
            where[pn] = cols.Select(c =>
            {
                double o = c(dm);
                for (int b = 0; b < hc.Count - 1; b++)
                {
                    double a = hc[b](dm), e = hc[b + 1](dm);
                    if (o <= e + 1e-7 || b == hc.Count - 2) return (b, e - a > 1e-9 ? Math.Clamp((o - a) / (e - a), 0, 1) : 0);
                }
                return (0, 0.0);
            }).ToArray();
        }
        top = new Vec3[ni, cols.Count];
        bot = new Vec3[ni, cols.Count];
        for (int k = 0; k < cols.Count; k++)
            for (int i = 0; i < ni; i++)
            {
                var (pn, u) = us[i];
                var p = slab.TopAt(pn, where[pn][k].b, where[pn][k].v, u);
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
            var us = _geo!.ChordTolerance > 0 ? sw.Samples(_geo.ChordTolerance) : HaunchSamples(ss.Where(s => s >= sw.U0 - 1e-9 && s <= sw.U1 + 1e-9).ToList());
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
