// TxDOT CSJ 0047-07-228 (bridge items also CSJ 0918-47-242): US 75 PEDESTRIAN UNDERPASS AT NORTHAVEN TRAIL (HNTB, 100% plans 09/2020).
// 861.57 ft shared-use path bridge: 14 Tx28 prestressed approach spans in 5 units + one steel TIED ARCH (span 10, Pier 10 - Pier 11).
// THIS SCRIPT builds everything EXCEPT the arch superstructure: alignment, profile, cross slope, the 5 approach units (one bridge each),
// all supports and their substructure, the arch PIERS 10 / 11 with their arch-span pedestals (fixed plan tops) for the arch model
// (built separately under Arch/). Span 10 is a GAP: Pier 10 is the LAST support of unit 3 and Pier 11 the FIRST support of unit 4.
// From scratch (re-runnable; replaces the alignment and the NHT bridges), run from the repo root:
//   sbm new  "samples\plans\Northaven NHT.sbm"
//   sbm run  "samples\plans\build-nht.csx" "samples\plans\Northaven NHT.sbm"
//   sbm run  "samples\plans\check-nht.csx" "samples\plans\Northaven NHT.sbm"
// Data: docs/plan-data/nht-0047-07-228/SUMMARY.md + CSVs; PDF "C:\SBMData\0047-07-228 NHT 100% Plans_09-04-2020_comments_Reduced.pdf"
// (no text layer: every value is read on raster clips; "p." = PDF page).
// Rules (user): model the plans; standard items from the standard; nothing unstated is invented - UNRESOLVED items name the PDF page and a
// plain reason; where the program has no input the line is marked GAP; precision 1/16" and 0.01 ft; differences are reported, not fixed.
//
// ARCH INTERFACE (see CLAUDE.md "Northaven (NHT) arch interface"): bridges "NHT Unit 3 (spans 7-9)" (last support "Pier 10") and
// "NHT Unit 4 (spans 11-12)" (first support "Pier 11"); their substructure units carry FixedSeats "arch bearing south" / "arch bearing north"
// (S, N in the pier frame, Z = plan pedestal top EL. A / EL. E, p.102) -> world point = unit.World(S, N, Z). CL arch bearing line =
// CL pier + 6 7/8" toward the arch (normal to the pier line); pier lines 19 deg 34'41" from the radial.
using System.IO;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\Northaven NHT.sbm");
var unresolved = new List<string>();
var gaps = new List<string>();
void U(string pages, string item, string why, string then = "") => unresolved.Add(UnresolvedFlag.Text(pages, item, why, then));
void G(string s) => gaps.Add(s);
var reported = new List<string>();
void R(string s) => reported.Add(s);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double El(double ft) => Ft(ft);
string Fe(double m) => (m / Ft(1)).ToString("0.000");

// ================================================================ 1. alignment TRAILCLNEW = CL NHT (p.18-19 / sh 69-70)
// Curves 102 (R 60 RT) .. 109 (R 200 LT), no spirals, no station equations. PI coordinates as printed (X east, Y north, ft).
// HORIZONTAL DATUM (U12): survey control sheets 1-2 of 3 are not in the set -> plan X / Y used as a LOCAL GRID in (international) feet.
const string AlName = "CL NHT (TRAILCLNEW)";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
// The PI table cannot be used as is: curves 105/106 (PRC) and 106/107 (PCC) touch (T + T = PI distance), and the 0.01 ft rounding of the
// printed PI coordinates makes them overlap by 0.005 ft. The alignment is therefore built ELEMENT BY ELEMENT from the plan data: begin at
// PT 102 (X, Y as printed) on its ahead tangent, each curve from its printed R and delta, each tangent from the printed
// stations (PC - PT). Coordinates of every PC / PT are then compared with the printed ones (below).
double Dms(int d, int m, double s) => (d + m / 60.0 + s / 3600.0) * Math.PI / 180;
// The model alignment STARTS AT PT 102 = 20+49.30 (begin bridge, X 2499806.50 Y 7016136.59, ahead tangent N 49 21'10.46" W): curve 102 and
// its back tangent are the trail BEFORE the bridge; that back tangent (S 07 01'12.90" E) passes about 5 ft from the span-8 girders, so
// projecting bridge points onto a model alignment that contains it picks the wrong branch (seen: Pier 8 girder 1). Off the bridge, not needed.
double heading = (90 - (360 - (49 + 21 / 60.0 + 10.46 / 3600.0))) * Math.PI / 180;    // N 49 21'10.46" W as a math angle (from +x, ccw)
var elems = new List<HorizontalElement>();
var cur = new Vec2(Ft(2499806.50), Ft(7016136.59));
void Tan(double lenFt) { var e = new HorizontalElement(cur, heading, Ft(lenFt), 0, 0); elems.Add(e); cur = e.EndPoint; }
void Arc(double rFt, double delta, bool right) { double k = (right ? -1 : 1) / Ft(rFt); var e = new HorizontalElement(cur, heading, Ft(rFt) * delta, k, k); elems.Add(e); cur = e.EndPoint; heading = e.EndAngle; }
Tan(42.71);                                         //      PT 102 20+49.30 - PC 103 20+92.01
Arc(60, Dms(136, 34, 2.33), true);                  // 103  PC 20+92.01  PT 22+35.03
Tan(39.33);                                         //      22+35.03 - 22+74.36
Arc(250, Dms(19, 34, 25.12), true);                 // 104  PC 22+74.36  PT 23+59.77
Tan(40.67);                                         //      23+59.77 - 24+00.44
Arc(385, Dms(17, 36, 13.15), true);                 // 105  PC 24+00.44  PRC 25+18.73
Arc(385, Dms(19, 13, 14.31), false);                // 106  PRC 25+18.73 PCC 26+47.89
Arc(250, Dms(15, 24, 5.32), false);                 // 107  PCC 26+47.89 PT 27+15.09
Tan(118.59);                                        //      27+15.09 - 28+33.68
Arc(200, Dms(11, 11, 6.14), true);                  // 108  PC 28+33.68  PT 28+72.73
Tan(17.54);                                         //      28+72.73 - 28+90.27
Arc(200, Dms(11, 11, 6.14), false);                 // 109  PC 28+90.27  PT 29+29.31
Tan(100);
var al = new Alignment(elems) { Name = AlName, StartStation = Sta("20+49.30") };
Doc.Project.Alignments.Add(al);
al.Description = "CL NHT = TRAILCLNEW (CSJ 0047-07-228 p.18-19); plan X/Y as a local grid in ft (datum not in the set, U12)";
U("p.17-19", "horizontal datum / projection / scale factor (U12)", "survey control sheets 1-2 of 3 are not in the set", "Plan X / Y used as a local grid in feet.");
{
    (string name, string sta)[] keys = { ("PT 102 (begin bridge)", "20+49.30"), ("PC 103", "20+92.01"), ("PT 103", "22+35.03"), ("PC 104", "22+74.36"), ("PT 104", "23+59.77"),
        ("PC 105", "24+00.44"), ("PRC 105/106", "25+18.73"), ("PCC 106/107", "26+47.89"), ("PT 107", "27+15.09"), ("PC 108", "28+33.68"), ("PT 108", "28+72.73"),
        ("PC 109", "28+90.27"), ("PT 109", "29+29.31") };
    (double x, double y)[] xy = { (2499806.50, 7016136.59), (2499774.09, 7016164.41), (2499810.26, 7016269.86), (2499849.55, 7016271.77), (2499933.91, 7016261.42),
        (2499972.85, 7016249.67), (2500079.12, 7016198.78), (2500195.83, 7016144.89), (2500262.26, 7016136.18), (2500380.85, 7016136.66), (2500419.67, 7016133.02),
        (2500436.89, 7016129.68), (2500475.70, 7016126.04) };
    double worst = 0;
    for (int k = 0; k < keys.Length; k++)
    {
        var so = al.Inverse(new Vec2(Ft(xy[k].x), Ft(xy[k].y)));
        double dSta = (so.Station - Sta(keys[k].sta)) / Ft(1), dOff = so.Offset / Ft(1);
        worst = Math.Max(worst, Math.Max(Math.Abs(dSta), Math.Abs(dOff)));
        if (Math.Abs(dSta) > 0.01 || Math.Abs(dOff) > 0.01) R($"alignment {keys[k].name}: plan point is {dSta:+0.000;-0.000} ft along / {dOff:+0.000;-0.000} ft off the model alignment (rounded PI coordinates)");
    }
    Print($"Alignment: {keys.Length} plan PC/PT points within {worst:0.000} ft of the model (station / offset)");
}

