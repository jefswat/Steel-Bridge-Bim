using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Generic inputs added for the Bonner Unit 6 details (NCDOT B-2500 p.405-455): open joints in a custom barrier, a first bar per
/// longitudinal deck line, the back + ahead beams of a pier continuity diaphragm with the BeamBottoms rule; and the sample model's detail items.</summary>
public class BonnerUnit6DetailTests
{
    private static string? RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SteelBridgeModeler.sln"))) return Path.Combine(d.FullName, rel);
        return null;
    }

    private static (BridgeFraming f, ConcreteModel c) Build(Action<BridgeDefinition> tweak)
    {
        var p = SampleProject.Create();
        tweak(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        Assert.False(f.HasErrors);
        return (f, ConcreteGenerator.Generate(f, p.Units));
    }

    private static void Plain(BridgeDefinition b)
    {
        b.Deck.Barrier.Type = BarrierType.Custom;
        b.Deck.Barrier.Left = false;
        b.Deck.Barrier.Profile = new() { new(0, 0), new(Ft(1), 0), new(Ft(1), Ft(3)), new(0, Ft(3)) };
        b.Deck.Reinforcement.Enabled = false;
    }

    [Fact]
    public void CustomBarrier_OpenJoints_SplitTheBarrier_AndRemoveTheGapConcrete()
    {
        var (_, c0) = Build(Plain);
        double v0 = c0.BarrierVolume;
        var (_, c1) = Build(b =>
        {
            Plain(b);
            b.Deck.Barrier.Joints = new()
            {
                new BarrierJoint { Support = b.Supports[1].Name, Along = 0, Width = In(0.5) },
                new BarrierJoint { Support = b.Supports[1].Name, Along = Ft(20), Width = In(0.5) },
                new BarrierJoint { Support = "no such support", Width = In(0.5) },
            };
        });
        var name = Assert.Single(c1.Barriers).Name;
        Assert.Equal(3, c1.BarrierPieces[name].Count);                                   // two joints -> three solids
        Assert.True(Math.Abs(v0 - 2 * In(0.5) * Ft(1) * Ft(3) - c1.BarrierVolume) < 1e-4, $"{c1.BarrierVolume}");   // gap = width x section (sampled sweep: 1e-4 m3)
        Assert.Contains(c1.Diagnostics, d => d.Message.Contains("joint at no such support") && d.Message.Contains("not built"));
        Assert.Contains(c1.Notes, n => n.Contains("2 open joint(s)"));
    }

    [Fact]
    public void CustomBarrier_ZoneEndingOnTheBarrierEnd_IsClampedToIt()
    {
        // an end-post zone that reaches 1/4" past the begin end of the barrier: clamped (it was rejected before)
        var (_, c1) = Build(b =>
        {
            Plain(b);
            b.Supports[0].SkewDegrees = 0;
        });
        var post = new List<BarrierProfilePoint> { new(0, 0), new(Ft(1), 0), new(Ft(1), Ft(5)), new(0, Ft(5)) };
        var (_, c2) = Build(b =>
        {
            Plain(b);
            b.Supports[0].SkewDegrees = 0;
            double endSta = b.Supports[0].Station - b.Supports[0].DeckEndOffset;
            b.Deck.Barrier.Zones = new() { new BarrierZone { Name = "end post", Station = endSta + Ft(2) - In(0.25), Length = Ft(4) + In(0.5), Profile = post } };
        });
        Assert.DoesNotContain(c2.Diagnostics, d => d.Message.Contains("zone 'end post' not built"));
        var name = Assert.Single(c2.Barriers).Name;
        Assert.Equal(2, c2.BarrierPieces[name].Count);
        // 4'-0" of the barrier is 5 ft high instead of 3 ft (the 1/4" beyond the end is clamped)
        Assert.True(Math.Abs(c1.BarrierVolume + Ft(4) * Ft(1) * Ft(2) - c2.BarrierVolume) < 1e-3, $"{c2.BarrierVolume}");
    }

    [Fact]
    public void DeckLongZone_StartBar_LeadsEveryLine_AlternateLinesFromTheOtherEnd()
    {
        var (f, c) = Build(b =>
        {
            b.Deck.Barrier.Type = BarrierType.None;
            b.Deck.Reinforcement.Enabled = true;
            b.Deck.Reinforcement.Plan = new DeckPlanReinforcement
            {
                Longitudinal =
                {
                    new DeckLongZone
                    {
                        Name = "bottom", Positions = new() { Ft(1), Ft(2), Ft(3), Ft(4) }, PositionLayers = Enumerable.Repeat(DeckBarLayer.Bottom, 4).ToList(),
                        Size = "#5", Mark = "B9", StockLength = Ft(40), Lap = Ft(2.5), StartMark = "B7", StartLength = Ft(25), ClosureMark = "B8", AlternateEnds = true,
                    },
                },
            };
        });
        Assert.Equal(4, c.Marks.Where(m => m.Mark == "B7").Sum(m => m.Count));              // one start bar per line
        Assert.Equal(4, c.Marks.Where(m => m.Mark == "B8").Sum(m => m.Count));              // one closing bar per line
        Assert.Equal(Ft(25), c.Marks.First(m => m.Mark == "B7").Length, 6);
        // the start bars of lines 1 and 3 are at the begin, of lines 2 and 4 at the end of the deck
        var starts = c.Bars.Where(b => b.Mark == "B7").Select(b => b.Points[0].XY).ToList();
        var begin = f.Deck.BeginLinePoint;
        var near = starts.Count(p => Vec2.Distance(p, begin) < Ft(30));
        Assert.Equal(2, near);
        var o = new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        Assert.DoesNotContain("StartMark", System.Text.Json.JsonSerializer.Serialize(new DeckLongZone(), o));
        Assert.DoesNotContain("StartLength", System.Text.Json.JsonSerializer.Serialize(new DeckLongZone(), o));
        Assert.DoesNotContain("Joints", System.Text.Json.JsonSerializer.Serialize(new BarrierDefinition(), o));
    }

    [Fact]
    public void PierContinuityDiaphragm_BeamBottomsRule_MergesTheBackAndAheadBeamsOfALine()
    {
        var doc = ConcreteDiaphragmTests.Flat(spans: 2);
        DiaphragmSectionPoint P(double x, DiaphragmZRef r, double dz = 0) => new(x, r, dz);
        doc.Bridge().Supports[1].Diaphragm = new ConcreteDiaphragmDefinition
        {
            Kind = ConcreteDiaphragmKind.PierContinuity, Risers = DiaphragmRiserRule.BeamBottoms,
            Parts = { new DiaphragmPart { Name = "pier", Section = { P(-Ft(1.5), DiaphragmZRef.Bottom), P(Ft(1.5), DiaphragmZRef.Bottom), P(Ft(1.5), DiaphragmZRef.Soffit), P(-Ft(1.5), DiaphragmZRef.Soffit) } } },
            Left = DiaphragmEnd.AtOffset(-Ft(9)), Right = DiaphragmEnd.AtOffset(Ft(9)),
        };
        doc.Regenerate();
        var ps = doc.Prestressed();
        var cd = ps.ConcreteDiaphragms!;
        var d = Assert.Single(cd.Diaphragms);
        Assert.Contains(cd.Diagnostics, x => x.Message.Contains("back and ahead beams of 3 girder line(s)"));
        // level bridge: bottom = beam bottom everywhere; volume = gross from the beam bottom to the soffit less 6 embedded beam ends
        var shape = ps.Shape;
        double zb = ps.Beams[0].BearingStart.Z - shape.Depth, soffit = 300 - In(8);
        double area = MeshBuilder.SignedArea(shape.Outline()), embed = Ft(1.5) - In(4);
        double expected = Ft(3) * Ft(18) * (soffit - zb) - 6 * embed * (area + shape.TopWidth * In(2));
        Assert.Equal(expected, d.Volume, 4);
        Assert.Equal("PIER_DIAPHRAGM", d.ObjectType);
        Assert.Equal("END_DIAPHRAGM", new SBM.Core.Diaphragms.ConcreteDiaphragm { Name = "x", Support = "s", SupportIndex = 0, Definition = new() { Kind = ConcreteDiaphragmKind.ExpansionJointEnd } }.ObjectType);
    }

    [Fact]
    public void SlabPipeDrain_IsAVerticalPipeThroughTheSlabOnly_WithItsFlag()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        b.Drains.Add(new DeckDrainDefinition { Station = b.Supports[0].Station + Ft(40), Left = false, FromGutter = In(3), PipeDiameter = In(6), SlabPipeOnly = true,
            PipeBelowSlab = In(2), Unresolved = "UNRESOLVED (p.1): drain position - approximate.", Basis = "test" });
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        var steel = SBM.Core.Steel.SteelModelGenerator.Generate(f, p.Units);
        var sub = SBM.Core.Substructure.SubstructureGenerator.Generate(f, steel, p.Units);
        var d = Assert.Single(sub.Drainage);
        Assert.Equal(SBM.Core.Substructure.SubElementKind.DrainPipe, d.Kind);
        var bb = d.Mesh.Bounds();
        Assert.Equal(b.Deck.Thickness + In(2), bb.Max.Z - bb.Min.Z, 3);                  // deck top to 2" below the soffit
        Assert.True(d.Assumption is { DimensionOnly: true });
        Assert.Contains(sub.Diagnostics, x => x.Message.Contains("drain position"));
        var o = new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        string json = System.Text.Json.JsonSerializer.Serialize(new DeckDrainDefinition(), o);
        Assert.DoesNotContain("SlabPipeOnly", json);
        Assert.DoesNotContain("PipeBelowSlab", json);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiaphragmSlopedEnd_RemovesTheWedge_AndIsOmittedWhenUnset(bool openBelow)
    {
        DiaphragmSectionPoint P(double x, DiaphragmZRef r, double dz = 0) => new(x, r, dz);
        double Vol(DiaphragmEnd right, out SBM.Api.SbmDocument d)
        {
            d = ConcreteDiaphragmTests.Flat();
            d.Regenerate();
            double zb0 = d.Prestressed().Beams[0].BearingStart.Z - d.Prestressed().Shape.Depth - In(3);
            d.Bridge().Supports[0].Diaphragm = new ConcreteDiaphragmDefinition
            {
                Kind = ConcreteDiaphragmKind.IntegralEnd, Name = "t", OpenBelowBeams = openBelow,
                Parts = { new DiaphragmPart { Name = "main", Section = { P(-Ft(1.5), DiaphragmZRef.Elevation, zb0), P(Ft(1.5), DiaphragmZRef.Elevation, zb0), P(Ft(1.5), DiaphragmZRef.Soffit), P(-Ft(1.5), DiaphragmZRef.Soffit) } } },
                Left = DiaphragmEnd.AtOffset(-Ft(9)), Right = right,
            };
            d.Regenerate();
            var cd = d.Prestressed().ConcreteDiaphragms!;
            Assert.True(cd.Diaphragms.Count == 1, string.Join(" | ", cd.Diagnostics.Select(x => x.Message)));
            return cd.Diaphragms[0].Volume;
        }
        double v0 = Vol(DiaphragmEnd.AtOffset(Ft(9)), out var d0);
        double v1 = Vol(DiaphragmEnd.Sloped(Ft(9), Ft(8)), out var d1);
        double zb = d1.Prestressed().Beams[0].BearingStart.Z - d1.Prestressed().Shape.Depth - In(3);
        double h = 300 - In(8) - zb;                                                          // Flat(): deck top 300 m, 8" slab
        Assert.Equal(0.5 * Ft(1) * h * Ft(3), v0 - v1, 4);                                    // the wedge between the bottom (9 ft) and top (8 ft) lines
        var o = new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        Assert.DoesNotContain("TopOffset", System.Text.Json.JsonSerializer.Serialize(DiaphragmEnd.AtOffset(1), o));
        Assert.Contains("TopOffset", System.Text.Json.JsonSerializer.Serialize(DiaphragmEnd.Sloped(1, 2), o));
    }

    [Fact]
    public void BonnerUnit6_Sample_DetailItems()
    {
        var path = RepoFile(Path.Combine("samples", "plans", "Bonner Unit 6 FIB.sbm"));
        if (path == null || !File.Exists(path)) return;
        var doc = SbmDocument.Open(path);
        if (doc.Bridge("Bonner Unit 6").Supports[1].Diaphragm == null) return;      // model not rebuilt with the detail scripts yet
        doc.Regenerate();
        double cy = Math.Pow(Ft(1), 3) * 27;
        var dm = doc.Diaphragms("Bonner Unit 6");
        Assert.Equal(7, dm.Diaphragms.Count);                                          // bents 29-35
        var cm = doc.Concrete("Bonner Unit 6");
        Assert.Equal(2, cm.Joints.Count);
        Assert.True(Math.Abs(cm.BarrierVolume / cy - 209.2) < 1.0, $"{cm.BarrierVolume / cy}");   // p.451 parapet 209.2 CY
        Assert.All(cm.Barriers, b => Assert.Equal(38, cm.BarrierPieces[b.Name].Count));   // 35 joints + 2 end posts
        Assert.Equal(1924 + 2199, cm.Marks.Where(m => m.Mark == "A101").Sum(m => m.Count));
        Assert.Equal(792, cm.Marks.Where(m => m.Mark == "B109").Sum(m => m.Count));
        Assert.Equal(44, cm.Marks.Where(m => m.Mark == "B107").Sum(m => m.Count));
        var ps = doc.Prestressed("Bonner Unit 6");
        Assert.All(ps.Beams, b => Assert.Equal(148, b.Bars.Count(x => x.Mark == "S1")));
        if (doc.Bridge("Bonner Unit 6").Railings is not { Count: > 0 }) return;            // model without the rail / parapet bars pass
        Assert.Equal(314, doc.Model("Bonner Unit 6").Steel!.Railings!.Runs.Sum(r => r.Posts)); // p.38 anchor assembly table
        Assert.Equal(1924, cm.Bars.Count(x => x.Mark == "S1"));                            // parapet S1 (p.448)
        Assert.Equal(145, cm.Bars.Count(x => x.Mark == "B101"));
        Assert.Equal(71, doc.Substructure("Bonner Unit 6")!.Drainage.Count);               // 6" PVC pipe drains p.409-414
    }
}
