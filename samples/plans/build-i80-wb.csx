// Iowa DOT Design 116 (B00168): WB I-80 viaduct over NB I-29, Council Bluffs — 3349'-10 x varies continuous welded
// girder bridge in three units. Built from the plan sheets (see notes at the end for what is approximate).
//   sbm new "samples\plans\I-80 WB and Ramp G.sbm"
//   sbm run "samples\plans\build-i80-wb.csx" "samples\plans\I-80 WB and Ramp G.sbm"
//   then build-ramp-g.csx, geolocate-cbis.csx, cross-slope-i80.csx (gore slope break and the 410' south-side
//   superelevation transition from sheet 190; the table below is the old simplification), deck-edges-i80.csx (true north deck edges of Units 2 and 3; the
//   straight-line edge tables below are only placeholders), girders-i80.csx (discontinuous girders at the piers from
//   the cap plans), pier-6a.csx (girders A-E begin on Pier 6A), piers-i80.csx and piers-ramp-g.csx (as-designed piers, footings and piles).
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var u = Doc.Units;
double ft = Ft(1);
Func<string, double> S = Sta;               // "7580+86.64" -> m (US units)

// ---------------------------------------------------------------- alignment: CL WB approach roadway
// I-80 curve data (design sheet 12): PC 7597+73.11, PI 7616+08.25, D = 74°48'21.33" LT, R = 2400.00', PT 7629+06.57.
// The WB approach roadway CL is 41.08' LT (north) of CL I-80, so its curve is concentric (R = 2358.92').
// Local coordinates (the plans give no state-plane values): back tangent due east; geolocated separately.
double R80 = Ft(2400), off = Ft(41.08), Rwb = R80 - off;
double delta = (74 + 48 / 60.0 + 21.33 / 3600) * Math.PI / 180;
double pc80 = S("7597+73.11");
var pcWb = new Vec2(Ft(10000), Ft(10000) + off);           // WB PC (I-80 PC at (10000, 10000) ft)
double T = Rwb * Math.Tan(delta / 2);
var piWb = pcWb + new Vec2(T, 0);
var ahead = Vec2.FromAngle(delta);                          // back tangent east, turning left by delta
var pis = new[]
{
    new PiRow(pcWb.Y, pcWb.X - Ft(2500)),
    new PiRow(piWb.Y, piWb.X, Rwb),
    new PiRow((piWb + ahead * (T + Ft(1500))).Y, (piWb + ahead * (T + Ft(1500))).X),
};
// I-80 station of a point on the WB CL -> WB station (stations match on the tangent; the WB curve is shorter by Rwb/R80).
double Wb(string i80) { double s = S(i80); return s <= pc80 ? s : pc80 + (s - pc80) * Rwb / R80; }
double startSta = pc80 - Ft(2500);
Doc.Project.Alignments.RemoveAll(a => a.Name == "I-80 WB");
var al = Doc.AddAlignment("I-80 WB", startSta, pis);
al.Description = "CL westbound approach roadway, 41.08' LT of CL I-80 (Iowa DOT Design 116)";

// Cross slope about the WB CL (sheet 196): crown 2% on the tangent, transition to one-way 3% at 7596+55.86 and full
// superelevation 5.9% (down to the north, inside of curve) from 7599+53.11.
al.CrossSlope = new CrossSlopeTable
{
    PivotOffset = 0,
    Rows =
    {
        new CrossSlopeRow { Station = Wb("7595+58.11"), LeftSlope = -0.02, RightSlope = -0.02 },
        new CrossSlopeRow { Station = Wb("7596+55.86"), LeftSlope = -0.03, RightSlope = 0.03 },
        new CrossSlopeRow { Station = Wb("7599+53.11"), LeftSlope = -0.059, RightSlope = 0.059 },
    },
};
// Profile: I-80 grade is at the P.G.L. 34'-0 right of the WB CL; elevation at the WB CL = PGL - right slope x 34'.
var pgl = new VerticalProfile
{
    Vpis =
    {
        new Vpi { Station = S("7570+00"), Elevation = Ft(996.08) },
        new Vpi { Station = S("7596+25"), Elevation = Ft(1029.61), LengthIn = Ft(375), LengthOut = Ft(375) },
        new Vpi { Station = S("7621+00"), Elevation = Ft(1016.10) },
    },
};
var vpis = new List<Vpi>();
for (double s80 = S("7572+00"); s80 <= S("7619+00"); s80 += Ft(25))
{
    double wb = s80 <= pc80 ? s80 : pc80 + (s80 - pc80) * Rwb / R80;
    var (_, rs) = al.CrossSlope.SlopesAt(wb);
    vpis.Add(new Vpi { Station = wb, Elevation = pgl.ElevationAt(s80) - rs * Ft(34) });
}
al.Profile = new VerticalProfile { Vpis = vpis };

