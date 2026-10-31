using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Wall;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Abutment on an MSE wall (2026-10-30, generic test inputs): facing clearance, piles through the reinforced zone, pile sleeves, IFC / database.</summary>
public class MseAbutmentTests
{
    [Fact]
    public void AbutmentOnMse_Clearance_PilesInZone_Sleeves()
    {
        var d = SbmDocument.Sample();
        // the first abutment on a pile row (test input): HP piles under the cap
        var sup = d.Project.Bridges[0].Supports[0];
        var sd = SBM.Core.Bridge.SubstructureDefinition.Default(sup.Type);
        sd.Foundation = SBM.Core.Bridge.FoundationType.PileCap;
        sup.Substructure = sd;
        var bm = d.Model();
        var unit = bm.Substructure!.Units[0];
        var f = unit.Foundations.First(x => x.Piles.Count > 0);
        // wall line along the support line, 1 m in front (span side, +N) of the cap; the facing looks to the span
        double nf = unit.CapN1 + 1.0;
        var a = unit.Plan(unit.CapS0 - 5, nf); var b = unit.Plan(unit.CapS1 + 5, nf);
        var al = new Alignment(new[] { HorizontalElement.Line(a, b) }) { Name = "MSE line" };
        d.Project.Alignments.Add(al);
        var dir = (b - a) / Vec2.Distance(a, b);
        var right = new Vec2(dir.Y, -dir.X);
        double top = f.BottomZ + 0.5, pad = f.BottomZ - 6;
        var wall = new RetainingWallDefinition
        {
            Name = "MSE-A", AlignmentName = "MSE line", BeginStation = 0, EndStation = al.Length, FaceSide = Vec2.Dot(right, unit.N) > 0 ? WallFaceSide.Right : WallFaceSide.Left,
            TopOfWall = { new(0, top), new(al.Length, top) }, FootingTopSteps = new() { new(0, pad) }, Source = "test p.5",
            Mse = new MseWallDefinition
            {
                FacingThickness = In(5.5), FacingBatter = 0, LevelingPadWidth = Ft(1), LevelingPadThickness = In(6), LevelingPadFrontProjection = In(3),
                LengthsByHeight = { new() { UpToHeight = 20, Length = 8 } }, LengthFrom = MseLengthOrigin.BackOfFacing,
                Abutments = new() { new() { Bridge = bm.Definition.Name, Support = unit.Name, MinFacingClearance = 1.5, SleeveDiameter = In(24), SleeveWall = In(0.25), SleeveBottom = pad, SleeveMaterial = "CMP", Source = "test p.6" } },
            },
        };
        d.Project.Walls.Add(wall);
        var w = d.Walls().Single();
        var r = w.Interfaces.Single();
        int piles = unit.Foundations.Sum(x => x.Piles.Count(p => p.N > nf - 8 && p.N < nf));
        Assert.True(piles > 0);
        Assert.Equal(piles, r.PilesInZone);
        Assert.Equal(piles, r.Sleeves);
        // the clear distance of the abutment concrete to the back of the facing: the facing is 1 m in front of the cap, 5.5 in thick toward the span?
        Assert.InRange(r.FacingClearance, 0.5, 1.01);
        Assert.False(r.ClearanceOk);
        Assert.Contains(w.Diagnostics, x => x.Message.Contains("CONFLICT (facing clearance)"));
        // sleeves: closed rings from the foundation bottom down to the given bottom, along the pile axis
        var sl = w.Elements.Where(e => e.Role == WallElementRole.PileSleeve).ToList();
        Assert.Equal(piles, sl.Count);
        var sb = sl[0].Solids[0].Bounds();
        Assert.Equal(f.BottomZ, sb.Max.Z, 6);
        Assert.Equal(pad, sb.Min.Z, 6);
        double ri = In(12), ro = In(12.25), k = 24 / (2 * Math.PI) * Math.Sin(2 * Math.PI / 24);
        if (!unit.Foundations.SelectMany(x => x.Piles).Any(p => p.IsBattered)) Assert.Equal(k * Math.PI * (ro * ro - ri * ri) * (f.BottomZ - pad), sl[0].Volume, 6);
        Assert.Contains(w.Diagnostics, x => x.Message.Contains("sleeve tops at the foundation bottom"));
        Assert.Contains(w.Diagnostics, x => x.Message.Contains("soil reinforcements not modeled"));
        // IFC: the sleeves are building element parts of the wall
        string ifc = Path.Combine(Path.GetTempPath(), "sbm-mse-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(ifc);
            string s = File.ReadAllText(ifc);
            Assert.Equal(piles, System.Text.RegularExpressions.Regex.Matches(s, "'PILE_SLEEVE'").Count);
        }
        finally { File.Delete(ifc); }
        // no sleeve bottom: UNRESOLVED, no sleeves
        wall.Mse.Abutments[0].SleeveBottom = double.NaN;
        var w2 = d.Walls().Single();
        Assert.DoesNotContain(w2.Elements, e => e.Role == WallElementRole.PileSleeve);
        Assert.Contains(w2.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("pile sleeve bottom"));
    }
}
