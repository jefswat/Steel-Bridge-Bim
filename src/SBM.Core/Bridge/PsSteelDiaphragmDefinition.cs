using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>
/// How the diaphragm member is set between the two beams' hole groups (user question Q4 / WD-1, Woodbury p.32).
/// The hole heights are fixed above each beam bottom, but adjacent beam bottoms differ (spacing x cross slope), so a
/// LEVEL member (as p.32 labels the W21x50 / W14x38) cannot have its bolt group centred on both plates; the member slots
/// take the difference. <see cref="AlongHoleLine"/> sets the member on the line through the two hole groups instead.
/// </summary>
public enum PsDiaphragmMemberElevation
{
    /// <summary>Member axis horizontal at the mean height of the two plate bolt groups (p.32 "(LEVEL)"). Default.</summary>
    Level,
    /// <summary>Member axis on the line through the centres of the two plate bolt groups.</summary>
    AlongHoleLine,
}

/// <summary>
/// Side of the web hole on which the 9" leg of the bent plate (and the member web) lies, along the beam. The standard's
/// PART SECTION A-A (p.32) has no station arrow and is drawn for the opposite stagger hand of Woodbury's beam layout
/// (p.21), so the side is not given by the plans (UNRESOLVED WD-3): nothing is detailed until it is set.
/// </summary>
public enum PsDiaphragmMemberSide
{
    Unresolved,
    TowardIncreasingStation,
    TowardDecreasingStation,
}

/// <summary>
/// Cast web-hole chain of one beam line in one span, as the plan dimensions it ("INTERMEDIATE DIAPHRAGM BOLT HOLE
/// LOCATIONS", Woodbury p.31): distances along the beam from the beam end at the lower station; every chain point but the
/// last is a hole (pair) location; the chain sums to the beam end-to-end length.
/// </summary>
public sealed class PsDiaphragmHoleChain
{
    /// <summary>1-based span.</summary>
    public int Span { get; set; }
    /// <summary>1-based girder (beam line) index, left to right as in the girder layout.</summary>
    public int Girder { get; set; }
    /// <summary>Chain dimensions (m), from the beam end in the direction of increasing station.</summary>
    public List<double> Dims { get; set; } = new();

    /// <summary>Hole locations from the beam end (m).</summary>
    public IReadOnlyList<double> Holes()
    {
        var r = new List<double>();
        double s = 0;
        for (int i = 0; i + 1 < Dims.Count; i++) { s += Dims[i]; r.Add(s); }
        return r;
    }

    public double Total => Dims.Sum();
}

/// <summary>One steel diaphragm between beam <see cref="LeftGirder"/> and <see cref="LeftGirder"/> + 1 of a span.</summary>
public sealed class PsDiaphragmBay
{
    /// <summary>1-based span.</summary>
    public int Span { get; set; }
    /// <summary>1-based left girder; the bay is LeftGirder..LeftGirder+1.</summary>
    public int LeftGirder { get; set; }
    /// <summary>1-based hole index in the left / right beam's chain.</summary>
    public int LeftHole { get; set; } = 1;
    public int RightHole { get; set; } = 1;
    /// <summary>Main member, exact AISC catalog name (C15x33.9 or W21x50).</summary>
    public string Member { get; set; } = "C15x33.9";
    /// <summary>Lower member hung under a W main member, flange to flange (W14x38 between G and H, span 2), or null.</summary>
    public string? Lower { get; set; }
    /// <summary>Plan (fabricated) member length (m); NaN = from the geometry (slot centres + 2 x end distance).</summary>
    public double PlanLength { get; set; } = double.NaN;
    /// <summary>Plan length of the lower member (m) (W14x38: 5'-4 7/8); NaN = not given (lower member not detailed).</summary>
    public double LowerPlanLength { get; set; } = double.NaN;
    /// <summary>Beam connection detail name for reports ("D" = W21x50 diaphragm, "F" = C15x33.9; same plates).</summary>
    public string Connection { get; set; } = "F";
}

