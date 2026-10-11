// Cross frames of WB I-80 as designed: frame types from the cross frame details (design sheets 118-120) and frame lines
// from the structural steel layouts (Unit 1: sheets 101-104). Re-runnable; run after girders-i80.csx / pier-6a.csx.
//  * Frames are on lines radial to the CL WB approach roadway; "cross frame spacing along girder A" = cumulative distance
//    along girder A from its first CL bearing. Through the skewed piers the frames are staggered: each pier-zone line has
//    frames only in some bays (listed per line).
//  * Type marks: Unit 1 default Type 1; (2) heavier frames next to the piers (drawn bold), (7) plate diaphragms, (9) strut.
//  * All bolts 1" in 1 1/16" holes, two columns 3 1/2" apart on the 7" connection plates, 1 3/4" edges (sheet 118 notes).
// Confirmed on the plans (docs/plan-data/cross-frame-gaps.md section 5): top gusset bolts 2 rows x 2 columns (pitch 3 1/2, gage 3 1/2,
// edge 1 3/4); abutment frame bottom gusset 2 x 2 bolts with the rows 6" apart (3" above/below the work point, WP 8" above the bottom flange).
// NOT dimensioned (UNRESOLVED, nothing invented, raised as warnings by the generator through CrossFrameStyle.Unresolved): tie plate
// length (the model's 10" stays), the lengthwise stud pitch of the shear studs on the W top struts (studs are not modeled).
using SBM.Core.Bridge;

double I(double inch) => In(inch);
CrossFrameStyle Frame(string name, string top, string diag, string bot, double gusset, int rows, double topWp, double botWp,
    int topRows = 2, bool tiePlates = false, double bottomPitch = 0, string[]? unresolved = null) => new()
{
    Name = name, Kind = CrossFrameStyleKind.InvertedK, TopChord = top, Diagonal = diag, BottomChord = bot, GussetThickness = I(gusset),
    BoltDiameter = I(1), BoltColumns = 2, BoltGage = I(3.5), BoltEdgeDistance = I(1.75), BoltPitch = I(3.5),
    BoltsPerConnection = rows, TopBoltRows = topRows, TopWorkPointOffset = I(topWp), BottomWorkPointOffset = I(botWp),
    TiePlateThickness = I(0.5), TiePlateLength = I(10), TiePlateOffsets = tiePlates ? new() { -I(16), I(16) } : new(),
    // Taller bottom groups keep the lowest bolt 7" below the work point (as the 5-row Type 1 detail) and grow upward.
    BottomBoltOffset = I(Math.Max(0, (rows - 1) * 3.5 / 2 - 7)),
    BottomBoltPitch = I(bottomPitch),
    Unresolved = (tiePlates ? new[] { "tie plate length is not dimensioned (I-80 Types 1-4, sheet 118): the model keeps 10 in; a length scaled from the elevation (about 4.7 in) is NOT used" } : Array.Empty<string>())
        .Concat(unresolved ?? Array.Empty<string>()).ToList(),
};
const string StudNote = "shear studs on the W top strut (7/8 in dia, 2 across) are not modeled: their lengthwise pitch / number per strut is not dimensioned";
// Type 10 / abutment frames: rolled-beam top strut bolted through its web; its bolt group starts 2'-2 below the top of
// slab (= 8" slab + 3 1/2" haunch above the top of web), so the strut's work point is 26" + half the group - 11 1/2" down.
double StrutWp(int rows) => 26 + (rows - 1) * 3.5 / 2 - 11.5;
var styles = new List<CrossFrameStyle>
{
    Frame("1", "L6x6x9/16", "L6x6x7/8", "2-MC8x20", 0.625, 5, 6, 10, tiePlates: true),
    Frame("2", "L6x6x9/16", "L8x6x5/8", "MC10x33.6", 0.625, 6, 6, 10, tiePlates: true),
    Frame("3", "L6x6x5/8", "L6x6x3/4", "2-MC10x25", 0.625, 6, 6, 10),
    Frame("4", "L6x6x3/8", "L6x6x1/2", "MC10x25", 0.625, 5, 6, 10, tiePlates: true),
    Frame("5", "L6x6x5/8", "L6x6x3/4", "2-MC10x28.5", 0.75, 6, 6, 10),
    Frame("6", "L6x6x3/8", "L6x6x5/8", "2-MC6x15.3", 0.5, 4, 6, 10),
    Frame("10", "W12x40", "L5x5x5/8", "2-MC6x15.3", 0.5, 3, StrutWp(3), 6, topRows: 3, unresolved: new[] { StudNote + " (Section C-C: 7/8 x 5 studs, 1 1/2 in from the edge)" }),
    Frame("ABUT", "W24x68", "L5x5x5/8", "L8x8x1/2", 0.5, 2, StrutWp(6), 8, topRows: 6, bottomPitch: 6, unresolved: new[] { StudNote + " (Section B-B: 7/8 x 0'-5 studs, 2 in from the edge)" }),
    new() { Name = "7", Kind = CrossFrameStyleKind.PlateDiaphragm, WebPlateThickness = I(0.625), FlangePlateWidth = I(16), FlangePlateThickness = I(0.75),
            DiaphragmBolts = 15, DiaphragmBoltPitch = I(6), BoltDiameter = I(1), TopWorkPointOffset = I(6), BottomWorkPointOffset = I(6) },
    new() { Name = "8", Kind = CrossFrameStyleKind.PlateDiaphragm, WebPlateThickness = I(0.5), FlangePlateWidth = I(12), FlangePlateThickness = I(0.75),
            DiaphragmBolts = 11, DiaphragmBoltPitch = 0, DiaphragmBoltEnd = I(3), BoltDiameter = I(1), TopWorkPointOffset = I(6), BottomWorkPointOffset = I(6) },
    new() { Name = "9", Kind = CrossFrameStyleKind.Strut, BottomChord = "2-MC8x20", GussetThickness = I(0.625), BoltDiameter = I(1),
            BoltColumns = 2, BoltGage = I(3.5), BoltEdgeDistance = I(1.75), BoltPitch = I(3.5), BoltsPerConnection = 3, BottomWorkPointOffset = I(10) },
};

