// MnDOT Br. 02069 - T.H. 65 S.B. over 117th Ave (S.P. 0208-02069, certified plans 9/26/2025, 39 sheets; PDF page = sheet).
// Single span 99'-3" CL-CL bearing, seven MnDOT 30MH pretensioned beams @ 6'-0", 9" CIP slab 41'-0" o-o, 0 deg skew,
// pile-supported abutments behind MSE walls. Builds the model FROM SCRATCH (re-runnable; replaces the alignment and bridge):
//   sbm new  "samples\plans\Br02069 TH65.sbm"
//   sbm run  "samples\plans\build-br02069.csx" "samples\plans\Br02069 TH65.sbm"        (run from the repo root)
// Data: docs/plan-data/br02069-th65/SUMMARY.md + CSVs (strands-30MH-B1.csv, seat-elevations.csv, working-points.csv).
// Check: samples/plans/check-br02069.csx. Results / GAP list: docs/plan-data/br02069-th65/MODEL.md.
//
// Rules (user): model the plans; nothing unstated is invented - UNRESOLVED items are printed with their PDF page; where the
// program has no input for a plan feature the closest existing input is used and the line is marked "// GAP: ...".
// Plan precision 1/16" and 0.01 ft.
//
// HORIZONTAL DATUM (U1): the plans do not state it (p34/p36/p37). The plan X/Y (easting/northing, ft) are used as a LOCAL GRID.
// Feet: the project's default (international) foot is used for every value, plan coordinates included, so all plan
// distances and elevations are reproduced exactly; no geodetic meaning is attached to the coordinates.
using System.IO;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;

double F(double ft, double inch = 0) => Ft(ft) + In(inch);
var unresolved = new List<string>();
var gaps = new List<string>();
// UNRESOLVED = plan item not stated / not readable: always the PDF page(s) AND a short plain reason why (SBM.Core.UnresolvedFlag).
void U(string pages, string item, string why, string then = "") => unresolved.Add(UnresolvedFlag.Text(pages, item, why, then));
void G(string s) => gaps.Add(s);

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "br02069-th65");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/br02069-th65 not found above the current directory - run from the repo.");
}
string dataDir = DataDir();
List<string[]> Csv(string name) => File.ReadAllLines(Path.Combine(dataDir, name)).Skip(1).Where(l => l.Trim().Length > 0)
    .Select(l => l.Split(',').Select(x => x.Trim()).ToArray()).ToList();   // no field of these CSVs contains a comma (inch marks are not quotes)

// ================================================================ 1. SB65 alignment (p36 Alignment Tabulation 1, p1)
// Region R7 tangent: POT 208+26.273 R7 (pt 1074, X 508139.723, Y 149812.027) -> PC 261+56.990 R7 (pt 1075, X 508208.766,
// Y 155142.296), azimuth 0 deg 44'31.598". The last SB65 equation (208+26.909 R6 BK = 208+26.273 R7 AH, p36) is at the POT,
// so the whole bridge (248+32.92 .. 249+35.83) lies in region R7 on this tangent; the model alignment starts at that equation
// and carries R7 stations only (regions R1..R6 are off the bridge and not modeled). The next curve (HPI 264+63.237,
// R 90000, PC 261+56.990) starts beyond the bridge and is not needed.
const string AlName = "SB65";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
var al = Doc.AddAlignment(AlName, Sta("208+26.273"), new[]
{
    new PiRow(Ft(149812.027), Ft(508139.723)),
    new PiRow(Ft(155142.296), Ft(508208.766)),
});
al.Description = "SB65 working line, region R7 tangent (Br. 02069 p36); local grid X/Y - horizontal datum not stated (U1)";
U("p34, p36, p37", "horizontal datum (U1)", "the survey / alignment sheets give X / Y without naming the coordinate system", "Plan X / Y used as a local grid in feet.");
{
    double len = (al.PointAt(Sta("261+56.990")) - al.PointAt(Sta("208+26.273"))).Length;
    var pc = al.PointAt(Sta("261+56.990"));
    Print($"SB65 tangent: length {len / Ft(1):F3} ft (plan stations 5330.717), PC misfit dX {(pc.X - Ft(508208.766)) / Ft(1):+0.000;-0.000} dY {(pc.Y - Ft(155142.296)) / Ft(1):+0.000;-0.000} ft");
    double az = Math.Atan2(pc.X - Ft(508139.723), pc.Y - Ft(149812.027)) * 180 / Math.PI;
    int dd = (int)az; double mm = (az - dd) * 60; double ss = (mm - (int)mm) * 60;
    Print($"SB65 azimuth {dd} deg {(int)mm}' {ss:F3}\" (plan 0 deg 44' 31.598\")");
}
// Profile (p1, p34): crest VC, VPC 242+75.000 EL 920.329, VPI 249+00.000 EL 935.954, VPT 255+25.000 EL 920.329,
// G1 +2.500 %, G2 -2.500 %, L = 1250.000'. The begin / end points carry the plan grades to the alignment ends.
double vpi = Sta("249+00.000"), zVpi = Ft(935.954);
double s0 = al.StartStation, s1 = al.EndStation;
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(s0, zVpi - 0.025 * (vpi - s0)),
        new Vpi(vpi, zVpi, Ft(1250)),
        new Vpi(s1, zVpi - 0.025 * (s1 - vpi)),
    },
};
Print($"Profile: VPC 242+75 EL {al.Profile.ElevationAt(Sta("242+75")) / Ft(1):F3} (plan 920.329), VPT 255+25 EL {al.Profile.ElevationAt(Sta("255+25")) / Ft(1):F3} (plan 920.329)");
// Cross slope (p2, p19): crown at the working line, -0.02 ft/ft both sides, constant; no superelevation on the bridge.
al.CrossSlope = new CrossSlopeTable { PivotOffset = 0, Rows = { new CrossSlopeRow(s0, -0.02, -0.02) } };

