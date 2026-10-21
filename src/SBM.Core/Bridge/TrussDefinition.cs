using System.Text.Json.Serialization;

namespace SBM.Core.Bridge;

/// <summary>
/// Steel TRUSS superstructure (OPTIONAL, <see cref="BridgeDefinition.Truss"/>; null = none, omitted from the .sbm). Generic for riveted / bolted
/// trusses of any form (Pratt, Warren, K, deck or through, continuous, arch-type with a suspended floor): the bridge that carries it has no
/// girders (<c>Girders.Count = 0</c>, support lines only, like the tied arch); generator <c>SBM.Core.Truss.TrussGenerator</c>. Geometry is
/// given as NAMED PANEL POINTS (station along the alignment + elevation) instantiated on each truss PLANE (offset from the alignment), MEMBERS
/// between named points with a generic built-up SECTION, JOINTS (gusset plates, rivet groups), the floor system (floor beams are members,
/// stringer lines + deck slab are generated), rope SUSPENDERS and SHOES. Meters internally. Design note:
/// docs/plan-data/ptc-delaware-river/TRUSS-DESIGN.md. Every value the plans do not give is an <c>Unresolved</c> text (page + plain reason);
/// the generator never invents a dimension silently: undimensioned members use a <see cref="TrussSectionKind.Nominal"/> envelope, flagged.
/// </summary>
public sealed class TrussDefinition
{
    /// <summary>Plan reference printed with every flag (e.g. "PTC DRB-4").</summary>
    public string Source { get; set; } = "";
    /// <summary>Name of the truss (database group, IFC assembly descriptions), e.g. "Main truss spans 18-20".</summary>
    public string Name { get; set; } = "Truss";
    /// <summary>Truss planes (lines): usually two at +/- c-c / 2 from the alignment.</summary>
    public List<TrussPlaneDefinition> Planes { get; set; } = new();
    /// <summary>Panel points by name (L0..Ln, U0..Un, W14.. ...), instantiated on every plane (or the listed ones).</summary>
    public List<TrussPointDefinition> Points { get; set; } = new();
    /// <summary>Member sections by name (members refer to them).</summary>
    public List<TrussSectionDefinition> Sections { get; set; } = new();
    public List<TrussMemberDefinition> Members { get; set; } = new();
    /// <summary>Joint details (gusset plates, rivet groups) by point; points without an entry get <see cref="JointDefaults"/> when
    /// <see cref="TrussJointDefaults.AutoGussets"/> is on.</summary>
    public List<TrussJointDefinition> Joints { get; set; } = new();
    public TrussJointDefaults JointDefaults { get; set; } = new();
    /// <summary>Stringers + deck slab (floor beams are <see cref="Members"/> with role FloorBeam); null = none.</summary>
    public TrussFloorDefinition? Floor { get; set; }
    /// <summary>Rope suspenders (suspended floors / hangers of wire rope).</summary>
    public List<SuspenderDefinition> Suspenders { get; set; } = new();
    public List<TrussShoeDefinition> Shoes { get; set; } = new();
    /// <summary>Material name per plan material letter (e.g. "C" -> "ASTM A7 carbon steel", "S" -> "ASTM A94 silicon steel").</summary>
    public Dictionary<string, string> Materials { get; set; } = new();
    /// <summary>Material of members without a letter.</summary>
    public string DefaultMaterial { get; set; } = "ASTM A7";
    /// <summary>Plan steel weight (lb) to compare with the model (0 = none) and where it is printed.</summary>
    public double PlanSteelWeightLb { get; set; }
    public string PlanSteelWeightSource { get; set; } = "";
    /// <summary>Plan items that are open (each an UNRESOLVED warning, text built with <c>UnresolvedFlag.Text</c>).</summary>
    public List<string> Unresolved { get; set; } = new();
    /// <summary>OPTIONAL (null = none, omitted from the .sbm): constant-section items swept along the alignment (curbs / parapets, walkway gratings,
    /// stringers and hand rails) or across it at a station (expansion dam plates), positioned from the finished grade (<see cref="TrussLineItemDefinition"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussLineItemDefinition>? LineItems { get; set; }
    /// <summary>OPTIONAL (omitted when false): rivet heads that would bear into a shoe sole plate (rivet lines of plate girders / joint rivets over the
    /// shoe) are COUNTERSUNK and chipped flush on that face (the sole plate bears on a finished surface): the rivet keeps its button head on the other
    /// face only (<see cref="Steel.BoltInstance.FlushEnd"/>); one Info line counts them.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RivetHeadsFlushAtSolePlates { get; set; }

    public TrussSectionDefinition? Section(string name) => Sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    public TrussPlaneDefinition? Plane(string name) => Planes.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public TrussDefinition Clone() => System.Text.Json.JsonSerializer.Deserialize<TrussDefinition>(System.Text.Json.JsonSerializer.Serialize(this, CloneJson), CloneJson)!;

    private static readonly System.Text.Json.JsonSerializerOptions CloneJson = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Input errors (generator does not run).</summary>
    public IEnumerable<string> Validate()
    {
        if (Planes.Count == 0) yield return "Truss: at least one truss plane is required.";
        foreach (var g in Planes.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)) yield return $"Truss: plane name '{g.Key}' is used twice.";
        foreach (var g in Sections.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)) yield return $"Truss: section name '{g.Key}' is used twice.";
        foreach (var m in Members)
        {
            if (string.IsNullOrWhiteSpace(m.Name)) yield return "Truss: a member has no name.";
            if (Section(m.Section) == null) yield return $"Truss: member '{m.Name}' uses the undefined section '{m.Section}'.";
        }
    }
}

