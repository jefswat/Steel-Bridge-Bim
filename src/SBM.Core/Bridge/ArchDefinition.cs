using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>
/// Steel TIED ARCH superstructure (OPTIONAL, <see cref="BridgeDefinition.Arch"/>; null = none, omitted from the .sbm). The bridge that carries it
/// has no girders (<c>Girders.Count = 0</c>); its supports are the arch piers (shared with the approach units through <c>SharedPier</c>, the arch
/// bridge never builds substructure). Every member is given in the LOCAL structure coordinate system of the plans (meters internally):
/// origin, X along the chord, Y left, Z normal to the tilted local XY plane (<see cref="ArchFrameDefinition"/>). Generator:
/// <c>SBM.Core.Arch.ArchGenerator</c>. Design note: docs/plan-data/nht-0047-07-228/ARCH-DESIGN.md.
/// </summary>
public sealed class ArchDefinition
{
    /// <summary>Plan reference printed with every flag (e.g. "TxDOT CSJ 0047-07-228").</summary>
    public string Source { get; set; } = "";
    public ArchFrameDefinition Frame { get; set; } = new();
    /// <summary>Named local points (work points, panel points, bracing points) members refer to.</summary>
    public List<ArchPoint> Points { get; set; } = new();
    public List<ArchRibDefinition> Ribs { get; set; } = new();
    public List<ArchTieDefinition> Ties { get; set; } = new();
    public List<ArchHangerDefinition> Hangers { get; set; } = new();
    public ArchHangerSystem HangerSystem { get; set; } = new();
    /// <summary>OPTIONAL weld rules (plan fillet weld symbols on full-contact T-joints), applied after every member is built.</summary>
    public List<ArchWeldRule> Welds { get; set; } = new();
    public List<ArchFloorbeamDefinition> Floorbeams { get; set; } = new();
    public List<ArchBraceDefinition> Bracing { get; set; } = new();
    /// <summary>Crossing splices of the bracing (OPTIONAL).</summary>
    public List<ArchBraceCrossing> BracingCrossings { get; set; } = new();
    /// <summary>AISC section of the bracing members (exact name, e.g. "W8x28").</summary>
    public string BracingSection { get; set; } = "W8x28";
    public string BracingMaterial { get; set; } = "ASTM A709 Gr. 50";
    /// <summary>Gusset connections of the bracing at the rib panel points (OPTIONAL, p.129-140): members end at the drawn brace ends.</summary>
    public List<ArchBracingConnection> BracingConnections { get; set; } = new();
    /// <summary>Plan item the bracing web orientation is not given by (UNRESOLVED text; empty = given).</summary>
    public string BracingOrientationUnresolved { get; set; } = "";
    public List<ArchKnuckleDefinition> Knuckles { get; set; } = new();
    /// <summary>Fin plate geometry (OPTIONAL; null = no fin plates even when hangers carry fin data).</summary>
    public ArchFinSettings? Fins { get; set; }
    /// <summary>Cable connectors at hanger crossings (pairs of hanger names) and their detail (OPTIONAL).</summary>
    public List<ArchCableConnector> CableConnectors { get; set; } = new();
    public ArchCableConnectorDetail? CableConnectorDetail { get; set; }
    /// <summary>Floorbeam-to-tie connection detail at both floorbeam ends (OPTIONAL, null = none).</summary>
    public ArchFloorbeamConnection? FloorbeamConnection { get; set; }
    public ArchDeckDefinition? Deck { get; set; }
    public List<ArchBearingDefinition> Bearings { get; set; } = new();
    /// <summary>Plan items that are open (each an UNRESOLVED warning, text built with <c>UnresolvedFlag.Text</c>).</summary>
    public List<string> Unresolved { get; set; } = new();

    public ArchPoint? Point(string name) => Points.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public ArchDefinition Clone() => System.Text.Json.JsonSerializer.Deserialize<ArchDefinition>(System.Text.Json.JsonSerializer.Serialize(this, CloneJson), CloneJson)!;

    private static readonly System.Text.Json.JsonSerializerOptions CloneJson = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}

/// <summary>A named point in the local structure coordinate system (meters).</summary>
public sealed class ArchPoint
{
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public ArchPoint() { }
    public ArchPoint(string name, double x, double y, double z) { Name = name; X = x; Y = y; Z = z; }
    [System.Text.Json.Serialization.JsonIgnore] public Geometry.Vec3 Vec => new(X, Y, Z);
}

/// <summary>
/// Local structure frame (plans: "local XY plane = global horizontal plane rotated DOWN about a line parallel to the pier CL; X parallel to the
/// chord CL1-CL2"). Two plan work points on the alignment (station, offset right +) with their local X give the plan X direction and the origin's
/// plan point (at local X = 0 on the WP1-WP2 line); <see cref="OriginElevation"/> is the global elevation of the origin; the local XY plane is
/// the horizontal plane rotated by <see cref="TiltDegrees"/> about a horizontal axis along the support line <see cref="TiltAxisSupport"/>,
/// descending toward +X. Plan-length and WP-elevation differences are reported, never adjusted.
/// </summary>
public sealed class ArchFrameDefinition
{
    public double Wp1Station { get; set; }
    public double Wp1Offset { get; set; }
    public double Wp1LocalX { get; set; } = -Ft(100.5);
    public double Wp2Station { get; set; }
    public double Wp2Offset { get; set; }
    public double Wp2LocalX { get; set; } = Ft(100.5);
    public double OriginElevation { get; set; }
    public double TiltDegrees { get; set; }
    /// <summary>Support (name) whose line is the tilt axis direction in plan.</summary>
    public string TiltAxisSupport { get; set; } = "";
    /// <summary>Printed WP elevations for the report (NaN = no check).</summary>
    public double CheckWp1Elevation { get; set; } = double.NaN;
    public double CheckWp2Elevation { get; set; } = double.NaN;
}

/// <summary>Circular rib in a plane inclined about its chord. The chord runs from <see cref="ChordStart"/> to <see cref="ChordEnd"/> (local points
/// on the tie WP plane); the rib plane is the vertical plane through the chord rotated by <see cref="InclinationDegrees"/> about the chord so the
/// crown moves toward +Y (positive) or -Y (negative). Rolled section bent about its weak axis: web normal to the arch plane, flange width in it.</summary>
public sealed class ArchRibDefinition
{
    public string Name { get; set; } = "";
    public string ChordStart { get; set; } = "";
    public string ChordEnd { get; set; } = "";
    public double Radius { get; set; } = Ft(126);
    /// <summary>Plan rise (check only; the circle is fixed by the chord and the radius).</summary>
    public double Rise { get; set; } = double.NaN;
    public double InclinationDegrees { get; set; }
    public string Section { get; set; } = "W14x132";
    public string Material { get; set; } = "ASTM A709 Gr. 50-T1";
    /// <summary>Field splice points (names of local points on the rib), in order from ChordStart. Rib pieces run between them.</summary>
    public List<string> Splices { get; set; } = new();
    /// <summary>Gap at each splice.</summary>
    public double SpliceGap { get; set; } = In(0.5);
    /// <summary>Lowest point of the rib (local Z of the knuckle where the stub ends below the first / last splice; NaN = the chord, Z = 0).</summary>
    public double StubBottomZ { get; set; } = double.NaN;
    /// <summary>The knuckle stub below the end splices is a built-up plate assembly on the plans; text = UNRESOLVED reason (empty = not flagged).</summary>
    public string StubUnresolved { get; set; } = "";
    /// <summary>Bolted field splice detail at every splice point (OPTIONAL, null = pieces only, no plates / bolts).</summary>
    public ArchSpliceDefinition? SpliceDetail { get; set; }
}

