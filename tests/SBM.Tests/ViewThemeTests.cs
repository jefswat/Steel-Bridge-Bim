using SBM.Core.Geometry;

namespace SBM.Tests;

public class ViewThemeTests
{
    [Fact]
    public void Dark_LeavesLayerColorsUnchanged()
    {
        Assert.Equal(((byte)255, (byte)255, (byte)0), ViewTheme.LineColor(255, 255, 0, ViewBackground.Dark));
        Assert.Equal(((byte)0x1B, (byte)0x1D, (byte)0x21), ViewTheme.Background(ViewBackground.Dark));
    }

    [Fact]
    public void Light_DarkensPaleColors_KeepsDarkOnes()
    {
        // White / grey -> dark grey.
        Assert.Equal(((byte)0x30, (byte)0x33, (byte)0x38), ViewTheme.LineColor(230, 230, 230, ViewBackground.Light));
        // Yellow keeps its hue, brightness capped.
        var (r, g, b) = ViewTheme.LineColor(255, 255, 0, ViewBackground.Light);
        Assert.Equal(r, g);
        Assert.Equal(0, b);
        Assert.True(ViewTheme.Brightness(r, g, b) <= ViewTheme.MaxBrightnessOnLight + 0.005);
        // Already dark enough: unchanged.
        Assert.Equal(((byte)120, (byte)30, (byte)30), ViewTheme.LineColor(120, 30, 30, ViewBackground.Light));
        // Every result is readable on the light background.
        var bg = ViewTheme.Background(ViewBackground.Light);
        for (int v = 0; v < 256; v += 15)
        {
            var c = ViewTheme.LineColor((byte)v, (byte)(255 - v), 200, ViewBackground.Light);
            Assert.True(ViewTheme.Brightness(bg.R, bg.G, bg.B) - ViewTheme.Brightness(c.R, c.G, c.B) > 0.45);
        }
    }
}
