using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SBM.IO.Database;

/// <summary>One plan set (a PDF) named in the link rules file.</summary>
public sealed class PlanSetSpec
{
    public string Id { get; set; } = "";
    /// <summary>File name or glob (e.g. <c>B00168*Design 116.pdf</c>) searched in the plan folders.</summary>
    public string Pdf { get; set; } = "";
    /// <summary>Value of <c>elements.bridge_group</c> for the structures this plan set describes (also the alignment name).</summary>
    public string BridgeGroup { get; set; } = "";
}

public sealed class LinkRule
{
    public string Id { get; set; } = "";
    /// <summary>Regex on the normalized sheet title (named groups become <c>{name}</c> placeholders in the targets).</summary>
    public string Sheets { get; set; } = "";
    public List<string>? PlanSets { get; set; }
    public string Role { get; set; } = "detail";
    public string Basis { get; set; } = "";
    /// <summary>OR of selectors; a selector is field → value (string with {captures} and % wildcards, or a list of those).</summary>
    public List<Dictionary<string, JsonElement>> Target { get; set; } = new();
}

public sealed class SheetHint
{
    public string Sheets { get; set; } = "";
    public string Suggests { get; set; } = "";
}

public sealed class LinkRules
{
    public List<PlanSetSpec> PlanSets { get; set; } = new();
    public List<LinkRule> Rules { get; set; } = new();
    /// <summary>Title regexes of sheets that need no model element (cover, revision sheet).</summary>
    public List<string> Administrative { get; set; } = new();
    public List<SheetHint> Hints { get; set; } = new();

    public static LinkRules Load(string path) => JsonSerializer.Deserialize<LinkRules>(File.ReadAllText(path), new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    }) ?? new LinkRules();
}

