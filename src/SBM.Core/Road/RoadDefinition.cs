using System.Text.Json.Serialization;

namespace SBM.Core.Road;

/// <summary>Kind of a road template component (IFC IfcRoadPart predefined type / IfcKerb).</summary>
public enum RoadComponentKind { Lane, Shoulder, Median, Sidewalk, CurbAndGutter, Ditch, Other }

/// <summary>One pavement layer of a component, from the top down (surface, base, subbase ...). Thickness measured vertically, meters.</summary>
public sealed class RoadCourse
{
    public string Name { get; set; } = "";
    public double Thickness { get; set; }
    public string Material { get; set; } = "";
}

/// <summary>A point of a curb / gutter section: U outward from the component's inner edge, V up from the top of the inner edge (meters).</summary>
public sealed class RoadSectionPoint
{
    public double U { get; set; }
    public double V { get; set; }
    public RoadSectionPoint() { }
    public RoadSectionPoint(double u, double v) { U = u; V = v; }
}

/// <summary>
/// A road template component, placed outward from the previous one (or from the side's <see cref="RoadSide.StartOffset"/>). Lanes / shoulders /
/// sidewalks / medians / ditches are a top surface of <see cref="Width"/> with <see cref="Courses"/> below it; a curb (or any component with a
/// <see cref="Section"/>) is that closed section swept along the road (its courses are ignored).
/// </summary>
public sealed class RoadComponent
{
    public string Name { get; set; } = "";
    public RoadComponentKind Kind { get; set; } = RoadComponentKind.Lane;
    public double Width { get; set; }
    /// <summary>OPTIONAL cross slope (rise / run, + = rising OUTWARD from the alignment); null = the alignment's own cross-slope table (superelevation).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Slope { get; set; }
    public List<RoadCourse> Courses { get; set; } = new();
    /// <summary>OPTIONAL closed section (curb and gutter, barrier): U outward from the inner edge, V up from its top; its width = the largest U.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RoadSectionPoint>? Section { get; set; }
    public string Source { get; set; } = "";
    /// <summary>Plan items not dimensioned (UNRESOLVED flag text, see <see cref="UnresolvedFlag"/>); raised as a warning, the component is still built.</summary>
    public string Unresolved { get; set; } = "";
}

/// <summary>Cut / fill side slope beyond the last component, to the existing ground (terrain). All values from the plans; none assumed.</summary>
public sealed class RoadSideSlope
{
    /// <summary>Horizontal per 1 vertical in cut (e.g. 2 = 2H:1V).</summary>
    public double CutRatio { get; set; }
    /// <summary>Horizontal per 1 vertical in fill.</summary>
    public double FillRatio { get; set; }
    /// <summary>Largest horizontal width searched for the daylight point (meters); no daylight within it = the slope is not built there (reported).</summary>
    public double MaxWidth { get; set; }
}

/// <summary>What a subgrade layer is: decides how the earthwork treats it (<see cref="RoadSubgradeLayer"/>).</summary>
public enum RoadSubgradeKind
{
    /// <summary>Placed material below the pavement courses (granular / aggregate subbase, select fill, drainage layer): earthwork is measured to its BOTTOM
    /// (the excavation includes it in cut; the embankment stops under it in fill), its own volume is the layer's.</summary>
    Subbase,
    /// <summary>In-place soil treated with lime / cement / fly ash (stabilized, modified subgrade): earthwork is measured to its TOP.</summary>
    TreatedSubgrade,
    /// <summary>In-place soil scarified / compacted (prepared, compacted, select subgrade bed): earthwork is measured to its TOP.</summary>
    PreparedSubgrade,
    /// <summary>Any other layer named by the plans: treated like in-place soil (earthwork to its top).</summary>
    Other,
}