// ================================================================ 2. bridge, supports, girders, deck (p1, p2, p3, p15)
const string BrName = "Br02069 TH65 SB";
Doc.Project.Bridges.RemoveAll(x => x.Name == BrName);
var b = new BridgeDefinition { Name = BrName, AlignmentName = AlName, Structure = "Br. 02069 (T.H. 65 S.B. over 117th Ave)" };
b.FieldSplices.Clear();
b.Studs.Enabled = false;
// Supports: CL brg S abut 248+34.75, N abut 249+34.00 (p1, p3, p15), skew 0 both; end of slab 1'-10" from CL brg (p1, p18).
// Bearings (p15, p31): 7 F1 fixed (B310) at the S abutment, 7 E1 expansion (B311) at the N abutment.
SupportDefinition Abut(string name, string sta, BearingFixity fix) => new()
{
    Name = name, Type = SupportType.Abutment, Station = Sta(sta), SkewDegrees = 0, DeckEndOffset = F(1, 10),
    GirderEndExtension = In(7.5), Fixity = fix,
};
b.Supports.Add(Abut("South Abutment", "248+34.75", BearingFixity.Fixed));
b.Supports.Add(Abut("North Abutment", "249+34.00", BearingFixity.Expansion));

// Girders: 7 lines, 6 @ 6'-0" = 36'-0", offsets -21, -15, -9, -3, +3, +9, +15 from the working line (+ = right / east) (p3, p15).
b.Girders.Count = 7;
b.Girders.FirstGirderOffset = -Ft(21);
b.Girders.Spacings = Enumerable.Repeat(Ft(6), 6).ToList();
b.Girders.Kind = GirderKind.TxDotPrestressed;

// Deck (p1, p2, p18, p19): 41'-0" out to out, edges -23.50 / +17.50 (1'-6" + 22'-0" W of WL, 16'-0" + 1'-6" E), 9" slab,
// stool (haunch) 4 1/4" at the CL bearings (p3 roadway-to-seat chain 9 + 4 1/4 + 30 + bearing).
b.Deck.LeftEdgeOffset = -Ft(23.5);
b.Deck.RightEdgeOffset = Ft(17.5);
b.Deck.Thickness = In(9);
b.Deck.OverhangThickness = In(9);
b.Deck.Haunch = In(4.25);
// GAP: no camber in the prestressed model (SUMMARY gap 10): the stool is constant 4 1/4" along the beam (plan value at CL brg only;
//      erection camber 7 3/8", residual 2 1/8" int / 2 1/4" ext, p16 - camber shape U10).
U("p16", "camber shape along the beam (U10)", "the sheet prints single camber / deflection values (erection 7 3/8\", residual 2 1/8\" / 2 1/4\"), no camber curve");
G("Prestressed camber / variable stool: the beams are straight, stool 4 1/4\" constant (plan value at CL brg only); camber 7 3/8\" erection, residual 2 1/8\"/2 1/4\" (p16, U10) not modeled. Needs: prestressed camber in the haunch (GirderLayoutDefinition.Camber for prestressed).");
// Deck reinforcing: the plan bar list (S401M-S404M..., p18/p19/p21) is not entered; the generic generator would invent bars -> off.
b.Deck.Reinforcement.Enabled = false;
// Deck reinforcing S401M..S404M: plan deck bars (DeckReinforcementDefinition.Plan) set by rebar-deck-br02069.csx (below); 84 + 84 + 137 + 137 = plan bill p21.

