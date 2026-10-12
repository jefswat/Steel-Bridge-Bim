using SBM.Core.Alignments;
using SBM.Core.Arch;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Tied arch generator (Arch/ArchGenerator): local frame, rib circle and inclination, ties, hangers, floorbeams, bracing, knuckles,
/// deck, bearings, IFC types, file round trip, existing models unchanged. Self-contained model (straight alignment), NHT-like dimensions.</summary>
public class ArchTests
{
    private const double Tol = 0.0254 / 16;

    private static (BridgeProject p, BridgeDefinition b) Model(double tiltDeg = 1.039)
    {
        var al = new Alignment { Name = "A", StartStation = 0 };
        al.SetElements(new[] { new HorizontalElement(new Vec2(0, 0), 0, Ft(600), 0, 0) });
        al.Profile = new VerticalProfile { Vpis = { new Vpi(0, Ft(500)), new Vpi(Ft(600), Ft(500) - Ft(600) * 0.016) } };
        al.CrossSlope = new CrossSlopeTable { Rows = { new CrossSlopeRow { Station = 0, LeftSlope = -0.01, RightSlope = -0.01 } } };
        var a = new ArchDefinition { Source = "test" };
        a.Frame = new ArchFrameDefinition
        {
            Wp1Station = Ft(200), Wp1Offset = 0, Wp2Station = Ft(200) + Ft(201) * Math.Cos(Numerics.DegToRad(tiltDeg)), Wp2Offset = 0,
            OriginElevation = Ft(500) - Ft(300.5) * 0.016 - In(13), TiltDegrees = tiltDeg, TiltAxisSupport = "P1",
        };
        void Pt(string n, double x, double y, double z = 0) => a.Points.Add(new ArchPoint(n, Ft(x), Ft(y), Ft(z)));
        Pt("T0", -100.5, -9.1667); Pt("T34", 100.5, -9.1667); Pt("T0'", 100.5, 9.1667); Pt("T34'", -100.5, 9.1667);
        // Splice on the south rib circle (centre x 0, 76 ft below the chord in the 5 deg plane) at 30 deg from the top.
        double r = 126, c = Math.Sqrt(126 * 126 - 100.5 * 100.5), th = Numerics.DegToRad(60);
        double h = r * Math.Sin(th) - c, x = r * Math.Cos(th);
        Pt("RS1", -x, -9.1667 + h * Math.Sin(Numerics.DegToRad(5)), h * Math.Cos(Numerics.DegToRad(5)));
        a.Ribs.Add(new ArchRibDefinition { Name = "South rib", ChordStart = "T0", ChordEnd = "T34", Radius = Ft(126), Rise = Ft(50), InclinationDegrees = 5, Splices = { "RS1" } });
        a.Ribs.Add(new ArchRibDefinition { Name = "North rib", ChordStart = "T0'", ChordEnd = "T34'", Radius = Ft(126), Rise = Ft(50), InclinationDegrees = -5 });
        ArchTieDefinition Tie(string n, string s, string e, string prime)
        {
            var t = new ArchTieDefinition { Name = n, Start = s, End = e, Elements = { new ArchTieElement { Kind = ArchTieElementKind.Line, Length = Ft(201) } } };
            for (int k = 1; k <= 5; k++) t.PanelPoints.Add(new ArchPanelPoint($"T{k}{prime}", Ft(201) * k / 6));
            return t;
        }
        a.Ties.Add(Tie("South tie", "T0", "T34", ""));
        a.Ties.Add(Tie("North tie", "T0'", "T34'", "'"));
        for (int n = 1; n <= 5; n++)
            a.Floorbeams.Add(new ArchFloorbeamDefinition { Name = $"FB{n}", TieA = "South tie", PanelA = $"T{n}", TieB = "North tie", PanelB = $"T{6 - n}'" });
        a.Hangers.Add(new ArchHangerDefinition { Name = "C1", Rib = "South rib", Tie = "South tie", TieAnchor = new ArchPoint("TC", Ft(-50), Ft(-9.1667), Ft(1.333)), RibAnchor = new ArchPoint("HC", Ft(-30), Ft(-5), Ft(40)), PlanLength = double.NaN });
        a.HangerSystem = new ArchHangerSystem
        {
            StrandWeightLbPerFt = 2.10,
            RibSocket = new ArchSocketDefinition { Name = "open", A = In(4.06), B = In(2.76), G = In(3.43), L = In(7.87), W = In(16.18), PinDiameter = In(1.378), ShankDiameter = In(1.299) },
            TieSocket = new ArchSocketDefinition { Name = "adj", A = In(4.06), B = In(2.76), G = In(3.43), L = In(7.87), W = In(34.41), PinDiameter = In(1.378), ShankDiameter = In(1.299) },
        };
        Pt("U1", -60, -6, 35); Pt("U1'", 60, 6, 35); Pt("UL1", 0, 0, 45);
        a.Bracing.Add(new ArchBraceDefinition { Name = "BR1", From = "U1", To = "UL1" });
        a.Bracing.Add(new ArchBraceDefinition { Name = "BR2", From = "UL1", To = "U1'" });
        a.Knuckles.Add(new ArchKnuckleDefinition { Name = "West", EndFrom = "T34'", EndTo = "T0", DeckPlate = { "T34'", "T5'", "T1", "T0" }, EndWebHeightAtEnds = In(10) });
        a.Knuckles.Add(new ArchKnuckleDefinition { Name = "East", EndFrom = "T34", EndTo = "T0'", DeckPlate = { "T34", "T5", "T1'", "T0'" }, EndWebHeightAtEnds = In(10) });
        a.Deck = new ArchDeckDefinition { RightTie = "South tie", LeftTie = "North tie", EndKnuckles = { "West", "East" },
            Curbs = { new ArchCurbDefinition { Name = "LT", InnerOffset = -Ft(7) }, new ArchCurbDefinition { Name = "RT", InnerOffset = Ft(7) } } };
        Pt("BRG-SW", -100.5 + 2 / 12.0, -9.1667);
        a.Bearings.Add(new ArchBearingDefinition { Name = "SW", Support = "P1", Point = "BRG-SW", SeatElevation = Ft(500), SteelUndersideZ = -In(2.125) });
        var b = new BridgeDefinition
        {
            Name = "Arch", AlignmentName = "A",
            Supports =
            {
                new SupportDefinition { Name = "P1", Type = SupportType.Pier, Station = Ft(200), Substructure = new SubstructureDefinition { Enabled = false } },
                new SupportDefinition { Name = "P2", Type = SupportType.Pier, Station = Ft(401), Substructure = new SubstructureDefinition { Enabled = false } },
            },
            Arch = a,
        };
        b.Girders.Count = 0;
        var p = new BridgeProject { Alignments = { al }, Bridges = { b } };
        return (p, b);
    }

