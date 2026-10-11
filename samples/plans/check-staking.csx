// STAKING DIAGRAM check (I-80 WB Design 116 PDF 15-17, Ramp G Design 316 PDF 14-17) against the model. A CHECK: changes nothing, does not save.
//   sbm run samples\plans\check-staking.csx "<model>.sbm"
// What the staking diagrams give (docs\plan-data\staking\*.csv, read from the text layer and raster clips):
//  * NO northing / easting and no coordinate datum: every staked point is a STATION + OFFSET - I-80 points as "STA. x, 41.08' LT." of CL I-80
//    (= the CL of the westbound approach roadway), Ramp G points as stations on CL Ramp G. A grid datum (state plane / UTM / project) is
//    therefore UNRESOLVED and nothing here is compared in grid coordinates; the model's UTM 15N placement (geolocate-cbis.csx) is a rigid
//    move and does not enter. Everything is compared in the model's own plan geometry: stations/offsets, distances and angles.
//  * Plan curve data on the sheets: P.C. STA 7597+73.11, R = 2400.00' (CL I-80, PDF 16/17); the WB CL is concentric 41.08' left, so an I-80
//    station maps to the WB alignment station by the radius ratio on the curve (the staked spans "measured along CL westbound approach roadway"
//    confirm this: 234'-1 1/16 = 196.72 + 38.02 x 2358.92 / 2400).
// Compared (tolerance 1/16" = 0.0052 ft for lengths; 0.01 deg for skews; 0.01 ft for spans printed to 1/16"):
//  (1) staked CL point vs the model CL pier line (model support line moved by PierCenterOffset for shared piers): station difference along the
//      alignment at the crossing.
//  (2) skew: angle between the model pier line and the alignment normal at the crossing vs the printed angle (magnitude; 90 deg = no skew).
//  (3) spans: printed span lengths vs the distance along the alignment between the model CL pier crossings.
//  (4) footings: centre along the pier line from the staked CL point (S), across (N), and sizes, vs the model footing solids.
//  (5) ties: CL Pier 11WB & 19G = I-80 7602+94.96 109.63' LT = Ramp G 67502+97.70; 77'-6 7/8 between the WB and Ramp G CL points on that pier
//      line; CL Pier 6A x CL Ramp A2 = I-80 7591+08.83 107.43' LT (Ramp A2 is not modeled).
// Env: ST_VERBOSE=1 prints every compared row.
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
double Z(double v, int d) => Math.Round(v, d) + 0.0;   // no "-0" in printouts
const double TolFt = 1.0 / 16 / 12, TolDeg = 0.01, TolSpan = 0.01;
bool verbose = Environment.GetEnvironmentVariable("ST_VERBOSE") == "1";
string Dir(string sub) => new[] { $@"docs\plan-data\{sub}", $@"..\..\docs\plan-data\{sub}", $@"C:\SBM\docs\plan-data\{sub}" }.First(System.IO.Directory.Exists);
string sDir = Dir("staking");
string[] Split(string line)      // CSV; a double quote opens a quoted field only at the start of the field (skews contain ")
{
    var res = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
    for (int i = 0; i < line.Length; i++)
    {
        char ch = line[i];
        if (ch == '"' && (q || cur.Length == 0)) { q = !q; continue; }
        if (ch == ',' && !q) { res.Add(cur.ToString()); cur.Clear(); } else cur.Append(ch);
    }
    res.Add(cur.ToString());
    return res.ToArray();
}
List<Dictionary<string, string>> Csv(string name)
{
    var lines = System.IO.File.ReadAllLines(System.IO.Path.Combine(sDir, name));
    var head = Split(lines[0]);
    return lines.Skip(1).Where(l => l.Trim() != "").Select(l => { var c = Split(l); return head.Select((h, i) => (h, v: i < c.Length ? c[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
double Fe(double m) => m / Ft(1);
string FtIn(double ft) { double a = Math.Abs(ft); int n = (int)Math.Round(a * 12 * 16); int f = n / (12 * 16); n -= f * 192; int i = n / 16; int fr = n % 16; string fs = ""; if (fr > 0) { int g = 16; while (fr % 2 == 0) { fr /= 2; g /= 2; } fs = $" {fr}/{g}"; } return $"{(ft < 0 ? "-" : "")}{f}'-{i}{fs}"; }

// ---- alignments and the plan's station datum
var wb = Doc.Alignment("I-80 WB");
var rg = Doc.Alignment("Ramp G");
double pc80 = Sta("7597+73.11"), R80 = Ft(2400), Rwb = R80 - Ft(41.08);
double WbSta(double i80) => i80 <= pc80 ? i80 : pc80 + (i80 - pc80) * Rwb / R80;
double I80Sta(double wbs) => wbs <= pc80 ? wbs : pc80 + (wbs - pc80) * R80 / Rwb;
(SBM.Core.Alignments.Alignment Al, double Sta, double Off) PlanAt(string alignment, string sta, double ltFt) => alignment switch
{
    "CL I-80" => (wb, WbSta(Sta(sta)), -Ft(ltFt - 41.08)),
    "CL Ramp G" => (rg, Sta(sta), -Ft(ltFt)),
    _ => throw new Exception($"alignment '{alignment}' is not in the model"),
};
Vec2 Tangent(SBM.Core.Alignments.Alignment al, double sta) { var (_, ang) = al.EvaluateAtDistance(al.StationToDistance(sta)); return Vec2.FromAngle(ang); }
// crossing of a line (point p0, unit direction d) with an alignment offset line: secant on the line parameter
Vec2 Cross(SBM.Core.Alignments.Alignment al, Vec2 p0, Vec2 d, double off)
{
    double F(double t) => al.Inverse(p0 + d * t).Offset - off;
    double t0 = 0, t1 = Ft(10), f0 = F(t0), f1 = F(t1);
    for (int it = 0; it < 40 && Math.Abs(f1) > 1e-8; it++) { double t2 = t1 - f1 * (t1 - t0) / (f1 - f0); t0 = t1; f0 = f1; t1 = t2; f1 = F(t1); }
    return p0 + d * t1;
}
// model CL pier line of a support: (point on the line, unit direction left->right, unit normal up-station)
(Vec2 P, Vec2 Dir, Vec2 Nrm, string Bridge, int Index)? PierLine(string bridge, string support)
{
    var b = Doc.Project.Bridges.First(x => x.Name == bridge);
    int n = b.Supports.Count + b.AuxiliarySupports.Count;
    int j = Enumerable.Range(0, n).FirstOrDefault(k => b.SupportAt(k).Name == support, -1);
    if (j < 0) return null;
    var line = Doc.Model(bridge).Framing.SupportLine(j);
    var al = Doc.Alignment(b.AlignmentName);
    var d = line.Direction.Normalized();
    var t = Tangent(al, line.Station);
    if (Vec2.Dot(d, t.Right) < 0) d = -d;                              // left -> right
    var nrm = d.Left; if (Vec2.Dot(nrm, t) < 0) nrm = -nrm;            // up-station
    return (line.AlignmentPoint + nrm * b.SupportAt(j).PierCenterOffset, d, nrm, bridge, j);
}

var flagged = new List<string>();
int nPts = 0, okPts = 0, nSk = 0, okSk = 0, nSp = 0, okSp = 0, nF = 0, okF = 0;
var pierCross = new Dictionary<string, (Vec2 Q, double ModelSta, SBM.Core.Alignments.Alignment Al, Vec2 Dir, Vec2 Nrm)>();

Print("== STAKING DIAGRAM CHECK (model - plan). Plan coordinates: station / offset only - grid datum UNRESOLVED (not stated on the staking sheets).");
Print("-- (1) staked CL points and (2) skews");
foreach (var r in Csv("supports.csv"))
{
    string bn = r["bridge"], sn = r["support"];
    var pl = PierLine(bn, sn);
    if (pl == null) { Print($"   {bn,-15} {sn,-10} not in the model"); continue; }
    var (P, dir, nrm, _, _) = pl.Value;
    var (al, psta, poff) = PlanAt(r["alignment"], r["station"], D(r["offset_lt_ft"]));
    var Q = Cross(al, P, dir, poff);                                   // model pier line at the staked point's offset
    double msta = al.Inverse(Q).Station;
    pierCross[$"{bn}|{sn}"] = (Q, msta, al, dir, nrm);
    double dSta = r["alignment"] == "CL I-80" ? I80Sta(msta) - Sta(r["station"]) : msta - psta;   // in the plan's own stationing
    double perp = Vec2.Dot(al.PointAt(psta, poff) - Q, nrm);
    nPts++; bool ok = Math.Abs(Fe(dSta)) <= TolFt; if (ok) okPts++;
    string skewTxt = "";
    if (r["skew_deg"] != "")
    {
        var nAl = Tangent(al, msta).Left;
        double sk = Math.Acos(Math.Min(1, Math.Abs(Vec2.Dot(dir, nAl.Normalized())))) * 180 / Math.PI;
        double dd = sk - D(r["skew_deg"]);
        nSk++; if (Math.Abs(dd) <= TolDeg) okSk++;
        skewTxt = $"skew model {sk,9:F4} plan {D(r["skew_deg"]),9:F4} ({r["skew_printed"]}) d {Z(dd, 4):+0.0000;-0.0000} deg{(Math.Abs(dd) > TolDeg ? " <-" : "")}";
        if (Math.Abs(dd) > TolDeg) flagged.Add($"skew {bn} {sn}: model {sk:F4} vs plan {r["skew_printed"]} = {D(r["skew_deg"]):F4} (d {Z(dd * 60, 1):+0.0;-0.0}'), PDF {r["pdf_page"]}");
    }
    else skewTxt = $"skew not compared ({r["skew_ref"]})";
    if (!ok) flagged.Add($"point {bn} {sn}: plan {r["station"]} {r["offset_lt_ft"]}' LT {r["alignment"]}, model CL pier crosses {Z(Fe(dSta), 3):+0.000;-0.000} ft (perp {Z(Fe(perp), 3):+0.000;-0.000} ft), PDF {r["pdf_page"]}");
    if (verbose || !ok) Print($"   {bn,-15} {sn,-10} {r["station"],-12} {r["offset_lt_ft"],6}' LT: model CL pier crossing d sta {Z(Fe(dSta), 4):+0.0000;-0.0000} ft (perp {Z(Fe(perp), 4):+0.0000;-0.0000}){(ok ? "" : " <-")} | {skewTxt}");
    else Print($"   {bn,-15} {sn,-10} {r["station"],-12} ok (d {Z(Fe(dSta), 4):+0.0000;-0.0000} ft) | {skewTxt}");
}
Print($"   points: {okPts}/{nPts} within {TolFt * 12:0.###}\"; skews: {okSk}/{nSk} within {TolDeg} deg");

Print("-- (3) spans along the CL (printed, PDF title blocks / dimension strings) vs the model CL pier crossings");
foreach (var r in Csv("spans.csv"))
{
    string bn = r["bridge"];
    var b = Doc.Project.Bridges.First(x => x.Name == bn);
    var printed = r["spans_printed"].Split(';').Select(x => x.Trim()).ToList();
    double Len(string s)
    {
        var m = System.Text.RegularExpressions.Regex.Match(s, @"^(\d+)'-(\d+)(?: (\d+)/(\d+))?$");
        return int.Parse(m.Groups[1].Value) + (int.Parse(m.Groups[2].Value) + (m.Groups[3].Success ? D(m.Groups[3].Value) / D(m.Groups[4].Value) : 0)) / 12.0;
    }
    var names = b.Supports.Select(s => s.Name).ToList();
    var cells = new List<string>();
    for (int k = 0; k + 1 < names.Count && k < printed.Count; k++)
    {
        // a shared pier is staked once (under one unit); its CL pier crossing serves both units
        bool Find(string nm, out (Vec2 Q, double ModelSta, SBM.Core.Alignments.Alignment Al, Vec2 Dir, Vec2 Nrm) v)
        {
            if (pierCross.TryGetValue($"{bn}|{nm}", out v)) return true;
            var key = pierCross.Keys.FirstOrDefault(k => k.EndsWith("|" + nm) && pierCross[k].Al == Doc.Alignment(b.AlignmentName));
            if (key != null) { v = pierCross[key]; return true; }
            return false;
        }
        if (!Find(names[k], out var a) || !Find(names[k + 1], out var c)) { cells.Add("n/a"); continue; }
        double model = Fe(c.ModelSta - a.ModelSta), plan = Len(printed[k]);
        nSp++; bool ok = Math.Abs(model - plan) <= TolSpan; if (ok) okSp++;
        cells.Add($"{Z(model - plan, 3):+0.000;-0.000}{(ok ? "" : "<-")}");
        if (!ok) flagged.Add($"span {bn} {names[k]}-{names[k + 1]}: model {model:F3} ft vs plan {printed[k]} ({plan:F3}), PDF {r["pdf_page"]}");
    }
    Print($"   {bn,-15} model - plan (ft): {string.Join("  ", cells)}");
}
Print($"   spans: {okSp}/{nSp} within {TolSpan} ft");

Print("-- (4) footings: S along the pier line from the staked CL point (right +), N across (up-station +), sizes S x N; model - plan in inches");
var sub = Doc.Project.Bridges.ToDictionary(b => b.Name, b => Doc.Substructure(b.Name));
foreach (var grp in Csv("footings.csv").GroupBy(r => (r["bridge"], r["support"])))
{
    var (bn, sn) = grp.Key;
    if (!pierCross.TryGetValue($"{bn}|{sn}", out var pc)) { Print($"   {bn} {sn}: no staked point"); continue; }
    var unit = sub.Values.SelectMany(s => s.Units).Where(u => u.Name == sn && u.Solids.Any(x => x.Kind == SubElementKind.Footing))
        .OrderBy(u => (u.Origin - pc.Q).Length).FirstOrDefault();
    if (unit == null) { Print($"   {bn,-15} {sn,-10} model has no footing solids"); continue; }
    var mf = unit.Solids.Where(x => x.Kind == SubElementKind.Footing).Select(f =>
    {
        var vs = f.Mesh.Positions.Select(p => new Vec2(Vec2.Dot(p.XY - pc.Q, pc.Dir), Vec2.Dot(p.XY - pc.Q, pc.Nrm))).ToList();
        double s0 = vs.Min(v => v.X), s1 = vs.Max(v => v.X), n0 = vs.Min(v => v.Y), n1 = vs.Max(v => v.Y);
        return (S: Fe((s0 + s1) / 2), N: Fe((n0 + n1) / 2), LS: Fe(s1 - s0), LN: Fe(n1 - n0));
    }).OrderBy(x => x.S).ToList();
    var pf = grp.OrderBy(r => int.Parse(r["footing"])).ToList();
    if (mf.Count != pf.Count) { flagged.Add($"footings {bn} {sn}: plan {pf.Count}, model {mf.Count}"); Print($"   {bn,-15} {sn,-10} footing count plan {pf.Count} model {mf.Count} <-"); continue; }
    for (int k = 0; k < pf.Count; k++)
    {
        var r = pf[k]; var m = mf[k];
        string sTxt = r["s_center_ft"];
        double ps;
        if (sTxt.StartsWith("TIE+"))
        {   // Pier 6A: from CL Pier 6A x CL Ramp A2 (I-80 7591+08.83, 107.43' LT) toward the WB CL
            var tie = PlanAt("CL I-80", "7591+08.83", 107.43);
            var tp = tie.Al.PointAt(tie.Sta, tie.Off);
            double sTie = Fe(Vec2.Dot(tp - pc.Q, pc.Dir)), nTie = Fe(Vec2.Dot(tp - pc.Q, pc.Nrm));
            ps = sTie + D(sTxt[4..]);
            var qwb = Cross(wb, pc.Q, pc.Dir, 0);
            double along = Fe((tp - qwb).Length), perp = -Fe(wb.Inverse(tp).Offset);
            Print($"   {bn,-15} {sn,-10} CL Ramp A2 tie point: off the model CL pier {Z(nTie * 12, 2):+0.00;-0.00}\"; from the WB CL crossing {along:F3} ft along the pier, {perp:F3} ft normal to the WB CL (plan 66'-4 1/8 = 66.344, drawn normal to the WB CL; 107.43 - 41.08 = 66.350)");
        }
        else ps = D(sTxt);
        var dS = (m.S - ps) * 12; var dN = r["n_center_ft"] == "" ? double.NaN : (m.N - D(r["n_center_ft"])) * 12;
        var dLS = (m.LS - D(r["size_s_ft"])) * 12; var dLN = (m.LN - D(r["size_n_ft"])) * 12;
        var ds = new[] { dS, dN, dLS, dLN }.Where(x => !double.IsNaN(x)).ToList();
        nF++; bool ok = ds.All(x => Math.Abs(x) <= 1.0 / 16 + 1e-6); if (ok) okF++;
        string Tx(double x) => double.IsNaN(x) ? "   -  " : $"{Z(x, 2),6:+0.00;-0.00}";
        Print($"   {bn,-15} {sn,-10} #{k + 1}: plan S {FtIn(ps),-13} {D(r["size_s_ft"]),5:0.##} x {D(r["size_n_ft"]),5:0.##} | model S {FtIn(m.S),-13} N {Z(m.N, 3),6:+0.000;-0.000} {m.LS,6:0.###} x {m.LN,6:0.###} | d(in) S {Tx(dS)} N {Tx(dN)} size {Tx(dLS)} {Tx(dLN)}{(ok ? "" : " <-")}");
        if (!ok) flagged.Add($"footing {bn} {sn} #{k + 1}: d S {Z(dS, 2):+0.00;-0.00}\" N {(double.IsNaN(dN) ? "-" : Z(dN, 2).ToString("+0.00;-0.00"))}\" size {Z(dLS, 2):+0.00;-0.00}\" x {Z(dLN, 2):+0.00;-0.00}\" (plan {r["printed_dims"]}), PDF {r["pdf_page"]}");
    }
}
Print($"   footings: {okF}/{nF} within 1/16\" (centre S, N and both sizes)");

Print("-- (5) ties between alignments");
{
    var a = PlanAt("CL I-80", "7602+94.96", 109.63);
    var pI = a.Al.PointAt(a.Sta, a.Off);
    var pR = rg.PointAt(Sta("67502+97.70"), 0);
    Print($"   I-80 7602+94.96 109.63' LT vs Ramp G 67502+97.70 in the model: {Fe((pI - pR).Length) * 12:0.00}\" apart (build-ramp-g.csx fits Ramp G to three ties; this is one of them)");
    if (pierCross.TryGetValue("I-80 WB Unit 2|Pier 11WB", out var w))
    {
        var qR = Cross(rg, w.Q, w.Dir, 0);
        double along = Fe(Vec2.Dot(qR - w.Q, w.Dir));
        Print($"   CL Pier 11WB line (model): WB CL point to Ramp G CL crossing {Math.Abs(along):F3} ft along the line; plan 77'-6 7/8 = 77.573 -> d {Z((Math.Abs(along) - 77.573) * 12, 2):+0.00;-0.00}\"");
        Print($"   Ramp G 67502+97.70 (plan) off the model CL Pier 11WB line by {Z(Fe(Vec2.Dot(pR - w.Q, w.Nrm)) * 12, 2):+0.00;-0.00}\" (normal), the I-80 tie point by {Z(Fe(Vec2.Dot(pI - w.Q, w.Nrm)) * 12, 2):+0.00;-0.00}\"");
        if (pierCross.TryGetValue("Ramp G Unit 4|Pier 19G", out var g))
            Print($"   model CL Pier 19G (Ramp G Unit 4) vs CL Pier 11WB (I-80 Unit 2): angle {Math.Acos(Math.Min(1, Math.Abs(Vec2.Dot(g.Dir, w.Dir)))) * 180 / Math.PI * 3600:0.0}\" of arc, Ramp G crossing {Z(Fe(Vec2.Dot(g.Q - w.Q, w.Nrm)) * 12, 2):+0.00;-0.00}\" off the 11WB line");
    }
}
Print($"== flagged: {flagged.Count}");
foreach (var f in flagged) Print("   " + f);
