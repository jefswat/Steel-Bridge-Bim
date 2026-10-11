using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>How the top of a bent (or abutment) cap is shaped across the bridge.</summary>
public enum CapTopShape
{
    /// <summary>One straight slope across the cap: the given slope, or (NaN) the best fit through the bearing seats.</summary>
    Sloped,
    /// <summary>Two slopes meeting at a crown (default: the profile grade line pivot), each fitted to its seats.</summary>
    Crowned,
    /// <summary>Level top; pedestals make up the difference to every seat.</summary>
    Level,
    /// <summary>Top follows the bearing seats (straight between bearings), so pedestals are all the minimum height.</summary>
    FollowSeats,
}

public enum CapBottomShape
{
    /// <summary>Constant depth: the bottom is parallel to the top.</summary>
    ParallelToTop,
    /// <summary>Level bottom; the depth input is the minimum (shallowest) depth.</summary>
    Level,
}

public enum ColumnShape { Round, Rectangular }

/// <summary>A point in a column's footing plan: S along the cap, N across it (up-station +), relative to the column.</summary>
public sealed class FootingPoint
{
    public double S { get; set; }
    public double N { get; set; }
    /// <summary>Piles only: unit plan direction (S, N components, footing frame) in which a BATTERED pile's tip moves away
    /// from its plan position; (0, 0) = plumb. The batter ratio is <see cref="FootingDefinition.BatterRatio"/>.</summary>
    public double BatterDirS { get; set; }
    public double BatterDirN { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool IsBattered => BatterDirS * BatterDirS + BatterDirN * BatterDirN > 1e-12;
    public FootingPoint() { }
    public FootingPoint(double s, double n) { S = s; N = n; }
    public FootingPoint(double s, double n, double batterDirS, double batterDirN) { S = s; N = n; BatterDirS = batterDirS; BatterDirN = batterDirN; }
    public FootingPoint Clone() => new(S, N, BatterDirS, BatterDirN);
}

/// <summary>
/// As-designed footing under one pier column (from the pile layout sheet). Anything left empty falls back to the pier's
/// standard inputs: rectangle FootingLength x FootingWidth, pile grid PileRows x PileColumns at PileSpacing, PileLength.
/// </summary>
public sealed class FootingDefinition
{
    /// <summary>1-based column number.</summary>
    public int Column { get; set; }
    /// <summary>Footing outline (closed polygon, e.g. a clipped corner); empty = rectangle.</summary>
    public List<FootingPoint> Outline { get; set; } = new();
    /// <summary>Pile positions; empty = the standard grid.</summary>
    public List<FootingPoint> Piles { get; set; } = new();
    /// <summary>Pile length (NaN = the pier's PileLength).</summary>
    public double PileLength { get; set; } = double.NaN;
    /// <summary>Batter of the piles that have a direction: 4 = "1:4" = 1 horizontal per 4 vertical (the plans do not define
    /// the ratio in words; this is the standard H:V reading, to be confirmed). Measured along the batter direction in plan.</summary>
    public double BatterRatio { get; set; } = 4;
    public FootingDefinition Clone() => new()
    {
        Column = Column, PileLength = PileLength, BatterRatio = BatterRatio,
        Outline = Outline.Select(p => p.Clone()).ToList(), Piles = Piles.Select(p => p.Clone()).ToList(),
    };

