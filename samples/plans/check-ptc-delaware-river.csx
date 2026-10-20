// Check of the PTC / NJTA Delaware River Bridge location model (phase 1a) against the plan values. Run after build-ptc-delaware-river.csx:
//   sbm run "samples\plans\check-ptc-delaware-river.csx" "samples\plans\PTC Delaware River Bridge.sbm"
// PASS = within the tolerance (printed stations 0.001 ft, inch-rounded span labels 1/16", elevations 0.002 ft); DIFF = reported, never fixed.
// Elevations are the printed GUTTER LINE values (NGVD29): the model profile is on the CL, so the gutter = ElevationAt(sta, +- 38'-3").
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var inv = CultureInfo.InvariantCulture;
int pass = 0, diff = 0;
var diffs = new List<string>();
void Row(string item, double model, double plan, double tol, string unit, string note = "")
{
    bool ok = Math.Abs(model - plan) <= tol + 1e-9;
    string s = $"{(ok ? "PASS" : "DIFF")}  {item}: model {model:0.####} vs plan {plan:0.####} {unit} (diff {model - plan:+0.####;-0.####}){(note != "" ? "  - " + note : "")}";
    if (ok) pass++; else { diff++; diffs.Add(s); }
    Print(s);
}
double ft = Ft(1);
double FtIn(double f, double i, double n = 0, double d = 1) => f + (i + n / d) / 12.0;
string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo.");
var rows = new List<string[]>();
foreach (var line in File.ReadAllLines(Path.Combine(dataDir, "supports.csv")).Skip(1))
{
    if (line.Trim() == "") continue;
    var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
    foreach (char c in line)
    {
        if (c == '"') q = !q;
        else if (c == ',' && !q) { cells.Add(cur.ToString()); cur.Clear(); }
        else cur.Append(c);
    }
    cells.Add(cur.ToString());
    rows.Add(cells.ToArray());
}

