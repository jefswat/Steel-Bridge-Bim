using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Geometry.Nurbs;
using SBM.Core.Solids;
using Xunit;

namespace SBM.Tests;

public class NurbsTests
{
    [Fact]
    public void Arc_IsExactCircle_AndKnotsValid()
    {
        var c = new Vec3(1000, 2000, 30);
        var x = new Vec3(1, 0, 0); var y = new Vec3(0, 0.6, 0.8);
        foreach (var (a0, a1) in new[] { (0.0, 2 * Math.PI), (0.3, 1.2), (-1.0, 2.5) })
        {
            var arc = NurbsArc.Arc(c, x, y, 2.5, a0, a1);
            Assert.Null(BSpline.Check(arc.Knots, arc.Degree, arc.Control.Length));
            Assert.True(arc.Control.Length <= 9);
            for (int i = 0; i <= 200; i++)
            {
                double u = i / 200.0;
                var p = arc.PointAt(u);
                Assert.Equal(2.5, (p - c).Length, 10);
                Assert.Equal(0, Vec3.Dot(p - c, Vec3.Cross(x, y)), 10);
            }
            Assert.Equal(0, (arc.PointAt(0) - (c + x * 2.5 * Math.Cos(a0) + y * 2.5 * Math.Sin(a0))).Length, 10);
            Assert.Equal(0, (arc.PointAt(1) - (c + x * 2.5 * Math.Cos(a1) + y * 2.5 * Math.Sin(a1))).Length, 10);
            Assert.Equal(2.5 * (a1 - a0), arc.Length(), 6);
        }
    }

