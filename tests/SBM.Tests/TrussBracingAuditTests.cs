using SBM.Core.Framing;
using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span agent review 2 (2026-11-03, fork-lowerlat): bracing connection audit of PLACED rivets (member end + plate, plate fastened to the
/// truss), plates carried along the stack over the whole lap, edge angle fills on the crossing member, plates lapping the crossing member's side.</summary>
public class TrussBracingAuditTests
{
    private const double Half = TrussTests.Half, Panel = TrussTests.Panel;
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    /// <summary>K of two laterals N:L2 / S:L2 -> the CL point LM3 on strut 3 (passing the apex), a plate at the apex and one at N:L2 with an edge angle.</summary>
    private static (BridgeProject p, TrussDefinition t) K(int edgeRivets)
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        var l3 = t.Points.First(x => x.Name == "L3");
        t.Points.Add(new TrussPointDefinition("LM3", l3.Station, l3.Elevation) { Offset = Half, Planes = new() { "N" } });
        t.Members.First(m => m.Name == "STRUT3").Section = "PL";
        t.Members.Add(new TrussMemberDefinition { Name = "KA", From = "N:L2", To = "N:LM3", Section = "PL", Role = TrussMemberRole.LowerLateral });
        t.Members.Add(new TrussMemberDefinition { Name = "KB", From = "S:L2", To = "N:LM3", Section = "PL", Role = TrussMemberRole.LowerLateral });
        t.JointDefaults.BracingCornerClearance = In(0.5);
        t.JointDefaults.BracingStackClearance = In(4);
        t.BracingGussets = new()
        {
            new TrussBracingGussetDefinition { Point = "N:LM3", Thickness = In(0.5), Side = TrussBracingPlateSide.Below, RivetsPerMember = 6, RivetPitch = In(3), RivetGage = In(4), RivetDiameter = In(0.875) },
            new TrussBracingGussetDefinition { Point = "N:L2", Thickness = In(0.5), Side = TrussBracingPlateSide.Below, EdgeAngle = "L4x3-1/2x7/16", EdgeRivets = edgeRivets, RivetsPerMember = 6,
                RivetPitch = In(3), RivetGage = In(4), RivetDiameter = In(0.875), PlateToStack = true, ProbeFaces = true },
        };
        return (p, t);
    }

    [Fact]
    public void Audit_CountsPlacedRivets_AndFlagsAPlateNotFastened()
    {
        var (p1, _) = K(4);
        var s1 = Regen(p1).Steel!;
        var apex = s1.Truss!.BracingConnections.Single(c => c.Where == "N:LM3");
        Assert.Equal(2, apex.Ends.Count);
        Assert.All(apex.Ends, e => Assert.InRange(e.Rivets, 1, 6));                 // placed rivets through lateral + plate
        Assert.True(apex.Plates.Single().Fastened);                                 // riveted to the strut passing the apex
        var l2 = s1.Truss.BracingConnections.Single(c => c.Where == "N:L2");
        Assert.True(l2.Angles.Single().ToPlate > 0);                                // angle riveted to its plate (its stack leg: whatever steel the test truss has behind it)
        // no edge angle rivets: the plate hangs on the lateral only
        var (p0, _) = K(0);
        var s0 = Regen(p0).Steel!;
        var l2b = s0.Truss!.BracingConnections.Single(c => c.Where == "N:L2");
        Assert.False(l2b.Plates.Single().Fastened);
        Assert.Contains(s0.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("plate N-BGL2 is not fastened"));
        Assert.Contains(s0.Diagnostics, d => d.Message.Contains("edge angle N-BGL2-EA has no placed rivet into the truss"));
        // geometry unchanged by the audit
        Assert.Equal(s0.Parts.Count, s1.Parts.Count);
    }

    [Fact]
    public void CarryWholeLap_PlateRunsAlongTheStackOverTheLap_OptionalInJson()
    {
        double AngleLength(bool whole)
        {
            var (p, t) = K(4);
            t.BracingGussets![1].CarryWholeLap = whole;
            Assert.Equal(whole, p.ToJson().Contains("CarryWholeLap"));
            return Regen(p).Steel!.Parts.Single(x => x.Mark == "N-BGL2-EA").Length;
        }
        Assert.True(AngleLength(true) >= AngleLength(false) - 1e-6);
    }

    [Fact]
    public void LapCrossing_EdgeAngleFill_OmittedWhenUnset()
    {
        var (p, t) = K(4);
        var json = p.ToJson();
        foreach (var k in new[] { "LapCrossing", "LapRivetRows", "LapRivetsPerRow", "EdgeAngleFill", "EdgeAngleSide" }) Assert.DoesNotContain(k, json);
        t.BracingGussets![0].EdgeAngleFill = true; t.BracingGussets[0].LapCrossing = In(6); t.BracingGussets[0].LapRivetRows = new() { In(3) }; t.BracingGussets[0].LapRivetsPerRow = 3;
        t.BracingGussets[0].EdgeAngleSide = TrussBracingPlateSide.Above;
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!;
        Assert.True(back.BracingGussets![0].EdgeAngleFill);
        Assert.Equal(In(6), back.BracingGussets[0].LapCrossing, 9);
        Assert.Equal(3, back.BracingGussets[0].LapRivetsPerRow);
        Assert.Equal(TrussBracingPlateSide.Above, back.BracingGussets[0].EdgeAngleSide);
    }

    [Fact]
    public void EdgeRivetCounts_PlateRows_ThreeMemberLines()
    {
        int Rivets(Action<TrussBracingGussetDefinition> set)
        {
            var (p, t) = K(4);
            set(t.BracingGussets![1]);
            return Regen(p).Steel!.Bolts.Count;
        }
        int b0 = Rivets(_ => { });
        // 9 in the plate leg in two staggered rows, 2 in the stack leg (was 4 + 4)
        int b1 = Rivets(bg => { bg.EdgeRivetCounts = new() { 2, 9 }; bg.EdgeRivetPlateRows = 2; });
        Assert.NotEqual(b0, b1);
        int b2 = Rivets(bg => bg.RivetLines = 3);
        Assert.True(b2 > 0);
        var (pj, tj) = K(4);
        Assert.DoesNotContain("EdgeRivetCounts", pj.ToJson());
        tj.BracingGussets![1].EdgeRivetCounts = new() { 2, 6 }; tj.BracingGussets[1].EdgeRivetPlateRows = 2; tj.BracingGussets[1].RivetLines = 3;
        var back = BridgeProject.FromJson(pj.ToJson()).Bridges[0].Truss!.BracingGussets![1];
        Assert.Equal(new List<int> { 2, 6 }, back.EdgeRivetCounts);
        Assert.Equal(2, back.EdgeRivetPlateRows);
        Assert.Equal(3, back.RivetLines);
    }

    [Fact]
    public void LapCrossing_SwayPlateLapsTheStrut_RiveredThroughIt()
    {
        // V sway from the top chord points U3 down to the strut CL at L3: plate pair (transverse) on the sway members, lapping the strut sides
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        var l3 = t.Points.First(x => x.Name == "L3");
        t.Sections.Add(new TrussSectionDefinition { Name = "SW", Kind = TrussSectionKind.Plate, Width = In(10), Depth = In(12) });
        t.Points.Add(new TrussPointDefinition("LM3", l3.Station, l3.Elevation) { Offset = Half, Planes = new() { "N" } });
        t.Members.First(m => m.Name == "STRUT3").Section = "PL";
        t.Members.RemoveAll(m => m.Name.StartsWith("FB"));
        t.Members.Add(new TrussMemberDefinition { Name = "SA", From = "N:U3", To = "N:LM3", Section = "SW", Role = TrussMemberRole.Sway });
        t.Members.Add(new TrussMemberDefinition { Name = "SB", From = "S:U3", To = "N:LM3", Section = "SW", Role = TrussMemberRole.Sway });
        t.JointDefaults.BracingCornerClearance = In(0.5);
        t.BracingGussets = new()
        {
            new TrussBracingGussetDefinition { Point = "N:LM3", Members = new() { "SA", "SB" }, Thickness = In(0.5), Side = TrussBracingPlateSide.Both, Orientation = TrussBracingPlateOrientation.Transverse,
                EdgeAngleOnCrossing = true, RivetsPerMember = 4, RivetPitch = In(3), RivetGage = In(3), RivetDiameter = In(0.875),
                LapCrossing = In(6), LapRivetRows = new() { In(3) }, LapRivetsPerRow = 3 },
        };
        var s = Regen(p).Steel!;
        var rec = s.Truss!.BracingConnections.SingleOrDefault(c => c.Where == "N:LM3");
        Assert.True(rec != null, string.Join(" / ", s.Diagnostics.Where(d => d.Message.Contains("LM3") || d.Message.Contains("bracing")).Select(d => d.Message)));
        Assert.All(rec.Plates, pl => Assert.True(pl.Fastened, $"{pl.Plate} d{pl.Direct}"));
        Assert.All(rec.Plates, pl => Assert.True(pl.Direct >= 3));
    }
}
