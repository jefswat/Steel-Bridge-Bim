using SBM.Core.Concrete;
using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>True bend radii on placed bars (<see cref="RebarBends"/>): corners become tangent arcs of pin / 2 + db / 2, sampled curves stay,
/// IFC arc fit recovers the bends, bar sheet labels every bend.</summary>
public class RebarBendsTests
{
    private static BarMark Mark(string code, string size, double pin = 0) => new()
    {
        Mark = "x", Size = RebarCatalog.Size(size), Location = "test",
        Shape = new BarShape { Code = code, Description = "", Centerline = new[] { Vec2.Zero, new Vec2(1, 0) }, PinDiameter = pin, FixedLength = 2 },
    };

    [Fact]
    public void StandardPin_Aashto()
    {
        Assert.Equal(6 * In(0.625), RebarBends.StandardPin(RebarCatalog.Size("#5"), false), 9);
        Assert.Equal(4 * In(0.625), RebarBends.StandardPin(RebarCatalog.Size("#5"), true), 9);
        Assert.Equal(6 * In(0.75), RebarBends.StandardPin(RebarCatalog.Size("#6"), true), 9);
        Assert.Equal(8 * In(1.128), RebarBends.StandardPin(RebarCatalog.Size("#9"), false), 9);
        // printed pin wins
        Assert.Equal(In(3), RebarBends.PinFor(Mark("U", "#5", In(3))), 9);
        Assert.Equal(In(1.5) + In(0.3125), RebarBends.CenterRadius(Mark("U", "#5", In(3))), 9);
    }

    [Fact]
    public void Corner_BecomesTangentArc_OfPinRadius()
    {
        double rc = In(2);
        var l = new[] { new Vec3(0, 1, 0), new Vec3(0, 0, 0), new Vec3(1, 0, 0) };
        var r = RebarBends.Round(l, rc, out var st);
        Assert.Equal(1, st.Bends);
        Assert.Equal(0, st.Reduced);
        var c = new Vec3(rc, rc, 0);
        // arc points on the circle, tangent points on the legs, ends kept
        Assert.Equal(l[0], r[0]);
        Assert.Equal(l[2], r[^1]);
        foreach (var q in r.Skip(1).Take(r.Length - 2)) Assert.Equal(rc, (q - c).Length, 9);
        Assert.Contains(r, q => (q - new Vec3(0, rc, 0)).Length < 1e-9);
        Assert.Contains(r, q => (q - new Vec3(rc, 0, 0)).Length < 1e-9);
        // ArcFit recovers one arc within 0.5 mm, length = legs - 2 rc + quarter circle
        var pieces = ArcFit.Fit(r, 0.0005);
        Assert.Single(pieces, x => x.IsArc);
        Assert.Equal(2 - 2 * rc + Math.PI / 2 * rc, ArcFit.Length(r, pieces), 6);
        // idempotent: a rounded bar is left as it is
        var again = RebarBends.Round(r, rc, out var st2);
        Assert.Equal(0, st2.Bends);
        Assert.Equal(r.Length, again.Length);
    }

    [Fact]
    public void SampledCurves_Spirals_AndTinyTurns_AreUnchanged()
    {
        // generator-rounded U bar (BarShape.Rounded at the same radius)
        double rc = In(2.5);
        var u = BarShape.Rounded(new[] { new Vec2(0, 1), Vec2.Zero, new Vec2(1, 0), new Vec2(1, 1) }, rc).Select(p => p.WithZ(0)).ToArray();
        RebarBends.Round(u, rc, out var su);
        Assert.Equal(0, su.Bends);
        // circular hoop of 32 sides, 4 ft diameter
        var hoop = Enumerable.Range(0, 33).Select(i => new Vec3(0.6 * Math.Cos(i * Math.PI / 16), 0.6 * Math.Sin(i * Math.PI / 16), 0)).ToArray();
        RebarBends.Round(hoop, rc, out var sh);
        Assert.Equal(0, sh.Bends);
        // bar following a 300 ft radius deck curve sampled every foot
        var deck = Enumerable.Range(0, 41).Select(i => new Vec3(Ft(300) * Math.Sin(i / 300.0), Ft(300) * (1 - Math.Cos(i / 300.0)), 0)).ToArray();
        RebarBends.Round(deck, rc, out var sd);
        Assert.Equal(0, sd.Bends);
        // spiral marks are skipped by Apply
        var bars = new List<BarPlacement> { new() { Mark = "x", Points = new[] { new Vec3(0, 1, 0), Vec3.Zero, new Vec3(1, 0, 0) } } };
        var sum = RebarBends.Apply(new[] { Mark("SPIRAL", "#4") }, bars);
        Assert.Equal(0, sum.Bends);
        Assert.Equal(3, bars[0].Points.Length);
    }

