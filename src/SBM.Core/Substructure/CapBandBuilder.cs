using SBM.Core.Bridge;
using SBM.Core.Geometry;

namespace SBM.Core.Substructure;

/// <summary>
/// Builds the tangent-arc band boundaries of the Iowa DOT "pier cap aesthetic treatment" sheets in the canonical
/// orientation (hill / long side at the left, fan of three nested arcs R3 &gt; R4 &gt; R5 at the right) and mirrors them when the
/// fan is at the left end. Coordinates: X along the cap top line from the left top corner, D depth below it (perpendicular).
/// All inputs/outputs are meters.
/// </summary>
public static class CapBandBuilder
{
    private const double Ext = 6 * 0.3048; // chains run this far past the top corners; the generator clips them to the cap

    /// <summary>
    /// The parametric figure (piers 1WB-15WB, 16G, 17G): hill arc R1 -> point of reverse curvature P (L-hill from the end,
    /// depth <paramref name="depthAtReverse"/>) -> sag arc R2 -> point of reverse curvature Q (L-fan from the other end) -> three
    /// hill arcs R3, R4, R5 tangent at Q. <paramref name="fanAtLeft"/> mirrors it (16G, 17G: L2 is the fan side there).
    /// </summary>
    public static CapAestheticsDefinition FromTable(double l1, double lHill, double lFan, double r1, double r2, double r3, double r4, double r5,
        bool fanAtLeft, double depthAtReverse = 3.5 * 0.3048)
    {
        double c = l1 - lFan - lHill;
        if (c <= 0 || c >= 2 * r2) throw new ArgumentException("reverse points do not fit the R2 arc");
        double alpha = Math.Asin(c / (2 * r2));
        var p = new Vec2(lHill, depthAtReverse);
        var q = new Vec2(l1 - lFan, depthAtReverse);
        var nP = new Vec2(-Math.Sin(alpha), Math.Cos(alpha));
        var c1 = p + nP * r1;
        double xs = -Ext;
        var start = new BandNode(xs, c1.Y - Math.Sqrt(r1 * r1 - (xs - c1.X) * (xs - c1.X)));
        var nQ = new Vec2(Math.Sin(alpha), Math.Cos(alpha));
        BandChain Chain(double rk)
        {
            var ck = q + nQ * rk;
            double xe = Math.Min(l1 + Ext, ck.X + 0.95 * rk);
            var ch = new BandChain();
            ch.Nodes.Add(new BandNode(start.X, start.D));
            ch.Nodes.Add(new BandNode(p.X, p.Y, r1));
            ch.Nodes.Add(new BandNode(q.X, q.Y, -r2));
            ch.Nodes.Add(new BandNode(xe, ck.Y - Math.Sqrt(rk * rk - (xe - ck.X) * (xe - ck.X)), rk));
            return ch;
        }
        return Finish(new List<BandChain> { Chain(r5), Chain(r4), Chain(r3) }, l1, fanAtLeft,
            $"Table figure: L1 {l1 / 0.3048:F2} ft, hill side {lHill / 0.3048:F2} ft, fan side {lFan / 0.3048:F2} ft, R1 {r1 / 0.3048:F1}, R2 {r2 / 0.3048:F1}, R3-R5 {r3 / 0.3048:F1}/{r4 / 0.3048:F1}/{r5 / 0.3048:F1} ft");
    }

    /// <summary>
    /// The explicit figures (Pier 6A, 19G, generic Ramp G 1G-15G / 18G): the long arc <paramref name="rLong"/> (sag) runs from the
    /// tip of the long side (<paramref name="tipX"/> beyond the top corner, <paramref name="tipDepth"/> below the top) to the point of
    /// reverse curvature Q (<paramref name="xq"/> from that corner, <paramref name="dq"/> deep); the three fan arcs R3 &gt; R4 &gt; R5
    /// continue tangent from Q (reverse curvature) toward the other end. Everything is measured from the long-side corner;
    /// <paramref name="fanAtLeft"/> mirrors it.
    /// </summary>
    public static CapAestheticsDefinition FromLongArc(double l1, double xq, double dq, double tipX, double tipDepth, double rLong,
        double r3, double r4, double r5, bool fanAtLeft)
    {
        var t0 = new Vec2(-tipX, tipDepth);
        var q = new Vec2(xq, dq);
        var v = q - t0;
        double chord = v.Length;
        if (chord >= 2 * rLong) throw new ArgumentException("tip and reverse point do not fit the long arc");
        var n = new Vec2(-v.Y, v.X) / chord;
        var cu = (t0 + q) * 0.5 - n * Math.Sqrt(rLong * rLong - chord * chord / 4); // sag: centre above
        var r0 = t0 - cu;
        var tang0 = new Vec2(r0.Y, -r0.X) / r0.Length;                              // heading at the tip (moving right)
        var lead = t0 - tang0 * (0.9144 / Math.Max(0.1, Math.Abs(tang0.X)));
        var dirOut = (q - cu) / (q - cu).Length;
        BandChain Chain(double rk)
        {
            var ck = q + dirOut * rk;
            double xe = Math.Min(l1 + Ext, ck.X + 0.95 * rk);
            var ch = new BandChain();
            ch.Nodes.Add(new BandNode(lead.X, lead.Y));
            ch.Nodes.Add(new BandNode(t0.X, t0.Y));
            ch.Nodes.Add(new BandNode(q.X, q.Y, -rLong));
            ch.Nodes.Add(new BandNode(xe, ck.Y - Math.Sqrt(rk * rk - (xe - ck.X) * (xe - ck.X)), rk));
            return ch;
        }
        return Finish(new List<BandChain> { Chain(r5), Chain(r4), Chain(r3) }, l1, fanAtLeft,
            $"Explicit figure: long arc R {rLong / 0.3048:F1} ft from the tip to Q ({xq / 0.3048:F2} ft, depth {dq / 0.3048:F3} ft), fan R {r3 / 0.3048:F1}/{r4 / 0.3048:F1}/{r5 / 0.3048:F1} ft tangent at Q");
    }

    private static CapAestheticsDefinition Finish(List<BandChain> layers, double l1, bool mirror, string source)
    {
        if (mirror) layers = layers.Select(l => Mirror(l, l1)).ToList();
        return new CapAestheticsDefinition { Layers = layers, Source = source };
    }

    /// <summary>The chain seen from the other end (X -> L1 - X); each arc keeps its circle.</summary>
    public static BandChain Mirror(BandChain c, double l1)
    {
        var m = new BandChain();
        int k = c.Nodes.Count;
        for (int j = 0; j < k; j++)
        {
            var node = c.Nodes[k - 1 - j];
            // the segment ending at the new node j is the old segment that started there: the old node (k - j) ended it.
            double r = j == 0 ? 0 : c.Nodes[k - j].Radius;
            m.Nodes.Add(new BandNode(l1 - node.X, node.D, r));
        }
        return m;
    }

    /// <summary>Depth of a chain at X (linear between samples); NaN outside its extent.</summary>
    public static double DepthAt(BandChain c, double x)
    {
        var pts = c.Sample(0.0002);
        for (int i = 1; i < pts.Count; i++)
            if ((pts[i - 1].X - x) * (pts[i].X - x) <= 0 && Math.Abs(pts[i].X - pts[i - 1].X) > 1e-12)
                return pts[i - 1].Y + (pts[i].Y - pts[i - 1].Y) * (x - pts[i - 1].X) / (pts[i].X - pts[i - 1].X);
        return double.NaN;
    }
}
