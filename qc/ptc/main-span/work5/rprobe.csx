// QC batch 5 (read-only): for rivets lying in parts whose mark matches a regex, print the steel along the shank line (+-8 in.) - which parts, where (in. from the rivet origin)
//   sbm run rprobe.csx "<model>" --no-pipeline -- <mark regex> [count]
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var st = Doc.Model(BN).Steel!;
var rx = new System.Text.RegularExpressions.Regex(Args[0]);
int cnt = Args.Length > 1 ? int.Parse(Args[1]) : 3;
const double IN = 0.0254;
var tgt = st.Parts.Where(p => rx.IsMatch(p.Mark) && p.Mesh is { IsEmpty: false }).ToList();
Print($"{tgt.Count} parts match; e.g. {string.Join(", ", tgt.Take(4).Select(p => p.Mark))}");
List<(double, double)> Iv(Mesh mesh, Vec3 o, Vec3 d)
{
    var ts = new List<double>(); var V = mesh.Positions; var I = mesh.Indices;
    for (int k = 0; k + 2 < I.Count; k += 3)
    {
        var a = V[I[k]]; var b = V[I[k + 1]]; var c = V[I[k + 2]];
        var e1 = b - a; var e2 = c - a; var h = Vec3.Cross(d, e2); double det = Vec3.Dot(e1, h); if (Math.Abs(det) < 1e-14) continue;
        double f = 1 / det; var s = o - a; double u = f * Vec3.Dot(s, h); if (u < 0 || u > 1) continue; var q = Vec3.Cross(s, e1); double v = f * Vec3.Dot(d, q); if (v < 0 || u + v > 1) continue;
        ts.Add(f * Vec3.Dot(e2, q));
    }
    ts.Sort(); var u2 = new List<double>(); foreach (var t in ts) if (u2.Count == 0 || t - u2[^1] > 1e-6) u2.Add(t);
    var r = new List<(double, double)>(); for (int k = 0; k + 1 < u2.Count; k += 2) r.Add((u2[k], u2[k + 1])); return r;
}
int shown = 0;
var pbs = st.Parts.Select(p => p.Mesh is { IsEmpty: false } ? p.Mesh.Bounds() : default).ToArray();
foreach (var t in tgt)
{
    var b = t.Mesh.Bounds();
    foreach (var r in st.Bolts.Where(r => r.Kind == BoltKind.Rivet))
    {
        var m = r.Frame.Origin + r.Frame.Z * (r.Grip / 2);
        if (m.X < b.Min.X || m.X > b.Max.X || m.Y < b.Min.Y || m.Y > b.Max.Y || m.Z < b.Min.Z || m.Z > b.Max.Z) continue;
        var o = r.Frame.Origin - r.Frame.Z * (8 * IN); var d = r.Frame.Z;
        var hits = new List<string>();
        for (int i = 0; i < st.Parts.Count; i++)
        {
            var pb = pbs[i]; double e = 0.25;
            if (st.Parts[i].Mesh is not { IsEmpty: false } || m.X < pb.Min.X - e || m.X > pb.Max.X + e || m.Y < pb.Min.Y - e || m.Y > pb.Max.Y + e || m.Z < pb.Min.Z - e || m.Z > pb.Max.Z + e) continue;
            foreach (var (a0, a1) in Iv(st.Parts[i].Mesh, o, d)) if (a1 > 0 && a0 < 16 * IN + r.Grip) hits.Add($"{st.Parts[i].Mark} {(a0 / IN - 8):F2}..{(a1 / IN - 8):F2}");
        }
        Print($"rivet in {t.Mark} grip {r.Grip / IN:F3} dir ({d.X:F2},{d.Y:F2},{d.Z:F2}): " + string.Join(" | ", hits));
        if (++shown >= cnt) break;
    }
    if (shown >= cnt) break;
}
