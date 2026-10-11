using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SBM.Core.Units;

namespace SBM.App.Editors;

/// <summary>How a numeric value is displayed and parsed.</summary>
public enum ValueKind { LengthLong, LengthShort, Station, Elevation, Coordinate, Angle, Slope, Integer, Number, Text }

/// <summary>Unit-aware formatting/parsing shared by form fields and grid rows.</summary>
public static class ValueFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Format(UnitSystem u, ValueKind kind, double v) => kind switch
    {
        ValueKind.LengthLong => u.FormatLength(v, LengthKind.Long),
        ValueKind.LengthShort => u.FormatLength(v, LengthKind.Short),
        ValueKind.Station => u.FormatStation(v),
        ValueKind.Elevation => u.FormatLength(v, LengthKind.Coordinate),
        ValueKind.Coordinate => (v / u.LongUnit).ToString("F4", Inv),
        ValueKind.Angle => v.ToString("0.######", Inv) + "°",
        ValueKind.Slope => UnitSystem.FormatSlope(v),
        ValueKind.Integer => ((int)Math.Round(v)).ToString(Inv),
        _ => v.ToString("0.######", Inv),
    };

    public static bool TryParse(UnitSystem u, ValueKind kind, string? text, out double v)
    {
        v = double.NaN;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        switch (kind)
        {
            case ValueKind.LengthLong: return u.TryParseLength(text, LengthKind.Long, out v);
            case ValueKind.LengthShort: return u.TryParseLength(text, LengthKind.Short, out v);
            case ValueKind.Station: return u.TryParseStation(text, out v);
            case ValueKind.Elevation:
            case ValueKind.Coordinate:
                if (u.TryParseLength(text, LengthKind.Coordinate, out v)) return true;
                return false;
            case ValueKind.Angle:
                if (UnitSystem.TryParseAngle(text, out double r)) { v = r * 180 / Math.PI; return true; }
                return false;
            case ValueKind.Slope: return UnitSystem.TryParseSlope(text, out v);
            case ValueKind.Integer:
                if (int.TryParse(text, NumberStyles.Integer, Inv, out int i)) { v = i; return true; }
                return false;
            default: return double.TryParse(text, NumberStyles.Float, Inv, out v);
        }
    }

    public static string Hint(UnitSystem u, ValueKind kind) => kind switch
    {
        ValueKind.LengthLong => u.IsUS ? "e.g. 12'-6 1/2\", 12.54', 1.2 m" : "e.g. 3.25, 3250 mm",
        ValueKind.LengthShort => u.IsUS ? "inches, e.g. 7/8, 1 1/4\"" : "mm",
        ValueKind.Station => u.IsUS ? "e.g. 12+34.56" : "e.g. 1+234.567",
        ValueKind.Elevation => u.IsUS ? "feet" : "meters",
        ValueKind.Coordinate => u.IsUS ? "feet" : "meters",
        ValueKind.Angle => "degrees or D°M'S\"",
        ValueKind.Slope => "percent, e.g. -2%",
        _ => "",
    };
}

public abstract partial class FieldBase : ObservableObject
{
    public string Label { get; init; } = "";
    public string? ToolTip { get; init; }
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isEnabled = true;

    public abstract void Refresh();
    /// <summary>Validates and writes the value back. Returns false if invalid.</summary>
    public abstract bool Commit();
}

public sealed class HeaderField : FieldBase
{
    public override void Refresh() { }
    public override bool Commit() => true;
}

public sealed partial class TextField : FieldBase
{
    private readonly UnitSystem _units;
    private readonly Func<double> _get;
    private readonly Action<double> _set;
    private readonly Func<double, string?>? _validate;

    public ValueKind Kind { get; }
    [ObservableProperty] private string _text = "";
    /// <summary>When set, a blank entry is allowed and means "automatic" (stored as NaN); the text describes the automatic value.</summary>
    public string? BlankMeans
    {
        get => _blankMeans;
        init { _blankMeans = value; Refresh(); }
    }
    private readonly string? _blankMeans;
    public string Hint => BlankMeans == null ? ValueFormat.Hint(_units, Kind)
        : ValueFormat.Hint(_units, Kind) is { Length: > 0 } h ? $"blank = {BlankMeans}; {h}" : $"blank = {BlankMeans}";

    public TextField(UnitSystem units, string label, ValueKind kind, Func<double> get, Action<double> set, Func<double, string?>? validate = null, string? tip = null)
    {
        _units = units;
        Label = label;
        Kind = kind;
        _get = get;
        _set = set;
        _validate = validate;
        ToolTip = tip;
        Refresh();
    }

    public override void Refresh()
    {
        double v = _get();
        Text = BlankMeans != null && double.IsNaN(v) ? "" : ValueFormat.Format(_units, Kind, v);
    }

    public override bool Commit()
    {
        if (BlankMeans != null && string.IsNullOrWhiteSpace(Text))
        {
            Error = null;
            _set(double.NaN);
            return true;
        }
        if (!ValueFormat.TryParse(_units, Kind, Text, out double v))
        {
            Error = $"Can't read \"{Text}\" ({Hint}).";
            return false;
        }
        var msg = _validate?.Invoke(v);
        if (msg != null) { Error = msg; return false; }
        Error = null;
        _set(v);
        Refresh();
        return true;
    }

    partial void OnTextChanged(string value) => Error = null;
}

public sealed partial class StringField : FieldBase
{
    private readonly Func<string> _get;
    private readonly Action<string> _set;
    private readonly Func<string, string?>? _validate;
    [ObservableProperty] private string _text = "";
    /// <summary>A blank entry is allowed (stored as "").</summary>
    public bool AllowEmpty { get; init; }

