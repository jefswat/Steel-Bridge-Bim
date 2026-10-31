namespace SBM.Core.Bridge;

// Precast segmental box: concrete details and critical-section reinforcing (ALL OPTIONAL: null lists on SegmentalDefinition = none, omitted from the .sbm).
// Plan data: docs/plan-data/bonner-b2500/segmental-unit5/DETAILS-DATA.md (anchor / erection bar blocks, openings, parapet, typical segment bars).
// Section coordinates are the box section's: x lateral from CL girder (for a block: positive TOWARD the block's web, mirrored for the left web),
// y DOWN from the deck top, both in the section plane (normal to the graded tangent, rotated by the cross slope).

public enum SegmentalBlockKind { AnchorBlock, ErectionBarBlock, DeviationBlock, ShearKey }

/// <summary>Joint of a typical segment a block is measured from: the bent-side joint (toward the cantilever bent) or the tip-side (bulkhead) joint
/// (short-line match casting: the form is built off the bulkhead face = the face farther from the pier; user decision 2026-10-30). <see cref="Centre"/>
/// (appended) = from the segment's middle station toward up-station (any hollow segment, e.g. a closure joint).</summary>
public enum SegmentalJointRef { BentSide, TipSide, Centre }

/// <summary>One section of a block type. The block section = the box VOID at that station (right half) clipped by every given limit (NaN = unused).
/// Sections between stations are lofted; a limit set that clips the void to nothing closes the block to an edge.</summary>
public sealed class SegmentalBlockStation
{
    /// <summary>Distance from the block start along the segment (away from the reference joint), m.</summary>
    public double Along { get; set; }
    /// <summary>Keep x >= / <= these offsets from CL girder.</summary>
    public double InnerFromCl { get; set; } = double.NaN;
    public double OuterFromCl { get; set; } = double.NaN;
    /// <summary>Keep x >= (web inside face at the block top) - this width (block top width measured from the web face).</summary>
    public double WidthFromWeb { get; set; } = double.NaN;
    /// <summary>Block top this high above the bottom slab top (keep y >= h - t - value).</summary>
    public double TopAboveSlab { get; set; } = double.NaN;
    /// <summary>Block top this high above the soffit (keep y >= h - value).</summary>
    public double TopAboveSoffit { get; set; } = double.NaN;
    /// <summary>Top-slab block: depth below the deck top of the free (inner) end of its bottom face.</summary>
    public double BottomBelowDeck { get; set; } = double.NaN;
    /// <summary>Top-slab block: its bottom face runs square to the web inside face, this long from it (with <see cref="BottomBelowDeck"/>);
    /// the inner face is vertical through the free end.</summary>
    public double BottomSquareToWeb { get; set; } = double.NaN;
    /// <summary>OPTIONAL block top this deep below the deck top (keep y >= value).</summary>
    public double TopBelowDeck { get; set; } = double.NaN;
    /// <summary>OPTIONAL web thickening: keep the part of the void within this distance (normal to the web) of the web inside face; the block's inner
    /// face is parallel to the web.</summary>
    public double ThicknessFromWeb { get; set; } = double.NaN;
    public SegmentalBlockStation Clone() => (SegmentalBlockStation)MemberwiseClone();
}

