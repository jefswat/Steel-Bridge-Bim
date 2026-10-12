// ODOT Vol 2 (Phase 2B, job piece 09033(11)) - Bridge 'O' spans 14-16: ONE steel tub girder unit (proof of concept), SUPERSTRUCTURE ONLY.
// Ramp 44F over I-44 & Ramps 44N / 44M2, 3 continuous spans 139'-3 3/8 + 161'-0 + 161'-0 along CL Ramp 44F (curve no. 9, R 881.47 LT),
// 2 steel tub girders (50" webs at 4:1, 77" bottom flange, 2 x 21" top flanges), 8" deck 32'-0 out to out.
// Builds the model FROM SCRATCH (re-runnable; replaces the alignment and bridge), from the repo root:
//   sbm new  "samples\plans\ODOT Bridge O tub unit.sbm"
//   sbm run  "samples\plans\build-odot-o-tub.csx" "samples\plans\ODOT Bridge O tub unit.sbm"
// Data: docs/plan-data/vol2-o-steel-tub-unit-spans14-16/ (SUMMARY.md, girder-plates-and-splices.csv, dead-load-deflections.csv) and the PDF
// "Vol 2_Phase 2B Bridge Combined.pdf" (O-1 p.254 ... O-39 p.293; fractions read on 230-300 dpi rasters). Check: check-odot-o-tub.csx.
// Rules (user): model the plans; nothing unstated is invented - UNRESOLVED items carry the PDF page and a plain reason; where the program has
// no input for a plan feature the line is marked "GAP". Plan precision 1/16" and 0.01 ft. No substructure (supports are bearing lines only).
using System.IO;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double Fr(double ft, double inch, double num, double den) => Ft(ft) + In(inch + num / den);   // 5'-10 5/8 = Fr(5, 10, 5, 8)
var unresolved = new List<string>();
var gaps = new List<string>();
void U(string pages, string item, string why, string then = "") => unresolved.Add(UnresolvedFlag.Text(pages, item, why, then));
void G(string s) => gaps.Add(s);

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "vol2-o-steel-tub-unit-spans14-16");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/vol2-o-steel-tub-unit-spans14-16 not found above the current directory - run from the repo.");
}
string dataDir = DataDir();

// ================================================================ 1. CL Ramp 44F (p.254 O-1, p.259 O-5)
// Curve no. 9: R 881.47, delta 77 deg 57'36.04" LT, L 1199.38; PC / PT not printed (U1). Working points (CL pier x CL ramp, p.259; pier 13 p.148):
var wp = new (string name, double sta, double n, double e)[]
{
    ("Pier 13", Sta("301+53.72"), 193142.24, 2113535.71),
    ("Pier 14", Sta("302+93.00"), 193275.14, 2113643.09),
    ("Pier 15", Sta("304+54.00"), 193434.63, 2113663.38),
    ("Pier 16", Sta("306+15.00"), 193595.16, 2113654.37),
    ("Abut 1 F.F. backwall", Sta("306+73.25"), 193652.45, 2113643.91),
};
Vec2 P(int i) => new(Ft(wp[i].e), Ft(wp[i].n));
// Circle through P13, P15, P16 (local grid X = E, Y = N; datum not printed, U2), checked against the plan radius and the other points.
Vec2 Circ(Vec2 a, Vec2 b, Vec2 c)
{
    double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
    double a2 = a.LengthSquared, b2 = b.LengthSquared, c2 = c.LengthSquared;
    return new Vec2((a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d, (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d);
}
var ctr = Circ(P(0), P(2), P(3));
double Rfit = (P(0) - ctr).Length;
Print($"Curve 9 fitted through the working points of piers 13 / 15 / 16: R {Rfit / Ft(1):F3} ft (plan 881.47)");
for (int i = 0; i < wp.Length; i++) Print($"  {wp[i].name}: radius {(P(i) - ctr).Length / Ft(1):F3} ft");
double R = Ft(881.47);
U("p.254", "PC / PT stations of curve no. 9 (U1)", "the general plan prints R, T, delta, D and L but no PC / PT station and the volume has no alignment sheet",
  "The alignment is the arc R 881.47 LT through the plan working points (centre fitted through piers 13 / 15 / 16), from 20 ft before pier 13 to the abutment-1 working point.");
U("p.259", "coordinate zone / datum (U2)", "the staking sheet gives N / E without naming the system", "Plan N / E used as a local grid in feet.");
// Arc: start 20 ft before the pier-13 working point (girder G1 begins about 5 ft before it on the skewed pier-13 line, the deck edge 8 ft).
double angP13 = Math.Atan2(P(0).Y - ctr.Y, P(0).X - ctr.X);
// LEFT curve: travel is counter-clockwise about the centre; tangent = radial rotated +90 deg.
double lead = Ft(20), arcLen = (wp[4].sta - wp[0].sta) + lead + Ft(10);
double ang0 = angP13 - lead / R;
var start = ctr + Vec2.FromAngle(ang0) * R;
const string AlName = "CL Ramp 44F";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
var al = new Alignment { Name = AlName, Description = "CL Ramp 44F = profile grade line (Bridge O, curve no. 9 arc; PC/PT not printed U1; local grid U2)" };
al.SetElements(new[] { new HorizontalElement(start, ang0 + Math.PI / 2, arcLen, 1 / R, 1 / R) });
// Station equation at pier 13 (p.254): 301+09.58 Ramp 44E = 301+53.72 Ramp 44F. Before it the stations are Ramp 44E.
al.StartStation = Sta("301+09.58") - lead;
al.StationEquations = new List<StationEquation> { new() { Distance = lead, StationBack = Sta("301+09.58"), StationAhead = Sta("301+53.72") } };
Doc.Project.Alignments.Add(al);
for (int i = 0; i < wp.Length; i++)
{
    var q = al.PointAt(wp[i].sta);
    Print($"  {wp[i].name} {Doc.Units.FormatStation(wp[i].sta)}: model N {q.Y / Ft(1):F2} E {q.X / Ft(1):F2} vs plan N {wp[i].n:F2} E {wp[i].e:F2} (dN {(q.Y / Ft(1) - wp[i].n):+0.00;-0.00} dE {(q.X / Ft(1) - wp[i].e):+0.00;-0.00} ft)");
}
// The other equations on p.254 relate Ramp 44F to other ramps (304+77.01 44F = 303+77.56 44N; 305+52.42 44F = 304+00.30 44M2); 44F stationing is
// continuous through them (pier stations 302+93 / 304+54 / 306+15 differ by the span lengths), so they are data only.
Print("Other ramps (p.254): 304+77.01 Ramp 44F = 303+77.56 Ramp 44N; 305+52.42 Ramp 44F = 304+00.30 Ramp 44M2 (relations to other alignments; 44F stationing continuous).");

// Profile (p.254): PVC 301+70.00 el 1134.85, PVI 302+70.00 el 1131.92, PVT 303+70.00 el 1131.35, VC 200', -2.93 % / -0.57 %.
// The profile is evaluated by STATION; the 20 ft lead-in carries Ramp 44E stations (< 301+09.58), so the back tangent is entered twice: on 44E
// stations up to the equation and on 44F stations from it (same physical grade line; the 44E / 44F station gap is never evaluated).
double zEq = 1131.92 + 0.0293 * (302.70 - 301.5372) * 100;   // back tangent at 301+53.72 44F (DERIVED from the PVI and the -2.93 % grade)
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(al.StartStation, Ft(zEq + 0.0293 * lead / Ft(1))),
        new Vpi(Sta("301+09.58"), Ft(zEq)),
        new Vpi(Sta("301+53.72"), Ft(zEq)),
        new Vpi(Sta("302+70.00"), Ft(1131.92), Ft(200)),
        new Vpi(Sta("306+83.25"), Ft(1131.92 - 0.0057 * (306.8325 - 302.70) * 100)),
    },
};
foreach (var (s, z) in new[] { ("301+70.00", 1134.85), ("303+70.00", 1131.35), ("301+53.72", 1135.33), ("302+93.00", 1132.14), ("304+54.00", 1130.87), ("306+15.00", 1129.96) })
    Print($"  profile {s}: model {al.Profile.ElevationAt(Sta(s)) / Ft(1):F2} plan {z:F2}");
