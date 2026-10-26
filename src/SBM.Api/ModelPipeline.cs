using System.Diagnostics;
using System.Text.Json;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.References;
using SBM.IO.Database;

namespace SBM.Api;

public enum PipelineStepStatus { Done, UpToDate, Skipped, Unresolved, Failed }

/// <summary>One step of the model pipeline: what happened (or why not) and, when something is open, the next action.</summary>
public sealed record PipelineStep(string Name, PipelineStepStatus Status, string Message, string? Next = null);

public sealed class PipelineOptions
{
    public bool Regenerate { get; set; } = true;
    public bool Database { get; set; } = true;
    public bool SheetIndex { get; set; } = true;
    public bool Terrain { get; set; } = true;
    /// <summary>No downloads (terrain is reported as skipped). Also <c>SBM_OFFLINE=1</c>.</summary>
    public bool Offline { get; set; } = Environment.GetEnvironmentVariable("SBM_OFFLINE") is "1" or "true";
    /// <summary>Also build the automatic index for plan sets that HAVE a curated index (only pages the curated index lacks are used).</summary>
    public bool Reindex { get; set; }
    /// <summary>Rebuild the database even when it is newer than the model and the rules.</summary>
    public bool ForceDatabase { get; set; }
    /// <summary>Plan-set PDFs (in addition to <see cref="BridgeProject.PlanPdfs"/>), used when no rules file names the model.</summary>
    public List<string> Pdfs { get; } = new();
    public List<string> PlanFolders { get; } = new();
    /// <summary>Terrain output / tile cache folder. Null = <c>%SBM_TERRAIN%</c>, else <c>C:\SBMData\terrain</c> when C:\SBMData exists, else <c>&lt;model folder&gt;\terrain</c>.</summary>
    public string? TerrainFolder { get; set; }
    /// <summary>Ground margin around the bridges (m).</summary>
    public double TerrainMargin { get; set; } = 300;
    public string Python { get; set; } = Environment.GetEnvironmentVariable("SBM_PYTHON") is { Length: > 0 } p ? p : "python";
    public Action<string>? Log { get; set; }
}

public sealed class PipelineReport
{
    public List<PipelineStep> Steps { get; } = new();
    public bool Failed => Steps.Any(s => s.Status == PipelineStepStatus.Failed);
    public int Open => Steps.Count(s => s.Status is PipelineStepStatus.Unresolved or PipelineStepStatus.Failed);
    public PipelineStep Add(string name, PipelineStepStatus st, string msg, string? next = null) { var s = new PipelineStep(name, st, msg, next); Steps.Add(s); return s; }

    private static string Tag(PipelineStepStatus s) => s switch
    {
        PipelineStepStatus.Done => "DONE      ", PipelineStepStatus.UpToDate => "UP TO DATE", PipelineStepStatus.Skipped => "SKIPPED   ",
        PipelineStepStatus.Unresolved => "UNRESOLVED", _ => "FAILED    ",
    };

    public string ToText()
    {
        var sb = new System.Text.StringBuilder();
        var shown = new HashSet<string>();
        foreach (var s in Steps)
        {
            sb.AppendLine($"  [{Tag(s.Status)}] {s.Name}: {s.Message}");
            if (s.Next != null) sb.AppendLine("               next: " + (shown.Add(s.Next) ? s.Next : "(as above)"));
        }
        sb.Append(Open == 0 ? "  Pipeline complete: nothing open." : $"  {Open} item(s) open (UNRESOLVED / FAILED above): they are required for a finished model.");
        return sb.ToString();
    }
}

/// <summary>CRS names the automatic terrain step can project (mirror of <c>tools/terrain/crs.py</c> SUPPORTED; test <c>ModelPipelineTests</c>).</summary>
public static class TerrainCrs
{
    private static readonly HashSet<string> StatePlane = new(StringComparer.OrdinalIgnoreCase)
    {
        "EPSG:2276", "EPSG:32138", "EPSG:2264", "EPSG:32119", "EPSG:2272", "EPSG:32129", "EPSG:2271", "EPSG:32128", "EPSG:2267", "EPSG:32124",
        "EPSG:3417", "EPSG:26975", "EPSG:3418", "EPSG:26976", "EPSG:32104", "ESRI:103708",
    };

