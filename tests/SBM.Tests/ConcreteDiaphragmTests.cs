using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Diaphragms;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Concrete end / pier diaphragms (SupportDefinition.Diaphragm, Diaphragms/ConcreteDiaphragmGenerator).</summary>
public class ConcreteDiaphragmTests
{
    private const double Z0 = 300;                // flat profile elevation (m)
    private static readonly double Slab = In(8), Haunch = In(2);

    /// <summary>Straight, level, no cross slope, square supports: 3 Tx54 beams at -6 / 0 / +6 ft, deck -9 .. +9 ft.</summary>
    internal static SbmDocument Flat(int spans = 1, double skew = 0)
    {
        var doc = SbmDocument.New("diaphragm test");
        doc.AddAlignment("CL", 0, new[] { new PiRow(0, 0), new PiRow(Ft(1000), 0) },
            new[] { new Vpi(0, Z0), new Vpi(Ft(1000), Z0) }, new CrossSlopeTable { Rows = { new CrossSlopeRow(0, 0, 0) } });
        var b = new BridgeDefinition { Name = "B", AlignmentName = "CL" };
        b.FieldSplices.Clear();
        for (int j = 0; j <= spans; j++)
            b.Supports.Add(new SupportDefinition
            {
                Name = j == 0 ? "Abut 1" : j == spans ? "Abut 2" : $"Pier {j}", Type = j == 0 || j == spans ? SupportType.Abutment : SupportType.Pier,
                Station = Ft(100 + 80 * j), SkewDegrees = skew, DeckEndOffset = Ft(1.5),
            });
        b.Girders.Count = 3;
        b.Girders.FirstGirderOffset = -Ft(6);
        b.Girders.Spacings = new() { Ft(6), Ft(6) };
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        b.Girders.Prestressed.Type = TxGirderType.Tx54;
        b.Girders.Prestressed.BearingToEnd = In(8);
        b.Girders.Prestressed.EndGapAtBent = In(4);
        b.Girders.Prestressed.InteriorEnds = InteriorGirderEnds.Skewed;
        b.Girders.Prestressed.PadLength = In(9);
        b.Girders.Prestressed.PadWidth = In(24);
        b.Girders.Prestressed.PadThickness = In(1);
        b.Deck.LeftEdgeOffset = -Ft(9);
        b.Deck.RightEdgeOffset = Ft(9);
        b.Deck.Thickness = Slab;
        b.Deck.OverhangThickness = Slab;
        b.Deck.Haunch = Haunch;
        b.Deck.Reinforcement.Enabled = false;
        b.Deck.Barrier.Type = BarrierType.None;
        foreach (var s in b.Supports) { s.Substructure = SubstructureDefinition.Default(s.Type); s.Substructure.Enabled = false; }
        doc.Project.Bridges.Add(b);
        return doc;
    }

    private static DiaphragmSectionPoint P(double x, DiaphragmZRef r, double dz = 0) => new(x, r, dz);

    private static ConcreteDiaphragmDefinition Rect(double x0, double x1, double bottom) => new()
    {
        Kind = ConcreteDiaphragmKind.IntegralEnd, Name = "test",
        Parts = { new DiaphragmPart { Name = "main", Section = { P(x0, DiaphragmZRef.Elevation, bottom), P(x1, DiaphragmZRef.Elevation, bottom), P(x1, DiaphragmZRef.Soffit), P(x0, DiaphragmZRef.Soffit) } } },
        Left = DiaphragmEnd.AtOffset(-Ft(9)), Right = DiaphragmEnd.AtOffset(Ft(9)),
    };

    private static double BeamBottom(SbmDocument doc) => doc.Prestressed().Beams[0].BearingStart.Z - doc.Prestressed().Shape.Depth;

