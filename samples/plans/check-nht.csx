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
double area = 0, areaPhys = 0;
// Deck ends: the slabs end at the SEJ-B armor faces (rail-joints-nht.csx: opening / 2 short of the CL SEJ at the piers, one opening short of the face of backwall at
// the abutments); the plans dimension and pay the slabs to the CL SEJ / face of backwall. Cut = (plan joint line) - (modeled deck end), normal to the support line.
double PlanDeckEnd(string sup) => sup is "Pier 10" or "Pier 11" ? -In(9.25) : 0;
double CutAt(SupportDefinition s) => PlanDeckEnd(s.Name) - s.DeckEndOffset;
for (int u = 0; u < 5; u++)
{
    var f = Doc.Framing(unitNames[u]);
    var bd = Doc.Bridge(unitNames[u]);
    double cb = CutAt(bd.Supports[0]), ce = CutAt(bd.Supports[^1]);
    double s0 = Cross(f.Deck!.BeginLinePoint - f.Deck.BeginLineDirection.Left * cb, f.Deck.BeginLineDirection),
           s1 = Cross(f.Deck.EndLinePoint + f.Deck.EndLineDirection.Left * ce, f.Deck.EndLineDirection);
    Chk($"{unitNames[u]} deck length along CL NHT (SEJ to SEJ)", (s1 - s0) / ft, unitLen[u], 0.01);
    double w = bd.Deck.RightEdgeOffset - bd.Deck.LeftEdgeOffset;
    areaPhys += f.Deck.Area;
    area += f.Deck.Area + cb * w / Math.Cos(bd.Supports[0].SkewDegrees * Math.PI / 180) + ce * w / Math.Cos(bd.Supports[^1].SkewDegrees * Math.PI / 180);
}
Print($"INFO  approach slabs end at the SEJ-B armor faces: physical slab area {areaPhys / (ft * ft):0.0} SF; the plan lengths / SF are to the CL SEJ (+ the cut-back strips)");

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
    // v7 (arch-nht.csx): the arch pedestal tops are modeled at the bottom of the bearing stack hung from the steel (p.107 note 8 / p.102 note 1: the contractor
    // adjusts the pedestal elevations) - then the p.102 EL is a CONFLICT row (not counted) and the seat is checked against the arch bearing seat (stack bottom).
    var supDef = Doc.Project.Bridges.SelectMany(b => b.Supports).First(x => x.Name == name && x.Substructure.CapBlocks.Any(cb => cb.Kind == CapBlockKind.Pedestal));
    var archSeats = Doc.Project.Bridges.Where(b => b.Arch != null).SelectMany(b => Doc.Steel(b.Name).BearingSeats).Where(bs => bs.Support == name).ToList();
    foreach (var (seat, nm, el) in new[] { (south, $"{name} arch seat south (EL. {(p10 ? "A" : "E")})", p10 ? elA : elE), (north, $"{name} arch seat north (EL. {(p10 ? "E" : "A")})", p10 ? elE : elA) })
    {
        var cbk = supDef.Substructure.CapBlocks.First(cb => cb.Name == seat.Name);
        bool adjusted = cbk.Reported?.Any(r => r.Contains("p.107 note 8")) == true;
        if (!adjusted) { Chk(nm, seat.Z / ft, el, 0.001); continue; }
        var w = su.World(seat.S, seat.N, seat.Z);
        var bs = archSeats.Where(x => x.Location != null).OrderBy(x => (x.Location!.Value - w.XY).Length).FirstOrDefault();
        Print($"CONFLICT  {nm}: model {seat.Z / ft:0.000} plan p.102 {el:0.000} ({seat.Z / ft - el:+0.000;-0.000} ft) - pedestal adjusted to the bearing (p.107 note 8 / p.102 note 1), not counted");
        if (bs != null) Chk($"{nm} = bottom of the arch bearing stack (conc. brg. surface, p.107 Section A-A)", seat.Z / ft, bs.SeatElevation / ft, 0.001);
    }
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

