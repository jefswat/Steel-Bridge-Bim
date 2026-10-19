using System.Globalization;
using System.Text;

namespace SBM.IO.Database;

public sealed class InspectionImportResult
{
    /// <summary>"element" (AASHTO element-level) or "ratings" (NBI / SNBI component ratings).</summary>
    public string Layout { get; set; } = "";
    public int Rows { get; set; }
    public List<Inspection> Inspections { get; } = new();
    /// <summary>Rows whose element_ref matched no element (stored with an empty GlobalId; kept).</summary>
    public int Unmapped { get; set; }
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Inspection data as CSV (UTF-8, comma separated, header row, column names case-insensitive, any order, extra columns ignored).
/// <para><b>Element-level layout</b> (AASHTO MBEI / SNBI condition states; one row per element total and one per defect):
/// <c>inspection_date</c>*, <c>inspection_kind</c>, <c>structure_number</c>, <c>inspector</c>, <c>element_ref</c> (GlobalId, mark or name of the
/// model element; empty = the structure), <c>element_number</c>* (e.g. 107), <c>element_name</c>, <c>parent_element_number</c> (for protective
/// systems / wearing surfaces, e.g. 515 under 107), <c>defect_number</c> (empty = the element total row, e.g. 1000), <c>defect_name</c>,
/// <c>unit</c> (LF, SF, EA), <c>total_qty</c>, <c>cs1</c>, <c>cs2</c>, <c>cs3</c>, <c>cs4</c>, <c>notes</c>.</para>
/// <para><b>Ratings layout</b> (NBI / SNBI component condition ratings 0-9 or N): <c>inspection_date</c>*, <c>inspection_kind</c>,
/// <c>structure_number</c>, <c>inspector</c>, <c>component</c>* (deck, superstructure, substructure, culvert, channel, ...), <c>rating</c>*,
/// <c>element_ref</c> (empty = whole structure), <c>notes</c>. (* = required.) The layout is detected from the header (<c>element_number</c> vs
/// <c>component</c> + <c>rating</c>). Rows are grouped into inspections by (date, kind, structure number), created when missing; importing the same
/// file again replaces the same rows.</para>
/// </summary>
public static class InspectionCsv
{
    public static readonly string[] ElementColumns =
        { "inspection_date", "inspection_kind", "structure_number", "inspector", "element_ref", "element_number", "element_name", "parent_element_number",
          "defect_number", "defect_name", "unit", "total_qty", "cs1", "cs2", "cs3", "cs4", "notes" };
    public static readonly string[] RatingColumns = { "inspection_date", "inspection_kind", "structure_number", "inspector", "component", "rating", "element_ref", "notes" };

    // ------------------------------------------------------------------ CSV basics

