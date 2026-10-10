using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SBM.App;

/// <summary>A ribbon command found by the command search.</summary>
public sealed record CommandEntry(string Name, string Where, string Description, Button Source);

/// <summary>Command search (Ctrl+K): every ribbon button, found by name, tab, group or tooltip text.</summary>
public partial class MainWindow
{
    private List<CommandEntry>? _commands;

    private List<CommandEntry> Commands => _commands ??= CollectCommands();

    private List<CommandEntry> CollectCommands()
    {
        var list = new List<CommandEntry>();
        foreach (TabItem tab in Ribbon.Items)
            Walk(tab.Content, tab.Header?.ToString() ?? "", "");
        return list.GroupBy(c => c.Name + c.Description).Select(g => g.First()).ToList();

        void Walk(object? node, string tab, string group)
        {
            if (node is HeaderedContentControl hc) group = hc.Header?.ToString() ?? group;
            if (node is Button b)
            {
                string name = string.Join(" ", LogicalDescendants(b).OfType<TextBlock>().Where(t => t.FontFamily != (FindResource("IconFont") as System.Windows.Media.FontFamily))
                    .Select(t => t.Text)).Trim();
                if (name.Length > 0)
                    list.Add(new CommandEntry(name, $"  ·  {tab} ▸ {group}", b.ToolTip?.ToString() ?? "", b));
                return;
            }
            if (node is DependencyObject d)
                foreach (var c in LogicalTreeHelper.GetChildren(d)) Walk(c, tab, group);
        }
    }

    private static IEnumerable<object> LogicalDescendants(DependencyObject d)
    {
        foreach (var c in LogicalTreeHelper.GetChildren(d))
        {
            yield return c;
            if (c is DependencyObject cd)
                foreach (var g in LogicalDescendants(cd)) yield return g;
        }
    }

    private void OnFindCommand(object sender, ExecutedRoutedEventArgs e) => FocusCommandSearch();

    private void FocusCommandSearch()
    {
        CommandSearch.Focus();
        CommandSearch.SelectAll();
    }

    private void OnCommandSearchFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommandSearchHint.Visibility = Visibility.Collapsed;
        UpdateCommandList();
    }

    private void OnCommandSearchLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is DependencyObject d && (IsDescendant(CommandList, d))) return;
        CommandPopup.IsOpen = false;
        CommandSearchHint.Visibility = CommandSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsDescendant(DependencyObject root, DependencyObject d)
    {
        for (var p = d; p != null; p = System.Windows.Media.VisualTreeHelper.GetParent(p) ?? LogicalTreeHelper.GetParent(p))
            if (p == root) return true;
        return false;
    }

    private void OnCommandSearchChanged(object sender, TextChangedEventArgs e) => UpdateCommandList();

    private void UpdateCommandList()
    {
        string[] words = CommandSearch.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = Commands
            .Select(c => (c, score: Score(c, words)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score).ThenBy(x => x.c.Name)
            .Select(x => x.c).ToList();
        CommandList.ItemsSource = hits;
        CommandList.SelectedIndex = hits.Count > 0 ? 0 : -1;
        CommandPopup.IsOpen = CommandSearch.IsKeyboardFocused && hits.Count > 0;
    }

    private static int Score(CommandEntry c, string[] words)
    {
        if (words.Length == 0) return 1;
        int score = 0;
        foreach (var w in words)
        {
            if (c.Name.StartsWith(w, StringComparison.OrdinalIgnoreCase)) score += 10;
            else if (c.Name.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 6;
            else if (c.Where.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 3;
            else if (c.Description.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 2;
            else return 0;
        }
        return score;
    }

    private void OnCommandSearchKey(object sender, KeyEventArgs e)
    {
        int n = CommandList.Items.Count;
        switch (e.Key)
        {
            case Key.Down when n > 0:
                CommandList.SelectedIndex = (CommandList.SelectedIndex + 1) % n;
                CommandList.ScrollIntoView(CommandList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up when n > 0:
                CommandList.SelectedIndex = (CommandList.SelectedIndex - 1 + n) % n;
                CommandList.ScrollIntoView(CommandList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter:
                if (CommandList.SelectedItem is CommandEntry c) Run(c);
                e.Handled = true;
                break;
            case Key.Escape:
                CommandSearch.Text = "";
                CommandPopup.IsOpen = false;
                View.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnCommandListClick(object sender, MouseButtonEventArgs e)
    {
        if (CommandList.SelectedItem is CommandEntry c) Run(c);
    }

    private void Run(CommandEntry c)
    {
        CommandPopup.IsOpen = false;
        CommandSearch.Text = "";
        // Show the tab the command lives on, so the user learns where it is.
        foreach (TabItem tab in Ribbon.Items)
            if (IsDescendant(tab, c.Source) || LogicalDescendants(tab).Contains(c.Source)) { Ribbon.SelectedItem = tab; break; }
        View.Focus();
        var b = c.Source;
        if (b.Command is { } cmd)
        {
            if (cmd.CanExecute(b.CommandParameter)) cmd.Execute(b.CommandParameter);
        }
        else b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    }

    /// <summary>Review ▸ Reports buttons: bring the bottom panel tab forward.</summary>
    private void OnShowReport(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string t } && int.TryParse(t, out int i)) ReportTabs.SelectedIndex = i;
    }
}
