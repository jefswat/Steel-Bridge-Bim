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
/// the element and link tables but keeps <c>sheets</c> and <c>qc</c> (GlobalIds are stable across rebuilds).
/// </summary>
public static class ModelDatabase
{
    public const int SchemaVersion = 1;

    private static readonly Regex UnitName = new(@"^(?<g>.*?)\s+Unit\s+(?<n>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CfName = new(@"^(?<type>.*?)\s+G(?<a>\d+)[–-]G(?<b>\d+)(?:, span (?<span>\d+))?", RegexOptions.Compiled);
    private static readonly Regex XgName = new(@"^Cross girder G(?<a>\d+)[–-]G(?<b>\d+) carrying discontinuous G(?<g>\d+)", RegexOptions.Compiled);

    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT);
        CREATE TABLE IF NOT EXISTS elements(
          global_id TEXT PRIMARY KEY, parent_global_id TEXT, seq INTEGER,
          level TEXT NOT NULL, category TEXT, subtype TEXT, ifc_class TEXT, in_ifc INTEGER NOT NULL DEFAULT 1,
          name TEXT, mark TEXT,
          bridge_group TEXT, bridge TEXT, unit TEXT, support TEXT, girder TEXT,
          station_begin REAL, station_end REAL, station_range TEXT,
          material TEXT, mass_kg REAL, volume_m3 REAL, length_m REAL,
          bbox_min_x REAL, bbox_min_y REAL, bbox_min_z REAL, bbox_max_x REAL, bbox_max_y REAL, bbox_max_z REAL,
          properties TEXT);
        CREATE TABLE IF NOT EXISTS sheets(
          sheet_id TEXT PRIMARY KEY, plan_set TEXT NOT NULL, pdf_file TEXT, pdf_page INTEGER, design_sheet TEXT, title TEXT,
          page_file TEXT, preview_file TEXT, kind TEXT, note TEXT);
        CREATE TABLE IF NOT EXISTS element_sheet(
          global_id TEXT NOT NULL, sheet_id TEXT NOT NULL, role TEXT NOT NULL DEFAULT 'detail', basis TEXT, source TEXT,
          PRIMARY KEY(global_id, sheet_id));
        CREATE TABLE IF NOT EXISTS qc(
          global_id TEXT NOT NULL, sheet_id TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'unchecked' CHECK(status IN ('unchecked','ok','issue')),
          checked_by TEXT, checked_at TEXT, note TEXT,
          PRIMARY KEY(global_id, sheet_id));
        """;

    public const string IndexSql = """
        CREATE INDEX IF NOT EXISTS ix_el_parent ON elements(parent_global_id);
        CREATE INDEX IF NOT EXISTS ix_el_cat ON elements(category);
        CREATE INDEX IF NOT EXISTS ix_el_bridge ON elements(bridge, category);
        CREATE INDEX IF NOT EXISTS ix_el_support ON elements(support);
        CREATE INDEX IF NOT EXISTS ix_el_mark ON elements(mark);
        CREATE INDEX IF NOT EXISTS ix_el_level ON elements(level);
        CREATE INDEX IF NOT EXISTS ix_es_sheet ON element_sheet(sheet_id);
        """;

    /// <summary>Views: hierarchy path, links resolved down the hierarchy (a link on an assembly applies to its children),
    /// and the QC join.</summary>
    public const string ViewSql = """
        DROP VIEW IF EXISTS v_element_sheets;
        CREATE VIEW v_element_sheets AS
          WITH RECURSIVE d(global_id, sheet_id, role, basis, link_root) AS (
            SELECT global_id, sheet_id, role, basis, global_id FROM element_sheet
            UNION ALL
            SELECT e.global_id, d.sheet_id, d.role, d.basis, d.link_root FROM d JOIN elements e ON e.parent_global_id = d.global_id)
          SELECT global_id, sheet_id, role, basis, link_root FROM d;
        DROP VIEW IF EXISTS v_element_path;
        CREATE VIEW v_element_path AS
          WITH RECURSIVE p(global_id, depth, anc, anc_name, anc_level) AS (
            SELECT global_id, 0, global_id, name, level FROM elements
            UNION ALL
            SELECT p.global_id, p.depth + 1, e.parent_global_id, pe.name, pe.level
              FROM p JOIN elements e ON e.global_id = p.anc JOIN elements pe ON pe.global_id = e.parent_global_id)
          SELECT global_id, depth, anc AS ancestor_global_id, anc_name AS ancestor_name, anc_level AS ancestor_level FROM p;
        DROP VIEW IF EXISTS v_unused_sheets;
        CREATE VIEW v_unused_sheets AS
          SELECT s.* FROM sheets s WHERE NOT EXISTS (SELECT 1 FROM element_sheet l WHERE l.sheet_id = s.sheet_id) AND COALESCE(s.kind,'') <> 'administrative';
        DROP VIEW IF EXISTS v_element_qc;
        CREATE VIEW v_element_qc AS
          SELECT e.global_id, e.level, e.category, e.name, e.mark, e.bridge, e.unit, e.support, e.girder,
                 s.sheet_id, s.plan_set, s.design_sheet, s.pdf_page, s.title, v.role, v.basis,
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
        Exec(cn, SchemaSql);
        if (Scalar<int>(cn, "SELECT COUNT(*) FROM pragma_table_info('sheets') WHERE name='note'") == 0) Exec(cn, "ALTER TABLE sheets ADD COLUMN note TEXT");
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