// Profile TRAILNEW (p.20 / sh 71): VPI 7 20+00.00 523.10; VPI 8 23+40.01 538.08 L 140; VPI 9 26+50.01 533.10 L 50; VPI 10 32+25.01 504.92 L 100.
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(Sta("20+00.00"), El(523.10)),
        new Vpi(Sta("23+40.01"), El(538.08), Ft(140)),
        new Vpi(Sta("26+50.01"), El(533.10), Ft(50)),
        new Vpi(Sta("32+25.01"), El(504.92), Ft(100)),
    },
};
Print($"PGL begin bridge 20+49.30 = {Fe(al.Profile.ElevationAt(Sta("20+49.30")))} (plan 525.27), end 29+10.87 = {Fe(al.Profile.ElevationAt(Sta("29+10.87")))} (plan 520.32)");
// Cross slope (p.89, 90, 152): crown at CL NHT & PGL, 1.00 % down each way, approach and arch; no superelevation.
al.CrossSlope = new CrossSlopeTable { PivotOffset = 0, Rows = { new CrossSlopeRow(al.StartStation, -0.01, -0.01) } };

// ================================================================ 2. supports (p.84-88 layout, p.94 foundation layout / schedule, p.102)
// Support line = the plan's station line of each support: CL pier (= CL cap = CL column, p.97/99/102) or, at the abutments, the face of
// backwall = CL cap = CL drilled shafts (p.95 plan). Approach supports radial (90 deg); arch piers 19 deg 34'41" (p.102 note "*", p.94 schedule).
double Az(char ns, int d, int m, double s, char ew)      // quadrant bearing -> azimuth (deg from north, clockwise)
{
    double a = d + m / 60.0 + s / 3600.0;
    return ns == 'N' ? (ew == 'E' ? a : 360 - a) : (ew == 'E' ? 180 - a : 180 + a);
}
// Skew (deg from the radial, + = right end up-station) of a pier line given by its plan bearing.
double SkewOf(double sta, double azDeg)
{
    var t = Vec2.FromAngle(al.AngleAt(sta));
    var r = t.Right;
    double a = azDeg * Math.PI / 180;
    var d = new Vec2(Math.Sin(a), Math.Cos(a));
    if (Vec2.Dot(d, r) < 0) d = -d;
    return Math.Atan2(Vec2.Dot(d, t), Vec2.Dot(d, r)) * 180 / Math.PI;
}
// name, station (p.84-88 / p.94), plan pier-line bearing (p.94 foundation schedule), type
var supData = new (string name, string sta, double az, bool abut)[]
{
    ("Abutment 1", "20+49.30", Az('N', 40, 38, 49.54, 'E'), true),
    ("Pier 2", "20+97.18", Az('N', 45, 34, 41.18, 'E'), false),
    ("Pier 3", "21+27.18", Az('N', 74, 13, 33.58, 'E'), false),
    ("Pier 4", "21+57.18", Az('S', 77, 7, 34.02, 'E'), false),
    ("Pier 5", "21+87.18", Az('S', 48, 28, 41.61, 'E'), false),
    ("Pier 6", "22+17.18", Az('S', 19, 49, 49.21, 'E'), false),
    ("Pier 7", "22+54.18", Az('S', 2, 47, 8.14, 'E'), false),
    ("Pier 8", "23+13.74", Az('S', 6, 14, 22.28, 'W'), false),
    ("Pier 9", "23+38.74", Az('S', 11, 58, 8.76, 'W'), false),
    ("Pier 10", "24+17.92", Az('S', 0, 11, 21.26, 'E'), false),
    ("Pier 11", "26+19.60", Az('S', 0, 11, 49.94, 'E'), false),
    ("Pier 12", "26+86.87", Az('S', 6, 14, 11.62, 'W'), false),
    ("Pier 13", "27+42.87", Az('S', 0, 13, 49.49, 'E'), false),
    ("Pier 14", "27+98.87", Az('S', 0, 13, 49.49, 'E'), false),
    ("Pier 15", "28+54.87", Az('S', 5, 50, 19.38, 'W'), false),
    ("Abutment 16", "29+10.87", Az('S', 5, 3, 8.70, 'W'), true),
};
U("p.94 vs p.87, p.97", "Pier 12 station (U2)", "the foundation schedule prints 26+86.67, the layout and the pier table 26+86.87",
  "26+86.87 used: it is the one consistent with the spans (27+42.87 - 56.00 = 26+86.87, p.87 / p.114-115).");
