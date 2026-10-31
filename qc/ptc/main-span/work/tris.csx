// QC: triangles of the N-truss-plane parts of the main span (read-only, no save), projected to (u = station ft, z ft), plus n range.
//   sbm run tris.csx "<PTC model>" --no-pipeline -- <out.csv>
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
var inv = CultureInfo.InvariantCulture;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var st = Doc.Model(BN).Steel!;
var bd = Doc.Project.Bridges[0];
var al = Doc.Project.Alignments.First(a => a.Name == bd.AlignmentName);
var pA = al.PointAt(al.StartStation); var pB = al.PointAt(al.StartStation + 100);
var dir = new Vec2(pB.X - pA.X, pB.Y - pA.Y); dir = dir * (1 / dir.Length);
(double u, double n, double z) L(Vec3 p)
{
    var d = new Vec2(p.X - pA.X, p.Y - pA.Y);
    return ((al.StartStation + d.X * dir.X + d.Y * dir.Y) / 0.3048, (d.X * dir.Y - d.Y * dir.X) / 0.3048, p.Z / 0.3048);
}
var w = new StreamWriter(Args[0]);
w.WriteLine("mark,cat,u1,z1,u2,z2,u3,z3,nmin,nmax");
foreach (var p in st.Parts)
{
    if (p.Category is not (PartCategory.TrussGusset or PartCategory.TrussChord or PartCategory.TrussDiagonal or PartCategory.TrussVertical or PartCategory.TrussHanger
        or PartCategory.SplicePlate or PartCategory.FillerPlate or PartCategory.TrussConnectionPlate or PartCategory.TrussConnectionAngle or PartCategory.TrussFloorBeam or PartCategory.TrussBracing)) continue;
    var q = p.Mesh.Positions.Select(L).ToArray();
    if (q.Max(x => x.n) > -30) continue; // N truss only
    for (int i = 0; i < p.Mesh.Indices.Count; i += 3)
    {
        var a = q[p.Mesh.Indices[i]]; var b = q[p.Mesh.Indices[i + 1]]; var c = q[p.Mesh.Indices[i + 2]];
        w.WriteLine(string.Join(",", "\"" + p.Mark + "\"", p.Category, a.u.ToString("F4", inv), a.z.ToString("F4", inv), b.u.ToString("F4", inv), b.z.ToString("F4", inv),
            c.u.ToString("F4", inv), c.z.ToString("F4", inv), Math.Min(a.n, Math.Min(b.n, c.n)).ToString("F4", inv), Math.Max(a.n, Math.Max(b.n, c.n)).ToString("F4", inv)));
    }
}
w.Close();
Print("done");
