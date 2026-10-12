using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>
/// Closed plate outline of straight edges and TRUE circular arcs (2D). Edge i runs from <see cref="Points"/>[i] to [i + 1] (wrapping);
/// <see cref="Radii"/>[i] = 0 for a straight edge, otherwise the arc radius, positive when the arc turns counter-clockwise from its start
/// to its end (centre to the left of the chord), negative clockwise. Arcs are minor arcs (sweep &lt; 180 deg). Builders tessellate it for
/// the mesh (<see cref="Tessellate"/>) and keep it on the recorded <see cref="ExtrudedSolid.ExactOutline"/> so IFC writes IfcArcIndex.
/// </summary>
public sealed class ArcPolygon
{
    public Vec2[] Points { get; }
    public double[] Radii { get; }

    public ArcPolygon(IReadOnlyList<Vec2> points, IReadOnlyList<double>? radii = null)
    {
        Points = points.ToArray();
        Radii = radii?.ToArray() ?? new double[Points.Length];
        if (Radii.Length != Points.Length) throw new ArgumentException("one radius per edge");
        for (int i = 0; i < Points.Length; i++)
        {
            double r = Radii[i];
            if (r == 0) continue;
            double chord = Vec2.Distance(Points[i], Points[(i + 1) % Points.Length]);
            if (Math.Abs(r) < chord / 2 - 1e-9) throw new ArgumentException($"edge {i}: radius {Math.Abs(r)} shorter than half the chord {chord / 2}");
        }
    }

    public int Count => Points.Length;
    public bool IsArc(int i) => Radii[i] != 0;
    public Vec2 End(int i) => Points[(i + 1) % Points.Length];

    /// <summary>Centre of arc edge i (minor arc; centre left of the chord for a CCW arc).</summary>
    public Vec2 Center(int i)
    {
        var a = Points[i]; var b = End(i); double r = Radii[i];
        var m = (a + b) * 0.5;
        double half = Vec2.Distance(a, b) / 2;
        double h = Math.Sqrt(Math.Max(0, r * r - half * half));
        var left = (b - a).Normalized().Left;
        return m + left * (r > 0 ? h : -h);
    }

    /// <summary>Signed sweep (radians, + CCW) of arc edge i.</summary>
    public double Sweep(int i)
    {
        var c = Center(i);
        double a0 = Math.Atan2(Points[i].Y - c.Y, Points[i].X - c.X), a1 = Math.Atan2(End(i).Y - c.Y, End(i).X - c.X);
        double s = a1 - a0;
        while (s > Math.PI) s -= 2 * Math.PI;
        while (s < -Math.PI) s += 2 * Math.PI;
        return Radii[i] > 0 ? Math.Abs(s) : -Math.Abs(s);
    }

    /// <summary>Point of edge i at parameter t (0..1).</summary>
    public Vec2 At(int i, double t)
    {
        if (!IsArc(i)) return Points[i] + (End(i) - Points[i]) * t;
        var c = Center(i);
        double a0 = Math.Atan2(Points[i].Y - c.Y, Points[i].X - c.X) + Sweep(i) * t;
        return c + new Vec2(Math.Cos(a0), Math.Sin(a0)) * Math.Abs(Radii[i]);
    }

    /// <summary>Mid point of arc edge i (IfcArcIndex middle point).</summary>
    public Vec2 Mid(int i) => At(i, 0.5);

    /// <summary>Polygon: vertices plus arc points so that no chord is farther than <paramref name="maxSagitta"/> from its arc.</summary>
    public List<Vec2> Tessellate(double maxSagitta)
    {
        var o = new List<Vec2>();
        for (int i = 0; i < Count; i++)
        {
            o.Add(Points[i]);
            if (!IsArc(i)) continue;
            double r = Math.Abs(Radii[i]);
            double step = 2 * Math.Acos(Math.Max(-1, 1 - Math.Min(maxSagitta, r) / r));
            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(Sweep(i)) / Math.Max(step, 1e-6)));
            for (int k = 1; k < n; k++) o.Add(At(i, (double)k / n));
        }
        return o;
    }

    /// <summary>Exact enclosed area (+ counter-clockwise).</summary>
    public double SignedArea()
    {
        double a = 0;
        for (int i = 0; i < Count; i++)
        {
            var p = Points[i]; var q = End(i);
            a += (p.X * q.Y - q.X * p.Y) / 2;
            if (IsArc(i))
            {
                double r = Math.Abs(Radii[i]), s = Sweep(i);
                a += Math.Sign(s) * r * r / 2 * (Math.Abs(s) - Math.Sin(Math.Abs(s)));   // circular segment, left of the chord for CCW
            }
        }
        return a;
    }

    /// <summary>The same outline run the other way.</summary>
    public ArcPolygon Reversed()
    {
        int n = Count;
        var p = new Vec2[n]; var r = new double[n];
        for (int k = 0; k < n; k++)
        {
            p[k] = Points[(n - k) % n];
            r[k] = -Radii[(n - 1 - k + n) % n];
        }
        return new ArcPolygon(p, r);
    }

    public ArcPolygon CounterClockwise() => SignedArea() < 0 ? Reversed() : this;

    /// <summary>Applies a rigid (rotation / reflection + translation) map; a reflection flips the arc senses.</summary>
    public ArcPolygon Map(Func<Vec2, Vec2> f, bool reflects) => new(Points.Select(f).ToArray(), Radii.Select(r => reflects ? -r : r).ToArray());

    /// <summary>The part from vertex <paramref name="i"/> to vertex <paramref name="j"/> (along the outline) closed by a straight edge j -> i.</summary>
    public ArcPolygon Sub(int i, int j)
    {
        var p = new List<Vec2>(); var r = new List<double>();
        for (int k = i; ; k = (k + 1) % Count)
        {
            p.Add(Points[k]);
            if (k == j) { r.Add(0); break; }
            r.Add(Radii[k]);
        }
        return new ArcPolygon(p, r);
    }
}
