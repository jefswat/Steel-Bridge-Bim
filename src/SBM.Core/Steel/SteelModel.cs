using SBM.Core.Geometry;
using SBM.Core.Framing;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

public enum PartCategory
{
    FlangePlate, WebPlate, BearingStiffener, ConnectionStiffener, IntermediateStiffener,
    Gusset, FillerPlate, Angle, Channel, SolePlate, BearingPad, Weld,
    SplicePlate, BearingPlate, MasonryPlate, BearingDisc, ShearPin, GuideBar, Ptfe,
}

public enum AssemblyKind { GirderFieldSection, CrossFrame, Bearing, FieldSplice, LateralBracing }

/// <summary>Member role, used for IFC predefined types and reports.</summary>
public enum MemberRole { None, TopChord, BottomChord, Diagonal, Diaphragm, Lateral }

public enum BoltKind { Structural, Anchor }

public sealed class SteelAssembly
{
    public required string Mark { get; init; }
    public required AssemblyKind Kind { get; init; }
    public string Description { get; init; } = "";
    /// <summary>Girder index (girder field sections, bearings) or −1.</summary>
    public int Girder { get; init; } = -1;
}

public sealed class SteelPart
{
    public required string Mark { get; init; }
    public required PartCategory Category { get; init; }
    public required string Assembly { get; init; }
    /// <summary>Size description, e.g. PL 16" x 1" or L4x4x3/8.</summary>
    public required string Description { get; init; }
    public required Mesh Mesh { get; init; }
    public string Material { get; init; } = "ASTM A709 Gr. 50";
    public MemberRole Role { get; init; }
    /// <summary>Overall length (plates: along the girder or longest dimension; angles: cut length).</summary>
    public double Length { get; init; }
    public double Volume => Math.Abs(Mesh.Volume());
    /// <summary>Rolled shapes: AISC nominal weight (lb/ft, all pieces of the member) — mass = weight x length, as on a steel list.</summary>
    public double NominalWeightLbPerFt { get; init; }
    public double Mass => NominalWeightLbPerFt > 0 ? NominalWeightLbPerFt * 1.48816394 * Length : Category switch
    {
        PartCategory.BearingPad or PartCategory.BearingDisc => Volume * 1150,
        PartCategory.Ptfe => Volume * 2200,
        _ => Volume * SteelCatalog.Density,
    };
}

/// <summary>One installed bolt. Frame origin is the bearing face under the head; +Z points from head to nut.</summary>
public sealed class BoltInstance
{
    public required Frame3 Frame { get; init; }
    public required double Grip { get; init; }
    public required double Length { get; init; }
    public required string Assembly { get; init; }
    public BoltKind Kind { get; init; } = BoltKind.Structural;
    /// <summary>Anchor bolts: diameter and embedment (structural bolts use the model's bolt spec).</summary>
    public double Diameter { get; init; }
    public double Embedment { get; init; }
}

/// <summary>Headed shear stud. Frame origin on the top of flange, +Z up the stud.</summary>
public sealed class StudInstance
{
    public required Frame3 Frame { get; init; }
    public required double Length { get; init; }
    public required string Assembly { get; init; }
}

/// <summary>Top of the bearing seat (pedestal / cap) under one bearing. Location/Axis/footprint are set when a girder
/// has more than one bearing at a support (prestressed beams on both sides of an interior bent).</summary>
public sealed record BearingSeat(string Support, int Girder, double Station, double SeatElevation, double SolePlateMaxThickness,
    string Type = "Elastomeric", string Fixity = "", Vec2? Location = null, Vec2? Axis = null, double FootprintLength = 0, double FootprintWidth = 0);

