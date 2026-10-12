using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Segmental;

/// <summary>One generated segment / closure joint / end segment of the box.</summary>
public sealed class SegmentResult
{
    public required string Name { get; init; }
    public required SegmentType Type { get; init; }
    public required string Span { get; init; }
    /// <summary>Cantilever bent (support name) or "".</summary>
    public required string Bent { get; init; }
    public required int Index { get; init; }
    public required double Station0 { get; init; }
    public required double Station1 { get; init; }
    /// <summary>Box depth (normal to the top) at the start / end joint.</summary>
    public required double Depth0 { get; init; }
    public required double Depth1 { get; init; }
    /// <summary>Top CL points (WP on the profile grade) at the two joints; the segment is the straight chord between them.</summary>
    public required Vec3 Top0 { get; init; }
    public required Vec3 Top1 { get; init; }
    /// <summary>The solid: one closed mesh; <see cref="Pieces"/> = its closed parts when it has several (hollow part + diaphragm).</summary>
    public required Mesh Mesh { get; init; }
    public List<Mesh> Pieces { get; } = new();
    public Assumption? Assumption { get; set; }
    /// <summary>Length along the 3D chord between the joint WPs.</summary>
    public double ChordLength => (Top1 - Top0).Length;
    /// <summary>Horizontal length along the alignment (stations).</summary>
    public double AlignmentLength => Station1 - Station0;
    public double Volume => Math.Abs(Mesh.Volume());
    public string Description { get; init; } = "";
}

/// <summary>One tendon (a schedule designation of Count tendons gives Count results, suffix L / R).</summary>
public sealed class TendonResult
{
    public required string Name { get; init; }
    public required TendonGroup Group { get; init; }
    public required string Location { get; init; }
    public required int Strands { get; init; }
    /// <summary>Centerline (straight runs; the plans give no profile coordinates).</summary>
    public required List<Vec3> Points { get; init; }
    public double PlanLength { get; init; }
    public double PlanWeightLb { get; init; }
    public double JackingForceKip { get; init; }
    public string StressingEnd { get; init; } = "";
    public string Anchors { get; init; } = "";
    /// <summary>Owning segment (transverse tendons) or cantilever bent ("") - for grouping.</summary>
    public string Owner { get; init; } = "";
    public Assumption? Assumption { get; set; }
    public double Length { get { double l = 0; for (int i = 1; i < Points.Count; i++) l += (Points[i] - Points[i - 1]).Length; return l; } }
}

public sealed class SegmentalBearingResult
{
    public required string Name { get; init; }
    public required string Bent { get; init; }
    public required string Type { get; init; }
    /// <summary>Bearing top centre (under the box soffit), bottom = top - height.</summary>
    public required Vec3 Top { get; init; }
    public required double Height { get; init; }
    /// <summary>Box soffit elevation above the CL bearing (vertical line through the bearing).</summary>
    public required double SoffitZ { get; init; }
    public double PedestalTop { get; init; } = double.NaN;
    /// <summary>Body solid (null when the plan size is not given).</summary>
    public Mesh? Mesh { get; init; }
    public Assumption? Assumption { get; set; }
    public string Description { get; init; } = "";
}

/// <summary>Plan-vs-model check row (also for the check script).</summary>
public sealed record SegmentalCheck(string Item, double Model, double Plan, double Difference, string Note);

public sealed class SegmentalModel
{
    public required SegmentalDefinition Definition { get; init; }
    public List<SegmentResult> Segments { get; } = new();
    public List<TendonResult> Tendons { get; } = new();
    public List<SegmentalBearingResult> Bearings { get; } = new();
    public Substructure.SubstructureModel? Substructure { get; set; }
    public List<SegmentalCheck> Checks { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public double ConcreteVolume => Segments.Sum(s => s.Volume);
    /// <summary>Strand weight (lb) from the model lengths x strands x the plan weight per foot.</summary>
    public double StrandWeightLb(Func<TendonResult, bool> which) =>
        Tendons.Where(which).Sum(t => t.Strands * t.Length / 0.3048 * Definition.StrandWeightLbPerFt);
}
