// Plan vs model check for MnDOT Br. 02069 (T.H. 65 S.B.), model built by samples/plans/build-br02069.csx:
//   sbm run "samples\plans\check-br02069.csx" "samples\plans\Br02069 TH65.sbm"        (run from the repo root; read-only)
// Checks (tolerance 0.01 ft unless noted; plan values are printed to 0.01 ft / 0.001 ft for coordinates):
//   working points A-F, CP1, CP2 (p3 / p1 / p34, working-points.csv): plan X/Y vs model (bearing points / working line);
//   top of roadway at the working points and at begin / end bridge (p1, p3);
//   bridge seats / pedestal tops (p3, p5, p10, seat-elevations.csv): model bearing seat (bottom of the bearing) and the as-designed
//   pedestal (beam step) top; future pedestals are not modeled (GAP);
//   span 99.25 (p1, p15), beam length 100.50 (p16), begin / end bridge stations (p1), deck area 4220 SF (SOQ p2, nearest SF).
// Prints a pass / fail table and a summary line. Horizontal datum not stated (U1): X/Y are the plan's local grid.
using System.IO;
using SBM.Core.Geometry;

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "br02069-th65");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/br02069-th65 not found above the current directory - run from the repo.");
}
string dir = DataDir();
List<string[]> Csv(string name) => File.ReadAllLines(Path.Combine(dir, name)).Skip(1).Where(l => l.Trim().Length > 0)
    .Select(l => l.Split(',').Select(x => x.Trim()).ToArray()).ToList();
