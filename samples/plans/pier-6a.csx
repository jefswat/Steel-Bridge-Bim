// Pier 6A (Design 116, design sheets 37-40 and framing plan 106): a hammerhead beside Pier 5WB carrying the ahead
// bearings of Unit 2 girders A-E (Ramp A2 bridge, not part of this design, on its back side). Girders A-E begin on its
// CL bearing line; F-K start on Pier 5WB. Run after girders-i80.csx (re-runnable).
//  * CL Pier 6A crosses CL Ramp A2 at I-80 STA 7591+08.83, 107.43' LT; skewed 3 39'00" from CL Pier 5WB (north end
//    further ahead); CL bearing 1'-6 ahead of the CL pier. Ramp A2 CL is 13'-2 5/16 left (north) of the column CL.
//  * Ahead bearings A-E along the pier from the left top corner (x = -26'): 2'-5 9/16, then 11'-6 9/16, 11'-2 13/16,
//    11'-6 15/16, 9'-7 1/2.
//  * Cap 8'-0 wide, 52'-0 between top corners, left beak tip 1'-2 beyond the corner, right tip 1'-6 1/8; tips 5'-0 below
//    the top; soffit 1002.94 / 1003.31 at kinks 6" outside the 10'-0 column; top 1010.75 / 1012.51 on one half and
//    1011.50 / 1013.28 on the other (stepped; elevations at the face of cap; Unit 2 is on the lower half).
//  * Column 10'-0 x 6'-6 octagonal (3'-4 x 1'-9 chamfers). Footing 29'-0 x 20'-0 x 5'-6, bottom 968.62; 24 HP14x73 x 75 ft.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double F(double ft) => Ft(ft);
double FI(int ft, double inch) => Ft(ft + inch / 12.0);
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var al = Doc.Alignment(u2.AlignmentName);

// ---- the CL bearing line of Pier 6A
double skew = -(3 + 39 / 60.0);                                     // north (left) end further ahead
double sta5 = u2.Supports[0].Station;
var (_, ang5) = al.EvaluateAtDistance(al.StationToDistance(sta5));
var dir = Vec2.FromAngle(ang5).Right.Rotate(skew * Math.PI / 180);  // left -> right along the pier
var ahead = new Vec2(-dir.Y, dir.X); if (Vec2.Dot(ahead, Vec2.FromAngle(ang5)) < 0) ahead = -ahead;
var rampPt = al.PointAt(Sta("7591+08.83"), -(Ft(107.43) - Ft(41.08)));
var colPt = rampPt + dir * FI(13, 2.3125);                          // column CL on the CL pier
var brgPt = colPt + ahead * Ft(1.5);
// Where the CL bearing line crosses the WB CL (offset 0): secant on the line parameter.
double OffAt(double t) => al.Inverse(brgPt + dir * t).Offset;
double t0 = 0, t1 = Ft(80), f0 = OffAt(t0), f1 = OffAt(t1);
for (int it = 0; it < 30 && Math.Abs(f1) > 1e-7; it++) { double t2 = t1 - f1 * (t1 - t0) / (f1 - f0); t0 = t1; f0 = f1; t1 = t2; f1 = OffAt(t1); }
var crossPt = brgPt + dir * t1;
double staLine = al.Inverse(crossPt).Station;
Print($"  CL brg Pier 6A crosses the WB CL at STA {Doc.Units.FormatStation(staLine)} (Pier 5WB CL brg at {Doc.Units.FormatStation(sta5)}); column CL {t1 / Ft(1):F2} ft right... along the line from the crossing: {Vec2.Dot(colPt - crossPt, dir) / Ft(1):F2} ft");
double Sof(Vec2 p) => Vec2.Dot(p - crossPt, dir);                  // cap coordinate S on this support
double Splan(double x) => Sof(colPt) + F(x);                       // plan x (from the column CL, right +)

// ---- girders A-E begin on Pier 6A
u2.AuxiliarySupports.RemoveAll(s => s.Name == "Pier 6A");
u2.Girders.Beginnings.RemoveAll(b => b.Support == "Pier 6A");
var pier6a = new SupportDefinition
{
    Name = "Pier 6A", Type = SupportType.Pier, Station = staLine, SkewDegrees = skew, GirderEndExtension = In(12),
    PierCenterOffset = -Ft(1.5), Fixity = BearingFixity.Expansion,
};
u2.AuxiliarySupports.Add(pier6a);
for (int g = 0; g < 5; g++) u2.Girders.Beginnings.Add(new GirderBeginning { Girder = g, Support = "Pier 6A" });

