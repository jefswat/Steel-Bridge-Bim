using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Railing;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Generic pedestrian railing generator (posts, rails, base plates, anchors, envelope) on curbs; sealed joints without blockout; mow strips.</summary>
public class PedestrianRailingTests
{
    private static readonly double CurbH = In(4), CurbW = In(10);

    /// <summary>Sample bridge with a 10 x 4 in curb at both deck edges and the given railing runs.</summary>
    private static (BridgeFraming f, ConcreteModel c, BridgeProject p) Build(params PedestrianRailingDefinition[] rails)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var bar = b.Deck.Barrier;
        bar.Type = BarrierType.Custom; bar.IsCurb = true; bar.Reinforcement = false;
        bar.Profile = new() { new(CurbW, 0), new(CurbW, CurbH), new(0, CurbH), new(0, 0) };
        b.Railings = rails.ToList();
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        Assert.False(f.HasErrors);
        return (f, ConcreteGenerator.Generate(f, p.Units), p);
    }

    private static PedestrianRailingDefinition Rail(BridgeDefinition? b, double offset) => new()
    {
        Name = "Rail RT", TypeName = "TEST RAIL", Offset = offset, BaseProbe = In(5),
        Envelope = new() { new(In(2), 0), new(In(8), 0), new(In(8), In(40)), new(In(2), In(40)) }, EnvelopeMassPerLength = 74.4,
        Post = new RailPostDefinition
        {
            Profile = new() { new(In(2), In(0.75)), new(In(8), In(0.75)), new(In(8), In(42)), new(In(3), In(42)) },
            Thickness = In(0.75), Spacing = Ft(8), EndDistance = Ft(1), Description = "PL 3/4 post",
            BasePlate = new RailBasePlateDefinition
            {
                Length = In(8), Width = In(7), Thickness = In(0.75), CenterR = In(5), AnchorDiameter = In(0.75), AnchorEmbedment = In(6),
                Anchors = { new(-In(2.5), -In(2.5)), new(In(2.5), -In(2.5)), new(-In(2.5), In(2.5)), new(In(2.5), In(2.5)) },
            },
        },
        Members = { new RailMemberDefinition { Name = "HR", Kind = RailMemberKind.Handrail, Diameter = In(1.9), Center = new(In(0.5), In(32)) },
                    new RailMemberDefinition { Name = "R1", Kind = RailMemberKind.Rail, Width = In(1), Height = In(2), Center = new(In(5), In(20)) } },
    };

    [Fact]
    public void Railing_RunsBetweenDeckEnds_PostsEquallySpaced_OnTheCurbTop()
    {
        var probe = SampleProject.Create().Bridges[0];
        double off = probe.Deck.RightEdgeOffset - CurbW;          // reference line = curb line (inner face)
        var (f, c, p) = Build(Rail(null, off));
        var rm = PedestrianRailingGenerator.Generate(f, c, p.Units);
        Assert.DoesNotContain(rm.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        var run = Assert.Single(rm.Runs);
        // ends on the deck end lines (on the reference line)
        Assert.True(Math.Abs(f.Deck!.FromBegin(run.Line[0].XY)) < 1e-4);
        Assert.True(Math.Abs(f.Deck.FromEnd(run.Line[^1].XY)) < 1e-4);
        // length = the reference line length (sampled polyline is a lower bound within a few mm)
        double poly = run.Line.Zip(run.Line.Skip(1), (a, b) => (b.XY - a.XY).Length).Sum();
        Assert.InRange(run.Length - poly, -1e-6, 0.01);
        // posts: n = ceil((L - 2e) / s) + 1, equal spacing <= s, first / last at e from the ends
        int n = (int)Math.Ceiling((run.Length - 2 * Ft(1)) / Ft(8)) + 1;
        Assert.Equal(n, run.Posts);
        var posts = rm.Parts.Where(x => x.Category == PartCategory.RailPost).ToList();
        Assert.Equal(n, posts.Count);
        Assert.Equal(n, rm.Parts.Count(x => x.Category == PartCategory.RailBasePlate));
        Assert.Equal(4 * n, rm.Bolts.Count(x => x.Kind == BoltKind.Anchor));
        // base on the curb top: the base plate bottom is the top of concrete under the probe point
        var top = new TopSurface(c.Barriers.Select(x => x.Mesh).Append(c.Deck!));
        foreach (var plate in rm.Parts.Where(x => x.Category == PartCategory.RailBasePlate))
        {
            var bb = plate.Mesh.Bounds();
            var ctr = new Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2);
            Assert.NotNull(top.At(ctr));
            Assert.Equal(top.At(ctr)!.Value, bb.Min.Z, 2);            // 1 cm: the curb top slopes with the grade under the plate
            Assert.Equal(In(0.75) * In(7) * In(8), plate.Volume, 6);
        }
        // post volume = profile area x thickness
        double area = Math.Abs(MeshBuilderArea(Rail(null, off).Post!.Profile));
        foreach (var post in posts) Assert.Equal(area * In(0.75), post.Volume, 7);
        // envelope mass by length; handrail and rail members along the run
        var env = rm.Parts.Single(x => x.Category == PartCategory.RailEnvelope);
        Assert.Equal(74.4 * run.Length, env.Mass, 3);
        Assert.Single(rm.Parts, x => x.Category == PartCategory.RailHandrail);
        Assert.Single(rm.Parts, x => x.Category == PartCategory.RailMember);
        var asm = Assert.Single(rm.Assemblies);
        Assert.Equal(AssemblyKind.Railing, asm.Kind);
        Assert.Equal("GUARDRAIL", asm.PredefinedType);
        Assert.Equal("TEST RAIL", asm.ObjectType);
    }

    [Fact]
    public void Railing_PostsOnAlignmentGrid_FabricPanelPerBay_SeeThroughMass()
    {
        var probe = SampleProject.Create().Bridges[0];
        double off = probe.Deck.RightEdgeOffset - CurbW;
        var r = Rail(null, off);
        r.Envelope = null; r.EnvelopeMassPerLength = 0;
        r.Members.Clear();
        double g0 = probe.Supports[0].Station + Ft(10);
        r.Post!.BasePlate = null; r.Post.GridStation = g0; r.Post.Spacing = Ft(8); r.Post.Thickness = In(6);
        double th = In(0.3), mpa = 1.95;
        r.Fabric = new RailFabricDefinition
        {
            Description = "test fabric", Path = new() { new(In(8), 0), new(In(8), In(42)) }, OffsetFromPost = th / 2, BottomH = 0, Thickness = th, MassPerArea = mpa,
            Unresolved = "UNRESOLVED (test): type", Basis = "test", Material = "Chain link",
        };
        r.FlagsDimensionOnly = true;
        r.Post.Unresolved = "UNRESOLVED (test): thickness"; r.Post.Basis = "test";
        var (f, c, p) = Build(r);
        var rm = PedestrianRailingGenerator.Generate(f, c, p.Units);
        var run = Assert.Single(rm.Runs);
        // posts on the 8'-0 grid of CL stations, k integer, inside the run
        Assert.True(run.Posts >= 3);
        foreach (double st in run.PostStations) Assert.True(Math.Abs(((st - g0) / Ft(8)) - Math.Round((st - g0) / Ft(8))) < 1e-3);
        for (int i = 1; i < run.PostStations.Count; i++) Assert.Equal(Ft(8), run.PostStations[i] - run.PostStations[i - 1], 3);
        // one panel per bay (both end bays; a post exactly on an end would drop one)
        var fab = rm.Parts.Where(x => x.Category == PartCategory.RailFabric).ToList();
        Assert.InRange(fab.Count, run.Posts - 1, run.Posts + 1);
        Assert.Equal(fab.Count, run.FabricPanels);
        // mass = area x mass per area; area = panel height x its length (the curved line adds a little)
        Assert.Equal(fab.Sum(x => x.Mass), run.FabricMass, 6);
        Assert.Equal(mpa * run.FabricArea, run.FabricMass, 3);
        Assert.InRange(run.FabricArea, 0.9 * In(42) * (run.Length - run.Posts * In(6)), 1.01 * In(42) * run.Length);
        // flags: dimension-only (normal layer), fabric and posts
        Assert.All(fab, x => Assert.True(x.Assumption is { DimensionOnly: true }));
        Assert.All(rm.Parts.Where(x => x.Category == PartCategory.RailPost), x => Assert.True(x.Assumption is { DimensionOnly: true }));
        // the fabric stands on the outer face of the posts (R >= 8 in)
        foreach (var v in fab[0].Mesh.Positions) Assert.True(f.Alignment.Inverse(v.XY).Offset >= off + In(8) - 1e-3);
        Assert.True(SteelModel.IsRailing(PartCategory.RailFabric));
    }

    [Fact]
    public void Railing_FabricMassNotCounted_KeepsPanelsAndAreaButNoMass()
    {
        var probe = SampleProject.Create().Bridges[0];
        double off = probe.Deck.RightEdgeOffset - CurbW;
        RailingModel Gen(bool notCounted)
        {
            var r = Rail(null, off);
            r.Envelope = null; r.EnvelopeMassPerLength = 0; r.Members.Clear();
            r.Post!.BasePlate = null; r.Post.GridStation = probe.Supports[0].Station + Ft(10); r.Post.Spacing = Ft(8); r.Post.Thickness = In(6);
            r.Fabric = new RailFabricDefinition
            {
                Description = "test fabric", Path = new() { new(In(8), 0), new(In(8), In(42)) }, OffsetFromPost = In(0.15), BottomH = 0, Thickness = In(0.3), MassPerArea = 1.95,
                Material = "Chain link", MassNotCounted = notCounted,
            };
            var (f, c, p) = Build(r);
            return PedestrianRailingGenerator.Generate(f, c, p.Units);
        }
        var counted = Gen(false); var not = Gen(true);
        var rc = Assert.Single(counted.Runs); var rn = Assert.Single(not.Runs);
        Assert.True(rc.FabricMass > 0);
        Assert.Equal(rc.FabricPanels, rn.FabricPanels);
        Assert.Equal(rc.FabricArea, rn.FabricArea, 6);
        Assert.Equal(0, rn.FabricMass);
        Assert.Equal(rc.Mass - rc.FabricMass, rn.Mass, 6);
        Assert.All(not.Parts.Where(x => x.Category == PartCategory.RailFabric), x => Assert.Equal(0, x.Mass));
        // default is omitted from the JSON
        Assert.DoesNotContain("MassNotCounted", System.Text.Json.JsonSerializer.Serialize(counted.Runs.Count > 0 ? new RailFabricDefinition() : null));
        Assert.Contains("MassNotCounted", System.Text.Json.JsonSerializer.Serialize(new RailFabricDefinition { MassNotCounted = true }));
    }

    private static double MeshBuilderArea(List<RailPoint> pts) => SBM.Core.Solids.MeshBuilder.SignedArea(pts.Select(q => new Vec2(q.R, q.H)).ToList());

    [Fact]
    public void Railing_StationLimitsClearanceAndLeftSide()
    {
        var probe = SampleProject.Create().Bridges[0];
        double s0 = probe.Supports[0].Station + Ft(30), s1 = probe.Supports[1].Station;
        var r = Rail(null, probe.Deck.LeftEdgeOffset + CurbW);
        r.BeginStation = s0; r.EndStation = s1; r.EndClearance = In(1); r.Post = null;
        var (f, c, p) = Build(r);
        var rm = PedestrianRailingGenerator.Generate(f, c, p.Units);
        var run = Assert.Single(rm.Runs);
        Assert.InRange(run.BeginStation - s0, In(0.9), In(1.1));       // the 1" clearance along the (offset) line moves the stations by ~1"
        Assert.InRange(s1 - run.EndStation, In(0.9), In(1.1));
        // left side: the envelope stands outward = left of the reference line
        var env = rm.Parts.Single(x => x.Category == PartCategory.RailEnvelope);
        var a = f.Alignment;
        var bb = env.Mesh.Bounds();
        var mid = new Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2);
        foreach (var v in env.Mesh.Positions) Assert.True(a.Inverse(v.XY).Offset <= r.Offset - In(2) + 1e-4);
        Assert.Equal(0, run.Posts);
    }

    [Fact]
    public void Railing_MergedIntoSteelModel_NotStructuralSteel_RoundTrips()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        b.Railings = new() { Rail(b, b.Deck.RightEdgeOffset - In(6)) };
        var cache = new RegenerationCache();
        var m = cache.Regenerate(p)[0]!;
        Assert.NotNull(m.Steel!.Railings);
        Assert.Contains(m.Steel.Parts, x => x.Category == PartCategory.RailPost);
        double structural = m.Steel.Parts.Where(x => !SteelModel.IsRailing(x.Category) && x.Category is not (PartCategory.BearingPad or PartCategory.Weld or PartCategory.BearingDisc or PartCategory.Ptfe or PartCategory.CableConnector or PartCategory.PtStrand or PartCategory.PtAnchorage)).Sum(x => x.Mass);
        Assert.Equal(structural, m.Steel.SteelMass, 6);
        // .sbm round trip keeps the railing; a project without railings writes no "Railings" key
        var back = BridgeProject.FromJson(p.ToJson());
        Assert.Equal(b.Railings[0].Post!.BasePlate!.Anchors.Count, back.Bridges[0].Railings![0].Post!.BasePlate!.Anchors.Count);
        Assert.Equal(b.Railings[0].Envelope!.Count, back.Bridges[0].Railings![0].Envelope!.Count);
        Assert.DoesNotContain("\"Railings\"", SampleProject.Create().ToJson());
        Assert.DoesNotContain("\"IsCurb\"", SampleProject.Create().ToJson());
    }

    [Fact]
    public void Railing_WithoutConcreteUnderIt_IsReportedNotBuilt()
    {
        var probe = SampleProject.Create().Bridges[0];
        var (f, c, p) = Build(Rail(null, probe.Deck.RightEdgeOffset + Ft(3)));   // 3 ft outside the deck edge
        var rm = PedestrianRailingGenerator.Generate(f, c, p.Units);
        Assert.Empty(rm.Runs);
        Assert.Contains(rm.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("no concrete"));
    }

    [Fact]
    public void SealedJoint_NoBlockout_CenteredBody()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        b.Supports[0].ExpansionJoint = new ExpansionJointDefinition
        {
            Type = ExpansionJointType.Sealed, GapAt50F = In(4), BodyCentered = true, BlockoutWidth = 0, BlockoutDepth = b.Deck.Thickness, SetDown = 0, EdgeInset = 0,
            Designation = "TEST SEJ", PlanLength = 1, Unresolved = "UNRESOLVED (p.1): test - not given.",
        };
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        var c = ConcreteGenerator.Generate(f, p.Units);
        var j = Assert.Single(c.Joints);
        Assert.True(j.Blockout.IsEmpty);
        Assert.NotNull(j.Body);
        Assert.NotNull(j.Assumption);
        // body straddles the deck end line: half the gap each side
        var d = f.Deck!;
        double min = j.Body!.Positions.Min(v => d.FromBegin(v.XY)), max = j.Body.Positions.Max(v => d.FromBegin(v.XY));
        Assert.Equal(-In(2), min, 3);
        Assert.Equal(In(2), max, 3);
        Assert.True(j.Length > b.Deck.RightEdgeOffset - b.Deck.LeftEdgeOffset - 0.01);
        Assert.Contains(c.Diagnostics, x => x.Message.Contains("test - not given"));
    }

    [Fact]
    public void MowStrip_RingVolumeAroundRectangularColumn()
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var pier = b.Supports[1];
        var sd = pier.ResolvedSubstructure.Clone();
        sd.Enabled = true; sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Ft(3); sd.ColumnDepth = Ft(4);
        sd.MowStrip = new MowStripDefinition { Width = Ft(2), Thickness = In(4), TopAboveFoundation = Ft(1), Unresolved = "UNRESOLVED (p.1): ground - not given." };
        pier.Substructure = sd;
        var m = new RegenerationCache().Regenerate(p)[0]!;
        var unit = m.Substructure!.Units.Single(u => u.Name == pier.Name);
        var strips = unit.Solids.Where(s => s.Kind == SubElementKind.MowStrip).ToList();
        Assert.Equal(unit.Columns.Count, strips.Count);
        double v = (Ft(7) * Ft(8) - Ft(3) * Ft(4)) * In(4);
        foreach (var s in strips) { Assert.Equal(v, s.Mesh.Volume(), 6); Assert.NotNull(s.Assumption); }
        var col = unit.Columns[0];
        Assert.Equal(col.BottomZ + Ft(1), strips[0].Mesh.Bounds().Max.Z, 6);
    }
}
