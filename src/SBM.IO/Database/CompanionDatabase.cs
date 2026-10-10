using System.Diagnostics;
using SBM.Core.Project;
using SBM.IO.Ifc;

namespace SBM.IO.Database;

public sealed class DatabaseOptions
{
    /// <summary>Plan-sheet link rules (JSON). Null = look for <c>sheet-links*.json</c> beside the model.</summary>
    public string? LinksFile { get; set; }
    /// <summary>Split the plan PDFs into single pages and (re)fill the sheets table. Without it, sheets already in the database are kept.</summary>
    public bool Pages { get; set; }
    public bool Previews { get; set; } = true;
    /// <summary>VACUUM after writing (smaller file; takes a few seconds and needs temporary disk space about the size of the database).</summary>
    public bool Vacuum { get; set; }
    /// <summary>Folders searched for the plan PDFs (the model folder and its <c>plans</c> subfolder are always searched).</summary>
    public List<string> PlanFolders { get; } = new();
    /// <summary>Write the unused-sheet report (markdown) here.</summary>
    public string? UnusedReport { get; set; }
    public string? ModelFile { get; set; }
    public Action<string>? Log { get; set; }
}

public sealed class CompanionResult
{
    public DatabaseBuildResult Build { get; set; } = new();
    public LinkResult? Links { get; set; }
    public int Sheets { get; set; }
    public string Coverage { get; set; } = "";
    public TimeSpan Elapsed { get; set; }
}

/// <summary>Builds the companion SQLite database of a model: element registry, plan sheets and sheet links (see <see cref="ModelDatabase"/>).</summary>
public static class CompanionDatabase
{
    public static string DefaultPath(string modelFile) => Path.ChangeExtension(modelFile, ".sqlite");

    public static string? FindLinksFile(string? modelFile)
    {
        string? dir = modelFile == null ? null : Path.GetDirectoryName(Path.GetFullPath(modelFile));
        return dir == null || !Directory.Exists(dir) ? null : Directory.EnumerateFiles(dir, "sheet-links*.json").OrderBy(f => f).FirstOrDefault();
    }

    public static string? FindScript()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            {
                string f = Path.Combine(d.FullName, "tools", "plan_split.py");
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
        res.Build = ModelDatabase.Write(dbPath, project, bridges, options.ModelFile, options.Vacuum);
        string? linksFile = options.LinksFile ?? FindLinksFile(options.ModelFile);
        LinkRules? rules = linksFile != null && File.Exists(linksFile) ? LinkRules.Load(linksFile) : null;
        using var cn = ModelDatabase.Open(dbPath);
        if (options.Pages)
        {
            if (rules == null) throw new InvalidOperationException("--pages needs a link rules file (plan sets): pass --links <file.json>.");
            string? script = FindScript() ?? throw new FileNotFoundException("tools/plan_split.py not found.");
            var folders = options.PlanFolders.Concat(new[] { dbDir, Path.Combine(dbDir, "plans") }).ToList();
            foreach (var set in rules.PlanSets)
            {
                string? pdf = PlanSheets.FindPdf(set, folders);
                if (pdf == null) { options.Log?.Invoke($"plan set {set.Id}: PDF '{set.Pdf}' not found in {string.Join("; ", folders)}"); continue; }
                string outDir = Path.Combine(dbDir, "plans", set.Id);
                options.Log?.Invoke($"splitting {Path.GetFileName(pdf)} -> {outDir}");
                PlanSheets.SplitPdf(pdf, outDir, script, options.Previews, options.Log);
                PlanSheets.ImportSheets(cn, set.Id, Path.Combine(outDir, "sheets.json"), "plans/" + set.Id);
            }
        }
        res.Sheets = ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM sheets");
        if (rules != null && res.Sheets > 0)
        {
            res.Links = PlanSheets.ApplyRules(cn, rules, Path.GetFileName(linksFile!));
            res.Coverage = PlanSheets.CoverageReport(cn);
            if (options.UnusedReport != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.UnusedReport))!);
                File.WriteAllText(options.UnusedReport, PlanSheets.UnusedReport(cn, rules, project.Name));
            }
        }
        res.Elapsed = sw.Elapsed;
        return res;
    }
}