// Cross slope (p.270 O-16): one plane 6 % across the deck, falling to the LEFT (inside of the curve), pivot = profile grade at CL Ramp 44F.
al.CrossSlope = new CrossSlopeTable { PivotOffset = 0, Rows = { new CrossSlopeRow(al.StartStation, -0.06, 0.06) } };
U("p.254, p.270", "superelevation transition", "the deck sections show 6 % only; no superelevation diagram is in the volume", "6 % used over the whole unit.");

// ================================================================ 2. bridge, supports (bearing lines only), deck
const string BrName = "Bridge O spans 14-16 (steel tub unit)";
Doc.Project.Bridges.RemoveAll(x => x.Name == BrName);
var b = new BridgeDefinition { Name = BrName, AlignmentName = AlName, Structure = "Bridge O (Ramp 44F over I-44 & Ramps 44N, 44M2)" };
b.FieldSplices.Clear();
SupportDefinition Sup(string name, double sta, double skew, double endExt, BearingFixity fix) => new()
{
    Name = name, Type = SupportType.Pier, Station = sta, SkewDegrees = skew, GirderEndExtension = endExt, Fixity = fix, DeckEndOffset = Ft(1),
    Substructure = new SubstructureDefinition { Enabled = false },   // SUPERSTRUCTURE ONLY (user scope): no piers / footings
};
// Pier 13 (p.273 O-19): end diaphragm lines 69 deg 02'38" to G1 and 69 deg 18'54" to G2; CL brg 1'-0 15/16 (G1) / 1'-0 7/8 (G2) from CL pier along
// the girders = 1'-0 normal to the pier (12 15/16 x sin 69.04 = 12.08", 12 7/8 x sin 69.31 = 12.04"). Skew solved so G1 meets the line at 69 deg 02'38".
double skew13 = 0;
{
    double targetG1 = 69 + 2 / 60.0 + 38 / 3600.0, offG1 = -F(13, 4);
    double AngleAt(double skewDeg, double off)
    {
        double sta = Sta("301+53.72");
        double d0 = al.StationToDistance(sta);
        var a = al.PointAtDistance(d0);
        double th = al.EvaluateAtDistance(d0).angle;
        var dir = Vec2.FromAngle(th).Right.Rotate(-skewDeg * Math.PI / 180);   // radial (left -> right) rotated so the right end moves ahead
        // girder G at offset 'off': circle radius R - off... intersect line a + dir*u with circle |p - ctr| = R + (-off) for a LEFT curve
        double rg = R + (-off) * -1;   // left offset (negative) -> smaller radius
        rg = R + off;
        // solve |a + dir u - ctr| = rg
        var w = a - ctr; double B2 = 2 * Vec2.Dot(w, dir), C = w.LengthSquared - rg * rg;
        double disc = B2 * B2 - 4 * C; double u1 = (-B2 + Math.Sqrt(disc)) / 2, u2 = (-B2 - Math.Sqrt(disc)) / 2;
        double u = Math.Abs(u1) < Math.Abs(u2) ? u1 : u2;
        var p = a + dir * u;
        var tan = (p - ctr).Normalized().Left;   // CCW travel
        return Math.Acos(Math.Abs(Vec2.Dot(tan, dir))) * 180 / Math.PI;
    }
    double lo = 0, hi = 40;
    for (int it = 0; it < 80; it++) { double m = (lo + hi) / 2; if (AngleAt(m, offG1) > targetG1) lo = m; else hi = m; }
    skew13 = (lo + hi) / 2;
    double g2 = AngleAt(skew13, F(4, 4));
    Print($"Pier 13 skew {skew13:F4} deg: G1 angle {AngleAt(skew13, offG1):F4} deg (plan 69.0439), G2 angle {g2:F4} deg (plan {69 + 18 / 60.0 + 54 / 3600.0:F4}, difference {(g2 - (69 + 18 / 60.0 + 54 / 3600.0)) * 3600:+0;-0}\")");
}
double sta13 = Sta("301+53.72") + Ft(1) / Math.Cos(skew13 * Math.PI / 180);   // CL brg 1'-0 normal to CL pier 13, ahead
// Bearing marks (p.293 O-39): P13 / P16 G1 (guided expansion), P14 F1 (fixed), P15 G2 (guided expansion).
b.Supports.Add(Sup("Pier 13", sta13, skew13, Fr(0, 9, 11, 16), BearingFixity.GuidedExpansion));
b.Supports.Add(Sup("Pier 14", Sta("302+93.00"), 0, 0, BearingFixity.Fixed));
b.Supports.Add(Sup("Pier 15", Sta("304+54.00"), 0, 0, BearingFixity.GuidedExpansion));
b.Supports.Add(Sup("Pier 16", Sta("306+14.00"), 0, In(9), BearingFixity.GuidedExpansion));   // CL brg 1'-0 back of CL pier 16 (p.273), girder end 9" (p.278)
Print($"Pier 13 CL brg station {Doc.Units.FormatStation(sta13)} (CL pier 301+53.72 + 1'-0 / cos skew); Pier 16 CL brg 306+14.00 (CL pier 306+15.00 - 1'-0).");
U("p.273", "deck ends at piers 13 and 16", "the unit's slab continues into Bridge N (pier 13) and span 17 (pier 16); the joint / continuity details there are outside this unit",
  "The modeled deck stops at CL pier 13 and CL pier 16 (DeckEndOffset 1'-0 from CL brg) - a model boundary, not a joint.");

