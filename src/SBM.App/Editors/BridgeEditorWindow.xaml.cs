using System.Windows;
using System.Windows.Controls;
using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Units;

namespace SBM.App.Editors;

public partial class BridgeEditorWindow : Window
{
    private readonly BridgeEditorViewModel _vm;
    private readonly Action<BridgeDefinition> _apply;

    private BridgeEditorWindow(BridgeDefinition def, BridgeProject project, UnitSystem units, int tab, bool isNew, Action<BridgeDefinition> apply)
    {
        InitializeComponent();
        _vm = new BridgeEditorViewModel(def, project, units);
        _apply = apply;
        DataContext = _vm;
        Title = isNew ? "New Bridge" : $"Bridge — {def.Name}";
        Tabs.SelectedIndex = Math.Clamp(tab, 0, Tabs.Items.Count - 1);

        GridColumns.Configure(SupportGrid);
        GridColumns.AddText(SupportGrid, "Name", nameof(SupportRowVm.Name), 110);
        GridColumns.AddCombo(SupportGrid, "Type", nameof(SupportRowVm.Type), _vm.SupportTypes, 110);
        GridColumns.AddCell(SupportGrid, "CL Brg Station", nameof(SupportRowVm.Station), 130);
        GridColumns.AddCell(SupportGrid, "Skew", nameof(SupportRowVm.Skew), 110);
        GridColumns.AddCell(SupportGrid, "Deck End Offset", nameof(SupportRowVm.DeckEndOffset), 120);
        GridColumns.AddCell(SupportGrid, "Girder Ext.", nameof(SupportRowVm.GirderExtension), 100);
        GridColumns.AddCombo(SupportGrid, "Bearing Fixity", nameof(SupportRowVm.Fixity), _vm.Fixities, 150);
        GridColumns.AddText(SupportGrid, "Span (back)", nameof(SupportRowVm.SpanLength), 110, readOnly: true);
        SupportGrid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(_vm.UpdateSpanLengths);

        GridColumns.Configure(SpliceGrid);
        GridColumns.AddCell(SpliceGrid, "Station", nameof(SpliceRowVm.Station), 200);

        GridColumns.Configure(PlateGrid);
        GridColumns.AddCombo(PlateGrid, "Plate", nameof(PlateRowVm.Plate), _vm.PlateTypes, 140);
        GridColumns.AddCell(PlateGrid, "Ends at Station", nameof(PlateRowVm.EndStation), 140);
        GridColumns.AddCell(PlateGrid, "Width", nameof(PlateRowVm.Width), 110);
        GridColumns.AddCell(PlateGrid, "Thickness", nameof(PlateRowVm.Thickness), 110);
    }

    private void OnPlateAdd(object sender, RoutedEventArgs e)
    {
        GridColumns.CommitEdits(PlateGrid);
        int i = Sel(PlateGrid);
        _vm.PlateRows.Insert(Math.Min(i + 1, _vm.PlateRows.Count), _vm.NewPlateRow(i));
    }

    private void OnPlateDelete(object sender, RoutedEventArgs e)
    {
        if (PlateGrid.SelectedItem is PlateRowVm r) _vm.PlateRows.Remove(r);
    }

    /// <summary>Non-modal instance with a no-op apply, for rendering previews/snapshots.</summary>
    private void OnCopySubstructure(object sender, RoutedEventArgs e) => _vm.CopySubstructureToSameType();

    public static BridgeEditorWindow CreateForPreview(BridgeDefinition def, BridgeProject project, UnitSystem units) =>
        new(def, project, units, 0, false, _ => { });

    public static void Edit(BridgeDefinition def, BridgeProject project, UnitSystem units, int tab, bool isNew, Action<BridgeDefinition> apply)
    {
        var w = new BridgeEditorWindow(def, project, units, tab, isNew, apply) { Owner = Application.Current.MainWindow };
        w.ShowDialog();
    }

    private bool ApplyChanges()
    {
        GridColumns.CommitEdits(SupportGrid);
        GridColumns.CommitEdits(SpliceGrid);
        GridColumns.CommitEdits(PlateGrid);
        if (!_vm.TryBuild(out var def) || def == null) return false;
        _apply(def);
        return true;
    }

    private void OnApply(object sender, RoutedEventArgs e) => ApplyChanges();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (ApplyChanges()) DialogResult = true;
    }

    private static int Sel(DataGrid g) => g.SelectedIndex >= 0 ? g.SelectedIndex : g.Items.Count - 1;

    private void OnSupportAdd(object sender, RoutedEventArgs e)
    {
        GridColumns.CommitEdits(SupportGrid);
        int i = Sel(SupportGrid);
        _vm.Supports.Insert(Math.Min(i + 1, _vm.Supports.Count), _vm.NewSupportAfter(i));
        _vm.RenameSupports();
        _vm.UpdateSpanLengths();
    }

    private void OnSupportDelete(object sender, RoutedEventArgs e)
    {
        if (SupportGrid.SelectedItem is SupportRowVm r && _vm.Supports.Count > 2)
        {
            _vm.Supports.Remove(r);
            _vm.RenameSupports();
            _vm.UpdateSpanLengths();
        }
    }

    private void OnSetSpans(object sender, RoutedEventArgs e)
    {
        GridColumns.CommitEdits(SupportGrid);
        _vm.ErrorText = _vm.ApplySpanList(_vm.SpanListText) ?? "";
    }

    private void OnCenterGirders(object sender, RoutedEventArgs e) => _vm.CenterGirders();

    private void OnSpliceAdd(object sender, RoutedEventArgs e)
    {
        GridColumns.CommitEdits(SpliceGrid);
        double sta = 0;
        if (_vm.Splices.Count > 0) _vm.Splices[^1].Station.TryGet(out sta);
        else if (_vm.Supports.Count > 1 && _vm.Supports[0].Station.TryGet(out double s0) && _vm.Supports[1].Station.TryGet(out double s1))
            sta = s0 + 0.75 * (s1 - s0);
        _vm.Splices.Add(new SpliceRowVm(_vm.Units, new FieldSpliceDefinition { Station = sta + (_vm.Splices.Count > 0 ? 50 * _vm.Units.LongUnit : 0) }));
    }

    private void OnSpliceDelete(object sender, RoutedEventArgs e)
    {
        if (SpliceGrid.SelectedItem is SpliceRowVm r) _vm.Splices.Remove(r);
    }
}
