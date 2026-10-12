using SBM.Core.Bridge;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Prestressed;

/// <summary>
/// State standard precast beam shapes beyond TxDOT (state, family, name) with their EXACT outline: the sharp outline of
/// the standard drawing (the printed dimensions run to the intersection points of the faces) plus true radius fillets and
/// chamfers (<see cref="SectionProfile.FromCorners"/>). Section properties are computed from that outline; the values printed
/// on the standard are kept in <see cref="TxGirderShape.PrintedArea"/> etc. for the check. The TxDOT Tx28-Tx70 shapes stay in
/// <see cref="TxGirderCatalog"/> (WBFL polygon) and are listed here too.
/// <para>Standard beams (<see cref="Beam"/>) are taken from the state standard, not designed per bridge: standard length,
/// strands and details; differences against the layout are check warnings in the generator.</para>
/// </summary>
public static class StandardBeamCatalog
{
    private static readonly Lazy<TxGirderShape[]> Shapes = new(() => new[] { IowaBtd(), MnDot30Mh(), FlFib96() });

    /// <summary>The non-Tx standard shapes.</summary>
    public static IReadOnlyList<TxGirderShape> StateShapes => Shapes.Value;

    /// <summary>Every catalog shape: TxDOT Tx28-Tx70 and the state standards.</summary>
    public static IEnumerable<TxGirderShape> All => TxGirderCatalog.All.Concat(Shapes.Value);

    /// <summary>Shape by key: "IA BTD", "Iowa BTD", "BTD", "MN 30MH", "MnDOT 30MH", "30MH", "Tx54", "TX Tx54". Null if unknown.</summary>
    public static TxGirderShape? Find(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var k = Normalize(key);
        foreach (var s in All)
            if (k == Normalize(s.Key) || k == Normalize(s.Name)) return s;
        return null;
    }

    private static string Normalize(string s)
    {
        var u = s.ToUpperInvariant().Replace(" ", "").Replace("-", "");
        foreach (var (a, b) in new[] { ("IOWA", "IA"), ("MNDOT", "MN"), ("MINNESOTA", "MN"), ("TXDOT", "TX"), ("TEXAS", "TX"), ("FDOT", "FL"), ("FLORIDA", "FL") })
            if (u.StartsWith(a)) u = b + u[a.Length..];
        return u;
    }

    // ------------------------------------------------------------------ shapes

    /// <summary>
    /// Iowa DOT bulb tee "D" (BTD), from the BTD beam cross section on Woodbury Design 817 PDF p.28 (DS 26) / std 4730-4733:
    /// depth 4'-6, top flange 2'-10 (3 13/16 edge + 1 11/16 taper over 1'-1 3/4), web 6 1/2 (straight 2'-11 1/2), bottom flange
    /// 2'-6 (7 1/2 edge + 5 1/2 taper over 11 3/4); R = 8 web-flange fillets, R = 2 at the bottom flange taper-to-edge corners,
    /// 3/4" fillet (drawn as a chamfer) at the bottom corners; top flange corners sharp. Tangent-point sub-dimensions
    /// (4 5/8, 7 15/16, 7 1/16; 4 5/8, 6, 2 13/16, 1 13/16, 6 3/16) reproduce from these within 1/16".
    /// Printed: A = 748.8 in², yb = 24.64 in, I = 285,860 in⁴.
    /// </summary>
    private static TxGirderShape IowaBtd()
    {
        double h = 54, bt = 17, bb = 15, w = 3.25;
        double yTopEdge = h - 3.8125, yTopWeb = yTopEdge - 1.6875, yBotEdge = 7.5, yBotWeb = yBotEdge + 5.5;
        var corners = Symmetric(new[]
        {
            ProfileCorner.Chamfer(bb, 0, 0.75),
            ProfileCorner.Fillet(bb, yBotEdge, 2),
            ProfileCorner.Fillet(w, yBotWeb, 8),
            ProfileCorner.Fillet(w, yTopWeb, 8),
            ProfileCorner.Sharp(bt, yTopEdge),
            ProfileCorner.Sharp(bt, h),
        });
        var dims = PrecastIBeamDims.Inches("BTD", 3.8125, 1.6875, 0, 0, 5.5, 7.5, h, 6.5, 6.5, 0, 13.75, 0, 0, 11.75, 0.75);
        return Make("IA", "BT", dims, corners, "Iowa DOT std 4730-4733 / Woodbury Design 817 PDF p.28 (DS 26)", 748.8, 24.64, 285860);
    }

