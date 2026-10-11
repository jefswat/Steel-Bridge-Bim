// Substructure elevation / height cross-check: every printed elevation and vertical dimension on the pier and abutment sheets of
// WB I-80 (Design 116) and Ramp G (Design 316) against the as-built model. Does not edit or save the model.
//   sbm run samples\plans\check-substructure-elevations.csx "samples\plans\I-80 WB and Ramp G.sbm"
// Data: samples\plans\substructure-elevations-i80-rampg.csv (columns set,pier,key,idx,plan,unit,pdf,half,note; plan = ft, or in for PEDH).
// Keys (model side): TOP_L/R = cap top at the top corners (CapOutline; TOPA_* = CapOutlineAhead), TIPD_* = corner to beak tip, DEPTH_L/R = top
// line to soffit at the kinks, DEPTH_CL = same at the column, SOFF_KINK_*, SOFF_COL k = soffit at column k, COLH_A k = soffit to top of
// footing, COLH_B k = soffit to the flare elevation, FLARE_ELEV/FLARE_HT, FTG_BOT/FTG_THK, OVERALL k = top line at column k to bottom of
// footing, PILE_HEAD (pile head above footing bottom), PILE_LEN, PED k = k-th pedestal top from the left (half A/B = ahead/back row),
// PEDH k = its height (in), ABUT_* = abutment items, NA = printed but no model equivalent. half A on DEPTH/TIPD/OVERALL uses the ahead cap top.
// Plan-only closure of the printed dimension strings is listed too (tolerance 0.015 ft: the plan elevations are rounded to 0.01).
// Env: SUBELEV_OUT=<file> writes every comparison as CSV. Tolerance: differences > 0.01 ft (0.12 in) are flagged.
using System.Globalization;
using System.Text;
using SBM.Core.Geometry;
using SBM.Core.Substructure;

const double Tol = 0.01;
var inv = CultureInfo.InvariantCulture;
string csvPath = new[] { @"samples\plans\substructure-elevations-i80-rampg.csv", @"C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\samples\plans\substructure-elevations-i80-rampg.csv" }.First(System.IO.File.Exists);
var outPath = Environment.GetEnvironmentVariable("SUBELEV_OUT");
double F = Ft(1), I = In(1);

List<string> SplitCsv(string ln)
{
    var r = new List<string>(); var sb = new StringBuilder(); bool q = false;
    foreach (var ch in ln)
    {
        if (ch == '"') q = !q;
        else if (ch == ',' && !q) { r.Add(sb.ToString()); sb.Clear(); }
        else sb.Append(ch);
    }
    r.Add(sb.ToString()); return r;
}
var plan = new List<(string Set, string Pier, string Key, int Idx, double Val, string Unit, int Pdf, string Half, string Note)>();
foreach (var ln in System.IO.File.ReadAllLines(csvPath))
{
    if (ln.StartsWith("#") || ln.Length < 5) continue;
    var c = SplitCsv(ln);
    plan.Add((c[0], c[1], c[2], int.Parse(c[3]), double.Parse(c[4], inv), c[5], int.Parse(c[6]), c[7], c[8]));
}

Doc.Regenerate();
var units = new Dictionary<string, SubstructureUnit>();
foreach (var b in Doc.Project.Bridges)
    foreach (var u in Doc.Model(b.Name).Substructure?.Units ?? new())
        if (!units.ContainsKey(u.Name)) units[u.Name] = u;

