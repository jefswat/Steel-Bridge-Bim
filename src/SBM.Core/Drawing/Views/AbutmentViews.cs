using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// 2D views of an as-designed abutment (<see cref="AbutmentDefinition"/>): the rear elevation along the support line (footing, beam steps,
/// the four lines parallel to the crown line, maskwalls, extension, hidden piles), the section through the abutment at the tallest beam step
/// (the plans' "SECTION THROUGH ABUTMENT"), and the piling / wing layout plan with the pile stations.
/// </summary>
public static class AbutmentViews
{
    /// <param name="part">0 = rear elevation, 1 = section through the abutment, 2 = piling and wing layout plan.</param>
    public static SheetView Abutment(ViewContext ctx, SubstructureUnit unit, int part = 0)
    {
        var g = unit.Abutment!;
        var ad = g.Def;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.BentScale);
        double D = ad.FootingDepth, W = ad.WashHeight;
        double mir = g.Sigma < 0 ? -1 : 1;           // viewed from the rear: the last support looks down-station (S mirrored)
        double Xs(double s) => mir * s;
        double nMid = unit.CapMidN;
        double s0 = unit.CapS0, s1 = unit.CapS1;
        var fb = unit.CapBottom;

        // ================= rear elevation
        var foot = new List<Vec2>();
        foreach (var p in fb) foot.Add(new Vec2(Xs(p.X), p.Y));
        foreach (var p in Enumerable.Reverse(fb)) foot.Add(new Vec2(Xs(p.X), p.Y + D));
        d.CutConcrete(foot, 0.06);
        double sbL = g.BwLeftS(g.N(g.XBwBack)), sbR = g.BwRightS(g.N(g.XBwBack));
        var xsB = new[] { sbL }.Concat(ad.BackwallTop.Select(p => p.S).Where(x => x > sbL && x < sbR)).Append(sbR).ToList();
        // the four lines parallel to the crown line (top, ledge, bottom of the vertical corbel face, bottom of the taper) and the construction joint
        double[] offs = { 0, ad.LedgeBelowTop, ad.LedgeBelowTop + ad.CorbelVertical, ad.LedgeBelowTop + ad.CorbelVertical + ad.CorbelTaperRise, g.JointDepth };
        // the corbel lines (ledge, vertical face, taper) stop at the corbel ends (wing inner faces)
        double scL = g.CorbelLeftS?.Invoke(g.N(g.XBwBack)) ?? sbL, scR = g.CorbelRightS?.Invoke(g.N(g.XBwBack)) ?? sbR;
        var xsC = new[] { scL }.Concat(ad.BackwallTop.Select(p => p.S).Where(x => x > scL && x < scR)).Append(scR).ToList();
        for (int k = 0; k < offs.Length; k++)
            d.Poly(k == 4 ? SheetLayers.ConcreteHidden : SheetLayers.Concrete, (k is >= 1 and <= 3 ? xsC : xsB).Select(s => new Vec2(Xs(s), g.BackwallTop(s) - offs[k])));
        foreach (double sc in new[] { scL, scR }.Where(s => s > sbL + 1e-4 && s < sbR - 1e-4))
            d.Line(SheetLayers.Concrete, new Vec2(Xs(sc), g.BackwallTop(sc)), new Vec2(Xs(sc), g.BackwallBaseZ(sc)));      // wing inner face (seen from the rear)
        d.Line(SheetLayers.Concrete, new Vec2(Xs(sbL), g.BackwallTop(sbL)), new Vec2(Xs(sbL), g.BackwallBaseZ(sbL) + D * 0));
        d.Line(SheetLayers.Concrete, new Vec2(Xs(sbR), g.BackwallTop(sbR)), new Vec2(Xs(sbR), g.BackwallBaseZ(sbR)));
        d.Poly(SheetLayers.Concrete, new[] { new Vec2(Xs(sbL), g.BackwallBaseZ(sbL)), new Vec2(Xs(sbR), g.BackwallBaseZ(sbR)) });
        foreach (var st in g.Steps)
        {
            d.Poly(SheetLayers.Concrete, new[] { new Vec2(Xs(st.S0), g.SeatBaseZ(st.S0)), new Vec2(Xs(st.S0), st.TopZ), new Vec2(Xs(st.S1), st.TopZ), new Vec2(Xs(st.S1), g.SeatBaseZ(st.S1)) });
            d.Text(st.Tag, new Vec2(Xs(st.S), st.TopZ + d.Paper(0.12)), d.TextH, 0, TextAlign.Center);
        }
        foreach (var mk in g.Masks)
        {
            double sA = unit.CapS0, sB = unit.CapS1;
            double sm = mk.End == AbutmentEnd.Left ? sA : sB;
            double sIn = mk.End == AbutmentEnd.Left ? g.BwLeftS(g.N(g.XBwBack)) : g.BwRightS(g.N(g.XBwBack));
            d.Poly(SheetLayers.Concrete, new[] { new Vec2(Xs(sm), mk.BaseZ), new Vec2(Xs(sm), mk.TopZ), new Vec2(Xs(sIn), mk.TopZ), new Vec2(Xs(sIn), mk.BaseZ) });
            d.ElevationTag(new Vec2(Xs(sm), mk.TopZ), $"MASKWALL TOP EL {d.Elev(mk.TopZ)}", right: mir * (mk.End == AbutmentEnd.Left ? -1 : 1) > 0);
        }
        // Wingwalls, wing footings and counterforts as seen from the rear (outline of each generated solid projected on the elevation).
        bool wingLabel = false;
        foreach (var w in unit.Solids.Where(x => x.Kind is SubElementKind.Wingwall or SubElementKind.WingFooting or SubElementKind.Counterfort))
        {
            var hull = Solids.MeshBuilder.ConvexHull(w.Mesh.Positions.Select(p => new Vec2(Xs(Vec2.Dot(p.XY - unit.Origin, unit.D)), p.Z)));
            if (hull.Count < 3) continue;
            d.Poly(w.Kind == SubElementKind.Wingwall ? SheetLayers.Concrete : SheetLayers.ConcreteHidden, hull, true);
            if (!wingLabel && w.Kind == SubElementKind.Wingwall)
            {
                var top = hull.OrderByDescending(q => q.Y).First();
                d.Leader(top, top + new Vec2(d.Paper(0.3), d.Paper(0.35)), "WINGWALL (SEE PILING AND WING LAYOUT)");
                wingLabel = true;
            }
        }
        if (g.Extension is { } ex)
            d.Poly(SheetLayers.Concrete, new[] { new Vec2(Xs(ex.S0), g.FootingBottom(ex.S0)), new Vec2(Xs(ex.S0), ex.TopZ), new Vec2(Xs(ex.S1), ex.TopZ), new Vec2(Xs(ex.S1), g.FootingBottom(ex.S1)) });
        foreach (var (ps, _) in g.PileXY.GroupBy(p => p.S).Select(x => (x.Key, 0)))
        {
            double top = g.FootingBottom(ps) + ad.PileTopAboveFootingBottom, bot = g.FootingBottom(ps) - Ft(3);
            d.Poly(SheetLayers.SteelHidden, new[] { new Vec2(Xs(ps) - In(7.3), top), new Vec2(Xs(ps) - In(7.3), bot) });
            d.Poly(SheetLayers.SteelHidden, new[] { new Vec2(Xs(ps) + In(7.3), top), new Vec2(Xs(ps) + In(7.3), bot) });
        }
        var tops = unit.Backwall!.Value.Top;
        d.ElevationTag(new Vec2(Xs(tops[0].X), tops[0].Y), $"TOP OF BACKWALL EL {d.Elev(tops[0].Y)}", right: mir < 0);
        d.ElevationTag(new Vec2(Xs(tops[^1].X), tops[^1].Y), $"TOP OF BACKWALL EL {d.Elev(tops[^1].Y)}", right: mir > 0);
        d.ElevationTag(new Vec2(Xs(fb[0].X), fb[0].Y), $"BOTTOM OF FOOTING EL {d.Elev(fb[0].Y)}", right: mir < 0);
        if (fb.Count > 1) d.ElevationTag(new Vec2(Xs(fb[^1].X), fb[^1].Y), $"BOTTOM OF FOOTING EL {d.Elev(fb[^1].Y)}", right: mir > 0);
        double zLow = fb.Min(p => p.Y) - Ft(3);
        if (g.Steps.Count > 1)
            d.DimChain(g.Steps.OrderBy(x => x.S).Select(x => new Vec2(Xs(x.S), zLow - d.Paper(0.3))).ToList(), 0);
        d.Dim(new Vec2(Xs(s0), zLow - d.Paper(0.9)), new Vec2(Xs(s1), zLow - d.Paper(0.9)), 0, $"{d.Len(s1 - s0)} FOOTING (ALONG SKEW)");
        // Beam step tops, a few per line so the list stays within the view; integral caps: the step top at each beam line.
        var stepList = g.Steps.OrderBy(x => x.S).Select(x => $"{x.Tag} {d.Elev(x.TopZ)}").ToList();
        if (stepList.Count == 0 && g.StepTop != null)
            for (int gi = 0; gi < ctx.Framing.Supports[unit.SupportIndex].Bearings.Length; gi++)
                if (ctx.Framing.Supports[unit.SupportIndex].Bearings[gi] is { } br)
                {
                    double sb = Vec2.Dot(ctx.Framing.Alignment.PointAt(br.Station, br.Offset) - unit.Origin, unit.D);
                    stepList.Add($"{ctx.GirderName(gi)} {d.Elev(g.StepTop(sb))}");
                }
        for (int i = 0; i < stepList.Count; i += 5)
            d.Text((i == 0 ? "BEAM STEP TOPS: " : "                ") + string.Join("  ", stepList.Skip(i).Take(5)), new Vec2(Xs(s0), zLow - d.Paper(1.35 + 0.17 * (i / 5))), d.SmallTextH, 0, TextAlign.Left);
        d.Line(SheetLayers.Center, new Vec2(0, zLow - d.Paper(1.1)), new Vec2(0, tops.Max(p => p.Y) + d.Paper(0.4)));
        d.Text("CL ALIGNMENT", new Vec2(0, tops.Max(p => p.Y) + d.Paper(0.55)), d.TextH, 0, TextAlign.Center);
        var box = dwg.Extents();
        string looking = g.Sigma > 0 ? "Looking up-station from the rear (fill side)" : "Looking down-station from the rear (fill side), S mirrored";
        d.Title($"{unit.Name.ToUpperInvariant()} — REAR ELEVATION", $"{looking}; lines parallel to the crown line · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((Xs(s0) + Xs(s1)) / 2, box.MinY - d.Paper(0.5)));

        // ================= section through the abutment at the tallest beam step (normal to the line)
        var dwgS = new PlanDrawing();
        var dS = new Drafter(dwgS, u, ctx.Options.BentScale);
        double xLeft = 0;
        var tall = g.Steps.OrderByDescending(x => x.TopZ - x.BaseZ).FirstOrDefault();
        double sC = tall?.S ?? (s0 + s1) / 2;
        double X(double x) => xLeft + (x - g.XFootBack);
        double zb = g.FootingBottom(sC), zt = g.BackwallTop(sC);
        double K = ad.BackwallBaseAboveFootingBottom, xF = g.XFootFront, xB = g.XFootBack;
        dS.CutConcrete(new List<Vec2>
        {
            new(X(xB), zb), new(X(xF), zb), new(X(xF), zb + D + W), new(X(g.XBwFront), zb + D + W), new(X(g.XBwFront), zb + K), new(X(g.XBwBack), zb + K), new(X(g.XBwBack), zb + D), new(X(xB), zb + D),
        }, 0.06);
        double cj = zt - g.JointDepth, Pj = ad.CorbelProjection;
        dS.CutConcrete(new List<Vec2> { new(X(g.XBwBack), zb + K), new(X(g.XBwFront), zb + K), new(X(g.XBwFront), cj), new(X(g.XBwBack), cj) }, 0.06);
        dS.CutConcrete(new List<Vec2>
        {
            new(X(g.XBwFront), cj), new(X(g.XBwFront), zt), new(X(g.XBwBack), zt), new(X(g.XBwBack), zt - ad.LedgeBelowTop), new(X(g.XBwBack - Pj), zt - ad.LedgeBelowTop),
            new(X(g.XBwBack - Pj), zt - ad.LedgeBelowTop - ad.CorbelVertical), new(X(g.XBwBack), zt - ad.LedgeBelowTop - ad.CorbelVertical - ad.CorbelTaperRise), new(X(g.XBwBack), cj),
        }, 0.06);
        if (tall != null)
            dS.CutConcrete(new List<Vec2> { new(X(g.XBwFront), tall.BaseZ), new(X(g.XStepFront), tall.BaseZ), new(X(g.XStepFront), tall.TopZ), new(X(g.XBwFront), tall.TopZ) }, 0.06);
        // piles (hidden), two rows
        foreach (double xr in new[] { -ad.PileBackRowToBearing, ad.PileFrontRowToBearing })
        {
            double top = zb + ad.PileTopAboveFootingBottom;
            dS.Poly(SheetLayers.SteelHidden, new[] { new Vec2(X(xr) - In(6.8), top), new Vec2(X(xr) - In(6.8), zb - Ft(2)) });
            dS.Poly(SheetLayers.SteelHidden, new[] { new Vec2(X(xr) + In(6.8), top), new Vec2(X(xr) + In(6.8), zb - Ft(2)) });
        }
        // bearing centerline
        dS.Line(SheetLayers.Center, new Vec2(X(0), zb - Ft(2.5)), new Vec2(X(0), zt + dS.Paper(0.3)));
        dS.Text("CL BRG", new Vec2(X(0), zt + dS.Paper(0.45)), dS.TextH, 0, TextAlign.Center);
        double yD = zb - dS.Paper(0.5);
        dS.DimChain(new[] { xB, g.XBwBack, g.XBwFront, 0.0, g.XStepFront, xF }.Select(x => new Vec2(X(x), yD)).ToList(), 0);
        dS.Dim(new Vec2(X(xB), zb), new Vec2(X(xF), zb), -0.75, dS.Short(ad.FootingWidth));
        dS.Dim(new Vec2(X(xF), zb), new Vec2(X(xF), zb + D), -0.4, dS.Short(D));
        dS.ElevationTag(new Vec2(X(xF), zb + D + W), $"WASH EL {dS.Elev(zb + D + W)}", right: true);
        dS.ElevationTag(new Vec2(X(g.XBwFront), cj), $"C.J. EL {dS.Elev(cj)}", right: true);
        string tallName = tall == null ? "" : tall.Tag;
        if (tall != null) dS.Text($"{tallName} STEP TOP EL {dS.Elev(tall.TopZ)}", new Vec2(X(g.XStepFront) + dS.Paper(0.15), tall.TopZ), dS.SmallTextH, 0, TextAlign.Left);
        dS.Title("SECTION THROUGH ABUTMENT", $"Normal to the line{(tall != null ? $" at {tallName} (tallest step)" : "")}; span side right · Scale {DrawingOptions.ScaleText(dS.Scale, u)}",
            new Vec2(X((xB + xF) / 2), zb - dS.Paper(2.0)));

        // ================= piling / wing layout plan (S along the line, N across it, true plan shape)
        var dwgP = new PlanDrawing();
        var dP = new Drafter(dwgP, u, ctx.Options.BentScale);
        double ox = 0, oy = 0;
        Vec2 Pl(double s, double n) => new(ox + (s - s0), oy + g.Sigma * n);
        // footing outline as built by the generator (end lines along the alignment offsets, notches along constant stations, corner pieces)
        dP.Poly(SheetLayers.Concrete, g.FootingOutline.Select(p => Pl(p.X, p.Y)), true);
        dP.Poly(SheetLayers.ConcreteHidden, new[] { Pl(g.BwLeftS(g.N(g.XBwBack)), g.N(g.XBwBack)), Pl(g.BwRightS(g.N(g.XBwBack)), g.N(g.XBwBack)), Pl(g.BwRightS(g.N(g.XBwFront)), g.N(g.XBwFront)), Pl(g.BwLeftS(g.N(g.XBwFront)), g.N(g.XBwFront)) }, true);
        foreach (var (ps, pn) in g.PileXY)
        {
            double a = In(7.3), b = In(6.8);
            dP.Poly(SheetLayers.Steel, new[] { Pl(ps - a, pn - b), Pl(ps + a, pn - b), Pl(ps + a, pn + b), Pl(ps - a, pn + b) }, true);
        }
        var stns = g.PileXY.Select(p => p.S).Distinct().OrderBy(x => x).ToList();
        if (stns.Count > 1) dP.DimChain(stns.Select(s => Pl(s, g.N(g.XFootBack)) + new Vec2(0, -dP.Paper(0.35))).ToList(), 0);
        Vec2 ToPlan(Vec2 world) => Pl(Vec2.Dot(world - unit.Origin, unit.D), Vec2.Dot(world - unit.Origin, unit.N));
        foreach (var w in g.Wings)
        {
            var a0 = ctx.Framing.Alignment;
            var pi = new[] { w.Point(a0, w.UBack, w.EIn), w.Point(a0, w.Length, w.EIn), w.Point(a0, w.Length, w.EOut), w.Point(a0, w.UBack, w.EOut) };
            dP.Poly(SheetLayers.Concrete, pi.Select(ToPlan), true);
            foreach (var c in w.Counterforts)
            {
                var R = new Vec2(c.Tangent.Y, -c.Tangent.X);
                Vec2[] Rect(double w, double e0, double e1) => new[] { c.Origin + c.Tangent * (-w / 2) + R * e0, c.Origin + c.Tangent * (w / 2) + R * e0, c.Origin + c.Tangent * (w / 2) + R * e1, c.Origin + c.Tangent * (-w / 2) + R * e1 };
                dP.Poly(SheetLayers.Concrete, Rect(c.BodyWidth > 0 ? c.BodyWidth : c.Width, c.YCf0, c.YCf1).Select(ToPlan), true);
                dP.Poly(SheetLayers.ConcreteHidden, Rect(c.Width, c.YFoot0, c.YFoot1).Select(ToPlan), true);
            }
        }
        foreach (var f in unit.Foundations.Skip(1))
            foreach (var p in f.Piles)
            {
                double a = In(7.3), b = In(6.8);
                {
                    var wv = new Vec2(p.WebS, p.WebN); var fv = new Vec2(wv.Y, -wv.X);        // web (depth) and flange directions
                    dP.Poly(SheetLayers.Steel, new[] { (-a, -b), (a, -b), (a, b), (-a, b) }.Select(q => Pl(p.S + fv.X * q.Item1 + wv.X * q.Item2, p.N + fv.Y * q.Item1 + wv.Y * q.Item2)), true);
                }
                // Locate a single extra (wing) pile from the nearest main pile: along the line and across it (wing pile groups are on the wing layout).
                if (g.PileXY.Count > 0 && unit.Foundations.Skip(1).Sum(x => x.Piles.Count) <= 2)
                {
                    var (ms, mn) = g.PileXY.OrderBy(q => Math.Abs(q.S - p.S)).First();
                    var pa = Pl(ms, mn); var pb = Pl(p.S, mn); var pc = Pl(p.S, p.N);
                    if (Vec2.Distance(pa, pb) > In(1)) dP.Dim(pa, pb, -0.45, dP.Short(Math.Abs(p.S - ms)));
                    if (Vec2.Distance(pb, pc) > In(1)) dP.Dim(pb, pc, 0.35, dP.Short(Math.Abs(p.N - mn)));
                }
            }
        var box3 = dwgP.Extents();
        dP.Title("PILING AND WING LAYOUT", $"{unit.Foundations.Sum(f => f.Piles.Count)} piles {unit.Definition.PileLabel(dP.Short)}; stations along the line, plan at the bottom of footing · Scale {DrawingOptions.ScaleText(dP.Scale, u)}",
            new Vec2(ox + (s1 - s0) / 2, box3.MinY - dP.Paper(0.5)));
        var chosen = part == 1 ? dwgS : part == 2 ? dwgP : dwg;
        string[] names = { "rear elevation", "section through abutment", "piling and wing layout" };
        return new SheetView($"{unit.Name} {names[part]}", unit.Name.Replace(" ", "") + "-abutment-" + part, chosen, d.Scale,
            "as-designed abutment: " + names[part] + ".");
    }

    private static void Merge(PlanDrawing dst, PlanDrawing src, double dx, double dy)
    {
        var o = new Vec3(dx, dy, 0);
        foreach (var l in src.Layers)
        {
            var t = dst.Layer(l.Style);
            foreach (var pl in l.Polylines) t.Polylines.Add(new Polyline3(pl.Points.Select(q => q + o).ToArray(), pl.Closed));
            foreach (var tx in l.Texts) t.Texts.Add(tx with { Position = tx.Position + o });
            foreach (var c in l.Circles) t.Circles.Add(c with { Center = c.Center + o });
        }
    }
}
