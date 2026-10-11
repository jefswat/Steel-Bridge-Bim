// Plan vs model check for Iowa DOT Design 817 (I-29 NB over Floyd Blvd, Woodbury County), model built by build-woodbury.csx:
//   sbm run samples\plans\check-woodbury.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"      (run from the repo root; read-only)
// Plan data: docs/plan-data/woodbury-i29-floyd/SUMMARY.md and its CSVs. Tolerance 0.01 ft (T: 1/16"); plan values as printed.
// Checks:
//   staking (p.7): support lines x extended tangent of CL NB roadway (station / offset of CL I-29); x curved CL NB (p.6);
//   spans along curved CL NB (p.6/p.21), along the extended tangent (p.7), beam CL-CL bearing along CL NB (p.21);
//   beam spacing along the abutment bearing lines (p.21); CL NB profile at the supports (p.6);
//   top of deck elevations 8 lines x 27 points (p.33, top-of-deck-elevations.csv): points at 6 / 12 / 6 equal spaces along each
//     line between the bearing lines (beam lines: between the beam's points on its bearing lines);
//   beam line haunch elevations 5 x 27 (p.34, haunch-elevations.csv): model finished grade - 8" structural deck + the plan's
//     anticipated deflection due to deck (p.34 misc table; deflections are a plan value, not modeled);
//   deck thickness at beams T (p.24, deck-thickness-at-beams.csv): at the bearings (model haunch inputs) and at the quarter
//     points (model beams are straight, NO camber: interior points are reported as INFO, not pass / fail);
//   beam seats (p.25, beam-information.csv): bottom of beam at CL bearing - bearing (3" abutments, 1" piers).
using System.IO;
using SBM.Core.Checks;
using SBM.Core.Geometry;

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "woodbury-i29-floyd");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/woodbury-i29-floyd not found above the current directory - run from the repo.");
}
string dir = DataDir();
List<string[]> Csv(string name) => File.ReadAllLines(Path.Combine(dir, name)).Skip(1).Where(l => l.Trim().Length > 0)
    .Select(l => l.Split(',').Select(x => x.Trim()).ToArray()).ToList();