// ---------------------------------------------------------------- 6. railing, curbs, joints, mow strips, T80SS(MOD) (rail-joints-nht.csx; p.84-91, 97, 99, 105-106, 113)
Print("== RAIL (PED), curbs, SEJ-B(MOD) joints + BS-EJCP + IGTS, mow strips, T80SS(MOD)");
var allNht = Doc.Project.Bridges.Where(b => b.Structure == Doc.Bridge(unitNames[0]).Structure).Select(b => b.Name).ToList();
bool archBuilt = allNht.Any(n => Doc.Bridge(n).Arch != null);
var runs = allNht.SelectMany(n => Doc.Model(n).Steel?.Railings?.Runs.AsEnumerable() ?? Enumerable.Empty<SBM.Core.Railing.RailingRun>()).ToList();
// The approach runs stop at the slab ends (armor faces); the plan "length of rail" runs to the CL SEJ / face of backwall: the cut-back strips are added back
// (cut / cos(skew) along the curb line) for the comparison. The arch runs are limited at the CL SEJ already.
double GapLf(string bridge, SBM.Core.Railing.RailingRun r)
{
    var bd = Doc.Bridge(bridge);
    if (bd.Arch != null || bd.Railings == null) return 0;
    var def = bd.Railings.First(x => x.Name == r.Name);
    double g = 0;
    if (def.BeginStation == null) g += CutAt(bd.Supports[0]) / Math.Cos(bd.Supports[0].SkewDegrees * Math.PI / 180);
    if (def.EndStation == null) g += CutAt(bd.Supports[^1]) / Math.Cos(bd.Supports[^1].SkewDegrees * Math.PI / 180);
    return g;
}
var runsB = allNht.SelectMany(n => (Doc.Model(n).Steel?.Railings?.Runs.AsEnumerable() ?? Enumerable.Empty<SBM.Core.Railing.RailingRun>()).Select(r => (n, r))).ToList();
double RailLf(string type, int side) => runsB.Where(x => x.r.TypeName == type && Math.Sign(x.r.Offset) == side).Sum(x => x.r.Length + GapLf(x.n, x.r)) / ft;
double RailLfPhys(string type) => runs.Where(r => r.TypeName == type).Sum(r => r.Length) / ft;
// p.84-88 "overall length of rail" (face of rail = curb line, 7'-0 from CL NHT)
Chk("RAIL (PED)(LOW) LT LF (p.84-88)", RailLf("RAIL (PED)(LOW)", -1), 307.93, 0.01, "LF");
Chk("RAIL (PED)(LOW) RT LF (p.84-88)", RailLf("RAIL (PED)(LOW)", 1), 270.95, 0.01, "LF");
if (archBuilt)
{
    Chk("RAIL (PED)(HIGH) LT LF (p.84-88)", RailLf("RAIL (PED)(HIGH)", -1), 571.28, 0.01, "LF");
    Chk("RAIL (PED)(HIGH) RT LF (p.84-88)", RailLf("RAIL (PED)(HIGH)", 1), 572.97, 0.01, "LF");
    Chk("5110 RAIL (PED)(HIGH) LF (p.91)", RailLf("RAIL (PED)(HIGH)", -1) + RailLf("RAIL (PED)(HIGH)", 1), 1144.2, 0.1, "LF");
}
else Print("INFO  RAIL (PED)(HIGH): the arch span is not in this model (run arch-nht.csx) - high rail LF not compared");
Chk("5110 RAIL (PED)(LOW) LF (p.91)", RailLf("RAIL (PED)(LOW)", -1) + RailLf("RAIL (PED)(LOW)", 1), 579.0, 0.1, "LF");
Print($"INFO  railing modeled on the curbs (gaps at the joints not counted): LOW {RailLfPhys("RAIL (PED)(LOW)"):0.00} LF, HIGH {RailLfPhys("RAIL (PED)(HIGH)"):0.00} LF");
// p.91 splits the pay quantities per unit (low 215.8 + 194.0 + 169.2 = 579.0, 0.12 LF more than the p.84-88 layout lengths; high 152.9 + 336.0 / 156.4 + 91.7 + 407.2)
double UnitLf(string unit, string type) => runsB.Where(x => x.n == unit && x.r.TypeName == type).Sum(x => x.r.Length + GapLf(x.n, x.r)) / ft;
Print($"INFO  p.91 per unit, LOW: Unit 1 model {UnitLf("NHT Unit 1", "RAIL (PED)(LOW)"):0.00} / plan 215.8, Unit 2 {UnitLf("NHT Unit 2", "RAIL (PED)(LOW)"):0.00} / 194.0, Unit 3 {UnitLf("NHT Unit 3", "RAIL (PED)(LOW)"):0.00} / 169.2");
Print($"INFO  p.91 per unit, HIGH: Unit 3 model {UnitLf("NHT Unit 3", "RAIL (PED)(HIGH)"):0.00} / plan 156.4, Unit 4 {UnitLf("NHT Unit 4", "RAIL (PED)(HIGH)"):0.00} / 152.9 + 91.7 = 244.6, Unit 5 {UnitLf("NHT Unit 5", "RAIL (PED)(HIGH)"):0.00} / 336.0"
    + (archBuilt ? $", arch {allNht.Where(n => Doc.Bridge(n).Arch != null).Sum(n => UnitLf(n, "RAIL (PED)(HIGH)")):0.00} / 407.2" : ""));