/// <summary>One truss plane (line): the panel points are placed at this offset from the alignment (right +), the plane through them is
/// vertical unless tilted (<see cref="TiltDegrees"/>, rotation about the longitudinal line, top toward the right +).</summary>
public sealed class TrussPlaneDefinition
{
    public string Name { get; set; } = "";
    public double Offset { get; set; }
    public double TiltDegrees { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>Named panel point: station along the alignment and elevation (meters). <see cref="Offset"/> (OPTIONAL) moves it off the plane offset
/// (e.g. lateral bracing points); <see cref="Planes"/> (OPTIONAL) limits it to some planes; a point listed again with Planes overrides it there.</summary>
public sealed class TrussPointDefinition
{
    public string Name { get; set; } = "";
    public double Station { get; set; }
    public double Elevation { get; set; }
    /// <summary>Offset from the PLANE (m, + = right of the alignment direction); 0 = on the plane.</summary>
    public double Offset { get; set; }
    public List<string>? Planes { get; set; }
    /// <summary>Plan reference of the value (e.g. "p.69 elevation table").</summary>
    public string Source { get; set; } = "";
    public TrussPointDefinition() { }
    public TrussPointDefinition(string name, double station, double elevation, string source = "") { Name = name; Station = station; Elevation = elevation; Source = source; }
}

public enum TrussSectionKind
{
    /// <summary>Riveted box: two web plate groups (plies), 4 angles at the web edges (inside or outside), top / bottom cover plates, lacing on open faces.</summary>
    Box,
    /// <summary>Riveted H / I: one web plate + 4 flange angles (+ flange plates); web across the truss plane (web members) or vertical (floor beams).</summary>
    H,
    /// <summary>Two channels (webs parallel to the truss plane) with lacing bars or batten plates on the open faces.</summary>
    LacedChannels,
    /// <summary>Two angles back to back (gap between) or four angles in a star / box (bracing).</summary>
    Angles,
    /// <summary>Rolled AISC shape by exact designation (W / C / MC / L / WT), or explicit dimensions for shapes not in AISC v16 (1950s rolled shapes).</summary>
    Rolled,
    /// <summary>Flat plate / bar (Width across x Depth up).</summary>
    Plate,
    /// <summary>Round bar or rope (Diameter).</summary>
    Round,
    /// <summary>Undimensioned member: rectangular ENVELOPE Width x Depth, always flagged UNRESOLVED (dimension).</summary>
    Nominal,
}

/// <summary>A plate in a stack (cover plates, flange plates): width x thickness, optional perforations (not cut, noted).</summary>
public sealed class TrussPlate
{
    public double Width { get; set; }
    public double Thickness { get; set; }
    /// <summary>Perforated cover plate (access holes): built solid, the holes are reported (they are rarely dimensioned).</summary>
    public bool Perforated { get; set; }
    /// <summary>Plan label, e.g. "1-PL (24-12) x 3/8".</summary>
    public string Label { get; set; } = "";
    /// <summary>OPTIONAL length along the member (m); 0 = the full member (partial-length cover plates of plate girders / floor beams).</summary>
    public double Length { get; set; }
    /// <summary>OPTIONAL distance of the centre of a partial plate from the member mid-length (m, + toward the member end); 0 = centred.</summary>
    public double Offset { get; set; }
    /// <summary>OPTIONAL distance of a partial plate's start from the member's START working point (m, along the axis); set = <see cref="Offset"/> ignored.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Start { get; set; }
    /// <summary>OPTIONAL: the plate runs between the gusset plates of the member's two end joints (riveted cover plates that stop where the gussets / splice
    /// plates begin; gusset envelope or the joint's outline). It does not count for the member width at the joints.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BetweenGussets { get; set; }
    /// <summary>OPTIONAL (null = none, omitted from the .sbm): access holes CUT in a perforated plate (<see cref="TrussPerforation"/>); the plate is then built
    /// as edge strips + the pieces between the holes (exact extrusions) instead of solid.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrussPerforation? Perforation { get; set; }
    public TrussPlate() { }
    public TrussPlate(double width, double thickness, bool perforated = false, string label = "") { Width = width; Thickness = thickness; Perforated = perforated; Label = label; }
}

/// <summary>One web ply of a built-up member (depth 0 = the section's <see cref="TrussSectionDefinition.WebDepth"/>).</summary>
public sealed class TrussPly
{
    public double Depth { get; set; }
    public double Thickness { get; set; }
    public string Label { get; set; } = "";
    /// <summary>OPTIONAL length of a partial-length ply along the member (m); 0 = the full member (e.g. local web reinforcing plates of a post).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Length { get; set; }
    /// <summary>OPTIONAL start of a partial ply from the member's START working point (m, along the axis); null = centred on the member.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Start { get; set; }
    /// <summary>OPTIONAL (null = none): access holes cut in a perforated web ply (manholes of box struts), see <see cref="TrussPerforation"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrussPerforation? Perforation { get; set; }
    public TrussPly() { }
    public TrussPly(double depth, double thickness, string label = "") { Depth = depth; Thickness = thickness; Label = label; }
}

/// <summary>
/// Access holes of a perforated cover plate / web plate (riveted members of the 1930s-1950s: "1-Pl (24-12) x 5/8" = a 24 in. plate with 12 in. wide
/// holes; "12 x 20 M.H. @ 3'-6"): obround holes <see cref="Width"/> across the plate (semicircular ends of radius Width / 2) x <see cref="Length"/> along
/// the member, centres <see cref="Pitch"/> apart on the plate's centre line (+ <see cref="CenterOffset"/> across), laid out symmetrically about the middle
/// of the free length between the member's end gussets (or the plate's own ends when it is a partial plate) as many as fit with at least
/// <see cref="EndClear"/> from those ends to the hole ends. <see cref="Stagger"/> shifts this plate's holes by half a pitch (holes alternated on the
/// opposite faces of the member). The plate is built as two edge strips swept along the member plus the pieces between the holes (planar polygons with
/// the arc ends, swept through the thickness): every piece an exact extrusion; mass by volume (holes removed).
/// </summary>
public sealed class TrussPerforation
{
    public double Width { get; set; }
    public double Length { get; set; }
    public double Pitch { get; set; }
    public double EndClear { get; set; }
    public bool Stagger { get; set; }
    public double CenterOffset { get; set; }
    /// <summary>Plan callout, e.g. "12 x 20 M.H. @ 3'-6 ctrs".</summary>
    public string Label { get; set; } = "";
    /// <summary>UNRESOLVED text (page + reason) for values not printed (end distance, stagger): dimension-only flag on the member.</summary>
    public string Unresolved { get; set; } = "";
}

/// <summary>Lacing bars on an open face of a built-up member (single = zigzag, double = crossed), between the rivet lines.</summary>
public sealed class TrussLacing
{
    public double BarWidth { get; set; }
    public double BarThickness { get; set; }
    /// <summary>Angle of the bars to the member axis (degrees; AASHTO 1953: 60 single / 45 double).</summary>
    public double AngleDegrees { get; set; } = 60;
    public bool Double { get; set; }
    /// <summary>Faces: "top", "bottom" or "both" (local +y / -y faces for Box and LacedChannels).</summary>
    public string Faces { get; set; } = "bottom";
    /// <summary>Distance between the rivet lines the bars span (0 = 3/4 of the face width).</summary>
    public double RivetLines { get; set; }
    public string Unresolved { get; set; } = "";
}

/// <summary>
/// Member section. Local axes: x = across the truss plane (plane normal), y = up in the plane perpendicular to the member axis (cross-plane members:
/// x horizontal perpendicular to the axis, y up). The working line passes through the section origin: Box / H at mid-depth of the web(s),
/// rolled shapes at mid-depth, plates / round / nominal at the centre; <see cref="WorkingLineOffset"/> moves the section up (+y).
/// </summary>
public sealed class TrussSectionDefinition
{
    public string Name { get; set; } = "";
    public TrussSectionKind Kind { get; set; }
    /// <summary>Printed section text (plan table), used as the part description.</summary>
    public string Label { get; set; } = "";
    /// <summary>Plan material letter (key of <see cref="TrussDefinition.Materials"/>) or a material name; "" = default.</summary>
    public string Material { get; set; } = "";
    // ---- Box / H
    /// <summary>Web plate depth (Box: each web; H: the web).</summary>
    public double WebDepth { get; set; }
    /// <summary>Web plies of ONE side (Box: stacked from the OUTSIDE face inward in this order; plies of different depth are centred on the
    /// working line), or the single web (H, first entry).</summary>
    public List<TrussPly> WebPlies { get; set; } = new();
    /// <summary>Box: out-to-out of the two web groups (outer faces; 0 = from <see cref="WebsBackToBack"/>).</summary>
    public double WebsOutToOut { get; set; }
    /// <summary>Box: back to back of the inside angles = distance between the faces they stand on (the innermost plies deeper than the angle leg zone);
    /// without inside angles the clear distance between the innermost plies (0 = from <see cref="WebsOutToOut"/>).</summary>
    public double WebsBackToBack { get; set; }
    /// <summary>Angle designation ("L8x6x3/4", AISC v16 when it exists) or empty with <see cref="AngleLegOnWeb"/> / <see cref="AngleLegOut"/> / <see cref="AngleThickness"/>.</summary>
    public string Angle { get; set; } = "";
    public double AngleLegOnWeb { get; set; }
    public double AngleLegOut { get; set; }
    public double AngleThickness { get; set; }
    /// <summary>Box: the angles' outstanding legs point OUT of the box (false = into the box: backs against the inner face of the innermost ply
    /// that reaches the angle's leg on the web).</summary>
    public bool AnglesOutside { get; set; }
    /// <summary>Box: angles only at the top (2) / bottom (2) web edges or both (4): "both" | "top" | "bottom" | "none".</summary>
    public string AngleEdges { get; set; } = "both";
    /// <summary>Distance the angle backs stand above / below the web edge (riveted chords: webs 1/4" - 1/2" below the angle backs; 0 = flush).</summary>
    public double AngleProjection { get; set; }
    /// <summary>Box: plates on top (+y) / bottom (-y) stacked outward; H: flange plates on both web edges; Rolled (OPTIONAL): cover plates on the outer
    /// faces of the two flanges (Top = the +depth flange, Bottom = the other).</summary>
    public List<TrussPlate> TopPlates { get; set; } = new();
    public List<TrussPlate> BottomPlates { get; set; } = new();
    public TrussLacing? Lacing { get; set; }
    /// <summary>H: true = web vertical (y, floor beams), false = web across the plane (x, truss web members connected by their flanges to the gussets).</summary>
    public bool WebVertical { get; set; }
    // ---- channels / angles
    public string Channel { get; set; } = "";
    /// <summary>LacedChannels: out-to-out of the channel webs (backs); Angles: gap between the angle backs.</summary>
    public double BackToBack { get; set; }
    public bool FlangesOut { get; set; }
    /// <summary>Angles: 2 (back to back, legs down) or 4 (box / star).</summary>
    public int AngleCount { get; set; } = 2;
    // ---- rolled
    /// <summary>Rolled: AISC designation (W24x76, C15x33.9, L6x4x1/2, WT...). Not in AISC v16 -> the explicit dimensions below (or Nominal).</summary>
    public string Designation { get; set; } = "";
    public double Depth { get; set; }
    public double FlangeWidth { get; set; }
    public double WebThickness { get; set; }
    public double FlangeThickness { get; set; }
    /// <summary>Rolled: plan / handbook weight (lb/ft) of a shape given by dimensions (0 = volume x density).</summary>
    public double WeightLbPerFt { get; set; }
    /// <summary>Rolled: web horizontal (shape turned 90 degrees).</summary>
    public bool Rotated { get; set; }
    /// <summary>OPTIONAL gross area of ONE rolled shape / channel as printed (m2; 0 = the catalog / dimension polygon): the web thickness of the built polygon
    /// is adjusted so its area matches (1950s shapes whose handbook areas differ from AISC v16 / the sharp-root polygon). Rolled and LacedChannels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double ShapeArea { get; set; }
    // ---- plate / round / nominal
    public double Width { get; set; }
    public double Diameter { get; set; }
    public double WorkingLineOffset { get; set; }
    /// <summary>Open dimension of this section (UNRESOLVED text; members stay on their layer, flagged dimension-only). Nominal sections need it.</summary>
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

public enum TrussMemberRole
{
    UpperChord, LowerChord, WindChord, Diagonal, Vertical, Post, Hanger, EndPost,
    UpperLateral, LowerLateral, Sway, Portal, Strut, FloorBeam, Stringer, Other,
    /// <summary>Riveted / built-up plate girder along a line (appended): in-plane member with no gussets, ends at its points (simple spans between bearings).</summary>
    Girder,
}

/// <summary>
/// One member between two named points. <see cref="From"/> / <see cref="To"/> are point names on the member's plane ("U3") or "plane:point"
/// ("N:U3") for members between planes (floor beams, laterals, sway frames, portals). <see cref="Plane"/> "" with plane-local ends = one
/// member per plane, marked "{plane}-{Name}".
/// </summary>
public sealed class TrussMemberDefinition
{
    public string Name { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    /// <summary>Plane name, "" = every plane (plane-local ends) or none (both ends qualified "plane:point").</summary>
    public string Plane { get; set; } = "";
    public string Section { get; set; } = "";
    public TrussMemberRole Role { get; set; }
    /// <summary>Panel label for the database grouping (e.g. "Panel 3"); "" = none.</summary>
    public string Panel { get; set; } = "";
    /// <summary>Bracing system / assembly name for cross-plane members ("Upper laterals", "Sway frames", "Portal U11"); "" = by role.</summary>
    public string System { get; set; } = "";
    /// <summary>Member end distances from the points along the axis; null = automatic (chords: square at the point with the joint overlap; web
    /// members: where they leave the chord envelope; cross-plane members: at the in-plane members' outer face).</summary>
    public double? StartSetback { get; set; }
    public double? EndSetback { get; set; }
    /// <summary>OPTIONAL (0 = none, omitted from the .sbm): TAPERED member (e.g. a floor beam bracket whose bottom flange slopes up toward the tip):
    /// the part of the section BELOW its axis (section y &lt; 0: lower web, bottom angles, bottom plates) is raised by this amount at the START cut end
    /// of the member (m), linear to <see cref="DepthReductionEnd"/> at the end cut; the top stays straight. Lacing, stiffeners and rivet lines are not tapered.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DepthReductionStart { get; set; }
    /// <summary>OPTIONAL (0 = none): depth reduction at the END cut (see <see cref="DepthReductionStart"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DepthReductionEnd { get; set; }
    /// <summary>Shift of the working line along the section y (e.g. floor beam axis below the panel point).</summary>
    public double OffsetZ { get; set; }
    /// <summary>Plan member length (check only, m; 0 = none).</summary>
    public double PlanLength { get; set; }
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL (null = none, omitted from the .sbm): web stiffeners / web splice plates riveted to the member's web
    /// (riveted plate girders, floor beams; <see cref="TrussStiffenerSet"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussStiffenerSet>? Stiffeners { get; set; }
    /// <summary>OPTIONAL (null = none, omitted from the .sbm): rivet lines along the member (<see cref="TrussRivetLine"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussRivetLine>? RivetLines { get; set; }
    /// <summary>OPTIONAL (null = none, omitted from the .sbm): end connection angles / plates of a framed member (floor beam / bracket to a truss post,
    /// girder or hanger; <see cref="TrussEndConnection"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussEndConnection>? EndConnections { get; set; }
}

public enum TrussMemberEnd { Start, End, Both }

/// <summary>A column of rivets: <see cref="Count"/> rivets <see cref="Pitch"/> apart along the section y (down from the piece top), the first
/// <see cref="First"/> below it; <see cref="Gage"/> = position of the column (see the owner).</summary>
public sealed class TrussRivetColumn
{
    public double Gage { get; set; }
    public int Count { get; set; }
    public double Pitch { get; set; }
    public double First { get; set; }
    public string Label { get; set; } = "";
    public TrussRivetColumn() { }
    public TrussRivetColumn(double gage, int count, double pitch, double first) { Gage = gage; Count = count; Pitch = pitch; First = first; }
}

/// <summary>
/// End connection of a framed member (generic riveted practice: floor beam / bracket end angles on a post, girder or hanger). At the member end(s)
/// (<see cref="End"/>), on BOTH web faces (section x = +-web thickness / 2): an angle <see cref="Angle"/> (first leg = OUTSTANDING leg, its back on the
/// connection face; second leg on the web, pointing into the member) <see cref="Length"/> long down from section y <see cref="Top"/>, optionally on a
/// fill (<see cref="FillThickness"/> x <see cref="FillWidth"/> from the back along the member, <see cref="FillLength"/> down from <see cref="FillTop"/>), or a
/// plate pair (<see cref="PlateWidth"/> > 0: end / pin plates <see cref="PlateWidth"/> along the member from the back x <see cref="PlateThickness"/>).
/// Back of the outstanding legs / plate end at <see cref="Back"/> from the member's end WORKING POINT along the axis toward the member, or (null) on the
/// first steel face the member axis meets beyond its cut end (gusset, post flange, girder web; probed at the top, middle and bottom of the angle, the
/// nearest face wins). <see cref="ExtendWeb"/> extends the member web (H / rolled sections) from its cut end to <see cref="WebClearance"/> short of the back
/// over the angle height (the flanges stay at the member setback). Rivets: <see cref="WebRivets"/> (gage = distance from the back along the member) through
/// the web stack, shank across the web, <see cref="OutRivets"/> (gage = distance from the web face across, along the outstanding leg, on each face) through
/// the outstanding leg and the steel behind it, shank along the member; the grip of every rivet is the CONTIGUOUS steel found along its shank
/// (connection parts + the members / gussets built before). All pieces are exact extrusions.
/// </summary>
public sealed class TrussEndConnection
{
    public TrussMemberEnd End { get; set; } = TrussMemberEnd.Both;
    public string Label { get; set; } = "";
    /// <summary>"L4x4x3/4" (AISC v16 or parsed); "" = no angle (plates / web extension + rivets only).</summary>
    public string Angle { get; set; } = "";
    /// <summary>Section y of the top of the angles / plates (m; relative to the member working line incl. <see cref="TrussMemberDefinition.OffsetZ"/>).</summary>
    public double Top { get; set; }
    public double Length { get; set; }
    public double? Back { get; set; }
    /// <summary>With <see cref="Back"/> null: the probed face + this distance (m, + toward the member), e.g. a gap left for a fill not modeled.</summary>
    public double BackOffset { get; set; }
    public bool ExtendWeb { get; set; }
    public double WebClearance { get; set; } = 0.0127;
    public double FillThickness { get; set; }
    public double FillWidth { get; set; }
    public double FillLength { get; set; }
    public double? FillTop { get; set; }
    public double PlateWidth { get; set; }
    public double PlateThickness { get; set; }
    /// <summary>Plate outline (u = distance from the back along the member, v = section y), replacing the PlateWidth x [Top - Length, Top] rectangle.</summary>
    public List<TrussUV>? PlateOutline { get; set; }
    /// <summary>The angle heel / plate inner face stands this far outside the web face (it lies on another connection plate, e.g. a pin plate on an end
    /// plate); nothing is built for the stand-off.</summary>
    public double StandOff { get; set; }
    public List<TrussRivetColumn>? WebRivets { get; set; }
    public List<TrussRivetColumn>? OutRivets { get; set; }
    public double RivetDiameter { get; set; }
    /// <summary>Diameter of the outstanding-leg rivets when it differs (field rivets into the post / hanger; 0 = <see cref="RivetDiameter"/>).</summary>
    public double OutRivetDiameter { get; set; }
    public string? RivetMaterial { get; set; }
    public string? RivetUnresolved { get; set; }
    public string Material { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>
/// Web stiffeners of a riveted member (generic): at every <see cref="Distances"/> (along the member axis from the START working point, m) on both
/// faces of the web (section x = +-web thickness / 2), <see cref="PerFace"/> angles (<see cref="Angle"/>, e.g. "L6x4x7/16": the first leg stands out,
/// the second lies on the web; two per face are placed back to back) or, when <see cref="PlateWidth"/> > 0, a plate PlateWidth (along the member)
/// x <see cref="PlateThickness"/> (web splice plates). Height <see cref="Height"/> centred on the web (section y), normal to the member axis.
/// </summary>
public sealed class TrussStiffenerSet
{
    public string Label { get; set; } = "";
    public string Angle { get; set; } = "";
    public int PerFace { get; set; } = 1;
    public double PlateWidth { get; set; }
    public double PlateThickness { get; set; }
    public double Height { get; set; }
    public List<double> Distances { get; set; } = new();
    public string Material { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL (0 = none): fill plate between the web and the web leg of each angle (thickness; the angle stands out by it), <see cref="FillWidth"/>
    /// along the member from the heel (0 = the web leg), full stiffener height.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double FillThickness { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double FillWidth { get; set; }
    /// <summary>OPTIONAL section y of the stiffener top (null = the stiffener is centred on the web); e.g. stiffeners of a tapered bracket hang from the top.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Top { get; set; }
    /// <summary>OPTIONAL height of the fills (0 = the stiffener height), centred on it (fills between the flange angle legs).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double FillHeight { get; set; }
    /// <summary>OPTIONAL (null = none): rivet columns through the web leg / fill / web (gage = distance from the heel along the member, toward the leg),
    /// grip = the contiguous steel on the shank line; diameter <see cref="RivetDiameter"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussRivetColumn>? Rivets { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RivetDiameter { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RivetUnresolved { get; set; }
    /// <summary>OPTIONAL: +1 / -1 = the web leg of each (single) angle points toward the member end / start; 0 = the <see cref="PerFace"/> rule.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LegToward { get; set; }
}

/// <summary>Defaults for the joints without their own <see cref="TrussJointDefinition"/>.</summary>
public sealed class TrussJointDefaults
{
    /// <summary>Gusset plates (two, one each side of the chord) at every point where a web member meets a chord.</summary>
    public bool AutoGussets { get; set; } = true;
    /// <summary>Gusset thickness (m); 0 = not given (built 3/4" and flagged).</summary>
    public double GussetThickness { get; set; }
    /// <summary>Length of each member covered by the gusset beyond the member end (0 = the member depth).</summary>
    public double Lap { get; set; }
    /// <summary>Gusset edge distance beyond the member outlines (envelope).</summary>
    public double Margin { get; set; }
    /// <summary>Clear distance between a web member end and the chord envelope.</summary>
    public double WebMemberClearance { get; set; }
    /// <summary>Why the joint outlines are envelopes (UNRESOLVED text, page + reason); "" = a generic text.</summary>
    public string Unresolved { get; set; } = "";
    public double RivetDiameter { get; set; }
}

/// <summary>Joint (panel point) detail: gusset plates and rivet groups.</summary>
public sealed class TrussJointDefinition
{
    public string Point { get; set; } = "";
    /// <summary>Plane name, "" = every plane.</summary>
    public string Plane { get; set; } = "";
    public double GussetThickness { get; set; }
    /// <summary>Gusset outline in joint coordinates (u along the bridge in the plane, v up, origin = the point), meters; empty = envelope (flagged).</summary>
    public List<TrussUV> Outline { get; set; } = new();
    /// <summary>Gussets each side at this out-to-out distance (0 = outside the chord's outer face).</summary>
    public double GussetsInsideFaceDistance { get; set; }
    public List<TrussRivetGroup> Rivets { get; set; } = new();
    /// <summary>No gussets at this point (e.g. a chord splice only).</summary>
    public bool NoGusset { get; set; }
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL (null = none, omitted from the .sbm): splice plates, inlay / web splice plates and fills on the chord(s) at this point
    /// (<see cref="TrussJointPlate"/>). Built on every plane the joint applies to.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussJointPlate>? Plates { get; set; }
    /// <summary>OPTIONAL (null = one gusset plate per face, omitted from the .sbm): further gusset plates STACKED on each face outside the
    /// <see cref="GussetThickness"/> gusset (which stays the inner layer against the members / fills, at <see cref="GussetsInsideFaceDistance"/>).
    /// List order = from the inner gusset outward; each layer lies on the outer face of the previous one (constant offset = sum of the thicknesses
    /// under it). Rivet heads sit on the outer face of the stack where they pass through it and the grip includes every layer there.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussGussetLayer>? GussetLayers { get; set; }
}

/// <summary>
/// An additional gusset plate layer of a joint (<see cref="TrussJointDefinition.GussetLayers"/>): thickness and its own outline in the joint frame
/// (u along the bridge toward the higher station in the truss plane, v up, origin = the point, meters). Empty outline = the joint's gusset outline.
/// The outline is clipped to the inner gusset's outline when <see cref="ClipToGusset"/> (a layer cannot hang past the plate it is riveted to).
/// </summary>
public sealed class TrussGussetLayer
{
    public double Thickness { get; set; }
    public List<TrussUV> Outline { get; set; } = new();
    public bool ClipToGusset { get; set; } = true;
    /// <summary>Plan callout, e.g. "2-3/4 Gussets".</summary>
    public string Label { get; set; } = "";
    /// <summary>Plan material letter / name (resolved through <see cref="TrussDefinition.Materials"/>); "" = the inner gusset's material.</summary>
    public string Material { get; set; } = "";
    /// <summary>UNRESOLVED text (page + reason) -> dimension-only flag on the layer plates (e.g. a scaled outline).</summary>
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>Face of a chord a <see cref="TrussJointPlate"/> lies on.</summary>
public enum TrussPlateFace
{
    /// <summary>On the top of the chord (above its top cover plate), across the section.</summary>
    Top,
    /// <summary>Under the bottom of the chord.</summary>
    Bottom,
    /// <summary>Against the INSIDE face of both webs of a box chord (one plate per web).</summary>
    WebInside,
    /// <summary>Against the OUTSIDE face of both webs (one plate per web; under the gussets where they overlap).</summary>
    WebOutside,
}

/// <summary>
/// Splice / fill plate on a chord at a joint (generic riveted splice): rectangular plate <see cref="Width"/> (across the face: section x for Top / Bottom,
/// section y for the webs) x <see cref="Thickness"/> x <see cref="Length"/> (along the chord), centred <see cref="Offset"/> from the point along the chord
/// (+ toward the higher station). Plates on one face are stacked outward in list order starting at the face of the chord member(s) that lie under
/// them (the deeper member wins where two meet), so a fill listed before a splice plate makes up the thickness difference as on the plans.
/// </summary>
public sealed class TrussJointPlate
{
    public TrussPlateFace Face { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public double Length { get; set; }
    public double Offset { get; set; }
    /// <summary>Fill plate (PartCategory.FillerPlate) instead of a splice plate.</summary>
    public bool Fill { get; set; }
    /// <summary>Plan callout, e.g. "2-Spl. Pls. 24 x 1/2".</summary>
    public string Label { get; set; } = "";
    /// <summary>Plan material letter / name (resolved through <see cref="TrussDefinition.Materials"/>).</summary>
    public string Material { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

public enum TrussLineItemKind { Steel, Concrete }

/// <summary>
/// Constant-section item swept along the alignment at a reference offset (longitudinal: curb + parapet, walkway grating, walkway stringer, hand rail)
/// or across it at a station (transverse: expansion dam plates, edge channels). The <see cref="Profile"/> is in (U, V): longitudinal U = horizontal
/// distance from the reference offset toward <see cref="Side"/>, transverse U = distance along the alignment from <see cref="Station"/>; V = height above
/// the FINISHED GRADE at the reference point of the ring (plumb, rigid). Concrete items go to the bridge's concrete barriers list (IFC IfcRailing
/// GUARDRAIL / database "barrier"), steel items are steel parts of <see cref="Category"/> (excluded from the steel weight when the category is railing /
/// joint hardware) in the assembly <see cref="Assembly"/>.
/// </summary>
public sealed class TrussLineItemDefinition
{
    public string Name { get; set; } = "";
    public TrussLineItemKind Kind { get; set; }
    public Steel.PartCategory Category { get; set; } = Steel.PartCategory.WalkwayPlate;
    /// <summary>Across the alignment at <see cref="Station"/> (between offsets <see cref="From"/> and <see cref="To"/>) instead of along it.</summary>
    public bool Transverse { get; set; }
    /// <summary>Longitudinal: offset of the reference line (m, right +). Ignored when transverse.</summary>
    public double ReferenceOffset { get; set; }
    /// <summary>Transverse: station of the reference line.</summary>
    public double Station { get; set; }
    /// <summary>Range: stations (longitudinal) or offsets (transverse).</summary>
    public double From { get; set; }
    public double To { get; set; }
    /// <summary>Longitudinal: +1 = U toward the right of the alignment, -1 = toward the left (mirrored profile).</summary>
    public int Side { get; set; } = 1;
    /// <summary>Closed section polygon (U, V), meters.</summary>
    public List<TrussUV> Profile { get; set; } = new();
    /// <summary>V measured from the finished grade at the reference point (true) or from the absolute elevation 0 (false).</summary>
    public bool FromGrade { get; set; } = true;
    public string Material { get; set; } = "";
    /// <summary>Nominal weight (lb/ft) when the solid is an envelope (grating, rope); 0 = solid x density.</summary>
    public double NominalWeightLbPerFt { get; set; }
    /// <summary>Density (kg/m3) of a non-steel item (aluminum 2700); 0 = steel.</summary>
    public double Density { get; set; }
    /// <summary>Steel assembly mark (created when missing) and its description.</summary>
    public string Assembly { get; set; } = "DECK-ITEMS";
    public string AssemblyDescription { get; set; } = "";
    /// <summary>OPTIONAL repeated pieces (comb teeth of a finger dam, blocks, ...): when &gt; 0 the item is built as separate pieces <see cref="RepeatWidth"/>
    /// long along the range every <see cref="RepeatPitch"/>, the first starting <see cref="RepeatStart"/> after <see cref="From"/>, none beyond <see cref="To"/>
    /// (one part, one solid per piece). 0 = one continuous piece (omitted from the .sbm).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RepeatPitch { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RepeatWidth { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RepeatStart { get; set; }
    /// <summary>OPTIONAL copies of the profile shifted by these V offsets (m; e.g. ladder rungs, rails at several levels), each a separate solid of the
    /// same part (combined with <see cref="RepeatPitch"/>). Null / empty = the profile once (omitted from the .sbm).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? CopiesV { get; set; }
    /// <summary>OPTIONAL (null = none): rivets of a steel item (e.g. clip angles of a framed diaphragm), given in the item frame; grip = the contiguous
    /// steel along the shank (probed through every part built before, incl. stringers).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussLineItemRivet>? Rivets { get; set; }
    /// <summary>OPTIONAL (null = none): clip angles at both ends of a TRANSVERSE steel item framed between two webs (diaphragm channels between stringers),
    /// see <see cref="TrussClipAngles"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrussClipAngles? ClipAngles { get; set; }
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>
/// Clip angles of a transverse item framed between two webs (generic: a diaphragm channel riveted to the stringer webs by one angle at each end). At each
/// end W0 = From / To (the abutting web faces) one angle: first leg on the item's web (plane U = <see cref="HeelU"/>, from W0 into the item), second leg on
/// the abutting web (plane W = W0, along U toward <see cref="Toward"/>), <see cref="Length"/> high down from V = <see cref="Top"/> (item frame, as the
/// profile). The heel corner lies at (HeelU, W0); the item leg stands on the -U side of HeelU when Toward &lt; 0 (back of a web at HeelU), else on the +U side.
/// Rivets: <see cref="ItemLegRivets"/> through the item leg + item web (gage = distance from W0 along the item), <see cref="WebLegRivets"/> through the web
/// leg + abutting web (gage = distance from the heel along U); grips probed. One part per item (category TrussConnectionAngle).
/// </summary>
public sealed class TrussClipAngles
{
    public string Angle { get; set; } = "";
    public double HeelU { get; set; }
    public int Toward { get; set; } = -1;
    public double Top { get; set; }
    public double Length { get; set; }
    public TrussRivetColumn? ItemLegRivets { get; set; }
    public TrussRivetColumn? WebLegRivets { get; set; }
    public double RivetDiameter { get; set; }
    public string Material { get; set; } = "";
    public string? RivetUnresolved { get; set; }
    public string Unresolved { get; set; } = "";
}

/// <summary>Rivet of a line item: shank through (U, V, W) along <see cref="Axis"/> ("U" along the alignment, "W" across = offset direction, "V" up),
/// head at the + end of the steel stack found there. Transverse items: U from the station, W = offset; longitudinal: U = station, W = offset from
/// the reference line toward the item side. V above the finished grade (or absolute, as the item).</summary>
public sealed class TrussLineItemRivet
{
    public double U { get; set; }
    public double V { get; set; }
    public double W { get; set; }
    public string Axis { get; set; } = "W";
    public double Diameter { get; set; }
    public string? Material { get; set; }
    public string? Unresolved { get; set; }
}

public sealed class TrussUV
{
    public double U { get; set; }
    public double V { get; set; }
    public TrussUV() { }
    public TrussUV(double u, double v) { U = u; V = v; }
}

/// <summary>Rivets connecting one member to the gussets of a joint: <see cref="Rows"/> lines parallel to the member axis at <see cref="Gage"/>
/// apart, <see cref="Count"/> rivets per gusset side at <see cref="Pitch"/>, first at <see cref="FirstFromEnd"/> from the member end.</summary>
public sealed class TrussRivetGroup
{
    /// <summary>Member name (the plan member name, e.g. "U1-L2").</summary>
    public string Member { get; set; } = "";
    public int Count { get; set; }
    public int Rows { get; set; } = 2;
    public double Pitch { get; set; }
    public double Gage { get; set; }
    public double FirstFromEnd { get; set; }
    public double Diameter { get; set; }
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL (omitted when false): the plans print the COUNT but no pitch / gage: <see cref="Count"/> rivets per gusset face are spread evenly
    /// over the lap area the gusset shares with the member (gusset outline clipped to the member band: web depth of a box / the in-plane extent of
    /// other sections, inset <see cref="EdgeDistance"/>), square grid along the member axis, nearest the member end first; <see cref="Pitch"/> /
    /// <see cref="Gage"/> / <see cref="Rows"/> are ignored. A grid tighter than 3 diameters is reported (CHECK). <see cref="Member"/> "*" = a whole-joint
    /// count: split over every member at the joint in proportion to its lap area.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Distributed { get; set; }
    /// <summary>OPTIONAL edge distance of distributed rivets from the member band and gusset edges (m; 0 = 1.5 diameters).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EdgeDistance { get; set; }
    /// <summary>OPTIONAL rivet material text (e.g. "1 1/8 in H.S. rivet steel"); null = a generic rivet steel text.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Material { get; set; }
    /// <summary>OPTIONAL open item of the group (UNRESOLVED text, page + reason): carried by every rivet as a dimension-only flag (IFC SBM_Status, database
    /// status) and raised once as a warning.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    /// <summary>OPTIONAL (omitted when false; <see cref="Distributed"/> groups): the rivets lie on the member JUST OUTSIDE the gusset (fill / splice plate
    /// zone the joint sheets draw beyond the gusset edge): spread over the member band from the outline edge outward over <see cref="BeyondLength"/>,
    /// heads on the member face (or on the joint's web outside splice / fill plates there), grip = those plates + the member plate stack + inside splice
    /// plates. They are not part of the gusset rivet cover check.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Beyond { get; set; }
    /// <summary>OPTIONAL length of the band beyond the gusset edge (m; 0 = 0.6 m), e.g. the extent of the drawn field.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double BeyondLength { get; set; }
}

/// <summary>
/// Line of rivets along a member (OPTIONAL <see cref="TrussMemberDefinition.RivetLines"/>; riveted plate girders: flange angles to the web, cover plates
/// to the angles). The shank line is parallel to section axis <see cref="Along"/> ("x" across, "y" up) through the section point (<see cref="X"/>,
/// <see cref="Y"/>); at each rivet station the grip is the CONNECTED stack of section pieces present there (partial-length cover plates included)
/// that contains that point, the head at the + end when <see cref="HeadPositive"/>. Rivets every <see cref="Pitch"/> from <see cref="FirstFromEnd"/>
/// after the member start to the same distance before its end, shifted by <see cref="Stagger"/> (staggered gage lines).
/// </summary>
public sealed class TrussRivetLine
{
    public string Label { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>"x" or "y": section axis the shank is parallel to.</summary>
    public string Along { get; set; } = "x";
    public bool HeadPositive { get; set; } = true;
    public double Pitch { get; set; }
    public double FirstFromEnd { get; set; }
    public double Stagger { get; set; }
    public double Diameter { get; set; }
    public string? Material { get; set; }
    /// <summary>UNRESOLVED text (e.g. "rule-based positions"): dimension-only flag on every rivet, warning once.</summary>
    public string? Unresolved { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>Floor system: stringer lines between consecutive floor beams (members with role FloorBeam) and the deck slab.</summary>
public sealed class TrussFloorDefinition
{
    public List<TrussStringerLine> Stringers { get; set; } = new();
    /// <summary>Top of stringer below the finished grade at the stringer line (slab + haunch); used when the line has no own value.</summary>
    public double StringerTopBelowGrade { get; set; }
    /// <summary>Stringers rest ON the floor beams (continuous / stacked framing): pieces run floor beam CL to CL, no end clearance.</summary>
    public bool StringersOnTop { get; set; }
    /// <summary>Clear distance between a stringer end and the floor beam web face (framed stringers).</summary>
    public double StringerEndClearance { get; set; }
    public TrussDeckDefinition? Deck { get; set; }
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class TrussStringerLine
{
    public string Name { get; set; } = "";
    /// <summary>Offset from the alignment (m, right +).</summary>
    public double Offset { get; set; }
    public string Section { get; set; } = "";
    /// <summary>OPTIONAL own top below grade (null = the floor's).</summary>
    public double? TopBelowGrade { get; set; }
    /// <summary>OPTIONAL station limits (null = every floor beam bay).</summary>
    public double? FromStation { get; set; }
    public double? ToStation { get; set; }
}

/// <summary>Deck slab between two alignment offsets and stations; top = finished grade.</summary>
public sealed class TrussDeckDefinition
{
    public double Thickness { get; set; }
    public double LeftOffset { get; set; }
    public double RightOffset { get; set; }
    public double BeginStation { get; set; }
    public double EndStation { get; set; }
    public double ConcreteStrengthKsi { get; set; } = 3;
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL (0 = none, omitted from the .sbm): slab thickening under every stringer line (haunch depth below the slab bottom),
    /// its width at the bottom and side slope (horizontal per vertical). Built as the deck's haunch body.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double HaunchDepth { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double HaunchBottomWidth { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double HaunchSideSlope { get; set; }
}

/// <summary>
/// Rope suspender (generic: wire rope / bridge strand hangers of a suspended floor or a suspension bridge): <see cref="Ropes"/> parallel ropes
/// between the pin centres at the <see cref="Upper"/> and <see cref="Lower"/> points ("plane:point" or point on <see cref="Plane"/>), spaced
/// across the truss plane; open sockets at both ends.
/// </summary>
public sealed class SuspenderDefinition
{
    public string Name { get; set; } = "";
    public string Plane { get; set; } = "";
    public string Upper { get; set; } = "";
    public string Lower { get; set; } = "";
    /// <summary>Pin centres below the upper / above the lower point (m; 0 = at the points).</summary>
    public double UpperPinBelowPoint { get; set; }
    public double LowerPinAbovePoint { get; set; }
    public double RopeDiameter { get; set; }
    public int Ropes { get; set; } = 1;
    /// <summary>Centre distance of the ropes (across the plane when <see cref="RopesAlongBridge"/> is false).</summary>
    public double RopeSpacing { get; set; }
    public bool RopesAlongBridge { get; set; }
    public double SocketLength { get; set; }
    public double SocketDiameter { get; set; }
    /// <summary>OPTIONAL: the socket reaches this far beyond the pin centre (m; the eye around the pin); 0 = the socket starts at the pin centre.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SocketBeyondPin { get; set; }
    /// <summary>OPTIONAL open socket jaw: gap between the two lugs (m, measured along the pin axis; 0 = solid envelope). The lugs = the socket circle
    /// outside the gap, from the socket end at the pin to <see cref="SocketJawLength"/> from the pin centre (toward the rope); the plate the pin
    /// passes through lies in the gap.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SocketJawWidth { get; set; }
    /// <summary>OPTIONAL: length of the jaw from the pin centre toward the rope (m; the basket starts there).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SocketJawLength { get; set; }
    public double PinDiameter { get; set; }
    /// <summary>Rope designation / grade text (IFC description), e.g. "4\" galvanized bridge rope".</summary>
    public string Rope { get; set; } = "";
    /// <summary>Rope weight (lb/ft per rope, catalog / plan; 0 = volume of the gross circle x steel density, which overstates a rope).</summary>
    public double RopeWeightLbPerFt { get; set; }
    /// <summary>Plan rope length (check only; 0 = none).</summary>
    public double PlanLength { get; set; }
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

public enum TrussShoeType { Fixed, Expansion, HoldDown }

/// <summary>Truss shoe (bearing) at a point ("plane:point" or point on <see cref="Plane"/>): pin at the point (+ <see cref="PinBelowPoint"/>), shoe body
/// down to the masonry plate, masonry plate on the pier (top = <see cref="SeatElevation"/> when given, else pin - height).</summary>
public sealed class TrussShoeDefinition
{
    public string Name { get; set; } = "";
    public string Plane { get; set; } = "";
    public string Point { get; set; } = "";
    /// <summary>Support (pier) name of the bridge the shoe stands on (bearing seat).</summary>
    public string Support { get; set; } = "";
    public TrussShoeType Type { get; set; }
    public double PinDiameter { get; set; }
    public double PinLength { get; set; }
    public double PinBelowPoint { get; set; }
    /// <summary>Pin centre to the bottom of the masonry plate.</summary>
    public double Height { get; set; }
    /// <summary>Shoe body length along the bridge at the bottom and width across.</summary>
    public double BodyLength { get; set; }
    public double BodyWidth { get; set; }
    /// <summary>Top of the shoe body (seat of the truss / top forging / sole plate) above the pin centre (0 = at the pin).</summary>
    public double BodyTopAbovePin { get; set; }
    /// <summary>Body length along the bridge at the pin (0 = 1.5 pin diameters).</summary>
    public double BodyTopLength { get; set; }
    public double MasonryLength { get; set; }
    public double MasonryWidth { get; set; }
    public double MasonryThickness { get; set; }
    /// <summary>OPTIONAL plan elevation of the masonry plate bottom (bearing seat); NaN = pin - Height.</summary>
    public double SeatElevation { get; set; } = double.NaN;
    /// <summary>OPTIONAL material of the shoe body (plan letter or name; null = cast steel, omitted from the .sbm), e.g. "C" for shoes built up of plates.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BodyMaterial { get; set; }
    public int AnchorBolts { get; set; }
    public double AnchorDiameter { get; set; }
    public double AnchorEmbedment { get; set; }
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL pieces of the shoe as drawn (plates, castings, pins, rockers, rollers; null / empty = the trapezoidal body envelope, omitted from the
    /// .sbm). When given they REPLACE the body envelope; the masonry plate and the pin of the fields above are still built (set their size 0 to give them as pieces).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussShoePiece>? Pieces { get; set; }
}

/// <summary>Role of a shoe piece: part category (Body = the shoe body category by shoe type, Plate = bearing plate, Pin / Roller = pin, Rocker = expansion
/// shoe body, Sole / Masonry = sole / masonry plate).</summary>
public enum TrussShoePieceKind { Body, Plate, Sole, Masonry, Pin, Roller, Rocker }

/// <summary>Extrusion direction of a shoe piece: Across = profile in (u along the bridge, z up) swept across the truss (n) from <see cref="TrussShoePiece.From"/>
/// to <see cref="TrussShoePiece.To"/>; Along = profile in (n across, z up) swept along the bridge (u); Vertical = profile in plan (u, n) swept up (z).</summary>
public enum TrussShoePieceAxis { Across, Along, Vertical }

/// <summary>One piece of a truss shoe in the shoe frame: origin = the shoe reference point (pin centre = point - PinBelowPoint), u along the bridge
/// (up-station +), n = the plane normal made horizontal (right +), z up. Profile = closed polygon (or a circle, optionally clipped to |profile x - centre x|
/// &lt;= <see cref="ClipHalfWidth"/> for segmental rollers), swept along the <see cref="Axis"/> from <see cref="From"/> to <see cref="To"/>; copies at every
/// combination of <see cref="ShiftsProfileX"/> (in the profile plane, its first coordinate) and <see cref="ShiftsSweep"/> (along the sweep), optionally
/// mirrored about n = 0 (<see cref="MirrorN"/>) or u = 0 (<see cref="MirrorU"/>). All meters.</summary>
public sealed class TrussShoePiece
{
    public string Name { get; set; } = "";
    public TrussShoePieceKind Kind { get; set; }
    public TrussShoePieceAxis Axis { get; set; }
    public List<TrussUV> Profile { get; set; } = new();
    /// <summary>Circle instead of a polygon: diameter (0 = polygon) and centre in the profile plane.</summary>
    public double CircleDiameter { get; set; }
    public double CircleX { get; set; }
    public double CircleY { get; set; }
    /// <summary>Segmental roller: the circle is cut by two lines |x - CircleX| = ClipHalfWidth (0 = full circle).</summary>
    public double ClipHalfWidth { get; set; }
    public double From { get; set; }
    public double To { get; set; }
    public List<double> ShiftsProfileX { get; set; } = new();
    public List<double> ShiftsSweep { get; set; } = new();
    public bool MirrorN { get; set; }
    public bool MirrorU { get; set; }
    /// <summary>Plan letter / material name (truss <c>Materials</c> map); empty = the truss default.</summary>
    public string Material { get; set; } = "";
    /// <summary>Size text for the part description, e.g. "PL 22 x 10 3/16 x 3'-2".</summary>
    public string Description { get; set; } = "";
    /// <summary>Plan item not dimensioned for this piece (dimension-only flag; empty = the shoe's flag, if any).</summary>
    public string Unresolved { get; set; } = "";
}
