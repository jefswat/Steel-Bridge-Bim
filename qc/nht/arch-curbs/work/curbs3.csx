// pass 3: curb ends vs the end web (horizontal rays along the alignment), bar end clearances, end web top vs grade.
using SBM.Core.Geometry;
using SBM.Core.Solids;

var br = Doc.Project.Bridges.First(b => b.Arch != null);
var bm = Doc.Model(br.Name);
var am = bm.Steel!.Arch!;
var al = Doc.Alignment(br.AlignmentName);
double IN(double m) => m / 0.0254;
double FT(double m) => m / 0.3048;
string S(double sta) { double f = FT(sta); int h = (int)Math.Floor(f / 100); return $"{h}+{f - 100 * h:00.00}"; }
double G(double sta, double off) => br.Deck.FinishedElevation(al, sta, off);
double Ray(Mesh m, Vec3 o, Vec3 d)
{
    double best = double.MaxValue;
    for (int i = 0; i < m.Indices.Count; i += 3)
    {
        var a = m.Positions[m.Indices[i]]; var b = m.Positions[m.Indices[i + 1]]; var c = m.Positions[m.Indices[i + 2]];
        var e1 = b - a; var e2 = c - a; var p = Vec3.Cross(d, e2); double det = Vec3.Dot(e1, p);
        if (Math.Abs(det) < 1e-14) continue;
        var t0 = o - a; double u = Vec3.Dot(t0, p) / det; if (u < 0 || u > 1) continue;
        var q = Vec3.Cross(t0, e1); double v = Vec3.Dot(d, q) / det; if (v < 0 || u + v > 1) continue;
        double t = Vec3.Dot(e2, q) / det; if (t > -1e-9 && t < best) best = t;
    }
    return best;
}
var ews = am.Steel.Parts.Where(p => p.Mark.EndsWith("-EW")).ToDictionary(p => p.Mark.Contains("WEST") ? "west" : "east", p => p.Mesh);
var curbs = am.Curbs!;
foreach (var c in br.Arch!.Deck!.Curbs)
{
    int side = Math.Sign(c.InnerOffset);
    var cv = curbs.Positions.Select(q => (q, so: al.Inverse(q.XY))).Where(v => Math.Sign(v.so.Offset) == side).ToList();
    foreach (var end in new[] { "west", "east" })
    {
        int dir = end == "west" ? -1 : 1;
        foreach (double off in new[] { c.InnerOffset, c.InnerOffset + side * c.Width / 2, c.InnerOffset + side * c.Width })
        {
            // curb end station at this offset: extreme station among vertices within 3 mm of the offset
            var at = cv.Where(v => Math.Abs(v.so.Offset - off) < 0.003).ToList();
            double sta; Vec3 p0;
            if (at.Count > 0) { var e = dir < 0 ? at.MinBy(v => v.so.Station) : at.MaxBy(v => v.so.Station); sta = e.so.Station; }
            else
            {   // mid-curb: interpolate between the inner and outer end stations
                var i0 = cv.Where(v => Math.Abs(v.so.Offset - c.InnerOffset) < 0.003); var o0 = cv.Where(v => Math.Abs(v.so.Offset - (c.InnerOffset + side * c.Width)) < 0.003);
                sta = dir < 0 ? (i0.Min(v => v.so.Station) + o0.Min(v => v.so.Station)) / 2 : (i0.Max(v => v.so.Station) + o0.Max(v => v.so.Station)) / 2;
            }
            var pp = al.PointAt(sta, off); var tg = (al.PointAt(sta + 0.01, off) - pp).Normalized() * dir;
            double g = G(sta, off);
            foreach (double h in new[] { 0.0127, c.Height - 0.0127 })
            {
                var o = pp.WithZ(g + h) - new Vec3(tg.X, tg.Y, 0) * 0.02;
                double t = Ray(ews[end], o, new Vec3(tg.X, tg.Y, 0));
                Print($"{c.Name} {end} end, off {FT(off):F3} ft, end sta {S(sta)}, {IN(h):F1}\" above grade: gap curb end -> end web along the alignment {(t == double.MaxValue ? "no hit" : IN(t - 0.02).ToString("F3") + " in")}");
            }
        }
        // end web top above grade at this curb (vertical ray down onto the EW from above the curb)
        double offm = c.InnerOffset + side * c.Width / 2;
        var dd = cv.Where(v => Math.Abs(v.so.Offset - c.InnerOffset) < 0.003);
        double se = dir < 0 ? dd.Min(v => v.so.Station) : dd.Max(v => v.so.Station);
        double topRel = double.NaN;
        for (double s = se - 0.15; s <= se + 0.15; s += 0.005)
        {
            var q = al.PointAt(s, offm); double z0 = G(s, offm) + 1;
            double t = Ray(ews[end], q.WithZ(z0), new Vec3(0, 0, -1));
            if (t < double.MaxValue) { double r = 1 - t; if (double.IsNaN(topRel) || r > topRel) topRel = r; }
        }
        Print($"{c.Name} {end}: end web top - finished grade at mid-curb = {(double.IsNaN(topRel) ? "no EW under" : IN(topRel).ToString("F2") + " in")}");
    }
    // bar ends nearest the curb ends
    foreach (var mk in new[] { "S1", "D4" })
    {
        var bars = am.Concrete.Bars.Where(b => b.Mark == mk && Math.Sign(al.Inverse(b.Points[0].XY).Offset) == side).ToList();
        var all = bars.SelectMany(b => b.Points).Select(p => (p, so: al.Inverse(p.XY))).ToList();
        foreach (var end in new[] { "west", "east" })
        {
            int dir = end == "west" ? -1 : 1;
            var e = dir < 0 ? all.MinBy(v => v.so.Station) : all.MaxBy(v => v.so.Station);
            var tg = (al.PointAt(e.so.Station + 0.01, e.so.Offset) - al.PointAt(e.so.Station, e.so.Offset)).Normalized() * dir;
            double t = Ray(ews[end], e.p, new Vec3(tg.X, tg.Y, 0));
            Print($"{c.Name} {mk} {end}-most point {S(e.so.Station)} off {FT(e.so.Offset):F3}: CL to end web along the alignment {IN(t):F2} in (clear {IN(t - (mk == "S1" ? 0.00635 : 0.0079)):F2} in)");
        }
    }
}
// S1 spacing along the curb line (inner face) - first / last stirrup to the curb ends at the inner face