    // ================================================================== build

    /// <summary>Writes the element registry of the model to <paramref name="path"/> (replacing the elements and links, keeping sheets and QC).</summary>
    public static DatabaseBuildResult Write(string path, BridgeProject project, IReadOnlyList<IfcBridgeData> bridges, string? modelFile = null)
    {
        var sw = Stopwatch.StartNew();
        using var cn = Open(path);
        Exec(cn, "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA temp_store=MEMORY;");
        Exec(cn, "DROP VIEW IF EXISTS v_element_sheets; DROP VIEW IF EXISTS v_element_path; DROP VIEW IF EXISTS v_unused_sheets; DROP VIEW IF EXISTS v_element_qc;");
        Exec(cn, "DROP INDEX IF EXISTS ix_el_parent; DROP INDEX IF EXISTS ix_el_cat; DROP INDEX IF EXISTS ix_el_bridge; DROP INDEX IF EXISTS ix_el_support; DROP INDEX IF EXISTS ix_el_mark; DROP INDEX IF EXISTS ix_el_level;");
        Exec(cn, "DELETE FROM elements;");
        var result = new DatabaseBuildResult { Path = path };
        using (var tx = cn.BeginTransaction())
        {
            var w = new Writer(cn, tx, result);
            new Builder(w, project, false).Run(bridges);
            w.Dispose();
            tx.Commit();
        }
        Exec(cn, IndexSql);
        // Links survive a rebuild (GlobalIds are stable) until the rules are applied again; drop those of elements that no longer exist.
        Exec(cn, "DELETE FROM element_sheet WHERE global_id NOT IN (SELECT global_id FROM elements)");
        Exec(cn, ViewSql);
        Exec(cn, "INSERT OR REPLACE INTO meta VALUES('schema_version', @v)", ("@v", SchemaVersion.ToString()));
        Exec(cn, "INSERT OR REPLACE INTO meta VALUES('project', @v)", ("@v", project.Name));
        Exec(cn, "INSERT OR REPLACE INTO meta VALUES('model_file', @v)", ("@v", modelFile ?? ""));
        Exec(cn, "INSERT OR REPLACE INTO meta VALUES('built_at', @v)", ("@v", DateTime.UtcNow.ToString("o")));
        Exec(cn, "INSERT OR REPLACE INTO meta VALUES('coordinates', @v)", ("@v", "bbox in meters, project grid (state plane) coordinates; length_m/mass_kg/volume_m3 SI"));
        result.Elapsed = sw.Elapsed;
        return result;
    }

    /// <summary>Computes only the model object → GlobalId map (no database): for the app to resolve picked objects.</summary>
    public static TagIndex BuildTagIndex(BridgeProject project, IReadOnlyList<IfcBridgeData> bridges)
    {
        var res = new DatabaseBuildResult();
        new Builder(new Writer(null, null, res), project, keysOnly: true).Run(bridges);
        return res.Tags;
    }

