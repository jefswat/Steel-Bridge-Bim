using SBM.Core.Arch;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Explicit plan bar sets (Bridge/PlanBarSet + Concrete/PlanBarSetBuilder) on substructure units and decks, the generic mesh-union clear-cover
/// test (Concrete/RebarCover), plan spirals, and the arch deck bill options (two hooked A1 per row, D3 edge bars, bill row counts).</summary>
public class PlanBarSetTests
{
    private static Mesh Box(double x0, double x1, double y0, double y1, double z0, double z1)
    {
        var m = new Mesh();
        MeshBuilder.AddBox(m, Frame3.World, x0, x1, y0, y1, z0, z1);
        return m;
    }

    [Fact]
    public void Cover_UnionOfSolids_SharedFaceIsNotAFreeFace()
    {
        // column 1 m square under a 3 m cap: a bar 2" above the cap soffit over the column is covered by the column below
        var cap = Box(-1.5, 1.5, -0.5, 0.5, 0, 1);
        var col = Box(-0.5, 0.5, -0.5, 0.5, -3, 0);
        var rc = new RebarCover(new[] { cap, col });
        var (s1, _) = rc.Slack(new Vec3(0, 0, In(2)), In(0.25), In(2), double.NaN, double.NaN);
        Assert.True(s1 >= 0, $"over the column the soffit is not a free face: slack {s1}");
        // the same height outside the column footprint: 2" to the soffit - (2" + radius) = -radius
        var (s2, f2) = rc.Slack(new Vec3(1.0, 0, In(2)), In(0.25), In(2), double.NaN, double.NaN);
        Assert.Equal(-In(0.25), s2, In(0.01));
        Assert.Equal("bottom face", f2);
        // a separate bottom cover applies at faces looking down
        var (s3, _) = rc.Slack(new Vec3(1.0, 0, In(4)), In(0.25), In(2), In(3.5), double.NaN);
        Assert.Equal(In(4) - In(3.75), s3, In(0.01));
        // outside the concrete
        var (s4, f4) = rc.Slack(new Vec3(3, 0, 0.5), 0, In(2), double.NaN, double.NaN);
        Assert.True(s4 < -In(2));
        Assert.Equal("outside the concrete", f4);
    }

    [Fact]
    public void Cover_AnalyticHost_IsExact()
    {
        var rc = new RebarCover(Array.Empty<Mesh>());
        rc.AddAnalytic(p => (Math.Min(1 - Math.Abs(p.X), p.Z), Math.Abs(p.X) < 1 - p.Z ? new Vec3(0, 0, -1) : new Vec3(1, 0, 0)));
        var (s, f) = rc.Slack(new Vec3(0, 0, In(3)), In(0.5), In(2), In(2.5), double.NaN);
        Assert.Equal(0, s, 1e-9);
        Assert.Equal("bottom face", f);
    }

    [Fact]
    public void Spiral_HasFlatTurnsAndThePitch()
    {
        var sp = new PlanSpiral { Radius = 0.5, TopZ = 10, BottomZ = 7, Pitch = 0.25, FlatTurns = 1.5 };
        var pts = sp.Points();
        Assert.Equal(10, pts[0].Z, 9);
        Assert.Equal(7, pts[^1].Z, 9);
        // 1.5 flat turns at both ends: the first 36 samples (24 per turn) stay at the top
        Assert.All(pts.Take(37), p => Assert.Equal(10, p.Z, 9));
        Assert.All(pts, p => Assert.Equal(0.5, new Vec2(p.X, p.Y).Length, 6));
        double len = 0; for (int i = 1; i < pts.Count; i++) len += (pts[i] - pts[i - 1]).Length;
        double turns = 3 + 3 / 0.25;
        Assert.InRange(len, turns * 2 * Math.PI * 0.5 * 0.99, turns * 2 * Math.PI * 0.5 * 1.01);
    }

