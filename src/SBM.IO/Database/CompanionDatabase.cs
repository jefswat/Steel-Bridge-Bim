using System.Diagnostics;
using SBM.Core.Project;
using SBM.IO.Ifc;

namespace SBM.IO.Database;

public sealed class DatabaseOptions
{
    /// <summary>Plan-sheet link rules (JSON). Null = <see cref="CompanionDatabase.FindLinksFile"/> (the <c>sheet-links*.json</c> beside the model that names it).</summary>
    public string? LinksFile { get; set; }
    /// <summary>Split the plan PDFs into single pages and (re)fill the sheets table. Without it, sheets already in the database are kept.</summary>
    public bool Pages { get; set; }
    public bool Previews { get; set; } = true;
    /// <summary>VACUUM after writing (smaller file; takes a few seconds and needs temporary disk space about the size of the database).</summary>
    public bool Vacuum { get; set; }
    /// <summary>Folders searched for the plan PDFs first (see <see cref="CompanionDatabase.PlanSearchFolders"/> for the others).</summary>
    public List<string> PlanFolders { get; } = new();
    /// <summary>Write the unused-sheet report (markdown) here.</summary>
    public string? UnusedReport { get; set; }
    public string? ModelFile { get; set; }
    /// <summary>CHECKING database for an independent checker: the element registry only (GlobalId, parent, level, category, mark, bridge / unit /
    /// support / girder, measured mass / volume / length / bbox) WITHOUT sheet links, plan sheets, QC, property text (descriptions, status reasons,
    /// bases) or the model path; elements the originator flagged carry <c>{"flagged":true}</c> and nothing else. See docs/API.md.</summary>
    public bool ForCheck { get; set; }
    public Action<string>? Log { get; set; }
}

public sealed class CompanionResult
{
    public DatabaseBuildResult Build { get; set; } = new();
    public LinkResult? Links { get; set; }
    public int Sheets { get; set; }
    public string Coverage { get; set; } = "";
    /// <summary>The link rules file used (null = none found).</summary>
    public string? LinksFile { get; set; }
    public TimeSpan Elapsed { get; set; }
}

/// <summary>Builds the companion SQLite database of a model: element registry, plan sheets and sheet links (see <see cref="ModelDatabase"/>).</summary>
public static class CompanionDatabase
{
    public static string DefaultPath(string modelFile) => Path.ChangeExtension(modelFile, ".sqlite");

    /// <summary>
    /// The link rules file of a model: a <c>sheet-links*.json</c> beside the model (or in its <c>plans</c> subfolder) whose <c>models</c>
    /// list names the model; else one without a <c>models</c> list. Null when there is none.
    /// </summary>
    public static string? FindLinksFile(string? modelFile)
    {
        string? dir = modelFile == null ? null : Path.GetDirectoryName(Path.GetFullPath(modelFile));
        if (dir == null || !Directory.Exists(dir)) return null;
        var files = new[] { dir, Path.Combine(dir, "plans") }.Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "sheet-links*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)).ToList();
        string? generic = null;
        foreach (var f in files)
        {
            LinkRules r;
            try { r = LinkRules.Load(f); } catch (Exception) { continue; }
            if (r.Models.Count == 0) generic ??= f;
            else if (r.IsFor(modelFile!)) return f;
        }
        return generic;
    }

