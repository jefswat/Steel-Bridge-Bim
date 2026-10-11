using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

// Miscellaneous bridge items from the plan set (Iowa DOT Designs 116 / 316): Ramp G deck drainage (trench inlets, collection pipe,
// pier joint collection systems), I-80 sign support brackets / exit sign bases, crash cushion end posts, lighting (pole bases,
// conduit, junction boxes). ALL OPTIONAL: BridgeDefinition.Misc == null and DeckDrainDefinition.Trench == null mean none.
// Standing rule: nothing is assumed. Every value is a plan value (docs/plan-data/misc-items-data.md); anything the plans do not give is a
// nullable input left null, and the generator RAISES it (warning) instead of inventing a number.

/// <summary>Trench-type inlet of a <see cref="DeckDrainDefinition"/> (Iowa Std. Sheet 1054 modified): a transverse trench starting 1/2" clear of the
/// gutter line, a vertical structural tube, grate panels. Position is measured FROM THE GUTTER LINE exactly as the plans dimension it.</summary>
public sealed class DrainTrenchDefinition
{
    /// <summary>Gutter line (barrier face) distance inboard of the deck edge; null = the deck's <see cref="DeckEdgeStrip"/> width (plans: 1'-7).</summary>
    public double? GutterFromEdge { get; set; }
    public double InletStartFromGutter { get; set; } = In(0.5);
    /// <summary>Trench length across the deck (4'-0).</summary>
    public double InletAcrossDeck { get; set; } = Ft(4);
    /// <summary>Clear width along the roadway (10" +/- 1/2).</summary>
    public double InletAlongRoad { get; set; } = In(10);
    /// <summary>Side plates (3/8") each side of the clear width.</summary>
    public double SidePlate { get; set; } = In(0.375);
    /// <summary>Tube centre from the gutter line (3'-0 1/2 = 1/2 + 3'-0).</summary>
    public double TubeFromGutter { get; set; } = Ft(3) + In(0.5);
    public double TubeDiameter { get; set; } = In(8);
    public double TubeWall { get; set; } = In(0.375);
    /// <summary>Trench depth below the grate top. The plans show 3 1/2" and 4 1/4" in different views (UNRESOLVED): the larger is modeled, uniform.</summary>
    public double TrenchDepth { get; set; } = In(4.25);
    public double GrateThickness { get; set; } = In(1.5);
    /// <summary>Piped drains: tube centre from CL of the reference girder (1'-8, Ramp G drains 22-25); checked, never used to move the drain.</summary>
    public double? TubeFromGirderCl { get; set; }
    public int ReferenceGirder { get; set; }
    /// <summary>Cross slope printed in the drain table (fraction); compared with the model at the gutter line.</summary>
    public double? TableCrossSlope { get; set; }
    /// <summary>Collector the tube is piped to (<see cref="DrainCollectorDefinition.Name"/>); empty = free fall.</summary>
    public string Collector { get; set; } = "";
    /// <summary>Drain number / location text from the plans' drain table.</summary>
    public string Label { get; set; } = "";
    public double BasinSize { get; set; } = Ft(10);
    public double BasinDepth { get; set; } = Ft(2);
    public DrainTrenchDefinition Clone() => (DrainTrenchDefinition)MemberwiseClone();
}

/// <summary>Distance from the deck top to the CL of a collection pipe at a station (plans: table on the span 19G sheet).</summary>
public sealed class CollectorPoint
{
    public double Station { get; set; }
    public double DeckTopToCl { get; set; }
    public string Note { get; set; } = "";
    public CollectorPoint Clone() => (CollectorPoint)MemberwiseClone();
}

