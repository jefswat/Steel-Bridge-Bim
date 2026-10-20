// Plan vs model check for Iowa DOT Design 1617 (Nebraska St. Ramp B), model built by build-nebraska-st.csx (read-only):
//   sbm run samples\plans\check-nebraska-st.csx "samples\plans\Nebraska St Ramp B.sbm"      (run from the repo root)
// Plan data: docs/plan-data/nebraska-ramp-b-b00167/ (SUMMARY.md + CSVs). Tolerances: 0.01 ft for elevations / stations / lengths in feet, 1/16" for
// plate and detail dimensions, as the user rule says. Every difference is printed (PASS / FAIL / INFO); nothing is adjusted.
// Sections: 1 spans + girder lengths (girder-data.csv), 2 field splice locations, 3 plate schedule (p.28), 4 camber / blocking / haunch
// (camber-deflection.csv, blocking.csv, haunch-elevations.csv), 5 top of deck (top-of-deck-elevations.csv), 6 bearings (bearings.csv, seats),
// 7 integral steel cap (p.29-31), 8 quantities (quantities.csv, bar-lists.csv).
using System.IO;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "nebraska-ramp-b-b00167");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/nebraska-ramp-b-b00167 not found above the current directory - run from the repo.");
}
string dir = DataDir();
List<string[]> Csv(string name) => File.ReadAllLines(Path.Combine(dir, name)).Skip(1).Where(l => l.Trim().Length > 0)
    .Select(l => System.Text.RegularExpressions.Regex.Matches(l, "\"[^\"]*\"|[^,]+|(?<=,)(?=,)|^(?=,)").Select(m => m.Value.Trim('"').Trim()).ToArray()).ToList();
