using System.Text.Json.Serialization;

namespace SBM.Core.Wall;

/// <summary>Where an MSE reinforcement length is measured from.</summary>
public enum MseLengthOrigin { BackOfFacing, FrontOfFacing }

/// <summary>Soil reinforcement kind (supplier drawings).</summary>
public enum MseReinforcementKind { Strip, Grid }

/// <summary>A point of a wall cross section: U from the control line toward the back (retained side), Z above the top of wall.</summary>
public sealed class WallSectionPoint
{
    public double U { get; set; }
    public double Z { get; set; }
    public WallSectionPoint() { }
    public WallSectionPoint(double u, double z) { U = u; Z = z; }
}

/// <summary>Reinforcement length between two stations of the wall alignment.</summary>
public sealed class MseLengthByStation
{
    public double FromStation { get; set; }
    public double ToStation { get; set; }
    public double Length { get; set; } = double.NaN;
}

/// <summary>Reinforcement length for design heights (top of wall - top of leveling pad) up to <see cref="UpToHeight"/>; the first row that covers the height wins.</summary>
public sealed class MseLengthByHeight
{
    public double UpToHeight { get; set; } = double.NaN;
    public double Length { get; set; } = double.NaN;
}

/// <summary>
/// Individual soil reinforcements (OPTIONAL; only from the wall SUPPLIER's drawings, user answer 7a.4). Built at LOD 400 (group Reinforcing, the wall name
/// as the bridge name); below it only the reinforced-zone envelope. Lengths = the zone lengths. Every value is required (NaN = not built, UNRESOLVED).
/// </summary>
public sealed class MseReinforcementDefinition
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MseReinforcementKind? Kind { get; set; }
    /// <summary>Levels above the top of the leveling pad (meters); a level at or above the top of wall at a station is not built there.</summary>
    public List<double> Levels { get; set; } = new();
    /// <summary>Strips: center-to-center spacing along the wall and the first strip's distance from the begin station. Grids: not read.</summary>
    public double Spacing { get; set; } = double.NaN;
    public double FirstFromBegin { get; set; } = double.NaN;
    /// <summary>Strip width (grids: not read) and thickness (grids: envelope thickness of the grid sheet).</summary>
    public double Width { get; set; } = double.NaN;
    public double Thickness { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>
/// MSE wall (OPTIONAL <see cref="RetainingWallDefinition.Mse"/>; design note section 3.2, user answer 7a.4 = CONTRACT PLANS ENVELOPE by default): facing panels
/// (an envelope per joint panel, or a rectangular panel grid when the panel size is given), leveling pad, coping and the reinforced-zone envelope. The control
/// line = FRONT FACE of the facing at the top of wall. Nothing is defaulted: a missing value leaves that component unbuilt with an UNRESOLVED flag (page + reason).
/// Reinforcement lengths come only from the length tables, never from a height ratio.
/// </summary>
public sealed class MseWallDefinition
{
    /// <summary>Facing thickness and front batter (horizontal per unit height; 0 = plumb). Required for the facing and the zone.</summary>
    public double FacingThickness { get; set; } = double.NaN;
    public double FacingBatter { get; set; } = double.NaN;
    /// <summary>OPTIONAL panel grid (both given): panels PanelWidth wide from the begin station, courses PanelHeight high from the leveling pad top; the
    /// top course is cut to the top of wall. Joint widths and the supplier's stagger pattern are not modeled (Info).</summary>
    public double PanelWidth { get; set; } = double.NaN;
    public double PanelHeight { get; set; } = double.NaN;
    /// <summary>Leveling pad: width, thickness, and how far its front edge lies in front of the facing front face at the pad top.</summary>
    public double LevelingPadWidth { get; set; } = double.NaN;
    public double LevelingPadThickness { get; set; } = double.NaN;
    public double LevelingPadFrontProjection { get; set; } = double.NaN;
    /// <summary>OPTIONAL coping section (closed polygon, U from the control line toward the back, Z above the top of wall); null = no coping.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WallSectionPoint>? Coping { get; set; }
    /// <summary>Reinforcement lengths: by station ranges (wins where a row covers the station) or by design height.</summary>
    public List<MseLengthByStation> LengthsByStation { get; set; } = new();
    public List<MseLengthByHeight> LengthsByHeight { get; set; } = new();
    /// <summary>Where the lengths are measured from; null = UNRESOLVED (the zone is built from the back of the facing and flagged).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MseLengthOrigin? LengthFrom { get; set; }
    /// <summary>OPTIONAL individual reinforcements (supplier drawings).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MseReinforcementDefinition? Reinforcement { get; set; }
    public string FacingMaterial { get; set; } = "";
    /// <summary>OPTIONAL abutments on / behind the wall (pile sleeves, facing clearance; <see cref="MseAbutmentCheck"/>); null = none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<MseAbutmentInterface>? Abutments { get; set; }
    public string Source { get; set; } = "";
}