// Girders (p.273 O-19, p.270 O-16): 2 tubs 17'-8 apart (radial), G2 4'-4 right of CL Ramp 44F (radial), G1 13'-4 left; concentric:
// G1 R 868'-1 11/16, CL R 881'-5 11/16, G2 R 885'-9 11/16.
b.Girders.Count = 2;
b.Girders.FirstGirderOffset = -F(13, 4);
b.Girders.Spacings = new List<double> { F(17, 8) };
b.Girders.PlanLayout = GirderPlanLayout.CurvedParallel;
Print($"Girder radii: G1 {(R - F(13, 4)) / Ft(1):F4} ft (plan 868'-1 11/16 = {868 + 1.6875 / 12:F4}), G2 {(R + F(4, 4)) / Ft(1):F4} ft (plan 885'-9 11/16 = {885 + 9.6875 / 12:F4})");

// Deck (p.270 O-16, p.271 O-17): 8" slab, 32'-0 o-o = 1'-6 parapet + 4'-0 shoulder + 15'-0 lane | CL Ramp 44F | 10'-0 shoulder + 1'-6 parapet;
// haunch 4 1/2" top of web (working point) to bottom of slab at the girder CL (typical section, p.273); 'H' check below.
b.Deck.LeftEdgeOffset = -F(20, 6);
b.Deck.RightEdgeOffset = F(11, 6);
b.Deck.Thickness = In(8);
b.Deck.OverhangThickness = In(8);
b.Deck.Haunch = In(4.5);
b.Deck.StayInPlaceForms = true;                       // 5 psf steel stay-in-place forms (p.254 design data)
b.Deck.Barrier.Type = BarrierType.FShape;             // 42" F-shaped parapet, std FSHP-42 (p.270)
b.Deck.Barrier.Height = In(42);
b.Deck.Barrier.BaseWidth = F(1, 6);
b.Deck.Barrier.Reinforcement = false;
U("p.270", "F-shaped parapet FSHP-42 section (top width, face breaks)", "the standard drawing FSHP-42 is not in the volume", "Program F-shape with 42\" height and 1'-6 base; other dimensions = program defaults.");
b.Deck.Reinforcement.Enabled = false;
G("Deck reinforcing: the plan bar list (bar-lists.csv, p.271-272) is not entered for this proof of concept; generic deck bars would be invented -> off.");
b.Studs.Diameter = In(0.875);                         // 3 - 7/8" x 7" studs (p.290 detail, p.278/279 spacing rows)
b.Bolts.Diameter = In(1);                             // 1" A325 type 3 with DTIs, holes 1 1/16" (p.281 notes 3, 4)
b.Bolts.Grade = "A325 Type 3";
b.FieldSpliceGap = In(0.25);                          // 1/4" max gap between girder sections (p.281)

