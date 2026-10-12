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
    // Rib pin = the p.147 "H(n) X2, Y2, Z2" point (10" inside the rib working line on the cable axis, P1 off the arch plane = p.148 fin plate P1 / P2);
    // the "HC(n) X2', Y2', Z2'" point lies ON the working line (R 126'-0) and the p.147 cable length is TC to it - labels vs geometry: CONFLICT reported.
    double tx = D(r["X1p_ft"]), ty = D(r["Y1p_ft"]), tz = D(r["Z1p_ft"]), hx = D(r["X2p_ft"]), hy = D(r["Y2p_ft"]), hz = D(r["Z2p_ft"]);
    double px = D(r["X2_ft"]), py = D(r["Y2_ft"]), pz = D(r["Z2_ft"]);
    double len = F(D(r["cable_length_TC_HC_ft"]));
    arch.Hangers.Add(new ArchHangerDefinition { Name = c + "-S", Rib = "South rib", Tie = "South tie", TieAnchor = new ArchPoint(r["TC"], F(tx), F(ty), F(tz)), RibAnchor = new ArchPoint(r["H"], F(px), F(py), F(pz)),
        RibWorkPoint = new ArchPoint(r["HC"], F(hx), F(hy), F(hz)), PlanLength = len });
    arch.Hangers.Add(new ArchHangerDefinition { Name = c + "-N", Rib = "North rib", Tie = "North tie", TieAnchor = new ArchPoint(r["TC"] + "'", -F(tx), -F(ty), F(tz)), RibAnchor = new ArchPoint(r["H"] + "'", -F(px), -F(py), F(pz)),
        RibWorkPoint = new ArchPoint(r["HC"] + "'", -F(hx), -F(hy), F(hz)), PlanLength = len });
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

// ---------------------------------------------------------------- cable fin plates (p.148 rib table, p.149 tie table + details, p.146 Section C-C), connectors (p.150)
var ribFins = Csv("cable-fin-plates-arch-rib.csv").ToDictionary(r => r["cable"]);
var tieFins = Csv("cable-fin-plates-tie-girder.csv").ToDictionary(r => r["cable"]);
foreach (var h in arch.Hangers)
{
    string c = h.Name.Split('-')[0];
    bool north = h.Name.EndsWith("-N");
    if (ribFins.TryGetValue(c, out var rf))
        h.RibFin = new ArchRibFin { AngleB = D(rf["angle_B_deg"]), P1 = In(D(rf["P1_in"])) * (north ? -1 : 1), P2 = In(D(rf["P2_in"])), P3 = In(D(rf["P3_in"])), P4 = In(D(rf["P4_in"])), WeldLength = In(D(rf["weld_D_in"])) };
    if (tieFins.TryGetValue(c, out var tf))
        h.TieFin = new ArchTieFin { Panel = tf["T_n"] + (north ? "'" : ""), AngleA = D(tf["angle_A_deg"]), Orient = tf["orient"] };
}
arch.Fins = new ArchFinSettings
{
    RibThickness = In(0.875), RibTopClip = In(1), RibPinRadius = In(2.25), PinHole = In(1.378),
    RibUnresolved = U("p.148", "rib cable fin plate thickness and top corner clips", "the rib fin plate table and Section A-A give P1-P4, angle B and weld D but no plate thickness, and the top clips are not dimensioned", "7/8\" (= the tie fin plate p.149, same open socket and 1.378\" pin) and 1\" x 1\" clips scaled from Section A-A."),
    TieThickness = In(0.875), TieWidth = In(8), TiePinFromEdge = In(1.875), TieDepthBelowBend = In(7), TieBendAboveWeb = In(0.5), TiePinAboveBend = In(4),
    TieFarEdgeAboveBend = In(0.5), TiePinRadius = In(2.25), TieHoleEdge = In(1.5), TieHoleRows = { In(2), In(5.5) },
    Material = "ASTM A709 Gr. 50-F1 (FCM)",
};
// Connectors at the crossings circled on the p.150 elevation (N.T.S.; 4 per end, same pairs on the mirror arch): C1xC2, C3xC4, C5xC6, C3xC6, C27xC28, C29xC30, C31xC32, C27xC30.
foreach (var sfx in new[] { "S", "N" })
    foreach (var (a, b) in new[] { ("C1", "C2"), ("C3", "C4"), ("C5", "C6"), ("C3", "C6"), ("C27", "C28"), ("C29", "C30"), ("C31", "C32"), ("C27", "C30") })
        arch.CableConnectors.Add(new ArchCableConnector { Name = $"{a}x{b}-{sfx}", CableA = $"{a}-{sfx}", CableB = $"{b}-{sfx}" });
