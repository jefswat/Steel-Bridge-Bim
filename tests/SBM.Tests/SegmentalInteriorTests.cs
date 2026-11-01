using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Segmental;
using SBM.Core.Solids;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>OPTIONAL SegmentalDefinition.Interiors (segment built as closed pieces along it, each with its own void / outer outline), diaphragm openings over
/// AlongFrom..AlongTo, bent slab openings at AlongFrom, slab openings by plan outline from the support-side face; JSON omitted when unset.</summary>
public class SegmentalInteriorTests
{
    private static List<PlanPoint> Pts(IEnumerable<Vec2> v) => v.Select(q => new PlanPoint(q.X, q.Y)).ToList();

    [Fact]
    public void Interiors_PiecesClosed_HollowAndSolidZones_OuterOverride()
    {
        var (p, b) = SegmentalTests.Model();
        var sg = b.Segmental!;
        double h = In(231), hb = In(228);
        var cell = SegmentalSection.Void(sg.Box, h, In(33)).ToList();
        var bulk = SegmentalSection.Void(sg.Box, hb, In(24)).ToList();
        sg.Interiors = new()
        {
            new() { Name = "bent", SegmentType = SegmentType.BentHalf, Source = "test", Pieces =
            {
                new() { From = 0, To = Ft(1.5), Void0 = Pts(cell) },
                new() { From = Ft(1.5), To = Ft(3.5) },
                new() { From = Ft(3.5), To = Ft(5), Void0 = Pts(cell), Void1 = Pts(bulk) },
            } },
            new() { Name = "end", SegmentType = SegmentType.End, Source = "test", Unresolved = "UNRESOLVED (p.1): test.", Pieces =
            {
                new() { From = 0, To = In(11), Outer0 = new() { new(0, 0), new(In(100), 0), new(In(100), In(26)), new(0, In(26)), new(-In(100), In(26)), new(-In(100), 0) } },
                new() { From = In(11), To = Ft(9.5) - Ft(10.0 / 12) - 0, Void0 = Pts(SegmentalSection.Void(sg.Box, sg.ConstantDepth, sg.ConstantBottomSlab)) },
            } },
        };
        var m = new RegenerationCache().Regenerate(p).Single()!;
        var sm = m.Concrete!.Segmental!;
        var halves = sm.Segments.Where(s => s.Type == SegmentType.BentHalf).ToList();
        Assert.Equal(2, halves.Count);
        foreach (var r in halves)
        {
            Assert.Equal(3, r.Pieces.Count);
            Assert.All(r.Pieces, pc => Assert.True(SegmentalPtHardwareTests.Closed(pc)));
            Assert.Equal(r.Pieces.Sum(pc => pc.Volume()), r.Mesh.Volume(), 9);
            Assert.Null(r.Assumption);
        }
        // the solid piece is the full outer section over 2'-0"; the hollow pieces are outer minus void
        var hv = halves[0];
        double solidArea = SegmentalSection.SolidArea(sg.Box, h);
        Assert.Equal(solidArea * Ft(2), hv.Pieces[1].Volume(), 2);
        Assert.Equal((solidArea - Math.Abs(MeshBuilder.SignedArea(cell))) * Ft(1.5), hv.Pieces[0].Volume(), 2);
        // end segment: nose piece = the given outer outline (200" x 26") over 11"
        var es = sm.Segments.First(s => s.Type == SegmentType.End);
        Assert.Equal(2, es.Pieces.Count);
        Assert.Equal(1, Math.Abs(es.Pieces[0].Volume()) / (In(200) * In(26) * In(11)), 3);   // length along the graded chord vs stations
        Assert.NotNull(es.Assumption);
        Assert.Contains(sm.Diagnostics, d => d.Message.Contains("Segmental interior 'bent': 2 BentHalf segments built as 3 pieces (1 solid)"));
        Assert.DoesNotContain(sm.Diagnostics, d => d.Message.Contains("pier diaphragm openings and the 3\" soffit step length"));

        Assert.Equal(2, BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!.Interiors!.Count);
        Assert.DoesNotContain("Interiors", SegmentalTests.Model().p.ToJson());
        Assert.DoesNotContain("Outer0", SegmentalTests.Model().p.ToJson());
    }

