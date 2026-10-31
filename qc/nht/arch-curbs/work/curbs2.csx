// Agent review of the NHT arch curbs, pass 2: curb ends vs end web, bars near steel, railing ends, mesh sections.
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

var br = Doc.Project.Bridges.First(b => b.Arch != null);
var bm = Doc.Model(br.Name);
var am = bm.Steel!.Arch!;
var al = Doc.Alignment(br.AlignmentName);
double IN(double m) => m / 0.0254;
double FT(double m) => m / 0.3048;
string S(double sta) { double f = FT(sta); int h = (int)Math.Floor(f / 100); return $"{h}+{f - 100 * h:00.00}"; }
double G(double sta, double off) => br.Deck.FinishedElevation(al, sta, off);

double PtTri(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
{
    var ab = b - a; var ac = c - a; var ap = p - a;
    double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
    if (d1 <= 0 && d2 <= 0) return (p - a).Length;
    var bp = p - b; double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
    if (d3 >= 0 && d4 <= d3) return (p - b).Length;
    double vc = d1 * d4 - d3 * d2;
    if (vc <= 0 && d1 >= 0 && d3 <= 0) { double v = d1 / (d1 - d3); return (p - (a + ab * v)).Length; }
    var cp = p - c; double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
    if (d6 >= 0 && d5 <= d6) return (p - c).Length;
    double vb = d5 * d2 - d1 * d6;
    if (vb <= 0 && d2 >= 0 && d6 <= 0) { double w = d2 / (d2 - d6); return (p - (a + ac * w)).Length; }
    double va = d3 * d6 - d5 * d4;
    if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) { double w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return (p - (b + (c - b) * w)).Length; }
    double den = 1 / (va + vb + vc); double vv = vb * den, ww = vc * den;
    return (p - (a + ab * vv + ac * ww)).Length;
}
double MeshDist(Mesh m, Vec3 p) { double best = 1e9; for (int i = 0; i < m.Indices.Count; i += 3) best = Math.Min(best, PtTri(p, m.Positions[m.Indices[i]], m.Positions[m.Indices[i + 1]], m.Positions[m.Indices[i + 2]])); return best; }

