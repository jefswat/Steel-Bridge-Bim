using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Arch;

/// <summary>One reported comparison (plan value vs model value) of the arch generator; never used to adjust the model.</summary>
public sealed record ArchCheck(string Item, string Plan, string Model, double Difference, bool Ok, string Note = "");

/// <summary>Generated tied arch: steel (parts, assemblies, anchor bolts, bearing seats), the deck concrete, data items and checks.</summary>
public sealed class ArchModel
{
    /// <summary>Local structure frame (origin, X, Y, Z in world coordinates).</summary>
    public Frame3 Frame { get; init; }
    public required SteelModel Steel { get; init; }
    public required Concrete.ConcreteModel Concrete { get; init; }
    /// <summary>Bearing names in seat order (BearingSeat.Girder = index).</summary>
    public List<string> BearingNames { get; } = new();
    public List<ArchCheck> Checks { get; } = new();
    /// <summary>Bolt count per splice assembly (rib RS / tie TS splices).</summary>
    public Dictionary<string, int> SpliceBolts { get; } = new();
    /// <summary>PT strand size (IFC IfcTendon) when tendon solids are built.</summary>
    public double PtStrandDiameter { get; set; }
    public double PtStrandArea { get; set; }
    /// <summary>Rib circle per rib: centre (local), plane basis (chord c, in-plane up u), radius, rise, arc length.</summary>
    public List<ArchRibResult> Ribs { get; } = new();
    public List<ArchTieResult> Ties { get; } = new();
    public List<ArchHangerResult> Hangers { get; } = new();
    public List<ArchFloorbeamResult> Floorbeams { get; } = new();
    public List<ArchBearingResult> Bearings { get; } = new();
    /// <summary>PT tendons (data, world polylines).</summary>
    public List<(string Name, List<Vec3> Path, string Description)> Tendons { get; } = new();
    /// <summary>Deck slab and curb meshes (also in <see cref="Concrete"/>).</summary>
    public Mesh? Slab { get; set; }
    /// <summary>Slab over the knuckles (end line to the first / last floorbeam, on the knuckle deck plates).</summary>
    public Mesh? SlabEnds { get; set; }
    public Mesh? Curbs { get; set; }
    public double SlabArea { get; set; }
    public List<Diagnostic> Diagnostics => Steel.Diagnostics;

    /// <summary>World point of a local point.</summary>
    public Vec3 World(Vec3 local) => Frame.ToWorld(local);
    /// <summary>Local coordinates of a world point.</summary>
    public Vec3 Local(Vec3 world)
    {
        var d = world - Frame.Origin;
        return new Vec3(Vec3.Dot(d, Frame.X), Vec3.Dot(d, Frame.Y), Vec3.Dot(d, Frame.Z));
    }
}

public sealed record ArchRibResult(string Name, Vec3 Center, Vec3 ChordDir, Vec3 Up, double Radius, double Rise, double ArcLength, double ThetaStart, double ThetaEnd)
{
    public Vec3 PointAt(double theta) => Center + ChordDir * (Radius * Math.Cos(theta)) + Up * (Radius * Math.Sin(theta));
}

public sealed record ArchTieResult(string Name, double StartAngle, double Length, double ClosureMiss, List<(string Name, Vec3 Local)> PanelPoints);

public sealed record ArchHangerResult(string Name, Vec3 TieAnchorWorld, Vec3 RibAnchorWorld, double Length, double PlanLength)
{
    /// <summary>Cable axis at the rib working line (p.147 HC column) when given; the plan length is measured to it.</summary>
    public Vec3? RibWorkPointWorld { get; init; }
    public double PlanBasisLength { get; init; } = double.NaN;
}

public sealed record ArchFloorbeamResult(string Name, Vec3 EndA, Vec3 EndB, double Length, double CrownFromA);

public sealed record ArchBearingResult(string Name, string Support, Vec3 Center, double SeatElevation, double TopOfStack, double SteelUnderside);
