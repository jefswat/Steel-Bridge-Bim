using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Prestressed;

/// <summary>One prestressing strand in a beam: a straight or depressed (harped) path, end face to end face.</summary>
public sealed class StrandInstance
{
    public required Vec3[] Points { get; init; }
    /// <summary>Position at midspan in section coordinates (x right of CL, y above the bottom).</summary>
    public required Vec2 Section { get; init; }
    public bool Depressed { get; init; }
    /// <summary>Height above the bottom at the girder ends (= Section.Y for straight strands).</summary>
    public double EndHeight { get; init; }
}

/// <summary>One simple-span TxDOT I-girder between two bearing lines.</summary>
public sealed class PrestressedBeam
{
    public required string Mark { get; init; }
    public required int Girder { get; init; }
    public required int Span { get; init; }
    public required TxGirderShape Shape { get; init; }
    public required Mesh Mesh { get; init; }
    /// <summary>End of girder to end of girder along the CL (top of beam).</summary>
    public required double Length { get; init; }
    /// <summary>CL bearing to CL bearing.</summary>
    public required double BearingSpan { get; init; }
    /// <summary>Top of beam at the CL girder at each end of the beam and at the bearings.</summary>
    public required Vec3 TopStart { get; init; }
    public required Vec3 TopEnd { get; init; }
    public required Vec3 BearingStart { get; init; }
    public required Vec3 BearingEnd { get; init; }
    public required double Slope { get; init; }
    public double SkewStart { get; init; }
    public double SkewEnd { get; init; }
    public List<StrandInstance> Strands { get; } = new();
    public double StrandDiameter { get; init; }
    /// <summary>Distance from each girder end to the hold-down points (depressed strands only).</summary>
    public double HoldDownFromEnd { get; init; }
    /// <summary>Haunch (bottom of deck to top of beam at the CL girder) along the beam: minimum / maximum.</summary>
    public double MinHaunch { get; init; }
    public double MaxHaunch { get; init; }
    public string Material { get; init; } = "Class H concrete";
    public double Volume => Math.Abs(Mesh.Volume());
    /// <summary>Mass at 155 pcf (IGD: total weight of concrete and reinforcing).</summary>
    public double Mass => Volume * 2482.9;
    public string Name => $"G{Girder + 1} Span {Span + 1}";
}

/// <summary>Elastomeric bearing pad under a prestressed beam end.</summary>
public sealed class PrestressedPad
{
    public required string Mark { get; init; }
    public required string Support { get; init; }
    public required int Girder { get; init; }
    public required int Span { get; init; }
    public required Mesh Mesh { get; init; }
    public required Vec3 Center { get; init; }
    public required double Length { get; init; }
    public required double Width { get; init; }
    public required double Thickness { get; init; }
    public string Description { get; init; } = "";
    /// <summary>Distance the pad was moved off the nominal CL bearing to stay on the girder (square ends at skewed bents).</summary>
    public double OffNominal { get; init; }
}

/// <summary>Generated TxDOT prestressed concrete superstructure (beams, strands, pads, bearing seats).</summary>
public sealed class PrestressedModel
{
    public required PrestressedBeamDefinition Definition { get; init; }
    public required TxGirderShape Shape { get; init; }
    public List<PrestressedBeam> Beams { get; } = new();
    public List<PrestressedPad> Pads { get; } = new();
    public List<BearingSeat> BearingSeats { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public TimeSpan GenerationTime { get; set; }

    public double ConcreteVolume => Beams.Sum(b => b.Volume);
    public double TotalMass => Beams.Sum(b => b.Mass);
    /// <summary>Total strand length.</summary>
    public double StrandLength => Beams.Sum(b => b.Strands.Sum(s => PolyLength(s.Points)));

    public static double PolyLength(IReadOnlyList<Vec3> p)
    {
        double l = 0;
        for (int i = 0; i + 1 < p.Count; i++) l += (p[i + 1] - p[i]).Length;
        return l;
    }
}