    // ================================================================== writer

    private sealed class Row
    {
        public string GlobalId = "", Parent = "", Level = "", Category = "", Subtype = "", IfcClass = "", Name = "", Mark = "";
        public string BridgeGroup = "", Bridge = "", Unit = "", Support = "", Girder = "", Material = "", Props = "", StationText = "";
        public bool InIfc = true;
        public double StaBegin = double.NaN, StaEnd = double.NaN, Mass = double.NaN, Volume = double.NaN, Length = double.NaN;
        public Box3 Box = Box3.Empty;
        public object? Tag;
    }

    private sealed class Writer : IDisposable
    {
        private readonly SqliteCommand? _cmd;
        private readonly SqliteParameter[] _p = new SqliteParameter[28];
        private readonly DatabaseBuildResult _res;
        private readonly SqliteParameter _props = new("$p", null);
        private int _seq;
        public Writer(SqliteConnection? cn, SqliteTransaction? tx, DatabaseBuildResult res)
        {
            _res = res;
            _cmd = null;
            if (cn == null) return;
            _cmd = cn.CreateCommand();
            _cmd.Transaction = tx;
            string[] cols = { "global_id", "parent_global_id", "seq", "level", "category", "subtype", "ifc_class", "in_ifc", "name", "mark", "bridge_group", "bridge", "unit", "support", "girder",
                "station_begin", "station_end", "station_range", "material", "mass_kg", "volume_m3", "length_m",
                "bbox_min_x", "bbox_min_y", "bbox_min_z", "bbox_max_x", "bbox_max_y", "bbox_max_z" };
            _cmd.CommandText = $"INSERT OR REPLACE INTO elements({string.Join(",", cols)},properties) VALUES({string.Join(",", cols.Select((_, i) => "$" + i))},$p)";
            for (int i = 0; i < cols.Length; i++) _cmd.Parameters.Add(_p[i] = new SqliteParameter("$" + i, null));
            _cmd.Parameters.Add(_props);
            _cmd.Prepare();
        }

        private static object? N(string s) => s.Length == 0 ? null : s;
        private static object? D(double d) => double.IsNaN(d) || double.IsInfinity(d) ? null : Math.Round(d, 6);

        public void Add(Row r)
        {
            if (r.Tag != null) _res.Tags.Add(r.Tag, r.GlobalId);
            if (_cmd == null) { _res.Elements++; return; }
            _p[0].Value = r.GlobalId; _p[1].Value = N(r.Parent); _p[2].Value = _seq++; _p[3].Value = r.Level; _p[4].Value = N(r.Category);
            _p[5].Value = N(r.Subtype); _p[6].Value = N(r.IfcClass); _p[7].Value = r.InIfc ? 1 : 0; _p[8].Value = N(r.Name); _p[9].Value = N(r.Mark);
            _p[10].Value = N(r.BridgeGroup); _p[11].Value = N(r.Bridge); _p[12].Value = N(r.Unit); _p[13].Value = N(r.Support); _p[14].Value = N(r.Girder);
            _p[15].Value = D(r.StaBegin); _p[16].Value = D(r.StaEnd); _p[17].Value = N(r.StationText); _p[18].Value = N(r.Material);
            _p[19].Value = D(r.Mass); _p[20].Value = D(r.Volume); _p[21].Value = D(r.Length);
            bool has = !r.Box.IsEmpty;
            _p[22].Value = has ? Math.Round(r.Box.Min.X, 4) : null; _p[23].Value = has ? Math.Round(r.Box.Min.Y, 4) : null; _p[24].Value = has ? Math.Round(r.Box.Min.Z, 4) : null;
            _p[25].Value = has ? Math.Round(r.Box.Max.X, 4) : null; _p[26].Value = has ? Math.Round(r.Box.Max.Y, 4) : null; _p[27].Value = has ? Math.Round(r.Box.Max.Z, 4) : null;
            _props.Value = N(r.Props);
            foreach (var q in _p) q.Value ??= DBNull.Value;
            _props.Value ??= DBNull.Value;
            _cmd.ExecuteNonQuery();
            _res.Elements++;
            _res.ByLevel[r.Level] = _res.ByLevel.GetValueOrDefault(r.Level) + 1;
            string c = r.Level + ": " + r.Category;
            _res.ByCategory[c] = _res.ByCategory.GetValueOrDefault(c) + 1;
        }

