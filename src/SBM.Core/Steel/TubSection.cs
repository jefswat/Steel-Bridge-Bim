using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Cross-section math of a steel tub girder (pure functions, shared by the steel generator, the concrete haunches and tests).
/// Section coordinates (x, y) are relative to the TOP WORKING POINT (top of web on the tub CL), x to the right, y up, in the
/// section ROTATED with the deck (plans: "girders are rotated about the top working point with the plane of the deck").
/// y = 0 is the top-of-web line (underside of the top flanges), y = -D the top of the bottom flange.
/// </summary>
public static class TubSection
{
    /// <summary>Web centerline x at height y (y â‰¤ 0) of web side Â±1.</summary>
    public static double WebX(TubGirderDefinition t, int side, double y) => side * (t.TopWebSpacing / 2 + t.WebSlope * y);

    /// <summary>Web length along the slope for a vertical web depth D.</summary>
    public static double WebSlopeLength(TubGirderDefinition t, double depth) => depth * Math.Sqrt(1 + t.WebSlope * t.WebSlope);

    /// <summary>Horizontal width of a web of thickness tw (thickness measured normal to the web).</summary>
    public static double HorizontalThickness(TubGirderDefinition t, double tw) => tw * Math.Sqrt(1 + t.WebSlope * t.WebSlope);

    /// <summary>Unit vector down along web side Â±1.</summary>
    public static Vec2 WebDown(TubGirderDefinition t, int side) => new Vec2(-side * t.WebSlope, -1).Normalized();

    /// <summary>Unit normal of web side Â±1 pointing OUT of the tub.</summary>
    public static Vec2 WebOut(TubGirderDefinition t, int side) => new Vec2(side, -t.WebSlope).Normalized();

    /// <summary>Point of web side Â±1 at slope distance w below its top working point and normal offset n (+ = outside).</summary>
    public static Vec2 WebPoint(TubGirderDefinition t, int side, double w, double n) =>
        new Vec2(side * t.TopWebSpacing / 2, 0) + WebDown(t, side) * w + WebOut(t, side) * n;

    /// <summary>x of the web face (inner = toward the tub CL, outer) at height y.</summary>
    public static double WebFaceX(TubGirderDefinition t, int side, double y, double tw, bool outer) =>
        WebX(t, side, y) + side * (outer ? 1 : -1) * HorizontalThickness(t, tw) / 2;

    /// <summary>Steel area of the section: 2 top flanges (each width x thickness), bottom flange, 2 webs (slope length x thickness).</summary>
    public static double Area(TubGirderDefinition t, double depth, double topWidth, double topThickness, double webThickness, double bottomWidth, double bottomThickness) =>
        2 * topWidth * topThickness + bottomWidth * bottomThickness + 2 * WebSlopeLength(t, depth) * webThickness;

    /// <summary>Cross slope of the finished deck at a plan point (rise per foot to the RIGHT of the alignment), the plane the tub is rotated with.</summary>
    public static double DeckSlope(BridgeFraming f, Vec2 p)
    {
        var a = f.Alignment;
        var so = a.Inverse(p);
        double e0 = f.Definition.Deck.FinishedElevation(a, so.Station, so.Offset - 0.05), e1 = f.Definition.Deck.FinishedElevation(a, so.Station, so.Offset + 0.05);
        return (e1 - e0) / 0.1;
    }
}

/// <summary>Section frame of a tub at one girder distance: origin = top working point, rotated lateral / up axes, horizontal tangent.</summary>
public readonly record struct TubFrame(Vec3 Origin, Vec3 Lateral, Vec3 Up, Vec3 Ahead, Vec2 PlanRight, Vec2 PlanAhead, double Cos, double Sin)
{
    public static TubFrame Create(Vec3 wp, Vec2 tangent, double slope)
    {
        double th = Math.Atan(slope), c = Math.Cos(th), s = Math.Sin(th);
        var r = tangent.Right;
        return new TubFrame(wp, new Vec3(r.X * c, r.Y * c, s), new Vec3(-r.X * s, -r.Y * s, c), new Vec3(tangent.X, tangent.Y, 0), r, tangent, c, s);
    }

    /// <summary>World point of section point (x, y), z along the (horizontal) girder tangent.</summary>
    public Vec3 At(double x, double y, double z = 0) => Origin + Lateral * x + Up * y + Ahead * z;
    public Vec3 At(Vec2 q, double z = 0) => At(q.X, q.Y, z);

    /// <summary>Section point (x, y) moved along the girder into the vertical plane through the working point with plan direction
    /// <paramref name="dir"/> (a skewed support line).</summary>
    public Vec3 AtInPlane(double x, double y, Vec2 dir)
    {
        double h = x * Cos - y * Sin;
        double dr = Vec2.Dot(dir, PlanRight);
        double k = Math.Abs(dr) < 1e-9 ? 0 : Vec2.Dot(dir, PlanAhead) / dr;
        return At(x, y, h * k);
    }

    /// <summary>Right-handed plate frame in the section plane: X = lateral, Y = up, Z = back (âˆ’ahead).</summary>
    public Frame3 Plane => new(Origin, Lateral, Up, Ahead * -1);
}
