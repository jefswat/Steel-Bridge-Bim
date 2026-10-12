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
    public List<ArchFloorbeamDefinition> Floorbeams { get; set; } = new();
    public List<ArchBraceDefinition> Bracing { get; set; } = new();
    /// <summary>AISC section of the bracing members (exact name, e.g. "W8x28").</summary>
    public string BracingSection { get; set; } = "W8x28";
    public string BracingMaterial { get; set; } = "ASTM A709 Gr. 50";
    /// <summary>Plan item the bracing web orientation is not given by (UNRESOLVED text; empty = given).</summary>
    public string BracingOrientationUnresolved { get; set; } = "";
    public List<ArchKnuckleDefinition> Knuckles { get; set; } = new();
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
    public Geometry.Vec3 Vec => new(X, Y, Z);
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
    /// <summary>Field splices (data; positions UNRESOLVED on the plans, see <see cref="ArchDefinition.Unresolved"/>).</summary>
    public List<string> Splices { get; set; } = new();
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
    /// <summary>Plan pin-to-pin length (check; NaN = none).</summary>
    public double PlanLength { get; set; } = double.NaN;
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
    /// <summary>PT openings (data): distance from the CL of tie A / tie B to the outer opening of each pair, pair spacing.</summary>
    public double PtDimA { get; set; } = double.NaN;
    public double PtDimB { get; set; } = double.NaN;
}

/// <summary>Bracing member between two named points.</summary>
public sealed class ArchBraceDefinition
{
    public string Name { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
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
