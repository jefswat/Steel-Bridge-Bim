using System.Text.Json.Serialization;

namespace SBM.Core.Utility;

/// <summary>Utility system of a network (IFC IfcDistributionSystemEnum; viewport colour per APWA).</summary>
public enum UtilitySystem { Storm, Sanitary, Water, Gas, Electrical, Telecom, Lighting, Other }

/// <summary>Kind of a network structure (node).</summary>
/// <summary>Kind of a network structure (node). <see cref="Valve"/> / <see cref="Hydrant"/> are in-line items built as an envelope (no inside): their
/// size fields give the OUTSIDE (diameter or length x width), rim = top, sump = bottom; walls / floor are not used.</summary>
public enum UtilityStructureKind { Manhole, Inlet, CatchBasin, JunctionBox, Headwall, Cleanout, Outfall, Other, Valve, Hydrant, Handhole, Vault }

/// <summary>Plan shape of a structure: round (inner diameter) or box (inner length x width).</summary>
public enum UtilityStructureShape { Round, Box }

/// <summary>Section of a pipe: circular (inner diameter), elliptical (inner rise x span) or box (inner rise x span).</summary>
public enum UtilityPipeShape { Circular, Elliptical, Box }

/// <summary>
/// A structure (node) of a utility network: manhole, inlet, catch basin, junction box, headwall... Location by X / Y (project coordinates) or by
/// station / offset on an alignment. Every size and elevation is a plan value; NaN = not given. A missing rim is taken from the FINISHED GROUND at the
/// point (roads, then grading, then the existing ground; reported); any other missing value = UNRESOLVED and the structure solid is not built (its
/// pipes still are, ending at the structure centre).
/// </summary>
public sealed class UtilityStructureDefinition
{
    public string Name { get; set; } = "";
    public UtilityStructureKind Kind { get; set; } = UtilityStructureKind.Manhole;
    /// <summary>Free text (catalog part, e.g. "Concentric cylindrical structure 48 in").</summary>
    public string Description { get; set; } = "";
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    /// <summary>OPTIONAL location on an alignment (used when X / Y are not given): station and offset (right +).</summary>
    public string AlignmentName { get; set; } = "";
    public double Station { get; set; } = double.NaN;
    public double Offset { get; set; } = double.NaN;
    /// <summary>Top of the structure (rim / top of grate, m); NaN = the finished ground at the point.</summary>
    public double RimElevation { get; set; } = double.NaN;
    /// <summary>Inside floor (sump) elevation, m.</summary>
    public double SumpElevation { get; set; } = double.NaN;
    public UtilityStructureShape Shape { get; set; } = UtilityStructureShape.Round;
    /// <summary>Round: inside diameter (m).</summary>
    public double InnerDiameter { get; set; } = double.NaN;
    /// <summary>Box: inside length (along <see cref="RotationDegrees"/>) and width (m).</summary>
    public double InnerLength { get; set; } = double.NaN;
    public double InnerWidth { get; set; } = double.NaN;
    /// <summary>Box: plan direction of the length, degrees counter-clockwise from project +X; null = along the outlet pipe (flagged as an assumption).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RotationDegrees { get; set; }
    public double WallThickness { get; set; } = double.NaN;
    public double FloorThickness { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Plan items not dimensioned (UNRESOLVED flag text); raised as a warning.</summary>
    public string Unresolved { get; set; } = "";
    /// <summary>OPTIONAL cone (round structures): reducer from the barrel to <see cref="UtilityConeDefinition.TopInnerDiameter"/>; null = the walls run to the top.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UtilityConeDefinition? Cone { get; set; }
    /// <summary>OPTIONAL frame and cover / grate on top (its top = the rim); the walls / cone then stop at the frame bottom. Null = none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UtilityFrameDefinition? Frame { get; set; }

    /// <summary>In-line item (valve, hydrant): an envelope solid, no inside.</summary>
    [JsonIgnore] public bool IsEnvelope => Kind is UtilityStructureKind.Valve or UtilityStructureKind.Hydrant;
}

/// <summary>Cone (eccentric or concentric reducer) at the top of a round structure: from the barrel inside diameter to the top opening over <see cref="Height"/>.</summary>
public sealed class UtilityConeDefinition
{
    /// <summary>Cone height (barrel top to cone top), m; NaN = not given -> UNRESOLVED, the cone is not built (walls to the top).</summary>
    public double Height { get; set; } = double.NaN;
    /// <summary>Inside diameter at the cone top (the access opening), m.</summary>
    public double TopInnerDiameter { get; set; } = double.NaN;
    /// <summary>Eccentric: one side of the cone stays vertical (in line with the barrel wall at <see cref="EccentricDegrees"/>); false = concentric.</summary>
    public bool Eccentric { get; set; }
    /// <summary>Eccentric: plan direction of the vertical side (degrees counter-clockwise from +X); null = toward the outlet pipe (flagged).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? EccentricDegrees { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>Cast frame with a cover (manhole) or a grate (inlet) on top of a structure. Every value from the plans / the casting catalog; NaN = not given.</summary>
public sealed class UtilityFrameDefinition
{
    /// <summary>Grate (inlet) or cover (manhole).</summary>
    public bool Grate { get; set; }
    /// <summary>Catalog designation (e.g. "R-1642").</summary>
    public string Designation { get; set; } = "";
    /// <summary>Frame height (bottom of the frame flange to the rim), m.</summary>
    public double Height { get; set; } = double.NaN;
    /// <summary>Round frame: outside diameter of the flange; box frame: outside length x width (along the structure's rotation), m.</summary>
    public double OuterDiameter { get; set; } = double.NaN;
    public double OuterLength { get; set; } = double.NaN;
    public double OuterWidth { get; set; } = double.NaN;
    /// <summary>Clear opening of the frame (round: diameter; box: length x width), m.</summary>
    public double OpeningDiameter { get; set; } = double.NaN;
    public double OpeningLength { get; set; } = double.NaN;
    public double OpeningWidth { get; set; } = double.NaN;
    /// <summary>Cover / grate thickness (its top = the rim), m; NaN = the cover / grate is not built (frame only, flagged). Grate bars / openings are not modeled.</summary>
    public double CoverThickness { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>
/// Hangers of a pipe run hung under a bridge (OPTIONAL <see cref="UtilityPipeDefinition.Hangers"/>; user answer 7a.5: hanger inputs are always optional and the
/// source page - bridge plans or utility plans - is recorded). A rod from the pipe's outside crown straight up to the lowest face of the bridge above.
/// </summary>
public sealed class UtilityHangerDefinition
{
    /// <summary>The bridge the run hangs from (name).</summary>
    public string Bridge { get; set; } = "";
    /// <summary>Maximum hanger spacing along the pipe (plan), m; NaN = not given -> UNRESOLVED, no hangers built.</summary>
    public double Spacing { get; set; } = double.NaN;
    /// <summary>First hanger from the pipe start (plan), m, then every <see cref="Spacing"/>; NaN = equal spaces not over <see cref="Spacing"/> with the end hangers half a
    /// space from the pipe ends (flagged as a layout assumption).</summary>
    public double FirstFromStart { get; set; } = double.NaN;
    /// <summary>Rod diameter, m; NaN = not given -> UNRESOLVED, the rods are not built (their positions are still reported).</summary>
    public double RodDiameter { get; set; } = double.NaN;
    /// <summary>Hanger type text (clevis, trapeze, roller ...).</summary>
    public string Type { get; set; } = "";
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
}

/// <summary>
/// Duct bank (OPTIONAL <see cref="UtilityNetworkDefinition.DuctBanks"/>): a concrete encasement with rows x columns of conduits, straight between two structures
/// (inside wall faces) or free ends. Bottom of encasement elevations at the ends; every size from the plans (NaN = not given -> UNRESOLVED, not built).
/// </summary>
public sealed class UtilityDuctBankDefinition
{
    public string Name { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public double StartX { get; set; } = double.NaN;
    public double StartY { get; set; } = double.NaN;
    public double EndX { get; set; } = double.NaN;
    public double EndY { get; set; } = double.NaN;
    /// <summary>Bottom of the encasement at the start / end, m.</summary>
    public double BottomStart { get; set; } = double.NaN;
    public double BottomEnd { get; set; } = double.NaN;
    /// <summary>Encasement width x height (section normal to the run), m.</summary>
    public double Width { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    public int Rows { get; set; }
    public int Columns { get; set; }
    /// <summary>Conduit inside diameter and wall thickness, m.</summary>
    public double ConduitDiameter { get; set; } = double.NaN;
    public double ConduitWall { get; set; } = double.NaN;
    /// <summary>Centre-to-centre spacing of the conduits across (columns) and up (rows), m.</summary>
    public double HorizontalSpacing { get; set; } = double.NaN;
    public double VerticalSpacing { get; set; } = double.NaN;
    /// <summary>Centre of the bottom-left conduit from the encasement's left face / bottom, m; NaN = the conduit group is centred in the encasement (flagged).</summary>
    public double FirstConduitFromLeft { get; set; } = double.NaN;
    public double FirstConduitFromBottom { get; set; } = double.NaN;
    public string ConduitMaterial { get; set; } = "";
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
}

/// <summary>
/// A pipe (link) of a network from structure <see cref="From"/> to <see cref="To"/> (the flow direction), or from / to a free end point
/// (<see cref="StartX"/> ... when the name is empty). Inverts are at the pipe ends (the inside face of the structure wall, or the free end).
/// Every size / invert is a plan value; NaN = not given -> UNRESOLVED, the pipe is not built.
/// </summary>
public sealed class UtilityPipeDefinition
{
    public string Name { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public double StartX { get; set; } = double.NaN;
    public double StartY { get; set; } = double.NaN;
    public double EndX { get; set; } = double.NaN;
    public double EndY { get; set; } = double.NaN;
    public double InvertStart { get; set; } = double.NaN;
    public double InvertEnd { get; set; } = double.NaN;
    public UtilityPipeShape Shape { get; set; } = UtilityPipeShape.Circular;
    /// <summary>Circular: inside diameter (m).</summary>
    public double InnerDiameter { get; set; } = double.NaN;
    /// <summary>Elliptical / box: inside rise (height) and span (width), m.</summary>
    public double InnerRise { get; set; } = double.NaN;
    public double InnerSpan { get; set; } = double.NaN;
    public double WallThickness { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Description { get; set; } = "";
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
    /// <summary>OPTIONAL plan cover (finished ground to the outside crown, m) for runs given by depth instead of inverts (pressure mains, conduits): used only where
    /// an invert is not given, at that end, on the finished ground; NaN = not used.</summary>
    public double Cover { get; set; } = double.NaN;
    /// <summary>OPTIONAL hangers: the run hangs under a bridge (null = buried / not supported).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UtilityHangerDefinition? Hangers { get; set; }
}

/// <summary>
/// A utility network (OPTIONAL <see cref="Project.BridgeProject.Utilities"/>; design note docs/plan-data/roads-walls-utilities-lod-design.md section 4):
/// storm drainage first. Structures + pipes; checks (cover, slope, invert drops) are reported, never applied. Generator <see cref="UtilityGenerator"/>.
/// </summary>
public sealed class UtilityNetworkDefinition
{
    public string Name { get; set; } = "";
    public UtilitySystem System { get; set; } = UtilitySystem.Storm;
    public string Owner { get; set; } = "";
    public string Source { get; set; } = "";
    public List<UtilityStructureDefinition> Structures { get; set; } = new();
    public List<UtilityPipeDefinition> Pipes { get; set; } = new();
    /// <summary>OPTIONAL required minimum cover over the pipe crowns (outside top, m) from the plans / specification; NaN = not given (cover is reported only).</summary>
    public double MinCover { get; set; } = double.NaN;
    /// <summary>OPTIONAL required clear distance to structures (bridges, walls, roads) from the plans / owner standards, m; NaN = only overlaps are reported.</summary>
    public double MinClearance { get; set; } = double.NaN;
    /// <summary>OPTIONAL required separation to OTHER utility networks (e.g. water / sanitary), m; the larger of the two networks' values governs.</summary>
    public double MinSeparation { get; set; } = double.NaN;
    public List<string> Unresolved { get; set; } = new();
    /// <summary>OPTIONAL duct banks (encasement + conduits) of this network; null = none (omitted from the .sbm).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UtilityDuctBankDefinition>? DuctBanks { get; set; }
}
