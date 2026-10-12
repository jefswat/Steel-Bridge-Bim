using static SBM.Core.Units.UnitSystem;
using SBM.Core.Alignments;

namespace SBM.Core.Bridge;

// All lengths in meters, angles in degrees (skews are user-facing), slopes rise/run.
// Defaults are typical US values for a steel plate girder bridge.

public enum SupportType { Abutment, Pier }

public sealed class SupportDefinition
{
    public string Name { get; set; } = "";
    public SupportType Type { get; set; }

    /// <summary>Station of the centerline of bearing at the alignment.</summary>
    public double Station { get; set; }

    /// <summary>
    /// Skew angle in degrees measured from the radial (normal-to-alignment) line. 0 = square;
    /// positive = the right end of the support line is ahead (up-station) of the left end.
    /// </summary>
    public double SkewDegrees { get; set; }

    /// <summary>Abutments only: distance from CL bearing to end of deck (measured normal to the support line).</summary>
    public double DeckEndOffset { get; set; } = Ft(1.5);

    /// <summary>End supports (abutments and expansion piers): girder extension beyond CL bearing to the girder end.</summary>
    public double GirderEndExtension { get; set; } = In(9);

    /// <summary>
    /// Expansion pier shared by several units / bridges (e.g. "Pier 5WB"): every support with the same id is one
    /// physical pier. Its cap, columns and foundation are built once (by the first bridge in the project whose support
    /// has the id and an enabled substructure) from the bearings of ALL bridges that land on it. Empty = not shared.
    /// </summary>
    public string SharedPier { get; set; } = "";
    /// <summary>Shared piers: CL bearing of this support line to the CL pier, normal to the support line, up-station positive.</summary>
    public double PierCenterOffset { get; set; }

    public BearingFixity Fixity { get; set; } = BearingFixity.Auto;

    public BearingFixity ResolvedFixity => Fixity != BearingFixity.Auto ? Fixity
        : Type == SupportType.Pier ? BearingFixity.Fixed : BearingFixity.Expansion;

    /// <summary>Per-girder fixity by girder index (plans' bearing type per girder line, e.g. FX / MD / GE); Auto or missing = <see cref="ResolvedFixity"/>.</summary>
    public List<BearingFixity>? GirderFixities { get; set; }

    public BearingFixity FixityOf(int girder) =>
        GirderFixities is { } l && girder >= 0 && girder < l.Count && l[girder] != BearingFixity.Auto ? l[girder] : ResolvedFixity;

    /// <summary>Bearings at this support (e.g. anchor bolts that differ by pier); null = the bridge's.</summary>
    public BearingDefinition? Bearings { get; set; }

    /// <summary>Cap, pedestals, columns and foundation; null = defaults for the support type.</summary>
    public SubstructureDefinition? Substructure { get; set; }

    /// <summary>Deck expansion joint at this END support (blockout + generic device); null = none.</summary>
    public ExpansionJointDefinition? ExpansionJoint { get; set; }

    /// <summary>
    /// OPTIONAL kinked end of slab at this END support (first or last support of the bridge): the plan's break points of the end-of-slab
    /// line as station / offset on the bridge alignment, LEFT to RIGHT, monotone along the line; the first and last segments extend to the
    /// deck edges. Null or fewer than 2 points = the straight line <see cref="DeckEndOffset"/> from the CL bearing (the default). When set it
    /// governs the deck outline, the slab solid, the joint blockout / end block and the clipping of the deck bars at this end;
    /// <see cref="DeckEndOffset"/> is still used for the opening / gap checks.
    /// </summary>
    public List<DeckEndPoint>? DeckEndPoints { get; set; }

    /// <summary>OPTIONAL concrete end / pier diaphragm (prestressed bridges, <see cref="ConcreteDiaphragmDefinition"/>); null = none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ConcreteDiaphragmDefinition? Diaphragm { get; set; }

    /// <summary>OPTIONAL dapped steel girder ends on this support line (expansion joints, <see cref="GirderEndDapDefinition"/>); null = square ends.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GirderEndDapDefinition? GirderEndDap { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public SubstructureDefinition ResolvedSubstructure => Substructure ?? SubstructureDefinition.Default(Type);

    public SupportDefinition Clone()
    {
        var c = (SupportDefinition)MemberwiseClone();
        c.Substructure = Substructure?.Clone();
        c.Bearings = Bearings?.Clone();
        c.ExpansionJoint = ExpansionJoint?.Clone();
        c.GirderFixities = GirderFixities?.ToList();
        c.DeckEndPoints = DeckEndPoints?.Select(p => p.Clone()).ToList();
        c.Diaphragm = Diaphragm?.Clone();
        c.GirderEndDap = GirderEndDap?.Clone();
        return c;
    }
}

/// <summary>A break point of a kinked end-of-slab line (<see cref="SupportDefinition.DeckEndPoints"/>): station / offset on the bridge alignment.</summary>
public sealed class DeckEndPoint
{
    public double Station { get; set; }
    public double Offset { get; set; }
    public DeckEndPoint() { }
    public DeckEndPoint(double station, double offset) { Station = station; Offset = offset; }
    public DeckEndPoint Clone() => (DeckEndPoint)MemberwiseClone();
}

public enum GirderPlanLayout
{
    /// <summary>Girders follow the alignment at their offsets (horizontally curved girders).</summary>
    CurvedParallel,
    /// <summary>Girders are straight between bearing points (chorded spans).</summary>
    Chorded,
}

public sealed class PlateSize
{
    public double Width { get; set; }
    public double Thickness { get; set; }
    public PlateSize() { }
    public PlateSize(double width, double thickness) { Width = width; Thickness = thickness; }
    public PlateSize Clone() => new(Width, Thickness);
}

public enum GirderPlate { TopFlange, Web, BottomFlange }

/// <summary>
/// A plate size that applies from the previous change (or girder start) up to <see cref="EndStation"/> — the station
/// of a radial line across all girders (shop splice / butt weld). The last segment of each plate runs to the girder end.
/// For the web, <see cref="PlateSize.Width"/> is ignored (web depth is constant).
/// </summary>
public sealed class PlateSegment
{
    public GirderPlate Plate { get; set; }
    public double EndStation { get; set; } = double.NaN;
    public double Width { get; set; }
    public double Thickness { get; set; }
    public PlateSegment Clone() => (PlateSegment)MemberwiseClone();
}

/// <summary>
/// One plate run of an as-designed girder: the plate size up to <see cref="EndSplice"/> (plan field splice number) or
/// <see cref="EndOffset"/> along the girder from its CL bearing at <see cref="EndSupport"/> (framing support index, + ahead).
/// The last run of a plate runs to the girder end (its end is ignored).
/// </summary>
public sealed class PlateRun
{
    public GirderPlate Plate { get; set; }
    public int EndSplice { get; set; }
    public int EndSupport { get; set; } = -1;
    public double EndOffset { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public PlateRun Clone() => (PlateRun)MemberwiseClone();
}

/// <summary>Plate runs of a group of girders (plans' girder elevations, e.g. "Girder A & F thru K").</summary>
public sealed class GirderPlateLayout
{
    public List<int> Girders { get; set; } = new();
    public List<PlateRun> Runs { get; set; } = new();
    public GirderPlateLayout Clone() => new() { Girders = Girders.ToList(), Runs = Runs.Select(r => r.Clone()).ToList() };
}

public sealed class GirderSection
{
    public PlateSize TopFlange { get; set; } = new(In(16), In(1));
    public double WebDepth { get; set; } = In(54);
    public double WebThickness { get; set; } = In(0.5);
    public PlateSize BottomFlange { get; set; } = new(In(18), In(1.25));

    /// <summary>
    /// Optional plate changes along the girders (all girders). Where a plate has no rows, the typical size above
    /// is used full length. Rows for one plate are ordered by station; the last row's station is ignored.
    /// </summary>
    public List<PlateSegment> PlateChanges { get; set; } = new();

