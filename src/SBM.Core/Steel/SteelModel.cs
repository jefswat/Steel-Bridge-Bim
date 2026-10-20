using SBM.Core.Geometry;
using SBM.Core.Framing;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

public enum PartCategory
{
    FlangePlate, WebPlate, BearingStiffener, ConnectionStiffener, IntermediateStiffener,
    Gusset, FillerPlate, Angle, Channel, SolePlate, BearingPad, Weld,
    SplicePlate, BearingPlate, MasonryPlate, BearingDisc, ShearPin, GuideBar, Ptfe,
    // Inspection access (cables on WT brackets, walkways).
    InspectionBracket, InspectionCable, Grating, WalkwayStringer, WalkwayPost, WalkwayRail, WalkwayPlate,
    // Tied arch (Arch/ArchGenerator): rib, tie girder plates, floorbeam plates, hanger strand / sockets, upper bracing, knuckle plates, form deck.
    ArchRib, TiePlate, FloorbeamPlate, Hanger, HangerSocket, ArchBracing, KnucklePlate, FormDeck,
    // Tied arch v2: cable fin plates, UHMW-PE cable connectors, connection plates (floorbeam / bracing), stiffeners.
    FinPlate, CableConnector, ArchConnectionPlate, ArchStiffener,
    // Longitudinal PT of the arch deck: strands (IfcTendon) and anchorages (IfcTendonAnchor).
    PtStrand, PtAnchorage,
    // Tub girders (appended; OPTIONAL feature): rolled tees (top struts, top lateral bracing), bottom flange access doors.
    RolledTee, AccessDoor,
    // Pedestrian railings (appended; OPTIONAL BridgeDefinition.Railings): posts, handrails (IfcRailing HANDRAIL), other rails / fence members, post base
    // plates, and the placeholder envelope of a railing whose details are not in the plan set. Not structural steel (excluded from SteelMass).
    RailPost, RailHandrail, RailMember, RailBasePlate, RailEnvelope,
    // Integral steel pier cap (appended; OPTIONAL SupportDefinition.IntegralSteelCap): girder end plates, connector plates inside the cap, girder connection
    // diaphragm plates, mill-to-bear continuity plates, cover plates, handhole cover / backing plates, wire mesh end screens.
    EndPlate, ConnectorPlate, DiaphragmPlate, ContinuityPlate, CoverPlate, HandholeCover, HandholeBacking, MeshScreen,
    // Expansion joint hardware (appended; OPTIONAL ExpansionJointDefinition.Armor / CoverPlate): armor plates with seal retainer bars, armor stud anchors,
    // walkway cover plates, cover plate anchors. Paid by the joint length / as misc steel: excluded from SteelMass.
    JointArmor, JointStud, JointCoverPlate, JointAnchor,
    // Pedestrian railing fence fabric (appended; OPTIONAL PedestrianRailingDefinition.Fabric): one thin panel per bay, drawn see-through.
    RailFabric,
    // Steel truss (appended; OPTIONAL BridgeDefinition.Truss, Truss/TrussGenerator): built-up / rolled members by role, gusset plates, floor beams,
    // stringers, rope suspenders + sockets, shoes (fixed pin / expansion rocker bodies) and their pins.
    TrussChord, TrussDiagonal, TrussVertical, TrussHanger, TrussBracing, TrussFloorBeam, TrussStringer, TrussGusset,
    Suspender, SuspenderSocket, TrussShoeFixed, TrussShoeExpansion, TrussPin,
}

public enum AssemblyKind { GirderFieldSection, CrossFrame, Bearing, FieldSplice, LateralBracing, InspectionCable, InspectionWalkway,
    ArchRib, ArchTie, ArchHanger, ArchFloorbeam, ArchBracing, ArchKnuckle, ArchDeckForm, ArchSplice, Railing,
    /// <summary>Integral steel pier cap member (appended) and the girder-to-cap connection inside / over it.</summary>
    PierCap, PierCapConnection,
    /// <summary>Expansion joint hardware (appended).</summary>
    ExpansionJoint,
    /// <summary>Steel truss (appended): one truss line (plane: chords, web members, gussets), a bracing system, the floor system, one suspender.</summary>
    TrussLine, TrussBracing, TrussFloor, TrussSuspender }

