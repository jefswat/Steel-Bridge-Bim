// Iowa DOT Design 1617 (File 30377, Woodbury County / Sioux City): NEBRASKA ST. RAMP B over Virginia St. and Virginia St. Ramp D ("911").
// 570'-0 x 26'-0 continuous welded plate girder bridge, weathering steel, spans 180'-0 / 203'-0 / 187'-0, 4 girders A-D at 7'-8, straight
// (tangent) from the S abutment to the P.C., curved (R 1500) after; Pier 1 = concrete 2-column pier with cap; Pier 2 = 2 turned columns with an
// INTEGRAL STEEL TWIN I-GIRDER CAP into which all four girders frame; 14 disc bearings; steel extrusion joints at both abutments.
// Plan data: docs/plan-data/nebraska-ramp-b-b00167/ (SUMMARY.md + CSVs). PDF: C:\SBMData\B00167 - Nebraska Ramp B - Design 116.pdf (PDF page = sheet).
//   sbm new "samples\plans\Nebraska St Ramp B.sbm"
//   sbm run samples\plans\build-nebraska-st.csx "samples\plans\Nebraska St Ramp B.sbm" -- "samples\plans\Nebraska St Ramp B.sbm"
//   sbm run samples\plans\check-nebraska-st.csx "samples\plans\Nebraska St Ramp B.sbm"
// Rules: plan values only; what the plans do not settle is printed / raised as "UNRESOLVED (p.N): item - reason"; differences are reported, never adjusted.
using System.Globalization;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\Nebraska St Ramp B.sbm");
Func<string, double> S = Sta;
double L(string s) => Len(s.Contains('\'') || s.Contains('"') ? s : s + "\"");   // "29'-7 13/16" or inches "13 1/2"
double Deg(int d, int m, double s) => (d + m / 60.0 + s / 3600.0) * Math.PI / 180;
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double I(double inch) => In(inch);
var unresolved = new List<string>();
void U(string pages, string item, string why, string then = "") { var t = SBM.Core.UnresolvedFlag.Text(pages, item, why, then); unresolved.Add(t); }
string PlanDir()
{
    for (var d = new System.IO.DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var q = System.IO.Path.Combine(d.FullName, "docs", "plan-data", "nebraska-ramp-b-b00167");
        if (System.IO.Directory.Exists(q)) return q;
    }
    throw new System.IO.DirectoryNotFoundException("docs/plan-data/nebraska-ramp-b-b00167 not found above the current directory - run from the repo.");
}
string dataDir = PlanDir();
List<string[]> Csv(string name) => System.IO.File.ReadAllLines(System.IO.Path.Combine(dataDir, name)).Skip(1).Where(l => l.Trim().Length > 0)
    .Select(l => System.Text.RegularExpressions.Regex.Matches(l, "\"[^\"]*\"|[^,]+|(?<=,)(?=,)|^(?=,)").Select(m => m.Value.Trim('"')).ToArray()).ToList();
double Num(string s) => double.Parse(s.Trim(), CultureInfo.InvariantCulture);

// ================================================================ alignment (situation plan p.6 / DS 5)
// Curve 1: PI 2472+49.63, Delta 1 deg 07'20.70" LT, R 2514.53, PC 2472+25.00, PT 2472+74.26; tangent to PC 2476+53.67;
// Curve 2: PI 2479+06.35, Delta 19 deg 07'26.97" LT, R 1500.00, PT 2481+54.34. No spirals, no station equations.
// PLACEMENT (APPROXIMATE, U1): no N/E in the set. NAD83 / UTM zone 14N (metres) from (a) the p.6 LOCATION lat 42.488579 / long -96.398648
// = E 713788.48 N 4707304.17, taken as CL Ramp B at STA 2475+52.56 (the structure's centre station in every title block; the location block does not
// name its point) and (b) the north arrows of p.27 / p.29 (vector geometry: the middle tangent PT1-PC2 at TRUE azimuth 296.262 deg on both sheets),
// minus the UTM grid convergence at the point (computed below) -> grid azimuth. Exact coordinates = road plans IM-NHS-029-7(49).
double R1 = Ft(2514.53), d1 = Deg(1, 7, 20.70), R2 = Ft(1500.00), d2 = Deg(19, 7, 26.97);
double T1 = R1 * Math.Tan(d1 / 2), T2 = R2 * Math.Tan(d2 / 2);
double startSta = S("2468+00"), endPast = Ft(300);
double lat = 42.488579, lon = -96.398648;
double conv = Math.Atan(Math.Tan((lon - (-99.0)) * Math.PI / 180) * Math.Sin(lat * Math.PI / 180)) * 180 / Math.PI;   // UTM 14N, CM -99
double trueAz = 296.262, gridAz = trueAz - conv;
var mid = new Vec2(0, 1);                         // construction frame: middle tangent heading north
var before = mid.Rotate(-d1);                     // left curves: heading increases (CCW) along the curve
var after = mid.Rotate(d2);
var pt1 = new Vec2(0, 0);
var pi1 = pt1 - mid * T1;
var pStart = pi1 - before * (S("2472+49.63") - startSta);
var pc2 = pt1 + mid * (S("2476+53.67") - S("2472+74.26"));
var pi2 = pc2 + mid * T2;
var pEnd = pi2 + after * (T2 + endPast);
var anchorLocal = pt1 + mid * (S("2475+52.56") - S("2472+74.26"));
var anchorUtm = new Vec2(713788.48, 4707304.17);
double rot = (360 - gridAz) * Math.PI / 180;
Vec2 Geo(Vec2 p) => anchorUtm + (p - anchorLocal).Rotate(rot);
// stationing: the PI1 station puts PT1 at 2472+74.26 only if the plan's T / L agree; checked below.
Doc.Project.Alignments.RemoveAll(a => a.Name == "CL Nebraska St. Ramp B");
// PT1 station from the PI station: PT = PI - T + L.
double ptSta = S("2472+49.63") - T1 + R1 * d1;
var shift = mid * (S("2472+74.26") - ptSta);      // move along the middle tangent so PT1 is at the plan station (the PI station then differs by the same)
var al = Doc.AddAlignment("CL Nebraska St. Ramp B", startSta + 0, new[]
{
    new PiRow(Geo(pStart).Y, Geo(pStart).X), new PiRow(Geo(pi1).Y, Geo(pi1).X, R1), new PiRow(Geo(pi2).Y, Geo(pi2).X, R2), new PiRow(Geo(pEnd).Y, Geo(pEnd).X),
});
al.Description = "CL Nebraska St. Ramp B = P.G.L. (Iowa DOT Design 1617) - APPROXIMATE placement in NAD83 / UTM 14N (m) from the p.6 GPS point + the p.27/p.29 north arrows";
{
    double pc1 = S("2472+49.63") - T1, pt1s = pc1 + R1 * d1;
    Print($"Curve 1: T {T1 / 0.3048:F2}' (plan 24.63), L {R1 * d1 / 0.3048:F2}' (49.26), PC {Doc.Units.FormatStation(pc1)} (2472+25.00), PT {Doc.Units.FormatStation(pt1s)} (2472+74.26)");
    Print($"Curve 2: T {T2 / 0.3048:F2}' (252.68), L {R2 * d2 / 0.3048:F2}' (500.67), PT {Doc.Units.FormatStation(S("2476+53.67") + R2 * d2)} (2481+54.34)");
    Print($"Placement (APPROXIMATE): CL STA 2475+52.56 at UTM14N E {Geo(anchorLocal).X:F2} N {Geo(anchorLocal).Y:F2}; middle tangent true az {trueAz:F3}, convergence {conv:F3}, grid az {gridAz:F3} deg");
    // stationing check: the middle tangent point at the PT station must be the PT
    var q = al.PointAt(S("2472+74.26"), 0); var qq = Geo(pt1);
    Print($"PT1 position check (alignment station 2472+74.26 vs construction PT): {(q - qq).Length * 1000:F1} mm");
}
U("p.6", "project coordinates (U1)", "the set has no N/E or state-plane data and the location block does not name the point of its lat/long",
    "APPROXIMATE placement: lat/long = CL at STA 2475+52.56 (structure centre station), heading from the p.27/p.29 north arrows (true north), UTM 14N grid.");

// Profile on CL = P.G.L.: PVI 2473+62.45 el 1125.25 VC 274.90 (G1 -0.2855 %, G2 -1.2973 %); PVI 2479+00.00 el 1118.28 VC 200 (G3 -2.6315 %).
double g1 = -0.002855, g3 = -0.026315;
double endSta = S("2481+54.34") + endPast;
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(startSta, Ft(1125.25) - g1 * (S("2473+62.45") - startSta)),
        new Vpi(S("2473+62.45"), Ft(1125.25), Ft(274.90)),
        new Vpi(S("2479+00.00"), Ft(1118.28), Ft(200)),
        new Vpi(endSta, Ft(1118.28) + g3 * (endSta - S("2479+00.00"))),
    },
};
Print($"Profile G2 between the VPIs: {(1118.28 - 1125.25) / 537.55 * 100:F4} % (plan -1.2973 %)");
// Superelevation (p.6-7): one plane rising to the RIGHT, pivot = CL / P.G.L.: 5 % at 2472+27.46 -> 3 % at 2472+89.86 (62.40'), 3 % -> 5 % 2476+39.57 to 2476+95.97.
al.CrossSlope = new CrossSlopeTable
{
    PivotOffset = 0,
    Rows =
    {
        new CrossSlopeRow(S("2472+27.46"), -0.05, 0.05), new CrossSlopeRow(S("2472+89.86"), -0.03, 0.03),
        new CrossSlopeRow(S("2476+39.57"), -0.03, 0.03), new CrossSlopeRow(S("2476+95.97"), -0.05, 0.05),
    },
};

// ================================================================ bridge, supports (staking p.8 / framing p.27)
// CL S abut brg 2472+67.56 on curve 1, RADIAL (p.8 / p.27: 0 deg 09'09" = 89 deg 50'51" to the tangent = 6.70 ft before the PT on R 2514.53);
// CL pier 1 2474+47.56, 6 deg 19'46" to the girders (= the tangent), right end up-station; CL pier 2 = CL integral steel pier cap 2476+50.56, square;
// CL N abut brg 2478+37.56, radial to curve 2. Spans along CL 180'-0 / 203'-0 / 187'-0.
const string Structure = "Nebraska St. Ramp B (Design 1617)";
const string BridgeName = "Nebraska St. Ramp B";
Doc.Project.Bridges.RemoveAll(b => b.Structure == Structure);
var br = new BridgeDefinition { Name = BridgeName, AlignmentName = al.Name, Structure = Structure };
br.FieldSplices.Clear();
SupportDefinition Sup(string name, SupportType t, string sta, double skew, BearingFixity fx) => new()
{
    Name = name, Type = t, Station = S(sta), SkewDegrees = skew, Fixity = fx, GirderEndExtension = F(1),
};
br.Supports.Add(Sup("S. Abut.", SupportType.Abutment, "2472+67.56", 0, BearingFixity.GuidedExpansion));
br.Supports.Add(Sup("Pier 1", SupportType.Pier, "2474+47.56", 6 + 19 / 60.0 + 46 / 3600.0, BearingFixity.Fixed));
br.Supports.Add(Sup("Pier 2", SupportType.Pier, "2476+50.56", 0, BearingFixity.GuidedExpansion));
br.Supports.Add(Sup("N. Abut.", SupportType.Abutment, "2478+37.56", 0, BearingFixity.GuidedExpansion));
// Deck ends: the slab is 572'-7 1/2 out to out = 1'-4 + 570'-0 + 1'-3 1/2 from the CL abutment bearings along CL (p.43 / DS 42); radial supports.
br.Supports[0].DeckEndOffset = F(1, 4);
br.Supports[3].DeckEndOffset = F(1, 3.5);
// Steel extrusion joint with neoprene gland (Iowa std 1026, p.48-49): opening at 50 F 2 1/2 (S) / 3 (N) between the deck end and the backwall (p.26).
foreach (var (k, gap) in new[] { (0, 2.5), (3, 3.0) })
    br.Supports[k].ExpansionJoint = new ExpansionJointDefinition
    {
        Type = ExpansionJointType.Sealed, GapAt50F = I(gap), BlockoutWidth = 0, Designation = "Steel extrusion joint with neoprene gland (Iowa std. 1026, F-shape)",
        Unresolved = "UNRESOLVED (p.48-49): extrusion / anchorage dimensions are in Iowa standard 1026, not in this set - the device is an envelope of the 50 F opening.",
        Basis = $"opening {gap} in at 50 F (p.26 settings table)",
    };

