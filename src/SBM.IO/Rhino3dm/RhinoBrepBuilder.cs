using SBM.Core.Geometry;
using SBM.Core.Geometry.Nurbs;
using SBM.Core.Solids;
using RG = Rhino.Geometry;
using SCurve = SBM.Core.Geometry.Nurbs.NurbsCurve;
using SSurface = SBM.Core.Geometry.Nurbs.NurbsSurface;

namespace SBM.IO.Rhino3dm;

/// <summary>
/// One bounded face of an exact solid, in world meters: a NURBS surface whose whole parameter rectangle is the face (Su x Sv outward), or a
/// planar region (plane X / Y, outward normal X x Y) bounded by an outer loop of edge curves; either may carry inner loops (holes) whose
/// curves are the edges they share with the hole walls.
/// </summary>
internal sealed class FaceSpec
{
    public SSurface? Surface { get; init; }
    public Vec3 Origin { get; init; }
    public Vec3 X { get; init; }
    public Vec3 Y { get; init; }
    /// <summary>Planar faces: outer loop curves, chained end to start (either sense; oriented in the plane by the builder).</summary>
    public List<SCurve> Outer { get; } = new();
    public List<List<SCurve>> Holes { get; } = new();
}

/// <summary>
/// Builds a Rhino B-rep from <see cref="FaceSpec"/>s with SHARED edges: an edge curve that two faces bound (same end vertices, same
/// geometry within the tolerance) is written once and trimmed by both faces, so a closed set of faces becomes a closed polysurface (solid).
/// Faces of a NURBS surface use its four iso boundaries; planar faces are PlaneSurfaces trimmed by the exact edge curves projected into
/// the plane (exact for curves lying in it); holes on curved faces get polyline trims (closest points of the 3D edge, the 3D edge stays exact).
/// Coordinates go through <paramref name="toModel"/> (base point and unit scale), a similarity, so NURBS stay exact.
/// </summary>
internal sealed class RhinoBrepBuilder
{
    private readonly RG.Brep _b = new();
    private readonly Func<Vec3, RG.Point3d> _p;
    private readonly double _scale, _tol;
    private readonly Dictionary<(long, long, long), List<int>> _vgrid = new();
    private readonly List<RG.Point3d> _vpts = new();
    private readonly List<(int v0, int v1, RG.Point3d[] samples)> _edges = new();
    private readonly Dictionary<(int, int), List<int>> _edgeByVerts = new();
    private const int EdgeSamples = 48;
    public string? Error { get; private set; }

    public RhinoBrepBuilder(Func<Vec3, RG.Point3d> toModel, double scale, double tolModel)
    {
        _p = toModel;
        _scale = scale;
        _tol = tolModel;
    }

    // ---------------------------------------------------------------- conversions

    public RG.NurbsCurve Curve3(SCurve c)
    {
        var r = new RG.NurbsCurve(3, c.IsRational, c.Degree + 1, c.Control.Length);
        for (int i = 1; i < c.Knots.Length - 1; i++) r.Knots[i - 1] = c.Knots[i];
        for (int i = 0; i < c.Control.Length; i++)
        {
            if (c.Weights != null) r.Points.SetPoint(i, _p(c.Control[i]), c.Weights[i]);
            else r.Points.SetPoint(i, _p(c.Control[i]));
        }
        return r;
    }

    public RG.NurbsSurface Surface3(SSurface s)
    {
        var r = RG.NurbsSurface.Create(3, s.IsRational, s.DegreeU + 1, s.DegreeV + 1, s.CountU, s.CountV);
        for (int i = 1; i < s.KnotsU.Length - 1; i++) r.KnotsU[i - 1] = s.KnotsU[i];
        for (int i = 1; i < s.KnotsV.Length - 1; i++) r.KnotsV[i - 1] = s.KnotsV[i];
        for (int i = 0; i < s.CountU; i++)
            for (int j = 0; j < s.CountV; j++)
            {
                if (s.Weights != null) r.Points.SetPoint(i, j, _p(s.Control[i, j]), s.Weights[i, j]);
                else r.Points.SetPoint(i, j, _p(s.Control[i, j]));
            }
        return r;
    }