var names = new[] { "PTC Girder spans 1-10", "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC Arch truss 18-20", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27", "PTC Girder spans 28-31" };
var fr = names.ToDictionary(n => n, n => Doc.Framing(n));
var al = fr[names[0]].Alignment;
double St(string unit, string support) => fr[unit].Supports.First(s => s.Name == support).Station / ft;
double Pl(string sta) => Sta(sta) / ft;

// ---------------------------------------------------------------- 1. supports vs supports.csv
Print("== 1. Supports vs supports.csv");
foreach (var n in names)
{
    var csv = rows.Where(r => r[6] == n).OrderBy(r => Sta(r[1])).ToList();
    Row($"{n}: support count", fr[n].Supports.Count, csv.Count, 0, "");
    for (int i = 0; i < csv.Count; i++)
        Row($"{n}: {csv[i][0]} station", fr[n].Supports[i].Station / ft, Pl(csv[i][1]), 0.0005, "ft");
}
if (Doc.Project.Georeference == null)
{
    Row("W abutment at the local origin (east)", al.PointAt(Sta("646+21.463")).X / ft, 0, 0.0005, "ft");
    Row("W abutment at the local origin (north)", al.PointAt(Sta("646+21.463")).Y / ft, 0, 0.0005, "ft");
}
else   // fitted EPSG:2272 position (GEOREFERENCE.md, approximate +/-10 m); model metres -> ftUS
{
    Row("W abutment E (EPSG:2272 ftUS, fitted)", al.PointAt(Sta("646+21.463")).X * 3937 / 1200.0, 2781357.274, 0.001, "ftUS");
    Row("W abutment N (EPSG:2272 ftUS, fitted)", al.PointAt(Sta("646+21.463")).Y * 3937 / 1200.0, 300154.409, 0.001, "ftUS");
}

// ---------------------------------------------------------------- 2. span lengths (p.1 / p.42 / p.43 as-built)
Print("== 2. Span lengths (pier CL to pier CL, or bearing to bearing at a unit end)");
var g = names[0]; var g2 = names[6];
Row("span 1 W abut - Pier 1 (123'-2\")", St(g, "Pier 1") - St(g, "W Abutment"), FtIn(123, 2), 1.0 / 16 / 12 * 1.5, "ft", "p.1, p.42");
for (int k = 1; k < 9; k++)
    Row($"span {k + 1} Pier {k} - Pier {k + 1} (124'-5\")", St(g, $"Pier {k + 1}") - St(g, $"Pier {k}"), FtIn(124, 5), 0.0105, "ft", "p.1, p.42; 995'-4\" for 8 spans");
Row("spans 2-9 total (8 @ 124'-5\" = 995'-4\")", St(g, "Pier 9") - St(g, "Pier 1"), FtIn(995, 4), 0.0105, "ft");
Row("span 10 Pier 9 - girder shoe, as built (121'-8 1/4\")", St(g, "Pier 10 girder brg") - St(g, "Pier 9"), FtIn(121, 8, 1, 4), 0.0105, "ft", "p.42 / p.5; p.1 (3-9-54) printed 121'-5\" + 5'-0 to the truss shoe");
Row("girder shoe - truss shoe pier 10 (4'-8 3/4\")", St(names[1], "Pier 10 truss brg") - St(g, "Pier 10 girder brg"), FtIn(4, 8, 3, 4), 0.0105, "ft", "p.9, p.42");
var t3 = names[1];
Row("span 11 Pier 10 truss brg - Pier 11 (215'-6\")", St(t3, "Pier 11") - St(t3, "Pier 10 truss brg"), FtIn(215, 6), 0.0105, "ft");
Row("span 12 (215'-6\")", St(t3, "Pier 12") - St(t3, "Pier 11"), FtIn(215, 6), 0.0105, "ft");
Row("span 13 Pier 12 - Pier 13 W brg (215'-6\")", St(t3, "Pier 13 W brg") - St(t3, "Pier 12"), FtIn(215, 6), 0.0105, "ft");
Row("3-span truss 11-13 (646'-6\")", St(t3, "Pier 13 W brg") - St(t3, "Pier 10 truss brg"), FtIn(646, 6), 0.0105, "ft");
Row("Pier 13 c.c. bearings (4'-0\")", St(names[2], "Pier 13 E brg") - St(t3, "Pier 13 W brg"), 4.0, 0.0005, "ft", "p.1, p.7");
var t4 = names[2];
Row("4-span truss 14-17 (4 @ 269'-4 1/2\" = 1077'-6\")", St(t4, "Pier 17 W brg") - St(t4, "Pier 13 E brg"), FtIn(1077, 6), 0.0105, "ft");
foreach (var (a, b, nm) in new[] { ("Pier 13 E brg", "Pier 14", "14"), ("Pier 14", "Pier 15", "15"), ("Pier 15", "Pier 16", "16"), ("Pier 16", "Pier 17 W brg", "17") })
    Row($"span {nm} (269'-4 1/2\")", St(t4, b) - St(t4, a), FtIn(269, 4, 1, 2), 0.0105, "ft");
var ar = names[3];
Row("Pier 17 c.c. bearings (4'-0\")", St(ar, "Pier 17 main brg") - St(t4, "Pier 17 W brg"), 4.0, 0.0005, "ft", "p.1, p.16");
Row("span 18 (11 panels @ 31'-0 = 341'-0)", St(ar, "Pier 18") - St(ar, "Pier 17 main brg"), 341.0, 0.0005, "ft", "p.1, p.69");
Row("span 19 (22 panels @ 31'-0 = 682'-0)", St(ar, "Pier 19") - St(ar, "Pier 18"), 682.0, 0.0005, "ft", "p.1, p.69");
Row("span 20 (11 panels @ 31'-0 = 341'-0)", St(ar, "Pier 20 main brg") - St(ar, "Pier 19"), 341.0, 0.0005, "ft", "p.1, p.69");
Row("centre of bridge = mid-span 19 = 682+80.38", (St(ar, "Pier 18") + St(ar, "Pier 19")) / 2, Pl("682+80.38"), 0.0005, "ft", "p.1, p.69");
Row("pier CL to pier CL 17 - 18 (343'), p.16", Pl("679+39.38") - Pl("675+96.38"), 343.0, 0.0005, "ft", "p.16 dimension 343'-0");
Row("pier CL to pier CL 19 - 20 (343'), p.16", Pl("689+64.38") - Pl("686+21.38"), 343.0, 0.0005, "ft", "p.16 dimension 343'-0");
var t4b = names[4];
Row("Pier 20 c.c. bearings (4'-0\")", St(t4b, "Pier 20 E brg") - St(ar, "Pier 20 main brg"), 4.0, 0.0005, "ft", "p.1");
Row("4-span truss 21-24 (1077'-6\")", St(t4b, "Pier 24 W brg") - St(t4b, "Pier 20 E brg"), FtIn(1077, 6), 0.0105, "ft");
foreach (var (a, b, nm) in new[] { ("Pier 20 E brg", "Pier 21", "21"), ("Pier 21", "Pier 22", "22"), ("Pier 22", "Pier 23", "23"), ("Pier 23", "Pier 24 W brg", "24") })
    Row($"span {nm} (269'-4 1/2\")", St(t4b, b) - St(t4b, a), FtIn(269, 4, 1, 2), 0.0105, "ft");
var t3b = names[5];
Row("Pier 24 c.c. bearings (4'-0\")", St(t3b, "Pier 24 E brg") - St(t4b, "Pier 24 W brg"), 4.0, 0.0005, "ft", "p.1");
Row("3-span truss 25-27 (646'-6\")", St(t3b, "Pier 27 truss brg") - St(t3b, "Pier 24 E brg"), FtIn(646, 6), 0.0105, "ft");
foreach (var (a, b, nm) in new[] { ("Pier 24 E brg", "Pier 25", "25"), ("Pier 25", "Pier 26", "26"), ("Pier 26", "Pier 27 truss brg", "27") })
    Row($"span {nm} (215'-6\")", St(t3b, b) - St(t3b, a), FtIn(215, 6), 0.0105, "ft");
Row("girder shoe - truss shoe pier 27 (4'-8 3/4\")", St(g2, "Pier 27 girder brg") - St(t3b, "Pier 27 truss brg"), FtIn(4, 8, 3, 4), 0.0105, "ft", "p.36, p.42");
Row("span 28 girder shoe - Pier 28, as built (121'-8 1/4\")", St(g2, "Pier 28") - St(g2, "Pier 27 girder brg"), FtIn(121, 8, 1, 4), 0.0105, "ft", "p.42 / p.32");
Row("spans 29-30 (2 @ 124'-5\" = 248'-10\")", St(g2, "Pier 30") - St(g2, "Pier 28"), FtIn(248, 10), 0.0105, "ft", "p.42 / p.32");
Row("span 31 Pier 30 - E abutment, as built (123'-2\")", St(g2, "E Abutment") - St(g2, "Pier 30"), FtIn(123, 2), 0.0105, "ft", "p.42 / p.32; p.1 (3-9-54) printed 3 spans @ 124'-5\" = 373'-3\" to 711+94.046");
Row("overall W abut - E abut as built (6571'-4\")", St(g2, "E Abutment") - St(g, "W Abutment"), FtIn(6571, 4), 0.0105, "ft", "p.42");
Print($"INFO  p.1 (3-9-54, original) printed 6572'-7\" = E abut 711+94.046; the as-built substructure and superstructure sheets (p.32, 36, 38, 42, 43) print 711+92.797 = 6571'-4\": E abut difference {(Pl("711+94.046") - Pl("711+92.797")):0.000} ft (RESOLVED: the later sheets govern, see supports.csv)");
Row("E abutment end of structure = CL shoe + 34'-9\" = 712+27.547 (p.38, p.41)", St(g2, "E Abutment") + FtIn(34, 9), Pl("712+27.547"), 0.0005, "ft");
Row("PN 4 length (p.41): 712+27.547 - 645+53.713", Pl("712+27.547") - Pl("645+53.713"), 6673.834, 0.0005, "ft");

// ---------------------------------------------------------------- 3. alignment
Print("== 3. Alignment");
double az = (Math.PI / 2 - al.AngleAt(Sta("680+00"))) * 180 / Math.PI; if (az < 0) az += 360;
if (Doc.Project.Georeference == null)
    Row("azimuth from local north (S 69 54'15\" E = 110 05'45\")", az, 110 + 5 / 60.0 + 45 / 3600.0, 1e-6, "deg");
else   // EPSG:2272 grid azimuth of the fitted axis (GEOREFERENCE.md); the plan bearing 110 05'45" is TRUE north (fitted true 110.055 deg, +/-0.15)
    Row("grid azimuth EPSG:2272 (fitted, GEOREFERENCE.md; plan S 69 54'15\" E = true bearing)", az, 108.162, 1e-6, "deg");
var pW = al.PointAt(Sta("646+21.463")); var pE = al.PointAt(Sta("711+92.797"));
Row("plan distance W - E abutment = station difference", Vec2.Distance(pW, pE) / ft, Pl("711+92.797") - Pl("646+21.463"), 0.0005, "ft");
Row("alignment elements (one tangent)", al.Elements.Count, 1, 0, "");
Print("INFO  georeference: none (UNRESOLVED: fit to aerial / LiDAR pending the user's download OK); datum NGVD29 as printed (NAVD88 shift later)");

// ---------------------------------------------------------------- 4. profile (gutter line = CL - 38.25 / 96)
Print("== 4. Profile (gutter line; the model profile is on the CL)");
double crown = 38.25 / 96.0;
double Gutter(string sta, double off = 38.25) => al.ElevationAt(Sta(sta), off * ft) / ft;
double GutterAt(double staFt) => al.ElevationAt(staFt * ft, 38.25 * ft) / ft;
Row("CL above gutter (p.107 38'-3 x 1/8\"/ft; p.43 prints 0.400)", al.ElevationAt(Sta("680+00")) / ft - Gutter("680+00"), 0.4, 0.002, "ft");
Row("right gutter = left gutter (symmetric crown)", Gutter("680+00"), Gutter("680+00", -38.25), 1e-9, "ft");
var vp = al.Profile;
Row("crest PVC 675+98.38 El 143.390", Gutter("675+98.38"), 143.390, 0.002, "ft", "p.1, p.69");
Row("crest PVT 689+62.38 El 143.390", Gutter("689+62.38"), 143.390, 0.002, "ft", "p.1");
Row("crest PVI 682+80.38 El 163.850 (VPI)", vp.Vpis[2].Elevation / ft - crown, 163.850, 0.002, "ft", "p.1, p.42, p.69");
Row("crest curve length L = 1364.000", vp.Vpis[2].CurveLength / ft, 1364.0, 0.0005, "ft");
Row("crest M = 10.230 (VPI - curve at the VPI)", vp.Vpis[2].Elevation / ft - crown - Gutter("682+80.38"), 10.230, 0.002, "ft", "p.1; 1364 x 0.06 / 8");
Row("PVC station = VPI - L/2 = 675+98.38", vp.Vpis[2].Station / ft - vp.Vpis[2].LengthIn / ft, Pl("675+98.38"), 0.0005, "ft", "LOCATION.md's earlier 675+49.38 was a misreading");
Row("PVT station = VPI + L/2 = 689+62.38", vp.Vpis[2].Station / ft + vp.Vpis[2].LengthOut / ft, Pl("689+62.38"), 0.0005, "ft");
Row("west VPI 644+67.14 El 49.454", vp.Vpis[1].Elevation / ft - crown, 49.454, 0.002, "ft", "p.5, p.42, p.43");
Row("west curve L = 1060", vp.Vpis[1].CurveLength / ft, 1060.0, 0.0005, "ft");
Row("west curve M.O. = 4.637", Gutter("644+67.14") - (vp.Vpis[1].Elevation / ft - crown), 4.637, 0.002, "ft", "sag: the curve lies above the VPI");
Row("west PVT 649+97.14 El 65.354", Gutter("649+97.14"), 65.354, 0.002, "ft", "p.5, p.43");
Row("grade between the curves (+3.00 %)", (Gutter("670+00") - Gutter("660+00")) / 1000.0, 0.03, 1e-6, "ft/ft", "p.1, p.5, p.42");
Row("grade east of the crest curve (-3.00 %)", (Gutter("700+00") - Gutter("695+00")) / 500.0, -0.03, 1e-6, "ft/ft", "p.1, p.32");

Print("== 4b. Gutter elevations at the bearing lines (p.43 elevation of girders G1)");
foreach (var (sta, el, nm) in new[] {
    ("646+21.463", 56.414, "W abutment brg"), ("647+44.630", 58.831, "Pier 1"), ("648+69.046", 61.782, "Pier 2"), ("649+93.463", 65.244, "Pier 3"),
    ("651+17.880", 68.975, "Pier 4"), ("652+42.297", 72.707, "Pier 5"), ("653+66.713", 76.440, "Pier 6"), ("654+91.130", 80.172, "Pier 7"),
    ("656+15.547", 83.905, "Pier 8"), ("657+39.963", 87.637, "Pier 9"), ("658+61.651", 91.288, "Pier 10 girder brg"), ("658+66.380", 91.430, "Pier 10 truss brg"),
    ("706+94.380", 91.430, "Pier 27 truss brg"), ("706+99.109", 91.288, "Pier 27 girder brg"), ("708+20.797", 87.637, "Pier 28"), ("709+45.213", 83.905, "Pier 29"),
    ("710+69.630", 80.172, "Pier 30"), ("711+92.797", 76.478, "E abutment brg") })
    Row($"gutter El at {nm} {sta}", Gutter(sta), el, 0.002, "ft", "p.43");

Print("== 4c. Main span gutter grade line by panel point (p.69 ELEVATIONS table, panel 0 = 675+98.38, 31'-0 panels)");
var p69 = new[] { 143.3900, 144.2989, 145.1655, 145.9898, 146.7718, 147.5116, 148.2091, 148.8643, 149.4773, 150.0480, 150.5764, 151.0625, 151.5064, 151.9080, 152.2673, 152.5843, 152.8591, 153.0916, 153.2818, 153.4298, 153.5355, 153.5989, 153.6200 };
double worst = 0; int worstK = 0;
for (int k = 0; k < p69.Length; k++)
{
    double d = Math.Abs(GutterAt(Pl("675+98.38") + 31.0 * k) - p69[k]);
    if (d > worst) { worst = d; worstK = k; }
}
Row("max |model - plan| over panel points 0-22 (23 values)", worst, 0, 0.0005, "ft", $"worst at panel point {worstK}");

Print("== 5. Crown / cross slope (p.107)");
Row("slope each side of the CL 1/8\" per ft", -(al.ElevationAt(Sta("680+00"), 12 * ft) - al.ElevationAt(Sta("680+00"))) / ft / 12, 1.0 / 96, 1e-9, "ft/ft");
Print("INFO  the gutter line 38'-3 from the CL (p.107) is on the printed profile; the deck / barrier / walkway are NOT modeled (SupportLinesOnly)");

Print($"\nPTC Delaware River location check: {pass} PASS / {diff} DIFF");
foreach (var d in diffs) Print("  " + d);