/// <summary>
/// One subgrade layer of a side of the template (OPTIONAL <see cref="RoadSide.Subgrade"/>, listed TOP DOWN): a constant-thickness band (measured vertically)
/// below the bottom of the pavement courses of the components it lies under, or below the layer above it. The top of the first layer = "top of subgrade"
/// = the bottom of the pavement structure (it steps where the components' course depths differ). All values from the plans; none assumed.
/// </summary>
public sealed class RoadSubgradeLayer
{
    public string Name { get; set; } = "";
    public RoadSubgradeKind Kind { get; set; } = RoadSubgradeKind.Subbase;
    public double Thickness { get; set; }
    public string Material { get; set; } = "";
    /// <summary>Names of the ADJACENT components of this side the layer lies under (inner to outer); empty = every component of the side.</summary>
    public List<string> Under { get; set; } = new();
    /// <summary>Distance the layer continues OUTWARD past the outer edge of the last component it lies under (meters, at the slope of that component's
    /// bottom), e.g. "subbase 1'-0 beyond the edge of shoulder"; 0 = ends at the edge.</summary>
    public double ExtendBeyond { get; set; }
    /// <summary>Outer end face of the layer, horizontal per 1 vertical (e.g. 2 for a 2:1 daylighted edge); 0 = vertical.</summary>
    public double EdgeRatio { get; set; }
    public string Source { get; set; } = "";
    /// <summary>Plan items not dimensioned (UNRESOLVED flag text); raised as a warning, the layer is still built.</summary>
    public string Unresolved { get; set; } = "";
}

/// <summary>One side of the template (left = negative offsets, right = positive; Civil 3D convention).</summary>
public sealed class RoadSide
{
    /// <summary>Offset of the first component's inner edge from the alignment (absolute value, meters; 0 = at the alignment).</summary>
    public double StartOffset { get; set; }
    public List<RoadComponent> Components { get; set; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoadSideSlope? SideSlope { get; set; }
    /// <summary>OPTIONAL subgrade layers under the pavement courses, top down (subbase, treated / prepared subgrade); null = none (earthwork to the
    /// bottom of the courses, as before).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RoadSubgradeLayer>? Subgrade { get; set; }
}

/// <summary>
/// A roadway corridor (OPTIONAL <see cref="Project.BridgeProject.Roads"/>; design note docs/plan-data/roads-walls-utilities-lod-design.md section 2):
/// an alignment + its profile and cross-slope table, a template (left / right component lists with courses) between two stations, optional side
/// slopes daylighting to the terrain. Generator <see cref="RoadGenerator"/>.
/// </summary>
public sealed class RoadDefinition
{
    public string Name { get; set; } = "";
    public string AlignmentName { get; set; } = "";
    public double BeginStation { get; set; }
    public double EndStation { get; set; }
    public RoadSide Left { get; set; } = new();
    public RoadSide Right { get; set; } = new();
    /// <summary>Largest station spacing of the template sections (meters); kink stations of the profile / cross slope are always included.</summary>
    public double SampleSpacing { get; set; } = 2.0;
    public string Source { get; set; } = "";
    public List<string> Unresolved { get; set; } = new();
    /// <summary>OPTIONAL ties of the road's begin / end section to a bridge end (approach slab far end, or the deck end): the station is then taken from the
    /// bridge and <see cref="BeginStation"/> / <see cref="EndStation"/> are only used when the tie cannot be made (reported).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoadBridgeTie? BeginAtBridge { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoadBridgeTie? EndAtBridge { get; set; }
    /// <summary>OPTIONAL required clearances to the bridges over / beside the road (plan values; see <see cref="RoadClearanceDefinition"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RoadClearanceDefinition>? Clearances { get; set; }
    /// <summary>OPTIONAL feature lines (Civil 3D corridor feature lines from LandXML PlanFeatures, <c>sbm import-corridor</c>): with 2 or more the road is built
    /// BETWEEN them (strips between neighbouring lines, ordered by offset) instead of the Left / Right template; their 3D points are the finished surface.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RoadFeatureLine>? FeatureLines { get; set; }
    /// <summary>OPTIONAL pavement courses (top down) under every feature-line strip; null / empty = top surface only (flagged: the pavement structure is not in a
    /// LandXML corridor export).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RoadCourse>? FeatureLineCourses { get; set; }
}

/// <summary>A 3D point of a feature line (project coordinates, meters).</summary>
public sealed class RoadFeaturePoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public RoadFeaturePoint() { }
    public RoadFeaturePoint(double x, double y, double z) { X = x; Y = y; Z = z; }
}

/// <summary>A corridor feature line (edge of travelled way, shoulder, ditch ...): a 3D polyline along the alignment.</summary>
public sealed class RoadFeatureLine
{
    public string Name { get; set; } = "";
    public List<RoadFeaturePoint> Points { get; set; } = new();
    /// <summary>OPTIONAL kind of the strip OUTWARD of this line (away from the alignment); null = from the line name (shoulder, curb, sidewalk, median, ditch, else lane).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoadComponentKind? Kind { get; set; }
}
