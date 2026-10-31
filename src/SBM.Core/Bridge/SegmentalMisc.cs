
namespace SBM.Core.Bridge;

// OPTIONAL miscellaneous items of a precast segmental box bridge (SegmentalDefinition.Misc; null = none, omitted from the .sbm, output unchanged):
// pile groups under the bent footings, deck drains through the segments, expansion joints at the unit ends (device envelope, end segment
// blockout, parapet end posts, parapet cover plates). Generator: Segmental/SegmentalGeneratorMisc.cs. All lengths in meters. Nothing is
// defaulted from a guess: a value the plans do not give stays NaN and the item is flagged (UnresolvedFlag text: page + plain reason) or not built.

/// <summary>One pile of a bent footing: plan position at the FOOTING BOTTOM plane in the bent frame (S along the bent line from the bent's
/// alignment point, right +; N along the alignment, up-station +) and the plan direction the TIP moves (2:12 batter arrows); 0, 0 = plumb.</summary>
public sealed class SegmentalPileDefinition
{
    public double S { get; set; }
    public double N { get; set; }
    public double BatterS { get; set; }
    public double BatterN { get; set; }
    /// <summary>Pile head above the footing bottom for this pile (NaN = the group's <see cref="SegmentalFoundationDefinition.Embedment"/>).</summary>
    public double Embedment { get; set; } = double.NaN;
    public SegmentalPileDefinition Clone() => (SegmentalPileDefinition)MemberwiseClone();
}

/// <summary>Footing / pile group data of one bent (the footing solid itself is <see cref="SegmentalPier"/>).</summary>
public sealed class SegmentalFoundationDefinition
{
    /// <summary>Support name of the bent ("Bent 18").</summary>
    public string Bent { get; set; } = "";
    /// <summary>OPTIONAL lateral position of the column centre along the bent line (S, right +; 0 = on the bent's alignment point). The cap /
    /// footing outlines and the bearing offsets are plain inputs of <see cref="SegmentalPier"/> / <see cref="SegmentalBearing"/>.</summary>
    public double ColumnOffsetS { get; set; }
    /// <summary>Square precast pile: side, plan label (e.g. "36\" SQ PS"), material text.</summary>
    public double PileSize { get; set; } = double.NaN;
    public string PileLabel { get; set; } = "";
    public string PileMaterial { get; set; } = "";
    /// <summary>OPTIONAL square pile corner chamfer (0 = sharp) and a round longitudinal VOID (diameter 0 = solid pile) that stops
    /// <see cref="VoidSolidAtHead"/> below the modeled head and <see cref="VoidSolidAtTip"/> above the tip (solid end sections; void ends flat).</summary>
    public double PileChamfer { get; set; }
    public double PileVoidDiameter { get; set; }
    public double VoidSolidAtHead { get; set; }
    public double VoidSolidAtTip { get; set; }
    /// <summary>Batter = vertical per 1 horizontal (2:12 = 6); 0 = plumb piles.</summary>
    public double BatterRatio { get; set; }
    /// <summary>Pile head above the footing bottom (NaN = not given: no piles, UNRESOLVED).</summary>
    public double Embedment { get; set; } = double.NaN;
    /// <summary>Tip elevation used for the model (plan "pile tip no higher than" elevation; NaN = not given: no piles, UNRESOLVED).</summary>
    public double TipElevation { get; set; } = double.NaN;
    public List<SegmentalPileDefinition> Piles { get; set; } = new();
    /// <summary>Plan check data (NaN / 0 = none): total pile length of the bent (bill of material LF, in meters), plan pile count,
    /// max factored axial load (tons / pile), scour critical elevation.</summary>
    public double PlanTotalLength { get; set; } = double.NaN;
    public int PlanCount { get; set; }
    public double MaxFactoredAxialTons { get; set; } = double.NaN;
    public double ScourCriticalElevation { get; set; } = double.NaN;
    public string Source { get; set; } = "";
    /// <summary>UNRESOLVED text (page + reason) put on every pile of the group (dimension-only flag: the piles exist).</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalFoundationDefinition Clone()
    {
        var c = (SegmentalFoundationDefinition)MemberwiseClone();
        c.Piles = Piles.Select(p => p.Clone()).ToList();
        return c;
    }
}

public enum SegmentalDrainSide { LowSide, Left, Right, HighSide }

