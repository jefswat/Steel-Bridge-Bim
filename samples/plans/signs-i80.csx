// I-80 WB sign supports (Design 116, design sheets 171A-176 = PDF 176, 178, 180-183): data in docs/plan-data/misc-items-data.md (b), misc/sign-bracket-dims.csv.
//  * Overhead truss brackets: Unit 2 at Pier 7WB (girders A and K, bracket height 8'-6, bottom flange 22", top of base 1026.33 N / 1028.33 S),
//    Unit 3 at Pier 13WB (girders A and J, 6'-4, 26", 1020.32 N / 1024.32 S). Post lines 7'-4 apart; ahead of CL pier: Unit 2 2'-2 and 9'-6;
//    Unit 3 girder J 2'-0 and 9'-4. Girder A is the NORTH girder = index 0 (verified from the model coordinates); K / J = the last girder.
//  * Bracket steel = modeled plates / stiffeners / 8 x 1 1/2" anchor bolts; base plate (2'-2 dia x 2") = placeholder (by others). Not modeled / unresolved
//    items are listed in Misc.Unresolved and raised as warnings.
//  * Exit sign bases (2, Ramp A gore at Pier 5WB and Ramp G gore at Pier 11WB, Unit 2): the plans show only a tick on the north girder for the station:
//    NOT placed (unresolved); the bar list (189 lb epoxy each) is scheduled.
// Re-runnable, after the I-80 build scripts.
using SBM.Core.Bridge;

double F(int ft, double inch) => Ft(ft) + In(inch);
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
foreach (var b in new[] { u2, u3 }) { b.Misc ??= new MiscItemsDefinition(); b.Misc.SignSupports.Clear(); b.Misc.Unresolved.RemoveAll(s => s.StartsWith("[sign]")); }

// Bracket = plate-girder cantilever OUTBOARD of the exterior girder (Section D-D / C-C PDF 178, 181): CL post `outb` from CL girder (W.P.) measured along the
// bracket, bracket at `angle` (deg) from the girder's ahead tangent toward the outboard side, square to the spreader beam, 2'-3 beyond the CL post;
// View J-J: top of base to the top of the bracket top flange = `stub` ("elevation line to level line", W = 2'-2 / 2'-0 post line, E = 9'-6 / 9'-4 post line),
// web depth = the girder web (8'-6 Unit 2, 6'-4 Unit 3).
SignBracketDefinition Br(int girder, double ahead, double topOfBaseFt, double height, double flange, double outb, double angle, double stub) =>
    new() { Girder = girder, AheadOfSupport = ahead, TopOfBaseElevation = Ft(topOfBaseFt), Height = height, BottomFlangeWidth = flange,
            PostOutboard = outb, AngleToGirderDegrees = angle, StubHeight = stub };
double Dms(int d, int m, int s) => d + m / 60.0 + s / 3600.0;

u2.Misc!.SignSupports.Add(new SignSupportDefinition
{
    Name = "Sign truss Unit 2 (Pier 7WB)", Kind = SignSupportKind.OverheadTrussBracket, Support = "Pier 7WB", TrussSpan = Ft(100),
    Brackets =
    {
        // girder A (north): D-D 6'-3 (W) / C-C 6'-7 7/16 (E), 92d52'02" (TYP.); J-J stub 1'-3 3/4 (W) / 1'-3 5/8 (E)
        Br(0, F(2, 2), 1026.33, F(8, 6), In(22), F(6, 3), Dms(92, 52, 2), F(1, 3.75)), Br(0, F(9, 6), 1026.33, F(8, 6), In(22), F(6, 7 + 7 / 16.0), Dms(92, 52, 2), F(1, 3.625)),
        // girder K (south): D-D 6'-1 3/4 (TYP.), 90d; stub 1'-0 3/8 (W) / 1'-0 (E)
        Br(u2.Girders.Count - 1, F(2, 2), 1028.33, F(8, 6), In(22), F(6, 1.75), 90, F(1, 0.375)), Br(u2.Girders.Count - 1, F(9, 6), 1028.33, F(8, 6), In(22), F(6, 1.75), 90, F(1, 0)),
    },
});
u2.Misc.Unresolved.Add("[sign] Unit 2 sign truss: truss CL parallel to the pier at STA 7595+82.22 (pier + 5'-10); sheets 171A/172A give the post lines 2'-2 and 9'-6 ahead of the CL pier (plan sheet 110 \"2'-2 (TYP.)\"); the sheet title-block station 7595+67.24 is the bridge reference, not a sign location.");