    /// <summary>
    /// Sets the batter directions of the plan's usual pattern from the pile positions: piles on the perimeter of the pile
    /// pattern (extreme |N| or, with <paramref name="sideRows"/>, extreme |S|) batter OUTWARD normal to that edge; where two
    /// edges meet (a corner pile) it batters diagonally at 45 degrees in plan when <paramref name="diagonalCorners"/>, else it
    /// follows the N edge. Interior piles stay plumb. Existing directions are replaced.
    /// </summary>
    public void BatterPerimeter(double ratio, bool sideRows, bool diagonalCorners)
    {
        BatterRatio = ratio;
        if (Piles.Count == 0) return;
        double eps = 1e-6, maxS = Piles.Max(p => Math.Abs(p.S)), maxN = Piles.Max(p => Math.Abs(p.N));
        double r2 = Math.Sqrt(0.5);
        foreach (var p in Piles)
        {
            bool onN = Math.Abs(Math.Abs(p.N) - maxN) < eps, onS = sideRows && Math.Abs(Math.Abs(p.S) - maxS) < eps;
            double ds = Math.Sign(p.S), dn = Math.Sign(p.N);
            if (onN && onS && diagonalCorners) { p.BatterDirS = ds * r2; p.BatterDirN = dn * r2; }
            else if (onN) { p.BatterDirS = 0; p.BatterDirN = dn; }
            else if (onS) { p.BatterDirS = ds; p.BatterDirN = 0; }
            else { p.BatterDirS = 0; p.BatterDirN = 0; }
        }
    }
}

/// <summary>A vertex of an as-designed cap elevation profile: S along the support line from the alignment point (right
/// positive), absolute elevation.</summary>
public sealed class CapProfilePoint
{
    public double S { get; set; }
    public double Elevation { get; set; }
    public CapProfilePoint() { }
    public CapProfilePoint(double s, double elevation) { S = s; Elevation = elevation; }
}

public enum FoundationType
{
    /// <summary>Drilled shafts (one per column; under abutment caps, the shafts frame directly into the cap).</summary>
    DrilledShaft,
    /// <summary>Spread footing under each column (continuous under abutment caps).</summary>
    SpreadFooting,
    /// <summary>Pile cap (footing) on a pile group under each column; abutment caps sit directly on piles.</summary>
    PileCap,
    None,
}

public enum PileType { SteelHP, PrecastConcrete }

/// <summary>Reinforcing for caps, columns, shafts, footings and backwalls (US bar sizes).</summary>
public sealed class SubstructureReinforcementDefinition
{
    public bool Enabled { get; set; } = true;
    public double ConcreteStrengthKsi { get; set; } = 3.6;
    public double SteelYieldKsi { get; set; } = 60;
    public bool EpoxyCoated { get; set; }
    public double CapCover { get; set; } = In(2);
    public double ColumnCover { get; set; } = In(2.5);
    public double ShaftCover { get; set; } = In(3);
    public double FootingCover { get; set; } = In(3);

    // Cap
    public int CapTopBarCount { get; set; } = 7;
    public string CapTopBarSize { get; set; } = "#11";
    public int CapBottomBarCount { get; set; } = 7;
    public string CapBottomBarSize { get; set; } = "#11";
    /// <summary>Longitudinal skin bars on each side face, between the top and bottom layers.</summary>
    public int CapSideBarsPerFace { get; set; } = 2;
    public string CapSideBarSize { get; set; } = "#5";
    public string CapStirrupSize { get; set; } = "#5";
    public double CapStirrupSpacing { get; set; } = In(6);

    // Columns and shafts
    public int ColumnBarCount { get; set; } = 12;
    public string ColumnBarSize { get; set; } = "#9";
    public string ColumnSpiralSize { get; set; } = "#4";
    /// <summary>Spiral pitch (round) or tie spacing (rectangular).</summary>
    public double ColumnSpiralPitch { get; set; } = In(6);
    // Rectangular / chamfered (octagonal) columns: vertical bars per face (plans' groups), dimensioned along the OUTSIDE
    // face and set in by cover + tie + db/2. All counts 0 = ColumnBarCount at equal spaces around the inset section.
    // A spacing of 0 = equal spaces over the face with bars at both of its (inset) corners, shared with the neighbouring
    // face; a spacing > 0 = a group centred on the face.
    /// <summary>Bars on each face parallel to the cap (along S).</summary>
    public int ColumnFaceBarsS { get; set; }
    public double ColumnFaceSpacingS { get; set; }
    /// <summary>Bars on each face normal to the cap (along N).</summary>
    public int ColumnFaceBarsN { get; set; }
    public double ColumnFaceSpacingN { get; set; }
    /// <summary>Bars on each chamfer face.</summary>
    public int ColumnChamferBars { get; set; }
    public double ColumnChamferSpacing { get; set; }
    /// <summary>Chamfer bars at these distances along the outside chamfer face from its S-face end (uneven groups); overrides the count.</summary>
    public List<double>? ColumnChamferBarPositions { get; set; }
    /// <summary>
    /// Perimeter ties as two closed hoops, each around one end of the section, overlapping this width in the middle (plans'
    /// "hoops"); 0 = one closed tie around the whole section. Tie size / spacing = ColumnSpiralSize / ColumnSpiralPitch.
    /// </summary>
    public double ColumnHoopOverlap { get; set; }
    /// <summary>Crossties (135° hooks) across each side between the middle bars of the upper and lower chamfers, at every tie level; "" = none.</summary>
    public string ColumnCrosstieSize { get; set; } = "";
    /// <summary>Chamfer bar the crossties wrap, counted from the S-face end of the chamfer (1 = first); 0 = the one nearest the chamfer middle.</summary>
    public int ColumnCrosstieBar { get; set; }

