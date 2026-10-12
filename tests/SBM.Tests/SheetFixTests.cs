using System.IO.Compression;
using SBM.Api;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Drawing.Views;
using SBM.IO.Dxf;
using Xunit;

namespace SBM.Tests;

/// <summary>Sheet fixes of the 2026-10-11 plan review (sheets-vs-plans.md): DXF zip export, notes, quantities, prestressed sheets.</summary>
public class SheetFixTests
{
    private static string TempDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "sbm-sheetfix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void SheetsDxfZip_HoldsOneEntryPerSheet_PlusModels()
    {
        var doc = SbmDocument.Sample();
        string dir = TempDir();
        try
        {
            var sheets = doc.Sheets();
            string zip = Path.Combine(dir, "set.zip");
            var names = doc.ExportSheetsDxf(zip, includeModels: true);
            using var z = ZipFile.OpenRead(zip);
            var sheetEntries = z.Entries.Where(e => !e.FullName.Contains('/')).ToList();
            var modelEntries = z.Entries.Where(e => e.FullName.StartsWith("models/")).ToList();
            Assert.Equal(sheets.Count, sheetEntries.Count);
            Assert.Equal(doc.Drawings().Count, modelEntries.Count);
            Assert.Equal(names.Count, z.Entries.Count);
            Assert.All(z.Entries, e => Assert.EndsWith(".dxf", e.FullName));
            // Each sheet entry is a paper-space DXF: R12 header, extents within 17 x 11 inches, sheet number in the name.
            Assert.Contains(sheetEntries, e => e.Name.Contains("S-1 "));
            using (var r = new StreamReader(sheetEntries[0].Open()))
            {
                string text = r.ReadToEnd();
                Assert.StartsWith("0\nSECTION", text);
                Assert.Contains("AC1009", text);
                Assert.EndsWith("EOF\n", text);
                var lines = text.Split('\n');
                int i = Array.IndexOf(lines, "$EXTMAX");
                double xmax = double.Parse(lines[i + 2], System.Globalization.CultureInfo.InvariantCulture);
                Assert.InRange(xmax, 1, 17.01);
            }
            Assert.False(File.Exists(zip + ".tmp"));

            // Without models: sheets only; a folder target writes files.
            var only = doc.ExportSheetsDxf(Path.Combine(dir, "only.zip"));
            Assert.Equal(sheets.Count, only.Count);
            var files = doc.ExportSheetsDxf(Path.Combine(dir, "folder"));
            Assert.Equal(sheets.Count, Directory.GetFiles(Path.Combine(dir, "folder"), "*.dxf").Length);
            Assert.All(files, f => Assert.True(File.Exists(f)));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void DrawingsZip_SameEntriesAsFolder()
    {
        var doc = SbmDocument.Sample();
        string dir = TempDir();
        try
        {
            var inFolder = doc.ExportDrawings(Path.Combine(dir, "f"));
            var inZip = doc.ExportDrawings(Path.Combine(dir, "d.zip"));
            Assert.Equal(inFolder.Select(Path.GetFileName).OrderBy(x => x), inZip.OrderBy(x => x));
            using var z = ZipFile.OpenRead(Path.Combine(dir, "d.zip"));
            Assert.Equal(inFolder.Count, z.Entries.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ZipNames_DuplicateStemsAreNumbered()
    {
        var u = SBM.Core.Units.UnitSystem.Default;
        var v = new SheetView("A", "same", new SBM.Core.Drawing.PlanDrawing(), 1, "", PaperSpace: true);
        string dir = TempDir();
        try
        {
            var names = SheetExport.WriteAll(Path.Combine(dir, "x.zip"), new[] { ("", "B", v), ("", "B", v), ("m", "B", v) }, u);
            Assert.Equal(new[] { "B - same.dxf", "B - same (2).dxf", "m/B - same.dxf" }, names);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
