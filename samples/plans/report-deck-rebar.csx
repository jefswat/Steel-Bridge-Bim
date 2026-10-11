// Deck reinforcing per unit and mark: model vs the plans' slab bar lists (I-80 PDF 196, Ramp G PDF 141 = docs\plan-data\deck-bar-lists.csv).  Does not save the model.
//   sbm run samples\plans\report-deck-rebar.csx "temp\model.sbm"
// Prints per bridge: placed bars (3D) / schedule-only marks / plan totals, then every mark (count, length, weight) with the difference.  DR_CSV=1 prints CSV rows.
using System.Globalization;
const double LbPerKg = 2.2046226218;
string csv = new[] { @"docs\plan-data\deck-bar-lists.csv", @"C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\docs\plan-data\deck-bar-lists.csv" }.First(System.IO.File.Exists);
var plan = new List<(string Br, int U, string Mark, string Loc, int Count, string Len, bool Varies, double Lb)>();
foreach (var ln in System.IO.File.ReadAllLines(csv))
{
    if (ln.StartsWith("#") || ln.Trim().Length == 0) continue;
    var c = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
    foreach (char ch in ln) { if (ch == '"') q = !q; else if (ch == ',' && !q) { c.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); }
    c.Add(sb.ToString());
    plan.Add((c[0], int.Parse(c[1]), c[2], c[4], int.Parse(c[5]), c[6], c[7] == "1", double.Parse(c[8], CultureInfo.InvariantCulture)));
}
bool Slab(string mark) => mark.StartsWith("7a") || mark.StartsWith("6b") || mark.StartsWith("5j") || mark.StartsWith("5a");
foreach (var b in Doc.Project.Bridges)
{
    var cm = Doc.Concrete(b.Name);
    string br = b.Name.StartsWith("I-80") ? "I-80 WB" : "Ramp G";
    int unit = int.Parse(b.Name.Substring(b.Name.LastIndexOf(' ') + 1));
    var marks = cm.Marks.Where(m => m.Unit == "").ToList();
    var placed = marks.Where(m => !m.Location.Contains("schedule only")).ToList();
    var sched = marks.Where(m => m.Location.Contains("schedule only")).ToList();
    var nIn3d = cm.Bars.Where(bp => bp.Unit == "").GroupBy(bp => bp.Mark).ToDictionary(g => g.Key, g => g.Count());
    double In3d(SBM.Core.Concrete.BarMark m) => m.Count == 0 ? 0 : Math.Min(1.0, (nIn3d.TryGetValue(m.Mark, out var k) ? k : 0) / (double)m.Count);
    var rows = plan.Where(p => p.Br == br && p.U == unit).ToList();
    double planSlab = rows.Where(r => Slab(r.Mark)).Sum(r => r.Lb), planAll = rows.Sum(r => r.Lb);
    double mSlab = marks.Where(m => Slab(m.Mark)).Sum(m => m.TotalMass) * LbPerKg;
    double mAll = marks.Sum(m => m.TotalMass) * LbPerKg;
    Print($"== {b.Name} ({(b.Deck.Reinforcement.Plan != null ? "plan-driven" : "generic layout")}): slab bars {mSlab:N0} lb vs plan {planSlab:N0} lb ({(planSlab > 0 ? (mSlab / planSlab - 1) * 100 : 0):+0.0;-0.0}%); " +
          $"all marks incl. end dam {mAll:N0} lb vs plan {planAll:N0} lb; placed in 3D {placed.Sum(m => m.TotalMass * In3d(m)) * LbPerKg:N0} lb ({placed.Sum(m => (int)Math.Round(m.Count * In3d(m))):N0} bars), schedule only / not modeled {(sched.Sum(m => m.TotalMass) + placed.Sum(m => m.TotalMass * (1 - In3d(m)))) * LbPerKg:N0} lb");
    if (b.Deck.Reinforcement.Plan == null) continue;
    var fam = b.Deck.Reinforcement.Plan.Families;
    foreach (var r in rows.Where(r => Slab(r.Mark) && !fam.Any(f => r.Mark.StartsWith(f))))
    {
        var m = marks.FirstOrDefault(x => x.Mark == r.Mark);
        double mw = m == null ? 0 : m.TotalMass * LbPerKg;
        Print($"   {r.Mark,-5} plan {r.Count,5} x {(r.Varies ? "VARIES" : r.Len),-8} {r.Lb,9:N0} lb   model {(m?.Count ?? 0),5} x {(m == null ? "-" : m.Varies ? $"{m.MinLength / 0.3048:F1}-{m.Length / 0.3048:F1} ft" : $"{m.Length / 0.3048:F2} ft"),-14} {mw,9:N0} lb  {(r.Lb > 0 ? (mw / r.Lb - 1) * 100 : 0),+6:0.0}%{(m != null && m.Location.Contains("schedule only") ? "  (schedule only)" : "")}");
    }
    foreach (var f in fam)
    {
        var pr = rows.Where(r => r.Mark.StartsWith(f)).ToList();
        var mm = marks.Where(m => m.Mark.StartsWith(f)).ToList();
        Print($"   {f}*   plan {pr.Sum(r => r.Count),5} bars {pr.Sum(r => r.Lb),9:N0} lb   model {mm.Sum(m => m.Count),5} bars {mm.Sum(m => m.TotalMass) * LbPerKg,9:N0} lb  {(pr.Sum(r => r.Lb) > 0 ? (mm.Sum(m => m.TotalMass) * LbPerKg / pr.Sum(r => r.Lb) - 1) * 100 : 0),+6:0.0}%   ({string.Join(", ", mm.OrderBy(m => m.Mark).Select(m => $"{m.Mark} x{m.Count}"))})");
    }
    var endDam = rows.Where(r => !Slab(r.Mark)).ToList();
    var damPlaced = endDam.Where(r => marks.Any(m => m.Mark == r.Mark && !m.Location.Contains("schedule only"))).ToList();
    var damSched = endDam.Except(damPlaced).ToList();
    foreach (var r in damPlaced)
    {
        var m = marks.First(x => x.Mark == r.Mark);
        int in3d = cm.Bars.Count(bp => bp.Mark == r.Mark && bp.Unit == "");
        double mw = m.TotalMass * LbPerKg;
        Print($"   {r.Mark,-5} plan {r.Count,5} x {r.Len,-8} {r.Lb,9:N0} lb   model {m.Count,5} x {m.Length / 0.3048:F2} ft {mw,9:N0} lb  {(r.Lb > 0 ? (mw / r.Lb - 1) * 100 : 0),+6:0.0}%   end dam, {in3d} in 3D{(in3d < m.Count ? $", {m.Count - in3d} not modeled (UNRESOLVED)" : "")}");
    }
    Print($"   end dam / insert marks (schedule only): {string.Join(", ", damSched.Select(r => r.Mark))} = {damSched.Sum(r => r.Lb):N0} lb");
}
