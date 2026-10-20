using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class SteelModelTests
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
    public void Extrusion_HasPositiveExactVolume()
    {
        var m = new Mesh();
        MeshBuilder.AddBox(m, Frame3.World, 0, 2, 0, 3, 0, 4);
        Assert.Equal(24, m.Volume(), 9);
        var l = new Mesh();
        var ang = SteelCatalog.Angle("L4x4x3/8");
        MeshBuilder.AddExtrusion(l, SteelCatalog.AnglePolygon(ang, 0, 1, 0, 1), Frame3.World, 0, 1);
        // AISC root fillet included; toes square (AISC area also rounds the toes): within 1.5% of the AISC area.
        Assert.Equal(1, l.Volume() / ang.Area, 0.015);
        Assert.Equal(In(0.375), ang.RootRadius, 6);
        var mirrored = new Mesh();
        MeshBuilder.AddExtrusion(mirrored, SteelCatalog.AnglePolygon(ang, 0, -1, 0, -1), Frame3.World, 0, 1);
        Assert.Equal(l.Volume(), mirrored.Volume(), 9);
    }

    [Fact]
    public void BoltLength_RoundsUpToQuarterInch()
    {
        var b = new BoltGeometry(In(0.875));
        double grip = In(0.5 + 0.875); // gusset + bearing stiffener
        double len = b.LengthForGrip(grip);
        Assert.Equal(0, Math.Round(len / In(0.25)) - len / In(0.25), 9);
        Assert.True(len >= grip + b.NutHeight + b.WasherThickness);
    }

    [Fact]
    public void Sample_ProducesAllPartFamilies_AndClosedSolids()
    {
        var (f, m) = Build(b => b.Bearings.Type = BearingType.Elastomeric);
        Assert.DoesNotContain(m.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        foreach (var cat in new[] { PartCategory.FlangePlate, PartCategory.WebPlate, PartCategory.BearingStiffener, PartCategory.ConnectionStiffener,
                     PartCategory.IntermediateStiffener, PartCategory.Gusset, PartCategory.Angle, PartCategory.SolePlate, PartCategory.BearingPad, PartCategory.Weld })
            Assert.Contains(m.Parts, p => p.Category == cat);
        Assert.All(m.Parts, p => Assert.True(p.Mesh.Volume() > 0, $"{p.Mark} volume {p.Mesh.Volume()}"));
        // Field sections: 5 girders × 3 sections each (two splices).
        Assert.Equal(15, m.Assemblies.Count(a => a.Kind == AssemblyKind.GirderFieldSection));
        Assert.Equal(f.CrossFrames.Count, m.Assemblies.Count(a => a.Kind == AssemblyKind.CrossFrame));
        Assert.Equal(f.Bearings.Count(), m.BearingSeats.Count);
    }

    [Fact]
    public void Girder_WebVolume_MatchesPlateTimesLength()
    {
        var (f, m) = Build();
        var sec = f.Definition.Girders.Section;
        double gap = f.Definition.FieldSpliceGap;
        // Holes are circumscribed 16-gons.
        int n = PlateBuilder.HoleSegments;
        double rh = m.Bolt.HoleDiameter / 2 / Math.Cos(Math.PI / n);
        double holeArea = n / 2.0 * rh * rh * Math.Sin(2 * Math.PI / n);
        foreach (var g in f.Girders)
        {
            double vol = m.Parts.Where(p => p.Category == PartCategory.WebPlate && p.Assembly.StartsWith(g.Name + "-")).Sum(p => p.Volume);
            int webBolts = m.Bolts.Count(b => b.Assembly.StartsWith("FS") && b.Assembly.EndsWith("-" + g.Name) && Math.Abs(b.Frame.Z.Z) < 0.1);
            double expected = sec.WebDepth * sec.WebThickness * (g.Length - 2 * gap) - webBolts * holeArea * sec.WebThickness;
            Assert.Equal(expected, vol, 4);
        }
    }

    [Fact]
    public void BoltCount_MatchesConnections()
    {
        var (f, m) = Build(b => b.CrossFrames.IntermediateType = CrossFrameType.KFrame);
        int perConn = f.Definition.Bolts.BoltsPerConnection;
        int expectedK = f.CrossFrames.Count(c => !c.AtSupport && c.Type == ResolvedCrossFrameType.KFrame) * 4 * perConn;
        int k = m.Bolts.Count(b => f.CrossFrames.Any(c => c.Mark == b.Assembly && !c.AtSupport));
        Assert.Equal(expectedK, k);
        Assert.All(m.Bolts, b => Assert.True(b.Length > b.Grip));
    }

    [Fact]
    public void ConnectionStiffeners_LieInSkewedCrossFramePlane()
    {
        var (f, _) = Build(); // 15° skew, frames parallel to skew
        foreach (var s in f.Stiffeners.Where(s => s.Kind == StiffenerKind.ConnectionPlate))
        {
            Assert.True(s.SkewAngle > Numerics.DegToRad(10), $"expected skewed stiffener, got {Numerics.RadToDeg(s.SkewAngle)}°");
            var cf = f.CrossFrames.Where(c => !c.AtSupport).OrderBy(c => Math.Min((c.Left.XY - s.Location.XY).Length, (c.Right.XY - s.Location.XY).Length)).First();
            Assert.True(Math.Abs(Vec2.Cross(cf.Direction, s.Direction)) < 1e-6);
        }
    }

    [Fact]
    public void BearingStiffenerWiderThanTopFlange_ClipToTopFlange_ClipsTheCornerAndStopsTheWeld()
    {
        // sample: 16" top flange, 18" bottom flange; an 8 1/2" stiffener plate reaches ~8.3" from the web CL = past the 8" top flange half width, inside the bottom flange
        void Wide(BridgeDefinition b) => b.Stiffeners.BearingStiffener = new PlateSize(In(8.5), In(1));
        var (_, off) = Build(Wide);
        var (_, on) = Build(b => { Wide(b); b.Stiffeners.ClipToTopFlange = true; });
        var bsOff = off.Parts.First(p => p.Category == PartCategory.BearingStiffener);
        var bsOn = on.Parts.First(p => p.Mark == bsOff.Mark);
        Assert.Contains(off.Diagnostics, d => d.Message.Contains("projects beyond the flange edge"));
        Assert.DoesNotContain(on.Diagnostics, d => d.Message.Contains("projects beyond the flange edge"));
        Assert.DoesNotContain("clipped", bsOff.Description);
        Assert.Contains("clipped to the", bsOn.Description);
        Assert.True(bsOn.Mesh.Volume() < bsOff.Mesh.Volume() - 1e-9, "the outer top corner is clipped off");
        var wOff = off.Parts.First(p => p.Mark == bsOff.Mark + "-W");
        var wOn = on.Parts.First(p => p.Mark == bsOff.Mark + "-W");
        Assert.True(wOn.Mesh.Volume() < wOff.Mesh.Volume(), "the top weld stops at the flange edge");
        // the default (flag off) leaves every other bridge untouched and the file lean
        Assert.DoesNotContain("ClipToTopFlange", System.Text.Json.JsonSerializer.Serialize(new StiffenerDefinition()));
    }

    [Fact]
    public void IntermediateStiffenerList_ReplacesTheAutomaticRule_AndIsLeanWhenUnused()
    {
        var (fAuto, _) = Build();
        int autoCount = fAuto.Stiffeners.Count(s => s.Kind == StiffenerKind.Intermediate && s.Girder == 0);
        Assert.True(autoCount > 2);
        var (f, _) = Build(b => b.Stiffeners.IntermediateLocations = new()
        {
            new() { Girder = 0, Span = 0, Reference = StiffenerLocationReference.SpanBegin, Distance = In(114) },
            new() { Girder = -1, Span = 0, Reference = StiffenerLocationReference.Panel, Panel = -1 },
            new() { Girder = 1, Span = 0, Reference = StiffenerLocationReference.SpanEnd, Distance = In(60), Unresolved = "UNRESOLVED (p.27): test position - not dimensioned." },
        });
        var g0 = f.Stiffeners.Where(s => s.Kind == StiffenerKind.Intermediate && s.Girder == 0).ToList();
        Assert.Equal(2, g0.Count);
        Assert.Equal(1, f.Stiffeners.Count(s => s.Kind == StiffenerKind.Intermediate && s.Girder == 2));
        var begin = f.Stiffeners.Where(s => s.Kind == StiffenerKind.Bearing && s.Girder == 0).Min(s => s.GirderDistance);
        Assert.Contains(g0, s => Math.Abs(s.GirderDistance - begin - In(114)) < 1e-6);
        Assert.Contains(f.Diagnostics, d => d.Message.Contains("test position - not dimensioned"));
        // default = no output change
        Assert.DoesNotContain("IntermediateLocations", System.Text.Json.JsonSerializer.Serialize(new StiffenerDefinition()));
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<StiffenerDefinition>(System.Text.Json.JsonSerializer.Serialize(new StiffenerDefinition
        { IntermediateLocations = new() { new() { Girder = 2, Span = 1, Reference = StiffenerLocationReference.Splice, SpliceNumber = 3, Distance = In(38) } } }))!;
        Assert.Equal(StiffenerLocationReference.Splice, roundTrip.IntermediateLocations![0].Reference);
        Assert.Equal(3, roundTrip.IntermediateLocations[0].SpliceNumber);
    }

    [Fact]
    public void Bolts_StayClearOfFlanges_AndGussetsClearMembers()
    {
        foreach (var type in new[] { CrossFrameType.XFrame, CrossFrameType.KFrame, CrossFrameType.InvertedKFrame, CrossFrameType.Diaphragm })
        {
            var (f, m) = Build(b => { b.CrossFrames.IntermediateType = type; b.CrossFrames.SupportType = type; });
            Assert.DoesNotContain(m.Diagnostics, d => d.Message.Contains("too short") || d.Message.Contains("flange;"));
            Assert.True(m.Bolts.Count > 0);
        }
    }

    [Fact]
    public void PlateChanges_SplitFlanges_WithTaper()
    {
        var (f, m) = Build(b =>
        {
            double pier = b.Supports[1].Station;
            b.Girders.Section.PlateChanges = new()
            {
                new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = pier - Ft(30), Width = In(18), Thickness = In(1) },
                new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = pier + Ft(30), Width = In(22), Thickness = In(2) },
                new PlateSegment { Plate = GirderPlate.BottomFlange, Width = In(18), Thickness = In(1) },
            };
        });
        var bf = m.Parts.Where(p => p.Category == PartCategory.FlangePlate && p.Mark.Contains("-BF")).ToList();
        Assert.Contains(bf, p => p.Description.Contains("22"));
        // Bottom of steel at the pier drops by the thicker flange.
        var pierBrg = f.Supports[1].Bearings[0]!;
        Assert.Equal(pierBrg.TopOfSteel.Z - In(1) - f.Definition.Girders.Section.WebDepth - In(2), pierBrg.BottomOfSteelElevation, 6);
    }

    [Fact]
    public void BearingSeat_IsBelowBottomFlange()
    {
        var (f, m) = Build(b => b.Bearings.Type = BearingType.Elastomeric);
        var bd = f.Definition.Bearings;
        foreach (var seat in m.BearingSeats)
        {
            var b = f.Bearings.First(x => x.Girder == seat.Girder && Math.Abs(x.Station - seat.Station) < 1e-9);
            double drop = b.BottomOfSteelElevation - seat.SeatElevation;
            Assert.InRange(drop, bd.PadHeight + bd.SolePlateMinThickness - 1e-6, bd.PadHeight + seat.SolePlateMaxThickness + 1e-6);
        }
    }
}