    /// <summary>As-designed plate runs per girder group (plate ends at field splices / offsets from bearings); girders
    /// listed here use them instead of <see cref="PlateChanges"/> for the plates they define.</summary>
    public List<GirderPlateLayout> Layouts { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public double TotalDepth => MaxTopFlangeThickness + WebDepth + BottomFlange.Thickness;

    /// <summary>Plate segments for one plate, ordered, with the typical size when none are given.</summary>
    public List<PlateSegment> Segments(GirderPlate plate)
    {
        var rows = PlateChanges.Where(p => p.Plate == plate).OrderBy(p => double.IsNaN(p.EndStation) ? double.MaxValue : p.EndStation).ToList();
        if (rows.Count > 0) return rows;
        return new List<PlateSegment>
        {
            plate switch
            {
                GirderPlate.TopFlange => new PlateSegment { Plate = plate, Width = TopFlange.Width, Thickness = TopFlange.Thickness },
                GirderPlate.BottomFlange => new PlateSegment { Plate = plate, Width = BottomFlange.Width, Thickness = BottomFlange.Thickness },
                _ => new PlateSegment { Plate = plate, Width = WebDepth, Thickness = WebThickness },
            },
        };
    }

    /// <summary>Plate size at a station (station boundaries belong to the earlier segment).</summary>
    public PlateSegment SegmentAt(GirderPlate plate, double station)
    {
        var segs = Segments(plate);
        for (int i = 0; i < segs.Count - 1; i++)
            if (station <= segs[i].EndStation) return segs[i];
        return segs[^1];
    }

    /// <summary>Thickest top flange (stud lengths, depth reporting). Girder elevations do not depend on flange thickness.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double MaxTopFlangeThickness => Segments(GirderPlate.TopFlange).Max(s => s.Thickness);

    public GirderSection Clone() => new()
    {
        TopFlange = TopFlange.Clone(), WebDepth = WebDepth, WebThickness = WebThickness, BottomFlange = BottomFlange.Clone(),
        PlateChanges = PlateChanges.Select(p => p.Clone()).ToList(),
        Layouts = Layouts.Select(l => l.Clone()).ToList(),
    };
}

/// <summary>A discontinuous girder: it ends on a cross girder <see cref="Distance"/> past the CL bearing of
/// <see cref="Support"/> (measured horizontally along the girder), framed between the neighboring girders.</summary>
/// <summary>A girder that begins on an auxiliary support line (e.g. a hammerhead beside the first pier) instead of on
/// the bridge's first support; it has its bearing there and no bearing on the first support.</summary>
public sealed class GirderBeginning
{
    /// <summary>Girder index (0 = G1).</summary>
    public int Girder { get; set; }
    /// <summary>Name of the auxiliary support (<see cref="BridgeDefinition.AuxiliarySupports"/>) carrying its start bearing.</summary>
    public string Support { get; set; } = "";
    public GirderBeginning Clone() => (GirderBeginning)MemberwiseClone();
}

public sealed class GirderTermination
{
    /// <summary>Girder index (0 = G1).</summary>
    public int Girder { get; set; }
    /// <summary>Support index the distance is measured from.</summary>
    public int Support { get; set; }
    /// <summary>CL bearing to CL cross girder, along the girder.</summary>
    public double Distance { get; set; }
    /// <summary>
    /// Optional plan angle (degrees) of the cross girder to this girder, as dimensioned on the plans: from the girder's
    /// ahead tangent, clockwise, to the cross girder direction (left → right). 90 = square (default when null).
    /// </summary>
    public double? CrossGirderAngleDegrees { get; set; }
    public GirderTermination Clone() => (GirderTermination)MemberwiseClone();
}

/// <summary>Built-up cross girder that discontinuous girders frame into.</summary>
public sealed class CrossGirderSection
{
    public double WebDepth { get; set; } = In(60);
    public double WebThickness { get; set; } = In(0.625);
    public double FlangeWidth { get; set; } = In(15);
    public double FlangeThickness { get; set; } = In(0.625);
    /// <summary>Gap between the end of the discontinuous girder and the cross girder web.</summary>
    public double EndGap { get; set; } = In(0.75);

    // ---- As-designed details (all OPTIONAL: the defaults reproduce the simple legacy cross girder).

    /// <summary>
    /// null = the top of the cross girder top flange is level with the top-of-web chord of the supporting girders (legacy).
    /// Otherwise the top of the top flange is this far BELOW the top of web (bottom of top flange) of the discontinuous
    /// girder at its end (I-80: 11" in Unit 2, 8" in Unit 3, design sheet 121).
    /// </summary>
    public double? TopBelowDiscontinuousWebTop { get; set; }
    /// <summary>Connection angle leg and thickness (L5x5x1/2).</summary>
    public double AngleLeg { get; set; } = In(5);
    public double AngleThickness { get; set; } = In(0.5);
    /// <summary>Length of the angles at the continuing girders and at the discontinuous girder; 0 = legacy (web depth - 4"),
    /// angles are centred in the cross girder web.</summary>
    public double AngleLength { get; set; }
    public double EndAngleLength { get; set; }
    /// <summary>Bolt pitch in each angle leg (single line); 0 = no bolts or holes.</summary>
    public double AngleBoltPitch { get; set; }
    /// <summary>Angle end to the first bolt.</summary>
    public double AngleBoltEndDistance { get; set; } = In(2);
    /// <summary>Heel (corner against the other member) to the bolt line, in both legs.</summary>
    public double AngleBoltGage { get; set; } = In(3);
    /// <summary>Bolt diameter of the angle connections; 0 = the bridge's bolt diameter.</summary>
    public double BoltDiameter { get; set; }

    /// <summary>Vertical (near side) stiffeners flanking each web opening: plate width (outstand from the web face) and thickness; width 0 = none.</summary>
    public double StiffenerWidth { get; set; }
    public double StiffenerThickness { get; set; }
    /// <summary>Opening edge to the stiffener centerline.</summary>
    public double StiffenerFromOpening { get; set; } = In(6);
    /// <summary>Web opening beside the discontinuous girder (one between each continuing girder and the discontinuous
    /// girder, centred between their centerlines); width 0 = none.</summary>
    public double OpeningWidth { get; set; }
    public double OpeningHeight { get; set; }
    public double OpeningRadius { get; set; } = In(6);
    /// <summary>Top of the cross girder top flange to the centerline of the upper opening edge plate.</summary>
    public double OpeningPlateFromTop { get; set; }
    /// <summary>Horizontal opening edge plates (far side) above and below each opening: outstand width and thickness; width 0 = none.</summary>
    public double OpeningPlateWidth { get; set; }
    public double OpeningPlateThickness { get; set; }
    /// <summary>Edge plate centerline to the opening edge.</summary>
    public double OpeningPlateToOpening { get; set; } = In(0.5);
    /// <summary>Edge plates stop this far short of the angle toes (assumed from the plan elevation, not dimensioned).</summary>
    public double OpeningPlateEndClear { get; set; } = In(3);
    /// <summary>Fillet weld sizes: web to flanges, stiffeners to web (0 = not modeled).</summary>
    public double WebFlangeWeld { get; set; }
    public double StiffenerWeld { get; set; }
    public CrossGirderSection Clone() => (CrossGirderSection)MemberwiseClone();
}

public enum GirderPlanElementKind { Line, ArcLeft, ArcRight }

/// <summary>One tangent or circular arc of an as-designed girder plan geometry (see <see cref="GirderPlanGeometry"/>).</summary>
public sealed class GirderPlanElement
{
    public GirderPlanElementKind Kind { get; set; } = GirderPlanElementKind.Line;
    /// <summary>Arc radius (ignored for lines).</summary>
    public double Radius { get; set; }
    /// <summary>Length along the element. The LAST element is extended past this when the girder runs longer.</summary>
    public double Length { get; set; }
    public GirderPlanElement Clone() => (GirderPlanElement)MemberwiseClone();
}

/// <summary>
/// Optional as-designed plan geometry of one girder (default: none = offset curve from the alignment, flared linearly
/// between the supports' <see cref="GirderLayoutDefinition.OffsetsAtSupports"/>): a start point in alignment
/// station/offset, a start direction and a chain of tangents and arcs (each tangent to the previous one), e.g. the plan's
/// "P.C. STA ..., R = ..." girder radius data. The first element is extended backwards and the last forwards as needed.
/// Bearings are where the girder crosses the support lines; everything else follows from those curves.
/// </summary>
public sealed class GirderPlanGeometry
{
    /// <summary>Girder index (0 = G1).</summary>
    public int Girder { get; set; }
    public double StartStation { get; set; }
    /// <summary>Offset of the start point from the alignment (right positive).</summary>
    public double StartOffset { get; set; }
    /// <summary>Direction of travel at the start, in degrees from the alignment tangent at StartStation (positive = toward the right).</summary>
    public double StartDirectionDegrees { get; set; }
    public List<GirderPlanElement> Elements { get; set; } = new();
    public GirderPlanGeometry Clone() => new()
    {
        Girder = Girder, StartStation = StartStation, StartOffset = StartOffset, StartDirectionDegrees = StartDirectionDegrees,
        Elements = Elements.Select(e => e.Clone()).ToList(),
    };
}

public sealed class GirderLayoutDefinition
{
    /// <summary>As-designed plan geometry per girder (optional, see <see cref="GirderPlanGeometry"/>).</summary>
    public List<GirderPlanGeometry> PlanGeometry { get; set; } = new();
    /// <summary>As-designed camber / deflection data per girder (optional, see <see cref="GirderCamberDefinition"/>); blocking is computed from it.</summary>
    public List<GirderCamberDefinition> Camber { get; set; } = new();
    /// <summary>Girders that end on cross girders inside the bridge (e.g. a widening that tapers out).</summary>
    public List<GirderTermination> Terminations { get; set; } = new();
    /// <summary>Girders that begin on an auxiliary support line (see <see cref="GirderBeginning"/>).</summary>
    public List<GirderBeginning> Beginnings { get; set; } = new();
    public CrossGirderSection CrossGirder { get; set; } = new();
    public int Count { get; set; } = 5;

