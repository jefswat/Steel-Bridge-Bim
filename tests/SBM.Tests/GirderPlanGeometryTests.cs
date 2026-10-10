using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>As-designed plan geometry of individual girders (tangent / arc chains, e.g. "P.C. STA ..., R = ...").</summary>
public class GirderPlanGeometryTests
{
    private static GirderPlanGeometry Chain(SbmDocument doc, BridgeDefinition b, int girder)
    {
        // Start 40 ft before the first support at the girder's offset, heading 1.5 deg toward the right of the alignment,
        // a tangent then a left arc (R = 900 ft) then a right arc (R = 1500 ft).
        return new GirderPlanGeometry
        {
            Girder = girder,
            StartStation = b.Supports[0].Station - Ft(40),
            StartOffset = b.Girders.OffsetsAtSupports?[0][girder] ?? b.Girders.UniformOffsets()[girder],
            StartDirectionDegrees = 1.5,
            Elements =
            {
                new GirderPlanElement { Kind = GirderPlanElementKind.Line, Length = Ft(150) },
                new GirderPlanElement { Kind = GirderPlanElementKind.ArcLeft, Radius = Ft(900), Length = Ft(400) },
                new GirderPlanElement { Kind = GirderPlanElementKind.ArcRight, Radius = Ft(1500), Length = Ft(1000) },
            },
        };
    }

    [Fact]
    public void OffsetIsTheNormalProjectionOfThePlanCurve()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        var al = doc.Alignment(b.AlignmentName);
        var g = Chain(doc, b, 1);
        var curve = new GirderPlanCurve(al, g);
        double d0 = al.StationToDistance(b.Supports[0].Station);
        for (double d = d0; d < d0 + Ft(500); d += Ft(37))
        {
            double off = curve.OffsetAt(d);
            var p = al.PointAtDistance(d, off);
            // The point must lie on the plan curve (distance to the densely sampled curve) ...
            double best = double.MaxValue;
            for (double t = -Ft(100); t < Ft(1400); t += 0.002)
                best = Math.Min(best, Vec2.Distance(curve.At(t).point, p));
            Assert.True(best < 0.003, $"d={d}: {best}");
            // ... and its alignment foot is d.
            Assert.Equal(d, al.StationToDistance(al.Inverse(p).Station), 5);
        }
    }

    [Fact]
    public void ArcLengthAndTangentContinuity()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        var curve = new GirderPlanCurve(doc.Alignment(b.AlignmentName), Chain(doc, b, 0));
        // Tangent is continuous across element boundaries and the arc turns by length / R.
        var (_, t1) = curve.At(Ft(150) - 1e-6); var (_, t2) = curve.At(Ft(150) + 1e-6);
        Assert.True(Vec2.Distance(t1, t2) < 1e-6);
        var (_, ta) = curve.At(Ft(150)); var (_, tb) = curve.At(Ft(550));
        Assert.Equal(Ft(400) / Ft(900), Math.Atan2(Vec2.Cross(ta, tb), Vec2.Dot(ta, tb)), 8);
        Assert.Equal(Ft(1550), curve.TotalLength, 9);
    }

    [Fact]
    public void PlanGirderBearsWhereItCrossesTheSupportLinesAndOthersAreUnchanged()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        Assert.Empty(doc.Regenerate().Errors);
        var before = doc.Model(b.Name).Framing;
        var g = Chain(doc, b, 1);
        b.Girders.PlanGeometry.Add(g);
        var r = doc.Regenerate();
        Assert.Empty(r.Errors);
        var f = doc.Model(b.Name).Framing;
        var al = doc.Alignment(b.AlignmentName);
        var curve = new GirderPlanCurve(al, g);
        for (int j = 0; j < b.Supports.Count; j++)
        {
            var br = f.Supports[j].Bearings[1]!;
            var p = al.PointAt(br.Station, br.Offset);
            // On the support line ...
            var line = f.Supports[j];
            Assert.True(Math.Abs(Vec2.Cross(line.Direction, p - line.AlignmentPoint)) < 1e-6);
            // ... and on the plan curve.
            double best = double.MaxValue;
            for (double t = -Ft(100); t < Ft(1500); t += 0.01) best = Math.Min(best, Vec2.Distance(curve.At(t).point, p));
            Assert.True(best < 0.01, $"support {j}: {best}");
            // Other girders keep their bearings.
            foreach (int o in new[] { 0, 2, 3 })
                Assert.Equal(before.Supports[j].Bearings[o]!.Offset, f.Supports[j].Bearings[o]!.Offset, 9);
        }
        // The girder path follows the curve: path points are on it and spans are longer than the support chords.
        var gr = f.Girders[1];
        foreach (var s in gr.Path.S.Where((_, i) => i % 7 == 0))
        {
            var (pt, _, _) = gr.Path.At(s);
            double best = double.MaxValue;
            for (double t = -Ft(100); t < Ft(1500); t += 0.02) best = Math.Min(best, Vec2.Distance(curve.At(t).point, pt.XY));
            Assert.True(best < 0.02);
        }
        for (int j = 0; j + 1 < b.Supports.Count; j++)
            Assert.True(gr.Spans[j].Length >= Vec2.Distance(gr.Spans[j].Start, gr.Spans[j].End) - 1e-9);
        // Cross frames still frame the bays on both sides of the plan girder.
        Assert.Contains(f.CrossFrames, c => c.Bay == 0 && !c.AtSupport);
        Assert.Contains(f.CrossFrames, c => c.Bay == 1 && !c.AtSupport);
    }

    [Fact]
    public void PlanGeometrySurvivesCloneAndSave()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.PlanGeometry.Add(Chain(doc, b, 2));
        var c = b.Clone();
        Assert.Equal(3, c.Girders.PlanGeometry[0].Elements.Count);
        Assert.NotSame(b.Girders.PlanGeometry[0], c.Girders.PlanGeometry[0]);
        var p = new SBM.Core.Project.BridgeProject();
        p.Bridges.Add(c);
        var back = SBM.Core.Project.BridgeProject.FromJson(p.ToJson()).Bridges[0].Girders.PlanGeometry[0];
        Assert.Equal(2, back.Girder);
        Assert.Equal(GirderPlanElementKind.ArcRight, back.Elements[2].Kind);
        Assert.Equal(Ft(900), back.Elements[1].Radius, 9);
        Assert.Equal(1.5, back.StartDirectionDegrees, 9);
    }

    [Fact]
    public void InvalidPlanGeometryIsReported()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.PlanGeometry.Add(new GirderPlanGeometry { Girder = 99, Elements = { new GirderPlanElement { Kind = GirderPlanElementKind.ArcLeft, Radius = 0, Length = 5 } } });
        var errs = b.Validate();
        Assert.Contains(errs, e => e.Contains("out of range"));
        Assert.Contains(errs, e => e.Contains("radius"));
    }
}
