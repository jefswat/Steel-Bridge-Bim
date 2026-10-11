using SBM.Core.Geometry;
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
}