/// <summary>One bolt row of a splice pattern: gage from the reference plate edge, the first column it uses and the column step (2 = staggered).</summary>
public sealed class ArchBoltRow
{
    public double Gage { get; set; }
    public int FirstColumn { get; set; }
    public int ColumnStep { get; set; } = 1;
    public ArchBoltRow() { }
    public ArchBoltRow(double gage, int first = 0, int step = 1) { Gage = gage; FirstColumn = first; ColumnStep = step; }
}

/// <summary>
/// Bolted field splice of an arch member (rib W shape bent about its weak axis, or tie T), symmetric about the joint: per flange an outside
/// plate (centred on the web) and two inside plates (one each side of the web, their outer edge flush with the outside plate edge); two web plates
/// (both faces). Bolt columns from the joint CL (first, pitch, count per side); rows by gage: flange rows from the OUTSIDE plate edge (mirrored to
/// both sides of the web), web rows from the web plate edge on the + side (rib: +binormal, tie: top). Plate lengths = 2 x (first + (count - 1) pitch + end edge).
/// Bolts: the model bolt (7/8" A325), holes = bolt + 1/16". Corner clips 45 deg (0 = square).
/// </summary>
public sealed class ArchSpliceDefinition
{
    public string Name { get; set; } = "";
    public double FlangeOuterWidth { get; set; }
    public double FlangeOuterThickness { get; set; }
    public double FlangeInnerWidth { get; set; }
    public double FlangeInnerThickness { get; set; }
    public double FlangeBoltFirst { get; set; }
    public double FlangeBoltPitch { get; set; }
    public int FlangeBoltColumns { get; set; }
    public double FlangeEndEdge { get; set; }
    public List<ArchBoltRow> FlangeRows { get; set; } = new();
    public double FlangeClip { get; set; }
    public double WebPlateWidth { get; set; }
    public double WebPlateThickness { get; set; }
    /// <summary>Web plate position: distance of its + edge below the web's + end (tie: top of web; NaN = centred on the web depth).</summary>
    public double WebPlateEdgeFromWebEnd { get; set; } = double.NaN;
    public double WebBoltFirst { get; set; }
    public double WebBoltPitch { get; set; }
    public int WebBoltColumns { get; set; }
    public double WebEndEdge { get; set; }
    public List<ArchBoltRow> WebRows { get; set; } = new();
    public double WebClip { get; set; }
    /// <summary>Web plates bent to the member curvature (rib: "CURVED", tie: "BENT").</summary>
    public string WebPlateNote { get; set; } = "";
    public string Material { get; set; } = "ASTM A709 Gr. 50";
    /// <summary>Plan length of the flange / web plates (check; NaN = none).</summary>
    public double CheckFlangePlateLength { get; set; } = double.NaN;
    public double CheckWebPlateLength { get; set; } = double.NaN;
    [System.Text.Json.Serialization.JsonIgnore] public double FlangePlateLength => 2 * (FlangeBoltFirst + (FlangeBoltColumns - 1) * FlangeBoltPitch + FlangeEndEdge);
    [System.Text.Json.Serialization.JsonIgnore] public double WebPlateLength => 2 * (WebBoltFirst + (WebBoltColumns - 1) * WebBoltPitch + WebEndEdge);
}

public enum ArchTieElementKind { Line, ArcLeft, ArcRight }

public sealed class ArchTieElement
{
    public ArchTieElementKind Kind { get; set; }
    public double Length { get; set; }
    public double Radius { get; set; }
}

/// <summary>Tie girder: built-up T (web on the flange, WP = bottom of the flange on the web CL) along its own plan chain in the local XY plane.
/// The start direction is solved so the chain closes on <see cref="End"/> (miss reported). Panel points are distances along the chain.</summary>
public sealed class ArchTieDefinition
{
    public string Name { get; set; } = "";
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public List<ArchTieElement> Elements { get; set; } = new();
    public List<ArchPanelPoint> PanelPoints { get; set; } = new();
    public double WebThickness { get; set; } = In(0.875);
    /// <summary>Web height above the flange.</summary>
    public double WebHeight { get; set; } = In(11);
    public double FlangeThickness { get; set; } = In(0.875);
    public double FlangeWidth { get; set; } = In(14);
    public string Material { get; set; } = "ASTM A709 Gr. 50-F1 (FCM)";
    /// <summary>Field splices (data names, v1; kept for old files).</summary>
    public List<string> Splices { get; set; } = new();
    /// <summary>Field splice positions (name, distance along the chain): the tie is cut into pieces there with <see cref="SpliceGap"/>; with
    /// <see cref="SpliceDetail"/> the splice plates and bolts are built.</summary>
    public List<ArchPanelPoint> SpliceAt { get; set; } = new();
    public double SpliceGap { get; set; } = In(0.5);
    public ArchSpliceDefinition? SpliceDetail { get; set; }
    /// <summary>UNRESOLVED reason for the splice positions (empty = plan dimensioned): flags the splice parts.</summary>
    public string SpliceUnresolved { get; set; } = "";
}

public sealed class ArchPanelPoint
{
    public string Name { get; set; } = "";
    public double Distance { get; set; }
    public ArchPanelPoint() { }
    public ArchPanelPoint(string name, double distance) { Name = name; Distance = distance; }
}

/// <summary>One hanger: straight strand between the pin centres of its two sockets (tie anchor TC, rib anchor HC).</summary>
public sealed class ArchHangerDefinition
{
    public string Name { get; set; } = "";
    public string Rib { get; set; } = "";
    /// <summary>Tie girder the lower socket is on (fin plate plane = cable + tie tangent).</summary>
    public string Tie { get; set; } = "";
    public ArchPoint TieAnchor { get; set; } = new();
    public ArchPoint RibAnchor { get; set; } = new();
    /// <summary>Plan length (check; NaN = none): pin to pin, or tie pin to <see cref="RibWorkPoint"/> when that is given.</summary>
    public double PlanLength { get; set; } = double.NaN;
    /// <summary>Cable axis point on the rib working line (OPTIONAL; p.147 lists it beside the rib pin; data + length check).</summary>
    public ArchPoint? RibWorkPoint { get; set; }
    /// <summary>Cable fin plate at the rib (OPTIONAL, plan table per cable; geometry settings in <see cref="ArchHangerSystem.Fins"/>).</summary>
    public ArchRibFin? RibFin { get; set; }
    /// <summary>Cable fin plate at the tie (OPTIONAL).</summary>
    public ArchTieFin? TieFin { get; set; }
}

