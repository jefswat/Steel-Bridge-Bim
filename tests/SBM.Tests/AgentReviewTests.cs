using SBM.Api;
using SBM.Core.Bridge;
using Xunit;

namespace SBM.Tests;

public class AgentReviewTests
{
    private static string TempModel(out SbmDocument doc)
    {
        string dir = Path.Combine(Path.GetTempPath(), "sbm-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "review test.sbm");
        doc = SbmDocument.Sample();
        doc.Save(path);
        doc = SbmDocument.Open(path);
        return path;
    }

    private static readonly AgentReviewOptions Fast = new() { Regenerate = false };

    [Fact]
    public void Defaults_FlaggedUntilSet_RelevanceRules_AndConfirm()
    {
        string path = TempModel(out var doc);
        var b = doc.Project.Bridges[0];
        var rep = AgentReview.Run(doc, Fast);

        // no PDF attached
        Assert.Contains(rep.Flags, f => f.Kind == ReviewFlagKind.PlanPdf && f.Level == ReviewLevel.Open);
        // bridge bolts are still the program's values
        string bolts = $"Bridges[{b.Name}].Bolts.Diameter";
        Assert.Contains(rep.Flags, f => f.Path == bolts && f.Level == ReviewLevel.Open);
        // disc bearings: the elastomeric pad / generic body inputs are not read, so never flagged
        Assert.Equal(BearingType.Disc, b.Bearings.Type);
        Assert.Contains(rep.Flags, f => f.Path.Contains(".Bearings.Disc", StringComparison.Ordinal));
        Assert.DoesNotContain(rep.Flags, f => f.Path.Contains(".Bearings.Pad", StringComparison.Ordinal) || f.Path.Contains(".Bearings.Generic", StringComparison.Ordinal));
        // a steel bridge: the prestressed inputs are not reviewed
        Assert.DoesNotContain(rep.Flags, f => f.Path.Contains(".Girders.Prestressed", StringComparison.Ordinal));

        // set from the plans -> no longer flagged
        b.Bolts.Diameter = 0.0254;
        rep = AgentReview.Run(doc, Fast);
        Assert.DoesNotContain(rep.Flags, f => f.Path == bolts);

        // confirmed -> not flagged while the value is unchanged
        string grade = $"Bridges[{b.Name}].Bolts.Grade";
        Assert.Contains(rep.Flags, f => f.Path == grade);
        Assert.True(AgentReview.Confirm(doc, new[] { $"Bridges[{b.Name}].Bolts" }, "plan note 5") >= 1);
        rep = AgentReview.Run(doc, Fast);
        Assert.DoesNotContain(rep.Flags, f => f.Path == grade);
        Assert.True(rep.Confirmed >= 1);
        Assert.True(File.Exists(AgentReviewRecord.PathFor(path)));
    }

    [Fact]
    public void CivilItems_StartingValuesScanned_ShapeAndLocationRules()
    {
        TempModel(out var doc);
        var net = new SBM.Core.Utility.UtilityNetworkDefinition { Name = "Storm A" };
        net.Structures.Add(new() { Name = "MH-1", X = 10, Y = 20, RimElevation = 100, SumpElevation = 97, InnerDiameter = 1.2, WallThickness = 0.125 });
        net.Pipes.Add(new() { Name = "P-1", From = "MH-1", EndX = 40, EndY = 20, InvertStart = 97.2, InvertEnd = 97, InnerDiameter = 0.45, WallThickness = 0.06 });
        doc.Project.Utilities.Add(net);
        doc.Project.Roads.Add(new SBM.Core.Road.RoadDefinition { Name = "Road A", AlignmentName = doc.Project.Alignments[0].Name });
        var rep = AgentReview.Run(doc, Fast);
        var paths = rep.Flags.Where(f => f.Kind == ReviewFlagKind.ProgramDefault).Select(f => f.Path).ToList();
        // a value not set from the plans: the floor thickness (NaN = not given) is reported, the given rim is not
        Assert.Contains("Utilities[Storm A].Structures[MH-1].FloorThickness", paths);
        Assert.DoesNotContain("Utilities[Storm A].Structures[MH-1].RimElevation", paths);
        // located by X / Y: the station / offset inputs are not read; round: the box sizes are not read; the pipe's start is a structure
        Assert.DoesNotContain(paths, p => p.StartsWith("Utilities[Storm A].Structures[MH-1].Station") || p.EndsWith("MH-1.InnerLength") || p.EndsWith("P-1.StartX") || p.EndsWith("P-1.Cover"));
        // roads are scanned too; the sample spacing is a modeling parameter
        Assert.Contains(paths, p => p.StartsWith("Roads[Road A]."));
        Assert.DoesNotContain("Roads[Road A].SampleSpacing", paths);
    }

    [Fact]
    public void RepeatedDefaults_ReportedOncePerPattern()
    {
        Assert.Equal("Bridges[A].Supports[*].Substructure.Footings[*].PileLength", AgentReview.Pattern("Bridges[A].Supports[Pier 1].Substructure.Footings[0].PileLength"));
        TempModel(out var doc);
        var rep = AgentReview.Run(doc, Fast);
        var paths = rep.Flags.Where(f => f.Kind == ReviewFlagKind.ProgramDefault).Select(f => (f.Path, f.Value, f.Level)).ToList();
        Assert.Equal(paths.Count, paths.Distinct().Count());
        Assert.All(rep.Flags.Where(f => f.Places > 1), f => Assert.True(f.Places >= AgentReview.RepeatThreshold && f.Path.Contains("[*]")));
    }

    [Fact]
    public void PlanPdf_FingerprintRecorded_ChangeFlagged_ThenAccepted()
    {
        string path = TempModel(out var doc);
        string pdf = Path.Combine(Path.GetDirectoryName(path)!, "plans.pdf");
        File.WriteAllText(pdf, "%PDF-1.4 first issue");
        doc.Project.PlanPdfs.Add("plans.pdf");

        var rep = AgentReview.Run(doc, Fast);
        Assert.DoesNotContain(rep.Flags, f => f.Kind == ReviewFlagKind.PlanPdf);
        Assert.Contains(rep.Notes, n => n.Contains("fingerprint recorded"));

        File.WriteAllText(pdf, "%PDF-1.4 revision A");
        rep = AgentReview.Run(doc, Fast);
        Assert.Contains(rep.Flags, f => f.Kind == ReviewFlagKind.PlanPdf && f.Subject.Contains("changed"));

        rep = AgentReview.Run(doc, new AgentReviewOptions { Regenerate = false, AcceptPdfs = { "plans.pdf" } });
        Assert.DoesNotContain(rep.Flags, f => f.Kind == ReviewFlagKind.PlanPdf);
        rep = AgentReview.Run(doc, Fast);
        Assert.DoesNotContain(rep.Flags, f => f.Kind == ReviewFlagKind.PlanPdf);

        File.Delete(pdf);
        rep = AgentReview.Run(doc, Fast);
        Assert.Contains(rep.Flags, f => f.Kind == ReviewFlagKind.PlanPdf && f.Subject.Contains("not found"));
    }
}
