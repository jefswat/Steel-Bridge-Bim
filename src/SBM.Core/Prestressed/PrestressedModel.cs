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
    /// <summary>Debonded (sheathed) length from the start / end face of the beam along the strand; 0 = bonded.</summary>
    public double DebondStart { get; init; }
    public double DebondEnd { get; init; }
    public string DebondSymbol { get; init; } = "";
    public bool Debonded => DebondStart > 0 || DebondEnd > 0;
    /// <summary>Sheathing pieces: the strand path from each end face over its debond length (0, 1 or 2 polylines).</summary>
    public List<Vec3[]> Sheathing()
    {
        var list = new List<Vec3[]>();
        if (DebondStart > 0) list.Add(Cut(Points, DebondStart));
        if (DebondEnd > 0) list.Add(Cut(Points.Reverse().ToArray(), DebondEnd));
        return list;
    }

    /// <summary>The first <paramref name="length"/> of a polyline.</summary>
    public static Vec3[] Cut(IReadOnlyList<Vec3> p, double length)
    {
        var r = new List<Vec3> { p[0] };
        double left = length;
        for (int i = 0; i + 1 < p.Count && left > 1e-9; i++)
        {
            double l = (p[i + 1] - p[i]).Length;
            if (l >= left) { r.Add(p[i] + (p[i + 1] - p[i]) * (left / l)); break; }
            r.Add(p[i + 1]);
            left -= l;
        }
        return r.ToArray();
    }
}

/// <summary>
/// Exact body of a beam with an exact <see cref="TxGirderShape.Profile"/>: the section profile in the vertical plane through
/// <see cref="Frame"/> (X = section x direction, Y = up, Z = X × Y), swept along <see cref="Extrusion"/> (the sloped beam axis),
/// then cut by the two end planes (points + OUTWARD normals). IFC: IfcExtrudedAreaSolid (arcs kept) clipped by two half spaces.
/// </summary>
public sealed record BeamBody(Frame3 Frame, double SectionXSign, Vec3 Extrusion, Vec3 StartPoint, Vec3 StartNormal, Vec3 EndPoint, Vec3 EndNormal);

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

    /// <summary>The beam definition used for this beam (the bridge's base one or a per-span one).</summary>
    public PrestressedBeamDefinition? Definition { get; init; }
    /// <summary>Beam designation ("BTD55"); "" = the shape name.</summary>
    public string Designation { get; init; } = "";
    public string TypeName => Designation != "" ? Designation : Shape.Name;
    /// <summary>Layout CL-CL bearing distance along this beam (from the framing), and standard − layout (0 when the beam is built to the layout).</summary>
    public double LayoutBearingSpan { get; init; }
    public double LengthDifference { get; init; }
    /// <summary>True when the beam was built at its standard length (<see cref="PrestressedBeamDefinition.StandardBearingLength"/>).</summary>
    public bool IsStandardLength { get; init; }
    /// <summary>Set when the beam is modeled on an assumption the plans do not fix (e.g. placement of a standard-length beam).</summary>
    public Assumption? Assumption { get; init; }
    /// <summary>Exact body (shapes with arcs); null = the mesh is the body.</summary>
    public BeamBody? Body { get; init; }
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

    /// <summary>Total debonded (sheathed) strand length.</summary>
    public double DebondedLength => Beams.Sum(b => b.Strands.Sum(s => s.DebondStart + s.DebondEnd));

    public static double PolyLength(IReadOnlyList<Vec3> p)
    {
        double l = 0;
        for (int i = 0; i + 1 < p.Count; i++) l += (p[i + 1] - p[i]).Length;
        return l;
    }
}
