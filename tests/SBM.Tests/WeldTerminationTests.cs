using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Weld termination / hold-back rule (<see cref="WeldTermination"/>, <see cref="WeldTerminationDefinition"/>; handoff 6e, user 2026-10-19).</summary>
public class WeldTerminationTests
{
    private static WeldFinder.Bead B(Vec3 p, Vec3 q) => new(p, q, new Vec3(0, 0, 1), new Vec3(0, 1, 0));

    [Fact]
    public void HoldBack_PlanValue_RuleOverride_ElseAwsDefaultFlagged()
    {
        Assert.Equal(In(0.25), WeldTermination.HoldBack(null, In(0.25)), 9);
        Assert.Equal(In(5.0 / 16), WeldTermination.HoldBack(null, In(5.0 / 16)), 9);          // weld size, min 1/4"
        Assert.Equal(In(0.25), WeldTermination.HoldBack(null, In(3.0 / 16)), 9);
        var d = new WeldTerminationDefinition { HoldBack = In(0.375), Source = "p.282 note 6" };
        Assert.Equal(In(0.375), WeldTermination.HoldBack(d, In(5.0 / 16)), 9);
        Assert.Equal(In(0.25), WeldTermination.HoldBack(d, In(5.0 / 16), In(0.25)), 9);
        Assert.NotNull(WeldTermination.Flag(null, In(0.25), x => x.ToString()));
        Assert.Null(WeldTermination.Flag(d, In(0.25), x => x.ToString()));
        Assert.Null(WeldTermination.Flag(null, In(0.25), x => x.ToString(), In(0.25)));
    }

    [Fact]
    public void Terminate_JoinsCollinearPieces_StopsAtCornersAndDropsShortRuns()
    {
        double hb = 0.01;
        // a straight edge split in 3 pieces (tessellation) = one run of 0.3: trimmed 0.01 at both ends only
        var straight = new[] { B(new(0, 0, 0), new(0.1, 0, 0)), B(new(0.2, 0, 0), new(0.1, 0, 0)), B(new(0.2, 0, 0), new(0.3, 0, 0)) };
        var t = WeldTermination.Terminate(straight, hb);
        Assert.Equal(0.28, WeldTermination.Length(t), 9);
        Assert.Contains(t, b => (b.P0 - new Vec3(0.01, 0, 0)).Length < 1e-9 || (b.P1 - new Vec3(0.01, 0, 0)).Length < 1e-9);
        // a 90 deg corner = two runs, each trimmed at both ends
        var corner = new[] { B(new(0, 0, 0), new(0.1, 0, 0)), B(new(0.1, 0, 0), new(0.1, 0.1, 0)) };
        Assert.Equal(0.16, WeldTermination.Length(WeldTermination.Terminate(corner, hb)), 9);
        // a run shorter than 2 hold-backs vanishes
        Assert.Empty(WeldTermination.Terminate(new[] { B(new(0, 0, 0), new(0.015, 0, 0)) }, hb));
        // a closed loop (all around) is kept whole
        var loop = new[] { B(new(0, 0, 0), new(0.1, 0, 0)), B(new(0.1, 0, 0), new(0.1, 0.01, 0)), B(new(0.1, 0.01, 0), new(0, 0.01, 0)), B(new(0, 0.01, 0), new(0, 0, 0)) };
        Assert.Equal(0.22, WeldTermination.Length(WeldTermination.Terminate(loop, hb, 95)), 9);
        // hold-back 0 = unchanged
        Assert.Equal(0.3, WeldTermination.Length(WeldTermination.Terminate(straight, 0)), 9);
    }

    [Fact]
    public void AddHeld_EndsSquareAtTheHoldBack()
    {
        var m = new Mesh();
        double len = m.AddHeld(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1), 0.01, 0.02, 0.03);
        Assert.Equal(0.95, len, 9);
        var bx = m.Bounds();
        Assert.Equal(0.02, bx.Min.X, 9);
        Assert.Equal(0.97, bx.Max.X, 9);
        Assert.Equal(0, new Mesh().AddHeld(new Vec3(0, 0, 0), new Vec3(0.04, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1), 0.01, 0.02, 0.02));
    }

    private static SteelModel Build(Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        tweak?.Invoke(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        return SteelModelGenerator.Generate(f, p.Units);
    }

    [Fact]
    public void Stiffener_WeldsStopShortOfClipsAndEdges_DefaultIsFlagged_PlanValueIsNot()
    {
        var m0 = Build();
        var w0 = m0.Parts.Where(p => p.Category == PartCategory.Weld).ToList();
        Assert.NotEmpty(w0);
        Assert.All(w0, p => Assert.NotNull(p.Assumption));                                   // no plan value: AWS default, UNRESOLVED
        Assert.Contains(m0.Diagnostics, d => d.Message.Contains("hold-back"));

        var m1 = Build(b => b.Welds = new WeldTerminationDefinition { HoldBack = In(0.5), Source = "test", StiffenerWebEndFactor = 5, StiffenerWebEndMin = In(2.25) });
        var w1 = m1.Parts.Where(p => p.Category == PartCategory.Weld).ToList();
        Assert.All(w1, p => Assert.Null(p.Assumption));
        Assert.All(w1, p => Assert.Contains("held back", p.Description));
        // a bearing stiffener: plate height h, two faces x (web weld + 2 flange welds) per side; the web weld ends X = 5T (min 2 1/4") past the
        // flange-to-web weld toe at both flanges, the flange welds stop 1/2" short of the clip and of the outer edge
        var def = SampleProject.Create().Bridges[0];
        var st = m1.Parts.First(p => p.Category == PartCategory.BearingStiffener);
        var sw = m1.Parts.First(p => p.Mark == st.Mark + "-W");
        var sw0 = m0.Parts.First(p => p.Mark == st.Mark + "-W");
        Assert.True(sw.Length < sw0.Length, $"{sw.Length} vs {sw0.Length}");
        // Length = the modeled root length: per bead side, web weld h - 2 web ends, flange welds w - clip - 2 hold-backs
        var sd = def.Stiffeners;
        Assert.InRange(sw.Length, 0.1, sw0.Length);
        // flange-to-web welds held back from the field section ends
        var fw0 = m0.Parts.First(p => p.Mark.EndsWith("-FW"));
        var fw1 = m1.Parts.First(p => p.Mark == fw0.Mark);
        Assert.Equal(fw0.Length - 8 * (In(0.5) - In(5.0 / 16)), fw1.Length, 4);
    }
}
