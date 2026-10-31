using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SBM.App.Viewport;
using SBM.Core.Drawing;
using SBM.Core.Drawing.Projection;
using SBM.Core.Units;

namespace SBM.App.Editors;

/// <summary>
/// Click views (View &gt; Click Views): a modeless window with a PLAN, an ELEVATION and a SECTION of the model at the point clicked in the 3D /
/// plan viewport, projected from the solids with hidden-line removal (<see cref="ModelProjector"/>). The host (MainWindow.ClickViews.cs) computes
/// the views; this window shows them, holds the options (window size, depth beyond the section, hidden lines) and identifies clicked lines.
/// </summary>
public sealed class ClickViewsWindow : Window
{
    private readonly UnitSystem _units;
    private readonly (DrawingCanvas canvas, TextBlock head)[] _panels = new (DrawingCanvas, TextBlock)[3];
    private readonly ProjectionResult?[] _results = new ProjectionResult?[3];
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly ComboBox _size = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly ComboBox _beyond = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly ComboBox _plan = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly CheckBox _hidden = new() { Content = "Hidden lines (dashed)", IsChecked = false, Margin = new Thickness(0, 4, 0, 4) };

    /// <summary>Half-widths of the view windows (m) and depths shown beyond the section cut (m).</summary>
    private static readonly double[] Sizes = { 7.62, 15.24, 30.48, 76.2 }, Beyonds = { 0, 0.9144, 3.048, 15.24 };

    /// <summary>The options changed: the host re-projects the last point.</summary>
    public event Action? OptionsChanged;

    public double HalfWidth => Sizes[Math.Max(0, _size.SelectedIndex)];
    public double Beyond => Beyonds[Math.Max(0, _beyond.SelectedIndex)];
    public bool HiddenLines => _hidden.IsChecked == true;
    /// <summary>Plan = horizontal cut at the clicked elevation (true) or a top view of everything (false).</summary>
    public bool PlanCut => _plan.SelectedIndex != 1;

    public ClickViewsWindow(UnitSystem units)
    {
        _units = units;
        Title = "Click views - plan, elevation, section at the clicked point";
        Width = 1500; Height = 950;
        // own dark background like the other tool windows: the default window brush is the system white, under the theme's light text
        Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        var grid = new Grid { Margin = new Thickness(6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        string[] names = { "PLAN", "ELEVATION (looking from the right of the alignment)", "SECTION (looking up-station)" };
        (int r, int c)[] cells = { (0, 0), (0, 1), (1, 0) };
        for (int i = 0; i < 3; i++)
        {
            var head = new TextBlock { Text = names[i], FontWeight = FontWeights.Bold, Margin = new Thickness(4, 2, 4, 2) };
            var canvas = new DrawingCanvas();
            var border = new Border { BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), BorderThickness = new Thickness(1), Margin = new Thickness(3), Child = canvas };
            var dock = new DockPanel();
            DockPanel.SetDock(head, Dock.Top);
            dock.Children.Add(head);
            dock.Children.Add(border);
            Grid.SetRow(dock, cells[i].r); Grid.SetColumn(dock, cells[i].c);
            grid.Children.Add(dock);
            _panels[i] = (canvas, head);
            int k = i;
            canvas.Clicked += p =>
            {
                if (_results[k]?.Pick(p, canvas.PixelSize * 6) is { } e) _picked.Text = $"{names[k].Split(' ')[0]}: {e.Name} ({e.Kind}), {e.Bridge}, layer {e.Layer}";
            };
        }
        foreach (var s in Sizes) _size.Items.Add($"{_units.FormatLength(2 * s)} wide");
        _size.SelectedIndex = 1;
        foreach (var b in Beyonds) _beyond.Items.Add(b == 0 ? "cut only" : _units.FormatLength(b) + " beyond the cut");
        _beyond.SelectedIndex = 1;
        _plan.Items.Add("cut at the clicked elevation");
        _plan.Items.Add("top view (no cut)");
        _plan.SelectedIndex = 0;
        _plan.SelectionChanged += (_, _) => OptionsChanged?.Invoke();
        _size.SelectionChanged += (_, _) => OptionsChanged?.Invoke();
        _beyond.SelectionChanged += (_, _) => OptionsChanged?.Invoke();
        _hidden.Click += (_, _) => OptionsChanged?.Invoke();
        var side = new StackPanel { Margin = new Thickness(10, 4, 10, 4) };
        side.Children.Add(new TextBlock { Text = "Click any element in the model view: the three views are cut / centred at the clicked point.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        side.Children.Add(new TextBlock { Text = "View window" });
        side.Children.Add(_size);
        side.Children.Add(new TextBlock { Text = "Plan" });
        side.Children.Add(_plan);
        side.Children.Add(new TextBlock { Text = "Section shows" });
        side.Children.Add(_beyond);
        side.Children.Add(_hidden);
        var zoom = new Button { Content = "Zoom extents (all three)", Padding = new Thickness(10, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) };
        zoom.Click += (_, _) => { foreach (var (c, _) in _panels) c.ZoomExtents(); };
        side.Children.Add(zoom);
        side.Children.Add(_info);
        side.Children.Add(_picked);
        var sv = new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(sv, 1); Grid.SetColumn(sv, 1);
        grid.Children.Add(sv);
        Content = grid;
        _info.Text = "No point yet.";
    }

    private readonly TextBlock _picked = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    /// <summary>Shows "projecting..." while the host works.</summary>
    public void ShowBusy(string text) => _info.Text = text;

    /// <summary>Shows the three projected views (plan, elevation, section) and the point description.</summary>
    public void ShowViews(ProjectionResult?[] results, string[] notes, string info)
    {
        for (int i = 0; i < 3; i++)
        {
            _results[i] = results[i];
            _panels[i].canvas.Drawing = results[i]?.Drawing ?? new PlanDrawing();
            string baseName = _panels[i].head.Text.Split(" - ")[0];
            _panels[i].head.Text = notes[i].Length > 0 ? $"{baseName} - {notes[i]}" : baseName;
        }
        _info.Text = info;
        _picked.Text = "Click a line in a view to identify its element.";
    }

    /// <summary>Writes the three views as PNGs (snapshot runs).</summary>
    public void RenderToPng(string stem)
    {
        string[] tags = { "plan", "elevation", "section" };
        for (int i = 0; i < 3; i++)
            if (_results[i] is { } r) DrawingCanvas.RenderToPng(r.Drawing, $"{stem}_click_{tags[i]}.png", 1200, 800);
        if (Content is FrameworkElement fe && fe.ActualWidth > 0)
        {
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)fe.ActualWidth, (int)fe.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen()) dc.DrawRectangle(Background ?? new SolidColorBrush(Color.FromRgb(32, 32, 32)), null, new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
            bmp.Render(bg);
            bmp.Render(fe);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var f = System.IO.File.Create($"{stem}_click_window.png");
            enc.Save(f);
        }
    }
}
