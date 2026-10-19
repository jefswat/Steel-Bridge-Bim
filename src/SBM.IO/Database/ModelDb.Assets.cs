using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SBM.IO.Database;

/// <summary>A photo, document or calc PDF referenced by the database (the file itself lives in the assets folder or at a URL).</summary>
public sealed class AssetFile
{
    public long Id { get; init; }
    public string Kind { get; init; } = "";
    public string Location { get; init; } = "";
    public string LocationKind { get; init; } = "file";
    public string FileName { get; init; } = "";
    public string MediaType { get; init; } = "";
    public long? SizeBytes { get; init; }
    public string Sha256 { get; init; } = "";
    public string ThumbLocation { get; init; } = "";
    public string TakenAt { get; init; } = "";
    public string AddedAt { get; init; } = "";
    public string AddedBy { get; init; } = "";
    public string Author { get; init; } = "";
    public string Caption { get; init; } = "";
    public string Note { get; init; } = "";
    /// <summary>Links as "element:&lt;GlobalId&gt;", "inspection:&lt;id&gt;", "finding:&lt;id&gt;", "calc:&lt;id&gt;".</summary>
    public List<string> Links { get; init; } = new();
    public bool IsImage => AssetStore.IsImage(MediaType);
}

public sealed class Inspection
{
    public long Id { get; init; }
    public string Date { get; init; } = "";
    public string Kind { get; init; } = "routine";
    public string StructureNumber { get; init; } = "";
    public string BridgeGlobalId { get; init; } = "";
    public string Standard { get; init; } = "";
    public string Inspector { get; init; } = "";
    public string Agency { get; init; } = "";
    public string Note { get; init; } = "";
    public string Label => $"{Date} {Kind}" + (StructureNumber.Length > 0 ? " " + StructureNumber : "");
}

/// <summary>One AASHTO element-level row (MBEI / SNBI): an element total (<see cref="DefectNumber"/> empty) or a defect of it, with the
/// condition state quantities CS1-CS4 in <see cref="Unit"/>.</summary>
public sealed class ConditionRow
{
    public long Id { get; init; }
    public long InspectionId { get; init; }
    public string InspectionDate { get; init; } = "";
    public string InspectionKind { get; init; } = "";
    public string GlobalId { get; init; } = "";
    public string ElementRef { get; init; } = "";
    public string ElementNumber { get; init; } = "";
    public string ElementName { get; init; } = "";
    public string ParentElementNumber { get; init; } = "";
    public string DefectNumber { get; init; } = "";
    public string DefectName { get; init; } = "";
    public string Unit { get; init; } = "";
    public double? TotalQty { get; init; }
    public double? Cs1 { get; init; }
    public double? Cs2 { get; init; }
    public double? Cs3 { get; init; }
    public double? Cs4 { get; init; }
    public string Note { get; init; } = "";
    /// <summary>Set by <see cref="ModelDb.ConditionFor"/>: the element the row is stored on when it is an ancestor ("" = the element itself).</summary>
    public string Via { get; set; } = "";
}

public sealed class ComponentRating
{
    public long InspectionId { get; init; }
    public string InspectionDate { get; init; } = "";
    public string Component { get; init; } = "";
    public string GlobalId { get; init; } = "";
    public string Rating { get; init; } = "";
    public string Note { get; init; } = "";
}