// ---- deck (typical section p.24 / DS 23, std 4305 mod.)
br.Deck.LeftEdgeOffset = -F(21, 7);          // 20'-0 + 1'-7 left of CL / PGL
br.Deck.RightEdgeOffset = F(7, 7);           // 6'-0 + 1'-7
br.Deck.Thickness = I(8);                    // 8" incl. 1/2" integral wearing surface
br.Deck.Haunch = I(3.5);                     // nominal haunch (11 1/2" top of deck to top of web)
br.Deck.OverhangThickness = I(9);
br.Deck.StayInPlaceForms = false;
br.Deck.Reinforcement.Enabled = false;       // deck bars: bar list only (bar-lists.csv), bend diagrams not transcribed (G9)
br.Deck.EdgeStrip = new DeckEdgeStrip { Width = F(1, 7) };   // deck under the rail level where the deck slopes toward the rail (p.24)

// ---- girders (framing plan p.27, girder elevation p.28, field splices p.34)
br.Girders.Count = 4;
br.Girders.PlanNames = new() { "A", "B", "C", "D" };
double[] off = { -F(18, 6), -F(10, 10), -F(3, 2), F(4, 6) };   // C is 3'-2 LEFT of CL, D 4'-6 right (p.24, p.27)
br.Girders.FirstGirderOffset = off[0];
br.Girders.Spacings = Enumerable.Repeat(F(7, 8), 3).ToList();
br.Girders.PlanLayout = GirderPlanLayout.CurvedParallel;
// "Girders A thru D are detailed TANGENT between CL south abutment and P.T." (p.27 note) and curved concentric with CL from the P.C. (R 1481'-6 ..
// 1504'-6): as-designed plan geometry = the tangent line at the girder's offset, then a left arc R = 1500 + offset (the first element extends back).
br.Girders.PlanGeometry = new();
for (int g = 0; g < 4; g++)
    br.Girders.PlanGeometry.Add(new GirderPlanGeometry
    {
        Girder = g, StartStation = S("2474+00"), StartOffset = off[g], StartDirectionDegrees = 0,
        Elements =
        {
            new GirderPlanElement { Kind = GirderPlanElementKind.Line, Length = S("2476+53.67") - S("2474+00") },
            new GirderPlanElement { Kind = GirderPlanElementKind.ArcLeft, Radius = Ft(1500) + off[g], Length = Ft(400) },
        },
    });
var sec = br.Girders.Section;
sec.WebDepth = I(76); sec.WebThickness = I(9.0 / 16);
sec.TopFlange = new PlateSize(I(18), I(1)); sec.BottomFlange = new PlateSize(I(22), I(1));
PlateRun R(GirderPlate pl, double w, double t, int fs) => new() { Plate = pl, Width = I(w), Thickness = I(t), EndSplice = fs };
PlateRun RS(GirderPlate pl, double w, double t, int sup, double offFt) => new() { Plate = pl, Width = I(w), Thickness = I(t), EndSupport = sup, EndOffset = Ft(offFt) };
const GirderPlate TF = GirderPlate.TopFlange, BF = GirderPlate.BottomFlange;
// p.28: plate breaks at the field splices and the shop splices 20'-0 before / 19'-0 after CL pier 1 and 23'-3 before / 20'-6 after CL pier 2 (all girders).
sec.Layouts = new()
{
    new GirderPlateLayout
    {
        Girders = new() { 0, 1, 2, 3 },
        Runs =
        {
            R(TF, 18, 1, 1), RS(TF, 22, 1, 1, -20), RS(TF, 22, 2, 1, 19), R(TF, 22, 1.125, 2), R(TF, 16, 0.875, 3),
            RS(TF, 24, 1.125, 2, -(23 + 3 / 12.0)), RS(TF, 24, 2, 2, 20.5), R(TF, 24, 1, 4), R(TF, 24, 1, 0),
            R(BF, 22, 1, 1), RS(BF, 22, 1.125, 1, -20), RS(BF, 22, 2, 1, 19), R(BF, 22, 1.25, 2), R(BF, 20, 1, 3),
            RS(BF, 24, 1.125, 2, -(23 + 3 / 12.0)), RS(BF, 24, 2.25, 2, 20.5), R(BF, 24, 1.125, 4), R(BF, 26, 1.5, 0),
        },
    },
};
// Field splices (p.34 schedule; locations p.27 / p.28): FS1 51'-0 before CL pier 1, FS2 54'-6 after, FS3 59'-0 before CL pier 2 (all girders);
// FS4 = CL pier 2 to P.C. 3'-1 5/16 + P.C. to FS4 along each girder (girder-data.csv). First bolt 2" from the joint, plates end 1 1/2" past the last bolt
// (2'-11 = 2 x (2 + 7 x 2 + 1 1/2)), 1/4" gap (p.34), 7/8" bolts in 15/16" holes (p.24 notes). Flange bolt lines from the flange CL (p.34 details):
// 16" - 18" flanges: 2 1/2 and edge - 1 1/2 (gage between), staggered; 20" - 24" flanges: 3, then the gage(s), 2" edge.
br.FieldSpliceGap = I(0.25);
br.Bolts.Diameter = I(0.875); br.Bolts.Grade = "A325 Type III";
FlangeSplicePattern Fl(double to, double wo, double ti, double wi, int n, double s, double[] lines, bool stag) => new()
{
    OuterThickness = I(to), OuterWidth = I(wo), InnerThickness = I(ti), InnerWidth = I(wi), Spaces = n, Spacing = I(s), LineOffsets = lines.Select(I).ToList(), Staggered = stag,
};
WebSplicePattern Web(double t, int rows, double rs, double sTop) => new() { PlateThickness = I(t), ColumnSpaces = 1, ColumnSpacing = I(3), RowSpaces = rows, RowSpacing = I(rs), TopRowBelowWebTop = I(sTop) };
FieldSpliceDetail Det(FlangeSplicePattern top, WebSplicePattern web, FlangeSplicePattern bot) => new() { BoltDiameter = I(0.875), FirstBoltFromJoint = I(2), PlateEndDistance = I(1.5), Top = top, Web = web, Bottom = bot };
var fsd = new Dictionary<int, FieldSpliceDetail>
{
    [1] = Det(Fl(7.0 / 16, 18, 0.5, 8, 7, 2, new[] { 2.5, 7.5 }, true), Web(5.0 / 16, 21, 3.25, 3.875), Fl(0.5, 22, 9.0 / 16, 10, 4, 3, new[] { 3.0, 9 }, false)),
    [2] = Det(Fl(3.0 / 8, 16, 7.0 / 16, 7, 7, 2, new[] { 2.5, 6.5 }, true), Web(5.0 / 16, 21, 3.25, 3.875), Fl(0.5, 20, 9.0 / 16, 9, 5, 3, new[] { 3.0, 8 }, false)),
    [3] = Det(Fl(3.0 / 8, 16, 7.0 / 16, 7, 7, 2, new[] { 2.5, 6.5 }, true), Web(5.0 / 16, 21, 3.25, 3.875), Fl(0.5, 20, 9.0 / 16, 9, 4, 3, new[] { 3.0, 8 }, false)),
    [4] = Det(Fl(7.0 / 16, 24, 0.5, 11, 3, 3, new[] { 3.0, 6.5, 10 }, false), Web(5.0 / 16, 23, 3, 3.5), Fl(0.5, 24, 9.0 / 16, 11, 4, 3, new[] { 3.0, 6.5, 10 }, false)),
};
var gd = Csv("girder-data.csv");
double GD(string g, int span, string seg) => Ft(Num(gd.First(r => r[0] == g && r[1] == span.ToString() && r[2] == seg)[4]));
string[] GL = { "A", "B", "C", "D" };
var fsLoc = new (int no, int sup, Func<string, double> off)[]
{
    (1, 1, g => -GD(g, 1, "FS1 to CL pier 1")),
    (2, 1, g => GD(g, 2, "CL pier 1 to FS2")),
    (3, 2, g => -GD(g, 2, "FS3 to CL pier 2")),
    (4, 2, g => GD(g, 3, "CL pier 2 to PC") + GD(g, 3, "PC to FS4")),
};
foreach (var (no, sup, fo) in fsLoc)
{
    var fs = new FieldSpliceDefinition { Number = no, Detail = fsd[no].Clone(), Station = br.Supports[sup].Station + fo("A") };
    for (int g = 0; g < 4; g++) fs.Girders.Add(new GirderSpliceLocation { Girder = g, Support = sup, Offset = fo(GL[g]) });
    br.FieldSplices.Add(fs);
}

