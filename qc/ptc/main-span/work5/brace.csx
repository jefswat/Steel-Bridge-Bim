// QC batch 5 (read-only, NO save): regenerate only "PTC Arch truss 18-20" in memory and measure the bracing connections:
//   - every rivet whose shank lies in a bracing plate / edge angle (marks "-BG"): parts its shank passes (> 1/32 in.), class own (only BG parts) / conn / none
//   - bracing member ends (laterals, struts, sway, portal) with no connecting rivet within 4 ft of the end
//   - Info lines of the bracing generator; optional clash (main span, no bolts) summary by category pair
//   sbm run brace.csx "<PTC model>" --no-pipeline -- <outdir> [clash]
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Bridge;
using SBM.Core.Solids;
var inv = CultureInfo.InvariantCulture;
string outDir = Args[0];
bool doClash = Args.Length > 1 && Args[1] == "clash";
Directory.CreateDirectory(outDir);
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
var sw = System.Diagnostics.Stopwatch.StartNew();
Doc.Regenerate();
Print($"regen {sw.Elapsed.TotalSeconds:0.0}s");
var m = Doc.Model(BN);
var st = m.Steel!;
var tm = st.Truss!;
const double IN = 0.0254;
var parts = st.Parts;
var pb = parts.Select(p => p.Mesh is { IsEmpty: false } ? p.Mesh.Bounds() : default).ToArray();
string Key(SteelPart p) => p.Member ?? p.Mark;
bool IsBG(SteelPart p) => p.Mark.Contains("-BG");
// grid of part bounds (1 m)
var grid = new Dictionary<(int, int, int), List<int>>();
for (int i = 0; i < parts.Count; i++)
{
    if (parts[i].Mesh is not { IsEmpty: false }) continue;
    var b = pb[i];
    for (int x = (int)Math.Floor(b.Min.X); x <= (int)Math.Floor(b.Max.X); x++)
        for (int y = (int)Math.Floor(b.Min.Y); y <= (int)Math.Floor(b.Max.Y); y++)
            for (int z = (int)Math.Floor(b.Min.Z); z <= (int)Math.Floor(b.Max.Z); z++)
            {
                if (!grid.TryGetValue((x, y, z), out var l)) grid[(x, y, z)] = l = new();
                l.Add(i);
            }
}
IEnumerable<int> Near(Vec3 p)
{
    var c = (Floor(p.X), Floor(p.Y), Floor(p.Z));
    return grid.TryGetValue(c, out var l) ? l : Enumerable.Empty<int>();
    static int Floor(double v) => (int)Math.Floor(v);
}
bool Inside(Vec3 p, Box3 b, double e) => p.X >= b.Min.X - e && p.X <= b.Max.X + e && p.Y >= b.Min.Y - e && p.Y <= b.Max.Y + e && p.Z >= b.Min.Z - e && p.Z <= b.Max.Z + e;
// overlap length of segment [0, L] of the line o + d t with the solid (parity of the triangle hits)
double Overlap(Mesh mesh, Vec3 o, Vec3 d, double Lseg)
{
    var ts = new List<double>();
    var P = mesh.Positions; var I = mesh.Indices;
    for (int k = 0; k + 2 < I.Count; k += 3)
    {
        var a = P[I[k]]; var b = P[I[k + 1]]; var c = P[I[k + 2]];
        var e1 = b - a; var e2 = c - a; var h = Vec3.Cross(d, e2); double det = Vec3.Dot(e1, h);
        if (Math.Abs(det) < 1e-14) continue;
        double f = 1 / det; var s = o - a; double uu = f * Vec3.Dot(s, h); if (uu < -1e-9 || uu > 1 + 1e-9) continue;
        var q = Vec3.Cross(s, e1); double vv = f * Vec3.Dot(d, q); if (vv < -1e-9 || uu + vv > 1 + 1e-9) continue;
        ts.Add(f * Vec3.Dot(e2, q));
    }
    ts.Sort();
    var u2 = new List<double>();
    foreach (var t in ts) if (u2.Count == 0 || t - u2[^1] > 1e-6) u2.Add(t);
    double tot = 0;
    for (int k = 0; k + 1 < u2.Count; k += 2) tot += Math.Max(0, Math.Min(Lseg, u2[k + 1]) - Math.Max(0, u2[k]));
    return tot;
}
var bgIdx = Enumerable.Range(0, parts.Count).Where(i => IsBG(parts[i])).ToList();
Print($"bracing plates {bgIdx.Count(i => parts[i].Category == PartCategory.TrussConnectionPlate)}, edge angles {bgIdx.Count(i => parts[i].Category == PartCategory.TrussConnectionAngle)}");
var rivets = st.Bolts.Where(b => b.Kind == BoltKind.Rivet).ToList();
var cls = new Dictionary<string, int>();
var memberEnds = new Dictionary<string, int>();   // member key -> connecting rivets
var wR = new StreamWriter(Path.Combine(outDir, "brace-rivets.csv"));
wR.WriteLine("x,y,z,dia_in,grip_in,class,parts");
int nr = 0;
foreach (var r in rivets)
{
    var o = r.Frame.Origin; var d = r.Frame.Z; double g = r.Grip;
    var mid = o + d * (g / 2);
    var cand = Near(mid).Concat(Near(o)).Concat(Near(o + d * g)).Distinct().ToList();
    if (!cand.Any(i => IsBG(parts[i]) && Inside(mid, pb[i], IN))) continue;
    nr++;
    var hit = new List<string>();
    bool bg = false, other = false;
    foreach (var i in cand)
    {
        if (!Inside(mid, pb[i], g / 2 + IN)) continue;
        double ov = Overlap(parts[i].Mesh, o - d * IN, d, g + 2 * IN);
        if (ov < IN / 32) continue;
        hit.Add(Key(parts[i]));
        if (IsBG(parts[i])) bg = true; else other = true;
    }
    string c = hit.Count == 0 ? "none" : !other ? (hit.Distinct().Count() > 1 ? "bg-only-multi" : "own") : bg ? "conn" : "nobg";
    cls[c] = cls.GetValueOrDefault(c) + 1;
    if (c == "conn") foreach (var h in hit.Distinct()) memberEnds[h] = memberEnds.GetValueOrDefault(h) + 1;
    wR.WriteLine($"{o.X.ToString("F3", inv)},{o.Y.ToString("F3", inv)},{o.Z.ToString("F3", inv)},{(r.Diameter / IN):F3},{(g / IN):F3},{c},\"{string.Join("|", hit.Distinct())}\"");
}
wR.Close();
Print($"bracing rivets {nr}: " + string.Join(", ", cls.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")));
// bracing member ends: connecting rivets within 4 ft of each end (rivets classified conn whose shank passes the member)
var wE = new StreamWriter(Path.Combine(outDir, "brace-ends.csv"));
wE.WriteLine("member,role,end,rivets_conn,plates_touching");
int ends = 0, air = 0, airNoPlate = 0;
var conRiv = new List<(Vec3 p, HashSet<string> h)>();
foreach (var line in File.ReadLines(Path.Combine(outDir, "brace-rivets.csv")).Skip(1))
{
    var f = line.Split(',');
    if (f[5] != "conn") continue;
    var hs = new HashSet<string>(line[(line.IndexOf('"') + 1)..line.LastIndexOf('"')].Split('|'));
    conRiv.Add((new Vec3(double.Parse(f[0], inv), double.Parse(f[1], inv), double.Parse(f[2], inv)), hs));
}
var byRole = new Dictionary<string, (int ends, int air)>();
foreach (var mm in tm.Members)
{
    if (mm.Role is not (TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral or TrussMemberRole.Strut or TrussMemberRole.Sway or TrussMemberRole.Portal)) continue;
    var mparts = Enumerable.Range(0, parts.Count).Where(i => Key(parts[i]) == mm.Mark).ToList();
    if (mparts.Count == 0) continue;
    var ax = (mm.B - mm.A).Normalized();
    // cut ends from the member's parts
    double tmin = double.MaxValue, tmax = double.MinValue;
    foreach (var i in mparts) foreach (var v in parts[i].Mesh.Positions) { double t = Vec3.Dot(v - mm.A, ax); tmin = Math.Min(tmin, t); tmax = Math.Max(tmax, t); }
    foreach (var (end, t0) in new[] { ("A", tmin), ("B", tmax) })
    {
        var E = mm.A + ax * t0;
        int n = conRiv.Count(cr => cr.h.Contains(mm.Mark) && (cr.p - E).Length < 4 * 0.3048);
        // plates touching: BG part bounds within 2 in. of the end point region
        int pt = bgIdx.Count(i => Inside(E, pb[i], 2.5 * 0.3048));
        ends++; if (n == 0) { air++; if (pt == 0) airNoPlate++; }
        var k = mm.Role.ToString(); var cur = byRole.GetValueOrDefault(k); byRole[k] = (cur.ends + 1, cur.air + (n == 0 ? 1 : 0));
        wE.WriteLine($"\"{mm.Mark}\",{mm.Role},{end},{n},{pt}");
    }
}
wE.Close();
Print($"bracing member ends {ends}: no connecting rivet {air} (no bracing plate within 2.5 ft: {airNoPlate}); " + string.Join(", ", byRole.Select(k => $"{k.Key} {k.Value.air}/{k.Value.ends}")));
foreach (var dg in m.Steel!.Diagnostics.Select(x => x.Message).Where(s => s.Contains("bracing connection plates") || s.Contains("bracing plate at")).Distinct().Take(12))
    Print("  " + (dg.Length > 300 ? dg[..300] : dg));
{
    var eaConn = new Dictionary<string, int>();
    foreach (var line in File.ReadLines(Path.Combine(outDir, "brace-rivets.csv")).Skip(1))
    {
        var f = line.Split(','); if (f[5] != "conn" && f[5] != "bg-only-multi") continue;
        var hs = line[(line.IndexOf('"') + 1)..line.LastIndexOf('"')].Split('|');
        if (hs.Length < 2) continue;
        foreach (var h in hs) if (h.Contains("-EA")) eaConn[h] = eaConn.GetValueOrDefault(h) + 1;
    }
    var eas = parts.Where(p => p.Mark.Contains("-BG") && p.Mark.Contains("-EA")).Select(p => p.Mark).ToList();
    var lone = eas.Where(e => !eaConn.ContainsKey(e)).ToList();
    Print($"edge angles {eas.Count}: without a connecting rivet {lone.Count}; e.g. {string.Join(", ", lone.Take(12))}");
    var plates = parts.Where(p => p.Category == PartCategory.TrussConnectionPlate && p.Mark.Contains("-BG")).Select(p => p.Mark).ToList();
    var pc = new HashSet<string>();
    foreach (var line in File.ReadLines(Path.Combine(outDir, "brace-rivets.csv")).Skip(1)) { var f = line.Split(','); if (f[5] != "conn") continue; foreach (var h in line[(line.IndexOf('"') + 1)..line.LastIndexOf('"')].Split('|')) pc.Add(h); }
    var lp = plates.Where(x => !pc.Contains(x)).ToList();
    Print($"bracing plates {plates.Count}: without a rivet to a member {lp.Count}; e.g. {string.Join(", ", lp.Take(12))}");
}
if (doClash)
{
    var rep = Doc.Clashes(BN, new SBM.Core.Checks.ClashOptions { Bolts = false, Misc = false, Concrete = false });
    var cl = rep.SelectMany(x => x.Clashes).ToList();
    Print($"clash (no bolts) {cl.Count}");
    foreach (var g2 in cl.GroupBy(c => c.CategoryPair).OrderByDescending(g2 => g2.Count()).Take(15)) Print($"  {g2.Count()} {g2.Key} max {g2.Max(c => c.Depth) / IN:F2} in");
    using var wc = new StreamWriter(Path.Combine(outDir, "clash.csv"));
    wc.WriteLine("kindA,markA,kindB,markB,depth_in,x,y,z");
    foreach (var c in cl) wc.WriteLine($"{c.A.Kind},\"{c.A.Mark}\",{c.B.Kind},\"{c.B.Mark}\",{c.Depth / IN:F3},{c.At.X:F2},{c.At.Y:F2},{c.At.Z:F2}");
}