public sealed class Finding
{
    public long Id { get; init; }
    public string GlobalId { get; init; } = "";
    public long? InspectionId { get; init; }
    public string FoundAt { get; init; } = "";
    public string Description { get; init; } = "";
    public string DefectType { get; init; } = "";
    public string Severity { get; init; } = "medium";
    public string LocationText { get; init; } = "";
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public double? Station { get; init; }
    public double? Offset { get; init; }
    public string RecommendedAction { get; init; } = "";
    public string Status { get; init; } = "open";
    public string DueDate { get; init; } = "";
    public string ClosedAt { get; init; } = "";
    public string CreatedBy { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string UpdatedAt { get; init; } = "";
    public string Note { get; init; } = "";
    public bool IsOpen => Status is not ("repaired" or "closed");
}

public sealed class CalcPackage
{
    public long Id { get; init; }
    public long FileId { get; init; }
    public string Title { get; init; } = "";
    public string Revision { get; init; } = "";
    public string CalcDate { get; init; } = "";
    public string Engineer { get; init; } = "";
    public int? PageCount { get; init; }
    public string SummaryPages { get; init; } = "";
    public string SummaryText { get; init; } = "";
    public string AddedAt { get; init; } = "";
    public string AddedBy { get; init; } = "";
    public string Note { get; init; } = "";
    public List<int> SummaryPageList => SummaryPages.Length > 0 ? AssetStore.ParsePages(SummaryPages) : new List<int> { 1 };
}

/// <summary>A value extracted from a calc summary page. Target = exactly one of <see cref="PropertyPath"/> (a model INPUT: compared and
/// can be applied), <see cref="ResultPath"/> (a model RESULT on the bridge's <c>BridgeModel</c>: compared only) or <see cref="Field"/> on an
/// element (<see cref="GlobalId"/> / <see cref="Mark"/>; a database column such as mass_kg or a properties key: compared only).</summary>
public sealed class CalcValue
{
    public long Id { get; set; }
    public long CalcId { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string GlobalId { get; set; } = "";
    public string Mark { get; set; } = "";
    public string PropertyPath { get; set; } = "";
    public string ResultPath { get; set; } = "";
    public string Bridge { get; set; } = "";
    public string Field { get; set; } = "";
    /// <summary>The calc value as text, with its unit when it has one ("60 in", "W36x135", "50 ksi"): what an apply sets.</summary>
    public string ValueText { get; set; } = "";
    public double? Value { get; set; }
    public string Unit { get; set; } = "";
    /// <summary>Comparison tolerance: a length / value with unit ("1/16\"", "0.5 kip") or a percentage ("1%"). Empty = default.</summary>
    public string Tolerance { get; set; } = "";
    public int? Page { get; set; }
    public string Quote { get; set; } = "";
    public string ExtractedBy { get; set; } = "";
    public string ExtractedAt { get; set; } = "";
    public string TargetText => PropertyPath.Length > 0 ? PropertyPath : ResultPath.Length > 0 ? (Bridge.Length > 0 ? Bridge + ": " : "") + "result " + ResultPath
        : $"{(Mark.Length > 0 ? Mark : GlobalId)}.{Field}";
}

public sealed class AppliedChange
{
    public long Id { get; init; }
    public long CalcId { get; init; }
    public long? ValueId { get; init; }
    public string CalcTitle { get; init; } = "";
    public string PropertyPath { get; init; } = "";
    public string OldValue { get; init; } = "";
    public string NewValue { get; init; } = "";
    public int? Page { get; init; }
    public string Quote { get; init; } = "";
    public string AppliedBy { get; init; } = "";
    public string AppliedAt { get; init; } = "";
    public string ModelFile { get; init; } = "";
}

/// <summary>An asset record whose element is no longer in the model (kept, reported).</summary>
public sealed record AssetOrphan(string Source, string Record, string GlobalId);

/// <summary>A referenced file that is missing or changed (kept, reported).</summary>
public sealed record AssetProblem(AssetFile File, AssetFileState State, string Path);

/// <summary>Asset management (G9): photos / documents / calcs by reference, inspections, findings, calc values. See <see cref="AssetSchema"/>.</summary>
public sealed partial class ModelDb
{
    private static string Now() => DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
    private static string Who(string? by) => string.IsNullOrWhiteSpace(by) ? Environment.UserName : by;
    private static long? L(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);
    private static int? I(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);
    private static object? Db(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private SqliteCommand Cmd(string sql, params (string, object?)[] ps)
    {
        var c = _cn.CreateCommand();
        c.CommandText = sql;
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return c;
    }

    private List<T> Query<T>(string sql, Func<SqliteDataReader, T> read, params (string, object?)[] ps)
    {
        using var c = Cmd(sql, ps);
        using var r = c.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(read(r));
        return list;
    }

    private long Insert(string sql, params (string, object?)[] ps)
    {
        using var c = Cmd(sql + "; SELECT last_insert_rowid();", ps);
        return (long)c.ExecuteScalar()!;
    }

    // ------------------------------------------------------------------ assets folder + files

    /// <summary>The assets folder: <c>meta.assets_folder</c> (relative to the database folder, or absolute) or <c>&lt;db name&gt;_assets</c>.</summary>
    public string AssetsFolder
    {
        get
        {
            string m = Meta("assets_folder");
            return m.Length == 0 ? AssetStore.DefaultFolder(Path) : System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(m) ? m : System.IO.Path.Combine(Folder, m));
        }
    }

    public AssetLocation ResolveAsset(AssetFile f) => AssetStore.Resolve(AssetsFolder, f.Location, f.LocationKind);

    /// <summary>Local path of the thumbnail, or null when there is none (or it is missing).</summary>
    public string? ThumbnailPath(AssetFile f)
    {
        if (f.ThumbLocation.Length == 0) return null;
        var p = AssetStore.Resolve(AssetsFolder, f.ThumbLocation, "file").LocalPath;
        return p != null && File.Exists(p) ? p : null;
    }

    public void SetThumbnail(long fileId, string relLocation) =>
        ModelDatabase.Exec(_cn, "UPDATE asset_files SET thumb_location=$t WHERE id=$i", ("$t", relLocation), ("$i", fileId));

    private const string FileCols = "id,kind,location,location_kind,file_name,media_type,size_bytes,sha256,thumb_location,taken_at,added_at,added_by,author,caption,note";

    private AssetFile ReadFile(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0), Kind = S(r, 1), Location = S(r, 2), LocationKind = S(r, 3), FileName = S(r, 4), MediaType = S(r, 5), SizeBytes = L(r, 6),
        Sha256 = S(r, 7), ThumbLocation = S(r, 8), TakenAt = S(r, 9), AddedAt = S(r, 10), AddedBy = S(r, 11), Author = S(r, 12), Caption = S(r, 13), Note = S(r, 14),
    };

    private List<AssetFile> WithLinks(List<AssetFile> files)
    {
        foreach (var f in files)
            f.Links.AddRange(Query("SELECT target_kind || ':' || target FROM asset_links WHERE file_id=$f ORDER BY target_kind, target", r => r.GetString(0), ("$f", f.Id)));
        return files;
    }

    public AssetFile? GetFile(long id) => WithLinks(Query($"SELECT {FileCols} FROM asset_files WHERE id=$i", ReadFile, ("$i", id))).FirstOrDefault();

