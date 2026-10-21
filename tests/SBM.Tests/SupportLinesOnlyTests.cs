using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Project;
using Xunit;

namespace SBM.Tests;

public class SupportLinesOnlyTests
{
    private static BridgeDefinition Lines(bool flag)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var d = b.Clone();
        d.Name = "Lines";
        d.Girders.Count = 0;
        d.Girders.Spacings = new();
        d.SupportLinesOnly = flag;
        return d;
    }

    [Fact]
    public void WithoutTheFlag_NoGirdersIsAnError()
    {
        Assert.Contains(Lines(false).Validate(), e => e.Contains("girder"));
        Assert.DoesNotContain(Lines(true).Validate(), e => e.Contains("girder"));
    }

    [Fact]
    public void SupportLinesOnly_BuildsSupportLinesAndNothingElse()
    {
        var p = SampleProject.Create();
        var d = Lines(true);
        var f = FramingGenerator.Generate(d, p.FindAlignment(d.AlignmentName)!);
        Assert.False(f.HasErrors);
        Assert.Equal(d.Supports.Count, f.Supports.Count);
        Assert.Empty(f.Girders);
        Assert.Null(f.Deck);
        Assert.All(f.Supports, s => Assert.Empty(s.Bearings));
    }

    [Fact]
    public void PlanSubstructure_TimberFenderSolid_NotConcrete_NoExtents_IfcFender()
    {
        var p = SampleProject.Create();
        var d = Lines(true);
        p.Bridges.Clear();
        p.Bridges.Add(d);
        SBM.Core.Bridge.FootingPoint P(double s, double n) => new() { S = s, N = n };
        var sd = new SubstructureDefinition { Enabled = true, Plan = new PlanSubstructureDefinition() };
        sd.Reinforcement.Enabled = false;
        var box = new List<SBM.Core.Bridge.FootingPoint> { P(-2, -1), P(2, -1), P(2, 1), P(-2, 1) };
        sd.Plan.Solids.Add(new PlanSolidDefinition { Name = "footing", Kind = SBM.Core.Substructure.SubElementKind.Footing, Sections = { new(0, box), new(1, box) } });
        var wale = new List<SBM.Core.Bridge.FootingPoint> { P(-10, 3), P(10, 3), P(10, 3.3), P(-10, 3.3) };
        sd.Plan.Solids.Add(new PlanSolidDefinition { Name = "fender wale", Kind = SBM.Core.Substructure.SubElementKind.Fender, Material = "Timber (treated Southern Yellow Pine)", Density = 600,
            Sections = { new(0.5, wale), new(0.8, wale) }, Flag = "UNRESOLVED (p.0): wale position scaled." });
        d.Supports[0].Substructure = sd;
        Assert.DoesNotContain("\"Material\"", System.Text.Json.JsonSerializer.Serialize(new PlanSolidDefinition()));
        var bm = Assert.Single(new RegenerationCache().Regenerate(p))!;
        var unit = bm.Substructure!.Units.Single(u => u.Name == d.Supports[0].Name);
        var f = unit.Solids.Single(s => s.Kind == SBM.Core.Substructure.SubElementKind.Fender);
        Assert.False(f.IsConcrete);
        Assert.Equal(600, f.Density);
        Assert.Equal(20 * 0.3 * 0.3, Math.Abs(f.Mesh.Volume()), 6);
        Assert.True(unit.Solids.Single(s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing).IsConcrete);
        Assert.Equal(-2, unit.CapS0, 6);        // the fender does not widen the unit's plan extents
        Assert.Equal(2, unit.CapS1, 6);
        var sw = new StringWriter();
        SBM.IO.Ifc.IfcExporter.Export(sw, "f.ifc", p, new[] { new SBM.IO.Ifc.IfcBridgeData(bm.Framing, bm.Steel, bm.Concrete, bm.Substructure) });
        string ifc = sw.ToString();
        Assert.Contains("IFCIMPACTPROTECTIONDEVICE(", ifc);
        Assert.Contains(".FENDER.", ifc);
        Assert.Contains("Timber (treated Southern Yellow Pine)", ifc);
        var back = BridgeProject.FromJson(p.ToJson());
        Assert.Equal("Timber (treated Southern Yellow Pine)", back.Bridges[0].Supports[0].Substructure!.Plan!.Solids[1].Material);
    }

    [Fact]
    public void Flag_IsOmittedFromTheFileWhenFalse_AndRoundTripsWhenTrue()
    {
        var p = SampleProject.Create();
        Assert.DoesNotContain("upportLinesOnly", p.ToJson());
        var d = Lines(true);
        p.Bridges.Add(d);
        var back = BridgeProject.FromJson(p.ToJson());
        Assert.True(back.Bridges.First(b => b.Name == "Lines").SupportLinesOnly);
        Assert.False(back.Bridges[0].SupportLinesOnly);
    }
}
