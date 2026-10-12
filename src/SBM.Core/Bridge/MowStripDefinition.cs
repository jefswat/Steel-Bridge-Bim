using System.Text.Json.Serialization;

namespace SBM.Core.Bridge;

/// <summary>
/// Concrete mow strip (riprap) around each column of a pier at the ground (OPTIONAL <see cref="SubstructureDefinition.MowStrip"/>): a ring
/// <see cref="Width"/> wide beyond every column face, <see cref="Thickness"/> thick, top at <see cref="TopElevation"/> or
/// <see cref="TopAboveFoundation"/> above the foundation top (e.g. the plans' minimum ground cover over the drilled shaft). Rectangular columns only.
/// Solid kind <c>SubElementKind.MowStrip</c> (IFC IfcSlab 'MOW_STRIP'). Lengths in meters.
/// </summary>
public sealed class MowStripDefinition
{
    public double Width { get; set; }
    public double Thickness { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopElevation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopAboveFoundation { get; set; }
    public string Source { get; set; } = "";
    /// <summary>What the plans do not fix (with page): the solid is built and flagged UNRESOLVED with <see cref="Basis"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
    public MowStripDefinition Clone() => (MowStripDefinition)MemberwiseClone();
}
