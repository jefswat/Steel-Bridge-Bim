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
}
