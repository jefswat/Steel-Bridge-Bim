using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SBM.IO.Database;

namespace SBM.App.Database;

/// <summary>
/// The plan sheets of the selected element INSIDE the app (G1): a separate, movable (second monitor), closable window with the element's
/// linked sheets on the left and the plan PDF on the right, opened at the sheet's page by the embedded Edge PDF viewer (WebView2).
/// One window; it follows the 3D / database selection while "Follow selection" is on. The first sheet shown is the element's own
/// detail sheet (then inherited detail, then general).
/// </summary>
public sealed class PlanSheetWindow : Window
{
    private static PlanSheetWindow? _instance;
    /// <summary>The user closed the window: picks no longer open it until a sheet is opened from the database panel.</summary>
    public static bool ClosedByUser { get; private set; }
    public static bool IsOpen => _instance is { IsLoaded: true };
    /// <summary>Last shown "file|page" (log / snapshot checks).</summary>
    public static string LastShown { get; private set; } = "";

    private readonly ListBox _list = new() { MinWidth = 250, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 6, 8, 6), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _message = new() { Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gainsboro };
    private readonly CheckBox _follow = new() { Content = "Follow selection", IsChecked = true, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly WebView2 _web = new();
    private readonly Grid _viewer = new();
    // PDF targets resolved at load: the main window disposes / reopens its ModelDb on rebuild and relink.
    private readonly Dictionary<string, (string File, int Page)?> _targets = new();
    private List<SheetLink> _sheets = new();
    private string _pdf = "";
    private int _page;
    private int _nav;
    private bool _ready;
    private (string file, int page)? _pending;
    private bool _filling;

    public static bool Following => _instance is { IsLoaded: true } w && w._follow.IsChecked == true;

    /// <summary>Shows the sheets of <paramref name="globalId"/> (opening the window), with <paramref name="sheet"/> or the best sheet selected.</summary>
    public static void ShowElement(Window owner, ModelDb db, string globalId, string label, SheetLink? sheet = null)
    {
        ClosedByUser = false;
        if (_instance == null || !_instance.IsLoaded)
        {
            _instance = new PlanSheetWindow { Owner = owner };
            _instance.Show();
        }
        _instance.Load(db, globalId, label, sheet);
        if (!_instance.IsActive && sheet != null) _instance.Activate();
    }

    /// <summary>Updates an open window that follows the selection (no-op otherwise).</summary>
    public static void Follow(ModelDb db, string globalId, string label)
    {
        if (Following) _instance!.Load(db, globalId, label, null);
    }

    public static void CloseIfOpen() { if (_instance is { IsLoaded: true } w) { w.Close(); ClosedByUser = false; } }

    private PlanSheetWindow()
    {
        Title = "Plan sheets";
        Width = 1250; Height = 860;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
        var external = new Button { Content = "Open in PDF viewer", Margin = new Thickness(8, 4, 8, 4), Padding = new Thickness(10, 2, 10, 2) };
        external.Click += (_, _) =>
        {
            if (_pdf.Length == 0) return;
            try { PdfOpener.Open(_pdf, _page); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open PDF", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        var top = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)) };
        DockPanel.SetDock(external, Dock.Right); DockPanel.SetDock(_follow, Dock.Right);
        top.Children.Add(external); top.Children.Add(_follow); top.Children.Add(_title);

        _list.SelectionChanged += (_, _) =>
        {
            if (_filling || _list.SelectedItem is not ListBoxItem { Tag: SheetLink s }) return;
            ShowSheet(s);
        };
        var listScroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _viewer.Children.Add(_web);
        _viewer.Children.Add(_message);
        var split = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)) };
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(listScroll, 0); Grid.SetColumn(split, 1); Grid.SetColumn(_viewer, 2);
        body.Children.Add(listScroll); body.Children.Add(split); body.Children.Add(_viewer);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top); root.Children.Add(body);
        Content = root;
        Closed += (_, _) => { ClosedByUser = true; _instance = null; };
        Loaded += async (_, _) => await InitWebAsync();
    }

    private async Task InitWebAsync()
    {
        try
        {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelBridgeModeler", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, data);
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.NavigationCompleted += (_, e) => ViewModels.MainViewModel.Log($"SHEETWIN: shown {LastShown} (ok {e.IsSuccess})");
            _ready = true;
            if (_pending is { } p) { _pending = null; Navigate(p.file, p.page); }
        }
        catch (Exception ex)
        {
            ShowMessage("The embedded PDF viewer needs the Microsoft Edge WebView2 Runtime (part of Windows 11; download it from Microsoft for Windows 10). "
                        + "Use \"Open in PDF viewer\" meanwhile.\n\n" + ex.Message);
            ViewModels.MainViewModel.Log("SHEETWIN: WebView2 unavailable: " + ex.Message);
        }
    }

    private void Load(ModelDb db, string globalId, string label, SheetLink? sheet)
    {
        _sheets = db.Sheets(globalId);
        _targets.Clear();
        foreach (var s in _sheets) _targets[s.SheetId] = db.PdfTarget(s);
        _title.Text = label;
        Title = "Plan sheets - " + label;
        _filling = true;
        _list.Items.Clear();
        foreach (var s in _sheets)
        {
            var sp = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
            sp.Children.Add(new TextBlock { Text = (s.DesignSheet.Length > 0 ? $"Sheet {s.DesignSheet} · " : "") + s.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, FontSize = 11.5 });
            sp.Children.Add(new TextBlock { Text = $"PDF page {s.PdfPage} · {s.Role}" + (s.Inherited ? $" (via {s.LinkRootName})" : ""), FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
            _list.Items.Add(new ListBoxItem { Content = sp, Tag = s, ToolTip = s.Basis.Length > 0 ? s.Basis : null });
        }
        _filling = false;
        if (_sheets.Count == 0)
        {
            ShowMessage(db.SheetCount == 0 ? "This model's database has no plan sheets (no sheet-links*.json found for it)." : "No plan sheet is linked to this element or its parents.");
            return;
        }
        var pick = sheet != null ? _sheets.FirstOrDefault(s => s.SheetId == sheet.SheetId) : null;
        pick ??= _sheets.FirstOrDefault(s => !s.Inherited && s.Role == "detail") ?? _sheets.FirstOrDefault(s => s.Role == "detail") ?? _sheets[0];
        _list.SelectedIndex = _sheets.IndexOf(pick); // -> ShowSheet
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private void ShowSheet(SheetLink s)
    {
        if (_targets.GetValueOrDefault(s.SheetId) is not { } t)
        {
            ShowMessage($"Plan PDF not found ({(s.PdfPath.Length > 0 ? s.PdfPath : s.PlanSet)}): put it beside the model, in a folder named in the rules file's planFolders or in %SBM_PLANS%, then reopen the model.");
            return;
        }
        if (_ready) Navigate(t.File, t.Page); else _pending = (t.File, t.Page);
    }

    private void Navigate(string file, int page)
    {
        _message.Visibility = Visibility.Collapsed;
        _web.Visibility = Visibility.Visible;
        _pdf = file; _page = page;
        LastShown = $"{file}|{page}";
        // A changed query string forces a real navigation: the Edge PDF viewer ignores a #page change on the document already shown.
        var uri = new Uri(Path.GetFullPath(file)).AbsoluteUri + "?v=" + (++_nav) + "#page=" + page + "&view=Fit";
        _web.CoreWebView2.Navigate(uri);
    }

    private void ShowMessage(string text)
    {
        _message.Text = text;
        _message.Visibility = Visibility.Visible;
        _web.Visibility = Visibility.Collapsed;
    }

    /// <summary>Snapshot support: the rendered viewer as a PNG (WPF capture does not include the WebView2 surface).</summary>
    public static async Task<bool> CaptureAsync(string png)
    {
        if (_instance is not { _ready: true } w) return false;
        await using var fs = File.Create(png);
        await w._web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
        return true;
    }
}
