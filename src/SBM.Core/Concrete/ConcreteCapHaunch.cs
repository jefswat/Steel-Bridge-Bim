using System.Globalization;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>Concrete haunch over an integral steel pier cap (<see cref="CapHaunchDefinition"/>): solid pieces, volume and what was measured.</summary>
public sealed class CapHaunchResult
{
    public required string Name { get; init; }
    public required string Support { get; init; }
    /// <summary>Cap assembly mark (e.g. "CAP2").</summary>
    public required string CapMark { get; init; }
    public required string Source { get; init; }
    /// <summary>All pieces in one mesh (closed shells, one per piece).</summary>
    public required Mesh Mesh { get; init; }
    /// <summary>The closed pieces (between the deck edges, girder haunches, cover plate ends and cross-slope breaks) - IFC items.</summary>
    public List<Mesh> Pieces { get; } = new();
    public double Width { get; init; }
    /// <summary>Haunch extent along the cap (plan S at the CL cap) and the depth range (deck soffit to the cap steel, vertical).</summary>
    public double S0 { get; set; }
    public double S1 { get; set; }
    public double MinDepth { get; set; } = double.NaN;
    public double MaxDepth { get; set; } = double.NaN;
    /// <summary>Depth at the CL cap in the middle of each bay (bay label, depth).</summary>
    public List<(string Bay, double Depth)> BayDepths { get; } = new();
    public List<string> Notes { get; } = new();
    public double Volume => Math.Abs(Mesh.Volume());
}

public sealed partial class ConcreteGenerator
{
    /// <summary>
    /// Haunches over integral steel pier caps (<see cref="IntegralSteelCapDefinition.Haunch"/>). The cap is taken from <see cref="IntegralCapFrame"/>
    /// (the same frame the steel detailer builds it in): bottom = the cap top steel (top flange top, or the top of a top cover plate), plumb sides at
    /// +-Width/2 from the CL cap, top = deck soffit, ends at the deck edges (outer face of a deck widening of a barrier zone at this support where it
    /// covers the haunch), interrupted by the girder haunches (they already fill from the girder top flange to the soffit). Pieces are split at
    /// those lines, the top cover plate ends (the bottom steps) and the cross-slope break lines (the top kinks), each a closed grid solid.
    /// </summary>
    private void BuildCapHaunches()
    {
        for (int j = 0; j < _def.Supports.Count; j++)
        {
            var cd = _def.Supports[j].IntegralSteelCap;
            if (cd?.Haunch is not { } hd) continue;
            try { BuildCapHaunch(j, cd, hd); }
            catch (Exception ex) { Warn($"{_def.Supports[j].Name} cap haunch not built: {ex.Message}"); }
        }
    }

