using System.Runtime.CompilerServices;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Drawing.Projection;

public enum EdgeKind : byte
{
    /// <summary>Open edge (one face): always drawn.</summary>
    Boundary,
    /// <summary>Dihedral above the sharp angle (or non-manifold): always drawn.</summary>
    Sharp,
    /// <summary>Smooth edge between two faces (tessellated curved surface): drawn only where it is a silhouette for the view.</summary>
    Smooth,
}

/// <summary>A view-independent edge candidate in world coordinates; <see cref="N1"/>/<see cref="N2"/> = outward normals of the two faces (Smooth).</summary>
public readonly record struct FeatureEdge(Vec3 A, Vec3 B, EdgeKind Kind, Vec3 N1, Vec3 N2)
{
    /// <summary>Drawn in a view whose line of sight is <paramref name="d"/> (Smooth edges: one face toward the viewer, the other away).</summary>
    public bool Drawn(Vec3 d) => Kind != EdgeKind.Smooth || (Vec3.Dot(N1, d) < 0) != (Vec3.Dot(N2, d) < 0);
}

/// <summary>Feature edges of one mesh and whether the mesh is closed (closed meshes may skip back faces as occluders).</summary>
public sealed class MeshEdges
{
    public required FeatureEdge[] Edges { get; init; }
    public required bool Closed { get; init; }
    public bool FromRecipes { get; init; }
}

/// <summary>
/// View-independent feature-edge extraction (cached per mesh). Faces are triangles merged BY PLANE (coplanar neighbours and coplanar
/// triangles with the same plane are one face), so tessellation lines inside planar faces never appear; T-junctions (a vertex on
/// another triangle's edge) are resolved by a 1D coverage sweep along each edge line. Edges between faces are Sharp above the
/// dihedral threshold, else Smooth (silhouette candidates); edges of one face are Boundary. Meshes fully described by
/// <see cref="ExtrudedSolid"/> recipes use the exact profile polygon edges + sweep edges instead.
/// </summary>
public static class FeatureEdges
{
    private sealed class Cache { public readonly Dictionary<(double, bool, bool, int), MeshEdges> ByKey = new(); }
    private static readonly ConditionalWeakTable<Mesh, Cache> _cache = new();

    public static MeshEdges Of(Mesh m, double sharpDeg = 30, bool useRecipes = true, bool holes = true, int holeSides = 16)
    {
        var c = _cache.GetOrCreateValue(m);
        var key = (sharpDeg, useRecipes, holes, holeSides);
        lock (c)
            if (c.ByKey.TryGetValue(key, out var e)) return e;
        var r = useRecipes && m.HasExactSolids ? FromRecipes(m.Solids!, sharpDeg, holes, holeSides) : FromMesh(m, sharpDeg);
        lock (c) c.ByKey[key] = r;
        return r;
    }

    // ------------------------------------------------------------------ recipes

