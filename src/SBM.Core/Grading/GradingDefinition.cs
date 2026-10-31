using System.Text.Json.Serialization;
using SBM.Core.Road;

namespace SBM.Core.Grading;

/// <summary>Side of a grading feature line the slope is projected to: Left / Right of the point order, or Outside of a closed line (pad).</summary>
public enum GradingSide { Left, Right, Outside }

/// <summary>Part of the graded surface a protection layer covers.</summary>
public enum GradingProtectionRegion { Slope, Top }

/// <summary>A feature line point: station / offset on the grading's alignment, or X / Y (project meters) when it has none; elevation always given.</summary>
public sealed class GradingPoint
{
    public double Station { get; set; } = double.NaN;
    public double Offset { get; set; } = double.NaN;
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double Elevation { get; set; } = double.NaN;
    public GradingPoint() { }
    /// <summary>Station / offset / elevation (alignment based).</summary>
    public static GradingPoint At(double station, double offset, double elevation) => new() { Station = station, Offset = offset, Elevation = elevation };
    /// <summary>X / Y / elevation (project coordinates).</summary>
    public static GradingPoint Xy(double x, double y, double elevation) => new() { X = x, Y = y, Elevation = elevation };
}

/// <summary>Slope protection (riprap, slope paving ...) on a region of the graded surface; its TOP is the finished grade, Thickness measured normal to the surface.</summary>
public sealed class GradingProtectionDefinition
{
    public string Name { get; set; } = "";
    /// <summary>Plan name of the protection, e.g. "Riprap Class 2", "Concrete slope paving".</summary>
    public string Kind { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GradingProtectionRegion? Region { get; set; }
    public double Thickness { get; set; } = double.NaN;
    public string Material { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>
/// A grading feature (OPTIONAL <see cref="Project.BridgeProject.Gradings"/>; design note docs/plan-data/roads-walls-utilities-lod-design.md section 3b): a 3D feature line
/// (open = berm / bench / toe line, closed = pad whose interior is the flat-triangulated top) with an OPTIONAL slope projected to the existing ground at the
/// plan's cut / fill ratios (<see cref="RoadSideSlope"/>), and OPTIONAL slope protection layers. Every value comes from the plans: a missing elevation, side or
/// ratio is an UNRESOLVED flag (page + reason) and that part is not built. Generator <see cref="GradingGenerator"/>.
/// </summary>
public sealed class GradingDefinition
{
    public string Name { get; set; } = "";
    /// <summary>Alignment of station / offset points; "" = the points are X / Y.</summary>
    public string AlignmentName { get; set; } = "";
    public List<GradingPoint> Points { get; set; } = new();
    /// <summary>The line closes back to its first point (a pad).</summary>
    public bool Closed { get; set; }
    /// <summary>Side the slope is projected to; null with a slope = UNRESOLVED.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GradingSide? Side { get; set; }
    /// <summary>OPTIONAL slope to the existing ground (cut / fill ratio = horizontal per vertical, MaxWidth = horizontal search limit).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoadSideSlope? Slope { get; set; }
    /// <summary>Largest spacing of the slope rays along the line (meters); corners on the slope side get fans of rays.</summary>
    public double SampleSpacing { get; set; } = 1.0;
    public List<GradingProtectionDefinition> Protection { get; set; } = new();
    public string Source { get; set; } = "";
    public List<string> Unresolved { get; set; } = new();
}
