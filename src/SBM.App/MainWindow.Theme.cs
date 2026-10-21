using System.IO;
using System.Windows;
using System.Windows.Media;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.Core.Geometry;

namespace SBM.App;

/// <summary>
/// Viewport background theme (View > Background): Light (default) or Dark, saved in
/// %LOCALAPPDATA%\SteelBridgeModeler\view-settings.json. Snapshot runs take `--dark` / `--light` and never save.
/// Line and text colors go through <see cref="ViewTheme.LineColor"/>; switching rebuilds the scene's line / text models.
/// </summary>
public partial class MainWindow
{
    private ViewBackground _theme = ViewBackground.Light;
    /// <summary>Layer color of every line model whose drawn color depends on the theme (scene + reference models).</summary>
    private readonly Dictionary<LineGeometryModel3D, Color> _rawLineColor = new();

    private sealed class ViewSettings { public string Background { get; set; } = "Light"; }

    private static string ViewSettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelBridgeModeler", "view-settings.json");

    private void InitTheme()
    {
        try
        {
            if (File.Exists(ViewSettingsPath))
            {
                var s = System.Text.Json.JsonSerializer.Deserialize<ViewSettings>(File.ReadAllText(ViewSettingsPath));
                if (s != null && Enum.TryParse<ViewBackground>(s.Background, true, out var t)) _theme = t;
            }
        }
        catch (Exception ex) { ViewModels.MainViewModel.Log("view settings not read: " + ex.Message); }
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--dark")) _theme = ViewBackground.Dark;
        else if (args.Contains("--light")) _theme = ViewBackground.Light;
        ApplyBackground();
    }

    private void ApplyBackground()
    {
        var (r, g, b) = ViewTheme.Background(_theme);
        View.BackgroundColor = Color.FromRgb(r, g, b);
        if (BackgroundLabel != null) BackgroundLabel.Text = _theme == ViewBackground.Light ? "Background: light" : "Background: dark";
    }

    /// <summary>The drawn color of a line / text of layer color c.</summary>
    private Color Themed(Color c)
    {
        var (r, g, b) = ViewTheme.LineColor(c.R, c.G, c.B, _theme);
        return Color.FromRgb(r, g, b);
    }

    private LineGeometryModel3D ThemedLine(LineGeometryModel3D m, Color layerColor)
    {
        _rawLineColor[m] = layerColor;
        m.Color = Themed(layerColor);
        return m;
    }

    /// <summary>Text geometry in the themed color (SceneBuilder writes the layer color; a copy is made only when it changes).</summary>
    private BillboardText3D ThemedText(BillboardText3D text, Color layerColor)
    {
        var c = Themed(layerColor);
        if (c == layerColor) return text;
        var fg = new HelixToolkit.Maths.Color4(c.R / 255f, c.G / 255f, c.B / 255f, 1f);
        var copy = new BillboardText3D();
        foreach (var t in text.TextInfo)
            copy.TextInfo.Add(new TextInfo(t.Text, t.Origin) { Foreground = fg, Background = t.Background, Scale = t.Scale });
        return copy;
    }

    private void OnToggleBackground(object sender, RoutedEventArgs e)
    {
        _theme = _theme == ViewBackground.Light ? ViewBackground.Dark : ViewBackground.Light;
        ApplyBackground();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ViewSettingsPath)!);
            File.WriteAllText(ViewSettingsPath, System.Text.Json.JsonSerializer.Serialize(new ViewSettings { Background = _theme.ToString() }));
        }
        catch (Exception ex) { ViewModels.MainViewModel.Log("view settings not saved: " + ex.Message); }
        foreach (var (m, c) in _rawLineColor) m.Color = Themed(c);
        if (_scene != null) OnSceneReady(_scene, false); // text colors are baked into the text geometry
    }
}