    [Fact]
    public void EndDiaphragm_VolumeIsGrossMinusEmbeddedBeamsAndHaunches()
    {
        var doc = Flat();
        doc.Regenerate();
        double zb = BeamBottom(doc) - In(3);
        doc.Bridge().Supports[0].Diaphragm = Rect(-Ft(1.5), Ft(1.5), zb);
        doc.Regenerate();
        var ps = doc.Prestressed();
        var cd = ps.ConcreteDiaphragms!;
        var d = Assert.Single(cd.Diaphragms);
        var shape = ps.Shape;
        double soffit = Z0 - Slab;
        double area = MeshBuilder.SignedArea(shape.Outline());
        double embed = Ft(1.5) - (-In(8));                    // beam end 8" behind the CL brg
        double expected = Ft(3) * Ft(18) * (soffit - zb) - 3 * embed * (area + shape.TopWidth * Haunch);
        Assert.Equal(expected, d.Volume, 6);
        // dimensions of the solid: bottom / top elevations, length, thickness normal to the support
        var bb = d.Mesh.Bounds();
        Assert.Equal(zb, bb.Min.Z, 6);
        Assert.Equal(soffit, bb.Max.Z, 6);
        Assert.Equal(Ft(18), d.Length, 6);
        Assert.Equal(Ft(18), bb.Max.X - bb.Min.X, 6);         // alignment runs north (PI rows are northing, easting): support line along X
        Assert.Equal(Ft(3), bb.Max.Y - bb.Min.Y, 6);
        Assert.Contains(cd.Diagnostics, x => x.Message.Contains("beams embedded G1-S1, G2-S1, G3-S1"));
        // every piece is a closed solid
        foreach (var pc in d.Pieces) Assert.True(Math.Abs(pc.Volume()) > 0);
        Assert.Equal(d.Volume, d.Pieces.Sum(p => Math.Abs(p.Volume())), 6);
    }

    [Fact]
    public void BeamBottomBelowDiaphragmBottom_IsReportedNotBuilt()
    {
        var doc = Flat();
        doc.Regenerate();
        doc.Bridge().Supports[0].Diaphragm = Rect(-Ft(1.5), Ft(1.5), BeamBottom(doc) + In(3));
        doc.Regenerate();
        var cd = doc.Prestressed().ConcreteDiaphragms!;
        Assert.Empty(cd.Diaphragms);
        Assert.Contains(cd.Diagnostics, x => x.Message.Contains("beam bottom is at or below the part bottom"));
    }

    [Fact]
    public void UnresolvedPart_IsNotBuilt_AndRisersUnresolved_FlagTheDiaphragm()
    {
        var doc = Flat();
        doc.Regenerate();
        double zb = BeamBottom(doc);
        var def = Rect(-Ft(1.5), -Ft(1), zb);                 // back strip only, no beams in it
        def.Parts.Add(new DiaphragmPart { Name = "front", Unresolved = "bottom not dimensioned (p.21)" });
        def.BottomSteps = new() { new(1, zb - In(3)), new(2, zb - In(2)), new(3, zb - In(1)) };
        def.Parts[0].Section[0].Ref = def.Parts[0].Section[1].Ref = DiaphragmZRef.Bottom;
        def.Parts[0].Section[0].Dz = def.Parts[0].Section[1].Dz = 0;
        doc.Bridge().Supports[0].Diaphragm = def;
        doc.Regenerate();
        var cd = doc.Prestressed().ConcreteDiaphragms!;
        var d = Assert.Single(cd.Diaphragms);
        Assert.Equal(new[] { "front" }, d.UnbuiltParts);
        Assert.Contains(cd.Diagnostics, x => x.Severity == DiagnosticSeverity.Warning && x.Message.Contains("UNRESOLVED: front not built - bottom not dimensioned (p.21)"));
        Assert.NotNull(d.Assumption);
        Assert.Equal(SBM.Core.Assumption.UnresolvedStatus, d.Assumption!.Status);
        // stepped bottom with risers at mid-bay (+-3 ft): volume = 0.5 ft x (soffit - step) x each step's length
        double soffit = Z0 - Slab;
        double expected = Ft(0.5) * (Ft(6) * (soffit - (zb - In(3))) + Ft(6) * (soffit - (zb - In(2))) + Ft(6) * (soffit - (zb - In(1))));
        Assert.Equal(expected, d.Volume, 6);
        Assert.Equal(zb - In(3), d.Mesh.Bounds().Min.Z, 6);
    }