// ================================================================ 3. tub section and plates (p.273, p.278 O-24, p.279 O-25)
var sec = b.Girders.Section;
sec.WebDepth = In(50);                                // 4'-2" vertical, top of web to top of bottom flange (p.273)
sec.WebThickness = In(0.625);
sec.TopFlange = new PlateSize(In(21), In(1.25));
sec.BottomFlange = new PlateSize(In(77), In(1.25));
var tub = new TubGirderDefinition
{
    TopWebSpacing = F(8, 0), WebSlope = 0.25, RotateWithDeck = true, PlanWebSlopeLength = Fr(4, 3, 9, 16),
    PlanSteelWeightLb = double.NaN,
};
U("p.254-297", "structural steel plan quantity", "no estimate of quantities / steel weight for the tub unit was found in the volume", "Steel weight is reported without a plan comparison.");
// Plate runs: (plate, end support or splice, offset along the girder from the CL bearing, width, thickness); last run of each plate to the girder end.
PlateRun Run(GirderPlate p, double w, double t, int sup = -1, double off = 0, int splice = 0) =>
    new() { Plate = p, Width = w, Thickness = t, EndSupport = splice > 0 ? -1 : sup, EndOffset = off, EndSplice = splice };
double W21 = In(21), W77 = In(77);
// Girder 1 (p.278): top / bottom flange changes at P14 - 15'-1 5/8 (= FS1 + 20'-4 1/2), P14 + 13'-11 15/16, P15 - 14'-0 5/16, P15 + 14'-8 15/16;
// top FS2..FS3 2 PL 1 1/4, bottom PL 1 3/8; webs 5/8, 3/4 from P15 - 14'-0 5/16 to P15 + 14'-8 15/16.
sec.Layouts.Add(new GirderPlateLayout
{
    Girders = { 0 },
    Runs =
    {
        Run(GirderPlate.TopFlange, W21, In(1.25), 1, -Fr(15, 1, 5, 8)), Run(GirderPlate.TopFlange, W21, In(1.875), 1, Fr(13, 11, 15, 16)),
        Run(GirderPlate.TopFlange, W21, In(1.25), splice: 3), Run(GirderPlate.TopFlange, W21, In(1.375), 2, -Fr(14, 0, 5, 16)),
        Run(GirderPlate.TopFlange, W21, In(2.25), 2, Fr(14, 8, 15, 16)), Run(GirderPlate.TopFlange, W21, In(1.375)),
        Run(GirderPlate.BottomFlange, W77, In(1.25), 1, -Fr(15, 1, 5, 8)), Run(GirderPlate.BottomFlange, W77, In(1.875), 1, Fr(13, 11, 15, 16)),
        Run(GirderPlate.BottomFlange, W77, In(1.375), 2, -Fr(14, 0, 5, 16)), Run(GirderPlate.BottomFlange, W77, In(2.25), 2, Fr(14, 8, 15, 16)),
        Run(GirderPlate.BottomFlange, W77, In(1.375)),
        Run(GirderPlate.Web, 0, In(0.625), 2, -Fr(14, 0, 5, 16)), Run(GirderPlate.Web, 0, In(0.75), 2, Fr(14, 8, 15, 16)), Run(GirderPlate.Web, 0, In(0.625)),
    },
});
// Girder 2 (p.279): changes at P14 - 15'-4 7/8 (= FS1 + 20'-9 15/16), P14 + 14'-3 9/16, P15 - 14'-3 9/16, P15 + 15'-1 1/2.
sec.Layouts.Add(new GirderPlateLayout
{
    Girders = { 1 },
    Runs =
    {
        Run(GirderPlate.TopFlange, W21, In(1.25), 1, -Fr(15, 4, 7, 8)), Run(GirderPlate.TopFlange, W21, In(1.875), 1, Fr(14, 3, 9, 16)),
        Run(GirderPlate.TopFlange, W21, In(1.25), splice: 3), Run(GirderPlate.TopFlange, W21, In(1.375), 2, -Fr(14, 3, 9, 16)),
        Run(GirderPlate.TopFlange, W21, In(2.25), 2, Fr(15, 1, 1, 2)), Run(GirderPlate.TopFlange, W21, In(1.375)),
        Run(GirderPlate.BottomFlange, W77, In(1.25), 1, -Fr(15, 4, 7, 8)), Run(GirderPlate.BottomFlange, W77, In(1.875), 1, Fr(14, 3, 9, 16)),
        Run(GirderPlate.BottomFlange, W77, In(1.375), 2, -Fr(14, 3, 9, 16)), Run(GirderPlate.BottomFlange, W77, In(2.25), 2, Fr(15, 1, 1, 2)),
        Run(GirderPlate.BottomFlange, W77, In(1.375)),
        Run(GirderPlate.Web, 0, In(0.625), 2, -Fr(14, 3, 9, 16)), Run(GirderPlate.Web, 0, In(0.75), 2, Fr(15, 1, 1, 2)), Run(GirderPlate.Web, 0, In(0.625)),
    },
});
U("p.278-279", "thickness transitions at shop splices", "the girder elevations show square steps; no taper detail is given", "Plates change square at the dimensioned points (no 1:2.5 taper modeled).");

