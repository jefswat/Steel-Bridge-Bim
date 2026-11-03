using System.Text.Json.Serialization;

namespace SBM.Core.Bridge;

/// <summary>
/// Connection plate lying FLAT ON THE FACES of members at a truss point (generic, OPTIONAL; <see cref="TrussDefinition.FacePlates"/>), e.g. a lateral / portal plate
/// riveted on a chord's top cover plate (PTC p.79 '7/8 Spl. & Lat. Pl.' at U11), or a plate under the bottom faces of a portal and a lateral on an edge angle.
/// <para>Planes: <see cref="NormalFrom"/> null = one flat PIECE per chord of the point's plane at the point (role <see cref="Chords"/>), in that chord's face plane
/// (chord axis x truss-plane normal) - a plate bent over a chord kink is built as two flat pieces meeting on the line where the two face planes meet (flag it);
/// a member name = ONE flat piece in the plane of that member's section x axis (its flange plane). The plate's bearing face lies on the extreme face of
/// <see cref="BearOn"/> (null = the chords) along the piece normal: <see cref="Side"/> Above = on their top faces, Below = under their bottom faces (probed on
/// the built parts over the piece outline).</para>
/// <para>Coordinates (<see cref="Outline"/>, fills, rivets, edge angle): origin = the foot of the perpendicular from the point onto the plate's bearing face (the
/// up-station piece when bent: a view normal to that face shows the point there); U = distance along the plate (+ up-station; the other piece of a bent plate
/// continues from the bend line, developed), V = horizontal across the truss plane, + = to the LEFT of the alignment tangent. Meters.</para>
/// Fills (<see cref="TrussFacePlateFill"/>) fill the gap between the plate's bearing face and a member face (probed at the fill outline's vertices: beveled when it
/// varies); rivets at the given points normal to the piece, grip = the contiguous steel along the shank (a rivet whose steel is the plate alone is not placed,
/// counted); an optional edge angle (<see cref="EdgeAngle"/>) on the plate's member-side face with its other leg on the truss-plane stack (probed).
/// Parts: <see cref="Steel.PartCategory.TrussConnectionPlate"/> (marks "{plane}-FP{point}{Suffix}" + "-1" / "-2" per piece), fills FillerPlate, angle TrussConnectionAngle.
/// </summary>
public sealed class TrussFacePlateDefinition
{
    /// <summary>"S:U11" (one plane) or "U11" (every plane).</summary>
    public string Point { get; set; } = "";
    public string Suffix { get; set; } = "";
    public string Label { get; set; } = "";
    public double Thickness { get; set; }
    public TrussBracingPlateSide Side { get; set; } = TrussBracingPlateSide.Above;
    /// <summary>OPTIONAL (null = the chords at the point): members (names or marks) whose faces the plate lies on.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? BearOn { get; set; }
    /// <summary>OPTIONAL (null = bent: one piece per chord at the point): member whose section x axis and axis give the plane of a single flat plate.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NormalFrom { get; set; }
    public List<TrussUV> Outline { get; set; } = new();
    /// <summary>OPTIONAL (false = the up-station piece): a bent plate's coordinate origin is the foot of the perpendicular on the DOWN-station piece (a sheet
    /// drawn normal to that chord's face, e.g. the mirrored half of a symmetric span).</summary>
    public bool OriginOnBack { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussFacePlateFill>? Fills { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussFacePlateRivet>? Rivets { get; set; }
    /// <summary>OPTIONAL: rivet patterns laid on a connected member's end (<see cref="TrussFacePlateRivetGroup"/>), for rivets the sheet counts / draws relative
    /// to the member rather than at fixed plate positions.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussFacePlateRivetGroup>? MemberRivets { get; set; }
    /// <summary>Lines of rivets in the edge angle's plate leg (1 = one line on the leg's gage, 2 = two staggered lines at 1/3 and 2/3 of the leg).</summary>
    public int EdgeAngleRowsPlate { get; set; } = 1;
    /// <summary>OPTIONAL (null = the plate's): diameter / material of the rivets in the edge angle's stack leg.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? EdgeAngleStackRivetDiameter { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EdgeAngleStackRivetMaterial { get; set; }
    /// <summary>OPTIONAL ("" = none): AISC angle on the plate's member-side face along the piece from <see cref="EdgeAngleU0"/> to <see cref="EdgeAngleU1"/>, its
    /// other leg on the truss-plane joint stack face (probed toward the truss plane from the plate outline's edge nearest to it).</summary>
    public string EdgeAngle { get; set; } = "";
    public double EdgeAngleU0 { get; set; }
    public double EdgeAngleU1 { get; set; }
    /// <summary>Rivets in the angle leg on the plate / on the stack (evenly spaced along the angle on the leg's gage line).</summary>
    public int EdgeAngleRivetsPlate { get; set; }
    public int EdgeAngleRivetsStack { get; set; }
    public string EdgeAngleLabel { get; set; } = "";
    public double RivetDiameter { get; set; }
    public string? RivetMaterial { get; set; }
    public string Material { get; set; } = "";
    public string Unresolved { get; set; } = "";
    /// <summary>Dimension-only flag text of the rivets (positions as drawn / derived); "" = <see cref="Unresolved"/>.</summary>
    public string RivetUnresolved { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>Fill under a <see cref="TrussFacePlateDefinition"/>: <see cref="Outline"/> (plate U, V) between the plate's bearing face and the face of
/// <see cref="Member"/> (probed normal to the piece at every vertex); <see cref="PlanThickness"/> (0 = none) is the drawn thickness, compared in an Info line.</summary>
public sealed class TrussFacePlateFill
{
    public string Member { get; set; } = "";
    public List<TrussUV> Outline { get; set; } = new();
    public string Label { get; set; } = "";
    public double PlanThickness { get; set; }
    public string Material { get; set; } = "";
}

/// <summary>Rivet of a <see cref="TrussFacePlateDefinition"/> at plate (U, V); diameter / material null = the plate's.</summary>
public sealed class TrussFacePlateRivet
{
    public double U { get; set; }
    public double V { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Diameter { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Material { get; set; }
    public TrussFacePlateRivet() { }
    public TrussFacePlateRivet(double u, double v) { U = u; V = v; }
}

/// <summary>Rivet pattern of a <see cref="TrussFacePlateDefinition"/> on a member's end: <see cref="Rows"/> lines parallel to the member (projected on the plate)
/// <see cref="Gage"/> apart, centred <see cref="Offset"/> to the left of its axis (looking from the point along the member), rivets every <see cref="Pitch"/>
/// from <see cref="FromEnd"/> past the member's cut end (odd lines shifted half a pitch when <see cref="Stagger"/>), the first <see cref="Count"/> positions
/// nearest the end.</summary>
public sealed class TrussFacePlateRivetGroup
{
    public string Member { get; set; } = "";
    public int Count { get; set; }
    public int Rows { get; set; } = 2;
    public double Gage { get; set; }
    public double Pitch { get; set; }
    public double FromEnd { get; set; }
    public bool Stagger { get; set; }
    public double Offset { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Diameter { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Material { get; set; }
}
