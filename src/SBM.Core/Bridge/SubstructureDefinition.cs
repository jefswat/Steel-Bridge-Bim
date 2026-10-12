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
    /// <summary>Piles only: plan direction (S, N components, footing frame) of the pile's WEB (the H section's web runs along it,
    /// the flanges across it). (0, 0) = automatic: web along N, or along the batter direction when the footing's
    /// <see cref="FootingDefinition.PileWeb"/> is <see cref="PileWebMode.AlongBatter"/> and the pile is battered.</summary>
    public double WebDirS { get; set; }
    public double WebDirN { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool HasWebDir => WebDirS * WebDirS + WebDirN * WebDirN > 1e-12;
    [System.Text.Json.Serialization.JsonIgnore] public bool IsBattered => BatterDirS * BatterDirS + BatterDirN * BatterDirN > 1e-12;
    public FootingPoint() { }
    public FootingPoint(double s, double n) { S = s; N = n; }
    public FootingPoint(double s, double n, double batterDirS, double batterDirN) { S = s; N = n; BatterDirS = batterDirS; BatterDirN = batterDirN; }
    public FootingPoint Clone() => new(S, N, BatterDirS, BatterDirN) { WebDirS = WebDirS, WebDirN = WebDirN };
}

/// <summary>How the web of the (steel H) piles of a footing is oriented in plan when a pile gives no direction of its own.</summary>
public enum PileWebMode
{
    /// <summary>Web across the cap (along N), flanges parallel to the cap line, for every pile (battered piles keep that section
    /// orientation rotated only by the batter).</summary>
    Default,
    /// <summary>Battered piles: web along the batter direction (flanges across it), as the plan pile symbols show; plumb piles as Default.</summary>
    AlongBatter,
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
    /// <summary>Plan orientation of the H-pile webs (see <see cref="PileWebMode"/>); a pile's own WebDirS/N overrides it.</summary>
    public PileWebMode PileWeb { get; set; } = PileWebMode.Default;
    /// <summary>As-designed reinforcing of this footing (the plan bar list: mats, side bars, uplift anchors). Empty = the generic
    /// mats of <see cref="SubstructureReinforcementDefinition.FootingBarSize"/> / spacing.</summary>
    public List<PlanBarGroup> Bars { get; set; } = new();
    /// <summary>Indices (into <see cref="Piles"/>) of the circled piles that carry the pile uplift anchors (the plans' 8f6 detail).</summary>
    public List<int> UpliftPiles { get; set; } = new();
    /// <summary>Uplift anchor V-bars per circled pile (the plans: 3, at 4 in. along the web).</summary>
    public int UpliftBarsPerPile { get; set; } = 3;
    /// <summary>OPTIONAL combined footing: this column stands on the footing of column n (1-based) - no footing / piles of its own
    /// (that footing's outline and piles are given relative to column n). 0 = own footing (default).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int OnFootingOf { get; set; }
    public FootingDefinition Clone() => new()
    {
        OnFootingOf = OnFootingOf,
        Bars = Bars.Select(b => b.Clone()).ToList(), UpliftPiles = UpliftPiles.ToList(), UpliftBarsPerPile = UpliftBarsPerPile,
        Column = Column, PileLength = PileLength, BatterRatio = BatterRatio, PileWeb = PileWeb,
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

/// <summary>Cap = concrete added to the cap; Maskwall; Pedestal = a bearing pedestal whose TOP is a fixed plan elevation (not driven by the
/// superstructure seats, e.g. the arch-span pedestals of an arch pier built by a separate model): its seat point is published on
/// <c>SubstructureUnit.FixedSeats</c> for whatever is built on it.</summary>
public enum CapBlockKind { Cap, Maskwall, Pedestal }
public enum CapBlockBase { CapTop, CapBottom }

/// <summary>
/// A vertical prism added to an as-designed cap: plan outline in cap coordinates (S along the support line from the alignment point, N normal to it, + up-station),
/// bottom on the cap top or the cap soffit (at each outline vertex), top = the plane TopElevation + TopSlopeS * (S - TopRefS) (level across N).
/// Woodbury piers (p.9 elevation, p.11 section C-C): the maskwall at the east cap end and the 4" reveal projection of the cap end face.
/// </summary>
public sealed class CapBlockDefinition
{
    public string Name { get; set; } = "";
    public CapBlockKind Kind { get; set; } = CapBlockKind.Cap;
    public List<FootingPoint> Outline { get; set; } = new();
    public CapBlockBase Base { get; set; } = CapBlockBase.CapTop;
    public double TopElevation { get; set; } = double.NaN;
    public double TopRefS { get; set; }
    public double TopSlopeS { get; set; }
    /// <summary>Plan reference (page / detail) for the description.</summary>
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL, <see cref="CapBlockKind.Pedestal"/>: the bearing (seat) point on the block top in cap coordinates (e.g. CL bearing x
    /// CL bearing line); null = the outline vertex average.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? SeatS { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? SeatN { get; set; }
    /// <summary>OPTIONAL plan differences about this block (e.g. the top does not reconcile with the bearing stack); each is raised as a
    /// generator warning, nothing is adjusted.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Reported { get; set; }
    public CapBlockDefinition Clone() => new()
    {
        Name = Name, Kind = Kind, Outline = Outline.Select(p => p.Clone()).ToList(), Base = Base, TopElevation = TopElevation, TopRefS = TopRefS, TopSlopeS = TopSlopeS, Source = Source,
        SeatS = SeatS, SeatN = SeatN, Reported = Reported?.ToList(),
    };
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

/// <summary>Pile type. CipPipe = cast-in-place concrete pipe pile (MnDOT "12\" C-I-P"): round steel shell (<see cref="SubstructureDefinition.PileShellThickness"/>)
/// filled with concrete; one round solid of the outside diameter <see cref="SubstructureDefinition.PileSize"/> (IFC: one IfcPile COMPOSITE, shell as a property).</summary>
public enum PileType { SteelHP, PrecastConcrete, CipPipe }

/// <summary>How chamfered-column vertical bars connect to the footing: run continuous from the footing mat (default), or
/// lap with L-shaped footing dowels, or mechanical couplers at two splice levels (alternate bars at different levels).</summary>
public enum ColumnSpliceType { Continuous, Lap, Coupler }

/// <summary>Ties of a flared column base: one closed tie per level, or lapped pairs of open U hairpins / closed end hoops.</summary>
public enum FlareTieType { ClosedTie, HairpinPair, HoopPair }

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
    // Clear covers used by the containment check, from the plans (not generator inputs): general note "minimum clear distance from face of
    // concrete to near reinforcing bar is to be 2 in. unless otherwise noted or shown" (footing sides, caps); footing top 3 in. as drawn on the
    // footing sections; footing bottom mat 1 in. clear ABOVE the top of the piles (pile section note "1 in. CL. top of pile").
    public double FootingSideClear { get; set; } = In(2);
    public double FootingTopClear { get; set; } = In(3);
    public double PileTopClear { get; set; } = In(1);

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
    /// <summary>Flare ties: one closed tie per level (default), or per level a lapped pair of open U hairpins / of closed end
    /// hoops around the two S ends (plans' "PLINTH, HAIRPINS" / "PLINTH, HOOPS"), lapping <see cref="FlareTieOverlap"/>.</summary>
    public FlareTieType FlareTieStyle { get; set; } = FlareTieType.ClosedTie;
    /// <summary>Out-to-out overlap along S of the pair (hairpins: between the leg ends; hoops: between the outside faces of the closing legs).</summary>
    public double FlareTieOverlap { get; set; }
    /// <summary>Pairs only: number of equal spaces between the levels (plans' "N SPA. @ ABT. s"), 3" above the footing to 3" below
    /// the plinth top; 0 = equal spaces not exceeding <see cref="FlareTieSpacing"/>.</summary>
    public int FlareTieSpaces { get; set; }

    // ---- Footing dowels / column splices (chamfered columns on a footing; ColumnSplice = Continuous keeps the old behaviour).
    // One L-shaped dowel (90 degree hook at the bottom, lying on the bottom mat) per column bar. Elevations are measured above the
    // footing top. Lap: the column bars start at the footing top and the dowels project ColumnLapLength above it (side by side, in
    // contact, on the inside of the column bar). Coupler: the column bar and its dowel are coaxial and meet at a coupler at the
    // level given by the bar's pattern letter.
    public ColumnSpliceType ColumnSplice { get; set; } = ColumnSpliceType.Continuous;
    /// <summary>Hook leg length of the dowels, out-to-out (0 = #11 2'-0, #14 2'-7, #18 3'-5).</summary>
    public double DowelHookLength { get; set; }
    /// <summary>Inside bend diameter of the dowel hook (0 = #11 1'-0, #14 1'-6 1/4, #18 2'-0, other sizes 6 db).</summary>
    public double DowelPin { get; set; }
    /// <summary>Underside of the dowel hook leg above the footing BOTTOM (the hook sits on the bottom mat).</summary>
    public double DowelBottomAboveFooting { get; set; } = In(16);
    /// <summary>Lap splice: dowel projection above the footing top (0 = Class B lap computed). The plans print 8'-4 uncoated / 12'-6 epoxy.</summary>
    public double ColumnLapLength { get; set; }
    /// <summary>Coupler splices: elevations above the footing top of the lower and the upper splice level.</summary>
    public double CouplerLow { get; set; }
    public double CouplerHigh { get; set; }
    /// <summary>Coupler body (generic: the plans do not give the make): length and diameter, 0 = 6 db / 1.75 db.</summary>
    public double CouplerLength { get; set; }
    public double CouplerDiameter { get; set; }
    /// <summary>One letter per vertical bar of a column, counter-clockwise from the +S axis (plan: S right, N up-station): H = splices
    /// at the upper level, L = lower level, T = lower level and ends <see cref="ColumnTerminatingBarLength"/> above it (bars that
    /// stop at the column top to clear the cap steel). Empty = alternate H, L around the section.</summary>
    public string ColumnSplicePattern { get; set; } = "";
    /// <summary>Length of the T bars of the pattern (0 = they run to the common bar top).</summary>
    public double ColumnTerminatingBarLength { get; set; }
    /// <summary>Column bar top above the soffit at the column centre (0 = 3 in. below the cap top cover as before).</summary>
    public double ColumnBarEmbedInCap { get; set; }
    /// <summary>Lap splice only: two-stage column (plans, Ramp G 9G): the upper bars start this far above the footing top and the lower bars
    /// run a lap length past that; 0 = single stage.</summary>
    public double ColumnMidLapStart { get; set; }

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

    /// <summary>As-designed cap and pedestal reinforcing (the plan bar list); when present it replaces the generic cap bars and stirrups.</summary>
    public List<PlanBarGroup> CapBars { get; set; } = new();

    public SubstructureReinforcementDefinition Clone()
    {
        var c = (SubstructureReinforcementDefinition)MemberwiseClone();
        c.ColumnChamferBarPositions = ColumnChamferBarPositions?.ToList();
        c.CapBars = CapBars.Select(b => b.Clone()).ToList();
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
    /// <summary>Optional plan prisms added to an as-designed cap (end maskwall, end reveal projection; Woodbury p.9 / p.11 section C-C), see <see cref="CapBlockDefinition"/>.</summary>
    public List<CapBlockDefinition> CapBlocks { get; set; } = new();
    /// <summary>OPTIONAL concrete mow strip around each column at the ground (see <see cref="MowStripDefinition"/>); null = none, omitted from the .sbm.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public MowStripDefinition? MowStrip { get; set; }
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
    /// <summary>Plan size of the bearing pedestals as the plans print it (cap plan, pedestal bar layout): along the cap (S) and across it (N), e.g. 5'-6 x 5'-8.
    /// Centred on the bearing footprint (on the mean of the seats when <see cref="PedestalShared"/>). 0 = derived from the bearing footprint + <see cref="PedestalProjection"/>.</summary>
    public double PedestalSizeAlong { get; set; }
    public double PedestalSizeAcross { get; set; }
    /// <summary>One pedestal block carries every seat of a girder line (back and ahead bearing of an expansion / continuity pier: 4 anchor bolts in one 5'-0 x 6'-0 block, the
    /// pedestal bar list counts 6 bars per BLOCK). The block top follows the highest seat; seats that differ are reported. Needs <see cref="PedestalSizeAlong"/> / Across.</summary>
    public bool PedestalShared { get; set; }
    /// <summary>Two pedestal rows of different widths (a shared pier's "BACK BRG." / "AHEAD BRG." rows, e.g. Pier 11WB 3'-11 / 3'-9): across size of the blocks of the
    /// seats on the ahead (+N) side of the CL pier; <see cref="PedestalSizeAcross"/> is then the back (-N) row's. 0 = every block uses PedestalSizeAcross.</summary>
    public double PedestalSizeAcrossAhead { get; set; }
    /// <summary>The two rows abut on the line N = CL pier + this value (+ toward ahead; Pier 11WB: 1"): back blocks span [joint - across, joint], ahead blocks
    /// [joint, joint + across ahead], instead of being centred on their seats. NaN = centred on the seats. Needs the plan size, not with <see cref="PedestalShared"/>.</summary>
    public double PedestalRowJoint { get; set; } = double.NaN;
    /// <summary>Plan pedestal blocks drawn as PARALLELOGRAMS (Woodbury cap steps, p.9: sides at 78 deg 30' to the CL pier): angle (degrees) of the block's
    /// side faces from the cap normal (N axis) in plan, positive = the side moves toward +S as N increases. <see cref="PedestalSizeAlong"/> is then measured
    /// along the cap at every N (the plan's "1'-10 + 1'-10" along the face). 0 = rectangular blocks. Needs the plan size.</summary>
    public double PedestalSideAngleDeg { get; set; }
    /// <summary>OPTIONAL own plan size along the cap / side angle of the AHEAD row (seats up-station of the CL pier) when the back and ahead
    /// parallelogram seats differ (girders meeting the pier at other angles each side, Northaven p.97 / p.102); null = the values above.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? PedestalSizeAlongAhead { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? PedestalSideAngleAheadDeg { get; set; }

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
    /// <summary>Rectangular columns: architectural projection (reveal) on BOTH faces normal to the cap (the S faces): depth beyond the face and its width along N,
    /// centred (Woodbury p.11 section B-B: 4" x 2'-11 5/8 on a 5'-3 5/8 x 4'-3 5/8 column, 5'-11 5/8 overall). 0 = none. Not with chamfers.</summary>
    public double ColumnProjectionS { get; set; }
    public double ColumnProjectionWidth { get; set; }
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
    /// <summary>Precast concrete pile side dimension; CIP pipe pile outside diameter.</summary>
    public double PileSize { get; set; } = In(18);
    /// <summary>CIP pipe pile steel shell wall thickness (NaN = not given: UNRESOLVED warning; the round solid is still built).</summary>
    public double PileShellThickness { get; set; } = double.NaN;
    /// <summary>Short pile label for views: "HP14x73", "12\" SQ", "12\" CIP PIPE".</summary>
    public string PileLabel(Func<double, string> shortLength) => PileType switch
    {
        PileType.SteelHP => PileSection,
        PileType.CipPipe => shortLength(PileSize) + " CIP PIPE",
        _ => shortLength(PileSize) + " SQ",
    };
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

    /// <summary>OPTIONAL plan items of this support's substructure that are open or approximated (each with the PDF page and the reason, built with
    /// <c>UnresolvedFlag.Text</c>); each is raised as a generator warning. Null = none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Unresolved { get; set; }

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
        c.CapBlocks = CapBlocks.Select(b => b.Clone()).ToList();
        c.MowStrip = MowStrip?.Clone();
        c.Aesthetics = Aesthetics?.Clone();
        c.Abutment = Abutment?.Clone();
        c.Unresolved = Unresolved?.ToList();
        return c;
    }
}
