using SBM.Core.Geometry;

namespace SBM.Core.Alignments;

public enum HorizontalElementKind { Line, Arc, Spiral }

/// <summary>
/// A horizontal alignment element with curvature varying linearly along its length.
/// This single form represents tangents (K0 = K1 = 0), circular arcs (K0 = K1 ≠ 0)
/// and clothoid spirals (K0 ≠ K1), including partial spirals.
/// Curvature is signed: positive = curving left (CCW), negative = curving right (CW).
/// Angles are math angles (radians CCW from +X / East). Lengths in meters.
/// </summary>
public sealed class HorizontalElement
{
    public Vec2 Start { get; init; }
    public double StartAngle { get; init; }
    public double Length { get; init; }
    public double K0 { get; init; }
    public double K1 { get; init; }

    /// <summary>Distance along the alignment at the start of this element (set by the owning alignment).</summary>
    public double StartDistance { get; internal set; }

    public double EndDistance => StartDistance + Length;

    public HorizontalElementKind Kind =>
        Math.Abs(K1 - K0) > 1e-15 ? HorizontalElementKind.Spiral
        : Math.Abs(K0) > 1e-15 ? HorizontalElementKind.Arc
        : HorizontalElementKind.Line;

    /// <summary>Radius of the arc (or of the sharper spiral end); +∞ for lines.</summary>
    public double Radius
    {
        get
        {
            double k = Math.Max(Math.Abs(K0), Math.Abs(K1));
            return k > 0 ? 1 / k : double.PositiveInfinity;
        }
    }

    private Box2? _bounds;

    public HorizontalElement() { }

    public HorizontalElement(Vec2 start, double startAngle, double length, double k0, double k1)
    {
        Start = start;
        StartAngle = startAngle;
        Length = length;
        K0 = k0;
        K1 = k1;
    }

    public static HorizontalElement Line(Vec2 start, Vec2 end) =>
        new(start, (end - start).Angle, (end - start).Length, 0, 0);

    public double CurvatureAt(double s) => Length > 0 ? K0 + (K1 - K0) * s / Length : K0;

    /// <summary>Tangent direction (math angle) at local distance s.</summary>
    public double AngleAt(double s)
    {
        double c = Length > 0 ? (K1 - K0) / (2 * Length) : 0;
        return StartAngle + K0 * s + c * s * s;
    }

    public Vec2 TangentAt(double s) => Vec2.FromAngle(AngleAt(s));

    /// <summary>Point at local distance s (may be outside [0, Length] for extension).</summary>
    public Vec2 PointAt(double s)
    {
        if (Kind != HorizontalElementKind.Spiral)
        {
            // Exact chord formula valid for lines and arcs of any curvature: chord = s·sinc(ks/2), direction = θ0 + ks/2.
            double half = K0 * s / 2;
            double chord = Math.Abs(half) < 1e-8 ? s * (1 - half * half / 6) : s * Math.Sin(half) / half;
            return Start + Vec2.FromAngle(StartAngle + half) * chord;
        }

        double c = (K1 - K0) / (2 * Length);
        double turn = Math.Abs(K0 * s) + Math.Abs(c * s * s);
        int segs = Math.Max(1, (int)Math.Ceiling(turn / 0.2) + (int)Math.Ceiling(Math.Abs(s) / Math.Max(Length, 1e-9) * 2));
        double a = StartAngle, k0 = K0;
        var d = Numerics.Integrate(t => Vec2.FromAngle(a + k0 * t + c * t * t), 0, s, segs);
        return Start + d;
    }

    public Vec2 EndPoint => PointAt(Length);
    public double EndAngle => AngleAt(Length);

    public Box2 Bounds
    {
        get
        {
            if (_bounds is { } b) return b;
            var box = Box2.Empty;
            int n = SampleCount();
            for (int i = 0; i <= n; i++) box.Add(PointAt(Length * i / n));
            // Inflate by the maximum sagitta between samples so the box is conservative.
            double k = Math.Max(Math.Abs(K0), Math.Abs(K1));
            double h = Length / n;
            box = box.Inflate(k * h * h / 8 + Numerics.LinearTol);
            _bounds = box;
            return box;
        }
    }

    /// <summary>Number of chord samples needed to keep angular change per chord small.</summary>
    public int SampleCount(double maxAngle = 0.02)
    {
        double turn = Math.Abs(AngleAt(Length) - StartAngle);
        double k = Math.Max(Math.Abs(K0), Math.Abs(K1));
        turn = Math.Max(turn, k * Length);
        return Math.Clamp((int)Math.Ceiling(turn / maxAngle), 1, 4096);
    }

    /// <summary>
    /// All local distances s in [0, Length] where the perpendicular from p meets this element.
    /// </summary>
    public IEnumerable<double> PerpendicularFeet(Vec2 p)
    {
        double F(double s) => Vec2.Dot(p - PointAt(s), TangentAt(s));
        int n = Math.Max(4, SampleCount(0.1) * 2);
        return Numerics.FindRoots(F, 0, Length, n);
    }

    public HorizontalElement Clone() => new(Start, StartAngle, Length, K0, K1);

    public override string ToString() => $"{Kind} L={Length:F3} R={Radius:F3} @({Start.X:F3},{Start.Y:F3})";
}
