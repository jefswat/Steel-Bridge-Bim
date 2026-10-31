using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using Xunit.Abstractions;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Skewed stiffeners bevelled to the web / flange faces and the per-face fit conditions (<see cref="PartFit"/>, plan inputs only).</summary>
public class StiffenerFitTests
{
    private readonly ITestOutputHelper _out;
    public StiffenerFitTests(ITestOutputHelper o) => _out = o;

    private static readonly Vec3 Up = new(0, 0, 1);

    private static (BridgeFraming f, SteelModel m) Build(double skewDeg, Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        foreach (var s in b.Supports) s.SkewDegrees = skewDeg;
        b.CrossFrames.Orientation = CrossFrameOrientation.ParallelToSkew;
        tweak?.Invoke(b);
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        return (f, SteelModelGenerator.Generate(f, p.Units));
    }

    /// <summary>Spread of the web-side vertices' distance from the web CL plane and of the top / bottom end vertices' distance from the flange plane
    /// (all ~0 when the plate sits flat on the web and the flanges).</summary>
    private static (double web, double top, double bottom) Spread(BridgeFraming f, StiffenerResult s, SteelPart part)
    {
        var g = f.Girders[s.Girder];
        var (p, t, _) = g.Path.At(s.GirderDistance);
        var right = new Vec3(t.Right.X, t.Right.Y, 0);
        var t3 = new Vec3(t.X, t.Y, g.Path.GradeAt(s.GirderDistance)).Normalized();
        var fn = (Up - t3 * Vec3.Dot(Up, t3)).Normalized();
        var dr = part.Mesh.Positions.Select(q => Math.Abs(Vec3.Dot(q - p, right))).ToList();
        double rmin = dr.Min();
        var web = dr.Where(x => x < rmin + In(0.75)).ToList();
        var dz = part.Mesh.Positions.Select(q => Vec3.Dot(q - p, fn)).ToList();
        double zmax = dz.Max(), zmin = dz.Min();
        var top = dz.Where(x => x > zmax - In(0.2)).ToList();
        var bot = dz.Where(x => x < zmin + In(0.2)).ToList();
        return (web.Max() - web.Min(), top.Max() - top.Min(), bot.Max() - bot.Min());
    }

    private static SteelPart PartOf(SteelModel m, StiffenerResult s, PartCategory cat) =>
        m.Parts.Where(x => x.Category == cat).OrderBy(x =>
        {
            var b = x.Mesh.Bounds();
            return (new Vec2((b.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2) - new Vec2(s.Location.X, s.Location.Y)).Length;
        }).First();

    [Fact]
    public void SkewedStiffeners_SitFlatOnWebAndFlanges_30Deg()
    {
        var (f, m) = Build(30);
        foreach (var kind in new[] { StiffenerKind.Bearing, StiffenerKind.ConnectionPlate })
        {
            var s = f.Stiffeners.First(x => x.Kind == kind && x.SkewAngle > Numerics.DegToRad(10));
            var part = PartOf(m, s, kind == StiffenerKind.Bearing ? PartCategory.BearingStiffener : PartCategory.ConnectionStiffener);
            var (w, tp, bt) = Spread(f, s, part);
            _out.WriteLine($"{kind} {part.Mark}: skew {Numerics.RadToDeg(s.SkewAngle):F2} deg, t {s.Plate.Thickness / 0.0254:F3} in: web edge {w * 1000:F4} mm, top {tp * 1000:F4} mm, bottom {bt * 1000:F4} mm");
            Assert.True(w < 1e-5, $"{part.Mark} web edge spread {w * 1000:F4} mm");
            Assert.True(tp < 1e-5, $"{part.Mark} top end spread {tp * 1000:F4} mm");
            Assert.True(bt < 1e-5, $"{part.Mark} bottom end spread {bt * 1000:F4} mm");
            Assert.Contains("bevelled", part.Description);
            Assert.True(Math.Abs(part.Mesh.Volume()) > 0);
            // weld roots on the bevelled contact lines: the web weld bead touches the web face (distance tw/2 from the CL) on both plate faces
            var weld = m.Parts.First(x => x.Mark == part.Mark + "-W");
            var (p, t, _) = f.Girders[s.Girder].Path.At(s.GirderDistance);
            var right = new Vec3(t.Right.X, t.Right.Y, 0);
            double wmin = weld.Mesh.Positions.Min(q => Math.Abs(Vec3.Dot(q - p, right)));
            double pmin = part.Mesh.Positions.Min(q => Math.Abs(Vec3.Dot(q - p, right)));
            Assert.Equal(pmin, wmin, 5);
        }
    }

    /// <summary>Edges (welded by position, 1e-9 m) not shared by exactly two triangles.</summary>
    private static int OpenEdges(Mesh m)
    {
        var key = new Dictionary<(long, long, long), int>();
        int Id(Vec3 p)
        {
            var k = ((long)Math.Round(p.X * 1e9), (long)Math.Round(p.Y * 1e9), (long)Math.Round(p.Z * 1e9));
            if (!key.TryGetValue(k, out int i)) key[k] = i = key.Count;
            return i;
        }
        var count = new Dictionary<(int, int), int>();
        for (int t = 0; t < m.TriangleCount; t++)
        {
            int a = Id(m.Positions[m.Indices[3 * t]]), b = Id(m.Positions[m.Indices[3 * t + 1]]), c = Id(m.Positions[m.Indices[3 * t + 2]]);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                var e = u < v ? (u, v) : (v, u);
                count[e] = count.GetValueOrDefault(e) + 1;
            }
        }
        return count.Values.Count(x => x != 2);
    }