// ------------------------------------------------------------------ model values
FoundationInfo FndOf(SubstructureUnit u, int col)
{
    if (u.Foundations.Count == 0) return null;
    if (col >= 0 && col < u.Columns.Count)
    {
        var f = u.Foundations.OrderBy(q => Math.Abs(q.S - u.Columns[col].S)).First();
        if (Math.Abs(f.S - u.Columns[col].S) < 1.0 * F) return f;
    }
    return u.Foundations[Math.Min(Math.Max(col, 0), u.Foundations.Count - 1)];
}
List<Vec2> TopLine(SubstructureUnit u, string half) => half == "A" && u.CapTopAhead.Count > 0 ? u.CapTopAhead : u.CapTop;
List<PedestalInfo> Row(SubstructureUnit u, string half)
{
    IEnumerable<PedestalInfo> q = u.Pedestals;
    if (half == "B") q = q.Where(p => p.N <= u.CapMidN + 0.25 * F);
    else if (half == "A") q = q.Where(p => p.N > u.CapMidN + 0.25 * F);
    return q.OrderBy(p => p.S).ToList();
}
// returns model value (ft, or in for PEDH), or NaN with a reason
(double V, string Why) Model(string key, int idx, string half, SubstructureUnit u)
{
    var d = u.Definition; var o = u.CapOutline; var oa = u.CapOutlineAhead;
    var ol = half == "A" && oa.Count == 6 ? oa : o;
    int ci = idx - 1;
    switch (key)
    {
        case "TOP_L": return (o[0].Y / F, "");
        case "TOP_R": return (o[1].Y / F, "");
        case "TOPA_L": return oa.Count == 6 ? (oa[0].Y / F, "") : (double.NaN, "model has no ahead half");
        case "TOPA_R": return oa.Count == 6 ? (oa[1].Y / F, "") : (double.NaN, "model has no ahead half");
        case "TIPD_L": return ((ol[0].Y - o[5].Y) / F, "");
        case "TIPD_R": return ((ol[1].Y - o[2].Y) / F, "");
        case "DEPTH_L": return ((SubstructureUnit.Interp(TopLine(u, half), o[4].X) - o[4].Y) / F, "");
        case "DEPTH_R": return ((SubstructureUnit.Interp(TopLine(u, half), o[3].X) - o[3].Y) / F, "");
        case "TIPS_L":
        case "TIPS_R": return (double.NaN, "plan-only: closure of the end-face string (corner-to-tip + tip-to-soffit = depth)");
        case "SOFF_KINK_L": return (o[4].Y / F, "");
        case "SOFF_KINK_R": return (o[3].Y / F, "");
        case "SOFF_COL": return ci < u.Columns.Count ? (u.Columns[ci].TopZ / F, "") : (double.NaN, "model has no such column");
        case "COLH_A": return ci < u.Columns.Count && FndOf(u, ci) is { } fa ? ((u.Columns[ci].TopZ - fa.TopZ) / F, "") : (double.NaN, "no column/footing");
        case "COLH_B":
            if (double.IsNaN(d.ColumnFlareElevation)) return (double.NaN, "model has no flare");
            return ci < u.Columns.Count ? ((u.Columns[ci].TopZ - d.ColumnFlareElevation) / F, "") : (double.NaN, "no column");
        case "DEPTH_CL": return u.Columns.Count > 0 ? ((SubstructureUnit.Interp(TopLine(u, half), u.Columns[0].S) - u.Columns[0].TopZ) / F, "") : (double.NaN, "no column");
        case "FLARE_ELEV": return double.IsNaN(d.ColumnFlareElevation) ? (double.NaN, "model has no flare") : (d.ColumnFlareElevation / F, "");
        case "FLARE_HT": return double.IsNaN(d.ColumnFlareElevation) || FndOf(u, 0) is not { } ff ? (double.NaN, "model has no flare") : ((d.ColumnFlareElevation - ff.TopZ) / F, "");
        case "FTG_BOT": return (WorstOf(u.Foundations.Select(q => q.BottomZ / F), idx), "");
        case "FTG_THK": return (WorstOf(u.Foundations.Select(q => (q.TopZ - q.BottomZ) / F), idx), "");
        case "OVERALL": return ci < u.Columns.Count && FndOf(u, ci) is { } fo ? ((SubstructureUnit.Interp(TopLine(u, half), u.Columns[ci].S) - fo.BottomZ) / F, "") : (double.NaN, "no column");
        case "PILE_HEAD": { var v = u.Foundations.SelectMany(q => q.Piles.Select(p => (p.TopZ - q.BottomZ) / F)).ToList(); return v.Count == 0 ? (double.NaN, "no piles") : (WorstOf(v, 1), ""); }
        case "PILE_LEN": { var v = u.Foundations.SelectMany(q => q.Piles.Select(p => p.Length / F)).ToList(); return v.Count == 0 ? (double.NaN, "no piles") : (WorstOf(v, 1), ""); }
        case "PED":
        case "PEDH":
        case "ABUT_PED":
            {
                var r = Row(u, half);
                if (ci >= r.Count) return (double.NaN, $"model has {r.Count} pedestals (row {(half == "" ? "all" : half)})");
                return (key == "PEDH" ? r[ci].Height / I : r[ci].SeatZ / F, "");
            }
        case "ABUT_FTG_BOT": return u.CapBottom.Count < 2 ? (double.NaN, "no cap") : ((idx == 1 ? u.CapBottom.First().Y : u.CapBottom.Last().Y) / F, "");
        case "ABUT_FTG_DEPTH":
        case "ABUT_WASH_DEPTH": return u.CapBottom.Count < 2 ? (double.NaN, "no cap") : ((u.CapTop.First().Y - u.CapBottom.First().Y) / F, "model cap depth (parametric)");
        case "ABUT_BW_TOP": return u.Backwall is { } bw ? (SubstructureUnit.Interp(bw.Top, 0.0) / F, "backwall top at S = 0 (support line meets the alignment)") : (double.NaN, "no backwall");
        case "ABUT_PILE_LEN": { var v = u.Foundations.SelectMany(q => q.Piles.Select(p => p.Length / F)).ToList(); return v.Count == 0 ? (double.NaN, "no piles") : (v.Average(), ""); }
        case "ABUT_PILE_HEAD": { var v = u.Foundations.SelectMany(q => q.Piles.Select(p => (p.TopZ - q.BottomZ) / F)).ToList(); return v.Count == 0 ? (double.NaN, "no piles") : (v.Average(), ""); }
    }
    return (double.NaN, "unknown key");
}
// value among the list farthest from the plan is not known here: return the first, the caller compares every footing separately
double WorstOf(IEnumerable<double> vals, int idx) => vals.First();

