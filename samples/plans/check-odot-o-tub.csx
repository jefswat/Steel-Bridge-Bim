// ODOT Bridge O spans 14-16 (steel tub unit) - plan vs model checks. Run on the model built by build-odot-o-tub.csx:
//   sbm run samples\plans\check-odot-o-tub.csx "samples\plans\ODOT Bridge O tub unit.sbm"
// Reports (never adjusts) at 1/16" (lengths) / 0.01 ft (elevations): working points, profile at the supports, girder spans CL brg to CL brg,
// field splice locations and the plan's derived plate / zone strings (p.278 O-24, p.279 O-25), splice bolt counts (p.281 O-27), bracing counts
// (p.273 O-19, p.282 O-28), dead load deflections (p.280 O-26), bearing H / bevels (p.271 O-17, p.293 O-39) and seats vs the pedestal elevations
// of the pier sheets (p.262, p.264, p.266, Bridge N p.180), steel weight (no plan quantity in the volume).
// Plan values that the build script ENTERS are not re-checked here; every plan dimension below is one the model has to reproduce from other inputs.
using System.Globalization;
using System.IO;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;

const string BrName = "Bridge O spans 14-16 (steel tub unit)";
var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == BrName) ?? throw new Exception("bridge not found - run build-odot-o-tub.csx first");
var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
var fr = Doc.Framing(BrName);
var st = Doc.Steel(BrName);
var tr = st.Tub ?? throw new Exception("no tub report (not a tub bridge?)");
const double Sixteenth = 0.0254 / 16;
double ToFt(double m) => m / 0.3048;
double ToIn(double m) => m / 0.0254;
double F(double ft, double inch = 0, double num = 0, double den = 1) => (ft * 12 + inch + num / den) * 0.0254;
string FI(double m)
{
    double i = Math.Round(Math.Abs(m) / 0.0254 * 16) / 16;
    int ft = (int)Math.Floor(i / 12 + 1e-9); double r = i - ft * 12;
    int whole = (int)Math.Floor(r + 1e-9); int n16 = (int)Math.Round((r - whole) * 16);
    int num = n16, den = 16; while (num > 0 && num % 2 == 0) { num /= 2; den /= 2; }
    return $"{(m < 0 ? "-" : "")}{ft}'-{whole}{(num > 0 ? $" {num}/{den}" : "")}\"";
}
int ok = 0, bad = 0;
void Row(string item, string plan, string model, bool good, string note = "")
{
    if (good) ok++; else bad++;
    Print($"{(good ? "OK  " : "DIFF")} {item}: plan {plan} | model {model}{(note == "" ? "" : "  - " + note)}");
}
void Len(string item, double plan, double model, string note = "") =>
    Row(item, FI(plan), $"{FI(model)} ({(model - plan >= 0 ? "+" : "")}{ToIn(model - plan):F3}\")", Math.Abs(model - plan) <= Sixteenth + 1e-6, note);
void Info(string s) => Print("INFO " + s);

string dataDir = FindData();
string FindData()
{
    for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "vol2-o-steel-tub-unit-spans14-16");
        if (Directory.Exists(p)) return p;
    }
    throw new Exception("docs/plan-data/vol2-o-steel-tub-unit-spans14-16 not found");
}
double Dbl(string s) => double.Parse(s, CultureInfo.InvariantCulture);

Print($"== {br.Name}: {st.Parts.Count} parts, {st.Assemblies.Count} assemblies, {st.Bolts.Count} bolts, {st.Studs.Count} studs");

