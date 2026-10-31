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

var wJ = new StreamWriter(Path.Combine(outDir, "gusset-parts.csv"));
wJ.WriteLine("plane,point,mark,layer,side,thk_in,n_in_lo,n_in_hi,area_sf,desc,flag");
var wS = new StreamWriter(Path.Combine(outDir, "stack-summary.csv"));
wS.WriteLine("plane,point,side,samples,layers,layer_gap_max_in,layer_gap_n,float_n,overlap_n,overlap_max_in,overlap_with,inner_gap_n,inner_gap_max_in,inner_nb,outer_nb,outside_steel_n");
var wP = new StreamWriter(Path.Combine(outDir, "stack-points.csv"));
wP.WriteLine("plane,point,side,a_in,b_in,stack");
var wR = new StreamWriter(Path.Combine(outDir, "rivet-summary.csv"));
wR.WriteLine("plane,point,side,rivets,head_buried_n,head_buried_max_in,head_off_n,grip_short_n,grip_long_n,grip_dmax_in,through_outer_layer_n,in_layer_area_n,miss_layer_n,grips,air_n,air_max_in");
var wRA = new StreamWriter(Path.Combine(outDir, "rivet-all.csv"));
wRA.WriteLine("plane,point,side,a_in,b_in,dia_in,grip_in,air_in,layers,stack");
var wRD = new StreamWriter(Path.Combine(outDir, "rivet-detail.csv"));
wRD.WriteLine("plane,point,side,a_in,b_in,dia_in,grip_in,chain_lo_in,chain_hi_in,chain,layers_at_point");

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

