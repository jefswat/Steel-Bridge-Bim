// Agent review 2026-11-03: every bracing member END of the main span (laterals, struts, sway, portals, floor laterals): which plates / angles touch the end zone
// and how many rivets lie in it. Ends with no connection part or no rivet = candidate missing connection (read-only, no save).
//   sbm run bracing_audit.csx "<PTC model>" --no-pipeline -- <out.csv>
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Bridge;
var inv = CultureInfo.InvariantCulture;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var st = Doc.Model(BN).Steel!;
var tm = st.Truss!;
var def = Doc.Project.Bridges[0].Truss!;
string F(double v) => v.ToString("F2", inv);
string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";
var roles = new[] { TrussMemberRole.UpperLateral, TrussMemberRole.LowerLateral, TrussMemberRole.Sway, TrussMemberRole.Portal, TrussMemberRole.Strut };
var conn = st.Parts.Where(p => p.Category is PartCategory.TrussConnectionPlate or PartCategory.TrussConnectionAngle or PartCategory.TrussGusset
    or PartCategory.FillerPlate or PartCategory.SplicePlate).Select(p => (p, b: p.Mesh.Bounds())).ToList();
var byMember = st.Parts.Where(p => p.Member != null).GroupBy(p => p.Member!).ToDictionary(g => g.Key, g => g.ToList());
var rivets = st.Bolts.Select(r => r.Frame.Origin).ToList();
var w = new StreamWriter(Args[0]);
w.WriteLine("member,role,system,from,to,end,point,gap_in,zone_len_in,n_conn,n_rivets,conn_parts,flag");
int nFlag = 0;
foreach (var m in tm.Members.Where(x => roles.Contains(x.Role)))
{
    var dm = def.Members.FirstOrDefault(d => d.Name == m.Name);
    string sys = dm?.System ?? "";
    if (!byMember.TryGetValue(m.Mark, out var parts))
        parts = st.Parts.Where(p => p.Mark == m.Mark || p.Mark.StartsWith(m.Mark + "-")).Where(p => p.Category == PartCategory.TrussBracing || p.Category == PartCategory.TrussFloorBeam).ToList();
    var pts = parts.SelectMany(p => p.Mesh.Positions).ToList();
    if (pts.Count == 0) { w.WriteLine(string.Join(",", Q(m.Mark), m.Role, Q(sys), Q(m.From), Q(m.To), "-", "", "", "", 0, 0, "", "NOT_BUILT")); continue; }
    var ax = (m.B - m.A).Normalized(); double L = (m.B - m.A).Length;
    double tmin = pts.Min(q => Vec3.Dot(q - m.A, ax)), tmax = pts.Max(q => Vec3.Dot(q - m.A, ax));
    foreach (var (end, tEnd, pt, gap) in new[] { ("A", tmin, m.From, tmin), ("B", tmax, m.To, L - tmax) })
    {
        double zl = 0.6;   // the last 0.6 m of the member at this end
        var zone = pts.Where(q => { double t = Vec3.Dot(q - m.A, ax); return end == "A" ? t <= tEnd + zl : t >= tEnd - zl; }).ToList();
        double e = 0.05;
        var lo = new Vec3(zone.Min(q => q.X) - e, zone.Min(q => q.Y) - e, zone.Min(q => q.Z) - e);
        var hi = new Vec3(zone.Max(q => q.X) + e, zone.Max(q => q.Y) + e, zone.Max(q => q.Z) + e);
        var hits = conn.Where(c => c.b.Min.X <= hi.X && c.b.Max.X >= lo.X && c.b.Min.Y <= hi.Y && c.b.Max.Y >= lo.Y && c.b.Min.Z <= hi.Z && c.b.Max.Z >= lo.Z)
            .Select(c => c.p.Mark).Distinct().ToList();
        double r = 0.02;
        int nr = rivets.Count(o => o.X >= lo.X + e - r && o.X <= hi.X - e + r && o.Y >= lo.Y + e - r && o.Y <= hi.Y - e + r && o.Z >= lo.Z + e - r && o.Z <= hi.Z - e + r);
        string flag = hits.Count == 0 ? "NO_CONNECTION_PART" : nr == 0 ? "NO_RIVETS" : gap > 2.0 ? "END_FAR_FROM_WP" : "";
        if (flag.Length > 0) nFlag++;
        w.WriteLine(string.Join(",", Q(m.Mark), m.Role, Q(sys), Q(m.From), Q(m.To), end, Q(pt), F(gap / 0.0254), F(zl / 0.0254), hits.Count, nr, Q(string.Join(" ", hits.Take(8))), flag));
    }
}
w.Close();
Print($"done, {nFlag} flagged ends");