    /// <summary>2D copy of a curve lying in a plane: control points mapped to plane coordinates (model units); exact for planar curves.</summary>
    private RG.NurbsCurve CurveInPlane(SCurve c, Vec3 o, Vec3 x, Vec3 y)
    {
        var r = new RG.NurbsCurve(3, c.IsRational, c.Degree + 1, c.Control.Length);
        for (int i = 1; i < c.Knots.Length - 1; i++) r.Knots[i - 1] = c.Knots[i];
        for (int i = 0; i < c.Control.Length; i++)
        {
            var q = c.Control[i] - o;
            var p2 = new RG.Point3d(Vec3.Dot(q, x) * _scale, Vec3.Dot(q, y) * _scale, 0);
            if (c.Weights != null) r.Points.SetPoint(i, p2, c.Weights[i]);
            else r.Points.SetPoint(i, p2);
        }
        return r;
    }

    // ---------------------------------------------------------------- topology

    private int Vertex(RG.Point3d p)
    {
        double cell = Math.Max(_tol * 4, 1e-9);
        long gx = (long)Math.Floor(p.X / cell), gy = (long)Math.Floor(p.Y / cell), gz = (long)Math.Floor(p.Z / cell);
        for (long i = gx - 1; i <= gx + 1; i++)
            for (long j = gy - 1; j <= gy + 1; j++)
                for (long k = gz - 1; k <= gz + 1; k++)
                    if (_vgrid.TryGetValue((i, j, k), out var list))
                        foreach (int v in list)
                            if (_vpts[v].DistanceTo(p) <= _tol) return v;
        int idx = _b.Vertices.Add(p, _tol).VertexIndex;
        _vpts.Add(p);
        if (!_vgrid.TryGetValue((gx, gy, gz), out var l2)) _vgrid[(gx, gy, gz)] = l2 = new List<int>();
        l2.Add(idx);
        return idx;
    }

    private static RG.Point3d[] Sample(RG.Curve c, int n)
    {
        var d = c.Domain;
        var s = new RG.Point3d[n + 1];
        for (int i = 0; i <= n; i++) s[i] = c.PointAt(d.T0 + (d.T1 - d.T0) * i / n);
        return s;
    }

