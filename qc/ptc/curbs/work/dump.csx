// Agent review of the PTC curbs: dump curb / parapet geometry, deck relation, railing, steel intrusions. Read-only (no Save).
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Solids;
using System.IO;

var outp = new StringWriter();
void P(string s) { outp.WriteLine(s); Print(s); }
var names = new[] { "PTC Girder spans 1-10", "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC Arch truss 18-20", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27", "PTC Girder spans 28-31" };
var rep = Doc.Regenerate();
const double CY = 0.764554858;
string St(double s) => $"{(int)(s / 0.3048 / 100)}+{(s / 0.3048) % 100:00.000}";
double ftIn(double m) => m / 0.0254;
bool Inside(List<Vec2> poly, Vec2 p)
{
    bool c = false;
    for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
    return c;
}
double totCurb = 0, totWing = 0;
foreach (var n in names)
{
    var def = Doc.Project.Bridges.First(b => b.Name == n);
    var al = Doc.Project.Alignments.First(a => a.Name == def.AlignmentName);
    var m = Doc.Model(n);
    var c = m.Concrete;
    var dk = def.Truss!.Floor!.Deck!;
    P($"=== {n}: deck {St(dk.BeginStation)} .. {St(dk.EndStation)} L={(dk.EndStation - dk.BeginStation) / 0.3048:0.000} ft, deck L/R {dk.LeftOffset / 0.3048:0.000}/{dk.RightOffset / 0.3048:0.000} ft, slab {c?.DeckVolume / CY:0.0} CY, thick {c?.HaunchVolume / CY:0.0} CY");
    foreach (var li in def.Truss.LineItems!.Where(l => l.Kind == TrussLineItemKind.Concrete))
        P($"  item {li.Name}: ref {li.ReferenceOffset / 0.3048:0.000} side {li.Side} {St(li.From)}..{St(li.To)} L={(li.To - li.From) / 0.3048:0.000} ft FromGrade={li.FromGrade} Rise={li.Rise} profile=" + string.Join(" ", li.Profile.Select(q => $"({ftIn(q.U):0.###},{ftIn(q.V):0.###})")));
    foreach (var (bn, mesh) in c!.Barriers)
    {
        var bb = mesh.Bounds();
        double v = Math.Abs(mesh.Volume());
        if (bn.Contains("abutment")) totWing += v; else totCurb += v;
        P($"  barrier {bn}: {v / CY:0.00} CY, tris={mesh.TriangleCount}, z {bb.Min.Z:0.000}..{bb.Max.Z:0.000}");
    }
    // section at a few stations: grade at gutter, at outer face; deck top check
    foreach (double f in new[] { 0.02, 0.5, 0.98 })
    {
        double s = dk.BeginStation + f * (dk.EndStation - dk.BeginStation);
        double g = al.ElevationAt(s, 38.25 * 0.3048), go = al.ElevationAt(s, 38.25 * 0.3048 + 34 * 0.0254), gl = al.ElevationAt(s, -38.25 * 0.3048), gcl = al.ElevationAt(s, 0);
        P($"    sta {St(s)}: CL {gcl:0.0000} gutterR {g:0.0000} gutterL {gl:0.0000} (CL-gutter {ftIn(gcl - g):0.000} in = {ftIn(gcl - g) / 38.25:0.0000} in/ft), outer face R drop from gutter {ftIn(g - go):0.000} in");
    }
    // steel vertices inside the curb / parapet section
    var polys = def.Truss.LineItems!.Where(l => l.Kind == TrussLineItemKind.Concrete).Select(l => (l, l.Profile.Select(q => new Vec2(q.U, q.V)).ToList())).ToList();
    var hits = new Dictionary<string, (int n, double maxPen, string where)>();
    if (m.Steel != null)
        foreach (var part in m.Steel.Parts)
        {
            if (part.Mesh.IsEmpty) continue;
            var pb = part.Mesh.Bounds();
            var ctr = al.Inverse(new Vec2((pb.Min.X + pb.Max.X) / 2, (pb.Min.Y + pb.Max.Y) / 2));
            double gz = al.ElevationAt(ctr.Station, 0);
            if (pb.Max.Z < gz - 1.5) continue;
            // quick reject: corners all within |off| < 37 ft
            var cs = new[] { new Vec2(pb.Min.X, pb.Min.Y), new Vec2(pb.Max.X, pb.Min.Y), new Vec2(pb.Min.X, pb.Max.Y), new Vec2(pb.Max.X, pb.Max.Y) }.Select(q => al.Inverse(q).Offset).ToList();
            if (cs.All(o => Math.Abs(o) < 37 * 0.3048) && Math.Abs(ctr.Offset) < 37 * 0.3048) continue;
            int cnt = 0; double pen = 0; string wh = "";
            foreach (var vtx in part.Mesh.Positions)
            {
                var io = al.Inverse(vtx.XY);
                foreach (var (l, poly) in polys)
                {
                    if (io.Station < l.From || io.Station > l.To) continue;
                    if (Math.Sign(io.Offset) != Math.Sign(l.Side)) continue;
                    double u = Math.Abs(io.Offset) - Math.Abs(l.ReferenceOffset);
                    double vv = vtx.Z - al.ElevationAt(io.Station, l.ReferenceOffset);
                    var q = new Vec2(u, vv);
                    // shrink 2 mm: count only real penetration
                    if (Inside(poly, q) && Inside(poly, new Vec2(u + 0.002, vv)) && Inside(poly, new Vec2(u - 0.002, vv)) && Inside(poly, new Vec2(u, vv + 0.002)) && Inside(poly, new Vec2(u, vv - 0.002)))
                    {
                        cnt++; double d = Math.Min(vv - (-0.01), 1); if (d > pen) { pen = d; wh = $"{St(io.Station)} u={ftIn(u):0.0} v={ftIn(vv):0.0} in ({l.Name})"; }
                    }
                }
            }
            if (cnt > 0) hits[part.Mark + " [" + part.Category + "]"] = (cnt, pen, wh);
        }
    P($"  steel parts with vertices inside the curb / parapet: {hits.Count}");
    foreach (var grp in hits.GroupBy(h => h.Key.Substring(h.Key.IndexOf('['))))
    {
        P($"    {grp.Key}: {grp.Count()} parts, e.g. " + string.Join("; ", grp.Take(4).Select(h => $"{h.Key} n={h.Value.n} at {h.Value.where}")));
    }
    // railing
    if (m.Steel?.Railings != null)
        foreach (var r in m.Steel.Railings.Runs)
            P($"  railing {r.Name}: L {r.Length / 0.3048:0.00} ft, posts {r.PostStations?.Count}, first/last post {(r.PostStations?.Count > 0 ? St(r.PostStations.First()) + " / " + St(r.PostStations.Last()) : "-")}");
    var rposts = m.Steel?.Parts.Where(p => p.Category == PartCategory.RailPost).ToList();
    if (rposts?.Count > 0)
    {
        var pb = rposts[0].Mesh.Bounds(); var io = al.Inverse(new Vec2((pb.Min.X + pb.Max.X) / 2, (pb.Min.Y + pb.Max.Y) / 2));
        P($"  post 1 {rposts[0].Mark}: sta {St(io.Station)} off {io.Offset / 0.3048:0.000} ft, bottom above gutter grade {ftIn(pb.Min.Z - al.ElevationAt(io.Station, Math.Sign(io.Offset) * 38.25 * 0.3048)):0.00} in, top {ftIn(pb.Max.Z - al.ElevationAt(io.Station, Math.Sign(io.Offset) * 38.25 * 0.3048)):0.00} in");
    }
    var rail = m.Steel?.Parts.FirstOrDefault(p => p.Category == PartCategory.RailHandrail || p.Category == PartCategory.RailMember);
    if (rail != null) { var rb = rail.Mesh.Bounds(); var io = al.Inverse(new Vec2((rb.Min.X + rb.Max.X) / 2, (rb.Min.Y + rb.Max.Y) / 2)); P($"  rail {rail.Mark}: top above gutter grade at mid ~ {ftIn(rb.Max.Z - al.ElevationAt(io.Station, Math.Sign(io.Offset) * 38.25 * 0.3048)):0.0} in (approx: bounds over a graded run)"); }
    // dams near the curbs
    foreach (var dp in m.Steel?.Parts.Where(p => p.Category == PartCategory.JointArmor && p.Mark.Contains("plate")).Take(2) ?? Enumerable.Empty<SteelPart>())
    { var db = dp.Mesh.Bounds(); P($"  dam part {dp.Mark}: bounds X {db.Min.X:0.00}..{db.Max.X:0.00} Y {db.Min.Y:0.00}..{db.Max.Y:0.00}"); }
    foreach (var w in (m.Steel?.Diagnostics ?? new()).Concat(c.Diagnostics ?? new()).Select(d => d.Message).Where(t => t.Contains("curb") || t.Contains("parapet")).Distinct().Take(6)) P("  diag: " + w);
}
P($"TOTAL curb + parapet {totCurb / CY:0.0} CY, abutment parapets {totWing / CY:0.0} CY");
foreach (var e in rep.Errors.Take(10)) P("ERROR " + e);
foreach (var w in rep.Warnings.Where(w => w.Contains("curb") || w.Contains("parapet")).Distinct().Take(20)) P("WARN " + w);
File.WriteAllText(@"C:\SBM\qc\ptc\curbs\work\dump.txt", outp.ToString());
