using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>As-designed cross frame inputs added for the plan gaps: links between two support lines, per-bay support styles,
/// bottom bolt pitch, UNRESOLVED style notes and reporting of plan-line bays that are not generated.</summary>
public class CrossFrameGapTests
{
    private static SbmDocument Doc(out BridgeDefinition b)
    {
        var doc = SbmDocument.Sample();
        b = doc.Bridge();
        b.FieldSplices.Clear();
        b.AuxiliarySupports.Add(new SupportDefinition
        {
            Name = "Pier 0A", Type = SupportType.Pier, Station = b.Supports[0].Station + Ft(3), SkewDegrees = -4, GirderEndExtension = In(12),
        });
        b.Girders.Beginnings.Add(new GirderBeginning { Girder = 0, Support = "Pier 0A" });
        b.Girders.Beginnings.Add(new GirderBeginning { Girder = 1, Support = "Pier 0A" });
        var cf = b.CrossFrames;
        cf.Styles.Add(new CrossFrameStyle
        {
            Name = "A", TopChord = "L6x6x9/16", Diagonal = "L6x6x7/8", BottomChord = "2-MC8x20", GussetThickness = In(0.625),
            BoltDiameter = In(1), BoltsPerConnection = 2, BoltPitch = In(3.5), BottomBoltPitch = In(6), BoltColumns = 2, BoltGage = In(3.5),
            BoltEdgeDistance = In(1.75), BottomWorkPointOffset = In(10), Unresolved = { "pitch of something is not dimensioned" },
        });
        cf.Styles.Add(new CrossFrameStyle { Name = "B", TopChord = "L6x6x9/16", Diagonal = "L6x6x7/8", BottomChord = "2-MC8x20", BoltDiameter = In(1) });
        return doc;
    }

    [Fact]
    public void Link_FrameBetweenGirdersOnTwoSupportLines()
    {
        var doc = Doc(out var b);
        b.CrossFrames.SupportStyle = "none";
        b.CrossFrames.Links.Add(new CrossFrameLink { LeftGirder = 1, LeftSupport = "Pier 0A", RightGirder = 2, RightSupport = b.Supports[0].Name, Style = "B" });
        Assert.Empty(doc.Regenerate().Errors);
        var m = doc.Model(b.Name);
        var f = m.Framing;
        var link = Assert.Single(f.CrossFrames, c => c.AtSupport);
        Assert.Equal((1, 2), (link.Bay, link.RightGirder));
        Assert.Equal("B", link.Style);
        Assert.Equal(f.AuxSupports[0].Bearings[1]!.TopOfSteel.XY.X, link.Left.XY.X, 9);
        Assert.Equal(f.Supports[0].Bearings[2]!.TopOfSteel.XY.Y, link.Right.XY.Y, 9);
        Assert.Contains(m.Steel!.Assemblies, a => a.Mark == link.Mark);
        Assert.Contains(m.Steel.Bolts, x => x.Assembly == link.Mark);
        Assert.Contains(f.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("link"));
        // A link naming a missing support is reported, not silently ignored.
        b.CrossFrames.Links[0].RightSupport = "Nowhere";
        doc.Regenerate();
        Assert.Contains(doc.Model(b.Name).Framing.Diagnostics, d => d.Message.Contains("support not found"));
    }

    [Fact]
    public void SupportBayStyles_OverrideOneBayOfASupportLine()
    {
        var doc = Doc(out var b);
        b.CrossFrames.SupportStyle = "B";
        b.CrossFrames.SupportBayStyles[b.Supports[1].Name] = new() { [2] = "A" };
        Assert.Empty(doc.Regenerate().Errors);
        var f = doc.Model(b.Name).Framing;
        var atPier = f.CrossFrames.Where(c => c.AtSupport && c.SupportIndex == 1).ToList();
        Assert.NotEmpty(atPier);
        Assert.All(atPier, c => Assert.Equal(c.Bay == 2 ? "A" : "B", c.Style));
        // Clone keeps the per-bay styles and links.
        b.CrossFrames.Links.Add(new CrossFrameLink { LeftGirder = 1, LeftSupport = "x", RightGirder = 2, RightSupport = "y" });
        var c2 = b.CrossFrames.Clone();
        Assert.Equal("A", c2.SupportBayStyles[b.Supports[1].Name][2]);
        Assert.Single(c2.Links);
    }

    [Fact]
    public void BottomBoltPitch_AndUnresolvedNotes()
    {
        var doc = Doc(out var b);
        b.CrossFrames.SupportStyle = "A";
        Assert.Empty(doc.Regenerate().Errors);
        var m = doc.Model(b.Name);
        var f = m.Framing;
        var cf = f.CrossFrames.First(c => c.AtSupport && c.Style == "A");
        var det = m.Steel!.CrossFrameDetails.First(d => d.Mark == cf.Mark);
        // Bottom gusset bolts at the left girder: 2 rows 6" apart (the common pitch is 3.5").
        var wp = det.WorkPoints;
        var bottom = det.Bolts.Where(p => Math.Abs(p.X - wp[1].X) < In(8) && p.Y < (wp[0].Y + wp[1].Y) / 2).Select(p => Math.Round(p.Y, 6)).Distinct().OrderBy(y => y).ToList();
        Assert.Equal(2, bottom.Count);
        Assert.Equal(In(6), bottom[1] - bottom[0], 6);
        // The note is raised once per bridge as a warning.
        Assert.Single(m.Steel.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("pitch of something"));
    }

    [Fact]
    public void PlanLineBayThatCannotBeGenerated_IsReported()
    {
        var doc = Doc(out var b);
        b.CrossFrames.IntermediateStyle = "B";
        b.CrossFrames.Lines.Add(new CrossFrameLine { Girder = 2, Distance = Ft(60), Bays = new() { 2, 40 } });
        Assert.Empty(doc.Regenerate().Errors);
        var f = doc.Model(b.Name).Framing;
        Assert.Contains(f.Diagnostics, d => d.Message.Contains("listed bay with left girder G41"));
    }

    [Fact]
    public void LineUnresolvedNotes_AreRaisedWithTheLineAndCloned()
    {
        var doc = Doc(out var b);
        b.CrossFrames.IntermediateStyle = "B";
        var line = new CrossFrameLine { Girder = 2, Distance = Ft(60), Bays = new() { 1, 2 }, Unresolved = { "dims conflict (PDF 115)" } };
        b.CrossFrames.Lines.Add(line);
        Assert.Empty(doc.Regenerate().Errors);
        var f = doc.Model(b.Name).Framing;
        Assert.Single(f.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("dims conflict (PDF 115)") && d.Message.Contains("60.00 ft along G3"));
        // Frames of the line are still generated as listed.
        Assert.Contains(f.CrossFrames, c => !c.AtSupport && c.Bay == 1);
        var c2 = line.Clone();
        c2.Unresolved.Add("x");
        Assert.Single(line.Unresolved);
    }
}