        public void Dispose() => _cmd?.Dispose();
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
            r.StationText = Math.Abs(hi - lo) < 0.01 ? _project.Units.FormatStation(lo) : _project.Units.FormatStation(lo) + " to " + _project.Units.FormatStation(hi);
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
            if (b.Steel != null) Steel(b.Steel, bk, superId);
            _ = deckSlabId;
        }

        private static Row Part(Row r, Row from) { r.StaBegin = from.StaBegin; r.StaEnd = from.StaEnd; r.StationText = from.StationText; return r; }

        // ------------------------------------------------------------ steel

        private sealed record AsmInfo(string GlobalId, string Support, string Girder, string Subtype);

        private void Steel(SteelModel steel, string bk, string superId)
        {
            var asmInfo = new Dictionary<string, AsmInfo>();
            var girderGroup = new Dictionary<int, string>();
            string cfGroup = "", lbGroup = "";
            string GirderGroup(int g)
            {
                if (girderGroup.TryGetValue(g, out var id)) return id;
                id = Id($"{bk}/girder/G{g + 1}");
                var r = New(id, superId, "group", "girder", "", $"G{g + 1}");
                r.InIfc = false; r.Girder = $"G{g + 1}";
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
                    case AssemblyKind.Bearing:
                        parent = GirderGroup(asm.Girder); cat = "bearing";
                        {
                            var parts = asm.Description.Split(", ");
                            if (parts.Length >= 3) support = parts[^2];
                            sub = parts[0].Contains('(') ? parts[0][(parts[0].IndexOf('(') + 1)..].TrimEnd(')') : "";
                        }
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
                var r = New(gid, parent, "assembly", cat, "IFCELEMENTASSEMBLY", asm.Mark);
                r.Mark = asm.Mark; r.Girder = girder; r.Support = support; r.Subtype = sub; r.Props = props.ToString();
                _w.Add(r);
                asmInfo[asm.Mark] = new AsmInfo(gid, support, girder, sub);
            }

            // parts, with quantities and station extent computed per assembly from the parts' bounding boxes afterwards (kept simple: per part)
            foreach (var part in steel.Parts)
            {
                string gid = Id(IfcKeys.Part(bk, part.Mark));
                asmInfo.TryGetValue(part.Assembly, out var ai);
                var r = New(gid, ai?.GlobalId ?? superId, "part", PartCategoryName(part.Category), IfcClassOf(part), part.Mark);
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
                r.Props = new Json().Add("description", part.Description).Add("assembly", part.Assembly)
                    .Add("role", part.Role == MemberRole.None ? null : part.Role.ToString()).Add("lb_per_ft", part.NominalWeightLbPerFt, 3).ToString();
                _w.Add(r);
            }

            // bolts, anchor bolts, studs: numbered exactly like the IFC export (bolts then studs, one running counter)
            int no = 0;
            var perAsm = new Dictionary<string, int>();
            string boltMat = $"ASTM F3125 Gr. {steel.BoltGrade}";
            void Fastener(object tagObj, int kind, Frame3 f, string asmMark, double grip, double len, double dia, double embed)
            {
                string gid = Id(IfcKeys.Fastener(bk, kind, asmMark, no++));
                asmInfo.TryGetValue(asmMark, out var ai);
                int idx = perAsm[asmMark] = perAsm.GetValueOrDefault(asmMark) + 1;
                string cat = kind switch { 1 => "anchor bolt", 2 => "shear stud", _ => "bolt" };
                string tag = kind switch { 1 => "AB", 2 => "SS", _ => "B" };
                var r = New(gid, ai?.GlobalId ?? superId, "fastener", cat, "IFCMECHANICALFASTENER", $"{cat} {asmMark}.{tag}{idx}");
                r.Mark = $"{asmMark}-{tag}{idx}";
                r.Support = ai?.Support ?? ""; r.Girder = ai?.Girder ?? "";
                r.Material = kind switch { 1 => "ASTM F1554", 2 => "ASTM A108 (AWS D1.5 Type B)", _ => boltMat };
                r.Tag = tagObj;
                if (_keysOnly) { _w.Add(r); return; }
                r.Length = len;
                r.Box.Add(f.Origin); r.Box.Add(f.Origin + f.Z * len);
                if (_al != null) { double s = _al.Inverse(new Vec2(f.Origin.X, f.Origin.Y)).Station; SetStation(r, s, s); }
                r.Props = new Json().Add("diameter", kind == 1 ? dia : kind == 2 ? steel.StudDiameter : steel.Bolt.Diameter).Add("grip", grip)
                    .Add("embedment", embed).ToString();
                _w.Add(r);
            }
            foreach (var bolt in steel.Bolts)
                Fastener(bolt, bolt.Kind == BoltKind.Anchor ? 1 : 0, bolt.Frame, bolt.Assembly, bolt.Grip, bolt.Length, bolt.Diameter, bolt.Embedment);
            foreach (var st in steel.Studs)
                Fastener(st, 2, st.Frame, st.Assembly, 0, st.Length, steel.StudDiameter, 0);
        }

