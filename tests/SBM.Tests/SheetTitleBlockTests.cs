using SBM.Core.Drawing;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Drawing.Views;
using SBM.Core.Units;
using Xunit;

namespace SBM.Tests;

/// <summary>Long sheet titles must fit the centre cell of the title block (PTC elevation sheet overflowed).</summary>
public class SheetTitleBlockTests
{
    private const string LongTitle = "GIRDER ELEVATIONS, SPANS 11 THROUGH 17, SOUTHBOUND AND NORTHBOUND UNITS, WITH FIELD SPLICES AND DIAPHRAGM LOCATIONS";

    [Fact]
    public void FittingTitle_IsUnchanged()
    {
        var (lines, h) = SheetComposer.FitTitle("TYPICAL SECTION", SheetComposer.TitleCellWidthIn);
        Assert.Single(lines);
        Assert.Equal(0.2, h);
    }

    [Fact]
    public void MidLengthTitle_ShrinksOnOneLine()
    {
        var (lines, h) = SheetComposer.FitTitle("GIRDER ELEVATIONS, SPANS 11 THROUGH 17 AND 18", SheetComposer.TitleCellWidthIn);
        Assert.Single(lines);
        Assert.InRange(h, 0.12, 0.2 - 1e-6);
    }

    [Fact]
    public void VeryLongTitle_Wraps_AndEveryLineFitsTheCell()
    {
        var (lines, h) = SheetComposer.FitTitle(LongTitle, SheetComposer.TitleCellWidthIn);
        Assert.InRange(lines.Count, 2, 3);
        Assert.All(lines, l => Assert.True(LabelPlacer.WidthInHeights(l) * h <= SheetComposer.TitleCellWidthIn + 1e-9, l));
    }

    [Fact]
    public void ComposedSheet_TitleTextStaysInsideTheCentreCell()
    {
        var fmt = new SheetFormat(17, 11, "P", "B", "D");
        var sheet = SheetComposer.Compose(new SheetSpec(LongTitle, "", new List<Panel>()), fmt, new UnitSystem());
        double xA = 17 - SheetFormat.Right - 3.0, xB = xA - 6.5, cx = (xA + xB) / 2;
        var texts = sheet.Drawing.Layer(SheetLayers.Title).Texts
            .Where(t => Math.Abs(t.Position.X / 0.0254 - cx) < 1e-6 && t.Height / 0.0254 > 0.1).ToList();
        Assert.True(texts.Count >= 2);
        foreach (var t in texts)
        {
            double w = LabelPlacer.WidthInHeights(t.Text) * t.Height / 0.0254;
            Assert.True(cx - w / 2 >= xB && cx + w / 2 <= xA, t.Text);
        }
    }
}
