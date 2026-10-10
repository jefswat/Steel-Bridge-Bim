using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SBM.Core.Units;

namespace SBM.Api;

/// <summary>
/// Reads and writes model values by path, e.g. <c>Bridges[0].Supports[1].SkewDegrees</c>,
/// <c>Bridges["Sample Bridge"].Girders.Section.WebDepth</c> or <c>Alignments[CL Route 1].StartStation</c>.
/// Values are text with engineering units: lengths accept 12'-6 1/2", 54", 1.2 m, 300 mm; stations 12+34.56;
/// angles 15°30'00"; slopes -2%. Plain numbers use the display unit implied by the property (see
/// <see cref="KindOf"/>). JSON (starting with { or [) sets whole objects/lists in internal SI units.
/// </summary>
public static partial class PropertyPath
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    [GeneratedRegex(@"\G(?:\.?(?<name>[A-Za-z_][A-Za-z0-9_]*)|\[(?<idx>[^\]]+)\])")]
    private static partial Regex Segment();

    private sealed record Step(string? Name, string? Index);

    private static List<Step> Parse(string path)
    {
        var steps = new List<Step>();
        int pos = 0;
        path = path.Trim();
        while (pos < path.Length)
        {
            var m = Segment().Match(path, pos);
            if (!m.Success || m.Length == 0) throw new ArgumentException($"Can't parse path at '{path[pos..]}'.");
            steps.Add(m.Groups["name"].Success ? new Step(m.Groups["name"].Value, null) : new Step(null, m.Groups["idx"].Value.Trim().Trim('"', '\'')));
            pos += m.Length;
        }
        return steps;
    }

    private static object? Index(object list, string index)
    {
        if (list is not IList l) throw new ArgumentException("Indexing a value that is not a list.");
        if (int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
        {
            if (i < 0) i += l.Count;
            if (i < 0 || i >= l.Count) throw new ArgumentException($"Index {index} is out of range (0–{l.Count - 1}).");
            return l[i];
        }
        foreach (var item in l)
        {
            var nameProp = item?.GetType().GetProperty("Name");
            if (nameProp?.GetValue(item) is string n && n.Equals(index, StringComparison.OrdinalIgnoreCase)) return item;
        }
        throw new ArgumentException($"No item named '{index}'.");
    }

    private static PropertyInfo Prop(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
        ?? throw new ArgumentException($"{target.GetType().Name} has no property '{name}'. Available: " +
                                       string.Join(", ", target.GetType().GetProperties().Select(p => p.Name)));

    /// <summary>Resolves the object that owns the last path step, and the last step.</summary>
    private static (object owner, Step last) Owner(object root, string path)
    {
        var steps = Parse(path);
        if (steps.Count == 0) throw new ArgumentException("Empty path.");
        object cur = root;
        for (int i = 0; i < steps.Count - 1; i++) cur = Walk(cur, steps[i]) ?? throw new ArgumentException($"'{Join(steps.Take(i + 1))}' is null.");
        return (cur, steps[^1]);
    }

    private static object? Walk(object cur, Step s) => s.Name != null ? Prop(cur, s.Name).GetValue(cur) : Index(cur, s.Index!);

    private static string Join(IEnumerable<Step> steps) => string.Concat(steps.Select(s => s.Name != null ? "." + s.Name : $"[{s.Index}]")).TrimStart('.');

    public static object? GetValue(object root, string path)
    {
        object? cur = root;
        foreach (var s in Parse(path))
        {
            if (cur == null) return null;
            cur = Walk(cur, s);
        }
        return cur;
    }

    /// <summary>Formats a value for display: doubles with units inferred from the property name, objects as JSON.</summary>
    public static string Format(object? value, string path, UnitSystem u)
    {
        if (value == null) return "null";
        if (value is double d)
        {
            string name = Parse(path).LastOrDefault(s => s.Name != null)?.Name ?? "";
            if (name.Equals("StrandDiameter", StringComparison.OrdinalIgnoreCase))
                return SBM.Core.Prestressed.TxGirderCatalog.StrandSize(d, u);
            return KindOf(name) switch
            {
                ValueKindHint.Station => u.FormatStation(d),
                ValueKindHint.Angle => d.ToString("0.######", CultureInfo.InvariantCulture) + "°",
                ValueKindHint.Slope => UnitSystem.FormatSlope(d),
                ValueKindHint.Short => u.FormatLength(d, LengthKind.Short),
                ValueKindHint.Plain => d.ToString("0.######", CultureInfo.InvariantCulture),
                _ => u.FormatLength(d),
            };
        }
        if (value is string or bool or int or Enum) return System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return JsonSerializer.Serialize(value, value.GetType(), Json);
    }

    public enum ValueKindHint { Long, Short, Station, Angle, Slope, Plain }

    /// <summary>Infers how a numeric property is entered from its name (plain numbers only; explicit units always win).</summary>
    public static ValueKindHint KindOf(string propertyName)
    {
        string n = propertyName.ToLowerInvariant();
        if (n.Contains("station")) return ValueKindHint.Station;
        if (n.Contains("degrees")) return ValueKindHint.Angle;
        if (n.Contains("slope")) return ValueKindHint.Slope;
        if (n.Contains("ratio") || n.Contains("scale") || n.Contains("fraction") || n.Contains("ksi") || n.Contains("opacity") || n.Contains("strength")) return ValueKindHint.Plain;
        if (n is "depressedtopatend" or "bearingtoend" or "padlength" or "genericbodylength") return ValueKindHint.Short;
        if (n is "maxbarlength") return ValueKindHint.Long;
        if (n.Contains("cover") || n.Contains("lapoverride") || n.Contains("tolerance")) return ValueKindHint.Short;
        // Cross girder / inspection access inputs: small dimensions in inches, run lengths and spacings in feet.
        if (n is "anglelength" or "endanglelength" or "bracketlength" or "anchorbracketlength" or "supportbracketlength" or "topbelowdiscontinuouswebtop"
            or "beginoffset" or "endoffset" or "ropeoffsetfromweb" or "ropeheightabovewebbottom" or "gratingabovewebbottom" or "walkwayendclearance"
            || n.Contains("fromopening") || n.Contains("toopening") || n.Contains("fromtop") || n.Contains("endclear")) return ValueKindHint.Short;
        if (n is "maxanchorspacing" or "maxpostspacing" or "stringerspanlimit" or "bracketclearofanchor") return ValueKindHint.Long;
        if (n.Contains("offset") && !n.Contains("workpoint")) return ValueKindHint.Long;
        string[] shortWords = { "thickness", "width", "diameter", "gap", "clip", "haunch", "edge", "pitch", "gage", "size", "lap",
            "clearance", "depth", "height", "extension", "projection", "embedment", "oversize", "increment", "penetration", "cover",
            "workpoint", "fromweb", "spacing" };
        if (n == "maxspacing" || n == "intermediatemaxspacing" || n == "mindistancefrombearing" || n == "mindistancefromsplice" || n == "maxbaywidth") return ValueKindHint.Long;
        return shortWords.Any(n.Contains) ? ValueKindHint.Short : ValueKindHint.Long;
    }

    /// <summary>Sets a value from text. Returns the formatted new value.</summary>
    public static string SetValue(object root, string path, string text, UnitSystem u)
    {
        var (owner, last) = Owner(root, path);
        if (last.Index != null)
        {
            if (owner is not IList list) throw new ArgumentException("Indexing a value that is not a list.");
            int i = int.Parse(last.Index, CultureInfo.InvariantCulture);
            var elemType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
            list[i] = Convert(text, elemType, "", u);
            return Format(list[i], path, u);
        }
        var prop = Prop(owner, last.Name!);
        if (!prop.CanWrite) throw new ArgumentException($"'{last.Name}' is read-only.");
        var value = Convert(text, prop.PropertyType, prop.Name, u);
        prop.SetValue(owner, value);
        return Format(prop.GetValue(owner), path, u);
    }

    /// <summary>Appends an item (JSON or text) to a list at the path.</summary>
    public static int Add(object root, string path, string text, UnitSystem u)
    {
        if (GetValue(root, path) is not IList list) throw new ArgumentException($"'{path}' is not a list.");
        var elemType = list.GetType().GetGenericArguments()[0];
        string t = text.Trim();
        if (!t.StartsWith('{') && !t.StartsWith('[') && t.Contains('=') && elemType.IsClass && elemType != typeof(string))
        {
            // "Station=26+70; Name=FS3" — create a default item and set its properties with units.
            var item = Activator.CreateInstance(elemType) ?? throw new ArgumentException($"Can't create {elemType.Name}.");
            foreach (var pair in t.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) throw new ArgumentException($"Expected name=value, got '{pair}'.");
                SetValue(item, pair[..eq].Trim(), pair[(eq + 1)..], u);
            }
            list.Add(item);
        }
        else list.Add(Convert(text, elemType, "", u));
        return list.Count - 1;
    }

    public static void Remove(object root, string path)
    {
        var (owner, last) = Owner(root, path);
        if (last.Index == null || owner is not IList list) throw new ArgumentException("Remove needs a list index, e.g. Bridges[0].FieldSplices[1].");
        int i = int.Parse(last.Index, CultureInfo.InvariantCulture);
        list.RemoveAt(i < 0 ? i + list.Count : i);
    }

    private static object? Convert(string text, Type type, string propName, UnitSystem u)
    {
        text = text.Trim();
        var nt = Nullable.GetUnderlyingType(type) ?? type;
        if ((text.StartsWith('{') || text.StartsWith('[')) && nt != typeof(string))
            return JsonSerializer.Deserialize(text, type, Json);
        if (nt == typeof(string)) return text;
        if (nt == typeof(bool)) return text.ToLowerInvariant() is "true" or "yes" or "1" or "on";
        if (nt == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
        if (nt.IsEnum) return Enum.Parse(nt, text.Replace(" ", "").Replace("-", ""), ignoreCase: true);
        if (nt == typeof(double))
        {
            double v;
            bool ok = KindOf(propName) switch
            {
                ValueKindHint.Station => u.TryParseStation(text, out v),
                ValueKindHint.Angle => TryAngle(text, out v),
                ValueKindHint.Slope => UnitSystem.TryParseSlope(text, out v),
                ValueKindHint.Plain => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v),
                ValueKindHint.Short => u.TryParseLength(text, LengthKind.Short, out v),
                _ => u.TryParseLength(text, LengthKind.Long, out v),
            };
            if (!ok) throw new ArgumentException($"Can't read '{text}' for {propName}.");
            return v;
        }
        return JsonSerializer.Deserialize(text, type, Json);
    }

    private static bool TryAngle(string text, out double degrees)
    {
        bool ok = UnitSystem.TryParseAngle(text, out double r);
        degrees = r * 180 / Math.PI;
        return ok;
    }

    /// <summary>Lists the settable members at a path (for discovery).</summary>
    public static IEnumerable<(string name, string type, string value)> Describe(object root, string path, UnitSystem u)
    {
        var obj = string.IsNullOrWhiteSpace(path) ? root : GetValue(root, path);
        if (obj == null) yield break;
        if (obj is IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var name = list[i]?.GetType().GetProperty("Name")?.GetValue(list[i]) as string;
                yield return ($"[{i}]", list[i]?.GetType().Name ?? "null", name ?? "");
            }
            yield break;
        }
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0 || p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
            object? v;
            try { v = p.GetValue(obj); } catch { continue; }
            string shown = v switch
            {
                null => "null",
                IList l => $"list ({l.Count})",
                double or string or bool or int or Enum => Format(v, (string.IsNullOrEmpty(path) ? "" : path + ".") + p.Name, u),
                _ => "{…}",
            };
            yield return (p.Name + (p.CanWrite ? "" : " (read-only)"), p.PropertyType.Name, shown);
        }
    }
}
