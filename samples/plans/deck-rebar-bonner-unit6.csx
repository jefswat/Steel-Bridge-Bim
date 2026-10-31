// NCDOT B-2500 Bonner Unit 6 - DECK REINFORCING (plan-driven, DeckReinforcementDefinition.Plan) + the superstructure bill of material (p.455) and the
// concrete parapet bill (p.451) as the unit's bar list (parapet-rebar-bonner-unit6.csx then places S1 / B1 / B6 / B7). Called by build-bonner-unit6.csx AFTER joints-parapet-bonner-unit6.csx (deck ends); re-runnable.
//   sbm run samples\plans\deck-rebar-bonner-unit6.csx "<model.sbm>"
// Plan data (p. = PDF page):
//   p.409-414 (S4-17..22) PLAN OF SPANS UNIT 6: "1924-#5A101 @ 6\" MAX. CTS. (TOP OF SLAB)", "2199-#5A101 @ 5 1/4\" MAX. CTS. (BOT. OF SLAB)", radial
//          (90 00'00" typ.); bottom "B" bars 12-#5 @ 8 1/2" per bay + 3 eq. spa. per overhang (44 lines); "22-#5B107 & 22-#5B108 ALTERNATE BARS" at the unit
//          ends; splices #5 to #5 2'-7" MIN.; top "B" bars #4 @ 1'-6" with #6 B101 / B102 / B103 over the bents ("partial plan of top B bars").
//   p.404 / p.406 (S4-12 / 14) typical sections: 3" clr top, 2 1/2" clr bottom; overhang bottom bars 3 1/2" from the edge, 3 eq. spa. to 2'-0" from CL girder;
//          bay bottom bars 1'-10 1/4" from the CL girders (12 @ 8 1/2" = 7'-9 1/2" in the 11'-6" bay); top bars 29 @ 1'-6" = 42'-0" (3 1/2" from each edge).
//   p.455 (S4-62) BILL OF MATERIAL UNIT 6 (superstructure, stainless): docs/plan-data/bonner-b2500/fib-unit6/superstructure-bill-of-material-unit6.csv.
//   p.451 (S4-58) BILL OF MATERIAL - REINFORCING STEEL FOR CONCRETE PARAPET (epoxy coated), Unit 6 column.
// Derived: A101 42'-1" in a 42'-7" deck = 3" clear at the deck edges (used as the side cover). One B line = B107 (27'-0") + 18 x B109 (53'-0") + B108
// (30'-0") lapped 2'-7" = 961.9 ft = the deck length less the end covers (962.2 ft deck): every line starts with B107 at one end and closes with B108
// at the other, alternate lines reversed (44 x B107, 44 x B108, 792 x B109 = the bill).
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Bonner Unit 6");
var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
var inv = CultureInfo.InvariantCulture;

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "bonner-b2500", "fib-unit6");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
List<string[]> Csv(string name)
{
    var rows = new List<string[]>();
    foreach (var line in File.ReadAllLines(Path.Combine(dataDir, name)).Skip(1))
    {
        if (line.Trim() == "") continue;
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = false; } else cur.Append(c); }
            else if (c == '"') q = true;
            else if (c == ',') { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cells.Add(cur.ToString());
        rows.Add(cells.ToArray());
    }
    return rows;
}

var r = br.Deck.Reinforcement;
r.Enabled = true;
r.EpoxyCoated = false;                         // stainless deck reinforcing (p.404, p.455 "stainless reinforcing")
r.TopCover = In(3); r.BottomCover = In(2.5);  // p.404 / p.406
r.SideCover = In(3);                           // derived: A101 42'-1" in the 42'-7" deck
r.NegativeMomentSteel = false;
var plan = new DeckPlanReinforcement();
r.Plan = plan;

