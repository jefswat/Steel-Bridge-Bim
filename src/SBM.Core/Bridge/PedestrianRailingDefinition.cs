using System.Text.Json;
using System.Text.Json.Serialization;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>IFC predefined type of a railing run (IfcRailingTypeEnum).</summary>
public enum RailingKind { Guardrail, Handrail, Fence, Balustrade }

/// <summary>Kind of a continuous rail member: a handrail (IfcRailing HANDRAIL) or another rail / fence member (IfcMember MEMBER).</summary>
public enum RailMemberKind { Handrail, Rail }

/// <summary>Point of a railing section: R = distance OUTWARD (away from the alignment) from the railing's reference line, H = height above the
/// railing base (the top of concrete found under the reference line at <see cref="PedestrianRailingDefinition.BaseProbe"/>). Meters.</summary>
public sealed class RailPoint
{
    public double R { get; set; }
    public double H { get; set; }
    public RailPoint() { }
    public RailPoint(double r, double h) { R = r; H = h; }
}

/// <summary>
/// A post: its side outline (polygon in the section plane, <see cref="RailPoint"/>s, any shape: tapered, curved top) extruded <see cref="Thickness"/>
/// along the rail, plumb. Posts are equally spaced at most <see cref="Spacing"/> apart from <see cref="EndDistance"/> inside each run end (a run of
/// length L gets n = ceil((L - 2 e) / s) + 1 posts). Optional base plate and anchor bolts.
/// </summary>
public sealed class RailPostDefinition
{
    public List<RailPoint> Profile { get; set; } = new();
    /// <summary>Post thickness along the rail.</summary>
    public double Thickness { get; set; }
    /// <summary>Maximum post spacing along the reference line.</summary>
    public double Spacing { get; set; }
    /// <summary>First / last post from the run ends, along the reference line.</summary>
    public double EndDistance { get; set; }
    /// <summary>OPTIONAL post layout on the ALIGNMENT stations (the plan elevations lay the posts out along the centerline): posts on the radial lines at
    /// <c>GridStation + k x Spacing</c> (k any integer, <see cref="Spacing"/> measured along the alignment), those inside the run only (the run ends
    /// included); <see cref="EndDistance"/> is then ignored. null = equal spacing from the run ends.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GridStation { get; set; }
    /// <summary>Size text, e.g. "PL 3/4 tapered post".</summary>
    public string Description { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RailBasePlateDefinition? BasePlate { get; set; }
    /// <summary>Not dimensioned on the plans: reason with page (the posts are built and flagged UNRESOLVED with <see cref="Basis"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
}

/// <summary>
/// Fence fabric (chain link / mesh) panels between the posts (OPTIONAL <see cref="PedestrianRailingDefinition.Fabric"/>): a thin sheet following
/// <see cref="Path"/> (a polyline in the section plane, bottom to top, e.g. the post's outer face), moved <see cref="OffsetFromPost"/> to its right-hand
/// side (outward for a path drawn upward with R outward), clipped to <see cref="BottomH"/>..<see cref="TopH"/> and <see cref="Thickness"/> thick; one panel
/// per bay (between consecutive posts, the end bays to the run ends), kept <see cref="PostClearance"/> clear of each post's side faces (half the post
/// thickness + clearance from its centre). Mass = <see cref="MassPerArea"/> x panel area (the part density is MassPerArea / Thickness).
/// </summary>
public sealed class RailFabricDefinition
{
    public string Description { get; set; } = "";
    /// <summary>Section line of the fabric face the panel follows (R outward, H above the railing base), bottom to top.</summary>
    public List<RailPoint> Path { get; set; } = new();
    /// <summary>Distance of the panel's mid-plane from <see cref="Path"/> (right-hand side of the upward path = outward).</summary>
    public double OffsetFromPost { get; set; }
    /// <summary>Lowest height of the fabric above the base.</summary>
    public double BottomH { get; set; }
    /// <summary>Highest height (null = the end of <see cref="Path"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopH { get; set; }
    /// <summary>Panel thickness (two wire diameters for a woven fabric).</summary>
    public double Thickness { get; set; }
    /// <summary>Nominal mass per area of the fabric (kg/m2).</summary>
    public double MassPerArea { get; set; }
    /// <summary>Gap between the fabric edge and the post face (along the rail).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PostClearance { get; set; }
    /// <summary>Material text (IFC / DB).</summary>
    public string Material { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
}

/// <summary>Base plate under a post: <see cref="Length"/> along the rail x <see cref="Width"/> across (centred at <see cref="CenterR"/>) x
/// <see cref="Thickness"/>, bottom on the railing base; anchor bolts at (along, across) offsets from the plate centre.</summary>
public sealed class RailBasePlateDefinition
{
    public double Length { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public double CenterR { get; set; }
    public double AnchorDiameter { get; set; }
    /// <summary>Anchor embedment below the top of concrete.</summary>
    public double AnchorEmbedment { get; set; }
    /// <summary>Anchor positions from the plate centre: R = along the rail (ahead +), H = across the rail (outward +).</summary>
    public List<RailPoint> Anchors { get; set; } = new();
}

/// <summary>A continuous rail member along the run (pipe of <see cref="Diameter"/>, or a <see cref="Width"/> x <see cref="Height"/> bar), centre
/// at <see cref="Center"/> in the section.</summary>
public sealed class RailMemberDefinition
{
    public string Name { get; set; } = "";
    public RailMemberKind Kind { get; set; } = RailMemberKind.Handrail;
    /// <summary>Pipe outside diameter (0 = rectangular bar <see cref="Width"/> x <see cref="Height"/>).</summary>
    public double Diameter { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Width { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Height { get; set; }
    public RailPoint Center { get; set; } = new();
    /// <summary>Size text, e.g. "1 1/2 in pipe".</summary>
    public string Description { get; set; } = "";
    /// <summary>Distance the member stops short of each run end (0 = full length).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EndSetback { get; set; }
    /// <summary>Mass per length (kg/m), e.g. a pipe whose wall is not modeled, or 0 when the member's weight is carried by the run's envelope mass;
    /// null = volume x density.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MassPerLength { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
}

/// <summary>
/// One run of a pedestrian / bicycle railing (OPTIONAL, <see cref="BridgeDefinition.Railings"/>), built by <c>Railing.PedestrianRailingGenerator</c>
/// along a REFERENCE LINE at a constant <see cref="Offset"/> from the alignment (right +; the outward side is the side of the offset). The railing
/// stands on the top of concrete found under the line (deck, curb, barrier: the bridge's concrete solids), so it follows curbs on any deck incl. the tied
/// arch. Components (all optional): <see cref="Envelope"/> = the drawn railing section swept continuously (placeholder when the post / fence details are
/// not in the set), <see cref="Post"/>s with base plates and anchor bolts, continuous <see cref="Members"/> (handrail, rails). Run limits: radial lines at
/// <see cref="BeginStation"/> / <see cref="EndStation"/>, else the deck end lines, else the first / last support line moved
/// <see cref="BeginLimitOffset"/> / <see cref="EndLimitOffset"/> away from the span. Lengths in meters. Steel parts go into the bridge's steel model
/// (assembly kind Railing, IFC IfcRailing aggregating the parts); quantities by length along the reference line (<see cref="PlanLength"/> check).
/// </summary>
public sealed class PedestrianRailingDefinition
{
    public string Name { get; set; } = "";
    /// <summary>Railing type as the plans name it (IFC ObjectType), e.g. "RAIL (PED)(HIGH)".</summary>
    public string TypeName { get; set; } = "";
    public RailingKind Kind { get; set; } = RailingKind.Guardrail;
    /// <summary>Reference line offset from the alignment (right +). The railing's outward side is the side of the offset (left for negative).</summary>
    public double Offset { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? BeginStation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? EndStation { get; set; }
    /// <summary>Without a deck result (tied arch): the run begins this far beyond the first support line (normal to it, away from the span).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double BeginLimitOffset { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EndLimitOffset { get; set; }
    /// <summary>Gap kept free at each run end (along the reference line).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EndClearance { get; set; }
    /// <summary>R (outward from the reference line) where the base elevation is probed on the concrete (e.g. the middle of a curb).</summary>
    public double BaseProbe { get; set; }
    /// <summary>Dips of the base narrower than this along the run (e.g. a curb notched around steel) are bridged at the lower rim and reported. 0 = a third of the post spacing, negative = off.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double BaseBridgeGap { get; set; }
    public string Material { get; set; } = "Stainless steel";
    /// <summary>Density of the material, kg/m3 (stainless steel about 7,980).</summary>
    public double Density { get; set; } = 7980;
    /// <summary>Sweep step along the run (the reference line is sampled at least this densely).</summary>
    public double SampleStep { get; set; } = Ft(1);

    /// <summary>Railing section swept along the whole run as one placeholder solid (null = none).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RailPoint>? Envelope { get; set; }
    /// <summary>Envelope mass per length (kg/m), e.g. the plans' design dead load of the railing; 0 = volume x density.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EnvelopeMassPerLength { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EnvelopeUnresolved { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EnvelopeBasis { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RailPostDefinition? Post { get; set; }
    public List<RailMemberDefinition> Members { get; set; } = new();
    /// <summary>See-through fence fabric between the posts (null = none).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RailFabricDefinition? Fabric { get; set; }

    /// <summary>OPTIONAL: the plans show the posts / handrail / fence (only sizes or the fabric type are open): their UNRESOLVED flags are
    /// <c>Assumption.DimensionOnly</c> (the parts stay on their normal viewport layers). The envelope flag stays a plain UNRESOLVED.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FlagsDimensionOnly { get; set; }

    /// <summary>Plan length of the run along the reference line, for the check (null = none).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PlanLength { get; set; }
    public string Source { get; set; } = "";
    /// <summary>Plan items not modeled (each raised as a warning, text built with <c>UnresolvedFlag.Text</c>).</summary>
    public List<string> Unresolved { get; set; } = new();

    private static readonly JsonSerializerOptions CloneJson = new() { Converters = { new JsonStringEnumConverter() } };
    public PedestrianRailingDefinition Clone() => JsonSerializer.Deserialize<PedestrianRailingDefinition>(JsonSerializer.Serialize(this, CloneJson), CloneJson)!;
}