/// <summary>Member role, used for IFC predefined types and reports.</summary>
public enum MemberRole { None, TopChord, BottomChord, Diagonal, Diaphragm, Lateral }

/// <summary>Rivet (appended): hot-driven rivet with button heads; <see cref="BoltInstance.Grip"/> = grip, Length = grip, Diameter = rivet diameter.</summary>
public enum BoltKind { Structural, Anchor, Rivet }

public sealed class SteelAssembly
{
    public required string Mark { get; init; }
    public required AssemblyKind Kind { get; init; }
    public string Description { get; init; } = "";
    /// <summary>Girder index (girder field sections, bearings) or −1.</summary>
    public int Girder { get; init; } = -1;
    /// <summary>Railing assemblies: IFC predefined type of the IfcRailing (GUARDRAIL, HANDRAIL, FENCE, BALUSTRADE) and its object type (railing type name).</summary>
    public string? PredefinedType { get; init; }
    public string? ObjectType { get; init; }
}

public sealed class SteelPart
{
    public required string Mark { get; init; }
    public required PartCategory Category { get; init; }
    public required string Assembly { get; init; }
    /// <summary>Size description, e.g. PL 16" x 1" or L4x4x3/8.</summary>
    public required string Description { get; init; }
    public required Mesh Mesh { get; init; }
    public string Material { get; set; } = "ASTM A709 Gr. 50";
    public MemberRole Role { get; init; }
    /// <summary>Overall length (plates: along the girder or longest dimension; angles: cut length).</summary>
    public double Length { get; init; }
    public double Volume => Math.Abs(Mesh.Volume());
    /// <summary>Volume of the exact NURBS solids (exact-geometry option), null when the part has none. Opt-in quantity; <see cref="Mass"/>
    /// stays on the mesh.</summary>
    public double? ExactVolume => Mesh.ExactVolume();
    /// <summary>Rolled shapes: AISC nominal weight (lb/ft, all pieces of the member) — mass = weight x length, as on a steel list.</summary>
    public double NominalWeightLbPerFt { get; init; }
    /// <summary>Geometry the plans do not fix (OPTIONAL): IFC Pset SBM_Status, viewport UNRESOLVED layer.</summary>
    public Assumption? Assumption { get; init; }
    /// <summary>Material density (kg/m3) when not structural steel (e.g. a stainless railing); 0 = by category.</summary>
    public double Density { get; init; }
    /// <summary>OPTIONAL fabrication attributes from the plan notes (toughness, contact faces, NDT, coating, shop assembly, tolerances); null = none.
    /// IFC Pset SBM_FabricationAttributes.</summary>
    public Bridge.FabricationAttributes? Fabrication { get; set; }
    public double Mass => NominalWeightLbPerFt > 0 ? NominalWeightLbPerFt * 1.48816394 * Length : Category switch
    {
        PartCategory.BearingPad or PartCategory.BearingDisc => Volume * 1150,
        PartCategory.Ptfe => Volume * 2200,
        PartCategory.CableConnector => Volume * 940,
        _ when Density > 0 => Volume * Density,
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
    /// <summary>Anchor bolts, OPTIONAL: height of the bolt top above the frame origin (0 = the standard washer + nut + stick-through stack). Tub bearings set it
    /// from the plan's projection above the bottom flange (p.292); other bridges leave it 0 (their solids and IFC are unchanged).</summary>
    public double TopHeight { get; init; }
    /// <summary>OPTIONAL material of this bolt when it is not the model's bolt grade (e.g. stainless screen bolts); null = the model's.</summary>
    public string? Material { get; init; }
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
    string Type = "Elastomeric", string Fixity = "", Vec2? Location = null, Vec2? Axis = null, double FootprintLength = 0, double FootprintWidth = 0,
    string Label = "");

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
    /// <summary>Bolt diameter actually used by this frame (style or bridge) and the style name ("" = automatic layout).</summary>
    public double BoltDiameter { get; init; }
    public string StyleName { get; init; } = "";
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
    /// <summary>Exact-geometry option: what was built as NURBS (null when the option is off).</summary>
    public NurbsReport? Nurbs { get; set; }
    /// <summary>Tub girder bridges: what the tub generator built and measured (null for I-girders).</summary>
    public TubGirderReport? Tub { get; set; }
    /// <summary>Integral steel pier caps built into this model (OPTIONAL <c>SupportDefinition.IntegralSteelCap</c>); empty = none.</summary>
    public List<IntegralCapReport> IntegralCaps { get; } = new();
    /// <summary>Tied arch results when this steel model was generated by <c>Arch.ArchGenerator</c> (null otherwise).</summary>
    public SBM.Core.Arch.ArchModel? Arch { get; set; }
    /// <summary>Truss results when this steel model was generated by <c>Truss.TrussGenerator</c> (null otherwise).</summary>
    public SBM.Core.Truss.TrussModel? Truss { get; set; }

