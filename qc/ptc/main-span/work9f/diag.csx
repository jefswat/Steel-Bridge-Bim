// diagnostics of the main span containing a regex (read-only)
//   sbm run diag.csx <model> --no-pipeline -- <regex> [member regex]
using SBM.Core.Bridge;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
var t = Doc.Project.Bridges[0].Truss!;
if (Args.Length > 1)
    foreach (var m in t.Members.Where(m => System.Text.RegularExpressions.Regex.IsMatch(m.Name, Args[1])))
        Print($"MEMBER {m.Name} role {m.Role} sec {m.Section} {m.From} -> {m.To} setbacks {m.StartSetback}/{m.EndSetback} cuts {m.StartCut}/{m.EndCut} plane '{m.Plane}' offZ {m.OffsetZ}");
Doc.Regenerate();
var tm = Doc.Model(BN);
foreach (var d in tm.Steel!.Diagnostics.Where(d => System.Text.RegularExpressions.Regex.IsMatch(d.Message, Args[0]))) Print($"{d.Severity}: {d.Message}");
