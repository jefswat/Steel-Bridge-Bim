using SBM.Core.Bridge;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Prestressed;

/// <summary>
/// Precast I-beam outline in PGSuper / WBFL "Precast I-Beam" terms (meters). From the web outward:
/// top: fillet W3 × D3, taper W2 × D2, flat lip W1, edge D1; bottom: fillet W4 × D4, taper W5 × D5, edge D6, chamfer C1.
/// Top width = T1 + 2(W1 + W2 + W3); bottom width = T2 + 2(W4 + W5).
/// </summary>
public sealed class PrecastIBeamDims
{
    public string Name { get; set; } = "";
    public double D1 { get; set; }
    public double D2 { get; set; }
    public double D3 { get; set; }
    public double D4 { get; set; }
    public double D5 { get; set; }
    public double D6 { get; set; }
    public double H { get; set; }
    public double T1 { get; set; }
    public double T2 { get; set; }
    public double W1 { get; set; }
    public double W2 { get; set; }
    public double W3 { get; set; }
    public double W4 { get; set; }
    public double W5 { get; set; }
    public double C1 { get; set; }

    public PrecastIBeamDims Clone() => (PrecastIBeamDims)MemberwiseClone();

    public static PrecastIBeamDims Inches(string name, double d1, double d2, double d3, double d4, double d5, double d6, double h,
        double t1, double t2, double w1, double w2, double w3, double w4, double w5, double c1) => new()
    {
        Name = name, D1 = In(d1), D2 = In(d2), D3 = In(d3), D4 = In(d4), D5 = In(d5), D6 = In(d6), H = In(h),
        T1 = In(t1), T2 = In(t2), W1 = In(w1), W2 = In(w2), W3 = In(w3), W4 = In(w4), W5 = In(w5), C1 = In(c1),
    };

    public List<string> Validate()
    {
        var e = new List<string>();
        if (H <= 0 || T1 <= 0 || T2 <= 0) e.Add($"Girder section {Name}: H, T1 and T2 must be positive.");
        if (D1 + D2 + D3 + D4 + D5 + D6 >= H) e.Add($"Girder section {Name}: H must exceed D1 + … + D6.");
        if (C1 < 0 || C1 > D6 || C1 > W5 + W4) e.Add($"Girder section {Name}: bottom chamfer C1 is too large.");
        return e;
    }
}

/// <summary>
/// A precast I-girder shape: the TxDOT Tx28–Tx70 (standard IGD, Aug 2017 rev. 3-23 — the polygon reproduces the
/// tabulated areas within 0.4 in² and Yb within 0.05", checked by tests) or any PGSuper Precast I-Beam (custom).
/// </summary>
public sealed class TxGirderShape
{
    public required TxGirderType Type { get; init; }
    public required PrecastIBeamDims Dims { get; init; }
    // Section properties (in, in², in⁴, plf): tabulated for Tx girders (IGD), computed from the outline otherwise.
    public double Yt { get; init; }
    public double Yb { get; init; }
    public double Area { get; init; }
    public double Ix { get; init; }
    public double Iy { get; init; }
    public double WeightPlf { get; init; }

    // ---- state standard shapes (StandardBeamCatalog): exact outline with true radius fillets
    /// <summary>State of the standard ("TX", "IA", "MN"); "" for a PGSuper custom section.</summary>
    public string State { get; init; } = "";
    /// <summary>Family of the standard ("Tx", "BT" bulb tee, "MH" mesa).</summary>
    public string Family { get; init; } = "";
    /// <summary>Exact outline (lines + arcs, meters). Null = the PGSuper / WBFL straight-taper polygon of <see cref="Dims"/>.</summary>
    public SectionProfile? Profile { get; init; }
    /// <summary>Where the shape is defined (plan sheet / standard).</summary>
    public string Source { get; init; } = "";
    /// <summary>Properties printed on the standard (in², in, in⁴); null = not printed. Computed values are in Area / Yb / Ix.</summary>
    public double? PrintedArea { get; init; }
    public double? PrintedYb { get; init; }
    public double? PrintedIx { get; init; }