// ---- stiffeners (p.27, p.32): bearing stiffeners per support, jacking stiffeners 1'-5 from CL brg at the abutments (toward the span) and 3'-3 each side
// at pier 1; connection plates PL 1/2 x 7 1/2; intermediate PL 7/16 x 6 1/2 at equal spaces between cross frames (p.27 note).
br.Stiffeners.BearingStiffener = new PlateSize(I(10.5), I(1));
br.Supports[0].BearingStiffener = new PlateSize(I(10.5), I(1));
br.Supports[1].BearingStiffener = new PlateSize(I(10.5), I(1));
br.Supports[3].BearingStiffener = new PlateSize(I(12.5), I(1.125));
br.Supports[0].JackingStiffenerOffsets = new() { F(1, 5) };
br.Supports[1].JackingStiffenerOffsets = new() { -F(3, 3), F(3, 3) };
br.Supports[3].JackingStiffenerOffsets = new() { -F(1, 5) };
br.Stiffeners.ConnectionPlate = new PlateSize(I(7.5), I(0.5));
br.Stiffeners.IntermediateStiffener = new PlateSize(I(6.5), I(7.0 / 16));
br.Stiffeners.CornerClip = I(1.5);
br.Stiffeners.ClipToTopFlange = true;   // p.32 Sect. A-A: bearing / jacking stiffeners sized for the bottom flange overhang the narrower top flange at the abutments (1.78" S, ~0.8" N); outer top corner clipped 45 deg to the flange edge, top weld stops there
br.Stiffeners.FlangeToWebWeld = I(5.0 / 16);
// INTERMEDIATE STIFFENERS as drawn on p.27 (vector linework re-read 2026-10-20, user decision d35): short ticks only in the abutment panels at 9'-6 from the CL bearing
// ("9'-6 (TYP.)", both abutments), in the two panels on each side of Pier 1 and of Pier 2 (the cap) at mid panel, and one extra on girder D in the FS 4 panel; none in the
// FS 1 / FS 2 / FS 3 panels = 10 per girder, 11 on D. (The generator's automatic mid-panel rule put 25 per girder and 7 clash groups with the splice plates.)
{
    var il = new List<IntermediateStiffenerLocation>();
    // spans: 0 = S abutment - Pier 1, 1 = Pier 1 - Pier 2 (cap), 2 = Pier 2 - N abutment
    il.Add(new() { Span = 0, Reference = StiffenerLocationReference.SpanBegin, Distance = F(9, 6) });
    foreach (int pn in new[] { -2, -1 }) il.Add(new() { Span = 0, Reference = StiffenerLocationReference.Panel, Panel = pn });
    foreach (int pn in new[] { 0, 1 }) il.Add(new() { Span = 1, Reference = StiffenerLocationReference.Panel, Panel = pn });
    foreach (int pn in new[] { -2, -1 }) il.Add(new() { Span = 1, Reference = StiffenerLocationReference.Panel, Panel = pn });
    foreach (int pn in new[] { 0, 1 }) il.Add(new() { Span = 2, Reference = StiffenerLocationReference.Panel, Panel = pn });
    il.Add(new() { Span = 2, Reference = StiffenerLocationReference.SpanEnd, Distance = F(9, 6) });
    il.Add(new()
    {
        Girder = 3, Span = 2, Reference = StiffenerLocationReference.Splice, SpliceNumber = 4, Distance = F(3, 2),
        Unresolved = SBM.Core.UnresolvedFlag.Text("p.27", "girder D extra intermediate stiffener in the FS 4 panel (3'-2 ahead of the CL FS 4)", "the tick is drawn but not dimensioned; 3'-2 is scaled from the sheet (+-1\")",
            "Modeled 3'-2 ahead of CL FS 4 (clear of the splice plates by 22\" or more)."),
    });
    br.Stiffeners.IntermediateLocations = il;
}
// Shear studs 7/8 x 5, 3 per row (1 1/2, 2 eq spa, 1 1/2) (p.28, p.32): spacing groups not reproduced (generic pitch).
br.Studs.Diameter = I(0.875); br.Studs.RowsAcross = 3; br.Studs.TransverseSpacing = I(7.5); br.Studs.Pitch = I(12);
br.Studs.EdgeDistance = I(1.5);   // p.32 shear stud detail: 1 1/2 from the flange edge, 2 EQ. SPA. across (6 1/2 on the 16\" flange, 7 1/2 on 18\", 8 1/2 on 20\", ...)
// p.28 girder elevation (all girders): 1'-0 from the girder end, 24 EQ. SPA. = 19'-0, 90 EQ. SPA. to 1'-9 before FS 1; 84 between 1'-9 after FS 1 and 1'-9 before FS 2; 73 FS 2 - FS 3 (1'-9 each side);
// 40 from 1'-9 after FS 3 to 8'-6 before CL pier 2 (the cap, p.29 lays out the rest), 39 from 8'-6 after CL pier 2 to 1'-9 before FS 4; 95 from 1'-9 after FS 4 to 19'-0 + 1'-0 from the N end, 24 EQ. SPA. = 19'-0, 1'-0 to the end.
// A row where two zones meet is one row (475 rows per girder A). Rows inside a splice / cap exclusion are reported.
{
    var SL = StudLocationKind.Splice;
    StudZone Z(StudLocation a, StudLocation b, int n, string src) => new() { From = a, To = b, Spaces = n, Source = src };
    StudLocation Gs(double ft) => new(StudLocationKind.GirderStart, 0, Ft(ft));
    StudLocation Ge(double ft) => new(StudLocationKind.GirderEnd, 0, Ft(ft));
    StudLocation Fs(int n, double ft) => new(SL, n, Ft(ft));
    StudLocation Pc(int sup, double ft) => new(StudLocationKind.Support, sup, Ft(ft));
    br.Studs.Zones = new()
    {
        Z(Gs(1), Gs(20), 24, "p.28 24 EQ. SPA. = 19'-0"),
        Z(Gs(20), Fs(1, -1.75), 90, "p.28 90 EQ. SPA."),
        Z(Fs(1, 1.75), Fs(2, -1.75), 84, "p.28 84 EQ. SPA."),
        Z(Fs(2, 1.75), Fs(3, -1.75), 73, "p.28 73 EQ. SPA."),
        Z(Fs(3, 1.75), Pc(2, -8.5), 40, "p.28 40 EQ. SPA., 8'-6 to CL pier 2"),
        Z(Pc(2, 8.5), Fs(4, -1.75), 39, "p.28 39 EQ. SPA., 8'-6 from CL pier 2"),
        Z(Fs(4, 1.75), Ge(-20), 95, "p.28 95 EQ. SPA."),
        Z(Ge(-20), Ge(-1), 24, "p.28 24 EQ. SPA. = 19'-0"),
    };
}

// ---- flange deflectors (p.32 "Flange deflector details (6 required)", p.27 callouts): L4x3x5/16 + PL 3 x 5/16 x 0'-7, 2 bolts 3/4 in 13/16 holes, 30 deg line, 5'-0 from the CL bearing,
// "on the outside of the exterior girders at the abutment and piers as shown on the framing plan" = 6 = two exterior girders at three supports.
br.FlangeDeflectors = new FlangeDeflectorDefinition
{
    Source = "p.32 flange deflector details / p.27", Supports = new() { 1, 2, 3 },
    Unresolved =
    {
        SBM.Core.UnresolvedFlag.Text("p.27, p.32", "which supports carry the 6 flange deflectors", "p.32 says 'at the abutment and piers as shown on the framing plan' and p.27 only draws callouts at pier 1 and the N abutment",
            "Read as the supports where the grade runs DOWN toward the bearing along the girder (grade decreases with the stationing): pier 1, pier 2 and the N abutment, both exterior girders = 6; none at the S abutment."),
        SBM.Core.UnresolvedFlag.Text("p.32", "flange that carries the deflectors and the 5'-0 reference", "the detail shows a girder flange plan without saying top or bottom; the dimension runs to the CL bearing line with 'GRADE DECREASES' toward it",
            "Bottom flange (the top flange is under the deck), the web-end corner 5'-0 before the CL bearing (up-grade side)."),
    },
};

// ---- bearings (p.40-42 bearing data, bearings.csv): vendor-designed disc bearings -> GENERIC (height incl. the 1/8 preformed pad).
var bd = br.Bearings;
bd.Type = BearingType.Generic;
bd.SolePlateMinThickness = I(1); bd.SolePlateProjection = I(1);
bd.GenericBodyLength = I(16); bd.GenericBodyWidth = I(16); bd.MasonryPlateThickness = I(1.75); bd.MasonryPadThickness = I(0.125);
bd.AnchorBoltCount = 4; bd.AnchorEdgeDistance = I(3); bd.AnchorEdgeDistanceFixed = I(5); bd.AnchorHoleOversize = I(0.25);
var brgRows = Csv("bearings.csv");
foreach (var (k, loc) in new[] { (0, "S abutment"), (1, "Pier 1"), (2, "Pier 2"), (3, "N abutment") })
{
    var r = brgRows.First(x => x[0] == loc);
    var sb = bd.Clone();
    sb.GenericHeight = I(Num(r[6]));
    double X = L(r[18]), Y = L(r[19]);
    var bolt = System.Text.RegularExpressions.Regex.Match(r[20], @"4-(.+) dia").Groups[1].Value;
    sb.AnchorSpacingAcross = 2 * X; sb.AnchorSpacingAlong = 2 * Y;
    sb.AnchorBoltDiameter = L(bolt); sb.AnchorBoltEmbedment = L(r[21]);
    sb.AnchorBoltGrade = k is 1 or 2 ? "F1554 Gr. 55" : "F1554 Gr. 36";
    if (k == 2)
    {
        // Pier 2: under the integral steel cap, axis along the cap; disc 28" min dia (p.40); sole plate min 1 1/4 (p.41); anchors X along the cap, Y across.
        sb.GenericBodyLength = I(28); sb.GenericBodyWidth = I(28); sb.SolePlateMinThickness = I(1.25);
        sb.AnchorSpacingAlong = 2 * X; sb.AnchorSpacingAcross = 2 * Y;
    }
    br.Supports[k].Bearings = sb;
}
U("p.40-42", "bearing component sizes (sole / masonry plate, body) (U5)", "the disc bearings are designed by the manufacturer; only the total height (9\" / 14\"), the disc 28\" min. (pier 2) and the anchor bolts are given",
    "GENERIC bearings: body 16 x 16 (28 x 28 at pier 2), masonry plate 1 3/4, preformed pad 1/8 (sole plate = body length).");

// ---- cross frames (p.33 / DS 32 details; spacing along girder A p.27): all frames 90 deg to the girders.
CrossFrameStyle Frame(string name, CrossFrameStyleKind kind, string top, string diag, string bot, double gusset, int rows, int topRows, double topWp, double botWp) => new()
{
    Name = name, Kind = kind, TopChord = top, Diagonal = diag, BottomChord = bot, GussetThickness = I(gusset),
    BoltDiameter = I(0.875), BoltColumns = 2, BoltGage = I(3), BoltEdgeDistance = I(1.5), BoltPitch = I(3),
    BoltsPerConnection = rows, TopBoltRows = topRows, TopWorkPointOffset = I(topWp), BottomWorkPointOffset = I(botWp),
    BottomBoltOffset = I(Math.Max(0, 3 + (rows - 1) * 3.0 / 2 - botWp)),
};
// Abutment frame: W24x68 top strut 4" below the top of the top flange (p.33 "4 (TYP.)"; S abut top flange 1", N abut 1"), L5x5x1/2 diagonals from the
// bottom corners to the strut centre and bottom chord, 1/2" gussets, W bolted 7 rows (6 spa @ 3); pier 1 / intermediate: L5x5x3/8 top chord, L5x5x1/2
// V diagonals to the bottom chord centre and bottom chord, 5/8" gussets.
br.CrossFrames.Styles = new()
{
    Frame("ABUT", CrossFrameStyleKind.InvertedK, "W24x68", "L5x5x1/2", "L5x5x1/2", 0.5, 3, 7, (4 - 1) + 23.7 / 2, 6),
    Frame("INT", CrossFrameStyleKind.KFrame, "L5x5x3/8", "L5x5x1/2", "L5x5x1/2", 0.625, 2, 2, 6, 6),
};
br.CrossFrames.Styles[0].Unresolved = new() { "studs 7/8 x 5 on the W24x68 strut (7 eq. spa., p.33 Section A-A) are not modeled" };
br.CrossFrames.IntermediateStyle = "INT";
br.CrossFrames.Orientation = CrossFrameOrientation.NormalToGirders;   // p.27: intermediate cross frames 90 deg 00'00" (TYP.) to the girders (span 3 "with respect to the local tangent"); only the pier 1 frame sits on the pier line
br.CrossFrames.SupportStyles = new() { ["S. Abut."] = "ABUT", ["Pier 1"] = "INT", ["N. Abut."] = "ABUT" };   // Pier 2: girders frame into the cap (no support frames)
Doc.Project.Bridges.Add(br);

