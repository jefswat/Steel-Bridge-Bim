using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Truss;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Truss member end options (TRUSS-DESIGN.md section 25, PTC floor beam connections): floor beams NORMAL TO GRADE, vertical end cuts, notches (cope /
/// slots), round holes through the web and end connection plates, TrimMember (member end moved to the probed connection face), horizontal connection
/// angles; omitted from the file when unset.</summary>
public class TrussMemberEndTests
{
    private const double Panel = 6.096, Depth = 6.096, Half = 4.572;
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;
    private static SteelPart Part(BridgeModel bm, string mark) => bm.Steel!.Parts.Single(x => x.Mark == mark);

    private static TrussSectionDefinition Hv() => new()
    {
        Name = "HV", Kind = TrussSectionKind.H, WebVertical = true, WebDepth = In(36), WebPlies = { new TrussPly(In(36), In(0.5)) }, Angle = "L4x4x1/2",
        TopPlates = { new TrussPlate(In(12), In(0.5)) }, BottomPlates = { new TrussPlate(In(12), In(0.5)) },
    };

    [Fact]
    public void NewInputs_AreOmittedFromTheFile_WhenUnset()
    {
        var (_, b) = TrussTests.Model();
        var json = System.Text.Json.JsonSerializer.Serialize(b.Truss, new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        foreach (var key in new[] { "NormalToGrade", "StartCut", "EndCut", "Notches", "Holes", "TrimMember", "HorizontalAngle" }) Assert.DoesNotContain($"\"{key}\"", json);
    }

    [Fact]
    public void NormalToGrade_FloorBeamLeansWithTheGrade_StiffenersFollow()
    {
        var (p, b) = TrussTests.Model();
        const double G = 0.03;
        p.Alignments[0].Profile = new VerticalProfile { Vpis = { new Vpi(0, Ft(100)), new Vpi(Ft(400), Ft(100) + G * Ft(400)) } };
        var t = b.Truss!;
        var fb = t.Members.First(m => m.Name == "FB3");
        fb.NormalToGrade = true;
        fb.Stiffeners = new() { new TrussStiffenerSet { Angle = "L4x3x3/8", Height = In(30), Distances = { Ft(15) }, PerFace = 1 } };
        // a level girder along plane N with two stiffener sets: default (normal to the girder axis = plumb here) and NormalToGrade (leans with the 3 % grade)
        t.Sections.Add(new TrussSectionDefinition { Name = "GIR", Kind = TrussSectionKind.H, WebVertical = true, WebDepth = In(60), WebPlies = { new TrussPly(In(60), In(0.5)) }, Angle = "L6x6x1/2" });
        t.Members.Add(new TrussMemberDefinition { Name = "GIR", From = "L0", To = "L6", Plane = "N", Section = "GIR", Role = TrussMemberRole.Girder, OffsetZ = -Ft(10), Stiffeners = new()
        {
            new TrussStiffenerSet { Angle = "L5x3.5x7/16", Height = In(48), Distances = { Ft(10) } },
            new TrussStiffenerSet { Angle = "L5x3.5x7/16", Height = In(48), Distances = { Ft(30) }, NormalToGrade = true },
        } });
        var bm = Regen(p);
        var al = p.Alignments[0];
        var mesh = Part(bm, "FB3").Mesh;
        // the section leans with the grade: the upper half of the (symmetric) section lies up-grade... down-station by G per unit height (plumb: 0)
        double Lean(SBM.Core.Solids.Mesh m)
        {
            double zMid = (m.Positions.Max(v => v.Z) + m.Positions.Min(v => v.Z)) / 2;
            var up = m.Positions.Where(v => v.Z > zMid).ToList(); var lo = m.Positions.Where(v => v.Z <= zMid).ToList();
            return (up.Average(v => al.Inverse(v.XY).Station) - lo.Average(v => al.Inverse(v.XY).Station)) / (up.Average(v => v.Z) - lo.Average(v => v.Z));
        }
        Assert.Equal(-G, Lean(mesh), 3);
        Assert.Equal(0, Lean(Part(bm, "FB4").Mesh), 6);
        // the stiffener (member frame, normal to the axis in the web plane) leans with the beam: its top is down-station of its bottom by the same ratio
        Assert.Equal(-G, Lean(Part(bm, "FB3-ST1").Mesh), 2);
        Assert.Equal(0, Lean(Part(bm, "N-GIR-ST1").Mesh), 6);
        Assert.Equal(-G, Lean(Part(bm, "N-GIR-ST2").Mesh), 3);
        // a NormalToGrade stiffener set on a member whose axis is along the grade = normal to the member axis (girder on the same grade)
        var json = System.Text.Json.JsonSerializer.Serialize(t, new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        Assert.Contains("\"NormalToGrade\"", json);
        Assert.True(BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!.Members.First(m => m.Name == "FB3").NormalToGrade);
    }

    [Fact]
    public void VerticalEndCut_SlopedMember_EndFaceInAVerticalPlane()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        t.Sections.Add(Hv());
        // sloped cross-plane member N:L4 -> S:U4, start cut vertical, end cut square
        t.Members.Add(new TrussMemberDefinition { Name = "XB", From = "N:L4", To = "S:U4", Section = "HV", Role = TrussMemberRole.FloorBeam, StartSetback = In(12), EndSetback = In(12), StartCut = TrussEndCut.Vertical });
        var bm = Regen(p);
        var r = bm.Steel!.Truss!.Members.Single(x => x.Mark == "XB");
        var ax = (r.B - r.A).Normalized();
        var h = new Vec3(ax.X, ax.Y, 0).Normalized();
        var pos = Part(bm, "XB").Mesh.Positions;
        // start face: every vertex at the horizontal distance of the cut point (12 in. along the axis)
        double target = In(12) * Vec3.Dot(ax, h);
        double m0 = pos.Min(v => Vec3.Dot(v - r.A, h));
        Assert.Equal(target, m0, 6);
        int onFace = pos.Count(v => Math.Abs(Vec3.Dot(v - r.A, h) - target) < 1e-6);
        Assert.True(onFace >= 12, $"{onFace} vertices on the vertical start face");
        // end face square: vertices at 12 in. from B along the axis
        Assert.True(pos.Count(v => Math.Abs(Vec3.Dot(r.B - v, ax) - In(12)) < 1e-6) >= 12);
        Assert.Contains("\"StartCut\"", System.Text.Json.JsonSerializer.Serialize(t.Members.Last(), new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
    }

    [Fact]
    public void NotchHoleHorizontalAngle_ExactVolumes_TrimMember_MovesTheEndToTheFace()
    {
        var (p0, b0) = TrussTests.Model();
        b0.Truss!.Sections.Add(Hv());
        b0.Truss.Members.Add(new TrussMemberDefinition { Name = "TB", From = "N:L4", To = "S:L4", Section = "HV", Role = TrussMemberRole.FloorBeam, OffsetZ = In(30), StartSetback = In(12), EndSetback = In(12) });
        var bm0 = Regen(p0);

        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        t.Sections.Add(Hv());
        var tb = new TrussMemberDefinition { Name = "TB", From = "N:L4", To = "S:L4", Section = "HV", Role = TrussMemberRole.FloorBeam, OffsetZ = In(30), StartSetback = In(12), EndSetback = In(12) };
        // cope at the start: 6 in. long, everything below 6 in. above the web bottom; hole 4 in. dia. on the web centre 24 in. from the start working point
        tb.Notches = new() { new TrussMemberNotch { End = TrussMemberEnd.Start, From = 0, Length = In(6), YMax = -In(12), Label = "cope" } };
        tb.Holes = new() { new TrussMemberHole { End = TrussMemberEnd.Start, Distance = In(24), Y = 0, Diameter = In(4) } };
        // end plates 8 in. wide from 21 in. (the hole lies inside them) + a horizontal angle along the member at the cope top
        tb.EndConnections = new()
        {
            new TrussEndConnection { End = TrussMemberEnd.Start, Label = "end plates", Back = In(21), PlateWidth = In(8), PlateThickness = In(0.5), Top = In(10), Length = In(20) },
            new TrussEndConnection { End = TrussMemberEnd.Start, Label = "seat angles", Angle = "L4x4x1/2", HorizontalAngle = true, Back = In(12), Top = -In(12), Length = In(10) },
        };
        t.Members.Add(tb);
        // FB2: TrimMember (backs probed at 10 3/4 in. (N, inside the gusset) / 8 in. (S, the vertical's flanges), setback 10 in.)
        var fb = t.Members.First(m => m.Name == "FB2");
        fb.EndConnections = new() { new TrussEndConnection { Label = "2-Ls 4x4x3/4", Angle = "L4x4x3/4", Top = In(14), Length = In(24), TrimMember = 0 } };
        var bm = Regen(p);

        // cope: removes the section below y = -12 in. over 6 in. (bottom cover 12 x 1/2, both bottom angles whole, web 6 x 1/2)
        double angleArea = SteelCatalog.FindAngle("L4x4x1/2")?.Area ?? In(0.5) * In(7.5);
        double removed = In(6) * (In(12) * In(0.5) + 2 * angleArea + In(6) * In(0.5));
        double hole = Math.PI * In(2) * In(2) * In(0.5);
        double dv = Part(bm0, "TB").Volume - Part(bm, "TB").Volume;
        Assert.InRange(dv, 0.97 * (removed + hole), 1.03 * (removed + hole));
        Assert.True(Part(bm, "TB").Mesh.HasExactSolids);
        // end plates: 2 x (8 x 20 - hole) x 1/2 (the hole 16-gon inscribed, within 2 %)
        var ep = Part(bm, "TB-EC1AP");
        double epFull = 2 * In(8) * In(20) * In(0.5);
        Assert.InRange(ep.Volume, epFull - 2 * hole * 1.02, epFull - 2 * hole * 0.97);
        Assert.True(ep.Mesh.HasExactSolids);
        // horizontal angles: 2 x angle area x 10 in.
        var ha = Part(bm, "TB-EC2A");
        var hp = TrussSections.ParseAngle("L4x4x1/2")!.Value;
        Assert.Equal(2 * (hp.a + hp.b - hp.t) * hp.t * In(10), ha.Volume, 9);

        // TrimMember: the member end moved to the back at both ends (N 10 3/4 = 3/4 in. shorter, S 8 = 2 in. longer than the 10 in. setbacks)
        var fb0 = Part(bm0, "FB2"); var fb1 = Part(bm, "FB2");
        Assert.Equal(fb0.Length + In(2) - In(0.75), fb1.Length, 6);
        Assert.Equal(fb0.Volume * fb1.Length / fb0.Length, fb1.Volume, 6);
        Assert.Contains(bm.Steel!.Diagnostics, d => d.Message.Contains("TrimMember"));
        // round trip
        var p2 = BridgeProject.FromJson(p.ToJson());
        var tb2 = p2.Bridges[0].Truss!.Members.First(m => m.Name == "TB");
        Assert.Single(tb2.Notches!); Assert.Single(tb2.Holes!); Assert.True(tb2.EndConnections![1].HorizontalAngle);
        Assert.Equal(0, p2.Bridges[0].Truss!.Members.First(m => m.Name == "FB2").EndConnections![0].TrimMember);
        Assert.Equal(Part(bm, "TB").Volume, Part(Regen(p2), "TB").Volume, 9);
    }
}
