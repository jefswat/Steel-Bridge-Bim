using Microsoft.Data.Sqlite;

namespace SBM.IO.Database;

/// <summary>
/// Asset management tables of the companion database (G9): files (photos, documents, calc PDFs) referenced by location, their links to
/// elements / inspections / findings / calcs, inspections (AASHTO element-level condition states, NBI / SNBI component ratings), findings,
/// calc packages with their extracted values and the log of values applied to the model.
/// <para>These tables are NEVER touched by a rebuild (<see cref="ModelDatabase.Write"/> rewrites only <c>el</c>, <c>strings</c>, <c>props</c>
/// and <c>element_sheet</c>). Records are keyed by GlobalId (stable across rebuilds); a record whose element is no longer in the model is an
/// orphan: kept and reported (<c>v_asset_orphans</c>), never deleted.</para>
/// <para>Files are NOT stored in the database: they live in the assets folder (<c>&lt;model&gt;_assets</c> beside the database, subfolders
/// photos / documents / calcs / thumbnails), named by content hash; the database keeps <c>location</c> (path relative to the assets folder,
/// forward slashes) + <c>location_kind</c> ('file' now, 'url' for permalinks later), resolved by <see cref="AssetStore.Resolve"/>, with sha256 +
/// size to detect a missing or changed file (reported, never deleted).</para>
/// </summary>
public static class AssetSchema
{
    /// <summary>Version of the asset tables (meta <c>asset_schema_version</c>). 1 = G9 phase 1 (2026-10-19).</summary>
    public const int Version = 1;

    public static readonly string[] Tables =
        { "asset_files", "asset_links", "inspections", "element_condition", "component_ratings", "findings", "calcs", "calc_values", "calc_applied" };

    public const string Sql = """
        CREATE TABLE IF NOT EXISTS asset_files(
          id INTEGER PRIMARY KEY,
          kind TEXT NOT NULL CHECK(kind IN ('photo','document','calc')),
          location TEXT NOT NULL,
          location_kind TEXT NOT NULL DEFAULT 'file' CHECK(location_kind IN ('file','url')),
          file_name TEXT NOT NULL, media_type TEXT, size_bytes INTEGER, sha256 TEXT,
          thumb_location TEXT,
          taken_at TEXT, added_at TEXT NOT NULL, added_by TEXT, author TEXT, caption TEXT, note TEXT);
        CREATE TABLE IF NOT EXISTS asset_links(
          file_id INTEGER NOT NULL,
          target_kind TEXT NOT NULL CHECK(target_kind IN ('element','inspection','finding','calc')),
          target TEXT NOT NULL,
          PRIMARY KEY(file_id, target_kind, target)) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS inspections(
          id INTEGER PRIMARY KEY,
          inspection_date TEXT NOT NULL,
          kind TEXT NOT NULL DEFAULT 'routine',
          structure_number TEXT NOT NULL DEFAULT '',
          bridge_global_id TEXT,
          standard TEXT, inspector TEXT, agency TEXT, note TEXT,
          created_at TEXT, created_by TEXT,
          UNIQUE(inspection_date, kind, structure_number));
        CREATE TABLE IF NOT EXISTS element_condition(
          id INTEGER PRIMARY KEY,
          inspection_id INTEGER NOT NULL,
          global_id TEXT NOT NULL DEFAULT '',
          element_ref TEXT NOT NULL DEFAULT '',
          element_number TEXT NOT NULL,
          element_name TEXT,
          parent_element_number TEXT NOT NULL DEFAULT '',
          defect_number TEXT NOT NULL DEFAULT '',
          defect_name TEXT,
          unit TEXT, total_qty REAL, cs1 REAL, cs2 REAL, cs3 REAL, cs4 REAL,
          note TEXT,
          UNIQUE(inspection_id, element_ref, element_number, parent_element_number, defect_number));
        CREATE TABLE IF NOT EXISTS component_ratings(
          inspection_id INTEGER NOT NULL,
          component TEXT NOT NULL,
          global_id TEXT NOT NULL DEFAULT '',
          rating TEXT NOT NULL CHECK(rating IN ('0','1','2','3','4','5','6','7','8','9','N')),
          note TEXT,
          PRIMARY KEY(inspection_id, component, global_id)) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS findings(
          id INTEGER PRIMARY KEY,
          global_id TEXT NOT NULL,
          inspection_id INTEGER,
          found_at TEXT NOT NULL,
          description TEXT NOT NULL,
          defect_type TEXT,
          severity TEXT NOT NULL DEFAULT 'medium' CHECK(severity IN ('low','medium','high','critical')),
          location_text TEXT,
          x REAL, y REAL, z REAL, station REAL, offset REAL,
          recommended_action TEXT,
          status TEXT NOT NULL DEFAULT 'open' CHECK(status IN ('open','monitor','scheduled','repaired','closed')),
          due_date TEXT, closed_at TEXT,
          created_at TEXT, created_by TEXT, updated_at TEXT, updated_by TEXT, note TEXT);
        CREATE TABLE IF NOT EXISTS calcs(
          id INTEGER PRIMARY KEY,
          file_id INTEGER NOT NULL,
          title TEXT NOT NULL,
          revision TEXT, calc_date TEXT, engineer TEXT,
          page_count INTEGER,
          summary_pages TEXT NOT NULL,
          summary_text TEXT,
          added_at TEXT, added_by TEXT, note TEXT);
        CREATE TABLE IF NOT EXISTS calc_values(
          id INTEGER PRIMARY KEY,
          calc_id INTEGER NOT NULL,
          key TEXT,
          name TEXT,
          global_id TEXT NOT NULL DEFAULT '',
          mark TEXT,
          property_path TEXT,
          result_path TEXT,
          bridge TEXT,
          field TEXT,
          value_text TEXT NOT NULL,
          value REAL,
          unit TEXT,
          tolerance TEXT,
          page INTEGER,
          quote TEXT,
          extracted_by TEXT, extracted_at TEXT);
        CREATE TABLE IF NOT EXISTS calc_applied(
          id INTEGER PRIMARY KEY,
          calc_id INTEGER NOT NULL,
          value_id INTEGER,
          calc_title TEXT,
          property_path TEXT NOT NULL,
          old_value TEXT, new_value TEXT,
          page INTEGER, quote TEXT,
          applied_by TEXT, applied_at TEXT NOT NULL,
          model_file TEXT);
        CREATE INDEX IF NOT EXISTS ix_al_target ON asset_links(target_kind, target);
        CREATE INDEX IF NOT EXISTS ix_ec_gid ON element_condition(global_id);
        CREATE INDEX IF NOT EXISTS ix_find_gid ON findings(global_id);
        CREATE INDEX IF NOT EXISTS ix_cv_calc ON calc_values(calc_id);
        CREATE INDEX IF NOT EXISTS ix_cv_gid ON calc_values(global_id);
        """;

