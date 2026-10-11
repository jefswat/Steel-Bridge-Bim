// FIELD HAUNCH DATA check (I-80 WB Design 116 PDF 136-157, Ramp G Design 316 PDF 113-128): plan haunch data vs the model.
// HAUNCH = top of web to bottom of deck at the girder centerline (the user's convention = the plans' "NOMINAL HAUNCH",
// TYP. SLAB & NOMINAL HAUNCH DETAIL: I-80 sheet 92 / PDF 94 "3 1/2" NOMINAL HAUNCH", Ramp G sheet 83 / PDF 85 "3 1/2" (UNITS 1, 2 & 4),
// 4" (UNIT 3)"; slab 8"). The plans' "FIELD HAUNCH" is a different quantity: girder line haunch elevation minus the SURVEYED top of
// the top flange (note 1, PDF 136 / 113) = bottom of slab at pour minus top of flange, i.e. HAUNCH minus the top flange thickness.
// This is a CHECK, it changes nothing and does not save the model.   sbm run samples\plans\check-haunch.csx "<model>.sbm"
// (after camber-i80.csx / camber-ramp-g.csx: the plan point locations come from the model's camber points).
// Compared at every plan point of every girder (tolerance 1/16" = plan precision of the fraction tables):
//  (1) model HAUNCH = deck finished grade (Deck.FinishedElevation at the girder line) - slab - model top of web   vs the plan nominal haunch.
//  (2) plan-implied HAUNCH = plan GIRDER LINE HAUNCH ELEVATION - plan slab+barrier deflection - model top of web   vs the nominal haunch
//      (the plan's own pour-time slab underside against the model's final steel; 0.01 ft = 1/8" printed precision, flagged > 1/8").
//  (3) CROSS SLOPE ADJUSTMENT (misc data table, inches, per girder flange edge L/R looking upstation): model = |finished grade at the girder
//      line - finished grade at the flange edge (girder line + or - top flange width / 2, normal to the girder)|.
//  (4) ALLOWABLE FIELD HAUNCH MIN/MAX (misc data table): model field haunch at the girder line = model HAUNCH - top flange thickness.
//  (5) plan consistency: misc-table slab+barrier deflection (1/16") vs the decimal deflection table (0.01") already in the model.
// Data: docs\plan-data\haunch\*-misc_data.csv, *-girder_line_haunch_elev.csv (extracted with page numbers; see haunch-staking-check.md),
//       discontinuous-girder tails from docs\plan-data\camber\*-unnumbered.csv (as check-blocking.csx).
// Env: HC_VERBOSE=1 prints every flagged value; HC_OUT=<file> writes every compared value as CSV.
using System.Globalization;
double Z(double v, int d) => Math.Round(v, d) + 0.0;   // no "-0" in printouts
const double TolIn = 1.0 / 16, TolElevIn = 0.125 + 1e-6, TolDefl = 1.0 / 32 + 0.005;
var verbose = Environment.GetEnvironmentVariable("HC_VERBOSE") == "1";
var outPath = Environment.GetEnvironmentVariable("HC_OUT");
string Dir(string sub) => new[] { $@"docs\plan-data\{sub}", $@"..\..\docs\plan-data\{sub}", $@"C:\SBM\docs\plan-data\{sub}" }.First(System.IO.Directory.Exists);
string hDir = Dir("haunch"), cDir = Dir("camber");
string[] Split(string line)      // CSV fields, double quotes allowed ("A, B, C(R) & D(L)")
{
    var res = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
    foreach (char ch in line)
    {
        if (ch == '"') q = !q;
        else if (ch == ',' && !q) { res.Add(cur.ToString()); cur.Clear(); }
        else cur.Append(ch);
    }
    res.Add(cur.ToString());
    return res.ToArray();
}
List<Dictionary<string, string>> Csv(string path)
{
    var lines = System.IO.File.ReadAllLines(path);
    var head = lines[0].Split(',');
    return lines.Skip(1).Where(l => l.Trim() != "").Select(l => { var c = Split(l); return head.Select((h, i) => (h, v: i < c.Length ? c[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
double Num(string s) => double.Parse(s.Trim().TrimEnd('*'), CultureInfo.InvariantCulture);
double InOf(double m) => m / In(1);
string StaS(double ft) { double h = Math.Floor(ft / 100); return $"{h:0}+{ft - h * 100:00.00}"; }
string Fr(double inches) { double a = Math.Abs(inches); int n = (int)Math.Round(a * 16); string w = (n / 16) > 0 ? $"{n / 16}" : ""; int f = n % 16; string fr = ""; if (f > 0) { int g = 16; while (f % 2 == 0) { f /= 2; g /= 2; } fr = $"{f}/{g}"; } var s = (w + (w != "" && fr != "" ? " " : "") + fr); return (inches < 0 && n > 0 ? "-" : "") + (s == "" ? "0" : s); }
// girder group label -> members (letter, side): "A, B, C(R) & D(L)", "E-F, D(R)", "H(R), I-K", "A, F-K", "B"
List<(char G, char Side)> Members(string label)
{
    var res = new List<(char, char)>();
    foreach (var tok0 in label.Replace("&", ",").Split(','))
    {
        var tok = tok0.Trim(); if (tok == "") continue;
        char side = 'B';
        var m = System.Text.RegularExpressions.Regex.Match(tok, @"^([A-K])\(([LR])\)$");
        if (m.Success) { res.Add((m.Groups[1].Value[0], m.Groups[2].Value[0])); continue; }
        m = System.Text.RegularExpressions.Regex.Match(tok, @"^([A-K])\s*-\s*([A-K])$");
        if (m.Success) { for (char c = m.Groups[1].Value[0]; c <= m.Groups[2].Value[0]; c++) res.Add((c, side)); continue; }
        if (tok.Length == 1 && tok[0] >= 'A' && tok[0] <= 'K') res.Add((tok[0], side));
        else throw new Exception($"unreadable girder group '{label}'");
    }
    return res;
}
// plan nominal haunch (in) per unit
double Nominal(string br, int u) => br == "RG" && u == 3 ? 4.0 : 3.5;

var rows = new List<(string Bridge, char G, int Pt, string Kind, double Sta, string Check, string Side, double Plan, double Model, int Page)>();
var notes = new List<string>();
var outRows = new List<string> { "bridge,girder,point,kind,station_ft,check,side,plan_in,model_in,diff_in,page" };
var deckNotes = new List<string>();
foreach (var (br, tag, nUnits) in new[] { ("I80", "I-80 WB Unit", 3), ("RG", "Ramp G Unit", 4) })
for (int u = 1; u <= nUnits; u++)
{
    string bn = $"{tag} {u}";
    var b = Doc.Project.Bridges.First(x => x.Name == bn);
    var al = Doc.Alignment(b.AlignmentName);
    var f = Doc.Model(bn).Framing;
    double nom = Nominal(br, u);
    deckNotes.Add($"{bn,-15} model Deck.Thickness {InOf(b.Deck.Thickness):0.###}\" (plan 8\"), Deck.Haunch {InOf(b.Deck.Haunch):0.###}\" (plan nominal {nom}\")");
    if (f.Camber == null) { notes.Add($"{bn}: no camber points in the model - run camber-*.csx first"); continue; }
    var misc = Csv(System.IO.Path.Combine(hDir, $"{br}-unit{u}-misc_data.csv"));
    var helev = Csv(System.IO.Path.Combine(hDir, $"{br}-unit{u}-girder_line_haunch_elev.csv"));
    // plan lookup: (table, letter, side, point) -> (value, page); discontinuous tables keyed by order index (mapped below)
    var mainT = new Dictionary<(string, char, char, int), (double V, int Page)>();
    var discT = new Dictionary<(string, char, char, int), (double V, int Page)>();
    foreach (var r in misc)
    {
        bool disc = r["table"].EndsWith("_discontinuous");
        string t = disc ? r["table"][..^"_discontinuous".Length] : r["table"];
        foreach (var (g, side) in Members(r["group"]))
            (disc ? discT : mainT)[(t, g, side, int.Parse(r["point"]))] = (Num(r["value_in"]), int.Parse(r["page"]));
    }
    var hplan = helev.ToDictionary(r => (r["girder"][0], int.Parse(r["point"])), r => (V: Num(r["haunch_elev_ft"]), Page: int.Parse(r["page"])));
    int helevPage = helev.Count > 0 ? int.Parse(helev[0]["page"]) : 0;
    foreach (var gres in f.Camber.Girders)
    {
        char gl = (char)('A' + gres.Girder);
        var gr = f.Girders[gres.Girder];
        var gcd = b.Girders.Camber.First(c => c.Girder == gres.Girder);
        var endKey = gcd.Keys.FirstOrDefault(k => k.GirderEnd);
        int tailStart = endKey != null ? gcd.Keys.Where(k => k.Number < endKey.Number).Max(k => k.Number) : int.MaxValue;
        var tailH = new List<double>();
        string tailFile = System.IO.Path.Combine(cDir, $"{br}-unit{u}-haunch_elev_ft-discontinuous-unnumbered.csv");
        if (endKey != null && System.IO.File.Exists(tailFile))
            tailH = Csv(tailFile).Where(r => r["girder"] == $"{gl}").OrderBy(r => int.Parse(r["order_index"])).Select(r => Num(r["value"])).ToList();
        (double V, int Page)? Plan(string table, char side, int pt)
        {
            if (pt >= tailStart)
            {
                int idx = pt - tailStart + 1;
                if (discT.TryGetValue((table, gl, side, idx), out var dv)) return dv;
                if (discT.TryGetValue((table, gl, 'B', idx), out dv)) return dv;
                if (pt > tailStart) return null;     // the CL pier point (idx 1) is also in the main table
            }
            if (mainT.TryGetValue((table, gl, side, pt), out var v)) return v;
            if (mainT.TryGetValue((table, gl, 'B', pt), out v)) return v;
            return null;
        }
        foreach (var p in gres.Points)
        {
            var (pt3, tan, _) = gr.Path.At(p.PathDistance);
            double web = p.TopOfWeb;
            double fg = b.Deck.FinishedElevation(al, p.Station, p.Offset);
            double hModel = InOf(fg - b.Deck.Thickness - web);
            // top flange just before / after the point: at a plate change (usually a field splice point) both sizes exist and the point is
            // compared with the one that fits better; such points are counted separately ("at a plate change")
            var tfa = gr.PlateAt(GirderPlate.TopFlange, Math.Max(0, p.PathDistance - 0.002));
            var tfb = gr.PlateAt(GirderPlate.TopFlange, p.PathDistance + 0.002);
            double tf = tfa is { } q ? InOf(q.Thickness) : double.NaN, bf = tfa is { } q2 ? q2.Width : double.NaN;
            double tf2 = tfb is { } q3 ? InOf(q3.Thickness) : tf, bf2 = tfb is { } q4 ? q4.Width : bf;
            bool change = Math.Abs(tf2 - tf) > 1e-6 || Math.Abs(bf2 - bf) > 1e-6;
            double sta = p.Station / Ft(1);
            void Add(string check, string side, double plan, double model, int page)
            {
                rows.Add((bn, gl, p.Number, p.Kind + (change ? "/plate-change" : ""), sta, check, side, plan, model, page));
                outRows.Add($"{bn},{gl},{p.Number},{p.Kind}{(change ? "/plate-change" : "")},{sta:F2},{check},{side},{plan:F4},{model:F4},{model - plan:F4},{page}");
            }
            double Best(double plan, double a, double c) => Math.Abs(a - plan) <= Math.Abs(c - plan) ? a : c;
            // (1) model haunch vs nominal
            Add("haunch_model", "CL", nom, hModel, br == "I80" ? 94 : 85);
            // (2) plan-implied haunch
            double planH = double.NaN; int hp = helevPage;
            if (p.Number >= tailStart && p.Number - tailStart < tailH.Count && p.Number > tailStart) planH = tailH[p.Number - tailStart];
            else if (hplan.TryGetValue((gl, p.Number), out var hv)) { planH = hv.V; hp = hv.Page; }
            if (!double.IsNaN(planH)) Add("haunch_plan_implied", "CL", nom, InOf(planH * Ft(1) - p.SlabDeflection - web), hp);
            // (3) cross slope adjustment per flange edge
            if (!double.IsNaN(bf))
                foreach (char side in new[] { 'L', 'R' })
                {
                    var pl = Plan("cross_slope_adjustment", side, p.Number);
                    if (pl == null) continue;
                    var dir = tan.Normalized();
                    double AdjAt(double w)
                    {
                        var e = pt3.XY + (side == 'L' ? dir.Left : dir.Right) * (w / 2);
                        var so = al.Inverse(e);
                        return InOf(fg - b.Deck.FinishedElevation(al, so.Station, so.Offset));
                    }
                    double adj = AdjAt(bf);
                    if (change) adj = Best(pl.Value.V, Math.Abs(adj), Math.Abs(AdjAt(bf2))) == Math.Abs(adj) ? adj : AdjAt(bf2);
                    Add("cross_slope_adj", $"{side}", pl.Value.V, Math.Abs(adj), pl.Value.Page);
                    if (verbose && Math.Abs(Math.Abs(adj) - pl.Value.V) > TolIn) Print($"   xs-adj {bn} {gl}{side} pt {p.Number}: signed model {Z(adj, 3):+0.000;-0.000} (+ = edge lower)");
                }
            // (4) allowable field haunch
            var mn = Plan("allowable_min", 'B', p.Number); var mx = Plan("allowable_max", 'B', p.Number);
            double tfUse = tf;
            if (change && mx != null) tfUse = (hModel - tf2 <= mx.Value.V + TolIn && (mn == null || hModel - tf2 >= mn.Value.V - TolIn)) ? tf2 : tf;
            if (mn != null && !double.IsNaN(tf)) Add("field_haunch_min", "CL", mn.Value.V, hModel - tfUse, mn.Value.Page);
            if (mx != null && !double.IsNaN(tf)) Add("field_haunch_max", "CL", mx.Value.V, hModel - tfUse, mx.Value.Page);
            tf = tfUse;
            // (5) plan consistency: fraction deflection vs decimal deflection (model input)
            var sd = Plan("slab_deflection", 'B', p.Number);
            if (sd != null) Add("slab_defl_frac_vs_decimal", "CL", sd.Value.V, InOf(p.SlabDeflection), sd.Value.Page);
        }
        // plan points the model did not locate
        int unl = mainT.Keys.Where(k => k.Item2 == gl && k.Item1 == "slab_deflection").Select(k => k.Item4).Distinct().Count(n => gres.Points.All(p => p.Number != n));
        if (unl > 0) notes.Add($"{bn} {gl}: {unl} plan points of the misc data table not located on the model girder (not compared)");
    }
}

string W(IEnumerable<(string Bridge, char G, int Pt, string Kind, double Sta, string Check, string Side, double Plan, double Model, int Page)> xs)
{
    var w = xs.OrderByDescending(x => Math.Abs(x.Model - x.Plan)).FirstOrDefault();
    return w.Bridge == null ? "-" : $"worst {Z(w.Model - w.Plan, 3):+0.000;-0.000}\" at {w.Bridge} {w.G}{(w.Side == "CL" ? "" : "(" + w.Side + ")")} pt {w.Pt} sta {StaS(w.Sta)} (plan {w.Plan:0.###} = {Fr(w.Plan)}, model {w.Model:0.###}, PDF {w.Page})";
}
Print("== FIELD HAUNCH DATA CHECK (model - plan, inches). HAUNCH = top of web to bottom of deck at the girder CL.");
foreach (var d in deckNotes) Print("   " + d);
var checks = new[] {
    ("haunch_model", TolIn, "(1) model HAUNCH (deck surface - slab - top of web) vs plan nominal haunch"),
    ("haunch_plan_implied", TolElevIn, "(2) plan-implied HAUNCH (plan girder line haunch elev - plan slab defl - model top of web) vs nominal; tol 1/8\" (0.01 ft printed)"),
    ("cross_slope_adj", TolIn, "(3) cross slope adjustment |grade(CL) - grade(flange edge)| vs plan"),
    ("slab_defl_frac_vs_decimal", TolDefl, "(5) PLAN CONSISTENCY: misc-table slab+barrier deflection (1/16) vs decimal table (0.01) = model input; tol 1/32 + 0.005") };
foreach (var (c, tol, title) in checks)
{
    var xs = rows.Where(x => x.Check == c).ToList();
    Print($"-- {title}");
    Print($"   compared {xs.Count}, within {xs.Count(x => Math.Abs(x.Model - x.Plan) <= tol)}, beyond {xs.Count(x => Math.Abs(x.Model - x.Plan) > tol)}; {W(xs)}");
    var pc = xs.Where(x => x.Kind.EndsWith("/plate-change")).ToList();
    if (pc.Count > 0) Print($"   of these at a top flange plate change (compared with the better of the two sizes): {pc.Count}, beyond {pc.Count(x => Math.Abs(x.Model - x.Plan) > tol)}");
    foreach (var gp in xs.GroupBy(x => x.Bridge))
    {
        var d = gp.Select(x => x.Model - x.Plan).ToList();
        Print($"   {gp.Key,-15} n={gp.Count(),5} mean {Z(d.Average(), 3):+0.000;-0.000} |d|max {d.Max(Math.Abs):0.000} beyond {gp.Count(x => Math.Abs(x.Model - x.Plan) > tol),4}  {W(gp)}");
    }
    if (verbose) foreach (var x in xs.Where(x => Math.Abs(x.Model - x.Plan) > tol).OrderBy(x => x.Bridge).ThenBy(x => x.G).ThenBy(x => x.Pt))
            Print($"      {x.Bridge,-15} {x.G}{x.Side,-2} pt {x.Pt,3} {x.Kind,-7} sta {StaS(x.Sta)} plan {Fr(x.Plan),7} model {x.Model,7:0.000} d {Z(x.Model - x.Plan, 3):+0.000;-0.000} PDF {x.Page}");
}
Print("-- (3) cross slope adjustment: flagged values by bridge / girder edge (count, mean d)");
foreach (var gp in rows.Where(x => x.Check == "cross_slope_adj" && Math.Abs(x.Model - x.Plan) > TolIn).GroupBy(x => (x.Bridge, x.G, x.Side)))
    Print($"   {gp.Key.Bridge,-15} {gp.Key.G}({gp.Key.Side}) {gp.Count(),3} flagged, pts {gp.Min(x => x.Pt)}-{gp.Max(x => x.Pt)}, mean d {Z(gp.Average(x => x.Model - x.Plan), 3):+0.000;-0.000}, {W(gp)}");
Print("-- (4) allowable field haunch: model field haunch at the girder CL (= model HAUNCH - top flange thickness) within the plan MIN..MAX");
{
    var mn = rows.Where(x => x.Check == "field_haunch_min").ToList();
    var mx = rows.Where(x => x.Check == "field_haunch_max").ToDictionary(x => (x.Bridge, x.G, x.Pt));
    int inside = 0, below = 0, above = 0;
    var bad = new List<string>();
    foreach (var x in mn)
    {
        if (!mx.TryGetValue((x.Bridge, x.G, x.Pt), out var y)) continue;
        if (x.Model < x.Plan - TolIn) { below++; bad.Add($"{x.Bridge} {x.G} pt {x.Pt} sta {StaS(x.Sta)}: model {x.Model:0.000} < MIN {Fr(x.Plan)} (PDF {x.Page})"); }
        else if (x.Model > y.Plan + TolIn) { above++; bad.Add($"{x.Bridge} {x.G} pt {x.Pt} sta {StaS(x.Sta)}: model {x.Model:0.000} > MAX {Fr(y.Plan)} (PDF {y.Page})"); }
        else inside++;
    }
    Print($"   points with MIN and MAX: {inside + below + above}; inside {inside}, below MIN {below}, above MAX {above}");
    foreach (var s in bad.Take(verbose ? 10000 : 15)) Print("      " + s);
    // OBSERVED (not printed on the plans): the MIN/MAX follow the plan's own cross slope adjustment a (larger of the girder's two edges) by one of
    //   C: MIN = a + 1/2, MAX = min(3 1/2, 4 - a)      T: MIN = max(0, a - 1/2), MAX = 3 - a
    // (C/T coincide with the top flange COMPRESSION / TENSION zones of the girder elevation sheets, e.g. I-80 Unit 2 PDF 112).
    var adj = rows.Where(x => x.Check == "cross_slope_adj").GroupBy(x => (x.Bridge, x.G, x.Pt)).ToDictionary(g => g.Key, g => g.Max(x => x.Plan));
    var rule = new Dictionary<(string, char, int), char>();
    foreach (var x in mn)
    {
        if (!mx.TryGetValue((x.Bridge, x.G, x.Pt), out var y) || !adj.TryGetValue((x.Bridge, x.G, x.Pt), out var a)) continue;
        bool C = Math.Abs(x.Plan - (a + 0.5)) < 1e-6 && Math.Abs(y.Plan - Math.Min(3.5, 4 - a)) < 1e-6;
        bool T = Math.Abs(x.Plan - Math.Max(0, a - 0.5)) < 1e-6 && Math.Abs(y.Plan - (3 - a)) < 1e-6;
        rule[(x.Bridge, x.G, x.Pt)] = C ? 'C' : T ? 'T' : '?';
    }
    foreach (var gp in rule.GroupBy(k => k.Key.Item1))
        Print($"   OBSERVED rule {gp.Key,-15} C {gp.Count(k => k.Value == 'C'),4}  T {gp.Count(k => k.Value == 'T'),4}  neither {gp.Count(k => k.Value == '?'),4}");
    var overMax = mn.Where(x => mx.TryGetValue((x.Bridge, x.G, x.Pt), out var y) && x.Model > y.Plan + TolIn).ToList();
    foreach (var gp in overMax.GroupBy(x => (x.Bridge, R: rule.TryGetValue((x.Bridge, x.G, x.Pt), out var r) ? r : '?')))
        Print($"   above MAX: {gp.Key.Bridge,-15} rule {gp.Key.R}: {gp.Count()} points, model field haunch {gp.Min(x => x.Model):0.000}-{gp.Max(x => x.Model):0.000}, plan MAX {string.Join("/", gp.Select(x => Fr(mx[(x.Bridge, x.G, x.Pt)].Plan)).Distinct())}");
}
foreach (var n in notes) Print("   NOTE " + n);
if (!string.IsNullOrEmpty(outPath)) { System.IO.File.WriteAllLines(outPath, outRows); Print($"wrote {outRows.Count - 1} rows to {outPath}"); }