/// <summary>Rib fin plate data of one cable (plan "cable fin plate at arch rib dimensions"): angle B (deg, check), P1 pin offset from the arch
/// plane (check), P2 pin to the rib CL along the fin (check), P3 straight side length, P4 rib CL to the plate top, weld length D (data).</summary>
public sealed class ArchRibFin
{
    public double AngleB { get; set; } = double.NaN;
    public double P1 { get; set; } = double.NaN;
    public double P2 { get; set; } = double.NaN;
    public double P3 { get; set; }
    public double P4 { get; set; }
    public double WeldLength { get; set; } = double.NaN;
}

/// <summary>Tie fin plate data of one cable (plan "cable fin plate at tie girder data"): panel point the plate is centred on, bend angle A (deg,
/// check), orientation LEFT / RIGHT (check).</summary>
public sealed class ArchTieFin
{
    public string Panel { get; set; } = "";
    public double AngleA { get; set; } = double.NaN;
    public string Orient { get; set; } = "";
}

/// <summary>Fin plate geometry common to all cables. Rib fin: plate between the rib flanges (inner faces), top P4 below the rib CL, straight
/// sides P3, then tangent to a radius around the pin; corner clips at the top. Tie fin: bent plate bolted to the outside face of the tie web,
/// lower part centred on the floorbeam CL (width, height below the bend line, bolt rows / columns), bend line above the top of the tie web,
/// upper part turned about the bend line into the cable plane, pin radius; the pin is the cable anchor point (TC / HC of the hanger).</summary>
public sealed class ArchFinSettings
{
    public double RibThickness { get; set; } = In(0.875);
    public double RibTopClip { get; set; } = In(1);
    public double RibPinRadius { get; set; } = In(2.25);
    /// <summary>UNRESOLVED reason for the rib fin plates (thickness / clip not dimensioned); empty = not flagged.</summary>
    public string RibUnresolved { get; set; } = "";
    public double PinHole { get; set; } = In(1.378);
    public double TieThickness { get; set; } = In(0.875);
    public double TieWidth { get; set; } = In(8);
    public double TiePinFromEdge { get; set; } = In(1.875);
    public double TieDepthBelowBend { get; set; } = In(7);
    public double TieBendAboveWeb { get; set; } = In(0.5);
    /// <summary>Plan pin distance above the bend line in the plate plane (check).</summary>
    public double TiePinAboveBend { get; set; } = In(4);
    /// <summary>Far edge rises this far above the bend line before the slope to the pin radius.</summary>
    public double TieFarEdgeAboveBend { get; set; } = In(0.5);
    public double TiePinRadius { get; set; } = In(2.25);
    public double TieHoleEdge { get; set; } = In(1.5);
    /// <summary>Bolt rows below the bend line.</summary>
    public List<double> TieHoleRows { get; set; } = new() { In(2), In(5.5) };
    public string Material { get; set; } = "ASTM A709 Gr. 50-F1 (FCM)";
    /// <summary>OPTIONAL rib fin plate welds (p.148): fillet leg (0 = welds not modeled), both faces of the plate along each flange inner face, only where
    /// the plate edge sits on the flange (contact found on the meshes): from the hold-back below the top of the contact (end of the top clip) plus
    /// <see cref="RibWeldSetback"/>, length = the table "weld length D" per cable, never past the hold-back above the end of the contact (flange tip /
    /// end of the straight side); a D longer than that is a CHECK row (plan vs model).</summary>
    public double RibWeldSize { get; set; }
    public double RibWeldSetback { get; set; }
    /// <summary>OPTIONAL hold-back of the rib fin welds from both ends of the flange contact (p.148 Section A-A: 1/4" +- 1/8" at each end of D); NaN = the
    /// bridge's <c>BridgeDefinition.Welds</c> / AWS default.</summary>
    public double RibWeldHoldBack { get; set; } = double.NaN;
    public string RibWeldHoldBackSource { get; set; } = "";
    /// <summary>UNRESOLVED reason for the weld position (page + why); empty = not flagged.</summary>
    public string RibWeldUnresolved { get; set; } = "";
    public string WeldMaterial { get; set; } = "E70XX (AWS D1.5)";
}

/// <summary>
/// OPTIONAL fillet weld rule (plan weld symbol on a T-joint whose extent is the full contact, no length dimensioned): every part whose mark matches
/// <see cref="Parts"/> (the attached plate: its edge face is welded) gets a fillet of <see cref="Size"/> along each line where its edge face sits on a
/// part matching <see cref="Onto"/> (<c>Steel.WeldFinder.TJoint</c>; both sides of the attached plate unless <see cref="OneSide"/>). Built as weld parts
/// (PartCategory.Weld). <see cref="Unresolved"/> (page + why) flags the welds as a dimension only (the weld exists on the plans).
/// </summary>
public sealed class ArchWeldRule
{
    public string Name { get; set; } = "";
    /// <summary>Regular expression on the attached part marks.</summary>
    public string Parts { get; set; } = "";
    /// <summary>Regular expression on the marks of the parts it is welded to; "$1" ... = the capture groups of <see cref="Parts"/> on the attached part's mark.</summary>
    public string Onto { get; set; } = "";
    public double Size { get; set; }
    public bool OneSide { get; set; }
    /// <summary>For a single-sided symbol (<see cref="OneSide"/>) whose arrow side the plan does not identify: "span" = the side of the attached plate facing
    /// the structure centre line, "+x" / "-x" = facing along the local X axis; empty = both faces are tried (only useful with a plate that has one face on it).
    /// Pair it with <see cref="Unresolved"/> so the choice is flagged.</summary>
    public string SideToward { get; set; } = "";
    /// <summary>Largest angle (degrees) between the attached plate's edge face and the face it sits on.</summary>
    public double AngleTolDeg { get; set; } = 3;
    /// <summary>Contact distance tolerance (gap between the edge face and the face it is welded to).</summary>
    public double Tolerance { get; set; } = In(1.0 / 32);
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    public string Material { get; set; } = "E70XX (AWS D1.5)";
    /// <summary>OPTIONAL hold-back of this rule's beads from plate corners, clips and the end of contact (<c>Steel.WeldTermination</c>); NaN = the bridge's
    /// <c>BridgeDefinition.Welds</c> value or the AWS default (weld size, min 1/4", flagged UNRESOLVED).</summary>
    public double HoldBack { get; set; } = double.NaN;
    public string HoldBackSource { get; set; } = "";
}

/// <summary>Cable connector at a hanger crossing (plan p.150): UHMW-PE pendant plate between the two cables with a sleeve bracket over each.</summary>
public sealed class ArchCableConnector
{
    public string Name { get; set; } = "";
    public string CableA { get; set; } = "";
    public string CableB { get; set; } = "";
}

