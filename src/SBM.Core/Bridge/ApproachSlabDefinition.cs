using System.Text.Json.Serialization;

namespace SBM.Core.Bridge;

/// <summary>Who an approach slab belongs to in the outputs (user answer 2026-10-28 7a.3: a per-project setting on each bridge end, chosen per plan set).</summary>
public enum ApproachSlabOwner { Bridge, Road }

/// <summary>How <see cref="ApproachSlabDefinition.Length"/> is measured.</summary>
public enum ApproachSlabLengthMeasure
{
    /// <summary>Along the alignment (stations) at every offset: the far end is the deck end moved by the length along the road.</summary>
    AlongAlignment,
    /// <summary>Normal to the deck end line: the far end is parallel to the deck end.</summary>
    NormalToDeckEnd,
}

/// <summary>
/// OPTIONAL approach slab at an END support of a bridge (<see cref="SupportDefinition.ApproachSlab"/>, first or last support; omitted from the .sbm
/// when null): a slab beyond the deck end, top = the deck's finished grade continued (profile + cross slope + edge strip), between the deck edges moved
/// in by the insets, with an optional sleeper slab under its far end. Every value comes from the plans: a missing length / thickness = not built
/// (warning); a missing <see cref="Owner"/> or <see cref="LengthMeasure"/> = built and flagged UNRESOLVED (kept with the bridge / measured along the
/// alignment). Built by <c>ConcreteGenerator</c> (<c>ConcreteModel.ApproachSlabs</c>).
/// </summary>
public sealed class ApproachSlabDefinition
{
    /// <summary>Length away from the bridge (meters), measured as <see cref="LengthMeasure"/>.</summary>
    public double Length { get; set; }
    public double Thickness { get; set; }
    /// <summary>Gap between the deck end face and the approach slab (joint opening; 0 = abutting the deck end).</summary>
    public double Gap { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApproachSlabLengthMeasure? LengthMeasure { get; set; }
    /// <summary>Lateral limits: distance inside the deck edge lines (continued beyond the deck end), e.g. to the curb faces; 0 = the deck edge.</summary>
    public double EdgeInsetLeft { get; set; }
    public double EdgeInsetRight { get; set; }
    /// <summary>Ownership in IFC / the database: the bridge (IfcSlab in the deck part) or the road (IfcSlab in the IfcRoad covering it). Null = UNRESOLVED.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApproachSlabOwner? Owner { get; set; }
    /// <summary>OPTIONAL sleeper slab under the far end (null = none).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SleeperSlabDefinition? Sleeper { get; set; }
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Plan items not dimensioned (UNRESOLVED flag text); raised as a warning, the slab is still built.</summary>
    public string Unresolved { get; set; } = "";

    public ApproachSlabDefinition Clone()
    {
        var c = (ApproachSlabDefinition)MemberwiseClone();
        c.Sleeper = Sleeper?.Clone();
        return c;
    }
}

/// <summary>Sleeper slab under the far end of an approach slab: a block below the approach slab bottom.</summary>
public sealed class SleeperSlabDefinition
{
    /// <summary>Width along the road (meters), measured like the approach slab length.</summary>
    public double Width { get; set; }
    /// <summary>Depth below the approach slab bottom.</summary>
    public double Depth { get; set; }
    /// <summary>Distance of the sleeper's far face beyond the approach slab's far end (+ = it projects past the end; 0 = flush).</summary>
    public double BeyondEnd { get; set; }
    public string Source { get; set; } = "";
    public SleeperSlabDefinition Clone() => (SleeperSlabDefinition)MemberwiseClone();
}
