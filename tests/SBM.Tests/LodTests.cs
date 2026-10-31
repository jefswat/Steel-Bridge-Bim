using SBM.Api;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;

namespace SBM.Tests;

/// <summary>Level of detail (<see cref="LodSettings"/>, 2026-10-28): optional, part of the regeneration key, applied after generation.</summary>
public class LodTests
{
    private static SbmDocument Sample(LodSettings? lod)
    {
        var d = SbmDocument.Sample();
        d.Project.Lod = lod;
        d.Regenerate();
        return d;
    }

    [Fact]
    public void Unset_IsOmittedFromTheFile_And_400IsTheFullModel()
    {
        var full = Sample(null);
        Assert.DoesNotContain("\"Lod\"", full.Project.ToJson());
        Assert.Equal("", new LodSettings().KeyFor("any"));
        var at400 = Sample(new LodSettings { Default = 400 });
        Assert.Equal(full.Steel().Parts.Count, at400.Steel().Parts.Count);
        Assert.Equal(full.Steel().Bolts.Count, at400.Steel().Bolts.Count);
        Assert.Equal(full.Concrete().Bars.Count, at400.Concrete().Bars.Count);
        Assert.Equal(full.Steel().SteelMass, at400.Steel().SteelMass, 9);
    }

    [Fact]
    public void Lod300_NoFastenersWeldsBarsOrHoles_MarksKept()
    {
        var full = Sample(null);
        var d = Sample(new LodSettings { Default = 300 });
        var st = d.Steel();
        Assert.True(full.Steel().Bolts.Count > 0 && full.Concrete().Bars.Count > 0);
        Assert.Empty(st.Bolts);
        Assert.True(full.Steel().Studs.Count > 0);
        Assert.Empty(st.Studs);                                                         // shear studs are fasteners too
        Assert.DoesNotContain(st.Parts, p => p.Category == PartCategory.Weld);
        Assert.Empty(d.Concrete().Bars);
        Assert.Equal(full.Concrete().Marks.Count, d.Concrete().Marks.Count);           // the bar schedule stays
        Assert.Contains(st.Parts, p => LodApply.IsConnectionPart(p.Category));          // connection parts stay at 300
        Assert.True(st.Parts.Sum(p => p.Mesh.TriangleCount) < full.Steel().Parts.Sum(p => p.Mesh.TriangleCount), "holes filled");
        Assert.Contains(st.Diagnostics, x => x.Message.StartsWith("Level of detail"));
        // 350: holes back
        var d350 = Sample(new LodSettings { Default = 350 });
        Assert.Equal(full.Steel().Parts.Where(p => p.Category != PartCategory.Weld).Sum(p => p.Mesh.TriangleCount), d350.Steel().Parts.Sum(p => p.Mesh.TriangleCount));
    }

    [Fact]
    public void Lod200_NoConnectionParts_Lod100_OneEnvelopePerAssembly_SameWeight()
    {
        var d200 = Sample(new LodSettings { Default = 200 });
        Assert.DoesNotContain(d200.Steel().Parts, p => LodApply.IsConnectionPart(p.Category));
        var d100 = Sample(new LodSettings { Default = 100 });
        var st = d100.Steel();
        Assert.Equal(st.Assemblies.Count, st.Parts.Count);
        Assert.All(st.Parts, p => Assert.EndsWith("-ENV", p.Mark));
        Assert.Equal(d200.Steel().Parts.Sum(p => p.Mass), st.Parts.Sum(p => p.Mass), 6);
    }

    [Fact]
    public void Groups_Overrides_Parse_RoundTrip()
    {
        var lod = SbmDocument.ParseLod("300,Fasteners=400");
        Assert.Equal(300, lod.Default);
        Assert.Equal(400, lod.LevelFor("B", LodGroup.Fasteners));
        Assert.Equal(300, lod.LevelFor("B", LodGroup.Welds));
        lod.Overrides = new() { new LodOverride { Bridge = "Main*", Level = 400 } };
        Assert.Equal(400, lod.LevelFor("Main span", LodGroup.Welds));
        Assert.Equal(300, lod.LevelFor("Approach", LodGroup.Welds));
        Assert.Equal(300, LodSettings.Snap(349));
        Assert.Equal(100, LodSettings.Snap(10));
        var d = SbmDocument.Sample();
        d.Project.Lod = lod;
        var back = BridgeProject.FromJson(d.Project.ToJson());
        Assert.NotNull(back.Lod);
        Assert.Equal(400, back.Lod!.LevelFor("Main span", LodGroup.Steel));
        Assert.Equal(300, back.Lod.LevelFor("Approach", LodGroup.Steel));
        // fasteners kept at 400, welds removed
        d.Regenerate();
        var st = d.Steel();
        Assert.NotEmpty(st.Bolts);
        Assert.DoesNotContain(st.Parts, p => p.Category == PartCategory.Weld);
    }

    [Fact]
    public void OutputCap_IfcAt300_ModelStays400_PerElementPset()
    {
        var d = Sample(new LodSettings { Outputs = new() { [LodOutput.Ifc] = 300 } });
        Assert.True(d.Steel().Bolts.Count > 0);                      // the project levels (viewport / model) are 400
        Assert.Contains("\"Outputs\"", d.Project.ToJson());
        Assert.Equal(300, d.Project.Lod!.ForOutput(LodOutput.Ifc).LevelFor("x", LodGroup.Fasteners));
        Assert.Same(d.Project.Lod, d.Project.Lod.ForOutput(LodOutput.Rhino));   // no cap: the same settings
        string path = Path.Combine(Path.GetTempPath(), "sbm-lodcap-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(path);
            string ifc = File.ReadAllText(path);
            Assert.DoesNotContain("IFCMECHANICALFASTENERTYPE(", ifc);   // no bolts at 300
            Assert.Contains("'SBM_LOD'", ifc);
            Assert.Contains("IFCLABEL('Steel')", ifc);
            Assert.Contains("'Level',$,IFCINTEGER(300)", ifc);
            Assert.True(d.Steel().Bolts.Count > 0);                  // the model after the export is back at the project levels
        }
        finally { File.Delete(path); }
    }
}
