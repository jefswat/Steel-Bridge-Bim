using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// Footing, cap and pedestal reinforcing from the plans' bar lists (<see cref="PlanBarGroup"/>). Counts, sizes and listed lengths are the
/// plan's, so marks / counts / lengths / weights reproduce the bar list. Positions follow simple, documented rules (the plans' individual
/// bar sketches and zone dimensions are not all decoded):
///  * Footing mats: bars evenly spaced from the first-bar offset (6") on both sides, ends centred; the lowest bottom layer sits 1" CLEAR
///    above the pile heads (pile embedment 12"), the top mat 3" clear below the top; bottom bars bent up 90 degrees at both ends.
///    Side bars: vertical bars evenly round the perimeter (3" clear), horizontal bars equally spaced in height on the faces.
///    Uplift anchors: 8f6 V-bars (D = 6, 2'-6 arms, 2'-8 wide) through the 1 1/2" hole in the web of each circled pile, 6" above the
///    footing bottom, three per pile at 4" along the web, the V in the plane across the web.
///  * Cap: all bars lie inside the LOWER (narrower) section: core width = full width - 2 x StepSetback when the relief step exists.
///    Longitudinals in rows (alternating groups across the rows), centred on the cap and following the top line / soffit; side bars at
///    equal heights on both faces; hoops between the outer columns (height solved from the listed length), cantilever hoops over the
///    cantilevers following the local depth; ties U-shaped from the soffit; hairpins, end bars, transverse and pedestal bars as described
///    at their kinds. Hoop zone spacing is even (the plans' zone dimensions are not read).
/// </summary>
public sealed partial class SubstructureGenerator
{
    private static RawBar PlanBar(string series, RebarSize size, string loc, IReadOnlyList<Vec3> pts) =>
        new(series, size, ShapeFor(pts.ToArray()), loc, pts.ToArray());

    /// <summary>Straight or bent bar whose scheduled length is the plan's listed length (bend allowances are inside the listed length).</summary>
    private static RawBar PlanBarFixed(string series, RebarSize size, string loc, IReadOnlyList<Vec3> pts, double listed)
    {
        var sh = ShapeFor(pts.ToArray());
        if (sh.Code == "STR") sh = BarShape.Straight(listed);
        else sh = new BarShape { Code = sh.Code, Description = sh.Description, Centerline = sh.Centerline, FixedLength = listed, PinDiameter = sh.PinDiameter, Dimensions = { } }
            .WithDims(sh);
        return new RawBar(series, size, sh, loc, pts.ToArray());
    }

    // ------------------------------------------------------------------ footings

    private void FootingPlanBars(SubstructureUnit unit, FoundationInfo f, FootingDefinition fd, List<RawBar> bars)
    {
        var sd = unit.Definition;
        var r = sd.Reinforcement;
        string loc = $"{unit.Name} footing {fd.Column}";
        double embed = sd.PileEmbedment;
        double cx = f.S, cy = f.N;
        double Cum(bool top, int order, double db)
        {
            double c = 0;
            // stacked layers: sum of the diameters of the lower orders (largest bar of each order)
            foreach (var o in fd.Bars.Where(g => g.Kind == PlanBarKind.FootingMat && g.Top == top && g.Order < order).GroupBy(g => g.Order))
                c += o.Max(g => RebarCatalog.Size(g.Size).Diameter);
            return c;
        }
        foreach (var g in fd.Bars)
        {
            var size = RebarCatalog.Size(g.Size);
            double db = size.Diameter;
            string where = $"{loc} {g.Mark}";
            if (g.Count <= 0) continue;
            switch (g.Kind)
            {
                case PlanBarKind.FootingMat:
                {
                    double along = g.AlongN ? f.SizeN : f.SizeS, across = g.AlongN ? f.SizeS : f.SizeN;
                    // Listed length = out-to-out: a mat bar ends 2" short of the face; a hooked bar's up-turned leg has its OUTSIDE face 2" from the face
                    double off = In(6), run = g.HookExtra > 0 ? along / 2 - In(2) - db / 2 : (g.Length > 0 ? g.Length : along - In(4)) / 2;
                    double z = g.Top ? f.TopZ - In(3) - db / 2 - Cum(true, g.Order, db) : f.BottomZ + embed + In(1) + db / 2 + Cum(false, g.Order, db);
                    string what = g.Top ? "top mat" : "bottom mat";
                    for (int i = 0; i < g.Count; i++)
                    {
                        double t = g.Count == 1 ? 0 : -across / 2 + off + (across - 2 * off) * i / (g.Count - 1);
                        Vec3 P(double a, double zz) => g.AlongN ? unit.World(cx + t, cy + a, zz) : unit.World(cx + a, cy + t, zz);
                        if (g.Shape == "U" && g.Dims.Length == 3 && !g.Top)
                        {
                            // plan bend diagram (11g1 / 11g2 ...): base and legs out to out (legs up from the base's bottom face), centred on the footing
                            var (us, ul) = PlanU(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Pin, g.Length);
                            double zb0 = z - db / 2;
                            bars.Add(new RawBar("F", size, us, $"{where} {what}", ul.Select(q => P(q.X - g.Dims[1] / 2, zb0 + q.Y)).ToArray()));
                            continue;
                        }
                        var pts = g.HookExtra > 0
                            ? new[] { P(-run, z + g.HookExtra), P(-run, z), P(run, z), P(run, z + g.HookExtra) }
                            : new[] { P(-run, z), P(run, z) };
                        bars.Add(PlanBarFixed(g.Top ? "G" : "F", size, $"{where} {what}", pts, g.Length > 0 ? g.Length : 2 * run));
                    }
                    break;
                }
                case PlanBarKind.FootingSideVertical:
                {
                    double inset = In(3) + db / 2, w = f.SizeS - 2 * inset, h = f.SizeN - 2 * inset, per = 2 * (w + h);
                    double len = g.Length > 0 ? g.Length : f.TopZ - f.BottomZ - In(5);
                    double z1 = f.TopZ - In(3), z0 = z1 - len;
                    for (int i = 0; i < g.Count; i++)
                    {
                        double d = per * (i + 0.5) / g.Count, s, n;
                        if (d < w) { s = -w / 2 + d; n = -h / 2; }
                        else if (d < w + h) { s = w / 2; n = -h / 2 + (d - w); }
                        else if (d < 2 * w + h) { s = w / 2 - (d - w - h); n = h / 2; }
                        else { s = -w / 2; n = h / 2 - (d - 2 * w - h); }
                        bars.Add(PlanBarFixed("K", size, $"{where} side bars (vertical)", new[] { unit.World(cx + s, cy + n, z0), unit.World(cx + s, cy + n, z1) }, len));
                    }
                    break;
                }
                case PlanBarKind.FootingSideHorizontal:
                {
                    double along = g.AlongN ? f.SizeN : f.SizeS, across = g.AlongN ? f.SizeS : f.SizeN;
                    double inset = In(3) + db / 2 + In(0.75), len = g.Length > 0 ? g.Length : along - In(4);
                    double zLo = f.BottomZ + In(9), zHi = f.TopZ - In(9);
                    int per = (g.Count + 1) / 2;
                    for (int face = 0; face < 2; face++)
                    {
                        int cnt = face == 0 ? per : g.Count - per;
                        for (int k = 0; k < cnt; k++)
                        {
                            double z = cnt == 1 ? (zLo + zHi) / 2 : zLo + (zHi - zLo) * k / (cnt - 1);
                            double t = (face == 0 ? -1 : 1) * (across / 2 - inset);
                            Vec3 P(double a, double tt) => g.AlongN ? unit.World(cx + tt, cy + a, z) : unit.World(cx + a, cy + tt, z);
                            // A listed length beyond the face-to-face run is turned in 90 degrees at both ends (the bars wrap the corners).
                            double avail = along - 2 * In(3), leg = len > avail ? (len - avail) / 2 : 0, run = len - 2 * leg;
                            double tIn = t - Math.Sign(t) * leg;
                            var pts = leg > 0
                                ? new[] { P(-run / 2, tIn), P(-run / 2, t), P(run / 2, t), P(run / 2, tIn) }
                                : new[] { P(-len / 2, t), P(len / 2, t) };
                            bars.Add(PlanBarFixed("K", size, $"{where} side bars (horizontal)", pts, len));
                        }
                    }
                    break;
                }
                case PlanBarKind.UpliftAnchor:
                    UpliftAnchors(unit, f, fd, g, size, where, bars);
                    break;
            }
        }
    }

