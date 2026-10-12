using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Generic expansion joint hardware (armor plates + retainer bars + studs, walkway cover plate + anchors), end block lateral limits, and ground
/// traffic barriers on a pier (OPTIONAL inputs; e.g. TxDOT SEJ-B / BS-EJCP / IGTS / T80SS).</summary>
public class JointHardwareTests
{
    private static ExpansionJointDefinition Joint(BridgeDefinition b, double gap) => new()
    {
        Type = ExpansionJointType.Sealed, GapAt50F = gap, BodyCentered = false, BlockoutWidth = 0, BlockoutDepth = In(4), SetDown = In(1), EdgeInset = In(10),
        Designation = "TEST SEJ",
        Armor = new JointArmorDefinition
        {
            Standard = "TEST", EdgeInsetLeft = In(10), EdgeInsetRight = In(10), StudLength = In(4), StudBentLength = In(4), StudBendDegrees = 18.4,
            StudSpacing = In(6), StudEndDistance = In(3), Unresolved = "UNRESOLVED (p.1): armor test - not given.",
        },
        CoverPlate = new JointCoverPlateDefinition { EdgeInsetLeft = In(10), EdgeInsetRight = In(10), EndClearance = In(1) },
        EndBlock = new DeckEndBlockDefinition { Depth = b.Deck.Thickness + In(2), FlatLength = Ft(4), ChamferRun = In(2), LeftOffset = -Ft(3), RightOffset = Ft(3), Source = "TEST IGTS" },
    };

