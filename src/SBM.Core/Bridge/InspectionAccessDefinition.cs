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

    // ---- As-designed details (all OPTIONAL, null = legacy behaviour; JSON omitted when null).

    /// <summary>Width of the grating (I-80 / Ramp G typical sections: "WALKWAY GRATING (LEVEL) (2'-0)"); null = <see cref="ClearWidth"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? GratingWidth { get; set; }
    /// <summary>true = top rail angle with its horizontal leg on the post top and its vertical leg on the INSIDE face of the posts (typical
    /// sections); null / false = legacy (vertical leg in the post's outer leg plane).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? TopRailInside { get; set; }
    /// <summary>Top of grating above the bottom of the cross girder web where the walkway ends at a cross girder (I-80 Type A 1'-0, Type B
    /// 2 1/2"); null = <see cref="InspectionAccessDefinition.GratingAboveWebBottom"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? GratingAboveCrossGirderWebBottom { get; set; }
    /// <summary>Cross girder web face to the last rail post (rails / posts stop there; stringers and grating continue to the web; I-80 2'-6
    /// "BOTH SIDES"); null = rails run to the stringer end.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? RailEndFromCrossGirderWeb { get; set; }
    /// <summary>End diaphragm at a cross girder: its hanging leg this far outside the cross girder flange edge (Type B, L3 1/2 clear of the
    /// bottom flange); null = the end diaphragm is at the stringer end (Type A PL diaphragm at the end connection).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? EndDiaphragmClearOfCrossGirderFlange { get; set; }

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

    // ---- As-designed walkway supports (OPTIONAL, null = legacy: grating GratingAboveWebBottom over the bottom of web everywhere, segments
    // WalkwayEndClearance short of each frame line). true = the walkway stringers bear on the cross frame BOTTOM STRUT (top of strut =
    // bottom of stringer, measured on the generated strut at the walkway), end StringerEndFromFrame beyond the strut; at plate diaphragms
    // the grating is GratingAboveDiaphragmFlange[style] over the top of the diaphragm bottom flange and the stringers end StringerEndFromWeb
    // from the diaphragm web; rails / posts stop RailClearOfFrame short of any frame member in the rail zone.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? SupportOnFrames { get; set; }
    /// <summary>Stringer end beyond the strut (along the walkway; I-80 sheet 167 Section G-G "1 1/2 ±").</summary>
    public double StringerEndFromFrame { get; set; } = In(1.5);
    /// <summary>Grating end to the frame plane (gratings of the two segments 1" apart over the strut, Section G-G).</summary>
    public double GratingGapAtFrame { get; set; } = In(0.5);
    /// <summary>Rail / post clearance to a frame member in the rail zone (Section G-G "1 1/2 *", rail end 3 MAX from the post).</summary>
    public double RailClearOfFrame { get; set; } = In(1.5);
    /// <summary>Stringer end to a diaphragm / cross girder web face (Sections E-E, S-S, K-K "1 1/2 ±").</summary>
    public double StringerEndFromWeb { get; set; } = In(1.5);
    /// <summary>Grating end to a cross girder web face (Sections K-K / O-O "1" CL.").</summary>
    public double GratingClearOfWeb { get; set; } = In(1);
    /// <summary>Top of grating above the top of the bottom flange of a plate diaphragm, by cross frame style name (I-80: "7" 1'-4, "8" 6").</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, double>? GratingAboveDiaphragmFlange { get; set; }
    /// <summary>Plan pages of the walkway details for the UNRESOLVED texts (e.g. "I-80 PDF 169-174").</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    // ---- Cable details (OPTIONAL)
    /// <summary>Hole for the wire rope in every girder stiffener the rope passes (cable sheet Section C-C: 7/8" typ.); null = none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? StiffenerRopeHole { get; set; }
    /// <summary>Rope hole diameter in the stiffeners at the bearing lines of the named supports (e.g. 1 1/2" at Piers 12WB-15WB).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, double>? StiffenerRopeHoleAtSupports { get; set; }
    /// <summary>true = the rope ends at the ends of the anchorage tee stem (the shackle / turnbuckle hardware between is not modeled) instead of
    /// running through the stem to its centre.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? RopeEndsAtAnchorStem { get; set; }

    public InspectionAccessDefinition Clone() => new()
    {
        Enabled = Enabled, RopeDiameter = RopeDiameter, RopeHeightAboveWebBottom = RopeHeightAboveWebBottom, RopeOffsetFromWeb = RopeOffsetFromWeb,
        AnchorBracket = AnchorBracket, SupportBracket = SupportBracket, BracketBoltDiameter = BracketBoltDiameter, MaxAnchorSpacing = MaxAnchorSpacing,
        BracketClearOfAnchor = BracketClearOfAnchor, AnchorBracketLength = AnchorBracketLength, SupportBracketLength = SupportBracketLength,
        BracketBoltEdge = BracketBoltEdge, Cables = Cables.Select(c => c.Clone()).ToList(),
        DefaultWalkway = DefaultWalkway, Walkways = Walkways.Select(w => w.Clone()).ToList(), TypeA = TypeA.Clone(), TypeB = TypeB.Clone(),
        GratingAboveWebBottom = GratingAboveWebBottom, WalkwayEndClearance = WalkwayEndClearance,
        SupportOnFrames = SupportOnFrames, StringerEndFromFrame = StringerEndFromFrame, GratingGapAtFrame = GratingGapAtFrame, RailClearOfFrame = RailClearOfFrame,
        StringerEndFromWeb = StringerEndFromWeb, GratingClearOfWeb = GratingClearOfWeb,
        GratingAboveDiaphragmFlange = GratingAboveDiaphragmFlange == null ? null : new(GratingAboveDiaphragmFlange), Source = Source,
        StiffenerRopeHole = StiffenerRopeHole, StiffenerRopeHoleAtSupports = StiffenerRopeHoleAtSupports == null ? null : new(StiffenerRopeHoleAtSupports),
        RopeEndsAtAnchorStem = RopeEndsAtAnchorStem,
    };
}
