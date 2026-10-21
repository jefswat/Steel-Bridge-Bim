// QC pass 5 (read-only): rivets near several truss points (one regeneration), in the point's frame (u = horizontal along the bridge, v = up, n = across, inches).
//   sbm run rivets_multi.csx model.sbm -- outdir "Bridge|Plane|Point" ... [radius_ft via env QC_R]
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
var inv = CultureInfo.InvariantCulture;
string outDir = Args[0];
double R = 7 * 0.3048;
Doc.Regenerate();
for (int a = 1; a < Args.Length; a++)
{
    var sp = Args[a].Split('|');
    string bn = sp[0], pl = sp[1], pt = sp[2];
    var st = Doc.Model(bn).Steel!;
    var tp = st.Truss!.Point(pl, pt);
    if (tp == null) { Print("no point " + Args[a]); continue; }
    var P = tp.World;
    var al = Doc.Project.Alignments.First(x => x.Name == Doc.Project.Bridges.First(b => b.Name == bn).AlignmentName);
    double sta = al.Inverse(new Vec2(P.X, P.Y)).Station;
    var p0 = al.PointAt(sta - 1); var p1 = al.PointAt(sta + 1);
    var u = new Vec3(p1.X - p0.X, p1.Y - p0.Y, 0); u = u * (1 / u.Length);
    var z = new Vec3(0, 0, 1); var n = Vec3.Cross(u, z);
    string fn = Path.Combine(outDir, (bn.Split(' ')[1] + "_" + pl + "_" + pt).Replace("'", "p") + ".csv");
    var w = new StreamWriter(fn);
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
    Print($"{k} rivets near {bn} {pl}-{pt} -> {fn}");
}
