// MnDOT Br. 02069 - BARRIER reinforcing (Fig. 5-397.138(A) (MOD.) = p24: Section A-A, inside elevations "JOINT AT ABUTMENT", bend diagrams, bill)
// as PlanBarSet data (superstructure, alignment frame: W = station, U = offset; z from the deck top at the bar point). Called by build-br02069.csx.
//   sbm run samples\plans\rebar-barrier-br02069.csx "samples\plans\Br02069 TH65.sbm"
// Read from the sheet (vector linework of Section A-A, 1 1/2" = 1'-0" plotted at half size = 3 pt per inch; dims govern where printed):
//  * Section A-A (bridge deck): barrier 1'-6" at the base (7" traffic-face batter + 11" top), 3'-0" high; coping 1'-1" flush with the deck edge, lower back face
//    2" inside it. R505M closed tapered loop (2'-8" x 4 1/2" top / 10 1/2" bottom + 8" leg): back leg 2 3/4" clr, front leg 2" clr to the traffic face;
//    R501M dowel (tail 1'-6" on top of the bottom deck mat - note 1, 2 3/8:12 leg, 9" top, 1'-5" leg) - both as drawn (vector).
//    Longitudinal bars: 4 levels x 2 (5" under the top, 2 equal spaces, + the two dowel-box levels): z 30.93", 20.00", 9.07", 2.82" above the deck;
//    No. 4 (R411M 40'-0" x 2 + R413M 28'-11", "3'-2" MIN. LAP") on the upper two levels and No. 6 (R612M 40'-0" x 2 + R614M 29'-9", "3'-7" MIN. LAP")
//    on the lower two (inside elevation labels; level assignment DERIVED); 102'-7" per line = 102'-11" - 2 x 2"; order S -> N: 40'-0", 40'-0", short bar
//    (inside elevation: R413M / R614M at the N end - DERIVED for the E barrier).
//  * Along the deck ("JOINT AT ABUTMENT"): R501M & R505M at 3", 9", 15" from each end of the bridge joint (deck end), "1'-0" MAX. SPG." between:
//    101 equal spaces (11.93") -> 106 per barrier = 212 for the two barriers = the bill (spacing DERIVED from the count).
//  * Approach-panel barrier bars (R502E, R503E, R504E, R506E, R507E, R517E, R518E, R408E, R409E, R610E, R415E, R616E): the approach panels and their
//    barriers (p24 left half, p27) are not part of the bridge model - listed in the schedule, not placed.
using System.IO;
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Br02069 TH65 SB");
var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
br.PlanBars ??= new PlanBarsDefinition();
var pbd = br.PlanBars;
pbd.Sets.RemoveAll(s => s.Superstructure && s.Location.Contains("BARRIER"));
pbd.BarList.RemoveAll(l => l.Superstructure && l.Location.Contains("BARRIER"));
pbd.Unresolved.RemoveAll(u => u.Contains("approach-panel barrier"));
// barrier profile (GAP 7 partly closed): Section A-A 7" + 11" = 1'-6" base, 11" top
br.Deck.Barrier.TopWidth = In(11);

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "br02069-th65");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/br02069-th65 not found");
}
var inv = System.Globalization.CultureInfo.InvariantCulture;
var bill = new List<string[]>();
foreach (var ln in File.ReadAllLines(Path.Combine(DataDir(), "bar-lists.csv")).Skip(1))
{
    var c = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
    foreach (char ch in ln) { if (ch == '"') q = !q; else if (ch == ',' && !q) { c.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); }
    c.Add(sb.ToString());
    if (c.Count > 8 && c[0] == "Barrier") bill.Add(c.ToArray());
}
foreach (var r in bill)
    pbd.BarList.Add(new PlanBarListLine { Superstructure = true, Mark = r[1], Size = "#" + r[2], Coating = r[3], Count = int.Parse(r[4]), Length = Ft(double.Parse(r[6], inv)), WeightLb = double.Parse(r[8], inv), Location = r[7] });
double Listed(string m) => double.Parse(bill.First(r => r[1] == m)[6], inv);   // ft

