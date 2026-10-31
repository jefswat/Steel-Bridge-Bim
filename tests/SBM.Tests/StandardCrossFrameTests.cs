using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using Xunit.Abstractions;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>OPTIONAL standard ("horizontal") cross frames (<see cref="StandardCrossFrameDefinition"/>): off = identical, depth solver, hole shifts,
/// hole registration on the bolt axis, per-type standards, taller-end default, twist measurement.</summary>
public class StandardCrossFrameTests
{
    private readonly ITestOutputHelper _out;
    public StandardCrossFrameTests(ITestOutputHelper o) => _out = o;

    private static readonly Vec3 Up = new(0, 0, 1);

    /// <summary>Sample bridge on a straight, flat alignment with a constant cross slope (+ = rising to the left: the left girders are higher), no skew.</summary>
    private static BridgeProject Straight(double crossSlope, Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        var pis = new PiDefinition { Pis = { new PiRow(Ft(5000), Ft(10000)), new PiRow(Ft(5000), Ft(14000)) } };
        var el = pis.Build(out _);
        var al = new Alignment(el)
        {
            Name = p.Alignments[0].Name, StartStation = 0, PiDefinition = pis,
            Profile = new VerticalProfile { Vpis = { new Vpi(0, Ft(500)), new Vpi(Ft(4000), Ft(500)) } },
        };
        al.CrossSlope = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, crossSlope, -crossSlope) } };
        p.Alignments[0] = al;
        var b = p.Bridges[0];
        foreach (var s in b.Supports) s.SkewDegrees = 0;
        b.Bearings.Type = BearingType.Elastomeric;
        tweak?.Invoke(b);
        return p;
    }

    private static (BridgeFraming f, SteelModel m) Gen(BridgeProject p)
    {
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        return (f, SteelModelGenerator.Generate(f, p.Units));
    }

    private static StandardCrossFrameDefinition Std(double top, double depth) => new() { Name = "STD", TopWorkPoint = top, Depth = depth, TopClearance = In(1), BottomClearance = In(2) };

    [Fact]
    public void Off_And_TwistReport_DoNotChangeGeometry_OrIfc()
    {
        var p0 = SampleProject.Create();
        var (_, m0) = Gen(p0);
        Assert.Null(m0.CrossFrameFit);
        var p1 = SampleProject.Create();
        p1.Bridges[0].CrossFrames.ReportTwist = true;
        var (_, m1) = Gen(p1);
        Assert.NotNull(m1.CrossFrameFit);
        Assert.Equal(m0.Parts.Count, m1.Parts.Count);
        for (int i = 0; i < m0.Parts.Count; i++)
        {
            Assert.Equal(m0.Parts[i].Mark, m1.Parts[i].Mark);
            Assert.Equal(m0.Parts[i].Mesh.Positions, m1.Parts[i].Mesh.Positions);
        }
        Assert.Equal(m0.Bolts.Select(b => b.Frame.Origin), m1.Bolts.Select(b => b.Frame.Origin));
        // the file format: nothing new written when off
        string json = p0.ToJson();
        Assert.DoesNotContain("DefinedEnd", json);
        Assert.DoesNotContain("ReportTwist", json);
        Assert.DoesNotContain("IntermediatePlane", json);
        Assert.DoesNotContain("SpacingRounding", json);
        // IFC: the twist report adds nothing (the pset is written only for standard frames)
        var f0 = FramingGenerator.Generate(p0.Bridges[0], p0.Alignments[0]);
        var f1 = FramingGenerator.Generate(p1.Bridges[0], p1.Alignments[0]);
        var a = new StringWriter(); var b = new StringWriter();
        IfcExporter.Export(a, "test.ifc", p0, new[] { new IfcBridgeData(f0, m0) });
        IfcExporter.Export(b, "test.ifc", p1, new[] { new IfcBridgeData(f1, m1) });
        static IEnumerable<string> Body(StringWriter w) => w.ToString().Split('\n').Where(l => !l.StartsWith("FILE_NAME"));
        Assert.Equal(Body(a), Body(b));
        // round trip of the new inputs
        p1.Bridges[0].CrossFrames.Standard = Std(In(6), In(30));
        p1.Bridges[0].CrossFrames.IntermediatePlane = CrossFramePlaneOrientation.NormalToBottomFlange;
        var back = BridgeProject.FromJson(p1.ToJson());
        Assert.True(back.Bridges[0].CrossFrames.ReportTwist);
        Assert.Equal(In(30), back.Bridges[0].CrossFrames.Standard!.Depth, 9);
        Assert.Equal(CrossFramePlaneOrientation.NormalToBottomFlange, back.Bridges[0].CrossFrames.IntermediatePlane);
        Assert.True(double.IsNaN(back.Bridges[0].CrossFrames.Standard!.SpacingRounding));
    }

    [Fact]
    public void LevelBridge_OneFamily_ZeroShifts_ZeroTwist_IdenticalPrototype()
    {
        var p = Straight(0, b => b.CrossFrames.Standard = Std(In(6), In(30)));
        var (_, m) = Gen(p);
        var rep = m.CrossFrameFit!;
        var rows = rep.Rows.Where(r => r.Applied).ToList();
        Assert.NotEmpty(rows);
        Assert.Single(rep.Families.Where(f => f.Standard == "STD").GroupBy(f => f.Type));
        foreach (var r in rows)
        {
            Assert.Equal(0, r.ShiftTopL, 9); Assert.Equal(0, r.ShiftTopR, 9);
            Assert.Equal(0, r.TwistDegrees, 9);
            Assert.True(r.MaxGapBuilt < 1e-9);
        }
        // every frame of a family is the same shop frame: member / gusset outlines relative to the top-left WP equal to 1e-6 m
        foreach (var fam in rep.Families)
        {
            var dets = fam.Marks.Select(mk => m.CrossFrameDetails.First(d => d.Mark == mk)).ToList();
            var proto = dets[0];
            foreach (var d in dets.Skip(1))
            {
                Assert.Equal(proto.Members.Count, d.Members.Count);
                for (int i = 0; i < proto.Members.Count; i++)
                    for (int k = 0; k < proto.Members[i].Outline.Length; k++)
                        Assert.True(Vec2.Distance(proto.Members[i].Outline[k] - proto.WorkPoints[0], d.Members[i].Outline[k] - d.WorkPoints[0]) < 1e-6, $"{d.Mark} {d.Members[i].Code}");
                for (int i = 0; i < proto.Gussets.Count; i++)
                    for (int k = 0; k < proto.Gussets[i].Outline.Length; k++)
                        Assert.True(Vec2.Distance(proto.Gussets[i].Outline[k] - proto.WorkPoints[0], d.Gussets[i].Outline[k] - d.WorkPoints[0]) < 1e-6, $"{d.Mark} {d.Gussets[i].Code}");
            }
        }
    }

    [Fact]
    public void CrossSlope_DepthSolver_Shifts_TallerEndDefault_AndMisfits()
    {
        const double slope = 0.02;   // rising to the left: in every bay the LEFT girder is the taller one
        double top = In(6);
        var p = Straight(slope, b => b.CrossFrames.Standard = Std(top, In(30)));
        var (f, m) = Gen(p);
        double d = p.Bridges[0].Girders.Section.WebDepth;
        var rows = m.CrossFrameFit!.Rows.Where(r => r.Applied).ToList();
        foreach (var r in rows)
        {
            var cf = f.CrossFrames.First(c => c.Mark == r.Mark);
            double dz = f.Girders[cf.Bay].Path.At(cf.LeftGlobalDistance).Item1.Z - f.Girders[cf.RightGirder].Path.At(cf.RightGlobalDistance).Item1.Z;
            Assert.True(dz > 0.01);
            Assert.Equal("L", r.DefinedEnd);                                     // taller end = the left (higher) girder
            Assert.Equal(0, r.ShiftTopL, 9);                                     // defined end: standard pattern
            Assert.Equal(dz, r.ShiftTopR, 9);                                     // lower girder: holes higher on its plate by the top of web difference
            Assert.Equal(d - top - In(2), r.MaxDepth, 9);                         // governed by the defined end's bottom flange (plumb plates, level frame)
            Assert.Equal(top - dz, r.ClearTopR, 9);                              // the lower girder's top flange is closer to the level top WP
        }
        // the right end defined instead: the bottom flange of the higher (left) girder governs, the left holes drop by dz
        var p2 = Straight(slope, b => { b.CrossFrames.Standard = Std(top, In(30)); b.CrossFrames.Standard.DefinedEnd = StandardDefinedEnd.Right; });
        var (f2, m2) = Gen(p2);
        var rep2 = m2.CrossFrameFit!;
        double dmax = double.MaxValue; string gov = "";
        foreach (var r in rep2.Rows.Where(r => r.Applied))
        {
            var cf = f2.CrossFrames.First(c => c.Mark == r.Mark);
            double dz = f2.Girders[cf.Bay].Path.At(cf.LeftGlobalDistance).Item1.Z - f2.Girders[cf.RightGirder].Path.At(cf.RightGlobalDistance).Item1.Z;
            Assert.Equal(-dz, r.ShiftTopL, 9);
            Assert.Equal(d - top - dz - In(2), r.MaxDepth, 9);
            if (r.MaxDepth < dmax) { dmax = r.MaxDepth; gov = r.Mark; }
        }
        var sum = rep2.Standards.Single();
        Assert.Equal(dmax, sum.MaxCommonDepth, 9);
        Assert.Equal(gov, sum.GoverningMark);

        // entered depth 1" deeper than the deepest common depth: exactly the governing frame(s) misfit by 1", still built
        var p3 = Straight(slope, b => { b.CrossFrames.Standard = Std(top, dmax + In(1)); b.CrossFrames.Standard.DefinedEnd = StandardDefinedEnd.Right; });
        var (_, m3) = Gen(p3);
        var rep3 = m3.CrossFrameFit!;
        var mis = rep3.Rows.Where(r => r.Headroom < -1e-6).ToList();
        Assert.Contains(mis, r => r.Mark == gov);
        Assert.All(mis, r => Assert.True(r.Headroom >= -In(1) - 1e-6));
        Assert.Contains(rep3.Rows.Where(r => Math.Abs(r.Headroom + In(1)) < 1e-6), r => r.Mark == gov);
        Assert.Contains(m3.Diagnostics, x => x.Message.Contains("does not fit at"));
        Assert.Contains(m3.Bolts, b => b.Assembly == gov);                    // the misfit frame is still built
    }

    [Fact]
    public void MissingInputs_AreUnresolved_StandardNotApplied()
    {
        var p = Straight(0.02, b => b.CrossFrames.Standard = new StandardCrossFrameDefinition { Name = "STD" });
        var (_, m) = Gen(p);
        var msgs = m.Diagnostics.Select(x => x.Message).ToList();
        Assert.Contains(msgs, x => x.StartsWith("UNRESOLVED") && x.Contains("top work point"));
        Assert.Contains(msgs, x => x.StartsWith("UNRESOLVED") && x.Contains("depth"));
        Assert.Contains(msgs, x => x.StartsWith("UNRESOLVED") && x.Contains("clearances"));
        Assert.All(m.CrossFrameFit!.Rows.Where(r => r.Standard != ""), r => Assert.False(r.Applied));
        Assert.True(m.CrossFrameFit.Standards.Single().MaxCommonDepth > 0);   // the report still gives the deepest common depth
        // geometry as without the standard
        var (_, m0) = Gen(Straight(0.02));
        Assert.Equal(m0.Bolts.Select(b => b.Frame.Origin), m.Bolts.Select(b => b.Frame.Origin));
    }

    [Fact]
    public void Twist_Analytic_OnSample_AveragedPlaneSplitsIt()
    {
        var p = SampleProject.Create();
        p.Bridges[0].CrossFrames.ReportTwist = true;
        p.Bridges[0].CrossFrames.PlatesAtEachTopOfWeb = true;   // legacy placement: the averaged plane through the left top of web (the math checked here)
        var (f, m) = Gen(p);
        var rep = m.CrossFrameFit!;
        var r = rep.Rows.MaxBy(x => Math.Abs(x.TwistDegrees))!;
        _out.WriteLine($"{r.Mark}: twist {r.TwistDegrees:0.0000} deg, gap L {r.GapBuiltL.Min * 1000:0.000}..{r.GapBuiltL.Max * 1000:0.000} mm");
        Assert.True(Math.Abs(r.TwistDegrees) > 0.05);
        var cf = f.CrossFrames.First(c => c.Mark == r.Mark);
        var gl = f.Girders[cf.Bay]; var gr = f.Girders[cf.RightGirder];
        var pl = gl.Path.At(cf.LeftGlobalDistance).Item1; var pr = gr.Path.At(cf.RightGlobalDistance).Item1;
        var u = new Vec3(pr.X - pl.X, pr.Y - pl.Y, 0).Normalized();
        Vec3 Fn(GirderPath path, double s)
        {
            var (_, t, _) = path.At(s);
            var t3 = new Vec3(t.X, t.Y, path.GradeAt(s)).Normalized();
            var w = (Up - t3 * Vec3.Dot(Up, t3)).Normalized();
            return (w - u * Vec3.Dot(w, u)).Normalized();
        }
        var yl = Fn(gl.Path, cf.LeftGlobalDistance); var yr = Fn(gr.Path, cf.RightGlobalDistance);
        // twist = difference of the two flange-normal angles about the frame line
        double aL = Math.Atan2(Vec3.Dot(Vec3.Cross(Up, yl), u), Vec3.Dot(Up, yl)), aR = Math.Atan2(Vec3.Dot(Vec3.Cross(Up, yr), u), Vec3.Dot(Up, yr));
        Assert.Equal(Numerics.RadToDeg(aR - aL), r.TwistDegrees, 9);
        // averaged plane through the left top of web: the left plate is rotated half the twist about the line through it -> gap = h sin(t/2) (+ the tp term)
        var det = m.CrossFrameDetails.First(d => d.Mark == r.Mark);
        double tp = det.PlateThickness, half = Math.Abs(Numerics.DegToRad(r.TwistDegrees)) / 2;
        var leftBolts = det.Bolts.Where(b => b.X < det.Span / 2).ToList();
        double hmax = leftBolts.Max(b => Math.Abs(b.Y - det.LeftWebTop));
        double expect = Math.Abs(tp / 2 - (tp / 2 + hmax * Math.Sin(half)) / Math.Cos(half));
        double expect2 = Math.Abs(tp / 2 - (tp / 2 - hmax * Math.Sin(half)) / Math.Cos(half));
        double got = Math.Max(Math.Abs(r.GapBuiltL.Min), Math.Abs(r.GapBuiltL.Max));
        Assert.True(Math.Min(Math.Abs(got - expect), Math.Abs(got - expect2)) < 1e-6, $"gap {got} vs {expect} / {expect2}");
        // the fitted-to-the-defined-end option puts no gap at the defined end
        var fitDefined = r.DefinedEnd == "L" ? r.GapFittedL : r.GapFittedR;
        Assert.True(Math.Max(Math.Abs(fitDefined.Min), Math.Abs(fitDefined.Max)) < 1e-9);
        Assert.Contains(m.Diagnostics, x => x.Message.StartsWith("Cross frame twist"));
    }

    [Fact]
    public void StandardFrames_BoltsSeatOnThePlate_HolesOnTheBoltAxis()
    {
        var p = SampleProject.Create();
        p.Bridges[0].CrossFrames.Standard = Std(In(6), In(30));
        p.Bridges[0].CrossFrames.PlatesAtEachTopOfWeb = true;   // legacy placement (plate plan direction = web point to web point, as reconstructed here)
        var (f, m) = Gen(p);
        var plate = p.Bridges[0].Stiffeners.ConnectionPlate;
        int checkedBolts = 0;
        foreach (var r in m.CrossFrameFit!.Rows.Where(x => x.Applied).Take(12))
        {
            var cf = f.CrossFrames.First(c => c.Mark == r.Mark);
            var pl = f.Girders[cf.Bay].Path.At(cf.LeftGlobalDistance).Item1; var pr = f.Girders[cf.RightGirder].Path.At(cf.RightGlobalDistance).Item1;
            var u = new Vec3(pr.X - pl.X, pr.Y - pl.Y, 0).Normalized();
            foreach (var bolt in m.Bolts.Where(b => b.Assembly == r.Mark && Math.Abs(b.Grip - (plate.Thickness + p.Bridges[0].CrossFrames.GussetThickness)) < 1e-9))
            {
                var o = bolt.Frame.Origin;
                bool right = Vec3.Dot(o - pl, u) > Vec3.Dot(pr - pl, u) / 2;
                var g = f.Girders[right ? cf.RightGirder : cf.Bay];
                double s = right ? cf.RightGlobalDistance : cf.LeftGlobalDistance;
                var (pp, t, _) = g.Path.At(s);
                var t3 = new Vec3(t.X, t.Y, g.Path.GradeAt(s)).Normalized();
                var w = (Up - t3 * Vec3.Dot(Up, t3)).Normalized();
                var y = (w - u * Vec3.Dot(w, u)).Normalized();
                var n = Vec3.Cross(u, y).Normalized();
                if (Vec3.Dot(n, bolt.Frame.Z) < 0) n = n * -1;
                // the bolt starts on the plate's back face, and its axis meets the mid-plane where BuildStiffener cut the hole
                Assert.True(Math.Abs(Vec3.Dot(o - pp, n) + plate.Thickness / 2) < 1e-9, $"{r.Mark}: bolt off the plate face by {Vec3.Dot(o - pp, n) + plate.Thickness / 2}");
                var e = new PlateEndGeometry(pp, y, w, pp - Up * p.Bridges[0].Girders.Section.WebDepth, 0);
                var plane = new FramePlaneGeom(o, u, Vec3.Cross(u, bolt.Frame.Z).Normalized());
                var hc = StandardCrossFrames.HoleCentre(plane, new Vec2(0, o.Z), e);
                Assert.True(Vec3.Cross(hc - o, bolt.Frame.Z).Length < 1e-9);         // on the bolt axis
                Assert.True(Math.Abs(Vec3.Dot(hc - pp, n)) < 1e-9);                     // on the plate mid-plane
                checkedBolts++;
            }
        }
        Assert.True(checkedBolts > 20);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("hole groups on the connection plates shifted"));
    }

    [Fact]
    public void PerTypeStandards_TwoSpacingFamilies_RoundingMerges()
    {
        var p = Straight(0.02, b =>
        {
            b.Girders.Spacings = new() { Ft(9), Ft(9), Ft(10), Ft(9) };
            b.CrossFrames.IntermediateType = CrossFrameType.KFrame;
            b.CrossFrames.Styles.Add(new CrossFrameStyle { Name = "S1", Kind = CrossFrameStyleKind.InvertedK, TopChord = "L4x4x3/8", Diagonal = "L4x4x3/8", BottomChord = "L4x4x3/8",
                Standard = new StandardCrossFrameDefinition { Name = "SUP", Scope = StandardFrameScope.Support, TopWorkPoint = In(8), Depth = In(28) } });
            b.CrossFrames.SupportStyle = "S1";
            b.CrossFrames.Standard = Std(In(6), In(30));
        });
        var (_, m) = Gen(p);
        var rep = m.CrossFrameFit!;
        Assert.All(rep.Rows.Where(r => r.AtSupport), r => { Assert.Equal("SUP", r.Standard); Assert.Equal(In(28), r.Depth, 9); });
        Assert.All(rep.Rows.Where(r => !r.AtSupport), r => { Assert.Equal("STD", r.Standard); Assert.Equal(In(30), r.Depth, 9); });
        // intermediate: two bay widths -> two families (9 ft x 3 bays, 10 ft x 1 bay), each identical inside
        var inter = rep.Families.Where(fm => fm.Standard == "STD").ToList();
        Assert.True(inter.Select(fm => Math.Round(fm.Spacing / In(1))).Distinct().Count() >= 2);
        // spacing rounding coarser than the bay difference merges them (only when given)
        var p2 = Straight(0.02, b =>
        {
            b.Girders.Spacings = new() { Ft(9), Ft(9), Ft(10), Ft(9) };
            b.CrossFrames.IntermediateType = CrossFrameType.KFrame;
            b.CrossFrames.Standard = Std(In(6), In(30));
            b.CrossFrames.Standard.SpacingRounding = Ft(4);
        });
        var (_, m2) = Gen(p2);
        foreach (var fm in inter.Concat(m2.CrossFrameFit!.Families)) _out.WriteLine($"{fm.Id} {fm.Standard} {fm.Type} {fm.Spacing / In(1):0.000} x {fm.Marks.Count}");
        Assert.True(m2.CrossFrameFit!.Families.Count < inter.Count);
        // the defined end follows the taller girder bay by bay; reproducible order
        Assert.Equal(rep.Rows.Select(r => r.Mark), rep.Rows.Select(r => r.Mark).OrderBy(x => x, SteelModelGenerator.MarkComparer.Instance));
    }

    [Fact]
    public void PlateDiaphragm_Standard_OneWebPlateOutline()
    {
        var p = Straight(0.02, b =>
        {
            b.CrossFrames.Styles.Add(new CrossFrameStyle { Name = "PD", Kind = CrossFrameStyleKind.PlateDiaphragm, DiaphragmBolts = 5, DiaphragmBoltPitch = In(4) });
            b.CrossFrames.IntermediateStyle = "PD";
            b.CrossFrames.Standard = Std(In(6), In(30));
        });
        var (_, m) = Gen(p);
        var webs = m.Parts.Where(x => x.Mark.EndsWith("-WEB")).ToList();
        Assert.NotEmpty(webs);
        Assert.Single(webs.Select(w => w.Description).Distinct());
        var fam = m.CrossFrameFit!.Families.First(fm => fm.Standard == "STD");
        var dets = fam.Marks.Select(mk => m.CrossFrameDetails.First(d => d.Mark == mk)).ToList();
        var w0 = dets[0].Members.First(x => x.Code == "WEB").Outline.Select(q => q - dets[0].WorkPoints[0]).ToList();
        foreach (var d in dets)
        {
            var w = d.Members.First(x => x.Code == "WEB").Outline.Select(q => q - d.WorkPoints[0]).ToList();
            for (int i = 0; i < w0.Count; i++) Assert.True(Vec2.Distance(w0[i], w[i]) < 1e-6);
        }
    }

    [Fact]
    public void PlaneInputs_DriveFrameAndConnectionPlates()
    {
        // intermediate frames and their plates plumb: no twist anywhere on the curved, graded sample
        var p = SampleProject.Create();
        p.Bridges[0].CrossFrames.IntermediatePlane = CrossFramePlaneOrientation.Plumb;
        p.Bridges[0].CrossFrames.ReportTwist = true;
        var (_, m) = Gen(p);
        Assert.All(m.CrossFrameFit!.Rows, r => Assert.Equal(0, r.TwistDegrees, 9));
        // support frames normal to the flanges while the bearing stiffeners stay plumb: the mismatch shows in the report (never forced)
        var p2 = SampleProject.Create();
        p2.Bridges[0].CrossFrames.SupportPlane = CrossFramePlaneOrientation.NormalToFlanges;
        p2.Bridges[0].CrossFrames.ReportTwist = true;
        var (_, m2) = Gen(p2);
        Assert.Contains(m2.CrossFrameFit!.Rows.Where(r => r.AtSupport), r => r.MaxGapBuilt > 1e-4);
        // ... and with the bearing stiffeners tilted too, the support plates follow the frame again
        var p3 = SampleProject.Create();
        p3.Bridges[0].CrossFrames.SupportPlane = CrossFramePlaneOrientation.NormalToFlanges;
        p3.Bridges[0].CrossFrames.BearingStiffenerPlane = CrossFramePlaneOrientation.NormalToFlanges;
        p3.Bridges[0].CrossFrames.ReportTwist = true;
        var (_, m3) = Gen(p3);
        Assert.True(m3.CrossFrameFit!.Rows.Where(r => r.AtSupport).Max(r => r.MaxGapBuilt) < m2.CrossFrameFit.Rows.Where(r => r.AtSupport).Max(r => r.MaxGapBuilt));
    }

    [Fact]
    public void Sheets_StandardFrameFamilies_AndHoleShiftTable()
    {
        var doc = SBM.Api.SbmDocument.Sample();
        doc.Project.Bridges[0].CrossFrames.Standard = Std(In(6), In(30));
        doc.Regenerate();
        var m = doc.Model();
        var ctx = new SBM.Core.Drawing.Views.ViewContext(m.Framing, m.Steel, m.Concrete, m.Substructure, null, doc.Units, SBM.Core.Drawing.Views.DrawingOptions.For(doc.Units));
        var sheets = SBM.Core.Drawing.Sheets.SheetSet.Build(ctx, new SBM.Core.Drawing.Sheets.SheetFormat(17, 11, "P", "B", "D"));
        Assert.Contains(sheets, s => s.Title == "Standard cross frames");
        Assert.Contains(sheets, s => s.Title == "Cross frame hole shifts");
        foreach (var s in sheets.Where(s => s.Title is "Standard cross frames" or "Cross frame hole shifts"))
        {
            foreach (var w in s.Warnings) _out.WriteLine($"{s.Number} {s.Title}: {w}");
            Assert.Empty(s.Warnings);
            var e = s.Drawing.Extents();
            Assert.True(e.MinX >= -1e-6 && e.MaxX <= 17 * 0.0254 + 1e-6 && e.MinY >= -1e-6 && e.MaxY <= 11 * 0.0254 + 1e-6, $"{s.Number} {s.Title}");
        }
    }
}
