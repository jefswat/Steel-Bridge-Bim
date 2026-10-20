using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;

namespace SBM.Core.Checks.Measure;

/// <summary>A length as a checker reads it: inches (US decimal), ft-in to 1/16" (plan style) and millimetres.</summary>
public readonly record struct Len(
    [property: JsonPropertyName("in")] double In,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("mm")] double Mm)
{
    public static Len Of(double meters) => new(Math.Round(meters / 0.0254, 4), Fmt.FtIn(meters), Math.Round(meters * 1000, 2));
    public override string ToString() => Text;
}

/// <summary>Formatting shared by the measure tools: plan-style ft-in to 1/16" (inches below 1 ft), SI in mm.</summary>
public static class Fmt
{
    private static readonly UnitSystem Us16 = new() { Kind = UnitSystemKind.US, InchFraction = 16 };
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>12'-3 1/2" above one foot, 9 5/16" below (rounded to 1/16").</summary>
    public static string FtIn(double meters)
    {
        if (double.IsNaN(meters) || double.IsInfinity(meters)) return "";
        double inches = Math.Abs(meters) / 0.0254;
        double r = Math.Round(inches * 16) / 16;
        return r < 12 ? (meters < 0 && r > 0 ? "-" : "") + Us16.FormatInches(r) : Us16.FormatLength(meters);
    }

    public static string Mm(double meters) => (meters * 1000).ToString("0.0", Inv) + " mm";
    public static string Both(double meters) => $"{FtIn(meters)} ({Mm(meters)})";
    public static string Deg(double degrees) => degrees.ToString("0.00", Inv) + "°";
    public static double R(double v, int digits = 4) => Math.Round(v, digits);

    /// <summary>Length in the units' short display unit (in / mm), for coordinate lists.</summary>
    public static double Short(UnitSystem u, double meters) => Math.Round(u.ToDisplay(meters, LengthKind.Short), u.IsUS ? 4 : 2);
    public static double Long(UnitSystem u, double meters) => Math.Round(u.ToDisplay(meters, LengthKind.Long), u.IsUS ? 4 : 4);

    /// <summary>Equal spaces collapsed: "3 spa @ 3" = 9"" items joined by " | ".</summary>
    public static string Chain(IReadOnlyList<double> spaces, double tol)
    {
        var parts = new List<string>();
        for (int i = 0; i < spaces.Count;)
        {
            int j = i + 1;
            while (j < spaces.Count && Math.Abs(spaces[j] - spaces[i]) <= tol) j++;
            int n = j - i;
            double avg = spaces.Skip(i).Take(n).Average();
            parts.Add(n == 1 ? FtIn(avg) : $"{n} spa @ {FtIn(avg)} = {FtIn(avg * n)}");
            i = j;
        }
        return string.Join(" | ", parts);
    }

    /// <summary>Runs of equal spaces (count, space).</summary>
    public static List<SpaceRun> Runs(IReadOnlyList<double> spaces, double tol)
    {
        var res = new List<SpaceRun>();
        for (int i = 0; i < spaces.Count;)
        {
            int j = i + 1;
            while (j < spaces.Count && Math.Abs(spaces[j] - spaces[i]) <= tol) j++;
            double avg = spaces.Skip(i).Take(j - i).Average();
            res.Add(new SpaceRun(j - i, Len.Of(avg), Len.Of(avg * (j - i))));
            i = j;
        }
        return res;
    }

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(object o) => JsonSerializer.Serialize(o, o.GetType(), Json);
}

public sealed record SpaceRun(int Count, Len Space, Len Total);

/// <summary>Which model element a measured item is: bridge, kind ("part", "bolt", "deck", "haunch", "barrier", "sub", "psbeam", "bar", "misc", "pad"),
/// mark / name, assembly, index (bars: position in the IFC export list; bolts: index in the assembly) and structural unit.</summary>
public sealed class ElementRef
{
    public required string Bridge { get; init; }
    public required string Kind { get; init; }
    public required string Mark { get; init; }
    public string? Assembly { get; init; }
    public int? Index { get; init; }
    public string? Unit { get; init; }
    /// <summary>IFC GlobalId of the element (the same id the companion database and IFC export use) when known.</summary>
    public string? GlobalId { get; set; }
    public override string ToString() => Assembly is { Length: > 0 } a && a != Mark ? $"{Mark} ({a})" : Mark;
}