double archSkew = 19 + 34 / 60.0 + 41 / 3600.0;
var skew = new Dictionary<string, double>();
foreach (var s in supData)
{
    double sk = SkewOf(Sta(s.sta), s.az);
    bool arch = s.name is "Pier 10" or "Pier 11";
    double plan = arch ? Math.Sign(sk) * archSkew : 0;
    skew[s.name] = plan;
    if (Math.Abs(sk - plan) * 3600 > 2) R($"{s.name}: pier-line bearing (p.94) gives {sk:0.00000} deg from the radial vs plan {plan:0.00000} deg ({(sk - plan) * 3600:+0.0;-0.0}\")");
}
Print($"Arch pier skews from the plan bearings: Pier 10 {SkewOf(Sta("24+17.92"), supData[9].az):0.0000}, Pier 11 {SkewOf(Sta("26+19.60"), supData[10].az):0.0000} deg (plan 19 deg 34'41\" = {archSkew:0.0000}); the p.102 note says \"to tangent\" but the line is 19.58 deg from the RADIAL (70 25'19\" to the tangent, p.84-86).");

// ================================================================ 3. prestressed girders (IGND p.118, framing plans p.108-117)
// TxDOT Tx28, 2 per span, 0.6" Gr 270 low-relaxation strands; IGND design table (p.118): spans / total strands / depressed (no, to end) / f'ci / f'c.
PrestressedBeamDefinition Tx28(int n, int dep, double toEnd, double fci, double fc, string spans) => new()
{
    Type = TxGirderType.Tx28, StrandCount = n, StrandDiameter = In(0.6), StrandStrength = 270,
    DepressedStrands = dep, DepressedTopAtEnd = In(toEnd), ReleaseStrength = fci, ConcreteStrength = fc,
    Designation = $"Tx28 ({n} strands)", Source = $"IGND p.118 / sh 169, spans {spans}",
    // Bearings: TxDOT IGEB (Aug 2017, C:\SBM\dist\SteelBridgeModeler\IG-IGEB-17.pdf) Table of Bearing Pad Dimensions, Tx28-Tx54, girder end skew
    // 0-21 deg (every approach end here: 0-16.6 deg): 8" x 21" pad, no clips, at abutments and conventional interior bents. Pad thickness (layer
    // count "N" of the G-1-"N" mark) is a bearing-layout item not in the set; the program's IGEB default 2 3/4" reproduces every p.91 seat (check-nht).
    PadLength = In(8), PadWidth = In(21),
};
// Plan span numbers -> beam: 1-6: 8 strands; 7: 14 (2 depressed, 6.5" at end); 8: 10; 9: 28 (4 depressed, 24.5"); 11: 20 (4 depressed, 22.5"); 12-15: 12.
PrestressedBeamDefinition BeamFor(int planSpan) => planSpan switch
{
    <= 6 => Tx28(8, 0, 0, 4.0, 5.0, "1-6"),
    7 => Tx28(14, 2, 6.5, 4.1, 5.0, "7"),
    8 => Tx28(10, 0, 0, 4.0, 5.0, "8"),
    9 => Tx28(28, 4, 24.5, 5.8, 7.8, "9"),
    11 => Tx28(20, 4, 22.5, 4.1, 5.4, "11"),
    _ => Tx28(12, 0, 0, 4.3, 5.0, "12-15"),
};
G("IGEB girder conflict details (IG-IGEB-17 sheet 1; plan notes \"GIRDER CONFLICTS AT PIERS NO. 2-4\" p.108, \"... AT ALL LOCATIONS\" p.110, 112): girder ends skewed on the bisector between the girders to keep 3\" clear; only square / parallel-to-bent ends exist -> ends modeled SQUARE to the girder at 9\" beyond the CL bearing (plan lengths); girder end corners may overlap at the piers.");
G("IGND strand eccentricities (e at CL 10.48 / end 10.48 .. 6.28 in, p.118) are check values; the strands are placed by the IGND fill rule (TxGirderCatalog), not compared yet.");