    /// <summary>Spacing between adjacent girders (Count − 1 values), measured normal to the alignment.</summary>
    public List<double> Spacings { get; set; } = Enumerable.Repeat(Ft(9), 4).ToList();

    /// <summary>Offset of girder 1 (leftmost) from the alignment, right positive.</summary>
    public double FirstGirderOffset { get; set; } = Ft(-18);

    /// <summary>
    /// Optional per-support girder offsets for flared layouts: [support][girder], right positive.
    /// When null, offsets come from <see cref="FirstGirderOffset"/> + <see cref="Spacings"/> everywhere.
    /// Offsets vary linearly with station between supports.
    /// </summary>
    public List<List<double>>? OffsetsAtSupports { get; set; }

    /// <summary>
    /// OPTIONAL per-span layouts (see <see cref="GirderSpanLayout"/>): straight chord girders with their own offsets at the begin
    /// and end line of each listed span. Null / empty = the offset curves above for every span (default).
    /// </summary>
    public List<GirderSpanLayout>? SpanLayouts { get; set; }

    /// <summary>The span layout of a span, or null.</summary>
    public GirderSpanLayout? SpanLayoutOf(int span) => SpanLayouts?.FirstOrDefault(l => l.Span == span);

    public GirderPlanLayout PlanLayout { get; set; } = GirderPlanLayout.CurvedParallel;

    public GirderSection Section { get; set; } = new();

    /// <summary>Steel plate girders (default) or TxDOT prestressed concrete I-girders.</summary>
    public GirderKind Kind { get; set; } = GirderKind.SteelPlate;
    public PrestressedBeamDefinition Prestressed { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public bool IsPrestressed => Kind == GirderKind.TxDotPrestressed;
    /// <summary>OPTIONAL steel tub (box) girders (<see cref="TubGirderDefinition"/>); null = steel I-girders. Omitted from the .sbm when null.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TubGirderDefinition? Tub { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool IsTub => Tub != null && Kind == GirderKind.SteelPlate;
    /// <summary>OPTIONAL plan names of the girders / beam lines, left to right (I-80 Unit 2 "A".."K", Woodbury beam lines "D".."H");
    /// used by the sheets only. Null or a missing / empty entry = "G1", "G2", ... Omitted from the .sbm when null.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PlanNames { get; set; }

    public IReadOnlyList<double> UniformOffsets()
    {
        var offs = new List<double>(Count) { FirstGirderOffset };
        for (int i = 1; i < Count; i++)
            offs.Add(offs[^1] + (i - 1 < Spacings.Count ? Spacings[i - 1] : Spacings.LastOrDefault()));
        return offs;
    }

    public GirderLayoutDefinition Clone() => new()
    {
        Count = Count,
        Spacings = Spacings.ToList(),
        FirstGirderOffset = FirstGirderOffset,
        OffsetsAtSupports = OffsetsAtSupports?.Select(l => l.ToList()).ToList(),
        SpanLayouts = SpanLayouts?.Select(l => l.Clone()).ToList(),
        PlanLayout = PlanLayout,
        Section = Section.Clone(),
        Kind = Kind,
        Prestressed = Prestressed.Clone(),
        Terminations = Terminations.Select(t => t.Clone()).ToList(),
        PlanGeometry = PlanGeometry.Select(t => t.Clone()).ToList(),
        Camber = Camber.Select(t => t.Clone()).ToList(),
        Beginnings = Beginnings.Select(t => t.Clone()).ToList(),
        CrossGirder = CrossGirder.Clone(),
        Tub = Tub?.Clone(),
        PlanNames = PlanNames?.ToList(),
    };
}

/// <summary>Custom = the section given point by point (<see cref="BarrierDefinition.Profile"/>, e.g. the Iowa aesthetic barrier).</summary>
public enum BarrierType { TxDotSstr, FShape, SingleSlope, None, Custom }

public enum TransverseBarOrientation
{
    /// <summary>Parallel to the skew when the skew is at or below the limit, otherwise normal to the alignment.</summary>
    Auto,
    NormalToAlignment,
    ParallelToSkew,
}

/// <summary>Cast-in-place deck reinforcement: top and bottom mats, 60 ft maximum bars with lap splices, and additional
/// longitudinal steel over interior supports so total longitudinal steel is at least 1% (AASHTO 6.10.1.7).</summary>
public sealed class DeckReinforcementDefinition
{
    public bool Enabled { get; set; } = true;
    public double ConcreteStrengthKsi { get; set; } = 4.0;
    public double SteelYieldKsi { get; set; } = 60;
    public bool EpoxyCoated { get; set; } = true;
    public double TopCover { get; set; } = In(2.5);
    public double BottomCover { get; set; } = In(1.25);
    /// <summary>Clear cover at deck edges and ends.</summary>
    public double SideCover { get; set; } = In(2);
    public TransverseBarOrientation TransverseOrientation { get; set; } = TransverseBarOrientation.Auto;
    public double SkewLimitDegrees { get; set; } = 15;
    public string TopTransverseSize { get; set; } = "#5";
    public double TopTransverseSpacing { get; set; } = In(6);
    public string BottomTransverseSize { get; set; } = "#5";
    public double BottomTransverseSpacing { get; set; } = In(6);
    public string TopLongitudinalSize { get; set; } = "#4";
    public double TopLongitudinalSpacing { get; set; } = In(9);
    public string BottomLongitudinalSize { get; set; } = "#5";
    public double BottomLongitudinalSpacing { get; set; } = In(9);
    /// <summary>Maximum bar length (stock length); longer runs are lap spliced.</summary>
    public double MaxBarLength { get; set; } = Ft(60);
    /// <summary>Lap splice length override; 0 = AASHTO Class B tension lap computed per bar.</summary>
    public double LapOverride { get; set; }
    /// <summary>Additional longitudinal bars over interior supports to reach 1% of the deck cross-section.</summary>
    public bool NegativeMomentSteel { get; set; } = true;
    public string NegativeMomentSize { get; set; } = "#5";
    /// <summary>Extent of the negative moment region each side of an interior support, as a fraction of the adjacent span.</summary>
    public double NegativeRegionFraction { get; set; } = 0.25;
    public double RequiredSteelRatio { get; set; } = 0.01;
    /// <summary>
    /// Straight bars whose lengths differ by no more than this share one mark, cut to the shortest length (cover can only
    /// increase). 0 = every inch is its own mark.
    /// </summary>
    public double MarkLengthTolerance { get; set; } = In(2);
    /// <summary>OPTIONAL plan-driven layout (zones + the plan bar list); null = the generic layout above. Sizes/covers above still apply to it.</summary>
    public DeckPlanReinforcement? Plan { get; set; }
    public DeckReinforcementDefinition Clone()
    {
        var c = (DeckReinforcementDefinition)MemberwiseClone();
        c.Plan = Plan?.Clone();
        return c;
    }
}

public sealed class BarrierDefinition
{
    public BarrierType Type { get; set; } = BarrierType.TxDotSstr;
    /// <summary>TxDOT SSTR rail reinforcing (Bars S, U, R) per standard RL-SSTR.</summary>
    public bool Reinforcement { get; set; } = true;
    public bool EpoxyCoated { get; set; } = true;
    /// <summary>Spacing of Bars S and U (6" max per the standard).</summary>
    public double StirrupSpacing { get; set; } = In(6);
    public double Height { get; set; } = In(32);
    public double BaseWidth { get; set; } = In(16);
    public double TopWidth { get; set; } = In(6);
    public bool Left { get; set; } = true;
    public bool Right { get; set; } = true;

    // ---- OPTIONAL custom barrier (Type = Custom); omitted from the .sbm when not used
    /// <summary>Section of a <see cref="BarrierType.Custom"/> barrier (see <see cref="BarrierProfilePoint"/>).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<BarrierProfilePoint>? Profile { get; set; }
    /// <summary>Name of the section (IFC object type, reports), e.g. "Iowa aesthetic barrier".</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileName { get; set; }
    /// <summary>The section stands PERPENDICULAR to the top of deck (rotated with the cross slope) instead of plumb (Iowa "barrier rail orientation detail").</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool PerpendicularToDeck { get; set; }
    /// <summary>Custom barrier: length beyond the begin / end deck-end line, measured along the toe (gutter) line from where the deck end crosses it; the barrier
    /// end is then square to the deck edge (e.g. "abut. diaph. ext." over an integral abutment). NaN = the end follows the deck end line.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? BeginExtension { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? EndExtension { get; set; }
    /// <summary>Custom barrier: lengths with another section (bump-outs), see <see cref="BarrierZone"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<BarrierZone>? Zones { get; set; }
    /// <summary>Plan reference of the custom section / lengths.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }
    /// <summary>Plan length of the barrier along the toe (gutter) line (bid length), for the check; null = none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? PlanLength { get; set; }

