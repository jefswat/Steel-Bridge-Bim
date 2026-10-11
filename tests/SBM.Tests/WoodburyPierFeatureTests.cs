using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Woodbury pier features: parallelogram plan pedestal blocks, column reveal projections and cap blocks (maskwall / end reveal).</summary>
public class WoodburyPierFeatureTests
{
    private static (SbmDocument doc, SubstructureDefinition sd, SubstructureUnit u0) Setup()
    {
        var doc = SbmDocument.Sample();
        var u0 = doc.Substructure().Units.Single(u => u.Type == SupportType.Pier);
        double s0 = u0.CapS0 - Ft(1), s1 = u0.CapS1 + Ft(1), ztop = u0.Pedestals.Min(p => p.SeatZ) - In(6), zb = ztop - Ft(5);
        var sd = SubstructureDefinition.Default(SupportType.Pier);
        sd.CapProfile = new() { new(s0, ztop), new(s1, ztop), new(s1, zb), new(s0, zb) };
        sd.CapWidth = Ft(4.5);
        sd.MinPedestalHeight = 0;
        return (doc, sd, u0);
    }

    [Fact]
    public void ParallelogramBlocks_KeepTheirPlanSizeAndAreaAndShear()
    {
        var (doc, sd, _) = Setup();
        sd.PedestalSizeAlong = Ft(3) + In(8); sd.PedestalSizeAcross = Ft(4) + In(1.625); sd.PedestalShared = true;
        sd.PedestalSideAngleDeg = -11.5;
        doc.Bridge().Supports.First(s => s.Type == SupportType.Pier).Substructure = sd;
        doc.Regenerate();
        var u = doc.Substructure().Units.Single(x => x.Type == SupportType.Pier);
        var ped = u.Solids.Where(s => s.Kind == SubElementKind.Pedestal).ToList();
        Assert.NotEmpty(ped);
        Assert.Contains("parallelogram", ped[0].Description);
        // a sheared prism has the area of the rectangle: volume / height ~ along x across (blocks on a level cap top here)
        var b = u.PedestalBlocks[0];
        Assert.Equal(sd.PedestalSizeAlong * sd.PedestalSizeAcross * b.Height, Math.Abs(ped[0].Mesh.Volume()), 4);
        var bx = ped[0].Mesh.Positions;
        // plan extent along S is the size plus the shear over the width
        double ext = bx.Max(p => Vec(p, u)) - bx.Min(p => Vec(p, u));
        Assert.Equal(sd.PedestalSizeAlong + sd.PedestalSizeAcross * Math.Tan(11.5 * Math.PI / 180), ext, 3);
    }

    private static double Vec(SBM.Core.Geometry.Vec3 p, SubstructureUnit u) => SBM.Core.Geometry.Vec2.Dot(new SBM.Core.Geometry.Vec2(p.X, p.Y) - u.Origin, u.D);

    [Fact]
    public void ColumnProjectionsAndCapBlocks_AreBuilt()
    {
        var (doc, sd, u0) = Setup();
        sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Ft(5) + In(3.625); sd.ColumnDepth = Ft(4) + In(3.625);
        sd.ColumnProjectionS = In(4); sd.ColumnProjectionWidth = Ft(2) + In(11.625);
        double s1 = sd.CapProfile.Max(p => p.S), ztop = sd.CapProfile.Max(p => p.Elevation);
        double hw = sd.CapWidth / 2;
        sd.CapBlocks = new()
        {
            new CapBlockDefinition { Name = "maskwall", Kind = CapBlockKind.Maskwall, TopElevation = ztop + Ft(5), TopRefS = s1,
                Outline = { new(s1 - In(18.5), hw), new(s1, hw), new(s1, -hw), new(s1 - In(8), -hw) } },
            new CapBlockDefinition { Name = "end reveal", Kind = CapBlockKind.Cap, Base = CapBlockBase.CapBottom, TopElevation = ztop + Ft(5), TopRefS = s1,
                Outline = { new(s1, -Ft(1.5)), new(s1 + In(4), -Ft(1.5)), new(s1 + In(4), Ft(1.5)), new(s1, Ft(1.5)) } },
            new CapBlockDefinition { Name = "no top", Outline = { new(s1 - 1, 0), new(s1, 0), new(s1, 0.5) } },
        };
        doc.Bridge().Supports.First(s => s.Type == SupportType.Pier).Substructure = sd;
        doc.Regenerate();
        var m = doc.Substructure();
        var u = m.Units.Single(x => x.Type == SupportType.Pier);
        var col = u.Solids.First(s => s.Kind == SubElementKind.Column);
        Assert.Contains("projections", col.Description);
        double h = u.Columns[0].TopZ - u.Columns[0].BottomZ;
        double area = sd.ColumnWidth * sd.ColumnDepth + 2 * sd.ColumnProjectionS * sd.ColumnProjectionWidth;
        Assert.Equal(area * h, Math.Abs(col.Mesh.Volume()), 2);
        var mask = u.Solids.Single(s => s.Kind == SubElementKind.Maskwall);
        Assert.Equal((In(18.5) + In(8)) / 2 * sd.CapWidth * Ft(5), Math.Abs(mask.Mesh.Volume()), 3);
        Assert.Contains(u.Solids, s => s.Name.EndsWith("end reveal"));
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("no top"));
        var rt = doc.Project.DeepClone().Bridges[0].Supports.First(s => s.Type == SupportType.Pier).Substructure!;
        Assert.Equal(3, rt.CapBlocks.Count);
        Assert.Equal(sd.ColumnProjectionS, rt.ColumnProjectionS, 9);
    }
}