// Lines: distance along girder A (ft), bays (null = all crossed), per-bay types.
var lines = new List<CrossFrameLine>();
void L(double ft, int[]? bays = null, params (int bay, string type)[] types)
{
    var l = new CrossFrameLine { Girder = 0, Distance = Ft(ft), Bays = bays?.ToList() };
    foreach (var (b, t) in types) l.BayStyles[b] = t;
    lines.Add(l);
}
double x = 0;
void Run(int n, double spacingFt, Func<int, (int[]? bays, (int, string)[] types)>? f = null)
{
    for (int k = 1; k <= n; k++) { x += spacingFt; var (b, t) = f?.Invoke(k) ?? (null, Array.Empty<(int, string)>()); L(x, b, t); }
}
// Skewed pier zone: 5 equal spaces starting at girder A's pier bearing (P0) - staggered bays as on sheets 101-103.
void PierZone(double totalFt, string p1Type = "")
{
    double sp = totalFt / 5;
    L(x, new[] { 0, 2, 3, 4 });
    x += sp; L(x, new[] { 1 }, p1Type != "" ? new[] { (1, p1Type) } : Array.Empty<(int, string)>());
    x += sp; L(x, new[] { 0, 2, 4 });
    x += sp; L(x, new[] { 0, 1, 3 });
    x += sp; L(x, new[] { 4 });
    x += sp; L(x, null, (3, "9"));
}
(int, string)[] T2(params int[] bays) => bays.Select(b => (b, "2")).ToArray();