    public bool IsTx => Type != TxGirderType.Custom;
    /// <summary>A state standard shape other than TxDOT (BTD, 30MH, ...): explicit strands only (no IGND grid).</summary>
    public bool IsStateStandard => !IsTx && State != "";
    /// <summary>Catalog key ("IA BTD", "MN 30MH", "TX Tx54").</summary>
    public string Key => State == "" ? Name : $"{State} {Name}";
    /// <summary>Text for notes and labels: "TxDOT Tx54 (IGD)", "Iowa BTD", "MnDOT 30MH", or the custom name.</summary>
    public string Designation => IsTx ? $"TxDOT {Name} (IGD)" : State switch { "IA" => $"Iowa {Name}", "MN" => $"MnDOT {Name}", "" => Name, _ => $"{State} {Name}" };
    public double Depth => Dims.H;
    public double DepthIn => Dims.H / In(1);
    public double TopWidth => Dims.T1 + 2 * (Dims.W1 + Dims.W2 + Dims.W3);
    public double TopWidthIn => TopWidth / In(1);
    public double BottomWidth => Dims.T2 + 2 * (Dims.W4 + Dims.W5);
    public double WebWidth => Math.Min(Dims.T1, Dims.T2);
    /// <summary>Straight web height ("B" on IGD).</summary>
    public double WebStraightIn => (Dims.H - Dims.D1 - Dims.D2 - Dims.D3 - Dims.D4 - Dims.D5 - Dims.D6) / In(1);
    /// <summary>Bottom flange vertical edge.</summary>
    public double BottomEdgeIn => Dims.D6 / In(1);
    public string Name => IsTx ? Type.ToString() : (string.IsNullOrWhiteSpace(Dims.Name) ? "Custom I-beam" : Dims.Name);

    private Vec2[]? _outline;

    /// <summary>Counter-clockwise outline in section coordinates (x right, y up from the bottom of the girder), meters.
    /// Shapes with an exact <see cref="Profile"/> return it with the arcs split into pieces of at most 7.5 degrees.</summary>
    public Vec2[] Outline()
    {
        if (Profile != null) return (Vec2[])(_outline ??= Profile.Tessellate()).Clone();
        var d = Dims;
        double hb = BottomWidth / 2, ht = TopWidth / 2, c = d.C1;
        double yWebBot = d.D6 + d.D5 + d.D4, yWebTop = d.H - d.D1 - d.D2 - d.D3;
        var right = new List<Vec2>();
        if (c > 1e-9) { right.Add(new(hb - c, 0)); right.Add(new(hb, c)); }
        else right.Add(new(hb, 0));
        right.Add(new(hb, d.D6));
        if (d.W5 > 1e-9 || d.D5 > 1e-9) right.Add(new(d.T2 / 2 + d.W4, d.D6 + d.D5));
        right.Add(new(d.T2 / 2, yWebBot));
        right.Add(new(d.T1 / 2, yWebTop));
        if (d.W3 > 1e-9 || d.D3 > 1e-9) right.Add(new(d.T1 / 2 + d.W3, d.H - d.D1 - d.D2));
        right.Add(new(d.T1 / 2 + d.W3 + d.W2, d.H - d.D1));
        if (d.W1 > 1e-9) right.Add(new(ht, d.H - d.D1));
        right.Add(new(ht, d.H));
        // Drop duplicates (zero-size features).
        var clean = new List<Vec2>();
        foreach (var p in right)
            if (clean.Count == 0 || (p - clean[^1]).Length > 1e-7) clean.Add(p);
        var pts = new List<Vec2>(clean);
        for (int i = clean.Count - 1; i >= 0; i--) pts.Add(new Vec2(-clean[i].X, clean[i].Y));
        return pts.ToArray();
    }

    /// <summary>Strands per row of the IGND grid (rows at 2½", 4½", …; positions at 1", 3", … each side of the CL).</summary>
    public int[] RowCapacity()
    {
        if (IsStateStandard) return Array.Empty<int>(); // strands come from the standard's own pattern (StrandLayout)
        int nRows = (int)Math.Floor((DepthIn - 3.5 - 2.5) / 2) + 1;
        var cap = new int[nRows];
        int[] bottom = BottomEdgeIn < 8 ? new[] { 14, 14, 12, 8, 4 } : new[] { 14, 14, 14, 12, 8, 4 };
        for (int r = 0; r < nRows; r++) cap[r] = r < bottom.Length ? bottom[r] : 2;
        return cap;
    }