        private string Group(string key, string parent, string name, string category)
        {
            string id = Id(key);
            var r = New(id, parent, "group", category, "", name);
            r.InIfc = false;
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
            PartCategory.Ptfe => "PTFE surface", _ => c.ToString(),
        };

        private static string IfcClassOf(SteelPart p) => p.Category switch
        {
            PartCategory.Angle or PartCategory.Channel => "IFCMEMBER",
            PartCategory.BearingPad or PartCategory.BearingDisc => "IFCBEARING",
            PartCategory.ShearPin => "IFCMECHANICALFASTENER",
            PartCategory.Weld => "IFCFASTENER",
            _ => "IFCPLATE",
        };

        // ------------------------------------------------------------ concrete (deck)

        private string Concrete(ConcreteModel c, SBM.Core.Bridge.BridgeDefinition def, string bk, string deckPartId)
        {
            string slabId = "";
            string concMat = $"Concrete, f'c = {def.Deck.Reinforcement.ConcreteStrengthKsi:0.#} ksi";
            if (c.Deck != null)
            {
                slabId = Id(IfcKeys.DeckSlab(bk));
                var r = New(slabId, deckPartId, "element", "deck slab", "IFCSLAB", "Deck slab");
                r.Material = concMat; r.Volume = c.DeckVolume + c.HaunchVolume; r.Mass = r.Volume * 2400;
                r.Box = c.Deck.Bounds(); if (c.Haunches is { IsEmpty: false } h) { var hb = h.Bounds(); r.Box.Add(hb.Min); r.Box.Add(hb.Max); }
                StationOf(r, r.Box);
                r.Props = new Json().Add("thickness_m", def.Deck.Thickness).Add("deck_volume_m3", c.DeckVolume).Add("haunch_volume_m3", c.HaunchVolume).ToString();
                _w.Add(r);
            }
            foreach (var (name, mesh) in c.Barriers)
            {
                var r = New(Id(IfcKeys.Barrier(bk, name)), deckPartId, "element", "barrier", "IFCRAILING", name);
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
                var r = New(Id(IfcKeys.JointBlockout(jk)), slabId.Length > 0 ? slabId : deckPartId, "element", "joint blockout", "IFCBUILDINGELEMENTPART", jr.BlockoutName);
                r.Support = jr.Support; r.Subtype = type; r.Material = concMat; r.Volume = jr.BlockoutVolume; r.Mass = r.Volume * 2400;
                r.Box = jr.Blockout.Bounds(); StationOf(r, r.Box);
                r.Props = new Json().Add("end", jr.AtBegin ? "begin" : "end").Add("joint_index", jn - 1).ToString();
                _w.Add(r);
                if (jr.Body != null)
                {
                    var d = New(Id(IfcKeys.JointDevice(jk)), deckPartId, "element", "expansion joint device", "IFCDISCRETEACCESSORY", jr.DeviceName);
                    d.Support = jr.Support; d.Subtype = type; d.Material = "Steel (placeholder)"; d.Box = jr.Body.Bounds(); d.Length = jr.Length;
                    StationOf(d, d.Box);
                    d.Props = new Json().Add("end", jr.AtBegin ? "begin" : "end").Add("placeholder", true).ToString();
                    _w.Add(d);
                }
            }
            Bars(c.Bars, c.Marks, deckPartId, i => IfcKeys.DeckBar(bk, i), "Deck", def.Deck.Reinforcement.SteelYieldKsi, "");
            return slabId;
        }

