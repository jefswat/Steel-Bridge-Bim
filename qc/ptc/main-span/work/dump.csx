// QC main span connections (read-only, no save): regenerate ONLY the main span bridge in memory and dump parts / rivets / members / points / diagnostics / clash.
//   sbm run dump.csx "<PTC model>" -- <outdir> [clash]
// Frame: u = along the alignment (ft from the alignment start of station 0 tangent, = station ft), n = offset right (ft), z = elevation (ft).
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
var inv = CultureInfo.InvariantCulture;
string outDir = Args[0];
bool doClash = Args.Length > 1 && Args[1] == "clash";
Directory.CreateDirectory(outDir);
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
var sw = System.Diagnostics.Stopwatch.StartNew();
var rep = Doc.Regenerate();
Print($"regen {sw.Elapsed.TotalSeconds:0.0}s bridges {Doc.Project.Bridges.Count}");
var m = Doc.Model(BN);
var st = m.Steel!;
var tm = st.Truss!;
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
if (doClash)
{
    sw.Restart();
    var o = new SBM.Core.Checks.ClashOptions { Bolts = true, Misc = false, Concrete = false };
    var cr = SBM.Core.Checks.SteelClashCheck.Run(m, o, Doc.Units);
    using var w = new StreamWriter(Path.Combine(outDir, "clash.csv"));
    w.WriteLine("kindA,markA,asmA,kindB,markB,asmB,depth_in,u,n,z,points");
    foreach (var c in cr.Clashes)
        w.WriteLine(string.Join(",", c.A.Kind, Q(c.A.Mark), Q(c.A.Assembly), c.B.Kind, Q(c.B.Mark), Q(c.B.Assembly), F(c.Depth / 0.0254), LS(c.At), c.Points));
    Print($"clash {cr.Clashes.Count} rows, items {cr.Items}, {sw.Elapsed.TotalSeconds:0}s");
}