// Posts / fence (2026-10-19): the envelope is replaced by posts at the p.85-88 elevation layout (8'-0 on CL NHT stations; LOW grid 20+51.74, HIGH grid 23+38.74) + fence fabric panels.
var allPostSt = runs.SelectMany(r => r.PostStations.Zip(r.PostStations.Skip(1), (p, q) => (q - p) / ft)).ToList();
Print($"INFO  RAIL (PED) type: not a TxDOT standard (RL-PR3 / PR11 / PR22 / PR3-HD / CLF-RO checked, see rail-joints-nht.csx); runs {runs.Count}, posts modeled {runs.Sum(r => r.Posts)} (post thickness / base plates / anchors UNRESOLVED U14, p.84 note 2 'diagrammatic'), fence fabric panels {runs.Sum(r => r.FabricPanels)}");
Print($"INFO  post spacing along CL NHT (p.85-88 elevations 28.8 pt = 8.00 ft): min {(allPostSt.Count > 0 ? allPostSt.Min() : 0):0.000} max {(allPostSt.Count > 0 ? allPostSt.Max() : 0):0.000} ft");
foreach (var (type, dl) in new[] { ("RAIL (PED)(LOW)", 50.0), ("RAIL (PED)(HIGH)", 100.0) })
{
    var rr = runs.Where(r => r.TypeName == type).ToList();
    double lf = rr.Sum(r => r.Length) / ft;
    if (lf <= 0) continue;
    Print($"INFO  {type}: modeled mass {rr.Sum(r => r.Mass) * 2.20462 / lf:0.0} PLF (posts, handrail pipe weight not counted, fabric {rr.Sum(r => r.FabricMass) * 2.20462 / lf:0.0} PLF at the nominal 0.40 lb/ft2 chain link guess) vs the p.83 design dead load {dl:0} PLF ({rr.Sum(r => r.FabricArea) / (ft * ft):N0} sf of fabric, {rr.Sum(r => r.Posts)} posts)");
}
// curbs: 10" x 4" with two 3/4" chamfers = 39.44 in2; both sides together run 2 x the CL NHT length of the approach decks (curvature terms cancel) less the cut-back strips
double curbV = unitNames.Sum(n => Doc.Model(n).Concrete?.Barriers.Where(x => x.Name.EndsWith("curb")).Sum(x => Math.Abs(x.Mesh.Volume())) ?? 0) / Math.Pow(ft, 3);
double curbA = (10 * 4 - 2 * 0.5 * 0.75 * 0.75) / 144.0;
double cutLen = unitNames.Sum(n => { var bd = Doc.Bridge(n); return CutAt(bd.Supports[0]) / Math.Cos(bd.Supports[0].SkewDegrees * Math.PI / 180) + CutAt(bd.Supports[^1]) / Math.Cos(bd.Supports[^1].SkewDegrees * Math.PI / 180); }) / ft;
Chk("approach curb volume (10\" x 4\", 3/4\" chamfers, both sides, to the slab ends; p.89, 113) CY", curbV / 27, curbA * 2 * (861.57 - 203.32 - cutLen) / 27, 0.05, "CY");

