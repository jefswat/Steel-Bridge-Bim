// Plan-driven deck reinforcing of Ramp G Units 1-4 (Design 316: SLAB PLAN & REINFORCING sheets 135-138 = PDF 137-140, bar list sheet 139 = PDF 141,
// typical section / superstructure notes sheet 83 = PDF 85).  Run after the build scripts; re-runnable.
//   sbm run samples\plans\deck-rebar-ramp-g.csx "temp\model.sbm"
// What the plans say (all read on 150-300 dpi clips):
//  * 7a1 #7 transverse at 10", top (2 1/2" clear below top of slab) and bottom (1" clear above the bottom), ONE bar across the whole width (34'-10 = 35'-2
//    less 2" each side), radial; positions measured along the LEFT edge from the begin end of slab:
//      U1 top 3 5/8" + 1040 spa. @ 10 (1041 bars), bottom 8 5/8" + 1039 spa.;  U2 top 2'-7 7/16 + 900 spa., bottom 3'-0 7/16 + 899 spa.;
//      U3 top 2'-3 + 1300 spa., bottom 2'-8 + 1299 spa.;  U4 top: 350 spa. along the left edge (351 bars) from 2'-3, then 826 spa. (827 bars) and 24 spa. (25 bars, 7a2
//      33'-3 .. 3'-1) measured along the RIGHT edge, "10" between the runs; bottom the same from 2'-8 with 825 spa. (826) and 25 spa. (26 bars, 7a3 33'-10 .. 2'-6).
//      The 25/26 bars of the last run are placed 10" after the previous run (as drawn); the plan puts the last one 4'-7 1/2 / 4'-2 1/2 before the end of the right edge
//      (model: 4'-7 9/16 / 4'-2 9/16; right edge 1002'-2 3/16 vs the plan's 1002'-2 1/4 with the Pier 19G end of slab of sheet 139 Details A/B, deck-end-kinks.csx).
//  * 5j1 #5 x 6'-10 top transverse "at rail", at the left and at the right edge, centered between the 7a bars.
//  * 6b1 #6 longitudinal, top and bottom alternating: per girder bay 22 equal spaces of 4 1/2" between lines 8 1/2" inside the girders (the section's 11 spa. @ 9" top
//    + 10 spa. @ 9" bottom, 23 bars); stock 39'-2 (U1, U3), 38'-9 (U2), 39'-11 (U4), min. lap 3'-0 (sheet 135 plan).
//  * COUNT CHECK: 6b1 / (pieces per line) = 85 lines in every unit = 69 bay lines + 16 in the overhangs.  The overhang positions are NOT dimensioned at readable
//    resolution (only "2" from the edge"): they are INFERRED as 8 lines per overhang at the bay pattern (4 1/2" apart, first 2" from the edge, alternating top/bottom).
//  * Not placed (schedule only): 5a4 slab at drains, 5a5 insert anchors, end dam bars 5d1-5d5, 4d6/4d7, 5e1-5e8 (sections B-B/C-C/E-E).
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

