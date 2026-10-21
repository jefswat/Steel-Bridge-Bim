using System.Collections.Concurrent;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Checks;

/// <summary>One solid taking part in the clash check: a steel part, a bolt or a misc element.</summary>
public sealed record ClashItem(string Kind, string Mark, string Assembly, string Description, Mesh Mesh);

/// <summary>Options of <see cref="SteelClashCheck"/>. Lengths in meters.</summary>
public sealed class ClashOptions
{
    /// <summary>Penetration below this depth is contact (faying surfaces, plates on flanges, bolt heads on plates), not a clash. Default 1/16".</summary>
    public double Tolerance { get; set; } = 0.0254 / 16;
    /// <summary>Include the installed bolts (head, shank, washer, nut). A bolt shank in its hole (hole larger than the bolt) is no clash.</summary>
    public bool Bolts { get; set; }
    /// <summary>Include the fillet welds (by definition fused to the parts they join; off by default).</summary>
    public bool Welds { get; set; }
    /// <summary>Include the non-concrete misc items (sign bracket steel, drain pipes, conduit ...).</summary>
    public bool Misc { get; set; } = true;
    /// <summary>Include the substructure concrete (caps, columns, pedestals, footings ...) and the simple deck drains; concrete is tested only
    /// against drains / misc items (drain vs pier cap / column), never against steel or other concrete.</summary>
    public bool Concrete { get; set; } = true;
}

/// <summary>A pair of solids that overlap by more than the tolerance.</summary>
public sealed record ClashResult(ClashItem A, ClashItem B, double Depth, Vec3 At, int Points)
{
    /// <summary>Category pair, alphabetical ("Angle x IntermediateStiffener").</summary>
    public string CategoryPair => string.CompareOrdinal(A.Kind, B.Kind) <= 0 ? $"{A.Kind} x {B.Kind}" : $"{B.Kind} x {A.Kind}";
    /// <summary>Location text filled by <see cref="SteelClashCheck.Locate"/> (station, offset, nearest girder).</summary>
    public string Location { get; set; } = "";
    public double Station { get; set; } = double.NaN;
}

