using SBM.Core.Geometry;

namespace SBM.Tests;

public class ViewNavTests
{
    private const double W = 1200, H = 800;

    private static OrthoView Oblique()
    {
        var look = new Vec3(1, 1, -0.8).Normalized();
        return new OrthoView(new Vec3(10, 20, 30) - look * 500, look, ViewNav.ZUp, 80);
    }

    private static void Near(Vec3 a, Vec3 b, double tol = 1e-6) => Assert.True((a - b).Length < tol, $"{a} vs {b}");

    [Fact]
    public void Pan_KeepsTheGrabbedPointUnderTheCursor()
    {
        var v = Oblique();
        var (from, to) = ((300.0, 200.0), (750.0, 640.0));
        var grabbed = ViewNav.ScreenToPlane(v, from.Item1, from.Item2, W, H) + v.Look * 123; // any point on the pixel ray
        var p = ViewNav.Pan(v, from, to, W, H);
        var (x, y) = ViewNav.ToScreen(p, grabbed, W, H);
        Assert.Equal(to.Item1, x, 6);
        Assert.Equal(to.Item2, y, 6);
        Assert.Equal(v.Width, p.Width);
        Near(v.Look, p.Look);
    }

    [Fact]
    public void Orbit_KeepsThePivotOnScreen_AndZUp()
    {
        var v = Oblique();
        var pivot = ViewNav.ScreenToPlane(v, 420, 310, W, H) + v.Look * 480;
        var before = ViewNav.ToScreen(v, pivot, W, H);
        var o = ViewNav.Orbit(v, pivot, 0.7, -0.3);
        var after = ViewNav.ToScreen(o, pivot, W, H);
        Assert.Equal(before.x, after.x, 6);
        Assert.Equal(before.y, after.y, 6);
        Near(o.Up, ViewNav.ZUp);
        // No roll: the camera's right axis stays horizontal.
        var (r, _) = ViewNav.Basis(o.Look, o.Up);
        Assert.Equal(0, r.Z, 9);
        // Distance to the pivot is preserved (rigid rotation).
        Assert.Equal((v.Position - pivot).Length, (o.Position - pivot).Length, 6);
    }

    [Fact]
    public void Orbit_ClampsThePitch_NeverOverThePole()
    {
        var v = Oblique();
        var pivot = new Vec3(10, 20, 30);
        var down = ViewNav.Orbit(v, pivot, 0, -10); // a huge drag down
        Assert.Equal(-ViewNav.MaxPitchDeg, ViewNav.Pitch(down.Look) * 180 / Math.PI, 6);
        var up = ViewNav.Orbit(v, pivot, 0, 10);
        Assert.Equal(ViewNav.MaxPitchDeg, ViewNav.Pitch(up.Look) * 180 / Math.PI, 6);
        // Still facing the same way horizontally (no flip).
        Assert.True(down.Look.X > 0 && down.Look.Y > 0);
        Assert.True(up.Look.X > 0 && up.Look.Y > 0);
        // Pushing further at the limit changes nothing.
        var again = ViewNav.Orbit(down, pivot, 0, -1);
        Near(again.Look, down.Look, 1e-9);
    }

    [Fact]
    public void Orbit_FromStraightDown_Works()
    {
        var v = new OrthoView(new Vec3(0, 0, 100), new Vec3(0, 0, -1), new Vec3(0, 1, 0), 50);
        var o = ViewNav.Orbit(v, Vec3.Zero, 0.2, 0.1);
        Assert.True(ViewNav.Pitch(o.Look) * 180 / Math.PI >= -ViewNav.MaxPitchDeg - 1e-9);
        Assert.False(double.IsNaN(o.Position.X));
    }

    [Fact]
    public void Orbit_YawOnly_KeepsPitch()
    {
        var v = Oblique();
        var o = ViewNav.Orbit(v, new Vec3(10, 20, 30), 1.1, 0);
        Assert.Equal(ViewNav.Pitch(v.Look), ViewNav.Pitch(o.Look), 9);
    }