    /// <summary>Views over the asset tables. They reference only the table <c>el</c> (never the view <c>elements</c>, which a rebuild drops and recreates).</summary>
    public const string ViewSql = """
        DROP VIEW IF EXISTS v_asset_orphans;
        CREATE VIEW v_asset_orphans AS
          SELECT 'asset_links' AS source, CAST(file_id AS TEXT) AS record, target AS global_id FROM asset_links
            WHERE target_kind = 'element' AND target NOT IN (SELECT global_id FROM el)
          UNION ALL SELECT 'element_condition', CAST(id AS TEXT), global_id FROM element_condition WHERE global_id <> '' AND global_id NOT IN (SELECT global_id FROM el)
          UNION ALL SELECT 'component_ratings', CAST(inspection_id AS TEXT) || ':' || component, global_id FROM component_ratings WHERE global_id <> '' AND global_id NOT IN (SELECT global_id FROM el)
          UNION ALL SELECT 'findings', CAST(id AS TEXT), global_id FROM findings WHERE global_id NOT IN (SELECT global_id FROM el)
          UNION ALL SELECT 'calc_values', CAST(id AS TEXT), global_id FROM calc_values WHERE global_id <> '' AND global_id NOT IN (SELECT global_id FROM el)
          UNION ALL SELECT 'inspections', CAST(id AS TEXT), bridge_global_id FROM inspections WHERE COALESCE(bridge_global_id,'') <> '' AND bridge_global_id NOT IN (SELECT global_id FROM el);
        DROP VIEW IF EXISTS v_condition_history;
        CREATE VIEW v_condition_history AS
          SELECT i.inspection_date, i.kind AS inspection_kind, i.structure_number, i.inspector, c.global_id, e.name AS element_model_name, e.mark,
                 c.element_ref, c.element_number, c.element_name, c.parent_element_number, c.defect_number, c.defect_name, c.unit, c.total_qty,
                 c.cs1, c.cs2, c.cs3, c.cs4, c.note, c.inspection_id, c.id
          FROM element_condition c JOIN inspections i ON i.id = c.inspection_id LEFT JOIN el e ON e.global_id = c.global_id;
        DROP VIEW IF EXISTS v_open_findings;
        CREATE VIEW v_open_findings AS
          SELECT f.*, e.name AS element_name, e.mark FROM findings f LEFT JOIN el e ON e.global_id = f.global_id WHERE f.status NOT IN ('repaired','closed');
        """;

    /// <summary>Creates / migrates the asset tables (idempotent; called by <see cref="ModelDatabase.Open"/>). Existing databases without them get
    /// them on first open; <c>meta.asset_schema_version</c> records the version.</summary>
    public static void Ensure(SqliteConnection cn)
    {
        int have = int.TryParse(ModelDatabase.Scalar<string>(cn, "SELECT value FROM meta WHERE key='asset_schema_version'"), out int v) ? v : 0;
        if (have >= Version && ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM sqlite_master WHERE name='v_asset_orphans'") > 0) return;
        ModelDatabase.Exec(cn, Sql);
        // (future migrations: if (have < 2) ALTER TABLE ...)
        ModelDatabase.Exec(cn, ViewSql);
        ModelDatabase.Exec(cn, "INSERT OR REPLACE INTO meta VALUES('asset_schema_version', @v)", ("@v", Version.ToString()));
    }
}
