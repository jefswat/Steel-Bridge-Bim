using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace SBM.IO.Database;

/// <summary>One element of the companion database as the element agent review sees it: identity, hierarchy, box and its FINGERPRINT (hash of what
/// the database records about it: category, subtype, name, mark, material, mass, volume, length, box to 0.1 mm, properties, and for a container the
/// fingerprints of its children). Any regenerated change of the element changes the fingerprint.</summary>
public sealed record ReviewElement(string GlobalId, long Id, long? ParentId, string Level, string Category, string Bridge, string Name, string Fingerprint,
    double[]? Box, bool HasChildren);

/// <summary>One row of the table <c>element_agent_review</c> (rewritten by every <c>sbm review</c> run, never by a database rebuild).</summary>
public sealed record ElementReviewRow(string GlobalId, string Name, string Level, string Category, string Bridge, string Result, string State,
    string ReviewedAt, string Sheets, string Note, string Report, string Changed);

public sealed partial class ModelDb
{
    /// <summary>Gap (m) within which two element boxes count as touching (neighbours of an element agent review).</summary>
    public const double NeighbourGap = 0.002;

    /// <summary>Every element with its fingerprint (containers include their children's fingerprints, so a change below a reviewed group stales it).</summary>
    public Dictionary<string, ReviewElement> ReviewElements()
    {
        var rows = new List<(long id, long? parent, string gid, string level, string cat, string bridge, string name, string own, double[]? box)>();
        using (var c = _cn.CreateCommand())
        {
            c.CommandText = """
                SELECT e.id, e.parent_id, e.global_id, COALESCE(sl.value,''), COALESCE(sc.value,''), COALESCE(sb.value,''), COALESCE(e.name, sc.value || ' ' || e.mark, ''),
                       COALESCE(st.value,''), COALESCE(e.mark,''), COALESCE(sm.value,''), e.mass_kg, e.volume_m3, e.length_m,
                       e.bbox_min_x, e.bbox_min_y, e.bbox_min_z, e.bbox_max_x, e.bbox_max_y, e.bbox_max_z, COALESCE(pr.json,'')
                FROM el e LEFT JOIN strings sl ON sl.id = e.level_s LEFT JOIN strings sc ON sc.id = e.category_s LEFT JOIN strings sb ON sb.id = e.bridge_s
                  LEFT JOIN strings st ON st.id = e.subtype_s LEFT JOIN strings sm ON sm.id = e.material_s LEFT JOIN props pr ON pr.id = e.props_id
                """;
            using var r = c.ExecuteReader();
            while (r.Read())
            {
                string N(int i, string fmt) => r.IsDBNull(i) ? "-" : r.GetDouble(i).ToString(fmt, CultureInfo.InvariantCulture);
                double[]? box = null;
                if (!r.IsDBNull(13) && !r.IsDBNull(16)) box = Enumerable.Range(13, 6).Select(i => r.IsDBNull(i) ? double.NaN : r.GetDouble(i)).ToArray();
                if (box != null && box.Any(v => double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12)) box = null;
                var own = string.Join("|", r.GetString(4), r.GetString(7), r.GetString(6), r.GetString(8), r.GetString(9), N(10, "G6"), N(11, "G6"), N(12, "G6"),
                    box == null ? "-" : string.Join(",", box.Select(v => Math.Round(v * 1e4).ToString(CultureInfo.InvariantCulture))), r.GetString(19));
                rows.Add((r.GetInt64(0), r.IsDBNull(1) ? null : r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), own, box));
            }
        }
        var kids = rows.Where(x => x.parent != null).GroupBy(x => x.parent!.Value).ToDictionary(g => g.Key, g => g.Select(x => x.id).OrderBy(i => i).ToList());
        var byId = rows.ToDictionary(x => x.id);
        var fp = new Dictionary<long, string>();
        // children before parents: process by depth (iterative post-order)
        foreach (var root in rows.Where(x => x.parent == null || !byId.ContainsKey(x.parent.Value)).Select(x => x.id))
        {
            var stack = new Stack<(long id, bool done)>();
            stack.Push((root, false));
            while (stack.Count > 0)
            {
                var (id, done) = stack.Pop();
                if (fp.ContainsKey(id)) continue;
                if (!done && kids.TryGetValue(id, out var ks)) { stack.Push((id, true)); foreach (var k in ks) if (!fp.ContainsKey(k)) stack.Push((k, false)); continue; }
                var row = byId[id];
                var sb = new StringBuilder(row.own);
                if (kids.TryGetValue(id, out var ch)) foreach (var k in ch) sb.Append('/').Append(fp.GetValueOrDefault(k, ""));
                fp[id] = Hash(sb.ToString());
            }
        }
        return rows.ToDictionary(x => x.gid, x => new ReviewElement(x.gid, x.id, x.parent, x.level, x.cat, x.bridge, x.name, fp.GetValueOrDefault(x.id, Hash(x.own)),
            x.box, kids.ContainsKey(x.id)), StringComparer.Ordinal);
    }

    /// <summary>One hash over the (sorted) neighbours' GlobalIds and fingerprints: any change, addition or removal of a neighbour changes it.</summary>
    public static string NeighbourHash(Dictionary<string, ReviewElement> all, IEnumerable<string> neighbours) =>
        Hash(string.Join(";", neighbours.OrderBy(g => g, StringComparer.Ordinal).Select(g => g + ":" + (all.TryGetValue(g, out var e) ? e.Fingerprint : ""))));

    /// <summary>Neighbours counted per category (shown when an interface changed).</summary>
    public static Dictionary<string, int> NeighbourCounts(Dictionary<string, ReviewElement> all, IEnumerable<string> neighbours) =>
        neighbours.Where(all.ContainsKey).GroupBy(g => all[g].Category).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)), 0, 12).ToLowerInvariant();

    /// <summary>Neighbours of each element of <paramref name="of"/>: elements WITHOUT children (leaf solids, bars, fasteners) whose box touches the
    /// element's box (within <see cref="NeighbourGap"/>), excluding the element's own descendants and ancestors. A box test: it includes everything in
    /// contact and some elements that only share the box (conservative - a change there also stales the review).</summary>
    public static Dictionary<string, List<string>> Neighbours(Dictionary<string, ReviewElement> all, IEnumerable<string> of)
    {
        var leaves = all.Values.Where(e => !e.HasChildren && e.Box != null).ToList();
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var targets = of.Where(all.ContainsKey).Distinct().ToList();
        if (targets.Count == 0 || leaves.Count == 0) return result;
        // uniform grid over the leaf boxes, cell size from the median leaf size (bounded)
        var sizes = leaves.Select(e => Math.Max(e.Box![3] - e.Box[0], Math.Max(e.Box[4] - e.Box[1], e.Box[5] - e.Box[2]))).OrderBy(v => v).ToList();
        double cell = Math.Clamp(sizes[sizes.Count / 2] * 2, 0.5, 20);
        var grid = new Dictionary<(int, int, int), List<int>>();
        (int, int, int) Cell(double x, double y, double z) => ((int)Math.Floor(x / cell), (int)Math.Floor(y / cell), (int)Math.Floor(z / cell));
        var big = new List<int>();
        for (int i = 0; i < leaves.Count; i++)
        {
            var b = leaves[i].Box!;
            var (i0, j0, k0) = Cell(b[0], b[1], b[2]); var (i1, j1, k1) = Cell(b[3], b[4], b[5]);
            if ((long)(i1 - i0 + 1) * (j1 - j0 + 1) * (k1 - k0 + 1) > 4096) { big.Add(i); continue; }
            for (int a = i0; a <= i1; a++) for (int c = j0; c <= j1; c++) for (int d = k0; d <= k1; d++)
            {
                if (!grid.TryGetValue((a, c, d), out var l)) grid[(a, c, d)] = l = new List<int>();
                l.Add(i);
            }
        }
        var byId = all.Values.ToDictionary(e => e.Id);
        foreach (var gid in targets)
        {
            var e = all[gid];
            var found = new HashSet<int>();
            var boxes = new List<double[]>();
            if (e.Box != null) boxes.Add(e.Box);
            if (boxes.Count == 0) { result[gid] = new List<string>(); continue; }
            var anc = new HashSet<long>();
            for (long? p = e.ParentId; p is long k && byId.TryGetValue(k, out var pe) && anc.Add(k); ) p = pe.ParentId;
            bool Touch(double[] a, double[] b) => a[0] <= b[3] + NeighbourGap && b[0] <= a[3] + NeighbourGap && a[1] <= b[4] + NeighbourGap && b[1] <= a[4] + NeighbourGap
                && a[2] <= b[5] + NeighbourGap && b[2] <= a[5] + NeighbourGap;
            bool Inside(ReviewElement x)
            {
                for (long? p = x.ParentId; p is long k && byId.TryGetValue(k, out var pe); p = pe.ParentId) if (k == e.Id) return true;
                return false;
            }
            var b0 = e.Box!;
            var (i0, j0, k0) = Cell(b0[0] - NeighbourGap, b0[1] - NeighbourGap, b0[2] - NeighbourGap);
            var (i1, j1, k1) = Cell(b0[3] + NeighbourGap, b0[4] + NeighbourGap, b0[5] + NeighbourGap);
            IEnumerable<int> Cand()
            {
                foreach (var i in big) yield return i;
                if ((long)(i1 - i0 + 1) * (j1 - j0 + 1) * (k1 - k0 + 1) > 1_000_000) { for (int i = 0; i < leaves.Count; i++) yield return i; yield break; }
                for (int a = i0; a <= i1; a++) for (int c = j0; c <= j1; c++) for (int d = k0; d <= k1; d++)
                    if (grid.TryGetValue((a, c, d), out var l)) foreach (var i in l) yield return i;
            }
            foreach (var i in Cand()) found.Add(i);
            var list = new List<string>();
            foreach (var i in found)
            {
                var x = leaves[i];
                if (x.Id == e.Id || anc.Contains(x.Id) || !Touch(b0, x.Box!) || Inside(x)) continue;
                list.Add(x.GlobalId);
            }
            list.Sort(StringComparer.Ordinal);
            result[gid] = list;
        }
        return result;
    }

    /// <summary>Global ids of the elements matching a selector (all given parts must match): bridge (exact), level (exact), category (exact or
    /// regex with <paramref name="regex"/>), name (regex, case-insensitive).</summary>
    public static List<string> Select(Dictionary<string, ReviewElement> all, string? bridge, string? level, string? category, string? nameRegex)
    {
        var rx = string.IsNullOrEmpty(nameRegex) ? null : new System.Text.RegularExpressions.Regex(nameRegex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return all.Values.Where(e => (bridge == null || e.Bridge == bridge) && (level == null || e.Level == level) && (category == null || e.Category == category)
            && (rx == null || rx.IsMatch(e.Name))).OrderBy(e => e.Id).Select(e => e.GlobalId).ToList();
    }

    /// <summary>Replaces the table <c>element_agent_review</c> with these rows and creates the views <c>v_element_agent_review</c> (joined to the
    /// elements) and <c>v_element_agent_review_stale</c>.</summary>
    public void WriteElementReview(IEnumerable<ElementReviewRow> rows, string at)
    {
        ModelDatabase.Exec(_cn, """
            CREATE TABLE IF NOT EXISTS element_agent_review(global_id TEXT PRIMARY KEY, name TEXT, level TEXT, category TEXT, bridge TEXT, result TEXT, state TEXT,
              reviewed_at TEXT, sheets TEXT, note TEXT, report TEXT, changed TEXT) WITHOUT ROWID;
            DELETE FROM element_agent_review;
            DROP VIEW IF EXISTS v_element_agent_review;
            CREATE VIEW v_element_agent_review AS SELECT r.*, e.unit, e.support, e.girder FROM element_agent_review r LEFT JOIN elements e ON e.global_id = r.global_id;
            DROP VIEW IF EXISTS v_element_agent_review_stale;
            CREATE VIEW v_element_agent_review_stale AS SELECT * FROM element_agent_review WHERE state <> 'current';
            """);
        using var tx = _cn.BeginTransaction();
        using (var c = _cn.CreateCommand())
        {
            c.Transaction = tx;
            c.CommandText = "INSERT OR REPLACE INTO element_agent_review VALUES($g,$n,$l,$c,$b,$r,$s,$a,$sh,$no,$re,$ch)";
            var ps = new[] { "$g", "$n", "$l", "$c", "$b", "$r", "$s", "$a", "$sh", "$no", "$re", "$ch" }.Select(n => c.Parameters.Add(n, SqliteType.Text)).ToArray();
            foreach (var row in rows)
            {
                var v = new[] { row.GlobalId, row.Name, row.Level, row.Category, row.Bridge, row.Result, row.State, row.ReviewedAt, row.Sheets, row.Note, row.Report, row.Changed };
                for (int i = 0; i < v.Length; i++) ps[i].Value = v[i];
                c.ExecuteNonQuery();
            }
        }
        using (var m = _cn.CreateCommand())
        {
            m.Transaction = tx;
            m.CommandText = "INSERT OR REPLACE INTO meta(key, value) VALUES('element_agent_review_at', $a)";
            m.Parameters.AddWithValue("$a", at);
            m.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