    private void BuildCapHaunch(int j, IntegralSteelCapDefinition cd, CapHaunchDefinition hd)
    {
        var sl = _f.SupportLine(j);
        string src = hd.Source.Length > 0 ? hd.Source : cd.Source.Length > 0 ? cd.Source : "plan";
        foreach (var u in hd.Unresolved) Warn($"{sl.Name} cap haunch: {u}");
        var cap = IntegralCapFrame.Create(_f, j);
        if (cap == null)
        {
            Warn(UnresolvedFlag.Text(src, $"{sl.Name} integral steel cap haunch", "the cap has no CL bearing positions / top of bearing elevations, so its top is not known", "The haunch is not built."));
            return;
        }
        double W = hd.Width > 0 ? hd.Width / 2 : cap.TopHalfWidth;
        double soleHalf = _def.BearingsAt(j).SolePlateLength / 2;
        // top cover plates: local x range and thickness; their ends in plan at the top face
        var covers = cd.CoverPlates.Where(c => c.Top).Select(c =>
        {
            var (s0, s1) = cap.CoverRange(c, soleHalf);
            return (s0, s1, t: c.Thickness, p0: cap.PlanS(cap.Xc(s0) + cap.M * cap.ZTop, cap.ZTop + c.Thickness), p1: cap.PlanS(cap.Xc(s1) + cap.M * cap.ZTop, cap.ZTop + c.Thickness));
        }).ToList();
        // steel top (local z) at a plan S on the cap top: flange, or the cover plate whose top face covers it
        double SteelC(double s) => cap.ZTop + covers.Where(c => s >= c.p0 - 1e-9 && s <= c.p1 + 1e-9).Select(c => c.t).DefaultIfEmpty(0).Max();
        // the same rule the detailer uses for its studs (S in the cover's own range): steel top under a stud line / stirrup
        double SteelCAtLine(double s) => cap.ZTop + covers.Where(c => s >= c.s0 - 1e-6 && s <= c.s1 + 1e-6).Select(c => c.t).DefaultIfEmpty(0).Max();

        // ---------------------------------------------------------------- deck soffit and edges
        var bar = Deck.Barrier;
        double dl = 0.3;
        BarrierZone? Widening(int side) => bar.Type == BarrierType.Custom && (side < 0 ? bar.Left : bar.Right)
            ? bar.Zones?.Where(z => z.DeckWidening > 0 && z.Support.Equals(sl.Name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(z => z.DeckWideningLength).FirstOrDefault()
            : null;
        double EdgeAt(int side, double d) => side < 0 ? LeftAtD(d) : RightAtD(d);
        // widening (custom barrier zone, perpendicular to the deck): bottom face below the slab edge; u = inward distance from the slab edge (< 0 outside)
        double WideningBottom(int side, double d, double u)
        {
            double oE = EdgeAt(side, d), zE = Top(d, oE);
            double sIn = (Top(d, oE - side * dl) - zE) / dl;
            return zE + sIn * u - Deck.OverhangThickness * Math.Sqrt(1 + sIn * sIn);
        }
        double WideningOut(int side, double d, BarrierZone z)
        {
            double oE = EdgeAt(side, d), zE = Top(d, oE);
            double sIn = (Top(d, oE - side * dl) - zE) / dl, c = 1 / Math.Sqrt(1 + sIn * sIn);
            return z.DeckWidening * c - Deck.OverhangThickness * sIn * c;     // outward reach of the widening's bottom outer edge
        }
        var wid = new Dictionary<int, BarrierZone?>();
        foreach (int side in new[] { -1, 1 })
        {
            var z = Widening(side);
            if (z != null && !bar.PerpendicularToDeck)
            {
                Warn($"{sl.Name} cap haunch: the deck widening '{z.Name}' is not perpendicular to the deck; the haunch ends at the slab edge there.");
                z = null;
            }
            if (z != null)
            {
                // the widening is centred on the support line at the edge, DeckWideningLength long along the edge: it must cover the haunch width
                if (z.DeckWideningLength / 2 < W - 1e-6)
                {
                    Warn($"{sl.Name} cap haunch: the deck widening '{z.Name}' ({FtIn(z.DeckWideningLength)} long) does not cover the {FtIn(2 * W)} haunch; the haunch ends at the slab edge on that side.");
                    z = null;
                }
            }
            wid[side] = z;
        }
        (double d, double o) DO(double s, double n) { var iv = _a.Inverse(cap.Plan(s, n)); return (iv.Distance, iv.Offset); }
        double Soffit(double s, double n)
        {
            var (d, o) = DO(s, n);
            double l = LeftAtD(d), r = RightAtD(d);
            if (o < l - 1e-9 && wid[-1] != null) return WideningBottom(-1, d, o - l);
            if (o > r + 1e-9 && wid[1] != null) return WideningBottom(1, d, r - o);
            return Top(d, Math.Clamp(o, l, r)) - Deck.Thickness;
        }
        // S on the line N = n where the offset reaches target(d) (bisection; the support line crosses the alignment once near the cap)
        double SolveS(double n, Func<double, double> target, double guess)
        {
            double F(double s) { var (d, o) = DO(s, n); return o - target(d); }
            double lo = guess - 5, hi = guess + 5, flo = F(lo), fhi = F(hi);
            for (int k = 0; k < 20 && flo * fhi > 0; k++) { lo -= 5 * (k + 1); hi += 5 * (k + 1); flo = F(lo); fhi = F(hi); }
            if (flo * fhi > 0) throw new InvalidOperationException("the support line does not cross the deck line");
            for (int it = 0; it < 80; it++)
            {
                double mid = (lo + hi) / 2, fm = F(mid);
                if ((fm < 0) == (flo < 0)) { lo = mid; flo = fm; } else hi = mid;
            }
            return (lo + hi) / 2;
        }
        double dSup = _a.Inverse(sl.AlignmentPoint).Distance;
        // first guess of S for an offset: the support line runs through the alignment point (S = 0) at the skew to the alignment normal
        double rightDot = Vec2.Dot(cap.Dir, _a.PointAtDistance(dSup, 1) - _a.PointAtDistance(dSup, 0));
        double Guess(double off) => off / (Math.Abs(rightDot) < 0.1 ? 1 : rightDot);
        Func<double, double> EdgeLine(int side) => n =>
        {
            var z = wid[side];
            return SolveS(n, d => EdgeAt(side, d) + (z != null ? side * WideningOut(side, d, z) : 0), Guess(EdgeAt(side, dSup)));
        };

        // ---------------------------------------------------------------- girder haunch footprints across the cap
        var foot = new List<(string name, Func<double, double> a, Func<double, double> b)>();
        foreach (var g in _f.Girders)
        {
            var br = sl.Bearings.ElementAtOrDefault(g.Index);
            int nSpan = g.Spans.Count;
            double sC = br != null && !double.IsNaN(br.PathDistance) ? br.PathDistance : g.DistanceAt(Math.Min(j, nSpan - 1), j == nSpan ? 1 : 0);
            var (p, tg, sta) = g.Path.At(sC);
            double hw = _def.Girders.IsPrestressed ? Prestressed.TxGirderCatalog.Get(_def.Girders.Prestressed).TopWidth / 2 : _def.Girders.Section.SegmentAt(GirderPlate.TopFlange, sta).Width / 2;
            var t = tg.Normalized();
            double tn = Vec2.Dot(t, cap.Nrm), ts = Vec2.Dot(t, cap.Dir);
            if (Math.Abs(tn) < 0.2) { Warn($"{sl.Name} cap haunch: {g.Name} runs nearly along the cap - its haunch is not cut out of the cap haunch."); continue; }
            var right = new Vec2(t.Right.X, t.Right.Y);
            Func<double, double> Side(double sg)
            {
                var q = p.XY + right * (sg * hw);
                double sq = cap.S(q), nq = cap.N(q);
                return n => sq + (n - nq) / tn * ts;
            }
            var sa = Side(-1); var sb = Side(1);
            foot.Add((GirderName(g.Index), n => Math.Min(sa(n), sb(n)), n => Math.Max(sa(n), sb(n))));
        }
        foot.Sort((x, y) => x.a(0).CompareTo(y.a(0)));

        // ---------------------------------------------------------------- cut lines along the cap (functions of n)
        var eL = EdgeLine(-1); var eR = EdgeLine(1);
        if (eL(0) > eR(0)) (eL, eR) = (eR, eL);
        var cuts = new List<(Func<double, double> f, string kind)> { (eL, "deck edge"), (eR, "deck edge") };
        foreach (var fp in foot) { cuts.Add((fp.a, "girder haunch " + fp.name)); cuts.Add((fp.b, "girder haunch " + fp.name)); }
        foreach (var c in covers) { double p0 = c.p0, p1 = c.p1; cuts.Add((_ => p0, "cover plate end")); cuts.Add((_ => p1, "cover plate end")); }
        var kinkOffs = new List<double>();
        foreach (double piv in PivotColumns()) kinkOffs.Add(piv);
        foreach (var gl in StripLines(LeftAtD(dSup), RightAtD(dSup))) kinkOffs.Add(gl);
        foreach (double ko in kinkOffs.Distinct())
        {
            if (ko <= LeftAtD(dSup) + 1e-4 || ko >= RightAtD(dSup) - 1e-4) continue;
            double k0 = ko;
            cuts.Add((n => SolveS(n, _ => k0, Guess(k0)), "cross-slope break"));
        }
        double lo0 = eL(0), hi0 = eR(0);
        var inside = cuts.Where(c => c.f(0) > lo0 + 1e-6 && c.f(0) < hi0 - 1e-6).ToList();
        inside.Insert(0, cuts[0]); inside.Add(cuts[1]);
        inside = inside.OrderBy(c => c.f(0)).ToList();
        // merge cuts that coincide at the CL cap
        var lines = new List<(Func<double, double> f, string kind)>();
        foreach (var c in inside) if (lines.Count == 0 || c.f(0) - lines[^1].f(0) > 1e-5) lines.Add(c);
        var ns = new[] { -W, -W / 2, 0, W / 2, W };
        bool InFoot(double s, double n) => foot.Any(fp => s > fp.a(n) + 1e-6 && s < fp.b(n) - 1e-6);

        var res = new CapHaunchResult
        {
            Name = $"Integral steel pier cap haunch, {sl.Name}", Support = sl.Name, CapMark = cd.Mark, Source = src, Mesh = new Mesh(), Width = 2 * W,
            S0 = lo0, S1 = hi0,
        };
        double minD = double.MaxValue, maxD = double.MinValue;
        int thin = 0;
        for (int i = 0; i + 1 < lines.Count; i++)
        {
            var A = lines[i].f; var B = lines[i + 1].f;
            double mid0 = (A(0) + B(0)) / 2;
            if (InFoot(mid0, 0)) continue;
            // the order of the cut lines must hold across the width
            if (ns.Any(n => B(n) - A(n) < -1e-6)) { Warn($"{sl.Name} cap haunch: the {lines[i].kind} and {lines[i + 1].kind} lines cross within the haunch width; piece at S {FtIn(mid0)} skipped."); continue; }
            double c = SteelC(mid0);
            int thin0 = thin;
            double len = ns.Max(n => B(n) - A(n));
            int ni = Math.Max(2, (int)Math.Ceiling(len / 0.5)) + 1;
            var top = new Vec3[ni, ns.Length];
            var bot = new Vec3[ni, ns.Length];
            for (int k = 0; k < ns.Length; k++)
            {
                double a = A(ns[k]), b = B(ns[k]);
                for (int q = 0; q < ni; q++)
                {
                    double s = a + (b - a) * q / (ni - 1);
                    var xy = cap.Plan(s, ns[k]);
                    double zb = cap.SurfaceZ(s, c), zt = Soffit(s, ns[k]);
                    double dep = zt - zb;
                    minD = Math.Min(minD, dep); maxD = Math.Max(maxD, dep);
                    if (dep < In(1.0 / 16)) { thin++; zt = zb + In(1.0 / 16); }
                    top[q, k] = xy.WithZ(zt);
                    bot[q, k] = xy.WithZ(zb);
                }
            }
            var piece = GridSolid(top, bot);
            // exact geometry ON: the piece as an exact block (along the cap between its cut lines, across the haunch width; top = deck soffit,
            // bottom = cap steel surface); not where the soffit was held 1/16" above the steel (the mesh is then not the exact surface)
            if (_geo != null && thin == thin0)
            {
                // the soffit steps where the deck edge crosses a piece under a deck widening (widening bottom vs slab soffit; the generator does
                // not cut the piece there): the exact description splits the piece at that line into blocks (the mesh is unchanged)
                var bounds = new List<Func<double, double>> { A };
                foreach (int side in new[] { -1, 1 })
                {
                    if (wid[side] == null) continue;
                    int sd = side;
                    Func<double, double> E = n => SolveS(n, d => EdgeAt(sd, d), Guess(EdgeAt(sd, dSup)));
                    try { if (ns.All(n => E(n) > A(n) + 1e-4 && E(n) < B(n) - 1e-4)) bounds.Add(E); }
                    catch (InvalidOperationException) { }
                }
                bounds.Add(B);
                bounds = bounds.OrderBy(f => f(0)).ToList();
                int n0 = piece.Nurbs?.Count ?? 0;
                string? why = null;
                double dev = 0;
                for (int k = 0; k + 1 < bounds.Count; k++)
                {
                    var Ak = bounds[k]; var Bk = bounds[k + 1];
                    Func<double, Vec3> Row(double v, bool bottom)
                    {
                        double n = -W + 2 * W * v, a0 = Ak(n), b0 = Bk(n), e = 1e-7 * Math.Sign(b0 - a0);
                        // the soffit one-sided at the block's own ends (a step at a split line belongs to the neighbouring block)
                        return u =>
                        {
                            double s = a0 + (b0 - a0) * u, se = u <= 0 ? s + e : u >= 1 ? s - e : s;
                            return cap.Plan(s, n).WithZ(bottom ? cap.SurfaceZ(s, c) : Soffit(se, n));
                        };
                    }
                    var nb = NurbsSlab.Create(1, _ => 1, (_, _, v) => Row(v, false), 0, _fitTol, out string w, 32, (_, _, v) => Row(v, true));
                    if (nb == null) { why = w; break; }
                    piece.AddNurbs(nb.ToSolid("cap haunch"), 0, k == 0 ? piece.TriangleCount : 0);
                    dev = Math.Max(dev, nb.MaxDeviation);
                }
                if (why != null)
                {
                    piece.DropNurbsFrom(n0);
                    _nurbs!.Fallback("cap haunch", $"{sl.Name}, piece S {FtIn(A(0))} .. {FtIn(B(0))} ({lines[i].kind} - {lines[i + 1].kind}): {why}");
                }
                else _nurbs!.Built("cap haunch", dev);
            }
            else if (_geo != null) _nurbs!.Fallback("cap haunch", "soffit held 1/16\" above the cap steel");
            res.Pieces.Add(piece);
            res.Mesh.Append(piece);
        }
        res.MinDepth = minD; res.MaxDepth = maxD;
        if (thin > 0) Warn($"{sl.Name} cap haunch: the deck soffit is less than 1/16\" above the cap steel at {thin} sample point(s) (minimum {In16(minD)}) - reported; the haunch is kept 1/16\" deep there.");
        // bays between the girder haunches / deck edges: depth at the CL cap in the middle
        var bays = BaysOf(foot, eL, eR);
        foreach (var (name, a, b, _) in bays)
        {
            double s = (a(0) + b(0)) / 2;
            res.BayDepths.Add((name, Soffit(s, 0) - cap.SurfaceZ(s, SteelC(s))));
        }
        string msg = $"{res.Name}: {FtIn(2 * W)} wide, {FtIn(hi0 - lo0)} along the cap between the deck edges (S {FtIn(lo0)} .. {FtIn(hi0)}), {res.Pieces.Count} pieces, depth {In16(minD)} .. {In16(maxD)} "
            + $"(CL cap mid-bay: {string.Join(", ", res.BayDepths.Select(x => $"{x.Bay} {In16(x.Depth)}"))}), concrete {res.Volume / Math.Pow(Ft(1), 3) / 27:0.00} CY ({src}). "
            + "Over the girder top flanges the girder haunches fill to the soffit; below them (cap flange to girder flange level) the space holds the splice plates / fills and is not concrete.";
        res.Notes.Add(msg);
        lock (_m.Notes) _m.Notes.Add(msg);
        _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, msg));
        lock (_m.CapHaunches) _m.CapHaunches.Add(res);

        // ---------------------------------------------------------------- reinforcing (plan bar sets in world coordinates)
        if (hd.Stirrups.Count == 0 && hd.Bars.Count == 0) return;
        var fs = cap.Fs;
        var sets = new List<PlanBarSet>();
        foreach (var st in hd.Stirrups)
        {
            double db = RebarCatalog.Size(st.Size).Diameter;
            var set = new PlanBarSet
            {
                Mark = st.Mark, Size = st.Size, Location = st.Location.Length > 0 ? st.Location : $"{sl.Name} integral steel pier cap haunch", Element = "cap haunch", Coating = st.Coating,
                Shape = "BENT", Dims = new[] { st.HookLength, st.LegHeight, st.BottomWidth, st.LegHeight, st.HookLength }, ListedLength = st.ListedLength, Pin = st.Pin, Cover = st.Cover,
                PlanCount = st.PlanCount, PlanWeightLb = st.PlanWeightLb, Source = st.Source.Length > 0 ? st.Source : src,
                Note = $"midway between the cap stud lines, {In16(st.ClearAboveSteel)} clear above the cap steel, in the plane square to the cap axis; legs / hooks from the out-to-out bend diagram",
            };
            var lineS = cd.StudLines.Select(l => cap.Sb[0] + l).OrderBy(x => x).ToList();
            if (lineS.Count < 2) Warn(UnresolvedFlag.Text(set.Source, $"{sl.Name} cap haunch stirrups {st.Mark}", "they are placed between the cap stud lines and the cap has none", "Not placed."));
            double yl = st.BottomWidth / 2 - db / 2, yh = st.BottomWidth / 2 - db + st.HookLength;
            for (int k = 0; k + 1 < lineS.Count; k++)
            {
                double sm = (lineS[k] + lineS[k + 1]) / 2;
                double x = cap.Xc(sm) + cap.M * cap.ZTop;   // between the studs, which stand at the horizontal plan distance from the CL bearing (tilted cap frame)
                double c = SteelCAtLine(sm);
                double sPlan = cap.PlanS(x, c);
                if (sPlan < lo0 || sPlan > hi0 || InFoot(sPlan, 0)) continue;
                double z0 = c + st.ClearAboveSteel + db / 2, z1 = c + st.ClearAboveSteel + st.LegHeight - db / 2;
                var pts = new List<Vec3>();
                if (st.HookLength > 0) pts.Add(fs.ToWorld(x, -yh, z1));
                pts.Add(fs.ToWorld(x, -yl, z1)); pts.Add(fs.ToWorld(x, -yl, z0)); pts.Add(fs.ToWorld(x, yl, z0)); pts.Add(fs.ToWorld(x, yl, z1));
                if (st.HookLength > 0) pts.Add(fs.ToWorld(x, yh, z1));
                set.Add(pts);
            }
            if (st.HookLength <= 0) set.Dims = new[] { st.LegHeight, st.BottomWidth, st.LegHeight };
            if (st.PlanCount is int pc && pc != set.Bars.Count) Warn($"{sl.Name} cap haunch: {set.Bars.Count} {st.Mark} stirrups fit between the cap stud lines in the haunch, the plan lists {pc} (reported).");
            sets.Add(set);
        }
        foreach (var bs in hd.Bars)
        {
            double db = RebarCatalog.Size(bs.Size).Diameter;
            var set = new PlanBarSet
            {
                Mark = bs.Mark, Size = bs.Size, Location = bs.Location.Length > 0 ? bs.Location : $"{sl.Name} integral steel pier cap haunch, along the cap", Element = "cap haunch", Coating = bs.Coating,
                Shape = "STR", ListedLength = bs.ListedLength, Cover = bs.Cover, PlanCount = bs.PlanCount, PlanWeightLb = bs.PlanWeightLb, Source = bs.Source.Length > 0 ? bs.Source : src,
            };
            var clear = new List<string>();
            foreach (var (name, a, b, _) in bays.Where(x => x.exterior == (bs.Bay == CapHaunchBay.Exterior)))
            {
                double sMid = (a(0) + b(0)) / 2;
                double c = SteelC(sMid), half = bs.ListedLength / 2;
                foreach (var pos in bs.Positions)
                {
                    double y = pos.X, cz = c + pos.Z;
                    double ya = Math.Clamp(y, -W, W);
                    // bay ends (plumb faces) at the bar's N and height, as local x along the cap axis
                    double xA = cap.Xc(a(ya)) + cap.M * cz, xB = cap.Xc(b(ya)) + cap.M * cz, xM = (xA + xB) / 2;
                    set.Add(new[] { fs.ToWorld(xM - half, y, cz), fs.ToWorld(xM + half, y, cz) });
                    clear.Add($"{name} {In16((xB - xA) / 2 - half)}");
                }
            }
            set.Note = $"centred in each {(bs.Bay == CapHaunchBay.Exterior ? "exterior" : "interior")} bay at (N, above the cap steel) {string.Join(" / ", bs.Positions.Select(q => $"({In16(q.X)}, {In16(q.Z)})"))} (bar centres); end clearance to the bay ends: {string.Join(", ", clear.Distinct())}";
            if (bs.PlanCount is int pc && pc != set.Bars.Count) Warn($"{sl.Name} cap haunch: {set.Bars.Count} {bs.Mark} bars placed, the plan lists {pc} (reported).");
            sets.Add(set);
        }
        lock (_capHaunchSets) _capHaunchSets.AddRange(sets);
    }