    public static IEnumerable<string> All => StatePlane.Concat(Enumerable.Range(1, 23).Select(z => $"EPSG:{26900 + z}")).Concat(Enumerable.Range(1, 60).Select(z => $"EPSG:{32600 + z}"));

    public static bool IsSupported(string? crs)
    {
        string n = (crs ?? "").Trim();
        if (StatePlane.Contains(n)) return true;
        if (!n.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase) || !int.TryParse(n[5..], out int code)) return false;
        return code is >= 26901 and <= 26923 or >= 32601 and <= 32660;
    }
}

public enum TerrainAction { Keep, Build, Unresolved, Skip }

public sealed record TerrainDecision(TerrainAction Action, string Reason, string? Next = null);

/// <summary>
/// The DEFAULT finishing pipeline of a model (CLI <c>sbm pipeline</c>, run automatically after <c>sbm run</c> saves a model): regenerate ->
/// georeference check -> terrain (3DEP DEM for a georeferenced project, cached) -> plan sheet index (automatic index / skeleton link rules from the
/// plan PDFs; curated rules stay authoritative) -> companion database with sheet links. Every step reports DONE / UP TO DATE / SKIPPED (why) /
/// UNRESOLVED (what is needed) / FAILED - nothing is silent. docs/MODEL-PIPELINE.md.
/// </summary>
public static class ModelPipeline
{
    public const string AutoTerrainName = "3DEP DEM (auto, sbm pipeline)";
    public const string GeoreferenceNext =
        "set Doc.Project.Georeference (CrsName \"EPSG:xxxx\", GridScale, ScaleCenterX/Y when localized, Basis = plan page) from the plan's coordinate data "
        + "(control monument / PI northing-easting / situation-plan lat-long): see samples/plans/terrain-br02069.csx (inferred CRS, flagged) and "
        + "build-ptc-delaware-river.csx (fitted); a model on a LOCAL grid must first be moved into the map CRS (samples/plans/geolocate-cbis.csx: "
        + "one GPS / plan point + the north arrow). Then rerun `sbm pipeline <model.sbm>`. Terrain from another source (survey TIN, LandXML, DEM) = "
        + "add a ReferenceKind.Terrain reference instead.";

    // ------------------------------------------------------------------ decisions (pure)

    /// <summary>What the terrain step will do for this project (no side effects).</summary>
    public static TerrainDecision DecideTerrain(BridgeProject p, string? modelFile, PipelineOptions o, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        if (!o.Terrain) return new(TerrainAction.Skip, "disabled (--no-terrain)");
        var refs = p.References.Where(r => r.Kind == ReferenceKind.Terrain).ToList();
        var present = refs.Where(r => r.Visible && exists(BridgeProject.ResolvePath(r.Path, modelFile))).ToList();
        if (present.Count > 0)
            return new(TerrainAction.Keep, $"terrain reference(s) present: {string.Join("; ", present.Select(r => r.Name))}");
        string missing = refs.Count == 0 ? "no terrain reference" : $"terrain reference file(s) missing: {string.Join("; ", refs.Select(r => r.Path))}";
        if (p.Georeference == null)
            return new(TerrainAction.Unresolved, $"{missing} and the project has NO georeference (project X / Y = a local grid or an unnamed system), so no ground can be fetched", GeoreferenceNext);
        if (!TerrainCrs.IsSupported(p.Georeference.CrsName))
            return new(TerrainAction.Unresolved, $"{missing}; georeference CRS '{p.Georeference.CrsName}' is not in tools/terrain/crs.py",
                "add the CRS projection parameters to tools/terrain/crs.py (+ TerrainCrs in ModelPipeline.cs), or attach a terrain reference by hand");
        if (o.Offline) return new(TerrainAction.Skip, $"{missing}; offline (--offline / SBM_OFFLINE=1): 3DEP not downloaded", "rerun `sbm pipeline <model.sbm>` with network");
        if (modelFile == null) return new(TerrainAction.Skip, $"{missing}; the model is not saved (no folder for the terrain file)", "save the model, then rerun");
        return new(TerrainAction.Build, $"{missing}: fetching USGS 3DEP for {p.Georeference.CrsName}");
    }

