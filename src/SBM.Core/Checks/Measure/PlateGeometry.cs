using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Checks.Measure;

/// <summary>A plate-like part of a solid: two large parallel opposite faces a small distance apart (a plate, one leg of an angle, a WT flange or stem).</summary>
public sealed class PlateComponent
{
    /// <summary>Unit normal of the first face (outward).</summary>
    public required Vec3 Normal { get; init; }
    /// <summary>A world point on the mid plane.</summary>
    public required Vec3 MidPoint { get; init; }
    public required double Thickness { get; init; }
    /// <summary>Area of the smaller of the two faces (m²).</summary>
    public required double FaceArea { get; init; }
    /// <summary>Share of the whole surface area in the two faces (a flat plate is close to 1; a curved plate has many facets and a small share).</summary>
    public required double Coverage { get; init; }
}

/// <summary>A flattened outline: outer boundary and inner loops in a plate frame (u, v), meters, counter-clockwise outer / clockwise inner.</summary>
public sealed class FlatShape
{
    public required List<Vec2> Outer { get; init; }
    public List<List<Vec2>> Inner { get; } = new();
    /// <summary>Other disconnected outer pieces (rare; the largest is <see cref="Outer"/>).</summary>
    public List<List<Vec2>> OtherPieces { get; } = new();
    /// <summary>Round holes known exactly (recipes): center, diameter. Section-derived holes are fitted from <see cref="Inner"/>.</summary>
    public List<(Vec2 Center, double Diameter)> ExactHoles { get; } = new();
    /// <summary>"recipe" (ExtrudedSolid: exact polygon + holes) or "mesh section" (mid-plane cut of the triangles).</summary>
    public required string Source { get; init; }
    /// <summary>Recipes: the sweep vector (world); its angle to the plate normal = edge bevel.</summary>
    public Vec3? Sweep { get; init; }
}

/// <summary>
/// Plate geometry from the generated solids, independent of the world axes: face planes are clustered from the triangles, the plate normal is the
/// pair of largest opposite parallel faces, the outline is the exact <see cref="ExtrudedSolid"/> recipe when the recipe lies in that plane, else the
/// mid-plane cut of the mesh (<see cref="MeshSection"/>). Strips of long plates are merged by cancelling shared edges.
/// </summary>
public static class PlateGeometry
{
    private const double CosNormal = 0.99996;   // 0.5 deg
    private const double PlaneGap = 3e-4;      // 0.3 mm between face planes of one direction

    private sealed class Plane { public Vec3 N; public double D; public double Area; }

