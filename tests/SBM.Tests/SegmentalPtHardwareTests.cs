using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Segmental;
using SBM.Core.Solids;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Segmental PT hardware and tendon lanes (OPTIONAL SegmentalDefinition.PtBars / Pipes / DuctLanes, TendonDefinition.Lanes / Profile, block station
/// limits TopBelowDeck / ThicknessFromWeb, JointRef Centre): closed block solids (also a block that tapers to an apex), erection bars between the far block
/// faces, bent future-PT pipes with the table deflection as a check row, tendons through their lane holes at every joint with the lane change inside the
/// segment and the deviation to the anchor; IFC entities; JSON omitted when unset.</summary>
public class SegmentalPtHardwareTests
{
    private static BridgeModel Generate(BridgeProject p) => new RegenerationCache().Regenerate(p).Single()!;

    internal static bool Closed(Mesh m)
    {
        var id = new Dictionary<Vec3, int>();
        int Key(Vec3 v) { var k = new Vec3(Math.Round(v.X, 6), Math.Round(v.Y, 6), Math.Round(v.Z, 6)); if (!id.TryGetValue(k, out int i)) id[k] = i = id.Count; return i; }
        var edges = new Dictionary<(int, int), int>();
        for (int t = 0; t < m.Indices.Count; t += 3)
        {
            var pa = m.Positions[m.Indices[t]]; var pb = m.Positions[m.Indices[t + 1]]; var pc = m.Positions[m.Indices[t + 2]];
            if (Vec3.Cross(pb - pa, pc - pa).Length < 1e-10) return false;           // zero-area triangles open the shell in OpenCascade
            int a = Key(pa), b = Key(pb), c = Key(pc);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) }) edges[(u, v)] = edges.GetValueOrDefault((u, v)) + 1;
        }
        return edges.All(e => e.Value == 1 && edges.GetValueOrDefault((e.Key.Item2, e.Key.Item1)) == 1);
    }

    private static (BridgeProject p, BridgeDefinition b) Model()
    {
        var (p, b) = SegmentalTests.Model();
        var sg = b.Segmental!;
        sg.BlockTypes = new()
        {
            new() { Name = "bottom", Kind = SegmentalBlockKind.AnchorBlock, From = SegmentalJointRef.BentSide, Start = Ft(4),
                Stations = { new() { Along = 0, TopAboveSlab = In(24), WidthFromWeb = In(34) }, new() { Along = In(76.5), TopAboveSlab = In(6), WidthFromWeb = In(29.5) },
                    new() { Along = In(102), TopAboveSlab = 0, WidthFromWeb = 0 } } },
            new() { Name = "A", Kind = SegmentalBlockKind.ErectionBarBlock, From = SegmentalJointRef.TipSide, Start = Ft(2) + In(11),
                Stations = { new() { Along = 0, BottomBelowDeck = Ft(2) + In(8.25), BottomSquareToWeb = Ft(3) + In(3) }, new() { Along = Ft(1.5), BottomBelowDeck = Ft(2) + In(8.25), BottomSquareToWeb = Ft(3) + In(3) } } },
            new() { Name = "C", Kind = SegmentalBlockKind.ErectionBarBlock, From = SegmentalJointRef.TipSide, Start = Ft(2) + In(11),
                Stations = { new() { Along = 0, InnerFromCl = Ft(5.75), TopBelowDeck = Ft(13.75) }, new() { Along = Ft(2), InnerFromCl = Ft(5.75), TopBelowDeck = Ft(13.75) } } },
            new() { Name = "dev", Kind = SegmentalBlockKind.DeviationBlock, From = SegmentalJointRef.Centre, Start = -Ft(1.5),
                Stations = { new() { Along = 0, ThicknessFromWeb = Ft(2.25) }, new() { Along = Ft(3), ThicknessFromWeb = Ft(2.25) } } },
        };
        sg.Blocks = new()
        {
            new() { Type = "bottom", Segment = "1-U2", Label = "B1" },
            new() { Type = "A", Segment = "1-U1" }, new() { Type = "A", Segment = "1-U2" }, new() { Type = "A", Segment = "1-U3" },
            new() { Type = "C", Segment = "1-D1" }, new() { Type = "C", Segment = "1-U1" },
            new() { Type = "dev", Segment = "CJ2" },
        };
        sg.PtBars = new()
        {
            new() { Name = "top bars", Segments = @"^1-U\d$", Pairing = SegmentalBarPairing.NextTowardTip, BlockType = "A",
                Positions = { new() { X = Ft(7.75), Depth = Ft(2) + In(0.5) }, new() { X = Ft(9) + In(8), Depth = Ft(2) + In(8.5) } }, Diameter = In(1.75), PrintedMinLength = Ft(14) },
            new() { Name = "bottom bars D1-U1", Segments = @"^1-D1$", Pairing = SegmentalBarPairing.AcrossBent, BlockType = "C", Positions = { new() { X = Ft(6.5), Depth = Ft(14.5) } }, Diameter = In(1.75) },
        };
        sg.Pipes = new()
        {
            new() { Name = "P1", Kind = SegmentalPipeKind.DeviationPipe, Placement = SegmentalPipePlacement.PiAtSegmentCentre, Segments = { "CJ2" }, Side = 1,
                A = In(43.13), B = In(43), C = In(43.13), D = In(98.02), F = In(98.7), G = In(98.15), DeltaDeg = 4, Radius = Ft(22), L1 = Ft(1.23), L2 = Ft(1.54), L3 = Ft(1.23),
                Length = Ft(4), OutsideDiameter = In(4.5), Wall = In(0.237) },
            new() { Name = "F1", Kind = SegmentalPipeKind.FuturePtPipe, Placement = SegmentalPipePlacement.OuterEndAtBentHalfFace, Bents = { "S1" }, UpStationHalf = true, HeightsBelowDeck = true,
                A = In(37.96), B = In(37.5), C = In(37.5), D = In(98.24), F = In(99), G = In(99), DeltaDeg = 2.12, Radius = Ft(30), L1 = Ft(1.95), L2 = Ft(1.11), L3 = Ft(0.95),
                Length = Ft(4), OutsideDiameter = In(4.5), Wall = In(0.237) },
        };
        // lanes: T3 in A1 at the bent, moves to A2 inside D2 / U2 (lane changes), anchors in A7 in segment 3 of each arm
        sg.DuctLanes = new() { new() { Name = "A1", X = Ft(15) + In(8.5), Depth = In(7.25) }, new() { Name = "A2", X = Ft(14) + In(11.5), Depth = In(7.25) },
            new() { Name = "A7", X = Ft(10) + In(2.5), Depth = In(7.25) } };
        var t3 = sg.Tendons.First(t => t.Name == "T3");
        t3.Lanes = new()
        {
            new() { Arm = "D", Joint = 0, Lane = "A1" }, new() { Arm = "D", Joint = 1, Lane = "A1" }, new() { Arm = "D", Joint = 2, Lane = "A2" }, new() { Arm = "D", Joint = 3, Lane = "A7" },
            new() { Arm = "U", Joint = 0, Lane = "A1" }, new() { Arm = "U", Joint = 1, Lane = "A1" }, new() { Arm = "U", Joint = 2, Lane = "A2" }, new() { Arm = "U", Joint = 3, Lane = "A7" },
        };
        t3.Profile = new SegmentalTendonProfile { AnchorLane = "A7", AnchorSetback = In(9), AnchorDepth = Ft(1) + In(1.5), AnchorPiFromFace = In(9) + Ft(9), AnchorRadius = Ft(35),
            ChangePiFromFace = Ft(3.5), ChangeRadius = Ft(35), Source = "test" };
        return (p, b);
    }

    [Fact]
    public void Blocks_ClosedWithoutZeroAreaTriangles_TaperToAnApex()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        Assert.Equal(2 + 6 + 4 + 2, sm.Blocks.Count(b => b.Kind != SegmentalBlockKind.ShearKey));
        foreach (var bl in sm.Blocks) { Assert.True(Closed(bl.Mesh), bl.Name); Assert.True(bl.Mesh.Volume() > 0, bl.Name); }
        // deviation block: centred on the closure joint, 3'-0" long
        var dev = sm.Blocks.First(b => b.Kind == SegmentalBlockKind.DeviationBlock);
        var cj = sm.Segments.Single(s => s.Name == "CJ2");
        Assert.True(dev.Mesh.Volume() > 0.3);
    }

    [Fact]
    public void PtBars_BetweenTheFarBlockFaces_AndPipesWithDeltaCheck()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        var top = sm.PtHardware.Where(h => h.Name.Contains("top bars")).ToList();
        Assert.Equal(2 * 2 * 2, top.Count);                       // U1-U2, U2-U3 x 2 webs x 2 positions
        // far faces of blocks 1'-6" long, 14 ft apart -> 15'-6" in plan
        foreach (var h in top) Assert.InRange(h.Length / Ft(1), 15.3, 15.8);
        Assert.Contains(sm.Checks, c => c.Item.StartsWith("PT bars top bars"));
        Assert.Equal(2, sm.PtHardware.Count(h => h.Name.Contains("D1-U1")));
        var pipes = sm.PtHardware.Where(h => h.IsPipe).ToList();
        Assert.Equal(3, pipes.Count);                              // P1 right; F1 both webs
        foreach (var h in pipes.Concat(top)) Assert.True(Closed(h.Mesh), h.Name);
        Assert.Contains(sm.Checks, c => c.Item.Contains("P1 right") && c.Item.Contains("printed delta"));
        var f1 = pipes.First(h => h.Kind == SegmentalHardwareKind.FuturePtPipe);
        Assert.InRange(f1.Length / Ft(1), 3.9, 4.1);
    }

    [Fact]
    public void Tendon_PassesEveryJointInItsLane_ChangesLaneInsideTheSegment()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        var t = sm.Tendons.First(x => x.Name == "S1 T3 R");
        Assert.True(t.Points.Count > 10);
        var off = sm.Checks.Single(c => c.Item.StartsWith("tendon S1 T3 R: largest duct offset"));
        Assert.True(off.Model < 1.0 / 16, $"{off.Model} in");
        var ang = sm.Checks.Single(c => c.Item.StartsWith("tendon S1 T3 R: largest angle change"));
        Assert.True(ang.Model < 0.5, $"{ang.Model} deg");
        // the anchor: 9" inside the tip face of 1-U3, 1'-1 1/2" below the deck top; the path ends there
        var u3 = sm.Segments.Single(s => s.Name == "1-U3");
        Assert.InRange((t.Points[^1] - u3.Top1).Length, Ft(10), Ft(10.5));
        Assert.DoesNotContain(sm.Checks, c => c.Item.StartsWith("CONFLICT tendon S1 T3") && c.Item.Contains("outside the concrete"));
        // R = 35 ft between P.I.s 3'-6" from the faces of a short test segment does not fit: reported per tendon (built at the radius that fits), never moved
        Assert.Contains(sm.Checks, c => c.Item.StartsWith("CONFLICT tendon S1 T3") && c.Item.Contains("not in its new lane before the joint") && c.Note.Contains("R = 35 ft does not fit"));
    }

    [Fact]
    public void Tendon_AnchorExitAngle_PlacesTheAnchorPi_AndReportsItAgainstTheLane()
    {
        var (p, b) = Model();
        var t3 = b.Segmental!.Tendons.First(t => t.Name == "T3");
        // anchor A7 -> lane A2 at joint 2 (57" away): a 5.40 deg exit over 9'-0" reaches 10.21" -> the P.I. lies 46.8" short of the lane (check row, built so)
        t3.Profile!.AnchorExits = new() { new() { Tendons = "T1,T3", AngleDeg = 5.40, Source = "test" } };
        var sm = Generate(p).Concrete!.Segmental!;
        var ex = sm.Checks.Where(c => c.Item.StartsWith("tendon S1 T3 R") && c.Item.Contains("printed 5.40 deg exit")).ToList();
        Assert.Equal(2, ex.Count);                                         // both arms
        foreach (var c in ex) Assert.InRange(c.Model, -(57 - 10.21) - 0.5, -(57 - 10.21) + 0.5);
        // omitted from the .sbm when unset
        var (p2, _) = Model();
        Assert.DoesNotContain("AnchorExits", p2.ToJson());
    }

    [Fact]
    public void Ifc_HardwareEntities_AndJsonOmittedWhenUnset()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "seg.ifc", p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        var txt = sw.ToString();
        Assert.Contains("'TEMPORARY_ERECTION_PT_BAR'", txt);
        Assert.Contains(".BAR.", txt);
        Assert.Contains("'DEVIATION_PIPE'", txt);
        Assert.Contains("'FUTURE_PT_PIPE'", txt);
        Assert.Contains("'DEVIATION_BLOCK'", txt);
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!;
        Assert.Equal(2, back.PtBars!.Count); Assert.Equal(2, back.Pipes!.Count); Assert.Equal(8, back.Tendons.First(t => t.Name == "T3").Lanes!.Count);
        var plain = SegmentalTests.Model().p.ToJson();
        foreach (var k in new[] { "PtBars", "\"Pipes\"", "DuctLanes", "\"Lanes\"" }) Assert.DoesNotContain(k, plain);
    }
}