/// <summary>Cable connector detail: pendant plate (hexagon length x straight side, thickness), sleeve brackets (length along the cable, outer radius,
/// wall / tab thickness, tab width each side).</summary>
public sealed class ArchCableConnectorDetail
{
    public double PendantLength { get; set; } = In(3.125);
    public double PendantSide { get; set; } = In(1.5);
    public double PendantThickness { get; set; } = In(0.1875);
    public double PendantEdgeAngleDeg { get; set; } = 23.25;
    public double BracketLength { get; set; } = In(2);
    public double BracketOuterRadius { get; set; } = In(11.0 / 16);
    public double BracketThickness { get; set; } = In(0.25);
    public double BracketTab { get; set; } = In(0.375);
    public string Material { get; set; } = "UHMW-PE (ultra high molecular weight polyethylene)";
    public double DensityKgM3 { get; set; } = 940;
    public string Basis { get; set; } = "";
}

/// <summary>Floorbeam-to-tie connection (plan p.146): a stiffener in the floorbeam web plane welded to the tie web and flange, two web
/// connection plates (stiffener to floorbeam web) and flange connection plates (tie flange to floorbeam flange) on top (and below), bolted.</summary>
public sealed class ArchFloorbeamConnection
{
    public double StiffenerThickness { get; set; } = In(1);
    /// <summary>Full-height flat at the tie web before the slope.</summary>
    public double StiffenerTopFlat { get; set; } = In(1);
    /// <summary>Height above the tie flange at the flange edge.</summary>
    public double StiffenerOuterHeight { get; set; } = In(8);
    public double StiffenerClip { get; set; } = In(1);
    public double PlateThickness { get; set; } = In(0.625);
    public double PlateLength { get; set; } = In(12.5);
    public double WebPlateHeight { get; set; } = In(7);
    public double BoltFirst { get; set; } = In(1.625);
    public double BoltPitch { get; set; } = In(1.625);
    public int BoltColumns { get; set; } = 3;
    /// <summary>Web plate rows: gage from the plate top.</summary>
    public List<ArchBoltRow> WebRows { get; set; } = new();
    public double FlangePlateWidth { get; set; } = In(4.9375);
    /// <summary>Flange plate rows: gage from the floorbeam flange edge.</summary>
    public List<ArchBoltRow> FlangeRows { get; set; } = new();
    public bool BottomFlangePlates { get; set; } = true;
    public string BottomUnresolved { get; set; } = "";
    public string Material { get; set; } = "ASTM A709 Gr. 50-T1";
}

/// <summary>Strand and sockets of all hangers (plan p.148 tables + catalog data).</summary>
public sealed class ArchHangerSystem
{
    public double StrandDiameter { get; set; } = In(1.0);
    /// <summary>Nominal weight (lb/ft; 0 = from the solid).</summary>
    public double StrandWeightLbPerFt { get; set; }
    public string StrandDescription { get; set; } = "";
    public string StrandMaterial { get; set; } = "ASTM A586 zinc-coated structural strand";
    /// <summary>Upper (rib) and lower (tie) sockets; null = not modeled.</summary>
    public ArchSocketDefinition? RibSocket { get; set; }
    public ArchSocketDefinition? TieSocket { get; set; }
}

/// <summary>Open strand socket envelope, plan letters (p.148): A head width (in the fin plane), B jaw width across the pin, G pin with heads,
/// L pin centre to end of the jaw body, W pin centre to the end of the fitting on the cable (incl. the turnbuckle for the adjustable socket),
/// dL pin diameter, Db shank diameter.</summary>
public sealed class ArchSocketDefinition
{
    public string Name { get; set; } = "";
    public double A { get; set; }
    public double B { get; set; }
    public double G { get; set; }
    public double L { get; set; }
    public double W { get; set; }
    public double PinDiameter { get; set; }
    public double ShankDiameter { get; set; }
    /// <summary>OPTIONAL open-jaw detail (all 0 = the v1-v4 solid jaw block): E = pin centre to the bottom of the jaw slot (toward the cable), F = pin
    /// centre to the back end of the jaws, SlotWidth = clear opening between the two jaws (the fin plate sits in it, the pin crosses both jaws and the
    /// plate). With it the socket is built as two jaws straddling the fin plate (pin normal to the plate), the body to L and the shank to W.</summary>
    public double E { get; set; }
    public double F { get; set; }
    public double SlotWidth { get; set; }
    /// <summary>UNRESOLVED reason (page + why) for a jaw dimension the plans do not give; the socket keeps its layer, flagged as a dimension.</summary>
    public string Unresolved { get; set; } = "";
    public string Material { get; set; } = "Socket (forged / cast steel, by supplier)";
    /// <summary>Envelope basis note (pset / report).</summary>
    public string Basis { get; set; } = "";
}

/// <summary>Floorbeam between two tie panel points (built-up T, flange coplanar with the tie flanges, web crowned).</summary>
public sealed class ArchFloorbeamDefinition
{
    public string Name { get; set; } = "";
    public string TieA { get; set; } = "";
    public string PanelA { get; set; } = "";
    public string TieB { get; set; } = "";
    public string PanelB { get; set; } = "";
    public double WebThickness { get; set; } = In(1);
    public double FlangeThickness { get; set; } = In(0.875);
    public double FlangeWidth { get; set; } = In(12);
    /// <summary>Overall depth (bottom of flange to top of web) at the web ends and at the crown.</summary>
    public double DepthAtEnds { get; set; } = In(8.875);
    public double DepthAtCrown { get; set; } = In(9.875);
    /// <summary>Crown position: true = where the floorbeam crosses the alignment (deck crown at CL NHT), false = mid length.</summary>
    public bool CrownAtAlignment { get; set; } = true;
    /// <summary>Clear gap between the floorbeam end and the tie flange edge (along the floorbeam).</summary>
    public double EndGap { get; set; } = In(0.5);
    public string Material { get; set; } = "ASTM A709 Gr. 50-T1";
    /// <summary>PT openings: distance from the CL of tie B (PtDimA, schedule "A") / tie A (PtDimB, "B") to the outer opening of each pair (NaN = none).</summary>
    public double PtDimA { get; set; } = double.NaN;
    public double PtDimB { get; set; } = double.NaN;
    /// <summary>PT slots cut in the web (obround length x height, centre height above the flange bottom, pair spacing; 0 length = data only).</summary>
    public double PtSlotLength { get; set; }
    public double PtSlotHeight { get; set; } = In(2.25);
    public double PtSlotZ { get; set; } = In(7);
    public double PtPairSpacing { get; set; } = Ft(1) + In(4);
    /// <summary>Horizontal shear studs on each web face (0 = none): count per face, height above the flange bottom, min / max spacing, length.</summary>
    public int StudsPerFace { get; set; }
    public double StudZ { get; set; } = In(7);
    public double StudMinSpacing { get; set; } = In(3.5);
    public double StudMaxSpacing { get; set; } = In(6);
    public double StudLength { get; set; } = double.NaN;
    public string StudUnresolved { get; set; } = "";
    /// <summary>Holes through the web (s from tie A, z above the flange bottom; e.g. deck bar passages), diameter.</summary>
    public List<ArchPoint> WebHoles { get; set; } = new();
    public double WebHoleDiameter { get; set; } = In(15.0 / 16);
}