public sealed class ClashReport
{
    public required string Bridge { get; init; }
    public List<ClashResult> Clashes { get; } = new();
    public int Items { get; set; }
    public long CandidatePairs { get; set; }
    /// <summary>Meshes that are not closed (their inside cannot be tested; their edges are still tested against the others).</summary>
    public List<string> OpenMeshes { get; } = new();
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// Solid-vs-solid interference of the steel of one bridge. Broad phase: bounding boxes (sweep along x). Narrow phase on the part
/// meshes, both ways: every edge of A is cut at its crossings with B's surface; the midpoint of each piece that lies inside B
/// (signed ray crossings, three rays, majority) is a penetration point with depth = its distance to B's surface. A pair clashes
/// when a point is deeper than <see cref="ClashOptions.Tolerance"/>; contact (faying surfaces, plates on flanges, bolts in larger
/// holes) gives depth ~0 and is not reported. Two coincident solids (no edge inside the other) are caught by equal bounds + volume.
/// Nothing is moved: this is a report.
/// </summary>
public static class SteelClashCheck
{
    /// <summary>The items of a bridge model: steel parts (welds optional), bolts (optional), misc non-concrete elements (optional).</summary>
    public static List<ClashItem> Items(BridgeModel m, ClashOptions o)
    {
        var res = new List<ClashItem>();
        if (m.Steel is { } st)
        {
            foreach (var p in st.Parts)
            {
                if (p.Mesh.IsEmpty || (p.Category == PartCategory.Weld && !o.Welds)) continue;
                res.Add(new ClashItem(p.Category.ToString(), p.Mark, p.Assembly, p.Description, p.Mesh));
            }
            if (o.Bolts)
            {
                var cache = new Dictionary<(double, double, double, BoltKind, double, bool), Mesh>();
                int i = 0;
                foreach (var b in st.Bolts)
                {
                    var key = (b.Diameter, b.Grip, b.Length, b.Kind, b.Embedment, b.FlushEnd);
                    if (!cache.TryGetValue(key, out var local))
                        cache[key] = local = b.Kind == BoltKind.Anchor ? SteelModel.AnchorBoltMesh(b.Diameter, b.Grip, b.Embedment, 12, b.TopHeight)
                            // rivets: button heads as modeled (no nut / washer / stick-through), a countersunk end flush
                            : b.Kind == BoltKind.Rivet ? SteelModel.RivetMesh(b.Diameter, b.Grip, flushEnd: b.FlushEnd)
                            : SteelModel.BoltMesh(b.Diameter > 0 ? new BoltGeometry(b.Diameter) : st.Bolt, b.Grip, b.Length);
                    var w = new Mesh();
                    foreach (var q in local.Positions) w.Positions.Add(b.Frame.ToWorld(q));
                    w.Indices.AddRange(local.Indices);
                    res.Add(new ClashItem(b.Kind == BoltKind.Anchor ? "AnchorBolt" : "Bolt", $"{b.Assembly}-B{++i}", b.Assembly,
                        $"{(b.Kind == BoltKind.Rivet ? (b.FlushEnd ? "rivet (one end flush)" : "rivet") : "bolt")} {b.Diameter / 0.0254:0.###}\" grip {b.Grip / 0.0254:0.###}\"", w));
                }
            }
        }
        if (o.Concrete && m.Substructure is { } sub)
        {
            foreach (var u in sub.Units)
                foreach (var so in u.Solids)
                    if (!so.Mesh.IsEmpty && so.IsConcrete) res.Add(new ClashItem("Concrete." + so.Kind, so.Name, u.Name, so.Description, so.Mesh));
            foreach (var d in sub.Drainage)
                if (!d.Mesh.IsEmpty && !d.IsConcrete && d.Kind != Substructure.SubElementKind.SplashBasin) res.Add(new ClashItem("Drain." + d.Kind, d.Name, "drainage", d.Description, d.Mesh));
        }
        if (o.Misc && m.Substructure?.Misc is { } misc)
            foreach (var e in misc.Elements)
            {
                if (e.Mesh.IsEmpty || e.Material.Contains("concrete", StringComparison.OrdinalIgnoreCase) || e.Kind == MiscKind.ErosionMattress) continue;
                if (e.Kind is MiscKind.SignAnchorBolt or MiscKind.PoleAnchorBolt && !o.Bolts) continue;
                res.Add(new ClashItem("Misc." + e.Kind, e.Name, e.Assembly, e.Description, e.Mesh));
            }
        return res;
    }

    /// <summary>Runs the check on a bridge model and fills the locations (station / offset on the bridge alignment, nearest girder).</summary>
    public static ClashReport Run(BridgeModel m, ClashOptions o, Units.UnitSystem? units = null)
    {
        var rep = Run(m.Definition.Name, Items(m, o), o);
        Locate(rep, m, units);
        return rep;
    }

    public static ClashReport Run(string bridge, IReadOnlyList<ClashItem> items, ClashOptions o)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rep = new ClashReport { Bridge = bridge, Items = items.Count };
        if (items.Count == 0) return rep;
        var origin = items[0].Mesh.Positions[0];
        var solids = new Solid[items.Count];
        Parallel.For(0, items.Count, i => solids[i] = new Solid(items[i].Mesh, origin));
        for (int i = 0; i < solids.Length; i++) if (!solids[i].Closed) rep.OpenMeshes.Add($"{items[i].Kind} {items[i].Mark}");

        // broad phase: sweep along x
        double tol = o.Tolerance;
        var order = Enumerable.Range(0, items.Count).OrderBy(i => solids[i].Box.Min.X).ToArray();
        var pairs = new List<(int, int)>();
        for (int k = 0; k < order.Length; k++)
        {
            int i = order[k];
            var bi = solids[i].Box;
            for (int l = k + 1; l < order.Length; l++)
            {
                int j = order[l];
                var bj = solids[j].Box;
                if (bj.Min.X > bi.Max.X - tol) break;
                if (Overlap(bi, bj, -tol) && Testable(items[i], items[j])) pairs.Add((i, j));
            }
        }
        rep.CandidatePairs = pairs.Count;

