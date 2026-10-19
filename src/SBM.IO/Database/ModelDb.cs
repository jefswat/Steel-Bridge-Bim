using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SBM.IO.Database;

/// <summary>One element row.</summary>
public sealed class ElementInfo
{
    public string GlobalId { get; init; } = "";
    public string Parent { get; init; } = "";
    public string Level { get; init; } = "";
    public string Category { get; init; } = "";
    public string Subtype { get; init; } = "";
    public string IfcClass { get; init; } = "";
    public bool InIfc { get; init; }
    public string Name { get; init; } = "";
    public string Mark { get; init; } = "";
    public string BridgeGroup { get; init; } = "";
    public string Bridge { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Support { get; init; } = "";
    public string Girder { get; init; } = "";
    public double? StationBegin { get; init; }
    public double? StationEnd { get; init; }
    /// <summary>Formatted by <see cref="ModelDb.FormatStations"/> (station numbers are stored in meters).</summary>
    public string StationRange { get; set; } = "";
    public string Material { get; init; } = "";
    public double? MassKg { get; init; }
    public double? VolumeM3 { get; init; }
    public double? LengthM { get; init; }
    public string Properties { get; init; } = "";
}

/// <summary>A plan sheet linked to an element (directly or through an ancestor) with its QC state for that element.</summary>
public sealed class SheetLink
{
    public string SheetId { get; init; } = "";
    public string PlanSet { get; init; } = "";
    public int PdfPage { get; init; }
    public string DesignSheet { get; init; } = "";
    public string Title { get; init; } = "";
    public string PageFile { get; init; } = "";
    public string PreviewFile { get; init; } = "";
    /// <summary>Absolute path of the whole plan-set PDF found when the database was built ("" = not found).</summary>
    public string PdfPath { get; init; } = "";
    public string Role { get; init; } = "";
    public string Basis { get; init; } = "";
    /// <summary>Name of the element the link is stored on (the element itself, or the ancestor it is inherited from).</summary>
    public string LinkRootName { get; init; } = "";
    public string LinkRootId { get; init; } = "";
    public bool Inherited { get; init; }
    public string QcStatus { get; init; } = "unchecked";
    public string CheckedBy { get; init; } = "";
    public string CheckedAt { get; init; } = "";
    public string Note { get; init; } = "";
}

/// <summary>Read access to the companion database for the app and API (element details, hierarchy chain, sheets, QC).</summary>
public sealed class ModelDb : IDisposable
{
    private readonly SqliteConnection _cn;
    public string Path { get; }
    public string Folder { get; }

    public ModelDb(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Folder = System.IO.Path.GetDirectoryName(Path)!;
        _cn = ModelDatabase.Open(Path);
    }

    public void Dispose() => _cn.Dispose();

    public string Meta(string key) => ModelDatabase.Scalar<string>(_cn, "SELECT value FROM meta WHERE key=$k", ("$k", key)) ?? "";
    public int ElementCount => ModelDatabase.Scalar<int>(_cn, "SELECT COUNT(*) FROM elements");
    public int SheetCount => ModelDatabase.Scalar<int>(_cn, "SELECT COUNT(*) FROM sheets");
    public int LinkCount => ModelDatabase.Scalar<int>(_cn, "SELECT COUNT(*) FROM element_sheet");

    private const string Cols = "global_id,parent_global_id,level,category,subtype,ifc_class,in_ifc,name,mark,bridge_group,bridge,unit,support,girder,station_begin,station_end,material,mass_kg,volume_m3,length_m,properties";

