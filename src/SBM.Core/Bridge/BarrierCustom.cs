using System.Text.Json.Serialization;

namespace SBM.Core.Bridge;

/// <summary>
/// Vertex of a custom barrier section (<see cref="BarrierType.Custom"/>): R = distance INWARD from the deck edge (the plan's back reference line; negative =
/// outboard of the edge, e.g. a bump-out), H = height above the top of deck. The polygon is counter-clockwise in (R, H) with the base on H = 0 (same
/// convention as <c>Concrete.BarrierProfiles</c>).
/// </summary>
public sealed class BarrierProfilePoint
{
    public double R { get; set; }
    public double H { get; set; }
    /// <summary>Fillet radius at this vertex (tangent arc, e.g. the plan's "R = 10 in" at a point of intersection); 0 = sharp.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Radius { get; set; }
    /// <summary>Chamfer at this vertex, cut back along both edges; 0 = sharp.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Chamfer { get; set; }
    /// <summary>Surface tag of the edge from this vertex to the next (e.g. "liner", "brick"): the swept face is reported to the surface treatments
    /// (<see cref="SurfaceTreatmentDefinition"/>, region host Barrier) under this tag. Empty = plain concrete.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tag { get; set; }
    public BarrierProfilePoint() { }
    public BarrierProfilePoint(double r, double h, double radius = 0, double chamfer = 0, string? tag = null) { R = r; H = h; Radius = radius; Chamfer = chamfer; Tag = tag; }
    public BarrierProfilePoint Clone() => (BarrierProfilePoint)MemberwiseClone();
}

/// <summary>
/// A length of a custom barrier with its own section (bump-out, pilaster, end block): centred on the point where a support's CL (or a station's radial
/// line) crosses the deck edge, <see cref="Length"/> measured along the deck edge. Where zones overlap, the SHORTER zone governs (nested zones, e.g. the
/// strip returns of a bump-out around its brick panel). Ends are square to the deck edge. Optional deck widening under it (plan "deck bump").
/// </summary>
public sealed class BarrierZone
{
    public string Name { get; set; } = "";
    /// <summary>Support whose CL crosses the deck edge at the zone centre ("" = use <see cref="Station"/>).</summary>
    public string Support { get; set; } = "";
    public double Station { get; set; } = double.NaN;
    public double Length { get; set; }
    public List<BarrierProfilePoint> Profile { get; set; } = new();
    /// <summary>Deck slab widening outboard of the deck edge under the zone (0 = none), its length along the edge (centred) and plan reference.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DeckWidening { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DeckWideningLength { get; set; }
    public string Source { get; set; } = "";
    public BarrierZone Clone()
    {
        var c = (BarrierZone)MemberwiseClone();
        c.Profile = Profile.Select(p => p.Clone()).ToList();
        return c;
    }
}
