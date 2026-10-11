using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>Which end of an abutment line (S direction: Left = down S / A end of the plans' rear elevation, Right = up S).</summary>
public enum AbutmentEnd { Left, Right }

/// <summary>Top of a beam step (seat block) for one girder, as printed on the rear elevation (absolute elevation).</summary>
public sealed class AbutmentStepDefinition
{
    /// <summary>1-based girder number (G1 = the first girder of the bridge).</summary>
    public int Girder { get; set; }
    public double TopElevation { get; set; }
    public AbutmentStepDefinition() { }
    public AbutmentStepDefinition(int girder, double top) { Girder = girder; TopElevation = top; }
}

/// <summary>End panel (maskwall) standing on the footing end: a thin wall parallel to the alignment, spanning the footing width.</summary>
public sealed class MaskwallDefinition
{
    public AbutmentEnd End { get; set; }
    public double Thickness { get; set; } = In(10);
    /// <summary>Height above the footing top (wash level) at the panel; the top is level.</summary>
    public double Height { get; set; }
    /// <summary>Distance from the footing end line to the outer face of the panel (the plans' 1'-2 zone holds the 10" panel).</summary>
    public double Setback { get; set; } = In(2);
}

/// <summary>Backwall extension at one end: backwall-thickness wall of the given width along the line, level top.</summary>
public sealed class BackwallExtensionDefinition
{
    public AbutmentEnd End { get; set; }
    /// <summary>Width along the abutment line, from the footing end line.</summary>
    public double Width { get; set; } = Ft(4.5);
    public double TopElevation { get; set; }
}

/// <summary>A counterfort on the fill side of a wingwall with its own wing footing.</summary>
public sealed class CounterfortDefinition
{
    /// <summary>Start of the counterfort (and of its footing) along the gutter line from the CL bearing line, rearward.</summary>
    public double Start { get; set; }
    /// <summary>Width along the wall.</summary>
    public double Width { get; set; } = Ft(3);
    /// <summary>Projection from the wall's fill-side face.</summary>
    public double Projection { get; set; } = Ft(3.5);
    /// <summary>Counterfort height above the wing footing top.</summary>
    public double Height { get; set; }
    public double FootingTop { get; set; }
    /// <summary>Bars: 6hN verticals (count per face row), hoops 5h4 (spaces).</summary>
    public int HoopSpaces { get; set; } = 8;
    public int VerticalBarsPerSide { get; set; } = 10;
    public string VerticalMark { get; set; } = "6h2";
    /// <summary>Vertical bars are L-bars with this foot (0 = straight).</summary>
    public double VerticalFoot { get; set; }
}

/// <summary>A wing footing pile with a pile uplift anchor (V-bars 8j3): footing number (1-based) and pile (1 = the outside pile).</summary>
public sealed class UpliftPileDefinition
{
    public int Footing { get; set; }
    public int Pile { get; set; }
}

/// <summary>Wingwall: a wall of the given thickness OUTBOARD of the gutter line (fill-side face on the gutter line), parallel to the alignment.</summary>
public sealed class WingwallDefinition
{
    public string Name { get; set; } = "";
    public AbutmentEnd End { get; set; }
    /// <summary>Alignment offset (right +) of the fill-side face = the gutter line.</summary>
    public double GutterOffset { get; set; }
    public double Thickness { get; set; } = Ft(1.75);
    /// <summary>Length along the gutter line from the CL bearing line to the free end (rearward).</summary>
    public double Length { get; set; }
    public double TopAtBackwall { get; set; }
    public double TopAtEnd { get; set; }
    /// <summary>Wall height at the free end (bottom = top - this when <see cref="BottomAtEnd"/> is NaN).</summary>
    public double EndHeight { get; set; } = Ft(4);
    public double BottomAtBackwall { get; set; }
    /// <summary>Level bottom reach from the backwall back face before the bottom slopes up (West 1'-6, East / Ramp G 0).</summary>
    public double LevelReach { get; set; }
    public double BottomAtEnd { get; set; } = double.NaN;
    public List<CounterfortDefinition> Counterforts { get; set; } = new();
    /// <summary>Wing footing: across the wall (outside + wall + fill side), along the wall, depth.</summary>
    public double FootingOutside { get; set; } = Ft(1.5);
    public double FootingFillSide { get; set; } = Ft(3.75);
    public double FootingDepth { get; set; } = Ft(3);
    public double FootingPileSpacing { get; set; } = Ft(4);
    public double FootingPileTopAbove { get; set; } = Ft(1);
    /// <summary>Wing footing piles circled on the plans (uplift anchors 8j3).</summary>
    public List<UpliftPileDefinition> UpliftPiles { get; set; } = new();
}

/// <summary>Plan bar-list line used to check the model (mark, count, length, weight as printed).</summary>
public sealed class PlanBarLine
{
    public string Mark { get; set; } = "";
    public int Count { get; set; }
    /// <summary>Length (m); for "VARIES" lines the maximum.</summary>
    public double Length { get; set; }
    public double WeightLb { get; set; }
    public bool Varies { get; set; }
    public PlanBarLine() { }
    public PlanBarLine(string mark, int count, double length, double weightLb, bool varies = false) { Mark = mark; Count = count; Length = length; WeightLb = weightLb; Varies = varies; }
}

/// <summary>Reinforcing parameters of the as-designed abutment (sizes follow the plan marks: the leading digit is the bar number).</summary>
public sealed class AbutmentRebarDefinition
{
    public string FootingLongMark { get; set; } = "9a1";
    /// <summary>Bars across the top and bottom rows of the footing section (6 equal spaces = 7; Ramp G 4 = 5).</summary>
    public int FootingRowBars { get; set; } = 7;
    /// <summary>Interior bars on each side face (4 equal spaces = 3).</summary>
    public int FootingSideInteriorBars { get; set; } = 3;
    /// <summary>Pieces per row (2 lapped bars; Ramp G one length).</summary>
    public int FootingLongPieces { get; set; } = 2;
    public double FootingLongLap { get; set; } = Ft(9) + In(1);
    /// <summary>5b1 hoops: per pile pitch count, first offset past the station and spacing; extra end hoops each end.</summary>
    public int HoopsPerPitch { get; set; } = 9;
    public double HoopFirstOffset { get; set; } = In(9);
    public double HoopSpacing { get; set; } = In(8);
    public int EndHoopsEach { get; set; }
    /// <summary>5b2 end staples each end (count, spacing, first offset from the footing end line).</summary>
    public int EndStaplesEach { get; set; } = 5;
    public double EndStapleSpacing { get; set; } = Ft(1);
    public double EndStapleFirst { get; set; } = In(3);
    public double HoopWidth { get; set; } = Ft(6) + In(4.5);
    public double HoopHeight { get; set; } = Ft(3) + In(5);
    /// <summary>6d1 back-face verticals, 5d2 front-face verticals: spacing and end offsets from the backwall ends.</summary>
    public double BackFaceSpacing { get; set; } = In(6);
    public double FrontFaceSpacing { get; set; } = Ft(1);
    public double BackFaceEdge { get; set; } = In(6);
    public double FrontFaceEdge { get; set; } = Ft(1);
    /// <summary>Verticals' lower end above the footing bottom (13'-1 bars end 2'-1 above it on the West sheet).</summary>
    public double VerticalBottomAboveFooting { get; set; } = Ft(2) + In(1);
    public int BackwallLongRowsPerFace { get; set; } = 11;
    public double BackwallLongLap { get; set; } = Ft(2) + In(11);
    public int BackwallLongPieces { get; set; } = 2;
    public double NotchBarSpacing { get; set; } = Ft(1);
    public double NotchDowelSpacing { get; set; } = Ft(2);
    public double NotchLongLap { get; set; } = Ft(2) + In(2);
    public int NotchLongBars { get; set; } = 4;
    public double HairpinSpacing { get; set; } = Ft(1);
    public double BackwallBarCover { get; set; } = In(2);
    public double FootingCover { get; set; } = In(3);
    public double WallCover { get; set; } = In(4);
    /// <summary>Beam steps: transverse U bars (5m1) per step, longitudinal (5n1) per step, hoop (5n2) at these girders only.</summary>
    public int StepUBars { get; set; } = 4;
    public int StepLongBars { get; set; } = 4;
    public List<int> StepHoopGirders { get; set; } = new();
    /// <summary>Plan bar list (used for the comparison and to place the extra end-corner bars the placement strings do not show).</summary>
    public List<PlanBarLine> PlanBarList { get; set; } = new();
}

/// <summary>
/// As-designed abutment (OPTIONAL on <see cref="SubstructureDefinition.Abutment"/>; null = the standard parametric abutment): pile-supported
/// footing with a vertical backwall (corbel / paving notch on the back face), beam steps in front of it, maskwall end panels, backwall
/// extension, wingwalls with counterforts on their own piled footings, piles at the plan stations and the bar list.
/// Section distances are measured from the CL bearing normal to the support line, positive toward the span (x). Plan positions are
/// alignment offsets (right positive) of lines parallel to the alignment tangent. Elevations are absolute.
/// </summary>
public sealed class AbutmentDefinition
{
    public string Source { get; set; } = "";
    // ---- plan
    /// <summary>Alignment offsets of the footing end lines (left is negative), parallel to the alignment.</summary>
    public double LeftEndOffset { get; set; }
    public double RightEndOffset { get; set; }
    /// <summary>Backwall end offsets (NaN = the footing end lines).</summary>
    public double BackwallLeftOffset { get; set; } = double.NaN;
    public double BackwallRightOffset { get; set; } = double.NaN;
    // ---- section chain
    public double FootingBackToBackwallBack { get; set; } = In(1.5);
    public double BackwallThickness { get; set; } = Ft(1.5);
    /// <summary>Backwall front face to CL bearing (22" I-80, 17" Ramp G) - behind the bearing.</summary>
    public double BackwallFrontToBearing { get; set; } = In(22);
    public double BearingToStepFront { get; set; } = In(27);
    public double BearingToFootingFront { get; set; } = Ft(3) + In(3);
    /// <summary>Printed footing width (check only; NaN = none).</summary>
    public double PlanFootingWidth { get; set; } = double.NaN;
    public double FootingDepth { get; set; } = Ft(4);
    public double WashHeight { get; set; } = In(1);
    /// <summary>Bottom of the backwall above the footing bottom (3'-10, keyed into the footing).</summary>
    public double BackwallBaseAboveFootingBottom { get; set; } = Ft(3) + In(10);
    // ---- elevations (S from the alignment point, absolute elevation); 1 point = level
    public List<CapProfilePoint> FootingBottom { get; set; } = new();
    /// <summary>Top of backwall (crown line): follows these points linearly (breaks allowed).</summary>
    public List<CapProfilePoint> BackwallTop { get; set; } = new();
    // ---- corbel / paving notch on the back face
    public double LedgeBelowTop { get; set; } = Ft(1.5);
    public double CorbelProjection { get; set; } = Ft(1.25);
    public double CorbelVertical { get; set; } = Ft(1.25);
    public double CorbelTaperRise { get; set; } = Ft(1.25);
    public double JointBelowTaper { get; set; } = In(3);
    // ---- steps
    public double StepLength { get; set; } = Ft(4.5);
    public List<AbutmentStepDefinition> Steps { get; set; } = new();
    public List<MaskwallDefinition> Masks { get; set; } = new();
    public BackwallExtensionDefinition? Extension { get; set; }
    // ---- piles (S coordinates along the line of each station; two rows)
    public List<double> PileStations { get; set; } = new();
    public double PileBackRowToBearing { get; set; } = Ft(1) + In(11.5);
    public double PileFrontRowToBearing { get; set; } = Ft(1) + In(9);
    public double PileTopAboveFootingBottom { get; set; } = Ft(2);
    /// <summary>Contract pile length (from the pile top).</summary>
    public double PileLength { get; set; } = Ft(125);
    // ---- wings
    public List<WingwallDefinition> Wings { get; set; } = new();
    public AbutmentRebarDefinition Rebar { get; set; } = new();

    public double FootingBackX => -(BackwallFrontToBearing + BackwallThickness + FootingBackToBackwallBack);
    public double FootingFrontX => BearingToFootingFront;
    public double FootingWidth => FootingFrontX - FootingBackX;

    private static readonly System.Text.Json.JsonSerializerOptions CloneOptions = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public AbutmentDefinition Clone() =>
        System.Text.Json.JsonSerializer.Deserialize<AbutmentDefinition>(System.Text.Json.JsonSerializer.Serialize(this, CloneOptions), CloneOptions)!;
}
