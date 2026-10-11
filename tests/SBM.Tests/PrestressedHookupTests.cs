using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Woodbury decisions 2026-10-11: span-end offsets from the W.P. / bearing lines along the beam (item 8), standard beams that
/// fit within 1/16" (no placement rule), pier pads parallel to the pier (17), haunch following the profile, and the T6 hook-up of the
/// steel intermediate diaphragms (12 / 19).</summary>
public class PrestressedHookupTests
{
    private static SbmDocument Prestressed(Action<PrestressedBeamDefinition>? edit = null)
    {
        var doc = SbmDocument.Sample();      // 2 spans, skew 15 deg
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        edit?.Invoke(b.Girders.Prestressed);
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    [Fact]
    public void SpanEnd_ShiftAlongGirder_AndOffsetsFromTheWorkPoint()
    {
        var doc = Prestressed();
        var b = doc.Bridge();
        var al = doc.Alignment(b.AlignmentName);
        int n = b.Girders.Count;
        double refOff = b.Girders.UniformOffsets()[0] - Ft(3);
        GirderSpanEnd E(double shift, double along) => new()
        {
            LineShift = shift, ShiftMeasure = SpanLineShiftMeasure.AlongGirder, OffsetOrigin = SpanOffsetOrigin.WorkPoint,
            Measure = SpanOffsetMeasure.AlongLine, ReferenceOffset = refOff, Offsets = { along },
        };
        var sp = Enumerable.Repeat(Ft(7.5), n - 1).ToList();
        b.Girders.SpanLayouts = new()
        {
            new GirderSpanLayout { Span = 0, Count = n, Begin = E(0, Ft(3)), End = E(-Ft(1), Ft(3.25)), PerpendicularSpacings = sp },
            new GirderSpanLayout { Span = 1, Count = n, Begin = E(Ft(1), Ft(2.75)), End = E(0, Ft(2.9)), PerpendicularSpacings = sp },
        };
        Assert.Empty(doc.Regenerate(includeSteel: false).Errors);
        var f = doc.Framing(b.Name);
        var pier = f.Supports[1];
        var nrm = pier.Direction.Left;
        // W.P. = pier line x reference curve.
        var wp = WorkPoint(al, pier.AlignmentPoint, pier.Direction, refOff);
        foreach (var (span, atEnd, along) in new[] { (0, true, Ft(3.25)), (1, false, Ft(2.75)) })
        {
            var c = f.Girders[0].Spans[span];
            var p = (atEnd ? c.LayoutEnd : c.LayoutBegin)!.Value.XY;
            // Offset measured along the bearing line from the W.P.'s foot (aligned dimension from the W.P.).
            Assert.Equal(along, Vec2.Dot(p - wp, pier.Direction), 6);
            // Bearing line 1'-0 from the pier line ALONG the girder.
            var t = (c.LayoutEnd!.Value.XY - c.LayoutBegin!.Value.XY).Normalized();
            double perp = Math.Abs(Vec2.Dot(p - pier.AlignmentPoint, nrm));
            Assert.Equal(Ft(1), perp / Math.Abs(Vec2.Dot(t, nrm)), 6);
            // Every girder's layout point of that end is on one line parallel to the pier.
            var q = (atEnd ? f.Girders[n - 1].Spans[span].LayoutEnd : f.Girders[n - 1].Spans[span].LayoutBegin)!.Value.XY;
            Assert.True(Math.Abs(Vec2.Cross(pier.Direction, q - p)) < 1e-6);
        }
        // Defaults unchanged: the shifted line crossing is the zero point (WorkPoint differs only when the line is shifted).
        var e0 = new GirderSpanEnd();
        Assert.Equal(SpanLineShiftMeasure.AlongReference, e0.ShiftMeasure);
        Assert.Equal(SpanOffsetOrigin.LineCrossing, e0.OffsetOrigin);
        var clone = E(Ft(1), 0).Clone();
        Assert.Equal(SpanLineShiftMeasure.AlongGirder, clone.ShiftMeasure);
        Assert.Equal(SpanOffsetOrigin.WorkPoint, clone.OffsetOrigin);
    }

    private static Vec2 WorkPoint(SBM.Core.Alignments.Alignment al, Vec2 q, Vec2 dir, double o)
    {
        var nrm = dir.Left;
        double s = al.Inverse(q).Station;
        for (int it = 0; it < 50; it++)
        {
            double F(double x) => Vec2.Dot(al.PointAt(x, o) - q, nrm);
            double v = F(s);
            s -= v / ((F(s + 1e-4) - F(s - 1e-4)) / 2e-4);
            if (Math.Abs(v) < 1e-12) break;
        }
        return al.PointAt(s, o);
    }

    [Fact]
    public void StandardBeam_WithinASixteenth_FitsWithoutPlacement()
    {
        var doc = Prestressed(p => Copy(p, StandardBeamCatalog.Beam("BTD55")!));
        Assert.Empty(doc.Regenerate().Errors);
        var b0 = doc.Model().Prestressed!.Beams[0];
        var p = doc.Bridge().Girders.Prestressed;
        p.StandardBearingLength = b0.LayoutBearingSpan + In(1.0 / 32);      // 1/32" longer than the layout: fits
        p.StandardOverallLength = p.StandardBearingLength + In(16);
        p.Placement = null;
        doc.Regenerate();
        var m = doc.Model().Prestressed!;
        var b = m.Beams.First(x => x.Girder == b0.Girder && x.Span == b0.Span);
        Assert.Null(b.Assumption);
        Assert.DoesNotContain(m.Diagnostics, d => d.Message.StartsWith("CHECK (standard beam length)") && d.Message.Contains($"G{b0.Girder + 1} span {b0.Span + 1} "));
        Assert.Contains(m.Diagnostics, d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Info && d.Message.StartsWith("Standard beam fit, span 1"));
    }

    [Fact]
    public void PierPads_ParallelToTheSupport()
    {
        var doc = Prestressed(p =>
        {
            p.PadLength = In(9);
            p.PadWidth = In(28.5);
            p.PadThickness = In(1);
            p.PierPadOrientation = PadOrientation.ParallelToSupport;
        });
        Assert.Empty(doc.Regenerate().Errors);
        var f = doc.Framing();
        var ps = doc.Model().Prestressed!;
        var pier = f.Supports[1];
        foreach (var pad in ps.Pads)
        {
            var pts = pad.Mesh.Positions;
            double Extent(Vec2 d) => pts.Max(q => Vec2.Dot(q.XY, d)) - pts.Min(q => Vec2.Dot(q.XY, d));
            if (pad.Support == pier.Name)
            {
                Assert.Equal(In(28.5), Extent(pier.Direction), 6);       // width along CL pier
                Assert.Equal(In(9), Extent(pier.Direction.Left), 6);
                Assert.Contains("parallel to the support", pad.Description);
            }
            else Assert.DoesNotContain("parallel", pad.Description);   // abutments stay square to the beam
        }
        // Seats carry the pad axis (support normal at the pier).
        var seat = ps.BearingSeats.First(s => s.Support == pier.Name);
        Assert.True(Math.Abs(Vec2.Cross(seat.Axis!.Value, pier.Direction.Left)) < 1e-9);
    }

    [Fact]
    public void HaunchSamples_SubdivideLongChordsButKeepJogs()
    {
        var s = ConcreteGenerator.HaunchSamples(new[] { 0.0, 10.0, 10.00001, 13.0 });
        Assert.Equal(0.0, s[0]);
        Assert.Contains(10.0, s);
        Assert.Contains(10.00001, s);
        Assert.Equal(13.0, s[^1]);
        Assert.All(s.Zip(s.Skip(1)), p => Assert.True(p.Second - p.First <= 1.5 + 1e-9));
        Assert.Equal(7 + 1 + 2 + 1, s.Count);   // 0..10 in 7 parts, the jog, 10..13 in 2 parts
    }

    [Fact]
    public void Haunch_FollowsTheDeckSoffitAlongStraightBeams()
    {
        // The sample alignment has a vertical curve: the haunch top must follow the deck soffit between the bearings.
        var doc = Prestressed();
        Assert.Empty(doc.Regenerate().Errors);
        var cm = doc.Concrete();
        var f = doc.Framing();
        var b = doc.Bridge();
        var al = doc.Alignment(b.AlignmentName);
        var g = f.Girders[1];
        var c = g.Spans[0];
        var mid = c.PointAt(0.5);
        var so = al.Inverse(mid);
        double soffit = b.Deck.FinishedElevation(al, so.Station, so.Offset) - b.Deck.Thickness;
        // Haunch vertices near midspan exist (before: rings only at the span ends) and the top ones lie on the soffit.
        double Soffit(Vec2 q) { var x = al.Inverse(q); return b.Deck.FinishedElevation(al, x.Station, x.Offset) - b.Deck.Thickness; }
        var near = cm.Haunches!.Positions.Where(q => Vec2.Distance(q.XY, mid) < 1.0).ToList();
        Assert.NotEmpty(near);
        Assert.Contains(near, q => Math.Abs(q.Z - Soffit(q.XY)) < 1e-6);
        Assert.All(near, q => Assert.True(q.Z <= Soffit(q.XY) + 1e-6));
        Assert.True(soffit > 0);
    }

    [Fact]
    public void SteelDiaphragms_AreHookedIntoTheModelAndIfc()
    {
        var doc = Prestressed(p => Copy(p, StandardBeamCatalog.Beam("BTD55")!));
        var b = doc.Bridge();
        Assert.Empty(doc.Regenerate().Errors);
        var beams = doc.Model().Prestressed!.Beams;
        var dd = new PsSteelDiaphragmDefinition { MemberSide = PsDiaphragmMemberSide.TowardIncreasingStation };
        foreach (var be in beams)
        {
            double half = be.Length * Math.Sqrt(1 + be.Slope * be.Slope) / 2;
            dd.Holes.Add(new PsDiaphragmHoleChain { Span = be.Span + 1, Girder = be.Girder + 1, Dims = new() { half, half } });
        }
        for (int g = 1; g < b.Girders.Count; g++) dd.Bays.Add(new PsDiaphragmBay { Span = 1, LeftGirder = g });
        b.PsSteelDiaphragms = dd;
        var r = doc.Regenerate();
        Assert.Empty(r.Errors);
        var m = doc.Model();
        Assert.NotNull(m.Prestressed!.SteelDiaphragms);
        var dm = m.Prestressed.SteelDiaphragms!;
        Assert.Equal(b.Girders.Count - 1, dm.Assemblies.Count);
        Assert.NotNull(m.Steel);
        Assert.Equal(dm.Parts.Count, m.Steel!.Parts.Count);
        Assert.Equal(dm.Bolts.Count, m.Steel.Bolts.Count);
        Assert.Equal(5 * (b.Girders.Count - 1), m.Steel.Parts.Count);          // member + 2 bent + 2 backing plates per bay
        Assert.Equal(12 * (b.Girders.Count - 1), m.Steel.Bolts.Count);         // per bay: 2 ends x (2 web + 4 member bolts)
        Assert.Contains(r.Warnings, w => w.Contains("Steel diaphragm"));       // detailer diagnostics reach the report
        // Unresolved member side = holes only, no steel (and no steel model).
        dd.MemberSide = PsDiaphragmMemberSide.Unresolved;
        doc.Regenerate();
        Assert.Empty(doc.Model().Steel!.Parts);
        Assert.Contains(doc.Model().Prestressed!.Diagnostics, d => d.Message.Contains("UNRESOLVED WD-3"));
        // IFC: members, plates and fasteners like cross frames.
        dd.MemberSide = PsDiaphragmMemberSide.TowardIncreasingStation;
        doc.Regenerate();
        string path = Path.Combine(Path.GetTempPath(), $"sbm-psdia-{Guid.NewGuid():N}.ifc");
        try
        {
            doc.ExportIfc(path);
            var text = File.ReadAllText(path);
            Assert.Equal(b.Girders.Count - 1, CountOf(text, "IFCMEMBER("));
            Assert.Equal(4 * (b.Girders.Count - 1), CountOf(text, "IFCPLATE("));
            Assert.Equal(m.Steel.Bolts.Count, CountOf(text, "IFCMECHANICALFASTENER("));
            Assert.Contains("ASTM A449", text);
        }
        finally { File.Delete(path); }
    }

    private static int CountOf(string text, string what)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; }
        return n;
    }

    private static void Copy(PrestressedBeamDefinition target, PrestressedBeamDefinition src)
    {
        foreach (var p in typeof(PrestressedBeamDefinition).GetProperties().Where(p => p.CanWrite))
            p.SetValue(target, p.GetValue(src.Clone()));
    }
}