// ---- bearings of A-E on their plan positions: iterate the girder offsets at Pier 5WB (support 0 of the unit)
double xA = -26 + FI(2, 5.5625) / Ft(1);
var xs = new[] { xA, xA + FI(11, 6.5625) / Ft(1), 0.0, 0.0, 0.0 };
xs[2] = xs[1] + FI(11, 2.8125) / Ft(1); xs[3] = xs[2] + FI(11, 6.9375) / Ft(1); xs[4] = xs[3] + FI(9, 7.5) / Ft(1);
var targets = xs.Select(x => al.Inverse(colPt + dir * F(x) + ahead * Ft(1.5)).Offset).ToArray();
for (int g = 0; g < 5; g++) Print($"  girder {(char)('A' + g)} at Pier 6A: plan x {xs[g]:F2} ft -> offset {targets[g] / Ft(1):F2} ft");
for (int it = 0; it < 6; it++)
{
    var r0 = Doc.Regenerate();
    if (r0.Errors.Count > 0) { foreach (var e in r0.Errors) Print("  ERROR " + e); break; }
    var line = Doc.Model(u2.Name).Framing.AuxSupports.First(s => s.Name == "Pier 6A");
    double worst = 0;
    for (int g = 0; g < 5; g++)
    {
        if (line.Bearings[g] is not { } br) { Print($"  girder {(char)('A' + g)}: no bearing on Pier 6A"); continue; }
        double err = targets[g] - br.Offset;
        worst = Math.Max(worst, Math.Abs(err));
        u2.Girders.OffsetsAtSupports![0][g] += err;
    }
    Print($"  pass {it + 1}: largest bearing offset error {worst / In(1):F2} in");
    if (worst < In(0.05)) break;
}

// ---- the as-designed pier
var sd = SubstructureDefinition.Default(SupportType.Pier);
double tL = 1010.75, tR = 1012.51, step = 1011.50 - 1010.75;
double m = (tR - tL) / 52.0;
double Top(double x) => tL + m * (x + 26);
List<CapProfilePoint> Prof(double lift) => new()
{
    new(Splan(-26), F(Top(-26) + lift)), new(Splan(26), F(Top(26) + lift)),
    new(Splan(26 + FI(1, 6.125) / Ft(1)), F(Top(26) - 5)), new(Splan(5.5), F(1003.31)),
    new(Splan(-5.5), F(1002.94)), new(Splan(-26 - FI(1, 2) / Ft(1)), F(Top(-26) - 5)),
};
// Unit 2 (ahead) bears on the LOWER half (1010.75 / 1012.51): with the gore's 3.4% slope its seats run parallel to that
// half at about 6"; the back half (Ramp A2 bridge) is 0.75 ft higher.
sd.CapProfile = Prof(step);
sd.CapProfileAhead = Prof(0);
sd.CapWidth = F(8);
sd.ColumnCount = 1; sd.FirstColumnOffset = Splan(0); sd.ColumnSpacings = new();
sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = F(10); sd.ColumnDepth = FI(6, 6);
sd.ColumnChamferS = FI(3, 4); sd.ColumnChamferN = FI(1, 9);
sd.Foundation = FoundationType.PileCap; sd.FootingThickness = FI(5, 6); sd.FoundationTopElevation = F(968.62 + 5.5);
sd.FootingLength = F(29); sd.FootingWidth = F(20);
sd.PileSection = "HP14x73"; sd.PileLength = F(75);
FootingPoint Q(double s, double n) => new(F(s), F(n));
var all6 = new[] { -12.5, -7.5, -2.5, 2.5, 7.5, 12.5 };
var piles = all6.Select(s => Q(s, 8)).Concat(new[] { -12.5, -7.5, 7.5, 12.5 }.Select(s => Q(s, 4)))
    .Concat(new[] { -12.5, -2.5, 2.5, 12.5 }.Select(s => Q(s, 0))).Concat(new[] { -12.5, -7.5, 7.5, 12.5 }.Select(s => Q(s, -4)))
    .Concat(all6.Select(s => Q(s, -8))).ToList();
sd.Footings = new() { new FootingDefinition { Column = 1, Piles = piles } };
sd.Footings[0].BatterPerimeter(4, false, false);   // plans: the 12 piles of both outer N rows (N = +-8) batter 1:4 outward in N
pier6a.Substructure = sd;

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
var md = Doc.Model(u2.Name);
foreach (var unit in md.Substructure!.Units.Where(x => x.Name is "Pier 6A" or "Pier 5WB"))
    Print($"  {unit.Name}: {unit.Pedestals.Count} pedestals, {unit.Pedestals.Min(p => p.Height) / In(1):F1} to {unit.Pedestals.Max(p => p.Height) / In(1):F1} in; {unit.Foundations.Sum(f => f.Piles.Count)} piles");
foreach (var d in md.Substructure.Diagnostics.Concat(md.Framing.Diagnostics).Where(d => d.Message.Contains("6A") || d.Message.Contains("5WB")))
    Print("  WARNING " + d.Message);
Doc.Save();