// ---- transverse A101: equally spaced radial lines along the left (outside) deck edge between the deck ends; the first line position is not dimensioned
var s29 = br.Supports.First(s => s.Name == "Bent 29"); var s35 = br.Supports.First(s => s.Name == "Bent 35");
double staB = s29.Station - s29.DeckEndOffset, staE = s35.Station + s35.DeckEndOffset;   // deck faces (DeckEndOffset < 0 = inside the span)
double EdgeLen(double off)
{
    double L = 0; int n = 2000; var prev = al.PointAt(staB, off);
    for (int i = 1; i <= n; i++) { var p = al.PointAt(staB + (staE - staB) * i / n, off); L += SBM.Core.Geometry.Vec2.Distance(prev, p); prev = p; }
    return L;
}
double edgeL = EdgeLen(br.Deck.LeftEdgeOffset);
DeckTransverseZone A(string name, DeckBarLayer layer, int count, double spacing)
{
    double start = (edgeL - (count - 1) * spacing) / 2;
    if (start < 0) throw new InvalidOperationException($"{name}: {count} lines @ {spacing / In(1):0.##} in do not fit {edgeL / Ft(1):0.00} ft");
    return new DeckTransverseZone
    {
        Name = name, Layer = layer, Size = "#5", Reference = DeckBarSide.Left, StartDistance = start, Spacing = spacing, Count = count,
        Pieces = { new DeckLinePiece { Mark = "A101", Length = 0 } },
    };
}
plan.Transverse.Add(A("A101 top", DeckBarLayer.Top, 1924, In(6)));
plan.Transverse.Add(A("A101 bottom", DeckBarLayer.Bottom, 2199, In(5.25)));

// ---- bottom longitudinal B107 / B109 / B108: 44 lines (overhang 3 1/2" + 3 eq. spa. to 2'-0" from CL girder; bays 1'-10 1/4" from the CL girders + 11 @ 8 1/2")
var pos = new List<double>();
double edge = br.Deck.LeftEdgeOffset;                                  // offsets from -L-, converted to distances from the left edge
double[] gl = { -F(17, 3), -F(5, 9), F(5, 9), F(17, 3) };
for (int k = 0; k < 4; k++) pos.Add(In(3.5) + k * In(7));                               // left overhang: 3 1/2", 10 1/2", 1'-5 1/2", 2'-0 1/2" from the edge
for (int b = 0; b < 3; b++) for (int k = 0; k < 12; k++) pos.Add(gl[b] + F(1, 10.25) + k * In(8.5) - edge);
for (int k = 3; k >= 0; k--) pos.Add(-edge - In(3.5) - k * In(7) - edge);             // right overhang (mirror)
pos = pos.Select(v => Math.Round(v, 9)).OrderBy(v => v).ToList();
if (pos.Count != 44) throw new InvalidOperationException($"{pos.Count} bottom B lines");
plan.Longitudinal.Add(new DeckLongZone
{
    Name = "bottom B107 / B109 / B108", Left = DeckLongRef.Edge(DeckBarSide.Left), Right = DeckLongRef.Edge(DeckBarSide.Right),
    Positions = pos, PositionLayers = pos.Select(_ => DeckBarLayer.Bottom).ToList(), Size = "#5",
    Mark = "B109", StockLength = F(53), Lap = F(2, 7), StartMark = "B107", StartLength = F(27), ClosureMark = "B108", AlternateEnds = true,
});

// ---- the unit's bar list: superstructure bill p.455 + parapet bill p.451 (Unit 6 column)
string Loc(string mark) => mark[0] switch
{
    'A' => "Slab, transverse (p.409-414)",
    'B' => "Slab, longitudinal (p.409-414)",
    'H' => "Temporary access opening repair (p.445 / p.409)",
    _ => "Bent / expansion joint diaphragms (p.405-408)",
};
double Len(string s)
{
    s = s.Replace("\"", "").Trim();
    var m = System.Text.RegularExpressions.Regex.Match(s, @"^(\d+)'-(\d+)(?: (\d+)/(\d+))?$");
    if (!m.Success) throw new FormatException(s);
    double inch = double.Parse(m.Groups[2].Value, inv) + (m.Groups[3].Success ? double.Parse(m.Groups[3].Value, inv) / double.Parse(m.Groups[4].Value, inv) : 0);
    return Ft(double.Parse(m.Groups[1].Value, inv)) + In(inch);
}
foreach (var row in Csv("superstructure-bill-of-material-unit6.csv"))
    plan.PlanBarList.Add(new DeckPlanBarLine { Mark = row[0], Size = row[2], Location = Loc(row[0]), Count = int.Parse(row[1], inv), Length = Len(row[4]), WeightLb = double.Parse(row[5], inv) });
