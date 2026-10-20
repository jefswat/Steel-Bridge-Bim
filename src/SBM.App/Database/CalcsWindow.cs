using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using SBM.Api;
using SBM.IO.Database;

namespace SBM.App.Database;

/// <summary>
/// Project-level calc packages (G9): list of calcs (Add calc…, Import values…), the stored calc PDF at its summary pages in the embedded Edge
/// PDF viewer (as the plan sheet window), the table of extracted values vs the model, and Apply selected… (confirmation listing every change;
/// each applied change is logged in the database with who / when / old -> new / calc page, then the model is regenerated and saved).
/// Differences are only shown; nothing is applied without the button and the confirmation.
/// </summary>
public sealed class CalcsWindow : Window
{
    /// <summary>What the window needs from the main window: the current database (reopened after rebuilds), a document over the live model
    /// (with its results) and the apply action (checkpoint, set, log, regenerate, save).</summary>
    public sealed class Host
    {
        public required Func<ModelDb?> Db { get; init; }
        public required Func<SbmDocument> Doc { get; init; }
        public required Func<IReadOnlyList<long>, Task<CalcApplyResult>> ApplyAsync { get; init; }
    }

    public sealed class Row
    {
        public bool Selected { get; set; }
        public bool CanSelect { get; init; }
        public long Id { get; init; }
        public string Name { get; init; } = "";
        public string Kind { get; init; } = "";
        public string Target { get; init; } = "";
        public string Model { get; init; } = "";
        public string Calc { get; init; } = "";
        public string Status { get; init; } = "";
        public string Page { get; init; } = "";
        public string Quote { get; init; } = "";
        public string Message { get; init; } = "";
        public int PageNo { get; init; }
    }

    private static CalcsWindow? _instance;
    public static string LastSummary { get; private set; } = "";

