using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SBM.IO.Database;

namespace SBM.App.Database;

/// <summary>
/// Properties-side panel with the database record of the selected element: GlobalId, clickable hierarchy chain, category / mark /
/// quantities, asset records (photos / documents, inspection history, findings, calcs: ElementDbPanel.Assets.cs), the plan sheets it is based on
/// (open the single-page PDF, preview, set the QC status and note).
/// Built in code so it follows the app's dark theme resources.
/// </summary>
public sealed partial class ElementDbPanel : UserControl
{
    public event Action? CreateRequested;
    public event Action? ImportPagesRequested;
    /// <summary>A hierarchy item was clicked (global id).</summary>
    public event Action<string>? NavigateRequested;
    public event Action<string, string>? QcSaved;
    /// <summary>Open a linked sheet in the app's plan sheet window (element GlobalId, sheet).</summary>
    public event Action<string, SheetLink>? OpenSheetRequested;

    private readonly StackPanel _root = new() { Margin = new Thickness(8, 4, 8, 8) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly Button _create = Btn("Create database");
    private readonly Button _import = Btn("Import plan pages…");
    private readonly StackPanel _body = new();
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
    private static readonly Brush Link = new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xFF));
    private static readonly Brush Card = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));

    /// <summary>The text of the last shown record (chain + sheets), for logs and snapshot checks.</summary>
    public string LastSummary { get; private set; } = "";
    /// <summary>How the last Open PDF was done (viewer, page), for logs.</summary>
    public string LastOpen { get; private set; } = "";

    public ElementDbPanel()
    {
        var buttons = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        buttons.Children.Add(_create);
        buttons.Children.Add(_import);
        _create.Click += (_, _) => CreateRequested?.Invoke();
        _import.Click += (_, _) => ImportPagesRequested?.Invoke();
        _root.Children.Add(Header("Database"));
        _root.Children.Add(_status);
        _root.Children.Add(buttons);
        _root.Children.Add(_body);
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _root };
    }

    private static Button Btn(string text) => new() { Content = text, Margin = new Thickness(0, 0, 6, 4), Padding = new Thickness(8, 1, 8, 1), MinWidth = 0 };

    private static TextBlock Header(string text) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2), Foreground = (Brush)(Application.Current.TryFindResource("AccentBrushSbm") ?? Link),
    };

    public void SetStatus(string text, bool canCreate, bool create, bool canImport)
    {
        _status.Text = text;
        _create.IsEnabled = canCreate;
        _create.Content = create ? "Create database" : "Rebuild database";
        _import.IsEnabled = canImport;
    }

    public void Clear()
    {
        _body.Children.Clear();
        LastSummary = "";
    }

    public void ShowMessage(string text)
    {
        _body.Children.Clear();
        _body.Children.Add(new TextBlock { Text = text, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        LastSummary = text;
    }

    private static string Q(double? v, string fmt, string unit) => v is { } d ? d.ToString(fmt, CultureInfo.InvariantCulture) + unit : "";

    private static TextBlock Row(string label, string value, StackPanel into)
    {
        if (string.IsNullOrEmpty(value)) return new TextBlock();
        var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11 });
        var v = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        into.Children.Add(g);
        return v;
    }

    /// <summary>Shows the record of <paramref name="globalId"/> from <paramref name="db"/>.</summary>
    public void ShowElement(ModelDb db, string globalId)
    {
        _body.Children.Clear();
        var chain = db.Chain(globalId);
        if (chain.Count == 0)
        {
            ShowMessage($"Not in the database (GlobalId {globalId}). The model changed since the database was built: rebuild it.");
            return;
        }
        var e = chain[^1];
        var sum = new System.Text.StringBuilder();

        _body.Children.Add(Header("Element"));
        var info = new StackPanel();
        Row("Name", e.Name, info);
        Row("Level", e.Level + (e.Category.Length > 0 ? " / " + e.Category : ""), info);
        Row("Mark", e.Mark, info);
        Row("Type", e.Subtype, info);
        Row("Unit", string.Join(" ", new[] { e.BridgeGroup, e.Unit }.Where(x => x.Length > 0)), info);
        Row("Support", e.Support, info);
        Row("Girder", e.Girder, info);
        Row("Station", e.StationRange, info);
        Row("Material", e.Material, info);
        Row("Mass", Q(e.MassKg, "N1", " kg"), info);
        Row("Volume", Q(e.VolumeM3, "N4", " m3"), info);
        Row("Length", Q(e.LengthM, "N3", " m"), info);
        Row("IFC", e.InIfc ? e.IfcClass : "not an IFC entity (grouping)", info);
        var idRow = new TextBox { Text = e.GlobalId, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontFamily = new FontFamily("Consolas"), FontSize = 11 };
        var idGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        idGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        idGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        idGrid.Children.Add(new TextBlock { Text = "GlobalId", Foreground = Muted, FontSize = 11 });
        Grid.SetColumn(idRow, 1);
        idGrid.Children.Add(idRow);
        info.Children.Add(idGrid);
        _body.Children.Add(info);

        _body.Children.Add(Header("Hierarchy"));
        var crumbs = new WrapPanel();
        for (int i = 0; i < chain.Count; i++)
        {
            var c = chain[i];
            string label = c.Name.Length > 0 ? c.Name : c.Level;
            bool last = i == chain.Count - 1;
            var tb = new TextBlock { Text = label, FontSize = 11, Margin = new Thickness(0, 0, 0, 0), Cursor = last ? Cursors.Arrow : Cursors.Hand,
                Foreground = last ? Brushes.White : Link, ToolTip = $"{c.Level} / {c.Category}" };
            if (!last)
            {
                tb.TextDecorations = TextDecorations.Underline;
                string id = c.GlobalId;
                tb.MouseLeftButtonUp += (_, _) => NavigateRequested?.Invoke(id);
            }
            crumbs.Children.Add(tb);
            if (!last) crumbs.Children.Add(new TextBlock { Text = " › ", Foreground = Muted, FontSize = 11 });
            sum.Append(label).Append(last ? "" : " > ");
        }
        _body.Children.Add(crumbs);
        int kids = db.ChildCount(globalId);
        if (kids > 0 && e.Level == "member")
        {
            // built-up member / joint gusset stack: every sub-element (ply, angle, cover plate, gusset layer, fill); a row click highlights that piece
            _body.Children.Add(Header($"Sub-elements ({kids})"));
            var list = new StackPanel();
            foreach (var c in db.Children(globalId, 1000))
            {
                var row = new TextBlock { FontSize = 11, Foreground = Link, Cursor = Cursors.Hand, Margin = new Thickness(0, 1, 0, 1), TextWrapping = TextWrapping.Wrap,
                    Text = $"{c.Name}   {c.Subtype}{(c.MassKg is > 0 ? $"   {c.MassKg:N0} kg" : "")}", ToolTip = "Show this sub-element and highlight it" };
                string id = c.GlobalId;
                row.MouseLeftButtonUp += (_, _) => NavigateRequested?.Invoke(id);
                list.Children.Add(row);
            }
            _body.Children.Add(list);
            sum.Append($" | {kids} sub-elements");
        }
        else if (kids > 0)
        {
            var ch = db.Children(globalId, 12);
            var kp = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            kp.Children.Add(new TextBlock { Text = $"{kids:N0} children: ", Foreground = Muted, FontSize = 11 });
            foreach (var c in ch)
            {
                var tb = new TextBlock { Text = c.Name, FontSize = 11, Foreground = Link, Cursor = Cursors.Hand, TextDecorations = TextDecorations.Underline, Margin = new Thickness(0, 0, 8, 0) };
                string id = c.GlobalId;
                tb.MouseLeftButtonUp += (_, _) => NavigateRequested?.Invoke(id);
                kp.Children.Add(tb);
            }
            if (kids > ch.Count) kp.Children.Add(new TextBlock { Text = "…", Foreground = Muted });
            _body.Children.Add(kp);
        }

        ShowAssets(db, globalId, sum);

        var sheets = db.Sheets(globalId);
        _body.Children.Add(Header($"Based on plan sheets ({sheets.Count})"));
        if (sheets.Count == 0)
            _body.Children.Add(new TextBlock
            {
                Text = db.SheetCount == 0
                    ? "No plan sheets in the database: there is no sheet-links*.json for this model beside it (plan sets, sheet index and link rules; see docs/API.md), or it has no sheet index (then use Import plan pages…)."
                    : db.LinkCount == 0 ? "The database has plan sheets but no links yet: rebuild it." : "No sheet is linked to this element or its parents.",
                Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap,
            });
        foreach (var s in sheets)
        {
            _body.Children.Add(SheetCard(db, globalId, s));
            sum.Append($" | sheet {s.PlanSet} {s.DesignSheet}/p{s.PdfPage} {s.Title} [{s.Role}, {s.QcStatus}{(s.Inherited ? ", via " + s.LinkRootName : "")}]");
        }
        LastSummary = sum.ToString();
    }

    private UIElement SheetCard(ModelDb db, string globalId, SheetLink s)
    {
        var card = new Border { Background = Card, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(6, 4, 6, 4), CornerRadius = new CornerRadius(3) };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = (s.DesignSheet.Length > 0 ? $"Sheet {s.DesignSheet} · " : "") + s.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, FontSize = 11.5,
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"{s.PlanSet}  ·  design sheet {(s.DesignSheet.Length > 0 ? s.DesignSheet : "-")}  ·  PDF page {s.PdfPage}", Foreground = Muted, FontSize = 11,
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"{s.Role}" + (s.Basis.Length > 0 ? ": " + s.Basis : "") + (s.Inherited ? $"  (via {s.LinkRootName})" : ""),
            Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap,
        });
        var row = new WrapPanel { Margin = new Thickness(0, 3, 0, 0) };
        var open = Btn("Show sheet");
        var ext = Btn("PDF viewer");
        var prev = Btn("Preview");
        var target = db.PdfTarget(s);
        string pdf = target?.File ?? "";
        string img = s.PreviewFile.Length > 0 ? db.Resolve(s.PreviewFile) : "";
        open.IsEnabled = ext.IsEnabled = target != null;
        prev.IsEnabled = img.Length > 0 && File.Exists(img);
        // ToolTipService.ShowOnDisabled so the reason shows on a disabled button too.
        ToolTipService.SetShowOnDisabled(open, true);
        ToolTipService.SetShowOnDisabled(ext, true);
        ToolTipService.SetShowOnDisabled(prev, true);
        open.ToolTip = target is { } t ? $"{t.File}, page {t.Page}" : $"Plan PDF not found ({(s.PdfPath.Length > 0 ? s.PdfPath : s.PlanSet)}): put it beside the model, in a folder named in the rules file's planFolders or in %SBM_PLANS%, then rebuild.";
        prev.ToolTip = prev.IsEnabled ? img : "No preview image: Import plan pages… (splits the PDF and renders previews).";
        ext.ToolTip = target is { } te ? $"Open {Path.GetFileName(te.File)} at page {te.Page} in the external PDF viewer" : open.ToolTip;
        open.ToolTip = target is { } to ? $"Show {Path.GetFileName(to.File)}, page {to.Page}, in the plan sheet window" : open.ToolTip;
        open.Click += (_, _) => OpenSheetRequested?.Invoke(globalId, s);
        ext.Click += (_, _) =>
        {
            if (db.PdfTarget(s) is not { } tt) return;
            try { LastOpen = PdfOpener.Open(tt.File, tt.Page); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this)!, ex.Message, "Open PDF", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        prev.Click += (_, _) => PlanPreviewWindow.ShowSheet(Window.GetWindow(this)!, $"{s.PlanSet} · design sheet {s.DesignSheet} · PDF {s.PdfPage} · {s.Title}", img, pdf, target?.Page ?? 1);
        row.Children.Add(open); row.Children.Add(ext); row.Children.Add(prev);
        var status = new ComboBox { MinWidth = 90, Margin = new Thickness(0, 0, 6, 4), ItemsSource = new[] { "unchecked", "ok", "issue" }, SelectedItem = s.QcStatus };
        row.Children.Add(status);
        sp.Children.Add(row);
        var note = new TextBox { Text = s.Note, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, MinHeight = 22, FontSize = 11, ToolTip = "QC note" };
        var save = Btn("Save QC");
        var saved = new TextBlock { FontSize = 11, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Text = s.CheckedAt.Length > 0 ? $"{s.CheckedBy}, {s.CheckedAt}" : "" };
        save.Click += (_, _) =>
        {
            db.SetQc(globalId, s.SheetId, (string)(status.SelectedItem ?? "unchecked"), null, note.Text);
            saved.Text = "saved " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            QcSaved?.Invoke(globalId, s.SheetId);
        };
        sp.Children.Add(note);
        var line = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
        DockPanel.SetDock(save, Dock.Left);
        line.Children.Add(save); line.Children.Add(saved);
        sp.Children.Add(line);
        card.Child = sp;
        return card;
    }
}
