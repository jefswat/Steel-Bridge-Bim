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
                    double off = In(6), run = (g.Length > 0 ? g.Length - 2 * g.HookExtra : along - In(4)) / 2;
                    double z = g.Top ? f.TopZ - In(3) - db / 2 - Cum(true, g.Order, db) : f.BottomZ + embed + In(1) + db / 2 + Cum(false, g.Order, db);
                    string what = g.Top ? "top mat" : "bottom mat";
                    for (int i = 0; i < g.Count; i++)
                    {
                        double t = g.Count == 1 ? 0 : -across / 2 + off + (across - 2 * off) * i / (g.Count - 1);
                        Vec3 P(double a, double zz) => g.AlongN ? unit.World(cx + t, cy + a, zz) : unit.World(cx + a, cy + t, zz);
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
                bars.Add(PlanBarFixed("C", size, $"{loc} {g.Mark} side faces", Follow(n, g.Length, z, sMid), g.Length));
            }
        }
        // ---- hoops, ties, cantilever hoops, hairpins
        double w = nHi - nLo - 2 * cov;
        double Even(int k, int cnt, double a, double b) => cnt == 1 ? (a + b) / 2 : a + (b - a) * k / (cnt - 1);
        foreach (var g in groups.Where(g => g.Kind is PlanBarKind.CapHoop or PlanBarKind.CapCantileverHoop or PlanBarKind.CapTie or PlanBarKind.CapHairpin))
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
                s = FitS(s, dirIn, x => Clr(x, TopAt(x) - cov) >= cov - 1e-4 && Clr(x, BotAt(x) + cov) >= cov - 1e-4);
                double zt = TopAt(s) - cov, zb = BotAt(s) + cov;
                if (zt - zb < In(12)) continue;
                string where = $"{loc} {g.Mark}";
                if (g.Kind is PlanBarKind.CapHoop or PlanBarKind.CapCantileverHoop)
                {
                    double h = zt - zb;
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
                    // 4c5 / 4c7: straight base with 4 1/2" legs turned up at both ends (7'-5 + 2 x 4 1/2 = 8'-2 on the 1G list).
                    double s = Math.Clamp(Even(i, g.Count, s0 + cov + In(3), s1 - cov - In(3)), s0 + cov, s1 - cov);
                    s = FitS(s, s < sMid ? 1 : -1, x => TopAt(x) - BotAt(x) > In(24) && Clr(x, (TopAt(x) + BotAt(x)) / 2) >= cov + size.Diameter / 2 + In(5));
                    double leg = In(4.5), bas = Math.Max(In(12), g.Length - 2 * leg), z = (TopAt(s) + BotAt(s)) / 2, nc = (unit.CapN0 + unit.CapN1) / 2;
                    // highest elevation (full width above the relief curve) where the base fits between the cap sides with the cover
                    for (double zz = TopAt(s) - cov - In(6); zz > BotAt(s) + cov + In(12); zz -= In(2))
                        if (unit.CapN1 - unit.CapN0 - 2 * Setb(s, zz) - 2 * (cov + size.Diameter / 2) >= bas + 1e-6 && Clr(s, zz + leg) >= cov) { z = zz - leg; break; }
                    var pts = new[] { unit.World(s, nc - bas / 2, z + leg), unit.World(s, nc - bas / 2, z), unit.World(s, nc + bas / 2, z), unit.World(s, nc + bas / 2, z + leg) };
                    bars.Add(PlanBarFixed("T", size, $"{where} transverse", pts, g.Length));
                }
                else
                {
                    int end = i % 2, k = i / 2, per = (g.Count + 1) / 2;
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
        var peds = unit.Pedestals;
        foreach (var g in groups.Where(g => g.Kind is PlanBarKind.PedestalLongitudinal or PlanBarKind.PedestalTransverse or PlanBarKind.PedestalHoop))
        {
            var size = RebarCatalog.Size(g.Size);
            string where = $"{loc} {g.Mark}";
            if (g.Kind == PlanBarKind.PedestalHoop)
            {
                double zh = TopAt(sMid) - cov - size.Diameter / 2 - In(3);
                double wh = unit.CapN1 - unit.CapN0 - 2 * Setb(sMid, zh) - 2 * (cov + size.Diameter / 2);
                double sLen = Math.Max(g.Length / 2 - wh, In(12));
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