    /// <summary>Bays of a cap haunch: between consecutive girder haunch footprints (interior) and between the exterior footprints and the deck edges.</summary>
    private static List<(string name, Func<double, double> a, Func<double, double> b, bool exterior)> BaysOf(
        List<(string name, Func<double, double> a, Func<double, double> b)> foot, Func<double, double> eL, Func<double, double> eR)
    {
        var bays = new List<(string, Func<double, double>, Func<double, double>, bool)>();
        if (foot.Count == 0) { bays.Add(("deck edge to deck edge", eL, eR, true)); return bays; }
        bays.Add(($"deck edge-{foot[0].name}", eL, foot[0].a, true));
        for (int i = 0; i + 1 < foot.Count; i++) bays.Add(($"{foot[i].name}-{foot[i + 1].name}", foot[i].b, foot[i + 1].a, false));
        bays.Add(($"{foot[^1].name}-deck edge", foot[^1].b, eR, true));
        return bays;
    }

    private readonly List<PlanBarSet> _capHaunchSets = new();

    /// <summary>Cap haunch reinforcing (after the concrete exists, so the cover check sees the slab, haunches and widenings).</summary>
    private void BuildCapHaunchRebar()
    {
        if (_capHaunchSets.Count == 0) return;
        var hosts = new List<Mesh>();
        hosts.AddRange(_m.CapHaunches.Select(c => c.Mesh));
        if (_m.Deck != null) hosts.Add(_m.Deck);
        if (_m.Haunches != null) hosts.Add(_m.Haunches);
        hosts.AddRange(_m.DeckBumps.Select(b => b.Mesh));
        var res = PlanBarSetBuilder.Build(_capHaunchSets, p => p, "", "CapHaunch", hosts, Deck.Reinforcement.EpoxyCoated);
        foreach (var w in res.Warnings) Warn(w);
        lock (_m)
        {
            _m.Marks.AddRange(res.Marks);
            _m.Bars.AddRange(res.Bars);
            _m.Notes.AddRange(res.Notes);
            _m.CoverViolations.AddRange(res.Violations);
        }
    }

    private string GirderName(int g) => _def.Girders.PlanNames is { } pn && g < pn.Count ? pn[g] : _f.Girders[g].Name;

    private static string FtIn(double m)
    {
        double inch = Math.Round(m / In(1) * 16) / 16;
        string sg = inch < 0 ? "-" : "";
        inch = Math.Abs(inch);
        int ft = (int)Math.Floor(inch / 12 + 1e-9);
        return $"{sg}{ft}'-{(inch - ft * 12).ToString("0.####", CultureInfo.InvariantCulture)}\"";
    }

    private static string In16(double m) => $"{(Math.Round(m / In(1) * 16) / 16).ToString("0.####", CultureInfo.InvariantCulture)}\"";
}