double? Num(string s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;

const string BrName = "Br02069 TH65 SB";
const double Tol = 0.01;
double ft = Ft(1);
var b = Doc.Bridge(BrName);
var al = Doc.Alignment(b.AlignmentName);
Doc.Regenerate();
var fr = Doc.Framing(BrName);
var ps = Doc.Prestressed(BrName);
var sub = Doc.Substructure(BrName);

var rows = new List<(string group, string item, string plan, string model, string diff, string status, string note)>();
int pass = 0, fail = 0, na = 0;
void Row(string group, string item, double? plan, double? model, string note = "", double tol = Tol, string fmt = "F3")
{
    if (plan == null || model == null)
    {
        rows.Add((group, item, plan?.ToString(fmt) ?? "-", model?.ToString(fmt) ?? "-", "-", "N/A", note));
        na++;
        return;
    }
    double d = model.Value - plan.Value;
    bool ok = Math.Abs(d) <= tol + 1e-9;
    if (ok) pass++; else fail++;
    rows.Add((group, item, plan.Value.ToString(fmt), model.Value.ToString(fmt), d.ToString("+0.000;-0.000;0.000"), ok ? "PASS" : "FAIL", note));
}
double Fin(double sta, double off) => b.Deck.FinishedElevation(al, sta, off) / ft;
int GirderAt(double offFt) => (int)Math.Round((offFt + 21) / 6);
int SupportOf(string sta) => Math.Abs(Sta(sta) - b.Supports[0].Station) < Ft(0.01) ? 0 : 1;

// ---------------------------------------------------------------- working points
foreach (var r in Csv("working-points.csv"))
{
    string pt = r[0], sta = r[2];
    double? x = Num(r[4]), y = Num(r[5]), rdwy = Num(r[6]), seat = Num(r[8]);
    double off = Num(r[3]) ?? double.NaN;
    if (x == null || double.IsNaN(off)) { if (rdwy == null) continue; }
    Vec2? mp = null;
    string src = "";
    if (!double.IsNaN(off) && (pt is "A" or "B" or "E" or "F"))
    {
        var br = fr.Supports[SupportOf(sta)].Bearings[GirderAt(off)]!;
        mp = br.TopOfSteel.XY; src = $"CL brg G{GirderAt(off) + 1} (framing)";
    }
    else if (!double.IsNaN(off)) { mp = al.PointAt(Sta(sta), Ft(off)); src = "alignment sta/off"; }
    if (mp is { } p && x != null)
    {
        Row("Working points", $"{pt} X ({sta}, {off:+0.00;-0.00;0.00})", x, p.X / ft, src);
        Row("Working points", $"{pt} Y", y, p.Y / ft, src);
    }
    if (rdwy != null) Row("Top of roadway", $"{pt} {sta} off {(double.IsNaN(off) ? 0 : off):+0.00;-0.00;0.00}", rdwy, Fin(Sta(sta), double.IsNaN(off) ? 0 : Ft(off)), "PG - 0.02|off| (deck finished grade)");
    if (seat != null)
    {
        int g = GirderAt(off), j = SupportOf(sta);
        var bs = ps.BearingSeats.First(s => s.Support == b.Supports[j].Name && s.Girder == g);
        Row("Bridge seat (W.P.)", $"{pt} G{g + 1} {b.Supports[j].Name}", seat, bs.SeatElevation / ft, "model seat = bottom of bearing" + (j == 1 ? "; GAP one bearing height (3 1/4\") per bridge, N plan L = 6 1/2\"" : ""));
    }
}

// ---------------------------------------------------------------- pedestal tops (seat-elevations.csv)
foreach (var r in Csv("seat-elevations.csv"))
{
    string side = r[0];
    double off = Num(r[3])!.Value, plan = Num(r[5])!.Value;
    int j = side == "S" ? 0 : 1;
    string sname = b.Supports[j].Name;
    bool future = r[2].Contains("future");
    if (future)
    {
        Row("Pedestal tops", $"{side} ped {r[1]} off {off:+0.00;-0.00} (future)", plan, null, "GAP: future pedestals not modeled (p5/p10)");
        continue;
    }
    int g = GirderAt(off);
    var unit = sub.Units.First(u => u.Name == sname);
    var ped = unit.Pedestals.FirstOrDefault(p => p.Girder == g);
    Row("Pedestal tops", $"{side} ped {r[1]} G{g + 1} as-designed step top", plan, ped?.SeatZ / ft, "pedestal solid (plan input)");
    var bs = ps.BearingSeats.First(s => s.Support == sname && s.Girder == g);
    Row("Bearing seats", $"{side} G{g + 1} off {off:+0.00;-0.00} model seat", plan, bs.SeatElevation / ft,
        j == 1 ? "GAP: bearing height 3 1/4\" for both ends (N plan 6 1/2\")" : "PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4\"");
}

// ---------------------------------------------------------------- span, beam, deck
Row("Layout", "Span 1 CL brg - CL brg (support stations)", 99.25, (b.Supports[1].Station - b.Supports[0].Station) / ft);
foreach (var beam in ps.Beams)
    Row("Layout", $"{beam.Mark} bearing span", 99.25, beam.BearingSpan / ft);
foreach (var beam in ps.Beams)
    Row("Layout", $"{beam.Mark} beam length o-o", 100.50, beam.Length / ft, "p16 100'-6\"");
{
    var st = fr.Deck!.Outline.Select(q => al.Inverse(q.XY)).ToList();
    Row("Layout", "Begin bridge (end of slab S) station", Sta("248+32.92") / ft, st.Min(s => s.Station) / ft, "p1 248+32.92");
    Row("Layout", "End bridge (end of slab N) station", Sta("249+35.83") / ft, st.Max(s => s.Station) / ft, "p1 249+35.83");
    Row("Layout", "Deck left edge offset", -23.50, st.Min(s => s.Offset) / ft);
    Row("Layout", "Deck right edge offset", 17.50, st.Max(s => s.Offset) / ft);
    double area = fr.Deck.Area / (ft * ft);
    Row("Layout", "Deck area (SF, SOQ nearest SF)", 4220, Math.Round(area), $"model {area:F1} SF = 41'-0\" x 102'-11\"", 0.5, "F1");
}
// Stool at CL brg (p3: 4 1/4") from the model: deck bottom - beam top at the bearing.
foreach (var beam in ps.Beams.Where(x => x.Girder == 0 || x.Girder == 6))
    foreach (var (pnt, end) in new[] { (beam.BearingStart, "S"), (beam.BearingEnd, "N") })
    {
        var so = al.Inverse(pnt.XY);
        double stool = (b.Deck.FinishedElevation(al, so.Station, so.Offset) - b.Deck.Thickness - pnt.Z) / In(1);
        Row("Stool", $"{beam.Mark} stool at CL brg {end} (in)", 4.25, stool, "p3 4 1/4\"; tolerance 1/16\"", 1.0 / 16, "F4");
    }

// ---------------------------------------------------------------- table
int w1 = rows.Max(r => r.item.Length);
Print($"{"Group",-20} {"Item".PadRight(w1)} {"Plan",12} {"Model",12} {"Diff",8}  Status  Note");
foreach (var r in rows)
    Print($"{r.group,-20} {r.item.PadRight(w1)} {r.plan,12} {r.model,12} {r.diff,8}  {r.status,-6}  {r.note}");
Print("");
Print($"SUMMARY: {pass} PASS, {fail} FAIL, {na} N/A (tolerance {Tol} ft; stool 1/16\"; area nearest SF)");
foreach (var g in rows.GroupBy(r => r.group))
    Print($"  {g.Key,-20} {g.Count(r => r.status == "PASS"),3} pass {g.Count(r => r.status == "FAIL"),3} fail {g.Count(r => r.status == "N/A"),3} n/a");