        var found = new ConcurrentBag<ClashResult>();
        Parallel.ForEach(pairs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, pr =>
        {
            var (i, j) = pr;
            var a = solids[i]; var b = solids[j];
            var (dAB, pAB, nAB) = a.Penetration(b, tol);
            var (dBA, pBA, nBA) = b.Penetration(a, tol);
            double depth = Math.Max(dAB, dBA);
            if (depth <= tol && a.Closed && b.Closed && Coincident(a, b, tol))
            {
                depth = a.MinExtent / 2; pAB = a.Center; nAB = 1;
            }
            if (depth <= tol) return;
            var at = (dAB >= dBA ? pAB : pBA) + origin;
            found.Add(new ClashResult(items[i], items[j], depth, at, nAB + nBA));
        });
        rep.Clashes.AddRange(found.OrderBy(c => c.CategoryPair, StringComparer.Ordinal).ThenBy(c => c.At.X).ThenBy(c => c.At.Y).ThenBy(c => c.A.Mark, StringComparer.Ordinal));
        rep.Elapsed = sw.Elapsed;
        return rep;
    }

    /// <summary>Station / offset of each clash on the bridge alignment and the nearest girder (plan name when the layout has one).</summary>
    public static void Locate(ClashReport rep, BridgeModel m, Units.UnitSystem? units)
    {
        var fr = m.Framing;
        var names = m.Definition.Girders.PlanNames;
        foreach (var c in rep.Clashes)
        {
            var so = fr.Alignment.Inverse(c.At.XY);
            c.Station = so.Station;
            string gname = "";
            double best = double.MaxValue;
            foreach (var g in fr.Girders)
            {
                var wl = g.WebLine;
                for (int k = 0; k + 1 < wl.Count; k++)
                {
                    double d = SegDist2(c.At.XY, wl[k].XY, wl[k + 1].XY);
                    if (d < best) { best = d; gname = names != null && g.Index < names.Count ? names[g.Index] : g.Name; }
                }
            }
            string sta = units != null ? units.FormatStation(so.Station) : so.Station.ToString("F3");
            string off = units != null ? units.FormatLength(so.Offset) : so.Offset.ToString("F3");
            string dist = units != null ? units.FormatLength(Math.Sqrt(best), Units.LengthKind.Short) : Math.Sqrt(best).ToString("F3");
            c.Location = $"Sta {sta}, offset {off}, girder {gname} ({dist} from its web CL)";
        }
    }

    private static double SegDist2(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a; double l2 = ab.LengthSquared;
        double t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
        return (a + ab * t - p).LengthSquared;
    }

    /// <summary>Concrete is tested only against drains / misc items.</summary>
    private static bool Testable(ClashItem a, ClashItem b)
    {
        bool ca = a.Kind.StartsWith("Concrete.", StringComparison.Ordinal), cb = b.Kind.StartsWith("Concrete.", StringComparison.Ordinal);
        if (!ca && !cb) return true;
        if (ca && cb) return false;
        var other = ca ? b : a;
        return other.Kind.StartsWith("Misc.", StringComparison.Ordinal) || other.Kind.StartsWith("Drain.", StringComparison.Ordinal);
    }

    private static bool Overlap(Box3 a, Box3 b, double grow) =>
        a.Min.X <= b.Max.X + grow && b.Min.X <= a.Max.X + grow && a.Min.Y <= b.Max.Y + grow && b.Min.Y <= a.Max.Y + grow &&
        a.Min.Z <= b.Max.Z + grow && b.Min.Z <= a.Max.Z + grow;

    private static bool Coincident(Solid a, Solid b, double tol)
    {
        bool Near(Vec3 p, Vec3 q) => Math.Abs(p.X - q.X) < tol && Math.Abs(p.Y - q.Y) < tol && Math.Abs(p.Z - q.Z) < tol;
        return Near(a.Box.Min, b.Box.Min) && Near(a.Box.Max, b.Box.Max) && Math.Abs(a.Volume - b.Volume) < 0.05 * Math.Max(a.Volume, b.Volume);
    }