/// <summary>Round deck drains through the top slab / wing, one per matching segment.</summary>
public sealed class SegmentalDeckDrainDefinition
{
    public string Name { get; set; } = "deck drain";
    /// <summary>Segment types that get a drain (empty = none) and an optional segment-name regex filter.</summary>
    public List<SegmentType> SegmentTypes { get; set; } = new();
    public string SegmentPattern { get; set; } = "";
    /// <summary>Drain diameter, CL distance from the deck edge measured across the top surface (section x), top below the riding surface.</summary>
    public double Diameter { get; set; } = double.NaN;
    public double EdgeDistance { get; set; } = double.NaN;
    public double TopBelowSurface { get; set; }
    /// <summary>LowSide = the side the cross slope falls to at the drain station.</summary>
    public SegmentalDrainSide Side { get; set; } = SegmentalDrainSide.LowSide;
    /// <summary>Position along the segment as a fraction of its length from the bent-side joint (0.5 = centre).</summary>
    public double AlongFraction { get; set; } = 0.5;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>UNRESOLVED text on every drain (dimension-only).</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalDeckDrainDefinition Clone() { var c = (SegmentalDeckDrainDefinition)MemberwiseClone(); c.SegmentTypes = SegmentTypes.ToList(); return c; }
}

/// <summary>Parapet end post at a joint: the part ABOVE the parapet (the parapet below it is the parapet solid), on both parapets.</summary>
public sealed class SegmentalJointEndPost
{
    public double Length { get; set; } = double.NaN;
    public double Width { get; set; } = double.NaN;
    /// <summary>Parapet inside face offset from the alignment (|x|), parapet height and the end post height above it.</summary>
    public double InsideFace { get; set; } = double.NaN;
    public double ParapetHeight { get; set; } = double.NaN;
    public double HeightAboveParapet { get; set; } = double.NaN;
    public SegmentalJointEndPost Clone() => (SegmentalJointEndPost)MemberwiseClone();
}

