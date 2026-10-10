using SBM.Core.Geometry;

namespace SBM.Core.Alignments;

/// <summary>One row of a PI table. The first and last rows are the POB/POE (curve data ignored).</summary>
public sealed class PiRow
{
    public double Northing { get; set; }
    public double Easting { get; set; }
    /// <summary>Circular curve radius; 0 = angle point (no curve).</summary>
    public double Radius { get; set; }
    public double SpiralIn { get; set; }
    public double SpiralOut { get; set; }

    public PiRow() { }
    public PiRow(double northing, double easting, double radius = 0, double spiralIn = 0, double spiralOut = 0)
    {
        Northing = northing;
        Easting = easting;
        Radius = radius;
        SpiralIn = spiralIn;
        SpiralOut = spiralOut;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public Vec2 Point => new(Easting, Northing);
}

/// <summary>Computed curve data for one PI (for reports and the alignment editor).</summary>
public sealed record PiCurveData(int PiIndex, double Deflection, Vec2 TS, Vec2 ST, double TangentIn, double TangentOut,
    double ArcLength, double SpiralInAngle, double SpiralOutAngle);

/// <summary>Alignment defined by PIs with spiral-curve-spiral transitions. Builds exact horizontal elements.</summary>
public sealed class PiDefinition
{
    public List<PiRow> Pis { get; set; } = new();

    /// <summary>Builds the elements. Throws <see cref="AlignmentException"/> with a user-readable message if invalid.</summary>
    public List<HorizontalElement> Build(out List<PiCurveData> curves)
    {
        curves = new List<PiCurveData>();
        if (Pis.Count < 2) throw new AlignmentException("At least two PIs (POB and POE) are required.");
        for (int i = 1; i < Pis.Count; i++)
            if (Vec2.Distance(Pis[i].Point, Pis[i - 1].Point) < 1e-6)
                throw new AlignmentException($"PI {i} and PI {i + 1} are coincident.");

        var elements = new List<HorizontalElement>();
        Vec2 cursor = Pis[0].Point;
        double prevTangentOut = 0; // tangent length consumed after previous PI

        for (int i = 1; i < Pis.Count - 1; i++)
        {
            var pi = Pis[i];
            var u1 = (pi.Point - Pis[i - 1].Point).Normalized();
            var u2 = (Pis[i + 1].Point - pi.Point).Normalized();
            double delta = Math.Atan2(Vec2.Cross(u1, u2), Vec2.Dot(u1, u2)); // signed, +left
            double absDelta = Math.Abs(delta);

            if (pi.Radius <= 0 || absDelta < 1e-10)
            {
                if (prevTangentOut > Vec2.Distance(Pis[i - 1].Point, pi.Point) + 1e-6)
                    throw new AlignmentException($"Curve at PI {i} extends past PI {i + 1}.");
                // Angle point or straight-through PI: run tangent to the PI.
                elements.Add(HorizontalElement.Line(cursor, pi.Point));
                cursor = pi.Point;
                prevTangentOut = 0;
                continue;
            }

            double r = pi.Radius, ls1 = Math.Max(0, pi.SpiralIn), ls2 = Math.Max(0, pi.SpiralOut);
            double th1 = ls1 / (2 * r), th2 = ls2 / (2 * r);
            if (th1 + th2 >= absDelta)
                throw new AlignmentException($"PI {i + 1}: spirals are too long for the {Numerics.RadToDeg(absDelta):F4}° deflection (total spiral angle {Numerics.RadToDeg(th1 + th2):F4}°).");

            double sigma = Math.Sign(delta);
            var (k1, p1) = SpiralShifts(ls1, r);
            var (k2, p2) = SpiralShifts(ls2, r);

            // Center lies at distance (R+p1) inside the back tangent and (R+p2) inside the ahead tangent.
            var n1 = u1.Left * sigma;
            var n2 = u2.Left * sigma;
            var c = SolveCenter(pi.Point, n1, r + p1, n2, r + p2);
            var foot1 = c - n1 * (r + p1);
            var foot2 = c - n2 * (r + p2);
            var ts = foot1 - u1 * k1;
            var st = foot2 + u2 * k2;

            double tIn = Vec2.Dot(pi.Point - ts, u1);
            double tOut = Vec2.Dot(st - pi.Point, u2);
            double back = Vec2.Distance(Pis[i - 1].Point, pi.Point);
            if (tIn + prevTangentOut > back + 1e-6)
                throw new AlignmentException($"Curves at PI {i} and PI {i + 1} overlap (need {tIn + prevTangentOut:F3}, have {back:F3}).");

            double arcLen = r * (absDelta - th1 - th2);
            curves.Add(new PiCurveData(i, delta, ts, st, tIn, tOut, arcLen, th1, th2));

            // Tangent from cursor to TS.
            if (Vec2.Distance(cursor, ts) > 1e-9) elements.Add(HorizontalElement.Line(cursor, ts));

            double a0 = u1.Angle;
            double k = sigma / r;
            var cur = ts;
            double ang = a0;
            if (ls1 > 0)
            {
                var sp = new HorizontalElement(cur, ang, ls1, 0, k);
                elements.Add(sp);
                cur = sp.EndPoint; ang = sp.EndAngle;
            }
            var arc = new HorizontalElement(cur, ang, arcLen, k, k);
            elements.Add(arc);
            cur = arc.EndPoint; ang = arc.EndAngle;
            if (ls2 > 0)
            {
                var sp = new HorizontalElement(cur, ang, ls2, k, 0);
                elements.Add(sp);
                cur = sp.EndPoint;
            }
            // Snap to the exact ST to stop round-off accumulating along the alignment.
            cursor = st;
            prevTangentOut = tOut;
        }

        var poe = Pis[^1].Point;
        double last = Vec2.Distance(Pis[^2].Point, poe);
        if (prevTangentOut > last + 1e-6)
            throw new AlignmentException($"Last curve extends past the POE (needs {prevTangentOut:F3}, have {last:F3}).");
        if (Vec2.Distance(cursor, poe) > 1e-9) elements.Add(HorizontalElement.Line(cursor, poe));
        return elements;
    }

