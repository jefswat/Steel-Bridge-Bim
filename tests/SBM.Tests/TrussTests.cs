using Microsoft.Data.Sqlite;
using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Truss;
using SBM.IO.Database;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Generic steel truss generator (Truss/TrussGenerator): panel points on two planes, Pratt / Warren member counts and lengths, exact
/// built-up sections (volumes = area x cut length), web member setbacks, gussets, rivets, floor beams + stringers + deck, rope suspender, shoes,
/// IFC entities, database groups, file round trip, existing models unchanged.</summary>
public class TrussTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sbmtruss_" + Guid.NewGuid().ToString("N"));
    public TrussTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private const double Panel = 6.096, Depth = 6.096, Half = 4.572;   // 20 ft panels, 20 ft deep, trusses 30 ft c-c

    private static TrussSectionDefinition Plate(string n, double w, double d) => new() { Name = n, Kind = TrussSectionKind.Plate, Width = w, Depth = d, Label = n };

    /// <summary>6-panel PRATT (pratt = true) or WARREN truss with verticals at the panel points of the bottom chord only (pratt = false).</summary>
    internal static (BridgeProject p, BridgeDefinition b) Model(bool pratt = true, bool extras = true)
    {
        var al = new Alignment { Name = "A", StartStation = 0 };
        al.SetElements(new[] { new HorizontalElement(new Vec2(0, 0), 0, Ft(400), 0, 0) });
        al.Profile = new VerticalProfile { Vpis = { new Vpi(0, Ft(100)), new Vpi(Ft(400), Ft(100)) } };
        double s0 = Ft(100), z0 = Ft(60);
        var t = new TrussDefinition { Source = "test", Name = "Test truss", JointDefaults = new TrussJointDefaults { GussetThickness = In(0.75) } };
        t.Planes.Add(new TrussPlaneDefinition { Name = "N", Offset = -Half });
        t.Planes.Add(new TrussPlaneDefinition { Name = "S", Offset = Half });
        for (int i = 0; i <= 6; i++) t.Points.Add(new TrussPointDefinition($"L{i}", s0 + i * Panel, z0));
        if (pratt) for (int i = 1; i <= 5; i++) t.Points.Add(new TrussPointDefinition($"U{i}", s0 + i * Panel, z0 + Depth));
        else for (int i = 0; i < 6; i++) t.Points.Add(new TrussPointDefinition($"U{i}", s0 + (i + 0.5) * Panel, z0 + Depth));
        t.Sections.Add(new TrussSectionDefinition
        {
            Name = "BOX", Kind = TrussSectionKind.Box, Label = "box chord", WebDepth = In(24), WebPlies = { new TrussPly(0, In(0.5)) }, WebsOutToOut = In(20),
            Angle = "L4x4x1/2", TopPlates = { new TrussPlate(In(18), In(0.5)) }, BottomPlates = { new TrussPlate(In(18), In(0.5), true) },
        });
        t.Sections.Add(Plate("PL", In(16), In(12)));
        t.Sections.Add(new TrussSectionDefinition { Name = "H", Kind = TrussSectionKind.H, WebDepth = In(14), WebPlies = { new TrussPly(0, In(0.5)) }, Angle = "L4x3x3/8" });
        t.Sections.Add(new TrussSectionDefinition { Name = "CH", Kind = TrussSectionKind.LacedChannels, Channel = "C12x20.7", BackToBack = In(16),
            Lacing = new TrussLacing { BarWidth = In(2.5), BarThickness = In(0.375), Faces = "both" } });
        t.Sections.Add(new TrussSectionDefinition { Name = "FB", Kind = TrussSectionKind.Rolled, Designation = "W36x150" });
        t.Sections.Add(new TrussSectionDefinition { Name = "ST", Kind = TrussSectionKind.Rolled, Designation = "W21x62" });
        t.Sections.Add(new TrussSectionDefinition { Name = "OLD", Kind = TrussSectionKind.Rolled, Designation = "24 I 79.9" });   // not in AISC v16, no dimensions
        t.Sections.Add(new TrussSectionDefinition { Name = "NOM", Kind = TrussSectionKind.Nominal, Width = In(10), Depth = In(10), Unresolved = "UNRESOLVED (p.0): strut section - not given." });
        void M(string n, string a, string b, string sec, TrussMemberRole r, double plan = 0) => t.Members.Add(new TrussMemberDefinition { Name = n, From = a, To = b, Section = sec, Role = r, PlanLength = plan, Panel = n });
        for (int i = 0; i < 6; i++) M($"L{i}-L{i + 1}", $"L{i}", $"L{i + 1}", i == 1 ? "PL" : "BOX", TrussMemberRole.LowerChord, Panel);
        if (pratt)
        {
            for (int i = 1; i < 5; i++) M($"U{i}-U{i + 1}", $"U{i}", $"U{i + 1}", "BOX", TrussMemberRole.UpperChord);
            M("L0-U1", "L0", "U1", "BOX", TrussMemberRole.EndPost, Math.Sqrt(2) * Panel);
            M("U5-L6", "U5", "L6", "BOX", TrussMemberRole.EndPost);
            for (int i = 1; i <= 5; i++) M($"U{i}-L{i}", $"U{i}", $"L{i}", "CH", TrussMemberRole.Vertical, Depth);
            M("U1-L2", "U1", "L2", "H", TrussMemberRole.Diagonal); M("U2-L3", "U2", "L3", "H", TrussMemberRole.Diagonal);
            M("U4-L3", "U4", "L3", "H", TrussMemberRole.Diagonal); M("U5-L4", "U5", "L4", "H", TrussMemberRole.Diagonal);
        }
        else
        {
            for (int i = 0; i < 5; i++) M($"U{i}-U{i + 1}", $"U{i}", $"U{i + 1}", "BOX", TrussMemberRole.UpperChord);
            for (int i = 0; i < 6; i++) { M($"L{i}-U{i}", $"L{i}", $"U{i}", "H", TrussMemberRole.Diagonal); M($"U{i}-L{i + 1}", $"U{i}", $"L{i + 1}", "H", TrussMemberRole.Diagonal); }
        }
        if (extras)
        {
            for (int i = 0; i <= 6; i++)
                t.Members.Add(new TrussMemberDefinition { Name = $"FB{i}", From = $"N:L{i}", To = $"S:L{i}", Section = "FB", Role = TrussMemberRole.FloorBeam, OffsetZ = In(30) });
            t.Members.Add(new TrussMemberDefinition { Name = "LAT1", From = "N:L0", To = "S:L1", Section = "OLD", Role = TrussMemberRole.LowerLateral });
            t.Members.Add(new TrussMemberDefinition { Name = "STRUT3", From = "N:L3", To = "S:L3", Section = "NOM", Role = TrussMemberRole.Strut });
            t.Floor = new TrussFloorDefinition
            {
                StringerTopBelowGrade = In(8), StringersOnTop = false, StringerEndClearance = In(0.5),
                Stringers = { new TrussStringerLine { Name = "A", Offset = -Ft(8), Section = "ST" }, new TrussStringerLine { Name = "B", Offset = 0, Section = "ST" }, new TrussStringerLine { Name = "C", Offset = Ft(8), Section = "ST" } },
                Deck = new TrussDeckDefinition { Thickness = In(8), LeftOffset = -Ft(14), RightOffset = Ft(14), BeginStation = s0, EndStation = s0 + 6 * Panel },
            };
            t.Joints.Add(new TrussJointDefinition { Point = "L2", Plane = "N", Rivets = { new TrussRivetGroup { Member = "U1-L2", Count = 6, Rows = 2, Pitch = In(3), Gage = In(4), FirstFromEnd = In(1.5), Diameter = In(0.875) } } });
            t.Joints.Add(new TrussJointDefinition { Point = "U3", Outline = { new TrussUV(-1, -0.5), new TrussUV(1, -0.5), new TrussUV(1, 0.5), new TrussUV(-1, 0.5) }, GussetThickness = In(0.625) });
            t.Suspenders.Add(new SuspenderDefinition { Name = "S3", Plane = "N", Upper = "U3", Lower = "L3", RopeDiameter = In(4), Ropes = 2, RopeSpacing = In(10), SocketLength = In(40), SocketDiameter = In(13.75), PinDiameter = In(7.25), PlanLength = Depth });
            t.Shoes.Add(new TrussShoeDefinition { Name = "N-L0", Plane = "N", Point = "L0", Support = "P1", Type = TrussShoeType.Expansion, PinDiameter = In(6), Height = Ft(3), BodyLength = Ft(3), BodyWidth = Ft(2), MasonryLength = Ft(3.5), MasonryWidth = Ft(2.5), MasonryThickness = In(4) });
            t.Shoes.Add(new TrussShoeDefinition { Name = "N-L6", Plane = "N", Point = "L6", Support = "P2", Type = TrussShoeType.Fixed, PinDiameter = In(6), Height = Ft(3), BodyLength = Ft(3), BodyWidth = Ft(2), MasonryLength = Ft(3.5), MasonryWidth = Ft(2.5), MasonryThickness = In(4), SeatElevation = z0 - Ft(3) });
        }
        var b = new BridgeDefinition
        {
            Name = "Truss", AlignmentName = "A",
            Supports =
            {
                new SupportDefinition { Name = "P1", Type = SupportType.Pier, Station = s0, Substructure = new SubstructureDefinition { Enabled = false } },
                new SupportDefinition { Name = "P2", Type = SupportType.Pier, Station = s0 + 6 * Panel, Substructure = new SubstructureDefinition { Enabled = false } },
            },
            Truss = t,
        };
        b.Girders.Count = 0;
        return (new BridgeProject { Alignments = { al }, Bridges = { b } }, b);
    }

    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    [Fact]
    public void Pratt_MemberCountsLengthsAndChecks()
    {
        var (p, _) = Model(pratt: true, extras: false);
        var bm = Regen(p);
        var tm = bm.Steel!.Truss!;
        Assert.Equal(2 * 21, tm.Members.Count);               // 6 lower + 4 upper chords + 2 end posts + 5 verticals + 4 diagonals per plane
        Assert.Equal(2 * 4, tm.Members.Count(m => m.Role == TrussMemberRole.Diagonal));
        Assert.All(tm.Checks, c => Assert.True(c.Ok, c.Item));
        Assert.Contains(tm.Checks, c => c.Item.Contains("N-L0-U1"));
        var d = tm.Member("S-U1-L2")!;
        Assert.Equal(Math.Sqrt(2) * Panel, d.WorkLength, 6);
        // Plane offsets: N on the left (-), S on the right (+) of an alignment along +X.
        Assert.Equal(Half, tm.Point("N", "L0")!.World.Y, 6);
        Assert.Equal(-Half, tm.Point("S", "L0")!.World.Y, 6);
        Assert.Null(bm.Substructure);
        Assert.DoesNotContain(bm.Framing.Diagnostics, x => x.Severity == SBM.Core.Framing.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Warren_MemberCounts()
    {
        var (p, _) = Model(pratt: false, extras: false);
        var tm = Regen(p).Steel!.Truss!;
        Assert.Equal(2 * (6 + 5 + 12), tm.Members.Count);
        Assert.Equal(0, tm.Members.Count(m => m.Role == TrussMemberRole.Vertical));
        // every web member meets a chord at both ends: gussets at the 7 lower and 6 upper points of both planes, two plates each
        Assert.Equal(2 * 2 * 13, tm.Gussets);
    }

    [Fact]
    public void Sections_AreExactExtrusions_VolumesMatchAreaTimesLength()
    {
        var (p, b) = Model();
        var bm = Regen(p);
        var steel = bm.Steel!;
        foreach (var part in steel.Parts.Where(x => x.Category is PartCategory.TrussChord or PartCategory.TrussDiagonal or PartCategory.TrussVertical
            or PartCategory.TrussFloorBeam or PartCategory.TrussStringer or PartCategory.TrussGusset or PartCategory.Suspender))
            Assert.True(part.Mesh.HasExactSolids, part.Mark);
        // straight chord between collinear neighbours: cut = panel; plate volume = area x length
        var pl = steel.Parts.First(x => x.Mark == "N-L1-L2");
        Assert.Equal(In(16) * In(12) * Panel, pl.Volume, 9);
        // built-up box: area of the pieces x length (AISC angle with fillet: area within 1 % of the catalog)
        var bs = TrussSections.Build(b.Truss!.Section("BOX")!);
        double expected = 2 * In(24) * In(0.5) + 4 * SteelCatalog.FindAngle("L4x4x1/2")!.Area + 2 * In(18) * In(0.5);
        Assert.InRange(bs.Area, expected * 0.99, expected * 1.01);
        var box = steel.Parts.First(x => x.Mark == "N-L2-L3");
        Assert.Equal(bs.Area * Panel, box.Volume, 9);
        // laced channels: lacing bars add volume beyond the channels
        var ch = steel.Parts.First(x => x.Mark == "N-U3-L3");
        var cs = TrussSections.Build(b.Truss.Section("CH")!);
        Assert.True(ch.Volume > cs.Area * ch.Length * 1.02);
        // rolled shapes from AISC: nominal weight x length
        var st = steel.Parts.First(x => x.Category == PartCategory.TrussStringer);
        Assert.Equal(62, st.NominalWeightLbPerFt, 6);
        // shape not in AISC v16 without dimensions -> flagged envelope; nominal section flagged (dimension-only: normal layer)
        var lat = steel.Parts.First(x => x.Mark == "LAT1");
        Assert.NotNull(lat.Assumption);
        Assert.True(lat.Assumption!.DimensionOnly);
        Assert.Contains(steel.Diagnostics, x => x.Message.Contains("24 I 79.9"));
        Assert.NotNull(steel.Parts.First(x => x.Mark == "STRUT3").Assumption);
    }

    [Fact]
    public void PlateGirderMember_PartialCoverPlates_AreCentredAndExact()
    {
        var (p, b) = Model(pratt: true, extras: false);
        double span = 6 * Panel, plateLen = Ft(20);
        b.Truss!.Sections.Add(new TrussSectionDefinition
        {
            Name = "GIR", Kind = TrussSectionKind.H, WebVertical = true, WebDepth = In(60), WebPlies = { new TrussPly(In(60), In(0.5)) }, Angle = "L6x6x1/2", AngleProjection = In(0.25),
            TopPlates = { new TrussPlate(In(16), In(0.5)), new TrussPlate(In(16), In(0.5)) { Length = plateLen, Offset = Ft(10) } }, BottomPlates = { new TrussPlate(In(16), In(0.5)) { Length = plateLen } },
        });
        b.Truss.Members.Add(new TrussMemberDefinition { Name = "GIR", From = "L0", To = "L6", Section = "GIR", Role = TrussMemberRole.Girder, Panel = "Span 1" });
        var bm = Regen(p);
        var part = bm.Steel!.Parts.First(x => x.Mark == "N-GIR");
        Assert.Equal(PartCategory.TrussGirder, part.Category);
        Assert.Contains(bm.Steel.Assemblies, a => a.Kind == AssemblyKind.PlateGirderLine && a.Mark == "GIRDER-N");
        var bs = TrussSections.Build(b.Truss.Section("GIR")!);
        Assert.Equal(3, bs.Pieces.Count(x => x.Length > 0 || x.Label.StartsWith("flange PL")));
        double full = bs.Pieces.Where(x => x.Length == 0).Sum(x => Math.Abs(SBM.Core.Solids.MeshBuilder.SignedArea(x.Polygon)));
        double partial = bs.Pieces.Where(x => x.Length > 0).Sum(x => Math.Abs(SBM.Core.Solids.MeshBuilder.SignedArea(x.Polygon)));
        Assert.Equal(full * span + partial * plateLen, part.Volume, 8);
        Assert.True(part.Mesh.HasExactSolids);
        Assert.Equal(span, part.Length, 6);
    }

    [Fact]
    public void RolledCoverPlates_StopAtTheGussets_PartialPlies_ShapeArea()
    {
        var (p, b) = Model(pratt: true, extras: false);
        var t = b.Truss!;
        double sq = 0.00064516;
        string json0 = System.Text.Json.JsonSerializer.Serialize(t);
        foreach (var key in new[] { "ShapeArea", "BetweenGussets", "\"Start\"" }) Assert.DoesNotContain(key, json0);
        // rolled diagonal with the printed area (web thickened) and one cover plate on each flange between the gussets (QC PTC I009 / I017)
        t.Sections.Add(new TrussSectionDefinition
        {
            Name = "WCP", Kind = TrussSectionKind.Rolled, Designation = "W14x90", Rotated = true, ShapeArea = 27.0 * sq,
            TopPlates = { new TrussPlate(In(14), In(0.75)) { BetweenGussets = true } }, BottomPlates = { new TrussPlate(In(14), In(0.75)) { BetweenGussets = true } },
        });
        t.Members.First(m => m.Name == "U2-L3").Section = "WCP";
        // partial outer web ply on a chord box: 5 ft from 2 ft after the start working point
        var box2 = new TrussSectionDefinition
        {
            Name = "BOX2", Kind = TrussSectionKind.Box, WebDepth = In(24), WebsOutToOut = In(20), Angle = "L4x4x1/2",
            WebPlies = { new TrussPly(0, In(0.375)) { Start = Ft(2), Length = Ft(5) }, new TrussPly(0, In(0.5)) },
        };
        t.Sections.Add(box2);
        t.Members.First(m => m.Name == "L2-L3").Section = "BOX2";
        // channel member at a printed channel area
        t.Section("CH")!.ShapeArea = 5.9 * sq;
        var bm = Regen(p);
        var tm = bm.Steel!.Truss!;
        var wcp = TrussSections.Build(t.Section("WCP")!);
        Assert.Equal((27.0 + 2 * 14 * 0.75) * sq, wcp.Area, 9);
        Assert.True(wcp.HalfWidthAtJoints < wcp.HalfWidth - In(0.7));
        var d = tm.Member("N-U2-L3")!;
        var part = bm.Steel.Parts.First(x => x.Mark == "N-U2-L3");
        double plateLen = (part.Volume - 27.0 * sq * d.CutLength) / (2 * 14 * 0.75 * sq);
        double lap = Math.Max(wcp.Depth, In(18));
        Assert.True(plateLen > Ft(5) && plateLen <= d.CutLength - 2 * lap + 1e-6, $"plate length {plateLen} vs cut {d.CutLength}");
        Assert.True(part.Mesh.HasExactSolids);
        Assert.Equal(0, part.NominalWeightLbPerFt);
        // no "wider than the gusset" CHECK for the plates that stop at the gussets
        Assert.DoesNotContain(bm.Steel.Diagnostics, x => x.Message.Contains("N-U2-L3 is"));
        var c = tm.Member("N-L2-L3")!;
        var bx = TrussSections.Build(box2);
        var ply = bx.Pieces.Where(x => !double.IsNaN(x.Start)).ToList();
        Assert.Equal(2, ply.Count);
        double full = bx.Pieces.Where(x => double.IsNaN(x.Start)).Sum(x => Math.Abs(SBM.Core.Solids.MeshBuilder.SignedArea(x.Polygon)));
        double partA = ply.Sum(x => Math.Abs(SBM.Core.Solids.MeshBuilder.SignedArea(x.Polygon)));
        Assert.Equal(full * c.CutLength + partA * Ft(5), bm.Steel.Parts.First(x => x.Mark == "N-L2-L3").Volume, 8);
        Assert.Equal(2 * 5.9 * sq, TrussSections.Build(t.Section("CH")!).Area, 9);
        // the inputs are written when set and read back
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!;
        Assert.True(back.Section("WCP")!.TopPlates[0].BetweenGussets);
        Assert.Equal(Ft(2), back.Section("BOX2")!.WebPlies[0].Start!.Value, 9);
        Assert.Equal(27.0 * sq, back.Section("WCP")!.ShapeArea, 12);
    }

    [Fact]
    public void WebMembers_StopAtTheChordEnvelope()
    {
        var (p, b) = Model();
        var tm = Regen(p).Steel!.Truss!;
        var box = TrussSections.Build(b.Truss!.Section("BOX")!);
        var h = TrussSections.Build(b.Truss.Section("H")!);
        // vertical (90 deg to the chords): setback = chord half depth (+ 0 clearance) at both ends
        var v = tm.Member("N-U2-L2")!;
        var ch = TrussSections.Build(b.Truss.Section("CH")!);
        Assert.Equal(Depth - box.YMax + box.YMin, v.CutLength, 6);
        // 45 deg diagonal: (h + ym cos) / sin at each end
        var d = tm.Member("N-U2-L3")!;
        double ym = Math.Max(h.YMax, -h.YMin), c = Math.Sqrt(0.5);
        double sb = (box.YMax + ym * c) / c, sa = (-box.YMin + ym * c) / c;
        Assert.Equal(d.WorkLength - sa - sb, d.CutLength, 6);
        Assert.True(ch.HalfWidth <= box.HalfWidth + 1e-9);
    }

    [Fact]
    public void LateralStopsClearOfPortal_OpenSocketJaw()
    {
        // lateral from the N:U3 corner to S:U4: without a portal both ends stop at the chord faces (symmetric); with a portal N:U3-S:U3 the U3 end
        // stops where every corner of the lateral's end section is beyond the portal's side face
        TrussMemberResult Lat(bool portal, out BridgeDefinition bd)
        {
            var (p, b) = Model();
            b.Truss!.Members.Add(new TrussMemberDefinition { Name = "LAT", From = "N:U3", To = "S:U4", Section = "PL", Role = TrussMemberRole.UpperLateral });
            if (portal) b.Truss.Members.Add(new TrussMemberDefinition { Name = "PORTAL", From = "N:U3", To = "S:U3", Section = "PL", Role = TrussMemberRole.Portal });
            bd = b;
            return Regen(p).Steel!.Truss!.Members.First(m => m.Name == "LAT");
        }
        var l0 = Lat(false, out _);
        var l1 = Lat(true, out var b1);
        double w = l0.WorkLength, s0 = (w - l0.CutLength) / 2;
        double len = Math.Sqrt(Panel * Panel + 4 * Half * Half), k = Panel / len;
        double expected = (In(8) + In(8) * (2 * Half / len)) / k + b1.Truss!.JointDefaults.WebMemberClearance;
        Assert.True(expected > s0);
        Assert.Equal(w - expected - s0, l1.CutLength, 6);

        // open socket: 7 1/4 beyond the pin, 4 in. jaw over 1'-8 from the pin; the rope runs between the baskets
        var (p2, b2) = Model();
        var s = b2.Truss!.Suspenders[0];
        s.SocketBeyondPin = In(7.25); s.SocketJawWidth = In(4); s.SocketJawLength = In(20); s.RopesAlongBridge = true;
        var steel = Regen(p2).Steel!;
        var rope = steel.Parts.First(x => x.Category == PartCategory.Suspender);
        Assert.Equal(Depth - 2 * (In(40) - In(7.25)), rope.Length, 6);
        var sock = steel.Parts.First(x => x.Category == PartCategory.SuspenderSocket);
        double r = In(13.75) / 2, h = In(2);
        double full = 0.5 * 24 * r * r * Math.Sin(2 * Math.PI / 24) * (In(40) - In(7.25) - In(20));
        double lugs = (Math.PI * r * r - 2 * (h * Math.Sqrt(r * r - h * h) + r * r * Math.Asin(h / r))) * (In(20) + In(7.25));
        Assert.Equal(full + lugs, sock.Volume, (full + lugs) * 0.01);
        // the .sbm stays unchanged for suspenders without the new inputs
        Assert.DoesNotContain("SocketJawWidth", System.Text.Json.JsonSerializer.Serialize(new SuspenderDefinition()));
    }

    [Fact]
    public void Joints_FloorSuspendersShoes()
    {
        var (p, _) = Model();
        var bm = Regen(p);
        var steel = bm.Steel!;
        var tm = steel.Truss!;
        // explicit outline at U3 (both planes) -> not flagged; envelope gussets flagged dimension-only
        var g = steel.Parts.Where(x => x.Category == PartCategory.TrussGusset).ToList();
        Assert.Null(steel.Parts.First(x => x.Mark == "N-GU3-L").Assumption);
        Assert.Equal(2 * In(0.625), steel.Parts.First(x => x.Mark == "N-GU3-L").Volume, 9);
        Assert.True(steel.Parts.First(x => x.Mark == "N-GL2-L").Assumption!.DimensionOnly);
        // rivets: 6 per gusset side x 2 sides
        Assert.Equal(12, steel.Bolts.Count(x => x.Kind == BoltKind.Rivet));
        // floor: 7 floor beams, 3 stringer lines x 6 bays, deck slab
        Assert.Equal(7, steel.Parts.Count(x => x.Category == PartCategory.TrussFloorBeam));
        Assert.Equal(18, steel.Parts.Count(x => x.Category == PartCategory.TrussStringer));
        Assert.NotNull(bm.Concrete!.Deck);
        Assert.Equal(In(8) * Ft(28) * 6 * Panel, Math.Abs(bm.Concrete.Deck!.Volume()), 6);
        // suspender: 2 ropes + 4 sockets + 2 pins, pin-to-pin length check
        Assert.Equal(2, steel.Parts.Count(x => x.Category == PartCategory.Suspender));
        Assert.Equal(4, steel.Parts.Count(x => x.Category == PartCategory.SuspenderSocket));
        Assert.Single(tm.Suspenders);
        Assert.Contains(tm.Checks, c => c.Item.StartsWith("suspender S3") && c.Ok);
        // shoes: bearing seats with location, fixed shoe seat check
        Assert.Equal(2, steel.BearingSeats.Count);
        Assert.All(steel.BearingSeats, s => Assert.NotNull(s.Location));
        Assert.Contains(tm.Checks, c => c.Item.StartsWith("shoe N-L6") && c.Ok);
        Assert.Equal(Ft(57), steel.BearingSeats.First(s => s.Label == "N-L0").SeatElevation, 6);
    }

    [Fact]
    public void GussetLayers_StackOutward_VolumesGripAndRoundTrip()
    {
        var (p0, _) = Model();
        var bm0 = Regen(p0);
        var (p, b) = Model();
        var jd = b.Truss!.Joints.First(j => j.Point == "L2" && j.Plane == "N");
        jd.GussetLayers = new()
        {
            new TrussGussetLayer { Thickness = In(0.5), Label = "2-1/2 Gussets" },                     // whole gusset outline
            new TrussGussetLayer { Thickness = In(0.375), Outline = { new TrussUV(-0.2, -0.15), new TrussUV(0.2, -0.15), new TrussUV(0.2, 0.15), new TrussUV(-0.2, 0.15) },
                Unresolved = "UNRESOLVED (p.0): layer outline scaled - test." },
        };
        // round trip: the layers survive; a joint without layers writes no GussetLayers key
        var p2 = BridgeProject.FromJson(p.ToJson());
        Assert.Equal(2, p2.Bridges[0].Truss!.Joints.First(j => j.Point == "L2" && j.Plane == "N").GussetLayers!.Count);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(p.ToJson(), "GussetLayers").Count);
        var bm = Regen(p);
        var steel = bm.Steel!;
        var tm = steel.Truss!;
        // stack record: inner 3/4 + 1/2 + 3/8 per face
        var st = tm.GussetStacks.Single(s => s.Plane == "N" && s.Point == "L2");
        Assert.Equal(3, st.Layers);
        Assert.Equal(In(0.75 + 0.5 + 0.375), st.TotalThickness, 9);
        Assert.Equal(1, tm.GussetStacks.Single(s => s.Plane == "S" && s.Point == "L2").Layers);
        Assert.Equal(bm0.Steel!.Truss!.Gussets + 4, tm.Gussets);
        // volumes: layer 2 = inner outline x 1/2, layer 3 = 0.4 x 0.3 m x 3/8
        var inner = steel.Parts.First(x => x.Mark == "N-GL2-R");
        var l2 = steel.Parts.First(x => x.Mark == "N-GL2-R2");
        var l3 = steel.Parts.First(x => x.Mark == "N-GL2-L3");
        Assert.Equal(inner.Volume * 0.5 / 0.75, l2.Volume, 9);
        Assert.Equal(0.12 * In(0.375), l3.Volume, 9);
        Assert.Equal(PartCategory.TrussGusset, l3.Category);
        Assert.True(l3.Assumption!.DimensionOnly);
        Assert.True(steel.Parts.First(x => x.Mark == "N-GL2-L2").Assumption!.DimensionOnly);   // envelope outline inherited -> its flag
        // stack order: across the plane (world y here) the plates of one face touch: inner | layer 2 | layer 3, outward
        (double lo, double hi) Y(string mark) { var v = steel.Parts.First(x => x.Mark == mark).Mesh.Positions; return (v.Min(q => q.Y), v.Max(q => q.Y)); }
        foreach (var f in new[] { "L", "R" })
        {
            var (a0, a1) = Y($"N-GL2-{f}"); var (b0, b1) = Y($"N-GL2-{f}2"); var (c0, c1) = Y($"N-GL2-{f}3");
            bool outwardPlus = b0 >= a1 - 1e-9;
            if (outwardPlus) { Assert.Equal(a1, b0, 9); Assert.Equal(b1, c0, 9); }
            else { Assert.Equal(a0, b1, 9); Assert.Equal(b0, c1, 9); }
            Assert.Equal(In(0.5), b1 - b0, 9);
            Assert.Equal(In(0.375), c1 - c0, 9);
        }
        // rivets of the L2 group: grip and head move out by the layers they pass through (1/2 everywhere, + 3/8 inside the small layer)
        var r0 = bm0.Steel!.Bolts.Where(x => x.Kind == BoltKind.Rivet).OrderBy(x => x.Frame.Origin.X).ThenBy(x => x.Frame.Origin.Z).ThenBy(x => x.Frame.Origin.Y).ToList();
        var r1 = steel.Bolts.Where(x => x.Kind == BoltKind.Rivet).OrderBy(x => x.Frame.Origin.X).ThenBy(x => x.Frame.Origin.Z).ThenBy(x => x.Frame.Origin.Y).ToList();
        Assert.Equal(r0.Count, r1.Count);
        for (int i = 0; i < r0.Count; i++)
        {
            double d = r1[i].Grip - r0[i].Grip;
            Assert.True(Math.Abs(d - In(0.5)) < 1e-9 || Math.Abs(d - In(0.875)) < 1e-9, $"grip change {d / 0.0254} in");
            Assert.Equal(d, Math.Abs(r1[i].Frame.Origin.Y - r0[i].Frame.Origin.Y), 9);
        }
    }

    [Fact]
    public void DistributedRivets_CleanStack_NoOverlap_Beyond_FlushHead()
    {
        var (p, b) = Model();
        var jd = b.Truss!.Joints.First(j => j.Point == "L2" && j.Plane == "N");
        jd.Rivets.Clear();
        jd.Rivets.Add(new TrussRivetGroup { Member = "L2-L3", Count = 30, Diameter = In(0.875), Distributed = true });
        jd.Rivets.Add(new TrussRivetGroup { Member = "U1-L2", Count = 12, Diameter = In(0.875), Distributed = true });
        jd.Rivets.Add(new TrussRivetGroup { Member = "L2-L3", Count = 8, Diameter = In(0.875), Distributed = true, Beyond = true, BeyondLength = In(18) });
        // round trip: Beyond / BeyondLength written only where set
        var json = p.ToJson();
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(json, "\"Beyond\"").Count);
        Assert.Equal(8, BridgeProject.FromJson(json).Bridges[0].Truss!.Joints.First(j => j.Point == "L2" && j.Plane == "N").Rivets.Single(r => r.Beyond).Count);
        Assert.DoesNotContain("RivetHeadsFlushAtSolePlates", json);
        var steel = Regen(p).Steel!;
        var rv = steel.Bolts.Where(x => x.Kind == BoltKind.Rivet).ToList();
        Assert.Equal(2 * (30 + 12 + 8), rv.Count);
        // no two rivets of one face closer than 1.6 d (button heads at most touching)
        var faceHeads = rv.Select(x => x.Frame.Origin).ToList();
        for (int i = 0; i < faceHeads.Count; i++)
            for (int j = i + 1; j < faceHeads.Count; j++)
                Assert.True((faceHeads[i] - faceHeads[j]).Length >= 1.6 * In(0.875) - 1e-6, "rivet heads overlap");
        // clean stack: every rivet passes a contiguous plate stack (gusset + 1/2 ply, + an angle leg where it lies on one) - never an angle root / horizontal leg
        var grips = rv.Select(x => Math.Round(x.Grip / 0.0254 * 16) / 16).Distinct().OrderBy(x => x).ToList();
        Assert.Equal(new[] { 0.5, 1.25, 1.75, 4.125 }, grips.ToArray());   // ply | gusset + ply | gusset + ply + angle leg | H diagonal: gusset + 3" fill gap + angle leg
        // beyond: 16 rivets with their heads on the member (no gusset in the grip: 1/2 ply, or ply + angle leg)
        Assert.True(rv.Count(x => x.Grip < In(0.75) + 1e-9) >= 16);
        // flush end: one head less
        Assert.True(SteelModel.RivetMesh(In(1), In(2), flushEnd: true).Positions.Count < SteelModel.RivetMesh(In(1), In(2)).Positions.Count);
    }

    [Fact]
    public void GussetOutline_Given_ReplacesTheEnvelope_RivetCoverCheck()
    {
        // envelope: the L2 rivets (pitch group on U1-L2) are covered, cover record without an outline
        var (p0, _) = Model();
        var tm0 = Regen(p0).Steel!.Truss!;
        var c0 = tm0.GussetRivetCovers.Single(c => c.Plane == "N" && c.Point == "L2");
        Assert.Equal(6, c0.Rivets);
        Assert.False(c0.OutlineGiven);

        // given outline (3 x 2 m rectangle about the point): plate = that area, flag basis says "outline as given", every rivet covered
        var (p, b) = Model();
        var jd = b.Truss!.Joints.First(j => j.Point == "L2" && j.Plane == "N");
        jd.Outline = new() { new TrussUV(-1.5, -0.5), new TrussUV(1.5, -0.5), new TrussUV(1.5, 1.5), new TrussUV(-1.5, 1.5) };
        jd.Unresolved = "UNRESOLVED (p.0): gusset outline scaled - test.";
        var bm = Regen(p);
        var steel = bm.Steel!;
        var g = steel.Parts.First(x => x.Mark == "N-GL2-R");
        Assert.Equal(6.0 * In(0.75), g.Volume, 6);
        Assert.Contains("outline as given", g.Assumption!.Basis);
        Assert.True(g.Assumption.DimensionOnly);
        var c1 = steel.Truss!.GussetRivetCovers.Single(c => c.Plane == "N" && c.Point == "L2");
        Assert.True(c1.Ok && c1.OutlineGiven && c1.Rivets == 6 && c1.MinEdge >= c1.RequiredEdge);

        // a small outline leaves the pitch rivets outside: reported, never moved
        jd.Outline = new() { new TrussUV(-0.05, -0.05), new TrussUV(0.05, -0.05), new TrussUV(0.05, 0.05), new TrussUV(-0.05, 0.05) };
        bm = Regen(p);
        var c2 = bm.Steel!.Truss!.GussetRivetCovers.Single(c => c.Plane == "N" && c.Point == "L2");
        Assert.False(c2.Ok);
        Assert.True(c2.MinEdge < 0);
        Assert.Contains(bm.Steel.Diagnostics, d => d.Message.Contains("Truss CHECK joint L2 (N)") && d.Message.Contains("outside the gusset outline"));
    }

    [Fact]
    public void EndChordExtension_CoversTheShoe_TaperedMember()
    {
        // default: the end chords are cut square at L0 / L6 -> the shoe tops (4 1/2 in. each side of the pin) project past them: CHECK warnings
        var (p, b) = Model();
        b.Truss!.Joints.Add(new TrussJointDefinition { Point = "L0", GussetThickness = In(0.5) });   // envelope gusset at the end joint
        var bm = Regen(p);
        Assert.Contains(bm.Steel!.Diagnostics, d => d.Message.Contains("CHECK shoe N-L0") && d.Message.Contains("beyond the end of chord N-L0-L1"));
        Assert.Contains(bm.Steel!.Diagnostics, d => d.Message.Contains("CHECK shoe N-L6") && d.Message.Contains("beyond the end of chord N-L5-L6"));
        var al = bm.Framing.Alignment;
        double MinSta(SteelPart q) => q.Mesh.Positions.Min(v => al.Inverse(v.XY).Station);
        double s0 = Ft(100);
        Assert.Equal(s0, MinSta(bm.Steel.Parts.First(x => x.Mark == "N-L0-L1")), 4);
        double gussetBefore = MinSta(bm.Steel.Parts.First(x => x.Mark == "N-GL0-L"));

        // chord ends extended 12 in. past the end points (negative setbacks): no CHECK, the chord and the envelope gusset reach 12 in. before L0
        b.Truss!.Members.First(m => m.Name == "L0-L1").StartSetback = -In(12);
        b.Truss.Members.First(m => m.Name == "L5-L6").EndSetback = -In(12);
        // tapered member: floor beam FB2 loses 10 in. of depth at its S end (bottom raised, top straight)
        b.Truss.Members.First(m => m.Name == "FB2").DepthReductionEnd = In(10);
        bm = Regen(p);
        Assert.DoesNotContain(bm.Steel!.Diagnostics, d => d.Message.Contains("beyond the end of chord"));
        Assert.Equal(s0 - In(12), MinSta(bm.Steel.Parts.First(x => x.Mark == "N-L0-L1")), 4);
        Assert.True(MinSta(bm.Steel.Parts.First(x => x.Mark == "N-GL0-L")) < Math.Min(gussetBefore, s0 - In(12)) + 1e-6);
        var fb = bm.Steel.Parts.First(x => x.Mark == "FB2");
        var pts = fb.Mesh.Positions.Select(v => (o: al.Inverse(v.XY).Offset, z: v.Z)).ToList();
        double oMin = pts.Min(q => q.o), oMax = pts.Max(q => q.o);
        double DepthNear(double o) { var s = pts.Where(q => Math.Abs(q.o - o) < 1e-3).ToList(); return s.Max(q => q.z) - s.Min(q => q.z); }
        double d0 = DepthNear(oMin), d1 = DepthNear(oMax);
        Assert.Equal(In(10), d0 - d1, 4);
        Assert.Equal(pts.Where(q => Math.Abs(q.o - oMin) < 1e-3).Max(q => q.z), pts.Where(q => Math.Abs(q.o - oMax) < 1e-3).Max(q => q.z), 6);   // top straight
        Assert.Contains(bm.Steel.Diagnostics, d => d.Message.Contains("tapered members"));
        // round trip keeps the new inputs; defaults are omitted from the file
        string json = p.ToJson();
        var back = BridgeProject.FromJson(json).Bridges[0].Truss!;
        Assert.Equal(In(10), back.Members.First(m => m.Name == "FB2").DepthReductionEnd, 9);
        Assert.Equal(-In(12), back.Members.First(m => m.Name == "L0-L1").StartSetback!.Value, 9);
        Assert.DoesNotContain("DepthReductionStart", json);
    }

    [Fact]
    public void Ifc_WritesTrussEntities_AndDatabaseGroups()
    {
        var (p, _) = Model();
        var bm = Regen(p);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "truss.ifc", p, new[] { new IfcBridgeData(bm.Framing, bm.Steel, bm.Concrete) });
        string ifc = sw.ToString();
        foreach (var e in new[] { ".CHORD.", ".BRACE.", ".STRUT.", ".GUSSET_PLATE.", ".TRUSS.", ".RIVET.", "IFCBEARING(", ".ROCKER.", "'FIXED_PIN_SHOE'", ".STRINGER.",
            "'FLOOR_BEAM'", ".SUSPENDER.", "'CABLE_SOCKET'", ".SUSPENSIONASSEMBLY.", ".BEAM_GRID.", ".CROSS_BRACING.", "IFCSLAB(", "IFCEXTRUDEDAREASOLID" })
            Assert.Contains(e, ifc);
        var doc = SbmDocument.Attach(p);
        doc.Regenerate();
        string db = Path.Combine(_dir, "t.sqlite");
        doc.ExportDatabase(db);
        using var cn = ModelDatabase.Open(db);
        Assert.Equal(1, ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements WHERE level = 'group' AND category = 'truss'"));
        Assert.Equal(2, ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements WHERE level = 'assembly' AND category = 'truss line'"));
        Assert.True(ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements WHERE level = 'group' AND category = 'truss panel'") > 10);
        Assert.True(ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements WHERE level = 'group' AND category = 'truss joint'") > 10);
        Assert.Equal(12, ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements WHERE category = 'rivet'"));
        Assert.True(ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements WHERE category = 'truss chord'") > 0);
    }

    [Fact]
    public void ChordThroughAPoint_FramesTheVertical_AndAnglesParse()
    {
        var (p0, _) = Model(pratt: true, extras: false);
        double cut0 = Regen(p0).Steel!.Truss!.Member("N-U1-L1")!.CutLength;
        var (p, b) = Model(pratt: true, extras: false);
        var t = b.Truss!;
        t.Members.RemoveAll(m => m.Name is "L0-L1" or "L1-L2");
        t.Members.Add(new TrussMemberDefinition { Name = "L0-L2", From = "L0", To = "L2", Section = "BOX", Role = TrussMemberRole.LowerChord, PlanLength = 2 * Panel });
        var tm = Regen(p).Steel!.Truss!;
        // U1-L1 stops at the L0-L2 chord passing through L1 exactly as at the two chord members ending there (the U1 end is unchanged)
        Assert.Equal(cut0, tm.Member("N-U1-L1")!.CutLength, 9);
        Assert.Contains(tm.Steel.Parts, x => x.Mark == "N-GL1-L");
        Assert.All(tm.Checks, c => Assert.True(c.Ok, c.Item));
        var a = TrussSections.ParseAngle("L7x4x7/8")!.Value;
        Assert.Equal(In(7), a.a, 9); Assert.Equal(In(4), a.b, 9); Assert.Equal(In(0.875), a.t, 9);
        Assert.Equal(In(1.125), TrussSections.ParseAngle("L8x8x1-1/8")!.Value.t, 9);
    }

    [Fact]
    public void DeckItems_SplicePlates_Stiffeners_ShoeMaterial()
    {
        var (p, b) = Model();
        var t = b.Truss!;
        var json0 = System.Text.Json.JsonSerializer.Serialize(t, new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        // none of the optional inputs set: nothing new in the file
        foreach (var key in new[] { "LineItems", "Plates", "Stiffeners", "HaunchDepth", "BodyMaterial" }) Assert.DoesNotContain($"\"{key}\"", json0);
        double s0 = Ft(100), s1 = s0 + 6 * Panel;
        // haunch (thickening) under the 3 stringer lines: 1 in deep, 17 in at the bottom, 12:12 sides -> area (17 + 19) / 2 x 1 in2 per line
        t.Floor!.Deck!.HaunchDepth = In(1); t.Floor.Deck.HaunchBottomWidth = In(17); t.Floor.Deck.HaunchSideSlope = 1;
        // concrete curb along the right edge (12 x 9 in), steel plate across at mid-length (24 x 1 in, curb to curb)
        t.LineItems = new()
        {
            new TrussLineItemDefinition { Name = "curb R", Kind = TrussLineItemKind.Concrete, ReferenceOffset = Ft(12), Side = 1, From = s0, To = s1,
                Profile = { new TrussUV(0, 0), new TrussUV(In(12), 0), new TrussUV(In(12), In(9)), new TrussUV(0, In(9)) } },
            new TrussLineItemDefinition { Name = "dam", Kind = TrussLineItemKind.Steel, Category = PartCategory.JointArmor, Transverse = true, Station = s0 + 3 * Panel, From = -Ft(12), To = Ft(12),
                Profile = { new TrussUV(-In(12), -In(1)), new TrussUV(In(12), -In(1)), new TrussUV(In(12), 0), new TrussUV(-In(12), 0) }, Unresolved = "UNRESOLVED (p.0): dam - test." },
        };
        // splice plates at L3 (chord L2-L3 / L3-L4 box): fill on top of the left member, splice plate over it, web splice inside the webs
        t.Joints.Add(new TrussJointDefinition { Point = "L3", Plane = "N", Plates = new()
        {
            new TrussJointPlate { Face = TrussPlateFace.Top, Width = In(18), Thickness = In(0.5), Length = In(24), Label = "spl" },
            new TrussJointPlate { Face = TrussPlateFace.Top, Width = In(18), Thickness = In(0.25), Length = In(10), Offset = -In(5), Fill = true, Label = "fill" },
            new TrussJointPlate { Face = TrussPlateFace.WebInside, Width = In(16), Thickness = In(0.5), Length = In(30), Label = "web spl" },
        } });
        // stiffeners on a plate girder member (H, web vertical): 3 pairs of L6x4x1/2 + one web splice plate pair
        t.Sections.Add(new TrussSectionDefinition { Name = "GIR", Kind = TrussSectionKind.H, WebVertical = true, WebDepth = In(60), WebPlies = { new TrussPly(In(60), In(0.5)) }, Angle = "L6x6x1/2" });
        t.Members.Add(new TrussMemberDefinition { Name = "GIR", From = "L0", To = "L6", Plane = "N", Section = "GIR", Role = TrussMemberRole.Girder, OffsetZ = -Ft(10), Stiffeners = new()
        {
            new TrussStiffenerSet { Angle = "L6x4x1/2", Height = In(48), Distances = { Ft(10), Ft(20), Ft(30) } },
            new TrussStiffenerSet { PlateWidth = In(20), PlateThickness = In(0.375), Height = In(40), Distances = { 3 * Panel } },
        } });
        t.Shoes[0].BodyMaterial = "S";
        var bm = Regen(p);
        var c = bm.Concrete!;
        Assert.NotNull(c.Haunches);
        // top on the slab soffit (follows the crown of the alignment's cross slope), bottom level at the line: area by the shoelace formula
        var al = p.Alignments[0];
        double Area(double o)
        {
            double sm = s0 + Panel, wt = In(9.5), wb = In(8.5), T = In(8);
            var q = new[] { (o - wt, al.ElevationAt(sm, o - wt) - T), (o - wb, al.ElevationAt(sm, o) - T - In(1)), (o + wb, al.ElevationAt(sm, o) - T - In(1)), (o + wt, al.ElevationAt(sm, o + wt) - T) };
            double a = 0; for (int i = 0; i < 4; i++) { var (x0, y0) = q[i]; var (x1, y1) = q[(i + 1) % 4]; a += x0 * y1 - x1 * y0; }
            return Math.Abs(a) / 2;
        }
        Assert.Equal((Area(-Ft(8)) + Area(0) + Area(Ft(8))) * 6 * Panel, c.HaunchVolume, 6);
        var curb = Assert.Single(c.Barriers);
        Assert.Equal("curb R", curb.Name);
        Assert.Equal(In(12) * In(9) * 6 * Panel, Math.Abs(curb.Mesh.Volume()), 6);
        var st = bm.Steel!;
        var dam = st.Parts.Single(x => x.Mark == "dam");
        Assert.Equal(In(24) * In(1) * Ft(24), dam.Volume, 6);
        Assert.NotNull(dam.Assumption);
        Assert.Contains(st.Assemblies, a => a.Kind == AssemblyKind.ExpansionJoint);
        // dam top flush with the grade at the CL
        Assert.Equal(Ft(100), dam.Mesh.Bounds().Max.Z, 6);
        var spl = st.Parts.Where(x => x.Category == PartCategory.SplicePlate && x.Mark.StartsWith("N-SPL3")).ToList();
        var fill = st.Parts.Single(x => x.Category == PartCategory.FillerPlate);
        Assert.Equal(2, spl.Count);
        Assert.Equal(In(18) * In(0.25) * In(10), fill.Volume, 9);
        var box = TrussSections.Build(t.Section("BOX")!);
        double ztop = bm.Steel!.Truss!.Point("N", "L3")!.World.Z + box.YMax;
        var top = spl.Single(x => x.Description.StartsWith("spl "));
        Assert.Equal(ztop + In(0.25), top.Mesh.Bounds().Min.Z, 6);     // on the fill
        Assert.Equal(In(18) * In(0.5) * In(24), top.Volume, 9);
        var web = spl.Single(x => x.Description.StartsWith("web spl"));
        Assert.Equal(2 * In(16) * In(0.5) * In(30), web.Volume, 9);   // one plate per web
        Assert.Equal(3, bm.Steel.Truss.SplicePlates);
        var stf = st.Parts.Where(x => x.Mark.StartsWith("N-GIR-ST")).ToList();
        Assert.Equal(2, stf.Count);
        var ang = TrussSections.ParseAngle("L6x4x1/2")!.Value;
        double angArea = (ang.a + ang.b - ang.t) * ang.t;
        Assert.Equal(3 * 2 * angArea * In(48), stf.Single(x => x.Category == PartCategory.IntermediateStiffener).Volume, 9);
        Assert.Equal(2 * In(20) * In(0.375) * In(40), stf.Single(x => x.Category == PartCategory.SplicePlate).Volume, 9);
        Assert.Contains("(S)", st.Parts.Single(x => x.Mark == "SHOE-N-L0-BODY").Material);
        // round trip keeps the new inputs
        var p2 = BridgeProject.FromJson(p.ToJson());
        Assert.Equal(2, p2.Bridges[0].Truss!.LineItems!.Count);
        Assert.Equal(Regen(p).Steel!.Parts.Count, Regen(p2).Steel!.Parts.Count);
    }

    [Fact]
    public void Perforations_EndConnections_StiffenerFills_ExactAndGrips()
    {
        var (p0, b0) = Model();
        var bm0 = Regen(p0);
        var json0 = System.Text.Json.JsonSerializer.Serialize(b0.Truss, new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        foreach (var key in new[] { "Perforation", "EndConnections", "FillThickness", "LegToward" }) Assert.DoesNotContain($"\"{key}\"", json0);
        var (p, b) = Model();
        var t = b.Truss!;
        // 1. perforated bottom cover plate of the box chords: 9 x 15 holes @ 2'-0, 6 in clear of the gussets
        var bot = t.Section("BOX")!.BottomPlates[0];
        bot.Perforation = new TrussPerforation { Width = In(9), Length = In(15), Pitch = In(24), EndClear = In(6), Label = "9 x 15 @ 2'-0" };
        // 2. floor beam FB2: end connection angles L4x4x3/4 on the vertical face (probed), web extended, rivets
        var fb = t.Members.First(m => m.Name == "FB2");
        fb.EndConnections = new() { new TrussEndConnection { Label = "2-Ls 4x4x3/4", Angle = "L4x4x3/4", Top = In(14), Length = In(24), ExtendWeb = true, RivetDiameter = In(0.875),
            WebRivets = new() { new TrussRivetColumn(In(2.5), 8, In(3), In(1.5)) }, OutRivets = new() { new TrussRivetColumn(In(2.5), 8, In(3), In(1.5)) } } };
        // 3. stiffener with fills and rivets on a girder member
        t.Sections.Add(new TrussSectionDefinition { Name = "GIR", Kind = TrussSectionKind.H, WebVertical = true, WebDepth = In(60), WebPlies = { new TrussPly(In(60), In(0.5)) }, Angle = "L6x6x1/2" });
        t.Members.Add(new TrussMemberDefinition { Name = "GIR", From = "L0", To = "L6", Plane = "N", Section = "GIR", Role = TrussMemberRole.Girder, OffsetZ = -Ft(10), Stiffeners = new()
        {
            new TrussStiffenerSet { Angle = "L5x3.5x7/16", Height = In(48), Distances = { Ft(10) }, FillThickness = In(0.5), FillWidth = In(3.5), LegToward = 1,
                Rivets = new() { new TrussRivetColumn(In(2), 6, In(4), In(4)) }, RivetDiameter = In(0.875) },
        } });
        // 4. framed diaphragm plate between stringers A and B with clip angles L4x3-1/2x5/16 (2 + 2 rivets per end)
        double twS = SteelCatalog.FindWideFlange("W21x62")!.WebThickness;
        t.LineItems = new() { new TrussLineItemDefinition { Name = "DIA", Kind = TrussLineItemKind.Steel, Category = PartCategory.Channel, Transverse = true, Station = Ft(100) + 2.5 * Panel,
            From = -Ft(8) + twS / 2, To = -twS / 2, Profile = { new TrussUV(0, -In(10)), new TrussUV(In(0.5), -In(10)), new TrussUV(In(0.5), -In(22)), new TrussUV(0, -In(22)) },
            ClipAngles = new TrussClipAngles { Angle = "L4x3-1/2x5/16", HeelU = 0, Toward = -1, Top = -In(11), Length = In(9), RivetDiameter = In(0.75),
                ItemLegRivets = new TrussRivetColumn(In(2), 2, In(3), In(3)), WebLegRivets = new TrussRivetColumn(In(2.25), 2, In(3), In(3)) } } };
        var bm = Regen(p);
        var st = bm.Steel!; var tm = st.Truss!;
        var clip = st.Parts.Single(x => x.Mark == "DIA-CA");
        var ca = TrussSections.ParseAngle("L4x3-1/2x5/16")!.Value;
        Assert.Equal(2 * (ca.a + ca.b - ca.t) * ca.t * In(9), clip.Volume, 9);
        var cr = st.Bolts.Where(x => x.Kind == BoltKind.Rivet && Math.Abs(x.Diameter - In(0.75)) < 1e-9).ToList();
        Assert.Equal(8, cr.Count);
        Assert.Equal(4, cr.Count(r => Math.Abs(r.Grip - Math.Round((ca.t + In(0.5)) / In(1.0 / 16)) * In(1.0 / 16)) < 1e-9));
        Assert.Equal(4, cr.Count(r => Math.Abs(r.Grip - Math.Round((ca.t + twS) / In(1.0 / 16)) * In(1.0 / 16)) < 1e-9));
        // perforated chord: volume = solid - holes x hole area x t (hole arcs 8 segments: inscribed polygon), every piece an exact extrusion
        var chord0 = bm0.Steel!.Parts.First(x => x.Mark == "N-L2-L3");
        var chord = st.Parts.First(x => x.Mark == "N-L2-L3");
        int holes = Assert.Single(tm.Perforations.Where(x => x.Member == "N-L2-L3")).Holes;
        Assert.True(holes >= 1);
        double inscribed = In(9) * (In(15) - In(9)) + 0.5 * 16 * In(4.5) * In(4.5) * Math.Sin(2 * Math.PI / 16);
        Assert.Equal(chord0.Volume - holes * inscribed * In(0.5), chord.Volume, 9);
        Assert.True(chord.Mesh.HasExactSolids);
        // end connections at both FB2 ends: angles on both web faces, web extension, rivets
        var ang = st.Parts.Where(x => x.Category == PartCategory.TrussConnectionAngle && x.Mark.StartsWith("FB2-EC")).ToList();
        Assert.Equal(2, ang.Count);
        var a = TrussSections.ParseAngle("L4x4x3/4")!.Value;
        foreach (var x in ang) { Assert.Equal(2 * (a.a + a.b - a.t) * a.t * In(24), x.Volume, 9); Assert.True(x.Mesh.HasExactSolids); }
        // backs probed on the first steel face beyond the cut (S end: the vertical's channel flanges 8 in from the plane; N end: the cut lies inside the
        // 3/4 in gusset at 10 in -> its outer face toward the member)
        Assert.Equal(new[] { "FB2 L2 end: 10 3/4\"", "FB2 L2 end: 8\"" }, tm.ConnectionBacks);
        // web extended only where the back lies inside the member setback (10 in)
        var ext = Assert.Single(st.Parts, x => x.Mark.StartsWith("FB2-EC1") && x.Mark.EndsWith("W"));
        Assert.Equal(w36Web() * (In(10) - In(8) - In(0.5)) * In(24), ext.Volume, 6);
        static double w36Web() => SteelCatalog.FindWideFlange("W36x150")!.WebThickness;
        var w36 = SteelCatalog.FindWideFlange("W36x150")!;
        var riv = st.Bolts.Where(x => x.Kind == BoltKind.Rivet && x.Assembly == "FLOOR").ToList();
        Assert.Equal(2 * (8 + 2 * 8), riv.Count);
        // web rivets: grip = 2 legs + web; outstanding leg rivets: the leg + the steel behind it (vertical)
        Assert.Equal(16, riv.Count(r => Math.Abs(r.Grip - (2 * a.t + w36.WebThickness)) < In(1.0 / 16)));
        Assert.True(riv.Count(r => r.Grip > a.t + In(1.0 / 8)) >= 16);
        // stiffener fills: angle stands out by the fill; rivets through angle + fill + web + fill + angle
        var fill = st.Parts.Single(x => x.Mark == "N-GIR-ST1F");
        Assert.Equal(PartCategory.TrussConnectionPlate, fill.Category);
        Assert.Equal(2 * In(3.5) * In(0.5) * In(48), fill.Volume, 9);
        var sr = st.Bolts.Where(x => x.Kind == BoltKind.Rivet && x.Assembly.StartsWith("GIRDER")).ToList();
        Assert.Equal(6, sr.Count);
        var s5 = TrussSections.ParseAngle("L5x3.5x7/16")!.Value;
        Assert.All(sr, r => Assert.Equal(2 * (s5.t + In(0.5)) + In(0.5), r.Grip, 3));
        // round trip
        var p2 = BridgeProject.FromJson(p.ToJson());
        Assert.NotNull(p2.Bridges[0].Truss!.Section("BOX")!.BottomPlates[0].Perforation);
        Assert.Single(p2.Bridges[0].Truss!.Members.First(m => m.Name == "FB2").EndConnections!);
        Assert.Equal(st.Parts.Count, Regen(p2).Steel!.Parts.Count);
    }

    [Fact]
    public void ShoePieces_ReplaceTheEnvelope_ExactVolumes()
    {
        var (p, b) = Model();
        var t = b.Truss!;
        var json0 = System.Text.Json.JsonSerializer.Serialize(t, new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        Assert.DoesNotContain("\"Pieces\"", json0);
        var sh = t.Shoes[0];
        sh.Pieces = new()
        {
            // 12 x 10 plate under the pin swept 2 ft across; two wing plates 1 in thick mirrored about n = 0 (profile in (u, z))
            new TrussShoePiece { Name = "stem", Kind = TrussShoePieceKind.Body, Axis = TrussShoePieceAxis.Across, From = -Ft(1), To = Ft(1),
                Profile = { new TrussUV(-In(6), -In(30)), new TrussUV(In(6), -In(30)), new TrussUV(In(6), -In(20)), new TrussUV(-In(6), -In(20)) } },
            new TrussShoePiece { Name = "wing", Kind = TrussShoePieceKind.Plate, Axis = TrussShoePieceAxis.Across, From = Ft(1), To = Ft(1) + In(1), MirrorN = true,
                Profile = { new TrussUV(-In(12), -In(32)), new TrussUV(In(12), -In(32)), new TrussUV(0, -In(20)) }, Unresolved = "UNRESOLVED (p.0): wing - test." },
            // three segmental rollers (circle 12 in clipped to +-4 in) at 10 in pitch, 18 in long
            new TrussShoePiece { Name = "roller", Kind = TrussShoePieceKind.Roller, CircleDiameter = In(12), CircleY = -In(38), ClipHalfWidth = In(4), From = -In(9), To = In(9),
                ShiftsProfileX = { -In(10), 0, In(10) }, Material = "Forged steel" },
            // rib in the (n, z) plane swept along u
            new TrussShoePiece { Name = "rib", Kind = TrussShoePieceKind.Plate, Axis = TrussShoePieceAxis.Along, From = -In(1), To = In(1),
                Profile = { new TrussUV(Ft(1), -In(32)), new TrussUV(Ft(1) + In(6), -In(32)), new TrussUV(Ft(1), -In(26)) } },
        };
        var bm = Regen(p);
        var parts = bm.Steel!.Parts.Where(x => x.Assembly == "SHOE-N-L0").ToList();
        Assert.DoesNotContain(parts, x => x.Mark.EndsWith("-BODY"));       // envelope replaced
        Assert.Contains(parts, x => x.Mark.EndsWith("-MP"));                // masonry plate + pin still from the fields
        Assert.Contains(parts, x => x.Mark.EndsWith("-PIN"));
        var stem = parts.Single(x => x.Description.StartsWith("stem"));
        Assert.Equal(PartCategory.TrussShoeExpansion, stem.Category);
        Assert.Equal(In(12) * In(10) * Ft(2), stem.Volume, 9);
        var wing = parts.Single(x => x.Description.StartsWith("wing"));
        Assert.Equal(2 * In(24) * In(12) / 2 * In(1), wing.Volume, 9);
        Assert.NotNull(wing.Assumption);
        var roller = parts.Single(x => x.Description.StartsWith("roller"));
        Assert.Equal(PartCategory.TrussPin, roller.Category);
        double h = Math.Sqrt(36 - 16), seg = 2 * (36 * Math.Asin(4 / 6.0)) + 2 * 4 * h;       // circle r 6 clipped to |x| <= 4 (32-gon inscribed: within 1 %)
        Assert.Equal(3 * seg * 18 * Math.Pow(In(1), 3), roller.Volume, 0.01 * roller.Volume);
        var rib = parts.Single(x => x.Description.StartsWith("rib"));
        Assert.Equal(In(6) * In(6) / 2 * In(2), rib.Volume, 9);
        // the rib lies across the truss: 1 ft to 1'-6 from the pin across (horizontal distance normal to the alignment = y here)
        var pin = parts.Single(x => x.Mark.EndsWith("-PIN")).Mesh.Bounds();
        double yc = (pin.Min.Y + pin.Max.Y) / 2;
        var rb = rib.Mesh.Bounds();
        Assert.Equal(In(6), rb.Max.Y - rb.Min.Y, 6);
        Assert.Equal(Ft(1) + In(3), Math.Abs((rb.Min.Y + rb.Max.Y) / 2 - yc), 6);
        // round trip keeps the pieces
        var p2 = BridgeProject.FromJson(p.ToJson());
        Assert.Equal(4, p2.Bridges[0].Truss!.Shoes[0].Pieces!.Count);
        Assert.Equal(Regen(p).Steel!.Parts.Count, Regen(p2).Steel!.Parts.Count);
    }

    [Fact]
    public void LineItemRepeat_BuildsCombTeeth()
    {
        var (p, b) = Model();
        var t = b.Truss!;
        var tooth = new TrussLineItemDefinition { Name = "teeth", Kind = TrussLineItemKind.Steel, Category = PartCategory.JointArmor, Transverse = true, Station = Ft(250), From = -Ft(10), To = Ft(10),
            Profile = { new TrussUV(0, -In(3)), new TrussUV(In(12), -In(3)), new TrussUV(In(12), 0), new TrussUV(0, 0) } };
        t.LineItems = new() { tooth };
        var json0 = System.Text.Json.JsonSerializer.Serialize(tooth);
        Assert.DoesNotContain("Repeat", json0);
        double one = Regen(p).Steel!.Parts.Single(x => x.Mark == "teeth").Volume;
        Assert.Equal(In(12) * In(3) * Ft(20), one, 6);
        // 2 in. teeth every 4 1/4 in. starting 1/8 in. after From: floor((240 - 0.125 - 2) / 4.25) + 1 = 56 teeth
        tooth.RepeatPitch = In(4.25); tooth.RepeatWidth = In(2); tooth.RepeatStart = In(0.125);
        var part = Regen(p).Steel!.Parts.Single(x => x.Mark == "teeth");
        Assert.Equal(56 * In(2) * In(12) * In(3), part.Volume, 9);
        Assert.Equal(56 * In(2), part.Length, 9);
        Assert.Contains("RepeatPitch", System.Text.Json.JsonSerializer.Serialize(tooth));
        Assert.DoesNotContain("CopiesV", System.Text.Json.JsonSerializer.Serialize(tooth));
        // CopiesV: the same teeth at three levels (ladder rungs, rails): three times the volume, top raised by the largest shift
        tooth.CopiesV = new() { 0, In(12), In(24) };
        var part3 = Regen(p).Steel!.Parts.Single(x => x.Mark == "teeth");
        Assert.Equal(3 * part.Volume, part3.Volume, 9);
        Assert.Equal(part.Mesh.Bounds().Max.Z + In(24), part3.Mesh.Bounds().Max.Z, 6);
    }

    [Fact]
    public void LineItem_TallThinPiece_MassFollowsVolume()
    {
        // ladder stile style: a tall section swept across a SHORT range. A nominal lb/ft would multiply the sweep length (3 in.), not the
        // height, so the stile mass was ~1/70 of its solid (PTC walkways); without it the mass is volume x density.
        var (p, b) = Model();
        var stile = new TrussLineItemDefinition { Name = "stile", Kind = TrussLineItemKind.Steel, Category = PartCategory.WalkwayPost, Transverse = true, Station = Ft(250), FromGrade = false,
            From = -In(1.5), To = In(1.5), Profile = { new TrussUV(0, 0), new TrussUV(In(0.375), 0), new TrussUV(In(0.375), Ft(20)), new TrussUV(0, Ft(20)) } };
        b.Truss!.LineItems = new() { stile };
        var part = Regen(p).Steel!.Parts.Single(x => x.Mark == "stile");
        Assert.Equal(In(0.375) * In(3) * Ft(20), part.Volume, 9);
        Assert.InRange(part.Mass / (part.Volume * 7850), 0.99, 1.01);
    }

    [Fact]
    public void BracingCornerClearance_BracingGussets_ClearEndsPlatesAndRivets()
    {
        // K of two laterals from the chord points N:L2 / S:L2 to the CL point of strut 3 (the strut passes the apex)
        (BridgeModel bm, BridgeDefinition b) Build(bool on)
        {
            var (p, b) = Model();
            var t = b.Truss!;
            t.Points.Add(new TrussPointDefinition("LM3", Ft(100) + 3 * Panel, Ft(60)) { Offset = Half, Planes = new() { "N" } });
            t.Members.First(m => m.Name == "STRUT3").Section = "PL";
            t.Members.Add(new TrussMemberDefinition { Name = "KA", From = "N:L2", To = "N:LM3", Section = "PL", Role = TrussMemberRole.LowerLateral });
            t.Members.Add(new TrussMemberDefinition { Name = "KB", From = "S:L2", To = "N:LM3", Section = "PL", Role = TrussMemberRole.LowerLateral });
            if (on)
            {
                t.JointDefaults.BracingCornerClearance = In(0.5);
                t.JointDefaults.BracingStackClearance = In(4);
                t.BracingGussets = new()
                {
                    new TrussBracingGussetDefinition { Point = "N:LM3", Thickness = In(0.5), Side = TrussBracingPlateSide.Below, RivetsPerMember = 6, RivetPitch = In(3), RivetGage = In(4), RivetDiameter = In(0.875) },
                    new TrussBracingGussetDefinition { Point = "N:L2", Thickness = In(0.5), Side = TrussBracingPlateSide.Below, EdgeAngle = "L4x3-1/2x7/16", EdgeRivets = 4, RivetsPerMember = 6,
                        RivetPitch = In(3), RivetGage = In(4), RivetDiameter = In(0.875), Unresolved = "UNRESOLVED (p.0): test plate - scaled." },
                };
            }
            return (Regen(p), b);
        }
        var (off, _) = Build(false);
        var (on, bOn) = Build(true);
        var o = new SBM.Core.Checks.ClashOptions();
        bool Pair(BridgeModel m, string a, string c) => SBM.Core.Checks.SteelClashCheck.Run(m, o).Clashes.Any(x => (x.A.Mark == a && x.B.Mark == c) || (x.A.Mark == c && x.B.Mark == a));
        // off: the K diagonals overlap each other and the strut at the apex (work point to work point)
        Assert.True(Pair(off, "KA", "KB") || Pair(off, "KA", "STRUT3"));
        // on: every corner clear - no overlap among the bracing, nor with the chord joint
        foreach (var (a, c) in new[] { ("KA", "KB"), ("KA", "STRUT3"), ("KB", "STRUT3") }) Assert.False(Pair(on, a, c), $"{a} x {c}");
        Assert.DoesNotContain(SBM.Core.Checks.SteelClashCheck.Run(on, o).Clashes, x => (x.A.Mark == "KA" || x.B.Mark == "KA") && (x.A.Kind == "TrussChord" || x.B.Kind == "TrussChord" || x.A.Kind == "TrussGusset" || x.B.Kind == "TrussGusset"));
        var ka0 = off.Steel!.Truss!.Member("KA")!; var ka1 = on.Steel!.Truss!.Member("KA")!;
        Assert.True(ka1.CutLength < ka0.CutLength - In(4));
        // plates: exact extrusions under the members, plate at L2 clear of the joint stack with its edge angle; rivets through plate + member
        var steel = on.Steel!;
        var apex = steel.Parts.Single(x => x.Mark == "N-BGLM3");
        var l2 = steel.Parts.Single(x => x.Mark == "N-BGL2");
        var ea = steel.Parts.Single(x => x.Mark == "N-BGL2-EA");
        Assert.Equal(PartCategory.TrussConnectionPlate, apex.Category);
        Assert.Equal(PartCategory.TrussConnectionAngle, ea.Category);
        Assert.True(apex.Mesh.HasExactSolids && l2.Mesh.HasExactSolids && ea.Mesh.HasExactSolids);
        Assert.NotNull(l2.Assumption);
        double topPl = apex.Mesh.Bounds().Max.Z, botK = steel.Parts.Single(x => x.Mark == "KA").Mesh.Bounds().Min.Z;
        Assert.InRange(topPl - botK, -1e-4, 1e-4);                                   // plate top on the member bottom (level members)
        Assert.InRange(apex.Mesh.Bounds().Max.Z - apex.Mesh.Bounds().Min.Z, In(0.5) - 1e-6, In(0.5) + 1e-6);
        double yN = Half - In(10) - In(0.75);                                         // stack face of the N plane (left = +y): box webs 20 in. out to out + 3/4 gussets
        Assert.True(l2.Mesh.Bounds().Max.Y <= yN - In(0.25) + 1e-4);
        Assert.True(ea.Mesh.Bounds().Max.Y <= yN + 1e-4);
        var bracingRivets = steel.Bolts.Where(x => x.Kind == BoltKind.Rivet && x.Diameter == In(0.875) && x.Assembly.StartsWith("BR-")).ToList();
        Assert.True(bracingRivets.Count >= 3 * 6, $"{bracingRivets.Count} rivets");
        Assert.All(bracingRivets, r => Assert.True(r.Grip >= In(7.0 / 16) - 1e-6));
        // the .sbm stays unchanged without the inputs; round trip with them
        var json = System.Text.Json.JsonSerializer.Serialize(new TrussDefinition());
        Assert.DoesNotContain("BracingGussets", json);
        Assert.DoesNotContain("BracingCornerClearance", json);
        var p2 = BridgeProject.FromJson(new BridgeProject { Alignments = { new Alignment { Name = "A" } }, Bridges = { bOn } }.ToJson());
        Assert.Equal(2, p2.Bridges[0].Truss!.BracingGussets!.Count);
        Assert.Equal(In(4), p2.Bridges[0].Truss!.JointDefaults.BracingStackClearance, 9);
    }

    [Fact]
    public void EndConnection_OutFill_FillsTheStandOffBelowTheGusset()
    {
        // FB2's N end: the angle backs lie on the L2 gusset (10 3/4 in. from the plane); above the gusset the outstanding legs stand off the vertical's
        // channel flanges (8 in.): OutFillWidth fills that run, as thick as the probed gap, and the leg rivets then grip leg + fill + flange
        (BridgeModel bm, int loose) Build(double w)
        {
            var (p, b) = Model();
            var fb = b.Truss!.Members.First(m => m.Name == "FB2");
            fb.EndConnections = new() { new TrussEndConnection { Label = "2-Ls 4x4x3/4", Angle = "L4x4x3/4", Top = In(14), Length = In(24), RivetDiameter = In(0.875), OutFillWidth = w,
                OutFillPlanThickness = In(2.75), OutRivets = new() { new TrussRivetColumn(In(2.5), 8, In(3), In(1.5)) } } };
            var m = Regen(p);
            var d = m.Steel!.Diagnostics.FirstOrDefault(x => x.Message.StartsWith("Truss CHECK end connections"));
            int n = d == null ? 0 : int.Parse(System.Text.RegularExpressions.Regex.Match(d.Message, @"connections: (\d+)").Groups[1].Value);
            return (m, n);
        }
        var (m0, loose0) = Build(0);
        var (m1, loose1) = Build(In(4));
        Assert.DoesNotContain(m0.Steel!.Parts, x => x.Mark.EndsWith("OF"));
        var fills = m1.Steel!.Parts.Where(x => x.Mark.StartsWith("FB2-EC1") && x.Mark.EndsWith("OF")).ToList();
        Assert.NotEmpty(fills);
        Assert.All(fills, f => { Assert.Equal(PartCategory.TrussConnectionPlate, f.Category); Assert.True(f.Mesh.HasExactSolids); });
        Assert.True(loose1 < loose0, $"{loose1} vs {loose0}");
        Assert.Contains(m1.Steel.Diagnostics, x => x.Message.Contains("fills under the outstanding legs where") && x.Message.Contains("plan 2 3/4"));
        // no fill overlaps the gusset or the vertical
        var o = new SBM.Core.Checks.ClashOptions();
        Assert.DoesNotContain(SBM.Core.Checks.SteelClashCheck.Run(m1, o).Clashes, c => c.A.Mark.EndsWith("OF") || c.B.Mark.EndsWith("OF"));
        Assert.DoesNotContain("OutFill", System.Text.Json.JsonSerializer.Serialize(new TrussEndConnection()));
    }

    [Fact]
    public void FileRoundTrip_AndExistingModelsUnchanged()
    {
        var (p, b) = Model();
        var p2 = BridgeProject.FromJson(p.ToJson());
        Assert.NotNull(p2.Bridges[0].Truss);
        Assert.Equal(b.Truss!.Members.Count, p2.Bridges[0].Truss!.Members.Count);
        Assert.True(double.IsNaN(p2.Bridges[0].Truss!.Shoes[0].SeatElevation));
        Assert.Equal(Regen(p).Steel!.Parts.Count, Regen(p2).Steel!.Parts.Count);
        var sample = SampleProject.Create();
        Assert.DoesNotContain("\"Truss\"", sample.ToJson());
        var bad = b.Clone();
        bad.Girders.Count = 3;
        Assert.Contains(bad.Validate(), e => e.Contains("truss bridge has no girders"));
        // enum values appended (existing numbers unchanged)
        Assert.Equal(25, (int)PartCategory.WalkwayPlate);
        Assert.Equal((int)PartCategory.RailFabric + 1, (int)PartCategory.TrussChord);
        Assert.Equal((int)AssemblyKind.FlangeDeflector + 1, (int)AssemblyKind.TrussLine);
        Assert.Equal(1, (int)BoltKind.Anchor);
    }
}
