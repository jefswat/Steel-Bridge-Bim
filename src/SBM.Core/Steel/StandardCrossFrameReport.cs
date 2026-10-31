using System.Collections.Concurrent;
using System.Globalization;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;

namespace SBM.Core.Steel;

/// <summary>
/// OPTIONAL standard cross frames and the twist report (<see cref="CrossFrameDefinition.Standard"/>, <see cref="CrossFrameDefinition.ReportTwist"/>):
/// which standard applies to a frame, one report row per frame (connections phase, parallel) and the bridge summary (families, depth solver, misfits,
/// twist) with its diagnostics. Nothing here changes geometry; nothing is chosen for the user.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private readonly ConcurrentBag<CrossFrameFitRow> _cfFit = new();
    /// <summary>Stiffener part marks by framing stiffener index (set in Run; for the report's plate column).</summary>
    private string[]? _stiffMarks;

    /// <summary>Standard of a frame: its style's own, else the definition's when the style is listed (or the list is empty); then the scope / supports.</summary>
    private StandardCrossFrameDefinition? StandardFor(CrossFrameResult cf, CrossFrameStyle? st) =>
        StandardCrossFrames.For(_def.CrossFrames, st, cf.Style, cf.AtSupport, cf.AtSupport && cf.SupportIndex >= 0 ? _def.SupportAt(cf.SupportIndex).Name : "");

    /// <summary>True flange normal: level across the girder, sloped with the grade along it.</summary>
    private static Vec3 TrueFlangeNormal(GirderPath path, double s)
    {
        var (_, t, _) = path.At(s);
        var t3 = new Vec3(t.X, t.Y, path.GradeAt(s)).Normalized();
        return (Up - t3 * Vec3.Dot(Up, t3)).Normalized();
    }

    /// <param name="built">the frame's (2D) plane; <paramref name="builtL"/> / <paramref name="builtR"/> = the plane each end was built in (= built,
    /// or the connection plates' planes of a warped frame).</param>
    private CrossFrameFitRow FitRow(CrossFrameResult cf, CrossFrameStyle? st, StandardCrossFrameDefinition? std, bool applied, FramePlaneGeom built,
        FramePlaneGeom builtL, FramePlaneGeom builtR, StandardFramePlane mode,
        Vec3 pl, PlateEndGeometry eL, PlateEndGeometry eR, double refL, double refR, bool defRight, double xbl, double xbr, double tp,
        List<Vec2> bL, List<Vec2> bR)
    {
        var u = built.U;
        var eDef = defRight ? eR : eL;
        var avg = StandardCrossFrames.Averaged(pl, u, eL, eR);
        var fit = StandardCrossFrames.Fitted(pl, u, eDef);
        var mid = StandardCrossFrames.Midway(pl, u, eL, eR);
        List<Vec2> To(List<Vec2> bs, FramePlaneGeom to) => bs.Select(b => StandardCrossFrames.Carry(b, built, to, eDef.Origin)).ToList();
        var gl = _f.Girders[cf.Bay];
        var gr = _f.Girders[cf.RightGirder];

        double topWp = std == null || double.IsNaN(std.TopWorkPoint) ? 0 : std.TopWorkPoint;
        double tc = std == null || double.IsNaN(std.TopClearance) ? 0 : std.TopClearance;
        double bc = std == null || double.IsNaN(std.BottomClearance) ? 0 : std.BottomClearance;
        bool topOfSteel = std?.Reference == StandardFrameReference.TopOfSteel;
        double zT = (defRight ? refR : refL) - topWp;

        double cTL = double.NaN, cTR = double.NaN, cBL = double.NaN, cBR = double.NaN, maxD = double.NaN, shortfall = double.NaN;
        double sTL = double.NaN, sTR = double.NaN, sBL = double.NaN, sBR = double.NaN;
        string maxEnd = "";
        if (std != null)
        {
            maxD = double.PositiveInfinity; shortfall = double.NegativeInfinity;
            foreach (bool right in new[] { false, true })
            {
                var e = right ? eR : eL;
                var bp = right ? builtR : builtL;
                double x = right ? xbr : xbl;
                var top = StandardCrossFrames.PlateCoords(StandardCrossFrames.HoleCentre(bp, new Vec2(x, zT), e), e, u);
                double yT1 = StandardCrossFrames.PlateCoords(StandardCrossFrames.HoleCentre(bp, new Vec2(x, zT - 1), e), e, u).Y;
                double k = top.Y - yT1;   // plate y per meter of frame depth (~1)
                double yTop = StandardCrossFrames.TopFlangeY(e, u, top.X), yBot = StandardCrossFrames.BottomFlangeY(e, u, top.X);
                double cT = yTop - top.Y;
                double dk = (top.Y - yBot - bc) / k;
                if (dk < maxD) { maxD = dk; maxEnd = right ? "R" : "L"; }
                shortfall = Math.Max(shortfall, tc - cT);
                double refY = topOfSteel ? e.TopFlangeThickness : 0;
                double cB = double.NaN, sT = double.NaN, sB = double.NaN;
                if (applied)
                {
                    double yB = StandardCrossFrames.PlateCoords(StandardCrossFrames.HoleCentre(bp, new Vec2(x, zT - std.Depth), e), e, u).Y;
                    cB = yB - yBot;
                    sT = top.Y - refY + std.TopWorkPoint;
                    sB = yB - refY + std.TopWorkPoint + std.Depth;
                }
                if (right) { cTR = cT; cBR = cB; sTR = sT; sBR = sB; } else { cTL = cT; cBL = cB; sTL = sT; sBL = sB; }
            }
        }

        return new CrossFrameFitRow
        {
            Mark = cf.Mark, Standard = std?.DisplayName ?? "", Style = cf.Style, Type = ChoiceName(cf.Type), AtSupport = cf.AtSupport, Span = cf.Span,
            LeftGirder = cf.Bay, RightGirder = cf.RightGirder, Station = cf.Station, LeftS = cf.LeftGlobalDistance, RightS = cf.RightGlobalDistance,
            LeftStation = gl.Path.At(cf.LeftGlobalDistance).Item3, RightStation = gr.Path.At(cf.RightGlobalDistance).Item3,
            Spacing = xbr - xbl, DefinedEnd = defRight ? "R" : "L", ReferenceDifference = refR - refL, Applied = applied,
            BuiltPlane = mode switch { StandardFramePlane.DefinedEnd => "defined end", StandardFramePlane.Midway => "midway", StandardFramePlane.Warped => "warped", _ => "averaged" },
            LeftShift = cf.LeftShift, RightShift = cf.RightShift, ReferenceEnd = cf.PlatesOnFramePlane ? (cf.ReferenceRight ? "R" : "L") : "",
            TopWorkPoint = std?.TopWorkPoint ?? double.NaN, Depth = std?.Depth ?? double.NaN,
            ClearTopL = cTL, ClearTopR = cTR, ClearBottomL = cBL, ClearBottomR = cBR, MaxDepth = maxD, MaxDepthEnd = maxEnd,
            Headroom = applied ? maxD - std!.Depth : double.NaN, TopShortfall = shortfall,
            ShiftTopL = sTL, ShiftTopR = sTR, ShiftBottomL = sBL, ShiftBottomR = sBR,
            TwistDegrees = StandardCrossFrames.TwistDegrees(eL.Up, eR.Up, u),
            OffsetL = builtL.LocalV(eL.Origin), OffsetR = builtR.LocalV(eR.Origin),
            GapBuiltL = StandardCrossFrames.GapRange(builtL, bL, eL, tp), GapBuiltR = StandardCrossFrames.GapRange(builtR, bR, eR, tp),
            GapAveragedL = StandardCrossFrames.GapRange(avg, To(bL, avg), eL, tp), GapAveragedR = StandardCrossFrames.GapRange(avg, To(bR, avg), eR, tp),
            GapMidwayL = StandardCrossFrames.GapRange(mid, To(bL, mid), eL, tp), GapMidwayR = StandardCrossFrames.GapRange(mid, To(bR, mid), eR, tp),
            GapFittedL = StandardCrossFrames.GapRange(fit, To(bL, fit), eL, tp), GapFittedR = StandardCrossFrames.GapRange(fit, To(bR, fit), eR, tp),
            BoltsL = bL.Count, BoltsR = bR.Count, SpacingRounding = std?.SpacingRounding ?? double.NaN,
        };
    }

    /// <summary>Plate part mark of the stiffener at (girder, s) of the given kind ("" when none was built there).</summary>
    private string PlateMarkAt(int girder, double s, bool bearing)
    {
        if (_stiffMarks == null) return "";
        var kind = bearing ? StiffenerKind.Bearing : StiffenerKind.ConnectionPlate;
        string best = ""; double bd = 0.02;
        for (int i = 0; i < _f.Stiffeners.Count; i++)
        {
            var x = _f.Stiffeners[i];
            if (x.Girder != girder || x.Kind != kind || x.Jacking) continue;
            double dd = Math.Abs(x.GirderDistance - s);
            if (dd < bd && _stiffMarks[i] is { Length: > 0 } m) { bd = dd; best = m; }
        }
        return best;
    }

    /// <summary>Families closer than this (1/16") are pointed out when no SpacingRounding is given.</summary>
    private const double UnitsFine = 0.0254 / 16;

    private string Dec(double m) => double.IsNaN(m) ? "-" : _u.IsUS ? (m / 0.0254).ToString("0.000", CultureInfo.InvariantCulture) + "\"" : (m * 1000).ToString("0.0", CultureInfo.InvariantCulture) + " mm";

    /// <summary>Bridge summary of the fit / twist rows: families, depth solver per standard, misfits, twist; diagnostics in a fixed order.</summary>
    private void FinishCrossFrameFit()
    {
        var cfd = _def.CrossFrames;
        var standards = new List<StandardCrossFrameDefinition>();
        if (cfd.Standard != null) standards.Add(cfd.Standard);
        foreach (var st in cfd.Styles) if (st.Standard != null) standards.Add(st.Standard);
        if (standards.Count == 0 && !cfd.ReportTwist) return;
        var rep = new CrossFrameFitReport();
        rep.Rows.AddRange(_cfFit.OrderBy(r => r.Mark, MarkComparer.Instance));
        foreach (var r in rep.Rows)
        {
            r.LeftPlate = PlateMarkAt(r.LeftGirder, r.LeftS, r.AtSupport);
            r.RightPlate = PlateMarkAt(r.RightGirder, r.RightS, r.AtSupport);
        }
        _model.CrossFrameFit = rep;
        var diag = new List<Diagnostic>();
        void Info(string m) => diag.Add(new Diagnostic(DiagnosticSeverity.Info, m));
        void Warning(string m) => diag.Add(new Diagnostic(DiagnosticSeverity.Warning, m));

        // Families: one shop frame per standard / style / type and work point spacing (same bay condition).
        int fn = 1;
        foreach (var grp in rep.Rows.Where(r => r.Standard != "")
                     .GroupBy(r => (r.Standard, r.Style, r.Type, r.AtSupport, StandardCrossFrames.SpacingKey(r.Spacing, r.SpacingRounding)))
                     .OrderBy(g => g.First().Mark, MarkComparer.Instance))
        {
            string id = $"F{fn++}";
            foreach (var r in grp) r.Family = id;
            var f = grp.First();
            rep.Families.Add(new CrossFrameFamily(id, f.Standard, f.Style, f.Type, f.AtSupport, f.Spacing, f.TopWorkPoint, f.Depth, grp.Select(r => r.Mark).ToList()));
        }

        var seen = new HashSet<string>();
        foreach (var std in standards)
        {
            if (!seen.Add(std.DisplayName)) { Warning($"Standard cross frame '{std.DisplayName}' is defined more than once; give each standard its own Name."); continue; }
            var rows = rep.Rows.Where(r => r.Standard == std.DisplayName).ToList();
            string src = std.Source != "" ? $" ({std.Source})" : "";
            string what = $"standard cross frame '{std.DisplayName}'";
            if (double.IsNaN(std.TopWorkPoint))
                Warning(UnresolvedFlag.Text(std.Source, $"{what} top work point", "not given (engineering input, no default)",
                    "Standard not applied: frames built as without it; the depth report measures from the reference (top WP 0)."));
            if (double.IsNaN(std.Depth))
                Warning(UnresolvedFlag.Text(std.Source, $"{what} depth", "not given (engineering input, no default)",
                    "Standard not applied: frames built as without it; the report gives the deepest common depth."));
            if (double.IsNaN(std.TopClearance) || double.IsNaN(std.BottomClearance))
                Warning(UnresolvedFlag.Text(std.Source, $"{what} {(double.IsNaN(std.TopClearance) && double.IsNaN(std.BottomClearance) ? "top and bottom clearances" : double.IsNaN(std.TopClearance) ? "top clearance" : "bottom clearance")} to the work point",
                    "not given (engineering input, no default)", "The fit check uses 0 (work point on the flange face)."));
            foreach (var note in std.Unresolved) Warning(UnresolvedFlag.Text(std.Source, what, note));
            if (rows.Count == 0) { Warning($"Standard cross frame '{std.DisplayName}'{src}: no frame is in its scope ({std.Scope}{(std.Styles.Count > 0 ? ", styles " + string.Join(", ", std.Styles.Select(s => s == "" ? "(default)" : s)) : "")})."); continue; }

            var gov = rows.MinBy(r => r.MaxDepth)!;
            double maxShort = rows.Max(r => r.TopShortfall);
            double topUsed = double.IsNaN(std.TopWorkPoint) ? 0 : std.TopWorkPoint;
            var sum = new StandardFrameSummary
            {
                Name = std.DisplayName, TopWorkPoint = std.TopWorkPoint, Depth = std.Depth, TopClearance = std.TopClearance, BottomClearance = std.BottomClearance,
                Applied = !double.IsNaN(std.TopWorkPoint) && !double.IsNaN(std.Depth), Frames = rows.Count,
                MaxCommonDepth = gov.MaxDepth, GoverningMark = gov.Mark, GoverningEnd = gov.MaxDepthEnd,
                GoverningGirder = gov.MaxDepthEnd == "R" ? gov.RightGirder : gov.LeftGirder, GoverningSpan = gov.Span,
                GoverningStation = gov.MaxDepthEnd == "R" ? gov.RightStation : gov.LeftStation, GoverningReferenceDifference = gov.ReferenceDifference,
                MinTopWorkPoint = topUsed + maxShort, MaxDepthAtMinTopWorkPoint = gov.MaxDepth - maxShort,
            };
            foreach (var r in rows.Where(r => r.Headroom < -1e-6).OrderBy(r => r.Headroom)) sum.Misfits.Add(r.Mark);
            foreach (var r in rows.Where(r => r.TopShortfall > 1e-6).OrderByDescending(r => r.TopShortfall)) sum.TopMisfits.Add(r.Mark);
            rep.Standards.Add(sum);

            var fams = rep.Families.Where(f => f.Standard == std.DisplayName).ToList();
            string refText = std.Reference == StandardFrameReference.TopOfSteel ? "top of steel" : "top of web";
            string endText = std.DefinedEnd switch { StandardDefinedEnd.Left => "left (lower numbered) girder", StandardDefinedEnd.Right => "right girder", _ => "taller end" };
            Info($"Standard cross frame '{std.DisplayName}'{src}: {rows.Count} frames in {fams.Count} famil{(fams.Count == 1 ? "y" : "ies")} (same type and work point spacing: "
                 + string.Join(", ", fams.Select(f => $"{f.Id} {f.Type}{(f.Style != "" ? " Type " + f.Style : "")} {In(f.Spacing)} x {f.Marks.Count}")) + "); "
                 + $"top WP {(double.IsNaN(std.TopWorkPoint) ? "not given" : In(std.TopWorkPoint))} below the {refText} at the {endText}, depth {(double.IsNaN(std.Depth) ? "not given" : In(std.Depth))}, "
                 + $"plane {(std.Plane ?? (cfd.PlatesAtEachTopOfWeb ? StandardFramePlane.Averaged : cfd.FramePlane ?? StandardFramePlane.Midway)) switch { StandardFramePlane.DefinedEnd => "fitted to the defined end", StandardFramePlane.Midway => "midway between the plates", StandardFramePlane.Warped => "warped (each end on its plate, members rolled)", _ => "averaged" }}. "
                 + (fams.Count > 1 && fams.Max(f => f.Spacing) - fams.Min(f => f.Spacing) < UnitsFine && double.IsNaN(std.SpacingRounding)
                     ? $"The {fams.Count} families differ by {Dec(fams.Max(f => f.Spacing) - fams.Min(f => f.Spacing))} at most - SpacingRounding (the precision the spacing is detailed to) is not given, so they are kept apart. " : "")
                 + $"Deepest common depth for this top WP{(double.IsNaN(std.BottomClearance) ? " (bottom clearance 0)" : $" and {In(std.BottomClearance)} bottom clearance")}: {Dec(sum.MaxCommonDepth)} at {gov.Mark} {gov.Bay} span {gov.Span + 1}, "
                 + $"{(gov.MaxDepthEnd == "R" ? "right" : "left")} end G{sum.GoverningGirder + 1} sta {_u.FormatStation(sum.GoverningStation)} (top of web difference across the bay {Dec(Math.Abs(gov.ReferenceDifference))}). "
                 + $"Smallest top WP clearing the top clearance at every frame {Dec(sum.MinTopWorkPoint)}, deepest common depth there {Dec(sum.MaxDepthAtMinTopWorkPoint)} (information, not applied).");
            if (sum.Applied && sum.Misfits.Count > 0)
                Warning($"Standard cross frame '{std.DisplayName}': depth {In(std.Depth)} does not fit at {sum.Misfits.Count} of {rows.Count} frames: "
                        + string.Join(", ", sum.Misfits.Take(12).Select(m => { var r = rows.First(x => x.Mark == m); return $"{m} ({Dec(r.Headroom)})"; }))
                        + (sum.Misfits.Count > 12 ? ", ..." : "") + " - built at the entered depth, nothing moved (sbm crossframes lists every frame).");
            if (sum.TopMisfits.Count > 0)
                Warning($"Standard cross frame '{std.DisplayName}': the top work point is closer to the top flange than the top clearance{(double.IsNaN(std.TopClearance) ? " (0, not given)" : $" {In(std.TopClearance)}")} at {sum.TopMisfits.Count} of {rows.Count} frames: "
                        + string.Join(", ", sum.TopMisfits.Take(12).Select(m => { var r = rows.First(x => x.Mark == m); return $"{m} ({Dec(r.TopShortfall)} short)"; }))
                        + (sum.TopMisfits.Count > 12 ? ", ..." : "") + $"; smallest top WP that clears every frame {Dec(sum.MinTopWorkPoint)}.");
            if (sum.Applied)
            {
                var sh = rows.Where(r => r.Applied).SelectMany(r => new[] { r.ShiftTopL, r.ShiftTopR }).Where(x => !double.IsNaN(x)).ToList();
                if (sh.Count > 0)
                    Info($"Standard cross frame '{std.DisplayName}': hole groups on the connection plates shifted from the standard pattern by {Dec(sh.Min())} to {Dec(sh.Max())} "
                         + "(holes where each bolt axis meets the plate; no slotted holes; table: sbm crossframes).");
            }
            // Twist checks against the user's limits only.
            double tl = std.TwistLimitDegrees, gl = std.TwistGapLimit;
            if (!double.IsNaN(tl) || !double.IsNaN(gl))
            {
                var over = rows.Where(r => (!double.IsNaN(tl) && Math.Abs(r.TwistDegrees) > tl) || (!double.IsNaN(gl) && r.MaxGapBuilt > gl)).ToList();
                foreach (var r in over) r.TwistFlag = true;
                foreach (var r in over.Take(20))
                    Warning($"CHECK (cross frame twist): {r.Mark} {r.Bay} span {r.Span + 1} twist {r.TwistDegrees:0.000} deg, far gusset gap {Dec(r.MaxGapBuilt)} "
                            + $"(limits {(double.IsNaN(tl) ? "-" : tl.ToString("0.###", CultureInfo.InvariantCulture) + " deg")} / {(double.IsNaN(gl) ? "-" : Dec(gl))}) - beat to fit, nothing straightened.");
                if (over.Count > 20) Warning($"CHECK (cross frame twist): {over.Count - 20} more frames over the limits (sbm crossframes).");
            }
        }

        // Twist summary (every reported frame): worst frame, gaps of both plane options.
        if (rep.Rows.Count > 0)
        {
            var worst = rep.Rows.MaxBy(r => Math.Abs(r.TwistDegrees))!;
            static double Median(List<double> v) { v.Sort(); return v.Count == 0 ? 0 : v[v.Count / 2]; }
            var avgG = rep.Rows.Select(r => r.MaxGapAveraged).ToList();
            var fitG = rep.Rows.Select(r => r.MaxGapFitted).ToList();
            var wa = rep.Rows.MaxBy(r => r.MaxGapAveraged)!;
            var wf = rep.Rows.MaxBy(r => r.MaxGapFitted)!;
            var wm = rep.Rows.MaxBy(r => r.MaxGapMidway)!;
            var sa = rep.Rows.MaxBy(r => r.SpreadAveraged)!;
            var sf = rep.Rows.MaxBy(r => r.SpreadFitted)!;
            Info($"Cross frame twist ({rep.Rows.Count} frames): largest {worst.TwistDegrees:0.000} deg at {worst.Mark} {worst.Bay} span {worst.Span + 1} "
                 + $"(plates {Dec(worst.OffsetL)} / {Dec(worst.OffsetR)} off the frame plane at the top of web); far-gusset gap as built max {Dec(rep.Rows.Max(r => r.MaxGapBuilt))}; "
                 + $"plane options: averaged max {Dec(wa.MaxGapAveraged)} at {wa.Mark} (typical {Dec(Median(avgG))}), fitted to the defined end max {Dec(wf.MaxGapFitted)} at {wf.Mark} (typical {Dec(Median(fitG))}), midway max {Dec(wm.MaxGapMidway)} at {wm.Mark} (typical {Dec(Median(rep.Rows.Select(r => r.MaxGapMidway).ToList()))}); spread over one end's bolts (twist alone) averaged {Dec(sa.SpreadAveraged)} at {sa.Mark}, fitted {Dec(sf.SpreadFitted)} at {sf.Mark}. Report only, nothing straightened.");
        }
        // Warped frames: roll of the members between the two plates and the leg-tip deviation it implies.
        var rolled = rep.Rows.Where(r => r.BuiltPlane == "warped").ToList();
        if (rolled.Count > 0)
        {
            var wr = rolled.MaxBy(r => Math.Abs(r.RollDegrees))!;
            var wt = rolled.MaxBy(r => r.LegTipDeviation)!;
            var med = rolled.Select(r => Math.Abs(r.RollDegrees)).OrderBy(x => x).ToList();
            Info($"Warped cross frames ({rolled.Count}): each end's gussets on its own connection plate (far-gusset gap {Dec(rolled.Max(r => r.MaxGapBuilt))} max); member roll "
                 + $"max {Math.Abs(wr.RollDegrees):0.0000} deg ({wr.Mark} {wr.RollMember}), median {med[med.Count / 2]:0.0000} deg; leg-tip deviation from an untwisted member max "
                 + $"{Dec(wt.LegTipDeviation)} ({wt.Mark}); members rolled by more than {WarpRollTolerance} deg are faceted solids (no exact extrusion).");
        }
        foreach (var d in diag) _diag.Enqueue(d);
    }
}
