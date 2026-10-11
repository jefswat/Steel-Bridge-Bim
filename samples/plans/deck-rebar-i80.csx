// Plan-driven deck reinforcing of I-80 WB Units 1-3 (Design 116: SLAB PLAN & REINFORCING sheets 177-188 = PDF 184-195, bar list sheet 189 = PDF 196,
// typical sections / superstructure notes sheets 92-97 = PDF 94-99).  Run after the build scripts (deck edges, joints); re-runnable.
//   sbm run samples\plans\deck-rebar-i80.csx "temp\model.sbm"
// Read on 150-300 dpi clips (fractions are custom glyphs):
//  * 7a #7 transverse: 2 1/2" clear below the top of slab (top) / 1" clear above the bottom (bottom); bars end 2" from the deck edges; radial (normal to the alignment;
//    the skewed deck ends just cut the lines, checked against the plans' printed bar lengths); one continuous 7" grid along the SOUTH edge (6 1/2" in the wedge at the
//    skewed beginning of Unit 3), the top and the bottom grids offset by 3 1/2".  Each line = lapped pieces (min. lap 3'-3), stock pieces fixed, the NORTH-most piece
//    the remainder ("VARIES").  Runs/marks as printed ("126 SPA. @ 7 = 73'-6, 127-7a1, 7a4 & 7a6 BARS").
//  * 6b #6 longitudinal, alternating top and bottom: 24 equal spaces / 25 bars between 8 1/2" inside the girder lines of a constant bay (the Part Typical Section);
//    bays that narrow (north of the continuous girders) have the equal-space counts printed on the longitudinal layouts.
//  * Transverse bar ends stop short of the joint blockouts: clear distance from the deck-end line (normal to it) calibrated on the bar lengths printed at the skewed
//    ends (7a4/7a7 Unit 1 begin, 7a2/7a13 Unit 2 end, 7a4/7a15 Unit 3 begin, 7a3/7a10/7a13 Unit 3 end): ~17" at the abutments (blockout D = 18"), ~27" at the piers
//    (D = 25").  Unit 2's begin is the KINKED slab end at Pier 6A (sheet 177 Detail A), which the deck outline does not have: the first lines there are longer than the plan's.
//  * Bar lists (PDF 196) are in docs\plan-data\deck-bar-lists.csv; the generator compares every placed mark with them and reports the differences.
using SBM.Core.Bridge;
using System.Globalization;

