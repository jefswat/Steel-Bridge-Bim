using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using Xunit;

namespace SBM.Tests;

/// <summary>Element centerlines (geometric centre + section centroid) and lateral bracing zones (2026-10-26).</summary>
public class CenterlineAndLateralZoneTests
{
    private static Vec2[] Rect(double w, double h) => new[] { new Vec2(-w / 2, -h / 2), new Vec2(w / 2, -h / 2), new Vec2(w / 2, h / 2), new Vec2(-w / 2, h / 2) };

    /// <summary>Prism of a section given in a (u, v) frame swept between two points.</summary>
    private static Mesh Prism(Vec2[] sec, Vec3 a, Vec3 b, Vec3 u, Vec3 v)
    {
        var m = new Mesh();
        Vec3[] Ring(Vec3 o) => sec.Select(s => o + u * s.X + v * s.Y).ToArray();
        MeshBuilder.AddLoft(m, sec, new[] { Ring(a), Ring(b) });
        return m;
    }

    [Fact]
    public void Column_Plumb_BothLinesThroughTheCentre_RecipeAndSectionsAgree()
    {
        var o = new Vec3(500000, 4000000, 100);   // state-plane magnitudes
        var m = Prism(Rect(1.2, 0.8), o, o + new Vec3(0, 0, 9), new Vec3(1, 0, 0), new Vec3(0, 1, 0));
        foreach (bool recipes in new[] { true, false })
        {
            var cl = Centerlines.Of(m, new CenterlineOptions { UseRecipes = recipes });
            Assert.Null(cl.Note);
            Assert.Equal(2, cl.Centroid.Count);
            Assert.Equal(0, (cl.Centroid[0] - o).Length, 6);
            Assert.Equal(0, (cl.Centroid[1] - (o + new Vec3(0, 0, 9))).Length, 6);
            Assert.Equal(0, (cl.Geometric[1] - cl.Centroid[1]).Length, 6);
            Assert.Equal(1, cl.Axis.Z, 9);
        }
    }

    [Fact]
    public void AngleSection_CentroidDiffersFromGeometricCentre()
    {
        // L 6 x 4 x 1 (inches) drawn heel at the origin: centroid x = 1.65 / 1.02?  Computed exactly from the two rectangles.
        double In(double x) => x * 0.0254;
        var sec = new[] { new Vec2(0, 0), new Vec2(In(4), 0), new Vec2(In(4), In(1)), new Vec2(In(1), In(1)), new Vec2(In(1), In(6)), new Vec2(0, In(6)) };
        double a1 = In(4) * In(1), a2 = In(1) * In(5);
        double cx = (a1 * In(2) + a2 * In(0.5)) / (a1 + a2), cy = (a1 * In(0.5) + a2 * In(3.5)) / (a1 + a2);
        var m = Prism(sec, Vec3.Zero, new Vec3(6, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1));
        foreach (bool recipes in new[] { true, false })
        {
            var cl = Centerlines.Of(m, new CenterlineOptions { UseRecipes = recipes });
            Assert.Equal(cx, cl.Centroid[0].Y, 6);
            Assert.Equal(cy, cl.Centroid[0].Z, 6);
            Assert.Equal(In(2), cl.Geometric[0].Y, 6);
            Assert.Equal(In(3), cl.Geometric[0].Z, 6);
            Assert.Equal(6, cl.Length, 6);
        }
    }

    [Fact]
    public void TaperedCap_LinesFollowTheVaryingSection()
    {
        // cap 20 m long, 1.5 m wide, top level at z = 0, depth 2.0 m at x = 0 -> 1.0 m at x = 20 (tapered soffit)
        var m = new Mesh();
        var sec = Rect(1.5, 1.0);
        Vec3[] Ring(double x) { double d = 2.0 - x / 20; return new[] { new Vec3(x, -0.75, -d), new Vec3(x, 0.75, -d), new Vec3(x, 0.75, 0), new Vec3(x, -0.75, 0) }; }
        MeshBuilder.AddLoft(m, sec, new[] { Ring(0), Ring(20) });
        var cl = Centerlines.Of(m, new CenterlineOptions { UseRecipes = false });
        Assert.Null(cl.Note);
        // rectangle section: centroid = centre = -d / 2
        foreach (var p in cl.Centroid) Assert.Equal(-(2.0 - p.X / 20) / 2, p.Z, 3);
        Assert.Equal(0, cl.Centroid[0].X, 3);
        Assert.Equal(20, cl.Centroid[^1].X, 3);
    }

