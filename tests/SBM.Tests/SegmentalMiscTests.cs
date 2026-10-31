using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Substructure;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Segmental misc items (SegmentalDefinition.Misc): battered piles under a bent footing (head embedment, tip at the given elevation, plan positions at
/// the footing bottom), column centre offset, deck drains on the low side of every typical segment, modular joint envelope / blockout / end posts / cover
/// plates; IFC / JSON.</summary>
public class SegmentalMiscTests
{
    private static BridgeModel Generate(BridgeProject p) => new RegenerationCache().Regenerate(p).Single()!;

    private static (BridgeProject p, BridgeDefinition b) Model()
    {
        var (p, b) = SegmentalTests.Model();
        b.Segmental!.Misc = new SegmentalMiscDefinition
        {
            Foundations =
            {
                new SegmentalFoundationDefinition
                {
                    Bent = "S1", ColumnOffsetS = -In(10.375), PileSize = In(36), PileLabel = "36\" SQ PS", BatterRatio = 6, Embedment = Ft(4), TipElevation = -Ft(110),
                    PlanCount = 4, PlanTotalLength = Ft(460), Source = "test",
                    Piles =
                    {
                        new() { S = 0, N = 0 },                                      // plumb
                        new() { S = Ft(22.5), N = 0, BatterS = 1 },                  // battered toward +S
                        new() { S = -Ft(7.5), N = -Ft(15), BatterN = -1 },           // battered toward -N
                        new() { S = Ft(7.5), N = Ft(3.75), BatterS = 1, Embedment = In(15) },
                    },
                },
            },
            Drains = { new SegmentalDeckDrainDefinition { SegmentTypes = { SegmentType.Typical }, Diameter = In(6), EdgeDistance = Ft(1) + In(7.5), TopBelowSurface = In(0.5), Material = "PVC" } },
            Joints =
            {
                new SegmentalExpansionJointDefinition
                {
                    Name = "J0", Support = "S0", Designation = "modular", CentreFromSupport = -In(12.5), HalfWidth = Ft(20), BlockoutLength = Ft(2) + In(2), BlockoutDepth = Ft(1) + In(4), BlockoutEdgeInset = In(9),
                    EndPost = new SegmentalJointEndPost { Length = Ft(3.75), Width = Ft(1) + In(2), InsideFace = Ft(20), ParapetHeight = Ft(2.5), HeightAboveParapet = Ft(2) + In(2) },
                    CoverPlate = new SegmentalJointCoverPlate { Thickness = In(0.75), Height = Ft(4) + In(5), Bottom = In(3), LengthLeft = Ft(6) + In(1), LengthRight = Ft(6) + In(3), TowardUnit = Ft(3) + In(4.5), Material = "M270" },
                    Unresolved = { "UNRESOLVED (test): device by others." },
                },
            },
        };
        return (p, b);
    }

    private static bool Closed(Mesh m)
    {
        var id = new Dictionary<Vec3, int>();
        int Key(Vec3 v) { var k = new Vec3(Math.Round(v.X, 7), Math.Round(v.Y, 7), Math.Round(v.Z, 7)); if (!id.TryGetValue(k, out int i)) id[k] = i = id.Count; return i; }
        var edges = new Dictionary<(int, int), int>();
        for (int t = 0; t < m.Indices.Count; t += 3)
        {
            int a = Key(m.Positions[m.Indices[t]]), b = Key(m.Positions[m.Indices[t + 1]]), c = Key(m.Positions[m.Indices[t + 2]]);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) }) if (u != v) edges[(u, v)] = edges.GetValueOrDefault((u, v)) + 1;
        }
        return edges.All(e => e.Value == 1 && edges.GetValueOrDefault((e.Key.Item2, e.Key.Item1)) == 1);
    }

    [Fact]
    public void Piles_BatterFromTheFootingBottom_ToTheTipElevation()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var unit = m.Substructure!.Units.Single();
        var f = unit.Foundations.Single();
        Assert.Equal(4, f.Piles.Count);
        var piles = unit.Solids.Where(s => s.Kind == SubElementKind.Pile).ToList();
        Assert.Equal(4, piles.Count);
        Assert.All(piles, s => { Assert.True(Closed(s.Mesh), s.Name); Assert.True(s.Mesh.Volume() > 0, s.Name); });
        double bof = -Ft(0.5);
        var plumb = f.Piles[0];
        Assert.False(plumb.IsBattered);
        Assert.Equal(bof + Ft(4), plumb.TopZ, 9);
        Assert.Equal(-Ft(110), plumb.TipZ, 9);
        // battered toward +S at 2:12: head 4 ft / 6 inside the plan point, tip (head - tip) / 6 beyond the head
        var b1 = f.Piles[1];
        Assert.Equal(Ft(22.5) - Ft(4) / 6, b1.S, 9);
        Assert.Equal(b1.S + (b1.TopZ - b1.TipZ) / 6, b1.TipS, 9);
        Assert.Equal(b1.N, b1.TipN, 9);
        var b2 = f.Piles[2];
        Assert.True(b2.TipN < b2.N && Math.Abs(b2.TipS - b2.S) < 1e-9);
        Assert.Equal(bof + In(15), f.Piles[3].TopZ, 9);                                  // own embedment
        // the battered solid's volume = area x axis length
        double vol = piles[1].Mesh.Volume(), expect = In(36) * In(36) * b1.Length;
        Assert.True(Math.Abs(vol - expect) / expect < 1e-6, $"{vol} vs {expect}");
        var sm = m.Concrete!.Segmental!;
        Assert.Contains(sm.Checks, c => c.Item.Contains("pile count") && c.Difference == 0);
        Assert.Contains(sm.Checks, c => c.Item.Contains("not fully inside the footing") && c.Model == 0);
        // the column is centred on the offset
        var col = unit.Columns.Single();
        Assert.Equal(-In(10.375), col.S, 9);
        var cb = unit.Solids.Single(s => s.Kind == SubElementKind.Column).Mesh.Bounds();
        var c0 = unit.World(-In(10.375), 0, 0);
        Assert.True(Math.Abs((cb.Min.X + cb.Max.X) / 2 - c0.X) < 1e-6 && Math.Abs((cb.Min.Y + cb.Max.Y) / 2 - c0.Y) < 1e-6);
    }

    [Fact]
    public void HollowChamferedPile_VolumeIsOuterMinusVoid()
    {
        var (p, b) = Model();
        var fd = b.Segmental!.Misc!.Foundations[0];
        fd.PileChamfer = In(1); fd.PileVoidDiameter = In(21); fd.VoidSolidAtHead = Ft(13); fd.VoidSolidAtTip = Ft(11);
        var unit = Generate(p).Substructure!.Units.Single();
        var f = unit.Foundations.Single();
        var solid = unit.Solids.Where(s => s.Kind == SubElementKind.Pile).ToList();
        for (int i = 0; i < 2; i++)
        {
            double L = f.Piles[i].Length;
            double outer = (In(36) * In(36) - 2 * In(1) * In(1)) * L;
            double voidA = 0.5 * 32 * (In(10.5) * In(10.5)) * Math.Sin(2 * Math.PI / 32);
            double expect = outer - voidA * (L - Ft(24));
            Assert.True(Math.Abs(solid[i].Mesh.Volume() - expect) / expect < 1e-6, $"{solid[i].Mesh.Volume()} vs {expect}");
        }
    }

    [Fact]
    public void Drains_OneOnTheLowSideOfEveryTypicalSegment()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var misc = m.Substructure!.Misc!;
        var drains = misc.Elements.Where(e => e.Kind == MiscKind.DrainTrench).ToList();
        Assert.Equal(6, drains.Count);
        var al = p.Alignments[0];
        foreach (var d in drains)
        {
            Assert.True(Closed(d.Mesh), d.Name);
            var bb = d.Mesh.Bounds();
            var c = new Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2);
            var so = al.Inverse(c);
            // right side falls (-4.5 %): low side = right; CL 1'-7 1/2" from the 21'-3 1/2" edge across the sloped top
            Assert.True(so.Offset > 0, d.Name);
            Assert.True(Math.Abs(so.Offset - (Ft(21) + In(3.5) - Ft(1) - In(7.5))) < Ft(0.05), $"{d.Name} {so.Offset / Ft(1)}");
            Assert.True(bb.Max.Z < al.ElevationAt(so.Station, so.Offset), d.Name);       // top below the deck surface
        }
        Assert.Equal(6, misc.Assemblies.Count(a => a.Category == "deck drain"));
    }

    [Fact]
    public void Joint_EnvelopeBlockoutEndPostsAndCoverPlates()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sm = m.Concrete!.Segmental!;
        var misc = m.Substructure!.Misc!;
        var dev = misc.Elements.Single(e => e.Kind == MiscKind.JointDevice);
        Assert.True(dev.Placeholder);
        Assert.NotNull(dev.Assumption);
        Assert.True(Closed(dev.Mesh));
        // end segment face 10" past S0, CL joint 12 1/2" before it -> opening 45" (derived, flagged)
        var row = sm.Checks.Single(c => c.Item.Contains("J0: opening"));
        Assert.Equal(In(45), row.Model, 6);
        double vol = dev.Mesh.Volume();
        Assert.True(Math.Abs(vol - In(45) * Ft(40) * (Ft(1) + In(4))) / vol < 1e-3);
        Assert.Equal(2, misc.Elements.Count(e => e.Kind == MiscKind.JointEndPost));
        Assert.Equal(2, misc.Elements.Count(e => e.Kind == MiscKind.JointCoverPlate));
        foreach (var e in misc.Elements.Where(e => e.Group == MiscGroup.ExpansionJoint)) Assert.True(Closed(e.Mesh), e.Name);
        var ep = misc.Elements.First(e => e.Kind == MiscKind.JointEndPost);
        double epv = Ft(3.75) * (Ft(1) + In(2)) * (Ft(2) + In(2));
        Assert.True(Math.Abs(ep.Mesh.Volume() - epv) / epv < 1e-3);                     // along the graded tangent (2 %)
        var bo = sm.Openings.Single(o => o.Name.Contains("J0 blockout"));
        Assert.Equal("0-ES", bo.Segment);
        double box = (Ft(2) + In(2)) * (Ft(1) + In(4)) * 2 * (Ft(21) + In(3.5) - In(9));
        Assert.True(bo.RemovedVolume > 0.5 * box && bo.RemovedVolume < box, $"{bo.RemovedVolume} vs {box}");    // clipped to the 10" wing tips
    }

    [Fact]
    public void Ifc_And_Json()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "seg.ifc", p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        var lines = sw.ToString().Split('\n');
        Assert.Equal(4, lines.Count(l => l.Contains("=IFCPILE(")));
        Assert.Equal(6, lines.Count(l => l.Contains("=IFCWASTETERMINAL(")));
        Assert.Single(lines, l => l.Contains("=IFCDISCRETEACCESSORY(") && l.Contains("'EXPANSION_JOINT_DEVICE'"));
        Assert.Equal(2, lines.Count(l => l.Contains("=IFCPLATE(") && l.Contains("'JOINT_COVER_PLATE'")));
        Assert.Equal(2, lines.Count(l => l.Contains("=IFCRAILING(") && l.Contains("'PARAPET_END_POST'")));
        var back = BridgeProject.FromJson(p.ToJson());
        var md = back.Bridges[0].Segmental!.Misc!;
        Assert.Equal(4, md.Foundations[0].Piles.Count);
        Assert.Equal(In(15), md.Foundations[0].Piles[3].Embedment, 9);
        Assert.Equal(-In(12.5), md.Joints[0].CentreFromSupport, 9);
        var (plain, _) = SegmentalTests.Model();
        Assert.DoesNotContain("Foundations", plain.ToJson());
    }

    [Fact]
    public void Fixtures_WebDeckFloorFrames_And_StationRecessOpenings()
    {
        var (p, b) = Model();
        var first = Generate(p).Concrete!.Segmental!.Segments.First(x => x.Type == SegmentType.Typical);
        double sta = (first.Station0 + first.Station1) / 2;
        b.Segmental!.Misc!.Fixtures.Add(new SegmentalFixtureDefinition
        {
            Name = "lamp", Group = MiscGroup.Lighting, Category = "box girder lighting", Side = 1, Reference = SegmentalFixtureReference.WebInside, Stations = { sta },
            Unresolved = "UNRESOLVED (test): size",
            Pieces =
            {
                new() { Kind = MiscKind.Luminaire, A0 = -0.1, A1 = 0.1, N0 = 0, N1 = 0.2, Z0 = 1.5, Z1 = 1.7 },
                new() { Kind = MiscKind.PlatformMember, Section = "W8X18", A0 = -1, A1 = 1, N0 = 0.3, N1 = 0.3, Z0 = 1, Z1 = 1 },
            },
        });
        b.Segmental.Misc.Fixtures.Add(new SegmentalFixtureDefinition
        {
            Name = "door", Group = MiscGroup.Access, Side = 1, Reference = SegmentalFixtureReference.Floor, Stations = { sta },
            Pieces = { new() { Kind = MiscKind.AccessDoor, Section = "ROD 38", Z0 = 0, Z1 = In(3.0 / 16) } },
        });
        b.Segmental.Misc.Fixtures.Add(new SegmentalFixtureDefinition
        {
            Name = "platform", Group = MiscGroup.SolarPlatform, Side = -1, Reference = SegmentalFixtureReference.DeckEdge, Stations = { sta, -1e6 },
            Pieces = { new() { Kind = MiscKind.SolarArray, Placeholder = true, A0 = -1, A1 = 1, N0 = 0, N1 = 2, Z0 = -1, Z1 = 0 } },
        });
        b.Segmental.Openings ??= new();
        b.Segmental.Openings.Add(new() { Name = "survey disc", Kind = SegmentalOpeningKind.TopSlab, Width = In(3.5), Length = In(3.5), Depth = In(1), CenterOffset = Ft(18),
            Side = SegmentalDrainSide.HighSide, Stations = { sta } });
        b.Segmental.Openings.Add(new() { Name = "vent hole", Kind = SegmentalOpeningKind.BottomSlab, Diameter = In(2), CenterOffset = Ft(4) + In(1.5), FromSoffitCorner = true,
            Side = SegmentalDrainSide.LowSide, Segments = { first.Name } });
        var m = Generate(p);
        var misc = m.Substructure!.Misc!;
        var sm = m.Concrete!.Segmental!;
        var al = p.Alignments[0];
        var lamp = misc.Elements.Single(e => e.Kind == MiscKind.Luminaire);
        Assert.True(Closed(lamp.Mesh));
        Assert.Equal(0.2 * 0.2 * 0.2, lamp.Mesh.Volume(), 6);
        Assert.True(lamp.Assumption is { DimensionOnly: true });
        var lb = lamp.Mesh.Bounds();
        var lso = al.Inverse(new Vec2((lb.Min.X + lb.Max.X) / 2, (lb.Min.Y + lb.Max.Y) / 2));
        Assert.True(lso.Offset > Ft(5) && lso.Offset < Ft(15), $"{lso.Offset / Ft(1)}");     // inside the cell, right web
        var w = misc.Elements.Single(e => e.Kind == MiscKind.PlatformMember);
        Assert.True(Closed(w.Mesh));
        double aW = SBM.Core.Steel.SteelCatalog.FindWideFlange("W8X18")!.AiscArea;
        Assert.True(Math.Abs(w.Mesh.Volume() - aW * 2) / (aW * 2) < 0.03, $"{w.Mesh.Volume()} vs {aW * 2}");
        var door = misc.Elements.Single(e => e.Kind == MiscKind.AccessDoor);
        Assert.Equal(Math.PI * Math.Pow(In(19), 2) * In(3.0 / 16), door.Mesh.Volume(), 4);
        Assert.Single(misc.Elements, e => e.Kind == MiscKind.SolarArray);                     // the station outside the unit is skipped
        var pso = al.Inverse(new Vec2(
            (misc.Elements.Single(e => e.Kind == MiscKind.SolarArray).Mesh.Bounds().Min.X + misc.Elements.Single(e => e.Kind == MiscKind.SolarArray).Mesh.Bounds().Max.X) / 2,
            (misc.Elements.Single(e => e.Kind == MiscKind.SolarArray).Mesh.Bounds().Min.Y + misc.Elements.Single(e => e.Kind == MiscKind.SolarArray).Mesh.Bounds().Max.Y) / 2));
        Assert.True(pso.Offset < -(Ft(21) + In(3.5)), $"{pso.Offset / Ft(1)}");               // outboard of the left deck edge
        var sd = sm.Openings.Single(o => o.Name.EndsWith("survey disc"));
        Assert.Equal(In(3.5) * In(3.5) * In(1), sd.RemovedVolume, 9);
        var sb = sd.Mesh.Bounds();
        Assert.True(al.Inverse(new Vec2((sb.Min.X + sb.Max.X) / 2, (sb.Min.Y + sb.Max.Y) / 2)).Offset < -Ft(17.9));    // right side falls: high side = left
        var vent = sm.Openings.Single(o => o.Name.EndsWith("vent hole"));
        var vb = vent.Mesh.Bounds();
        Assert.True(al.Inverse(new Vec2((vb.Min.X + vb.Max.X) / 2, (vb.Min.Y + vb.Max.Y) / 2)).Offset > Ft(3));         // low side = right
        var sw = new StringWriter();
        IfcExporter.Export(sw, "seg.ifc", p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        var txt = sw.ToString();
        Assert.Contains("=IFCLIGHTFIXTURE(", txt);
        Assert.Contains("=IFCSOLARDEVICE(", txt);
        Assert.Contains("'ACCESS_DOOR'", txt);
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!;
        Assert.Equal(3, back.Misc!.Fixtures.Count);
        Assert.Equal(SegmentalFixtureReference.WebInside, back.Misc.Fixtures[0].Reference);
        Assert.Equal(SegmentalDrainSide.HighSide, back.Openings!.Single(o => o.Name == "survey disc").Side);
    }
}