// ---------------------------------------------------------------- 1. working points (p.259 O-5) and profile (p.254 O-1)
Print("== working points (p.259 staking, N / E local grid) and profile grade (p.254)");
foreach (var (name, sta, n, e) in new[] { ("Pier 14", "302+93.00", 193275.14, 2113643.09), ("Pier 15", "304+54.00", 193434.63, 2113663.38),
             ("Pier 16", "306+15.00", 193595.16, 2113654.37), ("Abut 1 F.F. bkwl", "306+73.25", 193652.45, 2113643.91) })
{
    var q = al.PointAt(Sta(sta));
    double dn = ToFt(q.Y) - n, de = ToFt(q.X) - e;
    Row($"{name} {sta} N / E", $"{n:F2} / {e:F2}", $"{ToFt(q.Y):F2} / {ToFt(q.X):F2}", Math.Abs(dn) <= 0.0151 && Math.Abs(de) <= 0.0151, "+-0.01 ft (plan precision)");
}
{
    var q = al.PointAt(Sta("301+53.72"));
    Info($"Pier 13 301+53.72: model N {ToFt(q.Y):F2} E {ToFt(q.X):F2}; Bridge N p.148 prints N 193142.24 E 2113535.71 (Ramp 44E working point) - not on the R 881.47 arc through piers 14 / 16 (UNRESOLVED in the build).");
}
foreach (var (s, z) in new[] { ("301+53.72", 1135.33), ("302+93.00", 1132.14), ("304+54.00", 1130.87), ("306+15.00", 1129.96), ("306+74.00", 1129.62) })
{
    double m = ToFt(al.Profile!.ElevationAt(Sta(s)));
    Row($"profile grade {s}", $"{z:F2}", $"{m:F3}", Math.Abs(m - z) <= 0.0051 + 1e-9, "+-0.005 ft (plan 0.01)");
}

// ---------------------------------------------------------------- 2. spans, splices, plate strings (p.278 O-24 G1, p.279 O-25 G2)
Print("== girder spans CL brg to CL brg (p.278 / p.279) and bracing panel strings (p.273)");
double Brg(int g, int j) => tr.BearingS[g][j];
double Fs(int g, int n) => tr.SpliceS[g][n];
var spanPlan = new[] {
    new[] { F(141, 4), F(158, 6, 3, 4), F(157, 6, 7, 8) },
    new[] { F(137, 2, 5, 16), F(161, 9, 9, 16), F(160, 9, 3, 8) } };
var panelPlan = new[] {
    new[] { F(141, 3, 15, 16), F(158, 6, 3, 4), F(157, 6, 7, 8) },
    new[] { F(137, 2, 5, 16), F(161, 9, 1, 2), F(160, 9, 1, 2) } };   // G2 span 14 = 123'-6 5/8 + 13'-7 11/16
for (int g = 0; g < 2; g++)
    for (int j = 0; j < 3; j++)
    {
        double m = Brg(g, j + 1) - Brg(g, j);
        Len($"G{g + 1} span {14 + j} CL brg to CL brg (elevation sheet)", spanPlan[g][j], m);
        if (Math.Abs(panelPlan[g][j] - spanPlan[g][j]) > 1e-6) Len($"G{g + 1} span {14 + j} bracing panel string (framing plan p.273)", panelPlan[g][j], m, "plan strings disagree with each other");
    }
Print("== field splices (CL brg to CL splice along the girder) and derived plan dimensions");
Len("G1 FS2 to FS3 (65'-2 3/4 BOTH)", F(65, 2, 3, 4), Fs(0, 3) - Fs(0, 2));
Len("G1 FS3 to CL brg pier 15 (p.273 51'-4 1/8)", F(51, 4, 1, 8), Brg(0, 2) - Fs(0, 3));
Len("G1 FS3 to FS4 (85'-7 3/16)", F(85, 7, 3, 16), Fs(0, 4) - Fs(0, 3));
Len("G1 FS4 to CL brg pier 16 (123'-3 13/16)", F(123, 3, 13, 16), Brg(0, 3) - Fs(0, 4));
Len("G1 FS1 to FS2 (77'-6)", F(77, 6), Fs(0, 2) - Fs(0, 1));
Len("G2 FS2 to FS3 (66'-6 5/8 BOTH)", F(66, 6, 5, 8), Fs(1, 3) - Fs(1, 2));
Len("G2 FS3 to FS4 (87'-4 1/8)", F(87, 4, 1, 8), Fs(1, 4) - Fs(1, 3));
Len("G2 FS4 to CL brg pier 16 (125'-10 1/16)", F(125, 10, 1, 16), Brg(1, 3) - Fs(1, 4), "CSV labels it 'from pier 15'; numerically it is FS4 to pier 16");
Len("G2 FS1 to CL brg pier 14 (20'-9 15/16 + 15'-4 7/8)", F(20, 9, 15, 16) + F(15, 4, 7, 8), Brg(1, 1) - Fs(1, 1));

