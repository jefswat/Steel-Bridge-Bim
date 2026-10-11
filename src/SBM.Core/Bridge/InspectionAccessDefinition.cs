using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

// Inspection access: wire-rope inspection cables on WT brackets and walkways in the girder bays (I-80 design sheets
// 159-170, Ramp G sheets 130-134). OPTIONAL: BridgeDefinition.InspectionAccess == null means none.
// All lengths in meters.

public enum WalkwayType { None, A, B }

/// <summary>One inspection cable run on the outside (or inside) face of a girder web.</summary>
public sealed class InspectionCableDefinition
{
    /// <summary>Girder index (0 = G1).</summary>
    public int Girder { get; set; }
    /// <summary>True = the cable is on the left face of the web (outside face of the leftmost girder), false = right face.</summary>
    public bool LeftFace { get; set; } = true;
    /// <summary>Begin: support index and distance along the girder from that support's CL bearing (ahead +).</summary>
    public int BeginSupport { get; set; }
    public double BeginOffset { get; set; } = In(8);
    /// <summary>End: support index (-1 = the last support) and distance from its CL bearing (back = negative).</summary>
    public int EndSupport { get; set; } = -1;
    public double EndOffset { get; set; } = -In(8);
    public InspectionCableDefinition Clone() => (InspectionCableDefinition)MemberwiseClone();
}

/// <summary>Walkway selection for one bay (between a girder and the next continuing girder): type and first / last cross-frame line.</summary>
public sealed class InspectionWalkwayDefinition
{
    /// <summary>Left girder index of the bay (0 = G1).</summary>
    public int Bay { get; set; }
    /// <summary>Right girder of the bay; -1 = any bay with this left girder.</summary>
    public int RightGirder { get; set; } = -1;
    public WalkwayType Type { get; set; } = WalkwayType.A;
    /// <summary>Walkway runs from the cross-frame line with this ordinal in the bay (0 = first, usually the abutment frame)...</summary>
    public int FirstLine { get; set; }
    /// <summary>... to this ordinal (-1 = the last line of the bay).</summary>
    public int LastLine { get; set; } = -1;
    public InspectionWalkwayDefinition Clone() => (InspectionWalkwayDefinition)MemberwiseClone();
}

/// <summary>Typical walkway section (Type "A": C-channel stringers; Type "B": L8x4x1/2 stringers).</summary>
public sealed class WalkwaySectionDefinition
{
    /// <summary>Clear width between the inside faces of the rail posts.</summary>
    public double ClearWidth { get; set; } = In(25);
    public double GratingThickness { get; set; } = In(0.75);
    /// <summary>Weight of the welded grating (3/4" x 3/16" bearing bars at 1 3/16" + 1/4" rods at 4"), lb per sq ft (computed from the bars).</summary>
    public double GratingWeightLbPerSqFt { get; set; } = 5.3;
    /// <summary>Stringer shape for spans up to <see cref="StringerSpanLimit"/> and for longer spans (Type B: both the same).</summary>
    public string Stringer { get; set; } = "C7x12.25";
    public string LongStringer { get; set; } = "C7x14.75";
    /// <summary>Span (measured along the walkway between supports) above which <see cref="LongStringer"/> is used.</summary>
    public double StringerSpanLimit { get; set; } = Ft(25);
    /// <summary>Diaphragm: plate (width x thickness; width 0 = angle) or angle (name).</summary>
    public double DiaphragmPlateWidth { get; set; } = In(5);
    public double DiaphragmPlateThickness { get; set; } = In(0.375);
    public string DiaphragmAngle { get; set; } = "";
    /// <summary>Add a diaphragm mid-way between rail posts where the span exceeds <see cref="StringerSpanLimit"/> (Type A).</summary>
    public bool MidDiaphragmsOnLongSpans { get; set; } = true;
    public string PostAngle { get; set; } = "L3x3x5/16";
    public string TopRailAngle { get; set; } = "L3x3x5/16";
    /// <summary>Top of the top rail above the top of grating.</summary>
    public double TopRailHeight { get; set; } = Ft(3.5);
    /// <summary>Centerline of the intermediate rail above the top of grating.</summary>
    public double IntermediateRailHeight { get; set; } = Ft(2);
    public double RailPlateWidth { get; set; } = In(4);
    public double RailPlateThickness { get; set; } = In(0.375);
    /// <summary>Toe board PL 3/8 x 4 (Type A); false = none (Type B: the stringer leg serves).</summary>
    public bool ToeBoard { get; set; } = true;
    /// <summary>Bottom of the toe board above the top of the grating (assumed from the section drawing).</summary>
    public double ToeBoardClearance { get; set; } = In(0.5);
    /// <summary>Equal post spacing no greater than this.</summary>
    public double MaxPostSpacing { get; set; } = Ft(8.5);

