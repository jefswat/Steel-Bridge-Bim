namespace SBM.Core.Bridge;

/// <summary>Which mat a plan deck bar belongs to (transverse bars are the outer layer of each mat, longitudinal bars sit inside them).</summary>
public enum DeckBarLayer { Top, Bottom }

public enum DeckBarSide { Left, Right }

/// <summary>One bar of a plan transverse line: its mark and printed length (0 = the remainder of the line, with the lap added).</summary>
public sealed class DeckLinePiece
{
    public string Mark { get; set; } = "";
    /// <summary>Printed (horizontal) length; 0 = whatever remains of the line between the deck edges less the pieces before it, plus the lap.</summary>
    public double Length { get; set; }
    public DeckLinePiece Clone() => (DeckLinePiece)MemberwiseClone();
}

/// <summary>
/// A run of equally spaced transverse bar lines ("126 SPA. @ 7 = 73'-6, 127-7a1, 7a4 &amp; 7a6 BARS" on the plan's transverse layouts).
/// Lines are normal to the alignment (the plans' radial layout; the skewed end lines just cut them short) and run between the deck edges
/// less the side cover; each line is made of the listed pieces, lapped. Positions are measured along a deck edge from the deck-end line.
/// </summary>
public sealed class DeckTransverseZone
{
    public string Name { get; set; } = "";
    public DeckBarLayer Layer { get; set; }
    public string Size { get; set; } = "#7";
    /// <summary>Deck edge the positions are measured along ("measured along the south edge of slab").</summary>
    public DeckBarSide Reference { get; set; } = DeckBarSide.Right;
    /// <summary>Distance along the reference edge from the begin deck-end line to the FIRST line of the zone (see the two switches below).</summary>
    public double StartDistance { get; set; }
    /// <summary>True = StartDistance is measured from the LAST line of the previous zone of the same layer (where that line crosses this zone's reference edge), e.g. the "10" between two layout runs.</summary>
    public bool StartAfterPrevious { get; set; }
    /// <summary>True = the LAST line is StartDistance before the end of the reference edge (the end deck-end line), lines run back from it.</summary>
    public bool AnchorToEnd { get; set; }
    /// <summary>Index (in the transverse list) of a zone whose lines this zone sits between ("5j1 bars, place between 7a bars"): one line midway between each pair of consecutive lines. -1 = not used. Count (when &gt; 0) limits the number of lines placed; Spacing/StartDistance are ignored.</summary>
    public int BetweenZone { get; set; } = -1;
    /// <summary>With <see cref="BetweenZone"/>: also one line between the last line of that zone and the first line of the next zone of the same layer.</summary>
    public bool BetweenToNext { get; set; }
    /// <summary>Spacing along the reference edge.</summary>
    public double Spacing { get; set; }
    /// <summary>Number of lines (the plan's "N SPA." + 1).</summary>
    public int Count { get; set; }
    /// <summary>Lines are rotated from the normal to the alignment by this angle (degrees, + = right end up-station); 0 = normal.</summary>
    public double AngleDegrees { get; set; }
    /// <summary>Pieces in order from the first side; lines shorter than the fixed pieces get shortened pieces (reported).</summary>
    public List<DeckLinePiece> Pieces { get; set; } = new();
    public double Lap { get; set; }
    /// <summary>Edge the first piece starts from.</summary>
    public DeckBarSide FirstFrom { get; set; } = DeckBarSide.Right;
    /// <summary>Alternate lines start from the other edge (staggers the laps).</summary>
    public bool Alternate { get; set; }
    public DeckTransverseZone Clone()
    {
        var z = (DeckTransverseZone)MemberwiseClone();
        z.Pieces = Pieces.Select(p => p.Clone()).ToList();
        return z;
    }
}

public enum DeckLongRefKind { LeftEdge, RightEdge, Girder, Offset }

/// <summary>A line the longitudinal bars are spaced between: a deck edge, a girder web line (1-based number as G1..), or a fixed alignment offset.</summary>
public sealed class DeckLongRef
{
    public DeckLongRefKind Kind { get; set; }
    public int Girder { get; set; }
    public double Offset { get; set; }
    public static DeckLongRef Edge(DeckBarSide s) => new() { Kind = s == DeckBarSide.Left ? DeckLongRefKind.LeftEdge : DeckLongRefKind.RightEdge };
    public static DeckLongRef OfGirder(int g) => new() { Kind = DeckLongRefKind.Girder, Girder = g };
    public static DeckLongRef AtOffset(double o) => new() { Kind = DeckLongRefKind.Offset, Offset = o };
    public DeckLongRef Clone() => (DeckLongRef)MemberwiseClone();
}

