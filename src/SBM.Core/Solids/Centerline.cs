using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>
/// Centerlines of one solid: the GEOMETRIC centerline (centre of the section's extents at each station) and the CENTROID line (area centroid of the
/// section, holes subtracted). Both run end to end of the element along its axis. Lines are 3D polylines in world coordinates (meters); a prismatic
/// element gives two points, a tapered / stepped / curved one more.
/// </summary>
public sealed class ElementCenterline
{
    /// <summary>"extrusion" = exact from the recorded extrusion recipe; "sections" = cut from the mesh at stations (principal axis, then square to the path).</summary>
    public string Method { get; init; } = "";
    public List<Vec3> Geometric { get; init; } = new();
    public List<Vec3> Centroid { get; init; } = new();
    /// <summary>Unit direction of the element axis (first to last point of the centroid line).</summary>
    public Vec3 Axis { get; init; }
    /// <summary>Section area at the stations used (m², same order as the interior points); empty for the extrusion method (constant).</summary>
    public List<double> Areas { get; init; } = new();
    /// <summary>Why no line could be made (open / degenerate mesh); lines are then empty.</summary>
    public string? Note { get; init; }
    public double Length => Polyline(Centroid);

    public static double Polyline(IReadOnlyList<Vec3> p) { double s = 0; for (int i = 1; i < p.Count; i++) s += (p[i] - p[i - 1]).Length; return s; }
}

public sealed class CenterlineOptions
{
    /// <summary>Section stations along the element (more for curved / varying elements; the result is simplified).</summary>
    public int Stations { get; init; } = 24;
    /// <summary>Points closer than this to the chord of their neighbours are dropped (m).</summary>
    public double SimplifyTolerance { get; init; } = 0.001;
    /// <summary>An axis within this angle of plumb / level is snapped to it (degrees).</summary>
    public double SnapDegrees { get; init; } = 2;
    /// <summary>Use the extrusion recipe when the mesh is one straight extrusion.</summary>
    public bool UseRecipes { get; init; } = true;
}

/// <summary>Centerline extraction for any closed solid (see <see cref="ElementCenterline"/>).</summary>
public static class Centerlines
{
    public static ElementCenterline Of(Mesh mesh, CenterlineOptions? options = null)
    {
        var o = options ?? new CenterlineOptions();
        if (mesh.IsEmpty) return new ElementCenterline { Method = "none", Note = "empty mesh" };
        // the recipe is the answer only when the extrusion is the element's long direction (a cap drawn as an elevation profile and extruded across
        // its width has its axis IN the profile plane)
        if (o.UseRecipes && mesh.HasExactSolids && mesh.Solids!.Count == 1 && AlongExtrusion(mesh.Solids[0])) return FromExtrusion(mesh.Solids[0]);
        return FromSections(mesh, o);
    }

    private static bool AlongExtrusion(ExtrudedSolid s)
    {
        double len = s.Extrusion.Length, ext = 0;
        for (int i = 0; i < s.Outline.Length; i++)
            for (int j = i + 1; j < s.Outline.Length; j++) ext = Math.Max(ext, (s.Outline[i] - s.Outline[j]).Length);
        return len >= ext;
    }

    /// <summary>Exact: the profile's centroid / extents centre swept along the extrusion vector (sections parallel to the profile plane).</summary>
    public static ElementCenterline FromExtrusion(ExtrudedSolid s)
    {
        var (area, cen) = AreaCentroid(s.Outline);
        double ax = area * cen.X, ay = area * cen.Y, a = area;
        foreach (var h in s.Holes)
        {
            double ha = Math.PI * h.Diameter * h.Diameter / 4;
            a -= ha; ax -= ha * h.Center.X; ay -= ha * h.Center.Y;
        }
        var c = new Vec2(ax / a, ay / a);
        var g = new Vec2((s.Outline.Min(p => p.X) + s.Outline.Max(p => p.X)) / 2, (s.Outline.Min(p => p.Y) + s.Outline.Max(p => p.Y)) / 2);
        var c0 = s.Frame.ToWorld(c.X, c.Y, 0);
        var g0 = s.Frame.ToWorld(g.X, g.Y, 0);
        return new ElementCenterline
        {
            Method = "extrusion", Axis = s.Extrusion.Normalized(),
            Centroid = new() { c0, c0 + s.Extrusion }, Geometric = new() { g0, g0 + s.Extrusion },
        };
    }

    private sealed record Section(double S, Vec3 Point, Vec3 Normal, Vec3 Centroid, Vec3 Geometric, double Area, double Size);