    [Fact]
    public void Substructure_PlanSets_ReplaceTheGenericBars_CountUnplaced_AndReportCover()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var pier = b.Supports[1];
        var first = new RegenerationCache().Regenerate(p)[0]!.Substructure!.Units.First(u => u.Name == pier.Name);
        double s0 = first.CapS0, s1 = first.CapS1, top = first.CapTopAt((s0 + s1) / 2), nc = (first.CapN0 + first.CapN1) / 2;
        double sm = (s0 + s1) / 2;
        var good = new PlanBarSet { Mark = "A", Size = "#11", Location = "test cap top bars", ListedLength = 1.0, Cover = In(2), PlanCount = 3, Unplaced = 1,
            Unresolved = "UNRESOLVED (p.0): test" };
        double zt = top - In(2) - In(1.41) / 2 - In(0.5);
        good.Add(new[] { new Vec3(sm - 0.5, nc, zt), new Vec3(sm + 0.5, nc, zt) });
        good.Add(new[] { new Vec3(sm - 0.5, nc + In(6), zt), new Vec3(sm + 0.5, nc + In(6), zt) });
        var bad = new PlanBarSet { Mark = "X", Size = "#5", Location = "test bar too high", Cover = In(2) };
        bad.Add(new[] { new Vec3((s0 + s1) / 2 - 0.5, nc, top - In(1)), new Vec3((s0 + s1) / 2 + 0.5, nc, top - In(1)) });
        var sp = new PlanBarSet { Mark = "SP", Size = "#4", Location = "test spiral", Cover = In(1) };
        var col = first.Columns[0];
        sp.Spirals.Add(new PlanSpiral { X = col.S, Y = col.N, Radius = Math.Min(col.SizeS, col.SizeN) / 2 - In(3), TopZ = col.TopZ - In(3), BottomZ = col.BottomZ + In(3), Pitch = In(6) });
        pier.Substructure = first.Definition;
        pier.Substructure.Reinforcement.Enabled = true;
        pier.Substructure.Reinforcement.PlanBarSets = new() { good, bad, sp };
        var sub = new RegenerationCache().Regenerate(p)[0]!.Substructure!;
        var unit = sub.Units.First(u => u.Name == pier.Name);
        var marks = sub.Marks.Where(m => m.Unit == pier.Name).ToList();
        Assert.Equal(new[] { "A", "X", "SP" }, marks.Select(m => m.Mark));                      // only the plan marks: generic bars bypassed
        Assert.Equal(3, marks[0].Count);                                                       // 2 placed + 1 schedule-only
        Assert.Equal(2, sub.Bars.Count(x => x.Unit == pier.Name && x.Mark == "A"));
        Assert.Equal(good.ListedLength, marks[0].Length, 9);                                   // scheduled = listed
        Assert.Equal("SPIRAL", marks[2].Shape.Code);
        Assert.Contains(sub.Diagnostics, d => d.Message.Contains("UNRESOLVED (p.0): test"));
        var v = Assert.Single(unit.CoverViolations);
        Assert.Equal("X", v.Kind);
        Assert.True(v.Excess >= In(1.25) - In(0.01), $"excess {v.Excess / In(1)}");          // 1" (or less on a sloped cap top) to the top vs 2" + bar radius
        // the definition round-trips through the project file
        var p2 = BridgeProject.FromJson(p.ToJson());
        var sets2 = p2.Bridges[0].Supports[1].Substructure!.Reinforcement.PlanBarSets;
        Assert.Equal(3, sets2.Count);
        Assert.Equal(good.Bars[0], sets2[0].Bars[0]);
        Assert.Single(sets2[2].Spirals);
    }

    [Fact]
    public void Deck_PlanSets_ReplaceTheGenericLayout_AndAreCheckedAgainstTheSlab()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var al = p.Alignments[0];
        double sta = b.Supports[0].Station + Ft(40);
        var d = b.Deck;
        d.Barrier.Reinforcement = false;
        Vec3 At(double off, double below) { var q = al.PointAt(sta, off); return q.WithZ(d.FinishedElevation(al, sta, off) - below); }
        var set = new PlanBarSet { Mark = "A", Size = "#5", Location = "test transverse", Cover = In(2), CoverTop = In(2.5) };
        set.Add(new[] { At(d.LeftEdgeOffset + In(2), In(3)), At(0, In(3)), At(d.RightEdgeOffset - In(2), In(3)) });
        var high = new PlanBarSet { Mark = "B", Size = "#5", Location = "test too high", Cover = In(2), CoverTop = In(2.5) };
        high.Add(new[] { At(-1, In(1)), At(1, In(1)) });
        d.Reinforcement.PlanBarSets = new() { set, high };
        var c = new RegenerationCache().Regenerate(p)[0]!.Concrete!;
        Assert.Equal(new[] { "A", "B" }, c.Marks.Select(m => m.Mark).OrderBy(x => x));
        var v = Assert.Single(c.CoverViolations);
        Assert.Equal("B", v.Mark);
        Assert.Equal("top face", v.Face);
        Assert.InRange(v.Excess, In(1.8125) - In(0.02), In(1.8125) + In(0.02));
    }

    [Fact]
    public void ArchDeck_BillOptions_TwoHookedTopBarsPerRow_EdgeBars_RowCounts()
    {
        var (p, b) = ArchTests.Model();
        var dk = b.Arch!.Deck!;
        dk.Rebar = new ArchDeckRebar
        {
            TopTransLength = Ft(13) + In(10), BottomTransLength = Ft(18) + In(2), TransCount = 120, CurbStirrupCount = 100, CurbStirrupHook = In(4.5),
            TopTransPerRow = 2, TopTransHookOffset = In(7.5), EdgeBarMark = "D3", EdgeBarLength = Ft(5) + In(9), EdgeBarHook = In(5),
        };
        var bm = Assert.Single(new RegenerationCache().Regenerate(p))!;
        var m = bm.Steel!.Arch!;
        var marks = m.Concrete.Marks.ToDictionary(q => q.Mark);
        Assert.Equal(240, marks["A1"].Count);
        Assert.Equal(120, marks["B1"].Count);
        Assert.Equal(Ft(13) + In(10), marks["A1"].Length, 9);                 // scheduled at the listed length
        Assert.Equal(Ft(5) + In(9) + In(20), marks["D3"].Length, 9);          // 5'-9 + 2 x (5 + 5)
        int panels = m.Floorbeams.Count + 1;
        Assert.Equal(2 * panels, marks["D3"].Count);
        if (marks.TryGetValue("S1", out var s1)) Assert.Equal(2 * (In(6) + In(9)) + 2 * In(4.5), s1.Length, 9);
        Assert.Contains(m.Checks, c => c.Item.Contains("two hooked bars per row"));
        // every A1 bar: hooked end on the bottom bar level, developed length within 1" of the listed length
        foreach (var bar in m.Concrete.Bars.Where(x => x.Mark == "A1").Take(20))
        {
            double l = 0; for (int i = 1; i < bar.Points.Length; i++) l += (bar.Points[i] - bar.Points[i - 1]).Length;
            Assert.InRange(l, Ft(13) + In(9), Ft(13) + In(11));
            Assert.True(bar.Points[^1].Z < bar.Points[0].Z - In(3));
        }
    }
}