// Barriers (p1, p2, p24): MnDOT Concrete Barrier 36" Type S, 1'-6" wide at the base, both edges.
// GAP: only TxDOT SSTR / F-shape / single-slope profiles exist; Type S is modeled as the generic single slope (vertical back face,
//      sloped traffic face) 36" high x 1'-6" base. The top width and the face geometry of Fig. 5-397.138(A) are not read (p24) ->
//      the program default top width is kept and flagged.
b.Deck.Barrier.Type = BarrierType.SingleSlope;
b.Deck.Barrier.Height = In(36);
b.Deck.Barrier.BaseWidth = In(18);
b.Deck.Barrier.Reinforcement = false;            // R-bars (p24 bill) not modeled; SSTR bars would be invented
G($"Barrier: MnDOT Type S 36\" profile not in BarrierProfiles - modeled as SingleSlope 36\" x 1'-6\" base, top width = program default {b.Deck.Barrier.TopWidth / In(1):0.##}\" (not read, p24); barrier bars R501M..R612M (p24) not modeled. Needs: MnDOT Type S barrier profile + plan barrier bars.");
U("p24, Fig. 5-397.138(A)", "barrier Type S top width / traffic-face breaks", "the standard figure's dimensions have not been transcribed into the plan data yet", "Program default top width kept.");

// ================================================================ 3. prestressed beams (p16 30MH-101, Beam B1)
// MnDOT 30MH from the standard catalog (StandardBeamCatalog "MN 30MH", Fig. 5-397.501 mod. / p16): H 30", top flange 2'-10",
// bottom flange 3'-3", web 6 1/2", verticals top down 5", 1 5/8", 1'-0 7/16", 5 7/16", 5 1/2"; 6" R web fillets, 1" R / 2" R
// flange corners, 3/4" bottom chamfer (exact outline). The beam is the project beam 30MH-101 (drape removed, debonding added):
// it spans the layout bearings (99'-3" + 2 x 7 1/2" = 100'-6", p16), no standard length is entered, so no Placement is needed.
// Strands (strands-30MH-B1.csv, p16 END VIEW): 44 x 0.6" straight, 18 @ 2", 18 @ 4", 8 @ 6" (2" grid); debonded (sheathed) at
// EACH end: A 4 @ 9'-0", B 6 @ 13'-0", C 4 @ 17'-0" (14 of 44, p16).
double FtIn(string s)      // 13'-0" -> length
{
    var t = s.Replace("\"", "").Split('\'');
    return F(double.Parse(t[0]), double.Parse(t[1].TrimStart('-')));
}
var strands = new List<StrandPoint>();
foreach (var r in Csv("strands-30MH-B1.csv"))
{
    double y = In(double.Parse(r[3]));
    double db = r[4].Length > 0 ? FtIn(r[5]) : 0;
    strands.Add(new StrandPoint { X = In(double.Parse(r[2])), Y = y, YEnd = y, Harped = false, DebondStart = db, DebondEnd = db, DebondSymbol = r[4] });
}
foreach (var g in strands.Where(s => s.DebondSymbol != "").GroupBy(s => s.DebondSymbol).OrderBy(g => g.Key))
    Print($"Debond {g.Key}: {g.Count()} strands @ {g.First().DebondStart / Ft(1):F3} ft each end");