    /// <summary>Sections of the mesh: pass 1 normal to the principal (long) axis, pass 2 normal to the pass-1 centroid path (curved elements).
    /// End zones whose section is partial (skewed / tapered ends: area under 90 % of the median) are not used; the lines are extended straight to
    /// the element's extreme planes along the end tangents.</summary>
    public static ElementCenterline FromSections(Mesh mesh, CenterlineOptions o)
    {
        var origin = mesh.Positions[mesh.Indices[0]];
        var (axis, second, centre, ok) = PrincipalAxes(mesh, origin);
        if (!ok) return new ElementCenterline { Method = "sections", Note = "degenerate mesh (no volume, no extent)" };
        axis = Snap(axis, o.SnapDegrees);
        if (Math.Abs(axis.Z) > 0.999) second = Math.Abs(second.Z) > 0.9 ? new Vec3(1, 0, 0) : second;
        double sMin = double.MaxValue, sMax = double.MinValue;
        foreach (var p in mesh.Positions) { double s = Vec3.Dot(p - centre, axis); sMin = Math.Min(sMin, s); sMax = Math.Max(sMax, s); }
        double len = sMax - sMin;
        if (len < 1e-6) return new ElementCenterline { Method = "sections", Note = "no length along the axis" };
        int n = Math.Max(3, o.Stations);
        var up = Math.Abs(axis.Z) > 0.9 ? second : new Vec3(0, 0, 1);

        // pass 1: straight axis
        var pass1 = new List<Section>();
        for (int i = 0; i < n; i++)
        {
            double s = sMin + len * (i + 0.5) / n;
            var pt = centre + axis * s;
            var sec = Cut(mesh, pt, axis, up, null, 0);
            if (sec != null) pass1.Add(sec with { S = s });
        }
        pass1 = FullSections(pass1);
        if (pass1.Count == 0) return new ElementCenterline { Method = "sections", Note = "no closed section found (open mesh?)" };

        // pass 2 (curved elements only: the pass-1 path leaves its chord by more than 25 mm / 0.5 % of the length): square to the pass-1 centroid
        // path, keeping the loops near the predicted point. A straight path (prismatic, tapered, stepped) keeps the sections normal to the axis.
        var pass2 = new List<Section>();
        double dev = 0;
        if (pass1.Count > 2)
        {
            var ch = (pass1[^1].Centroid - pass1[0].Centroid); double chl = ch.Length;
            if (chl > 1e-9) foreach (var x in pass1) dev = Math.Max(dev, Vec3.Cross(x.Centroid - pass1[0].Centroid, ch).Length / chl);
        }
        bool curved = dev > Math.Max(0.025, 0.005 * len);
        for (int i = 0; i < (curved ? pass1.Count : 0); i++)
        {
            var prev = pass1[Math.Max(0, i - 1)].Centroid;
            var next = pass1[Math.Min(pass1.Count - 1, i + 1)].Centroid;
            var t = pass1.Count > 1 ? (next - prev).Normalized() : axis;
            if (Vec3.Dot(t, axis) < 0.2) t = axis;   // folded path: keep the axis
            var u = Math.Abs(t.Z) > 0.9 ? second : new Vec3(0, 0, 1);
            var sec = Cut(mesh, pass1[i].Centroid, t, u, pass1[i].Centroid, pass1[i].Size * 0.75);
            pass2.Add(sec != null ? sec with { S = pass1[i].S } : pass1[i]);
        }
        pass2 = FullSections(pass2);
        if (pass2.Count == 0) pass2 = pass1;

        var cen = Extend(pass2.Select(x => x.Centroid).ToList(), pass2.Select(x => x.Normal).ToList(), mesh, axis, centre, sMin, sMax);
        var geo = Extend(pass2.Select(x => x.Geometric).ToList(), pass2.Select(x => x.Normal).ToList(), mesh, axis, centre, sMin, sMax);
        return new ElementCenterline
        {
            Method = "sections", Axis = (cen[^1] - cen[0]).Normalized(),
            Centroid = Simplify(cen, o.SimplifyTolerance), Geometric = Simplify(geo, o.SimplifyTolerance),
            Areas = pass2.Select(x => x.Area).ToList(),
        };
    }

