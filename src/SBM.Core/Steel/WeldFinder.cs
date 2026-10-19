using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Generic T-joint fillet weld lines between two plate solids (meshes in the same coordinates): where an EDGE FACE of plate B lies on a face of part A
/// (normals opposite within <c>angleTolDeg</c>, every vertex of the B face within <c>distTol</c> of the A plane and inside an A triangle), the weld
/// roots are the boundary edges of that contact region that run along B's two large plate faces (one bead per side of B = "both sides"); the bead
/// legs run along A's face away from B and along B's face away from A. Used where the plans give a weld symbol on a joint whose extent is "full
/// contact" (no length dimensioned). Pure geometry: nothing is moved; edges that only partly sit on A are skipped.
/// </summary>
public static class WeldFinder
{
    public readonly record struct Bead(Vec3 P0, Vec3 P1, Vec3 LegOnA, Vec3 LegOnB);

    private readonly record struct Tri(Vec3 A, Vec3 B, Vec3 C, Vec3 N, double Area);

    private static List<Tri> Tris(Mesh m)
    {
        var l = new List<Tri>(m.TriangleCount);
        for (int i = 0; i + 2 < m.Indices.Count; i += 3)
        {
            var a = m.Positions[m.Indices[i]]; var b = m.Positions[m.Indices[i + 1]]; var c = m.Positions[m.Indices[i + 2]];
            var n = Vec3.Cross(b - a, c - a);
            double ar = n.Length / 2;
            if (ar < 1e-12) continue;
            l.Add(new Tri(a, b, c, n * (1 / n.Length), ar));
        }
        return l;
    }