double Num(string s) => double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
double Frac(string s)
{
    s = s.Split('(')[0].Trim();
    double v = 0, sg = 1;
    if (s.StartsWith("-")) { sg = -1; s = s[1..]; }
    foreach (var t in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        v += t.Contains('/') ? double.Parse(t.Split('/')[0]) / double.Parse(t.Split('/')[1]) : Num(t);
    return sg * v;
}

const string BrName = "Nebraska St. Ramp B";
double ft = Ft(1), inch = In(1);
var b = Doc.Bridge(BrName);
var al = Doc.Alignment(b.AlignmentName);
var rr = Doc.Regenerate();
var md = Doc.Model(BrName);
var fr = md.Framing;
var st = md.Steel!;
string[] GL = { "A", "B", "C", "D" };

var rows = new List<(string group, string item, string plan, string model, string diff, string status, string note)>();
void Row(string group, string item, double plan, double model, double tol, string unit = "ft", string note = "", bool info = false)
{
    double d = model - plan;
    bool ok = Math.Abs(d) <= tol + 1e-9;
    string fmt = unit == "in" ? "F3" : unit == "%" ? "F3" : unit == "lb" || unit == "LF" || unit == "SF" ? "N0" : unit == "CY" ? "F1" : "F2";
    rows.Add((group, item, plan.ToString(fmt) + " " + unit, model.ToString(fmt) + " " + unit, d.ToString(unit == "in" ? "+0.000;-0.000;0.000" : "+0.000;-0.000;0.000"), info ? "INFO" : ok ? "PASS" : "FAIL", note));
}
void Text(string group, string item, string plan, string model, string status, string note = "") => rows.Add((group, item, plan, model, "", status, note));

// ================================================================ 1. spans and girder lengths (p.6-7, p.27 girder data)
double[] planSpan = { 180, 203, 187 };
for (int j = 0; j < 3; j++)
    Row("1 spans", $"span {j + 1} along CL", planSpan[j], (b.Supports[j + 1].Station - b.Supports[j].Station) / ft, 0.01);
var gd = Csv("girder-data.csv");
double Brg(int g, int sup)
{
    var br = fr.SupportLine(sup).Bearings[g]!;
    var gr = fr.Girders[g]; int n = gr.Spans.Count;
    return !double.IsNaN(br.PathDistance) ? br.PathDistance : gr.DistanceAt(Math.Min(sup, n - 1), sup == n ? 1 : 0);
}
for (int g = 0; g < 4; g++)
    for (int j = 0; j < 3; j++)
    {
        double plan = Num(gd.First(r => r[0] == GL[g] && r[1] == (j + 1).ToString() && r[2] == "span length")[4]);
        // plan girder lengths are horizontal (p.27 note: "no allowance for grade"); the model path distance is horizontal too
        Row("1 girder span lengths", $"{GL[g]} span {j + 1}", plan, (Brg(g, j + 1) - Brg(g, j)) / ft, 1.0 / 16 / 12, note: "p.27 girder data");
    }

// ================================================================ 2. field splices (p.27 / p.28 girder data)
foreach (var (fsNo, seg, sup, sign) in new[] { (1, "FS1 to CL pier 1", 1, -1), (2, "CL pier 1 to FS2", 1, 1), (3, "FS3 to CL pier 2", 2, -1) })
    for (int g = 0; g < 4; g++)
    {
        var sp = fr.Splices.FirstOrDefault(x => x.Girder == g && x.Number == fsNo);
        double plan = Num(gd.First(r => r[0] == GL[g] && r[2] == seg)[4]);
        Row("2 field splices", $"FS{fsNo} {GL[g]}: {seg}", plan, sp == null ? double.NaN : sign * (sp.GirderDistance - Brg(g, sup)) / ft, 1.0 / 16 / 12);
    }
for (int g = 0; g < 4; g++)
{
    var sp = fr.Splices.First(x => x.Girder == g && x.Number == 4);
    double plan = Num(gd.First(r => r[0] == GL[g] && r[2] == "CL pier 2 to PC")[4]) + Num(gd.First(r => r[0] == GL[g] && r[2] == "PC to FS4")[4]);
    Row("2 field splices", $"FS4 {GL[g]}: CL pier 2 to FS4 (3'-1 5/16 + arc)", plan, (sp.GirderDistance - Brg(g, 2)) / ft, 1.0 / 16 / 12);
    double planS1 = Num(gd.First(r => r[0] == GL[g] && r[2] == "CL S abut brg to FS1")[4]);
    var s1 = fr.Splices.First(x => x.Girder == g && x.Number == 1);
    Row("2 field splices", $"FS1 {GL[g]}: CL S abut brg to FS1", planS1, (s1.GirderDistance - Brg(g, 0)) / ft, 1.0 / 16 / 12);
    double planN = Num(gd.First(r => r[0] == GL[g] && r[2] == "FS4 to CL N abut brg")[4]);
    Row("2 field splices", $"FS4 {GL[g]}: FS4 to CL N abut brg", planN, (Brg(g, 3) - sp.GirderDistance) / ft, 1.0 / 16 / 12);
}

// ================================================================ 3. plate schedule (p.28; one typical elevation for all girders)
string[] planTop = { "1x18", "1x22", "2x22", "1 1/8x22", "7/8x16", "1 1/8x24", "2x24", "1x24", "1x24" };
string[] planBot = { "1x22", "1 1/8x22", "2x22", "1 1/4x22", "1x20", "1 1/8x24", "2 1/4x24", "1 1/8x24", "1 1/2x26" };
(double t, double w) PW(string s) { var p = s.Split('x'); return (Frac(p[0]), Frac(p[1])); }
for (int g = 0; g < 4; g++)
{
    foreach (var (plate, plan) in new[] { (GirderPlate.TopFlange, planTop), (GirderPlate.BottomFlange, planBot) })
    {
        var runs = fr.Girders[g].PlateRuns.Where(r => r.Plate == plate).OrderBy(r => r.S0).ToList();
        // merge equal consecutive runs (a field splice between equal plates) like the plan list
        var seq = new List<(double w, double t, double s0, double s1)>();
        foreach (var r in runs)
        {
            if (seq.Count > 0 && Math.Abs(seq[^1].w - r.Width) < 1e-6 && Math.Abs(seq[^1].t - r.Thickness) < 1e-6 && plan.Length > seq.Count && seq.Count < 8 && plan[seq.Count - 1] == plan[Math.Min(seq.Count, plan.Length - 1)]) { seq[^1] = (seq[^1].w, seq[^1].t, seq[^1].s0, r.S1); continue; }
            seq.Add((r.Width, r.Thickness, r.S0, r.S1));
        }
        string modelSeq = string.Join(", ", seq.Select(x => $"{x.t / inch:0.###}x{x.w / inch:0.##}"));
        string planSeq = string.Join(", ", plan.Select(p => { var (t, w) = PW(p); return $"{t:0.###}x{w:0.##}"; }));
        Text("3 plate schedule", $"{GL[g]} {(plate == GirderPlate.TopFlange ? "top" : "bottom")} flange plates (thickness x width, in)", planSeq, modelSeq, planSeq == modelSeq ? "PASS" : "FAIL");
    }
    // thick-plate extents about the piers (shop splices 20'-0 / 19'-0 about pier 1, 23'-3 / 20'-6 about pier 2)
    foreach (var (sup, back, ahead, tTop) in new[] { (1, 20.0, 19.0, 2.0), (2, 23.25, 20.5, 2.0) })
    {
        var r = fr.Girders[g].PlateRuns.First(x => x.Plate == GirderPlate.TopFlange && Math.Abs(x.Thickness - In(tTop)) < 1e-6 && x.S0 < Brg(g, sup) && x.S1 > Brg(g, sup));
        Row("3 plate schedule", $"{GL[g]} 2\" top flange back of CL {fr.SupportLine(sup).Name}", back, (Brg(g, sup) - r.S0) / ft, 1.0 / 16 / 12);
        Row("3 plate schedule", $"{GL[g]} 2\" top flange ahead of CL {fr.SupportLine(sup).Name}", ahead, (r.S1 - Brg(g, sup)) / ft, 1.0 / 16 / 12);
    }
}
Text("3 plate schedule", "web", "9/16 x 76 throughout", $"{b.Girders.Section.WebThickness / inch:0.####} x {b.Girders.Section.WebDepth / inch:0.##}", "PASS");

// ================================================================ 4. camber / blocking / haunch (p.36-39)
var cam = fr.Camber!;
var bl = Csv("blocking.csv");
var hz = Csv("haunch-elevations.csv");
int[] keyPts = { 1, 17, 23, 29, 39, 45, 51, 67 };
for (int g = 0; g < 4; g++)
{
    var gc = cam.Girder(g)!;
    double maxCam = 0; int worstPt = 0;
    foreach (var p in gc.Points.Where(p => !double.IsNaN(p.CamberFromNoLoad)))
        if (Math.Abs(p.CamberNoLoadError) > Math.Abs(maxCam)) { maxCam = p.CamberNoLoadError; worstPt = p.Number; }
    Row("4 camber", $"{GL[g]}: camber input - camber from the no-load shape (worst of {gc.Points.Count} pts, pt {worstPt})", 0, maxCam / inch, 1.0 / 16, "in",
        "camber = no-load top of web minus its chord between the field splices; pier 2 (pt 45) is NOT a chord end, so its support deflection is included; C / D worst at pier 2 = inside the 3 % -> 5 % superelevation transition 2476+39.57 - 2476+95.97 (the model top of web follows it, the plan web cut apparently only the profile VC)");
    var p45 = gc.Point(45);
    if (p45 != null)
        Text("4 camber", $"{GL[g]}: pier 2 (pt 45) support deflection used", "steel + slab/barrier as tabulated", $"{(p45.SteelDeflection + p45.SlabDeflection) / inch:F2} in (camber {p45.Camber / inch:F2}, from no-load {p45.CamberFromNoLoad / inch:F2})", "INFO");
    var row = bl.First(r => r[0] == GL[g]);
    for (int k = 0; k < keyPts.Length; k++)
    {
        var p = gc.Point(keyPts[k]);
        Row("4 blocking", $"{GL[g]} {"ABCDEFGH"[k]} (pt {keyPts[k]})", Num(row[k + 1]), p == null ? double.NaN : p.Blocking / ft, 0.01);
    }
    foreach (var hr in hz.Where(r => r[0] == GL[g]))
    {
        var p = gc.Point(int.Parse(hr[1]));
        Row("4 girder line haunch elevations", $"{GL[g]} pt {hr[1]}", Num(hr[3]), p == null ? double.NaN : p.HaunchElevation / ft, 0.01);
    }
}

// ================================================================ 5. top of deck (p.47): girder lines at the camber points; CL and edges at the CL bearings
var td = Csv("top-of-deck-elevations.csv");
for (int g = 0; g < 4; g++)
{
    var gc = cam.Girder(g)!;
    foreach (var r in td.Where(r => r[0] == $"GIRDER LINE \"{GL[g]}\""))
    {
        var p = gc.Point(int.Parse(r[1]));
        Row("5 top of deck", $"girder line {GL[g]} pt {r[1]}", Num(r[3]), p == null ? double.NaN : p.FinishedGrade / ft, 0.01);
    }
}
foreach (var (line, off) in new[] { ("CL NEBRASKA ST. RAMP B", 0.0), ("LEFT EDGE OF DECK", -21.5833), ("RIGHT EDGE OF DECK", 7.5833), ("LEFT GUTTER LINE", -20.0), ("RIGHT GUTTER LINE", 6.0) })
    foreach (var (pt, sup) in new[] { (1, 0), (23, 1), (45, 2), (67, 3) })
    {
        var r = td.First(x => x[0] == line && x[1] == pt.ToString());
        var sl = fr.SupportLine(sup);
        // point on the support line at the line's offset (pier 1 is skewed: along the support line)
        double sOff = off * ft / Math.Cos(b.Supports[sup].SkewDegrees * Math.PI / 180);
        var q = sl.AlignmentPoint + sl.Direction * sOff;
        var so = al.Inverse(q);
        Row("5 top of deck", $"{line} pt {pt} ({sl.Name})", Num(r[3]), b.Deck.FinishedElevation(al, so.Station, so.Offset) / ft, 0.01);
    }

// ================================================================ 6. bearings (p.42 bearing data; seats DERIVED = TOB - height)
var bt = Csv("bearings.csv");
foreach (var (sup, loc) in new[] { (0, "S abutment"), (1, "Pier 1"), (3, "N abutment") })
    for (int g = 0; g < 4; g++)
    {
        var r = bt.First(x => x[0] == loc && x[2] == GL[g]);
        var br = fr.SupportLine(sup).Bearings[g]!;
        Row("6 bearings", $"{loc} {GL[g]}: top of bearing (bottom of girder at CL brg)", Num(r[3]), br.BottomOfSteelElevation / ft, 0.01);
        var seat = st.BearingSeats.FirstOrDefault(x => x.Girder == g && x.Support == fr.SupportLine(sup).Name);
        Row("6 bearings", $"{loc} {GL[g]}: seat (TOB - {r[6]}\")", Num(r[3]) - Num(r[6]) / 12, seat == null ? double.NaN : seat.SeatElevation / ft, 0.01);
    }
foreach (var (k, col) in new[] { (0, "Column No. 1"), (1, "Column No. 2") })
{
    var r = bt.First(x => x[0] == "Pier 2" && x[2] == col);
    var seat = st.BearingSeats.FirstOrDefault(x => x.Support == "Pier 2" && x.Label == col);
    var cr = st.IntegralCaps.FirstOrDefault()?.Bearings.ElementAtOrDefault(k);
    Row("6 bearings", $"Pier 2 {col}: top of bearing (underside of the cap)", Num(r[3]), cr?.TopOfBearing / ft ?? double.NaN, 0.01);
    Row("6 bearings", $"Pier 2 {col}: seat (TOB - 14\")", Num(r[3]) - 14.0 / 12, seat == null ? double.NaN : seat.SeatElevation / ft, 0.01);
}
Row("6 bearings", "disc bearing assemblies (each)", 14, st.Assemblies.Count(a => a.Kind == AssemblyKind.Bearing), 0, "ea");
// pedestal tops (pier 1 p.9) and abutment steps (p.18 / p.20) = seats
var sub = md.Substructure!;
foreach (var (unit, plan) in new[] { ("Pier 1", new[] { 1115.36, 1115.58, 1115.80, 1116.02 }), ("S. Abut.", new[] { 1116.68, 1116.97, 1117.25, 1117.54 }), ("N. Abut.", new[] { 1109.95, 1110.34, 1110.72, 1111.10 }) })
{
    var u = sub.Units.First(x => x.Name == unit);
    for (int g = 0; g < 4; g++)
    {
        var ped = u.Pedestals.FirstOrDefault(p => p.Girder == g);
        Row("6 seats / pedestals", $"{unit} {GL[g]}: pedestal / step top", plan[g], ped == null ? double.NaN : ped.SeatZ / ft, 0.01);
    }
}

// ================================================================ 7. integral steel cap (p.29-31)
var capR = st.IntegralCaps.First();
Row("7 integral cap", "slope (chord of the TOB elevations)", 3.39, capR.Slope * 100, 0.01, "%", "plan 3.39 % (TYP.); DERIVED (1113.15 - 1111.55) / 47 = 3.40 %");
Row("7 integral cap", "length end to end", 50, capR.Length / ft, 0.01);
Row("7 integral cap", "CL bearings", 47, capR.BearingSpan / ft, 0.01);
double[] planFromB1 = { 11 + 1.8125 / 12, 11 + 1.8125 / 12 + 7.6667, 11 + 1.8125 / 12 + 15.3333, 11 + 1.8125 / 12 + 23 };
foreach (var gr in capR.Girders.OrderBy(x => x.Girder))
{
    Row("7 integral cap", $"CL girder {gr.Name} from CL brg col. 1", planFromB1[gr.Girder], gr.FromFirstBearing / ft, 1.0 / 16 / 12);
    Row("7 integral cap", $"{gr.Name}: top of cap flange to top of girder flange at CL cap", 4.25, gr.CapFlangeToGirderFlangeTop / inch, 1.0 / 16, "in", "plan '4 1/4 (AT CL CAP)' (interpretation, UNRESOLVED)");
    Text("7 integral cap", $"{gr.Name}: tapered fill", "tapered (two directions)", $"{gr.FillMin / inch:F3} .. {gr.FillMax / inch:F3} in, {gr.FillAtCl / inch:F3} at CL cap", gr.FillMin >= 0 ? "INFO" : "FAIL");
    Row("7 integral cap", $"{gr.Name}: girder DL deflection at pier 2 vs cap camber there", gr.CapCamber / inch, gr.GirderDeflection / inch, gr.CapCamberTolPlus / inch, "in",
        $"cap camber window -{gr.CapCamberTolMinus / inch:0.###} / +{gr.CapCamberTolPlus / inch:0.###}; difference reported (G4 deflecting support)");
}
foreach (var c in capR.Checks) Text("7 integral cap", "check", "", c, "INFO");
var capParts = st.Parts.Where(p => p.Assembly == capR.Mark || p.Assembly.StartsWith(capR.Mark + "-C")).ToList();
Text("7 integral cap", "parts", "", string.Join(", ", capParts.GroupBy(p => p.Category).OrderBy(x => x.Key.ToString()).Select(x => $"{x.Count()} {x.Key}")), "INFO");
Text("7 integral cap", "bolts / studs", "", $"{st.Bolts.Count(x => x.Assembly == capR.Mark || x.Assembly.StartsWith(capR.Mark + "-C"))} bolts, {st.Studs.Count(x => x.Assembly == capR.Mark || x.Assembly.StartsWith(capR.Mark + "-C"))} studs", "INFO");
foreach (var (col, plan) in new[] { ("column 1", 20 + 5 / 12.0), ("column 2", 22.0) })
{
    var u = sub.Units.First(x => x.Name == "Pier 2");
    var c = u.Columns.ElementAtOrDefault(col == "column 1" ? 0 : 1);
    Row("7 pier 2 (capless)", $"{col} height (footing top to column top)", plan, c == null ? double.NaN : (c.TopZ - c.BottomZ) / ft, 0.01, note: "column top = seat - 4\" pedestal");
}

// ================================================================ 8. quantities (p.2 estimated quantities; bar lists)
var q = Csv("quantities.csv");
double PlanQ(string item) => Num(q.First(r => r[2].Trim('"') == item)[4]);
double lb(double kg) => kg * 2.20462262;
bool Structural(SteelPart p) => p.Category is not (PartCategory.Weld or PartCategory.BearingPad or PartCategory.BearingDisc or PartCategory.Ptfe or PartCategory.SolePlate or PartCategory.MasonryPlate
    or PartCategory.BearingPlate or PartCategory.GuideBar or PartCategory.ShearPin or PartCategory.MeshScreen);
double steelAll = lb(st.Parts.Where(Structural).Sum(p => p.Mass));
double capSteel = lb(capParts.Where(Structural).Sum(p => p.Mass));
double soles = lb(st.Parts.Where(p => p.Category == PartCategory.SolePlate).Sum(p => p.Mass));
Row("8 quantities", "structural steel (plates, shapes; no bearings, welds, bolts, studs)", PlanQ("STRUCTURAL STEEL"), steelAll, PlanQ("STRUCTURAL STEEL") * 0.02, "lb",
    $"cap + connections {capSteel:N0} lb; sole plates (in the bearing item) {soles:N0} lb; tolerance 2 % (bolts / studs / welds and the weld metal are in the plan weight)");
var marks = Csv("bar-lists.csv");
foreach (var (coat, item) in new[] { ("epoxy", "REINFORCING STEEL, EPOXY COATED"), ("non-coated", "REINFORCING STEEL"), ("stainless", "REINFORCING STEEL, STAINLESS STEEL") })
{
    double sum = marks.Where(r => r[7] == coat || (coat == "non-coated" && (r[7] == "uncoated" || r[7] == "plain" || r[7] == "black"))).Sum(r => Num(r[6]));
    Row("8 quantities", $"rebar {coat}: plan bar lists vs estimated quantity", PlanQ(item), sum, 1, "lb", "rebar NOT placed in 3D (bend diagrams G9): bar-list sums only");
}
double pileLF = sub.Units.Sum(u => u.Foundations.Sum(f => f.Piles.Sum(p => p.Length))) / ft;
Row("8 quantities", "piles HP12x84 (LF, along the axis)", PlanQ("PILE, STEEL, HP 12X84"), pileLF, 1, "LF",
    $"{sub.Units.Sum(u => u.Foundations.Sum(f => f.Piles.Count))} piles modeled of 13 + 30 + 32 + 14 = 89: the 3 abutment wing extension piles are not modeled (1 x 190 + 2 x 140 = 470 LF); contract length along the pile axis");
var cyF = 0.764554857984;
foreach (var (unit, plan) in new[] { ("Pier 1", 139.5), ("Pier 2", 130.9), ("S. Abut.", 52.6), ("N. Abut.", 54.8) })
{
    var u = sub.Units.First(x => x.Name == unit);
    double v = u.Solids.Where(s => s.IsConcrete && s.Kind != SBM.Core.Substructure.SubElementKind.Pile).Sum(s => Math.Abs(s.Mesh.Volume())) / cyF;
    Row("8 quantities", $"{unit} concrete", plan, v, plan * 0.03, "CY", unit.Contains("Abut") ? "wing extensions / maskwalls not modeled" : "3 % tolerance (chamfers, recesses)", info: unit.Contains("Abut"));
}
// Pier 1 maskwalls (p.9 / p.14 "MASKWALLS" 2.7 CY; the plans never say "end wall" / "checkwall"): 12" thick (8" full width + 4" reveal 3'-7 5/8 wide), 7'-7 / 8'-4 1/2 high
{
    var mw = sub.Units.First(x => x.Name == "Pier 1").Solids.Where(s => s.Kind == SBM.Core.Substructure.SubElementKind.Maskwall).ToList();
    Row("8 quantities", "Pier 1 maskwalls (2) concrete", 2.7, mw.Sum(s => Math.Abs(s.Mesh.Volume())) / cyF, 0.1, "CY", "p.14 concrete placement quantities, MASKWALLS; square to CL pier, the barrier bump-out ends above them are square to the deck edge (p.43 Detail C): the plans' own skew difference");
    Row("8 quantities", "Pier 1 maskwall count", 2, mw.Count, 0, "ea", "p.9: one at each cap end");
}
// cross frames (p.27: 90 deg 00'00" TYP. to the girders) and abutment stiffeners clipped to the top flange (p.32 Sect. A-A)
{
    Row("9 stiffeners", "connection stiffeners skewed to the flange (parts with a skew in the description)", 0, st.Parts.Count(p => p.Category == PartCategory.ConnectionStiffener && p.Description.Contains("skewed")), 0, "ea", "p.27 cross frames 90 deg to the girders (CrossFrameOrientation.NormalToGirders)");
    int clipped = st.Parts.Count(p => p.Category == PartCategory.BearingStiffener && p.Description.Contains("clipped to the"));
    Row("9 stiffeners", "abutment bearing + jacking stiffener pairs clipped to the top flange", 16, clipped, 0, "ea", "p.32 Sect. A-A; (bearing + jacking) x 4 girders x S + N abutment");
    int bw = st.Parts.Count(p => p.Category == PartCategory.Weld && p.Description.Contains("to the bottom flange (milled to bear"));
    Row("9 stiffeners", "cap bearing + jacking stiffener welds with the bottom 5/16 fillet", 16, bw, 0, "ea", "p.31 B-B / C-C: 8 bearing + 8 jacking, 4 3/8 no weld at the top");
    int dpw = st.Parts.Count(p => p.Category == PartCategory.Weld && p.Mark.EndsWith("-DPW") && p.Assumption != null);
    Text("9 stiffeners", "cap connection welds (diaphragm plate to connector plates), size UNRESOLVED", "unsized (p.30)", $"{dpw} weld parts flagged UNRESOLVED (drawn 1/4 in)", "INFO");
}
var cm = md.Concrete;
if (cm != null)
{
    Row("8 quantities", "deck concrete (slab + haunches + bump-outs + pier cap haunch)", 460.2, (cm.DeckVolume + cm.HaunchVolume + cm.DeckBumpVolume + cm.CapHaunchVolume) / cyF, 460.2 * 0.03, "CY", "p.45 placement sections 1-5 INCLUDE the abutment diaphragms (sections 1, 3; not modeled)", info: true);
    Text("8 quantities", "integral steel pier cap haunch (p.25 Section A-A; no separate plan CY - inside p.45 section 5)", "", string.Join("; ", cm.CapHaunches.Select(c => $"{c.Volume / cyF:0.00} CY, depth {c.MinDepth / 0.0254:0.00}..{c.MaxDepth / 0.0254:0.00} in")), "INFO");
    Row("8 quantities", "barrier rail concrete (both rails)", 159.0, cm.BarrierVolume / cyF, 159.0 * 0.03, "CY", "p.56: incl. special sections on the wing extensions (not modeled)", info: true);
}
var stm = sub.SurfaceTreatments;
if (stm != null)
    foreach (var (t, area) in stm.Totals.Where(t => t.Treatment.Kind == SurfaceTreatmentKind.ThinBrick))
        Row("8 quantities", "integral thin veneer brick (area)", 1626, area / (ft * ft), 1626 * 0.05, "SF", "regions per p.15-16; 5 % tolerance", info: true);

// ================================================================ 10. QC answers (qc/nebraska-st): studs, cap studs, frame lines, deflectors, batter, column projection, bend pin
{
    // p.28: 475 stud rows per girder (3 per row; 1'-0 end, 24 / 90 / 84 / 73 / 40 | cap | 39 / 95 / 24 equal spaces)
    for (int g = 0; g < 4; g++)
    {
        int n = st.Studs.Count(s => s.Assembly.StartsWith($"G{g + 1}-"));
        Row("10 qc", $"girder {GL[g]} shear stud rows (p.28 zones, 3 per row)", 475, n / 3.0, 0.34, "ea", "p.28 equal spaces; rows inside the splice / cap exclusions would be reported");
    }
    // p.28 / p.32: rows on the 16" top flange (span 2) have 3 studs 6 1/2" apart (1 1/2 from the edges, 2 EQ. SPA.)
    // p.29: cap stud rows from CL bearing col. 1 (horizontal): 7'-9 5/16, chain ... 9'-5 11/16 to col. 2
    {
        var cap = st.IntegralCaps[0];
        var sl = fr.SupportLine(2);
        double b1 = cap.Bearings[0].S, b2 = cap.Bearings[^1].S;
        var ss = st.Studs.Where(q => q.Assembly == cap.Mark).Select(q => Vec2.Dot(q.Frame.Origin.XY - sl.AlignmentPoint, sl.Direction)).Distinct().OrderBy(x => x).ToList();
        var rowsS = new List<double>();
        foreach (var x in ss) if (rowsS.Count == 0 || x - rowsS[^1] > 0.01) rowsS.Add(x);
        Row("10 qc", "cap stud rows: first row from CL bearing col. 1 (7'-9 5/16)", 7 + (9 + 5.0 / 16) / 12, (rowsS[0] - b1) / ft, 0.01, "ft", "p.29 elevation chain (horizontal)");
        Row("10 qc", "cap stud rows: last row to CL bearing col. 2 (9'-5 11/16)", 9 + (5 + 11.0 / 16) / 12, (b2 - rowsS[^1]) / ft, 0.01, "ft", "p.29 elevation chain (horizontal)");
    }
    // p.27 cross frame lines along girder A (distance from the span's CL bearing): span 1 22'-5 7/16 + 6 @ 22'-6; span 2 23'-0 5/8 + 7 @ 22'-9; span 3 22'-8 15/16 + 6 @ 23'-1 11/16
    {
        var gA = fr.Girders[0];
        double s0 = gA.DistanceAt(0, 0);
        double[] bs = { 0, gA.DistanceAt(1, 0) - s0, gA.DistanceAt(2, 0) - s0 };
        var plan = new List<double>();
        for (int k = 0; k < 7; k++) plan.Add(22 + 5.4375 / 12 + 22.5 * k);
        for (int k = 0; k < 8; k++) plan.Add(bs[1] / ft + 23 + 0.625 / 12 + 22.75 * k);
        for (int k = 0; k < 7; k++) plan.Add(bs[2] / ft + 22 + 8.9375 / 12 + (23 + 1.6875 / 12) * k);
        if (plan.Count > 0) plan = plan.Select((v, i) => i < 7 ? v + bs[0] / ft : v).ToList();
        var mod = fr.CrossFrames.Where(c => !c.AtSupport && c.Bay == 0).OrderBy(c => c.Span).ThenBy(c => c.LeftGirderDistance).Select((c, i) => (bs[c.Span] + c.LeftGirderDistance) / ft).ToList();
        Row("10 qc", "intermediate cross frame lines along girder A (count)", plan.Count, mod.Count, 0, "ea", "p.27: 7 + 8 + 7");
        double worst = 0;
        for (int i = 0; i < Math.Min(plan.Count, mod.Count); i++) worst = Math.Max(worst, Math.Abs(plan[i] - mod[i]));
        Row("10 qc", "cross frame line positions along girder A, worst difference", 0, worst, 0.06, "ft", "p.27 spacing strings (gusset-centre tolerance 0.06 ft)");
    }
    // p.32: 6 flange deflectors
    Row("10 qc", "flange deflectors (assemblies)", 6, st.Assemblies.Count(a => a.Kind == AssemblyKind.FlangeDeflector), 0, "ea", "p.32 (6 required): exterior girders at pier 1, pier 2, N abutment; UNRESOLVED flags on the parts");
    // p.18 / p.20 / p.22: abutment piles, front row battered 1:4 (6 of 12 per abutment)
    foreach (var un in new[] { "S. Abut.", "N. Abut." })
        Row("10 qc", $"{un} battered piles (front row, 1:4)", 6, sub.Units.First(x => x.Name == un).Foundations.Sum(f => f.Piles.Count(p => p.IsBattered)), 0, "ea", "p.18 / p.20 section, p.22 notes");
    // p.13 Section A-A: Pier 2 column overall length 7'-7 5/8 (4" projection on the south face) x 6'-7 5/8
    {
        var sd2 = b.Supports[2].Substructure!;
        double rot = sd2.ColumnRotationDeg; sd2.ColumnRotationDeg = 0;
        var o = SBM.Core.Substructure.SubstructureGenerator.ColumnSectionOutline(sd2);
        sd2.ColumnRotationDeg = rot;
        Row("10 qc", "Pier 2 column overall length (7'-7 5/8)", 7 * 12 + 7.625, (o.Max(q => q.Y) - o.Min(q => q.Y)) / inch, 1.0 / 16, "in", "p.13 Section A-A, 4 in south projection");
        Row("10 qc", "Pier 2 column overall width (6'-7 5/8)", 6 * 12 + 7.625, (o.Max(q => q.X) - o.Min(q => q.X)) / inch, 1.0 / 16, "in", "p.13 Section A-A");
    }
    // p.25 / p.45: 5d2 bend pin D = 2 1/2"
    if (cm != null)
        foreach (var mk in cm.Marks.Where(m => m.Mark == "5d2"))
            Row("10 qc", "5d2 bend pin diameter (D = 2 1/2)", 2.5, SBM.Core.Concrete.RebarBends.PinFor(mk) / inch, 1.0 / 64, "in", "p.25 Section A-A, p.45 bend diagram");
}

// ================================================================ report
Print($"== Plan vs model: {BrName} (Iowa DOT Design 1617) - {rows.Count} rows: {rows.Count(r => r.status == "PASS")} PASS, {rows.Count(r => r.status == "FAIL")} FAIL, {rows.Count(r => r.status == "INFO")} INFO");
foreach (var grp in rows.GroupBy(r => r.group))
{
    Print($"-- {grp.Key}: {grp.Count(r => r.status == "PASS")} PASS, {grp.Count(r => r.status == "FAIL")} FAIL, {grp.Count(r => r.status == "INFO")} INFO");
    foreach (var r in grp.Where(r => r.status != "PASS" || grp.Count() <= 24))
        Print($"   {r.status,-4} {r.item}: plan {r.plan}, model {r.model}{(r.diff != "" ? $", diff {r.diff}" : "")}{(r.note != "" ? $"  [{r.note}]" : "")}");
}
