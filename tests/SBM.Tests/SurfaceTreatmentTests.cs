using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Surface;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Custom barrier sections, diaphragm step blocks and surface treatments (Woodbury form liner / thin brick features).</summary>
public class SurfaceTreatmentTests
{
    [Fact]
    public void CustomProfile_FilletIsTangentArcAndTagsFollowTheEdges()
    {
        // 20 x 40 in. rectangle, fillet R 5 at the top-right corner, chamfer 1 at the top-left, back edge tagged
        var pts = new List<BarrierProfilePoint>
        {
            new(0, 0), new(In(20), 0), new(In(20), In(40), radius: In(5)), new(0, In(40), chamfer: In(1), tag: "liner"),
        };
        var x = BarrierProfiles.Expand(pts);
        var ctr = new Vec2(In(15), In(35));
        var arc = x.Points.Where(p => p.X > In(15) - 1e-9 && p.Y > In(35) - 1e-9).ToList();
        Assert.True(arc.Count >= 3);
        foreach (var p in arc) Assert.Equal(In(5), (p - ctr).Length, 9);
        // area: rectangle - fillet corner (R^2 (1 - pi/4)) - chamfer triangle
        double area = In(20) * In(40) - In(5) * In(5) * (1 - Math.PI / 4) - In(1) * In(1) / 2;
        Assert.Equal(area, MeshBuilder.SignedArea(x.Points), 4);
        // the tagged edge is the back face (R = 0) down to the base
        int k = Array.IndexOf(x.EdgeTags, "liner");
        Assert.Equal(1, x.EdgeTags.Count(t => t == "liner"));
        Assert.Equal(0, x.Points[k].X, 9);
        Assert.Equal(0, x.Points[(k + 1) % x.Points.Length].Y, 9);
    }

