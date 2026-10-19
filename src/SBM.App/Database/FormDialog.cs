using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace SBM.App.Database;

/// <summary>Small modal form built in code (dark theme): labeled text boxes, combo boxes, check boxes and file pickers. Used for Add finding,
/// Add calc and photo captions.</summary>
public sealed class FormDialog : Window
{
    private readonly Grid _grid = new() { Margin = new Thickness(12, 10, 12, 4) };
    private readonly Dictionary<string, Func<object?>> _values = new();
    private readonly HashSet<string> _required = new();
    private readonly TextBlock _error = new() { Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)), Margin = new Thickness(12, 0, 12, 0), TextWrapping = TextWrapping.Wrap };

    public FormDialog(string title, string? intro = null, double width = 480)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (_, _) =>
        {
            var missing = _required.Where(k => _values[k]() is null or "").ToList();
            if (missing.Count > 0) { _error.Text = "Required: " + string.Join(", ", missing); return; }
            DialogResult = true;
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 8, 12, 12) };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var root = new StackPanel();
        if (intro != null) root.Children.Add(new TextBlock { Text = intro, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 10, 12, 0), Foreground = Brushes.Gainsboro });
        root.Children.Add(_grid);
        root.Children.Add(_error);
        root.Children.Add(buttons);
        Content = root;
    }

    private void AddRow(string label, FrameworkElement control, bool required)
    {
        int r = _grid.RowDefinitions.Count;
        _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var l = new TextBlock { Text = label + (required ? " *" : ""), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3), Foreground = Brushes.Gainsboro };
        control.Margin = new Thickness(0, 3, 0, 3);
        Grid.SetRow(l, r); Grid.SetRow(control, r); Grid.SetColumn(control, 1);
        _grid.Children.Add(l); _grid.Children.Add(control);
        if (required) _required.Add(label);
    }

    public FormDialog Text(string label, string value = "", bool required = false, bool multiline = false)
    {
        var tb = new TextBox { Text = value, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 54 : 0 };
        AddRow(label, tb, required);
        _values[label] = () => tb.Text.Trim();
        return this;
    }

    public FormDialog Combo(string label, IEnumerable<string> items, string? selected = null, bool editable = false)
    {
        var list = items.ToList();
        var cb = new ComboBox { ItemsSource = list, IsEditable = editable, SelectedItem = selected ?? list.FirstOrDefault(), Text = selected ?? "" };
        AddRow(label, cb, false);
        _values[label] = () => editable ? cb.Text : cb.SelectedItem as string;
        return this;
    }

    public FormDialog Check(string label, bool value, bool enabled = true, string? text = null)
    {
        var c = new CheckBox { IsChecked = value, IsEnabled = enabled, Content = text };
        AddRow(label, c, false);
        _values[label] = () => c.IsChecked == true;
        return this;
    }

    public FormDialog Files(string label, string filter, bool multi = true, bool required = false)
    {
        var tb = new TextBox { IsReadOnly = true };
        var browse = new Button { Content = "Browse…", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 1, 8, 1) };
        var files = new List<string>();
        browse.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog { Filter = filter, Multiselect = multi };
            if (dlg.ShowDialog(this) != true) return;
            files.Clear(); files.AddRange(dlg.FileNames);
            tb.Text = string.Join("; ", files.Select(System.IO.Path.GetFileName));
        };
        var dp = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        dp.Children.Add(browse); dp.Children.Add(tb);
        AddRow(label, dp, required);
        _values[label] = () => files.Count == 0 ? null : files.ToList();
        return this;
    }

    public string Get(string label) => _values[label]() as string ?? "";
    public bool IsChecked(string label) => _values[label]() is true;
    public List<string> GetFiles(string label) => _values[label]() as List<string> ?? new List<string>();
}
