using SBM.Api;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Drawing.Views;
using Xunit;

namespace SBM.Tests;

public class SheetTests
{
    private static (SbmDocument doc, ViewContext ctx) Ctx()
    {
        var doc = SbmDocument.Sample();
        var m = doc.Model();
        return (doc, new ViewContext(m.Framing, m.Steel, m.Concrete, m.Substructure, null, doc.Units, DrawingOptions.For(doc.Units)));
    }

    [Fact]
    public void SheetSet_FitsEveryViewOn11x17()
    {
        var (_, ctx) = Ctx();
        var sheets = SheetSet.Build(ctx, new SheetFormat(17, 11, "P", "B", "D"));
        Assert.Contains(sheets, s => s.Title.StartsWith("Girder elevations"));
        Assert.Contains(sheets, s => s.Title == "Steel details");
        Assert.Contains(sheets, s => s.Title == "Pier 1 bearing details");
        Assert.All(sheets, s => Assert.Empty(s.Warnings));
        foreach (var s in sheets)
        {
            var e = s.Drawing.Extents();
            Assert.True(e.MinX >= -1e-6 && e.MaxX <= 17 * 0.0254 + 1e-6 && e.MinY >= -1e-6 && e.MaxY <= 11 * 0.0254 + 1e-6, $"{s.Number} {s.Title} extents {e.MinX / 0.0254:0.00},{e.MinY / 0.0254:0.00} - {e.MaxX / 0.0254:0.00},{e.MaxY / 0.0254:0.00}");
        }
        Assert.Equal($"S-{sheets.Count}", sheets[^1].Number);
    }

    [Fact]
    public void Pdf_IsWellFormed()
    {
        var (_, ctx) = Ctx();
        var sheets = SheetSet.Build(ctx, new SheetFormat(17, 11, "P", "B", "D"));
        using var ms = new MemoryStream();
        SBM.IO.Pdf.PdfWriter.Write(ms, sheets, "test");
        string text = System.Text.Encoding.Latin1.GetString(ms.ToArray());
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains($"/Count {sheets.Count}", text);
        Assert.Contains("/MediaBox [0 0 1224 792]", text);
        Assert.EndsWith("%%EOF\n", text);
        // xref offsets point at "n 0 obj".
        int xref = int.Parse(text[(text.LastIndexOf("startxref\n") + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[xref..]);
        var first = text[(xref + 5)..].Split('\n')[2];
        long off = long.Parse(first[..10]);
        Assert.StartsWith("1 0 obj", text[(int)off..]);
    }

    [Fact]
    public void CrossFrameDetails_MatchSteelParts()
    {
        var (doc, _) = Ctx();
        var steel = doc.Steel();
        Assert.Equal(doc.Framing().CrossFrames.Count, steel.CrossFrameDetails.Count);
        foreach (var d in steel.CrossFrameDetails.Take(10))
        {
            int angles = steel.Parts.Count(p => p.Assembly == d.Mark && p.Category == SBM.Core.Steel.PartCategory.Angle);
            Assert.Equal(angles, d.Members.Count(m => m.Code != "DIA"));
            int bolts = steel.Bolts.Count(b => b.Assembly == d.Mark);
            Assert.Equal(bolts, d.Bolts.Count);
        }
    }

    [Fact]
    public void AnchorChecks_CoverEveryPierBearing()
    {
        var (doc, ctx) = Ctx();
        doc.Bridge().Bearings.Type = SBM.Core.Bridge.BearingType.Disc;
        var pier = ctx.Substructure!.Units.Single(u => u.Type == SBM.Core.Bridge.SupportType.Pier);
        var checks = BearingViews.Checks(ctx, pier, out _);
        Assert.Equal(5, checks.Count);
        Assert.All(checks, c => Assert.Equal(4, c.Bolts));
        Assert.All(checks, c => Assert.True(c.BottomZ < pier.CapTop.Max(p => p.Y)));
        Assert.Equal(0, BearingViews.SegmentDistance(new(0, 0, 0), new(0, 0, 1), new(-1, 0, 0.5), new(1, 0, 0.5)), 9);
        Assert.Equal(2, BearingViews.SegmentDistance(new(0, 0, 0), new(0, 0, 1), new(-1, 2, 0.5), new(1, 2, 0.5)), 9);
    }
}