    /// <summary>8f6: V-bars through the web hole of each circled pile (see the class summary).</summary>
    private void UpliftAnchors(SubstructureUnit unit, FoundationInfo f, FootingDefinition fd, PlanBarGroup g, RebarSize size, string where, List<RawBar> bars)
    {
        double db = size.Diameter, len = g.Length > 0 ? g.Length : Ft(5), arm = len / 2, half = In(16);
        double rise = Math.Sqrt(Math.Max(arm * arm - half * half, 1e-6));
        double pin = In(6), rc = pin / 2 + db / 2;
        var bends = new List<(Vec2, double)>();
        var local = BarShape.Rounded(new[] { new Vec2(-half, rise), new Vec2(0, 0), new Vec2(half, rise) }, rc, bends);
        double yMin = local.Min(q => q.Y);                          // the bar's lowest point (inside of the pin) sits in the hole: shift the V down onto it
        local = local.Select(q => new Vec2(q.X, q.Y - yMin)).ToArray();
        BarShape Shape()
        {
            var sh = new BarShape
            {
                Code = "V", Description = "V anchor bar through the pile web hole", Centerline = local, PinDiameter = pin, FixedLength = len,
                Dimensions = { new BarDimension("A", arm, new Vec2(0, 0), new Vec2(-half, rise), new Vec2(-1, 0)), new BarDimension("W", 2 * half, new Vec2(-half, rise), new Vec2(half, rise), new Vec2(0, 1)) },
            };
            sh.Bends.AddRange(bends);
            return sh;
        }
        int perPile = Math.Max(1, fd.UpliftBarsPerPile);
        int want = g.Count, placed = 0;
        var piles = fd.UpliftPiles.Where(i => i >= 0 && i < f.Piles.Count).Select(i => f.Piles[i]).ToList();
        if (piles.Count == 0) { Warn($"{where}: {want} uplift anchors listed but no circled piles are set."); return; }
        double zHole = f.BottomZ + In(6);
        foreach (var p in piles)
        {
            double fl = p.WebN, fs = -p.WebS;                       // flange direction (S, N) = across the web
            for (int k = 0; k < perPile && placed < want; k++, placed++)
            {
                double u = (k - (perPile - 1) / 2.0) * In(4);       // along the web
                double cs = p.S + p.WebS * u, cn = p.N + p.WebN * u;
                var pts = local.Select(q => unit.World(cs + fl * q.X, cn + fs * q.X, zHole + q.Y)).ToArray();
                bars.Add(new RawBar("U", size, Shape(), $"{where} pile uplift anchors", pts));
            }
        }
        if (placed < want) Warn($"{where}: {want} anchors listed, {placed} placed on {piles.Count} circled piles x {perPile}.");
    }

    // ------------------------------------------------------------------ caps and pedestals