    /// <summary>Plate-like components of a mesh, best (largest faces) first. <paramref name="maxThickness"/> limits the face separation.</summary>
    public static List<PlateComponent> Components(Mesh m, double maxThickness = 0.2)
    {
        var res = new List<PlateComponent>();
        if (m.IsEmpty) return res;
        var o = m.Positions[0];
        int nt = m.TriangleCount;
        var tn = new Vec3[nt]; var td = new double[nt]; var ta = new double[nt];
        double total = 0;
        for (int t = 0; t < nt; t++)
        {
            var a = m.Positions[m.Indices[3 * t]] - o; var b = m.Positions[m.Indices[3 * t + 1]] - o; var c = m.Positions[m.Indices[3 * t + 2]] - o;
            var cr = Vec3.Cross(b - a, c - a);
            double l = cr.Length;
            ta[t] = l / 2; total += l / 2;
            if (l < 1e-14) continue;
            tn[t] = cr * (1 / l);
            td[t] = Vec3.Dot(tn[t], (a + b + c) * (1.0 / 3));
        }
        // direction clusters (largest triangles first), then planes by offset
        var order = Enumerable.Range(0, nt).Where(t => ta[t] > 1e-12).OrderByDescending(t => ta[t]).ToArray();
        var dirs = new List<(Vec3 N, List<int> Tris)>();
        foreach (int t in order)
        {
            int k = -1;
            for (int i = 0; i < dirs.Count; i++) if (Vec3.Dot(dirs[i].N, tn[t]) > CosNormal) { k = i; break; }
            if (k < 0) dirs.Add((tn[t], new List<int> { t })); else dirs[k].Tris.Add(t);
        }
        var planes = new List<Plane>();
        foreach (var (n0, tris) in dirs)
        {
            tris.Sort((x, y) => td[x].CompareTo(td[y]));
            int s = 0;
            for (int i = 1; i <= tris.Count; i++)
            {
                if (i < tris.Count && td[tris[i]] - td[tris[i - 1]] <= PlaneGap) continue;
                double A = 0, D = 0; var N = Vec3.Zero;
                for (int k = s; k < i; k++) { int t = tris[k]; A += ta[t]; D += td[t] * ta[t]; N += tn[t] * ta[t]; }
                planes.Add(new Plane { N = N.Normalized(), D = D / A, Area = A });
                s = i;
            }
        }
        planes.Sort((a, b) => b.Area.CompareTo(a.Area));
        if (planes.Count > 400) planes.RemoveRange(400, planes.Count - 400);
        var pairs = new List<(Plane A, Plane B, double T, double Score)>();
        for (int i = 0; i < planes.Count; i++)
            for (int j = i + 1; j < planes.Count; j++)
            {
                var a = planes[i]; var b = planes[j];
                if (Vec3.Dot(a.N, b.N) > -CosNormal) continue;
                double t = a.D + b.D;
                double sc = Math.Min(a.Area, b.Area);
                if (t < 1e-4 || t > maxThickness || t > 0.6 * Math.Sqrt(sc)) continue;
                pairs.Add((a, b, t, sc));
            }
        pairs.Sort((x, y) => y.Score.CompareTo(x.Score));
        var used = new List<(Vec3 N, double Lo, double Hi)>();
        foreach (var (a, b, t, sc) in pairs)
        {
            if (res.Count > 0 && sc < 0.05 * res[0].FaceArea) break;
            double lo = a.D - t, hi = a.D;
            bool dup = false;
            foreach (var (un, ulo, uhi) in used)
            {
                double dot = Vec3.Dot(un, a.N);
                if (Math.Abs(dot) < CosNormal) continue;
                double l2 = dot > 0 ? lo : -hi, h2 = dot > 0 ? hi : -lo;
                if (l2 < uhi - 1e-6 && h2 > ulo + 1e-6) { dup = true; break; }
            }
            if (dup) continue;
            used.Add((a.N, lo, hi));
            res.Add(new PlateComponent
            {
                Normal = a.N, MidPoint = o + a.N * (a.D - t / 2), Thickness = t, FaceArea = sc, Coverage = total > 0 ? (a.Area + b.Area) / total : 0,
            });
            if (res.Count >= 8) break;
        }
        return res;
    }

    /// <summary>Outline of a component in the frame (O, U, V) with N = U x V = the component normal (either sign).</summary>
    public static FlatShape Outline(Mesh m, PlateComponent c, Vec3 O, Vec3 U, Vec3 V)
    {
        var N = Vec3.Cross(U, V);
        return FromRecipes(m, c, O, U, V, N) ?? FromSection(m, O, U, V, N);
    }

    private static FlatShape? FromRecipes(Mesh m, PlateComponent c, Vec3 O, Vec3 U, Vec3 V, Vec3 N)
    {
        if (!m.HasExactSolids || m.Solids is not { } solids) return null;
        foreach (var s in solids)
        {
            if (Math.Abs(Vec3.Dot(s.Frame.Z, N)) < 0.9999) return null;
            if (Math.Abs(Math.Abs(Vec3.Dot(s.Extrusion, N)) - c.Thickness) > 5e-4) return null;
            var mid = s.Frame.Origin + s.Extrusion * 0.5;
            if (Math.Abs(Vec3.Dot(mid - c.MidPoint, N)) > 5e-4) return null;
        }
        Vec2 To2(Vec3 p) { var d = p - O; return new Vec2(Vec3.Dot(d, U), Vec3.Dot(d, V)); }
        var outers = new List<List<Vec2>>();
        var holes = new List<(Vec2, double)>();
        foreach (var s in solids)
        {
            var half = s.Extrusion * 0.5;
            var loop = s.Outline.Select(q => To2(s.Frame.ToWorld(q.X, q.Y, 0) + half)).ToList();
            if (Geo2.Area(loop) < 0) loop.Reverse();
            outers.Add(loop);
            foreach (var h in s.Holes) holes.Add((To2(s.Frame.ToWorld(h.Center.X, h.Center.Y, 0) + half), h.Diameter));
        }
        var merged = outers.Count == 1 ? outers : Union(outers);
        if (merged.Count == 0) return null;
        merged = merged.Select(l => Geo2.Simplify(l)).Where(l => l.Count >= 3).OrderByDescending(l => Math.Abs(Geo2.Area(l))).ToList();
        var fs = new FlatShape { Outer = merged[0], Source = "recipe", Sweep = solids[0].Extrusion };
        fs.OtherPieces.AddRange(merged.Skip(1).Where(l => Geo2.Area(l) > 0));
        foreach (var l in merged.Skip(1).Where(l => Geo2.Area(l) < 0)) fs.Inner.Add(l);
        foreach (var (hc, d) in holes)
        {
            fs.ExactHoles.Add((hc, d));
            var circ = Enumerable.Range(0, 24).Select(k => hc + Vec2.FromAngle(-2 * Math.PI * k / 24) * (d / 2)).ToList();
            fs.Inner.Add(circ);
        }
        return fs;
    }