    public static WalkwaySectionDefinition TypeA() => new();
    public static WalkwaySectionDefinition TypeB() => new()
    {
        ClearWidth = In(26), Stringer = "L8x4x1/2", LongStringer = "L8x4x1/2", DiaphragmPlateWidth = 0, DiaphragmAngle = "L3-1/2x3-1/2x5/16",
        MidDiaphragmsOnLongSpans = false, ToeBoard = false,
    };
    public WalkwaySectionDefinition Clone() => (WalkwaySectionDefinition)MemberwiseClone();
}

public sealed class InspectionAccessDefinition
{
    public bool Enabled { get; set; } = true;

    // ---- inspection cable
    public double RopeDiameter { get; set; } = In(0.5);
    /// <summary>Rope height above the bottom of the web (4'-6).</summary>
    public double RopeHeightAboveWebBottom { get; set; } = Ft(4.5);
    /// <summary>Rope centerline to the web face (3").</summary>
    public double RopeOffsetFromWeb { get; set; } = In(3);
    /// <summary>End anchorage and intermediate supports (WT5x44, 6 bolts) and the support brackets at cross-frame stiffeners (WT5x15, 4 bolts).</summary>
    public string AnchorBracket { get; set; } = "WT5x44";
    public string SupportBracket { get; set; } = "WT5x15";
    public double BracketBoltDiameter { get; set; } = In(0.875);
    /// <summary>Maximum distance between end anchorages / intermediate supports (200'-0; the minimum is 45'-0).</summary>
    public double MaxAnchorSpacing { get; set; } = Ft(200);
    /// <summary>Support brackets only at stiffeners at least this far from an anchorage.</summary>
    public double BracketClearOfAnchor { get; set; } = Ft(2);
    /// <summary>WT piece length (along the girder) of the anchorage bracket: 3 bolt columns at 3" with 1 1/2" ends = 9".</summary>
    public double AnchorBracketLength { get; set; } = In(9);
    /// <summary>WT piece length (vertical) of the support bracket (assumed from the elevation: 4" row spacing + 1 1/2" ends).</summary>
    public double SupportBracketLength { get; set; } = In(7);
    /// <summary>Bolt edge distance from the flange edge / piece end.</summary>
    public double BracketBoltEdge { get; set; } = In(1.5);
    public List<InspectionCableDefinition> Cables { get; set; } = new();

    // ---- walkways
    /// <summary>Walkway type for bays without an entry in <see cref="Walkways"/> (None = no walkway).</summary>
    public WalkwayType DefaultWalkway { get; set; } = WalkwayType.None;
    public List<InspectionWalkwayDefinition> Walkways { get; set; } = new();
    public WalkwaySectionDefinition TypeA { get; set; } = WalkwaySectionDefinition.TypeA();
    public WalkwaySectionDefinition TypeB { get; set; } = WalkwaySectionDefinition.TypeB();
    /// <summary>Top of the grating above the bottom of the web (2 1/2": design sheet 165 note, Unit 3 at the cross girders).</summary>
    public double GratingAboveWebBottom { get; set; } = In(2.5);
    /// <summary>Walkway segments stop this far short of the cross frame line (assumed: half the bottom strut depth).</summary>
    public double WalkwayEndClearance { get; set; } = In(4);

    public InspectionAccessDefinition Clone() => new()
    {
        Enabled = Enabled, RopeDiameter = RopeDiameter, RopeHeightAboveWebBottom = RopeHeightAboveWebBottom, RopeOffsetFromWeb = RopeOffsetFromWeb,
        AnchorBracket = AnchorBracket, SupportBracket = SupportBracket, BracketBoltDiameter = BracketBoltDiameter, MaxAnchorSpacing = MaxAnchorSpacing,
        BracketClearOfAnchor = BracketClearOfAnchor, AnchorBracketLength = AnchorBracketLength, SupportBracketLength = SupportBracketLength,
        BracketBoltEdge = BracketBoltEdge, Cables = Cables.Select(c => c.Clone()).ToList(),
        DefaultWalkway = DefaultWalkway, Walkways = Walkways.Select(w => w.Clone()).ToList(), TypeA = TypeA.Clone(), TypeB = TypeB.Clone(),
        GratingAboveWebBottom = GratingAboveWebBottom, WalkwayEndClearance = WalkwayEndClearance,
    };
}
