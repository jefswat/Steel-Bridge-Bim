using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// Integral abutment (<see cref="AbutmentKind.Integral"/>, Iowa std 2087-BTCD as modified on the Woodbury plans p.15-17): a pile cap with a stepped top (one
/// level step per girder at the printed seat elevation, vertical risers), a sloped / broken bottom (<see cref="AbutmentDefinition.FootingBottom"/>), ends
/// parallel to the alignment, ONE pile row (normally on the CL bearing), an optional wing extension (wall outboard of the gutter line on its own level
/// footing) and CMP pile casings. No backwall / corbel / step blocks: the abutment diaphragm is cast with the deck (concrete-bridge design, feature C).
/// Nothing is guessed: riser positions, casing bottom, wing pile positions, wing footing top and wall-top positions are plan inputs and are reported
/// UNRESOLVED (with the plan page) when missing; the affected solid is then not built.
/// </summary>
public sealed partial class SubstructureGenerator
{
    private const string StepDiagramPage = "p.17 (abutment step diagram)";

    private SubstructureUnit BuildIntegralAbutment(int j, SubstructureUnit unit, SubstructureDefinition sd, SupportLineResult sl, List<Seat> seats)
    {
        var ad = sd.Abutment!;
        var a = _f.Alignment;
        string name = sl.Name;
        double sg = j == 0 ? 1 : -1;                        // span side: +N for the first support, -N for the last
        var R = a.RightNormalAt(unit.Station);
        double dr = Vec2.Dot(unit.D, R), nr = Vec2.Dot(unit.N, R);
        if (Math.Abs(dr) < 0.2) throw new InvalidOperationException("the support line is nearly parallel to the alignment");
        double Offs(double s, double n) => a.Inverse(unit.Plan(s, n)).Offset;
        double SolveS(double e, double n)
        {
            double s = (e - nr * n) / dr;
            for (int i = 0; i < 6; i++) { double f = Offs(s, n) - e; if (Math.Abs(f) < 1e-8) break; s -= f / dr; }
            return s;
        }
        Plane EndPlane(double e) => n => SolveS(e, n);
        Plane ConstPlane(double c) => _ => c;

        if (double.IsNaN(ad.CapBackToBearing) || double.IsNaN(ad.CapFrontToBearing))
            throw new InvalidOperationException($"{name}: the integral abutment needs CapBackToBearing and CapFrontToBearing (pile cap faces from the CL bearing)");
        var fbPts = PolyPts(ad.FootingBottom);
        if (fbPts.Count == 0) throw new InvalidOperationException("the abutment needs a footing-bottom (cap bottom) elevation");
        double FB(double s) => SubstructureUnit.Interp(fbPts, s);
        double xB = -ad.CapBackToBearing, xF = ad.CapFrontToBearing;
        if (xF <= xB) throw new InvalidOperationException($"{name}: the cap front face must be ahead of the back face");
        double Nx(double x) => sg * x;
        double nMid = Nx((xB + xF) / 2), nBack = Nx(xB), nFront = Nx(xF);
        var leftEnd = EndPlane(ad.LeftEndOffset);
        var rightEnd = EndPlane(ad.RightEndOffset);
        if (leftEnd(nMid) >= rightEnd(nMid)) throw new InvalidOperationException("the left end offset must be less than the right end offset");
        double s0Mid = leftEnd(nMid), s1Mid = rightEnd(nMid);

        var ignored = new List<string>();
        if (ad.Masks.Count > 0) ignored.Add("Masks");
        if (ad.Wings.Count > 0) ignored.Add("Wings (use WingExtension)");
        if (ad.Extension != null) ignored.Add("Extension");
        if (ad.FootingNotches.Count > 0) ignored.Add("FootingNotches");
        if (ad.BackwallTop.Count > 0) ignored.Add("BackwallTop");
        if (ad.StandalonePedestals.Count > 0) ignored.Add("StandalonePedestals");
        if (ignored.Count > 0) Warn($"{name}: integral abutment - stub-abutment inputs not used: {string.Join(", ", ignored)}.");

        // ---- steps: one level step per girder at the plan top; checked against the bearing seat
        var gs = seats.Where(x => x.Girder < 1000).OrderBy(x => x.S).ToList();
        var tops = new List<double>();
        bool stepsOk = gs.Count > 0;
        foreach (var seat in gs)
        {
            var sdef = ad.Steps.FirstOrDefault(x => x.Girder == seat.Girder + 1);
            if (sdef == null)
            {
                Warn($"{name}: " + UnresolvedFlag.Text(StepDiagramPage, $"step top elevation for girder G{seat.Girder + 1}", "none was entered for this girder (the step diagram gives one per beam)", "The stepped cap is not built."));
                stepsOk = false; tops.Add(double.NaN); continue;
            }
            tops.Add(sdef.TopElevation);
            double toSeat = double.IsNaN(ad.StepToSeat) ? 0 : ad.StepToSeat;
            double diff = sdef.TopElevation + toSeat - seat.Z;
            if (Math.Abs(diff) > In(0.125))
                Warn($"{name}: step G{seat.Girder + 1} top {El(sdef.TopElevation)}{(toSeat != 0 ? $" + {Sh(toSeat)} to the seat" : "")} differs from the bearing seat {El(seat.Z)} by {Sh(Math.Abs(diff))} - the step follows the plan; check the bearing stack.");
        }
        // riser positions: explicit, or mid-bay only when confirmed (StepRisersAtMidBay); otherwise UNRESOLVED (open question Q3)
        List<double>? risers = null;
        if (gs.Count <= 1) risers = new List<double>();
        else if (ad.StepBoundaryS.Count > 0)
        {
            if (ad.StepBoundaryS.Count != gs.Count - 1)
                Warn($"{name}: " + UnresolvedFlag.Text(StepDiagramPage, "step riser positions", $"{ad.StepBoundaryS.Count} were entered for {gs.Count} girders but {gs.Count - 1} are needed (one between each pair of beams)", "The stepped cap is not built."));
            else
            {
                risers = ad.StepBoundaryS.OrderBy(x => x).ToList();
                for (int k = 0; k < risers.Count; k++)
                    if (risers[k] <= gs[k].S || risers[k] >= gs[k + 1].S)
                        Warn($"{name}: step riser {k + 1} at S {L(risers[k])} is not between girders G{gs[k].Girder + 1} and G{gs[k + 1].Girder + 1} (S {L(gs[k].S)} .. {L(gs[k + 1].S)}); built as given.");
            }
        }
        else if (ad.StepRisersAtMidBay) risers = Enumerable.Range(0, gs.Count - 1).Select(k => (gs[k].S + gs[k + 1].S) / 2).ToList();
        else
            Warn($"{name}: " + UnresolvedFlag.Text(StepDiagramPage, "step riser positions between the girders", "the diagram does not dimension them (mid-bay or a standard distance from the beam CL? open question Q3)", "Set StepBoundaryS or StepRisersAtMidBay. The stepped cap is not built."));
        if (risers != null && risers.FirstOrDefault(r => new[] { nBack, nFront }.Any(n => r <= leftEnd(n) + 1e-4 || r >= rightEnd(n) - 1e-4), double.NaN) is var bad && !double.IsNaN(bad))
        {
            Warn($"{name}: " + UnresolvedFlag.Text(StepDiagramPage, $"step riser at S {L(bad)}", "the entered position lies outside the cap ends, so it cannot be the plan riser", "The stepped cap is not built."));
            risers = null;
        }
        Func<double, double>? stepTop = null;
        if (stepsOk && risers != null)
        {
            var rs = risers; var ts = tops;
            stepTop = s => { int k = 0; while (k < rs.Count && s > rs[k] + 1e-9) k++; return ts[k]; };
        }

        // ---- end depths (end steps are known even when the risers are not)
        double depthL = tops.Count > 0 ? tops[0] - FB(s0Mid) : double.NaN, depthR = tops.Count > 0 ? tops[^1] - FB(s1Mid) : double.NaN;
        void CheckDepth(string side, double model, double plan)
        {
            if (double.IsNaN(model)) return;
            Note($"{name}: integral cap depth at the {side} end (step top to bottom, cap centreline) {Sh(model)}{(double.IsNaN(plan) ? "" : $", plan {Sh(plan)}")}.");
            if (!double.IsNaN(plan) && Math.Abs(model - plan) > In(0.125))
                Warn($"{name}: integral cap depth at the {side} end is {Sh(model)} from the step top and the bottom elevation but the step diagram prints {Sh(plan)} ({StepDiagramPage}).");
        }
        CheckDepth("left", depthL, ad.PlanCapDepthLeft);
        CheckDepth("right", depthR, ad.PlanCapDepthRight);

        Func<double, double> viewTop = stepTop ?? (gs.Count > 0 && stepsOk
            ? (s => SubstructureUnit.Interp(gs.Select((g, i) => new Vec2(g.S, tops[i])).ToList(), s))   // view placeholder only: no solid is built
            : (s => FB(s) + In(1)));
        var geo = new AbutmentGeometry
        {
            Def = ad, Sigma = sg, XFootBack = xB, XBwBack = xB, XBwFront = xF, XStepFront = xF, XFootFront = xF,
            FootingBottom = FB, BackwallTop = viewTop, LeftEndS = n => leftEnd(n), RightEndS = n => rightEnd(n),
            BwLeftS = n => leftEnd(n), BwRightS = n => rightEnd(n), JointDepth = 0,
            StepTop = stepTop, DepthAtLeftEnd = depthL, DepthAtRightEnd = depthR,
        };
        if (stepTop != null) geo.StepRisersS.AddRange(risers!);
        unit.Abutment = geo;
        {
            const int sub = 8;
            for (int k = 0; k <= sub; k++) { double n = Nx(xB + (xF - xB) * k / sub); geo.FootingOutline.Add(new Vec2(rightEnd(n), n)); }
            for (int k = sub; k >= 0; k--) { double n = Nx(xB + (xF - xB) * k / sub); geo.FootingOutline.Add(new Vec2(leftEnd(n), n)); }
        }
        unit.CapS0 = s0Mid; unit.CapS1 = s1Mid;
        unit.CapN0 = Math.Min(nBack, nFront); unit.CapN1 = Math.Max(nBack, nFront); unit.CapMidN = nMid;
        var fbBreaks = fbPts.Select(p => p.X).Where(x => x > s0Mid && x < s1Mid).Distinct().OrderBy(x => x).ToList();
        foreach (double s in new[] { s0Mid }.Concat(fbBreaks).Append(s1Mid)) unit.CapBottom.Add(new Vec2(s, FB(s)));
        var topLine = new List<Vec2> { new(s0Mid, viewTop(s0Mid)) };
        if (stepTop != null)
            for (int k = 0; k < risers!.Count; k++) { topLine.Add(new Vec2(risers[k], tops[k])); topLine.Add(new Vec2(risers[k], tops[k + 1])); }
        else foreach (var g in gs) topLine.Add(new Vec2(g.S, viewTop(g.S)));
        topLine.Add(new Vec2(s1Mid, viewTop(s1Mid)));
        unit.CapTop.AddRange(topLine);
        unit.CapTopDescription = stepTop != null ? "integral abutment pile cap (stepped top)" : "integral abutment pile cap (step risers UNRESOLVED - line through the step tops)";
        unit.Backwall = (unit.CapN0, unit.CapN1, topLine);

        // ---- pile cap: one slab per step between the end planes and the risers (every face planar)
        if (stepTop != null)
        {
            var mesh = new Mesh();
            var planes = new List<Plane> { leftEnd };
            planes.AddRange(risers!.Select(ConstPlane));
            planes.Add(rightEnd);
            for (int k = 0; k < tops.Count; k++)
            {
                double t = tops[k];
                double sA = planes[k](nMid), sB = planes[k + 1](nMid);
                double fbMax = new[] { sA, sB }.Concat(fbBreaks.Where(x => x > sA && x < sB)).Max(FB);
                if (t <= fbMax + 1e-4) Warn($"{name}: step {k + 1} top {El(t)} is not above the cap bottom ({El(fbMax)}).");
                AddSlab(mesh, unit, new[]
                {
                    new SV(nBack, s => FB(s)), new SV(nFront, s => FB(s)), new SV(nFront, _ => t), new SV(nBack, _ => t),
                }, planes[k], planes[k + 1], fbBreaks);
            }
            double vol = Math.Abs(mesh.Volume());
            const double cy = 0.764554857984;
            unit.Solids.Add(new SubSolid(SubElementKind.Footing, $"{name} pile cap",
                $"Integral abutment pile cap {L(s1Mid - s0Mid)} along the line, {Sh(xF - xB)} wide, {tops.Count} level steps (top {El(tops.Min())} .. {El(tops.Max())}), depth {Sh(depthL)} / {Sh(depthR)} at the ends, ends parallel to the alignment, {vol / cy:0.0} cy", mesh));
            if (!double.IsNaN(ad.PlanCapVolume) && Math.Abs(vol - ad.PlanCapVolume) > 0.02 * ad.PlanCapVolume)
                Warn($"{name}: integral pile cap concrete {vol / cy:0.0} cy (model) vs {ad.PlanCapVolume / cy:0.0} cy (plan).");
        }

        // ---- piles (one or more rows from PileRowOffsets) and their casing positions
        var casingAt = new List<(double s, double n, double top)>();
        var main = new FoundationInfo { Type = FoundationType.PileCap, S = (s0Mid + s1Mid) / 2, N = nMid, TopZ = viewTop((s0Mid + s1Mid) / 2), BottomZ = FB((s0Mid + s1Mid) / 2), SizeS = s1Mid - s0Mid, SizeN = xF - xB };
        unit.Foundations.Add(main);
        geo.PileStationsS = ad.PileStations.ToArray();
        if (ad.PileRowOffsets.Count == 0 && ad.PileStations.Count > 0)
            Warn($"{name}: " + UnresolvedFlag.Text("p.15 pile layout", "integral abutment pile line (PileRowOffsets)", "it was not entered, so the model does not know where the pile line is across the cap (e.g. [0] = on the CL bearing)", "No cap piles built."));
        else
            foreach (double ps in ad.PileStations.OrderBy(x => x))
                foreach (double xr in ad.PileRowOffsets)
                {
                    double pn = Nx(xr);
                    if (xr < xB + In(6) || xr > xF - In(6)) Warn($"{name}: pile row x {Sh(xr)} is within 6\" of (or beyond) a cap face.");
                    if (ps < leftEnd(pn) + In(6) || ps > rightEnd(pn) - In(6))
                        Warn($"{name}: pile at S {L(ps)}, x {Sh(xr)} is within 6\" of (or beyond) the cap end line.");
                    double head = FB(ps) + ad.PileTopAboveFootingBottom;
                    if (stepTop != null && head > stepTop(ps) - In(3))
                        Warn($"{name}: pile at S {L(ps)}: head {El(head)} is less than 3\" below the cap top {El(stepTop(ps))}.");
                    geo.PileXY.Add((ps, pn));
                    AddPilesAt(unit, sd, main, 0, 0, FB(ps), new List<FootingPoint> { new(ps, pn) }, ad.PileLength, 0, $"{name} pile group 1", embedment: ad.PileTopAboveFootingBottom);
                    casingAt.Add((ps, pn, FB(ps)));
                }

        // ---- wing extension
        if (ad.WingExtension is { } we) BuildWingExtension(unit, sd, ad, we, a, sg, name, nBack, SolveS, casingAt);

        // ---- CMP casings
        if (ad.PileCasing is { } pc) BuildPileCasings(unit, geo, pc, name, casingAt);

        Note($"{name}: integral abutment ({ad.Source}), cap {L(s1Mid - s0Mid)} along the line, {gs.Count} steps, {casingAt.Count} piles{(stepTop == null ? " - stepped cap NOT built (UNRESOLVED)" : "")}.");
        return unit;
    }

