using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>ASTM A615/A706 deformed bar (US customary designation). Meters, m², kg/m.</summary>
public sealed record RebarSize(string Name, double Diameter, double Area, double MassPerLength)
{
    /// <summary>Minimum inside bend diameter for standard hooks/bends (AASHTO 5.10.2.3: 6db for #3–#8, 8db for #9–#11, 10db for #14/#18).
    /// Stirrups and ties #3-#5 may use 4db: <see cref="RebarBends.StandardPin"/>.</summary>
    public double MinPinDiameter => (Name is "#14" or "#18" ? 10 : Name is "#9" or "#10" or "#11" ? 8 : 6) * Diameter;
}

public static class RebarCatalog
{
    private static RebarSize B(string n, double dIn, double aIn2, double lbPerFt) =>
        new(n, In(dIn), aIn2 * 0.00064516, lbPerFt * 1.48816394);

    public static IReadOnlyList<RebarSize> Sizes { get; } = new[]
    {
        B("#3", 0.375, 0.11, 0.376), B("#4", 0.500, 0.20, 0.668), B("#5", 0.625, 0.31, 1.043), B("#6", 0.750, 0.44, 1.502),
        B("#7", 0.875, 0.60, 2.044), B("#8", 1.000, 0.79, 2.670), B("#9", 1.128, 1.00, 3.400), B("#10", 1.270, 1.27, 4.303),
        B("#11", 1.410, 1.56, 5.313), B("#14", 1.693, 2.25, 7.650), B("#18", 2.257, 4.00, 13.600),
    };

    public static RebarSize Size(string name) =>
        Sizes.FirstOrDefault(s => s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Sizes[2];

    /// <summary>
    /// Tension development length per AASHTO LRFD 5.10.8.2.1a: ℓdb = 2.4·db·fy/√f'c (in, ksi), modified by
    /// λrc = db/cb (0.4–1.0, cb = min(cover to center, half spacing), Ktr = 0) and λcf for epoxy (1.5 if cover &lt; 3db or
    /// clear spacing &lt; 6db, else 1.2), λrl·λcf ≤ 1.7; not less than 12 in.
    /// </summary>
    public static double DevelopmentLength(RebarSize bar, double fcKsi, double fyKsi, bool epoxy, double clearCover, double spacing, bool topBar = false)
    {
        double db = bar.Diameter / 0.0254;
        double ldb = 2.4 * db * fyKsi / Math.Sqrt(fcKsi);
        double cb = Math.Min(clearCover / 0.0254 + db / 2, spacing / 0.0254 / 2);
        double lrc = Math.Clamp(db / Math.Max(cb, 1e-6), 0.4, 1.0);
        double lcf = !epoxy ? 1.0 : (clearCover / 0.0254 < 3 * db || (spacing / 0.0254 - db) < 6 * db ? 1.5 : 1.2);
        double lrl = topBar ? 1.3 : 1.0;
        double f = Math.Min(lrl * lcf, 1.7);
        return In(Math.Max(ldb * lrc * f, 12));
    }

    /// <summary>Class B tension lap splice (1.3 ℓd), rounded up to the inch.</summary>
    public static double ClassBLap(RebarSize bar, double fcKsi, double fyKsi, bool epoxy, double clearCover, double spacing, bool topBar = false)
    {
        double lap = 1.3 * DevelopmentLength(bar, fcKsi, fyKsi, epoxy, clearCover, spacing, topBar);
        return Math.Ceiling(lap / In(1) - 1e-9) * In(1);
    }
}

/// <summary>One dimension on a bending diagram (out-to-out), with where to draw it in the shape's 2D frame.</summary>
public sealed record BarDimension(string Letter, double Value, Vec2 From, Vec2 To, Vec2 Normal);

/// <summary>
/// Bar shape for scheduling and bend diagrams: a 2D centerline (with bends as arcs) in the bar's own plane, and the
/// labeled out-to-out dimensions.
/// </summary>
public sealed class BarShape
{
    public required string Code { get; init; }
    public required string Description { get; init; }
    public required Vec2[] Centerline { get; init; }
    public List<BarDimension> Dimensions { get; } = new();
    /// <summary>Inside bend (pin) diameter, 0 for straight bars.</summary>
    public double PinDiameter { get; init; }

    /// <summary>Developed length when it is not the centerline length (spirals: the centerline is drawn as one turn).</summary>
    public double? FixedLength { get; init; }

    public double DevelopedLength
    {
        get
        {
            if (FixedLength is { } fixedLength) return fixedLength;
            double l = 0;
            for (int i = 1; i < Centerline.Length; i++) l += Vec2.Distance(Centerline[i - 1], Centerline[i]);
            return l;
        }
    }

    public static BarShape Straight(double length) => new()
    {
        Code = "STR", Description = "Straight",
        Centerline = new[] { Vec2.Zero, new Vec2(length, 0) },
        Dimensions = { new BarDimension("A", length, Vec2.Zero, new Vec2(length, 0), new Vec2(0, 1)) },
    };

