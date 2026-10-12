using System.Text;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Solids;
using static SBM.IO.Ifc.StepWriter;

namespace SBM.IO.Ifc;

/// <summary>
/// Geometry for <see cref="IfcGeometryMode.ReferenceView"/> (IFC 4.3 Alignment-based View, AbV). The view forbids
/// IfcExtrudedAreaSolid, boolean / clipping results, half spaces and IfcPlane (see usBrSpec_AbV Exclusions.ids), so the
/// solids the Parametric export writes that way become planar-faced boundary representations:
/// <list type="bullet">
/// <item>recorded extrusions (<see cref="ExtrudedSolid"/>: plates with bolt holes, angles, boxes, straight girder plates)
///   -> IfcPolygonalFaceSet: one IfcIndexedPolygonalFaceWithVoids per end cap (holes stay holes, as regular polygons) and one
///   quad per side / hole wall. Exact for the outline; each hole circle becomes an equal-area polygon (same volume as the
///   Parametric solid) with as many sides as an inscribed polygon of sagitta &lt;= the chord tolerance needs.</item>
/// <item>prestressed beams with a true-arc profile (IfcExtrudedAreaSolid clipped by two IfcHalfSpaceSolid in Parametric)
///   -> one IfcPolygonalFaceSet: arcs tessellated to the chord tolerance, swept along the beam axis and cut EXACTLY by the
///   two skewed end planes (each side face is a planar quad, each end cap a planar polygon in its end plane).</item>
/// </list>
/// Triangle meshes (IfcTriangulatedFaceSet), swept disks (bars, strands, sheathing) and the mapped bolt / stud types are
/// already allowed and are written by <see cref="IfcExporter"/> unchanged.
/// </summary>
internal sealed class IfcReferenceViewGeometry
{
    /// <summary>Default chord tolerance (m): 1/32 in = 0.79375 mm.</summary>
    public const double DefaultChordTolerance = 0.0254 / 32;

    private readonly StepWriter _w;
    private readonly Func<Vec3, string> _point;
    private readonly double _tol;

    /// <param name="point">Formats a world point (m) as an IFC coordinate triple in project units relative to the base point.</param>
    /// <param name="chordTolerance">Maximum sagitta (m) between a true arc / hole circle and its polygon.</param>
    public IfcReferenceViewGeometry(StepWriter w, Func<Vec3, string> point, double chordTolerance)
    {
        _w = w;
        _point = point;
        _tol = chordTolerance > 0 ? chordTolerance : DefaultChordTolerance;
    }

    public double ChordTolerance => _tol;
    public int FaceSets { get; private set; }

    /// <summary>Segments of a full circle of radius <paramref name="r"/> with sagitta &lt;= <paramref name="tol"/>, at least 8
    /// (a 15/16 in bolt hole at 1/32 in: 9). Hole walls dominate the file size, so no rounding up beyond the tolerance.</summary>
    public static int CircleSegments(double r, double tol)
    {
        int n = tol >= r ? 8 : (int)Math.Ceiling(Math.PI / Math.Acos(1 - tol / r) - 1e-9);
        return Math.Max(n, 8);
    }