/// <summary>A solid of the generated model with its identity.</summary>
public sealed class MeasureElement
{
    public required ElementRef Ref { get; init; }
    public required Mesh Mesh { get; init; }
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsConcrete { get; init; }
    private Box3? _box;
    public Box3 Bounds => _box ??= Mesh.Bounds();
}

/// <summary>Small 2D polygon / point-set helpers used by the measure tools.</summary>
public static class Geo2
{
    public static double Area(IReadOnlyList<Vec2> p) => MeshBuilder.SignedArea(p);

    public static Vec2 Centroid(IReadOnlyList<Vec2> p)
    {
        double a = 0, cx = 0, cy = 0;
        var o = p[0];
        for (int i = 0; i < p.Count; i++)
        {
            var u = p[i] - o; var v = p[(i + 1) % p.Count] - o;
            double c = Vec2.Cross(u, v);
            a += c; cx += (u.X + v.X) * c; cy += (u.Y + v.Y) * c;
        }
        if (Math.Abs(a) < 1e-18) return new Vec2(p.Average(q => q.X), p.Average(q => q.Y));
        return o + new Vec2(cx / (3 * a), cy / (3 * a));
    }

    public static bool Inside(IReadOnlyList<Vec2> poly, Vec2 q)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Y > q.Y) != (b.Y > q.Y) && q.X < (b.X - a.X) * (q.Y - a.Y) / (b.Y - a.Y) + a.X) c = !c;
        }
        return c;
    }

    public static double SegDist(Vec2 p, Vec2 a, Vec2 b) => (Closest(p, a, b) - p).Length;

    public static Vec2 Closest(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a; double l2 = ab.LengthSquared;
        double t = l2 < 1e-24 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
        return a + ab * t;
    }

    public static double SegSegDist(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        if (SegmentsCross(a, b, c, d)) return 0;
        return Math.Min(Math.Min(SegDist(a, c, d), SegDist(b, c, d)), Math.Min(SegDist(c, a, b), SegDist(d, a, b)));
    }

    public static bool SegmentsCross(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double d1 = Vec2.Cross(b - a, c - a), d2 = Vec2.Cross(b - a, d - a), d3 = Vec2.Cross(d - c, a - c), d4 = Vec2.Cross(d - c, b - c);
        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    /// <summary>Distance from p to the infinite line through a, b.</summary>
    public static double LineDist(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a; double l = ab.Length;
        return l < 1e-15 ? (p - a).Length : Math.Abs(Vec2.Cross(ab, p - a)) / l;
    }

    /// <summary>Ray p + t·d (t > 0) against segment a-b: t, or NaN.</summary>
    public static double Ray(Vec2 p, Vec2 d, Vec2 a, Vec2 b)
    {
        var e = b - a;
        double den = Vec2.Cross(d, e);
        if (Math.Abs(den) < 1e-15) return double.NaN;
        var ap = a - p;
        double t = Vec2.Cross(ap, e) / den, s = Vec2.Cross(ap, d) / den;
        return t > 1e-12 && s >= -1e-9 && s <= 1 + 1e-9 ? t : double.NaN;
    }

    /// <summary>Removes repeated points and vertices that lie on the chord of their neighbours (within <paramref name="tol"/> m).</summary>
    public static List<Vec2> Simplify(IReadOnlyList<Vec2> loop, double tol = 2e-5)
    {
        var p = new List<Vec2>();
        foreach (var q in loop) if (p.Count == 0 || (q - p[^1]).Length > tol) p.Add(q);
        if (p.Count > 1 && (p[0] - p[^1]).Length <= tol) p.RemoveAt(p.Count - 1);
        bool changed = true;
        while (changed && p.Count > 3)
        {
            changed = false;
            for (int i = 0; i < p.Count && p.Count > 3; i++)
            {
                var a = p[(i - 1 + p.Count) % p.Count]; var b = p[(i + 1) % p.Count];
                var ab = b - a; var ap = p[i] - a;
                double l = ab.Length;
                if (l < 1e-12) { p.RemoveAt(i); changed = true; break; }
                double t = Vec2.Dot(ap, ab) / (l * l);
                if (Math.Abs(Vec2.Cross(ab, ap)) / l < tol && t > 0 && t < 1) { p.RemoveAt(i); changed = true; i--; }
            }
        }
        return p;
    }

    /// <summary>1D clusters of sorted values: a new cluster starts where the gap exceeds <paramref name="tol"/>. Returns the cluster means and the index of each value's cluster.</summary>
    public static (List<double> Centers, int[] Of) Cluster(IReadOnlyList<double> values, double tol)
    {
        var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToArray();
        var of = new int[values.Count];
        var centers = new List<double>();
        var cur = new List<int>();
        void Flush() { if (cur.Count == 0) return; double m = cur.Average(i => values[i]); foreach (int i in cur) of[i] = centers.Count; centers.Add(m); cur.Clear(); }
        for (int k = 0; k < order.Length; k++)
        {
            if (cur.Count > 0 && values[order[k]] - values[cur[^1]] > tol) Flush();
            cur.Add(order[k]);
        }
        Flush();
        return (centers, of);
    }

    /// <summary>Circle through three points (2D): center and radius (radius NaN when collinear).</summary>
    public static (Vec2 C, double R) Circle(Vec2 a, Vec2 b, Vec2 c)
    {
        double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (Math.Abs(d) < 1e-18) return (a, double.NaN);
        double a2 = a.LengthSquared, b2 = b.LengthSquared, c2 = c.LengthSquared;
        var o = new Vec2((a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d, (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d);
        return (o, (a - o).Length);
    }
}

/// <summary>3D helpers.</summary>
public static class Geo3
{
    public static Vec3 ClosestOnTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;
        var bp = p - b; double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;
        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        var cp = p - c; double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;
        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        double den = 1 / (va + vb + vc);
        return a + ab * (vb * den) + ac * (vc * den);
    }

    /// <summary>Closest points of segments p1-q1 and p2-q2.</summary>
    public static (Vec3 A, Vec3 B) ClosestSegSeg(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2)
    {
        var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2;
        double a = Vec3.Dot(d1, d1), e = Vec3.Dot(d2, d2), f = Vec3.Dot(d2, r);
        double s, t;
        if (a <= 1e-24 && e <= 1e-24) return (p1, p2);
        if (a <= 1e-24) { s = 0; t = Math.Clamp(f / e, 0, 1); }
        else
        {
            double c = Vec3.Dot(d1, r);
            if (e <= 1e-24) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
            else
            {
                double b = Vec3.Dot(d1, d2), den = a * e - b * b;
                s = den > 1e-24 ? Math.Clamp((b * f - c * e) / den, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((b - c) / a, 0, 1); }
            }
        }
        return (p1 + d1 * s, p2 + d2 * t);
    }

    public static double BoxDistance(Box3 a, Box3 b)
    {
        double dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
        double dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
        double dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static Vec3 Parse(string text)
    {
        var p = text.Split(',', StringSplitOptions.TrimEntries);
        if (p.Length != 3) throw new ArgumentException($"Expected x,y,z, got '{text}'.");
        return new Vec3(double.Parse(p[0], Fmt.Inv), double.Parse(p[1], Fmt.Inv), double.Parse(p[2], Fmt.Inv));
    }

    /// <summary>World vector as a short list [x, y, z] (6 decimals).</summary>
    public static double[] Arr(Vec3 v) => new[] { Math.Round(v.X, 6), Math.Round(v.Y, 6), Math.Round(v.Z, 6) };
}
