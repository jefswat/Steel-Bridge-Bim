using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Substructure;

namespace SBM.Core.CableStayed;

/// <summary>What <see cref="CableStayedGenerator"/> built: steel (pylon, stays, steel deck, barriers), concrete (concrete deck), substructure (piers,
/// abutment seats, footings, a concrete trunk), the stays and pylon curves for reports, the analysis model and the first-cut forces.</summary>
public sealed class CableStayedModel
{
    public SteelModel Steel { get; init; } = new() { Bolt = new BoltGeometry(Units.UnitSystem.In(0.875)), BoltGrade = "A325" };
    public Concrete.ConcreteModel Concrete { get; init; } = new();
    public SubstructureModel? Substructure { get; set; }
    /// <summary>Pylon frame (origin = base point, X / Y / Z).</summary>
    public Frame3 PylonFrame { get; set; }
    /// <summary>Frames of all pylons (first = <see cref="PylonFrame"/>).</summary>
    public List<Frame3> PylonFrames { get; } = new();
    public List<CsLegCurve> Legs { get; } = new();
    public List<CsStay> Stays { get; } = new();
    /// <summary>Deck CL stations: begin / end, support stations (name), pylon station.</summary>
    public double DeckBegin { get; set; }
    public double DeckEnd { get; set; }
    public List<(string Name, double Station, bool Pylon)> Supports { get; } = new();
    /// <summary>Deck section (offset right +, height above the CL grade) and its properties.</summary>
    public List<Vec2> DeckSection { get; } = new();
    public CsSectionProps DeckProps { get; set; } = new();
    /// <summary>Deck weight per length when it is not the section area x density (edge girder deck: steel + slab), N/m; 0 = from <see cref="DeckProps"/>.</summary>
    public double DeckWeightPerLength { get; set; }
    public CsAnalysisModel Analysis { get; } = new();
    public CsForceReport Forces { get; set; } = new();
    public List<CsClearanceResult> Clearances { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
}

/// <summary>A pylon leg as built: arc-length table of the curve (pylon frame -> world through <see cref="CableStayedModel.PylonFrame"/>).</summary>
public sealed class CsLegCurve
{
    public required string Name { get; init; }
    public required Bridge.CsPylonLeg Definition { get; init; }
    /// <summary>Pylon number (index into <see cref="Bridge.CableStayedDefinition.AllPylons"/> / <see cref="CableStayedModel.PylonFrames"/>).</summary>
    public int Pylon { get; init; }
    /// <summary>World points along the curve (dense) and their cumulative lengths.</summary>
    public required Vec3[] Points { get; init; }
    public required double[] S { get; init; }
    public double Length => S[^1];

    /// <summary>World point at a fraction of the length (0 = start, 1 = tip).</summary>
    public Vec3 At(double u)
    {
        double s = Math.Clamp(u, 0, 1) * Length;
        int i = Array.BinarySearch(S, s);
        if (i >= 0) return Points[i];
        i = ~i;
        if (i <= 0) return Points[0];
        if (i >= S.Length) return Points[^1];
        double f = (s - S[i - 1]) / Math.Max(1e-12, S[i] - S[i - 1]);
        return Points[i - 1] + (Points[i] - Points[i - 1]) * f;
    }

    public Vec3 TangentAt(double u)
    {
        double du = 1e-3;
        return (At(Math.Min(1, u + du)) - At(Math.Max(0, u - du))).Normalized();
    }
}

public sealed class CsStay
{
    public required string Name { get; init; }
    public required string Group { get; init; }
    public required int Leg { get; init; }
    /// <summary>Deck (or ground) anchor and pylon anchor, world.</summary>
    public required Vec3 Lower { get; init; }
    public required Vec3 Upper { get; init; }
    /// <summary>Deck anchor station (NaN for a ground anchor).</summary>
    public double Station { get; init; } = double.NaN;
    public double PylonFraction { get; init; }
    public double Diameter { get; init; }
    public double Area { get; init; }
    public double Length => (Upper - Lower).Length;
    /// <summary>Angle to the horizontal (degrees).</summary>
    public double AngleDeg => Math.Atan2(Upper.Z - Lower.Z, (Upper - Lower).XY.Length) * 180 / Math.PI;
}

/// <summary>Section properties about the centroid: area, second moments about the horizontal (Iy, strong for a deck) and vertical (Iz) axes, torsion.</summary>
public sealed record CsSectionProps
{
    public double A { get; init; }
    public double Iy { get; init; }
    public double Iz { get; init; }
    public double J { get; init; }
    /// <summary>Centroid in the section coordinates (deck: offset, height).</summary>
    public Vec2 Centroid { get; init; }
    public double Depth { get; init; }
    public double Width { get; init; }
}

public sealed class CsNode
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required Vec3 P { get; init; }
}