// ---------------------------------------------------------------- helpers
const string Structure = "I-80 WB Viaduct (Design 116)";
SupportDefinition Sup(string name, SupportType type, double sta, double skewDeg) => new()
{
    Name = name, Type = type, Station = sta, SkewDegrees = skewDeg, GirderEndExtension = In(12), DeckEndOffset = Ft(1.5),
    Fixity = type == SupportType.Abutment ? BearingFixity.Expansion : BearingFixity.Auto,
};
double Dms(int d, int m, double s) => d + m / 60.0 + s / 3600;
SubstructureDefinition Pier(int cols, double spacingFt, double colW, double colD, double ftgL, double ftgW, int pr, int pc, double footBottom)
{
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.CapWidth = Ft(6); sd.CapDepth = Ft(7); sd.CapEndOverhang = Ft(4);
    sd.BottomShape = CapBottomShape.Level;
    sd.TopShape = CapTopShape.FollowSeats;
    sd.ColumnCount = cols;
    sd.ColumnSpacings = Enumerable.Repeat(Ft(spacingFt), cols - 1).ToList();
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Ft(colW); sd.ColumnDepth = Ft(colD);
    sd.Foundation = FoundationType.PileCap;
    sd.FootingLength = Ft(ftgL); sd.FootingWidth = Ft(ftgW); sd.FootingThickness = Ft(5.5);
    sd.FoundationTopElevation = Ft(footBottom + 5.5);
    sd.PileType = PileType.SteelHP; sd.PileSection = "HP14x73"; sd.PileRows = pr; sd.PileColumns = pc;
    sd.PileSpacing = Math.Min(Ft(ftgL - 2), Ft(ftgW - 2)) / Math.Max(1, Math.Max(pr, pc) - 1);
    sd.PileLength = Ft(100);
    sd.MinPedestalHeight = In(3); sd.MaxPedestalHeight = Ft(3);
    return sd;
}
SubstructureDefinition Abut(double footBottom)
{
    var sd = SubstructureDefinition.Default(SupportType.Abutment);
    sd.CapWidth = Ft(4); sd.CapDepth = Ft(4.5);
    sd.Foundation = FoundationType.PileCap;
    sd.PileSection = "HP14x73"; sd.PileLength = Ft(125);
    sd.FoundationTopElevation = Ft(footBottom);
    return sd;
}
// Plate schedule: alternating positive / negative regions. Each negative region = (pier station, start, end of the
// heavy-plate zone from the pier, distances to the region ends), TF/BF sizes in inches.
List<PlateSegment> Plates((double sta, double back, double ahead, double peakBack, double peakAhead, double tfW, double tfT, double tfPkT, double bfW, double bfT, double bfPkT)[] piers,
    double posTfW, double posTfT, double posBfW, double posBfT)
{
    var list = new List<PlateSegment>();
    foreach (var plate in new[] { GirderPlate.TopFlange, GirderPlate.BottomFlange })
    {
        bool top = plate == GirderPlate.TopFlange;
        foreach (var p in piers)
        {
            list.Add(new PlateSegment { Plate = plate, EndStation = p.sta - p.back, Width = In(top ? posTfW : posBfW), Thickness = In(top ? posTfT : posBfT) });
            list.Add(new PlateSegment { Plate = plate, EndStation = p.sta - p.peakBack, Width = In(top ? p.tfW : p.bfW), Thickness = In(top ? p.tfT : p.bfT) });
            list.Add(new PlateSegment { Plate = plate, EndStation = p.sta + p.peakAhead, Width = In(top ? p.tfW : p.bfW), Thickness = In(top ? p.tfPkT : p.bfPkT) });
            list.Add(new PlateSegment { Plate = plate, EndStation = p.sta + p.ahead, Width = In(top ? p.tfW : p.bfW), Thickness = In(top ? p.tfT : p.bfT) });
        }
        list.Add(new PlateSegment { Plate = plate, Width = In(top ? posTfW : posBfW), Thickness = In(top ? posTfT : posBfT) });
    }
    return list;
}
BridgeDefinition Unit(string name)
{
    var b = new BridgeDefinition { Name = name, AlignmentName = al.Name, Structure = Structure };
    b.FieldSplices.Clear();
    b.Bearings.Type = BearingType.Disc;
    b.Deck.Thickness = In(8); b.Deck.Haunch = In(3.5);           // 11 1/2" top of slab to top of web
    b.Deck.Barrier.Type = BarrierType.FShape;                     // Iowa 44" rail approximated
    b.Deck.Barrier.Height = In(44);
    b.Girders.PlanLayout = GirderPlanLayout.CurvedParallel;
    b.CrossFrames.MaxSpacing = Ft(25);
    return b;
}
Doc.Project.Bridges.RemoveAll(b => b.Structure == Structure);

