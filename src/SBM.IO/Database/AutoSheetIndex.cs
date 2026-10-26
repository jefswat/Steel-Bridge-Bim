using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SBM.IO.Database;

/// <summary>
/// MACHINE-made sheet index of a plan set (<c>tools/plan_index_auto.py</c>, run by <c>sbm pipeline</c>): <c>sheet-index-&lt;plan set id&gt;.auto.json</c> beside the
/// link rules file. The hand-curated index in the rules file (<see cref="PlanSetSpec.Sheets"/>) is authoritative: the automatic one only fills a plan set
/// that has no curated index, or pages the curated index lacks (<see cref="Merge"/>). The pipeline never edits a rules file it did not create.
/// </summary>
public static class AutoSheetIndex
{
    public sealed class Result
    {
        public string Pdf { get; set; } = "";
        public int PageCount { get; set; }
        public int TextPages { get; set; }
        public int RasterPages { get; set; }
        public bool OcrAvailable { get; set; }
        public int OcrPages { get; set; }
        public int Empty { get; set; }
        public int LowConfidence { get; set; }
        public List<SheetIndexEntry> Sheets { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static string Slug(string s)
    {
        string t = Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return t.Length == 0 ? "plans" : t.Length > 60 ? t[..60].TrimEnd('-') : t;
    }

    /// <summary>The automatic index file of plan set <paramref name="planSetId"/> in <paramref name="folder"/> (the rules file's folder).</summary>
    public static string PathFor(string folder, string planSetId) => Path.Combine(folder, $"sheet-index-{Slug(planSetId)}.auto.json");

    public static Result? Load(string file)
    {
        if (!File.Exists(file)) return null;
        try { return JsonSerializer.Deserialize<Result>(File.ReadAllText(file), Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Curated entries win page by page; automatic entries are added only for pages the curated index does not list. Sorted by page.</summary>
    public static (List<SheetIndexEntry> merged, int added) Merge(IReadOnlyList<SheetIndexEntry>? curated, IReadOnlyList<SheetIndexEntry>? auto)
    {
        var list = new List<SheetIndexEntry>(curated ?? Array.Empty<SheetIndexEntry>());
        var pages = new HashSet<int>(list.Select(e => e.Page));
        int added = 0;
        foreach (var e in auto ?? Array.Empty<SheetIndexEntry>())
            if (e.Page > 0 && pages.Add(e.Page)) { list.Add(new SheetIndexEntry { Page = e.Page, Sheet = e.Sheet ?? "", Title = e.Title ?? "" }); added++; }
        list.Sort((a, b) => a.Page.CompareTo(b.Page));
        return (list, added);
    }

    /// <summary>Skeleton link rules file for a model whose plan set has no rules yet: models, plan folders, plan sets (the sheet index lives in the
    /// automatic index files beside it), NO rules. A person / agent adds the rules (and may paste a corrected index inline, which then wins).</summary>
    public static string Skeleton(string modelFile, IEnumerable<(string id, string pdfPattern, string bridgeGroup)> sets, IEnumerable<string> planFolders, DateTime when)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  // SKELETON written by `sbm pipeline` {when:yyyy-MM-dd} for {Path.GetFileName(modelFile)}. The sheet index of each plan set is in");
        sb.AppendLine("  // sheet-index-<id>.auto.json beside this file (machine-made from the PDF text layer / OCR; low-confidence pages flagged \"q\": \"low\").");
        sb.AppendLine("  // TODO: add link rules (\"rules\": title regex -> element selectors, see sheet-links-i80-rampg.json and docs/API.md \"Companion database\"),");
        sb.AppendLine("  // \"administrative\" titles, and check bridgeGroup (= elements.bridge_group). This file is yours now: the pipeline never rewrites it.");
        sb.AppendLine($"  \"models\": [ {JsonSerializer.Serialize(Path.GetFileName(modelFile))} ],");
        sb.AppendLine($"  \"planFolders\": [ {string.Join(", ", planFolders.Distinct(StringComparer.OrdinalIgnoreCase).Select(f => JsonSerializer.Serialize(f.Replace('\\', '/'))))} ],");
        sb.AppendLine("  \"planSets\": [");
        var s = sets.ToList();
        for (int i = 0; i < s.Count; i++)
            sb.AppendLine($"    {{ \"id\": {JsonSerializer.Serialize(s[i].id)}, \"pdf\": {JsonSerializer.Serialize(s[i].pdfPattern)}, \"bridgeGroup\": {JsonSerializer.Serialize(s[i].bridgeGroup)} }}{(i < s.Count - 1 ? "," : "")}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"administrative\": [ \"TITLE SHEET\", \"REVISION\", \"INDEX OF SHEETS\" ],");
        sb.AppendLine("  \"rules\": []");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
