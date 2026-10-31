// read-only: parts near each main shoe (box u +-6 ft, n +-5 ft, z WP-12 ft..WP+3 ft) + all main part marks/categories/assemblies. NO save.
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
var inv = CultureInfo.InvariantCulture;
string outDir = Args[0];
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
var rep = Doc.Regenerate();
var st = Doc.Model(BN).Steel!; var tm = st.Truss!;
var bd = Doc.Project.Bridges[0];
var al = Doc.Project.Alignments.First(a => a.Name == bd.AlignmentName);
(double u, double n, double z) L(Vec3 p) { var q = al.Inverse(new Vec2(p.X, p.Y)); return (q.Station / 0.3048, q.Offset / 0.3048, p.Z / 0.3048); }
using (var w = new StreamWriter(Path.Combine(outDir, "near-shoes.txt")))
foreach (var sh in tm.Shoes)
{
    var lw = L(sh.Pin);
    var wp = tm.Points.First(p => p.Plane == sh.Name.Split('-')[0] && p.Name == sh.Name.Substring(2));
    double wz = wp.World.Z / 0.3048;
    w.WriteLine($"##### {sh.Name} pin u={lw.u:0.000} n={lw.n:0.000} z={lw.z:0.0000} WP z={wz:0.0000}");
    foreach (var p in st.Parts)
    {
        if (p.Mesh.IsEmpty) continue;
        var b = p.Mesh.Bounds();
        var c = L(new Vec3((b.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2, 0));
        if (Math.Abs(c.u - lw.u) > 60 || Math.Abs(c.n - lw.n) > 30) continue;
        double mu = 1e9, mn = 1e9, mz = 1e9, xu = -1e9, xn = -1e9, xz = -1e9;
        foreach (var v in p.Mesh.Positions) { var q = L(v); mu = Math.Min(mu, q.u); mn = Math.Min(mn, q.n); mz = Math.Min(mz, q.z); xu = Math.Max(xu, q.u); xn = Math.Max(xn, q.n); xz = Math.Max(xz, q.z); }
        if (xu < lw.u - 6 || mu > lw.u + 6 || xn < lw.n - 5 || mn > lw.n + 5 || mz > wz + 3 || xz < wz - 12) continue;
        w.WriteLine($"  {p.Mark,-40} {p.Category,-22} {p.Assembly,-22} u {(mu - lw.u) * 12,8:0.00}..{(xu - lw.u) * 12,8:0.00} n {(mn - lw.n) * 12,8:0.00}..{(xn - lw.n) * 12,8:0.00} zRelWP {(mz - wz) * 12,8:0.00}..{(xz - wz) * 12,8:0.00} in | {p.Description}");
    }
}
using (var w = new StreamWriter(Path.Combine(outDir, "all-parts.tsv")))
{
    w.WriteLine("mark\tcategory\tassembly\tmass_kg\tlength_m\tdescription");
    foreach (var p in st.Parts) w.WriteLine($"{p.Mark}\t{p.Category}\t{p.Assembly}\t{p.Mass:0.0}\t{p.Length:0.000}\t{p.Description}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "assemblies.tsv")))
    foreach (var a in st.Assemblies) w.WriteLine($"{a.Mark}\t{a.Kind}\t{a.Description}");
using (var w = new StreamWriter(Path.Combine(outDir, "members-res.tsv")))
    foreach (var m in tm.Members) w.WriteLine($"{m.Name}\t{m.Mark}\t{m.Plane}\t{m.From}\t{m.To}\t{m.Role}\t{m.Section}\t{m.WorkLength / 0.3048:0.0000}\t{m.CutLength / 0.3048:0.0000}\t{tm.PanelOf.GetValueOrDefault(m.Mark)}");
Print("done");