// ---------------------------------------------------------------- Unit 1: W. Abut - Pier 5WB, girders A-F at 11'-5
var u1 = Unit("I-80 WB Unit 1");
double p5 = Wb("7591+06.39");
u1.Supports.AddRange(new[]
{
    Sup("West Abut", SupportType.Abutment, Wb("7578+79.64"), 44),
    Sup("Pier 1WB", SupportType.Pier, Wb("7580+86.64"), 45.5),
    Sup("Pier 2WB", SupportType.Pier, Wb("7583+64.64"), 44),
    Sup("Pier 3WB", SupportType.Pier, Wb("7586+29.64"), 44),
    Sup("Pier 4WB", SupportType.Pier, Wb("7588+69.64"), 0),
    Sup("Pier 5WB", SupportType.Pier, p5 - Ft(1.5), 0),
});
var s5 = u1.Supports[^1];
s5.SharedPier = "Pier 5WB"; s5.PierCenterOffset = Ft(1.5); s5.Fixity = BearingFixity.Expansion;
u1.Girders.Count = 6;
u1.Girders.PlanNames = new() { "A", "B", "C", "D", "E", "F" };   // plan girder letters (sheets only)
u1.Girders.Spacings = Enumerable.Repeat(Ft(11 + 5 / 12.0), 5).ToList();
u1.Girders.FirstGirderOffset = In(3.5) - 2 * Ft(11 + 5 / 12.0);   // girder C 3 1/2" right of the WB CL
u1.Deck.LeftEdgeOffset = -Ft(25 + 7 / 12.0); u1.Deck.RightEdgeOffset = Ft(37 + 7 / 12.0);
u1.Girders.Section.WebDepth = In(102); u1.Girders.Section.WebThickness = In(11 / 16.0);
u1.Girders.Section.TopFlange = new PlateSize { Width = In(20), Thickness = In(0.875) };
u1.Girders.Section.BottomFlange = new PlateSize { Width = In(20), Thickness = In(0.875) };
double P(int i) => u1.Supports[i].Station;
u1.Girders.Section.PlateChanges = Plates(new[]
{
    (P(1), Ft(55), Ft(80.5), Ft(22), Ft(22), 32.0, 1.375, 2.25, 34.0, 1.5, 2.25),
    (P(2), Ft(69), Ft(64), Ft(25), Ft(25), 32.0, 1.375, 2.25, 34.0, 1.5, 2.25),
    (P(3), Ft(64), Ft(70), Ft(20), Ft(20), 30.0, 1.25, 2.0, 36.0, 1.5, 2.0),
    (P(4), Ft(70), Ft(64), Ft(20), Ft(19), 34.0, 1.5, 2.0, 34.0, 1.5, 2.0),
}, 20, 0.875, 20, 0.875);
// Last span (Pier 4WB - Pier 5WB beyond FS 9): 1 1/8 x 24 both flanges.
u1.Girders.Section.PlateChanges.RemoveAll(p => double.IsNaN(p.EndStation));
u1.Girders.Section.PlateChanges.Add(new PlateSegment { Plate = GirderPlate.TopFlange, Width = In(24), Thickness = In(1.125) });
u1.Girders.Section.PlateChanges.Add(new PlateSegment { Plate = GirderPlate.BottomFlange, Width = In(24), Thickness = In(1.125) });
foreach (var d in new[] { P(0) + Ft(50), P(1) - Ft(55), P(1) + Ft(80.5), P(2) - Ft(69), P(2) + Ft(64), P(3) - Ft(64), P(3) + Ft(70), P(4) - Ft(70), P(4) + Ft(64), P(5) - Ft(45) })
    u1.FieldSplices.Add(new FieldSpliceDefinition { Station = d });