    public static List<List<string>> Read(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var f = new StringBuilder();
        bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { f.Append('"'); i++; } else q = false; }
                else f.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': q = true; break;
                case ',': row.Add(f.ToString()); f.Clear(); break;
                case '\r': break;
                case '\n': row.Add(f.ToString()); f.Clear(); if (row.Any(x => x.Length > 0)) rows.Add(row); row = new List<string>(); break;
                default: f.Append(c); break;
            }
        }
        row.Add(f.ToString());
        if (row.Any(x => x.Length > 0)) rows.Add(row);
        return rows;
    }

    public static string Field(string? v)
    {
        v ??= "";
        return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    public static string Line(IEnumerable<string?> fields) => string.Join(",", fields.Select(Field));

    private static string Num(double? v) => v is { } d ? d.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private static double? ParseNum(string s, string col, int line)
    {
        s = s.Trim();
        if (s.Length == 0) return null;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : throw new FormatException($"line {line}: {col} '{s}' is not a number.");
    }

    // ------------------------------------------------------------------ import

    public static InspectionImportResult Import(ModelDb db, string csvFile, string? defaultKind = null, string? by = null)
    {
        var rows = Read(File.ReadAllText(csvFile, Encoding.UTF8).TrimStart('﻿'));
        if (rows.Count == 0) throw new FormatException($"{csvFile} is empty.");
        var head = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(string n) => head.IndexOf(n);
        var res = new InspectionImportResult();
        bool ratings = Col("component") >= 0 && Col("rating") >= 0;
        if (!ratings && Col("element_number") < 0)
            throw new FormatException($"{csvFile}: header needs element_number (element-level layout) or component + rating (ratings layout). See docs/API.md.");
        if (Col("inspection_date") < 0) throw new FormatException($"{csvFile}: header needs inspection_date.");
        res.Layout = ratings ? "ratings" : "element";
        var refs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var insp = new Dictionary<string, Inspection>();
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            int line = i + 1;
            string V(string n) { int c = Col(n); return c >= 0 && c < r.Count ? r[c].Trim() : ""; }
            string date = V("inspection_date");
            if (date.Length == 0) throw new FormatException($"line {line}: inspection_date is empty.");
            string kind = V("inspection_kind") is { Length: > 0 } k ? k : defaultKind ?? "routine";
            string sn = V("structure_number");
            string key = ModelDb.NormDate(date) + "|" + kind.ToLowerInvariant() + "|" + sn;
            if (!insp.TryGetValue(key, out var ins))
            {
                ins = db.AddInspection(date, kind, sn, V("inspector") is { Length: > 0 } who ? who : null, ratings ? "NBI/SNBI ratings" : "AASHTO element-level", by: by);
                insp[key] = ins;
                res.Inspections.Add(ins);
            }
            string eref = V("element_ref");
            string gid = "";
            if (eref.Length > 0)
            {
                if (!refs.TryGetValue(eref, out var g))
                {
                    g = db.TryFindElement(eref) ?? "";
                    refs[eref] = g;
                    if (g.Length == 0) res.Warnings.Add($"line {line}: element_ref '{eref}' matches no single element of the model: stored unmapped (kept).");
                }
                gid = g;
                if (gid.Length == 0) res.Unmapped++;
            }
            if (ratings)
            {
                try { db.SetRating(ins.Id, V("component"), V("rating"), gid, V("notes") is { Length: > 0 } n ? n : null); }
                catch (ArgumentException ex) { throw new FormatException($"line {line}: {ex.Message}"); }
            }
            else
            {
                string en = V("element_number");
                if (en.Length == 0) throw new FormatException($"line {line}: element_number is empty.");
                var c = new ConditionRow
                {
                    GlobalId = gid, ElementRef = eref, ElementNumber = en, ElementName = V("element_name"), ParentElementNumber = V("parent_element_number"),
                    DefectNumber = V("defect_number"), DefectName = V("defect_name"), Unit = V("unit"), TotalQty = ParseNum(V("total_qty"), "total_qty", line),
                    Cs1 = ParseNum(V("cs1"), "cs1", line), Cs2 = ParseNum(V("cs2"), "cs2", line), Cs3 = ParseNum(V("cs3"), "cs3", line), Cs4 = ParseNum(V("cs4"), "cs4", line),
                    Note = V("notes"),
                };
                double sum = (c.Cs1 ?? 0) + (c.Cs2 ?? 0) + (c.Cs3 ?? 0) + (c.Cs4 ?? 0);
                if (c.DefectNumber.Length == 0 && c.TotalQty is { } tq && Math.Abs(sum - tq) > Math.Max(0.01, 0.005 * tq))
                    res.Warnings.Add($"line {line}: element {en}: CS1-CS4 add up to {sum:0.##}, total quantity is {tq:0.##}.");
                db.UpsertCondition(ins.Id, c);
            }
            res.Rows++;
        }
        return res;
    }

    // ------------------------------------------------------------------ export

    /// <summary>Writes the inspection history: <paramref name="csvFile"/> = element-level rows (import layout + global_id, element_model_name),
    /// <c>&lt;name&gt;_ratings.csv</c> = component ratings, <c>&lt;name&gt;_findings.csv</c> = findings. Returns the files written.</summary>
    public static List<string> ExportHistory(ModelDb db, string csvFile)
    {
        var files = new List<string>();
        var insp = db.Inspections().ToDictionary(i => i.Id);
        string Lbl(string gid) => gid.Length == 0 ? "" : db.Get(gid) is { } e ? (e.Name.Length > 0 ? e.Name : e.Category) : "(not in the model)";
        var sb = new StringBuilder();
        sb.AppendLine(Line(ElementColumns.Concat(new[] { "global_id", "element_model_name" })));
        foreach (var c in db.Condition().OrderBy(c => c.InspectionDate).ThenBy(c => c.ElementRef).ThenBy(c => c.ElementNumber).ThenBy(c => c.ParentElementNumber).ThenBy(c => c.DefectNumber))
        {
            var i = insp[c.InspectionId];
            sb.AppendLine(Line(new[] { i.Date, i.Kind, i.StructureNumber, i.Inspector, c.ElementRef, c.ElementNumber, c.ElementName, c.ParentElementNumber, c.DefectNumber,
                c.DefectName, c.Unit, Num(c.TotalQty), Num(c.Cs1), Num(c.Cs2), Num(c.Cs3), Num(c.Cs4), c.Note, c.GlobalId, Lbl(c.GlobalId) }));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(csvFile))!);
        File.WriteAllText(csvFile, sb.ToString(), new UTF8Encoding(true));
        files.Add(csvFile);

        string bas = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(csvFile))!, Path.GetFileNameWithoutExtension(csvFile));
        sb.Clear();
        sb.AppendLine(Line(RatingColumns.Concat(new[] { "global_id", "element_model_name" })));
        foreach (var r in db.Ratings().OrderBy(r => r.InspectionDate).ThenBy(r => r.Component))
        {
            var i = insp[r.InspectionId];
            string eref = r.GlobalId.Length > 0 ? db.Get(r.GlobalId)?.Mark is { Length: > 0 } m ? m : r.GlobalId : "";
            sb.AppendLine(Line(new[] { i.Date, i.Kind, i.StructureNumber, i.Inspector, r.Component, r.Rating, eref, r.Note, r.GlobalId, Lbl(r.GlobalId) }));
        }
        File.WriteAllText(bas + "_ratings.csv", sb.ToString(), new UTF8Encoding(true));
        files.Add(bas + "_ratings.csv");

        sb.Clear();
        sb.AppendLine(Line(new[] { "id", "found_at", "inspection_date", "global_id", "element_model_name", "description", "defect_type", "severity", "location_text",
            "x", "y", "z", "station_m", "offset_m", "recommended_action", "status", "due_date", "closed_at", "created_by", "updated_at", "notes", "photos" }));
        foreach (var f in db.Findings().OrderBy(f => f.FoundAt).ThenBy(f => f.Id))
        {
            string idate = f.InspectionId is { } ii && insp.TryGetValue(ii, out var fi) ? fi.Date : "";
            var photos = db.Files(null, "finding", f.Id.ToString(CultureInfo.InvariantCulture)).Select(p => p.Location);
            sb.AppendLine(Line(new[] { f.Id.ToString(CultureInfo.InvariantCulture), f.FoundAt, idate, f.GlobalId, Lbl(f.GlobalId), f.Description, f.DefectType, f.Severity,
                f.LocationText, Num(f.X), Num(f.Y), Num(f.Z), Num(f.Station), Num(f.Offset), f.RecommendedAction, f.Status, f.DueDate, f.ClosedAt, f.CreatedBy, f.UpdatedAt, f.Note,
                string.Join(" ", photos) }));
        }
        File.WriteAllText(bas + "_findings.csv", sb.ToString(), new UTF8Encoding(true));
        files.Add(bas + "_findings.csv");
        return files;
    }
}
