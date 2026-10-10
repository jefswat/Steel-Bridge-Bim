using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Girders that begin on an auxiliary support line (e.g. I-80 WB girders A-E on Pier 6A beside Pier 5WB).</summary>
public class GirderBeginningTests
{
    private static SbmDocument Doc(out BridgeDefinition b)
    {
        var doc = SbmDocument.Sample();
        b = doc.Bridge();
        b.FieldSplices.Clear();
        // G1 and G2 begin on a pier line 3 ft past the first support, skewed 4 deg.
        b.AuxiliarySupports.Add(new SupportDefinition
        {
            Name = "Pier 0A", Type = SupportType.Pier, Station = b.Supports[0].Station + Ft(3), SkewDegrees = -4, GirderEndExtension = In(12),
        });
        b.Girders.Beginnings.Add(new GirderBeginning { Girder = 0, Support = "Pier 0A" });
        b.Girders.Beginnings.Add(new GirderBeginning { Girder = 1, Support = "Pier 0A" });
        return doc;
    }

    [Fact]
    public void BegunGirdersBearOnTheAuxiliaryLineOnly()
    {
        var doc = Doc(out var b);
        var r = doc.Regenerate();
        Assert.Empty(r.Errors);
        var m = doc.Model(b.Name);
        var f = m.Framing;
        Assert.Null(f.Supports[0].Bearings[0]);
        Assert.Null(f.Supports[0].Bearings[1]);
        Assert.NotNull(f.Supports[0].Bearings[2]);
        var aux = Assert.Single(f.AuxSupports);
        Assert.NotNull(aux.Bearings[0]);
        Assert.NotNull(aux.Bearings[1]);
        Assert.Null(aux.Bearings[2]);
        // The girder path starts at 0 again, one end extension before its start bearing.
        Assert.Equal(In(12), aux.Bearings[0]!.PathDistance, 6);
        Assert.Equal(0, f.Girders[0].Path.S[0], 6);
        Assert.True(f.Girders[0].Length < f.Girders[2].Length);
        // A support cross frame between G1 and G2 on the auxiliary line; none between G2 and G3 there.
        Assert.Contains(f.CrossFrames, c => c.AtSupport && c.SupportIndex == aux.Index && c.Bay == 0 && c.RightGirder == 1);
        Assert.DoesNotContain(f.CrossFrames, c => c.AtSupport && c.SupportIndex == 0 && (c.Bay <= 1));
        // Steel: bearings on Pier 0A for G1/G2, bearing stiffeners there; substructure: a unit for Pier 0A.
        Assert.Contains(m.Steel!.Assemblies, a => a.Kind == AssemblyKind.Bearing && a.Mark == "B-Pier0A-G1");
        Assert.DoesNotContain(m.Steel.Assemblies, a => a.Mark == $"B-{b.Supports[0].Name.Replace(" ", "")}-G1");
        Assert.Contains(m.Substructure!.Units, u => u.Name == "Pier 0A");
        // Every bearing has an elevation chain (auxiliary bearings included) and none mismatches.
        var chains = SBM.Core.Checks.ElevationCheck.AllBearings(f, m.Steel, doc.Units, m.Substructure);
        Assert.Contains(chains, c => c.Title.StartsWith("Pier 0A"));
        Assert.DoesNotContain(chains, c => c.Mismatch);
    }

    [Fact]
    public void BeginningsSurviveSaveAndClone()
    {
        Doc(out var b);
        var c = b.Clone();
        Assert.Single(c.AuxiliarySupports);
        Assert.Equal(2, c.Girders.Beginnings.Count);
        var p = new SBM.Core.Project.BridgeProject();
        p.Bridges.Add(c);
        var back = SBM.Core.Project.BridgeProject.FromJson(p.ToJson());
        Assert.Equal("Pier 0A", back.Bridges[0].Girders.Beginnings[1].Support);
        Assert.Equal(-4, back.Bridges[0].AuxiliarySupports[0].SkewDegrees, 9);
    }
}