/// <summary>Iowa 1036-BTD beam connection: bent plate on the bay side, backing plate on the far face, bolts, slots.</summary>
public sealed class PsDiaphragmConnection
{
    /// <summary>Lower web hole above the beam bottom (1'-8 1/2) and pitch to the upper hole (9 3/8).</summary>
    public double LowerHoleAboveBottom { get; set; }
    public double HolePitch { get; set; }
    /// <summary>Cast hole diameter in the web (1 1/2, drilling not allowed).</summary>
    public double CastHoleDiameter { get; set; }
    /// <summary>Bent PL 9 x 6 x 1/2 x 1'-1 3/8: 9" leg (member side), 6" leg (on the web), thickness, length (vertical).</summary>
    public double BentLongLeg { get; set; }
    public double BentShortLeg { get; set; }
    public double BentThickness { get; set; }
    public double PlateLength { get; set; }
    /// <summary>Web holes to the plate top / bottom (2").</summary>
    public double PlateEndDistance { get; set; }
    /// <summary>Web hole CL to the outer face of the 9" leg along the beam (6" leg tip 2 1/2 beyond the hole: 6 - 2 1/2).</summary>
    public double HoleToLongLegFace { get; set; }
    /// <summary>Backing PL 6 x 3/8 x 1'-1 3/8 (centred on the holes: 3 + 3).</summary>
    public double BackingWidth { get; set; }
    public double BackingThickness { get; set; }
    /// <summary>1 1/16 holes in the 6" leg and the backing plate.</summary>
    public double PlateHoleDiameter { get; set; }
    /// <summary>Web bolts 7/8 x 9 1/4 (A449, 3" min thread), 2 per connection.</summary>
    public double BoltDiameter { get; set; }
    public double WebBoltLength { get; set; }
    /// <summary>9" leg: 1" x 2" horizontal slots, 3 1/2 from the free edge, at 2, 3 @ 3 1/8, 2 (from the plate bottom).</summary>
    public double LegSlotWidth { get; set; }
    public double LegSlotLength { get; set; }
    public double LegSlotFromFreeEdge { get; set; }
    public List<double> SlotHeights { get; set; } = new();
    /// <summary>Member: 1" x 1 1/2" vertical slots, 1 5/8 from the member end; bolts 7/8 x 2 3/4 (4 per end).</summary>
    public double MemberSlotWidth { get; set; }
    public double MemberSlotLength { get; set; }
    public double MemberSlotFromEnd { get; set; }
    public double MemberBoltLength { get; set; }
    /// <summary>Lower member (W14x38 under W21x50): 18 bolts 7/8 x 2 1/2 in 8 equal spaces, 6" from the ends; two
    /// bolts per row at the AISC workable gage; end plates PL 6 1/2 x 3/8 x 1'-2; two shims PL 6 1/2 x 1/8 x 1'-2 per end.</summary>
    public int LowerBoltRows { get; set; }
    public double LowerBoltEndDistance { get; set; }
    public double LowerBoltLength { get; set; }
    public double EndPlateWidth { get; set; }
    public double EndPlateThickness { get; set; }
    public double EndPlateLength { get; set; }
    public int ShimsPerEnd { get; set; }
    public double ShimThickness { get; set; }

    /// <summary>Iowa DOT standard 1036-1-BTD / 1036-2-BTD (06-14, as modified on Woodbury DS 29-30, PDF p.31-32).</summary>
    public static PsDiaphragmConnection IowaBtd() => new()
    {
        LowerHoleAboveBottom = Ft(1) + In(8.5),
        HolePitch = In(9.375),
        CastHoleDiameter = In(1.5),
        BentLongLeg = In(9),
        BentShortLeg = In(6),
        BentThickness = In(0.5),
        PlateLength = Ft(1) + In(1.375),
        PlateEndDistance = In(2),
        HoleToLongLegFace = In(6 - 2.5),
        BackingWidth = In(6),
        BackingThickness = In(0.375),
        PlateHoleDiameter = In(1.0625),
        BoltDiameter = In(0.875),
        WebBoltLength = In(9.25),
        LegSlotWidth = In(1),
        LegSlotLength = In(2),
        LegSlotFromFreeEdge = In(3.5),
        SlotHeights = new() { In(2), In(2 + 3.125), In(2 + 6.25), In(2 + 9.375) },
        MemberSlotWidth = In(1),
        MemberSlotLength = In(1.5),
        MemberSlotFromEnd = In(1.625),
        MemberBoltLength = In(2.75),
        LowerBoltRows = 9,
        LowerBoltEndDistance = In(6),
        LowerBoltLength = In(2.5),
        EndPlateWidth = In(6.5),
        EndPlateThickness = In(0.375),
        EndPlateLength = Ft(1) + In(2),
        ShimsPerEnd = 2,
        ShimThickness = In(0.125),
    };
}

/// <summary>
/// Steel intermediate diaphragms between prestressed beams bolted through cast web holes (Iowa 1036-BTD; feature D /
/// task T4 of docs/plan-data/concrete-bridge-design.md). Detailed by <c>Diaphragms.PsSteelDiaphragmDetailer</c>.
/// </summary>
public sealed class PsSteelDiaphragmDefinition
{
    public List<PsDiaphragmHoleChain> Holes { get; set; } = new();
    public List<PsDiaphragmBay> Bays { get; set; } = new();
    public PsDiaphragmConnection Connection { get; set; } = PsDiaphragmConnection.IowaBtd();
    /// <summary>Q4 / WD-1: level (default, as labelled on p.32) or along the line through the two hole groups.</summary>
    public PsDiaphragmMemberElevation Elevation { get; set; } = PsDiaphragmMemberElevation.Level;
    /// <summary>WD-3: side of the hole the member lies on (not given by the plans).</summary>
    public PsDiaphragmMemberSide MemberSide { get; set; } = PsDiaphragmMemberSide.Unresolved;
    /// <summary>Beam line names for marks / messages (index = girder - 1), e.g. D, E, F, G, H. Empty = numbers.</summary>
    public List<string> BeamLineNames { get; set; } = new();
    /// <summary>Plan total (lb) of the intermediate diaphragm steel incl. bolts (Woodbury 4,258) - info check; NaN = none.</summary>
    public double PlanSteelWeightLb { get; set; } = double.NaN;
    /// <summary>Plates and members. Woodbury p.5 lists A709 Gr. 36 / 50 / 50W without naming the diaphragm grade, so the
    /// grade is left open; p.31: all diaphragm materials galvanized.</summary>
    public string Material { get; set; } = "Structural steel (A709 grade not given, p.5), galvanized";
}
