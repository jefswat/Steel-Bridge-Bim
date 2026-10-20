using SBM.Api;
using SBM.Core.Checks.Measure;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>QC measure tools (sbm measure / sbm section): frames from the geometry, never from the world axes.</summary>
public class MeasureTests
{
    private static readonly UnitSystem Us = new();

    /// <summary>A plate in an arbitrary skewed, sloped frame at state-plane coordinates: 20" x 12" x 1/2", one 2" x 2" corner clip,
    /// 2 columns x 3 rows of 15/16" holes at 3" pitch / 3" gage, 1 1/2" edge distances.</summary>
    private static (Mesh Mesh, Frame3 Frame) TiltedPlate()
    {
        var x = new Vec3(Math.Cos(0.65), Math.Sin(0.65), 0.18).Normalized();
        var f = Frame3.FromXZ(new Vec3(2_113_500.25, 193_240.75, 1130.5), x, new Vec3(0.25, -0.31, 1));
        var outline = new List<Vec2> { new(0, 0), new(In(20), 0), new(In(20), In(10)), new(In(18), In(12)), new(0, In(12)) };
        var holes = new List<Hole>();
        foreach (double u in new[] { 1.5, 4.5 })
            foreach (double v in new[] { 1.5, 4.5, 7.5 })
                holes.Add(new Hole(new Vec2(In(u), In(v)), In(15.0 / 16)));
        var m = new Mesh();
        PlateBuilder.AddPlanar(m, outline, holes, f, 0, In(0.5));
        return (m, f);
    }

    private static Mesh Untyped(Mesh m)
    {
        var r = new Mesh();
        r.Positions.AddRange(m.Positions);
        r.Indices.AddRange(m.Indices);
        return r;
    }

    private static ElementRef Id(string mark) => new() { Bridge = "test", Kind = "part", Mark = mark };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Plate_TiltedSkewed_FlattensToItsOwnFrame(bool recipe)
    {
        var (mesh, f) = TiltedPlate();
        if (!recipe) mesh = Untyped(mesh);
        var r = PlateMeasure.Measure(mesh, Id("P1"), "Gusset", "test", new PlateFrameOptions(), Us)!;
        Assert.Equal(recipe ? "recipe" : "mesh section", r.Source);
        Assert.Equal(0.5, r.Thickness.In, 4);
        Assert.Equal(20, r.Width.In, 3);
        Assert.Equal(12, r.Height.In, 3);
        // the plate normal is the frame z (either sign), not a world axis
        Assert.True(Math.Abs(Math.Abs(r.Normal[0] * f.Z.X + r.Normal[1] * f.Z.Y + r.Normal[2] * f.Z.Z) - 1) < 1e-6);
        Assert.Contains(r.Features, x => x.Kind == "clip" && Math.Abs(x.LegA!.Value.In - 2) < 1e-3 && Math.Abs(x.LegB!.Value.In - 2) < 1e-3);
        Assert.Equal(6, r.Holes.Count);
        Assert.All(r.Holes, h => Assert.Equal(15.0 / 16, h.Diameter.In, 3));
        var g = Assert.Single(r.HoleGroups);
        Assert.Equal(2, g.Columns + g.Rows - 3);   // 2 x 3 either way round
        Assert.Equal(6, g.Holes);
        Assert.True(g.FullGrid);
        Assert.Contains("3\"", g.SpacingU + g.SpacingV);
        Assert.Equal(1.5, g.MinEdgeDistance!.Value.In, 3);
    }

    [Fact]
    public void Plate_RotatedBoltColumn_IsOneColumnAlongItsOwnAxis()
    {
        // a column of 5 holes parallel to an end edge sloped 14 deg (tub web diaphragm): one column, not five
        double a = 14 * Math.PI / 180;
        var dir = new Vec2(Math.Sin(a), Math.Cos(a));
        var outline = new List<Vec2> { new(0, 0), new(In(40), 0), new(In(40) - In(20) * Math.Tan(a), In(20)), new(In(20) * Math.Tan(a), In(20)) };
        var holes = Enumerable.Range(0, 5).Select(i => new Hole(new Vec2(In(4), In(2)) + dir * In(3.5 * i), In(1.0625))).ToList();
        var m = new Mesh();
        PlateBuilder.AddPlanar(m, outline, holes, Frame3.World, 0, In(0.625));
        var r = PlateMeasure.Measure(m, Id("W"), "WebPlate", "", new PlateFrameOptions(), Us)!;
        var g = Assert.Single(r.HoleGroups);
        Assert.Equal(1, g.Columns);
        Assert.Equal(5, g.Rows);
        Assert.Equal(14, Math.Abs(g.AxisDeg), 1);
        Assert.Contains("4 spa @ 3 1/2\"", g.SpacingV);
    }

    [Fact]
    public void Union_TouchingStrips_MergeIntoOneOutline()
    {
        var a = new List<Vec2> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        var b = new List<Vec2> { new(1, 0), new(2, 0), new(2, 1), new(1, 1) };
        var c = new List<Vec2> { new(0, 1), new(0.5, 1), new(2, 1), new(2, 1.5), new(0, 1.5) };   // T-junction at (1, 1)
        var u = PlateGeometry.Union(new List<List<Vec2>> { a, b, c });
        var loop = Geo2.Simplify(Assert.Single(u));
        Assert.Equal(4, loop.Count);
        Assert.Equal(3.0, Geo2.Area(loop), 9);
    }

