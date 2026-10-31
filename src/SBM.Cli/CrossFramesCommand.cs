using System.Globalization;
using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Cli;

/// <summary><c>sbm crossframes</c>: standard cross frame depth report, hole-shift table and connection plate twist (read-only, the model is never saved).</summary>
public static class CrossFramesCommand
{
    public const string Help = """
        sbm crossframes <model.sbm> [--bridge NAME] [--csv f.csv] [--rows N] [--twist] [--legacy] [--frame-plane midway|averaged|defined|warped]
                        [--top L] [--depth L] [--top-clear L] [--bottom-clear L] [--end taller|left|right] [--plane averaged|defined|midway|warped]
                        [--reference web|steel] [--scope intermediate|support|all] [--rounding L]
          Reports the model's STANDARD cross frames (CrossFrameDefinition.Standard / CrossFrameStyle.Standard): families (one shop frame per type and
          work point spacing), deepest common depth for the entered top work point with the governing frame / end / girder / station, misfits, the
          hole-shift table (hole groups on each connection plate vs the standard pattern, + = up) and the twist between the two connection plates of
          every frame (angle, plate offsets, far-gusset gap per bolt for the averaged plane and the plane fitted to the defined end).
          --twist         also report frames that are not standard (CrossFrameDefinition.ReportTwist, in memory only).
          --legacy        in memory: CrossFrameDefinition.PlatesAtEachTopOfWeb (plates at each girder's top-of-web crossing, the pre-2026-10-27 output).
          --frame-plane   in memory: CrossFrameDefinition.FramePlane (default midway; warped = each end on its plate, members rolled).
          --top/--depth   WHAT-IF: apply a standard with these values in memory (other options as listed; not saved). Lengths in project units
                          or with units (6", 5'-4").
          Without a standard or --twist nothing is reported. The .sbm is never written.
        """;

    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine(Help); return 0; }
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var doc = SbmDocument.Open(args[1]);
        var u = doc.Units;
        double Len(string name)
        {
            var t = Opt(name);
            if (t == null) return double.NaN;
            if (!u.TryParseLength(t, LengthKind.Short, out double m)) throw new ArgumentException($"{name} {t}: not a length");
            return m;
        }
        string? bridgeFilter = Opt("--bridge");
        var bridges = doc.Project.Bridges.Where(b => b.Girders.Count > 0 && !b.Girders.IsTub
            && (bridgeFilter == null || b.Name.Contains(bridgeFilter, StringComparison.OrdinalIgnoreCase))).ToList();
        if (bridges.Count == 0) { Console.Error.WriteLine("No steel I-girder bridge matches."); return 1; }

        bool whatIf = Opt("--top") != null || Opt("--depth") != null;
        foreach (var b in bridges)
        {
            if (args.Contains("--twist")) b.CrossFrames.ReportTwist = true;
            if (args.Contains("--legacy")) b.CrossFrames.PlatesAtEachTopOfWeb = true;
            if (Opt("--frame-plane") is { } fpo) b.CrossFrames.FramePlane = PlaneOpt(fpo);
            if (!whatIf) continue;
            var s = b.CrossFrames.Standard?.Clone() ?? new StandardCrossFrameDefinition { Name = "what-if" };
            if (Opt("--top") != null) s.TopWorkPoint = Len("--top");
            if (Opt("--depth") != null) s.Depth = Len("--depth");
            if (Opt("--top-clear") != null) s.TopClearance = Len("--top-clear");
            if (Opt("--bottom-clear") != null) s.BottomClearance = Len("--bottom-clear");
            if (Opt("--end") is { } e) s.DefinedEnd = Enum.Parse<StandardDefinedEnd>(e, true);
            if (Opt("--plane") is { } p) s.Plane = PlaneOpt(p);
            if (Opt("--reference") is { } r) s.Reference = r.Contains("steel", StringComparison.OrdinalIgnoreCase) ? StandardFrameReference.TopOfSteel : StandardFrameReference.TopOfWeb;
            if (Opt("--scope") is { } sc) s.Scope = Enum.Parse<StandardFrameScope>(sc, true);
            if (Opt("--rounding") != null) s.SpacingRounding = Len("--rounding");
            b.CrossFrames.Standard = s;
        }
        doc.Regenerate();

        string L(double m) => double.IsNaN(m) ? "" : u.IsUS ? (m / 0.0254).ToString("0.000", CultureInfo.InvariantCulture) : (m * 1000).ToString("0.0", CultureInfo.InvariantCulture);
        string unit = u.IsUS ? "in" : "mm";
        string Sta(double s) => double.IsNaN(s) ? "" : u.FormatStation(s);
        int maxRows = int.Parse(Opt("--rows") ?? "40", CultureInfo.InvariantCulture);
        StreamWriter? csv = Opt("--csv") is { } cf ? new StreamWriter(cf) : null;
        csv?.WriteLine($"bridge,mark,family,standard,style,type,support,span,bay,station,defined_end,applied,plane,spacing_{unit},tow_difference_{unit},top_wp_{unit},depth_{unit},"
                       + $"clear_top_L_{unit},clear_top_R_{unit},clear_bottom_L_{unit},clear_bottom_R_{unit},max_depth_{unit},max_depth_end,headroom_{unit},top_shortfall_{unit},"
                       + $"left_girder,left_station,left_plate,shift_top_L_{unit},shift_bottom_L_{unit},right_girder,right_station,right_plate,shift_top_R_{unit},shift_bottom_R_{unit},"
                       + $"twist_deg,offset_L_{unit},offset_R_{unit},gap_built_L_min,gap_built_L_max,gap_built_R_min,gap_built_R_max,"
                       + $"gap_avg_L_min,gap_avg_L_max,gap_avg_R_min,gap_avg_R_max,gap_fit_L_min,gap_fit_L_max,gap_fit_R_min,gap_fit_R_max,gap_mid_L_min,gap_mid_L_max,gap_mid_R_min,gap_mid_R_max,twist_flag,reference_end,shift_L_{unit},shift_R_{unit},roll_deg,roll_member,leg_tip_{unit}");
        int reported = 0;
        foreach (var b in bridges)
        {
            var rep = doc.CrossFrameFit(b.Name);
            if (rep == null) continue;
            reported++;
            Console.WriteLine($"== {b.Name}: {rep.Rows.Count} frames reported");
            foreach (var d in doc.Steel(b.Name).Diagnostics.Where(d => d.Message.Contains("tandard cross frame") || d.Message.Contains("Cross frame twist") || d.Message.Contains("cross frame twist") || d.Message.Contains("Warped cross frames")))
                Console.WriteLine($"  [{d.Severity}] {d.Message}");
            foreach (var d in doc.Framing(b.Name).Diagnostics.Where(d => d.Message.Contains("connection plates on the frame plane")))
                Console.WriteLine($"  [{d.Severity}] {d.Message}");
            if (rep.Families.Count > 0)
            {
                Console.WriteLine($"  Families ({unit}):");
                foreach (var f in rep.Families)
                    Console.WriteLine($"    {f.Id,-4} {f.Standard,-12} {f.Type,-18} {(f.Style == "" ? "" : "Type " + f.Style),-10} spacing {L(f.Spacing),9}  x {f.Marks.Count,3}  prototype {f.Prototype}");
            }
            var std = rep.Rows.Where(r => r.Applied).ToList();
            if (std.Count > 0)
            {
                Console.WriteLine($"  Hole-shift table ({unit}, + = up from the standard pattern; plate marks from the stiffener numbering):");
                Console.WriteLine($"    {"frame",-8}{"fam",-5}{"bay",-8}{"span",5} {"left plate",-14}{"top",8}{"bot",8}  {"right plate",-14}{"top",8}{"bot",8}{"headroom",10}{"twist deg",10}");
                foreach (var r in std.OrderBy(r => r.LeftGirder).ThenBy(r => r.LeftS).Take(maxRows))
                    Console.WriteLine($"    {r.Mark,-8}{r.Family,-5}{r.Bay,-8}{r.Span + 1,5} {r.LeftPlate,-14}{L(r.ShiftTopL),8}{L(r.ShiftBottomL),8}  {r.RightPlate,-14}{L(r.ShiftTopR),8}{L(r.ShiftBottomR),8}{L(r.Headroom),10}{r.TwistDegrees,10:0.000}");
                if (std.Count > maxRows) Console.WriteLine($"    ... {std.Count - maxRows} more (--rows N or --csv)");
            }
            // Twist evaluation of the two plane options.
            static double Med(IEnumerable<double> v) { var l = v.OrderBy(x => x).ToList(); return l.Count == 0 ? 0 : l[l.Count / 2]; }
            var tw = rep.Rows.MaxBy(r => Math.Abs(r.TwistDegrees))!;
            var wa = rep.Rows.MaxBy(r => r.MaxGapAveraged)!;
            var wf = rep.Rows.MaxBy(r => r.MaxGapFitted)!;
            var wm = rep.Rows.MaxBy(r => r.MaxGapMidway)!;
            Console.WriteLine($"  Twist ({unit}): max {tw.TwistDegrees:0.000} deg at {tw.Mark} {tw.Bay} span {tw.Span + 1} sta {Sta(tw.Station)}, median |twist| {Med(rep.Rows.Select(r => Math.Abs(r.TwistDegrees))):0.000} deg");
            Console.WriteLine($"    far-gusset gap, averaged plane:          max {L(wa.MaxGapAveraged)} at {wa.Mark} {wa.Bay} span {wa.Span + 1}, median {L(Med(rep.Rows.Select(r => r.MaxGapAveraged)))}");
            Console.WriteLine($"    far-gusset gap, midway between plates:   max {L(wm.MaxGapMidway)} at {wm.Mark} {wm.Bay} span {wm.Span + 1}, median {L(Med(rep.Rows.Select(r => r.MaxGapMidway)))}");
            Console.WriteLine($"    far-gusset gap, fitted to defined end:   max {L(wf.MaxGapFitted)} at {wf.Mark} {wf.Bay} span {wf.Span + 1}, median {L(Med(rep.Rows.Select(r => r.MaxGapFitted)))}");
            var sa = rep.Rows.MaxBy(r => r.SpreadAveraged)!;
            var sf = rep.Rows.MaxBy(r => r.SpreadFitted)!;
            Console.WriteLine($"    spread over one end's bolts (twist alone): averaged max {L(sa.SpreadAveraged)} at {sa.Mark}, median {L(Med(rep.Rows.Select(r => r.SpreadAveraged)))}; fitted max {L(sf.SpreadFitted)} at {sf.Mark}, median {L(Med(rep.Rows.Select(r => r.SpreadFitted)))}");
            Console.WriteLine($"    as built: max {L(rep.Rows.Max(r => r.MaxGapBuilt))}; plate offset from the frame plane at the top of web max {L(rep.Rows.Max(r => Math.Max(Math.Abs(r.OffsetL), Math.Abs(r.OffsetR))))}");
            var moved = rep.Rows.Select(r => Math.Max(Math.Abs(r.LeftShift), Math.Abs(r.RightShift))).Where(x => x > 0).ToList();
            if (moved.Count > 0)
                Console.WriteLine($"    plates moved onto the frame plane: {moved.Count} frames, max {L(moved.Max())}, median {L(Med(moved))}");
            var rolled = rep.Rows.Where(r => r.BuiltPlane == "warped").ToList();
            if (rolled.Count > 0)
            {
                var wr = rolled.MaxBy(r => Math.Abs(r.RollDegrees))!;
                var wt = rolled.MaxBy(r => r.LegTipDeviation)!;
                Console.WriteLine($"    warped: member roll max {Math.Abs(wr.RollDegrees):0.0000} deg at {wr.Mark} {wr.RollMember}, median {Med(rolled.Select(r => Math.Abs(r.RollDegrees))):0.0000} deg; "
                                  + $"leg-tip deviation max {L(wt.LegTipDeviation)} at {wt.Mark}, median {L(Med(rolled.Select(r => r.LegTipDeviation)))}");
            }
            if (csv != null)
                foreach (var r in rep.Rows)
                    csv.WriteLine(string.Join(",", new[]
                    {
                        b.Name, r.Mark, r.Family, r.Standard, r.Style, r.Type, r.AtSupport ? "yes" : "", (r.Span + 1).ToString(CultureInfo.InvariantCulture), r.Bay, Sta(r.Station),
                        r.DefinedEnd, r.Applied ? "yes" : "", r.BuiltPlane, L(r.Spacing), L(r.ReferenceDifference), L(r.TopWorkPoint), L(r.Depth),
                        L(r.ClearTopL), L(r.ClearTopR), L(r.ClearBottomL), L(r.ClearBottomR), L(r.MaxDepth), r.MaxDepthEnd, L(r.Headroom), L(r.TopShortfall),
                        $"G{r.LeftGirder + 1}", Sta(r.LeftStation), r.LeftPlate, L(r.ShiftTopL), L(r.ShiftBottomL),
                        $"G{r.RightGirder + 1}", Sta(r.RightStation), r.RightPlate, L(r.ShiftTopR), L(r.ShiftBottomR),
                        r.TwistDegrees.ToString("0.0000", CultureInfo.InvariantCulture), L(r.OffsetL), L(r.OffsetR),
                        L(r.GapBuiltL.Min), L(r.GapBuiltL.Max), L(r.GapBuiltR.Min), L(r.GapBuiltR.Max),
                        L(r.GapAveragedL.Min), L(r.GapAveragedL.Max), L(r.GapAveragedR.Min), L(r.GapAveragedR.Max),
                        L(r.GapFittedL.Min), L(r.GapFittedL.Max), L(r.GapFittedR.Min), L(r.GapFittedR.Max),
                        L(r.GapMidwayL.Min), L(r.GapMidwayL.Max), L(r.GapMidwayR.Min), L(r.GapMidwayR.Max), r.TwistFlag ? "yes" : "",
                        r.ReferenceEnd, L(r.LeftShift), L(r.RightShift), r.RollDegrees.ToString("0.00000", CultureInfo.InvariantCulture), r.RollMember, L(r.LegTipDeviation),
                    }.Select(Csv)));
        }
        csv?.Dispose();
        if (reported == 0) Console.WriteLine("No cross frame report: the model sets no standard cross frame and no ReportTwist (use --twist, or --top / --depth for a what-if).");
        return 0;
    }

    private static StandardFramePlane PlaneOpt(string p) =>
        p.StartsWith("def", StringComparison.OrdinalIgnoreCase) ? StandardFramePlane.DefinedEnd
        : p.StartsWith("mid", StringComparison.OrdinalIgnoreCase) ? StandardFramePlane.Midway
        : p.StartsWith("war", StringComparison.OrdinalIgnoreCase) ? StandardFramePlane.Warped
        : p.StartsWith("av", StringComparison.OrdinalIgnoreCase) ? StandardFramePlane.Averaged
        : throw new ArgumentException($"plane '{p}': use midway, averaged, defined or warped");

    private static string Csv(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