/// <summary>Bracing member between two named points.</summary>
public sealed class ArchBraceDefinition
{
    public string Name { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    /// <summary>Point the (continuous) member passes through (check, OPTIONAL).</summary>
    public string Via { get; set; } = "";
    /// <summary>Continuous member this one is cut short of at its start / end (crossing splice): trimmed to the gap off that member's flange tips.</summary>
    public string TrimStartAt { get; set; } = "";
    public string TrimEndAt { get; set; } = "";
    public double TrimGap { get; set; } = In(0.5);
}

/// <summary>Bracing crossing splice (p.141 / 142): top and bottom plates along the spliced member over the continuous one. Lengths L1 / L2 / L3 along
/// the plate edge (L2 = crossing zone), plan crossing angles (check), plate width, thickness, bolt pattern (edge, pitch, gage, edge to the continuous flange).</summary>
public sealed class ArchBraceCrossing
{
    public string Name { get; set; } = "";
    public string Point { get; set; } = "";
    public string Continuous { get; set; } = "";
    /// <summary>The two spliced pieces (first ends at the crossing, second starts there).</summary>
    public string PieceA { get; set; } = "";
    public string PieceB { get; set; } = "";
    public double TopL1 { get; set; }
    public double TopL2 { get; set; }
    public double TopL3 { get; set; }
    public double TopAngleDeg { get; set; } = double.NaN;
    public double BottomL1 { get; set; }
    public double BottomL2 { get; set; }
    public double BottomL3 { get; set; }
    public double BottomAngleDeg { get; set; } = double.NaN;
    public double Width { get; set; } = In(7);
    public double Thickness { get; set; } = In(0.5);
    public double Edge { get; set; } = In(1.5);
    public double Pitch { get; set; } = In(3);
    public double Gage { get; set; } = In(4);
    public double EdgeToContinuousFlange { get; set; } = In(1.75);
    public string Unresolved { get; set; } = "";
}

/// <summary>Outline vertex of a drawn plate (plate drawing coordinates, m). <see cref="Radius"/> describes the edge to the NEXT vertex: 0 = straight,
/// otherwise a true circular (minor) arc, + counter-clockwise / - clockwise. <see cref="Weld"/> marks the edge welded to the connection plate.</summary>
public sealed class ArchOutlinePoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; }
    public bool Weld { get; set; }
    public ArchOutlinePoint() { }
    public ArchOutlinePoint(double x, double y, double radius = 0, bool weld = false) { X = x; Y = y; Radius = radius; Weld = weld; }
}

/// <summary>A plate as drawn in one view (outline with arcs, round holes), in that view's 2D coordinates (m).</summary>
public sealed class ArchPlateDrawing
{
    public List<ArchOutlinePoint> Outline { get; set; } = new();
    /// <summary>Hole centres (X, Y); diameter = the bolt hole of the model.</summary>
    public List<ArchPoint> Holes { get; set; } = new();
}

/// <summary>Brace drawn under a gusset (dashed W shape in the plate view): a point of the brace web centre line at the brace END, and the
/// unit direction of the centre line from the end INTO the brace (drawing coordinates).</summary>
public sealed class ArchGussetLeg
{
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double DirX { get; set; }
    public double DirY { get; set; }
}

/// <summary>Gusset plate of a bracing connection in its own plane ("in plane of plate" view, flat pattern): outline with true arcs, holes, the
/// bend line (OPTIONAL: two outline vertices; the plate is bent there, each side lies on its own brace flange) and the braces drawn under it.</summary>
public sealed class ArchGussetPlate
{
    public ArchPlateDrawing Plate { get; set; } = new();
    public ArchPoint? BendA { get; set; }
    public ArchPoint? BendB { get; set; }
    public List<ArchGussetLeg> Legs { get; set; } = new();
}

/// <summary>
/// Upper lateral bracing connection at a rib panel point (plan p.129-140; one sheet per U(n) / U(n)' pair, drawn for U(n)', U(n) = the 180 deg
/// rotation about local Z). Top and bottom 7/8" GUSSETS lie on the outside faces of the brace flanges (one leg per brace, bent at the bend line
/// when two braces meet) and are welded to 7/8" CONNECTION PLATES bolted to the outside face of the rib flange facing the bracing; 1" STIFFENERS
/// between the rib flanges behind them. Placement: each gusset leg is placed by its brace (drawn brace centre line on the brace axis, the
/// welded edge on the connection plate face; the contact face on the brace flange); the connection plates and stiffeners by VIEW A-A
/// (origin = the WP, x along the rib, y radial outward, seen from the bracing). Every plan-vs-plan difference is a CHECK, never adjusted.
/// </summary>
public sealed class ArchBracingConnection
{
    public string Name { get; set; } = "";
    /// <summary>Work point (named point, e.g. "U5'").</summary>
    public string Point { get; set; } = "";
    public string Rib { get; set; } = "";
    /// <summary>Plan reference (sheet / page) printed in checks.</summary>
    public string Source { get; set; } = "";
    public ArchGussetPlate? TopGusset { get; set; }
    public ArchGussetPlate? BottomGusset { get; set; }
    /// <summary>VIEW A-A plates (origin WP, x along the rib, y radial outward, m).</summary>
    public ArchPlateDrawing? TopConnectionPlate { get; set; }
    public ArchPlateDrawing? BottomConnectionPlate { get; set; }
    /// <summary>VIEW A-A stiffener rectangles (x along the rib = thickness, y radial = width), m.</summary>
    public List<ArchPlateDrawing> Stiffeners { get; set; } = new();
    /// <summary>VIEW A-A: radial position of the rib web centre line relative to the WP (check; NaN = not given).</summary>
    public double WebAxisY { get; set; } = double.NaN;
    /// <summary>Plan: CL rib to the WP across the rib (B-B 8 1/4", check; NaN = not given).</summary>
    public double CheckWpFromRibCl { get; set; } = double.NaN;
    /// <summary>Plan: VIEW A-A angles of the welded edges / bolt lines to the rib axis (deg, checks).</summary>
    public List<double> CheckWeldAnglesDeg { get; set; } = new();
    /// <summary>Plan: gusset edge 1" inside the connection plate edge (D-D / H-H, check; NaN = not given).</summary>
    public double CheckGussetFromPlateEdge { get; set; } = double.NaN;
    public double GussetThickness { get; set; } = In(0.875);
    /// <summary>Cut each gusset's welded edge to the connection plate face (a bevel of the angle between the gusset plane and the plate face, printed on the
    /// sheets e.g. 111 deg 13'43"), so the edge bears over the full thickness for the 5/16" fillets (views D-D / E-E / I-I / J-J). False = the edge is
    /// square to the gusset (a wedge gap up to ~1/2" at the weld). The bevelled gusset is a faceted solid in IFC (no exact arcs).</summary>
    public bool MitreWeldedEdge { get; set; }
    public double ConnectionPlateThickness { get; set; } = In(0.875);
    public double StiffenerThickness { get; set; } = In(1);
    /// <summary>Stiffener corner clips at the web-flange junctions (B-B 1 1/2").</summary>
    public double StiffenerClip { get; set; } = In(1.5);
    public string Material { get; set; } = "ASTM A709 Gr. 50";
    /// <summary>Stiffeners are rib plates (p.127 quantity note: rib incl. stiffener plates).</summary>
    public string StiffenerMaterial { get; set; } = "ASTM A709 Gr. 50-T1";
    /// <summary>Items of this connection that are not dimensioned (UNRESOLVED texts; the parts they affect are flagged).</summary>
    public string BraceEndUnresolved { get; set; } = "";
    public string StiffenerUnresolved { get; set; } = "";
}

