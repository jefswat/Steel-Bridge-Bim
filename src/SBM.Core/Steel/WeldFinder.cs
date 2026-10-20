using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Generic T-joint fillet weld lines between two plate solids (meshes in the same coordinates): where an EDGE FACE of plate B lies on a face of part A
/// (normals opposite within <c>angleTolDeg</c>, the edge within <c>distTol</c> of the A plane and inside an A triangle), the weld
/// roots are the perimeter edges of B's plate faces whose edge face sits on A (one bead per side of B = "both sides"), only along the stretches that
/// really lie on A; the bead legs run along A's face away from B and along B's face away from A. Used where the plans give a weld symbol on a joint
/// whose extent is "full contact" (no length dimensioned). Pure geometry: nothing is moved; the length of edges that are only partly on A is returned
/// (overhang).
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
    /// triangulated with extra vertices on the contact edge (holes) shares no exact edge with the contact face; the plate face is then found by collinear overlap. The same option
    /// relaxes "inside an A triangle" to "inside the box of A" (plates placed analytically, long strips, a few mm of chord error).</summary>
    public static List<Bead> TJoint(Mesh a, Mesh b, double distTol, double angleTolDeg = 3, bool bothSides = true, Vec3? side = null, bool tJunctions = false) =>
        TJoint(a, b, distTol, out _, angleTolDeg, bothSides, side is { } sd ? (_, ns) => Vec3.Dot(ns, sd) > 0 : null, tJunctions);

    /// <summary>As <see cref="TJoint(Mesh, Mesh, double, double, bool, Vec3?, bool)"/>; <paramref name="overhang"/> = total length of the plate edges that sit
    /// on A over part of their length only (the part off A, where the plate edge face overhangs A or does not touch it). <paramref name="keep"/>
    /// (used when <paramref name="bothSides"/> is false) decides per edge, from its start point and the outward normal of the B plate face on that
    /// side, whether that side is welded (a single-sided weld symbol).</summary>
    public static List<Bead> TJoint(Mesh a, Mesh b, double distTol, out double overhang, double angleTolDeg = 3, bool bothSides = true, Func<Vec3, Vec3, bool>? keep = null,
        bool tJunctions = false)
    {
        overhang = 0;
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

        // Vertex ids (positions merged within 20 um) and the triangles on each edge.
        var cell = new Dictionary<(long, long, long), List<int>>();
        var verts = new List<Vec3>();
        const double Cell = 1e-4, Merge = 2e-5;
        int Id(Vec3 p)
        {
            long cx = (long)Math.Floor(p.X / Cell), cy = (long)Math.Floor(p.Y / Cell), cz = (long)Math.Floor(p.Z / Cell);
            for (long x = cx - 1; x <= cx + 1; x++)
                for (long y = cy - 1; y <= cy + 1; y++)
                    for (long z = cz - 1; z <= cz + 1; z++)
                        if (cell.TryGetValue((x, y, z), out var l))
                            foreach (int i in l) if ((verts[i] - p).Length <= Merge) return i;
            verts.Add(p);
            if (!cell.TryGetValue((cx, cy, cz), out var own)) cell[(cx, cy, cz)] = own = new List<int>();
            own.Add(verts.Count - 1);
            return verts.Count - 1;
        }
        var edges = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < tb.Count; i++)
        {
            int ia = Id(tb[i].A), ib = Id(tb[i].B), ic = Id(tb[i].C);
            foreach (var (u, v) in new[] { (ia, ib), (ib, ic), (ic, ia) })
            {
                if (u == v) continue;
                var key = u < v ? (u, v) : (v, u);
                if (!edges.TryGetValue(key, out var l)) edges[key] = l = new List<int>();
                l.Add(i);
            }
        }
        // Perimeter edges of the plate: between a plate-face triangle (normal +-nPlate) and an edge-face triangle. The weld root is that edge where the
        // edge face sits on A (opposite normal, within the distance tolerance, inside an A triangle); only the stretches on A are welded.
        foreach (var ((u, v), tris) in edges)
        {
            var p0 = verts[u]; var q0 = verts[v];
            Vec3 nb, ns;
            if (tris.Count == 2)
            {
                int ip = -1, ie = -1;
                foreach (int ti in tris)
                {
                    double dn = Math.Abs(Vec3.Dot(tb[ti].N, nPlate));
                    if (dn > 0.97) ip = ip < 0 ? ti : -2;
                    else ie = ie < 0 ? ti : -2;                              // any other face = an edge face (cut square or oblique to the plate)
                }
                if (ip < 0 || ie < 0) continue;
                nb = tb[ie].N;                                               // B edge face normal, pointing into A
                ns = tb[ip].N;                                               // B plate face normal (outward)
            }
            else if (tJunctions && tris.Count == 1 && Math.Abs(Vec3.Dot(tb[tris[0]].N, nPlate)) <= 0.97
                     && CollinearNeighbour(tb, new HashSet<int> { tris[0] }, p0, q0, out var nsT) && Math.Abs(Vec3.Dot(nsT, nPlate)) > 0.97)
            {
                nb = tb[tris[0]].N;                                          // edge face whose plate-face neighbour is split at T-junctions
                ns = nsT;
            }
            else continue;
            if (!bothSides && keep != null && !keep((p0 + q0) * 0.5, ns)) continue;
            double len = (q0 - p0).Length; if (len < 1e-5) continue;
            // Stretches on A: coarse samples (1/4" or finer), boundaries refined by bisection.
            int n = Math.Clamp((int)Math.Ceiling(len / 0.00635), 2, 400);
            var on = new bool[n + 1];
            for (int i = 0; i <= n; i++) on[i] = OnA(p0 + (q0 - p0) * ((double)i / n), nb);
            if (!on.Any(x => x)) continue;
            double Bisect(bool onAtLo, double s0, double s1)
            {
                for (int k = 0; k < 12; k++)
                {
                    double m = (s0 + s1) / 2;
                    if (OnA(p0 + (q0 - p0) * m, nb) == onAtLo) s0 = m; else s1 = m;
                }
                return onAtLo ? s0 : s1;
            }
            var runs = new List<(double, double)>();
            for (int i = 0; i <= n; i++)
            {
                if (!on[i]) continue;
                int j = i; while (j + 1 <= n && on[j + 1]) j++;
                double s0 = (double)i / n, s1 = (double)j / n;
                if (i > 0) s0 = Bisect(false, (double)(i - 1) / n, s0);
                if (j < n) s1 = Bisect(true, s1, (double)(j + 1) / n);
                runs.Add((s0, s1)); i = j;
            }
            var na = -nb;
            var ed = (q0 - p0) * (1 / len);
            var legA = ns - na * Vec3.Dot(ns, na); if (legA.Length < 1e-6) continue; legA = legA.Normalized();
            var legB = Vec3.Cross(ed, ns).Normalized(); if (Vec3.Dot(legB, na) < 0) legB = -legB;
            double covered = 0;
            foreach (var (s0, s1) in runs)
            {
                if ((s1 - s0) * len < 1e-4) continue;
                res.Add(new Bead(p0 + (q0 - p0) * s0, p0 + (q0 - p0) * s1, legA, legB));
                covered += (s1 - s0) * len;
            }
            if (len - covered > distTol) overhang += len - covered;
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
