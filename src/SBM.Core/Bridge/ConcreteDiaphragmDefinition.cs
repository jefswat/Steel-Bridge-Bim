namespace SBM.Core.Bridge;

/// <summary>Kind of a cast-in-place diaphragm at a support of a prestressed-beam bridge (concrete-bridge design feature C).</summary>
public enum ConcreteDiaphragmKind
{
    /// <summary>Integral abutment diaphragm cast with the deck on top of the pile cap (Iowa 2087-BTCD).</summary>
    IntegralEnd,
    /// <summary>Semi-integral end diaphragm on the abutment back ledge (MnDOT).</summary>
    SemiIntegralEnd,
    /// <summary>Continuity diaphragm over a pier (beams made continuous by the deck and the diaphragm).</summary>
    PierContinuity,
}

/// <summary>Elevation reference of a section point.</summary>
public enum DiaphragmZRef
{
    /// <summary>Finished deck surface (<see cref="DeckDefinition.FinishedElevation"/>) at the point.</summary>
    DeckTop,
    /// <summary>Deck soffit = deck top − <see cref="DeckDefinition.Thickness"/> (the split between deck slab and diaphragm).</summary>
    Soffit,
    /// <summary>The diaphragm bottom = the stepped substructure top (<see cref="ConcreteDiaphragmDefinition.BottomSteps"/>).</summary>
    Bottom,
    /// <summary>An absolute elevation (<see cref="DiaphragmSectionPoint.Dz"/> is the elevation).</summary>
    Elevation,
}

/// <summary>A vertex of a diaphragm section: X normal to the support line (abutments: from the CL bearing, + toward the span;
/// piers: from the CL pier, + ahead station), elevation = reference + <see cref="Dz"/> (for <see cref="DiaphragmZRef.Elevation"/>: Dz = elevation).</summary>
public sealed class DiaphragmSectionPoint
{
    public double X { get; set; }
    public DiaphragmZRef Ref { get; set; }
    public double Dz { get; set; }
    public DiaphragmSectionPoint() { }
    public DiaphragmSectionPoint(double x, DiaphragmZRef r, double dz = 0) { X = x; Ref = r; Dz = dz; }
    public DiaphragmSectionPoint Clone() => (DiaphragmSectionPoint)MemberwiseClone();
}

public enum DiaphragmEndKind
{
    /// <summary>End face along the alignment offset line <see cref="DiaphragmEnd.Offset"/> (e.g. deck edge, gutter line, stage joint).</summary>
    Offset,
    /// <summary>The point on the CL bearing / CL pier line at <see cref="DiaphragmEnd.Distance"/> from the point where that line meets the
    /// offset line <see cref="DiaphragmEnd.Offset"/> (measured along the line toward the other end); the end face then follows the alignment
    /// offset line through that point (longitudinal stage joint).</summary>
    AlongLineFromOffset,
    /// <summary>Flush with the outside face (top-flange edge line) of the fascia beam of the part (Woodbury p.20 Detail B).</summary>
    FasciaOutside,
}

public sealed class DiaphragmEnd
{
    public DiaphragmEndKind Kind { get; set; }
    public double Offset { get; set; }
    public double Distance { get; set; }
    /// <summary>Plan reference of the end (shown in reports).</summary>
    public string Source { get; set; } = "";
    public static DiaphragmEnd AtOffset(double offset, string source = "") => new() { Kind = DiaphragmEndKind.Offset, Offset = offset, Source = source };
    public static DiaphragmEnd Fascia(string source = "") => new() { Kind = DiaphragmEndKind.FasciaOutside, Source = source };
    public static DiaphragmEnd AlongLine(double offset, double distance, string source = "") =>
        new() { Kind = DiaphragmEndKind.AlongLineFromOffset, Offset = offset, Distance = distance, Source = source };
    public DiaphragmEnd Clone() => (DiaphragmEnd)MemberwiseClone();
}

/// <summary>One prism-like part of a diaphragm: a closed section polygon (vertically convex: every vertical line meets it in one
/// interval; non-vertical edges keep one elevation reference) swept along the support line between the two ends. The beams that
/// run into the part are cut out automatically (beam outline + haunch up to the soffit).</summary>
public sealed class DiaphragmPart
{
    public string Name { get; set; } = "";
    public List<DiaphragmSectionPoint> Section { get; set; } = new();
    /// <summary>Ends of this part; null = the diaphragm's.</summary>
    public DiaphragmEnd? Left { get; set; }
    public DiaphragmEnd? Right { get; set; }
    /// <summary>Non-empty = the part is a plan item that cannot be built (what the plans lack + PDF page): not built, UNRESOLVED warning.</summary>
    public string Unresolved { get; set; } = "";
    public DiaphragmPart Clone()
    {
        var c = (DiaphragmPart)MemberwiseClone();
        c.Section = Section.Select(p => p.Clone()).ToList();
        c.Left = Left?.Clone();
        c.Right = Right?.Clone();
        return c;
    }
}

/// <summary>Level step of the diaphragm bottom (top of the stepped cap / pier cap) under a beam line.</summary>
public sealed class DiaphragmStep
{
    /// <summary>1-based girder (beam line).</summary>
    public int Girder { get; set; }
    public double Elevation { get; set; }
    public DiaphragmStep() { }
    public DiaphragmStep(int girder, double elevation) { Girder = girder; Elevation = elevation; }
    public DiaphragmStep Clone() => (DiaphragmStep)MemberwiseClone();
}

/// <summary>Where the risers between the bottom steps are.</summary>
public enum DiaphragmRiserRule
{
    /// <summary>Not stated on the plans: drawn at mid-bay for display only, the diaphragm is flagged UNRESOLVED (Assumption).</summary>
    Unresolved,
    /// <summary>At mid-bay between the beam CLs on the CL bearing / CL pier line (confirmed).</summary>
    MidBay,
    /// <summary>At <see cref="ConcreteDiaphragmDefinition.RiserS"/>.</summary>
    Explicit,
}