/// <summary>Knuckle / end floorbeam (simplified): end flange plate under the end line, end web on it, deck plate between the end line and the first
/// floorbeam line. Plan heights not dimensioned are NaN + an UNRESOLVED text; a scaled value is used only when given, and the part is flagged.</summary>
public sealed class ArchKnuckleDefinition
{
    public string Name { get; set; } = "";
    /// <summary>End line (bent line) from point / to point (e.g. T34' -> T0).</summary>
    public string EndFrom { get; set; } = "";
    public string EndTo { get; set; } = "";
    /// <summary>Overall end floorbeam length along the end line, centred on the chord (21'-2).</summary>
    public double EndLength { get; set; } = Ft(21) + In(2);
    /// <summary>Deck plate outline (local point names, in order).</summary>
    public List<string> DeckPlate { get; set; } = new();
    public double EndFlangeThickness { get; set; } = In(2);
    public double EndFlangeWidth { get; set; } = In(32);
    /// <summary>Local Z of the end flange bottom.</summary>
    public double EndFlangeBottomZ { get; set; } = -In(1.125);
    /// <summary>Outside face of the end web (= outer edge of the end flange) measured outward from the end line, normal to it (p.124: 1'-1 1/2).</summary>
    public double EndWebOffset { get; set; } = Ft(1) + In(1.5);
    public double EndWebThickness { get; set; } = In(1);
    public double EndWebHeightAtChord { get; set; } = Ft(1) + In(0.125);
    /// <summary>End web height at its ends (NaN = not dimensioned: then <see cref="EndWebScaledHeightAtEnds"/> is used, flagged).</summary>
    public double EndWebHeightAtEnds { get; set; } = double.NaN;
    public double EndWebScaledHeightAtEnds { get; set; } = double.NaN;
    public string EndWebUnresolved { get; set; } = "";
    public double DeckPlateThickness { get; set; } = In(1);
    /// <summary>Local Z of the deck plate top.</summary>
    public double DeckPlateTopZ { get; set; } = In(0.875);
    public string DeckPlateUnresolved { get; set; } = "";
    public string Material { get; set; } = "ASTM A709 Gr. 50";
    /// <summary>PT anchorages (data): local points of the tendon anchor centres.</summary>
    public List<ArchPoint> PtAnchors { get; set; } = new();

    // ---- v2 detail (p.123-126; OPTIONAL: with Detail null the v1 simplified knuckle is built)
    public ArchKnuckleDetail? Detail { get; set; }
    /// <summary>Rib-to-tie corners of this knuckle (one per rib end on the end line).</summary>
    public List<ArchKnuckleCorner> Corners { get; set; } = new();
    /// <summary>End web PT openings: centres along the end line from the chord crossing (+ toward EndTo), size, centre height above the end flange top.</summary>
    public List<double> EndWebOpeningsAt { get; set; } = new();
    public double EndWebOpeningLength { get; set; } = In(14);
    public double EndWebOpeningHeight { get; set; } = In(3);
    public double EndWebOpeningZ { get; set; } = In(6.125);
    /// <summary>End web shear studs (inside face): positions along the end line from the chord crossing, height above the end flange top, length.</summary>
    public List<double> EndWebStudsAt { get; set; } = new();
    public double EndWebStudZ { get; set; } = In(6.125);
    public double EndWebStudLength { get; set; } = In(7);
    /// <summary>Deck ("end") plate edges: from the tie CL (overlap on the tie flange), from the first floorbeam CL, over the end flange inner edge (NaN = v1 outline).</summary>
    public double DeckPlateEdgeFromTieCL { get; set; } = double.NaN;
    public double DeckPlateEdgeFromFloorbeamCL { get; set; } = double.NaN;
    public double DeckPlateOverEndFlange { get; set; } = double.NaN;
    /// <summary>Deck plate shear studs: rows parallel to the end line (X = distance from the end line toward the span, Z = stud length), spacing along a
    /// row, clearance to the plate edges; UNRESOLVED text when the spacing along the rows is scaled.</summary>
    public List<ArchPoint> DeckStudRows { get; set; } = new();
    public double DeckStudSpacing { get; set; }
    public double DeckStudEdge { get; set; } = In(3);
    public string DeckStudUnresolved { get; set; } = "";
}

/// <summary>One rib-to-tie corner of a knuckle: the tie end point (= rib springing), the rib and tie, and the bent arch flange plate leg heights.</summary>
public sealed class ArchKnuckleCorner
{
    public string Point { get; set; } = "";
    public string Rib { get; set; } = "";
    public string Tie { get; set; } = "";
    /// <summary>Rib splice point where the built-up stub meets the rolled rib (e.g. RS1).</summary>
    public string Splice { get; set; } = "";
    /// <summary>Horizontal leg height of the outside / inside arch flange plate (in the plane of the plate).</summary>
    public double OutsideLegHeight { get; set; }
    public double InsideLegHeight { get; set; }
}