        private void Bars(IEnumerable<BarPlacement> bars, IEnumerable<BarMark> marks, string parent, Func<int, string> key, string location, double fy, string support)
        {
            var mk = marks.GroupBy(m => m.Mark).ToDictionary(g => g.Key, g => g.First());
            int n = 0;
            foreach (var bar in bars)
            {
                var m = mk[bar.Mark];
                var r = New(Id(key(n++)), parent, "bar", "rebar", "IFCREINFORCINGBAR", "Bar " + m.Mark);
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
                int k = 0;
                foreach (var so in unit.Solids)
                {
                    if (so.Mesh.IsEmpty) continue;
                    var (cat, cls) = so.Kind switch
                    {
                        SubElementKind.Cap => (abut ? "abutment cap" : "pier cap", "IFCBEAM"),
                        SubElementKind.Backwall => ("backwall", "IFCWALL"),
                        SubElementKind.Pedestal => ("pedestal", "IFCCOLUMN"),
                        SubElementKind.Column => ("column", "IFCCOLUMN"),
                        SubElementKind.DrilledShaft => ("drilled shaft", "IFCPILE"),
                        SubElementKind.Pile => ("pile", "IFCPILE"),
                        _ => ("footing", "IFCFOOTING"),
                    };
                    var r = New(Id(IfcKeys.SubSolid(uk, k++)), uid, "element", cat, cls, so.Name);
                    r.Support = unit.Name; r.Tag = so;
                    if (_keysOnly) { _w.Add(r); continue; }
                    r.Material = so.IsConcrete ? concMat : "ASTM A572 Gr. 50 (HP piling)";
                    r.Volume = Math.Abs(so.Mesh.Volume()); r.Mass = r.Volume * (so.IsConcrete ? 2400 : 7850);
                    r.Box = so.Mesh.Bounds(); StationOf(r, r.Box);
                    r.Props = new Json().Add("description", so.Description).ToString();
                    _w.Add(r);
                }
                Bars(sub.Bars.Where(b => b.Unit == unit.Name), sub.Marks.Where(m => m.Unit == unit.Name), uid, i => IfcKeys.SubBar(uk, i), "", unit.Definition.Reinforcement.SteelYieldKsi, unit.Name);
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

        // ------------------------------------------------------------ prestressed

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
                r.Props = new Json().Add("shape", b.Shape.Name).Add("strands", b.Strands.Count).ToString();
                _w.Add(r);
                int n = 0;
                foreach (var st in b.Strands)
                {
                    var s = New(Id(IfcKeys.PsStrand(bk, b.Mark, n++)), aid, "bar", st.Depressed ? "depressed strand" : "strand", "IFCTENDON", st.Depressed ? "Depressed strand" : "Strand");
                    s.Tag = st;
                    s.Girder = a.Girder; s.Length = SBM.Core.Prestressed.PrestressedModel.PolyLength(st.Points);
                    foreach (var p in st.Points) s.Box.Add(p);
                    _w.Add(s);
                }
            }
            foreach (var p in ps.Pads)
            {
                var r = New(Id(IfcKeys.PsPad(bk, p.Mark)), superId, "element", "bearing pad", "IFCBEARING", "Pad " + p.Mark);
                r.Tag = p;
                r.Mark = p.Mark; r.Support = p.Support; r.Girder = $"G{p.Girder + 1}"; r.Box = p.Mesh.Bounds(); StationOf(r, r.Box);
                r.Props = new Json().Add("description", p.Description).ToString();
                _w.Add(r);
            }
        }
    }
}
