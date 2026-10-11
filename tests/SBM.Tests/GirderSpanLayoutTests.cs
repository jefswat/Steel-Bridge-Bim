using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Checks;
using SBM.Core.Geometry;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Per-span girder layouts (straight chords with their own offsets at each span end) and the profile grade line /
/// cross-slope pivot offset.</summary>
public class GirderSpanLayoutTests
{
    private static SbmDocument Prestressed()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    private static List<double> Shifted(BridgeDefinition b, double d) => b.Girders.UniformOffsets().Select(o => o + d).ToList();

    private static bool OnLine(Vec2 p, Vec2 a, Vec2 dir) => Math.Abs(Vec2.Cross(dir, p - a)) < 1e-6;

    [Fact]
    public void ChordsUseTheirOwnOffsetsAndJogAtThePier()
    {
        var doc = Prestressed();
        var b = doc.Bridge();
        var al = doc.Alignment(b.AlignmentName);
        int n = b.Girders.Count;
        b.Girders.SpanLayouts = new()
        {
            new GirderSpanLayout { Span = 0, Count = n, Begin = new() { Offsets = Shifted(b, 0) }, End = new() { Offsets = Shifted(b, Ft(0.5)) } },
            new GirderSpanLayout
            {
                Span = 1, Count = n, Begin = new() { Offsets = Shifted(b, -Ft(0.4)), Haunches = Enumerable.Repeat(In(2), n).ToList() },
                End = new() { Offsets = Shifted(b, Ft(0.2)) },
            },
        };
        Assert.Empty(b.Validate());
        var r = doc.Regenerate(includeSteel: false);
        Assert.Empty(r.Errors);
        var f = doc.Framing(b.Name);
        var expected = b.Girders.UniformOffsets();
        for (int i = 0; i < n; i++)
        {
            var g = f.Girders[i];
            for (int j = 0; j < 2; j++)
            {
                var c = g.Spans[j];
                // The layout points are on the given offset curves and on the support lines (no line shift).
                double ob = expected[i] + (j == 0 ? 0 : -Ft(0.4)), oe = expected[i] + (j == 0 ? Ft(0.5) : Ft(0.2));
                Assert.Equal(ob, al.Inverse(c.LayoutBegin!.Value.XY).Offset, 6);
                Assert.Equal(oe, al.Inverse(c.LayoutEnd!.Value.XY).Offset, 6);
                Assert.True(OnLine(c.Start, f.Supports[j].AlignmentPoint, f.Supports[j].Direction));
                Assert.True(OnLine(c.End, f.Supports[j + 1].AlignmentPoint, f.Supports[j + 1].Direction));
                // Straight chord.
                Assert.True(Vec2.Distance(c.TangentAt(0.1), c.TangentAt(0.9)) < 1e-9);
                Assert.Equal(Vec2.Distance(c.Start, c.End), c.Length, 9);
            }
            // At the pier the girder has a back and an ahead point 0.9 ft apart (offset 0.5 vs -0.4, along the skewed line).
            var back = f.SpanBearing(i, 0, true)!;
            var ahead = f.SpanBearing(i, 1, false)!;
            Assert.NotSame(back, ahead);
            Assert.Equal(Ft(0.9), back.Offset - ahead.Offset, 5);
            Assert.Same(back, f.Supports[1].Bearings[i]);
            Assert.Same(ahead, f.Supports[1].AheadBearings![i]);
            // Prestressed: top of beam = finished grade - slab - haunch at the layout points (2" haunch at span 2 begin).
            var lb = g.Spans[1].LayoutBegin!.Value;
            var so = al.Inverse(lb.XY);
            Assert.Equal(b.Deck.FinishedElevation(al, so.Station, so.Offset) - b.Deck.Thickness - In(2), lb.Z, 6);
            var le = g.Spans[0].LayoutEnd!.Value;
            so = al.Inverse(le.XY);
            Assert.Equal(b.Deck.FinishedElevation(al, so.Station, so.Offset) - b.Deck.Thickness - b.Deck.Haunch, le.Z, 6);
        }
        // The elevation check of a prestressed girder names the span layout.
        var mid = f.Girders[0].Spans[1].PointAt(0.5);
        var chain = ElevationCheck.GirderAt(f, 0, al.Inverse(mid).Station, doc.Units);
        Assert.Contains(chain.Steps, s => s.Basis.Contains("span 2 layout"));
    }