arch.CableConnectorDetail = new ArchCableConnectorDetail { Basis = "p.150 pendant plate 3 1/8 x 3/16, sleeve brackets R 11/16 x 1/4 x 2\" (N.T.S. details, UHMW-PE)" };

// ---------------------------------------------------------------- floorbeams (p.143, p.145, p.146): FBn = south Tn - north T(34-n)'
var sched = Csv("floorbeam-schedule.csv").ToDictionary(r => r["floorbeam"], r => r);
string studWhy = U("p.145", "floorbeam shear stud length", "Section A-A shows the 7/8\" horizontal studs at 7\" above the flange bottom (32 per face, 3 1/2 - 6\" spacing) without a length", "6 7/8\" (7\" nominal stud as welded) scaled from Section A-A.");
for (int n = 1; n <= 33; n++)
    arch.Floorbeams.Add(new ArchFloorbeamDefinition
    {
        Name = $"FB{n}", TieA = "South tie", PanelA = $"T{n}", TieB = "North tie", PanelB = $"T{34 - n}'",
        PtDimA = F(D(sched[$"FB{n}"]["dim_A_ft"])), PtDimB = F(D(sched[$"FB{n}"]["dim_B_ft"])),
        PtSlotLength = In(5.5), PtSlotHeight = In(2.25), PtSlotZ = In(7), PtPairSpacing = FI(1, 4),
        StudsPerFace = 32, StudZ = In(7), StudMinSpacing = In(3.5), StudMaxSpacing = In(6), StudLength = In(6.875), StudUnresolved = studWhy,
    });
// Floorbeam connection (p.146): 1" stiffener (1" flat, 8" at the flange edge, 1 x 1 clip), 2 - 5/8 x 7 x 1'-0 1/2 web plates (rows 1 3/8, 3 1/2, 5 5/8 below the
// top, staggered columns 1 5/8 / 3 1/4 / 4 7/8 from the joint), 5/8 x 4 15/16 x 1'-0 1/2 flange plates each side (rows 1 3/8 and 3 9/16 from the flange edge).
arch.FloorbeamConnection = new ArchFloorbeamConnection
{
    StiffenerThickness = In(1), StiffenerTopFlat = In(1), StiffenerOuterHeight = In(8), StiffenerClip = In(1),
    PlateThickness = In(0.625), PlateLength = FI(1, 0.5), WebPlateHeight = In(7), BoltFirst = In(1.625), BoltPitch = In(1.625), BoltColumns = 3,
    WebRows = { new ArchBoltRow(In(1.375), 0, 2), new ArchBoltRow(In(3.5), 1, 2), new ArchBoltRow(In(5.625), 0, 2) },
    FlangePlateWidth = In(4.9375), FlangeRows = { new ArchBoltRow(In(1.375), 0, 2), new ArchBoltRow(In(3.5625), 1, 2) },
    BottomFlangePlates = true,
    BottomUnresolved = U("p.146", "floorbeam flange connection plates below the flange", "Detail 1 shows a 5/8\" plate under the flange but Section B-B shows only the top plates in plan", "Bottom plates = the top plates mirrored (2 per end)."),
};

