// Cross girders under the discontinuous girders of WB I-80 Units 2 and 3 as designed: cross girder details, design sheet 121
// (PDF page 125 of the Design 116 plans). Re-runnable; run after build-i80-wb.csx and girders-i80.csx.
//  * Web / flanges: Unit 2 PL 9/16 x 80 web, PL 5/8 x 15 flanges; Unit 3 PL 3/4 x 60 web, PL 7/8 x 12 flanges. Flange-web fillet
//    welds 5/16, stiffener welds 1/4 (callouts). Both flanges LEVEL (the model keeps the chord between the two continuing girders).
//  * Vertical position (elevation views): top of the top flange is 11" (Unit 2) / 8" (Unit 3) BELOW the top of web (bottom of the
//    top flange) of the discontinuous girder (verified on a 600 dpi raster: the rotated "11" is two strokes, the next dimension is 8").
//  * Connection angles L5x5x1/2, 10 spaces @ 6 + 2" ends = 5'-4" long (Unit 2); 8 @ 6 + 2" ends = 4'-4" (Unit 3), centred in the web
//    (Unit 2: 8" clear above and below = 8 + 64 + 8 = 80; Unit 3: 4 + 52 + 4 = 60). Single bolt line 3" from the heel and 2" from
//    the toe in both legs (Sections B-B / F-F): two angles on the near and far faces at each continuing girder, one pair
//    straddling the discontinuous girder web (near face only).
//  * Opening 3'-0 x 2'-0, R = 6", centred between each continuing girder CL and the discontinuous girder CL ("EQ.", "CL HOLE"),
//    edge plates PL 5/8 x 6 far side with their CL 1'-10 (Unit 2) / 1'-0 (Unit 3) below the top of the cross girder and 1/2" off
//    the opening edge; vertical stiffeners PL 5/8 x 7 1/2 (Unit 2) / PL 1/2 x 6 (Unit 3) near side, 6" (CL to opening edge) each side.
//  * Plan angle between each discontinuous girder and its cross girder from the orientation plan (clockwise from the girder's
//    ahead tangent to the cross girder, left to right), see GirderTermination.CrossGirderAngleDegrees.
// UNCERTAIN / assumptions: bolt diameter is not printed on sheet 121 -> 1" in 1 1/16" holes as all I-80 structural bolts
//    (sheet 118 notes); edge plate length (stops 3" short of the angle toes, scaled from the elevation); the stiffener at the
//    exterior girder edge (PL 1/2 x 6 1/2) and the 7/8" wire rope hole belong to the girder stiffener and are not modeled here.
using SBM.Core.Bridge;

double Dms(int d, int m, double s) => d + m / 60.0 + s / 3600.0;
double I(double inch) => In(inch);
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");

void Common(CrossGirderSection x, bool unit2)
{
    x.WebDepth = I(unit2 ? 80 : 60); x.WebThickness = I(unit2 ? 0.5625 : 0.75);
    x.FlangeWidth = I(unit2 ? 15 : 12); x.FlangeThickness = I(unit2 ? 0.625 : 0.875);
    x.TopBelowDiscontinuousWebTop = I(unit2 ? 11 : 8);
    x.AngleLeg = I(5); x.AngleThickness = I(0.5);
    x.AngleLength = I(unit2 ? 64 : 52); x.EndAngleLength = I(unit2 ? 64 : 52);
    x.AngleBoltPitch = I(6); x.AngleBoltEndDistance = I(2); x.AngleBoltGage = I(3); x.BoltDiameter = I(1);
    x.OpeningWidth = I(24); x.OpeningHeight = I(36); x.OpeningRadius = I(6);
    x.OpeningPlateFromTop = I(unit2 ? 22 : 12); x.OpeningPlateToOpening = I(0.5);
    x.OpeningPlateWidth = I(6); x.OpeningPlateThickness = I(0.625); x.OpeningPlateEndClear = I(3);
    x.StiffenerWidth = I(unit2 ? 7.5 : 6); x.StiffenerThickness = I(unit2 ? 0.625 : 0.5); x.StiffenerFromOpening = I(6);
    x.WebFlangeWeld = I(5.0 / 16); x.StiffenerWeld = I(0.25);
}
Common(u2.Girders.CrossGirder, true);
Common(u3.Girders.CrossGirder, false);

void Angle(BridgeDefinition b, int girder, double deg)
{
    var t = b.Girders.Terminations.FirstOrDefault(q => q.Girder == girder);
    if (t == null) { Print($"  {b.Name}: no termination for girder {girder}"); return; }
    t.CrossGirderAngleDegrees = deg;
}
Angle(u2, 4, Dms(89, 16, 23));   // E
Angle(u2, 3, Dms(88, 32, 47));   // D
Angle(u2, 2, Dms(87, 40, 4));    // C
Angle(u2, 1, Dms(90, 0, 0));     // B
Angle(u3, 1, Dms(87, 6, 45));    // B
Angle(u3, 2, Dms(88, 0, 13));    // C

var r = Doc.Regenerate();
foreach (var b in new[] { u2, u3 })
{
    var md = Doc.Model(b.Name);
    foreach (var x in md.Framing.CrossGirders)
    {
        var asm = md.Steel!.Parts.Where(p => p.Assembly == x.Mark).ToList();
        Print($"{b.Name} {x.Mark}: {asm.Count} parts, {md.Steel.Bolts.Count(q => q.Assembly == x.Mark)} bolts, web {asm.First(p => p.Mark.EndsWith("-W")).Description}");
    }
    foreach (var d in md.Steel!.Diagnostics.Where(d => d.Message.Contains("XG-"))) Print("  " + d.Message);
}
Print($"Regenerated: {r.Parts:N0} parts, {r.Bolts:N0} bolts.");
Doc.Save();
