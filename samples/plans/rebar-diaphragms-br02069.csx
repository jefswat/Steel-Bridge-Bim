// MnDOT Br. 02069 - END DIAPHRAGM reinforcing S405E .. S617S (p.20 plan / elevation, p.21 SECTION A-A between beams / B-B at beams, bill p.21:
// counts are for BOTH abutments) placed as PlanBarSet data (superstructure, support frame: W = S along the CL brg = alignment offset, U = x from CL brg
// toward the span). Called by build-br02069.csx (re-runnable: replaces the diaphragm sets / bill lines).
//   sbm run samples\plans\rebar-diaphragms-br02069.csx "samples\plans\Br02069 TH65.sbm"
// Read from the sheets (vector linework of p.21 A-A / B-B, 1 1/2" = 1'-0" plotted at half size = 3 pt per inch; dims govern where printed):
//  * Along the diaphragm (p.20 plan, vector): one set every 1'-0" from WL -23'-0" to +17'-0" (41 per diaphragm): "4 SPS. @ 1'-0" (TYP. AT BEAMS)" =
//    beam CL and +-1'-0" (3 sets of S510E, S512E, S514E, S615M), "2 SPS. @ 1'-0" (TYP. BTWN. BEAMS)" = the 3 bay sets (S510E, S511E, S513E, S615M,
//    S616M, S617S), "1 SET" at each end 6" from the diaphragm end (vector: the end sets continue the 1'-0" spacing). Bill: S510E / S615M 82 = 2 x 41,
//    S511E / S513E / S616M / S617S 40 = 2 x 20, S512E / S514E 42 = 2 x 21.
//  * Section (x from CL brg; z from the deck top unless noted): 2" clr typ. S513E / S514E inverted U at the top, base 2" clr under the paving bracket
//    (note 2), legs 1'-0" down, base 2'-8" / 1'-6" from 2" clr at the back face; S511E (bays) U base 2" clr above the diaphragm bottom (= bottom of the
//    beams), base 2'-8", back leg 1'-10", front leg 2'-10" into the deck; S512E (at beams) U base 1'-6", legs 1'-8"; S510E U in the back part, base 11",
//    legs 2'-9", nested inside the S511E back leg, base under the lowest B.F. bar (4" above the back-part bottom: "S609E B.F. ... 4"").
//    S609E 12 per diaphragm (40'-8" = 41'-0" - 2 x 2"): 5 B.F. (2 eq. sps from the top row under S513E to the S511E-base row, 2 eq. sps to 4" above the
//    back-part bottom), 4 inside the S510E front leg (rows 1, 3, 4, 5), 3 inside the S512E / S514E front legs (rows 1, 3 and one 8 7/8" above the beam
//    bottom - vector, not dimensioned). S607E 2 / S608E 1 per bay on the front face (rows 1, 2 / row 3), centred in the bay; S405E 1 in the 1'-0" x 1'-0"
//    fillet (2" clr); S506E 2 per beam through the web holes (Fig. 5-397.560 30MH: 7 1/2" from the beam end = on the CL brg, 1'-1 1/4" and 1'-4 3/4"
//    above the beam bottom), 4'-0" centred on the beam.
//    S615M L: vertical leg (30" = listed - 3'-6") at x -1'-8" (vector), horizontal 3'-6" in the deck under the top mat (note 5: tie to the top mat;
//    vector z -4.6"); S616M (bays): 3'-10" in the deck + 12:12 leg down-back (24" = listed - 3'-10"), kink at x +1'-7.6" (vector); S617S stainless
//    approach panel tie: drawn to scale on A-A (45-deg leg from the front part up through the back face, 180-deg hook 4 1/2" clr under the top in the
//    approach panel) - vector coordinates (the sum = the listed 4'-2").
//  Row profiles follow the deck top (DeckTop rows) or the bottom of the beams (straight between the bottom-flange edges, level beyond the fascia, as the
//  diaphragm bottom; plan seat + bearing height) - longitudinal bars are polylines with a vertex at every flange edge (bent to the crown: DERIVED).
using System.IO;
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Br02069 TH65 SB");
var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
br.PlanBars ??= new PlanBarsDefinition();
var pbd = br.PlanBars;
pbd.Sets.RemoveAll(s => s.Superstructure && s.Location.Contains("DIAPH"));
pbd.BarList.RemoveAll(l => l.Superstructure && l.Location.Contains("DIAPH"));

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "br02069-th65");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/br02069-th65 not found");
}
string dir = DataDir();
List<string[]> Csv(string name)
{
    var res = new List<string[]>();
    foreach (var ln in File.ReadAllLines(Path.Combine(dir, name)).Skip(1))
    {
        if (ln.Trim().Length == 0) continue;
        var c = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
        foreach (char ch in ln) { if (ch == '"') q = !q; else if (ch == ',' && !q) { c.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); }
        c.Add(sb.ToString());
        res.Add(c.ToArray());
    }
    return res;
}
var inv = System.Globalization.CultureInfo.InvariantCulture;
var bill = Csv("bar-lists.csv").Where(r => r[7].Contains("DIAPH")).ToList();
var seats = Csv("seat-elevations.csv");
foreach (var r in bill)
    pbd.BarList.Add(new PlanBarListLine { Superstructure = true, Mark = r[1], Size = "#" + r[2], Coating = r[3], Count = int.Parse(r[4]), Length = Ft(double.Parse(r[6], inv)), WeightLb = double.Parse(r[8], inv), Location = r[7] });