/// <summary>Knuckle corner detail (p.124 / p.125): bent 1" arch flange plates (rib flange width, R6 bends, 1" clip) from the end web to the first rib
/// splice, stub web, vertical stiffener (normal to the chord, outward of the springing), arch stiffener (normal to the rib), tie joint at the end
/// flange inner edge with 1/2" gussets (both faces, bolted), 1 1/8" fill + 1" splice plate under the tie flange, 1" cheese plates at the jacks.</summary>
public sealed class ArchKnuckleDetail
{
    public double FlangePlateThickness { get; set; } = In(1);
    public double FlangeHalfWidth { get; set; } = In(7.375);
    public double BendRadius { get; set; } = In(6);
    public double BottomClip { get; set; } = In(1);
    public double StubWebThickness { get; set; } = In(1);
    public string StubWebUnresolved { get; set; } = "";
    public double StiffenerFromSpringing { get; set; } = In(7);
    public double StiffenerThickness { get; set; } = In(1);
    public double StiffenerHeight { get; set; } = In(9.5);
    public double ArchStiffenerAlongRib { get; set; } = Ft(1) + In(11.625);
    public double ArchStiffenerThickness { get; set; } = In(1);
    public string ArchStiffenerUnresolved { get; set; } = "";
    public double JointGap { get; set; } = In(0.5);
    public double GussetThickness { get; set; } = In(0.5);
    public double GussetHeight { get; set; } = In(9);
    public double GussetTopBelowWeb { get; set; } = In(0.5);
    public double GussetBoltFirst { get; set; } = In(2.5);
    public double GussetBoltPitch { get; set; } = In(3);
    public int GussetBoltColumns { get; set; } = 3;
    public double GussetEdge { get; set; } = In(1.5);
    public List<double> GussetRows { get; set; } = new() { In(1.5), In(4.5), In(7.5) };
    public double GussetClip { get; set; } = In(1);
    public double FillThickness { get; set; } = In(1.125);
    public double SplicePlateThickness { get; set; } = In(1);
    public double SplicePlateLength { get; set; } = In(24);
    /// <summary>OPTIONAL plan outline of the 1" splice plate and the fill (p.124 partial plan, dashed "1\" SPLICE PL AND FILL PL"; NaN = v2 tie-flange-wide
    /// rectangle): half width each side of the chord through the T point, end edge parallel to the joint (end flange edge) this far past it, the
    /// ACUTE end corner cut (along the side edge / along the end edge). The plate starts at the end web outside face.</summary>
    public double SpliceHalfWidth { get; set; } = double.NaN;
    public double SpliceBeyondJoint { get; set; } = double.NaN;
    public double SpliceChamferSide { get; set; }
    public double SpliceChamferEnd { get; set; }
    /// <summary>OPTIONAL end flange-to-splice bolt rows (p.124 "7 SPA @ 4 3/8"): lateral offset each side of the chord, first bolt from the end web outside face
    /// (along the chord), count, pitch. Count 0 = none.</summary>
    public double SpliceBoltRowOffset { get; set; }
    public double SpliceBoltFirst { get; set; }
    public int SpliceBoltCount { get; set; }
    public double SpliceBoltPitch { get; set; }
    public string SpliceUnresolved { get; set; } = "";
    public double CheeseThickness { get; set; } = In(1);
    public double CheeseLength { get; set; } = In(10.25);
    public double CheeseWidth { get; set; } = In(10.25);
    /// <summary>Jacks: distance from the CL bearing along the chord, both sides (p.124: 1'-1).</summary>
    public double JackFromBearing { get; set; } = Ft(1) + In(1);
    /// <summary>CL bearing from the springing point along the chord toward the span (p.124: 2").</summary>
    public double BearingFromSpringing { get; set; } = In(2);
    public string CheeseUnresolved { get; set; } = "";
    public string Material { get; set; } = "ASTM A709 Gr. 50";
}

/// <summary>Cast-in-place deck on a corrugated steel form deck spanning the floorbeams, curbs, longitudinal PT (data).</summary>
public sealed class ArchDeckDefinition
{
    /// <summary>Ties the slab spans between (edges at their inner web faces); first = right (south), second = left.</summary>
    public string RightTie { get; set; } = "";
    public string LeftTie { get; set; } = "";
    /// <summary>End lines (knuckle names, begin then end) the slab runs between (outside faces of the end webs).</summary>
    public List<string> EndKnuckles { get; set; } = new();
    /// <summary>Top = finished grade of the bridge alignment (profile + cross slope) when true.</summary>
    public bool TopAtFinishedGrade { get; set; } = true;
    /// <summary>Local Z of the bottom of the form deck (on the floorbeam flange top).</summary>
    public double FormBottomZ { get; set; } = In(0.875);
    public double FormDepth { get; set; } = In(3);
    public double FormPitch { get; set; } = In(8);
    /// <summary>Form profile: width of the top flat and of the flute bottom; flute opening at the top = pitch - top flat.</summary>
    public double FormTopFlat { get; set; } = In(5.75);
    public double FormFluteBottom { get; set; } = In(1.3);
    public double FormThickness { get; set; } = In(0.1046);
    public string FormDescription { get; set; } = "";
    public string FormUnresolved { get; set; } = "";
    public double ConcreteStrengthKsi { get; set; } = 5;
    public List<ArchCurbDefinition> Curbs { get; set; } = new();
    public List<ArchTendon> Tendons { get; set; } = new();
    /// <summary>PT system (OPTIONAL; null = tendons are data only): strands, anchorages at the knuckle end webs.</summary>
    public ArchPtSystem? PtSystem { get; set; }
    /// <summary>Deck reinforcement (OPTIONAL, null = none).</summary>
    public ArchDeckRebar? Rebar { get; set; }
    /// <summary>Corrugations (flute centres) on the longitudinal bottom bar grid (true: a flute at every <see cref="ArchDeckRebar.LongSpacing"/> from the
    /// structure CL, "#5 bar in each corrugation"); false = profile from the south tie web face (v1).</summary>
    public bool FlutesOnBarGrid { get; set; }
}

/// <summary>Longitudinal PT (p.155): strands per tendon (flat duct), strand size, jacking force, strand spacing in the duct, anchorage envelope at the
/// knuckle end webs (the tendon ends run to the nearest end web PT opening).</summary>
public sealed class ArchPtSystem
{
    public int StrandsPerTendon { get; set; } = 4;
    public double StrandDiameter { get; set; } = In(0.6);
    public double StrandArea { get; set; } = In(1) * In(1) * 0.217;
    public double JackingForceKips { get; set; } = 170;
    public double StrandSpacing { get; set; } = In(1.9375);
    public double AnchorLength { get; set; } = In(10.5);
    public double AnchorWidth { get; set; } = In(13);
    public double AnchorHeight { get; set; } = In(4);
    public string Unresolved { get; set; } = "";
    public string StrandMaterial { get; set; } = "ASTM A416 Gr. 270 low-relaxation strand";
    public string AnchorMaterial { get; set; } = "PT flat anchorage (DYWIDAG, by supplier)";
    /// <summary>OPTIONAL flat duct (0 width = no duct, v1-v4): outside width x height (obround), wall, from the anchorage end (AnchorLength from the end
    /// web) along the tendon to the other anchorage; strands inside side by side at <see cref="DuctStrandSpacing"/> (converging from
    /// <see cref="StrandSpacing"/> at the anchor over the anchorage length).</summary>
    public double DuctWidth { get; set; }
    public double DuctHeight { get; set; }
    public double DuctWall { get; set; }
    public double DuctStrandSpacing { get; set; }
    public string DuctMaterial { get; set; } = "Corrugated polyethylene / polypropylene PT duct";
    /// <summary>UNRESOLVED reason (page + why) for the duct size; the duct stays on its layer, flagged as a dimension.</summary>
    public string DuctUnresolved { get; set; } = "";
    /// <summary>Anchorage zone bars (S2): distances from the end web inside face along the tendon, outline width x height around the tendon.</summary>
    public List<double> ZoneBarsAt { get; set; } = new();
    public double ZoneBarWidth { get; set; } = In(9.25);
    public double ZoneBarHeight { get; set; } = In(6);
    public string ZoneBarMark { get; set; } = "S2";
    public string ZoneBarSize { get; set; } = "#5";
}