    public static MeshEdges FromRecipes(IEnumerable<ExtrudedSolid> solids, double sharpDeg, bool holes = true, int holeSides = 16)
    {
        double cosSharp = Math.Cos(sharpDeg * Math.PI / 180);
        var res = new List<FeatureEdge>();
        foreach (var s in solids)
        {
            var f = s.Frame;
            var E = s.Extrusion;
            double up = Math.Sign(Vec3.Dot(E, f.Z));
            if (up == 0) continue;
            var capBot = f.Z * -up;
            var capTop = f.Z * up;
            void Loop(IReadOnlyList<Vec2> poly, bool round)
            {
                int n = poly.Count;
                var B = new Vec3[n];
                for (int i = 0; i < n; i++) B[i] = f.ToWorld(poly[i].X, poly[i].Y, 0);
                var side = new Vec3[n];
                for (int i = 0; i < n; i++) side[i] = (Vec3.Cross(B[(i + 1) % n] - B[i], E) * up).Normalized();
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    res.Add(Classify(B[i], B[j], capBot, side[i], cosSharp));
                    res.Add(Classify(B[i] + E, B[j] + E, capTop, side[i], cosSharp));
                    res.Add(round ? new FeatureEdge(B[i], B[i] + E, EdgeKind.Smooth, side[(i - 1 + n) % n], side[i])
                                  : Classify(B[i], B[i] + E, side[(i - 1 + n) % n], side[i], cosSharp));
                }
            }
            Loop(s.Outline, false);
            if (holes)
                foreach (var h in s.Holes) Loop(PlateBuilder.Circle(h, holeSides), true);   // hole walls are cylinders: silhouettes only
        }
        return new MeshEdges { Edges = res.ToArray(), Closed = true, FromRecipes = true };
    }

    private static FeatureEdge Classify(Vec3 a, Vec3 b, Vec3 n1, Vec3 n2, double cosSharp) =>
        new(a, b, Vec3.Dot(n1, n2) >= cosSharp ? EdgeKind.Smooth : EdgeKind.Sharp, n1, n2);

    // ------------------------------------------------------------------ meshes

    public static MeshEdges FromMesh(Mesh m, double sharpDeg)
    {
        double cosSharp = Math.Cos(sharpDeg * Math.PI / 180);
        int nv = m.Positions.Count, nt = m.TriangleCount;
        if (nt == 0) return new MeshEdges { Edges = Array.Empty<FeatureEdge>(), Closed = false };
        var o = m.Positions[0];
        // 1. Weld vertices (0.01 mm), neighbour cells checked so a point near a cell border still finds its twin.
        const double Tol = 1e-5;
        var weld = new int[nv];
        var loc = new Vec3[nv];
        var cells = new Dictionary<(long, long, long), int>(nv);
        var reps = new List<Vec3>(nv);
        for (int i = 0; i < nv; i++)
        {
            var p = m.Positions[i] - o;
            loc[i] = p;
            long qx = (long)Math.Floor(p.X / Tol), qy = (long)Math.Floor(p.Y / Tol), qz = (long)Math.Floor(p.Z / Tol);
            int found = -1;
            if (cells.TryGetValue((qx, qy, qz), out int r0) && (reps[r0] - p).Length <= Tol) found = r0;
            else
                for (long dx = -1; dx <= 1 && found < 0; dx++)
                    for (long dy = -1; dy <= 1 && found < 0; dy++)
                        for (long dz = -1; dz <= 1 && found < 0; dz++)
                            if (cells.TryGetValue((qx + dx, qy + dy, qz + dz), out int r) && (reps[r] - p).Length <= Tol) found = r;
            if (found < 0)
            {
                found = reps.Count;
                reps.Add(p);
                cells.TryAdd((qx, qy, qz), found);
            }
            weld[i] = found;
        }
        int nw = reps.Count;

        // 2. Triangle normals / planes.
        var tn = new Vec3[nt];
        var toff = new double[nt];
        var ok = new bool[nt];
        for (int t = 0; t < nt; t++)
        {
            int a = weld[m.Indices[3 * t]], b = weld[m.Indices[3 * t + 1]], c = weld[m.Indices[3 * t + 2]];
            if (a == b || b == c || a == c) continue;
            var n = Vec3.Cross(reps[b] - reps[a], reps[c] - reps[a]);
            double l = n.Length;
            if (l < 1e-14) continue;
            tn[t] = n * (1 / l);
            toff[t] = Vec3.Dot(tn[t], reps[a]);
            ok[t] = true;
        }

        // 3. Directed edge entries sorted by undirected key.
        var keys = new long[3 * nt];
        var data = new int[3 * nt];   // tri * 2 + (direction a<b ? 0 : 1)
        int ne = 0;
        for (int t = 0; t < nt; t++)
        {
            if (!ok[t]) continue;
            for (int k = 0; k < 3; k++)
            {
                int a = weld[m.Indices[3 * t + k]], b = weld[m.Indices[3 * t + (k + 1) % 3]];
                int lo = Math.Min(a, b), hi = Math.Max(a, b);
                keys[ne] = (long)lo * nw + hi;
                data[ne] = t * 2 + (a < b ? 0 : 1);
                ne++;
            }
        }
        Array.Sort(keys, data, 0, ne);

        // 4. Plane groups: union of coplanar edge neighbours + same plane hash.
        var parent = new int[nt];
        for (int t = 0; t < nt; t++) parent[t] = t;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int x, int y) { x = Find(x); y = Find(y); if (x != y) parent[Math.Max(x, y)] = Math.Min(x, y); }
        bool Coplanar(int x, int y) => Vec3.Dot(tn[x], tn[y]) > 1 - 1e-9 && Math.Abs(toff[x] - toff[y]) < 1e-6;
        for (int i = 0; i < ne;)
        {
            int j = i + 1;
            while (j < ne && keys[j] == keys[i]) j++;
            for (int x = i; x < j; x++)
                for (int y = x + 1; y < j; y++)
                {
                    int ta = data[x] >> 1, tb = data[y] >> 1;
                    if (Coplanar(ta, tb)) Union(ta, tb);
                }
            i = j;
        }
        var planes = new Dictionary<(long, long, long, long), int>();
        for (int t = 0; t < nt; t++)
        {
            if (!ok[t]) continue;
            var n = tn[t];
            var pk = ((long)Math.Round(n.X * 1e5), (long)Math.Round(n.Y * 1e5), (long)Math.Round(n.Z * 1e5), (long)Math.Round(toff[t] * 1e5));
            if (planes.TryGetValue(pk, out int first)) Union(first, t); else planes[pk] = t;
        }

        // 5. Classify each undirected edge by the faces (groups) that still have it after cancelling coplanar pairs.
        var res = new List<FeatureEdge>();
        var leftovers = new List<(Vec3 P, Vec3 Q, int G)>();
        var parts = new List<(int g, int net)>(4);
        bool closed = true;
        for (int i = 0; i < ne;)
        {
            int j = i + 1;
            while (j < ne && keys[j] == keys[i]) j++;
            parts.Clear();
            for (int x = i; x < j; x++)
            {
                int g = Find(data[x] >> 1);
                int s = (data[x] & 1) == 0 ? 1 : -1;
                int k = parts.FindIndex(p => p.g == g);
                if (k < 0) parts.Add((g, s)); else parts[k] = (g, parts[k].net + s);
            }
            parts.RemoveAll(p => p.net == 0);
            long key = keys[i];
            int lo = (int)(key / nw), hi = (int)(key % nw);
            var A = reps[lo] + o; var B = reps[hi] + o;
            if (parts.Count == 1)
            {
                // Open edge or one side of a T-junction: resolved along the edge line below. Direction = traversal of the face.
                if (parts[0].net > 0) leftovers.Add((reps[lo], reps[hi], parts[0].g)); else leftovers.Add((reps[hi], reps[lo], parts[0].g));
            }
            else if (parts.Count == 2) res.Add(Pair(A, B, parts[0].g, parts[0].net, parts[1].g, parts[1].net));
            else if (parts.Count > 2) res.Add(new FeatureEdge(A, B, EdgeKind.Sharp, tn[parts[0].g], tn[parts[1].g]));
            i = j;
        }

        FeatureEdge Pair(Vec3 a, Vec3 b, int g1, int s1, int g2, int s2)
        {
            var n1 = tn[g1]; var n2 = tn[g2];
            if (Math.Sign(s1) == Math.Sign(s2)) n2 = -n2;   // faces oriented inconsistently: compare as if consistent
            return new FeatureEdge(a, b, Vec3.Dot(n1, n2) >= cosSharp ? EdgeKind.Smooth : EdgeKind.Sharp, n1, n2);
        }

        // 6. T-junctions: per supporting line, 1D coverage of each face (signed by traversal); intervals where exactly the
        //    same faces remain are classified like the edges above.
        if (leftovers.Count > 0)
        {
            var buckets = new Dictionary<(long, long, long, long, long, long), List<int>>();
            for (int k = 0; k < leftovers.Count; k++)
            {
                var (P, Q, _) = leftovers[k];
                var u = (Q - P).Normalized();
                double ax = Math.Abs(u.X), ay = Math.Abs(u.Y), az = Math.Abs(u.Z);
                double big = ax >= ay && ax >= az ? u.X : ay >= az ? u.Y : u.Z;
                if (big < 0) u = -u;
                var foot = P - u * Vec3.Dot(P, u);
                var bk = ((long)Math.Round(u.X * 1e4), (long)Math.Round(u.Y * 1e4), (long)Math.Round(u.Z * 1e4),
                    (long)Math.Round(foot.X * 1e4), (long)Math.Round(foot.Y * 1e4), (long)Math.Round(foot.Z * 1e4));
                if (!buckets.TryGetValue(bk, out var l)) buckets[bk] = l = new List<int>();
                l.Add(k);
            }
            foreach (var l in buckets.Values)
            {
                var (P0, Q0, _) = leftovers[l[0]];
                var u = (Q0 - P0).Normalized();
                double ax = Math.Abs(u.X), ay = Math.Abs(u.Y), az = Math.Abs(u.Z);
                if ((ax >= ay && ax >= az ? u.X : ay >= az ? u.Y : u.Z) < 0) u = -u;
                var basePt = P0 - u * Vec3.Dot(P0, u);
                var segs = l.Select(k => { var (P, Q, G) = leftovers[k]; double tp = Vec3.Dot(P, u), tq = Vec3.Dot(Q, u); return (t0: Math.Min(tp, tq), t1: Math.Max(tp, tq), sg: tq > tp ? 1 : -1, G); }).ToList();
                var ts = segs.SelectMany(s => new[] { s.t0, s.t1 }).OrderBy(x => x).ToList();
                var cuts = new List<double>();
                foreach (double t in ts) if (cuts.Count == 0 || t - cuts[^1] > 1e-7) cuts.Add(t);
                (EdgeKind kind, int g1, int g2, Vec3 n1, Vec3 n2)? prev = null;
                double runStart = 0;
                void Flush(double tEnd)
                {
                    if (prev is not { } pv || tEnd - runStart < 1e-7) return;
                    res.Add(new FeatureEdge(basePt + u * runStart + o, basePt + u * tEnd + o, pv.kind, pv.n1, pv.n2));
                    if (pv.kind == EdgeKind.Boundary) closed = false;
                }
                for (int c = 0; c + 1 < cuts.Count; c++)
                {
                    double ta = cuts[c], tb = cuts[c + 1], tm = (ta + tb) / 2;
                    parts.Clear();
                    foreach (var s in segs)
                    {
                        if (s.t0 > tm || s.t1 < tm) continue;
                        int k = parts.FindIndex(p => p.g == s.G);
                        if (k < 0) parts.Add((s.G, s.sg)); else parts[k] = (s.G, parts[k].net + s.sg);
                    }
                    parts.RemoveAll(p => p.net == 0);
                    (EdgeKind kind, int g1, int g2, Vec3 n1, Vec3 n2)? cur = null;
                    if (parts.Count == 1) cur = (EdgeKind.Boundary, parts[0].g, -1, tn[parts[0].g], tn[parts[0].g]);
                    else if (parts.Count == 2)
                    {
                        var fe = Pair(Vec3.Zero, Vec3.Zero, parts[0].g, parts[0].net, parts[1].g, parts[1].net);
                        cur = (fe.Kind, parts[0].g, parts[1].g, fe.N1, fe.N2);
                    }
                    else if (parts.Count > 2) cur = (EdgeKind.Sharp, parts[0].g, parts[1].g, tn[parts[0].g], tn[parts[1].g]);
                    bool same = cur != null && prev != null && cur.Value.kind == prev.Value.kind && cur.Value.g1 == prev.Value.g1 && cur.Value.g2 == prev.Value.g2;
                    if (!same) { Flush(ta); prev = cur; runStart = ta; }
                }
                if (cuts.Count > 0) Flush(cuts[^1]);
            }
        }
        return new MeshEdges { Edges = res.ToArray(), Closed = closed };
    }
}