double Listed(string m) => double.Parse(bill.First(r => r[1] == m)[6], inv) * 12;     // inches

const PlanBarZRef EL = PlanBarZRef.Elevation, DT = PlanBarZRef.DeckTop;
double r4 = 0.25, r5 = 0.3125, r6 = 0.375;
var beams = new[] { -21.0, -15, -9, -3, 3, 9, 15 };
double hb = 19.5;                                  // half bottom flange (3'-3")
foreach (int j in new[] { 0, 1 })
{
    string side = j == 0 ? "S" : "N";
    double L = j == 0 ? 3.25 : 6.5;                                   // F1 / E1 bearing height
    double ledge = 920.75 + (j == 0 ? 19.25 : 16.75) / 12, backBot = ledge + 1.0 / 12;
    double sta = br.Supports[j].Station;
    double Sta(double uIn) => sta + (j == 0 ? 1 : -1) * In(uIn);
    double Deck(double w, double uIn) => br.Deck.FinishedElevation(al, Sta(uIn), Ft(w)) / Ft(1);
    double BB(double off) => double.Parse(seats.First(r => r[0] == side && Math.Abs(double.Parse(r[3], inv) - off) < 1e-6)[5], inv) + L / 12;
    // bottom of the beams along the line (ft): level under each bottom flange, straight between the flange edges, level beyond the fascia beams
    double Bot(double w)
    {
        if (w <= beams[0]) return BB(beams[0]);
        if (w >= beams[^1]) return BB(beams[^1]);
        for (int i = 0; i < beams.Length; i++)
        {
            if (Math.Abs(w - beams[i]) <= hb / 12) return BB(beams[i]);
            if (i + 1 < beams.Length && w > beams[i] + hb / 12 && w < beams[i + 1] - hb / 12)
            {
                double a = beams[i] + hb / 12, b = beams[i + 1] - hb / 12;
                return BB(beams[i]) + (BB(beams[i + 1]) - BB(beams[i])) * (w - a) / (b - a);
            }
        }
        return BB(beams[^1]);
    }
    PlanBarPoint E(double w, double uIn, double z) => new(Ft(w), In(uIn), EL, Ft(z));
    PlanBarPoint D(double w, double uIn, double dzIn) => new(Ft(w), In(uIn), DT, In(dzIn));
    PlanBarSet Add(string mark, string name, IEnumerable<PlanBarPoint> path, IEnumerable<double>? pos = null, string shape = "", params double[] dims)
    {
        var r = bill.First(x => x[1] == mark);
        var s = new PlanBarSet
        {
            Name = $"{side} {name}", Mark = mark, Size = "#" + r[2], Coating = r[3], Location = r[7], Source = "p.20 / p.21", Frame = PlanBarFrame.Support, Support = j,
            Superstructure = true, ListedLength = Ft(Listed(mark) / 12), Shape = shape, Cover = In(2),
        };
        s.Path.AddRange(path);
        if (pos != null) s.Positions.AddRange(pos.Select(Ft));
        s.Dims.AddRange(dims.Select(In));
        pbd.Sets.Add(s);
        return s;
    }
    // ---- set positions (ft along the line)
    var all = Enumerable.Range(0, 41).Select(k => -23.0 + k).ToList();
    var atBeam = all.Where(w => beams.Any(b => Math.Abs(w - b) <= 1 + 1e-9)).ToList();
    var bay = all.Except(atBeam).ToList();
    // ---- transverse bars, one Add per position (the bottom / deck top vary along the line)
    double xB = -30, xF = 6;
    double legB = xB + 2 + r5, legF = xF - 2 - r5;                           // S511E / S513E legs (2" clr to the back / front face)
    double legBr = legB + 18 - 2 * r5;                                      // S512E / S514E front leg (base 1'-6")
    foreach (double w in all)
    {
        // S510E: back-part U, nested in the S511E / S512E back leg; base under the lowest B.F. bar (4" above the back-part bottom)
        double a = legB + 0.625, b = a + 11 - 2 * r5, zb = backBot + (4 - r6 - r5) / 12;
        Add("S510E", "back-part U", new[] { E(w, a, zb + (33 - 2 * r5) / 12), E(w, a, zb), E(w, b, zb), E(w, b, zb + (33 - 2 * r5) / 12) }, null, "U", 33, 11, 33);
        // S615M: vertical leg at x -1'-8", horizontal 3'-6" in the deck under the top mat
        double zh = -4.6;
        Add("S615M", "vert. L", new[] { D(w, -20, zh - (30 - 2 * r6)), D(w, -20, zh), D(w, -20 + 42 - r6, zh) }, null, "L", 30, 42);
    }
    foreach (double w in bay)
    {
        double zb = Bot(w) + (2 + r5) / 12;
        Add("S511E", "stirrup btwn beams", new[] { E(w, legB, zb + (22 - 2 * r5) / 12), E(w, legB, zb), E(w, legF, zb), E(w, legF, zb + (34 - 2 * r5) / 12) }, null, "U (longer leg in the deck)", 22, 32, 34);
        Add("S513E", "tie btwn beams", new[] { D(w, legB, -18 - r5 - (12 - 2 * r5)), D(w, legB, -18 - r5), D(w, legF, -18 - r5), D(w, legF, -18 - r5 - (12 - 2 * r5)) }, null, "U", 12, 32, 12);
        // S616M: 3'-10" in the deck from the kink at x +1'-7.6", 12:12 leg 24" down-back
        double k = 24 / Math.Sqrt(2);
        Add("S616M", "deck tie", new[] { D(w, 19.6 - k, -4.6 - k), D(w, 19.6, -4.6), D(w, 19.6 + 46 - r6, -4.6) }, null, "Horizontal + 12:12 leg", 24, 46);
        // S617S approach panel tie, as drawn on A-A (vector)
        Add("S617S", "approach panel tie", new[] { D(w, -25.23, -8.33), D(w, -28.13, -5.43), D(w, -30.27, -4.53), D(w, -32.40, -5.43), D(w, -33.27, -7.53), D(w, -32.40, -9.67), D(w, -6.10, -35.93) }, null,
            "Straight + 180-deg hook (as drawn)", 42);
    }
    foreach (double w in atBeam)
    {
        double zb = Bot(w) + (2 + r5) / 12;
        Add("S512E", "stirrup at beams", new[] { E(w, legB, zb + (20 - 2 * r5) / 12), E(w, legB, zb), E(w, legBr, zb), E(w, legBr, zb + (20 - 2 * r5) / 12) }, null, "U", 20, 18, 20);
        Add("S514E", "tie at beams", new[] { D(w, legB, -18 - r5 - (12 - 2 * r5)), D(w, legB, -18 - r5), D(w, legBr, -18 - r5), D(w, legBr, -18 - r5 - (12 - 2 * r5)) }, null, "U", 12, 18, 12);
    }
    // ---- longitudinal S609E (40'-8"), polylines through the flange edges
    double w0 = -23.5 + 2.0 / 12, w1 = 17.5 - 2.0 / 12;
    var ws = new List<double> { w0 };
    foreach (double bm in beams) { ws.Add(bm - hb / 12); ws.Add(bm + hb / 12); }
    ws.Add(0); ws.Add(w1);
    ws = ws.Where(w => w >= w0 && w <= w1).Distinct().OrderBy(w => w).ToList();
    double Row1(double w, double u) => Deck(w, u) - 19.0 / 12;
    double Row3(double w) => Bot(w) + 3.0 / 12;
    double Row5 = backBot + 4.0 / 12;
    void Long(string mark, string name, double u, Func<double, double> z, double lenIn = 0)
    {
        var pts = ws.Select(w => E(w, u, z(w))).ToList();
        Add(mark, name, pts, null, "Straight");
    }
    double uB13 = legB + r5 + r6, uB45 = legB + 0.625 + r5 + r6, uC1 = legB + 0.625 + 11 - 2 * r5 - r5 - r6, uC2 = legBr - r5 - r6;
    Long("S609E", "B.F. row 1", uB13, w => Row1(w, uB13));
    Long("S609E", "B.F. row 2", uB13, w => (Row1(w, uB13) + Row3(w)) / 2);
    Long("S609E", "B.F. row 3", uB13, w => Row3(w));
    Long("S609E", "B.F. row 4", uB45, w => (Row3(w) + Row5) / 2);
    Long("S609E", "B.F. row 5", uB45, w => Row5);
    foreach (var (nm, f) in new (string, Func<double, double>)[] { ("row 1", w => Row1(w, uC1)), ("row 3", w => Row3(w)), ("row 4", w => (Row3(w) + Row5) / 2), ("row 5", w => Row5) })
        Long("S609E", "inside S510E " + nm, uC1, f);
    foreach (var (nm, f) in new (string, Func<double, double>)[] { ("row 1", w => Row1(w, uC2)), ("8 7/8 above the beam bottom", w => Bot(w) + 8.875 / 12), ("row 3", w => Row3(w)) })
        Long("S609E", "inside S512E " + nm, uC2, f);
    // S405E in the fillet: 2" clr to the 45-deg face (1'-0" x 1'-0" from the front face x +6" to x +1'-6" under the slab)
    {
        double c = (2 + r4) / Math.Sqrt(2), u = 12 - c;
        Long("S405E", "fillet", u, w => Deck(w, u) - 9.0 / 12 - (6 - c) / 12);
    }
    // ---- front-face bars between beams (inside the S511E front leg), centred in the bay
    double uFF = legF - r5 - r6;
    for (int i = 0; i + 1 < beams.Length; i++)
    {
        double mid = (beams[i] + beams[i + 1]) / 2;
        foreach (var (mark, zf) in new (string, Func<double, double>)[] { ("S607E", w => Row1(w, uFF)), ("S607E", w => (Row1(w, uFF) + Row3(w)) / 2), ("S608E", w => Row3(w)) })
        {
            double h = Listed(mark) / 24;
            Add(mark, "front face btwn beams", new[] { E(mid - h, uFF, zf(mid - h)), E(mid, uFF, zf(mid)), E(mid + h, uFF, zf(mid + h)) }, null, "Straight");
        }
    }
    // ---- S506E through the web holes (on the CL brg, 1'-1 1/4" and 1'-4 3/4" above the beam bottom), 4'-0" centred on each beam
    foreach (double bm in beams)
        foreach (double h in new[] { 13.25, 16.75 })
            Add("S506E", "thru beams", new[] { E(bm - 2, 0, BB(bm) + h / 12), E(bm + 2, 0, BB(bm) + h / 12) }, null, "Straight");
}
Print($"Br02069 end diaphragm plan bars: {pbd.Sets.Count(s => s.Superstructure && s.Location.Contains("DIAPH"))} sets, {bill.Count} bill lines");