/// <summary>Reusable block shape (PT anchor block / blister, erection PT bar block, deviation block).</summary>
public sealed class SegmentalBlockType
{
    public string Name { get; set; } = "";
    public SegmentalBlockKind Kind { get; set; }
    /// <summary>Joint the block start is measured from and the distance (m) from that joint to the block start.</summary>
    public SegmentalJointRef From { get; set; }
    public double Start { get; set; }
    public List<SegmentalBlockStation> Stations { get; set; } = new();
    public string Description { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Open / scaled dimensions (UnresolvedFlag text): blocks are built and flagged dimension-only. A type with <see cref="NotBuilt"/> is only a record.</summary>
    public string Unresolved { get; set; } = "";
    /// <summary>The plans do not give enough to build the shape (raised once as a warning; placements are counted, no solid).</summary>
    public string NotBuilt { get; set; } = "";
    public SegmentalBlockType Clone() { var c = (SegmentalBlockType)MemberwiseClone(); c.Stations = Stations.Select(s => s.Clone()).ToList(); return c; }
}

/// <summary>One block placement: block type in a named typical segment, on the left (-1), right (+1) or both (0) webs; Label = tendon / bar served.</summary>
public sealed class SegmentalBlockPlacement
{
    public string Type { get; set; } = "";
    public string Segment { get; set; } = "";
    public int Side { get; set; }
    public string Label { get; set; } = "";
    public string Source { get; set; } = "";
    public SegmentalBlockPlacement Clone() => (SegmentalBlockPlacement)MemberwiseClone();
}

public enum SegmentalOpeningKind { BottomSlab, TopSlab, BentDiaphragm }

/// <summary>Opening through the box: slab openings (rectangle Width across x Length along, or round Diameter) centred on the segment (or, with
/// <see cref="Bents"/>, on the bent CL split into the two bent halves); diaphragm openings = <see cref="Outline"/> through the solid bent diaphragm.</summary>
public sealed class SegmentalOpeningDefinition
{
    public string Name { get; set; } = "";
    public SegmentalOpeningKind Kind { get; set; }
    /// <summary>Segments (slab openings centred on the segment).</summary>
    public List<string> Segments { get; set; } = new();
    /// <summary>OPTIONAL -L- stations of slab openings (centred there, in the segment that contains the station); added to <see cref="Segments"/>.</summary>
    public List<double> Stations { get; set; } = new();
    /// <summary>Cantilever bents (openings at the bent CL, one void per bent half).</summary>
    public List<string> Bents { get; set; } = new();
    public double Width { get; set; } = double.NaN;
    public double Length { get; set; } = double.NaN;
    public double Diameter { get; set; } = double.NaN;
    public double CenterOffset { get; set; }
    /// <summary>OPTIONAL side rule for slab openings (null = <see cref="CenterOffset"/> signed, right +): the opening sits |CenterOffset| on that side
    /// (LowSide / HighSide = the side the cross slope falls / rises to at the segment centre).</summary>
    public SegmentalDrainSide? Side { get; set; }
    /// <summary>OPTIONAL: |CenterOffset| is measured inward from the soffit corner (half bottom width at the segment centre) instead of from -L-.</summary>
    public bool FromSoffitCorner { get; set; }
    /// <summary>OPTIONAL recess depth from the top (top slab) / bottom (bottom slab) face (NaN = through the slab).</summary>
    public double Depth { get; set; } = double.NaN;
    /// <summary>Diaphragm opening outline in the section: S = x from CL girder, N = height above the bottom slab top (or depth below the deck top with
    /// <see cref="OutlineFromTop"/>).</summary>
    public List<PlanPoint> Outline { get; set; } = new();
    public bool OutlineFromTop { get; set; }
    /// <summary>Temporary opening (repaired after erection): recorded and drawn, no concrete deducted.</summary>
    public bool Temporary { get; set; }
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public SegmentalOpeningDefinition Clone()
    {
        var c = (SegmentalOpeningDefinition)MemberwiseClone();
        c.Segments = Segments.ToList(); c.Stations = Stations.ToList(); c.Bents = Bents.ToList(); c.Outline = Outline.Select(p => new PlanPoint(p.S, p.N)).ToList();
        return c;
    }
}

/// <summary>Concrete parapet on both deck edges: rectangle Width x Height (top corners chamfered), inside face at InsideOffset from the alignment,
/// plumb, bottom on the box top; continuous over the unit (joints recorded only).</summary>
/// <summary>Run of parapet expansion joints between two supports (or a support and the unit end): <see cref="Spaces"/> equal spaces of
/// <see cref="SegmentalParapetDefinition.JointSpacing"/> centred in the run, measured along the deck edge at
/// <see cref="SegmentalParapetDefinition.JointEdgeOffset"/> from the alignment (each side its own arc; equal end lengths = the plans' symmetric chains).
/// <see cref="From"/> / <see cref="To"/> = support names, "" = the box end. Printed end lengths / arcs (m, NaN = not printed) are check rows.</summary>
public sealed class SegmentalParapetJointRun
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Spaces { get; set; }
    public double PrintedEndA { get; set; } = double.NaN;
    public double PrintedEndB { get; set; } = double.NaN;
    public double PrintedArcA { get; set; } = double.NaN;
    public double PrintedArcB { get; set; } = double.NaN;
    public string Source { get; set; } = "";
    public SegmentalParapetJointRun Clone() => (SegmentalParapetJointRun)MemberwiseClone();
}

