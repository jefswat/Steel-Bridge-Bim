// Agent review 2026-11-02: parts list (bbox) of the main span + triangles of every part near the target marks (read-only, no save).
//   sbm run dump.csx "<PTC model>" --no-pipeline -- <outdir> <radius m> <mark,mark,...> [<x,y,z;x,y,z extra centres>]
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Solids;
var inv = CultureInfo.InvariantCulture;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var st = Doc.Model(BN).Steel!;
string outDir = Args[0]; double R = double.Parse(Args[1], inv);
var targets = Args[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
Directory.CreateDirectory(outDir);
string F(double v) => v.ToString("F4", inv);
string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";
var boxes = st.Parts.Select(p => (p, b: p.Mesh.Bounds())).ToList();
using (var w = new StreamWriter(Path.Combine(outDir, "parts.csv")))
{
    w.WriteLine("mark,cat,assembly,member,material,desc,x0,y0,z0,x1,y1,z1,mass,status");
    foreach (var (p, b) in boxes)
        w.WriteLine(string.Join(",", Q(p.Mark), p.Category, Q(p.Assembly), Q(p.Member ?? ""), Q(p.Material), Q(p.Description), F(b.Min.X), F(b.Min.Y), F(b.Min.Z), F(b.Max.X), F(b.Max.Y), F(b.Max.Z),
            F(p.Mass), Q(p.Assumption == null ? "" : p.Assumption.Status + ": " + p.Assumption.Reason)));
}
using (var w = new StreamWriter(Path.Combine(outDir, "assemblies.csv")))
{
    w.WriteLine("mark,kind,desc");
    foreach (var a in st.Assemblies) w.WriteLine(string.Join(",", Q(a.Mark), a.Kind, Q(a.Description)));
}
var centres = new List<(string name, Vec3 lo, Vec3 hi)>();
foreach (var t in targets)
{
    var hit = t.StartsWith("~") ? boxes.Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.p.Mark + " " + x.p.Assembly + " " + x.p.Description, t[1..])).ToList()
        : boxes.Where(x => x.p.Mark.Equals(t, StringComparison.OrdinalIgnoreCase)).ToList();
    if (hit.Count == 0) { Print("not found " + t); continue; }
    foreach (var h in hit) centres.Add((t, h.b.Min, h.b.Max));
}
if (Args.Length > 3)
    foreach (var c in Args[3].Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var v = c.Split(',').Select(x => double.Parse(x, inv)).ToArray();
        var p0 = new Vec3(v[0], v[1], v[2]);
        centres.Add(($"P{centres.Count}", p0, p0));
    }
bool Near((Vec3 lo, Vec3 hi) a, Box3 b) =>
    b.Min.X <= a.hi.X + R && b.Max.X >= a.lo.X - R && b.Min.Y <= a.hi.Y + R && b.Max.Y >= a.lo.Y - R && b.Min.Z <= a.hi.Z + R && b.Max.Z >= a.lo.Z - R;
using (var w = new StreamWriter(Path.Combine(outDir, "targets.csv")))
{
    w.WriteLine("name,x0,y0,z0,x1,y1,z1");
    foreach (var c in centres) w.WriteLine(string.Join(",", Q(c.name), F(c.lo.X), F(c.lo.Y), F(c.lo.Z), F(c.hi.X), F(c.hi.Y), F(c.hi.Z)));
}
using (var w = new StreamWriter(Path.Combine(outDir, "tris.csv")))
{
    w.WriteLine("mark,cat,ax,ay,az,bx,by,bz,cx,cy,cz");
    foreach (var (p, b) in boxes)
    {
        if (!centres.Any(c => Near((c.lo, c.hi), b))) continue;
        var P = p.Mesh.Positions; var I = p.Mesh.Indices;
        for (int i = 0; i < I.Count; i += 3)
        {
            var a = P[I[i]]; var bb = P[I[i + 1]]; var c = P[I[i + 2]];
            w.WriteLine(string.Join(",", Q(p.Mark), p.Category, F(a.X), F(a.Y), F(a.Z), F(bb.X), F(bb.Y), F(bb.Z), F(c.X), F(c.Y), F(c.Z)));
        }
    }
}
// alignment frame for station / offset readouts
var bd = Doc.Project.Bridges[0];
var al = Doc.Project.Alignments.First(a => a.Name == bd.AlignmentName);
using (var w = new StreamWriter(Path.Combine(outDir, "frame.csv")))
{
    var p0 = al.PointAt(al.StartStation); var n0 = al.RightNormalAt(al.StartStation);
    w.WriteLine("sta0,x0,y0,nx,ny");
    w.WriteLine(string.Join(",", F(al.StartStation), F(p0.X), F(p0.Y), F(n0.X), F(n0.Y)));
}
Print("done " + centres.Count);
