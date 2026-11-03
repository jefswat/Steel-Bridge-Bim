// Agent review 2026-11-02 (user: "screenshots of each joint ... main span only"): triangles + rivets around every main truss joint (read-only, no save).
//   sbm run dump_joints.csx "<PTC model>" --no-pipeline -- <outdir> [half-size m]
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Solids;
var inv = CultureInfo.InvariantCulture;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var st = Doc.Model(BN).Steel!;
var tm = st.Truss!;
string outDir = Args[0]; double H = Args.Length > 1 ? double.Parse(Args[1], inv) : 3.0;
Directory.CreateDirectory(outDir);
string F(double v) => v.ToString("F4", inv);
string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";
var bd = Doc.Project.Bridges[0];
var al = Doc.Project.Alignments.First(a => a.Name == bd.AlignmentName);
var broles = new[] { SBM.Core.Bridge.TrussMemberRole.UpperLateral, SBM.Core.Bridge.TrussMemberRole.LowerLateral, SBM.Core.Bridge.TrussMemberRole.Sway, SBM.Core.Bridge.TrussMemberRole.Portal, SBM.Core.Bridge.TrussMemberRole.Strut };
var ends = new HashSet<string>();
foreach (var m in tm.Members.Where(x => broles.Contains(x.Role))) { ends.Add(m.From); ends.Add(m.To); }
string Nm(string s) => s.Contains(':') ? s[(s.IndexOf(':') + 1)..] : s;
var endNames = ends.Select(Nm).ToHashSet();
// one point per name: the S plane copy for truss panel points, the point itself for CL / work points
var joints = tm.Points.Where(p => endNames.Contains(p.Name)).GroupBy(p => p.Name).Select(g => g.FirstOrDefault(p => p.Plane == "S") ?? g.First()).ToList();
var boxes = st.Parts.Where(p => p.Mesh is { IsEmpty: false }).Select(p => (p, b: p.Mesh.Bounds())).ToList();
using (var w = new StreamWriter(Path.Combine(outDir, "joints.csv")))
{
    w.WriteLine("plane,name,station,x,y,z,tx,ty,nx,ny");
    foreach (var j in joints)
    {
        var rn = al.RightNormalAt(j.Station);
        w.WriteLine(string.Join(",", j.Plane, Q(j.Name), F(j.Station), F(j.World.X), F(j.World.Y), F(j.World.Z), F(-rn.Y), F(rn.X), F(rn.X), F(rn.Y)));
    }
}
using (var w = new StreamWriter(Path.Combine(outDir, "parts.csv")))
{
    w.WriteLine("mark,cat,assembly,member,desc,status");
    foreach (var (p, b) in boxes) w.WriteLine(string.Join(",", Q(p.Mark), p.Category, Q(p.Assembly), Q(p.Member ?? ""), Q(p.Description), Q(p.Assumption == null ? "" : p.Assumption.Reason)));
}
foreach (var j in joints)
{
    var P = j.World;
    string tag = $"{j.Plane}_{j.Name.Replace("'", "p")}";
    using (var w = new StreamWriter(Path.Combine(outDir, $"tris_{tag}.csv")))
    {
        w.WriteLine("mark,cat,ax,ay,az,bx,by,bz,cx,cy,cz");
        foreach (var (p, b) in boxes)
        {
            if (b.Min.X > P.X + H || b.Max.X < P.X - H || b.Min.Y > P.Y + H || b.Max.Y < P.Y - H || b.Min.Z > P.Z + H || b.Max.Z < P.Z - H) continue;
            var Ps = p.Mesh.Positions; var I = p.Mesh.Indices;
            for (int i = 0; i < I.Count; i += 3)
            {
                var a = Ps[I[i]]; var bb = Ps[I[i + 1]]; var c = Ps[I[i + 2]];
                if (Math.Max(a.X, Math.Max(bb.X, c.X)) < P.X - H || Math.Min(a.X, Math.Min(bb.X, c.X)) > P.X + H || Math.Max(a.Y, Math.Max(bb.Y, c.Y)) < P.Y - H || Math.Min(a.Y, Math.Min(bb.Y, c.Y)) > P.Y + H
                    || Math.Max(a.Z, Math.Max(bb.Z, c.Z)) < P.Z - H || Math.Min(a.Z, Math.Min(bb.Z, c.Z)) > P.Z + H) continue;
                w.WriteLine(string.Join(",", Q(p.Mark), p.Category, F(a.X), F(a.Y), F(a.Z), F(bb.X), F(bb.Y), F(bb.Z), F(c.X), F(c.Y), F(c.Z)));
            }
        }
    }
    using (var w = new StreamWriter(Path.Combine(outDir, $"rivets_{tag}.csv")))
    {
        w.WriteLine("x,y,z,ax,ay,az,dia,grip,assembly");
        foreach (var r in st.Bolts)
        {
            var o = r.Frame.Origin;
            if (Math.Abs(o.X - P.X) > H || Math.Abs(o.Y - P.Y) > H || Math.Abs(o.Z - P.Z) > H) continue;
            var z = r.Frame.Z;
            w.WriteLine(string.Join(",", F(o.X), F(o.Y), F(o.Z), F(z.X), F(z.Y), F(z.Z), F(r.Diameter), F(r.Grip), Q(r.Assembly)));
        }
    }
}
Print($"done {joints.Count} joints");
