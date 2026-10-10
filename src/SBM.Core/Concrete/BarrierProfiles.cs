using SBM.Core.Bridge;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>
/// Barrier cross sections in (r, h): r = distance inward from the deck edge (back face), h = height above the slab.
/// Polygons are counter-clockwise with the base on h = 0.
/// </summary>
public static class BarrierProfiles
{
    /// <summary>
    /// TxDOT Type SSTR (RL-SSTR, Sept 2019), "on bridge slab": 3'-0" high, 1'-2 1/2" base, 7 1/2" top, traffic face
    /// sloped 7" over 36" (vertical back face). The upper 1'-0" of the back face stands 3/4" proud of the lower part
    /// (shown on the standard; offset assumed 3/4"), 1" chamfers at the top corners.
    /// </summary>
    public static Vec2[] TxDotSstr()
    {
        double c = In(1), k = In(0.75);
        return new[]
        {
            new Vec2(k, 0), new Vec2(In(14.5), 0), new Vec2(In(7.5) + c * 7 / 36, In(36) - c), new Vec2(In(7.5) - c * 0.2, In(36)),
            new Vec2(c, In(36)), new Vec2(0, In(36) - c), new Vec2(0, In(24) + k), new Vec2(k, In(24)),
        };
    }

    public static Vec2[] FShape(BarrierDefinition b)
    {
        double h = b.Height, bw = b.BaseWidth, tw = b.TopWidth;
        return new[]
        {
            new Vec2(0, 0), new Vec2(bw, 0), new Vec2(bw, In(3)), new Vec2(bw - In(2.5), In(13)), new Vec2(tw, h), new Vec2(0, h),
        };
    }

    public static Vec2[] SingleSlope(BarrierDefinition b) =>
        new[] { new Vec2(0, 0), new Vec2(b.BaseWidth, 0), new Vec2(b.TopWidth, b.Height), new Vec2(0, b.Height) };

    public static Vec2[]? For(BarrierDefinition b) => b.Type switch
    {
        BarrierType.TxDotSstr => TxDotSstr(),
        BarrierType.FShape => FShape(b),
        BarrierType.SingleSlope => SingleSlope(b),
        _ => null,
    };

    /// <summary>Toe (base) width and top-of-rail traffic face distance from the back face, for plan linework.</summary>
    public static (double toe, double top, double height) PlanLines(BarrierDefinition b)
    {
        var p = For(b);
        if (p == null) return (0, 0, 0);
        double h = p.Max(q => q.Y);
        return (p.Where(q => q.Y < 1e-9).Max(q => q.X), p.Where(q => q.Y > h - In(1.01)).Max(q => q.X), h);
    }
}