    public BarrierDefinition Clone()
    {
        var c = (BarrierDefinition)MemberwiseClone();
        c.Profile = Profile?.Select(p => p.Clone()).ToList();
        c.Zones = Zones?.Select(z => z.Clone()).ToList();
        return c;
    }
}

/// <summary>Deck edge offset at a station (variable-width decks; linear between points, held beyond the ends).</summary>
public sealed class DeckEdgePoint
{
    public double Station { get; set; }
    public double Offset { get; set; }
    public DeckEdgePoint Clone() => (DeckEdgePoint)MemberwiseClone();
}

/// <summary>
/// OPTIONAL strip along each deck edge placed LEVEL where the finished grade falls toward the barrier (project-specific: Iowa DOT
/// plans note "where the deck slopes downward towards the barrier rail, the deck under the barrier rail shall be placed level ...
/// where it slopes downward away from the barrier rail, along the same cross slope as the deck"). Width is measured inboard from the
/// deck edge (follows a variable deck width); the strip starts at the "gutter line" = edge - Width. Per side and station, by the sign of
/// that side's cross slope at the gutter line: it falls toward the edge (outward slope &lt; 0) -> the strip is level at the gutter
/// elevation; otherwise the cross-slope plane continues. Applies only when the gutter line lies on the same side of the pivot as the edge.
/// </summary>
public sealed class DeckEdgeStrip
{
    public double Width { get; set; }
    public bool LowSideLevel { get; set; } = true;
    public DeckEdgeStrip Clone() => (DeckEdgeStrip)MemberwiseClone();
}

public sealed class DeckDefinition
{
    /// <summary>Optional level strip at the deck edges (see <see cref="DeckEdgeStrip"/>); null = the cross-slope plane to the edge.</summary>
    public DeckEdgeStrip? EdgeStrip { get; set; }

    /// <summary>
    /// Finished grade (top of deck) at a station / offset: the alignment's profile + cross slope, with the optional
    /// <see cref="EdgeStrip"/> applied. The gutter-line offset at which a level strip took over is returned in <paramref name="levelAt"/>
    /// (NaN when the plane is used).
    /// </summary>
    public double FinishedElevation(Alignment a, double station, double offset) => FinishedElevation(a, station, offset, out _);

    public double FinishedElevation(Alignment a, double station, double offset, out double levelAt)
    {
        levelAt = double.NaN;
        if (EdgeStrip is { LowSideLevel: true, Width: > 0 } es && a.CrossSlope.Rows.Count > 0)
        {
            double piv = a.CrossSlope.PivotOffsetAt(station);
            var (l, r) = a.CrossSlope.SidesAt(station);
            double gr = RightAt(station) - es.Width, gl = LeftAt(station) + es.Width;
            if (offset > gr && gr > piv && r.SlopeAt(gr - piv) < 0) levelAt = gr;
            else if (offset < gl && gl < piv && l.SlopeAt(piv - gl) < 0) levelAt = gl;
        }
        return a.ElevationAt(station, double.IsNaN(levelAt) ? offset : levelAt);
    }

    /// <summary>Left deck edge offset from the alignment (right positive, so usually negative).</summary>
    public double LeftEdgeOffset { get; set; } = Ft(-21.5);
    public double RightEdgeOffset { get; set; } = Ft(21.5);
    /// <summary>Variable width: left / right edge offsets by station (null or empty = the constant offsets above).</summary>
    public List<DeckEdgePoint>? LeftEdgeTable { get; set; }
    public List<DeckEdgePoint>? RightEdgeTable { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool VariableWidth => LeftEdgeTable is { Count: > 0 } || RightEdgeTable is { Count: > 0 };

    private static double Interp(List<DeckEdgePoint>? t, double fallback, double station)
    {
        if (t is not { Count: > 0 }) return fallback;
        if (station <= t[0].Station) return t[0].Offset;
        if (station >= t[^1].Station) return t[^1].Offset;
        int i = 1;
        while (t[i].Station < station) i++;
        var a = t[i - 1];
        var b = t[i];
        double s = (station - a.Station) / Math.Max(1e-9, b.Station - a.Station);
        return a.Offset + (b.Offset - a.Offset) * s;
    }

    /// <summary>Left / right edge offset at a station.</summary>
    public double LeftAt(double station) => Interp(LeftEdgeTable, LeftEdgeOffset, station);
    public double RightAt(double station) => Interp(RightEdgeTable, RightEdgeOffset, station);
    /// <summary>Extremes over the whole deck (for views and extents).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double MinLeft => LeftEdgeTable is { Count: > 0 } t ? Math.Min(LeftEdgeOffset, t.Min(p => p.Offset)) : LeftEdgeOffset;
    [System.Text.Json.Serialization.JsonIgnore]
    public double MaxRight => RightEdgeTable is { Count: > 0 } t ? Math.Max(RightEdgeOffset, t.Max(p => p.Offset)) : RightEdgeOffset;
    public double Thickness { get; set; } = In(8.5);
    /// <summary>Haunch: top of web to bottom of deck, at the girder centerline (top flange sits within it).</summary>
    public double Haunch { get; set; } = In(3);
    public double OverhangThickness { get; set; } = In(9);
    public bool StayInPlaceForms { get; set; } = true;
    /// <summary>SIP form depth (corrugation depth plus support angle).</summary>
    public double SipFormDepth { get; set; } = In(2);
    public BarrierDefinition Barrier { get; set; } = new();
    public DeckReinforcementDefinition Reinforcement { get; set; } = new();
    public DeckDefinition Clone() => new()
    {
        LeftEdgeOffset = LeftEdgeOffset, RightEdgeOffset = RightEdgeOffset, Thickness = Thickness, Haunch = Haunch,
        LeftEdgeTable = LeftEdgeTable?.Select(p => p.Clone()).ToList(), RightEdgeTable = RightEdgeTable?.Select(p => p.Clone()).ToList(),
        OverhangThickness = OverhangThickness, StayInPlaceForms = StayInPlaceForms, SipFormDepth = SipFormDepth,
        Barrier = Barrier.Clone(), Reinforcement = Reinforcement.Clone(), EdgeStrip = EdgeStrip?.Clone(),
    };
}

public enum CrossFrameType { Auto, XFrame, KFrame, InvertedKFrame, Diaphragm }

public enum CrossFrameOrientation
{
    /// <summary>Parallel to skew when skew ≤ 20°, otherwise normal to girders (AASHTO 6.7.4.2).</summary>
    Auto,
    NormalToGirders,
    ParallelToSkew,
}

public enum CrossFrameArrangement
{
    /// <summary>Contiguous lines when skew ≤ 20°, otherwise staggered per bay.</summary>
    Auto,
    Contiguous,
    Staggered,
}

public sealed class CrossFrameDefinition
{
    public CrossFrameType IntermediateType { get; set; } = CrossFrameType.Auto;
    public CrossFrameType SupportType { get; set; } = CrossFrameType.Auto;
    public CrossFrameOrientation Orientation { get; set; } = CrossFrameOrientation.Auto;
    public CrossFrameArrangement Arrangement { get; set; } = CrossFrameArrangement.Auto;
    public double MaxSpacing { get; set; } = Ft(25);
    /// <summary>Bays wider than this get no cross frames (0 = no limit).</summary>
    public double MaxBayWidth { get; set; }
    /// <summary>Minimum distance along the girder from a bearing to an intermediate cross frame.</summary>
    public double MinDistanceFromBearing { get; set; } = Ft(3);
    /// <summary>Minimum distance along the girder from a field splice centerline to an intermediate cross frame. Frames
    /// closer than this are reported as conflicts (0 = no check).</summary>
    public double MinDistanceFromSplice { get; set; } = Ft(3);
    /// <summary>Opt-in: move conflicting intermediate frames to the nearest clear spot. Every move is reported. Off by
    /// default — a conflict usually means the frame or splice locations disagree with the design.</summary>
    public bool MoveClearOfSplices { get; set; }
    /// <summary>Skew limit (degrees) used by the Auto orientation/arrangement rules.</summary>
    public double SkewLimitDegrees { get; set; } = 20;
    /// <summary>Bay width / girder depth ratio above which Auto selects K-frames instead of X-frames.</summary>
    public double KFrameAspectRatio { get; set; } = 1.5;

    // ---- Member and connection detail (single angles welded to gussets, gussets bolted to connection stiffeners)
    public string ChordAngle { get; set; } = "L4x4x3/8";
    public string DiagonalAngle { get; set; } = "L4x4x3/8";
    public string SupportChordAngle { get; set; } = "L5x5x3/8";
    public string SupportDiagonalAngle { get; set; } = "L4x4x3/8";
    /// <summary>Channel used for the Diaphragm type.</summary>
    public string DiaphragmChannel { get; set; } = "C15x33.9";
    /// <summary>Top work point below the top of web.</summary>
    public double TopWorkPointOffset { get; set; } = In(6);
    /// <summary>Bottom work point above the top of the bottom flange.</summary>
    public double BottomWorkPointOffset { get; set; } = In(6);
    public double GussetThickness { get; set; } = In(0.5);
    /// <summary>Clear distance from the edge of the connection stiffener to the end of the angle.</summary>
    public double MemberClearance { get; set; } = In(1);
    /// <summary>Length of angle lapped onto the gusset (weld length along heel and toe).</summary>
    public double WeldLap { get; set; } = In(6);
    public double MemberWeldSize { get; set; } = In(0.25);