    private static FlatShape FromSection(Mesh m, Vec3 O, Vec3 U, Vec3 V, Vec3 N)
    {
        var ms = new MeshSection(O, N, V);   // X = V x N = U
        var segs = ms.Cut(m);
        var loops = MeshSection.Loops(segs, 1e-5).Select(l => Geo2.Simplify(l, 1e-6)).Where(l => l.Count >= 3).ToList();
        if (loops.Count == 0) return new FlatShape { Outer = new List<Vec2>(), Source = "mesh section (no closed outline)" };
        // nesting depth -> outer (even) / inner (odd)
        var boxes = loops.Select(l => (minX: l.Min(p => p.X), minY: l.Min(p => p.Y), maxX: l.Max(p => p.X), maxY: l.Max(p => p.Y))).ToList();
        var area = loops.Select(l => Math.Abs(Geo2.Area(l))).ToList();
        var depth = new int[loops.Count];
        for (int i = 0; i < loops.Count; i++)
            for (int j = 0; j < loops.Count; j++)
            {
                if (i == j || area[j] <= area[i]) continue;
                var bi = boxes[i]; var bj = boxes[j];
                if (bi.minX < bj.minX - 1e-6 || bi.maxX > bj.maxX + 1e-6 || bi.minY < bj.minY - 1e-6 || bi.maxY > bj.maxY + 1e-6) continue;
                if (Contained(loops[i], loops[j])) depth[i]++;
            }
        var outers = new List<List<Vec2>>();
        var inners = new List<List<Vec2>>();
        for (int i = 0; i < loops.Count; i++)
        {
            var l = loops[i];
            bool outer = depth[i] % 2 == 0;
            if (outer != Geo2.Area(l) > 0) l.Reverse();
            (outer ? outers : inners).Add(l);
        }
        var merged = outers.Count == 1 ? outers : Union(outers.Concat(inners).ToList());
        var outs = merged.Where(l => Geo2.Area(l) > 0).Select(l => Geo2.Simplify(l)).Where(l => l.Count >= 3).OrderByDescending(l => Geo2.Area(l)).ToList();
        var ins = outers.Count == 1 ? inners : merged.Where(l => Geo2.Area(l) < 0).ToList();
        var fs = new FlatShape { Outer = outs.Count > 0 ? outs[0] : new List<Vec2>(), Source = "mesh section" };
        fs.OtherPieces.AddRange(outs.Skip(1));
        fs.Inner.AddRange(ins.Select(l => Geo2.Simplify(l, 1e-6)).Where(l => l.Count >= 3));
        return fs;
    }

    /// <summary>Loop a inside loop b: decided by the vertex of a farthest from b's boundary (shared edges of touching strips do not count).</summary>
    private static bool Contained(List<Vec2> a, List<Vec2> b)
    {
        double best = -1; Vec2 q = a[0];
        int step = Math.Max(1, a.Count / 16);
        for (int i = 0; i < a.Count; i += step)
        {
            double d = double.MaxValue;
            for (int k = 0; k < b.Count; k++) d = Math.Min(d, Geo2.SegDist(a[i], b[k], b[(k + 1) % b.Count]));
            if (d > best) { best = d; q = a[i]; }
        }
        if (best < 1e-6)
        {
            // every sampled vertex lies on b: use an edge midpoint nudged inward of a
            var e0 = a[0]; var e1 = a[1];
            var mid = (e0 + e1) * 0.5 + (e1 - e0).Normalized().Left * 1e-5 * (Geo2.Area(a) > 0 ? 1 : -1);
            q = mid;
        }
        return Geo2.Inside(b, q);
    }

