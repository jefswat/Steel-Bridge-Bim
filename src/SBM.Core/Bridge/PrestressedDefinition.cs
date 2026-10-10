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
    public StrandPoint Clone() => (StrandPoint)MemberwiseClone();
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

    public PrestressedBeamDefinition Clone()
    {
        var c = (PrestressedBeamDefinition)MemberwiseClone();
        c.CustomSection = CustomSection?.Clone();
        c.StrandLayout = StrandLayout?.Select(s => s.Clone()).ToList();
        return c;
    }

    public List<string> Validate()
    {
        var e = new List<string>();
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
        if (StrandDiameter <= 0) e.Add("Prestressed: the strand diameter must be positive.");
        if (BearingToEnd <= 0 || PadLength < 0 || PadWidth <= 0 || PadThickness <= 0) e.Add("Prestressed: bearing dimensions must be positive.");
        return e;
    }
}