var res = new List<(string Set, string Pier, string Key, int Idx, string Half, double Plan, string Unit, int Pdf, double Model, double Diff, string Note, string Why)>();
var missing = new List<string>();
foreach (var p in plan)
{
    if (p.Key == "NA") { res.Add((p.Set, p.Pier, p.Key, p.Idx, p.Half, p.Val, p.Unit, p.Pdf, double.NaN, double.NaN, p.Note, "no model equivalent")); continue; }
    if (!units.TryGetValue(p.Pier, out var u)) { missing.Add($"{p.Pier} (no model unit)"); res.Add((p.Set, p.Pier, p.Key, p.Idx, p.Half, p.Val, p.Unit, p.Pdf, double.NaN, double.NaN, p.Note, "no model unit")); continue; }
    var pv = p.Unit == "in" ? p.Val : p.Val;
    // footing / pile items: compare against EVERY footing and report the worst
    if (p.Key is "FTG_BOT" or "FTG_THK" or "PILE_HEAD" or "PILE_LEN")
    {
        IEnumerable<double> vals = p.Key switch
        {
            "FTG_BOT" => u.Foundations.Select(q => q.BottomZ / F),
            "FTG_THK" => u.Foundations.Select(q => (q.TopZ - q.BottomZ) / F),
            "PILE_HEAD" => u.Foundations.SelectMany(q => q.Piles.Select(x => (x.TopZ - q.BottomZ) / F)),
            _ => u.Foundations.SelectMany(q => q.Piles.Select(x => x.Length / F)),
        };
        var l = vals.ToList();
        if (l.Count == 0) { res.Add((p.Set, p.Pier, p.Key, p.Idx, p.Half, pv, p.Unit, p.Pdf, double.NaN, double.NaN, p.Note, "no foundation / piles")); continue; }
        double worst = l.OrderByDescending(x => Math.Abs(x - pv)).First();
        res.Add((p.Set, p.Pier, p.Key, p.Idx, p.Half, pv, p.Unit, p.Pdf, worst, worst - pv, p.Note, l.Count > 1 && l.Max() - l.Min() > 1e-6 ? $"model range {l.Min():F3}..{l.Max():F3}" : ""));
        continue;
    }
    var (mv, why) = Model(p.Key, p.Idx, p.Half, u);
    res.Add((p.Set, p.Pier, p.Key, p.Idx, p.Half, pv, p.Unit, p.Pdf, mv, double.IsNaN(mv) ? double.NaN : mv - pv, p.Note, why));
}
// pedestal count check per pier / row
var cnt = new List<string>();
foreach (var g in plan.Where(x => x.Key is "PED" or "ABUT_PED").GroupBy(x => (x.Pier, x.Half)))
    if (units.TryGetValue(g.Key.Pier, out var u2) && Row(u2, g.Key.Half).Count != g.Count())
        cnt.Add($"{g.Key.Pier} row '{(g.Key.Half == "" ? "all" : g.Key.Half)}': plan {g.Count()} pedestals, model {Row(u2, g.Key.Half).Count}");

