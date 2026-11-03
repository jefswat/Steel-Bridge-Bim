using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Connection plates flat on member faces (TrussDefinition.FacePlates, PTC U11 agent review 2026-11-03): a plate on the chord top covers bent over
/// the chord kink (two flat pieces), fills to a member below it, rivets at plate points and on a member's end, an edge angle on the stack.</summary>
public class TrussFacePlateTests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    private static (BridgeProject p, TrussDefinition t) WithStrut()
    {
        var (p, b) = TrussTests.Model(pratt: true, extras: false);
        var t = b.Truss!;
        t.Members.Add(new TrussMemberDefinition { Name = "PS1", From = "N:U1", To = "S:U1", Section = "PL", Role = TrussMemberRole.Portal });
        t.JointDefaults.BracingStackClearance = In(1);
        return (p, t);
    }

    [Fact]
    public void FacePlate_BentOverTheKink_FillsRivetsAndRoundTrip()
    {
        var (p0, _) = WithStrut();
        Assert.DoesNotContain("FacePlates", p0.ToJson());
        var s0 = Regen(p0).Steel!;
        int rivets0 = s0.Bolts.Count;
        var (p, t) = WithStrut();
        // N truss = left of the alignment: inboard (toward the strut) = -V
        t.FacePlates = new()
        {
            new TrussFacePlateDefinition
            {
                Point = "N:U1", Label = "test plate", Thickness = In(0.875), Side = TrussBracingPlateSide.Above, Material = "C",
                Outline = new() { new(-In(30), In(10)), new(In(30), In(10)), new(In(30), -In(40)), new(-In(30), -In(40)) },
                Fills = new() { new TrussFacePlateFill { Member = "PS1", Outline = new() { new(-In(7), -In(10)), new(In(7.5), -In(10)), new(In(7.5), -In(38)), new(-In(7), -In(38)) }, PlanThickness = In(6) } },
                Rivets = new() { new TrussFacePlateRivet(In(10), 0), new TrussFacePlateRivet(In(20), -In(25)) },   // over the chord cover; one over nothing (not placed)
                MemberRivets = new() { new TrussFacePlateRivetGroup { Member = "PS1", Count = 6, Rows = 2, Gage = In(4), Pitch = In(3), FromEnd = In(1.5), Stagger = true, Offset = In(4) } },   // rows at U +2 / +6 (left of the strut = up-station)
                RivetDiameter = In(0.875),
            },
        };
        var json = p.ToJson();
        Assert.Contains("FacePlates", json);
        var bm = Regen(p);
        var st = bm.Steel!;
        var plate = st.Parts.Single(x => x.Mark == "N-FPU1");
        Assert.Contains("two flat pieces", plate.Description);
        Assert.Equal(PartCategory.TrussConnectionPlate, plate.Category);
        // bearing face on the chord tops: the plate's lowest point over the level chord U1-U2 = the chord's highest point there
        var u12 = st.Parts.Where(x => x.Mark.StartsWith("N-U1-U2")).ToList();
        double chordTop = u12.Max(x => x.Mesh.Bounds().Max.Z);
        var pos = plate.Mesh.Positions;
        double x0 = pos.Min(q => q.X), x1 = pos.Max(q => q.X);
        var ahead = pos.Where(q => q.X > x1 - In(10)).ToList();
        Assert.InRange(ahead.Min(q => q.Z) - chordTop, -In(1.0 / 32), In(1.0 / 32));
        Assert.InRange(ahead.Max(q => q.Z) - ahead.Min(q => q.Z), In(0.875) - In(1.0 / 32), In(0.875) + In(1.0 / 32));
        // the back piece follows the 45 deg end post (L0-U1): its far end lies well below the ahead piece
        var back = pos.Where(q => q.X < x0 + In(5)).ToList();
        Assert.True(back.Max(q => q.Z) < ahead.Min(q => q.Z) - In(10), "back piece on the end post face");
        // fill between the plate and the strut top
        var fill = st.Parts.Single(x => x.Mark == "N-FPU1-F1");
        Assert.Equal(PartCategory.FillerPlate, fill.Category);
        var strutTop = st.Parts.Where(x => x.Mark.StartsWith("PS1")).Max(x => x.Mesh.Bounds().Max.Z);
        Assert.InRange(fill.Mesh.Bounds().Max.Z - fill.Mesh.Bounds().Min.Z, chordTop - strutTop - In(1.0 / 16), chordTop - strutTop + In(1.0 / 16));
        // rivets: the chord-cover point and the 6 strut rivets placed (grip more than the plate), the point over nothing skipped
        var newR = st.Bolts.Skip(rivets0).Where(r => r.Kind == BoltKind.Rivet).ToList();
        Assert.True(7 == newR.Count, string.Join(" | ", st.Diagnostics.Where(d => d.Message.Contains("face plate")).Select(d => d.Message)));
        Assert.All(newR, r => Assert.True(r.Grip > In(0.875) + In(1.0 / 32)));
        Assert.Contains(st.Diagnostics, d => d.Message.Contains("face plate rivets - 7 placed") && d.Message.Contains("1 positions grip the plate"));
        // round trip
        var back2 = BridgeProject.FromJson(json).Bridges[0].Truss!.FacePlates!;
        Assert.Single(back2);
        Assert.Equal(6, back2[0].MemberRivets![0].Count);
    }

    [Fact]
    public void FacePlate_FlatUnderAMember_EdgeAngleOnTheStack()
    {
        var (p, t) = WithStrut();
        t.FacePlates = new()
        {
            new TrussFacePlateDefinition
            {
                Point = "N:U1", Suffix = "-B", Thickness = In(0.5625), Side = TrussBracingPlateSide.Below, BearOn = new() { "PS1" }, NormalFrom = "PS1",
                Outline = new() { new(-In(12), -In(12)), new(In(12), -In(12)), new(In(12), -In(40)), new(-In(12), -In(40)) },
                EdgeAngle = "L4x4x1/2", EdgeAngleU0 = -In(10), EdgeAngleU1 = In(10), EdgeAngleRivetsPlate = 4, EdgeAngleRivetsStack = 3, RivetDiameter = In(0.875),
                MemberRivets = new() { new TrussFacePlateRivetGroup { Member = "PS1", Count = 4, Rows = 2, Gage = In(4), Pitch = In(3), FromEnd = In(1.5) } },
            },
        };
        var st = Regen(p).Steel!;
        var plate = st.Parts.Single(x => x.Mark == "N-FPU1-B");
        Assert.DoesNotContain("two flat pieces", plate.Description);
        double strutBot = st.Parts.Where(x => x.Mark.StartsWith("PS1")).Min(x => x.Mesh.Bounds().Min.Z);
        Assert.InRange(plate.Mesh.Bounds().Max.Z - strutBot, -In(1.0 / 32), In(1.0 / 32));
        var ea = st.Parts.Single(x => x.Mark == "N-FPU1-B-EA");
        Assert.Equal(PartCategory.TrussConnectionAngle, ea.Category);
        Assert.InRange(ea.Length, In(20) - 1e-6, In(20) + 1e-6);
        Assert.Contains(st.Diagnostics, d => d.Message.Contains("face plate rivets -"));
    }

    [Fact]
    public void PlaneItemRivets_NeedSteelBehind_SkipsRivetsThroughThePieceAlone()
    {
        var (p, t) = WithStrut();
        TrussPlaneItemDefinition Item(bool need) => new()
        {
            Name = "G", PointA = "N:U1", PointB = "S:U1", Kind = TrussPlaneItemKind.Plate, W0 = In(20), W1 = In(20.75), RivetsNeedSteelBehind = need,
            Outline = new() { new(In(60), -In(30)), new(In(80), -In(30)), new(In(80), -In(10)), new(In(60), -In(10)) },
            Rivets = new() { new TrussPlaneItemRivet { U = In(70), V = -In(20), W = In(20.375), Diameter = In(0.875) } },   // in the air beside the strut
        };
        t.PlaneItems = new() { Item(false) };
        Assert.DoesNotContain("RivetsNeedSteelBehind", p.ToJson());
        var s0 = Regen(p).Steel!;
        Assert.Contains(s0.Diagnostics, d => d.Message.Contains("plane item rivets - 1 placed"));
        t.PlaneItems = new() { Item(true) };
        Assert.Contains("RivetsNeedSteelBehind", p.ToJson());
        var s1 = Regen(p).Steel!;
        Assert.Contains(s1.Diagnostics, d => d.Message.Contains("plane item rivets - 0 placed") && d.Message.Contains("1 positions with nothing behind"));
    }
}