    [Fact]
    public void PierDiaphragm_EmbedsBothSpans_EndsFlushWithFasciaBeam_AndFillersKeyway()
    {
        var doc = Flat(spans: 2);
        doc.Regenerate();
        var ps0 = doc.Prestressed();
        double zb = BeamBottom(doc) - In(1);
        var sec = new List<DiaphragmSectionPoint> { P(-Ft(1.5), DiaphragmZRef.Bottom), P(Ft(1.5), DiaphragmZRef.Bottom), P(Ft(1.5), DiaphragmZRef.Soffit), P(-Ft(1.5), DiaphragmZRef.Soffit) };
        var def = new ConcreteDiaphragmDefinition
        {
            Kind = ConcreteDiaphragmKind.PierContinuity,
            Parts = { new DiaphragmPart { Name = "pier", Section = sec } },
            Left = DiaphragmEnd.AtOffset(-Ft(9)), Right = DiaphragmEnd.Fascia(),
            BottomSteps = { new(1, zb), new(2, zb), new(3, zb) }, Risers = DiaphragmRiserRule.MidBay, PlanBearingGap = In(1),
            Fillers = { new DiaphragmFillerDefinition { Name = "around bearings", Material = "Preformed filler", Kind = DiaphragmFillerKind.AroundBearings, Thickness = In(1) } },
            Keyways = { new DiaphragmKeywayDefinition { Name = "key", X = 0, Width = In(8), Depth = In(2) } },
        };
        doc.Bridge().Supports[1].Diaphragm = def;
        doc.Regenerate();
        var ps = doc.Prestressed();
        var cd = ps.ConcreteDiaphragms!;
        var d = Assert.Single(cd.Diaphragms);
        Assert.Null(d.Assumption);                               // risers confirmed (MidBay) and all level anyway
        Assert.Equal("PIER_DIAPHRAGM", d.ObjectType);
        var shape = ps.Shape;
        // flush with the outside face (top flange edge) of the fascia beam G3 at +6 ft
        Assert.Equal(Ft(15) + shape.TopWidth / 2, d.Length, 6);
        Assert.Contains(cd.Diagnostics, x => x.Message.Contains("G1-S1, G2-S1, G3-S1") && x.Message.Contains("G1-S2, G2-S2, G3-S2") || x.Message.Contains("beams embedded G1-S1, G2-S1, G3-S1"));
        Assert.Contains(cd.Diagnostics, x => x.Message.Contains("beam bottoms at the CL bearings are 1\" above the plan steps"));
        // volume: gross minus 6 embedded beam ends (each 1'-6 - 4" deep into the 3'-0)
        double soffit = Z0 - Slab;
        double area = MeshBuilder.SignedArea(shape.Outline());
        double embed = Ft(1.5) - In(4);
        double expected = Ft(3) * d.Length * (soffit - zb) - 6 * embed * (area + shape.TopWidth * Haunch);
        Assert.Equal(expected, d.Volume, 5);
        // keyway: 8" x 2" between the bottom flanges (2 bays of 6 ft - bottom flange width)
        var key = Assert.Single(cd.Accessories, a => a.Kind == DiaphragmAccessoryKind.Keyway);
        Assert.Equal(2 * (Ft(6) - shape.BottomWidth) * In(8) * In(2), key.Volume, 7);
        Assert.Equal(zb - In(2), key.Mesh.Bounds().Min.Z, 6);
        // preformed filler around the pads: bottom flange footprint inside the diaphragm minus the pad, 1" high
        var fill = Assert.Single(cd.Accessories, a => a.Kind == DiaphragmAccessoryKind.Filler);
        // pad 9" x 2'-0 centred 1'-0 from the CL pier: wholly inside the 1'-2 embedment (4" .. 1'-6)
        double zoneW = Math.Max(shape.BottomWidth, shape.TopWidth);   // bearing zone width = open-below void width
        Assert.Equal(6 * (zoneW * embed - In(24) * In(9)) * In(1), fill.Volume, 7);
        // bearing zone open below the beams (pads + filler, not concrete): minus the bottom-flange footprint x 1" per beam end
        def.OpenBelowBeams = true;
        doc.Regenerate();
        Assert.True(doc.Prestressed().ConcreteDiaphragms!.Diaphragms.Count == 1, string.Join(" | ", doc.Prestressed().ConcreteDiaphragms!.Diagnostics.Select(x => x.Message)));
        var open = doc.Prestressed().ConcreteDiaphragms!.Diaphragms[0];
        Assert.True(Math.Abs(expected - 6 * zoneW * embed * In(1) - open.Volume) < 1e-5, string.Join("; ", open.Pieces.Select(pc => { var bb = pc.Bounds(); return $"{Math.Abs(pc.Volume()):0.0000} x{bb.Min.X:0.00}..{bb.Max.X:0.00} y{bb.Min.Y:0.00}..{bb.Max.Y:0.00}"; })));
    }

