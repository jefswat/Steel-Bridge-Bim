using SBM.Core.Prestressed;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>Superstructure girder kind.</summary>
public enum GirderKind { SteelPlate, TxDotPrestressed }

/// <summary>TxDOT prestressed concrete I-girder shapes (standard IGD), or a custom PGSuper precast I-beam section.</summary>
public enum TxGirderType { Tx28, Tx34, Tx40, Tx46, Tx54, Tx62, Tx70, Custom }

/// <summary>Girder ends at interior bents (IGEB): square to the girder (conventional bent) or parallel to the bent.</summary>
public enum InteriorGirderEnds { Square, Skewed }

/// <summary>One strand in an explicit layout (from PGSuper): position at midspan and its height at the girder ends.</summary>
public sealed class StrandPoint
{
    /// <summary>Right of the girder CL.</summary>
    public double X { get; set; }
    /// <summary>Above the bottom of the girder, between the harping points.</summary>
    public double Y { get; set; }
    /// <summary>Above the bottom at the girder ends (= Y for straight strands).</summary>
    public double YEnd { get; set; }
    /// <summary>Right of the CL at the girder ends (harped strands fanned from a bundle at the hold-downs); NaN = X.</summary>
    public double XEnd { get; set; } = double.NaN;
    public bool Harped { get; set; }
    /// <summary>Debonded (sheathed) length from the START end of the beam (0 = bonded to the end).</summary>
    public double DebondStart { get; set; }
    /// <summary>Debonded (sheathed) length from the END of the beam (0 = bonded to the end).</summary>
    public double DebondEnd { get; set; }
    /// <summary>Plan symbol of the debond group ("A", "B", ...), for schedules.</summary>
    public string DebondSymbol { get; set; } = "";
    public StrandPoint Clone() => (StrandPoint)MemberwiseClone();
}

/// <summary>How a standard-length beam sits between layout bearings whose distance differs from its standard CL-CL bearing length.</summary>
/// <remarks>AtPierBearing: the standard CL bearing on the layout bearing at the pier (interior support) end; ambiguous when both
/// or neither end is at a pier (UNRESOLVED, drawn centred).</remarks>
public enum StandardBeamPlacement { Centered, AtStartBearing, AtEndBearing, AtPierBearing }

/// <summary>Plan orientation of a bearing pad: length along the beam (square to the beam) or along the support-line normal
/// (width parallel to the support line).</summary>
public enum PadOrientation { SquareToBeam, ParallelToSupport }

/// <summary>
/// A beam definition for some spans / beam lines (e.g. Woodbury: BTD55 in span 1, BTD105 in span 2, BTD60 in span 3).
/// Matching entries are tried in order, the LAST match wins; no match = the bridge's base definition.
/// </summary>
public sealed class PrestressedSpanBeam
{
    /// <summary>Span indices (0 = first span); null = every span.</summary>
    public List<int>? Spans { get; set; }
    /// <summary>Girder (beam line) indices (0 = G1); null = every beam line.</summary>
    public List<int>? Girders { get; set; }
    public PrestressedBeamDefinition Beam { get; set; } = new();
    public bool Matches(int span, int girder) => (Spans == null || Spans.Contains(span)) && (Girders == null || Girders.Contains(girder));
    public PrestressedSpanBeam Clone() => new() { Spans = Spans?.ToList(), Girders = Girders?.ToList(), Beam = Beam.Clone() };
}

/// <summary>
/// Prestressed concrete I-girder: TxDOT Tx28–Tx70 per standards IGD (girder details), IGND (strand grid) and IGEB
/// (bearings and girder ends), or a custom PGSuper precast I-beam. One simple-span beam per span and girder line.
/// Strands fill the 2" grid row by row from the bottom, each row from the "A" positions outward; depressed strands are
/// taken from the A positions (pairs) and rise from the hold-down points to the girder ends. An explicit
/// <see cref="StrandLayout"/> (PGSuper import) replaces the grid fill.
/// </summary>
public sealed class PrestressedBeamDefinition
{
    public TxGirderType Type { get; set; } = TxGirderType.Tx54;
    /// <summary>Section for <see cref="TxGirderType.Custom"/> (e.g. imported from a PGSuper girder library entry).</summary>
    public PrecastIBeamDims? CustomSection { get; set; }

    /// <summary>Total number of strands (filled per the IGND standard pattern).</summary>
    public int StrandCount { get; set; } = 40;
    public double StrandDiameter { get; set; } = In(0.6);
    /// <summary>Strand ultimate strength, ksi.</summary>
    public double StrandStrength { get; set; } = 270;

