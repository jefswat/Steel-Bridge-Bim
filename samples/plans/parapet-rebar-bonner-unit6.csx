// NCDOT B-2500 Bonner Unit 6 - CONCRETE PARAPET BARS as explicit deck plan bar sets (Element "barrier"). Called by build-bonner-unit6.csx AFTER
// deck-rebar-bonner-unit6.csx (it takes the placed marks out of the schedule-only bar list rows); re-runnable.
//   sbm run samples\plans\parapet-rebar-bonner-unit6.csx "<model.sbm>"
// Plan data (p. = PDF page):
//   p.448 (S4-55) CONCRETE PARAPET PLAN UNIT 6 ("all reinforcing steel is spaced along the outside edge of deck"; dimensions typical for each side):
//          #5 S1 per span: span 30 6'-6" + 6" + 153 spa @ 1'-0" + 6" + 4" (156); spans 31-34 4" + 7" + 159 spa + 7" + 4" (162); span 35 4" + 6" + 155 spa
//          + 6 9/16" (left) / 5 7/16" (right) + 4'-6" (158) = 1924 per unit. "8-#5 B1 (typ. ea. 26'-0" section)", 8-#5 B6 in the 28'-5" sections at the bents,
//          8-#5 B7 in the first section (bent 29), "6-#5 B8 and 2-#5 B10" (left) / "6-#5 B9 and 2-#5 B11" (right) in the last section (bent 35).
//   p.451 (S4-58) Section B-B: parapet 1'-2", 2" clear to S1, "PROJECTION #5S1 2'-4\"" above the deck, 4-#5 "B" bars each face at 2", 8", 9" spacings from the
//          deck; bar type 1 = 10" x 2'-9" legs + 8" foot (S1 7'-0"); bill (Unit 6 column, epoxy coated): B1 384 / 25'-7", B6 160 / 28'-0", B7 16 / 25'-5", S1 1924 / 7'-0".
// Placed: S1, B1, B6, B7 and the end post bars E1, E2, F1, S2. Schedule only (reason in the flags): B8-B11 (which bars of the 8 are the short ones is not shown).
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var br = Doc.Project.Bridges.First(b => b.Name == "Bonner Unit 6");
var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double E = F(21, 3.5);
var deck = br.Deck;
var sup = br.Supports.ToDictionary(s => s.Name, s => s.Station);
var plan = deck.Reinforcement.Plan ?? throw new InvalidOperationException("run deck-rebar-bonner-unit6.csx first");

// ---- edge distance (from a bent control line, along the outside edge of deck) -> station, per span and side
Func<double, double> EdgeMap(int startBent, int side)
{
    double s0 = sup[$"Bent {startBent}"], s1 = sup[$"Bent {startBent + 1}"];
    var sta = new List<double>(); var len = new List<double>(); Vec2 prev = default; double acc = 0;
    for (double s = s0; ; s = Math.Min(s + Ft(0.1), s1))
    {
        var (p, ang) = al.EvaluateAtDistance(al.StationToDistance(s));
        var e = p + Vec2.FromAngle(ang).Right * (side * E);
        if (sta.Count > 0) acc += (e - prev).Length;
        sta.Add(s); len.Add(acc); prev = e;
        if (s >= s1 - 1e-9) break;
    }
    return d =>
    {
        int i = len.FindIndex(x => x >= d);
        if (i < 0) return sta[^1] + (d - len[^1]);
        if (i == 0) return sta[0] + d;
        return sta[i - 1] + (sta[i] - sta[i - 1]) * (d - len[i - 1]) / (len[i] - len[i - 1]);
    };
}
// point at station, R inward from the deck edge, h above the deck top AT THE EDGE (as the barrier section is placed)
Vec3 P(int side, double sta, double rIn, double hIn)
{
    double off = side * (E - In(rIn));
    var p = al.PointAt(sta, off);
    return p.WithZ(deck.FinishedElevation(al, sta, side * E) + In(hIn));
}

