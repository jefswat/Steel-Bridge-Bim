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

    /// <summary>OPTIONAL steel armor of a sealed / strip-seal joint (armor plates with seal retainer bars and stud anchors on both faces of the opening,
    /// e.g. TxDOT SEJ-B); built only with <see cref="Body"/> (the device owner). Null = none, omitted from the .sbm.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public JointArmorDefinition? Armor { get; set; }
    /// <summary>OPTIONAL walkway cover plate over the joint (e.g. TxDOT BS-EJCP); built only with <see cref="Body"/>. Null = none, omitted from the .sbm.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public JointCoverPlateDefinition? CoverPlate { get; set; }
    /// <summary>OPTIONAL barrier cover (slider) plates over the joint (e.g. Iowa modular joint details: 3/8 in bent plate wrapping the barrier, set in a recess);
    /// built only with <see cref="Body"/>. Null = none, omitted from the .sbm.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public JointBarrierPlateDefinition? BarrierPlate { get; set; }

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
        c.Armor = Armor?.Clone();
        c.CoverPlate = CoverPlate?.Clone();
        c.BarrierPlate = BarrierPlate?.Clone();
        return c;
    }
}

/// <summary>
/// Steel armor of a sealed expansion joint (OPTIONAL <see cref="ExpansionJointDefinition.Armor"/>; generic, e.g. TxDOT SEJ-B "PL 1/2 x 4 with Bar 1/2 x 1/4 and
/// 5/8 in stud anchors at 6 in max"). Each face of the opening gets a vertical plate (<see cref="PlateThickness"/> x <see cref="PlateDepth"/>, top flush with the top
/// of slab) cast into the slab edge, a seal retainer bar on its joint face at the bottom, and headed stud anchors welded to its back. The NEAR plate's joint
/// face is the deck-end face of the bridge that carries the joint; the FAR plate's joint face is <see cref="ExpansionJointDefinition.GapAt50F"/> (the
/// opening) beyond it. Plates run along the joint between the alignment offsets of the deck edges moved inward by <see cref="EdgeInsetLeft"/> /
/// <see cref="EdgeInsetRight"/> (measured normal to the alignment, e.g. the toes of the curbs). Steel parts go into the bridge's steel model (assembly kind
/// ExpansionJoint, not structural steel). Lengths in meters.
/// </summary>
public sealed class JointArmorDefinition
{
    /// <summary>Standard / source text (part descriptions), e.g. "TxDOT SEJ-B (10-2024)".</summary>
    public string Standard { get; set; } = "";
    public double PlateThickness { get; set; } = In(0.5);
    public double PlateDepth { get; set; } = In(4);
    /// <summary>Seal retainer bar on the joint face at the plate bottom: projection into the opening x height (0 = none) and its rise above the plate bottom.</summary>
    public double BarProjection { get; set; } = In(0.25);
    public double BarHeight { get; set; } = In(0.5);
    public double BarAboveBottom { get; set; } = In(0.25);
    public double EdgeInsetLeft { get; set; }
    public double EdgeInsetRight { get; set; }
    /// <summary>Build the far plate too (false: the far face belongs to another structure that is not modeled).</summary>
    public bool FarPlate { get; set; } = true;
    /// <summary>Headed stud anchors on the back of each plate: diameter, centre below the top of slab, horizontal leg from the plate back, then an inclined
    /// leg dropping at <see cref="StudBendDegrees"/> (0 = straight stud), head diameter / height; spacing max along the plate and first stud from each end.</summary>
    public double StudDiameter { get; set; } = In(0.625);
    public double StudDrop { get; set; } = In(1.5);
    public double StudLength { get; set; } = In(4);
    public double StudBentLength { get; set; }
    public double StudBendDegrees { get; set; }
    public double StudHeadDiameter { get; set; } = In(1.25);
    public double StudHeadHeight { get; set; } = In(0.3125);
    public double StudSpacing { get; set; } = In(6);
    public double StudEndDistance { get; set; } = In(3);
    public string Material { get; set; } = "ASTM A36";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
    public JointArmorDefinition Clone() => (JointArmorDefinition)MemberwiseClone();
}