    /// <summary>
    /// MnDOT 30MH (mesa beam), from "SECTION AT CL SPAN" / END VIEW on Br02069 PDF p.16 (Fig. 5-397.501 mod.): H 30, top flange
    /// 2'-10 (5" edge + 1 5/8 taper over 13 3/4 = 5 5/16 + 8 7/16), web 6 1/2 (straight 1'-0 7/16), bottom flange 3'-3
    /// (5 1/2 edge + 5 7/16 taper over 16 1/4 = 4 1/8 + 10 3/4 + 1 3/8); 6" R web fillets (typ.), 1" R at the top flange
    /// taper-to-edge corners, 2" R at the bottom flange taper-to-edge corners, 3/4" chamfer at the bottom corners; top corners
    /// sharp. Section properties are not printed on the plan.
    /// </summary>
    private static TxGirderShape MnDot30Mh()
    {
        double h = 30, bt = 17, bb = 19.5, w = 3.25;
        double yTopEdge = h - 5, yTopWeb = yTopEdge - 1.625, yBotEdge = 5.5, yBotWeb = yBotEdge + 5.4375;
        var corners = Symmetric(new[]
        {
            ProfileCorner.Chamfer(bb, 0, 0.75),
            ProfileCorner.Fillet(bb, yBotEdge, 2),
            ProfileCorner.Fillet(w, yBotWeb, 6),
            ProfileCorner.Fillet(w, yTopWeb, 6),
            ProfileCorner.Fillet(bt, yTopEdge, 1),
            ProfileCorner.Sharp(bt, h),
        });
        var dims = PrecastIBeamDims.Inches("30MH", 5, 1.625, 0, 0, 5.4375, 5.5, h, 6.5, 6.5, 0, 13.75, 0, 0, 16.25, 0.75);
        return Make("MN", "MH", dims, corners, "MnDOT Fig. 5-397.501 (mod.) / Br02069 PDF p.16", null, null, null);
    }

    /// <summary>
    /// 96" Florida I-Beam (FIB-96) as DIMENSIONED on NCDOT B-2500 (Bonner Bridge) PDF p.438, sheet S4-46 "GIRDER OUTSIDE DIMENSIONS"
    /// (the plans cite no FDOT index; title "96" PRESTRESSED CONCRETE MODIFIED BULB TEE"): depth 8'-0; top flange 4'-0 wide,
    /// 3 1/2 edge, underside sloping 1 1/2 over 1'-5 from the tip, then a 3 1/2 x 3 1/2 chamfer into the 7" web (sharp corners: the
    /// chamfer IS the outline, ending 3 1/2 + 1 1/2 + 3 1/2 below the top); web straight 6'-1 down to the sharp intersection with the
    /// bottom flange slope (7 1/2 rise over 1'-3 1/2 from the web face to the 3'-2 wide flange edge, 7" vertical edge), R = 1'-3 fillet
    /// between web and slope. The bottom corners carry a 3/4" chamfer DRAWN but not dimensioned (scaled from the vector drawing:
    /// 0.73"). Vector geometry of p.438 reproduces every point within 0.01" (tangent points of the R 1'-3 at y 23.90" / (11.97", 10.40")).
    /// Section properties are not printed; computed A = 1,226.6 in² (8.52 ft²) - the 49.9 CY quantity of the 158'-3 1/2 span 30
    /// girder implies 8.51 ft².
    /// </summary>
    private static TxGirderShape FlFib96()
    {
        double h = 96, bt = 24, bb = 19, w = 3.5;
        var corners = Symmetric(new[]
        {
            ProfileCorner.Chamfer(bb, 0, 0.75),
            ProfileCorner.Sharp(bb, 7),
            ProfileCorner.Fillet(w, 14.5, 15),
            ProfileCorner.Sharp(w, h - 8.5),
            ProfileCorner.Sharp(w + 3.5, h - 5),
            ProfileCorner.Sharp(bt, h - 3.5),
            ProfileCorner.Sharp(bt, h),
        });
        var dims = PrecastIBeamDims.Inches("FIB-96", 3.5, 1.5, 3.5, 0, 7.5, 7, h, 7, 7, 0, 17, 3.5, 0, 15.5, 0.75);
        return Make("FL", "FIB", dims, corners, "NCDOT B-2500 PDF p.438 (S4-46) girder outside dimensions; no FDOT index cited", null, null, null);
    }