    /// <summary>Files of a kind (null = all), optionally only those linked to <paramref name="targetKind"/>:<paramref name="target"/>.</summary>
    public List<AssetFile> Files(string? kind = null, string? targetKind = null, string? target = null)
    {
        string sql = $"SELECT {FileCols} FROM asset_files f WHERE ($k IS NULL OR kind=$k)"
                     + (targetKind != null ? " AND EXISTS (SELECT 1 FROM asset_links l WHERE l.file_id=f.id AND l.target_kind=$tk AND l.target=$t)" : "")
                     + " ORDER BY COALESCE(taken_at, added_at) DESC, id DESC";
        return WithLinks(Query(sql, ReadFile, ("$k", kind), ("$tk", targetKind), ("$t", target)));
    }

    /// <summary>Photos / documents linked to an element (directly, or to a finding of it).</summary>
    public List<AssetFile> FilesForElement(string globalId, string? kind = null)
    {
        string sql = $"""
            SELECT {FileCols} FROM asset_files f WHERE ($k IS NULL OR kind=$k) AND EXISTS (SELECT 1 FROM asset_links l WHERE l.file_id=f.id AND
              ((l.target_kind='element' AND l.target=$g) OR (l.target_kind='finding' AND l.target IN (SELECT CAST(id AS TEXT) FROM findings WHERE global_id=$g))))
            ORDER BY COALESCE(taken_at, added_at) DESC, id DESC
            """;
        return WithLinks(Query(sql, ReadFile, ("$k", kind), ("$g", globalId)));
    }

    public sealed class AddFileOptions
    {
        public string? Caption { get; set; }
        public string? Author { get; set; }
        /// <summary>When the photo was taken / the document dated (ISO). Null = EXIF date of a JPEG, else empty.</summary>
        public string? TakenAt { get; set; }
        public string? Note { get; set; }
        public string? By { get; set; }
        public bool Thumbnail { get; set; } = true;
        public int ThumbnailPage { get; set; } = 1;
        public Action<string>? Log { get; set; }
    }

    /// <summary>Copies a file into the assets folder and records it (the same content of the same kind is recorded once: the existing record is
    /// returned, with the caption etc. filled where empty).</summary>
    public AssetFile AddFile(string kind, string source, AddFileOptions? o = null)
    {
        o ??= new AddFileOptions();
        string folder = AssetsFolder;
        var (rel, sha, size) = AssetStore.Import(folder, kind, source);
        long? existing = ModelDatabase.Scalar<long?>(_cn, "SELECT id FROM asset_files WHERE kind=$k AND sha256=$s", ("$k", kind), ("$s", sha));
        string media = AssetStore.MediaType(source);
        string? thumb = null;
        if (o.Thumbnail && (AssetStore.IsImage(media) || media == "application/pdf"))
            thumb = AssetStore.MakeThumbnail(folder, System.IO.Path.Combine(folder, rel.Replace('/', System.IO.Path.DirectorySeparatorChar)), sha, o.ThumbnailPage, log: o.Log);
        if (existing is { } id)
        {
            ModelDatabase.Exec(_cn, """
                UPDATE asset_files SET caption=COALESCE(NULLIF(caption,''),$c), author=COALESCE(NULLIF(author,''),$a), taken_at=COALESCE(NULLIF(taken_at,''),$t),
                  thumb_location=COALESCE(NULLIF(thumb_location,''),$th), location=$l, location_kind='file', size_bytes=$sz WHERE id=$i
                """, ("$c", Db(o.Caption)), ("$a", Db(o.Author)), ("$t", Db(o.TakenAt)), ("$th", thumb), ("$l", rel), ("$sz", size), ("$i", id));
            return GetFile(id)!;
        }
        string? taken = o.TakenAt ?? (media == "image/jpeg" ? AssetStore.ExifDate(source) : null);
        long nid = Insert("""
            INSERT INTO asset_files(kind, location, location_kind, file_name, media_type, size_bytes, sha256, thumb_location, taken_at, added_at, added_by, author, caption, note)
            VALUES($k, $l, 'file', $n, $m, $sz, $s, $th, $t, $at, $by, $a, $c, $no)
            """, ("$k", kind), ("$l", rel), ("$n", System.IO.Path.GetFileName(source)), ("$m", media), ("$sz", size), ("$s", sha), ("$th", thumb), ("$t", taken),
            ("$at", Now()), ("$by", Who(o.By)), ("$a", Db(o.Author)), ("$c", Db(o.Caption)), ("$no", Db(o.Note)));
        return GetFile(nid)!;
    }

    /// <summary>Records a file that lives at a URL (permalink): nothing is copied; sha256 / size are optional.</summary>
    public AssetFile AddUrl(string kind, string url, string fileName, AddFileOptions? o = null, string? sha256 = null, long? size = null)
    {
        o ??= new AddFileOptions();
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) throw new ArgumentException($"Not an absolute URL: {url}");
        long nid = Insert("""
            INSERT INTO asset_files(kind, location, location_kind, file_name, media_type, size_bytes, sha256, taken_at, added_at, added_by, author, caption, note)
            VALUES($k, $l, 'url', $n, $m, $sz, $s, $t, $at, $by, $a, $c, $no)
            """, ("$k", kind), ("$l", url), ("$n", fileName), ("$m", AssetStore.MediaType(fileName)), ("$sz", size), ("$s", sha256), ("$t", Db(o.TakenAt)),
            ("$at", Now()), ("$by", Who(o.By)), ("$a", Db(o.Author)), ("$c", Db(o.Caption)), ("$no", Db(o.Note)));
        return GetFile(nid)!;
    }

    /// <summary>Links a file to an element (GlobalId), inspection, finding or calc (id). Idempotent.</summary>
    public void Link(long fileId, string targetKind, string target) =>
        ModelDatabase.Exec(_cn, "INSERT OR IGNORE INTO asset_links(file_id, target_kind, target) VALUES($f, $k, $t)", ("$f", fileId), ("$k", targetKind), ("$t", target));

