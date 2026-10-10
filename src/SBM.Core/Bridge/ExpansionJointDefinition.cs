using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

public enum ExpansionJointType { None, Modular, StripSeal }

/// <summary>
/// Deck expansion joint at an END support (first or last support of the bridge). OPTIONAL: <c>SupportDefinition.ExpansionJoint</c>
/// null or <see cref="Type"/> None = a plain deck end. The plans dimension the blockout and the opening only; the joint device
/// (beams, support bars, boxes, anchorage, gland) is designed and supplied by the manufacturer, so the model builds
/// (1) the blockout, cut out of the deck and cast as its own solid, and (2) a GENERIC envelope of the device. Not a design.
/// Lengths in meters; "C"/"D" are the plans' dimensions (measured perpendicular to the joint).
/// </summary>
public sealed class ExpansionJointDefinition
{
    public ExpansionJointType Type { get; set; } = ExpansionJointType.Modular;

    /// <summary>Plan dimension "C": opening at 50 F between the deck-end face of this unit and the face across the joint (the other unit's deck end, or the abutment backwall).</summary>
    public double GapAt50F { get; set; } = In(9);

    /// <summary>Plan dimension "D": width of the blockout measured from the deck-end face, perpendicular to the joint.</summary>
    public double BlockoutWidth { get; set; } = In(25);

    /// <summary>Blockout depth below the top of slab (plans: 1'-2).</summary>
    public double BlockoutDepth { get; set; } = Ft(1) + In(2);

    /// <summary>Top of the device below the top of slab (plans: 1/8").</summary>
    public double SetDown { get; set; } = In(0.125);

    /// <summary>Plan CL bearing to deck end (normal to the support line). Optional CHECK value: a different <c>SupportDefinition.DeckEndOffset</c> is reported, never changed.</summary>
    public double? DeckEndOffset { get; set; }

    /// <summary>Number of seals (gaps between beams); null = not given by the plans (manufacturer).</summary>
    public int? SealCount { get; set; }

    /// <summary>Build the generic device body here. Two units meeting at a pier share one joint: set it on one side only (the body spans the whole GapAt50F).</summary>
    public bool Body { get; set; } = true;

    /// <summary>Barrier-side clearance kept free at each end of the body, measured from the deck edge (the plans' barrier base is the default).</summary>
    public double? EdgeInset { get; set; }

    public bool Active => Type != ExpansionJointType.None;

    public string TypeName => Type switch
    {
        ExpansionJointType.Modular => "Modular",
        ExpansionJointType.StripSeal => "Strip seal",
        _ => "None",
    };

    public ExpansionJointDefinition Clone() => (ExpansionJointDefinition)MemberwiseClone();
}
