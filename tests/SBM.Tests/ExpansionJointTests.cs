using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class ExpansionJointTests
{
    private static (BridgeFraming f, ConcreteModel c) Build(Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        tweak?.Invoke(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        Assert.False(f.HasErrors);
        return (f, ConcreteGenerator.Generate(f, p.Units));
    }

    private static void AddJoints(BridgeDefinition b, double depth = 0)
    {
        foreach (var s in new[] { b.Supports[0], b.Supports[^1] })
        {
            s.ExpansionJoint = new ExpansionJointDefinition { Type = ExpansionJointType.Modular, BlockoutWidth = In(25), GapAt50F = In(9) };
            if (depth > 0) s.ExpansionJoint.BlockoutDepth = depth;
        }
    }

    [Theory]
    [InlineData(0.0)]          // 14" deeper than the slab: blockout limited to the slab, a warning is given
    [InlineData(0.1)]          // shallower than the slab: the deck keeps a shelf
    public void Blockout_PlusCutBackDeck_EqualsOriginalDeck(double depth)
    {
        var (_, plain) = Build();
        var (f, c) = Build(b => AddJoints(b, depth));
        Assert.Equal(2, c.Joints.Count);
        double blockouts = c.Joints.Sum(j => j.BlockoutVolume);
        Assert.True(blockouts > 0);
        Assert.Equal(plain.DeckVolume, c.DeckVolume + blockouts, 2);
        if (depth == 0) Assert.Contains(c.Diagnostics, d => d.Message.Contains("exceeds the modeled slab"));
    }

    [Fact]
    public void Body_FillsTheGap_AndOffByDefault()
    {
        var (_, none) = Build();
        Assert.Empty(none.Joints);
        var (f, c) = Build(b => AddJoints(b));
        var j = c.Joints[0];
        Assert.NotNull(j.Body);
        Assert.True(j.Length > 5);
        // body volume ~ gap x length x (slab - set-down)
        double slab = f.Definition.Deck.Thickness;
        Assert.InRange(j.BodyVolume, 0.5 * In(9) * j.Length * slab, 1.1 * In(9) * j.Length * slab);
        var b2 = Build(b => { AddJoints(b); b.Supports[^1].ExpansionJoint!.Body = false; });
        Assert.Null(b2.c.Joints.Single(x => !x.AtBegin).Body);
    }

    [Fact]
    public void Ifc_HasPlaceholderAccessory_AndBlockoutParts()
    {
        var p = SampleProject.Create();
        AddJoints(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var s = SteelModelGenerator.Generate(f, p.Units);
        var c = ConcreteGenerator.Generate(f, p.Units);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "t.ifc", p, new[] { new IfcBridgeData(f, s, c) });
        string ifc = sw.ToString();
        Assert.Contains("IFCDISCRETEACCESSORY(", ifc);
        Assert.Contains(".EXPANSION_JOINT_DEVICE.", ifc);
        Assert.Contains("manufacturer-designed placeholder", ifc);
        Assert.Contains("'SBM_Placeholder'", ifc);
        Assert.Contains("IFCBUILDINGELEMENTPART(", ifc);
        Assert.Contains("JOINT_BLOCKOUT_CONCRETE", ifc);
    }
}
