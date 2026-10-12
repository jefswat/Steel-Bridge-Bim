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
using SBM.Core.Bridge;

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

// ---------------------------------------------------------------- substructure (p.9-12 piers, p.15-17 abutments, p.3 quantities)
// Plan values as printed (0.01 ft elevations, 1/16" dimensions); counts exact. The abutment pile caps are built with the risers 1'-9 past the lower
// beam (user decision 11); the volumes of the other drawn riser candidates stay as INFO evidence.
var subm = Doc.Model(BrName).Substructure;
if (subm == null) Row("Substructure", "substructure generated", 1, 0, "not generated");
else
{
    var U = subm.Units.ToDictionary(u => u.SupportIndex);
    double[][] pierSteps = { new[] { 1117.94, 1118.23, 1118.52, 1118.80, 1119.06 }, new[] { 1118.21, 1118.62, 1119.03, 1119.43, 1119.82 } };
    double[] pJ = { 1117.25, 1117.42 }, pK = { 1118.76, 1119.54 };
    double[][] pLMN = { new[] { 1112.36, 1113.01, 1113.65 }, new[] { 1112.57, 1113.48, 1114.39 } };
    string[][] dimA = { new[] { "17'-4 5/16\"", "18'-0 1/8\"", "18'-7 13/16\"" }, new[] { "17'-6 13/16\"", "18'-5 3/4\"", "19'-4 11/16\"" } };
    for (int pi = 0; pi < 2; pi++)
    {
        if (!U.TryGetValue(pi + 1, out var u)) { Row("Pier (p.9)", $"Pier {pi + 1} built", 1, 0); continue; }
        string P = $"Pier {pi + 1}";
        var blocks = u.PedestalBlocks.OrderBy(b => b.S).ToList();
        Row("Pier (p.9)", $"{P} step blocks (count)", 5, blocks.Count, "", 0, "F0");
        for (int i = 0; i < Math.Min(5, blocks.Count); i++)
            Row("Pier (p.9)", $"{P} step {beamNames[i]} top (table of pier elevations)", pierSteps[pi][i], blocks[i].SeatZ / ft, $"block {blocks[i].Height / In(1):F2}\" above the sloped cap top");
        double s0 = u.CapOutline.Min(p => p.X);
        Row("Pier (p.9)", $"{P} J cap top at the west end", pJ[pi], u.CapTopAt(s0) / ft);
        Row("Pier (p.9)", $"{P} K cap top at the east end (outer face of the 4\" reveal)", pK[pi], u.CapTopAt(s0 + Len("42'-1 5/8\"")) / ft, "cap top line extended over the reveal");
        var cols = u.Columns.OrderBy(c => c.S).ToList();
        Row("Pier (p.9)", $"{P} columns", 3, cols.Count, "", 0, "F0");
        for (int c = 0; c < Math.Min(3, cols.Count); c++)
        {
            Row("Pier (p.9)", $"{P} {"LMN"[c]} cap bottom at column {c + 2}", pLMN[pi][c], u.CapBottomAt(cols[c].S) / ft);
            Row("Pier (p.9)", $"{P} column {c + 2} height DIM A (in)", Len(dimA[pi][c]) / In(1), (cols[c].TopZ - cols[c].BottomZ) / In(1), "", 1.0 / 16, "F4");
        }
        Row("Pier (p.9)", $"{P} maskwall solids", 1, u.Solids.Count(x => x.Kind == SBM.Core.Substructure.SubElementKind.Maskwall), "p.9 / p.11 C-C", 0, "F0");
        var foots = u.Foundations.Where(f => f.Type == FoundationType.PileCap).ToList();
        Row("Pier (p.9)", $"{P} footings", 3, foots.Count, "", 0, "F0");
        for (int fi = 0; fi < foots.Count; fi++)
        {
            var f = foots[fi];
            string F = $"{P} footing {fi + 2}";
            Row("Pier footings (p.9/11)", $"{F} bottom", 1090.00, f.BottomZ / ft);
            Row("Pier footings (p.9/11)", $"{F} top", 1095.00, f.TopZ / ft, "bottom + 5'-0");
            Row("Pier footings (p.9/11)", $"{F} size along the pier (in)", 120, f.SizeS / In(1), "10'-0", 1.0 / 16, "F4");
            Row("Pier footings (p.9/11)", $"{F} size across the pier (in)", 162, f.SizeN / In(1), "13'-6", 1.0 / 16, "F4");
            Row("Pier piles (p.11)", $"{F} piles", 12, f.Piles.Count, "HP12x84", 0, "F0");
            Row("Pier piles (p.11)", $"{F} battered piles (outer rows, away from CL pier)", 6, f.Piles.Count(p => p.IsBattered && Math.Sign(p.TipN - p.N) == Math.Sign(p.N - u.CapMidN)), "U6", 0, "F0");
            foreach (var p in f.Piles.Where(p => p.IsBattered).Take(1))
                Row("Pier piles (p.11)", $"{F} batter H:V x 4", 1, 4 * Math.Sqrt(Math.Pow(p.TipS - p.S, 2) + Math.Pow(p.TipN - p.N, 2)) / (p.TopZ - p.TipZ), "1:4", 0.001, "F4");
            Row("Pier piles (p.11)", $"{F} pile head above the footing bottom (in)", 12, (f.Piles.Max(p => p.TopZ) - f.BottomZ) / In(1), "1'-0 top of pile", 1.0 / 16, "F4");
        }
    }
    // Abutments: piles, bottom elevations, step diagram depths (computed from the step tops and the bottom, cap or no cap)
    var abs = new[] { (k: 0, n: "S. Abut.", bw: 1113.71, be: 1114.60, dl: "3'-9 1/2\"", dr: "3'-8 1/2\"", vol: 18.1, steps: new[] { 1117.50, 1117.71, 1117.92, 1118.11, 1118.31 }),
                      (k: 3, n: "N. Abut.", bw: 1113.91, be: 1115.64, dl: "3'-11 3/4\"", dr: "3'-10 1/2\"", vol: 19.5, steps: new[] { 1117.89, 1118.29, 1118.70, 1119.10, 1119.51 }) };
    foreach (var ab in abs)
    {
        if (!U.TryGetValue(ab.k, out var u) || u.Abutment is not { } geo) { Row("Abutment (p.15-17)", $"{ab.n} built", 1, 0); continue; }
        Row("Abutment (p.15-17)", $"{ab.n} piles (7 on CL brg + 1 wing)", 8, u.Foundations.Sum(f => f.Piles.Count), "HP12x84", 0, "F0");
        double nm = u.CapMidN, sL = geo.LeftEndS(nm), sR = geo.RightEndS(nm);
        Row("Abutment (p.15-17)", $"{ab.n} bottom of footing at the west end", ab.bw, geo.FootingBottom(sL) / ft);
        Row("Abutment (p.15-17)", $"{ab.n} bottom of footing at the east end (level 3'-0)", ab.be, geo.FootingBottom(sR) / ft);
        Row("Abutment (p.15-17)", $"{ab.n} step diagram depth at the west end (in)", Len(ab.dl) / In(1), geo.DepthAtLeftEnd / In(1), "step D - bottom", 1.0 / 16, "F4");
        Row("Abutment (p.15-17)", $"{ab.n} step diagram depth at the east end (in)", Len(ab.dr) / In(1), geo.DepthAtRightEnd / In(1), "step H - bottom", 1.0 / 16, "F4");
        Row("Abutment (p.15-17)", $"{ab.n} stepped pile cap solid built", 1, u.Solids.Any(s => s.Name.EndsWith("pile cap")) ? 1 : 0, "risers 1'-9 past the lower beam (user decision 11)", 0, "F0");
        {
            // Concrete vs the plan quantity 'ABUTMENT FOOTING' (p.17) = pile cap + wing extension footing (the diaphragm is cast with the deck)
            double capCy = u.Solids.Where(s => s.Name.EndsWith("pile cap")).Sum(s => Math.Abs(s.Mesh.Volume())) / 0.764554857984;
            double wingCy = u.Solids.Where(s => s.Name.Contains("wing extension") && s.Name.EndsWith("footing")).Sum(s => Math.Abs(s.Mesh.Volume())) / 0.764554857984;
            Row("Abutment volume (p.17)", $"{ab.n} abutment footing cy = pile cap {capCy:F2} + wing ext. footing {wingCy:F2} (model)", ab.vol, capCy + wingCy, "plan quantity rounded to 0.1 cy; risers per decision 11", 0.05, "F2", info: true);
            var risers = geo.StepRisersS;
            Row("Abutment (p.15-17)", $"{ab.n} step risers built", 4, risers.Count, "decision 11", 0, "F0");
        }
        Row("Abutment (p.15-17)", $"{ab.n} wing extension wall + footing solids", 2, u.Solids.Count(s => s.Kind is SBM.Core.Substructure.SubElementKind.Wingwall or SBM.Core.Substructure.SubElementKind.WingFooting), "", 0, "F0");
        Row("Abutment (p.15-17)", $"{ab.n} CMP casings scheduled", 8, geo.Casings.Count, "bottom UNRESOLVED (EPS, road plans)", 0, "F0");
        // girder S on CL brg (beam chords x the support line)
        var bS = Enumerable.Range(0, 5).Select(g =>
        {
            var c = fr.Girders[g].Spans[ab.k == 0 ? 0 : 2];
            Numerics.IntersectLines(fr.Supports[ab.k].AlignmentPoint, fr.Supports[ab.k].Direction, c.Start, (c.End - c.Start).Normalized(), out double t, out _);
            return t;
        }).ToList();
        if (bS.Count == 5)
        {
            double Vol(Func<int, double> riser)
            {
                double v = 0; int n = 800;
                for (int i = 0; i < n; i++)
                {
                    double s = sL + (sR - sL) * (i + 0.5) / n;
                    int k = 0; while (k < 4 && s > riser(k)) k++;
                    v += (Ft(ab.steps[k]) - geo.FootingBottom(s)) * Ft(3) * (sR - sL) / n;
                }
                return v / 0.764554857984;
            }
            Row("Abutment volume (p.17)", $"{ab.n} cap cy, risers 1'-9 past the lower beam (rear elev. p.15/16, scaled)", ab.vol, Vol(k => bS[k] + Ft(1.75)), "pile cap only, no wing footing", 0.05, "F2", info: true);
            Row("Abutment volume (p.17)", $"{ab.n} cap cy, risers 3'-4 past the lower beam (step diagram p.17, scaled)", ab.vol, Vol(k => bS[k] + Ft(3) + In(4)), "", 0.05, "F2", info: true);
            Row("Abutment volume (p.17)", $"{ab.n} cap cy, risers at mid-bay", ab.vol, Vol(k => (bS[k] + bS[k + 1]) / 2), "", 0.05, "F2", info: true);
            double wing = u.Solids.Where(s => s.Name.Contains("wing extension") && s.Name.EndsWith("footing")).Sum(s => Math.Abs(s.Mesh.Volume())) / 0.764554857984;
            Row("Abutment volume (p.17)", $"{ab.n} wing extension footing cy (model)", 0, wing, "add to the cap candidates if the plan quantity includes it", 0.05, "F2", info: true);
            // Riser offset x past the lower beam's CL (same for every riser) that reproduces the plan 'abutment footing' quantity (cap + wing footing):
            // the quantity is rounded to 0.1 cy, so x is only determined to about +- (0.05 cy / sum of the step rises x 3'-0) - EVIDENCE, not a dimension.
            double lo = 0, hi = Ft(8);
            for (int it = 0; it < 60; it++) { double mid = (lo + hi) / 2; if (Vol(k => bS[k] + mid) + wing > ab.vol) lo = mid; else hi = mid; }
            double rises = Enumerable.Range(0, 4).Sum(k => Ft(ab.steps[k + 1] - ab.steps[k]));
            double tolX = 0.05 * 0.764554857984 / (rises * Ft(3));
            Row("Abutment volume (p.17)", $"{ab.n} riser offset past the lower beam CL that matches {ab.vol} cy (in)", 21, (lo + hi) / 2 / In(1), $"+-{tolX / In(1):F1}\" from the 0.1 cy rounding; plan value 21 = the 1'-9 drawn on the rear elevation", 0, "F1", info: true);
        }
    }
    // Concrete diaphragms (diaphragms-woodbury.csx): bottoms on the substructure tops - abutment risers per decision 11, pier step blocks p.9
    {
        var dm = Doc.Model(BrName).Prestressed?.ConcreteDiaphragms;
        foreach (var nm in new[] { "S. Abut.", "Pier 1", "Pier 2", "N. Abut." })
        {
            var d = dm?.Diaphragms.FirstOrDefault(x => x.Support == nm);
            Row("Diaphragms (p.15-20)", $"{nm} diaphragm built", 1, d == null ? 0 : 1, "", 0, "F0");
            if (d == null) continue;
            Row("Diaphragms (p.15-20)", $"{nm} diaphragm flagged UNRESOLVED (bottom)", 0, d.Assumption == null ? 0 : 1, d.Assumption?.Reason ?? "", 0, "F0");
            Row("Diaphragms (p.15-20)", $"{nm} diaphragm concrete cy ({string.Join(", ", d.Parts.Select(p => $"{p.Name} {p.Volume / 0.764554857984:F2}"))})", 0, d.Volume / 0.764554857984, "model; plan quantity is in the deck concrete", 0, "F2", info: true);
        }
        foreach (var dg in (dm?.Diagnostics ?? new()).Where(x => x.Message.Contains("step blocks")))
            Print("diaphragm: " + dg.Message);
    }
    // Piles: plan quantity 11,440 LF = 8 x 155 + 8 x 150 + 72 x 125 (contract lengths, p.3 / p.12 / p.17)
    var allPiles = subm.Units.SelectMany(x => x.Foundations).SelectMany(f => f.Piles).ToList();
    Row("Quantities (p.3)", "Piles HP12x84 (count)", 88, allPiles.Count, "72 pier + 16 abutment", 0, "F0");
    Row("Quantities (p.3)", "Piles HP12x84 (LF)", 11440, allPiles.Sum(p => p.Length) / ft, "contract lengths along the pile axis", 0.5, "F1");
    // Rebar: placed plan marks vs the bar-list weights (p.12) per pier; marks without a 3D rule = INFO (schedule only)
    foreach (var r in File.ReadAllLines(Path.Combine(dir, "bar-lists.csv")).Skip(1).Where(l => l.StartsWith("Pier ")))
    {
        var c = System.Text.RegularExpressions.Regex.Matches(r, "\"[^\"]*\"|[^,]+").Select(m => m.Value.Trim('"')).ToArray();
        string unitName = c[0], mark = c[2];
        double plan = double.Parse(c[7].Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture);
        var mk = subm.Marks.Where(m => m.Unit == unitName && System.Text.RegularExpressions.Regex.IsMatch(m.Location, $@"(^|\s){mark}(\s|$)")).ToList();
        if (mk.Count == 0) { Row("Pier rebar (p.12)", $"{unitName} {mark} weight (lb)", plan, 0, "not placed in 3D (schedule only)", 0, "F0", info: true); continue; }
        Row("Pier rebar (p.12)", $"{unitName} {mark} weight (lb)", plan, mk.Sum(m => m.TotalMass) / 0.45359237, $"{mk.Sum(m => m.Count)} bars", Math.Max(1, plan * 0.01), "F0");
    }
    var gen = subm.Marks.Where(m => (m.Unit == "Pier 1" || m.Unit == "Pier 2") && m.Location.Contains("column")).ToList();
    Row("Pier rebar (p.12)", "column bars of the generic column pipeline (lb, both piers; plan 10d1-10d5 + 10d2 + 5e1-5e3 + 6d13-6d16)",
        2 * (2580 + 5753 + 2661 + 2741 + 1596 + 1666 + 430 + 257 + 265 + 137 + 173), gen.Sum(m => m.TotalMass) / 0.45359237, "verticals + dowels + generic #5 ties; INFO", 1, "F0", info: true);
}
// ---------------------------------------------------------------- steel diaphragm W21x50 + W14x38 (p.32 note: only between beam lines G and H over the traveled roadway, span 2)
{
    var parts = Doc.Model(BrName).Steel?.Parts ?? new();
    foreach (var shape in new[] { "W21x50", "W14x38" })
    {
        var ps = parts.Where(p => p.Description.Contains(shape)).ToList();
        Row("Steel diaphragms (p.31/32)", $"{shape} members", 1, ps.Count, string.Join("; ", ps.Select(p => p.Assembly)), 0, "F0");
    }
}

