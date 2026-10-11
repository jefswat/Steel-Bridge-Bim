using SBM.Core.Bridge;
using SBM.Core.Checks;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Kinked (polyline) end-of-slab lines: <see cref="SupportDefinition.DeckEndPoints"/> and <see cref="DeckEndPolyline"/>.</summary>
public class KinkedDeckEndTests
{
    // ------------------------------------------------------------------ geometry of the polyline itself

    /// <summary>Begin line along +X (left to right), deck toward +Y; left part moved 1 m into the deck, joined by a short diagonal.</summary>
    private static DeckEndPolyline Step() => new(new List<Vec2> { new(-10, 1), new(-1, 1), new(0, 0), new(10, 0) }, atBegin: true);

    [Fact]
    public void SignedDistance_FollowsEachSegment()
    {
        var p = Step();
        Assert.True(p.IsMonotone);
        Assert.Equal(2.0, p.SignedDistance(new Vec2(-5, 3)), 9);   // left part (y = 1)
        Assert.Equal(3.0, p.SignedDistance(new Vec2(5, 3)), 9);    // right part (y = 0)
        Assert.True(p.SignedDistance(new Vec2(5, -0.1)) < 0);
        Assert.True(p.SignedDistance(new Vec2(-5, 0.5)) < 0);      // behind the moved left part
        Assert.True(p.SignedDistance(new Vec2(-0.5, 0.6)) > 0);    // just inside the diagonal
        Assert.False(new DeckEndPolyline(new List<Vec2> { new(0, 0), new(5, 0), new(3, 1) }, true).IsMonotone);
    }

    [Fact]
    public void Inset_MovesEverySegmentParallel()
    {
        var q = Step().Inset(0.5);
        Assert.Equal(4, q.Points.Count);
        Assert.Equal(1.5, q.Points[0].Y, 9);
        Assert.Equal(0.5, q.Points[^1].Y, 9);
        // the inset diagonal is 0.5 from the original diagonal
        var d = (new Vec2(0, 0) - new Vec2(-1, 1)).Normalized();
        var n = d.Left;
        Assert.Equal(0.5, Vec2.Dot(q.Points[1] - new Vec2(-1, 1), n), 9);
        Assert.Equal(0.5, Vec2.Dot(q.Points[2] - new Vec2(0, 0), n), 9);
        // every inset point is 0.5 inside the original line
        foreach (var x in new[] { -8.0, -3.0, 4.0, 9.0 }) Assert.Equal(0.5, Step().SignedDistance(new Vec2(x, q.Points[x < -1 ? 0 : 3].Y)), 9);
    }

    [Fact]
    public void Inset_DropsAVanishedMiddleSegment()
    {
        // a 0.3 m deep notch out of the deck between two parallel parts: insetting by 1 m makes the notch segments turn around
        var p = new DeckEndPolyline(new List<Vec2> { new(-10, 0), new(-0.1, 0), new(0, -0.3), new(0.1, 0), new(10, 0) }, true);
        var q = p.Inset(1);
        Assert.True(q.IsMonotone);
        Assert.True(q.Points.Count < 5);
        Assert.All(q.Points, v => Assert.Equal(1.0, v.Y, 6));
    }

    [Fact]
    public void Clip_KeepsThePartInTheDeck()
    {
        var p = Step();
        // a line normal to the end, crossing the left part (y = 1)
        var a = new Vec2(-5, -3); var b = new Vec2(-5, 8);
        Assert.True(p.Clip(ref a, ref b));
        Assert.Equal(1.0, a.Y, 9);
        Assert.Equal(8.0, b.Y, 9);
        // a line along the end, behind it on the right: only the part in the deck on the left (y = 0.5 > 0 on the right? no: right part y = 0 -> inside)
        var c = new Vec2(-9, 0.5); var e = new Vec2(9, 0.5);
        Assert.True(p.Clip(ref c, ref e));
        Assert.Equal(-0.5, c.X, 9);       // enters the deck at the diagonal (y = 0.5 at x = -0.5)
        Assert.Equal(9.0, e.X, 9);
        var f = new Vec2(-9, -1); var g = new Vec2(9, -1);
        Assert.False(p.Clip(ref f, ref g));
    }

    // ------------------------------------------------------------------ the deck of a bridge