u3.Misc!.SignSupports.Add(new SignSupportDefinition
{
    Name = "Sign truss Unit 3 (Pier 13WB)", Kind = SignSupportKind.OverheadTrussBracket, Support = "Pier 13WB", TrussSpan = Ft(90),
    Brackets =
    {
        // girder J (south, last girder): 2'-0 and 9'-4 ahead of the pier. Girder A (north): the 12'-10 7/8 dimension starts from a reference line whose meaning is UNRESOLVED -> not placed.
        // girder J: revised Section D-D (PDF 181) 5'-0 3/8 at 89d49'28" (W) / 5'-0 1/4 at 90d (E); View J-J (PDF 180) stub 1'-0 1/4 (W) / 1'-0 3/4 (E)
        Br(u3.Girders.Count - 1, F(2, 0), 1024.32, F(6, 4), In(26), F(5, 0.375), Dms(89, 49, 28), F(1, 0.25)), Br(u3.Girders.Count - 1, F(9, 4), 1024.32, F(6, 4), In(26), F(5, 0.25), 90, F(1, 0.75)),
    },
});
u3.Misc.Unresolved.Add("[sign] Unit 3 sign truss: the two brackets on girder A (north, top of base 1020.32) are NOT placed - the plan dimension 12'-10 7/8 (I-80 PDF 117) starts from a vertical reference line whose meaning is unresolved; girder A section D-D (PDF 181) was revised (5'-4 5/8, 5'-9 1/2; angles 93d19'39\" / 93d00'30\", struck values illegible).");
u3.Misc.Unresolved.Add("[sign] Sign supports: anchor bolt lengths, the post stub plate lengths, the HSS 14.000x0.500 post height and the sign truss size / elevation are not in the plans.");

// exit sign bases (Unit 2: Ramp A gore at Pier 5WB, Ramp G gore at Pier 11WB), bars per base
List<MiscBarRow> ExitBars() => new()
{
    new() { Mark = "5x1", Size = "#5", Count = 2, Length = F(9, 6), Location = "base bottom" },
    new() { Mark = "5x2", Size = "#5", Count = 3, Length = F(17, 5), Location = "base hoops horizontal" },
    new() { Mark = "5x3", Size = "#5", Count = 4, Length = F(8, 2), Location = "base top" },
    new() { Mark = "5x4", Size = "#5", Count = 9, Length = F(8, 8), Location = "base hoops transverse vertical" },
};
foreach (var (name, sup) in new[] { ("Exit sign base Ramp A (Pier 5WB)", "Pier 5WB"), ("Exit sign base Ramp G (Pier 11WB)", "Pier 11WB") })
    u2.Misc.SignSupports.Add(new SignSupportDefinition { Name = name, Kind = SignSupportKind.ExitSignBase, Support = sup, Girder = 0, Bars = ExitBars() });
u2.Misc.Unresolved.Add("[sign] Exit sign bases: station not printed (sheets 104 / 108 show only a tick on the north girder; scaled about 50'-0 back of CL bearing Pier 5WB and about 60' back of Pier 11WB): not placed.");

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var b in new[] { u2, u3 })
{
    var misc = Doc.Substructure(b.Name).Misc;
    if (misc == null) { Print($"{b.Name}: no misc model"); continue; }
    Print($"{b.Name}: {misc.Assemblies.Count} assemblies, {misc.Elements.Count} elements, rebar marks {misc.Marks.Count}, {misc.RebarMass / 0.45359237:F0} lb");
    foreach (var d in misc.Diagnostics.Where(d => d.Message.Contains("ign") || d.Message.Contains("xit") || d.Message.Contains("CL post"))) Print($"   {d.Severity}: {d.Message}");
}
Doc.Save();
