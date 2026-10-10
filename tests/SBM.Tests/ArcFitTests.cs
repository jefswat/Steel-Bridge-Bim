using SBM.Core.Geometry;
using Xunit;

namespace SBM.Tests;

public class ArcFitTests
{
    private static Vec3 Helix(double r, double pitch, double t) =>
        new(r * Math.Cos(t), r * Math.Sin(t), pitch * t / (2 * Math.PI));

    [Fact]
    public void HelixStaysAPolylineThroughItsSamples()
    {
        // Arcs fitted to a helix are planar and meet with kinks; swept-disk kernels mis-build those chains, so the
        // fitter must leave a helix as straight pieces through every sample (64 per turn keeps chords within 0.5 mm).
        double r = 0.4, pitch = 0.1524;
        var pts = Enumerable.Range(0, 64 * 10 + 1).Select(i => Helix(r, pitch, 2 * Math.PI * i / 64)).ToArray();
        var pieces = ArcFit.Fit(pts, 0.0005);
        Assert.All(pieces, c => Assert.False(c.IsArc));
        Assert.Equal(pts.Length - 1, pieces.Sum(c => c.B - c.A));
        double sag = r * (1 - Math.Cos(Math.PI / 64));
        Assert.True(sag < 0.0005, $"chord sag {sag * 1000:F2} mm");
    }

    [Fact]
    public void KinkedArcsAreRejected()
    {
        // Two quarter circles in different planes meeting at a corner: each fits an arc, but the joint is kinked.
        var pts = new List<Vec3>();
        for (int i = 0; i <= 8; i++) { double t = Math.PI / 2 * i / 8; pts.Add(new Vec3(Math.Cos(t), Math.Sin(t), 0)); }
        // Second quarter circle in the y-z plane leaves the corner along +z (the first arrives along -x): a 90° kink.
        for (int i = 1; i <= 8; i++) { double t = Math.PI / 2 * i / 8; pts.Add(new Vec3(0, Math.Cos(t), Math.Sin(t))); }
        Assert.All(ArcFit.Fit(pts, 0.0005), c => Assert.False(c.IsArc));
    }

    [Fact]
    public void BentBarIsLegsPlusOneArc()
    {
        // Leg along +x, 90-degree bend of radius 0.1 sampled at 8 points, leg along +y.
        var pts = new List<Vec3> { new(-1, 0, 0) };
        for (int i = 0; i <= 8; i++) { double t = -Math.PI / 2 + Math.PI / 2 * i / 8; pts.Add(new Vec3(0.1 * Math.Cos(t), 0.1 + 0.1 * Math.Sin(t), 0)); }
        pts.Add(new Vec3(0.1, 1, 0));
        var pieces = ArcFit.Fit(pts, 0.0005);
        Assert.Equal(3, pieces.Count);
        Assert.False(pieces[0].IsArc);
        Assert.True(pieces[1].IsArc);
        Assert.False(pieces[2].IsArc);
        Assert.Equal(1 + Math.PI * 0.1 / 2 + 0.9, ArcFit.Length(pts, pieces), 6);
    }
}