var curbs = am.Curbs!;
// ----- end web parts: top elevation vs grade at the curb, distance of the curb end corners (bottom + top)
var ews = am.Steel.Parts.Where(p => p.Mark.EndsWith("-EW")).ToList();
foreach (var ew in ews)
{
    Print($"== {ew.Mark}: {ew.Description}");
    foreach (var side in new[] { -1, 1 })
    {
        var c = br.Arch.Deck!.Curbs.First(x => Math.Sign(x.InnerOffset) == side);
        // EW vertices near this curb
        var near = ew.Mesh.Positions.Select(q => (q, so: al.Inverse(q.XY))).Where(v => Math.Abs(v.so.Offset - (c.InnerOffset + side * c.Width / 2)) < 0.4).ToList();
        if (near.Count == 0) { Print($"   no EW vertex near {c.Name}"); continue; }
        double topRel = near.Max(v => v.q.Z - G(v.so.Station, v.so.Offset));
        Print($"   near {c.Name}: EW top - finished grade = {IN(topRel):F2} in (max over vertices within 0.4 m of mid-curb)");
        // curb end corners at this EW
        var cv = curbs.Positions.Select(q => (q, so: al.Inverse(q.XY))).Where(v => Math.Sign(v.so.Offset) == side).ToList();
        double mid = cv.Average(v => v.so.Station);
        bool west = ew.Mark.Contains("WEST");
        var endv = cv.Where(v => west ? v.so.Station < mid : v.so.Station > mid).OrderBy(v => west ? v.so.Station : -v.so.Station).Take(12).ToList();
        foreach (var v in endv.Where(v => Math.Abs(Math.Abs(v.so.Offset) - Math.Abs(c.InnerOffset)) < 0.003 || Math.Abs(Math.Abs(v.so.Offset) - Math.Abs(c.InnerOffset) - c.Width) < 0.003).Take(8))
        {
            double d = MeshDist(ew.Mesh, v.q);
            Print($"   curb corner {S(v.so.Station)} off {FT(v.so.Offset):F3} z-grade {IN(v.q.Z - G(v.so.Station, v.so.Offset)):F2}: dist to {ew.Mark} {IN(d):F3} in");
        }
    }
}
// ----- slab ends at the curb lines: station of the slab-ends mesh extreme vertices at the curb offsets (for comparison)
foreach (var side in new[] { -1, 1 })
{
    var c = br.Arch.Deck!.Curbs.First(x => Math.Sign(x.InnerOffset) == side);
    var sv = am.SlabEnds!.Positions.Select(q => (q, so: al.Inverse(q.XY))).Where(v => Math.Abs(v.so.Offset - c.InnerOffset) < 0.01 && Math.Abs(v.q.Z - G(v.so.Station, v.so.Offset)) < 0.005).ToList();
    if (sv.Count > 0) Print($"slab-end top vertices at {c.Name} inner line: stations {string.Join(", ", sv.Select(v => S(v.so.Station)).Distinct().OrderBy(x => x).Take(3))} .. {string.Join(", ", sv.Select(v => S(v.so.Station)).Distinct().OrderBy(x => x).TakeLast(3))}");
}
// ----- bars close to steel
var steelParts = am.Steel.Parts.Where(p => !p.Mark.StartsWith("RAIL")).ToList();
var boxes = steelParts.Select(p => (p, b: p.Mesh.Bounds())).ToList();
foreach (var mk in new[] { "S1", "D4" })
{
    var hits = new Dictionary<string, (double d, int n, string where)>();
    foreach (var b in am.Concrete.Bars.Where(x => x.Mark == mk))
        foreach (var p in b.Points)
            foreach (var (sp, bx) in boxes)
            {
                if (p.X < bx.Min.X - 0.03 || p.X > bx.Max.X + 0.03 || p.Y < bx.Min.Y - 0.03 || p.Y > bx.Max.Y + 0.03 || p.Z < bx.Min.Z - 0.03 || p.Z > bx.Max.Z + 0.03) continue;
                double d = MeshDist(sp.Mesh, p);
                if (d > 0.0254) continue;
                var so = al.Inverse(p.XY);
                string w = $"{S(so.Station)} off {FT(so.Offset):F3} z-grade {IN(p.Z - G(so.Station, so.Offset)):F2}";
                if (!hits.TryGetValue(sp.Mark, out var h) || d < h.d) hits[sp.Mark] = (d, (hits.TryGetValue(sp.Mark, out var h0) ? h0.n : 0) + 1, w);
                else hits[sp.Mark] = (h.d, h.n + 1, h.where);
            }
    foreach (var kv in hits.OrderBy(k => k.Value.d)) { var sp = steelParts.First(p => p.Mark == kv.Key); Print($"{mk} within 1\" of {kv.Key} ({sp.Category}, {sp.Description}): min {IN(kv.Value.d):F3} in at {kv.Value.where}, {kv.Value.n} pts; part top-grade {IN(sp.Mesh.Bounds().Max.Z - G(al.Inverse(((sp.Mesh.Bounds().Min + sp.Mesh.Bounds().Max) * 0.5).XY).Station, al.Inverse(((sp.Mesh.Bounds().Min + sp.Mesh.Bounds().Max) * 0.5).XY).Offset)):F2} in"); }
}
// ----- railing run ends
foreach (var run in bm.Steel.Railings?.Runs ?? new())
{
    int side = Math.Sign(run.Offset);
    var c = br.Arch.Deck!.Curbs.First(x => Math.Sign(x.InnerOffset) == side);
    foreach (var q in run.Line.Take(3).Concat(run.Line.TakeLast(3)))
    {
        var so = al.Inverse(q.XY);
        Print($"rail {run.Name} base {S(so.Station)} off {FT(so.Offset):F3}: z - (grade at curb line + 4\") = {IN(q.Z - G(so.Station, c.InnerOffset) - c.Height):F2} in");
    }
}
// ----- curb section check at a few stations (vertical cut of the curb mesh at the radial line): polygon via vertices near the station
