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
// agent review 2026-10-30 (qc/i80-rampg/curbs I02 / I03 / I04): the wedge corners ARE dimensioned on the top-of-slab sheets (I-80 CL offsets; WB CL = I-80 CL - 41.08 ft LT,
// as cross-slope-i80.csx): sheet 190 Detail A (PDF 197) Ramp A 7591+08.51 at 65.08' / 68.51' LT -> 7591+13.51 at 65.79' / 67.79' LT -> end 7591+16.01, parallel to the CL;
// sheet 191 Detail A (PDF 198) Ramp G 7603+15.03 at 77.08' / 81.08' LT -> 7603+20.20 at 77.79' / 79.79' LT -> +22.78 (asymmetric taper). The Ramp G post stands
// up-station of CL Pier 11WB on Unit 3's deck. The stations are read on the I-80 CL and taken on the WB alignment (radial lines, as the cross-slope rows).
double W(double i80LtFt) => -Ft(i80LtFt - 41.08);
// rail part (agent review I05): PDF 229 / 230 Section C-C = two 3'-8 barrier halves (1'-5 base: 3* toe, 5 x 7 lower slope, 3 1/2 x 2'-10 upper slope, 8 1/2 top) back to
// back, filled solid between; it runs from the end of slab (sheet 190 / 191 Detail A corners) to the wedge start; the joint recess in it is not built.
var railHalf = new List<EndPostRailPoint> { new(0, 0), new(0, In(3)), new(In(5), In(10)), new(In(8.5), In(44)) };
u2.Misc.EndPosts.Add(new CrashCushionEndPostDefinition { Name = "Crash cushion end post Ramp A (Pier 5WB)", Support = "Pier 5WB", Bars = ra, Source = "sheet 190 Detail A (PDF 197), end post PDF 229",
    TaperLength = Ft(5), PlanRows = new() { new(Sta("7591+08.51"), W(68.51), W(65.08)), new(Sta("7591+13.51"), W(67.79), W(65.79)), new(Sta("7591+16.01"), W(67.79), W(65.79)) },
    RailRows = new() { new(Sta("7591+06.74"), W(68.76), W(65.08), Sta("7591+06.76")), new(Sta("7591+08.51"), W(68.51), W(65.08)) }, RailHalfProfile = railHalf });
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
u3.Misc ??= new MiscItemsDefinition();
u3.Misc.EndPosts.RemoveAll(x => x.Name.Contains("Ramp G"));
u3.Misc.Unresolved.RemoveAll(s => s.StartsWith("[cushion]"));
u3.Misc.EndPosts.Add(new CrashCushionEndPostDefinition { Name = "Crash cushion end post Ramp G (Pier 11WB)", Support = "Pier 11WB", Bars = rg, Source = "sheet 191 Detail A (PDF 198), end post PDF 230",
    TaperLength = Ft(20.20 - 15.03), PlanRows = new() { new(Sta("7603+15.03"), W(81.08), W(77.08)), new(Sta("7603+20.20"), W(79.79), W(77.79)), new(Sta("7603+22.78"), W(79.79), W(77.79)) },
    RailRows = new() { new(Sta("7603+11.05"), W(81.59), W(77.08), Sta("7603+13.52")), new(Sta("7603+15.03"), W(81.08), W(77.08)) }, RailHalfProfile = railHalf });
string cushionFlag = "[cushion] UNRESOLVED (PDF 229 / 230 Section C-C, sheets 222 / 223): the rail part of the end post is built from the end of slab corners (sheets 190 / 191 Detail A) to the wedge start, but its toe height is marked maximum ('3*': may vary with construction) and the joint recess in it (sheet 203) is not built; its bars 5c4 / 6d1 / 6d4 / 5c5 are scheduled only; the I.M. 455 crash cushion unit has no geometry in the plans (user decision for its length).";
u2.Misc.Unresolved.Add(cushionFlag);
u3.Misc.Unresolved.Add(cushionFlag);

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var misc in new[] { Doc.Substructure(u2.Name).Misc, Doc.Substructure(u3.Name).Misc })
if (misc != null)
{
    foreach (var g in misc.Marks.Where(m => m.Unit.StartsWith("Crash")).GroupBy(m => (m.Unit, ep: m.Coating == "stainless")))
        Print($"   {g.Key.Unit} {(g.Key.ep ? "stainless" : "epoxy")}: {g.Sum(m => m.TotalMass) / 0.45359237:F0} lb");
    foreach (var d in misc.Diagnostics.Where(d => d.Message.Contains("end post") || d.Message.Contains("cushion"))) Print($"   {d.Severity}: {d.Message}");
    // plan quantity: PDF 229 / 230 'Concrete placement summary' = 2.4 CU. YDS. per end post (reported, never adjusted)
    foreach (var g in misc.Elements.Where(e => e.Kind == SBM.Core.Misc.MiscKind.EndPostConcrete).GroupBy(e => e.Assembly))
    {
        double cy = g.Sum(e => e.Mesh.Volume()) / Math.Pow(Ft(1), 3) / 27;
        Print($"   {g.Key}: concrete {cy:0.00} CY (wedge + rail part) vs plan 2.4 CY -> {(Math.Abs(cy - 2.4) <= 0.1 ? "PASS" : "DIFF (reported)")}");
    }
}
Doc.Save();