foreach (var gs in tm.GussetStacks)
{
    var pt = tm.Points.FirstOrDefault(x => x.Plane == gs.Plane && x.Name == gs.Point);
    if (pt == null) continue;
    var P = pt.World;
    var rx = new Regex("^" + Regex.Escape(gs.Plane) + "-G" + Regex.Escape(gs.Point) + "-(L|R)(\\d*)$");
    var gp = new List<(int idx, int side, int layer)>();
    for (int i = 0; i < parts.Count; i++)
    {
        if (parts[i].Category != PartCategory.TrussGusset) continue;
        var mm = rx.Match(parts[i].Mark); if (!mm.Success) continue;
        gp.Add((i, mm.Groups[1].Value == "L" ? -1 : 1, mm.Groups[2].Value.Length == 0 ? 1 : int.Parse(mm.Groups[2].Value)));
    }
    if (gp.Count == 0) continue;
    // normal from the largest triangle of the inner R gusset
    var g0 = parts[gp.First(g => g.side == 1 && g.layer == 1).idx];
    Vec3 n = new Vec3(0, 0, 1); double best = 0;
    for (int i = 0; i < g0.Mesh.Indices.Count; i += 3)
    {
        var a = g0.Mesh.Positions[g0.Mesh.Indices[i]]; var b = g0.Mesh.Positions[g0.Mesh.Indices[i + 1]]; var c = g0.Mesh.Positions[g0.Mesh.Indices[i + 2]];
        var cr = Vec3.Cross(b - a, c - a); if (cr.Length > best) { best = cr.Length; n = cr.Normalized(); }
    }
    var cen = g0.Mesh.Positions.Aggregate(new Vec3(0, 0, 0), (s, q) => s + q) * (1.0 / g0.Mesh.Positions.Count);
    if (Vec3.Dot(cen - P, n) < 0) n = -n;
    var up = new Vec3(0, 0, 1);
    var v = (up - n * Vec3.Dot(up, n)).Normalized();
    var u = Vec3.Cross(v, n).Normalized();
    foreach (var g in gp)
    {
        var p = parts[g.idx];
        double lo = p.Mesh.Positions.Min(q => Vec3.Dot(q - P, n)), hi = p.Mesh.Positions.Max(q => Vec3.Dot(q - P, n));
        var area = Regex.Match(p.Description, "\\(([0-9.]+) SF\\)");
        wJ.WriteLine(string.Join(",", gs.Plane, gs.Point, Q(p.Mark), g.layer, g.side, F((hi - lo) / IN), F(lo / IN), F(hi / IN), area.Success ? area.Groups[1].Value : "", Q(p.Description), Q(p.Assumption?.Reason ?? "")));
    }
    // candidate parts near the joint
    double R = 14 * 0.3048;
    var cand = new List<int>();
    for (int i = 0; i < parts.Count; i++)
    {
        var b = pb[i];
        if (b.Max.X < P.X - R || b.Min.X > P.X + R || b.Max.Y < P.Y - R || b.Min.Y > P.Y + R || b.Max.Z < P.Z - R || b.Min.Z > P.Z + R) continue;
        cand.Add(i);
    }
    // bucket triangles by (a, b) cell
    double cell = 6 * IN;
    var buckets = new Dictionary<(int, int), List<(int part, Vec3 a, Vec3 b, Vec3 c)>>();
    var bigTris = new List<(int part, Vec3 a, Vec3 b, Vec3 c)>();
    foreach (int i in cand)
    {
        var ms = parts[i].Mesh;
        for (int k = 0; k < ms.Indices.Count; k += 3)
        {
            var a = ms.Positions[ms.Indices[k]]; var b = ms.Positions[ms.Indices[k + 1]]; var c = ms.Positions[ms.Indices[k + 2]];
            double[] aa = { Vec3.Dot(a - P, u), Vec3.Dot(b - P, u), Vec3.Dot(c - P, u) }, bb = { Vec3.Dot(a - P, v), Vec3.Dot(b - P, v), Vec3.Dot(c - P, v) };
            int i0 = (int)Math.Floor(aa.Min() / cell), i1 = (int)Math.Floor(aa.Max() / cell), j0 = (int)Math.Floor(bb.Min() / cell), j1 = (int)Math.Floor(bb.Max() / cell);
            if ((i1 - i0 + 1) * (j1 - j0 + 1) > 4000) { bigTris.Add((i, a, b, c)); continue; }   // long member faces: tested on every ray (work3 dropped them)
            for (int ii = i0; ii <= i1; ii++) for (int jj = j0; jj <= j1; jj++)
                {
                    if (!buckets.TryGetValue((ii, jj), out var l)) buckets[(ii, jj)] = l = new();
                    l.Add((i, a, b, c));
                }
        }
    }
    // intervals along a line o + t d (t in m), per part
    List<(double t0, double t1, int part)> Intervals(Vec3 o, Vec3 d)
    {
        var w = o - P;
        var key = ((int)Math.Floor(Vec3.Dot(w, u) / cell), (int)Math.Floor(Vec3.Dot(w, v) / cell));
        var res = new List<(double, double, int)>();
        if (!buckets.TryGetValue(key, out var l0)) l0 = new();
        var l = l0.Concat(bigTris);
        // depth counting by the triangle normals (entries before exits at equal t): touching / overlapping solids of one part mesh stay one interval
        // (work3 used hit parity with de-duplication, which turned two touching plies into a gap)
        var hits = new Dictionary<int, List<(double t, int s)>>();
        foreach (var tr in l)
            if (Hit(o, d, tr.a, tr.b, tr.c, out double t))
            {
                if (!hits.TryGetValue(tr.part, out var h)) hits[tr.part] = h = new();
                h.Add((t, Vec3.Dot(Vec3.Cross(tr.b - tr.a, tr.c - tr.a), d) < 0 ? 1 : -1));
            }
        foreach (var (pi, h) in hits)
        {
            h.Sort((x, y) => x.t != y.t ? x.t.CompareTo(y.t) : y.s.CompareTo(x.s));
            int depth = 0; double start = 0;
            foreach (var (t, sg) in h)
            {
                if (sg > 0) { if (depth == 0) start = t; depth++; }
                else if (depth > 0) { depth--; if (depth == 0) res.Add((start, t, pi)); }
            }
        }
        res.Sort((x, y) => x.Item1.CompareTo(y.Item1));
        return res;
    }
    // sample region: union of the gusset parts' (a, b) extents
    double amin = 1e9, amax = -1e9, bmin = 1e9, bmax = -1e9;
    foreach (var g in gp) foreach (var q in parts[g.idx].Mesh.Positions) { double a = Vec3.Dot(q - P, u), b = Vec3.Dot(q - P, v); amin = Math.Min(amin, a); amax = Math.Max(amax, a); bmin = Math.Min(bmin, b); bmax = Math.Max(bmax, b); }
    double step = 3 * IN, jit = 0.0123 * IN;
    var gIdx = gp.ToDictionary(g => g.idx, g => g);
    int nLayers = gp.Max(g => g.layer);
    foreach (int side in new[] { -1, 1 })
    {
        int samples = 0, layerGapN = 0, floatN = 0, overlapN = 0, innerGapN = 0, outsideN = 0;
        double layerGapMax = 0, overlapMax = 0, innerGapMax = 0;
        var overlapWith = new Dictionary<string, int>(); var innerNb = new Dictionary<string, int>(); var outerNb = new Dictionary<string, int>();
        for (double a = amin + step / 2; a < amax; a += step)
            for (double b = bmin + step / 2; b < bmax; b += step)
            {
                var o = P + u * (a + jit) + v * (b + jit * 1.7);
                var iv = Intervals(o, n);
                // keep this side (t * side > 0) and convert to "outward" coordinate s = side * t
                var sideIv = iv.Select(x => side > 0 ? (s0: x.t0, s1: x.t1, part: x.part) : (s0: -x.t1, s1: -x.t0, part: x.part)).Where(x => x.s1 > 0).OrderBy(x => x.s0).ToList();
                var myG = sideIv.Where(x => gIdx.TryGetValue(x.part, out var g) && g.side == side).ToList();
                if (myG.Count == 0) continue;
                samples++;
                var stackTxt = string.Join(" | ", sideIv.Where(x => x.s0 < myG.Max(g => g.s1) + 3 * IN && x.s1 > myG.Min(g => g.s0) - 3 * IN)
                    .Select(x => $"{parts[x.part].Mark}[{(x.s0 / IN):F3}..{(x.s1 / IN):F3}]"));
                if (nLayers > 1 || samples % 4 == 0) wP.WriteLine($"{gs.Plane},{gs.Point},{side},{F(a / IN)},{F(b / IN)},{Q(stackTxt)}");
                // layer adjacency
                var byLayer = myG.ToDictionary(x => gIdx[x.part].layer, x => x);
                foreach (var (k, x) in byLayer)
                {
                    if (k < 2) continue;
                    if (byLayer.TryGetValue(k - 1, out var y))
                    {
                        double gap = x.s0 - y.s1;
                        if (Math.Abs(gap) > IN / 32) { layerGapN++; layerGapMax = Math.Max(layerGapMax, Math.Abs(gap)); }
                    }
                    else floatN++;
                }
                // overlaps involving my gussets
                bool ov = false;
                foreach (var x in myG)
                    foreach (var y in sideIv)
                    {
                        if (y.part == x.part) continue;
                        double o2 = Math.Min(x.s1, y.s1) - Math.Max(x.s0, y.s0);
                        if (o2 > IN / 32)
                        {
                            ov = true; overlapMax = Math.Max(overlapMax, o2);
                            var key = $"{parts[x.part].Mark.Split('-').Last()}x{parts[y.part].Category}";
                            overlapWith[key] = overlapWith.GetValueOrDefault(key) + 1;
                        }
                    }
                if (ov) overlapN++;
                // inner gusset to the steel under it
                var inner = myG.OrderBy(x => x.s0).First();
                var under = sideIv.Where(x => !gIdx.ContainsKey(x.part) && x.s1 <= inner.s0 + IN / 32 && x.s1 > inner.s0 - 2 * IN).OrderByDescending(x => x.s1).FirstOrDefault();
                if (under.s1 != 0 || under.part != 0)
                {
                    double gap = inner.s0 - under.s1;
                    var cat = parts[under.part].Category.ToString();
                    innerNb[cat] = innerNb.GetValueOrDefault(cat) + 1;
                    if (gap > IN / 32) { innerGapN++; innerGapMax = Math.Max(innerGapMax, gap); }
                }
                var outer = myG.OrderBy(x => x.s1).Last();
                var over = sideIv.Where(x => !gIdx.ContainsKey(x.part) && x.s0 >= outer.s1 - IN / 32 && x.s0 < outer.s1 + 2 * IN).OrderBy(x => x.s0).FirstOrDefault();
                if (over.s1 != 0 || over.part != 0)
                {
                    outsideN++;
                    var cat = parts[over.part].Category.ToString();
                    outerNb[cat] = outerNb.GetValueOrDefault(cat) + 1;
                }
            }
        string D(Dictionary<string, int> d) => Q(string.Join("; ", d.OrderByDescending(x => x.Value).Select(x => $"{x.Key}:{x.Value}")));
        wS.WriteLine(string.Join(",", gs.Plane, gs.Point, side, samples, nLayers, F(layerGapMax / IN), layerGapN, floatN, overlapN, F(overlapMax / IN), D(overlapWith), innerGapN, F(innerGapMax / IN), D(innerNb), D(outerNb), outsideN));

        // rivets of this face
        double rr = Math.Max(Math.Max(Math.Abs(amin), amax), Math.Max(Math.Abs(bmin), bmax)) + 6 * IN;
        int airN = 0; double airMax = 0; int nr = 0, buried = 0, headOff = 0, gShort = 0, gLong = 0, thruOuter = 0, inLayerArea = 0, missLayer = 0; double buriedMax = 0, gdMax = 0;
        var grips = new Dictionary<string, int>();
        foreach (var bt in rivets)
        {
            var z = bt.Frame.Z;
            if (Vec3.Dot(z, n) * -side < 0.95) continue;          // shank pointing inward from this face
            var w = bt.Frame.Origin - P;
            double ta = Vec3.Dot(w, u), tb = Vec3.Dot(w, v), tn = Vec3.Dot(w, n) * side;
            if (Math.Abs(ta) > rr || Math.Abs(tb) > rr || tn < 0 || tn > 3 * 0.3048) continue;
            if (ta < amin || ta > amax || tb < bmin || tb > bmax) continue;
            // the rivet must lie over one of this face's gusset parts
            var mid = bt.Frame.Origin + z * (bt.Grip / 2);
            var iv = Intervals(mid + u * jit, z).OrderBy(x => x.t0).ToList();   // t relative to mid along z
            double h0 = -bt.Grip / 2;   // head face at t = h0
            var myG = iv.Where(x => gIdx.TryGetValue(x.part, out var g) && g.side == side).ToList();
            if (myG.Count == 0) continue;
            nr++;
            // contiguous chain containing the shank start (t just above h0)
            var chain = new List<(double t0, double t1, int part)>();
            var seed = iv.Where(x => x.t0 <= h0 + IN / 16 && x.t1 >= h0 + IN / 64).OrderBy(x => x.t0).FirstOrDefault();
            double clo = double.NaN, chi = double.NaN;
            if (seed.t1 != 0 || seed.part != 0)
            {
                clo = seed.t0; chi = seed.t1; chain.Add(seed);
                bool grow = true;
                while (grow)
                {
                    grow = false;
                    foreach (var x in iv)
                    {
                        if (chain.Contains(x)) continue;
                        if (x.t1 >= clo - IN / 64 && x.t0 <= chi + IN / 64) { chain.Add(x); clo = Math.Min(clo, x.t0); chi = Math.Max(chi, x.t1); grow = true; }
                    }
                }
            }
            else { headOff++; }
            if (!double.IsNaN(clo))
            {
                double bur = h0 - clo;
                if (bur > IN / 32) { buried++; buriedMax = Math.Max(buriedMax, bur); }
                double lenChain = chi - Math.Max(clo, h0);
                double dg = bt.Grip - (chi - h0);
                if (dg > IN / 16) gShort++; else if (dg < -IN / 16) gLong++;
                gdMax = Math.Max(gdMax, Math.Abs(dg));
            }
            var layersHere = myG.Select(x => gIdx[x.part].layer).Distinct().OrderBy(x => x).ToList();
            int maxL = layersHere.Max();
            if (maxL > 1) inLayerArea++;
            bool thru = chain.Any(x => gIdx.TryGetValue(x.part, out var g) && g.side == side && g.layer == maxL);
            if (maxL > 1 && thru) thruOuter++;
            if (maxL > 1 && !thru) missLayer++;
            string gk = (bt.Grip / IN).ToString("0.###", inv);
            grips[gk] = grips.GetValueOrDefault(gk) + 1;
            double steel = 0; foreach (var x in iv) { double a0 = Math.Max(x.t0, h0), a1 = Math.Min(x.t1, h0 + bt.Grip); if (a1 > a0) steel += a1 - a0; }
            double air = bt.Grip - steel;
            if (air > IN / 16) { airN++; airMax = Math.Max(airMax, air); }
            string stk = string.Join(" ", iv.Where(x => x.t1 > h0 - IN && x.t0 < h0 + bt.Grip + IN).Select(x => $"{parts[x.part].Mark}[{((x.t0 - h0) / IN):F3}..{((x.t1 - h0) / IN):F3}]"));
            wRA.WriteLine(string.Join(",", gs.Plane, gs.Point, side, F(ta / IN), F(tb / IN), F(bt.Diameter / IN), F(bt.Grip / IN), F(air / IN), Q(string.Join("+", layersHere)), Q(stk)));
            if (nLayers > 1 || nr % 10 == 0)
                wRD.WriteLine(string.Join(",", gs.Plane, gs.Point, side, F(ta / IN), F(tb / IN), F(bt.Diameter / IN), F(bt.Grip / IN), double.IsNaN(clo) ? "" : F((clo - h0) / IN), double.IsNaN(chi) ? "" : F((chi - h0) / IN),
                    Q(string.Join(" ", chain.OrderBy(x => x.t0).Select(x => parts[x.part].Mark))), Q(string.Join("+", layersHere))));
        }
        wR.WriteLine(string.Join(",", gs.Plane, gs.Point, side, nr, buried, F(buriedMax / IN), headOff, gShort, gLong, F(gdMax / IN), thruOuter, inLayerArea, missLayer, Q(string.Join("; ", grips.OrderByDescending(x => x.Value).Take(6).Select(x => $"{x.Key}:{x.Value}"))), airN, F(airMax / IN)));
    }
    wS.Flush(); wR.Flush();
}
wRA.Close(); wJ.Close(); wS.Close(); wP.Close(); wR.Close(); wRD.Close();
using (var w = new StreamWriter(Path.Combine(outDir, "stacks.csv")))
{
    w.WriteLine("plane,point,thk_in,areas_sf");
    foreach (var g in tm.GussetStacks) w.WriteLine($"{g.Plane},{g.Point},{Q(string.Join(" + ", g.Thicknesses.Select(t => (t / IN).ToString("0.###", inv))))},{Q(string.Join(" + ", g.Areas.Select(a => (a / 0.092903).ToString("0.0", inv))))}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "diag-gusset.txt")))
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("gusset", StringComparison.OrdinalIgnoreCase) || d.Message.Contains("layer", StringComparison.OrdinalIgnoreCase)))
        w.WriteLine($"{d.Severity}\t{d.Message}");
