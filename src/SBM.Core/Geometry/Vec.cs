using System.Runtime.CompilerServices;

namespace SBM.Core.Geometry;

/// <summary>Double-precision 2D vector. X = Easting, Y = Northing for plan geometry.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    /// <summary>Math angle (radians, CCW from +X).</summary>
    public double Angle => Math.Atan2(Y, X);

    public Vec2 Normalized()
    {
        double l = Length;
        return l > 0 ? new Vec2(X / l, Y / l) : Zero;
    }

    /// <summary>Perpendicular rotated 90° CCW (to the left of travel).</summary>
    public Vec2 Left => new(-Y, X);

    /// <summary>Perpendicular rotated 90° CW (to the right of travel).</summary>
    public Vec2 Right => new(Y, -X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 FromAngle(double a) => new(Math.Cos(a), Math.Sin(a));

    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>Z-component of the 3D cross product (positive when b is CCW of a).</summary>
    public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

    public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;

    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public Vec2 Rotate(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new Vec2(X * c - Y * s, X * s + Y * c);
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public Vec3 WithZ(double z) => new(X, Y, z);

    public override string ToString() => $"({X:F4}, {Y:F4})";
}

/// <summary>Double-precision 3D vector. Z = elevation.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public Vec2 XY => new(X, Y);

    public Vec3 Normalized()
    {
        double l = Length;
        return l > 0 ? new Vec3(X / l, Y / l, Z / l) : Zero;
    }

    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static Vec3 Lerp(Vec3 a, Vec3 b, double t) => a + (b - a) * t;

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => new(a.X * s, a.Y * s, a.Z * s);

    public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
}

/// <summary>Axis-aligned 2D bounding box.</summary>
public struct Box2
{
    public double MinX, MinY, MaxX, MaxY;

    public static Box2 Empty => new() { MinX = double.MaxValue, MinY = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue };

    public readonly bool IsEmpty => MinX > MaxX;
    public readonly Vec2 Center => new((MinX + MaxX) / 2, (MinY + MaxY) / 2);
    public readonly double Width => MaxX - MinX;
    public readonly double Height => MaxY - MinY;

    public void Add(Vec2 p)
    {
        if (p.X < MinX) MinX = p.X;
        if (p.Y < MinY) MinY = p.Y;
        if (p.X > MaxX) MaxX = p.X;
        if (p.Y > MaxY) MaxY = p.Y;
    }

    public void Add(Box2 b)
    {
        if (b.IsEmpty) return;
        Add(new Vec2(b.MinX, b.MinY));
        Add(new Vec2(b.MaxX, b.MaxY));
    }

    public readonly Box2 Inflate(double d) => new() { MinX = MinX - d, MinY = MinY - d, MaxX = MaxX + d, MaxY = MaxY + d };

    public readonly bool Contains(Vec2 p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
}
