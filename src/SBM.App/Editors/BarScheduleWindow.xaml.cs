using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SBM.Core.Concrete;
using SBM.Core.Substructure;
using SBM.Core.Units;

namespace SBM.App.Editors;

public sealed record BarRow(BarMark Source, string Unit, string Mark, string Size, int Count, string Length, string Shape, string A, string B, string C,
    string Weight, string Coating, string Location);

/// <summary>Bar mark list (superstructure and each substructure unit) with bending diagrams; export to CSV.</summary>
public partial class BarScheduleWindow : Window
{
    public const string Superstructure = "Superstructure";
    private const string All = "All units";
    private readonly UnitSystem _u;
    private readonly List<BarRow> _rows;
    private readonly string _bridge;
    private readonly ConcreteModel? _deck;
    private readonly SubstructureModel? _sub;

    public BarScheduleWindow(string bridgeName, ConcreteModel? deck, SubstructureModel? sub, UnitSystem units, SBM.Core.Prestressed.PrestressedModel? beams = null)
    {
        InitializeComponent();
        _u = units;
        _bridge = bridgeName;
        _deck = deck;
        _sub = sub;
        Title = $"Bar Schedule — {bridgeName}";
        Diagram.Units = units;
        double wUnit = units.IsUS ? 2.20462 : 1;
        string wName = units.IsUS ? "lb" : "kg";
        string Dim(BarMark m, string letter) =>
            m.Shape.Code == "STR" ? "" : m.Shape.Dimensions.FirstOrDefault(d => d.Letter == letter) is { } d ? units.FormatLength(d.Value, LengthKind.Short) : "";
        var marks = (deck?.Marks ?? new List<BarMark>()).Concat(sub?.Marks ?? new List<BarMark>()).Concat(beams?.BarMarks ?? new List<BarMark>());
        _rows = marks.Select(m => new BarRow(m, string.IsNullOrEmpty(m.Unit) ? Superstructure : m.Unit, m.Mark, m.Size.Name, m.Count,
            m.Varies ? $"{units.FormatLength(m.MinLength)} – {units.FormatLength(m.Length)}" : units.FormatLength(m.Length),
            m.Shape.Code switch { "STR" => "Straight", "TIE" => "Tie / stirrup", "SPIRAL" => "Spiral", "BENT" => "Bent", _ => m.Shape.Code },
            Dim(m, "A"), Dim(m, "B"), m.Shape.Code == "SPIRAL" ? Dim(m, "D") : Dim(m, "C"),
            $"{m.TotalMass * wUnit:N0} {wName}", m.Epoxy ? "Epoxy" : "Uncoated", m.Location)).ToList();
        UnitFilter.ItemsSource = new[] { All }.Concat(_rows.Select(r => r.Unit).Distinct()).ToList();
        UnitFilter.SelectedIndex = 0;
    }

    private void OnUnitChanged(object sender, SelectionChangedEventArgs e)
    {
        string unit = UnitFilter.SelectedItem as string ?? All;
        var rows = unit == All ? _rows : _rows.Where(r => r.Unit == unit).ToList();
        Grid.ItemsSource = rows;
        if (rows.Count > 0) Grid.SelectedIndex = 0;
        double wUnit = _u.IsUS ? 2.20462 : 1;
        string wName = _u.IsUS ? "lb" : "kg";
        var bySize = rows.GroupBy(r => r.Size).OrderBy(g => RebarCatalog.Sizes.ToList().FindIndex(s => s.Name == g.Key))
            .Select(g => $"{g.Key}: {g.Sum(r => r.Source.TotalMass) * wUnit:N0} {wName}");
        double cy = _u.IsUS ? 0.764554857984 : 1;
        string vu = _u.IsUS ? "CY" : "m³";
        string concrete = "";
        if (unit is All or Superstructure && _deck != null)
            concrete += $"deck {_deck.DeckVolume / cy:N1} {vu}, haunches {_deck.HaunchVolume / cy:N1} {vu}, barriers {_deck.BarrierVolume / cy:N1} {vu}";
        if (_sub != null && unit != Superstructure)
        {
            double v = _sub.Units.Where(x => unit == All || x.Name == unit).SelectMany(x => x.Solids).Where(s => s.IsConcrete).Sum(s => Math.Abs(s.Mesh.Volume()));
            if (v > 0) concrete += (concrete.Length > 0 ? ", " : "") + $"substructure {v / cy:N1} {vu}";
        }
        Totals.Text = $"Reinforcing {rows.Sum(r => r.Source.TotalMass) * wUnit:N0} {wName}  ({string.Join(",  ", bySize)})" +
                      (concrete.Length > 0 ? $"   ·   Concrete: {concrete}" : "");
    }

    private void OnSelected(object sender, SelectionChangedEventArgs e) => Diagram.Mark = (Grid.SelectedItem as BarRow)?.Source;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"{_bridge} Bar Schedule.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder("Unit,Mark,Size,No.,Length,Min Length,Shape,A,B,C/D,Pin Dia,Weight,Coating,Location\n");
        string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        foreach (var r in (IEnumerable<BarRow>)Grid.ItemsSource)
        {
            var m = r.Source;
            sb.Append(string.Join(",", Q(r.Unit), Q(r.Mark), r.Size, r.Count, Q(_u.FormatLength(m.Length)), Q(m.Varies ? _u.FormatLength(m.MinLength) : ""),
                r.Shape, Q(r.A), Q(r.B), Q(r.C), Q(m.Shape.PinDiameter > 0 ? _u.FormatLength(m.Shape.PinDiameter, LengthKind.Short) : ""),
                Q(r.Weight), r.Coating, Q(r.Location))).Append('\n');
        }
        File.WriteAllText(dlg.FileName, sb.ToString());
    }
}