    public double SteelMass => Parts.Where(p => p.Category is not (PartCategory.BearingPad or PartCategory.Weld or PartCategory.BearingDisc or PartCategory.Ptfe or PartCategory.CableConnector or PartCategory.PtStrand or PartCategory.PtAnchorage) && !IsRailing(p.Category) && !IsJointHardware(p.Category)).Sum(p => p.Mass);
    /// <summary>Pedestrian railing parts (not structural steel).</summary>
    public static bool IsRailing(PartCategory c) => c is PartCategory.RailPost or PartCategory.RailHandrail or PartCategory.RailMember or PartCategory.RailBasePlate or PartCategory.RailEnvelope or PartCategory.RailFabric;
    /// <summary>Expansion joint hardware parts (armor, studs, cover plates, anchors; not structural steel).</summary>
    public static bool IsJointHardware(PartCategory c) => c is PartCategory.JointArmor or PartCategory.JointStud or PartCategory.JointCoverPlate or PartCategory.JointAnchor;
    /// <summary>Pedestrian railing runs built into this model (<c>BridgeDefinition.Railings</c>; null = none).</summary>
    public SBM.Core.Railing.RailingModel? Railings { get; set; }

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
    public static Mesh AnchorBoltMesh(double diameter, double plateThickness, double embedment, int segments = 12, double topHeight = 0)
    {
        var m = new Mesh();
        var f = Frame3.World;
        var g = new BoltGeometry(diameter);
        double bottom = -plateThickness - embedment;
        double washerT = Units.UnitSystem.In(0.375), washer = 3 * diameter;
        double top = topHeight > 0 ? topHeight : washerT + g.NutHeight + g.StickThrough;
        MeshBuilder.AddPrism(m, f, diameter / 2, segments, bottom, top);
        MeshBuilder.AddBox(m, f, -washer / 2, washer / 2, -washer / 2, washer / 2, 0, washerT);
        MeshBuilder.AddPrism(m, f, g.NutAcrossFlats / Math.Sqrt(3), 6, washerT, washerT + g.NutHeight);
        MeshBuilder.AddPrism(m, f, g.NutAcrossFlats / Math.Sqrt(3), 6, bottom, bottom + g.NutHeight);
        return m;
    }

    /// <summary>Rivet solid in the bolt frame (origin = face under the factory head, +Z through the grip): shank + two button heads
    /// (head diameter 1.6 d, height 0.6 d, as octagonal prisms - the heads' rounded shape is not modeled).</summary>
    public static Mesh RivetMesh(double diameter, double grip, int segments = 8)
    {
        var m = new Mesh();
        var f = Frame3.World;
        double hr = 0.8 * diameter / Math.Cos(Math.PI / segments), hh = 0.6 * diameter;
        MeshBuilder.AddPrism(m, f, hr, segments, -hh, 0);
        MeshBuilder.AddPrism(m, f, diameter / 2 / Math.Cos(Math.PI / segments), segments, 0, grip);
        MeshBuilder.AddPrism(m, f, hr, segments, grip, grip + hh);
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