    [Fact]
    public void BevelledStiffeners_AreClosedSolids()
    {
        var (_, m) = Build(30);
        var parts = m.Parts.Where(x => x.Description.Contains("bevelled")).ToList();
        Assert.NotEmpty(parts);
        foreach (var p in parts) Assert.True(OpenEdges(p.Mesh) == 0, $"{p.Mark}: {OpenEdges(p.Mesh)} open edges");
    }

    [Fact]
    public void SquareStiffeners_Unchanged_ExactExtrusions()
    {
        var (f, m) = Build(0);
        // normal to the flanges (connection / intermediate): square, exact extrusions
        var parts = m.Parts.Where(x => x.Category is PartCategory.ConnectionStiffener or PartCategory.IntermediateStiffener).ToList();
        Assert.NotEmpty(parts);
        Assert.All(parts, x => Assert.DoesNotContain("bevelled", x.Description));
        Assert.All(parts, x => Assert.True(x.Mesh.HasExactSolids, x.Mark));
        // plumb bearing stiffeners on a grade: the ends are bevelled to the sloped flanges (user decision 2026-10-28), square where the girder is level
        var grade = m.Parts.Where(x => x.Category == PartCategory.BearingStiffener).ToList();
        Assert.NotEmpty(grade);
        Assert.Contains(grade, x => x.Description.Contains("bevelled"));
    }

    [Fact]
    public void NoFitInput_NoFitRecorded_PotentialNoncomplianceFlagged()
    {
        var (_, m) = Build(15);
        var bs = m.Parts.First(x => x.Category == PartCategory.BearingStiffener);
        var cs = m.Parts.First(x => x.Category == PartCategory.ConnectionStiffener);
        var ist = m.Parts.First(x => x.Category == PartCategory.IntermediateStiffener);
        foreach (var x in new[] { bs, cs, ist })
        {
            Assert.Null(x.Fits);                                                  // nothing assumed
            Assert.NotNull(x.Assumption);
            Assert.Equal(StiffenerFits.NoncomplianceStatus, x.Assumption!.Status);
            Assert.True(x.Assumption.DimensionOnly);                             // stays on its layer
        }
        Assert.Contains("6.10.11.2", bs.Assumption!.Reason);
        Assert.Contains("6.6.1.3.1", cs.Assumption!.Reason);
        Assert.Contains("6.10.11.1", ist.Assumption!.Reason);
        Assert.Contains(m.Diagnostics, d => d.Message.Contains(StiffenerFits.NoncomplianceStatus) && d.Message.Contains("6.10.11.2"));
        Assert.DoesNotContain(m.Diagnostics, d => d.Message.Contains("UNRESOLVED (inputs checked"));
    }