// Girder layout per span = the framing plans' PIER REPORT (p.108, 110, 112, 114, 116; U13 transcribed here): "distance between station line and
// girder 1" (L = left, along the CL pier / abutment line from CL NHT) and the girder spacing along it. Bearings: CL brg 1'-0 from CL pier MEASURED
// ALONG THE GIRDER at the approach piers (IGEB, p.108 note 1; confirmed: GIRDER REPORT C-C pier - C-C brg = 2.000 on spans 2-6); 1'-0 NORMAL to the
// face of backwall at the abutments (p.95 section A-A; span 15: 2.003 = 1.000 + 1.0024 at the 86 deg abutment line); 1'-9 1/4 NORMAL to CL pier at
// Piers 10 / 11 (p.102; span 9: 78.046 - 75.199 = 1.000 + 1.847 = 1.771 / sin 73 28'39").
// (planSpan, begin: L, spacing, bearing, normal?, end: L, spacing, bearing, normal?)
var layouts = new (int span, double l0, double s0, double b0, bool n0, double l1, double s1, double b1, bool n1)[]
{
    (1, 4.833, 9.667, 1.0, true, 5.074, 9.703, 1.0, false),
    (2, 5.833, 9.929, 1.0, false, 5.833, 9.929, 1.0, false),
    (3, 5.833, 9.929, 1.0, false, 5.833, 9.929, 1.0, false),
    (4, 5.833, 9.929, 1.0, false, 5.833, 9.929, 1.0, false),
    (5, 5.833, 9.929, 1.0, false, 5.833, 9.929, 1.0, false),
    (6, 5.833, 9.929, 1.0, false, 5.833, 9.941, 1.0, false),
    (7, 5.833, 9.941, 1.0, false, 5.833, 10.551, 1.0, false),
    (8, 5.833, 10.667, 1.0, false, 4.833, 9.667, 1.0, false),
    (9, 4.833, 9.667, 1.0, false, 5.174, 10.253, 1.771, true),
    (11, 4.075, 10.260, 1.771, true, 3.775, 9.609, 1.0, false),
    (12, 3.775, 9.609, 1.0, false, 4.833, 9.667, 1.0, false),
    (13, 4.833, 9.667, 1.0, false, 4.833, 9.667, 1.0, false),
    (14, 4.833, 9.667, 1.0, false, 4.833, 9.667, 1.0, false),
    (15, 4.833, 9.667, 1.0, false, 4.833, 9.667, 1.0, true),
};
double Brg1(double ft) => ft == 1.771 ? Len("1'-9 1/4\"") : Ft(ft);
GirderSpanEnd End(double l, double spa, double brg, bool normal) => new()
{
    Measure = SpanOffsetMeasure.AlongLine, ReferenceOffset = 0, Offsets = { -Ft(l) }, Spacings = new() { Ft(spa) },
    BearingOffset = Brg1(brg), BearingMeasure = normal ? BearingOffsetMeasure.NormalToSupport : BearingOffsetMeasure.AlongGirder,
};

// ================================================================ 4. the 5 approach units (one bridge each; p.84-88, 108-117)
const string Structure = "US 75 Ped. Underpass at Northaven Trail (CSJ 0047-07-228)";
Doc.Project.Bridges.RemoveAll(b => b.Structure == Structure);
var units = new (string name, string[] sups, int firstSpan)[]
{
    ("NHT Unit 1 (spans 1-3)", new[] { "Abutment 1", "Pier 2", "Pier 3", "Pier 4" }, 1),
    ("NHT Unit 2 (spans 4-6)", new[] { "Pier 4", "Pier 5", "Pier 6", "Pier 7" }, 4),
    ("NHT Unit 3 (spans 7-9)", new[] { "Pier 7", "Pier 8", "Pier 9", "Pier 10" }, 7),
    ("NHT Unit 4 (spans 11-12)", new[] { "Pier 11", "Pier 12", "Pier 13" }, 11),
    ("NHT Unit 5 (spans 13-15)", new[] { "Pier 13", "Pier 14", "Pier 15", "Abutment 16" }, 13),
};
var shared = new HashSet<string> { "Pier 4", "Pier 7", "Pier 13" };
var bridges = new Dictionary<string, BridgeDefinition>();
var subOf = new Dictionary<string, SupportDefinition>();     // owner support definition of each physical support
foreach (var (uname, sups, firstSpan) in units)
{
    var br = new BridgeDefinition { Name = uname, AlignmentName = AlName, Structure = Structure };
    br.FieldSplices.Clear();
    br.Studs.Enabled = false;
    for (int k = 0; k < sups.Length; k++)
    {
        var d = supData.First(x => x.name == sups[k]);
        bool first = k == 0, last = k == sups.Length - 1;
        var sup = new SupportDefinition
        {
            Name = d.name, Type = d.abut ? SupportType.Abutment : SupportType.Pier, Station = Sta(d.sta), SkewDegrees = skew[d.name],
            // Deck ends (p.109-117 plans): at the abutments the slab ends at the face of backwall (= the support line); at the unit joints
            // P4 / P7 / P13 at CL pier; at Piers 10 / 11 the SEJ is 9 1/4" from CL pier normal to it, on the approach side (p.113, 115).
            DeckEndOffset = d.name is "Pier 10" or "Pier 11" ? -In(9.25) : 0,
            SharedPier = shared.Contains(d.name) ? d.name : "",
            Substructure = new SubstructureDefinition { Enabled = false },
        };
        br.Supports.Add(sup);
        if (!subOf.ContainsKey(d.name)) subOf[d.name] = sup;
    }
    // Deck (p.89, 109): 15'-8" overall = 7'-10" each side of CL NHT (curb 10" + 14'-0" clear), 8 1/2" CIP slab on PCP panels,
    // X = 10 1/2" top of slab to top of girder at CL brg (all spans, p.109/113/115 tables) -> haunch 2".
    br.Deck.LeftEdgeOffset = -Len("7'-10\"");
    br.Deck.RightEdgeOffset = Len("7'-10\"");
    br.Deck.Thickness = In(8.5);
    br.Deck.OverhangThickness = In(8.5);
    br.Deck.Haunch = In(2);
    br.Deck.StayInPlaceForms = false;
    br.Deck.Reinforcement.Enabled = false;
    br.Deck.Barrier.Type = BarrierType.None;
    br.Deck.Barrier.Left = false; br.Deck.Barrier.Right = false;
    // Girders
    br.Girders.Count = 2;
    br.Girders.Kind = GirderKind.TxDotPrestressed;
    br.Girders.PlanLayout = GirderPlanLayout.Chorded;
    br.Girders.FirstGirderOffset = -Ft(4.833);
    br.Girders.Spacings = new() { Ft(9.667) };
    int nSpan = sups.Length - 1;
    br.Girders.Prestressed = BeamFor(firstSpan);
    br.Girders.Prestressed.SpanBeams = Enumerable.Range(0, nSpan).Select(j => new PrestressedSpanBeam { Spans = new() { j }, Beam = BeamFor(firstSpan + j) }).ToList();
    br.Girders.SpanLayouts = Enumerable.Range(0, nSpan).Select(j =>
    {
        var l = layouts.First(x => x.span == firstSpan + j);
        return new GirderSpanLayout { Span = j, Count = 2, Begin = End(l.l0, l.s0, l.b0, l.n0), End = End(l.l1, l.s1, l.b1, l.n1) };
    }).ToList();
    Doc.Project.Bridges.Add(br);
    bridges[uname] = br;
}
G("RAIL (PED) low 4'-3 / high 9'-10 posts (p.84, 89) and the 10\" curbs (height not dimensioned, p.109): no pedestrian rail / curb profile in BarrierProfiles -> no rail or curb modeled.");
G("Deck reinforcing (bars A, C, CS, D, G, H, J, M, OA, P, T, UP; p.109-117 bar tables, not transcribed - U13) not modeled; PCP panels not modeled separately (8 1/2\" slab solid).");
G("Expansion joints SEJ-B(MOD) 4\" + BS-EJCP (p.84-88, 91): standard not in the set; deck ends modeled at the joint line, no blockout / device.");
U("p.84-88, 109-117", "deck gap at the SEJ-B(MOD) joints (Abut 1, P4, P7, P10, P11, P13, Abut 16)", "the SEJ-B(MOD) standard is not in the set, the sheets show only the joint line",
  "Each deck ends AT the joint line (P4 / P7 / P13: CL pier; P10 / P11: 9 1/4\" from CL pier; abutments: face of backwall).");