// unit: stock 6b1, top start / count, bottom start / count (inches), U4 special
var specs = new (int U, string Stock, double TopStart, int TopN, double BotStart, int BotN)[]
{
    (1, "39'-2", 3.625, 1041, 8.625, 1040),
    (2, "38'-9", 31.4375, 901, 36.4375, 900),
    (3, "39'-2", 27, 1301, 32, 1300),
    (4, "39'-11", 27, 351, 32, 351),
};
foreach (var sp in specs)
{
    var b = Doc.Project.Bridges.First(x => x.Name == $"Ramp G Unit {sp.U}");
    var r = b.Deck.Reinforcement;
    r.Enabled = true; r.EpoxyCoated = true;
    r.TopCover = In(2.5); r.BottomCover = In(1); r.SideCover = In(2);
    // longitudinal bars stop at the joint blockout (D: 4" South Abutment, 25" piers): reproduces the 6b1 counts of Units 1, 2 and 4 exactly (Unit 3: -0.7%)
    var plan = new DeckPlanReinforcement { LongitudinalBeginCover = In(sp.U == 1 ? 4 : 25), LongitudinalEndCover = In(25), WeightTolerance = 0.03, PlanBarList = BarList("Ramp G", sp.U) };
    if (double.TryParse(Environment.GetEnvironmentVariable("DR_LB"), out var lbIn)) plan.LongitudinalBeginCover = In(lbIn);
    if (double.TryParse(Environment.GetEnvironmentVariable("DR_LE"), out var leIn)) plan.LongitudinalEndCover = In(leIn);
    // CALIBRATION (not dimensioned on the plans, as for I-80 in docs\plan-data\deck-rebar.md section 3): where the transverse bars stop at the skewed
    // Pier 19G end of Unit 4. The printed last bars 7a2 3'-1 (4'-7 1/2 before the end of the right edge) and 7a3 2'-6 (4'-2 1/2) and the first 7a2 33'-3
    // need the bar ends 27" from the end of slab, normal to it (= blockout D 25" + 2", as at the I-80 Unit 2 end): model 33'-2 7/8 .. 3'-1 5/8 / 33'-10 3/8 .. 2'-6.
    // The other Ramp G ends are radial (no clipped lines).
    if (sp.U == 4) plan.TransverseEndClear = In(27);
    if (sp.U == 4 && double.TryParse(Environment.GetEnvironmentVariable("DR_TE"), out var teIn)) plan.TransverseEndClear = In(teIn);
    DeckLinePiece P(string mark, double len = 0) => new() { Mark = mark, Length = len };
    DeckTransverseZone T(string name, DeckBarLayer layer, double startIn, int n, string mark, DeckBarSide refEdge = DeckBarSide.Left) => new()
    {
        Name = name, Layer = layer, Size = "#7", Reference = refEdge, StartDistance = In(startIn), Spacing = In(10), Count = n, Pieces = { P(mark) },
        FirstFrom = DeckBarSide.Left,
    };
    var tr = plan.Transverse;
    int iTop1 = tr.Count; tr.Add(T("top 7a1", DeckBarLayer.Top, sp.TopStart, sp.TopN, "7a1"));
    if (sp.U == 4)
    {
        int iTop2 = tr.Count;
        tr.Add(new DeckTransverseZone { Name = "top 7a1 (right edge)", Layer = DeckBarLayer.Top, Size = "#7", Reference = DeckBarSide.Right, StartAfterPrevious = true, StartDistance = In(10), Spacing = In(10), Count = 827, Pieces = { P("7a1") } });
        int iTop3 = tr.Count;
        tr.Add(new DeckTransverseZone { Name = "top 7a2 (end)", Layer = DeckBarLayer.Top, Size = "#7", Reference = DeckBarSide.Right, StartAfterPrevious = true, StartDistance = In(10), Spacing = In(10), Count = 25, Pieces = { P("7a2") } });
        tr.Add(T("bottom 7a1", DeckBarLayer.Bottom, sp.BotStart, sp.BotN, "7a1"));
        tr.Add(new DeckTransverseZone { Name = "bottom 7a1 (right edge)", Layer = DeckBarLayer.Bottom, Size = "#7", Reference = DeckBarSide.Right, StartAfterPrevious = true, StartDistance = In(10), Spacing = In(10), Count = 826, Pieces = { P("7a1") } });
        tr.Add(new DeckTransverseZone { Name = "bottom 7a3 (end)", Layer = DeckBarLayer.Bottom, Size = "#7", Reference = DeckBarSide.Right, StartAfterPrevious = true, StartDistance = In(10), Spacing = In(10), Count = 26, Pieces = { P("7a3") } });
        // 5j1 "between the 7a bars": left edge 1177 = 350 + 1 + 826, right edge 1199 = 350 + 1 + 826 + 1 + 21 (the last 3 gaps at the chamfered corner are not placed: plan count)
        DeckTransverseZone J(string name, int between, bool toNext, int n, DeckBarSide from) => new()
        {
            Name = name, Layer = DeckBarLayer.Top, Size = "#5", BetweenZone = between, BetweenToNext = toNext, Count = n, FirstFrom = from, Pieces = { P("5j1", FI(6, 10)) },
        };
        tr.Add(J("5j1 left (run 1)", iTop1, true, 351, DeckBarSide.Left));
        tr.Add(J("5j1 left (run 2)", iTop2, false, 826, DeckBarSide.Left));
        tr.Add(J("5j1 right (run 1)", iTop1, true, 351, DeckBarSide.Right));
        tr.Add(J("5j1 right (run 2)", iTop2, true, 827, DeckBarSide.Right));
        tr.Add(J("5j1 right (run 3)", iTop3, false, 21, DeckBarSide.Right));
    }
    else
    {
        tr.Add(T("bottom 7a1", DeckBarLayer.Bottom, sp.BotStart, sp.BotN, "7a1"));
        foreach (var from in new[] { DeckBarSide.Left, DeckBarSide.Right })
            tr.Add(new DeckTransverseZone
            {
                Name = "5j1 " + from.ToString().ToLower(), Layer = DeckBarLayer.Top, Size = "#5", BetweenZone = iTop1, Count = sp.TopN - 1, FirstFrom = from, Pieces = { P("5j1", FI(6, 10)) },
            });
    }

    // longitudinal: 3 girder bays + 2 overhangs
    double stock = ParseFtIn(sp.Stock);
    DeckLongZone L(string name, DeckLongRef a, DeckLongRef c) => new()
    {
        Name = name, Left = a, Right = c, Size = "#6", Mark = "6b1", StockLength = stock, Lap = Ft(3), LastIsStock = true, FirstLayer = DeckBarLayer.Top,
    };
    var ov = new List<double> { 2, 6.5, 11, 15.5, 20, 24.5, 29, 33.5 }.Select(x => In(x)).ToList();
    var lz = plan.Longitudinal;
    var left = L("left overhang (inferred)", DeckLongRef.Edge(DeckBarSide.Left), DeckLongRef.OfGirder(1)); left.Positions = ov; lz.Add(left);
    for (int g = 1; g <= 3; g++)
    {
        var z = L($"bay G{g}-G{g + 1}", DeckLongRef.OfGirder(g), DeckLongRef.OfGirder(g + 1));
        z.Spaces = 22; z.LeftMargin = In(8.5); z.RightMargin = In(8.5);
        lz.Add(z);
    }
    var right = L("right overhang (inferred)", DeckLongRef.OfGirder(4), DeckLongRef.Edge(DeckBarSide.Right)); right.Positions = ov.ToList(); right.PositionsFromRight = true; lz.Add(right);
    r.Plan = plan;
}

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var sp in specs)
{
    var name = $"Ramp G Unit {sp.U}";
    var cm = Doc.Concrete(name);
    Print($"== {name}");
    foreach (var n in cm.Notes.Where(n => n.StartsWith("Plan-driven") || n.StartsWith("  ") || n.StartsWith("Edge") || n.StartsWith("Transverse zone") || n.StartsWith("Top:") || n.StartsWith("Bottom:"))) Print("   " + n);
    foreach (var d in cm.Diagnostics.Where(d => d.Message.StartsWith("Deck reinforc"))) Print($"   {d.Severity}: {d.Message}");
}
Doc.Save();