    /// <summary>The terrain output / tile cache folder (see <see cref="PipelineOptions.TerrainFolder"/>).</summary>
    public static string TerrainFolder(PipelineOptions o, string modelFile, Func<string, bool>? writable = null)
    {
        writable ??= Writable;
        if (o.TerrainFolder is { Length: > 0 } f) return f;
        if (Environment.GetEnvironmentVariable("SBM_TERRAIN") is { Length: > 0 } env) return env;
        if (writable(@"C:\SBMData\terrain")) return @"C:\SBMData\terrain";
        // A model in the repository: <repo>\dist\terrain (git-ignored, where the hand-made terrains live).
        for (var d = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(modelFile))!); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SteelBridgeModeler.sln")))
            {
                string rd = Path.Combine(d.FullName, "dist", "terrain");
                if (writable(rd)) return rd;
                break;
            }
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(modelFile))!, "terrain");
    }

    /// <summary>True when the folder exists (or can be created) and a file can be written in it.</summary>
    public static bool Writable(string dir)
    {
        try
        {
            if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(dir)) ?? dir) && !Directory.Exists(dir)) return false;
            Directory.CreateDirectory(dir);
            string probe = Path.Combine(dir, ".sbm-write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>True when the database is newer than the model file and every input of its sheet links (rules file, automatic index files).</summary>
    public static bool DatabaseUpToDate(string dbPath, string modelFile, IEnumerable<string> inputs, Func<string, DateTime?>? mtime = null)
    {
        mtime ??= f => File.Exists(f) ? File.GetLastWriteTimeUtc(f) : null;
        if (mtime(dbPath) is not { } db) return false;
        if (mtime(modelFile) is { } m && m > db) return false;
        return inputs.All(f => mtime(f) is not { } t || t <= db);
    }

    /// <summary>Plan-set PDFs known for the project: the options' PDFs + <see cref="BridgeProject.PlanPdfs"/> (relative to the model), existing files only.</summary>
    public static List<string> KnownPdfs(BridgeProject p, string? modelFile, PipelineOptions o) =>
        o.Pdfs.Concat(p.PlanPdfs).Select(f => BridgeProject.ResolvePath(f, modelFile)).Select(f => Path.IsPathRooted(f) ? f : Path.GetFullPath(f))
            .Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Plan extents (project metres) of the bridges: alignment points between the first and last support (every 10 m), else all alignments.</summary>
    public static (double x0, double y0, double x1, double y1)? Extents(BridgeProject p)
    {
        var pts = new List<Vec2>();
        foreach (var b in p.Bridges)
        {
            var al = p.FindAlignment(b.AlignmentName);
            var st = b.Supports.Concat(b.AuxiliarySupports).Select(s => s.Station).Where(double.IsFinite).ToList();
            if (al == null || st.Count == 0) continue;
            double s0 = Math.Max(st.Min(), al.StartStation), s1 = Math.Min(st.Max(), al.EndStation);
            for (double s = s0; s <= s1 + 1e-6; s += Math.Max(10, (s1 - s0) / 500)) pts.Add(al.PointAt(s));
            pts.Add(al.PointAt(s1));
        }
        if (pts.Count == 0)
            foreach (var al in p.Alignments.Where(a => a.Length > 0))
                for (double s = al.StartStation; s <= al.EndStation + 1e-6; s += Math.Max(10, al.Length / 500)) pts.Add(al.PointAt(s));
        if (pts.Count == 0) return null;
        return (pts.Min(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.X), pts.Max(q => q.Y));
    }

    // ------------------------------------------------------------------ run

    /// <summary>What the pipeline would do / what is missing, without changing anything (no regeneration, downloads or writes).</summary>
    public static PipelineReport Status(SbmDocument doc, PipelineOptions? o = null)
    {
        o ??= new PipelineOptions();
        var rep = new PipelineReport();
        string? model = doc.FilePath;
        GeoreferenceStep(doc.Project, rep);
        var td = DecideTerrain(doc.Project, model, o);
        rep.Add("terrain", td.Action switch { TerrainAction.Keep => PipelineStepStatus.UpToDate, TerrainAction.Unresolved => PipelineStepStatus.Unresolved, TerrainAction.Skip => PipelineStepStatus.Skipped, _ => PipelineStepStatus.Unresolved },
            td.Action == TerrainAction.Build ? "no terrain yet (the pipeline will fetch 3DEP)" : td.Reason, td.Action == TerrainAction.Build ? "run `sbm pipeline <model.sbm>`" : td.Next);
        if (model == null) { rep.Add("database", PipelineStepStatus.Skipped, "model not saved"); return rep; }
        string? links = CompanionDatabase.FindLinksFile(model, doc.Project.Bridges.Select(b => b.Name));
        if (links == null)
            rep.Add("sheet index", PipelineStepStatus.Unresolved, "no sheet-links*.json names this model (no plan sheet index, no element -> sheet links)",
                KnownPdfs(doc.Project, model, o).Count > 0 ? "run `sbm pipeline <model.sbm>` (writes the skeleton + automatic index)" : "run `sbm pipeline <model.sbm> --pdf <plan set.pdf>` (or set Doc.Project.PlanPdfs)");
        else
        {
            var rules = LinkRules.Load(links);
            string dir = Path.GetDirectoryName(links)!;
            int noIndex = rules.PlanSets.Count(s => s.Sheets == null && !File.Exists(AutoSheetIndex.PathFor(dir, s.Id)));
            rep.Add("sheet index", noIndex > 0 ? PipelineStepStatus.Unresolved : PipelineStepStatus.UpToDate,
                $"{Path.GetFileName(links)}: {rules.PlanSets.Count} plan set(s), {noIndex} without an index, {rules.Rules.Count} link rule(s)",
                noIndex > 0 ? "run `sbm pipeline <model.sbm>`" : rules.Rules.Count == 0 ? "write link rules (title regex -> elements) in " + Path.GetFileName(links) : null);
        }
        string db = CompanionDatabase.DefaultPath(model);
        var inputs = LinkInputs(links);
        rep.Add("database", DatabaseUpToDate(db, model, inputs) ? PipelineStepStatus.UpToDate : PipelineStepStatus.Unresolved,
            File.Exists(db) ? (DatabaseUpToDate(db, model, inputs) ? $"{Path.GetFileName(db)} is newer than the model and its rules" : $"{Path.GetFileName(db)} is OLDER than the model / rules")
                            : $"no companion database ({Path.GetFileName(db)})",
            DatabaseUpToDate(db, model, inputs) ? null : "run `sbm pipeline <model.sbm>`");
        return rep;
    }

    private static List<string> LinkInputs(string? links)
    {
        var list = new List<string>();
        if (links == null) return list;
        list.Add(links);
        try { foreach (var s in LinkRules.Load(links).PlanSets) list.Add(AutoSheetIndex.PathFor(Path.GetDirectoryName(links)!, s.Id)); } catch (Exception) { }
        return list;
    }

    private static void GeoreferenceStep(BridgeProject p, PipelineReport rep)
    {
        if (p.Georeference is { } g)
            rep.Add("georeference", PipelineStepStatus.UpToDate, $"{g.CrsName}, grid scale {g.GridScale}" + (TerrainCrs.IsSupported(g.CrsName) ? "" : " (CRS not in the automatic terrain table)"));
        else
            rep.Add("georeference", PipelineStepStatus.Unresolved, "none: project X / Y are not tied to a map CRS (IFC map conversion 'Project grid'; no automatic terrain)", GeoreferenceNext);
    }

    /// <summary>Runs the pipeline on a SAVED model (see the class summary). Saves the model only when the terrain step added a reference.</summary>
    public static PipelineReport Run(SbmDocument doc, PipelineOptions o)
    {
        var rep = new PipelineReport();
        string? model = doc.FilePath;
        void Log(string m) => o.Log?.Invoke(m);
        if (o.Regenerate)
        {
            Log("regenerating ...");
            var r = doc.Regenerate();
            rep.Add("regenerate", r.Errors.Count > 0 ? PipelineStepStatus.Failed : PipelineStepStatus.Done,
                $"{r.Bridges} bridge(s) in {r.Elapsed.TotalSeconds:F1} s, {r.Warnings.Count} warning(s), {r.Errors.Count} error(s)" + (r.Errors.Count > 0 ? ": " + string.Join(" | ", r.Errors.Take(3)) : ""));
        }
        else rep.Add("regenerate", PipelineStepStatus.Skipped, "--no-regen");
        if (model == null)
        {
            rep.Add("pipeline", PipelineStepStatus.Skipped, "the model has no file: save it first (the database, sheet index and terrain live beside it)", "Doc.Save(path) then `sbm pipeline <model.sbm>`");
            return rep;
        }

        GeoreferenceStep(doc.Project, rep);
        if (TerrainStep(doc, o, rep, Log) == TerrainAction.Keep)
            foreach (var p in doc.TerrainProblems) rep.Add("terrain", PipelineStepStatus.Failed, p);   // a kept reference that did not load (others did)

        string? links = o.SheetIndex ? SheetIndexStep(doc, o, rep, Log) : CompanionDatabase.FindLinksFile(model, doc.Project.Bridges.Select(b => b.Name));
        if (!o.SheetIndex) rep.Add("sheet index", PipelineStepStatus.Skipped, "--no-index");

        if (!o.Database) { rep.Add("database", PipelineStepStatus.Skipped, "--no-db"); return rep; }
        string db = CompanionDatabase.DefaultPath(model);
        if (!o.ForceDatabase && DatabaseUpToDate(db, model, LinkInputs(links)))
        {
            rep.Add("database", PipelineStepStatus.UpToDate, $"{Path.GetFileName(db)} is newer than the model and its link rules (--force-db rebuilds)");
            return rep;
        }
        try
        {
            Log("building the companion database ...");
            var dbo = new DatabaseOptions { Vacuum = true, Log = m => Log("  db: " + m) };
            dbo.PlanFolders.AddRange(o.PlanFolders);
            dbo.BridgeNames.AddRange(doc.Project.Bridges.Select(b => b.Name));
            var res = doc.ExportDatabase(null, dbo);
            string linkText = res.Links is { } l ? $"{res.Sheets} sheets, {l.Links:N0} element -> sheet links, {l.SheetsLinked} sheets linked"
                : res.LinksFile == null ? "NO plan sheet links (no rules file)" : $"{res.Sheets} sheets, no links";
            var st = res.Links is { Links: > 0 } ? PipelineStepStatus.Done : PipelineStepStatus.Unresolved;
            rep.Add("database", st, $"{res.Build.Path}: {res.Build.Elements:N0} elements, {linkText} ({res.Elapsed.TotalSeconds:F1} s)",
                st == PipelineStepStatus.Done ? null : res.LinksFile == null ? "see the sheet index step" : "write link rules in " + Path.GetFileName(res.LinksFile));
        }
        catch (Exception ex) { rep.Add("database", PipelineStepStatus.Failed, ex.Message); }
        return rep;
    }

    private static TerrainAction TerrainStep(SbmDocument doc, PipelineOptions o, PipelineReport rep, Action<string> log)
    {
        string? model = doc.FilePath;
        var d = DecideTerrain(doc.Project, model, o);
        switch (d.Action)
        {
            case TerrainAction.Keep: rep.Add("terrain", PipelineStepStatus.UpToDate, d.Reason); return d.Action;
            case TerrainAction.Skip: rep.Add("terrain", PipelineStepStatus.Skipped, d.Reason, d.Next); return d.Action;
            case TerrainAction.Unresolved: rep.Add("terrain", PipelineStepStatus.Unresolved, d.Reason, d.Next); return d.Action;
        }
        var geo = doc.Project.Georeference!;
        if (Extents(doc.Project) is not { } e) { rep.Add("terrain", PipelineStepStatus.Unresolved, "the project has no alignment geometry to take the extents from"); return d.Action; }
        string? script = CompanionDatabase.FindScript(Path.Combine("terrain", "auto_terrain.py"));
        if (script == null) { rep.Add("terrain", PipelineStepStatus.Failed, "tools/terrain/auto_terrain.py not found (run from the repository or the published program folder)"); return d.Action; }
        string folder = TerrainFolder(o, model!);
        string slug = AutoSheetIndex.Slug(Path.GetFileNameWithoutExtension(model!));
        string outBase = Path.Combine(folder, slug, slug + "-3dep");
        double m = o.TerrainMargin;
        var args = new List<string> { script, "--crs", geo.CrsName, "--bbox", F(e.x0 - m), F(e.y0 - m), F(e.x1 + m), F(e.y1 + m),
            "--out", outBase, "--cache", folder, "--scale", F(geo.GridScale > 0 ? geo.GridScale : 1), "--center", F(geo.ScaleCenterX ?? 0), F(geo.ScaleCenterY ?? 0) };
        log($"terrain: 3DEP for {geo.CrsName}, box {e.x1 - e.x0 + 2 * m:F0} x {e.y1 - e.y0 + 2 * m:F0} m -> {outBase}.flt (tiles cached in {folder}\\tiles)");
        var (code, output) = RunPython(o.Python, args, TimeSpan.FromMinutes(30), log);
        var result = ParseResult(output);
        string status = result?.GetProperty("status").GetString() ?? "error";
        string message = result?.GetProperty("message").GetString() ?? (code == -1 ? "python could not be started (set SBM_PYTHON)" : $"exit {code}: " + Tail(output));
        if (status != "ok")
        {
            rep.Add("terrain", status == "network" ? PipelineStepStatus.Skipped : PipelineStepStatus.Failed, $"3DEP not attached: {message}",
                status == "network" ? "rerun `sbm pipeline <model.sbm>` with network" : null);
            return d.Action;
        }
        string file = result!.Value.GetProperty("file").GetString()!;
        doc.Project.References.RemoveAll(r => r.Name == AutoTerrainName);
        doc.Project.References.Add(new ReferenceModelDefinition
        {
            Name = AutoTerrainName, Path = file, Kind = ReferenceKind.Terrain, Format = ReferenceFormat.Grid, UnitScale = 1.0,
            ContourInterval = doc.Project.Units.Kind == SBM.Core.Units.UnitSystemKind.US ? 2 * 0.3048 : 0.5, R = 150, G = 140, B = 120,
        });
        doc.Save();
        var rr = doc.Regenerate();   // foundation tops / ground lines follow the new ground
        rep.Add("terrain", PipelineStepStatus.Done,
            $"{message}; {result.Value.GetProperty("dataset").GetString()} ({result.Value.GetProperty("tiles").GetArrayLength()} tile(s)), z {result.Value.GetProperty("zmin").GetDouble():F1} .. {result.Value.GetProperty("zmax").GetDouble():F1} m NAVD88 -> {file}; reference '{AutoTerrainName}' added, model saved and regenerated ({rr.Elapsed.TotalSeconds:F1} s)",
            "check the plan vertical datum (3DEP = NAVD88; NGVD29 plans differ by up to ~0.3-1 m)");
        return d.Action;
    }

    /// <summary>Sheet index step; returns the link rules file in use (null = none).</summary>
    private static string? SheetIndexStep(SbmDocument doc, PipelineOptions o, PipelineReport rep, Action<string> log)
    {
        string model = doc.FilePath!;
        var bridges = doc.Project.Bridges.Select(b => b.Name).ToList();
        string? links = CompanionDatabase.FindLinksFile(model, bridges);
        string? script = CompanionDatabase.FindScript("plan_index_auto.py");
        if (links == null)
        {
            var pdfs = KnownPdfs(doc.Project, model, o);
            if (pdfs.Count == 0)
            {
                rep.Add("sheet index", PipelineStepStatus.Unresolved, "no sheet-links*.json names this model and no plan PDF is known: no sheet index, no element -> sheet links",
                    "rerun `sbm pipeline <model.sbm> --pdf <plan set.pdf>` (or add the PDF to Doc.Project.PlanPdfs in the build script): it writes a skeleton sheet-links file + the automatic index");
                return null;
            }
            string dir = Path.GetDirectoryName(model)!;
            string group = bridges.Count > 0 ? ModelDatabase.BridgeGroupOf(bridges[0]) : doc.Project.Name;
            var sets = pdfs.Select(p => (id: AutoSheetIndex.Slug(Path.GetFileNameWithoutExtension(p)), pdf: Path.GetFileName(p), group)).ToList();
            string file = Path.Combine(dir, $"sheet-links-{AutoSheetIndex.Slug(Path.GetFileNameWithoutExtension(model))}.json");
            if (File.Exists(file)) { rep.Add("sheet index", PipelineStepStatus.Failed, $"{Path.GetFileName(file)} exists but does not name this model: not overwritten", "add the model file name to its \"models\""); return null; }
            File.WriteAllText(file, AutoSheetIndex.Skeleton(model, sets, pdfs.Select(p => Path.GetDirectoryName(p)!), DateTime.Now));
            rep.Add("sheet index", PipelineStepStatus.Done, $"wrote the SKELETON rules file {file} ({sets.Count} plan set(s), no link rules yet)",
                "write the link rules (sheet title regex -> element selectors) in it; until then the database has the sheets but no element -> sheet links");
            links = file;
        }
        var rules = LinkRules.Load(links);
        string rdir = Path.GetDirectoryName(links)!;
        var dbo = new DatabaseOptions { ModelFile = model };
        dbo.PlanFolders.AddRange(o.PlanFolders);
        dbo.PlanFolders.AddRange(KnownPdfs(doc.Project, model, o).Select(p => Path.GetDirectoryName(p)!));
        var folders = CompanionDatabase.PlanSearchFolders(dbo, rules, rdir);
        foreach (var set in rules.PlanSets)
        {
            string autoFile = AutoSheetIndex.PathFor(rdir, set.Id);
            string name = $"sheet index {set.Id}";
            if (set.Sheets != null && !o.Reindex)
            {
                rep.Add(name, PipelineStepStatus.UpToDate, $"curated index in {Path.GetFileName(links)}: {set.Sheets.Count} pages (authoritative; --reindex adds pages it lacks)");
                continue;
            }
            string? pdf = PlanSheets.FindPdf(set, folders);
            if (pdf == null)
            {
                rep.Add(name, File.Exists(autoFile) ? PipelineStepStatus.UpToDate : PipelineStepStatus.Unresolved,
                    $"plan PDF '{set.Pdf}' not found in {string.Join("; ", folders.Where(Directory.Exists))}" + (File.Exists(autoFile) ? $" (using the existing {Path.GetFileName(autoFile)})" : ": no index"),
                    "put the PDF in a plan folder (rules \"planFolders\", --plans, %SBM_PLANS%) or pass --pdf");
                continue;
            }
            if (File.Exists(autoFile) && File.GetLastWriteTimeUtc(autoFile) >= File.GetLastWriteTimeUtc(pdf) && File.GetLastWriteTimeUtc(autoFile) >= File.GetLastWriteTimeUtc(script ?? autoFile))
            {
                rep.Add(name, PipelineStepStatus.UpToDate, IndexText(AutoSheetIndex.Load(autoFile), autoFile));
                continue;
            }
            if (script == null) { rep.Add(name, PipelineStepStatus.Failed, "tools/plan_index_auto.py not found"); continue; }
            log($"indexing {Path.GetFileName(pdf)} ...");
            var (code, output) = RunPython(o.Python, new[] { script, pdf, autoFile }, TimeSpan.FromMinutes(20), log);
            var res = AutoSheetIndex.Load(autoFile);
            if (code != 0 || res == null) { rep.Add(name, PipelineStepStatus.Failed, code == -1 ? "python could not be started (set SBM_PYTHON)" : $"plan_index_auto.py exit {code}: {Tail(output)}"); continue; }
            int noTitle = res.RasterPages - res.OcrPages;
            rep.Add(name, noTitle > 0 ? PipelineStepStatus.Unresolved : PipelineStepStatus.Done, IndexText(res, autoFile),
                noTitle > 0 ? $"{noTitle} scanned page(s) without a text layer: install Tesseract (PyMuPDF OCR) and rerun, or write their titles into the rules file's \"sheets\" index"
                    : res.LowConfidence > 0 ? $"{res.LowConfidence} low-confidence page(s) (\"q\": \"low\" in {Path.GetFileName(autoFile)}): check / correct them in a curated \"sheets\" index" : null);
        }
        if (rules.Rules.Count == 0)
            rep.Add("link rules", PipelineStepStatus.Unresolved, $"{Path.GetFileName(links)} has no link rules: the database gets the sheets but no element -> sheet links",
                "write rules (title regex -> element selectors), see sheet-links-nebraska-st.json and docs/API.md \"Companion database\"");
        return links;
    }

    private static string IndexText(AutoSheetIndex.Result? r, string file) => r == null ? $"{Path.GetFileName(file)} unreadable"
        : $"AUTOMATIC index {Path.GetFileName(file)}: {r.PageCount} pages ({r.TextPages} text, {r.RasterPages} scanned, {r.OcrPages} OCR), {r.Empty} without title, {r.LowConfidence} low confidence";

    private static string F(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static string Tail(string s) { var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries); return string.Join(" | ", lines.TakeLast(3)).Trim(); }

    public static JsonElement? ParseResult(string output)
    {
        foreach (var line in output.Split('\n').Reverse())
            if (line.StartsWith("RESULT ", StringComparison.Ordinal))
                try { return JsonDocument.Parse(line[7..]).RootElement.Clone(); } catch (JsonException) { return null; }
        return null;
    }

    /// <summary>Runs <c>python -I script args</c>; returns (exit code, stdout + stderr); -1 = could not start.</summary>
    public static (int code, string output) RunPython(string python, IEnumerable<string> args, TimeSpan timeout, Action<string>? log = null)
    {
        var psi = new ProcessStartInfo(python) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-I");
        foreach (var a in args) psi.ArgumentList.Add(a);
        var sb = new System.Text.StringBuilder();
        Process? p;
        try { p = Process.Start(psi); } catch (Exception ex) { return (-1, ex.Message); }
        if (p == null) return (-1, "not started");
        p.OutputDataReceived += (_, e) => { if (e.Data == null) return; lock (sb) sb.AppendLine(e.Data); if (!e.Data.StartsWith("RESULT ")) log?.Invoke("  " + e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data == null) return; lock (sb) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        if (!p.WaitForExit((int)timeout.TotalMilliseconds)) { try { p.Kill(true); } catch (Exception) { } return (-2, sb + "\ntimed out"); }
        p.WaitForExit();
        return (p.ExitCode, sb.ToString());
    }
}