    [Theory]
    [InlineData(InteriorGirderEnds.Square)]
    [InlineData(InteriorGirderEnds.Skewed)]
    public void PierDiaphragm_OnASkew_CutsSquareAndSkewedBeamEndsExactly(InteriorGirderEnds ends)
    {
        const double skew = 20;
        var doc = Flat(spans: 2, skew: skew);
        doc.Bridge().Girders.Prestressed.InteriorEnds = ends;
        doc.Regenerate();
        double zb = BeamBottom(doc) - In(1);
        var sec = new List<DiaphragmSectionPoint> { P(-Ft(1.5), DiaphragmZRef.Elevation, zb), P(Ft(1.5), DiaphragmZRef.Elevation, zb), P(Ft(1.5), DiaphragmZRef.Soffit), P(-Ft(1.5), DiaphragmZRef.Soffit) };
        doc.Bridge().Supports[1].Diaphragm = new ConcreteDiaphragmDefinition
        {
            Kind = ConcreteDiaphragmKind.PierContinuity, Parts = { new DiaphragmPart { Name = "pier", Section = sec } },
            Left = DiaphragmEnd.AtOffset(-Ft(9)), Right = DiaphragmEnd.AtOffset(Ft(9)),
        };
        doc.Regenerate();
        var ps = doc.Prestressed();
        Assert.True(ps.ConcreteDiaphragms!.Diaphragms.Count == 1, string.Join(" | ", ps.ConcreteDiaphragms.Diagnostics.Select(x => x.Message)));
        var d = ps.ConcreteDiaphragms!.Diaphragms[0];
        var sl = doc.Framing().Supports[1];
        var n = sl.Direction.Left; if (n.Y < 0) n = n * -1;     // up-station normal (alignment runs north)
        double cos = Math.Cos(skew * Math.PI / 180);
        double area = MeshBuilder.SignedArea(ps.Shape.Outline()) + ps.Shape.TopWidth * Haunch;
        double embedded = 0;
        foreach (var b in ps.Beams)
        {
            // axial length of the CL inside the 3'-0 band from the end face (prism volume = area x axial length at the centroid, xb = 0)
            if (b.Span == 0) embedded += area * (Vec2.Dot(b.TopEnd.XY - sl.AlignmentPoint, n) + Ft(1.5)) / cos;
            else embedded += area * (Ft(1.5) - Vec2.Dot(b.TopStart.XY - sl.AlignmentPoint, n)) / cos;
        }
        double gross = Ft(3) * Ft(18) / cos * (Z0 - Slab - zb);
        Assert.Equal(gross - embedded, d.Volume, 6);
        Assert.Equal(Ft(18) / cos, d.Length, 6);
    }