    /// <summary>Station where the offset line e crosses the plane N = nFace (bisection near sta0).</summary>
    private static double CrossStation(Alignment a, SubstructureUnit unit, double sta0, double dirSta, double e, double nFace)
    {
        double lo = sta0 - dirSta * 3, hi = sta0 + dirSta * 12;
        double g(double s) => Vec2.Dot(a.PointAt(s, e) - unit.Origin, unit.N) - nFace;
        double glo = g(lo);
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2, gm = g(mid);
            if (Math.Sign(gm) == Math.Sign(glo)) { lo = mid; glo = gm; } else hi = mid;
        }
        return (lo + hi) / 2;
    }

    /// <summary>Iowa integral wing extension: wall outboard of the gutter line and its level footing, rearward from the cap back face, with its piles.</summary>
    private void BuildWingExtension(SubstructureUnit unit, SubstructureDefinition sd, AbutmentDefinition ad, WingExtensionDefinition we, Alignment a, double sg, string name,
        double nBack, Func<double, double, double> solveS, List<(double s, double n, double top)> casingAt)
    {
        string wn = $"wing extension {(we.End == AbutmentEnd.Left ? "left" : "right")}";
        var missing = new List<string>();
        if (double.IsNaN(we.Length) || we.Length <= 0) missing.Add("Length");
        if (double.IsNaN(we.WallThickness) || we.WallThickness <= 0) missing.Add("WallThickness");
        if (missing.Count > 0) { Warn($"{name}: " + UnresolvedFlag.Text("p.15 A-A / p.17", $"{wn}: {string.Join(", ", missing)}", "not dimensioned on these sheets (or not entered)", "Not built.")); return; }
        double side = we.GutterOffset < 0 ? -1 : 1;                    // outboard direction in offset
        double eIn = we.GutterOffset, eOut = eIn + side * we.WallThickness;
        double dirSta = -sg;                                            // rearward = away from the span
        double sta0 = a.Inverse(unit.Plan(solveS(eIn, 0), 0)).Station;
        var path = new OffsetPath(a, sta0, dirSta, eIn, we.Length + Ft(20));
        double staBackIn = CrossStation(a, unit, sta0, dirSta, eIn, nBack);
        double ub = path.U(staBackIn), uEnd = ub + we.Length, staEnd = path.Sta(uEnd);

        // loft between two offset lines from the cap back face to the perpendicular end; bottom / top as functions of U from the back face
        Mesh Body(double e0, double e1, Func<double, double> bot, Func<double, double> top)
        {
            double sb0 = CrossStation(a, unit, sta0, dirSta, e0, nBack), sb1 = CrossStation(a, unit, sta0, dirSta, e1, nBack);
            Vec3[] Ring(double st0, double st1)
            {
                var p0 = a.PointAt(st0, e0); var p1 = a.PointAt(st1, e1);
                double u0 = path.U(st0) - ub, u1 = path.U(st1) - ub;
                return new[] { p0.WithZ(bot(u0)), p1.WithZ(bot(u1)), p1.WithZ(top(u1)), p0.WithZ(top(u0)) };
            }
            var rings = new List<Vec3[]> { Ring(sb0, sb1) };
            bool straight = Math.Abs(a.AngleAt(staBackIn) - a.AngleAt(staEnd)) < 2e-5;
            int n = straight ? 1 : Math.Max(1, (int)Math.Ceiling(we.Length / 1.0));
            for (int k = 1; k <= n; k++) { double st = path.Sta(ub + we.Length * k / n); rings.Add(Ring(st, st)); }
            var sec = new[] { new Vec2(0, 0), new Vec2(Math.Abs(e1 - e0), 0), new Vec2(Math.Abs(e1 - e0), 1), new Vec2(0, 1) };
            var m = new Mesh();
            MeshBuilder.AddLoft(m, sec, rings);
            return m;
        }

        bool topOk = !double.IsNaN(we.FootingTopElevation), botOk = !double.IsNaN(we.FootingBottomElevation), insideOk = !double.IsNaN(we.FootingInside);
        if (!topOk) Warn($"{name}: " + UnresolvedFlag.Text("p.15 A-A / p.20 A-A", $"{wn}: footing top (= wall bottom) elevation", "no elevation is printed for it (or none was entered)", "Footing and wall not built."));
        else if (!botOk || !insideOk) Warn($"{name}: " + UnresolvedFlag.Text("p.15 A-A / p.17", $"{wn}: footing {(!botOk ? "bottom elevation" : "width inside the gutter line")}", "not dimensioned on these sheets (or not entered)", "Footing not built."));
        if (topOk && botOk && insideOk)
        {
            if (we.FootingTopElevation <= we.FootingBottomElevation) Warn($"{name}: {wn}: footing top {El(we.FootingTopElevation)} is not above the bottom {El(we.FootingBottomElevation)}; footing not built.");
            else
            {
                double eFi = eIn - side * we.FootingInside;
                var fm = Body(eFi, eOut, _ => we.FootingBottomElevation, _ => we.FootingTopElevation);
                unit.Solids.Add(new SubSolid(SubElementKind.WingFooting, $"{name} {wn} footing",
                    $"Wing extension footing {Sh(we.FootingInside + we.WallThickness)} across ({Sh(we.FootingInside)} inside the gutter line + the wall) x {Sh(we.Length)} along the gutter line from the cap back face, level bottom {El(we.FootingBottomElevation)}, top {El(we.FootingTopElevation)}", fm));
            }
        }
        if (topOk)
        {
            if (we.WallTop.Count == 0) Warn($"{name}: " + UnresolvedFlag.Text("p.20", $"{wn}: wall top elevations / positions A / B / C", "none were entered (the points are tagged on p.20 but their values are not all printed)", "Wall not built."));
            else
            {
                var wt = we.WallTop.OrderBy(p => p.U).Select(p => new Vec2(p.U, p.Elevation)).ToList();
                double Top(double u) => SubstructureUnit.Interp(wt, u);
                double uFirst = Math.Min(0, path.U(CrossStation(a, unit, sta0, dirSta, eOut, nBack)) - ub);
                if (wt[0].X > uFirst + In(0.5) || wt[^1].X < we.Length - In(0.5))
                    Warn($"{name}: {wn}: wall top points cover U {L(wt[0].X)} .. {L(wt[^1].X)} but the wall runs {L(uFirst)} .. {L(we.Length)} from the cap back face (skewed back face included); the top is extended linearly beyond the given points.");
                var wm = Body(eIn, eOut, _ => we.FootingTopElevation, Top);
                unit.Solids.Add(new SubSolid(SubElementKind.Wingwall, $"{name} {wn} wall",
                    $"Wing extension wall {Sh(we.WallThickness)} thick outboard of the gutter line, {Sh(we.Length)} from the cap back face, top {El(wt.Min(p => p.Y))} .. {El(wt.Max(p => p.Y))}, bottom {El(we.FootingTopElevation)}", wm));
            }
        }

        // piles
        int placedPiles = 0;
        if (we.Piles.Count > 0 && botOk)
        {
            if (double.IsNaN(we.PileTopAboveFootingBottom)) Warn($"{name}: " + UnresolvedFlag.Text("p.15 A-A / p.17", $"{wn}: pile head embedment in the extension footing", "not dimensioned for the extension footing (or not entered)", "Wing extension piles not built."));
            else
            {
                double sm = path.Sta(ub + we.Length / 2);
                var pm = a.PointAt(sm, eIn);
                var foot = new FoundationInfo
                {
                    Type = FoundationType.PileCap, S = Vec2.Dot(pm - unit.Origin, unit.D), N = Vec2.Dot(pm - unit.Origin, unit.N),
                    TopZ = topOk ? we.FootingTopElevation : we.FootingBottomElevation, BottomZ = we.FootingBottomElevation,
                    SizeS = we.Length, SizeN = (insideOk ? we.FootingInside : 0) + we.WallThickness,
                };
                unit.Foundations.Add(foot);
                foreach (var p in we.Piles)
                {
                    if (double.IsNaN(p.Along) || double.IsNaN(p.Across)) continue;
                    var pp = a.PointAt(path.Sta(ub + p.Along), eIn + side * p.Across);
                    double ps = Vec2.Dot(pp - unit.Origin, unit.D), pn = Vec2.Dot(pp - unit.Origin, unit.N);
                    AddPilesAt(unit, sd, foot, 0, 0, we.FootingBottomElevation, new List<FootingPoint> { new(ps, pn) }, ad.PileLength, 0, $"{name} {wn}", embedment: we.PileTopAboveFootingBottom);
                    casingAt.Add((ps, pn, we.FootingBottomElevation));
                    placedPiles++;
                }
            }
        }
        else if (we.Piles.Count > 0) Warn($"{name}: " + UnresolvedFlag.Text("p.15 A-A / p.17", $"{wn}: extension piles", "their head level depends on the footing bottom elevation, which is not given", "Not built."));
        int positioned = we.Piles.Count(p => !double.IsNaN(p.Along) && !double.IsNaN(p.Across));
        if (we.PlanPileCount > positioned)
            Warn($"{name}: " + UnresolvedFlag.Text("p.17, WA-1", $"{wn}: position of {we.PlanPileCount - positioned} of {we.PlanPileCount} plan pile(s)", "the sheet counts them but does not locate them along / across the extension", "Not built."));
        _ = placedPiles;
    }

    /// <summary>Pile-head spiral (schedule only): one per abutment pile; developed length = turns x the helix turn length at the PRINTED diameter.</summary>
    private void IntegralPileSpiral(SubstructureUnit unit, AbutmentDefinition ad, List<SBM.Core.Concrete.BarMark> marks, bool epoxy)
    {
        if (ad.PileSpiral is not { } sp) return;
        string name = unit.Name;
        var size = SBM.Core.Concrete.RebarCatalog.Sizes.FirstOrDefault(z => z.Name.Equals(sp.BarSize.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? (sp.BarSize.Trim() == "#2" ? new SBM.Core.Concrete.RebarSize("#2", In(0.25), 0.05 * 0.00064516, 0.167 * 1.48816394) : null);
        if (size == null || double.IsNaN(sp.Diameter) || double.IsNaN(sp.Pitch) || sp.Turns <= 0)
        {
            Warn($"{name}: " + UnresolvedFlag.Text("p.15 note", "pile spiral bar size / diameter / pitch / turns", "not all of them were entered from the note", "Not scheduled."));
            return;
        }
        int piles = unit.Foundations.Sum(f => f.Piles.Count);
        double turn = Math.Sqrt(Math.Pow(Math.PI * sp.Diameter, 2) + sp.Pitch * sp.Pitch);
        double r = sp.Diameter / 2;
        var circle = Enumerable.Range(0, 25).Select(i => Vec2.FromAngle(2 * Math.PI * i / 24) * r).ToArray();
        var shape = new SBM.Core.Concrete.BarShape
        {
            Code = "SPIRAL", Description = $"Spiral {sp.Turns} turns, {Sh(sp.Diameter)} dia (as printed), {Sh(sp.Pitch)} pitch", Centerline = circle, FixedLength = sp.Turns * turn,
        };
        marks.Add(new SBM.Core.Concrete.BarMark
        {
            Mark = string.IsNullOrWhiteSpace(sp.Mark) ? "pile spiral" : sp.Mark, Size = size, Shape = shape, Location = "pile head spiral (schedule only)", Unit = name,
            Epoxy = epoxy, Count = piles,
        });
    }

    /// <summary>CMP casings around the abutment piles from the bottom of the concrete down to the casing bottom (UNRESOLVED when NaN: schedule records only).</summary>
    private void BuildPileCasings(SubstructureUnit unit, AbutmentGeometry geo, PileCasingDefinition pc, string name, List<(double s, double n, double top)> at)
    {
        if (double.IsNaN(pc.Diameter) || pc.Diameter <= 0) { Warn($"{name}: " + UnresolvedFlag.Text("p.17", "pile casing diameter", "not given on the sheet (or not entered)", "Casings not modeled.")); return; }
        string fill = string.IsNullOrWhiteSpace(pc.Fill) ? "" : $", filled with {pc.Fill}";
        foreach (var (s, n, top) in at) geo.Casings.Add(new PileCasingInfo(s, n, top, pc.BottomElevation, pc.Diameter));
        double planNote = pc.PlanTotalLength;
        if (double.IsNaN(pc.BottomElevation))
        {
            Warn($"{name}: " + UnresolvedFlag.Text("p.17", "CMP pile casing bottom", "it is the bottom of the EPS fill, which is in the road plans, not this set", $"{at.Count} casing(s) of {Sh(pc.Diameter)} scheduled with unknown length, no solid."));
            if (planNote > 0)
                Note($"{name}: pile casings - plan quantity {L(planNote)} for the bridge (schedule value; not used for the geometry).");
            return;
        }
        double total = 0;
        foreach (var (s, n, top) in at)
        {
            if (pc.BottomElevation >= top - 1e-4) { Warn($"{name}: pile casing at S {L(s)}: bottom {El(pc.BottomElevation)} is not below the concrete bottom {El(top)}; not built."); continue; }
            var m = new Mesh();
            MeshBuilder.AddPrism(m, Frame(unit, s, n), pc.Diameter / 2, 24, pc.BottomElevation, top);
            unit.Solids.Add(new SubSolid(SubElementKind.PileCasing, $"{name} pile casing S {L(s)}",
                $"{Sh(pc.Diameter)} CMP pile casing{fill}, {El(top)} to {El(pc.BottomElevation)} ({L(top - pc.BottomElevation)}), envelope incl. the fill", m, IsConcrete: false));
            total += top - pc.BottomElevation;
        }
        Note($"{name}: pile casings {L(total)} total for {at.Count} piles{(planNote > 0 ? $" (plan quantity for the bridge {L(planNote)})" : "")}.");
    }
}