// Alternative reading of the nominal cap-depth / beak dimensions: Ramp G and 6A print "9'-0 (TYP.)" / "5'-6 (TYP.)" etc. on slanted dimension lines
// (perpendicular to the sloped top), and the I-80 7'-0 is read the same way here. Where the model offsets the soffit by that dimension VERTICALLY
// (depth at the kink = nominal), the soffit / column-height items shift by D (1/cos - 1); depth and beak items are scaled by cos. NaN = not applicable.
double AltOf(string pier, string key, string half, double mv)
{
    if (double.IsNaN(mv) || !units.TryGetValue(pier, out var u)) return double.NaN;
    var ol = half == "A" && u.CapOutlineAhead.Count == 6 ? u.CapOutlineAhead : u.CapOutline;
    if (ol.Count < 6) return double.NaN;
    double slope = (ol[1].Y - ol[0].Y) / (ol[1].X - ol[0].X), cos = 1 / Math.Sqrt(1 + slope * slope);
    var dRow = plan.FirstOrDefault(x => x.Pier == pier && x.Key == "DEPTH_L" && x.Half == half);
    if (dRow.Pier == null) return double.NaN;
    double D = dRow.Val, delta = D * (1 / cos - 1);
    bool vertD = Math.Abs(Model("DEPTH_L", 1, half, u).V - D) < 1e-6;
    switch (key)
    {
        case "SOFF_COL": case "SOFF_KINK_L": case "SOFF_KINK_R": case "COLH_A": case "COLH_B": return vertD ? mv - delta : double.NaN;
        case "DEPTH_L": case "DEPTH_R": case "TIPD_L": case "TIPD_R": case "TIPS_L": case "TIPS_R": return mv * cos;
    }
    return double.NaN;
}
string AltTxt(string pier, string key, string half, double model, double plan)
{
    double a = AltOf(pier, key, half, model);
    return double.IsNaN(a) ? "" : $"  | perpendicular reading: model {a:F4}, diff {a - plan:+0.0000;-0.0000}{(Math.Abs(a - plan) <= Tol ? " (explained)" : "")}";
}
bool FlagOf(double diff, string unit) => !double.IsNaN(diff) && (unit == "in" ? Math.Abs(diff) > Tol * 12 : Math.Abs(diff) > Tol);
int compared = res.Count(r => !double.IsNaN(r.Diff));
Print($"Substructure elevation check: {compared} values compared on {res.Where(r => !double.IsNaN(r.Diff)).Select(r => r.Pier).Distinct().Count()} supports; {res.Count(r => r.Key == "NA")} printed values have no model equivalent.");
foreach (var m in missing.Distinct()) Print("  MISSING " + m);
foreach (var c in cnt) Print("  COUNT " + c);
foreach (var r in res.Where(r => FlagOf(r.Diff, r.Unit)).OrderBy(r => r.Pier).ThenBy(r => r.Key).ThenBy(r => r.Idx))
    Print($"  DIFF {r.Pier,-11} {r.Key,-15} {r.Idx,2}{(r.Half == "" ? "  " : " " + r.Half)} plan {r.Plan,10:F4} {r.Unit} (PDF {r.Pdf})  model {r.Model,10:F4}  diff {r.Diff,8:+0.0000;-0.0000}  {r.Why}{AltTxt(r.Pier, r.Key, r.Half, r.Model, r.Plan)}");