    /// <summary>Folders searched for the plan PDFs: the options' folders, the rules file's <c>planFolders</c>, the database / model folder and
    /// their <c>plans</c> subfolders, the folders in <c>%SBM_PLANS%</c> (;-separated) and the program folder.</summary>
    public static List<string> PlanSearchFolders(DatabaseOptions options, LinkRules? rules, string dbDir)
    {
        var list = new List<string>(options.PlanFolders);
        if (rules != null) list.AddRange(rules.ResolvedPlanFolders());
        list.Add(dbDir); list.Add(Path.Combine(dbDir, "plans"));
        if (options.ModelFile != null && Path.GetDirectoryName(Path.GetFullPath(options.ModelFile)) is { } md) { list.Add(md); list.Add(Path.Combine(md, "plans")); }
        if (Environment.GetEnvironmentVariable("SBM_PLANS") is { Length: > 0 } env) list.AddRange(env.Split(';', StringSplitOptions.RemoveEmptyEntries));
        list.Add(AppContext.BaseDirectory);
        return list.Select(f => { try { return Path.GetFullPath(f); } catch (Exception) { return f; } }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string? FindScript(string name = "plan_split.py")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            {
                string f = Path.Combine(d.FullName, "tools", name);
                if (File.Exists(f)) return f;
            }
        return null;
    }

    public static CompanionResult Build(string dbPath, BridgeProject project, IReadOnlyList<IfcBridgeData> bridges, DatabaseOptions? options = null)
    {
        options ??= new DatabaseOptions();
        var sw = Stopwatch.StartNew();
        var res = new CompanionResult();
        string dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath))!;
        Directory.CreateDirectory(dbDir);
        res.Build = ModelDatabase.Write(dbPath, project, bridges, options.ForCheck ? null : options.ModelFile, options.Vacuum && !options.ForCheck);
        using (var cn = ModelDatabase.Open(dbPath))
        {
            if (options.ForCheck) StripForCheck(cn, options.Vacuum);
            else ApplySheets(cn, project.Name, options, res, dbDir);
        }
        res.Elapsed = sw.Elapsed;
        return res;
    }

    /// <summary>Turns a freshly written database into the checking database (see <see cref="DatabaseOptions.ForCheck"/>): no sheets, links, QC,
    /// property text or model path; names / marks / category strings that cite plans or state sizes are removed; flagged elements keep only a flag.</summary>
    public static void StripForCheck(Microsoft.Data.Sqlite.SqliteConnection cn, bool vacuum = false)
    {
        ModelDatabase.Exec(cn, "DELETE FROM element_sheet; DELETE FROM qc; DELETE FROM sheets;");
        // Property JSON (descriptions, reasons, bases, plan-derived values) is dropped; a status marks the element as flagged, nothing more.
        const string flagJson = "{\"flagged\":true}";
        ModelDatabase.Exec(cn, "INSERT INTO props(id, json) VALUES((SELECT COALESCE(MAX(id),0)+1 FROM props), $j) ON CONFLICT(json) DO NOTHING", ("$j", flagJson));
        long flagId = ModelDatabase.Scalar<long>(cn, "SELECT id FROM props WHERE json = $j", ("$j", flagJson));
        ModelDatabase.Exec(cn, "UPDATE el SET props_id = $f WHERE props_id IN (SELECT id FROM props WHERE json_extract(json, '$.status') IS NOT NULL)", ("$f", flagId));
        ModelDatabase.Exec(cn, "UPDATE el SET props_id = NULL WHERE props_id IS NOT NULL AND props_id <> $f", ("$f", flagId));
        ModelDatabase.Exec(cn, "DELETE FROM props WHERE id <> $f", ("$f", flagId));
        // Text that cites the plans or states a size / judgement: names and marks to NULL (the view falls back to category + mark), strings dropped.
        var leakNames = new List<(long id, bool name, bool mark)>();
        using (var c = cn.CreateCommand())
        {
            c.CommandText = "SELECT id, name, mark FROM el WHERE name IS NOT NULL OR mark IS NOT NULL";
            using var r = c.ExecuteReader();
            while (r.Read())
            {
                bool n = !r.IsDBNull(1) && IfcCheckFilter.IsLeak(r.GetString(1)), m = !r.IsDBNull(2) && IfcCheckFilter.IsLeak(r.GetString(2));
                if (n || m) leakNames.Add((r.GetInt64(0), n, m));
            }
        }
        using (var tx = cn.BeginTransaction())
        {
            foreach (var (id, n, m) in leakNames)
                ModelDatabase.Exec(cn, $"UPDATE el SET {(n ? "name = NULL" : "")}{(n && m ? ", " : "")}{(m ? "mark = NULL" : "")} WHERE id = $i", ("$i", id));
            tx.Commit();
        }
        var leakStrings = new List<long>();
        using (var c = cn.CreateCommand())
        {
            c.CommandText = "SELECT id, value FROM strings";
            using var r = c.ExecuteReader();
            while (r.Read()) if (IfcCheckFilter.IsLeak(r.GetString(1))) leakStrings.Add(r.GetInt64(0));
        }
        foreach (long sid in leakStrings)
        {
            foreach (string col in new[] { "level_s", "category_s", "subtype_s", "ifc_class_s", "bridge_group_s", "bridge_s", "unit_s", "support_s", "girder_s", "material_s" })
                ModelDatabase.Exec(cn, $"UPDATE el SET {col} = NULL WHERE {col} = $s", ("$s", sid));
            ModelDatabase.Exec(cn, "DELETE FROM strings WHERE id = $s", ("$s", sid));
        }
        ModelDatabase.Exec(cn, "DELETE FROM meta WHERE key IN ('model_file','links_file','links_applied_at')");
        ModelDatabase.Exec(cn, "INSERT OR REPLACE INTO meta VALUES('for_check', '1')");
        ModelDatabase.Exec(cn, "INSERT OR REPLACE INTO meta VALUES('for_check_note', 'Checking database: element registry only. Flagged elements carry properties = {\"flagged\":true}.')");
        ModelDatabase.Exec(cn, "DROP VIEW IF EXISTS v_unresolved");
        ModelDatabase.Exec(cn, "DROP VIEW IF EXISTS v_flagged");
        ModelDatabase.Exec(cn, "CREATE VIEW v_flagged AS SELECT global_id, level, category, name, mark, bridge, unit, support, girder FROM elements WHERE json_extract(properties, '$.flagged') = 1");
        if (vacuum) ModelDatabase.Exec(cn, "VACUUM");
    }

    /// <summary>
    /// Re-applies the plan sheets and link rules to an existing database without rewriting the element registry (seconds instead of a full
    /// build): used by the app when a database has no links yet or the rules file changed after the links were made.
    /// </summary>
    public static CompanionResult Relink(string dbPath, string projectName, DatabaseOptions? options = null)
    {
        options ??= new DatabaseOptions();
        var sw = Stopwatch.StartNew();
        var res = new CompanionResult { Build = new DatabaseBuildResult { Path = dbPath } };
        string dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath))!;
        using (var cn = ModelDatabase.Open(dbPath))
            ApplySheets(cn, projectName, options, res, dbDir);
        res.Elapsed = sw.Elapsed;
        return res;
    }

    private static void ApplySheets(Microsoft.Data.Sqlite.SqliteConnection cn, string projectName, DatabaseOptions options, CompanionResult res, string dbDir)
    {
        string? linksFile = options.LinksFile ?? FindLinksFile(options.ModelFile);
        res.LinksFile = linksFile;
        LinkRules? rules = linksFile != null && File.Exists(linksFile) ? LinkRules.Load(linksFile) : null;
        if (rules == null)
            options.Log?.Invoke(linksFile != null ? $"link rules file '{linksFile}' not found: no plan sheet links."
                : "no sheet-links*.json for this model beside it (or in its plans folder): no plan sheet links. See docs/API.md, companion database.");
        if (options.Pages && rules == null) throw new InvalidOperationException("--pages needs a link rules file (plan sets): pass --links <file.json>.");
        if (rules != null)
        {
            var folders = PlanSearchFolders(options, rules, dbDir);
            string? script = options.Pages ? FindScript() ?? throw new FileNotFoundException("tools/plan_split.py not found.") : null;
            foreach (var set in rules.PlanSets)
            {
                string? pdf = PlanSheets.FindPdf(set, folders);
                if (pdf == null) options.Log?.Invoke($"plan set {set.Id}: PDF '{set.Pdf}' not found in {string.Join("; ", folders.Where(Directory.Exists))}"
                    + (set.Sheets != null ? " (sheets and links come from the sheet index; Open PDF needs the PDF)" : ""));
                if (options.Pages && pdf != null)
                {
                    string outDir = Path.Combine(dbDir, "plans", set.Id);
                    options.Log?.Invoke($"splitting {Path.GetFileName(pdf)} -> {outDir}");
                    PlanSheets.SplitPdf(pdf, outDir, script!, options.Previews, options.Log);
                    PlanSheets.ImportSheets(cn, set.Id, Path.Combine(outDir, "sheets.json"), "plans/" + set.Id, pdf);
                }
                if (set.Sheets != null) PlanSheets.ImportIndex(cn, set, pdf);
                else if (!options.Pages && ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM sheets WHERE plan_set=$s", ("$s", set.Id)) == 0)
                    options.Log?.Invoke($"plan set {set.Id} has no sheet index in {Path.GetFileName(linksFile)} and no imported pages: add \"sheets\" to the plan set or run with --pages.");
            }
        }
        res.Sheets = ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM sheets");
        if (rules != null && res.Sheets > 0)
        {
            res.Links = PlanSheets.ApplyRules(cn, rules, Path.GetFileName(linksFile!));
            res.Coverage = PlanSheets.CoverageReport(cn);
            ModelDatabase.Exec(cn, "INSERT OR REPLACE INTO meta VALUES('links_file', @v)", ("@v", Path.GetFullPath(linksFile!)));
            ModelDatabase.Exec(cn, "INSERT OR REPLACE INTO meta VALUES('links_applied_at', @v)", ("@v", DateTime.UtcNow.ToString("o")));
            if (options.UnusedReport != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.UnusedReport))!);
                File.WriteAllText(options.UnusedReport, PlanSheets.UnusedReport(cn, rules, projectName));
            }
        }
    }
}
