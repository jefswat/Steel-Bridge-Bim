using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Steel;

/// <summary>
/// Angle shape (AISC naming). LegA is the connected (long) leg, LegB the outstanding leg. Meters. Kdes, area and centroid
/// from the AISC Shapes Database v16.0; workable gages from AISC Manual Table 1-7A (by leg length).
/// </summary>
public sealed record AngleSize(string Name, double LegA, double LegB, double Thickness)
{
    /// <summary>AISC kdes: heel to the toe of the root fillet (0 = sharp corner).</summary>
    public double Kdes { get; init; }
    public double AiscArea { get; init; }
    /// <summary>AISC centroid from the heel along the long leg (y) / along the short leg (x); 0 = computed.</summary>
    public double AiscY { get; init; }
    public double AiscX { get; init; }
    public double WeightPerFootLb { get; init; }
    /// <summary>Root fillet radius between the legs = kdes - t.</summary>
    public double RootRadius => Kdes > Thickness ? Kdes - Thickness : 0;
    public double Area => AiscArea > 0 ? AiscArea : Thickness * (LegA + LegB - Thickness);

    /// <summary>Distance from the heel (back of the outstanding leg) to the centroid, measured along the connected leg.</summary>
    public double CentroidFromHeel => AiscY > 0 ? AiscY :
        (LegA * Thickness * LegA / 2 + (LegB - Thickness) * Thickness * Thickness / 2) / (Thickness * (LegA + LegB - Thickness));

    public double WeightPerLength => Area * SteelCatalog.Density;
    /// <summary>Workable gages (g, g1, g2) from the heel in the connected / outstanding leg (AISC Manual Table 1-7A; g1/g2 = 0 where none).</summary>
    public (double G, double G1, double G2) GageA => SteelCatalog.AngleGage(LegA);
    public (double G, double G1, double G2) GageB => SteelCatalog.AngleGage(LegB);
}

/// <summary>
/// American Standard (C) / miscellaneous (MC) channel. Meters. tf is the AISC average flange thickness; C flanges have the
/// standard 16-2/3% inner slope (thickness tf at the middle of the flange's free width), MC flanges are modeled parallel
/// (their slope is not in the AISC database). Root fillets from kdes; WGi = AISC workable gage (from the web back).
/// </summary>
public sealed record ChannelSize(string Name, double Depth, double FlangeWidth, double WebThickness, double FlangeThickness)
{
    public double Kdes { get; init; }
    public double FlangeSlope { get; init; }
    public double WGi { get; init; }
    public double AiscArea { get; init; }
    /// <summary>Centroid distance from the back of the web (AISC x).</summary>
    public double AiscX { get; init; }
    public double WeightPerFootLb { get; init; }
}

/// <summary>Rolled wide-flange shape (AISC W). Meters. kdes / kdet / k1 and workable gages WGi / WGo from the AISC database.</summary>
public sealed record WideFlangeSize(string Name, double Depth, double FlangeWidth, double WebThickness, double FlangeThickness)
{
    public double Kdes { get; init; }
    public double Kdet { get; init; }
    public double K1 { get; init; }
    public double WGi { get; init; }
    public double WGo { get; init; }
    public double AiscArea { get; init; }
    public double WeightPerFootLb { get; init; }
    /// <summary>Flange-to-web fillet radius = kdes - tf.</summary>
    public double RootRadius => Math.Max(0, Kdes - FlangeThickness);
}

/// <summary>Structural tee (AISC WT, cut from a W): depth, flange width, stem thickness, flange thickness. Meters.</summary>
public sealed record TeeSize(string Name, double Depth, double FlangeWidth, double StemThickness, double FlangeThickness)
{
    public double Kdes { get; init; }
    public double Kdet { get; init; }
    public double WGi { get; init; }
    public double WGo { get; init; }
    public double AiscArea { get; init; }
    /// <summary>Centroid from the outer face of the flange (AISC y).</summary>
    public double AiscY { get; init; }
    public double WeightPerFootLb { get; init; }
    public double RootRadius => Math.Max(0, Kdes - FlangeThickness);
}

/// <summary>Structural bolt dimensions per ASTM F3125 (A325/A490) heavy hex, nut A563, washer F436.</summary>
public sealed record BoltGeometry(double Diameter)
{
    public double HoleDiameter => Diameter + In(1.0 / 16);
    public double HeadAcrossFlats => 1.5 * Diameter + In(1.0 / 8);
    public double HeadHeight => 0.625 * Diameter;
    public double NutAcrossFlats => 1.5 * Diameter + In(1.0 / 8);
    public double NutHeight => Diameter - In(1.0 / 64);
    public double WasherOd => 2 * Diameter + In(0.25);
    public double WasherThickness => In(5.0 / 32);
    /// <summary>Minimum stick-through beyond the nut.</summary>
    public double StickThrough => In(0.25);