    private readonly Host _host;
    private readonly ListBox _list = new() { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 6, 8, 6), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly WrapPanel _pages = new() { Margin = new Thickness(8, 0, 8, 4) };
    private readonly TextBlock _message = new() { Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gainsboro };
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4, 8, 4), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gainsboro, FontSize = 11.5 };
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single, FontSize = 11.5 };
    private readonly TextBox _summary = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 11 };
    private readonly WebView2 _web = new();
    private readonly Grid _viewer = new();
    private List<Row> _rows = new();
    private CalcPackage? _calc;
    private string _pdf = "";
    private bool _ready;
    private (string file, int page)? _pending;
    private int _nav;

    public static void ShowCalcs(Window owner, Host host, long calcId = 0)
    {
        if (_instance == null || !_instance.IsLoaded)
        {
            _instance = new CalcsWindow(host) { Owner = owner };
            _instance.Show();
        }
        _instance.Reload(calcId);
        _instance.Activate();
    }

    public static void CloseIfOpen() { if (_instance is { IsLoaded: true } w) w.Close(); }

    private CalcsWindow(Host host)
    {
        _host = host;
        Title = "Calcs";
        Width = 1350; Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));

        Button B(string text, string tip, RoutedEventHandler click)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(10, 2, 10, 2), ToolTip = tip };
            b.Click += click;
            return b;
        }
        var top = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)) };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 4, 8, 4) };
        tools.Children.Add(B("Add calc…", "Copy a calc PDF into the assets folder and record its summary pages", (_, _) => AddCalc()));
        tools.Children.Add(B("Import values…", "Read the values extracted from the summary pages (JSON, schema sbm-calc-values/1)", (_, _) => ImportValues()));
        tools.Children.Add(B("Open in PDF viewer", "Open the calc PDF at the shown page in the external viewer", (_, _) => { if (_pdf.Length > 0) PdfOpener.Open(_pdf, _shownPage); }));
        DockPanel.SetDock(tools, Dock.Right);
        top.Children.Add(tools); top.Children.Add(_title);

        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is ListBoxItem { Tag: CalcPackage c }) ShowCalc(c); };
        var left = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        _viewer.Children.Add(_web);
        _viewer.Children.Add(_message);
        var pdfPane = new DockPanel();
        DockPanel.SetDock(_pages, Dock.Top);
        pdfPane.Children.Add(_pages); pdfPane.Children.Add(_viewer);

        BuildGrid();
        var apply = B("Apply selected…", "Set the selected model inputs to the calc values (confirmation first); every change is logged", async (_, _) => await ApplyAsync());
        var refresh = B("Refresh", "Compare again with the current model", (_, _) => { if (_calc != null) FillRows(_calc); });
        var selAll = B("Select all differences", "Select every differing model input", (_, _) => { foreach (var r in _rows) r.Selected = r.CanSelect; _grid.Items.Refresh(); });
        var gridBar = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
        var gb = new StackPanel { Orientation = Orientation.Horizontal };
        gb.Children.Add(refresh); gb.Children.Add(selAll); gb.Children.Add(apply);
        DockPanel.SetDock(gb, Dock.Right);
        gridBar.Children.Add(gb); gridBar.Children.Add(_status);
        var tabs = new TabControl { Margin = new Thickness(0, 2, 0, 0) };
        tabs.Items.Add(new TabItem { Header = "Values vs model", Content = _grid });
        tabs.Items.Add(new TabItem { Header = "Summary text", Content = _summary });
        tabs.Items.Add(new TabItem { Header = "Applied changes", Content = _log });
        var bottom = new DockPanel();
        DockPanel.SetDock(gridBar, Dock.Top);
        bottom.Children.Add(gridBar); bottom.Children.Add(tabs);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.25, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var hsplit = new GridSplitter { Height = 4, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)) };
        Grid.SetRow(pdfPane, 0); Grid.SetRow(hsplit, 1); Grid.SetRow(bottom, 2);
        right.Children.Add(pdfPane); right.Children.Add(hsplit); right.Children.Add(bottom);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var vsplit = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)) };
        Grid.SetColumn(left, 0); Grid.SetColumn(vsplit, 1); Grid.SetColumn(right, 2);
        body.Children.Add(left); body.Children.Add(vsplit); body.Children.Add(right);
        var root = new DockPanel { Background = Background }; // on the content too: snapshots render the content only
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top); root.Children.Add(body);
        Content = root;
        Closed += (_, _) => _instance = null;
        Loaded += async (_, _) => await InitWebAsync();
    }

    private readonly DataGrid _log = new() { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, FontSize = 11.5 };
    private int _shownPage = 1;

    private void BuildGrid()
    {
        var sel = new DataGridCheckBoxColumn { Header = "Apply", Binding = new Binding(nameof(Row.Selected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } };
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(IsEnabledProperty, new Binding(nameof(Row.CanSelect))));
        sel.CellStyle = cellStyle;
        _grid.Columns.Add(sel);
        void Col(string h, string p, double w, DataGridLength? len = null)
        {
            var c = new DataGridTextColumn { Header = h, Binding = new Binding(p), IsReadOnly = true, Width = len ?? new DataGridLength(w) };
            if (p == nameof(Row.Status))
            {
                var st = new Style(typeof(TextBlock));
                st.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                AddColor(st, "match", Color.FromRgb(0x6A, 0xC0, 0x6A));
                AddColor(st, "differs", Color.FromRgb(0xE8, 0xA8, 0x40));
                AddColor(st, "missing", Color.FromRgb(0xF0, 0x60, 0x60));
                AddColor(st, "error", Color.FromRgb(0xF0, 0x60, 0x60));
                c.ElementStyle = st;
            }
            _grid.Columns.Add(c);
        }
        static void AddColor(Style st, string value, Color c)
        {
            var t = new DataTrigger { Binding = new Binding(nameof(Row.Status)), Value = value };
            t.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(c)));
            st.Triggers.Add(t);
        }
        Col("#", nameof(Row.Id), 40);
        Col("Value", nameof(Row.Name), 170);
        Col("Status", nameof(Row.Status), 70);
        Col("Model", nameof(Row.Model), 110);
        Col("Calc", nameof(Row.Calc), 110);
        Col("Target", nameof(Row.Target), 260);
        Col("Page", nameof(Row.Page), 45);
        Col("Source line", nameof(Row.Quote), 0, new DataGridLength(1, DataGridLengthUnitType.Star));
        _grid.SelectionChanged += (_, _) => { if (_grid.SelectedItem is Row r && r.PageNo > 0) ShowPage(r.PageNo); };

        foreach (var (h, p, w) in new[] { ("When", nameof(AppliedChange.AppliedAt), 140.0), ("By", nameof(AppliedChange.AppliedBy), 100), ("Property", nameof(AppliedChange.PropertyPath), 280),
                     ("Old", nameof(AppliedChange.OldValue), 100), ("New", nameof(AppliedChange.NewValue), 100), ("Page", nameof(AppliedChange.Page), 45), ("Calc", nameof(AppliedChange.CalcTitle), 200) })
            _log.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p), Width = new DataGridLength(w) });
    }

    private async Task InitWebAsync()
    {
        try
        {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelBridgeModeler", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, data);
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _ready = true;
            if (_pending is { } p) { _pending = null; Navigate(p.file, p.page); }
        }
        catch (Exception ex)
        {
            ShowMessage("The embedded PDF viewer needs the Microsoft Edge WebView2 Runtime. Use \"Open in PDF viewer\" meanwhile.\n\n" + ex.Message);
        }
    }

    private void Reload(long calcId)
    {
        var db = _host.Db();
        _list.Items.Clear();
        if (db == null) { ShowMessage("No database for this model: create it in the Properties panel (Database)."); return; }
        var calcs = db.Calcs();
        foreach (var c in calcs)
        {
            var sp = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
            sp.Children.Add(new TextBlock { Text = c.Title + (c.Revision.Length > 0 ? " rev " + c.Revision : ""), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, FontSize = 11.5 });
            int n = db.CalcValues(c.Id).Count, a = db.AppliedLog(c.Id).Count;
            sp.Children.Add(new TextBlock { Text = $"{db.GetFile(c.FileId)?.FileName} · {c.PageCount} p. · summary {c.SummaryPages}\n{n} values · {a} applied", FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
            _list.Items.Add(new ListBoxItem { Content = sp, Tag = c });
        }
        if (calcs.Count == 0)
        {
            _title.Text = "No calc packages";
            ShowMessage("No calc packages yet. Add calc… copies a calc PDF into the assets folder and records its summary pages (the 1-2 pages with the final design conclusions). "
                        + "Then extract the summary values into a JSON file (schema sbm-calc-values/1, see docs/API.md) and use Import values… to compare them with the model.");
            _rows = new List<Row>();
            _grid.ItemsSource = _rows;
            return;
        }
        var pick = calcs.FirstOrDefault(c => c.Id == calcId) ?? (_calc != null ? calcs.FirstOrDefault(c => c.Id == _calc.Id) : null) ?? calcs[0];
        _list.SelectedIndex = calcs.IndexOf(pick);
    }

    private void ShowCalc(CalcPackage c)
    {
        var db = _host.Db();
        if (db == null) return;
        _calc = c;
        var f = db.GetFile(c.FileId);
        _title.Text = $"{c.Title}{(c.Revision.Length > 0 ? " rev " + c.Revision : "")} · {f?.FileName} · summary pages {c.SummaryPages}";
        Title = "Calcs - " + c.Title;
        _summary.Text = c.SummaryText.Length > 0 ? c.SummaryText : "(no summary text: python + PyMuPDF read it when the calc is added)";
        _pages.Children.Clear();
        foreach (int p in c.SummaryPageList)
        {
            var b = new Button { Content = $"Summary p. {p}", Margin = new Thickness(0, 4, 6, 0), Padding = new Thickness(8, 1, 8, 1) };
            int pp = p;
            b.Click += (_, _) => ShowPage(pp);
            _pages.Children.Add(b);
        }
        _pdf = "";
        if (f != null)
        {
            var loc = db.ResolveAsset(f);
            if (loc.IsFile && loc.Exists) _pdf = loc.LocalPath!;
            else if (!loc.IsFile && loc.Url != null) _pdf = loc.Url.AbsoluteUri;
            else ShowMessage($"Calc PDF missing: {loc.Target} (the record is kept; restore the file into the assets folder).");
        }
        if (_pdf.Length > 0) ShowPage(c.SummaryPageList[0]);
        FillRows(c);
        _log.ItemsSource = db.AppliedLog(c.Id);
    }

    private void FillRows(CalcPackage c)
    {
        var db = _host.Db();
        if (db == null) return;
        var vals = db.CalcValues(c.Id);
        _rows = new List<Row>();
        if (vals.Count > 0)
        {
            SbmDocument doc;
            try { doc = _host.Doc(); }
            catch (Exception ex) { _status.Text = "Model not available: " + ex.Message; return; }
            foreach (var v in vals)
            {
                var r = CalcSync.DiffOne(doc, db, v, c.Title);
                _rows.Add(new Row
                {
                    Id = v.Id, Name = v.Name, Kind = r.Kind, Target = r.Target, Model = r.ModelText, Calc = r.CalcText, Status = r.Status, CanSelect = r.Applicable,
                    Page = v.Page?.ToString(CultureInfo.InvariantCulture) ?? "", PageNo = v.Page ?? 0, Quote = v.Quote, Message = r.Message,
                });
            }
        }
        _grid.ItemsSource = _rows;
        int diff = _rows.Count(r => r.Status == "differs"), ap = _rows.Count(r => r.CanSelect);
        _status.Text = vals.Count == 0
            ? "No values for this calc yet: Import values… (JSON, schema sbm-calc-values/1)."
            : $"{_rows.Count} values: {_rows.Count(r => r.Status == "match")} match, {diff} differ ({ap} model inputs can be applied; results are only reported), {_rows.Count(r => r.Status is "missing" or "error")} missing / unreadable.";
        LastSummary = $"calc {c.Id} {c.Title}: " + string.Join("; ", _rows.Select(r => $"[{r.Id}] {r.Name} {r.Status} model {r.Model} calc {r.Calc}"));
        ViewModels.MainViewModel.Log("CALCS: " + LastSummary);
    }

    private void ShowPage(int page)
    {
        _shownPage = page;
        if (_pdf.Length == 0) return;
        if (_ready) Navigate(_pdf, page); else _pending = (_pdf, page);
    }

    private void Navigate(string file, int page)
    {
        _message.Visibility = Visibility.Collapsed;
        _web.Visibility = Visibility.Visible;
        string uri = file.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? file : new Uri(Path.GetFullPath(file)).AbsoluteUri;
        _web.CoreWebView2.Navigate(uri + "?v=" + (++_nav) + "#page=" + page + "&view=FitH");
    }

    private void ShowMessage(string text)
    {
        _message.Text = text;
        _message.Visibility = Visibility.Visible;
        _web.Visibility = Visibility.Collapsed;
    }

    private void AddCalc()
    {
        var db = _host.Db();
        if (db == null) return;
        var dlg = new OpenFileDialog { Filter = "PDF|*.pdf", Title = "Calc package (PDF)" };
        if (dlg.ShowDialog(this) != true) return;
        var form = new FormDialog("Add calc", $"{Path.GetFileName(dlg.FileName)} is copied into {db.AssetsFolder}\\calcs. Summary pages = the 1-2 pages with the final design conclusions.")
            .Text("Title", Path.GetFileNameWithoutExtension(dlg.FileName), required: true).Text("Summary pages", "1-2", required: true)
            .Text("Revision").Text("Date").Text("Engineer").Text("Elements", "", false);
        form.Owner = this;
        if (form.ShowDialog() != true) return;
        try
        {
            var els = form.Get("Elements").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(db.FindElement).ToList();
            var c = db.AddCalc(dlg.FileName, form.Get("Summary pages"), form.Get("Title"), form.Get("Revision") is { Length: > 0 } r ? r : null,
                form.Get("Date") is { Length: > 0 } d ? d : null, form.Get("Engineer") is { Length: > 0 } e ? e : null, els, null, m => ViewModels.MainViewModel.Log("DB: " + m));
            Reload(c.Id);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Add calc", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ImportValues()
    {
        var db = _host.Db();
        if (db == null || _calc == null) return;
        var dlg = new OpenFileDialog { Filter = "Values JSON|*.json|All files|*.*", Title = $"Values extracted from \"{_calc.Title}\" (schema sbm-calc-values/1)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            if (db.CalcValues(_calc.Id).Count > 0 && MessageBox.Show(this, "Replace the values of this calc? (The log of applied changes is kept.)", "Import values",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            int n = CalcSync.ImportValues(db, _calc.Id, dlg.FileName);
            ViewModels.MainViewModel.Log($"DB: calc {_calc.Id}: {n} values from {dlg.FileName}");
            Reload(_calc.Id);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import values", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async Task ApplyAsync()
    {
        if (_calc == null) return;
        var sel = _rows.Where(r => r.Selected && r.CanSelect).ToList();
        if (sel.Count == 0) { MessageBox.Show(this, "Tick the model inputs to apply (only differing inputs can be applied; results are reported only).", "Apply", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var db = _host.Db();
        var vals = db?.CalcValues(_calc.Id).ToDictionary(v => v.Id);
        string list = string.Join("\n", sel.Select(r => $"  {vals?[r.Id].PropertyPath}: {r.Model} -> {r.Calc}   (p.{r.Page})"));
        if (MessageBox.Show(this, $"Apply {sel.Count} value(s) from \"{_calc.Title}\" to the model?\n\n{list}\n\nEach change is logged in the database (who, when, old -> new, calc page); the model is regenerated and saved. Undo restores the inputs (the log keeps the record).",
                "Apply calc values", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            var res = await _host.ApplyAsync(sel.Select(r => r.Id).ToList());
            string msg = $"{res.Applied.Count} change(s) applied" + (res.SavedTo != null ? $", saved {Path.GetFileName(res.SavedTo)}" : " (save the model)")
                         + (res.Skipped.Count > 0 ? "\nSkipped: " + string.Join("; ", res.Skipped.Select(s => $"[{s.value.Id}] {s.reason}")) : "");
            _status.Text = msg;
            ShowCalc(_calc);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Apply", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    /// <summary>Snapshot support: the window (WPF) and the PDF viewer (WebView2) as PNGs.</summary>
    public static async Task<string> CaptureAsync(string pngBase)
    {
        if (_instance is not { } w) return "not open";
        w.UpdateLayout();
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render((Visual)w.Content);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using (var fs = File.Create(pngBase + ".png")) enc.Save(fs);
        if (w._ready)
        {
            await using var fs2 = File.Create(pngBase + "_pdf.png");
            await w._web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs2);
        }
        return pngBase + ".png";
    }
}
