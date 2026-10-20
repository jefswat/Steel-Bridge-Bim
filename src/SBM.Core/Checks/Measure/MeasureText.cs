namespace SBM.Core.Checks.Measure;

/// <summary>Plain-text reports of the query results (the JSON carries every value).</summary>
public static class MeasureText
{
    public static string ToText(object r)
    {
        var sb = new System.Text.StringBuilder();
        switch (r)
        {
            case DistanceResult d:
                sb.AppendLine($"Distance {d.A} -> {d.B}: {d.Distance.Text} ({d.Distance.Mm:0.0} mm){(d.Overlap ? " OVERLAP (negative = depth)" : "")}");
                sb.AppendLine($"  at A ({string.Join(", ", d.PointA)}) / B ({string.Join(", ", d.PointB)}){(d.Direction is { } dir ? $", direction A->B ({dir[0]:0.###}, {dir[1]:0.###}, {dir[2]:0.###})" : "")}");
                if (d.A.GlobalId != null || d.B.GlobalId != null) sb.AppendLine($"  GlobalIds {d.A.GlobalId} / {d.B.GlobalId}");
                foreach (var n in d.Notes) sb.AppendLine("  note: " + n);
                break;
            case PointResult p:
                sb.AppendLine($"Point E {p.At[0]}, N {p.At[1]}{(double.IsNaN(p.At[2]) ? "" : $", Z {p.At[2]}")}: {(p.Station != null ? $"Sta {p.Station}, offset {p.Offset} on {p.Alignment}; profile grade {p.ProfileGrade}, finished grade {p.FinishedGrade}" : "")}");
                foreach (var (k, v) in p.Elevations) sb.AppendLine($"  {k}: {v:0.####}");
                foreach (var h in p.Elements) sb.AppendLine($"  {h.Element.Kind,-8} {h.Element.Mark,-30} top {h.Top:0.####} bottom {h.Bottom:0.####} ({h.Depth.Text}){(h.ContainsPoint ? "  <- contains the point" : "")}");
                foreach (var n in p.Notes) sb.AppendLine("  note: " + n);
                break;
            case BarResult b:
                sb.AppendLine($"Bar {b.Bar.Mark} {b.Size} ({b.Shape}) {b.Location}; {b.BarsOfMark} bars of the mark{(b.Bar.GlobalId != null ? ", GlobalId " + b.Bar.GlobalId : "")}");
                sb.AppendLine($"  developed centerline {b.DevelopedLength.Text}{(b.ScheduledLength is { } sl ? $", scheduled {sl.Text}" : "")}; printed pin {b.PrintedPin?.Text ?? "-"}, AASHTO min pin {b.MinPinAashto.Text}");
                sb.AppendLine($"  straights: {string.Join(" | ", b.Straights.Select(x => x.Text))}");
                foreach (var bd in b.Bends) sb.AppendLine($"  bend {bd.No}: {bd.AngleDeg:0.#}°, centerline R {bd.Radius.Text}, pin {bd.PinDiameter.Text} = {bd.PinOverDb:0.##} db");
                foreach (var h in b.Hooks) sb.AppendLine($"  {h}");
                if (b.StartStation != null) sb.AppendLine($"  from {b.StartStation} to {b.EndStation}");
                foreach (var n in b.Notes) sb.AppendLine("  note: " + n);
                break;
            case CoverResult c:
                sb.AppendLine($"Cover {c.Mark} {c.Size}: {c.Bars} bars in {c.Hosts}");
                sb.AppendLine($"  minimum clear cover by face: {string.Join(", ", c.MinByFace.Select(kv => $"{kv.Key} {kv.Value.Text}"))}");
                foreach (var row in c.PerBar.OrderBy(x => x.Min?.In ?? 1e9).Take(5)) sb.AppendLine($"  {row.Bar.Mark} bar index {row.Bar.Index}: min {row.Min?.Text} ({row.MinFace}){(row.Outside ? " OUTSIDE the concrete" : "")}");
                foreach (var n in c.Notes) sb.AppendLine("  note: " + n);
                break;
            case SpacingResult s:
                sb.AppendLine($"Spacing {s.Mark}: {s.Bars} bars on {s.Lines} lines, {s.Along}");
                if (s.FirstStation != null) sb.AppendLine($"  first line Sta {s.FirstStation}, last Sta {s.LastStation}");
                sb.AppendLine($"  {s.Chain}  (total {s.Total.Text})");
                foreach (var n in s.Notes) sb.AppendLine("  note: " + n);
                break;
            case LapResult l:
                sb.AppendLine($"Laps {l.Mark} {l.Size}: {l.Laps.Count} lapped pairs among {l.Bars} bars; lap {l.MinLap?.Text} .. {l.MaxLap?.Text}{(l.ClassBLap is { } cb ? $", Class B (AASHTO 5.10.8) {cb.Text}" : "")}");
                if (l.MinStagger is { } ms) sb.AppendLine($"  min distance between lap centers {ms.Text}; max share of laps within one lap length {l.MaxShareSplicedPct}%");
                foreach (var row in l.Laps.Take(12)) sb.AppendLine($"  {row.BarA} / {row.BarB}: lap {row.Lap.Text}, clear {row.Clear.Text}{(row.CenterStation != null ? $" at Sta {row.CenterStation}" : "")}");
                if (l.Laps.Count > 12) sb.AppendLine($"  ... {l.Laps.Count - 12} more (JSON)");
                foreach (var n in l.Notes) sb.AppendLine("  note: " + n);
                break;
            default: sb.AppendLine(Fmt.ToJson(r)); break;
        }
        return sb.ToString();
    }
}
