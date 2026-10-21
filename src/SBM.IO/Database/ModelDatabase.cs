using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SBM.Core.Alignments;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using SBM.IO.Ifc;

namespace SBM.IO.Database;

/// <summary>Model object (SteelPart, BoltInstance, StudInstance, BarPlacement, SubSolid, PrestressedBeam, ...) → GlobalId, by reference.</summary>
public sealed class TagIndex
{
    private readonly Dictionary<object, string> _map = new(ReferenceEqualityComparer.Instance);
    public int Count => _map.Count;
    internal void Add(object tag, string globalId) => _map[tag] = globalId;
    public string? Lookup(object? tag) => tag != null && _map.TryGetValue(tag, out var g) ? g : null;
}

/// <summary>Counts and timing of a database build.</summary>
public sealed class DatabaseBuildResult
{
    public int Elements { get; set; }
    public Dictionary<string, int> ByLevel { get; } = new();
    public Dictionary<string, int> ByCategory { get; } = new();
    public TimeSpan Elapsed { get; set; }
    public string Path { get; set; } = "";
    public TagIndex Tags { get; set; } = new();
}

/// <summary>
/// Companion SQLite database of a model: one row per model element (<c>elements</c>) with the SAME GlobalId the IFC export
/// uses (<see cref="IfcKeys"/>) and the aggregation hierarchy
/// project → site → [bridge group →] unit (IfcBridge) → Superstructure / Deck / Substructure → girder (virtual group) →
/// assembly (field section, field splice, bearing; cross frames under a "Cross frames" group) → part → bolt / stud / weld,
/// and pier / abutment → cap, column, pedestal, footing, pile, rebar. Virtual groups (bridge group, girder, cross frames,
/// lateral bracing) have no IFC entity (<c>in_ifc</c> = 0) but deterministic ids too.
/// Plan sheets (<c>sheets</c>), links (<c>element_sheet</c>) and QC records (<c>qc</c>) live in the same file; a rebuild replaces
/// the element and link tables but keeps <c>sheets</c> and <c>qc</c> (GlobalIds are stable across rebuilds). The asset management tables
/// (<see cref="AssetSchema"/>: photos / documents / calcs by reference, inspections, findings, calc values) are never touched by a rebuild either.
/// </summary>
public static class ModelDatabase
{
    public const int SchemaVersion = 3; // 3 = asset management tables (AssetSchema, G9)

