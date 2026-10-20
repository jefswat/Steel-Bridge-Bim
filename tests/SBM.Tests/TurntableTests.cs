using SBM.Core.Geometry;
using Xunit;

namespace SBM.Tests;

public class TurntableTests
{
    [Fact]
    public void Frames_LieOnACircle_SamePitchAndDistance_LookAtCentre()
    {
        var c = new Vec3(10, -5, 3);
        var look0 = Turntable.Look(0.7, 35);
        int n = 120;
        double pitch0 = ViewNav.Pitch(look0);
        for (int i = 0; i < n; i++)
        {
            var v = Turntable.Frame(c, look0, 400, 90, i, n, 360);
            Assert.Equal(400, (v.Position - c).Length, 6);
            Assert.Equal(pitch0, ViewNav.Pitch(v.Look), 9);
            Assert.True(((c - v.Position).Normalized() - v.Look).Length < 1e-9);
            Assert.Equal(0, v.Up.X); Assert.Equal(1, v.Up.Z);
        }
        // a quarter of the frames = 90 degrees of yaw
        var a = Turntable.Frame(c, look0, 400, 90, 0, n, 360);
        var b = Turntable.Frame(c, look0, 400, 90, 30, n, 360);
        Assert.Equal(0, a.Look.X * b.Look.X + a.Look.Y * b.Look.Y, 9);
    }

    [Fact]
    public void WidthForSphere_CoversBothAxes()
    {
        Assert.Equal(2 * 50 * 1.06 * 1.78, Turntable.WidthForSphere(50, 1.78), 9);
        Assert.Equal(2 * 50 * 1.06, Turntable.WidthForSphere(50, 0.5), 9);
    }
}
