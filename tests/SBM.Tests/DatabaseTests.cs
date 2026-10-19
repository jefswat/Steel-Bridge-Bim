using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SBM.Api;
using SBM.IO.Database;
using SBM.IO.Ifc;
using Xunit;

namespace SBM.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sbmdb_" + Guid.NewGuid().ToString("N"));

    public DatabaseTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private static readonly HashSet<string> NotElements = new()
    {
        "IFCRELAGGREGATES", "IFCRELCONTAINEDINSPATIALSTRUCTURE", "IFCPROPERTYSET", "IFCRELDEFINESBYPROPERTIES", "IFCRELASSOCIATESMATERIAL",
        "IFCRELDEFINESBYTYPE", "IFCMECHANICALFASTENERTYPE", "IFCOPENINGELEMENT", "IFCRELVOIDSELEMENT",
        // IFC 4.3 alignment structure below the IfcAlignment row (layouts, segments, stationing referents) and its relations.
        "IFCRELNESTS", "IFCRELREFERENCEDINSPATIALSTRUCTURE", "IFCALIGNMENTHORIZONTAL", "IFCALIGNMENTVERTICAL", "IFCALIGNMENTSEGMENT", "IFCREFERENT",
    };

    private (SbmDocument doc, string db, CompanionResult res) Build()
    {
        var doc = SbmDocument.Sample();
        string db = Path.Combine(_dir, "m.sqlite");
        var res = doc.ExportDatabase(db);
        return (doc, db, res);
    }

    private static List<(string id, string parent, string level, string category, string name)> Rows(string db)
    {
        using var cn = ModelDatabase.Open(db);
        using var c = cn.CreateCommand();
        c.CommandText = "SELECT global_id, COALESCE(parent_global_id,''), level, COALESCE(category,''), COALESCE(name,'') FROM elements";
        using var r = c.ExecuteReader();
        var list = new List<(string, string, string, string, string)>();
        while (r.Read()) list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
        return list;
    }

    [Fact]
    public void GirderGroup_CarriesCamberAndBlocking()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Project.Bridges[0];
        var gc = new SBM.Core.Bridge.GirderCamberDefinition { Girder = 0, DatumPoint = 1 };
        gc.Keys.Add(new SBM.Core.Bridge.CamberKeyPoint { Number = 1, Support = 0 });
        gc.Keys.Add(new SBM.Core.Bridge.CamberKeyPoint { Number = 3, Support = 1 });
        for (int i = 1; i <= 3; i++)
            gc.Points.Add(new SBM.Core.Bridge.CamberPoint { Number = i, Camber = 0.0254 * i, SteelDeflection = 0.0254 * i, SlabDeflection = 0.0254 * 2 * i });
        b.Girders.Camber.Add(gc);
        doc.Regenerate();
        string db = Path.Combine(_dir, "c.sqlite");
        doc.ExportDatabase(db);
        using var cn = ModelDatabase.Open(db);
        using var c = cn.CreateCommand();
        c.CommandText = "SELECT properties FROM elements WHERE level = 'group' AND category = 'girder' AND name = 'G1'";
        string props = (string)c.ExecuteScalar()!;
        Assert.Contains("\"camber_points\":3", props);
        Assert.Contains("\"camber_datum_point\":1", props);
        Assert.Contains("blocking_max_ft", props);
        Assert.Contains("camber_table", props);
    }

    [Fact]
    public void DeckReinforcingGroup_CarriesPlanScheduleIncludingScheduleOnlyMarks()
    {
        var doc = SbmDocument.Sample();
        var r = doc.Project.Bridges[0].Deck.Reinforcement;
        r.Plan = new SBM.Core.Bridge.DeckPlanReinforcement();
        r.Plan.Transverse.Add(new SBM.Core.Bridge.DeckTransverseZone
        {
            Layer = SBM.Core.Bridge.DeckBarLayer.Top, Size = "#7", StartDistance = 0.3048 * 60, Spacing = 0.0254 * 7, Count = 12,
            Pieces = { new SBM.Core.Bridge.DeckLinePiece { Mark = "7a1" } },
        });
        r.Plan.PlanBarList.Add(new SBM.Core.Bridge.DeckPlanBarLine { Mark = "5e1", Size = "#5", Count = 30, Length = 0.3048 * 9.33, WeightLb = 292, Location = "END DAM" });
        doc.Regenerate();
        string db = Path.Combine(_dir, "plan.sqlite");
        doc.ExportDatabase(db);
        using var cn = ModelDatabase.Open(db);
        using var c = cn.CreateCommand();
        c.CommandText = "SELECT properties, global_id FROM elements WHERE level = 'group' AND category = 'deck reinforcing'";
        using var rd = c.ExecuteReader();
        Assert.True(rd.Read());
        string props = rd.GetString(0), gid = rd.GetString(1);
        rd.Close();
        Assert.Contains("\"plan_driven\":true", props);
        Assert.Contains("schedule_only_marks\":\"5e1\"", props);
        Assert.Contains("7a1,#7,12,", props);
        c.CommandText = "SELECT COUNT(*) FROM elements WHERE parent_global_id = $g AND level = 'bar'";
        c.Parameters.AddWithValue("$g", gid);
        Assert.Equal(12, Convert.ToInt32(c.ExecuteScalar()));      // the schedule-only mark has no bar element
    }

    [Fact]
    public void GlobalIds_AreTheIfcGlobalIds()
    {
        var (doc, db, res) = Build();
        string ifc = Path.Combine(_dir, "m.ifc");
        doc.ExportIfc(ifc);
        var inIfc = new HashSet<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(ifc), @"^#\d+=(IFC[A-Z]+)\('([0-9A-Za-z_$]{22})'", RegexOptions.Multiline))
            if (!NotElements.Contains(m.Groups[1].Value)) inIfc.Add(m.Groups[2].Value);
        using var cn = ModelDatabase.Open(db);
        var dbIfc = new HashSet<string>();
        using (var c = cn.CreateCommand())
        {
            c.CommandText = "SELECT global_id FROM elements WHERE in_ifc = 1";
            using var r = c.ExecuteReader();
            while (r.Read()) dbIfc.Add(r.GetString(0));
        }
        Assert.True(dbIfc.Count > 100);
        Assert.Equal(inIfc.OrderBy(x => x), dbIfc.OrderBy(x => x));
        Assert.True(res.Build.Elements >= dbIfc.Count);
    }

    [Fact]
    public void Hierarchy_IsConnected_AndBoltChainReachesTheProject()
    {
        var (_, db, _) = Build();
        var rows = Rows(db);
        var byId = rows.ToDictionary(r => r.id);
        Assert.Single(rows, r => r.level == "project");
        foreach (var r in rows.Where(r => r.level != "project"))
            Assert.True(byId.ContainsKey(r.parent), $"{r.level} {r.name} has no parent row");
        var bolt = rows.First(r => r.category == "bolt");
        var chain = new List<string>();
        for (var cur = bolt; ; cur = byId[cur.parent]) { chain.Add(cur.level + ":" + cur.category); if (cur.level == "project") break; }
        Assert.Contains(chain, c => c.StartsWith("assembly:"));
        Assert.Contains(chain, c => c == "group:girder" || c == "group:cross frame group");
        Assert.Contains(chain, c => c == "unit:unit");
        Assert.Equal("project:project", chain[^1]);
        using var mdb = new ModelDb(db);
        Assert.Equal(chain.Count, mdb.Chain(bolt.id).Count);
    }

    [Fact]
    public void TagIndex_ResolvesModelObjects()
    {
        var (doc, db, res) = Build();
        var steel = doc.Steel();
        var tags = res.Build.Tags;
        var part = steel.Parts[steel.Parts.Count / 2];
        string? gid = tags.Lookup(part);
        Assert.NotNull(gid);
        using var mdb = new ModelDb(db);
        Assert.Equal(part.Mark, mdb.Get(gid!)!.Mark);
        var m = doc.Model();
        var keysOnly = ModelDatabase.BuildTagIndex(doc.Project, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        Assert.Equal(gid, keysOnly.Lookup(part));
        Assert.NotNull(tags.Lookup(steel.Bolts[0]));
    }

    [Fact]
    public void Links_InheritDownTheHierarchy_AndUnusedSheetsAreListed_AndQcSurvivesRebuild()
    {
        var (doc, db, _) = Build();
        string rules = Path.Combine(_dir, "rules.json");
        File.WriteAllText(rules, """
            { "planSets": [ { "id": "set1", "pdf": "x.pdf", "bridgeGroup": "" } ],
              "administrative": [ "^COVER" ],
              "rules": [
                { "id": "splices", "sheets": "^FIELD SPLICE", "basis": "splices", "target": [ { "level": "assembly", "category": "field splice", "bridge_group": "*" } ] },
                { "id": "lighting", "sheets": "^LIGHTING", "basis": "lights", "target": [ { "level": "element", "category": "streetlight", "bridge_group": "*" } ] }
              ],
              "hints": [ { "sheets": "LIGHTING", "suggests": "lighting missing" } ] }
            """);
        string sheets = Path.Combine(_dir, "sheets.json");
        File.WriteAllText(sheets, """
            { "pdf_file": "x.pdf", "sheets": [
              { "pdf_page": 1, "design_sheet": "", "title": "COVER", "page_file": "p001.pdf", "preview_file": "" },
              { "pdf_page": 2, "design_sheet": "5", "title": "FIELD SPLICE DETAILS", "page_file": "p002.pdf", "preview_file": "" },
              { "pdf_page": 3, "design_sheet": "6", "title": "LIGHTING DETAILS", "page_file": "p003.pdf", "preview_file": "" } ] }
            """);
        using (var cn = ModelDatabase.Open(db))
        {
            Assert.Equal(3, PlanSheets.ImportSheets(cn, "set1", sheets, "plans/set1"));
            var lr = PlanSheets.ApplyRules(cn, LinkRules.Load(rules), "rules.json");
            Assert.Equal(1, lr.SheetsLinked);
            Assert.Contains(lr.Warnings, w => w.Contains("lighting"));
            Assert.Equal(1, ModelDatabase.Scalar<int>(cn, "SELECT COUNT(*) FROM v_unused_sheets"));   // cover is administrative, lighting unused
            string report = PlanSheets.UnusedReport(cn, LinkRules.Load(rules), "t");
            Assert.Contains("LIGHTING DETAILS", report);
            Assert.Contains("lighting missing", report);
            Assert.DoesNotContain("COVER", report);
        }
        using (var mdb = new ModelDb(db))
        {
            var all = Rows(db);
            string splice = all.First(r => r.category == "field splice").id;
            var plate = all.First(r => r.parent == splice);
            var direct = mdb.Sheets(splice);
            var inherited = mdb.Sheets(plate.id);
            Assert.Single(direct);
            Assert.False(direct[0].Inherited);
            Assert.Single(inherited);
            Assert.True(inherited[0].Inherited);
            Assert.Equal("FIELD SPLICE DETAILS", inherited[0].Title);
            mdb.SetQc(plate.id, inherited[0].SheetId, "issue", "tester", "hole pattern differs");
            Assert.Throws<ArgumentException>(() => mdb.SetQc(plate.id, inherited[0].SheetId, "bad", null, null));
        }
        // Rebuild: elements are replaced, sheets and QC stay.
        doc.ExportDatabase(db);
        using var again = new ModelDb(db);
        var rows2 = Rows(db);
        string sp2 = rows2.First(r => r.category == "field splice").id;
        var plate2 = rows2.First(r => r.parent == sp2);
        Assert.Equal("issue", again.Sheets(plate2.id).Single().QcStatus);
        Assert.Equal("hole pattern differs", again.Sheets(plate2.id).Single().Note);
    }

    private const string IndexRules = """
        { "models": [ "m.sbm" ], "planFolders": [ "pdfs" ],
          "planSets": [ { "id": "set1", "pdf": "plan*.pdf", "bridgeGroup": "",
            "sheets": [ { "p": 1, "s": "", "t": "TITLE SHEET" }, { "p": 2, "s": "5", "t": "FIELD SPLICE DETAILS" },
                        { "p": 3, "s": "6", "t": "FIELD SPLICE DETAILS" }, { "p": 4, "s": "7", "t": "SUMMARY`OF`QUANTITIES" } ] } ],
          "administrative": [ "^TITLE SHEET" ],
          "rules": [
            { "id": "splices", "sheets": "^FIELD SPLICE", "pages": [ 3 ], "basis": "splices", "target": [ { "level": "assembly", "category": "field splice", "bridge_group": "*" } ] },
            { "id": "qty", "sheets": "^SUMMARY OF QUANTITIES", "role": "general", "basis": "quantities", "target": [ { "level": "unit", "bridge_group": "*" } ] } ] }
        """;

    [Fact]
    public void SheetIndex_GivesLinksWithoutSplitting_AndRulesFileIsPickedPerModel()
    {
        var doc = SbmDocument.Sample();
        string model = Path.Combine(_dir, "m.sbm");
        // Two rules files beside the model: the one naming another model sorts first and must be skipped.
        File.WriteAllText(Path.Combine(_dir, "sheet-links-a.json"), """{ "models": [ "other.sbm" ], "planSets": [], "rules": [] }""");
        File.WriteAllText(Path.Combine(_dir, "sheet-links-m.json"), IndexRules);
        Assert.Equal(Path.Combine(_dir, "sheet-links-m.json"), CompanionDatabase.FindLinksFile(model));
        Assert.Null(CompanionDatabase.FindLinksFile(Path.Combine(_dir, "third.sbm")));

        string db = Path.Combine(_dir, "m.sqlite");
        var res = doc.ExportDatabase(db, new DatabaseOptions { ModelFile = model });
        Assert.Equal(4, res.Sheets);
        Assert.NotNull(res.Links);
        Assert.True(res.Links!.Links > 0);
        using (var mdb = new ModelDb(db))
        {
            var all = Rows(db);
            string splice = all.First(r => r.category == "field splice").id;
            string boltGroup = all.First(p => p.parent == splice && p.level == "group").id;
            var bolt = all.First(r => r.parent == boltGroup);
            var sheets = mdb.Sheets(bolt.id);
            // pages filter: only p.3 of the two FIELD SPLICE DETAILS sheets; the unit's general link is inherited too
            var sp = Assert.Single(sheets, s => s.Title == "FIELD SPLICE DETAILS");
            Assert.Equal(3, sp.PdfPage);
            Assert.Equal("6", sp.DesignSheet);
            Assert.True(sp.Inherited);
            Assert.Contains(sheets, s => s.Title == "SUMMARY OF QUANTITIES" && s.Role == "general");   // backticks normalized
            Assert.Null(mdb.PdfTarget(sp));   // no PDF anywhere yet
        }

        // The PDF appears in a planFolders folder: a relink (no element rebuild) records it and Open PDF goes to the sheet's page.
        Directory.CreateDirectory(Path.Combine(_dir, "pdfs"));
        File.WriteAllText(Path.Combine(_dir, "pdfs", "plan set.pdf"), "%PDF-1.4");
        var rl = CompanionDatabase.Relink(db, "t", new DatabaseOptions { ModelFile = model });
        Assert.Equal(4, rl.Sheets);
        using (var mdb = new ModelDb(db))
        {
            var all = Rows(db);
            string splice = all.First(r => r.category == "field splice").id;
            var s = mdb.Sheets(splice).Single(x => x.Title == "FIELD SPLICE DETAILS");
            Assert.False(s.Inherited);
            var t = mdb.PdfTarget(s);
            Assert.NotNull(t);
            Assert.Equal(Path.Combine(_dir, "pdfs", "plan set.pdf"), t!.Value.File);
            Assert.Equal(3, t.Value.Page);
            Assert.Equal(Path.Combine(_dir, "sheet-links-m.json"), mdb.Meta("links_file"));
        }
    }

    [Fact]
    public void SheetsFor_AndFindSheets_QueryAnExistingDatabase()
    {
        var doc = SbmDocument.Sample();
        string model = Path.Combine(_dir, "m.sbm");
        File.WriteAllText(Path.Combine(_dir, "sheet-links-m.json"), IndexRules);
        Directory.CreateDirectory(Path.Combine(_dir, "pdfs"));
        File.WriteAllText(Path.Combine(_dir, "pdfs", "plan set.pdf"), "%PDF-1.4");
        string db = Path.Combine(_dir, "m.sqlite");
        doc.ExportDatabase(db, new DatabaseOptions { ModelFile = model });
        using var mdb = new ModelDb(db);

        // by category (substring): a field splice, its path, direct + inherited sheets, one de-duplicated page list
        var res = mdb.SheetsFor("field splice", 5);
        Assert.True(res.TotalMatches >= res.Elements.Count);
        Assert.True(res.Elements.Count is > 0 and <= 5);
        var el = res.Elements[0];
        Assert.Contains(" > ", el.Path);
        Assert.Contains(el.Sheets, s => s.Title == "FIELD SPLICE DETAILS" && s.PdfPage == 3 && !s.Inherited);
        Assert.Contains(el.Sheets, s => s.Title == "SUMMARY OF QUANTITIES" && s.Inherited);
        var pages = Assert.Single(res.Pages);
        Assert.Equal(Path.Combine(_dir, "pdfs", "plan set.pdf"), pages.Key);
        Assert.Equal(new[] { 3, 4 }, pages.Value);

        // exact mark wins over substring matching; no match gives an empty result
        string mark = mdb.Get(el.GlobalId)!.Mark;
        if (mark.Length > 0) Assert.All(mdb.SheetsFor(mark).Elements, e => Assert.Equal(mark, e.Mark, ignoreCase: true));
        Assert.Equal(0, mdb.SheetsFor("no such thing zzz").TotalMatches);
        Assert.True(mdb.SheetsFor("%splice%").TotalMatches > 0);

        // sheets by title / number / regex
        var f = mdb.FindSheets("splice details");
        Assert.Equal(new[] { 2, 3 }, f.Select(s => s.PdfPage));
        Assert.Equal(f.Count, f.Count(s => s.PdfPath.EndsWith("plan set.pdf")));
        Assert.True(f.Single(s => s.PdfPage == 3).LinkedElements > 0);
        Assert.Equal(0, f.Single(s => s.PdfPage == 2).LinkedElements);
        Assert.Equal(3, Assert.Single(mdb.FindSheets("6")).PdfPage);
        Assert.Equal(new[] { 1, 4 }, mdb.FindSheets("^(TITLE|SUMMARY)").Select(s => s.PdfPage));
        Assert.Empty(mdb.FindSheets("nothing"));
    }

    private static string RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SteelBridgeModeler.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException("repository root not found");
    }

    [Theory]
    [InlineData("sheet-links-i80-rampg.json", "I-80 WB and Ramp G.sbm", 414)]
    [InlineData("sheet-links-woodbury.json", "Woodbury I-29 NB over Floyd.sbm", 48)]
    [InlineData("sheet-links-br02069.json", "Br02069 TH65.sbm", 39)]
    [InlineData("sheet-links-nht.json", "Northaven NHT.sbm", 155)]
    [InlineData("sheet-links-odot-o.json", "ODOT Bridge O tub unit.sbm", 49)]
    [InlineData("sheet-links-bonner-unit5.json", "Bonner Unit 5 segmental.sbm", 144)]
    [InlineData("sheet-links-bonner-unit6.json", "Bonner Unit 6 FIB.sbm", 98)]
    public void ShippedLinkRules_HaveASheetIndex_AndEveryRuleMatchesASheet(string file, string model, int pages)
    {
        string path = RepoFile(Path.Combine("samples", "plans", file));
        var rules = LinkRules.Load(path);
        Assert.True(rules.IsFor(model));
        Assert.Equal(path, CompanionDatabase.FindLinksFile(RepoFile(Path.Combine("samples", "plans", model))));
        var index = rules.PlanSets.SelectMany(p => (p.Sheets ?? new()).Select(s => (set: p.Id, s.Page, title: PlanSheets.NormalizeTitle(s.Title)))).ToList();
        Assert.Equal(pages, index.Count);
        foreach (var r in rules.Rules)
        {
            var rx = new Regex(r.Sheets, RegexOptions.IgnoreCase);
            Assert.True(index.Any(s => (r.PlanSets == null || r.PlanSets.Contains(s.set)) && (r.Pages == null || r.Pages.Contains(s.Page)) && rx.IsMatch(s.title)),
                $"{file}: rule '{r.Id}' matches no sheet of the index");
        }
    }
}