/// <summary>Deck drain collection pipe under the deck beside a girder (Ramp G span 19G: 12" pipe 1'-8 right of CL girder A at 1.78%).</summary>
public sealed class DrainCollectorDefinition
{
    public string Name { get; set; } = "Collector";
    /// <summary>Nominal pipe size (12"); ASTM A53 Sch 40 outside diameter / wall are taken from the standard table.</summary>
    public double NominalDiameter { get; set; } = In(12);
    public int ReferenceGirder { get; set; }
    /// <summary>CL pipe from CL reference girder, normal to the girder in plan, right positive.</summary>
    public double OffsetFromGirder { get; set; } = Ft(1) + In(8);
    public double Slope { get; set; } = 0.0178;
    public double StartStation { get; set; }
    /// <summary>Support (name) at which the pipe turns down; its CL bearing line ends the run.</summary>
    public string EndSupport { get; set; } = "";
    public List<CollectorPoint> Points { get; set; } = new();
    public double HangerSpacing { get; set; } = Ft(5);
    /// <summary>Flexible coupler stations (plans: 4'-0 upstream of drains 23, 24 and 2'-0 upstream of drain 25).</summary>
    public List<double> CouplerStations { get; set; } = new();
    public DrainCollectorDefinition Clone()
    {
        var c = (DrainCollectorDefinition)MemberwiseClone();
        c.Points = Points.Select(p => p.Clone()).ToList();
        c.CouplerStations = CouplerStations.ToList();
        return c;
    }
}

public enum PierDrainOutlet { ErosionMattress, Pavement }

/// <summary>Deck-joint collection system at a pier (Ramp G Piers 5G, 9G, 14G, 19G): neoprene trough under the joint, trough collection box,
/// 8" pipe at 1:12 min to the pier collection box, which hangs on the (back) FACE of the cap (Ramp G PDF 155 sections A-A / C-C, PDF 157 box details),
/// then a pipe from the box bottom at 8% min along the cap face toward the CL pier and vertical down the column (PDF 157 drain pipe elevations).
/// Nothing passes through the cap concrete. Placement conventions are flagged by the generator; unknown dimensions stay null.</summary>
public sealed class PierDrainDefinition
{
    /// <summary>END support of the unit that owns the joint (name).</summary>
    public string Support { get; set; } = "";
    /// <summary>Right (true) = collects at the right gutter (girder D side at 5G/9G/14G), false = left (girder A side at 19G).</summary>
    public bool CollectRight { get; set; } = true;
    public double TroughSlope { get; set; } = 0.059;
    /// <summary>Trough collection box centre from the gutter line on the collecting side, measured along the pier line (3'-6; 4'-0 at 19G).</summary>
    public double BoxFromGutter { get; set; } = Ft(3.5);
    public double BoxLength { get; set; } = Ft(2) + In(2);
    public double BoxWidth { get; set; } = Ft(1);
    public double BoxHighEndHeight { get; set; } = Ft(1) + In(7);
    public double BoxBottomSlope { get; set; } = In(3);
    /// <summary>Drop of the trough at the collection end printed on the sheet (1'-8): only compared with slope x run.</summary>
    public double? PrintedTroughDrop { get; set; }
    public double PipeNominalDiameter { get; set; } = In(8);
    public double Pipe1Slope { get; set; } = 1.0 / 12;
    /// <summary>Cap collection box position along the cap from the CL column (S, right positive): +12'-6 at 5G/9G/14G, -18'-3 7/8 at 19G.</summary>
    public double CapBoxFromColumn { get; set; }
    /// <summary>Cap face the pier collection box is fastened to (PDF 155 sections A-A / C-C and the PDF 157 elevations looking ahead: the back face).</summary>
    public CapFaceSide CapBoxFace { get; set; } = CapFaceSide.Back;
    /// <summary>Box width along the cap face (PDF 157 view A-A: 1'-4).</summary>
    public double CapBoxAlongFace { get; set; } = Ft(1) + In(4);
    /// <summary>Box projection from the cap face at the top (PDF 157 box elevation: 1'-6) and at the bottom (1'-4, the outer wall tapers over the lower 1'-0).</summary>
    public double CapBoxOutTop { get; set; } = Ft(1) + In(6);
    public double CapBoxOutBottom { get; set; } = Ft(1) + In(4);
    /// <summary>CL of the pipe dropping into the box from the cap face (PDF 155 sections A-A / C-C, PDF 157: 9" at 50 F).</summary>
    public double CapBoxInletFromFace { get; set; } = In(9);
    /// <summary>Clear distance from the cap face to the outside of the outlet pipe at the box bottom (PDF 157 box elevation: 1 1/2").</summary>
    public double CapBoxOutletClear { get; set; } = In(1.5);
    /// <summary>Depth the inlet pipe enters the box below its top (PDF 157 box elevation / view A-A: 4").</summary>
    public double CapBoxInletEmbed { get; set; } = In(4);
    public double CapBoxDepth { get; set; } = Ft(2);
    /// <summary>Box top below the cap top (plans: 1/4" min / 1" max; section A-A: 1" at CL box): null = UNRESOLVED (generator raises and uses the 1/4" minimum).</summary>
    public double? CapBoxBelowTop { get; set; }
    public double CapPipeSlope { get; set; } = 0.08;
    public double CapPipeNominalDiameter { get; set; } = In(8);
    public double SupportSpacing { get; set; } = Ft(6);
    public PierDrainOutlet Outlet { get; set; } = PierDrainOutlet.ErosionMattress;
    public double MattressSize { get; set; } = Ft(4);
    public double MattressThickness { get; set; } = Ft(1);
    public double OutletAboveMattress { get; set; } = Ft(1);
    public double OutletLegLength { get; set; } = Ft(1);
    /// <summary>Pavement elevation under the pipe end (Pier 19G: 981 ft); the pipe ends 1" above it.</summary>
    public double? PavementElevation { get; set; }
    /// <summary>Span collector that ends in the cap box (Pier 19G).</summary>
    public string Collector { get; set; } = "";
    public PierDrainDefinition Clone() => (PierDrainDefinition)MemberwiseClone();
}

