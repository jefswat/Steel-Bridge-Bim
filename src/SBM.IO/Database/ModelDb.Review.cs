using Microsoft.Data.Sqlite;

namespace SBM.IO.Database;

/// <summary>A plan sheet no element links to (view <c>v_unused_sheets</c>; administrative sheets excluded).</summary>
public sealed record UnusedSheet(string SheetId, string PlanSet, int Page, string Sheet, string Title);

/// <summary>Elements of one bridge / level / category that have no sheet used to model them (no non-general link of their own or on an ancestor).</summary>
public sealed record UnlinkedElements(string Bridge, string Level, string Category, int Count, List<string> Examples);

/// <summary>One agent review flag row (table <c>agent_review</c>, rewritten by every <c>sbm review</c> run; never touched by <see cref="ModelDatabase"/> rebuilds).</summary>
public sealed record AgentReviewRow(string Kind, string Level, string Bridge, string Subject, string Path, string Value, string Reason, string Next);

public sealed partial class ModelDb
{
    /// <summary>Plan PDFs the sheet index resolved (sheets.pdf_path), distinct.</summary>
    public List<string> PdfPaths()
    {
        var list = new List<string>();
        using var c = _cn.CreateCommand();
        c.CommandText = "SELECT DISTINCT pdf_path FROM sheets WHERE COALESCE(pdf_path,'') <> ''";
        using var r = c.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    public List<UnusedSheet> UnusedSheets()
    {
        var list = new List<UnusedSheet>();
        using var c = _cn.CreateCommand();
        c.CommandText = "SELECT sheet_id, plan_set, COALESCE(pdf_page,0), COALESCE(design_sheet,''), COALESCE(title,'') FROM v_unused_sheets ORDER BY plan_set, pdf_page";
        using var r = c.ExecuteReader();
        while (r.Read()) list.Add(new UnusedSheet(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4)));
        return list;
    }

    /// <summary>Levels whose links (general elevations, stress sheets) do not count as the sheet a member below them was modeled from.</summary>
    private static readonly HashSet<string> BridgeLevels = new(StringComparer.Ordinal) { "project", "site", "alignment", "unit" };