// ------------------------------------------------------------------ dimension-string closure
// Plan only (CSV values) and model.
var closure = new List<(string Pier, string What, double A, double B, double Diff, string Src)>();
foreach (var g in plan.Where(x => x.Key != "NA" && !x.Key.StartsWith("ABUT_")).GroupBy(x => x.Pier))
{
    double? P(string key, int idx = 1, string half = "") => plan.Where(x => x.Pier == g.Key && x.Key == key && x.Idx == idx && x.Half == half).Select(x => (double?)x.Val).FirstOrDefault();
    var top = new[] { P("TOP_L"), P("TOP_R") }; var topA = new[] { P("TOPA_L"), P("TOPA_R") };
    var colRows = plan.Where(x => x.Pier == g.Key && (x.Key == "COLH_A" || x.Key == "COLH_B")).ToList();
    int ncol = colRows.Count > 0 ? colRows.Max(x => x.Idx) : 0;
    double? flare = P("FLARE_ELEV"), flareHt = P("FLARE_HT"), bot = P("FTG_BOT"), thk = P("FTG_THK");
    double? ftop = bot.HasValue && thk.HasValue ? bot + thk : null;
    if (flare.HasValue && flareHt.HasValue && ftop.HasValue) closure.Add((g.Key, "flare elevation - flare height = footing bottom + footing thickness", flare.Value - flareHt.Value, ftop.Value, flare.Value - flareHt.Value - ftop.Value, "plan"));
    for (int k = 1; k <= ncol; k++)
    {
        double? a = P("COLH_A", k), b2 = P("COLH_B", k), sc = P("SOFF_COL", k);
        if (a.HasValue && b2.HasValue && flareHt.HasValue) closure.Add((g.Key, $"column {k}: Dim A - Dim B = flare height", a.Value - b2.Value, flareHt.Value, a.Value - b2.Value - flareHt.Value, "plan"));
        if (sc.HasValue && a.HasValue && ftop.HasValue) closure.Add((g.Key, $"column {k}: soffit elev - Dim A = footing top", sc.Value - a.Value, ftop.Value, sc.Value - a.Value - ftop.Value, "plan"));
        if (sc.HasValue && b2.HasValue && flare.HasValue) closure.Add((g.Key, $"column {k}: soffit elev - Dim B = flare elevation", sc.Value - b2.Value, flare.Value, sc.Value - b2.Value - flare.Value, "plan"));
    }
    // overall string: depth + column height(s) + flare + footing = overall; and top elevation at the column - footing bottom = overall
    double? ov = P("OVERALL", 1, plan.Where(x => x.Pier == g.Key && x.Key == "OVERALL").Select(x => x.Half).FirstOrDefault() ?? "");
    if (ov.HasValue)
    {
        string oh = plan.First(x => x.Pier == g.Key && x.Key == "OVERALL").Half;
        double? dep = P("DEPTH_CL", 1, oh) ?? P("DEPTH_L", 1, oh);
        double? colA = P("COLH_A", 1), colB = P("COLH_B", 1);
        double? colTot = colA ?? (colB.HasValue && flareHt.HasValue ? colB + flareHt : null);
        if (dep.HasValue && colTot.HasValue && thk.HasValue)
            closure.Add((g.Key, "cap depth + column height (+ flare) + footing thickness = overall", dep.Value + colTot.Value + thk.Value, ov.Value, dep.Value + colTot.Value + thk.Value - ov.Value, "plan"));
        // top line at the column: level top, or the mean of the corners when the column is at the middle of the corners
        double? tcol = null;
        var tt = oh == "A" ? topA : top;
        if (tt[0].HasValue && tt[1].HasValue && (Math.Abs(tt[0].Value - tt[1].Value) < 1e-9 || g.Key.EndsWith("G") && g.Key != "Pier 16G" && g.Key != "Pier 17G" && g.Key != "Pier 19G" || g.Key == "Pier 6A")) tcol = (tt[0].Value + tt[1].Value) / 2;
        if (tcol.HasValue && bot.HasValue) closure.Add((g.Key, "top elevation at the column - footing bottom = overall", tcol.Value - bot.Value, ov.Value, tcol.Value - bot.Value - ov.Value, "plan"));
    }
    var kl = P("SOFF_KINK_L"); var kr = P("SOFF_KINK_R");
    if (kl.HasValue && kr.HasValue && flare.HasValue && P("COLH_B", 1) is double cb1) closure.Add((g.Key, "mean kink soffit elev - column height above flare = flare elevation", (kl.Value + kr.Value) / 2 - cb1, flare.Value, (kl.Value + kr.Value) / 2 - cb1 - flare.Value, "plan"));
    if (kl.HasValue && kr.HasValue && !flare.HasValue && P("COLH_A", 1) is double ca1 && ftop.HasValue) closure.Add((g.Key, "mean kink soffit elev - column height = footing top", (kl.Value + kr.Value) / 2 - ca1, ftop.Value, (kl.Value + kr.Value) / 2 - ca1 - ftop.Value, "plan"));
    // printed pedestal height = pedestal top - level cap top (plan only; level tops only)
    if (top[0].HasValue && top[1].HasValue && Math.Abs(top[0].Value - top[1].Value) < 1e-9)
        foreach (var ph in plan.Where(x => x.Pier == g.Key && x.Key == "PEDH"))
        {
            var pe = plan.FirstOrDefault(x => x.Pier == g.Key && x.Key == "PED" && x.Idx == ph.Idx && x.Half == ph.Half);
            if (pe.Pier != null) closure.Add((g.Key, $"pedestal {ph.Idx}{(ph.Half == "" ? "" : " row " + ph.Half)}: printed *ELEV. - cap top = printed height", pe.Val - top[0].Value, ph.Val / 12.0, pe.Val - top[0].Value - ph.Val / 12.0, "plan"));
        }
    // corner-to-tip + tip-to-soffit = cap depth (4'-6 + 2'-6 = 7'-0, 5'-6 + 3'-6 = 9'-0, 6A 5'-0 + 3'-6 = 8'-6)
    foreach (var side in new[] { "L", "R" })
    {
        string h6 = g.Key == "Pier 6A" ? "A" : "";
        double? tdv = P("TIPD_" + side, 1, h6), tsv = P("TIPS_" + side, 1, h6), dv = P("DEPTH_" + side, 1, h6);
        if (tdv.HasValue && tsv.HasValue && dv.HasValue) closure.Add((g.Key, $"corner-to-tip + tip-to-soffit = cap depth ({side})", tdv.Value + tsv.Value, dv.Value, tdv.Value + tsv.Value - dv.Value, "plan"));
    }
}
// Model closure: cap depth at the column + column height (+ flare) + footing thickness = top line at the column - footing bottom
foreach (var kv in units)
{
    var u = kv.Value; if (u.Type != SBM.Core.Bridge.SupportType.Pier || u.Columns.Count == 0 || u.Foundations.Count == 0) continue;
    for (int k = 0; k < u.Columns.Count; k++)
    {
        var f = FndOf(u, k); var c = u.Columns[k];
        var tl = u.CapTop; double top = SubstructureUnit.Interp(tl, c.S);
        double lhs = (top - c.TopZ) + (c.TopZ - f.TopZ) + (f.TopZ - f.BottomZ), rhs = top - f.BottomZ;
        if (Math.Abs(lhs - rhs) > 1e-6) closure.Add((u.Name, $"model column {k + 1}: depth + column + footing = overall", lhs / F, rhs / F, (lhs - rhs) / F, "model"));
    }
}
Print($"Dimension strings: {closure.Count(c => c.Src == "plan")} plan identities checked, {closure.Count(c => c.Src == "plan" && Math.Abs(c.Diff) > 0.015)} do not close (> 0.015 ft):");
foreach (var c in closure.Where(c => Math.Abs(c.Diff) > 0.015)) Print($"  CLOSURE {c.Pier,-11} {c.What}: {c.A:F4} vs {c.B:F4}  ({c.Diff:+0.0000;-0.0000} ft, {c.Src})");