    private static string S(SqliteDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
    private static double? D(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDouble(i);

    private static ElementInfo Read(SqliteDataReader r) => new()
    {
        GlobalId = S(r, 0), Parent = S(r, 1), Level = S(r, 2), Category = S(r, 3), Subtype = S(r, 4), IfcClass = S(r, 5), InIfc = r.GetInt32(6) != 0,
        Name = S(r, 7), Mark = S(r, 8), BridgeGroup = S(r, 9), Bridge = S(r, 10), Unit = S(r, 11), Support = S(r, 12), Girder = S(r, 13),
        StationBegin = D(r, 14), StationEnd = D(r, 15), Material = S(r, 16), MassKg = D(r, 17), VolumeM3 = D(r, 18), LengthM = D(r, 19), Properties = S(r, 20),
    };

    public ElementInfo? Get(string globalId)
    {
        using var c = _cn.CreateCommand();
        c.CommandText = $"SELECT {Cols} FROM elements WHERE global_id=$g";
        c.Parameters.AddWithValue("$g", globalId);
        using var r = c.ExecuteReader();
        if (!r.Read()) return null;
        var e = Read(r);
        e.StationRange = FormatStations(e.StationBegin, e.StationEnd);
        return e;
    }

    private string? _style, _foot;

    /// <summary>"7596+55.86" style (US: 100-ft stations in the project's foot; SI: 1000 m) or "a to b".</summary>
    public string FormatStations(double? a, double? b)
    {
        if (a == null) return "";
        _style ??= Meta("station_style");
        _foot ??= Meta("foot_m");
        double foot = double.TryParse(_foot, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0 ? f : 0.3048;
        string F(double sta)
        {
            double unit = _style == "US" ? 100 * foot : 1000;
            double whole = Math.Floor(sta / unit + 1e-9);
            double rem = (sta - whole * unit) / (_style == "US" ? foot : 1);
            return $"{whole:0}+{rem:00.00}";
        }
        return b == null || Math.Abs(b.Value - a.Value) < 0.01 ? F(a.Value) : F(a.Value) + " to " + F(b.Value);
    }

    /// <summary>Ancestors from the project down to the element itself (the last item).</summary>
    public List<ElementInfo> Chain(string globalId)
    {
        var chain = new List<ElementInfo>();
        string? id = globalId;
        for (int guard = 0; id != null && id.Length > 0 && guard < 30; guard++)
        {
            var e = Get(id);
            if (e == null) break;
            chain.Insert(0, e);
            id = e.Parent;
        }
        return chain;
    }

    public List<ElementInfo> Children(string globalId, int limit = 200)
    {
        var list = new List<ElementInfo>();
        using var c = _cn.CreateCommand();
        c.CommandText = $"SELECT {Cols} FROM elements WHERE seq IN (SELECT id FROM el WHERE parent_id = (SELECT id FROM el WHERE global_id = $g) LIMIT {limit}) ORDER BY seq";
        c.Parameters.AddWithValue("$g", globalId);
        using var r = c.ExecuteReader();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    public int ChildCount(string globalId) => ModelDatabase.Scalar<int>(_cn, "SELECT COUNT(*) FROM el WHERE parent_id = (SELECT id FROM el WHERE global_id = $g)", ("$g", globalId));

    /// <summary>Global ids of an element and all its descendants (limited).</summary>
    public List<string> Descendants(string globalId, int limit = 20000)
    {
        var list = new List<string>();
        using var c = _cn.CreateCommand();
        c.CommandText = $"WITH RECURSIVE d(i) AS (SELECT id FROM el WHERE global_id = $g UNION ALL SELECT e.id FROM el e JOIN d ON e.parent_id = d.i) SELECT e.global_id FROM d JOIN el e ON e.id = d.i LIMIT {limit}";
        c.Parameters.AddWithValue("$g", globalId);
        using var r = c.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>Elements of a category with the given name whose bounding box contains (or is nearest to) a world point.</summary>
    public string? FindByNameNear(string category, string name, double x, double y, double z)
    {
        using var c = _cn.CreateCommand();
        c.CommandText = """
            SELECT global_id FROM el WHERE category_s = (SELECT id FROM strings WHERE value = $c) AND name = $n AND bbox_min_x IS NOT NULL
            ORDER BY (MAX(bbox_min_x - $x, 0) + MAX($x - bbox_max_x, 0)) + (MAX(bbox_min_y - $y, 0) + MAX($y - bbox_max_y, 0)) + (MAX(bbox_min_z - $z, 0) + MAX($z - bbox_max_z, 0)) LIMIT 1
            """;
        c.Parameters.AddWithValue("$c", category);
        c.Parameters.AddWithValue("$n", name);
        c.Parameters.AddWithValue("$x", x); c.Parameters.AddWithValue("$y", y); c.Parameters.AddWithValue("$z", z);
        return c.ExecuteScalar() as string;
    }

    /// <summary>Sheets the element is based on: links on the element and on each ancestor, with the QC record for this element.</summary>
    public List<SheetLink> Sheets(string globalId)
    {
        var chain = Chain(globalId);
        var list = new List<SheetLink>();
        var seen = new HashSet<string>();
        // nearest link first: the element itself, then its parent, ...
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            using var c = _cn.CreateCommand();
            c.CommandText = """
                SELECT s.sheet_id, s.plan_set, s.pdf_page, s.design_sheet, s.title, s.page_file, s.preview_file, l.role, l.basis,
                       q.status, q.checked_by, q.checked_at, q.note, s.pdf_path
                FROM element_sheet l JOIN sheets s ON s.sheet_id = l.sheet_id
                LEFT JOIN qc q ON q.global_id = $g AND q.sheet_id = l.sheet_id
                WHERE l.global_id = $a ORDER BY (l.role = 'detail') DESC, s.plan_set, s.pdf_page
                """;
            c.Parameters.AddWithValue("$g", globalId);
            c.Parameters.AddWithValue("$a", chain[i].GlobalId);
            using var r = c.ExecuteReader();
            while (r.Read())
            {
                string id = r.GetString(0);
                if (!seen.Add(id)) continue;
                list.Add(new SheetLink
                {
                    SheetId = id, PlanSet = S(r, 1), PdfPage = r.GetInt32(2), DesignSheet = S(r, 3), Title = S(r, 4), PageFile = S(r, 5), PreviewFile = S(r, 6),
                    Role = S(r, 7), Basis = S(r, 8), PdfPath = S(r, 13), QcStatus = r.IsDBNull(9) ? "unchecked" : r.GetString(9), CheckedBy = S(r, 10), CheckedAt = S(r, 11), Note = S(r, 12),
                    LinkRootName = chain[i].Name, LinkRootId = chain[i].GlobalId, Inherited = i != chain.Count - 1,
                });
            }
        }
        return list;
    }

    /// <summary>One element matched by <see cref="SheetsFor"/>, its hierarchy path and its linked sheets (direct + inherited).</summary>
    public sealed class ElementSheets
    {
        public string GlobalId { get; init; } = "";
        public string Mark { get; init; } = "";
        public string Name { get; init; } = "";
        public string Category { get; init; } = "";
        public string Path { get; init; } = "";
        public List<SheetLink> Sheets { get; init; } = new();
    }

    public sealed class SheetsForResult
    {
        public string Query { get; init; } = "";
        public int TotalMatches { get; init; }
        public List<ElementSheets> Elements { get; init; } = new();
        /// <summary>De-duplicated pages to read: PDF path to sorted page numbers (over the listed elements).</summary>
        public SortedDictionary<string, List<int>> Pages { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static string LikeEscape(string t) => t.Replace("\\", "\\\\").Replace("_", "\\_");

    /// <summary>Elements whose mark matches exactly (case-insensitive); when none do, whose mark, name or category (groups are elements,
    /// so group names too) contains the text or matches it as a % pattern. Up to <paramref name="max"/> are returned with their sheets;
    /// <see cref="SheetsForResult.TotalMatches"/> counts all matches.</summary>
    public SheetsForResult SheetsFor(string text, int max = 20)
    {
        text = text.Trim();
        var ids = new List<string>();
        int total;
        using (var c = _cn.CreateCommand())
        {
            c.CommandText = $"SELECT global_id FROM elements WHERE mark = $t COLLATE NOCASE ORDER BY seq";
            c.Parameters.AddWithValue("$t", text);
            using var r = c.ExecuteReader();
            while (r.Read()) ids.Add(r.GetString(0));
        }
        total = ids.Count;
        if (total == 0 && text.Length > 0)
        {
            string pat = text.Contains('%') ? text : "%" + LikeEscape(text) + "%";
            const string where = "WHERE mark LIKE $p ESCAPE '\\' OR name LIKE $p ESCAPE '\\' OR category LIKE $p ESCAPE '\\'";
            total = ModelDatabase.Scalar<int>(_cn, "SELECT COUNT(*) FROM elements " + where, ("$p", pat));
            using var c = _cn.CreateCommand();
            c.CommandText = $"SELECT global_id FROM elements {where} ORDER BY seq LIMIT {max}";
            c.Parameters.AddWithValue("$p", pat);
            using var r = c.ExecuteReader();
            while (r.Read()) ids.Add(r.GetString(0));
        }
        var res = new SheetsForResult { Query = text, TotalMatches = total };
        foreach (var id in ids.Take(max))
        {
            var chain = Chain(id);
            if (chain.Count == 0) continue;
            var e = chain[^1];
            var item = new ElementSheets
            {
                GlobalId = id, Mark = e.Mark, Name = e.Name, Category = e.Category,
                Path = string.Join(" > ", chain.Select(x => x.Name.Length > 0 ? x.Name : x.Category)),
                Sheets = Sheets(id),
            };
            res.Elements.Add(item);
            foreach (var s in item.Sheets)
            {
                if (s.PdfPath.Length == 0) continue;
                if (!res.Pages.TryGetValue(s.PdfPath, out var pages)) res.Pages[s.PdfPath] = pages = new List<int>();
                if (!pages.Contains(s.PdfPage)) pages.Add(s.PdfPage);
            }
        }
        foreach (var p in res.Pages.Values) p.Sort();
        return res;
    }

    /// <summary>A sheet found by <see cref="FindSheets"/>.</summary>
    public sealed class SheetInfo
    {
        public string SheetId { get; init; } = "";
        public string PlanSet { get; init; } = "";
        public int PdfPage { get; init; }
        public string DesignSheet { get; init; } = "";
        public string Title { get; init; } = "";
        public string PdfPath { get; init; } = "";
        /// <summary>Elements linked to the sheet directly (links inherited by descendants are not counted).</summary>
        public int LinkedElements { get; init; }
    }

    /// <summary>Sheets whose title or sheet number contains the text (case-insensitive), or matches it as a regex when it starts with ^.</summary>
    public List<SheetInfo> FindSheets(string text)
    {
        text = text.Trim();
        System.Text.RegularExpressions.Regex? rx = text.StartsWith('^')
            ? new(text, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)) : null;
        bool Hit(string v) => rx != null ? rx.IsMatch(v) : v.Contains(text, StringComparison.OrdinalIgnoreCase);
        var list = new List<SheetInfo>();
        using var c = _cn.CreateCommand();
        c.CommandText = """
            SELECT s.sheet_id, s.plan_set, s.pdf_page, s.design_sheet, s.title, s.pdf_path,
                   (SELECT COUNT(DISTINCT l.global_id) FROM element_sheet l WHERE l.sheet_id = s.sheet_id)
            FROM sheets s ORDER BY s.plan_set, s.pdf_page
            """;
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            string title = S(r, 4), no = S(r, 3);
            if (!Hit(title) && !Hit(no)) continue;
            list.Add(new SheetInfo { SheetId = S(r, 0), PlanSet = S(r, 1), PdfPage = r.GetInt32(2), DesignSheet = no, Title = title, PdfPath = S(r, 5), LinkedElements = r.GetInt32(6) });
        }
        return list;
    }

    /// <summary>Absolute path of a sheet's page file or preview (stored relative to the database folder).</summary>
    public string Resolve(string relative) => System.IO.Path.GetFullPath(System.IO.Path.Combine(Folder, relative));

    /// <summary>What opens a sheet: the single-page PDF of a split (page 1 of it) when it exists, else the whole plan-set PDF at the sheet's
    /// page; null when neither file exists.</summary>
    public (string File, int Page)? PdfTarget(SheetLink s)
    {
        if (s.PageFile.Length > 0 && Resolve(s.PageFile) is var pf && File.Exists(pf)) return (pf, 1);
        if (s.PdfPath.Length > 0 && File.Exists(s.PdfPath)) return (s.PdfPath, s.PdfPage);
        return null;
    }

    public void SetQc(string globalId, string sheetId, string status, string? by, string? note)
    {
        if (status is not ("unchecked" or "ok" or "issue")) throw new ArgumentException("status must be unchecked, ok or issue");
        ModelDatabase.Exec(_cn, """
            INSERT INTO qc(global_id, sheet_id, status, checked_by, checked_at, note) VALUES($g, $s, $st, $by, $at, $n)
            ON CONFLICT(global_id, sheet_id) DO UPDATE SET status=$st, checked_by=$by, checked_at=$at, note=$n
            """, ("$g", globalId), ("$s", sheetId), ("$st", status), ("$by", by ?? Environment.UserName),
            ("$at", DateTime.Now.ToString("s", CultureInfo.InvariantCulture)), ("$n", note ?? ""));
    }
}
