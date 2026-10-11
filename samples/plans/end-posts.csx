// Crash cushion end posts (I-80 PDF 229 Ramp A = design 222, PDF 230 Ramp G = design 223): the 7'-6 concrete wedge (3'-8 -> 2'-10 high over 5'-0, then 2'-10 for
// 2'-6, 2'-0 end) with its bar list; the cushion unit (Materials I.M. 455) has no geometry in the plans. Data: docs/plan-data/misc-items-data.md (c),
// misc/crash-cushion-end-post-bars.csv. Both posts belong to I-80 WB Unit 2 (Ramp A: Pier 5WB, Ramp G: Pier 11WB) - Ramp A itself is not a model bridge.
//  * POSITION UNRESOLVED (distance from the CL pier to the start of the end block is not dimensioned; the Ramp G wedge is skewed 27d27'55" and its plan dimensions other
//    than the 7'-6 are not resolved): the wedge and the cushion placeholder are NOT placed; the bar lists are scheduled (weights reproduce the plan: epoxy 373 / 450 lb,
//    stainless 240 / 262 lb). Varying bars (length range printed only) take the average length from the printed weight.
// Re-runnable.
using SBM.Core.Bridge;

double F(int ft, double inch) => Ft(ft) + In(inch);
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
u2.Misc ??= new MiscItemsDefinition();
u2.Misc.EndPosts.Clear();
u2.Misc.Unresolved.RemoveAll(s => s.StartsWith("[cushion]"));

// mark, count, length ft, length in (0 = varies), weight lb, stainless?, location
MiscBarRow R(string mark, int n, int ft, double inch, double lb, bool stainless, string loc) => new()
{
    Mark = mark, Size = "#" + new string(mark.TakeWhile(char.IsDigit).ToArray()), Count = n, Length = ft == 0 && inch == 0 ? 0 : F(ft, inch), WeightLb = lb,
    Epoxy = !stainless, Coating = stainless ? "stainless" : "", Location = loc,
};
var ra = new List<MiscBarRow>
{
    R("6c2", 8, 4, 8, 56, false, "end block vertical"), R("5c4", 6, 7, 1, 44, false, "rail vertical"), R("6d1", 24, 3, 7, 129, false, "rail horizontal"),
    R("6d2", 8, 7, 2, 86, false, "end block horizontal"), R("6d3", 3, 7, 2, 32, false, "end block horizontal"), R("6d4", 2, 3, 7, 11, false, "rail horizontal"),
    R("5d5", 2, 7, 2, 15, false, "end block horizontal"), R("6c1", 6, 6, 3, 56, true, "end block vertical"),
    R("6c3", 10, 0, 0, 100, true, "end block vertical, vertical leg 3'-4 min to 4'-0 max"), R("5c5", 6, 5, 10, 37, true, "rail vertical"),
    R("5c6", 2, 5, 4, 11, true, "end block vertical"), R("5c7", 2, 5, 5, 11, true, "end block vertical"), R("5c8", 2, 5, 11, 12, true, "end block vertical"),
    R("5c9", 2, 6, 0, 13, true, "end block vertical"),
};
var rg = new List<MiscBarRow>
{
    R("6c2", 3, 4, 8, 21, false, "end block vertical"), R("6c4", 5, 0, 0, 37, false, "end block vertical, varies"), R("5c5", 7, 7, 1, 52, false, "rail vertical"),
    R("5c11", 2, 7, 5, 15, false, "rail vertical"), R("6d1", 12, 3, 10, 69, false, "rail horizontal"), R("6d2", 8, 7, 2, 86, false, "end block horizontal"),
    R("6d3", 4, 7, 2, 43, false, "end block horizontal"), R("6d4", 1, 3, 5, 5, false, "rail horizontal"), R("6d5", 1, 5, 0, 8, false, "rail horizontal"),
    R("6d6", 12, 5, 6, 99, false, "rail horizontal"), R("5d7", 1, 7, 2, 7, false, "end block horizontal"), R("5d8", 1, 7, 3, 8, false, "end block horizontal"),
    R("6c1", 6, 6, 3, 56, true, "end block vertical"), R("6c3", 10, 0, 0, 100, true, "end block vertical, varies"), R("5c6", 7, 5, 10, 43, true, "rail vertical"),
    R("5c7", 2, 5, 7, 12, true, "end block vertical"), R("5c8", 2, 5, 8, 12, true, "end block vertical"), R("5c9", 2, 6, 2, 13, true, "end block vertical"),
    R("5c10", 2, 6, 3, 13, true, "end block vertical"), R("5c12", 2, 6, 0, 13, true, "rail vertical"),
};
u2.Misc.EndPosts.Add(new CrashCushionEndPostDefinition { Name = "Crash cushion end post Ramp A (Pier 5WB)", Support = "Pier 5WB", Bars = ra });
u2.Misc.EndPosts.Add(new CrashCushionEndPostDefinition { Name = "Crash cushion end post Ramp G (Pier 11WB)", Support = "Pier 11WB", Bars = rg });
u2.Misc.Unresolved.Add("[cushion] End posts: start of the end block from the CL pier (scaled about 2'-1 on Ramp A), wedge direction, plan width at the pier end (two 1'-5 rails + gap) and the skewed Ramp G plan are not dimensioned; the I.M. 455 crash cushion has no geometry in the plans.");

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
var misc = Doc.Substructure(u2.Name).Misc;
if (misc != null)
{
    foreach (var g in misc.Marks.Where(m => m.Unit.StartsWith("Crash")).GroupBy(m => (m.Unit, ep: m.Coating == "stainless")))
        Print($"   {g.Key.Unit} {(g.Key.ep ? "stainless" : "epoxy")}: {g.Sum(m => m.TotalMass) / 0.45359237:F0} lb");
    foreach (var d in misc.Diagnostics.Where(d => d.Message.Contains("end post") || d.Message.Contains("cushion"))) Print($"   {d.Severity}: {d.Message}");
}
Doc.Save();
