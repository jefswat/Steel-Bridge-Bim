using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class DetailingTests
{
    private static (BridgeFraming f, SteelModel m) Build(Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        tweak?.Invoke(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        Assert.False(f.HasErrors, string.Join("\n", f.Diagnostics));
        return (f, SteelModelGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void PlanFrames_StylesOnExplicitLines_ChannelsBeamsAndPlateDiaphragms()
    {
        var (f, m) = Build(b =>
        {
            var cf = b.CrossFrames;
            cf.Styles.Add(new CrossFrameStyle
            {
                Name = "1", TopChord = "L6x6x9/16", Diagonal = "L6x6x7/8", BottomChord = "2-MC8x20", GussetThickness = In(0.625),
                BoltDiameter = In(1), BoltsPerConnection = 5, BoltPitch = In(3.5), BoltEdgeDistance = In(1.75), BottomWorkPointOffset = In(10),
                TiePlateOffsets = { -In(16), In(16) },
            });
            cf.Styles.Add(new CrossFrameStyle { Name = "2", TopChord = "L6x6x9/16", Diagonal = "L8x6x5/8", BottomChord = "MC10x33.6", BoltDiameter = In(1) });
            cf.Styles.Add(new CrossFrameStyle { Name = "10", TopChord = "W12x40", Diagonal = "L5x5x5/8", BottomChord = "2-MC6x15.3",
                GussetThickness = In(0.5), BoltDiameter = In(1), BoltsPerConnection = 3, BoltPitch = In(3.5), TopWorkPointOffset = In(18) });
            cf.Styles.Add(new CrossFrameStyle { Name = "7", Kind = CrossFrameStyleKind.PlateDiaphragm, WebPlateThickness = In(0.625),
                FlangePlateWidth = In(16), FlangePlateThickness = In(0.75), DiaphragmBolts = 9, DiaphragmBoltPitch = In(6), BoltDiameter = In(1) });
            cf.Styles.Add(new CrossFrameStyle { Name = "T", Kind = CrossFrameStyleKind.KFrame, TopChord = "W24x68", Diagonal = "L5x5x3/8",
                BottomChord = "WT5x15", GussetThickness = In(0.5), BoltDiameter = In(0.875), TopWorkPointOffset = In(16) });
            cf.IntermediateStyle = "1";
            cf.SupportStyle = "10";
            cf.SupportStyles[b.Supports[1].Name] = "none";
            cf.Lines.Add(new CrossFrameLine { Girder = 0, Distance = Ft(30) });
            cf.Lines.Add(new CrossFrameLine { Girder = 0, Distance = Ft(60), BayStyles = { [0] = "2", [1] = "7", [2] = "none", [3] = "T" } });
        });
        // Span 1 has exactly the two lines; the pier has no support frames, the abutment has Type 10 frames.
        var span1 = f.CrossFrames.Where(c => c.Span == 0 && !c.AtSupport).ToList();
        int bays = f.Girders.Count - 1;
        Assert.Equal(2 * bays - 1, span1.Count);
        Assert.DoesNotContain(f.CrossFrames, c => c.AtSupport && c.SupportIndex == 1);
        Assert.All(f.CrossFrames.Where(c => c.AtSupport), c => Assert.Equal("10", c.Style));
        Assert.Contains(span1, c => c.Style == "7" && c.Type == ResolvedCrossFrameType.PlateDiaphragm);
        Assert.DoesNotContain(f.Diagnostics, d => d.Message.Contains("not defined") || d.Message.Contains("not in the catalog"));
        // Type 1: back-to-back MC8x20 bottom chord with tie plates, 1" bolts.
        var t1 = span1.First(c => c.Style == "1").Mark;
        Assert.Contains(m.Parts, p => p.Assembly == t1 && p.Description == "2-MC8x20");
        Assert.Equal(2, m.Parts.Count(p => p.Assembly == t1 && p.Mark.Contains("-TP")));
        Assert.All(m.Bolts.Where(b => b.Assembly == t1), b => Assert.Equal(In(1), b.Diameter, 9));
        Assert.Equal(4 * 5, m.Bolts.Count(b => b.Assembly == t1));                     // 4 gussets x 5 bolts
        // Type 10: W12x40 top strut bolted through its web (no top gussets), 3 bolts per girder there.
        var t10 = f.CrossFrames.First(c => c.Style == "10").Mark;
        Assert.Contains(m.Parts, p => p.Assembly == t10 && p.Description == "W12x40");
        Assert.DoesNotContain(m.Parts, p => p.Assembly == t10 && (p.Mark.EndsWith("-GTL") || p.Mark.EndsWith("-GTR")));
        Assert.Contains(m.Parts, p => p.Assembly == t10 && p.Mark.EndsWith("-GC"));
        // Type 7 plate diaphragm: web + 2 flange plates, 9 bolts per end.
        var t7 = span1.First(c => c.Style == "7").Mark;
        Assert.Single(m.Parts, p => p.Assembly == t7 && p.Mark.EndsWith("-WEB"));
        Assert.Equal(2, m.Parts.Count(p => p.Assembly == t7 && p.Mark.Contains("-FL")));
        Assert.Equal(18, m.Bolts.Count(b => b.Assembly == t7));
        // K-frame with a WT bottom chord (stem on the gussets, flange below them) and a W top strut.
        var tk = span1.First(c => c.Style == "T").Mark;
        Assert.Contains(m.Parts, p => p.Assembly == tk && p.Description == "WT5x15");
        Assert.Contains(m.Parts, p => p.Assembly == tk && p.Mark.EndsWith("-GC"));
        Assert.All(m.Parts.Where(p => !p.Mesh.IsEmpty), p => Assert.True(Math.Abs(p.Mesh.Volume()) > 0, p.Mark));
    }

    [Fact]
    public void PlateWithHoles_VolumeIsAreaMinusHoles()
    {
        var mesh = new Mesh();
        var outline = new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 0.5), new Vec2(0, 0.5) };
        var holes = new[] { new Hole(new Vec2(0.2, 0.25), 0.1), new Hole(new Vec2(0.5, 0.25), 0.1), new Hole(new Vec2(0.8, 0.25), 0.1) };
        PlateBuilder.AddPlanar(mesh, outline, holes, Frame3.World, 0, 0.02);
        int n = PlateBuilder.HoleSegments;
        double r = 0.05 / Math.Cos(Math.PI / n);
        double holeA = n / 2.0 * r * r * Math.Sin(2 * Math.PI / n);
        Assert.Equal((0.5 - 3 * holeA) * 0.02, mesh.Volume(), 10);
    }

    [Fact]
    public void PlateWithColumnOfHolesNearEdge_TriangulatesCleanly()
    {
        // Bearing-stiffener-like plate: clipped corners, a vertical column of holes 1-1/2" from the outer edge.
        var outline = new[] { new Vec2(0, -0.04), new Vec2(0.04, 0), new Vec2(0.18, 0), new Vec2(0.18, -1.37), new Vec2(0.04, -1.37), new Vec2(0, -1.33) };
        var holes = new[] { new Hole(new Vec2(0.142, -0.6), 0.024), new Hole(new Vec2(0.142, -0.676), 0.024), new Hole(new Vec2(0.142, -0.752), 0.024) };
        var mesh = new Mesh();
        PlateBuilder.AddPlanar(mesh, outline, holes, Frame3.World, 0, 0.022);
        int n = PlateBuilder.HoleSegments;
        double r = 0.012 / Math.Cos(Math.PI / n);
        double holeA = n / 2.0 * r * r * Math.Sin(2 * Math.PI / n);
        double area = Math.Abs(MeshBuilder.SignedArea(outline)) - 3 * holeA;
        Assert.Equal(area * 0.022, mesh.Volume(), 10);
        // Every cap triangle must lie inside the plate (no triangle may cover a hole center).
        foreach (var h in holes)
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                var a = mesh.Positions[mesh.Indices[i]].XY;
                var b = mesh.Positions[mesh.Indices[i + 1]].XY;
                var c = mesh.Positions[mesh.Indices[i + 2]].XY;
                double d1 = Vec2.Cross(b - a, h.Center - a), d2 = Vec2.Cross(c - b, h.Center - b), d3 = Vec2.Cross(a - c, h.Center - c);
                bool inside = (d1 > 1e-12 && d2 > 1e-12 && d3 > 1e-12) || (d1 < -1e-12 && d2 < -1e-12 && d3 < -1e-12);
                Assert.False(inside, "a face triangle covers a bolt hole");
            }
    }

    [Fact]
    public void Studs_LengthFromHaunchMinusFlangePlusPenetration()
    {
        var (f, m) = Build(b => { foreach (var s in b.Supports) s.SkewDegrees = 0; });
        Assert.NotEmpty(m.Studs);
        var d = f.Definition;
        // Haunch 3", top flange 1" → 2" to the deck bottom (± cross slope across the flange) + 2" penetration ≈ 4".
        double expected = d.Deck.Haunch - d.Girders.Section.TopFlange.Thickness + d.Studs.Penetration;
        Assert.All(m.Studs, s => Assert.InRange(s.Length, expected - In(0.01), expected + In(1.01)));
        // Studs sit on the top of the top flange.
        var g0 = f.Girders[0];
        Assert.All(m.Studs.Where(s => s.Assembly.StartsWith("G1-")), s =>
        {
            var p = g0.Path.At(g0.Path.Length / 2);
            Assert.True(s.Frame.Z.Z > 0.999);
        });
    }

    [Fact]
    public void Studs_OmittedOverSplicePlates()
    {
        var (f, m) = Build();
        foreach (var sp in f.Splices)
        {
            var g = f.Girders[sp.Girder];
            var at = g.Path.At(sp.GirderDistance).point;
            Assert.DoesNotContain(m.Studs, s => s.Assembly.StartsWith(g.Name + "-") && (s.Frame.Origin.XY - at.XY).Length < In(6));
        }
    }

    [Fact]
    public void IntermediateFrames_AreNormalToFlanges_SupportFramesPlumb()
    {
        var (f, m) = Build();
        // On grade the intermediate connection stiffeners tilt with the girder; bearing stiffeners stay plumb.
        foreach (var part in m.Parts.Where(p => p.Category == PartCategory.BearingStiffener))
        {
            var bb = part.Mesh.Bounds();
            Assert.True(bb.Max.Z - bb.Min.Z > 1.0);
        }
        var stiff = f.Stiffeners.First(s => s.Kind == StiffenerKind.ConnectionPlate);
        var g = f.Girders[stiff.Girder];
        double grade = g.Path.GradeAt(stiff.GirderDistance);
        Assert.True(Math.Abs(grade) > 0.001, "sample should be on grade");
        var cs = m.Parts.Where(p => p.Category == PartCategory.ConnectionStiffener).ToList();
        Assert.NotEmpty(cs);
        // Top and bottom of a tilted plate are offset along the girder by ~ depth × grade.
        var plate = cs.First();
        var pts = plate.Mesh.Positions;
        var top = pts.OrderByDescending(p => p.Z).Take(4).Aggregate(Vec3.Zero, (a, p) => a + p) * 0.25;
        var bot = pts.OrderBy(p => p.Z).Take(4).Aggregate(Vec3.Zero, (a, p) => a + p) * 0.25;
        double horiz = (top.XY - bot.XY).Length;
        Assert.True(horiz > 0.5 * Math.Abs(grade) * f.Definition.Girders.Section.WebDepth, $"horizontal shift {horiz}");
    }

    [Fact]
    public void ConnectionStiffeners_HaveBoltHoles()
    {
        var (f, m) = Build();
        var cs = m.Parts.First(p => p.Category == PartCategory.ConnectionStiffener);
        // A holed plate has more triangles than a 6-sided clipped plate (12 cap + 12 wall triangles per side).
        Assert.True(cs.Mesh.TriangleCount > 60);
    }

    [Fact]
    public void FieldSplice_HasPlatesBoltsAndFillersWhenThicknessDiffers()
    {
        var (f, m) = Build(b =>
        {
            double s1 = b.FieldSplices[0].Station;
            b.Girders.Section.PlateChanges = new()
            {
                new PlateSegment { Plate = GirderPlate.TopFlange, EndStation = s1, Width = In(16), Thickness = In(1) },
                new PlateSegment { Plate = GirderPlate.TopFlange, EndStation = b.FieldSplices[1].Station, Width = In(18), Thickness = In(1.5) },
                new PlateSegment { Plate = GirderPlate.TopFlange, Width = In(16), Thickness = In(1) },
            };
        });
        var fs = m.Parts.Where(p => p.Assembly.StartsWith("FS1-G1")).ToList();
        // 3 top flange + 3 bottom flange + 2 web splice plates.
        Assert.Equal(8, fs.Count(p => p.Category == PartCategory.SplicePlate));
        Assert.Equal(1, fs.Count(p => p.Category == PartCategory.FillerPlate));
        // Width taper at the field splice: wider (18") side tapers down to 16" at the joint.
        var outer = fs.First(p => p.Description.StartsWith("Outside top"));
        Assert.Contains("16", outer.Description);
        var sd = f.Definition.SpliceDetail;
        int topBolts = 2 * sd.TopFlangeBoltRows * 2 * sd.FlangeBoltLinesPerSide;
        Assert.True(m.Bolts.Count(b => b.Assembly == "FS1-G1") >= topBolts);
    }

    [Fact]
    public void ShopSplice_ThicknessTransitionIsTapered()
    {
        var (f, m) = Build(b =>
        {
            double pier = b.Supports[1].Station;
            b.Girders.Section.PlateChanges = new()
            {
                new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = pier - Ft(20), Width = In(18), Thickness = In(1) },
                new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = pier + Ft(20), Width = In(18), Thickness = In(2) },
                new PlateSegment { Plate = GirderPlate.BottomFlange, Width = In(18), Thickness = In(1) },
            };
        });
        var bf = m.Parts.Where(p => p.Mark.StartsWith("G1-2-BF")).ToList();
        Assert.True(bf.Count >= 2);
        // Volume of the thick plate is less than full-thickness × length because of the 1:2.5 tapers.
        var thick = bf.First(p => p.Description.Contains("x 2\""));
        double full = In(18) * In(2) * thick.Length;
        Assert.True(thick.Volume < full - 1e-6);
        Assert.True(thick.Volume > In(18) * In(1) * thick.Length);
    }

    [Fact]
    public void DiscBearings_HaveAllComponentsAndAnchorBolts()
    {
        var (f, m) = Build(b =>
        {
            b.Bearings.Type = BearingType.Disc;
            b.Supports[0].Fixity = BearingFixity.GuidedExpansion;
        });
        foreach (var cat in new[] { PartCategory.BearingPlate, PartCategory.BearingDisc, PartCategory.ShearPin, PartCategory.MasonryPlate, PartCategory.Ptfe, PartCategory.GuideBar })
            Assert.Contains(m.Parts, p => p.Category == cat);
        int bearings = f.Bearings.Count();
        Assert.Equal(bearings * f.Definition.Bearings.AnchorBoltCount, m.Bolts.Count(b => b.Kind == BoltKind.Anchor));
        // Pier (fixed): no PTFE.
        Assert.DoesNotContain(m.Parts, p => p.Category == PartCategory.Ptfe && p.Assembly.Contains("Pier1"));
        Assert.All(m.BearingSeats, s => Assert.Equal("Disc", s.Type));
    }

    [Theory]
    [InlineData(LateralFlange.Bottom)]
    [InlineData(LateralFlange.Top)]
    public void LateralBracing_GeneratesMembersGussetsAndFlangeHoles(LateralFlange flange)
    {
        var (f, m) = Build(b =>
        {
            b.LateralBracing.Enabled = true;
            b.LateralBracing.Flange = flange;
            b.LateralBracing.Bays = new() { 1, 4 };
        });
        var lb = m.Parts.Where(p => p.Assembly.StartsWith("LB-")).ToList();
        Assert.Contains(lb, p => p.Category == PartCategory.Angle && p.Role == MemberRole.Lateral);
        Assert.Contains(lb, p => p.Category == PartCategory.Gusset);
        Assert.Contains(m.Bolts, b => b.Assembly.StartsWith("LB-"));
        Assert.DoesNotContain(m.Diagnostics, d => d.Message.Contains("lateral member too short"));
        Assert.All(lb, p => Assert.True(p.Mesh.Volume() > 0));
    }
}
