using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Diaphragms;

/// <summary>One built part of a concrete diaphragm (closed solid; beams cut out).</summary>
public sealed record ConcreteDiaphragmPart(string Name, Mesh Mesh, double Volume, double XMin, double XMax);

/// <summary>Cast hole through a beam web inside a diaphragm (bars through the beams): position, and whether a built part holds it.</summary>
public sealed record DiaphragmHoleResult(string Beam, Vec3 Center, Vec3 Axis, double Diameter, bool InsideBuiltPart, double StrandClearance);

/// <summary>A concrete end / pier diaphragm at one support (superstructure element; IFC IfcBeam .DIAPHRAGM.).</summary>
public sealed class ConcreteDiaphragm
{
    public required string Name { get; init; }
    public required string Support { get; init; }
    public required int SupportIndex { get; init; }
    public required ConcreteDiaphragmDefinition Definition { get; init; }
    public ConcreteDiaphragmKind Kind => Definition.Kind;
    public List<ConcreteDiaphragmPart> Parts { get; } = new();
    /// <summary>All built parts merged (for the viewport / IFC body).</summary>
    public Mesh Mesh { get; } = new();
    /// <summary>The closed solids (one per slab) the merged <see cref="Mesh"/> is made of - watertight bodies for IFC.</summary>
    public List<Mesh> Pieces { get; } = new();
    public double Volume => Parts.Sum(p => p.Volume);
    /// <summary>Set when the diaphragm is modeled on an item the plans do not fix (e.g. bottom risers between beams).</summary>
    public Assumption? Assumption { get; set; }
    public List<DiaphragmHoleResult> Holes { get; } = new();
    /// <summary>Parts of the definition that are not built (UNRESOLVED), by name.</summary>
    public List<string> UnbuiltParts { get; } = new();
    /// <summary>Length along the CL bearing / CL pier line (x = 0) between the diaphragm ends.</summary>
    public double Length { get; set; }
    public string Material => Definition.Material;
    public string ObjectType => Kind == ConcreteDiaphragmKind.PierContinuity ? "PIER_DIAPHRAGM" : "END_DIAPHRAGM";
    public string Description { get; set; } = "";
}

public enum DiaphragmAccessoryKind { Filler, Keyway }

/// <summary>Thin solid next to a diaphragm: polystyrene / preformed joint filler, or a construction-joint keyway.</summary>
public sealed class DiaphragmAccessory
{
    public required string Name { get; init; }
    public required string Support { get; init; }
    public required DiaphragmAccessoryKind Kind { get; init; }
    public required string Material { get; init; }
    public required Mesh Mesh { get; init; }
    /// <summary>Closed solids the mesh is made of (IFC items).</summary>
    public List<Mesh> Pieces { get; init; } = new();
    public double Volume => Math.Abs(Mesh.Volume());
    public Assumption? Assumption { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>Concrete diaphragms of one prestressed bridge (built by <see cref="ConcreteDiaphragmGenerator"/>).</summary>
public sealed class ConcreteDiaphragmModel
{
    public List<ConcreteDiaphragm> Diaphragms { get; } = new();
    public List<DiaphragmAccessory> Accessories { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public double ConcreteVolume => Diaphragms.Sum(d => d.Volume);
    public bool IsEmpty => Diaphragms.Count == 0 && Accessories.Count == 0;
}