// ---------------- Unit 1 (girders A-F; bays 0 = A-B ... 4 = E-F)
var u1 = new List<CrossFrameLine>();
lines = u1;
x = 0;
Run(1, 22 + 0.625 / 12);
Run(1, 22 + 0.625 / 12);
Run(1, 23 + 8.75 / 12, _ => (null, new[] { (0, "7"), (2, "2"), (3, "2"), (4, "2") }));
Run(6, 23, k => k switch
{
    1 => (null, new[] { (1, "7"), (2, "7") }),
    2 => (null, new[] { (3, "7"), (4, "7") }),
    5 => (null, T2(0, 1, 2)),
    _ => (null, Array.Empty<(int, string)>()),
});
lines.RemoveAt(lines.Count - 1); x -= 23;        // the 6th space ends at A's Pier 1WB bearing = P0 of the pier zone
x += 23; PierZone(58 + 1.0625 / 12);
Run(3, 20 + 2 / 12.0, k => k == 1 ? (null, T2(2, 3, 4)) : (null, Array.Empty<(int, string)>()));
Run(4, 25 + 0.25 / 12);
Run(3, 20 + 2 / 12.0, k => k == 2 ? (null, T2(0, 1, 2)) : (null, Array.Empty<(int, string)>()));
lines.RemoveAt(lines.Count - 1);                  // 3rd space ends at A's Pier 2WB bearing
PierZone(55 + 1.5 / 12);
Run(3, 22 + 5.75 / 12, k => k == 1 ? (null, T2(2, 3, 4)) : (null, Array.Empty<(int, string)>()));
Run(3, 25);
Run(3, 22 + 5.75 / 12, k => k == 2 ? (null, T2(0, 1, 2, 3)) : (null, Array.Empty<(int, string)>()));
lines.RemoveAt(lines.Count - 1);                  // A's Pier 3WB bearing
PierZone(55 + 1.5 / 12, "2");
Run(3, 22, k => k == 1 ? (null, T2(1, 2, 3, 4)) : (null, Array.Empty<(int, string)>()));
Run(3, (74 + 7.75 / 12) / 3);
Run(3, 22);
lines.RemoveAt(lines.Count - 1);                  // Pier 4WB (radial): support frames, not a line
Run(3, 20 + 1 / 12.0);
Run(7, 25);
lines.RemoveAt(lines.Count - 1);                  // ends at the CL bearing at Pier 5WB: end frames (Type 10)
Print($"Unit 1: {u1.Count} frame lines, last at {u1[^1].Distance / Ft(1):F3} ft along girder A (A to Pier 5WB bearing {x / 1:F3} ft)");

var unit1 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 1");
var cf1 = unit1.CrossFrames;
cf1.Styles = styles.Select(s => s.Clone()).ToList();
cf1.IntermediateStyle = "1";
cf1.Lines = u1;
cf1.SupportStyles = new()
{
    [unit1.Supports[0].Name] = "ABUT", [unit1.Supports[1].Name] = "none", [unit1.Supports[2].Name] = "none",
    [unit1.Supports[3].Name] = "none", [unit1.Supports[4].Name] = "1", [unit1.Supports[5].Name] = "10",
};
foreach (var s in unit1.Supports) Print($"  {s.Name}: support frames {cf1.SupportStyles[s.Name]}");