/// <summary>One line of a plan bar list for a misc concrete item (schedule only: bar positions are not modeled). Length = the listed length;
/// 0 = derive it from the printed weight (varying bars whose length range is printed but not the individual lengths).</summary>
public sealed class MiscBarRow
{
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#5";
    public int Count { get; set; }
    public double Length { get; set; }
    /// <summary>Printed weight per item (lb): used for a varying bar's average length.</summary>
    public double WeightLb { get; set; }
    public List<double>? Lengths { get; set; }
    public bool Epoxy { get; set; } = true;
    public string Coating { get; set; } = "";
    public string Location { get; set; } = "";
    public MiscBarRow Clone() { var c = (MiscBarRow)MemberwiseClone(); c.Lengths = Lengths?.ToList(); return c; }
}

public enum SignSupportKind { OverheadTrussBracket, ExitSignBase }

/// <summary>One sign truss support bracket (stub) on an exterior girder.</summary>
public sealed class SignBracketDefinition
{
    public int Girder { get; set; }
    /// <summary>Distance ahead of the CL bearing of <see cref="SignSupportDefinition.Support"/> along the girder (post line).</summary>
    public double AheadOfSupport { get; set; }
    public double TopOfBaseElevation { get; set; }
    public double Height { get; set; }
    /// <summary>Bottom flange plate width (22" Unit 2, 26" Unit 3).</summary>
    public double BottomFlangeWidth { get; set; }
    public SignBracketDefinition Clone() => (SignBracketDefinition)MemberwiseClone();
}