// joints: p.91 0454 SEALED EXPANSION JOINT (4 IN)(SEJ-B)(MOD) 75 LF (CSJ 0918: Abut 1, P4, P7, P13, Abut 16) + 32 LF (CSJ 0047: P10, P11) = 107 LF
// = 15 LF per radial joint, 16 at the 19.58 deg skewed P10 / P11. Model: the seal / armor between the curb toes (14'-0 trail, / cos(skew) at P10 / P11).
// SEJ-B "at sidewalk" / seal upturn detail: the seal turns up the 4" curb faces and is trimmed 1/2" below the curb top -> + 2 x 3 1/2" per joint; the plan LF
// is that length rounded UP to the foot per joint (14.58 -> 15, 15.44 -> 16): reproduced here (DERIVED; the 2'-0 skew termination of SEJ-B does not apply to it).
var joints = unitNames.SelectMany(n => Doc.Model(n).Concrete?.Joints.AsEnumerable() ?? Enumerable.Empty<SBM.Core.Concrete.ExpansionJointResult>()).ToList();
var devices = joints.Where(j => j.Body != null).ToList();
Chk("SEJ-B(MOD) joints modeled (7 locations, p.84-88)", devices.Count, 7, 0, "EA");
Chk("IGTS thickened slab ends (10 approach deck ends = every unit end, p.109-117 'see IGTS')", joints.Count(j => j.EndBlock != null), 10, 0, "EA");
double upturn = 2 * In(4 - 0.5);
double jArchM = devices.Where(j => j.Support is "Pier 10" or "Pier 11").Sum(j => j.Length) / ft, jAllM = devices.Sum(j => j.Length) / ft;
Print($"INFO  SEJ-B armor / seal between the curb toes: CSJ 0918 {jAllM - jArchM:0.00} LF, CSJ 0047 {jArchM:0.00} LF, total {jAllM:0.00} LF (the earlier 111.57 LF was the full 15'-8 deck width)");
double PayLf(SBM.Core.Concrete.ExpansionJointResult j) => Math.Ceiling((j.Length + upturn) / ft - 1e-6);
foreach (var j in devices) Print($"INFO  {j.Support}: seal {j.Length / ft:0.000} + upturns {upturn / ft:0.000} = {(j.Length + upturn) / ft:0.000} ft -> {PayLf(j):0} LF");
double jArch = devices.Where(j => j.Support is "Pier 10" or "Pier 11").Sum(PayLf), jAll = devices.Sum(PayLf);
Chk("SEJ-B(MOD) LF CSJ 0918 (5 joints, p.91; seal incl. curb upturns, per joint rounded up)", jAll - jArch, 75, 0.1, "LF");
Chk("SEJ-B(MOD) LF CSJ 0047 (P10 + P11, p.91; idem)", jArch, 32, 0.1, "LF");
Chk("0454 SEJ-B(MOD) LF total (p.91)", jAll, 107, 0.1, "LF");
// arch side of the P10 / P11 joints: the far armor plate is cast into the arch deck end; where does the arch slab (knuckle end web face) end?
if (archBuilt)
{
    var archName = allNht.First(n => Doc.Bridge(n).Arch != null);
    var acm = Doc.Model(archName).Concrete;
    var ats = new SBM.Core.Railing.TopSurface(new[] { acm?.Deck, acm?.Haunches }.Where(m => m != null).Select(m => m!));
    foreach (var (pier, dir) in new[] { ("Pier 10", 1), ("Pier 11", -1) })
    {
        var sup = Doc.Project.Bridges.SelectMany(b => b.Supports).First(x => x.Name == pier);
        double sk = sup.SkewDegrees * Math.PI / 180;
        var tg = Vec2.FromAngle(al.AngleAt(sup.Station)); var rg = tg.Right;
        var nrm = -rg * Math.Sin(sk) + tg * Math.Cos(sk);
        var sej = al.PointAt(sup.Station, 0) - nrm * dir * In(9.25);
        double first = double.NaN;
        for (double x = -In(3); x <= In(24); x += In(1.0 / 32)) if (ats.At(sej + nrm * dir * x) is double) { first = x; break; }
        Print($"INFO  {pier}: the arch slab begins {first / In(1):0.00}\" from the CL SEJ at CL NHT; the SEJ-B 2 1/2\" opening puts the arch-side armor face at 1.25\" -> the arch deck overlaps the opening by {(In(1.25) - first) / In(1):0.00}\" (arch model unchanged, CONFLICT reported)");
    }
}
// armor / studs / cover plates (SEJ-B, BS-EJCP 10-2024: post-date the plans)
var hw = devices.SelectMany(j => j.Hardware.Select(p => (j, p))).ToList();
foreach (var j in devices)
{
    int studs = j.Hardware.Count(p => p.Category == SBM.Core.Steel.PartCategory.JointStud);
    double arm = j.Hardware.Where(p => p.Category == SBM.Core.Steel.PartCategory.JointArmor).Sum(p => p.Length) / ft;
    // SEJ-B: studs at 6" max, first 3" from each plate end -> n = ceil((L - 6") / 6") + 1 per plate
    int expect = j.Hardware.Where(p => p.Category == SBM.Core.Steel.PartCategory.JointArmor).Sum(p => (int)Math.Ceiling((p.Length - In(6)) / In(6) - 1e-9) + 1);
    Chk($"SEJ-B stud anchors at {j.Support} (5/8\" at 6\" max on both armor plates)", studs, expect, 0, "EA");
    Print($"INFO  {j.Support}: armor 2 x PL 1/2 x 4 = {arm:0.000} ft, {LbOf(j.Hardware.Where(p => p.Category == SBM.Core.Steel.PartCategory.JointArmor).Sum(p => p.Mass)):0} lb");
}
// BS-EJCP: p.91 note: Abut 1 194, P4 194, P7 194, P10 206, P11 206, P13 194, Abut 16 194 = 1,382 lb (STR STEEL (MISC NON-BRIDGE)); the standard: "estimated
// weight of one sidewalk expansion joint cover plate is 14 plf". Plate length checked at 14 plf (1 lb); the modeled steel (1/4" x 1'-4 = 13.61 plf) reported.
var ejcpPlan = new Dictionary<string, double> { ["Abutment 1"] = 194, ["Pier 4"] = 194, ["Pier 7"] = 194, ["Pier 10"] = 206, ["Pier 11"] = 206, ["Pier 13"] = 194, ["Abutment 16"] = 194 };
double cpModel = 0, cpAt14 = 0;
foreach (var j in devices)
{
    var cp = j.Hardware.FirstOrDefault(p => p.Category == SBM.Core.Steel.PartCategory.JointCoverPlate);
    if (cp == null) { fail++; fails.Add($"BS-EJCP at {j.Support}"); Print($"FAIL  BS-EJCP at {j.Support}: not modeled"); continue; }
    double at14 = 14 * cp.Length / ft, mlb = LbOf(cp.Mass);
    cpModel += mlb; cpAt14 += at14;
    Chk($"BS-EJCP at {j.Support}: plate length x 14 plf (p.91 note)", at14, ejcpPlan[j.Support], 1.0, "lb");
    Print($"INFO  BS-EJCP at {j.Support}: {cp.Length / ft:0.000} ft, modeled steel {mlb:0.0} lb, {j.Hardware.Count(p => p.Category == SBM.Core.Steel.PartCategory.JointAnchor)} sleeve anchors 3/8\" x 2 1/2\"");
}
Chk("BS-EJCP total at 14 plf (p.91 STR STEEL (MISC NON-BRIDGE) 1,382 lb)", cpAt14, 1382, 3, "lb");
Print($"INFO  BS-EJCP modeled steel {cpModel:0} lb (1/4\" x 1'-4 = 13.61 plf) vs the p.91 1,382 lb at the standard's 14 plf estimate ({cpModel - 1382:+0;-0} lb)");
foreach (var j in joints.Where(x => x.EndBlock != null))
    Print($"INFO  IGTS thickened slab end at {j.Support} ({(j.AtBegin ? "begin" : "end")}): {j.EndBlock!.Volume / Math.Pow(ft, 3):0.00} CF");