    /// <summary>Text report: counts by category pair, then each clash.</summary>
    public static string ToText(IEnumerable<ClashReport> reports, Units.UnitSystem? units)
    {
        var sb = new System.Text.StringBuilder();
        string Len(double m) => units != null ? units.FormatLength(m, Units.LengthKind.Short) : $"{m:F4} m";
        foreach (var r in reports)
        {
            sb.AppendLine($"{r.Bridge}: {r.Clashes.Count} clashes ({r.Items} solids, {r.CandidatePairs} box pairs, {r.Elapsed.TotalSeconds:F1} s)");
            foreach (var g in r.Clashes.GroupBy(c => c.CategoryPair).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
                sb.AppendLine($"  {g.Count(),5}  {g.Key}  (max depth {Len(g.Max(c => c.Depth))})");
            if (r.OpenMeshes.Count > 0) sb.AppendLine($"  open meshes (inside not testable): {r.OpenMeshes.Count}: {string.Join(", ", r.OpenMeshes.Take(10))}{(r.OpenMeshes.Count > 10 ? " ..." : "")}");
        }
        foreach (var r in reports)
            foreach (var c in r.Clashes)
                sb.AppendLine($"  {r.Bridge} | {c.CategoryPair} | {c.A.Mark} ({c.A.Description}) x {c.B.Mark} ({c.B.Description}) | depth {Len(c.Depth)} | {c.Location}");
        return sb.ToString();
    }

    public static void WriteCsv(string path, IEnumerable<ClashReport> reports, Units.UnitSystem? units)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("bridge,category_pair,mark_a,kind_a,assembly_a,desc_a,mark_b,kind_b,assembly_b,desc_b,depth_in,station,location,x,y,z");
        string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        foreach (var r in reports)
            foreach (var c in r.Clashes)
                w.WriteLine(string.Join(",", Q(r.Bridge), Q(c.CategoryPair), Q(c.A.Mark), c.A.Kind, Q(c.A.Assembly), Q(c.A.Description), Q(c.B.Mark), c.B.Kind,
                    Q(c.B.Assembly), Q(c.B.Description), (c.Depth / 0.0254).ToString("F4"), units != null && !double.IsNaN(c.Station) ? units.FormatStation(c.Station) : c.Station.ToString("F3"),
                    Q(c.Location), c.At.X.ToString("F4"), c.At.Y.ToString("F4"), c.At.Z.ToString("F4")));
    }

    // =========================================================================== geometry

    /// <summary>A part mesh rebased to a common origin, with a triangle BVH (built on first use) and its edge list.</summary>
    private sealed class Solid
    {
        public readonly Vec3[] P;
        public readonly int[] T;
        public readonly Box3 Box;
        public readonly bool Closed;
        public readonly double Volume;
        public readonly (int a, int b)[] Edges;
        private Bvh? _tri, _edge;
        private readonly object _lock = new();

        public Solid(Mesh m, Vec3 origin)
        {
            P = new Vec3[m.Positions.Count];
            var box = Box3.Empty;
            for (int i = 0; i < P.Length; i++) { P[i] = m.Positions[i] - origin; box.Add(P[i]); }
            T = m.Indices.ToArray();
            Box = box;
            // weld vertices by position (builders duplicate vertices per face) for the edge list and the closed test
            var key = new Dictionary<(long, long, long), int>();
            var id = new int[P.Length];
            for (int i = 0; i < P.Length; i++)
            {
                var k = ((long)Math.Round(P[i].X * 1e6), (long)Math.Round(P[i].Y * 1e6), (long)Math.Round(P[i].Z * 1e6));
                if (!key.TryGetValue(k, out int vi)) key[k] = vi = i;
                id[i] = vi;
            }
            var edges = new HashSet<(int, int)>();
            for (int t = 0; t + 2 < T.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = id[T[t + e]], b = id[T[t + (e + 1) % 3]];
                    if (a != b) edges.Add(a < b ? (a, b) : (b, a));
                }
            Edges = edges.ToArray();
            // Closed (watertight, T-junctions allowed): the enclosed volume does not depend on the reference point.
            double v1 = 0, v2 = 0;
            var o1 = box.Min; var o2 = box.Max;
            for (int t = 0; t + 2 < T.Length; t += 3)
            {
                v1 += Vec3.Dot(P[T[t]] - o1, Vec3.Cross(P[T[t + 1]] - o1, P[T[t + 2]] - o1));
                v2 += Vec3.Dot(P[T[t]] - o2, Vec3.Cross(P[T[t + 1]] - o2, P[T[t + 2]] - o2));
            }
            Volume = Math.Abs(v1 / 6);
            var ext = box.Max - box.Min;
            Closed = T.Length >= 12 && Math.Abs(v1 - v2) / 6 <= 1e-6 * Math.Max(Volume, 1e-12) + 1e-12 * Math.Max(1, ext.X * ext.Y * ext.Z);
        }

