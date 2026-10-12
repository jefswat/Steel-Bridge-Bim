using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>Kind of as-designed abutment. Stub = footing + backwall + corbel + beam steps (the default, the I-80 / Ramp G plans);
/// Integral = Iowa integral abutment pile cap (std 2087-BTCD, Woodbury p.15-17): stepped top, ONE pile row, no backwall (the diaphragm is cast with the deck).</summary>
public enum AbutmentKind { Stub, Integral }

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

/// <summary>Pedestal (beam step) built WITHOUT a bearing of this bridge, e.g. MnDOT "Type B" pedestals for a future beam line (Br. 02069 p5 / p6, built now:
/// their bars are in the abutment bar list p8 and quantities p4). Same section as the beam steps (backwall front face to <see cref="AbutmentDefinition.BearingToStepFront"/>).</summary>
public sealed class AbutmentPedestalDefinition
{
    /// <summary>Centre along the support line, S from the alignment point (as <see cref="AbutmentDefinition.PileStations"/>).</summary>
    public double S { get; set; }
    /// <summary>Top elevation as printed (absolute).</summary>
    public double TopElevation { get; set; }
    /// <summary>Length along the support line (NaN = <see cref="AbutmentDefinition.StepLength"/>).</summary>
    public double Length { get; set; } = double.NaN;
    /// <summary>Drawing / solid label, e.g. "FUTURE BEAM".</summary>
    public string Label { get; set; } = "";
    public AbutmentPedestalDefinition() { }
    public AbutmentPedestalDefinition(double s, double top, string label) { S = s; TopElevation = top; Label = label; }
}

/// <summary>End panel (maskwall) standing on the footing end: a thin wall parallel to the alignment, spanning the footing width.</summary>
public sealed class MaskwallDefinition
{
    public AbutmentEnd End { get; set; }
    public double Thickness { get; set; } = In(10);
    /// <summary>Height above the footing top (wash level) at the panel; the top is level.</summary>
    public double Height { get; set; }
    /// <summary>Distance from the footing end line to the outer face of the panel. Plans (West/East p85/p89, South p80): the panel is the OUTERMOST 10" of the
    /// wall zone, outer face flush with the footing end line = 0. Negative = the panel stands OUTSIDE the footing end line (MnDOT Br. 02069 p5 / p11:
    /// mask wall inner face on the footing end line, Setback = -Thickness).</summary>
    public double Setback { get; set; }
    /// <summary>OPTIONAL level bottom of the panel (absolute elevation; null = on the footing top / wash as before). MnDOT Br. 02069 p11 C-C: the mask wall
    /// stands beside the footing down to the footing bottom EL 917.75; the panel is then a plain rectangle across the footing width.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? BottomElevation { get; set; }
}

/// <summary>
/// OPTIONAL MnDOT-type abutment stem on the footing (Br. 02069 p11 A-A, semi-integral abutment p21): a wall from the backwall BACK face line to
/// <see cref="FrontToBearing"/> ahead of the CL bearing, with a BACK LEDGE (the top between the back face and the seat riser, under the end diaphragm /
/// infill wall) and a SEAT in front of the riser (riser on the backwall front-face line; the seat runs straight from <see cref="SeatBackTop"/> at the
/// riser to <see cref="SeatFrontTop"/> at the front face, e.g. the 1" slope). With a stem the footing is a plain block (no wash, no keyed backwall),
/// the backwall stands on the ledge as ONE plain wall (no corbel / pours: the MnDOT infill wall) and the pedestals stand on the seat.
/// Elevations are (S along the line, elevation) points, 1 point = level. Every value is a plan value; a missing one = the stem is not built (warning).
/// </summary>
public sealed class AbutmentStemDefinition
{
    /// <summary>Stem front face ahead of the CL bearing (Br. 02069 1'-6"); NaN = not given.</summary>
    public double FrontToBearing { get; set; } = double.NaN;
    /// <summary>Back ledge top (under the end diaphragm / infill wall).</summary>
    public List<CapProfilePoint> LedgeTop { get; set; } = new();
    /// <summary>Seat at the riser (= backwall front-face line).</summary>
    public List<CapProfilePoint> SeatBackTop { get; set; } = new();
    /// <summary>Seat at the stem front face.</summary>
    public List<CapProfilePoint> SeatFrontTop { get; set; } = new();
    public string Source { get; set; } = "";
}

/// <summary>Backwall extension at one end: backwall-thickness wall BEYOND the footing end line, from the footing-bottom line up to a level top, ending in a cap
/// perpendicular to the strip (West p85: cap corners 4'-6 3/8 / 2'-9 3/8 / 3'-9 7/8 from the gutter / end line; East p89: 3'-3 1/2 to the end line).</summary>
public sealed class BackwallExtensionDefinition
{
    public AbutmentEnd End { get; set; }
    /// <summary>Length of the strip axis from the footing end line (measured at the backwall mid-thickness) to the perpendicular cap.</summary>
    public double Width { get; set; } = Ft(4.5);
    public double TopElevation { get; set; }
}

