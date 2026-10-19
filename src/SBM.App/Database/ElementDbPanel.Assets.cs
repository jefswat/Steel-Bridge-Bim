using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SBM.Api;
using SBM.IO.Database;

namespace SBM.App.Database;

/// <summary>
/// Asset management sections of the database panel (G9): photos and documents of the element (thumbnails, open full size, Add photo… /
/// Add document…), inspection history (latest AASHTO element-level CS quantities with defects, NBI / SNBI ratings, older inspections, findings
/// with follow-up status, Add finding…) and the calcs linked to the element with their values vs the model.
/// </summary>
public sealed partial class ElementDbPanel
{
    /// <summary>Compares a calc value with the live model (set by the main window).</summary>
    public Func<CalcValue, string, CalcDiffRow>? CalcDiff { get; set; }
    /// <summary>World point (E, N, Z m) of the last 3D pick, offered as the location of a new finding.</summary>
    public Func<(double x, double y, double z)?>? PickedPoint { get; set; }
    /// <summary>Open the project's Calcs window at a calc (0 = list).</summary>
    public event Action<long>? OpenCalcsRequested;

    private static readonly Brush Good = new SolidColorBrush(Color.FromRgb(0x6A, 0xC0, 0x6A));
    private static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0xE8, 0xA8, 0x40));
    private static readonly Brush Bad = new SolidColorBrush(Color.FromRgb(0xF0, 0x60, 0x60));

    private static Brush SeverityBrush(string s) => s switch { "critical" => Bad, "high" => Warn, "medium" => new SolidColorBrush(Color.FromRgb(0xE0, 0xD0, 0x60)), _ => Muted };
    private static Brush StatusBrush(string s) => s switch { "match" => Good, "differs" => Warn, _ => Bad };

    private static string N(double? v) => v is { } d ? d.ToString("#,0.##", CultureInfo.InvariantCulture) : "";

    private static TextBlock Small(string text, Brush? fg = null, bool wrap = true) =>
        new() { Text = text, FontSize = 11, Foreground = fg ?? Brushes.Gainsboro, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };

    private void ShowAssets(ModelDb db, string globalId, System.Text.StringBuilder sum)
    {
        try
        {
            ShowPhotos(db, globalId, sum);
            ShowInspection(db, globalId, sum);
            ShowCalcs(db, globalId, sum);
        }
        catch (Exception ex)
        {
            _body.Children.Add(Small("Asset records could not be read: " + ex.Message, Bad));
            ViewModels.MainViewModel.Log("DB assets: " + ex);
        }
    }

    private void Refresh(ModelDb db, string globalId)
    {
        double off = (Content as ScrollViewer)?.VerticalOffset ?? 0;
        ShowElement(db, globalId);
        Dispatcher.BeginInvoke(() => (Content as ScrollViewer)?.ScrollToVerticalOffset(off), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------ photos / documents

    /// <summary>Files of the element and of its ancestors (nearest first), with the ancestor's name for inherited ones.</summary>
    private static List<(AssetFile file, string via)> FilesOf(ModelDb db, string globalId)
    {
        var list = new List<(AssetFile, string)>();
        var seen = new HashSet<long>();
        var chain = db.Chain(globalId);
        for (int i = chain.Count - 1; i >= 0; i--)
            foreach (var f in db.FilesForElement(chain[i].GlobalId).Where(f => f.Kind != "calc"))
                if (seen.Add(f.Id)) list.Add((f, i == chain.Count - 1 ? "" : chain[i].Name));
        return list;
    }

    private void ShowPhotos(ModelDb db, string globalId, System.Text.StringBuilder sum)
    {
        var files = FilesOf(db, globalId);
        var photos = files.Where(f => f.file.Kind == "photo").ToList();
        var docs = files.Where(f => f.file.Kind == "document").ToList();
        _body.Children.Add(Header($"Photos ({photos.Count}) and documents ({docs.Count})"));
        var bar = new WrapPanel();
        var addPhoto = Btn("Add photo…");
        var addDoc = Btn("Add document…");
        addPhoto.ToolTip = $"Copy photos into {db.AssetsFolder}\\photos and link them to this element";
        addDoc.ToolTip = "Copy reports, drawings, letters ... into the assets folder and link them to this element";
        addPhoto.Click += (_, _) => AddFiles(db, globalId, "photo");
        addDoc.Click += (_, _) => AddFiles(db, globalId, "document");
        bar.Children.Add(addPhoto); bar.Children.Add(addDoc);
        _body.Children.Add(bar);
        if (photos.Count > 0)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
            foreach (var (f, via) in photos.Take(24))
            {
                var thumb = AssetImages.Thumbnail(db, f);
                var loc = db.ResolveAsset(f);
                var tile = new Border
                {
                    Width = 104, Height = 84, Margin = new Thickness(0, 0, 4, 4), Background = Card, CornerRadius = new CornerRadius(3), Cursor = Cursors.Hand,
                    BorderBrush = loc.IsFile && !loc.Exists ? Bad : Brushes.Transparent, BorderThickness = new Thickness(1),
                    ToolTip = $"{(f.Caption.Length > 0 ? f.Caption + "\n" : "")}{f.FileName}{(f.TakenAt.Length > 0 ? "\ntaken " + f.TakenAt : "")}\nadded {f.AddedAt} by {f.AddedBy}"
                              + (via.Length > 0 ? $"\nlinked to {via}" : "") + (loc.IsFile && !loc.Exists ? "\nFILE MISSING: " + loc.LocalPath : "") + "\nClick: full size",
                };
                tile.Child = thumb != null
                    ? new Image { Source = thumb, Stretch = Stretch.Uniform }
                    : new TextBlock { Text = f.FileName, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4), Foreground = Muted };
                tile.MouseLeftButtonUp += (_, _) => AssetImages.Open(Window.GetWindow(this)!, db, f);
                wrap.Children.Add(tile);
            }
            _body.Children.Add(wrap);
            if (photos.Count > 24) _body.Children.Add(Small($"… {photos.Count - 24} more (sbm asset <model> photo list)", Muted));
        }
        foreach (var (f, via) in docs)
        {
            var tb = new TextBlock { FontSize = 11, Foreground = Link, Cursor = Cursors.Hand, TextDecorations = TextDecorations.Underline, TextWrapping = TextWrapping.Wrap,
                Text = f.FileName + (f.Caption.Length > 0 ? " - " + f.Caption : ""), ToolTip = $"{f.MediaType}, {(f.SizeBytes ?? 0) / 1024.0:N0} KB, added {f.AddedAt}" + (via.Length > 0 ? $", linked to {via}" : "") };
            tb.MouseLeftButtonUp += (_, _) => AssetImages.Open(Window.GetWindow(this)!, db, f);
            _body.Children.Add(tb);
        }
        if (photos.Count + docs.Count == 0) _body.Children.Add(Small("No photos or documents linked to this element or its parents.", Muted));
        sum.Append($" | photos {photos.Count}, documents {docs.Count}");
    }

    private void AddFiles(ModelDb db, string globalId, string kind)
    {
        var owner = Window.GetWindow(this)!;
        var dlg = new OpenFileDialog
        {
            Multiselect = true, Title = kind == "photo" ? "Add photos" : "Add documents",
            Filter = kind == "photo" ? "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.gif;*.webp;*.heic|All files|*.*" : "Documents|*.pdf;*.docx;*.doc;*.xlsx;*.xls;*.csv;*.txt;*.dwg;*.dxf|All files|*.*",
        };
        if (dlg.ShowDialog(owner) != true) return;
        var form = new FormDialog(kind == "photo" ? "Add photos" : "Add documents", $"{dlg.FileNames.Length} file(s) for {db.DescribeElement(globalId)}. They are copied into {db.AssetsFolder}.")
            .Text("Caption").Text("Author").Text(kind == "photo" ? "Taken (date)" : "Dated");
        form.Owner = owner;
        if (form.ShowDialog() != true) return;
        string dateLabel = kind == "photo" ? "Taken (date)" : "Dated";
        try
        {
            string? taken = form.Get(dateLabel) is { Length: > 0 } t ? ModelDb.NormDate(t) : null;
            foreach (var file in dlg.FileNames)
            {
                // photos: the app makes the thumbnail with WPF (no python needed); PDFs get one from PyMuPDF when available
                var f = db.AddFile(kind, file, new ModelDb.AddFileOptions
                {
                    Caption = form.Get("Caption"), Author = form.Get("Author"), TakenAt = taken, Thumbnail = kind != "photo",
                    Log = m => ViewModels.MainViewModel.Log("DB: " + m),
                });
                db.Link(f.Id, "element", globalId);
                ViewModels.MainViewModel.Log($"DB: added {kind} #{f.Id} {f.FileName} -> {f.Location} for {globalId}");
            }
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Add " + kind, MessageBoxButton.OK, MessageBoxImage.Warning); }
        Refresh(db, globalId);
    }

    // ------------------------------------------------------------------ inspection

    private void ShowInspection(ModelDb db, string globalId, System.Text.StringBuilder sum)
    {
        var cond = db.ConditionFor(globalId);
        var ratings = db.RatingsFor(globalId);
        var findings = db.Findings(globalId, withDescendants: true);
        _body.Children.Add(Header("Inspection"));
        if (cond.Count == 0 && ratings.Count == 0)
            _body.Children.Add(Small(db.Inspections().Count == 0
                ? "No inspections in the database (sbm asset <model> inspection import <file.csv>)."
                : "No element-level data or ratings for this element or its parents.", Muted));
        if (ratings.Count > 0)
        {
            string last = ratings[0].InspectionDate; // newest first
            var latest = ratings.Where(r => r.InspectionDate == last)
                .OrderBy(r => Array.IndexOf(ModelDb.Components, r.Component) is var i && i >= 0 ? i : 99).ThenBy(r => r.Component).ToList();
            var prev = ratings.Where(r => string.CompareOrdinal(r.InspectionDate, last) < 0).ToList();
            var wp = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
            wp.Children.Add(Small($"Ratings {last}: ", Muted, false));
            foreach (var r in latest)
            {
                string? before = prev.Where(p => p.Component == r.Component && p.GlobalId == r.GlobalId).OrderByDescending(p => p.InspectionDate).FirstOrDefault()?.Rating;
                var b = new Border { Background = Card, CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 4, 3),
                    ToolTip = (r.Note.Length > 0 ? r.Note + "\n" : "") + (before != null ? $"previous: {before}" : "") };
                bool worse = before != null && int.TryParse(before, out int bv) && int.TryParse(r.Rating, out int rv) && rv < bv;
                b.Child = Small($"{Cap(r.Component)} {r.Rating}{(worse ? " ▼" : "")}", worse ? Warn : Brushes.Gainsboro, false);
                wp.Children.Add(b);
            }
            _body.Children.Add(wp);
            sum.Append(" | ratings " + string.Join(" ", latest.Select(r => $"{r.Component}={r.Rating}")));
        }
        if (cond.Count > 0)
        {
            string last = cond[0].InspectionDate;
            var rows = cond.Where(c => c.InspectionDate == last).ToList();
            _body.Children.Add(Small($"Element-level {last} ({cond[0].InspectionKind})" + (cond[0].Via.Length > 0 ? $", recorded on {cond[0].Via}" : ""), Muted));
            var g = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            foreach (var w in new[] { 1.0, 0.0, 0, 0, 0, 0 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = w > 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            void Cell(int row, int col, string text, Brush? fg = null, bool bold = false)
            {
                var t = Small(text, fg, false);
                t.Margin = new Thickness(col == 0 ? 0 : 8, 0, 0, 0);
                t.TextAlignment = col == 0 ? TextAlignment.Left : TextAlignment.Right;
                if (bold) t.FontWeight = FontWeights.SemiBold;
                if (col == 0) t.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetRow(t, row); Grid.SetColumn(t, col);
                g.Children.Add(t);
            }
            g.RowDefinitions.Add(new RowDefinition());
            string[] heads = { "Element", "Total", "CS1", "CS2", "CS3", "CS4" };
            for (int c = 0; c < heads.Length; c++) Cell(0, c, heads[c], Muted);
            foreach (var r in rows)
            {
                int ri = g.RowDefinitions.Count;
                g.RowDefinitions.Add(new RowDefinition());
                bool defect = r.DefectNumber.Length > 0;
                string label = defect ? $"   {r.DefectNumber} {r.DefectName}" : $"{(r.ParentElementNumber.Length > 0 ? "  " : "")}{r.ElementNumber} {r.ElementName}";
                Cell(ri, 0, label, defect ? Muted : null, !defect && r.ParentElementNumber.Length == 0);
                Cell(ri, 1, N(r.TotalQty) + (r.Unit.Length > 0 ? " " + r.Unit : ""), defect ? Muted : null);
                Cell(ri, 2, N(r.Cs1), defect ? Muted : null);
                Cell(ri, 3, N(r.Cs2), r.Cs2 > 0 ? new SolidColorBrush(Color.FromRgb(0xE0, 0xD0, 0x60)) : defect ? Muted : null);
                Cell(ri, 4, N(r.Cs3), r.Cs3 > 0 ? Warn : defect ? Muted : null);
                Cell(ri, 5, N(r.Cs4), r.Cs4 > 0 ? Bad : defect ? Muted : null);
                if (r.Note.Length > 0) ((FrameworkElement)g.Children[^6]).ToolTip = r.Note;
            }
            _body.Children.Add(g);
            // history of the element totals (older inspections)
            foreach (var grp in cond.Where(c => c.InspectionDate != last && c.DefectNumber.Length == 0).GroupBy(c => c.InspectionDate).Take(6))
                _body.Children.Add(Small($"{grp.Key}: " + string.Join("; ", grp.Select(c => $"{c.ElementNumber} CS1-4 {N(c.Cs1)}/{N(c.Cs2)}/{N(c.Cs3)}/{N(c.Cs4)} {c.Unit}")), Muted));
            sum.Append($" | element-level {last}: " + string.Join(" ", rows.Where(r => r.DefectNumber.Length == 0).Select(r => $"{r.ElementNumber} {N(r.Cs1)}/{N(r.Cs2)}/{N(r.Cs3)}/{N(r.Cs4)}")));
        }

        var fbar = new DockPanel { Margin = new Thickness(0, 4, 0, 2) };
        var add = Btn("Add finding…");
        add.Click += (_, _) => AddFinding(db, globalId);
        DockPanel.SetDock(add, Dock.Right);
        fbar.Children.Add(add);
        fbar.Children.Add(Small($"Findings ({findings.Count(f => f.IsOpen)} open of {findings.Count}, incl. parts below)", Muted));
        _body.Children.Add(fbar);
        foreach (var f in findings.Take(20)) _body.Children.Add(FindingCard(db, globalId, f));
        sum.Append($" | findings {findings.Count} ({findings.Count(f => f.IsOpen)} open)");
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].Replace('_', ' ');

    private UIElement FindingCard(ModelDb db, string shownId, Finding f)
    {
        var card = new Border { Background = Card, Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(6, 3, 6, 4), CornerRadius = new CornerRadius(3),
            BorderBrush = SeverityBrush(f.Severity), BorderThickness = new Thickness(3, 0, 0, 0) };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = f.Description, FontWeight = FontWeights.SemiBold, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        string where = f.GlobalId != shownId ? (db.Get(f.GlobalId) is { } e ? e.Name : "(element not in the model)") + " · " : "";
        sp.Children.Add(Small($"{where}{f.FoundAt} · {f.Severity}" + (f.DefectType.Length > 0 ? $" · defect {f.DefectType}" : "")
                              + (f.LocationText.Length > 0 ? $" · {f.LocationText}" : "") + (f.Station is { } st ? $" · sta {st:F2} m" : ""), Muted));
        if (f.RecommendedAction.Length > 0) sp.Children.Add(Small("Action: " + f.RecommendedAction + (f.DueDate.Length > 0 ? $" (due {f.DueDate})" : "")));
        var row = new WrapPanel { Margin = new Thickness(0, 3, 0, 0) };
        var status = new ComboBox { MinWidth = 100, Margin = new Thickness(0, 0, 6, 0), ItemsSource = ModelDb.FindingStatuses, SelectedItem = f.Status };
        var note = new TextBox { Text = f.Note, MinWidth = 120, FontSize = 11, ToolTip = "Follow-up note", Margin = new Thickness(0, 0, 6, 0) };
        var save = Btn("Save");
        var saved = Small(f.UpdatedAt.Length > 0 ? "updated " + f.UpdatedAt : "", Muted, false);
        save.Click += (_, _) =>
        {
            try
            {
                db.UpdateFinding(f.Id, (string?)status.SelectedItem, note.Text);
                saved.Text = "saved " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { saved.Text = ex.Message; }
        };
        row.Children.Add(status); row.Children.Add(note); row.Children.Add(save); row.Children.Add(saved);
        sp.Children.Add(row);
        card.Child = sp;
        return card;
    }

    private void AddFinding(ModelDb db, string globalId)
    {
        var owner = Window.GetWindow(this)!;
        var pick = PickedPoint?.Invoke();
        var insp = db.Inspections();
        var form = new FormDialog("Add finding", $"Finding on {db.DescribeElement(globalId)}", 520)
            .Text("Description", required: true, multiline: true)
            .Text("Defect type")
            .Combo("Severity", ModelDb.Severities, "medium")
            .Text("Location")
            .Check("3D point", pick != null, pick != null, pick is { } p ? $"use the picked point E {p.x:F2}, N {p.y:F2}, Z {p.z:F2}" : "(pick a point on the element first)")
            .Text("Recommended action")
            .Combo("Status", ModelDb.FindingStatuses, "open")
            .Text("Due date")
            .Combo("Inspection", new[] { "(none)" }.Concat(insp.Select(i => $"#{i.Id} {i.Label}")), insp.Count > 0 ? $"#{insp[0].Id} {insp[0].Label}" : "(none)")
            .Files("Photos", "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.gif;*.webp|All files|*.*");
        form.Owner = owner;
        if (form.ShowDialog() != true) return;
        try
        {
            string ins = form.Get("Inspection");
            long? inspId = ins.StartsWith('#') && long.TryParse(ins[1..ins.IndexOf(' ')], out long iid) ? iid : null;
            bool usePt = form.IsChecked("3D point") && pick != null;
            var f = db.AddFinding(new Finding
            {
                GlobalId = globalId, InspectionId = inspId, Description = form.Get("Description"), DefectType = form.Get("Defect type"), Severity = form.Get("Severity"),
                LocationText = form.Get("Location"), X = usePt ? pick!.Value.x : null, Y = usePt ? pick!.Value.y : null, Z = usePt ? pick!.Value.z : null,
                RecommendedAction = form.Get("Recommended action"), Status = form.Get("Status"), DueDate = form.Get("Due date") is { Length: > 0 } d ? ModelDb.NormDate(d) : "",
            });
            foreach (var ph in form.GetFiles("Photos"))
            {
                var pf = db.AddFile("photo", ph, new ModelDb.AddFileOptions { Caption = f.Description, Thumbnail = false });
                db.Link(pf.Id, "finding", f.Id.ToString(CultureInfo.InvariantCulture));
                db.Link(pf.Id, "element", globalId);
            }
            ViewModels.MainViewModel.Log($"DB: finding #{f.Id} on {globalId}: {f.Description}");
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Add finding", MessageBoxButton.OK, MessageBoxImage.Warning); }
        Refresh(db, globalId);
    }

    // ------------------------------------------------------------------ calcs

    private void ShowCalcs(ModelDb db, string globalId, System.Text.StringBuilder sum)
    {
        var calcs = db.CalcsForElement(globalId);
        var head = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        var open = Btn("Calcs window…");
        open.Click += (_, _) => OpenCalcsRequested?.Invoke(0);
        DockPanel.SetDock(open, Dock.Right);
        head.Children.Add(open);
        head.Children.Add(Header($"Calcs ({calcs.Count})"));
        _body.Children.Add(head);
        if (calcs.Count == 0) { _body.Children.Add(Small(db.Calcs().Count == 0 ? "No calc packages in the database (Calcs window, or sbm asset <model> calc add)." : "No calc is linked to this element or its parents.", Muted)); return; }
        var chain = db.Chain(globalId);
        var ids = chain.Select(e => e.GlobalId).ToHashSet();
        var marks = chain.Select(e => e.Mark).Where(m => m.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in calcs)
        {
            var card = new Border { Background = Card, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(6, 4, 6, 4), CornerRadius = new CornerRadius(3) };
            var sp = new StackPanel();
            var title = new TextBlock { Text = c.Title + (c.Revision.Length > 0 ? " rev " + c.Revision : ""), FontWeight = FontWeights.SemiBold, FontSize = 11.5, Foreground = Link,
                Cursor = Cursors.Hand, TextDecorations = TextDecorations.Underline, ToolTip = "Open in the Calcs window (summary pages, diff, apply)" };
            long cid = c.Id;
            title.MouseLeftButtonUp += (_, _) => OpenCalcsRequested?.Invoke(cid);
            sp.Children.Add(title);
            sp.Children.Add(Small($"{db.GetFile(c.FileId)?.FileName} · summary p. {c.SummaryPages}" + (c.CalcDate.Length > 0 ? " · " + c.CalcDate : ""), Muted));
            var vals = db.CalcValues(c.Id).Where(v => ids.Contains(v.GlobalId) || marks.Contains(v.Mark)).ToList();
            if (vals.Count == 0) vals = db.CalcValues(c.Id).Take(8).ToList();
            foreach (var v in vals)
            {
                var r = CalcDiff?.Invoke(v, c.Title);
                var line = new DockPanel();
                var st = Small(r?.Status ?? "", r != null ? StatusBrush(r.Status) : Muted, false);
                DockPanel.SetDock(st, Dock.Right);
                st.Margin = new Thickness(6, 0, 0, 0);
                line.Children.Add(st);
                line.Children.Add(Small($"{v.Name}: model {(r?.ModelText is { Length: > 0 } mt ? mt : "-")} | calc {r?.CalcText ?? v.ValueText} (p.{v.Page})"));
                line.ToolTip = $"{v.TargetText}\n\"{v.Quote}\"" + (r?.Message is { Length: > 0 } msg ? "\n" + msg : "");
                sp.Children.Add(line);
                sum.Append($" | calc {c.Id} {v.Name} {r?.Status}");
            }
            card.Child = sp;
            _body.Children.Add(card);
        }
    }
}
