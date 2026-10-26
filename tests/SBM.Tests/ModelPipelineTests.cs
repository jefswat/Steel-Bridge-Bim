using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SBM.Api;
using SBM.Core.Project;
using SBM.Core.References;
using SBM.IO.Database;
using Xunit;

namespace SBM.Tests;

/// <summary>The default model pipeline (sbm pipeline): decisions, skips, sheet index merge / skeleton, database up-to-date logic. No network, no Python.</summary>
public class ModelPipelineTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sbmpipe_" + Guid.NewGuid().ToString("N"));

    public ModelPipelineTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private static BridgeProject Project(string? crs, params (string name, string path)[] terrains)
    {
        var p = new BridgeProject { Name = "t" };
        if (crs != null) p.Georeference = new GeoreferenceDefinition { CrsName = crs, GridScale = 1 };
        foreach (var (n, f) in terrains) p.References.Add(new ReferenceModelDefinition { Name = n, Path = f, Kind = ReferenceKind.Terrain });
        return p;
    }

    [Fact]
    public void Terrain_Decisions_NameWhatIsNeeded()
    {
        var on = new PipelineOptions { Offline = false };
        string model = @"C:\x\m.sbm";
        // no georeference, no terrain: a required decision, naming the georeference + the geolocate path
        var d = ModelPipeline.DecideTerrain(Project(null), model, on, _ => false);
        Assert.Equal(TerrainAction.Unresolved, d.Action);
        Assert.Contains("NO georeference", d.Reason);
        Assert.Contains("geolocate-cbis.csx", d.Next);
        // a terrain file that exists is kept, whatever the georeference
        Assert.Equal(TerrainAction.Keep, ModelPipeline.DecideTerrain(Project(null, ("g", @"C:\t\g.flt")), model, on, f => f == @"C:\t\g.flt").Action);
        // georeferenced in a supported CRS: fetched; the reference file missing counts as no terrain
        Assert.Equal(TerrainAction.Build, ModelPipeline.DecideTerrain(Project("EPSG:2276"), model, on, _ => false).Action);
        var miss = ModelPipeline.DecideTerrain(Project("EPSG:26915", ("old", @"C:\gone\g.flt")), model, on, _ => false);
        Assert.Equal(TerrainAction.Build, miss.Action);
        Assert.Contains(@"C:\gone\g.flt", miss.Reason);
        // unsupported CRS / offline / disabled / unsaved
        Assert.Equal(TerrainAction.Unresolved, ModelPipeline.DecideTerrain(Project("EPSG:9999"), model, on, _ => false).Action);
        Assert.Equal(TerrainAction.Skip, ModelPipeline.DecideTerrain(Project("EPSG:2276"), model, new PipelineOptions { Offline = true }, _ => false).Action);
        Assert.Equal(TerrainAction.Skip, ModelPipeline.DecideTerrain(Project("EPSG:2276"), model, new PipelineOptions { Terrain = false }, _ => false).Action);
        Assert.Equal(TerrainAction.Skip, ModelPipeline.DecideTerrain(Project("EPSG:2276"), null, on, _ => false).Action);
        // an invisible terrain is not "present"
        var hidden = Project("EPSG:2276", ("g", @"C:\t\g.flt"));
        hidden.References[0].Visible = false;
        Assert.Equal(TerrainAction.Build, ModelPipeline.DecideTerrain(hidden, model, on, _ => true).Action);
    }

    [Fact]
    public void TerrainCrs_MirrorsThePythonTable()
    {
        string py = File.ReadAllText(RepoFile(Path.Combine("tools", "terrain", "crs.py")));
        var listed = Regex.Matches(py, @"""((?:EPSG|ESRI):\d+)""").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.NotEmpty(listed);
        foreach (var c in listed) Assert.True(TerrainCrs.IsSupported(c), c + " is in crs.py but not in TerrainCrs");
        foreach (var c in TerrainCrs.All.Where(c => !Regex.IsMatch(c, @"^EPSG:(269(0[1-9]|1\d|2[0-3])|326\d\d)$")))
            Assert.True(listed.Contains(c), c + " is in TerrainCrs but not in crs.py");
        Assert.True(TerrainCrs.IsSupported("EPSG:26914"));
        Assert.True(TerrainCrs.IsSupported("epsg:32615"));
        Assert.False(TerrainCrs.IsSupported("EPSG:26924"));
        Assert.False(TerrainCrs.IsSupported(""));
    }

    [Fact]
    public void AutoIndex_Merge_CuratedWins_AndOnlyMissingPagesAreAdded()
    {
        var curated = new List<SheetIndexEntry> { new() { Page = 2, Sheet = "5", Title = "PIER (HAND)" }, new() { Page = 1, Sheet = "", Title = "TITLE" } };
        var auto = new List<SheetIndexEntry> { new() { Page = 1, Sheet = "1", Title = "AUTO TITLE" }, new() { Page = 3, Sheet = "6", Title = "AUTO P3" }, new() { Page = 0, Title = "bad" } };
        var (merged, added) = AutoSheetIndex.Merge(curated, auto);
        Assert.Equal(1, added);
        Assert.Equal(new[] { 1, 2, 3 }, merged.Select(e => e.Page));
        Assert.Equal("TITLE", merged[0].Title);
        Assert.Equal("PIER (HAND)", merged[1].Title);
        Assert.Equal("AUTO P3", merged[2].Title);
        var (onlyAuto, n) = AutoSheetIndex.Merge(null, auto);
        Assert.Equal(2, n);
        Assert.Equal(2, onlyAuto.Count);
        Assert.Equal("sheet-index-b00167-nebraska-ramp-b.auto.json", Path.GetFileName(AutoSheetIndex.PathFor(_dir, "B00167 - Nebraska Ramp B")));
    }

    [Fact]
    public void Skeleton_IsAValidRulesFile_ForTheModel()
    {
        string text = AutoSheetIndex.Skeleton(Path.Combine(_dir, "My Bridge.sbm"), new[] { ("plans-a", "Plans A.pdf", "My Bridge") }, new[] { @"C:\SBMData", @"C:\SBMData" }, new DateTime(2026, 10, 26));
        string f = Path.Combine(_dir, "sheet-links-my-bridge.json");
        File.WriteAllText(f, text);
        var r = LinkRules.Load(f);
        Assert.True(r.IsFor("My Bridge.sbm"));
        var set = Assert.Single(r.PlanSets);
        Assert.Equal("Plans A.pdf", set.Pdf);
        Assert.Null(set.Sheets);   // the index lives in the automatic index file
        Assert.Empty(r.Rules);
        Assert.Single(r.PlanFolders);
        Assert.Equal(f, CompanionDatabase.FindLinksFile(Path.Combine(_dir, "My Bridge.sbm")));
    }

    [Fact]
    public void DatabaseUpToDate_ComparesModelAndRuleInputs()
    {
        var t = new Dictionary<string, DateTime> { ["db"] = new(2026, 1, 2), ["m"] = new(2026, 1, 1), ["r"] = new(2026, 1, 1) };
        DateTime? M(string f) => t.TryGetValue(f, out var v) ? v : null;
        Assert.True(ModelPipeline.DatabaseUpToDate("db", "m", new[] { "r", "absent" }, M));
        t["r"] = new(2026, 1, 3);
        Assert.False(ModelPipeline.DatabaseUpToDate("db", "m", new[] { "r" }, M));
        t["r"] = new(2026, 1, 1); t["m"] = new(2026, 1, 3);
        Assert.False(ModelPipeline.DatabaseUpToDate("db", "m", Array.Empty<string>(), M));
        Assert.False(ModelPipeline.DatabaseUpToDate("nodb", "m", Array.Empty<string>(), M));
    }

    [Fact]
    public void TerrainFolder_SkipsReadOnlyData_ThenModelFolder()
    {
        string model = Path.Combine(_dir, "m.sbm");
        Assert.Equal(@"D:\t", ModelPipeline.TerrainFolder(new PipelineOptions { TerrainFolder = @"D:\t" }, model, _ => true));
        if (Environment.GetEnvironmentVariable("SBM_TERRAIN") is { Length: > 0 }) return;
        Assert.Equal(@"C:\SBMData\terrain", ModelPipeline.TerrainFolder(new PipelineOptions(), model, _ => true));
        Assert.Equal(Path.Combine(_dir, "terrain"), ModelPipeline.TerrainFolder(new PipelineOptions(), model, _ => false));
    }

    [Fact]
    public void PlanPdfs_RoundTrip_AndOmittedWhenEmpty()
    {
        var p = new BridgeProject { Name = "x" };
        Assert.DoesNotContain("PlanPdfs", p.ToJson());
        p.PlanPdfs.Add(@"C:\SBMData\plans.pdf");
        var q = BridgeProject.FromJson(p.ToJson());
        Assert.Equal(@"C:\SBMData\plans.pdf", Assert.Single(q.PlanPdfs));
    }

    [Fact]
    public void Run_NewPlanSet_WritesSkeleton_UsesTheAutoIndex_BuildsTheDatabase_ThenIsUpToDate()
    {
        string model = Path.Combine(_dir, "proof.sbm");
        var doc = SbmDocument.Sample();
        doc.Project.References.Clear();
        doc.Save(model);
        string pdf = Path.Combine(_dir, "plan.pdf");
        File.WriteAllText(pdf, "%PDF-1.4");
        File.SetLastWriteTimeUtc(pdf, DateTime.UtcNow.AddDays(-1));
        // The automatic index the Python tool would write (newer than the PDF and the tool: no Python run).
        File.WriteAllText(AutoSheetIndex.PathFor(_dir, "plan"), """
            { "pdf": "plan.pdf", "page_count": 3, "text_pages": 2, "raster_pages": 1, "ocr_available": false, "ocr_pages": 0, "empty": 1, "low_confidence": 1,
              "sheets": [ { "p": 1, "s": "1", "t": "TITLE SHEET", "src": "text", "q": "ok" }, { "p": 2, "s": "2", "t": "FIELD SPLICE DETAILS", "src": "text", "q": "ok" },
                          { "p": 3, "s": "", "t": "", "src": "raster", "q": "low" } ] }
            """);
        var o = new PipelineOptions { Offline = true };
        o.Pdfs.Add(pdf);
        var rep = ModelPipeline.Run(doc, o);
        string text = rep.ToText();
        Assert.Contains(rep.Steps, s => s.Name == "georeference" && s.Status == PipelineStepStatus.Unresolved);
        Assert.Contains(rep.Steps, s => s.Name == "terrain" && s.Status == PipelineStepStatus.Unresolved && s.Message.Contains("NO georeference"));
        Assert.Contains(rep.Steps, s => s.Name == "sheet index" && s.Status == PipelineStepStatus.Done && s.Message.Contains("SKELETON"));
        Assert.Contains(rep.Steps, s => s.Name == "sheet index plan" && s.Status == PipelineStepStatus.UpToDate);
        Assert.Contains(rep.Steps, s => s.Name == "link rules" && s.Status == PipelineStepStatus.Unresolved);
        Assert.True(File.Exists(Path.Combine(_dir, "sheet-links-proof.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "proof.sqlite")));
        using (var mdb = new ModelDb(Path.Combine(_dir, "proof.sqlite"))) Assert.Equal(3, mdb.SheetCount);
        Assert.Contains("item(s) open", text);
        Assert.False(rep.Failed);

        // Second run: the skeleton is not rewritten (the hand-edited file wins), the database is up to date.
        File.AppendAllText(Path.Combine(_dir, "sheet-links-proof.json"), "\n// hand edit\n");
        File.SetLastWriteTimeUtc(Path.Combine(_dir, "proof.sqlite"), DateTime.UtcNow.AddMinutes(1));
        var rep2 = ModelPipeline.Run(doc, o);
        Assert.Contains("// hand edit", File.ReadAllText(Path.Combine(_dir, "sheet-links-proof.json")));
        Assert.Contains(rep2.Steps, s => s.Name == "database" && s.Status == PipelineStepStatus.UpToDate);
        Assert.DoesNotContain(rep2.Steps, s => s.Message.Contains("SKELETON"));

        // Status reports without changing anything.
        var st = ModelPipeline.Status(doc, o);
        Assert.Contains(st.Steps, s => s.Name == "database" && s.Status == PipelineStepStatus.UpToDate);
    }

    [Fact]
    public void Run_UnsavedModel_SaysSaveFirst()
    {
        var rep = ModelPipeline.Run(SbmDocument.Sample(), new PipelineOptions { Offline = true, Regenerate = false });
        Assert.Contains(rep.Steps, s => s.Status == PipelineStepStatus.Skipped && s.Message.Contains("save it first"));
    }

    private static string RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SteelBridgeModeler.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException("repository root not found");
    }
}
