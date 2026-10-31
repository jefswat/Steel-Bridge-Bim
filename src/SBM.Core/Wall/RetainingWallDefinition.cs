using System.Text.Json.Serialization;

namespace SBM.Core.Wall;

/// <summary>Which side of the wall control line the exposed face looks to (Civil 3D: offsets right-positive). The retained soil is on the other side.</summary>
public enum WallFaceSide { Left, Right }

/// <summary>A (station, elevation) row of a wall table (meters; stations on the wall's alignment).</summary>
public sealed class WallElevationPoint
{
    public double Station { get; set; }
    public double Elevation { get; set; }
    public WallElevationPoint() { }
    public WallElevationPoint(double station, double elevation) { Station = station; Elevation = elevation; }
}

/// <summary>Shear key under a cantilever wall footing.</summary>
public sealed class WallKeyDefinition
{
    public double Width { get; set; } = double.NaN;
    public double Depth { get; set; } = double.NaN;
    /// <summary>Distance from the toe (front edge of the footing) to the front face of the key.</summary>
    public double FromToe { get; set; } = double.NaN;
}

/// <summary>
/// A retaining wall (OPTIONAL <see cref="Project.BridgeProject.Walls"/>; design note docs/plan-data/roads-walls-utilities-lod-design.md section 3). Stage 1 =
/// cantilever cast-in-place wall: stem on a spread footing, both swept along the wall control line between joints. CONTROL LINE = the FRONT FACE of the
/// stem at the TOP of the wall, at <see cref="Offset"/> from the alignment. Every dimension comes from the plans: required values default to NaN and a
/// missing one means the wall is not built (warning, page + reason); nothing is defaulted. Generator <see cref="RetainingWallGenerator"/>.
/// </summary>
public sealed class RetainingWallDefinition
{
    public string Name { get; set; } = "";
    public string AlignmentName { get; set; } = "";
    /// <summary>Offset of the control line from the alignment (meters, right +).</summary>
    public double Offset { get; set; }
    public double BeginStation { get; set; }
    public double EndStation { get; set; }
    /// <summary>Side the exposed face looks to; null = UNRESOLVED (not built).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WallFaceSide? FaceSide { get; set; }
    /// <summary>Top of wall at the control line, linear between rows (held beyond the first / last row).</summary>
    public List<WallElevationPoint> TopOfWall { get; set; } = new();
    /// <summary>Top of footing as STEPS: each row = the level from its station to the next row's station (vertical steps); the first row also covers the wall
    /// before it. Used when given; else <see cref="FootingTopProfile"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WallElevationPoint>? FootingTopSteps { get; set; }
    /// <summary>Top of footing linear between rows (a sloping footing).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WallElevationPoint>? FootingTopProfile { get; set; }
    /// <summary>Stem thickness at the top of wall.</summary>
    public double StemTopThickness { get; set; } = double.NaN;
    /// <summary>Front / back face batter: horizontal per unit height (0 = plumb; the front face leans back toward the soil going up, the back face widens the stem going down).</summary>
    public double FrontBatter { get; set; } = double.NaN;
    public double BackBatter { get; set; } = double.NaN;
    /// <summary>Footing: thickness, toe (front face of the stem at the footing top to the front edge), heel (back face of the stem at the footing top to the back edge).</summary>
    public double FootingThickness { get; set; } = double.NaN;
    public double Toe { get; set; } = double.NaN;
    public double Heel { get; set; } = double.NaN;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WallKeyDefinition? Key { get; set; }
    /// <summary>Expansion / contraction joint stations: stem and footing are separate panels between them.</summary>
    public List<double> JointStations { get; set; } = new();
    /// <summary>Largest station spacing of the swept sections (meters); table stations, steps and joints are always included.</summary>
    public double SampleSpacing { get; set; } = 2.0;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
    public List<string> Unresolved { get; set; } = new();
    /// <summary>OPTIONAL: the wall is an MSE wall (facing, leveling pad, coping, reinforced zone; <see cref="MseWallDefinition"/>). The cantilever stem /
    /// footing inputs are then not read; the top of footing tables give the TOP OF THE LEVELING PAD. Omitted from the .sbm when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MseWallDefinition? Mse { get; set; }
}