// ---------------------------------------------------------------- upper lateral bracing (p.128): UL(n) is on the straight U(n)-U'(11-n); the other pair of
// members U(n+1)-UL(n)-U'(12-n) kinks at UL(n) (0.1-0.5 ft off a straight line in the table) -> members end at the table points.
// p.141 / 142: at each crossing UL(n) one member is continuous (U(n)-UL(n)-U'(11-n), straight in the table) and the other is spliced over it with top and
// bottom plates (7" wide, L1 / L2 / L3, 12 bolts each); the spliced pieces stop 1/2" off the continuous flange tips (L2 sin(angle) = bf + 1").
var ulSplices = Csv("upper-lateral-bracing-splices.csv").ToDictionary(r => r["crossing"]);
string ulWhy = U("p.141 / 142", "bracing crossing splice plate thickness", "the top / bottom splice plates are dimensioned in plan only (7\" wide, lengths, bolts) without a thickness", "1/2\".");
for (int n = 1; n <= 10; n++)
{
    string ul = $"UL{n}";
    arch.Bracing.Add(new ArchBraceDefinition { Name = $"BR{n}X", From = $"U{n}", To = $"U{11 - n}'", Via = ul });
    arch.Bracing.Add(new ArchBraceDefinition { Name = $"BR{n}C", From = $"U{n + 1}", To = ul, TrimEndAt = $"BR{n}X" });
    arch.Bracing.Add(new ArchBraceDefinition { Name = $"BR{n}D", From = ul, To = $"U{12 - n}'", TrimStartAt = $"BR{n}X" });
    var sp = ulSplices[ul];
    arch.BracingCrossings.Add(new ArchBraceCrossing { Name = ul, Point = ul, Continuous = $"BR{n}X", PieceA = $"BR{n}C", PieceB = $"BR{n}D",
        TopL1 = In(D(sp["top_L1_in"])), TopL2 = In(D(sp["top_L2_in"])), TopL3 = In(D(sp["top_L3_in"])), TopAngleDeg = D(sp["top_angle_deg"]),
        BottomL1 = In(D(sp["bot_L1_in"])), BottomL2 = In(D(sp["bot_L2_in"])), BottomL3 = In(D(sp["bot_L3_in"])), BottomAngleDeg = D(sp["bot_angle_deg"]),
        Thickness = In(0.5), Unresolved = ulWhy });
}
arch.BracingOrientationUnresolved = U("p.129-140", "upper lateral bracing member ends at the rib panel points U / U'", "a member end without a gusset connection is not detailed", "Member runs to the work point; web vertical per the p.128 typical section.");
// Gusset connections at the 22 rib panel points (p.129-140; p.129 = p.130, one sheet per U(n) / U(n)' pair drawn for U(n)', U(n) = the 180 deg
// rotation): geometry read from the VECTOR linework (tools/nht_bracing_plates.py -> upper-lateral-bracing-plates.csv, 0.1 pt = 0.022"):
// gusset flat patterns with true arcs (R 2" corners / copes, R 9 3/4 - 2'-0 between the legs), bend lines, 15/16" holes, the dashed W8x28 under
// each leg (web centre line + end); VIEW A-A connection plates (outline + holes) and 1" stiffeners relative to the WP. Printed: 7/8" gussets and
// connection plates, 1" stiffeners with 1 1/2" clips, 1" clear of the flange tips (B-B), CL rib to WP 8 1/4" (B-B), gusset 1" inside the plate edge (D-D).
{
    var rows = Csv("upper-lateral-bracing-plates.csv");
    ArchPlateDrawing Drawing(IEnumerable<Dictionary<string, string>> rs) => new ArchPlateDrawing
    {
        Outline = rs.Where(r => r["kind"] == "V").OrderBy(r => int.Parse(r["seq"])).Select(r => new ArchOutlinePoint(In(D(r["x_in"])), In(D(r["y_in"])), In(D(r["r_in"])), r["a"] == "1")).ToList(),
        Holes = rs.Where(r => r["kind"] == "H").Select(r => new ArchPoint("H" + r["seq"], In(D(r["x_in"])), In(D(r["y_in"])), 0)).ToList(),
    };
    ArchGussetPlate Gusset(List<Dictionary<string, string>> rs)
    {
        var bend = rs.Where(r => r["kind"] == "BEND").OrderBy(r => r["seq"]).Select(r => new ArchPoint("B" + r["seq"], In(D(r["x_in"])), In(D(r["y_in"])), 0)).ToList();
        return new ArchGussetPlate
        {
            Plate = Drawing(rs), BendA = bend.Count == 2 ? bend[0] : null, BendB = bend.Count == 2 ? bend[1] : null,
            Legs = rs.Where(r => r["kind"] == "BRACE").Select(r => new ArchGussetLeg { EndX = In(D(r["x_in"])), EndY = In(D(r["y_in"])), DirX = D(r["a"]), DirY = D(r["b"]) }).ToList(),
        };
    }
    string stWhy = U("p.129-140 VIEW A-A / B-B", "rib stiffener position along the rib and radial extent", "the 1\" stiffeners are drawn dashed in VIEW A-A without dimensions (B-B gives only the 1 1/2\" clips and 1\" clear of the flange tips)", "Position and radial extent taken from the vector linework of A-A (to scale, 0.022\").");
    string endWhy = U("p.129-140", "bracing member end at the gusset", "the W8x28 end is drawn dashed under the gussets without a dimension", "End taken from the vector linework of the gusset views (to scale).");
    // VIEW A-A printed angles of the connection plate bolt lines to the rib axis (per sheet; 04 39'13" on every two-brace sheet).
    double Dms(int d, int m, int sec) => d + m / 60.0 + sec / 3600.0;
    var aaAngles = new Dictionary<int, double[]>
    {
        [1] = new[] { Dms(4, 39, 13) }, [2] = new[] { Dms(3, 5, 16), Dms(4, 39, 13) }, [3] = new[] { Dms(3, 26, 50), Dms(4, 39, 13) },
        [4] = new[] { Dms(3, 53, 49), Dms(4, 39, 13) }, [5] = new[] { Dms(4, 20, 23), Dms(4, 39, 13) }, [6] = new[] { Dms(4, 37, 12), Dms(4, 39, 13) },
        [7] = new[] { Dms(4, 36, 26), Dms(4, 39, 13) }, [8] = new[] { Dms(4, 18, 30), Dms(4, 39, 13) }, [9] = new[] { Dms(3, 51, 45), Dms(4, 39, 13) },
        [10] = new[] { Dms(3, 25, 46), Dms(4, 39, 13) }, [11] = new[] { Dms(3, 6, 20) },
    };
    // CONFLICT (sheet titles vs the p.128 table): the connection drawn on the sheet titled "U(n) and U(n)' details" fits the members at the table
    // points U(12-n) / U(12-n)', not U(n): e.g. the single-brace sheet "U1" has the brace at 24.6 deg to the welded edge, but at the table point U1
    // the brace (to UL1) makes at least 36.2 deg with the rib flange plane (impossible), while at U11' (to UL1) the fit is 0.01 deg; on every sheet the
    // drawn brace angles fit U(12-n) within 0.4 deg and U(n) not at all. Each sheet is modeled at the points its geometry fits.
    arch.Unresolved.Add("CONFLICT (p.129-140 sheet titles vs p.128 table): the bracing connection drawn on the sheet 'U(n) and U(n)' details' fits the table points U(12-n) / U(12-n)' (drawn gusset angles within 0.4 deg; at U(n) they are geometrically impossible, e.g. sheet U1: 24.6 deg vs >= 36.2 deg at U1). Each sheet is modeled at U(12-n) / U(12-n)'; U6 is its own mirror.");
    for (int n = 1; n <= 11; n++)
    {
        var det = rows.Where(r => r["detail"] == $"U{n}").ToList();
        if (det.Count == 0) { Print($"WARNING bracing detail U{n} missing in the CSV"); continue; }
        string page = det[0]["source"];
        double webY = det.Where(r => r["plate"] == "AA" && r["kind"] == "WEB").Select(r => In(D(r["y_in"]))).DefaultIfEmpty(double.NaN).First();
        int k = 12 - n;
        foreach (var (pt, rib) in new[] { ($"U{k}'", "North rib"), ($"U{k}", "South rib") })
            arch.BracingConnections.Add(new ArchBracingConnection
            {
                Name = pt, Point = pt, Rib = rib, Source = $"{page} sheet 'U{n} and U{n}' details'", CheckWeldAnglesDeg = aaAngles[n].ToList(),
                TopGusset = Gusset(det.Where(r => r["plate"] == "G-TOP").ToList()), BottomGusset = Gusset(det.Where(r => r["plate"] == "G-BOT").ToList()),
                TopConnectionPlate = Drawing(det.Where(r => r["plate"] == "CP-TOP")), BottomConnectionPlate = Drawing(det.Where(r => r["plate"] == "CP-BOT")),
                Stiffeners = { Drawing(det.Where(r => r["plate"] == "ST-TOP")), Drawing(det.Where(r => r["plate"] == "ST-BOT")) },
                WebAxisY = webY, CheckWpFromRibCl = In(8.25), CheckGussetFromPlateEdge = In(1),
                StiffenerUnresolved = stWhy, BraceEndUnresolved = endWhy,
            });
    }
}