/// <summary>
/// Walkway expansion joint cover plate (OPTIONAL <see cref="ExpansionJointDefinition.CoverPlate"/>; generic, e.g. TxDOT BS-EJCP "1/4 in slip resistant plate
/// 1'-4 wide, bent 1/4 in at the CL, countersunk 3/8 in flat head sleeve anchors at 6 in max in two staggered rows on one side"). The plate lies on the top of
/// slab centred on the CL of the opening, from the deck edge offsets moved inward by <see cref="EdgeInsetLeft"/> / <see cref="EdgeInsetRight"/> plus
/// <see cref="EndClearance"/> (along the joint). Corner clips and edge bevels are not modeled. Lengths in meters.
/// </summary>
public sealed class JointCoverPlateDefinition
{
    public string Standard { get; set; } = "";
    public double Thickness { get; set; } = In(0.25);
    public double Width { get; set; } = Ft(1) + In(4);
    /// <summary>Rise of the bent CL above the plate edges.</summary>
    public double Crown { get; set; } = In(0.25);
    public double EdgeInsetLeft { get; set; }
    public double EdgeInsetRight { get; set; }
    public double EndClearance { get; set; } = In(1);
    /// <summary>Anchors (on the near = deck side of the joint): diameter, length below the plate, rows at these distances from the plate edge (staggered),
    /// spacing max between successive anchors along the plate (alternating rows), first anchor from each end.</summary>
    public double AnchorDiameter { get; set; } = In(0.375);
    public double AnchorLength { get; set; } = In(2.5);
    public List<double> AnchorRows { get; set; } = new() { In(1.5), In(4.5) };
    public double AnchorSpacing { get; set; } = In(6);
    public double AnchorEndDistance { get; set; } = In(3);
    public string Material { get; set; } = "Slip resistant steel plate, galvanized";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
    public JointCoverPlateDefinition Clone() { var c = (JointCoverPlateDefinition)MemberwiseClone(); c.AnchorRows = AnchorRows.ToList(); return c; }
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

    /// <summary>OPTIONAL lateral limits (alignment offsets, right +) of the block, e.g. between the top flanges of the outer girders (TxDOT IGTS thickened
    /// slab end "between girders"); null = the deck edge. Omitted from the .sbm when null.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? LeftOffset { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? RightOffset { get; set; }
    /// <summary>OPTIONAL source / standard text and UNRESOLVED flag (raised as a warning, carried by the block: IFC SBM_Status).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }

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

/// <summary>
/// Barrier cover (slider) plate at an expansion joint (OPTIONAL <see cref="ExpansionJointDefinition.BarrierPlate"/>): a plate bent to the barrier section (every
/// face of the bridge's barrier profile except its base), its outer face <see cref="Depress"/> inside the concrete faces (recessed), stopping
/// <see cref="BottomClear"/> above the base on both sides; along the barrier from <see cref="LengthOut"/> beyond the deck-end face (over the opening, onto the
/// next unit) to <see cref="LengthIn"/> into this deck. Fasteners (cap screws, bent bolts) and the recess in the concrete are not modeled. Lengths in meters.
/// </summary>
public sealed class JointBarrierPlateDefinition
{
    public double Thickness { get; set; } = In(0.375);
    public double Depress { get; set; } = In(0.25);
    public double BottomClear { get; set; } = In(0.25);
    public double LengthIn { get; set; }
    public double LengthOut { get; set; }
    /// <summary>OPTIONAL lengths of the right-hand plate when they differ from the left (skewed joints); null = <see cref="LengthIn"/> / <see cref="LengthOut"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? RightLengthIn { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? RightLengthOut { get; set; }
    public bool Left { get; set; } = true;
    public bool Right { get; set; } = true;
    public string Material { get; set; } = "ASTM A36, galvanized";
    public string Source { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
    public JointBarrierPlateDefinition Clone() => (JointBarrierPlateDefinition)MemberwiseClone();
}