    // Flared column base (SubstructureDefinition.ColumnFlare*): its own vertical bars and ties around the larger
    // section, from the foundation to below the flare top; the column cage continues inside it.
    public string FlareBarSize { get; set; } = "";
    /// <summary>Flare bars per face (S faces, N faces, chamfers); spacing 0 = equal spaces with shared corner bars, > 0 = a group
    /// centred on the face (as for the column); all counts 0 = no flare bars.</summary>
    public int FlareFaceBarsS { get; set; }
    public double FlareFaceSpacingS { get; set; }
    public int FlareFaceBarsN { get; set; }
    public double FlareFaceSpacingN { get; set; }
    public int FlareChamferBars { get; set; }
    public double FlareCover { get; set; } = In(3.5);
    public string FlareTieSize { get; set; } = "#6";
    public double FlareTieSpacing { get; set; } = In(12);

    public int ShaftBarCount { get; set; } = 12;
    public string ShaftBarSize { get; set; } = "#9";
    public string ShaftSpiralSize { get; set; } = "#4";
    public double ShaftSpiralPitch { get; set; } = In(6);

    // Footings
    public string FootingBarSize { get; set; } = "#8";
    public double FootingBarSpacing { get; set; } = In(12);

    // Abutment backwall
    public string BackwallBarSize { get; set; } = "#5";
    public double BackwallBarSpacing { get; set; } = In(12);

    public double MaxBarLength { get; set; } = Ft(60);

    public SubstructureReinforcementDefinition Clone()
    {
        var c = (SubstructureReinforcementDefinition)MemberwiseClone();
        c.ColumnChamferBarPositions = ColumnChamferBarPositions?.ToList();
        return c;
    }
}

/// <summary>
/// Substructure at one support: a cap (bent cap at piers, abutment cap with backwall at abutments) with pedestals under
/// the bearings, columns (piers), and foundations. Cap positions are measured along the support line (skewed), from the
/// alignment, right positive; distances across the cap are normal to the support line.
/// Elevations: the cap top is placed so the shortest pedestal is <see cref="MinPedestalHeight"/>; pedestals take up
/// the rest of each seat (variable pedestals), so a sloping cap and variable pedestals are both supported.
/// </summary>
public sealed class SubstructureDefinition
{
    public bool Enabled { get; set; } = true;

    // ---------------- Cap
    /// <summary>Cap width normal to the support line.</summary>
    public double CapWidth { get; set; } = Ft(4);
    /// <summary>Cap depth (constant for a parallel bottom; the minimum depth for a level bottom).</summary>
    public double CapDepth { get; set; } = Ft(4);
    /// <summary>Cap extension beyond the exterior bearings, measured along the cap.</summary>
    public double CapEndOverhang { get; set; } = Ft(2.5);
    /// <summary>Shift of the cap centerline from the CL bearing (normal to the support line, up-station positive). Piers only.</summary>
    public double CapOffset { get; set; }
    /// <summary>
    /// As-designed cap elevation (from the pier / abutment elevation sheet): a closed polygon in (S, elevation), e.g.
    /// tapered cantilevers with raked ends or a hammerhead. When given it replaces the top/bottom shape rules, the cap
    /// depth and the cap extents; pedestals still take the difference to each bearing seat. Empty = derived.
    /// </summary>
    public List<CapProfilePoint> CapProfile { get; set; } = new();
    /// <summary>Optional as-designed profile of the up-station half of the cap width (e.g. a stepped top where the ahead
    /// unit's girders are shallower). Empty = the whole width uses <see cref="CapProfile"/>.</summary>
    public List<CapProfilePoint> CapProfileAhead { get; set; } = new();
    /// <summary>Optional architectural texture band on one face of an as-designed cap (see <see cref="CapAestheticsDefinition"/>).</summary>
    public CapAestheticsDefinition? Aesthetics { get; set; }
    public CapTopShape TopShape { get; set; } = CapTopShape.Sloped;
    /// <summary>Slope of a sloped cap top (rise/run along the cap, left to right); NaN = fit to the bearing seats.</summary>
    public double CapSlope { get; set; } = double.NaN;
    /// <summary>Crown position along the cap for a crowned top; NaN = at the profile grade pivot.</summary>
    public double CrownOffset { get; set; } = double.NaN;
    public CapBottomShape BottomShape { get; set; } = CapBottomShape.ParallelToTop;