// ---------------------------------------------------------------- terrain (3DEP lidar 2020, 1 m, NAVD88) vs plan ground - INFO (placement is APPROXIMATE, U1)
{
    var pts = new (string what, string sta, double off, double elev)[]
    {
        ("CL I-29 x CL Floyd (Floyd grade at 103+29.91, p.6 VC)", "464+51.66", 0, 1096.78 + 0.5 - 0.01 * 79.91 + 0.02 / 200 * 79.91 * 79.91),
        ("S berm A1", "464+07.48", -67.58, 1098.01), ("S berm A2", "463+89.85", 27.00, 1097.85), ("S berm A3", "463+82.61", 67.58, 1098.36),
        ("S berm B1", "463+61.58", -67.58, 1113.17), ("S berm B2", "463+44.56", 27.00, 1115.38), ("S berm B3", "463+37.29", 67.58, 1116.43),
        ("N berm A1", "465+29.97", -67.58, 1097.94), ("N berm A2", "465+07.18", 27.00, 1097.96), ("N berm A3", "464+97.97", 67.58, 1098.63),
        ("N berm B1", "465+82.29", -67.58, 1111.69), ("N berm B2", "465+57.26", 27.00, 1115.70), ("N berm B3", "465+47.15", 67.58, 1117.75),
    };
    foreach (var p in pts)
    {
        var q = al.PointAt(Sta(p.sta), Ft(p.off));
        double? z = Doc.GroundAt(q);
        Row("Terrain 3DEP (p.6)", $"{p.what} STA {p.sta} {Math.Abs(p.off):F2}' {(p.off < 0 ? "LT" : "RT")}", p.elev, z.HasValue ? z.Value / ft : double.NaN, "lidar 2020 vs plan; INFO", 0.5, "F2", info: true);
    }
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