// ---- S1: type 1 = 10" top, 2'-9" legs, 8" foot inward on the traffic-face leg; top 2'-4" above the deck, 2" clear to the faces (#5, db 5/8")
const double db = 0.625;
double rO = 1.5 + 2 + db / 2, rI = 15.5 - 2 - db / 2, hT = 28 - db / 2, hB = 28 - 33, hF = hB + db / 2, rTip = rI - db / 2 + 8;
var s1 = new PlanBarSet
{
    Mark = "S1", Size = "#5", Element = "barrier", Coating = "epoxy coated", Shape = "1", Dims = new[] { In(10), In(33), In(33), In(8) }, ListedLength = F(7),
    Location = "Concrete parapet stirrups (p.448 / p.451)", PlanCount = 1924, PlanWeightLb = Math.Round(37396.0 * 1924 / 5122), Source = "p.448, p.451 Section B-B / bar type 1",
    Note = "Legs 2'-9\" out-to-out: 5\" into the deck below the parapet base (derived: 2'-9\" - 2'-4\" projection).",
};
(int start, double first, (int n, double s)[] g)[] S1Chain(int side) => new[]
{
    (29, 78.0, new (int, double)[] { (1, 6), (153, 12) }),
    (30, 4.0, new (int, double)[] { (1, 7), (159, 12), (1, 7) }), (31, 4.0, new (int, double)[] { (1, 7), (159, 12), (1, 7) }),
    (32, 4.0, new (int, double)[] { (1, 7), (159, 12), (1, 7) }), (33, 4.0, new (int, double)[] { (1, 7), (159, 12), (1, 7) }),
    (34, 4.0, new (int, double)[] { (1, 6), (155, 12), (1, side < 0 ? 6.5625 : 5.4375) }),
};
// span 30 ends "153 spa + 6\" + 4\"": the 6" is the last space (1 + 1 + 153 + 1 = 156)
int nS1 = 0;
foreach (int side in new[] { -1, 1 })
    foreach (var (start, first, g) in S1Chain(side))
    {
        var map = EdgeMap(start, side);
        var ds = new List<double> { first };
        foreach (var (n, sp) in g) for (int i = 0; i < n; i++) ds.Add(ds[^1] + sp);
        if (start == 29) ds.Add(ds[^1] + 6);
        foreach (double d in ds)
        {
            double sta = map(In(d));
            s1.Add(new[] { P(side, sta, rO, hB), P(side, sta, rO, hT), P(side, sta, rI, hT), P(side, sta, rI, hF), P(side, sta, rTip, hF) });
            nS1++;
        }
    }