    /// <summary>All grid positions in the standard fill order (row by row from the bottom, each row from A outward,
    /// left/right pairs). Section coordinates in meters.</summary>
    public List<Vec2> StrandPositions()
    {
        var list = new List<Vec2>();
        var cap = RowCapacity();
        for (int r = 0; r < cap.Length; r++)
        {
            double y = In(2.5 + 2 * r);
            for (int k = 0; k < cap[r] / 2; k++)
            {
                double x = In(1 + 2 * k);
                list.Add(new Vec2(-x, y));
                list.Add(new Vec2(x, y));
            }
        }
        return list;
    }

    /// <summary>Highest "A" grid position (y), the default top of the depressed strands at the ends.</summary>
    public double HighestAPosition => In(2.5 + 2 * Math.Max(0, RowCapacity().Length - 1));

    /// <summary>Shape from arbitrary dimensions with properties computed from the outline.</summary>
    public static TxGirderShape FromDims(PrecastIBeamDims d)
    {
        var o = d.H > 0 ? new TxGirderShape { Type = TxGirderType.Custom, Dims = d }.Outline() : Array.Empty<Vec2>();
        double a = 0, sy = 0, syy = 0, sxx = 0;
        for (int i = 0; i < o.Length; i++)
        {
            var p = o[i] / In(1);
            var q = o[(i + 1) % o.Length] / In(1);
            double cr = p.X * q.Y - q.X * p.Y;
            a += cr;
            sy += (p.Y + q.Y) * cr;
            syy += (p.Y * p.Y + p.Y * q.Y + q.Y * q.Y) * cr;
            sxx += (p.X * p.X + p.X * q.X + q.X * q.X) * cr;
        }
        a /= 2;
        double yb = Math.Abs(a) > 1e-9 ? sy / (6 * a) : 0;
        double ixBase = syy / 12, iy = sxx / 12;
        double ix = ixBase - a * yb * yb;
        return new TxGirderShape
        {
            Type = TxGirderType.Custom, Dims = d, Area = a, Yb = yb, Yt = d.H / In(1) - yb, Ix = ix, Iy = iy, WeightPlf = a / 144 * 155,
        };
    }
}

public static class TxGirderCatalog
{
    // IGD dimensions in PGSuper terms (TxDOT library: D1 3½, D2 2 (2½ Tx62/70), D3 2, D4 3, D5 4¾, D6 6¾/8¾, T 7, W2 12½ (15½),
    // W3 2, W4 3, W5 9½, C1 ¾).
    private static PrecastIBeamDims Tx(string n, double h, double d2, double d6, double w2) =>
        PrecastIBeamDims.Inches(n, 3.5, d2, 2, 3, 4.75, d6, h, 7, 7, 0, w2, 2, 3, 9.5, 0.75);

    private static readonly TxGirderShape[] Shapes =
    {
        new() { State = "TX", Family = "Tx", Source = "TxDOT IGD (Aug 2017)", Type = TxGirderType.Tx28, Dims = Tx("Tx28", 28, 2, 6.75, 12.5), Yt = 15.02, Yb = 12.98, Area = 585, Ix = 52772, Iy = 40559, WeightPlf = 630 },
        new() { State = "TX", Family = "Tx", Source = "TxDOT IGD (Aug 2017)", Type = TxGirderType.Tx34, Dims = Tx("Tx34", 34, 2, 6.75, 12.5), Yt = 18.49, Yb = 15.51, Area = 627, Ix = 88355, Iy = 40731, WeightPlf = 675 },
        new() { State = "TX", Family = "Tx", Source = "TxDOT IGD (Aug 2017)", Type = TxGirderType.Tx40, Dims = Tx("Tx40", 40, 2, 6.75, 12.5), Yt = 21.90, Yb = 18.10, Area = 669, Ix = 134990, Iy = 40902, WeightPlf = 720 },
        new() { State = "TX", Family = "Tx", Source = "TxDOT IGD (Aug 2017)", Type = TxGirderType.Tx46, Dims = Tx("Tx46", 46, 2, 8.75, 12.5), Yt = 25.90, Yb = 20.10, Area = 761, Ix = 198089, Iy = 46478, WeightPlf = 819 },
        new() { State = "TX", Family = "Tx", Source = "TxDOT IGD (Aug 2017)", Type = TxGirderType.Tx54, Dims = Tx("Tx54", 54, 2, 8.75, 12.5), Yt = 30.49, Yb = 23.51, Area = 817, Ix = 299740, Iy = 46707, WeightPlf = 880 },
        new() { State = "TX", Family = "Tx", Source = "TxDOT IGD (Aug 2017)", Type = TxGirderType.Tx62, Dims = Tx("Tx62", 62, 2.5, 8.75, 15.5), Yt = 33.72, Yb = 28.28, Area = 910, Ix = 463072, Iy = 57351, WeightPlf = 980 },
        new() { State = "TX", Family = "Tx", Source = "TxDOT IGD (Aug 2017)", Type = TxGirderType.Tx70, Dims = Tx("Tx70", 70, 2.5, 8.75, 15.5), Yt = 38.09, Yb = 31.91, Area = 966, Ix = 628747, Iy = 57579, WeightPlf = 1040 },
    };