    private static (BridgeFraming f, ConcreteModel c, BridgeProject p) Build(Action<BridgeDefinition> tweak)
    {
        var p = SampleProject.Create();
        tweak(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        Assert.False(f.HasErrors);
        return (f, ConcreteGenerator.Generate(f, p.Units), p);
    }

    [Fact]
    public void CustomBarrier_ExtensionsZoneAndTreatmentArea()
    {
        double ext = Ft(2);
        var (f, c, p) = Build(b =>
        {
            b.Deck.Barrier.Type = BarrierType.Custom;
            b.Deck.Barrier.Left = false;
            b.Deck.Barrier.Profile = new() { new(0, 0), new(Ft(1), 0), new(Ft(1), Ft(3)), new(0, Ft(3), tag: "liner") };
            b.Deck.Barrier.BeginExtension = ext;
            b.Deck.Barrier.EndExtension = ext;
            b.Deck.Barrier.PerpendicularToDeck = true;
            // a 4 ft bump-out at the second support: back face 6 in. outboard of the deck edge, tagged brick, on a 1 ft deck widening
            b.Deck.Barrier.Zones = new()
            {
                new BarrierZone
                {
                    Name = "bump", Support = b.Supports[1].Name, Length = Ft(4), DeckWidening = Ft(1), DeckWideningLength = Ft(5),
                    Profile = new() { new(-In(6), 0), new(Ft(1), 0), new(Ft(1), Ft(3)), new(-In(6), Ft(3), tag: "brick") },
                },
            };
            b.SurfaceTreatments = new()
            {
                new SurfaceTreatmentDefinition
                {
                    Name = "liner", Kind = SurfaceTreatmentKind.FormLiner, Relief = In(0.25),
                    Regions = { new SurfaceRegion { Name = "back", Host = SurfaceHost.Barrier, Tag = "liner" } },
                },
                new SurfaceTreatmentDefinition
                {
                    Name = "brick", Kind = SurfaceTreatmentKind.ThinBrick, Relief = In(0.375), PlanQuantity = 2 * Ft(4) * Ft(3),
                    Regions = { new SurfaceRegion { Name = "bump", Host = SurfaceHost.Barrier, Tag = "brick" } },
                },
            };
        });
        var bar = Assert.Single(c.Barriers);
        Assert.Equal("Right barrier", bar.Name);
        Assert.Equal(3, c.BarrierPieces[bar.Name].Count);                 // standard / bump / standard
        Assert.Single(c.DeckBumps);
        Assert.Equal(Ft(1) * Ft(5) * f.Definition.Deck.OverhangThickness, Math.Abs(c.DeckBumps[0].Mesh.Volume()), 3);
        // the toe (gutter) line runs ext beyond each deck end
        var note = Assert.Single(c.Notes, n => n.StartsWith("Right barrier"));
        Assert.Contains("along the toe", note);
        var st = SurfaceTreatmentGenerator.Generate(f, c, null, null, p.Units)!;
        var brick = Assert.Single(st.Items, i => i.Treatment.Name == "brick");
        // bump face 4 ft x 3 ft + the two 6 in. returns x 3 ft
        Assert.Equal(Ft(4) * Ft(3) + 2 * In(6) * Ft(3), brick.Area, 3);
        Assert.Equal(brick.Area * In(0.375), Math.Abs(brick.Mesh.Volume()), 4);
        var liner = Assert.Single(st.Items, i => i.Treatment.Name == "liner");
        // liner = 3 ft x (barrier length along the deck edge - 4 ft bump)
        Assert.InRange(liner.Area / Ft(3), Ft(50), Ft(2000));
        Assert.InRange(Math.Abs(liner.Mesh.Volume()) / (liner.Area * In(0.25)), 0.999, 1.001);   // thin prisms on the (slightly warped) loft quads
        Assert.Contains(st.Diagnostics, d => d.Message.Contains("brick") && d.Message.Contains("plan"));
    }

    [Fact]
    public void PierDiaphragm_StepBlocksOnASlopedTop()
    {
        // same diaphragm twice: level steps everywhere (MidBay) vs 3 ft step blocks with the substructure top 6 in. lower between them
        double Vol(bool blocks)
        {
            var doc = SbmDocument.New("blocks");
            doc.AddAlignment("CL", 0, new[] { new PiRow(0, 0), new PiRow(Ft(1000), 0) },
                new[] { new Vpi(0, 300), new Vpi(Ft(1000), 300) }, new CrossSlopeTable { Rows = { new CrossSlopeRow(0, 0, 0) } });
            var b = new BridgeDefinition { Name = "B", AlignmentName = "CL" };
            b.FieldSplices.Clear();
            for (int j = 0; j <= 2; j++)
                b.Supports.Add(new SupportDefinition
                {
                    Name = j == 1 ? "Pier 1" : $"Abut {j}", Type = j == 1 ? SupportType.Pier : SupportType.Abutment, Station = Ft(100 + 80 * j), DeckEndOffset = Ft(1.5),
                });
            b.Girders.Count = 3; b.Girders.FirstGirderOffset = -Ft(6); b.Girders.Spacings = new() { Ft(6), Ft(6) };
            b.Girders.Kind = GirderKind.TxDotPrestressed; b.Girders.Prestressed.Type = TxGirderType.Tx54;
            b.Girders.Prestressed.BearingToEnd = In(8); b.Girders.Prestressed.EndGapAtBent = In(4);
            b.Deck.LeftEdgeOffset = -Ft(9); b.Deck.RightEdgeOffset = Ft(9); b.Deck.Thickness = In(8); b.Deck.OverhangThickness = In(8); b.Deck.Haunch = In(2);
            b.Deck.Reinforcement.Enabled = false; b.Deck.Barrier.Type = BarrierType.None;
            foreach (var s in b.Supports) { s.Substructure = SubstructureDefinition.Default(s.Type); s.Substructure.Enabled = false; }
            doc.Project.Bridges.Add(b);
            doc.Regenerate();
            double zb = doc.Prestressed().Beams[0].BearingStart.Z - doc.Prestressed().Shape.Depth - In(1);
            var sec = new List<DiaphragmSectionPoint> { new(-Ft(1.5), DiaphragmZRef.Bottom), new(Ft(1.5), DiaphragmZRef.Bottom), new(Ft(1.5), DiaphragmZRef.Soffit), new(-Ft(1.5), DiaphragmZRef.Soffit) };
            var def = new ConcreteDiaphragmDefinition
            {
                Kind = ConcreteDiaphragmKind.PierContinuity, Parts = { new DiaphragmPart { Name = "pier", Section = sec } },
                Left = DiaphragmEnd.AtOffset(-Ft(9)), Right = DiaphragmEnd.AtOffset(Ft(9)),
                BottomSteps = { new(1, zb), new(2, zb), new(3, zb) }, Risers = blocks ? DiaphragmRiserRule.Blocks : DiaphragmRiserRule.MidBay,
                StepBlocks = blocks ? new DiaphragmStepBlocks { Length = Ft(3), Between = { new CapProfilePoint(0, zb - Ft(0.5)) } } : null,
            };
            doc.Bridge().Supports[1].Diaphragm = def;
            doc.Regenerate();
            var d = Assert.Single(doc.Prestressed().ConcreteDiaphragms!.Diaphragms);
            Assert.Null(d.Assumption);
            return d.Volume;
        }
        // gaps between the blocks (beams at -6 / 0 / +6 ft, blocks +-1.5 ft, ends at +-9 ft): 1.5 + 3 + 3 + 1.5 = 9 ft, 3 ft wide, 0.5 ft deeper
        Assert.Equal(Ft(9) * Ft(3) * Ft(0.5), Vol(true) - Vol(false), 6);
    }
}