Print($"INFO  IGTS thickened slab ends total {joints.Where(x => x.EndBlock != null).Sum(x => x.EndBlock!.Volume) / Math.Pow(ft * 3, 3):0.000} CY (IGTS 'quantity adjustments' are not in the p.91 slab SF)");
// mow strips: p.91 0432 RIPRAP (MOW STRIP)(4 IN) 6.1 CY
double mow = unitNames.SelectMany(n => Doc.Model(n).Substructure?.Units.AsEnumerable() ?? Enumerable.Empty<SubstructureUnit>())
    .SelectMany(un => un.Solids).Where(s => s.Kind == SubElementKind.MowStrip).Sum(s => Math.Abs(s.Mesh.Volume())) / Math.Pow(ft * 3, 3);
Chk("0432 riprap (mow strip)(4 in) CY (p.91)", mow, 6.1, 0.05, "CY");
// T80SS(MOD): p.91 0450 6091 RAIL (TY T80SS)(MOD) 78.0 LF (Piers 10 & 11); p.105 38'-2 limits of cutting per side; p.106 4'-6 high, 9'-1 + 4'-6 + 7'-11
var t80 = unitNames.SelectMany(n => Doc.Model(n).Substructure?.Units.AsEnumerable() ?? Enumerable.Empty<SubstructureUnit>())
    .SelectMany(un => un.Solids.Select(s => (un, s))).Where(x => x.s.Kind == SubElementKind.TrafficBarrier).ToList();