// ---------------- Unit 2 (girders A-K = 0-10; D and E end at cross girders, so bays near them bridge to F), sheets
// 106, 108 (107 is VOID, replaced by revised 108) and 109: default Type 3, frames perpendicular to the CL WB; spacing
// along girder K (cumulative from K's CL bearing at Pier 5WB). K's bearings: the girder data tables give K's span
// lengths 208'-6, 260'-0, 266'-7 1/4, 199'-7 1/2 (sum of the string), 165'-11 9/16, 138'-8 11/16.
// Skewed Piers 8WB-10WB (docs/plan-data/cross-frame-gaps.md section 1): the plans put a frame LINE (radial to the CL WB approach
// roadway) through EACH girder's CL bearing, with frames only in the bays drawn. The ring dimensions "n'-n" are measured along the
// PREVIOUS girder from its bearing to the radial through the next girder's bearing (verified to 0.003 ft). So these lines are
// entered per girder (Girder = X, Distance = X's cumulative bearing distance, explicit Bays + BayStyles); the K-string dims that
// land on those radials are only advanced (Skip), K's own bearing lines stay on the K string.
// Settled from the PDF 110 vectors (docs/plan-data/cross-frame-gaps.md "Visual checks"): 8WB A radial bay B-C is ABSENT (no frame linework
// under the "9'-0 13/16" dimension, the connection ticks at B and C are one-sided: A-B ends at B, C-F starts at C); 8WB G radial bay G-H is
// PRESENT (two frame pieces either side of the "42 41'27"" text and a one-sided tick at H); 8WB I radial: Type 4 only in C-F and F-G (double
// lines + "4" marks), A-B and B-C single lines = Type 3; 8WB K radial "bay B-F" is the cross girder XG-C (drawn in the cross girder callout;
// the line is 0.045 ft from its CL), not a cross frame.
var u2 = new List<CrossFrameLine>();
lines = u2;
x = 0;
void Skip(double ft) => x += ft;                                  // a K-string space that ends on a per-girder pier-zone line
L(x += 23.5);
Run(5, 24);
Run(3, 21 + 8 / 12.0); lines.RemoveAt(lines.Count - 1);          // Pier 6WB: support frames
Run(12, 21 + 8 / 12.0); lines.RemoveAt(lines.Count - 1);         // Pier 7WB: support frames
Run(8, 21 + 5 / 12.0, k => k switch
{
    7 => (null, new[] { (0, "4"), (1, "4"), (2, "4"), (5, "4"), (6, "4") }),
    8 => (new[] { 0, 1, 5, 6, 7, 8, 9 }, new[] { (5, "4"), (6, "4"), (7, "4") }),   // C-F (2) absent on the plans
    _ => (null, Array.Empty<(int, string)>()),
});
Skip(21 + 6.8125 / 12);                                           // A's 8WB radial  (per-girder line below)
Skip(19 + 10.8125 / 12);                                          // F
Skip(21 + 10 / 12.0);                                             // H
Skip(21 + 2.8125 / 12);                                           // J
double kBrg8 = x + 10 + 4.8125 / 12;
Run(1, 10 + 4.8125 / 12, _ => (new[] { 0, 5, 6, 7, 8, 9 }, new[] { (5, "4"), (6, "4"), (7, "4"), (8, "9") }));   // K's Pier 8WB bearing (B-F = cross girder XG-C)
Run(5, 23 + 8 / 12.0, k => k == 1 ? (null, new[] { (6, "4"), (7, "4"), (8, "4"), (9, "4") }) : (null, Array.Empty<(int, string)>()));   // K + 23'-8: G-H..J-K Type 4
Skip(23 + 11.5625 / 12);                                          // A's 9WB radial
Skip(20 + 5.5625 / 12);                                           // G
Skip(9 + 4.625 / 12);                                             // H
Skip(18 + 3.0625 / 12);                                           // J
double kBrg9 = x + 9 + 2.3125 / 12;
Run(1, 9 + 2.3125 / 12, _ => (new[] { 5, 6, 7, 8, 9 }, new[] { (8, "9") }));        // K's Pier 9WB bearing: strut in I-J
Run(4, 23 + 10 / 12.0);
Skip(23 + 10.4375 / 12);                                          // A's 10WB radial
Skip(15 + 11.5625 / 12);                                          // G
Skip(7 + 6.75 / 12);                                              // H
double kBrg10 = x + 23 + 2.8125 / 12;
Run(1, 23 + 2.8125 / 12, _ => (new[] { 0, 5, 6, 7, 8, 9 }, new[] { (8, "9") }));    // K's Pier 10WB bearing: strut in I-J
Run(4, 20.5);
Run(1, 20 + 5.375 / 12);
Run(1, 18 + 6.4375 / 12);
Run(1, 17 + 8.875 / 12); lines.RemoveAt(lines.Count - 1);        // K's Pier 11WB bearing: end frames (Type 10)
foreach (var l2 in u2) l2.Girder = 10;
Print($"K string at its bearing lines: 8WB {kBrg8:F3} (table 734.769), 9WB {kBrg9:F3} (934.364), 10WB {kBrg10:F3} (1100.329) ft");
// Per-girder lines through the bearings (distances = cumulative bearing distances along the girder, report section 1).
void P(int g, double d, int[] bays, params (int bay, string type)[] types)
{
    var l = new CrossFrameLine { Girder = g, Distance = Ft(d), Bays = bays.ToList() };
    foreach (var (b, t) in types) l.BayStyles[b] = t;
    u2.Add(l);
}
// Pier 8WB (sheet 107A)
P(0, 659.127, new[] { 0, 2, 5, 6, 7, 8, 9 }, (6, "9"));           // A  (bay B-C unresolved)
P(2, 672.561, new[] { 0, 2 });                                    // C
P(5, 680.918, new[] { 5, 7, 8, 9 });                              // F
P(6, 691.776, new[] { 0, 1, 2, 6 });                              // G
P(7, 702.589, new[] { 7, 9 });                                    // H
P(8, 713.358, new[] { 0, 1, 2, 5, 6, 8 }, (2, "4"), (5, "4"));    // I
P(9, 724.084, new[] { 9 });                                       // J
// Pier 9WB (sheet 108)
P(0, 868.425, new[] { 0, 5, 6, 7, 8, 9 });                        // A
P(5, 882.668, new[] { 1, 5 });                                    // F
P(6, 893.065, new[] { 0, 1, 6, 8, 9 });                           // G
P(7, 903.432, new[] { 7, 9 });                                    // H
P(8, 913.770, new[] { 0, 1, 5, 6, 8 });                           // I
P(9, 924.081, new[] { 9 });                                       // J
// Pier 10WB (sheet 108)
P(0, 1039.826, new[] { 0, 6, 7, 8, 9 });                          // A
P(5, 1052.074, new[] { 5 });                                      // F
P(6, 1061.759, new[] { 0, 6, 8, 9 });                             // G
P(7, 1071.426, new[] { 7 });                                      // H
P(8, 1081.077, new[] { 0, 5, 6, 8 });                             // I
P(9, 1090.711, new[] { 9 });                                      // J
Print($"Unit 2: {u2.Count} frame lines (K string + per-girder pier-zone lines), K to Pier 11WB bearing {x:F3} ft");