// Field splices (p.278 / p.279; CL brg to CL splice along the girder, top working point). G1: FS1 = P13 + 105'-9 7/8, FS2 = P14 + 41'-11 7/8,
// FS3 = FS2 + 65'-2 3/4 = P14 + 107'-2 5/8 (= P15 - 51'-4 1/8, p.273), FS4 = P15 + 34'-3 1/16. G2: FS1 = P13 + 100'-11 1/2, FS2 = P14 + 42'-10 1/8,
// FS3 = P15 - 52'-4 3/4 (38'-1 3/16 + 14'-3 9/16, p.279; p.273 52'-4 3/4), FS4 = P15 + 34'-11 3/8 (15'-1 1/2 + 19'-9 7/8).
void Splice(int no, (int sup, double off) g1, (int sup, double off) g2) => b.FieldSplices.Add(new FieldSpliceDefinition
{
    Number = no,
    Girders = { new GirderSpliceLocation { Girder = 0, Support = g1.sup, Offset = g1.off }, new GirderSpliceLocation { Girder = 1, Support = g2.sup, Offset = g2.off } },
});
Splice(1, (0, Fr(105, 9, 7, 8)), (0, F(100, 11.5)));
Splice(2, (1, Fr(41, 11, 7, 8)), (1, Fr(42, 10, 1, 8)));
Splice(3, (1, Fr(107, 2, 5, 8)), (2, -F(52, 4.75)));
Splice(4, (2, Fr(34, 3, 1, 16)), (2, Fr(34, 11, 3, 8)));
// Splice details (p.281 O-27): top 1 PL 3/4 x 21 x 3'-2 1/2 + 2 PL 3/4 x 9 1/2; bolts 4 lines (2" edge, 5 1/2", 6" centre gap) x 6 rows each side
// @ 3", first 2 1/4" from CL splice; web 2 PL 5/8 x 22 1/2 x 3'-6 1/2, 3 columns @ 3 1/2" each side x 12 rows @ 3 1/2"; bottom 2 PL x 63 1/2 x
// 3'-0 1/2 (3/4 splice 1, 7/8 splices 2-4), 18 columns @ 3 1/2" x 5 rows @ 3 1/2" each side; filler PL 1/8 x 1'-9 x 1'-7 1/8 at splice 3 only.
tub.Splice = new TubSpliceDetail
{
    Splices =
    {
        new TubSpliceOverride { Number = 1, BottomPlateThickness = In(0.75) },
        new TubSpliceOverride { Number = 2, BottomPlateThickness = In(0.875) },
        new TubSpliceOverride { Number = 3, BottomPlateThickness = In(0.875), FillerThickness = In(0.125), FillerWidth = In(21), FillerLength = Fr(1, 7, 1, 8) },
        new TubSpliceOverride { Number = 4, BottomPlateThickness = In(0.875) },
    },
};
U("p.281", "web splice plate position on the web", "the section dimensions 6 1/2\" + 3'-6 1/2\" + 6 1/2\" (4'-7 1/2\") exceed the 4'-3 9/16\" web length on the slope",
  "Web splice plates centred on the web.");
U("p.281", "bottom splice plate edge dimension 8 3/4\"", "(77\" - 63 1/2\") / 2 = 6 3/4\", not 8 3/4\"", "Plates centred on the tub CL.");

