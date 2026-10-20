using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Database;
using SBM.IO.Ifc;
using Xunit;

namespace SBM.Tests;

/// <summary>CHECKING export (IfcExportOptions.ForCheck, sbm db --for-check): the originator's interpretation is stripped, the model is not touched.</summary>
public class CheckExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sbmchk_" + Guid.NewGuid().ToString("N"));

    public CheckExportTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private static string Export(IfcExportOptions? options, bool fabrication = true)
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var s = SteelModelGenerator.Generate(f, p.Units);
        if (fabrication)
            foreach (var part in s.Parts.Take(5))
                part.Fabrication = new FabricationAttributes { Toughness = "CVN zone 2", Notes = { "QZ nine p.29" } };
        var sw = new StringWriter();
        IfcExporter.Export(sw, "test.ifc", p, new[] { new IfcBridgeData(f, s) }, options);
        string ifc = sw.ToString();
        return ifc[ifc.IndexOf("DATA;", StringComparison.Ordinal)..];
    }

    private static Dictionary<int, string> Lines(string data)
    {
        var d = new Dictionary<int, string>();
        foreach (Match m in Regex.Matches(data, @"^#(\d+)=(.*)$", RegexOptions.Multiline)) d[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
        return d;
    }

    /// <summary>The entity with every string and every unset value ($) replaced by one placeholder: what is left must be equal.</summary>
    private static string Blank(string line) => Regex.Replace(Regex.Replace(line, @"'(?:[^']|'')*'", "''"), @"(?<=[(,])\$(?=[,)])", "''");

    private static IEnumerable<string> Strings(string line) =>
        Regex.Matches(line, @"'((?:[^']|'')*)'").Select(m => m.Groups[1].Value).Where(s => !(s.Length == 22 && !s.Contains(' ')));

    [Fact]
    public void NormalExport_IsUnchangedByTheOption()
    {
        Assert.False(new IfcExportOptions().ForCheck);
        Assert.Equal(Export(null), Export(new IfcExportOptions(ForCheck: false)));
    }

    [Fact]
    public void ForCheck_StripsText_KeepsEveryGeometryEntityAndId()
    {
        var normal = Lines(Export(null));
        var chk = Lines(Export(new IfcExportOptions(ForCheck: true)));
        // Sanity: the normal export does carry what the check export must not.
        Assert.Contains(normal.Values.SelectMany(Strings), s => IfcCheckFilter.IsLeak(s));
        Assert.Contains(normal.Values, l => l.Contains("'PlanNotes'") && l.Contains("QZ nine p.29"));
        Assert.DoesNotContain(chk.Values.SelectMany(Strings), s => IfcCheckFilter.IsLeak(s));
        Assert.DoesNotContain(chk.Values, l => l.Contains("PlanNotes") || l.Contains("QZ nine") || l.Contains("'Size'") || l.Contains("'Toughness'"));
        // Same ids; only root entities (names / descriptions), property values / sets / relationships differ or vanish.
        var allowed = new HashSet<string> { "IFCPROPERTYSINGLEVALUE", "IFCPROPERTYSET", "IFCRELDEFINESBYPROPERTIES" };
        int changedRoots = 0;
        foreach (var (id, line) in normal)
        {
            string type = line[..line.IndexOf('(')];
            if (!chk.TryGetValue(id, out var other)) { Assert.Contains(type, allowed); continue; }
            if (other == line) continue;
            Assert.True(allowed.Contains(type) || line.StartsWith(type + "('") || type == "IFCMATERIAL" || type == "IFCPROJECTEDCRS", $"{type} changed: {line} -> {other}");
            changedRoots++;
            // Everything but the strings is the same: placements, representations, predefined types, GlobalIds.
            if (!allowed.Contains(type) && !line.StartsWith("IFCMATERIAL"))
                Assert.Equal(Blank(line), Blank(other));
        }
        Assert.True(changedRoots > 0);
        Assert.Empty(chk.Keys.Except(normal.Keys));
        // Geometry entities are byte-identical.
        foreach (var t in new[] { "IFCCARTESIANPOINT", "IFCLOCALPLACEMENT", "IFCEXTRUDEDAREASOLID", "IFCTRIANGULATEDFACESET", "IFCSHAPEREPRESENTATION", "IFCPRODUCTDEFINITIONSHAPE" })
            Assert.Equal(normal.Values.Where(l => l.StartsWith(t + "(")).ToList(), chk.Values.Where(l => l.StartsWith(t + "(")).ToList());
    }

    [Fact]
    public void ForCheck_KeepDesignations_KeepsClaimsButNeverNotes()
    {
        var chk = Lines(Export(new IfcExportOptions(ForCheck: true, KeepDesignations: true))).Values.ToList();
        Assert.Contains(chk, l => l.Contains("'Toughness'") && l.Contains("CVN zone 2"));
        Assert.DoesNotContain(chk, l => l.Contains("PlanNotes") || l.Contains("QZ nine"));
        // Plan size strings (7/8", PL 3/4" x ...) are still stripped; section designations (WT9x38) are not size strings and may stay.
        Assert.DoesNotContain(chk.SelectMany(Strings), s => IfcCheckFilter.SizeString.IsMatch(s) || IfcCheckFilter.JudgementLeak.IsMatch(s));
    }

    [Fact]
    public void Filter_Status_BecomesAFlagWithoutReasonOrBasis()
    {
        var fl = new IfcCheckFilter();
        string q1 = fl.Filter(1, "IFCPROPERTYSINGLEVALUE", "'Status',$,IFCLABEL('UNRESOLVED'),$")!;
        Assert.Contains("FLAGGED", q1);
        Assert.DoesNotContain("UNRESOLVED", q1);
        Assert.Null(fl.Filter(2, "IFCPROPERTYSINGLEVALUE", "'Reason',$,IFCTEXT('UNRESOLVED (p.91): the plans lack it'),$"));
        Assert.Null(fl.Filter(3, "IFCPROPERTYSINGLEVALUE", "'Basis',$,IFCTEXT('assumed 3/4 in.'),$"));
        string ps = fl.Filter(4, "IFCPROPERTYSET", "'0123456789012345678901',$,'SBM_Status',$,(#1,#2,#3)")!;
        Assert.Contains("(#1)", ps);
        Assert.DoesNotContain("#2", ps);
        // A set whose properties are all gone and its relationship vanish too.
        Assert.Null(fl.Filter(5, "IFCPROPERTYSET", "'0123456789012345678902',$,'SBM_X',$,(#2,#3)"));
        Assert.Null(fl.Filter(6, "IFCRELDEFINESBYPROPERTIES", "'0123456789012345678903',$,$,$,(#99),#5"));
        Assert.NotNull(fl.Filter(7, "IFCRELDEFINESBYPROPERTIES", "'0123456789012345678904',$,$,$,(#99),#4"));
        // Root entities: Description removed, a size / page text in the Name becomes the GlobalId, class refinements stay.
        string root = fl.Filter(8, "IFCPLATE", "'0123456789012345678905',$,'PL 3/4\" x 12\"','Web splice PL 5/8\" x 22 1/2\" (p.4)','FILLER_PLATE',#10,#11,'M1',.USERDEFINED.")!;
        Assert.Equal("'0123456789012345678905',$,'0123456789012345678905',$,'FILLER_PLATE',#10,#11,'M1',.USERDEFINED.", root);
        string kerb = fl.Filter(9, "IFCKERB", "'0123456789012345678906',$,'Left curb','Curb 10\" x 4\"','Curb 10\" x 4\" (p.89)',#1,#2,'Left curb',.USERDEFINED.")!;
        Assert.Contains("'USERDEFINED'", kerb);
        Assert.Equal("IFCLABEL('Steel')", FilterMaterialValue(fl));
    }

    private static string FilterMaterialValue(IfcCheckFilter fl)
    {
        string m = fl.Filter(20, "IFCMATERIAL", "'Steel (placeholder, A36 minimum per plans)',$,$")!;
        return "IFCLABEL(" + m[..m.IndexOf(",$")] + ")";
    }

    [Fact]
    public void ForCheckDatabase_IsTheRegistryWithoutSheetsLinksOrText()
    {
        var doc = SbmDocument.Sample();
        string normalDb = Path.Combine(_dir, "n.sqlite"), chkDb = Path.Combine(_dir, "c.sqlite");
        doc.ExportDatabase(normalDb);
        var res = doc.ExportDatabase(chkDb, new DatabaseOptions { ForCheck = true });
        Assert.Null(res.LinksFile);
        using var n = ModelDatabase.Open(normalDb);
        using var c = ModelDatabase.Open(chkDb);
        Assert.Equal(ModelDatabase.Scalar<long>(n, "SELECT COUNT(*) FROM el"), ModelDatabase.Scalar<long>(c, "SELECT COUNT(*) FROM el"));
        Assert.Equal(0, ModelDatabase.Scalar<long>(c, "SELECT COUNT(*) FROM sheets") + ModelDatabase.Scalar<long>(c, "SELECT COUNT(*) FROM element_sheet") + ModelDatabase.Scalar<long>(c, "SELECT COUNT(*) FROM qc"));
        Assert.Equal(0, ModelDatabase.Scalar<long>(c, "SELECT COUNT(*) FROM props WHERE json <> '{\"flagged\":true}'"));
        Assert.Equal(0, ModelDatabase.Scalar<long>(c, "SELECT COUNT(*) FROM meta WHERE key IN ('model_file','links_file')"));
        Assert.Equal("1", ModelDatabase.Scalar<string>(c, "SELECT value FROM meta WHERE key = 'for_check'"));
        // The normal database has property text (descriptions); the checking one has none, and the same GlobalIds.
        Assert.True(ModelDatabase.Scalar<long>(n, "SELECT COUNT(*) FROM props WHERE json LIKE '%description%'") > 0);
        Assert.Equal(0, ModelDatabase.Scalar<long>(c, "SELECT COUNT(*) FROM elements WHERE properties LIKE '%description%'"));
        string Ids(SqliteConnection cn) { using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT group_concat(global_id) FROM (SELECT global_id FROM el ORDER BY global_id)"; return (string)cmd.ExecuteScalar()!; }
        Assert.Equal(Ids(n), Ids(c));
    }
}