/// <summary>Sign support: overhead truss brackets (bracket steel modeled, post / base plate / truss as placeholders) or exit sign bases.</summary>
public sealed class SignSupportDefinition
{
    /// <summary>Plan bar list per item (schedule only).</summary>
    public List<MiscBarRow> Bars { get; set; } = new();
    public string Name { get; set; } = "";
    public SignSupportKind Kind { get; set; }
    public string Support { get; set; } = "";
    public List<SignBracketDefinition> Brackets { get; set; } = new();
    /// <summary>Truss span CL post to CL post (100'-0 / 90'-0), carried by the placeholders.</summary>
    public double TrussSpan { get; set; }
    // Bracket plate sizes (plans: I-80 PDF 178/182).
    public double WebThickness { get; set; } = In(0.75);
    public double TopPlateThickness { get; set; } = In(1.875);
    public double TopPlateWidth { get; set; } = In(28);
    public double TopPlateLength { get; set; } = Ft(2) + In(4);
    public double BottomFlangeThickness { get; set; } = In(1.25);
    public double StiffenerThickness { get; set; } = In(1.125);
    public double StiffenerWidth { get; set; } = In(12.5);
    public double StiffenerOffset { get; set; } = Ft(1);
    public int AnchorBoltCount { get; set; } = 8;
    public double AnchorBoltDiameter { get; set; } = In(1.5);
    public double AnchorCircleDiameter { get; set; } = Ft(1) + In(8);
    public double AnchorStartAngleDegrees { get; set; } = 22.5;
    public double BasePlateDiameter { get; set; } = Ft(2) + In(2);
    public double BasePlateThickness { get; set; } = In(2);
    public double PostOuterDiameter { get; set; } = In(14);
    public double PostWall { get; set; } = In(0.5);
    // Exit sign base (outrigger block): placed only when BackFromBearing is given.
    public int Girder { get; set; }
    public double? BackFromBearing { get; set; }
    public double BlockAlong { get; set; } = Ft(2);
    public double BlockOut { get; set; } = Ft(6) + In(2);
    public double BlockHeight { get; set; } = Ft(2) + In(6);
    public SignSupportDefinition Clone()
    {
        var c = (SignSupportDefinition)MemberwiseClone();
        c.Brackets = Brackets.Select(b => b.Clone()).ToList();
        c.Bars = Bars.Select(b => b.Clone()).ToList();
        return c;
    }
}

/// <summary>Concrete end post of a crash cushion at a gore (7'-6 wedge, 3'-8 to 2'-10 high, 2'-0 end). Placed only when the position is given.</summary>
public sealed class CrashCushionEndPostDefinition
{
    /// <summary>Plan bar list per item (schedule only).</summary>
    public List<MiscBarRow> Bars { get; set; } = new();
    public string Name { get; set; } = "";
    public string Support { get; set; } = "";
    /// <summary>Plan position of the pier-end of the wedge: station and offset from the alignment, unit direction angle (degrees from the alignment tangent). Null = UNRESOLVED.</summary>
    public double? Station { get; set; }
    public double? Offset { get; set; }
    public double? DirectionDegrees { get; set; }
    public double Length { get; set; } = Ft(7.5);
    public double TaperLength { get; set; } = Ft(5);
    public double EndWidth { get; set; } = Ft(2);
    /// <summary>Plan width at the pier end (two rails 1'-5 each + gap); null = UNRESOLVED.</summary>
    public double? PierEndWidth { get; set; }
    public double HeightRailEnd { get; set; } = Ft(3) + In(8);
    public double HeightAtEnd { get; set; } = Ft(2) + In(10);
    public double? CushionLength { get; set; }
    public CrashCushionEndPostDefinition Clone() { var c = (CrashCushionEndPostDefinition)MemberwiseClone(); c.Bars = Bars.Select(b => b.Clone()).ToList(); return c; }
}

public enum LightingSide { Left, Right }