    /// <summary>
    /// Union of loops that touch along shared edges (strips of one plate), outer loops counter-clockwise, inner loops clockwise: directed edges are split
    /// at the other loops' vertices (T-junctions), opposite pairs cancel, the rest is chained into loops.
    /// </summary>
    public static List<List<Vec2>> Union(List<List<Vec2>> loops, double tol = 1e-5)
    {
        var segs = new List<(Vec2 A, Vec2 B)>();
        var verts = loops.SelectMany(l => l).ToList();
        // vertex grid for the T-junction search
        double cell = 0.05;
        var grid = new Dictionary<(long, long), List<int>>();
        for (int i = 0; i < verts.Count; i++)
        {
            var k = ((long)Math.Floor(verts[i].X / cell), (long)Math.Floor(verts[i].Y / cell));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
            l.Add(i);
        }
        foreach (var loop in loops)
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % loop.Count];
                var ab = b - a; double len = ab.Length;
                if (len < tol) continue;
                var ts = new List<double>();
                long x0 = (long)Math.Floor((Math.Min(a.X, b.X) - tol) / cell), x1 = (long)Math.Floor((Math.Max(a.X, b.X) + tol) / cell);
                long y0 = (long)Math.Floor((Math.Min(a.Y, b.Y) - tol) / cell), y1 = (long)Math.Floor((Math.Max(a.Y, b.Y) + tol) / cell);
                if ((x1 - x0 + 1) * (y1 - y0 + 1) > 20000)
                {
                    foreach (var v in verts) TryAdd(v);
                }
                else
                    for (long gx = x0; gx <= x1; gx++)
                        for (long gy = y0; gy <= y1; gy++)
                            if (grid.TryGetValue((gx, gy), out var l)) foreach (int vi in l) TryAdd(verts[vi]);
                void TryAdd(Vec2 v)
                {
                    double t = Vec2.Dot(v - a, ab) / (len * len);
                    if (t * len <= tol || (1 - t) * len <= tol) return;
                    if (Math.Abs(Vec2.Cross(ab, v - a)) / len > tol) return;
                    ts.Add(t);
                }
                ts.Sort();
                var prev = a;
                foreach (double t in ts.Distinct())
                {
                    var p = a + ab * t;
                    if ((p - prev).Length > tol) { segs.Add((prev, p)); prev = p; }
                }
                if ((b - prev).Length > tol) segs.Add((prev, b));
            }
        (long, long) K(Vec2 p) => ((long)Math.Round(p.X / tol), (long)Math.Round(p.Y / tol));
        // an edge whose reverse is also present is shared by two touching loops: both go
        var keyCount = new HashSet<((long, long), (long, long))>();
        foreach (var (a, b) in segs) keyCount.Add((K(a), K(b)));
        var keep = new List<(Vec2 A, Vec2 B)>();
        foreach (var (a, b) in segs)
            if (!keyCount.Contains((K(b), K(a)))) keep.Add((a, b));
        // chain directed segments
        var from = new Dictionary<(long, long), List<int>>();
        for (int i = 0; i < keep.Count; i++) { var k = K(keep[i].A); if (!from.TryGetValue(k, out var l)) from[k] = l = new List<int>(); l.Add(i); }
        var done = new bool[keep.Count];
        var res = new List<List<Vec2>>();
        for (int i = 0; i < keep.Count; i++)
        {
            if (done[i]) continue;
            var loop = new List<Vec2> { keep[i].A };
            int cur = i;
            int guard = 0;
            while (guard++ < keep.Count + 2)
            {
                done[cur] = true;
                var end = keep[cur].B;
                if ((end - loop[0]).Length <= 2 * tol) break;
                loop.Add(end);
                int next = -1;
                if (from.TryGetValue(K(end), out var l)) foreach (int j in l) if (!done[j]) { next = j; break; }
                if (next < 0) { loop.Clear(); break; }
                cur = next;
            }
            if (loop.Count >= 3) res.Add(loop);
        }
        return res;
    }
}