    // ---------------- Pedestals (bearing seats)
    /// <summary>Shortest pedestal (0 = the highest-seated bearing sits on the cap). Pedestals under 1/8" are not built.</summary>
    public double MinPedestalHeight { get; set; } = In(3);
    /// <summary>Pedestal projection beyond the bearing footprint (masonry plate or pad), each side.</summary>
    public double PedestalProjection { get; set; } = In(3);
    /// <summary>Pedestals taller than this are reported (consider sloping the cap).</summary>
    public double MaxPedestalHeight { get; set; } = Ft(1.5);

    // ---------------- Columns (piers)
    public int ColumnCount { get; set; } = 3;
    /// <summary>Spacings between columns along the cap (ColumnCount − 1 values).</summary>
    public List<double> ColumnSpacings { get; set; } = new() { Ft(15), Ft(15) };
    /// <summary>Position of column 1 along the cap from the alignment (right positive); NaN = centered under the bearings.</summary>
    public double FirstColumnOffset { get; set; } = double.NaN;
    public ColumnShape ColumnShape { get; set; } = ColumnShape.Round;
    public double ColumnDiameter { get; set; } = Ft(3);
    /// <summary>Rectangular columns: size along the cap.</summary>
    public double ColumnWidth { get; set; } = Ft(3);
    /// <summary>Rectangular columns: size normal to the cap.</summary>
    public double ColumnDepth { get; set; } = Ft(3);
    /// <summary>Rectangular columns: corner chamfer along the cap (S) and across it (N); 0 = square corners.</summary>
    public double ColumnChamferS { get; set; }
    public double ColumnChamferN { get; set; }
    /// <summary>Optional flared base: below this elevation the column has the flare section (NaN = no flare).</summary>
    public double ColumnFlareElevation { get; set; } = double.NaN;
    public double ColumnFlareWidth { get; set; }
    public double ColumnFlareDepth { get; set; }
    public double ColumnFlareChamferS { get; set; }
    public double ColumnFlareChamferN { get; set; }

    // ---------------- Foundation
    public FoundationType Foundation { get; set; } = FoundationType.DrilledShaft;
    /// <summary>Top of foundation (shaft top / footing top); NaN = ground − <see cref="GroundCover"/>, or 20 ft of column without terrain.</summary>
    public double FoundationTopElevation { get; set; } = double.NaN;
    /// <summary>Depth of the top of foundation below the ground when it is set automatically.</summary>
    public double GroundCover { get; set; } = Ft(1);
    public double ShaftDiameter { get; set; } = Ft(3.5);
    public double ShaftLength { get; set; } = Ft(40);
    /// <summary>Footing / pile cap size along the cap.</summary>
    public double FootingLength { get; set; } = Ft(10);
    /// <summary>Footing / pile cap size normal to the cap.</summary>
    public double FootingWidth { get; set; } = Ft(10);
    public double FootingThickness { get; set; } = Ft(3);
    public PileType PileType { get; set; } = PileType.SteelHP;
    /// <summary>"HP14x73" style for steel; side dimension for precast (see <see cref="PileSize"/>).</summary>
    public string PileSection { get; set; } = "HP14x73";
    /// <summary>Precast concrete pile side dimension.</summary>
    public double PileSize { get; set; } = In(18);
    /// <summary>Piles across the cap (normal to the support line) per group.</summary>
    public int PileRows { get; set; } = 3;
    /// <summary>Piles along the cap per group (pile caps); total piles under an abutment cap.</summary>
    public int PileColumns { get; set; } = 3;
    public double PileSpacing { get; set; } = Ft(3);
    public double PileLength { get; set; } = Ft(50);
    public double PileEmbedment { get; set; } = In(12);
    /// <summary>Optional as-designed footings per column (outline, explicit piles, pile length); others use the standard inputs.</summary>
    public List<FootingDefinition> Footings { get; set; } = new();

