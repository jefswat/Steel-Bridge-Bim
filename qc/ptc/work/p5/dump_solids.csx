// QC pass 4 (read-only): every steel part's exact extrusion solids of the PTC truss / girder bridges, in a frame of the bridge:
// x = horizontal normal to the (straight) alignment, measured from the truss / girder plane the part is nearest to (+ = away from the bridge CL),
// sta = station along the alignment (ft), z = elevation (ft). Per solid: x / sta / z ranges (in / ft / ft) + the member-axis local y range for members.
//   sbm run dump_solids.csx ptc.sbm -- out.csv
using System.IO;
using System.Globalization;
using SBM.Core.Steel;
using SBM.Core.Geometry;
var inv = CultureInfo.InvariantCulture;
string outPath = Args.Length > 0 ? Args[0] : "solids.csv";
Doc.Regenerate();
var w = new StreamWriter(outPath);
w.WriteLine("bridge,plane,mark,category,assembly,desc,solid,xin0,xin1,sta0,sta1,z0,z1,yin0,yin1,along0,along1,mass_kg");
double IN = 0.0254, FT = 0.3048;
foreach (var b in Doc.Project.Bridges)
{
    if (b.Truss == null) continue;
    var bm = Doc.Model(b.Name);
    var tm = bm.Steel?.Truss;
    if (tm == null) { Print("no truss model " + b.Name); continue; }
    // alignment direction from two points of the same plane far apart
    var pts = tm.Points;
    var p0 = pts.OrderBy(p => p.Station).First();
    var chordEnds = tm.Members.Where(m => m.Plane != "" && m.Plane == tm.Members.First(q => q.Plane != "").Plane && (m.Role.ToString().Contains("LowerChord") || m.Role.ToString() == "Girder")).SelectMany(m => new[] { m.A, m.B }).ToList();
    var ca = chordEnds.OrderBy(p => Vec3.Dot(p - p0.World, new Vec3(1, 0, 0))).First(); var cb = chordEnds.OrderBy(p => Vec3.Dot(p - p0.World, new Vec3(1, 0, 0))).Last();
    var t = new Vec3(cb.X - ca.X, cb.Y - ca.Y, 0); if (Vec3.Dot(t, new Vec3(1,0,0)) < 0) t = t * -1; t = t * (1 / Math.Sqrt(t.X * t.X + t.Y * t.Y));
    var xh = new Vec3(t.Y, -t.X, 0);   // right of the alignment
    var planes = tm.Members.Where(m => m.Plane != "" && (m.Role.ToString().Contains("Chord") || m.Role.ToString() == "Girder")).GroupBy(m => m.Plane).ToDictionary(g => g.Key, g => g.Average(m => Vec3.Dot(m.A - p0.World, xh)));
    double cl = planes.Values.Average();
    Print($"{b.Name}: planes " + string.Join(", ", planes.Select(kv => $"{kv.Key}={(kv.Value - cl) / FT:F3} ft")));
    var mem = tm.Members.ToDictionary(m => m.Mark, m => m, StringComparer.OrdinalIgnoreCase);
    foreach (var part in bm.Steel!.Parts)
    {
        var sol = part.Mesh.Solids;
        if (sol == null || sol.Count == 0) continue;
        mem.TryGetValue(part.Mark, out var mr);
        Vec3 ax = default, yax = default; if (mr != null) { ax = (mr.B - mr.A); ax = ax * (1 / ax.Length); var xp = Vec3.Cross(ax, new Vec3(0, 0, 1)); if (xp.Length < 1e-6) xp = xh; xp = xp * (1 / xp.Length); yax = Vec3.Cross(xp, ax); if (yax.Z < 0) yax = yax * -1; }
        int k = 0;
        foreach (var s in sol)
        {
            var wp = new List<Vec3>();
            foreach (var q in s.Outline) { var a = s.Frame.ToWorld(q.X, q.Y, 0); wp.Add(a); wp.Add(a + s.Extrusion); }
            var xs = wp.Select(p => Vec3.Dot(p - p0.World, xh)).ToList();
            double xm = xs.Average();
            var pl = planes.OrderBy(kv => Math.Abs(kv.Value - xm)).First();
            double sg = pl.Value >= cl ? 1 : -1;
            var xr = xs.Select(x => (x - pl.Value) * sg / IN).ToList();
            var st = wp.Select(p => (p0.Station + Vec3.Dot(p - p0.World, t)) / FT).ToList();
            var zz = wp.Select(p => p.Z / FT).ToList();
            string yr = ",", ar = ",";
            if (mr != null)
            {
                var ys = wp.Select(p => Vec3.Dot(p - mr.A, yax) / IN).ToList(); var al = wp.Select(p => Vec3.Dot(p - mr.A, ax) / IN).ToList();
                yr = $"{ys.Min().ToString("F3", inv)},{ys.Max().ToString("F3", inv)}"; ar = $"{al.Min().ToString("F2", inv)},{al.Max().ToString("F2", inv)}";
            }
            w.WriteLine(string.Join(",", b.Name, pl.Key, part.Mark, part.Category, part.Assembly, "\"" + (part.Description ?? "").Replace("\"", "'") + "\"", k,
                xr.Min().ToString("F3", inv), xr.Max().ToString("F3", inv), st.Min().ToString("F3", inv), st.Max().ToString("F3", inv), zz.Min().ToString("F3", inv), zz.Max().ToString("F3", inv), yr, ar,
                k == 0 ? part.Mass.ToString("F1", inv) : ""));
            k++;
        }
    }
}
w.Close();
Print("written " + outPath);
