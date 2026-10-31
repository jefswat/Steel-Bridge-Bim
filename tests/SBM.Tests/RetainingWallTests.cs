using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Wall;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Cantilever retaining walls (<see cref="RetainingWallGenerator"/>, 2026-10-29; generic test inputs per the user's answer 7a.1).</summary>
public class RetainingWallTests
{
    private static (SbmDocument d, Alignment al) Doc()
    {
        var d = SbmDocument.Sample();
        var al = new Alignment(new[] { HorizontalElement.Line(new Vec2(1000, 1000), new Vec2(1100, 1000)) }) { Name = "Wall line" };
        d.Project.Alignments.Add(al);
        return (d, al);
    }

    /// <summary>Closed and consistently oriented: after welding, every directed edge appears once and its reverse once.</summary>
    internal static bool Closed(SBM.Core.Solids.Mesh m)
    {
        (long, long, long) K(Vec3 p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6), (long)Math.Round(p.Z * 1e6));
        var edges = new Dictionary<((long, long, long), (long, long, long)), int>();
        for (int t = 0; t < m.TriangleCount; t++)
            for (int e = 0; e < 3; e++)
            {
                var a = K(m.Positions[m.Indices[3 * t + e]]); var b = K(m.Positions[m.Indices[3 * t + (e + 1) % 3]]);
                if (a == b) continue;
                edges[(a, b)] = edges.GetValueOrDefault((a, b)) + 1;
            }
        return edges.All(kv => kv.Value == 1 && edges.GetValueOrDefault((kv.Key.Item2, kv.Key.Item1)) == 1);
    }

    private static RetainingWallDefinition Wall() => new()
    {
        Name = "RW-1", AlignmentName = "Wall line", Offset = 0, BeginStation = 10, EndStation = 40, FaceSide = WallFaceSide.Right,
        TopOfWall = { new(10, 102.0), new(40, 103.5) },
        FootingTopSteps = new() { new(10, 95.0), new(25, 95.6) },
        StemTopThickness = Ft(1), FrontBatter = 1.0 / 48, BackBatter = 0.04, FootingThickness = Ft(2.5), Toe = Ft(3), Heel = Ft(6),
        Key = new WallKeyDefinition { Width = Ft(1.5), Depth = Ft(1), FromToe = Ft(4) },
        JointStations = { 20, 30 }, Source = "generic test",
    };

    [Fact]
    public void Volumes_MatchTheSection_PanelsAtJoints_StepSplitsTheSolids()
    {
        var (_, al) = Doc();
        var w = Wall();
        var m = RetainingWallGenerator.Generate(w, al);
        Assert.DoesNotContain(m.Diagnostics, x => x.Severity != SBM.Core.Framing.DiagnosticSeverity.Info);
        Assert.Equal(3, m.Panels.Count);
        Assert.Equal(2, m.Panels[1].Stems.Count);   // the footing step at 25 lies inside panel 2 (20-30)
        Assert.All(m.Panels.SelectMany(p => p.Stems.Concat(p.Footings)), x => Assert.True(Closed(x), "closed solid"));
        // analytic integrals along the straight control line
        double stem = 0, ftg = 0, faceA = 0; int n = 30000;
        for (int i = 0; i < n; i++)
        {
            double s = 10 + 30 * (i + 0.5) / n, top = 102.0 + 1.5 * (s - 10) / 30, zf = s < 25 ? 95.0 : 95.6, h = top - zf, ds = 30.0 / n;
            stem += ds * (w.StemTopThickness * h + 0.5 * h * h * (w.FrontBatter + w.BackBatter));
            double width = w.Toe + w.StemTopThickness + h * (w.FrontBatter + w.BackBatter) + w.Heel;
            ftg += ds * (w.FootingThickness * width + w.Key!.Width * w.Key.Depth);
            faceA += ds * h * Math.Sqrt(1 + w.FrontBatter * w.FrontBatter);
        }
        Assert.Equal(stem, m.StemVolume, 3);
        Assert.Equal(ftg, m.FootingVolume, 3);
        Assert.Equal(faceA, m.FaceArea, 2);
        // the front face at the top of wall lies on the control line, the soil is to the left (face right): the heel is at negative offsets
        var line = m.PlanLines[2];
        Assert.All(line, p => Assert.True(al.Inverse(p.XY).Offset < 0));
    }

    [Fact]
    public void MissingDimension_NotBuilt_Unresolved_OmittedWhenEmpty()
    {
        Assert.DoesNotContain("\"Walls\"", SbmDocument.Sample().Project.ToJson());
        var (d, al) = Doc();
        var w = Wall();
        w.Heel = double.NaN; w.FaceSide = null;
        var m = RetainingWallGenerator.Generate(w, al);
        Assert.Empty(m.Panels);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED (generic test)") && x.Message.Contains("heel") && x.Message.Contains("face side"));
        d.Project.Walls.Add(Wall());
        var back = BridgeProject.FromJson(d.Project.ToJson());
        Assert.Equal(2, back.Walls.Single().JointStations.Count);
        Assert.Equal(Ft(4), back.Walls[0].Key!.FromToe, 9);
    }

    [Fact]
    public void Ifc_WallAssembly_StemAndFootingPerPanel()
    {
        var (d, _) = Doc();
        d.Project.Walls.Add(Wall());
        d.Regenerate();
        string path = Path.Combine(Path.GetTempPath(), "sbm-wall-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(path);
            var ifc = File.ReadAllText(path);
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(ifc, @"IFCWALL\('[^']*',\$,'RW-1 stem [^;]*\.RETAININGWALL\.").Count);
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(ifc, @"IFCFOOTING\('[^']*',\$,'RW-1 footing [^;]*\.STRIP_FOOTING\.").Count);
            Assert.Contains("'RETAINING_WALL'", ifc);
        }
        finally { File.Delete(path); }
    }

    // ---------------- MSE (contract-plans envelope, user answer 7a.4) ----------------

    private static RetainingWallDefinition Mse() => new()
    {
        Name = "MSE-1", AlignmentName = "Wall line", Offset = 0, BeginStation = 10, EndStation = 40, FaceSide = WallFaceSide.Right,
        TopOfWall = { new(10, 102.0), new(40, 103.5) },
        FootingTopSteps = new() { new(10, 95.0), new(25, 95.6) },
        JointStations = { 20, 30 }, Source = "generic test",
        Mse = new MseWallDefinition
        {
            FacingThickness = In(5.5), FacingBatter = 0, PanelWidth = 1.5, PanelHeight = 1.5,
            LevelingPadWidth = Ft(1), LevelingPadThickness = In(6), LevelingPadFrontProjection = In(3),
            Coping = new() { new(-0.05, 0), new(0.4, 0), new(0.4, 0.6), new(-0.05, 0.6) },
            LengthsByHeight = { new() { UpToHeight = 7.2, Length = 5.0 }, new() { UpToHeight = 9.0, Length = 6.5 } },
            LengthsByStation = { new() { FromStation = 38, ToStation = 40, Length = 7.0 } },
            LengthFrom = MseLengthOrigin.BackOfFacing,
            Reinforcement = new MseReinforcementDefinition { Kind = MseReinforcementKind.Strip, Levels = { 0.4, 1.15, 1.9, 2.65, 3.4, 4.15, 4.9, 5.65, 6.4, 7.15, 7.9 },
                Spacing = 0.75, FirstFromBegin = 0.375, Width = 0.05, Thickness = 0.004, Source = "supplier test" },
        },
    };

    private static double Tow(double s) => 102.0 + 1.5 * (s - 10) / 30;
    private static double PadTop(double s) => s < 25 ? 95.0 : 95.6;
    private static double Len(double s) => s >= 38 ? 7.0 : Tow(s) - PadTop(s) <= 7.2 ? 5.0 : 6.5;

    [Fact]
    public void Mse_EnvelopeVolumes_PanelGrid_ZoneLengthsFromTheTables_Strips()
    {
        var (_, al) = Doc();
        var w = Mse();
        var m = RetainingWallGenerator.Generate(w, al);
        Assert.DoesNotContain(m.Diagnostics, x => x.Severity != SBM.Core.Framing.DiagnosticSeverity.Info);
        Assert.All(m.Elements.SelectMany(e => e.Solids), x => Assert.True(Closed(x), "closed solid"));
        double V(WallElementRole r) => m.Elements.Where(e => e.Role == r).Sum(e => e.Volume);
        double facing = 0, zone = 0; int n = 30000;
        for (int i = 0; i < n; i++)
        {
            double s = 10 + 30 * (i + 0.5) / n, h = Tow(s) - PadTop(s), ds = 30.0 / n;
            facing += ds * w.Mse!.FacingThickness * h;
            zone += ds * Len(s) * h;
        }
        Assert.Equal(facing, V(WallElementRole.FacingPanel), 3);
        Assert.Equal(0, V(WallElementRole.Facing), 9);   // the facing of each joint panel holds its precast panels
        Assert.Equal(zone, V(WallElementRole.ReinforcedZone), 2);
        Assert.Equal(Ft(1) * In(6) * 30, V(WallElementRole.LevelingPad), 4);
        Assert.Equal(0.45 * 0.6 * 30, V(WallElementRole.Coping), 4);
        Assert.Equal(3, m.Elements.Count(e => e.Role == WallElementRole.Coping));
        // panels: never taller or wider than the panel size, each inside one joint panel
        foreach (var p in m.Elements.Where(e => e.Role == WallElementRole.FacingPanel))
        {
            var b = p.Solids[0].Bounds();
            Assert.True(b.Max.Z - b.Min.Z <= 1.5 + 1e-6 && p.ToStation - p.FromStation <= 1.5 + 1e-6);
            var jp = m.Panels.Single(x => x.Index == p.Panel);
            Assert.True(p.FromStation >= jp.FromStation - 1e-9 && p.ToStation <= jp.ToStation + 1e-9);
        }
        // zone pieces break where the height table changes (station 14 and 26), at the step (25) and the station row (38)
        var zs = m.Elements.Where(e => e.Role == WallElementRole.ReinforcedZone).ToList();
        Assert.Equal(new[] { 10.0, 14, 25, 26, 38 }, zs.Select(z => Math.Round(z.FromStation, 4)).ToArray());
        Assert.Equal(new[] { 5.0, 6.5, 5.0, 6.5, 7.0 }, zs.Select(z => z.Length).ToArray());
        // strips: every level below the top of wall at each strip station, length = the zone length there
        int expect = 0;
        for (double s = 10.375; s <= 40 + 1e-9; s += 0.75)
            foreach (var lv in w.Mse!.Reinforcement!.Levels)
                if (PadTop(s) + lv + 0.002 < Tow(s)) expect++;
        var strips = m.Elements.Where(e => e.Role == WallElementRole.Reinforcement).ToList();
        Assert.Equal(expect, strips.Count);
        Assert.All(strips, x => Assert.Equal(Len(x.FromStation), x.Length, 6));
        Assert.All(strips, x => Assert.Equal(0.05 * 0.004 * x.Length, x.Volume, 8));
        // below LOD 400 only the zone envelope
        var low = RetainingWallGenerator.Generate(w, al, reinforcement: false);
        Assert.DoesNotContain(low.Elements, e => e.Role == WallElementRole.Reinforcement);
        Assert.Contains(low.Diagnostics, x => x.Message.Contains("below LOD 400"));
    }

    [Fact]
    public void Mse_MissingValues_Flagged_PartsNotBuilt_RoundTrip()
    {
        var (d, al) = Doc();
        var w = Mse();
        w.Mse!.LengthsByHeight.Clear(); w.Mse.LengthsByStation.Clear(); w.Mse.LevelingPadThickness = double.NaN; w.Mse.Reinforcement!.Spacing = double.NaN;
        var m = RetainingWallGenerator.Generate(w, al);
        Assert.DoesNotContain(m.Elements, e => e.Role is WallElementRole.ReinforcedZone or WallElementRole.LevelingPad or WallElementRole.Reinforcement);
        Assert.Contains(m.Elements, e => e.Role == WallElementRole.FacingPanel);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED (generic test)") && x.Message.Contains("reinforcement lengths not given"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED (generic test)") && x.Message.Contains("leveling pad"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED (supplier test)") && x.Message.Contains("spacing"));
        // no length origin: built from the back of the facing and flagged
        var w2 = Mse(); w2.Mse!.LengthFrom = null;
        var m2 = RetainingWallGenerator.Generate(w2, al);
        Assert.Contains(m2.Elements, e => e.Role == WallElementRole.ReinforcedZone);
        Assert.Contains(m2.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("measured from"));
        // no panel size: one facing envelope per joint panel
        var w3 = Mse(); w3.Mse!.PanelWidth = double.NaN;
        var m3 = RetainingWallGenerator.Generate(w3, al);
        Assert.Equal(3, m3.Elements.Count(e => e.Role == WallElementRole.Facing && e.Solids.Count > 0));
        Assert.DoesNotContain(m3.Elements, e => e.Role == WallElementRole.FacingPanel);
        Assert.All(m3.Elements.SelectMany(e => e.Solids), x => Assert.True(Closed(x), "closed solid"));
        d.Project.Walls.Add(Mse());
        string json = d.Project.ToJson();
        var cant = new BridgeProject(); cant.Walls.Add(Wall());
        Assert.DoesNotContain("\"Mse\"", cant.ToJson());
        var back = BridgeProject.FromJson(json).Walls.Single().Mse!;
        Assert.Equal(MseReinforcementKind.Strip, back.Reinforcement!.Kind);
        Assert.Equal(2, back.LengthsByHeight.Count);
        Assert.Equal(4, back.Coping!.Count);
    }

    [Fact]
    public void Mse_Ifc_FacingPanelsZoneCopingStrips_DatabaseKeysMatch()
    {
        var (d, _) = Doc();
        d.Project.Walls.Add(Mse());
        d.Regenerate();
        var m = d.Walls().Single();
        string path = Path.Combine(Path.GetTempPath(), "sbm-mse-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(path);
            var ifc = File.ReadAllText(path);
            int Count(string re) => System.Text.RegularExpressions.Regex.Matches(ifc, re).Count;
            Assert.Equal(3, Count(@"IFCWALL\('[^']*',\$,'MSE-1 facing \d+'[^;]*\.RETAININGWALL\."));
            Assert.Equal(m.Elements.Count(e => e.Role == WallElementRole.FacingPanel), Count(@"IFCBUILDINGELEMENTPART\('[^']*',\$,'MSE-1 facing panel [^;]*\.PRECASTPANEL\."));
            Assert.Equal(5, Count(@"IFCREINFORCEDSOIL\("));
            Assert.Equal(3, Count(@"IFCCOVERING\('[^']*',\$,'MSE-1 coping[^;]*\.COPING\."));
            Assert.Equal(2, Count(@"IFCFOOTING\('[^']*',\$,'MSE-1 leveling pad[^;]*'LEVELING_PAD'"));
            Assert.Equal(m.Elements.Count(e => e.Role == WallElementRole.Reinforcement), Count(@"'SOIL_REINFORCEMENT'"));
            var tags = SBM.IO.Database.ModelDatabase.BuildTagIndex(d.Project, Array.Empty<SBM.IO.Ifc.IfcBridgeData>(), null, new[] { m });
            Assert.All(m.Elements.Where(e => e.Solids.Count > 0).Take(50), e => Assert.Contains("'" + tags.Lookup(e.Solids[0]) + "'", ifc));
        }
        finally { File.Delete(path); }
    }
}
