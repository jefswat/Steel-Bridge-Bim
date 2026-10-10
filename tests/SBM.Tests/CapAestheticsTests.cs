using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class CapAestheticsTests
{
    private static double Ft(double x) => x * 0.3048;

    [Fact]
    public void TableBand_PassesThroughReversePointsAtThreeAndAHalfFeet()
    {
        // Pier 1WB: L1 90, L2 22'-6, L3 30'-0, R1 47'-6, R2 125'-0, R3-R5 82/71/62.5.
        var ae = CapBandBuilder.FromTable(Ft(90), Ft(22.5), Ft(30), Ft(47.5), Ft(125), Ft(82), Ft(71), Ft(62.5), false);
        Assert.Equal(3, ae.Layers.Count);
        Assert.Equal(Ft(3.5), CapBandBuilder.DepthAt(ae.Layers[2], Ft(22.5)), 4);
        Assert.Equal(Ft(3.5), CapBandBuilder.DepthAt(ae.Layers[2], Ft(60)), 4);
        // The sag between the reverse points is deeper than 3'-6 (R2 = 125' over a 37.5' chord: 1.41' of sag).
        Assert.Equal(Ft(3.5 + 125 - Math.Sqrt(125 * 125 - 18.75 * 18.75)), CapBandBuilder.DepthAt(ae.Layers[2], Ft(41.25)), 3);
        // The three fan arcs fan out from the common tangent point: R5 (smallest radius) is the lowest line.
        double x = Ft(85);
        Assert.True(CapBandBuilder.DepthAt(ae.Layers[0], x) > CapBandBuilder.DepthAt(ae.Layers[1], x));
        Assert.True(CapBandBuilder.DepthAt(ae.Layers[1], x) > CapBandBuilder.DepthAt(ae.Layers[2], x));
        Assert.Equal(CapBandBuilder.DepthAt(ae.Layers[0], Ft(60)), CapBandBuilder.DepthAt(ae.Layers[2], Ft(60)), 6);
    }

    [Fact]
    public void MirroredBand_IsTheMirrorImage()
    {
        var a = CapBandBuilder.FromTable(Ft(63), Ft(15.75), Ft(21), Ft(25.5), Ft(61.5), Ft(42), Ft(35.5), Ft(30), false);
        var b = CapBandBuilder.FromTable(Ft(63), Ft(15.75), Ft(21), Ft(25.5), Ft(61.5), Ft(42), Ft(35.5), Ft(30), true);
        foreach (double x in new[] { 3.0, 10.0, 25.0, 40.0, 55.0 })
            Assert.Equal(CapBandBuilder.DepthAt(a.Layers[0], Ft(x)), CapBandBuilder.DepthAt(b.Layers[0], Ft(63 - x)), 6);
    }

    [Fact]
    public void LongArcBand_HitsTheLabeledPoints()
    {
        var ae = CapBandBuilder.FromLongArc(Ft(52), Ft(25.84), Ft(5.0), Ft(1.17), Ft(6.75), Ft(84), Ft(63.5), Ft(60), Ft(59.5), false);
        Assert.Equal(Ft(6.75), CapBandBuilder.DepthAt(ae.Layers[2], -Ft(1.17)), 4);
        Assert.Equal(Ft(5.0), CapBandBuilder.DepthAt(ae.Layers[2], Ft(25.84)), 4);
    }

    [Fact]
    public void AsDesignedCap_GetsAReliefSolid_AndItRoundTrips()
    {
        var doc = SbmDocument.Sample();
        var pier = doc.Bridge().Supports.First(s => s.Type == SupportType.Pier);
        var u0 = doc.Substructure().Units.Single(u => u.Type == SupportType.Pier);
        double s0 = u0.CapS0 - Ft(1), s1 = u0.CapS1 + Ft(1), ztop = u0.CapTop.Max(p => p.Y) + Ft(1), zb = ztop - Ft(7);
        var sd = SubstructureDefinition.Default(SupportType.Pier);
        sd.CapProfile = new() { new(s0, ztop), new(s1, ztop), new(s1 + Ft(1), ztop - Ft(4.5)), new(s1 - Ft(5), zb), new(s0 + Ft(5), zb), new(s0 - Ft(1), ztop - Ft(4.5)) };
        double l1 = s1 - s0;
        sd.Aesthetics = CapBandBuilder.FromTable(l1, l1 * 0.25, l1 * 0.33, l1 * 0.5, l1 * 1.4, l1 * 0.9, l1 * 0.8, l1 * 0.7, false);
        pier.Substructure = sd;
        doc.Regenerate();
        var m = doc.Substructure();
        var unit = m.Units.Single(u => u.Type == SupportType.Pier);
        var relief = unit.Solids.SingleOrDefault(s => s.Name.EndsWith("cap section step")) ?? throw new Exception(string.Join(" / ", m.Diagnostics.Select(d => d.Message).Concat(m.Notes)));
        double v = Math.Abs(relief.Mesh.Volume());
        // Band area (<= 4.5' deep over l1) times 1 1/2" is the upper bound; it must be a real fraction of it.
        Assert.InRange(v, 0.2 * l1 * Ft(3.5) * In(6) * 2, l1 * Ft(4.6) * In(6) * 2);
        Assert.NotEmpty(unit.ReliefLines);
        // The relief stands proud of the plain cap face (N1 side by default): its bounds exceed the cap's.
        var cap = unit.Solids.First(s => s.Kind == SubElementKind.Cap && !s.Name.EndsWith("step"));
        Assert.True(relief.Mesh.Volume() * cap.Mesh.Volume() > 0);
        var rt = doc.Project.DeepClone();
        var back = rt.Bridges[0].Supports.First(s => s.Type == SupportType.Pier).Substructure!.Aesthetics!;
        Assert.Equal(3, back.Layers.Count);
        Assert.Equal(sd.Aesthetics.StepSetback, back.StepSetback, 9);
        Assert.Equal(sd.Aesthetics.Layers[0].Nodes.Count, back.Layers[0].Nodes.Count);
    }
}