    public void Unlink(long fileId, string targetKind, string target) =>
        ModelDatabase.Exec(_cn, "DELETE FROM asset_links WHERE file_id=$f AND target_kind=$k AND target=$t", ("$f", fileId), ("$k", targetKind), ("$t", target));

    public void SetCaption(long fileId, string caption) => ModelDatabase.Exec(_cn, "UPDATE asset_files SET caption=$c WHERE id=$i", ("$c", caption), ("$i", fileId));

    /// <summary>Referenced files that are missing or changed (size; SHA-256 too when <paramref name="hash"/>). Never deletes anything.</summary>
    public List<AssetProblem> CheckFiles(bool hash = false)
    {
        var list = new List<AssetProblem>();
        foreach (var f in Files())
        {
            var loc = ResolveAsset(f);
            var st = AssetStore.Check(loc, f.SizeBytes, f.Sha256, hash);
            if (st is not (AssetFileState.Ok or AssetFileState.Url)) list.Add(new AssetProblem(f, st, loc.Target));
        }
        return list;
    }

    /// <summary>Asset records whose element is no longer in the model (kept, reported).</summary>
    public List<AssetOrphan> Orphans() => Query("SELECT source, record, global_id FROM v_asset_orphans ORDER BY source, record", r => new AssetOrphan(S(r, 0), S(r, 1), S(r, 2)));

    /// <summary>Counts per asset table.</summary>
    public Dictionary<string, long> AssetCounts()
    {
        var d = new Dictionary<string, long>();
        foreach (var k in new[] { "photo", "document", "calc" }) d[k == "calc" ? "calc_pdfs" : k + "s"] = ModelDatabase.Scalar<long>(_cn, "SELECT COUNT(*) FROM asset_files WHERE kind=$k", ("$k", k));
        foreach (var t in new[] { "inspections", "element_condition", "component_ratings", "findings", "calcs", "calc_values", "calc_applied" })
            d[t] = ModelDatabase.Scalar<long>(_cn, $"SELECT COUNT(*) FROM {t}");
        d["open_findings"] = ModelDatabase.Scalar<long>(_cn, "SELECT COUNT(*) FROM findings WHERE status NOT IN ('repaired','closed')");
        return d;
    }

    // ------------------------------------------------------------------ element lookup

    /// <summary>Resolves an element reference: a GlobalId, else an exact mark, else an exact name (case-insensitive). Throws when nothing or
    /// more than one element matches.</summary>
    public string FindElement(string text)
    {
        text = text.Trim();
        if (ModelDatabase.Scalar<long?>(_cn, "SELECT id FROM el WHERE global_id=$g", ("$g", text)) != null) return text;
        foreach (var col in new[] { "mark", "name" })
        {
            var ids = Query($"SELECT global_id FROM elements WHERE {col} = $t COLLATE NOCASE LIMIT 6", r => r.GetString(0), ("$t", text));
            if (ids.Count == 1) return ids[0];
            if (ids.Count > 1)
                throw new ArgumentException($"'{text}' matches several elements by {col} ({string.Join(", ", ids.Take(5).Select(i => DescribeElement(i)))}{(ids.Count > 5 ? ", ..." : "")}): use the GlobalId.");
        }
        throw new ArgumentException($"No element with GlobalId, mark or name '{text}' (sbm db <model> --sheets-for \"{text}\" lists partial matches).");
    }

    /// <summary>Like <see cref="FindElement"/> but returns null instead of throwing.</summary>
    public string? TryFindElement(string text)
    {
        try { return FindElement(text); } catch (ArgumentException) { return null; }
    }

    public string DescribeElement(string globalId) => Get(globalId) is { } e ? $"{(e.Name.Length > 0 ? e.Name : e.Category)} [{e.Category}] {globalId}" : globalId + " (not in the model)";

    // ------------------------------------------------------------------ inspections

