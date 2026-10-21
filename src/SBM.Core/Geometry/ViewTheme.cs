namespace SBM.Core.Geometry;

/// <summary>Viewport background theme. Light is the default.</summary>
public enum ViewBackground { Light, Dark }

/// <summary>
/// Viewport background colors and the line / text color adjustment that keeps layer colors readable on them.
/// Layer colors (`LayerStyle`) are chosen for the dark background; on the light one, colors too pale to read are
/// darkened (hue kept, near-greys go to dark grey, like CAD's colour 7 white/black swap). Solids are lit and unchanged.
/// </summary>
public static class ViewTheme
{
    public static (byte R, byte G, byte B) Background(ViewBackground theme) =>
        theme == ViewBackground.Dark ? ((byte)0x1B, (byte)0x1D, (byte)0x21) : ((byte)0xEE, (byte)0xF0, (byte)0xF3);

    /// <summary>Perceived brightness 0-1 (Rec. 601 weights).</summary>
    public static double Brightness(byte r, byte g, byte b) => (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;

    /// <summary>Brightest a line / text color may be on the light background.</summary>
    public const double MaxBrightnessOnLight = 0.45;

    /// <summary>The color to draw a line or text of layer color (r, g, b) on the given background.</summary>
    public static (byte R, byte G, byte B) LineColor(byte r, byte g, byte b, ViewBackground theme)
    {
        if (theme == ViewBackground.Dark) return (r, g, b);
        double y = Brightness(r, g, b);
        if (y <= MaxBrightnessOnLight) return (r, g, b);
        int spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        if (spread < 40) return (0x30, 0x33, 0x38); // white / grey linework -> dark grey
        double k = MaxBrightnessOnLight / y;
        return ((byte)Math.Round(r * k), (byte)Math.Round(g * k), (byte)Math.Round(b * k));
    }
}