public sealed class SegmentalParapetDefinition
{
    public double InsideOffset { get; set; } = double.NaN;
    public double Width { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    public double Chamfer { get; set; }
    public double JointSpacing { get; set; } = double.NaN;
    public double JointWidth { get; set; } = double.NaN;
    /// <summary>OPTIONAL joint runs (null = continuous parapet, joints recorded only) and the deck-edge offset their arcs are measured on.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<SegmentalParapetJointRun>? JointRuns { get; set; }
    public double JointEdgeOffset { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public SegmentalParapetDefinition Clone() { var c = (SegmentalParapetDefinition)MemberwiseClone(); c.JointRuns = JointRuns?.Select(r => r.Clone()).ToList(); return c; }
}

public enum SegmentalBarPlacement { ScheduleOnly, TransverseTop, TransverseSoffit, LongitudinalTop, LongitudinalSoffit }

/// <summary>Plan bar of a segment bill. Entries with the same Mark form one mark: the schedule count = sum of <see cref="CountPerSegment"/> x the
/// matched segments; entries with a <see cref="Placement"/> place straight bars (<see cref="Count"/> per segment at <see cref="Spacing"/>, centred;
/// transverse = across, centred on CL girder; longitudinal = along the segment, centred in it) at <see cref="FaceDistance"/> (face to bar centerline)
/// below the deck top / above the soffit. Placed bars are checked for clear cover against the segment solids (reported, never moved).</summary>
public sealed class SegmentalBarDefinition
{
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#4";
    /// <summary>Regex on the segment names this bar belongs to.</summary>
    public string Segments { get; set; } = "";
    public int CountPerSegment { get; set; }
    public double ListedLength { get; set; }
    public string ShapeCode { get; set; } = "STR";
    public string PrintedDims { get; set; } = "";
    public SegmentalBarPlacement Placement { get; set; }
    public int Count { get; set; }
    public double Spacing { get; set; }
    public double FaceDistance { get; set; } = double.NaN;
    /// <summary>Clear covers for the cover check (NaN = not checked).</summary>
    public double Cover { get; set; } = double.NaN;
    public double CoverTop { get; set; } = double.NaN;
    public double CoverBottom { get; set; } = double.NaN;
    public string Location { get; set; } = "";
    public string Unit { get; set; } = "";
    public bool Epoxy { get; set; }
    public string Source { get; set; } = "";
    /// <summary>Why the bar (or the rest of the mark) is not placed (UnresolvedFlag text, in the mark location).</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalBarDefinition Clone() => (SegmentalBarDefinition)MemberwiseClone();
}

public enum SegmentalKeyKind { WebShear, TopSlabAlignment, BottomSlabAlignment }

/// <summary>Web key row of the plan's key table: section number at the joint (0 = the bent control line), number of male keys, the key zone length and
/// the distance from the soffit to the first key (both along the web, see <see cref="SegmentalKeyDefinition.BottomAlongWeb"/>).</summary>
public sealed class SegmentalWebKeyRow
{
    public int Section { get; set; }
    public int Count { get; set; }
    public double Zone { get; set; } = double.NaN;
    public double BottomDistance { get; set; } = double.NaN;
    public SegmentalWebKeyRow Clone() => (SegmentalWebKeyRow)MemberwiseClone();
}

/// <summary>
/// Match-cast joint keys (OPTIONAL <see cref="SegmentalDefinition.Keys"/>): trapezoid keys <see cref="Depth"/> deep, <see cref="BaseWidth"/> at the joint face,
/// <see cref="FlatWidth"/> at the tip, <see cref="Length"/> long, protruding from the MATCH-CAST face of the segment farther from its cantilever bent into the
/// female recess of its neighbour (the bulkhead face is female); at the bent control line from the up-station bent half. Web keys: rows per section, centred
/// in the web thickness, spaced <see cref="Spacing"/> along the web from the soffit; slab keys: centres at <see cref="Offsets"/> from CL girder,
/// <see cref="CenterDepth"/> below the deck top (top slab) or below the bottom slab top (bottom slab), on the joints of sections <see cref="FromSection"/> ..
/// <see cref="ToSection"/> (joints beyond the last table section take its number). The keys are recorded solids: the segment solids are not cut (male volume
/// = female volume, net zero concrete).
/// </summary>
public sealed class SegmentalKeyDefinition
{
    public string Name { get; set; } = "";
    public SegmentalKeyKind Kind { get; set; }
    public double Depth { get; set; } = double.NaN;
    public double BaseWidth { get; set; } = double.NaN;
    public double FlatWidth { get; set; } = double.NaN;
    public double Length { get; set; } = double.NaN;
    public double Spacing { get; set; } = double.NaN;
    /// <summary>Web keys: the bottom distance is measured along the web from the soffit (outside bottom corner) to the first key centre.</summary>
    public bool BottomAlongWeb { get; set; } = true;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<SegmentalWebKeyRow>? WebRows { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? Offsets { get; set; }
    public double CenterDepth { get; set; } = double.NaN;
    public int FromSection { get; set; }
    public int ToSection { get; set; } = int.MaxValue;
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public SegmentalKeyDefinition Clone()
    {
        var c = (SegmentalKeyDefinition)MemberwiseClone();
        c.WebRows = WebRows?.Select(r => r.Clone()).ToList(); c.Offsets = Offsets?.ToList();
        return c;
    }
}

public enum SegmentalPtBarKind { Temporary, Permanent }

/// <summary>Bar position in the box section: X from CL girder toward the bar's web (mirrored for the left web); Depth below the deck top or Height above
/// the soffit (one of them, the other NaN).</summary>
public sealed class SegmentalBarPosition
{
    public double X { get; set; } = double.NaN;
    public double Depth { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    public SegmentalBarPosition Clone() => (SegmentalBarPosition)MemberwiseClone();
}

/// <summary>Which segments a PT bar connects: each matched segment with the next one toward the tip in its arm (erection joint by joint), or the first
/// segments D1 / U1 of the same bent (through the bent segment).</summary>
public enum SegmentalBarPairing { NextTowardTip, AcrossBent }

/// <summary>
/// OPTIONAL post-tensioning bars (1 3/4" erection bars, permanent bent bars) between the blocks of two segments: for every segment matched by
/// <see cref="Segments"/> (regex) the bar runs from the face of its <see cref="BlockType"/> block farther from the partner segment to the far face of
/// the partner's block (both blocks must be built), at every <see cref="Positions"/> entry on both webs (<see cref="Sides"/> 0) or, with
/// <see cref="Alternate"/>, at one position per joint taken in turn (staggered pattern). Bars are straight between the end points (section positions at
/// each end's station). Results: <c>SegmentalModel.PtHardware</c>; printed minimum lengths are check rows. Temporary bars of consecutive joints reuse
/// the block ducts: all are drawn.
/// </summary>
public sealed class SegmentalPtBarDefinition
{
    public string Name { get; set; } = "";
    public SegmentalPtBarKind Kind { get; set; }
    public string Segments { get; set; } = "";
    public SegmentalBarPairing Pairing { get; set; }
    public string BlockType { get; set; } = "";
    public List<SegmentalBarPosition> Positions { get; set; } = new();
    public bool Alternate { get; set; }
    public int Sides { get; set; }
    public double Diameter { get; set; } = double.NaN;
    public double DuctDiameter { get; set; } = double.NaN;
    public double PrintedMinLength { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Open items (UnresolvedFlag text): bars are built and flagged dimension-only.</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalPtBarDefinition Clone() { var c = (SegmentalPtBarDefinition)MemberwiseClone(); c.Positions = Positions.Select(p => p.Clone()).ToList(); return c; }
}

/// <summary>Which face of a segment <see cref="SegmentalFixedBarDefinition"/> positions are measured from: the face nearer the segment's own bent / support
/// control line (bent half: the CL split face; end segment: its anchor face at the support) or the other face.</summary>
public enum SegmentalFaceRef { SupportSide, FarSide }

/// <summary>Bar direction of a fixed bar: across the depth of the box (vertical PT bars: top end / bottom end) or along the box between two along positions.</summary>
public enum SegmentalFixedBarKind { Vertical, Longitudinal }

/// <summary>One bar of a <see cref="SegmentalFixedBarDefinition"/> in the segment's section frame (x from CL girder, + right; ends at depths / heights).
/// Vertical: at <see cref="Along"/>, top end at (X, TopDepth), bottom end at (XBottom or X, BottomHeight above the soffit). Longitudinal: from <see cref="Along"/>
/// to <see cref="AlongEnd"/> at (X, height above the soffit = BottomHeight, or TopDepth below the deck top).</summary>
public sealed class SegmentalFixedBarPosition
{
    public double Along { get; set; } = double.NaN;
    public double AlongEnd { get; set; } = double.NaN;
    public double X { get; set; } = double.NaN;
    public double XBottom { get; set; } = double.NaN;
    public SegmentalFixedBarPosition Clone() => (SegmentalFixedBarPosition)MemberwiseClone();
}

/// <summary>
/// OPTIONAL PT bars fixed inside one segment (vertical PT bars of bent / end segments, longitudinal bars within a segment): for every segment of
/// <see cref="SegmentType"/> whose name matches <see cref="Segments"/> (regex, empty = all of that type) one bar per <see cref="Positions"/> entry and side
/// (<see cref="Sides"/> 0 = mirrored about CL girder, else -1 / +1), positions along the CL girder from the <see cref="From"/> face. Ends: <see cref="TopDepth"/>
/// below the deck top, <see cref="BottomHeight"/> above the soffit (NaN = not given: not built). <see cref="ListedLength"/> = the printed bar length (check row).
/// </summary>
public sealed class SegmentalFixedBarDefinition
{
    public string Name { get; set; } = "";
    public SegmentalFixedBarKind Kind { get; set; }
    public SegmentType SegmentType { get; set; }
    public string Segments { get; set; } = "";
    public SegmentalFaceRef From { get; set; }
    public List<SegmentalFixedBarPosition> Positions { get; set; } = new();
    public int Sides { get; set; }
    public double TopDepth { get; set; } = double.NaN;
    public double BottomHeight { get; set; } = double.NaN;
    public double Diameter { get; set; } = double.NaN;
    public double DuctDiameter { get; set; } = double.NaN;
    public double ListedLength { get; set; } = double.NaN;
    /// <summary>Printed number of bars in the whole unit (check row; NaN = none).</summary>
    public double PlanCount { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Open items (UnresolvedFlag text): bars are built and flagged dimension-only.</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalFixedBarDefinition Clone() { var c = (SegmentalFixedBarDefinition)MemberwiseClone(); c.Positions = Positions.Select(p => p.Clone()).ToList(); return c; }
}

public enum SegmentalPipeKind { DeviationPipe, FuturePtPipe }

/// <summary>Where a pipe's along position comes from: its P.I. on the middle station of the host segment (deviation block pipes centred on the closure
/// joint), or its outer (L1) end on the end face of a bent half (diaphragm pipes; <see cref="SegmentalPipeDefinition.UpStationHalf"/> picks the half).</summary>
public enum SegmentalPipePlacement { PiAtSegmentCentre, OuterEndAtBentHalfFace }

/// <summary>
/// OPTIONAL bent steel pipe for future PT (plan "PIPE VARIABLES" tables): straight L1, arc of <see cref="Radius"/> (L2), straight L3; the tendon CL at the
/// L1 end / P.I. / L3 end is at heights <see cref="A"/> / <see cref="B"/> / <see cref="C"/> (above the soffit, or below the deck top with
/// <see cref="HeightsBelowDeck"/>) and horizontal offsets <see cref="D"/> / <see cref="F"/> / <see cref="G"/> from CL girder (left side mirrored). The
/// legs' along components follow from the tangent lengths L1 + R tan(delta / 2) (printed delta) and the offsets; the deflection that results is a
/// check row against the printed delta (never adjusted). Hosts: <see cref="Segments"/> (pipe P.I. at their middle station) or <see cref="Bents"/>.
/// </summary>
public sealed class SegmentalPipeDefinition
{
    public string Name { get; set; } = "";
    public SegmentalPipeKind Kind { get; set; }
    public SegmentalPipePlacement Placement { get; set; }
    public List<string> Segments { get; set; } = new();
    public List<string> Bents { get; set; } = new();
    /// <summary>-1 left web, +1 right web, 0 both (mirrored).</summary>
    public int Side { get; set; }
    /// <summary>OuterEndAtBentHalfFace: the pipe lies in the up-station bent half (outer end at its up-station end face; the anchor is down-station).</summary>
    public bool UpStationHalf { get; set; }
    public double A { get; set; } = double.NaN;
    public double B { get; set; } = double.NaN;
    public double C { get; set; } = double.NaN;
    public double D { get; set; } = double.NaN;
    public double F { get; set; } = double.NaN;
    public double G { get; set; } = double.NaN;
    public bool HeightsBelowDeck { get; set; }
    public double DeltaDeg { get; set; } = double.NaN;
    public double Radius { get; set; } = double.NaN;
    public double L1 { get; set; } = double.NaN;
    public double L2 { get; set; } = double.NaN;
    public double L3 { get; set; } = double.NaN;
    public double Length { get; set; } = double.NaN;
    /// <summary>Outside diameter / wall thickness of the pipe (4" Sch. 40: 4.5" / 0.237").</summary>
    public double OutsideDiameter { get; set; } = double.NaN;
    public double Wall { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public SegmentalPipeDefinition Clone() { var c = (SegmentalPipeDefinition)MemberwiseClone(); c.Segments = Segments.ToList(); c.Bents = Bents.ToList(); return c; }
}

/// <summary>Duct lane = a hole in the joint faces: X from CL girder (toward the tendon's web; the left tendon of a pair is mirrored) and Depth below the
/// deck top or Height above the soffit.</summary>
public sealed class SegmentalDuctLane
{
    public string Name { get; set; } = "";
    public double X { get; set; } = double.NaN;
    public double Depth { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    /// <summary>X measured inward from the soffit outside corner of the web (the lane follows the varying box width), not from CL girder.</summary>
    public bool FromSoffitCorner { get; set; }
    public SegmentalDuctLane Clone() => (SegmentalDuctLane)MemberwiseClone();
}

/// <summary>Lane of a tendon at one joint of an arm: Arm "D" (down-station of the bent) or "U"; Joint k = the tip-side (bulkhead) face of segment k of
/// that arm, 0 = the bent-side face of segment 1 (through the bent segment). Lane "" = not read (the tendon runs straight past that joint).</summary>
public sealed class SegmentalTendonLane
{
    public string Arm { get; set; } = "";
    public int Joint { get; set; }
    public string Lane { get; set; } = "";
    public SegmentalTendonLane Clone() => (SegmentalTendonLane)MemberwiseClone();
}

/// <summary>
/// OPTIONAL tendon deviation profile (plan "LONGITUDINAL POST-TENSIONING GEOMETRY DETAILS"). Dimensions are measured from the joint faces along CL girder.
/// Cantilever tendons: the duct crosses every joint face in its lane; where the lane changes the duct leaves the old lane at a P.I.
/// <see cref="ChangePiFromFace"/> from the face it entered and reaches the new lane at a P.I. the same distance before the next face (curves
/// <see cref="ChangeRadius"/>), so it is already in the new lane at the joint. In the anchoring segment it deviates from its lane (P.I.
/// <see cref="AnchorPiFromFace"/> from the anchor face) to the anchor <see cref="AnchorSetback"/> inside the tip face, in lane <see cref="AnchorLane"/>
/// (x) at <see cref="AnchorDepth"/> below the deck top (curve <see cref="AnchorRadius"/>). Continuity tendons anchored in a block (blister): the anchor is
/// on the anchor face of the <see cref="AnchorBlockType"/> block serving the tendon (block Label starts with the tendon name), <see cref="AnchorFromWeb"/>
/// from the web inside face and <see cref="AnchorAboveSlab"/> above the bottom slab top, P.I. <see cref="AnchorPiFromAnchor"/> from it.
/// </summary>
public sealed class SegmentalTendonProfile
{
    public string AnchorLane { get; set; } = "";
    public double AnchorSetback { get; set; } = double.NaN;
    public double AnchorDepth { get; set; } = double.NaN;
    public double AnchorPiFromFace { get; set; } = double.NaN;
    public double AnchorRadius { get; set; } = double.NaN;
    public double ChangePiFromFace { get; set; } = double.NaN;
    public double ChangeRadius { get; set; } = double.NaN;
    /// <summary>Lane-change P.I. distance from the segment's bent-side (match-cast) face; NaN = <see cref="ChangePiFromFace"/> (which is measured from the
    /// bulkhead face = the face farther from the pier).</summary>
    public double ChangePiFromBentFace { get; set; } = double.NaN;
    /// <summary>OPTIONAL per-segment lane-change P.I.s printed for particular tendons (both measured from the segment's bent-side face); a segment
    /// without an entry uses <see cref="ChangePiFromFace"/> / <see cref="ChangePiFromBentFace"/>. Null = none.</summary>
    public List<SegmentalLaneChangePi>? LaneChangePis { get; set; }
    /// <summary>OPTIONAL straight crossings of a joint face at a printed plan angle (the duct passes the joint hole on a straight diagonal; the P.I.s on both
    /// sides lie on that line, so there is no kink at the face). Null = every face crossed parallel to the lane.</summary>
    public List<SegmentalFaceCrossing>? FaceCrossings { get; set; }
    /// <summary>OPTIONAL anchor depth below the deck top for tendons anchored in a TOP slab anchor block (then <see cref="AnchorFromWeb"/> /
    /// <see cref="AnchorAboveSlab"/> are not used and the anchor lies in the tendon's lane in plan). NaN = bottom slab anchor.</summary>
    public double AnchorBelowTop { get; set; } = double.NaN;
    /// <summary>OPTIONAL lateral offset of a top slab anchor from the box CL (with <see cref="AnchorBelowTop"/>); NaN = in the tendon's lane.</summary>
    public double AnchorFromCl { get; set; } = double.NaN;
    /// <summary>OPTIONAL plan angle of a run of one-lane steps in consecutive segments (cantilever rule): the duct runs one straight diagonal through the joint
    /// holes at this angle, P.I.s only where the run leaves / enters a lane (p.303: 3.07 deg = 9" per 14'-0" segment). NaN = a P.I. pair in every segment.</summary>
    public double ChainAngleDeg { get; set; } = double.NaN;
    /// <summary>OPTIONAL comma list of the tendons the <see cref="ChainAngleDeg"/> diagonal applies to (p.303 dimensions it for T7-T12 only; T13 is drawn as a
    /// lane-parallel S-curve). Null = every tendon.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ChainTendons { get; set; }
    /// <summary>OPTIONAL anchor station of a block-anchored (blister) tendon measured along the box from the bent-side face of the anchor segment (p.298: C1 / C2
    /// 3'-4"). Null = the anchor face of the block (its end toward the bent).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? AnchorFromBentFace { get; set; }
    /// <summary>OPTIONAL printed plan exit angle at the anchor per tendon (cantilever rule; p.303: 5.40 deg T7-T12, 6.86 deg T3 / T5): the duct leaves the
    /// anchor straight at this angle to the anchor P.I.; the P.I. found so is compared (check row) with the P.I. on the lane / chain diagonal through the
    /// next joint hole. Null = the anchor P.I. on that lane / diagonal.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<SegmentalAnchorExit>? AnchorExits { get; set; }
    /// <summary>OPTIONAL end-span tendons (layout FromEndSegment): anchors on the END SEGMENT anchor face (p.272) per tendon; the tendon runs from there
    /// through its lanes to the anchor block in its span (as the continuity tendons). Null = end-span tendons keep the straight duct.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<SegmentalEndFaceAnchor>? EndFaceAnchors { get; set; }
    public string AnchorBlockType { get; set; } = "";
    public double AnchorFromWeb { get; set; } = double.NaN;
    public double AnchorAboveSlab { get; set; } = double.NaN;
    public double AnchorPiFromAnchor { get; set; } = double.NaN;
    /// <summary>Height above the bottom slab top at which <see cref="AnchorFromWeb"/> is measured from the web inside face (NaN = at the anchor).</summary>
    public double AnchorWebLevelAboveSlab { get; set; } = double.NaN;
    /// <summary>Continuity tendons: keep the lane read at the closure end through every joint (lane changes not modeled; flagged by the caller).</summary>
    public bool ClosureLaneOnly { get; set; }
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public SegmentalTendonProfile Clone() { var c = (SegmentalTendonProfile)MemberwiseClone(); c.LaneChangePis = LaneChangePis?.Select(o => o.Clone()).ToList(); c.FaceCrossings = FaceCrossings?.Select(o => o.Clone()).ToList(); c.AnchorExits = AnchorExits?.Select(o => o.Clone()).ToList(); c.EndFaceAnchors = EndFaceAnchors?.Select(o => o.Clone()).ToList(); return c; }
}

/// <summary>Anchor of the listed end-span tendons on the end segment's anchor face: offset from the box CL, height above the soffit (NaN = at the
/// lane height), offset where the duct leaves the end segment's span face (the galvanized pipe), bend radius there and the printed pipe bend (check).</summary>
public sealed class SegmentalEndFaceAnchor
{
    public string Tendons { get; set; } = "";
    public double FromCl { get; set; } = double.NaN;
    public double AboveSoffit { get; set; } = double.NaN;
    public double SpanFaceFromCl { get; set; } = double.NaN;
    public double BendRadius { get; set; } = double.NaN;
    public double BendDeg { get; set; } = double.NaN;
    /// <summary>OPTIONAL P.I. of the pipe bend measured along the box from the anchor face (p.272 Section A-A: 3'-6" for B1 / B2, 3'-10" for C1 / C2): the pipe runs
    /// straight from the anchor to this P.I. at the lane height of the closure joint hole (bend <see cref="BendRadius"/>), then in plan to the span face offset.
    /// Null = the bend at the span face (pipe straight from the anchor to the span face).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? BendPiFromAnchor { get; set; }
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public SegmentalEndFaceAnchor Clone() => (SegmentalEndFaceAnchor)MemberwiseClone();
}

/// <summary>Printed plan angle at which the listed tendons leave their anchor (toward the lane at the anchor segment's bent face).</summary>
public sealed class SegmentalAnchorExit
{
    /// <summary>Comma list of tendon names (e.g. "T7,T8").</summary>
    public string Tendons { get; set; } = "";
    public double AngleDeg { get; set; } = double.NaN;
    public string Source { get; set; } = "";
    public SegmentalAnchorExit Clone() => (SegmentalAnchorExit)MemberwiseClone();
}

/// <summary>Lane-change P.I.s of the listed tendons in segment k of each arm (k counted from the bent, as XX-Dk / XX-Uk; 0 = every segment where the tendon changes
/// lane), both from the segment's bent-side face.</summary>
public sealed class SegmentalLaneChangePi
{
    /// <summary>Comma list of tendon names (e.g. "B1,B6").</summary>
    public string Tendons { get; set; } = "";
    public int Segment { get; set; }
    public double FirstPiFromBentFace { get; set; } = double.NaN;
    public double SecondPiFromBentFace { get; set; } = double.NaN;
    /// <summary>OPTIONAL second P.I. measured from the segment's bulkhead face (the face farther from the pier) instead; wins over <see cref="SecondPiFromBentFace"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? SecondPiFromBulkheadFace { get; set; }
    /// <summary>OPTIONAL radius of these two curves; null = the profile's ChangeRadius.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? Radius { get; set; }
    public string Source { get; set; } = "";
    public SegmentalLaneChangePi Clone() => (SegmentalLaneChangePi)MemberwiseClone();
}

/// <summary>Straight crossing of joint k (tip face of segment k, counted from the bent) by the listed tendons at a plan angle to the lane: P.I.s on the line through
/// the joint hole PiBeforeFace inside segment k and PiAfterFace inside segment k + 1.</summary>
public sealed class SegmentalFaceCrossing
{
    public string Tendons { get; set; } = "";
    public int Joint { get; set; }
    public double AngleDeg { get; set; } = double.NaN;
    public double PiBeforeFace { get; set; } = double.NaN;
    public double PiAfterFace { get; set; } = double.NaN;
    public string Source { get; set; } = "";
    public SegmentalFaceCrossing Clone() => (SegmentalFaceCrossing)MemberwiseClone();
}