/// <summary>Pole base (anchor bolt circle, block outboard of the barrier). Pole and luminaire are not in the contract.</summary>
public sealed class PoleBaseDefinition
{
    /// <summary>Plan bar list per item (schedule only).</summary>
    public List<MiscBarRow> Bars { get; set; } = new();
    public string Name { get; set; } = "";
    public string Support { get; set; } = "";
    /// <summary>Plan CL of the base measured along the barrier from the CL support (ahead positive); null = UNRESOLVED (generator raises, not placed).</summary>
    public double? AlongFromSupport { get; set; }
    public LightingSide Side { get; set; }
    public double BoltCircleDiameter { get; set; }
    public int BoltCount { get; set; } = 4;
    public double BoltDiameter { get; set; } = In(1);
    public double BoltLength { get; set; } = Ft(1) + In(11);
    public double BlockAlong { get; set; } = Ft(2);
    public double BlockOut { get; set; } = Ft(2) + In(6);
    public double TopAboveLedge { get; set; } = In(5);
    public double HeightAtBarrier { get; set; } = Ft(2) + In(7.5);
    public double HeightAtOuterEdge { get; set; } = In(11);
    public double SoffitRadius { get; set; } = Ft(3);
    public PoleBaseDefinition Clone() { var c = (PoleBaseDefinition)MemberwiseClone(); c.Bars = Bars.Select(b => b.Clone()).ToList(); return c; }
}

public enum JunctionBoxKind { Power, Its, Unresolved }

public sealed class JunctionBoxDefinition
{
    public string Name { get; set; } = "";
    public string Support { get; set; } = "";
    /// <summary>Along the barrier from the CL support (ahead positive).</summary>
    public double AlongFromSupport { get; set; }
    public LightingSide Side { get; set; }
    public JunctionBoxKind Kind { get; set; } = JunctionBoxKind.Unresolved;
    /// <summary>Plan size of the LI-104 Type 1 box is not in the plan set: stand-in values flagged as a placeholder.</summary>
    public double Width { get; set; } = Ft(1) + In(6);
    public double Height { get; set; } = In(6);
    public double Depth { get; set; } = In(5);
    public JunctionBoxDefinition Clone() => (JunctionBoxDefinition)MemberwiseClone();
}

/// <summary>Rigid steel conduit run inside the barrier (2" at the printed height above the barrier base, 5" from the traffic face).</summary>
public sealed class ConduitRunDefinition
{
    public string Name { get; set; } = "";
    public LightingSide Side { get; set; }
    public JunctionBoxKind Kind { get; set; } = JunctionBoxKind.Power;
    public double FromStation { get; set; }
    public double ToStation { get; set; }
    public double NominalDiameter { get; set; } = In(2);
    public double HeightAboveBase { get; set; }
    public double FromTrafficFace { get; set; } = In(5);
    public ConduitRunDefinition Clone() => (ConduitRunDefinition)MemberwiseClone();
}

public sealed class LightingDefinition
{
    public List<PoleBaseDefinition> PoleBases { get; set; } = new();
    public List<JunctionBoxDefinition> JunctionBoxes { get; set; } = new();
    public List<ConduitRunDefinition> Conduits { get; set; } = new();
    public LightingDefinition Clone() => new()
    {
        PoleBases = PoleBases.Select(p => p.Clone()).ToList(),
        JunctionBoxes = JunctionBoxes.Select(p => p.Clone()).ToList(),
        Conduits = Conduits.Select(p => p.Clone()).ToList(),
    };
}

/// <summary>Container of the optional misc items of one bridge (null on the bridge = none).</summary>
public sealed class MiscItemsDefinition
{
    /// <summary>Plan items that could not be determined (data scripts list them): raised as warnings by the generator, never guessed.</summary>
    public List<string> Unresolved { get; set; } = new();
    public List<DrainCollectorDefinition> Collectors { get; set; } = new();
    public List<PierDrainDefinition> PierDrains { get; set; } = new();
    public List<SignSupportDefinition> SignSupports { get; set; } = new();
    public List<CrashCushionEndPostDefinition> EndPosts { get; set; } = new();
    public LightingDefinition? Lighting { get; set; }
    public MiscItemsDefinition Clone() => new()
    {
        Collectors = Collectors.Select(c => c.Clone()).ToList(),
        PierDrains = PierDrains.Select(c => c.Clone()).ToList(),
        SignSupports = SignSupports.Select(c => c.Clone()).ToList(),
        EndPosts = EndPosts.Select(c => c.Clone()).ToList(),
        Lighting = Lighting?.Clone(),
        Unresolved = Unresolved.ToList(),
    };
}