    [Fact]
    public void Openings_DiaphragmRange_BentSlabAtDistance_PlanOutlineFromSupportFace()
    {
        var (p, b) = SegmentalTests.Model();
        var sg = b.Segmental!;
        sg.Openings = new()
        {
            new() { Name = "diaphragm opening", Kind = SegmentalOpeningKind.BentDiaphragm, OutlineFromTop = true, AlongFrom = Ft(1.5), AlongTo = Ft(3.5), Bents = { "S1" },
                Outline = { new(-In(30), In(198)), new(In(30), In(198)), new(In(30), In(102)), new(-In(30), In(102)) } },
            new() { Name = "pintle", Kind = SegmentalOpeningKind.BottomSlab, Diameter = In(8), CenterOffset = In(51), AlongFrom = In(13), Bents = { "S1" } },
            new() { Name = "notch", Kind = SegmentalOpeningKind.BottomSlab, Segments = { "2-ES" }, PlanOutline = new() { new(-In(36), In(11)), new(-In(21), In(26)), new(In(21), In(26)), new(In(36), In(11)) } },
        };
        var m = new RegenerationCache().Regenerate(p).Single()!;
        var sm = m.Concrete!.Segmental!;
        var dia = sm.Openings.Where(o => o.Name.EndsWith("diaphragm opening")).ToList();
        Assert.Equal(2, dia.Count);
        Assert.All(dia, o => Assert.Equal(In(60) * In(96) * Ft(2), o.RemovedVolume, 6));
        // the opening void covers 1'-6" .. 3'-6" from the bent CL (+ the 2 cm margins)
        foreach (var o in dia)
        {
            var bb = o.Mesh.Bounds();
            Assert.True(bb.Max.Z - bb.Min.Z > In(95));
        }
        var pin = sm.Openings.Where(o => o.Name.EndsWith("pintle")).ToList();
        Assert.Equal(2, pin.Count);                                                         // one whole opening in each half
        Assert.All(pin, o => Assert.Contains("1.083 ft from the S1 CL", o.Description));
        var notch = Assert.Single(sm.Openings, o => o.Name.EndsWith("notch"));
        Assert.Equal((In(72) + In(42)) / 2 * In(15) * sg.ConstantBottomSlab, notch.RemovedVolume, 6);
        Assert.DoesNotContain("PlanOutline", SegmentalTests.Model().p.ToJson());
    }