    /// <summary>Bends (centerline arc center and radius) for drawing the bend radius on diagrams.</summary>
    public List<(Vec2 Center, double Radius)> Bends { get; } = new();

    /// <summary>
    /// Polyline with every interior corner replaced by a tangent arc of the given centerline radius (sampled every ~15°).
    /// Arc centers are added to <paramref name="bends"/>.
    /// </summary>
    public static Vec2[] Rounded(IReadOnlyList<Vec2> pts, double radius, List<(Vec2, double)>? bends = null)
    {
        var res = new List<Vec2> { pts[0] };
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var corner = pts[i];
            var u = (pts[i - 1] - corner).Normalized();
            var v = (pts[i + 1] - corner).Normalized();
            double ang = Math.Acos(Math.Clamp(Vec2.Dot(u, v), -1, 1));
            if (ang < 1e-6 || ang > Math.PI - 1e-6) { res.Add(corner); continue; }
            double t = radius / Math.Tan(ang / 2);
            var p = corner + u * t;
            var q = corner + v * t;
            var c = corner + (u + v).Normalized() * (radius / Math.Sin(ang / 2));
            bends?.Add((c, radius));
            double a1 = (p - c).Angle, a2 = (q - c).Angle;
            double sweep = Numerics.NormalizeAngle(a2 - a1);
            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12)));
            for (int k = 0; k <= n; k++) res.Add(c + Vec2.FromAngle(a1 + sweep * k / n) * radius);
        }
        res.Add(pts[^1]);
        return res.ToArray();
    }

    /// <summary>Two straight legs joined by an arc tangent to both (centerline), sampled every ~15°.</summary>
    public static Vec2[] Fillet(Vec2 a0, Vec2 corner, Vec2 b1, double radius)
    {
        var u = (a0 - corner).Normalized();
        var v = (b1 - corner).Normalized();
        double ang = Math.Acos(Math.Clamp(Vec2.Dot(u, v), -1, 1));
        if (ang < 1e-6 || ang > Math.PI - 1e-6) return new[] { a0, corner, b1 };
        double t = radius / Math.Tan(ang / 2);
        var p = corner + u * t;
        var q = corner + v * t;
        var bis = (u + v).Normalized();
        var c = corner + bis * (radius / Math.Sin(ang / 2));
        double a1 = (p - c).Angle, a2 = (q - c).Angle;
        double sweep = Numerics.NormalizeAngle(a2 - a1);
        int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12)));
        var pts = new List<Vec2> { a0 };
        for (int i = 0; i <= n; i++) pts.Add(c + Vec2.FromAngle(a1 + sweep * i / n) * radius);
        pts.Add(b1);
        return pts.ToArray();
    }
}

/// <summary>A scheduled bar mark: all bars with identical size, shape and dimensions.</summary>
public sealed class BarMark
{
    public required string Mark { get; init; }
    public required RebarSize Size { get; init; }
    public required BarShape Shape { get; init; }
    public required string Location { get; init; }
    /// <summary>Structural unit the mark belongs to ("" = superstructure; otherwise e.g. "Pier 1"). Marks are unique per unit.</summary>
    public string Unit { get; init; } = "";
    public bool Epoxy { get; init; }
    /// <summary>Other coating / material note ("stainless"); empty = plain or epoxy per <see cref="Epoxy"/>.</summary>
    public string Coating { get; init; } = "";
    public int Count { get; set; }
    /// <summary>Individual lengths for variable-length marks (straight bars whose lengths vary, e.g. at skewed ends).</summary>
    public List<double>? VaryingLengths { get; init; }
    public bool Varies => VaryingLengths is { Count: > 1 };
    public double Length => Varies ? VaryingLengths!.Max() : Shape.DevelopedLength;
    public double MinLength => Varies ? VaryingLengths!.Min() : Length;
    public double TotalLength => Varies ? VaryingLengths!.Sum() : Count * Length;
    public double TotalMass => TotalLength * Size.MassPerLength;
}

/// <summary>One placed bar: world centerline (meters).</summary>
public sealed class BarPlacement
{
    public required string Mark { get; init; }
    /// <summary>World centerline. Generators may give sharp corners; <see cref="RebarBends.Apply"/> (run once by the regeneration) replaces them
    /// with true bends of the mark's pin radius, so every consumer reads the bent bar.</summary>
    public required Vec3[] Points { get; set; }
    /// <summary>Set once <see cref="RebarBends.Apply"/> has processed the bar (it is then not rounded again).</summary>
    public bool BendsRounded { get; set; }
    /// <summary>For identical shaped bars: local frame (x, y = shape plane) so they can be GPU-instanced.</summary>
    public Frame3? Frame { get; init; }
    public string Group { get; init; } = "Deck";
    /// <summary>Structural unit of the mark (see <see cref="BarMark.Unit"/>).</summary>
    public string Unit { get; init; } = "";
}