    /// <summary>Stations whose section area is at least 90 % of the median (drops the partial sections in skewed / tapered end zones) and at least
    /// 90 % of the median of their +-3 neighbours (drops cuts through holes); when that leaves fewer than two, every station is kept.</summary>
    private static List<Section> FullSections(List<Section> secs)
    {
        if (secs.Count < 3) return secs;
        var sorted = secs.Select(x => x.Area).OrderBy(a => a).ToList();
        double med = sorted[sorted.Count / 2];
        var full = secs.Where(x => x.Area >= 0.9 * med).ToList();
        // keep the interior run between the first and last full section (a tapered member keeps its smaller sections there)
        if (full.Count < 2) return secs;
        int i0 = secs.IndexOf(full[0]), i1 = secs.IndexOf(full[^1]);
        var run = secs.GetRange(i0, i1 - i0 + 1);
        // stations through a hole / opening (handholes, cut-outs, a missed loop) lose area against their neighbours: they would move the line
        var keep = new List<Section>();
        for (int i = 0; i < run.Count; i++)
        {
            var w = run.Skip(Math.Max(0, i - 3)).Take(Math.Min(run.Count, i + 4) - Math.Max(0, i - 3)).Select(x => x.Area).OrderBy(a => a).ToList();
            if (run[i].Area >= 0.9 * w[w.Count / 2]) keep.Add(run[i]);
        }
        return keep.Count >= 2 ? keep : run;
    }

    /// <summary>Straight extension of the line at both ends along its end tangents to the extreme points of the mesh on that tangent.</summary>
    private static List<Vec3> Extend(List<Vec3> pts, List<Vec3> normals, Mesh mesh, Vec3 axis, Vec3 centre, double sMin, double sMax)
    {
        var r = new List<Vec3>(pts);
        Vec3 t0 = pts.Count > 1 ? (pts[1] - pts[0]).Normalized() : axis, t1 = pts.Count > 1 ? (pts[^1] - pts[^2]).Normalized() : axis;
        Vec3 n0 = normals[0], n1 = normals[^1];   // end planes are normal to the end sections; the line runs on along its end tangent to them
        if (pts.Count == 1 || Vec3.Dot(t0, n0) < 0.2) t0 = n0;
        if (pts.Count == 1 || Vec3.Dot(t1, n1) < 0.2) t1 = n1;
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (var p in mesh.Positions) { lo = Math.Min(lo, Vec3.Dot(p - pts[0], n0)); hi = Math.Max(hi, Vec3.Dot(p - pts[^1], n1)); }
        // the line ends where it leaves the solid (skewed / bevelled end faces), else on the extreme plane
        // (a line outside the material - angle / channel centroids, box voids - finds no exit and stops on the extreme plane)
        double p0 = lo < -1e-6 ? -lo / Vec3.Dot(t0, n0) : 0, p1 = hi > 1e-6 ? hi / Vec3.Dot(t1, n1) : 0;
        double e0 = Exit(mesh, pts[0], -t0, p0 + 1e-3) ?? p0;
        double e1 = Exit(mesh, pts[^1], t1, p1 + 1e-3) ?? p1;
        if (e0 > 1e-6) r.Insert(0, pts[0] - t0 * e0);
        if (e1 > 1e-6) r.Add(pts[^1] + t1 * e1);
        return r;
    }

    /// <summary>Distance along the ray to the farthest crossing of the mesh surface within <paramref name="max"/> (the exit from the solid), null when none.</summary>
    private static double? Exit(Mesh mesh, Vec3 o, Vec3 d, double max)
    {
        var P = mesh.Positions; var I = mesh.Indices;
        double best = -1;
        for (int t = 0; t + 2 < I.Count; t += 3)
        {
            var a = P[I[t]]; var e1 = P[I[t + 1]] - a; var e2 = P[I[t + 2]] - a;
            var pv = Vec3.Cross(d, e2); double det = Vec3.Dot(e1, pv);
            if (Math.Abs(det) < 1e-18) continue;
            double inv = 1 / det; var tv = o - a;
            double u = Vec3.Dot(tv, pv) * inv; if (u < -1e-9 || u > 1 + 1e-9) continue;
            var qv = Vec3.Cross(tv, e1); double v = Vec3.Dot(d, qv) * inv; if (v < -1e-9 || u + v > 1 + 1e-9) continue;
            double dist = Vec3.Dot(e2, qv) * inv;
            if (dist > best && dist <= max) best = dist;
        }
        return best > 0 ? best : null;
    }

