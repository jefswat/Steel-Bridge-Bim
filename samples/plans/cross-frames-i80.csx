// Cross frames of WB I-80 as designed: frame types from the cross frame details (design sheets 118-120) and frame lines
// from the structural steel layouts (Unit 1: sheets 101-104). Re-runnable; run after girders-i80.csx / pier-6a.csx.
//  * Frames are on lines radial to the CL WB approach roadway; "cross frame spacing along girder A" = cumulative distance
//    along girder A from its first CL bearing. Through the skewed piers the frames are staggered: each pier-zone line has
//    frames only in some bays (listed per line).
//  * Type marks: Unit 1 default Type 1; (2) heavier frames next to the piers (drawn bold), (7) plate diaphragms, (9) strut.
//  * All bolts 1" in 1 1/16" holes, two columns 3 1/2" apart on the 7" connection plates, 1 3/4" edges (sheet 118 notes).
// Assumed (not dimensioned on the plans): tie plate length 10"; top gusset bolt rows (2); abutment frame bottom bolt rows (2).
using SBM.Core.Bridge;

double I(double inch) => In(inch);
CrossFrameStyle Frame(string name, string top, string diag, string bot, double gusset, int rows, double topWp, double botWp,
    int topRows = 2, bool tiePlates = false) => new()
{
    Name = name, Kind = CrossFrameStyleKind.InvertedK, TopChord = top, Diagonal = diag, BottomChord = bot, GussetThickness = I(gusset),
    BoltDiameter = I(1), BoltColumns = 2, BoltGage = I(3.5), BoltEdgeDistance = I(1.75), BoltPitch = I(3.5),
    BoltsPerConnection = rows, TopBoltRows = topRows, TopWorkPointOffset = I(topWp), BottomWorkPointOffset = I(botWp),
    TiePlateThickness = I(0.5), TiePlateLength = I(10), TiePlateOffsets = tiePlates ? new() { -I(16), I(16) } : new(),
    // Taller bottom groups keep the lowest bolt 7" below the work point (as the 5-row Type 1 detail) and grow upward.
    BottomBoltOffset = I(Math.Max(0, (rows - 1) * 3.5 / 2 - 7)),
};
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
    Frame("10", "W12x40", "L5x5x5/8", "2-MC6x15.3", 0.5, 3, StrutWp(3), 6, topRows: 3),
    Frame("ABUT", "W24x68", "L5x5x5/8", "L8x8x1/2", 0.5, 2, StrutWp(6), 8, topRows: 6),
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
// Skewed Piers 8WB-10WB: the frames on K's string and on K's bearing lines are modeled with the bays the lines cross;
// the plans' additional partial frames placed from other girders' bearings (dims marked "measured along girder") are
// NOT yet transcribed (open item).
var u2 = new List<CrossFrameLine>();
lines = u2;
x = 0;
L(x += 23.5);
Run(5, 24);
Run(3, 21 + 8 / 12.0); lines.RemoveAt(lines.Count - 1);          // Pier 6WB: support frames
Run(12, 21 + 8 / 12.0); lines.RemoveAt(lines.Count - 1);         // Pier 7WB: support frames
Run(8, 21 + 5 / 12.0, k => k switch
{
    7 => (null, new[] { (0, "4"), (1, "4"), (2, "4"), (5, "4"), (6, "4") }),
    8 => (null, new[] { (5, "4"), (6, "4"), (7, "4") }),
    _ => (null, Array.Empty<(int, string)>()),
});
Run(1, 21 + 6.8125 / 12, _ => (null, new[] { (6, "9") }));
Run(1, 19 + 10.8125 / 12);
Run(1, 21 + 10 / 12.0);
Run(1, 21 + 2.8125 / 12);
L(714.5, new[] { 2, 5 }, (2, "4"), (5, "4"));                    // partial line between K's string dims (approx.)
Run(1, 10 + 4.8125 / 12, _ => (null, new[] { (2, "4"), (5, "4"), (6, "4"), (7, "4"), (8, "9") }));   // K's Pier 8WB bearing
Run(5, 23 + 8 / 12.0, k => k == 1 ? (null, new[] { (6, "4"), (7, "4"), (8, "4") }) : (null, Array.Empty<(int, string)>()));
Run(1, 23 + 11.5625 / 12);
Run(1, 20 + 5.5625 / 12);
Run(1, 9 + 4.625 / 12);
Run(1, 18 + 3.0625 / 12);
Run(1, 9 + 2.3125 / 12, _ => (null, new[] { (9, "9") }));        // K's Pier 9WB bearing
Run(4, 23 + 10 / 12.0);
Run(1, 23 + 10.4375 / 12);
Run(1, 15 + 11.5625 / 12);
Run(1, 7 + 6.75 / 12);
Run(1, 23 + 2.8125 / 12, _ => (null, new[] { (9, "9") }));       // K's Pier 10WB bearing
Run(4, 20.5);
Run(1, 20 + 5.375 / 12);
Run(1, 18 + 6.4375 / 12);
Run(1, 17 + 8.875 / 12); lines.RemoveAt(lines.Count - 1);        // K's Pier 11WB bearing: end frames (Type 10)
foreach (var l2 in u2) l2.Girder = 10;
Print($"Unit 2: {u2.Count} frame lines along girder K, K to Pier 11WB bearing {x:F3} ft");

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

// ---------------- Unit 3 (girders A-J = 0-9; B and C end at cross girders), sheets 112, 113A (113 is VOID) and 114:
// default Type 5, frames radial to the CL WB except along the piers (support frames on the skewed pier lines) and the
// east abutment. Spacing along girder J (cumulative from J's CL bearing at Pier 11WB); J's span lengths from the girder
// data: 158'-3 11/16, 194'-11 15/16, 195'-0 3/16, 195'-0 11/16, 160'-0 1/16 (each bearing falls on a string dimension).
// Type 6 in the north bay (A-D) from mid Span 14WB to Span 16WB; one line of Type 8 plate diaphragms in Span 16WB.
// Near the skewed piers the plans add partial lines measured along girder A (upper bays); the three at Pier 11WB are
// included, the others (12WB: 21'-11 3/16; 13WB: 16'-1 15/16, 12'-10 7/8, 7'-4 1/8) are NOT yet (open item).
var u3 = new List<CrossFrameLine>();
lines = u3;
x = 0;
Run(1, 23 + 2.375 / 12);
Run(3, 23);
Run(1, 20 + 11.0625 / 12);
Run(2, 22 + 7.125 / 12); lines.RemoveAt(lines.Count - 1);       // J's Pier 12WB bearing: support frames
Run(1, 23);
Run(6, 23);
Run(1, 17);
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
Print($"Unit 3: {u3.Count} frame lines, J to East Abut bearing {x:F3} ft (spans sum 903.380)");

var unit3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
var cf3 = unit3.CrossFrames;
cf3.Styles = styles.Select(s => s.Clone()).ToList();
cf3.IntermediateStyle = "5";
cf3.Lines = u3;
cf3.SupportStyles = new()
{
    ["Pier 11WB"] = "10", ["Pier 12WB"] = "5", ["Pier 13WB"] = "5", ["Pier 14WB"] = "5", ["Pier 15WB"] = "5", ["East Abut"] = "ABUT",
};

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