    [Fact]
    public void AlongLineOffsetsLineShiftAndPerpendicularSpacings()
    {
        var doc = Prestressed();
        var b = doc.Bridge();
        var al = doc.Alignment(b.AlignmentName);
        int n = b.Girders.Count;
        double refOff = b.Girders.UniformOffsets()[0] - Ft(3);
        double sp = Ft(7.5);
        GirderSpanEnd E(double shift, double along) => new()
        {
            LineShift = shift, Measure = SpanOffsetMeasure.AlongLine, ReferenceOffset = refOff, Offsets = { along },
        };
        b.Girders.SpanLayouts = new()
        {
            new GirderSpanLayout { Span = 0, Count = n, Begin = E(0, Ft(3)), End = E(-Ft(1), Ft(3.25)), PerpendicularSpacings = Enumerable.Repeat(sp, n - 1).ToList() },
            new GirderSpanLayout { Span = 1, Count = n, Begin = E(Ft(1), Ft(2.75)), End = E(0, Ft(2.9)), PerpendicularSpacings = Enumerable.Repeat(sp, n - 1).ToList() },
        };
        var r = doc.Regenerate(includeSteel: false);
        Assert.Empty(r.Errors);
        var f = doc.Framing(b.Name);
        var pier = f.Supports[1];
        // Span 1 end line: the pier line moved 1 ft back along the reference offset curve; beam 1 is 3.25 ft along it from there.
        var c0 = f.Girders[0].Spans[0];
        var p = c0.LayoutEnd!.Value.XY;
        double sPier = Cross(al, pier.AlignmentPoint, pier.Direction, refOff);
        var refPt = al.PointAt(sPier, refOff);
        // The reference point on the shifted line lies on the reference curve 1 ft (arc) before the pier crossing.
        var wp = p - pier.Direction * Ft(3.25);
        Assert.Equal(refOff, al.Inverse(wp).Offset, 6);
        Assert.Equal(Ft(1), Vec2.Distance(wp, refPt), 4);   // straight-ish curve: chord ~ arc over 1 ft
        Assert.True(al.Inverse(wp).Station < sPier);
        // Lines parallel to the support line.
        var q = f.Girders[n - 1].Spans[0].LayoutEnd!.Value.XY;
        Assert.True(Math.Abs(Vec2.Cross(pier.Direction, q - p)) < 1e-6);
        // Perpendicular spacing between parallel chords.
        for (int j = 0; j < 2; j++)
        {
            var a = f.Girders[0].Spans[j];
            var t = (a.LayoutEnd!.Value.XY - a.LayoutBegin!.Value.XY).Normalized();
            for (int i = 1; i < n; i++)
            {
                var ci = f.Girders[i].Spans[j];
                Assert.Equal(sp * i, Vec2.Dot(ci.LayoutBegin!.Value.XY - a.LayoutBegin!.Value.XY, t.Right), 6);
                Assert.Equal(sp * i, Vec2.Dot(ci.End - a.Start, t.Right), 6);
            }
        }
    }

    private static double Cross(Alignment al, Vec2 q, Vec2 dir, double o)
    {
        var nrm = dir.Left;
        double s = al.Inverse(q).Station;
        for (int it = 0; it < 50; it++)
        {
            double F(double x) => Vec2.Dot(al.PointAt(x, o) - q, nrm);
            double v = F(s);
            s -= v / ((F(s + 1e-4) - F(s - 1e-4)) / 2e-4);
            if (Math.Abs(v) < 1e-11) break;
        }
        return s;
    }