    private static Section? Cut(Mesh mesh, Vec3 at, Vec3 normal, Vec3 up, Vec3? near, double radius)
    {
        var ms = new MeshSection(at, normal, up);
        var loops = MeshSection.Loops(ms.Cut(mesh));
        if (loops.Count == 0) return null;
        // nesting depth (even = solid, odd = hole)
        var info = loops.Select(l => (loop: l, area: Math.Abs(MeshBuilder.SignedArea(l)), c: AreaCentroid(l).c)).ToList();
        var depth = new int[loops.Count];
        for (int i = 0; i < loops.Count; i++)
            for (int j = 0; j < loops.Count; j++)
                if (i != j && info[j].area > info[i].area && Inside(info[j].loop, loops[i][0])) depth[i]++;
        // with a predicted point: outer loops near it, and the loops inside them
        var keep = Enumerable.Range(0, loops.Count).ToList();
        if (near is Vec3 nr && radius > 0)
        {
            var p2 = ms.To2D(nr);
            var outers = keep.Where(i => depth[i] % 2 == 0 && (info[i].c - p2).Length <= radius).ToList();
            if (outers.Count == 0) outers = keep.Where(i => depth[i] % 2 == 0).OrderBy(i => (info[i].c - p2).Length).Take(1).ToList();
            keep = keep.Where(i => outers.Contains(i) || (depth[i] % 2 == 1 && outers.Any(k => Inside(loops[k], loops[i][0])))).ToList();
        }
        double a = 0, ax = 0, ay = 0, x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        foreach (int i in keep)
        {
            double sgn = depth[i] % 2 == 0 ? 1 : -1;
            a += sgn * info[i].area; ax += sgn * info[i].area * info[i].c.X; ay += sgn * info[i].area * info[i].c.Y;
            if (sgn > 0) foreach (var p in loops[i]) { x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X); y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y); }
        }
        if (a <= 1e-12) return null;
        Vec3 W(double x, double y) => at + ms.XAxis * x + ms.YAxis * y;
        double size = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        return new Section(0, at, normal, W(ax / a, ay / a), W((x0 + x1) / 2, (y0 + y1) / 2), a, size);
    }

    private static bool Inside(List<Vec2> poly, Vec2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
        return c;
    }

    public static (double area, Vec2 c) AreaCentroid(IReadOnlyList<Vec2> p)
    {
        double a = 0, cx = 0, cy = 0;
        var o = p[0];
        for (int i = 0; i < p.Count; i++)
        {
            var q0 = p[i] - o; var q1 = p[(i + 1) % p.Count] - o;
            double cr = q0.X * q1.Y - q1.X * q0.Y;
            a += cr; cx += (q0.X + q1.X) * cr; cy += (q0.Y + q1.Y) * cr;
        }
        a /= 2;
        if (Math.Abs(a) < 1e-18) return (0, p[0]);
        return (Math.Abs(a), new Vec2(o.X + cx / (6 * a), o.Y + cy / (6 * a)));
    }

    private static Vec3 Snap(Vec3 a, double deg)
    {
        double sinTol = Math.Sin(deg * Math.PI / 180);
        var up = new Vec3(0, 0, 1);
        if (Math.Abs(a.Z) > Math.Cos(deg * Math.PI / 180)) return a.Z > 0 ? up : -up;
        if (Math.Abs(a.Z) < sinTol) return new Vec3(a.X, a.Y, 0).Normalized();
        return a;
    }

    /// <summary>Long axis (largest second moment of the volume about the centroid), second axis and the centroid; triangle-area weighted when the mesh
    /// encloses no volume. Coordinates relative to a mesh vertex (state-plane magnitudes).</summary>
    public static (Vec3 axis, Vec3 second, Vec3 centroid, bool ok) PrincipalAxes(Mesh mesh, Vec3 origin)
    {
        var P = mesh.Positions; var I = mesh.Indices;
        double V = 0; var sum = Vec3.Zero; var M = new double[3, 3];
        for (int t = 0; t + 2 < I.Count; t += 3)
        {
            var a = P[I[t]] - origin; var b = P[I[t + 1]] - origin; var c = P[I[t + 2]] - origin;
            double v = Vec3.Dot(a, Vec3.Cross(b, c)) / 6;
            V += v; sum += (a + b + c) * (v / 4);
            var s = a + b + c;
            Acc(M, v / 20, a, b, c, s);
        }
        bool volume = Math.Abs(V) > 1e-12;
        if (!volume)
        {
            double A = 0; sum = Vec3.Zero; M = new double[3, 3];
            for (int t = 0; t + 2 < I.Count; t += 3)
            {
                var a = P[I[t]] - origin; var b = P[I[t + 1]] - origin; var c = P[I[t + 2]] - origin;
                double ar = Vec3.Cross(b - a, c - a).Length / 2;
                A += ar; sum += (a + b + c) * (ar / 3);
                foreach (var q in new[] { a, b, c }) AddOuter(M, q, ar / 3);
            }
            if (A < 1e-14) return (default, default, default, false);
            V = A;
        }
        var cen = sum * (1 / V);
        var C = new double[3, 3];
        double[] cv = { cen.X, cen.Y, cen.Z };
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) C[i, j] = M[i, j] / V - cv[i] * cv[j];
        var (vals, vecs) = Jacobi(C);
        var order = new[] { 0, 1, 2 }.OrderByDescending(k => vals[k]).ToArray();
        Vec3 Col(int k) => new Vec3(vecs[0, k], vecs[1, k], vecs[2, k]).Normalized();
        var ax = Col(order[0]);
        // no single long direction (disc, square footing, cube): the axis of symmetry = the third principal axis (plumb for a disc / footing)
        if (vals[order[0]] - vals[order[1]] < 0.02 * Math.Max(1e-18, vals[order[0]])) ax = Col(order[2]);
        if (ax.X + ax.Y + ax.Z * 1e-3 < 0 && Math.Abs(ax.Z) < 0.99) ax = -ax;
        if (Math.Abs(ax.Z) >= 0.99 && ax.Z < 0) ax = -ax;
        var sec = Col(order[1]);
        return (ax, sec, origin + cen, true);
    }

    private static void Acc(double[,] M, double f, Vec3 a, Vec3 b, Vec3 c, Vec3 s)
    {
        // ∫ x_i x_j over the tetrahedron (0, a, b, c) = V / 20 (Σ v v^T + s s^T), s = a + b + c
        AddOuter(M, a, f); AddOuter(M, b, f); AddOuter(M, c, f); AddOuter(M, s, f);
    }

    private static void AddOuter(double[,] M, Vec3 v, double f)
    {
        double[] x = { v.X, v.Y, v.Z };
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) M[i, j] += f * x[i] * x[j];
    }

    private static (double[] vals, double[,] vecs) Jacobi(double[,] A0)
    {
        var A = (double[,])A0.Clone();
        var V = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (int sweep = 0; sweep < 50; sweep++)
        {
            double off = Math.Abs(A[0, 1]) + Math.Abs(A[0, 2]) + Math.Abs(A[1, 2]);
            if (off < 1e-30) break;
            for (int p = 0; p < 2; p++)
                for (int q = p + 1; q < 3; q++)
                {
                    if (Math.Abs(A[p, q]) < 1e-300) continue;
                    double th = (A[q, q] - A[p, p]) / (2 * A[p, q]);
                    double t = Math.Sign(th) / (Math.Abs(th) + Math.Sqrt(th * th + 1));
                    if (th == 0) t = 1;
                    double c = 1 / Math.Sqrt(t * t + 1), s = t * c;
                    for (int k = 0; k < 3; k++)
                    {
                        double akp = A[k, p], akq = A[k, q];
                        A[k, p] = c * akp - s * akq; A[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double apk = A[p, k], aqk = A[q, k];
                        A[p, k] = c * apk - s * aqk; A[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double vkp = V[k, p], vkq = V[k, q];
                        V[k, p] = c * vkp - s * vkq; V[k, q] = s * vkp + c * vkq;
                    }
                }
        }
        return (new[] { A[0, 0], A[1, 1], A[2, 2] }, V);
    }

    /// <summary>Drops points closer than <paramref name="tol"/> to the chord of the kept neighbours (Douglas-Peucker).</summary>
    public static List<Vec3> Simplify(List<Vec3> p, double tol)
    {
        if (p.Count <= 2) return p;
        var keep = new bool[p.Count]; keep[0] = keep[^1] = true;
        void Rec(int i, int j)
        {
            if (j <= i + 1) return;
            var d = p[j] - p[i]; double L = d.Length;
            int best = -1; double bd = tol;
            for (int k = i + 1; k < j; k++)
            {
                double dist = L < 1e-12 ? (p[k] - p[i]).Length : Vec3.Cross(p[k] - p[i], d).Length / L;
                if (dist > bd) { bd = dist; best = k; }
            }
            if (best < 0) return;
            keep[best] = true; Rec(i, best); Rec(best, j);
        }
        Rec(0, p.Count - 1);
        return p.Where((_, k) => keep[k]).ToList();
    }
}