    /// <summary>Number of depressed strands (even; taken from the A positions, lowest first).</summary>
    public int DepressedStrands { get; set; }
    /// <summary>Depressed strands at the girder ends: height of the TOP depressed strand above the bottom of the
    /// girder. 0 = the highest A position under the top flange.</summary>
    public double DepressedTopAtEnd { get; set; }
    /// <summary>Explicit strands (overrides the grid fill, count and depressed inputs). Null = IGND fill.</summary>
    public List<StrandPoint>? StrandLayout { get; set; }
    /// <summary>Hold-down (harping) points each side of midspan as a fraction of the span: 0 = TxDOT (L/20, 5'-0" min).</summary>
    public double HarpPointFraction { get; set; }

    /// <summary>28-day concrete strength f'c, ksi.</summary>
    public double ConcreteStrength { get; set; } = 8.5;
    /// <summary>Release strength f'ci, ksi.</summary>
    public double ReleaseStrength { get; set; } = 6.0;

    /// <summary>Interior bents: CL bent to end of girder (IGD/IGEB: 3"). For square ends this is to the nearest corner.</summary>
    public double EndGapAtBent { get; set; } = In(3);
    /// <summary>End of girder to CL bearing (IGD: 9"). At abutments the support station is the CL bearing.</summary>
    public double BearingToEnd { get; set; } = In(9);
    /// <summary>Interior bents: square ends (IGEB conventional interior bent) or ends parallel to the bent.</summary>
    public InteriorGirderEnds InteriorEnds { get; set; } = InteriorGirderEnds.Square;

    /// <summary>Elastomeric pad along the girder; 0 = per the IGEB table (by girder, bent type and end skew, with clips).</summary>
    public double PadLength { get; set; }
    public double PadWidth { get; set; } = In(21);
    /// <summary>Total pad height (IGEB: ¼" covers + 6 × ¼" layers + 0.105" laminates ≈ 2¾").</summary>
    public double PadThickness { get; set; } = In(2.75);
    /// <summary>Plan orientation of the pads at interior supports (piers / bents): square to the beam (default, TxDOT IGEB) or
    /// with the pad width along the support line (Woodbury p.20 TOP OF PIER DETAILS part plan: 9 x 2'-4 1/2 pads parallel to
    /// CL pier). Abutment pads stay square to the beam.</summary>
    public PadOrientation PierPadOrientation { get; set; } = PadOrientation.SquareToBeam;

    // ---- state standard beams (all OPTIONAL; defaults keep the TxDOT / PGSuper behavior)

    /// <summary>State standard shape key from <see cref="StandardBeamCatalog"/> ("IA BTD", "MN 30MH"); overrides <see cref="Type"/>.
    /// Null / "" = Type / CustomSection.</summary>
    public string? Standard { get; set; }
    /// <summary>Beam designation for marks and schedules ("BTD55"); "" = the shape name.</summary>
    public string Designation { get; set; } = "";
    /// <summary>STANDARD CL-CL bearing length of the beam (along the beam CL). 0 = the beam spans the layout bearings (Tx path).
    /// When set, the beam is built at this length and the difference against the layout is reported (CHECK warning), never stretched.</summary>
    public double StandardBearingLength { get; set; }
    /// <summary>STANDARD overall length (end to end along the CL); 0 = StandardBearingLength + 2 × BearingToEnd.</summary>
    public double StandardOverallLength { get; set; }
    /// <summary>Placement of the standard-length beam between the layout bearings when they differ. NO default: null = not
    /// decided (the plans don't state it) → UNRESOLVED warning for every beam whose length differs; such a beam is drawn
    /// centred between the layout bearings for display only.</summary>
    public StandardBeamPlacement? Placement { get; set; }
    /// <summary>Plan reference / decision record for <see cref="Placement"/> (quoted in the CHECK warning).</summary>
    public string PlacementBasis { get; set; } = "";
    /// <summary>Hold-down points each side of MIDSPAN of the beam (e.g. 6'-0 / 10'-6 Iowa BTD); 0 = <see cref="HarpPointFraction"/>.</summary>
    public double HoldDownFromMidspan { get; set; }
    /// <summary>Deflected strands by grid column: x of the columns (right of the CL) in <see cref="StrandLayout"/>; every strand in a
    /// column is deflected (with <see cref="HarpedTopBelowTopAtEnd"/>). Null = per-strand Harped / YEnd only.</summary>
    public List<double>? HarpedColumns { get; set; }
    /// <summary>Top deflected strand of each column at the beam ends: distance BELOW THE TOP of the beam (Iowa "4 in below top").</summary>
    public double HarpedTopBelowTopAtEnd { get; set; }
    /// <summary>Spacing of the deflected strands of one column at the ends (2" grid).</summary>
    public double HarpedEndSpacing { get; set; } = In(2);
    /// <summary>Wall thickness of the debond sheathing drawn around debonded strands (display; the product is not dimensioned on the plans).</summary>
    public double SheathThickness { get; set; } = In(1.0 / 16);
    /// <summary>Plan reference of this definition (for warnings).</summary>
    public string Source { get; set; } = "";
    /// <summary>Plan items of this beam not on the sheets: each raised as an "UNRESOLVED (prestressed): ..." warning (with the PDF page).</summary>
    public List<string>? Unresolved { get; set; }
    /// <summary>Different beams per span / beam line (base definition = this one). Null = this beam everywhere.</summary>
    public List<PrestressedSpanBeam>? SpanBeams { get; set; }
    /// <summary>Mild reinforcing of the beam (stirrups, end-zone bars, longitudinal bars) from the beam standard / plan sheet
    /// (OPTIONAL; null = no beam bars, output unchanged). <c>StandardBeamRebar</c> builds it per designation.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BeamReinforcementDefinition? Reinforcement { get; set; }