    // ---- As-designed frames (plans' cross frame types and framing plan). Optional: without styles / lines the
    //      generator's automatic layout and the members above are used.
    /// <summary>Named frame types (plans' "Type 1" ...).</summary>
    public List<CrossFrameStyle> Styles { get; set; } = new();
    /// <summary>Style of intermediate frames not given otherwise ("" = the members above).</summary>
    public string IntermediateStyle { get; set; } = "";
    /// <summary>Style of support frames per support name ("none" = no frames on that support line); missing = <see cref="SupportStyle"/>.</summary>
    public Dictionary<string, string> SupportStyles { get; set; } = new();
    /// <summary>Style of single bays of a support line, by support name then bay (left girder index, as <see cref="CrossFrameLine.Bays"/>);
    /// bays not listed use <see cref="SupportStyles"/> (plans: Type 6 in the north bay of Piers 14WB / 15WB, Type 5 elsewhere).</summary>
    public Dictionary<string, Dictionary<int, string>> SupportBayStyles { get; set; } = new();
    /// <summary>Default style of support frames ("" = the members above, "none" = no support frames).</summary>
    public string SupportStyle { get; set; } = "";
    /// <summary>
    /// Explicit intermediate frame lines (plans' framing plan). Spans crossed by lines get exactly these frames instead of
    /// the automatic spacing: each line gets a frame in every bay whose two girders it crosses within the same span.
    /// </summary>
    public List<CrossFrameLine> Lines { get; set; } = new();
    /// <summary>Frames whose two ends are on DIFFERENT support lines (plans: the E-F end frame between girder E at Pier 6A and
    /// girder F at Pier 5WB).</summary>
    public List<CrossFrameLink> Links { get; set; } = new();

    public CrossFrameStyle? FindStyle(string name) => Styles.FirstOrDefault(s => s.Name == name);

    public CrossFrameDefinition Clone()
    {
        var c = (CrossFrameDefinition)MemberwiseClone();
        c.Styles = Styles.Select(s => s.Clone()).ToList();
        c.SupportStyles = new Dictionary<string, string>(SupportStyles);
        c.SupportBayStyles = SupportBayStyles.ToDictionary(k => k.Key, k => new Dictionary<int, string>(k.Value));
        c.Lines = Lines.Select(l => l.Clone()).ToList();
        c.Links = Links.Select(l => l.Clone()).ToList();
        return c;
    }
}

public enum CrossFrameStyleKind
{
    /// <summary>Chevron: diagonals from the bottom corners to the top chord centre.</summary>
    InvertedK,
    /// <summary>Diagonals from the top corners to the bottom chord centre.</summary>
    KFrame,
    XFrame,
    /// <summary>Bottom chord (strut) only.</summary>
    Strut,
    /// <summary>Built-up plate diaphragm: web plate with top and bottom flange plates, bolted to the connection plates.</summary>
    PlateDiaphragm,
}

/// <summary>A cross frame type as detailed on the plans.</summary>
public sealed class CrossFrameStyle
{
    public string Name { get; set; } = "";
    public CrossFrameStyleKind Kind { get; set; } = CrossFrameStyleKind.InvertedK;
    /// <summary>Top chord: angle ("L6x6x9/16") or rolled beam ("W12x40", web in the frame plane, bolted through the web); "" = none.</summary>
    public string TopChord { get; set; } = "";
    public string Diagonal { get; set; } = "";
    /// <summary>Bottom chord: angle, channel ("MC10x33.6", web on the gusset) or back-to-back channels ("2-MC8x20", straddling the gussets).</summary>
    public string BottomChord { get; set; } = "";
    public double GussetThickness { get; set; } = In(0.625);
    /// <summary>Bolt diameter (0 = the bridge's bolts); holes are 1/16" larger.</summary>
    public double BoltDiameter { get; set; }
    /// <summary>Bolts per gusset connection to the stiffener (0 = the bridge's), with pitch / edge distance (0 = the bridge's).</summary>
    public int BoltsPerConnection { get; set; }
    public double BoltPitch { get; set; }
    public double BoltEdgeDistance { get; set; }
    /// <summary>Bolt columns on the connection plate (plans: 2 columns 3 1/2" apart on a 7" plate) and their gage.</summary>
    public int BoltColumns { get; set; } = 1;
    public double BoltGage { get; set; } = In(3.5);
    /// <summary>Bolt rows at the top gussets / a rolled-beam top chord (0 = <see cref="BoltsPerConnection"/>).</summary>
    public int TopBoltRows { get; set; }
    /// <summary>Shift of the bottom gusset bolt group centre from the bottom work point (+ = up), e.g. to keep a taller group
    /// clear of the bottom flange.</summary>
    public double BottomBoltOffset { get; set; }
    /// <summary>Pitch of the bottom gusset bolt rows (0 = <see cref="BoltPitch"/>); plans: I-80 abutment frames 6".</summary>
    public double BottomBoltPitch { get; set; }
    /// <summary>Plan items of this type that are NOT dimensioned (tie plate length, stud pitch ...): each is reported as an
    /// UNRESOLVED warning once per bridge in which the style is used. Nothing is invented for them.</summary>
    public List<string> Unresolved { get; set; } = new();
    /// <summary>Top work point below the top of web / bottom work point above the bottom flange (NaN = the definition's).</summary>
    public double TopWorkPointOffset { get; set; } = double.NaN;
    public double BottomWorkPointOffset { get; set; } = double.NaN;
    /// <summary>Tie plates across the bottom flanges of back-to-back channels: thickness, length along the chord, centres
    /// measured from the bay centre (plans: ±1'-4).</summary>
    public double TiePlateThickness { get; set; } = In(0.5);
    public double TiePlateLength { get; set; } = In(8);
    public List<double> TiePlateOffsets { get; set; } = new();
    /// <summary>Plate diaphragm: web plate thickness, flange plates (width x thickness), bolts per end (column on the
    /// connection plate) at a pitch; depth = top to bottom work point.</summary>
    public double WebPlateThickness { get; set; } = In(0.5);
    public double FlangePlateWidth { get; set; } = In(12);
    public double FlangePlateThickness { get; set; } = In(0.75);
    public int DiaphragmBolts { get; set; } = 11;
    /// <summary>Pitch of the diaphragm bolt column; 0 = equal spaces over the web plate with <see cref="DiaphragmBoltEnd"/> at both ends.</summary>
    public double DiaphragmBoltPitch { get; set; } = In(6);
    public double DiaphragmBoltEnd { get; set; } = In(3);
    public CrossFrameStyle Clone()
    {
        var c = (CrossFrameStyle)MemberwiseClone();
        c.TiePlateOffsets = TiePlateOffsets.ToList();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}

/// <summary>A frame between girder <see cref="LeftGirder"/> at support line <see cref="LeftSupport"/> and girder
/// <see cref="RightGirder"/> at support line <see cref="RightSupport"/> (names of supports or auxiliary supports); the frame
/// plane is the vertical plane through the two CL bearing points.</summary>
public sealed class CrossFrameLink
{
    public int LeftGirder { get; set; }
    public string LeftSupport { get; set; } = "";
    public int RightGirder { get; set; }
    public string RightSupport { get; set; } = "";
    public string Style { get; set; } = "";
    public CrossFrameLink Clone() => (CrossFrameLink)MemberwiseClone();
}

/// <summary>An intermediate frame line of the framing plan.</summary>
public sealed class CrossFrameLine
{
    /// <summary>Reference girder index the distance is measured along (plans: "cross frame spacing along girder A").</summary>
    public int Girder { get; set; }
    /// <summary>Distance along the reference girder from its first CL bearing, cumulative through the spans.</summary>
    public double Distance { get; set; }
    /// <summary>Line direction: radial (perpendicular) to the alignment at the reference point (plans: "cross frames
    /// radial to CL westbound approach roadway"), or normal to the reference girder.</summary>
    public bool NormalToGirder { get; set; }
    /// <summary>Style of this line's frames ("" = the intermediate style).</summary>
    public string Style { get; set; } = "";
    /// <summary>Per-bay styles by left girder index ("none" = no frame in that bay).</summary>
    public Dictionary<int, string> BayStyles { get; set; } = new();
    /// <summary>Bays (by left girder index) that get a frame on this line; null = every bay the line crosses
    /// (plans: frames staggered through skewed supports use only some bays of each line).</summary>
    public List<int>? Bays { get; set; }
    /// <summary>Plan items of this line that are not dimensioned or that conflict between dimensions (with the plan page);
    /// raised as UNRESOLVED warnings with the line location, nothing is changed by them.</summary>
    public List<string> Unresolved { get; set; } = new();
    public CrossFrameLine Clone()
    {
        var c = (CrossFrameLine)MemberwiseClone();
        c.BayStyles = new Dictionary<int, string>(BayStyles);
        c.Bays = Bays?.ToList();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}

/// <summary>High-strength bolt specification used for field connections.</summary>
public sealed class BoltDefinition
{
    /// <summary>ASTM designation, e.g. A325 (F3125 Gr. A325) or A490.</summary>
    public string Grade { get; set; } = "A325";
    public double Diameter { get; set; } = In(0.875);
    public int BoltsPerConnection { get; set; } = 3;
    public double Pitch { get; set; } = In(3);
    public double EdgeDistance { get; set; } = In(1.5);
    public BoltDefinition Clone() => (BoltDefinition)MemberwiseClone();
}

/// <summary>Generic = vendor-designed (e.g. disc bearings designed by the manufacturer): sole plate, one body, masonry plate, anchors.</summary>
public enum BearingType { Elastomeric, Disc, Generic }

public enum BearingFixity
{
    /// <summary>Default by support type: piers fixed, abutments expansion.</summary>
    Auto,
    Fixed,
    /// <summary>Non-guided expansion (multi-directional).</summary>
    Expansion,
    /// <summary>Guided expansion (longitudinal movement only).</summary>
    GuidedExpansion,
}

public sealed class BearingDefinition
{
    public BearingType Type { get; set; } = BearingType.Elastomeric;