    private void CapPlanBars(SubstructureUnit unit, List<RawBar> bars)
    {
        var sd = unit.Definition;
        var r = sd.Reinforcement;
        double cov = r.CapCover;
        var st = RebarCatalog.Size(r.CapStirrupSize);
        double sb = sd.Aesthetics is { Layers.Count: > 0 } aes ? aes.StepSetback : 0;
        double nLo = unit.CapN0 + sb, nHi = unit.CapN1 - sb;                       // lower (core) section
        double s0 = unit.CapS0, s1 = unit.CapS1, capLen = s1 - s0, sMid = (s0 + s1) / 2;
        string loc = $"{unit.Name} cap";
        var topLine = unit.CapTopLine();
        double TopAt(double s) => Math.Min(SubstructureUnit.Interp(topLine, s), unit.CapTopAt(s));      // never above the concrete (raked / beak ends)
        double BotAt(double s) => unit.CapBottomAt(s);
        // lowest cap top over a horizontal bar's S extent [sa, sb] and N extent [na, nb] (a level bar must keep its cover under the lowest point of the top)
        double TopSpan(double sa, double sb, double na, double nb) =>
            new[] { TopAt(sa), TopAt(sb), unit.CapTopAt(sa, na), unit.CapTopAt(sb, na), unit.CapTopAt(sa, nb), unit.CapTopAt(sb, nb) }.Min();
        var ss = unit.CapTop.Select(p => p.X).Concat(unit.CapBottom.Select(p => p.X)).Where(x => x > s0 && x < s1).Distinct().OrderBy(x => x).ToList();
        var groups = r.CapBars;
        double colHalf = unit.Columns.Count > 0 ? unit.Columns.Max(c => c.SizeS) / 2 : 0;
        double midLo = unit.Columns.Count > 0 ? unit.Columns.Min(c => c.S) - colHalf : s0 + cov;
        double midHi = unit.Columns.Count > 0 ? unit.Columns.Max(c => c.S) + colHalf : s1 - cov;
        midLo = Math.Max(midLo, s0 + cov + In(3)); midHi = Math.Min(midHi, s1 - cov - In(3));

        var poly = unit.CapOutline.Count >= 3 ? unit.CapOutline.ToList() : unit.CapTop.Concat(Enumerable.Reverse(unit.CapBottom)).ToList();
        double Clr(double s, double z) => PolyClear(poly, new Vec2(s, z));                  // distance to the cap outline (S-Z), + inside
        double Setb(double s, double z) => LateralSetback(unit, s, z);

        // Centred polyline following z(s) for a bar of the given listed length. The ends keep the cover to the (raked) end faces where the
        // cap allows; a bar that is longer than the room is kept as listed (extended past the cover) and reported by the containment check.
        Vec3[] Follow(double n, double length, Func<double, double> z, double centre, double endCover = -1, int hook = 0, double rad = 0)
        {
            if (endCover < 0) endCover = cov;
            double lo = s0, hi = s1, legH = 0, fullLength = length;
            for (int iter = 0; iter < 3; iter++)
            {
                double lg = legH;
                lo = s0; hi = s1;
                for (double x = s0; x < sMid; x += In(1)) { if (Clr(x, z(x)) >= endCover - 1e-6 && (lg <= 0 || (Clr(x, z(x) + hook * lg) >= endCover + rad - 1e-6 && Clr(x, z(x)) >= endCover + rad - 1e-6))) { lo = x; break; } }
                for (double x = s1; x > sMid; x -= In(1)) { if (Clr(x, z(x)) >= endCover - 1e-6 && (lg <= 0 || (Clr(x, z(x) + hook * lg) >= endCover + rad - 1e-6 && Clr(x, z(x)) >= endCover + rad - 1e-6))) { hi = x; break; } }
                if (hook != 0 && fullLength > hi - lo + 1e-6) legH = (fullLength - (hi - lo)) / 2; else legH = 0;
            }
            if (legH > 0)
            {   // listed length beyond the room between the end faces: the excess is turned down (top bars) / up (bottom bars) in a hook at each end, as the plans' bent bars
                length = hi - lo; centre = (lo + hi) / 2;
            }
            double a = Math.Max(lo, centre - length / 2), b = Math.Min(hi, centre + length / 2);
            if (b - a < length - 1e-6 && Math.Abs(centre - sMid) < 1e-6 + capLen) { double mid = (a + b) / 2; a = mid - length / 2; b = mid + length / 2; if (a < lo - 1e-6 || b > hi + 1e-6) { a = Math.Min(a, Math.Max(lo, a)); } }
            var sl = ss.Where(x => x > a + 1e-6 && x < b - 1e-6).Prepend(a).Append(b).ToList();
            var pts = sl.Select(x => unit.World(x, n, z(x))).ToArray();
            double tot = 0;
            for (int i = 1; i < pts.Length; i++) tot += (pts[i] - pts[i - 1]).Length;
            if (legH > 0)
                pts = pts.Prepend(pts[0] + new Vec3(0, 0, hook * legH)).Append(pts[^1] + new Vec3(0, 0, hook * legH)).ToArray();
            else if (tot > length + 1e-6) pts = SubPolyline(pts, (tot - length) / 2, tot - (tot - length) / 2);
            else if (tot < length - 1e-6)
            {                                                                  // listed length is longer than the room: keep it, extend both ends straight
                double ex = (length - tot) / 2; var d0 = (pts[0] - pts[1]).Normalized(); var d1 = (pts[^1] - pts[^2]).Normalized();
                pts[0] = pts[0] + d0 * ex; pts[^1] = pts[^1] + d1 * ex;
            }
            return pts;
        }
        // Moves a station inward from the cap ends until the test holds (cap deep / wide enough with the cover to the end faces).
        double FitS(double s, int dir, Func<double, bool> ok)
        {
            for (int k = 0; k < 400 && !ok(s); k++) s += dir * In(1);
            return Math.Clamp(s, s0, s1);
        }
        double nA = nLo + cov + st.Diameter, nB = nHi - cov - st.Diameter;           // inside the stirrups

        // ---- longitudinal layers: rows of bars alternating through the groups
        void Rows(IEnumerable<PlanBarGroup> gs, string series, string what, Func<double, double> z0, int dir)
        {
            var list = gs.SelectMany(g => Enumerable.Range(0, g.Count).Select(_ => g)).ToList();
            if (list.Count == 0) return;
            double dbMax = list.Max(g => RebarCatalog.Size(g.Size).Diameter);
            double avail = nB - nA, cap = Math.Max(2, Math.Floor(avail / Math.Max(1.5 * dbMax, In(3))) + 1);
            int R = (int)Math.Ceiling(list.Count / cap);
            for (int row = 0; row < R; row++)
            {
                var mine = list.Where((g, i) => i % R == row).ToList();
                for (int j = 0; j < mine.Count; j++)
                {
                    var g = mine[j];
                    var size = RebarCatalog.Size(g.Size);
                    double n = mine.Count == 1 ? (nA + nB) / 2 : nA + size.Diameter / 2 + (avail - size.Diameter) * j / (mine.Count - 1);
                    double off = dir * row * (dbMax + In(1.5));
                    Func<double, double> z = s => z0(s) + off;
                    var pts = Follow(n, g.Length, z, sMid, -1, dir < 0 ? -1 : 1, size.Diameter / 2);
                    bars.Add(PlanBarFixed(series, size, $"{loc} {g.Mark} {what}", pts, g.Length));
                }
            }
        }
        Rows(groups.Where(g => g.Kind == PlanBarKind.CapTop), "A", "top", s => TopAt(s) - cov - st.Diameter - RebarCatalog.Size(groups.First(g => g.Kind == PlanBarKind.CapTop).Size).Diameter / 2, -1);
        Rows(groups.Where(g => g.Kind == PlanBarKind.CapBottom), "B", "bottom", s => BotAt(s) + cov + st.Diameter + RebarCatalog.Size(groups.First(g => g.Kind == PlanBarKind.CapBottom).Size).Diameter / 2, +1);
        int rowsB = 0;
        foreach (var g in groups.Where(g => g.Kind == PlanBarKind.CapCantilever))
        {
            var size = RebarCatalog.Size(g.Size);
            int half = (g.Count + 1) / 2;
            for (int end = 0; end < 2; end++)
            {
                int cnt = end == 0 ? half : g.Count - half;
                for (int j = 0; j < cnt; j++)
                {
                    double n = cnt == 1 ? (nA + nB) / 2 : nA + (nB - nA) * j / (cnt - 1);
                    if (g.Shape == "CS" && g.Dims.Length == 4)
                    {
                        // 6b2 (plan bend diagram): flat run 2'-0, then a slanted run of 9'-1 (8'-9 along S, 2'-4 1/2 down), out to out along the bottom face - the
                        // bar follows the sloping soffit of the cantilever. Starts at the end face (cover) and descends toward the columns.
                        double flat = g.Dims[0], slant = g.Dims[1], run = g.Dims[2], rise = g.Dims[3];
                        var outline = new[] { new Vec2(0, 0), new Vec2(flat, 0), new Vec2(flat + run, -rise) };
                        var (cs, cl) = PlanBent(size, outline, new[] { flat, slant }, +1, "CS", $"Bar {Dim(flat)} flat + {Dim(slant)} slanted ({Dim(run)} x {Dim(rise)}) out to out", g.Pin, g.Length);
                        double sTip = end == 0 ? s0 : s1, inward = end == 0 ? 1 : -1;
                        // The bar is placed as low as the clear cover allows and pushed in from the (raked) end face only as far as the cover needs:
                        // for each set-back from the tip (1/4" steps) the lowest elevation of the bar (1/4" steps from the soffit) whose every point
                        // (sampled along the segments: the soffit bevel ends in a re-entrant corner) keeps the cover. Earlier the elevation was fixed
                        // from the soffit 1 ft inside the tip, below the end-face corner, and only the set-back was searched: the slanted run is longer
                        // than the soffit bevel (8'-9 vs ~9'-2 1/2 incl. the 2'-0 flat), so no set-back fitted and every 6b2 was reported.
                        var samp = new List<Vec2>();
                        for (int k = 0; k + 1 < cl.Length; k++)
                        {
                            int ns2 = Math.Max(1, (int)Math.Ceiling(Vec2.Distance(cl[k], cl[k + 1]) / In(1)));
                            for (int m2 = 0; m2 < ns2; m2++) samp.Add(Vec2.Lerp(cl[k], cl[k + 1], m2 / (double)ns2));
                        }
                        samp.Add(cl[^1]);
                        double need = cov + st.Diameter + size.Diameter / 2 - 1e-4, sv = sTip, zb = 0;
                        bool foundCs = false;
                        for (int it = 0; it < 800 && !foundCs; it++, sv += inward * In(0.25))
                        {
                            double zLow = cl.Max(q => BotAt(sv + inward * q.X) - q.Y) + cov + st.Diameter;                       // lowest point on the soffit + cover
                            for (int jz = 0; jz < 48 && !foundCs; jz++)
                            {
                                zb = zLow + jz * In(0.25);
                                foundCs = samp.All(q => Clr(sv + inward * q.X, zb + q.Y) >= need);
                            }
                        }
                        if (foundCs) sv -= inward * In(0.25);
                        else { sv = sTip + inward * cov; zb = BotAt(sTip + inward * (cov + flat / 2)) + cov + st.Diameter; }   // no position keeps the cover: reported by the check
                        zb += 3 * size.Diameter * rowsB;                                                                  // second group (6b3) one row up
                        bars.Add(new RawBar("B", size, cs, $"{loc} {g.Mark} cantilever bottom", cl.Select(q => unit.World(sv + inward * q.X, n, zb + q.Y)).ToArray()));
                        continue;
                    }
                    double centre = end == 0 ? s0 + cov + g.Length / 2 : s1 - cov - g.Length / 2;
                    var pts = Follow(n, g.Length, s => BotAt(s) + cov + st.Diameter + size.Diameter / 2 + 3 * size.Diameter * (1 + rowsB), centre, -1, 1, size.Diameter / 2);
                    bars.Add(PlanBarFixed("B", size, $"{loc} {g.Mark} cantilever bottom", pts, g.Length));
                }
            }
            rowsB++;
        }
        // ---- side (skin) bars: equal heights on both faces
        var sideList = groups.Where(g => g.Kind == PlanBarKind.CapSide).SelectMany(g => Enumerable.Range(0, g.Count).Select(_ => g)).ToList();
        if (sideList.Count > 0)
        {
            int perFace = (sideList.Count + 1) / 2;
            for (int i = 0; i < sideList.Count; i++)
            {
                var g = sideList[i]; var size = RebarCatalog.Size(g.Size);
                int face = i % 2, k = i / 2;
                double f = (k + 1.0) / (perFace + 1.0);
                double n = face == 0 ? nLo + cov + st.Diameter + size.Diameter / 2 : nHi - cov - st.Diameter - size.Diameter / 2;
                Func<double, double> z = s => BotAt(s) + (TopAt(s) - BotAt(s)) * f;
                // 5a11 / 5a12: short bars at the cap ends (plan elevation: "5a11 (E.F.)" at the left end, "5a12 (E.F.)" at the right); their height along the end is not dimensioned
                // The bar end keeps the cover to the (raked) end face at the bar's own height: s0 / s1 are the top corners, at mid-height the end face is inches
                // further in (centring on s0 + cover put the end through the raked face; Follow then re-centres a bar it has to shorten, so it must fit as given).
                double endC = sMid;
                if (g.End != 0)
                {
                    double x = g.End < 0 ? s0 : s1;
                    for (int it = 0; it < 2000 && Clr(x, z(x)) < cov - 1e-6; it++) x -= g.End * In(0.25);
                    endC = x - g.End * g.Length / 2;
                }
                bars.Add(PlanBarFixed("C", size, $"{loc} {g.Mark} side faces", Follow(n, g.Length, z, endC), g.Length));
            }
        }
        // ---- hoops, ties, cantilever hoops, hairpins
        double w = nHi - nLo - 2 * cov;
        double Even(int k, int cnt, double a, double b) => cnt == 1 ? (a + b) / 2 : a + (b - a) * k / (cnt - 1);
        // 5c5 / 5c6 (plan bend diagram): U bars in the transverse plane, base across the width (5'-5 = 6'-0 less 3 1/2" clear at both faces) with legs
        // hanging down (2'-11 / 2'-0 out to out from the top face of the base), two bars per station along the top ("N SPA. @ 1'-4; k-5c5 BARS").
        // NOT READ: the plans' zone dimensions (which stations are c5 / c6). Stations: equal spaces from 4" inside the top ends; the groups are given the
        // stations by the depth of the full-width zone (the longer-legged group where the zone is deepest), as the plan's zones follow the relief arc.
        var hair = groups.Where(g => g.Kind == PlanBarKind.CapHairpin && g.Shape == "U" && g.Dims.Length == 3 && g.Count > 0).OrderBy(g => g.Dims[0]).ToList();
        if (hair.Count > 0)
        {
            const int perStation = 2;
            int totalBars = hair.Sum(g => g.Count);
            if (totalBars % perStation != 0 || hair.Any(g => g.Count % perStation != 0)) Warn($"{loc}: UNRESOLVED - hairpin counts {string.Join("/", hair.Select(g => g.Count))} are not multiples of {perStation} per station; hairpins not placed.");
            else
            {
                int ns = totalBars / perStation;
                // ends of the top face: the highest outline vertex on each side of mid-cap (the top may be sloped, with only one vertex at the maximum)
                double sTopL = poly.Where(q => q.X < sMid).OrderByDescending(q => q.Y).First().X, sTopR = poly.Where(q => q.X > sMid).OrderByDescending(q => q.Y).First().X;
                double LowZ(double sx)
                {
                    double zl = double.MaxValue;
                    foreach (var line in unit.ReliefLines) if (line.Count > 1 && sx >= line[0].X && sx <= line[^1].X) zl = Math.Min(zl, SubstructureUnit.Interp(line, sx));
                    return zl == double.MaxValue ? BotAt(sx) : Math.Max(zl, BotAt(sx));
                }
                double margin = hair.Max(g => g.ZoneMargin) > 0 ? hair.Max(g => g.ZoneMargin) : In(4);
                var stations = Enumerable.Range(0, ns).Select(j => Even(j, ns, sTopL + margin, sTopR - margin)).ToList();
                // zones read from the plan (k-5cX BARS labels left to right): station j belongs to the mark whose zone it falls in
                bool zoned = hair.All(g => g.ZoneStations.Length > 0 && g.ZoneStations.Length == hair[0].ZoneStations.Length) && hair.Sum(g => g.ZoneStations.Sum()) == ns;
                var byDepth = zoned ? stations : stations.OrderBy(sx => TopAt(sx) - LowZ(sx)).ToList();           // shallowest first
                var zoneOf = new Dictionary<PlanBarGroup, List<double>>();
                if (zoned)
                {
                    int pos = 0;
                    for (int zq = 0; zq < hair[0].ZoneStations.Length; zq++)
                        foreach (var g in hair)
                        {
                            int zi = zq;
                            int c = g.ZoneStations[zi];
                            if (c == 0) continue;
                            if (!zoneOf.TryGetValue(g, out var l)) zoneOf[g] = l = new List<double>();
                            l.AddRange(stations.Skip(pos).Take(c)); pos += c;
                        }
                }
                int at = 0;
                foreach (var g in hair)
                {
                    var size = RebarCatalog.Size(g.Size);
                    var (hs, hl) = PlanU(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Pin, g.Length);
                    double ncH = (unit.CapN0 + unit.CapN1) / 2;
                    foreach (var sx in zoned ? zoneOf[g] : byDepth.Skip(at).Take(g.Count / perStation))
                        for (int b = 0; b < perStation; b++)
                        {
                            double sp = sx + (b - (perStation - 1) / 2.0) * size.Diameter, zOut = TopSpan(sp, sp, ncH - g.Dims[1] / 2, ncH + g.Dims[1] / 2) - cov;
                            bars.Add(new RawBar("R", size, hs, $"{loc} {g.Mark} hairpins", hl.Select(q => unit.World(sp, ncH + q.X - g.Dims[1] / 2, zOut - q.Y)).ToArray()));
                        }
                    at += g.Count / perStation;
                }
                Warn($"{loc}: hairpins {string.Join(", ", hair.Select(g => g.Mark))}: " + (zoned
                    ? $"{ns} stations at equal spaces {Dim(margin)} inside the top ends, marks by the plan's zone order and counts (the plan's gap dimensions between zones are not read); {perStation} bars per station side by side (their offset is not dimensioned)."
                    : $"UNRESOLVED - the plan's zones are not read; {ns} stations at equal spaces, marks given by the depth of the full-width zone."));
            }
        }
        foreach (var g in groups.Where(g => (g.Kind is PlanBarKind.CapHoop or PlanBarKind.CapCantileverHoop or PlanBarKind.CapTie or PlanBarKind.CapHairpin) && !(g.Kind == PlanBarKind.CapHairpin && g.Shape == "U" && g.Dims.Length == 3)))
        {
            var size = RebarCatalog.Size(g.Size);
            bool cant = g.Kind == PlanBarKind.CapCantileverHoop;
            int cntEnd0 = (g.Count + 1) / 2;
            for (int i = 0; i < g.Count; i++)
            {
                double s;
                if (cant && unit.Columns.Count > 0)
                {
                    bool left = i < cntEnd0; int kk = left ? i : i - cntEnd0, cc = left ? cntEnd0 : g.Count - cntEnd0;
                    s = left ? Even(kk, cc, s0 + cov + In(3), midLo - In(6)) : Even(kk, cc, midHi + In(6), s1 - cov - In(3));
                }
                else if (g.Kind == PlanBarKind.CapTie || g.Kind == PlanBarKind.CapHairpin)
                    s = Even(i, g.Count, midLo + In(3), midHi - In(3)) + (g.Kind == PlanBarKind.CapTie ? (midHi - midLo) / (2.0 * g.Count) : 0);
                else s = Even(i, g.Count, midLo, midHi);
                s = Math.Clamp(s, s0 + cov, s1 - cov);
                int dirIn = s < sMid ? 1 : -1;
                string where = $"{loc} {g.Mark}";
                if (g.Kind == PlanBarKind.CapHairpin && g.Shape == "U" && g.Dims.Length == 3)
                {
                    // 5c5 / 5c6 (plan bend diagram): U bar in the transverse plane, base across the width (5'-5 = 6'-0 less 3 1/2" clear at both faces),
                    // legs hanging down (2'-11 / 2'-0, out to out from the top of the base). Stations stay as laid out (zones are not read).
                    var (hs, hl) = PlanU(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Pin, g.Length);
                    double zOut = TopAt(s) - cov, ncH = (unit.CapN0 + unit.CapN1) / 2;
                    bars.Add(new RawBar("R", size, hs, $"{where} hairpins", hl.Select(q => unit.World(s, ncH + q.X - g.Dims[1] / 2, zOut - q.Y)).ToArray()));
                    continue;
                }
                s = FitS(s, dirIn, x => Clr(x, TopAt(x) - cov) >= cov - 1e-4 && Clr(x, BotAt(x) + cov) >= cov - 1e-4);
                double zt = TopAt(s) - cov, zb = BotAt(s) + cov;
                if (zt - zb < In(12)) continue;
                if (g.Kind is PlanBarKind.CapHoop or PlanBarKind.CapCantileverHoop)
                {
                    double h = zt - zb;
                    if (g.Shape is "HOOP" or "HOOPV" && g.Dims.Length == 4)
                    {
                        // plan bend diagram: HOOP {w, h, lap, lap}; HOOPV (cantilever hoops) {w, h min, h max, lap} - height = the local depth within the printed range.
                        // Centred across the cap (the section sketch puts the hoops off-centre; which side is not decoded).
                        double hw = g.Dims[0], hh = g.Shape == "HOOP" ? g.Dims[1] : Math.Clamp(zt - zb, g.Dims[1], g.Dims[2]), lp = g.Shape == "HOOP" ? g.Dims[2] : g.Dims[3];
                        var (hs2, hl2) = PlanHoop(size, hw, hh, lp, g.Shape == "HOOP" ? g.Dims[3] : lp, g.Pin, g.Shape == "HOOP" ? g.Length : 2 * (hw + hh) + 2 * lp);
                        double ncHoop = (unit.CapN0 + unit.CapN1) / 2;
                        bars.Add(new RawBar("S", size, hs2, $"{where} {(cant ? "cantilever hoops" : "hoops")}", hl2.Select(p => unit.World(s, ncHoop + p.X - hw / 2, zt - hh + p.Y)).ToArray()));
                        continue;
                    }
                    if (!cant && g.Length > 0) h = Math.Min(h, SolveHoopHeight(w, g.Length, size));
                    var (shape, local) = ClosedStirrup(w, h, size);
                    double zTopHoop = zt;
                    var pts = local.Select(p => unit.World(s, nLo + cov + p.X, zTopHoop - h + p.Y)).ToArray();
                    bars.Add(new RawBar("S", size, shape, $"{where} {(cant ? "cantilever hoops" : "hoops")}", pts));
                }
                else if (g.Kind == PlanBarKind.CapTie)
                {
                    double leg = Math.Max(In(6), (g.Length - w) / 2);
                    double rd = size.Diameter / 2, z0 = zb + rd, z1 = Math.Min(zt - rd, zb + leg);
                    var pts = new[] { unit.World(s, nLo + cov + rd, z1), unit.World(s, nLo + cov + rd, z0), unit.World(s, nHi - cov - rd, z0), unit.World(s, nHi - cov - rd, z1) };
                    bars.Add(PlanBarFixed("S", size, $"{where} ties", pts, g.Length > 0 ? g.Length : w + 2 * leg));
                }
                else
                {
                    double leg = Math.Max(In(12), (g.Length - In(4)) / 2), n = (nLo + nHi) / 2, top = TopAt(s) - cov - In(2);
                    double bot = Math.Max(BotAt(s) + cov, top - leg);
                    var pts = new[] { unit.World(s - In(2), n, top), unit.World(s - In(2), n, bot), unit.World(s + In(2), n, bot), unit.World(s + In(2), n, top) };
                    bars.Add(PlanBarFixed("R", size, $"{where} hairpins", pts, g.Length));
                }
            }
        }
        // ---- end bars, c7
        foreach (var g in groups.Where(g => g.Kind is PlanBarKind.CapEnd or PlanBarKind.CapTransverse))
        {
            var size = RebarCatalog.Size(g.Size);
            bool vertical = g.Vertical;
            for (int i = 0; i < g.Count; i++)
            {
                string where = $"{loc} {g.Mark}";
                if (g.Kind == PlanBarKind.CapTransverse)
                {
                    // 4c5 / 4c7 (plan bend diagram): base 4'-8 (7'-5 on the wide caps) with 4 1/2" legs turned up at both ends, out to out.
                    double s = Math.Clamp(Even(i, g.Count, s0 + cov + In(3), s1 - cov - In(3)), s0 + cov, s1 - cov);
                    s = FitS(s, s < sMid ? 1 : -1, x => TopAt(x) - BotAt(x) > In(24) && Clr(x, (TopAt(x) + BotAt(x)) / 2) >= cov + size.Diameter / 2 + In(5));
                    bool planU = g.Shape == "U" && g.Dims.Length == 3;
                    double leg1 = planU ? g.Dims[0] : In(4.5), leg2 = planU ? g.Dims[2] : leg1, leg = Math.Max(leg1, leg2);
                    double bas = planU ? g.Dims[1] : Math.Max(In(12), g.Length - 2 * leg), z = (TopAt(s) + BotAt(s)) / 2, nc = (unit.CapN0 + unit.CapN1) / 2;
                    // highest elevation (full width above the relief curve) where the base fits between the cap sides with the cover
                    for (double zz = TopAt(s) - cov - In(6); zz > BotAt(s) + cov + In(12); zz -= In(2))
                        if (unit.CapN1 - unit.CapN0 - 2 * Setb(s, zz) - 2 * (cov + (planU ? 0 : size.Diameter / 2)) >= bas + 1e-6 && Clr(s, zz + leg) >= cov) { z = zz - leg; break; }
                    if (planU)
                    {
                        var (ts, tl) = PlanU(size, leg1, bas, leg2, g.Pin, g.Length);
                        double zb0 = z - size.Diameter / 2;                                   // base: outside face at zb0, legs up
                        bars.Add(new RawBar("T", size, ts, $"{where} transverse", tl.Select(q => unit.World(s, nc + q.X - bas / 2, zb0 + q.Y)).ToArray()));
                    }
                    else
                    {
                        var pts = new[] { unit.World(s, nc - bas / 2, z + leg), unit.World(s, nc - bas / 2, z), unit.World(s, nc + bas / 2, z), unit.World(s, nc + bas / 2, z + leg) };
                        bars.Add(PlanBarFixed("T", size, $"{where} transverse", pts, g.Length));
                    }
                }
                else
                {
                    int end = i % 2, k = i / 2, per = (g.Count + 1) / 2;
                    if (g.End != 0) { end = g.End < 0 ? 0 : 1; k = i; per = g.Count; }      // the plan shows the group at one end only (5WB: 9-5a15 left, 9-5a14 right)
                    if (vertical && g.Shape == "A15" && g.Dims.Length == 4)
                    {
                        // 5a15 (Pier 5WB, Design 116 PDF 38 bend diagram, elevation PDF 35/37 "9-5a15"): bar in the vertical longitudinal plane at the end - 10" leg
                        // inward under the top, 4'-5 down the (vertical) end face, 2'-0 slanted inward along the soffit bevel (6" drop), out to out; 9 across the width
                        // at 9" from the faces (plan PDF 36 "9, 8 EQ. SPA., 9"). Pushed in from the end / down from the top only as far as the clear cover needs.
                        var (fs, fl) = PlanA15(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Dims[3], g.Pin, g.Length);
                        double nE = per == 1 ? (unit.CapN0 + unit.CapN1) / 2 : unit.CapN0 + In(9) + (unit.CapN1 - unit.CapN0 - In(18)) * k / (per - 1);
                        double inw = end == 0 ? 1 : -1, sTip = end == 0 ? s0 : s1;
                        var smp = new List<Vec2>();
                        for (int q = 0; q + 1 < fl.Length; q++)
                        {
                            int ns3 = Math.Max(1, (int)Math.Ceiling(Vec2.Distance(fl[q], fl[q + 1]) / In(1)));
                            for (int m3 = 0; m3 < ns3; m3++) smp.Add(Vec2.Lerp(fl[q], fl[q + 1], m3 / (double)ns3));
                        }
                        smp.Add(fl[^1]);
                        double needE = cov + size.Diameter / 2 - 1e-4, sv = sTip, zt0 = 0; bool okE = false;
                        for (int it = 0; it < 400 && !okE; it++, sv += inw * In(0.25))
                        {
                            double zTopE = Math.Min(TopAt(sv), TopAt(sv + inw * g.Dims[0])) - cov;
                            for (int jz = 0; jz < 48 && !okE; jz++) { zt0 = zTopE - jz * In(0.25); okE = smp.All(q => Clr(sv + inw * q.X, zt0 + q.Y) >= needE); }
                        }
                        if (okE) sv -= inw * In(0.25);
                        else { sv = sTip + inw * cov; zt0 = TopAt(sv) - cov; }                                                       // no position keeps the cover: reported by the check
                        bars.Add(new RawBar("E", size, fs, $"{where} end bars", fl.Select(q => unit.World(sv + inw * q.X, nE, zt0 + q.Y)).ToArray()));
                        continue;
                    }
                    if (vertical && g.Shape == "A14" && g.Dims.Length == 4)
                    {
                        // 5a14 (plan bend diagram): bar in the vertical longitudinal plane at the cap end - vertical leg 24", top run 4'-0, slanted leg 10" (2 1/2"
                        // run) parallel to the raked end face; 6 across the width at 9" from the faces (5 equal spaces), at each end. The bar is pushed in from the end
                        // face only as far as the clear cover needs; its top follows the cap top minus the cover.
                        var (es, el) = PlanA14(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Dims[3], g.Pin, g.Length);
                        double nEnd = per == 1 ? (unit.CapN0 + unit.CapN1) / 2 : unit.CapN0 + In(9) + (unit.CapN1 - unit.CapN0 - In(18)) * k / (per - 1);
                        double dirOut = end == 0 ? -1 : 1, sv0 = end == 0 ? s0 : s1;
                        double extent = g.Dims[1] + g.Dims[3];
                        double sv = sv0 - dirOut * (extent + cov), zTopOut = 0, svFirst = sv;
                        bool found = false;
                        for (int it = 0; it < 1600 && !found; it++)
                        {
                            double sOut = sv + dirOut * extent;
                            zTopOut = Math.Min(TopAt(sv), TopAt(sOut)) - cov;                    // level top run: under the lowest point of the (sloped) top
                            found = true;
                            foreach (var q in el)
                                if (Clr(sv + dirOut * q.X, zTopOut + q.Y) < cov + size.Diameter / 2 - 1e-4) { found = false; break; }
                            if (!found) sv -= dirOut * In(0.25);
                        }
                        if (!found) { sv = svFirst; zTopOut = Math.Min(TopAt(sv), TopAt(sv + dirOut * extent)) - cov; }          // no position keeps the cover: at the end face, reported by the check
                        bars.Add(new RawBar("E", size, es, $"{where} end bars", el.Select(q => unit.World(sv + dirOut * q.X, nEnd, zTopOut + q.Y)).ToArray()));
                        continue;
                    }
                    if (!vertical && g.Shape == "U" && g.Dims.Length == 3)
                    {
                        // 5a13 / 6a14 (plan bend diagram): U with a 5'-3 base and 2'-0 legs, out to out; 4 at each end stacked at equal spaces ("3 EQ. SPA., 4-5a13 BARS").
                        // The plane / leg direction of the sketch are not dimensioned - the legs stand up from the base in the transverse plane (as before). The stack is set
                        // back from the (raked) end face only as far as the clear cover needs and spans the cover-to-cover depth there.
                        var (us, ul) = PlanU(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Pin, g.Length);
                        double ncU = (unit.CapN0 + unit.CapN1) / 2, legU = Math.Max(g.Dims[0], g.Dims[2]), rU = size.Diameter / 2;
                        double sTipU = end == 0 ? s0 : s1, inwardU = end == 0 ? 1 : -1;
                        bool placedU = false;
                        for (int it = 0; it < 1600 && !placedU; it++)
                        {
                            double sx = sTipU + inwardU * it * In(0.25);
                            double zlo = double.NaN, zhi = double.NaN;
                            // the base spans the width (5'-3 of a 6'-0 cap): it also needs the full-width (upper) section there - the plan's stack lies above the relief
                            // line at the end (Design 116 PDF 36, pier 5WB elevation "3 EQ. SPA.; 4-5a13 BARS"); earlier only the S-Z outline was tested
                            bool Wide(double zz) => g.Dims[1] / 2 <= (unit.CapN1 - unit.CapN0) / 2 - Setb(sx, zz) - cov + 1e-4;
                            for (double zz = BotAt(sx); zz <= TopAt(sx) + 1e-9; zz += In(0.25)) if (Clr(sx, zz) >= cov + rU - 1e-4 && Wide(zz)) { zlo = zz; break; }
                            for (double zz = TopAt(sx); zz >= BotAt(sx) - 1e-9; zz -= In(0.25)) if (Clr(sx, zz) >= cov + rU - 1e-4) { zhi = zz; break; }
                            if (double.IsNaN(zlo) || double.IsNaN(zhi) || zhi - zlo < legU + 2 * rU) continue;
                            double zb0 = per == 1 ? zlo : zlo + (zhi - zlo - legU) * k / (per - 1);       // outside face of the base (bar centre at + r)
                            bars.Add(new RawBar("E", size, us, $"{where} end transverse", ul.Select(q => unit.World(sx, ncU + q.X - g.Dims[1] / 2, zb0 + q.Y)).ToArray()));
                            placedU = true;
                        }
                        if (!placedU)
                        {                                                                       // no position keeps the cover: at the end face over the cover-to-cover depth, reported by the check
                            double sx = sTipU + inwardU * cov, zlo = BotAt(sx) + cov + rU, zhi = TopAt(sx) - cov - rU;
                            double zb0 = per == 1 ? zlo : zlo + Math.Max(zhi - zlo - legU, 0) * k / (per - 1);
                            bars.Add(new RawBar("E", size, us, $"{where} end transverse", ul.Select(q => unit.World(sx, ncU + q.X - g.Dims[1] / 2, zb0 + q.Y)).ToArray()));
                        }
                        continue;
                    }
                    double need = vertical ? g.Length : Math.Max(In(12), (g.Length - w) / 2) + In(6);
                    double s = end == 0 ? s0 + cov + In(2) : s1 - cov - In(2);
                    // move in from the tip until the cap is deep enough for the bar
                    s = FitS(s, end == 0 ? 1 : -1, x => TopAt(x) - BotAt(x) - 2 * cov >= need && Clr(x, TopAt(x) - cov) >= cov - 1e-4 && Clr(x, BotAt(x) + cov) >= cov - 1e-4);
                    double zt = TopAt(s) - cov, zb = BotAt(s) + cov;
                    if (zt - zb < In(6)) continue;
                    if (vertical)
                    {
                        double n = per == 1 ? (nLo + nHi) / 2 : nLo + cov + (nHi - nLo - 2 * cov) * k / (per - 1);
                        double len = Math.Min(g.Length, zt - zb);
                        bars.Add(PlanBarFixed("E", size, $"{where} end vertical", new[] { unit.World(s, n, zt), unit.World(s, n, zt - g.Length) }, g.Length));
                    }
                    else
                    {
                        double z = per == 1 ? (zt + zb) / 2 : zb + (zt - zb) * (k + 0.5) / per;
                        double leg = Math.Max(In(3), (g.Length - w) / 2);
                        z = Math.Min(z, zt - leg);
                        double sbz = Setb(s, z), a = unit.CapN0 + sbz + cov + size.Diameter / 2, bb = unit.CapN1 - sbz - cov - size.Diameter / 2;
                        double bas = Math.Min(bb - a, g.Length - 2 * leg), nc = (unit.CapN0 + unit.CapN1) / 2;
                        leg = (g.Length - bas) / 2; z = Math.Min(z, zt - leg);
                        var pts = new[] { unit.World(s, nc - bas / 2, z + leg), unit.World(s, nc - bas / 2, z), unit.World(s, nc + bas / 2, z), unit.World(s, nc + bas / 2, z + leg) };
                        bars.Add(PlanBarFixed("E", size, $"{where} end transverse", pts, g.Length));
                    }
                }
            }
        }
        // ---- pedestal bars: U bars over each pedestal (base in the pedestal, legs into the cap), hoops round the pedestals
        var peds = unit.PedestalBlocks;
        foreach (var g in groups.Where(g => g.Kind is PlanBarKind.PedestalLongitudinal or PlanBarKind.PedestalTransverse or PlanBarKind.PedestalHoop))
        {
            var size = RebarCatalog.Size(g.Size);
            string where = $"{loc} {g.Mark}";
            if (g.Kind == PlanBarKind.PedestalHoop)
            {
                double zh = TopAt(sMid) - cov - size.Diameter / 2 - In(3);
                double wh = unit.CapN1 - unit.CapN0 - 2 * Setb(sMid, zh) - 2 * (cov + size.Diameter / 2);
                double sLen = Math.Max(g.Length / 2 - wh, In(12));
                if (g.Shape == "HOOP" && g.Dims.Length == 4)
                {
                    // 5n2 (plan bend diagram): hoop 5'-2 (along S) x 5'-4 (along N), 6" lap tails, out to out; the plan puts them at the pedestals of girders C and D
                    // ("AT GIRDERS C & D ONLY", Pier 1WB) - which pedestals is not decoded for the other piers: placed at mid-cap, one above the other (as before).
                    var (hs, hl) = PlanHoop(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Dims[3], g.Pin, g.Length);
                    // With the plan note's girders (PlanBarGroup.Girders): the hoops go round those pedestal blocks (plan: hoop 2" inside the block faces; the pier
                    // elevations show it as a dot inside the pedestal), g.Count / blocks per block, at mid-height of the block (elevation not dimensioned).
                    var sel = PedestalBlocksOf(unit, g.Girders);
                    if (sel.Count > 0 && g.Count % sel.Count == 0)
                    {
                        int perB = g.Count / sel.Count;
                        foreach (var p in sel)
                        {
                            // the diagram's longer side goes along the block's longer side (3WB: 5'-4 x 6'-2 in a 6'-6 x 5'-8 block)
                            bool swap = (g.Dims[0] > g.Dims[1]) != (p.LengthS > p.WidthN) && Math.Abs(g.Dims[0] - g.Dims[1]) > 1e-6 && Math.Abs(p.LengthS - p.WidthN) > 1e-6;
                            double hS = swap ? g.Dims[1] : g.Dims[0], hN = swap ? g.Dims[0] : g.Dims[1];
                            for (int i = 0; i < perB; i++)
                            {
                                double zc = (p.CapTopZ + p.SeatZ) / 2 - (i - (perB - 1) / 2.0) * In(3);
                                bars.Add(new RawBar("M", size, hs, $"{where} pedestal hoops", hl.Select(q => unit.World(p.S + (swap ? q.Y : q.X) - hS / 2, p.N + (swap ? q.X : q.Y) - hN / 2, zc)).ToArray()));
                            }
                        }
                        continue;
                    }
                    Warn($"{unit.Name}: UNRESOLVED {g.Mark} pedestal hoops - " + (g.Girders == "" ? "the girders that carry them are not given"
                        : $"the plan note's girders \"{g.Girders}\" select {sel.Count} pedestal block(s), the bar list has {g.Count} hoops")
                        + "; placed at mid-cap under the cap top (position not modeled).");
                    for (int i = 0; i < g.Count; i++)
                        bars.Add(new RawBar("M", size, hs, $"{where} pedestal hoops", hl.Select(q => unit.World(sMid + q.X - g.Dims[0] / 2, (unit.CapN0 + unit.CapN1) / 2 + q.Y - g.Dims[1] / 2, zh - i * In(3))).ToArray()));
                    continue;
                }
                for (int i = 0; i < g.Count; i++)
                {
                    var line = new[] { new Vec2(-sLen / 2, -wh / 2), new Vec2(sLen / 2, -wh / 2), new Vec2(sLen / 2, wh / 2), new Vec2(-sLen / 2, wh / 2) };
                    var (shape, local) = PolygonTie(line, size);
                    var pts = local.Select(p => unit.World(sMid + p.X, (unit.CapN0 + unit.CapN1) / 2 + p.Y, zh - i * In(3))).ToArray();
                    bars.Add(new RawBar("M", size, shape, $"{where} pedestal hoops", pts));
                }
                continue;
            }
            if (peds.Count == 0) continue;
            if (g.Shape == "U" && g.Dims.Length == 3 && g.Count % peds.Count == 0)
            {
                // 5m1 / 5n1 (plan bend diagram, pedestal plan): U bars, base out to out 5'-1 / 5'-3, legs 2'-8 hanging into the cap from the base. Per pedestal
                // g.Count / pedestals bars at equal spaces, the end bars 4" from the pedestal edges (plan: "4, 5 EQ SPA, 4"). 5m1 (longitudinal) runs along S
                // and is spaced across N; 5n1 (transverse) runs along N and is spaced across S. Vertical position (pier elevations, e.g. Design 116 PDF 19, pier 1WB
                // "6-5m1" detail): the base lies in the TOP of the pedestal block (pedestals are 8 .. 12" high), the 5n1 bases (dots in the elevation) on top with the
                // plan cover below the pedestal top, the 5m1 base one 5n1 bar below them; the legs hang through the pedestal into the cap. (Earlier the base was put
                // under the cap top: the legs reached a pedestal height deeper, below the relief line into the 6" narrower lower section.) Only with the plan pedestal
                // size (PedestalSizeAlong / Across): the bearing-footprint pedestals (9G, 19G, 11WB, 6A: plan size not settled) are smaller than these bars, whose
                // base then stays under the cap top as before (reported by the check).
                bool planPed = unit.Definition.PedestalSizeAlong > 0 && unit.Definition.PedestalSizeAcross > 0;
                int per = g.Count / peds.Count;
                bool alongS2 = g.Kind == PlanBarKind.PedestalLongitudinal;
                var trans = groups.FirstOrDefault(x => x.Kind == PlanBarKind.PedestalTransverse);
                double under = alongS2 && trans != null ? RebarCatalog.Size(trans.Size).Diameter : 0;
                var (ps, pl) = PlanU(size, g.Dims[0], g.Dims[1], g.Dims[2], g.Pin, g.Length);
                foreach (var p in peds)
                {
                    double across = alongS2 ? p.WidthN : p.LengthS;
                    double zOut = (planPed && p.Height > In(0.125) ? p.SeatZ
                        : TopSpan(p.S - (alongS2 ? g.Dims[1] / 2 : across / 2), p.S + (alongS2 ? g.Dims[1] / 2 : across / 2), p.N - (alongS2 ? across / 2 : g.Dims[1] / 2), p.N + (alongS2 ? across / 2 : g.Dims[1] / 2))) - cov - under;
                    for (int k = 0; k < per; k++)
                    {
                        double t = per == 1 ? 0 : -across / 2 + In(4) + (across - In(8)) * k / (per - 1);
                        Vec3 Pp(double u, double v) => alongS2 ? unit.World(p.S + u - g.Dims[1] / 2, p.N + t, zOut - v) : unit.World(p.S + t, p.N + u - g.Dims[1] / 2, zOut - v);
                        bars.Add(new RawBar("M", size, ps, $"{where} pedestal bars", pl.Select(q => Pp(q.X, q.Y)).ToArray()));
                    }
                }
                continue;
            }
            for (int i = 0; i < g.Count; i++)
            {
                var p = peds[i % peds.Count];
                bool alongS = g.Kind == PlanBarKind.PedestalLongitudinal;
                // The pedestal bars are U bars in the top of the cap under the bearing seat (the pedestal itself is only inches high): base just under
                // the cap top, legs down into the cap; the base keeps the cover to the cap sides at that elevation (full width above the relief curve).
                double zTop = TopAt(p.S) - cov - size.Diameter / 2;
                double nAvail = unit.CapN1 - unit.CapN0 - 2 * Setb(p.S, zTop) - 2 * (cov + size.Diameter / 2);
                double baseLen = Math.Clamp(g.Length - In(54), In(12), alongS ? Ft(8) : Math.Max(In(12), nAvail)), leg = (g.Length - baseLen) / 2;
                double lateral = ((i / peds.Count) - ((g.Count + peds.Count - 1) / peds.Count - 1) / 2.0) * In(6);
                Vec3 P(double a, double z) => alongS ? unit.World(p.S + a, p.N + lateral, z) : unit.World(p.S + lateral, p.N + a, z);
                var pts = new[] { P(-baseLen / 2, zTop - leg), P(-baseLen / 2, zTop), P(baseLen / 2, zTop), P(baseLen / 2, zTop - leg) };
                bars.Add(PlanBarFixed("M", size, $"{where} pedestal bars", pts, g.Length));
            }
        }
    }

