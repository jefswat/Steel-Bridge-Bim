// RE-CHECK (2026-10-28) main span connections, read-only, NO save: regenerate only "PTC Arch truss 18-20" once, dump everything.
//   sbm run recheck.csx "<PTC model>" --no-pipeline -- <outdir>
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Bridge;
using SBM.Core.Solids;
var inv = CultureInfo.InvariantCulture;
string outDir = Args[0];
Directory.CreateDirectory(outDir);
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
var sw = System.Diagnostics.Stopwatch.StartNew();
Doc.Regenerate();
Print($"regen {sw.Elapsed.TotalSeconds:0.0}s");
var m = Doc.Model(BN);
var st = m.Steel!;
var tm = st.Truss!;
{ // ---- A: dump
var bd = Doc.Project.Bridges[0];
var al = Doc.Project.Alignments.First(a => a.Name == bd.AlignmentName);
double s0 = 680000 * 0.3048 / 1; // placeholder, not used
var pA = al.PointAt(al.StartStation); var pB = al.PointAt(al.StartStation + 100);
var dir = new Vec2(pB.X - pA.X, pB.Y - pA.Y); dir = dir * (1 / dir.Length);
string F(double x) => x.ToString("F4", inv);
(double u, double n, double z) L(Vec3 p)
{
    var d = new Vec2(p.X - pA.X, p.Y - pA.Y);
    double u = (al.StartStation + d.X * dir.X + d.Y * dir.Y) / 0.3048;
    double n = (d.X * dir.Y - d.Y * dir.X) / 0.3048; // right positive
    return (u, n, p.Z / 0.3048);
}
string LS(Vec3 p) { var q = L(p); return F(q.u) + "," + F(q.n) + "," + F(q.z); }
string Q(string s) => "\"" + (s ?? "").Replace("\"", "'").Replace("\n", " ") + "\"";

using (var w = new StreamWriter(Path.Combine(outDir, "points.csv")))
{
    w.WriteLine("plane,name,station_ft,u,n,z");
    foreach (var p in tm.Points) w.WriteLine($"{p.Plane},{p.Name},{F(p.Station / 0.3048)},{LS(p.World)}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "members.csv")))
{
    w.WriteLine("name,mark,plane,from,to,au,an,az,bu,bn,bz,work_ft,cut_ft,role,section,flagged,panel");
    foreach (var mm in tm.Members)
        w.WriteLine(string.Join(",", Q(mm.Name), Q(mm.Mark), mm.Plane, mm.From, mm.To, LS(mm.A), LS(mm.B), F(mm.WorkLength / 0.3048), F(mm.CutLength / 0.3048), mm.Role, Q(mm.Section), mm.Flagged,
            Q(tm.PanelOf.TryGetValue(mm.Mark, out var pn) ? pn : "")));
}
using (var w = new StreamWriter(Path.Combine(outDir, "parts.csv")))
using (var wv = new StreamWriter(Path.Combine(outDir, "plate-verts.csv")))
{
    w.WriteLine("idx,mark,category,assembly,description,mass_kg,vol_m3,minu,minn,minz,maxu,maxn,maxz,status,dimonly,reason,panel");
    wv.WriteLine("idx,mark,u,n,z");
    int i = 0;
    foreach (var p in st.Parts)
    {
        var b = p.Mesh.Bounds();
        var lo = L(b.Min); var hi = L(b.Max);
        // bounds in local frame from vertices (robust to rotation)
        double mu = 1e9, mn = 1e9, mz = 1e9, xu = -1e9, xn = -1e9, xz = -1e9;
        foreach (var v in p.Mesh.Positions) { var q = L(v); mu = Math.Min(mu, q.u); mn = Math.Min(mn, q.n); mz = Math.Min(mz, q.z); xu = Math.Max(xu, q.u); xn = Math.Max(xn, q.n); xz = Math.Max(xz, q.z); }
        double vol = 0; try { vol = p.Mesh.Volume(); } catch { }
        w.WriteLine(string.Join(",", i, Q(p.Mark), p.Category, Q(p.Assembly), Q(p.Description), F(p.Mass), F(vol), F(mu), F(mn), F(mz), F(xu), F(xn), F(xz),
            p.Assumption?.Status ?? "", p.Assumption?.DimensionOnly ?? false, Q(p.Assumption?.Reason ?? ""), Q(tm.PanelOf.TryGetValue(p.Mark, out var pn) ? pn : "")));
        if (p.Category is PartCategory.TrussGusset or PartCategory.TrussConnectionPlate or PartCategory.TrussConnectionAngle or PartCategory.SplicePlate or PartCategory.FillerPlate)
        {
            var seen = new HashSet<string>();
            foreach (var v in p.Mesh.Positions)
            {
                var q = L(v); var k = $"{q.u:F3},{q.n:F3},{q.z:F3}";
                if (seen.Add(k)) wv.WriteLine($"{i},{Q(p.Mark)},{F(q.u)},{F(q.n)},{F(q.z)}");
            }
        }
        i++;
    }
}
using (var w = new StreamWriter(Path.Combine(outDir, "rivets.csv")))
{
    w.WriteLine("i,u,n,z,au,an,az,dia_in,grip_in,len_in,assembly,material,flush,status,reason");
    int i = 0;
    foreach (var b in st.Bolts)
    {
        var a = b.Frame.Z; var a0 = L(b.Frame.Origin); var a1 = L(b.Frame.Origin + a);
        w.WriteLine(string.Join(",", i++, F(a0.u), F(a0.n), F(a0.z), F(a1.u - a0.u), F(a1.n - a0.n), F(a1.z - a0.z), F(b.Diameter / 0.0254), F(b.Grip / 0.0254), F(b.Length / 0.0254),
            Q(b.Assembly), Q(b.Material ?? ""), b.FlushEnd, b.Kind, Q(b.Assumption?.Reason ?? "")));
    }
}
using (var w = new StreamWriter(Path.Combine(outDir, "stacks.csv")))
{
    w.WriteLine("plane,point,thk_in,areas_sf");
    foreach (var g in tm.GussetStacks) w.WriteLine($"{g.Plane},{g.Point},{Q(string.Join(" + ", g.Thicknesses.Select(t => (t / 0.0254).ToString("0.###", inv))))},{Q(string.Join(" + ", g.Areas.Select(a => (a / 0.092903).ToString("0.0", inv))))}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "covers.csv")))
{
    w.WriteLine("plane,point,rivets,short,minedge_in,reqedge_in,outline,drawn");
    foreach (var c in tm.GussetRivetCovers) w.WriteLine($"{c.Plane},{c.Point},{c.Rivets},{c.Short},{F(c.MinEdge / 0.0254)},{F(c.RequiredEdge / 0.0254)},{c.OutlineGiven},{c.Drawn}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "diagnostics.txt")))
    foreach (var d in st.Diagnostics) w.WriteLine($"{d.Severity}\t{d.Message}");
using (var w = new StreamWriter(Path.Combine(outDir, "checks.csv")))
{
    w.WriteLine("item,plan,model,diff,ok,note");
    foreach (var c in tm.Checks) w.WriteLine(string.Join(",", Q(c.Item), Q(c.Plan), Q(c.Model), F(c.Difference), c.Ok, Q(c.Note)));
}
Print($"parts {st.Parts.Count} bolts {st.Bolts.Count} members {tm.Members.Count} stacks {tm.GussetStacks.Count}");
using (var w = new StreamWriter(Path.Combine(outDir, "dropped.csv")))
{
    w.WriteLine("plane,point,member,u_in,v_in,s_in,y_in,dia_in,beyond,reason");
    foreach (var d in tm.DroppedRivets) w.WriteLine($"{d.Plane},{d.Point},\"{d.Member}\",{(d.U/0.0254).ToString("F2",inv)},{(d.V/0.0254).ToString("F2",inv)},{(d.S/0.0254).ToString("F2",inv)},{(d.Y/0.0254).ToString("F2",inv)},{(d.Diameter/0.0254).ToString("F4",inv)},{d.Beyond},{d.Reason}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "partmember.csv")))
{
    w.WriteLine("mark,member,piece,category");
    foreach (var p in st.Parts) w.WriteLine($"\"{p.Mark}\",\"{p.Member}\",\"{p.Piece}\",{p.Category}");
}
}
Print($"A done {sw.Elapsed.TotalSeconds:0}s");
{ // ---- B: bracing
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
}
Print($"B done {sw.Elapsed.TotalSeconds:0}s");
{ // ---- C: joint stacks / rivet grips
string outDirL = Path.Combine(outDir, "layers"); Directory.CreateDirectory(outDirL);
const double IN = 0.0254;
string F(double x) => x.ToString("F3", inv);
string Q(string s) => "\"" + (s ?? "").Replace("\"", "'").Replace("\n", " ") + "\"";

// part bounds
var parts = st.Parts;
var pb = parts.Select(p => p.Mesh.Bounds()).ToArray();
var rivets = st.Bolts.Where(b => b.Kind == BoltKind.Rivet).ToList();

var wJ = new StreamWriter(Path.Combine(outDirL, "gusset-parts.csv"));
wJ.WriteLine("plane,point,mark,layer,side,thk_in,n_in_lo,n_in_hi,area_sf,desc,flag");
var wS = new StreamWriter(Path.Combine(outDirL, "stack-summary.csv"));
wS.WriteLine("plane,point,side,samples,layers,layer_gap_max_in,layer_gap_n,float_n,overlap_n,overlap_max_in,overlap_with,inner_gap_n,inner_gap_max_in,inner_nb,outer_nb,outside_steel_n");
var wP = new StreamWriter(Path.Combine(outDirL, "stack-points.csv"));
wP.WriteLine("plane,point,side,a_in,b_in,stack");
var wR = new StreamWriter(Path.Combine(outDirL, "rivet-summary.csv"));
wR.WriteLine("plane,point,side,rivets,head_buried_n,head_buried_max_in,head_off_n,grip_short_n,grip_long_n,grip_dmax_in,through_outer_layer_n,in_layer_area_n,miss_layer_n,grips,air_n,air_max_in");
var wRA = new StreamWriter(Path.Combine(outDirL, "rivet-all.csv"));
wRA.WriteLine("plane,point,side,a_in,b_in,dia_in,grip_in,air_in,layers,stack");
var wRD = new StreamWriter(Path.Combine(outDirL, "rivet-detail.csv"));
wRD.WriteLine("plane,point,side,a_in,b_in,dia_in,grip_in,chain_lo_in,chain_hi_in,chain,layers_at_point");

// Moller-Trumbore, infinite line
bool Hit(Vec3 o, Vec3 d, Vec3 a, Vec3 b, Vec3 c, out double t)
{
    t = 0;
    var e1 = b - a; var e2 = c - a;
    var p = Vec3.Cross(d, e2);
    double det = Vec3.Dot(e1, p);
    if (Math.Abs(det) < 1e-14) return false;
    double inv = 1 / det;
    var s = o - a;
    double uu = Vec3.Dot(s, p) * inv; if (uu < -1e-9 || uu > 1 + 1e-9) return false;
    var q = Vec3.Cross(s, e1);
    double vv = Vec3.Dot(d, q) * inv; if (vv < -1e-9 || uu + vv > 1 + 1e-9) return false;
    t = Vec3.Dot(e2, q) * inv;
    return true;
}

foreach (var gs in tm.GussetStacks)
{
    var pt = tm.Points.FirstOrDefault(x => x.Plane == gs.Plane && x.Name == gs.Point);
    if (pt == null) continue;
    var P = pt.World;
    var rx = new Regex("^" + Regex.Escape(gs.Plane) + "-G" + Regex.Escape(gs.Point) + "-(L|R)(\\d*)$");
    var gp = new List<(int idx, int side, int layer)>();
    for (int i = 0; i < parts.Count; i++)
    {
        if (parts[i].Category != PartCategory.TrussGusset) continue;
        var mm = rx.Match(parts[i].Mark); if (!mm.Success) continue;
        gp.Add((i, mm.Groups[1].Value == "L" ? -1 : 1, mm.Groups[2].Value.Length == 0 ? 1 : int.Parse(mm.Groups[2].Value)));
    }
    if (gp.Count == 0) continue;
    // normal from the largest triangle of the inner R gusset
    var g0 = parts[gp.First(g => g.side == 1 && g.layer == 1).idx];
    Vec3 n = new Vec3(0, 0, 1); double best = 0;
    for (int i = 0; i < g0.Mesh.Indices.Count; i += 3)
    {
        var a = g0.Mesh.Positions[g0.Mesh.Indices[i]]; var b = g0.Mesh.Positions[g0.Mesh.Indices[i + 1]]; var c = g0.Mesh.Positions[g0.Mesh.Indices[i + 2]];
        var cr = Vec3.Cross(b - a, c - a); if (cr.Length > best) { best = cr.Length; n = cr.Normalized(); }
    }
    var cen = g0.Mesh.Positions.Aggregate(new Vec3(0, 0, 0), (s, q) => s + q) * (1.0 / g0.Mesh.Positions.Count);
    if (Vec3.Dot(cen - P, n) < 0) n = -n;
    var up = new Vec3(0, 0, 1);
    var v = (up - n * Vec3.Dot(up, n)).Normalized();
    var u = Vec3.Cross(v, n).Normalized();
    foreach (var g in gp)
    {
        var p = parts[g.idx];
        double lo = p.Mesh.Positions.Min(q => Vec3.Dot(q - P, n)), hi = p.Mesh.Positions.Max(q => Vec3.Dot(q - P, n));
        var area = Regex.Match(p.Description, "\\(([0-9.]+) SF\\)");
        wJ.WriteLine(string.Join(",", gs.Plane, gs.Point, Q(p.Mark), g.layer, g.side, F((hi - lo) / IN), F(lo / IN), F(hi / IN), area.Success ? area.Groups[1].Value : "", Q(p.Description), Q(p.Assumption?.Reason ?? "")));
    }
    // candidate parts near the joint
    double R = 14 * 0.3048;
    var cand = new List<int>();
    for (int i = 0; i < parts.Count; i++)
    {
        var b = pb[i];
        if (b.Max.X < P.X - R || b.Min.X > P.X + R || b.Max.Y < P.Y - R || b.Min.Y > P.Y + R || b.Max.Z < P.Z - R || b.Min.Z > P.Z + R) continue;
        cand.Add(i);
    }
    // bucket triangles by (a, b) cell
    double cell = 6 * IN;
    var buckets = new Dictionary<(int, int), List<(int part, Vec3 a, Vec3 b, Vec3 c)>>();
    var bigTris = new List<(int part, Vec3 a, Vec3 b, Vec3 c)>();
    foreach (int i in cand)
    {
        var ms = parts[i].Mesh;
        for (int k = 0; k < ms.Indices.Count; k += 3)
        {
            var a = ms.Positions[ms.Indices[k]]; var b = ms.Positions[ms.Indices[k + 1]]; var c = ms.Positions[ms.Indices[k + 2]];
            double[] aa = { Vec3.Dot(a - P, u), Vec3.Dot(b - P, u), Vec3.Dot(c - P, u) }, bb = { Vec3.Dot(a - P, v), Vec3.Dot(b - P, v), Vec3.Dot(c - P, v) };
            int i0 = (int)Math.Floor(aa.Min() / cell), i1 = (int)Math.Floor(aa.Max() / cell), j0 = (int)Math.Floor(bb.Min() / cell), j1 = (int)Math.Floor(bb.Max() / cell);
            if ((i1 - i0 + 1) * (j1 - j0 + 1) > 4000) { bigTris.Add((i, a, b, c)); continue; }   // long member faces: tested on every ray (work3 dropped them)
            for (int ii = i0; ii <= i1; ii++) for (int jj = j0; jj <= j1; jj++)
                {
                    if (!buckets.TryGetValue((ii, jj), out var l)) buckets[(ii, jj)] = l = new();
                    l.Add((i, a, b, c));
                }
        }
    }
    // intervals along a line o + t d (t in m), per part
    List<(double t0, double t1, int part)> Intervals(Vec3 o, Vec3 d)
    {
        var w = o - P;
        var key = ((int)Math.Floor(Vec3.Dot(w, u) / cell), (int)Math.Floor(Vec3.Dot(w, v) / cell));
        var res = new List<(double, double, int)>();
        if (!buckets.TryGetValue(key, out var l0)) l0 = new();
        var l = l0.Concat(bigTris);
        // depth counting by the triangle normals (entries before exits at equal t): touching / overlapping solids of one part mesh stay one interval
        // (work3 used hit parity with de-duplication, which turned two touching plies into a gap)
        var hits = new Dictionary<int, List<(double t, int s)>>();
        foreach (var tr in l)
            if (Hit(o, d, tr.a, tr.b, tr.c, out double t))
            {
                if (!hits.TryGetValue(tr.part, out var h)) hits[tr.part] = h = new();
                h.Add((t, Vec3.Dot(Vec3.Cross(tr.b - tr.a, tr.c - tr.a), d) < 0 ? 1 : -1));
            }
        foreach (var (pi, h) in hits)
        {
            h.Sort((x, y) => x.t != y.t ? x.t.CompareTo(y.t) : y.s.CompareTo(x.s));
            int depth = 0; double start = 0;
            foreach (var (t, sg) in h)
            {
                if (sg > 0) { if (depth == 0) start = t; depth++; }
                else if (depth > 0) { depth--; if (depth == 0) res.Add((start, t, pi)); }
            }
        }
        res.Sort((x, y) => x.Item1.CompareTo(y.Item1));
        return res;
    }
    // sample region: union of the gusset parts' (a, b) extents
    double amin = 1e9, amax = -1e9, bmin = 1e9, bmax = -1e9;
    foreach (var g in gp) foreach (var q in parts[g.idx].Mesh.Positions) { double a = Vec3.Dot(q - P, u), b = Vec3.Dot(q - P, v); amin = Math.Min(amin, a); amax = Math.Max(amax, a); bmin = Math.Min(bmin, b); bmax = Math.Max(bmax, b); }
    double step = 3 * IN, jit = 0.0123 * IN;
    var gIdx = gp.ToDictionary(g => g.idx, g => g);
    int nLayers = gp.Max(g => g.layer);
    foreach (int side in new[] { -1, 1 })
    {
        int samples = 0, layerGapN = 0, floatN = 0, overlapN = 0, innerGapN = 0, outsideN = 0;
        double layerGapMax = 0, overlapMax = 0, innerGapMax = 0;
        var overlapWith = new Dictionary<string, int>(); var innerNb = new Dictionary<string, int>(); var outerNb = new Dictionary<string, int>();
        for (double a = amin + step / 2; a < amax; a += step)
            for (double b = bmin + step / 2; b < bmax; b += step)
            {
                var o = P + u * (a + jit) + v * (b + jit * 1.7);
                var iv = Intervals(o, n);
                // keep this side (t * side > 0) and convert to "outward" coordinate s = side * t
                var sideIv = iv.Select(x => side > 0 ? (s0: x.t0, s1: x.t1, part: x.part) : (s0: -x.t1, s1: -x.t0, part: x.part)).Where(x => x.s1 > 0).OrderBy(x => x.s0).ToList();
                var myG = sideIv.Where(x => gIdx.TryGetValue(x.part, out var g) && g.side == side).ToList();
                if (myG.Count == 0) continue;
                samples++;
                var stackTxt = string.Join(" | ", sideIv.Where(x => x.s0 < myG.Max(g => g.s1) + 3 * IN && x.s1 > myG.Min(g => g.s0) - 3 * IN)
                    .Select(x => $"{parts[x.part].Mark}[{(x.s0 / IN):F3}..{(x.s1 / IN):F3}]"));
                if (nLayers > 1 || samples % 4 == 0) wP.WriteLine($"{gs.Plane},{gs.Point},{side},{F(a / IN)},{F(b / IN)},{Q(stackTxt)}");
                // layer adjacency
                var byLayer = myG.ToDictionary(x => gIdx[x.part].layer, x => x);
                foreach (var (k, x) in byLayer)
                {
                    if (k < 2) continue;
                    if (byLayer.TryGetValue(k - 1, out var y))
                    {
                        double gap = x.s0 - y.s1;
                        if (Math.Abs(gap) > IN / 32) { layerGapN++; layerGapMax = Math.Max(layerGapMax, Math.Abs(gap)); }
                    }
                    else floatN++;
                }
                // overlaps involving my gussets
                bool ov = false;
                foreach (var x in myG)
                    foreach (var y in sideIv)
                    {
                        if (y.part == x.part) continue;
                        double o2 = Math.Min(x.s1, y.s1) - Math.Max(x.s0, y.s0);
                        if (o2 > IN / 32)
                        {
                            ov = true; overlapMax = Math.Max(overlapMax, o2);
                            var key = $"{parts[x.part].Mark.Split('-').Last()}x{parts[y.part].Category}";
                            overlapWith[key] = overlapWith.GetValueOrDefault(key) + 1;
                        }
                    }
                if (ov) overlapN++;
                // inner gusset to the steel under it
                var inner = myG.OrderBy(x => x.s0).First();
                var under = sideIv.Where(x => !gIdx.ContainsKey(x.part) && x.s1 <= inner.s0 + IN / 32 && x.s1 > inner.s0 - 2 * IN).OrderByDescending(x => x.s1).FirstOrDefault();
                if (under.s1 != 0 || under.part != 0)
                {
                    double gap = inner.s0 - under.s1;
                    var cat = parts[under.part].Category.ToString();
                    innerNb[cat] = innerNb.GetValueOrDefault(cat) + 1;
                    if (gap > IN / 32) { innerGapN++; innerGapMax = Math.Max(innerGapMax, gap); }
                }
                var outer = myG.OrderBy(x => x.s1).Last();
                var over = sideIv.Where(x => !gIdx.ContainsKey(x.part) && x.s0 >= outer.s1 - IN / 32 && x.s0 < outer.s1 + 2 * IN).OrderBy(x => x.s0).FirstOrDefault();
                if (over.s1 != 0 || over.part != 0)
                {
                    outsideN++;
                    var cat = parts[over.part].Category.ToString();
                    outerNb[cat] = outerNb.GetValueOrDefault(cat) + 1;
                }
            }
        string D(Dictionary<string, int> d) => Q(string.Join("; ", d.OrderByDescending(x => x.Value).Select(x => $"{x.Key}:{x.Value}")));
        wS.WriteLine(string.Join(",", gs.Plane, gs.Point, side, samples, nLayers, F(layerGapMax / IN), layerGapN, floatN, overlapN, F(overlapMax / IN), D(overlapWith), innerGapN, F(innerGapMax / IN), D(innerNb), D(outerNb), outsideN));

        // rivets of this face
        double rr = Math.Max(Math.Max(Math.Abs(amin), amax), Math.Max(Math.Abs(bmin), bmax)) + 6 * IN;
        int airN = 0; double airMax = 0; int nr = 0, buried = 0, headOff = 0, gShort = 0, gLong = 0, thruOuter = 0, inLayerArea = 0, missLayer = 0; double buriedMax = 0, gdMax = 0;
        var grips = new Dictionary<string, int>();
        foreach (var bt in rivets)
        {
            var z = bt.Frame.Z;
            if (Vec3.Dot(z, n) * -side < 0.95) continue;          // shank pointing inward from this face
            var w = bt.Frame.Origin - P;
            double ta = Vec3.Dot(w, u), tb = Vec3.Dot(w, v), tn = Vec3.Dot(w, n) * side;
            if (Math.Abs(ta) > rr || Math.Abs(tb) > rr || tn < 0 || tn > 3 * 0.3048) continue;
            if (ta < amin || ta > amax || tb < bmin || tb > bmax) continue;
            // the rivet must lie over one of this face's gusset parts
            var mid = bt.Frame.Origin + z * (bt.Grip / 2);
            var iv = Intervals(mid + u * jit, z).OrderBy(x => x.t0).ToList();   // t relative to mid along z
            double h0 = -bt.Grip / 2;   // head face at t = h0
            var myG = iv.Where(x => gIdx.TryGetValue(x.part, out var g) && g.side == side).ToList();
            if (myG.Count == 0) continue;
            nr++;
            // contiguous chain containing the shank start (t just above h0)
            var chain = new List<(double t0, double t1, int part)>();
            var seed = iv.Where(x => x.t0 <= h0 + IN / 16 && x.t1 >= h0 + IN / 64).OrderBy(x => x.t0).FirstOrDefault();
            double clo = double.NaN, chi = double.NaN;
            if (seed.t1 != 0 || seed.part != 0)
            {
                clo = seed.t0; chi = seed.t1; chain.Add(seed);
                bool grow = true;
                while (grow)
                {
                    grow = false;
                    foreach (var x in iv)
                    {
                        if (chain.Contains(x)) continue;
                        if (x.t1 >= clo - IN / 64 && x.t0 <= chi + IN / 64) { chain.Add(x); clo = Math.Min(clo, x.t0); chi = Math.Max(chi, x.t1); grow = true; }
                    }
                }
            }
            else { headOff++; }
            if (!double.IsNaN(clo))
            {
                double bur = h0 - clo;
                if (bur > IN / 32) { buried++; buriedMax = Math.Max(buriedMax, bur); }
                double lenChain = chi - Math.Max(clo, h0);
                double dg = bt.Grip - (chi - h0);
                if (dg > IN / 16) gShort++; else if (dg < -IN / 16) gLong++;
                gdMax = Math.Max(gdMax, Math.Abs(dg));
            }
            var layersHere = myG.Select(x => gIdx[x.part].layer).Distinct().OrderBy(x => x).ToList();
            int maxL = layersHere.Max();
            if (maxL > 1) inLayerArea++;
            bool thru = chain.Any(x => gIdx.TryGetValue(x.part, out var g) && g.side == side && g.layer == maxL);
            if (maxL > 1 && thru) thruOuter++;
            if (maxL > 1 && !thru) missLayer++;
            string gk = (bt.Grip / IN).ToString("0.###", inv);
            grips[gk] = grips.GetValueOrDefault(gk) + 1;
            double steel = 0; foreach (var x in iv) { double a0 = Math.Max(x.t0, h0), a1 = Math.Min(x.t1, h0 + bt.Grip); if (a1 > a0) steel += a1 - a0; }
            double air = bt.Grip - steel;
            if (air > IN / 16) { airN++; airMax = Math.Max(airMax, air); }
            string stk = string.Join(" ", iv.Where(x => x.t1 > h0 - IN && x.t0 < h0 + bt.Grip + IN).Select(x => $"{parts[x.part].Mark}[{((x.t0 - h0) / IN):F3}..{((x.t1 - h0) / IN):F3}]"));
            wRA.WriteLine(string.Join(",", gs.Plane, gs.Point, side, F(ta / IN), F(tb / IN), F(bt.Diameter / IN), F(bt.Grip / IN), F(air / IN), Q(string.Join("+", layersHere)), Q(stk)));
            if (nLayers > 1 || nr % 10 == 0)
                wRD.WriteLine(string.Join(",", gs.Plane, gs.Point, side, F(ta / IN), F(tb / IN), F(bt.Diameter / IN), F(bt.Grip / IN), double.IsNaN(clo) ? "" : F((clo - h0) / IN), double.IsNaN(chi) ? "" : F((chi - h0) / IN),
                    Q(string.Join(" ", chain.OrderBy(x => x.t0).Select(x => parts[x.part].Mark))), Q(string.Join("+", layersHere))));
        }
        wR.WriteLine(string.Join(",", gs.Plane, gs.Point, side, nr, buried, F(buriedMax / IN), headOff, gShort, gLong, F(gdMax / IN), thruOuter, inLayerArea, missLayer, Q(string.Join("; ", grips.OrderByDescending(x => x.Value).Take(6).Select(x => $"{x.Key}:{x.Value}"))), airN, F(airMax / IN)));
    }
    wS.Flush(); wR.Flush();
}
wRA.Close(); wJ.Close(); wS.Close(); wP.Close(); wR.Close(); wRD.Close();
using (var w = new StreamWriter(Path.Combine(outDirL, "stacks.csv")))
{
    w.WriteLine("plane,point,thk_in,areas_sf");
    foreach (var g in tm.GussetStacks) w.WriteLine($"{g.Plane},{g.Point},{Q(string.Join(" + ", g.Thicknesses.Select(t => (t / IN).ToString("0.###", inv))))},{Q(string.Join(" + ", g.Areas.Select(a => (a / 0.092903).ToString("0.0", inv))))}");
}
using (var w = new StreamWriter(Path.Combine(outDirL, "diag-gusset.txt")))
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("gusset", StringComparison.OrdinalIgnoreCase) || d.Message.Contains("layer", StringComparison.OrdinalIgnoreCase)))
        w.WriteLine($"{d.Severity}\t{d.Message}");
Print($"done {sw.Elapsed.TotalSeconds:0}s");
using (var w = new StreamWriter(Path.Combine(outDirL, "diag-all.txt")))
    foreach (var d in st.Diagnostics) w.WriteLine($"{d.Severity}\t{d.Message}");
using (var w = new StreamWriter(Path.Combine(outDirL, "parts.csv")))
{
    w.WriteLine("mark,category,assembly,len_ft,mass_lb,cx,cy,cz,vol_cf,depth_in,width_in,desc");
    var mem = tm.Members.ToDictionary(x => x.Mark, x => x);
    foreach (var p in st.Parts)
    {
        var bx = p.Mesh.Bounds(); var c = (bx.Min + bx.Max) * 0.5;
        string dep = "", wid = "";
        if (mem.TryGetValue(p.Mark, out var mr))
        {
            // extents in the member frame: across = horizontal normal to the axis, up = normal to axis and across
            var ax = (mr.B - mr.A).Normalized(); var acr = Vec3.Cross(new Vec3(0, 0, 1), ax).Normalized(); var upv = Vec3.Cross(ax, acr).Normalized();
            dep = ((p.Mesh.Positions.Max(q => Vec3.Dot(q, upv)) - p.Mesh.Positions.Min(q => Vec3.Dot(q, upv))) / IN).ToString("F3", inv);
            wid = ((p.Mesh.Positions.Max(q => Vec3.Dot(q, acr)) - p.Mesh.Positions.Min(q => Vec3.Dot(q, acr))) / IN).ToString("F3", inv);
        }
        w.WriteLine($"{Q(p.Mark)},{p.Category},{Q(p.Assembly)},{(p.Length / 0.3048).ToString("F3", inv)},{(p.Mass * 2.20462262).ToString("F1", inv)},{F(c.X)},{F(c.Y)},{F(c.Z)},{(p.Volume / 0.0283168).ToString("F4", inv)},{dep},{wid},{Q(p.Description)}");
    }
}
Print($"steel {st.SteelMass * 2.20462262:N0} lb, rivets {rivets.Count}");
}
Print($"C done {sw.Elapsed.TotalSeconds:0}s");
{ // ---- D: clash with bolts
    const double IN = 0.0254;
    var rep = Doc.Clashes(BN, new SBM.Core.Checks.ClashOptions { Bolts = true, Misc = false, Concrete = false });
    var cl = rep.SelectMany(x => x.Clashes).ToList();
    Print($"clash (bolts) {cl.Count}");
    using var wc = new StreamWriter(Path.Combine(outDir, "clash.csv"));
    wc.WriteLine("kindA,markA,kindB,markB,pair,depth_in,x,y,z");
    foreach (var c in cl) wc.WriteLine($"{c.A.Kind},\"{c.A.Mark}\",{c.B.Kind},\"{c.B.Mark}\",\"{c.CategoryPair}\",{(c.Depth / IN).ToString("F3", inv)},{c.At.X.ToString("F3", inv)},{c.At.Y.ToString("F3", inv)},{c.At.Z.ToString("F3", inv)}");
}
Print($"D done {sw.Elapsed.TotalSeconds:0}s");
