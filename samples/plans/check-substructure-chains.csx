// Elevation chains below the bearings (ElevationCheck: seat -> pedestal -> as-designed cap -> column -> footing -> pile head) for every
// bridge, reconciled against the plan elevations of samples\plans\substructure-elevations-i80-rampg.csv (the data of
// check-substructure-elevations.csx). Does not edit or save the model.
//   sbm run samples\plans\check-substructure-chains.csx "samples\plans\I-80 WB and Ramp G.sbm"
// Part 1: chain vs generated substructure (Mismatch) and the REPORTED input differences (shared pedestal seats, seats off the cap).
// Part 2: chain values vs plan: seat = PED k (row A/B by the seat N), pedestal height = PEDH k, cap top under the seat = the plan top
// line (printed corner elevations, linear between the model's corner S), column top = SOFF_COL k, flare = FLARE_ELEV, footing top =
// FTG_BOT + FTG_THK, footing bottom = FTG_BOT, pile head = FTG_BOT + PILE_HEAD. Flag > 0.01 ft (0.12 in for heights).
// Env: SUBCHAIN_OUT=<file> writes every comparison as CSV.
using System.Globalization;
using System.Text;
using SBM.Core.Checks;
using SBM.Core.Substructure;

const double Tol = 0.01;
var inv = CultureInfo.InvariantCulture;
string csvPath = @"samples\plans\substructure-elevations-i80-rampg.csv";
double F = Ft(1), I = In(1);
List<string> SplitCsv(string ln)
{
    var r = new List<string>(); var sb = new StringBuilder(); bool q = false;
    foreach (var ch in ln) { if (ch == '"') q = !q; else if (ch == ',' && !q) { r.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); }
    r.Add(sb.ToString()); return r;
}
var plan = new List<(string Pier, string Key, int Idx, double Val, int Pdf, string Half)>();
foreach (var ln in System.IO.File.ReadAllLines(csvPath))
{
    if (ln.StartsWith("#") || ln.Length < 5) continue;
    var c = SplitCsv(ln);
    plan.Add((c[1], c[2], int.Parse(c[3]), double.Parse(c[4], inv), int.Parse(c[6]), c[7]));
}
double? P(string pier, string key, int idx = 1, string half = "") =>
    plan.Where(x => x.Pier == pier && x.Key == key && x.Idx == idx && x.Half == half).Select(x => (double?)x.Val).FirstOrDefault();
int Pdf(string pier, string key) => plan.Where(x => x.Pier == pier && x.Key == key).Select(x => x.Pdf).FirstOrDefault();

Doc.Regenerate();
var units = new Dictionary<string, SubstructureUnit>();
foreach (var b in Doc.Project.Bridges)
    foreach (var u in Doc.Model(b.Name).Substructure?.Units ?? new())
        if (!units.ContainsKey(u.Name)) units[u.Name] = u;

var chains = new List<(string Bridge, ElevationChain Chain)>();
foreach (var b in Doc.Project.Bridges) chains.AddRange(Doc.BearingElevations(b.Name).Select(c => (b.Name, c)));
int withSub = chains.Count(c => c.Chain.Substructure != null);
Print($"Part 1: {chains.Count} bearing chains, {withSub} carried down the substructure, {chains.Count(c => c.Chain.Mismatch)} differ from the generated model, {chains.Count(c => c.Chain.Reported.Count > 0)} with reported input differences.");
foreach (var (b, c) in chains.Where(c => c.Chain.Mismatch)) Print($"  MISMATCH {b} | {c.Title} | {string.Join(" / ", c.Checks.Where(t => t.Contains("differs")))}");
foreach (var (b, c) in chains.Where(c => c.Chain.Reported.Count > 0)) Print($"  REPORTED {b} | {c.Title} | {string.Join(" / ", c.Reported)}");
foreach (var (b, c) in chains.Where(c => c.Chain.Substructure == null)) Print($"  NO SUBSTRUCTURE {b} | {c.Title}");