// plate thickness changes: model boundaries vs the plan's dimension strings that are NOT build inputs
double Change(int g, GirderPlate p, double near)
{
    var runs = tr.PlateRuns.Where(r => r.Girder == g && r.Plate == p).OrderBy(r => r.S0).ToList();
    var bounds = new List<double>();
    for (int i = 1; i < runs.Count; i++) if (Math.Abs(runs[i].Thickness - runs[i - 1].Thickness) > 1e-6) bounds.Add(runs[i].S0);
    return bounds.Count == 0 ? double.NaN : bounds.OrderBy(b => Math.Abs(b - near)).First();
}
double T(int g, GirderPlate p, double s) => tr.PlateRuns.Where(r => r.Girder == g && r.Plate == p && s >= r.S0 && s <= r.S1).Select(r => r.Thickness).FirstOrDefault(double.NaN);
Print("== plate strings (thickness changes)");
Len("G1 FS1 to bottom PL 1 1/4 -> 1 7/8 (20'-4 1/2)", F(20, 4, 1, 2), Change(0, GirderPlate.BottomFlange, Brg(0, 1) - F(15)) - Fs(0, 1));
Len("G1 bottom PL 1 3/8 to FS2 (27'-11 15/16)", F(27, 11, 15, 16), Fs(0, 2) - Change(0, GirderPlate.BottomFlange, Brg(0, 1) + F(14)));
Len("G1 FS3 to bottom PL 2 1/4 (37'-3 13/16)", F(37, 3, 13, 16), Change(0, GirderPlate.BottomFlange, Brg(0, 2) - F(14)) - Fs(0, 3));
Len("G1 bottom PL 1 3/8 (19'-6 1/8) to FS4", F(19, 6, 1, 8), Fs(0, 4) - Change(0, GirderPlate.BottomFlange, Brg(0, 2) + F(15)));
Len("G2 FS1 to bottom PL 1 1/4 -> 1 7/8 (20'-9 15/16)", F(20, 9, 15, 16), Change(1, GirderPlate.BottomFlange, Brg(1, 1) - F(15)) - Fs(1, 1));
Len("G2 bottom PL 1 3/8 to FS2 (28'-6 9/16)", F(28, 6, 9, 16), Fs(1, 2) - Change(1, GirderPlate.BottomFlange, Brg(1, 1) + F(14)));
Len("G2 FS3 to bottom PL 2 1/4 (38'-1 3/16)", F(38, 1, 3, 16), Change(1, GirderPlate.BottomFlange, Brg(1, 2) - F(14)) - Fs(1, 3));
Len("G2 top PL 2 1/4 -> 1 3/8 (19'-9 7/8) to FS4", F(19, 9, 7, 8), Fs(1, 4) - Change(1, GirderPlate.TopFlange, Brg(1, 2) + F(15)));
// thickness sequence per girder / plate between the CL bearings (CSV strings)
var seqPlan = new (int g, GirderPlate p, double[] t)[]
{
    (0, GirderPlate.TopFlange, new[] { 1.25, 1.875, 1.25, 1.375, 2.25, 1.375 }), (0, GirderPlate.BottomFlange, new[] { 1.25, 1.875, 1.375, 2.25, 1.375 }),
    (0, GirderPlate.Web, new[] { 0.625, 0.75, 0.625 }),
    (1, GirderPlate.TopFlange, new[] { 1.25, 1.875, 1.25, 1.375, 2.25, 1.375 }), (1, GirderPlate.BottomFlange, new[] { 1.25, 1.875, 1.375, 2.25, 1.375 }),
    (1, GirderPlate.Web, new[] { 0.625, 0.75, 0.625 }),
};
foreach (var (g, p, t) in seqPlan)
{
    var runs = tr.PlateRuns.Where(r => r.Girder == g && r.Plate == p).OrderBy(r => r.S0).ToList();
    var seq = new List<double>();
    foreach (var r in runs) { double ti = ToIn(r.Thickness); if (seq.Count == 0 || Math.Abs(seq[^1] - ti) > 1e-6) seq.Add(ti); }
    var widths = runs.Select(r => Math.Round(ToIn(r.Width), 4)).Distinct().ToList();
    Row($"G{g + 1} {p} thicknesses along the girder", string.Join(" / ", t), string.Join(" / ", seq.Select(x => x.ToString("0.###"))), seq.SequenceEqual(t, new Tol()),
        p == GirderPlate.Web ? "web plate on the slope 4'-3 9/16" : $"width {string.Join(",", widths)}\" (plan {(p == GirderPlate.TopFlange ? 21 : 77)}\")");
}
// web plates over the field sections
var webParts = st.Parts.Where(x => x.Category == PartCategory.WebPlate && x.Description.StartsWith("Web")).ToList();
Info($"{webParts.Count} web plate pieces; web length on the slope model {FI(tr.WebSlopeLength)} (plan 4'-3 9/16), web depth {FI(tr.WebDepth)} (plan 4'-2).");
Len("web length on the slope (p.273)", F(4, 3, 9, 16), tr.WebSlopeLength);