// ---------------------------------------------------------------- knuckles (p.123-126)
// v2 detail (p.123-126, re-read at 300-500 dpi): end flange 2" x 32" x 21'-2 (1'-1 1/2 outside the end line), end web 1" (1'-0 1/8 at the chord), 4 PT
// openings 14" x 3" (2'-7 1/2 and 4'-1 1/2 from the chord, at 6 1/8 above the end flange) and 7/8" x 7" studs @ 9 1/2; 1" deck ("END") plate on the
// flange tops (3" over the end flange edge, 6" over the tie flanges, 5" over the FB1 flange = 1" off the tie / FB CLs) with 7/8" studs (rows 10 5/8 apart,
// 7" on the plate, 8" on the end flange); per rib end: bent 1" arch flange plates (rib flange width, legs NW 9 15/16 / 10 3/16, SW 10 1/2 / 10 1/2,
// R 6", 1" clip; SE = NW, NE = SW by the rotational symmetry of p.123), 1" stiffener 7" outside the springing (9 1/2), arch stiffener 1'-11 5/8 up the
// rib, tie joint at the end flange edge with 2 - 1/2" gussets (9 x 20, 18 bolts), 1 1/8" fill + 1" splice plate, 1" cheese plates at the jacks.
string ewWhy = U("p.126", "knuckle end web height at its ends", "only the height at the CL1-CL2 chord (1'-0 1/8) is dimensioned", "End height 10 1/4\" scaled from Section A-A (1/2\" = 1'-0).");
var kDetail = new ArchKnuckleDetail
{
    StubWebUnresolved = U("p.124 / 125 Section A-A", "knuckle stub web thickness", "the web plate milled to bear on the end flange (1'-11 5/8 to the arch stiffener) is drawn but not labelled", "1\" (as drawn, = the arch flange plates)."),
    ArchStiffenerUnresolved = U("p.124 / 125 Section A-A", "knuckle arch stiffener extent", "the 1\" arch stiffener is shown on the intrados side only, its width is not dimensioned", "From the stub web face to the flange tip (7 3/8 from the rib CL)."),
    // v4: the splice / fill outline from the p.124 partial plan VECTOR linework (dashed "1\" SPLICE PL AND FILL PL", T34' origin, 1" = 1'-0 = 6 pt/in):
    // sides at +-11.0" from the T0'-T34' chord (solid outer edge y = +11.00, dashed inner edge y = -11.00), end edge parallel to the end flange edge
    // (joint) 11.76" past it, acute end corner cut from (26.58, -11.00) to (35.30, -2.30) = 13 1/4" along the side x 9 13/16" along the end; end flange
    // bolt rows +-9 1/2" (1 1/2" from the edges) "7 SPA @ 4 3/8" (printed), first hole 2.82" from the end web outside face along the chord (vector).
    // p.125 (SW) shows the same plate mirrored (end edge at the obtuse corner 28.6" from T0, 10.15" inside / scaled) -> one rule for the 4 corners.
    SpliceHalfWidth = In(11), SpliceBeyondJoint = In(11.75), SpliceChamferSide = In(13.25), SpliceChamferEnd = In(9.8125),
    SpliceBoltRowOffset = In(9.5), SpliceBoltFirst = In(2.8125), SpliceBoltCount = 8, SpliceBoltPitch = In(4.375),
    SpliceUnresolved = U("p.124 / 125", "knuckle splice / fill plate outline", "the 1\" splice plate and 1 1/8\" fill plate are drawn dashed in the partial plans but only the bolt pitch is printed; the splice-to-tie flange bolts along the joint (3 SPA @ 3) are not dimensioned to the tie",
        "Outline 1'-10 wide, end web to 11 3/4\" past the joint, acute corner cut 13 1/4 x 9 13/16 (VECTOR linework p.124, to scale); end flange bolt rows modeled, splice-to-tie bolts not modeled."),
    CheeseUnresolved = U("p.124 / 125 Section A-A", "knuckle jack cheese plates", "the 1\" cheese plates at the jacks (1'-1 each side of CL bearing) are drawn ~10 1/4\" long but not dimensioned in plan", "10 1/4\" x 10 1/4\" centred on the jacks."),
};
var studRows = new List<ArchPoint>();
for (int i = -3; i <= 5; i++) studRows.Add(new ArchPoint($"R{i + 4}", In(23.875) + i * In(10.625), 0, i < 0 ? In(8) : In(7)));
string dsWhy = U("p.126", "knuckle end plate stud positions", "the rows are 10 5/8\" apart (dimensioned) but the first row offset and the column lines (6 @ 8 3/8, 5 @ 7 1/2, 4 @ 8 3/4 along the FB1 line, plus pairs at the tendons) are not reproduced", "Rows from 5 3/8\" inside the end flange edge (scaled), 9 1/2\" along the rows (scaled), 3\" clear of the plate edges.");
double[] openings = { -In(49.5), -In(31.5), In(31.5), In(49.5) };
double[] webStuds = { -97.25, -87.75, -78.25, -68.75, -59.25, -16.25, -6.75, 2.75, 12.25, 21.75, 64.75, 74.25, 83.75, 93.25, 102.75 };
ArchKnuckleDefinition Knuckle(string name, string from, string to, string[] deck, ArchKnuckleCorner[] corners)
{
    var k = new ArchKnuckleDefinition { Name = name, EndFrom = from, EndTo = to, DeckPlate = deck.ToList(), EndWebScaledHeightAtEnds = In(10.25), EndWebUnresolved = ewWhy,
        DeckPlateTopZ = In(0.875) + In(1), Detail = kDetail, EndWebOpeningsAt = openings.ToList(), EndWebStudsAt = webStuds.Select(In).ToList(),
        DeckPlateEdgeFromTieCL = In(1), DeckPlateEdgeFromFloorbeamCL = In(1), DeckPlateOverEndFlange = In(3),
        DeckStudRows = studRows, DeckStudSpacing = In(9.5), DeckStudEdge = In(3), DeckStudUnresolved = dsWhy };
    k.Corners.AddRange(corners);
    return k;
}
arch.Knuckles.Add(Knuckle("West", "T34'", "T0", new[] { "T34'", "T33'", "T1", "T0" }, new[]
{
    new ArchKnuckleCorner { Point = "T34'", Rib = "North rib", Tie = "North tie", Splice = "RS5'", OutsideLegHeight = In(9.9375), InsideLegHeight = In(10.1875) },
    new ArchKnuckleCorner { Point = "T0", Rib = "South rib", Tie = "South tie", Splice = "RS1", OutsideLegHeight = In(10.5), InsideLegHeight = In(10.5) },
}));
arch.Knuckles.Add(Knuckle("East", "T34", "T0'", new[] { "T34", "T33", "T1'", "T0'" }, new[]
{
    new ArchKnuckleCorner { Point = "T34", Rib = "South rib", Tie = "South tie", Splice = "RS5", OutsideLegHeight = In(9.9375), InsideLegHeight = In(10.1875) },
    new ArchKnuckleCorner { Point = "T0'", Rib = "North rib", Tie = "North tie", Splice = "RS1'", OutsideLegHeight = In(10.5), InsideLegHeight = In(10.5) },
}));