    [Fact]
    public void SampleModel_EveryElementGetsALine()
    {
        var doc = SbmDocument.Sample();
        var rows = doc.Centerlines();
        Assert.NotEmpty(rows);
        Assert.Contains(rows, r => r.Line.Method == "extrusion");
        Assert.Contains(rows, r => r.Ref.Kind == "sub");
        Assert.All(rows.Where(r => r.Ref.Kind == "sub" && r.Category == "Column"), r =>
        {
            Assert.Null(r.Line.Note);
            Assert.True(Math.Abs(r.Line.Axis.Z) > 0.99, $"{r.Ref.Mark} axis {r.Line.Axis}");
        });
        Assert.True(rows.Count(r => r.Line.Note != null) <= rows.Count / 50, string.Join("\n", rows.Where(r => r.Line.Note != null).Take(20).Select(r => $"{r.Ref.Kind} {r.Ref.Mark}: {r.Line.Note}")));
        Assert.All(rows.Where(r => r.Line.Note == null), r => Assert.True(r.Line.Centroid.Count >= 2 && r.Line.Geometric.Count >= 2));
    }

    // ------------------------------------------------------------------ lateral zones

    private static (BridgeFraming f, SteelModel m) Build(Action<BridgeDefinition> tweak)
    {
        var p = SampleProject.Create();
        tweak(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        return (f, SteelModelGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void Zones_ThatRepeatTheSingleLayout_GiveTheSameParts()
    {
        var (_, a) = Build(b => { b.LateralBracing.Enabled = true; b.LateralBracing.Bays = new() { 1, 4 }; });
        var (_, z) = Build(b =>
        {
            b.LateralBracing.Enabled = true;
            b.LateralBracing.Zones = new() { new LateralBracingZone { Bays = new() { 1, 4 } } };
        });
        var pa = a.Parts.Where(p => p.Assembly.StartsWith("LB-")).ToList();
        var pz = z.Parts.Where(p => p.Assembly.StartsWith("LB-")).ToList();
        Assert.NotEmpty(pa);
        Assert.Equal(pa.Select(p => p.Mark).OrderBy(x => x), pz.Select(p => p.Mark).OrderBy(x => x));
        var vz = pz.ToDictionary(p => p.Mark, p => p.Mesh.Volume());
        foreach (var p in pa) Assert.Equal(p.Mesh.Volume(), vz[p.Mark], 9);
        Assert.Equal(a.Bolts.Count(b => b.Assembly.StartsWith("LB-")), z.Bolts.Count(b => b.Assembly.StartsWith("LB-")));
    }

    [Fact]
    public void Zones_PerBayFlangePanelRange_PatternAndMember()
    {
        var (f, m) = Build(b =>
        {
            b.LateralBracing.Enabled = true;
            b.LateralBracing.Zones = new()
            {
                new LateralBracingZone { Name = "A", Spans = new() { 1 }, Bays = new() { 1 }, FromPanel = 1, ToPanel = 3, Angle = "L6x6x1/2", Source = "test p.1" },
                new LateralBracingZone { Name = "B", Spans = new() { 1 }, Bays = new() { 1 }, FromPanel = -3, Pattern = LateralPattern.SingleDiagonal, DiagonalStart = LateralDiagonalStart.RightGirder },
                new LateralBracingZone { Name = "T", Flange = LateralFlange.Top, Spans = new() { 1 }, Bays = new() { 2 }, Pattern = LateralPattern.X, Source = "test p.2" },
                new LateralBracingZone { Name = "none", Bays = new() { 9 } },
            };
        });
        int pts = f.CrossFrames.Where(c => c.Bay == 0 && (c.Span == 0 || (c.AtSupport && c.SupportIndex == 1))).Select(c => Math.Round(c.LeftGlobalDistance, 3)).Distinct().Count();
        Assert.True(pts >= 6, $"sample span 1 has {pts} panel points");
        var asm = m.Assemblies.Where(a => a.Kind == AssemblyKind.LateralBracing).Select(a => a.Mark).ToList();
        Assert.Contains("LB-S1-B1-R1", asm);
        Assert.Contains("LB-S1-B1-R2", asm);
        Assert.Contains("LB-S1-B2-T", asm);
        Assert.DoesNotContain(asm, x => x.StartsWith("LB-S2"));
        var r1 = m.Parts.Where(p => p.Assembly == "LB-S1-B1-R1" && p.Category == PartCategory.Angle).ToList();
        Assert.Equal(2, r1.Count);                                     // panel points 1-3 = two panels
        Assert.All(r1, p => Assert.Equal("L6x6x1/2", p.Description));
        var r2 = m.Parts.Where(p => p.Assembly == "LB-S1-B1-R2" && p.Category == PartCategory.Angle).ToList();
        Assert.Equal(2, r2.Count);                                     // last three points = two panels
        // X: two diagonals per panel, the second cut in two at the crossing and flagged
        int panelsB2 = f.CrossFrames.Where(c => c.Bay == 1 && (c.Span == 0 || (c.AtSupport && c.SupportIndex == 1))).Select(c => Math.Round(c.LeftGlobalDistance, 3)).Distinct().Count() - 1;
        var x = m.Parts.Where(p => p.Assembly == "LB-S1-B2-T" && p.Category == PartCategory.Angle).ToList();
        Assert.Equal(panelsB2 * 3, x.Count);
        Assert.Equal(panelsB2 * 2, x.Count(p => p.Assumption != null));
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("'none'") && d.Message.Contains("bay 9"));
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("'none'") && d.Message.Contains("nothing built"));
        Assert.DoesNotContain(m.Diagnostics, d => d.Message.Contains("lateral member too short"));
        Assert.All(m.Parts.Where(p => p.Assembly.StartsWith("LB-")), p => Assert.True(p.Mesh.Volume() > 0, p.Mark));
        // the shared panel point between R1 and R2 (if any) carries one gusset only
        var gus = m.Parts.Where(p => p.Assembly.StartsWith("LB-S1-B1") && p.Category == PartCategory.Gusset).ToList();
        var centres = gus.Select(g => g.Mesh.Bounds()).Select(bx => (bx.Min + bx.Max) * 0.5).ToList();
        for (int i = 0; i < centres.Count; i++)
            for (int j = i + 1; j < centres.Count; j++)
                Assert.True((centres[i] - centres[j]).Length > 0.05, $"{gus[i].Mark} / {gus[j].Mark} on the same point");
    }

    [Fact]
    public void Zones_ByDistanceFromTheSpanStart()
    {
        var (f, m) = Build(b =>
        {
            b.LateralBracing.Enabled = true;
            b.LateralBracing.Zones = new() { new LateralBracingZone { Spans = new() { 1 }, Bays = new() { 1 }, FromDistance = 0, ToDistance = 15 } };
        });
        double d0 = f.Girders[0].DistanceAt(0, 0.0);
        var pts = f.CrossFrames.Where(c => c.Bay == 0 && (c.Span == 0 || (c.AtSupport && c.SupportIndex == 1))).Select(c => c.LeftGlobalDistance - d0).Distinct().OrderBy(x => x).ToList();
        int expected = Enumerable.Range(0, pts.Count - 1).Count(k => pts[k] >= -0.001 && pts[k + 1] <= 15.001);
        Assert.True(expected > 0);
        Assert.Equal(expected, m.Parts.Count(p => p.Assembly == "LB-S1-B1" && p.Category == PartCategory.Angle));
    }
}
