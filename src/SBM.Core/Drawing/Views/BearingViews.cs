using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>Anchor bolt clearance results for one bearing.</summary>
public sealed record AnchorCheck(int Girder, string Mark, int Bolts, double Diameter, double Embedment, double EdgeToPedestal, double EdgeToCap,
    double BottomZ, double CapBottomZ, double ClearToBars, string NearestBar, bool Ok, string Notes);

/// <summary>
/// Bearing details at a pier for anchor bolt considerations: plan (masonry plate, anchor bolts, pedestal, cap faces and
/// cap reinforcing), a section along the girder (bearing stack, anchor embedment, cap top bars), and a table of checks
/// for every bearing: edge distances to the pedestal and cap faces, embedment within the cap, and clear distance from
/// each anchor bolt to the cap reinforcing (3D, from the generated bars).
/// </summary>
public static class BearingViews
{
    private sealed record BearingData(int Girder, string Mark, Vec2 Center, Vec2 Tx, BearingResult Br, PedestalInfo? Ped, List<BoltInstance> Anchors);

    private static List<BearingData> Bearings(ViewContext ctx, SubstructureUnit unit)
    {
        var f = ctx.Framing;
        var sl = f.SupportLine(unit.SupportIndex);
        var list = new List<BearingData>();
        for (int gi = 0; gi < sl.Bearings.Length; gi++)
        {
            var br = sl.Bearings[gi];
            if (br == null) continue;
            string mark = $"B-{sl.Name.Replace(" ", "")}-{f.Girders[gi].Name}";
            var g = f.Girders[gi];
            int nSpan = g.Spans.Count;
            double sG = !double.IsNaN(br.PathDistance) ? br.PathDistance : g.DistanceAt(Math.Min(unit.SupportIndex, nSpan - 1), unit.SupportIndex == nSpan ? 1 : 0);
            var tx = g.Path.At(sG).tangent.Normalized();
            var anchors = ctx.Steel?.Bolts.Where(b => b.Kind == BoltKind.Anchor && b.Assembly == mark).ToList() ?? new();
            list.Add(new BearingData(gi, mark, br.TopOfSteel.XY, tx, br, unit.Pedestals.FirstOrDefault(p => p.Girder == gi), anchors));
        }
        return list;
    }

    public static List<AnchorCheck> Checks(ViewContext ctx, SubstructureUnit unit, out List<string> notes)
    {
        notes = new List<string>();
        var sd = unit.Definition;
        var bars = ctx.Substructure?.Bars.Where(b => b.Unit == unit.Name).ToList() ?? new();
        var marks = ctx.Substructure?.Marks.Where(m => m.Unit == unit.Name).ToDictionary(m => m.Mark) ?? new();
        var res = new List<AnchorCheck>();
        foreach (var b in Bearings(ctx, unit))
        {
            if (b.Anchors.Count == 0) continue;
            double da = b.Anchors[0].Diameter;
            double edgePed = double.MaxValue, edgeCap = double.MaxValue, clear = double.MaxValue, bottom = double.MaxValue;
            string nearest = "";
            foreach (var a in b.Anchors)
            {
                var top = a.Frame.Origin;
                var bot = top - new Vec3(0, 0, a.Length);
                bottom = Math.Min(bottom, bot.Z);
                double s = Vec2.Dot(top.XY - unit.Origin, unit.D), n = Vec2.Dot(top.XY - unit.Origin, unit.N);
                if (b.Ped is { } p && p.Height >= In(0.125))
                    edgePed = Math.Min(edgePed, Math.Min(Math.Min(s - (p.S - p.LengthS / 2), p.S + p.LengthS / 2 - s), Math.Min(n - (p.N - p.WidthN / 2), p.N + p.WidthN / 2 - n)));
                edgeCap = Math.Min(edgeCap, Math.Min(Math.Min(n - unit.CapN0, unit.CapN1 - n), Math.Min(s - unit.CapS0, unit.CapS1 - s)));
                foreach (var bar in bars)
                {
                    if (!marks.TryGetValue(bar.Mark, out var mk)) continue;
                    for (int i = 1; i < bar.Points.Length; i++)
                    {
                        double dist = SegmentDistance(top, bot, bar.Points[i - 1], bar.Points[i]) - da / 2 - mk.Size.Diameter / 2;
                        if (dist < clear) { clear = dist; nearest = $"{mk.Mark} ({mk.Size.Name})"; }
                    }
                }
            }
            double capBot = unit.CapBottomAt(Vec2.Dot(b.Center - unit.Origin, unit.D));
            double minEdge = Math.Max(6 * da, In(4));
            var why = new List<string>();
            if (edgePed < minEdge && edgePed < double.MaxValue) why.Add("pedestal edge");
            if (edgeCap < minEdge) why.Add("cap edge");
            if (clear < In(1)) why.Add(clear < 0 ? "hits rebar" : "rebar clearance");
            if (bottom < capBot + In(3)) why.Add("embedment below cap");
            res.Add(new AnchorCheck(b.Girder, b.Mark, b.Anchors.Count, da, b.Anchors[0].Embedment, edgePed, edgeCap, bottom, capBot,
                clear, nearest, why.Count == 0, string.Join(", ", why)));
        }
        notes.Add($"Edge distance target: max(6 x anchor dia, 4\"); clear distance to reinforcing target: 1\"; anchors must stop 3\" above the bottom of cap.");
        notes.Add("Clearances are measured in 3D between each anchor bolt and the generated cap bars (stirrups, top, side and bottom bars).");
        return res;
    }