    private const string InspCols = "id,inspection_date,kind,structure_number,bridge_global_id,standard,inspector,agency,note";
    private static Inspection ReadInsp(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0), Date = S(r, 1), Kind = S(r, 2), StructureNumber = S(r, 3), BridgeGlobalId = S(r, 4), Standard = S(r, 5), Inspector = S(r, 6), Agency = S(r, 7), Note = S(r, 8),
    };

    /// <summary>Finds the inspection (date, kind, structure number) or creates it; other fields fill empty ones.</summary>
    public Inspection AddInspection(string date, string kind = "routine", string structureNumber = "", string? inspector = null, string? standard = null,
        string? agency = null, string? bridgeGlobalId = null, string? note = null, string? by = null)
    {
        date = NormDate(date);
        kind = string.IsNullOrWhiteSpace(kind) ? "routine" : kind.Trim().ToLowerInvariant();
        ModelDatabase.Exec(_cn, """
            INSERT INTO inspections(inspection_date, kind, structure_number, bridge_global_id, standard, inspector, agency, note, created_at, created_by)
            VALUES($d, $k, $s, $b, $st, $i, $a, $n, $at, $by)
            ON CONFLICT(inspection_date, kind, structure_number) DO UPDATE SET
              inspector=COALESCE(NULLIF(inspector,''), excluded.inspector), standard=COALESCE(NULLIF(standard,''), excluded.standard),
              agency=COALESCE(NULLIF(agency,''), excluded.agency), bridge_global_id=COALESCE(NULLIF(bridge_global_id,''), excluded.bridge_global_id),
              note=COALESCE(NULLIF(note,''), excluded.note)
            """, ("$d", date), ("$k", kind), ("$s", structureNumber ?? ""), ("$b", Db(bridgeGlobalId)), ("$st", Db(standard)), ("$i", Db(inspector)), ("$a", Db(agency)),
            ("$n", Db(note)), ("$at", Now()), ("$by", Who(by)));
        return Query($"SELECT {InspCols} FROM inspections WHERE inspection_date=$d AND kind=$k AND structure_number=$s", ReadInsp, ("$d", date), ("$k", kind), ("$s", structureNumber ?? "")).Single();
    }

    public List<Inspection> Inspections() => Query($"SELECT {InspCols} FROM inspections ORDER BY inspection_date DESC, id DESC", ReadInsp);
    public Inspection? GetInspection(long id) => Query($"SELECT {InspCols} FROM inspections WHERE id=$i", ReadInsp, ("$i", id)).FirstOrDefault();

    /// <summary>"2026-05-01", "5/1/2026", "2026-05-01T10:00" → ISO date (time kept when given).</summary>
    public static string NormDate(string text)
    {
        text = text.Trim();
        if (DateTime.TryParseExact(text, new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return text.Length <= 10 ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("s", CultureInfo.InvariantCulture);
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out d))
            return d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("s", CultureInfo.InvariantCulture);
        throw new ArgumentException($"Can't read the date '{text}' (use yyyy-MM-dd).");
    }

    /// <summary>Adds or replaces one element-level row (key: inspection, element ref, element number, parent element number, defect number).</summary>
    public void UpsertCondition(long inspectionId, ConditionRow c)
    {
        ModelDatabase.Exec(_cn, """
            INSERT INTO element_condition(inspection_id, global_id, element_ref, element_number, element_name, parent_element_number, defect_number, defect_name, unit, total_qty, cs1, cs2, cs3, cs4, note)
            VALUES($i, $g, $r, $en, $nm, $pe, $dn, $dm, $u, $t, $c1, $c2, $c3, $c4, $n)
            ON CONFLICT(inspection_id, element_ref, element_number, parent_element_number, defect_number) DO UPDATE SET
              global_id=excluded.global_id, element_name=excluded.element_name, defect_name=excluded.defect_name, unit=excluded.unit, total_qty=excluded.total_qty,
              cs1=excluded.cs1, cs2=excluded.cs2, cs3=excluded.cs3, cs4=excluded.cs4, note=excluded.note
            """, ("$i", inspectionId), ("$g", c.GlobalId), ("$r", c.ElementRef), ("$en", c.ElementNumber), ("$nm", Db(c.ElementName)), ("$pe", c.ParentElementNumber),
            ("$dn", c.DefectNumber), ("$dm", Db(c.DefectName)), ("$u", Db(c.Unit)), ("$t", c.TotalQty), ("$c1", c.Cs1), ("$c2", c.Cs2), ("$c3", c.Cs3), ("$c4", c.Cs4), ("$n", Db(c.Note)));
    }

    private const string CondCols = "c.id,c.inspection_id,i.inspection_date,i.kind,c.global_id,c.element_ref,c.element_number,c.element_name,c.parent_element_number,c.defect_number,c.defect_name,c.unit,c.total_qty,c.cs1,c.cs2,c.cs3,c.cs4,c.note";
    private static ConditionRow ReadCond(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0), InspectionId = r.GetInt64(1), InspectionDate = S(r, 2), InspectionKind = S(r, 3), GlobalId = S(r, 4), ElementRef = S(r, 5), ElementNumber = S(r, 6),
        ElementName = S(r, 7), ParentElementNumber = S(r, 8), DefectNumber = S(r, 9), DefectName = S(r, 10), Unit = S(r, 11), TotalQty = D(r, 12),
        Cs1 = D(r, 13), Cs2 = D(r, 14), Cs3 = D(r, 15), Cs4 = D(r, 16), Note = S(r, 17),
    };

    /// <summary>All element-level rows (history), newest inspection first; optionally of one inspection.</summary>
    public List<ConditionRow> Condition(long? inspectionId = null) =>
        Query($"SELECT {CondCols} FROM element_condition c JOIN inspections i ON i.id=c.inspection_id WHERE ($i IS NULL OR c.inspection_id=$i) " +
              "ORDER BY i.inspection_date DESC, c.element_number, c.parent_element_number, c.defect_number", ReadCond, ("$i", inspectionId));

    /// <summary>Element-level history of an element: rows stored on it, else on its nearest ancestor that has rows (AASHTO elements are often
    /// recorded per girder line / span / bridge), newest inspection first. <see cref="ConditionRow.Via"/> names the ancestor.</summary>
    public List<ConditionRow> ConditionFor(string globalId)
    {
        var chain = Chain(globalId);
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var rows = Query($"SELECT {CondCols} FROM element_condition c JOIN inspections i ON i.id=c.inspection_id WHERE c.global_id=$g " +
                             "ORDER BY i.inspection_date DESC, c.element_number, c.parent_element_number, c.defect_number", ReadCond, ("$g", chain[i].GlobalId));
            if (rows.Count == 0) continue;
            if (i != chain.Count - 1) foreach (var r in rows) r.Via = chain[i].Name.Length > 0 ? chain[i].Name : chain[i].Category;
            return rows;
        }
        return Query($"SELECT {CondCols} FROM element_condition c JOIN inspections i ON i.id=c.inspection_id WHERE c.global_id=$g", ReadCond, ("$g", globalId));
    }

    public static readonly string[] Components = { "deck", "superstructure", "substructure", "culvert", "channel", "channel_protection", "waterway", "scour", "bearings", "joints", "railings", "approach" };

    public void SetRating(long inspectionId, string component, string rating, string globalId = "", string? note = null)
    {
        rating = rating.Trim().ToUpperInvariant();
        if (rating.Length != 1 || !"0123456789N".Contains(rating[0])) throw new ArgumentException($"Rating '{rating}' must be 0-9 or N.");
        ModelDatabase.Exec(_cn, """
            INSERT INTO component_ratings(inspection_id, component, global_id, rating, note) VALUES($i, $c, $g, $r, $n)
            ON CONFLICT(inspection_id, component, global_id) DO UPDATE SET rating=excluded.rating, note=excluded.note
            """, ("$i", inspectionId), ("$c", component.Trim().ToLowerInvariant()), ("$g", globalId), ("$r", rating), ("$n", Db(note)));
    }

    /// <summary>Component ratings, newest inspection first; optionally those of one element (exactly) or of the whole structure (globalId "").</summary>
    public List<ComponentRating> Ratings(string? globalId = null) =>
        Query("""
            SELECT r.inspection_id, i.inspection_date, r.component, r.global_id, r.rating, r.note FROM component_ratings r JOIN inspections i ON i.id=r.inspection_id
            WHERE ($g IS NULL OR r.global_id=$g) ORDER BY i.inspection_date DESC, r.component
            """, r => new ComponentRating { InspectionId = r.GetInt64(0), InspectionDate = S(r, 1), Component = S(r, 2), GlobalId = S(r, 3), Rating = S(r, 4), Note = S(r, 5) }, ("$g", globalId));

    /// <summary>Ratings that apply to an element: stored on it or on an ancestor (e.g. the bridge unit), or structure-wide (globalId "").</summary>
    public List<ComponentRating> RatingsFor(string globalId)
    {
        var ids = Chain(globalId).Select(e => e.GlobalId).Append("").ToList();
        return Ratings().Where(r => ids.Contains(r.GlobalId)).ToList();
    }

    // ------------------------------------------------------------------ findings

    private const string FindCols = "id,global_id,inspection_id,found_at,description,defect_type,severity,location_text,x,y,z,station,offset,recommended_action,status,due_date,closed_at,created_by,created_at,updated_at,note";
    private static Finding ReadFinding(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0), GlobalId = S(r, 1), InspectionId = L(r, 2), FoundAt = S(r, 3), Description = S(r, 4), DefectType = S(r, 5), Severity = S(r, 6), LocationText = S(r, 7),
        X = D(r, 8), Y = D(r, 9), Z = D(r, 10), Station = D(r, 11), Offset = D(r, 12), RecommendedAction = S(r, 13), Status = S(r, 14), DueDate = S(r, 15), ClosedAt = S(r, 16),
        CreatedBy = S(r, 17), CreatedAt = S(r, 18), UpdatedAt = S(r, 19), Note = S(r, 20),
    };

    public static readonly string[] Severities = { "low", "medium", "high", "critical" };
    public static readonly string[] FindingStatuses = { "open", "monitor", "scheduled", "repaired", "closed" };

    public Finding AddFinding(Finding f, string? by = null)
    {
        if (string.IsNullOrWhiteSpace(f.Description)) throw new ArgumentException("A finding needs a description.");
        if (!Severities.Contains(f.Severity)) throw new ArgumentException($"Severity must be one of {string.Join(", ", Severities)}.");
        if (!FindingStatuses.Contains(f.Status)) throw new ArgumentException($"Status must be one of {string.Join(", ", FindingStatuses)}.");
        long id = Insert("""
            INSERT INTO findings(global_id, inspection_id, found_at, description, defect_type, severity, location_text, x, y, z, station, offset, recommended_action, status, due_date, created_by, created_at, updated_at, note)
            VALUES($g, $i, $fa, $d, $dt, $sv, $lt, $x, $y, $z, $st, $of, $ra, $s, $due, $by, $now, $now, $n)
            """, ("$g", f.GlobalId), ("$i", f.InspectionId), ("$fa", f.FoundAt.Length > 0 ? NormDate(f.FoundAt) : DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("$d", f.Description), ("$dt", Db(f.DefectType)), ("$sv", f.Severity), ("$lt", Db(f.LocationText)), ("$x", f.X), ("$y", f.Y), ("$z", f.Z), ("$st", f.Station), ("$of", f.Offset),
            ("$ra", Db(f.RecommendedAction)), ("$s", f.Status), ("$due", Db(f.DueDate)), ("$by", Who(by)), ("$now", Now()), ("$n", Db(f.Note)));
        return GetFinding(id)!;
    }

    public Finding? GetFinding(long id) => Query($"SELECT {FindCols} FROM findings WHERE id=$i", ReadFinding, ("$i", id)).FirstOrDefault();

    /// <summary>Updates a finding's follow-up status (and note / action / due date when given). Closing or repairing sets closed_at.</summary>
    public void UpdateFinding(long id, string? status = null, string? note = null, string? action = null, string? due = null, string? by = null)
    {
        if (status != null && !FindingStatuses.Contains(status)) throw new ArgumentException($"Status must be one of {string.Join(", ", FindingStatuses)}.");
        if (GetFinding(id) == null) throw new ArgumentException($"No finding {id}.");
        ModelDatabase.Exec(_cn, """
            UPDATE findings SET status=COALESCE($s, status), note=COALESCE($n, note), recommended_action=COALESCE($a, recommended_action), due_date=COALESCE($d, due_date),
              closed_at=CASE WHEN $s IN ('repaired','closed') THEN $now WHEN $s IS NOT NULL THEN NULL ELSE closed_at END, updated_at=$now, updated_by=$by WHERE id=$i
            """, ("$s", status), ("$n", note), ("$a", action), ("$d", due), ("$now", Now()), ("$by", Who(by)), ("$i", id));
    }

    /// <summary>Findings, newest first; of one element (and its descendants when <paramref name="withDescendants"/>), open ones only on request.</summary>
    public List<Finding> Findings(string? globalId = null, bool openOnly = false, bool withDescendants = false)
    {
        var all = Query($"SELECT {FindCols} FROM findings WHERE ($o = 0 OR status NOT IN ('repaired','closed')) ORDER BY found_at DESC, id DESC", ReadFinding, ("$o", openOnly ? 1 : 0));
        if (globalId == null) return all;
        var ids = withDescendants ? new HashSet<string>(Descendants(globalId)) { globalId } : new HashSet<string> { globalId };
        return all.Where(f => ids.Contains(f.GlobalId)).ToList();
    }

    // ------------------------------------------------------------------ calcs

    private const string CalcCols = "id,file_id,title,revision,calc_date,engineer,page_count,summary_pages,summary_text,added_at,added_by,note";
    private static CalcPackage ReadCalc(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0), FileId = r.GetInt64(1), Title = S(r, 2), Revision = S(r, 3), CalcDate = S(r, 4), Engineer = S(r, 5), PageCount = I(r, 6),
        SummaryPages = S(r, 7), SummaryText = S(r, 8), AddedAt = S(r, 9), AddedBy = S(r, 10), Note = S(r, 11),
    };

    /// <summary>Adds a calc package: the PDF is copied to calcs/, the summary pages' text is read (PyMuPDF; empty without python), a thumbnail
    /// of the first summary page is made. Elements (GlobalIds) are linked to the calc PDF.</summary>
    public CalcPackage AddCalc(string pdf, string summaryPages, string? title = null, string? revision = null, string? calcDate = null, string? engineer = null,
        IEnumerable<string>? elements = null, string? by = null, Action<string>? log = null)
    {
        var pages = AssetStore.ParsePages(summaryPages);
        var f = AddFile("calc", pdf, new AddFileOptions { By = by, TakenAt = calcDate, ThumbnailPage = pages[0], Log = log, Caption = title });
        var loc = ResolveAsset(f);
        var txt = AssetStore.PdfText(loc.LocalPath!, pages, log);
        if (txt is { } t && pages.Any(p => p > t.pages)) throw new ArgumentException($"Summary pages {summaryPages}: the PDF has {t.pages} pages.");
        long id = Insert("""
            INSERT INTO calcs(file_id, title, revision, calc_date, engineer, page_count, summary_pages, summary_text, added_at, added_by)
            VALUES($f, $t, $r, $d, $e, $pc, $sp, $st, $at, $by)
            """, ("$f", f.Id), ("$t", string.IsNullOrWhiteSpace(title) ? System.IO.Path.GetFileNameWithoutExtension(pdf) : title), ("$r", Db(revision)),
            ("$d", calcDate != null ? NormDate(calcDate) : null), ("$e", Db(engineer)), ("$pc", txt?.pages), ("$sp", string.Join(",", pages)), ("$st", txt?.text), ("$at", Now()), ("$by", Who(by)));
        Link(f.Id, "calc", id.ToString(CultureInfo.InvariantCulture));
        foreach (var g in elements ?? Enumerable.Empty<string>()) Link(f.Id, "element", g);
        return GetCalc(id)!;
    }

    public List<CalcPackage> Calcs() => Query($"SELECT {CalcCols} FROM calcs ORDER BY id", ReadCalc);
    public CalcPackage? GetCalc(long id) => Query($"SELECT {CalcCols} FROM calcs WHERE id=$i", ReadCalc, ("$i", id)).FirstOrDefault();

    /// <summary>Calc packages linked to an element (its calc PDF is linked to the element) or having values on it or one of its ancestors.</summary>
    public List<CalcPackage> CalcsForElement(string globalId)
    {
        var chain = Chain(globalId).Select(e => e.GlobalId).ToList();
        if (chain.Count == 0) chain.Add(globalId);
        var marks = Chain(globalId).Select(e => e.Mark).Where(m => m.Length > 0).ToList();
        var ids = new HashSet<long>();
        foreach (var g in chain)
        {
            foreach (var id in Query("SELECT CAST(l2.target AS INTEGER) FROM asset_links l JOIN asset_links l2 ON l2.file_id=l.file_id AND l2.target_kind='calc' WHERE l.target_kind='element' AND l.target=$g", r => r.GetInt64(0), ("$g", g))) ids.Add(id);
            foreach (var id in Query("SELECT DISTINCT calc_id FROM calc_values WHERE global_id=$g", r => r.GetInt64(0), ("$g", g))) ids.Add(id);
        }
        foreach (var m in marks)
            foreach (var id in Query("SELECT DISTINCT calc_id FROM calc_values WHERE mark=$m COLLATE NOCASE", r => r.GetInt64(0), ("$m", m))) ids.Add(id);
        return Calcs().Where(c => ids.Contains(c.Id)).ToList();
    }

    private const string ValCols = "id,calc_id,key,name,global_id,mark,property_path,result_path,bridge,field,value_text,value,unit,tolerance,page,quote,extracted_by,extracted_at";
    private static CalcValue ReadVal(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0), CalcId = r.GetInt64(1), Key = S(r, 2), Name = S(r, 3), GlobalId = S(r, 4), Mark = S(r, 5), PropertyPath = S(r, 6), ResultPath = S(r, 7),
        Bridge = S(r, 8), Field = S(r, 9), ValueText = S(r, 10), Value = D(r, 11), Unit = S(r, 12), Tolerance = S(r, 13), Page = I(r, 14), Quote = S(r, 15),
        ExtractedBy = S(r, 16), ExtractedAt = S(r, 17),
    };

    /// <summary>Replaces the extracted values of a calc (the applied-change log is kept: it carries its own copy of path, old / new value, page, quote).</summary>
    public int ReplaceCalcValues(long calcId, IEnumerable<CalcValue> values)
    {
        if (GetCalc(calcId) == null) throw new ArgumentException($"No calc {calcId}.");
        ModelDatabase.Exec(_cn, "DELETE FROM calc_values WHERE calc_id=$c", ("$c", calcId));
        int n = 0;
        foreach (var v in values)
        {
            v.CalcId = calcId;
            v.Id = Insert("""
                INSERT INTO calc_values(calc_id, key, name, global_id, mark, property_path, result_path, bridge, field, value_text, value, unit, tolerance, page, quote, extracted_by, extracted_at)
                VALUES($c, $k, $n, $g, $m, $pp, $rp, $b, $f, $vt, $v, $u, $t, $p, $q, $eb, $ea)
                """, ("$c", calcId), ("$k", Db(v.Key)), ("$n", Db(v.Name)), ("$g", v.GlobalId), ("$m", Db(v.Mark)), ("$pp", Db(v.PropertyPath)), ("$rp", Db(v.ResultPath)),
                ("$b", Db(v.Bridge)), ("$f", Db(v.Field)), ("$vt", v.ValueText), ("$v", v.Value), ("$u", Db(v.Unit)), ("$t", Db(v.Tolerance)), ("$p", v.Page), ("$q", Db(v.Quote)),
                ("$eb", Db(v.ExtractedBy)), ("$ea", v.ExtractedAt.Length > 0 ? v.ExtractedAt : Now()));
            n++;
        }
        return n;
    }

    public List<CalcValue> CalcValues(long? calcId = null) =>
        Query($"SELECT {ValCols} FROM calc_values WHERE ($c IS NULL OR calc_id=$c) ORDER BY calc_id, id", ReadVal, ("$c", calcId));

    /// <summary>Records one value applied to the model (who, when, old → new, calc + page).</summary>
    public long RecordApplied(CalcValue v, string calcTitle, string oldValue, string newValue, string? modelFile, string? by = null) =>
        Insert("""
            INSERT INTO calc_applied(calc_id, value_id, calc_title, property_path, old_value, new_value, page, quote, applied_by, applied_at, model_file)
            VALUES($c, $v, $t, $p, $o, $n, $pg, $q, $by, $at, $m)
            """, ("$c", v.CalcId), ("$v", v.Id), ("$t", calcTitle), ("$p", v.PropertyPath), ("$o", oldValue), ("$n", newValue), ("$pg", v.Page), ("$q", Db(v.Quote)),
            ("$by", Who(by)), ("$at", Now()), ("$m", Db(modelFile)));

    public List<AppliedChange> AppliedLog(long? calcId = null) =>
        Query("SELECT id,calc_id,value_id,calc_title,property_path,old_value,new_value,page,quote,applied_by,applied_at,model_file FROM calc_applied WHERE ($c IS NULL OR calc_id=$c) ORDER BY id DESC",
            r => new AppliedChange
            {
                Id = r.GetInt64(0), CalcId = r.GetInt64(1), ValueId = L(r, 2), CalcTitle = S(r, 3), PropertyPath = S(r, 4), OldValue = S(r, 5), NewValue = S(r, 6),
                Page = I(r, 7), Quote = S(r, 8), AppliedBy = S(r, 9), AppliedAt = S(r, 10), ModelFile = S(r, 11),
            }, ("$c", calcId));

    /// <summary>A database column or a key of the element's properties JSON, for calc values that target an element result (mass_kg, length_m,
    /// volume_m3, material, or e.g. "section"). Null when the element or the field does not exist.</summary>
    public object? ElementField(string globalId, string field)
    {
        var e = Get(globalId);
        if (e == null) return null;
        switch (field.ToLowerInvariant())
        {
            case "mass_kg": return e.MassKg;
            case "volume_m3": return e.VolumeM3;
            case "length_m": return e.LengthM;
            case "material": return e.Material;
            case "name": return e.Name;
            case "mark": return e.Mark;
            case "subtype": return e.Subtype;
        }
        if (e.Properties.Length == 0) return null;
        using var j = System.Text.Json.JsonDocument.Parse(e.Properties);
        if (!j.RootElement.TryGetProperty(field, out var p)) return null;
        return p.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number => p.GetDouble(),
            System.Text.Json.JsonValueKind.String => p.GetString(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            _ => p.GetRawText(),
        };
    }

    /// <summary>The element of a mark (exact, case-insensitive) when exactly one has it, else null.</summary>
    public string? ElementByMark(string mark)
    {
        var ids = Query("SELECT global_id FROM elements WHERE mark = $t COLLATE NOCASE LIMIT 2", r => r.GetString(0), ("$t", mark));
        return ids.Count == 1 ? ids[0] : null;
    }
}