    [Fact]
    public void Apply_UsesMarkPin_ClosedLoops_AndShortLegs()
    {
        var mk = Mark("TIE", "#4");                                     // no printed pin: 4 db for a #4 tie
        double rc = 2 * In(0.5) + In(0.25);
        var loop = new[] { Vec3.Zero, new Vec3(0.3, 0, 0), new Vec3(0.3, 0.2, 0), new Vec3(0, 0.2, 0), Vec3.Zero };
        var bars = new List<BarPlacement> { new() { Mark = "x", Points = loop } };
        var s = RebarBends.Apply(new[] { mk }, bars);
        Assert.Equal(4, s.Bends);
        var p = bars[0].Points;
        Assert.Equal(p[0], p[^1]);                                       // still closed
        Assert.True(p.All(q => q.X >= -1e-9 && q.X <= 0.3 + 1e-9));
        Assert.Equal(new Vec3(rc, 0, 0).X, p[0].X, 9);                  // starts after the first corner's arc
        Assert.Equal(0, RebarBends.Apply(new[] { mk }, bars).Bends);   // once only
        // a 1" hook tail is shorter than the tangent length: radius reduced to fit, counted
        var hook = new[] { new Vec3(0, In(1), 0), Vec3.Zero, new Vec3(1, 0, 0) };
        RebarBends.Round(hook, In(2), out var sr);
        Assert.Equal(1, sr.Reduced);
    }

    [Fact]
    public void FitRadius_SmoothPath_KeepsHalfEndLegs()
    {
        var path = new[] { Vec3.Zero, new Vec3(2, 0, 0), new Vec3(4, 0.5, 0), new Vec3(6, 0.5, 0) };
        var r = RebarBends.Round(path, 1000, out var st, fitRadius: true);
        Assert.Equal(2, st.Bends);
        Assert.True(st.MaxDeviation < 0.1);
        // tangent-continuous: no turn above one arc step anywhere
        for (int i = 1; i + 1 < r.Length; i++)
        {
            var a = (r[i] - r[i - 1]).Normalized(); var b = (r[i + 1] - r[i]).Normalized();
            Assert.True(Math.Acos(Math.Clamp(Vec3.Dot(a, b), -1, 1)) < 16 * Math.PI / 180);
        }
        Assert.Contains(r, q => Math.Abs(q.X - 1) < 1e-6 && Math.Abs(q.Y) < 1e-6);   // first half of the end leg straight
    }

    [Fact]
    public void BarSheet_LabelsEveryBend()
    {
        double rc = In(2);
        var u = BarShape.Rounded(new[] { new Vec2(0, 1), Vec2.Zero, new Vec2(1, 0), new Vec2(1, 1) }, rc);
        var b = BarSheet.BendPoints(u, rc);
        Assert.Equal(2, b.Count);
        // labels go outside the bends: (0,0) corner -> outward toward -x,-y
        var first = b.OrderBy(x => x.at.X).First();
        Assert.True(first.outward.X < 0 && first.outward.Y < 0);
        var sharp = BarSheet.BendPoints(new[] { new Vec2(0, 1), Vec2.Zero, new Vec2(1, 0), new Vec2(1, 1), new Vec2(0.8, 1) }, rc);
        Assert.Equal(3, sharp.Count);
    }
}
