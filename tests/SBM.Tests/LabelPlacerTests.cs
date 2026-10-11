using SBM.Core.Drawing;
using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using Xunit;

namespace SBM.Tests;

public class LabelPlacerTests
{
    [Fact]
    public void OverprintedLabels_AreSeparated()
    {
        var dwg = new PlanDrawing();
        var layer = dwg.Layer(SheetLayers.Text);
        for (int i = 0; i < 5; i++) layer.Texts.Add(new TextItem(new Vec3(i * 0.2, 0, 0), "FIELD SPLICE STA 7592+66.39", 1.0, 0, TextAlign.Left));
        int moved = LabelPlacer.Resolve(dwg);
        Assert.True(moved >= 4);
        // No two texts overlap afterwards (axis-aligned check; all are horizontal).
        var t = layer.Texts;
        for (int i = 0; i < t.Count; i++)
            for (int j = i + 1; j < t.Count; j++)
            {
                double w = LabelPlacer.WidthInHeights(t[i].Text);
                bool xo = Math.Abs(t[i].Position.X - t[j].Position.X) < w;
                bool yo = Math.Abs(t[i].Position.Y - t[j].Position.Y) < 1.0;
                Assert.False(xo && yo, $"texts {i} and {j} overlap");
            }
    }

    [Fact]
    public void SeparateLabels_AreNotMoved()
    {
        var dwg = new PlanDrawing();
        var layer = dwg.Layer(SheetLayers.Text);
        layer.Texts.Add(new TextItem(new Vec3(0, 0, 0), "A", 1.0, 0, TextAlign.Left));
        layer.Texts.Add(new TextItem(new Vec3(0, 5, 0), "B", 1.0, 0, TextAlign.Left));
        Assert.Equal(0, LabelPlacer.Resolve(dwg));
    }

    [Fact]
    public void BoltGroups_SplitConnections()
    {
        var bolts = new List<Vec2>();
        for (int i = 0; i < 4; i++) bolts.Add(new Vec2(0, i * 0.0762));        // 3" pitch
        for (int i = 0; i < 6; i++) bolts.Add(new Vec2(0, 2.0 + i * 0.0889));  // 3.5" pitch, 2 m away
        var g = SteelDetailViews.BoltGroups(bolts);
        Assert.Equal(2, g.Count);
        Assert.Equal(new[] { 4, 6 }, g.Select(x => x.Count).OrderBy(x => x).ToArray());
    }
}