// ================================================================ Pier 2: integral steel twin I-girder cap (p.29-31 / DS 28-30) + capless columns (p.12-13)
var cap = new IntegralSteelCapDefinition
{
    Mark = "CAP2", Source = "p.29-31 / DS 28-30",
    // CL bearings on columns 1 / 2 along the cap from CL Ramp B (p.29 elevation: 29'-7 13/16 left, 17'-4 3/16 right; 47'-0 between).
    BearingOffsets = { -L("29'-7 13/16"), L("17'-4 3/16") },
    BearingTopElevations = { Ft(1111.55), Ft(1113.15) },              // p.42 bearing data (TOB = underside of the cap)
    BearingNames = { "Column No. 1", "Column No. 2" },
    EndOverhang = F(1, 6), PlanSlope = 0.0339,
    Members = 2, WebOffset = I(10 + 3.0 / 16), WebDepth = I(90), WebThickness = I(7.0 / 8),
    TopFlange = new PlateSize(I(20), I(1.75)), BottomFlange = new PlateSize(I(20), I(1.75)), PlanFlangeGap = I(3.0 / 16),
    Material = "ASTM A709 Gr. HPS 70W", SecondaryMaterial = "ASTM A709 Gr. 50W",
    BearingStiffener = new PlateSize(I(9), I(1.5)), JackingStiffenerOffset = F(5, 6), StiffenerClip = I(1), StiffenerTopNoWeld = I(4.375), StiffenerWeld = I(5.0 / 16), StiffenerBottomWeld = true,   // p.31 B-B / C-C: 5/16 + 5/16 at the bottom (mill to bear), 4 3/8 no weld at the top
    StudLength = I(9),
};
// camber diagram (p.29): from the chord between the CL bearings, 6 spa @ 7'-10, tolerances (-0, +x)
double[] cc = { 3.0 / 16, 3.0 / 8, 7.0 / 16, 3.0 / 8, 3.0 / 16 }, ct = { 1.0 / 16, 1.0 / 8, 3.0 / 16, 1.0 / 8, 1.0 / 16 };
for (int k = 0; k < 5; k++) cap.Camber.Add(new CapCamberPoint(F(7, 10) * (k + 1), I(cc[k]), 0, I(ct[k])));
// top cover plates (p.31 Detail B + View E-E): from the cap end to 5'-6 + 4'-5 (col 1) / 6'-2 (col 2) past the CL bearing; bolts 2" from the inner end,
// columns: 4 spa @ 3 from 3" off the cap end, 3 either side of the CL brg and of the jacking line, 20 spa @ 3, 16 (col 1) / 23 (col 2) spa @ 3.
// Rows (both covers): 2 3/16, 7 3/16 (3 off the web line 10 3/16), 13 3/16, 18 3/16 (2" edge) each side of the CL cap.
var coverRows = new List<double> { -18.1875, -13.1875, -7.1875, -2.1875, 2.1875, 7.1875, 13.1875, 18.1875 }.Select(I).ToList();
foreach (var (k, tail, n2) in new[] { (0, 53.0, 16), (1, 74.0, 23) })
{
    var cols = new List<double>();
    for (int i = 0; i < 5; i++) cols.Add(-15 + 3 * i);
    for (int i = 0; i <= 20; i++) cols.Add(3 + 3 * i);
    for (int i = 0; i <= n2; i++) cols.Add(69 + 3 * i);
    cap.CoverPlates.Add(new CapCoverPlate
    {
        Top = true, Bearing = k, From = -F(1, 6), To = F(5, 6) + I(tail), Width = I(40.375), Thickness = I(0.75),
        BoltColumns = cols.Select(I).ToList(), BoltRows = coverRows,
        // p.31 Detail B (read at 110 dpi 2026-10-19): 1'-6 cap end to CL bearing... CL bearing to the future jacking location 5'-6, then 4'-5 (col. 1) / 6'-2 (col. 2) to the plate end: View E-E chain 11'-5 / 13'-2 from the cap end
    });
}
// bottom cover plates (p.31 Detail C + View F-F): start 1 1/2" from the edge of the bearing sole plate (note *), end 2'-10 1/2 past the CL exterior girder
// (CL brg -> jacking 5'-6 -> CL ext. girder 5'-7 13/16 (col 1) / 7'-4 3/16 (col 2)); bolts @ 3 (TYP.) from the start, 3 each side of the jacking line,
// 19 / 26 spa @ 3, 4 13/16 / 4 3/16 to 3" before the CL ext. girder, 3 past it, 10 spa @ 3, 1 1/2 to the end.
double soleHalfIn = br.Supports[2].Bearings!.SolePlateLength / 2 / I(1);
foreach (var (k, toGirder, n2, gap4) in new[] { (0, 67.8125, 19, 4.8125), (1, 88.1875, 26, 4.1875) })
{
    var cols = new List<double>();
    for (double x = 63; x >= soleHalfIn + 1.5 + 1.5 - 1e-6; x -= 3) cols.Add(x);
    for (int i = 0; i <= n2; i++) cols.Add(69 + 3 * i);
    double cg = 66 + toGirder;
    cols.Add(cg - 3);
    for (int i = 0; i <= 10; i++) cols.Add(cg + 3 + 3 * i);
    cap.CoverPlates.Add(new CapCoverPlate
    {
        Top = false, Bearing = k, From = I(1.5), FromSoleEdge = true, To = I(cg + 34.5), Width = I(40.375), Thickness = I(0.75),
        BoltColumns = cols.OrderBy(x => x).Select(I).ToList(), BoltRows = coverRows,
        Unresolved = SBM.Core.UnresolvedFlag.Text("p.31", $"bottom cover plate start at {(k == 0 ? "column 1" : "column 2")} (dimension *)", "it is 'to be coordinated with the bearing manufacturer' (1 1/2 from the sole plate edge) and the sole plate is vendor designed (U5)",
            "Start 1 1/2 from the edge of the modeled 2'-4 sole plate (= the 28\" disc body); bolts at the 3 (TYP.) pitch back to the start (first bolt 1 1/2 from the plate end)."),
    });
}
// girder connection (p.30 elevation, plan, sections D-D / E-E): end plate on the girder, cap web, connector plate inside; bolts through all three.
// End plate outline from Section E-E (z from the top of the cap web at the CL girder): top 1 3/4 below it, upper part 1'-6 wide (bolts 1 1/2 from the
// edges), lower part 2'-3 wide (bolts 2 from the edges) - flare (4" rad.) and bottom chamfer vertices SCALED (not dimensioned).
var conn = new CapGirderConnection
{
    EndPlateThickness = I(0.5), ConnectorPlateThickness = I(0.5), DiaphragmPlateThickness = I(0.75), BottomPlate = new PlateSize(I(24), I(2.5)),
    BottomPlateWeld = I(5.0 / 16),   // p.30 elevation "5/16 + 5/16, mill to bear" at the PL 2 1/2 x 24
    DiaphragmWeld = I(0.25),         // p.30 weld symbols PL 3/4 to the connector plates carry NO size: drawn at 1/4" and flagged UNRESOLVED (dimension only)
    TopFlangeEndBeyondCapFlange = I(1), CopeRadius = I(2), CopeClearance = 0,
    OuterSplicePlate = new PlateSize(I(24), I(1.375)), InnerSplicePlate = new PlateSize(I(11), I(1.375)), InnerPlateFromWeb = I(1),
    SplicePlateEndDistance = I(1.5), SpliceStudLength = I(4), PlanCapFlangeToGirderFlangeTop = I(4.25),
};
foreach (var (x, z) in new[] { (-9.0, -1.75), (9.0, -1.75), (9.0, -57.9), (13.5, -66.6), (13.5, -82.75), (7.5, -86.25), (-7.5, -86.25), (-13.5, -82.75), (-13.5, -66.6), (-9.0, -57.9) })
    conn.EndPlateOutline.Add(new CapPoint(I(x), I(z)));
for (int i = 0; i < 12; i++)
{
    double z = -3.25 - 6 * i;
    foreach (double x in i >= 5 ? new[] { -7.5, -3.5, 3.5, 7.5 } : new[] { -7.5, 7.5 }) conn.EndPlateBolts.Add(new CapPoint(I(x), I(z)));
}
foreach (double z in new[] { -76.25, -81.25 })
    foreach (double x in new[] { -11.5, -7.5, -3.5, 3.5, 7.5, 11.5 }) conn.EndPlateBolts.Add(new CapPoint(I(x), I(z)));
conn.EndPlateBolts.Add(new CapPoint(0, I(-84.75)));
// top flange splice over the cap (p.30 plan): bolt columns from the CL cap 2 3/16, then 5, 6, 5 (over the cap), 4 1/2, 21 spa @ 3 (on each girder flange),
// 1 1/2 to the plate end (14'-6 3/8 overall); rows 3 and 10 each side of the CL girder; studs 7/8 x 4: 4 3/16, 6, 6, 8, then 5 eq. spa (12) to 3 from the end.
foreach (double c in new[] { 2.1875, 7.1875, 13.1875, 18.1875 }) conn.SpliceBoltColumns.Add(I(c));
for (int i = 0; i <= 21; i++) conn.SpliceBoltColumns.Add(I(22.6875 + 3 * i));
conn.SpliceBoltRows = new() { -I(10), -I(3), I(3), I(10) };
foreach (double c in new[] { 4.1875, 10.1875, 16.1875, 24.1875, 36.1875, 48.1875, 60.1875, 72.1875, 84.1875 }) conn.SpliceStudColumns.Add(I(c));
conn.SpliceStudRows = new() { -I(6.5), 0, I(6.5) };
cap.Connection = conn;
// handholes (p.30 Section E-E): 1'-0, 2'-9, 4'-6, 6'-3 below the top of the cap web; 1'-6 (levels 1-3) / 1'-10 (level 4) each side of the CL girder,
// alternating near / far web (solid / dashed). Opening 6 x 8 (ellipse as drawn), cover PL 1/2 x 10 x 1'-1, backing PL 1/2 (2 pieces), 6-3/4" bolts.
foreach (var (web, a) in new[] { (CapWebSide.Back, 1), (CapWebSide.Ahead, -1) })
{
    cap.Handholes.Add(new CapHandhole(web, -a * I(18), I(12)));
    cap.Handholes.Add(new CapHandhole(web, a * I(18), I(33)));
    cap.Handholes.Add(new CapHandhole(web, -a * I(18), I(54)));
    cap.Handholes.Add(new CapHandhole(web, a * I(22), I(75)));
}
var hd = new CapHandholeDetail { OpeningWidth = I(6), OpeningHeight = I(8), CoverThickness = I(0.5), BackingThickness = I(0.5), BoltDiameter = I(0.75) };
foreach (var (x, z) in new[] { (-1.25, 6.5), (1.25, 6.5), (5.0, 4.25), (5.0, -4.25), (1.25, -6.5), (-1.25, -6.5), (-5.0, -4.25), (-5.0, 4.25) }) hd.CoverOutline.Add(new CapPoint(I(x), I(z)));
foreach (var (x, z) in new[] { (0.0, 5.25), (0.0, -5.25), (-3.75, 3.0), (3.75, 3.0), (-3.75, -3.0), (3.75, -3.0) }) hd.Bolts.Add(new CapPoint(I(x), I(z)));
foreach (int sg in new[] { 1, -1 })
{
    // backing plate: 1 3/4 .. 6 1/2 from the CL handhole, chamfered like the cover, opening cut to the 6 x 8 ellipse ("match opening in web PL")
    var pts = new List<CapPoint> { new(I(-5), I(sg * 1.75)) };
    double xc = 3 * Math.Sqrt(1 - 1.75 * 1.75 / 16), a0 = Math.Atan2(1.75 / 4, -xc / 3), a1 = Math.Atan2(1.75 / 4, xc / 3);
    for (int i = 0; i <= 12; i++)
    {
        double t = a0 + (a1 - a0) * i / 12;
        pts.Add(new CapPoint(I(3 * Math.Cos(t)), I(sg * 4 * Math.Sin(t))));
    }
    pts.Add(new CapPoint(I(5), I(sg * 1.75))); pts.Add(new CapPoint(I(5), I(sg * 4.25))); pts.Add(new CapPoint(I(1.25), I(sg * 6.5)));
    pts.Add(new CapPoint(I(-1.25), I(sg * 6.5))); pts.Add(new CapPoint(I(-5), I(sg * 4.25)));
    hd.BackingOutlines.Add(pts);
}
cap.HandholeDetail = hd;
// cap studs 7/8 x 9 (p.29 elevation chain from CL brg col 1; p.31 Section A-A rows: 2 from the edge, 3 eq. spa, 2 3/16 across the CL; View E-E on the cover plates)
{
    double x0 = 7 + (9 + 5.0 / 16) / 12;
    var lines = new List<double>();
    for (int i = 0; i <= 4; i++) lines.Add(x0 + 0.5 * i);
    double gA = 11 + (1 + 13.0 / 16) / 12;
    foreach (double g0 in new[] { gA, gA + 92 / 12.0, gA + 184 / 12.0 })
        for (int i = 0; i <= 10; i++) lines.Add(g0 + 16 / 12.0 + 0.5 * i);
    double gD = gA + 23;
    for (int i = 0; i <= 4; i++) lines.Add(gD + 16.5 / 12 + 0.5 * i);
    cap.StudLines = lines.Select(x => Ft(x)).ToList();
    double sp = (18.1875 - 1.09375) / 3;
    cap.StudRows = new[] { -18.1875, -18.1875 + sp, -18.1875 + 2 * sp, -1.09375, 1.09375, 18.1875 - 2 * sp, 18.1875 - sp, 18.1875 }.Select(I).ToList();
    cap.CoverPlateStudRows = new[] { -16.1875, -10.1875, -4.1875, 4.1875, 10.1875, 16.1875 }.Select(I).ToList();
    Print($"Cap studs: {lines.Count} lines from {lines[0]:F3} to {lines[^1]:F3} ft (plan 47'-0 - 9'-5 11/16 = {47 - (9 + 5.6875 / 12):F3})");
}
cap.EndScreens = new CapEndScreen();
cap.Unresolved.AddRange(new[]
{
    SBM.Core.UnresolvedFlag.Text("p.30", "girder end plate flare (4\" rad.) and bottom chamfer vertices", "Section E-E dimensions only the bolt chain, the widths (via the edge distances) and the top / bottom", "Vertices scaled from Section E-E at 250 dpi."),
    SBM.Core.UnresolvedFlag.Text("p.30", "girder web cope under the cap top flange", "only the 2\" radius is drawn; the cope depth / clearance is not dimensioned", "Cope to the underside of the cap top flange, no clearance, square corner."),
    SBM.Core.UnresolvedFlag.Text("p.30", "'4 1/4 (AT CL CAP)' in Section E-E", "the dimension's lower end reads as the top of the cap top flange, its upper end is not identifiable", "Compared as top of cap flange to top of girder flange (reported)."),
    SBM.Core.UnresolvedFlag.Text("p.30-31", "identity of 'PL 3/4' / '1/2 connector PL' (U4 / U6)", "read at 300-400 dpi as: 3/4 girder connection diaphragm plate in the girder web plane between 1/2 connector plates on the inner web faces, bolts end plate + web + connector plate", "Modeled that way; confirm."),
});
// concrete haunch over the cap (p.25 Section A-A + Section near Pier No. 2; p.29 plan "EDGE OF SLAB" jogs around the cap = the p.43 Detail D
// bump-out; p.45 bar list + bend diagram): cast with the deck (placement section 5, p.43 / p.45), sides flush with the top flange edges (40 3/8 out
// to out), cap steel (flange / top cover plate) to the deck soffit, between the deck edges; no chamfer or haunch dimension is drawn (depth = deck
// soffit - cap steel). Bars: 5d2 U (6 + 8 + 3'-0 + 8 + 6 out to out, D = 2 1/2, 5'-4 listed), "2 CL." above the cap steel, "PLACE BETWEEN SHEAR
// STUDS", 10 per bay + 4 at each overhang (38); 5e5 (between girder lines, 9 x 5'-4) / 5e6 (outside the exterior girders, 6 x 2'-5): 3 per bay, in
// the two bottom corners of the 5d2 and at the CL cap (Section A-A). Corner bars nest in the 2 1/2" pin bends (centre = bend centre + (1 1/4 - 5/16)
// toward the corner); the middle bar rests on the 5d2 bottom.
{
    double clr = 2, db = 0.625, rin = 1.25, d45 = (rin - db / 2) * Math.Sqrt(0.5);
    double nC = 18 - db - rin + d45, hC = clr + db + rin - d45, hM = clr + db + db / 2;
    var pos = new List<CapPoint> { new(-I(nC), I(hC)), new(0, I(hM)), new(I(nC), I(hC)) };
    cap.Haunch = new CapHaunchDefinition
    {
        Source = "p.25 Section A-A / Section near Pier No. 2; p.29 plan; p.43 Detail D; p.45",
        Width = 0,   // = top flanges out to out (2 x 10 3/16 + 20 = 40 3/8, Section A-A: haunch sides flush with the flange edges)
        Stirrups =
        {
            new CapHaunchStirrups { Mark = "5d2", Size = "#5", Location = "INTEGRAL STEEL PIER CAP HAUNCH", BottomWidth = I(36), LegHeight = I(8), HookLength = I(6), Pin = I(2.5),
                ClearAboveSteel = I(clr), ListedLength = L("5'-4"), PlanCount = 38, PlanWeightLb = 211, Source = "p.25 Section A-A, p.45 (5d2 bend diagram)" },
        },
        Bars =
        {
            new CapHaunchBars { Mark = "5e5", Size = "#5", Location = "INTEGRAL STEEL PIER CAP HAUNCH LONGIT.", Bay = CapHaunchBay.Interior, Positions = pos,
                ListedLength = L("5'-4"), PlanCount = 9, PlanWeightLb = 50, Source = "p.25 Section A-A / Section near Pier No. 2 (3-5e5 TYP.), p.45" },
            new CapHaunchBars { Mark = "5e6", Size = "#5", Location = "INTEGRAL STEEL PIER CAP HAUNCH LONGIT.", Bay = CapHaunchBay.Exterior, Positions = pos.Select(q => q.Clone()).ToList(),
                ListedLength = L("2'-5"), PlanCount = 6, PlanWeightLb = 15, Source = "p.25 Section A-A / Section near Pier No. 2 (3-5e6 TYP.), p.45" },
        },
        Unresolved =
        {
            SBM.Core.UnresolvedFlag.Text("p.25", "position of the 5e5 / 5e6 corner bars inside the 5d2", "Section A-A draws them in the bottom corners without a dimension",
                "Nested in the 2 1/2\" pin bends of the 5d2 (from the p.45 bend diagram)."),
        },
    };
}
br.Supports[2].IntegralSteelCap = cap;