/// <summary>A counterfort on the fill side of a wingwall with its own wing footing.</summary>
public sealed class CounterfortDefinition
{
    /// <summary>Start of the counterfort (and of its footing) along the gutter line from the CL bearing line, rearward.</summary>
    public double Start { get; set; }
    /// <summary>Width of the WING FOOTING along the wall (centred on the counterfort).</summary>
    public double Width { get; set; } = Ft(3);
    /// <summary>Width of the COUNTERFORT itself along the wall, centred on the footing (plans: 1'-10 between 7" margins, sections E-E / F-F). 0 = same as <see cref="Width"/>.</summary>
    public double BodyWidth { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public double BodyWidthResolved => BodyWidth > 0 ? BodyWidth : Width;
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
    /// <summary>OPTIONAL (default off = the plain sloped bottom): over each counterfort's wing footing (Start .. Start + Width) the wall bottom drops
    /// to the footing top, with vertical steps at the footing ends (plan wing elevations: I-80 PDF 87 A-A / K-K, PDF 91, Ramp G PDF 82).</summary>
    public bool BottomOnFootings { get; set; }
}

/// <summary>Notches of the footing plan outline at one end (West p86 piling layout): between a face and the end line (parallel to the alignment) the outline runs along a
/// line of constant STATION. Front = span side (x toward the span), back = fill side. Absolute station (m) of that line; NaN = no notch on that side.</summary>
public sealed class FootingNotchDefinition
{
    public AbutmentEnd End { get; set; }
    public double FrontStation { get; set; } = double.NaN;
    public double BackStation { get; set; } = double.NaN;
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
    /// <summary>The plan's bend diagram of this mark (schedule shape, no 3D placement): "U" {leg, base, leg}, "L" {leg, run}, "HOOP" {w, h, lap, lap}, "HAT" {foot, leg, top, leg, foot},
    /// "V" {arm, width, depth}; all out to out as printed. "" = straight / not entered.</summary>
    public string Shape { get; set; } = "";
    public double[] Dims { get; set; } = Array.Empty<double>();
    /// <summary>Printed pin diameter D of the diagram (m).</summary>
    public double Pin { get; set; }
    public PlanBarLine Shaped(string shape, double pin, params double[] dims) { Shape = shape; Pin = pin; Dims = dims; return this; }
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
    /// <summary>Backwall vertical bars (printed straight lengths, hung from the top of the backwall less the cover): back face (6d1) and front face (5d2),
    /// their spacing and the distance of the first bar from the backwall end.</summary>
    /// <summary>Printed numbers of backwall verticals (the plan strings: West 171 spaces @ 6" = 172 bars, 86 spaces @ 1'-0 = 87 bars; the list adds corner bars that are not drawn).
    /// 0 = as many as fit between the backwall ends at the spacing.</summary>
    public int BackFaceCount { get; set; }
    public int FrontFaceCount { get; set; }
    public double BackFaceBarLength { get; set; }
    public double FrontFaceBarLength { get; set; }
    public double BackFaceSpacing { get; set; } = In(6);
    public double FrontFaceSpacing { get; set; } = Ft(1);
    /// <summary>Alignment offset from which BackFaceEdge / FrontFaceEdge of the backwall vertical strings are measured (NaN = the left backwall end).</summary>
    public double BackwallStringStartOffset { get; set; } = double.NaN;
    public double BackFaceEdge { get; set; } = In(6);
    public double FrontFaceEdge { get; set; } = Ft(1);
    public double BackwallBarCover { get; set; } = In(2);
    public int BackwallLongRowsPerFace { get; set; } = 11;
    public int BackwallLongPieces { get; set; } = 2;
    /// <summary>Beam steps: transverse U bars (5m1) per step, longitudinal (5n1) per step, hoop (5n2) at these girders only.</summary>
    public int StepUBars { get; set; } = 4;
    public int StepLongBars { get; set; } = 4;
    public List<int> StepHoopGirders { get; set; } = new();
    /// <summary>Plan bar list (used for the comparison and to place the extra end-corner bars the placement strings do not show).</summary>
    public List<PlanBarLine> PlanBarList { get; set; } = new();
}

/// <summary>Elevation of a wall top at a distance U along the wall (m from the wall's start).</summary>
public sealed class WallTopPoint
{
    public double U { get; set; }
    public double Elevation { get; set; }
    public WallTopPoint() { }
    public WallTopPoint(double u, double elevation) { U = u; Elevation = elevation; }
}

/// <summary>A pile of an integral wing extension footing: Along = distance along the gutter line from the cap back face (rearward), Across = from the
/// gutter line, + outboard. NaN = not read from the plans (UNRESOLVED, no pile).</summary>
public sealed class WingExtensionPileDefinition
{
    public double Along { get; set; } = double.NaN;
    public double Across { get; set; } = double.NaN;
}

/// <summary>
/// Iowa integral wing extension (Woodbury p.15 A-A, p.17, p.20 A-A): a wall OUTBOARD of the gutter line on its own level-bottom footing, running rearward along
/// the gutter line from the pile-cap back face. Every elevation / position is a plan input: NaN or empty = UNRESOLVED (the part is not built).
/// </summary>
public sealed class WingExtensionDefinition
{
    public AbutmentEnd End { get; set; }
    /// <summary>Alignment offset (right +) of the gutter line = the wall's inner face; the wall is outboard (away from the alignment).</summary>
    public double GutterOffset { get; set; }
    /// <summary>Length along the gutter line from the pile-cap back face (Woodbury 6'-6).</summary>
    public double Length { get; set; } = double.NaN;
    /// <summary>Wall thickness outboard of the gutter line (Woodbury 1'-7).</summary>
    public double WallThickness { get; set; } = double.NaN;
    /// <summary>Footing width INBOARD of the gutter line (Woodbury 1'-5; the footing is this + the wall thickness wide).</summary>
    public double FootingInside { get; set; } = double.NaN;
    /// <summary>Level footing bottom (Woodbury S 1114.60).</summary>
    public double FootingBottomElevation { get; set; } = double.NaN;
    /// <summary>Level footing top = wall bottom (not printed in the Woodbury set as far as read: UNRESOLVED until entered).</summary>
    public double FootingTopElevation { get; set; } = double.NaN;
    /// <summary>Wall top along the wall (U from the cap back face along the gutter line; the p.20 A / B / C table). 1 point = level; empty = UNRESOLVED.</summary>
    public List<WallTopPoint> WallTop { get; set; } = new();
    public List<WingExtensionPileDefinition> Piles { get; set; } = new();
    /// <summary>Piles the plans show in this extension (Woodbury 1). More than the positioned <see cref="Piles"/> = UNRESOLVED warning.</summary>
    public int PlanPileCount { get; set; }
    /// <summary>Pile head above the footing bottom (NaN = UNRESOLVED, no pile).</summary>
    public double PileTopAboveFootingBottom { get; set; } = double.NaN;
}

/// <summary>Corrugated metal pipe casing around each abutment pile through the EPS fill (Woodbury p.17: 24" CMP, flowable fill). The casing runs from the
/// bottom of the concrete (cap / wing footing) down to <see cref="BottomElevation"/>; NaN = bottom of the EPS fill, not in the set = UNRESOLVED (no solid,
/// schedule records with an unknown length).</summary>
public sealed class PileCasingDefinition
{
    public double Diameter { get; set; } = double.NaN;
    public double BottomElevation { get; set; } = double.NaN;
    public string Fill { get; set; } = "";
    /// <summary>Plan total length of casing for the BRIDGE (m; Woodbury 281 LF for both abutments, p.3 / p.5); 0 = none (reported only).</summary>
    public double PlanTotalLength { get; set; }
}

/// <summary>Pile-head spiral (Woodbury p.15 note: 7 turns No. 2 bar, 21" dia, 3" pitch): schedule-only bar, one per abutment pile.</summary>
public sealed class PileSpiralDefinition
{
    /// <summary>Schedule label ("" = "pile spiral"; the plans give no mark).</summary>
    public string Mark { get; set; } = "";
    /// <summary>Bar size "#2" .. "#18" (No. 2 = 1/4" plain).</summary>
    public string BarSize { get; set; } = "";
    /// <summary>Spiral diameter as printed (out to out).</summary>
    public double Diameter { get; set; } = double.NaN;
    public double Pitch { get; set; } = double.NaN;
    public int Turns { get; set; }
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
    /// <summary>Stub (default, today's footing + backwall) or Integral (pile cap with a stepped top; the "Integral" inputs below).</summary>
    public AbutmentKind Kind { get; set; } = AbutmentKind.Stub;
    // ---- plan
    /// <summary>Alignment offsets of the footing end lines (left is negative), parallel to the alignment.</summary>
    public double LeftEndOffset { get; set; }
    public double RightEndOffset { get; set; }
    /// <summary>Backwall end offsets (NaN = the footing end lines).</summary>
    public double BackwallLeftOffset { get; set; } = double.NaN;
    public double BackwallRightOffset { get; set; } = double.NaN;
    /// <summary>Alignment offsets between which the corbel / paving notch runs on the back face (rear elevations p80 / p85 / p89: the ledge and taper
    /// lines stop at the wing inner faces = gutter lines; the backwall continues behind the wings to the maskwall / extension as a plain wall).
    /// NaN = the backwall ends.</summary>
    public double CorbelLeftOffset { get; set; } = double.NaN;
    public double CorbelRightOffset { get; set; } = double.NaN;
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
    /// <summary>OPTIONAL MnDOT stem with back ledge and seat (Stub kind only; null = the footing / wash / keyed backwall section above).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AbutmentStemDefinition? Stem { get; set; }
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
    /// <summary>Pedestals without a bearing (future beam lines); Stub abutments only.</summary>
    public List<AbutmentPedestalDefinition> StandalonePedestals { get; set; } = new();
    public List<MaskwallDefinition> Masks { get; set; } = new();
    /// <summary>True footing plan outline: end notches (empty = ends are plain alignment-offset lines).</summary>
    public List<FootingNotchDefinition> FootingNotches { get; set; } = new();
    public BackwallExtensionDefinition? Extension { get; set; }
    // ---- integral pile cap (Kind = Integral): section from the CL bearing, stepped top from Steps (one level step per girder)
    /// <summary>Integral: cap back face behind the CL bearing (Woodbury 1'-6: the 3'-0 cap centred on CL brg and the piles). NaN = required input missing.</summary>
    public double CapBackToBearing { get; set; } = double.NaN;
    /// <summary>Integral: cap front face ahead of the CL bearing (Woodbury 1'-6).</summary>
    public double CapFrontToBearing { get; set; } = double.NaN;
    /// <summary>Integral: S (along the support line, from the alignment point) of each step riser, one between each pair of adjacent girders, in S order.
    /// Empty = not given: see <see cref="StepRisersAtMidBay"/>.</summary>
    public List<double> StepBoundaryS { get; set; } = new();
    /// <summary>Integral: risers at mid-bay between the girder bearings. Set only when the plans / designer confirm it (Woodbury p.17 step diagram: open question Q3);
    /// with neither this nor <see cref="StepBoundaryS"/> the riser positions are UNRESOLVED and the cap solid is not built.</summary>
    public bool StepRisersAtMidBay { get; set; }
    /// <summary>Integral: printed step-top to bearing-seat (beam bottom) distance, e.g. the 3" steel bar (NaN = the step top is compared with the seat directly).</summary>
    public double StepToSeat { get; set; } = double.NaN;
    /// <summary>Integral: printed cap depth (step top to bottom) at the left / right end, checked against the model (step diagram p.17; NaN = no check).</summary>
    public double PlanCapDepthLeft { get; set; } = double.NaN;
    public double PlanCapDepthRight { get; set; } = double.NaN;
    /// <summary>Printed concrete volume of the cap (m3; NaN = no check). Woodbury S 18.1 cy, N 19.5 cy (p.17).</summary>
    public double PlanCapVolume { get; set; } = double.NaN;
    public WingExtensionDefinition? WingExtension { get; set; }
    public PileCasingDefinition? PileCasing { get; set; }
    public PileSpiralDefinition? PileSpiral { get; set; }
    // ---- piles (S coordinates along the line of each station; two rows)
    public List<double> PileStations { get; set; } = new();
    /// <summary>x (from the CL bearing, + toward the span) of each pile row. Empty = the two legacy rows (-<see cref="PileBackRowToBearing"/>,
    /// +<see cref="PileFrontRowToBearing"/>). Woodbury integral: [0] (piles on the CL bearing).</summary>
    public List<double> PileRowOffsets { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<double> PileRows => PileRowOffsets.Count > 0 ? PileRowOffsets : new[] { -PileBackRowToBearing, PileFrontRowToBearing };
    public double PileBackRowToBearing { get; set; } = Ft(1) + In(11.5);
    public double PileFrontRowToBearing { get; set; } = Ft(1) + In(9);
    public double PileTopAboveFootingBottom { get; set; } = Ft(2);
    /// <summary>Contract pile length (from the pile top).</summary>
    public double PileLength { get; set; } = Ft(125);
    // ---- wings
    public List<WingwallDefinition> Wings { get; set; } = new();
    public AbutmentRebarDefinition Rebar { get; set; } = new();

    public double FootingBackX => Kind == AbutmentKind.Integral ? -CapBackToBearing : -(BackwallFrontToBearing + BackwallThickness + FootingBackToBackwallBack);
    public double FootingFrontX => Kind == AbutmentKind.Integral ? CapFrontToBearing : BearingToFootingFront;
    public double FootingWidth => FootingFrontX - FootingBackX;

    private static readonly System.Text.Json.JsonSerializerOptions CloneOptions = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public AbutmentDefinition Clone() =>
        System.Text.Json.JsonSerializer.Deserialize<AbutmentDefinition>(System.Text.Json.JsonSerializer.Serialize(this, CloneOptions), CloneOptions)!;
}
