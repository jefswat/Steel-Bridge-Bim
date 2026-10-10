using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SBM.Core.Alignments;
using SBM.Core.Units;
using SBM.IO.PgSuper;

namespace SBM.App.Editors;

/// <summary>Places a PGSuper bridge on one of the project's alignments: alignment, begin station and name.</summary>
public sealed class PgSuperImportWindow : Window
{
    private readonly ComboBox _align = new() { MinWidth = 260 };
    private readonly TextBox _station = new() { MinWidth = 160 };
    private readonly TextBox _name = new() { MinWidth = 260 };
    private readonly UnitSystem _u;

    public Alignment? Alignment => _align.SelectedItem as Alignment;
    public double BeginStation { get; private set; }
    public string BridgeName => _name.Text.Trim();

    public PgSuperImportWindow(PgsBridge pg, IReadOnlyList<Alignment> alignments, UnitSystem u, string defaultName)
    {
        _u = u;
        Title = "Import PGSuper Bridge";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));

        _align.ItemsSource = alignments;
        _align.DisplayMemberPath = nameof(SBM.Core.Alignments.Alignment.Name);
        _align.SelectedIndex = 0;
        _name.Text = defaultName;
        double s0 = pg.Piers[0].Station;
        void Default()
        {
            if (Alignment is not { } a) return;
            _station.Text = u.FormatStation(s0 >= a.StartStation && s0 <= a.EndStation ? s0 : a.StartStation);
        }
        _align.SelectionChanged += (_, _) => Default();
        Default();

        var spans = string.Join(", ", Enumerable.Range(0, pg.Piers.Count - 1).Select(k => u.FormatLength(pg.Piers[k + 1].Station - pg.Piers[k].Station)));
        var info = new TextBlock
        {
            Text = $"{pg.GirderName} ({pg.SectionName}), {pg.GirderCount} girders, spans {spans}\n" +
                   $"PGSuper {pg.Version} · first pier at STA {u.FormatStation(s0)} on the PGSuper alignment",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)), Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap, MaxWidth = 460,
        };
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        void Row(int r, string label, FrameworkElement c)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 12, 4) };
            Grid.SetRow(t, r);
            Grid.SetRow(c, r);
            Grid.SetColumn(c, 1);
            c.Margin = new Thickness(0, 4, 0, 4);
            grid.Children.Add(t);
            grid.Children.Add(c);
        }
        Row(0, "Alignment", _align);
        Row(1, "Station of the first PGSuper pier line", _station);
        Row(2, "Bridge name", _name);

        var ok = new Button { Content = "Import", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };
        ok.Click += (_, _) =>
        {
            if (Alignment == null) { MessageBox.Show(this, "Choose an alignment."); return; }
            if (!_u.TryParseStation(_station.Text, out double sta)) { MessageBox.Show(this, $"Can't read the station '{_station.Text}'."); return; }
            BeginStation = sta;
            DialogResult = true;
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var note = new TextBlock
        {
            Text = "Spans, skews, girder spacing, deck, haunch (\"A\" − slab), girder section, strands and bearings come from PGSuper;\n" +
                   "geometry (alignment, profile, cross slope) comes from this project's alignment.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)), FontSize = 11.5, Margin = new Thickness(0, 0, 0, 12),
        };
        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(info);
        root.Children.Add(grid);
        root.Children.Add(note);
        root.Children.Add(buttons);
        Content = root;
    }
}