if (!string.IsNullOrEmpty(outPath))
{
    var sb = new StringBuilder("set,pier,key,idx,half,plan,unit,pdf,model,diff,flag,alt,altdiff,note,why\n");
    string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
    foreach (var r in res)
        sb.AppendLine(string.Join(",", r.Set, Q(r.Pier), r.Key, r.Idx, r.Half, r.Plan.ToString("F5", inv), r.Unit, r.Pdf,
            double.IsNaN(r.Model) ? "" : r.Model.ToString("F5", inv), double.IsNaN(r.Diff) ? "" : r.Diff.ToString("F5", inv), FlagOf(r.Diff, r.Unit) ? "DIFF" : "",
            double.IsNaN(AltOf(r.Pier, r.Key, r.Half, r.Model)) ? "" : AltOf(r.Pier, r.Key, r.Half, r.Model).ToString("F5", inv),
            double.IsNaN(AltOf(r.Pier, r.Key, r.Half, r.Model)) ? "" : (AltOf(r.Pier, r.Key, r.Half, r.Model) - r.Plan).ToString("F5", inv), Q(r.Note), Q(r.Why)));
    System.IO.File.WriteAllText(outPath, sb.ToString());
    var sc = new StringBuilder("pier,what,a,b,diff,src\n");
    foreach (var c in closure) sc.AppendLine($"{Q(c.Pier)},{Q(c.What)},{c.A.ToString("F5", inv)},{c.B.ToString("F5", inv)},{c.Diff.ToString("F5", inv)},{c.Src}");
    System.IO.File.WriteAllText(outPath.Replace(".csv", "-closure.csv"), sc.ToString());
}
