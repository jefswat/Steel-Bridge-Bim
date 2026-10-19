using System.Globalization;
using System.Text.Json;
using SBM.IO.Database;

namespace SBM.Api;

/// <summary>One calc value compared with the model.</summary>
public sealed class CalcDiffRow
{
    public CalcValue Value { get; init; } = new();
    public string CalcTitle { get; init; } = "";
    /// <summary>match | differs | missing (target not found) | error (value not readable for the target).</summary>
    public string Status { get; init; } = "";
    /// <summary>The model's current value, formatted (for an input: as <c>sbm get</c> shows it; for a result: in the calc's unit).</summary>
    public string ModelText { get; init; } = "";
    /// <summary>The calc value, formatted the same way.</summary>
    public string CalcText { get; init; } = "";
    public string Message { get; init; } = "";
    /// <summary>What the value is compared with: the property path, "result Steel.SteelMass", or "element name.field".</summary>
    public string Target { get; init; } = "";
    /// <summary>A model input (property path) that differs: can be applied.</summary>
    public bool Applicable => Status == "differs" && Value.PropertyPath.Length > 0;
    public string Kind => Value.PropertyPath.Length > 0 ? "input" : "result";
}

public sealed class CalcApplyResult
{
    public List<(CalcDiffRow row, string oldText, string newText)> Applied { get; } = new();
    public List<(CalcValue value, string reason)> Skipped { get; } = new();
    public string? SavedTo { get; set; }
}

/// <summary>
/// Calc values vs the model (G9). Values come from the summary pages of a calc package as a JSON file (schema <c>sbm-calc-values/1</c>, see
/// <see cref="ParseValues"/> and docs/API.md) written by an extraction agent or by hand. <see cref="Diff"/> compares each value with the model:
/// inputs through <see cref="PropertyPath"/> (same unit parsing as <c>sbm set</c>), results through a path on the bridge's <c>BridgeModel</c>
/// (e.g. <c>Steel.SteelMass</c>) or an element field of the companion database (mass_kg, length_m, a properties key). <see cref="Apply"/> sets
/// SELECTED differing inputs, records every change in <c>calc_applied</c> (who, when, old → new, calc + page) and saves the model. Nothing is
/// ever applied by a diff.
/// </summary>
public static class CalcSync
{
    public const string Schema = "sbm-calc-values/1";

    /// <summary>Default comparison tolerance: relative, for model inputs and for results.</summary>
    public const double InputRelTol = 0.001, ResultRelTol = 0.005;

    // ------------------------------------------------------------------ values JSON

