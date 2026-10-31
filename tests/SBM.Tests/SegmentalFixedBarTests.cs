using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Segmental;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>OPTIONAL SegmentalDefinition.FixedBars: vertical PT bars fixed in the bent halves (positions from the support-side face = the CL split, mirrored about
/// CL girder, top end below the deck top, bottom end above the soffit), normal to the deck top in the section frame; length / count check rows; IFC 'VERTICAL_PT_BAR';
/// JSON omitted when unset.</summary>
public class SegmentalFixedBarTests
{
    [Fact]
    public void VerticalBars_InEveryBentHalf_FromTheSplitFace_NormalToTheTop()
    {
        var (p, b) = SegmentalTests.Model();
        b.Segmental!.FixedBars = new()
        {
            new() { Name = "vertical PT bar", Kind = SegmentalFixedBarKind.Vertical, SegmentType = SegmentType.BentHalf, From = SegmentalFaceRef.SupportSide,
                Positions = { new() { Along = Ft(1), X = Ft(3) + In(4) }, new() { Along = Ft(2.5), X = Ft(6) + In(4) } }, TopDepth = In(10.5), BottomHeight = In(6.5),
                Diameter = In(1.75), DuctDiameter = In(2.75), ListedLength = Ft(17) + In(10), PlanCount = 999, Source = "test" },
        };
        var m = new RegenerationCache().Regenerate(p).Single()!;
        var sm = m.Concrete!.Segmental!;
        var bars = sm.PtHardware.Where(h => h.Kind == SegmentalHardwareKind.VerticalPtBar).ToList();
        int halves = sm.Segments.Count(s => s.Type == SegmentType.BentHalf);
        Assert.Equal(halves * 2 * 2, bars.Count);
        foreach (var h in bars)
        {
            var seg = sm.Segments.First(s => s.Name == h.Segment);
            var chord = (seg.Top1 - seg.Top0).Normalized();
            var axis = (h.Points[1] - h.Points[0]).Normalized();
            Assert.True(Math.Abs(Vec3.Dot(chord, axis)) < 1e-6, "bar normal to the segment chord");
            Assert.True(axis.Z < -0.99, "top end first, pointing down");
        }
        // the first position is 1'-0" from the split face (the face nearer the bent control line)
        var one = bars.First(h => h.Name.EndsWith(" left 1"));
        var s0 = sm.Segments.First(s => s.Name == one.Segment);
        var chord0 = (s0.Top1 - s0.Top0).Normalized();
        double t = Vec3.Dot(one.Points[0] - s0.Top0, chord0), len = (s0.Top1 - s0.Top0).Length;
        bool splitAt0 = sm.Segments.Any(o => o != s0 && o.Type == SegmentType.BentHalf && Math.Abs(o.Station1 - s0.Station0) < 1e-6);
        Assert.Equal(Ft(1), splitAt0 ? t : len - t, 3);
        Assert.Contains(sm.Checks, c => c.Item.Contains("vertical PT bar: length between the modeled anchor ends"));
        Assert.Contains(sm.Checks, c => c.Item.Contains("vertical PT bar: count") && c.Model == bars.Count);

        var sw = new StringWriter();
        IfcExporter.Export(sw, "seg.ifc", p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        Assert.Contains("'VERTICAL_PT_BAR'", sw.ToString());
        Assert.Single(BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!.FixedBars!);
        Assert.DoesNotContain("FixedBars", SegmentalTests.Model().p.ToJson());
    }
}
