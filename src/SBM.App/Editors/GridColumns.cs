using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SBM.App.Editors;

/// <summary>Builds DataGrid columns bound to <see cref="CellValue"/> properties, with red highlighting on parse errors.</summary>
public static class GridColumns
{
    private static readonly Brush ErrorBg = Freeze(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x40, 0x40)));
    private static Brush Freeze(Brush b) { b.Freeze(); return b; }

    public static void AddCell(DataGrid grid, string header, string property, double width = 120)
    {
        var display = new Style(typeof(TextBlock));
        display.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0)));
        var trigger = new DataTrigger { Binding = new Binding($"{property}.HasError"), Value = true };
        trigger.Setters.Add(new Setter(TextBlock.BackgroundProperty, ErrorBg));
        display.Triggers.Add(trigger);

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding($"{property}.Text") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            Width = new DataGridLength(width),
            ElementStyle = display,
        });
    }

    public static void AddText(DataGrid grid, string header, string property, double width = 120, bool readOnly = false)
    {
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(property) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay },
            Width = new DataGridLength(width),
            IsReadOnly = readOnly,
        });
    }

    /// <summary>
    /// Choice column: a TextBlock when displayed, the app-styled (dark) ComboBox when editing. A DataGridComboBoxColumn
    /// uses the classic ComboBox template, which renders light text on a white box under the dark theme.
    /// </summary>
    public static void AddCombo(DataGrid grid, string header, string property, System.Collections.IEnumerable items, double width = 120)
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(property));
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var combo = new FrameworkElementFactory(typeof(ComboBox));
        combo.SetValue(ItemsControl.ItemsSourceProperty, items);
        combo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
            new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        combo.SetValue(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)));
        combo.SetValue(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)));
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = header,
            CellTemplate = new DataTemplate { VisualTree = text },
            CellEditingTemplate = new DataTemplate { VisualTree = combo },
            Width = new DataGridLength(width),
        });
    }

    public static void Configure(DataGrid grid)
    {
        grid.AutoGenerateColumns = false;
        grid.CanUserAddRows = false;
        grid.CanUserDeleteRows = false;
        grid.CanUserSortColumns = false;
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.SelectionMode = DataGridSelectionMode.Single;
        grid.RowHeaderWidth = 28;
        grid.LoadingRow += (_, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
    }

    /// <summary>Commits any in-progress cell edit so bound values are current.</summary>
    public static void CommitEdits(DataGrid grid)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
    }
}
