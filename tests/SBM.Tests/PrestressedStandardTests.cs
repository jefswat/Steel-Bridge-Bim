using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.IO.PgSuper;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>State standard shapes (exact outlines with arcs), per-span standard beams at standard length, debonding and
/// harping by grid column.</summary>
public class PrestressedStandardTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private readonly Xunit.Abstractions.ITestOutputHelper _out = output;
    private static SbmDocument Sample(Action<PrestressedBeamDefinition> edit)
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        edit(b.Girders.Prestressed);
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    private static void CopyInto(PrestressedBeamDefinition target, PrestressedBeamDefinition src)
    {
        foreach (var p in typeof(PrestressedBeamDefinition).GetProperties().Where(p => p.CanWrite))
            p.SetValue(target, p.GetValue(src.Clone()));
    }

    /// <summary>30MH test pattern: 2" grid rows at 2" and 4" (x = +-1 ... +-15 in) - not the Br02069 pattern.</summary>
    private static List<StrandPoint> MhLayout() =>
        StrandPatterns.Rows(new[] { 2.0, 4.0 }.Select(y => (In(y), Enumerable.Range(0, 8).SelectMany(k => new[] { In(-1 - 2 * k), In(1 + 2 * k) }).ToArray())));

    [Fact]
    public void Catalog_FindsStandardsByKey_AndKeepsTx()
    {
        Assert.NotNull(StandardBeamCatalog.Find("IA BTD"));
        Assert.NotNull(StandardBeamCatalog.Find("Iowa BTD"));
        Assert.NotNull(StandardBeamCatalog.Find("MnDOT 30MH"));
        Assert.NotNull(StandardBeamCatalog.Find("30MH"));
        Assert.Equal(TxGirderType.Tx54, StandardBeamCatalog.Find("Tx54")!.Type);
        Assert.Null(StandardBeamCatalog.Find("XYZ"));
        // Tx shapes keep the WBFL polygon and tabulated properties.
        var tx = TxGirderCatalog.Get(TxGirderType.Tx54);
        Assert.Null(tx.Profile);
        Assert.Equal(817, tx.Area);
    }

    [Fact]
    public void IowaBtd_ExactOutline_HasArcs_AndMatchesPrintedProperties()
    {
        var s = StandardBeamCatalog.Find("IA BTD")!;
        Assert.NotNull(s.Profile);
        var arcs = s.Profile!.Segments.Where(g => g.IsArc).ToList();
        Assert.Equal(6, arcs.Count); // R2 + R8 + R8 each side; bottom corners are 3/4" chamfers
        Assert.Equal(4, arcs.Count(a => Math.Abs(a.Radius - In(8)) < 1e-9));
        Assert.Equal(2, arcs.Count(a => Math.Abs(a.Radius - In(2)) < 1e-9));
        Assert.Equal(In(54), s.Profile.Height, 9);
        Assert.Equal(In(34), s.Profile.Width, 9);
        // Closed and tangent: every segment starts where the previous ends.
        var g = s.Profile.Segments;
        for (int i = 0; i < g.Count; i++) Assert.True((g[i].Start - g[(i + g.Count - 1) % g.Count].End).Length < 1e-9);
        // Printed on the standard: A 748.8 in2, yb 24.64 in, I 285,860 in4 (computed from the outline; differences reported).
        Assert.InRange(s.Area, 748.8 * 0.99, 748.8 * 1.01);
        Assert.InRange(s.Yb, 24.64 - 0.15, 24.64 + 0.15);
        Assert.InRange(s.Ix, 285860 * 0.99, 285860 * 1.01);
        Assert.Equal(3, StandardBeamCatalog.PropertyCheck(s).Count);
        // Tessellated outline area converges on the exact area.
        var o = s.Outline().Select(p => new Vec2(p.X / In(1), p.Y / In(1))).ToList();
        Assert.InRange(SBM.Core.Solids.MeshBuilder.SignedArea(o), s.Area - 0.5, s.Area + 0.5);
        foreach (var r in StandardBeamCatalog.PropertyCheck(s)) _out.WriteLine($"BTD {r.Property}: computed {r.Computed:0.###} printed {r.Printed:0.###} diff {r.Difference:0.###}");
    }

    [Fact]
    public void MnDot30Mh_ExactOutline_HasArcs()
    {
        var s = StandardBeamCatalog.Find("MN 30MH")!;
        var arcs = s.Profile!.Segments.Where(g => g.IsArc).ToList();
        Assert.Equal(8, arcs.Count); // 2" R, 6" R, 6" R, 1" R each side
        Assert.Equal(4, arcs.Count(a => Math.Abs(a.Radius - In(6)) < 1e-9));
        Assert.Equal(2, arcs.Count(a => Math.Abs(a.Radius - In(1)) < 1e-9));
        Assert.Equal(In(30), s.Profile.Height, 9);
        Assert.Equal(In(39), s.Profile.Width, 9);
        Assert.Empty(StandardBeamCatalog.PropertyCheck(s)); // nothing printed on the plan
        // Sharp polygon minus the corner cuts: area below the sharp-corner area, above web + flanges.
        double sharp = 2 * (17 * 5 + (17 + 3.25) * 1.625 / 2 + 3.25 * 12.4375 + (19.5 + 3.25) * 5.4375 / 2 + 19.5 * 5.5);
        Assert.InRange(s.Area, sharp - 2, sharp + 20); // concave web fillets add area, convex corners remove a little
    }

    [Fact]
    public void SectionProfile_FilletedSquare_HasExactProperties()
    {
        // 10 x 10 square with R1 on every corner: A = 100 - (4 - pi), centroid at the middle.
        var p = SectionProfile.FromCorners(new[]
        {
            ProfileCorner.Fillet(0, 0, 1), ProfileCorner.Fillet(10, 0, 1), ProfileCorner.Fillet(10, 10, 1), ProfileCorner.Fillet(0, 10, 1),
        });
        var (a, xc, yc, ix, _) = p.Properties();
        Assert.Equal(100 - (4 - Math.PI), a, 9);
        Assert.Equal(5, xc, 9);
        Assert.Equal(5, yc, 9);
        Assert.True(ix < 1000.0 / 12 * 10 && ix > 800);
    }

    [Fact]
    public void StandardBeam_IsBuiltAtStandardLength_WithCheckAndUnresolvedPlacement()
    {
        // First pass: layout bearing spans of the sample bridge.
        var doc = Sample(p => CopyInto(p, StandardBeamCatalog.Beam("BTD55")!));
        Assert.Empty(doc.Regenerate().Errors);
        var b0 = doc.Model().Prestressed!.Beams[0];
        double layout = b0.LayoutBearingSpan;
        Assert.True(b0.IsStandardLength);

        // Standard 1'-0" longer than the layout, placement not set: modeled at the standard length, centred, UNRESOLVED.
        var p = doc.Bridge().Girders.Prestressed;
        p.StandardBearingLength = layout + Ft(1);
        p.StandardOverallLength = p.StandardBearingLength + In(16);
        p.SpanBeams = null;
        doc.Regenerate();
        var m = doc.Model().Prestressed!;
        var b = m.Beams.First(x => x.Girder == b0.Girder && x.Span == b0.Span);
        Assert.Equal(Ft(1), b.LengthDifference, 6);
        Assert.Equal(layout + Ft(1), (b.BearingEnd - b.BearingStart).Length, 4);
        Assert.Equal(layout + Ft(1) + In(16), b.Length * Math.Sqrt(1 + b.Slope * b.Slope), 4);
        Assert.NotNull(b.Assumption);
        Assert.Contains(m.Diagnostics, d => d.Message.StartsWith("CHECK (standard beam length)"));
        Assert.Contains(m.Diagnostics, d => d.Message.StartsWith("UNRESOLVED (prestressed): placement"));

        // Placement decided (hold the start bearing): no UNRESOLVED, start bearing on the layout bearing.
        p.Placement = StandardBeamPlacement.AtStartBearing;
        doc.Regenerate();
        var b2 = doc.Model().Prestressed!.Beams.First(x => x.Girder == b0.Girder && x.Span == b0.Span);
        Assert.Null(b2.Assumption);
        Assert.Equal(Ft(0.5), (b2.BearingStart - b.BearingStart).Length, 4); // centred: 6" before the layout bearing
    }

    [Fact]
    public void SpanBeams_GiveEachSpanItsOwnStandardBeam()
    {
        var doc = Sample(p =>
        {
            CopyInto(p, StandardBeamCatalog.Beam("BTD55")!);
            p.SpanBeams = new() { new PrestressedSpanBeam { Spans = new() { 1 }, Beam = StandardBeamCatalog.Beam("BTD60")! } };
        });
        Assert.Empty(doc.Regenerate().Errors);
        var beams = doc.Model().Prestressed!.Beams;
        Assert.All(beams.Where(x => x.Span == 0), x => Assert.Equal("BTD55", x.TypeName));
        Assert.All(beams.Where(x => x.Span == 1), x => Assert.Equal("BTD60", x.TypeName));
        Assert.All(beams.Where(x => x.Span == 1), x => Assert.Equal(Ft(60), (x.BearingEnd - x.BearingStart).Length, 4));
        Assert.All(beams, x => Assert.NotNull(x.Body));
    }

    [Fact]
    public void HarpedColumns_DeflectWholeColumns_ToBelowTopAtEnds_WithHoldDownsFromMidspan()
    {
        var d = StandardBeamCatalog.Beam("BTD105")!;
        var strands = StrandPatterns.Resolve(d, In(54))!;
        var harped = strands.Where(s => s.Harped).ToList();
        Assert.NotEmpty(harped);
        Assert.All(harped, s => Assert.Contains(d.HarpedColumns!, c => Math.Abs(c - s.X) < 1e-6));
        foreach (var col in harped.GroupBy(s => Math.Round(s.X, 6)))
        {
            var ordered = col.OrderByDescending(s => s.Y).ToList();
            Assert.Equal(In(54) - In(4), ordered[0].YEnd, 9);
            for (int k = 1; k < ordered.Count; k++) Assert.Equal(ordered[k - 1].YEnd - In(2), ordered[k].YEnd, 9);
        }
        var doc = Sample(p => CopyInto(p, d));
        doc.Regenerate();
        var b = doc.Model().Prestressed!.Beams[0];
        var dep = b.Strands.Where(s => s.Depressed).MaxBy(s => s.EndHeight)!;
        // Hold-down points 10'-6" each side of the beam's midspan.
        var mid = (b.TopStart + b.TopEnd) * 0.5;
        Assert.Equal(Ft(10.5), Math.Abs((dep.Points[1] - mid).XY.Length), 2);
        Assert.Equal(In(50), dep.EndHeight, 9);
    }

    [Fact]
    public void Debonding_ModelsSheathing_AndExportsTendonConduits()
    {
        // Br02069-style debonding (A 4 @ 9'-0", B 6 @ 13'-0", C 4 @ 17'-0" each end) on a 30MH test pattern.
        var layout = MhLayout();
        int a = StrandPatterns.Debond(layout, new[] { (In(-15), In(2)), (In(15), In(2)), (In(-13), In(2)), (In(13), In(2)) }, Ft(9), "A");
        int bb = StrandPatterns.Debond(layout, new[] { (In(-11), In(2)), (In(11), In(2)), (In(-9), In(2)), (In(9), In(2)), (In(-7), In(2)), (In(7), In(2)) }, Ft(13), "B");
        int c = StrandPatterns.Debond(layout, new[] { (In(-15), In(4)), (In(15), In(4)), (In(-13), In(4)), (In(13), In(4)) }, Ft(17), "C");
        Assert.Equal((4, 6, 4), (a, bb, c));
        var doc = Sample(p =>
        {
            p.Standard = "MN 30MH";
            p.Designation = "30MH";
            p.StrandLayout = layout;
            p.StrandCount = layout.Count;
            p.PadLength = In(10); p.PadWidth = In(24); p.PadThickness = In(2.5);
        });
        Assert.Empty(doc.Regenerate().Errors);
        var ps = doc.Model().Prestressed!;
        var beam = ps.Beams[0];
        Assert.Equal(14, beam.Strands.Count(s => s.Debonded));
        var sA = beam.Strands.First(s => s.DebondSymbol == "A");
        var sheath = sA.Sheathing();
        Assert.Equal(2, sheath.Count);
        Assert.All(sheath, piece => Assert.Equal(Ft(9), PrestressedModel.PolyLength(piece), 6));
        Assert.Equal(2 * (4 * Ft(9) + 6 * Ft(13) + 4 * Ft(17)) * ps.Beams.Count, ps.DebondedLength, 4);
        Assert.Contains(ps.Diagnostics, d => d.Message.Contains("sheathing"));

        string path = Path.Combine(Path.GetTempPath(), $"psd_{Guid.NewGuid():N}.ifc");
        try
        {
            doc.ExportIfc(path);
            string text = File.ReadAllText(path);
            Assert.Equal(ps.Beams.Count * 14 * 2, CountOf(text, "=IFCTENDONCONDUIT("));
            Assert.Equal(ps.Beams.Count * 14, CountOf(text, "'SBM_Debonding'"));
            // Exact profile with arcs.
            Assert.Equal(ps.Beams.Count, CountOf(text, "=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,'MN 30MH'"));
            Assert.Contains("IFCARCINDEX(", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Debonding_TooLongForTheBeam_IsWarned()
    {
        var layout = MhLayout();
        StrandPatterns.Debond(layout, new[] { (In(1), In(2)) }, Ft(400));
        var doc = Sample(p => { p.Standard = "MN 30MH"; p.StrandLayout = layout; p.StrandCount = layout.Count; p.PadLength = In(10); });
        doc.Regenerate();
        Assert.Contains(doc.Model().Prestressed!.Diagnostics, d => d.Message.Contains("debond lengths exceed"));
    }

    [Fact]
    public void Validate_StateStandardNeedsExplicitStrands()
    {
        var p = new PrestressedBeamDefinition { Standard = "IA BTD" };
        Assert.Contains(p.Validate(), e => e.Contains("explicit strand layout"));
        Assert.Contains(new PrestressedBeamDefinition { Standard = "nope" }.Validate(), e => e.Contains("unknown standard"));
        Assert.Empty(StandardBeamCatalog.Beam("BTD55")!.Validate());
    }

    [Fact]
    public void PgSuper_ReadsDebondInfo()
    {
        string src = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "PGSuper_WF58G.pgs"));
        int i = src.IndexOf("<StraightStrandDebonding", StringComparison.Ordinal);
        Assert.True(i >= 0);
        int j = src.IndexOf("</StraightStrandDebonding>", i, StringComparison.Ordinal) + "</StraightStrandDebonding>".Length;
        string deb = "<StraightStrandDebonding version=\"1\"><DebondInfoCount>1</DebondInfoCount>" +
                     "<DebondInfo version=\"1\"><Strand>0</Strand><Length1>3.048</Length1><Length2>1.524</Length2></DebondInfo></StraightStrandDebonding>";
        string path = Path.Combine(Path.GetTempPath(), $"pgd_{Guid.NewGuid():N}.pgs");
        try
        {
            File.WriteAllText(path, System.Text.RegularExpressions.Regex.Replace(src, "<StraightStrandDebonding version=\"1\"><DebondInfoCount>0</DebondInfoCount></StraightStrandDebonding>", deb));
            var pg = PgSuperReader.Read(path);
            var gd = pg.Girders[1];
            Assert.Equal((0, 3.048, 1.524), gd.Debond.Single());
            var shape = TxGirderShape.FromDims(pg.Dims!);
            var layout = PgSuperImport.StrandLayout(pg, gd, shape, new List<string>())!;
            var db = layout.Where(s => s.DebondStart > 0).ToList();
            Assert.InRange(db.Count, 1, 2); // grid entry 0 (single or pair)
            Assert.All(db, s => { Assert.Equal(3.048, s.DebondStart, 9); Assert.Equal(1.524, s.DebondEnd, 9); });
        }
        finally { File.Delete(path); }
    }

    private static int CountOf(string text, string what)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; }
        return n;
    }
}
