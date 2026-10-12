using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>True-arc plate outlines (<see cref="ArcPolygon"/>, IFC IfcArcIndex) and the arch bracing gusset connections (p.129-140 detail type).</summary>
public class ArchBracingConnectionTests
{
    [Fact]
    public void ArcPolygon_AreaTessellationAndSplit()
    {
        // Unit square; then its top edge as a minor arc bulging outward (radius 1, chord 1).
        var pts = new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) };
        var sq = new ArcPolygon(pts);
        Assert.Equal(1, sq.SignedArea(), 12);
        // Edge 2 (1,1)->(0,1) as an arc bulging up (outward): going -x, outward = up = right of the direction -> centre left -> CCW (+).
        var a = new ArcPolygon(pts, new[] { 0, 0, 1.0, 0 });
        double seg = 0.5 * (Math.PI / 3 - Math.Sin(Math.PI / 3));     // chord 1, R 1 -> sweep 60 deg
        Assert.Equal(1 + seg, a.SignedArea(), 12);
        Assert.True(a.Mid(2).Y > 1 + 0.13);                              // sagitta 1 - cos 30 = 0.134
        Assert.Equal(-(1 + seg), a.Reversed().SignedArea(), 12);
        var poly = a.Tessellate(1e-4);
        Assert.Equal(1 + seg, Math.Abs(MeshBuilder.SignedArea(poly)), 3);
        var half = a.Sub(0, 2);                                          // (0,0) (1,0) (1,1) closed back to (0,0)
        Assert.Equal(0.5, half.SignedArea(), 12);
    }

    [Fact]
    public void PlanarArcPlate_RecordsExactOutline_AndIfcWritesArcs()
    {
        var o = new ArcPolygon(new[] { new Vec2(0, 0), new Vec2(0.3, 0), new Vec2(0.3, 0.2), new Vec2(0, 0.2) }, new[] { 0, 0.15, 0, 0 });
        var m = new Mesh();
        PlateBuilder.AddPlanarArc(m, o, new[] { new Hole(new Vec2(0.1, 0.1), 0.024) }, Frame3.World, 0, 0.0222);
        Assert.True(m.HasExactSolids);
        var e = Assert.Single(m.Solids!);
        Assert.NotNull(e.ExactOutline);
        double exact = (o.SignedArea() - Math.PI * 0.012 * 0.012) * 0.0222;
        Assert.Equal(exact, Math.Abs(m.Volume()), 5);
    }

    /// <summary>One single-leg connection at U1 of <see cref="ArchTests.Model"/> (brace BR1 U1 -> UL1).</summary>
    internal static ArchBracingConnection Connection()
    {
        double I(double x) => In(x);
        var c = new ArchBracingConnection { Name = "U1", Point = "U1", Rib = "South rib", Source = "test" };
        var dir = new Vec2(Math.Cos(-20 * Math.PI / 180), Math.Sin(-20 * Math.PI / 180));
        c.TopGusset = new ArchGussetPlate
        {
            Plate = new ArchPlateDrawing
            {
                Outline = { new ArchOutlinePoint(0, 0, I(3)), new ArchOutlinePoint(I(2), I(-4)), new ArchOutlinePoint(I(19), I(-10)), new ArchOutlinePoint(I(22), I(-3)),
                    new ArchOutlinePoint(I(16), 0, 0, true) },
                Holes = { new ArchPoint("h1", I(14), I(-5), 0), new ArchPoint("h2", I(17), I(-6.1), 0) },
            },
            Legs = { new ArchGussetLeg { EndX = I(9), EndY = I(-4), DirX = dir.X, DirY = dir.Y } },
        };
        c.BottomGusset = c.TopGusset;
        c.TopConnectionPlate = new ArchPlateDrawing { Outline = { new(I(-8), I(2)), new(I(8), I(2)), new(I(8), I(6.5)), new(I(-8), I(6.5)) },
            Holes = { new ArchPoint("a", I(-6), I(3.75), 0), new ArchPoint("b", I(6), I(3.75), 0) } };
        c.Stiffeners.Add(new ArchPlateDrawing { Outline = { new(I(-0.5), I(0.32)), new(I(0.5), I(0.32)), new(I(0.5), I(6.36)), new(I(-0.5), I(6.36)) } });
        c.WebAxisY = 0;
        return c;
    }

    [Fact]
    public void BracingConnection_GussetOnTheRolledBrace_PlatesBoltsIfc()
    {
        var (p, b) = ArchTests.Model();
        b.Arch!.BracingConnections.Add(Connection());
        var (_, m0) = ArchTests.Generate(p);
        var gus = m0.Steel.Parts.Where(q => q.Category == PartCategory.Gusset).ToList();
        Assert.Equal(2, gus.Count);
        Assert.All(gus, g => Assert.Contains(g.Mesh.Solids!, s => s.ExactOutline != null && s.ExactOutline.Radii.Any(r => r != 0)));
        // gusset volume = exact outline area x 7/8" minus the holes
        var o = new ArcPolygon(Connection().TopGusset!.Plate.Outline.Select(q => new Vec2(q.X, q.Y)).ToList(), Connection().TopGusset!.Plate.Outline.Select(q => q.Radius).ToList());
        double hole = m0.Steel.Bolt.HoleDiameter;
        Assert.Equal((Math.Abs(o.SignedArea()) - 2 * Math.PI * hole * hole / 4) * In(0.875), gus[0].Volume, 5);
        Assert.Single(m0.Steel.Parts, q => q.Category == PartCategory.ArchConnectionPlate && q.Assembly == "UPPER-BRACING-U1");
        Assert.Single(m0.Steel.Parts, q => q.Category == PartCategory.ArchStiffener);
        Assert.Equal(2 * 2 + 2, m0.Steel.Bolts.Count(q => q.Assembly == "UPPER-BRACING-U1"));
        // the brace starts at the drawn end, not at the work point
        var br = m0.Steel.Parts.First(q => q.Mark == "BR1");
        double wp = (m0.World(b.Arch.Point("UL1")!.Vec) - m0.World(b.Arch.Point("U1")!.Vec)).Length;
        Assert.True(br.Length < wp - In(6));
        Assert.Null(br.Assumption);
        // gusset angle reproduced: the welded edge lies in the connection plate face (no CHECK row about an impossible angle)
        Assert.DoesNotContain(m0.Checks, ch => ch.Item.Contains("smallest possible") && !ch.Ok);

        var bm = Assert.Single(new RegenerationCache().Regenerate(p))!;
        var sw = new StringWriter();
        IfcExporter.Export(sw, "arch.ifc", p, new[] { new IfcBridgeData(bm.Framing, bm.Steel, bm.Concrete) });
        Assert.Contains("IFCARCINDEX((", sw.ToString());
    }
}
