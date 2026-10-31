using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Segmental box details (SegmentalDefinition.BlockTypes / Blocks / Openings / Parapet / Bars): closed block solids inside the box void, mirrored
/// left / right, a block clipped to nothing closes it; opening voids + removed concrete; plumb parapet volume; schedule + placed bars with cover check;
/// IFC parts / voids / bars; JSON omitted when unset.</summary>
public class SegmentalDetailsTests
{
    private static BridgeModel Generate(BridgeProject p) => new RegenerationCache().Regenerate(p).Single()!;

    private static (BridgeProject p, BridgeDefinition b) Model()
    {
        var (p, b) = SegmentalTests.Model();
        var sg = b.Segmental!;
        sg.BlockTypes = new()
        {
            new() { Name = "top", Kind = SegmentalBlockKind.AnchorBlock, From = SegmentalJointRef.BentSide, Start = Ft(3) + In(5),
                Stations = { new() { Along = 0, BottomBelowDeck = Ft(2) + In(11.25), BottomSquareToWeb = Ft(3) + In(2.25) }, new() { Along = Ft(6) + In(9), BottomBelowDeck = Ft(2) + In(11.25), BottomSquareToWeb = Ft(3) + In(2.25) } } },
            new() { Name = "bottom", Kind = SegmentalBlockKind.AnchorBlock, From = SegmentalJointRef.BentSide, Start = Ft(4),
                Stations = { new() { Along = 0, TopAboveSlab = In(24), WidthFromWeb = In(34) }, new() { Along = In(76.5), TopAboveSlab = In(6), WidthFromWeb = In(29.5) },
                    new() { Along = In(102), TopAboveSlab = 0, WidthFromWeb = 0 } } },
            new() { Name = "typeA", Kind = SegmentalBlockKind.ErectionBarBlock, NotBuilt = "UNRESOLVED (p.300): not drawable." },
        };
        sg.Blocks = new()
        {
            new() { Type = "top", Segment = "1-D3", Label = "C1" }, new() { Type = "top", Segment = "1-U3", Label = "C1" },
            new() { Type = "bottom", Segment = "1-U2", Label = "B1" }, new() { Type = "typeA", Segment = "1-U1" },
        };
        sg.Openings = new()
        {
            new() { Name = "A.O.", Kind = SegmentalOpeningKind.BottomSlab, Width = Ft(3), Length = Ft(4), Segments = { "1-U2" } },
            new() { Name = "T.A.O.", Kind = SegmentalOpeningKind.TopSlab, Width = Ft(3.5), Length = Ft(2.5), Temporary = true, Segments = { "1-D2" } },
            new() { Name = "diaphragm opening", Kind = SegmentalOpeningKind.BentDiaphragm, Bents = { "S1" },
                Outline = { new(-In(30), 0), new(In(30), 0), new(In(30), In(96)), new(In(15), In(120)), new(-In(15), In(120)), new(-In(30), In(96)) } },
        };
        sg.Parapet = new SegmentalParapetDefinition { InsideOffset = Ft(20), Width = Ft(1) + In(2), Height = Ft(2.5), Chamfer = In(0.75), Material = "Class AA" };
        sg.Bars = new()
        {
            new() { Mark = "A1", Size = "#4", Segments = @"^1-[DU]3$", CountPerSegment = 15, ListedLength = Ft(42) + In(3) },
            new() { Mark = "A1", Size = "#4", Segments = @"^1-[DU]3$", ListedLength = Ft(42) + In(3), Placement = SegmentalBarPlacement.TransverseTop, Count = 15, Spacing = In(11.5),
                FaceDistance = In(3.25), Cover = In(2), CoverTop = In(3) },
            new() { Mark = "W1", Size = "#6", Segments = @"^1-[DU]3$", CountPerSegment = 56, ListedLength = Ft(14) + In(4), ShapeCode = "3", Unresolved = "UNRESOLVED (p.270): position." },
        };
        return (p, b);
    }