// parapet bill, Unit 6 column (p.451; epoxy coated): mark, count, size, length, weight of the unit = total weight x unit count / total count
var parapet = new (string mark, int n, string size, string len, int total, int weight)[]
{
    ("B1", 384, "#5", "25'-7\"", 1024, 27324), ("B6", 160, "#5", "28'-0\"", 160, 4673), ("B7", 16, "#5", "25'-5\"", 16, 424), ("B8", 6, "#5", "27'-6\"", 6, 172),
    ("B9", 6, "#5", "27'-5\"", 6, 172), ("B10", 2, "#5", "26'-7\"", 2, 55), ("B11", 2, "#5", "26'-6\"", 2, 55),
    ("E1", 32, "#7", "4'-6\"", 80, 736), ("E2", 8, "#7", "4'-1\"", 40, 334), ("F1", 24, "#6", "3'-5\"", 72, 369), ("S1", 1924, "#5", "7'-0\"", 5122, 37396), ("S2", 22, "#6", "7'-3\"", 70, 762),
};
foreach (var p in parapet)
    plan.PlanBarList.Add(new DeckPlanBarLine { Mark = p.mark, Size = p.size, Location = $"Concrete parapet / end posts, epoxy coated (p.451 mark {p.mark}; p.448-451)", Count = p.n, Length = Len(p.len), WeightLb = Math.Round((double)p.weight * p.n / p.total) });

plan.Unresolved.Add(UnresolvedFlag.Text("p.409-414", "first / last A101 line from the deck ends", "the plans give the counts and the maximum spacings (1924 @ 6\" top, 2199 @ 5 1/4\" bottom) but no distance of the first line from the deck end",
    "Lines equally spaced at the maximum spacing, centred between the deck ends along the left edge."));