// ================================================================ fabrication notes (fabrication-notes.csv) -> part attributes / grades
{
    var notes = Csv("fabrication-notes.csv").ToDictionary(r => r[0], r => r[1]);
    FabricationRule Rule(string id, Action<FabricationRule> set)
    {
        var r = new FabricationRule { Note = id, Source = notes.TryGetValue(id, out var p) ? p : "?" };
        if (!notes.ContainsKey(id)) Print($"!! fabrication note {id} not in the CSV");
        set(r); return r;
    }
    List<string> C(params string[] c) => c.ToList();
    var girderPlates = C("FlangePlate", "WebPlate");
    var capMain = C("FlangePlate", "WebPlate", "SplicePlate", "CoverPlate", "ConnectorPlate", "DiaphragmPlate", "EndPlate");
    var steelParts = C("FlangePlate", "WebPlate", "BearingStiffener", "ConnectionStiffener", "IntermediateStiffener", "Gusset", "FillerPlate", "Angle", "Channel", "SplicePlate",
        "EndPlate", "ConnectorPlate", "DiaphragmPlate", "ContinuityPlate", "CoverPlate", "HandholeCover", "HandholeBacking", "SolePlate", "MasonryPlate");
    br.Fabrication = new()
    {
        Rule("F01", r => { r.Categories = steelParts; r.Material = "ASTM A709 Gr. 50W"; r.Set.Notes.Add("weathering steel"); }),
        Rule("F29", r => { r.AssemblyKinds = C("PierCap", "PierCapConnection"); r.Categories = C("FlangePlate", "WebPlate", "BearingStiffener", "CoverPlate", "ConnectorPlate"); r.Material = "ASTM A709 Gr. HPS 70W"; }),
        Rule("F29", r => { r.Categories = C("EndPlate"); r.Material = "ASTM A709 Gr. HPS 70W"; }),
        Rule("F30", r => { r.AssemblyKinds = C("PierCap", "PierCapConnection"); r.Categories = C("FillerPlate", "HandholeCover", "HandholeBacking", "Angle", "SplicePlate", "DiaphragmPlate", "ContinuityPlate"); r.Material = "ASTM A709 Gr. 50W"; }),
        Rule("F12", r => { r.Categories = girderPlates; r.MarkPattern = "^G1-(1|2)-"; r.Set.CoatingExterior = "Paint exterior face of girder A, S abut. to FS 2 (limits of painting detail p.27), Art. 2408.02 Q"; }),
        Rule("F19", r => { r.Categories = C("Weld"); r.Set.Ndt = "MT (magnetic particle) of welds per the standard specifications"; }),
        Rule("F54", r => { r.Categories = C("Weld"); r.DescriptionPattern = "flange to web"; r.Set.Weld = "5/16 fillet both sides, continuous submerged arc welding (std 1021W mod.)"; }),
        Rule("F58", r => { r.AssemblyKinds = C("GirderFieldSection"); r.Categories = C("FlangePlate"); r.Set.Ndt = "Flange butt welds: RT full width in tension / reversal zones, min. 50 % of width in compression (zones p.28)"; }),
        Rule("F57", r => { r.AssemblyKinds = C("GirderFieldSection"); r.Categories = C("FlangePlate"); r.Set.Weld = "Shop butt splices: 1:3 thickness transition, 60 deg groove both sides, bench weld, ground smooth"; }),
        Rule("F64", r => { r.AssemblyKinds = C("GirderFieldSection"); r.Categories = C("WebPlate"); r.Set.Camber = "Web cut for dead load deflection + vertical curve; camber held at the CL bearings (p.36-37 tables)"; }),
        Rule("F23", r => { r.AssemblyKinds = C("CrossFrame"); r.Set.Toughness = "Charpy V-notch per Art. 4152.02"; }),
        Rule("F23", r => { r.Categories = C("ConnectionStiffener"); r.Set.Toughness = "Charpy V-notch per Art. 4152.02"; }),
        Rule("F55", r => { r.AssemblyKinds = C("GirderFieldSection"); r.Categories = C("BearingStiffener"); r.Set.ContactFinish = "Mill to bear (bottom flange end)"; }),
        Rule("F17", r => { r.AssemblyKinds = C("FieldSplice"); r.Categories = C("FillerPlate"); r.Set.Tolerance = "Fill thickness verified / adjusted in fabrication for a close fit"; }),
        Rule("F28", r => { r.AssemblyKinds = C("PierCap", "PierCapConnection"); r.Categories = capMain; r.Set.Toughness = "CVN temperature zone 2, main tension member"; }),
        Rule("F28", r => { r.Categories = C("EndPlate"); r.Set.Toughness = "CVN temperature zone 2, main tension member"; }),
        Rule("F32", r => { r.AssemblyKinds = C("PierCap"); r.Categories = C("FlangePlate", "WebPlate"); r.Set.Camber = "Cap camber from the chord between bearings 3/16 (-0,+1/16), 3/8 (-0,+1/8), 7/16 (-0,+3/16), 3/8, 3/16 at 6 spa @ 7'-10; plumb under full dead load"; }),
        Rule("F49", r => { r.AssemblyKinds = C("PierCap"); r.Categories = C("FlangePlate", "WebPlate"); r.Set.Weld = "Web to flange: continuous submerged arc weld (size not shown, U3)"; }),
        Rule("F59", r => { r.AssemblyKinds = C("PierCap"); r.Categories = C("FlangePlate"); r.Set.Ndt = "Cap bottom flanges in tension, top flanges in compression (radiography per F58)"; }),
        Rule("F41", r => { r.AssemblyKinds = C("PierCap"); r.Categories = C("FlangePlate", "WebPlate", "BearingStiffener", "CoverPlate", "HandholeCover"); r.Set.CoatingExterior = "FS 20045 weathering steel brown, semi-gloss (exterior incl. the inside between the cap end and the bearing stiffeners)"; }),
        Rule("F42", r => { r.AssemblyKinds = C("PierCap", "PierCapConnection"); r.Categories = C("FlangePlate", "WebPlate", "ConnectorPlate", "DiaphragmPlate", "ContinuityPlate", "HandholeBacking", "BearingStiffener"); r.Set.CoatingInterior = "White (interior of the cap)"; }),
        Rule("F35", r => { r.AssemblyKinds = C("PierCap", "PierCapConnection"); r.Set.ShopAssembly = "Shop assemble cap + girder field sections framing to it + connection assemblies; drill bolt holes in the fully assembled position"; }),
        Rule("F35", r => { r.Categories = C("EndPlate"); r.Set.ShopAssembly = "Shop assembled with the cap; holes drilled in the assembled position"; }),
        Rule("F36", r => { r.Categories = C("EndPlate"); r.Set.ContactFinish = "Square and flat after welding (complete contact of the bolted connection)"; }),
        Rule("F44", r => { r.Categories = C("EndPlate"); r.Set.ContactFinish = "Full contact at bottom flange and web after bolting"; }),
        Rule("F45", r => { r.Categories = C("EndPlate"); r.Set.Tolerance = "+/- 1/16 (end plate faces across the cap), +/- 1/64 (end plate face); mill from oversized plate after welding if required"; }),
        Rule("F43", r => { r.Categories = C("ContinuityPlate"); r.Set.ContactFinish = "Mill to bear, tight fit (centre on the girder bottom flanges, match slope)"; }),
        Rule("F51", r => { r.AssemblyKinds = C("PierCap"); r.Categories = C("BearingStiffener"); r.Set.ContactFinish = "Mill to bear (bottom); 4 3/8 no weld at the top; 1\" 45 deg clips"; }),
        Rule("F38", r => { r.AssemblyKinds = C("PierCap"); r.Categories = C("BearingStiffener"); r.Set.Tolerance = "Bearing stiffeners vertical under full dead load"; }),
        Rule("F33", r => { r.AssemblyKinds = C("PierCapConnection"); r.Categories = C("FillerPlate"); r.Set.ShopAssembly = "Connection fill plates tapered (two directions: multiple plates tapered in opposite directions allowed)"; }),
        Rule("F34", r => { r.AssemblyKinds = C("PierCapConnection"); r.Categories = C("FillerPlate"); r.Set.Notes.Add("hardened beveled washers at tapered connections"); }),
        Rule("F47", r => { r.Categories = C("HandholeBacking"); r.Set.ShopAssembly = "Resin bond to the web, grind smooth, tack weld"; }),
        Rule("F40", r => { r.Categories = C("MeshScreen"); r.Set.CoatingExterior = "Galvanized, then painted FS 20045 brown semi-gloss (galvanized surfaces supplemental specification)"; }),
        Rule("F53", r => { r.Categories = C("MeshScreen", "Angle"); r.AssemblyKinds = C("PierCap"); r.Set.Notes.Add("subsidiary to structural steel; 3/8 stainless bolts, jam nuts, oversized washers"); }),
        Rule("F65", r => { r.Categories = C("SolePlate"); r.Set.ShopAssembly = "Sole plates tapered to the longitudinal / transverse slope; 2\" wider than the flange"; }),
        Rule("F66", r => { r.Categories = C("SolePlate"); r.Set.Weld = "5/16 fillet to the flange full length (6\" min GE / 1'-0 min FX / 3'-0 min pier 2 GE)"; }),
    };
}

