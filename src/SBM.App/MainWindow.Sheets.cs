using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Drawing.Views;
using SBM.IO.Dxf;
using SBM.IO.Pdf;

namespace SBM.App;

/// <summary>Sheets mode: 11x17 plan sheets composed from the current model, previewed live and exported to PDF / DXF.</summary>
public partial class MainWindow
{
    private List<Sheet> _sheets = new();
    private bool _sheetsStale = true;
    private int _sheetBuild;

    private bool SheetsMode => ModeSheets.IsChecked == true;

    private void OnModeModel(object sender, RoutedEventArgs e)
    {
        SheetsPanel.Visibility = Visibility.Collapsed;
        ViewSwitch.Visibility = Visibility.Visible;
        UpdateIsolateBanner();
        UpdateNorthArrow();
    }

    private async void OnModeSheets(object sender, RoutedEventArgs e)
    {
        SheetsPanel.Visibility = Visibility.Visible;
        ViewSwitch.Visibility = Visibility.Collapsed;
        UpdateIsolateBanner();
        UpdateNorthArrow();
        if (_sheetsStale) await RebuildSheetsAsync();
    }

    /// <summary>Called after every regeneration: sheets rebuild now if visible, otherwise on the next switch.</summary>
    private void MarkSheetsStale()
    {
        _sheetsStale = true;
        if (SheetsMode) _ = RebuildSheetsAsync();
    }

    private async Task RebuildSheetsAsync()
    {
        int version = ++_sheetBuild;
        if (_vm.BuildDrawingContext() is not { } ctx) { SheetStatus.Text = "No bridge to draw. Create a bridge first."; return; }
        SheetStatus.Text = "Building sheets…";
        int keep = SheetList.SelectedIndex;
        var fmt = new SheetFormat(17, 11, _vm.Project.Name, ctx.Framing.Definition.Name, DateTime.Now.ToString("MM/dd/yyyy"));
        var sw = Stopwatch.StartNew();
        List<Sheet> sheets;
        try { sheets = await Task.Run(() => SheetSet.Build(ctx, fmt)); }
        catch (Exception ex)
        {
            ViewModels.MainViewModel.Log(ex.ToString());
            SheetStatus.Text = $"Sheet generation failed: {ex.Message}";
            return;
        }
        if (version != _sheetBuild) return;
        _sheets = sheets;
        _sheetsStale = false;
        SheetList.ItemsSource = sheets;
        SheetList.SelectedIndex = Math.Clamp(keep, 0, sheets.Count - 1);
        var warn = sheets.SelectMany(s => s.Warnings).ToList();
        SheetStatus.Text = $"{sheets.Count} sheets (11x17) built in {sw.ElapsedMilliseconds} ms" + (warn.Count > 0 ? $" — {warn[0]}" : "");
    }

    private void OnSheetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SheetList.SelectedItem is Sheet s)
        {
            SheetCanvas.PaperSize = (s.WidthIn, s.HeightIn);
            SheetCanvas.Drawing = s.Drawing;
        }
    }

    private void OnSheetZoom(object sender, RoutedEventArgs e) => SheetCanvas.ZoomExtents();

    private string BridgeFileName => _vm.Project.Bridges.FirstOrDefault()?.Name ?? "Bridge";

    private void OnExportPdf(object sender, RoutedEventArgs e)
    {
        if (_sheets.Count == 0) return;
        var dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"{BridgeFileName} Sheets.pdf" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            PdfWriter.Write(dlg.FileName, _sheets, BridgeFileName);
            SheetStatus.Text = $"Wrote {_sheets.Count} sheets to {dlg.FileName}";
        }
        catch (IOException ex) { SheetStatus.Text = $"Could not write the PDF: {ex.Message}"; }
    }

    private void OnExportSheetsDxf(object sender, RoutedEventArgs e)
    {
        if (_sheets.Count == 0) return;
        var dlg = new OpenFolderDialog { Title = "Folder for the sheet DXF files (paper inches)" };
        if (dlg.ShowDialog() != true) return;
        foreach (var s in _sheets)
            SheetExport.Write(dlg.FolderName, BridgeFileName, new SheetView(s.Title, $"{s.Number} {s.Title}", s.Drawing, 1, "", PaperSpace: true), _vm.Units);
        SheetStatus.Text = $"Wrote {_sheets.Count} sheet DXF files to {dlg.FolderName}";
    }

    /// <summary>One .zip with every sheet as a paper-space DXF, optionally with the full-size 2D model DXFs ("models/").</summary>
    private void OnExportSheetsDxfZip(object sender, RoutedEventArgs e)
    {
        if (_sheets.Count == 0) return;
        var dlg = new SaveFileDialog { Filter = "ZIP archive (*.zip)|*.zip", FileName = $"{BridgeFileName} Sheets DXF.zip", Title = "Sheet DXF files (paper inches) as one zip" };
        if (dlg.ShowDialog() != true) return;
        var ask = MessageBox.Show(this, "Also include the full-size 2D model DXFs (layout, sections, bents, bar bending) in a 'models' folder of the zip?",
            "Export sheets DXF (zip)", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (ask == MessageBoxResult.Cancel) return;
        try
        {
            var models = ask == MessageBoxResult.Yes ? _vm.BuildDrawings()?.views : null;
            var files = SheetExport.WriteSheets(dlg.FileName, BridgeFileName, _sheets, _vm.Units, models);
            SheetStatus.Text = $"Wrote {files.Count} DXF files ({_sheets.Count} sheets{(models != null ? $", {models.Count} 2D models" : "")}) to {dlg.FileName}";
        }
        catch (IOException ex) { SheetStatus.Text = $"Could not write the zip: {ex.Message}"; }
    }

    private void OnExport2DModels(object sender, RoutedEventArgs e)
    {
        if (_vm.BuildDrawings() is not { } r) return;
        var dlg = new OpenFolderDialog { Title = "Folder for the 2D DXF models (full size)" };
        if (dlg.ShowDialog() != true) return;
        foreach (var v in r.views) SheetExport.Write(dlg.FolderName, r.bridge.Name, v, _vm.Units);
        SheetStatus.Text = $"Wrote {r.views.Count} 2D model DXF files to {dlg.FolderName}";
    }
}