b.Girders.Prestressed = new PrestressedBeamDefinition
{
    Standard = "MN 30MH",
    Designation = "30MH-101",
    Source = "MnDOT Br. 02069 p16 (30MH-101, Beam B1)",
    StrandLayout = strands,                // 44 x 0.6" straight with debonding, strands-30MH-B1.csv
    StrandCount = strands.Count,
    StrandDiameter = In(0.6),
    StrandStrength = 300,                  // Gr 300 low relaxation (p16)
    ConcreteStrength = 9.5, ReleaseStrength = 8.0,   // f'c 9.5, f'ci 8.0 ksi (p16)
    BearingToEnd = In(7.5),                // 7 1/2" end of beam to CL brg (p16); 100'-6" o-o = 99'-3" + 2 x 7 1/2"
    // Bearings (p31): PadLength / PadWidth / PadThickness = fallback only; the per-END bearings below govern.
    PadLength = In(12), PadWidth = In(30), PadThickness = In(3.25),
    // Per beam END (p31, p15): S = start = F1 fixed (B310), N = end = E1 expansion (B311); height = plan bearing height L (p3 seat chain
    // 9 + 4 1/4 + 30 + L): F1 1/2" pad + 1 1/4" curved plate + 1 1/2" bearing plate = 3 1/4"; E1 3 3/4" pad + 1 1/4" + 1 1/2" = 6 1/2".
    // The bearing is modeled as one block 12" x 30" of height L (curved-plate assembly geometry: GAP). Sole plate B303 is cast flush with the
    // beam bottom (p30 "cast with beam"; not in the L chain) - not modeled (GAP 4).
    StartBearing = new PrestressedBearingDefinition { Mark = "F1", Length = In(12), Width = In(30), Thickness = In(3.25), Description = "MnDOT B310 curved-plate bearing, fixed, L 3 1/4\" (p31)" },
    EndBearing = new PrestressedBearingDefinition { Mark = "E1", Length = In(12), Width = In(30), Thickness = In(6.5), Description = "MnDOT B311 curved-plate bearing, expansion, L 6 1/2\" (p31)" },
};
Print($"Strands: {strands.Count} (plan 44), debonded {strands.Count(s => s.DebondStart > 0)} (plan 14)");
G("Bearings: MnDOT curved-plate bearings F1 (B310) / E1 (B311) (p31) modeled as one 12\" x 30\" block of the plan bearing height L (3 1/4\" S, 6 1/2\" N, PrestressedBeamDefinition.StartBearing / EndBearing); the pad / curved plate / bearing plate / pintles are not separate parts. Needs: a curved-plate bearing assembly.");
G("Sole plate B303 (p16, p30; dims U6) not modeled. Needs: sole plate for prestressed beams.");
// Concrete end diaphragms (p15, p17, p20, p21; U2 closed): SupportDefinition.Diaphragm, set by diaphragms-br02069.csx (below); front-part bottom UNRESOLVED (p.21).
// GAP 10 closed: beam bars G301E / G303E / G402E / G505E / G508E placed by beam-rebar-br02069.csx (PrestressedBeamDefinition.Reinforcement);
// G806E and the G303E paired with G402E stay UNRESOLVED (p16, no length / zone extent); optional 2 x 1/2" top strands not modeled (contractor's option).