    // ---------------- Abutments
    /// <summary>Abutment cap: distance from the CL bearing to the front (span-side) face of the cap.</summary>
    public double BearingToCapFront { get; set; } = Ft(1.5);
    public double BackwallThickness { get; set; } = Ft(1);
    /// <summary>Gap between the end of the deck and the front face of the backwall.</summary>
    public double BackwallGap { get; set; } = In(1);
    /// <summary>Top of backwall below finished grade (seat for an approach slab or joint).</summary>
    public double BackwallTopBelowGrade { get; set; }
    /// <summary>Abutment drilled shafts / piles: count and spacing along the cap (uses the column inputs when 0).</summary>
    public int AbutmentFoundationCount { get; set; } = 4;
    public double AbutmentFoundationSpacing { get; set; } = Ft(12);

    /// <summary>OPTIONAL as-designed abutment (footing, backwall with corbel, beam steps, maskwalls, wingwalls, piles, bar list); null = the
    /// standard parametric abutment above. Abutments only.</summary>
    public AbutmentDefinition? Abutment { get; set; }

    public SubstructureReinforcementDefinition Reinforcement { get; set; } = new();

    public static SubstructureDefinition Default(SupportType type) => type == SupportType.Pier
        ? new SubstructureDefinition()
        : new SubstructureDefinition
        {
            CapWidth = Ft(3.5), CapDepth = Ft(3), Foundation = FoundationType.DrilledShaft, ShaftDiameter = Ft(2.5),
            ShaftLength = Ft(35), ColumnCount = 0, ColumnSpacings = new(),
        };

    public IReadOnlyList<double> ColumnOffsets(double centerIfAuto)
    {
        var list = new List<double>();
        if (ColumnCount <= 0) return list;
        double total = 0;
        for (int i = 0; i < ColumnCount - 1; i++) total += i < ColumnSpacings.Count ? ColumnSpacings[i] : ColumnSpacings.LastOrDefault(Ft(15));
        double s = double.IsNaN(FirstColumnOffset) ? centerIfAuto - total / 2 : FirstColumnOffset;
        list.Add(s);
        for (int i = 0; i < ColumnCount - 1; i++) list.Add(s += i < ColumnSpacings.Count ? ColumnSpacings[i] : ColumnSpacings.LastOrDefault(Ft(15)));
        return list;
    }

    public List<string> Validate(string name, SupportType type)
    {
        var e = new List<string>();
        if (CapWidth <= 0 || CapDepth <= 0) e.Add($"{name}: cap width and depth must be positive.");
        if (MinPedestalHeight < 0) e.Add($"{name}: minimum pedestal height must not be negative.");
        if (type == SupportType.Pier)
        {
            if (ColumnCount < 1) e.Add($"{name}: a bent needs at least one column.");
            if (ColumnCount > 1 && ColumnSpacings.Take(ColumnCount - 1).Any(s => s <= 0)) e.Add($"{name}: column spacings must be positive.");
            double size = ColumnShape == ColumnShape.Round ? ColumnDiameter : Math.Min(ColumnWidth, ColumnDepth);
            if (size <= 0) e.Add($"{name}: column size must be positive.");
        }
        if (Foundation == FoundationType.DrilledShaft && (ShaftDiameter <= 0 || ShaftLength <= 0)) e.Add($"{name}: drilled shaft diameter and length must be positive.");
        if (Foundation is FoundationType.SpreadFooting or FoundationType.PileCap && (FootingLength <= 0 || FootingWidth <= 0 || FootingThickness <= 0))
            e.Add($"{name}: footing dimensions must be positive.");
        if (Foundation == FoundationType.PileCap && (PileRows < 1 || PileColumns < 1 || PileSpacing <= 0 || PileLength <= 0))
            e.Add($"{name}: pile rows, columns, spacing and length must be positive.");
        return e;
    }

    public SubstructureDefinition Clone()
    {
        var c = (SubstructureDefinition)MemberwiseClone();
        c.ColumnSpacings = ColumnSpacings.ToList();
        c.Reinforcement = Reinforcement.Clone();
        c.CapProfile = CapProfile.Select(p => new CapProfilePoint(p.S, p.Elevation)).ToList();
        c.CapProfileAhead = CapProfileAhead.Select(p => new CapProfilePoint(p.S, p.Elevation)).ToList();
        c.Footings = Footings.Select(f => f.Clone()).ToList();
        c.Aesthetics = Aesthetics?.Clone();
        c.Abutment = Abutment?.Clone();
        return c;
    }
}