    /// <summary>Pedestal blocks (in cap order) carrying a seat of one of the girders of a plan note ("C,D", "B-D", "G-I,B-D"; A = girder index 0; a seat of
    /// the ahead span, index + 1000, counts by its letter too). Empty when the text is empty or unreadable.</summary>
    internal static List<PedestalInfo> PedestalBlocksOf(SubstructureUnit unit, string girders)
    {
        var idx = new HashSet<int>();
        foreach (var part in (girders ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = System.Text.RegularExpressions.Regex.Match(part, @"^([A-Z])(?:-([A-Z]))?$");
            if (!m.Success) return new();
            char a = m.Groups[1].Value[0], b = m.Groups[2].Success ? m.Groups[2].Value[0] : a;
            for (char c = a; c <= b; c++) idx.Add(c - 'A');
        }
        if (idx.Count == 0) return new();
        return unit.PedestalBlocks.Where(p => unit.Pedestals.Any(q => idx.Contains(q.Girder % 1000)
                && Math.Abs(q.RowS - p.S) <= p.LengthS / 2 + 1e-6 && Math.Abs(q.RowN - p.N) <= p.WidthN / 2 + 1e-6))
            .OrderBy(p => p.S).ToList();
    }

    /// <summary>Hoop height h (out-to-out) so that the closed stirrup w x h has the listed developed length.</summary>
    private static double SolveHoopHeight(double w, double length, RebarSize size)
    {
        double lo = In(6), hi = Ft(40);
        for (int k = 0; k < 60; k++)
        {
            double mid = (lo + hi) / 2;
            if (ClosedStirrup(w, mid, size).shape.DevelopedLength < length) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }
}

internal static class BarShapeExt
{
    /// <summary>Copies the dimensions of <paramref name="from"/> onto this shape and returns it.</summary>
    public static BarShape WithDims(this BarShape s, BarShape from)
    {
        s.Dimensions.AddRange(from.Dimensions);
        s.Bends.AddRange(from.Bends);
        return s;
    }
}
