using SBM.Api;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Substructure;
using SBM.Core.Utility;
using SBM.Core.Drawing.Views;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Utilities stage 2 (2026-10-30, generic test inputs): cones / frames / covers, wall openings, in-line valves, cover-defined inverts, duct banks,
/// bridge-hung runs, clash / clearance check, pipe profiles.</summary>
public class UtilityStage2Tests
{
    private static bool Closed(Mesh m)
    {
        var b = m.Bounds();
        double V(Vec3 o)
        {
            double v = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                var a = m.Positions[m.Indices[3 * t]] - o; var q = m.Positions[m.Indices[3 * t + 1]] - o; var c = m.Positions[m.Indices[3 * t + 2]] - o;
                v += Vec3.Dot(a, Vec3.Cross(q, c));
            }
            return v / 6;
        }
        double v1 = V(b.Min), v2 = V(b.Max);
        return v1 > 0 && Math.Abs(v1 - v2) <= 1e-6 * v1;
    }

    private static UtilityStructureDefinition Mh(string name, Vec2 p, double rim, double sump) => new()
    {
        Name = name, Kind = UtilityStructureKind.Manhole, X = p.X, Y = p.Y, RimElevation = rim, SumpElevation = sump,
        InnerDiameter = In(48), WallThickness = In(5), FloorThickness = In(8), Source = "test p.1",
    };

    private static UtilityPipeDefinition Pipe(string name, string from, string to, double i0, double i1, double d = 0.4572, double wall = 0.0635) => new()
    {
        Name = name, From = from, To = to, InvertStart = i0, InvertEnd = i1, InnerDiameter = d, WallThickness = wall, Material = "RCP",
    };

    [Fact]
    public void ConeFrameCover_WallsStopBelow_ClosedSolids_Openings()
    {
        var a = new Vec2(1000, 2000);
        var mh = Mh("MH-1", a, 150, 147);
        mh.Cone = new UtilityConeDefinition { Height = Ft(2), TopInnerDiameter = In(24) };
        mh.Frame = new UtilityFrameDefinition { Height = In(8), OuterDiameter = In(36), OpeningDiameter = In(24), CoverThickness = In(1), Designation = "R-1642" };
        var net = new UtilityNetworkDefinition { Name = "S", Structures = { mh, Mh("MH-2", a + new Vec2(30, 0), 150, 146.5) }, Pipes = { Pipe("P-1", "MH-1", "MH-2", 147.2, 147.0) } };
        var m = UtilityGenerator.Generate(net, Array.Empty<SBM.Core.Alignments.Alignment>(), null);
        var s = m.Structures[0];
        Assert.Equal(150 - In(8) - Ft(2), s.Mesh!.Bounds().Max.Z, 9);
        Assert.Equal(new[] { "cone", "frame", "cover" }, s.Parts.Select(p => p.Kind));
        Assert.All(s.Parts, p => Assert.True(Closed(p.Mesh), p.Name));
        Assert.True(Closed(s.Mesh));
        // concentric cone: frustum shells of 32-gons
        double k = 32 / (2 * Math.PI) * Math.Sin(2 * Math.PI / 32), h = Ft(2), R = In(24), r = In(12), t = In(5);
        double frustum(double r0, double r1) => k * Math.PI * h / 3 * (r0 * r0 + r0 * r1 + r1 * r1);
        Assert.Equal(frustum(R + t, r + t) - frustum(R, r), s.Parts[0].Volume, 6);
        // cover = the 24 in opening x 1 in
        Assert.Equal(k * Math.PI * In(12) * In(12) * In(1), s.Parts[2].Volume, 7);
        Assert.Equal(150, s.Parts[2].Mesh.Bounds().Max.Z, 9);
        // one opening per pipe end: area x wall, and the net volume deducts it
        Assert.Single(s.Openings);
        double ro = 0.4572 / 2 + 0.0635;
        double cos = 1 / Math.Sqrt(1 + Math.Pow(0.2 / m.Pipes[0].Length, 2));
        Assert.Equal(Math.PI * ro * ro * In(5) / cos, s.Openings[0].Deduction, 9);
        Assert.True(Closed(s.Openings[0].Mesh));
        Assert.Equal(Math.Abs(s.Mesh.Volume()) + s.Parts[0].Volume - s.Openings[0].Deduction, s.Volume, 9);
        // eccentric cone, vertical side toward +X: its outside top reaches the barrel's outside +X face
        mh.Cone.Eccentric = true; mh.Cone.EccentricDegrees = 0;
        var e = UtilityGenerator.Generate(net, Array.Empty<SBM.Core.Alignments.Alignment>(), null).Structures[0];
        Assert.Equal(e.Mesh!.Bounds().Max.X, e.Parts[0].Mesh.Bounds().Max.X, 9);
        // missing frame size: UNRESOLVED, walls stop at the frame bottom, no frame
        mh.Frame.OpeningDiameter = double.NaN; mh.Cone = null;
        var u = UtilityGenerator.Generate(net, Array.Empty<SBM.Core.Alignments.Alignment>(), null);
        Assert.Empty(u.Structures[0].Parts);
        Assert.Contains(u.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("frame outside / opening size"));
        Assert.Equal(150 - In(8), u.Structures[0].Mesh!.Bounds().Max.Z, 9);
    }

    [Fact]
    public void Water_ValveEnvelope_InvertsFromCover_NoGravityChecks()
    {
        var a = new Vec2(0, 0);
        var net = new UtilityNetworkDefinition
        {
            Name = "W", System = UtilitySystem.Water,
            Structures = { new() { Name = "V-1", Kind = UtilityStructureKind.Valve, X = 20, Y = 0, RimElevation = 99, SumpElevation = 98, InnerDiameter = 0.6 } },
            Pipes =
            {
                new() { Name = "W-1", StartX = 0, StartY = 0, To = "V-1", Cover = Ft(4), InnerDiameter = In(8), WallThickness = In(0.5) },
                new() { Name = "W-2", From = "V-1", EndX = 40, EndY = 0, Cover = Ft(4), InnerDiameter = In(8), WallThickness = In(0.5), InvertEnd = 98.9 },
            },
        };
        var m = UtilityGenerator.Generate(net, Array.Empty<SBM.Core.Alignments.Alignment>(), _ => (100.0, "existing ground"));
        var v = m.Structures.Single();
        Assert.True(v.IsBuilt && Closed(v.Mesh!));
        Assert.Equal(Math.PI * 0.09 * 1 * 32 / (2 * Math.PI) * Math.Sin(2 * Math.PI / 32), v.Volume, 6);   // solid envelope, no openings
        Assert.Empty(v.Openings);
        var w1 = m.Pipes.Single(p => p.Definition.Name == "W-1");
        Assert.Equal(100 - Ft(4) - In(8.5), w1.Start.Z, 9);
        Assert.Equal(20 - 0.3, w1.Length, 9);      // to the valve envelope face
        var w2 = m.Pipes.Single(p => p.Definition.Name == "W-2");
        Assert.Equal(98.9, w2.End.Z, 9);           // a given invert wins over the cover
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("W-1: start invert") && x.Message.Contains("plan cover"));
        Assert.DoesNotContain(m.Diagnostics, x => x.Message.Contains("adverse slope"));   // pressure main: no gravity checks
    }

    [Fact]
    public void DuctBank_EncasementWithHoles_Conduits_Openings_Ifc()
    {
        var (h1, h2) = (new Vec2(500, 500), new Vec2(560, 500));
        UtilityStructureDefinition Hh(string n, Vec2 p) => new()
        {
            Name = n, Kind = UtilityStructureKind.Handhole, Shape = UtilityStructureShape.Box, X = p.X, Y = p.Y, RimElevation = 100, SumpElevation = 98.5,
            InnerLength = 1.2, InnerWidth = 0.9, RotationDegrees = 0, WallThickness = 0.15, FloorThickness = 0.15,
        };
        var bank = new UtilityDuctBankDefinition
        {
            Name = "DB-1", From = "HH-1", To = "HH-2", BottomStart = 98.7, BottomEnd = 98.6, Width = In(30), Height = In(22), Rows = 2, Columns = 3,
            ConduitDiameter = In(4), ConduitWall = In(0.25), HorizontalSpacing = In(7.5), VerticalSpacing = In(7.5), FirstConduitFromLeft = In(7.5), FirstConduitFromBottom = In(7.25),
            Material = "Concrete", ConduitMaterial = "PVC Sch 40", Source = "test p.2",
        };
        var net = new UtilityNetworkDefinition { Name = "E", System = UtilitySystem.Electrical, Structures = { Hh("HH-1", h1), Hh("HH-2", h2) }, DuctBanks = new() { bank } };
        var m = UtilityGenerator.Generate(net, Array.Empty<SBM.Core.Alignments.Alignment>(), null);
        var b = m.DuctBanks.Single();
        Assert.Equal(60 - 2 * 0.6, b.Length, 9);
        Assert.Equal(6, b.Conduits.Count);
        Assert.All(b.Conduits, c => Assert.True(Closed(c)));
        Assert.True(Closed(b.Encasement));
        Assert.True(b.Encasement.HasExactSolids);
        double alen = Math.Sqrt(b.Length * b.Length + 0.01), od = In(4.5);
        double hole16 = 16 * Math.Pow(od / 2, 2) * Math.Tan(Math.PI / 16);   // circumscribed 16-gon (PlateBuilder.Circle)
        Assert.Equal((In(30) * In(22) - 6 * hole16) * alen, b.ConcreteVolume, 4);
        Assert.Equal(6 * alen, b.ConduitLength, 9);
        Assert.Single(m.Structures[0].Openings);
        Assert.Single(m.Structures[1].Openings);
        Assert.DoesNotContain(m.Diagnostics, x => x.Message.Contains("UNRESOLVED"));
        // centred group when the first conduit is not dimensioned: flagged
        bank.FirstConduitFromLeft = double.NaN;
        Assert.Contains(UtilityGenerator.Generate(net, Array.Empty<SBM.Core.Alignments.Alignment>(), null).Diagnostics, x => x.Message.Contains("duct bank DB-1 conduit position"));
        // IFC: assembly > encasement + conduits, handholes, voids
        var d = SbmDocument.New("u");
        bank.FirstConduitFromLeft = In(7.5);
        d.Project.Utilities.Add(net);
        string ifc = Path.Combine(Path.GetTempPath(), "sbm-db-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(ifc);
            string s = File.ReadAllText(ifc);
            Assert.Contains("'DUCT_BANK'", s);
            Assert.Contains("'ENCASEMENT'", s);
            Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(s, @"IFCCABLECARRIERSEGMENT\(.*\.CONDUITSEGMENT\.").Count);
            Assert.Contains("'HANDHOLE'", s);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(s, "IFCRELVOIDSELEMENT").Count);
            Assert.Contains(".ELECTRICAL.", s);
        }
        finally { File.Delete(ifc); }
    }

    [Fact]
    public void BridgeHungRun_Hangers_Clash_Clearance_Profile()
    {
        var d = SbmDocument.Sample();
        var bm = d.Model();
        var al = d.Alignment();
        var bd = bm.Definition;
        double s0 = bd.Supports[0].Station, s1 = bd.Supports[1].Station;
        double sa = s0 + (s1 - s0) * 0.3, sb = s0 + (s1 - s0) * 0.7;
        var pa = al.PointAt(sa, 0); var pb = al.PointAt(sb, 0);
        double z = al.ElevationAt(sa) - 6;
        var hung = new UtilityPipeDefinition
        {
            Name = "HP-1", StartX = pa.X, StartY = pa.Y, EndX = pb.X, EndY = pb.Y, InvertStart = z, InvertEnd = z - 0.05, InnerDiameter = In(12), WallThickness = In(0.5),
            Hangers = new UtilityHangerDefinition { Bridge = bd.Name, Spacing = Ft(10), RodDiameter = In(0.75), Type = "clevis", Source = "test p.3" },
        };
        // a buried pipe straight through a pier column
        var col = bm.Substructure!.Units.SelectMany(u => u.Solids).First(x => x.Kind == SubElementKind.Column);
        var cb = col.Mesh.Bounds(); var cc = (cb.Min + cb.Max) * 0.5;
        var through = new UtilityPipeDefinition { Name = "XP-1", StartX = cc.X - 3, StartY = cc.Y, EndX = cc.X + 3, EndY = cc.Y, InvertStart = cc.Z, InvertEnd = cc.Z - 0.01, InnerDiameter = 0.3, WallThickness = 0.03 };
        // a second network 0.25 m beside XP-1 with a 0.5 m separation requirement
        var other = new UtilityNetworkDefinition
        {
            Name = "W", System = UtilitySystem.Water, MinSeparation = 0.5,
            Pipes = { new() { Name = "WX", StartX = cc.X - 3, StartY = cc.Y + 0.36 + 0.25, EndX = cc.X + 3, EndY = cc.Y + 0.36 + 0.25, InvertStart = cc.Z, InvertEnd = cc.Z, InnerDiameter = 0.3, WallThickness = 0.03 } },
        };
        d.Project.Utilities.Add(new UtilityNetworkDefinition { Name = "S", Pipes = { hung, through } });
        d.Project.Utilities.Add(other);
        var nets = d.Utilities();
        var s = nets[0];
        var hp = s.Pipes.Single(p => p.Definition.Name == "HP-1");
        int n = (int)Math.Ceiling(hp.Length / Ft(10) - 1e-9);
        Assert.Equal(n, s.Hangers.Count);
        Assert.All(s.Hangers, h => { Assert.True(h.Length > 0.5, h.Name); Assert.True(Closed(h.Mesh)); Assert.StartsWith(bd.Name, h.Above); });
        Assert.Equal(hp.Length / n / 2, s.Hangers[0].Distance, 9);
        Assert.Contains(s.Diagnostics, x => x.Message.Contains("HP-1 hanger layout") && x.Message.Contains("UNRESOLVED"));
        // no clash of the hung pipe or its rods with its own bridge (rods end on the lowest face above)
        Assert.DoesNotContain(s.Clashes, c => c.Element.StartsWith("HP-1"));
        Assert.Contains(s.Clashes, c => c.Overlap && c.Element == "XP-1" && c.OtherKind == "substructure");
        var sep = nets[1].Clashes.Single(c => !c.Overlap);
        Assert.Equal("WX", sep.Element);
        Assert.Equal(0.25, sep.Value, 3);
        Assert.Equal(0.5, sep.Required, 9);
        Assert.Contains(s.Clashes, c => !c.Overlap && c.Element == "XP-1" && c.Other == "WX");
        // profile runs: each pipe once
        var runs = UtilityProfileViews.Runs(s);
        Assert.Equal(2, runs.Count);
        var dwg = UtilityProfileViews.Profile(runs[0], null, d.Project.Units);
        Assert.Contains(dwg.Layers, l => l.Style.Name == "P-UTIL-PIPE" && l.Polylines.Count == 2);
    }

    [Fact]
    public void Profile_StormRun_FlowOrder()
    {
        var a = new Vec2(0, 0);
        var net = new UtilityNetworkDefinition
        {
            Name = "S",
            Structures = { Mh("MH-1", a, 100, 96), Mh("MH-2", a + new Vec2(30, 0), 100, 95.5), Mh("MH-3", a + new Vec2(30, 25), 100, 96.5) },
            Pipes = { Pipe("P-1", "MH-1", "MH-2", 96.2, 95.9), Pipe("P-3", "MH-3", "MH-2", 96.7, 96.0) },
        };
        var m = UtilityGenerator.Generate(net, Array.Empty<SBM.Core.Alignments.Alignment>(), _ => (100.0, "existing ground"));
        var runs = UtilityProfileViews.Runs(m);
        Assert.Equal(2, runs.Count);
        Assert.Equal(new[] { "MH-1", "MH-2" }, runs[0].Structures.Select(x => x.Definition.Name));
        Assert.Equal(new[] { "MH-3", "MH-2" }, runs[1].Structures.Select(x => x.Definition.Name));
        var dwg = UtilityProfileViews.Profile(runs[0], _ => (100.0, "existing ground"), SBM.Core.Units.UnitSystem.Default);
        Assert.Contains(dwg.Layers, l => l.Style.Name == "S-GROUND" && l.Polylines.Count == 1);
        Assert.Contains(dwg.Layers, l => l.Style.Name == "P-UTIL-STRUCTURE" && l.Polylines.Count == 4);
        Assert.Contains(dwg.Layers.SelectMany(l => l.Texts), t => t.Text.Contains("INV OUT"));
    }
}