Print($"done {sw.Elapsed.TotalSeconds:0}s");
using (var w = new StreamWriter(Path.Combine(outDir, "diag-all.txt")))
    foreach (var d in st.Diagnostics) w.WriteLine($"{d.Severity}\t{d.Message}");
using (var w = new StreamWriter(Path.Combine(outDir, "parts.csv")))
{
    w.WriteLine("mark,category,assembly,len_ft,mass_lb,cx,cy,cz,vol_cf,depth_in,width_in,desc");
    var mem = tm.Members.ToDictionary(x => x.Mark, x => x);
    foreach (var p in st.Parts)
    {
        var bx = p.Mesh.Bounds(); var c = (bx.Min + bx.Max) * 0.5;
        string dep = "", wid = "";
        if (mem.TryGetValue(p.Mark, out var mr))
        {
            // extents in the member frame: across = horizontal normal to the axis, up = normal to axis and across
            var ax = (mr.B - mr.A).Normalized(); var acr = Vec3.Cross(new Vec3(0, 0, 1), ax).Normalized(); var upv = Vec3.Cross(ax, acr).Normalized();
            dep = ((p.Mesh.Positions.Max(q => Vec3.Dot(q, upv)) - p.Mesh.Positions.Min(q => Vec3.Dot(q, upv))) / IN).ToString("F3", inv);
            wid = ((p.Mesh.Positions.Max(q => Vec3.Dot(q, acr)) - p.Mesh.Positions.Min(q => Vec3.Dot(q, acr))) / IN).ToString("F3", inv);
        }
        w.WriteLine($"{Q(p.Mark)},{p.Category},{Q(p.Assembly)},{(p.Length / 0.3048).ToString("F3", inv)},{(p.Mass * 2.20462262).ToString("F1", inv)},{F(c.X)},{F(c.Y)},{F(c.Z)},{(p.Volume / 0.0283168).ToString("F4", inv)},{dep},{wid},{Q(p.Description)}");
    }
}
using (var w = new StreamWriter(Path.Combine(outDir, "dropped.csv")))
{
    w.WriteLine("plane,point,member,u_in,v_in,dia_in,beyond,reason");
    foreach (var d in tm.DroppedRivets) w.WriteLine($"{d.Plane},{d.Point},{Q(d.Member)},{F(d.U / IN)},{F(d.V / IN)},{F(d.Diameter / IN)},{d.Beyond},{Q(d.Reason)}");
}
Print($"steel {st.SteelMass * 2.20462262:N0} lb, rivets {rivets.Count}");
