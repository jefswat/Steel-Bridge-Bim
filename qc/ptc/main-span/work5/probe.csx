// QC batch 5 probe (read-only): vertical stack at a few plan points near a joint: which parts a vertical line passes, z ranges (in. relative to the point)
//   sbm run probe.csx "<model>" --no-pipeline -- <point e.g. N:L5> [radius_ft]
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var st = Doc.Model(BN).Steel!; var tm = st.Truss!;
var spec = Args[0].Split(':');
var P = tm.Points.First(p => p.Plane == spec[0] && p.Name == spec[1]).World;
double R = (Args.Length > 1 ? double.Parse(Args[1]) : 4) * 0.3048;
Print($"point {Args[0]} at {P.X:F2},{P.Y:F2},{P.Z:F3}");
foreach (var mm in tm.Members.Where(m => (m.A - P).Length < 0.01 || (m.B - P).Length < 0.01)) Print($"  member {mm.Mark} {mm.Role} {mm.Section} A={(mm.A - P).Length:F2} B={(mm.B - P).Length:F2}");
var near = st.Parts.Where(p => p.Mesh is { IsEmpty: false } && p.Mesh.Bounds() is var b && b.Min.X < P.X + R && b.Max.X > P.X - R && b.Min.Y < P.Y + R && b.Max.Y > P.Y - R && b.Min.Z < P.Z + R && b.Max.Z > P.Z - R).ToList();
// samples: points at offsets (given in ft from P in world X/Y) along each member axis at 2 / 3 ft
var dirs = tm.Members.Where(m => (m.A - P).Length < 0.01 || (m.B - P).Length < 0.01).Select(m => ((m.A - P).Length < 0.01 ? m.B - m.A : m.A - m.B).Normalized()).ToList();
foreach (var d in dirs)
    foreach (double s in new[] { 2.0, 3.0 })
    {
        var q = P + d * (s * 0.3048);
        var hits = new List<(double z0, double z1, string m)>();
        foreach (var p in near)
        {
            var ts = new List<double>(); var V = p.Mesh.Positions; var I = p.Mesh.Indices;
            var o = new Vec3(q.X, q.Y, P.Z - 3); var dz = new Vec3(0, 0, 1);
            for (int k = 0; k + 2 < I.Count; k += 3)
            {
                var a = V[I[k]]; var bq = V[I[k + 1]]; var c = V[I[k + 2]];
                var e1 = bq - a; var e2 = c - a; var h = Vec3.Cross(dz, e2); double det = Vec3.Dot(e1, h); if (Math.Abs(det) < 1e-14) continue;
                double f = 1 / det; var sv = o - a; double uu = f * Vec3.Dot(sv, h); if (uu < 0 || uu > 1) continue; var qq = Vec3.Cross(sv, e1); double vv = f * Vec3.Dot(dz, qq); if (vv < 0 || uu + vv > 1) continue;
                ts.Add(f * Vec3.Dot(e2, qq));
            }
            ts.Sort(); var u2 = new List<double>(); foreach (var t in ts) if (u2.Count == 0 || t - u2[^1] > 1e-6) u2.Add(t);
            for (int k = 0; k + 1 < u2.Count; k += 2) hits.Add((o.Z + u2[k] - P.Z, o.Z + u2[k + 1] - P.Z, p.Mark));
        }
        Print($"  dir ({d.X:F2},{d.Y:F2},{d.Z:F2}) at {s} ft: " + string.Join(" | ", hits.OrderBy(h => h.z0).Select(h => $"{h.m} {h.z0 / 0.0254:F2}..{h.z1 / 0.0254:F2}")));
    }