    /// <summary>Clothoid shift values: k (abscissa of the shifted PC) and p (offset of the shifted curve).</summary>
    public static (double k, double p) SpiralShifts(double ls, double r)
    {
        if (ls <= 0) return (0, 0);
        var sp = new HorizontalElement(Vec2.Zero, 0, ls, 0, 1 / r);
        var e = sp.EndPoint;
        double th = ls / (2 * r);
        return (e.X - r * Math.Sin(th), e.Y - r * (1 - Math.Cos(th)));
    }

    private static Vec2 SolveCenter(Vec2 pi, Vec2 n1, double d1, Vec2 n2, double d2)
    {
        // dot(C - PI, n1) = d1 ; dot(C - PI, n2) = d2
        double det = n1.X * n2.Y - n1.Y * n2.X;
        double x = (d1 * n2.Y - n1.Y * d2) / det;
        double y = (n1.X * d2 - d1 * n2.X) / det;
        return pi + new Vec2(x, y);
    }

    /// <summary>
    /// Attempts to recover a PI table from horizontal elements consisting of tangents and
    /// (full) spiral-arc-spiral groups. Returns null if the geometry can't be expressed this way.
    /// </summary>
    public static PiDefinition? TryExtract(IReadOnlyList<HorizontalElement> elements)
    {
        if (elements.Count == 0) return null;
        if (elements[0].Kind != HorizontalElementKind.Line || elements[^1].Kind != HorizontalElementKind.Line) return null;

        var def = new PiDefinition();
        def.Pis.Add(new PiRow(elements[0].Start.Y, elements[0].Start.X));
        int i = 1;
        var prevLine = elements[0];
        while (i < elements.Count)
        {
            var e = elements[i];
            if (e.Kind == HorizontalElementKind.Line)
            {
                // Angle point between two consecutive lines.
                if (!Numerics.IntersectLines(prevLine.Start, prevLine.TangentAt(0), e.Start, e.TangentAt(0), out double t, out _))
                {
                    prevLine = e; i++; continue; // collinear: merge
                }
                var pi = prevLine.Start + prevLine.TangentAt(0) * t;
                def.Pis.Add(new PiRow(pi.Y, pi.X));
                prevLine = e; i++;
                continue;
            }

            // Collect curve group until the next line.
            double ls1 = 0, ls2 = 0, radius = 0;
            int j = i;
            if (elements[j].Kind == HorizontalElementKind.Spiral)
            {
                if (Math.Abs(elements[j].K0) > 1e-12) return null;
                ls1 = elements[j].Length; j++;
            }
            if (j >= elements.Count || elements[j].Kind != HorizontalElementKind.Arc) return null;
            radius = elements[j].Radius; j++;
            if (j < elements.Count && elements[j].Kind == HorizontalElementKind.Spiral)
            {
                if (Math.Abs(elements[j].K1) > 1e-12) return null;
                ls2 = elements[j].Length; j++;
            }
            if (j >= elements.Count || elements[j].Kind != HorizontalElementKind.Line) return null;
            var next = elements[j];
            if (!Numerics.IntersectLines(prevLine.Start, prevLine.TangentAt(0), next.Start, next.TangentAt(0), out double tt, out _))
                return null;
            var p = prevLine.Start + prevLine.TangentAt(0) * tt;
            def.Pis.Add(new PiRow(p.Y, p.X, radius, ls1, ls2));
            prevLine = next;
            i = j + 1;
        }
        var end = elements[^1].EndPoint;
        def.Pis.Add(new PiRow(end.Y, end.X));
        return def;
    }

    public PiDefinition Clone() => new()
    {
        Pis = Pis.Select(p => new PiRow(p.Northing, p.Easting, p.Radius, p.SpiralIn, p.SpiralOut)).ToList(),
    };
}

public sealed class AlignmentException(string message) : Exception(message);