// ---- Part 2
var res = new List<(string Pier, string Item, string Where, double Plan, double Model, string Unit, int Pdf)>();
var seen = new HashSet<string>();
void Add(string pier, string item, string where, double? pl, double model, string unit, int pdf)
{
    if (pl == null || double.IsNaN(model)) return;
    if (!seen.Add(pier + "|" + item + "|" + where)) return;
    res.Add((pier, item, where, pl.Value, model, unit, pdf));
}
foreach (var (_, c) in chains)
{
    if (c.Substructure is not { } v || !units.TryGetValue(v.Unit, out var u)) continue;
    string pier = v.Unit;
    if (!plan.Any(x => x.Pier == pier)) continue;
    // seat row and index as the plan numbers them (left to right; rows A / B when the plan has both)
    bool rows = plan.Any(x => x.Pier == pier && x.Key == "PED" && x.Half != "");
    string half = !rows ? "" : v.SeatN > u.CapMidN + 0.25 * F ? "A" : "B";
    var row = u.Pedestals.Where(p => half == "" || (half == "A" ? p.RowN > u.CapMidN + 0.25 * F : p.RowN <= u.CapMidN + 0.25 * F)).OrderBy(p => p.RowS).ToList();
    int k = row.FindIndex(p => Math.Abs(p.RowS - v.SeatS) < 1e-4 && Math.Abs(p.RowN - v.SeatN) < 1e-4) + 1;
    string seatTag = $"seat {k}{(half == "" ? "" : " row " + half)}";
    if (k > 0)
    {
        Add(pier, "Seat (*ELEV.)", seatTag, P(pier, "PED", k, half), v.SeatZ / F, "ft", Pdf(pier, "PED"));
        Add(pier, "Pedestal height", seatTag, P(pier, "PEDH", k, half), (v.SeatZ - v.CapTop) / I, "in", Pdf(pier, "PEDH"));
    }
    // plan top line under the seat: printed corner elevations at the model's corner S (stepped: ahead half uses TOPA_*)
    bool aheadHalf = u.CapOutlineAhead.Count >= 2 && v.SeatN > u.CapMidN;
    var ol = aheadHalf ? u.CapOutlineAhead : u.CapOutline;
    double? tl = aheadHalf ? P(pier, "TOPA_L") : P(pier, "TOP_L"), tr = aheadHalf ? P(pier, "TOPA_R") : P(pier, "TOP_R");
    if (ol.Count >= 2 && tl != null && tr != null && Math.Abs(ol[1].X - ol[0].X) > 1e-6)
    {
        double t = (v.SeatS - ol[0].X) / (ol[1].X - ol[0].X);
        Add(pier, "Cap top under the seat (plan corners)", seatTag + (aheadHalf ? " (ahead half)" : ""), tl + t * (tr - tl), v.CapTop / F, "ft", Pdf(pier, "TOP_L"));
    }
    if (v.Column >= 0)
    {
        string col = $"column {v.Column + 1}";
        Add(pier, "Column top = soffit at the column", col, P(pier, "SOFF_COL", v.Column + 1), v.ColumnTop / F, "ft", Pdf(pier, "SOFF_COL"));
        Add(pier, "Flare elevation", col, P(pier, "FLARE_ELEV"), v.FlareZ / F, "ft", Pdf(pier, "FLARE_ELEV"));
        double? bot = P(pier, "FTG_BOT"), thk = P(pier, "FTG_THK"), ph = P(pier, "PILE_HEAD");
        Add(pier, "Footing top", col, bot + thk, v.FoundationTop / F, "ft", Pdf(pier, "FTG_BOT"));
        Add(pier, "Footing bottom", col, bot, v.FoundationBottom / F, "ft", Pdf(pier, "FTG_BOT"));
        Add(pier, "Pile head", col, bot + ph, v.PileHead / F, "ft", Pdf(pier, "PILE_HEAD"));
    }
}
bool Flag(double d, string unit) => unit == "in" ? Math.Abs(d) > Tol * 12 : Math.Abs(d) > Tol;
Print($"Part 2: {res.Count} chain values compared with the plan on {res.Select(r => r.Pier).Distinct().Count()} piers; {res.Count(r => Flag(r.Model - r.Plan, r.Unit))} differ by more than 0.01 ft (0.12 in):");
foreach (var g in res.GroupBy(r => r.Item))
    Print($"  {g.Key}: {g.Count()} compared, {g.Count(r => Flag(r.Model - r.Plan, r.Unit))} flagged, largest {g.Max(r => Math.Abs(r.Model - r.Plan)):F4} {g.First().Unit}");
foreach (var r in res.Where(r => Flag(r.Model - r.Plan, r.Unit)).OrderBy(r => r.Pier).ThenBy(r => r.Item).ThenBy(r => r.Where))
    Print($"  DIFF {r.Pier,-11} {r.Item,-38} {r.Where,-22} plan {r.Plan,10:F4} {r.Unit} (PDF {r.Pdf})  chain {r.Model,10:F4}  diff {r.Model - r.Plan:+0.0000;-0.0000}");
var outPath = Environment.GetEnvironmentVariable("SUBCHAIN_OUT");
if (!string.IsNullOrEmpty(outPath))
{
    var sb = new StringBuilder("pier,item,where,plan,unit,pdf,chain,diff,flag\n");
    foreach (var r in res)
        sb.AppendLine($"\"{r.Pier}\",\"{r.Item}\",\"{r.Where}\",{r.Plan.ToString("F5", inv)},{r.Unit},{r.Pdf},{r.Model.ToString("F5", inv)},{(r.Model - r.Plan).ToString("F5", inv)},{(Flag(r.Model - r.Plan, r.Unit) ? "DIFF" : "")}");
    System.IO.File.WriteAllText(outPath, sb.ToString());
}
