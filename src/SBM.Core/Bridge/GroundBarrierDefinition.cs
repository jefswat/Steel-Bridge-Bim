using System.Text.Json.Serialization;

namespace SBM.Core.Bridge;

/// <summary>Vertex of a ground barrier path in the pier frame (S along the support line from the alignment point, right +; N normal, up-station +) with the
/// barrier height there (linear between vertices).</summary>
public sealed class GroundBarrierVertex
{
    public double S { get; set; }
    public double N { get; set; }
    public double Height { get; set; }
    public GroundBarrierVertex() { }
    public GroundBarrierVertex(double s, double n, double height) { S = s; N = n; Height = height; }
}

/// <summary>
/// Concrete traffic barrier standing on the ground / pavement near a pier (OPTIONAL <see cref="SubstructureDefinition.GroundBarriers"/>, e.g. a TxDOT T80SS
/// barrier protecting a pier column in a highway median). The section <see cref="Profile"/> (R = distance from the BACK face toward the traffic face,
/// H = height above the base; full height <see cref="FullHeight"/>) is swept along <see cref="Path"/> (the back face at the base), the traffic face on the
/// <see cref="TrafficSide"/> of the path direction (+1 right, -1 left). Where a vertex is lower than the full height (an end taper) the profile points above
/// <see cref="StretchAt"/> move down by the difference (the top shape is kept, the stretch zone shortens). Base at <see cref="BaseElevation"/> or
/// <see cref="BaseAboveFoundation"/> above the foundation top. Optional replacement pavement strip under the barrier from R <see cref="PavementR0"/>,
/// <see cref="PavementWidth"/> wide, <see cref="PavementThickness"/> deep, along the whole path. Solids SubElementKind.TrafficBarrier (IFC IfcRailing
/// GUARDRAIL) / PavementStrip (IfcPavement RIGID). Lengths in meters.
/// </summary>
public sealed class GroundBarrierDefinition
{
    public string Name { get; set; } = "";
    /// <summary>Barrier type as the plans name it, e.g. "T80SS(MOD)".</summary>
    public string TypeName { get; set; } = "";
    public List<RailPoint> Profile { get; set; } = new();
    public double FullHeight { get; set; }
    public double StretchAt { get; set; }
    public List<GroundBarrierVertex> Path { get; set; } = new();
    public int TrafficSide { get; set; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? BaseElevation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? BaseAboveFoundation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PavementR0 { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PavementWidth { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PavementThickness { get; set; }
    /// <summary>Plan length (pay LF) for the check (null = none).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PlanLength { get; set; }
    public string Source { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unresolved { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Basis { get; set; }
    /// <summary>OPTIONAL separate flag for the pavement strip (null = the barrier's).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PavementUnresolved { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PavementBasis { get; set; }

    public GroundBarrierDefinition Clone()
    {
        var c = (GroundBarrierDefinition)MemberwiseClone();
        c.Profile = Profile.Select(p => new RailPoint(p.R, p.H)).ToList();
        c.Path = Path.Select(v => new GroundBarrierVertex(v.S, v.N, v.Height)).ToList();
        return c;
    }
}
