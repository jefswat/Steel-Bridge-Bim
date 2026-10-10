using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Drawing;
using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class DrawingTests
{
    [Fact]
    public void DrawingSet_HasAllViews_AndWritesReadableDxf()
    {
        var doc = SbmDocument.Sample();
        var views = doc.Drawings();
        Assert.Contains(views, v => v.FileStem == "Layout");
        Assert.Contains(views, v => v.FileStem == "Plan");
        Assert.Contains(views, v => v.FileStem == "Typical Section");
        Assert.Contains(views, v => v.FileStem == "Pier1");
        Assert.Contains(views, v => v.FileStem == "Abut1");
        Assert.Contains(views, v => v.FileStem == "Bar Bending" && v.PaperSpace);
        string dir = Path.Combine(Path.GetTempPath(), "sbm-dwg-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = doc.ExportDrawings(dir);
            Assert.Equal(views.Count, files.Count);
            foreach (var f in files)
            {
                string text = File.ReadAllText(f);
                Assert.StartsWith("0\nSECTION", text);
                Assert.EndsWith("EOF\n", text);
                var geo = SBM.IO.References.ReferenceReaders.ReadDxf(f, 0.3048);
                Assert.True(geo.Polylines.Count > 10, Path.GetFileName(f));
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Layout_ElevationStationsMatchPlanStations()
    {
        var doc = SbmDocument.Sample();
        var layout = doc.Drawings().First(v => v.FileStem == "Layout").Drawing;
        var f = doc.Framing();
        // Every support CL line in the elevation is drawn at X = its station (developed view).
        var centers = layout.Layers.First(l => l.Name == SheetLayers.Center.Name).Polylines;
        foreach (var sl in f.Supports)
            Assert.Contains(centers, p => p.Points.Length == 2 && Math.Abs(p.Points[0].X - sl.Station) < 1e-9 && Math.Abs(p.Points[1].X - sl.Station) < 1e-9);
        var (b, e) = LayoutViews.DeckEndStations(f);
        Assert.Equal(f.Supports[0].Station - f.Definition.Supports[0].DeckEndOffset, b, 0);
    }

    [Fact]
    public void BarSheet_ListsEveryMark()
    {
        var doc = SbmDocument.Sample();
        var sheet = doc.Drawings().First(v => v.FileStem == "Bar Bending").Drawing;
        var texts = sheet.Layers.SelectMany(l => l.Texts).Select(t => t.Text).ToHashSet();
        foreach (var m in doc.Concrete().Marks.Concat(doc.Substructure().Marks))
            Assert.Contains(m.Mark, texts);
        Assert.Contains(texts, t => t.StartsWith("S (#"));
    }

    [Fact]
    public void MirrorX_KeepsTextReadable()
    {
        var d = new PlanDrawing();
        var l = d.Layer(SheetLayers.Text);
        l.Texts.Add(new TextItem(new Vec3(5, 1, 0), "A", 1, 0, TextAlign.Left));
        l.Poly(new[] { new Vec3(1, 0, 0), new Vec3(3, 2, 0) });
        d.MirrorX();
        Assert.Equal(-5, l.Texts[0].Position.X);
        Assert.Equal(TextAlign.Right, l.Texts[0].Align);
        Assert.Equal(-3, l.Polylines[0].Points[1].X);
    }

    [Fact]
    public void ElevationCheck_ContinuesThroughPedestalAndCap()
    {
        var doc = SbmDocument.Sample();
        var chains = doc.BearingElevations();
        Assert.All(chains, c => Assert.False(c.Mismatch, string.Join("; ", c.Checks)));
        Assert.All(chains, c => Assert.Contains(c.Steps, s => s.Item == "− Pedestal"));
        Assert.All(chains, c => Assert.Equal("Bottom of cap at CL bearing", c.Steps[^1].Result));
        Assert.All(chains, c => Assert.Contains(c.Checks, t => t.StartsWith("Pedestal height and cap top match")));
    }
}