// ---- B bars: 4 each face, inside the S1 legs, heights 2", 10", 19" (p.451 Section B-B) and the top bar in the S1 top corner (derived from the S1 position)
double rBo = rO + db, rBi = rI - db;
double[] hB4 = { 2, 10, 19, hT - db };
var bSets = new Dictionary<string, PlanBarSet>();
PlanBarSet BSet(string mark, double len, int planN, double planLb) => bSets.TryGetValue(mark, out var x) ? x : bSets[mark] = new PlanBarSet
{
    Mark = mark, Size = "#5", Element = "barrier", Coating = "epoxy coated", ListedLength = len, Location = "Concrete parapet longitudinal (p.448 / p.451)",
    PlanCount = planN, PlanWeightLb = planLb, Source = "p.448 parapet plan, p.451 Section B-B and bill",
    Note = "Centred in the joint section (the bar list length leaves 2 1/4\" - 2 1/2\" to the joints); the top bar sits in the S1 top corner (derived, not dimensioned).",
};
// sections per span along the edge: (from, to) in inches from the start bent; span 30 starts at the deck face 2'-7 1/2" (= along the edge, tangent span)
double Sp(int start, int side) => start == 34 ? (side < 0 ? 1930.5625 : 1929.4375) : 1930;
foreach (int side in new[] { -1, 1 })
    for (int start = 29; start <= 34; start++)
    {
        var map = EdgeMap(start, side);
        var cuts = new List<double> { 0, 341, 653, 965, 1277, 1589, Sp(start, side) };
        for (int k = 0; k < 6; k++)
        {
            double a = cuts[k], b = cuts[k + 1];
            string mark; double len;
            if (start == 29 && k == 0) { a = 31.5; mark = "B7"; len = 305; }
            else if (start == 34 && k == 5) continue;                           // B8-B11: schedule only
            else if (k == 0 || k == 5) { mark = "B6"; len = 336; }
            else { mark = "B1"; len = 307; }
            var set = BSet(mark, In(len), mark == "B1" ? 384 : mark == "B6" ? 160 : 16, mark == "B1" ? 10246 : mark == "B6" ? 4673 : 424);
            double m = (a + b) / 2, d0 = m - len / 2, d1 = m + len / 2;
            foreach (double r in new[] { rBo, rBi })
                foreach (double h in hB4)
                {
                    var pts = new List<Vec3>();
                    int n = Math.Max(2, (int)Math.Ceiling((d1 - d0) / 60.0));
                    for (int q = 0; q <= n; q++) pts.Add(P(side, map(In(d0 + (d1 - d0) * q / n)), r, h));
                    set.Add(pts);
                }
        }
    }

// ---- END POST bars (p.450 bent 29, p.451 bent 35; bill p.451): u = distance from the end post's INNER end (3'-9" from the deck face) toward the joint.
//   E bars #7 vertical, each face: bent 29 "5-#7E1 @ 9 1/2\" CTS." from 2 1/2"; bent 35 2 1/2" + 9" + 9" + 11" + 11", the two at the joint = E2 ("7E2").
//   E1 4'-6": top 2" below the 4'-8" post top, bottom on the deck (derived); E2 4'-1": top 2" below the top, bottom 5" above the deck (derived, shorter at the
//   Transflex parapet blockout). Faces: inside the S2 legs ("2 3/4\" CL. to #6S2").
//   F1 #6 3'-5" horizontal each face, "3-#6F1 (EA. FACE)" at 3" / 10" / 10" from the post top, centred in the 3'-9" post (2" each end, derived).
//   S2 #6 (type 2: 9" top, 2'-9" legs, 1'-0" foot) like S1: bent 29 "5-#6S2 @ 9 1/2\" CTS." at the E stations; bent 35 "6-#6S2 @ 4 1/2\" CTS." first ~3 1/4" from
//   the inner end (scaled, p.451); projection not printed -> S1's 2'-4" (dimension-only).
double rEo = 1.5 + 2.75 + 0.75 + 0.4375, rEi = 15.5 - 2.75 - 0.75 - 0.4375, rFo = rEo + 0.8125, rFi = rEi - 0.8125;
var e1 = new PlanBarSet { Mark = "E1", Size = "#7", Element = "barrier", Coating = "epoxy coated", ListedLength = F(4, 6), PlanCount = 32, PlanWeightLb = 294, Location = "End post verticals (p.450 / p.451)", Source = "p.450, p.451" };
var e2 = new PlanBarSet { Mark = "E2", Size = "#7", Element = "barrier", Coating = "epoxy coated", ListedLength = F(4, 1), PlanCount = 8, PlanWeightLb = 67, Location = "End post verticals at the Transflex joint (p.451)", Source = "p.451",
    Unresolved = U("p.451", "E2 bottom end", "the 4'-1\" bar is 5\" shorter than E1 and stands at the Transflex blockout; its bottom is not dimensioned", "Bottom 5\" above the deck, top 2\" below the post top (derived).") };
