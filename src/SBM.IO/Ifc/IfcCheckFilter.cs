using System.Text.RegularExpressions;

namespace SBM.IO.Ifc;

/// <summary>
/// CHECKING export (<see cref="IfcExportOptions.ForCheck"/>): an entity filter applied by <see cref="StepWriter"/> to every entity as it is
/// written, so the IFC handed to an independent checker carries the model (geometry, placements, GlobalIds, classes, materials, measured
/// quantities) but not the originator's reasoning: free-text descriptions, plan / page references, size strings, UNRESOLVED / assumption
/// reasons and bases, fabrication notes, sources. Geometry entities are never touched. Normal exports do not use this class (byte-identical).
/// The list of what is stripped is in docs/API.md ("Checking export"); tools/check_ifc_leaks.py verifies a file.
/// </summary>
public sealed class IfcCheckFilter
{
    /// <summary>Text that cites the plans or states the originator's judgement (page references, sheets, notes, UNRESOLVED, scaled, assumed...).</summary>
    public static readonly Regex JudgementLeak = new(
        @"\bpp?\.\s*\d|\bpdf\b|\bsheets?\b|\bnotes?\b|\bUNRESOLVED\b|\bscaled\b|\bassum|\bplans?\b|\bvectors?\b|\binferred\b|\bderived\b|\bnot given\b|\bnot shown\b|\bsee\b|\bdetail [A-Z0-9]\b|\bOT\d|\brev\.?\s*\d|\(\d{2}-\d{4}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Inch / fraction size strings (7/8", 1 3/4", 600 3/8", 20").</summary>
    public static readonly Regex SizeString = new("\\d+\\s*/\\s*\\d+|\\d\\s*(?:\"|″|\\bin\\b|\\binch)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Rolled-section / plate designations (WT 7x26.5, L 4x4x1/2, PL 3/4 x 12).</summary>
    public static readonly Regex Designation = new(@"\b(?:WT|W|HP|MC|C|L|PL|HSS|TS|S|M)\s?\d+(?:\.\d+)?\s?[xX×]\s?\d", RegexOptions.Compiled);

    /// <summary>Properties that restate plan notes / the fabrication specification (checkable claims, not measurements): dropped unless KeepDesignations.</summary>
    private static readonly HashSet<string> ClaimProps = new(StringComparer.Ordinal)
        { "Toughness", "ContactFinish", "NDT", "CoatingExterior", "CoatingInterior", "ShopAssembly", "Tolerance", "Camber", "Weld", "Size" };

    /// <summary>Properties always dropped: reasons, bases, sources, plan notes, plan quantities (the plan value is the checker's to read).</summary>
    private static readonly HashSet<string> AlwaysDropProps = new(StringComparer.Ordinal)
        { "Reason", "Basis", "Source", "PlanNotes", "QuantityItem", "Products", "Pattern", "Treatment", "Anchors" };

    private readonly bool _keepDesignations;
    private readonly HashSet<int> _dropped = new();

    public IfcCheckFilter(bool keepDesignations = false) => _keepDesignations = keepDesignations;

    /// <summary>Entities not written (property values, property sets and their relationships) and strings blanked, for the export summary.</summary>
    public int DroppedEntities { get; private set; }
    public int BlankedStrings { get; private set; }

    /// <summary>Returns the (possibly rewritten) attribute text, or null when the entity is not written at all.</summary>
    public string? Filter(int id, string type, string body)
    {
        switch (type)
        {
            case "IFCPROPERTYSINGLEVALUE": return Prop(id, body);
            case "IFCPROPERTYSET": return Pset(id, body);
            case "IFCRELDEFINESBYPROPERTIES":
            {
                // ...,(related),#pset : dropped with its property set.
                int h = body.LastIndexOf('#');
                if (h >= 0 && int.TryParse(body.AsSpan(h + 1), out int ps) && _dropped.Contains(ps)) return Drop(id);
                return body;
            }
            case "IFCMATERIAL": return Material(Attrs(body, desc: 1, categoryIdx: 2));
            case "IFCPROJECTEDCRS": return Attrs(body, desc: 1, categoryIdx: -1);
            case "IFCSURFACESTYLE": return Attrs(body, desc: -1, categoryIdx: -1, nameIdx: 0);
        }
        // IfcRoot subclasses: the first attribute is a 22-character GlobalId.
        if (body.Length > 25 && body[0] == '\'' && body[23] == '\'' && body[24] == ',') return Root(type, body);
        return body;
    }

    private string? Drop(int id) { _dropped.Add(id); DroppedEntities++; return null; }

    // ---------------------------------------------------------------- IfcRoot: Name / Description / ObjectType / other strings

    private string Root(string type, string body)
    {
        var a = Split(body);
        if (a.Count < 4) return body;
        bool changed = false;
        // Description: always removed (free text that states sizes / reasons / sources).
        if (a[3] != "$") { a[3] = "$"; changed = true; }
        // Name: a neutral identifier. Text that cites plans or states a size / judgement becomes the GlobalId.
        if (a[2] != "$" && IsLeak(Str(a[2]))) { a[2] = a[0]; changed = true; BlankedStrings++; }
        // ObjectType / ApplicableOccurrence (class refinements like 'END_DIAPHRAGM' stay), Tag / LongName / ElementType: leaks are blanked.
        for (int i = 4; i < a.Count; i++)
        {
            if (a[i].Length < 2 || a[i][0] != '\'' || !IsLeak(Str(a[i]))) continue;
            // A USERDEFINED predefined type must keep a (neutral) ObjectType (IFC rule CorrectPredefinedType).
            a[i] = i == 7 && !type.EndsWith("TYPE", StringComparison.Ordinal) ? a[0]
                : i == 4 && a[^1] == ".USERDEFINED." ? "'USERDEFINED'" : "$";
            changed = true; BlankedStrings++;
        }
        return changed ? string.Join(",", a) : body;
    }

    /// <summary>Material NAME only: remarks in parentheses ("(p.83)", "(placeholder, A36 minimum per plans)") are removed.</summary>
    private string Material(string body)
    {
        var a = Split(body);
        if (a.Count == 0 || a[0].Length < 2 || a[0][0] != '\'' || !IsLeak(Str(a[0]))) return body;
        string name = Regex.Replace(Str(a[0]), @"\s*\([^)]*\)", "").Trim();
        if (name.Length == 0 || IsLeak(name)) name = "Material";
        a[0] = StepWriter.S(name);
        BlankedStrings++;
        return string.Join(",", a);
    }

    private string Attrs(string body, int desc, int categoryIdx, int nameIdx = -1)
    {
        var a = Split(body);
        bool changed = false;
        if (desc >= 0 && desc < a.Count && a[desc] != "$") { a[desc] = "$"; changed = true; }
        if (categoryIdx >= 0 && categoryIdx < a.Count && a[categoryIdx].StartsWith('\'') && IsLeak(Str(a[categoryIdx]))) { a[categoryIdx] = "$"; changed = true; BlankedStrings++; }
        if (nameIdx >= 0 && nameIdx < a.Count && a[nameIdx].StartsWith('\'') && IsLeak(Str(a[nameIdx]))) { a[nameIdx] = "$"; changed = true; BlankedStrings++; }
        return changed ? string.Join(",", a) : body;
    }

    // ---------------------------------------------------------------- properties

    private string? Prop(int id, string body)
    {
        var a = Split(body);
        if (a.Count < 3) return body;
        string name = Str(a[0]);
        string value = a[2];
        if (AlwaysDropProps.Contains(name) || name.StartsWith("Plan", StringComparison.Ordinal)) return Drop(id);
        // Flagged, not why (Reason / Basis are dropped).
        if (name == "Status") return body.Replace(value, "IFCLABEL('FLAGGED')");
        string text = Str(value);
        if (ClaimProps.Contains(name))
        {
            if (!_keepDesignations) return Drop(id);
            // Designations stay (WT 7x26.5); a plan size string or a page citation never does.
            return JudgementLeak.IsMatch(text) || SizeString.IsMatch(text) ? Drop(id) : body;
        }
        return text.Length > 0 && IsLeak(text) ? Drop(id) : body;
    }

    private string? Pset(int id, string body)
    {
        var a = Split(body);
        if (a.Count < 5) return body;
        int before = Regex.Matches(a[4], @"#\d+").Count;
        var refs = Regex.Matches(a[4], @"#(\d+)").Select(m => int.Parse(m.Groups[1].Value)).Where(r => !_dropped.Contains(r)).ToList();
        if (refs.Count == 0) return Drop(id);
        if (refs.Count == before) return body;
        a[4] = "(" + string.Join(",", refs.Select(r => "#" + r)) + ")";
        return string.Join(",", a);
    }

    // ---------------------------------------------------------------- text helpers

    /// <summary>True when a string cites a page / plan or states a size or judgement.</summary>
    public static bool IsLeak(string s) => s.Length > 0 && (JudgementLeak.IsMatch(s) || SizeString.IsMatch(s) || Designation.IsMatch(s));

    /// <summary>The text of a STEP attribute: IFCLABEL('x') / IFCTEXT('x') / 'x'; empty when it holds no string.</summary>
    private static string Str(string attr)
    {
        int q = attr.IndexOf('\'');
        if (q < 0) return "";
        int e = attr.LastIndexOf('\'');
        return e > q ? attr.Substring(q + 1, e - q - 1).Replace("''", "'") : "";
    }

    /// <summary>Top-level attributes of a STEP entity body (quotes and parentheses respected).</summary>
    internal static List<string> Split(string body)
    {
        var list = new List<string>();
        int depth = 0, start = 0;
        bool inStr = false;
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (inStr) { if (c == '\'') { if (i + 1 < body.Length && body[i + 1] == '\'') i++; else inStr = false; } continue; }
            if (c == '\'') inStr = true;
            else if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0) { list.Add(body.Substring(start, i - start)); start = i + 1; }
        }
        list.Add(body.Substring(start));
        return list;
    }
}