double FI(double ft, double inch) => Ft(ft) + In(inch);
double ParseFtIn(string s)
{
    var m = System.Text.RegularExpressions.Regex.Match(s, @"^(\d+)'-(\d+)$");
    return FI(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
}
string csv = new[] { @"docs\plan-data\deck-bar-lists.csv", @"C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\docs\plan-data\deck-bar-lists.csv" }.First(System.IO.File.Exists);
List<DeckPlanBarLine> BarList(string br, int unit)
{
    var res = new List<DeckPlanBarLine>();
    foreach (var ln in System.IO.File.ReadAllLines(csv))
    {
        if (ln.StartsWith("#") || ln.Trim().Length == 0) continue;
        var c = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
        foreach (char ch in ln) { if (ch == '"') q = !q; else if (ch == ',' && !q) { c.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); }
        c.Add(sb.ToString());
        if (c[0] != br || int.Parse(c[1]) != unit) continue;
        res.Add(new DeckPlanBarLine
        {
            Mark = c[2], Size = c[3], Location = c[4], Count = int.Parse(c[5], CultureInfo.InvariantCulture),
            Length = c[6].Length > 0 ? ParseFtIn(c[6]) : 0, Varies = c[7] == "1", WeightLb = double.Parse(c[8], CultureInfo.InvariantCulture),
        });
    }
    return res;
}

const double Lap = 3.25;      // min. lap 3'-3 (ft)
DeckLinePiece P(string mark, double lenFt = 0) => new() { Mark = mark, Length = Ft(lenFt) };
double F(int ft, int inch) => ft + inch / 12.0;
DeckTransverseZone Z(DeckBarLayer layer, string name, int n, double spacingIn, double startFt, bool after, DeckBarSide from, DeckBarSide refEdge, params DeckLinePiece[] pieces)
{
    var z = new DeckTransverseZone
    {
        Name = name, Layer = layer, Size = "#7", Reference = refEdge, Spacing = In(spacingIn), Count = n, Lap = Ft(Lap), FirstFrom = from,
        StartAfterPrevious = after, StartDistance = Ft(startFt),
    };
    z.Pieces.AddRange(pieces);
    return z;
}
const DeckBarLayer TOP = DeckBarLayer.Top, BOT = DeckBarLayer.Bottom;
const DeckBarSide L = DeckBarSide.Left, R = DeckBarSide.Right;


// ---------------------------------------------------------------- longitudinal helpers
// Overhang bars (Unit 1 Detail A, design sheet 97, read from the vector section: the 8 bar dots between the slab edge and the girder CL), inches from the slab edge.
// The Unit 2/3 overhangs (3'-0 .. 3'-9) have the same detail; the number of lines is confirmed by the bar-list weights.
var ovPos = new[] { 5.1, 7.1, 10.1, 13.1, 15.1, 19.1, 23.4, 27.6 }.Select(x => In(x)).ToList();
var ovLay = new List<DeckBarLayer> { TOP, BOT, TOP, BOT, TOP, TOP, BOT, TOP };
DeckLongZone Long(string name, DeckLongRef a, DeckLongRef b, string mark, double stockFt, string closure = "", bool lastIsStock = false) => new()
{
    Name = name, Left = a, Right = b, Size = "#6", Mark = mark, ClosureMark = closure, StockLength = Ft(stockFt), Lap = Ft(3), LastIsStock = lastIsStock, FirstLayer = TOP,
};
DeckLongZone Overhang(bool north, int girder, string mark, double stockFt, string closure, bool lastIsStock)
{
    var z = north ? Long("north overhang", DeckLongRef.Edge(L), DeckLongRef.OfGirder(girder), mark, stockFt, closure, lastIsStock)
                  : Long("south overhang", DeckLongRef.OfGirder(girder), DeckLongRef.Edge(R), mark, stockFt, closure, lastIsStock);
    z.Positions = ovPos.ToList(); z.PositionLayers = ovLay.ToList(); z.PositionsFromRight = !north;
    return z;
}
// constant 11'-6 bays: 24 equal spaces, 25 bars, 8 1/2" inside the girder lines (Part Typical Section, design sheet 92)
IEnumerable<DeckLongZone> ConstBays(int firstGirder, int lastGirder, string mark, double stockFt, string closure, bool lastIsStock)
{
    for (int g = firstGirder; g < lastGirder; g++)
    {
        var z = Long($"bay G{g}-G{g + 1}", DeckLongRef.OfGirder(g), DeckLongRef.OfGirder(g + 1), mark, stockFt, closure, lastIsStock);
        z.Spaces = 24; z.LeftMargin = In(8.5); z.RightMargin = In(8.5);
        yield return z;
    }
}
// variable bays north of the continuous girders: columns of the plans' "N EQ. SPA." arrows (station of the arrow, rows = girder pair + spaces); a zone runs from the
// midpoint to the previous arrow column to the midpoint to the next one while the pair and the count stay the same; 9" inside the girder lines (Detail B).
IEnumerable<DeckLongZone> VarBays((string sta, (int l, int r, int n)[] rows)[] cols, string mark, double stockFt, string closure, bool lastIsStock)
{
    double[] st = cols.Select(c => Sta(c.sta)).ToArray();
    var open = new Dictionary<(int, int, int), int>();   // zone key -> first column
    var res = new List<DeckLongZone>();
    void Close((int, int, int) key, int first, int lastCol)
    {
        double a = first == 0 ? double.NaN : (st[first - 1] + st[first]) / 2;
        double b = lastCol == cols.Length - 1 ? double.NaN : (st[lastCol] + st[lastCol + 1]) / 2;
        var z = Long($"bay G{key.Item1}-G{key.Item2} ({key.Item3} sp.)", DeckLongRef.OfGirder(key.Item1), DeckLongRef.OfGirder(key.Item2), mark, stockFt, closure, lastIsStock);
        z.Spaces = key.Item3; z.LeftMargin = In(9); z.RightMargin = In(9); z.StartStation = a; z.EndStation = b;
        res.Add(z);
    }
    for (int i = 0; i < cols.Length; i++)
    {
        var keys = cols[i].rows.Select(r => (r.l, r.r, r.n)).ToHashSet();
        foreach (var k in open.Keys.ToList()) if (!keys.Contains(k)) { Close(k, open[k], i - 1); open.Remove(k); }
        foreach (var k in keys) if (!open.ContainsKey(k)) open[k] = i;
    }
    foreach (var k in open.Keys.ToList()) Close(k, open[k], cols.Length - 1);
    return res.OrderBy(z => double.IsNaN(z.StartStation) ? double.MinValue : z.StartStation);
}
(int, int, int) Rw(int l, int r, int n) => (l, r, n);

// ---------------------------------------------------------------- Unit 1
{
    var b = Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 1");
    var t = new List<DeckTransverseZone>();
    double g = 7.0 / 12;
    // measured along the north (left) edge from the north corner of the skewed begin line (sheet 178: "3'-10 7/8", "4'-2 3/8")
    t.Add(Z(BOT, "bottom wedge", 97, 7, 3 + 10.875 / 12 + 0.0 + 1, false, L, L, P("7a4")));
    t[0].StartDistance = FI(3, 10.875);
    t.Add(Z(BOT, "bottom 4 lines", 4, 7, g, true, L, L, P("7a2", F(27, 4)), P("7a3")));
    t.Add(Z(BOT, "bottom main", 2036, 7, g, true, L, L, P("7a2", F(27, 4)), P("7a1")));
    t.Add(Z(TOP, "top wedge", 96, 7, 0, false, L, L, P("7a7")));
    t[3].StartDistance = FI(4, 2.375);
    t.Add(Z(TOP, "top 5 lines", 5, 7, g, true, L, L, P("7a5", F(33, 1)), P("7a6")));
    t.Add(Z(TOP, "top main", 2036, 7, g, true, L, L, P("7a5", F(33, 1)), P("7a5", F(33, 1))));
    var r = b.Deck.Reinforcement;
    r.Enabled = true; r.EpoxyCoated = true; r.TopCover = In(2.5); r.BottomCover = In(1); r.SideCover = In(2);
    var lz1 = new List<DeckLongZone> { Overhang(true, 1, "6b1", F(39, 0), "6b1c", false) };
    lz1.AddRange(ConstBays(1, 6, "6b1", F(39, 0), "6b1c", false));
    lz1.Add(Overhang(false, 6, "6b1", F(39, 0), "6b1c", false));
    r.Plan = new DeckPlanReinforcement { WeightTolerance = 0.03, PlanBarList = BarList("I-80 WB", 1), Transverse = t, Longitudinal = lz1, Families = new() { "6b" }, TransverseBeginClear = In(17), TransverseEndClear = In(27), LongitudinalBeginCover = In(17), LongitudinalEndCover = In(27) };
}

// ---------------------------------------------------------------- Unit 2
{
    var b = Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 2");
    var t = new List<DeckTransverseZone>();
    double g = 7.0 / 12;
    // along the south (right) edge from the begin end of slab: top 2'-5 3/8, bottom 2'-8 7/8 (= top + 3 1/2"); runs continue at 7"
    var z0 = Z(TOP, "top 2 lines", 2, 7, F(2, 5) + 0.375 / 12, false, R, R, P("7a1", F(56, 4)), P("7a3")); t.Add(z0);
    t.Add(Z(TOP, "top 4 lines", 4, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a4", F(32, 8)), P("7a5")));
    t.Add(Z(TOP, "top 127", 127, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a4", F(32, 8)), P("7a6")));
    t.Add(Z(TOP, "top 1006", 1006, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a7")));
    t.Add(Z(TOP, "top 313 (7a8)", 313, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a8")));
    t.Add(Z(TOP, "top 313 (7a9)", 313, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a9")));
    t.Add(Z(TOP, "top 291", 291, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a10", F(21, 10))));
    t.Add(Z(TOP, "top 14", 14, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a11")));
    t.Add(Z(TOP, "top 52 (end)", 52, 7, g, true, R, R, P("7a2")));
    t.Add(Z(BOT, "bottom 1 line", 1, 7, F(2, 8) + 0.875 / 12, false, R, R, P("7a12", F(50, 7)), P("7a14", F(25, 10))));
    t.Add(Z(BOT, "bottom 4 lines", 4, 7, g, true, R, R, P("7a12", F(50, 7)), P("7a15", F(33, 9)), P("7a16")));
    t.Add(Z(BOT, "bottom 325", 325, 7, g, true, R, R, P("7a12", F(50, 7)), P("7a15", F(33, 9)), P("7a17")));
    t.Add(Z(BOT, "bottom 808", 808, 7, g, true, R, R, P("7a12", F(50, 7)), P("7a18")));
    t.Add(Z(BOT, "bottom 314", 314, 7, g, true, R, R, P("7a12", F(50, 7)), P("7a19")));
    t.Add(Z(BOT, "bottom 313", 313, 7, g, true, R, R, P("7a12", F(50, 7)), P("7a20")));
    t.Add(Z(BOT, "bottom 291", 291, 7, g, true, R, R, P("7a12", F(50, 7)), P("7a21", F(27, 7))));
    t.Add(Z(BOT, "bottom 14", 14, 7, g, true, R, R, P("7a12", F(50, 7)), P("7a22")));
    t.Add(Z(BOT, "bottom 51 (end)", 51, 7, g, true, R, R, P("7a13")));
    var r = b.Deck.Reinforcement;
    r.Enabled = true; r.EpoxyCoated = true; r.TopCover = In(2.5); r.BottomCover = In(1); r.SideCover = In(2);
    var lz2 = new List<DeckLongZone> { Overhang(true, 1, "6b1", F(39, 10), "6b1c", false) };
    lz2.AddRange(VarBays(new (string, (int, int, int)[])[]
    {
        ("7591+25.5", new[] { Rw(1,2,24), Rw(2,3,24), Rw(3,4,24), Rw(4,5,20), Rw(5,6,20) }),
        ("7591+56.2", new[] { Rw(1,2,24), Rw(2,3,24), Rw(3,4,24), Rw(4,5,20), Rw(5,6,20) }),
        ("7591+92.6", new[] { Rw(1,2,24), Rw(2,3,24), Rw(3,4,24), Rw(4,5,18), Rw(5,6,18) }),
        ("7592+29.9", new[] { Rw(1,2,22), Rw(2,3,22), Rw(3,4,22), Rw(4,5,18), Rw(5,6,18) }),
        ("7592+67.7", new[] { Rw(1,2,22), Rw(2,3,22), Rw(3,4,22), Rw(4,5,16), Rw(5,6,16) }),
        ("7593+04.1", new[] { Rw(1,2,22), Rw(2,3,22), Rw(3,4,20), Rw(4,5,16), Rw(5,6,16) }),
        ("7593+42.0", new[] { Rw(1,2,22), Rw(2,3,22), Rw(3,4,20), Rw(4,5,14), Rw(5,6,14) }),
        ("7593+78.8", new[] { Rw(1,2,22), Rw(2,3,22), Rw(3,4,18), Rw(4,5,14), Rw(5,6,14) }),
        ("7594+24.2", new[] { Rw(1,2,20), Rw(2,3,20), Rw(3,4,18), Rw(4,6,24) }),
        ("7594+59.3", new[] { Rw(1,2,20), Rw(2,3,20), Rw(3,4,18), Rw(4,6,24) }),
        ("7594+94.3", new[] { Rw(1,2,20), Rw(2,3,20), Rw(3,4,16), Rw(4,6,22) }),
        ("7595+29.3", new[] { Rw(1,2,18), Rw(2,3,20), Rw(3,4,16), Rw(4,6,20) }),
        ("7595+64.4", new[] { Rw(1,2,18), Rw(2,3,18), Rw(3,4,14), Rw(4,6,18) }),
        ("7595+99.4", new[] { Rw(1,2,18), Rw(2,3,18), Rw(3,4,14), Rw(4,6,16) }),
        ("7596+43.5", new[] { Rw(1,2,18), Rw(2,3,18), Rw(3,6,26) }),
        ("7596+74.5", new[] { Rw(1,2,16), Rw(2,3,18), Rw(3,6,24) }),
        ("7597+74.5", new[] { Rw(1,2,16), Rw(2,3,16), Rw(3,6,22) }),
        ("7598+01.8", new[] { Rw(1,2,14), Rw(2,3,16), Rw(3,6,20) }),
        ("7598+40.2", new[] { Rw(1,2,14), Rw(2,3,14), Rw(3,6,18) }),
        ("7598+73.8", new[] { Rw(1,2,14), Rw(2,3,12), Rw(3,6,14) }),
        ("7599+12.1", new[] { Rw(1,2,14), Rw(2,6,22) }),
        ("7599+51.6", new[] { Rw(1,2,14), Rw(2,6,20) }),
        ("7599+90.4", new[] { Rw(1,2,14), Rw(2,6,18) }),
        ("7600+23.0", new[] { Rw(1,2,14), Rw(2,6,14) }),
        ("7600+71.0", new[] { Rw(1,2,14), Rw(2,6,14) }),
        ("7601+00.8", new[] { Rw(1,6,26) }),
        ("7601+27.3", new[] { Rw(1,6,26) }),
        ("7601+57.8", new[] { Rw(1,6,24) }),
        ("7601+98.7", new[] { Rw(1,6,24) }),
        ("7602+29.5", new[] { Rw(1,6,24) }),
        ("7602+66.7", new[] { Rw(1,6,24) }),
        ("7602+96.0", new[] { Rw(1,6,24) }),
    }, "6b1", F(39, 10), "6b1c", false));
    lz2.AddRange(ConstBays(6, 11, "6b1", F(39, 10), "6b1c", false));
    lz2.Add(Overhang(false, 11, "6b1", F(39, 10), "6b1c", false));
    r.Plan = new DeckPlanReinforcement { WeightTolerance = 0.03, PlanBarList = BarList("I-80 WB", 2), Transverse = t, Longitudinal = lz2, Families = new() { "6b" }, TransverseBeginClear = In(27), TransverseEndClear = In(27), LongitudinalBeginCover = In(27), LongitudinalEndCover = In(27) };
}

// ---------------------------------------------------------------- Unit 3
{
    var b = Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 3");
    var t = new List<DeckTransverseZone>();
    double g = 7.0 / 12, g65 = 6.5 / 12;
    // dims along the south edge; the first lines lie BEFORE the south corner of the skewed begin line (wedge), so the chain is anchored at the END:
    // last line 2'-4 3/8 (top) / 2'-8 (bottom) before the end of the 906'-3 1/8 edge.
    double edge = F(906, 3) + 0.125 / 12;
    double chainTop = 58 * 6.5 / 12 + g65 + 48 * 6.5 / 12 + g + 330 * g + g + 291 * g + g + 279 * g + g + 629 * g + g + 3 * g + g + 8 * g;
    double chainBot = 59 * 6.5 / 12 + g65 + 3 * 6.5 / 12 + g65 + 44 * 6.5 / 12 + g + 57 * g + g + 272 * g + g + 291 * g + g + 279 * g + g + 629 * g + g + 2 * g + g + 8 * g;
    double s0Top = edge - (F(2, 4) + 0.375 / 12) - chainTop;
    double s0Bot = edge - F(2, 8) - chainBot;
    Print($"Unit 3 chain start (from the south corner): top {s0Top:F3} ft, bottom {s0Bot:F3} ft");
    t.Add(Z(TOP, "top wedge 59", 59, 6.5, s0Top, false, L, R, P("7a4")));
    t.Add(Z(TOP, "top wedge 49", 49, 6.5, g65, true, L, R, P("7a5", F(58, 3)), P("7a2")));
    t.Add(Z(TOP, "top 331", 331, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a6")));
    t.Add(Z(TOP, "top 292", 292, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a7")));
    t.Add(Z(TOP, "top 280", 280, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a8")));
    t.Add(Z(TOP, "top 630", 630, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a9", F(27, 10))));
    t.Add(Z(TOP, "top 4", 4, 7, g, true, R, R, P("7a1", F(56, 4)), P("7a10")));
    t.Add(Z(TOP, "top 9 (end)", 9, 7, g, true, R, R, P("7a3")));
    t.Add(Z(BOT, "bottom wedge 60", 60, 6.5, s0Bot, false, L, R, P("7a15")));
    t.Add(Z(BOT, "bottom 4", 4, 6.5, g65, true, L, R, P("7a16", F(28, 4)), P("7a14")));
    t.Add(Z(BOT, "bottom wedge 45", 45, 6.5, g65, true, L, R, P("7a16", F(28, 4)), P("7a17", F(38, 9)), P("7a12")));
    t.Add(Z(BOT, "bottom wedge 58", 58, 7, g, true, L, R, P("7a16", F(28, 4)), P("7a17", F(38, 9)), P("7a11", F(50, 7))));
    t.Add(Z(BOT, "bottom 273", 273, 7, g, true, R, R, P("7a11", F(50, 7)), P("7a18")));
    t.Add(Z(BOT, "bottom 292", 292, 7, g, true, R, R, P("7a11", F(50, 7)), P("7a19")));
    t.Add(Z(BOT, "bottom 280", 280, 7, g, true, R, R, P("7a11", F(50, 7)), P("7a20")));
    t.Add(Z(BOT, "bottom 630", 630, 7, g, true, R, R, P("7a11", F(50, 7)), P("7a21", F(33, 7))));
    t.Add(Z(BOT, "bottom 3", 3, 7, g, true, R, R, P("7a11", F(50, 7)), P("7a22")));
    t.Add(Z(BOT, "bottom 9 (end)", 9, 7, g, true, R, R, P("7a13")));
    var r = b.Deck.Reinforcement;
    r.Enabled = true; r.EpoxyCoated = true; r.TopCover = In(2.5); r.BottomCover = In(1); r.SideCover = In(2);
    var lz3 = new List<DeckLongZone> { Overhang(true, 1, "6b2", F(39, 9), "", true) };
    lz3.AddRange(VarBays(new (string, (int, int, int)[])[]
    {
        ("7603+59.9", new[] { Rw(1,2,26), Rw(2,3,26), Rw(3,4,26) }),
        ("7603+88.8", new[] { Rw(1,2,24), Rw(2,3,24), Rw(3,4,26) }),
        ("7604+20.4", new[] { Rw(1,2,22), Rw(2,3,22), Rw(3,4,26) }),
        ("7604+56.4", new[] { Rw(1,2,20), Rw(2,3,20), Rw(3,4,26) }),
        ("7604+87.1", new[] { Rw(1,2,18), Rw(2,3,18), Rw(3,4,24) }),
        ("7605+20.0", new[] { Rw(1,2,16), Rw(2,3,16), Rw(3,4,22) }),
        ("7605+70.0", new[] { Rw(1,3,26), Rw(3,4,22) }),
        ("7606+01.4", new[] { Rw(1,3,22), Rw(3,4,20) }),
        ("7606+34.3", new[] { Rw(1,3,20), Rw(3,4,18) }),
        ("7606+68.1", new[] { Rw(1,3,16), Rw(3,4,16) }),
        ("7607+00.1", new[] { Rw(1,3,12), Rw(3,4,12) }),
        ("7608+00.0", new[] { Rw(1,4,22) }),
        ("7608+60.8", new[] { Rw(1,4,18) }),
        ("7609+32.0", new[] { Rw(1,4,16) }),
        ("7610+09.0", new[] { Rw(1,4,14) }),
        ("7610+85.0", new[] { Rw(1,4,14) }),
        ("7611+49.0", new[] { Rw(1,4,14) }),
    }, "6b2", F(39, 9), "", true));
    lz3.AddRange(ConstBays(4, 10, "6b1", F(39, 1), "", true));
    lz3.Add(Overhang(false, 10, "6b1", F(39, 1), "", true));
    r.Plan = new DeckPlanReinforcement { WeightTolerance = 0.03, PlanBarList = BarList("I-80 WB", 3), Transverse = t, Longitudinal = lz3, TransverseBeginClear = In(27), TransverseEndClear = In(17.5), LongitudinalBeginCover = In(27), LongitudinalEndCover = In(17.5) };
}

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var n in new[] { 1, 2, 3 })
{
    var name = $"I-80 WB Unit {n}";
    var cm = Doc.Concrete(name);
    Print($"== {name}");
    foreach (var s in cm.Notes.Where(s => s.StartsWith("Plan-driven") || s.StartsWith("  ") || s.StartsWith("Edge") || s.StartsWith("Transverse zone") || s.StartsWith("Top:") || s.StartsWith("Bottom:"))) Print("   " + s);
    foreach (var d in cm.Diagnostics.Where(d => d.Message.StartsWith("Deck reinforc"))) Print($"   {d.Severity}: {d.Message}");
}
if (Environment.GetEnvironmentVariable("DR_NOSAVE") != "1") Doc.Save();