var unit2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var cf2 = unit2.CrossFrames;
cf2.Styles = styles.Select(s => s.Clone()).ToList();
cf2.IntermediateStyle = "3";
cf2.Lines = u2;
cf2.SupportStyles = new()
{
    ["Pier 5WB"] = "10", ["Pier 6A"] = "10", ["Pier 6WB"] = "3", ["Pier 7WB"] = "3",
    ["Pier 8WB"] = "none", ["Pier 9WB"] = "none", ["Pier 10WB"] = "none", ["Pier 11WB"] = "10",
};
// Bay E-F between E at Pier 6A and F at Pier 5WB: Type 10 end frame on the straight line between the two CL bearings (sheet 117, Section A-A).
cf2.Links = new() { new CrossFrameLink { LeftGirder = 4, LeftSupport = "Pier 6A", RightGirder = 5, RightSupport = "Pier 5WB", Style = "10" } };

// ---------------- Unit 3 (girders A-J = 0-9; B and C end at cross girders), sheets 112, 113A (113 is VOID) and 114:
// default Type 5, frames radial to the CL WB except along the piers (support frames on the skewed pier lines) and the
// east abutment. Spacing along girder J (cumulative from J's CL bearing at Pier 11WB); J's span lengths from the girder
// data: 158'-3 11/16, 194'-11 15/16, 195'-0 3/16, 195'-0 11/16, 160'-0 1/16 (each bearing falls on a string dimension).
// Type 6 in the north bay (A-D) from mid Span 14WB to Span 16WB; one line of Type 8 plate diaphragms in Span 16WB.
// Near the skewed piers the plans add partial lines (upper bays) measured along girder A or from a girder's bearing; the three
// at Pier 11WB are along A. Piers 12WB / 13WB (PDF 115 / 117, docs/plan-data/cross-frame-gaps.md section 3): the J-string lines next
// to the piers have only the bays drawn, plus per-girder lines (P3 below) positioned by the plan dimensions.
var u3 = new List<CrossFrameLine>();
lines = u3;
x = 0;
Run(1, 23 + 2.375 / 12);
Run(3, 23);
Run(1, 20 + 11.0625 / 12, _ => (new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, Array.Empty<(int, string)>()));   // J - 45'-2 1/4: no A-B (it would lie on the pier line)
// "2 SPA @ 22'-7 1/8" to J's 12WB bearing: the plan draws NO line at J - 22'-7 1/8; the line drawn there is the straight radial
// through F's bearing (21'-11 3/16 along A, 5'-0 along E; P3 below), which crosses J at J - 20.32: the J dim is advanced only.
Skip(22 + 7.125 / 12);
Run(1, 22 + 7.125 / 12); lines.RemoveAt(lines.Count - 1);       // J's Pier 12WB bearing: support frames
Run(1, 23);
Run(6, 23);
Run(1, 17, _ => (new[] { 7, 8 }, Array.Empty<(int, string)>()));   // J - 16'-11 15/16: only H-I, I-J drawn (A-E: the G radial below)
Run(1, 16 + 11.9375 / 12); lines.RemoveAt(lines.Count - 1);     // J's Pier 13WB bearing
Run(1, 9 + 4 / 12.0);
Run(1, 19 + 5.1875 / 12);
Run(7, 23.75, k => k >= 5 ? (null, new[] { (0, "6") }) : (null, Array.Empty<(int, string)>()));
lines.RemoveAt(lines.Count - 1);                                  // J's Pier 14WB bearing
Run(1, 5 + 0.6875 / 12, _ => (null, new[] { (0, "6") }));
Run(1, 22, _ => (null, new[] { (0, "6") }));
Run(7, 24, _ => (null, new[] { (0, "6") })); lines.RemoveAt(lines.Count - 1);   // J's Pier 15WB bearing
Run(1, 11 + 3.0625 / 12, _ => (null, new[] { (0, "6") }));
Run(1, 23.75, _ => (null, new[] { (0, "6") }));
Run(5, 25, k => k switch
{
    1 => (null, new[] { (0, "6") }),
    2 => (null, Enumerable.Range(0, 10).Select(b => (b, "8")).ToArray()),
    _ => (null, Array.Empty<(int, string)>()),
});
lines.RemoveAt(lines.Count - 1);                                  // J's East Abutment bearing: abutment frames
foreach (var l3 in u3) l3.Girder = 9;
// Partial lines along girder A at the skewed Pier 11WB end (upper bays; the lower bays are still before the pier there).
double xa = 0;
foreach (double sp in new[] { 20 + 4.25 / 12, 17 + 8.4375 / 12, 17 + 1.625 / 12 })
    u3.Add(new CrossFrameLine { Girder = 0, Distance = Ft(xa += sp) });
