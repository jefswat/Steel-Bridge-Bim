// Railing base jump check (all bridges): consecutive RailingRun.Line points whose |dz| exceeds 1/4" beyond the grade change (0.2 x plan distance).
// A jump = the reference line (base = top of concrete under the line) steps, e.g. a gap / notch in a curb: the handrail would kink.
//   sbm run samples\plans\check-rail-base.csx "<model>.sbm"       (prints one row per jump, then the totals; changes nothing)
double tol = 0.0254 / 4;
int runs = 0, jumps = 0;
foreach (var n in Doc.Project.Bridges.Select(b => b.Name))
{
    var rr = Doc.Model(n).Steel?.Railings;
    if (rr == null) continue;
    foreach (var r in rr.Runs)
    {
        runs++;
        for (int i = 1; i < r.Line.Count; i++)
        {
            var a = r.Line[i - 1]; var b = r.Line[i];
            double dxy = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)), dz = b.Z - a.Z;
            if (Math.Abs(dz) > tol + 0.2 * dxy) { jumps++; Print($"JUMP {n} | {r.Name} | pt {i} ({a.X:F2}, {a.Y:F2}) dz {dz * 1000:F1} mm over {dxy:F2} m"); }
        }
    }
}
Print($"{runs} railing runs, {jumps} base jump(s) > 1/4\" beyond the grade change");
foreach (var n in Doc.Project.Bridges.Select(b => b.Name))
    foreach (var d in Doc.Model(n).Steel?.Railings?.Diagnostics ?? new()) if (d.Message.Contains("dips for less")) Print($"BRIDGED {n}: {d.Message}");
