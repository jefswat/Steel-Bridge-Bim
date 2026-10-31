// Agent review: dump every longitudinal tendon of the Bonner Unit 5 segmental box in BOX coordinates (station S, lateral X right +, depth Y below the
// deck top WP, mapped back through the generator's chord frames) + segments + check rows + diagnostics. Read-only.
using System.Globalization;
using System.IO;
using SBM.Core.Geometry;
var outDir = @"C:\SBM\qc\bonner-unit5\pt-profiles\work";
var br = Doc.Project.Bridges.First(b => b.Segmental != null);
var sm = Doc.Segmental(br.Name);
var al = Doc.Framing(br.Name).Alignment;
var d = sm.Definition;
var ci = CultureInfo.InvariantCulture;
double CrossSlope(double sta) { double w = d.Box.TopWidth / 2; double dr = al.CrossSlope.DeltaElevation(sta, w), dl = al.CrossSlope.DeltaElevation(sta, -w); return (dr - dl) / (2 * w); }
(Vec3 O, Vec3 Lat, Vec3 Up, Vec3 T) Fr(double sta)
{
    double dist = al.StationToDistance(sta);
    var (p, ang) = al.EvaluateAtDistance(dist);
    var tan = Vec2.FromAngle(ang); var right = tan.Right;
    double g = al.Profile.IsEmpty ? 0 : al.Profile.GradeAt(sta);
    var T = new Vec3(tan.X, tan.Y, g).Normalized();
    var L = new Vec3(right.X, right.Y, CrossSlope(sta)).Normalized();
    var lat = (L - T * Vec3.Dot(L, T)).Normalized();
    var up = Vec3.Cross(lat, T).Normalized();
    var o = p.WithZ(al.ElevationAt(sta, 0));
    foreach (var r in sm.Segments)
        if (sta >= r.Station0 - 1e-9 && sta <= r.Station1 + 1e-9) { double u = (sta - r.Station0) / (r.Station1 - r.Station0); o = Vec3.Lerp(r.Top0, r.Top1, u); break; }
    return (o, lat, up, T);
}
double F(Vec3 P, double s) { var f = Fr(s); return Vec3.Dot(P - f.O, f.T); }
var segs = sm.Segments.OrderBy(s => s.Station0).ToList();
using (var w = new StreamWriter(Path.Combine(outDir, "segments.csv")))
{
    w.WriteLine("name,type,span,bent,index,sta0,sta1,depth0,depth1");
    foreach (var s in segs) w.WriteLine(string.Join(",", s.Name, s.Type, s.Span, s.Bent, s.Index, s.Station0.ToString("R", ci), s.Station1.ToString("R", ci), s.Depth0.ToString("R", ci), s.Depth1.ToString("R", ci)));
}
using (var w = new StreamWriter(Path.Combine(outDir, "bents.csv")))
{
    w.WriteLine("bent,station");
    foreach (var b in sm.BentRows) w.WriteLine($"{b.Bent},{b.Station.ToString("R", ci)}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "tendon-points.csv")))
{
    w.WriteLine("tendon,group,location,strands,i,S,X,Y,res");
    foreach (var t in sm.Tendons.Where(t => t.Group != SBM.Core.Bridge.TendonGroup.Transverse))
    {
        double sPrev = double.NaN;
        for (int i = 0; i < t.Points.Count; i++)
        {
            var P = t.Points[i];
            var so = al.Inverse(new Vec2(P.X, P.Y));
            double s0 = double.IsNaN(sPrev) ? so.Station : sPrev;
            double a = s0 - 3, b = s0 + 3, fa = F(P, a), fb = F(P, b);
            int guard = 0;
            while (fa * fb > 0 && guard++ < 20) { a -= 3; b += 3; fa = F(P, a); fb = F(P, b); }
            for (int k = 0; k < 80; k++) { double m = 0.5 * (a + b), fm = F(P, m); if (fa * fm <= 0) { b = m; fb = fm; } else { a = m; fa = fm; } }
            double S = 0.5 * (a + b); sPrev = S;
            var f = Fr(S); var dv = P - f.O;
            double X = Vec3.Dot(dv, f.Lat), Y = -Vec3.Dot(dv, f.Up);
            w.WriteLine(string.Join(",", "\"" + t.Name + "\"", t.Group, "\"" + t.Location + "\"", t.Strands, i, S.ToString("R", ci), X.ToString("R", ci), Y.ToString("R", ci), Vec3.Dot(dv, f.T).ToString("E2", ci)));
        }
    }
}
using (var w = new StreamWriter(Path.Combine(outDir, "checks.tsv")))
    foreach (var c in sm.Checks) w.WriteLine($"{c.Item}\t{c.Model.ToString("R", ci)}\t{c.Plan.ToString("R", ci)}\t{c.Difference.ToString("R", ci)}\t{c.Note}");
using (var w = new StreamWriter(Path.Combine(outDir, "diagnostics.txt")))
    foreach (var g in sm.Diagnostics) w.WriteLine($"{g.Severity}: {g.Message}");
// lane table + tendon definitions as used
using (var w = new StreamWriter(Path.Combine(outDir, "lanes-def.csv")))
{
    w.WriteLine("lane,X,Depth,Height,FromSoffitCorner");
    foreach (var l in d.DuctLanes ?? new()) w.WriteLine($"{l.Name},{l.X.ToString("R", ci)},{l.Depth.ToString("R", ci)},{l.Height.ToString("R", ci)},{l.FromSoffitCorner}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "tendon-defs.csv")))
{
    w.WriteLine("name,location,group,layout,anchorDown,anchorUp,lanes");
    foreach (var t in d.Tendons) w.WriteLine($"{t.Name},\"{t.Location}\",{t.Group},{t.Layout},{t.AnchorDown},{t.AnchorUp},\"{string.Join(" ", (t.Lanes ?? new()).Select(l => l.Arm + l.Joint + ":" + l.Lane))}\"");
}
// box half soffit at depth h for the row C lanes
using (var w = new StreamWriter(Path.Combine(outDir, "box.txt")))
{
    w.WriteLine($"TopWidth {d.Box.TopWidth} WebRootX {d.Box.WebRootX}");
    foreach (var h in new[] { 2.5, 3.0, 4.0, 5.0, 6.0 }) w.WriteLine($"h {h} halfSoffit {d.Box.HalfSoffit(h)}");
}
Print($"bridge {br.Name}: {sm.Tendons.Count} tendons, {sm.Checks.Count} checks, {segs.Count} segments");