// Per-girder lines at Piers 12WB / 13WB (radial; distance = the girder's bearing distance from its first bearing + the plan dim).
var f3 = Doc.Model("I-80 WB Unit 3").Framing;
double Brg3(int g, int j) => (f3.Girders[g].DistanceAt(j, 0) - f3.Girders[g].DistanceAt(0, 0)) / Ft(1);
void P3(int g, double ft, int[] bays, params string[] notes)
{
    var l = new CrossFrameLine { Girder = g, Distance = Ft(ft), Bays = bays.ToList() };
    l.Unresolved.AddRange(notes);
    u3.Add(l);
}
// Pier 12WB (PDF 115): "21'-11 3/16" along A from A's bearing; the same radial crosses E at +5.007 = the "5'-0" along E (from E's bearing to
// the line drawn at F's bearing): frames A-B..D-E and G-H..I-J (one straight line on the plan; no frame touches F on it).
P3(0, Brg3(0, 1) + 21 + 11.1875 / 12, new[] { 0, 1, 2, 3, 6, 7, 8 },
   "Pier 12WB, PDF 115: the J string '2 SPA @ 22'-7 1/8 = 45'-2 1/4' to J's bearing puts a line at J - 22'-7 1/8, but the line drawn there is this straight " +
   "radial (21'-11 3/16 along A, 5'-0 along E), which crosses J at J - 20.32 ft; its G-H..I-J frames are modeled on the drawn line (the I-J frame is partly " +
   "under the 6'-11 3/8 dimension). The line is drawn through F's bearing; in the model it passes 0.067 ft from it (F's bearing radial crosses A at +21.999)");