// tension zones (fracture critical, p.278 / p.279), lengths the build does not enter
Print("== tension flange zones");
double ZoneLen(int g, TubFlangeZone z, double near) => tr.TensionZones.Where(r => r.Girder == g && r.Flanges == z).OrderBy(r => Math.Abs((r.S0 + r.S1) / 2 - near)).Select(r => r.S1 - r.S0).FirstOrDefault(double.NaN);
Len("G1 TOP zone over pier 14 (38'-5 7/16)", F(38, 5, 7, 16), ZoneLen(0, TubFlangeZone.Top, Brg(0, 1)));
Len("G1 TOP zone over pier 15 (47'-11 1/4)", F(47, 11, 1, 4), ZoneLen(0, TubFlangeZone.Top, Brg(0, 2)));
Len("G1 BOTTOM zone span 16 (103'-5 5/16)", F(103, 5, 5, 16), ZoneLen(0, TubFlangeZone.Bottom, Brg(0, 3)));
Len("G2 TOP zone over pier 14 (39'-2 7/8)", F(39, 2, 7, 8), ZoneLen(1, TubFlangeZone.Top, Brg(1, 1)), "UNRESOLVED in the build (p.279): plan string does not close");
Len("G2 TOP zone over pier 15 (48'-10 7/8)", F(48, 10, 7, 8), ZoneLen(1, TubFlangeZone.Top, Brg(1, 2)));
Len("G2 BOTTOM zone span 16 (105'-6 13/16)", F(105, 6, 13, 16), ZoneLen(1, TubFlangeZone.Bottom, Brg(1, 3)));

// ---------------------------------------------------------------- 3. splice bolts (p.281 O-27)
Print("== field splices (p.281)");
// per splice and girder: top 2 flanges x 4 lines x 6 rows x 2 sides = 96; webs 2 x 3 x 12 x 2 = 144; bottom 18 x 5 x 2 = 180 -> 420
int perSplice = 2 * 4 * 6 * 2 + 2 * 3 * 12 * 2 + 18 * 5 * 2;
Row("field splice bolts (8 splices x 420)", $"{8 * perSplice}", $"{tr.SpliceBolts}", tr.SpliceBolts == 8 * perSplice);
foreach (var a in st.Assemblies.Where(a => a.Kind == AssemblyKind.FieldSplice).OrderBy(a => a.Mark))
{
    int nb = st.Bolts.Count(b => b.Assembly == a.Mark);
    var plates = st.Parts.Where(p => p.Assembly == a.Mark && p.Category is PartCategory.SplicePlate or PartCategory.FillerPlate).ToList();
    var bottom = plates.Where(p => p.Description.Contains("63")).Select(p => p.Description).Distinct();
    Row($"{a.Mark} bolts", $"{perSplice}", $"{nb}", nb == perSplice, $"{plates.Count} plates; {string.Join("; ", bottom)}");
}

// ---------------------------------------------------------------- 4. bracing (p.273 framing plan, p.282)
Print("== bracing counts (p.273 panel strings, p.282)");
int[][] spaces = { new[] { 14, 17, 16 }, new[] { 14, 17, 16 } };
for (int g = 0; g < 2; g++)
    for (int j = 0; j < 3; j++)
    {
        int n = tr.PanelPoints.Count(p => p.Girder == g && p.Span == j);
        Row($"G{g + 1} span {14 + j} interior panel points", $"{spaces[g][j] - 1} ({spaces[g][j]} spaces)", $"{n}", n == spaces[g][j] - 1);
    }