// ================================================================ 4. bracing (p.273 O-19, p.282 O-28, p.289 O-35)
// Panels: G1 span 14 14 eq. spa (141'-3 15/16), G2 span 14 2 eq. spa = 13'-7 11/16 at pier 13 + 12 eq. spa (123'-6 5/8), spans 15 / 16 17 / 16 eq. spa
// (G1 158'-6 3/4 / 157'-6 7/8, G2 161'-9 1/2 / 160'-9 1/2). Solid panel points (even) = internal K frame + top strut, dashed (odd) = top strut.
tub.Panels.Add(new TubPanelSegment { Girder = 0, Span = 0, Spaces = 14 });
tub.Panels.Add(new TubPanelSegment { Girder = 1, Span = 0, Spaces = 2, Length = Fr(13, 7, 11, 16) });
tub.Panels.Add(new TubPanelSegment { Girder = 1, Span = 0, Spaces = 12 });
foreach (int g in new[] { 0, 1 })
{
    tub.Panels.Add(new TubPanelSegment { Girder = g, Span = 1, Spaces = 17 });
    tub.Panels.Add(new TubPanelSegment { Girder = g, Span = 2, Spaces = 16 });
}
tub.Internal = new TubInternalBracingDefinition
{
    CrossFrameEvery = 2, Diagonal = "L5x5x5/8", TopStrut = "WT6x29", StrutBelowWebTop = In(1), GussetThickness = In(0.75),
    Stiffener = new PlateSize(In(10), In(0.75)), StiffenerBottomGap = In(1.5), TabWidth = In(10), TabHeight = In(12), TabThickness = In(0.75),
    DiagonalBolts = 3, BoltPitch = In(3),
};
tub.Internal.Unresolved.Add(UnresolvedFlag.Text("p.282", "internal cross frame centre gusset outline", "Detail C shows the gusset PL 3/4 but dimensions only the bolt spacing", "Gusset drawn 1'-10 x 1'-2 below the strut."));
tub.Internal.Unresolved.Add(UnresolvedFlag.Text("p.282", "diagonal work points", "Detail B1 / C give bolt spacings but not the work lines", "Diagonals run from the web CL at the top of the bottom flange to the top working point, 3 bolts at each end."));
U("p.273", "which panel points carry internal cross frames", "the framing plan distinguishes solid / dashed lines graphically only", "Even panel points (solid lines) = K frame + top strut, odd = top strut, as drawn.");
tub.Lateral = new TubLateralBracingDefinition { Member = "WT9x38", WorkPointFromPanelPoint = In(8), BoltsPerLine = 3, BoltPitch = In(3), BoltGage = In(4.0625) };
tub.Lateral.WorkPointFromSupport[0] = In(12);   // 1'-0 from CL support diaphragm at pier 13 (p.289)
U("p.289", "lateral bracing end bolts", "Detail A shows 6 holes per member end with 1 3/4\" / 3\" / 4 1/16\" dimensions", "2 lines x 3 bolts @ 3\" near each end modeled; bolts off the flange are dropped.");
// External intermediate diaphragms (p.273 bold radial lines, p.282): G1 points 4, 10 (span 14), 6, 12 (span 15), 6, 10 (span 16).
tub.External = new TubExternalDiaphragmDefinition
{
    ReferenceGirder = 0,
    Points = { new(0, 4), new(0, 10), new(1, 6), new(1, 12), new(2, 6), new(2, 10) },
    WebThickness = In(0.625), WebDepth = F(3, 2), WebTopBelowWebTop = In(6), Flange = new PlateSize(In(12), In(0.75)),
    Angle = "L8x8x3/4", AngleLength = In(32), GirderLegBolts = 8, DiaphragmLegBolts = 9, BoltPitch = In(3.5), EndClearance = In(0.5),
};
// Support diaphragms (p.283-288 O-29..O-34).
tub.SupportDiaphragms.Add(new TubSupportDiaphragmDefinition
{
    Support = 0, InteriorWebThickness = In(0.75), InteriorTopFlange = new PlateSize(In(12), In(1)), ExteriorWebThickness = In(0.75), ExteriorFlange = new PlateSize(In(12), In(1)),
    BearingStiffener = new PlateSize(In(8), In(1)), BearingStiffenerSpacing = F(1, 6), Connection = TubDiaphragmConnection.BentPlates, ConnectionShape = "L8x8x1/2",
    ConnectionLength = In(35.5), GirderLegBolts = 9, DiaphragmLegBolts = 10, BoltPitch = In(3.5),
    Unresolved = { UnresolvedFlag.Text("p.284", "exterior diaphragm height 2'-11 1/2\" and its position", "the dimension does not reconcile with the 4'-2\" tub depth on the sheet", "Exterior diaphragm built full depth (top flange at the top-of-web line, bottom flange on the bottom-flange top)."),
                   UnresolvedFlag.Text("p.283-284", "top / bottom connection plates PL 1 x 12", "lengths and bolt patterns only partly dimensioned", "Not built.") },
});
foreach (int j in new[] { 1, 2 })
    tub.SupportDiaphragms.Add(new TubSupportDiaphragmDefinition
    {
        Support = j, InteriorWebThickness = In(1.5), InteriorTopFlange = new PlateSize(In(12), In(1.5)), ExteriorWebThickness = In(0.75), ExteriorFlange = new PlateSize(In(12), In(1)),
        BearingStiffener = new PlateSize(In(8), In(1.25)), BearingStiffenerSpacing = F(2, 0),
        AccessOpeningWidth = F(1, 6), AccessOpeningHeight = F(2, 8), AccessOpeningRadius = In(8), AccessOpeningBottom = In(11),
        Connection = TubDiaphragmConnection.Angles, ConnectionShape = "L8x8x1/2", ConnectionLength = In(42.5), GirderLegBolts = 11, DiaphragmLegBolts = 12, BoltPitch = In(3.5),
        Unresolved = { UnresolvedFlag.Text("p.287-288", "top / bottom connection plates PL 1 x 12", "lengths and bolt patterns only partly dimensioned", "Not built.") },
    });
tub.SupportDiaphragms.Add(new TubSupportDiaphragmDefinition
{
    Support = 3, InteriorWebThickness = In(0.75), InteriorTopFlange = new PlateSize(In(12), In(1)), ExteriorWebThickness = In(0.75), ExteriorFlange = new PlateSize(In(12), In(1)),
    BearingStiffener = new PlateSize(In(8), In(1)), BearingStiffenerSpacing = F(1, 6), Connection = TubDiaphragmConnection.Angles, ConnectionShape = "L8x8x1/2",
    ConnectionLength = In(35.5), GirderLegBolts = 9, DiaphragmLegBolts = 10, BoltPitch = In(3.5),
    Unresolved = { UnresolvedFlag.Text("p.285-286", "top / bottom connection plates PL 1 x 12", "lengths and bolt patterns only partly dimensioned", "Not built.") },
});
G("Support diaphragm details not modeled: top/bottom connection plates, corner clips, rodent screens / expanded metal (p.290), drip plates (p.290), drain holes (p.290), flange-to-web and stiffener fillet welds of the tubs, transverse stiffener ticks in the end bays (p.273, not dimensioned).");