    [Fact]
    public void ZoomAt_KeepsThePointUnderTheCursor_AndStepIsConsistent()
    {
        var v = Oblique();
        var p = ViewNav.ScreenToPlane(v, 900, 150, W, H) + v.Look * 300;
        var before = ViewNav.ToScreen(v, p, W, H);
        double f = ViewNav.WheelFactor(120);
        Assert.Equal(0.85, f, 9);
        var z = ViewNav.ZoomAt(v, p, f);
        var after = ViewNav.ToScreen(z, p, W, H);
        Assert.Equal(before.x, after.x, 6);
        Assert.Equal(before.y, after.y, 6);
        Assert.Equal(v.Width * 0.85, z.Width, 9);
        // In and out by one notch returns to the same width.
        Assert.Equal(v.Width, ViewNav.ZoomAt(z, p, ViewNav.WheelFactor(-120)).Width, 9);
    }

    [Fact]
    public void ZoomAt_ClampsTheWidth_AndStillKeepsThePoint()
    {
        var v = Oblique() with { Width = 0.03 };
        var p = ViewNav.ScreenToPlane(v, 100, 100, W, H);
        var z = ViewNav.ZoomAt(v, p, 0.5, minWidth: 0.02);
        Assert.Equal(0.02, z.Width, 12);
        var (x, y) = ViewNav.ToScreen(z, p, W, H);
        Assert.Equal(100, x, 6);
        Assert.Equal(100, y, 6);
    }

    [Fact]
    public void Dolly_NeverStalls_NearThePoint()
    {
        var eye = new Vec3(0, 0, 0);
        var p = new Vec3(0, 0.001, 0); // 1 mm away
        var e2 = ViewNav.DollyToward(eye, p, 0.85, 0.05);
        Assert.Equal(0.05, e2.Y, 9); // moved through the point by the minimum step
        var far = ViewNav.DollyToward(eye, new Vec3(100, 0, 0), 0.85, 0.05);
        Assert.Equal(15, far.X, 9);
        Assert.True(ViewNav.NearPlaneFor(0.3) < 0.01);
    }

    [Fact]
    public void Bindings_CadPreset()
    {
        var t = NavPresets.Cad;
        Assert.Equal(NavAction.Pan, NavPresets.Resolve(t, NavButton.Middle, NavMods.None, false));
        Assert.Equal(NavAction.Orbit, NavPresets.Resolve(t, NavButton.Middle, NavMods.Ctrl, false));
        Assert.Equal(NavAction.Orbit, NavPresets.Resolve(t, NavButton.Middle, NavMods.Shift, false));
        Assert.Equal(NavAction.Orbit, NavPresets.Resolve(t, NavButton.Right, NavMods.None, false));
        Assert.Equal(NavAction.ZoomExtents, NavPresets.Resolve(t, NavButton.Middle, NavMods.None, true));
        Assert.Equal(NavAction.Orbit, NavPresets.Resolve(t, NavButton.Middle, NavMods.Ctrl, true)); // double click falls back
        Assert.Equal(NavAction.None, NavPresets.Resolve(t, NavButton.Left, NavMods.None, false));
        Assert.Contains("Middle-drag", NavPresets.Describe(t));
    }

    [Fact]
    public void NorthArrow_Plan_Rotated_AndLookingNorth()
    {
        // plan, north up: north points straight up the screen (y down), east to the right
        var plan = new OrthoView(new Vec3(0, 0, 100), new Vec3(0, 0, -1), new Vec3(0, 1, 0), 50);
        var (n, e) = ViewNav.NorthArrow(plan);
        Assert.Equal(0, n.x, 9); Assert.Equal(-1, n.y, 9); Assert.Equal(1, e.x, 9); Assert.Equal(0, e.y, 9);
        // plan with east up the screen: north points left
        (n, _) = ViewNav.NorthArrow(plan with { Up = new Vec3(1, 0, 0) });
        Assert.Equal(-1, n.x, 9); Assert.Equal(0, n.y, 9);
        // 3D from the south looking north 30 deg down: north up the screen, foreshortened by sin 30
        double c = Math.Cos(Math.PI / 6), s = Math.Sin(Math.PI / 6);
        (n, e) = ViewNav.NorthArrow(new OrthoView(new Vec3(0, -100, 50), new Vec3(0, c, -s), ViewNav.ZUp, 50));
        Assert.Equal(0, n.x, 9); Assert.Equal(-s, n.y, 9); Assert.Equal(1, e.x, 9);
    }
}
