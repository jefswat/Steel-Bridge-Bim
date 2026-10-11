using System.Text.RegularExpressions;
using SBM.Core.Framing;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;

namespace SBM.Tests;

public class IfcTests
{
    private static string ExportSample()
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var s = SteelModelGenerator.Generate(f, p.Units);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "test.ifc", p, new[] { new IfcBridgeData(f, s) });
        return sw.ToString();
    }

    [Fact]
    public void Ifc_AllReferencesResolve_AndIdsAreUnique()
    {
        string ifc = ExportSample();
        Assert.StartsWith("ISO-10303-21;", ifc);
        Assert.Contains("FILE_SCHEMA(('IFC4X3_ADD2'));", ifc);
        var defined = new HashSet<int>();
        foreach (Match m in Regex.Matches(ifc, @"^#(\d+)=", RegexOptions.Multiline))
            Assert.True(defined.Add(int.Parse(m.Groups[1].Value)), "duplicate id " + m.Value);
        foreach (Match m in Regex.Matches(ifc, @"#(\d+)(?!=)"))
            Assert.Contains(int.Parse(m.Groups[1].Value), defined);
    }

    [Fact]
    public void Ifc_ContainsSemanticEntities()
    {
        string ifc = ExportSample();
        foreach (var e in new[] { "IFCBRIDGE(", "IFCBRIDGEPART(", "IFCELEMENTASSEMBLY(", "IFCPLATE(", "IFCMEMBER(", "IFCMECHANICALFASTENER(",
                     "IFCBEARING(", "IFCFASTENER(", "IFCALIGNMENT(", "IFCMAPCONVERSION(", ".FLANGE_PLATE.", ".STIFFENER_PLATE.", ".GUSSET_PLATE.", ".BOLT." })
            Assert.Contains(e, ifc);
    }

    [Fact]
    public void Ifc_GlobalIds_AreDeterministic()
    {
        Assert.Equal(StepWriter.Guid("abc"), StepWriter.Guid("abc"));
        Assert.Matches("^'[0-9A-Za-z_$]{22}'$", StepWriter.Guid("abc"));
        Assert.NotEqual(StepWriter.Guid("abc"), StepWriter.Guid("abd"));
    }

    [Fact]
    public void Ifc_RegeneratedModel_ExportsIdentically()
    {
        // The steel generator builds parts in parallel: stiffener numbers (part marks -> GlobalIds) and hole order must
        // not depend on thread timing. Same input, same file (apart from the FILE_NAME time stamp).
        static string Data(string ifc) => ifc[ifc.IndexOf("DATA;", StringComparison.Ordinal)..];
        var first = Data(ExportSample()).Split('\n');
        for (int i = 0; i < 3; i++)
        {
            var again = Data(ExportSample()).Split('\n');
            int k = 0;
            while (k < first.Length && k < again.Length && first[k] == again[k]) k++;
            Assert.True(k == first.Length && k == again.Length,
                $"export differs between regenerations at data line {k}:\n{(k < first.Length ? first[k] : "<end>")}\n{(k < again.Length ? again[k] : "<end>")}");
        }
    }

    [Fact]
    public void IfcZip_IsOneDeflatedModelMatchingThePlainExport()
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var s = SteelModelGenerator.Generate(f, p.Units);
        string dir = Path.Combine(Path.GetTempPath(), "sbm-ifczip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string zipPath = Path.Combine(dir, "Bridge.ifczip");
            IfcExporter.Export(zipPath, p, new[] { new IfcBridgeData(f, s) });
            using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var entry = Assert.Single(zip.Entries);
            Assert.Equal("Bridge.ifc", entry.FullName);
            Assert.True(entry.CompressedLength < entry.Length / 3, "entry should be deflated");
            using var r = new StreamReader(entry.Open());
            string inner = r.ReadToEnd();
            Assert.StartsWith("ISO-10303-21;", inner);
            Assert.EndsWith("END-ISO-10303-21;\n", inner);
            // Same data as a plain export (only the FILE_NAME header line carries a timestamp).
            var sw = new StringWriter();
            IfcExporter.Export(sw, "Bridge.ifc", p, new[] { new IfcBridgeData(f, s) });
            static string Body(string t) => t[t.IndexOf("DATA;", StringComparison.Ordinal)..];
            Assert.Equal(Body(sw.ToString()), Body(inner));
        }
        finally { Directory.Delete(dir, true); }
    }
}
