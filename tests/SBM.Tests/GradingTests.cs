using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Grading;
using SBM.Core.Project;
using SBM.Core.Road;
using Xunit;

namespace SBM.Tests;

/// <summary>Grading features (<see cref="GradingGenerator"/>, 2026-10-29; generic test inputs per the user's answer 7a.1).</summary>
public class GradingTests
{
    private static double? Flat(Vec2 _) => 100.0;

    private static GradingDefinition Pad() => new()
    {
        Name = "Pad A", Closed = true, Side = GradingSide.Outside, Source = "generic test",
        Points = { GradingPoint.Xy(0, 0, 105), GradingPoint.Xy(20, 0, 105), GradingPoint.Xy(20, 20, 105), GradingPoint.Xy(0, 20, 105) },
        Slope = new RoadSideSlope { CutRatio = 2, FillRatio = 2, MaxWidth = 30 },
        Protection =
        {
            new GradingProtectionDefinition { Name = "riprap", Kind = "Riprap Class 2", Region = GradingProtectionRegion.Slope, Thickness = 0.3 },
            new GradingProtectionDefinition { Name = "paving", Kind = "Concrete slope paving", Region = GradingProtectionRegion.Top, Thickness = 0.1 },
        },
    };

    [Fact]
    public void ClosedPad_FillVolume_Daylight_FinishedGround_Protection()
    {
        var m = GradingGenerator.Generate(Pad(), null, Flat);
        Assert.DoesNotContain(m.Diagnostics, x => x.Severity != SBM.Core.Framing.DiagnosticSeverity.Info);
        // prism + side wedges + corner cone (fan every 7.5 deg = inscribed 48-gon)
        double H = 5, R = 10, n = 48, cone = Math.PI * R * R * H / 3 * (n * Math.Sin(2 * Math.PI / n) / (2 * Math.PI));
        double fill = 400 * H + 80 * H * R / 2 + cone;
        Assert.Equal(fill, m.FillVolume!.Value, fill * 0.003);
        Assert.Equal(0, m.CutVolume!.Value, 1e-6);
        Assert.Single(m.Daylight);
        Assert.All(m.Daylight[0], p => Assert.Equal(100, p.Z, 6));
        // daylight 10 m outside the pad edges (corners: on the 10 m arc)
        Assert.All(m.Daylight[0], p =>
        {
            double dx = Math.Max(Math.Max(-p.X, p.X - 20), 0), dy = Math.Max(Math.Max(-p.Y, p.Y - 20), 0);
            Assert.Equal(10, Math.Sqrt(dx * dx + dy * dy), 6);
        });
        var fg = new FinishedGround(Flat, new (string, IEnumerable<SBM.Core.Solids.Mesh>)[] { ("grading", m.Surfaces) });
        Assert.Equal((105.0, "grading"), (fg.Query(new Vec2(10, 10)).z!.Value, fg.Query(new Vec2(10, 10)).source));
        Assert.Equal(102.5, fg.ElevationAt(new Vec2(25, 10))!.Value, 6);
        Assert.Equal((100.0, "existing ground"), (fg.Query(new Vec2(60, 10)).z!.Value, fg.Query(new Vec2(60, 10)).source));
        // protection: top paving = area x thickness exactly; riprap on the slope = area x thickness (vertex normals at the corner fans)
        var pav = m.Protection.Single(p => p.Definition.Name == "paving");
        Assert.Equal(400 * 0.1, pav.Volume, 4);
        var rip = m.Protection.Single(p => p.Definition.Name == "riprap");
        Assert.Equal(rip.Area * 0.3, rip.Volume, rip.Area * 0.3 * 0.02);
        Assert.All(m.Protection, p => Assert.True(RetainingWallTests.Closed(p.Solid), "closed solid"));
    }

