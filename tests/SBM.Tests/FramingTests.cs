using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class FramingTests
{
    private static (BridgeFraming f, Alignment a) Sample(Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        tweak?.Invoke(b);
        var a = p.Alignments[0];
        return (FramingGenerator.Generate(b, a), a);
    }

    [Fact]
    public void Sample_GeneratesWithoutErrors()
    {
        var (f, _) = Sample();
        Assert.False(f.HasErrors, string.Join("\n", f.Diagnostics));
        Assert.Equal(5, f.Girders.Count);
        Assert.Equal(3, f.Supports.Count);
        Assert.NotNull(f.Deck);
        Assert.Equal(10, f.Splices.Count);
        Assert.All(f.Supports, s => Assert.All(s.Bearings, b => Assert.NotNull(b)));
        Assert.Contains(f.Stiffeners, s => s.Kind == StiffenerKind.Intermediate);
    }

    [Fact]
    public void Bearings_LieOnSupportLineAndGirderOffset()
    {
        var (f, a) = Sample();
        foreach (var s in f.Supports)
            foreach (var b in s.Bearings.OfType<BearingResult>())
            {
                double miss = Vec2.Cross(s.Direction, b.TopOfSteel.XY - s.AlignmentPoint);
                Assert.True(Math.Abs(miss) < 1e-6, $"bearing off support line by {miss}");
                var so = a.Inverse(b.TopOfSteel.XY);
                double expected = f.Definition.Girders.UniformOffsets()[b.Girder];
                Assert.Equal(expected, so.Offset, 6);
            }
    }

    [Fact]
    public void TopOfWeb_IsDeckMinusSlabAndHaunch_FlangeThicknessOnTop()
    {
        var (f, a) = Sample();
        var d = f.Definition.Deck;
        double tf = f.Definition.Girders.Section.TopFlange.Thickness;
        foreach (var b in f.Bearings)
        {
            var so = a.Inverse(b.TopOfSteel.XY);
            double topOfWeb = a.ElevationAt(so.Station, so.Offset) - d.Thickness - d.Haunch;
            Assert.Equal(topOfWeb + tf, b.TopOfSteel.Z, 6);
        }
    }

    [Fact]
    public void CrossFrames_RespectMaximumSpacingAlongEveryGirder()
    {
        var (f, _) = Sample();
        double max = f.Definition.CrossFrames.MaxSpacing;
        for (int span = 0; span < 2; span++)
            for (int g = 0; g < 5; g++)
            {
                var s = new List<double>();
                foreach (var cf in f.CrossFrames.Where(c => c.Span == span))
                {
                    if (cf.Bay == g) s.Add(cf.LeftGirderDistance);
                    if (cf.Bay + 1 == g) s.Add(cf.RightGirderDistance);
                }
                s.Add(0);
                s.Add(f.Girders[g].Spans[span].Length);
                s.Sort();
                for (int i = 1; i < s.Count; i++) Assert.True(s[i] - s[i - 1] <= max + 1e-6, $"gap {s[i] - s[i - 1]}");
            }
        Assert.DoesNotContain(f.Diagnostics, d => d.Severity != DiagnosticSeverity.Info && d.Message.Contains("exceeding"));
    }

    [Fact]
    public void HighSkew_UsesStaggeredFramesNormalToGirders_WithBearingClearance()
    {
        var (f, a) = Sample(b =>
        {
            foreach (var s in b.Supports) s.SkewDegrees = 45;
        });
        Assert.False(f.HasErrors, string.Join("\n", f.Diagnostics));
        double clear = f.Definition.CrossFrames.MinDistanceFromBearing;
        foreach (var cf in f.CrossFrames.Where(c => !c.AtSupport))
        {
            // Normal (radial) frames: both ends at the same station.
            var l = a.Inverse(cf.Left.XY);
            var r = a.Inverse(cf.Right.XY);
            Assert.Equal(l.Station, r.Station, 4);
            Assert.True(cf.LeftGirderDistance >= clear - 1e-3);
            Assert.True(cf.RightGirderDistance >= clear - 1e-3);
            Assert.True(f.Girders[cf.Bay].Spans[cf.Span].Length - cf.LeftGirderDistance >= clear - 1e-3);
        }
    }

    [Fact]
    public void ChordedAndFlaredLayouts_Generate()
    {
        var (f, _) = Sample(b =>
        {
            b.Girders.PlanLayout = GirderPlanLayout.Chorded;
            var u = b.Girders.UniformOffsets().ToList();
            b.Girders.OffsetsAtSupports = new List<List<double>>
            {
                u,
                u.Select(o => o * 1.1).ToList(),
                u.Select(o => o * 1.2).ToList(),
            };
            b.Deck.LeftEdgeOffset = Ft(-26);
            b.Deck.RightEdgeOffset = Ft(26);
        });
        Assert.False(f.HasErrors, string.Join("\n", f.Diagnostics));
        Assert.True(f.CrossFrames.Count > 20);
    }

    [Fact]
    public void InvalidDefinition_ReportsErrors()
    {
        var (f, _) = Sample(b => b.Supports[1].Station = b.Supports[0].Station - 10);
        Assert.True(f.HasErrors);
    }
}