public enum DiaphragmFillerKind
{
    /// <summary>A layer against the end face of the built parts at <see cref="DiaphragmFillerDefinition.Side"/> (−1 left, +1 right), outward.</summary>
    EndFace,
    /// <summary>A section polygon (same rules as a part) swept between the diaphragm ends.</summary>
    Section,
    /// <summary>The gap under each beam end inside the diaphragm, between the beam bottom flange and the diaphragm bottom, around the
    /// bearing pad ("1 in. preformed filler around bearings").</summary>
    AroundBearings,
}

/// <summary>Polystyrene / preformed joint filler between the diaphragm and adjacent concrete, modeled as a thin solid.</summary>
public sealed class DiaphragmFillerDefinition
{
    public string Name { get; set; } = "";
    /// <summary>Material as named on the plans (e.g. "Type B polystyrene", "Preformed expansion joint filler").</summary>
    public string Material { get; set; } = "";
    public DiaphragmFillerKind Kind { get; set; }
    public int Side { get; set; }
    public double Thickness { get; set; }
    public List<DiaphragmSectionPoint> Section { get; set; } = new();
    public string Source { get; set; } = "";
    public DiaphragmFillerDefinition Clone()
    {
        var c = (DiaphragmFillerDefinition)MemberwiseClone();
        c.Section = Section.Select(p => p.Clone()).ToList();
        return c;
    }
}

/// <summary>Construction-joint keyway in the substructure top under the diaphragm (filled by the diaphragm pour), modeled as a thin solid
/// below the diaphragm bottom.</summary>
public sealed class DiaphragmKeywayDefinition
{
    public string Name { get; set; } = "Keyway";
    /// <summary>Center X (same convention as the section).</summary>
    public double X { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
    /// <summary>Only in the bays between beams (bottom flange edge to bottom flange edge); false = full diaphragm length.</summary>
    public bool BetweenBeams { get; set; } = true;
    /// <summary>Non-empty: an item of the keyway the plans do not fix (with page); the keyway is flagged UNRESOLVED (Assumption) with this reason.</summary>
    public string Unresolved { get; set; } = "";
    /// <summary>What is modeled for <see cref="Unresolved"/>.</summary>
    public string Basis { get; set; } = "";
    public string Source { get; set; } = "";
    public DiaphragmKeywayDefinition Clone() => (DiaphragmKeywayDefinition)MemberwiseClone();
}

/// <summary>Cast hole through the beam web inside the diaphragm (bars through the beams), per beam end: from the beam end along the CL beam,
/// height above the beam bottom.</summary>
public sealed class DiaphragmBeamHole
{
    public double FromEnd { get; set; }
    public double AboveBottom { get; set; }
    public double Diameter { get; set; }
    public string Source { get; set; } = "";
    public DiaphragmBeamHole Clone() => (DiaphragmBeamHole)MemberwiseClone();
}

/// <summary>
/// OPTIONAL concrete end / pier diaphragm at a support of a PRESTRESSED bridge (<see cref="SupportDefinition.Diaphragm"/>), built by
/// <c>Diaphragms.ConcreteDiaphragmGenerator</c> after the beams and the substructure. Superstructure element, split from the deck slab at
/// the soffit (the slab above stays in the deck solid). Every value is a plan value; items the plans do not give are listed UNRESOLVED.
/// </summary>
public sealed class ConcreteDiaphragmDefinition
{
    public ConcreteDiaphragmKind Kind { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Plan reference (sheet / page).</summary>
    public string Source { get; set; } = "";
    public string Material { get; set; } = "Concrete (deck mix)";
    public List<DiaphragmPart> Parts { get; set; } = new();
    public DiaphragmEnd? Left { get; set; }
    public DiaphragmEnd? Right { get; set; }
    public List<DiaphragmStep> BottomSteps { get; set; } = new();
    public DiaphragmRiserRule Risers { get; set; } = DiaphragmRiserRule.Unresolved;
    /// <summary>Explicit riser positions: distance along the support line from the alignment point (+ right).</summary>
    public List<double> RiserS { get; set; } = new();
    /// <summary>Why the riser position is unresolved (plan page) - shown with <see cref="DiaphragmRiserRule.Unresolved"/>.</summary>
    public string RiserSource { get; set; } = "";
    public List<DiaphragmFillerDefinition> Fillers { get; set; } = new();
    public List<DiaphragmKeywayDefinition> Keyways { get; set; } = new();
    public List<DiaphragmBeamHole> BeamHoles { get; set; } = new();
    /// <summary>Expected clear gap between the beam bottom and the diaphragm bottom step (bearing height), for the seat check; NaN = no check.</summary>
    public double PlanBearingGap { get; set; } = double.NaN;
    /// <summary>Plan items not dimensioned (raised as UNRESOLVED warnings).</summary>
    public List<string> Unresolved { get; set; } = new();

    public ConcreteDiaphragmDefinition Clone()
    {
        var c = (ConcreteDiaphragmDefinition)MemberwiseClone();
        c.Parts = Parts.Select(p => p.Clone()).ToList();
        c.Left = Left?.Clone();
        c.Right = Right?.Clone();
        c.BottomSteps = BottomSteps.Select(s => s.Clone()).ToList();
        c.RiserS = RiserS.ToList();
        c.Fillers = Fillers.Select(f => f.Clone()).ToList();
        c.Keyways = Keyways.Select(k => k.Clone()).ToList();
        c.BeamHoles = BeamHoles.Select(h => h.Clone()).ToList();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}
