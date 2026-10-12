using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

public enum ExpansionJointType { None, Modular, StripSeal, Sealed }

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

    /// <summary>
    /// OPTIONAL thickened deck end ("end dam diaphragm" on the plans) under the blockout; null = the deck keeps its plain thickness
    /// and the blockout is limited to the slab (a warning is given when the plans' blockout is deeper).
    /// </summary>
    public DeckEndBlockDefinition? EndBlock { get; set; }

    // ---- OPTIONAL (omitted from the .sbm when unused): plan designation, centred device, checks, unresolved flag
    /// <summary>Plan designation of the joint (device name, IFC description), e.g. "SEJ-B(MOD) 4 in sealed expansion joint + BS-EJCP cover plate".</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Designation { get; set; }
    /// <summary>The device body is centred on the deck-end line (half of <see cref="GapAt50F"/> each side) instead of filling the opening beyond it: for
    /// plans that dimension the slab to the CL joint and give no opening (the body then overlaps the deck ends; it is a placeholder).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool BodyCentered { get; set; }
    /// <summary>Plan length of the joint (bid LF), check value only.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? PlanLength { get; set; }
    /// <summary>What the plans do not give (with the page): raised as a warning and carried by the device (IFC SBM_Status, database status).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }

    public bool Active => Type != ExpansionJointType.None;

    public string TypeName => Type switch
    {
        ExpansionJointType.Modular => "Modular",
        ExpansionJointType.StripSeal => "Strip seal",
        ExpansionJointType.Sealed => "Sealed",
        _ => "None",
    };

    public ExpansionJointDefinition Clone()
    {
        var c = (ExpansionJointDefinition)MemberwiseClone();
        c.EndBlock = EndBlock?.Clone();
        return c;
    }
}

/// <summary>One soffit vertex of an end block: distance from the deck-end face (perpendicular to the joint) and drop below the finished top of slab.</summary>
public sealed class EndBlockPoint
{
    public double X { get; set; }
    public double Drop { get; set; }
    public EndBlockPoint() { }
    public EndBlockPoint(double x, double drop) { X = x; Drop = drop; }
}

/// <summary>
/// Thickened deck end at an expansion joint (plans: "END DAM DIAPH."). Spans the full deck width between the deck edges. Its soffit is
/// <see cref="Depth"/> below the top of slab for <see cref="FlatLength"/> from the deck-end face, then rises at 45 degrees to the slab soffit
/// (<see cref="ChamferRun"/> null = Depth minus the slab thickness), unless <see cref="Soffit"/> gives the profile explicitly
/// (deck-end face to the slab soffit). The girder ends, upper webs and top flanges beneath it are embedded in it (see docs/plan-data/deck-end-blocks.md).
/// Lengths in meters.
/// </summary>
public sealed class DeckEndBlockDefinition
{
    /// <summary>Top of slab to soffit at the deck-end face (plans: 2'-2 at the I-80 and Ramp G piers and I-80 abutments).</summary>
    public double Depth { get; set; } = Ft(2) + In(2);

    /// <summary>Deck-end face to the start of the soffit chamfer (plans: 3'-4 1/2 I-80 piers, 3'-3 Ramp G piers, 2'-10 I-80 abutments).</summary>
    public double FlatLength { get; set; } = Ft(3) + In(4.5);

    /// <summary>Horizontal run of the chamfer; null = 45 degrees (run = Depth minus slab thickness).</summary>
    public double? ChamferRun { get; set; }

    /// <summary>Explicit soffit polyline from the deck-end face to the slab soffit (overrides Depth/FlatLength/ChamferRun).</summary>
    public List<EndBlockPoint> Soffit { get; set; } = new();

    public DeckEndBlockDefinition Clone()
    {
        var c = (DeckEndBlockDefinition)MemberwiseClone();
        c.Soffit = Soffit.Select(p => new EndBlockPoint(p.X, p.Drop)).ToList();
        return c;
    }

    /// <summary>Soffit polyline for a slab of the given thickness (first point at X = 0).</summary>
    public List<EndBlockPoint> SoffitFor(double slab)
    {
        if (Soffit.Count >= 2) return Soffit.OrderBy(p => p.X).ToList();
        double d = Depth;
        if (d <= slab) return new();
        double run = ChamferRun ?? (d - slab);
        return new() { new(0, d), new(FlatLength, d), new(FlatLength + run, slab) };
    }
}