    private static bool Closed(Mesh m)
    {
        var id = new Dictionary<Vec3, int>();
        int Key(Vec3 v) { var k = new Vec3(Math.Round(v.X, 7), Math.Round(v.Y, 7), Math.Round(v.Z, 7)); if (!id.TryGetValue(k, out int i)) id[k] = i = id.Count; return i; }
        var edges = new Dictionary<(int, int), int>();
        for (int t = 0; t < m.Indices.Count; t += 3)
        {
            int a = Key(m.Positions[m.Indices[t]]), b = Key(m.Positions[m.Indices[t + 1]]), c = Key(m.Positions[m.Indices[t + 2]]);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) }) if (u != v) edges[(u, v)] = edges.GetValueOrDefault((u, v)) + 1;
        }
        return edges.All(e => e.Value == 1 && edges.GetValueOrDefault((e.Key.Item2, e.Key.Item1)) == 1);
    }

    [Fact]
    public void Blocks_AreClosedMirroredAndInsideTheBox()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        Assert.Equal(6, sm.Blocks.Count);                        // 2 top x 2 webs + 1 bottom x 2 webs; type A recorded only
        foreach (var bl in sm.Blocks)
        {
            Assert.True(Closed(bl.Mesh), bl.Name);
            Assert.True(bl.Mesh.Volume() > 0, bl.Name);
            var seg = sm.Segments.Single(s => s.Name == bl.Segment).Mesh.Bounds();
            var bb = bl.Mesh.Bounds();
            Assert.True(bb.Min.Z >= seg.Min.Z - 1e-6 && bb.Max.Z <= seg.Max.Z + 1e-6, bl.Name);
        }
        foreach (var g in sm.Blocks.GroupBy(b => (b.Segment, b.Type)))
        {
            var v = g.Select(b => b.Volume).ToList();
            Assert.Equal(2, v.Count);
            Assert.Equal(v[0], v[1], 4);                        // symmetric box section about CL girder
        }
        // the top block is a prism 6'-9" long: its volume / length = the clipped section area, the same at both stations
        var top = sm.Blocks.First(b => b.Type == "top");
        double area = top.Volume / (Ft(6) + In(9));
        Assert.InRange(area / (In(1) * In(1)), 150, 900);
        // the bottom block tapers to an edge: smaller than a 2'-10" x 2'-0" x 8'-6" box
        var bot = sm.Blocks.First(b => b.Type == "bottom");
        Assert.True(bot.Volume < In(34) * In(24) * In(102));
        Assert.Contains(sm.Diagnostics, d => d.Message.Contains("'typeA' recorded, not built"));
    }

    [Fact]
    public void Openings_VoidsAndRemovedConcrete()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        var ao = sm.Openings.Single(o => o.Name == "1-U2 A.O.");
        var seg = sm.Segments.Single(s => s.Name == "1-U2");
        Assert.True(Closed(ao.Mesh));
        Assert.InRange(ao.RemovedVolume / (Ft(3) * Ft(4)), In(18), In(24.1));      // 3'-0 x 4'-0 x the bottom slab at mid segment
        Assert.Equal(0, sm.Openings.Single(o => o.Name.EndsWith("T.A.O.")).RemovedVolume);
        var dia = sm.Openings.Where(o => o.Kind == SegmentalOpeningKind.BentDiaphragm).ToList();
        Assert.Equal(2, dia.Count);                                                   // one void per bent half
        double a = In(60) * In(120) - 2 * 0.5 * In(15) * In(24);
        Assert.Equal(2 * a * Ft(3.5), dia.Sum(o => o.RemovedVolume), 6);
        Assert.Equal(sm.ConcreteVolume + sm.BlockVolume - sm.OpeningVolume, sm.NetConcreteVolume, 9);
        _ = seg;
    }

    [Fact]
    public void Parapet_PlumbSectionOverTheUnit()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sm = m.Concrete!.Segmental!;
        Assert.Equal(2, sm.Parapets.Count);
        double len = sm.Segments.Sum(s => s.ChordLength);
        double area = In(14) * In(30) - In(0.75) * In(0.75);
        foreach (var (_, mesh) in sm.Parapets) Assert.InRange(Math.Abs(mesh.Volume()) / (area * len), 0.995, 1.005);
        Assert.Equal(2, m.Concrete.Barriers.Count(b => b.Name.EndsWith("parapet")));
        Assert.Equal("Class AA", m.Concrete.BarrierInfo["Left parapet"].Material);
    }

    [Fact]
    public void Bars_ScheduleAndPlacedWithCover()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sm = m.Concrete!.Segmental!;
        var a1 = sm.Marks.Single(x => x.Mark == "A1");
        Assert.Equal(30, a1.Count);
        Assert.Equal(30, sm.Bars.Count(b => b.Mark == "A1"));
        var w1 = sm.Marks.Single(x => x.Mark == "W1");
        Assert.Equal(112, w1.Count);
        Assert.Contains("not modeled - UNRESOLVED", w1.Location);
        Assert.Equal(112 * (Ft(14) + In(4)), w1.TotalLength, 6);
        Assert.Empty(sm.CoverViolations);
        Assert.Equal(sm.Bars.Count, m.Concrete.Bars.Count);
        // every A1 bar 3 1/4" below the deck top, 42'-3" long
        foreach (var b in sm.Bars) Assert.Equal(Ft(42) + In(3), (b.Points[^1] - b.Points[0]).Length, 6);
    }

    [Fact]
    public void Ifc_PartsVoidsAndBars_AndJsonOmittedWhenUnset()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "seg.ifc", p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        var lines = sw.ToString().Split('\n');
        Assert.Equal(6, lines.Count(l => l.Contains("=IFCBUILDINGELEMENTPART(") && l.Contains("'ANCHOR_BLOCK'")));
        Assert.Equal(4, lines.Count(l => l.Contains("=IFCOPENINGELEMENT(")));
        Assert.Equal(4, lines.Count(l => l.Contains("=IFCRELVOIDSELEMENT(")));
        Assert.Equal(30, lines.Count(l => l.Contains("=IFCREINFORCINGBAR(")));
        string bk = IfcKeys.Bridge(p.Name, "Seg Unit 1");
        var idx = SBM.IO.Database.ModelDatabase.BuildTagIndex(p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        Assert.NotNull(idx);
        Assert.Contains(IfcKeys.GlobalId(IfcKeys.SegBlock(bk, m.Concrete!.Segmental!.Blocks[0].Name)), sw.ToString());
        // round trip keeps the details; a model without them writes none of the new keys
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!;
        Assert.Equal(3, back.BlockTypes!.Count);
        Assert.Equal(3, back.Openings!.Count);
        Assert.NotNull(back.Parapet);
        var plain = SegmentalTests.Model().p.ToJson();
        foreach (var k in new[] { "BlockTypes", "\"Blocks\"", "Openings", "Parapet", "\"Bars\"" }) Assert.DoesNotContain(k, plain);
    }

    [Fact]
    public void Keys_MaleOnTheMatchCastFace_PerSectionRow_AndParapetJoints()
    {
        var (p, b) = Model();
        var sg = b.Segmental!;
        double kd = In(1.25), kb = In(3.875), kf = In(2.625);
        sg.Keys = new()
        {
            new() { Name = "web", Kind = SegmentalKeyKind.WebShear, Depth = kd, BaseWidth = kb, FlatWidth = kf, Length = Ft(1) + In(2), Spacing = In(6.5),
                WebRows = new() { new() { Section = 0, Count = 27, BottomDistance = In(36.25) }, new() { Section = 1, Count = 27, BottomDistance = In(33.25) },
                    new() { Section = 2, Count = 24, BottomDistance = In(31.25) }, new() { Section = 3, Count = 22, BottomDistance = In(24.75) } } },
            new() { Name = "top", Kind = SegmentalKeyKind.TopSlabAlignment, Depth = kd, BaseWidth = kb, FlatWidth = kf, Length = Ft(3), Offsets = new() { -Ft(19), 0, Ft(19) }, CenterDepth = In(4.4375) },
        };
        sg.Parapet!.JointSpacing = Ft(30); sg.Parapet.JointWidth = In(0.5); sg.Parapet.JointEdgeOffset = Ft(21) + In(3.5);
        sg.Parapet.JointRuns = new() { new() { From = "S0", To = "S1", Spaces = 1, PrintedEndA = Ft(15), Source = "test" } };
        var sm = Generate(p).Concrete!.Segmental!;
        var keys = sm.Blocks.Where(k => k.Kind == SegmentalBlockKind.ShearKey).ToList();
        // joints: D1 / D2 / D3 and U1 / U2 / U3 (sections 1-3) + the bent CL (up-station half) = 7, each 2 web sets + 1 top set
        Assert.Equal(7 * 3, keys.Count);
        int N(SBM.Core.Segmental.SegmentalBlockResult k) => int.Parse(k.Label.Split(' ')[0]);
        Assert.All(keys.Where(k => k.Type == "web" && k.Segment.EndsWith("1")), k => Assert.Equal(27, N(k)));
        Assert.All(keys.Where(k => k.Type == "web" && k.Segment.EndsWith("3")), k => Assert.Equal(22, N(k)));
        Assert.Equal(2, keys.Count(k => k.Type == "web" && k.Segment == "1-UB"));
        double one = (kb + kf) / 2 * kd;
        foreach (var k in keys)
        {
            Assert.True(k.Mesh.Volume() > 0, k.Name);
            double len = k.Type == "web" ? Ft(1) + In(2) : Ft(3);
            Assert.InRange(k.Volume / (N(k) * one * len), 0.999, 1.001);   // frames along the chord: tiny skew of the key prism
        }
        // keys are recorded only: not in the block concrete
        Assert.Equal(0, sm.BlockVolume - sm.Blocks.Where(x => x.Kind != SegmentalBlockKind.ShearKey).Sum(x => x.Volume), 9);
        Assert.Equal(keys.Sum(k => k.Volume), sm.KeyVolume, 9);
        // the male keys of 1-D2 protrude from its bent-side (up-station) face into 1-D1 (the joint plane is square to the GRADED tangent and rotated by the
        // cross slope, so the corners 19 ft out reach a little further in plan station)
        var d2 = sm.Segments.Single(s => s.Name == "1-D2");
        var kD2 = keys.First(k => k.Segment == "1-D2" && k.Type == "top").Mesh;
        var al = p.Alignments[0];
        double sMax = kD2.Positions.Max(q => al.Inverse(q.XY).Station);
        Assert.True(sMax - d2.Station1 > kd * 0.9 && sMax - d2.Station1 < kd * 1.4, $"{(sMax - d2.Station1) / In(1)} in, D2 {d2.Station0} - {d2.Station1}");
        // parapet: two joints per side (15 + 30 + 15 along the edge), 1/2" gaps
        Assert.Equal(4, sm.ParapetJoints.Count);
        Assert.Contains(sm.Checks, c => c.Item.Contains("end length") && Math.Abs(c.Difference) < In(1.0 / 16));
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!;
        Assert.Equal(2, back.Keys!.Count);
        Assert.Equal(4, back.Keys[0].WebRows!.Count);
        Assert.Single(back.Parapet!.JointRuns!);
        string plain = SegmentalTests.Model().p.ToJson();
        Assert.DoesNotContain("\"Keys\"", plain);
        Assert.DoesNotContain("JointRuns", plain);
    }
}
