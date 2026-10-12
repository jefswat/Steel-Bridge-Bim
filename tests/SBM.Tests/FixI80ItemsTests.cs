using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Girder end dap, wingwall seated on the wing footings (plan geometry, I-80 / Ramp G items).</summary>
public class FixI80ItemsTests
{
    private static (BridgeFraming f, SteelModel m) Build(Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        tweak?.Invoke(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        Assert.False(f.HasErrors, string.Join("\n", f.Diagnostics));
        return (f, SteelModelGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void DapProfile_FollowsThePlanPolyline_WithFilletsAndCope()
    {
        var def = new GirderEndDapDefinition { CopeInset = In(6) };
        var d = new GirderEndDap(def, true, 30, Ft(1), "S");
        Assert.Null(d.Problem(In(82)));
        Assert.Equal(In(7.5), d.Drop(0), 9);                               // lowered at the end
        Assert.Equal(In(7.5), d.Drop(Ft(1) + In(10)), 9);                  // flat to the fillet (2'-2 - 2.49")
        Assert.Equal(In(3.75), d.Drop(Ft(2) + In(2) + In(3.75)), 6);       // mid-slope on the 45 deg line
        Assert.Equal(0, d.Drop(Ft(2) + In(2) + In(7.5) + In(2.5)), 9);     // normal level past the upper fillet
        Assert.Equal(Math.PI / 4, d.Angle(Ft(2) + In(2) + In(3.75)), 6);
        // cope: 10" tab at the end face (less the fillet tangent), on the 45 deg line in between, full 6" inset + fillet tangent at the recess
        Assert.Equal(In(10) - In(6) * Math.Tan(Math.PI / 8), d.CopeDepth(0), 6);
        Assert.Equal(In(13), d.CopeDepth(In(3)), 6);
        Assert.Equal(In(16) + In(6) * Math.Tan(Math.PI / 8), d.CopeDepth(In(6)), 6);
        // end of the girder: x measured from the end
        var e = new GirderEndDap(def, false, 30, Ft(1), "E");
        Assert.Equal(In(7.5), e.Drop(30), 9);
        Assert.Equal(0, e.Drop(25), 9);
    }

    [Fact]
    public void GirderEndDap_LowersFlangeAndStiffenerAtBothEnds_DefaultUnchanged()
    {
        var (f0, m0) = Build();
        var (f1, m1) = Build(b =>
        {
            b.Supports[0].GirderEndDap = new GirderEndDapDefinition { CopeInset = In(6), Source = "test" };
            b.Supports[^1].GirderEndDap = new GirderEndDapDefinition();
        });
        Assert.Contains(m1.Diagnostics, x => x.Message.Contains("Girder end dap at"));
        var g = f1.Girders[0];
        var t0 = g.Path.At(0).Item2;
        var end = g.StartEnd;
        double TopZ(SteelModel m, Vec3 at) => m.Parts.Where(p => p.Category == PartCategory.FlangePlate && p.Mark.StartsWith(g.Name + "-1-TF"))
            .SelectMany(p => p.Mesh.Positions).Where(q => (q.XY - at.XY).Length < 0.35).Max(q => q.Z);
        Assert.Equal(In(7.5), TopZ(m0, end) - TopZ(m1, end), 3);
        // web: the coped web has less area than the plain one, but keeps its full height away from the end
        double V(SteelModel m) => m.Parts.Where(p => p.Category == PartCategory.WebPlate && p.Mark.StartsWith(g.Name + "-1-")).Sum(p => Math.Abs(p.Mesh.Volume()));
        Assert.True(V(m1) < V(m0));
        // bearing stiffeners stop under the lowered flange
        double Hb(SteelModel m) => m.Parts.Where(p => p.Category == PartCategory.BearingStiffener && p.Mark.StartsWith(g.Name + "-1-"))
            .OrderBy(p => ((p.Mesh.Bounds().Min + p.Mesh.Bounds().Max) * 0.5 - end).XY.Length).First().Mesh.Bounds().Max.Z;
        Assert.Equal(In(7.5), Hb(m0) - Hb(m1), 2);
        // no dap: byte-identical meshes
        var (_, m2) = Build();
        Assert.Equal(m0.Parts.Sum(p => p.Mesh.Volume()), m2.Parts.Sum(p => p.Mesh.Volume()));
    }

    [Fact]
    public void Wingwall_BottomOnFootings_FillsDownToTheFootingTops()
    {
        double Vol(bool seat)
        {
            var doc = SbmDocument.Sample();
            var u0 = doc.Substructure().Units.First(u => u.SupportIndex == 0);
            var al = doc.Alignment();
            double Off(double s) => al.Inverse(u0.Plan(s, 0)).Offset;
            double sMin = u0.Pedestals.Min(p => p.S) - Ft(3), sMax = u0.Pedestals.Max(p => p.S) + Ft(3);
            double zSeat = u0.Pedestals.Min(p => p.SeatZ), zf = zSeat - Ft(5);
            var ad = new AbutmentDefinition { LeftEndOffset = Off(sMin), RightEndOffset = Off(sMax), PlanFootingWidth = In(80.5), BackwallFrontToBearing = In(22) };
            ad.FootingBottom.Add(new CapProfilePoint(0, zf));
            ad.BackwallTop.Add(new CapProfilePoint(sMin, zf + Ft(11))); ad.BackwallTop.Add(new CapProfilePoint(sMax, zf + Ft(11.5)));
            foreach (var p in u0.Pedestals) ad.Steps.Add(new AbutmentStepDefinition(p.Girder + 1, p.SeatZ));
            for (int i = 0; i < 5; i++) ad.PileStations.Add(sMin + Ft(4) + Ft(6.8) * i);
            // sloped bottom: 1' at the backwall, 4' high free end -> about 2.4' above the footing top over the footing
            var wd = new WingwallDefinition { Name = "L", End = AbutmentEnd.Left, GutterOffset = Off(sMin) + Ft(3), Length = Ft(20), TopAtBackwall = zf + Ft(11),
                TopAtEnd = zf + Ft(10), BottomAtBackwall = zf + Ft(1), LevelReach = Ft(1), BottomOnFootings = seat };
            wd.Counterforts.Add(new CounterfortDefinition { Start = Ft(8), Height = Ft(7), FootingTop = zf + Ft(1.5) });
            ad.Wings.Add(wd);
            var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
            sd.Abutment = ad;
            doc.Bridge().Supports[0].Substructure = sd;
            doc.Regenerate();
            var u = doc.Substructure().Units.First(x => x.SupportIndex == 0);
            var w = u.Solids.Single(s => s.Kind == SubElementKind.Wingwall);
            // seated: the wall reaches the footing top over the footing
            double minZ = w.Mesh.Bounds().Min.Z;
            Assert.True(minZ <= zf + Ft(1) + 1e-6);
            return Math.Abs(w.Mesh.Volume());
        }
        double v0 = Vol(false), v1 = Vol(true);
        Assert.True(v1 > v0 + 0.05, $"seated wall {v1:F3} vs {v0:F3} m3");
    }
}