    /// <summary>Elements at the given levels without a sheet used to model them, grouped by bridge / level / category (up to 3 example names each).</summary>
    public List<UnlinkedElements> Unlinked(ICollection<string> levels)
    {
        var linked = new HashSet<string>(StringComparer.Ordinal);
        using (var c = _cn.CreateCommand())
        {
            c.CommandText = "SELECT DISTINCT global_id FROM element_sheet WHERE role <> 'general'";
            using var r = c.ExecuteReader();
            while (r.Read()) linked.Add(r.GetString(0));
        }
        var rows = new Dictionary<long, (long? parent, string gid, string level, string cat, string bridge, string name)>();
        using (var c = _cn.CreateCommand())
        {
            c.CommandText = """
                SELECT e.id, e.parent_id, e.global_id, COALESCE(sl.value,''), COALESCE(sc.value,''), COALESCE(sb.value,''), COALESCE(e.name, e.mark, '')
                FROM el e LEFT JOIN strings sl ON sl.id = e.level_s LEFT JOIN strings sc ON sc.id = e.category_s LEFT JOIN strings sb ON sb.id = e.bridge_s
                """;
            using var r = c.ExecuteReader();
            while (r.Read())
                rows[r.GetInt64(0)] = (r.IsDBNull(1) ? null : r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6));
        }
        var memo = new Dictionary<long, bool>();
        bool Linked(long id)
        {
            var chain = new List<long>();
            bool result = false;
            long? cur = id;
            while (cur is long k && rows.TryGetValue(k, out var row))
            {
                if (memo.TryGetValue(k, out var m)) { result = m; break; }
                chain.Add(k);
                if (linked.Contains(row.gid) && !BridgeLevels.Contains(row.level)) { result = true; break; }
                cur = row.parent;
            }
            foreach (var k in chain) memo[k] = result;
            return result;
        }
        // a container (truss, substructure, cap group) whose members have sheets is not reported itself
        var below = new HashSet<long>();
        foreach (var (id, row) in rows)
            if (linked.Contains(row.gid) && !BridgeLevels.Contains(row.level))
                for (long? cur = row.parent; cur is long k && rows.TryGetValue(k, out var pr) && below.Add(k); ) cur = pr.parent;
        var groups = new Dictionary<(string, string, string), (int n, List<string> ex)>();
        foreach (var (id, row) in rows)
        {
            if (!levels.Contains(row.level) || below.Contains(id) || Linked(id)) continue;
            var key = (row.bridge, row.level, row.cat);
            if (!groups.TryGetValue(key, out var g)) g = (0, new List<string>());
            if (g.ex.Count < 3 && row.name.Length > 0) g.ex.Add(row.name);
            groups[key] = (g.n + 1, g.ex);
        }
        return groups.Select(kv => new UnlinkedElements(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value.n, kv.Value.ex))
            .OrderBy(u => u.Bridge).ThenBy(u => u.Level).ThenBy(u => u.Category).ToList();
    }

    /// <summary>Distinct sheets ("p.N sheet - title") linked directly (non-general) to elements of a bridge, optionally of one support and / or whose
    /// category contains one of the keywords. Used to point a review flag at the sheet where the value should be read.</summary>
    public List<string> SheetsFor(string bridge, string? support, IReadOnlyList<string>? categoryKeywords, int limit = 6)
    {
        var where = new List<string> { "e.bridge = $b" };
        var ps = new List<(string, object?)> { ("$b", bridge) };
        if (support != null) { where.Add("e.support = $s"); ps.Add(("$s", support)); }
        if (categoryKeywords is { Count: > 0 })
        {
            var ors = new List<string>();
            for (int i = 0; i < categoryKeywords.Count; i++) { ors.Add($"e.category LIKE $k{i}"); ps.Add(($"$k{i}", "%" + categoryKeywords[i] + "%")); }
            where.Add("(" + string.Join(" OR ", ors) + ")");
        }
        using var c = _cn.CreateCommand();
        c.CommandText = $"""
            SELECT s.pdf_page, COALESCE(s.design_sheet,''), COALESCE(s.title,''), COUNT(*) AS n FROM element_sheet l JOIN elements e ON e.global_id = l.global_id
            JOIN sheets s ON s.sheet_id = l.sheet_id WHERE l.role <> 'general' AND {string.Join(" AND ", where)}
            GROUP BY s.sheet_id ORDER BY n DESC LIMIT {limit}
            """;
        foreach (var (k, v) in ps) c.Parameters.AddWithValue(k, v ?? DBNull.Value);
        var list = new List<string>();
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            string sheet = r.GetString(1), title = r.GetString(2);
            list.Add($"p.{(r.IsDBNull(0) ? 0 : r.GetInt32(0))}" + (sheet.Length > 0 ? $" ({sheet})" : "") + (title.Length > 0 ? " " + title : ""));
        }
        return list;
    }

    /// <summary>Replaces the table <c>agent_review</c> with these rows (+ meta <c>agent_review_at</c>) and creates the view <c>v_agent_review_open</c>.</summary>
    public void WriteAgentReview(IEnumerable<AgentReviewRow> rows, string at)
    {
        ModelDatabase.Exec(_cn, """
            CREATE TABLE IF NOT EXISTS agent_review(id INTEGER PRIMARY KEY, reviewed_at TEXT, kind TEXT, level TEXT, bridge TEXT, subject TEXT, path TEXT,
              value TEXT, reason TEXT, next TEXT);
            DELETE FROM agent_review;
            DROP VIEW IF EXISTS v_agent_review_open;
            CREATE VIEW v_agent_review_open AS SELECT * FROM agent_review WHERE level = 'open';
            """);
        using var tx = _cn.BeginTransaction();
        using (var c = _cn.CreateCommand())
        {
            c.Transaction = tx;
            c.CommandText = "INSERT INTO agent_review(reviewed_at, kind, level, bridge, subject, path, value, reason, next) VALUES($a,$k,$l,$b,$s,$p,$v,$r,$n)";
            var pa = c.Parameters.Add("$a", SqliteType.Text); var pk = c.Parameters.Add("$k", SqliteType.Text); var pl = c.Parameters.Add("$l", SqliteType.Text);
            var pb = c.Parameters.Add("$b", SqliteType.Text); var psub = c.Parameters.Add("$s", SqliteType.Text); var pp = c.Parameters.Add("$p", SqliteType.Text);
            var pv = c.Parameters.Add("$v", SqliteType.Text); var pr = c.Parameters.Add("$r", SqliteType.Text); var pn = c.Parameters.Add("$n", SqliteType.Text);
            foreach (var row in rows)
            {
                pa.Value = at; pk.Value = row.Kind; pl.Value = row.Level; pb.Value = row.Bridge; psub.Value = row.Subject; pp.Value = row.Path;
                pv.Value = row.Value; pr.Value = row.Reason; pn.Value = row.Next;
                c.ExecuteNonQuery();
            }
        }
        using (var m = _cn.CreateCommand())
        {
            m.Transaction = tx;
            m.CommandText = "INSERT OR REPLACE INTO meta(key, value) VALUES('agent_review_at', $a)";
            m.Parameters.AddWithValue("$a", at);
            m.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
