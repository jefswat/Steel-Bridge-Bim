using SBM.Core.Bridge;
using SBM.Core.Checks;
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

    private static void AddBlocks(BridgeDefinition b, double depth, double flat)
    {
        AddJoints(b, Ft(1) + In(2));
        foreach (var sp in new[] { b.Supports[0], b.Supports[^1] })
            sp.ExpansionJoint!.EndBlock = new DeckEndBlockDefinition { Depth = depth, FlatLength = flat };
    }

    [Fact]
    public void EndBlock_OffByDefault_BlockoutKeepsSlabLimit()
    {
        var (_, c) = Build(b => AddJoints(b));
        Assert.All(c.Joints, j => Assert.Null(j.EndBlock));
        Assert.Contains(c.Diagnostics, d => d.Message.Contains("exceeds the modeled slab"));
    }

    [Fact]
    public void EndBlock_CutsBlockoutToPlanDepth_AndVolumeFollowsSection()
    {
        var (f1, c1) = Build(b => AddBlocks(b, Ft(2) + In(2), Ft(3) + In(4.5)));
        Assert.All(c1.Joints, j => Assert.NotNull(j.EndBlock));
        Assert.DoesNotContain(c1.Diagnostics, d => d.Message.Contains("exceeds the modeled slab"));
        var (_, c0) = Build(b => AddJoints(b));
        // blockout now 14" deep (it was limited to the 8" slab)
        double slab = f1.Definition.Deck.Thickness, bo = Ft(1) + In(2);
        Assert.InRange(c1.Joints[0].BlockoutVolume / c0.Joints[0].BlockoutVolume, 0.95 * bo / slab, 1.05 * bo / slab);
        // end block volume ~ section area x deck width; compare two depths (same width)
        var (_, c2) = Build(b => AddBlocks(b, Ft(1) + In(8), Ft(3) + In(4.5)));
        double w = In(25);
        double a1 = ShoelaceArea(Ft(2) + In(2), Ft(3) + In(4.5), slab, bo, w), a2 = ShoelaceArea(Ft(1) + In(8), Ft(3) + In(4.5), slab, bo, w);
        double r = c1.Joints[0].EndBlock!.Volume / c2.Joints[0].EndBlock!.Volume;
        Assert.InRange(r / (a1 / a2), 0.98, 1.02);
        Assert.True(c1.Joints[0].EndBlock!.Volume > 1.0);
        // blockout + cut-back deck + end block: the deck itself is unchanged by the block
        Assert.Equal(c0.DeckVolume, c1.DeckVolume, 3);
    }

    private static double ShoelaceArea(double depth, double flat, double slab, double bo, double w)
    {
        var pts = new List<(double x, double y)> { (0, bo), (0, depth), (flat, depth), (flat + (depth - slab), slab), (w, slab), (w, bo) };
        double a = 0;
        for (int i = 0; i < pts.Count; i++) { var p = pts[i]; var q = pts[(i + 1) % pts.Count]; a += p.x * q.y - q.x * p.y; }
        return Math.Abs(a) / 2;
    }

    [Fact]
    public void EndBlock_Probe_InsideOutside()
    {
        var (f, c) = Build(b => AddBlocks(b, Ft(2) + In(2), Ft(3) + In(4.5)));
        var blk = c.Joints[0].EndBlock!;
        var m = blk.Mesh.Bounds();
        var p = (m.Min + m.Max) * 0.5;
        // find a point in the mesh's vertical mid-height just above its soffit: pick a vertex on the soffit and move up 1"
        var v = blk.Mesh.Positions.OrderBy(q => q.Z).First();
        Assert.True(blk.Probe(new SBM.Core.Geometry.Vec3(v.X, v.Y, v.Z + In(1))).Inside || blk.Probe(new SBM.Core.Geometry.Vec3(v.X, v.Y, v.Z + In(1))).X < 0.01);
        Assert.False(blk.Probe(new SBM.Core.Geometry.Vec3(v.X, v.Y, v.Z - In(2))).Inside);
    }

    [Fact]
    public void EndBlock_GirderEndsEmbedded_ReportedAsIntended_NothingMoved()
    {
        var p = SampleProject.Create();
        AddBlocks(p.Bridges[0], Ft(2) + In(2), Ft(3) + In(4.5));
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var before = f.Bearings.Select(x => x.Station).ToList();
        var st = SteelModelGenerator.Generate(f, p.Units);
        var c = ConcreteGenerator.Generate(f, p.Units);
        DeckEndBlockCheck.Run(f, st, c, p.Units);
        Assert.Contains(c.EndBlockClashes, x => x.Intended && x.Member == "Girder top flange");
        Assert.Contains(c.EndBlockClashes, x => x.Intended && x.Member == "Shear studs");
        Assert.Contains(c.Diagnostics, d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Info && d.Message.Contains("embedded as the plans show"));
        Assert.Equal(before, f.Bearings.Select(x => x.Station).ToList());
    }

    [Fact]
    public void EndBlock_TooShallow_WarnsAndBuildsNothing()
    {
        var (_, c) = Build(b => AddBlocks(b, In(10), Ft(3)));
        Assert.All(c.Joints, j => Assert.Null(j.EndBlock));
        Assert.Contains(c.Diagnostics, d => d.Message.Contains("no end block built"));
    }

    [Fact]
    public void Ifc_HasEndBlockParts()
    {
        var p = SampleProject.Create();
        AddBlocks(p.Bridges[0], Ft(2) + In(2), Ft(3) + In(4.5));
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var s = SteelModelGenerator.Generate(f, p.Units);
        var c = ConcreteGenerator.Generate(f, p.Units);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "t.ifc", p, new[] { new IfcBridgeData(f, s, c) });
        Assert.Contains("DECK_END_BLOCK", sw.ToString());
    }
}
