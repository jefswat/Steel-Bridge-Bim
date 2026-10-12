using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Northaven (TxDOT CSJ 0047-07-228) features: span-layout bearing offsets from the support line (PIER REPORT girder positions at
/// CL pier, CL bearing 1'-0 along the girder / normal to the support), fixed-elevation pedestal cap blocks (arch seats on the arch piers)
/// and per-row pedestal size / side angle.</summary>
public class NorthavenFeatureTests
{
    private static SbmDocument Prestressed()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    [Fact]
    public void BearingOffset_MovesTheLayoutPointAlongTheChord_AndTheBeamEnds()
    {
        var doc = Prestressed();
        var b = doc.Bridge();
        int n = b.Girders.Count;
        var offs = b.Girders.UniformOffsets();
        b.Girders.SpanLayouts = new()
        {
            new GirderSpanLayout
            {
                Span = 0, Count = n,
                Begin = new() { Offsets = offs.ToList() },
                End = new() { Offsets = offs.ToList(), BearingOffset = Ft(1), BearingMeasure = BearingOffsetMeasure.AlongGirder },
            },
            new GirderSpanLayout
            {
                Span = 1, Count = n,
                Begin = new() { Offsets = offs.ToList(), BearingOffset = Ft(1.5), BearingMeasure = BearingOffsetMeasure.NormalToSupport },
                End = new() { Offsets = offs.ToList() },
            },
        };
        Assert.Empty(b.Validate());
        var r = doc.Regenerate();
        Assert.Empty(r.Errors);
        var f = doc.Framing(b.Name);
        var ps = doc.Prestressed(b.Name);
        double end = ps.Definition.BearingToEnd;
        for (int i = 0; i < n; i++)
        {
            // span 1 end: the layout point is 1 ft from the support-line point ALONG the chord, into the span
            var c0 = f.Girders[i].Spans[0];
            var t0 = (c0.End - c0.Start).Normalized();
            var le = c0.LayoutEnd!.Value.XY;
            Assert.Equal(Ft(1), Vec2.Distance(le, c0.End), 6);
            Assert.True(Vec2.Dot(c0.End - le, t0) > 0);
            // span 2 begin: 1.5 ft from the support line NORMAL to it
            var c1 = f.Girders[i].Spans[1];
            var lb = c1.LayoutBegin!.Value.XY;
            var sup = f.Supports[1];
            Assert.Equal(Ft(1.5), Math.Abs(Vec2.Dot(lb - sup.AlignmentPoint, sup.Direction.Left)), 6);
            Assert.True(Math.Abs(Vec2.Cross(c1.End - c1.Start, lb - c1.Start)) < 1e-6);   // still on the chord
            // beams: CL bearings = layout points; ends 9" beyond them (along the CL at the along-girder end, normal at the normal end)
            var t1 = (c1.End - c1.Start).Normalized();
            double cos = Math.Abs(Vec2.Dot(t1, sup.Direction.Left));
            var beam1 = ps.Beams.First(x => x.Span == 1 && x.Girder == i);
            Assert.Equal(Vec2.Distance(lb, c1.End), beam1.BearingSpan, 6);
            double endAbut = b.Supports[2].Type == SupportType.Abutment ? end / Math.Abs(Vec2.Dot(t1, f.Supports[2].Direction.Left)) : 0;
            if (b.Supports[2].Type == SupportType.Abutment) Assert.Equal(beam1.BearingSpan + end / cos + endAbut, beam1.Length, 6);
            var beam0 = ps.Beams.First(x => x.Span == 0 && x.Girder == i);
            Assert.Equal(Vec2.Distance(c0.Start, le), beam0.BearingSpan, 6);
        }
        // the inputs survive a project round trip; defaults are not written
        var rt = doc.Project.DeepClone().Bridges[0].Girders.SpanLayouts![1].Begin;
        Assert.Equal(Ft(1.5), rt.BearingOffset, 9);
        Assert.Equal(BearingOffsetMeasure.NormalToSupport, rt.BearingMeasure);
        b.Girders.SpanLayouts[0].End.BearingOffset = -1;
        Assert.Contains(b.Validate(), e => e.Contains("bearing offset"));
    }

    private static (SbmDocument doc, SubstructureDefinition sd) PierWithProfile(SbmDocument doc)
    {
        var u0 = doc.Substructure().Units.First(u => u.Type == SupportType.Pier);
        double s0 = u0.CapS0 - Ft(1), s1 = u0.CapS1 + Ft(1), ztop = u0.Pedestals.Min(p => p.SeatZ) - In(6), zb = ztop - Ft(5);
        var sd = SubstructureDefinition.Default(SupportType.Pier);
        sd.CapProfile = new() { new(s0, ztop), new(s1, ztop), new(s1, zb), new(s0, zb) };
        sd.CapWidth = Ft(6);
        sd.MinPedestalHeight = 0;
        return (doc, sd);
    }

    [Fact]
    public void PedestalCapBlock_PublishesAFixedSeat_AndRaisesItsReportedDifferences()
    {
        var doc = SbmDocument.Sample();
        doc.Regenerate();
        var (_, sd) = PierWithProfile(doc);
        double ztop = sd.CapProfile.Max(p => p.Elevation), s1 = sd.CapProfile.Max(p => p.S);
        double sSeat = s1 - Ft(3), nSeat = In(6.875);
        sd.CapBlocks = new()
        {
            new CapBlockDefinition
            {
                Name = "arch bearing south", Kind = CapBlockKind.Pedestal, TopElevation = ztop + Ft(1.5), SeatS = sSeat, SeatN = nSeat,
                Outline = { new(sSeat - Ft(1), nSeat - Ft(0.75)), new(sSeat + Ft(1), nSeat - Ft(0.75)), new(sSeat + Ft(1), nSeat + Ft(0.75)), new(sSeat - Ft(1), nSeat + Ft(0.75)) },
                Reported = new() { "pedestal top does not reconcile (test)" },
            },
        };
        doc.Bridge().Supports.First(s => s.Type == SupportType.Pier).Substructure = sd;
        doc.Regenerate();
        var m = doc.Substructure();
        var u = m.Units.First(x => x.Type == SupportType.Pier);
        var fs = Assert.Single(u.FixedSeats);
        Assert.Equal("arch bearing south", fs.Name);
        Assert.Equal(sSeat, fs.S, 9);
        Assert.Equal(nSeat, fs.N, 9);
        Assert.Equal(ztop + Ft(1.5), fs.Z, 9);
        var solid = u.Solids.Single(s => s.Name.EndsWith("arch bearing south"));
        Assert.Equal(SubElementKind.Pedestal, solid.Kind);
        Assert.Equal(Ft(2) * Ft(1.5) * Ft(1.5), Math.Abs(solid.Mesh.Volume()), 4);
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("pedestal top does not reconcile (test)"));
        var rt = doc.Project.DeepClone().Bridges[0].Supports.First(s => s.Type == SupportType.Pier).Substructure!.CapBlocks[0];
        Assert.Equal(CapBlockKind.Pedestal, rt.Kind);
        Assert.Equal(sSeat, rt.SeatS!.Value, 9);
        Assert.Single(rt.Reported!);
    }

    [Fact]
    public void PedestalRows_TakeTheirOwnAlongSizeAndSideAngle()
    {
        var doc = Prestressed();
        doc.Regenerate();
        var b = doc.Bridge();
        var pier = b.Supports.First(s => s.Type == SupportType.Pier);
        var sd = pier.Substructure!;
        sd.PedestalSizeAlong = Ft(3); sd.PedestalSizeAcross = Ft(1.75); sd.PedestalRowJoint = 0; sd.MinPedestalHeight = In(1.5);
        sd.PedestalSideAngleDeg = 10;
        sd.PedestalSizeAlongAhead = Ft(3.5); sd.PedestalSideAngleAheadDeg = -15;
        doc.Regenerate();
        var u = doc.Substructure().Units.First(x => x.Type == SupportType.Pier);
        var backBlocks = u.PedestalBlocks.Where(p => p.N < u.CapMidN).ToList();
        var aheadBlocks = u.PedestalBlocks.Where(p => p.N > u.CapMidN).ToList();
        Assert.NotEmpty(backBlocks);
        Assert.NotEmpty(aheadBlocks);
        Assert.All(backBlocks, p => Assert.Equal(Ft(3), p.LengthS, 9));
        Assert.All(aheadBlocks, p => Assert.Equal(Ft(3.5), p.LengthS, 9));
        var solids = u.Solids.Where(s => s.Kind == SubElementKind.Pedestal).ToList();
        Assert.Contains(solids, s => s.Description.Contains("sides 10 deg"));
        Assert.Contains(solids, s => s.Description.Contains("sides -15 deg"));
        // without the ahead inputs every block keeps the common size (backward compatible)
        sd.PedestalSizeAlongAhead = null; sd.PedestalSideAngleAheadDeg = null;
        doc.Regenerate();
        u = doc.Substructure().Units.First(x => x.Type == SupportType.Pier);
        Assert.All(u.PedestalBlocks, p => Assert.Equal(Ft(3), p.LengthS, 9));
    }
}