public sealed class LinkResult
{
    public int Sheets, Links, SheetsLinked, SheetsUnused;
    public Dictionary<string, int> LinksByRule { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>Plan sheets of the database: splitting PDFs into single pages, the sheet table, and the rule-based element → sheet links.</summary>
public static class PlanSheets
{
    /// <summary>Runs <c>tools/plan_split.py</c> (PyMuPDF) to split a PDF into single-page PDFs + JPEG previews in <paramref name="outDir"/>.</summary>
    public static void SplitPdf(string pdf, string outDir, string script, bool previews = true, Action<string>? log = null)
    {
        var psi = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-I"); psi.ArgumentList.Add(script); psi.ArgumentList.Add(pdf); psi.ArgumentList.Add(outDir);
        if (!previews) psi.ArgumentList.Add("--no-preview");
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start python.");
        string o = p.StandardOutput.ReadToEnd(), e = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"plan_split.py failed: {e.Trim()}");
        log?.Invoke(o.Trim());
    }

    public static string? FindPdf(PlanSetSpec spec, IEnumerable<string> folders)
    {
        foreach (var dir in folders)
        {
            if (!Directory.Exists(dir)) continue;
            var hit = Directory.EnumerateFiles(dir, spec.Pdf.Contains('*') || spec.Pdf.Contains('?') ? spec.Pdf : spec.Pdf).FirstOrDefault();
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>Reads <c>sheets.json</c> written by the split tool into the <c>sheets</c> table. <paramref name="relDir"/> is the page folder relative to the database file.</summary>
    public static int ImportSheets(SqliteConnection cn, string planSet, string sheetsJson, string relDir)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(sheetsJson));
        string pdf = doc.RootElement.GetProperty("pdf_file").GetString() ?? "";
        int n = 0;
        ModelDatabase.Exec(cn, "BEGIN");
        using var del = cn.CreateCommand();
        del.CommandText = "DELETE FROM sheets WHERE plan_set=$s";
        del.Parameters.AddWithValue("$s", planSet);
        del.ExecuteNonQuery();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO sheets(sheet_id,plan_set,pdf_file,pdf_page,design_sheet,title,page_file,preview_file) VALUES($id,$ps,$pdf,$pg,$ds,$t,$pf,$pv)";
        foreach (var s in doc.RootElement.GetProperty("sheets").EnumerateArray())
        {
            int page = s.GetProperty("pdf_page").GetInt32();
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", SheetId(planSet, page));
            cmd.Parameters.AddWithValue("$ps", planSet);
            cmd.Parameters.AddWithValue("$pdf", pdf);
            cmd.Parameters.AddWithValue("$pg", page);
            cmd.Parameters.AddWithValue("$ds", s.GetProperty("design_sheet").GetString() ?? "");
            cmd.Parameters.AddWithValue("$t", s.GetProperty("title").GetString() ?? "");
            cmd.Parameters.AddWithValue("$pf", relDir.TrimEnd('/', '\\').Replace('\\', '/') + "/" + s.GetProperty("page_file").GetString());
            string prev = s.GetProperty("preview_file").GetString() ?? "";
            cmd.Parameters.AddWithValue("$pv", prev.Length == 0 ? DBNull.Value : relDir.TrimEnd('/', '\\').Replace('\\', '/') + "/" + prev);
            cmd.ExecuteNonQuery();
            n++;
        }
        ModelDatabase.Exec(cn, "COMMIT");
        return n;
    }

    public static string SheetId(string planSet, int page) => $"{planSet}:p{page:000}";

    // ================================================================== link rules

    private static readonly string[] Fields = { "level", "category", "subtype", "bridge", "bridge_group", "unit", "support", "girder", "mark", "name" };

    private static string Expand(string template, Match m)
    {
        foreach (Group g in m.Groups)
            if (!string.IsNullOrEmpty(g.Name) && !char.IsDigit(g.Name[0])) template = template.Replace("{" + g.Name + "}", g.Value);
        return template;
    }

    /// <summary>Applies the rules to the sheets and elements already in the database (replacing earlier links).</summary>
    public static LinkResult ApplyRules(SqliteConnection cn, LinkRules rules, string sourceName)
    {
        var res = new LinkResult();
        var sheets = new List<(string id, string set, int page, string design, string title)>();
        using (var q = cn.CreateCommand())
        {
            q.CommandText = "SELECT sheet_id, plan_set, pdf_page, design_sheet, title FROM sheets ORDER BY plan_set, pdf_page";
            using var r = q.ExecuteReader();
            while (r.Read()) sheets.Add((r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4)));
        }
        res.Sheets = sheets.Count;
        var sets = rules.PlanSets.ToDictionary(p => p.Id);
        var adminRx = rules.Administrative.Select(a => new Regex(a, RegexOptions.IgnoreCase)).ToList();
        ModelDatabase.Exec(cn, "BEGIN");
        ModelDatabase.Exec(cn, "DELETE FROM element_sheet");
        ModelDatabase.Exec(cn, "UPDATE sheets SET kind=NULL, note=NULL");
        foreach (var s in sheets)
            if (adminRx.Any(a => a.IsMatch(s.title)))
                ModelDatabase.Exec(cn, "UPDATE sheets SET kind='administrative' WHERE sheet_id=$id", ("$id", s.id));

        var ruleHit = rules.Rules.ToDictionary(r => r.Id, _ => 0);
        foreach (var rule in rules.Rules)
        {
            var rx = new Regex(rule.Sheets, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach (var s in sheets)
            {
                if (rule.PlanSets != null && !rule.PlanSets.Contains(s.set)) continue;
                var m = rx.Match(s.title);
                if (!m.Success) continue;
                sets.TryGetValue(s.set, out var set);
                int before = res.LinksByRule.GetValueOrDefault(rule.Id);
                foreach (var target in rule.Target)
                {
                    var ps = new List<(string, object?)>();
                    string where = BuildWhere(target, m, set, "e", ps, out string whereP);
                    string sql = $"""
                        INSERT INTO element_sheet(global_id, sheet_id, role, basis, source)
                        SELECT e.global_id, $sheet, $role, $basis, $src FROM elements e
                        WHERE {where} AND NOT EXISTS (SELECT 1 FROM elements p WHERE p.global_id = e.parent_global_id AND {whereP})
                        ON CONFLICT(global_id, sheet_id) DO UPDATE SET role = excluded.role, basis = excluded.basis, source = excluded.source WHERE excluded.role = 'detail' AND element_sheet.role <> 'detail'
                        """;
                    ps.Add(("$sheet", s.id)); ps.Add(("$role", rule.Role)); ps.Add(("$basis", Expand(rule.Basis, m))); ps.Add(("$src", $"{sourceName}#{rule.Id}"));
                    using var c = cn.CreateCommand();
                    c.CommandText = sql;
                    foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
                    int added = c.ExecuteNonQuery();
                    ruleHit[rule.Id] += added;
                    res.LinksByRule[rule.Id] = res.LinksByRule.GetValueOrDefault(rule.Id) + added;
                }
                if (res.LinksByRule.GetValueOrDefault(rule.Id) == before && rule.Target.Count > 0)
                    ModelDatabase.Exec(cn, "UPDATE sheets SET note = COALESCE(note || ' ', '') || $n WHERE sheet_id = $id",
                        ("$n", $"Rule '{rule.Id}' ({Expand(rule.Basis, m)}) matches this sheet, but the model has no such element: it is missing from the model."), ("$id", s.id));
            }
        }
        foreach (var (id, n) in ruleHit)
            if (n == 0) res.Warnings.Add($"rule '{id}' linked no element (no matching sheet or element)");
        ModelDatabase.Exec(cn, "COMMIT");
        res.Links = ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM element_sheet");
        res.SheetsLinked = ModelDatabase.Scalar<int>(cn, "SELECT COUNT(DISTINCT sheet_id) FROM element_sheet");
        res.SheetsUnused = ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM v_unused_sheets");
        return res;
    }

    private static string BuildWhere(Dictionary<string, JsonElement> sel, Match m, PlanSetSpec? set, string alias, List<(string, object?)> ps, out string wherePrimed)
    {
        var parts = new List<string>();
        var partsP = new List<string>();
        int i = 0;
        var fields = sel.ToDictionary(k => k.Key.ToLowerInvariant(), k => k.Value);
        if (!fields.ContainsKey("bridge_group") && set != null && set.BridgeGroup.Length > 0)
            fields["bridge_group"] = JsonDocument.Parse(JsonSerializer.Serialize(set.BridgeGroup)).RootElement.Clone();
        foreach (var (field, val) in fields)
        {
            if (!Fields.Contains(field)) throw new InvalidDataException($"Unknown selector field '{field}'.");
            var values = val.ValueKind == JsonValueKind.Array ? val.EnumerateArray().Select(v => v.GetString() ?? "").ToList() : new List<string> { val.GetString() ?? "" };
            if (values.Count == 1 && values[0] == "*") continue;
            var ors = new List<string>();
            foreach (var v0 in values)
            {
                string v = Expand(v0, m);
                string pn = $"$s{ps.Count}_{i++}";
                ps.Add((pn, v));
                ors.Add(v.Contains('%') ? $"{{A}}.{field} LIKE {pn}" : $"{{A}}.{field} = {pn} COLLATE NOCASE");
            }
            string expr = "(" + string.Join(" OR ", ors) + ")";
            parts.Add(expr.Replace("{A}", alias));
            partsP.Add(expr.Replace("{A}", "p"));
        }
        if (parts.Count == 0) { wherePrimed = "1=1"; return "1=1"; }
        wherePrimed = string.Join(" AND ", partsP);
        return string.Join(" AND ", parts);
    }

    // ================================================================== reports

    /// <summary>Markdown report of sheets that no element is based on, grouped by title, with what each suggests is missing from the model.</summary>
    public static string UnusedReport(SqliteConnection cn, LinkRules rules, string modelName)
    {
        var hints = rules.Hints.Select(h => (rx: new Regex(h.Sheets, RegexOptions.IgnoreCase), h.Suggests)).ToList();
        var rows = new List<(string set, int page, string design, string title)>();
        var notes = new List<string>();
        using (var q = cn.CreateCommand())
        {
            q.CommandText = "SELECT plan_set, pdf_page, design_sheet, title, COALESCE(note,'') FROM v_unused_sheets ORDER BY title, plan_set, pdf_page";
            using var r = q.ExecuteReader();
            while (r.Read()) { rows.Add((r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetString(3))); notes.Add(r.GetString(4)); }
        }
        int total = ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM sheets");
        int admin = ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM sheets WHERE kind='administrative'");
        var sb = new StringBuilder();
        sb.AppendLine($"# Plan sheets not linked to any model element ({modelName})");
        sb.AppendLine();
        sb.AppendLine("Generated by `sbm db ... --links ...` from the companion database (view `v_unused_sheets`). A sheet appears here when");
        sb.AppendLine("no element of the model is based on it (no row in `element_sheet`). Such a sheet either describes something the");
        sb.AppendLine("model definition does not contain yet (the \"Suggests\" column), or the link rules need a rule for it");
        sb.AppendLine("(`samples/plans/sheet-links-i80-rampg.json`). \"Design\" = design sheet number, \"PDF\" = PDF page.");
        sb.AppendLine();
        sb.AppendLine($"{rows.Count} of {total} sheets are unlinked ({admin} administrative sheets - cover, revision - are excluded).");
        sb.AppendLine();
        sb.AppendLine("| Sheet title | Sheets (set: design sheet / PDF page) | Suggests missing from the model |");
        sb.AppendLine("|---|---|---|");
        string Norm(string t) => Regex.Replace(Regex.Replace(Regex.Replace(t, @"\s+-\s+UNIT\s+\d+", " - UNIT n", RegexOptions.IgnoreCase), @"\s*/\s*REVISED:?\s*$", ""), @"AND", "&");
        var withNote = rows.Select((r, i) => (r, note: notes[i]));
        foreach (var g in withNote.GroupBy(x => Norm(x.r.title)))
        {
            var list = string.Join("; ", g.GroupBy(x => x.r.set).Select(s =>
                $"{s.Key}: " + string.Join(", ", s.Select(x => (x.r.design.Length > 0 ? x.r.design : "-") + "/" + x.r.page))));
            string hint = hints.FirstOrDefault(h => h.rx.IsMatch(g.Key)).Suggests ?? "";
            string note = g.Select(x => x.note).FirstOrDefault(n => n.Length > 0) ?? "";
            hint = (hint + " " + note).Trim();
            if (hint.Length == 0) hint = "(no hint; check whether a link rule is missing)";
            sb.AppendLine($"| {g.Key} | {list} | {hint} |");
        }
        return sb.ToString();
    }

    /// <summary>Coverage statistics: elements with at least one linked sheet (own or inherited from an ancestor), by level and role.</summary>
    public static string CoverageReport(SqliteConnection cn)
    {
        var sb = new StringBuilder();
        void Q(string title, string sql)
        {
            sb.AppendLine(title);
            using var c = cn.CreateCommand();
            c.CommandText = sql;
            using var r = c.ExecuteReader();
            while (r.Read())
            {
                var cells = new List<string>();
                for (int i = 0; i < r.FieldCount; i++) cells.Add(Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "");
                sb.AppendLine("  " + string.Join(" | ", cells));
            }
        }
        sb.AppendLine("COVERAGE");
        // Propagate only a flag down the hierarchy (any link / detail link), not every (element, sheet) pair.
        ModelDatabase.Exec(cn, "DROP TABLE IF EXISTS temp.cov");
        ModelDatabase.Exec(cn, """
            CREATE TEMP TABLE cov AS
            WITH RECURSIVE c(gid, det) AS (
              SELECT global_id, MAX(role = 'detail') FROM element_sheet GROUP BY global_id
              UNION
              SELECT e.global_id, c.det FROM c JOIN elements e ON e.parent_global_id = c.gid)
            SELECT gid, MAX(det) AS det FROM c GROUP BY gid
            """);
        ModelDatabase.Exec(cn, "CREATE UNIQUE INDEX temp.ix_cov ON cov(gid)");
        Q("elements by level (count | with any sheet | with a detail sheet):", """
            SELECT e.level, COUNT(*), SUM(c.gid IS NOT NULL), SUM(COALESCE(c.det, 0)) FROM elements e LEFT JOIN cov c ON c.gid = e.global_id
            GROUP BY e.level ORDER BY 2 DESC
            """);
        Q("total (elements | with any sheet | % | with a detail sheet | %):", """
            SELECT COUNT(*), SUM(c.gid IS NOT NULL), ROUND(100.0 * SUM(c.gid IS NOT NULL) / COUNT(*), 1),
                   SUM(COALESCE(c.det, 0)), ROUND(100.0 * SUM(COALESCE(c.det, 0)) / COUNT(*), 1)
            FROM elements e LEFT JOIN cov c ON c.gid = e.global_id
            """);
        Q("elements with NO detail sheet by category (count):", """
            SELECT e.level || ': ' || COALESCE(e.category, ''), COUNT(*) FROM elements e LEFT JOIN cov c ON c.gid = e.global_id
            WHERE COALESCE(c.det, 0) = 0 GROUP BY 1 ORDER BY 2 DESC
            """);
        return sb.ToString();
    }
}