double Frac(string s)   // "9 7/8", "1/2", "1 11/16", "2 1/2 (0.208)"
{
    s = s.Split('(')[0].Trim();
    double v = 0;
    foreach (var t in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        v += t.Contains('/') ? double.Parse(t.Split('/')[0]) / double.Parse(t.Split('/')[1]) : double.Parse(t, System.Globalization.CultureInfo.InvariantCulture);
    return v;
}
double Num(string s) => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

const string BrName = "I-29 NB over Floyd Blvd - Stage 1";
const double Tol = 0.01;
double ft = Ft(1);
var b = Doc.Bridge(BrName);
var al = Doc.Alignment(b.AlignmentName);
Doc.Regenerate();
var fr = Doc.Framing(BrName);
int NG = b.Girders.Count, NS = b.Supports.Count;
string[] beamNames = { "D", "E", "F", "G", "H" };

var rows = new List<(string group, string item, string plan, string model, string diff, string status, string note)>();
var worst = new Dictionary<string, (double d, string item)>();
void Row(string group, string item, double plan, double model, string note = "", double tol = Tol, string fmt = "F2", bool info = false)
{
    double d = model - plan;
    bool ok = Math.Abs(d) <= tol + 1e-9;
    string st = info ? "INFO" : ok ? "PASS" : "FAIL";
    rows.Add((group, item, plan.ToString(fmt), model.ToString(fmt), d.ToString(fmt == "F2" ? "+0.000;-0.000;0.000" : "+0.0000;-0.0000;0.0000"), st, note));
    if (!worst.TryGetValue(group, out var w) || Math.Abs(d) > Math.Abs(w.d)) worst[group] = (d, item);
}
double Fin(Vec2 p) { var so = al.Inverse(p); return b.Deck.FinishedElevation(al, so.Station, so.Offset); }

// ---------------------------------------------------------------- helpers: lines and offset curves
// Where the line (q, dir) crosses the constant offset curve o: alignment station.
double CrossOffset(Vec2 q, Vec2 dir, double o)
{
    var n = dir.Left;
    double s = al.Inverse(q).Station;
    double F(double x) => Vec2.Dot(al.PointAt(x, o) - q, n);
    for (int it = 0; it < 50; it++)
    {
        double f = F(s), h = 1e-4;
        double df = (F(s + h) - F(s - h)) / (2 * h);
        s -= f / df;
        if (Math.Abs(f) < 1e-11) break;
    }
    return s;
}
// Arc length along the offset curve o between two stations, and the station at a given arc length from s0.
double Arc(double s0, double s1, double o, int n = 400)
{
    double sum = 0; var prev = al.PointAt(s0, o);
    for (int k = 1; k <= n; k++) { var p = al.PointAt(s0 + (s1 - s0) * k / n, o); sum += Vec2.Distance(prev, p); prev = p; }
    return sum;
}
double StationAtArc(double s0, double o, double arc)
{
    double s = s0 + arc;
    for (int it = 0; it < 30; it++)
    {
        double f = Arc(s0, s, o, 100) - arc;
        if (Math.Abs(f) < 1e-9) break;
        s -= f * 0.99;
    }
    return s;
}
double clNb = Ft(27.00);
var back = Vec2.FromAngle(al.AngleAt(al.StartStation));      // back tangent direction
var tanOrigin = al.PointAt(al.StartStation, clNb);            // extended tangent of CL NB roadway

// Layout ("CL bearing") line of span j at its begin / end: through the girders' layout points, parallel to the support line.
(Vec2 p, Vec2 dir) BearingLine(int span, bool atEnd)
{
    var c = fr.Girders[0].Spans[span];
    var pt = (atEnd ? c.LayoutEnd : c.LayoutBegin)?.XY ?? (atEnd ? c.End : c.Start);
    return (pt, fr.Supports[atEnd ? span + 1 : span].Direction);
}

// ---------------------------------------------------------------- staking (p.7) and situation plan (p.6)
string[] stakSta = { "463+39.99", "463+95.87", "465+00.94", "465+60.17" };
double[] stakOff = { 27.00, 27.02, 29.88, 33.62 };
string[] curSta = { "463+39.99", "463+95.88", "465+01.60", "465+61.85" };
var stakePts = new List<Vec2>();
for (int k = 0; k < NS; k++)
{
    var sl = fr.Supports[k];
    if (!Numerics.IntersectLines(sl.AlignmentPoint, sl.Direction, tanOrigin, back, out double t, out _)) continue;
    var q = sl.AlignmentPoint + sl.Direction * t;
    stakePts.Add(q);
    var so = al.Inverse(q);
    Row("Staking (p.7)", $"{sl.Name} x ext. tangent CL NB: station", Sta(stakSta[k]) / ft, so.Station / ft);
    Row("Staking (p.7)", $"{sl.Name} x ext. tangent CL NB: offset", stakOff[k], so.Offset / ft);
    Row("Staking (p.6)", $"{sl.Name} x curved CL NB (27.00' RT): station", Sta(curSta[k]) / ft, CrossOffset(sl.AlignmentPoint, sl.Direction, clNb) / ft);
}
string[] tanSpans = { "55'-11 7/8\"", "106'-4 5/8\"", "60'-1 13/16\"" };
string[] curSpans = { "55'-11 15/16\"", "106'-11 7/16\"", "60'-11 1/2\"" };
string[] brgSpans = { "54'-11 15/16\"", "104'-11 7/16\"", "59'-11 1/2\"" };
double total = 0;
for (int j = 0; j < NS - 1; j++)
{
    Row("Spans", $"Span {j + 1} along extended tangent (p.7)", Len(tanSpans[j]) / ft, Vec2.Distance(stakePts[j], stakePts[j + 1]) / ft);
    double s0 = CrossOffset(fr.Supports[j].AlignmentPoint, fr.Supports[j].Direction, clNb);
    double s1 = CrossOffset(fr.Supports[j + 1].AlignmentPoint, fr.Supports[j + 1].Direction, clNb);
    double arc = Arc(s0, s1, clNb);
    total += arc;
    Row("Spans", $"Span {j + 1} along curved CL NB (p.6, p.21)", Len(curSpans[j]) / ft, arc / ft);
    var (bp, bd) = BearingLine(j, false);
    var (ep, ed) = BearingLine(j, true);
    double b0 = CrossOffset(bp, bd, clNb), b1 = CrossOffset(ep, ed, clNb);
    Row("Spans", $"Span {j + 1} beam CL-CL bearing along CL NB (p.21)", Len(brgSpans[j]) / ft, Arc(b0, b1, clNb) / ft);
}
Row("Spans", "Total CL abut brg to CL abut brg along CL NB (p.6)", Len("223'-10 7/8\"") / ft, total / ft);

// Beam spacing along the abutment bearing lines (p.21: 4 @ 8'-1 7/8 both abutments).
for (int i = 0; i + 1 < NG; i++)
{
    var a0 = fr.Girders[i].Spans[0]; var a1 = fr.Girders[i + 1].Spans[0];
    Row("Spacing along brg", $"S. Abut. {beamNames[i]}-{beamNames[i + 1]}", Len("8'-1 7/8\"") / ft, Vec2.Distance(a0.LayoutBegin!.Value.XY, a1.LayoutBegin!.Value.XY) / ft);
    var z0 = fr.Girders[i].Spans[^1]; var z1 = fr.Girders[i + 1].Spans[^1];
    Row("Spacing along brg", $"N. Abut. {beamNames[i]}-{beamNames[i + 1]}", Len("8'-1 7/8\"") / ft, Vec2.Distance(z0.LayoutEnd!.Value.XY, z1.LayoutEnd!.Value.XY) / ft);
}
// Beam D from CL NB along CL bearing (p.21) - the build input; reported as a round-trip.
string[][] dOff = { new[] { "3'-1 11/16\"", "3'-3 1/2\"" }, new[] { "2'-10 15/16\"", "3'-0 11/16\"" }, new[] { "2'-7 3/8\"", "2'-8 1/16\"" } };
for (int j = 0; j < NS - 1; j++)
    foreach (bool e in new[] { false, true })
    {
        // Aligned dimension from the W.P. (CL support x CL NB, p.21 note) along the CL bearing: projection on the bearing line.
        var (lp, ld) = BearingLine(j, e);
        var sl = fr.Supports[e ? j + 1 : j];
        var w = al.PointAt(CrossOffset(sl.AlignmentPoint, sl.Direction, clNb), clNb);
        var c = fr.Girders[0].Spans[j];
        var pD = (e ? c.LayoutEnd : c.LayoutBegin)!.Value.XY;
        Row("Beam D on brg line", $"Span {j + 1} {(e ? "end" : "begin")}: W.P. to beam D along brg", Len(dOff[j][e ? 1 : 0]) / ft, Vec2.Dot(pD - w, ld) / ft);
    }
// CL pier to CL beam bearing (p.21): 1'-0 along the beam; printed perpendicular to the pier 11 13/16 / 11 3/4 (pier 1 back / ahead),
// 11 3/4 / 11 5/8 (pier 2); the 1'-0 also on the CL NB dimension string (INFO: along CL NB).
string[][] perpBrg = { new[] { "11 13/16", "11 3/4" }, new[] { "11 3/4", "11 5/8" } };
for (int k = 1; k <= 2; k++)
    foreach (bool ahead in new[] { false, true })
    {
        var sl = fr.Supports[k];
        int span = ahead ? k : k - 1;
        var (lp, ld) = BearingLine(span, !ahead);
        var nrm = sl.Direction.Left;
        double perp = Math.Abs(Vec2.Dot(lp - sl.AlignmentPoint, nrm));
        Row("Pier brg lines (p.21)", $"Pier {k} {(ahead ? "ahead" : "back")}: CL pier to CL brg perpendicular (in)", Frac(perpBrg[k - 1][ahead ? 1 : 0]), perp / In(1), "", 1.0 / 16, "F4");
        var g = fr.Girders[0].Spans[span];
        var pB = (ahead ? g.LayoutBegin : g.LayoutEnd)!.Value.XY;
        var t = (g.End - g.Start).Normalized();
        double along = perp / Math.Abs(Vec2.Dot(t, nrm));
        Row("Pier brg lines (p.21)", $"Pier {k} {(ahead ? "ahead" : "back")}: CL pier to CL brg along the beam (in)", 12, along / In(1), "", 1.0 / 16, "F4");
        double sP = CrossOffset(sl.AlignmentPoint, sl.Direction, clNb), sB = CrossOffset(lp, ld, clNb);
        Row("Pier brg lines (p.21)", $"Pier {k} {(ahead ? "ahead" : "back")}: CL pier to CL brg along CL NB (in)", 12, Arc(Math.Min(sP, sB), Math.Max(sP, sB), clNb) / In(1), "1'-0 on the CL NB string", 1.0 / 16, "F4", info: true);
    }
// Beam CL-CL bearing along each beam (chord, 3D along the sloped beam) vs the STANDARD beam length (p.27; Iowa standard sheets
// 4732 BTD55 / 4733 BTD60 / 4742 BTD105: 55'-0 / 60'-0 / 105'-0 CL to CL bearings, 8" end to CL bearing).
double[] stdBrg = { Ft(55), Ft(105), Ft(60) };
string[] stdName = { "BTD55", "BTD105", "BTD60" };
for (int j = 0; j < NS - 1; j++)
    for (int g = 0; g < NG; g++)
    {
        var c = fr.Girders[g].Spans[j];
        double L = (c.LayoutEnd!.Value - c.LayoutBegin!.Value).Length;
        Row("Beam chord vs std", $"Span {j + 1} beam {beamNames[g]} CL-CL brg (in)", stdBrg[j] / In(1), L / In(1), stdName[j], 1.0 / 16, "F4");
    }

// CL NB profile at the supports (p.6): CL brg (abutments) / CL pier on curved CL NB.
double[] prof = { 1122.92, 1123.25, 1123.49, 1123.27 };
for (int k = 0; k < NS; k++)
{
    var sl = fr.Supports[k];
    double s = CrossOffset(sl.AlignmentPoint, sl.Direction, clNb);
    Row("Profile (p.6)", $"CL NB at {sl.Name}", prof[k], b.Deck.FinishedElevation(al, s, clNb) / ft);
}

// ---------------------------------------------------------------- top of deck (p.33)
int[] eq = { 6, 12, 6 };
// Points along a constant offset line o: 1 + 6, 13, 6 equal arc divisions between the bearing lines.
List<Vec2> OffsetLinePoints(double o)
{
    var list = new List<Vec2>();
    for (int j = 0; j < NS - 1; j++)
    {
        var (bp, bd) = BearingLine(j, false);
        var (ep, ed) = BearingLine(j, true);
        double s0 = CrossOffset(bp, bd, o), s1 = CrossOffset(ep, ed, o);
        double L = Arc(s0, s1, o);
        for (int k = 0; k <= eq[j]; k++) list.Add(al.PointAt(k == 0 ? s0 : k == eq[j] ? s1 : StationAtArc(s0, o, L * k / eq[j]), o));
    }
    return list;
}
List<Vec2> BeamPoints(int g)
{
    var list = new List<Vec2>();
    for (int j = 0; j < NS - 1; j++)
    {
        var c = fr.Girders[g].Spans[j];
        var p0 = c.LayoutBegin!.Value.XY; var p1 = c.LayoutEnd!.Value.XY;
        for (int k = 0; k <= eq[j]; k++) list.Add(p0 + (p1 - p0) * ((double)k / eq[j]));
    }
    return list;
}
var linePts = new Dictionary<string, List<Vec2>>
{
    ["CL NB roadway & west edge of deck"] = OffsetLinePoints(clNb),
    ["East gutter line"] = OffsetLinePoints(clNb + Ft(36)),
    ["East edge of deck"] = OffsetLinePoints(clNb + Len("37'-7\"")),
};
for (int g = 0; g < NG; g++) linePts[$"Beam line {beamNames[g]}"] = BeamPoints(g);
foreach (var r in Csv("top-of-deck-elevations.csv"))
{
    var pts = linePts[r[0]];
    int pt = int.Parse(r[1]);
    Row("Top of deck (p.33)", $"{r[0]} pt {pt}", Num(r[3]), Fin(pts[pt - 1]) / ft);
}

// ---------------------------------------------------------------- beam line haunch elevations (p.34)
var tod = Csv("top-of-deck-elevations.csv").Where(r => r[0].StartsWith("Beam line ")).ToDictionary(r => (r[0].Substring(10), int.Parse(r[1])), r => r[3]);
var defl = Csv("haunch-misc-data.csv").ToDictionary(r => int.Parse(r[0]), r => Frac(r[1]));
foreach (var r in Csv("haunch-elevations.csv"))
{
    int g = Array.IndexOf(beamNames, r[0]), pt = int.Parse(r[1]);
    double model = Fin(linePts[$"Beam line {r[0]}"][pt - 1]) - b.Deck.Thickness + In(defl[pt]);
    // The plan's own residual: p.33 top of deck - 8" + p.34 deflection - p.34 haunch elevation (rounded tables).
    double own = Num(tod[(r[0], pt)]) - 8.0 / 12 + defl[pt] / 12 - Num(r[2]);
    Row("Haunch elev (p.34)", $"{r[0]} pt {pt}", Num(r[2]), model / ft, $"FG - 8\" + defl {defl[pt]:0.###}\"; plan p.33 - 8\" + defl - p.34 = {own:+0.000;-0.000;0.000}");
}

// ---------------------------------------------------------------- deck thickness at beams T (p.24) and seats (p.25)
// Locations: bearings (S abut, pier back/ahead, N abut) and quarter points Q R S (span 1), U V W (span 2), X Y Z (span 3).
var tRows = Csv("deck-thickness-at-beams.csv");
string[] quarter = { "Q", "R", "S", "U", "V", "W", "X", "Y", "Z" };
foreach (var r in tRows)
{
    int g = Array.IndexOf(beamNames, r[0]);
    string loc = r[1];
    double plan = Frac(r[2]);
    int span; double u; bool interior = false;
    switch (loc)
    {
        case "CL S abut brg": span = 0; u = 0; break;
        case "CL pier 1 brg (back)": span = 0; u = 1; break;
        case "CL pier 1 brg (ahead)": span = 1; u = 0; break;
        case "CL pier 2 brg (back)": span = 1; u = 1; break;
        case "CL pier 2 brg (ahead)": span = 2; u = 0; break;
        case "CL N abut brg": span = 2; u = 1; break;
        default:
            int qi = Array.IndexOf(quarter, loc);
            span = qi / 3; u = (qi % 3 + 1) / 4.0; interior = true; break;
    }
    var c = fr.Girders[g].Spans[span];
    var a = c.LayoutBegin!.Value; var z = c.LayoutEnd!.Value;
    var p = a + (z - a) * u;
    double t = (Fin(p.XY) - p.Z) / In(1);
    Row(interior ? "T quarter pts (p.24)" : "T at bearings (p.24)", $"{r[0]} {loc} (in)", plan, t,
        interior ? "model beam straight, no camber" : "", 1.0 / 16, "F4", info: interior);
}
var bi = Csv("beam-information.csv");
double depth = Ft(4.5);   // BTD depth 4'-6 (p.28); the placeholder shape must have the same depth
foreach (var r in bi)
{
    int g = Array.IndexOf(beamNames, r[1]), j = int.Parse(r[2]) - 1;
    var c = fr.Girders[g].Spans[j];
    Row("Beam seats (p.25)", $"{r[1]} span {j + 1} start", Num(r[8]), (c.LayoutBegin!.Value.Z - depth - In(Num(r[6]))) / ft, $"top - 4'-6 - {r[6]}\" brg");
    Row("Beam seats (p.25)", $"{r[1]} span {j + 1} end", Num(r[9]), (c.LayoutEnd!.Value.Z - depth - In(Num(r[7]))) / ft, $"top - 4'-6 - {r[7]}\" brg");
}

// ---------------------------------------------------------------- one elevation chain (shows the PGL offset)
var chain = ElevationCheck.Grade(al, CrossOffset(fr.Supports[1].AlignmentPoint, fr.Supports[1].Direction, clNb), clNb, Doc.Units, "CL NB at Pier 1", b.Deck);
Print(ElevationCheck.ToText(new[] { chain }, Doc.Units));

// ---------------------------------------------------------------- table
bool verbose = Args.Contains("--all");
int w1 = rows.Max(r => r.item.Length);
Print($"{"Group",-22} {"Item".PadRight(w1)} {"Plan",10} {"Model",10} {"Diff",9}  Status  Note");
foreach (var r in rows.Where(r => verbose || r.status != "PASS" || !r.group.StartsWith("Top of deck") && !r.group.StartsWith("Haunch")))
    Print($"{r.group,-22} {r.item.PadRight(w1)} {r.plan,10} {r.model,10} {r.diff,9}  {r.status,-6}  {r.note}");
Print("");
int pass = rows.Count(r => r.status == "PASS"), fail = rows.Count(r => r.status == "FAIL"), info = rows.Count(r => r.status == "INFO");
Print($"SUMMARY: {pass} PASS, {fail} FAIL, {info} INFO (tolerance {Tol} ft; T 1/16\"; passing top-of-deck / haunch rows hidden, --all lists them)");
foreach (var g in rows.GroupBy(r => r.group))
    Print($"  {g.Key,-22} {g.Count(r => r.status == "PASS"),3} pass {g.Count(r => r.status == "FAIL"),3} fail {g.Count(r => r.status == "INFO"),3} info   worst {worst[g.Key].d:+0.0000;-0.0000} ({worst[g.Key].item})");
