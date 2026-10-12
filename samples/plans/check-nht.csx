// Northaven Trail (TxDOT CSJ 0047-07-228) - model vs plan check. Run after build-nht.csx, from the repo root:
//   sbm run samples\plans\check-nht.csx "samples\plans\Northaven NHT.sbm"
// Compares: alignment PC/PT points, PGL, unit lengths (units-spans.csv), support stations, girder angles (PIER REPORTs p.108-116), girder
// lengths (GIRDER REPORTs: C-C pier / C-C brg horizontal, bottom-flange length, slope), bearing seats (bearing-seat-elevations.csv, p.91),
// approach pier / abutment / arch pier control elevations (approach-piers.csv p.97/99, p.95/96, p.102), drilled shaft tops / tips
// (p.95/96/98/100/101), arch pedestal seats (p.102) and quantities (quantities.csv p.91; p.102 arch pier concrete).
// Tolerances (user rule): 0.01 ft for plan values in feet, 1/16" for dimensions in inches, 30" of arc for angles. Plan values in 0.01 ft (slab /
// beam lengths) are compared at 0.01 ft too. Every row prints PASS / FAIL; differences are reported, nothing is changed.
using System.IO;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;

int pass = 0, fail = 0;
var fails = new List<string>();
double ft = Ft(1);
void Chk(string what, double model, double plan, double tol, string unit = "ft")
{
    bool ok = Math.Abs(model - plan) <= tol + 1e-9;
    if (ok) pass++; else { fail++; fails.Add(what); }
    Print($"{(ok ? "PASS" : "FAIL")}  {what}: model {model:0.000} plan {plan:0.000} ({model - plan:+0.000;-0.000} {unit})");
}
string DataDir()
{
    for (var dd = new DirectoryInfo(Environment.CurrentDirectory); dd != null; dd = dd.Parent)
    {
        var p = Path.Combine(dd.FullName, "docs", "plan-data", "nht-0047-07-228");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/nht-0047-07-228 not found above the current directory - run from the repo.");
}
List<string[]> Csv(string name) => File.ReadAllLines(Path.Combine(DataDir(), name)).Skip(1).Where(l => l.Trim() != "").Select(l => l.Split(',')).ToList();

var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
var al = Doc.Alignment("CL NHT (TRAILCLNEW)");
string[] unitNames = { "NHT Unit 1", "NHT Unit 2", "NHT Unit 3", "NHT Unit 4", "NHT Unit 5" };
int[] firstSpan = { 1, 4, 7, 11, 13 };

// ---------------------------------------------------------------- 1. alignment and profile (p.18-20, 84, 88)
Print("== alignment / profile");
(string n, string sta, double x, double y)[] pts =
{
    ("PT 102", "20+49.30", 2499806.50, 7016136.59), ("PC 103", "20+92.01", 2499774.09, 7016164.41), ("PT 103", "22+35.03", 2499810.26, 7016269.86),
    ("PC 104", "22+74.36", 2499849.55, 7016271.77), ("PT 104", "23+59.77", 2499933.91, 7016261.42), ("PC 105", "24+00.44", 2499972.85, 7016249.67),
    ("PRC 105/106", "25+18.73", 2500079.12, 7016198.78), ("PCC 106/107", "26+47.89", 2500195.83, 7016144.89), ("PT 107", "27+15.09", 2500262.26, 7016136.18),
    ("PC 108", "28+33.68", 2500380.85, 7016136.66), ("PT 108", "28+72.73", 2500419.67, 7016133.02), ("PC 109", "28+90.27", 2500436.89, 7016129.68),
    ("PT 109", "29+29.31", 2500475.70, 7016126.04),
};
foreach (var p in pts)
{
    var q = al.PointAt(Sta(p.sta), 0);
    Chk($"{p.n} {p.sta} X", q.X / ft, p.x, 0.01);
    Chk($"{p.n} {p.sta} Y", q.Y / ft, p.y, 0.01);
}
Chk("PGL begin bridge 20+49.30", al.Profile.ElevationAt(Sta("20+49.30")) / ft, 525.27, 0.01);
Chk("PGL end bridge 29+10.87", al.Profile.ElevationAt(Sta("29+10.87")) / ft, 520.32, 0.01);

// ---------------------------------------------------------------- 2. supports, deck / unit lengths (p.84-88, units-spans.csv)
Print("== supports / units");
foreach (var r in Csv("supports.csv"))
{
    string name = r[0].Replace(" (arch pier)", "");
    double plan = Sta(r[1].Split(' ')[0]);
    var sup = Doc.Project.Bridges.SelectMany(b => b.Supports).FirstOrDefault(s => s.Name == name);
    if (sup == null) { fail++; fails.Add(name); Print($"FAIL  {name}: not in the model"); continue; }
    Chk($"{name} station", sup.Station / ft, plan / ft, 0.005);
}
// station where a deck-end line (point, direction) crosses CL NHT
double Cross(Vec2 p, Vec2 dir)
{
    var n = dir.Left;
    double s = al.Inverse(p).Station;
    for (int it = 0; it < 30; it++)
    {
        double fv = Vec2.Dot(al.PointAt(s, 0) - p, n);
        s -= fv / Vec2.Dot(Vec2.FromAngle(al.AngleAt(s)), n);
        if (Math.Abs(fv) < 1e-10) break;
    }
    return s;
}
double[] unitLen = { 107.88, 97.00, 162.92, 122.45, 168.00 };
double area = 0;
for (int u = 0; u < 5; u++)
{
    var f = Doc.Framing(unitNames[u]);
    double s0 = Cross(f.Deck!.BeginLinePoint, f.Deck.BeginLineDirection), s1 = Cross(f.Deck.EndLinePoint, f.Deck.EndLineDirection);
    Chk($"{unitNames[u]} deck length along CL NHT (SEJ to SEJ)", (s1 - s0) / ft, unitLen[u], 0.01);
    area += f.Deck.Area;
}

// ---------------------------------------------------------------- 3. girder angles (PIER REPORTs) and girder lengths (GIRDER REPORTs)
Print("== girder layout (PIER / GIRDER REPORTs p.108, 110, 112, 114, 116)");
// (plan span, support index in the unit's span: 0 = begin / 1 = end, g1 D M S, g2 D M S)
var angles = new (int span, int end, double g1, double g2)[]
{
    (1, 0, 90, 90), (1, 1, D(85, 4, 8), D(85, 4, 8)), (2, 0, D(75, 40, 34), D(75, 40, 34)), (2, 1, D(75, 40, 34), D(75, 40, 34)),
    (3, 0, D(75, 40, 34), D(75, 40, 34)), (3, 1, D(75, 40, 34), D(75, 40, 34)), (4, 0, D(75, 40, 34), D(75, 40, 34)), (4, 1, D(75, 40, 34), D(75, 40, 34)),
    (5, 0, D(75, 40, 34), D(75, 40, 34)), (5, 1, D(75, 40, 34), D(75, 40, 34)), (6, 0, D(77, 15, 23), D(76, 53, 19)), (6, 1, D(85, 41, 56), D(86, 4, 0)),
    (7, 0, D(86, 59, 40), D(86, 26, 35)), (7, 1, D(83, 58, 51), D(84, 31, 54)), (8, 0, D(84, 53, 39), D(87, 8, 7)), (8, 1, D(89, 22, 34), D(87, 8, 7)),
    (9, 0, D(85, 38, 9), D(85, 29, 58)), (9, 1, D(73, 28, 39), D(73, 20, 28)), (11, 0, D(76, 9, 10), D(76, 31, 36)), (11, 1, D(82, 35, 12), D(82, 57, 38)),
    (12, 0, D(84, 3, 28), D(84, 10, 7)), (12, 1, D(89, 28, 31), D(89, 21, 52)), (13, 0, 90, 90), (13, 1, 90, 90), (14, 0, D(88, 50, 7), D(88, 52, 11)),
    (14, 1, D(85, 5, 44), D(85, 3, 40)), (15, 0, D(86, 46, 52), D(86, 47, 23)), (15, 1, D(85, 59, 41), D(86, 0, 12)),
};
double D(int d, int m, int s) => d + m / 60.0 + s / 3600.0;
// (plan span, g1: C-C pier, C-C brg, bottom flange, slope; g2: ...)
var greport = new (int span, double[] g1, double[] g2)[]
{
    (1, new[] { 48.307, 46.307, 47.85, 0.0437 }, new[] { 47.473, 45.473, 47.02, 0.0444 }),
    (2, new[] { 32.575, 30.575, 32.10, 0.0407 }, new[] { 27.662, 25.662, 27.19, 0.0479 }),
    (3, new[] { 32.575, 30.575, 32.10, 0.0407 }, new[] { 27.662, 25.662, 27.19, 0.0479 }),
    (4, new[] { 32.575, 30.575, 32.10, 0.0407 }, new[] { 27.662, 25.662, 27.19, 0.0479 }),
    (5, new[] { 32.575, 30.575, 32.10, 0.0407 }, new[] { 27.662, 25.662, 27.19, 0.0479 }),
    (6, new[] { 38.556, 36.556, 38.09, 0.0423 }, new[] { 35.621, 33.621, 35.16, 0.0459 }),
    (7, new[] { 60.398, 58.398, 59.94, 0.0367 }, new[] { 58.773, 56.773, 58.31, 0.0376 }),
    (8, new[] { 25.542, 23.542, 25.05, 0.0199 }, new[] { 24.507, 22.507, 24.01, 0.0203 }),
    (9, new[] { 78.046, 75.199, 76.73, -0.0018 }, new[] { 80.227, 77.378, 78.91, -0.0025 }),
    (11, new[] { 67.606, 64.782, 66.34, -0.0345 }, new[] { 66.392, 63.571, 65.13, -0.0344 }),
    (12, new[] { 55.517, 53.517, 55.09, -0.0496 }, new[] { 56.601, 54.601, 56.17, -0.0483 }),
    (13, new[] { 56.000, 54.000, 55.57, -0.0490 }, new[] { 56.000, 54.000, 55.57, -0.0490 }),
    (14, new[] { 56.483, 54.483, 56.05, -0.0486 }, new[] { 55.460, 53.460, 55.03, -0.0495 }),
    (15, new[] { 55.905, 53.902, 55.47, -0.0491 }, new[] { 56.037, 54.034, 55.60, -0.0490 }),
};
double txLf = 0;
for (int u = 0; u < 5; u++)
{
    var f = Doc.Framing(unitNames[u]);
    var ps = Doc.Prestressed(unitNames[u]);
    int nSpan = Doc.Bridge(unitNames[u]).Supports.Count - 1;
    for (int j = 0; j < nSpan; j++)
    {
        int planSpan = firstSpan[u] + j;
        var gr = greport.First(x => x.span == planSpan);
        for (int g = 0; g < 2; g++)
        {
            var b0 = f.SupportLine(j).BearingFor(g, true)!.TopOfSteel;
            var b1 = f.SupportLine(j + 1).BearingFor(g, false)!.TopOfSteel;
            var chord = new Vec2(b1.X - b0.X, b1.Y - b0.Y);
            var plan = g == 0 ? gr.g1 : gr.g2;
            Chk($"span {planSpan} G{g + 1} C-C pier (horizontal)", chord.Length / ft, plan[0], 0.01);
            var beam = ps.Beams.First(x => x.Span == j && x.Girder == g);
            double cbrg = new Vec2(beam.BearingEnd.X - beam.BearingStart.X, beam.BearingEnd.Y - beam.BearingStart.Y).Length;
            Chk($"span {planSpan} G{g + 1} C-C brg (horizontal)", cbrg / ft, plan[1], 0.01);
            double slope = (beam.BearingEnd.Z - beam.BearingStart.Z) / cbrg;
            double trueLen = beam.Length * Math.Sqrt(1 + slope * slope);      // PrestressedBeam.Length is the plan (horizontal) length
            Chk($"span {planSpan} G{g + 1} bottom flange length (along the slope)", trueLen / ft, plan[2], 0.01);
            Chk($"span {planSpan} G{g + 1} girder slope", slope, plan[3], 0.0005, "ft/ft");
            txLf += trueLen / ft;
            foreach (var (k, end) in new[] { (j, 0), (j + 1, 1) })
            {
                var a = angles.FirstOrDefault(x => x.span == planSpan && x.end == end);
                double planAng = g == 0 ? a.g1 : a.g2;
                var dir = f.SupportLine(k).Direction;
                double ang = Math.Acos(Math.Min(1, Math.Abs(Vec2.Dot(chord.Normalized(), dir)))) * 180 / Math.PI;
                Chk($"span {planSpan} G{g + 1} girder angle at {f.SupportLine(k).Name}", ang * 3600, planAng * 3600, 30, "sec");
            }
        }
    }
}

// ---------------------------------------------------------------- 4. substructure: seats, control elevations, shafts
Print("== bearing seats (p.91) and substructure");
// physical support -> its substructure unit (owner bridge)
var unitsByName = new Dictionary<string, SubstructureUnit>();
foreach (var un in unitNames)
    foreach (var su in Doc.Model(un).Substructure?.Units.AsEnumerable() ?? Enumerable.Empty<SubstructureUnit>())
        unitsByName.TryAdd(su.Name, su);
foreach (var r in Csv("bearing-seat-elevations.csv"))
{
    string name = r[0].Replace("Abut ", "Abutment ");
    if (name.Contains("(arch)")) continue;
    if (!unitsByName.TryGetValue(name, out var su)) { fail++; fails.Add(name + " seats"); Print($"FAIL  {name}: no substructure unit"); continue; }
    bool back = r[1] == "BACK";
    for (int g = 0; g < 2; g++)
    {
        // the row's seats left to right = G1, G2 (seats of the other bridge on a shared pier carry their own girder numbering)
        var row = su.Pedestals.Where(p => su.Type == SupportType.Abutment || (back ? p.RowN < su.CapMidN : p.RowN > su.CapMidN)).OrderBy(p => p.RowS).ToList();
        var ped = row.Count == 2 ? new List<PedestalInfo> { row[g] } : new List<PedestalInfo>();
        if (ped.Count != 1) { fail++; fails.Add($"{name} {r[1]} G{g + 1}"); Print($"FAIL  {name} {r[1]} G{g + 1}: {ped.Count} seats found"); continue; }
        Chk($"{name} {r[1]} G{g + 1} bearing seat", ped[0].SeatZ / ft, double.Parse(r[2 + g]), 0.01);
    }
}
// approach piers (approach-piers.csv: EL A/B cap top at the ends, C cap bottom at the column, D top of shaft) + shaft tips (p.98, p.100)
var tips = new Dictionary<string, double>
{
    ["Pier 2"] = 492.224, ["Pier 3"] = 492.546, ["Pier 4"] = 492.867, ["Pier 5"] = 493.189, ["Pier 6"] = 492.510, ["Pier 7"] = 496.134,
    ["Pier 8"] = 496.368, ["Pier 9"] = 495.883, ["Pier 12"] = 481.129, ["Pier 13"] = 481.392, ["Pier 14"] = 491.647, ["Pier 15"] = 491.900,
};
foreach (var r in Csv("approach-piers.csv"))
{
    string name = r[0];
    var su = unitsByName[name];
    Chk($"{name} EL. A cap top left end", su.CapTopAt(su.CapS0) / ft, double.Parse(r[8]), 0.01);
    Chk($"{name} EL. B cap top right end", su.CapTopAt(su.CapS1) / ft, double.Parse(r[9]), 0.01);
    Chk($"{name} EL. C cap bottom at column", su.Columns[0].TopZ / ft, double.Parse(r[10]), 0.01);
    Chk($"{name} EL. D top of shaft", su.Foundations[0].TopZ / ft, double.Parse(r[11]), 0.01);
    Chk($"{name} shaft tip", su.Foundations[0].BottomZ / ft, tips[name], 0.01);
    Chk($"{name} cap length (LF1+LF2+LF3)", (su.CapS1 - su.CapS0) / ft, 15.000, 0.005);
    var rng = r[13].Split(" to ");
    double hMin = su.Pedestals.Min(p => p.Height) / ft, hMax = su.Pedestals.Max(p => p.Height) / ft;
    Chk($"{name} seat - cap top, min", hMin, double.Parse(rng[0]), 0.01);
    Chk($"{name} seat - cap top, max", hMax, double.Parse(rng[1]), 0.01);
}
// abutments (p.95 / p.96)
foreach (var (name, elA, elC, top, tip, len) in new[] { ("Abutment 1", 524.699, 521.637, 519.153, 488.153, 14.583), ("Abutment 16", 519.744, 516.682, 514.185, 492.185, 14.563) })
{
    var su = unitsByName[name];
    Chk($"{name} EL. C/D cap top", su.CapTopAt(0) / ft, elC, 0.01);
    var bw = su.Backwall!.Value.Top;
    Chk($"{name} EL. A backwall top, end S0", bw[0].Y / ft, elA, 0.01);
    Chk($"{name} EL. B backwall top, end S1", bw[^1].Y / ft, elA, 0.01);
    Chk($"{name} cap length", (su.CapS1 - su.CapS0) / ft, len, 0.005);
    foreach (var fd in su.Foundations)
    {
        Chk($"{name} shaft at S {fd.S / ft:0.000} top", fd.TopZ / ft, top, 0.02);
        Chk($"{name} shaft at S {fd.S / ft:0.000} tip", fd.BottomZ / ft, tip, 0.01);
        Chk($"{name} shaft |S| = 4.500", Math.Abs(fd.S) / ft, 4.5, 0.005);
    }
}
// arch piers (p.101, 102)
foreach (var (name, elA, elE, elC, elF, top, tip) in new[] { ("Pier 10", 534.779, 534.726, 533.219, 527.219, 509.150, 492.150), ("Pier 11", 531.546, 531.494, 529.896, 523.896, 509.110, 480.110) })
{
    var su = unitsByName[name];
    Chk($"{name} EL. C cap top at CL pier", su.CapTopAt(0) / ft, elC, 0.01);
    Chk($"{name} EL. F cap bottom at column", su.Columns[0].TopZ / ft, elF, 0.01);
    Chk($"{name} shaft top", su.Foundations[0].TopZ / ft, top, 0.01);
    Chk($"{name} shaft tip", su.Foundations[0].BottomZ / ft, tip, 0.01);
    Chk($"{name} shaft offset from CL NHT (0'-7\")", Math.Abs(su.Foundations[0].S) / ft, 7 / 12.0, 1 / 16.0 / 12);
    Chk($"{name} cap length 24'-0", (su.CapS1 - su.CapS0) / ft, 24, 0.005);
    bool p10 = name == "Pier 10";
    var south = su.FixedSeats.First(x => x.Name == "arch bearing south");
    var north = su.FixedSeats.First(x => x.Name == "arch bearing north");
    Chk($"{name} arch seat south (EL. {(p10 ? "A" : "E")})", south.Z / ft, p10 ? elA : elE, 0.001);
    Chk($"{name} arch seat north (EL. {(p10 ? "E" : "A")})", north.Z / ft, p10 ? elE : elA, 0.001);
    Chk($"{name} CL arch brg from CL pier (6 7/8\")", Math.Abs(south.N) / In(1), 6.875, 1 / 16.0, "in");
    Chk($"{name} arch bearings 9'-10 + 9'-8 1/8 = 19'-6 1/8 apart", Math.Abs(south.S - north.S) / ft, 19 + 6.125 / 12, 1 / 16.0 / 12);
    double cap = su.Solids.Where(s => s.Kind is SubElementKind.Cap or SubElementKind.Pedestal).Sum(s => Math.Abs(s.Mesh.Volume())) / Math.Pow(ft * 3, 3);
    double col = su.Solids.Where(s => s.Kind == SubElementKind.Column).Sum(s => Math.Abs(s.Mesh.Volume())) / Math.Pow(ft * 3, 3);
    Chk($"{name} cap + pedestal concrete (p.102 table)", cap, 26.0, 0.1, "CY");
    Chk($"{name} column concrete (p.102 table)", col, p10 ? 13.2 : 10.8, 0.1, "CY");
}

// ---------------------------------------------------------------- 5. quantities (p.91, quantities.csv)
Print("== quantities (p.91)");
double Lf(double dia) => unitsByName.Values.SelectMany(u => u.Foundations).Where(x => x.Type == SBM.Core.Bridge.FoundationType.DrilledShaft && Math.Abs(x.SizeS - Ft(dia)) < 0.01)
    .Sum(x => (x.TopZ - x.BottomZ) / ft);
Chk("drill shaft (36 in) LF", Lf(3), 106, 1, "LF");
Chk("drill shaft (48 in) LF", Lf(4), 240, 1, "LF");
Chk("drill shaft (78 in) LF", Lf(6.5), 46, 1, "LF");
Chk("prestr conc girder (Tx28) LF", txLf, 1304.25, 0.5, "LF");
Chk("reinf conc slab (HPC), approach SF", area / (ft * ft), 10319, 10, "SF");

Print($"\nTOTAL: {pass} PASS, {fail} FAIL");
foreach (var x in fails) Print("  failed: " + x);
var diags = unitNames.SelectMany(u => (Doc.Model(u).Substructure?.Diagnostics.AsEnumerable() ?? Enumerable.Empty<SBM.Core.Framing.Diagnostic>()).Select(d => $"{u}: {d.Severity}: {d.Message}")).Where(s => !s.Contains(": Info:")).ToList();
Print($"\nSubstructure warnings ({diags.Count}):");
foreach (var d in diags) Print("  " + d);