    /// <summary>Anchor bolt check table (paper inches).</summary>
    public static SheetView Checks(ViewContext ctx, SubstructureUnit unit)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1);
        var checks = Checks(ctx, unit, out var notes);
        string S(double m) => m >= 1e6 ? "—" : u.FormatLength(m, LengthKind.Short);
        var cols = new (string h, double w)[] { ("BEARING", 1.6), ("ANCHORS", 1.3), ("EMBED.", 0.9), ("EDGE TO PED.", 1.3), ("EDGE TO CAP", 1.3), ("BOTTOM EL", 1.2), ("CAP BOTT. EL", 1.3), ("CLEAR TO BARS", 1.4), ("NEAREST BAR", 1.4), ("STATUS", 2.6) };
        double rowH = d.Paper(0.22), y = 0, tw = d.Paper(cols.Sum(c => c.w));
        d.Text($"{unit.Name.ToUpperInvariant()} — ANCHOR BOLT CHECKS", new Vec2(0, d.Paper(0.3)), d.Paper(0.14), 0, TextAlign.Left, SheetLayers.Title);
        int rows = checks.Count + 1;
        d.Rect(SheetLayers.Text, 0, -rowH * rows, tw, 0);
        double x = 0;
        foreach (var (h, w) in cols)
        {
            d.Text(h, new Vec2(x + d.Paper(0.05), -rowH / 2), d.Paper(0.085), 0, TextAlign.Left);
            x += d.Paper(w);
            d.Line(SheetLayers.Text, new Vec2(x, 0), new Vec2(x, -rowH * rows));
        }
        for (int i = 0; i < checks.Count; i++)
        {
            var c = checks[i];
            double ry = -rowH * (i + 1);
            d.Line(SheetLayers.Text, new Vec2(0, ry), new Vec2(tw, ry));
            var vals = new[]
            {
                c.Mark, $"{c.Bolts} - {S(c.Diameter)}", S(c.Embedment), S(c.EdgeToPedestal), S(c.EdgeToCap), d.Elev(c.BottomZ), d.Elev(c.CapBottomZ),
                S(c.ClearToBars), c.NearestBar, c.Ok ? "OK" : "CHECK: " + c.Notes,
            };
            x = 0;
            for (int k = 0; k < cols.Length; k++)
            {
                d.Text(vals[k], new Vec2(x + d.Paper(0.05), ry - rowH / 2), d.Paper(0.085), 0, TextAlign.Left, k == cols.Length - 1 && !c.Ok ? SheetLayers.Rebar : SheetLayers.Text);
                x += d.Paper(cols[k].w);
            }
        }
        for (int i = 0; i < notes.Count; i++)
            d.Text(notes[i], new Vec2(0, -rowH * rows - d.Paper(0.25 + 0.18 * i)), d.Paper(0.08), 0, TextAlign.Left);
        return new SheetView("Anchor bolt checks", "Anchors", dwg, 1, "", PaperSpace: true);
    }

    /// <summary>The bearing with the smallest margin (or G1).</summary>
    private static BearingData? Typical(ViewContext ctx, SubstructureUnit unit)
    {
        var all = Bearings(ctx, unit).Where(b => b.Anchors.Count > 0).ToList();
        if (all.Count == 0) return Bearings(ctx, unit).FirstOrDefault();
        var checks = Checks(ctx, unit, out _);
        var worst = checks.OrderBy(c => c.Ok).ThenBy(c => Math.Min(c.ClearToBars, Math.Min(c.EdgeToPedestal, c.EdgeToCap))).FirstOrDefault();
        return all.FirstOrDefault(b => b.Girder == worst?.Girder) ?? all[0];
    }

    /// <summary>
    /// Bearing plan drawn with the girder axis horizontal (X along the girder, Y across, up-station to the right), so the
    /// masonry plate and anchor bolts dimension square; the pedestal, cap faces and cap bars are rotated by the skew.
    /// </summary>
    public static SheetView Plan(ViewContext ctx, SubstructureUnit unit)
    {
        var u = ctx.Units;
        var def = ctx.Framing.Definition;
        var bd = def.BearingsAt(unit.SupportIndex);
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var b = Typical(ctx, unit);
        if (b == null) return new SheetView("Bearing plan", "Bearing Plan", dwg, d.Scale, "");
        var fixP = def.SupportAt(unit.SupportIndex).FixityOf(b.Girder);
        var tx = Vec2.Dot(b.Tx, unit.N) >= 0 ? b.Tx : -b.Tx;   // girder axis pointing up-station
        var ty = tx.Left;
        Vec2 L(Vec2 world) => new(Vec2.Dot(world - b.Center, tx), Vec2.Dot(world - b.Center, ty));
        Vec2 Cap(double s, double n) => L(unit.Plan(s, n));
        double sB = Vec2.Dot(b.Center - unit.Origin, unit.D);
        bool disc = bd.Type == BearingType.Disc;
        var (lm, wm) = bd.Footprint(fixP);
        double half = Math.Max(lm, wm) / 2 + Ft(1.6);

        // Cap faces (clipped to the window, along the skewed cap line) and the pedestal.
        double ext = half * 1.4;
        foreach (double n in new[] { unit.CapN0, unit.CapN1 })
        {
            var p0 = Cap(sB - ext, n);
            var p1 = Cap(sB + ext, n);
            d.Line(SheetLayers.Concrete, p0, p1);
            if (n == unit.CapN1) d.TextAlong("CAP FACE", p0 + (p1 - p0).Normalized() * d.Paper(0.1) + (p1 - p0).Normalized().Left * d.Paper(0.08), p1 - p0, d.SmallTextH);
        }
        Vec2[]? ped = null;
        if (b.Ped is { } pd && pd.Height >= In(0.125))
        {
            ped = new[]
            {
                Cap(pd.S - pd.LengthS / 2, pd.N - pd.WidthN / 2), Cap(pd.S + pd.LengthS / 2, pd.N - pd.WidthN / 2),
                Cap(pd.S + pd.LengthS / 2, pd.N + pd.WidthN / 2), Cap(pd.S - pd.LengthS / 2, pd.N + pd.WidthN / 2),
            };
            d.Poly(SheetLayers.Concrete, ped, true);
            var top = ped.OrderByDescending(q => q.Y).First();
            d.Leader(top, top + new Vec2(d.Paper(0.45), d.Paper(0.3)), $"PEDESTAL {d.Short(pd.LengthS)} x\n{d.Short(pd.WidthN)}");
        }

        // Cap reinforcing near the bearing: top bars along the cap, stirrups across.
        if (ctx.Substructure != null)
        {
            var marks = ctx.Substructure.Marks.Where(m => m.Unit == unit.Name).ToDictionary(m => m.Mark);
            bool labelS = false, labelA = false;
            foreach (var bar in ctx.Substructure.Bars.Where(x => x.Unit == unit.Name && (x.Mark[0] == 'A' || x.Mark[0] == 'S')))
            {
                var pts = bar.Points.Select(q => L(q.XY)).ToList();
                if (bar.Mark[0] == 'S')
                {
                    if (pts.Max(q => q.Length) > half * 1.6) continue;
                    var p0 = pts.OrderBy(q => Vec2.Dot(q, unit.N)).First();
                    var p1 = pts.OrderBy(q => Vec2.Dot(q, unit.N)).Last();
                    d.Line(SheetLayers.Rebar, p0, p1);
                    if (!labelS) { d.Leader(Vec2.Lerp(p0, p1, 0.85), Vec2.Lerp(p0, p1, 0.85) + new Vec2(-d.Paper(0.5), d.Paper(0.5)), $"STIRRUPS {bar.Mark} ({marks[bar.Mark].Size.Name})\n@ {d.Short(unit.Definition.Reinforcement.CapStirrupSpacing)} MAX"); labelS = true; }
                }
                else
                {
                    // Clip the long top bar to the window.
                    var dir = (pts[^1] - pts[0]).Normalized();
                    var mid = pts[0] + dir * Vec2.Dot(-pts[0], dir);
                    if (mid.Length > half * 1.2) continue;
                    var p0 = mid - dir * half * 1.3;
                    var p1 = mid + dir * half * 1.3;
                    d.Line(SheetLayers.Rebar, p0, p1);
                    if (!labelA) { d.Leader(p1, p1 + new Vec2(d.Paper(0.3), -d.Paper(0.3)), $"TOP BARS\n{bar.Mark} ({marks[bar.Mark].Size.Name})"); labelA = true; }
                }
            }
        }

        // Bearing plates.
        Vec2[] R(double l, double w) => new[] { new Vec2(-l / 2, -w / 2), new Vec2(l / 2, -w / 2), new Vec2(l / 2, w / 2), new Vec2(-l / 2, w / 2) };
        d.Poly(SheetLayers.Steel, R(lm, wm), true);
        if (disc)
        {
            d.Poly(SheetLayers.SteelHidden, R(bd.UpperPlateLength, bd.UpperPlateWidth), true);
            d.Circle(SheetLayers.SteelHidden, new Vec2(0, 0), bd.DiscDiameter / 2);
        }
        else if (bd.Type == BearingType.Generic)
            d.Poly(SheetLayers.SteelHidden, R(bd.GenericBodyLength, bd.GenericBodyWidth), true);
        var bf = ctx.Framing.PlateAtBearing(b.Br, GirderPlate.BottomFlange);
        d.Line(SheetLayers.SteelHidden, new Vec2(-half, -bf.Width / 2), new Vec2(half, -bf.Width / 2));
        d.Line(SheetLayers.SteelHidden, new Vec2(-half, bf.Width / 2), new Vec2(half, bf.Width / 2));
        d.Line(SheetLayers.Center, new Vec2(-half, 0), new Vec2(half, 0));
        d.Text($"CL {ctx.Framing.Girders[b.Girder].Name}", new Vec2(half + d.Paper(0.08), 0), d.SmallTextH, 0, TextAlign.Left);
        var brgA = Cap(sB - ext * 0.8, Vec2.Dot(b.Center - unit.Origin, unit.N));
        var brgB = Cap(sB + ext * 0.8, Vec2.Dot(b.Center - unit.Origin, unit.N));
        d.Line(SheetLayers.Center, brgA, brgB);
        d.TextAlong($"CL BRG {unit.Name.ToUpperInvariant()}", brgB + (brgB - brgA).Normalized() * d.Paper(0.05), brgB - brgA, d.SmallTextH);

        // Anchor bolts and dimensions (square to the plate).
        double y0 = -wm / 2, y1 = wm / 2, x0 = -lm / 2, x1 = lm / 2;
        d.Dim(new Vec2(x0, y0), new Vec2(x1, y0), -0.75, $"{d.Short(lm)} PLATE");
        d.Dim(new Vec2(x0, y0), new Vec2(x0, y1), 0.75, $"{d.Short(wm)} PLATE");
        if (b.Anchors.Count > 0)
        {
            var aps = b.Anchors.Select(a => L(a.Frame.Origin.XY)).ToList();
            foreach (var q in aps)
            {
                d.Circle(SheetLayers.Steel, q, b.Anchors[0].Diameter / 2);
                d.Circle(SheetLayers.SteelHidden, q, (b.Anchors[0].Diameter + bd.AnchorHoleOversize) / 2);
            }
            var ax = aps.Select(q => q.X).Distinct().OrderBy(v => v).Aggregate(new List<double>(), (l, v) => { if (l.Count == 0 || v - l[^1] > In(0.1)) l.Add(v); return l; });
            var ay = aps.Select(q => q.Y).Distinct().OrderBy(v => v).Aggregate(new List<double>(), (l, v) => { if (l.Count == 0 || v - l[^1] > In(0.1)) l.Add(v); return l; });
            // Along the girder: edge – spacing – edge, above the plate.
            var chainX = new List<double> { x0 };
            chainX.AddRange(ax);
            chainX.Add(x1);
            d.DimChain(chainX.Select(x => new Vec2(x, y1)).ToList(), 0.45, i => d.Short(chainX[i + 1] - chainX[i]));
            var chainY = new List<double> { y0 };
            chainY.AddRange(ay);
            chainY.Add(y1);
            d.DimChain(chainY.Select(y => new Vec2(x1, y)).ToList(), -0.45, i => d.Short(chainY[i + 1] - chainY[i]));
            var q0 = aps.OrderBy(q => q.X).ThenBy(q => q.Y).First();
            d.Leader(q0, q0 + new Vec2(-d.Paper(0.75), -d.Paper(0.9)),
                $"{b.Anchors.Count} - {d.Short(b.Anchors[0].Diameter)} DIA ANCHOR BOLTS\n{bd.AnchorBoltGrade}, {d.Short(b.Anchors[0].Embedment)} EMBED.\n{d.Short(b.Anchors[0].Diameter + bd.AnchorHoleOversize)} HOLES IN MASONRY PL");
            // Shortest distance from an anchor to the pedestal and cap faces.
            if (ped != null)
            {
                double best = double.MaxValue;
                Vec2 bp = default, bq = default;
                foreach (var q in aps)
                    for (int i = 0; i < 4; i++)
                    {
                        var e0 = ped[i];
                        var e1 = ped[(i + 1) % 4];
                        var dir = (e1 - e0).Normalized();
                        var foot = e0 + dir * Math.Clamp(Vec2.Dot(q - e0, dir), 0, Vec2.Distance(e0, e1));
                        double dist = Vec2.Distance(q, foot);
                        if (dist < best) { best = dist; bp = q; bq = foot; }
                    }
                d.Line(SheetLayers.Dims, bp, bq);
                d.Leader(Vec2.Lerp(bp, bq, 0.5), new Vec2(Math.Max(bp.X, bq.X) + d.Paper(0.9), Math.Min(bp.Y, bq.Y) - d.Paper(0.9)), $"{d.Short(best)} MIN. ANCHOR\nTO PEDESTAL FACE");
            }
        }
        var box = dwg.Extents();
        d.Title($"BEARING PLAN — {b.Mark}", "Girder axis horizontal, up-station to the right; the bearing with the smallest anchor margin is shown",
            new Vec2((box.MinX + box.MaxX) / 2, box.MinY - d.Paper(0.3)));
        return new SheetView("Bearing plan", "Bearing Plan", dwg, d.Scale, "");
    }

    public static SheetView Section(ViewContext ctx, SubstructureUnit unit)
    {
        var u = ctx.Units;
        var def = ctx.Framing.Definition;
        var bd = def.BearingsAt(unit.SupportIndex);
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var b = Typical(ctx, unit);
        if (b == null) return new SheetView("Bearing section", "Bearing Section", dwg, d.Scale, "");
        var br = b.Br;
        var sec = def.Girders.Section;
        var bf = ctx.Framing.PlateAtBearing(br, GirderPlate.BottomFlange);
        double zbs = br.BottomOfSteelElevation;
        var gB = ctx.Framing.Girders[b.Girder];
        double grade = gB.Path.GradeAt(!double.IsNaN(br.PathDistance) ? br.PathDistance
            : gB.DistanceAt(Math.Min(unit.SupportIndex, gB.Spans.Count - 1), unit.SupportIndex == gB.Spans.Count ? 1 : 0));
        double T(Vec2 world) => Vec2.Dot(world - b.Center, b.Tx);
        bool disc = bd.Type == BearingType.Disc, generic = bd.Type == BearingType.Generic;
        double ls = bd.SolePlateLength;
        // Girder bottom flange + web stub (sloped with the grade).
        double half = Math.Max(ls, bd.Footprint(def.SupportAt(unit.SupportIndex).FixityOf(b.Girder)).Length) / 2 + Ft(1.2);
        d.Poly(SheetLayers.Steel, new[] { new Vec2(-half, zbs - grade * half), new Vec2(half, zbs + grade * half), new Vec2(half, zbs + grade * half + bf.Thickness), new Vec2(-half, zbs - grade * half + bf.Thickness) }, true);
        d.Line(SheetLayers.Steel, new Vec2(-half, zbs - grade * half + bf.Thickness + sec.WebThickness), new Vec2(half, zbs + grade * half + bf.Thickness + sec.WebThickness));
        // Sole plate (tapered, bottom level), stack below.
        double zb = zbs - Math.Abs(grade) * ls / 2 - bd.SolePlateMinThickness;
        d.Poly(SheetLayers.Steel, new[] { new Vec2(-ls / 2, zb), new Vec2(ls / 2, zb), new Vec2(ls / 2, zbs + grade * ls / 2), new Vec2(-ls / 2, zbs - grade * ls / 2) }, true);
        double z = zb;
        var fix = def.SupportAt(unit.SupportIndex).FixityOf(b.Girder);
        var (lmP, _) = bd.Footprint(fix);
        if (generic)
        {
            double hb = Math.Max(0, bd.GenericBodyHeight(grade));
            d.Rect(SheetLayers.Steel, -bd.GenericBodyLength / 2, z - hb, bd.GenericBodyLength / 2, z); z -= hb;
            d.Rect(SheetLayers.Steel, -lmP / 2, z - bd.MasonryPlateThickness, lmP / 2, z); z -= bd.MasonryPlateThickness;
        }
        else if (disc)
        {
            double lu = bd.UpperPlateLength + (fix != BearingFixity.Fixed ? bd.ExpansionPlateExtension : 0);
            d.Rect(SheetLayers.Steel, -lu / 2, z - bd.UpperPlateThickness, lu / 2, z); z -= bd.UpperPlateThickness;
            if (fix != BearingFixity.Fixed) { d.Rect(SheetLayers.Steel, -bd.DiscDiameter / 2, z - bd.PtfeThickness, bd.DiscDiameter / 2, z); z -= bd.PtfeThickness; }
            d.Rect(SheetLayers.Steel, -bd.DiscDiameter / 2, z - bd.DiscHeight, bd.DiscDiameter / 2, z); z -= bd.DiscHeight;
            d.Rect(SheetLayers.Steel, -lmP / 2, z - bd.MasonryPlateThickness, lmP / 2, z); z -= bd.MasonryPlateThickness;
        }
        if (bd.Type != BearingType.Elastomeric && bd.MasonryPadThickness > 0)
        {
            d.Rect(SheetLayers.Bearing, -lmP / 2, z - bd.MasonryPadThickness, lmP / 2, z); z -= bd.MasonryPadThickness;
        }
        else { d.Rect(SheetLayers.Bearing, -bd.PadLength / 2, z - bd.PadHeight, bd.PadLength / 2, z); z -= bd.PadHeight; }
        double seat = z;
        // Pedestal and cap cut along the girder line.
        double sB = Vec2.Dot(b.Center - unit.Origin, unit.D);
        double capTop = unit.CapTopAt(sB), capBot = unit.CapBottomAt(sB);
        double kN = Vec2.Dot(b.Tx, unit.N);
        double nB = Vec2.Dot(b.Center - unit.Origin, unit.N);
        double t0 = Math.Abs(kN) > 0.05 ? (unit.CapN0 - nB) / kN : -half, t1 = Math.Abs(kN) > 0.05 ? (unit.CapN1 - nB) / kN : half;
        if (t0 > t1) (t0, t1) = (t1, t0);
        if (b.Ped is { } p && p.Height >= In(0.125))
        {
            double pa = (p.N - p.WidthN / 2 - nB) / kN, pb = (p.N + p.WidthN / 2 - nB) / kN;
            if (pa > pb) (pa, pb) = (pb, pa);
            d.CutConcrete(new List<Vec2> { new(pa, capTop), new(pb, capTop), new(pb, seat), new(pa, seat) }, 0.05);
        }
        d.CutConcrete(new List<Vec2> { new(t0, capBot), new(t1, capBot), new(t1, capTop), new(t0, capTop) }, 0.08);
        // Anchor bolts (projected) and cap bars cut by the section plane.
        foreach (var a in b.Anchors)
        {
            double ta = T(a.Frame.Origin.XY);
            double top = a.Frame.Origin.Z + Math.Max(In(2), a.Diameter * 1.6), bot = a.Frame.Origin.Z - a.Length;
            d.Rect(SheetLayers.Steel, ta - a.Diameter / 2, bot, ta + a.Diameter / 2, top);
        }
        if (ctx.Substructure != null)
        {
            var nrm = b.Tx.Left;
            var marks = ctx.Substructure.Marks.Where(m => m.Unit == unit.Name).ToDictionary(m => m.Mark);
            foreach (var bar in ctx.Substructure.Bars.Where(x => x.Unit == unit.Name && x.Mark[0] is 'A' or 'B' or 'C'))
                for (int i = 1; i < bar.Points.Length; i++)
                {
                    double da = Vec2.Dot(bar.Points[i - 1].XY - b.Center, nrm), db = Vec2.Dot(bar.Points[i].XY - b.Center, nrm);
                    if (da * db > 0 || Math.Abs(da - db) < 1e-12) continue;
                    var q = Vec3.Lerp(bar.Points[i - 1], bar.Points[i], da / (da - db));
                    double t = T(q.XY);
                    if (t < t0 - In(1) || t > t1 + In(1)) continue;
                    d.Circle(SheetLayers.Rebar, new Vec2(t, q.Z), Math.Max(d.Paper(0.012), marks[bar.Mark].Size.Diameter / 2), true);
                    break;
                }
        }
        // Elevations and embedment.
        d.ElevationTag(new Vec2(half, zbs + grade * half), $"BOTT. OF STEEL AT CL BRG EL {d.Elev(zbs)}");
        d.ElevationTag(new Vec2(t1, seat), $"BRG SEAT EL {d.Elev(seat)}");
        d.ElevationTag(new Vec2(t1, capTop), $"TOP OF CAP EL {d.Elev(capTop)}");
        d.ElevationTag(new Vec2(t1, capBot), $"BOTT. OF CAP EL {d.Elev(capBot)}");
        if (b.Anchors.Count > 0)
        {
            var a0 = b.Anchors.OrderBy(a => T(a.Frame.Origin.XY)).First();
            double ta = T(a0.Frame.Origin.XY);
            double top = a0.Frame.Origin.Z - bd.MasonryPlateThickness - (bd.Type != BearingType.Elastomeric ? bd.MasonryPadThickness : 0), bot = a0.Frame.Origin.Z - a0.Length;
            d.Dim(new Vec2(ta, bot), new Vec2(ta, top), 0.5, $"{d.Short(top - bot)} EMBED.");
            d.ElevationTag(new Vec2(t0, bot), $"BOTT. OF ANCHOR EL {d.Elev(bot)}", right: false);
        }
        var box = dwg.Extents();
        string pad = bd.MasonryPadThickness > 0 ? ", masonry pad" : "";
        string stack = generic ? $"Sole plate, bearing by manufacturer (assumed height {d.Short(bd.GenericHeight)}), masonry plate{pad}"
            : disc ? (fix != BearingFixity.Fixed ? "Sole plate, upper bearing plate, PTFE, disc, masonry plate" : "Sole plate, upper bearing plate, disc, masonry plate") + pad : "Sole plate, elastomeric pad";
        d.Title($"SECTION ALONG {ctx.Framing.Girders[b.Girder].Name} AT {unit.Name.ToUpperInvariant()}", $"{stack} ({fix})", new Vec2(0, box.MinY - d.Paper(0.3)));
        return new SheetView("Bearing section", "Bearing Section", dwg, d.Scale, "");
    }

    /// <summary>Shortest distance between two 3D segments.</summary>
    public static double SegmentDistance(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2)
    {
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r = p1 - p2;
        double a = Vec3.Dot(d1, d1), e = Vec3.Dot(d2, d2), f = Vec3.Dot(d2, r);
        double s, t;
        if (a < 1e-15 && e < 1e-15) return (p1 - p2).Length;
        if (a < 1e-15) { s = 0; t = Math.Clamp(f / e, 0, 1); }
        else
        {
            double c = Vec3.Dot(d1, r);
            if (e < 1e-15) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
            else
            {
                double b = Vec3.Dot(d1, d2), denom = a * e - b * b;
                s = denom > 1e-15 ? Math.Clamp((b * f - c * e) / denom, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((b - c) / a, 0, 1); }
            }
        }
        return ((p1 + d1 * s) - (p2 + d2 * t)).Length;
    }
}
