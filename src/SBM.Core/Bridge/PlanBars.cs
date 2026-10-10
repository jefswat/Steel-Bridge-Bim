namespace SBM.Core.Bridge;

/// <summary>What a <see cref="PlanBarGroup"/> is (the plans' bar-list groups of footings, caps and pedestals).</summary>
public enum PlanBarKind
{
    // Footing
    FootingMat, FootingSideVertical, FootingSideHorizontal, UpliftAnchor,
    // Cap
    CapTop, CapBottom, CapSide, CapCantilever, CapHoop, CapCantileverHoop, CapTie, CapHairpin, CapEnd, CapTransverse,
    // Pedestal (under the bearings)
    PedestalLongitudinal, PedestalTransverse, PedestalHoop,
}

/// <summary>
/// One line of a plan bar list ("11g1  FOOTING, BOTTOM, TRANSVERSE  54  19'-6") for one footing or one cap: mark, size, number of bars
/// and the listed length, plus the few placement hints the generator needs. Counts and lengths are the plan's, so marks, counts, lengths
/// and weights reproduce the bar list; positions follow the group kind (see SubstructurePlanBars.cs for the layout rules).
/// </summary>
public sealed class PlanBarGroup
{
    /// <summary>Plan mark, e.g. "11g1" (kept in the bar's location text).</summary>
    public string Mark { get; set; } = "";
    public PlanBarKind Kind { get; set; }
    public string Size { get; set; } = "#5";
    /// <summary>Number of bars of this group on this footing / cap.</summary>
    public int Count { get; set; }
    /// <summary>Listed length (developed, out-to-out as the plans print it); for "VARIES" groups the average length from the weight.</summary>
    public double Length { get; set; }
    public bool Varies { get; set; }
    /// <summary>Footing mats: layer order from the face the mat is next to (1 = outer: lowest of the bottom mat, highest of the top mat).</summary>
    public int Order { get; set; } = 1;
    /// <summary>Footing mats and horizontal side bars: true = bars run along N (the plans' "transverse"), false = along S.</summary>
    public bool AlongN { get; set; }
    /// <summary>Bottom mat bars: extra developed length per end bent up 90 degrees (the hook leg, out-to-out). 0 = straight.</summary>
    public double HookExtra { get; set; }
    /// <summary>Cap end bars: true = vertical bars at the end faces, false = transverse U bars.</summary>
    public bool Vertical { get; set; }
    /// <summary>True = top mat (footing mats only).</summary>
    public bool Top { get; set; }
    public PlanBarGroup Clone() => (PlanBarGroup)MemberwiseClone();
}