// ================================================================ substructure
var units = new List<string>();
Doc.Regenerate(includeSteel: false);
var fr = Doc.Framing(br.Name);
// ---- intermediate cross frame lines (p.27 framing plan, measured along girder A, normal to the girders; "7 SPA. @ ..." strings anchored at the CL bearing of each span):
// span 1: 22'-5 7/16 then 6 spa @ 22'-6 (last line 20'-5 3/8 before pier 1); span 2: 23'-0 5/8 then 7 spa @ 22'-9 (22'-9 to pier 2; along D 20'-6 5/8, 7 @ 22'-9);
// span 3: 22'-8 15/16 then 6 spa @ 23'-1 11/16 (the 7th space ends at the N abutment frame; along D 23'-0 5/8, 7 @ 23'-6).
{
    var gA = fr.Girders[0];
    int nsp = gA.Spans.Count;
    double s0A = gA.DistanceAt(0, 0);
    double[] bA = Enumerable.Range(0, nsp).Select(k => gA.DistanceAt(k, 0) - s0A).ToArray();
    var lines = new List<CrossFrameLine>();
    void Ln(int span, double first, double step, int n)
    {
        for (int k = 0; k < n; k++) lines.Add(new CrossFrameLine { Girder = 0, Distance = bA[span] + first + step * k, NormalToGirder = true });
    }
    Ln(0, F(22, 5.4375), F(22, 6), 7);
    Ln(1, F(23, 0.625), F(22, 9), 8);
    Ln(2, F(22, 8.9375), F(23, 1.6875), 7);
    br.CrossFrames.Lines = lines;
    Print($"Cross frame lines (p.27): {lines.Count} along girder A; span starts {string.Join(", ", bA.Select(v => (v / 0.3048).ToString("F3")))} ft");
}
// chord line between the abutments (staking p.8): CL at the CL abutment bearings
var cS = al.PointAt(br.Supports[0].Station, 0); var cN = al.PointAt(br.Supports[3].Station, 0);
var chordDir = (cN - cS).Normalized();
double ChordS(int k)
{
    var sl = fr.Supports[k];
    Numerics.IntersectLines(sl.AlignmentPoint, sl.Direction, cS, chordDir, out double t, out _);
    return t;
}
{
    double ang = Math.Acos(Math.Abs(Vec2.Dot(chordDir, Vec2.FromAngle(al.AngleAt(S("2475+00")))))) * 180 / Math.PI;
    Print($"Chord between abutments {(cN - cS).Length / 0.3048:F3} ft (plan 569'-7 13/16 = 569.651), {ang:F4} deg to the middle tangent (plan 1 deg 07'53\" = 1.1314)");
    for (int k = 0; k < 4; k++)
    {
        var d = fr.Supports[k].Direction;
        double a = Math.Acos(Math.Abs(Vec2.Dot(d, chordDir.Right))) * 180 / Math.PI;
        Print($"  {fr.Supports[k].Name}: skew to the chord normal {a:F4} deg; chord crosses at S {ChordS(k) / 0.3048:F3} ft");
    }
}

// ---- Pier 1 (p.9-11, p.14): concrete cap 29'-3 5/8 (18'-1 3/8 left + 11'-2 1/4 right of the chord along CL pier), 4'-11 5/8 x 5'-0, level top 1115.03;
// pedestals 5'-0 x 4'-5 5/8; 2 columns 5'-3 5/8 x 4'-11 5/8 with 4" projections on both S faces (3'-7 5/8 wide) (Section B-B), flush with the cap ends
// (2'-11 13/16 to CL column); footings 11'-0 (along) x 17'-0 x 5'-0, bottom 1086.95, 15 HP12x84 150' (3 x 5 at 4'-0 / 3'-6), end rows battered 1:4.
{
    double sc = ChordS(1);
    double s0 = sc - L("18'-1 3/8"), s1 = sc + L("11'-2 1/4");
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.CapWidth = L("4'-11 5/8"); sd.CapDepth = F(5);
    // p.9 pier cap plan: the 29'-3 5/8 overall length includes a 4" end projection 3'-7 5/8 wide (8" notches at the four corners); the full-width cap body is 4" in from each end.
    double capEnd = I(4), capProjW = L("3'-7 5/8");
    sd.CapProfile = new() { new(s0 + capEnd, Ft(1110.03)), new(s1 - capEnd, Ft(1110.03)), new(s1 - capEnd, Ft(1115.03)), new(s0 + capEnd, Ft(1115.03)) };
    sd.PedestalSizeAlong = F(5); sd.PedestalSizeAcross = L("4'-5 5/8");
    sd.ColumnCount = 2; sd.FirstColumnOffset = s0 + L("2'-11 13/16"); sd.ColumnSpacings = new() { F(23, 4) };
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = L("5'-3 5/8"); sd.ColumnDepth = L("4'-11 5/8");
    sd.ColumnProjectionS = I(4); sd.ColumnProjectionWidth = L("3'-7 5/8");
    sd.Foundation = FoundationType.PileCap; sd.FoundationTopElevation = Ft(1086.95 + 5); sd.FootingThickness = F(5);
    sd.FootingLength = F(11); sd.FootingWidth = F(17);
    sd.PileType = PileType.SteelHP; sd.PileSection = "HP12x84"; sd.PileEmbedment = F(1); sd.PileLength = F(150);
    sd.Reinforcement.Enabled = false;   // pier bars: bar list only (bend diagrams G9)
    for (int c = 1; c <= 2; c++)
    {
        var fd = new FootingDefinition { Column = c, PileLength = F(150), BatterRatio = 4 };
        foreach (double ps in new[] { -4.0, 0, 4 })
            foreach (double pn in new[] { -7.0, -3.5, 0, 3.5, 7 })
                fd.Piles.Add(new FootingPoint(Ft(ps), Ft(pn)) { BatterDirN = Math.Abs(pn) > 6 ? Math.Sign(pn) : 0 });
        sd.Footings.Add(fd);
    }
    // MASKWALLS (p.9 pier elevation / plan; p.14 "MASKWALLS" concrete quantity 2.7 CY and bars 5c5 "MASKWALL HORIZONTAL" / 5d7 "MASKWALL VERTICAL"; the plans do
    // not use the word "end wall" or "checkwall"): one at each cap end, square to the CL pier (cap frame), 12" thick along the pier line = 8" at the full cap width
    // 4'-11 5/8 (inner) + the 4" reveal projection 3'-7 5/8 wide (outer, 8" notches each side, the same projection as the columns), rising 7'-7 (col 1 end) /
    // 8'-4 1/2 (col 2 end) above the cap top 1115.03 at the OUTER face; the top slopes down 1/2" to the inner face (drawn as one straight line). Volume 36 cf each,
    // 2 x 36 = 2.7 CY = the p.14 quantity. The barrier bump-out ends above them are square to the deck edge (p.43 Detail C) - the plans' own skew difference.
    double mwFull = L("4'-11 5/8"), mwProj = L("3'-7 5/8"), mwOut = I(4), mwIn = I(12), mwSlope = I(0.5) / I(12);
    sd.CapBlocks = new()
    {
        new CapBlockDefinition { Name = "end projection (column 1 end)", Kind = CapBlockKind.Cap, Base = CapBlockBase.CapBottom, TopElevation = Ft(1115.03), TopRefS = s0,
            Outline = { new(s0, -capProjW / 2), new(s0 + capEnd, -capProjW / 2), new(s0 + capEnd, capProjW / 2), new(s0, capProjW / 2) }, Source = "p.9 pier cap plan (4\" x 3'-7 5/8 end projection, 8\" notches)" },
        new CapBlockDefinition { Name = "end projection (column 2 end)", Kind = CapBlockKind.Cap, Base = CapBlockBase.CapBottom, TopElevation = Ft(1115.03), TopRefS = s1,
            Outline = { new(s1 - capEnd, -capProjW / 2), new(s1, -capProjW / 2), new(s1, capProjW / 2), new(s1 - capEnd, capProjW / 2) }, Source = "p.9 pier cap plan (4\" x 3'-7 5/8 end projection, 8\" notches)" },
        new CapBlockDefinition { Name = "maskwall (column 1 end)", Kind = CapBlockKind.Maskwall, Base = CapBlockBase.CapTop, TopElevation = Ft(1115.03) + F(7, 7), TopRefS = s0, TopSlopeS = -mwSlope,
            Outline = { new(s0, -mwProj / 2), new(s0 + mwOut, -mwProj / 2), new(s0 + mwOut, -mwFull / 2), new(s0 + mwIn, -mwFull / 2), new(s0 + mwIn, mwFull / 2), new(s0 + mwOut, mwFull / 2), new(s0 + mwOut, mwProj / 2), new(s0, mwProj / 2) },
            Source = "p.9 pier elevation / plan, p.14 maskwall quantity" },
        new CapBlockDefinition { Name = "maskwall (column 2 end)", Kind = CapBlockKind.Maskwall, Base = CapBlockBase.CapTop, TopElevation = Ft(1115.03) + F(8, 4.5), TopRefS = s1, TopSlopeS = mwSlope,
            Outline = { new(s1, -mwProj / 2), new(s1, mwProj / 2), new(s1 - mwOut, mwProj / 2), new(s1 - mwOut, mwFull / 2), new(s1 - mwIn, mwFull / 2), new(s1 - mwIn, -mwFull / 2), new(s1 - mwOut, -mwFull / 2), new(s1 - mwOut, -mwProj / 2) },
            Source = "p.9 pier elevation / plan, p.14 maskwall quantity" },
    };
    sd.Unresolved = new()
    {
        SBM.Core.UnresolvedFlag.Text("p.9-11", "terra cotta blockouts (south faces) and the thin brick recesses of the columns / cap", "decorative recesses (terra cotta = LS item 17)", "Not cut into the concrete; thin brick as a surface treatment."),
        SBM.Core.UnresolvedFlag.Text("p.11", "uplift anchors 8f6 at the circled piles (Detail I)", "the circled pile positions are not read", "Not modeled."),
    };
    br.Supports[1].Substructure = sd;
    double girderEnds = (fr.Supports[1].Bearings[0]!.TopOfSteel.XY - fr.Supports[1].AlignmentPoint).Length;
    Print($"Pier 1: chord at S {sc / 0.3048:F3} ft; cap {s0 / 0.3048:F3} .. {s1 / 0.3048:F3} ft (plan cap ends 3'-0 15/16 beyond A / D along CL pier: A at {-girderEnds / 0.3048:F3})");
}