var t80Def = unitNames.SelectMany(n => Doc.Bridge(n).Supports).Where(s => s.Substructure?.GroundBarriers != null).SelectMany(s => s.Substructure!.GroundBarriers!).ToList();
double t80Lf = t80Def.Sum(g => Enumerable.Range(1, g.Path.Count - 1).Sum(i => Math.Sqrt(Math.Pow(g.Path[i].S - g.Path[i - 1].S, 2) + Math.Pow(g.Path[i].N - g.Path[i - 1].N, 2)))) / ft;
Chk("T80SS(MOD) barriers modeled (Pier 10 east / west, p.105)", t80.Count, 2, 0, "EA");
Chk("T80SS(MOD) length per barrier = p.105 38'-2 limits of cutting", t80Lf / Math.Max(1, t80Def.Count), 38 + 2 / 12.0, 0.01, "LF");
Chk("0450 RAIL (TY T80SS)(MOD) LF (p.91, Piers 10 & 11)", t80Lf, 78.0, 0.1, "LF");
foreach (var g in t80Def) Chk($"{g.Name}: height of the straight part (p.106 4'-6)", g.Path.Max(v => v.Height) / ft, 4.5, In(1.0 / 16) / ft);
foreach (var (un, s) in t80) Print($"INFO  {s.Name}: {Math.Abs(s.Mesh.Volume()) / Math.Pow(ft * 3, 3):0.00} CY, base {s.Mesh.Bounds().Min.Z / ft:0.000} (shaft top + 2'-9, flagged)");
Print($"INFO  replacement pavement under the barriers: {unitNames.SelectMany(n => Doc.Model(n).Substructure?.Units.AsEnumerable() ?? Enumerable.Empty<SubstructureUnit>()).SelectMany(un => un.Solids).Where(s => s.Kind == SubElementKind.PavementStrip).Sum(s => Math.Abs(s.Mesh.Volume())) / Math.Pow(ft * 3, 3):0.00} CY; T80SS(MOD) bill (p.106, one side, contractor's information) R 682 + RH 151 + H1 170 + S1 699 + U 120 = 1,823 lb - bars not placed (UNRESOLVED)");
// ---------------------------------------------------------------- 6. reinforcing (bar-lists.csv: p.95-104 tables, approach unit sheets p.109-117, arch deck p.154)
// Counts must match the plan tables exactly; weights: the scheduled weight (count x listed length x A615 unit weight) vs the printed weight within 3 lb or 1 %
// (the plan rounds); bars of variable geometry (S2: plan length = average) within 3 %. Elements without a plan bar list (shaft cages, approach decks: 2.3 lb/SF
// estimate) are INFO rows. Cover violations are listed (bars are never moved).
Print("== reinforcing (bar tables)");
double LbOf(double kg) => kg / 0.45359237;
void Info(string s) => Print("INFO  " + s);
var elementRows = new List<(string element, double model, double plan, string basis)>();
var subUnits = unitNames.SelectMany(u => Doc.Model(u).Substructure!.Units.Where(x => x.Definition.Enabled && x.Definition.Reinforcement.PlanBarSets.Count > 0).Select(x => (b: u, x))).ToList();
var archBill = new Dictionary<string, (int n, double w)>();
foreach (var (bname, su) in subUnits)
{
    var sm = Doc.Model(bname).Substructure!;
    double mTab = 0, pTab = 0, mAll = 0;
    bool arch = su.Name is "Pier 10" or "Pier 11";
    foreach (var set in su.Definition.Reinforcement.PlanBarSets)
    {
        var mk = sm.Marks.FirstOrDefault(x => x.Unit == su.Name && x.Mark == set.Mark);
        if (mk == null) { if (set.PlanCount > 0) { fail++; fails.Add($"{su.Name} {set.Mark} not modeled"); Print($"FAIL  {su.Name} {set.Mark}: not modeled"); } continue; }
        double w = LbOf(mk.TotalMass);
        mAll += w;
        if (arch) { if (set.PlanCount != null) { archBill.TryGetValue(set.Mark, out var a); archBill[set.Mark] = (a.n + mk.Count, a.w + w); } continue; }
        if (set.PlanCount is int pc && set.PlanWeightLb is double pw)
        {
            mTab += w; pTab += pw;
            bool cOk = mk.Count == pc; if (cOk) pass++; else { fail++; fails.Add($"{su.Name} {set.Mark} count"); }
            double tol = set.ListedLength > 0 ? Math.Max(3, 0.01 * pw) : 0.03 * pw + 3;
            bool wOk = Math.Abs(w - pw) <= tol; if (wOk) pass++; else { fail++; fails.Add($"{su.Name} {set.Mark} weight"); }
            Print($"{(cOk && wOk ? "PASS" : "FAIL")}  {su.Name} {set.Mark} ({set.Size}): count {mk.Count} plan {pc}; weight {w:0} lb plan {pw:0} ({Math.Round(w - pw):+0;-0;0}){(set.ListedLength <= 0 ? " [geometric lengths]" : "")}");
        }
        else Info($"{su.Name} {set.Mark} ({set.Size}, {set.Element}): {mk.Count} bars, {w:0} lb - not tabulated in the plans");
    }
    if (!arch)
    {
        elementRows.Add(($"{su.Name} (tabulated marks)", mTab, pTab, "plan bar tables"));
        if (mAll - mTab > 0.5) elementRows.Add(($"{su.Name} shaft steel (not tabulated)", mAll - mTab, double.NaN, "FD / FV / spiral, no plan weight"));
    }
}
// arch piers: the p.104 bill is for both piers (V2 = Pier 10, V3 = Pier 11)
var archRows = Csv("bar-lists.csv").Where(r => r[0] == "Arch piers 10 & 11").ToList();
foreach (var r in archRows.Where(r => !r[1].StartsWith("TOTAL")))
{
    string mk = r[1]; int pn = int.Parse(r[3]); double pw = double.Parse(r[6]);
    archBill.TryGetValue(mk, out var a);
    bool cOk = a.n == pn; if (cOk) pass++; else { fail++; fails.Add($"Arch piers {mk} count"); }
    bool wOk = Math.Abs(a.w - pw) <= Math.Max(3, 0.01 * pw) + 2; if (wOk) pass++; else { fail++; fails.Add($"Arch piers {mk} weight"); }
    Print($"{(cOk && wOk ? "PASS" : "FAIL")}  Arch piers 10 + 11 {mk}: count {a.n} plan {pn}; weight {a.w:0} lb plan {pw:0} ({Math.Round(a.w - pw):+0;-0;0})");
}
foreach (var name in new[] { "Pier 10", "Pier 11" })
{
    var (bn, su) = subUnits.First(x => x.x.Name == name);
    var sm = Doc.Model(bn).Substructure!;
    var bill = su.Definition.Reinforcement.PlanBarSets.Where(s => s.PlanCount > 0).Select(s => s.Mark).ToHashSet();
    double mb = sm.Marks.Where(m => m.Unit == name && bill.Contains(m.Mark)).Sum(m => LbOf(m.TotalMass));
    double other = sm.Marks.Where(m => m.Unit == name && !bill.Contains(m.Mark)).Sum(m => LbOf(m.TotalMass));
    foreach (var m in sm.Marks.Where(m => m.Unit == name && !bill.Contains(m.Mark))) Info($"{name} {m.Mark}: {m.Count} bars, {LbOf(m.TotalMass):0} lb - not in the p.104 bill ({m.Location})");
    double plan = double.Parse(archRows.First(r => r[1] == $"TOTAL P{name[5..]}")[6]);
    elementRows.Add(($"{name} (p.104 bill marks)", mb, plan, "p.104 'reinforcing steel pier n'"));
    elementRows.Add(($"{name} shaft steel + H1 (not in the bill)", other, double.NaN, "28 #11 + #7 hoops + V1 dowels; H1 schedule-only"));
}
// approach decks (2.3 lb/SF estimate) and arch deck (p.154 bill)
for (int k = 0; k < unitNames.Length; k++)
{
    var c = Doc.Model(unitNames[k]).Concrete!;
    double w = c.Marks.Sum(m => LbOf(m.TotalMass));
    var row = Csv("bar-lists.csv").First(r => r[0].StartsWith($"Approach deck Unit {k + 1}") && r[1] == "TOTAL");
    double plan = double.Parse(row[6]);
    Info($"{unitNames[k]} deck: {string.Join(", ", c.Marks.Select(m => $"{m.Mark} {m.Count} ({LbOf(m.TotalMass):0} lb)"))} = {w:0} lb vs plan estimate {plan:0} lb (2.3 lb/SF; P, G/H/J/M{(k == 2 ? ", UP" : "")} not modeled - UNRESOLVED)");
    elementRows.Add(($"{unitNames[k]} approach deck", w, plan, "plan = 2.3 lb/SF estimate (note 2), no bar list"));
}
if (Doc.Project.Bridges.Any(b => b.Name == "Arch span 10"))
{
    var c = Doc.Model("Arch span 10").Concrete!;
    var bill = new (string m, int n, double w)[] { ("A1", 606, 8752), ("B1", 303, 5746), ("D1", 162, 5637), ("D2", 162, 5637), ("D3", 68, 527), ("D4", 48, 1670), ("S1", 606, 1316), ("S2", 32, 92) };
    double mt = 0;
    foreach (var (m, n, w) in bill)
    {
        var mk = c.Marks.FirstOrDefault(x => x.Mark == m);
        double mw = mk == null ? 0 : LbOf(mk.TotalMass); mt += mw;
        bool ok = mk != null && mk.Count == n; if (ok) pass++; else { fail++; fails.Add($"Arch deck {m} count"); }
        Print($"{(ok ? "PASS" : "FAIL")}  Arch deck {m}: count {mk?.Count ?? 0} plan {n}; weight {mw:0} lb plan {w:0} ({Math.Round(mw - w):+0;-0;0})");
    }
    elementRows.Add(("Arch deck (span 10)", mt, 29377, "p.154 bill of reinforcing steel"));
}
Print("\n== reinforcing weight by element (lb): model vs plan");
foreach (var (e, mw, pw, basis) in elementRows)
    Print($"  {e,-46} model {mw,8:0}  plan {(double.IsNaN(pw) ? "      -" : pw.ToString("0").PadLeft(7))}  {(double.IsNaN(pw) ? "" : $"({mw - pw:+0;-0}, {(mw - pw) / Math.Max(1, pw) * 100:+0.0;-0.0} %)")}  {basis}");