        public Vec3 Center => (Box.Min + Box.Max) * 0.5;
        public double MinExtent => Math.Min(Box.Max.X - Box.Min.X, Math.Min(Box.Max.Y - Box.Min.Y, Box.Max.Z - Box.Min.Z));

        private Bvh Tri
        {
            get
            {
                if (_tri != null) return _tri;
                lock (_lock)
                {
                    if (_tri != null) return _tri;
                    var boxes = new Box3[T.Length / 3];
                    for (int t = 0; t < boxes.Length; t++)
                    {
                        var b = Box3.Empty; b.Add(P[T[3 * t]]); b.Add(P[T[3 * t + 1]]); b.Add(P[T[3 * t + 2]]);
                        boxes[t] = b;
                    }
                    return _tri = new Bvh(boxes);
                }
            }
        }

        private Bvh EdgeTree
        {
            get
            {
                if (_edge != null) return _edge;
                lock (_lock)
                {
                    if (_edge != null) return _edge;
                    var boxes = new Box3[Edges.Length];
                    for (int e = 0; e < boxes.Length; e++) { var b = Box3.Empty; b.Add(P[Edges[e].a]); b.Add(P[Edges[e].b]); boxes[e] = b; }
                    return _edge = new Bvh(boxes);
                }
            }
        }

        /// <summary>Deepest point of this solid's edges inside <paramref name="other"/>: (depth, point, number of points deeper than tol).</summary>
        public (double depth, Vec3 at, int n) Penetration(Solid other, double tol)
        {
            double best = 0; Vec3 at = default; int n = 0;
            if (!other.Closed) return (0, at, 0);
            var ob = other.Box;
            var q = new Box3 { Min = ob.Min - new Vec3(tol, tol, tol), Max = ob.Max + new Vec3(tol, tol, tol) };
            var ts = new List<double>();
            var stack = new Stack<int>();
            foreach (int e in EdgeTree.Query(q, new Stack<int>()))
            {
                var p0 = P[Edges[e].a]; var p1 = P[Edges[e].b];
                ts.Clear(); ts.Add(0); ts.Add(1);
                other.SegmentCrossings(p0, p1, ts, stack);
                ts.Sort();
                for (int k = 0; k + 1 < ts.Count; k++)
                {
                    if (ts[k + 1] - ts[k] < 1e-9) continue;
                    var m = Vec3.Lerp(p0, p1, (ts[k] + ts[k + 1]) / 2);
                    if (!Inside(ob, m)) continue;
                    double d = other.Distance(m, best > tol ? double.MaxValue : tol, stack);
                    if (d <= tol) continue;
                    if (!other.Contains(m, stack)) continue;
                    if (best <= tol) d = other.Distance(m, double.MaxValue, stack);
                    n++;
                    if (d > best) { best = d; at = m; }
                    if (n >= 400) return (best, at, n);
                }
            }
            return (best, at, n);
        }

        private static bool Inside(Box3 b, Vec3 p) => p.X >= b.Min.X && p.X <= b.Max.X && p.Y >= b.Min.Y && p.Y <= b.Max.Y && p.Z >= b.Min.Z && p.Z <= b.Max.Z;

        /// <summary>Parameters t in (0,1) where segment p0-p1 crosses a triangle (not parallel to it).</summary>
        private void SegmentCrossings(Vec3 p0, Vec3 p1, List<double> ts, Stack<int> stack)
        {
            var d = p1 - p0;
            foreach (int t in Tri.Ray(p0, d, stack))
            {
                if (RayTri(p0, d, P[T[3 * t]], P[T[3 * t + 1]], P[T[3 * t + 2]], out double u, out _) && u > 0 && u < 1) ts.Add(u);
            }
        }