    [Fact]
    public void Pier_HollowSegments_SlopedFooting_CapOpening_PtBars_BarList()
    {
        var (p, b) = SegmentalTests.Model();
        var pier = b.Segmental!.Bents[0].Pier!;
        // cap 55.0, height 5.5 -> soffit 49.5; base to 17.0, grout 0.25, segments 12 + 12 + 8.25 = 32.25 -> 49.5
        pier.GroutBed = In(3);
        pier.ColumnSegments = new() { Ft(12), Ft(12), Ft(8.25) };
        pier.ColumnCore = new[] { (-5.5, -4.0), (5.5, -4.0), (6.5, -3.0), (6.5, 3.0), (5.5, 4.0), (-5.5, 4.0), (-6.5, 3.0), (-6.5, -3.0) }.Select(q => new PlanPoint(Ft(q.Item1), Ft(q.Item2))).ToList();
        pier.FootingLevelZone = new() { new(-Ft(8), -Ft(5.5)), new(Ft(8), -Ft(5.5)), new(Ft(8), Ft(5.5)), new(-Ft(8), Ft(5.5)) };
        pier.FootingTopEdge = Ft(6.65);
        pier.CapOpenings = new() { new() { new(-Ft(1.5), -Ft(2.75)), new(Ft(1.5), -Ft(2.75)), new(Ft(1.5), Ft(2.75)), new(-Ft(1.5), Ft(2.75)) } };
        // T cap with both stem chamfer corners on one line (bent 18 shape): the cap faces must still conform to the walls
        pier.CapTaperInset = 0; pier.CapTaperHeight = 0;
        pier.CapOutline = new[] { (-21.75, -6.5), (21.75, -6.5), (21.75, 1.5), (12.5, 1.5), (11.5, 2.5), (11.5, 9.5), (-11.5, 9.5), (-11.5, 2.5), (-12.5, 1.5), (-21.75, 1.5) }
            .Select(q => new PlanPoint(Ft(q.Item1), Ft(q.Item2))).ToList();
        pier.PtBars = new() { new() { Name = "PT 1", S = Ft(2.25), N = Ft(4.75), Bottom = Ft(2), Top = Ft(53.5), Diameter = In(2.5), DuctDiameter = In(4), PlateWidth = In(12), PlateThickness = In(3.5) } };
        pier.BarList = new() { new() { Mark = "T1", Size = "#6", Count = 45, Length = Ft(37) + In(4), Coating = "stainless", Element = "footing", PlanWeightLb = 2524 } };
        var m = new RegenerationCache().Regenerate(p).Single()!;
        var sm = m.Concrete!.Segmental!;
        var unit = sm.Substructure!.Units.Single();
        var segs = unit.Solids.Where(s => s.Name.Contains("column segment")).ToList();
        Assert.Equal(3, segs.Count);
        double ring = Ft(16) * Ft(11) - (Ft(13) * Ft(8) - 4 * 0.5 * Ft(1) * Ft(1));
        Assert.Equal(ring * Ft(12), Math.Abs(segs[0].Mesh.Volume()), 6);
        Assert.All(unit.Solids, s => Assert.True(SegmentalPtHardwareTests.Closed(s.Mesh), s.Name));
        Assert.Contains(unit.Solids, s => s.Name.EndsWith("grout bed"));
        Assert.Contains(sm.Checks, c => c.Item.Contains("column stack") && Math.Abs(c.Difference) < 1e-6);
        // sloped footing: volume between the flat-at-CL and flat-at-edge boxes
        var ftg = unit.Solids.Single(s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing);
        double area = Ft(51) * Ft(36), v = Math.Abs(ftg.Mesh.Volume());
        Assert.True(v < area * (Ft(7) + Ft(0.5)) && v > area * (Ft(6.65) + Ft(0.5)));
        Assert.Equal(Ft(7), ftg.Mesh.Bounds().Max.Z, 6);
        // cap with the access opening: tapered volume less 3 x 5.5 x 5.5 ft
        var cap = unit.Solids.Single(s => s.Kind == SBM.Core.Substructure.SubElementKind.Cap);
        Assert.Contains("access opening", cap.Description);
        Assert.Single(unit.Solids, s => s.Kind == SBM.Core.Substructure.SubElementKind.PtBar);
        Assert.Single(unit.Solids, s => s.Kind == SBM.Core.Substructure.SubElementKind.PtAnchor);
        Assert.Contains(sm.Substructure.Marks, mk => mk.Mark == "T1" && mk.Unit == "S1" && mk.Count == 45);
        Assert.Contains(sm.Checks, c => c.Item.Contains("pier bar bill") && Math.Abs(c.Difference) < 15);
        var sw = new StringWriter();
        SBM.IO.Ifc.IfcExporter.Export(sw, "seg.ifc", p, new[] { new SBM.IO.Ifc.IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        Assert.Contains("'PIER_PT_BAR'", sw.ToString());
        Assert.DoesNotContain("ColumnSegments", SegmentalTests.Model().p.ToJson());
        Assert.Equal(3, BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!.Bents[0].Pier!.ColumnSegments!.Count);
    }
}
