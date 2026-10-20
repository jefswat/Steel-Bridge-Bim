namespace SBM.Core.Geometry;

/// <summary>
/// Camera path of a turntable video: the orthographic camera circles the scene centre (yaw about world Z), keeping its pitch,
/// its distance from the centre and its view width. Frame <c>i</c> of <c>n</c> turns by <c>degrees * i / n</c> (a full 360 turn
/// therefore does not repeat the first frame at the end, so the video loops).
/// </summary>
public static class Turntable
{
    /// <summary>View of frame <paramref name="i"/> (0-based) of <paramref name="n"/>. <paramref name="startLook"/> is the view direction at frame 0.</summary>
    public static OrthoView Frame(Vec3 center, Vec3 startLook, double distance, double width, int i, int n, double degrees = 360)
    {
        var look = startLook.Normalized();
        double yaw = n <= 0 ? 0 : degrees * Math.PI / 180 * i / n;
        look = ViewNav.Rotate(look, ViewNav.ZUp, yaw).Normalized();
        return new OrthoView(center - look * distance, look, ViewNav.ZUp, width);
    }

    /// <summary>View direction for a heading (yaw from +X, rad) and an elevation (deg, positive = looking down from above).</summary>
    public static Vec3 Look(double headingRad, double lookDownDeg)
    {
        double p = lookDownDeg * Math.PI / 180;
        return new Vec3(Math.Cos(headingRad) * Math.Cos(p), Math.Sin(headingRad) * Math.Cos(p), -Math.Sin(p));
    }

    /// <summary>Orthographic width that keeps a sphere of <paramref name="radius"/> inside a viewport of the given aspect (w / h) in any orientation.</summary>
    public static double WidthForSphere(double radius, double aspect, double margin = 1.06)
    {
        double d = 2 * radius * margin;
        return aspect >= 1 ? d * aspect : d;   // wider than tall: vertical extent = width / aspect = d
    }
}