        /// <summary>Distance to the surface (best-first BVH search); stops early once a triangle closer than <paramref name="limit"/> is found
        /// (then returns a value &lt;= limit). limit = MaxValue = exact nearest distance.</summary>
        public double Distance(Vec3 p, double limit, Stack<int> stack)
        {
            double best = double.MaxValue;
            bool early = limit < double.MaxValue;
            Tri.Nearest(p, ref best, t =>
            {
                var c = MiscMesh.ClosestOnTriangle(p, P[T[3 * t]], P[T[3 * t + 1]], P[T[3 * t + 2]]);
                return (c - p).Length;
            }, early ? limit : -1);
            return best;
        }

        private static readonly Vec3[] Dirs = { new Vec3(0.5773, 0.5774, 0.5775).Normalized(), new Vec3(-0.6123, 0.2347, 0.7551).Normalized(), new Vec3(0.1931, -0.8812, -0.4314).Normalized() };

        /// <summary>Inside by signed ray crossings (winding number, union of shells), majority of three rays.</summary>
        public bool Contains(Vec3 p, Stack<int> stack)
        {
            int votes = 0;
            double far = 2 * ((Box.Max - Box.Min).Length + 1);
            for (int r = 0; r < Dirs.Length; r++)
            {
                var d = Dirs[r] * far;
                int w = 0;
                foreach (int t in Tri.Ray(p, d, stack))
                {
                    var a = P[T[3 * t]]; var b = P[T[3 * t + 1]]; var c = P[T[3 * t + 2]];
                    if (RayTri(p, d, a, b, c, out double u, out double dn) && u > 0) w += dn > 0 ? 1 : -1;
                }
                if (w != 0) votes++;
                if (votes >= 2) return true;
                if (r - votes >= 1) return false;
            }
            return votes >= 2;
        }

        /// <summary>Möller–Trumbore; u = ray parameter, dn = sign of normal . direction.</summary>
        private static bool RayTri(Vec3 o, Vec3 d, Vec3 a, Vec3 b, Vec3 c, out double u, out double dn)
        {
            u = 0; dn = 0;
            var e1 = b - a; var e2 = c - a;
            var pv = Vec3.Cross(d, e2);
            double det = Vec3.Dot(e1, pv);
            var n = Vec3.Cross(e1, e2);
            double nl = n.Length, dl = d.Length;
            if (nl < 1e-18 || Math.Abs(det) < 1e-12 * nl * dl) return false;
            double inv = 1 / det;
            var tv = o - a;
            double bu = Vec3.Dot(tv, pv) * inv;
            if (bu < 0 || bu > 1) return false;
            var qv = Vec3.Cross(tv, e1);
            double bv = Vec3.Dot(d, qv) * inv;
            if (bv < 0 || bu + bv > 1) return false;
            u = Vec3.Dot(e2, qv) * inv;
            dn = Vec3.Dot(n, d);
            return true;
        }
    }

    /// <summary>Bounding volume hierarchy over boxes (median split, leaves of up to 6).</summary>
    private sealed class Bvh
    {
        private readonly Box3[] _nb;
        private readonly int[] _left, _right, _start, _count;
        private readonly int[] _idx;
        private int _n;

        public Bvh(Box3[] boxes)
        {
            int cap = Math.Max(1, 2 * boxes.Length);
            _nb = new Box3[cap]; _left = new int[cap]; _right = new int[cap]; _start = new int[cap]; _count = new int[cap];
            _idx = Enumerable.Range(0, boxes.Length).ToArray();
            var cen = boxes.Select(b => (b.Min + b.Max) * 0.5).ToArray();
            if (boxes.Length > 0) Build(boxes, cen, 0, boxes.Length);
        }