    [Fact]
    public void Derivatives_MatchFiniteDifferences()
    {
        var arc = NurbsArc.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), 3, 0, 2);
        var poly = new NurbsCurve(3, new double[] { 0, 0, 0, 0, 0.4, 0.7, 1, 1, 1, 1 },
            new[] { new Vec3(0, 0, 0), new Vec3(1, 2, 0), new Vec3(2, -1, 1), new Vec3(3, 3, 2), new Vec3(4, 0, 1), new Vec3(5, 1, 0) });
        foreach (var c in new[] { arc, poly })
            foreach (double u in new[] { 0.1, 0.33, 0.62, 0.77, 0.95 })   // not on knots (C1 joins)
            {
                const double h = 1e-6;
                var (_, d1, d2) = c.Eval(u);
                var fd1 = (c.PointAt(u + h) - c.PointAt(u - h)) * (1 / (2 * h));
                var fd2 = (c.Eval(u + h).d1 - c.Eval(u - h).d1) * (1 / (2 * h));
                Assert.True((d1 - fd1).Length < 1e-5 * (1 + d1.Length));
                Assert.True((d2 - fd2).Length < 1e-4 * (1 + d2.Length));
            }
        var s = NurbsSurface.Ruled(arc, arc.Translated(new Vec3(0, 0, 2)));
        var (p, su, sv) = s.Eval(0.4, 0.3);
        const double e = 1e-6;
        Assert.True((su - (s.PointAt(0.4 + e, 0.3) - s.PointAt(0.4 - e, 0.3)) * (1 / (2 * e))).Length < 1e-5);
        Assert.True((sv - new Vec3(0, 0, 2)).Length < 1e-9);
        Assert.Equal(3, p.XY.Length, 10);
    }

    [Fact]
    public void BasisFunctions_PartitionOfUnity()
    {
        var U = new double[] { 0, 0, 0, 0, 1, 2, 2, 2, 3, 4, 4, 4, 4 };   // interior multiplicity 3 = degree (C0)
        Assert.Null(BSpline.Check(U, 3, U.Length - 4));
        Assert.NotNull(BSpline.Check(new double[] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2 }, 3, 8));   // interior multiplicity 4 > degree
        Span<double> N = stackalloc double[4];
        for (double u = 0; u <= 4; u += 0.05)
        {
            int sp = BSpline.FindSpan(U.Length - 5, 3, u, U);
            BSpline.BasisFuns(sp, u, 3, U, N);
            double sum = 0;
            foreach (double n in N) { Assert.True(n >= -1e-15); sum += n; }
            Assert.Equal(1, sum, 12);
        }
    }

    [Fact]
    public void Fit_ReachesTolerance_AndInterpolatesBreaks()
    {
        // helix-like offset curve with a kink at u = 7 (grade break)
        Vec3 F(double u) => new(500000 + 300 * Math.Cos(u / 300), 4000000 + 300 * Math.Sin(u / 300), 100 + (u < 7 ? 0.02 * u : 0.14 - 0.03 * (u - 7)));
        var fit = NurbsFit.FitCommon(1, u => new[] { F(u) }, 0, 40, new[] { 7.0 }, 1e-4);
        Assert.True(fit.Converged);
        Assert.True(fit.MaxDeviation <= 1e-4);
        var c = fit.Curve(0);
        Assert.Null(BSpline.Check(c.Knots, 3, c.Control.Length));
        Assert.Equal(0, (c.PointAt(0) - F(0)).Length, 9);
        Assert.Equal(0, (c.PointAt(40) - F(40)).Length, 9);
        Assert.Equal(0, (c.PointAt(7) - F(7)).Length, 9);
        double worst = 0;
        for (int i = 0; i <= 4000; i++) worst = Math.Max(worst, (c.PointAt(i / 100.0) - F(i / 100.0)).Length);
        Assert.True(worst < 1.05e-4, $"dense check {worst}");
    }

    [Fact]
    public void Sweep_Box_VolumeAreaAndFaces()
    {
        // straight 10 x 2 x 0.5 plate with one 0.2 hole: exact volume and area
        var sw = NurbsSweep.Plate(0, 10, _ => -1, _ => 1, _ => 0, _ => 0.5, (s, w, t) => new Vec3(1000 + s, 2000 + w, 50 + t),
            Array.Empty<double>(), 1e-6, out string why)!;
        Assert.NotNull(sw);
        var holes = new[] { new Hole(new Vec2(5, 0.3), 0.2) };
        var solid = sw.ToSolid("plate", holes);
        Assert.Equal(4 + 2 + 2, solid.Faces.Count);      // 4 ruled sides, 2 planar ends, 2 half-cylinder hole walls
        Assert.Equal(2, solid.Faces.Count(f => f.Holes.Count == 1));
        double v = 10 * 2 * 0.5 - Math.PI * 0.01 * 0.5;
        Assert.Equal(v, solid.Volume(), 6);
        double a = 2 * (10 * 2 + 10 * 0.5 + 2 * 0.5) - 2 * Math.PI * 0.01 + 2 * Math.PI * 0.1 * 0.5;
        Assert.Equal(a, solid.Area(), 5);
    }

    [Fact]
    public void CurvedPlate_NurbsVsMesh()
    {
        // flange 0.5 x 0.05 on a 200 m radius, 30 m long, rising on a 3% grade with a parabolic vertical curve
        const double R = 200;
        Vec3 Map(double s, double w, double t)
        {
            double a = s / R;
            var c = new Vec3(3000000, 1000000, 0);
            var radial = new Vec3(Math.Cos(a), Math.Sin(a), 0);
            return c + radial * (R + w) + new Vec3(0, 0, 300 + 0.03 * s - 0.0004 * s * s + t);
        }
        var sw = NurbsSweep.Plate(0, 30, _ => -0.25, _ => 0.25, _ => 0, _ => 0.05, Map, Array.Empty<double>(), 1e-4, out _)!;
        Assert.True(sw.MaxDeviation <= 1e-4);
        var solid = sw.ToSolid("girder flange");
        // exact volume of the curved plate: area x centroid path (Pappus) - grade shears only vertically, volume unchanged
        double exact = 0.5 * 0.05 * 30;
        Assert.Equal(exact, solid.Volume(), 6);
        // today's mesh: plate built on the 1.5 m chord path samples
        var mesh = new Mesh();
        var ss = Enumerable.Range(0, 21).Select(i => i * 1.5).ToList();
        PlateBuilder.AddLongPlate(mesh, 0, 30, ss, _ => -0.25, _ => 0.25, _ => 0, _ => 0.05, Array.Empty<Hole>(), (s, w, t) =>
        {
            int i = Math.Min((int)(s / 1.5), 19);
            double f = (s - ss[i]) / 1.5;
            return Vec3.Lerp(Map(ss[i], w, t), Map(ss[i + 1], w, t), f);   // chorded path like GirderPath
        });
        double meshVol = Math.Abs(mesh.Volume());
        Assert.InRange((meshVol - exact) / exact, -1e-3, 0);     // chords cut inside: smaller by ~ (1.5^2 / 12R^2)
        // vertices of today's mesh lie on the NURBS (the path samples are exact points)
        var nm = new Mesh();
        PlateBuilder.AddLongPlate(nm, 0, 30, ss, _ => -0.25, _ => 0.25, _ => 0, _ => 0.05, Array.Empty<Hole>(), sw.PlateMap);
        double dev = 0;
        for (int i = 0; i < mesh.Positions.Count; i++) dev = Math.Max(dev, (mesh.Positions[i] - nm.Positions[i]).Length);
        Assert.True(dev <= 1e-4, $"mesh vertex deviation {dev}");
        // chord sag of today's mesh vs the exact surface at mid-chord
        var mid = Vec3.Lerp(Map(0, 0.25, 0), Map(1.5, 0.25, 0), 0.5);
        double sag = (sw.PlateMap(0.75, 0.25, 0) - mid).Length;
        Assert.InRange(sag, 1e-3, 2e-3);   // 1.5^2 / (8 x 200.25) = 1.40 mm
    }

    [Fact]
    public void ChordTolerance_IsHonoured()
    {
        const double R = 150;
        Vec3 Map(double s, double w, double t) => new Vec3((R + w) * Math.Cos(s / R), (R + w) * Math.Sin(s / R), t);
        var sw = NurbsSweep.Plate(0, 40, _ => -0.3, _ => 0.3, _ => 0, _ => 0.03, Map, Array.Empty<double>(), 1e-5, out _)!;
        foreach (double chord in new[] { 1e-3, 1e-4 })
        {
            var us = sw.Samples(chord);
            foreach (var c in sw.Curves)
                for (int i = 0; i + 1 < us.Count; i++)
                    for (int k = 1; k < 10; k++)
                    {
                        double u = us[i] + (us[i + 1] - us[i]) * k / 10;
                        var a = c.PointAt(us[i]); var b = c.PointAt(us[i + 1]);
                        var q = c.PointAt(u);
                        double t = Vec3.Dot(q - a, b - a) / Vec3.Dot(b - a, b - a);
                        Assert.True((a + (b - a) * t - q).Length <= chord * 1.01);
                    }
            // and not absurdly dense: sag = L^2 / 8R  ->  L = sqrt(8 R chord)
            Assert.True(us.Count < 2.5 * 40 / Math.Sqrt(8 * R * chord) + 4);
        }
    }

    [Fact]
    public void Section_CutsCurvedPlate()
    {
        const double R = 100;
        Vec3 Map(double s, double w, double t) => new Vec3((R + w) * Math.Cos(s / R), (R + w) * Math.Sin(s / R), t);
        var solid = NurbsSweep.Plate(0, 20, _ => -0.2, _ => 0.2, _ => 0, _ => 0.04, Map, Array.Empty<double>(), 1e-6, out _)!.ToSolid("plate");
        // radial plane at s = 10
        double a = 10 / R;
        var n = new Vec3(-Math.Sin(a), Math.Cos(a), 0);
        var loops = NurbsSection.Cut(solid, Vec3.Zero, n);
        var pts = loops.SelectMany(l => l).ToList();
        Assert.NotEmpty(pts);
        foreach (var p in pts)
        {
            Assert.True(Math.Abs(Vec3.Dot(p, n)) < 1e-7);
            double r = p.XY.Length;
            Assert.InRange(r, R - 0.2 - 3e-6, R + 0.2 + 3e-6);   // fit tolerance 1e-6
            Assert.InRange(p.Z, -1e-6, 0.04 + 1e-6);
        }
        Assert.Equal(4, loops.Count);   // one segment per side face
    }

    [Fact]
    public void Setting_DefaultOff_IsNotWrittenToTheProjectFile_AndRoundTrips()
    {
        var p = SampleProject.Create();
        Assert.False(p.Geometry.ExactCurvedGeometry);
        Assert.DoesNotContain("\"Geometry\"", p.ToJson());
        p.Geometry.ExactCurvedGeometry = true;
        p.Geometry.ChordTolerance = 0.002;
        var q = BridgeProject.FromJson(p.ToJson());
        Assert.True(q.Geometry.ExactCurvedGeometry);
        Assert.Equal(0.002, q.Geometry.ChordTolerance);
        Assert.Null(GeometrySettings.Active(SampleProject.Create()));
    }

    [Fact]
    public void SampleBridge_ExactGeometry_SameMeshLayout_ExactVolumesClose()
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var off = SteelModelGenerator.Generate(f, p.Units);
        Assert.Null(off.Nurbs);
        Assert.All(off.Parts, x => Assert.Null(x.Mesh.Nurbs));
        var on = SteelModelGenerator.Generate(f, p.Units, new GeometrySettings { ExactCurvedGeometry = true });
        Assert.NotNull(on.Nurbs);
        Assert.Empty(on.Nurbs!.Fallbacks);
        var curved = on.Parts.Where(x => x.Mesh.Nurbs != null).ToList();
        Assert.Contains(curved, x => x.Category == PartCategory.FlangePlate);
        Assert.Contains(curved, x => x.Category == PartCategory.WebPlate);
        Assert.True(on.Nurbs.MaxDeviation <= 0.0254 / 256 + 1e-12);
        var offByMark = off.Parts.ToDictionary(x => x.Mark);
        foreach (var part in curved)
        {
            var o = offByMark[part.Mark];
            // default chord tolerance = today's density: same vertex / triangle layout, vertices moved by at most the fit tolerance + chord
            Assert.Equal(o.Mesh.Positions.Count, part.Mesh.Positions.Count);
            Assert.Equal(o.Mesh.TriangleCount, part.Mesh.TriangleCount);
            Assert.True(part.Mesh.HasExactNurbs);
            double exact = part.ExactVolume!.Value;
            Assert.InRange(Math.Abs(part.Volume / exact - 1), 0, 2e-3);
            Assert.InRange(Math.Abs(o.Volume / exact - 1), 0, 2e-3);
            // B-rep: 4 sides + 2 ends + 2 half-cylinder walls per hole; holes trim both t faces
            var s = part.Mesh.Nurbs![0];
            int holes = s.Faces.Sum(fc => fc.Holes.Count) / 2;
            Assert.Equal(6 + 2 * holes, s.Faces.Count);
        }
    }

    [Fact]
    public void SampleDeck_ExactGeometry()
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var off = SBM.Core.Concrete.ConcreteGenerator.Generate(f, p.Units);
        var on = SBM.Core.Concrete.ConcreteGenerator.Generate(f, p.Units, new GeometrySettings { ExactCurvedGeometry = true });
        Assert.Empty(on.Nurbs!.Fallbacks);
        Assert.NotNull(on.Deck!.Nurbs);
        double exact = on.Deck.ExactVolume()!.Value;
        Assert.InRange(Math.Abs(off.DeckVolume / exact - 1), 0, 1e-3);
        foreach (var (_, b) in on.Barriers) Assert.InRange(Math.Abs(Math.Abs(b.Volume()) / b.ExactVolume()!.Value - 1), 0, 1e-3);
        // a radial section through the deck: points on the exact top / bottom surfaces
        var a = p.Alignments[0];
        var (pt, ang) = a.EvaluateAtDistance((f.Deck!.Outline.Select(q => a.Inverse(q.XY).Distance).Min() + f.Deck.Outline.Select(q => a.Inverse(q.XY).Distance).Max()) / 2);
        var n = Vec2.FromAngle(ang);
        var cut = NurbsSection.Cut(on.Deck.Nurbs![0], pt.WithZ(0), new Vec3(n.X, n.Y, 0));
        Assert.NotEmpty(cut);
        foreach (var pnt in cut.SelectMany(c => c))
        {
            var so = a.Inverse(pnt.XY);
            double top = p.Bridges[0].Deck.FinishedElevation(a, so.Station, so.Offset);
            double dz = Math.Min(Math.Abs(pnt.Z - top), Math.Abs(pnt.Z - (top - p.Bridges[0].Deck.Thickness)));
            if (dz > 1e-3) Assert.InRange(pnt.Z, top - p.Bridges[0].Deck.Thickness - 1e-3, top + 1e-3);   // side / end faces: between
            else Assert.True(dz < 1.5e-4, $"section point {dz * 1000:0.###} mm off the exact deck surface");
        }
    }
}