// ================================================================ 4. abutments (p4, p5, p9, p10, p11)
// Section (p11 A-A, p4/p9): footing 6'-0" x 3'-0", bottom EL 917.75, back face 3'-3" behind CL brg, front 2'-9" ahead; stem 4'-0"
// (back toe 9", front toe 1'-3") = back ledge 1'-6" + seat 2'-6" (1'-0" behind CL brg, 1'-6" ahead). Back ledge S 922.35 / N 922.15
// (footing -> ledge 1'-7 1/4" / 1'-4 3/4"); seat front S 923.27 / N 923.06 (p5, p10), seat slopes 1" to the front; infill wall 1'-6"
// on the back ledge, top S 927.73 / N 927.80; pedestals 2'-4" along the beam on the seat (1'-0" behind + 1'-4" ahead of CL brg,
// flush with the seat back edge, 2" short of the seat front face; S Type A p6, N Type C p12).
// Mapping onto AbutmentDefinition with the MnDOT STEM (AbutmentDefinition.Stem, semi-integral abutment p11 A-A / p21 A-A):
//   footing            = plain 59'-7" x 6'-0" x 3'-0" block (back face 3'-3" behind CL brg, front 2'-9" ahead);
//   stem               = back face 2'-6" behind CL brg (= backwall back face), front face 1'-6" ahead; back ledge 1'-6" (x -2'-6" .. -1'-0") at
//                        footing top + 1'-7 1/4" S / 1'-4 3/4" N (p11 note 3); seat riser on the backwall front-face line (1'-0" behind CL brg),
//                        seat at the riser = ledge + 1'-0" (p11 A-A), at the front face = the printed seat front 923.27 / 923.06 (1" slope, p11);
//   backwall           = infill wall 1'-6" on the back ledge, ONLY in the E 18'-5" (WL +17'-7" .. +36'-0", p5 / p10 / p11 Detail C), one plain wall;
//                        under the superstructure the end diaphragm sits on the ledge (diaphragms-br02069.csx);
//   beam steps         = pedestals on the seat, 2'-4" (CL brg -1'-0" .. +1'-4") x plan size along the abutment;
//   mask wall          = W end only, 1'-5 1/2" BESIDE the footing (inner face on the footing W end line WL -23'-7", p5 / p3), from the footing
//                        bottom 917.75 to the printed top (p11 C-C: 12'-10 3/8" S / 12'-11" N).
G("Mask wall details (p11 C-C / D-D): the 5\" x 5\" toe notch with 2\" Type B polystyrene, the 2\" x 1'-1\" coping and the 4\" step of the outer 7\" at the back end are not modeled (plain 1'-5 1/2\" x 6'-0\" panel; net about +0.07 CY). Needs: maskwall section details.");
G("Stem construction-joint keyway 2\" x 12\" on the footing top (p11 A-A note 5) not modeled (volume-neutral).");
G("Test piles: one test pile per abutment longer (S 105 vs 95 ft, N 100 vs 90 ft, back row, p4/p9) - one PileLength per abutment; contract lengths used. Needs: per-pile length.");
// Abutment reinforcing A / B series (p6-p8, p12-p14): plan bar sets, rebar-abutments-br02069.csx (below); the Iowa abutment rebar generator stays off.
// U9 closed (SUMMARY sec. 4): mask wall at the W end only (p5 / p10, no mask wall at the E end, which ends flush at +36'-0" with keyways for the widening);
//    the infill wall is only in the E 18'-5" (+17'-7" .. +36'-0"); under the superstructure the end diaphragm sits on the back ledge.
U("p4, p9", "pile tip elevations (U8)", "the pile notes give only estimated lengths (95 / 90 ft, test piles 105 / 100 ft), no tip elevations", "Piles hang the contract length from the head.");
// U2 closed (SUMMARY sec. 3); the end diaphragm (back + front part, polystyrene) is modeled by diaphragms-br02069.csx; decision 14 closed by p.21 A-A / p.11 Detail A.
U("p8, p14, p21, p24", "bend dimensions of 22 bent marks and the barrier loop lengths (U3)", "one leg or hook of those marks is not dimensioned on the sheet, the barrier loops R505M / R517E / R506E / R507E / R518E do not add up to the listed length, and no sheet shows pin diameters", "No bars of these marks are modeled (reinforcement GAPs).");

