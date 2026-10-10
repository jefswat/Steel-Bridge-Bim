using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>A round hole through a plate, in the plate's 2D (u, w) coordinates.</summary>
public readonly record struct Hole(Vec2 Center, double Diameter);

/// <summary>
/// Builds plate solids with true holes. A plate is a 2D outline (u, w) with holes, given thickness t ∈ [t0, t1],
/// mapped to 3D by a function (u, w, t) → world. Planar plates use an affine map; girder plates use a map that
/// follows the girder path and are built in short strips so curved plates stay accurate.
/// </summary>
public static class PlateBuilder
{
    public const int HoleSegments = 16;

    /// <summary>Adds a prism with holes. <paramref name="walls"/>[i] = false suppresses the wall on outline edge i→i+1 (internal cut).</summary>
    public static void AddPrism(Mesh mesh, IReadOnlyList<Vec2> outline, IReadOnlyList<Hole> holes,
        Func<double, double, double, Vec3> map, Func<Vec2, double> t0, Func<Vec2, double> t1, IReadOnlyList<bool>? walls = null)
    {
        int triStart = mesh.TriangleCount;
        AddPrismCore(mesh, outline, holes, map, t0, t1, walls);
        // A closed prism (no suppressed walls) through an affine, orthonormal map is an exact extrusion.
        if (walls == null || walls.All(x => x)) mesh.Describe(Extrusion(outline, holes, map, t0, t1), triStart);
    }

    /// <summary>The prism as an exact extrusion, or null if the map bends it, varies its thickness or distorts its holes.</summary>
    private static ExtrudedSolid? Extrusion(IReadOnlyList<Vec2> outline, IReadOnlyList<Hole> holes,
        Func<double, double, double, Vec3> map, Func<Vec2, double> t0, Func<Vec2, double> t1)
    {
        var bottom = outline.Select(q => map(q.X, q.Y, t0(q))).ToArray();
        var top = outline.Select(q => map(q.X, q.Y, t1(q))).ToArray();
        var hc = new List<(Vec3, double)>(holes.Count);
        foreach (var h in holes)
        {
            var c = map(h.Center.X, h.Center.Y, t0(h.Center));
            double r = h.Diameter / 2;
            var px = map(h.Center.X + r, h.Center.Y, t0(h.Center));
            var py = map(h.Center.X, h.Center.Y + r, t0(h.Center));
            // Plumb webs on a grade make (s, w) slightly sheared; the real holes are round, so accept a few percent.
            if (Math.Abs((px - c).Length - r) > 0.05 * r || Math.Abs((py - c).Length - r) > 0.05 * r || Math.Abs(Vec3.Dot(px - c, py - c)) > 0.1 * r * r) return null;
            hc.Add((c, h.Diameter));
        }
        return ExtrudedSolid.TryCreate(bottom, top, hc);
    }