    // ---- Disc bearing (pot-type alternative): sole plate / upper bearing plate (+ stainless & PTFE for expansion)
    //      / polyether-urethane disc with shear-restriction pin / lower bearing (masonry) plate / anchor bolts.
    public double DiscDiameter { get; set; } = In(12);
    public double DiscHeight { get; set; } = In(1.75);
    public double UpperPlateLength { get; set; } = In(18);
    public double UpperPlateWidth { get; set; } = In(18);
    public double UpperPlateThickness { get; set; } = In(1.5);
    /// <summary>Extra upper-plate length along the girder for expansion bearings (movement + PTFE coverage).</summary>
    public double ExpansionPlateExtension { get; set; } = In(6);
    public double PtfeThickness { get; set; } = In(0.125);
    public double MasonryPlateLength { get; set; } = In(22);
    public double MasonryPlateWidth { get; set; } = In(30);
    public double MasonryPlateThickness { get; set; } = In(1.75);
    /// <summary>Preformed masonry pad under the masonry plate (e.g. 1/8"); 0 = none.</summary>
    public double MasonryPadThickness { get; set; }

    // ---- Generic (vendor-designed) bearing: the plans give the assumed total height, the sole plate and the anchor bolts;
    //      the bearing itself is shown as one body between the sole plate and the masonry plate.
    /// <summary>
    /// Generic bearing: assumed total height at the CL bearing, bottom of flange (top of bearing) to bottom of the masonry
    /// pad (plans' "bearing height", used to set the pedestal tops).
    /// </summary>
    public double GenericHeight { get; set; } = In(9);
    /// <summary>Generic bearing body plan size (along the girder x across).</summary>
    public double GenericBodyLength { get; set; } = In(16);
    public double GenericBodyWidth { get; set; } = In(16);
    /// <summary>Anchor bolt spacing along the girder (plans' 2 x "Y"); 0 = from the masonry plate length and edge distance.</summary>
    public double AnchorSpacingAlong { get; set; }
    /// <summary>Anchor bolt spacing across the girder (plans' 2 x "X"); 0 = from the masonry plate width and edge distance.</summary>
    public double AnchorSpacingAcross { get; set; }
    /// <summary>Masonry plate edge beyond the anchor bolts at fixed bearings; 0 = <see cref="AnchorEdgeDistance"/>.</summary>
    public double AnchorEdgeDistanceFixed { get; set; }

    public double EdgeDistance(BearingFixity fix) => fix == BearingFixity.Fixed && AnchorEdgeDistanceFixed > 0 ? AnchorEdgeDistanceFixed : AnchorEdgeDistance;
    /// <summary>Masonry plate (along, across): from the anchor spacing + edges when the spacing is given.</summary>
    public (double Length, double Width) MasonryPlate(BearingFixity fix) =>
        (AnchorSpacingAlong > 0 ? AnchorSpacingAlong + 2 * EdgeDistance(fix) : MasonryPlateLength,
         AnchorSpacingAcross > 0 ? AnchorSpacingAcross + 2 * EdgeDistance(fix) : MasonryPlateWidth);

    /// <summary>Bottom of the bearing in plan (along, across): masonry plate, or the elastomeric pad.</summary>
    public (double Length, double Width) Footprint(BearingFixity fix) => Type == BearingType.Elastomeric ? (PadLength, PadWidth) : MasonryPlate(fix);

    /// <summary>Sole plate length along the girder.</summary>
    public double SolePlateLength => Type switch
    {
        BearingType.Elastomeric => PadLength + 2 * SolePlateProjection,
        BearingType.Generic => GenericBodyLength,
        _ => UpperPlateLength,
    };
    /// <summary>Sole plate thickness at the CL bearing (tapered with the girder grade, bottom level).</summary>
    public double SoleAtCl(double grade) => SolePlateMinThickness + Math.Abs(grade) * SolePlateLength / 2;
    /// <summary>
    /// Generic bearing body height = assumed total − sole plate at CL − masonry plate − pad (shared by BearingDetailer,
    /// ElevationCheck and BearingViews; negative = the given parts exceed the assumed height).
    /// </summary>
    public double GenericBodyHeight(double grade) => GenericHeight - SoleAtCl(grade) - MasonryPlateThickness - MasonryPadThickness;
    public double ShearPinDiameter { get; set; } = In(3);
    public double GuideBarWidth { get; set; } = In(1.5);
    public double GuideBarHeight { get; set; } = In(2.5);
    public int AnchorBoltCount { get; set; } = 4;
    public double AnchorBoltDiameter { get; set; } = In(1.25);
    public string AnchorBoltGrade { get; set; } = "F1554 Gr. 36";
    public double AnchorBoltEmbedment { get; set; } = In(18);
    /// <summary>Anchor bolt hole oversize in the masonry plate.</summary>
    public double AnchorHoleOversize { get; set; } = In(0.5);
    public double AnchorEdgeDistance { get; set; } = In(2.5);