// ---------------------------------------------------------------- deck (p.151-152, L.B. Foster 3" bridge decking), curbs, PT (data)
var deck = new ArchDeckDefinition
{
    RightTie = "South tie", LeftTie = "North tie", EndKnuckles = { "West", "East" },
    FormThickness = In(0.1046), FormDescription = "3\" x 8\" 12 ga galvanized steel deck form (Gr 33, G165), L.B. Foster 3\" bridge decking",
    // p.152 "corrugated steel deck panel": 3" deep, 8" pitch, 2'-0 cover; pans 5 3/8 wide at the bottom, ribs 2 5/8 at the base / 1 1/2 at the top.
    FormTopFlat = In(1.5), FormFluteBottom = In(5.375), FlutesOnBarGrid = true,
    ConcreteStrengthKsi = 5,
};
// Deck bars (p.152 section, p.154 bill): A1 #5 13'-10 top transverse @ 8 between the curbs, B1 #5 18'-2 bottom transverse @ 8, 27 - D1 (top) / D2 (bottom, one
// in each corrugation) @ 8 = 17'-4, #5 40'-0 lapped 2'-8 min, curbs S1 #4 @ 8 (6 x 9 closed) with 4 - D4; covers 2 1/2 top, 1 3/4 above the deck pans.
deck.Rebar = new ArchDeckRebar
{
    TopTransLength = FI(13, 10), BottomTransLength = FI(18, 2), TransSpacing = In(8), LongCount = 27, LongSpacing = In(8), LongBarLength = Ft(40), LongLap = FI(2, 8),
    TopCover = In(2.5), BottomClear = In(1.75), EndCover = In(2), CurbStirrupWidth = In(6), CurbStirrupHeight = In(9), CurbTopCover = In(2),
    // rebar fix (2026-10-12): p.152 shows TWO A1 per row - lapped 2'-8" min at CL NHT, each hooked down at its tie with a return lapping B1 ("hook top bar with
    // 2'-8" lap to bottom bar") = 2 x 303 = 606; rows = the bill's 303 (A1 / B1, S1 2 x 303); D3 (68 = 2 ties x 34 floorbeam panels, BAR D3 5'-9" + 5" hooks)
    // in section at the tie web under the studs at the bottom bar level (6" from CL tie SCALED). Hook drop leg 7 1/2" inside CL tie SCALED (clears the 7" studs).
    TransCount = 303, CurbStirrupCount = 303, CurbStirrupHook = In(4.5),TopTransPerRow = 2, TopTransHookOffset = In(7.5), TopTransHookReturn = FI(2, 8),
    EdgeBarMark = "D3", EdgeBarLength = FI(5, 9), EdgeBarHook = In(5), EdgeBarOffset = In(6),
    Unresolved = U("p.151 / 152 / 154", "arch deck bar layout details", "the lap positions of the 40'-0 D1 / D2 / D4 bars (type BT, bent to the knuckle stud grid on p.151) are not dimensioned, the A1 hook position and the D3 position are drawn, not dimensioned, and 303 rows (bill) over the deck length exceed the 8\" maximum spacing slightly", "Lines lapped 2'-8 from the west end with a shorter last bar; A1 hook 7 1/2\" and D3 6\" inside CL tie (SCALED); the bill row count is modeled; bill differences are CHECK rows."),
    PlanList = { new ArchPoint("A1", 606, FI(13, 10), 0), new ArchPoint("B1", 303, FI(18, 2), 0), new ArchPoint("D1", 162, Ft(40), 0), new ArchPoint("D2", 162, Ft(40), 0),
        new ArchPoint("D3", 68, FI(7, 5), 0), new ArchPoint("D4", 48, Ft(40), 0), new ArchPoint("S1", 606, FI(3, 3), 0), new ArchPoint("S2", 32, FI(2, 9), 0) },
};
// Longitudinal PT (p.155): 4 tendons of 4 - 0.6" strands (DYWIDAG flat anchorage drawn with 4 wedges; 170 kips jacking = 4 x 0.217 x 270 x 0.725), single-end
// stressing; S2 anchorage-zone bars at 1'-0, 1'-4, 2'-10 1/2, 3'-2 1/2 from the end web.
deck.PtSystem = new ArchPtSystem
{
    StrandsPerTendon = 4, StrandDiameter = In(0.6), StrandArea = In(1) * In(1) * 0.217, JackingForceKips = 170, StrandSpacing = In(1.9375),
    AnchorLength = In(10.5), AnchorWidth = In(13), AnchorHeight = In(4),
    ZoneBarsAt = { In(12), In(16), In(34.5), In(38.5) }, ZoneBarWidth = In(9.25), ZoneBarHeight = In(6),
    Unresolved = U("p.155", "PT strand arrangement and anchorage envelope", "the flat duct, the strand spacing in it and the anchorage casting are drawn (Section A-A / B-B) but not dimensioned", "Strands side by side at 1 15/16\", anchorage 13\" x 4\" x 10 1/2\" (scaled); duct not modeled."),
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
// Plan orientation: the masonry plate (3'-2 x 1'-5) sits in the 3'-4 x 1'-7 arch pedestal whose long axis is 63 deg to the pier line (p.102 plan, TYP;
// both piers, in the pier frame S right / N up-station as the approach model's pedestals) -> the bearing plates and anchor bolts follow it.
void Brg(string name, string support, string tpt, double seatFt) => arch.Bearings.Add(new ArchBearingDefinition { Name = name, Support = support, Point = "BRG-" + name, SeatElevation = F(seatFt), SteelUndersideZ = -In(1.125) - In(1), HangFromSteel = true,
    LongAxisAngleDeg = 63, LongAxisSource = "p.102 pier plan: arch span pedestal 63 deg (TYP) to the CL pier; p.107 bearing plan inside it" });
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
    // U6 (bracing part) RESOLVED by the p.129-140 sections: the U point is the WP on the outer face of the 7/8" connection plate (B-B: 8 1/4" from CL rib,
    // A-A: 3/4 - 1 1/4" radially off the web CL); checked per point by the generator (CHECK rows "WP to the connection plate face").
    U("p.152, 143", "tie girder web shear studs", "the typical arch deck section (p.152) shows a horizontal 7/8\" x 7\" stud on the inside face of each tie web and the tie quantity (p.143 note 1) includes shear studs, but no spacing / count is given on any sheet", "Not modeled (the tie quantity check is short by them)."),
    "CONFLICT (p.147 vs p.148, p.120): the p.147 table labels H(n) 'arch rib working point' and HC(n) 'arch rib cable anchor point', but HC(n) lies on the rib working line (R 126'-0 through the RS points, within 0.0004') and H(n) lies 10\" inside it on the cable axis, P1 off the arch plane - exactly the p.148 fin plate pin (P2 sin B = 10\", P1). Modeled: rib pin = H(n), strand and sockets pin to pin; the p.147 cable length (TC to HC) is checked to HC(n) (U6 resolved for the hangers).",
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

if (!testModel) { var rj = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\rail-joints-nht.csx", Doc, Args); Print(rj.Output.TrimEnd()); if (!rj.Success) Print("ERROR in rail-joints-nht.csx: " + rj.Error); }   // RAIL (PED)(HIGH) on the arch curbs
var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
foreach (var w in rep.Warnings.Where(w => w.StartsWith(br.Name))) Print("warning " + w);
am = Doc.Steel(br.Name).Arch!;
Print($"Arch: {am.Steel.Parts.Count} parts, {am.Steel.SteelMass * 2.20462:N0} lb, {am.Hangers.Count} hangers, {am.Floorbeams.Count} floorbeams, {am.Bearings.Count} bearings, deck {am.SlabArea / 0.09290304:N0} SF (plan 3,698)");
foreach (var c in am.Checks.Where(c => !c.Ok)) Print($"CHECK {c.Item}: plan {c.Plan} model {c.Model}");
Doc.Save(outPath);
Print($"Saved {outPath}");