var seatRows = Csv("seat-elevations.csv");
void SetAbutment(SupportDefinition sup, string side, double ledgeAboveFooting, double seatFront, double infillTop, double maskTop, double pedAlong, double pileLen, string pages)
{
    var ad = new AbutmentDefinition
    {
        Source = $"MnDOT Br. 02069 {sup.Name}, {pages}",
        LeftEndOffset = -F(23, 7), RightEndOffset = Ft(36),            // 59'-7" footing, -23.58 to +36.00 (future widening, p2/p5/p10)
        PlanFootingWidth = Ft(6),
        BackwallThickness = F(1, 6), BackwallFrontToBearing = Ft(1), FootingBackToBackwallBack = In(9), BearingToFootingFront = F(2, 9),
        FootingDepth = Ft(3),
        // MnDOT stem (p11 A-A): front face 1'-6" ahead of CL brg; back ledge = footing top + 1'-7 1/4" S / 1'-4 3/4" N (note 3); seat at the riser
        // = ledge + 1'-0"; seat front as printed (p5 / p10) -> 1" slope (p11). Riser = backwall front-face line 1'-0" behind CL brg.
        Stem = new AbutmentStemDefinition { FrontToBearing = F(1, 6), Source = "p11 A-A, notes 3 / 5; p21 A-A" },
        BearingToStepFront = F(1, 4),                                  // pedestal 2'-4" along the beam (CL brg -1'-0" .. +1'-4", 2" short of the seat front; p6 / p12, U7)
        StepLength = pedAlong,
        BackwallLeftOffset = F(17, 7),                                 // infill wall only in the E 18'-5": W end at WL +17'-7" (p5 / p10 / p11 Detail C)
        // the stem builds the infill wall as one plain wall (no corbel / pours); the corbel inputs are not used
        CorbelProjection = 0, CorbelVertical = 0, CorbelTaperRise = 0, LedgeBelowTop = F(1, 6), JointBelowTaper = 0,
        PileBackRowToBearing = F(1, 9), PileFrontRowToBearing = F(1, 3),  // 2 rows: 1'-9" behind, 1'-3" ahead of CL brg (p4, p9, p11)
        PileTopAboveFootingBottom = Ft(1),                              // 1'-0" embedment
        PileLength = Ft(pileLen),
    };
    ad.FootingBottom.Add(new CapProfilePoint(0, Ft(917.75)));
    ad.BackwallTop.Add(new CapProfilePoint(0, Ft(infillTop)));
    double ledge = Ft(917.75) + Ft(3) + ledgeAboveFooting;
    ad.Stem!.LedgeTop.Add(new CapProfilePoint(0, ledge));
    ad.Stem.SeatBackTop.Add(new CapProfilePoint(0, ledge + Ft(1)));
    ad.Stem.SeatFrontTop.Add(new CapProfilePoint(0, Ft(seatFront)));
    Print($"{sup.Name}: ledge {ledge / Ft(1):F3}, seat at riser {(ledge + Ft(1)) / Ft(1):F3}, seat front {seatFront:F2} (slope {(ledge + Ft(1) - Ft(seatFront)) / In(1):F2}\", plan 1\")");
    // West mask wall 1'-5 1/2" (p11 C-C) BESIDE the footing: inner face on the footing W end line (Setback = -thickness), bottom = footing bottom
    // 917.75, top as printed (height 12'-10 3/8" S / 12'-11" N, p11).
    ad.Masks.Add(new MaskwallDefinition { End = AbutmentEnd.Left, Thickness = In(17.5), Setback = -In(17.5), BottomElevation = Ft(917.75), Height = Ft(maskTop) - Ft(917.75) });
    // Piles: 7 per row @ 9'-5", 1'-6" from the W end (1'-7" E) (p4, p9). Skew 0 on a tangent: S along the line = alignment offset.
    for (int k = 0; k < 7; k++) ad.PileStations.Add(-F(23, 7) + F(1, 6) + k * F(9, 5));
    // Pedestals for the FUTURE beam lines (+21, +27, +33; S Type B p5 note 6 / p6, N Type D p10 note 6 / p12) are BUILT NOW - only the beams are
    // future: their bars are in this contract's abutment bar lists (p8 A527M 24 = 3 x 8, A528M 15 = 3 x 5; p14 B527M 18 = 3 x 6, B528M 12 = 3 x 4),
    // carried in the abutment quantities (p4 Cr-epoxy 480 lb = A508M + A613M + A527M + A528M; p9 390 lb) and p2 SOQ footnote 2 (Cr-epoxy bars
    // "include ... future abutment pedestal reinforcement"). Same size as the beam pedestals (p5 4'-8" / p10 3'-6" along the abutment, 2'-4" along
    // the beam, p6 / p12); centres at 6'-0" spacing from the dimension chains (p5: 3" W end + 10 x 4'-8" + 9 x 1'-4" + 8" E end = 59'-7";
    // p10: 10" + 10 x 3'-6" + 9 x 2'-6" + 1'-3"); tops from the elevation (seat-elevations.csv, p5 / p10).
    foreach (var row in seatRows.Where(r => r[0] == side && r[2].Contains("future")))
        ad.StandalonePedestals.Add(new AbutmentPedestalDefinition(Ft(double.Parse(row[3])), Ft(double.Parse(row[5])), "FUTURE BEAM"));
    // Pedestal (beam step) tops = plan pedestal elevations (seat-elevations.csv) for the 7 beam lines G1 (-21) .. G7 (+15).
    for (int g = 0; g < 7; g++)
    {
        double off = -21 + 6 * g;
        var row = seatRows.First(r => r[0] == side && Math.Abs(double.Parse(r[3]) - off) < 1e-6);
        ad.Steps.Add(new AbutmentStepDefinition(g + 1, Ft(double.Parse(row[5]))));
    }
    var sd = SubstructureDefinition.Default(SupportType.Abutment);
    sd.Abutment = ad;
    sd.PileType = PileType.CipPipe; sd.PileSize = In(12); sd.PileShellThickness = In(0.25);  // 12" C-I-P concrete piling (p4 / p9 pile notes: "nominal diameter of 12" (and a thickness of 1/4")"; splice B201 p29)
    sd.Reinforcement.Enabled = false;                                // GAP: abutment bars not entered
    sd.BackwallGap = Ft(1) - F(1, 10);                               // deck end 1'-10" vs infill face 1'-0": end diaphragm zone (no gap) -> report only
    sup.Substructure = sd;
}
// S: ledge = footing top + 1'-7 1/4" (922.354), seat front 923.27, infill 927.73, mask top 930.61, pedestals 4'-8" along the abutment, piles 95 ft (13 + test 105)
SetAbutment(b.Supports[0], "S", In(19.25), 923.27, 927.73, 930.61, F(4, 8), 95, "p4, p5, p11");
// N: ledge = footing top + 1'-4 3/4" (922.146), seat front 923.06, infill 927.80, mask top 930.67, pedestals 3'-6", piles 90 ft (13 + test 100)
SetAbutment(b.Supports[1], "N", In(16.75), 923.06, 927.80, 930.67, F(3, 6), 90, "p9, p10, p11");
// Deck end 1'-10" from CL brg = 10" behind the riser line (1'-0" behind CL brg): the slab end sits on the end diaphragm back part / paving bracket (semi-integral, p18 / p20 / p21).

