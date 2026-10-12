using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SBM.Core.Project;

namespace SBM.App.Editors;

/// <summary>
/// Open dialog with the recent models (newest first: file name, folder, last opened). Click a row to open it; missing files are greyed
/// with Remove; "Clear list"; "Browse…" = the normal file dialog. The list itself is <see cref="RecentModelList"/> (changes are saved by
/// the <paramref name="save"/> callback).
/// </summary>
public sealed class RecentModelsWindow : Window
{
    private readonly RecentModelList _list;
    private readonly Action _save;
    private readonly StackPanel _rows = new();
    private readonly Button _clear = new() { Content = "Clear list", MinWidth = 90 };

    /// <summary>The model chosen from the list (null = Browse or Cancel).</summary>
    public string? SelectedPath { get; private set; }
    /// <summary>The user asked for the file dialog.</summary>
    public bool Browse { get; private set; }

    private static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly Brush RowBg = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
    private static readonly Brush RowHover = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x41));

    public RecentModelsWindow(RecentModelList list, Action save)
    {
        _list = list;
        _save = save;
        Title = "Open model";
        Width = 640;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 720;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));

        var head = new TextBlock { Text = "Recent models", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        var scroll = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 560 };

        var browse = new Button { Content = "Browse…", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };
        browse.Click += (_, _) => { Browse = true; DialogResult = true; };
        _clear.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Remove every model from the recent list? (The files are not touched.)", "Recent models", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            _list.Clear();
            _save();
            Fill();
        };
        var buttons = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        DockPanel.SetDock(_clear, Dock.Left);
        buttons.Children.Add(_clear);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(browse);
        right.Children.Add(cancel);
        DockPanel.SetDock(right, Dock.Right);
        buttons.Children.Add(right);

        var root = new StackPanel();
        root.Children.Add(head);
        root.Children.Add(scroll);
        root.Children.Add(buttons);
        Content = new Border { Padding = new Thickness(16), Child = root, Background = Background };
        Fill();
    }

    private void Fill()
    {
        _rows.Children.Clear();
        var now = DateTime.UtcNow;
        var items = _list.WithStatus(File.Exists);
        _clear.IsEnabled = items.Count > 0;
        if (items.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "No recent models yet: models you open or save appear here.", Foreground = Dim, Margin = new Thickness(0, 6, 0, 6) });
            return;
        }
        foreach (var (item, exists) in items)
        {
            var name = new TextBlock { Text = item.FileName, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            var folder = new TextBlock { Text = exists ? item.Folder : item.Folder + "  (file not found)", Foreground = Dim, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
            var when = new TextBlock { Text = RecentModelList.Describe(item.LastOpenedUtc, now), Foreground = Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) };
            var text = new StackPanel();
            text.Children.Add(name);
            text.Children.Add(folder);
            var remove = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Remove from the list (the file is not touched)" };
            string path = item.Path;
            remove.Click += (_, e) => { e.Handled = true; _list.Remove(path); _save(); Fill(); };
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(remove, Dock.Right);
            DockPanel.SetDock(when, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(when);
            row.Children.Add(text);
            var border = new Border
            {
                Child = row, Background = RowBg, CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 6, 6, 6), Margin = new Thickness(0, 0, 0, 4),
                ToolTip = path, Cursor = exists ? Cursors.Hand : Cursors.Arrow,
            };
            if (exists)
            {
                // Present files: Remove only on hover, click opens.
                remove.Visibility = Visibility.Hidden;
                border.MouseEnter += (_, _) => { border.Background = RowHover; remove.Visibility = Visibility.Visible; };
                border.MouseLeave += (_, _) => { border.Background = RowBg; remove.Visibility = Visibility.Hidden; };
                border.MouseLeftButtonUp += (_, e) =>
                {
                    if (e.OriginalSource is DependencyObject d && IsInside(remove, d)) return;
                    SelectedPath = path;
                    DialogResult = true;
                };
            }
            else
            {
                name.Opacity = 0.5;
                when.Opacity = 0.5;
            }
            _rows.Children.Add(border);
        }
    }

    private static bool IsInside(DependencyObject parent, DependencyObject d)
    {
        for (var x = d; x != null; x = x is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(x) : LogicalTreeHelper.GetParent(x))
            if (x == parent) return true;
        return false;
    }
}