/// <summary>Deck bars (p.152 / 154): transverse top (A) and bottom (B) bars at a spacing along the structure CL, longitudinal top (D1) / bottom (D2) lines
/// on a grid across the deck (count, spacing, bar length, lap), curb bars (S stirrups at a spacing, D4 corner bars), covers.</summary>
public sealed class ArchDeckRebar
{
    public string TopTransMark { get; set; } = "A1";
    public double TopTransLength { get; set; }
    public string BottomTransMark { get; set; } = "B1";
    public double BottomTransLength { get; set; }
    public double TransSpacing { get; set; } = In(8);
    public string TopLongMark { get; set; } = "D1";
    public string BottomLongMark { get; set; } = "D2";
    public int LongCount { get; set; } = 27;
    public double LongSpacing { get; set; } = In(8);
    public double LongBarLength { get; set; } = Ft(40);
    public double LongLap { get; set; } = Ft(2) + In(8);
    public string Size { get; set; } = "#5";
    public double TopCover { get; set; } = In(2.5);
    public double BottomClear { get; set; } = In(1.75);
    public double EndCover { get; set; } = In(2);
    public string CurbStirrupMark { get; set; } = "S1";
    public string CurbStirrupSize { get; set; } = "#4";
    public double CurbStirrupWidth { get; set; } = In(6);
    public double CurbStirrupHeight { get; set; } = In(9);
    public double CurbTopCover { get; set; } = In(2);
    /// <summary>OPTIONAL 135-deg hook tail of each end of the closed curb stirrup (out-to-out, p.154 BAR S1 "4 1/2\" (TYP)"); 0 = closed loop without tails.</summary>
    public double CurbStirrupHook { get; set; }
    public string CurbLongMark { get; set; } = "D4";
    /// <summary>OPTIONAL number of transverse rows (A / B) equally spaced between the end covers (the bill count); 0 = as many as <see cref="TransSpacing"/> allows.</summary>
    public int TransCount { get; set; }
    /// <summary>OPTIONAL curb stirrups per curb, equally spaced (the bill count / curbs); 0 = by <see cref="TransSpacing"/>.</summary>
    public int CurbStirrupCount { get; set; }
    /// <summary>Top transverse bars per row: 1 = one straight bar centred on CL NHT (default); 2 = two bars lapped at CL NHT, each hooked down at its deck edge
    /// with a return leg lapping the bottom transverse bar (p.152 "hook top bar with 2'-8 lap to bottom bar"): drop leg at <see cref="TopTransHookOffset"/> inside the
    /// CL of the tie, return <see cref="TopTransHookReturn"/> long on top of the bottom bar, top run = listed length - drop - return (the centre lap results).</summary>
    public int TopTransPerRow { get; set; } = 1;
    public double TopTransHookOffset { get; set; } = In(7.5);
    public double TopTransHookReturn { get; set; } = Ft(2) + In(8);
    /// <summary>OPTIONAL edge bars (D3, p.152 / 154): one per tie per panel between floorbeam lines, centred in the panel, straight <see cref="EdgeBarLength"/>
    /// (out-to-out) with <see cref="EdgeBarHook"/> up legs and returns at both ends, <see cref="EdgeBarOffset"/> inside the CL of the tie at the bottom bar level.
    /// "" = none.</summary>
    public string EdgeBarMark { get; set; } = "";
    public double EdgeBarLength { get; set; }
    public double EdgeBarHook { get; set; } = In(5);
    public double EdgeBarOffset { get; set; } = In(6);
    public string Unresolved { get; set; } = "";
    /// <summary>Plan bar list (check): mark, count, length.</summary>
    public List<ArchPoint> PlanList { get; set; } = new();
}

/// <summary>Curb along an alignment offset: inner face at <see cref="InnerOffset"/> (right +), width toward the deck edge.</summary>
public sealed class ArchCurbDefinition
{
    public string Name { get; set; } = "";
    public double InnerOffset { get; set; }
    public double Width { get; set; } = In(10);
    public double Height { get; set; } = In(4);
}

/// <summary>PT tendon (data only in v1): local polyline of its centreline.</summary>
public sealed class ArchTendon
{
    public string Name { get; set; } = "";
    public List<ArchPoint> Path { get; set; } = new();
    public string Description { get; set; } = "";
}

/// <summary>Elastomeric bearing under a knuckle corner (p.107): stack built UP from the plan pedestal top.</summary>
public sealed class ArchBearingDefinition
{
    public string Name { get; set; } = "";
    public string Support { get; set; } = "";
    /// <summary>Bearing centre (local point name; plan point = its projection to the seat).</summary>
    public string Point { get; set; } = "";
    /// <summary>Plan pedestal top (top of concrete bearing surface).</summary>
    public double SeatElevation { get; set; } = double.NaN;
    /// <summary>Local Z of the underside of the steel above the bearing (chain check).</summary>
    public double SteelUndersideZ { get; set; } = double.NaN;
    public string Fixity { get; set; } = "Partially fixed";
    /// <summary>OPTIONAL (default false = stack built UP from the plan pedestal top): true = the stack is built DOWN from the underside of the steel
    /// (SteelUndersideZ at the bearing centre): the beveled upper sole plate bears on the steel with its top in the plane of the steel underside, the
    /// rest below it level; the plan pedestal top is then checked (p.107 note 8: the pedestal elevations are adjusted to the fabricated bearing).</summary>
    public bool HangFromSteel { get; set; }
    /// <summary>Long axis of the masonry / sole plates in plan: angle (deg) from the support line +S (right) toward +N (up-station), as the plan
    /// pedestal (NaN = normal to the support line).</summary>
    public double LongAxisAngleDeg { get; set; } = double.NaN;
    public string LongAxisSource { get; set; } = "";
    public double PadThickness { get; set; } = In(0.125);
    public double MasonryLength { get; set; } = Ft(3) + In(2);
    public double MasonryWidth { get; set; } = Ft(1) + In(5);
    public double MasonryThickness { get; set; } = In(2);
    public double SoleLength { get; set; } = In(16);
    public double SoleWidth { get; set; } = In(15);
    public double LowerSoleThickness { get; set; } = In(3);
    public double ElastomerLength { get; set; } = In(15);
    public double ElastomerWidth { get; set; } = In(15);
    public double ElastomerHeight { get; set; } = In(3.625);
    /// <summary>Beveled upper sole: thickness at the A end, CL, C end (along the long direction).</summary>
    public double UpperSoleA { get; set; } = In(3.125);
    public double UpperSoleB { get; set; } = In(3);
    public double UpperSoleC { get; set; } = In(2.875);
    public double AnchorDiameter { get; set; } = In(1.5);
    public double AnchorEmbedment { get; set; } = Ft(1.5);
    /// <summary>Anchor bolt positions from the bearing centre (along the long direction, across).</summary>
    public double AnchorAlong { get; set; } = In(13);
    public double AnchorAcross { get; set; } = In(5.5);
    public double PinDiameter { get; set; } = In(3);
}