// ---- Pier 2 (p.12-14): NO concrete cap - 2 columns turned 15 deg 00'00" carrying the cap bearings on 4" pedestals (5'-0 x 6'-10), column core 5'-11 5/8 x
// 7'-3 5/8 with 4" projections on both S faces (5'-11 5/8 wide) (Section A-A), heights 20'-5 / 22'-0 on 4'-0 footings 13'-6 sq (turned 15 deg),
// bottom 1085.64, 16 HP12x84 130' per footing (4 x 4 at 1'-6 / 3'-6 / 1'-9 / 1'-9 / 3'-6 / 1'-6), perimeter piles battered 1:4 outward.
{
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.NoCap = true;
    sd.CaplessPedestalHeight = I(4);
    sd.PlanColumnHeights = new() { F(20, 5), F(22) };
    sd.PedestalSizeAlong = F(5); sd.PedestalSizeAcross = F(6, 10);
    sd.ColumnCount = 2; sd.FirstColumnOffset = cap.BearingOffsets[0]; sd.ColumnSpacings = new() { F(47) };
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = L("5'-11 5/8"); sd.ColumnDepth = L("7'-3 5/8");
    sd.ColumnProjectionS = I(4); sd.ColumnProjectionWidth = L("5'-11 5/8");
    sd.ColumnProjectionBack = I(4); sd.ColumnProjectionBackWidth = L("3'-11 5/8");   // p.13 Section A-A: 4" projection on the south (back-station) face, 3'-11 5/8 wide; 7'-7 5/8 overall
    sd.ColumnRotationDeg = 15;
    sd.Foundation = FoundationType.PileCap; sd.FoundationTopElevation = Ft(1085.64 + 4); sd.FootingThickness = F(4);
    sd.FootingLength = F(13, 6); sd.FootingWidth = F(13, 6);
    sd.PileType = PileType.SteelHP; sd.PileSection = "HP12x84"; sd.PileEmbedment = F(1); sd.PileLength = F(130);
    sd.Reinforcement.Enabled = false;
    double r15 = 15 * Math.PI / 180, cr = Math.Cos(r15), sr = Math.Sin(r15);
    Vec2 Rt(double s, double n) => new(s * cr - n * sr, s * sr + n * cr);
    double h = F(6, 9);
    for (int c = 1; c <= 2; c++)
    {
        var fd = new FootingDefinition { Column = c, PileLength = F(130), BatterRatio = 4 };
        foreach (var (s, n) in new[] { (-h, -h), (h, -h), (h, h), (-h, h) }) { var q = Rt(s, n); fd.Outline.Add(new FootingPoint(q.X, q.Y)); }
        double[] at = { -5.25, -1.75, 1.75, 5.25 };
        foreach (double ps in at)
            foreach (double pn in at)
            {
                var q = Rt(Ft(ps), Ft(pn));
                // batter direction in the footing frame: rows at +-5.25 N outward along N; the two middle piles of the +-5.25 S columns outward along S
                Vec2 dir = Math.Abs(pn) > 5 ? new Vec2(0, Math.Sign(pn)) : Math.Abs(ps) > 5 ? new Vec2(Math.Sign(ps), 0) : new Vec2(0, 0);
                var dq = Rt(dir.X, dir.Y);
                fd.Piles.Add(new FootingPoint(q.X, q.Y) { BatterDirS = dq.X, BatterDirN = dq.Y });
            }
        sd.Footings.Add(fd);
    }
    sd.Unresolved = new()
    {
        SBM.Core.UnresolvedFlag.Text("p.13", "4\" pilaster on one N face of the pier 2 columns and the terra cotta blockout", "decorative; widths read only partly", "Not modeled."),
        SBM.Core.UnresolvedFlag.Text("p.12", "column centres across the support line", "the pier plan centres the columns on the cap bearings in S only", "Columns centred on the CL pier (N = 0)."),
    };
    br.Supports[2].Substructure = sd;
    double sc = ChordS(2);
    Print($"Pier 2: chord at S {sc / 0.3048:F3} ft; staking CL footings {(sc - L("22'-0 15/16")) / 0.3048:F3} / {(sc + L("24'-11 1/16")) / 0.3048:F3} ft vs cap bearings {cap.BearingOffsets[0] / 0.3048:F3} / {cap.BearingOffsets[1] / 0.3048:F3} ft");
}

// ---- abutments (p.18-23): stub abutments, as designed. Footing 6'-3 (CL brg 3'-2 from the back face, 3'-1 to the front at the bottom) x 3'-10 at the back,
// backwall 1'-6 thick, front face 1'-6 1/2 (*) from CL brg, corbel 8" with 1'-1 / 1'-3 / 1'-3; steps 4'-0 long to 2'-1 ahead of CL brg at the seats;
// piles HP12x84 in two rows 1'-6 from the footing faces at 6 stations (p.22: 1'-6 3/4, 3 @ 5'-2 1/2, 4'-4 3/4 + 9 3/4, 5'-2 1/2, 1'-6 3/4 from the left end).
foreach (var (k, nm, bot, seats, pileLen) in new[]
{
    (0, "S. Abut.", 1112.35, new[] { 1116.68, 1116.97, 1117.25, 1117.54 }, 190.0),
    (3, "N. Abut.", 1105.62, new[] { 1109.95, 1110.34, 1110.72, 1111.10 }, 140.0),
})
{
    var sup = br.Supports[k];
    var sl = fr.Supports[k];
    var ad = new AbutmentDefinition
    {
        Source = $"Iowa DOT Design 1617 {nm} p.{(k == 0 ? "18-19" : "20-21")}, piling p.22", Kind = AbutmentKind.Stub,
        LeftEndOffset = -F(21, 7), RightEndOffset = F(7, 7),
        PlanFootingWidth = F(6, 3), BackwallThickness = F(1, 6), BackwallFrontToBearing = L("1'-6 1/2"), BearingToStepFront = F(2, 1), BearingToFootingFront = F(3, 1),
        FootingDepth = F(4), WashHeight = I(1), BackwallBaseAboveFootingBottom = F(3, 10),
        LedgeBelowTop = F(1, 1), CorbelProjection = I(8), CorbelVertical = F(1, 3), CorbelTaperRise = F(1, 3), StepLength = F(4),
        PileBackRowToBearing = F(1, 8), PileFrontRowToBearing = F(1, 7), PileTopAboveFootingBottom = F(2), PileLength = Ft(pileLen),
        // p.18 / p.20 "Section through abutment" (and p.22 notes "batter piles in the direction shown"): the rear (fill side) pile is plumb, the FRONT (span side) pile
        // is battered 1:4 outward toward the span; dimensions (1'-6 from each face) are at the bottom of the footing.
        PileBatterRatio = 4, PileBatterRows = new() { 1 },
    };
    ad.FootingBottom.Add(new CapProfilePoint(0, Ft(bot)));
    // backwall top parallel to the roadway crown line (p.18): the finished grade at the backwall front face along the support line
    var nrm = sl.Direction.Left; if (Vec2.Dot(nrm, Vec2.FromAngle(al.AngleAt(sl.Station))) < 0) nrm = -nrm;
    double nb = (k == 0 ? -1 : 1) * L("1'-6 1/2");
    foreach (double e in new[] { -21.583, -20.0, 0.0, 6.0, 7.583 })
    {
        double s = e * 0.3048;   // radial support line: S = alignment offset at the line
        var p = sl.AlignmentPoint + sl.Direction * s + nrm * nb;
        var so = al.Inverse(p);
        ad.BackwallTop.Add(new CapProfilePoint(s, br.Deck.FinishedElevation(al, so.Station, so.Offset)));
    }
    for (int g = 0; g < 4; g++) ad.Steps.Add(new AbutmentStepDefinition(g + 1, Ft(seats[g])));
    double sA = -F(21, 7);
    foreach (double d in new[] { 1.5625, 6.7708, 11.9792, 17.1875, 22.3958, 27.6042 }) ad.PileStations.Add(sA + Ft(d));
    var sd = SubstructureDefinition.Default(SupportType.Abutment);
    sd.Abutment = ad;
    sd.PileType = PileType.SteelHP; sd.PileSection = "HP12x84"; sd.PileLength = Ft(pileLen);
    sd.BackwallGap = I(k == 0 ? 2.5 : 3);
    sd.Reinforcement.Enabled = false;
    sd.Unresolved = new()
    {
        SBM.Core.UnresolvedFlag.Text($"p.{(k == 0 ? "18-19" : "20-21")}, p.22", $"{nm} wing extension{(k == 0 ? "" : "s")}, maskwall{(k == 0 ? "" : "s")}, backwall extension and the L-shaped wing footing (10'-7 1/2 overall) with its pile{(k == 0 ? "" : "s")}",
            "the stub abutment generator has no wing-extension footing: the plan footing outline steps out 4'-4 1/2 x 3'-6 beyond the main footing at the end(s) with a skewed end face (5 deg 54'10\" S / 5 deg 53'33\" N) and bevelled bottom (p.19 / p.21 Views A-A, B-B, F-F, C-C, E-E), wall tops from the wingwall elevation table; the wing pile (1'-6 from the end, p.22) needs that footing",
            $"Not modeled ({(k == 0 ? 1 : 2)} pile{(k == 0 ? "" : "s")} of the {(k == 0 ? 13 : 14)} missing; 12 main piles placed, front row battered 1:4)."),
        SBM.Core.UnresolvedFlag.Text($"p.{(k == 0 ? 18 : 20)}", $"{nm} step blocks", "steps run from the backwall face to 2'-1 ahead of CL brg (p.18 section)", "Generator steps (BearingToStepFront 2'-1)."),
    };
    sup.Substructure = sd;
}

// ================================================================ camber / blocking (p.36-37, camber-deflection.csv, blocking.csv)
{
    var rows = Csv("camber-deflection.csv");
    br.Girders.Camber = new();
    var keys = new (int pt, int sup, int fs)[] { (1, 0, 0), (17, -1, 1), (23, 1, 0), (29, -1, 2), (39, -1, 3), (45, 2, 0), (51, -1, 4), (67, 3, 0) };
    for (int g = 0; g < 4; g++)
    {
        var gc = new GirderCamberDefinition { Girder = g, DatumPoint = 67 };   // blocking H (CL N abut brg) = 0.00 (blocking.csv)
        foreach (var (pt, sp, fs) in keys)
            gc.Keys.Add(fs > 0 ? new CamberKeyPoint { Number = pt, Splice = fs } : new CamberKeyPoint { Number = pt, Support = sp });
        foreach (var r in rows.Where(r => r[0] == GL[g]))
            gc.Points.Add(new CamberPoint { Number = int.Parse(r[1]), Camber = I(Num(r[3])), SlabDeflection = I(Num(r[4])), SteelDeflection = I(Num(r[5])) });
        br.Girders.Camber.Add(gc);
    }
    Print($"Camber: {br.Girders.Camber.Sum(c => c.Points.Count)} points on {br.Girders.Camber.Count} girders.");
}