    /// <summary>Segments of an arc of radius <paramref name="r"/> sweeping <paramref name="sweep"/> rad with sagitta &lt;= tol.</summary>
    public static int ArcSegments(double r, double sweep, double tol)
    {
        if (r <= 0 || tol >= r) return Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2)));
        double step = 2 * Math.Acos(1 - tol / r);
        return Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / step - 1e-9));
    }

    /// <summary>Closed polygon of a profile made of lines and true arcs (each arc split to the chord tolerance; vertices on the arc).</summary>
    public static List<Vec2> Tessellate(IReadOnlyList<ProfileSegment> segs, double tol)
    {
        var pts = new List<Vec2>();
        foreach (var g in segs)
        {
            if (!g.IsArc) { pts.Add(g.Start); continue; }
            int n = ArcSegments(g.Radius, g.Sweep, tol);
            for (int k = 0; k < n; k++) pts.Add(g.PointAt((double)k / n));
        }
        // Drop repeats (an arc ending where the next segment starts is added once).
        var r = new List<Vec2>(pts.Count);
        foreach (var p in pts)
            if (r.Count == 0 || (p - r[^1]).Length > 1e-9) r.Add(p);
        if (r.Count > 1 && (r[0] - r[^1]).Length <= 1e-9) r.RemoveAt(r.Count - 1);
        return r;
    }

    /// <summary>
    /// Clockwise polygon (a void loop) with the AREA of the hole circle: vertices on radius r*sqrt(2 pi / (n sin(2 pi / n))),
    /// so plate volumes match the Parametric extrusion (an inscribed polygon leaves e.g. a 2" conduit bore 10 % too full);
    /// vertices lie just outside the circle, edge midpoints just inside, both within the sagitta of the inscribed polygon.
    /// Falls back to the inscribed polygon when an enlarged vertex would leave the outline (<paramref name="outline"/>).
    /// </summary>
    public Vec2[] HolePolygon(Hole h, IReadOnlyList<Vec2>? outline = null)
    {
        double r = h.Diameter / 2;
        int n = CircleSegments(r, _tol);
        double re = r * Math.Sqrt(2 * Math.PI / (n * Math.Sin(2 * Math.PI / n)));
        var p = Ring(h.Center, re, n);
        if (outline != null && !p.All(q => StrictlyInside(q, outline))) p = Ring(h.Center, r, n);
        return p;
    }

    private static Vec2[] Ring(Vec2 c, double r, int n)
    {
        var p = new Vec2[n];
        for (int k = 0; k < n; k++)
        {
            double a = -2 * Math.PI * k / n;
            p[k] = c + new Vec2(Math.Cos(a), Math.Sin(a)) * r;
        }
        return p;
    }

    private static bool StrictlyInside(Vec2 c, IReadOnlyList<Vec2> o)
    {
        bool inside = false;
        for (int i = 0, j = o.Count - 1; i < o.Count; j = i++)
        {
            var a = o[i]; var b = o[j];
            if ((a.Y > c.Y) != (b.Y > c.Y) && c.X < a.X + (c.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) inside = !inside;
            var ab = b - a;
            double len2 = Vec2.Dot(ab, ab);
            double t = len2 > 0 ? Math.Clamp(Vec2.Dot(c - a, ab) / len2, 0, 1) : 0;
            if (Vec2.Distance(a + ab * t, c) <= 1e-5) return false;
        }
        return inside;
    }

    /// <summary>A recorded extrusion as an IfcPolygonalFaceSet (returns its id).</summary>
    public int Prism(ExtrudedSolid e)
    {
        // True-arc outlines (plates with radiused edges) are tessellated to the chord tolerance like the prestressed profiles.
        IReadOnlyList<Vec2> o = e.ExactOutline is { } xo ? xo.Tessellate(_tol) : e.Outline;
        return Prism(e.Frame, o, e.Holes.Select(h => HolePolygon(h, o)).ToList(), e.Extrusion, null, null);
    }

    /// <summary>
    /// Prestressed beam with an exact profile: the polygonized profile in <see cref="BeamBody.Frame"/> (mirrored when the
    /// section x runs against the frame X), swept along the beam axis and cut by the two end planes (keep the side opposite
    /// to each OUTWARD normal) - the face-set equivalent of the Parametric IfcBooleanClippingResult.
    /// </summary>
    public int ClippedBeam(BeamBody bb, SectionProfile profile)
    {
        var pts = Tessellate(profile.Segments, _tol);
        if (bb.SectionXSign < 0) pts = pts.Select(p => new Vec2(-p.X, p.Y)).ToList();
        return Prism(bb.Frame, pts, Array.Empty<Vec2[]>(), bb.Extrusion, (bb.StartPoint, bb.StartNormal), (bb.EndPoint, bb.EndNormal));
    }

    /// <summary>
    /// General prism: outer loop + void loops in the frame's x-y plane, swept by <paramref name="d"/>; optionally bounded by a
    /// start / end plane (point, outward normal) instead of the profile plane / profile plane + d. Faces are oriented outward.
    /// </summary>
    public int Prism(Frame3 f, IReadOnlyList<Vec2> outer, IReadOnlyList<Vec2[]> holes, Vec3 d,
        (Vec3 P, Vec3 N)? start, (Vec3 P, Vec3 N)? end)
    {
        var loops = new List<Vec2[]>(1 + holes.Count);
        var o = outer.ToArray();
        if (MeshBuilder.SignedArea(o) < 0) Array.Reverse(o);
        loops.Add(o);
        foreach (var h in holes)
        {
            var hh = h.ToArray();
            if (MeshBuilder.SignedArea(hh) > 0) Array.Reverse(hh);
            loops.Add(hh);
        }
        bool flip = Vec3.Dot(d, f.Z) < 0;

        int total = loops.Sum(l => l.Length);
        var bottom = new Vec3[total];
        var top = new Vec3[total];
        var first = new int[loops.Count];
        int k = 0;
        for (int li = 0; li < loops.Count; li++)
        {
            first[li] = k;
            foreach (var q in loops[li])
            {
                var b = f.Origin + f.X * q.X + f.Y * q.Y;
                double t0 = start is { } s ? Vec3.Dot(s.P - b, s.N) / Vec3.Dot(d, s.N) : 0;
                double t1 = end is { } en ? Vec3.Dot(en.P - b, en.N) / Vec3.Dot(d, en.N) : 1;
                bottom[k] = b + d * t0;
                top[k] = b + d * t1;
                k++;
            }
        }

        var sb = new StringBuilder("(");
        for (int i = 0; i < total; i++) { if (i > 0) sb.Append(','); sb.Append(_point(bottom[i])); }
        for (int i = 0; i < total; i++) { sb.Append(','); sb.Append(_point(top[i])); }
        sb.Append("),$");
        int pts = _w.Add("IFCCARTESIANPOINTLIST3D", sb.ToString());

        int B(int li, int i) => first[li] + i + 1;
        int T(int li, int i) => total + first[li] + i + 1;
        var faces = new List<int>(2 + total);

        // End caps: the top keeps the loop orientation (outer CCW, voids CW seen along +Z), the bottom is reversed.
        int Cap(Func<int, int, int> idx, bool reverse)
        {
            IEnumerable<int> Loop(int li)
            {
                var ids = Enumerable.Range(0, loops[li].Length).Select(i => idx(li, i));
                return reverse ? ids.Reverse() : ids;
            }
            sb.Clear().Append('(').AppendJoin(',', Loop(0)).Append(')');
            if (loops.Count == 1) return _w.Add("IFCINDEXEDPOLYGONALFACE", sb.ToString());
            sb.Append(",(");
            for (int li = 1; li < loops.Count; li++)
            {
                if (li > 1) sb.Append(',');
                sb.Append('(').AppendJoin(',', Loop(li)).Append(')');
            }
            sb.Append(')');
            return _w.Add("IFCINDEXEDPOLYGONALFACEWITHVOIDS", sb.ToString());
        }
        faces.Add(Cap(B, !flip));
        faces.Add(Cap(T, flip));

        // Side walls (outline sides and hole walls): planar quads b_i, b_i+1, t_i+1, t_i.
        for (int li = 0; li < loops.Count; li++)
        {
            int n = loops[li].Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a = B(li, i), b = B(li, j), c = T(li, j), e = T(li, i);
                faces.Add(_w.Add("IFCINDEXEDPOLYGONALFACE", flip ? $"({e},{c},{b},{a})" : $"({a},{b},{c},{e})"));
            }
        }

        sb.Clear().Append(Ref(pts)).Append(",.T.,(");
        for (int i = 0; i < faces.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Ref(faces[i])); }
        sb.Append("),$");
        FaceSets++;
        return _w.Add("IFCPOLYGONALFACESET", sb.ToString());
    }
}