    private static readonly Regex UnitName = new(@"^(?<g>.*?)\s+Unit\s+(?<n>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CfName = new(@"^(?<type>.*?)\s+G(?<a>\d+)[–-]G(?<b>\d+)(?:, span (?<span>\d+))?", RegexOptions.Compiled);
    private static readonly Regex XgName = new(@"^Cross girder G(?<a>\d+)[–-]G(?<b>\d+) carrying discontinuous G(?<g>\d+)", RegexOptions.Compiled);

    // Physical layout (compact): el = one row per element with integer keys; repeated text (level, category, material, unit names...) lives
    // once in strings, identical property JSON once in props. The readable table is the VIEW elements (same columns as before).
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT);
        CREATE TABLE IF NOT EXISTS strings(id INTEGER PRIMARY KEY, value TEXT NOT NULL UNIQUE);
        CREATE TABLE IF NOT EXISTS props(id INTEGER PRIMARY KEY, json TEXT NOT NULL UNIQUE);
        CREATE TABLE IF NOT EXISTS el(
          id INTEGER PRIMARY KEY, global_id TEXT NOT NULL UNIQUE, parent_id INTEGER,
          level_s INTEGER, category_s INTEGER, subtype_s INTEGER, ifc_class_s INTEGER, in_ifc INTEGER NOT NULL DEFAULT 1,
          name TEXT, mark TEXT,
          bridge_group_s INTEGER, bridge_s INTEGER, unit_s INTEGER, support_s INTEGER, girder_s INTEGER,
          station_begin REAL, station_end REAL, material_s INTEGER, mass_kg REAL, volume_m3 REAL, length_m REAL,
          bbox_min_x REAL, bbox_min_y REAL, bbox_min_z REAL, bbox_max_x REAL, bbox_max_y REAL, bbox_max_z REAL,
          props_id INTEGER);
        CREATE TABLE IF NOT EXISTS sheets(
          sheet_id TEXT PRIMARY KEY, plan_set TEXT NOT NULL, pdf_file TEXT, pdf_page INTEGER, design_sheet TEXT, title TEXT,
          page_file TEXT, preview_file TEXT, kind TEXT, note TEXT, pdf_path TEXT);
        CREATE TABLE IF NOT EXISTS element_sheet(
          global_id TEXT NOT NULL, sheet_id TEXT NOT NULL, role TEXT NOT NULL DEFAULT 'detail', basis TEXT, source TEXT,
          PRIMARY KEY(global_id, sheet_id)) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS qc(
          global_id TEXT NOT NULL, sheet_id TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'unchecked' CHECK(status IN ('unchecked','ok','issue')),
          checked_by TEXT, checked_at TEXT, note TEXT,
          PRIMARY KEY(global_id, sheet_id)) WITHOUT ROWID;
        """;

    public const string IndexSql = """
        CREATE INDEX IF NOT EXISTS ix_el_parent ON el(parent_id);
        CREATE INDEX IF NOT EXISTS ix_el_cat ON el(category_s, level_s);
        CREATE INDEX IF NOT EXISTS ix_es_sheet ON element_sheet(sheet_id);
        """;

    /// <summary>Views: <c>elements</c> (readable columns), links resolved down the hierarchy (a link on a group applies to every
    /// child), the hierarchy path, unused sheets and the QC join.</summary>
    public const string ViewSql = """
        DROP VIEW IF EXISTS elements;
        CREATE VIEW elements AS
          SELECT e.id AS seq, e.global_id, p.global_id AS parent_global_id, sl.value AS level, sc.value AS category, st.value AS subtype,
                 si.value AS ifc_class, e.in_ifc, COALESCE(e.name, sc.value || ' ' || e.mark) AS name, e.mark,
                 sbg.value AS bridge_group, sb.value AS bridge, su.value AS unit, ss.value AS support, sg.value AS girder,
                 e.station_begin, e.station_end, sm.value AS material, e.mass_kg, e.volume_m3, e.length_m,
                 e.bbox_min_x, e.bbox_min_y, e.bbox_min_z, e.bbox_max_x, e.bbox_max_y, e.bbox_max_z, pr.json AS properties
          FROM el e LEFT JOIN el p ON p.id = e.parent_id
            LEFT JOIN strings sl ON sl.id = e.level_s LEFT JOIN strings sc ON sc.id = e.category_s LEFT JOIN strings st ON st.id = e.subtype_s
            LEFT JOIN strings si ON si.id = e.ifc_class_s LEFT JOIN strings sbg ON sbg.id = e.bridge_group_s LEFT JOIN strings sb ON sb.id = e.bridge_s
            LEFT JOIN strings su ON su.id = e.unit_s LEFT JOIN strings ss ON ss.id = e.support_s LEFT JOIN strings sg ON sg.id = e.girder_s
            LEFT JOIN strings sm ON sm.id = e.material_s LEFT JOIN props pr ON pr.id = e.props_id;
        DROP VIEW IF EXISTS v_element_sheets;
        CREATE VIEW v_element_sheets AS
          WITH RECURSIVE d(id, sheet_id, role, basis, link_root) AS (
            SELECT e.id, l.sheet_id, l.role, l.basis, l.global_id FROM element_sheet l JOIN el e ON e.global_id = l.global_id
            UNION ALL
            -- Only the sheets used to model an element (user, 2026-10-19): a child inherits the links of its NEAREST linked ancestor (none when it
            -- has links of its own) and general sheets (quantities, notes, situation plans) stay on the element they are linked to.
            SELECT c.id, d.sheet_id, d.role, d.basis, d.link_root FROM d JOIN el c ON c.parent_id = d.id
            WHERE d.role <> 'general' AND NOT EXISTS (SELECT 1 FROM element_sheet l2 WHERE l2.global_id = c.global_id AND l2.role <> 'general'))
          SELECT e.global_id, d.sheet_id, d.role, d.basis, d.link_root FROM d JOIN el e ON e.id = d.id;
        DROP VIEW IF EXISTS v_element_path;
        CREATE VIEW v_element_path AS
          WITH RECURSIVE p(eid, depth, anc) AS (
            SELECT id, 0, id FROM el
            UNION ALL
            SELECT p.eid, p.depth + 1, e.parent_id FROM p JOIN el e ON e.id = p.anc WHERE e.parent_id IS NOT NULL)
          SELECT a.global_id AS global_id, p.depth AS depth, b.global_id AS ancestor_global_id, b.name AS ancestor_name
          FROM p JOIN el a ON a.id = p.eid JOIN el b ON b.id = p.anc;
        DROP VIEW IF EXISTS v_unused_sheets;
        CREATE VIEW v_unused_sheets AS
          SELECT s.* FROM sheets s WHERE NOT EXISTS (SELECT 1 FROM element_sheet l WHERE l.sheet_id = s.sheet_id) AND COALESCE(s.kind,'') <> 'administrative';
        DROP VIEW IF EXISTS v_unresolved;
        CREATE VIEW v_unresolved AS
          SELECT e.global_id, e.level, e.category, e.name, e.bridge, e.unit, e.support,
                 json_extract(e.properties, '$.status') AS status, json_extract(e.properties, '$.reason') AS reason, json_extract(e.properties, '$.assumed') AS assumed,
                 COALESCE((SELECT q.status FROM qc q WHERE q.global_id = e.global_id ORDER BY q.checked_at DESC LIMIT 1), 'unchecked') AS qc_status
          FROM elements e WHERE json_extract(e.properties, '$.status') = 'UNRESOLVED';
        DROP VIEW IF EXISTS v_element_qc;
        CREATE VIEW v_element_qc AS
          SELECT e.global_id, e.level, e.category, e.name, e.mark, e.bridge, e.unit, e.support, e.girder,
                 s.sheet_id, s.plan_set, s.design_sheet, s.pdf_page, s.title, v.role, v.basis, v.link_root,
                 COALESCE(q.status,'unchecked') AS qc_status, q.checked_by, q.checked_at, q.note
          FROM v_element_sheets v JOIN elements e ON e.global_id = v.global_id JOIN sheets s ON s.sheet_id = v.sheet_id
          LEFT JOIN qc q ON q.global_id = v.global_id AND q.sheet_id = v.sheet_id;
        """;

    /// <summary>Opens (creating the schema if needed) a database file for reading and writing.</summary>
    public static SqliteConnection Open(string path)
    {
        var cn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        cn.Open();
        Exec(cn, "PRAGMA temp_store=MEMORY;");
        // Version 1 files had a plain `elements` table: replace it (a rebuild refills; sheets, links and QC are kept).
        if (Scalar<int>(cn, "SELECT COUNT(*) FROM sqlite_master WHERE name='elements' AND type='table'") > 0) Exec(cn, "DROP TABLE elements");
        Exec(cn, SchemaSql);
        if (Scalar<int>(cn, "SELECT COUNT(*) FROM pragma_table_info('sheets') WHERE name='note'") == 0) Exec(cn, "ALTER TABLE sheets ADD COLUMN note TEXT");
        if (Scalar<int>(cn, "SELECT COUNT(*) FROM pragma_table_info('sheets') WHERE name='pdf_path'") == 0) Exec(cn, "ALTER TABLE sheets ADD COLUMN pdf_path TEXT");
        AssetSchema.Ensure(cn);
        return cn;
    }

    public static void Exec(SqliteConnection cn, string sql, params (string, object?)[] ps)
    {
        using var c = cn.CreateCommand();
        c.CommandText = sql;
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        c.ExecuteNonQuery();
    }

    public static T? Scalar<T>(SqliteConnection cn, string sql, params (string, object?)[] ps)
    {
        using var c = cn.CreateCommand();
        c.CommandText = sql;
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        var r = c.ExecuteScalar();
        return r is null or DBNull ? default : (T)Convert.ChangeType(r, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture);
    }

    /// <summary>File size in MB and the row count of each table (SQLite's dbstat byte sizes are used when available).</summary>
    public static (double totalMb, List<(string name, double mb, long rows)> objects) SizeReport(SqliteConnection cn, string path)
    {
        double total = new FileInfo(path).Length / 1048576.0;
        var list = new List<(string, double, long)>();
        var bytes = new Dictionary<string, double>();
        try
        {
            using var c = cn.CreateCommand();
            c.CommandText = "SELECT name, SUM(pgsize) / 1048576.0 FROM dbstat GROUP BY name";
            using var r = c.ExecuteReader();
            while (r.Read()) bytes[r.GetString(0)] = r.GetDouble(1);
        }
        catch (SqliteException) { }
        foreach (var t in new[] { "el", "strings", "props", "element_sheet", "sheets", "qc" }.Concat(AssetSchema.Tables))
            list.Add((t, bytes.GetValueOrDefault(t, double.NaN), Scalar<long>(cn, $"SELECT COUNT(*) FROM {t}")));
        return (total, list);
    }

    /// <summary>One-line summary: file size, largest tables; says so when above <paramref name="noteMb"/> (informational only, never an error).</summary>
    public static string SizeText(string path, double noteMb = SizeNoteMb)
    {
        using var cn = Open(path);
        var (total, objs) = SizeReport(cn, path);
        string tables = string.Join(", ", objs.OrderByDescending(o => double.IsNaN(o.mb) ? o.rows : o.mb * 1e6).Take(4)
            .Select(o => double.IsNaN(o.mb) ? $"{o.name} {o.rows:N0} rows" : $"{o.name} {o.mb:F1} MB ({o.rows:N0} rows)"));
        return total > noteMb ? $"Note: the database is {total:F1} MB (> {noteMb:F0} MB). Largest tables: {tables}" : $"Database size {total:F1} MB. Largest tables: {tables}";
    }

    /// <summary>Informational size threshold (MB) above which the CLI / app mention the database size prominently. Not a limit.</summary>
    public const double SizeNoteMb = 100;

    // ================================================================== build

    /// <summary>Computes only the model object → GlobalId map (no database): for the app to resolve picked objects.</summary>
    public static TagIndex BuildTagIndex(BridgeProject project, IReadOnlyList<IfcBridgeData> bridges)
    {
        var res = new DatabaseBuildResult();
        new Builder(new Writer(null, res), project, keysOnly: true).Run(bridges);
        return res.Tags;
    }

    /// <summary>Writes the element registry of the model to <paramref name="path"/> (replacing the elements; sheets, links and QC are kept).</summary>
    public static DatabaseBuildResult Write(string path, BridgeProject project, IReadOnlyList<IfcBridgeData> bridges, string? modelFile = null, bool vacuum = false)
    {
        var sw = Stopwatch.StartNew();
        var result = new DatabaseBuildResult { Path = path };
        using (var cn = Open(path))
        {
            Exec(cn, "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF;");
            Exec(cn, "DROP VIEW IF EXISTS v_element_sheets; DROP VIEW IF EXISTS v_element_path; DROP VIEW IF EXISTS v_unused_sheets; DROP VIEW IF EXISTS v_element_qc; DROP VIEW IF EXISTS v_unresolved; DROP VIEW IF EXISTS elements;");
            Exec(cn, "DROP INDEX IF EXISTS ix_el_parent; DROP INDEX IF EXISTS ix_el_cat;");
            Exec(cn, "DELETE FROM el; DELETE FROM strings; DELETE FROM props;");
            using (var tx = cn.BeginTransaction())
            {
                using var w = new Writer(cn, result, tx);
                new Builder(w, project, false).Run(bridges);
                tx.Commit();
            }
            Exec(cn, IndexSql);
            // Links survive a rebuild (GlobalIds are stable) until the rules are applied again; drop those of elements that no longer exist.
            Exec(cn, "DELETE FROM element_sheet WHERE global_id NOT IN (SELECT global_id FROM el)");
            Exec(cn, ViewSql);
            Exec(cn, "INSERT OR REPLACE INTO meta VALUES('schema_version', @v)", ("@v", SchemaVersion.ToString()));
            Exec(cn, "INSERT OR REPLACE INTO meta VALUES('project', @v)", ("@v", project.Name));
            Exec(cn, "INSERT OR REPLACE INTO meta VALUES('model_file', @v)", ("@v", modelFile ?? ""));
            Exec(cn, "INSERT OR REPLACE INTO meta VALUES('built_at', @v)", ("@v", DateTime.UtcNow.ToString("o")));
            Exec(cn, "INSERT OR REPLACE INTO meta VALUES('station_style', @v)", ("@v", project.Units.IsUS ? "US" : "SI"));
            Exec(cn, "INSERT OR REPLACE INTO meta VALUES('foot_m', @v)", ("@v", project.Units.FootInMeters.ToString("R", CultureInfo.InvariantCulture)));
            Exec(cn, "INSERT OR REPLACE INTO meta VALUES('coordinates', @v)", ("@v", "bbox in meters, project grid (state plane) coordinates; length_m/mass_kg/volume_m3 SI; stations in meters (display stations)"));
            if (vacuum) Exec(cn, "VACUUM");
        }
        result.Elapsed = sw.Elapsed;
        return result;
    }

    // ================================================================== writer

    private sealed class Row
    {
        public string GlobalId = "", Parent = "", Level = "", Category = "", Subtype = "", IfcClass = "", Name = "", Mark = "";
        public string BridgeGroup = "", Bridge = "", Unit = "", Support = "", Girder = "", Material = "", Props = "";
        public bool InIfc = true;
        public double StaBegin = double.NaN, StaEnd = double.NaN, Mass = double.NaN, Volume = double.NaN, Length = double.NaN;
        public Box3 Box = Box3.Empty;
        public object? Tag;
    }

    private sealed class Writer : IDisposable
    {
        private readonly SqliteCommand? _cmd, _strCmd, _propCmd;
        private readonly SqliteParameter[] _p = new SqliteParameter[28];
        private readonly SqliteParameter _sv = new("$v", null), _si = new("$i", null), _pv = new("$j", null), _pi = new("$k", null);
        private readonly DatabaseBuildResult _res;
        private readonly Dictionary<string, int> _strings = new();
        private readonly Dictionary<string, int> _props = new();
        private readonly Dictionary<string, int> _ids = new();
        private int _nextId;

        public Writer(SqliteConnection? cn, DatabaseBuildResult res, SqliteTransaction? tx = null)
        {
            _res = res;
            if (cn == null) return;
            string[] cols = { "global_id", "parent_id", "level_s", "category_s", "subtype_s", "ifc_class_s", "in_ifc", "name", "mark", "bridge_group_s", "bridge_s", "unit_s", "support_s", "girder_s",
                "station_begin", "station_end", "material_s", "mass_kg", "volume_m3", "length_m",
                "bbox_min_x", "bbox_min_y", "bbox_min_z", "bbox_max_x", "bbox_max_y", "bbox_max_z", "props_id", "id" };
            _cmd = cn.CreateCommand();
            _cmd.Transaction = tx;
            _cmd.CommandText = $"INSERT INTO el({string.Join(",", cols)}) VALUES({string.Join(",", cols.Select((_, i) => "$" + i))})";
            for (int i = 0; i < cols.Length; i++) _cmd.Parameters.Add(_p[i] = new SqliteParameter("$" + i, null));
            _cmd.Prepare();
            _strCmd = cn.CreateCommand(); _strCmd.Transaction = tx;
            _strCmd.CommandText = "INSERT INTO strings(id, value) VALUES($i, $v)";
            _strCmd.Parameters.Add(_si); _strCmd.Parameters.Add(_sv);
            _propCmd = cn.CreateCommand(); _propCmd.Transaction = tx;
            _propCmd.CommandText = "INSERT INTO props(id, json) VALUES($k, $j)";
            _propCmd.Parameters.Add(_pi); _propCmd.Parameters.Add(_pv);
        }

        private object S(string s)
        {
            if (s.Length == 0) return DBNull.Value;
            if (!_strings.TryGetValue(s, out int id))
            {
                _strings[s] = id = _strings.Count + 1;
                _si.Value = id; _sv.Value = s; _strCmd!.ExecuteNonQuery();
            }
            return id;
        }

        private object P(string json)
        {
            if (json.Length == 0) return DBNull.Value;
            if (!_props.TryGetValue(json, out int id))
            {
                _props[json] = id = _props.Count + 1;
                _pi.Value = id; _pv.Value = json; _propCmd!.ExecuteNonQuery();
            }
            return id;
        }

        private static object N(string s) => s.Length == 0 ? DBNull.Value : s;
        private static object D(double d) => double.IsNaN(d) || double.IsInfinity(d) ? DBNull.Value : Math.Round(d, 6);

        public void Add(Row r)
        {
            if (r.Tag != null) _res.Tags.Add(r.Tag, r.GlobalId);
            _res.Elements++;
            _res.ByLevel[r.Level] = _res.ByLevel.GetValueOrDefault(r.Level) + 1;
            string c = r.Level + ": " + r.Category;
            _res.ByCategory[c] = _res.ByCategory.GetValueOrDefault(c) + 1;
            if (_cmd == null) return;
            int id = ++_nextId;
            _ids[r.GlobalId] = id;
            _p[0].Value = r.GlobalId;
            _p[1].Value = r.Parent.Length > 0 && _ids.TryGetValue(r.Parent, out int pid) ? pid : DBNull.Value;
            _p[2].Value = S(r.Level); _p[3].Value = S(r.Category); _p[4].Value = S(r.Subtype); _p[5].Value = S(r.IfcClass);
            _p[6].Value = r.InIfc ? 1 : 0; _p[7].Value = N(r.Level == "part" && r.Name == r.Mark && r.IfcClass != "IFCBRIDGEPART" ? "" : r.Name); _p[8].Value = N(r.Mark);
            _p[9].Value = S(r.BridgeGroup); _p[10].Value = S(r.Bridge); _p[11].Value = S(r.Unit); _p[12].Value = S(r.Support); _p[13].Value = S(r.Girder);
            _p[14].Value = D(r.StaBegin); _p[15].Value = D(r.StaEnd); _p[16].Value = S(r.Material);
            _p[17].Value = D(r.Mass); _p[18].Value = D(r.Volume); _p[19].Value = D(r.Length);
            bool has = !r.Box.IsEmpty;
            _p[20].Value = has ? Math.Round(r.Box.Min.X, 4) : DBNull.Value; _p[21].Value = has ? Math.Round(r.Box.Min.Y, 4) : DBNull.Value; _p[22].Value = has ? Math.Round(r.Box.Min.Z, 4) : DBNull.Value;
            _p[23].Value = has ? Math.Round(r.Box.Max.X, 4) : DBNull.Value; _p[24].Value = has ? Math.Round(r.Box.Max.Y, 4) : DBNull.Value; _p[25].Value = has ? Math.Round(r.Box.Max.Z, 4) : DBNull.Value;
            _p[26].Value = P(r.Props);
            _p[27].Value = id;
            _cmd.ExecuteNonQuery();
        }

        public void Dispose() { _cmd?.Dispose(); _strCmd?.Dispose(); _propCmd?.Dispose(); }
    }

    // ================================================================== JSON helper

    private sealed class Json
    {
        private readonly StringBuilder _sb = new("{");
        public Json Add(string k, string? v)
        {
            if (string.IsNullOrEmpty(v)) return this;
            Key(k); _sb.Append('"');
            foreach (char c in v)
            {
                if (c is '"' or '\\') _sb.Append('\\').Append(c);
                else if (c < 0x20) _sb.Append(' ');
                else _sb.Append(c);
            }
            _sb.Append('"');
            return this;
        }
        public Json Add(string k, double v, int dec = 5)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return this;
            Key(k); _sb.Append(Math.Round(v, dec).ToString(CultureInfo.InvariantCulture));
            return this;
        }
        public Json Add(string k, int v) { Key(k); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public Json Add(string k, bool v) { Key(k); _sb.Append(v ? "true" : "false"); return this; }
        private void Key(string k) { if (_sb.Length > 1) _sb.Append(','); _sb.Append('"').Append(k).Append("\":"); }
        public override string ToString() => _sb.Length == 1 ? "" : _sb.Append('}').ToString();
    }

    // ================================================================== model walk

    private sealed class Builder
    {
        private readonly Writer _w;
        private readonly BridgeProject _project;
        private readonly string _key;
        private Alignment? _al;
        private string _bridgeGroup = "", _bridge = "", _unit = "";

        private readonly bool _keysOnly;
        public Builder(Writer w, BridgeProject project, bool keysOnly) { _w = w; _project = project; _key = project.Name; _keysOnly = keysOnly; }

        private Row New(string gid, string parent, string level, string category, string ifcClass, string name) => new()
        {
            GlobalId = gid, Parent = parent, Level = level, Category = category, IfcClass = ifcClass, Name = name,
            BridgeGroup = _bridgeGroup, Bridge = _bridge, Unit = _unit,
        };

        private static string Id(string key) => IfcKeys.GlobalId(key);

        private void StationOf(Row r, Box3 b)
        {
            if (_keysOnly || _al == null || b.IsEmpty) return;
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var x in new[] { b.Min.X, b.Max.X })
                foreach (var y in new[] { b.Min.Y, b.Max.Y })
                {
                    double s = _al.Inverse(new Vec2(x, y)).Station;
                    lo = Math.Min(lo, s); hi = Math.Max(hi, s);
                }
            SetStation(r, lo, hi);
        }

        private void SetStation(Row r, double lo, double hi)
        {
            r.StaBegin = lo; r.StaEnd = hi;
        }

        public void Run(IReadOnlyList<IfcBridgeData> bridges)
        {
            string projId = Id(IfcKeys.Project(_key));
            string siteId = Id(_key + "/site");
            _w.Add(New(projId, "", "project", "project", "IFCPROJECT", _project.Name));
            _w.Add(New(siteId, projId, "site", "site", "IFCSITE", "Site"));
            foreach (var al in _project.Alignments.Where(a => a.Elements.Count > 0))
            {
                var r = New(Id(IfcKeys.Alignment(_key, al.Name)), siteId, "alignment", "alignment", "IFCALIGNMENT", al.Name);
                r.Length = al.Length; r.BridgeGroup = al.Name;
                SetStation(r, al.StartStation, al.EndStation);
                _w.Add(r);
            }
            var groups = new HashSet<string>();
            foreach (var b in bridges)
            {
                string name = b.Framing.Definition.Name;
                var m = UnitName.Match(name);
                _bridgeGroup = m.Success ? m.Groups["g"].Value : name;
                _unit = m.Success ? "Unit " + m.Groups["n"].Value : "";
                _bridge = name;
                string parent = siteId;
                if (m.Success)
                {
                    parent = Id(_key + "/bridgegroup/" + _bridgeGroup);
                    if (groups.Add(_bridgeGroup))
                    {
                        _unit = "";
                        var gr = New(parent, siteId, "bridge", "bridge", "", _bridgeGroup);
                        gr.InIfc = false;
                        gr.Bridge = "";
                        _w.Add(gr);
                        _unit = "Unit " + m.Groups["n"].Value;
                    }
                }
                Bridge(b, parent);
            }
        }

        private void Bridge(IfcBridgeData b, string parent)
        {
            var framing = b.Framing;
            var def = framing.Definition;
            _al = framing.Alignment;
            string bk = IfcKeys.Bridge(_key, def.Name);
            string unitId = Id(bk);
            var ur = New(unitId, parent, "unit", "unit", "IFCBRIDGE", def.Name);
            if (framing.Supports.Count > 0) SetStation(ur, framing.Supports[0].Station, framing.Supports[^1].Station);
            ur.Props = new Json().Add("alignment", def.AlignmentName).Add("spans", framing.Supports.Count - 1).Add("girders", def.Girders.Count).ToString();
            _w.Add(ur);

            string superId = Id(IfcKeys.SuperPart(bk));
            _w.Add(Part(New(superId, unitId, "part", "superstructure", "IFCBRIDGEPART", "Superstructure"), ur));
            string deckPartId = "";
            if (b.Concrete != null)
            {
                deckPartId = Id(IfcKeys.DeckPart(bk));
                _w.Add(Part(New(deckPartId, unitId, "part", "deck", "IFCBRIDGEPART", "Deck"), ur));
            }
            string subId = "";
            if (b.Substructure is { Units.Count: > 0 })
            {
                subId = Id(IfcKeys.SubPart(bk));
                _w.Add(Part(New(subId, unitId, "part", "substructure", "IFCBRIDGEPART", "Substructure"), ur));
            }
            if (b.Substructure != null && subId.Length > 0) Substructure(b.Substructure, bk, subId, superId);
            string deckSlabId = "";
            if (b.Concrete != null) deckSlabId = Concrete(b.Concrete, def, bk, deckPartId);
            if (b.Prestressed != null) Prestressed(b.Prestressed, bk, superId);
            if (b.Concrete?.Segmental is { } sgm) Segmental(sgm, bk, superId, def.Name);
            if (b.Steel != null) Steel(b.Steel, bk, superId, framing.Camber);
            if (b.Substructure?.Misc is { IsEmpty: false } misc) MiscItems(misc, bk, superId, def.Name);
            if (b.Substructure?.SurfaceTreatments is { IsEmpty: false } stm) SurfaceTreatments(stm, bk, unitId, def.Name);
            _ = deckSlabId;
        }

        /// <summary>Surface treatments: one group per treatment (plan quantity vs model area in its properties), one element per region (IfcCovering).</summary>
        private void SurfaceTreatments(SBM.Core.Surface.SurfaceTreatmentModel stm, string bk, string unitId, string bridgeName)
        {
            const double sf = 0.09290304;
            var inv = CultureInfo.InvariantCulture;
            foreach (var (t, area) in stm.Totals.ToList())
            {
                var pj = new Json().Add("treatment_kind", t.Kind.ToString()).Add("pattern", t.Pattern).Add("orientation", t.Orientation).Add("model_area_sf", Math.Round(area / sf, 1));
                if (!double.IsNaN(t.PlanQuantity)) pj.Add("plan_quantity_sf", Math.Round(t.PlanQuantity / sf, 1)).Add("model_minus_plan_sf", Math.Round((area - t.PlanQuantity) / sf, 1));
                pj.Add("quantity_item", t.QuantityItem).Add("source", t.Source);
                string gid = Group($"{bk}/grp/surface/{t.Name}", unitId, $"{bridgeName} {t.Name}", "surface treatment", props: pj.ToString());
                foreach (var it in stm.Items.Where(i => i.Treatment == t))
                {
                    bool brick = t.Kind == SBM.Core.Bridge.SurfaceTreatmentKind.ThinBrick;
                    var r = New(Id(IfcKeys.SurfaceTreatment(bk, it.Name)), gid, "element", brick ? "thin brick" : "form liner", "IFCCOVERING", it.Name);
                    r.Tag = it;
                    r.Support = it.Region.Support;
                    if (_keysOnly) { _w.Add(r); continue; }
                    r.Material = brick ? "Integral thin veneer brick" : "Concrete, form liner texture";
                    r.Volume = Math.Abs(it.Mesh.Volume());
                    if (!it.Mesh.IsEmpty) { r.Box = it.Mesh.Bounds(); StationOf(r, r.Box); }
                    var ej = new Json().Add("area_sf", Math.Round(it.Area / sf, 2)).Add("relief_in", Math.Round(t.Relief / 0.0254, 3)).Add("host", it.Region.Host.ToString())
                        .Add("region_source", it.Region.Source).Add("description", it.Description);
                    if (it.Assumption is { } asu) ej.Add("status", asu.Status).Add("reason", asu.Reason).Add("assumed", asu.Basis);
                    r.Props = ej.ToString();
                    _w.Add(r);
                }
            }
            _ = inv;
        }

        /// <summary>Camber / blocking of a girder as properties of its group: summary values plus the point table.</summary>
        private static string CamberProps(SBM.Core.Framing.GirderCamberResult cg)
        {
            const double ft = 0.3048, inch = 0.0254;
            var inv = CultureInfo.InvariantCulture;
            var rows = cg.Points.Select(p => string.Join(",", p.Number.ToString(inv), (p.PathDistance / ft).ToString("F3", inv), (p.Camber / inch).ToString("F2", inv),
                (p.SteelDeflection / inch).ToString("F2", inv), (p.SlabDeflection / inch).ToString("F2", inv), (p.HaunchElevation / ft).ToString("F3", inv),
                double.IsNaN(p.Blocking) ? "" : (p.Blocking / ft).ToString("F3", inv)));
            var bl = cg.Points.Where(p => !double.IsNaN(p.Blocking)).ToList();
            var j = new Json().Add("camber_points", cg.Points.Count).Add("camber_datum_point", cg.DatumPoint)
                .Add("camber_max_in", cg.Points.Max(p => p.Camber) / inch).Add("camber_min_in", cg.Points.Min(p => p.Camber) / inch);
            if (bl.Count > 0) j.Add("blocking_max_ft", bl.Max(p => p.Blocking) / ft).Add("blocking_min_ft", bl.Min(p => p.Blocking) / ft);
            return j.Add("camber_table_columns", "point,distance_along_girder_ft,camber_in(up+),steel_deflection_in(down+),slab_barrier_deflection_in(down+),girder_line_haunch_elevation_ft,blocking_ft")
                .Add("camber_table", string.Join(";", rows)).ToString();
        }

        private static Row Part(Row r, Row from) { r.StaBegin = from.StaBegin; r.StaEnd = from.StaEnd; return r; }

        // ------------------------------------------------------------ steel

        private sealed record AsmInfo(string GlobalId, string Support, string Girder, string Subtype);

        private void Steel(SteelModel steel, string bk, string superId, SBM.Core.Framing.CamberResult? camber = null)
        {
            var asmInfo = new Dictionary<string, AsmInfo>();
            var girderGroup = new Dictionary<int, string>();
            string cfGroup = "", lbGroup = "", inspGroup = "", archGroup = "", railGroup = "", jointHwGroup = "";
            // OPTIONAL integral steel pier caps: one group per cap (support from the report), holding the cap, its connections and its bearings
            var capGroups = new Dictionary<string, (string Id, string Support)>();
            (string Id, string Support) CapGroup(string capMark)
            {
                if (capGroups.TryGetValue(capMark, out var cg)) return cg;
                var rep = steel.IntegralCaps.FirstOrDefault(c => c.Mark == capMark);
                string sup = rep?.Support ?? "";
                return capGroups[capMark] = (Group($"{bk}/group/cap/{capMark}", superId, $"Integral steel pier cap {capMark}", "integral steel cap group", sup), sup);
            }
            string? CapOfBearing(SteelAssembly a) => a.Girder < 0 && a.Description.Contains("integral steel cap")
                ? steel.IntegralCaps.FirstOrDefault(c => a.Description.Contains(", " + c.Support + ","))?.Mark : null;
            string ArchGroup() => archGroup.Length > 0 ? archGroup : (archGroup = Group($"{bk}/group/arch", superId, "Tied arch", "tied arch"));
            // Steel truss (Truss/TrussGenerator): group "truss" > truss line / bracing / floor / suspender / shoe assemblies > panel / joint groups > members
            string trussGroup = "";
            string TrussGroup() => trussGroup.Length > 0 ? trussGroup : (trussGroup = Group($"{bk}/group/truss", superId, steel.Truss?.Name ?? "Truss", "truss"));
            string GirderGroup(int g)
            {
                if (girderGroup.TryGetValue(g, out var id)) return id;
                id = Id($"{bk}/girder/G{g + 1}");
                var r = New(id, superId, "group", "girder", "", $"G{g + 1}");
                r.InIfc = false; r.Girder = $"G{g + 1}";
                if (camber?.Girder(g) is { Points.Count: > 0 } cg) r.Props = CamberProps(cg);
                _w.Add(r);
                return girderGroup[g] = id;
            }
            foreach (var asm in steel.Assemblies)
            {
                string gid = Id(IfcKeys.Assembly(bk, asm.Mark));
                string parent, support = "", girder = asm.Girder >= 0 ? $"G{asm.Girder + 1}" : "", sub = "", cat;
                var props = new Json().Add("description", asm.Description);
                switch (asm.Kind)
                {
                    // Tied arch (Arch/ArchGenerator): all assemblies under the "Tied arch" group; bearings name their pier in the description.
                    case AssemblyKind.Bearing when steel.Arch != null:
                        parent = ArchGroup(); cat = "bearing"; sub = "arch bearing";
                        { var mm = Regex.Match(asm.Description, @" at (.+)$"); if (mm.Success) support = mm.Groups[1].Value; }
                        break;
                    case AssemblyKind.Bearing when steel.Truss != null:
                        parent = TrussGroup(); cat = "bearing"; sub = "truss shoe";
                        { var mm = Regex.Match(asm.Description, @" at (.+)$"); if (mm.Success) support = mm.Groups[1].Value; }
                        break;
                    case AssemblyKind.TrussLine: parent = TrussGroup(); cat = "truss line"; break;
                    case AssemblyKind.PlateGirderLine: parent = TrussGroup(); cat = "plate girder"; break;
                    case AssemblyKind.TrussBracing: parent = TrussGroup(); cat = "truss bracing"; sub = asm.Description.Contains(" - ") ? asm.Description[(asm.Description.LastIndexOf(" - ", StringComparison.Ordinal) + 3)..] : ""; break;
                    case AssemblyKind.TrussFloor: parent = TrussGroup(); cat = "floor system"; break;
                    case AssemblyKind.TrussSuspender: parent = TrussGroup(); cat = "suspender"; break;
                    case AssemblyKind.ArchRib: parent = ArchGroup(); cat = "arch rib"; break;
                    case AssemblyKind.ArchTie: parent = ArchGroup(); cat = "tie girder"; break;
                    case AssemblyKind.ArchHanger: parent = ArchGroup(); cat = "hanger"; break;
                    case AssemblyKind.ArchFloorbeam: parent = ArchGroup(); cat = "floorbeam"; break;
                    case AssemblyKind.ArchBracing: parent = ArchGroup(); cat = "upper lateral bracing"; break;
                    case AssemblyKind.ArchKnuckle: parent = ArchGroup(); cat = "knuckle"; break;
                    case AssemblyKind.ArchDeckForm: parent = ArchGroup(); cat = "deck form"; break;
                    case AssemblyKind.ArchSplice: parent = ArchGroup(); cat = "arch field splice"; sub = asm.Description.Contains("tie") ? "tie splice" : "rib splice"; break;
                    case AssemblyKind.GirderFieldSection:
                        parent = GirderGroup(asm.Girder); cat = "girder field section";
                        {
                            var mm = Regex.Match(asm.Mark, @"-(\d+)$");
                            if (mm.Success) props.Add("field_section", int.Parse(mm.Groups[1].Value));
                        }
                        break;
                    case AssemblyKind.FieldSplice:
                        parent = GirderGroup(asm.Girder); cat = "field splice";
                        {
                            var mm = Regex.Match(asm.Description, @"splice (\d+)");
                            if (mm.Success) { props.Add("splice_no", int.Parse(mm.Groups[1].Value)); sub = "FS" + mm.Groups[1].Value; }
                        }
                        break;
                    case AssemblyKind.PierCap:
                        { var cg = CapGroup(asm.Mark); parent = cg.Id; support = cg.Support; cat = "integral steel pier cap"; }
                        break;
                    case AssemblyKind.PierCapConnection:
                        { var cg = CapGroup(asm.Mark[..asm.Mark.LastIndexOf("-C", StringComparison.Ordinal)]); parent = cg.Id; support = cg.Support; cat = "pier cap connection"; }
                        break;
                    case AssemblyKind.Bearing when CapOfBearing(asm) is { } capMark:
                        { var cg = CapGroup(capMark); parent = cg.Id; support = cg.Support; cat = "bearing"; sub = asm.Description.Split(", ")[0]; }
                        break;
                    case AssemblyKind.Bearing:
                        parent = GirderGroup(asm.Girder); cat = "bearing";
                        {
                            var parts = asm.Description.Split(", ");
                            if (parts.Length >= 3) support = parts[^2];
                            sub = parts[0].Contains('(') ? parts[0][(parts[0].IndexOf('(') + 1)..].TrimEnd(')') : "";
                        }
                        break;
                    case AssemblyKind.InspectionCable:
                    case AssemblyKind.InspectionWalkway:
                        parent = inspGroup.Length > 0 ? inspGroup : (inspGroup = Group($"{bk}/group/inspection", superId, "Inspection access", "inspection access group"));
                        cat = asm.Kind == AssemblyKind.InspectionCable ? "inspection cable" : "inspection walkway";
                        sub = asm.Kind == AssemblyKind.InspectionCable ? "wire rope cable" : asm.Description.Contains("Type B") ? "Type B walkway" : "Type A walkway";
                        break;
                    case AssemblyKind.Railing:
                        parent = railGroup.Length > 0 ? railGroup : (railGroup = Group($"{bk}/group/railings", superId, "Pedestrian railings", "railing group"));
                        cat = "pedestrian railing"; sub = asm.ObjectType ?? "";
                        break;
                    case AssemblyKind.ExpansionJoint:
                        parent = jointHwGroup.Length > 0 ? jointHwGroup : (jointHwGroup = Group($"{bk}/group/joint-hardware", superId, "Expansion joint hardware", "joint hardware group"));
                        cat = "expansion joint hardware"; sub = asm.ObjectType ?? "";
                        { var mm = Regex.Match(asm.Description, @" at (.+)$"); if (mm.Success) support = mm.Groups[1].Value; }
                        break;
                    case AssemblyKind.FlangeDeflector:
                        parent = GirderGroup(asm.Girder); cat = "flange deflector";
                        break;
                    case AssemblyKind.LateralBracing:
                        parent = lbGroup.Length > 0 ? lbGroup : (lbGroup = Group($"{bk}/group/lateral", superId, "Lateral bracing", "lateral bracing group"));
                        cat = "lateral bracing";
                        break;
                    default:
                        parent = cfGroup.Length > 0 ? cfGroup : (cfGroup = Group($"{bk}/group/crossframes", superId, "Cross frames", "cross frame group"));
                        var xg = XgName.Match(asm.Description);
                        var cf = CfName.Match(asm.Description);
                        if (xg.Success)
                        {
                            cat = "cross girder"; girder = "G" + xg.Groups["g"].Value; sub = "cross girder";
                            props.Add("girder_left", "G" + xg.Groups["a"].Value).Add("girder_right", "G" + xg.Groups["b"].Value);
                        }
                        else
                        {
                            cat = "cross frame";
                            if (cf.Success)
                            {
                                sub = cf.Groups["type"].Value;
                                girder = $"G{cf.Groups["a"].Value}-G{cf.Groups["b"].Value}";
                                props.Add("girder_left", "G" + cf.Groups["a"].Value).Add("girder_right", "G" + cf.Groups["b"].Value);
                                if (cf.Groups["span"].Success) props.Add("span", int.Parse(cf.Groups["span"].Value));
                            }
                            if (sub.StartsWith("Support")) props.Add("at_support", true);
                        }
                        break;
                }
                var r = New(gid, parent, "assembly", cat, asm.Kind == AssemblyKind.Railing ? "IFCRAILING" : "IFCELEMENTASSEMBLY", asm.Mark);
                r.Mark = asm.Mark; r.Girder = girder; r.Support = support; r.Subtype = sub; r.Props = props.ToString();
                _w.Add(r);
                asmInfo[asm.Mark] = new AsmInfo(gid, support, girder, sub);
            }

            // Groups per assembly for the many-instance items: "FS4 bolts", "G1-1 studs", "B-Pier1WB-G1 welds" ... (links / QC live on them or higher).
            var asmGroups = new Dictionary<(string, string), string>();
            string AsmGroup(string asmMark, string kind, string label, string cat)
            {
                if (asmGroups.TryGetValue((asmMark, kind), out var g)) return g;
                asmInfo.TryGetValue(asmMark, out var ai0);
                string id = Id($"{bk}/grp/{asmMark}/{kind}");
                var gr = New(id, ai0?.GlobalId ?? superId, "group", cat, "", $"{asmMark} {label}");
                gr.InIfc = false; gr.Support = ai0?.Support ?? ""; gr.Girder = ai0?.Girder ?? "";
                _w.Add(gr);
                return asmGroups[(asmMark, kind)] = id;
            }

            // parts, with quantities and station extent computed per assembly from the parts' bounding boxes afterwards (kept simple: per part)
            foreach (var part in steel.Parts)
            {
                string gid = Id(IfcKeys.Part(bk, part.Mark));
                asmInfo.TryGetValue(part.Assembly, out var ai);
                string partParent = part.Category == PartCategory.Weld && ai != null ? AsmGroup(part.Assembly, "welds", "welds", "weld group")
                    : ai != null && steel.Truss != null && steel.Truss.PanelOf.TryGetValue(part.Mark, out var panel) ? AsmGroup(part.Assembly, "panel/" + panel, panel, panel.StartsWith("Joint") ? "truss joint" : "truss panel")
                    : ai?.GlobalId ?? superId;
                var r = New(gid, partParent, "part", PartCategoryName(part.Category), IfcClassOf(part), part.Mark);
                r.InIfc = !part.Mesh.IsEmpty;
                r.Mark = part.Mark; r.Material = part.Material; r.Support = ai?.Support ?? ""; r.Girder = ai?.Girder ?? "";
                r.Tag = part;
                if (_keysOnly) { _w.Add(r); continue; }
                r.Mass = part.Mass; r.Length = part.Length;
                if (!part.Mesh.IsEmpty)
                {
                    r.Volume = part.Volume;
                    r.Box = part.Mesh.Bounds();
                    StationOf(r, r.Box);
                }
                var pjs = new Json().Add("description", part.Description).Add("assembly", part.Assembly)
                    .Add("role", part.Role == MemberRole.None ? null : part.Role.ToString()).Add("lb_per_ft", part.NominalWeightLbPerFt, 3);
                if (part.Assumption is { } pas && (SteelModel.IsRailing(part.Category) || IsCapPart(part) || SteelModel.IsJointHardware(part.Category) || pas.DimensionOnly)) pjs.Add("status", pas.Status).Add("reason", pas.Reason).Add("assumed", pas.Basis);
                if (part.Fabrication is { IsEmpty: false } fab) foreach (var (fn, fv) in fab.Fields()) pjs.Add("fab_" + fn.ToLowerInvariant(), fv);
                r.Props = pjs.ToString();
                _w.Add(r);
            }

            // bolts, anchor bolts, studs: numbered exactly like the IFC export (bolts then studs, one running counter)
            int no = 0;
            var perAsm = new Dictionary<string, int>();
            string boltMat = steel.BoltGrade.StartsWith("ASTM") ? steel.BoltGrade : $"ASTM F3125 Gr. {steel.BoltGrade}";
            void Fastener(object tagObj, int kind, Frame3 f, string asmMark, double grip, double len, double dia, double embed)
            {
                string gid = Id(IfcKeys.Fastener(bk, kind, asmMark, no++));
                asmInfo.TryGetValue(asmMark, out var ai);
                int idx = perAsm[asmMark] = perAsm.GetValueOrDefault(asmMark) + 1;
                string cat = kind switch { 1 => "anchor bolt", 2 => "shear stud", 4 => "rivet", _ => "bolt" };
                string tag = kind switch { 1 => "AB", 2 => "SS", 4 => "R", _ => "B" };
                string fparent = ai == null ? superId : kind switch
                {
                    1 => AsmGroup(asmMark, "anchors", "anchor bolts", "anchor bolt group"),
                    2 => AsmGroup(asmMark, "studs", "shear studs", "stud group"),
                    4 => AsmGroup(asmMark, "rivets", "rivets", "rivet group"),
                    _ => AsmGroup(asmMark, "bolts", "bolts", "bolt group"),
                };
                var r = New(gid, fparent, "fastener", cat, "IFCMECHANICALFASTENER", "");
                r.Mark = $"{asmMark}-{tag}{idx}";
                r.Support = ai?.Support ?? ""; r.Girder = ai?.Girder ?? "";
                r.Material = kind switch { 1 => "ASTM F1554", 2 => "ASTM A108 (AWS D1.5 Type B)", 4 => (tagObj as BoltInstance)?.Material ?? "Rivet steel (high strength / carbon, per plan notes)", _ => boltMat };
                r.Tag = tagObj;
                if (_keysOnly) { _w.Add(r); return; }
                r.Length = len;
                r.Box.Add(f.Origin); r.Box.Add(f.Origin + f.Z * len);
                if (_al != null) { double s = _al.Inverse(new Vec2(f.Origin.X, f.Origin.Y)).Station; SetStation(r, s, s); }
                var fj = new Json().Add("diameter", kind is 1 or 4 ? dia : kind == 2 ? steel.StudDiameter : steel.Bolt.Diameter).Add("grip", grip)
                    .Add("embedment", embed);
                if ((tagObj as BoltInstance)?.Assumption is { } fas) fj.Add("status", fas.Status).Add("reason", fas.Reason).Add("assumed", fas.Basis);
                r.Props = fj.ToString();
                _w.Add(r);
            }
            foreach (var bolt in steel.Bolts)
                Fastener(bolt, bolt.Kind == BoltKind.Anchor ? 1 : bolt.Kind == BoltKind.Rivet ? 4 : 0, bolt.Frame, bolt.Assembly, bolt.Grip, bolt.Length, bolt.Diameter, bolt.Embedment);
            foreach (var st in steel.Studs)
                Fastener(st, 2, st.Frame, st.Assembly, 0, st.Length, steel.StudDiameter, 0);
        }

        private string Group(string key, string parent, string name, string category, string support = "", string subtype = "", string props = "")
        {
            string id = Id(key);
            var r = New(id, parent, "group", category, "", name);
            r.InIfc = false; r.Support = support; r.Subtype = subtype;
            if (props.Length > 0) r.Props = props;
            _w.Add(r);
            return id;
        }

        private static string PartCategoryName(PartCategory c) => c switch
        {
            PartCategory.FlangePlate => "flange plate", PartCategory.WebPlate => "web plate",
            PartCategory.BearingStiffener => "bearing stiffener", PartCategory.ConnectionStiffener => "connection stiffener",
            PartCategory.IntermediateStiffener => "intermediate stiffener", PartCategory.Gusset => "gusset",
            PartCategory.FillerPlate => "filler plate", PartCategory.Angle => "angle", PartCategory.Channel => "channel",
            PartCategory.SolePlate => "sole plate", PartCategory.BearingPad => "bearing pad", PartCategory.Weld => "weld",
            PartCategory.SplicePlate => "splice plate", PartCategory.BearingPlate => "bearing plate", PartCategory.MasonryPlate => "masonry plate",
            PartCategory.BearingDisc => "bearing disc", PartCategory.ShearPin => "shear pin", PartCategory.GuideBar => "guide bar",
            PartCategory.Ptfe => "PTFE surface",
            PartCategory.InspectionBracket => "cable bracket", PartCategory.InspectionCable => "wire rope", PartCategory.Grating => "grating",
            PartCategory.WalkwayStringer => "walkway stringer", PartCategory.WalkwayPost => "walkway post", PartCategory.WalkwayRail => "walkway rail",
            PartCategory.WalkwayPlate => "walkway plate",
            PartCategory.ArchRib => "arch rib", PartCategory.TiePlate => "tie girder plate", PartCategory.FloorbeamPlate => "floorbeam plate",
            PartCategory.Hanger => "hanger strand", PartCategory.HangerSocket => "hanger socket", PartCategory.ArchBracing => "upper lateral bracing",
            PartCategory.KnucklePlate => "knuckle plate", PartCategory.FormDeck => "steel deck form",
            PartCategory.FinPlate => "cable fin plate", PartCategory.CableConnector => "cable connector", PartCategory.ArchConnectionPlate => "connection plate",
            PartCategory.ArchStiffener => "stiffener", PartCategory.PtStrand => "PT strand", PartCategory.PtAnchorage => "PT anchorage",
            PartCategory.RolledTee => "tee", PartCategory.AccessDoor => "access door",
            PartCategory.RailPost => "railing post", PartCategory.RailHandrail => "handrail", PartCategory.RailMember => "rail member",
            PartCategory.RailBasePlate => "post base plate", PartCategory.RailEnvelope => "railing envelope", PartCategory.RailFabric => "railing fence fabric",
            PartCategory.EndPlate => "end plate", PartCategory.ConnectorPlate => "connector plate", PartCategory.DiaphragmPlate => "connection diaphragm plate",
            PartCategory.ContinuityPlate => "mill-to-bear plate", PartCategory.CoverPlate => "cover plate", PartCategory.HandholeCover => "handhole cover",
            PartCategory.HandholeBacking => "handhole backing plate", PartCategory.MeshScreen => "wire mesh screen",
            PartCategory.JointArmor => "joint armor plate", PartCategory.JointStud => "joint stud anchor", PartCategory.JointCoverPlate => "joint cover plate",
            PartCategory.JointAnchor => "cover plate anchor",
            PartCategory.TrussGirder => "plate girder", PartCategory.TrussChord => "truss chord", PartCategory.TrussDiagonal => "truss diagonal", PartCategory.TrussVertical => "truss vertical",
            PartCategory.TrussHanger => "truss hanger", PartCategory.TrussBracing => "truss bracing member", PartCategory.TrussFloorBeam => "floor beam",
            PartCategory.TrussStringer => "stringer", PartCategory.TrussGusset => "gusset plate", PartCategory.Suspender => "suspender rope",
            PartCategory.SuspenderSocket => "suspender socket", PartCategory.TrussPin => "pin", PartCategory.TrussShoeFixed => "fixed shoe",
            PartCategory.TrussShoeExpansion => "expansion shoe", PartCategory.TrussConnectionAngle => "connection angle",
            PartCategory.TrussConnectionPlate => "connection plate", _ => c.ToString(),
        };

        private static bool IsCapPart(SteelPart p) => p.Category is PartCategory.EndPlate or PartCategory.ConnectorPlate or PartCategory.DiaphragmPlate
            or PartCategory.ContinuityPlate or PartCategory.CoverPlate or PartCategory.HandholeCover or PartCategory.HandholeBacking or PartCategory.MeshScreen;

        private static string IfcClassOf(SteelPart p) => p.Category switch
        {
            PartCategory.Angle or PartCategory.Channel or PartCategory.InspectionBracket or PartCategory.InspectionCable
                or PartCategory.WalkwayStringer or PartCategory.WalkwayPost or PartCategory.RolledTee
                or PartCategory.ArchRib or PartCategory.Hanger or PartCategory.ArchBracing or PartCategory.RailPost or PartCategory.RailMember
                or PartCategory.TrussChord or PartCategory.TrussDiagonal or PartCategory.TrussVertical or PartCategory.TrussHanger or PartCategory.TrussBracing
                or PartCategory.TrussStringer or PartCategory.Suspender or PartCategory.TrussConnectionAngle => "IFCMEMBER",
            PartCategory.TrussFloorBeam or PartCategory.TrussGirder => "IFCBEAM",
            PartCategory.TrussShoeFixed or PartCategory.TrussShoeExpansion => "IFCBEARING",
            PartCategory.SuspenderSocket or PartCategory.TrussPin => "IFCMECHANICALFASTENER",
            PartCategory.RailHandrail or PartCategory.RailEnvelope => "IFCRAILING",
            PartCategory.HangerSocket or PartCategory.CableConnector => "IFCMECHANICALFASTENER",
            PartCategory.PtStrand => "IFCTENDON",
            PartCategory.PtAnchorage => "IFCTENDONANCHOR",
            PartCategory.WalkwayRail => "IFCRAILING",
            PartCategory.BearingPad or PartCategory.BearingDisc => "IFCBEARING",
            PartCategory.ShearPin or PartCategory.JointStud or PartCategory.JointAnchor => "IFCMECHANICALFASTENER",
            PartCategory.Weld => "IFCFASTENER",
            _ => "IFCPLATE",
        };

        // ------------------------------------------------------------ concrete (deck)

        private string Concrete(ConcreteModel c, SBM.Core.Bridge.BridgeDefinition def, string bk, string deckPartId)
        {
            string slabId = "";
            string concMat = $"Concrete, f'c = {def.Deck.Reinforcement.ConcreteStrengthKsi:0.#} ksi";
            string who = _unit.Length > 0 ? $"{_bridgeGroup} {_unit}" : def.Name;
            if (c.Deck != null)
            {
                slabId = Id(IfcKeys.DeckSlab(bk));
                var r = New(slabId, deckPartId, "element", "deck slab", "IFCSLAB", "Deck slab");
                r.Material = concMat; r.Volume = c.DeckVolume + c.HaunchVolume + c.DeckBumpVolume; r.Mass = r.Volume * 2400;
                r.Box = c.Deck.Bounds(); if (c.Haunches is { IsEmpty: false } h) { var hb = h.Bounds(); r.Box.Add(hb.Min); r.Box.Add(hb.Max); }
                StationOf(r, r.Box);
                r.Props = new Json().Add("thickness_m", def.Deck.Thickness).Add("deck_volume_m3", c.DeckVolume).Add("haunch_volume_m3", c.HaunchVolume).ToString();
                _w.Add(r);
            }
            // concrete haunches over integral steel pier caps (same GlobalId key as the IFC IfcBuildingElementPart)
            foreach (var ch in c.CapHaunches.Where(x => !x.Mesh.IsEmpty))
            {
                var r = New(Id(IfcKeys.CapHaunch(bk, ch.Support)), deckPartId, "element", "pier cap haunch", "IFCBUILDINGELEMENTPART", ch.Name);
                r.Support = ch.Support; r.Subtype = ch.CapMark; r.Material = concMat; r.Volume = ch.Volume; r.Mass = r.Volume * 2400;
                r.Box = ch.Mesh.Bounds(); StationOf(r, r.Box);
                var hj = new Json().Add("width_m", ch.Width).Add("length_m", ch.S1 - ch.S0).Add("min_depth_m", ch.MinDepth).Add("max_depth_m", ch.MaxDepth)
                    .Add("pieces", ch.Pieces.Count).Add("source", ch.Source);
                foreach (var (bay, dep) in ch.BayDepths) hj.Add("depth_cl_" + bay.ToLowerInvariant().Replace(' ', '_'), dep);
                r.Props = hj.ToString();
                _w.Add(r);
            }
            // Groups at the granularity the plans use: one deck-reinforcing group per unit, one barrier group (concrete + its bars) per unit.
            string deckRebar = "", barrierGroup = "", barrierRebar = "", diaphragmRebar = "";
            // the group carries the unit's deck bar schedule (every mark, also the schedule-only rows of a plan bar list that have no 3D bar)
            string DeckRebarProps()
            {
                var railMarks = c.Bars.Where(b => b.Group == "Rail").Select(b => b.Mark).ToHashSet();
                var dm = c.Marks.Where(m => m.Unit == "" && !railMarks.Contains(m.Mark)).OrderBy(m => m.Mark, StringComparer.Ordinal).ToList();
                if (dm.Count == 0) return "";
                const double Lb = 2.2046226218;
                var placed = dm.Where(m => !m.Location.Contains("schedule only")).ToList();
                var sched = dm.Except(placed).ToList();
                return new Json().Add("plan_driven", def.Deck.Reinforcement.Plan != null)
                    .Add("marks", dm.Count).Add("bars_placed", placed.Sum(m => m.Count)).Add("placed_lb", placed.Sum(m => m.TotalMass) * Lb)
                    .Add("schedule_only_marks", string.Join(",", sched.Select(m => m.Mark))).Add("schedule_only_lb", sched.Sum(m => m.TotalMass) * Lb)
                    .Add("schedule_columns", "mark,size,count,length_ft(max),min_length_ft,weight_lb,placement")
                    .Add("schedule", string.Join(";", dm.Select(m => string.Join(",", m.Mark, m.Size.Name, m.Count.ToString(CultureInfo.InvariantCulture), (m.Length / 0.3048).ToString("0.00", CultureInfo.InvariantCulture),
                        (m.MinLength / 0.3048).ToString("0.00", CultureInfo.InvariantCulture), (m.TotalMass * Lb).ToString("0", CultureInfo.InvariantCulture), m.Location.Contains("schedule only") ? "schedule only" : "3D"))))
                    .ToString();
            }
            string DeckRebar() => deckRebar.Length > 0 ? deckRebar : deckRebar = Group($"{bk}/grp/deck-rebar", deckPartId, $"{who} deck reinforcing", "deck reinforcing", props: DeckRebarProps());
            string BarrierGroup() => barrierGroup.Length > 0 ? barrierGroup : barrierGroup = Group($"{bk}/grp/barriers", deckPartId, $"{who} barriers", "barrier group");
            string BarrierRebar() => barrierRebar.Length > 0 ? barrierRebar : barrierRebar = Group($"{bk}/grp/barrier-rebar", BarrierGroup(), $"{who} barrier reinforcing", "barrier reinforcing");
            // concrete end / pier diaphragm plan bars (PlanBarSet bars with Group "Diaphragm"); only bridges that have them get the group
            string DiaphragmRebar() => diaphragmRebar.Length > 0 ? diaphragmRebar : diaphragmRebar = Group($"{bk}/grp/diaphragm-rebar", deckPartId, $"{who} diaphragm reinforcing", "diaphragm reinforcing");
            foreach (var (name, mesh) in c.Barriers)
            {
                bool curb = def.Deck.Barrier.IsCurb;
                var r = New(Id(IfcKeys.Barrier(bk, name)), BarrierGroup(), "element", curb ? "curb" : "barrier", curb ? "IFCKERB" : "IFCRAILING", name);
                r.Material = concMat; r.Volume = Math.Abs(mesh.Volume()); r.Mass = r.Volume * 2400; r.Box = mesh.Bounds();
                StationOf(r, r.Box);
                r.Subtype = def.Deck.Barrier.Type.ToString();
                _w.Add(r);
            }
            int jn = 0;
            foreach (var jr in c.Joints)
            {
                string jk = IfcKeys.Joint(bk, jn++);
                string type = jr.Definition.TypeName;
                string jg = Group(jk + "/grp", deckPartId, $"Expansion joint at {jr.Support} ({(jr.AtBegin ? "begin" : "end")} of {who})", "joint", jr.Support, type);
                if (!jr.Blockout.IsEmpty)
                {
                    var r = New(Id(IfcKeys.JointBlockout(jk)), jg, "element", "joint blockout", "IFCBUILDINGELEMENTPART", jr.BlockoutName);
                    r.Support = jr.Support; r.Subtype = type; r.Material = concMat; r.Volume = jr.BlockoutVolume; r.Mass = r.Volume * 2400;
                    r.Box = jr.Blockout.Bounds(); StationOf(r, r.Box);
                    r.Props = new Json().Add("end", jr.AtBegin ? "begin" : "end").Add("joint_index", jn - 1).ToString();
                    _w.Add(r);
                }
                if (jr.EndBlock is { } eb && !eb.Mesh.IsEmpty && eb.Definition.Source is { Length: > 0 })
                {
                    // OPTIONAL thickened slab end with a source (e.g. TxDOT IGTS); same GlobalId key as the IFC IfcBuildingElementPart
                    var r = New(Id(jk + "/endblock"), jg, "element", "thickened slab end", "IFCBUILDINGELEMENTPART", eb.Name);
                    r.Support = jr.Support; r.Subtype = eb.Definition.Source; r.Material = concMat; r.Volume = eb.Volume; r.Mass = r.Volume * 2400;
                    r.Box = eb.Mesh.Bounds(); StationOf(r, r.Box);
                    var ej = new Json().Add("end", jr.AtBegin ? "begin" : "end");
                    if (eb.Assumption is { } eas) ej.Add("status", eas.Status).Add("reason", eas.Reason).Add("assumed", eas.Basis);
                    r.Props = ej.ToString();
                    _w.Add(r);
                }
                if (jr.Body != null)
                {
                    var d = New(Id(IfcKeys.JointDevice(jk)), jg, "element", "expansion joint device", "IFCDISCRETEACCESSORY", jr.DeviceName);
                    d.Support = jr.Support; d.Subtype = type; d.Material = "Steel (placeholder)"; d.Box = jr.Body.Bounds(); d.Length = jr.Length;
                    StationOf(d, d.Box);
                    var dj = new Json().Add("end", jr.AtBegin ? "begin" : "end").Add("placeholder", true);
                    if (jr.Definition.Designation is { Length: > 0 } des) dj.Add("designation", des);
                    if (jr.Definition.PlanLength is double jpl) dj.Add("plan_length_m", jpl);
                    if (jr.Assumption is { } jas) dj.Add("status", jas.Status).Add("reason", jas.Reason).Add("assumed", jas.Basis);
                    d.Props = dj.ToString();
                    _w.Add(d);
                }
            }
            Bars(c.Bars, c.Marks, (bar, m) => bar.Group == "Rail" ? BarrierRebar() : bar.Group == "Diaphragm" ? DiaphragmRebar() : DeckRebar(), i => IfcKeys.DeckBar(bk, i), def.Deck.Reinforcement.SteelYieldKsi, "");
            return slabId;
        }

        /// <summary>One row per bar placement (GlobalId = the IFC bar's); <paramref name="parentOf"/> picks the group a bar belongs to.</summary>
        private void Bars(IEnumerable<BarPlacement> bars, IEnumerable<BarMark> marks, Func<BarPlacement, BarMark, string> parentOf, Func<int, string> key, double fy, string support)
        {
            var mk = marks.GroupBy(m => m.Mark).ToDictionary(g => g.Key, g => g.First());
            int n = 0;
            foreach (var bar in bars)
            {
                var m = mk[bar.Mark];
                var r = New(Id(key(n++)), parentOf(bar, m), "bar", "rebar", "IFCREINFORCINGBAR", "");
                r.Mark = string.IsNullOrEmpty(m.Unit) ? m.Mark : $"{m.Unit} {m.Mark}";
                r.Support = support; r.Tag = bar;
                if (_keysOnly) { _w.Add(r); continue; }
                r.Material = $"ASTM A615 Gr. {fy:0}" + (m.Epoxy ? ", epoxy coated" : "");
                double len = 0;
                for (int i = 0; i + 1 < bar.Points.Length; i++) len += (bar.Points[i + 1] - bar.Points[i]).Length;
                r.Length = len; r.Mass = len * m.Size.MassPerLength;
                foreach (var p in bar.Points) r.Box.Add(p);
                r.Subtype = m.Size.Name;
                r.Props = new Json().Add("size", m.Size.Name).Add("shape", m.Shape.Code).Add("location", m.Location).Add("group", bar.Group)
                    .Add("epoxy", m.Epoxy).ToString();
                _w.Add(r);
            }
        }

        // ------------------------------------------------------------ substructure

        private static readonly Regex ColNo = new(@"column (\d+)", RegexOptions.Compiled);
        private static readonly Regex WingNo = new(@"(?:wingwall|counterfort|wing footing) (\S+?)(?: \d+)?(?: pile \d+)?$", RegexOptions.Compiled);
        private static readonly Regex PileGroupNo = new(@"pile group (\d+)", RegexOptions.Compiled);

        private void Substructure(SubstructureModel sub, string bk, string subId, string superId)
        {
            foreach (var unit in sub.Units)
            {
                string uk = IfcKeys.Unit(bk, unit.Name);
                string uid = Id(uk);
                bool abut = unit.Type == SBM.Core.Bridge.SupportType.Abutment;
                var ur = New(uid, subId, "part", abut ? "abutment" : "pier", "IFCBRIDGEPART", unit.Name);
                ur.Support = unit.Name; ur.Mark = unit.Name;
                SetStation(ur, unit.Station, unit.Station);
                ur.Props = new Json().Add("columns", unit.Columns.Count).Add("pedestals", unit.Pedestals.Count).Add("foundations", unit.Foundations.Count)
                    .Add("cap_top", unit.CapTopDescription).ToString();
                _w.Add(ur);
                string concMat = $"Concrete, f'c = {unit.Definition.Reinforcement.ConcreteStrengthKsi:0.#} ksi";

                // Groups at the plans' granularity: cap (concrete + pedestals + cap bars), each column (concrete + its bars, ties), each footing
                // (footing + piles/shaft + footing bars), backwall. Created on first use.
                var groups = new Dictionary<string, string>();
                string G(string id, string label, string cat)
                {
                    if (groups.TryGetValue(id, out var g)) return g;
                    return groups[id] = Group($"{uk}/grp/{id}", uid, $"{unit.Name} – {label}", cat, unit.Name);
                }
                string CapG() => G("cap", "Cap", "cap group");
                string ColG(int n) => G("col" + n, $"Column {n}", "column group");
                string FtgG(int n) => G("ftg" + n, $"Footing {n}", "footing group");
                string PileGroupG(int n) => G("pg" + n, $"Pile group {n}", "footing group");
                Vec2 ColPos(int idx) => unit.Plan(unit.Columns[idx].S, unit.Columns[idx].N);
                Vec2 FtgPos(int idx) => unit.Plan(unit.Foundations[idx].S, unit.Foundations[idx].N);

                string WingG(string solidName)
                {
                    var wm = WingNo.Match(solidName);
                    string w = wm.Success ? wm.Groups[1].Value : "";
                    return G("wing" + w, $"Wingwall {w}", "wingwall group");
                }
                string ParentOfSolid(SubSolid so)
                {
                    switch (so.Kind)
                    {
                        case SubElementKind.Cap: case SubElementKind.Pedestal: return CapG();
                        case SubElementKind.Backwall: return G("backwall", "Backwall", "backwall group");
                        case SubElementKind.Maskwall: return G("mask" + so.Name, so.Name.Substring(unit.Name.Length).Trim(), "maskwall group");
                        case SubElementKind.Wingwall: case SubElementKind.Counterfort: case SubElementKind.WingFooting: return WingG(so.Name);
                        case SubElementKind.Footing when !ColNo.IsMatch(so.Name) && !PileGroupNo.IsMatch(so.Name): return FtgG(1);
                        case SubElementKind.Column:
                            return ColNo.Match(so.Name) is { Success: true } c1 ? ColG(int.Parse(c1.Groups[1].Value)) : uid;
                        default:
                            if (so.Kind == SubElementKind.Pile && WingNo.IsMatch(so.Name)) return WingG(so.Name);
                            if (PileGroupNo.Match(so.Name) is { Success: true } pg) return PileGroupG(int.Parse(pg.Groups[1].Value));
                            if (ColNo.Match(so.Name) is { Success: true } c2) return FtgG(int.Parse(c2.Groups[1].Value));
                            return uid;
                    }
                }

                int k = 0;
                foreach (var so in unit.Solids)
                {
                    if (so.Mesh.IsEmpty) continue;
                    var (cat, cls) = so.Kind switch
                    {
                        SubElementKind.Cap => (abut ? "abutment cap" : "pier cap", "IFCBEAM"),
                        SubElementKind.Backwall => ("backwall", "IFCWALL"),
                        SubElementKind.Pedestal => ("pedestal", "IFCCOLUMN"),
                        SubElementKind.Wingwall => ("wingwall", "IFCWALL"),
                        SubElementKind.Counterfort => ("counterfort", "IFCWALL"),
                        SubElementKind.Maskwall => ("maskwall", "IFCWALL"),
                        SubElementKind.WingFooting => ("wing footing", "IFCFOOTING"),
                        SubElementKind.Column => ("column", "IFCCOLUMN"),
                        SubElementKind.DrilledShaft => ("drilled shaft", "IFCPILE"),
                        SubElementKind.Pile => ("pile", "IFCPILE"),
                        SubElementKind.MowStrip => ("mow strip", "IFCSLAB"),
                        SubElementKind.TrafficBarrier => ("traffic barrier", "IFCRAILING"),
                        SubElementKind.PavementStrip => ("replacement pavement", "IFCPAVEMENT"),
                        _ => ("footing", "IFCFOOTING"),
                    };
                    var r = New(Id(IfcKeys.SubSolid(uk, k++)), ParentOfSolid(so), "element", cat, cls, so.Name);
                    r.Support = unit.Name; r.Tag = so;
                    if (_keysOnly) { _w.Add(r); continue; }
                    r.Material = so.IsConcrete ? concMat : "ASTM A572 Gr. 50 (HP piling)";
                    r.Volume = Math.Abs(so.Mesh.Volume()); r.Mass = r.Volume * (so.IsConcrete ? 2400 : 7850);
                    r.Box = so.Mesh.Bounds(); StationOf(r, r.Box);
                    var sj = new Json().Add("description", so.Description);
                    if (so.Assumption is { } sas) sj.Add("status", sas.Status).Add("reason", sas.Reason).Add("assumed", sas.Basis);
                    r.Props = sj.ToString();
                    _w.Add(r);
                }

                // Bars by what they reinforce, from the mark's location text; marks that span several columns / footings are split by
                // the nearest column / footing (plan distance from the bar's centroid).
                string ParentOfBar(BarPlacement bar, BarMark m)
                {
                    string loc = m.Location;
                    double cx = 0, cy = 0;
                    foreach (var p in bar.Points) { cx += p.X; cy += p.Y; }
                    var c = new Vec2(cx / bar.Points.Length, cy / bar.Points.Length);
                    int Nearest(IEnumerable<int> cand, Func<int, Vec2> pos)
                    {
                        int best = -1; double bd = double.MaxValue;
                        foreach (int i in cand) { double d = Vec2.Distance(c, pos(i)); if (d < bd) { bd = d; best = i; } }
                        return best;
                    }
                    if (loc.Contains(" cap ")) return CapG();
                    if (loc.Contains("backwall")) return G("backwall", "Backwall", "backwall group");
                    if (loc.Contains("footing") && unit.Foundations.Count > 0)
                    {
                        int i = Nearest(Enumerable.Range(0, unit.Foundations.Count), FtgPos);
                        return FtgG(i + 1);
                    }
                    var cols = ColNo.Matches(loc).Select(x => int.Parse(x.Groups[1].Value)).Where(n => n >= 1 && n <= unit.Columns.Count).Distinct().ToList();
                    if (cols.Count == 1) return ColG(cols[0]);
                    if (cols.Count > 1) return ColG(Nearest(cols.Select(n => n - 1), ColPos) + 1);
                    return uid;
                }
                Bars(sub.Bars.Where(b => b.Unit == unit.Name), sub.Marks.Where(m => m.Unit == unit.Name), ParentOfBar, i => IfcKeys.SubBar(uk, i), unit.Definition.Reinforcement.SteelYieldKsi, unit.Name);
            }
            int n = 0;
            foreach (var so in sub.Drainage)
            {
                var (cat, cls) = so.Kind switch
                {
                    SubElementKind.DeckDrain => ("deck drain", "IFCWASTETERMINAL"),
                    SubElementKind.DrainPipe => ("drain pipe", "IFCPIPESEGMENT"),
                    _ => ("splash basin", "IFCCOURSE"),
                };
                var r = New(Id(IfcKeys.Drain(bk, n++)), superId, "element", cat, cls, so.Name);
                r.Tag = so;
                if (_keysOnly) { _w.Add(r); continue; }
                var tm = Regex.Match(so.Name, @"\((Type [^)]+)\)");
                if (tm.Success) r.Subtype = tm.Groups[1].Value;
                r.Volume = Math.Abs(so.Mesh.Volume()); r.Box = so.Mesh.Bounds(); StationOf(r, r.Box);
                r.Props = new Json().Add("description", so.Description).ToString();
                _w.Add(r);
            }
        }

        // ------------------------------------------------------------ misc items (drainage, sign supports, crash cushion, lighting)

        private void MiscItems(SBM.Core.Misc.MiscModel misc, string bk, string superId, string bridgeName)
        {
            string who = _unit.Length > 0 ? $"{_bridgeGroup} {_unit}" : bridgeName;
            var groups = new Dictionary<SBM.Core.Misc.MiscGroup, string>();
            string GroupOf(SBM.Core.Misc.MiscGroup g)
            {
                if (groups.TryGetValue(g, out var id)) return id;
                var (title, cat) = g switch
                {
                    SBM.Core.Misc.MiscGroup.Drainage => ("deck drainage", "drainage"),
                    SBM.Core.Misc.MiscGroup.SignSupport => ("sign supports", "sign supports"),
                    SBM.Core.Misc.MiscGroup.CrashCushion => ("crash cushion end posts", "crash cushion"),
                    _ => ("lighting", "lighting"),
                };
                return groups[g] = Group($"{bk}/misc-grp/{g}", superId, $"{who} {title}", cat);
            }
            var asm = new Dictionary<string, string>();
            foreach (var a in misc.Assemblies)
            {
                if (asm.ContainsKey(a.Name)) continue;
                string id = Id(IfcKeys.MiscAssembly(bk, a.Name));
                var r = New(id, GroupOf(a.Group), "assembly", a.Category, "IFCELEMENTASSEMBLY", a.Name);
                r.Mark = a.Name;
                if (!_keysOnly) r.Props = new Json().Add("description", a.Description).ToString();
                _w.Add(r);
                asm[a.Name] = id;
            }
            int n = 0;
            foreach (var el in misc.Elements)
            {
                var (cat, cls) = el.Kind switch
                {
                    SBM.Core.Misc.MiscKind.DrainTrench => ("deck drain", "IFCWASTETERMINAL"),
                    SBM.Core.Misc.MiscKind.DrainGrate => ("drain grate", "IFCPLATE"),
                    SBM.Core.Misc.MiscKind.DrainTube or SBM.Core.Misc.MiscKind.DrainPipe => ("drain pipe", "IFCPIPESEGMENT"),
                    SBM.Core.Misc.MiscKind.PipeFitting => ("pipe fitting", "IFCPIPEFITTING"),
                    SBM.Core.Misc.MiscKind.PipeHanger => ("pipe hanger", "IFCDISCRETEACCESSORY"),
                    SBM.Core.Misc.MiscKind.PipeSupport => ("pipe support", "IFCDISCRETEACCESSORY"),
                    SBM.Core.Misc.MiscKind.DrainBox => ("collection box", "IFCTANK"),
                    SBM.Core.Misc.MiscKind.DrainTrough => ("drain trough", "IFCBUILDINGELEMENTPROXY"),
                    SBM.Core.Misc.MiscKind.SplashBasin => ("splash basin", "IFCCOURSE"),
                    SBM.Core.Misc.MiscKind.ErosionMattress => ("erosion mattress", "IFCCOURSE"),
                    SBM.Core.Misc.MiscKind.SignPlate => ("sign bracket plate", "IFCPLATE"),
                    SBM.Core.Misc.MiscKind.SignStiffener => ("sign bracket stiffener", "IFCPLATE"),
                    SBM.Core.Misc.MiscKind.SignAnchorBolt or SBM.Core.Misc.MiscKind.PoleAnchorBolt => ("anchor bolt group", "IFCMECHANICALFASTENER"),
                    SBM.Core.Misc.MiscKind.SignPlaceholder => ("sign base plate (by others)", "IFCBUILDINGELEMENTPROXY"),
                    SBM.Core.Misc.MiscKind.SignBlock => ("exit sign base", "IFCFOOTING"),
                    SBM.Core.Misc.MiscKind.EndPostConcrete => ("crash cushion end post", "IFCBUILDINGELEMENTPROXY"),
                    SBM.Core.Misc.MiscKind.CushionPlaceholder => ("crash cushion (by others)", "IFCBUILDINGELEMENTPROXY"),
                    SBM.Core.Misc.MiscKind.PoleBaseConcrete => ("pole base", "IFCFOOTING"),
                    SBM.Core.Misc.MiscKind.JunctionBox => ("junction box", "IFCJUNCTIONBOX"),
                    _ => ("conduit", "IFCCABLECARRIERSEGMENT"),
                };
                string parent = asm.TryGetValue(el.Assembly, out var pid) ? pid : GroupOf(el.Group);
                var r = New(Id(IfcKeys.MiscElement(bk, n++)), parent, "element", cat, cls, el.Name);
                r.Tag = el;
                if (_keysOnly) { _w.Add(r); continue; }
                r.Material = el.Material;
                r.Volume = Math.Abs(el.Mesh.Volume());
                bool concrete = el.Material.StartsWith("Class C", StringComparison.OrdinalIgnoreCase);
                bool steel = el.Material.Contains("steel", StringComparison.OrdinalIgnoreCase) || el.Material.Contains("A53") || el.Material.Contains("A36") || el.Material.Contains("A500") || el.Material.Contains("F1554");
                if (concrete) r.Mass = r.Volume * 2400; else if (steel && !el.Placeholder) r.Mass = r.Volume * 7850;
                r.Box = el.Mesh.Bounds(); StationOf(r, r.Box);
                var pj = new Json().Add("description", el.Description);
                if (el.Placeholder) pj.Add("placeholder", true).Add("basis", el.PlaceholderBasis);
                if (el.Assumption is { } asu) pj.Add("status", asu.Status).Add("reason", asu.Reason).Add("assumed", asu.Basis);
                r.Props = pj.ToString();
                _w.Add(r);
            }
        }

        // ------------------------------------------------------------ prestressed

        // ------------------------------------------------------------ precast segmental box

        /// <summary>Segmental box: group "Segmental box" (summary in props) > cantilever assemblies (segments, cantilever + transverse tendons) and
        /// the closure joints / end segments; continuity tendons per span group; bearings group. GlobalIds = the IFC keys (IfcKeys.Seg*).</summary>
        private void Segmental(SBM.Core.Segmental.SegmentalModel sm, string bk, string superId, string bridgeName)
        {
            const double cy = 0.764554857984;
            var types = sm.Segments.GroupBy(s => s.Type).ToDictionary(g => g.Key.ToString(), g => g.Count());
            var gp = new Json().Add("segments", sm.Segments.Count).Add("typical", types.GetValueOrDefault("Typical")).Add("bent_halves", types.GetValueOrDefault("BentHalf"))
                .Add("closure_joints", types.GetValueOrDefault("Closure")).Add("end_segments", types.GetValueOrDefault("End")).Add("tendons", sm.Tendons.Count)
                .Add("bearings", sm.Bearings.Count).Add("box_concrete_cy", Math.Round(sm.ConcreteVolume / cy, 1)).Add("source", sm.Definition.Source);
            string gid = Group($"{bk}/grp/segmental", superId, $"{bridgeName} segmental box", "segmental box", props: gp.ToString());
            var asm = new Dictionary<string, string>();
            foreach (var bent in sm.Segments.Where(x => x.Bent != "").Select(x => x.Bent).Distinct())
            {
                string aid = Id(IfcKeys.SegCantilever(bk, bent));
                var a = New(aid, gid, "assembly", "segmental cantilever", "IFCELEMENTASSEMBLY", bent + " cantilever");
                a.Support = bent; a.Mark = bent;
                _w.Add(a);
                asm[bent] = aid;
            }
            static string Cat(SBM.Core.Bridge.SegmentType t) => t switch
            {
                SBM.Core.Bridge.SegmentType.BentHalf => "bent segment", SBM.Core.Bridge.SegmentType.Closure => "closure joint",
                SBM.Core.Bridge.SegmentType.End => "end segment", _ => "typical segment",
            };
            foreach (var s in sm.Segments)
            {
                var r = New(Id(IfcKeys.Segment(bk, s.Name)), s.Bent != "" ? asm[s.Bent] : gid, "part", Cat(s.Type), "IFCBEAM", s.Name);
                r.Tag = s; r.Mark = s.Name; r.Support = s.Bent; r.Subtype = s.Span;
                if (_keysOnly) { _w.Add(r); continue; }
                r.Material = s.Type == SBM.Core.Bridge.SegmentType.Closure ? "Cast-in-place closure joint concrete" : $"Precast segmental box concrete, f'c = {sm.Definition.ConcreteStrengthKsi:0.0#} ksi";
                r.Volume = s.Volume; r.Mass = s.Volume * 2400; r.Length = s.ChordLength;
                r.Box = s.Mesh.Bounds(); SetStation(r, s.Station0, s.Station1);
                var pj = new Json().Add("span", s.Span).Add("depth_start_ft", Math.Round(s.Depth0 / 0.3048, 4)).Add("depth_end_ft", Math.Round(s.Depth1 / 0.3048, 4))
                    .Add("length_along_L_ft", Math.Round(s.AlignmentLength / 0.3048, 4)).Add("chord_length_ft", Math.Round(s.ChordLength / 0.3048, 4)).Add("description", s.Description);
                if (s.Assumption is { } asu) pj.Add("status", asu.Status).Add("reason", asu.Reason).Add("assumed", asu.Basis);
                r.Props = pj.ToString();
                _w.Add(r);
            }
            var segBent = sm.Segments.ToDictionary(x => x.Name, x => x.Bent);
            var spanGroups = new Dictionary<string, string>();
            foreach (var t in sm.Tendons)
            {
                string owner = t.Group == SBM.Core.Bridge.TendonGroup.Transverse ? segBent.GetValueOrDefault(t.Owner, "") : t.Owner;
                string parent;
                if (owner != "" && asm.TryGetValue(owner, out var aid)) parent = aid;
                else
                {
                    string key = t.Group == SBM.Core.Bridge.TendonGroup.Transverse ? "transverse tendons" : $"{t.Location} continuity tendons";
                    if (!spanGroups.TryGetValue(key, out parent!)) spanGroups[key] = parent = Group($"{bk}/grp/segmental/{key}", gid, key, "tendon group");
                }
                string cat = t.Group switch
                {
                    SBM.Core.Bridge.TendonGroup.Cantilever => "cantilever tendon", SBM.Core.Bridge.TendonGroup.Bottom => "bottom continuity tendon",
                    SBM.Core.Bridge.TendonGroup.Closure => "closure tendon", _ => "transverse tendon",
                };
                var r = New(Id(IfcKeys.SegTendon(bk, t.Name)), parent, "element", cat, "IFCTENDON", t.Name);
                r.Tag = t; r.Mark = t.Name; r.Support = owner;
                if (_keysOnly) { _w.Add(r); continue; }
                r.Material = sm.Definition.StrandMaterial; r.Length = t.Length;
                foreach (var p in t.Points) r.Box.Add(p);
                StationOf(r, r.Box);
                var pj = new Json().Add("location", t.Location).Add("strands", t.Strands).Add("plan_length_ft", Math.Round(t.PlanLength / 0.3048, 3))
                    .Add("model_length_ft", Math.Round(t.Length / 0.3048, 3)).Add("plan_weight_lb", t.PlanWeightLb).Add("jacking_force_kip", t.JackingForceKip)
                    .Add("stressing_end", t.StressingEnd).Add("anchors", t.Anchors);
                if (t.Assumption is { } asu) pj.Add("status", asu.Status).Add("reason", asu.Reason).Add("assumed", asu.Basis);
                r.Props = pj.ToString();
                _w.Add(r);
            }
            string bg = sm.Bearings.Count > 0 ? Group($"{bk}/grp/segmental/bearings", gid, "Disc bearings", "bearing group") : "";
            foreach (var b in sm.Bearings)
            {
                var r = New(Id(IfcKeys.SegBearing(bk, b.Name)), bg, "element", "disc bearing", "IFCBEARING", b.Name);
                r.Tag = b; r.Support = b.Bent; r.Subtype = "Type " + b.Type; r.InIfc = true;
                if (_keysOnly) { _w.Add(r); continue; }
                if (b.Mesh is { IsEmpty: false } m) { r.Box = m.Bounds(); StationOf(r, r.Box); }
                var pj = new Json().Add("description", b.Description).Add("soffit_el_ft", Math.Round(b.SoffitZ / 0.3048, 3)).Add("bearing_top_el_ft", Math.Round(b.Top.Z / 0.3048, 3));
                if (b.Assumption is { } asu) pj.Add("status", asu.Status).Add("reason", asu.Reason).Add("assumed", asu.Basis);
                r.Props = pj.ToString();
                _w.Add(r);
            }
        }

        private void Prestressed(SBM.Core.Prestressed.PrestressedModel ps, string bk, string superId)
        {
            foreach (var b in ps.Beams)
            {
                string aid = Id(IfcKeys.PsAssembly(bk, b.Mark));
                var a = New(aid, superId, "assembly", "prestressed girder", "IFCELEMENTASSEMBLY", b.Mark);
                a.Mark = b.Mark; a.Girder = $"G{b.Girder + 1}";
                _w.Add(a);
                var r = New(Id(IfcKeys.PsBeam(bk, b.Mark)), aid, "part", "prestressed beam", "IFCBEAM", b.Mark);
                r.Tag = b;
                r.Mark = b.Mark; r.Girder = a.Girder; r.Material = b.Material; r.Volume = b.Volume; r.Mass = b.Mass; r.Length = b.Length;
                r.Box = b.Mesh.Bounds(); StationOf(r, r.Box);
                var props = new Json().Add("shape", b.Shape.Name).Add("strands", b.Strands.Count);
                if (b.Designation != "") props.Add("designation", b.Designation);
                if (b.IsStandardLength) props.Add("layout_bearing_span", b.LayoutBearingSpan).Add("standard_minus_layout", b.LengthDifference);
                if (b.Assumption != null) props.Add("status", b.Assumption.Status).Add("reason", b.Assumption.Reason).Add("assumed", b.Assumption.Basis);
                r.Props = props.ToString();
                _w.Add(r);
                int n = 0;
                foreach (var st in b.Strands)
                {
                    int sn = n++;
                    var s = New(Id(IfcKeys.PsStrand(bk, b.Mark, sn)), aid, "bar", st.Depressed ? "depressed strand" : "strand", "IFCTENDON", st.Depressed ? "Depressed strand" : "Strand");
                    s.Tag = st;
                    s.Girder = a.Girder; s.Length = SBM.Core.Prestressed.PrestressedModel.PolyLength(st.Points);
                    foreach (var p in st.Points) s.Box.Add(p);
                    if (st.Debonded) s.Props = new Json().Add("debond_start", st.DebondStart).Add("debond_end", st.DebondEnd).Add("debond_symbol", st.DebondSymbol).ToString();
                    _w.Add(s);
                    int e = 0;
                    foreach (var piece in st.Sheathing())
                    {
                        int end = st.DebondStart > 0 && e == 0 ? 0 : 1;
                        e++;
                        var h = New(Id(IfcKeys.PsSheath(bk, b.Mark, sn, end)), aid, "element", "debond sheathing", "IFCTENDONCONDUIT", "Debond sheathing");
                        h.Girder = a.Girder; h.Length = SBM.Core.Prestressed.PrestressedModel.PolyLength(piece);
                        foreach (var p in piece) h.Box.Add(p);
                        _w.Add(h);
                    }
                }
                if (b.Bars.Count > 0)
                {
                    string unit = b.Bars[0].Unit;
                    Bars(b.Bars, ps.BarMarks.Where(m => m.Unit == unit), (bar, m) => aid, i => IfcKeys.PsBar(bk, b.Mark, i), b.Definition?.Reinforcement?.SteelYieldKsi ?? 60, "");
                }
            }
            foreach (var p in ps.Pads)
            {
                var r = New(Id(IfcKeys.PsPad(bk, p.Mark)), superId, "element", "bearing pad", "IFCBEARING", "Pad " + p.Mark);
                r.Tag = p;
                r.Mark = p.Mark; r.Support = p.Support; r.Girder = $"G{p.Girder + 1}"; r.Box = p.Mesh.Bounds(); StationOf(r, r.Box);
                r.Props = new Json().Add("description", p.Description).ToString();
                _w.Add(r);
                if (p.SolePlate is { } sp)
                {
                    var q = New(Id(IfcKeys.PsSolePlate(bk, p.Mark)), superId, "element", "sole plate", "IFCPLATE", sp.Mark);
                    q.Tag = sp;
                    q.Mark = sp.Mark; q.Support = p.Support; q.Girder = r.Girder; q.Box = sp.Mesh.Bounds(); StationOf(q, q.Box);
                    q.Material = "Steel"; q.Volume = sp.Volume; q.Mass = sp.Mass; q.Length = sp.Length;
                    q.Props = new Json().Add("description", sp.Description).Add("thickness_at_bearing", sp.ThicknessAtBearing).ToString();
                    _w.Add(q);
                }
            }
        }
    }
}