    private static double DistToPolyline(RG.Point3d q, RG.Point3d[] pl)
    {
        double best = double.MaxValue;
        for (int i = 0; i + 1 < pl.Length; i++)
        {
            var a = pl[i]; var ab = pl[i + 1] - a;
            double l2 = ab.SquareLength;
            double t = l2 < 1e-30 ? 0 : Math.Clamp((q - a) * ab / l2, 0, 1);
            double d = q.DistanceTo(a + ab * t);
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>Edge for a 3D curve (in the loop's direction): an existing edge with the same vertices and geometry, else a new one.</summary>
    private (int edge, bool rev) Edge(RG.Curve c3)
    {
        int v0 = Vertex(c3.PointAtStart), v1 = Vertex(c3.PointAtEnd);
        var key = (Math.Min(v0, v1), Math.Max(v0, v1));
        var samples = Sample(c3, EdgeSamples);
        if (_edgeByVerts.TryGetValue(key, out var cands))
            foreach (int e in cands)
            {
                var (ev0, _, es) = _edges[e];
                // same geometry: interior points of the new curve on the stored one (sagitta of 48 chords is far below the tolerance)
                bool same = true;
                foreach (int k in new[] { EdgeSamples / 4, EdgeSamples / 2, 3 * EdgeSamples / 4 })
                    if (DistToPolyline(samples[k], es) > Math.Max(_tol * 10, 1e-6)) { same = false; break; }
                if (same) return (e, ev0 != v0);
            }
        if (v0 == v1 && c3.PointAtStart.DistanceTo(c3.PointAt(c3.Domain.Mid)) <= _tol) throw new InvalidOperationException("degenerate edge");
        int ci = _b.AddEdgeCurve(c3);
        var edge = _b.Edges.Add(v0, v1, ci, _tol);
        _edges.Add((v0, v1, samples));
        if (!_edgeByVerts.TryGetValue(key, out var l)) _edgeByVerts[key] = l = new List<int>();
        l.Add(edge.EdgeIndex);
        return (edge.EdgeIndex, false);
    }

    private void Loop(RG.BrepFace face, RG.BrepLoopType type, List<(RG.Curve c3, RG.Curve c2, RG.IsoStatus iso)> segs)
    {
        var loop = _b.Loops.Add(type, face);
        foreach (var (c3, c2, iso) in segs)
        {
            var (ei, rev) = Edge(c3);
            int c2i = _b.AddTrimCurve(c2);
            var t = _b.Trims.Add(_b.Edges[ei], rev, loop, c2i);
            t.IsoStatus = iso;
            t.SetTolerances(0, 0);
        }
    }

    private static double SignedArea(IReadOnlyList<RG.Point3d> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; a += u.X * v.Y - v.X * u.Y; }
        return a / 2;
    }

    /// <summary>Orients a loop of (3D, 2D) segment pairs: counter-clockwise in the 2D parameter plane for outer loops, clockwise for holes.</summary>
    private static List<(RG.Curve c3, RG.Curve c2, RG.IsoStatus iso)> Orient(List<(RG.Curve c3, RG.Curve c2, RG.IsoStatus iso)> segs, bool outer)
    {
        var pts = new List<RG.Point3d>();
        foreach (var (_, c2, _) in segs) pts.AddRange(Sample(c2, 8).Take(8));
        bool ccw = SignedArea(pts) > 0;
        if (ccw == outer) return segs;
        var r = new List<(RG.Curve, RG.Curve, RG.IsoStatus)>();
        for (int i = segs.Count - 1; i >= 0; i--)
        {
            var (c3, c2, iso) = segs[i];
            var a = c3.DuplicateCurve(); a.Reverse();
            var b = c2.DuplicateCurve(); b.Reverse();
            r.Add((a, b, iso));
        }
        return r;
    }

    /// <summary>Chains curves end to start (reversing where needed); null when they do not form one closed loop.</summary>
    public static List<SCurve>? Chain(List<SCurve> curves, double tol)
    {
        if (curves.Count == 0) return null;
        var left = new List<SCurve>(curves.Skip(1));
        var res = new List<SCurve> { curves[0] };
        while (left.Count > 0)
        {
            var end = res[^1].PointAt(res[^1].End);
            int hit = -1; bool flip = false;
            for (int i = 0; i < left.Count && hit < 0; i++)
            {
                if ((left[i].PointAt(left[i].Start) - end).Length <= tol) hit = i;
                else if ((left[i].PointAt(left[i].End) - end).Length <= tol) { hit = i; flip = true; }
            }
            if (hit < 0) return null;
            res.Add(flip ? Reversed(left[hit]) : left[hit]);
            left.RemoveAt(hit);
        }
        return (res[0].PointAt(res[0].Start) - res[^1].PointAt(res[^1].End)).Length <= tol ? res : null;
    }

    public static SCurve Reversed(SCurve c)
    {
        double a = c.Knots[0], b = c.Knots[^1];
        return new SCurve(c.Degree, c.Knots.Reverse().Select(k => a + b - k).ToArray(), c.Control.Reverse().ToArray(), c.Weights?.Reverse().ToArray());
    }

    public static SCurve Line(Vec3 a, Vec3 b) => new(1, new double[] { 0, 0, 1, 1 }, new[] { a, b });

    // ---------------------------------------------------------------- faces

    public void AddFace(FaceSpec f)
    {
        if (f.Surface is { } s) AddSurfaceFace(f, s);
        else AddPlanarFace(f);
    }

    private void AddSurfaceFace(FaceSpec f, SSurface s)
    {
        var rs = Surface3(s);
        int si = _b.AddSurface(rs);
        var face = _b.Faces.Add(si);
        double u0 = s.U0, u1 = s.U1, v0 = s.V0, v1 = s.V1;
        RG.Curve Rev(RG.Curve c) { var d = c.DuplicateCurve(); d.Reverse(); return d; }
        RG.Curve L2(double a, double b, double c, double d) => new RG.LineCurve(new RG.Point2d(a, b), new RG.Point2d(c, d));
        var segs = new List<(RG.Curve, RG.Curve, RG.IsoStatus)>
        {
            (Curve3(s.Boundary(true, false)), L2(u0, v0, u1, v0), RG.IsoStatus.South),
            (Curve3(s.Boundary(false, true)), L2(u1, v0, u1, v1), RG.IsoStatus.East),
            (Rev(Curve3(s.Boundary(true, true))), L2(u1, v1, u0, v1), RG.IsoStatus.North),
            (Rev(Curve3(s.Boundary(false, false))), L2(u0, v1, u0, v0), RG.IsoStatus.West),
        };
        foreach (var (c3, _, _) in segs)
            if (c3.PointAtStart.DistanceTo(c3.PointAtEnd) <= _tol && c3.PointAtStart.DistanceTo(c3.PointAt(c3.Domain.Mid)) <= _tol)
                throw new InvalidOperationException("collapsed surface boundary (singular trim) not supported");
        Loop(face, RG.BrepLoopType.Outer, segs);
        foreach (var hole in f.Holes)
        {
            // polyline trims through the closest surface points of the 3D edge samples (warm-started along the loop)
            var (gu, gv) = Guess(s, hole[0].PointAt(hole[0].Start));
            var hs = new List<(RG.Curve, RG.Curve, RG.IsoStatus)>();
            RG.Point3d? first = null, prevEnd = null;
            for (int k = 0; k < hole.Count; k++)
            {
                var c = hole[k];
                const int n = 24;
                var uv = new List<RG.Point3d>();
                for (int i = 0; i <= n; i++)
                {
                    var q = c.PointAt(c.Start + (c.End - c.Start) * i / n);
                    var (u, v, _) = s.ClosestPoint(q, gu, gv);
                    gu = u; gv = v;
                    uv.Add(new RG.Point3d(u, v, 0));
                }
                if (prevEnd is { } pe) uv[0] = pe;                    // consecutive trims meet exactly
                if (k == 0) first = uv[0];
                if (k == hole.Count - 1 && first is { } fp) uv[^1] = fp;
                prevEnd = uv[^1];
                hs.Add((Curve3(c), new RG.PolylineCurve(uv), RG.IsoStatus.None));
            }
            Loop(face, RG.BrepLoopType.Inner, Orient(hs, false));
        }
    }

    private static (double u, double v) Guess(SSurface s, Vec3 q)
    {
        double bu = s.U0, bv = s.V0, bd = double.MaxValue;
        const int n = 24;
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n; j++)
            {
                double u = s.U0 + (s.U1 - s.U0) * i / n, v = s.V0 + (s.V1 - s.V0) * j / n;
                double d = (s.PointAt(u, v) - q).Length;
                if (d < bd) { bd = d; bu = u; bv = v; }
            }
        var (cu, cv, _) = s.ClosestPoint(q, bu, bv);
        return (cu, cv);
    }

    private void AddPlanarFace(FaceSpec f)
    {
        var x = f.X.Normalized();
        var y = f.Y.Normalized();
        var o = f.Origin;
        var outer = Snap(f.Outer).Select(c => ((RG.Curve)Curve3(c), (RG.Curve)CurveInPlane(c, o, x, y), RG.IsoStatus.None)).ToList();
        var holes = f.Holes.Select(h => Snap(h).Select(c => ((RG.Curve)Curve3(c), (RG.Curve)CurveInPlane(c, o, x, y), RG.IsoStatus.None)).ToList()).ToList();
        // plane surface domain = the loops' extent in plane coordinates (+ margin): the trims stay inside the surface
        double minU = double.MaxValue, minV = double.MaxValue, maxU = double.MinValue, maxV = double.MinValue;
        foreach (var (_, c2, _) in outer)
            foreach (var q in Sample(c2, 16)) { minU = Math.Min(minU, q.X); maxU = Math.Max(maxU, q.X); minV = Math.Min(minV, q.Y); maxV = Math.Max(maxV, q.Y); }
        double m = Math.Max(Math.Max(maxU - minU, maxV - minV) * 0.01, _tol * 10);
        var plane = new RG.Plane(_p(o), new RG.Vector3d(x.X, x.Y, x.Z), new RG.Vector3d(y.X, y.Y, y.Z));
        var ps = new RG.PlaneSurface(plane, new RG.Interval(minU - m, maxU + m), new RG.Interval(minV - m, maxV + m));
        int si = _b.AddSurface(ps);
        var face = _b.Faces.Add(si);
        Loop(face, RG.BrepLoopType.Outer, Orient(outer, true));
        foreach (var h in holes) Loop(face, RG.BrepLoopType.Inner, Orient(h, false));
    }

    /// <summary>The finished B-rep (null when Rhino finds it invalid; <see cref="Error"/> says why).</summary>
    public RG.Brep? Finish()
    {
        // vertex / edge / trim tolerances are set when the parts are added (openNURBS cannot recompute edge tolerances here)
        _b.SetTolerancesBoxesAndFlags(true, false, false, false, true, true, true, true);
        if (!_b.IsValid)
        {
            bool t = _b.IsValidTopology(out string l1), g = _b.IsValidGeometry(out string l2), f = _b.IsValidTolerancesAndFlags(out string l3);
            _b.IsValidWithLog(out string l4);
            string log = !t ? "topology: " + l1 : !g ? "geometry: " + l2 : !f ? "tolerances/flags: " + l3 : "invalid B-rep: " + l4;
            Error = string.Join(" | ", log.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Take(4));
            return null;
        }
        return _b;
    }

    // ---------------------------------------------------------------- face sets of the SBM solids

    /// <summary>Faces of a recorded exact NURBS solid; hole loops use the hole walls' boundary arcs so the edges are shared.</summary>
    public static List<FaceSpec> Faces(NurbsSolid solid)
    {
        var res = new List<FaceSpec>();
        var wallCurves = new List<SCurve>();
        foreach (var c in solid.Faces.Where(f => f.Surface != null && f.Role == "hole wall")
                     .SelectMany(f => new[] { f.Surface!.Boundary(true, false), f.Surface!.Boundary(true, true) }))
            if (!wallCurves.Any(w => SameCurve(w, c))) wallCurves.Add(c);   // walls of a hole registered twice
        List<SCurve> HoleLoop(NurbsHole h)
        {
            bool On(SCurve c)
            {
                foreach (double t in new[] { c.Start, (c.Start + c.End) / 2, c.End })
                {
                    var q = c.PointAt(t) - h.Center;
                    if (Math.Abs(q.Length - h.Radius) > 1e-4 + h.Radius * 1e-3) return false;
                    if (Math.Abs(Vec3.Dot(q, h.Normal.Normalized())) > Math.Max(1e-3, h.Radius * 0.2)) return false;
                }
                return true;
            }
            var mine = wallCurves.Where(On).ToList();
            if (mine.Count > 0 && Chain(mine, 1e-6) is { } ch) return ch;
            var (ax, ay) = NurbsArc.Axes(h.Normal);
            return new List<SCurve> { NurbsArc.Arc(h.Center, ax, ay, h.Radius, 0, Math.PI), NurbsArc.Arc(h.Center, ax, ay, h.Radius, Math.PI, 2 * Math.PI) };
        }
        static bool SameCurve(SCurve a, SCurve b) =>
            a.Control.Length == b.Control.Length && a.Control.Zip(b.Control).All(x => (x.First - x.Second).Length < 1e-9);
        var walls = new List<SSurface>();
        bool SameSurface(SSurface a, SSurface b)
        {
            if (a.CountU != b.CountU || a.CountV != b.CountV) return false;
            for (int i = 0; i < a.CountU; i++) for (int j = 0; j < a.CountV; j++) if ((a.Control[i, j] - b.Control[i, j]).Length > 1e-9) return false;
            return true;
        }
        foreach (var f in solid.Faces)
        {
            FaceSpec fs;
            if (f.Surface is { } srf)
            {
                // the same hole registered twice gives identical walls (and loops): keep one
                if (f.Role == "hole wall")
                {
                    if (walls.Any(w => SameSurface(w, srf))) continue;
                    walls.Add(srf);
                }
                // zero-area face (two opposite boundaries coincide, e.g. a section corner of zero width): its neighbours share that edge directly
                if (SameCurve(srf.Boundary(true, false), srf.Boundary(true, true)) || SameCurve(srf.Boundary(false, false), srf.Boundary(false, true))) continue;
                fs = new FaceSpec { Surface = srf };
            }
            else
            {
                var p0 = f.Polygon!;
                var pl = new List<Vec3>();
                foreach (var q in p0) if (pl.Count == 0 || (q - pl[^1]).Length > 1e-9) pl.Add(q);
                if (pl.Count > 1 && (pl[0] - pl[^1]).Length <= 1e-9) pl.RemoveAt(pl.Count - 1);
                if (pl.Count < 3) continue;
                var p = pl.ToArray();
                var n = f.PlaneNormal();
                var x = p[1] - p[0];
                x = (x - n * Vec3.Dot(x, n)).Normalized();
                fs = new FaceSpec { Origin = p[0], X = x, Y = Vec3.Cross(n, x) };
                for (int i = 0; i < p.Length; i++) fs.Outer.Add(Line(p[i], p[(i + 1) % p.Length]));
            }
            var seen = new List<NurbsHole>();
            foreach (var h in f.Holes)
            {
                if (seen.Any(x => (x.Center - h.Center).Length < 1e-7 && Math.Abs(x.Radius - h.Radius) < 1e-7)) continue;
                seen.Add(h);
                fs.Holes.Add(HoleLoop(h));
            }
            res.Add(fs);
        }
        return res;
    }

    /// <summary>Faces of a recorded extrusion whose sweep is NOT normal to its profile (oblique prism): planar end caps with the exact outline
    /// (lines / true arcs) and hole circles, ruled side faces, two half-cylinder walls per hole.</summary>
    public static List<FaceSpec> Faces(ExtrudedSolid es)
    {
        var f = es.Frame;
        var d = es.Extrusion;
        double side = Vec3.Dot(d, f.Z) >= 0 ? 1 : -1;
        Vec3 W(Vec2 q) => f.ToWorld(q.X, q.Y, 0);
        var bottom = new List<SCurve>();
        var dirs = new List<Vec3>();                 // outward in-plane direction at each edge's middle (profile CCW about f.Z)
        if (es.ExactOutline is { } ao)
        {
            var a = ao.CounterClockwise();
            for (int i = 0; i < a.Count; i++)
            {
                var p0 = a.Points[i]; var p1 = a.End(i);
                if (!a.IsArc(i)) bottom.Add(Line(W(p0), W(p1)));
                else bottom.Add(Arc2(W(a.Center(i)), f.X, f.Y, a.Center(i), p0, a.Sweep(i), Math.Abs(a.Radii[i])));
                var t = a.At(i, 0.51) - a.At(i, 0.49);
                dirs.Add(f.X * t.Y - f.Y * t.X);
            }
        }
        else
        {
            var o = es.Outline.ToList();
            if (MeshBuilder.SignedArea(o) < 0) o.Reverse();
            for (int i = 0; i < o.Count; i++)
            {
                var p0 = o[i]; var p1 = o[(i + 1) % o.Count];
                bottom.Add(Line(W(p0), W(p1)));
                var t = p1 - p0;
                dirs.Add(f.X * t.Y - f.Y * t.X);
            }
        }
        var top = bottom.Select(c => c.Translated(d)).ToList();
        var holesB = new List<List<SCurve>>();
        var holes = DistinctHoles(es.Holes);
        foreach (var h in holes)
        {
            var c = W(h.Center);
            double r = h.Diameter / 2;
            holesB.Add(new List<SCurve> { NurbsArc.Arc(c, f.X, f.Y, r, 0, Math.PI), NurbsArc.Arc(c, f.X, f.Y, r, Math.PI, 2 * Math.PI) });
        }
        var res = new List<FaceSpec>();
        // caps: bottom outward = -side * Z, top outward = +side * Z
        var capB = new FaceSpec { Origin = f.Origin, X = f.X, Y = f.Y * -side };
        capB.Outer.AddRange(bottom);
        capB.Holes.AddRange(holesB);
        var capT = new FaceSpec { Origin = f.Origin + d, X = f.X, Y = f.Y * side };
        capT.Outer.AddRange(top);
        capT.Holes.AddRange(holesB.Select(h => h.Select(c => c.Translated(d)).ToList()));
        res.Add(capB);
        res.Add(capT);
        for (int i = 0; i < bottom.Count; i++)
        {
            var s = SSurface.Ruled(bottom[i], top[i]);
            double um = (s.U0 + s.U1) / 2;
            if (Vec3.Dot(s.NormalAt(um, 0.5), dirs[i]) < 0) s = s.ReversedV();
            res.Add(new FaceSpec { Surface = s });
        }
        for (int k = 0; k < holesB.Count; k++)
        {
            var axisPt = W(holes[k].Center) + d * 0.5;
            foreach (var hb in holesB[k])
            {
                var s = SSurface.Ruled(hb, hb.Translated(d));
                var (pm, su, sv) = s.Eval((s.U0 + s.U1) / 2, 0.5);
                if (Vec3.Dot(Vec3.Cross(su, sv), axisPt - pm) < 0) s = s.ReversedV();   // outward = into the hole
                res.Add(new FaceSpec { Surface = s });
            }
        }
        return res;
    }

    /// <summary>Faces of an exact prestressed beam body: the section profile (lines + true arcs) swept along the beam axis and cut by the two
    /// end planes. The end caps are the profile projected along the axis onto each plane (an affine map, so the arcs stay exact rational
    /// curves - ellipse arcs on a skewed end); the sides are ruled faces between the two caps.</summary>
    public static List<FaceSpec> Faces(SBM.Core.Prestressed.BeamBody bb, SBM.Core.Prestressed.SectionProfile profile)
    {
        var f = bb.Frame;
        var segs = profile.Segments.ToList();
        if (bb.SectionXSign < 0)   // section x runs against the placement X: mirror (as the IFC export) and keep the outline counter-clockwise
        {
            static Vec2 M(Vec2 p) => new(-p.X, p.Y);
            segs = segs.Select(g => new SBM.Core.Prestressed.ProfileSegment(M(g.End), M(g.Start), g.Center is { } c ? M(c) : null, g.Ccw)).Reverse().ToList();
        }
        double area = 0;
        foreach (var g in segs) area += g.Start.X * g.End.Y - g.End.X * g.Start.Y;
        double orient = area >= 0 ? 1 : -1;
        Vec3 W(Vec2 q) => f.ToWorld(q.X, q.Y, 0);
        var a = bb.Extrusion.Normalized();
        Vec3 OnPlane(Vec3 p, Vec3 pp, Vec3 n) => p + a * (Vec3.Dot(pp - p, n) / Vec3.Dot(a, n));
        SCurve Map(SCurve c, Vec3 pp, Vec3 n) => new(c.Degree, c.Knots, c.Control.Select(x => OnPlane(x, pp, n)).ToArray(), c.Weights);
        var prof = new List<SCurve>();
        var dirs = new List<Vec3>();
        foreach (var g in segs)
        {
            prof.Add(g.Center is { } c ? Arc2(W(c), f.X, f.Y, c, g.Start, g.Sweep, g.Radius) : Line(W(g.Start), W(g.End)));
            var t = g.PointAt(0.51) - g.PointAt(0.49);
            dirs.Add((f.X * t.Y - f.Y * t.X) * orient);
        }
        prof = Snap(prof);          // fillet radii rounded in the profile data: arc ends meet the next segment exactly
        var start = prof.Select(c => Map(c, bb.StartPoint, bb.StartNormal)).ToList();
        var end = prof.Select(c => Map(c, bb.EndPoint, bb.EndNormal)).ToList();
        FaceSpec Cap(Vec3 pp, Vec3 n, List<SCurve> loop)
        {
            n = n.Normalized();
            var x = f.X - n * Vec3.Dot(f.X, n);
            if (x.Length < 1e-6) x = f.Y - n * Vec3.Dot(f.Y, n);
            x = x.Normalized();
            var fs = new FaceSpec { Origin = pp, X = x, Y = Vec3.Cross(n, x) };
            fs.Outer.AddRange(loop);
            return fs;
        }
        var res = new List<FaceSpec> { Cap(bb.StartPoint, bb.StartNormal, start), Cap(bb.EndPoint, bb.EndNormal, end) };
        for (int i = 0; i < prof.Count; i++)
        {
            var s = SSurface.Ruled(start[i], end[i]);
            if (Vec3.Dot(s.NormalAt((s.U0 + s.U1) / 2, 0.5), dirs[i]) < 0) s = s.ReversedV();
            res.Add(new FaceSpec { Surface = s });
        }
        return res;
    }

    /// <summary>Closes a chained loop exactly: each curve starts at the previous curve's last control point (clamped curves: = its end point).</summary>
    public static List<SCurve> Snap(List<SCurve> loop)
    {
        var r = new List<SCurve>(loop.Count);
        for (int i = 0; i < loop.Count; i++)
        {
            var c = loop[i];
            var prev = loop[(i + loop.Count - 1) % loop.Count];
            var ctrl = c.Control.ToArray();
            ctrl[0] = prev.Control[^1];
            r.Add(new SCurve(c.Degree, c.Knots, ctrl, c.Weights));
        }
        return r;
    }

    /// <summary>Holes without repeats (a hole registered twice would give two identical loops).</summary>
    public static List<Hole> DistinctHoles(IEnumerable<Hole> holes)
    {
        var r = new List<Hole>();
        foreach (var h in holes)
            if (!r.Any(x => Vec2.Distance(x.Center, h.Center) < 1e-7 && Math.Abs(x.Diameter - h.Diameter) < 1e-7)) r.Add(h);
        return r;
    }

    /// <summary>Arc of an outline edge (2D centre c2, start p0, signed sweep) as an exact rational curve in the profile frame.</summary>
    private static SCurve Arc2(Vec3 center, Vec3 x, Vec3 y, Vec2 c2, Vec2 p0, double sweep, double r)
    {
        double a0 = Math.Atan2(p0.Y - c2.Y, p0.X - c2.X);
        return sweep > 0 ? NurbsArc.Arc(center, x, y, r, a0, a0 + sweep) : NurbsArc.Arc(center, x, y * -1, r, -a0, -a0 - sweep);
    }
}