    [Fact]
    public void Ifc_WritesDiaphragmAsIfcBeamDiaphragm_WithFillerParts()
    {
        var doc = Flat();
        doc.Regenerate();
        var def = Rect(-Ft(1.5), Ft(1.5), BeamBottom(doc) - In(3));
        def.Fillers.Add(new DiaphragmFillerDefinition { Name = "poly W", Material = "1\" Type B polystyrene", Kind = DiaphragmFillerKind.EndFace, Side = -1, Thickness = In(1) });
        doc.Bridge().Supports[0].Diaphragm = def;
        doc.Regenerate();
        var m = doc.Model();
        var sw = new StringWriter();
        IfcExporter.Export(sw, "t.ifc", doc.Project, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        string ifc = sw.ToString();
        Assert.Contains("'END_DIAPHRAGM'", ifc);
        Assert.Single(ifc.Split('\n'), l => l.Contains("IFCBEAM(") && l.TrimEnd().EndsWith(".DIAPHRAGM.);"));
        Assert.Contains(ifc.Split('\n'), l => l.Contains("IFCBUILDINGELEMENTPART(") && l.Contains("'JOINT_FILLER'"));
        Assert.Contains("Type B polystyrene", ifc);
        var fill = doc.Prestressed().ConcreteDiaphragms!.Accessories.Single();
        double h = (Z0 - Slab) - (BeamBottom(doc) - In(3));
        Assert.Equal(In(1) * Ft(3) * h, fill.Volume, 7);
    }

    [Fact]
    public void Definition_RoundTripsInTheProjectFile_AndIsOmittedWhenNull()
    {
        var doc = Flat();
        string before = doc.Project.ToJson();
        Assert.DoesNotContain("\"Diaphragm\"", before);
        doc.Bridge().Supports[0].Diaphragm = Rect(-Ft(1.5), Ft(1.5), 100);
        doc.Bridge().Supports[0].Diaphragm!.Parts.Add(new DiaphragmPart { Name = "x", Unresolved = "why" });
        var back = BridgeProject.FromJson(doc.Project.ToJson());
        var d = back.Bridges[0].Supports[0].Diaphragm!;
        Assert.Equal(2, d.Parts.Count);
        Assert.Equal("why", d.Parts[1].Unresolved);
        Assert.Equal(DiaphragmEndKind.Offset, d.Left!.Kind);
        Assert.Null(back.Bridges[0].Supports[1].Diaphragm);
        var clone = doc.Bridge().Supports[0].Clone();
        Assert.NotSame(clone.Diaphragm, doc.Bridge().Supports[0].Diaphragm);
    }

    /// <summary>Vertical-line hits of a mesh at a plan point (sorted z).</summary>
    private static List<double> Hits(Mesh m, Vec2 q)
    {
        var zs = new List<double>();
        for (int t = 0; t + 2 < m.Indices.Count; t += 3)
        {
            var a = m.Positions[m.Indices[t]]; var b = m.Positions[m.Indices[t + 1]]; var c = m.Positions[m.Indices[t + 2]];
            double d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(d) < 1e-12) continue;
            double l1 = ((b.Y - c.Y) * (q.X - c.X) + (c.X - b.X) * (q.Y - c.Y)) / d, l2 = ((c.Y - a.Y) * (q.X - c.X) + (a.X - c.X) * (q.Y - c.Y)) / d, l3 = 1 - l1 - l2;
            if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
            zs.Add(l1 * a.Z + l2 * b.Z + l3 * c.Z);
        }
        return zs.OrderBy(z => z).ToList();
    }