int panelsPlan = spaces.Sum(x => x.Sum());
int ppPlan = panelsPlan - 6;
Row("top struts (every interior panel point)", $"{ppPlan}", $"{tr.TopStruts}", tr.TopStruts == ppPlan);
int cfPlan = spaces.Sum(x => x.Sum(s => (s - 1) / 2));
Row("internal K cross frames (even panel points, UNRESOLVED p.273)", $"{cfPlan}", $"{tr.InternalCrossFrames}", tr.InternalCrossFrames == cfPlan);
Row("top lateral members (one per panel)", $"{panelsPlan}", $"{tr.LateralMembers}", tr.LateralMembers == panelsPlan);
Row("external intermediate diaphragms (p.273 bold radial lines)", "6", $"{tr.ExternalDiaphragms}", tr.ExternalDiaphragms == 6);
Row("support diaphragm lines", "4", $"{tr.SupportDiaphragms}", tr.SupportDiaphragms == 4);
Row("access hatches (p.273)", "4", $"{tr.Hatches}", tr.Hatches == 4);
Info($"transverse stiffeners {tr.Stiffeners} (no plan count), studs {tr.Studs}");
// stud counts from the plan spacing rows (3 studs per row per flange, 2 flanges): rows = spaces + 1 per zone
int[] studSpaces = { 124 + 40 + 48 + 62 + 59 + 39 + 162, 148 + 45 + 54 + 76 + 67 + 44 + 186 };
int studsPlan = studSpaces.Sum(s => s + 7) * 3 * 2;
int girderStuds = st.Studs.Count(x => !x.Assembly.StartsWith("XSD"));
Row("girder shear studs (3 per row per top flange, p.278 / p.279 spacing rows)", $"{studsPlan}", $"{girderStuds}", girderStuds == studsPlan, "7 zones per girder: rows = spaces + 1");
Info($"support diaphragm top flange studs (p.283 / p.285 / p.287: 2 - 7/8\" x 7\" @ 1'-0 max, 6\" from the ends): {tr.Studs - girderStuds}");
// external diaphragm: frames radial?
foreach (var pp in tr.PanelPoints.GroupBy(p => (p.Span, p.Point)).Where(x => x.Count() == 2))
{
    double d = Math.Abs(pp.First().Station - pp.Last().Station);
    if (br.Girders.Tub!.External.Points.Any(e => e.Span == pp.Key.Span && e.Point == pp.Key.Point) && d > 0.01)
        Info($"external diaphragm at span {14 + pp.Key.Span} point {pp.Key.Point}: G1 / G2 panel points {ToFt(d):F3} ft apart in station (plan draws radial lines)");
}

// ---------------------------------------------------------------- 5. dead load deflections (p.280 O-26)
Print("== dead load deflections (p.280, inches; camber input = total deflection)");
var rows = File.ReadAllLines(Path.Combine(dataDir, "dead-load-deflections.csv")).Skip(1).Where(l => l.Trim() != "").Select(l => l.Split(',')).ToList();
var cam = fr.Camber;
for (int g = 0; g < 2; g++)
{
    var gc = cam?.Girder(g);
    if (gc == null) { Row($"G{g + 1} camber table", "present", "missing", false); continue; }
    double maxD = 0; int n = 0, unl = gc.UnlocatedPoints.Count;
    foreach (var r in rows.Where(r => r[0] == (g + 1).ToString() && !r[1].StartsWith("FS")))
    {
        int no = r[1].StartsWith("PIER") ? 1 + 10 * (int.Parse(r[1][4..]) - 13) : 1 + 10 * int.Parse(r[1].Split('.')[0]) + int.Parse(r[1].Split('.')[1]);
        var p = gc.Point(no);
        if (p == null) { unl++; continue; }
        n++;
        maxD = Math.Max(maxD, Math.Abs(ToIn(p.TotalDeflection) - Dbl(r[4])));
        maxD = Math.Max(maxD, Math.Abs(ToIn(p.SteelDeflection) - Dbl(r[3])));
    }
    Row($"G{g + 1} deflection points located (27 tenth points + 4 supports)", "31", $"{n} (unlocated {unl})", n == 31 && unl == 0 && maxD <= 0.0101, $"max |model - plan| {maxD:F3}\" (plan total is rounded: concrete + steel differs from it by up to 0.01\")");
    // field splice rows: plan value vs the model deflection interpolated (linear) between the neighbouring tenth points at the model's splice location
    foreach (var r in rows.Where(r => r[0] == (g + 1).ToString() && r[1].StartsWith("FS")))
    {
        int no = int.Parse(r[1][2..]);
        double s = Fs(g, no);
        var pts = gc.Points.OrderBy(p => p.PathDistance).ToList();
        int i = pts.FindIndex(p => p.PathDistance > s);
        if (i <= 0) { Info($"G{g + 1} FS{no}: not between located points"); continue; }
        var a = pts[i - 1]; var b = pts[i];
        double u = (s - a.PathDistance) / (b.PathDistance - a.PathDistance);
        double lin = ToIn(a.TotalDeflection + u * (b.TotalDeflection - a.TotalDeflection));
        Info($"G{g + 1} FS{no}: plan total {Dbl(r[4]):F2}\", linear between points {a.Number} / {b.Number} at the model splice {lin:F2}\" (difference {lin - Dbl(r[4]):+0.00;-0.00}\"; chord, not the deflected curve)");
    }
}

