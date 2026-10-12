using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
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
        BarrierType.Custom => b.Profile is { Count: >= 3 } pr ? Expand(pr).Points : null,
        _ => null,
    };

    /// <summary>A custom section with its fillets / chamfers expanded: points (CCW) and the surface tag of every edge i -> i+1 ("" = none).</summary>
    public sealed record Expanded(Vec2[] Points, string[] EdgeTags);

    /// <summary>Expands <see cref="BarrierProfilePoint"/>s: a vertex with a Radius becomes a tangent arc (sampled at 7.5 deg or finer), a Chamfer two
    /// points cut back along both edges. The tag of a vertex goes to the edge that leaves its last generated point; arc / chamfer edges are untagged.</summary>
    public static Expanded Expand(IReadOnlyList<BarrierProfilePoint> pts)
    {
        int n = pts.Count;
        var outPts = new List<Vec2>();
        var tags = new List<string>();
        for (int i = 0; i < n; i++)
        {
            var p = pts[i];
            var v = new Vec2(p.R, p.H);
            var a = new Vec2(pts[(i + n - 1) % n].R, pts[(i + n - 1) % n].H);
            var c = new Vec2(pts[(i + 1) % n].R, pts[(i + 1) % n].H);
            var u1 = (a - v).Normalized(); var u2 = (c - v).Normalized();
            var gen = new List<Vec2>();
            if (p.Radius > 0)
            {
                double th = Math.Acos(Math.Clamp(Vec2.Dot(u1, u2), -1, 1));
                if (th < 1e-6 || Math.PI - th < 1e-6) gen.Add(v);
                else
                {
                    double t = p.Radius / Math.Tan(th / 2);
                    if (t > (a - v).Length || t > (c - v).Length) throw new ArgumentException($"barrier fillet R {p.Radius:0.####} at ({p.R:0.####}, {p.H:0.####}) does not fit between its neighbours");
                    var t1 = v + u1 * t; var t2 = v + u2 * t;
                    var bis = (u1 + u2).Normalized();
                    var ctr = v + bis * (p.Radius / Math.Sin(th / 2));
                    double a1 = Math.Atan2(t1.Y - ctr.Y, t1.X - ctr.X), a2 = Math.Atan2(t2.Y - ctr.Y, t2.X - ctr.X);
                    double sweep = a2 - a1;
                    while (sweep > Math.PI) sweep -= 2 * Math.PI;
                    while (sweep < -Math.PI) sweep += 2 * Math.PI;
                    int segs = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (7.5 * Math.PI / 180)));
                    for (int k = 0; k <= segs; k++) { double ang = a1 + sweep * k / segs; gen.Add(ctr + new Vec2(Math.Cos(ang), Math.Sin(ang)) * p.Radius); }
                }
            }
            else if (p.Chamfer > 0) { gen.Add(v + u1 * p.Chamfer); gen.Add(v + u2 * p.Chamfer); }
            else gen.Add(v);
            for (int k = 0; k < gen.Count; k++)
            {
                outPts.Add(gen[k]);
                tags.Add(k == gen.Count - 1 ? p.Tag ?? "" : "");
            }
        }
        var arr = outPts.ToArray();
        if (MeshBuilder.SignedArea(arr) <= 0) throw new ArgumentException("custom barrier section must be counter-clockwise in (R, H)");
        return new Expanded(arr, tags.ToArray());
    }

    /// <summary>Toe (base) width and top-of-rail traffic face distance from the back face, for plan linework.</summary>
    public static (double toe, double top, double height) PlanLines(BarrierDefinition b)
    {
        var p = For(b);
        if (p == null) return (0, 0, 0);
        double h = p.Max(q => q.Y);
        return (p.Where(q => q.Y < 1e-9).Max(q => q.X), p.Where(q => q.Y > h - In(1.01)).Max(q => q.X), h);
    }
}