G("Prestressed camber / dead-load deflections (p.109-117 tables A / B, Z at CL span): beams straight, haunch 2\" constant along the beam (plan Z at midspan is a check value).");

// ---------------------------------------------------------------- support frames and girder chords (script geometry = the generator's)
// Support frame: A = CL NHT point, d = along the line (left -> right), n = normal (up-station). Girder points on the line = A + d x offset.
(Vec2 a, Vec2 d, Vec2 n) Frame(string sup)
{
    var s = supData.First(x => x.name == sup);
    double sta = Sta(s.sta), sk = skew[sup] * Math.PI / 180;
    var t = Vec2.FromAngle(al.AngleAt(sta));
    var r = t.Right;
    return (al.PointAt(sta, 0), r * Math.Cos(sk) + t * Math.Sin(sk), -r * Math.Sin(sk) + t * Math.Cos(sk));
}
(string b, string e) SpanSupports(int planSpan)
{
    foreach (var (_, sups, first) in units)
        if (planSpan >= first && planSpan < first + sups.Length - 1) return (sups[planSpan - first], sups[planSpan - first + 1]);
    throw new ArgumentException($"span {planSpan}");
}
// Girder chord (unit, ahead) of girder g (0 / 1) of a plan span, from the PIER REPORT points on the two support lines.
Vec2 Chord(int planSpan, int g)
{
    var l = layouts.First(x => x.span == planSpan);
    var (sb, se) = SpanSupports(planSpan);
    var fb = Frame(sb); var fe = Frame(se);
    var p0 = fb.a + fb.d * (-Ft(l.l0) + g * Ft(l.s0));
    var p1 = fe.a + fe.d * (-Ft(l.l1) + g * Ft(l.s1));
    return (p1 - p0).Normalized();
}
// Side angle (deg from the support normal, + = S grows with N) of the parallelogram seats under the girders of a span at one support
// (mean of the two girders) and the plan girder angle (pier-line to girder) for the record.
double SideAngle(string sup, int planSpan)
{
    var f = Frame(sup);
    return Enumerable.Range(0, 2).Select(g => { var c = Chord(planSpan, g); return Math.Atan2(Vec2.Dot(c, f.d), Vec2.Dot(c, f.n)) * 180 / Math.PI; }).Average();
}

