// Northaven Trail tied arch (span 10) - plan vs model checks. Run on a model built by arch-nht.csx:
//   sbm run samples\plans\check-arch-nht.csx "samples\plans\Northaven Trail.sbm"
// Reports (never adjusts): hanger lengths vs p.147 (+-1/16"), rib radius / rise / 5 deg plane / splice points, tie points vs p.147 T points,
// floorbeam count and positions, bearing chain (B1-B3), deck area, steel weights vs the plan quantity tables, hanger strand and sockets vs the
// WireCo structural cable catalog (Table 1 p.10, Table 6 p.20).
using System.Globalization;
using System.IO;
using SBM.Core.Arch;
using SBM.Core.Geometry;
using SBM.Core.Steel;

var br = Doc.Project.Bridges.FirstOrDefault(b => b.Arch != null) ?? throw new Exception("no arch bridge in this model (run arch-nht.csx first)");
var am = Doc.Steel(br.Name).Arch!;
const double Tol = 0.0254 / 16;
double ToFt(double m) => m / 0.3048;
double ToIn(double m) => m / 0.0254;
int bad = 0;
void Row(string item, string plan, string model, bool ok, string note = "") { if (!ok) bad++; Print($"{(ok ? "OK  " : "DIFF")} {item}: plan {plan} | model {model}{(note == "" ? "" : "  - " + note)}"); }
string dataDir = FindData();
string FindData()
{
    for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "nht-0047-07-228");
        if (Directory.Exists(p)) return p;
    }
    throw new Exception("docs/plan-data/nht-0047-07-228 not found");
}
List<Dictionary<string, string>> Csv(string name)
{
    var lines = File.ReadAllLines(Path.Combine(dataDir, name));
    var hdr = lines[0].Split(',');
    return lines.Skip(1).Where(l => l.Trim().Length > 0).Select(l => { var f = l.Split(','); return hdr.Select((h, i) => (h, v: i < f.Length ? f[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

Print($"== {br.Name}: {am.Steel.Parts.Count} parts, {am.Hangers.Count} hangers, {am.Floorbeams.Count} floorbeams, {am.Bearings.Count} bearings");
foreach (var c in am.Checks) Row(c.Item, c.Plan, c.Model, c.Ok, c.Note);

// ---- hangers: pin-to-pin TC-HC lengths (p.147) - recomputed here from the generated world points
Print("== hangers (p.147 cable lengths, +-1/16\")");
double maxH = 0;
foreach (var h in am.Hangers)
{
    double d = (h.RibAnchorWorld - h.TieAnchorWorld).Length - h.PlanLength;
    maxH = Math.Max(maxH, Math.Abs(d));
    if (Math.Abs(d) > Tol) Row($"hanger {h.Name}", $"{ToFt(h.PlanLength):F3}'", $"{ToFt(h.PlanLength + d):F3}'", false);
}
Row($"{am.Hangers.Count} hangers, max |model - plan|", "64 within 1/16\"", $"{ToIn(maxH):F4}\"", am.Hangers.Count == 64 && maxH <= Tol);

// ---- ribs: radius, rise, inclination, points on the circle / plane
Print("== ribs (p.119: R 126'-0, rise 50'-0, 5 deg inward; p.120 splices)");
foreach (var r in am.Ribs)
{
    Row($"{r.Name} radius", "126.000'", $"{ToFt(r.Radius):F3}'", Math.Abs(r.Radius - 126 * 0.3048) <= Tol);
    Row($"{r.Name} rise", "50.000'", $"{ToFt(r.Rise):F4}'", Math.Abs(r.Rise - 50 * 0.3048) <= Tol);
    double inc = Math.Acos(Math.Abs(r.Up.Z)) * 180 / Math.PI;
    Row($"{r.Name} inclination of the arch plane", "5.000 deg", $"{inc:F4} deg", Math.Abs(inc - 5) < 1e-6);
    double maxDev = 0;
    for (int i = 0; i <= 50; i++)
    {
        var p = r.PointAt(r.ThetaStart + (r.ThetaEnd - r.ThetaStart) * i / 50);
        maxDev = Math.Max(maxDev, Math.Abs((p - r.Center).Length - r.Radius));
    }
    Row($"{r.Name} WP points on the circle (51 samples)", "0", $"{ToIn(maxDev):F5}\"", maxDev <= Tol);
    Row($"{r.Name} arc length T0-T34", "232.68' (SUMMARY, derived)", $"{ToFt(r.ArcLength):F2}'", Math.Abs(ToFt(r.ArcLength) - 232.68) < 0.01);
}
// Splice points and bracing U points vs the rib circle (U6 reported).
foreach (var p in br.Arch!.Points.Where(p => p.Name.StartsWith("RS") || (p.Name.StartsWith("U") && !p.Name.StartsWith("UL"))))
{
    var rib = am.Ribs.First(r => (p.Y < 0) == (r.Center.Y < 0));
    double dr = (p.Vec - rib.Center).Length;
    var n = Vec3.Cross(rib.ChordDir, rib.Up).Normalized();
    double off = Vec3.Dot(p.Vec - rib.Center, n);
    if (p.Name.StartsWith("RS")) Row($"splice {p.Name} on the rib WP circle / plane", "R 126.000' / 0", $"R {ToFt(dr):F3}' / {ToIn(off):F2}\"", Math.Abs(dr - rib.Radius) <= 0.003 && Math.Abs(off) <= 0.003, "p.120 coordinates to 0.001'");
    else if (p.Name is "U1" or "U6" or "U11") Row($"bracing point {p.Name} vs rib WP (U6)", "on the rib?", $"R {ToFt(dr):F3}', {ToFt(off):F3}' off the plane", true, "reported only (UNRESOLVED U6)");
}

// ---- ties: generated panel points vs p.147 T points (south) and their rotation (north)
Print("== ties (p.143 chain vs p.147 T points)");
var tpts = Csv("cable-hanger-coordinates.csv").ToDictionary(r => r["T"], r => new Vec3(D(r["X1_ft"]) * 0.3048, D(r["Y1_ft"]) * 0.3048, 0));
var south = am.Ties.First(t => t.Name == "South tie").PanelPoints.ToDictionary(p => p.Name, p => p.Local);
var north = am.Ties.First(t => t.Name == "North tie").PanelPoints.ToDictionary(p => p.Name, p => p.Local);
double maxT = 0; string worst = "";
foreach (var (n, p) in tpts)
{
    double d1 = (south[n] - p).Length, d2 = (north[n + "'"] + p).Length;
    if (Math.Max(d1, d2) > maxT) { maxT = Math.Max(d1, d2); worst = n; }
}
Row("tie panel points T2-T33 (both ties) vs p.147", "within 1/16\"", $"max {ToIn(maxT):F3}\" at {worst}", maxT <= Tol);
foreach (var t in am.Ties) Row($"{t.Name} length along the chain", "201.676' (1'-7 15/16 + 93'-4 + 98'-4 + 8'-4 3/16)", $"{ToFt(t.Length):F3}'", Math.Abs(ToFt(t.Length) - 201.6771) < 0.001);

// ---- floorbeams
Print("== floorbeams (p.143, p.145)");
Row("floorbeam count", "33 (FB1-FB33)", am.Floorbeams.Count.ToString(), am.Floorbeams.Count == 33);
double minL = am.Floorbeams.Min(f => f.Length), maxL = am.Floorbeams.Max(f => f.Length);
Row("floorbeam CL tie to CL tie", "18'-4 (18.333')", $"{ToFt(minL):F4}' - {ToFt(maxL):F4}'", Math.Abs(minL - 18.3333 * 0.3048) <= Tol && Math.Abs(maxL - 18.3333 * 0.3048) <= Tol);
foreach (var f in am.Floorbeams.Where((f, i) => i % 8 == 0))
    Print($"     {f.Name}: crown {ToFt(f.CrownFromA):F3}' from the south tie (CL NHT crossing)");
// Panel lengths: south 16 @ 5'-10 then 16 @ 6'-1 3/4 (p.143).
var sp = Enumerable.Range(1, 33).Select(k => south[$"T{k}"]).ToList();
var spans = sp.Zip(sp.Skip(1), (a, b) => (b - a).Length).ToList();
Row("south tie chords FB1-FB17 (arc 5'-10 on R 342'-4)", "5.833' arc", $"{ToFt(spans.Take(16).Min()):F4}'-{ToFt(spans.Take(16).Max()):F4}' chord", Math.Abs(spans.Take(16).Average() - 5.8333 * 0.3048) < 0.001);

// ---- bearings
Print("== bearings (p.102 pedestal tops, p.107 stack 11 3/4\", p.119 WP chain)");
foreach (var b in am.Bearings)
    Print($"     {b.Name} @ {b.Support}: pedestal {ToFt(b.SeatElevation):F3}', top of stack {ToFt(b.TopOfStack):F3}', steel underside (WP chain) {ToFt(b.SteelUnderside):F3}', gap {ToIn(b.SteelUnderside - b.TopOfStack):F2}\"");

// ---- deck
Print("== deck (p.91 / p.151: 3,698 SF)");
double sf = am.SlabArea / 0.09290304;
Row("arch deck area (top surface, end web to end web)", "3,698 SF", $"{sf:N0} SF", Math.Abs(sf - 3698) / 3698 < 0.01, "pay limits of the plan quantity not stated");
double cy = (Math.Abs(am.Slab?.Volume() ?? 0) + Math.Abs(am.SlabEnds?.Volume() ?? 0)) / 0.764554857984;
Print($"     slab {cy:N1} CY (incl. concrete in the form flutes; steel inside the slab not deducted), curbs {Math.Abs(am.Curbs?.Volume() ?? 0) / 0.764554857984:N1} CY");

// ---- field splices (p.127 rib RS1-RS5: 68 bolts each = flanges 2 x 2 x 12 + web 2 x 10; p.144 tie: 42 each = flange 2 x 12 + web 2 x 9)
Print("== field splices (p.127, p.144)");
foreach (var (prefix, plan, what) in new[] { ("RIB-RS", 68, "rib splice"), ("TIE-TS", 42, "tie splice") })
{
    var sp = am.SpliceBolts.Where(k => k.Key.Contains(prefix)).ToList();
    Row($"{what}s: count / bolts per splice", prefix == "RIB-RS" ? "10 (RS1-RS5 x 2 ribs) / 68" : "8 (TS(1)-TS(4) x 2 ties, framing plan p.143; U5) / 42",
        $"{sp.Count} / {string.Join(",", sp.Select(k => k.Value).Distinct())}", sp.Count == (prefix == "RIB-RS" ? 10 : 8) && sp.All(k => k.Value == plan));
}
// p.120 TS1-TS3 table (arc mid-panel T9-T10, T17-T18, T25-T26) vs the framing-plan splices built (U5): reported.
{
    var st = br.Arch.Ties[0];
    string Between(double d) { var pp = st.PanelPoints.OrderBy(p => p.Distance).ToList(); var a = pp.LastOrDefault(p => p.Distance < d); var b = pp.FirstOrDefault(p => p.Distance > d); return $"{a?.Name ?? "T0"}-{b?.Name ?? "T34"}"; }
    foreach (var (n, x, y, a, b) in new[] { ("TS1", -44.892, -5.614, "T9", "T10"), ("TS2", 1.736, -9.498, "T17", "T18"), ("TS3", 50.757, -12.756, "T25", "T26") })
    {
        var mid = (south[a] + south[b]) * 0.5;
        Row($"p.120 table {n} ({x:F3}, {y:F3})", $"between {a}-{b} (arc mid panel; chord mid {ToFt(mid.X):F3}, {ToFt(mid.Y):F3})",
            $"south tie splices built (p.143): {string.Join(", ", st.SpliceAt.Select(s => s.Name + " " + Between(s.Distance)))}", false, "U5 - p.120 set reported, the p.143 / p.93 set (5 segments) is built");
    }
}

// ---- steel quantities vs the plan tables (groups as the plan quantity notes: members + splice plates + connection material + fasteners)
Print("== steel weights (model) vs plan quantity tables");
var asmOf = am.Steel.Assemblies.ToDictionary(a => a.Mark, a => a);
double boltLb(BoltInstance b) => Math.Abs(SteelModel.BoltMesh(b.Diameter > 0 ? new BoltGeometry(b.Diameter) : am.Steel.Bolt, b.Grip, b.Length, 8).Volume()) * SteelCatalog.Density * 2.20462262;
string GroupOfAsm(string mark)
{
    if (mark.Contains("-RIB")) return "rib";
    if (mark.Contains("-TIE")) return "tie";
    if (mark.StartsWith("FB")) return "floor";
    if (mark.StartsWith("UPPER-BRACING") || mark.StartsWith("BR")) return "bracing";
    if (mark.StartsWith("KNUCKLE")) return "knuckle";
    if (mark.StartsWith("FIN")) return "fin";
    return "other";
}
string GroupOf(SteelPart p) => p.Category switch
{
    PartCategory.ArchRib => "rib",
    PartCategory.KnucklePlate => "knuckle",
    PartCategory.Hanger or PartCategory.HangerSocket or PartCategory.FormDeck or PartCategory.BearingPad or PartCategory.SolePlate or PartCategory.MasonryPlate or PartCategory.ShearPin => "other",
    _ when p.Description.Contains("fin plate", StringComparison.OrdinalIgnoreCase) => "fin",
    _ => GroupOfAsm(p.Assembly),
};
var gParts = am.Steel.Parts.GroupBy(GroupOf).ToDictionary(g => g.Key, g => g.Sum(p => p.Mass) * 2.20462262);
var gBolts = am.Steel.Bolts.Where(b => b.Kind == BoltKind.Structural).GroupBy(b => GroupOfAsm(b.Assembly)).ToDictionary(g => g.Key, g => (n: g.Count(), lb: g.Sum(boltLb)));
var gStuds = am.Steel.Studs.GroupBy(s => GroupOfAsm(s.Assembly)).ToDictionary(g => g.Key, g => (n: g.Count(), lb: g.Sum(s => Math.Abs(am.Steel.StudMesh(s.Length, 8).Volume()) * SteelCatalog.Density * 2.20462262)));
double Grp(string g) => gParts.GetValueOrDefault(g) + (gBolts.TryGetValue(g, out var b) ? b.lb : 0) + (gStuds.TryGetValue(g, out var s) ? s.lb : 0);
void Q(string item, string g, double plan, double before, string note)
{
    double model = Grp(g);
    string extra = $"plates/members {gParts.GetValueOrDefault(g):N0}" + (gBolts.TryGetValue(g, out var b) ? $" + {b.n} bolts {b.lb:N0}" : "") + (gStuds.TryGetValue(g, out var s) ? $" + {s.n} studs {s.lb:N0}" : "");
    Row(item, $"{plan:N0} lb", $"{model:N0} lb ({(model - plan) / plan * 100:+0.0;-0.0}%; v1 {before:N0} lb, gap closed {(Math.Abs(before - plan) - Math.Abs(model - plan)):N0} lb) = {extra}", Math.Abs(model - plan) / plan < 0.10, note);
}
Q("arch rib Gr 50-T1 (p.127)", "rib", 59100, 59272, "plan incl. splice plates, stiffener plates, fasteners");
Q("tie girders Gr 50-F1 (p.143)", "tie", 33200, 30024, "plan incl. splice plates, shear studs, fasteners");
Q("floor system Gr 50-T1 (p.143)", "floor", 61000, 36449, "plan incl. floorbeams, END PLATES, studs, stiffeners, connection plates, fasteners");
Q("upper lateral bracing Gr 50 (p.128)", "bracing", 21600, 13038, "plan incl. gussets / connections");
Q("knuckles Gr 50 (p.123)", "knuckle", 9700, 21923, "end flange + end web + deck plate + stub envelopes; CONFLICT kept: the 2\" x 32\" end flanges (9,219 lb) + 1\" deck plates (9,060 lb) alone exceed 9,700");
if (gParts.ContainsKey("other")) Print($"     not in the Gr 50 / 50-T1 / 50-F1 groups (hangers, sockets, form deck, bearings): {gParts["other"]:N0} lb");
foreach (var g in am.Steel.Parts.Where(p => p.Category == PartCategory.KnucklePlate).GroupBy(p => p.Description))
    Print($"     knuckle: {g.Count()} x {g.Key} = {g.Sum(p => p.Mass) * 2.20462262:N0} lb");
if (gParts.ContainsKey("fin")) Q("cable fin plates Gr 50-F1 (p.148)", "fin", 3800, 0, "rib + tie fin plates");
else Row("cable fin plates Gr 50-F1 (p.148)", "3,800 lb", "not modeled", false, "GAP");
double strand = am.Steel.Parts.Where(p => p.Category == PartCategory.Hanger).Sum(p => p.Length) / 0.3048;
Print($"     hanger strand {strand:N1} ft visible between sockets x 2.10 lb/ft = {strand * 2.10:N0} lb; pin-to-pin total {am.Hangers.Sum(h => h.PlanLength) / 0.3048:N1} ft (pay item 'cable hangers' 1 LS)");
double form = am.Steel.Parts.Where(p => p.Category == PartCategory.FormDeck).Sum(p => p.Mass) * 2.20462262;
Print($"     steel deck form {form:N0} lb from the solid (12 ga 0.1046\") vs 7.4 psf x {sf:N0} SF = {7.4 * sf:N0} lb (p.152 weight)");

// ---- hanger strand / sockets vs the WireCo catalog (user-supplied)
Print("== hangers vs WireCo structural cable catalog (WWstrucCAT)");
Row("strand 1\" (Table 1 p.10)", "plan 1.0 in (24.4 mm printed - 1.0 in = 25.4 mm)", "catalog 1\": 2.10 lb/ft, 0.600 sq in, min. breaking 61.0 / 59.2 / 57.9 tons (Class A / B / C), E 24,000 ksi prestretched", true,
    "coating class not selected on the plans (galvanized only) -> breaking force UNRESOLVED between the classes; model weight 2.10 lb/ft");
var hs = br.Arch.HangerSystem;
Row("open strand socket at rib (p.148 vs Table 6 p.20, 15/16-1\")", $"pin {ToIn(hs.RibSocket!.PinDiameter):F3}\", A {ToIn(hs.RibSocket.A):F2}\", L {ToIn(hs.RibSocket.L):F2}\", W {ToIn(hs.RibSocket.W):F2}\"",
    "catalog: pin D 2 1/4\" x 4 3/4\", A 12\", J 5 5/8\", M 4\", N 2 3/8\", O 2 1/4\", P 4\"", false,
    "the plan sockets (35 mm pin, metric table) are another supplier's product; modeled at the plan dimensions, catalog not used for geometry");
Row("adjustable socket + turnbuckle at tie (p.148)", $"W {ToIn(hs.TieSocket!.W):F2}\"", "no adjustable socket / turnbuckle in the catalog tables (Tables 4-12 are fixed open / closed / bridge / anchor sockets)", false, "plan dimensions used");

Print($"== {bad} differences (reported, not adjusted)");
