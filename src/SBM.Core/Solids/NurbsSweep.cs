using SBM.Core.Geometry;
using SBM.Core.Geometry.Nurbs;

namespace SBM.Core.Solids;

/// <summary>
/// A polygon section swept along a curved path, exactly as the generators define it: corner k of the section at parameter u
/// is a known point C_k(u) (girder plates: path distance s; barriers / haunches: their own parameter) and the section is
/// straight between consecutive corners. All corner curves are fitted on ONE knot vector (<see cref="NurbsFit.FitCommon"/>),
/// so each lateral face is an exact ruled NURBS surface between two corner curves, and the ends are planar.
/// Plates (4 corners from a map (s, w, t)) also carry round holes through their t faces.
/// </summary>
public sealed class NurbsSweep
{
    public required CurveFit Fit { get; init; }
    public int Corners => Fit.Control.Length;
    public double U0 => Fit.Knots[0];
    public double U1 => Fit.Knots[^1];
    public double MaxDeviation => Fit.MaxDeviation;
    public required double Tolerance { get; init; }

    // plate data: section = [w0(s), w1(s)] x [t0(s), t1(s)], corners (w0,t0), (w1,t0), (w1,t1), (w0,t1)
    public Func<double, double>? W0 { get; init; }
    public Func<double, double>? W1 { get; init; }
    public Func<double, double>? T0 { get; init; }
    public Func<double, double>? T1 { get; init; }

    private NurbsCurve[]? _curves;
    public NurbsCurve[] Curves => _curves ??= Enumerable.Range(0, Corners).Select(Fit.Curve).ToArray();

    /// <summary>Fits a sweep; null (with the reason) when the tolerance is not reached.</summary>
    public static NurbsSweep? Create(int corners, Func<double, Vec3[]> exact, double u0, double u1, IEnumerable<double> breaks, double tol,
        out string reason, double initialSpan = 10)
    {
        reason = "";
        if (!(u1 > u0)) { reason = "empty range"; return null; }
        var fit = NurbsFit.FitCommon(corners, exact, u0, u1, breaks, tol, 3, initialSpan);
        if (!fit.Converged) { reason = $"fit {fit.MaxDeviation * 1000:0.###} mm > {tol * 1000:0.###} mm"; return null; }
        return new NurbsSweep { Fit = fit, Tolerance = tol };
    }

    /// <summary>Plate between w0(s)..w1(s) and t0(s)..t1(s) through a map (s, w, t) -> world that is affine in (w, t) at fixed s.</summary>
    public static NurbsSweep? Plate(double s0, double s1, Func<double, double> w0, Func<double, double> w1, Func<double, double> t0, Func<double, double> t1,
        Func<double, double, double, Vec3> map, IEnumerable<double> breaks, double tol, out string reason)
    {
        var sw = Create(4, s => new[] { map(s, w0(s), t0(s)), map(s, w1(s), t0(s)), map(s, w1(s), t1(s)), map(s, w0(s), t1(s)) }, s0, s1, breaks, tol, out reason);
        return sw == null ? null : new NurbsSweep { Fit = sw.Fit, Tolerance = tol, W0 = w0, W1 = w1, T0 = t0, T1 = t1 };
    }

    public Vec3[] Ring(double u) => Curves.Select(c => c.PointAt(u)).ToArray();

    /// <summary>Plate map on the NURBS: bilinear blend of the four corner curves (exactly on the ruled faces at the boundary).</summary>
    public Vec3 PlateMap(double s, double w, double t)
    {
        double a = (w - W0!(s)) / (W1!(s) - W0(s)), b = (t - T0!(s)) / (T1!(s) - T0(s));
        var c = Curves;
        return c[0].PointAt(s) * ((1 - a) * (1 - b)) + c[1].PointAt(s) * (a * (1 - b)) + c[2].PointAt(s) * (a * b) + c[3].PointAt(s) * ((1 - a) * b);
    }

    /// <summary>Parameters where a polyline through every corner curve stays within <paramref name="chord"/> of the curves
    /// (knots included, so kinks are vertices), plus <paramref name="extra"/>.</summary>
    public List<double> Samples(double chord, IEnumerable<double>? extra = null) => SampleCurves(Curves, chord, extra);

    /// <summary>Parameters where polylines through all <paramref name="curves"/> (one knot vector) stay within <paramref name="chord"/>.</summary>
    public static List<double> SampleCurves(IReadOnlyList<NurbsCurve> curves, double chord, IEnumerable<double>? extra = null)
    {
        var list = new List<double>();
        var ks = curves[0].Knots.Distinct().ToList();
        bool Flat(double a, double b, int depth)
        {
            if (depth > 20) return true;
            foreach (var c in curves)
            {
                var pa = c.PointAt(a);
                var pb = c.PointAt(b);
                var ab = pb - pa;
                double l2 = Vec3.Dot(ab, ab);
                for (int j = 1; j <= 3; j++)
                {
                    var q = c.PointAt(a + (b - a) * j / 4);
                    double t = l2 > 0 ? Math.Clamp(Vec3.Dot(q - pa, ab) / l2, 0, 1) : 0;
                    if ((pa + ab * t - q).Length > chord) return false;
                }
            }
            return true;
        }
        void Rec(double a, double b, int depth)
        {
            if (Flat(a, b, depth)) { list.Add(a); return; }
            double m = (a + b) / 2;
            Rec(a, m, depth + 1);
            Rec(m, b, depth + 1);
        }
        for (int i = 0; i + 1 < ks.Count; i++) Rec(ks[i], ks[i + 1], 0);
        list.Add(ks[^1]);
        if (extra != null) list.AddRange(extra.Where(x => x > ks[0] && x < ks[^1]));
        return list.Distinct().OrderBy(x => x).ToList();
    }

