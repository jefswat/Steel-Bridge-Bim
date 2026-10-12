// TxDOT CSJ 0047-07-228 (HNTB 2020) - US 75 pedestrian underpass at Northaven Trail: STEEL TIED ARCH, span 10 (CL pier 10 - CL pier 11).
// Adds (or replaces) the bridge "Arch span 10" with an ArchDefinition: local structure frame, 2 inclined W14x132 ribs, 2 curved T tie girders,
// 64 hangers (1" spiral strand, open strand sockets p.148), 33 floorbeams, W8x28 upper lateral bracing, 2 knuckles (simplified), deck on 3" x 8"
// steel deck forms with curbs, PT tendons (data), 4 elastomeric bearings on the pier 10 / 11 arch pedestals.
// Run AFTER build-nht.csx (approach model: alignment + piers 10 / 11):
//   sbm run samples\plans\arch-nht.csx "samples\plans\Northaven Trail.sbm" -- "samples\plans\Northaven Trail.sbm"
// Without an approach model (no support named "Pier 10" / "Pier 11") the script builds its own TEST alignment (curves 104-107 from p.18-19,
// TRAILNEW profile p.20, 1 % crown) and two supports:
//   sbm run samples\plans\arch-nht.csx -- temp\arch-test.sbm
// Plan data: docs/plan-data/nht-0047-07-228 (SUMMARY.md 3a / 8, CSVs). Design note: ARCH-DESIGN.md there. Checks: check-arch-nht.csx.
using System.Globalization;
using System.IO;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? "arch-nht.sbm");
const string Src = "TxDOT CSJ 0047-07-228";
double F(double ft) => Ft(ft);
double FI(double ft, double inch) => Ft(ft) + In(inch);
string dataDir = FindData();
string FindData()
{
    var d = new DirectoryInfo(Directory.GetCurrentDirectory());
    for (; d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "nht-0047-07-228");
        if (Directory.Exists(p)) return p;
    }
    throw new Exception("docs/plan-data/nht-0047-07-228 not found above the current folder");
}
List<Dictionary<string, string>> Csv(string name)
{
    var lines = File.ReadAllLines(Path.Combine(dataDir, name));
    var hdr = lines[0].Split(',');
    return lines.Skip(1).Where(l => l.Trim().Length > 0).Select(l => { var f = l.Split(','); return hdr.Select((h, i) => (h, v: i < f.Length ? f[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);

// ---------------------------------------------------------------- alignment + piers 10 / 11 (approach model or test model)
BridgeDefinition? approach10 = Doc.Project.Bridges.FirstOrDefault(b => b.Arch == null && b.Supports.Any(s => s.Name.Equals("Pier 10", StringComparison.OrdinalIgnoreCase)));
BridgeDefinition? approach11 = Doc.Project.Bridges.FirstOrDefault(b => b.Arch == null && b.Supports.Any(s => s.Name.Equals("Pier 11", StringComparison.OrdinalIgnoreCase)));
Alignment al;
SupportDefinition? sup10 = approach10?.Supports.First(s => s.Name.Equals("Pier 10", StringComparison.OrdinalIgnoreCase));
SupportDefinition? sup11 = approach11?.Supports.First(s => s.Name.Equals("Pier 11", StringComparison.OrdinalIgnoreCase));
bool testModel = sup10 == null || sup11 == null;
// Plan pier stations (p.94) and pier-line bearings (p.94: S 00 11'21.26" E / S 00 11'49.94" E).
double sta10 = Sta("24+17.92"), sta11 = Sta("26+19.60");
double Az(double deg, double min, double sec) => (deg + min / 60 + sec / 3600) * Math.PI / 180;   // quadrant angle
Vec2 SouthEast(double a) => new(Math.Sin(a), -Math.Cos(a));                                          // S a E as a unit vector (x east, y north)
if (testModel)
{
    Print("No approach model with Pier 10 / Pier 11 - building the TEST alignment and supports.");
    Doc.Project.Alignments.RemoveAll(a => a.Name == "CL NHT (arch test)");
    // TRAILCLNEW curves 104-107 (p.18-19), state plane feet (datum U12) as international feet.
    // TRAILCLNEW (p.18-19) from PC 104: curves 104-107 + tangents as elements (the PI table rounds curves 105/106 into each other at the PRC).
    // State plane feet (datum U12) taken as international feet.
    double bearing = 90 - (87 + 12 / 60.0 + 51.86 / 3600);                              // N 87 12'51.86" E -> math angle (deg)
    var els = new List<HorizontalElement>();
    var p0 = new Vec2(F(2499849.55), F(7016271.77));
    double ang = bearing * Math.PI / 180;
    void El(double lenFt, double k)
    {
        var e = new HorizontalElement(p0, ang, F(lenFt), k, k);
        els.Add(e);
        p0 = e.EndPoint; ang = e.EndAngle;
    }
    El(23_59.77 - 22_74.36, -1 / F(250)); El(24_00.44 - 23_59.77, 0); El(25_18.73 - 24_00.44, -1 / F(385)); El(26_47.89 - 25_18.73, 1 / F(385));
    El(27_15.09 - 26_47.89, 1 / F(250)); El(100, 0);
    al = new Alignment { Name = "CL NHT (arch test)", StartStation = Sta("22+74.36") };
    al.SetElements(els);
    Doc.Project.Alignments.Add(al);
    al.Profile = new VerticalProfile { Vpis = { new Vpi(Sta("20+00.00"), F(523.10)), new Vpi(Sta("23+40.01"), F(538.08), F(140)), new Vpi(Sta("26+50.01"), F(533.10), F(50)), new Vpi(Sta("32+25.01"), F(504.92), F(100)) } };
    al.CrossSlope = new CrossSlopeTable { Rows = { new CrossSlopeRow { Station = Sta("20+00"), LeftSlope = -0.01, RightSlope = -0.01 } } };
    Print($"test alignment: PRC 25+18.73 at {al.PointAt(Sta("25+18.73")).X / 0.3048:F2}, {al.PointAt(Sta("25+18.73")).Y / 0.3048:F2} (plan 2500079.12, 7016198.78)");
}
else
{
    al = Doc.Project.FindAlignment(approach10!.AlignmentName)!;
    sta10 = sup10!.Station; sta11 = sup11!.Station;
    Print($"Approach model found: alignment '{al.Name}', Pier 10 at {Doc.Units.FormatStation(sta10)} ({approach10.Name}), Pier 11 at {Doc.Units.FormatStation(sta11)} ({approach11!.Name}).");
}
// Pier lines: the plan bearings (p.94).
var dir10 = SouthEast(Az(0, 11, 21.26));
var dir11 = SouthEast(Az(0, 11, 49.94));
double SkewOf(double sta, Vec2 lineDir)
{
    var right = Vec2.FromAngle(al.AngleAt(sta)).Right;
    if (Vec2.Dot(lineDir, right) < 0) lineDir = -lineDir;
    return Math.Atan2(Vec2.Cross(right, lineDir), Vec2.Dot(right, lineDir)) * 180 / Math.PI;
}
double skew10 = sup10?.SkewDegrees ?? SkewOf(sta10, dir10), skew11 = sup11?.SkewDegrees ?? SkewOf(sta11, dir11);
Print($"pier skews: P10 {skew10:F4} deg, P11 {skew11:F4} deg (plan 19 deg 34'41\" = 19.5781 to the radial)");
// The arch bridge does NOT share the piers (no SharedPier link): the pier caps and the arch pedestals belong to the approach units (fixed-elevation
// CapBlocks "arch bearing south / north"); the bearings read their tops from SubstructureUnit.FixedSeats (approach mode) or p.102 (test mode).

// ---------------------------------------------------------------- local frame (p.119)
// CL1 / CL2: on the T lines (4 13/16 from CL pier toward the span, p.119), 7" LT (P10) / RT (P11) of the CL NHT pier point along the pier (p.119
// "7 in offset from CL pier", = the shaft offset p.94). Plan chord CL1-CL2 = 201'-0 local / 200'-11 11/16 in plan (U3: not reproduced).
Vec2 Line(double sta, double skew) => Vec2.FromAngle(al.AngleAt(sta)).Right.Rotate(skew * Math.PI / 180);
Vec2 Wp(double sta, double skew, double along, Vec2 towardSpan)
{
    var p = al.PointAt(sta) + Line(sta, skew) * along;
    var n = Line(sta, skew).Left; if (Vec2.Dot(n, towardSpan) < 0) n = -n;
    return p + n * In(4.8125);
}
var p10 = al.PointAt(sta10); var p11 = al.PointAt(sta11);
var cl1 = Wp(sta10, skew10, -In(7), p11 - p10);   // LT = - along the left->right line
var cl2 = Wp(sta11, skew11, In(7), p10 - p11);
var so1 = al.Inverse(cl1); var so2 = al.Inverse(cl2);
var arch = new ArchDefinition { Source = Src };
arch.Frame = new ArchFrameDefinition
{
    Wp1Station = so1.Station, Wp1Offset = so1.Offset, Wp1LocalX = -F(100.5),
    Wp2Station = so2.Station, Wp2Offset = so2.Offset, Wp2LocalX = F(100.5),
    OriginElevation = F(534.12), TiltDegrees = 1.039, TiltAxisSupport = "Pier 10",
    CheckWp1Elevation = F(535.74), CheckWp2Elevation = F(532.49),
};
Print($"CL1 {Doc.Units.FormatStation(so1.Station)} {so1.Offset / 0.3048:F3}' / CL2 {Doc.Units.FormatStation(so2.Station)} {so2.Offset / 0.3048:F3}'; plan distance {(cl2 - cl1).Length / 0.3048:F3}' (p.119 200.974')");

// ---------------------------------------------------------------- points (p.119, p.120, p.128, p.147)
void Pt(string n, double x, double y, double z) => arch.Points.Add(new ArchPoint(n, F(x), F(y), F(z)));
Pt("L0", 0, 0, 0); Pt("CL1", -100.5, 0, 0); Pt("CL2", 100.5, 0, 0);
Pt("T34'", -105.000, 8.658, 0); Pt("T0", -96.000, -8.658, 0); Pt("T0'", 96.000, 8.658, 0); Pt("T34", 105.000, -8.658, 0);
var rs = new[] { (-93.537, -8.383, 3.140), (-48.171, -5.305, 38.318), (7.840, -4.304, 49.767), (58.079, -5.342, 37.898), (102.537, -8.383, 3.140) };
for (int i = 0; i < rs.Length; i++) { Pt($"RS{i + 1}", rs[i].Item1, rs[i].Item2, rs[i].Item3); Pt($"RS{i + 1}'", -rs[i].Item1, -rs[i].Item2, rs[i].Item3); }
foreach (var r in Csv("upper-lateral-bracing-points.csv")) Pt(r["point"], D(r["X_ft"]), D(r["Y_ft"]), D(r["Z_ft"]));

// ---------------------------------------------------------------- ties (p.143): R 342'-4 x 16 @ 5'-10, R 360'-8 x 16 @ 6'-1 3/4, straights 1'-7 15/16 / 8'-4 3/16
double l0 = FI(1, 7.9375), s1 = FI(5, 10), s2 = FI(6, 1.75), l3 = FI(8, 4.1875);
ArchTieDefinition Tie(string name, string start, string end, string prime)
{
    var t = new ArchTieDefinition { Name = name, Start = start, End = end };
    t.Elements.Add(new ArchTieElement { Kind = ArchTieElementKind.Line, Length = l0 });
    t.Elements.Add(new ArchTieElement { Kind = ArchTieElementKind.ArcRight, Radius = FI(342, 4), Length = 16 * s1 });
    t.Elements.Add(new ArchTieElement { Kind = ArchTieElementKind.ArcLeft, Radius = FI(360, 8), Length = 16 * s2 });
    t.Elements.Add(new ArchTieElement { Kind = ArchTieElementKind.Line, Length = l3 });
    for (int k = 1; k <= 33; k++) t.PanelPoints.Add(new ArchPanelPoint($"T{k}{prime}", k <= 17 ? l0 + (k - 1) * s1 : l0 + 16 * s1 + (k - 17) * s2));
    return t;
}
arch.Ties.Add(Tie("South tie", "T0", "T34", ""));
arch.Ties.Add(Tie("North tie", "T0'", "T34'", "'"));   // 180 deg rotation of the south tie (p.119 note 8)
// Tie field splices (p.144 detail; positions p.143 framing plan TS(1)-TS(4), drawn at mid panel, not dimensioned -> U5 flag).
double PanelDist(int k) => k == 0 ? 0 : k <= 17 ? l0 + (k - 1) * s1 : l0 + 16 * s1 + (k - 17) * s2;
var tieSplice = new ArchSpliceDefinition
{
    Name = "p.144: web 2 - 9\" x 3/4\" bent PL (top flush with top of web), flange 5/8\" x 1'-2 bottom + 2 - 6\" x 5/8\" top PL, 1'-6 1/2 long, 7/8\" bolts in 15/16\" holes",
    FlangeOuterWidth = FI(1, 2), FlangeOuterThickness = In(0.625), FlangeInnerWidth = In(6), FlangeInnerThickness = In(0.625),
    FlangeBoltFirst = In(1.75), FlangeBoltPitch = In(3), FlangeBoltColumns = 3, FlangeEndEdge = In(1.5), FlangeRows = { new ArchBoltRow(In(1.5)), new ArchBoltRow(In(4.5)) },
    WebPlateWidth = In(9), WebPlateThickness = In(0.75), WebPlateEdgeFromWebEnd = 0, WebPlateNote = "BENT to the tie curve",
    WebBoltFirst = In(1.75), WebBoltPitch = In(3), WebBoltColumns = 3, WebEndEdge = In(1.5), WebRows = { new ArchBoltRow(In(1.5)), new ArchBoltRow(In(4.5)), new ArchBoltRow(In(7.5)) },
    Material = "ASTM A709 Gr. 50-F1 (FCM)", CheckFlangePlateLength = FI(1, 6.5), CheckWebPlateLength = FI(1, 6.5),
};
string tsWhy = U("p.143, 120, 93", "tie girder field splice positions (U5)", "the framing plan shows TS(1)-TS(4) per tie (5 segments = erection 'tie segments 1-5', p.93) without dimensions, the north tie splices are not the 180 deg rotation of the south ones (S TS(1) T7-T8, N TS(1) T6'-T7'), and the p.120 elevation / table show only TS1-TS3 (T9-T10, T17-T18, T25-T26)", "Splices built per the framing plan at mid panel (the p.120 table TS points are the arc mid-panel points); the p.120 set is reported only.");
foreach (var (t, pairs) in new[] { (arch.Ties[0], new[] { 7, 14, 22, 28 }), (arch.Ties[1], new[] { 6, 12, 20, 27 }) })
{
    for (int i = 0; i < 4; i++) t.SpliceAt.Add(new ArchPanelPoint($"TS({i + 1})", (PanelDist(pairs[i]) + PanelDist(pairs[i] + 1)) / 2));
    t.SpliceDetail = tieSplice; t.SpliceUnresolved = tsWhy;
}

// ---------------------------------------------------------------- ribs (p.119, p.127)
string stubWhy = U("p.123-124", "knuckle arch stub below RS1 / above RS5", "the stub is built up from 1\" arch flange plates, an end web and stiffeners whose outlines are only partly dimensioned", "Modeled as the W14x132 envelope down to the end flange top.");
// Rib splice (p.127, "RS1 and RS6 [= RS5] similar" -> all five): flanges 14 1/2 x 5/8 outside + 2 - 5 5/8 x 5/8 inside (flush with the outside plate
// edge, Section A-A), rows 1 3/4 and 4 1/4 from the plate edge, staggered columns at 2" from 2" (6 per side); web 2 - 10 x 3/4 CURVED, 5 rows @ 1 3/4,
// staggered columns at 2" from 2" (4 per side); 1 3/4 end edges, 1 3/4 x 1 3/4 clips.
var ribSplice = new ArchSpliceDefinition
{
    Name = "p.127: flanges 14 1/2\" x 5/8\" outside + 2 - 5 5/8\" x 5/8\" inside PL, web 2 - 10\" x 3/4\" curved PL, 7/8\" A325 bolts",
    FlangeOuterWidth = In(14.5), FlangeOuterThickness = In(0.625), FlangeInnerWidth = In(5.625), FlangeInnerThickness = In(0.625),
    FlangeBoltFirst = In(2), FlangeBoltPitch = In(2), FlangeBoltColumns = 6, FlangeEndEdge = In(1.75), FlangeClip = In(1.75),
    FlangeRows = { new ArchBoltRow(In(1.75), 0, 2), new ArchBoltRow(In(4.25), 1, 2) },
    WebPlateWidth = In(10), WebPlateThickness = In(0.75), WebPlateNote = "CURVED to the rib", WebClip = In(1.75),
    WebBoltFirst = In(2), WebBoltPitch = In(2), WebBoltColumns = 4, WebEndEdge = In(1.75),
    WebRows = { new ArchBoltRow(In(1.5), 0, 2), new ArchBoltRow(In(3.25), 1, 2), new ArchBoltRow(In(5), 0, 2), new ArchBoltRow(In(6.75), 1, 2), new ArchBoltRow(In(8.5), 0, 2) },
    Material = "ASTM A709 Gr. 50-T1",
};
arch.Ribs.Add(new ArchRibDefinition { Name = "South rib", ChordStart = "T0", ChordEnd = "T34", Radius = F(126), Rise = F(50), InclinationDegrees = 5,
    Splices = { "RS1", "RS2", "RS3", "RS4", "RS5" }, StubBottomZ = In(0.875), StubUnresolved = stubWhy, SpliceDetail = ribSplice });
arch.Ribs.Add(new ArchRibDefinition { Name = "North rib", ChordStart = "T0'", ChordEnd = "T34'", Radius = F(126), Rise = F(50), InclinationDegrees = -5,
    Splices = { "RS1'", "RS2'", "RS3'", "RS4'", "RS5'" }, StubBottomZ = In(0.875), StubUnresolved = stubWhy, SpliceDetail = ribSplice });

// ---------------------------------------------------------------- hangers (p.147 CSV; north = (-X, -Y, Z)); sockets p.148 tables; strand WireCo Table 1 p.10
foreach (var r in Csv("cable-hanger-coordinates.csv"))
{
    string c = r["cable"];
    double tx = D(r["X1p_ft"]), ty = D(r["Y1p_ft"]), tz = D(r["Z1p_ft"]), hx = D(r["X2p_ft"]), hy = D(r["Y2p_ft"]), hz = D(r["Z2p_ft"]);
    double len = F(D(r["cable_length_TC_HC_ft"]));
    arch.Hangers.Add(new ArchHangerDefinition { Name = c + "-S", Rib = "South rib", Tie = "South tie", TieAnchor = new ArchPoint(r["TC"], F(tx), F(ty), F(tz)), RibAnchor = new ArchPoint(r["HC"], F(hx), F(hy), F(hz)), PlanLength = len });
    arch.Hangers.Add(new ArchHangerDefinition { Name = c + "-N", Rib = "North rib", Tie = "North tie", TieAnchor = new ArchPoint(r["TC"] + "'", -F(tx), -F(ty), F(tz)), RibAnchor = new ArchPoint(r["HC"] + "'", -F(hx), -F(hy), F(hz)), PlanLength = len });
}
arch.HangerSystem = new ArchHangerSystem
{
    StrandDiameter = In(1.0), StrandWeightLbPerFt = 2.10,
    StrandDescription = "galvanized structural strand (WireCo / Bethlehem Table 1 p.10: 0.600 sq in, 2.10 lb/ft, E 24,000 ksi prestretched)",
    StrandMaterial = "ASTM A586 zinc-coated structural strand",
    RibSocket = new ArchSocketDefinition { Name = "Open strand socket (at arch rib)", A = In(4.06), B = In(2.76), G = In(3.43), L = In(7.87), W = In(16.18), PinDiameter = In(1.378), ShankDiameter = In(1.299),
        Basis = "plan p.148 OPEN STRAND SOCKET DIMENSIONS (envelope: jaw block A x B to L, shank Db to W, pin dL x G)" },
    TieSocket = new ArchSocketDefinition { Name = "Adjustable open strand socket with turnbuckle (at tie girder)", A = In(4.06), B = In(2.76), G = In(3.43), L = In(7.87), W = In(34.41), PinDiameter = In(1.378), ShankDiameter = In(1.299),
        Basis = "plan p.148 ADJUSTABLE OPEN STRAND SOCKET DIMENSIONS (turnbuckle body drawn at the shank diameter - its outline is not dimensioned)" },
};

// ---------------------------------------------------------------- floorbeams (p.143, p.145, p.146): FBn = south Tn - north T(34-n)'
var sched = Csv("floorbeam-schedule.csv").ToDictionary(r => r["floorbeam"], r => r);
for (int n = 1; n <= 33; n++)
    arch.Floorbeams.Add(new ArchFloorbeamDefinition
    {
        Name = $"FB{n}", TieA = "South tie", PanelA = $"T{n}", TieB = "North tie", PanelB = $"T{34 - n}'",
        PtDimA = F(D(sched[$"FB{n}"]["dim_A_ft"])), PtDimB = F(D(sched[$"FB{n}"]["dim_B_ft"])),
    });

// ---------------------------------------------------------------- upper lateral bracing (p.128): UL(n) is on the straight U(n)-U'(11-n); the other pair of
// members U(n+1)-UL(n)-U'(12-n) kinks at UL(n) (0.1-0.5 ft off a straight line in the table) -> members end at the table points.
for (int n = 1; n <= 10; n++)
{
    string ul = $"UL{n}";
    arch.Bracing.Add(new ArchBraceDefinition { Name = $"BR{n}A", From = $"U{n}", To = ul });
    arch.Bracing.Add(new ArchBraceDefinition { Name = $"BR{n}B", From = ul, To = $"U{11 - n}'" });
    arch.Bracing.Add(new ArchBraceDefinition { Name = $"BR{n}C", From = $"U{n + 1}", To = ul });
    arch.Bracing.Add(new ArchBraceDefinition { Name = $"BR{n}D", From = ul, To = $"U{12 - n}'" });
}
arch.BracingOrientationUnresolved = U("p.128-142", "W8x28 upper lateral bracing web orientation and member ends", "the bracing details (p.129-142) are not transcribed and the U points lie 0.685 ft off the rib WP plane (U6)", "Web set in the plane of the member and the local Z axis, members work point to work point.");

// ---------------------------------------------------------------- knuckles (p.123-126)
string ewWhy = U("p.126", "knuckle end web height at its ends", "only the height at the CL1-CL2 chord (1'-0 1/8) is dimensioned", "End height 10\" scaled from Section A-A.");
string dpWhy = U("p.123-124", "knuckle 1\" deck plate elevation", "the plate is shown in plan only", "Top flush with the end flange top (7/8\" above the tie WP).");
arch.Knuckles.Add(new ArchKnuckleDefinition { Name = "West", EndFrom = "T34'", EndTo = "T0", DeckPlate = { "T34'", "T33'", "T1", "T0" },
    EndWebScaledHeightAtEnds = In(10), EndWebUnresolved = ewWhy, DeckPlateUnresolved = dpWhy });
arch.Knuckles.Add(new ArchKnuckleDefinition { Name = "East", EndFrom = "T34", EndTo = "T0'", DeckPlate = { "T34", "T33", "T1'", "T0'" },
    EndWebScaledHeightAtEnds = In(10), EndWebUnresolved = ewWhy, DeckPlateUnresolved = dpWhy });

// ---------------------------------------------------------------- deck (p.151-152, L.B. Foster 3" bridge decking), curbs, PT (data)
var deck = new ArchDeckDefinition
{
    RightTie = "South tie", LeftTie = "North tie", EndKnuckles = { "West", "East" },
    FormThickness = In(0.1046), FormDescription = "3\" x 8\" 12 ga galvanized steel deck form (Gr 33, G165), L.B. Foster 3\" bridge decking",
    FormUnresolved = U("L.B. Foster brochure p.6", "steel deck form flute widths", "the brochure drawing is not dimensioned (3\" deep, 8\" pitch, 24\" cover only)", "Top flat 5 3/4\" and flute bottom 1 5/16\" scaled from the drawing."),
    ConcreteStrengthKsi = 5,
};
deck.Curbs.Add(new ArchCurbDefinition { Name = "Curb LT", InnerOffset = -F(7) });
deck.Curbs.Add(new ArchCurbDefinition { Name = "Curb RT", InnerOffset = F(7) });
arch.Deck = deck;
// PT tendons through the floorbeam openings (p.145: A from the north (left looking upstation) tie, B from the south tie, pairs 1'-4, 7" above the flange bottom).
var tie = arch.Ties.ToDictionary(t => t.Name);
string[] ptNames = { "PT-N1", "PT-N2", "PT-S2", "PT-S1" };
var ptPaths = ptNames.ToDictionary(n => n, n => new ArchTendon { Name = n, Description = "Longitudinal PT tendon (A416 Gr 270 LR, grouted); strand count p.155 not transcribed - data only" });
deck.Tendons.AddRange(ptPaths.Values);
// (paths filled after regeneration from the generated tie panel points - see below)

// ---------------------------------------------------------------- bearings (p.102, p.107, p.124)
// CL brg 2" from the T point toward the span (p.124); pedestal tops p.102: EL A = SOUTH at Pier 10 / NORTH at Pier 11 (orientation derived by the
// approach model from p.102 + p.91, build-nht.csx); in approach mode the tops are read from the pier units' FixedSeats instead. Underside of steel = end flange bottom (1 1/8" below the WP) - 1" plate below it (p.124, not dimensioned, U10).
void Brg(string name, string support, string tpt, double seatFt) => arch.Bearings.Add(new ArchBearingDefinition { Name = name, Support = support, Point = "BRG-" + name, SeatElevation = F(seatFt), SteelUndersideZ = -In(1.125) - In(1) });
Brg("NW", "Pier 10", "T34'", 534.726); Brg("SW", "Pier 10", "T0", 534.779); Brg("NE", "Pier 11", "T0'", 531.546); Brg("SE", "Pier 11", "T34", 531.494);
foreach (var (bn, tn) in new[] { ("NW", "T34'"), ("SW", "T0"), ("NE", "T0'"), ("SE", "T34") })
{
    var t = arch.Point(tn)!;
    bool west = t.X < 0;
    var other = west ? (tn == "T34'" ? arch.Point("T0")! : arch.Point("T34'")!) : (tn == "T0'" ? arch.Point("T34")! : arch.Point("T0'")!);
    var line = new Vec2(other.X - t.X, other.Y - t.Y).Normalized();
    var nrm = line.Left; if (Vec2.Dot(nrm, new Vec2(-t.X, -t.Y)) < 0) nrm = -nrm;
    arch.Points.Add(new ArchPoint("BRG-" + bn, t.X + nrm.X * In(2), t.Y + nrm.Y * In(2), 0));
}

// ---------------------------------------------------------------- open plan items (each raised as a warning)
arch.Unresolved.AddRange(new[]
{
    U("p.102, 107, 119, 124", "arch pedestal / bearing elevations (B1-B3)", "the plan pedestal tops do not reconcile with the WP elevations + bearing stack (0.13-0.20 ft; EL A vs E 0.053 ft)", "Bearings are built up from the plan pedestal tops; the chain difference is a CHECK row."),
    U("p.102", "which arch pedestal is EL A / EL E", "the view direction of the pier elevation is not stated on the sheet", "EL A = south at Pier 10 / north at Pier 11, as derived from p.102 + p.91 by the approach model."),
    U("p.94, 102, 119", "CL1 / CL2 relative to the piers (U3)", "no combination of the layout offsets reproduces the plan chord 200'-11 11/16", "CL1/CL2 = 7\" along the pier + 4 13/16\" toward the span; the local geometry is kept and the difference reported."),
    U("p.119-150", "camber / no-load geometry (U4)", "not given", "Final geometry modeled."),
    U("p.147, 128", "hanger anchor offsets from the rib WP (U6)", "H points lie 0.81-0.84 ft inside the rib WP circle and U points 0.685 ft off its plane; the offset rule is not stated", "Hanger and bracing points are taken from the tables as printed."),
    U("p.107, 93", "sole plate weld target (U8)", "the bearing note and the erection sequence name different plates", "Not modeled (welds)."),
    U("p.119, 124", "WP position in the tie / steel underside at the bearings (U10)", "the WP is at the bottom of the tie flange (p.119) but drawn inside the 2\" end flange (p.124) and the plate below it is not dimensioned", "End flange from 1 1/8\" below to 7/8\" above the WP (tie flange + 1 1/8\" fill = 2\"), steel underside 1\" lower."),
    U("p.148", "hanger sockets vs the supplied WireCo catalog", "the plan sockets (pin 1.378\", W 16.18\" / 34.41\") are not the catalog's 1\" open strand socket (Table 6 p.20: pin 2 1/4\", A 12\")", "Sockets modeled at the plan table dimensions; the catalog row is reported by check-arch-nht.csx."),
});

// ---------------------------------------------------------------- the arch bridge
Doc.Project.Bridges.RemoveAll(b => b.Arch != null && b.Name == "Arch span 10");
var br = new BridgeDefinition
{
    Name = "Arch span 10", AlignmentName = al.Name, Structure = approach10?.Structure ?? "Northaven Trail",
    Supports =
    {
        new SupportDefinition { Name = "Pier 10", Type = SupportType.Pier, Station = sta10, SkewDegrees = skew10, Substructure = new SubstructureDefinition { Enabled = false } },
        new SupportDefinition { Name = "Pier 11", Type = SupportType.Pier, Station = sta11, SkewDegrees = skew11, Substructure = new SubstructureDefinition { Enabled = false } },
    },
    Arch = arch,
};
br.Girders.Count = 0;
Doc.Project.Bridges.Add(br);

// PT tendon paths need the tie panel points: generate once, then fill the data and regenerate.
var rep0 = Doc.Regenerate();
var am = Doc.Steel(br.Name).Arch!;
var south = am.Ties.First(t => t.Name == "South tie").PanelPoints.ToDictionary(p => p.Name, p => p.Local);
var north = am.Ties.First(t => t.Name == "North tie").PanelPoints.ToDictionary(p => p.Name, p => p.Local);
for (int n = 1; n <= 33; n++)
{
    var fb = arch.Floorbeams[n - 1];
    var a = south[$"T{n}"]; var b = north[$"T{34 - n}'"];
    var e = (b - a) * (1 / (b - a).Length);
    double len = (b - a).Length, z = In(7);
    Vec3 At(double s) => a + e * s;
    void Add(string tn, Vec3 p) => ptPaths[tn].Path.Add(new ArchPoint($"FB{n}", p.X, p.Y, z));
    Add("PT-N1", At(len - fb.PtDimA)); Add("PT-N2", At(len - fb.PtDimA - FI(1, 4)));
    Add("PT-S1", At(fb.PtDimB)); Add("PT-S2", At(fb.PtDimB + FI(1, 4)));
}

// Approach mode: bearing seats = the approach model's fixed-elevation arch pedestals (nearest FixedSeat of the pier unit in plan).
if (!testModel)
{
    foreach (var (bname, uname, pier) in new[] { ("NW", approach10!.Name, "Pier 10"), ("SW", approach10.Name, "Pier 10"), ("NE", approach11!.Name, "Pier 11"), ("SE", approach11.Name, "Pier 11") })
    {
        var unit = Doc.Model(uname).Substructure?.Units.FirstOrDefault(u => u.Name == pier);
        var bdef = arch.Bearings.First(b => b.Name == bname);
        var bres = am.Bearings.FirstOrDefault(b => b.Name == bname);
        if (unit == null || unit.FixedSeats.Count == 0 || bres == null) { Print($"WARNING bearing {bname}: no FixedSeats on {pier} of {uname} - p.102 value kept"); continue; }
        var best = unit.FixedSeats.Select(fs => (fs, w: unit.World(fs.S, fs.N, fs.Z))).OrderBy(x => (x.w.XY - bres.Center.XY).Length).First();
        bdef.SeatElevation = best.fs.Z;
        Print($"bearing {bname}: seat '{best.fs.Name}' of {pier} top {best.fs.Z / 0.3048:F3}'; CL bearing (arch geometry) to the pedestal seat point {(best.w.XY - bres.Center.XY).Length / 0.0254:F2}\" in plan");
    }
}

var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
foreach (var w in rep.Warnings.Where(w => w.StartsWith(br.Name))) Print("warning " + w);
am = Doc.Steel(br.Name).Arch!;
Print($"Arch: {am.Steel.Parts.Count} parts, {am.Steel.SteelMass * 2.20462:N0} lb, {am.Hangers.Count} hangers, {am.Floorbeams.Count} floorbeams, {am.Bearings.Count} bearings, deck {am.SlabArea / 0.09290304:N0} SF (plan 3,698)");
foreach (var c in am.Checks.Where(c => !c.Ok)) Print($"CHECK {c.Item}: plan {c.Plan} model {c.Model}");
Doc.Save(outPath);
Print($"Saved {outPath}");
