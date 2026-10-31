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

    private List<Func<double, double>> HardColumns(bool kinkVertices = true)
    {
        var list = new List<Func<double, double>>();
        if (!Deck.VariableWidth)
        {
            double l = Deck.LeftEdgeOffset, r = Deck.RightEdgeOffset;
            var offs = new List<double> { l, r };
            foreach (double piv in PivotColumns()) if (piv > l && piv < r) offs.Add(piv);
            offs.AddRange(StripLines(l, r));
            offs.AddRange(BreakOffsets().Where(o => o > l && o < r));
            if (kinkVertices) foreach (var v in CutKinkVertices()) offs.Add(_a.Inverse(v).Offset);
            // OPTIONAL sloped overhang soffit: the slab bottom kinks where the taper starts
            if (SlopedOverhang is { } oh)
            {
                if (oh.Left && l + oh.Length < r - 1e-6) offs.Add(l + oh.Length);
                if (oh.Right && r - oh.Length > l + 1e-6) offs.Add(r - oh.Length);
                // and where the sloped soffit crosses the flat slab soffit (thickness = max of the two)
                double f = Math.Abs(oh.RootThickness - oh.TipThickness) > 1e-9 ? (Deck.Thickness - oh.TipThickness) / (oh.RootThickness - oh.TipThickness) : -1;
                if (f > 1e-6 && f < 1 - 1e-6)
                {
                    if (oh.Left) offs.Add(l + oh.Length * f);
                    if (oh.Right) offs.Add(r - oh.Length * f);
                }
            }
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
        foreach (var v in kinkVertices ? CutKinkVertices() : Enumerable.Empty<Vec2>())
        {
            var so = _a.Inverse(v);
            double dv = so.Distance, ov = so.Offset;
            for (int j = 0; j + 1 < list.Count; j++)
            {
                double a = list[j](dv), b = list[j + 1](dv);
                if (ov <= a + 1e-6 || ov >= b - 1e-6) continue;
                var ca = list[j]; var cb = list[j + 1];
                double f = (ov - a) / (b - a);
                list.Insert(j + 1, d => { double a = ca(d); return a + (cb(d) - a) * f; });
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
        // ONE column set for every panel: the hard columns plus every point where a kink line meets a deck end (as a fixed blend of its two
        // neighbours, so columns never cross). A panel boundary is then split at the same points on both sides (no T-junctions where a kink
        // line runs into a skewed / kinked end) and a band beyond the end in one panel is the same band, with the same end curve, in the next.
        // every inserted column is a fixed blend of the two HARD columns it lies between (base index), never of another inserted column: blends of
        // blends nested one closure per insertion and evaluated each neighbour twice (cost doubled per column: Ramp G U4 regenerated in 410 s)
        var all = hard.ToList();
        var baseOf = Enumerable.Range(0, hard.Count).ToList();   // index of the hard column at or left of each entry
        for (int k = 0; k < kinks.Count; k++)
        {
            double dk = kinks[k];
            foreach (double o in roots[k])
            {
                int at = all.FindIndex(h => h(dk) > o);
                if (at <= 0 || Math.Abs(all[at](dk) - o) <= 1e-3 || Math.Abs(all[at - 1](dk) - o) <= 1e-3) continue;
                int ib = baseOf[at - 1];
                var h0 = hard[ib]; var h1 = hard[Math.Min(ib + 1, hard.Count - 1)];
                double a0 = h0(dk), w = h1(dk) - a0;
                if (Math.Abs(w) < 1e-12) continue;
                double g = (o - a0) / w;
                all.Insert(at, d => { double a = h0(d); return a + (h1(d) - a) * g; });
                baseOf.Insert(at, ib);
            }
        }
        for (int pn = 0; pn < panels; pn++)
        {
            pmid[pn] = (bnd[pn] + bnd[pn + 1]) / 2;
            pcols[pn] = all;
        }
        Func<double, Vec3> Row(int pn, int b, double v)
        {
            var ha = pcols[pn][b]; var hb = pcols[pn][b + 1];
            Func<double, double> off = d => { double a = ha(d); return a + (hb(d) - a) * v; };
            var (db, de) = EndsVar(off, InsetB, InsetE);
            // panel boundaries at the kink lines, clamped to this row's ends (rows beyond a skewed end have no length in that panel)
            double d0 = pn == 0 ? db : Math.Clamp(kinks[pn - 1], db, de), d1 = pn == panels - 1 ? de : Math.Clamp(kinks[pn], db, de);
            return u =>
            {
                double d = d0 + (d1 - d0) * u, o = off(d);
                return _a.PointAtDistance(d, o).WithZ(Top(d, o));
            };
        }
        // OPTIONAL sloped overhang soffit: an exact bottom (thickness linear in the offset within each band), fitted with the top
        Func<int, int, double, Func<double, Vec3>>? bottomRow = null;
        if (SlopedOverhang != null)
            bottomRow = (pn, b, v) =>
            {
                var ha = pcols[pn][b]; var hb = pcols[pn][b + 1];
                Func<double, double> off = d => { double a = ha(d); return a + (hb(d) - a) * v; };
                var (db, de) = EndsVar(off, InsetB, InsetE);
                double d0 = pn == 0 ? db : Math.Clamp(kinks[pn - 1], db, de), d1 = pn == panels - 1 ? de : Math.Clamp(kinks[pn], db, de);
                // one-sided thickness at the band's own edge columns: the soffit steps at the overhang root when the root is thicker than the slab
                double side = v <= 0 ? 1 : v >= 1 ? -1 : 0;
                return u =>
                {
                    double d = d0 + (d1 - d0) * u, o = off(d);
                    double dir = Math.Sign(hb(d) - ha(d));
                    return _a.PointAtDistance(d, o).WithZ(Top(d, o) - SlabT(o + side * dir * 1e-7));
                };
            };
        var slab = NurbsSlab.Create(panels, pn => pcols[pn].Count - 1, Row, Deck.Thickness, _fitTol, out string reason, 32, bottomRow);
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
                bot[i, k] = slab.Bottom != null ? slab.BottomAt(pn, where[pn][k].b, where[pn][k].v, u) : new Vec3(p.X, p.Y, p.Z - Deck.Thickness);
            }
        _nurbs!.Built("deck slab", slab.MaxDeviation);
        return slab;
    }

    /// <summary>
    /// The exact-mode slab grid without its collapsed cells: where a column has no length in a panel (beyond a skewed / kinked deck end) its samples
    /// coincide, so the grid has zero-area triangles and repeated vertices. Vertices closer than <paramref name="tol"/> are merged and triangles that
    /// repeat a vertex are left out: what remains is the same surface as a closed, edge-manifold mesh (every edge shared by two triangles).
    /// </summary>
    internal static Mesh WeldCollapsed(Mesh m, double tol = 1e-9)
    {
        var res = new Mesh();
        var map = new int[m.Positions.Count];
        var grid = new Dictionary<(long, long, long), List<int>>();
        double cell = tol * 4;
        for (int i = 0; i < map.Length; i++)
        {
            var p = m.Positions[i];
            long gx = (long)Math.Floor(p.X / cell), gy = (long)Math.Floor(p.Y / cell), gz = (long)Math.Floor(p.Z / cell);
            int hit = -1;
            for (long a = gx - 1; a <= gx + 1 && hit < 0; a++)
                for (long b = gy - 1; b <= gy + 1 && hit < 0; b++)
                    for (long c = gz - 1; c <= gz + 1 && hit < 0; c++)
                        if (grid.TryGetValue((a, b, c), out var l))
                            foreach (int v in l) if ((res.Positions[v] - p).Length <= tol) { hit = v; break; }
            if (hit < 0)
            {
                hit = res.Add(p);
                if (!grid.TryGetValue((gx, gy, gz), out var l2)) grid[(gx, gy, gz)] = l2 = new List<int>();
                l2.Add(hit);
            }
            map[i] = hit;
        }
        for (int t = 0; t < m.TriangleCount; t++)
        {
            int a = map[m.Indices[3 * t]], b = map[m.Indices[3 * t + 1]], c = map[m.Indices[3 * t + 2]];
            if (a != b && b != c && a != c) res.Tri(a, b, c);
        }
        return res;
    }

    /// <summary>Haunch along one girder as a NURBS sweep (null = today's loft). Corners: flange edges at top of flange, deck bottom above them.</summary>
    private bool NurbsHaunch(Mesh mesh, Framing.GirderResult g, Func<double, (double hw, double tf)> seg)
    {
        var path = g.Path;
        Vec3[] Ring(double s, (double hw, double tf) sg)
        {
            var (p, t) = path.ExactAt(s);
            var right = new Vec3(t.Right.X, t.Right.Y, 0);
            var pl = (p + right * -sg.hw).XY;
            var pr = (p + right * sg.hw).XY;
            double ztf = p.Z + sg.tf;
            return new[] { pl.WithZ(ztf), pr.WithZ(ztf), pr.WithZ(DeckBottomAt(pr)), pl.WithZ(DeckBottomAt(pl)) };
        }
        return NurbsHaunch(mesh, g, s => seg(s), (s, sg) => Ring(s, sg));
    }

    /// <summary>Haunch over one flange of a TUB girder (side -1 / +1) as a NURBS sweep: the flange edges in the tub's rotated section frame at the
    /// top of the flange (exact girder path, deck cross slope), the deck soffit above them (as the loft of <c>BuildTubHaunches</c>).</summary>
    private bool NurbsTubHaunch(Mesh mesh, Framing.GirderResult g, int side, double halfSpacing, bool rotate)
    {
        var path = g.Path;
        Vec3[] Ring(double s, (double hw, double tf) sg)
        {
            var (p, t) = path.ExactAt(s);
            var f = SBM.Core.Steel.TubFrame.Create(p, t, rotate ? SBM.Core.Steel.TubSection.DeckSlope(_f, p.XY) : 0);
            var a = f.At(side * halfSpacing - sg.hw, sg.tf);
            var b = f.At(side * halfSpacing + sg.hw, sg.tf);
            return new[] { a, b, b.XY.WithZ(DeckBottomAt(b.XY)), a.XY.WithZ(DeckBottomAt(a.XY)) };
        }
        return NurbsHaunch(mesh, g, s => g.PlateAt(SBM.Core.Bridge.GirderPlate.TopFlange, s) is { } w ? (w.Width / 2, w.Thickness) : (0, 0), Ring);
    }

    /// <summary>A haunch as NURBS sweeps of the ring function <paramref name="ring"/> (bottom corners a, b at the flange; top corners at the deck soffit
    /// above them) for the flange size <paramref name="seg"/> at a path distance; the mesh is tessellated from the sweeps (false = keep the loft).</summary>
    private bool NurbsHaunch(Mesh mesh, Framing.GirderResult g, Func<double, (double hw, double tf)> seg, Func<double, (double hw, double tf), Vec3[]> ring)
    {
        var path = g.Path;
        var ss = path.S;
        Vec3[] Ring(double s, (double hw, double tf) sg) => ring(s, sg);
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
        var sweeps = new List<(NurbsSweep sw, Func<double, double>? sOf)>();
        double In(Vec2 p) => Math.Min(DR.FromBegin(p), DR.FromEnd(p));
        foreach (var (ia, ib) in runs)
        {
            if (ib <= ia) continue;
            // As the loft path (ClipHaunchRings): each bottom corner stops where it crosses a (skewed / kinked) deck end, so no haunch projects past
            // the deck. Corner c runs over its own path range [lo_c, hi_c]; u in [0, 1] maps to both ranges. Runs inside the deck are unchanged.
            double s0 = ss[ia], s1 = ss[ib];
            var lo = new double[2]; var hi = new double[2];
            bool clip = false, empty = false;
            for (int c = 0; c < 2; c++)
            {
                double G(double s) => In(Exact(s)[c].XY);
                lo[c] = s0; hi[c] = s1;
                if (G(s0) < -1e-6) { lo[c] = Cut(G, s0, s1, true); clip = true; }
                if (G(s1) < -1e-6) { hi[c] = Cut(G, s0, s1, false); clip = true; }
                if (!(hi[c] > lo[c] + 1e-4)) empty = true;
            }
            if (empty) { _nurbs!.Fallback("haunch", $"{g.Name}: outside the deck ends"); return false; }
            NurbsSweep? sw;
            string reason;
            if (!clip) sw = NurbsSweep.Create(4, Exact, s0, s1, breaks, _fitTol, out reason);
            else
            {
                double SA(double u) => lo[0] + (hi[0] - lo[0]) * u;
                double SB(double u) => lo[1] + (hi[1] - lo[1]) * u;
                sw = NurbsSweep.Create(4, u => { var ra = Exact(SA(u)); var rb = Exact(SB(u)); return new[] { ra[0], rb[1], rb[2], ra[3] }; }, 0, 1,
                    Array.Empty<double>(), _fitTol, out reason, 0.05);
            }
            if (sw == null) { _nurbs!.Fallback("haunch", $"{g.Name}: {reason}"); return false; }
            sweeps.Add((sw, clip ? u => lo[0] + (hi[0] - lo[0]) * u : null));
        }
        var sec = new[] { new Vec2(-1, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(-1, 1) };
        foreach (var (sw, sOf) in sweeps)
        {
            List<double> us;
            if (_geo!.ChordTolerance > 0) us = sw.Samples(_geo.ChordTolerance);
            else if (sOf == null) us = HaunchSamples(ss.Where(s => s >= sw.U0 - 1e-9 && s <= sw.U1 + 1e-9).ToList());
            else
            {
                // today's stations mapped onto u (corner 0's range), the clipped ends included
                double a0 = sOf(0), a1 = sOf(1);
                var st = new List<double> { a0 };
                st.AddRange(ss.Where(s => s > a0 + 1e-6 && s < a1 - 1e-6));
                st.Add(a1);
                us = HaunchSamples(st).Select(s => (s - a0) / (a1 - a0)).ToList();
            }
            int tri0 = mesh.TriangleCount;
            MeshBuilder.AddLoft(mesh, sec, us.Select(sw.Ring).ToList());
            mesh.AddNurbs(sw.ToSolid("haunch"), tri0);
            _nurbs!.Built("haunch", sw.MaxDeviation);
        }
        return true;
    }

    /// <summary>Where g changes sign between a and b (bisection): from the start (g(a) &lt; 0 &lt;= g(b)) or from the end.</summary>
    private static double Cut(Func<double, double> g, double a, double b, bool fromStart)
    {
        double lo = a, hi = b;
        // first inside sample from that end (g may stay positive in the middle only)
        int n = 64;
        if (fromStart) { for (int i = 1; i <= n; i++) { double x = a + (b - a) * i / n; if (g(x) >= 0) { hi = x; lo = a + (b - a) * (i - 1) / n; break; } } }
        else { for (int i = 1; i <= n; i++) { double x = b - (b - a) * i / n; if (g(x) >= 0) { lo = x; hi = b - (b - a) * (i - 1) / n; break; } } }
        for (int k = 0; k < 60; k++)
        {
            double m = (lo + hi) / 2;
            bool inside = g(m) >= 0;
            if (fromStart) { if (inside) hi = m; else lo = m; }
            else { if (inside) lo = m; else hi = m; }
        }
        return fromStart ? hi : lo;
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

    /// <summary>Exact geometry ON: records the exact sweep of a loft the generator built (corner curves = the generator's exact corner functions of
    /// u in [0, 1], ruled faces between them, planar ends) on the mesh's triangles from <paramref name="tri0"/>; the mesh itself is NOT changed.
    /// False (reason reported) when the fit does not reach the tolerance.</summary>
    private bool RecordSweep(Mesh mesh, int tri0, int corners, Func<double, Vec3[]> exact, IEnumerable<double> breaks, string kind)
    {
        if (tri0 == 0 && mesh.HasExactSolids) return true;   // a straight loft already recorded its exact extrusion
        var sw = NurbsSweep.Create(corners, exact, 0, 1, breaks, _fitTol, out string reason, 0.1);
        if (sw == null) { _nurbs!.Fallback(kind, reason); return false; }
        mesh.AddNurbs(sw.ToSolid(kind), tri0);
        _nurbs!.Built(kind, sw.MaxDeviation);
        return true;
    }

    /// <summary>
    /// Lines across a deck end where a block between insets <paramref name="x0"/> .. <paramref name="x1"/> from the deck-end face may kink, left to
    /// right, as functions of the inset x giving an offset function of the alignment distance: the deck edges moved in by li / ri, the crown,
    /// gutter and cross-slope break lines, the break points of the (inset) kinked deck end and the radial finished-grade kink lines that cross the
    /// block (cross-slope / edge table stations). The range must not contain an inset where the
    /// end's break points change (<see cref="EndSplits"/>): each break point is followed by its vertex index in the inset polyline, evaluated a
    /// hair inside the range at its ends, and clamped to the block's lateral ends.
    /// </summary>
    private List<Func<double, Func<double, double>>>? EndLines(bool begin, double x0, double x1, double li, double ri, out string reason)
    {
        reason = "";
        var hard = HardColumns(false);
        var L = hard[0]; var R = hard[^1];
        Func<double, double> l = d => L(d) + li, r = d => R(d) - ri;
        var lines = new List<Func<double, Func<double, double>>> { _ => l };
        foreach (var h in hard.Skip(1).Take(hard.Count - 2)) lines.Add(_ => h);
        // insets may run either way (a device envelope reaches past the deck-end face: negative insets)
        double xm = (x0 + x1) / 2, xlo = Math.Min(x0, x1), xhi = Math.Max(x0, x1), dx = Math.Min(2e-6, (xhi - xlo) / 4);
        double Cl(double x) => Math.Clamp(x, xlo + dx, xhi - dx);
        if (EndPoly(begin, xm) is { } pm)
        {
            int np = pm.Points.Count;
            foreach (int i in InsideVertices(pm, li, ri))
            {
                int ii = i;
                lines.Add(x =>
                {
                    var p = EndPoly(begin, Cl(x))!;
                    double o = p.Points.Count == np ? _a.Inverse(p.Points[ii]).Offset : _a.Inverse(pm.Points[ii]).Offset;
                    return d => { double a = l(d), b = r(d); return a <= b ? Math.Clamp(o, a, b) : o; };
                });
            }
        }
        // finished-grade kink lines crossing the block: the j-th point where the radial line at dk meets the end inset by x (a constant offset per x,
        // so the row crossing the end at x lies on the radial line)
        foreach (double dk in EndKinkDistances(begin, x0, x1, li, ri))
        {
            int nr = KinkRoots(begin, dk, xm, li, ri).Count;
            for (int j = 0; j < nr; j++)
            {
                int jj = j; double dd = dk;
                lines.Add(x =>
                {
                    var rs = KinkRoots(begin, dd, Cl(x), li, ri);
                    double o = rs.Count == nr ? rs[jj] : KinkRoots(begin, dd, xm, li, ri)[jj];
                    return d => { double a = l(d), b = r(d); return a <= b ? Math.Clamp(o, a, b) : o; };
                });
            }
        }
        lines.Add(_ => r);
        // order across at the mid inset (offsets where each line meets the end), drop lines outside the edges and duplicates
        double At(Func<double, Func<double, double>> f) { var off = f(xm); double d = EndD(off, begin, xm); return off(d); }
        double lo = At(lines[0]), hi = At(lines[^1]);
        var inner = lines.Skip(1).Take(lines.Count - 2).Select(f => (f, o: At(f))).Where(t => t.o > lo + 1e-4 && t.o < hi - 1e-4).OrderBy(t => t.o).ToList();
        var res = new List<Func<double, Func<double, double>>> { lines[0] };
        double last = lo;
        foreach (var (f, o) in inner) if (o - last > 1e-4) { res.Add(f); last = o; }
        if (hi - last <= 1e-4) res.RemoveAt(res.Count - 1);
        res.Add(lines[^1]);
        return res;
    }

    /// <summary>Alignment distances of the finished-grade kink lines (cross-slope / pivot rows, grade breaks, vertical curve ends, deck edge table
    /// rows) that may cross a block at a deck end between insets x0 and x1 (the end's distance range there, 0.5 m margin).</summary>
    private List<double> EndKinkDistances(bool begin, double x0, double x1, double li, double ri)
    {
        var hard = HardColumns(false);
        var L = hard[0]; var R = hard[^1];
        Func<double, double> l = d => L(d) + li, r = d => R(d) - ri;
        var ds = new List<double>();
        foreach (double x in new[] { x0, x1 })
        {
            ds.Add(EndD(l, begin, x)); ds.Add(EndD(r, begin, x));
            if (EndPoly(begin, x) is { } p) ds.AddRange(p.Points.Skip(1).Take(p.Points.Count - 2).Select(q => _a.Inverse(q).Distance));
        }
        double lo = ds.Min() - 0.5, hi = ds.Max() + 0.5;
        var sta = KinkStations(_a);
        if (Deck.LeftEdgeTable is { } lt) sta.AddRange(lt.Select(q => q.Station));
        if (Deck.RightEdgeTable is { } rt) sta.AddRange(rt.Select(q => q.Station));
        return sta.Select(s => _a.StationToDistance(s)).Where(d => d > lo && d < hi).Distinct().OrderBy(d => d).ToList();
    }

    /// <summary>Offsets where the radial kink line at distance dk meets the deck end moved in by the inset x, strictly inside the block's lateral ends.</summary>
    private List<double> KinkRoots(bool begin, double dk, double x, double li, double ri)
    {
        var hard = HardColumns(false);
        double l = hard[0](dk) + li, r = hard[^1](dk) - ri;
        if (!(r - l > 2e-3)) return new List<double>();
        var poly = EndPoly(begin, x);
        var nrm = begin ? DR.BeginLineDirection.Left : -DR.EndLineDirection.Left;
        var lp = (begin ? DR.BeginLinePoint : DR.EndLinePoint) + nrm * x;
        double G(double o) { var p = _a.PointAtDistance(dk, o); return poly != null ? poly.SignedDistance(p) : Vec2.Dot(p - lp, nrm); }
        return Numerics.FindRoots(G, l, r, 200).Where(o => o > l + 1e-3 && o < r - 1e-3).OrderBy(o => o).ToList();
    }

    /// <summary>Indices of the interior break points of an inset deck-end polyline that lie strictly inside the block (1 mm from its lateral ends: a
    /// break point ON a deck edge, e.g. where a flared edge meets the end, is the block's corner, not a kink across it).</summary>
    private List<int> InsideVertices(Framing.DeckEndPolyline p, double li, double ri)
    {
        var hard = HardColumns(false);
        var L = hard[0]; var R = hard[^1];
        var res = new List<int>();
        for (int i = 1; i + 1 < p.Points.Count; i++)
        {
            var so = _a.Inverse(p.Points[i]);
            if (so.Offset > L(so.Distance) + li + 1e-3 && so.Offset < R(so.Distance) - ri - 1e-3) res.Add(i);
        }
        return res;
    }

    /// <summary>Insets strictly between x0 and x1 where the (inset) kinked deck end changes its break points inside the block: a break point
    /// crosses a lateral end of the block or a middle segment vanishes. Each is rounded to a micron (the inset polylines are cached per micron).</summary>
    private List<double> EndSplits(bool begin, double x0, double x1, double li, double ri)
    {
        var kinks = EndKinkDistances(begin, x0, x1, li, ri);
        var hardCols = HardColumns(false);
        // gap between the fixed columns (edges, crown / pivot, gutter and break lines) that a moving line lies in: a break point or a grade kink line
        // crossing a fixed column inside the block swaps the panel order (inverted panel, folded faces) - split there too (I-80 U3 East Abut: a cross-slope
        // row meeting the skewed end slides ~5 m across per m of inset)
        int Gap(double d, double o) => hardCols.Count(h => h(d) < o - 1e-9);
        string Sig(double x)
        {
            var sb = new System.Text.StringBuilder();
            if (EndPoly(begin, x) is { } p)
            {
                sb.Append(p.Points.Count).Append(':');
                foreach (int i in InsideVertices(p, li, ri)) { var so = _a.Inverse(p.Points[i]); sb.Append(i).Append('@').Append(Gap(so.Distance, so.Offset)).Append(','); }
            }
            sb.Append('|');
            foreach (double dk in kinks)
            {
                var rs = KinkRoots(begin, dk, x, li, ri);
                sb.Append(rs.Count).Append('[').Append(string.Join(",", rs.Select(o => Gap(dk, o)))).Append(']');
            }
            return sb.ToString();
        }
        double lo = Math.Min(x0, x1), hi = Math.Max(x0, x1), e = Math.Min(2e-6, (hi - lo) / 4);
        var res = new List<double>();
        const int n = 64;
        string prev = Sig(lo + e);
        double xa = lo;
        for (int k = 1; k <= n; k++)
        {
            double xb = k == n ? hi - e : lo + (hi - lo) * k / n;
            string cur = Sig(xb);
            if (cur != prev)
            {
                double a = xa, b = xb;
                while (b - a > 1e-7) { double m = (a + b) / 2; if (Sig(m) == prev) a = m; else b = m; }
                double xs = Math.Round((a + b) / 2 * 1e6) / 1e6;
                if (xs > lo + 1e-5 && xs < hi - 1e-5) res.Add(xs);
            }
            prev = cur; xa = xb;
        }
        // in the direction x0 -> x1
        return x1 >= x0 ? res : Enumerable.Reverse(res).ToList();
    }

    /// <summary>
    /// Exact geometry ON: records the exact block of a deck-end element (joint blockout, shelf, device envelope, end block piece) on its mesh: inset x
    /// from the deck-end face between the values <paramref name="xs"/> (bands at the soffit breaks), across the deck between the end lines
    /// (<see cref="EndLines"/>: panels), top / bottom = drops below finished grade as functions of x. Faces: NURBS tops and bottoms fitted to the exact
    /// points (deck-end crossing of each offset curve, finished grade), ruled faces at the insets and at the lateral ends. Where a kinked deck end
    /// changes its break points across the block (<see cref="EndSplits"/>) the block is recorded as several exact solids split at those insets (one
    /// element; all or nothing). The mesh is unchanged.
    /// </summary>
    private bool RecordEndBlock(Mesh mesh, int tri0, int triCount, bool begin, double[] xs, Func<double, double> topDrop, Func<double, double> botDrop,
        double li, double ri, string kind)
    {
        var splits = EndSplits(begin, xs[0], xs[^1], li, ri);
        var cuts = new List<double> { xs[0] };
        cuts.AddRange(splits);
        cuts.Add(xs[^1]);
        int n0 = mesh.Nurbs?.Count ?? 0;
        for (int c = 0; c + 1 < cuts.Count; c++)
        {
            double a = cuts[c], b = cuts[c + 1];
            double sa = Math.Min(a, b), sb = Math.Max(a, b);
            var sub = new[] { a }.Concat(xs.Where(x => x > sa + 1e-6 && x < sb - 1e-6)).Append(b).ToArray();
            var lines = EndLines(begin, a, b, li, ri, out string why);
            if (lines == null) { mesh.DropNurbsFrom(n0); _nurbs!.Fallback(kind, why); return false; }
            // end crossing of each line per inset (shared by the neighbouring panels and by the top / bottom fits)
            var endAt = new System.Collections.Concurrent.ConcurrentDictionary<(int, long), double>();
            double LineEnd(int k, double x, Func<double, double> f) => endAt.GetOrAdd((k, BitConverter.DoubleToInt64Bits(x)), _ => EndD(f, begin, x));
            Func<double, Vec3> Row(int pn, int bb, double v, bool bottom)
            {
                double x = sub[bb] + (sub[bb + 1] - sub[bb]) * v;
                var A = lines[pn](x); var B = lines[pn + 1](x);
                double drop = bottom ? botDrop(x) : topDrop(x);
                // the row's end crossings at its two lateral ends by the full search; between them the crossing stays on one end segment (panels are
                // bounded by the break-point lines), so a secant from the interpolated guess finds it (the full search scans the whole extended
                // polyline at every sample point: most of the regeneration time of kinked-end blocks)
                var poly = EndPoly(begin, x);
                double dA = LineEnd(pn, x, A), dB = LineEnd(pn + 1, x, B);
                return u =>
                {
                    Func<double, double> off = d => { double a = A(d); return a + (B(d) - a) * u; };
                    double d = poly != null && EndSecant(poly, off, dA + (dB - dA) * u) is double ds ? ds : EndD(off, begin, x), o = off(d);
                    return _a.PointAtDistance(d, o).WithZ(Top(d, o) - drop);
                };
            }
            var slab = NurbsSlab.Create(lines.Count - 1, _ => sub.Length - 1, (p, bb, v) => Row(p, bb, v, false), 0, _fitTol, out string reason, 128, (p, bb, v) => Row(p, bb, v, true));
            if (slab == null) { mesh.DropNurbsFrom(n0); _nurbs!.Fallback(kind, reason); return false; }
            // the first solid lists the element's triangles, further ones none (count 0: the same triangle range)
            if (c == 0) mesh.AddNurbs(slab.ToSolid(kind), tri0, triCount);
            else mesh.AddNurbs(slab.ToSolid(kind), tri0, 0);
            _nurbs!.Built(kind, slab.MaxDeviation);
        }
        return true;
    }

    /// <summary>Alignment distance where the offset curve crosses the (inset) kinked deck end, by secant iteration from a guess near it; null when it
    /// does not converge (residual 1e-11 m or step 1e-10 m) within 30 steps (the caller then uses the full search <see cref="EndD"/>).</summary>
    private double? EndSecant(Framing.DeckEndPolyline poly, Func<double, double> off, double guess)
    {
        double F(double d) => poly.SignedDistance(_a.PointAtDistance(d, off(d)));
        double d0 = guess, d1 = guess + 1e-3, f0 = F(d0), f1 = F(d1);
        for (int it = 0; it < 30; it++)
        {
            if (Math.Abs(f1) < 1e-11) return d1;
            double den = f1 - f0;
            if (Math.Abs(den) < 1e-15) return null;
            double d2 = d1 - f1 * (d1 - d0) / den;
            if (Math.Abs(d2 - guess) > 2) return null;   // ran off: not the crossing near the guess
            if (Math.Abs(d2 - d1) < 1e-10) return d2;     // converged (the residual can stall at the alignment's evaluation noise)
            d0 = d1; f0 = f1; d1 = d2; f1 = F(d1);
        }
        return Math.Abs(f1) < 1e-11 ? d1 : null;
    }

    /// <summary>Exact geometry ON: the deck end block (end dam diaphragm / thickened slab end) as exact blocks: soffit below, the slab soffit (or the
    /// blockout bottom next to the deck-end face) above. Every vertical step (of the top at the blockout edge, of the soffit polyline) ends one block and
    /// starts the next (one element, several exact solids; all or nothing).</summary>
    private void RecordEndBlockSection(Mesh mesh, bool begin, List<SBM.Core.Bridge.EndBlockPoint> soffit, double bt, double slab, double d, double li, double ri)
    {
        const string kind = "deck end block";
        double xEnd = soffit[^1].X;
        if (Math.Abs(soffit[^1].Drop - slab) > 1e-6 || soffit.Zip(soffit.Skip(1)).Any(t => t.Second.X < t.First.X - 1e-9))
        { _nurbs!.Fallback(kind, "soffit does not end on the slab soffit / runs back toward the deck end"); return; }
        // soffit drop at x, from the side of the step that lies toward 'toward' (vertical soffit segments are steps)
        double Soff(double x, double toward)
        {
            double xe = x + Math.Sign(toward - x) * 1e-7;
            for (int i = 0; i + 1 < soffit.Count; i++)
            {
                double a = soffit[i].X, b = soffit[i + 1].X;
                if (b - a < 1e-9) continue;
                if (xe <= b || i + 2 == soffit.Count)
                    return soffit[i].Drop + (soffit[i + 1].Drop - soffit[i].Drop) * Math.Clamp((xe - a) / (b - a), 0, 1);
            }
            return soffit[^1].Drop;
        }
        var steps = new List<double>();
        for (int i = 0; i + 1 < soffit.Count; i++) if (soffit[i + 1].X - soffit[i].X < 1e-9 && soffit[i].X > 1e-9 && soffit[i].X < xEnd - 1e-9) steps.Add(soffit[i].X);
        if (bt > slab + 1e-6) steps.Add(d);
        var cuts = new List<double> { 0 };
        cuts.AddRange(steps.Distinct().OrderBy(x => x));
        cuts.Add(xEnd);
        var verts = soffit.Select(q => q.X).ToList();
        int tri = mesh.TriangleCount;
        int nurbs0 = mesh.Nurbs?.Count ?? 0;
        for (int k = 0; k + 1 < cuts.Count; k++)
        {
            double a = cuts[k], b = cuts[k + 1], mid = (a + b) / 2;
            if (b - a < 1e-6) continue;
            double top = mid < d ? Math.Max(bt, slab) : slab;
            var xs = new[] { a }.Concat(verts.Where(x => x > a + 1e-6 && x < b - 1e-6)).Append(b).Distinct().ToArray();
            Func<double, double> bot = x => Soff(x, mid);
            if (xs.Any(x => bot(x) < top - 1e-9) || xs.Take(xs.Length - 1).Any(x => bot(x) < top + 1e-6 && x > a + 1e-6))
            { mesh.DropNurbsFrom(nurbs0); _nurbs!.Fallback(kind, "soffit above the slab soffit"); return; }
            if (!RecordEndBlock(mesh, 0, mesh.Nurbs?.Count > nurbs0 ? 0 : tri, begin, xs, _ => top, bot, li, ri, kind))
            {
                mesh.DropNurbsFrom(nurbs0);   // all or nothing: the element stays its mesh
                return;
            }
        }
    }

    /// <summary>Kink stations of the finished grade between two alignment distances, as fractions of [d0, d1] (sweep parameters).</summary>
    private double[] GradeBreaks(double d0, double d1)
    {
        if (!(d1 > d0)) return Array.Empty<double>();
        var sta = KinkStations(_a);
        if (Deck.LeftEdgeTable is { } lt) sta.AddRange(lt.Select(r => r.Station));
        if (Deck.RightEdgeTable is { } rt) sta.AddRange(rt.Select(r => r.Station));
        return sta.Select(st => (_a.StationToDistance(st) - d0) / (d1 - d0)).Where(u => u > 1e-6 && u < 1 - 1e-6).Distinct().OrderBy(u => u).ToArray();
    }
}