public sealed class CsMember
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    /// <summary>deck / link / pylon / trunk / crossbeam / strut / stay / pier.</summary>
    public required string Kind { get; init; }
    public required int N1 { get; init; }
    public required int N2 { get; init; }
    public required CsSectionProps Section { get; init; }
    public double E { get; init; }
    public double Density { get; init; }
    /// <summary>Tension-only cable (truss element).</summary>
    public bool Cable { get; init; }
    /// <summary>Unit vector of the section's local "width" axis (horizontal normal to the member, or the deck's right normal).</summary>
    public Vec3 WidthAxis { get; init; }
}

public sealed class CsSupport
{
    public required int Node { get; init; }
    public required string Name { get; init; }
    /// <summary>Restrained translations (x, y, z) and rotations (rx, ry, rz) in world axes.</summary>
    public required bool[] Fixed { get; init; }
}

/// <summary>Analysis wireframe: nodes / members (sections, E, density) / supports; world coordinates in meters.</summary>
public sealed class CsAnalysisModel
{
    public List<CsNode> Nodes { get; } = new();
    public List<CsMember> Members { get; } = new();
    public List<CsSupport> Supports { get; } = new();
}

/// <summary>First-cut dead and live load forces (tributary lengths, statics; no frame analysis) to start sizing the stays and the pylon lean.</summary>
public sealed class CsForceReport
{
    public double DeckSelfWeight { get; set; }     // N/m
    public double BarrierWeight { get; set; }      // N/m
    public double SuperimposedDead { get; set; }   // N/m
    public double LiveLoad { get; set; }           // N/m (pedestrian load x deck top width)
    public List<CsStayForce> Stays { get; } = new();
    public List<CsLegBalance> Legs { get; } = new();
    /// <summary>Pylon base (frame origin) resultant of the stays (DL / DL+LL) and the pylon self weight, world N and N m.</summary>
    public Vec3 BaseForceDead { get; set; }
    public Vec3 BaseMomentDead { get; set; }
    public Vec3 BaseForceTotal { get; set; }
    public Vec3 BaseMomentTotal { get; set; }
    /// <summary>Base overturning moments: longitudinal (+ = tipping toward the pylon frame +X) and transverse (+ = toward +Y, right).</summary>
    public double BaseMomentLongDead { get; set; }
    public double BaseMomentTransDead { get; set; }
    public double BaseMomentLongTotal { get; set; }
    public double BaseMomentTransTotal { get; set; }
    public double PylonSelfWeight { get; set; }
    /// <summary>Base statics per pylon (the fields above = the first pylon).</summary>
    public List<CsPylonBase> Pylons { get; } = new();
    public List<string> Notes { get; } = new();
}

public sealed class CsStayForce
{
    public required string Stay { get; init; }
    public double Tributary { get; init; }
    public double Dead { get; init; }
    public double Total { get; init; }
    public double RequiredArea { get; init; }
    public double Area { get; init; }
    public double Ratio => RequiredArea / Math.Max(1e-12, Area);
}

public sealed class CsLegBalance
{
    public required string Leg { get; init; }
    public double SelfWeight { get; init; }
    /// <summary>Sum of the stay forces on the leg (DL), world.</summary>
    public Vec3 StayForceDead { get; init; }
    public Vec3 StayForceTotal { get; init; }
}

public sealed record CsClearanceResult(string Name, double Required, double Provided, double AtStation, bool Ok);

/// <summary>Base statics of one pylon (stays + its self weight about its frame origin), world N / N m; long + = toward its frame +X.</summary>
public sealed record CsPylonBase(string Support, Vec3 ForceDead, Vec3 ForceTotal, double MomentLongDead, double MomentLongTotal, double MomentTransDead, double MomentTransTotal, double SelfWeight);