// cover report
Print("\n== clear cover (plan bars, never moved): violations beyond 1/8\"");
foreach (var (bname, su) in subUnits)
    foreach (var g in su.CoverViolations.GroupBy(v => v.Kind))
        Info($"{su.Name} {g.Key}: {g.Count()} bar(s), worst {g.Max(v => v.Excess) / In(1):0.00}\" at the {g.MaxBy(v => v.Excess)!.Face}");
foreach (var u in unitNames.Concat(Doc.Project.Bridges.Where(b => b.Name == "Arch span 10").Select(b => b.Name)))
    foreach (var g in Doc.Model(u).Concrete!.CoverViolations.GroupBy(v => v.Mark))
        Info($"{u} deck {g.Key}: {g.Count()} bar(s), worst {g.Max(v => v.Excess) / In(1):0.00}\" ({string.Join(", ", g.GroupBy(v => v.Face).Select(f => $"{f.Key} {f.Count()}"))})");

Print($"\nTOTAL: {pass} PASS, {fail} FAIL");
foreach (var x in fails) Print("  failed: " + x);
var diags = unitNames.SelectMany(u => (Doc.Model(u).Substructure?.Diagnostics.AsEnumerable() ?? Enumerable.Empty<SBM.Core.Framing.Diagnostic>()).Select(d => $"{u}: {d.Severity}: {d.Message}")).Where(s => !s.Contains(": Info:")).ToList();
Print($"\nSubstructure warnings ({diags.Count}):");
foreach (var d in diags) Print("  " + d);