    [Fact]
    public void OpenLine_OnAlignment_CutSlope_ToTheLeft()
    {
        var al = new Alignment(new[] { HorizontalElement.Line(new Vec2(1000, 1000), new Vec2(1100, 1000)) }) { Name = "Ditch line" };
        var g = new GradingDefinition
        {
            Name = "Bench", AlignmentName = "Ditch line", Side = GradingSide.Left, Source = "generic test",
            Points = { GradingPoint.At(10, 0, 95), GradingPoint.At(60, 0, 95) },
            Slope = new RoadSideSlope { CutRatio = 2, FillRatio = 3, MaxWidth = 20 },
        };
        var m = GradingGenerator.Generate(g, al, Flat);
        Assert.DoesNotContain(m.Diagnostics, x => x.Severity != SBM.Core.Framing.DiagnosticSeverity.Info);
        Assert.Equal(5 * 10 / 2.0 * 50, m.CutVolume!.Value, 1250 * 0.005);
        Assert.Equal(0, m.FillVolume!.Value, 1e-6);
        Assert.All(m.Daylight.Single(), p => Assert.Equal(-10, al.Inverse(p.XY).Offset, 6));   // left = negative offsets
        Assert.Null(m.TopSurface);
    }

    [Fact]
    public void MissingValues_Flagged_NotBuilt_RoundTrip()
    {
        var g = Pad(); g.Side = null; g.Protection[0].Thickness = double.NaN;
        var m = GradingGenerator.Generate(g, null, Flat);
        Assert.Null(m.SlopeSurface);
        Assert.NotNull(m.TopSurface);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED (generic test)") && x.Message.Contains("side the slope runs to"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED (generic test)") && x.Message.Contains("riprap") && x.Message.Contains("thickness"));
        var noGround = GradingGenerator.Generate(Pad(), null, null);
        Assert.Contains(noGround.Diagnostics, x => x.Message.Contains("no terrain is loaded"));
        Assert.Null(noGround.CutVolume);
        var g2 = Pad(); g2.Points[2].Elevation = double.NaN;
        var m2 = GradingGenerator.Generate(g2, null, Flat);
        Assert.False(m2.IsBuilt);
        Assert.Contains(m2.Diagnostics, x => x.Message.Contains("point(s) 3"));
        var p = new BridgeProject();
        Assert.DoesNotContain("\"Gradings\"", p.ToJson());
        p.Gradings.Add(Pad());
        var back = BridgeProject.FromJson(p.ToJson()).Gradings.Single();
        Assert.Equal(GradingSide.Outside, back.Side);
        Assert.Equal(4, back.Points.Count);
        Assert.Equal(GradingProtectionRegion.Top, back.Protection[1].Region);
    }

    [Fact]
    public void Ifc_GradedSurface_Protection_DatabaseKeysMatch()
    {
        var d = SbmDocument.Sample();
        var g = Pad(); g.Slope = null; g.Protection.RemoveAt(0);   // no terrain in the sample: the pad top + its paving
        d.Project.Gradings.Add(g);
        d.Regenerate();
        var gm = d.Gradings().Single();
        string path = Path.Combine(Path.GetTempPath(), "sbm-grading-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(path);
            var ifc = File.ReadAllText(path);
            Assert.Matches(@"IFCGEOGRAPHICELEMENT\('[^']*',\$,'Pad A graded surface'[^;]*\.TERRAIN\.", ifc);
            Assert.Matches(@"IFCCOURSE\('[^']*',\$,'Pad A paving'[^;]*\.PROTECTION\.", ifc);
            Assert.Contains("'GRADING'", ifc);
            var tags = SBM.IO.Database.ModelDatabase.BuildTagIndex(d.Project, Array.Empty<SBM.IO.Ifc.IfcBridgeData>(), null, null, new[] { gm });
            Assert.Contains("'" + tags.Lookup(gm.Surfaces.First()) + "'", ifc);
            Assert.Contains("'" + tags.Lookup(gm.Protection[0].Solid) + "'", ifc);
        }
        finally { File.Delete(path); }
    }
}