/// <summary>
/// Longitudinal bar lines between two reference lines over a station range: <see cref="Spaces"/> equal spaces between the first and last
/// line (which sit <see cref="LeftMargin"/> / <see cref="RightMargin"/> inside the references), alternating top and bottom layers ("24 EQUAL
/// SPACES; 25-6b BARS (ALT. TOP AND BOTTOM OF SLAB)"). Bars are lap spliced in stock lengths along their line.
/// </summary>
public sealed class DeckLongZone
{
    public string Name { get; set; } = "";
    public DeckLongRef Left { get; set; } = DeckLongRef.Edge(DeckBarSide.Left);
    public DeckLongRef Right { get; set; } = DeckLongRef.Edge(DeckBarSide.Right);
    /// <summary>Station range; NaN = the deck begin / end. The zone's bars run Lap beyond the end station (into the next zone).</summary>
    public double StartStation { get; set; } = double.NaN;
    public double EndStation { get; set; } = double.NaN;
    public double LeftMargin { get; set; }
    public double RightMargin { get; set; }
    /// <summary>Equal spaces between the first and the last line (lines = Spaces + 1).</summary>
    public int Spaces { get; set; }
    /// <summary>Layer of the first (left) line; the next lines alternate unless <see cref="Alternate"/> is off.</summary>
    public DeckBarLayer FirstLayer { get; set; } = DeckBarLayer.Top;
    public bool Alternate { get; set; } = true;
    /// <summary>Lines at explicit distances from the left reference instead of equal spaces (the overhang bars of the typical sections); layers alternate from <see cref="FirstLayer"/>.</summary>
    public List<double>? Positions { get; set; }
    /// <summary>Positions are measured from the right reference (toward the left) instead of from the left one.</summary>
    public bool PositionsFromRight { get; set; }
    /// <summary>Layer of each line of <see cref="Positions"/> (read from the plan's section); null = alternate from <see cref="FirstLayer"/>.</summary>
    public List<DeckBarLayer>? PositionLayers { get; set; }
    public string Size { get; set; } = "#6";
    /// <summary>Mark of stock-length bars.</summary>
    public string Mark { get; set; } = "";
    /// <summary>Mark of the shorter closing bar of a line (the stock mark when empty).</summary>
    public string ClosureMark { get; set; } = "";
    public double StockLength { get; set; }
    public double Lap { get; set; }
    /// <summary>True = the last bar of a line is also a stock bar (lapped further) instead of a shorter closing bar.</summary>
    public bool LastIsStock { get; set; }
    public DeckLongZone Clone()
    {
        var z = (DeckLongZone)MemberwiseClone();
        z.Left = Left.Clone(); z.Right = Right.Clone();
        z.Positions = Positions?.ToList();
        z.PositionLayers = PositionLayers?.ToList();
        return z;
    }
}

/// <summary>One line of a plan slab bar list for a unit ("7a1  SLAB, TRANSVERSE, TOP  2,070  56'-4  238,351").</summary>
public sealed class DeckPlanBarLine
{
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#7";
    public string Location { get; set; } = "";
    public int Count { get; set; }
    /// <summary>Printed length (the maximum for "VARIES").</summary>
    public double Length { get; set; }
    public bool Varies { get; set; }
    public double WeightLb { get; set; }
    public DeckPlanBarLine Clone() => (DeckPlanBarLine)MemberwiseClone();
}

/// <summary>
/// OPTIONAL plan-driven deck reinforcing (null = the generic generator): zones of transverse lines and longitudinal bays as the plans'
/// slab sheets print them, plus the plan's bar list for the unit. Marks, counts, lengths and weights reproduce the bar list; bar-list rows
/// with no placed bar stay in the schedule at the printed count and length (no 3D bar), and every difference is reported.
/// </summary>
public sealed class DeckPlanReinforcement
{
    public List<DeckTransverseZone> Transverse { get; set; } = new();
    public List<DeckLongZone> Longitudinal { get; set; } = new();
    public List<DeckPlanBarLine> PlanBarList { get; set; } = new();
    /// <summary>
    /// Mark families ("6b") compared as a whole: the plan's rows of the family are not matched mark by mark (the plan's start/closure pieces are not distinguished
    /// by the generator) and are not put in the schedule; the placed marks starting with the family prefix are compared with their sum.
    /// </summary>
    public List<string> Families { get; set; } = new();
    /// <summary>Clear distance of the longitudinal bar ends from the deck-end lines (0 = use the deck side cover).</summary>
    public double LongitudinalEndCover { get; set; }
    public double LongitudinalBeginCover { get; set; }
    /// <summary>
    /// Clear distance of the transverse bar ends from the begin / end deck-end line, normal to the line (0 = the deck side cover). The plans' bars stop short of the
    /// expansion joint blockout; the value is calibrated on the bar lengths the plans print at the skewed ends (docs/plan-data/deck-rebar.md).
    /// </summary>
    public double TransverseBeginClear { get; set; }
    public double TransverseEndClear { get; set; }
    /// <summary>Relative tolerance used when comparing the model with the plan bar list (counts exactly, weights by this fraction).</summary>
    public double WeightTolerance { get; set; } = 0.03;
    public DeckPlanReinforcement Clone() => new()
    {
        Transverse = Transverse.Select(z => z.Clone()).ToList(),
        Longitudinal = Longitudinal.Select(z => z.Clone()).ToList(),
        PlanBarList = PlanBarList.Select(l => l.Clone()).ToList(), Families = Families.ToList(),
        LongitudinalEndCover = LongitudinalEndCover, LongitudinalBeginCover = LongitudinalBeginCover, TransverseBeginClear = TransverseBeginClear, TransverseEndClear = TransverseEndClear, WeightTolerance = WeightTolerance,
    };
}