/// <summary>
/// A cross frame as detailed, in its own plane: x = plan distance from the left web centerline along the frame,
/// z = elevation along the frame's up axis (left top of web = its true elevation). Used for detail drawings.
/// </summary>
public sealed class CrossFrameDetail2D
{
    public required string Mark { get; init; }
    public required bool AtSupport { get; init; }
    public required Framing.ResolvedCrossFrameType Type { get; init; }
    public required double Span { get; init; }
    public required double LeftWebTop { get; init; }
    public required double RightWebTop { get; init; }
    /// <summary>Web faces (x) toward the bay at the left and right girders.</summary>
    public required double LeftWebFace { get; init; }
    public required double RightWebFace { get; init; }
    public required double PlateWidth { get; init; }
    public required double PlateThickness { get; init; }
    public required double GussetThickness { get; init; }
    public required Vec2[] WorkPoints { get; init; } // A, B, C, D
    public List<(string Code, string Angle, Vec2[] Outline, Vec2 HeelA, Vec2 HeelB, bool Front)> Members { get; } = new();
    public List<(string Code, Vec2[] Outline)> Gussets { get; } = new();
    /// <summary>Member work lines (work point to work point).</summary>
    public List<(string Code, Vec2 Wp1, Vec2 Wp2)> WorkLines { get; } = new();
    public List<Vec2> Bolts { get; } = new();
    public Vec2[]? Filler { get; set; }
    public double WeldLap { get; init; }
    public double WeldSize { get; init; }
    public string ChannelName { get; init; } = "";
}

/// <summary>Explicit 3D steel model derived from a <see cref="BridgeFraming"/>.</summary>
public sealed class SteelModel
{
    public required BoltGeometry Bolt { get; init; }
    public required string BoltGrade { get; init; }
    public List<SteelAssembly> Assemblies { get; } = new();
    public List<SteelPart> Parts { get; } = new();
    public List<BoltInstance> Bolts { get; } = new();
    public List<StudInstance> Studs { get; } = new();
    public double StudDiameter { get; init; }
    public double StudHeadDiameter { get; init; }
    public double StudHeadHeight { get; init; }
    public List<BearingSeat> BearingSeats { get; } = new();
    public List<CrossFrameDetail2D> CrossFrameDetails { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public TimeSpan GenerationTime { get; set; }

    public double SteelMass => Parts.Where(p => p.Category is not (PartCategory.BearingPad or PartCategory.Weld or PartCategory.BearingDisc or PartCategory.Ptfe)).Sum(p => p.Mass);

    /// <summary>Headed stud solid (shank + head), base at the origin, +Z up.</summary>
    public Mesh StudMesh(double length, int segments = 12)
    {
        var m = new Mesh();
        var f = Frame3.World;
        MeshBuilder.AddPrism(m, f, StudDiameter / 2, segments, 0, length - StudHeadHeight);
        MeshBuilder.AddPrism(m, f, StudHeadDiameter / 2, segments, length - StudHeadHeight, length);
        return m;
    }

    /// <summary>Anchor rod embedded below the masonry plate with a plate washer and nut on top and a nut at the bottom.</summary>
    public static Mesh AnchorBoltMesh(double diameter, double plateThickness, double embedment, int segments = 12)
    {
        var m = new Mesh();
        var f = Frame3.World;
        var g = new BoltGeometry(diameter);
        double bottom = -plateThickness - embedment;
        double washerT = Units.UnitSystem.In(0.375), washer = 3 * diameter;
        double top = washerT + g.NutHeight + g.StickThrough;
        MeshBuilder.AddPrism(m, f, diameter / 2, segments, bottom, top);
        MeshBuilder.AddBox(m, f, -washer / 2, washer / 2, -washer / 2, washer / 2, 0, washerT);
        MeshBuilder.AddPrism(m, f, g.NutAcrossFlats / Math.Sqrt(3), 6, washerT, washerT + g.NutHeight);
        MeshBuilder.AddPrism(m, f, g.NutAcrossFlats / Math.Sqrt(3), 6, bottom, bottom + g.NutHeight);
        return m;
    }

    /// <summary>Builds the bolt solid (head, shank, washer, nut) in the bolt's local frame.</summary>
    public static Mesh BoltMesh(BoltGeometry g, double grip, double length, int segments = 12)
    {
        var m = new Mesh();
        var f = Frame3.World;
        double hexR = g.HeadAcrossFlats / Math.Sqrt(3);
        MeshBuilder.AddPrism(m, f, hexR, 6, -g.HeadHeight, 0);
        MeshBuilder.AddPrism(m, f, g.Diameter / 2, segments, 0, length);
        MeshBuilder.AddPrism(m, f, g.WasherOd / 2, segments, grip, grip + g.WasherThickness);
        MeshBuilder.AddPrism(m, f, g.NutAcrossFlats / Math.Sqrt(3), 6, grip + g.WasherThickness, grip + g.WasherThickness + g.NutHeight);
        return m;
    }
}
