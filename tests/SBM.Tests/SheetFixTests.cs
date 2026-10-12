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

    private static string RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SteelBridgeModeler.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException("repository root not found");
    }

    /// <summary>A shipped plan model (opened read-only, regenerated) and its drawing context.</summary>
    private static (SbmDocument doc, ViewContext ctx) Model(string file)
    {
        var doc = SbmDocument.Open(RepoFile(Path.Combine("samples", "plans", file)));
        doc.Regenerate();
        var m = doc.Model();
        return (doc, new ViewContext(m.Framing, m.Steel, m.Concrete, m.Substructure, null, doc.Units, DrawingOptions.For(doc.Units), m.Prestressed));
    }

    private static IEnumerable<string> Texts(Sheet s) => s.Drawing.Layers.SelectMany(l => l.Texts).Select(t => t.Text);

    [Fact]
    public void PrestressedNotes_AreAgencySpecific_WithStrengthsPerBeamType()
    {
        var (_, br) = Model("Br02069 TH65.sbm");
        var notes = string.Join("\n", PsText.Notes(br.Prestressed!, br.Framing.Definition, br.Units));
        Assert.Contains("MnDOT", notes);
        Assert.DoesNotContain("TxDOT", notes);
        Assert.DoesNotContain("Class H", notes);
        Assert.Contains("f'c = 9.5 ksi, f'ci = 8.0 ksi", notes);
        Assert.Contains("Elastomeric 12\" x 30\" x 3 1/4\"", notes);   // pads from the model, not the definition default

        var (_, wb) = Model("Woodbury I-29 NB over Floyd.sbm");
        string s = PsText.Strengths(wb.Prestressed!);
        Assert.Contains("BTD105: f'c = 7.0 ksi, f'ci = 6.0 ksi", s);
        Assert.Contains("BTD55, BTD60: f'c = 5.0 ksi, f'ci = 4.5 ksi", s);
        Assert.Equal("Iowa DOT BTD105", PsText.TypeName(wb.Prestressed!.Shape, "BTD105"));
        // No TxDOT wording anywhere on the Woodbury sheets; the prestressed sheets are there.
        var sheets = SheetSet.Build(wb, new SheetFormat(17, 11, "P", "B", "D"));
        Assert.DoesNotContain(sheets.SelectMany(Texts), t => t.Contains("TxDOT") || t.Contains("IGEB") || t.Contains("Class H"));
        foreach (var title in new[] { "Beam layout spans 1-3", "Prestressed beam BTD55", "Prestressed beam BTD105", "Prestressed beam BTD60", "Prestressed beam schedule",
                                      "Steel intermediate diaphragms", "Pier 1 diaphragm", "S. Abut. diaphragm" })
            Assert.Contains(sheets, x => x.Title == title);
        Assert.DoesNotContain(sheets, x => x.Title.Contains("ABUT. ABUTMENT", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sheets.SelectMany(Texts), t => t.Trim() == "BEAM STEP TOPS:");
        Assert.All(sheets, x => Assert.Empty(x.Warnings));
        foreach (var x in sheets)
        {
            var e = x.Drawing.Extents();
            Assert.True(e.MinX >= -1e-6 && e.MaxX <= 17 * 0.0254 + 1e-6 && e.MinY >= -1e-6 && e.MaxY <= 11 * 0.0254 + 1e-6, $"{x.Number} {x.Title}");
        }
    }

    [Fact]
    public void TxGirders_KeepTheTxDotNotes()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = SBM.Core.Bridge.GirderKind.TxDotPrestressed;
        foreach (var sp in b.Supports) sp.Substructure = SBM.Core.Bridge.SubstructureDefinition.Default(sp.Type);
        doc.Regenerate();
        var m = doc.Model();
        var notes = string.Join("\n", PsText.Notes(m.Prestressed!, m.Framing.Definition, doc.Units));
        Assert.Contains("TxDOT standard IGD", notes);
        Assert.Contains("Class H", notes);
    }

    [Fact]
    public void Quantities_GroupLikeThePlanItems_PileFillIsNotStructuralConcrete()
    {
        var (_, br) = Model("Br02069 TH65.sbm");
        var q = TableViews.QuantityRows(br);
        var sub = br.Substructure!;
        double fill = sub.Units.SelectMany(u => u.Solids).Where(s => s.IsConcrete && s.Kind == SBM.Core.Substructure.SubElementKind.Pile).Sum(s => Math.Abs(s.Mesh.Volume())) * 1.307951;
        Assert.True(fill > 50, $"pile fill {fill:0.0} CY");
        var tot = q.Single(r => r.Item == "STRUCTURAL CONCRETE - SUBSTRUCTURE TOTAL");
        Assert.Equal(sub.ConcreteVolume * 1.307951 - fill, tot.Value, 1);
        Assert.Equal("SUBSTRUCTURE", tot.Group);
        var pf = q.Single(r => r.Item == "CONCRETE FILL OF PIPE PILES");
        Assert.Equal("PILING", pf.Group);
        Assert.Equal(fill, pf.Value, 1);
        // One row per abutment; the two add up to the total.
        var units = q.Where(r => r.Item.StartsWith("STRUCTURAL CONCRETE - ") && !r.Item.EndsWith("TOTAL")).ToList();
        Assert.Equal(2, units.Count);
        Assert.Equal(tot.Value, units.Sum(r => r.Value), 1);
        Assert.Contains(q, r => r.Item == "BEAMS, PRETENSIONED PRESTRESSED CONCRETE, 30MH-101" && r.Value == 7);
        Assert.Contains(q, r => r.Item.StartsWith("PILES, 12\" CIP") && r.Unit == "LF");

        var (_, wb) = Model("Woodbury I-29 NB over Floyd.sbm");
        var w = TableViews.QuantityRows(wb);
        Assert.Contains(w, r => r.Item == "BEAMS, PRETENSIONED PRESTRESSED CONCRETE, BTD105" && r.Value == 5);
        Assert.Contains(w, r => r.Item == "STRUCTURAL STEEL - STEEL INTERMEDIATE DIAPHRAGMS");
        Assert.DoesNotContain(w, r => r.Item == "STRUCTURAL STEEL - CROSS FRAMES AND CROSS GIRDERS");
        // The modeled reinforcing is the pier bars; the groups the model lacks are listed, so the plan total can be reconciled.
        var epoxy = w.Single(r => r.Item == "REINFORCING STEEL, EPOXY COATED - TOTAL (MODELED BARS)");
        Assert.Equal(wb.Substructure!.RebarMass * 2.20462, epoxy.Value, 0);
        Assert.Contains(w, r => r.Item == "REINFORCING STEEL - DECK AND BARRIER" && double.IsNaN(r.Value));
        Assert.Contains(w, r => r.Item.StartsWith("REINFORCING STEEL - S. ABUT.") && double.IsNaN(r.Value));
        Assert.Contains(w, r => r.Item == "PILES, HP12X84" && Math.Abs(r.Value - 11440) < 1);
    }

    [Fact]
    public void PileTips_PlumbAndBatteredSeparately_PositionsAtTheFootingBottom()
    {
        var d = new Drafter(new SBM.Core.Drawing.PlanDrawing(), SBM.Core.Units.UnitSystem.Default, 1);
        double ft = 0.3048;
        var plumb = new SBM.Core.Substructure.PileInfo(0, 0, 1090 * ft + 0.3048, 966 * ft);
        var bat = new SBM.Core.Substructure.PileInfo(0, 1.6, 1091 * ft, 969.732 * ft, 0, 1.6 + 121.27 * ft / 4);
        Assert.Equal("TIP EL 966.000 (PLUMB), 969.732 (BATTERED)", PierViews.TipText(new[] { plumb, bat }, d));
        Assert.Equal("TIP EL 966.000", PierViews.TipText(new[] { plumb }, d));
        // 1:4 batter, pile top 1 ft above the footing bottom: 3 in further out at the footing bottom.
        var at = PierViews.PileAt(bat, 1090 * ft);
        Assert.Equal(1.6 + 0.25 * ft, at.Y, 4);
    }

    [Fact]
    public void MeshSection_CutsABox_IntoOneClosedLoop()
    {
        var m = new SBM.Core.Solids.Mesh();
        SBM.Core.Solids.MeshBuilder.AddBox(m, SBM.Core.Solids.Frame3.World, 0, 2, 0, 1, 0, 3);
        var sec = new MeshSection(new SBM.Core.Geometry.Vec3(1, 0, 0), new SBM.Core.Geometry.Vec3(1, 0, 0), new SBM.Core.Geometry.Vec3(0, 0, 1));
        var loops = MeshSection.Loops(sec.Cut(m));
        Assert.Single(loops);
        double area = Math.Abs(SBM.Core.Solids.MeshBuilder.SignedArea(loops[0]));
        Assert.Equal(1 * 3, area, 6);
        Assert.Empty(MeshSection.ClipX(sec.Cut(m), 5, 6));
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