    /// <summary>Weld beads of plate <paramref name="b"/> onto part <paramref name="a"/>; <paramref name="bothSides"/> false = only the side whose B face
    /// normal is closest to <paramref name="side"/> (when given). <paramref name="tJunctions"/> (default false = unchanged behaviour): a plate whose large faces are
    /// triangulated with extra vertices on the contact edge (holes) shares no exact edge with the contact face; the adjacent face is then found by collinear overlap. The same option
    /// relaxes "inside an A triangle" to "inside the box of A" (plates placed analytically, long strips, a few mm of chord error).</summary>
    public static List<Bead> TJoint(Mesh a, Mesh b, double distTol, double angleTolDeg = 3, bool bothSides = true, Vec3? side = null, bool tJunctions = false)
    {
        var res = new List<Bead>();
        var ta = Tris(a); var tb = Tris(b);
        var aBox = a.Bounds();
        if (ta.Count == 0 || tb.Count == 0) return res;
        var bb = b.Bounds();
        var lo = bb.Min - new Vec3(distTol, distTol, distTol); var hi = bb.Max + new Vec3(distTol, distTol, distTol);
        ta = ta.Where(t => Overlap(t, lo, hi)).ToList();
        if (ta.Count == 0) return res;
        double cosTol = Math.Cos(angleTolDeg * Math.PI / 180);
        // Plate normal of B: the normal direction with the largest area (both orientations together).
        var groups = new List<(Vec3 n, double area)>();
        foreach (var t in tb)
        {
            int k = groups.FindIndex(g => Math.Abs(Vec3.Dot(g.n, t.N)) > 0.999);
            if (k < 0) groups.Add((t.N, t.Area)); else groups[k] = (groups[k].n, groups[k].area + t.Area);
        }
        var nPlate = groups.OrderByDescending(g => g.area).First().n;

        bool OnA(Vec3 p, Vec3 nb)
        {
            foreach (var t in ta)
            {
                if (Vec3.Dot(t.N, nb) > -cosTol) continue;
                double d = Vec3.Dot(p - t.A, t.N);
                if (Math.Abs(d) > distTol) continue;
                if (tJunctions ? WithinBox(p, aBox, 0.0065) : Inside(t, p - t.N * d, distTol * 0.5)) return true;
            }
            return false;
        }
        // Contact triangles of B.
        var contact = new List<int>();
        for (int i = 0; i < tb.Count; i++)
        {
            var t = tb[i];
            if (Math.Abs(Vec3.Dot(t.N, nPlate)) > 0.5) continue;             // a plate face, not an edge face
            if (OnA((t.A + t.B + t.C) * (1.0 / 3), t.N) && OnA(t.A, t.N) && OnA(t.B, t.N) && OnA(t.C, t.N)) contact.Add(i);
        }
        if (contact.Count == 0) return res;
        // Edge use counts among the contact triangles (position keys) and the B triangles touching each edge.
        static (long, long, long) K(Vec3 p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6), (long)Math.Round(p.Z * 1e6));
        static ((long, long, long), (long, long, long)) E(Vec3 p, Vec3 q) { var a = K(p); var b = K(q); return a.CompareTo(b) <= 0 ? (a, b) : (b, a); }
        var cset = new HashSet<int>(contact);
        var use = new Dictionary<((long, long, long), (long, long, long)), (int count, Vec3 p, Vec3 q, int tri)>();
        foreach (int i in contact)
        {
            var t = tb[i];
            foreach (var (p, q) in new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) })
            {
                var key = E(p, q);
                use[key] = use.TryGetValue(key, out var u) ? (u.count + 1, u.p, u.q, u.tri) : (1, p, q, i);
            }
        }
        var other = new Dictionary<((long, long, long), (long, long, long)), Vec3>();
        for (int i = 0; i < tb.Count; i++)
        {
            if (cset.Contains(i)) continue;
            var t = tb[i];
            foreach (var (p, q) in new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) })
            {
                var key = E(p, q);
                if (use.ContainsKey(key)) other[key] = t.N;
            }
        }
        foreach (var (key, u) in use)
        {
            if (u.count != 1) continue;
            if (!other.TryGetValue(key, out var ns) && !(tJunctions && CollinearNeighbour(tb, cset, u.p, u.q, out ns))) continue;
            if (Math.Abs(Vec3.Dot(ns, nPlate)) < 0.97) continue;             // along a plate face only (not the plate ends)
            if (!bothSides && side is { } sd && Vec3.Dot(ns, sd) <= 0) continue;
            var nb = tb[u.tri].N;                                             // B contact face normal, pointing into A
            var na = -nb;
            var ed = (u.q - u.p); double len = ed.Length; if (len < 1e-5) continue;
            ed *= 1 / len;
            var legA = ns - na * Vec3.Dot(ns, na); if (legA.Length < 1e-6) continue; legA = legA.Normalized();
            var legB = Vec3.Cross(ed, ns).Normalized(); if (Vec3.Dot(legB, na) < 0) legB = -legB;
            res.Add(new Bead(u.p, u.q, legA, legB));
        }
        return res;
    }

    /// <summary>Normal of a non-contact triangle with an edge collinear with and overlapping the segment p-q (T-junction fallback).</summary>
    private static bool CollinearNeighbour(List<Tri> tb, HashSet<int> contact, Vec3 p, Vec3 q, out Vec3 n)
    {
        n = default;
        var d = q - p; double len = d.Length; if (len < 1e-9) return false;
        d *= 1 / len;
        const double tol = 1e-5;
        bool On(Vec3 x, out double t) { var r = x - p; t = Vec3.Dot(r, d); return (r - d * t).Length < tol; }
        for (int i = 0; i < tb.Count; i++)
        {
            if (contact.Contains(i)) continue;
            var t0 = tb[i];
            foreach (var (u, v) in new[] { (t0.A, t0.B), (t0.B, t0.C), (t0.C, t0.A) })
            {
                if (!On(u, out double tu) || !On(v, out double tv)) continue;
                double lo = Math.Max(Math.Min(tu, tv), 0), hi = Math.Min(Math.Max(tu, tv), len);
                if (hi - lo < len * 0.05) continue;
                n = t0.N; return true;
            }
        }
        return false;
    }

    /// <summary>Adds fillet beads to a mesh; returns the total root length.</summary>
    public static double AddBeads(Mesh m, IEnumerable<Bead> beads, double size)
    {
        double len = 0;
        foreach (var b in beads)
        {
            if ((b.P1 - b.P0).Length < 1e-6) continue;
            MeshBuilder.AddFilletWeld(m, b.P0, b.P1, b.LegOnA, b.LegOnB, size);
            len += (b.P1 - b.P0).Length;
        }
        return len;
    }

    private static bool WithinBox(Vec3 p, Box3 box, double tol) =>
        p.X >= box.Min.X - tol && p.X <= box.Max.X + tol && p.Y >= box.Min.Y - tol && p.Y <= box.Max.Y + tol && p.Z >= box.Min.Z - tol && p.Z <= box.Max.Z + tol;

    private static bool Overlap(Tri t, Vec3 lo, Vec3 hi)
    {
        double x0 = Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)), x1 = Math.Max(t.A.X, Math.Max(t.B.X, t.C.X));
        double y0 = Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)), y1 = Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y));
        double z0 = Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)), z1 = Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z));
        return x1 >= lo.X && x0 <= hi.X && y1 >= lo.Y && y0 <= hi.Y && z1 >= lo.Z && z0 <= hi.Z;
    }

    /// <summary>Point (in the triangle's plane) inside the triangle grown by <paramref name="tol"/>.</summary>
    private static bool Inside(Tri t, Vec3 p, double tol)
    {
        foreach (var (u, v) in new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) })
        {
            var e = v - u;
            var inward = Vec3.Cross(t.N, e);
            double l = inward.Length; if (l < 1e-12) continue;
            if (Vec3.Dot(p - u, inward) / l < -tol) return false;
        }
        return true;
    }
}