var f1 = new PlanBarSet { Mark = "F1", Size = "#6", Element = "barrier", Coating = "epoxy coated", ListedLength = F(3, 5), PlanCount = 24, PlanWeightLb = 123, Location = "End post horizontals (p.450 / p.451)", Source = "p.450, p.451" };
var s2 = new PlanBarSet { Mark = "S2", Size = "#6", Element = "barrier", Coating = "epoxy coated", Shape = "2", Dims = new[] { In(9), In(33), In(33), In(12) }, ListedLength = F(7, 3), PlanCount = 22, PlanWeightLb = 240,
    Location = "End post stirrups (p.450 / p.451)", Source = "p.450, p.451",
    Unresolved = U("p.450 / p.451", "S2 height and bent 35 S2 positions", "the S2 projection is not printed and the bent 35 group (6 @ 4 1/2\") is not tied to a dimension from the post end", "Projection = S1's 2'-4\"; bent 35 first S2 3 1/4\" from the inner end (scaled).") };
foreach (int side in new[] { -1, 1 })
    foreach (bool at29 in new[] { true, false })
    {
        var map = EdgeMap(at29 ? 29 : 34, side);
        double inner = at29 ? 31.5 + 45 : Sp(34, side) - 2.125 - 45;     // inner end of the post along the edge from the start bent
        double D(double u) => at29 ? inner - u : inner + u;
        var uE = at29 ? new[] { 2.5, 12, 21.5, 31, 40.5 } : new[] { 2.5, 11.5, 20.5, 31.5, 42.5 };
        for (int i = 0; i < 5; i++)
        {
            bool isE2 = !at29 && i >= 3;
            double sta = map(In(D(uE[i])));
            foreach (double r in new[] { rEo, rEi })
                (isE2 ? e2 : e1).Add(new[] { P(side, sta, r, isE2 ? 5 : 0), P(side, sta, r, 54) });
        }
        foreach (double h in new[] { 53.0, 43, 33 })
            foreach (double r in new[] { rFo, rFi })
            {
                var pts = new List<Vec3>();
                for (int q = 0; q <= 4; q++) pts.Add(P(side, map(In(D(2 + 41.0 * q / 4))), r, h));
                f1.Add(pts);
            }
        var uS = at29 ? uE.ToList() : Enumerable.Range(0, 6).Select(k => 3.25 + 4.5 * k).ToList();
        double r2o = 1.5 + 2.75 + 0.375, r2i = r2o + 9 - 0.75, h2 = 28 - 0.375, tip2 = r2i - 0.375 + 12;
        foreach (double u in uS)
        {
            double sta = map(In(D(u)));
            s2.Add(new[] { P(side, sta, r2o, -5), P(side, sta, r2o, h2), P(side, sta, r2i, h2), P(side, sta, r2i, -5 + 0.375), P(side, sta, tip2, -5 + 0.375) });
        }
    }

var sets = deck.Reinforcement.PlanBarSets;
sets.RemoveAll(x => x.Element == "barrier");
sets.Add(s1);
sets.AddRange(bSets.Values);
sets.AddRange(new[] { e1, e2, f1, s2 });
// the placed marks leave the schedule-only bar-list rows
plan.PlanBarList.RemoveAll(l => new[] { "S1", "B1", "B6", "B7", "E1", "E2", "F1", "S2" }.Contains(l.Mark));
plan.Unresolved.RemoveAll(u => u.Contains("parapet and end post bars"));
plan.Unresolved.Add(U("p.448 / p.451", "parapet bars B8 / B9 (6 bars) and B10 / B11 (2 bars) in the last section at bent 35", "the plan lists 6 + 2 bars of two lengths per side but does not show which of the 8 positions take the shorter B10 / B11",
    "Schedule only at the bill counts and lengths."));
Print($"Bonner Unit 6 parapet bars: S1 {nS1} (plan 1924), B1 {bSets["B1"].Bars.Count} (384), B6 {bSets["B6"].Bars.Count} (160), B7 {bSets["B7"].Bars.Count} (16), E1 {e1.Bars.Count} (32), E2 {e2.Bars.Count} (8), F1 {f1.Bars.Count} (24), S2 {s2.Bars.Count} (22)");