    /// <summary>Bolt length for a grip: grip + washer + nut + stick-through, rounded up to the next 1/4".</summary>
    public double LengthForGrip(double grip)
    {
        double raw = grip + WasherThickness + NutHeight + StickThrough;
        double q = In(0.25);
        return Math.Ceiling(raw / q - 1e-9) * q;
    }
}

public static class SteelCatalog
{
    /// <summary>Steel density, kg/m³ (490 lb/ft³).</summary>
    public const double Density = 7849.0;

    private sealed record Row(string Type, string Label, Dictionary<string, double> V)
    {
        public double this[string k] => V.TryGetValue(k, out var x) ? x : 0;
    }

    /// <summary>W, WT, C, MC and L shapes of the AISC Shapes Database v16.0 (embedded AiscShapes.csv).</summary>
    private static readonly Lazy<List<Row>> Rows = new(() =>
    {
        var list = new List<Row>();
        using var st = typeof(SteelCatalog).Assembly.GetManifestResourceStream("SBM.Core.Steel.AiscShapes.csv")
            ?? throw new InvalidOperationException("AiscShapes.csv resource missing.");
        using var rd = new StreamReader(st);
        string[]? hdr = null;
        string? line;
        while ((line = rd.ReadLine()) != null)
        {
            if (line.StartsWith('#') || line.Length == 0) continue;
            var f = line.Split(',');
            if (hdr == null) { hdr = f; continue; }
            var v = new Dictionary<string, double>();
            for (int k = 2; k < f.Length && k < hdr.Length; k++)
                if (double.TryParse(f[k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x)) v[hdr[k]] = x;
            list.Add(new Row(f[0], f[1], v));
        }
        return list;
    });

    /// <summary>AISC label in this program's style (L6X6X9/16 -> L6x6x9/16).</summary>
    private static string Nice(string label) => label.Replace('X', 'x');

    private static double I(double inch) => In(inch);

    public static IReadOnlyList<AngleSize> Angles => _angles.Value;
    private static readonly Lazy<List<AngleSize>> _angles = new(() => Rows.Value.Where(r => r.Type == "L").Select(r =>
    {
        // AISC: b = long leg, d = short leg; y = centroid along the long leg, x = along the short leg.
        double b = r["b"], d = r["d"];
        return new AngleSize(Nice(r.Label), I(Math.Max(b, d)), I(Math.Min(b, d)), I(r["t"]))
        {
            Kdes = I(r["kdes"]), AiscArea = r["A"] * 0.00064516, AiscY = I(r["y"]), AiscX = I(r["x"]), WeightPerFootLb = r["W"],
        };
    }).ToList());

    public static IReadOnlyList<ChannelSize> Channels => _channels.Value;
    private static readonly Lazy<List<ChannelSize>> _channels = new(() => Rows.Value.Where(r => r.Type is "C" or "MC").Select(r =>
        new ChannelSize(Nice(r.Label), I(r["d"]), I(r["bf"]), I(r["tw"]), I(r["tf"]))
        {
            Kdes = I(r["kdes"]), FlangeSlope = r.Type == "C" ? 1.0 / 6 : 0, WGi = I(r["WGi"]), AiscArea = r["A"] * 0.00064516,
            AiscX = I(r["x"]), WeightPerFootLb = r["W"],
        }).ToList());

    public static IReadOnlyList<WideFlangeSize> WideFlanges => _w.Value;
    private static readonly Lazy<List<WideFlangeSize>> _w = new(() => Rows.Value.Where(r => r.Type == "W").Select(r =>
        new WideFlangeSize(Nice(r.Label), I(r["d"]), I(r["bf"]), I(r["tw"]), I(r["tf"]))
        {
            Kdes = I(r["kdes"]), Kdet = I(r["kdet"]), K1 = I(r["k1"]), WGi = I(r["WGi"]), WGo = I(r["WGo"]),
            AiscArea = r["A"] * 0.00064516, WeightPerFootLb = r["W"],
        }).ToList());

    public static IReadOnlyList<TeeSize> Tees => _wt.Value;
    private static readonly Lazy<List<TeeSize>> _wt = new(() => Rows.Value.Where(r => r.Type == "WT").Select(r =>
        new TeeSize(Nice(r.Label), I(r["d"]), I(r["bf"]), I(r["tw"]), I(r["tf"]))
        {
            Kdes = I(r["kdes"]), Kdet = I(r["kdet"]), WGi = I(r["WGi"]), WGo = I(r["WGo"]), AiscArea = r["A"] * 0.00064516,
            AiscY = I(r["y"]), WeightPerFootLb = r["W"],
        }).ToList());

    /// <summary>Workable gages in angle legs (g, g1, g2), AISC Manual Table 1-7A; not part of the shapes database.</summary>
    public static (double G, double G1, double G2) AngleGage(double leg)
    {
        double inch = leg / 0.0254;
        (double l, double g, double g1, double g2)[] t =
        {
            (8, 4.5, 3, 3), (7, 4, 2.5, 3), (6, 3.5, 2.25, 2.5), (5, 3, 2, 1.75), (4, 2.5, 0, 0), (3.5, 2, 0, 0), (3, 1.75, 0, 0),
            (2.5, 1.375, 0, 0), (2, 1.125, 0, 0), (1.75, 1, 0, 0), (1.5, 0.875, 0, 0), (1.375, 0.875, 0, 0), (1.25, 0.75, 0, 0), (1, 0.625, 0, 0),
        };
        var row = t.FirstOrDefault(x => Math.Abs(x.l - inch) < 0.01);
        return row.l == 0 ? (0, 0, 0) : (I(row.g), I(row.g1), I(row.g2));
    }

    private static T? Find<T>(IEnumerable<T> list, Func<T, string> name, string n) where T : class =>
        list.FirstOrDefault(a => name(a).Equals(n.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Exact catalog lookups (null when the name is not in the catalog — callers report it).</summary>
    public static AngleSize? FindAngle(string name) => Find(Angles, a => a.Name, name);
    public static ChannelSize? FindChannel(string name) => Find(Channels, a => a.Name, name);
    public static TeeSize? FindTee(string name) => Find(Tees, a => a.Name, name);
    public static WideFlangeSize? FindWideFlange(string name) => Find(WideFlanges, a => a.Name, name);

    public static AngleSize Angle(string name) => FindAngle(name) ?? FindAngle("L4x4x3/8")!;
    public static ChannelSize Channel(string name) => FindChannel(name) ?? FindChannel("C15x33.9")!;

    // ------------------------------------------------------------------ section outlines (with AISC fillets)

    /// <summary>
    /// Replaces polygon vertices by circular arcs tangent to both adjacent edges (radius per vertex index; tangent distance
    /// clamped to the shorter edge). Segments per 90 degrees: 6.
    /// </summary>
    public static Vec2[] RoundCorners(IReadOnlyList<Vec2> poly, IReadOnlyDictionary<int, double> radii)
    {
        var res = new List<Vec2>();
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            if (!radii.TryGetValue(i, out double r) || r <= 0) { res.Add(poly[i]); continue; }
            Vec2 p = poly[i], a = poly[(i - 1 + n) % n], b = poly[(i + 1) % n];
            var u1 = (a - p).Normalized(); var u2 = (b - p).Normalized();
            double ang = Math.Acos(Math.Clamp(Vec2.Dot(u1, u2), -1, 1));
            if (ang < 1e-6 || Math.PI - ang < 1e-6) { res.Add(p); continue; }
            double tdist = Math.Min(r / Math.Tan(ang / 2), Math.Min((a - p).Length, (b - p).Length) * 0.999);
            double rr = tdist * Math.Tan(ang / 2);
            var t1 = p + u1 * tdist; var t2 = p + u2 * tdist;
            var c = p + (u1 + u2).Normalized() * (rr / Math.Sin(ang / 2));
            double a0 = Math.Atan2(t1.Y - c.Y, t1.X - c.X), a1 = Math.Atan2(t2.Y - c.Y, t2.X - c.X);
            double sw = a1 - a0;
            while (sw > Math.PI) sw -= 2 * Math.PI;
            while (sw < -Math.PI) sw += 2 * Math.PI;
            int k = Math.Max(2, (int)Math.Ceiling(Math.Abs(sw) / (Math.PI / 12)));
            for (int j = 0; j <= k; j++) res.Add(c + Vec2.FromAngle(a0 + sw * j / k) * rr);
        }
        return res.ToArray();
    }

    /// <summary>
    /// Angle section polygon in (m, v): heel at m = heelM; connected leg runs toward +s·m; outstanding leg toward +o·v from
    /// vBase. Root fillet r = kdes − t (AISC); toes square (the toe radius is not published).
    /// </summary>
    public static Vec2[] AnglePolygon(AngleSize a, double heelM, int s, double vBase, int o) => RoundCorners(new[]
    {
        new Vec2(heelM, vBase),
        new Vec2(heelM + s * a.LegA, vBase),
        new Vec2(heelM + s * a.LegA, vBase + o * a.Thickness),
        new Vec2(heelM + s * a.Thickness, vBase + o * a.Thickness),
        new Vec2(heelM + s * a.Thickness, vBase + o * a.LegB),
        new Vec2(heelM, vBase + o * a.LegB),
    }, new Dictionary<int, double> { [3] = a.RootRadius });

    /// <summary>
    /// Channel section in (m, v): web on plane v = vBase spanning m ∈ [−d/2, d/2], flanges toward +o·v. C flanges sloped
    /// 16-2/3% (tf at mid free width), root fillets so the web toe of fillet is at kdes from the flange's outer face.
    /// </summary>
    public static Vec2[] ChannelPolygon(ChannelSize c, double vBase, int o)
    {
        double h = c.Depth / 2, bf = c.FlangeWidth, tw = c.WebThickness, sl = c.FlangeSlope;
        double uMid = (bf + tw) / 2;
        double T(double u) => c.FlangeThickness + sl * (uMid - u);          // flange thickness at distance u from the web back
        double tRoot = T(tw), tTip = T(bf);
        // Interior angle between the web face and the sloped flange face; radius so the web tangent point is at kdes.
        double theta = Math.PI / 2 + Math.Atan(sl);
        double r = c.Kdes > tRoot ? (c.Kdes - tRoot) * Math.Tan(theta / 2) : 0;
        var poly = new[]
        {
            new Vec2(-h, vBase), new Vec2(h, vBase), new Vec2(h, vBase + o * bf),
            new Vec2(h - tTip, vBase + o * bf), new Vec2(h - tRoot, vBase + o * tw),
            new Vec2(-h + tRoot, vBase + o * tw), new Vec2(-h + tTip, vBase + o * bf),
            new Vec2(-h, vBase + o * bf),
        };
        return RoundCorners(poly, new Dictionary<int, double> { [4] = r, [5] = r });
    }

    /// <summary>Wide-flange section in (m, v): depth along m centred on 0, web from v = vWeb to vWeb + tw, flanges centred on the web; root fillets kdes − tf.</summary>
    public static Vec2[] WideFlangePolygon(WideFlangeSize w, double vWeb)
    {
        double h = w.Depth / 2, tf = w.FlangeThickness, vc = vWeb + w.WebThickness / 2, hb = w.FlangeWidth / 2;
        var poly = new[]
        {
            new Vec2(-h, vc - hb), new Vec2(-h, vc + hb), new Vec2(-h + tf, vc + hb),
            new Vec2(-h + tf, vWeb + w.WebThickness), new Vec2(h - tf, vWeb + w.WebThickness), new Vec2(h - tf, vc + hb),
            new Vec2(h, vc + hb), new Vec2(h, vc - hb), new Vec2(h - tf, vc - hb),
            new Vec2(h - tf, vWeb), new Vec2(-h + tf, vWeb), new Vec2(-h + tf, vc - hb),
        };
        double r = w.RootRadius;
        return RoundCorners(poly, new Dictionary<int, double> { [3] = r, [4] = r, [9] = r, [10] = r });
    }

    /// <summary>Tee section in (m, v): flange at m = −d/2 (outer face), stem toward +m from v = vStem to vStem + tw; root fillets kdes − tf.</summary>
    public static Vec2[] TeePolygon(TeeSize t, double vStem)
    {
        double h = t.Depth / 2, tf = t.FlangeThickness, vc = vStem + t.StemThickness / 2, hb = t.FlangeWidth / 2;
        var poly = new[]
        {
            new Vec2(-h, vc - hb), new Vec2(-h, vc + hb), new Vec2(-h + tf, vc + hb),
            new Vec2(-h + tf, vStem + t.StemThickness), new Vec2(h, vStem + t.StemThickness), new Vec2(h, vStem),
            new Vec2(-h + tf, vStem), new Vec2(-h + tf, vc - hb),
        };
        double r = t.RootRadius;
        return RoundCorners(poly, new Dictionary<int, double> { [3] = r, [6] = r });
    }
}
