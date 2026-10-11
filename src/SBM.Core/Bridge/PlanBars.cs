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
    /// <summary>
    /// The plan's bend diagram of this mark (OPTIONAL; "" = straight or not read): "U" = <see cref="Dims"/> {leg, base, leg}, "L" = {leg, long run},
    /// "A14" = {vertical leg, top run, slanted leg length, slanted leg horizontal projection}. All out-to-out as the plans print them
    /// (the bar list length = their sum, no bend deductions).
    /// </summary>
    public string Shape { get; set; } = "";
    /// <summary>Printed dimensions of <see cref="Shape"/> (meters, order as documented there).</summary>
    public double[] Dims { get; set; } = Array.Empty<double>();
    /// <summary>Printed pin (bend) diameter D of the bend diagram, meters; 0 = not printed / straight.</summary>
    public double Pin { get; set; }
    /// <summary>Hairpins (5c5 / 5c6): stations of THIS mark in each zone of the plan's elevation (zones left to right, 0 = zone of another mark; "N SPA. @ 1'-4; k-5c5 BARS" gives k / 2 stations).</summary>
    public int[] ZoneStations { get; set; } = Array.Empty<int>();
    /// <summary>Distance of the first / last station from the top end of the cap (the plan's "4" / "6"), meters.</summary>
    public double ZoneMargin { get; set; }
    /// <summary>Short cap skin bars (5a11 / 5a12): -1 = at the begin (left, S0) end of the cap, +1 = at the far end, 0 = not read from the plan elevation.</summary>
    public int End { get; set; }
    /// <summary>Pedestal hoops (5n2): the girders whose pedestals carry them, as the plan note prints them ("AT GIRDERS C &amp; D ONLY" = "C,D";
    /// "B-D" = a range; letter A = girder index 0, a seat of the ahead span counts too). "" = not given: the hoops are placed at mid-cap and flagged UNRESOLVED.</summary>
    public string Girders { get; set; } = "";
    /// <summary>Pedestal bars (U bars and hoops) on a pier with separate back and ahead pedestal blocks: "" = every block; "BACK" / "AHEAD" = only the blocks of
    /// the seats on that side of the CL pier (Pier 11WB: 5n1 back row, 5n2 ahead row; Pier 9G: 5n3 and the 5n2 hoops in the back pedestals); "LINE" = one set per
    /// girder line, the base running across the back AND the ahead block (Pier 9G 5n1, base 5'-7 across both 3'-0 blocks).</summary>
    public string PedestalRow { get; set; } = "";
    /// <summary>Pedestal U bars: distance of the end bars of a block from the block edges as the plan prints it ("4, 5 EQ. SPA., 4"; Pier 9G "6, 2 SPA. @ 1'-0, 6"),
    /// centre of bar, meters. 0 = 4".</summary>
    public double EdgeDistance { get; set; }
    public PlanBarGroup Clone() => (PlanBarGroup)MemberwiseClone();
}