// ================================================================ 5a. approach piers 2-9, 12-15 (p.97-100)
// Cap 15'-0 (7'-6 + 7'-6 about CL pier) x 3'-6 (1'-9 + 1'-9), top LEVEL at EL. A = EL. B (cap ends), soffit EL. C at the column.
// Piers 2-5, 8, 9, 12, 13 (p.97): column 3'-0 x 3'-0 on CL pier; cap 1'-6 deep at the ends, soffit straight to the column faces (2'-6 at the column).
// Piers 6, 7, 14, 15 (p.99): column 4'-0 (along the cap) x 3'-0, CL 3.782' RIGHT of CL pier sta (p.94 "3.782 RT", p.99 plan 3.782 / 3.718); cap
// 1'-6 deep at the left end, soffit straight to the left column face, then level 3'-6 deep to the right end (p.99 elevation).
// Bearing seats (p.97/99 plan, p.98/100 BEARING SEAT DETAIL): "LEVEL 3'-0 FOR BEARING SEAT" = 1'-6 + 1'-6 square to the girder, from CL pier to
// the cap face (1'-9), sides parallel to the girders -> plan pedestal per seat (parallelogram), top = the bearing seat; 1 1/2" min at CL brg.
// Drilled shafts 48" (p.98 / p.100 tables): top = EL. D, tip as printed.
var tips = new Dictionary<string, double>
{
    ["Pier 2"] = 492.224, ["Pier 3"] = 492.546, ["Pier 4"] = 492.867, ["Pier 5"] = 493.189, ["Pier 6"] = 492.510, ["Pier 7"] = 496.134,
    ["Pier 8"] = 496.368, ["Pier 9"] = 495.883, ["Pier 12"] = 481.129, ["Pier 13"] = 481.392, ["Pier 14"] = 491.647, ["Pier 15"] = 491.900,
};
string DataDir()
{
    for (var dd = new DirectoryInfo(Environment.CurrentDirectory); dd != null; dd = dd.Parent)
    {
        var p = Path.Combine(dd.FullName, "docs", "plan-data", "nht-0047-07-228");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/nht-0047-07-228 not found above the current directory - run from the repo.");
}
var pierRows = File.ReadAllLines(Path.Combine(DataDir(), "approach-piers.csv")).Skip(1).Where(x => x.Trim() != "").Select(x => x.Split(',')).ToList();
var wide = new HashSet<string> { "Pier 6", "Pier 7", "Pier 14", "Pier 15" };
foreach (var row in pierRows)
{
    string name = row[0];
    double elA = double.Parse(row[8]), elB = double.Parse(row[9]), elC = double.Parse(row[10]), elD = double.Parse(row[11]);
    if (Math.Abs(elA - elB) > 0.0005) R($"{name}: EL. A {elA:0.000} <> EL. B {elB:0.000} (p.97/99) - cap modeled level at EL. A");
    bool w = wide.Contains(name);
    double zA = El(elA), zC = El(elC);
    var sd = new SubstructureDefinition
    {
        CapWidth = F(3, 6), CapDepth = zA - zC, CapOffset = 0,
        ColumnCount = 1, ColumnSpacings = new(), FirstColumnOffset = w ? Ft(3.782) : 0,
        ColumnShape = ColumnShape.Rectangular, ColumnWidth = w ? Ft(4) : Ft(3), ColumnDepth = Ft(3),
        Foundation = FoundationType.DrilledShaft, ShaftDiameter = Ft(4), FoundationTopElevation = El(elD), ShaftLength = El(elD) - El(tips[name]),
        PedestalSizeAcross = F(1, 9), PedestalRowJoint = 0, MinPedestalHeight = 0,
    };
    sd.Reinforcement.Enabled = false;
    if (w)
    {
        if (Math.Abs((zA - zC) - F(3, 6)) > In(1.0 / 16)) R($"{name}: EL. A - EL. C = {Fe(zA - zC)} vs 3'-6 (p.99)");
        sd.CapProfile = new() { new(-Ft(7.5), zA), new(Ft(7.5), zA), new(Ft(7.5), zC), new(Ft(3.782) - Ft(2), zC), new(-Ft(7.5), zA - F(1, 6)) };
    }
    else
    {
        if (Math.Abs((zA - zC) - F(2, 6)) > In(1.0 / 16)) R($"{name}: EL. A - EL. C = {Fe(zA - zC)} vs 2'-6 (p.97)");
        sd.CapProfile = new() { new(-Ft(7.5), zA), new(Ft(7.5), zA), new(Ft(7.5), zA - F(1, 6)), new(Ft(1.5), zC), new(-Ft(1.5), zC), new(-Ft(7.5), zA - F(1, 6)) };
    }
    // seat parallelograms per row: back row = the span ending here, ahead row = the span starting here
    int back = layouts.Select(x => x.span).FirstOrDefault(sp => SpanSupports(sp).e == name);
    int ahead = layouts.Select(x => x.span).FirstOrDefault(sp => SpanSupports(sp).b == name);
    double thB = back > 0 ? SideAngle(name, back) : SideAngle(name, ahead), thA = ahead > 0 ? SideAngle(name, ahead) : thB;
    sd.PedestalSideAngleDeg = thB; sd.PedestalSizeAlong = Ft(3) / Math.Cos(thB * Math.PI / 180);
    sd.PedestalSideAngleAheadDeg = thA; sd.PedestalSizeAlongAhead = Ft(3) / Math.Cos(thA * Math.PI / 180);
    subOf[name].Substructure = sd;
}
G("Approach pier cap reinforcing / column / shaft bars (p.97-100 bar tables) not entered: substructure reinforcement off. Mow strips / riprap (p.97-100) and anti-graffiti coating not modeled.");
U("p.97, 99", "column corner chamfers / cap edge chamfers of the approach piers", "chamfer sizes are not dimensioned on the sheets", "Square corners modeled.");

// ================================================================ 5b. abutments 1 and 16 (p.95, 96)
// Support line = face of backwall = CL cap = CL drilled shafts (p.95/96 plan). Cap 3'-6 (1'-9 + 1'-9) x 2'-6, top LEVEL at EL. C = EL. D; backwall
// 1'-0 behind the face of backwall, top EL. A / B at the backwall ends; 2 x 36" drilled shafts 9'-0 apart, 4'-6 each side of CL NHT; seats as at the
// piers ("level 3'-0" square to the girder, from the face of backwall to the cap front face, p.95 / 96 plan). CL brg 1'-0 from the face of backwall.
// Abut 1: cap 14.583 = 7.291 (right) + 7.292 (left), EL A/B 524.699, C/D 521.637, shafts top 519.153 tip 488.153 (31 ft).
// Abut 16: cap 14.563 = 7.279 (left) + 7.284 (right), EL A/B 519.744, C/D 516.682, shafts top 514.185 tip 492.185 (22 ft).
void Abut(string name, double sLeft, double sRight, double elA, double elC, double shaftTop, double shaftTip, int planSpan, string pages)
{
    var sup = subOf[name];
    bool first = name == "Abutment 1";
    double zC = El(elC), zBot = zC - F(2, 6);
    var sd = new SubstructureDefinition
    {
        CapWidth = F(3, 6), CapDepth = F(2, 6), BearingToCapFront = F(1, 9), BackwallThickness = Ft(1), BackwallGap = 0,
        ColumnCount = 0, ColumnSpacings = new(), Foundation = FoundationType.DrilledShaft, ShaftDiameter = Ft(3),
        AbutmentFoundationCount = 2, AbutmentFoundationSpacing = Ft(9), ShaftLength = zBot - El(shaftTip),
        PedestalSizeAcross = F(1, 9), PedestalRowJoint = 0, MinPedestalHeight = 0,
    };
    sd.Reinforcement.Enabled = false;
    sd.CapProfile = new() { new(-Ft(sLeft), zC), new(Ft(sRight), zC), new(Ft(sRight), zBot), new(-Ft(sLeft), zBot) };
    double th = SideAngle(name, planSpan);
    sd.PedestalSideAngleDeg = th; sd.PedestalSideAngleAheadDeg = th;
    sd.PedestalSizeAlong = Ft(3) / Math.Cos(th * Math.PI / 180); sd.PedestalSizeAlongAhead = sd.PedestalSizeAlong;
    // Backwall top: the generator follows finished grade minus BackwallTopBelowGrade; the value is DERIVED so the top reproduces EL. A at the backwall
    // end (mid-thickness of the backwall, right end).
    var f = Frame(name);
    double nMid = (first ? -1 : 1) * Ft(0.5);
    var pEnd = f.a + f.d * Ft(sRight) + f.n * nMid;
    var so = al.Inverse(pEnd);
    double below = al.ElevationAt(so.Station, so.Offset) - El(elA);
    sd.BackwallTopBelowGrade = below;
    R($"{name}: backwall top = finished grade - {below / In(1):0.00}\" (DERIVED from EL. A {elA:0.000}, {pages}); section A-A notes 3\" from finished grade.");
    R($"{name}: shaft top {shaftTop:0.000} (plan) vs cap bottom {Fe(zBot)} = EL. C - 2'-6 ({(El(shaftTop) - zBot) / Ft(1):+0.000;-0.000} ft); shafts modeled from the cap bottom to the plan tip {shaftTip:0.000}.");
    sup.Substructure = sd;
}
Abut("Abutment 1", 7.292, 7.291, 524.699, 521.637, 519.153, 488.153, 1, "p.95");
Abut("Abutment 16", 7.279, 7.284, 519.744, 516.682, 514.185, 492.185, 15, "p.96");
G("Abutment 16 left cap end is skewed 5 19'17\" to the retaining wall (p.96 plan, 0.163' over the cap front); the parametric abutment cap end is square to the support line.");
G("Abutment reinforcing (A, H, S, U, V bars, p.95/96 tables) and the 1/2\" preformed bituminous fiber at the wall faces not modeled; wingwalls are retaining walls by others (MSE, p.47-69).");

// ================================================================ 5. arch piers 10 & 11 (p.101-107; hammerhead)
// Cap (p.102 plan / elevation): 24'-0 (12'-0 + 12'-0 about CL pier & drilled shaft) x 6'-0 (3'-0 + 3'-0 about CL pier), 6'-0 deep at the
// column (2'-9 + 3'-3), 2'-9 at the ends, soffit straight from the end to the column face (9'-9 cantilevers), column 4'-6 square on a 6'-6 shaft.
// Shaft / column CL is 0'-7" from the CL NHT pier point along the pier line: P10 LEFT, P11 RIGHT (p.94 schedule "0.584 LT / RT", p.102).
// Cap top EL. C (at CL pier), soffit EL. F; shaft top / tip (p.101 table). Arch pedestals 3'-4 x 1'-7 at 63 deg to the pier line (p.102 plan),
// tops LEVEL at EL. A / EL. E; arch bearings 9'-10 and 9'-8 1/8 from CL pier & shaft (p.102). CL arch brg 6 7/8" from CL pier (arch side).
// Orientation (DERIVED from p.102 + p.91): the p.102 plan is drawn with the arch side DOWN; for Pier 10 (arch ahead) page-right = LEFT, for
// Pier 11 (arch back) page-right = RIGHT (shaft 0'-7" page-right = P10 LT / P11 RT as p.94; approach seats EL. D = G1 (left) at P10, EL. B = G1 at P11
// as p.91). So EL. A (page-left pedestal, 9'-10 from the shaft) is the SOUTH (right) arch bearing at Pier 10 and the NORTH (left) one at Pier 11.
SubstructureDefinition ArchPier(bool p10, double elA, double elE, double elC, double elF, double shaftTop, double shaftTip, double sideDeg)
{
    double sS = (p10 ? -1 : 1) * In(7);                 // shaft / column CL along the pier line from CL NHT
    double zC = El(elC), zF = El(elF);
    if (Math.Abs((zC - zF) - Ft(6)) > In(1.0 / 16)) R($"Pier {(p10 ? 10 : 11)}: EL. C - EL. F = {Fe(zC - zF)} ft vs the 6'-0 cap depth (p.102)");
    var sd = new SubstructureDefinition
    {
        CapWidth = Ft(6), CapDepth = Ft(6), CapOffset = 0,
        ColumnCount = 1, ColumnSpacings = new(), FirstColumnOffset = sS,
        ColumnShape = ColumnShape.Rectangular, ColumnWidth = F(4, 6), ColumnDepth = F(4, 6),
        Foundation = FoundationType.DrilledShaft, ShaftDiameter = F(6, 6), FoundationTopElevation = El(shaftTop), ShaftLength = El(shaftTop) - El(shaftTip),
        // approach-span seats (p.102 "approach span pedestal (typ.)"): 1'-6 + 1'-6 measured square to the girder, from CL pier to the cap face
        // (3'-0 across), sides parallel to the girders (** angle table = the girder angles of p.112 / 114).
        PedestalSideAngleDeg = sideDeg, PedestalSideAngleAheadDeg = sideDeg,
        PedestalSizeAlong = Ft(3) / Math.Cos(sideDeg * Math.PI / 180),
        PedestalSizeAcross = Ft(3), PedestalRowJoint = 0, MinPedestalHeight = 0,
    };
    sd.Reinforcement.Enabled = false;
    sd.CapProfile = new()
    {
        new(sS - Ft(12), zC), new(sS + Ft(12), zC), new(sS + Ft(12), zC - F(2, 9)), new(sS + F(2, 3), zF), new(sS - F(2, 3), zF), new(sS - Ft(12), zC - F(2, 9)),
    };
    // arch pedestals: CapBlocks with a FIXED plan top (CapBlockKind.Pedestal) -> SubstructureUnit.FixedSeats
    double nArch = (p10 ? 1 : -1) * In(6.875);
    double sA = sS + (p10 ? 1 : -1) * F(9, 10);                 // EL. A pedestal: page-left, 9'-10 from the shaft
    double sE = sS - (p10 ? 1 : -1) * F(9, 8.125);              // EL. E pedestal: page-right, 9'-8 1/8 from the shaft
    double ang = 63 * Math.PI / 180;
    var u = new Vec2(Math.Cos(ang), Math.Sin(ang));             // long axis (3'-4) in (S, N), 63 deg from the pier line (same for both piers, DERIVED)
    var v = new Vec2(-u.Y, u.X);
    CapBlockDefinition Ped(string name, double s, double top) => new()
    {
        Name = name, Kind = CapBlockKind.Pedestal, Base = CapBlockBase.CapTop, TopElevation = El(top), SeatS = s, SeatN = nArch,
        Source = "p.102 / sh 153",
        Outline = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(c => { var q = new Vec2(s, nArch) + u * (c.Item1 * F(3, 4) / 2) + v * (c.Item2 * F(1, 7) / 2); return new FootingPoint(q.X, q.Y); }).ToList(),
    };
    string south = p10 ? "A" : "E", north = p10 ? "E" : "A";
    double sSouth = p10 ? sA : sE, sNorth = p10 ? sE : sA, zSouth = p10 ? elA : elE, zNorth = p10 ? elE : elA;
    sd.CapBlocks.Add(Ped("arch bearing south", sSouth, zSouth));
    sd.CapBlocks.Add(Ped("arch bearing north", sNorth, zNorth));
    sd.CapBlocks[0].Source += $" EL. {south}"; sd.CapBlocks[1].Source += $" EL. {north}";
    return sd;
}
// Elevation table (p.102): P10 A 534.779 B 533.345 C 533.219 D 533.400 E 534.726 F 527.219; P11 A 531.546 B 530.106 C 529.896 D 530.036 E 531.494 F 523.896.
// Shaft table (p.101): P10 top 509.150 tip 492.150 (17.0 ft); P11 top 509.110 tip 480.110 (29.0 ft). Girder angles at the approach brg: p.112 P10 73 28'39" /
// 73 20'28" (mean 73.41), p.114 P11 76 09'10" / 76 31'36" (mean 76.34).
var p10 = subOf["Pier 10"]; var p11 = subOf["Pier 11"];
p10.Substructure = ArchPier(true, 534.779, 534.726, 533.219, 527.219, 509.150, 492.150, SideAngle("Pier 10", 9));
p11.Substructure = ArchPier(false, 531.546, 531.494, 529.896, 523.896, 509.110, 480.110, SideAngle("Pier 11", 11));
Print($"Approach seat side angles (girder chords): Pier 10 {SideAngle("Pier 10", 9):0.000} deg from the pier normal (plan ** 73 28'39\" / 73 20'28\" -> {90 - 73.41:0.000}), Pier 11 {SideAngle("Pier 11", 11):0.000} (plan 76 09'10\" / 76 31'36\" -> {90 - 76.34:0.000})");
G("Arch pier cap: top 1/4\"/ft drain slope (p.102) not modeled (the cap profile is extruded level across N); cap edge chamfers and the column corner chamfers (p.102/103, size not dimensioned) not modeled.");
G("Arch pier reinforcing (bar-lists.csv, p.103-104) not entered: substructure reinforcement off for Piers 10 / 11.");
G("T80SS(MOD) traffic barrier on the arch piers (p.105-106, 78 LF) not modeled.");
// B1-B3 (SUMMARY section 8): the p.102 pedestal tops are the plan values; the p.119 tie WP chain does not reconcile -> reported, not fixed.
{
    double stack = In(2) + In(11.75);                     // ~2" steel below the WP (p.124, UNVERIFIED) + bearing H 11.75" (p.107)
    void Chain(SupportDefinition sp, double wp, string pier)
    {
        foreach (var cb in sp.Substructure!.CapBlocks)
        {
            double expect = El(wp) - stack, d = cb.TopElevation - expect;
            cb.Reported = new()
            {
                $"B1-B3 (p.102, 107, 119, 124): plan pedestal top {Fe(cb.TopElevation)} vs tie WP {wp:0.00} - 2\" steel - 11.75\" bearing = {Fe(expect)} ({(d / Ft(1)):+0.000;-0.000} ft); the plan top is modeled (p.107 note 8: pedestal elevations to be adjusted to the fabricated bearing).",
            };
        }
        var t = sp.Substructure.CapBlocks;
        R($"{pier}: arch pedestals differ by {Fe(Math.Abs(t[0].TopElevation - t[1].TopElevation))} ft (B1) although both tie ends have WP {wp:0.00} (p.102 vs p.119)");
    }
    Chain(p10, 535.74, "Pier 10");
    Chain(p11, 532.49, "Pier 11");
    U("p.107, 119, 124", "arch bearing elevations (U1 / B1-B3)", "the p.102 pedestal tops do not reconcile with the p.119 tie WP minus the p.107 bearing stack (0.13-0.20 ft) and differ between the two pedestals of one pier (0.05 ft)",
      "The p.102 pedestal tops are modeled; differences reported per pedestal.");
}

// ================================================================ 6. regenerate, report, save
var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
foreach (var w in rep.Warnings.Take(80)) Print("warning " + w);
foreach (var (uname, _, _) in units)
{
    var m = Doc.Model(uname);
    Print($"{uname}: {m.Prestressed?.Beams.Count ?? 0} beams, substructure units {m.Substructure?.Units.Count ?? 0}");
}
foreach (var (uname, pier) in new[] { ("NHT Unit 3 (spans 7-9)", "Pier 10"), ("NHT Unit 4 (spans 11-12)", "Pier 11") })
{
    var unit = Doc.Model(uname).Substructure?.Units.FirstOrDefault(x => x.Name == pier);
    if (unit == null) { Print($"ERROR {pier}: substructure unit not generated"); continue; }
    foreach (var fs in unit.FixedSeats)
    {
        var w = unit.World(fs.S, fs.N, fs.Z);
        Print($"{pier} {fs.Name}: S {Fe(fs.S)} N {Fe(fs.N)} ft (pier frame), X {Fe(w.X)} Y {Fe(w.Y)} Z {Fe(w.Z)}");
    }
}
foreach (var s in reported) Print("REPORTED " + s);
foreach (var u in unresolved) Print("UNRESOLVED " + u);
foreach (var g in gaps) Print("GAP " + g);
Doc.Save(outPath);
Print($"Saved {outPath}");