    [Fact]
    public void SteelGirdersMustMeetAtThePier()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        int n = b.Girders.Count;
        var off = b.Girders.UniformOffsets();
        b.Girders.SpanLayouts = new()
        {
            new GirderSpanLayout { Span = 0, Count = n, Begin = new() { Offsets = off.ToList() }, End = new() { Offsets = Shifted(b, Ft(0.5)) } },
            new GirderSpanLayout { Span = 1, Count = n, Begin = new() { Offsets = Shifted(b, Ft(0.5)) }, End = new() { Offsets = off.ToList() } },
        };
        var r = doc.Regenerate(includeSteel: false);
        Assert.Empty(r.Errors);
        var f = doc.Framing(b.Name);
        Assert.Null(f.Supports[1].AheadBearings);
        Assert.Equal(off[0] + Ft(0.5), f.Supports[1].Bearings[0]!.Offset, 6);
        // A jog is an error for continuous steel girders.
        b.Girders.SpanLayouts[1].Begin.Offsets = Shifted(b, Ft(0.2));
        r = doc.Regenerate(includeSteel: false);
        Assert.Contains(r.Errors, e => e.Contains("steel girders are continuous"));
    }

    [Fact]
    public void NoLayoutKeepsTheDefaultFraming()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        doc.Regenerate(includeSteel: false);
        var before = doc.Framing(b.Name).Bearings.Select(x => (x.Station, x.Offset, x.TopOfSteel.Z)).ToList();
        b.Girders.SpanLayouts = new();
        doc.Regenerate(includeSteel: false);
        var after = doc.Framing(b.Name).Bearings.Select(x => (x.Station, x.Offset, x.TopOfSteel.Z)).ToList();
        Assert.Equal(before, after);
        Assert.All(doc.Framing(b.Name).Supports, s => Assert.Null(s.AheadBearings));
    }

    [Fact]
    public void LayoutValidationCloneAndJson()
    {
        var doc = Prestressed();
        var b = doc.Bridge();
        int n = b.Girders.Count;
        b.Girders.SpanLayouts = new()
        {
            new GirderSpanLayout { Span = 0, Count = n + 1, Begin = new() { Offsets = { 0 } }, End = new() { Offsets = Shifted(b, 0) } },
            new GirderSpanLayout { Span = 5, Count = n, Begin = new() { Offsets = Shifted(b, 0) }, End = new() { Offsets = Shifted(b, 0) } },
        };
        var errs = b.Validate();
        Assert.Contains(errs, e => e.Contains("a different number of girders per span is not supported"));
        Assert.Contains(errs, e => e.Contains("span index out of range"));
        Assert.Contains(errs, e => e.Contains("girder offset(s) required"));

        b.Girders.SpanLayouts = new()
        {
            new GirderSpanLayout
            {
                Span = 1, Count = n, PerpendicularSpacings = Enumerable.Repeat(Ft(8), n - 1).ToList(),
                Begin = new() { Measure = SpanOffsetMeasure.AlongLine, LineShift = Ft(1), ReferenceOffset = Ft(-2), Offsets = { Ft(3) }, Haunches = Enumerable.Repeat(In(1), n).ToList() },
                End = new() { Offsets = { Ft(1) }, Spacings = Enumerable.Repeat(Ft(8), n - 1).ToList() },
            },
        };
        Assert.Empty(b.Validate());
        var c = b.Clone();
        Assert.NotSame(b.Girders.SpanLayouts[0], c.Girders.SpanLayouts![0]);
        var p = new SBM.Core.Project.BridgeProject();
        p.Bridges.Add(c);
        var back = SBM.Core.Project.BridgeProject.FromJson(p.ToJson()).Bridges[0].Girders.SpanLayoutOf(1)!;
        Assert.Equal(SpanOffsetMeasure.AlongLine, back.Begin.Measure);
        Assert.Equal(Ft(1), back.Begin.LineShift, 9);
        Assert.Equal(n, back.Begin.Haunches!.Count);
        Assert.Equal(Ft(1) + Ft(8) * (n - 1), back.End.Resolved()[^1], 9);
        Assert.Equal(n - 1, back.PerpendicularSpacings!.Count);
        // Old files: no SpanLayouts property -> null.
        var plain = new SBM.Core.Project.BridgeProject();
        plain.Bridges.Add(SbmDocument.Sample().Bridge().Clone());
        Assert.Null(SBM.Core.Project.BridgeProject.FromJson(plain.ToJson()).Bridges[0].Girders.SpanLayouts);
    }

    // ---------------------------------------------------------------- profile grade line / pivot offset

    [Fact]
    public void ConstantPivotOffsetIsTheProfileGradeLine()
    {
        var doc = SbmDocument.Sample();
        var al = doc.Project.Alignments[0];
        double sta = al.StartStation + Ft(500);
        al.CrossSlope = new CrossSlopeTable { PivotOffset = Ft(5), Rows = { new CrossSlopeRow(0, -0.02, 0.05) } };
        double pgl = al.Profile.ElevationAt(sta);
        Assert.Equal(pgl, al.ElevationAt(sta, Ft(5)), 9);
        Assert.Equal(pgl + 0.05 * Ft(22), al.ElevationAt(sta, Ft(27)), 9);
        Assert.Equal(pgl - 0.02 * Ft(10), al.ElevationAt(sta, -Ft(5)), 9);
        var u = doc.Units;
        var chain = ElevationCheck.Grade(al, sta, Ft(27), u, "test");
        Assert.Contains(chain.Steps, s => s.Basis.Contains("on the PGL") && s.Basis.Contains("RT of the alignment"));
    }

    [Fact]
    public void PivotOffsetTableInterpolatesAndHolds()
    {
        var cs = new CrossSlopeTable
        {
            PivotOffset = Ft(99),   // ignored when the table is given
            PivotOffsets = new() { new PivotOffsetRow(Ft(1000), Ft(5)), new PivotOffsetRow(Ft(1200), Ft(9)) },
            Rows = { new CrossSlopeRow(0, -0.02, -0.02) },
        };
        Assert.True(cs.VariablePivot);
        Assert.Equal(Ft(5), cs.PivotOffsetAt(Ft(900)), 9);
        Assert.Equal(Ft(7), cs.PivotOffsetAt(Ft(1100)), 9);
        Assert.Equal(Ft(9), cs.PivotOffsetAt(Ft(1300)), 9);
        Assert.Equal(-0.02 * Ft(3), cs.DeltaElevation(Ft(1100), Ft(10)), 9);
        Assert.Equal(-0.02 * Ft(7), cs.DeltaElevation(Ft(1100), 0), 9);
        Assert.Contains("interpolated", cs.PivotSource(Ft(1100), s => s.ToString(), x => x.ToString()));
        Assert.Empty(cs.Validate());
        var c = cs.Clone();
        Assert.NotSame(cs.PivotOffsets, c.PivotOffsets);
        Assert.Equal(Ft(7), c.PivotOffsetAt(Ft(1100)), 9);
        cs.PivotOffsets.Add(new PivotOffsetRow(Ft(1100), 0));
        Assert.Contains(cs.Validate(), e => e.Contains("Pivot offset row 3"));
    }

    [Fact]
    public void PivotOffsetTableSurvivesSaveAndOldFilesHaveNone()
    {
        var doc = SbmDocument.Sample();
        var al = doc.Project.Alignments[0];
        Assert.Null(SBM.Core.Project.BridgeProject.FromJson(doc.Project.ToJson()).Alignments[0].CrossSlope.PivotOffsets);
        al.CrossSlope.PivotOffsets = new() { new PivotOffsetRow(Ft(10), Ft(1)), new PivotOffsetRow(Ft(20), Ft(2)) };
        var back = SBM.Core.Project.BridgeProject.FromJson(doc.Project.ToJson()).Alignments[0].CrossSlope;
        Assert.Equal(2, back.PivotOffsets!.Count);
        Assert.Equal(Ft(1.5), back.PivotOffsetAt(Ft(15)), 9);
    }
}