// ---- top longitudinal B bars (p.409 PARTIAL PLAN OF TOP "B" BARS at bent 30, "typ." at bents 30-34; p.406 typical section "29-#6B101 OR #6B102 & 56-#6B103
// @ 6\" CTS. = 42'-0\"", 3 1/2" from each edge): 29 lines @ 1'-6", alternate lines mirrored ("2'-2\" STAGGER"):
//   line A (first, left): B102 -55'-5" .. -16'-9", B101 -19'-11" .. +17'-9", B102 +14'-7" .. +53'-3"   (laps #6 to #6 3'-2")
//   line B:               B102 -53'-3" .. -14'-7", B101 -17'-9" .. +19'-11", B102 +16'-9" .. +55'-5"
//   B103 between the lines (3 spa @ 6\"): 2 per gap = 56, -24'-2" .. +24'-2"; spans 31-34: 2 x #4 B104 (29'-4") lapped 2'-2" (#4 to #4 / #4 to #6) close the gap exactly
//   (160'-10" - 53'-3" - 55'-5" + 2 x 2'-2" = 2 x 29'-4" - 2'-2"). Distances measured along -L- (stations; radial lines). Top bars lie under the top A101 (3" clear + #5).
var tb = new Dictionary<string, PlanBarSet>();
PlanBarSet TB(string mark, string size, double len) => tb.TryGetValue(mark, out var x) ? x : tb[mark] = new PlanBarSet
{
    Mark = mark, Size = size, Element = "deck", ListedLength = len, Location = "Slab, longitudinal top (p.409 partial plan of top B bars)", Source = "p.409, p.406",
    Note = "Lines parallel to -L-, lengths along the stations.",
};
double dbOf(string size) => size == "#6" ? 0.75 : 0.5;
SBM.Core.Geometry.Vec3 Pt(double sta, double off, double db) => al.PointAt(sta, off).WithZ(br.Deck.FinishedElevation(al, sta, off) - In(3 + 0.625 + db / 2));
void Line(string mark, string size, double off, double s0, double s1)
{
    var set = TB(mark, size, s1 - s0);
    var pts = new List<SBM.Core.Geometry.Vec3>();
    int n = Math.Max(2, (int)Math.Ceiling((s1 - s0) / Ft(5)));
    for (int q = 0; q <= n; q++) pts.Add(Pt(s0 + (s1 - s0) * q / n, off, dbOf(size)));
    set.Add(pts);
}
double left = br.Deck.LeftEdgeOffset;
var bentSta = br.Supports.ToDictionary(x => x.Name, x => x.Station);
for (int bent = 30; bent <= 34; bent++)
{
    double sb = bentSta[$"Bent {bent}"];
    for (int k = 0; k < 29; k++)
    {
        double off = left + In(3.5) + k * F(1, 6);
        bool A = k % 2 == 0;
        Line("B102", "#6", off, sb - (A ? F(55, 5) : F(53, 3)), sb - (A ? F(16, 9) : F(14, 7)));
        Line("B101", "#6", off, sb - (A ? F(19, 11) : F(17, 9)), sb + (A ? F(17, 9) : F(19, 11)));
        Line("B102", "#6", off, sb + (A ? F(14, 7) : F(16, 9)), sb + (A ? F(53, 3) : F(55, 5)));
        if (k < 28) foreach (double d in new[] { 6.0, 12.0 }) Line("B103", "#6", off + In(d), sb - F(24, 2), sb + F(24, 2));
        if (bent < 34)
        {
            double g0 = sb + (A ? F(53, 3) : F(55, 5)) - F(2, 2);
            Line("B104", "#4", off, g0, g0 + F(29, 4));
            Line("B104", "#4", off, g0 + F(29, 4) - F(2, 2), g0 + 2 * F(29, 4) - F(2, 2));
        }
    }
}
// spans 30 / 35 (p.409 / p.414): per line B104 (29'-4") or B106 (31'-6") at the unit end ("15-#4B104 & 14-#4B106 ALTERNATE BARS") + 2 x B105 (41'-0"); the plans
// give the laps only as minimums ("2'-2\" MIN. (#4 TO #4)", "(#4 TO #6)"). READING (not an invention, flagged dimension-only): bars laid from the deck end (3" end cover
// = the A101 side cover) with the 2'-2" minimum laps between the #4 bars; whatever remains is the lap onto the #6 B102 of the first / last interior bent.
// Which lines take the longer B106: the lines whose gap is 2'-2" longer (span 30: B lines, 14; span 35: A lines, 15) - this reproduces 15 / 14 and 14 / 15.
var sEnd29 = br.Supports.First(x => x.Name == "Bent 29"); var sEnd35 = br.Supports.First(x => x.Name == "Bent 35");
double face29 = sEnd29.Station - sEnd29.DeckEndOffset + In(3), face35 = sEnd35.Station + sEnd35.DeckEndOffset - In(3);
var endLaps = new List<double>();
for (int k = 0; k < 29; k++)
{
    double off = left + In(3.5) + k * F(1, 6);
    bool A = k % 2 == 0;
    // span 30: from the bent 29 deck end toward bent 30
    {
        bool longBar = !A; string m0 = longBar ? "B106" : "B104"; double l0 = longBar ? F(31, 6) : F(29, 4);
        double x0 = face29, x1 = x0 + l0; Line(m0, "#4", off, x0, x1);
        double y0 = x1 - F(2, 2), y1 = y0 + F(41); Line("B105", "#4", off, y0, y1);
        double z0 = y1 - F(2, 2), z1 = z0 + F(41); Line("B105", "#4", off, z0, z1);
        double b102 = bentSta["Bent 30"] - (A ? F(55, 5) : F(53, 3));
        endLaps.Add(z1 - b102);
    }
    // span 35: from the bent 35 deck end back toward bent 34
    {
        bool longBar = A; string m0 = longBar ? "B106" : "B104"; double l0 = longBar ? F(31, 6) : F(29, 4);
        double x1 = face35, x0 = x1 - l0; Line(m0, "#4", off, x0, x1);
        double y1 = x0 + F(2, 2), y0 = y1 - F(41); Line("B105", "#4", off, y0, y1);
        double z1 = y0 + F(2, 2), z0 = z1 - F(41); Line("B105", "#4", off, z0, z1);
        double b102 = bentSta["Bent 34"] + (A ? F(53, 3) : F(55, 5));
        endLaps.Add(b102 - z0);
    }
}
string lapTxt = $"lap onto the #6 B102: span 30 {endLaps.Where((_, i) => i % 2 == 0).Min() / In(1):0.##}-{endLaps.Where((_, i) => i % 2 == 0).Max() / In(1):0.##} in, span 35 {endLaps.Where((_, i) => i % 2 == 1).Min() / In(1):0.##}-{endLaps.Where((_, i) => i % 2 == 1).Max() / In(1):0.##} in";
string endFlag = UnresolvedFlag.Text("p.409, p.414", "top #4 B104 / B105 / B106 in the end spans 30 and 35", "the plans give the bar lengths and only MINIMUM laps (2'-2\"), not the bar positions",
    $"Reading: laid from the deck end (3\" cover) with 2'-2\" laps between the #4 bars, the rest is the lap onto the #6 B102 ({lapTxt}).");
