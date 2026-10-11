// Clear-cover containment of the plan-listed substructure bars (caps, pedestals, footings, columns, dowels): one row per bar outside the
// envelope (SubstructureUnit.CoverViolations), summarised by bridge / unit / mark / governing face with the worst excess. Does not save the model.
//   sbm run samples\plans\check-sub-cover.csx "temp\model.sbm" [-- detail]
bool detail = Args.Length > 0 && Args[0] == "detail";
double In_(double m) => m / 0.0254;
int grand = 0;
foreach (var b in Doc.Project.Bridges)
{
    var sm = Doc.Model(b.Name).Substructure;
    if (sm == null) continue;
    foreach (var u in sm.Units)
    {
        if (u.CoverViolations.Count == 0) continue;
        grand += u.CoverViolations.Count;
        Print($"== {b.Name} / {u.Name}: {u.CoverViolations.Count} bar(s)");
        foreach (var g in u.CoverViolations.GroupBy(v => (v.Kind, v.Face)).OrderBy(g => g.Key.Kind))
        {
            var w = g.OrderByDescending(v => v.Excess).First();
            Print($"   {g.Key.Kind,-40} {g.Key.Face,-32} x{g.Count(),4}  worst {In_(w.Excess):0.00}\"  min {In_(g.Min(v => v.Excess)):0.00}\"  at S {w.S:0.000} N {w.N:0.000} Z {w.Z:0.000}");
            if (detail) foreach (var v in g) Print($"        {In_(v.Excess):0.00}\" at S {v.S:0.000} N {v.N:0.000} Z {v.Z:0.000}");
        }
    }
}
Print($"TOTAL {grand} bar(s) outside the clear-cover envelope");