    private static (BridgeProject p, BridgeFraming f, ConcreteModel c) Build(Action<BridgeDefinition>? tweak = null, bool rebar = false)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        if (rebar)
        {
            b.Deck.Reinforcement.Plan = new DeckPlanReinforcement { TransverseBeginClear = In(2) };
            b.Deck.Reinforcement.Plan.Transverse.Add(new DeckTransverseZone
            {
                Name = "begin", Layer = DeckBarLayer.Top, Size = "#7", Reference = DeckBarSide.Left, StartDistance = In(3), Spacing = In(7), Count = 40,
                Pieces = { new DeckLinePiece { Mark = "7a1" } },
            });
        }
        tweak?.Invoke(b);
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        Assert.False(f.HasErrors);
        return (p, f, ConcreteGenerator.Generate(f, p.Units));
    }

    /// <summary>Kinks the begin end: the part left of the alignment moves <paramref name="shift"/> into the deck (a short diagonal at the alignment).</summary>
    private static void Kink(BridgeProject p, BridgeFraming straight, BridgeDefinition b, double shift)
    {
        var a = p.Alignments[0];
        var dr = straight.Deck!;
        var o = dr.BeginLinePoint; var t = dr.BeginLineDirection; var n = t.Left;
        var pts = new[] { o - t * 30 + n * shift, o - t * 0.5 + n * shift, o, o + t * 30 };
        b.Supports[0].DeckEndPoints = pts.Select(q => { var so = a.Inverse(q); return new DeckEndPoint(so.Station, so.Offset); }).ToList();
    }

    [Fact]
    public void Outline_And_Slab_FollowTheKink()
    {
        var (p0, f0, c0) = Build();
        double shift = 0.6;
        var (p, f, c) = Build(b => Kink(p0, f0, b, shift));
        var dr = f.Deck!;
        Assert.NotNull(dr.BeginPolyline);
        Assert.Null(dr.EndPolyline);
        // the left edge starts `shift` behind... i.e. inside the straight line; the right edge is unchanged
        var left0 = f0.Deck!.Outline[0].XY; var left = dr.Outline[0].XY;
        Assert.Equal(shift, f0.Deck.FromBegin(left), 3);
        Assert.Equal(0, f0.Deck.FromBegin(left0), 6);
        // both break points between the edges are outline vertices
        foreach (var v in dr.BeginPolyline!.Points.Skip(1).Take(2))
            Assert.Contains(dr.Outline, q => Vec2.Distance(q.XY, v) < 1e-6);
        // plan area: the strip removed on the left (shift x left half width) less the small triangle at the diagonal
        var d = f.Definition.Deck;
        double halfLeft = -d.LeftEdgeOffset;
        double removed = f0.Deck.Area - dr.Area;
        double skewF = 1 / Math.Cos(15 * Math.PI / 180);           // the sample's skew: strip measured normal to the line, edge length along it
        Assert.InRange(removed, shift * (halfLeft * skewF - 0.5) * 0.98, shift * halfLeft * skewF * 1.02);
        // slab volume falls by the same plan area x slab thickness
        Assert.InRange(c0.DeckVolume - c.DeckVolume, removed * d.Thickness * 0.99, removed * d.Thickness * 1.01);
        // no point of the slab lies behind the kinked line
        Assert.All(c.Deck!.Positions, q => Assert.True(dr.FromBegin(q.XY) > -1e-3));
    }

    [Fact]
    public void TransverseBars_AreClippedByTheKinkedLine()
    {
        var (p0, f0, c0) = Build(rebar: true);
        var (_, f, c) = Build(b => Kink(p0, f0, b, 0.6), rebar: true);
        var dr = f.Deck!;
        double clear = In(2);
        foreach (var bar in c.Bars.Where(x => x.Mark == "7a1"))
            foreach (var q in bar.Points) Assert.True(dr.FromBegin(q.XY) >= clear - 1e-6, $"bar point {dr.FromBegin(q.XY):F4} m from the kinked begin line");
        // bars stop at the clear from the moved left part (well ahead of the straight line) and from the right part (on the straight line)
        var ends = c.Bars.Where(x => x.Mark == "7a1").SelectMany(x => new[] { x.Points[0].XY, x.Points[^1].XY }).Where(q => Math.Abs(dr.FromBegin(q) - clear) < 1e-3).ToList();
        Assert.Contains(ends, q => f0.Deck!.FromBegin(q) > 0.6 + clear - 1e-3);
        Assert.Contains(ends, q => Math.Abs(f0.Deck!.FromBegin(q) - clear) < 1e-3);
        Assert.Equal(c0.Bars.Count(x => x.Mark == "7a1"), c.Bars.Count(x => x.Mark == "7a1"));
        // the containment check measures from the kinked line
        var rep = DeckRebarCheck.Run(f, null, c, p0.Units)!;
        Assert.True(rep.MinBeginDistance >= clear - In(1.0 / 16));
    }

    [Fact]
    public void NoPoints_MeansTheStraightLine_AndTheInputRoundTrips()
    {
        var (_, f0, c0) = Build();
        var (_, f1, c1) = Build(b => b.Supports[0].DeckEndPoints = new List<DeckEndPoint> { new(0, 0) });   // < 2 points: ignored
        Assert.Null(f1.Deck!.BeginPolyline);
        Assert.Equal(f0.Deck!.Area, f1.Deck.Area, 9);
        Assert.Equal(c0.DeckVolume, c1.DeckVolume, 9);
        var p = SampleProject.Create();
        p.Bridges[0].Supports[^1].DeckEndPoints = new() { new(Ft(100), Ft(-20)), new(Ft(101), Ft(0)), new(Ft(100.5), Ft(20)) };
        var q = BridgeProject.FromJson(p.ToJson());
        var pts = q.Bridges[0].Supports[^1].DeckEndPoints!;
        Assert.Equal(3, pts.Count);
        Assert.Equal(Ft(101), pts[1].Station, 9);
        Assert.Null(q.Bridges[0].Supports[0].DeckEndPoints);
        Assert.NotSame(pts, q.Bridges[0].Supports[^1].Clone().DeckEndPoints);
    }
}