    public static IReadOnlyList<TxGirderShape> All => Shapes;

    /// <summary>A Tx shape (for <see cref="TxGirderType.Custom"/> use <see cref="Get(PrestressedBeamDefinition)"/>).</summary>
    public static TxGirderShape Get(TxGirderType t) => t == TxGirderType.Custom ? Shapes[(int)TxGirderType.Tx54] : Shapes[(int)t];

    /// <summary>The definition's shape: a state standard (<see cref="PrestressedBeamDefinition.Standard"/>), a Tx girder, or
    /// its custom (PGSuper) section.</summary>
    public static TxGirderShape Get(PrestressedBeamDefinition pd) =>
        !string.IsNullOrWhiteSpace(pd.Standard) && StandardBeamCatalog.Find(pd.Standard) is { } std ? std
        : pd.Type == TxGirderType.Custom && pd.CustomSection is { } c ? TxGirderShape.FromDims(c) : Get(pd.Type);

    /// <summary>Tx girder by name ("Tx54", "TX54", "Tx 54"), else null.</summary>
    public static TxGirderType? ParseTx(string name)
    {
        var n = name.Replace(" ", "").ToUpperInvariant();
        foreach (var s in Shapes)
            if (n == s.Type.ToString().ToUpperInvariant()) return s.Type;
        return null;
    }

    /// <summary>Strand size as written on plans: 0.6" / 0.5" (US) or mm.</summary>
    public static string StrandSize(double diameterM, Units.UnitSystem u) =>
        u.IsUS ? $"{diameterM / In(1):0.0##}\"" : $"{diameterM * 1000:0.#} mm";

    /// <summary>Nominal strand area (in²) for the common sizes; otherwise from the diameter.</summary>
    public static double StrandAreaIn2(double diameterM)
    {
        double d = diameterM / In(1);
        if (Math.Abs(d - 0.5) < 0.01) return 0.153;
        if (Math.Abs(d - 0.52) < 0.01) return 0.167;
        if (Math.Abs(d - 0.6) < 0.01) return 0.217;
        if (Math.Abs(d - 0.7) < 0.01) return 0.294;
        return Math.PI * d * d / 4 * 0.74;
    }

    /// <summary>
    /// TxDOT IGEB Table of Bearing Pad Dimensions (Aug 2017): pad length along the girder × width (21"), the corner clips
    /// "A" (along the length) × "B" (across), or a 15" round pad. Abutments / transition bents with backwalls by girder
    /// end skew; conventional interior bents 8" (Tx28–Tx54) or 9" (Tx62/Tx70) at any skew.
    /// </summary>
    public static (double Length, double Width, double ClipA, double ClipB, bool Round) IgebPad(TxGirderType t, bool abutment, double skewDeg)
    {
        bool big = t is TxGirderType.Tx62 or TxGirderType.Tx70;
        double s = Math.Abs(skewDeg);
        if (!abutment) return (In(big ? 9 : 8), In(21), 0, 0, false);
        if (!big)
            return s <= 21 ? (In(8), In(21), 0, 0, false)
                : s <= 30 ? (In(8), In(21), In(1.5), In(2.5), false)
                : s <= 45 ? (In(9), In(21), In(4.5), In(4.5), false)
                : (In(15), In(15), 0, 0, true);
        return s <= 21 ? (In(9), In(21), 0, 0, false)
            : s <= 30 ? (In(9), In(21), In(1.5), In(2.5), false)
            : s <= 45 ? (In(10), In(21), In(4.5), In(4.5), false)
            : (In(10), In(21), In(7.25), In(4.25), false);
    }
}