    /// <summary>Pad dimension along the girder.</summary>
    public double PadLength { get; set; } = In(14);
    /// <summary>Pad dimension across the girder.</summary>
    public double PadWidth { get; set; } = In(20);
    public double PadHeight { get; set; } = In(3.5);
    /// <summary>Minimum sole plate thickness (at its low end; the plate is tapered to bear level).</summary>
    public double SolePlateMinThickness { get; set; } = In(1.5);
    /// <summary>Sole plate projection beyond the pad, each side.</summary>
    public double SolePlateProjection { get; set; } = In(1);
    public BearingDefinition Clone() => (BearingDefinition)MemberwiseClone();
}

public enum StiffenerSide { Both, Left, Right, Interior }

public sealed class StiffenerDefinition
{
    public PlateSize BearingStiffener { get; set; } = new(In(7), In(0.875));
    public PlateSize ConnectionPlate { get; set; } = new(In(7), In(0.5));
    /// <summary>Maximum transverse stiffener spacing; 0 = no intermediate stiffeners.</summary>
    public double IntermediateMaxSpacing { get; set; } = 0;
    public PlateSize IntermediateStiffener { get; set; } = new(In(6), In(0.5));
    /// <summary>Which web face gets intermediate stiffeners. Interior = face toward the bridge centerline.</summary>
    public StiffenerSide IntermediateSide { get; set; } = StiffenerSide.Interior;
    /// <summary>Corner clip at the web-to-flange corners (clears the flange-to-web weld).</summary>
    public double CornerClip { get; set; } = In(1.5);
    /// <summary>Gap between intermediate stiffeners and the bottom (tension) flange.</summary>
    public double IntermediateBottomGap { get; set; } = In(2);
    public double WeldSize { get; set; } = In(0.25);
    public double FlangeToWebWeld { get; set; } = In(5.0 / 16);
    public StiffenerDefinition Clone() => new()
    {
        BearingStiffener = BearingStiffener.Clone(), ConnectionPlate = ConnectionPlate.Clone(),
        IntermediateMaxSpacing = IntermediateMaxSpacing, IntermediateStiffener = IntermediateStiffener.Clone(),
        IntermediateSide = IntermediateSide, CornerClip = CornerClip, IntermediateBottomGap = IntermediateBottomGap,
        WeldSize = WeldSize, FlangeToWebWeld = FlangeToWebWeld,
    };
}

/// <summary>
/// Bolted field splice detail (NSBA-style): flange splices with an outside plate the full (narrower) flange width and
/// two inside plates, web splice with two plates; fillers where plate thicknesses differ. Bolt quantities are design
/// inputs. Defaults follow common NSBA practice (7/8" A325, 3" pitch, 1-1/2" edge distance).
/// </summary>
public sealed class FieldSpliceDetail
{
    public double FlangeOuterPlateThickness { get; set; } = In(0.625);
    public double FlangeInnerPlateThickness { get; set; } = In(0.625);
    public double WebPlateThickness { get; set; } = In(0.4375);
    /// <summary>Longitudinal bolt lines on each side of the web (across the flange).</summary>
    public int FlangeBoltLinesPerSide { get; set; } = 2;
    /// <summary>Bolt rows (transverse lines) on each side of the joint.</summary>
    public int TopFlangeBoltRows { get; set; } = 3;
    public int BottomFlangeBoltRows { get; set; } = 4;
    /// <summary>Vertical bolt columns in the web on each side of the joint.</summary>
    public int WebBoltColumns { get; set; } = 2;
    /// <summary>Transverse spacing (gage) between flange bolt lines.</summary>
    public double FlangeGage { get; set; } = In(3);
    /// <summary>Distance from the web face to the first flange bolt line (wrench/weld clearance).</summary>
    public double FirstLineFromWeb { get; set; } = In(1.75);
    /// <summary>Clear distance from the flange to the edge of the web splice plates.</summary>
    public double WebPlateFlangeClearance { get; set; } = In(1.25);
    /// <summary>Gap between the inside flange splice plates and the web/fillet weld.</summary>
    public double InnerPlateWebClearance { get; set; } = In(0.5);
    // ---- As scheduled (optional; when the patterns are given they replace the grid rules above)
    /// <summary>Bolt diameter (0 = the bridge's bolts).</summary>
    public double BoltDiameter { get; set; }
    /// <summary>First bolt row / column from the splice CL (0 = gap/2 + edge distance).</summary>
    public double FirstBoltFromJoint { get; set; }
    /// <summary>Plate edge beyond the last bolts (0 = the bolt edge distance).</summary>
    public double PlateEndDistance { get; set; }
    public FlangeSplicePattern? Top { get; set; }
    public FlangeSplicePattern? Bottom { get; set; }
    public WebSplicePattern? Web { get; set; }
    public FieldSpliceDetail Clone()
    {
        var c = (FieldSpliceDetail)MemberwiseClone();
        c.Top = Top?.Clone(); c.Bottom = Bottom?.Clone(); c.Web = Web?.Clone();
        return c;
    }
}

public sealed class StudDefinition
{
    public bool Enabled { get; set; } = true;
    public double Diameter { get; set; } = In(0.875);
    public int RowsAcross { get; set; } = 3;
    public double TransverseSpacing { get; set; } = In(4.5);
    /// <summary>Uniform pitch along the girder (0 = no studs).</summary>
    public double Pitch { get; set; } = In(12);
    /// <summary>Required penetration above the bottom of the deck (AASHTO 6.10.10.1.4: 2").</summary>
    public double Penetration { get; set; } = In(2);
    /// <summary>Minimum concrete cover over the stud head.</summary>
    public double MinCover { get; set; } = In(2);
    public double HeadDiameter { get; set; } = In(1.375);
    public double HeadHeight { get; set; } = In(0.375);
    /// <summary>Stud lengths are rounded up to this increment (after weld burn-off).</summary>
    public double LengthIncrement { get; set; } = In(0.5);
    /// <summary>Clearance from splice plates, bolt heads and girder ends.</summary>
    public double Clearance { get; set; } = In(1.5);
    public StudDefinition Clone() => (StudDefinition)MemberwiseClone();
}

public enum LateralFlange { Bottom, Top }
public enum LateralPattern { Warren, SingleDiagonal, X }

/// <summary>Plan (lateral) bracing between two girders in selected bays, single angles welded to gussets bolted to the flange.</summary>
public sealed class LateralBracingDefinition
{
    public bool Enabled { get; set; }
    public LateralFlange Flange { get; set; } = LateralFlange.Bottom;
    /// <summary>1-based bay numbers (bay 1 = G1–G2).</summary>
    public List<int> Bays { get; set; } = new() { 1 };
    /// <summary>1-based spans; empty = all spans.</summary>
    public List<int> Spans { get; set; } = new();
    public LateralPattern Pattern { get; set; } = LateralPattern.Warren;
    public string Angle { get; set; } = "L5x5x3/8";
    public double GussetThickness { get; set; } = In(0.5);
    /// <summary>Bolts per gusset-to-flange connection (two lines along the girder).</summary>
    public int BoltsPerConnection { get; set; } = 4;
    public LateralBracingDefinition Clone() => new()
    {
        Enabled = Enabled, Flange = Flange, Bays = Bays.ToList(), Spans = Spans.ToList(), Pattern = Pattern, Angle = Angle,
        GussetThickness = GussetThickness, BoltsPerConnection = BoltsPerConnection,
    };
}

public sealed class FieldSpliceDefinition
{
    /// <summary>Station at the alignment; the splice line runs radially across all girders (when <see cref="Girders"/> is empty).</summary>
    public double Station { get; set; }
    /// <summary>
    /// Per-girder locations (plans' girder data tables): distance along the girder from its CL bearing at a support
    /// (+ ahead / - back). When given, only the listed girders are spliced, each at its own location.
    /// </summary>
    public List<GirderSpliceLocation> Girders { get; set; } = new();
    /// <summary>This splice's plates and bolt pattern (plans' field splice schedule); null = the bridge's SpliceDetail.</summary>
    public FieldSpliceDetail? Detail { get; set; }
    /// <summary>Plan splice number for marks / messages (0 = list position + 1).</summary>
    public int Number { get; set; }
    public FieldSpliceDefinition Clone() => new()
    {
        Station = Station, Girders = Girders.Select(g => g.Clone()).ToList(), Detail = Detail?.Clone(), Number = Number,
    };
}

public sealed class GirderSpliceLocation
{
    public int Girder { get; set; }
    /// <summary>Support index in framing numbering (Supports, then AuxiliarySupports).</summary>
    public int Support { get; set; }
    public double Offset { get; set; }
    public GirderSpliceLocation Clone() => (GirderSpliceLocation)MemberwiseClone();
}

/// <summary>
/// Flange splice bolt pattern as scheduled: bolt rows along the girder (first row FirstBoltFromJoint from the splice CL,
/// then <see cref="Spaces"/> spaces at <see cref="Spacing"/>) on bolt lines at <see cref="LineOffsets"/> either side of the
/// girder CL; staggered = alternate rows on the outer / inner lines (narrow flanges).
/// </summary>
public sealed class FlangeSplicePattern
{
    public double OuterThickness { get; set; }
    public double OuterWidth { get; set; }
    public double InnerThickness { get; set; }
    public double InnerWidth { get; set; }
    public int Spaces { get; set; }
    public double Spacing { get; set; }
    public List<double> LineOffsets { get; set; } = new();
    public bool Staggered { get; set; }
    public FlangeSplicePattern Clone() { var c = (FlangeSplicePattern)MemberwiseClone(); c.LineOffsets = LineOffsets.ToList(); return c; }
}

/// <summary>Web splice as scheduled: columns from FirstBoltFromJoint, rows down from the top row at
/// <see cref="TopRowBelowWebTop"/> (plans' Dim. "S"), plate edges PlateEndDistance beyond the bolts.</summary>
public sealed class WebSplicePattern
{
    public double PlateThickness { get; set; }
    public int ColumnSpaces { get; set; }
    public double ColumnSpacing { get; set; }
    public int RowSpaces { get; set; }
    public double RowSpacing { get; set; }
    public double TopRowBelowWebTop { get; set; }
    public WebSplicePattern Clone() => (WebSplicePattern)MemberwiseClone();
}

public enum DrainOutlet { FreeFall, PipedToPier }

/// <summary>A deck drain at the gutter: a grated inlet through the slab and a downspout below the girders, either
/// free-falling (with an optional erosion-stone splash basin on the ground) or piped to the nearest pier.</summary>
public sealed class DeckDrainDefinition
{
    public double Station { get; set; }
    /// <summary>Left (true) or right gutter.</summary>
    public bool Left { get; set; } = true;
    /// <summary>Inlet center from the face of barrier (gutter line).</summary>
    public double FromGutter { get; set; } = In(9);
    public string Type { get; set; } = "Type A";
    public DrainOutlet Outlet { get; set; } = DrainOutlet.FreeFall;
    public bool SplashBasin { get; set; }
    public double InletLength { get; set; } = In(24);
    public double InletWidth { get; set; } = In(12);
    public double PipeDiameter { get; set; } = In(8);
    /// <summary>Free fall: downspout bottom below the bottom of the girders.</summary>
    public double DropBelowGirders { get; set; } = Ft(1);
    /// <summary>Trench-type inlet as designed on the plans (null = the simple legacy inlet above).</summary>
    public DrainTrenchDefinition? Trench { get; set; }
    public DeckDrainDefinition Clone() { var c = (DeckDrainDefinition)MemberwiseClone(); c.Trench = Trench?.Clone(); return c; }
}

/// <summary>The complete parametric definition of one steel girder superstructure.</summary>
public sealed class BridgeDefinition
{
    public string Name { get; set; } = "Bridge 1";
    public string AlignmentName { get; set; } = "";
    /// <summary>Structure this bridge is a unit of (units of one viaduct, ramps sharing its piers). Empty = standalone.</summary>
    public string Structure { get; set; } = "";
    public List<SupportDefinition> Supports { get; set; } = new();
    /// <summary>Support lines that carry bearings of girders beginning there (<see cref="GirderLayoutDefinition.Beginnings"/>)
    /// without dividing the spans. The line passes through the alignment at <c>Station</c> with <c>SkewDegrees</c>, like a
    /// support (it is the CL bearing line); framing indices continue after <see cref="Supports"/>.</summary>
    public List<SupportDefinition> AuxiliarySupports { get; set; } = new();
    /// <summary>Support by framing index: the supports, then the auxiliary supports.</summary>
    public SupportDefinition SupportAt(int index) => index < Supports.Count ? Supports[index] : AuxiliarySupports[index - Supports.Count];
    /// <summary>Bearing definition at a support (framing index): the support's override or the bridge's.</summary>
    public BearingDefinition BearingsAt(int index) => SupportAt(index).Bearings ?? Bearings;
    public GirderLayoutDefinition Girders { get; set; } = new();
    public DeckDefinition Deck { get; set; } = new();
    public CrossFrameDefinition CrossFrames { get; set; } = new();
    public StiffenerDefinition Stiffeners { get; set; } = new();
    public List<FieldSpliceDefinition> FieldSplices { get; set; } = new();
    public BoltDefinition Bolts { get; set; } = new();
    public BearingDefinition Bearings { get; set; } = new();
    /// <summary>Gap between girder ends at field splices.</summary>
    public double FieldSpliceGap { get; set; } = In(0.375);
    public FieldSpliceDetail SpliceDetail { get; set; } = new();
    public StudDefinition Studs { get; set; } = new();
    public LateralBracingDefinition LateralBracing { get; set; } = new();
    /// <summary>Deck drains (scuppers) with downspouts.</summary>
    public List<DeckDrainDefinition> Drains { get; set; } = new();
    /// <summary>Inspection cables and walkways (null = none).</summary>
    public InspectionAccessDefinition? InspectionAccess { get; set; }
    /// <summary>Drain collectors, pier drains, sign supports, crash cushion end posts, lighting (null = none).</summary>
    public MiscItemsDefinition? Misc { get; set; }
    /// <summary>Steel intermediate diaphragms between PRESTRESSED beams bolted through cast web holes (Iowa 1036-BTD; null = none).
    /// Detailed after the beams by <c>Diaphragms.PsSteelDiaphragmDetailer</c> in <c>RegenerationCache</c>.</summary>
    public PsSteelDiaphragmDefinition? PsSteelDiaphragms { get; set; }
    /// <summary>Steel tied arch (OPTIONAL; null = none, omitted from the .sbm). A bridge with an arch has no girders (<c>Girders.Count = 0</c>);
    /// generated by <c>Arch.ArchGenerator</c> in <c>RegenerationCache</c>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ArchDefinition? Arch { get; set; }
    /// <summary>Precast post-tensioned segmental box girder (OPTIONAL; null = none, omitted from the .sbm). A segmental bridge has no girders
    /// (<c>Girders.Count = 0</c>); generated by <c>Segmental.SegmentalGenerator</c> in <c>RegenerationCache</c>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SegmentalDefinition? Segmental { get; set; }
    /// <summary>OPTIONAL architectural surface treatments (form liner texture, thin brick, ...) on barrier / substructure / diaphragm faces
    /// (<see cref="SurfaceTreatmentDefinition"/>); null = none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<SurfaceTreatmentDefinition>? SurfaceTreatments { get; set; }

