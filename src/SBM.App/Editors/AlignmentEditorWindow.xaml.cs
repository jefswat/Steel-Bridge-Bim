using System.Windows;
using System.Windows.Controls;
using SBM.Core.Alignments;
using SBM.Core.Units;

namespace SBM.App.Editors;

public partial class AlignmentEditorWindow : Window
{
    private readonly AlignmentEditorViewModel _vm;
    private readonly Action<Alignment> _apply;
    private Alignment? _last;

    private AlignmentEditorWindow(Alignment a, UnitSystem units, bool isNew, Action<Alignment> apply)
    {
        InitializeComponent();
        _vm = new AlignmentEditorViewModel(a, units);
        _apply = apply;
        DataContext = _vm;
        Title = isNew ? "New Alignment" : $"Alignment — {a.Name}";
        string lu = units.LongUnitName;

        GridColumns.Configure(PiGrid);
        GridColumns.AddCell(PiGrid, $"Northing ({lu})", nameof(PiRowVm.Northing), 150);
        GridColumns.AddCell(PiGrid, $"Easting ({lu})", nameof(PiRowVm.Easting), 150);
        GridColumns.AddCell(PiGrid, "Radius", nameof(PiRowVm.Radius), 130);
        GridColumns.AddCell(PiGrid, "Spiral In (Ls)", nameof(PiRowVm.SpiralIn), 130);
        GridColumns.AddCell(PiGrid, "Spiral Out (Ls)", nameof(PiRowVm.SpiralOut), 130);

        GridColumns.Configure(VpiGrid);
        GridColumns.AddCell(VpiGrid, "Station", nameof(VpiRowVm.Station), 140);
        GridColumns.AddCell(VpiGrid, $"Elevation ({lu})", nameof(VpiRowVm.Elevation), 140);
        GridColumns.AddCell(VpiGrid, "VC Length", nameof(VpiRowVm.CurveLength), 130);
        GridColumns.AddCell(VpiGrid, "L1 (unsym.)", nameof(VpiRowVm.LengthIn), 130);

        GridColumns.Configure(SlopeGrid);
        GridColumns.AddCell(SlopeGrid, "Station", nameof(SlopeRowVm.Station), 140);
        GridColumns.AddCell(SlopeGrid, "Left Slope", nameof(SlopeRowVm.Left), 130);
        GridColumns.AddCell(SlopeGrid, "Right Slope", nameof(SlopeRowVm.Right), 130);

        if (!_vm.PiEditable)
        {
            PiGrid.IsEnabled = false;
            PiToolbar.IsEnabled = false;
            CurveGrid.Visibility = Visibility.Collapsed;
            ElementGrid.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Non-modal instance with a no-op apply, for rendering previews/snapshots.</summary>
    public static AlignmentEditorWindow CreateForPreview(Alignment a, UnitSystem units) => new(a, units, false, _ => { });

    /// <summary>Opens the editor modally. <paramref name="apply"/> is called for every Apply/OK with a new alignment.</summary>
    public static Alignment? Edit(Alignment a, UnitSystem units, bool isNew, Action<Alignment> apply)
    {
        var w = new AlignmentEditorWindow(a, units, isNew, apply) { Owner = Application.Current.MainWindow };
        w.ShowDialog();
        return w._last;
    }

    private void CommitAll()
    {
        GridColumns.CommitEdits(PiGrid);
        GridColumns.CommitEdits(VpiGrid);
        GridColumns.CommitEdits(SlopeGrid);
    }

    private bool ApplyChanges()
    {
        CommitAll();
        if (!_vm.TryBuild(out var a) || a == null) return false;
        _last = a;
        _apply(a);
        return true;
    }

    private void OnApply(object sender, RoutedEventArgs e) => ApplyChanges();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (ApplyChanges()) DialogResult = true;
    }

    private void OnCompute(object sender, RoutedEventArgs e)
    {
        CommitAll();
        _vm.TryBuild(out _);
    }

    private static int Sel(DataGrid g) => g.SelectedIndex >= 0 ? g.SelectedIndex : g.Items.Count - 1;

    private void OnPiAdd(object sender, RoutedEventArgs e)
    {
        CommitAll();
        int i = Sel(PiGrid);
        _vm.Pis.Insert(Math.Min(i + 1, _vm.Pis.Count), _vm.NewPiRow(i));
    }

    private void OnPiDelete(object sender, RoutedEventArgs e)
    {
        if (PiGrid.SelectedItem is PiRowVm r && _vm.Pis.Count > 2) _vm.Pis.Remove(r);
    }

    private void OnVpiAdd(object sender, RoutedEventArgs e)
    {
        CommitAll();
        int i = Sel(VpiGrid);
        _vm.Vpis.Insert(Math.Min(i + 1, _vm.Vpis.Count), _vm.NewVpiRow(i));
    }

    private void OnVpiDelete(object sender, RoutedEventArgs e)
    {
        if (VpiGrid.SelectedItem is VpiRowVm r) _vm.Vpis.Remove(r);
    }

    private void OnSlopeAdd(object sender, RoutedEventArgs e)
    {
        CommitAll();
        int i = Sel(SlopeGrid);
        _vm.Slopes.Insert(Math.Min(i + 1, _vm.Slopes.Count), _vm.NewSlopeRow(i));
    }

    private void OnSlopeDelete(object sender, RoutedEventArgs e)
    {
        if (SlopeGrid.SelectedItem is SlopeRowVm r) _vm.Slopes.Remove(r);
    }
}