        private int Build(Box3[] boxes, Vec3[] cen, int s, int e)
        {
            int node = _n++;
            var b = Box3.Empty;
            for (int i = s; i < e; i++) { b.Add(boxes[_idx[i]].Min); b.Add(boxes[_idx[i]].Max); }
            _nb[node] = b;
            if (e - s <= 6) { _left[node] = -1; _start[node] = s; _count[node] = e - s; return node; }
            var ext = b.Max - b.Min;
            int axis = ext.X >= ext.Y && ext.X >= ext.Z ? 0 : ext.Y >= ext.Z ? 1 : 2;
            double Key(int i) => axis == 0 ? cen[i].X : axis == 1 ? cen[i].Y : cen[i].Z;
            Array.Sort(_idx, s, e - s, Comparer<int>.Create((p, q) => Key(p).CompareTo(Key(q))));
            int mid = (s + e) / 2;
            _left[node] = Build(boxes, cen, s, mid);
            _right[node] = Build(boxes, cen, mid, e);
            return node;
        }

        /// <summary>Primitives whose box the segment o..o+d (t in [0,1]) passes through (slab test).</summary>
        public IEnumerable<int> Ray(Vec3 o, Vec3 d, Stack<int> stack)
        {
            if (_n == 0) yield break;
            double ix = 1 / d.X, iy = 1 / d.Y, iz = 1 / d.Z;
            stack.Clear();
            stack.Push(0);
            while (stack.Count > 0)
            {
                int node = stack.Pop();
                var b = _nb[node];
                double t0 = 0, t1 = 1;
                if (!Slab(o.X, ix, b.Min.X, b.Max.X, ref t0, ref t1) || !Slab(o.Y, iy, b.Min.Y, b.Max.Y, ref t0, ref t1) || !Slab(o.Z, iz, b.Min.Z, b.Max.Z, ref t0, ref t1)) continue;
                if (_left[node] < 0)
                {
                    for (int i = _start[node]; i < _start[node] + _count[node]; i++) yield return _idx[i];
                }
                else { stack.Push(_left[node]); stack.Push(_right[node]); }
            }
        }

        private static bool Slab(double o, double inv, double lo, double hi, ref double t0, ref double t1)
        {
            const double eps = 1e-9;
            if (double.IsInfinity(inv)) return o >= lo - eps && o <= hi + eps;
            double a = (lo - eps - o) * inv, b = (hi + eps - o) * inv;
            if (a > b) (a, b) = (b, a);
            if (a > t0) t0 = a;
            if (b < t1) t1 = b;
            return t0 <= t1;
        }

        /// <summary>Best-first nearest search; <paramref name="dist"/> = exact distance to primitive i. Stops when best &lt;= stopBelow.</summary>
        public void Nearest(Vec3 p, ref double best, Func<int, double> dist, double stopBelow)
        {
            if (_n == 0) return;
            var heap = new PriorityQueue<int, double>();
            heap.Enqueue(0, BoxDist(_nb[0], p));
            while (heap.TryDequeue(out int node, out double bd))
            {
                if (bd >= best) break;
                if (_left[node] < 0)
                {
                    for (int i = _start[node]; i < _start[node] + _count[node]; i++)
                    {
                        double d = dist(_idx[i]);
                        if (d < best) { best = d; if (best <= stopBelow) return; }
                    }
                }
                else
                {
                    heap.Enqueue(_left[node], BoxDist(_nb[_left[node]], p));
                    heap.Enqueue(_right[node], BoxDist(_nb[_right[node]], p));
                }
            }
        }

        private static double BoxDist(Box3 b, Vec3 p)
        {
            double dx = Math.Max(0, Math.Max(b.Min.X - p.X, p.X - b.Max.X));
            double dy = Math.Max(0, Math.Max(b.Min.Y - p.Y, p.Y - b.Max.Y));
            double dz = Math.Max(0, Math.Max(b.Min.Z - p.Z, p.Z - b.Max.Z));
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public IEnumerable<int> Query(Box3 q, Stack<int> stack)
        {
            if (_n == 0) yield break;
            stack.Clear();
            stack.Push(0);
            while (stack.Count > 0)
            {
                int node = stack.Pop();
                if (!Overlap(_nb[node], q, 0)) continue;
                if (_left[node] < 0)
                {
                    for (int i = _start[node]; i < _start[node] + _count[node]; i++) yield return _idx[i];
                }
                else { stack.Push(_left[node]); stack.Push(_right[node]); }
            }
        }
    }
}