    /// <summary>Right half (bottom to top, inches) mirrored into a counter-clockwise outline.</summary>
    private static List<ProfileCorner> Symmetric(IReadOnlyList<ProfileCorner> right)
    {
        var list = new List<ProfileCorner>(right);
        for (int i = right.Count - 1; i >= 0; i--)
        {
            var c = right[i];
            list.Add(new ProfileCorner(new Vec2(-c.Point.X, c.Point.Y), c.Kind, c.Size));
        }
        return list;
    }

    private static TxGirderShape Make(string state, string family, PrecastIBeamDims dims, IReadOnlyList<ProfileCorner> cornersIn,
        string source, double? area, double? yb, double? ix)
    {
        var pin = SectionProfile.FromCorners(cornersIn);
        var p = pin.Properties();
        return new TxGirderShape
        {
            Type = TxGirderType.Custom, Dims = dims, State = state, Family = family, Source = source,
            Profile = pin.Scaled(In(1)),
            Area = p.Area, Yb = p.Yc, Yt = dims.H / In(1) - p.Yc, Ix = p.Ix, Iy = p.Iy, WeightPlf = p.Area / 144 * 150,
            PrintedArea = area, PrintedYb = yb, PrintedIx = ix,
        };
    }

    /// <summary>Computed vs printed properties (in², in, in⁴) of a shape: (name, computed, printed, difference) rows.</summary>
    public static List<(string Property, double Computed, double Printed, double Difference)> PropertyCheck(TxGirderShape s)
    {
        var rows = new List<(string, double, double, double)>();
        if (s.PrintedArea is { } a) rows.Add(("A (in²)", s.Area, a, s.Area - a));
        if (s.PrintedYb is { } y) rows.Add(("yb (in)", s.Yb, y, s.Yb - y));
        if (s.PrintedIx is { } i) rows.Add(("I (in⁴)", s.Ix, i, s.Ix - i));
        return rows;
    }

    // ------------------------------------------------------------------ standard beams

    /// <summary>Names of the standard beams in <see cref="Beam"/>.</summary>
    public static IReadOnlyList<string> BeamNames { get; } = new[] { "BTD55", "BTD60", "BTD105" };

    /// <summary>
    /// A standard beam as a beam definition: shape, standard length (CL-CL bearing + overall), end distance, strands (0.6" Gr 270,
    /// pattern on the 2" grid), deflected strands and hold-down points, concrete. Iowa BTD55 / BTD60 / BTD105: Woodbury Design 817
    /// PDF p.27 (DS 25, beam data table) and p.28-30 (DS 26-28, strand patterns). Null if unknown.
    /// The placement of a standard beam between layout bearings that differ from its standard length is not stated on those
    /// sheets: <see cref="PrestressedBeamDefinition.Placement"/> stays null (user decision pending) = UNRESOLVED warning when they differ.
    /// </summary>
    public static PrestressedBeamDefinition? Beam(string name)
    {
        var n = name.ToUpperInvariant().Replace(" ", "");
        return n switch
        {
            "BTD55" => Btd("BTD55", 55, 56 + 4 / 12.0, 5.0, 4.5, 0, new[] { (2.0, new[] { 2.0, 6, 10, 12, 18, 20, 24, 28 }), (6.0, new[] { 2.0, 6, 10, 20, 24, 28 }) },
                Array.Empty<double>(), 0, "Woodbury p.28 (DS 26)"),
            "BTD60" => Btd("BTD60", 60, 61 + 4 / 12.0, 5.0, 4.5, 6.0, new[] { (2.0, new[] { 2.0, 6, 10, 12, 14, 16, 18, 20, 24, 28 }), (6.0, new[] { 2.0, 12, 18, 28 }) },
                new[] { 14.0, 16 }, 4, "Woodbury p.29 (DS 27)"),
            "BTD105" => Btd("BTD105", 105, 106 + 4 / 12.0, 7.0, 6.0, 10.5, new[]
                {
                    (2.0, new[] { 2.0, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28 }),
                    (4.0, new[] { 2.0, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 28 }),
                    (6.0, new[] { 2.0, 6, 12, 14, 16, 18, 24, 28 }),
                }, new[] { 14.0, 16 }, 4, "Woodbury p.30 (DS 28)"),
            _ => null,
        };
    }