    /// <summary>The closed B-rep: one ruled face per section edge, planar ends, and (plates) round holes in the t faces with
    /// half-cylinder walls. Holes are (s, w) centres + diameter, as the generators register them.</summary>
    public NurbsSolid ToSolid(string kind, IReadOnlyList<Hole>? holes = null)
    {
        var solid = new NurbsSolid { Kind = kind, Tolerance = Tolerance, MaxDeviation = MaxDeviation };
        var c = Curves;
        int n = c.Length;
        double um = (U0 + U1) / 2;
        var ring = Ring(um);
        var nSec = Newell(ring);
        var faces = new NurbsFace[n];
        for (int k = 0; k < n; k++)
        {
            var s = NurbsSurface.Ruled(c[k], c[(k + 1) % n]);
            var outward = Vec3.Cross(ring[(k + 1) % n] - ring[k], nSec);
            var (p, su, sv) = s.Eval(um, 0.5);
            if (Vec3.Dot(Vec3.Cross(su, sv), outward) < 0) s = s.ReversedV();
            faces[k] = new NurbsFace { Surface = s, Role = n == 4 && W0 != null ? k switch { 0 => "t0", 1 => "w1", 2 => "t1", _ => "w0" } : "side" };
            solid.Faces.Add(faces[k]);
        }
        foreach (bool end in new[] { false, true })
        {
            double u = end ? U1 : U0;
            var r = Ring(u);
            var dir = Centroid(Ring(end ? U1 - (U1 - U0) * 1e-4 : U0 + (U1 - U0) * 1e-4)) - Centroid(r);   // into the solid
            if (Vec3.Dot(Newell(r), dir) > 0) Array.Reverse(r);
            solid.Faces.Add(new NurbsFace { Polygon = r, Role = end ? "end 1" : "end 0" });
        }
        if (holes != null && holes.Count > 0)
        {
            if (W0 == null) throw new InvalidOperationException("Holes need a plate sweep");
            foreach (var h in holes)
            {
                double s = h.Center.X, w = h.Center.Y, r = h.Diameter / 2;
                var top = PlateMap(s, w, T1!(s));
                var bot = PlateMap(s, w, T0!(s));
                var nTop = SurfaceNormal(faces[2].Surface!, top, s);
                var nBot = SurfaceNormal(faces[0].Surface!, bot, s);
                faces[2].Holes.Add(new NurbsHole(top, nTop, r));
                faces[0].Holes.Add(new NurbsHole(bot, nBot, r));
                // walls: circle in the t1 face's tangent plane, translated to the t0 face; two half cylinders
                var (x, y) = NurbsArc.Axes(nTop);
                var d = bot - top;
                foreach (var (a0, a1) in new[] { (0.0, Math.PI), (Math.PI, 2 * Math.PI) })
                {
                    var arcT = NurbsArc.Arc(top, x, y, r, a0, a1);
                    var arcB = arcT.Translated(d);
                    var ws = NurbsSurface.Ruled(arcT, arcB);
                    var (p, su, sv) = ws.Eval(0.5, 0.5);
                    var axisPt = top + d * 0.5;
                    if (Vec3.Dot(Vec3.Cross(su, sv), axisPt - p) < 0) ws = ws.ReversedV();   // outward = into the hole
                    solid.Faces.Add(new NurbsFace { Surface = ws, Role = "hole wall" });
                }
            }
        }
        return solid;
    }

    private static Vec3 SurfaceNormal(NurbsSurface s, Vec3 p, double uGuess)
    {
        var (u, v, _) = s.ClosestPoint(p, uGuess, 0.5);
        return s.NormalAt(u, v);
    }

    private static Vec3 Centroid(IReadOnlyList<Vec3> p)
    {
        var c = Vec3.Zero;
        foreach (var q in p) c += q - p[0];
        return p[0] + c * (1.0 / p.Count);
    }

    private static Vec3 Newell(IReadOnlyList<Vec3> p)
    {
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i] - p[0];
            var b = p[(i + 1) % p.Count] - p[0];
            nx += (a.Y - b.Y) * (a.Z + b.Z);
            ny += (a.Z - b.Z) * (a.X + b.X);
            nz += (a.X - b.X) * (a.Y + b.Y);
        }
        return new Vec3(nx, ny, nz);
    }
}