// ================================================================ 5. bearings (p.292 O-38, p.293 O-39) and access hatches (p.273, p.290)
TubBearingDefinition Brg(int sup, string mark, double B, double C, int D, int Fn, double H, double K, double L, double M, double N, double planH, params double[] rows) => new()
{
    Support = sup, Mark = mark, PadLength = In(B), PadWidth = In(C), PadHeight = In(H), InteriorLayers = D, InteriorLayer = In(0.5), Laminates = Fn, ExteriorLayer = In(0.25),
    PlateThickness = In(2.5), PlateWidth = In(K), PlateLength = In(L), PlanTransverseSlope = M / 100, PlanLongitudinalSlope = N / 100, PlanH = planH,
    PlanHorizontalOffset = In(3), AnchorDiameter = In(2), AnchorAcross = F(1, 9.5), AnchorEmbedment = F(1, 3), AnchorRows = rows.Select(r => In(r)).ToList(),
};
tub.Bearings.Add(Brg(0, "P13 (G1)", 13, 22, 5, 6, 3.75, 23, 14, 4.52, -4.92, F(5, 10), 9));
tub.Bearings.Add(Brg(1, "P14 (F1)", 20, 30, 5, 6, 3.75, 31, 21, 6.00, -1.48, Fr(5, 10, 5, 8), -9, 9));
tub.Bearings.Add(Brg(2, "P15 (G2)", 22, 30, 5, 6, 3.75, 31, 23, 6.00, -0.57, F(5, 11), -9, 9));
tub.Bearings.Add(Brg(3, "P16 (G1)", 15, 23, 7, 8, 5.0, 24, 16, 6.00, -0.57, Fr(5, 11, 3, 8), -9));
U("p.292", "anchor bolt rows at the end piers 13 / 16", "the expansion-pier plan (G1) shows one row 9\" from the CL brg; the row side at each end is not labeled", "Row placed 9\" from the CL brg on the span side.");
G("Bearing contact plates (4x4 bottom contact PL at fixed, 8x8x1/2 top contact PL with 2 1/2 x 6 slots at expansion, p.292) and guide details not modeled; pads are single solids (laminates listed in the description).");
foreach (int g in new[] { 0, 1 })
{
    tub.Hatches.Add(new TubAccessHatchDefinition { Girder = g, At = TubLocation.AtSupport(0, F(4, 3)) });
    tub.Hatches.Add(new TubAccessHatchDefinition { Girder = g, At = TubLocation.AtSupport(3, -F(4, 3)) });
}
U("p.273", "access hatch position reference", "4'-3\" is dimensioned from the CL hatch to the CL brg line drawn at the girder; the side (span) is from the drawing", "Hatches 4'-3\" into spans 14 and 16 on the tub CL.");

// ================================================================ 6. studs and tension zones (p.278 / p.279)
TubLocation S(int sup, double off = 0) => TubLocation.AtSupport(sup, off);
TubLocation FS(int no, double off = 0) => TubLocation.AtSplice(no, off);
double c10 = F(1, 10), c8 = In(8);
void Studs(int g, params (TubLocation a, TubLocation e, int n, double max)[] zones) { foreach (var z in zones) tub.StudZones.Add(new TubStudZone { Girder = g, From = z.a, To = z.e, Spaces = z.n, MaxPitch = In(z.max) }); }
Studs(0, (S(0, Ft(1)), FS(1, -c10), 124, 10), (FS(1, c10), S(1, -c8), 40, 10), (S(1, c8), FS(2, -c10), 48, 10), (FS(2, c10), FS(3, -c10), 62, 12),
         (FS(3, c10), S(2, -c8), 59, 10), (S(2, c8), FS(4, -c10), 39, 10), (FS(4, c10), S(3, -c8), 162, 9));
Studs(1, (S(0, Ft(1)), FS(1, -c10), 148, 8), (FS(1, c10), S(1, -c8), 45, 9), (S(1, c8), FS(2, -c10), 54, 9), (FS(2, c10), FS(3, -c10), 76, 10),
         (FS(3, c10), S(2, -c8), 67, 9), (S(2, c8), FS(4, -c10), 44, 9), (FS(4, c10), S(3, -c8), 186, 8));
