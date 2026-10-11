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
//  * END DAM DIAPHRAGM bars at the expansion piers 5G, 9G, 14G (PDF 137 section B-B, bend diagrams PDF 141, layout along the joint PDF 138 "PARTIAL SECTION NEAR
//    EXPANSION-EXPANSION PIER"): 5d2 / 5d3 hoops, 5d4 U, 5d5 Z, placed in 3D (see END_DAM below). Everything else of the end dams is schedule only with an UNRESOLVED warning.
//  * Not placed (schedule only): 5a4 slab at drains, 5a5 insert anchors (drain positions, sheets 158+), the end dam bars listed in Unresolved below.
using SBM.Core.Bridge;
using System.Globalization;

const bool END_DAM = true;     // false = the end dam bars stay schedule only (as before)

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

// ---- END DAM DIAPHRAGM bars (expansion piers).  Section B-B (PDF 137), all inches, X from the deck-end face into the deck, Drop below the top of slab:
//  block 2'-2 deep, blockout 2'-1 x 1'-2, soffit flat 3'-3 then 45 deg to the 8" slab soffit (joints-ramp-g.csx).  Printed clears: 3" CLR. from the deck-end face,
//  2" CLR. (TYP.) from the blockout wall, 1 1/2" CLR. above the soffit (to the 5d4 bottom leg); elsewhere the superstructure notes' 2" clear (PDF 85).
//  * 5d4 (U 4'-0 / 1'-9 / 1'-6, PDF 141): vertical leg 3" from the face, bottom leg 1 1/2" above the soffit -> top leg at 26 - 1.5 - 21 = 3.5" below the top (just under the
//    7a top bars, 2 1/2" clear + 7/8" = 3 3/8").
//  * 5d3 (hoop 2'-10 x 8, 6" tails): 3" from the face, resting on the 5d4 bottom leg as drawn (bottom 1 1/2" + 5/8" above the soffit).
//  * 5d2 (hoop 11 x 1'-8, 6" tails): 2" clear from the blockout wall (X 27..38), bottom level with the 5d3 bottom as drawn.
//  * 5d5 (1'-6, 2'-5 at 45 deg = 1'-8 1/2 x 1'-8 1/2, 1'-6): bottom leg 1 1/2" above the soffit (with 5d4), slope 2" clear from the 45 deg soffit -> bottom bend at X = 65 - 2 sqrt 2 - 24.5.
//  Along the joint (PARTIAL SECTION, PDF 138): per bay 10" from the girder CL + 8 spa. @ 1'-0 + 10" (9 bars, girders 9'-8 apart); outside the exterior girders 10" and 1'-0 more
//  (then 1'-3 to the edge).  The four marks of a set ("9-5d2, 9-5d3, 9-5d4, 9-5d5") are at the SAME position along the joint, as printed (no side-by-side offset:
//  DeckEndDamBar.SideOffset = 0; where two of them share a leg, e.g. the 5d3 / 5d4 legs at the face, the 3D bars coincide).
//  Pier 19G (Unit 4 end, section E-E PDF 140): "10 bars between girders and 3 outside exterior girders at equal spacing" on the 33 deg skewed joint - the partial section
//  says "reinforcing for Pier No. 19G similar" but gives no dimensions for it: those 36 bars per mark are counted, not modeled (UNRESOLVED).
void EndDam(DeckPlanReinforcement plan, int unit)
{
    double db = In(0.625);
    var ends = unit == 1 ? DeckEndSide.End : unit == 4 ? DeckEndSide.Begin : DeckEndSide.Both;
    int unplaced = unit == 4 ? 36 : 0;
    string why = unit == 4 ? "Pier 19G end of Unit 4 (section E-E, PDF 140): 10 bars per bay and 3 outside the exterior girders 'at equal spacing' on the skewed joint; positions along the joint not dimensioned (the partial section PDF 138 dimensions Piers 5G/9G/14G only) - 36 bars counted, not modeled" : "";
    DeckEndDamPoint P(double x, double d) => new(In(x), In(d));
    DeckEndDamBar B(string mark, double listedFt, double listedIn, double side) => new()
    {
        Mark = mark, Size = "#5", Ends = ends, Pin = In(2.5), ListedLength = FI(listedFt, listedIn), FromGirder = In(10), Spacing = In(12), PerBay = 9,
        Overhang = { In(10), In(22) }, SideOffset = side * db, Unplaced = unplaced, Unresolved = why,
    };
    var d2 = B("5d2", 6, 2, 0); d2.HoopWidth = In(11); d2.HoopHeight = In(20); d2.HoopLapSide = In(6); d2.HoopLapTop = In(6); d2.HoopCorner = P(27, 26 - 1.5 - 0.625 - 20);
    var d3 = B("5d3", 8, 0, 0); d3.HoopWidth = In(34); d3.HoopHeight = In(8); d3.HoopLapSide = In(6); d3.HoopLapTop = In(6); d3.HoopCorner = P(3, 26 - 1.5 - 0.625 - 8);
    var d4 = B("5d4", 7, 3, 0); d4.Outside = new() { P(51, 3.5), P(3, 3.5), P(3, 24.5), P(21, 24.5) }; d4.Dims = new() { In(48), In(21), In(18) };
    double xb = 65 - 2 * Math.Sqrt(2) - 24.5;                         // bottom bend of 5d5 (outside face), 2" clear from the 45 deg soffit X + Drop = 65
    var d5 = B("5d5", 5, 5, 0); d5.Outside = new() { P(xb - 18, 24.5), P(xb, 24.5), P(xb + 20.5, 4.0), P(xb + 38.5, 4.0) }; d5.Dims = new() { In(18), In(29), In(18) };
    // User decision 2026-10-11 (option A): 5d4 clashes with the steel are covered by the plan note with Section B-B (PDF 137) - reported as Info, not moved.
    d4.FieldCutNote = "Section B-B, PDF 137: '5d4 bars shall be field cut or spaced as required at expansion joint support boxes'";
    // Any other mark that meets the steel stays a PLAN CONFLICT (bars never moved). Unit 3: its girders have 20" top flanges at the Pier 9G / 14G ends.
    if (unit == 3)
        foreach (var d in new[] { d2, d3, d5 })
            d.ConflictNote = "the partial section (PDF 138) puts the first bar 10\" from each girder CL, but Unit 3's top flanges at Piers 9G / 14G are 20\" wide (10\" each side), " +
                             "so the bar sits on the flange edge and its leg below the slab runs through the flange (Units 1, 2, 4 have 18\" flanges: 11/16\" clear)";
    plan.EndDam.AddRange(new[] { d2, d3, d4, d5 });
    string ends5 = unit == 1 ? "South Abutment (section A-A, PDF 137) and Pier 5G" : unit == 4 ? "Piers 14G and 19G (sections B-B PDF 137, E-E PDF 140)" : "both expansion piers (section B-B, PDF 137)";
    plan.Unresolved.Add($"Ramp G Unit {unit} end dam longitudinal bars 5e* at {ends5}: the bars along the joint are drawn as dots in the sections but only those at hoop corners follow " +
        "from dimensions (the other 5d3-hoop bars, the 5e4 / 5e8 bars in the blockout) - schedule only.");
    plan.Unresolved.Add($"Ramp G Unit {unit} end dam bars 4d6 / 4d7 under the barriers (section C-C, PDF 138): '4-4d6 EQ. SPA.' without the extent along the joint, 4d7 position not dimensioned; " +
        "the reduced end block under the barrier (1'-3 at the bottom) is not modeled - schedule only.");
    if (unit == 1)
        plan.Unresolved.Add("Ramp G Unit 1 South Abutment 5d1 hoops (section A-A, PDF 137, 36 bars 'parallel to longit. 6b1 bars'): spacing along the joint not dimensioned - schedule only.");
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
    // The other Ramp G ends are radial (no clipped lines). CALIBRATED value, not a plan dimension: accepted by the user 2026-10-11 (decision 5, option A).
    if (sp.U == 4) plan.TransverseEndClear = In(27);   // CALIBRATED (Pier 19G slab bar end clear, 27" normal to the end of slab)
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
    if (END_DAM) EndDam(plan, sp.U);
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
    foreach (var d in cm.Diagnostics.Where(d => d.Message.StartsWith("Deck reinforc") || d.Message.StartsWith("UNRESOLVED") || d.Message.StartsWith("Deck end dam"))) Print($"   {d.Severity}: {d.Message}");
}
Doc.Save();
