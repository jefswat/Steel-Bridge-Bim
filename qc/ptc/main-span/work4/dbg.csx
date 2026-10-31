// QC gusset LAYERING of the main span (read-only, NO save): regenerate only "PTC Arch truss 18-20" in memory, then per joint x plane x face:
//   gusset parts (thickness, offset band from the work point along the plane normal), ray-cast stack profile over the gusset area (3 in. grid):
//   gaps / overlaps between layers, between the inner gusset and the steel under it, steel outside the outer layer; rivets through the stack
//   (head on the outer face? grip = contiguous steel?).
//   sbm run layers.csx "<PTC model>" --no-pipeline -- <outdir>
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using SBM.Core.Geometry;
using SBM.Core.Steel;
var inv = CultureInfo.InvariantCulture;
string outDir = Args[0];
Directory.CreateDirectory(outDir);
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
// batch 1 toggles (in memory only, never saved): MODE env "after" = the batch 1 inputs applied to the loaded model
if (Environment.GetEnvironmentVariable("MODE") == "after")
{
    var tt = Doc.Project.Bridges[0].Truss!;
    tt.JointRivetGripsFromSteel = true;
    foreach (var nm in new[] { "STRUT", "LAT31", "LAT50", "FLAT21", "L13-W14" }) { var sd = tt.Section(nm)!; sd.Rotated = true; if (nm == "FLAT21") sd.BackToBack = 0.0254 * 13.625; }
    var rb = new List<int> { 2, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 2 };
    foreach (var l in tt.Floor!.Stringers) l.ContinuousBays = rb.ToList();
    Print("MODE after applied");
}
var sw = System.Diagnostics.Stopwatch.StartNew();
Doc.Regenerate();
Print($"regen {sw.Elapsed.TotalSeconds:0.0}s");
var m = Doc.Model(BN);
var st = m.Steel!;
var tm = st.Truss!;
const double IN = 0.0254;
string F(double x) => x.ToString("F3", inv);
string Q(string s) => "\"" + (s ?? "").Replace("\"", "'").Replace("\n", " ") + "\"";

// part bounds
var parts = st.Parts;
var pb = parts.Select(p => p.Mesh.Bounds()).ToArray();
var rivets = st.Bolts.Where(b => b.Kind == BoltKind.Rivet).ToList();


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


var P = tm.Points.First(x => x.Plane == "N" && x.Name == "L14").World;
var near = rivets.Where(b => (b.Frame.Origin - P).Length < 3.0 && Math.Abs(b.Grip - 3.0 * IN) < 1e-4).Take(4).ToList();
foreach (var b in near)
{
    var o = b.Frame.Origin; var d = b.Frame.Z;
    Print($"rivet {F((o-P).X/IN)},{F((o-P).Y/IN)},{F((o-P).Z/IN)} grip {F(b.Grip/IN)} dia {F(b.Diameter/IN)} asm {b.Assembly}");
    for (int i = 0; i < parts.Count; i++)
    {
        var bb = pb[i];
        if (bb.Max.X < o.X - 0.3 || bb.Min.X > o.X + 0.3 || bb.Max.Y < o.Y - 0.3 || bb.Min.Y > o.Y + 0.3 || bb.Max.Z < o.Z - 0.3 || bb.Min.Z > o.Z + 0.3) continue;
        var ms = parts[i].Mesh; var hs = new List<(double t, int s)>();
        for (int k = 0; k < ms.Indices.Count; k += 3)
        {
            var a = ms.Positions[ms.Indices[k]]; var bq = ms.Positions[ms.Indices[k + 1]]; var c = ms.Positions[ms.Indices[k + 2]];
            if (Hit(o + new Vec3(1.37e-6, -0.83e-6, 1.11e-6), d, a, bq, c, out double t) && Math.Abs(t) < 0.3) hs.Add((t, Vec3.Dot(Vec3.Cross(bq - a, c - a), d) < 0 ? 1 : -1));
        }
        if (hs.Count > 0) Print($"   {parts[i].Mark} {parts[i].Category}: " + string.Join(" ", hs.OrderBy(h => h.t).Select(h => $"{(h.s > 0 ? "+" : "-")}{F(h.t / IN)}")));
    }
}