// ================================================================ barrier (p.54-56, std 1020SE mod.): Iowa aesthetic barrier both sides
{
    BarrierProfilePoint P(double r, double hh, double radius = 0, double chamfer = 0, string? tag = null) => new(I(r), I(hh), I(radius), I(chamfer), tag);
    const double c38 = 0.375;
    List<BarrierProfilePoint> Front(double backR, double backTop) => new() { P(19, 0), P(19, 3, radius: 1.5), P(14, 10, radius: 10), P(10.5, 44, chamfer: c38), P(backR, backTop, chamfer: c38) };
    List<BarrierProfilePoint> Back(double stripR, double recessR, double inner, string tag)
    {
        var l = new List<BarrierProfilePoint>();
        foreach (var (top, bot) in new[] { (37.5, 32.0), (25.5, 20.0), (13.5, 0.0) })
        {
            l.Add(P(stripR, top + c38));
            if (inner != recessR) l.Add(P(inner, top));
            l.Add(P(recessR, top, tag: tag));
            if (bot > 0) { l.Add(P(recessR, bot)); if (inner != recessR) l.Add(P(inner, bot)); l.Add(P(stripR, bot - c38)); }
            else l.Add(P(recessR, 0));
        }
        return l;
    }
    // Part Section C-C (p.54): 1'-7 base = 2 + 8 1/2 + 3 1/2 + 5, 3'-8 at the gutter, 3'-7 5/8 at the back, rustication strips (6 1/8 / 5 1/2 / 6 1/2 ...);
    // D-D bump-out: brick plane 2'-1 from the gutter, strips 4 further, 3'-7 1/4; G-G strip returns 2'-7 + 2'-7, H-H brick 1'-11 13/16 + 1'-11 13/16;
    // bump-outs at pier 1 and pier 2 on both rails (p.43 Details C / D), deck widened 8" over 2'-7 + 2'-7.
    var std = Front(0.5, 43.625); std.AddRange(Back(0.5, 2, 2, "liner"));
    var dd = Front(-10, 43.25); dd.AddRange(Back(-10, -6, -6, "brick"));
    var ret = Front(-10, 43.25); ret.AddRange(Back(-10, 2, -6, "liner"));
    var b = br.Deck.Barrier;
    b.Type = BarrierType.Custom; b.ProfileName = "Iowa aesthetic barrier rail (Design 1617 DS 53-55)";
    b.Left = true; b.Right = true; b.Reinforcement = false; b.Profile = std; b.PerpendicularToDeck = true;
    b.Source = "p.54 Part Sections C-C, D-D, G-G, H-H; p.43 Details C / D";
    b.Zones = new();
    foreach (var pier in new[] { "Pier 1", "Pier 2" })
    {
        b.Zones.Add(new BarrierZone { Name = $"bump-out {pier} strip returns", Support = pier, Length = F(5, 2), Profile = ret, Source = "p.54 Part Section G-G (2'-7 + 2'-7)" });
        b.Zones.Add(new BarrierZone { Name = $"bump-out {pier}", Support = pier, Length = L("3'-11 5/8"), Profile = dd, DeckWidening = I(8), DeckWideningLength = F(5, 2),
            Source = "p.54 Part Sections D-D / H-H (1'-11 13/16 + 1'-11 13/16); p.43 Details C / D (deck 8\" x 2'-7 + 2'-7)" });
    }
    U("p.54", "barrier ends on the abutment wing extensions / approach slabs (special sections A / B, 578'-1 / 585'-8 bid lengths)", "the rail beyond the deck ends sits on the wing extensions (not modeled)", "Barrier modeled over the deck length only.");
}

// ================================================================ surface treatments (p.15-17 thin brick / terra cotta, p.54-56 barrier, p.66 texture)
{
    var list = new List<SurfaceTreatmentDefinition>();
    var brick = new SurfaceTreatmentDefinition
    {
        Name = "Integral thin veneer brick", Kind = SurfaceTreatmentKind.ThinBrick,
        Pattern = "modular size brick, running bond, 2 1/4 x 7 5/8, mixture of dark brown tones (up to two color lots), velour texture",
        Products = "per the special provisions for integral thin veneer brick for structural concrete (p.15 notes)",
        Relief = I(0.375), Orientation = "level",
        PlanQuantity = 1626 * Math.Pow(Ft(1), 2), QuantityItem = "2599-9999014 Integral thin veneer brick for structural concrete, 1,626 SF (p.2)",
        Source = "p.15 / DS 14, p.16 / DS 15; p.54-56",
    };
    var sd1 = br.Supports[1].Substructure!; var sd2 = br.Supports[2].Substructure!;
    // Pier 1 columns (p.15 elevation, Details A / B): from ELEV. 1098.00 to 2" below the cap soffit, bands 2 1/4 at 5 3/4 / 13 3/4 above the bottom and
    // 5 3/4 below the top course; wraps all sides.
    double soff = Ft(1110.03), top1 = soff - I(2), b1 = Ft(1098.00);
    brick.Regions.Add(new SurfaceRegion
    {
        Name = "Pier 1 columns", Host = SurfaceHost.Substructure, Support = "Pier 1", Elements = { "Column" },
        Lower = SurfaceLevel.Level(b1), Upper = SurfaceLevel.Level(top1),
        Exclude =
        {
            new SurfaceBand { Lower = SurfaceLevel.Level(b1 + I(5.75)), Upper = SurfaceLevel.Level(b1 + I(8)), Source = "p.15 Detail B" },
            new SurfaceBand { Lower = SurfaceLevel.Level(b1 + I(13.75)), Upper = SurfaceLevel.Level(b1 + I(16)), Source = "p.15 Detail B" },
            new SurfaceBand { Lower = SurfaceLevel.Level(top1 - I(8)), Upper = SurfaceLevel.Level(top1 - I(5.75)), Source = "p.15 Detail A" },
        },
        Source = "p.15 elevation, Details A / B (wraps all sides)",
    });
    // Pier 1 cap faces (p.15 Detail A): 8 7/16 plain + 3'-7 1/8 brick + 8 7/16 plain on the 5'-0 cap beam, both long faces.
    foreach (double fn in new[] { 1.0, -1.0 })
        brick.Regions.Add(new SurfaceRegion
        {
            Name = $"Pier 1 cap {(fn > 0 ? "ahead" : "back")} face", Host = SurfaceHost.Substructure, Support = "Pier 1", Elements = { "Cap" }, NameContains = "cap", FacingN = fn,
            Lower = SurfaceLevel.Level(soff + I(8 + 7.0 / 16)), Upper = SurfaceLevel.Level(Ft(1115.03) - I(8 + 7.0 / 16)), Source = "p.15 Detail A (8 7/16 + 3'-7 1/8 + 8 7/16)",
        });
    // Pier 1 cap ends (p.15 notes: "thin veneer brick and feature bands without brick veneer shall wrap around all sides of columns and cap beam"): the end faces
    // (4" projection and the 8" steps) get the same Detail A band as the long faces. The soffit between the columns is a horizontal face (not a surface-region host): UNRESOLVED below.
    foreach (double fsn in new[] { 1.0, -1.0 })
        brick.Regions.Add(new SurfaceRegion
        {
            Name = $"Pier 1 cap {(fsn > 0 ? "column 2" : "column 1")} end faces", Host = SurfaceHost.Substructure, Support = "Pier 1", Elements = { "Cap" }, FacingS = fsn,
            Lower = SurfaceLevel.Level(soff + I(8 + 7.0 / 16)), Upper = SurfaceLevel.Level(Ft(1115.03) - I(8 + 7.0 / 16)), Source = "p.15 notes (wrap), Detail A (8 7/16 + 3'-7 1/8 + 8 7/16)",
        });
    brick.Unresolved.Add("(p.15) Pier 1 cap soffit between the columns (about 86 SF, 4'-11 5/8 x 17'-4 3/8): the p.15 note wraps the brick around all sides of the cap beam, but the surface-region model has no horizontal (soffit) host; the p.3 quantity 822.9 SF is short in the model by about that area.");
    // Pier 2 columns (p.16 elevation, Detail A): from ELEV. 1095.60 to 9" + ... below the column top: brick ends at the 2'-0 / 1'-5 band chain under the
    // pedestal (9" plain at the top), Detail B bands at the bottom like pier 1.
    double b2 = Ft(1095.60);
    brick.Regions.Add(new SurfaceRegion
    {
        Name = "Pier 2 columns", Host = SurfaceHost.Substructure, Support = "Pier 2", Elements = { "Column" },
        Lower = SurfaceLevel.Level(b2), Upper = SurfaceLevel.Level(Ft(1085.64 + 4 + 20 + 5 / 12.0) - I(9)),
        Exclude =
        {
            new SurfaceBand { Lower = SurfaceLevel.Level(b2 + I(5.75)), Upper = SurfaceLevel.Level(b2 + I(8)), Source = "p.16 / p.15 Detail B" },
            new SurfaceBand { Lower = SurfaceLevel.Level(b2 + I(13.75)), Upper = SurfaceLevel.Level(b2 + I(16)), Source = "p.16 / p.15 Detail B" },
        },
        Source = "p.16 elevation, Detail A (9\" plain at the top), Detail B",
        Unresolved = "p.16 Detail A: the top limit is drawn at column 1; column 2 (1'-7 taller) uses the same elevation", Basis = "brick up to 9\" below the column 1 top",
    });
    brick.Regions.Add(new SurfaceRegion { Name = "barrier bump-outs (piers 1 and 2)", Host = SurfaceHost.Barrier, Tag = "brick", Source = "p.54 Part Sections D-D / H-H" });
    brick.Unresolved.Add("(p.15-17) terra cotta panels (LS item 17: prepare and install only) in the south-face blockouts are not modeled; the blockouts are not cut.");
    list.Add(brick);
    list.Add(new SurfaceTreatmentDefinition
    {
        Name = "Concrete texture (barrier)", Kind = SurfaceTreatmentKind.FormLiner, Pattern = "concrete texture per p.66 / DS 65", Relief = I(0.25),
        Orientation = "parallel to grade", QuantityItem = "incidental to 'Concrete barrier railing, aesthetic' (p.56 notes)", Source = "p.54 Part Sections C-C / F-F; p.66",
        Regions = { new SurfaceRegion { Name = "barrier back face recesses", Host = SurfaceHost.Barrier, Tag = "liner", Source = "p.54 C-C 'concrete texture, see DS 65'" } },
        Unresolved = { "(p.66) texture depth / pattern dimensions not read: relief 1/4 in assumed as the layer thickness only" },
    });
    br.SurfaceTreatments = list;
}

// ================================================================ existing ground (USGS 3DEP 1 m, the Woodbury tile covers Ramp B)
Doc.Project.References.RemoveAll(x => x.Name == "3DEP lidar 1 m (Sioux City I-29)");
Doc.Project.References.Add(new SBM.Core.References.ReferenceModelDefinition
{
    Name = "3DEP lidar 1 m (Sioux City I-29)", Path = @"C:\SBM\dist\terrain\sioux-city-i29-3dep-1m-utm14n.flt",
    Kind = SBM.Core.References.ReferenceKind.Terrain, Format = SBM.Core.References.ReferenceFormat.Grid, UnitScale = 1.0,
    ContourInterval = Ft(2), R = 150, G = 140, B = 120,
});

var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
Print($"Regenerated in {rep.Elapsed.TotalSeconds:F1} s: {rep.Parts:N0} parts, {rep.Bolts:N0} bolts, {rep.SteelMassKg * 2.20462:N0} lb steel (plan 926,401).");
var md = Doc.Model(br.Name);
foreach (var w in rep.Warnings.Where(w => !w.Contains("cross-frame spacing")).Take(80)) Print("warning " + w);
if (md.Steel is { } st)
    foreach (var c in st.IntegralCaps)
    {
        Print($"== {c.Mark} at {c.Support}: slope {c.Slope * 100:F3} %, length {c.Length / 0.3048:F3} ft, steel {c.SteelMassKg * 2.20462:N0} lb");
        foreach (var x in c.Checks) Print("   " + x);
        foreach (var g in c.Girders)
            Print($"   {g.Name}: S {g.S / 0.3048:F3} ft, fill {g.FillMin / I(1):F3} .. {g.FillMax / I(1):F3} in (CL {g.FillAtCl / I(1):F3}), cap camber {g.CapCamber / I(1):F3} in vs girder DL deflection {g.GirderDeflection / I(1):F3} in");
    }
if (md.Substructure is { } subm)
{
    foreach (var d in subm.Diagnostics) Print($"substructure {d.Severity}: {d.Message}");
    foreach (var u in subm.Units) Print($"substructure unit {u.Name}: {u.Solids.Count} solids, {u.Foundations.Sum(f => f.Piles.Count)} piles");
}
foreach (var u in unresolved) Print(u);
Doc.Save(outPath);
Print($"Saved {outPath}");
