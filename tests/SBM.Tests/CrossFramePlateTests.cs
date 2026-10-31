using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using Xunit.Abstractions;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Cross frame connection plates on the frame plane (default since 2026-10-27): location defined at the reference girder's top of web, every
/// plate where the frame plane meets its girder, legacy switch <see cref="CrossFrameDefinition.PlatesAtEachTopOfWeb"/>, frame plane modes (Midway default,
/// Warped = twisted members).</summary>
public class CrossFramePlateTests
{
    private readonly ITestOutputHelper _out;
    public CrossFramePlateTests(ITestOutputHelper o) => _out = o;

    /// <summary>Sample bridge on a straight alignment with a constant grade (or a crest vertical curve), a constant cross slope and skewed supports.</summary>
    private static BridgeProject Graded(double grade, double crossSlope, double skew, bool crest = false, Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        var pis = new PiDefinition { Pis = { new PiRow(Ft(5000), Ft(10000)), new PiRow(Ft(5000), Ft(14000)) } };
        var al = new Alignment(pis.Build(out _)) { Name = p.Alignments[0].Name, StartStation = 0, PiDefinition = pis };
        al.Profile = crest
            ? new VerticalProfile { Vpis = { new Vpi(0, Ft(500)), new Vpi(Ft(2000), Ft(500) + Ft(2000) * 0.05, Ft(1200)), new Vpi(Ft(4000), Ft(500)) } }
            : new VerticalProfile { Vpis = { new Vpi(0, Ft(500)), new Vpi(Ft(4000), Ft(500) + Ft(4000) * grade) } };
        al.CrossSlope = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, crossSlope, -crossSlope) } };
        p.Alignments[0] = al;
        var b = p.Bridges[0];
        foreach (var s in b.Supports) s.SkewDegrees = skew;
        b.Bearings.Type = BearingType.Elastomeric;
        b.CrossFrames.ReportTwist = true;
        tweak?.Invoke(b);
        return p;
    }

    private static (BridgeFraming f, SteelModel m) Gen(BridgeProject p)
    {
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        return (f, SteelModelGenerator.Generate(f, p.Units));
    }

    private const double Tol = 1e-5;   // 0.01 mm

    [Fact]
    public void ConstantGrade_CrossSlope_Skew_PlatesOnFramePlane_ZeroGapAtEveryBolt()
    {
        // constant grade and cross slope, straight: both plates of a frame have the same up axis (no true twist), so a flat plate pair contains the
        // frame plane at every bolt
        var p = Graded(0.04, 0.03, 15);
        var (f, m) = Gen(p);
        var inter = f.CrossFrames.Where(c => !c.AtSupport).ToList();
        Assert.NotEmpty(inter);
        Assert.All(inter, c => Assert.True(c.PlatesOnFramePlane));
        Assert.Contains(inter, c => Math.Abs(c.LeftShift) + Math.Abs(c.RightShift) > In(0.01));   // the far plates did move
        int bolts = 0;
        foreach (var r in m.CrossFrameFit!.Rows.Where(r => !r.AtSupport))
        {
            Assert.True(Math.Abs(r.TwistDegrees) < 1e-6, $"{r.Mark} twist {r.TwistDegrees}");
            Assert.True(r.MaxGapBuilt < Tol, $"{r.Mark}: gap {r.MaxGapBuilt * 1000:0.000} mm");
            Assert.True(Math.Abs(r.OffsetL) < Tol && Math.Abs(r.OffsetR) < Tol, $"{r.Mark}: plate offset {r.OffsetL} / {r.OffsetR}");
            Assert.Equal("midway", r.BuiltPlane);
            bolts += r.BoltsL + r.BoltsR;
        }
        Assert.True(bolts > 50);
        Assert.Contains(f.Diagnostics, d => d.Message.Contains("connection plates on the frame plane"));

        // the same bridge with the legacy switch: plates at each top-of-web crossing, off the frame plane by ~ top of web difference x grade
        var (fl, ml) = Gen(Graded(0.04, 0.03, 15, tweak: b => b.CrossFrames.PlatesAtEachTopOfWeb = true));
        Assert.All(fl.CrossFrames, c => { Assert.False(c.PlatesOnFramePlane); Assert.Equal(0, c.LeftShift); Assert.Equal(0, c.RightShift); Assert.Null(c.PlaneDirection); });
        double legacyGap = ml.CrossFrameFit!.Rows.Where(r => !r.AtSupport).Max(r => r.MaxGapBuilt);
        _out.WriteLine($"legacy far-gusset gap {legacyGap / In(1):0.000} in; plate moves default max {inter.Max(c => Math.Max(Math.Abs(c.LeftShift), Math.Abs(c.RightShift))) / In(1):0.000} in");
        Assert.True(legacyGap > In(0.01));
        Assert.DoesNotContain(fl.Diagnostics, d => d.Message.Contains("connection plates on the frame plane"));
        // file format: the switches are written only when set
        Assert.DoesNotContain("PlatesAtEachTopOfWeb", p.ToJson());
        Assert.DoesNotContain("FramePlane", p.ToJson());
        var back = BridgeProject.FromJson(Graded(0, 0, 0, tweak: b => { b.CrossFrames.PlatesAtEachTopOfWeb = true; b.CrossFrames.FramePlane = StandardFramePlane.Warped; }).ToJson());
        Assert.True(back.Bridges[0].CrossFrames.PlatesAtEachTopOfWeb);
        Assert.Equal(StandardFramePlane.Warped, back.Bridges[0].CrossFrames.FramePlane);
    }

    [Fact]
    public void HolesOnPlateMidPlane_AndConnectionPlatesShared()
    {
        var p = Graded(0.04, 0.03, 15);
        var (f, m) = Gen(p);
        // every connection plate hole lies on its plate (BuildStiffener converts registered holes by projection: the out-of-plane part must be 0)
        var tp = p.Bridges[0].Stiffeners.ConnectionPlate.Thickness;
        int n = 0;
        foreach (var b in m.Bolts.Where(b => b.Assembly.StartsWith("CF") && Math.Abs(b.Grip - (tp + p.Bridges[0].CrossFrames.GussetThickness)) < 1e-9))
        {
            var cf = f.CrossFrames.First(c => c.Mark == b.Assembly);
            if (cf.AtSupport) continue;
            var o = b.Frame.Origin;
            // nearest connection plate of the frame's two girders
            var st = f.Stiffeners.Where(s => s.Kind == StiffenerKind.ConnectionPlate && (s.Girder == cf.Bay || s.Girder == cf.RightGirder))
                .MinBy(s => (s.Location.XY - o.XY).Length)!;
            var g = f.Girders[st.Girder];
            var (pp, _, _) = g.Path.At(st.GirderDistance);
            var u = new Vec3(st.Direction.X, st.Direction.Y, 0);
            var y = StandardCrossFrames.FlangeNormal(g.Path, st.GirderDistance, u);
            var nrm = Vec3.Cross(u, y).Normalized();
            // the bolt is seated on the plate's back face
            Assert.True(Math.Abs(Math.Abs(Vec3.Dot(o - pp, nrm)) - tp / 2) < Tol, $"{cf.Mark}: bolt {Math.Abs(Vec3.Dot(o - pp, nrm)) - tp / 2} off the plate face");
            n++;
        }
        Assert.True(n > 50);
        // frames of one line in adjacent bays still share one plate through the web (pairs)
        Assert.Contains(f.Stiffeners, s => s.Kind == StiffenerKind.ConnectionPlate && s.Side == StiffenerSide.Both);
    }

    [Fact]
    public void Lines_DistanceAtReferenceTopOfWeb_ChainedOutward()
    {
        double dist = Ft(40);
        var p = Graded(0.04, 0.03, 0, tweak: b =>
        {
            b.CrossFrames.Lines.Add(new CrossFrameLine { Girder = 2, Distance = dist, NormalToGirder = true });
            b.CrossFrames.Lines.Add(new CrossFrameLine { Girder = 2, Distance = dist + Ft(20), NormalToGirder = true });
        });
        var (f, _) = Gen(p);
        var g2 = f.Girders[2];
        double s0 = g2.DistanceAt(0, 0);
        var line1 = f.CrossFrames.Where(c => !c.AtSupport && c.Span == 0 && (c.Bay == 2 || c.RightGirder == 2))
            .Where(c => Math.Abs((c.Bay == 2 ? c.LeftGlobalDistance : c.RightGlobalDistance) - s0 - dist) < 0.05).ToList();
        Assert.Equal(2, line1.Count);
        foreach (var c in line1)
        {
            double sRef = c.Bay == 2 ? c.LeftGlobalDistance : c.RightGlobalDistance;
            Assert.Equal(s0 + dist, sRef, 9);                                         // the reference end is exactly at the line distance (top of web)
            Assert.Equal(c.Bay < 2, c.ReferenceRight);                               // anchored at the end nearer the reference girder
            Assert.Equal(0, c.Bay == 2 ? c.LeftShift : c.RightShift, 12);
            Assert.True(Math.Abs(c.Bay == 2 ? c.RightShift : c.LeftShift) > In(0.005));
        }
        // the outer bays continue from the solved plates (one plate at G2 / G4 shared by both bays)
        var all = f.CrossFrames.Where(c => !c.AtSupport && c.LineGirder == 2 && c.Span == 0).ToList();
        foreach (var c in all)
            foreach (var o in all.Where(o => o.RightGirder == c.Bay && Math.Abs(o.RightGlobalDistance - c.LeftGlobalDistance) < 0.05))
                Assert.Equal(o.RightGlobalDistance, c.LeftGlobalDistance, 12);
    }

    [Fact]
    public void Warped_ZeroGapBothEnds_TwistedMembersFaceted_UntwistedExact()
    {
        // crest vertical curve + skewed frames parallel to the supports: the two girders of a frame have different grades -> true twist
        var p = Graded(0, 0.02, 30, crest: true, tweak: b => { b.CrossFrames.FramePlane = StandardFramePlane.Warped; b.CrossFrames.Orientation = CrossFrameOrientation.ParallelToSkew; });
        var (f, m) = Gen(p);
        var rows = m.CrossFrameFit!.Rows.Where(r => !r.AtSupport).ToList();
        Assert.Contains(rows, r => Math.Abs(r.TwistDegrees) > 0.01);
        foreach (var r in rows)
        {
            Assert.Equal("warped", r.BuiltPlane);
            Assert.True(r.MaxGapBuilt < Tol, $"{r.Mark}: warped gap {r.MaxGapBuilt * 1000:0.0000} mm");
        }
        var tw = rows.MaxBy(r => Math.Abs(r.RollDegrees))!;
        _out.WriteLine($"max twist {rows.Max(r => Math.Abs(r.TwistDegrees)):0.0000} deg, roll {tw.RollDegrees:0.0000} deg ({tw.Mark} {tw.RollMember}), tip {tw.LegTipDeviation / In(1):0.0000} in");
        Assert.True(Math.Abs(tw.RollDegrees) > SteelModelGenerator.WarpRollTolerance);
        Assert.True(tw.LegTipDeviation > 0);
        var twisted = m.Parts.First(x => x.Mark == $"{tw.Mark}-{tw.RollMember}");
        Assert.False(twisted.Mesh.HasExactSolids);                                    // faceted
        Assert.Contains(m.Diagnostics, d => d.Message.StartsWith("Warped cross frames"));
        // midway (default) on the same bridge: members stay exact extrusions; the far gussets carry the true twist (reported, not hidden)
        var (_, mm) = Gen(Graded(0, 0.02, 30, crest: true, tweak: b => b.CrossFrames.Orientation = CrossFrameOrientation.ParallelToSkew));
        var same = mm.Parts.First(x => x.Mark == twisted.Mark);
        Assert.True(same.Mesh.HasExactSolids);
        Assert.True(mm.CrossFrameFit!.Rows.First(r => r.Mark == tw.Mark).MaxGapBuilt > Tol);

        // untwisted (constant grade): warped members stay exact extrusions and gaps are 0
        var (_, mu) = Gen(Graded(0.04, 0.03, 15, tweak: b => b.CrossFrames.FramePlane = StandardFramePlane.Warped));
        var members = mu.Parts.Where(x => x.Assembly.StartsWith("CF") && x.Category == PartCategory.Angle).ToList();
        Assert.NotEmpty(members);
        Assert.All(members.Where(x => !mu.CrossFrameFit!.Rows.First(r => r.Mark == x.Assembly).AtSupport), x => Assert.True(x.Mesh.HasExactSolids, x.Mark));
        Assert.True(mu.CrossFrameFit!.Rows.Max(r => r.MaxGapBuilt) < Tol);
    }

    [Fact]
    public void Standard_DefinedEnd_IsTheChainReference()
    {
        // cross slope rising to the left: the taller end is the LEFT girder; DefinedEnd Right makes the right girder the reference
        var p = Graded(0.04, 0.02, 0, tweak: b => b.CrossFrames.Standard = new StandardCrossFrameDefinition
            { Name = "STD", TopWorkPoint = In(6), Depth = In(30), TopClearance = In(1), BottomClearance = In(1), DefinedEnd = StandardDefinedEnd.Right });
        var (f, m) = Gen(p);
        var inter = f.CrossFrames.Where(c => !c.AtSupport).ToList();
        int last = inter.Max(c => c.RightGirder);
        Assert.All(inter.Where(c => c.RightGirder == last), c => Assert.True(c.ReferenceRight));
        Assert.All(m.CrossFrameFit!.Rows.Where(r => !r.AtSupport), r => Assert.True(r.MaxGapBuilt < Tol, $"{r.Mark} {r.MaxGapBuilt}"));
    }
}
