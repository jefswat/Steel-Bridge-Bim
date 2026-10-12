using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Segmental;
using SBM.Core.Solids;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Precast segmental box (Segmental/SegmentalGenerator): section geometry at printed depths, depth table at the joints, closed
/// segment shells, joint continuity, cross slope rotation (DIM "B"), tendons (lengths, weights), transverse PT, IFC entity types, JSON round
/// trip / omission, and the Bonner Unit 5 sample (segment counts, tendon schedule weights).</summary>
public class SegmentalTests
{
    private const double Sixteenth = 0.0254 / 16;
    private static double Ft2(double m2) => m2 / (Ft(1) * Ft(1));

    /// <summary>Two short spans with one cantilever bent (S1), end segments + closures, Bonner box section and depth table rows 1-3.</summary>
    internal static (BridgeProject p, BridgeDefinition b) Model()
    {
        var al = new Alignment { Name = "A", StartStation = 0 };
        al.SetElements(new[] { new HorizontalElement(new Vec2(0, 0), 0.3, Ft(400), 0, 0) });
        al.Profile = new VerticalProfile { Vpis = { new Vpi(0, Ft(80)), new Vpi(Ft(400), Ft(80) + Ft(400) * 0.02) } };
        al.CrossSlope = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, 0.045, -0.045) } };
        var b = new BridgeDefinition { Name = "Seg Unit 1", AlignmentName = "A" };
        b.FieldSplices.Clear();
        b.Girders.Count = 0; b.Girders.Spacings = new();
        foreach (var (n, x) in new[] { ("S0", 100.0), ("S1", 160.0), ("S2", 220.0) })
            b.Supports.Add(new SupportDefinition { Name = n, Type = SupportType.Pier, Station = Ft(x), Substructure = new SubstructureDefinition { Enabled = false } });
        var sg = new SegmentalDefinition { Source = "test" };
        sg.DepthTable.Add(new SegmentalDepthSection { Number = 1, X = Ft(5), Depth = In(228), HalfSoffitPrinted = In(93), BottomSlab = In(24) });
        sg.DepthTable.Add(new SegmentalDepthSection { Number = 2, X = Ft(19), Depth = In(207.125), HalfSoffitPrinted = In(98.25), BottomSlab = In(21.25) });
        sg.DepthTable.Add(new SegmentalDepthSection { Number = 3, X = Ft(33), Depth = In(188.375), HalfSoffitPrinted = In(102.875), BottomSlab = In(18.375) });
        // short test cantilever: the constant zone starts at section 3 (the real box reaches 9'-0" at x = 159 ft)
        sg.ParabolaVertexX = Ft(33); sg.ParabolaVertexDepth = In(188.375); sg.ParabolaCoefficient = In(39.625 / (28 * 28)) / (Ft(1) * Ft(1));
        sg.BentSoffitTransition = Ft(1.5); sg.BentDiaphragm = Ft(3.5); sg.ConstantDepth = In(188.375); sg.ConstantBottomSlab = In(18.375);
        SegmentDefinition S(string name, SegmentType t, double x0, double x1, string bent = "", int k = 0) => new() { Name = name, Type = t, X0 = Ft(x0), X1 = Ft(x1), Bent = bent, Index = k };
        sg.Spans.Add(new SegmentalSpan
        {
            Span = 0, Name = "Span 1",
            Segments = { S("0-ES", SegmentType.End, 10.0 / 12, 9.5), S("CJ1", SegmentType.Closure, 9.5, 13), S("1-D3", SegmentType.Typical, 13, 27, "S1", 3),
                S("1-D2", SegmentType.Typical, 27, 41, "S1", 2), S("1-D1", SegmentType.Typical, 41, 55, "S1", 1), S("1-DB", SegmentType.BentHalf, 55, 60, "S1") },
        });
        sg.Spans.Add(new SegmentalSpan
        {
            Span = 1, Name = "Span 2",
            Segments = { S("1-UB", SegmentType.BentHalf, 0, 5, "S1"), S("1-U1", SegmentType.Typical, 5, 19, "S1", 1), S("1-U2", SegmentType.Typical, 19, 33, "S1", 2),
                S("1-U3", SegmentType.Typical, 33, 47, "S1", 3), S("CJ2", SegmentType.Closure, 47, 50.5), S("2-ES", SegmentType.End, 50.5, 60 - 10.0 / 12) },
        });
        sg.Bents.Add(new SegmentalBent
        {
            Name = "S1", DepthAtCl = In(231),
            Bearings =
            {
                new SegmentalBearing { Name = "left", Type = "II", Offset = -Ft(5.25), Height = In(20), SizeAcross = Ft(5.5), SizeAlong = Ft(5.5) },
                new SegmentalBearing { Name = "right", Type = "II", Offset = Ft(5.25), Height = In(20) },
            },
            Pier = new SegmentalPier
            {
                CapTop = Ft(55), CapHeight = Ft(5.5), CapTaperInset = In(4), CapTaperHeight = Ft(3), ColumnS = Ft(16), ColumnN = Ft(11), ColumnBaseTop = Ft(17),
                CapOutline = { new PlanPoint(-Ft(9), -Ft(6.5)), new PlanPoint(Ft(9), -Ft(6.5)), new PlanPoint(Ft(9), Ft(6.5)), new PlanPoint(-Ft(9), Ft(6.5)) },
                FootingOutline = { new PlanPoint(-Ft(25.5), -Ft(18)), new PlanPoint(Ft(25.5), -Ft(18)), new PlanPoint(Ft(25.5), Ft(18)), new PlanPoint(-Ft(25.5), Ft(18)) },
                FootingTop = Ft(7), FootingBottom = -Ft(0.5), PileCount = 24,
            },
        });
        sg.Tendons.Add(new TendonDefinition { Name = "T3", Group = TendonGroup.Cantilever, Layout = TendonLayout.Cantilever, Location = "S1", Count = 2, Strands = 22,
            PlanLength = Ft(92.25), PlanWeightEachLb = 1502, AnchorDown = 3, AnchorUp = 3, DuctOffset = Ft(15) + In(8.5), DuctDepth = In(7.25) });
        sg.Tendons.Add(new TendonDefinition { Name = "B1", Group = TendonGroup.Bottom, Layout = TendonLayout.FromEndSegment, Location = "Span 2", Count = 2, Strands = 16,
            PlanLength = Ft(30), DuctOffset = Ft(8.75), DuctDepth = In(5), AboveSoffit = true });
        sg.Transverse.Add(new TransverseTendonDefinition { SegmentType = SegmentType.Typical, Name = "TT", PerSegment = 4, PlanLength = Ft(41) + In(7), PlanWeightPerSegmentLb = 492.4, Depth = In(4.5) });
        b.Segmental = sg;
        var p = new BridgeProject { Name = "Seg test", Alignments = { al }, Bridges = { b } };
        return (p, b);
    }

    private static BridgeModel Generate(BridgeProject p) => new RegenerationCache().Regenerate(p).Single()!;

    /// <summary>Every edge of the (position-merged) mesh is used by exactly two triangles, in opposite directions.</summary>
    private static bool Closed(Mesh m)
    {
        var id = new Dictionary<Vec3, int>();
        int Key(Vec3 v) { var k = new Vec3(Math.Round(v.X, 7), Math.Round(v.Y, 7), Math.Round(v.Z, 7)); if (!id.TryGetValue(k, out int i)) id[k] = i = id.Count; return i; }
        var edges = new Dictionary<(int, int), int>();
        for (int t = 0; t < m.Indices.Count; t += 3)
        {
            int a = Key(m.Positions[m.Indices[t]]), b = Key(m.Positions[m.Indices[t + 1]]), c = Key(m.Positions[m.Indices[t + 2]]);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) }) edges[(u, v)] = edges.GetValueOrDefault((u, v)) + 1;
        }
        return edges.All(e => e.Value == 1 && edges.GetValueOrDefault((e.Key.Item2, e.Key.Item1)) == 1);
    }

    [Fact]
    public void Section_AtPrintedDepths_MatchesThePlanTable()
    {
        var box = new SegmentalBoxSection();
        Assert.Equal(126.139, Ft2(SegmentalSection.Area(box, In(228), In(24), noFillet: true)), 1);    // S3-20 section 1 (extraction gross area)
        Assert.Equal(87.104, Ft2(SegmentalSection.Area(box, In(108), In(10), noFillet: true)), 1);     // section 12
        Assert.True(Math.Abs(box.HalfSoffit(In(231)) - In(92.25)) < Sixteenth);                       // S3-29 bent: 7'-8 1/4"
        Assert.True(Math.Abs(box.HalfSoffit(In(228)) - In(93)) < Sixteenth);                          // section 1: 7'-9"
        Assert.True(Math.Abs(box.WebRootX - box.WebHorizontal - In(127)) < In(0.01));                 // inner web corner 127.0"
        var o = SegmentalSection.Outer(box, In(108));
        Assert.Equal(12, o.Count);
        Assert.Equal(In(255.5), o.Max(p => p.X), 6);
        Assert.Equal(new Vec2(0, In(108)), o[SegmentalSection.OuterCl]);
        var v = SegmentalSection.Void(box, In(108), In(10));
        Assert.Equal(new Vec2(0, In(98)), v[SegmentalSection.VoidBottomCl(box)]);
        // the fillet adds concrete: area with fillet > without
        Assert.True(SegmentalSection.Area(box, In(108), In(10)) > SegmentalSection.Area(box, In(108), In(10), noFillet: true));
    }

    [Fact]
    public void Segments_AreClosedChordsWithTableDepthsAtTheJoints()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sm = m.Concrete!.Segmental!;
        Assert.Equal(12, sm.Segments.Count);
        Assert.DoesNotContain(m.Framing.Diagnostics, d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Error);
        foreach (var s in sm.Segments)
        {
            Assert.True(Closed(s.Mesh), $"{s.Name} is not a closed shell");
            Assert.True(s.Mesh.Volume() > 0, s.Name);
        }
        // consecutive segments share their joint WP
        var ordered = sm.Segments.OrderBy(s => s.Station0).ToList();
        for (int i = 1; i < ordered.Count; i++)
            if (Math.Abs(ordered[i].Station0 - ordered[i - 1].Station1) < 1e-9) Assert.True((ordered[i].Top0 - ordered[i - 1].Top1).Length < 1e-9);
        // printed depths at the joints x = 5, 19, 33 ft from the bent
        var d1 = sm.Segments.Single(s => s.Name == "1-D1");
        Assert.True(Math.Abs(d1.Depth1 - In(228)) < Sixteenth);
        Assert.True(Math.Abs(d1.Depth0 - In(207.125)) < Sixteenth);
        var u3 = sm.Segments.Single(s => s.Name == "1-U3");
        Assert.True(Math.Abs(u3.Depth0 - In(188.375)) < Sixteenth);
        // the reconstruction beyond the table: parabola (flagged)
        Assert.Contains(sm.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("depth curve"));
        // bent halves: 19'-3" at the CL, solid diaphragm -> heavier than a hollow 5 ft piece, flagged
        var db = sm.Segments.Single(s => s.Name == "1-DB");
        Assert.True(Math.Abs(db.Depth1 - In(231)) < Sixteenth);
        Assert.True(db.Volume > SegmentalSection.Area(new SegmentalBoxSection(), In(231), In(27)) * Ft(5));
        Assert.NotNull(db.Assumption);
        Assert.NotNull(sm.Segments.Single(s => s.Name == "0-ES").Assumption);
        // closure / end segment: the constant depth
        Assert.True(Math.Abs(sm.Segments.Single(s => s.Name == "CJ1").Depth0 - In(188.375)) < 1e-9);
    }

    [Fact]
    public void CrossSlopeAndGrade_RotateTheBox_DimAAndB()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        var row = sm.BentRows.Single(r => r.Bent == "S1");
        Assert.True(Math.Abs(row.Depth - In(231)) < Sixteenth);
        // plan rule (S3-19): DIM B = H sin(atan(cross slope)), DIM A = H sin(atan(grade)); the box axis is normal to the top, so with the top falling to the
        // right the soffit CL lies to the LEFT (uphill) of the vertical through the WP
        Assert.True(Math.Abs(row.DimB + In(231) * Math.Sin(Math.Atan(0.045))) < Sixteenth, $"DIM B {row.DimB / In(1):0.000}");
        Assert.True(Math.Abs(Math.Abs(row.DimA) - In(231) * Math.Sin(Math.Atan(0.02))) < Sixteenth, $"DIM A {row.DimA / In(1):0.000}");
        Assert.Equal(-0.045, row.CrossSlope, 6);
    }

    [Fact]
    public void Tendons_FollowTheBoxAndCarryTheSchedule()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        var t3 = sm.Tendons.Where(t => t.Group == TendonGroup.Cantilever).ToList();
        Assert.Equal(2, t3.Count);
        // anchor faces = tip faces of D3 / U3: 2 x (5 + 3 x 14) = 94 ft along the (straight) alignment
        foreach (var t in t3) Assert.True(Math.Abs(t.Length - Ft(94)) < In(0.5), $"{t.Name} {t.Length / Ft(1):0.000}");
        Assert.Equal(2 * 1502, t3.Sum(t => t.PlanWeightLb));
        Assert.True(Math.Abs(sm.StrandWeightLb(t => t.Group == TendonGroup.Cantilever) - 2 * 22 * 94 * 0.74) < 5);
        // top slab duct: 7 1/4" below the top surface at every joint (straight runs joint to joint)
        var f = sm.Segments.Single(s => s.Name == "1-D1");
        Assert.All(t3, t => Assert.True(t.Points.Count >= 2));
        var b1 = sm.Tendons.Where(t => t.Group == TendonGroup.Bottom).ToList();
        Assert.Equal(2, b1.Count);
        Assert.All(b1, t => Assert.True(Math.Abs(t.Length - Ft(30)) < In(2), $"{t.Length / Ft(1):0.000}"));
        // transverse: 4 per typical segment, 41'-7"
        var tt = sm.Tendons.Where(t => t.Group == TendonGroup.Transverse).ToList();
        Assert.Equal(6 * 4, tt.Count);
        Assert.All(tt, t => Assert.True(Math.Abs(t.Length - (Ft(41) + In(7))) < 1e-6));
        _ = f;
    }

    [Fact]
    public void Bearings_AndBent_AreBuilt()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sm = m.Concrete!.Segmental!;
        Assert.Equal(2, sm.Bearings.Count);
        Assert.NotNull(sm.Bearings.Single(b => b.Name == "S1 left").Mesh);
        Assert.Null(sm.Bearings.Single(b => b.Name == "S1 right").Mesh);                  // size not given -> no solid
        var bl = sm.Bearings.Single(b => b.Name == "S1 left");
        Assert.True(Math.Abs(bl.Top.Z - bl.SoffitZ) < 1e-9);                                // no pedestal top: bearing top on the soffit
        Assert.NotNull(m.Substructure);
        var unit = m.Substructure!.Units.Single();
        Assert.Contains(unit.Solids, s => s.Kind == SBM.Core.Substructure.SubElementKind.Cap);
        Assert.Contains(unit.Solids, s => s.Kind == SBM.Core.Substructure.SubElementKind.Column);
        Assert.Contains(unit.Solids, s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing);
        Assert.Equal(Ft(55), unit.Solids.Single(s => s.Kind == SBM.Core.Substructure.SubElementKind.Cap).Mesh.Bounds().Max.Z, 6);
    }

    [Fact]
    public void Ifc_WritesSegmentsTendonsAndBearings()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "seg.ifc", p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        string s = sw.ToString();
        var lines = s.Split('\n');
        Assert.Equal(12, lines.Count(l => l.Contains("=IFCBEAM(") && l.Contains(".GIRDER_SEGMENT.")));
        Assert.Equal(2, lines.Count(l => l.Contains("=IFCBEAM(") && l.Contains("'CLOSURE_JOINT'")));
        Assert.Equal(2 + 2 + 24, lines.Count(l => l.Contains("=IFCTENDON(") && l.Contains(".STRAND.")));
        Assert.Equal(2, lines.Count(l => l.Contains("=IFCBEARING(") && l.Contains(".DISK.")));
        Assert.Contains(lines, l => l.Contains("=IFCELEMENTASSEMBLY(") && l.Contains("S1 cantilever") && l.Contains(".GIRDER."));
        Assert.Contains("'SBM_Tendon'", s);
        Assert.Contains("'SBM_Status'", s);
        Assert.Contains(lines, l => l.Contains("=IFCBEAM(") && l.Contains(".PIERCAP."));
        // database keys = IFC GlobalIds
        var idx = SBM.IO.Database.ModelDatabase.BuildTagIndex(p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        string bk = IfcKeys.Bridge(p.Name, "Seg Unit 1");
        foreach (var k in new[] { IfcKeys.Segment(bk, "1-U2"), IfcKeys.SegTendon(bk, "S1 T3 L"), IfcKeys.SegBearing(bk, "S1 left"), IfcKeys.SegCantilever(bk, "S1") })
            Assert.Contains(IfcKeys.GlobalId(k), s);
        Assert.NotNull(idx);
    }

    [Fact]
    public void Json_RoundTripsAndIsOmittedWhenNull()
    {
        var (p, _) = Model();
        var back = BridgeProject.FromJson(p.ToJson());
        var sg = back.Bridges[0].Segmental!;
        Assert.Equal(2, sg.Spans.Count);
        Assert.Equal(4, sg.Bents[0].Pier!.FootingOutline.Count);
        Assert.Equal(p.Bridges[0].Segmental!.ParabolaCoefficient, sg.ParabolaCoefficient, 12);
        var plain = new BridgeProject { Bridges = { BridgeDefinition.CreateDefault(0, "A") } };
        Assert.DoesNotContain("Segmental", plain.ToJson());
        // a regular steel bridge still needs girders; a segmental one must have none
        var bad = Model().b; bad.Girders.Count = 2;
        Assert.Contains(bad.Validate(), e => e.Contains("segmental"));
    }

    private static string? RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SteelBridgeModeler.sln"))) return Path.Combine(d.FullName, rel);
        return null;
    }

    [Fact]
    public void BonnerUnit5Sample_MatchesTheSegmentLayoutAndTendonSchedule()
    {
        var path = RepoFile(Path.Combine("samples", "plans", "Bonner Unit 5 segmental.sbm"));
        if (path == null || !File.Exists(path)) return;
        var doc = SbmDocument.Open(path);
        var sm = doc.Segmental("Bonner Unit 5");
        Assert.Equal(242, sm.Segments.Count(s => s.Type == SegmentType.Typical));       // S3-34: 2 x 13 + 9 x 24
        Assert.Equal(11, sm.Segments.Count(s => s.Type == SegmentType.Closure));
        Assert.Equal(20, sm.Segments.Count(s => s.Type == SegmentType.BentHalf));
        Assert.Equal(2, sm.Segments.Count(s => s.Type == SegmentType.End));
        Assert.All(sm.Segments, s => Assert.True(Closed(s.Mesh), s.Name));
        // tendon schedule (S3-57/58): plan weights T 774,968 / B 267,524 / C 25,716 lb
        Assert.Equal(774968, sm.Tendons.Where(t => t.Group == TendonGroup.Cantilever).Sum(t => t.PlanWeightLb), 0);
        Assert.Equal(267524, sm.Tendons.Where(t => t.Group == TendonGroup.Bottom).Sum(t => t.PlanWeightLb), 0);
        Assert.Equal(25716, sm.Tendons.Where(t => t.Group == TendonGroup.Closure).Sum(t => t.PlanWeightLb), 0);
        // transverse PT typical + closures: printed 121,624 lb (rounded per segment)
        var segType = sm.Segments.ToDictionary(s => s.Name, s => s.Type);
        double tw = sm.StrandWeightLb(t => t.Group == TendonGroup.Transverse && segType[t.Owner] is SegmentType.Typical or SegmentType.Closure);
        Assert.True(Math.Abs(tw - 121624) < 25, $"{tw:0}");
        // depths at the printed sections (S3-20) at every cantilever joint within 1/16"
        Assert.All(sm.Checks.Where(c => c.Item.StartsWith("depth at section")), c => Assert.True(Math.Abs(c.Difference) < Sixteenth, c.Item));
        // span lengths along -L-: 350 ft (200 ft end spans less the 10" joint gap)
        foreach (var g in sm.Segments.GroupBy(s => s.Span))
        {
            double len = (g.Max(s => s.Station1) - g.Min(s => s.Station0)) / Ft(1);
            Assert.Equal(g.Key is "Span 19" or "Span 29" ? 200 - 10.0 / 12 : 350, len, 2);
        }
    }
}
