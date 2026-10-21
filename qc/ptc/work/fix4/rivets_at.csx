// Fix pass 4 (user item): rivets near one truss point, in the point's frame (u = horizontal along the bridge, v = up, n = across, inches).
//   sbm run rivets_at.csx model.sbm -- "PTC 4-span truss 14-17" N L20 out.csv [radius_ft]
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
var inv = CultureInfo.InvariantCulture;
string bn = Args[0], pl = Args[1], pt = Args[2], outPath = Args[3];
double R = (Args.Length > 4 ? double.Parse(Args[4], inv) : 6) * 0.3048;
Doc.Regenerate();
var st = Doc.Model(bn).Steel!;
var P = st.Truss!.Point(pl, pt)!.World;
// along: the chord direction from the lower chord members at the point
var al = Doc.Project.Alignments.First(a => a.Name == Doc.Project.Bridges.First(b => b.Name == bn).AlignmentName);
double sta = al.Inverse(new Vec2(P.X, P.Y)).Station;
var p0 = al.PointAt(sta - 1); var p1 = al.PointAt(sta + 1);
var u = new Vec3(p1.X - p0.X, p1.Y - p0.Y, 0); u = u * (1 / u.Length);
var z = new Vec3(0, 0, 1); var n = Vec3.Cross(u, z);
var w = new StreamWriter(outPath);
w.WriteLine("u_in,v_in,n_in,dia_in,grip_in,assembly,shank_n");
int k = 0;
foreach (var b in st.Bolts.Where(b => b.Kind == BoltKind.Rivet))
{
    var d = b.Frame.Origin - P;
    if (d.Length > R) continue;
    w.WriteLine(string.Join(",", (Vec3.Dot(d, u) / 0.0254).ToString("F2", inv), (Vec3.Dot(d, z) / 0.0254).ToString("F2", inv), (Vec3.Dot(d, n) / 0.0254).ToString("F2", inv),
        (b.Diameter / 0.0254).ToString("F3", inv), (b.Grip / 0.0254).ToString("F3", inv), b.Assembly, Vec3.Dot(b.Frame.Z, n).ToString("F2", inv)));
    k++;
}
w.Close();
Print($"{k} rivets within {R / 0.3048:0.#} ft of {pl}-{pt} -> {outPath}");