/// <summary>Steel cover plate on the traffic face of each parapet across the joint (recessed flush).</summary>
public sealed class SegmentalJointCoverPlate
{
    public double Thickness { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    /// <summary>Bottom of the plate above the deck surface.</summary>
    public double Bottom { get; set; }
    /// <summary>Plate length on the left / right parapet and the part of it on THIS unit's side of the CL joint.</summary>
    public double LengthLeft { get; set; } = double.NaN;
    public double LengthRight { get; set; } = double.NaN;
    public double TowardUnit { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public SegmentalJointCoverPlate Clone() => (SegmentalJointCoverPlate)MemberwiseClone();
}

/// <summary>Expansion joint at the first / last support of the segmental unit.</summary>
public sealed class SegmentalExpansionJointDefinition
{
    public string Name { get; set; } = "";
    /// <summary>Support (bent) name; the joint is at the deck end next to it.</summary>
    public string Support { get; set; } = "";
    public string Designation { get; set; } = "";
    /// <summary>CL joint from the support line along the alignment (+ up-station; NaN = not given).</summary>
    public double CentreFromSupport { get; set; } = double.NaN;
    /// <summary>Opening at the setting temperature (NaN = not given: derived as twice the distance deck end - CL joint, flagged).</summary>
    public double Opening { get; set; } = double.NaN;
    /// <summary>Device envelope limits across (|x| from the alignment) and depth below the deck surface (NaN = the blockout depth).</summary>
    public double HalfWidth { get; set; } = double.NaN;
    public double DeviceDepth { get; set; } = double.NaN;
    /// <summary>Blockout in this unit's deck end (top slab notch): length from the deck end face, depth below the surface, distance of its ends
    /// from the deck edges (NaN = no blockout built).</summary>
    public double BlockoutLength { get; set; } = double.NaN;
    public double BlockoutDepth { get; set; } = double.NaN;
    public double BlockoutEdgeInset { get; set; } = double.NaN;
    public SegmentalJointEndPost? EndPost { get; set; }
    public SegmentalJointCoverPlate? CoverPlate { get; set; }
    public string Source { get; set; } = "";
    /// <summary>UNRESOLVED texts raised once and put on the device envelope.</summary>
    public List<string> Unresolved { get; set; } = new();
    public SegmentalExpansionJointDefinition Clone()
    {
        var c = (SegmentalExpansionJointDefinition)MemberwiseClone();
        c.EndPost = EndPost?.Clone(); c.CoverPlate = CoverPlate?.Clone(); c.Unresolved = Unresolved.ToList();
        return c;
    }
}

/// <summary>Where a fixture's local frame sits (A along the horizontal alignment tangent, + up-station; N horizontal across; Z up, level).</summary>
public enum SegmentalFixtureReference
{
    /// <summary>Origin = deck edge at the top surface on the fixture's side; N + outboard (away from the bridge); Z from that point.</summary>
    DeckEdge,
    /// <summary>Origin = top of the bottom slab (finished floor of the segment) under the INSIDE face of the web on the fixture's side; Z above that
    /// floor; N + into the cell, measured from the web inside face AT EACH PIECE'S MID HEIGHT (the webs are inclined).</summary>
    WebInside,
    /// <summary>Origin = top of the bottom slab (finished floor) on -L- (bottom slab top line of a solid section at a bent); N + toward the fixture side.</summary>
    Floor,
    /// <summary>The box section frame (follows the cross slope): origin on -L- at the top surface, N = section x (right +, independent of the side),
    /// Z = up normal to the top surface (negative = into the slab).</summary>
    DeckSection,
}

/// <summary>One solid of a fixture: an axis-aligned box in the fixture frame (A0..A1, N0..N1, Z0..Z1), or with <see cref="Section"/> a member swept
/// straight from (A0, N0, Z0) to (A1, N1, Z1): AISC W / L names ("W8X18", "L3X3X1/4"), "PL t x w" (w in the section's up direction), "ROD d".
/// Section up = Z for members not vertical, else A; <see cref="Roll"/> turns it about the axis (degrees).</summary>
public sealed class SegmentalFixturePiece
{
    public string Name { get; set; } = "";
    public SBM.Core.Misc.MiscKind Kind { get; set; } = SBM.Core.Misc.MiscKind.PlatformMember;
    public string Description { get; set; } = "";
    public string Material { get; set; } = "";
    /// <summary>Item by others: envelope with Pset SBM_Placeholder (basis text).</summary>
    public bool Placeholder { get; set; }
    public string PlaceholderBasis { get; set; } = "";
    public double A0 { get; set; }
    public double A1 { get; set; }
    public double N0 { get; set; }
    public double N1 { get; set; }
    public double Z0 { get; set; }
    public double Z1 { get; set; }
    public string Section { get; set; } = "";
    public double Roll { get; set; }
    /// <summary>UNRESOLVED text of this piece (dimension-only flag); empty = the fixture's flag only.</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalFixturePiece Clone() => (SegmentalFixturePiece)MemberwiseClone();
}

/// <summary>A fixture (platform, luminaire set, panel, screen ...) repeated at -L- stations on one side of the box, built from
/// <see cref="Pieces"/> in its local level frame (<see cref="SegmentalFixtureReference"/>). One misc assembly per station.</summary>
public sealed class SegmentalFixtureDefinition
{
    public string Name { get; set; } = "";
    public SBM.Core.Misc.MiscGroup Group { get; set; } = SBM.Core.Misc.MiscGroup.Lighting;
    /// <summary>Assembly category (database / IFC ObjectType).</summary>
    public string Category { get; set; } = "";
    public List<double> Stations { get; set; } = new();
    /// <summary>OPTIONAL names per station (same order; empty = "{Name} n").</summary>
    public List<string> Labels { get; set; } = new();
    /// <summary>+1 = right of -L-, -1 = left; 0 = not given (DeckEdge / WebInside fixtures are then not built; Floor / DeckSection use +1).</summary>
    public int Side { get; set; }
    public SegmentalFixtureReference Reference { get; set; }
    public List<SegmentalFixturePiece> Pieces { get; set; } = new();
    public string Source { get; set; } = "";
    /// <summary>UNRESOLVED text (page + reason) raised once and put on every piece (dimension-only: the plans show the item).</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalFixtureDefinition Clone()
    {
        var c = (SegmentalFixtureDefinition)MemberwiseClone();
        c.Stations = Stations.ToList(); c.Labels = Labels.ToList(); c.Pieces = Pieces.Select(p => p.Clone()).ToList();
        return c;
    }
}

public sealed class SegmentalMiscDefinition
{
    public List<SegmentalFoundationDefinition> Foundations { get; set; } = new();
    public List<SegmentalDeckDrainDefinition> Drains { get; set; } = new();
    public List<SegmentalExpansionJointDefinition> Joints { get; set; } = new();
    /// <summary>OPTIONAL fixtures attached to the box (empty = none).</summary>
    public List<SegmentalFixtureDefinition> Fixtures { get; set; } = new();
    /// <summary>Plan items inventoried but not built (raised as warnings; text built with UnresolvedFlag).</summary>
    public List<string> Unresolved { get; set; } = new();
    public SegmentalFoundationDefinition? Foundation(string bent) => Foundations.FirstOrDefault(f => f.Bent.Equals(bent, StringComparison.OrdinalIgnoreCase));
    public SegmentalMiscDefinition Clone() => new()
    {
        Foundations = Foundations.Select(f => f.Clone()).ToList(), Drains = Drains.Select(d => d.Clone()).ToList(),
        Joints = Joints.Select(j => j.Clone()).ToList(), Fixtures = Fixtures.Select(f => f.Clone()).ToList(), Unresolved = Unresolved.ToList(),
    };
}