// "6'-11 3/8" along I from I's bearing: frames A-B..H-I (I-J absent); the line is the radial through J's ahead jacking stiffener (J + 2'-0).
P3(8, Brg3(8, 1) + 6 + 11.375 / 12, new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
   "Pier 12WB, PDF 115: placed by the dimension 6'-11 3/8 along I from I's CL bearing; the frame linework is drawn about 2 ft further ahead (scaled: crosses A " +
   "about 45.5 ft past A's bearing vs 43.44 ft from the dimension)");
// Pier 13WB (PDF 117): A-string chain from A's bearing 16'-1 15/16, 12'-10 7/8, 7'-4 1/8 (the last line is the J-string line J + 9'-4,
// which the model crosses with A at +36.416 = the chain). The 16'-1 15/16 line is drawn through G's bearing (frames A-C, C-D, D-E; it
// crosses E at +7.995 = the "8'-0" along E); the 12'-10 7/8 line is the sign truss support bracket line (PDF 117 / 180) with a frame in A-C only.
double a13 = Brg3(0, 2);
P3(0, a13 + 16 + 1.9375 / 12, new[] { 0, 2, 3 },
   "Pier 13WB, PDF 117: line drawn through G's CL bearing, but in the model the radial at 16'-1 15/16 along A (= 8'-0 along E, both plan dims) passes 0.15 ft " +
   "from G's bearing (G's bearing radial crosses A at +16.01): check G's 13WB bearing / girder geometry (no frame in bays E-G on this line). Also the 13WB " +
   "pier-line spacings A-C 6'-3 7/16, C-D 6'-9 1/16 vs model 6.345 / 6.707 ft (A-D 13.04 = model 13.05): girder C's 13WB bearing is about 0.05 ft off along the pier");
P3(0, a13 + 16 + 1.9375 / 12 + 12 + 10.875 / 12, new[] { 0 });
Print($"Unit 3: {u3.Count} frame lines, J to East Abut bearing {x:F3} ft (spans sum 903.380); bearings A/F/I at 12WB {Brg3(0, 1):F3}/{Brg3(5, 1):F3}/{Brg3(8, 1):F3}, A at 13WB {a13:F3} ft");

var unit3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
var cf3 = unit3.CrossFrames;
cf3.Styles = styles.Select(s => s.Clone()).ToList();
cf3.IntermediateStyle = "5";
cf3.Lines = u3;
cf3.SupportStyles = new()
{
    ["Pier 11WB"] = "10", ["Pier 12WB"] = "5", ["Pier 13WB"] = "5", ["Pier 14WB"] = "5", ["Pier 15WB"] = "5", ["East Abut"] = "ABUT",
};
// Type 6 in the north bay (A-D, left girder A = 0) of the support frames at Piers 14WB and 15WB (sheets 117-118: Type 6 circle pairs).
cf3.SupportBayStyles = new() { ["Pier 14WB"] = new() { [0] = "6" }, ["Pier 15WB"] = new() { [0] = "6" } };

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var ub in new[] { unit1, unit2, unit3 })
{
    var f = Doc.Model(ub.Name).Framing;
    Print($"{ub.Name}: {f.CrossFrames.Count} frames: " + string.Join(", ", f.CrossFrames.GroupBy(c => c.Style == "" ? "(default)" : c.Style).OrderBy(g => g.Key).Select(g => $"{g.Count()} x {g.Key}")));
    foreach (var d in f.Diagnostics.Concat(Doc.Model(ub.Name).Steel!.Diagnostics).Where(d => d.Message.Contains("rame") || d.Message.Contains("CF")))
        Print("  " + d.Message);
}
Doc.Save();
