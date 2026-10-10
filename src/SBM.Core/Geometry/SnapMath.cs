using System.Numerics;
using System.Runtime.CompilerServices;

namespace SBM.Core.Geometry;

/// <summary>
/// Single-precision geometry primitives for object snaps and picking (scene-local coordinates, an orthographic ray is a
/// point + unit direction). Pure functions so they can be unit tested; used by the viewport's snap engine.
/// </summary>
public static class SnapMath
{
    /// <summary>Perpendicular distance from a point to the infinite line through <paramref name="o"/> along unit <paramref name="d"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceToRay(Vector3 p, Vector3 o, Vector3 d)
    {
        var v = p - o;
        float t = Vector3.Dot(v, d);
        return (v - d * t).Length();
    }

    /// <summary>Squared perpendicular distance from a point to the ray's line (cheaper for range tests).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceToRaySq(Vector3 p, Vector3 o, Vector3 d, out float t)
    {
        var v = p - o;
        t = Vector3.Dot(v, d);
        return (v - d * t).LengthSquared();
    }

    /// <summary>Nearest point on segment AB to P; <paramref name="t"/> is the parameter 0..1.</summary>
    public static Vector3 NearestOnSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        t = l2 > 1e-20f ? Math.Clamp(Vector3.Dot(p - a, ab) / l2, 0f, 1f) : 0f;
        return a + ab * t;
    }

    /// <summary>
    /// Foot of the perpendicular from P to the line AB, if it falls inside the segment (strictly between the end points,
    /// with a small margin so it does not duplicate the endpoint snaps).
    /// </summary>
    public static bool PerpendicularFoot(Vector3 p, Vector3 a, Vector3 b, out Vector3 foot)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        foot = default;
        if (l2 < 1e-20f) return false;
        float t = Vector3.Dot(p - a, ab) / l2;
        if (t <= 0.001f || t >= 0.999f) return false;
        foot = a + ab * t;
        return true;
    }

    /// <summary>
    /// Closest point of segment AB to the ray's line (o, unit d). Returns the segment point; <paramref name="distance"/> is
    /// the distance between the two closest points (ray line ↔ segment) and <paramref name="rayT"/> the ray parameter.
    /// </summary>
    public static Vector3 ClosestOnSegmentToRay(Vector3 o, Vector3 d, Vector3 a, Vector3 b, out float distance, out float rayT)
    {
        var u = b - a;
        var w = a - o;
        float uu = Vector3.Dot(u, u), ud = Vector3.Dot(u, d), wu = Vector3.Dot(w, u), wd = Vector3.Dot(w, d);
        float den = uu - ud * ud; // d is unit; |u'|^2 where u' is u without its component along the ray
        // distance^2(s) = |w' + s u'|^2  ->  s = -(w'.u') / (u'.u') with w'.u' = wu - wd ud
        float s = den < 1e-12f * Math.Max(uu, 1e-12f) ? 0.5f : Math.Clamp(-(wu - wd * ud) / den, 0f, 1f);
        var q = a + u * s;
        rayT = Vector3.Dot(q - o, d);
        distance = (q - o - d * rayT).Length();
        return q;
    }

    /// <summary>Two-sided Möller–Trumbore ray / triangle test; t is the distance along the (unit) direction.</summary>
    public static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, p);
        if (det > -1e-12f && det < 1e-12f) return false;
        float inv = 1f / det;
        var s = o - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u < -1e-6f || u > 1 + 1e-6f) return false;
        var q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(d, q) * inv;
        if (v < -1e-6f || u + v > 1 + 1e-6f) return false;
        t = Vector3.Dot(e2, q) * inv;
        return true;
    }

    /// <summary>Ray (point, direction reciprocals) against a box inflated by <paramref name="pad"/>; the ray is a line (both directions count).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LineHitsBox(Vector3 o, Vector3 invD, Vector3 min, Vector3 max, float pad)
    {
        float t0 = float.NegativeInfinity, t1 = float.PositiveInfinity;
        if (!Slab(o.X, invD.X, min.X - pad, max.X + pad, ref t0, ref t1)) return false;
        if (!Slab(o.Y, invD.Y, min.Y - pad, max.Y + pad, ref t0, ref t1)) return false;
        if (!Slab(o.Z, invD.Z, min.Z - pad, max.Z + pad, ref t0, ref t1)) return false;
        return t1 >= t0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Slab(float o, float inv, float lo, float hi, ref float t0, ref float t1)
    {
        if (float.IsInfinity(inv))
            return o >= lo && o <= hi; // parallel to the slab
        float a = (lo - o) * inv, b = (hi - o) * inv;
        if (a > b) (a, b) = (b, a);
        if (a > t0) t0 = a;
        if (b < t1) t1 = b;
        return t1 >= t0;
    }

    /// <summary>Reciprocal of a direction with exact zeros mapped to infinity (for <see cref="LineHitsBox"/>).</summary>
    public static Vector3 Reciprocal(Vector3 d) => new(
        Math.Abs(d.X) < 1e-12f ? float.PositiveInfinity : 1f / d.X,
        Math.Abs(d.Y) < 1e-12f ? float.PositiveInfinity : 1f / d.Y,
        Math.Abs(d.Z) < 1e-12f ? float.PositiveInfinity : 1f / d.Z);

    /// <summary>Intersection of 2D segments P1P2 and P3P4 (parameters s on the first, t on the second, both inside 0..1).</summary>
    public static bool SegmentIntersect2D(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out float s, out float t)
    {
        s = t = 0;
        var r = p2 - p1;
        var q = p4 - p3;
        float den = r.X * q.Y - r.Y * q.X;
        if (Math.Abs(den) < 1e-12f * Math.Max(1f, r.LengthSquared() + q.LengthSquared())) return false; // parallel
        var w = p3 - p1;
        s = (w.X * q.Y - w.Y * q.X) / den;
        t = (w.X * r.Y - w.Y * r.X) / den;
        return s >= -1e-6f && s <= 1 + 1e-6f && t >= -1e-6f && t <= 1 + 1e-6f;
    }

    /// <summary>Axis-aligned bounds of a transformed box (the 8 corners).</summary>
    public static (Vector3 min, Vector3 max) TransformBox(Vector3 min, Vector3 max, Matrix4x4 m)
    {
        var lo = new Vector3(float.MaxValue);
        var hi = new Vector3(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var c = Vector3.Transform(new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z), m);
            lo = Vector3.Min(lo, c);
            hi = Vector3.Max(hi, c);
        }
        return (lo, hi);
    }
}
