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
    public TrussPlate() { }
    public TrussPlate(double width, double thickness, bool perforated = false, string label = "") { Width = width; Thickness = thickness; Perforated = perforated; Label = label; }
}

/// <summary>One web ply of a built-up member (depth 0 = the section's <see cref="TrussSectionDefinition.WebDepth"/>).</summary>
public sealed class TrussPly
{
    public double Depth { get; set; }
    public double Thickness { get; set; }
    public string Label { get; set; } = "";
    public TrussPly() { }
    public TrussPly(double depth, double thickness, string label = "") { Depth = depth; Thickness = thickness; Label = label; }
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
    /// <summary>Box: plates on top (+y) / bottom (-y) stacked outward; H: flange plates on both web edges.</summary>
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
    public string Unresolved { get; set; } = "";
    public string Source { get; set; } = "";
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