    /// <summary>A typical two-span continuous layout starting at the given station.</summary>
    public static BridgeDefinition CreateDefault(double beginStation, string alignmentName)
    {
        double s1 = Ft(140), s2 = Ft(140);
        return new BridgeDefinition
        {
            AlignmentName = alignmentName,
            Supports =
            {
                new SupportDefinition { Name = "Abut 1", Type = SupportType.Abutment, Station = beginStation },
                new SupportDefinition { Name = "Pier 1", Type = SupportType.Pier, Station = beginStation + s1 },
                new SupportDefinition { Name = "Abut 2", Type = SupportType.Abutment, Station = beginStation + s1 + s2 },
            },
            FieldSplices =
            {
                new FieldSpliceDefinition { Station = beginStation + 0.75 * s1 },
                new FieldSpliceDefinition { Station = beginStation + s1 + 0.25 * s2 },
            },
        };
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Supports.Count < 2) errors.Add("At least two supports are required.");
        for (int i = 1; i < Supports.Count; i++)
            if (Supports[i].Station <= Supports[i - 1].Station)
                errors.Add($"{Supports[i].Name}: support stations must increase.");
        foreach (var s in Supports)
            if (Math.Abs(s.SkewDegrees) >= 75) errors.Add($"{s.Name}: skew must be less than 75°.");
        if (Girders.Count < 1 && Arch == null && Segmental == null) errors.Add("At least one girder is required.");
        if (Segmental != null && Girders.Count != 0) errors.Add("A segmental box bridge has no girders (set Girders.Count = 0).");
        if (Segmental != null) errors.AddRange(Segmental.Validate(Supports.Count - 1));
        if (Arch != null && Girders.Count != 0) errors.Add("A bridge with a tied arch has no girders (set Girders.Count = 0).");
        if (Girders.Count > 1 && Girders.Spacings.Count < Girders.Count - 1)
            errors.Add($"{Girders.Count - 1} girder spacings are required.");
        if (Girders.Spacings.Take(Math.Max(0, Girders.Count - 1)).Any(s => s <= 0))
            errors.Add("Girder spacings must be positive.");
        if (Girders.OffsetsAtSupports is { } tbl)
        {
            if (tbl.Count != Supports.Count || tbl.Any(r => r.Count != Girders.Count))
                errors.Add("Flared girder offset table must have one row per support and one value per girder.");
            else if (tbl.Any(r => r.Zip(r.Skip(1)).Any(p => p.Second <= p.First)))
                errors.Add("Flared girder offsets must increase from left to right at every support.");
        }
        foreach (var pg in Girders.PlanGeometry)
        {
            string gn = $"Girder {pg.Girder + 1} plan geometry";
            if (pg.Girder < 0 || pg.Girder >= Girders.Count) errors.Add($"{gn}: girder index out of range.");
            else if (Girders.PlanGeometry.Count(o => o.Girder == pg.Girder) > 1) errors.Add($"{gn} is given more than once.");
            if (pg.Elements.Count == 0) errors.Add($"{gn} has no elements.");
            foreach (var e in pg.Elements)
            {
                if (e.Length < 0) errors.Add($"{gn}: element lengths cannot be negative.");
                if (e.Kind != GirderPlanElementKind.Line && e.Radius <= 0) errors.Add($"{gn}: arc radius must be positive.");
            }
        }
        if (Girders.SpanLayouts is { } sls)
            foreach (var sl in sls)
            {
                errors.AddRange(sl.Validate(Girders.Count, Supports.Count - 1, Girders.IsPrestressed));
                if (sls.Count(o => o.Span == sl.Span) > 1) errors.Add($"Span {sl.Span + 1} girder layout is given more than once.");
            }
        if (Deck.RightEdgeOffset <= Deck.LeftEdgeOffset) errors.Add("Deck right edge must be right of the left edge.");
        if (Girders.IsPrestressed) errors.AddRange(Girders.Prestressed.Validate());
        if (CrossFrames.MaxSpacing <= 0) errors.Add("Cross-frame maximum spacing must be positive.");
        foreach (var s in Supports)
            if (s.Substructure is { Enabled: true } sub) errors.AddRange(sub.Validate(s.Name, s.Type));
        return errors;
    }

    public BridgeDefinition Clone() => new()
    {
        Name = Name,
        AlignmentName = AlignmentName,
        Structure = Structure,
        Supports = Supports.Select(s => s.Clone()).ToList(),
        AuxiliarySupports = AuxiliarySupports.Select(s => s.Clone()).ToList(),
        Girders = Girders.Clone(),
        Deck = Deck.Clone(),
        CrossFrames = CrossFrames.Clone(),
        Stiffeners = Stiffeners.Clone(),
        FieldSplices = FieldSplices.Select(s => s.Clone()).ToList(),
        Bolts = Bolts.Clone(),
        Bearings = Bearings.Clone(),
        FieldSpliceGap = FieldSpliceGap,
        SpliceDetail = SpliceDetail.Clone(),
        Studs = Studs.Clone(),
        LateralBracing = LateralBracing.Clone(),
        Drains = Drains.Select(d => d.Clone()).ToList(),
        InspectionAccess = InspectionAccess?.Clone(),
        Misc = Misc?.Clone(),
        Arch = Arch?.Clone(),
        Segmental = Segmental?.Clone(),
        SurfaceTreatments = SurfaceTreatments?.Select(t => t.Clone()).ToList(),
    };
}