    private static void AddPrismCore(Mesh mesh, IReadOnlyList<Vec2> outline, IReadOnlyList<Hole> holes,
        Func<double, double, double, Vec3> map, Func<Vec2, double> t0, Func<Vec2, double> t1, IReadOnlyList<bool>? walls)
    {
        var outer = outline.ToList();
        var wallFlags = walls?.ToList() ?? Enumerable.Repeat(true, outer.Count).ToList();
        if (MeshBuilder.SignedArea(outer) < 0)
        {
            outer.Reverse();
            // Edge i (i→i+1) becomes edge n-2-i after reversal.
            var wf = new List<bool>(wallFlags.Count);
            int n0 = wallFlags.Count;
            for (int i = 0; i < n0; i++) wf.Add(wallFlags[(n0 - 2 - i + n0) % n0]);
            wallFlags = wf;
        }

        // Handedness of the map: we want u × w to point toward +t.
        var c = Centroid(outer);
        double tm0 = t0(c), tm1 = t1(c);
        double tmid = (tm0 + tm1) / 2;
        var p = map(c.X, c.Y, tmid);
        var du = map(c.X + 1e-4, c.Y, tmid) - p;
        var dw = map(c.X, c.Y + 1e-4, tmid) - p;
        var dt = map(c.X, c.Y, tmid + 1e-4) - p;
        bool flip = Vec3.Dot(Vec3.Cross(du, dw), dt) * Math.Sign(tm1 - tm0 == 0 ? 1 : tm1 - tm0) < 0;

        var holeLoops = holes.Select(h => Circle(h)).ToList();
        var (verts, tris) = TriangulateWithHoles(outer, holeLoops);

        void Tri(Vec3 a, Vec3 b, Vec3 cc)
        {
            int ia = mesh.Add(a), ib = mesh.Add(b), ic = mesh.Add(cc);
            if (flip) mesh.Tri(ia, ic, ib); else mesh.Tri(ia, ib, ic);
        }
        Vec3 Top(Vec2 q) => map(q.X, q.Y, t1(q));
        Vec3 Bot(Vec2 q) => map(q.X, q.Y, t0(q));

        // Faces: shared vertex buffers for compactness.
        int baseTop = mesh.Positions.Count;
        foreach (var v in verts) mesh.Add(Top(v));
        int baseBot = mesh.Positions.Count;
        foreach (var v in verts) mesh.Add(Bot(v));
        foreach (var (a, b, cc) in tris)
        {
            if (flip) { mesh.Tri(baseTop + a, baseTop + cc, baseTop + b); mesh.Tri(baseBot + a, baseBot + b, baseBot + cc); }
            else { mesh.Tri(baseTop + a, baseTop + b, baseTop + cc); mesh.Tri(baseBot + a, baseBot + cc, baseBot + b); }
        }

        // Walls: outer (CCW) and holes (CW) — quad (a0, b0, b1, a1) faces outward for both.
        void Walls(IReadOnlyList<Vec2> loop, IReadOnlyList<bool>? flags)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                if (flags != null && !flags[i]) continue;
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                var a0 = Bot(a);
                var b0 = Bot(b);
                var b1 = Top(b);
                var a1 = Top(a);
                Tri(a0, b0, b1);
                Tri(a0, b1, a1);
            }
        }
        Walls(outer, wallFlags);
        foreach (var h in holeLoops) Walls(h, null);
    }

    /// <summary>Planar plate in a frame: outline in (x, y), thickness along z from z0 to z1.</summary>
    public static void AddPlanar(Mesh mesh, IReadOnlyList<Vec2> outline, IReadOnlyList<Hole> holes, Frame3 f, double z0, double z1) =>
        AddPrism(mesh, outline, holes, (u, w, t) => f.ToWorld(u, w, t), _ => z0, _ => z1);

    /// <summary>
    /// Long plate along a girder: u = distance s, w = transverse coordinate. The outline is w ∈ [w0(s), w1(s)] for
    /// s ∈ [s0, s1]; thickness t ∈ [t0(s), t1(s)]. Built in strips at <paramref name="breaks"/> (path samples and
    /// taper points), skipping breaks that would cut a hole.
    /// </summary>
    public static void AddLongPlate(Mesh mesh, double s0, double s1, IEnumerable<double> breaks,
        Func<double, double> w0, Func<double, double> w1, Func<double, double> t0, Func<double, double> t1,
        IReadOnlyList<Hole> holes, Func<double, double, double, Vec3> map)
    {
        // Cut between hole columns as well, so each strip has one column of holes; then cut between hole rows so each
        // cell holds at most one hole. Triangulation stays linear in the number of holes.
        var holeCols = holes.Select(h => h.Center.X).Distinct().OrderBy(x => x).ToList();
        var colCuts = holeCols.Zip(holeCols.Skip(1), (x, y) => (x + y) / 2);
        var cuts = breaks.Concat(colCuts).Where(b => b > s0 + 1e-6 && b < s1 - 1e-6)
            .Where(b => !holes.Any(h => Math.Abs(b - h.Center.X) < h.Diameter / 2 + 1e-3))
            .Append(s0).Append(s1).Distinct().OrderBy(x => x).ToList();
        var stripTri = new List<int>();
        for (int k = 0; k + 1 < cuts.Count; k++)
        {
            stripTri.Add(mesh.TriangleCount);
            double a = cuts[k], b = cuts[k + 1];
            var hs = holes.Where(h => h.Center.X > a && h.Center.X < b).ToList();
            double ta0 = t0(a), tb0 = t0(b), ta1 = t1(a), tb1 = t1(b);
            double L = b - a;
            Func<Vec2, double> T0 = q => ta0 + (tb0 - ta0) * (q.X - a) / L;
            Func<Vec2, double> T1 = q => ta1 + (tb1 - ta1) * (q.X - a) / L;
            bool endA = a <= s0 + 1e-9, endB = b >= s1 - 1e-9;

            var rows = hs.Select(h => h.Center.Y).Distinct().OrderBy(x => x).ToList();
            double wLo = Math.Max(w0(a), w0(b)), wHi = Math.Min(w1(a), w1(b));
            var wCuts = rows.Zip(rows.Skip(1), (x, y) => (x + y) / 2)
                .Where(c => c > wLo + 1e-6 && c < wHi - 1e-6 && !hs.Any(h => Math.Abs(c - h.Center.Y) < h.Diameter / 2 + 1e-3)).ToList();
            if (wCuts.Count == 0)
            {
                var outline = new[] { new Vec2(a, w0(a)), new Vec2(b, w0(b)), new Vec2(b, w1(b)), new Vec2(a, w1(a)) };
                AddPrismCore(mesh, outline, hs, map, T0, T1, new[] { true, endB, true, endA });
                continue;
            }
            // Cells between w-cuts (cut lines are constant w; the first/last cells keep the tapered edges).
            var bounds = new List<double> { double.NaN };
            bounds.AddRange(wCuts);
            bounds.Add(double.NaN);
            for (int c = 0; c + 1 < bounds.Count; c++)
            {
                bool first = c == 0, last = c + 1 == bounds.Count - 1;
                double lo = bounds[c], hi = bounds[c + 1];
                var outline = new[]
                {
                    new Vec2(a, first ? w0(a) : lo), new Vec2(b, first ? w0(b) : lo),
                    new Vec2(b, last ? w1(b) : hi), new Vec2(a, last ? w1(a) : hi),
                };
                var cellHoles = hs.Where(h => (first || h.Center.Y > lo) && (last || h.Center.Y < hi)).ToList();
                AddPrismCore(mesh, outline, cellHoles, map, T0, T1, new[] { first, endB, last, endA });
            }
        }
        stripTri.Add(mesh.TriangleCount);
        DescribeLongPlate(mesh, cuts, stripTri, w0, w1, t0, t1, holes, map);
    }

    /// <summary>
    /// Records the long plate as few exact extrusions: consecutive strips are merged greedily while they stay flat
    /// (within <see cref="ExtrudedSolid.Tolerance"/>) and of constant thickness. Curved or thickness-tapered stretches
    /// leave triangles undescribed, so exporters fall back to the mesh for that part.
    /// </summary>
    private static void DescribeLongPlate(Mesh mesh, List<double> cuts, List<int> stripTri,
        Func<double, double> w0, Func<double, double> w1, Func<double, double> t0, Func<double, double> t1,
        IReadOnlyList<Hole> holes, Func<double, double, double, Vec3> map)
    {
        int strips = cuts.Count - 1;
        ExtrudedSolid? Fit(int k0, int k1)
        {
            double a = cuts[k0], b = cuts[k1 + 1];
            double ta = t0(a), tb = t1(a);
            for (int k = k0; k <= k1 + 1; k++)
                if (Math.Abs(t0(cuts[k]) - ta) > 1e-6 || Math.Abs(t1(cuts[k]) - tb) > 1e-6) return null;
            var q = new List<Vec2>();
            for (int k = k0; k <= k1 + 1; k++) q.Add(new Vec2(cuts[k], w0(cuts[k])));
            for (int k = k1 + 1; k >= k0; k--) q.Add(new Vec2(cuts[k], w1(cuts[k])));
            var hs = holes.Where(h => h.Center.X > a && h.Center.X < b).ToList();
            return Extrusion(q, hs, map, _ => ta, _ => tb);
        }
        int k0 = 0;
        while (k0 < strips)
        {
            ExtrudedSolid? best = null;
            int k1 = k0;
            for (int j = k0; j < strips; j++)
            {
                var s = Fit(k0, j);
                if (s == null) break;
                best = s;
                k1 = j;
            }
            if (best != null)
            {
                mesh.DescribeRange(best, stripTri[k1 + 1] - stripTri[k0]);
                k0 = k1 + 1;
            }
            else k0++;
        }
    }

    public static Vec2[] Circle(Hole h, int n = HoleSegments)
    {
        // Clockwise (holes), circumscribed so the polygon contains the true circle.
        double r = h.Diameter / 2 / Math.Cos(Math.PI / n);
        var pts = new Vec2[n];
        for (int i = 0; i < n; i++) pts[i] = h.Center + Vec2.FromAngle(-2 * Math.PI * i / n) * r;
        return pts;
    }

    private static Vec2 Centroid(IReadOnlyList<Vec2> p)
    {
        var c = Vec2.Zero;
        foreach (var q in p) c += q;
        return c / p.Count;
    }

    /// <summary>
    /// Triangulates a CCW outer polygon with CW holes by bridging each hole to the outer boundary (Eberly) and
    /// ear clipping the merged polygon. Returns the merged vertex list and CCW triangles into it.
    /// </summary>
    public static (List<Vec2> verts, List<(int, int, int)> tris) TriangulateWithHoles(IReadOnlyList<Vec2> outer, IReadOnlyList<Vec2[]> holes)
    {
        var poly = outer.ToList();
        foreach (var hole in holes.OrderByDescending(h => h.Max(p => p.X)))
        {
            var h = hole.ToList();
            if (MeshBuilder.SignedArea(h) > 0) h.Reverse();
            int mi = 0;
            for (int i = 1; i < h.Count; i++) if (h[i].X > h[mi].X) mi = i;
            var m = h[mi];

            // Ray from M toward +x: nearest edge intersection.
            double bestX = double.MaxValue;
            int bestEdge = -1;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                if ((a.Y > m.Y) == (b.Y > m.Y)) continue;
                double x = a.X + (m.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (x >= m.X - 1e-12 && x < bestX) { bestX = x; bestEdge = i; }
            }
            int pi;
            if (bestEdge < 0)
            {
                pi = 0;
                double d = double.MaxValue;
                for (int i = 0; i < poly.Count; i++) { double dd = (poly[i] - m).LengthSquared; if (dd < d) { d = dd; pi = i; } }
            }
            else
            {
                // Bridge horizontally to the nearest boundary hit by the +x ray, inserting that point into the boundary.
                // A horizontal bridge can't cross anything: holes further right are already merged (the ray hits them
                // first), and holes not yet merged have their rightmost point at or left of this one.
                int e0 = bestEdge, e1 = (bestEdge + 1) % poly.Count;
                var I = new Vec2(bestX, m.Y);
                if ((poly[e0] - I).LengthSquared < 1e-20) pi = e0;
                else if ((poly[e1] - I).LengthSquared < 1e-20) pi = e1;
                else
                {
                    poly.Insert(e0 + 1, I);
                    pi = e0 + 1;
                }
            }
            var merged = new List<Vec2>(poly.Count + h.Count + 2);
            for (int i = 0; i <= pi; i++) merged.Add(poly[i]);
            for (int k = 0; k <= h.Count; k++) merged.Add(h[(mi + k) % h.Count]);
            merged.Add(poly[pi]);
            for (int i = pi + 1; i < poly.Count; i++) merged.Add(poly[i]);
            poly = merged;
        }
        return (poly, EarClip(poly));
    }

    private static bool InTri(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
    {
        double d1 = Vec2.Cross(b - a, p - a), d2 = Vec2.Cross(c - b, p - b), d3 = Vec2.Cross(a - c, p - c);
        bool neg = d1 < -1e-14 || d2 < -1e-14 || d3 < -1e-14, pos = d1 > 1e-14 || d2 > 1e-14 || d3 > 1e-14;
        return !(neg && pos);
    }

    /// <summary>Ear clipping tolerant of the duplicated bridge vertices (CCW polygon).</summary>
    private static List<(int, int, int)> EarClip(List<Vec2> p)
    {
        var tris = new List<(int, int, int)>();
        var idx = Enumerable.Range(0, p.Count).ToList();
        int guard = 0;
        while (idx.Count > 3 && guard++ < 100000)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                var a = p[ia];
                var b = p[ib];
                var c = p[ic];
                if (Vec2.Cross(b - a, c - b) <= 1e-15) continue;
                bool blocked = false;
                foreach (int j in idx)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    var q = p[j];
                    if ((q - a).LengthSquared < 1e-20 || (q - b).LengthSquared < 1e-20 || (q - c).LengthSquared < 1e-20) continue;
                    if (StrictlyInside(q, a, b, c)) { blocked = true; break; }
                }
                if (blocked) continue;
                tris.Add((ia, ib, ic));
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped)
            {
                // Remove a degenerate (collinear) vertex and continue.
                int k = Enumerable.Range(0, idx.Count).OrderBy(i =>
                    Math.Abs(Vec2.Cross(p[idx[i]] - p[idx[(i + idx.Count - 1) % idx.Count]], p[idx[(i + 1) % idx.Count]] - p[idx[i]]))).First();
                idx.RemoveAt(k);
            }
        }
        if (idx.Count == 3 && Vec2.Cross(p[idx[1]] - p[idx[0]], p[idx[2]] - p[idx[1]]) > 0) tris.Add((idx[0], idx[1], idx[2]));
        return tris;
    }

    private static bool StrictlyInside(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
    {
        double d1 = Vec2.Cross(b - a, p - a), d2 = Vec2.Cross(c - b, p - b), d3 = Vec2.Cross(a - c, p - c);
        return d1 > 1e-14 && d2 > 1e-14 && d3 > 1e-14;
    }

    /// <summary>Clips a convex or simple polygon by the half-plane dot(p, n) ≥ c (Sutherland–Hodgman).</summary>
    public static List<Vec2> ClipHalfPlane(IReadOnlyList<Vec2> poly, Vec2 n, double c)
    {
        var res = new List<Vec2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            double da = Vec2.Dot(a, n) - c, db = Vec2.Dot(b, n) - c;
            if (da >= 0) res.Add(a);
            if ((da >= 0) != (db >= 0)) res.Add(a + (b - a) * (da / (da - db)));
        }
        return res;
    }
}