    /// <summary>Iowa BTD beam: rows (height above the bottom, x positions from the left bottom flange edge, 30" flange), deflected
    /// columns (x from the edge), hold-down distance from midspan (ft), top deflected strand at the ends below the top (in).</summary>
    private static PrestressedBeamDefinition Btd(string mark, double brgFt, double overallFt, double fc, double fci, double holdFt,
        (double y, double[] xs)[] rows, double[] deflectedCols, double belowTopIn, string sheet)
    {
        var layout = StrandPatterns.Rows(rows.Select(r => (In(r.y), r.xs.Select(x => In(x - 15)).ToArray())));
        var d = new PrestressedBeamDefinition
        {
            Standard = "IA BTD", Designation = mark, StrandDiameter = In(0.6), StrandStrength = 270,
            ConcreteStrength = fc, ReleaseStrength = fci,
            StandardBearingLength = Ft(brgFt), StandardOverallLength = Ft(overallFt), BearingToEnd = In(8),
            StrandLayout = layout, StrandCount = layout.Count,
            HarpedColumns = deflectedCols.Length > 0 ? deflectedCols.Select(x => In(x - 15)).ToList() : null,
            HarpedTopBelowTopAtEnd = belowTopIn > 0 ? In(belowTopIn) : 0,
            HoldDownFromMidspan = Ft(holdFt),
            Source = $"Iowa DOT standard BTD beam {mark}: Woodbury Design 817 p.27 (DS 25) beam data, {sheet} strand pattern",
        };
        d.DepressedStrands = StrandPatterns.Resolve(d, In(54))!.Count(s => s.Harped);
        return d;
    }
}

/// <summary>Helpers that build explicit strand layouts (<see cref="PrestressedBeamDefinition.StrandLayout"/>) from plan data.</summary>
public static class StrandPatterns
{
    /// <summary>Straight strands from rows: (height above the bottom, x positions right of the CL), meters.</summary>
    public static List<StrandPoint> Rows(IEnumerable<(double Y, double[] Xs)> rows) =>
        rows.SelectMany(r => r.Xs.Select(x => new StrandPoint { X = x, Y = r.Y, YEnd = r.Y })).ToList();

    /// <summary>Marks the strands within 1 mm of (x, y) as debonded <paramref name="length"/> from BOTH ends (symbol for the schedule).</summary>
    public static int Debond(List<StrandPoint> layout, IEnumerable<(double X, double Y)> at, double length, string symbol = "")
    {
        int n = 0;
        foreach (var (x, y) in at)
            foreach (var s in layout.Where(s => Math.Abs(s.X - x) < 1e-3 && Math.Abs(s.Y - y) < 1e-3))
            {
                s.DebondStart = s.DebondEnd = length;
                s.DebondSymbol = symbol;
                n++;
            }
        return n;
    }

    /// <summary>
    /// The strands the generator builds for a definition: the explicit layout with the grid-column harping applied
    /// (<see cref="PrestressedBeamDefinition.HarpedColumns"/>: every strand in a listed column is deflected; at the ends each
    /// column's strands keep their order, the highest at <see cref="PrestressedBeamDefinition.HarpedTopBelowTopAtEnd"/> below the
    /// top and the rest at <see cref="PrestressedBeamDefinition.HarpedEndSpacing"/> below each other). Null layout = null.
    /// </summary>
    public static List<StrandPoint>? Resolve(PrestressedBeamDefinition d, double depth)
    {
        if (d.StrandLayout == null) return null;
        var list = d.StrandLayout.Select(s => s.Clone()).ToList();
        if (d.HarpedColumns is { Count: > 0 } cols && d.HarpedTopBelowTopAtEnd > 0)
            foreach (double cx in cols)
            {
                var col = list.Where(s => Math.Abs(s.X - cx) < 1e-4).OrderByDescending(s => s.Y).ToList();
                for (int k = 0; k < col.Count; k++)
                {
                    col[k].Harped = true;
                    col[k].YEnd = depth - d.HarpedTopBelowTopAtEnd - k * d.HarpedEndSpacing;
                    col[k].XEnd = double.NaN;
                }
            }
        return list;
    }
}
