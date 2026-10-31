using Xunit;

namespace SBM.Tests;

/// <summary>Test classes that call the native openNURBS library (rhino3dm) run one after another: xUnit runs classes in parallel, and two
/// 3DM writes / B-rep builds at once intermittently failed (Rhino3dmTests.ExactCurvedGeometry_WritesClosedNurbsBreps,
/// RhinoBarPipeTests.SampleExport_BarSolids_AreClosedPipes and DeckSolidsTests failed only in the full parallel run and passed alone).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RhinoNativeCollection
{
    public const string Name = "Rhino3dm native";
}
