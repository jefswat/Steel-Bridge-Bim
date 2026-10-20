using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SBM.App.Viewport;
using SBM.Core.Drawing;
using SBM.Core.Drawing.Projection;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Units;

namespace SBM.App.Editors;

/// <summary>
/// 2D drawing window for a projected view ("Project view to 2D"): visible lines on the 3D layer names, hidden lines dashed on "-HIDDEN"
/// layers (toggle), click a line to see its element, export DXF (model units) or a PDF sheet (fitted standard scale).
/// </summary>
public sealed class ProjectionWindow : Window
{
    private readonly ProjectionResult _result;
    private readonly UnitSystem _units;
    private readonly DrawingCanvas _canvas = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4, 8, 4), TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _hidden = new() { Content = "Hidden lines", IsChecked = true, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly string _title;

    public ProjectionWindow(ProjectionResult result, UnitSystem units, string title)
    {
        _result = result;
        _units = units;
        _title = title;
        Title = $"Projected view - {title}";
        Width = 1400; Height = 900;
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        Button B(string text, RoutedEventHandler h) { var b = new Button { Content = text, Margin = new Thickness(4, 0, 4, 0), Padding = new Thickness(10, 3, 10, 3) }; b.Click += h; return b; }
        bar.Children.Add(B("Zoom extents", (_, _) => _canvas.ZoomExtents()));
        bar.Children.Add(_hidden);
        bar.Children.Add(B("Export DXF...", (_, _) => ExportDxf()));
        bar.Children.Add(B("Export PDF...", (_, _) => ExportPdf()));
        _hidden.Click += (_, _) => _canvas.Drawing = Filtered();
        var grid = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        grid.Children.Add(bar);
        grid.Children.Add(_status);
        grid.Children.Add(_canvas);
        Content = grid;
        _canvas.Drawing = Filtered();
        _canvas.Clicked += p =>
        {
            var e = _result.Pick(p, _canvas.PixelSize * 6);
            _status.Text = e == null ? Summary() : $"{e.Name} ({e.Kind}) on {e.Layer}, {e.Bridge} - key {e.Key}";
        };
        _status.Text = Summary();
    }

    private string Summary() => $"{_result.View.Describe}. {_result.Stats}. Click a line to identify its element.";

    /// <summary>The drawing shown / exported: with or without the hidden layers.</summary>
    public PlanDrawing Filtered()
    {
        if (_hidden.IsChecked == true) return _result.Drawing;
        var d = new PlanDrawing();
        foreach (var l in _result.Drawing.Layers.Where(l => !l.Name.EndsWith(ModelProjector.HiddenSuffix)))
            d.Layer(l.Style).Polylines.AddRange(l.Polylines);
        return d;
    }

    private void ExportDxf()
    {
        var dlg = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = _title + ".dxf" };
        if (dlg.ShowDialog(this) != true) return;
        try { SBM.IO.Dxf.DxfWriter.Write(dlg.FileName, Filtered(), _units.LongUnit, flat: true); _status.Text = $"Wrote {dlg.FileName}"; }
        catch (IOException ex) { _status.Text = $"Could not write: {ex.Message}"; }
    }

    private void ExportPdf()
    {
        var dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = _title + ".pdf" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var sheets = Sheet(_result, Filtered(), _title, _units);
            SBM.IO.Pdf.PdfWriter.Write(dlg.FileName, sheets, _title);
            _status.Text = $"Wrote {dlg.FileName} ({string.Join("; ", sheets[0].ScaleNotes)})";
        }
        catch (IOException ex) { _status.Text = $"Could not write: {ex.Message}"; }
    }

    /// <summary>One 11x17 sheet with the projected drawing at the largest standard scale that fits.</summary>
    public static List<Sheet> Sheet(ProjectionResult r, PlanDrawing drawing, string title, UnitSystem u)
    {
        var shown = new ProjectionResult { Drawing = drawing, Stats = r.Stats, View = r.View };
        var fmt = new SheetFormat(17, 11, title, "", DateTime.Now.ToString("MM/dd/yyyy"));
        var (ax0, ay0, ax1, ay1) = fmt.Area;
        var spec = new SheetSpec(title, r.View.Describe, new[] { new SBM.Core.Drawing.Sheets.Panel(new ViewSpec(title,
            s => SBM.Core.Drawing.Views.ProjectedViews.View(shown, title, title.ToUpperInvariant(), "Projected from the 3D model", s, u), ScaleFamily.Engineering), 0, 0, ax1 - ax0, ay1 - ay0) });
        return SheetComposer.Number(new List<Sheet> { SheetComposer.Compose(spec, fmt, u) });
    }
}