foreach (var mk in new[] { "B104", "B105", "B106" }) tb[mk].Note = "End spans: " + endFlag;
tb["B104"].Note = "Spans 31-34: 2 x 29'-4\" lapped 2'-2\" close the gap exactly. " + tb["B104"].Note;
Print("End-span top #4: " + lapTxt + " (2'-2\" min.)");
foreach (var (m, n, w) in new[] { ("B101", 145, 8203), ("B102", 290, 16842), ("B103", 280, 20327), ("B104", 261, 5114), ("B105", 116, 3177), ("B106", 29, 610) }) { tb[m].PlanCount = n; tb[m].PlanWeightLb = w; }
plan.Unresolved.Add(endFlag);
if (endLaps.Min() < F(2, 2) - 1e-6)
    plan.Unresolved.Add($"CONFLICT (p.409 / p.414): the printed #4 bar lengths with 2'-2\" minimum #4 laps and a 3\" end cover leave {endLaps.Min() / In(1):0.##} in. of lap onto the #6 B102, less than the 2'-2\" minimum (#4 to #6) - modeled as read, not adjusted.");
foreach (var m in tb.Values) { m.ListedLength = plan.PlanBarList.First(l => l.Mark == m.Mark).Length; }
r.PlanBarSets.RemoveAll(x => x.Element == "deck");
r.PlanBarSets.AddRange(tb.Values);
plan.PlanBarList.RemoveAll(l => tb.ContainsKey(l.Mark));
plan.Unresolved.Add(UnresolvedFlag.Text("p.405-408, p.445", "diaphragm bars (E, G, K, S, U) and access opening repair bars (H)", "bar shapes and positions are drawn in the sections but not transcribed in this pass (GAP)",
    "Schedule only at the bill counts and lengths."));
plan.Unresolved.Add(UnresolvedFlag.Text("p.448-451", "parapet and end post bars (B1-B11, E1, E2, F1, S1, S2)", "set here as schedule-only rows; parapet-rebar-bonner-unit6.csx places the ones the sheets dimension",
    "Schedule only; Unit 6 weights = the printed totals x Unit 6 count / total count."));
Print($"Bonner Unit 6 deck reinforcing: A101 top 1924 / bottom 2199 along {edgeL / Ft(1):0.00} ft of the left edge; 44 bottom B lines; bar list {plan.PlanBarList.Count} rows");