Doc.Project.Bridges.Add(b);

// ---------------------------------------------------------------- concrete end / pier diaphragms + fillers / keyways (diaphragms-br02069.csx, run from the repo root)
{
    var dr = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\diaphragms-br02069.csx", Doc, Args);
    Print(dr.Output.TrimEnd());
    if (!dr.Success) Print("ERROR in diaphragms-br02069.csx: " + dr.Error);
}

// ---------------------------------------------------------------- abutment reinforcing (plan bars, rebar-abutments-br02069.csx)
{
    var ar = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\rebar-abutments-br02069.csx", Doc, Args);
    Print(ar.Output.TrimEnd());
    if (!ar.Success) Print("ERROR in rebar-abutments-br02069.csx: " + ar.Error);
    var dk = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\rebar-deck-br02069.csx", Doc, Args);
    Print(dk.Output.TrimEnd());
    if (!dk.Success) Print("ERROR in rebar-deck-br02069.csx: " + dk.Error);
    var dr2 = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\rebar-diaphragms-br02069.csx", Doc, Args);
    Print(dr2.Output.TrimEnd());
    if (!dr2.Success) Print("ERROR in rebar-diaphragms-br02069.csx: " + dr2.Error);
}

// ---------------------------------------------------------------- beam mild reinforcing from the beam standard (beam-rebar-br02069.csx)
{
    var br2 = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\beam-rebar-br02069.csx", Doc, Args);
    Print(br2.Output.TrimEnd());
    if (!br2.Success) Print("ERROR in beam-rebar-br02069.csx: " + br2.Error);
}
// ================================================================ regenerate, report, save
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridge(s), {res.Errors.Count} errors, {res.Warnings.Count} warnings.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var w in res.Warnings) Print("  WARN  " + w);
var fr = Doc.Framing(BrName);
var ps = Doc.Prestressed(BrName);
Print($"Beams: {ps.Beams.Count} x {ps.Shape.Name}, length {ps.Beams[0].Length / Ft(1):F3} ft (plan 100.500), bearing span {ps.Beams[0].BearingSpan / Ft(1):F3} ft (plan 99.250), area {ps.Shape.Area:F1} in2");
Print($"Deck area {fr.Deck!.Area / (Ft(1) * Ft(1)):F1} SF (plan SOQ 4220 SF)");
var sub = Doc.Substructure(BrName);
foreach (var u in sub.Units)
{
    Print($"== {u.Name}");
    foreach (var g in u.Solids.GroupBy(s => s.Kind)) Print($"  {g.Key}: {g.Count()} solids, {g.Sum(s => Math.Abs(s.Mesh.Volume())) / 0.764554857984:F1} CY");
    Print($"  piles: {u.Foundations.Sum(f => f.Piles.Count)}");
}
foreach (var d in sub.Diagnostics) Print($"  SUB {d.Severity}: {d.Message}");
Print("");
Print("UNRESOLVED (plan items not stated / not read):");
foreach (var s in unresolved) Print("  " + s);
Print("GAPS (program features missing; closest input used):");
foreach (var s in gaps) Print("  GAP " + s);
Doc.Save();
Print($"Saved {Doc.FilePath}");