    /// <summary>
    /// Reads a values file: <c>{"schema": "sbm-calc-values/1", "extractedBy": "...", "values": [ ... ]}</c> or a bare array. Each value:
    /// <c>name</c>, optional <c>key</c>, the target = ONE of <c>propertyPath</c> | <c>resultPath</c> (+ <c>bridge</c>) | <c>field</c> with
    /// <c>globalId</c> or <c>mark</c>; <c>value</c> (number or text), <c>unit</c>, optional <c>tol</c> ("1/16\"", "0.5 kip", "1%"),
    /// <c>page</c> (calc PDF page), <c>quote</c> (the source line), optional <c>element</c> (GlobalId, mark or name, resolved on import) /
    /// <c>globalId</c> / <c>mark</c> to tie the value to an element (required with <c>field</c>).
    /// </summary>
    public static List<CalcValue> ParseValues(string json, ModelDb? db = null)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        JsonElement arr;
        string by = "";
        if (root.ValueKind == JsonValueKind.Array) arr = root;
        else
        {
            if (root.TryGetProperty("schema", out var sc) && sc.GetString() is { } s && s != Schema) throw new FormatException($"Unknown values schema '{s}' (expected {Schema}).");
            if (!root.TryGetProperty("values", out arr) || arr.ValueKind != JsonValueKind.Array) throw new FormatException("The values file needs a \"values\" array.");
            by = Str(root, "extractedBy");
        }
        var list = new List<CalcValue>();
        int i = 0;
        foreach (var e in arr.EnumerateArray())
        {
            i++;
            var v = new CalcValue
            {
                Key = Str(e, "key"), Name = Str(e, "name"), GlobalId = Str(e, "globalId"), Mark = Str(e, "mark"), PropertyPath = Str(e, "propertyPath"),
                ResultPath = Str(e, "resultPath"), Bridge = Str(e, "bridge"), Field = Str(e, "field"), Unit = Str(e, "unit"), Tolerance = Str(e, "tol"),
                Quote = Str(e, "quote"), ExtractedBy = Str(e, "extractedBy") is { Length: > 0 } eb ? eb : by,
                Page = e.TryGetProperty("page", out var pg) && pg.ValueKind == JsonValueKind.Number ? pg.GetInt32() : null,
            };
            int targets = (v.PropertyPath.Length > 0 ? 1 : 0) + (v.ResultPath.Length > 0 ? 1 : 0) + (v.Field.Length > 0 ? 1 : 0);
            if (targets != 1) throw new FormatException($"value {i} ({v.Name}): give exactly one target: propertyPath, resultPath or field (with globalId / mark).");
            string elRef = Str(e, "element");
            if (elRef.Length > 0 && v.GlobalId.Length == 0)
            {
                if (db == null) throw new FormatException($"value {i} ({v.Name}): \"element\" needs the companion database to resolve it.");
                v.GlobalId = db.TryFindElement(elRef) ?? throw new FormatException($"value {i} ({v.Name}): element '{elRef}' matches no single model element (use globalId).");
            }
            if (v.Field.Length > 0 && v.GlobalId.Length == 0 && v.Mark.Length == 0) throw new FormatException($"value {i} ({v.Name}): field needs element, globalId or mark.");
            if (!e.TryGetProperty("value", out var val)) throw new FormatException($"value {i} ({v.Name}): no value.");
            if (val.ValueKind == JsonValueKind.Number)
            {
                v.Value = val.GetDouble();
                v.ValueText = val.GetRawText() + (v.Unit.Length > 0 ? " " + v.Unit : "");
            }
            else
            {
                string t = val.ValueKind == JsonValueKind.String ? val.GetString()! : val.GetRawText();
                bool numeric = double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
                if (numeric) v.Value = d;
                v.ValueText = numeric && v.Unit.Length > 0 ? t + " " + v.Unit : t;
            }
            if (db != null && v.GlobalId.Length == 0 && v.Mark.Length > 0 && db.ElementByMark(v.Mark) is { } g) v.GlobalId = g;
            list.Add(v);
        }
        return list;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) ? p.ValueKind switch { JsonValueKind.String => p.GetString() ?? "", JsonValueKind.Null => "", _ => p.GetRawText() } : "";

    /// <summary>Reads a values file into the calc's values (replacing the previous extraction; the applied-change log is kept).</summary>
    public static int ImportValues(ModelDb db, long calcId, string jsonFile) => db.ReplaceCalcValues(calcId, ParseValues(File.ReadAllText(jsonFile), db));

    // ------------------------------------------------------------------ units for results

    private static readonly Dictionary<string, (string dim, double si)> UnitTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m"] = ("L", 1), ["mm"] = ("L", 0.001), ["cm"] = ("L", 0.01), ["in"] = ("L", 0.0254), ["\""] = ("L", 0.0254), ["ft"] = ("L", 0.3048), ["'"] = ("L", 0.3048), ["yd"] = ("L", 0.9144),
        ["kg"] = ("M", 1), ["t"] = ("M", 1000), ["tonne"] = ("M", 1000), ["lb"] = ("M", 0.45359237), ["lbs"] = ("M", 0.45359237), ["kip"] = ("M", 453.59237),
        ["kips"] = ("M", 453.59237), ["ton"] = ("M", 907.18474), ["tons"] = ("M", 907.18474), ["kn"] = ("M", 1000 / 9.80665), ["n"] = ("M", 1 / 9.80665),
        ["m2"] = ("A", 1), ["mm2"] = ("A", 1e-6), ["in2"] = ("A", 0.00064516), ["ft2"] = ("A", 0.09290304), ["sf"] = ("A", 0.09290304), ["sq ft"] = ("A", 0.09290304),
        ["yd2"] = ("A", 0.83612736), ["sy"] = ("A", 0.83612736),
        ["m3"] = ("V", 1), ["ft3"] = ("V", 0.028316846592), ["cf"] = ("V", 0.028316846592), ["yd3"] = ("V", 0.764554857984), ["cy"] = ("V", 0.764554857984), ["in3"] = ("V", 1.6387064e-5),
        ["pa"] = ("S", 1), ["kpa"] = ("S", 1e3), ["mpa"] = ("S", 1e6), ["psi"] = ("S", 6894.757293), ["ksi"] = ("S", 6894757.293),
        ["kip-ft"] = ("Mo", 1355.8179483), ["k-ft"] = ("Mo", 1355.8179483), ["kn-m"] = ("Mo", 1000),
        ["deg"] = ("D", 1), ["°"] = ("D", 1),
    };

    /// <summary>SI unit of a database field / properties key from its suffix (mass_kg, length_m, ...), null when unknown.</summary>
    private static string? FieldUnit(string field)
    {
        string f = field.ToLowerInvariant();
        foreach (var (suffix, unit) in new[] { ("_kg", "kg"), ("_m3", "m3"), ("_m2", "m2"), ("_mm", "mm"), ("_m", "m"), ("_in", "in"), ("_ft", "ft"), ("_lb", "lb"), ("_ksi", "ksi") })
            if (f.EndsWith(suffix, StringComparison.Ordinal)) return unit;
        return null;
    }

    /// <summary>A model result in SI vs a calc number in <paramref name="unit"/>: the model value converted to the calc unit (null = unknown unit).</summary>
    public static double? FromSi(double si, string unit) => UnitTable.TryGetValue(unit.Trim(), out var u) ? si / u.si : null;
    public static double? ToSi(double v, string unit) => UnitTable.TryGetValue(unit.Trim(), out var u) ? v * u.si : null;

    // ------------------------------------------------------------------ diff

    private static string Norm(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant().Replace("×", "X");

    private static (double rel, double abs) Tol(CalcValue v, Func<string, double?> parseAbs, double defRel)
    {
        string t = v.Tolerance.Trim();
        if (t.Length == 0) return (defRel, 1e-9);
        if (t.EndsWith('%') && double.TryParse(t[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double pct)) return (pct / 100, 1e-9);
        return parseAbs(t) is { } a ? (0, Math.Abs(a)) : (defRel, 1e-9);
    }

    private static bool Close(double a, double b, (double rel, double abs) tol) => Math.Abs(a - b) <= Math.Max(tol.abs, tol.rel * Math.Max(Math.Abs(a), Math.Abs(b))) + 1e-12;

    /// <summary>Compares the values of one calc (or all calcs) with the model. Reads only.</summary>
    public static List<CalcDiffRow> Diff(SbmDocument doc, ModelDb db, long? calcId = null)
    {
        var calcs = db.Calcs().ToDictionary(c => c.Id);
        var rows = new List<CalcDiffRow>();
        foreach (var v in db.CalcValues(calcId))
            rows.Add(DiffOne(doc, db, v, calcs.TryGetValue(v.CalcId, out var c) ? c.Title : ""));
        return rows;
    }

    public static CalcDiffRow DiffOne(SbmDocument doc, ModelDb? db, CalcValue v, string calcTitle = "")
    {
        string target = v.TargetText;
        if (v.Field.Length > 0 && db != null && (v.GlobalId.Length > 0 ? v.GlobalId : db.ElementByMark(v.Mark)) is { } tg && db.Get(tg) is { } te)
            target = (te.Name.Length > 0 ? te.Name : te.Category) + "." + v.Field;
        CalcDiffRow R(string status, string model, string calc, string msg = "") =>
            new() { Value = v, CalcTitle = calcTitle, Status = status, ModelText = model, CalcText = calc, Message = msg, Target = target };
        try
        {
            if (v.PropertyPath.Length > 0)
            {
                object? cur;
                try { cur = doc.Get(v.PropertyPath); }
                catch (ArgumentException ex) { return R("missing", "", v.ValueText, ex.Message); }
                string curText = PropertyPath.Format(cur, v.PropertyPath, doc.Units);
                object? calc;
                try { calc = PropertyPath.Parse(doc.Project, v.PropertyPath, v.ValueText, doc.Units); }
                catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException) { return R("error", curText, v.ValueText, ex.Message); }
                string calcText = PropertyPath.Format(calc, v.PropertyPath, doc.Units);
                bool same;
                if (cur is double a && calc is double b)
                {
                    var tol = Tol(v, t => PropertyPath.Parse(doc.Project, v.PropertyPath, t, doc.Units) is double d ? d : null, InputRelTol);
                    same = Close(a, b, tol);
                }
                else same = Norm(curText) == Norm(calcText);
                return R(same ? "match" : "differs", curText, calcText);
            }
            object? model;
            string where;
            if (v.ResultPath.Length > 0)
            {
                var bm = doc.Model(v.Bridge.Length > 0 ? v.Bridge : null);
                try { model = PropertyPath.GetValue(bm, v.ResultPath); }
                catch (ArgumentException ex) { return R("missing", "", v.ValueText, ex.Message); }
                where = "result";
            }
            else
            {
                if (db == null) return R("error", "", v.ValueText, "element fields need the companion database");
                string gid = v.GlobalId.Length > 0 ? v.GlobalId : db.ElementByMark(v.Mark) ?? "";
                if (gid.Length == 0) return R("missing", "", v.ValueText, $"no single element with mark '{v.Mark}'");
                model = db.ElementField(gid, v.Field);
                if (model == null) return R("missing", "", v.ValueText, db.Get(gid) == null ? $"element {gid} is not in the model (orphan)" : $"element has no field '{v.Field}'");
                where = "field";
            }
            if (model is double or float or int or long)
            {
                double m = Convert.ToDouble(model, CultureInfo.InvariantCulture);
                if (v.Value is not { } cv) return R("error", m.ToString("G6", CultureInfo.InvariantCulture), v.ValueText, "the calc value is not a number");
                // results are SI (kg, m, m2, m3, Pa) unless the field name says otherwise (…_ksi, …_ft)
                string siUnit = where == "field" ? FieldUnit(v.Field) ?? "" : "";
                double mInCalcUnit = m;
                if (v.Unit.Length > 0)
                {
                    double? toCalc = siUnit.Length > 0 ? (ToSi(m, siUnit) is { } si ? FromSi(si, v.Unit) : null) : FromSi(m, v.Unit);
                    if (toCalc == null) return R("error", m.ToString("G6", CultureInfo.InvariantCulture), v.ValueText, $"unknown unit '{v.Unit}' (known: {string.Join(" ", UnitTable.Keys.Take(30))} ...)");
                    mInCalcUnit = toCalc.Value;
                }
                var tol = Tol(v, t =>
                {
                    var parts = t.Split(' ', 2, StringSplitOptions.TrimEntries);
                    return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ? (parts.Length > 1 && v.Unit.Length > 0 && ToSi(x, parts[1]) is { } xs ? FromSi(xs, v.Unit) : x) : null;
                }, ResultRelTol);
                string unit = v.Unit.Length > 0 ? " " + v.Unit : "";
                return R(Close(mInCalcUnit, cv, tol) ? "match" : "differs", mInCalcUnit.ToString("#,0.###", CultureInfo.InvariantCulture) + unit, cv.ToString("#,0.###", CultureInfo.InvariantCulture) + unit);
            }
            string ms = model switch { null => "null", string s => s, bool b => b ? "true" : "false", _ => PropertyPath.Format(model, v.ResultPath, doc.Units) };
            return R(Norm(ms) == Norm(v.ValueText) ? "match" : "differs", ms, v.ValueText);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            return R("error", "", v.ValueText, ex.Message);
        }
    }

    // ------------------------------------------------------------------ apply

    /// <summary>
    /// Applies the given calc values (ids) to the model inputs: only values whose target is a property path and that currently DIFFER are set;
    /// everything else is reported as skipped. Every change is recorded in <c>calc_applied</c>. The model is saved to its file when
    /// <paramref name="save"/> (the CLI; the app saves through its own command).
    /// </summary>
    public static CalcApplyResult Apply(SbmDocument doc, ModelDb db, IEnumerable<long> valueIds, string? by = null, bool save = true)
    {
        var res = new CalcApplyResult();
        var all = db.CalcValues().ToDictionary(v => v.Id);
        var calcs = db.Calcs().ToDictionary(c => c.Id);
        foreach (var id in valueIds.Distinct())
        {
            if (!all.TryGetValue(id, out var v)) { res.Skipped.Add((new CalcValue { Id = id }, "no such calc value")); continue; }
            string title = calcs.TryGetValue(v.CalcId, out var c) ? c.Title : "";
            if (v.PropertyPath.Length == 0) { res.Skipped.Add((v, "a model result, not an input: it cannot be applied (change the inputs that produce it)")); continue; }
            var row = DiffOne(doc, db, v, title);
            if (row.Status != "differs") { res.Skipped.Add((v, row.Status == "match" ? "already matches the model" : $"{row.Status}: {row.Message}")); continue; }
            string old = row.ModelText;
            string now = doc.Set(v.PropertyPath, v.ValueText);
            db.RecordApplied(v, title, old, now, doc.FilePath, by);
            res.Applied.Add((row, old, now));
        }
        if (save && res.Applied.Count > 0 && doc.FilePath != null)
        {
            doc.Save();
            res.SavedTo = doc.FilePath;
        }
        return res;
    }

    /// <summary>Parses "3,4,7-9" into value ids.</summary>
    public static List<long> ParseIds(string text)
    {
        var ids = new List<long>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ab = part.Split('-', StringSplitOptions.TrimEntries);
            if (!long.TryParse(ab[0], out long a)) throw new ArgumentException($"Bad id list '{text}' (e.g. 3,4,7-9).");
            long b = ab.Length > 1 && long.TryParse(ab[1], out long bb) ? bb : a;
            for (long i = a; i <= b; i++) ids.Add(i);
        }
        return ids;
    }
}