    [Fact]
    public void Armor_Studs_CoverPlate_FromTheJointLine()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        double gap = In(2.5);
        b.Supports[0].ExpansionJoint = Joint(b, gap);
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        var c = ConcreteGenerator.Generate(f, p.Units);
        var j = Assert.Single(c.Joints);
        Assert.NotNull(j.HardwareAssembly);
        Assert.Equal(AssemblyKind.ExpansionJoint, j.HardwareAssembly!.Kind);
        var d = f.Deck!;
        // sample: 15 deg skewed deck end -> armor length along the joint ~ width between the insets / cos(15); exactly the joint (seal) length
        double width = j.Length;
        Assert.Equal((b.Deck.RightEdgeOffset - b.Deck.LeftEdgeOffset - 2 * In(10)) / Math.Cos(15 * Math.PI / 180), width, 2);
        var armor = j.Hardware.Where(x => x.Category == PartCategory.JointArmor).ToList();
        Assert.Equal(2, armor.Count);
        foreach (var a in armor)
        {
            Assert.Equal(width, a.Length, 3);
            // PL 1/2 x 4 + bar 1/4 x 1/2 swept along the joint
            Assert.Equal((In(0.5) * In(4) + In(0.25) * In(0.5)) * a.Length, a.Volume, 4);
            Assert.NotNull(a.Assumption);
        }
        // near plate: joint face on the deck-end line, back 1/2 in into the deck; far plate: joint face one opening beyond
        var near = armor.Single(x => x.Mark.EndsWith("-AN")); var far = armor.Single(x => x.Mark.EndsWith("-AF"));
        Assert.Equal(-In(0.25), near.Mesh.Positions.Min(v => d.FromBegin(v.XY)), 3);      // the retainer bar projects 1/4 in into the opening
        Assert.Equal(In(0.5), near.Mesh.Positions.Max(v => d.FromBegin(v.XY)), 3);
        Assert.Equal(-gap - In(0.5), far.Mesh.Positions.Min(v => d.FromBegin(v.XY)), 3);
        Assert.Equal(-gap + In(0.25), far.Mesh.Positions.Max(v => d.FromBegin(v.XY)), 3);
        // top of the plates on the finished top of slab
        // studs: n = ceil((L - 2 x 3") / 6") + 1 per plate, all behind their plate (away from the opening)
        int n = (int)Math.Ceiling((width - In(6)) / In(6) - 1e-9) + 1;
        var studs = j.Hardware.Where(x => x.Category == PartCategory.JointStud).ToList();
        Assert.Equal(2 * n, studs.Count);
        Assert.All(studs.Where(s => s.Mark.Contains("-SN")), s => Assert.True(s.Mesh.Positions.Min(v => d.FromBegin(v.XY)) > In(0.5) - 1e-3));
        Assert.All(studs.Where(s => s.Mark.Contains("-SF")), s => Assert.True(s.Mesh.Positions.Max(v => d.FromBegin(v.XY)) < -gap - In(0.5) + 1e-3));
        // cover plate: 1'-4 wide centred on the opening, length = between the insets less 1" each end, crowned 1/4"
        var cp = Assert.Single(j.Hardware, x => x.Category == PartCategory.JointCoverPlate);
        Assert.Equal(width - 2 * In(1), cp.Length, 3);
        double cmin = cp.Mesh.Positions.Min(v => d.FromBegin(v.XY)), cmax = cp.Mesh.Positions.Max(v => d.FromBegin(v.XY));
        Assert.Equal(Ft(1) + In(4), cmax - cmin, 3);
        Assert.Equal(-gap / 2, (cmax + cmin) / 2, 3);
        Assert.Equal(In(0.25) * (Ft(1) + In(4)) * cp.Length, cp.Volume, 4);          // the crown does not change the cross-section area
        int na = (int)Math.Ceiling((cp.Length - In(6)) / In(6) - 1e-9) + 1;
        Assert.Equal(na, j.Hardware.Count(x => x.Category == PartCategory.JointAnchor));
        // seal body between the curb insets, in the opening only
        Assert.True(j.Body!.Positions.All(v => d.FromBegin(v.XY) <= 1e-6 && d.FromBegin(v.XY) >= -gap - 1e-6));
        Assert.Equal(width, j.Length, 3);
        // end block between the lateral limits only, with its source in the name
        var eb = j.EndBlock!;
        Assert.Contains("TEST IGTS", eb.Name);
        Assert.All(eb.Mesh.Positions, v => Assert.InRange(p.Alignments[0].Inverse(v.XY).Offset, -Ft(3) - 1e-3, Ft(3) + 1e-3));
        double ebv = In(2) * (Ft(4) + In(1)) * Ft(6) / Math.Cos(15 * Math.PI / 180);  // 2" deeper over 4'-0 + the 45 deg chamfer (half of 2"), 6 ft wide (skewed line)
        Assert.InRange(eb.Volume / ebv, 0.99, 1.01);
        Assert.Contains(c.Diagnostics, x => x.Message.Contains("armor test - not given"));
    }

    [Fact]
    public void Hardware_MergedIntoSteelModel_NotStructuralSteel_RoundTrips()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        double before = new RegenerationCache().Regenerate(p)[0]!.Steel!.SteelMass;
        b.Supports[^1].ExpansionJoint = Joint(b, In(2.5));
        var m = new RegenerationCache().Regenerate(p)[0]!;
        Assert.Contains(m.Steel!.Assemblies, a => a.Kind == AssemblyKind.ExpansionJoint);
        Assert.Contains(m.Steel.Parts, x => x.Category == PartCategory.JointArmor);
        Assert.Equal(before, m.Steel.SteelMass, 6);          // joint hardware is not structural steel
        var back = BridgeProject.FromJson(p.ToJson());
        var bj = back.Bridges[0].Supports[^1].ExpansionJoint!;
        Assert.Equal(18.4, bj.Armor!.StudBendDegrees, 6);
        Assert.Equal(2, bj.CoverPlate!.AnchorRows.Count);
        Assert.Equal(Ft(3), bj.EndBlock!.RightOffset!.Value, 9);
        var json = SampleProject.Create().ToJson();
        Assert.DoesNotContain("\"Armor\"", json);
        Assert.DoesNotContain("\"CoverPlate\"", json);
        Assert.DoesNotContain("\"GroundBarriers\"", json);
        Assert.DoesNotContain("\"LeftOffset\"", json);
    }

    [Fact]
    public void GroundBarrier_SweptSection_TaperAndPavement()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var pier = b.Supports[1];
        var sd = pier.ResolvedSubstructure.Clone();
        sd.Enabled = true;
        // 1 ft x 3 ft rectangle; level 10 ft then a taper to 2 ft over 10 ft; the top 6" keep their shape
        var prof = new List<RailPoint> { new(0, 0), new(Ft(1), 0), new(Ft(1), Ft(3)), new(0, Ft(3)) };
        sd.GroundBarriers = new()
        {
            new GroundBarrierDefinition
            {
                Name = "B", TypeName = "TEST", Profile = prof, FullHeight = Ft(3), StretchAt = 0, TrafficSide = 1,
                Path = { new(Ft(-10), Ft(5), Ft(3)), new(Ft(0), Ft(5), Ft(3)), new(Ft(10), Ft(5), Ft(2)) },
                BaseAboveFoundation = Ft(2), PavementR0 = -In(1), PavementWidth = Ft(2), PavementThickness = Ft(1),
                Unresolved = "UNRESOLVED (p.1): base - not given.",
            },
        };
        pier.Substructure = sd;
        var m = new RegenerationCache().Regenerate(p)[0]!;
        var unit = m.Substructure!.Units.Single(u => u.Name == pier.Name);
        var bar = Assert.Single(unit.Solids, s => s.Kind == SubElementKind.TrafficBarrier);
        var pav = Assert.Single(unit.Solids, s => s.Kind == SubElementKind.PavementStrip);
        // 10 ft at 3 ft + 10 ft tapering 3 -> 2 ft (linear) of a 1 ft wide section
        Assert.Equal(Ft(1) * (Ft(10) * Ft(3) + Ft(10) * Ft(2.5)), bar.Mesh.Volume(), 4);
        Assert.Equal(Ft(2) * Ft(1) * Ft(20), pav.Mesh.Volume(), 4);
        var col = unit.Columns[0];
        Assert.Equal(col.BottomZ + Ft(2), bar.Mesh.Bounds().Min.Z, 6);
        Assert.Equal(col.BottomZ + Ft(2) + Ft(3), bar.Mesh.Bounds().Max.Z, 6);
        Assert.NotNull(bar.Assumption);
        Assert.NotNull(pav.Assumption);
    }
}