    private static (BridgeFraming f, ArchModel m) Generate(BridgeProject p)
    {
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        Assert.False(f.HasErrors, string.Join("; ", f.Diagnostics.Select(d => d.Message)));
        return (f, ArchGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void Frame_IsTiltedAboutTheSupportLine()
    {
        var (p, _) = Model();
        var (f, m) = Generate(p);
        var z = m.Frame.Z;
        Assert.Equal(1.039, Math.Acos(z.Z) * 180 / Math.PI, 6);
        // The tilt axis is the support line: its direction is horizontal in the local plane.
        var axis = f.Supports[0].Direction.WithZ(0);
        Assert.True(Math.Abs(Vec3.Dot(axis, z)) < 1e-12);
        Assert.Equal(1, m.Frame.X.Length, 9);
        Assert.True(Math.Abs(Vec3.Dot(m.Frame.X, m.Frame.Y)) < 1e-12);
        Assert.True(m.Frame.Y.Y > 0);  // local Y = left of X
    }

    [Fact]
    public void Ribs_AreCircularInclinedArcsThroughTheChordEnds()
    {
        var (p, _) = Model();
        var (_, m) = Generate(p);
        Assert.Equal(2, m.Ribs.Count);
        foreach (var r in m.Ribs)
        {
            Assert.True(Math.Abs(Ft(50) - r.Rise) < Tol);
            Assert.Equal(5, Math.Acos(Math.Abs(r.Up.Z)) * 180 / Math.PI, 9);
            for (int i = 0; i <= 20; i++)
                Assert.Equal(r.Radius, (r.PointAt(r.ThetaStart + (r.ThetaEnd - r.ThetaStart) * i / 20.0) - r.Center).Length, 9);
            Assert.True(Math.Abs(r.PointAt(r.ThetaStart).Z) < 1e-9 && Math.Abs(r.PointAt(r.ThetaEnd).Z) < 1e-9);
            // Inward: the crown leans toward the CL (local Y = 0).
            var crown = r.PointAt((r.ThetaStart + r.ThetaEnd) / 2);
            Assert.True(Math.Abs(crown.Y) < Math.Abs(r.PointAt(r.ThetaStart).Y));
        }
        var south = m.Steel.Parts.Where(x => x.Assembly == "SOUTH-RIB").ToList();
        Assert.Equal(2, south.Count(x => x.Category == PartCategory.ArchRib));        // one splice -> 2 pieces
        Assert.All(south, x => Assert.Equal(132, x.NominalWeightLbPerFt));
        Assert.Contains(m.Checks, c => c.Item.Contains("South rib: rise") && c.Ok);
    }

    [Fact]
    public void Hangers_TiesFloorbeams_AreBuiltFromThePlanPoints()
    {
        var (p, _) = Model();
        var (_, m) = Generate(p);
        var h = Assert.Single(m.Hangers);
        double expected = Math.Sqrt(Math.Pow(Ft(20), 2) + Math.Pow(Ft(4.1667), 2) + Math.Pow(Ft(40 - 1.333), 2));
        Assert.Equal(expected, h.Length, 9);
        Assert.Equal(expected, (h.RibAnchorWorld - h.TieAnchorWorld).Length, 9);
        Assert.Equal(1, m.Steel.Parts.Count(x => x.Category == PartCategory.Hanger));
        Assert.Equal(2, m.Steel.Parts.Count(x => x.Category == PartCategory.HangerSocket));
        Assert.All(m.Ties, t => Assert.True(t.ClosureMiss < Tol));
        Assert.Equal(5, m.Floorbeams.Count);
        Assert.All(m.Floorbeams, fb => Assert.Equal(Ft(18.3334), fb.Length, 4));
        Assert.Equal(10, m.Steel.Parts.Count(x => x.Category == PartCategory.FloorbeamPlate));
        Assert.Equal(4, m.Steel.Parts.Count(x => x.Category == PartCategory.TiePlate));
        Assert.Equal(2, m.Steel.Parts.Count(x => x.Category == PartCategory.ArchBracing));
        Assert.Equal(6, m.Steel.Parts.Count(x => x.Category == PartCategory.KnucklePlate));
        Assert.NotNull(m.Concrete.Deck);
        Assert.False(m.Concrete.Deck!.IsEmpty);
        Assert.True(Math.Abs(m.Concrete.Deck.Volume()) > 0);
        Assert.NotNull(m.Curbs);
        Assert.Single(m.Steel.Parts, x => x.Category == PartCategory.FormDeck);
        // Bearing stack 1/8 + 2 + 3 + 3 5/8 + 3 = 11 3/4" on the seat; seat with location for the pier.
        var br = Assert.Single(m.Bearings);
        Assert.Equal(br.SeatElevation + In(11.75), br.TopOfStack, 9);
        var seat = Assert.Single(m.Steel.BearingSeats);
        Assert.NotNull(seat.Location);
        Assert.Equal(4, m.Steel.Bolts.Count(b => b.Kind == BoltKind.Anchor));
    }

    [Fact]
    public void Cache_UsesTheArchGenerator_AndTheFileRoundTrips()
    {
        var (p, b) = Model();
        var cache = new RegenerationCache();
        var bm = Assert.Single(cache.Regenerate(p));
        Assert.NotNull(bm!.Steel?.Arch);
        Assert.Null(bm.Substructure);
        int parts = bm.Steel!.Parts.Count;
        var p2 = BridgeProject.FromJson(p.ToJson());
        Assert.NotNull(p2.Bridges[0].Arch);
        Assert.Equal(b.Arch!.Hangers.Count, p2.Bridges[0].Arch!.Hangers.Count);
        var bm2 = Assert.Single(new RegenerationCache().Regenerate(p2));
        Assert.Equal(parts, bm2!.Steel!.Parts.Count);
    }

    [Fact]
    public void Ifc_WritesArchEntities()
    {
        var (p, _) = Model();
        var bm = Assert.Single(new RegenerationCache().Regenerate(p))!;
        var sw = new StringWriter();
        IfcExporter.Export(sw, "arch.ifc", p, new[] { new IfcBridgeData(bm.Framing, bm.Steel, bm.Concrete) });
        string ifc = sw.ToString();
        foreach (var e in new[] { ".ARCH_SEGMENT.", ".SUSPENDER.", "'CABLE_SOCKET'", ".SHEET.", "'KNUCKLE_PLATE'", ".ARCH.", ".SUSPENSIONASSEMBLY.", "IFCSLAB(", ".ANCHORBOLT.", ".BRACE." })
            Assert.Contains(e, ifc);
    }

    [Fact]
    public void ExistingModels_AreUnchanged()
    {
        var p = SampleProject.Create();
        Assert.DoesNotContain("\"Arch\"", p.ToJson());
        Assert.Null(p.Bridges[0].Arch);
        // A bridge without girders and without an arch is still an error.
        var b = p.Bridges[0].Clone();
        b.Girders.Count = 0;
        Assert.Contains(b.Validate(), e => e.Contains("At least one girder"));
        // Enum values are appended (existing values keep their numbers).
        Assert.Equal(25, (int)PartCategory.WalkwayPlate);
        Assert.Equal(6, (int)AssemblyKind.InspectionWalkway);
    }
}