    [Fact]
    public void FitInputs_RecordedPerFace_WeldsFollowThem()
    {
        var (_, m0) = Build(15);
        var (_, m) = Build(15, b => b.Stiffeners.EndFits = new()
        {
            new(StiffenerFitKind.Bearing, StiffenerEnd.Bottom, FitCondition.MillToBear, "test p.1"),
            new(StiffenerFitKind.Bearing, StiffenerEnd.Bottom, FitCondition.Welded, "test p.1"),
            new(StiffenerFitKind.Bearing, StiffenerEnd.Top, FitCondition.TightFit, "test p.2"),
        });
        var bs = m.Parts.First(x => x.Category == PartCategory.BearingStiffener);
        Assert.NotNull(bs.Fits);
        Assert.Contains(bs.Fits!, x => x.Face == "bottom end" && x.Condition == FitCondition.MillToBear && x.Source == "test p.1" && x.Against!.Contains("-BF"));
        Assert.Contains(bs.Fits!, x => x.Face == "top end" && x.Condition == FitCondition.TightFit && x.Against!.Contains("-TF"));
        Assert.Null(bs.Assumption);                                               // bearing ends given: no flag (top has no AASHTO requirement anyway)
        // tight fit only at the top: no top flange weld -> shorter weld than the default
        var w0 = m0.Parts.First(x => x.Mark == bs.Mark + "-W");
        var w1 = m.Parts.First(x => x.Mark == bs.Mark + "-W");
        Assert.True(w1.Length < w0.Length - 0.05, $"{w1.Length} vs {w0.Length}");
        // the part each fit is against exists
        Assert.All(bs.Fits!, x => Assert.Contains(m.Parts, q => q.Mark == x.Against));
    }

    [Fact]
    public void FitInputs_WrittenToIfc_AsFitConditionPset()
    {
        var p = SampleProject.Create();
        p.Bridges[0].Stiffeners.EndFits = new() { new(StiffenerFitKind.Bearing, StiffenerEnd.Bottom, FitCondition.MillToBear, "test p.9") };
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var s = SteelModelGenerator.Generate(f, p.Units);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "test.ifc", p, new[] { new IfcBridgeData(f, s) });
        string text = sw.ToString();
        Assert.Contains("'SBM_FitConditions'", text);
        Assert.Contains("Mill to bear: bottom end against", text);
        Assert.Contains("POTENTIAL NONCOMPLIANCE", text);                         // connection plates / intermediate stiffeners: no input
    }

    [Fact]
    public void FitInputs_RoundTrip_OmittedWhenNull()
    {
        var p = SampleProject.Create();
        string j0 = p.ToJson();
        Assert.DoesNotContain("EndFits", j0);
        Assert.DoesNotContain("BearingStiffenerFits", j0);
        p.Bridges[0].Stiffeners.EndFits = new() { new(StiffenerFitKind.Intermediate, StiffenerEnd.Top, FitCondition.TightFit, "p.32") };
        p.Bridges[0].Supports[0].BearingStiffenerFits = new() { new(StiffenerFitKind.Jacking, StiffenerEnd.Bottom, FitCondition.FinishToBear, "p.31") };
        var q = BridgeProject.FromJson(p.ToJson());
        var e = Assert.Single(q.Bridges[0].Stiffeners.EndFits!);
        Assert.Equal((StiffenerFitKind.Intermediate, StiffenerEnd.Top, FitCondition.TightFit, "p.32"), (e.Kind, e.End, e.Condition, e.Source));
        var sf = Assert.Single(q.Bridges[0].Supports[0].BearingStiffenerFits!);
        Assert.Equal(FitCondition.FinishToBear, sf.Condition);
        // jacking stiffeners take the bearing entries when none is given for them; support entries win over the bridge's
        var sd = new StiffenerDefinition { EndFits = new() { new(StiffenerFitKind.Bearing, StiffenerEnd.Bottom, FitCondition.MillToBear, "b") } };
        Assert.Equal("b", StiffenerFits.Of(sd, null, StiffenerFitKind.Jacking, StiffenerEnd.Bottom).Single().Source);
        Assert.Equal("p.31", StiffenerFits.Of(sd, q.Bridges[0].Supports[0], StiffenerFitKind.Jacking, StiffenerEnd.Bottom).Single().Source);
        Assert.Empty(StiffenerFits.Of(sd, null, StiffenerFitKind.Bearing, StiffenerEnd.Top));
    }
}