    [Fact]
    public void Bends_SampledArc_RecoversRadiusAndAngle()
    {
        double db = In(0.625), r = In(2.5);
        var pts2 = BarShape.Rounded(new[] { new Vec2(0, 0), new Vec2(In(12), 0), new Vec2(In(12), In(12)) }, r);
        var pts = pts2.Select(p => new Vec3(p.X, 0.6 * p.Y, 0.8 * p.Y)).ToList();   // rigidly into an inclined plane
        var (bends, straights) = MeasureQueries.Bends(pts, db);
        var b = Assert.Single(bends);
        Assert.Equal(90, b.AngleDeg, 1);
        Assert.Equal(2.5, b.Radius.In, 2);
        Assert.Equal(2, straights.Count);
    }

    [Fact]
    public void Fmt_PlanStyleLengthsAndChains()
    {
        Assert.Equal("3/4\"", Fmt.FtIn(In(0.75)));
        Assert.Equal("1'-3 5/16\"", Fmt.FtIn(In(15.3125)));
        Assert.Equal("3 spa @ 3\" = 9\" | 4 1/2\"", Fmt.Chain(new[] { In(3), In(3), In(3), In(4.5) }, In(0.125)));
    }

    private static readonly Lazy<(SbmDocument Doc, MeasureModel M)> Sample = new(() =>
    {
        var d = SbmDocument.Sample();
        d.Regenerate();
        return (d, d.MeasureModel());
    });

    [Fact]
    public void Assembly_FieldSplice_PlanesStackJointAndBoltsPerSide()
    {
        var (_, mm) = Sample.Value;
        var notes = new List<string>();
        var (m, a) = mm.Assembly("FS1-G1", null, notes);
        var r = AssemblyMeasure.Measure(mm, m, a, new AssemblyOptions());
        Assert.True(r.Planes.Count >= 3);                     // web + two flange planes
        Assert.Equal(r.Bolts, r.BoltsPlaced);
        var web = r.Planes.First(p => p.NormalFromVerticalDeg > 80);
        var j = Assert.Single(web.JointGaps);
        Assert.InRange(j.Gap.In, 0, 2);
        Assert.Equal(2, web.BoltsPerSide.Count);
        Assert.Equal(web.BoltsPerSide.Values.First(), web.BoltsPerSide.Values.Last());
        Assert.NotNull(r.GlobalId);
        Assert.Contains("<svg", MeasureSvg.Assembly(r));
    }

    [Fact]
    public void Section_SampleDeck_LoopsBarsCoverAndDims()
    {
        var (d, mm) = Sample.Value;
        var m = mm.Bridges[0];
        var pl = SectionPlanes.AtStation(mm, m, d.Station("21+70"), null, null, 0, new List<string>());
        // normal to the alignment incl. grade: not plumb unless the grade is zero
        Assert.True(Math.Abs(pl.V.Z) > 0.99);
        var r = ModelSection.Cut(mm, pl, new SectionOptions { Dims = true });
        var deck = Assert.Single(r.Elements, e => e.Element.Kind == "deck" && e.Element.Mark == "Deck slab");
        Assert.NotEmpty(deck.Loops);
        Assert.NotNull(deck.Element.GlobalId);
        Assert.Contains(r.Elements, e => e.Element.Kind == "part" && e.Element.Mark.Contains("-W"));
        Assert.NotEmpty(r.Bars);
        Assert.Contains(r.Bars, b => b.Cover is { In: > 0.5 and < 6 });
        Assert.Contains(r.Dims!, x => x.Kind == "vertical clear");
        Assert.Contains("<svg", ModelSection.Svg(r));
    }

    [Fact]
    public void Rebar_SampleSpacingLapsAndCover()
    {
        var (_, mm) = Sample.Value;
        var sp = MeasureQueries.Spacing(mm, "T1", null, null, "alignment", In(0.125));
        Assert.Contains("spa @", sp.Chain);
        Assert.True(sp.Lines > 50);
        var laps = MeasureQueries.Laps(mm, "L1", null, null, 4);
        Assert.NotEmpty(laps.Laps);
        Assert.True(laps.MinLap!.Value.In > 6);
        var cov = MeasureQueries.Cover(mm, "T1#1", null, null);
        Assert.True(cov.MinByFace.ContainsKey("top"));
        Assert.InRange(cov.MinByFace["top"].In, 0.5, 5);
    }

    [Fact]
    public void Distance_TwoParts_ClosestPointsAndGlobalIds()
    {
        var (_, mm) = Sample.Value;
        var r = MeasureQueries.Distance(mm, "G1-1-W1", "G2-1-W1", null);
        Assert.False(r.Overlap);
        Assert.InRange(r.Distance.In, 60, 200);      // girder spacing less one web thickness
        Assert.NotNull(r.A.GlobalId);
    }
}
