using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class MiscItemsTests
{
    [Fact]
    public void HollowTube_HasTheRightVolume_AndIsAnExactExtrusion()
    {
        var m = MiscMesh.Tube(new Vec3(5, 6, 0), new Vec3(5, 6, 3), 0.1, 0.09, 48);
        double exact = Math.PI * (0.1 * 0.1 - 0.09 * 0.09) * 3;
        Assert.InRange(m.Volume() / exact, 0.97, 1.0);   // inscribed polygons
        Assert.True(m.HasExactSolids);
        Assert.Single(m.Solids!);
        Assert.Single(m.Solids![0].Holes);
    }

    [Fact]
    public void SegmentTriangleDistance_AndPipeClearance()
    {
        var (d, at) = MiscMesh.SegmentTriangle(new Vec3(0, 0, 0), new Vec3(0, 0, 10), new Vec3(1, -1, 4), new Vec3(1, 2, 4), new Vec3(1, -1, 7));
        Assert.Equal(1.0, d, 6);
        Assert.InRange(at.Z, 4, 7);
        // a box 0.5 m from a vertical pipe of radius 0.1: clearance 0.4
        var box = new Mesh();
        MeshBuilder.AddBox(box, Frame3.World, 0.5, 1.5, -1, 1, 0, 3);
        var part = new SBM.Core.Steel.SteelPart { Mark = "B", Category = SBM.Core.Steel.PartCategory.FlangePlate, Assembly = "A", Description = "box", Mesh = box };
        var (c, against, _) = MiscMesh.Clearance(MiscMesh.Index(new[] { part }), new Vec3(0, 0, 0), new Vec3(0, 0, 3), 0.1);
        Assert.Equal(0.4, c, 6);
        Assert.Contains("B", against);
    }

    private static SbmDocument Doc()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    [Fact]
    public void TrenchDrain_IsPositionedFromTheGutterLine_AndReportsClearance()
    {
        var doc = Doc();
        var b = doc.Bridge();
        b.Deck.EdgeStrip = new DeckEdgeStrip { Width = Ft(1) + In(7) };
        double sta = b.Supports[0].Station + Ft(70);
        b.Drains.Add(new DeckDrainDefinition { Station = sta, Left = true, Trench = new DrainTrenchDefinition() });
        Assert.Empty(doc.Regenerate().Errors);
        var misc = doc.Substructure().Misc;
        Assert.NotNull(misc);
        var tube = misc!.Elements.Single(e => e.Kind == MiscKind.DrainTube);
        var bb = tube.Mesh.Bounds();
        var c = new Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2);
        var so = doc.Framing().Alignment.Inverse(c);
        double edge = b.Deck.LeftAt(sta), gut = edge + Ft(1) + In(7);
        // left side: inboard = increasing offset; tube centre 3'-0 1/2 from the gutter line
        Assert.Equal(gut + Ft(3) + In(0.5), so.Offset, 3);
        Assert.Equal(sta, so.Station, 1);
        // outer diameter 8"
        Assert.Equal(In(8), bb.Max.X - bb.Min.X, 1);
        Assert.Contains(misc.Clearances, x => x.Drain.StartsWith("Drain 1") && x.Element == "tube");
        // the trench starts 1/2" clear of the gutter line and is 4'-0 long: inboard extent
        var tr = misc.Elements.Single(e => e.Kind == MiscKind.DrainTrench).Mesh.Bounds();
        Assert.NotNull(misc.Elements.Single(e => e.Kind == MiscKind.DrainTrench).Opening);
        Assert.True(tr.Max.Z > tr.Min.Z);
    }

    [Fact]
    public void TubeOverAGirderFlange_IsAConflictWarning_AndTheDrainIsNotMoved()
    {
        var doc = Doc();
        var b = doc.Bridge();
        b.Deck.EdgeStrip = new DeckEdgeStrip { Width = Ft(1) + In(7) };
        double sta = b.Supports[0].Station + Ft(70);
        var f = doc.Framing();
        var g0 = f.Girders[0];
        var web = g0.Path.WebTop[g0.Path.Count / 2];
        double gOff = f.Alignment.Inverse(web.XY).Offset;
        double sta0 = f.Alignment.Inverse(web.XY).Station;
        double gut = b.Deck.LeftAt(sta0) + Ft(1) + In(7);
        // tube exactly over the CL of girder 1 (a drain the plans would never draw)
        b.Drains.Add(new DeckDrainDefinition { Station = sta0, Left = true, Trench = new DrainTrenchDefinition { TubeFromGutter = gOff - gut } });
        Assert.Empty(doc.Regenerate().Errors);
        var misc = doc.Substructure().Misc!;
        var cl = misc.Clearances.First(x => x.Element == "tube");
        Assert.True(cl.Clearance <= 0, $"clearance {cl.Clearance}");
        Assert.Contains(misc.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("CONFLICT"));
    }

    [Fact]
    public void UnresolvedItems_AreRaised_AndBarListsAreScheduledOnly()
    {
        var doc = Doc();
        var b = doc.Bridge();
        b.Misc = new MiscItemsDefinition();
        b.Misc.Unresolved.Add("test item");
        b.Misc.EndPosts.Add(new CrashCushionEndPostDefinition
        {
            Name = "End post", Support = b.Supports[1].Name,
            Bars = { new MiscBarRow { Mark = "6d1", Size = "#6", Count = 24, Length = Ft(3) + In(7), Epoxy = true }, new MiscBarRow { Mark = "6c3", Size = "#6", Count = 10, WeightLb = 100, Coating = "stainless", Epoxy = false } },
        });
        Assert.Empty(doc.Regenerate().Errors);
        var misc = doc.Substructure().Misc!;
        Assert.Contains(misc.Diagnostics, d => d.Message.StartsWith("UNRESOLVED test item"));
        Assert.Contains(misc.Diagnostics, d => d.Message.Contains("NOT placed"));
        Assert.Empty(misc.Elements);
        // the plan's weights: 24 x 3'-7 #6 = 129 lb; the varying bar takes the printed 100 lb
        Assert.Equal(129, misc.Marks.Single(m => m.Mark == "6d1").TotalMass / 0.45359237, 0);
        Assert.Equal(100, misc.Marks.Single(m => m.Mark == "6c3").TotalMass / 0.45359237, 0);
        Assert.Equal("stainless", misc.Marks.Single(m => m.Mark == "6c3").Coating);
    }

    [Fact]
    public void Lighting_BuildsPoleBaseJunctionBoxAndConduit_AndIfcCarriesThem()
    {
        var doc = Doc();
        var b = doc.Bridge();
        string pier = b.Supports[1].Name;
        b.Misc = new MiscItemsDefinition
        {
            Lighting = new LightingDefinition
            {
                PoleBases = { new PoleBaseDefinition { Name = "Pole base", Support = pier, AlongFromSupport = 0, Side = LightingSide.Right, BoltCircleDiameter = In(13.5) } },
                JunctionBoxes = { new JunctionBoxDefinition { Name = "JB", Support = pier, AlongFromSupport = Ft(2.5), Side = LightingSide.Right } },
                Conduits = { new ConduitRunDefinition { Name = "ITS conduit", Side = LightingSide.Right, Kind = JunctionBoxKind.Its, FromStation = b.Supports[0].Station, ToStation = b.Supports[^1].Station, HeightAboveBase = Ft(1) + In(8) } },
            },
        };
        Assert.Empty(doc.Regenerate().Errors);
        var misc = doc.Substructure().Misc!;
        Assert.Contains(misc.Elements, e => e.Kind == MiscKind.PoleBaseConcrete);
        Assert.Equal(4, misc.Elements.Count(e => e.Kind == MiscKind.PoleAnchorBolt) * 4);
        var jb = misc.Elements.Single(e => e.Kind == MiscKind.JunctionBox);
        Assert.True(jb.Placeholder);
        Assert.Contains(misc.Elements, e => e.Kind == MiscKind.Conduit);
        // block: 2'-0 along the barrier, 2'-6 out; its mesh is a closed solid
        var block = misc.Elements.Single(e => e.Kind == MiscKind.PoleBaseConcrete).Mesh;
        Assert.InRange(block.Volume() / 0.764554857984, 0.3, 1.2);   // plans: 1.0 CY including the blockout
        var f = doc.Framing();
        var p = doc.Project;
        var sw = new StringWriter();
        var m = doc.Model();
        IfcExporter.Export(sw, "t.ifc", p, new[] { new IfcBridgeData(f, m.Steel, m.Concrete, m.Substructure) });
        string ifc = sw.ToString();
        Assert.Contains("IFCJUNCTIONBOX(", ifc);
        Assert.Contains("IFCCABLECARRIERSEGMENT(", ifc);
        Assert.Contains(".CONDUITSEGMENT.", ifc);
        Assert.Contains(".ANCHORBOLT.", ifc);
        Assert.Contains("'SBM_Placeholder'", ifc);
    }
}
