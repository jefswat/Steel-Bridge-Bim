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
    r.Plan = new DeckPlanReinforcement { LongitudinalEndCover = In(2), WeightTolerance = 0.03, PlanBarList = BarList("I-80 WB", 1), Transverse = t, TransverseBeginClear = In(17), TransverseEndClear = In(27) };
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
    r.Plan = new DeckPlanReinforcement { LongitudinalEndCover = In(2), WeightTolerance = 0.03, PlanBarList = BarList("I-80 WB", 2), Transverse = t, TransverseBeginClear = In(27), TransverseEndClear = In(27) };
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
    r.Plan = new DeckPlanReinforcement { LongitudinalEndCover = In(2), WeightTolerance = 0.03, PlanBarList = BarList("I-80 WB", 3), Transverse = t, TransverseBeginClear = In(27), TransverseEndClear = In(17.5) };
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