// ---------------------------------------------------------------- 6. bearings (p.271 H, p.293 bevels) and seats vs pedestal elevations
Print("== bearings: H top of slab to bottom of bearing assembly at CL brg (p.271), plate bevels M / N (p.293)");
foreach (var b in tr.Bearings)
{
    Row($"{b.Support} G{b.Girder + 1} {b.Mark} H", FI(b.PlanH), $"{FI(b.H)} ({ToIn(b.H - b.PlanH):+0.000;-0.000}\")", Math.Abs(b.H - b.PlanH) <= Sixteenth + 1e-6,
        $"top of slab {ToFt(b.TopOfSlab):F3}, seat {ToFt(b.SeatElevation):F3}");
    Row($"{b.Support} G{b.Girder + 1} bevel transverse M", $"{b.PlanTransverse * 100:F2} %", $"{b.TransverseSlope * 100:F2} %", Math.Abs(b.TransverseSlope - b.PlanTransverse) <= 0.00005 + 1e-9);
    Row($"{b.Support} G{b.Girder + 1} bevel longitudinal N", $"{b.PlanLongitudinal * 100:F2} %", $"{b.LongitudinalSlope * 100:F2} %", Math.Abs(b.LongitudinalSlope - b.PlanLongitudinal) <= 0.00005 + 1e-9);
    if (!double.IsNaN(b.PlanHorizontalOffset)) Len($"{b.Support} G{b.Girder + 1} bearing CL horizontal offset from the top working point (p.292)", b.PlanHorizontalOffset, b.HorizontalOffset);
}
// Pedestal tops = bottom of the bearing assembly (pad on the pedestal). Pier 14 p.262, pier 15 p.264, pier 16 p.266 pedestals 1 / 2 (span 16 row,
// CL brg side; 3 / 4 carry span 17), pier 13 Bridge N p.180 pedestals 5 (G1) / 4 (G2) (span-14 row; 3 carries Bridge N).
var ped = new Dictionary<(string, int), (double el, string src)>
{
    [("Pier 13", 0)] = (1128.81, "p.180 ped 5"), [("Pier 13", 1)] = (1129.65, "p.180 ped 4"),
    [("Pier 14", 0)] = (1125.44, "p.262"), [("Pier 14", 1)] = (1126.50, "p.262"),
    [("Pier 15", 0)] = (1124.16, "p.264"), [("Pier 15", 1)] = (1125.22, "p.264"),
    [("Pier 16", 0)] = (1123.05, "p.266 ped 1"), [("Pier 16", 1)] = (1124.12, "p.266 ped 2"),
};
Print("== bearing seats vs pedestal elevations (substructure sheets; substructure itself not modeled)");
foreach (var b in tr.Bearings)
    if (ped.TryGetValue((b.Support, b.Girder), out var pe))
        Row($"{b.Support} G{b.Girder + 1} seat (bottom of pad)", $"{pe.el:F2} ({pe.src})", $"{ToFt(b.SeatElevation):F3}", Math.Abs(ToFt(b.SeatElevation) - pe.el) <= 0.0051 + 1e-9, "+-0.005 ft");

// ---------------------------------------------------------------- 7. weight
Print("== steel weight");
double lb = st.SteelMass / 0.45359237;
Info($"structural steel (excl. pads, welds, studs) {lb:N0} lb = {lb / 2000:N1} tons; no plan quantity for the tub unit in the volume (UNRESOLVED p.254-297).");
foreach (var grp in st.Parts.GroupBy(p => p.Category).OrderByDescending(g => g.Sum(p => p.Mass)))
    Info($"  {grp.Key}: {grp.Count()} parts, {grp.Sum(p => p.Mass) / 0.45359237:N0} lb");

Print($"== {ok} OK, {bad} DIFF");

class Tol : IEqualityComparer<double>
{
    public bool Equals(double a, double b) => Math.Abs(a - b) < 1e-6;
    public int GetHashCode(double a) => 0;
}