    public StringField(string label, Func<string> get, Action<string> set, Func<string, string?>? validate = null, string? tip = null)
    {
        Label = label;
        _get = get;
        _set = set;
        _validate = validate;
        ToolTip = tip;
        Refresh();
    }

    public override void Refresh() => Text = _get();
    public override bool Commit()
    {
        string t = (Text ?? "").Trim();
        if (t.Length == 0 && !AllowEmpty) { Error = "Required."; return false; }
        if (t.Length > 0 && _validate?.Invoke(t) is { } msg) { Error = msg; return false; }
        Error = null;
        _set(t);
        return true;
    }

    partial void OnTextChanged(string value) => Error = null;
}

/// <summary>Comma-separated list of lengths (e.g. bar positions "8, 15 1/4, 22 1/2"); blank = none (null).</summary>
public sealed partial class LengthListField : FieldBase
{
    private readonly UnitSystem _u;
    private readonly LengthKind _kind;
    private readonly Func<List<double>?> _get;
    private readonly Action<List<double>?> _set;
    [ObservableProperty] private string _text = "";
    public string Hint { get; }

    public LengthListField(UnitSystem u, string label, LengthKind kind, Func<List<double>?> get, Action<List<double>?> set, string blankMeans, string? tip = null)
    {
        _u = u;
        _kind = kind;
        Label = label;
        _get = get;
        _set = set;
        ToolTip = tip;
        Hint = $"blank = {blankMeans}; comma list, {ValueFormat.Hint(u, kind == LengthKind.Short ? ValueKind.LengthShort : ValueKind.LengthLong)}";
        Refresh();
    }

    public override void Refresh() => Text = _get() is { Count: > 0 } l ? string.Join(", ", l.Select(x => _u.FormatLength(x, _kind))) : "";

    public override bool Commit()
    {
        var list = new List<double>();
        foreach (var tok in (Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!_u.TryParseLength(tok, _kind, out double v) || v < 0) { Error = $"Can't read \"{tok}\"."; return false; }
            list.Add(v);
        }
        Error = null;
        _set(list.Count == 0 ? null : list);
        Refresh();
        return true;
    }

    partial void OnTextChanged(string value) => Error = null;
}

public sealed partial class BoolField : FieldBase
{
    private readonly Func<bool> _get;
    private readonly Action<bool> _set;
    [ObservableProperty] private bool _value;

    public BoolField(string label, Func<bool> get, Action<bool> set, string? tip = null)
    {
        Label = label;
        _get = get;
        _set = set;
        ToolTip = tip;
        Refresh();
    }

    public override void Refresh() => Value = _get();
    public override bool Commit() { _set(Value); return true; }
}

public sealed record Choice(string Label, object Value)
{
    public override string ToString() => Label;
}

public sealed partial class ChoiceField : FieldBase
{
    private readonly Func<object> _get;
    private readonly Action<object> _set;
    public ObservableCollection<Choice> Options { get; }
    [ObservableProperty] private Choice? _selected;

    public ChoiceField(string label, IEnumerable<Choice> options, Func<object> get, Action<object> set, string? tip = null)
    {
        Label = label;
        Options = new ObservableCollection<Choice>(options);
        _get = get;
        _set = set;
        ToolTip = tip;
        Refresh();
    }

    public static ChoiceField ForEnum<T>(string label, Func<T> get, Action<T> set, Func<T, string>? name = null, string? tip = null) where T : struct, Enum =>
        new(label, Enum.GetValues<T>().Select(v => new Choice(name?.Invoke(v) ?? Humanize(v.ToString()), v)),
            () => get(), v => set((T)v), tip);

    public static string Humanize(string s) =>
        string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1]) ? " " + c : c.ToString()));

    public override void Refresh()
    {
        var v = _get();
        Selected = Options.FirstOrDefault(o => Equals(o.Value, v)) ?? Options.FirstOrDefault();
    }

    public override bool Commit()
    {
        if (Selected != null) _set(Selected.Value);
        return true;
    }
}

/// <summary>Picks the form template for a field type.</summary>
public sealed class FieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Text { get; set; }
    public DataTemplate? String { get; set; }
    public DataTemplate? Bool { get; set; }
    public DataTemplate? Choice { get; set; }
    public DataTemplate? Header { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        TextField or SpacingField or IntListField or LengthListField => Text,
        StringField => String,
        BoolField => Bool,
        ChoiceField => Choice,
        HeaderField => Header,
        _ => base.SelectTemplate(item, container),
    };
}

/// <summary>A grid cell bound to a numeric value with unit-aware text.</summary>
public sealed partial class CellValue : ObservableObject
{
    private readonly UnitSystem _units;
    public ValueKind Kind { get; }
    public bool AllowBlank { get; }
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _hasError;

    public CellValue(UnitSystem units, ValueKind kind, double? value, bool allowBlank = false)
    {
        _units = units;
        Kind = kind;
        AllowBlank = allowBlank;
        _text = value is { } v && !double.IsNaN(v) ? ValueFormat.Format(units, kind, v) : "";
    }

    public bool TryGet(out double v)
    {
        if (AllowBlank && string.IsNullOrWhiteSpace(Text)) { v = double.NaN; HasError = false; return true; }
        bool ok = ValueFormat.TryParse(_units, Kind, Text, out v);
        HasError = !ok;
        return ok;
    }

    partial void OnTextChanged(string value) => HasError = false;

    public override string ToString() => Text;
}
