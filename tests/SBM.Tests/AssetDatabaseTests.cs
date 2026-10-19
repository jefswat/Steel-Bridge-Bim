using Microsoft.Data.Sqlite;
using SBM.Api;
using SBM.IO.Database;
using Xunit;

namespace SBM.Tests;

/// <summary>G9 asset management in the companion database: schema survival across rebuilds, file references, inspection CSV, calc diff / apply.</summary>
public class AssetDatabaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sbmasset_" + Guid.NewGuid().ToString("N"));

    public AssetDatabaseTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private (SbmDocument doc, string model, string db) Build()
    {
        var doc = SbmDocument.Sample();
        string model = Path.Combine(_dir, "m.sbm");
        doc.Save(model);
        string db = CompanionDatabase.DefaultPath(model);
        doc.ExportDatabase(db);
        return (doc, model, db);
    }

    private string WriteFile(string name, string text)
    {
        string p = Path.Combine(_dir, name);
        File.WriteAllText(p, text);
        return p;
    }

    // A minimal one-page PDF (MuPDF repairs the missing xref offsets).
    private const string Pdf = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
                               "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n";

    [Fact]
    public void AssetTables_SurviveRebuild_AndOrphansAreKeptAndReported()
    {
        var (doc, _, db) = Build();
        string photo = WriteFile("p1.jpg", "not really a jpeg, but bytes");
        long fileId;
        using (var m = new ModelDb(db))
        {
            Assert.Equal(AssetSchema.Version.ToString(), m.Meta("asset_schema_version"));
            string g1 = m.FindElement("G1");
            var f = m.AddFile("photo", photo, new ModelDb.AddFileOptions { Caption = "web", Thumbnail = false });
            fileId = f.Id;
            m.Link(f.Id, "element", g1);
            m.Link(f.Id, "element", "GONE_GLOBAL_ID_000000");
            var ins = m.AddInspection("2026-05-20", "routine", "S1");
            m.UpsertCondition(ins.Id, new ConditionRow { GlobalId = g1, ElementRef = "G1", ElementNumber = "107", Unit = "LF", TotalQty = 100, Cs1 = 90, Cs2 = 10 });
            m.SetRating(ins.Id, "deck", "7");
            m.AddFinding(new Finding { GlobalId = g1, Description = "rust", Severity = "low" });
            m.AddFinding(new Finding { GlobalId = "GONE_GLOBAL_ID_000000", Description = "on a removed element" });
        }
        // rebuild (element registry rewritten) twice, also after a model change
        doc.Set("Bridges[0].Girders.Section.WebDepth", "60\"");
        doc.Regenerate();
        doc.ExportDatabase(db);
        doc.ExportDatabase(db);
        using (var m = new ModelDb(db))
        {
            var c = m.AssetCounts();
            Assert.Equal(1, c["photos"]);
            Assert.Equal(1, c["inspections"]);
            Assert.Equal(1, c["element_condition"]);
            Assert.Equal(1, c["component_ratings"]);
            Assert.Equal(2, c["findings"]);
            string g1 = m.FindElement("G1");
            Assert.Single(m.FilesForElement(g1));
            Assert.Single(m.ConditionFor(g1));
            // a child of G1 sees the girder's element-level rows (via the ancestor)
            string g11 = m.FindElement("G1-1");
            var inherited = m.ConditionFor(g11);
            Assert.Single(inherited);
            Assert.Equal("G1", inherited[0].Via);
            var orphans = m.Orphans();
            Assert.Contains(orphans, o => o.Source == "findings" && o.GlobalId == "GONE_GLOBAL_ID_000000");
            Assert.Contains(orphans, o => o.Source == "asset_links" && o.Record == fileId.ToString());
            Assert.Equal("3", m.Meta("schema_version"));
        }
    }

    [Fact]
    public void ExistingDatabaseWithoutAssetTables_IsMigratedOnOpen()
    {
        var (_, _, db) = Build();
        using (var cn = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            cn.Open();
            using var c = cn.CreateCommand();
            c.CommandText = "DROP VIEW v_asset_orphans; DROP VIEW v_condition_history; DROP VIEW v_open_findings; " +
                            string.Concat(AssetSchema.Tables.Select(t => $"DROP TABLE {t}; ")) + "DELETE FROM meta WHERE key='asset_schema_version'; UPDATE meta SET value='2' WHERE key='schema_version';";
            c.ExecuteNonQuery();
        }
        using var m = new ModelDb(db);
        Assert.Equal(AssetSchema.Version.ToString(), m.Meta("asset_schema_version"));
        Assert.Equal(0, m.AssetCounts()["findings"]);
        Assert.Empty(m.Orphans());
    }

    [Fact]
    public void Files_AreCopiedByHash_ReferencedRelatively_AndChangesAreDetectedNotDeleted()
    {
        var (_, _, db) = Build();
        string src = WriteFile("Report 2026.pdf", Pdf);
        using (var m = new ModelDb(db))
        {
            var f = m.AddFile("document", src, new ModelDb.AddFileOptions { Thumbnail = false, Caption = "report" });
            Assert.Equal("file", f.LocationKind);
            Assert.StartsWith("documents/", f.Location);
            Assert.DoesNotContain(":", f.Location);
            Assert.Equal("Report 2026.pdf", f.FileName);
            Assert.Equal("application/pdf", f.MediaType);
            Assert.Equal(AssetStore.Sha256(src), f.Sha256);
            var loc = m.ResolveAsset(f);
            Assert.True(loc.Exists);
            Assert.StartsWith(AssetStore.DefaultFolder(db), loc.LocalPath);
            Assert.Equal(File.ReadAllText(src), File.ReadAllText(loc.LocalPath!));
            // same content again: one record
            Assert.Equal(f.Id, m.AddFile("document", src).Id);
            Assert.Empty(m.CheckFiles(hash: true));
            // changed content (same size) -> hash mismatch, deleted -> missing; the record is kept
            File.WriteAllText(loc.LocalPath!, Pdf.Replace("612", "613"));
            Assert.Equal(AssetFileState.HashChanged, Assert.Single(m.CheckFiles(hash: true)).State);
            File.Delete(loc.LocalPath!);
            Assert.Equal(AssetFileState.Missing, Assert.Single(m.CheckFiles()).State);
            Assert.NotNull(m.GetFile(f.Id));
            // URL references (permalinks) resolve to the URL, nothing is copied
            var u = m.AddUrl("photo", "https://example.org/assets/123", "IMG_1.jpg");
            var ul = m.ResolveAsset(u);
            Assert.False(ul.IsFile);
            Assert.Equal("https://example.org/assets/123", ul.Url!.AbsoluteUri);
        }
        // the model folder moved: database + assets folder together -> references still resolve
        string moved = Path.Combine(_dir, "moved");
        Directory.CreateDirectory(moved);
        File.Copy(src, Path.Combine(moved, "x.pdf"));
        SqliteConnection.ClearAllPools();
        File.Move(db, Path.Combine(moved, "m.sqlite"));
        Directory.Move(AssetStore.DefaultFolder(db), AssetStore.DefaultFolder(Path.Combine(moved, "m.sqlite")));
        using (var m = new ModelDb(Path.Combine(moved, "m.sqlite")))
        {
            var f2 = m.AddFile("photo", Path.Combine(moved, "x.pdf"), new ModelDb.AddFileOptions { Thumbnail = false });
            Assert.True(m.ResolveAsset(f2).Exists);
            Assert.StartsWith(moved, m.ResolveAsset(f2).LocalPath);
        }
    }

    [Fact]
    public void JpegExifDate_IsRead()
    {
        // JPEG SOI + APP1 Exif with IFD0 -> ExifIFD -> DateTimeOriginal
        var tiff = new List<byte> { (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0 };
        void U16(int v) { tiff.Add((byte)v); tiff.Add((byte)(v >> 8)); }
        void U32(int v) { U16(v & 0xFFFF); U16(v >> 16); }
        U16(1); U16(0x8769); U16(4); U32(1); U32(26); U32(0);          // IFD0 at 8: one entry -> ExifIFD at 26
        U16(1); U16(0x9003); U16(2); U32(20); U32(44); U32(0);         // ExifIFD at 26: DateTimeOriginal at 44
        tiff.AddRange(System.Text.Encoding.ASCII.GetBytes("2025:07:04 09:30:00\0"));
        var app1 = new List<byte>(System.Text.Encoding.ASCII.GetBytes("Exif\0\0")); app1.AddRange(tiff);
        var jpg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1, (byte)((app1.Count + 2) >> 8), (byte)(app1.Count + 2) };
        jpg.AddRange(app1); jpg.AddRange(new byte[] { 0xFF, 0xD9 });
        string p = Path.Combine(_dir, "e.jpg");
        File.WriteAllBytes(p, jpg.ToArray());
        Assert.Equal("2025-07-04T09:30:00", AssetStore.ExifDate(p));
    }

    [Fact]
    public void InspectionCsv_ImportsElementLevelAndRatings_MapsElements_AndExportRoundTrips()
    {
        var (_, _, db) = Build();
        string el = WriteFile("el.csv", """
            inspection_date,inspection_kind,structure_number,inspector,element_ref,element_number,element_name,parent_element_number,defect_number,defect_name,unit,total_qty,cs1,cs2,cs3,cs4,notes
            2024-05-14,routine,S1,J. Doe,G1,107,Steel Open Girder,,,,LF,265,250,15,0,0,
            2024-05-14,routine,S1,J. Doe,G1,107,Steel Open Girder,,1000,Corrosion,LF,15,0,15,0,0,"rust, light"
            5/20/2026,routine,S1,A. Smith,G1,107,Steel Open Girder,,,,LF,265,230,30,5,0,
            5/20/2026,routine,S1,A. Smith,NO SUCH THING,12,Deck,,,,SF,100,50,40,0,0,sum is wrong
            """);
        string rt = WriteFile("rt.csv", """
            inspection_date,structure_number,component,rating,element_ref,notes
            2024-05-14,S1,deck,7,,
            2026-05-20,S1,superstructure,6,G1,corrosion
            2026-05-20,S1,channel,N,,
            """);
        using var m = new ModelDb(db);
        var r1 = InspectionCsv.Import(m, el);
        Assert.Equal("element", r1.Layout);
        Assert.Equal(4, r1.Rows);
        Assert.Equal(2, r1.Inspections.Count);
        Assert.Equal(1, r1.Unmapped);
        Assert.Contains(r1.Warnings, w => w.Contains("NO SUCH THING"));
        Assert.Contains(r1.Warnings, w => w.Contains("add up to 90"));
        var r2 = InspectionCsv.Import(m, rt);
        Assert.Equal("ratings", r2.Layout);
        Assert.Equal(2, m.Inspections().Count); // same (date, kind, structure) -> same inspections
        // importing again replaces, never duplicates
        InspectionCsv.Import(m, el);
        Assert.Equal(4, m.Condition().Count);
        string g1 = m.FindElement("G1");
        var hist = m.ConditionFor(g1);
        Assert.Equal(3, hist.Count);
        Assert.Equal("2026-05-20", hist[0].InspectionDate);
        Assert.Equal("rust, light", hist.Single(h => h.DefectNumber == "1000").Note);
        Assert.Contains(m.RatingsFor(g1), r => r.Component == "superstructure" && r.Rating == "6");
        Assert.Contains(m.RatingsFor(g1), r => r.Component == "channel" && r.Rating == "N");
        Assert.Throws<ArgumentException>(() => m.SetRating(1, "deck", "10"));

        var files = InspectionCsv.ExportHistory(m, Path.Combine(_dir, "out", "hist.csv"));
        Assert.Equal(3, files.Count);
        // round trip into a fresh database of the same model: same rows
        string db2 = Path.Combine(_dir, "m2.sqlite");
        File.Copy(db, db2);
        using (var cn = new SqliteConnection($"Data Source={db2};Pooling=False"))
        {
            cn.Open();
            using var c = cn.CreateCommand();
            c.CommandText = "DELETE FROM element_condition; DELETE FROM component_ratings; DELETE FROM inspections;";
            c.ExecuteNonQuery();
        }
        using (var m2 = new ModelDb(db2))
        {
            InspectionCsv.Import(m2, files[0]);
            InspectionCsv.Import(m2, files[1]);
            Assert.Equal(m.Condition().Select(Key).OrderBy(x => x), m2.Condition().Select(Key).OrderBy(x => x));
            Assert.Equal(m.Ratings().Count, m2.Ratings().Count);
        }
        static string Key(ConditionRow c) => $"{c.InspectionDate}|{c.GlobalId}|{c.ElementNumber}|{c.DefectNumber}|{c.TotalQty}|{c.Cs1}|{c.Cs2}|{c.Cs3}|{c.Cs4}|{c.Note}";
    }

    [Fact]
    public void Findings_FollowUpStatus_AndPhotosOfFindingsShowOnTheElement()
    {
        var (_, _, db) = Build();
        using var m = new ModelDb(db);
        string g = m.FindElement("G1-1");
        var f = m.AddFinding(new Finding { GlobalId = g, Description = "Section loss", Severity = "high", LocationText = "Abut 1 bearing", Station = 320, Offset = -3.6 });
        Assert.True(f.IsOpen);
        var ph = m.AddFile("photo", WriteFile("f.png", "png bytes"), new ModelDb.AddFileOptions { Thumbnail = false });
        m.Link(ph.Id, "finding", f.Id.ToString());
        Assert.Single(m.FilesForElement(g));
        Assert.Single(m.Findings(m.FindElement("G1"), openOnly: true, withDescendants: true));
        m.UpdateFinding(f.Id, "repaired", "painted");
        var f2 = m.GetFinding(f.Id)!;
        Assert.False(f2.IsOpen);
        Assert.NotEqual("", f2.ClosedAt);
        Assert.Empty(m.Findings(g, openOnly: true));
        Assert.Throws<ArgumentException>(() => m.AddFinding(new Finding { GlobalId = g, Description = "x", Severity = "huge" }));
    }

    [Fact]
    public void CalcValues_DiffAgainstTheModel_AndApplyOnlySelectedInputs_WithALog()
    {
        var (doc, model, db) = Build();
        string pdf = WriteFile("calc.pdf", Pdf);
        string json = WriteFile("values.json", """
            {
              "schema": "sbm-calc-values/1",
              "extractedBy": "test",
              "values": [
                { "name": "Web depth", "propertyPath": "Bridges[0].Girders.Section.WebDepth", "value": 60, "unit": "in", "element": "G1", "page": 1, "quote": "web 60 in" },
                { "name": "Web thickness", "propertyPath": "Bridges[0].Girders.Section.WebThickness", "value": "1/2\"", "page": 1 },
                { "name": "Skew", "propertyPath": "Bridges[0].Supports[1].SkewDegrees", "value": 15.0004, "tol": "0.01", "page": 1 },
                { "name": "Steel", "resultPath": "Steel.SteelMass", "value": 1, "unit": "kip", "page": 1 },
                { "name": "Bad path", "propertyPath": "Bridges[0].NoSuchThing", "value": 1, "page": 1 },
                { "name": "Cap volume", "element": "Abut 1 cap", "field": "volume_m3", "value": 14.66263, "unit": "m3", "page": 1 }
              ]
            }
            """);
        double steelKip;
        using (var m = new ModelDb(db))
        {
            var calc = m.AddCalc(pdf, "1", "Girder calcs", elements: new[] { m.FindElement("G1") });
            Assert.Equal("1", calc.SummaryPages);
            Assert.Equal(5 + 1, CalcSync.ImportValues(m, calc.Id, json));
            var vals = m.CalcValues(calc.Id);
            Assert.Equal(m.FindElement("G1"), vals[0].GlobalId);
            Assert.Equal("60 in", vals[0].ValueText);
            Assert.Contains(m.CalcsForElement(m.FindElement("G1-1")), c => c.Id == calc.Id); // via the ancestor G1

            var diff = CalcSync.Diff(doc, m, calc.Id);
            Assert.Equal("differs", diff[0].Status);
            Assert.True(diff[0].Applicable);
            Assert.Equal("54\"", diff[0].ModelText);
            Assert.Equal("match", diff[1].Status);
            Assert.Equal("match", diff[2].Status); // within the given tolerance
            Assert.Equal("differs", diff[3].Status);
            Assert.False(diff[3].Applicable);       // a result is never applied
            Assert.Equal("missing", diff[4].Status);
            Assert.Equal("match", diff[5].Status);
            steelKip = doc.Model().Steel!.SteelMass / 453.59237;
            Assert.Equal(steelKip, double.Parse(diff[3].ModelText.Split(' ')[0].Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture), 2);

            // diff never changes anything
            Assert.Equal(0.0254 * 54, (double)doc.Get("Bridges[0].Girders.Section.WebDepth")!, 9);
            // apply: only the selected, differing input; the result and the matching one are skipped
            var res = CalcSync.Apply(doc, m, new[] { vals[0].Id, vals[1].Id, vals[3].Id }, by: "tester");
            Assert.Single(res.Applied);
            Assert.Equal(2, res.Skipped.Count);
            Assert.Equal(model, res.SavedTo);
            var log = Assert.Single(m.AppliedLog(calc.Id));
            Assert.Equal("Bridges[0].Girders.Section.WebDepth", log.PropertyPath);
            Assert.Equal("54\"", log.OldValue);
            Assert.Equal("60\"", log.NewValue);
            Assert.Equal("tester", log.AppliedBy);
            Assert.Equal(1, log.Page);
            Assert.Equal("Girder calcs", log.CalcTitle);
            // now it matches
            Assert.Equal("match", CalcSync.DiffOne(doc, m, m.CalcValues(calc.Id)[0]).Status);
        }
        Assert.Equal(0.0254 * 60, (double)SbmDocument.Open(model).Get("Bridges[0].Girders.Section.WebDepth")!, 9);
        // the log and values survive a rebuild
        doc.ExportDatabase(db);
        using (var m = new ModelDb(db))
        {
            Assert.Single(m.AppliedLog());
            Assert.Equal(6, m.CalcValues().Count);
        }
    }

    [Fact]
    public void CalcValuesJson_IsValidated()
    {
        Assert.Throws<FormatException>(() => CalcSync.ParseValues("""{"values":[{"name":"x","value":1}]}"""));
        Assert.Throws<FormatException>(() => CalcSync.ParseValues("""{"values":[{"name":"x","propertyPath":"A","resultPath":"B","value":1}]}"""));
        Assert.Throws<FormatException>(() => CalcSync.ParseValues("""{"values":[{"name":"x","field":"mass_kg","value":1}]}"""));
        Assert.Throws<FormatException>(() => CalcSync.ParseValues("""{"schema":"other/2","values":[]}"""));
        var v = Assert.Single(CalcSync.ParseValues("""[{"name":"Section","resultPath":"X","value":"W36x135","page":3,"quote":"Use W36x135"}]"""));
        Assert.Equal("W36x135", v.ValueText);
        Assert.Null(v.Value);
        Assert.Equal(new List<int> { 1, 2, 5 }, AssetStore.ParsePages("1-2,5"));
        Assert.Equal(new List<long> { 3, 4, 7, 8, 9 }, CalcSync.ParseIds("3,4,7-9"));
    }
}