u1.Supports[0].Substructure = Abut(992.82);
u1.Supports[1].Substructure = Pier(3, 32.5, 8, 4, 18, 18, 5, 5, 968.83);
u1.Supports[2].Substructure = Pier(3, 32.5, 8, 4, 18, 18, 5, 5, 968.38);
u1.Supports[3].Substructure = Pier(3, 31, 8, 4, 22, 22, 5, 5, 968.0);
u1.Supports[4].Substructure = Pier(2, 25, 8, 4, 21.5, 14.5, 4, 5, 968.0);
u1.Supports[5].Substructure = Pier(2, 25, 8, 4, 20, 16, 4, 5, 968.0);
Doc.Project.Bridges.Add(u1);

// ---------------------------------------------------------------- Unit 2: Pier 5WB - Pier 11WB, girders A + F-K
// (girders B-E of the Ramp A2 widening end on cross girders inside the unit and are not modeled; A follows the edge).
var u2 = Unit("I-80 WB Unit 2");
double p11 = Wb("7603+32.45");
u2.Supports.AddRange(new[]
{
    Sup("Pier 5WB", SupportType.Pier, p5 + Ft(1.5), 0),
    Sup("Pier 6WB", SupportType.Pier, Wb("7593+16.39"), 0),
    Sup("Pier 7WB", SupportType.Pier, Wb("7595+76.39"), 0),
    Sup("Pier 8WB", SupportType.Pier, Wb("7598+11.13"), Dms(42, 41, 27)),
    Sup("Pier 9WB", SupportType.Pier, Wb("7600+15.47"), Dms(38, 38, 0)),
    Sup("Pier 10WB", SupportType.Pier, Wb("7601+86.39"), Dms(33, 43, 51)),
    Sup("Pier 11WB", SupportType.Pier, p11 - Ft(1.5), Dms(27, 27, 55)),
});
u2.Supports[0].SharedPier = "Pier 5WB"; u2.Supports[0].PierCenterOffset = -Ft(1.5); u2.Supports[0].Fixity = BearingFixity.Expansion;
u2.Supports[^1].SharedPier = "Pier 11WB & 19G"; u2.Supports[^1].PierCenterOffset = Ft(1.5); u2.Supports[^1].Fixity = BearingFixity.Expansion;
// Girders A-E are discontinuous toward the east: E, D, C, B end on cross girders (design sheet 121, girder data
// sheets 105-110); F-K run the full unit at 11'-6 (H on the WB CL). Where the plans give "varies", the girders that
// still exist at a pier are spread evenly between A and F; ended girders sit between their neighbors.
var fk = new[] { -23.0, -11.5, 0, 11.5, 23, 34.5 };
var aU2 = new[] { -74.5, -66.2, -53.2, -42.8, -39.6, -36.9, -34.5 };
var lastSupportU2 = new[] { 6, 4, 3, 2, 1 };   // A..E: last support index each exists at (A: all)
List<double> SpreadBetween(double a, double f, int[] lastSup, int j)
{
    int n = lastSup.Length;                      // A + discontinuous girders
    var exist = Enumerable.Range(0, n).Where(k => lastSup[k] >= j).ToList();
    var pos = new double[n];
    for (int q = 0; q < exist.Count; q++) pos[exist[q]] = a + (f - a) * q / exist.Count;
    for (int k = 1; k < n; k++)
        if (lastSup[k] < j && false) { }
    // existing first (above), then ended girders left to right
    for (int k = 1; k < n; k++)
        if (lastSup[k] < j)
        {
            int nx = Enumerable.Range(k + 1, n - k - 1).FirstOrDefault(m => lastSup[m] >= j, -1);
            pos[k] = (pos[k - 1] + (nx >= 0 ? pos[nx] : f)) / 2;
        }
    return pos.ToList();
}
u2.Girders.Count = 11;
u2.Girders.PlanNames = new() { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K" };
u2.Girders.OffsetsAtSupports = Enumerable.Range(0, aU2.Length)
    .Select(j => SpreadBetween(aU2[j], fk[0], lastSupportU2, j).Concat(fk).Select(Ft).ToList()).ToList();
u2.Girders.Spacings = Enumerable.Repeat(Ft(11.5), 10).ToList();
u2.Girders.Terminations = new()
{
    new GirderTermination { Girder = 4, Support = 1, Distance = Ft(86 + 8.0625 / 12) },   // E: Pier 6WB + 86'-8 1/16
    new GirderTermination { Girder = 3, Support = 2, Distance = Ft(64 + 3.25 / 12) },     // D: Pier 7WB + 64'-3 1/4
    new GirderTermination { Girder = 2, Support = 3, Distance = Ft(59 + 1.0625 / 12) },   // C: Pier 8WB + 59'-1 1/16
    new GirderTermination { Girder = 1, Support = 4, Distance = Ft(50 + 3.5 / 12) },      // B: Pier 9WB + 50'-3 1/2
};
u2.Girders.CrossGirder = new CrossGirderSection { WebDepth = In(80), WebThickness = In(0.625), FlangeWidth = In(15), FlangeThickness = In(0.625) };
u2.Deck.RightEdgeOffset = Ft(37 + 7 / 12.0);
u2.Deck.LeftEdgeOffset = Ft(-37.6);
u2.Deck.LeftEdgeTable = new()
{
    new DeckEdgePoint { Station = Wb("7591+50.68"), Offset = -Ft(118.65 - 41.08) },
    new DeckEdgePoint { Station = Wb("7597+73.11"), Offset = -Ft(87.53 - 41.08) },
    new DeckEdgePoint { Station = p11, Offset = -Ft(37.6) },
};
u2.Girders.Section.WebDepth = In(102); u2.Girders.Section.WebThickness = In(11 / 16.0);
u2.Girders.Section.TopFlange = new PlateSize { Width = In(20), Thickness = In(0.875) };
u2.Girders.Section.BottomFlange = new PlateSize { Width = In(20), Thickness = In(0.875) };
double Q(int i) => u2.Supports[i].Station;
u2.Girders.Section.PlateChanges = Plates(new[]
{
    (Q(1), Ft(55), Ft(60), Ft(18), Ft(18), 30.0, 1.375, 2.0, 30.0, 1.5, 2.0),
    (Q(2), Ft(56), Ft(50), Ft(18), Ft(18), 32.0, 1.375, 2.0, 32.0, 1.5, 2.0),
    (Q(3), Ft(45), Ft(45), Ft(29), Ft(22), 30.0, 1.375, 1.625, 34.0, 1.5, 1.75),
    (Q(4), Ft(40), Ft(40), Ft(25), Ft(22), 18.0, 0.875, 1.375, 18.0, 0.875, 1.375),
    (Q(5), Ft(40), Ft(35), Ft(27), Ft(18), 18.0, 0.875, 1.375, 18.0, 0.875, 1.375),
}, 18, 0.875, 18, 0.875);
foreach (var (i, back, ahd) in new[] { (1, 55.0, 60.0), (2, 56.0, 50.0), (3, 45.0, 45.0), (4, 40.0, 40.0), (5, 40.0, 35.0) })
{
    u2.FieldSplices.Add(new FieldSpliceDefinition { Station = Q(i) - Ft(back) });
    u2.FieldSplices.Add(new FieldSpliceDefinition { Station = Q(i) + Ft(ahd) });
}
u2.Supports[1].Substructure = Pier(4, 27.33, 8, 4, 20, 18, 4, 4, 968.0);
u2.Supports[2].Substructure = Pier(3, 34.5, 8, 4, 29, 20, 5, 6, 968.0);
u2.Supports[3].Substructure = Pier(4, 25, 8, 4, 21.5, 14.5, 4, 5, 968.0);
u2.Supports[4].Substructure = Pier(3, 36, 8, 4, 26, 17.5, 5, 6, 968.0);
u2.Supports[5].Substructure = Pier(3, 32.5, 8, 4, 26, 17.5, 5, 6, 968.0);
u2.Supports[6].Substructure = Pier(4, 30, 8, 4, 22.5, 18, 5, 5, 968.0);
u2.Supports[0].Substructure = new SubstructureDefinition { Enabled = false }; // built by Unit 1 (shared pier)
Doc.Project.Bridges.Add(u2);

// ---------------------------------------------------------------- Unit 3: Pier 11WB - E. Abut, girders A + D-J, 76" web
var u3 = Unit("I-80 WB Unit 3");
u3.Supports.AddRange(new[]
{
    Sup("Pier 11WB", SupportType.Pier, p11 + Ft(1 + 8 / 12.0), Dms(27, 27, 55)),
    Sup("Pier 12WB", SupportType.Pier, Wb("7604+95.87"), Dms(23, 40, 36)),
    Sup("Pier 13WB", SupportType.Pier, Wb("7606+94.77"), Dms(18, 48, 56)),
    Sup("Pier 14WB", SupportType.Pier, Wb("7608+93.43"), Dms(14, 4, 22)),
    Sup("Pier 15WB", SupportType.Pier, Wb("7610+92.01"), Dms(9, 19, 56)),
    Sup("East Abut", SupportType.Abutment, Wb("7612+54.84"), Dms(5, 26, 43)),
});
u3.Supports[0].SharedPier = "Pier 11WB & 19G"; u3.Supports[0].PierCenterOffset = -Ft(1 + 8 / 12.0); u3.Supports[0].Fixity = BearingFixity.Expansion;
u3.Supports[0].Substructure = new SubstructureDefinition { Enabled = false };   // built by Unit 2
var dj = new[] { -34.5, -23, -11.5, 0, 11.5, 23, 34.5 };
double s11 = u3.Supports[0].Station, sE = u3.Supports[^1].Station;
var lastSupportU3 = new[] { 5, 1, 2 };        // A; B ends past Pier 12WB; C ends past Pier 13WB (sheet 115)
u3.Girders.Count = 10;
u3.Girders.PlanNames = new() { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
u3.Girders.OffsetsAtSupports = u3.Supports.Select((sp, j) =>
{
    double a = -73.1 + (-40.5 + 73.1) * (sp.Station - s11) / (sE - s11);
    return SpreadBetween(a, dj[0], lastSupportU3, j).Concat(dj).Select(Ft).ToList();
}).ToList();
u3.Girders.Spacings = Enumerable.Repeat(Ft(11.5), 9).ToList();
u3.Girders.Terminations = new()
{
    // Girder elevations, design sheet 115: B ends on a cross girder 60'-3 11/16 past Pier 12WB, C 53'-2 11/16 past Pier 13WB
    // (B and C start on Pier 19G; 162'-9 7/8 is B's span 19G-12WB, not its end).
    new GirderTermination { Girder = 1, Support = 1, Distance = Ft(60 + 3.6875 / 12) },   // B: Pier 12WB + 60'-3 11/16
    new GirderTermination { Girder = 2, Support = 2, Distance = Ft(53 + 2.6875 / 12) },   // C: Pier 13WB + 53'-2 11/16
};
u3.Girders.CrossGirder = new CrossGirderSection { WebDepth = In(60), WebThickness = In(0.75), FlangeWidth = In(12), FlangeThickness = In(0.875) };
u3.Deck.RightEdgeOffset = Ft(37 + 7 / 12.0);
u3.Deck.LeftEdgeOffset = -Ft(43.6);
u3.Deck.LeftEdgeTable = new()
{
    new DeckEdgePoint { Station = s11, Offset = -Ft(76.2) },
    new DeckEdgePoint { Station = sE, Offset = -Ft(43.6) },
};
u3.Girders.Section.WebDepth = In(76); u3.Girders.Section.WebThickness = In(0.625);
u3.Girders.Section.TopFlange = new PlateSize { Width = In(18), Thickness = In(0.875) };
u3.Girders.Section.BottomFlange = new PlateSize { Width = In(20), Thickness = In(0.875) };
double V(int i) => u3.Supports[i].Station;
u3.Girders.Section.PlateChanges = Plates(new[]
{
    (V(1), Ft(58), Ft(74), Ft(19), Ft(19), 26.0, 1.125, 2.0, 26.0, 1.25, 2.0),
    (V(2), Ft(56), Ft(60), Ft(26), Ft(26), 26.0, 1.125, 2.0, 26.0, 1.125, 2.0),
    (V(3), Ft(56), Ft(55), Ft(25), Ft(23), 26.0, 1.125, 2.0, 26.0, 1.125, 2.0),
    (V(4), Ft(56), Ft(58), Ft(21), Ft(30), 26.0, 1.125, 2.0, 26.0, 1.25, 2.0),
}, 18, 0.875, 20, 0.875);
foreach (var (i, back, ahd) in new[] { (1, 58.0, 74.0), (2, 56.0, 60.0), (3, 56.0, 55.0), (4, 56.0, 58.0) })
{
    u3.FieldSplices.Add(new FieldSpliceDefinition { Station = V(i) - Ft(back) });
    u3.FieldSplices.Add(new FieldSpliceDefinition { Station = V(i) + Ft(ahd) });
}
u3.CrossFrames.Orientation = CrossFrameOrientation.NormalToGirders;   // "radial to CL WB approach roadway"
u3.Supports[1].Substructure = Pier(4, 29, 8, 4, 20.5, 16, 5, 4, 968.0);
u3.Supports[2].Substructure = Pier(3, 30.5, 8, 4, 30, 24, 6, 5, 968.0);
u3.Supports[3].Substructure = Pier(3, 28, 8, 4, 20.5, 19, 5, 5, 968.0);
u3.Supports[4].Substructure = Pier(3, 27, 8, 4, 20.5, 19, 5, 5, 968.0);
u3.Supports[5].Substructure = Abut(1003.0);
Doc.Project.Bridges.Add(u3);

// ---------------------------------------------------------------- deck drains (Table of Deck Drains, design sheet 220)
// (I-80 station, side, type, outlet, splash basin). Piped drains run to Pier 11WB (span 11WB) or Pier 12WB.
var drains = new (string sta, bool left, string type, bool piped, bool basin)[]
{
    ("7578+70.00", true, "Type A", false, false), ("7578+85.00", true, "Type A", false, false), ("7579+24.00", false, "Type A", false, false),
    ("7579+34.00", false, "Type A", false, false), ("7581+08.00", true, "Type A", false, true), ("7581+38.50", false, "Type A", false, true),
    ("7584+01.00", true, "Type A", false, true), ("7584+27.00", false, "Type A", false, true), ("7587+42.00", true, "Type A", false, true),
    ("7587+42.00", false, "Type A", false, true), ("7588+92.00", true, "Type A", false, true), ("7588+92.00", false, "Type A", false, true),
    ("7590+02.00", false, "Type A", false, true),
    ("7591+26.39", false, "Type A", false, true), ("7591+26.39", true, "Type A", false, true), ("7591+36.39", true, "Type A", false, true),
    ("7592+36.39", true, "Type A", false, true), ("7593+36.39", true, "Type A", false, true), ("7594+16.39", true, "Type A", false, true),
    ("7594+86.39", true, "Type A", false, true), ("7595+56.39", true, "Type A", false, true), ("7596+67.79", true, "Type A", false, true),
    ("7602+44.00", true, "Type B", true, false), ("7602+54.00", true, "Type B", true, false), ("7602+64.00", true, "Type B", true, false),
    ("7602+74.00", true, "Type B", true, false), ("7602+84.00", true, "Type B", true, false), ("7602+94.00", true, "Type B", true, false),
    ("7603+04.00", true, "Type B", true, false),
    ("7603+86.79", true, "Type A", true, false), ("7604+73.67", true, "Type A", true, false), ("7605+26.79", true, "Type A", true, false),
    ("7606+28.43", true, "Type A", false, true), ("7607+17.43", true, "Type B", false, true), ("7607+77.43", true, "Type B", false, true),
    ("7608+47.43", true, "Type B", false, true), ("7609+12.52", true, "Type B", false, true), ("7609+82.52", true, "Type B", false, true),
    ("7610+52.52", true, "Type B", false, true), ("7612+20.69", true, "Type B", false, false), ("7612+30.69", true, "Type B", false, false),
    ("7612+40.69", true, "Type B", false, false),
};
foreach (var (sta, left, type, piped, basin) in drains)
{
    double ws = Wb(sta);
    var unit = new[] { u1, u2, u3 }.FirstOrDefault(b => ws >= b.Supports[0].Station - Ft(30) && ws <= b.Supports[^1].Station + Ft(5)) ?? u3;
    unit.Drains.Add(new DeckDrainDefinition
    {
        Station = ws, Left = left, Type = type, Outlet = piped ? DrainOutlet.PipedToPier : DrainOutlet.FreeFall, SplashBasin = basin,
        PipeDiameter = piped ? In(8) : In(8),
    });
}

var r = Doc.Regenerate();
Print($"Regenerated {r.Bridges} bridges in {r.Elapsed.TotalSeconds:F1} s: {r.Parts:N0} parts, {r.Bolts:N0} bolts, {r.SteelMassKg / 907.18474:N0} tons steel.");
foreach (var e in r.Errors) Print("ERROR " + e);
foreach (var w in r.Warnings.Take(25)) Print("warning " + w);
Doc.Save();
