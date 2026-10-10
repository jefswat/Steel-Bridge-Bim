using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SBM.Core.Checks;
using SBM.Core.Framing;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.App.Editors;

public sealed record BearingCheckRow(ElevationChain Chain, string Support, string Girder, string Station, string Deck, string Seat, string Status);
public sealed record StepRow(string Item, string Change, string Elevation, string Result, string Basis);
public sealed record CheckLine(string Icon, Brush Color, string Text);

/// <summary>
/// Transparent elevation calculations: for each bearing, profile grade → cross slope → deck → haunch → web → flange →
/// sole plate → bearing → seat, every line with its inputs and formula, reconciled against the 3D model.
/// </summary>
public partial class ElevationCheckWindow : Window
{
    private static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x5F, 0xD0, 0x7A));
    private static readonly Brush Bad = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
    private readonly UnitSystem _u;
    private readonly BridgeFraming _f;
    private readonly List<ElevationChain> _chains;
    private ElevationChain? _shown;

    public ElevationCheckWindow(BridgeFraming framing, SteelModel? steel, UnitSystem units, SBM.Core.Substructure.SubstructureModel? sub = null,
        SBM.Core.Prestressed.PrestressedModel? prestressed = null)
    {
        InitializeComponent();
        _u = units;
        _f = framing;
        Title = $"Elevation Check — {framing.Definition.Name}";
        _chains = ElevationCheck.AllBearings(framing, steel, units, sub, prestressed);
        BearingGrid.ItemsSource = _chains.Select(c =>
        {
            var parts = c.Title.Split(" — ");
            return new BearingCheckRow(c, parts[0], parts.Length > 1 ? parts[1].Split(' ')[0] : "", units.FormatStation(c.Station),
                units.FormatLength(c.Steps[1].Elevation, LengthKind.Coordinate),
                units.FormatLength(c.Steps.LastOrDefault(s => s.Result.StartsWith("Bottom of bearing"))?.Elevation ?? c.Final, LengthKind.Coordinate),
                c.Mismatch ? "✗ differs" : "✓ matches");
        }).ToList();
        int bad = _chains.Count(c => c.Mismatch);
        Summary.Text = $"{_chains.Count} bearings checked — " + (bad == 0
            ? "every hand calculation matches the generated 3D model."
            : $"{bad} do not match the 3D model (see the red lines).");
        GirderBox.ItemsSource = new[] { "(finished grade only)" }.Concat(framing.Girders.Select(g => g.Name)).ToList();
        GirderBox.SelectedIndex = 0;
        if (_chains.Count > 0)
        {
            StaBox.Text = units.FormatStation(_chains[0].Station);
            OffBox.Text = units.FormatLength(0);
            BearingGrid.SelectedIndex = 0;
        }
    }

    private void OnBearingSelected(object sender, SelectionChangedEventArgs e)
    {
        if (BearingGrid.SelectedItem is BearingCheckRow r) Show(r.Chain);
    }

    private void Show(ElevationChain c)
    {
        _shown = c;
        ChainTitle.Text = c.Title;
        ChainLocation.Text = $"STA {_u.FormatStation(c.Station)}   ·   offset {_u.FormatLength(c.Offset)} {(c.Offset < 0 ? "LT" : "RT")}   ·   " +
                             "each line = previous elevation + change; (info) lines are not added";
        StepGrid.ItemsSource = c.Steps.Select(s => new StepRow(s.Item, FormatChange(s.Change), _u.FormatLength(s.Elevation, LengthKind.Coordinate),
            s.Result, s.Basis)).ToList();
        ChecksList.ItemsSource = c.Checks.Select(t =>
        {
            bool bad = t.Contains("differs");
            return new CheckLine(bad ? "" : "", bad ? Bad : Ok, t);
        }).ToList();
    }

    private string FormatChange(double d) => double.IsNaN(d) ? "" :
        (d >= 0 ? "+" : "−") + _u.FormatLength(Math.Abs(d), Math.Abs(d) < 0.3 ? LengthKind.Short : LengthKind.Long);

    private void OnCalculate(object sender, RoutedEventArgs e)
    {
        CalcError.Text = "";
        if (!_u.TryParseStation(StaBox.Text, out double sta)) { CalcError.Text = "Enter a station, e.g. 21+50.00"; return; }
        try
        {
            ElevationChain c;
            if (GirderBox.SelectedIndex > 0) c = ElevationCheck.GirderAt(_f, GirderBox.SelectedIndex - 1, sta, _u);
            else
            {
                if (!_u.TryParseLength(OffBox.Text, LengthKind.Long, out double off)) { CalcError.Text = "Enter an offset (right positive)"; return; }
                c = ElevationCheck.Grade(_f.Alignment, sta, off, _u, $"Finished grade at STA {_u.FormatStation(sta)}, offset {_u.FormatLength(off)}");
            }
            BearingGrid.SelectedIndex = -1;
            Show(c);
        }
        catch (Exception ex) { CalcError.Text = ex.Message; }
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_shown != null) Clipboard.SetText(ElevationCheck.ToText(new[] { _shown }, _u));
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Text report (*.txt)|*.txt|CSV, one row per step (*.csv)|*.csv",
            FileName = $"{_f.Definition.Name} Elevation Check",
        };
        if (dlg.ShowDialog() != true) return;
        if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder("Location,Station,Offset,Step,Change,Elevation,Result,Basis\n");
            foreach (var c in _chains)
                foreach (var s in c.Steps)
                    sb.Append(string.Join(",", Q(c.Title), Q(_u.FormatStation(c.Station)), Q(_u.FormatLength(c.Offset)), Q(s.Item),
                        Q(FormatChange(s.Change)), Q(_u.FormatLength(s.Elevation, LengthKind.Coordinate)), Q(s.Result), Q(s.Basis))).Append('\n');
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }
        else File.WriteAllText(dlg.FileName, ElevationCheck.ToText(_chains, _u), Encoding.UTF8);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
