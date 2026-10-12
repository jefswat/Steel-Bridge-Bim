using System.IO;
using System.Windows;
using System.Windows.Controls;
using SBM.Core.Project;

namespace SBM.App;

/// <summary>Home ▸ Project ▸ Recent: the recent-models menu (the Open button / Ctrl+O shows the same list as a dialog, Editors/RecentModelsWindow).</summary>
public partial class MainWindow
{
    private void OnRecentMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = RecentButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var now = DateTime.UtcNow;
        var items = _vm.Recent.WithStatus(File.Exists);
        if (items.Count == 0) menu.Items.Add(new MenuItem { Header = "No recent models yet", IsEnabled = false });
        int n = 0;
        foreach (var (item, exists) in items)
        {
            var head = new StackPanel();
            head.Children.Add(new TextBlock { Text = $"{++n}  {item.FileName}", FontWeight = FontWeights.SemiBold });
            head.Children.Add(new TextBlock
            {
                Text = $"{item.Folder}{(exists ? "" : "  (file not found)")}  ·  {RecentModelList.Describe(item.LastOpenedUtc, now)}",
                FontSize = 11, Opacity = 0.7, MaxWidth = 560, TextTrimming = TextTrimming.CharacterEllipsis,
            });
            string path = item.Path;
            var mi = new MenuItem { Header = head, ToolTip = path };
            if (exists) mi.Click += (_, _) => _vm.OpenRecent(path);
            else
            {
                // Missing file: greyed, the submenu removes it.
                head.Opacity = 0.5;
                var remove = new MenuItem { Header = "Remove from the list" };
                remove.Click += (_, _) => { _vm.Recent.Remove(path); _vm.SaveRecent(); };
                mi.Items.Add(remove);
            }
            menu.Items.Add(mi);
        }
        menu.Items.Add(new Separator());
        var open = new MenuItem { Header = "Open…", InputGestureText = "Ctrl+O" };
        open.Click += (_, _) => _vm.OpenCommand.Execute(null);
        menu.Items.Add(open);
        var missing = new MenuItem { Header = "Remove missing files", IsEnabled = items.Any(i => !i.exists) };
        missing.Click += (_, _) => { _vm.Recent.RemoveMissing(File.Exists); _vm.SaveRecent(); };
        menu.Items.Add(missing);
        var clear = new MenuItem { Header = "Clear list", IsEnabled = items.Count > 0 };
        clear.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Remove every model from the recent list? (The files are not touched.)", "Recent models", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            _vm.Recent.Clear();
            _vm.SaveRecent();
        };
        menu.Items.Add(clear);
        menu.IsOpen = true;
    }

    /// <summary>Snapshot option <c>--recentdialog</c>: renders the Open dialog (recent models) to <c>&lt;stem&gt;_recent.png</c> from a copy of
    /// the list plus a missing file (the saved list is not changed).</summary>
    private void SnapshotRecentDialog(string path)
    {
        if (!Environment.GetCommandLineArgs().Contains("--recentdialog")) return;
        var copy = new RecentModelList { Items = _vm.Recent.Items.Select(i => new RecentModel { Path = i.Path, LastOpenedUtc = i.LastOpenedUtc }).ToList() };
        if (_vm.FilePath is { } fp) copy.Touch(fp, DateTime.UtcNow.AddMinutes(-5));
        copy.Touch(Path.Combine(Path.GetTempPath(), "Moved away", "Missing model.sbm"), DateTime.UtcNow.AddDays(-3));
        var w = new Editors.RecentModelsWindow(copy, () => { }) { Owner = this, ShowActivated = false };
        w.Show();
        w.UpdateLayout();
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        SaveElement((FrameworkElement)w.Content, Path.ChangeExtension(path, null) + "_recent.png");
        ViewModels.MainViewModel.Log($"RECENT: {copy.Items.Count} entries: {string.Join(" | ", copy.Items.Select(i => i.FileName + (File.Exists(i.Path) ? "" : " (missing)")))}");
        w.Close();
    }
}
