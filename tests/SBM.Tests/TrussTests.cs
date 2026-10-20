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