    /// <summary>The definition that applies to a span and beam line (last matching <see cref="SpanBeams"/> entry, else this).</summary>
    public PrestressedBeamDefinition For(int span, int girder)
    {
        if (SpanBeams != null)
            for (int k = SpanBeams.Count - 1; k >= 0; k--)
                if (SpanBeams[k].Matches(span, girder)) return SpanBeams[k].Beam;
        return this;
    }

    /// <summary>Every definition in use: this one and the per-span ones.</summary>
    public IEnumerable<PrestressedBeamDefinition> AllDefinitions() => new[] { this }.Concat(SpanBeams?.Select(s => s.Beam) ?? Enumerable.Empty<PrestressedBeamDefinition>());

    public PrestressedBeamDefinition Clone()
    {
        var c = (PrestressedBeamDefinition)MemberwiseClone();
        c.CustomSection = CustomSection?.Clone();
        c.StrandLayout = StrandLayout?.Select(s => s.Clone()).ToList();
        c.HarpedColumns = HarpedColumns?.ToList();
        c.Unresolved = Unresolved?.ToList();
        c.SpanBeams = SpanBeams?.Select(s => s.Clone()).ToList();
        c.Reinforcement = Reinforcement?.Clone();
        return c;
    }

    public List<string> Validate()
    {
        var e = new List<string>();
        if (SpanBeams != null)
            foreach (var sb in SpanBeams)
                foreach (var m in sb.Beam.Validate())
                    e.Add($"Spans {(sb.Spans == null ? "all" : string.Join(",", sb.Spans.Select(x => x + 1)))}: {m}");
        if (!string.IsNullOrWhiteSpace(Standard))
        {
            var std = StandardBeamCatalog.Find(Standard);
            if (std == null) e.Add($"Prestressed: unknown standard beam shape '{Standard}' (known: {string.Join(", ", StandardBeamCatalog.All.Select(s => s.Key))}).");
            else if (std.IsStateStandard && StrandLayout == null) e.Add($"Prestressed: {std.Designation} needs an explicit strand layout (the standard's pattern).");
            if (StandardBearingLength < 0 || StandardOverallLength < 0) e.Add("Prestressed: standard lengths must not be negative.");
            if (StandardOverallLength > 0 && StandardOverallLength < StandardBearingLength) e.Add("Prestressed: the standard overall length is shorter than the CL-CL bearing length.");
            if (std is { IsStateStandard: true }) return Finish(e);
        }
        return Finish(ValidateBase(e));
    }

    private List<string> Finish(List<string> e)
    {
        if (StrandDiameter <= 0) e.Add("Prestressed: the strand diameter must be positive.");
        if (BearingToEnd <= 0 || PadLength < 0 || PadWidth <= 0 || PadThickness <= 0) e.Add("Prestressed: bearing dimensions must be positive.");
        if (StrandLayout != null && StrandLayout.Any(s => s.DebondStart < 0 || s.DebondEnd < 0)) e.Add("Prestressed: debond lengths must not be negative.");
        return e;
    }

    private List<string> ValidateBase(List<string> e)
    {
        if (Type == TxGirderType.Custom)
        {
            if (CustomSection == null) e.Add("Prestressed: a custom girder type needs a section (import one from PGSuper).");
            else e.AddRange(CustomSection.Validate());
        }
        if (StrandLayout == null)
        {
            var shape = TxGirderCatalog.Get(this);
            int capacity = shape.StrandPositions().Count;
            if (StrandCount < 2 || StrandCount % 2 != 0) e.Add("Prestressed: the strand count must be even and at least 2.");
            if (StrandCount > capacity) e.Add($"Prestressed: {shape.Name} holds at most {capacity} strands.");
            if (DepressedStrands < 0 || DepressedStrands % 2 != 0) e.Add("Prestressed: depressed strands must be an even number.");
        }
        return e;
    }
}
