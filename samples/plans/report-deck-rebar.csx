// Deck reinforcing per unit and mark: model vs the plans' slab bar lists (I-80 PDF 196, Ramp G PDF 141).  Does not save the model.
//   sbm run samples\plans\report-deck-rebar.csx "temp\model.sbm"
// Env: DR_PLAN=<csv> (default docs\plan-data\deck-bar-lists.csv); DR_MARKS=1 prints every mark.
using System.Globalization;
const double LbPerKg = 2.2046226218;
var verbose = Environment.GetEnvironmentVariable("DR_MARKS") == "1";
foreach (var b in Doc.Project.Bridges)
{
    var cm = Doc.Concrete(b.Name);
    var slab = cm.Marks.Where(m => m.Unit == "").ToList();
    double lb = slab.Sum(m => m.TotalMass) * LbPerKg;
    Print($"{b.Name}: {slab.Count} marks, {slab.Sum(m => m.Count)} bars, {lb:N0} lb");
    if (verbose)
        foreach (var m in slab.OrderBy(m => m.Mark))
            Print($"   {m.Mark,-8} {m.Size.Name,-3} x{m.Count,-6} {(m.Varies ? "VARIES " + (m.MinLength / 0.3048).ToString("F1") : (m.Length / 0.3048).ToString("F2"))} ft  {m.TotalMass * LbPerKg:N0} lb  {m.Location}");
}