    /// <summary>Semi-integral end diaphragm on bearings (MnDOT Br. 02069 p.21 A-A / B-B): DiaphragmRiserRule.BeamBottoms = the bottom is the bottom of the
    /// beams (level under each bottom flange, straight between adjacent flange edges), chamfer + fillet edges, open below the beams; a filler whose TOP is on
    /// the Bottom reference follows the pieces between the beams; sloped part bottoms crossing the beam outline (fillet) are built.</summary>
    [Fact]
    public void SemiIntegral_BeamBottomsRule_BottomFollowsTheBeams_ChamferFilletAndFillerBuilt()
    {
        var doc = Flat();
        doc.Alignment("CL").CrossSlope = new CrossSlopeTable { PivotOffset = 0, Rows = { new CrossSlopeRow(0, -0.02, -0.02) } };
        doc.Regenerate();
        var ps0 = doc.Prestressed();
        double Bb(int g) => ps0.Beams.First(x => x.Girder == g && x.Span == 0).BearingStart.Z - ps0.Shape.Depth;
        double b0 = Bb(0), b1 = Bb(1), b2 = Bb(2);
        Assert.True(b1 - b0 > Ft(0.1));                       // crown: the middle beam is higher
        const DiaphragmZRef Bot = DiaphragmZRef.Bottom, Sof = DiaphragmZRef.Soffit, El = DiaphragmZRef.Elevation;
        double zLow = b0 - Ft(2);
        doc.Bridge().Supports[0].Diaphragm = new ConcreteDiaphragmDefinition
        {
            Kind = ConcreteDiaphragmKind.SemiIntegralEnd, Name = "si", Risers = DiaphragmRiserRule.BeamBottoms, OpenBelowBeams = true,
            Parts =
            {
                new DiaphragmPart { Name = "back", Section = { P(-Ft(2), El, zLow), P(-Ft(1.25), El, zLow), P(-Ft(1.25), Sof), P(-Ft(2), Sof) } },
                new DiaphragmPart { Name = "front", Section = { P(-Ft(1.25), Bot, -In(3)), P(-Ft(1), Bot), P(Ft(0.5), Bot), P(Ft(0.5), Sof, -Ft(1)), P(Ft(1.5), Sof), P(-Ft(1.25), Sof) } },
            },
            Left = DiaphragmEnd.AtOffset(-Ft(9)), Right = DiaphragmEnd.AtOffset(Ft(9)),
            Fillers =
            {
                new DiaphragmFillerDefinition { Name = "3in", Material = "3\" Type B polystyrene", Kind = DiaphragmFillerKind.Section, Thickness = In(3),
                    Section = { P(-Ft(1.25), El, zLow), P(-Ft(1), El, zLow), P(-Ft(1), Bot, -In(3)), P(-Ft(1.25), Bot, -In(3)) } },
            },
        };
        doc.Regenerate();
        var cd = doc.Prestressed().ConcreteDiaphragms!;
        Assert.DoesNotContain(cd.Diagnostics, x => x.Message.Contains("not built"));
        var d = Assert.Single(cd.Diaphragms);
        Assert.Equal(2, d.Parts.Count);
        foreach (var pc in d.Pieces) Assert.True(Math.Abs(pc.Volume()) > 0);
        Assert.Equal(d.Volume, d.Pieces.Sum(p => Math.Abs(p.Volume())), 6);
        var al = doc.Alignment("CL");
        double sta = doc.Bridge().Supports[0].Station;
        Vec2 Pt(double off, double x) => al.PointAt(sta + x, off);
        // mid-bay between G1 (-6) and G2 (0) the bottom is the straight line between the flange edges (here the mean of the two beam bottoms)
        Assert.Equal((b0 + b1) / 2, Hits(d.Mesh, Pt(-Ft(3), 0))[0], 4);
        // behind the beam end (8") at the beam CL: level under the flange at that beam's bottom
        Assert.Equal(b1, Hits(d.Mesh, Pt(0, -Ft(0.9)))[0], 4);
        Assert.Equal(b0, Hits(d.Mesh, Pt(-Ft(8.5), 0))[0], 4);                // beyond the fascia flange edge: level at the fascia bottom
        // chamfer 3" x 3": half way across it the bottom is 1 1/2" below the beam-bottom line
        Assert.Equal((b0 + b1) / 2 - In(1.5), Hits(d.Mesh, Pt(-Ft(3), -Ft(1.125)))[0], 4);
        // fillet: 6" ahead of the front face the bottom is 6" below the soffit
        Assert.Equal(Hits(d.Mesh, Pt(-Ft(3), Ft(1)))[^1] - In(6), Hits(d.Mesh, Pt(-Ft(3), Ft(1)))[0], 4);
        // the filler's top (Bottom reference) follows the pieces: mid-bay = mean - 3"
        var f = Assert.Single(cd.Accessories);
        Assert.Equal((b0 + b1) / 2 - In(3), Hits(f.Mesh, Pt(-Ft(3), -Ft(1.125)))[^1], 4);
        Assert.Equal(zLow, Hits(f.Mesh, Pt(-Ft(3), -Ft(1.125)))[0], 6);
    }

    [Fact]
    public void SteelSample_IsUnchanged_NoDiaphragmsInFileModelOrIfc()
    {
        var doc = SbmDocument.Sample();
        Assert.DoesNotContain("Diaphragm\"", doc.Project.ToJson());
        doc.Regenerate();
        var m = doc.Model();
        Assert.Null(m.Prestressed);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "s.ifc", doc.Project, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        Assert.DoesNotContain(".DIAPHRAGM.", sw.ToString());
        Assert.DoesNotContain("JOINT_FILLER", sw.ToString());
        // prestressed bridge without definitions: no diaphragm model at all
        var flat = Flat();
        flat.Regenerate();
        Assert.Null(flat.Prestressed().ConcreteDiaphragms);
    }
}