double s0 = br.Supports[0].Station - Ft(1) - In(10), s1 = br.Supports[1].Station + Ft(1) + In(10);   // deck ends (1'-10" from CL brg)
double L = (s1 - s0) / In(1);                                                                          // inches
var pos = new List<double> { 3, 9, 15 };
for (int k = 1; k <= 101; k++) pos.Add(15 + (L - 30) * k / 101);
pos.AddRange(new[] { L - 9, L - 3 });
pos = pos.Distinct().OrderBy(x => x).ToList();
PlanBarSet Add(string mark, string name, IEnumerable<PlanBarPoint> path, IEnumerable<double>? posIn, string shape, double[] dims)
{
    var r = bill.First(x => x[1] == mark);
    var s = new PlanBarSet
    {
        Name = name, Mark = mark, Size = "#" + r[2], Coating = r[3], Location = r[7], Source = "p24 Fig. 5-397.138(A) (MOD.)", Frame = PlanBarFrame.Alignment,
        Superstructure = true, ListedLength = Ft(Listed(mark)), Shape = shape, Cover = In(2),
    };
    s.Path.AddRange(path);
    if (posIn != null) s.Positions.AddRange(posIn.Select(In));
    s.Dims.AddRange(dims.Select(In));
    pbd.Sets.Add(s);
    return s;
}
// section coordinates (inches): x = from the lower back face, - toward the deck (A-A vector), z above the deck top; the deck edge is at x = +1.97
var loop = new (double x, double z)[] { (-6.60, 1.93), (-10.57, 1.93), (-13.03, 4.90), (-7.77, 31.90), (-5.30, 33.93), (-2.80, 31.43), (-2.80, 4.07), (-5.30, 1.57), (-9.80, 1.57) };
var dowel = new (double x, double z)[] { (-32.27, -7.07), (-17.03, -7.07), (-14.60, -5.03), (-12.07, 7.90), (-9.60, 9.93), (-4.90, 9.93), (-2.40, 7.43), (-2.40, -4.57), (-4.90, -7.07), (-10.40, -7.07) };
foreach (var (side, edge, sign) in new[] { ("W", -23.5, 1.0), ("E", 17.5, -1.0) })
{
    double Off(double x) => edge + sign * (1.97 - x) / 12;      // inches from the deck edge inward
    PlanBarPoint P(double st, double x, double z) => new(st, Ft(Off(x)), PlanBarZRef.DeckTop, In(z));
    Add("R505M", $"{side} barrier vertical loop", loop.Select(p => P(s0, p.x, p.z)), pos, "Closed tapered loop + 8\" leg (as drawn; bill length not reconciled, U3)", new[] { 4.5, 32, 10.5, 8 });
    Add("R501M", $"{side} barrier dowel", dowel.Select(p => P(s0, p.x, p.z)), pos, "Dowel (as drawn)", new[] { 18.0, 9, 17 });
    // longitudinal: S -> N 40'-0", 40'-0", short bar, lapped
    foreach (var (x, z, n6) in new[] { (-3.78, 30.93, false), (-6.95, 30.93, false), (-3.78, 20.00, false), (-9.08, 20.00, false), (-3.78, 9.07, true), (-10.58, 9.07, true), (-3.78, 2.82, true), (-11.32, 2.82, true) })
    {
        string a = n6 ? "R612M" : "R411M", b = n6 ? "R614M" : "R413M";
        double lap = n6 ? 3 + 7.0 / 12 : 3 + 2.0 / 12;
        double st = s0 + Ft(2.0 / 12);
        foreach (var (mk, len) in new[] { (a, 40.0), (a, 40.0), (b, Listed(b)) })
        {
            var pts = new List<PlanBarPoint>();
            for (int k = 0; k <= 4; k++) pts.Add(P(st + Ft(len) * k / 4, x, z));       // follows the profile (VC)
            Add(mk, $"{side} barrier longit.", pts, null, "Straight", new[] { len * 12 });
            st += Ft(len - lap);
        }
    }
}
// approach-panel barrier bars: schedule only
var appr = new[] { "R502E", "R503E", "R504E", "R506E", "R507E", "R517E", "R518E", "R408E", "R409E", "R610E", "R415E", "R616E" };
pbd.Unresolved.Add($"p24 / p27: approach-panel barrier bars {string.Join(", ", appr)} - the approach panels and their barriers are not part of the bridge model (no host concrete); scheduled at the bill counts / lengths, not placed.");
Print($"Br02069 barrier plan bars: {pbd.Sets.Count(s => s.Location.Contains("BARRIER"))} sets ({pos.Count} positions per barrier), {bill.Count} bill lines");