U("p.278-279", "first stud at pier 13", "the row label reads \"STUD SPA ~ 1'-0\"\" with an unreadable leader", "First stud row 1'-0 from the CL brg (gives 9.97\" / 7.96\" spacing, within the 10\" / 8\" max).");
void Zone(int g, TubLocation a, TubLocation e, TubFlangeZone z) => tub.TensionZones.Add(new TubTensionZone { Girder = g, From = a, To = e, Flanges = z });
// G1: 85'-9 3/4 bottom, 20'-0 1/8 + 20'-4 1/2 both, 38'-5 7/16 top, 18'-8 1/16 both, 65'-2 3/4 both, 27'-11 7/8 both, 47'-11 1/4 top, 9'-8 1/16 + 19'-10 1/2 both, 103'-5 5/16 bottom.
Zone(0, S(0), S(0, Fr(85, 9, 3, 4)), TubFlangeZone.Bottom);
Zone(0, S(0, Fr(85, 9, 3, 4)), FS(1, F(20, 4.5)), TubFlangeZone.Both);
Zone(0, FS(1, F(20, 4.5)), FS(2, -Fr(18, 8, 1, 16)), TubFlangeZone.Top);
Zone(0, FS(2, -Fr(18, 8, 1, 16)), FS(3, Fr(27, 11, 7, 8)), TubFlangeZone.Both);
Zone(0, FS(3, Fr(27, 11, 7, 8)), FS(4, -Fr(9, 8, 1, 16)), TubFlangeZone.Top);
Zone(0, FS(4, -Fr(9, 8, 1, 16)), FS(4, F(19, 10.5)), TubFlangeZone.Both);
Zone(0, FS(4, F(19, 10.5)), S(3), TubFlangeZone.Bottom);
// G2: 80'-6 1/2 bottom, 20'-5 + 18'-3 1/2 both, top, 19'-0 5/8 both, 66'-6 5/8 both, 28'-6 3/4 both, 48'-10 7/8 top, 9'-10 7/16 + 20'-3 1/4 both, 105'-6 13/16 bottom.
Zone(1, S(0), S(0, F(80, 6.5)), TubFlangeZone.Bottom);
Zone(1, S(0, F(80, 6.5)), FS(1, F(18, 3.5)), TubFlangeZone.Both);
Zone(1, FS(1, F(18, 3.5)), FS(2, -Fr(19, 0, 5, 8)), TubFlangeZone.Top);
Zone(1, FS(2, -Fr(19, 0, 5, 8)), FS(3, F(28, 6.75)), TubFlangeZone.Both);
Zone(1, FS(3, F(28, 6.75)), FS(4, -Fr(9, 10, 7, 16)), TubFlangeZone.Top);
Zone(1, FS(4, -Fr(9, 10, 7, 16)), FS(4, F(20, 3.25)), TubFlangeZone.Both);
Zone(1, FS(4, F(20, 3.25)), S(3), TubFlangeZone.Bottom);
U("p.279", "G2 top tension zone over pier 14", "it is dimensioned 39'-2 7/8\" but FS1 + 18'-3 1/2\" to FS2 - 19'-0 5/8\" is 41'-8 3/4\" (79'-0 7/8\" between the splices)",
  "Zone taken between the two BOTH zones (41'-8 3/4\").");

// ================================================================ 7. deck pour sequence (p.272 O-18): data only
foreach (var (sec_, a, e, note) in new[] { (1, "301+53.72", "302+58.00", "span 14 to 35'-0 before CL pier 14"), (3, "302+58.00", "303+28.00", "35'-0 + 35'-0 at pier 14"),
    (2, "303+28.00", "304+09.00", "span 15"), (3, "304+09.00", "304+89.00", "45'-0 + 35'-0 at pier 15"), (1, "304+89.00", "306+15.00", "span 16 (continues into span 17, not modeled)") })
    tub.DeckPours.Add(new DeckPourDefinition { Section = sec_, FromStation = Sta(a), ToStation = Sta(e), Note = note + "; joints parallel to CL pier, 48 h lapse" });

foreach (var u in unresolved) tub.Unresolved.Add(u);
b.Girders.Tub = tub;

// ================================================================ 8. camber data (p.280 O-26, dead-load-deflections.csv)
// Tenth points of each span (equal spacing between CL bearings) + the field splices. Point numbers: 1 + 10 x span + tenth; key points = CL bearings.
// The sheet gives deflections only (concrete = slab + 5 psf forms + parapet, steel) - the camber input is set to the TOTAL deflection (DERIVED: no
// separate camber table); FS rows are compared by the check script (not keyed here: the camber table locates points between key points only).
var rows = File.ReadAllLines(Path.Combine(dataDir, "dead-load-deflections.csv")).Skip(1).Where(l => l.Trim() != "").Select(l => l.Split(',')).ToList();
foreach (int g in new[] { 0, 1 })
{
    var cd = new GirderCamberDefinition { Girder = g, DatumPoint = 0 };
    for (int j = 0; j < 4; j++) cd.Keys.Add(new CamberKeyPoint { Number = 1 + 10 * j, Support = j });
    foreach (var r in rows.Where(r => r[0] == (g + 1).ToString()))
    {
        string pt = r[1];
        if (pt.StartsWith("FS")) continue;
        int no;
        if (pt.StartsWith("PIER")) no = 1 + 10 * (int.Parse(pt[4..]) - 13);
        else { var parts = pt.Split('.'); no = 1 + 10 * int.Parse(parts[0]) + int.Parse(parts[1]); }
        double conc = double.Parse(r[2]), steel = double.Parse(r[3]), tot = double.Parse(r[4]);
        cd.Points.Add(new CamberPoint { Number = no, Camber = In(tot), SteelDeflection = In(steel), SlabDeflection = In(conc) });
    }
    b.Girders.Camber.Add(cd);
}

Doc.Project.Bridges.Add(b);

// ================================================================ regenerate, report, save
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridge(s), {res.Errors.Count} errors, {res.Warnings.Count} warnings.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var w in res.Warnings) Print("  WARN  " + w);
var st = Doc.Steel(BrName);
foreach (var d in st.Diagnostics.Where(d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Info)) Print("  INFO  " + d.Message);
Print($"Steel: {st.Parts.Count} parts, {st.Assemblies.Count} assemblies, {st.Bolts.Count} bolts, {st.Studs.Count} studs, {st.SteelMass / 0.45359237:N0} lb");
Print("GAPS (program features missing; closest input used):");
foreach (var s in gaps) Print("  GAP " + s);
Doc.Save();
Print($"Saved {Doc.FilePath}");
