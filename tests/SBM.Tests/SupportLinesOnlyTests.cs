using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Project;
using Xunit;

namespace SBM.Tests;

public class SupportLinesOnlyTests
{
    private static BridgeDefinition Lines(bool flag)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var d = b.Clone();
        d.Name = "Lines";
        d.Girders.Count = 0;
        d.Girders.Spacings = new();
        d.SupportLinesOnly = flag;
        return d;
    }

    [Fact]
    public void WithoutTheFlag_NoGirdersIsAnError()
    {
        Assert.Contains(Lines(false).Validate(), e => e.Contains("girder"));
        Assert.DoesNotContain(Lines(true).Validate(), e => e.Contains("girder"));
    }

    [Fact]
    public void SupportLinesOnly_BuildsSupportLinesAndNothingElse()
    {
        var p = SampleProject.Create();
        var d = Lines(true);
        var f = FramingGenerator.Generate(d, p.FindAlignment(d.AlignmentName)!);
        Assert.False(f.HasErrors);
        Assert.Equal(d.Supports.Count, f.Supports.Count);
        Assert.Empty(f.Girders);
        Assert.Null(f.Deck);
        Assert.All(f.Supports, s => Assert.Empty(s.Bearings));
    }

    [Fact]
    public void Flag_IsOmittedFromTheFileWhenFalse_AndRoundTripsWhenTrue()
    {
        var p = SampleProject.Create();
        Assert.DoesNotContain("upportLinesOnly", p.ToJson());
        var d = Lines(true);
        p.Bridges.Add(d);
        var back = BridgeProject.FromJson(p.ToJson());
        Assert.True(back.Bridges.First(b => b.Name == "Lines").SupportLinesOnly);
        Assert.False(back.Bridges[0].SupportLinesOnly);
    }
}
