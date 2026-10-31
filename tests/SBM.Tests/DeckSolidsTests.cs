using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.IO.Rhino3dm;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>
/// Deck-related elements as CLOSED exact solids (2026-10-28): the exact slab where cross-slope kink lines run into a skewed end (panel layout with
/// one column set, shared rows across), blockouts / end blocks on a kinked end whose break point leaves the block across its width, concrete
/// diaphragms, tub girder haunches, the arch deck; and the exact-mode slab MESH edge-manifold.
/// </summary>
[Collection(RhinoNativeCollection.Name)]
public class DeckSolidsTests
{
    private static string TempFile(string name) => Path.Combine(Path.GetTempPath(), "sbm-decksolids-" + Guid.NewGuid().ToString("N")[..8] + "-" + name);

    /// <summary>Every edge of the mesh (vertices merged at 1e-7 m, triangles repeating a vertex left out) shared by exactly two triangles.</summary>
    internal static (int bad, int degenerate) EdgeCheck(Mesh m)
    {
        var key = new Dictionary<(long, long, long), int>();
        var id = new int[m.Positions.Count];
        for (int i = 0; i < id.Length; i++)
        {
            var p = m.Positions[i];
            var k = ((long)Math.Round(p.X * 1e7), (long)Math.Round(p.Y * 1e7), (long)Math.Round(p.Z * 1e7));
            if (!key.TryGetValue(k, out int v)) key[k] = v = key.Count;
            id[i] = v;
        }
        var edges = new Dictionary<(int, int), int>();
        int degen = 0;
        for (int t = 0; t < m.TriangleCount; t++)
        {
            int a = id[m.Indices[3 * t]], b = id[m.Indices[3 * t + 1]], c = id[m.Indices[3 * t + 2]];
            if (a == b || b == c || a == c) { degen++; continue; }
            foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) })
            {
                var e = (Math.Min(x, y), Math.Max(x, y));
                edges[e] = edges.GetValueOrDefault(e) + 1;
            }
        }
        return (edges.Values.Count(n => n != 2), degen);
    }

    /// <summary>Exports the given layers and asserts: every object a valid closed B-rep / extrusion, every element's volume within 0.1 % of the
    /// model's Volume_m3 (when it has one).</summary>
    internal static List<Rhino3dmObjectInfo> AssertClosed(SbmDocument doc, string name, string layers)
    {
        string path = TempFile(name + ".3dm");
        try
        {
            var r = doc.Export3dm(path, detail: false, layers: layers);
            string why = string.Join("; ", r.FallbackItems.Concat(r.MeshItems));
            Assert.True(r.Fallbacks == 0, why);
            string units = Rhino3dmInfo.Read(path).Units;
            double s = units == "Feet" ? 0.3048 : units == "Meters" ? 1 : throw new InvalidOperationException(units);
            var objs = Rhino3dmInfo.Objects(path, layers);
            Assert.NotEmpty(objs);
            foreach (var o in objs)
            {
                Assert.True(o.Type is "Brep" or "Extrusion", $"{o.Name}: {o.Type} ({why})");
                Assert.True(o.Valid && o.Closed, $"{o.Name}: valid {o.Valid}, closed {o.Closed} {o.Naked}");
            }
            foreach (var g in objs.Where(o => o.ModelVolumeM3 != "").GroupBy(o => (o.Element, o.Name)))
            {
                double v = g.Sum(o => o.Volume) * s * s * s, model = double.Parse(g.First().ModelVolumeM3, System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(Math.Abs(v / model - 1) < 0.001, $"{g.Key.Name}: 3dm {v:0.######} m3 vs model {model:0.######} m3");
            }
            return objs;
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RuledLoft_BoxAndWedge_ExactVolume_OutwardFaces()
    {
        var r0 = new[] { new Vec3(0, 0, 0), new Vec3(2, 0, 0), new Vec3(2, 0, 1), new Vec3(0, 0, 1) };
        var r1 = new[] { new Vec3(0, 3, 0), new Vec3(2, 3, 0), new Vec3(2, 3, 2), new Vec3(0, 3, 1) };   // top of the far ring raised at one corner
        var s = NurbsSolid.RuledLoft(r0, r1, "test")!;
        Assert.Equal(6, s.Faces.Count);
        // the straight loft: section area linear in y except the bilinear top: area(y) = 2 + y/3 (one corner rising 1 m over 3 m: half of the top width)
        Assert.Equal(2 * 3 * 1 + 0.5 * 3 * 1 * 2 / 2, s.Volume(), 9);
        // reversed ring order gives the same solid
        var s2 = NurbsSolid.RuledLoft(r0.Reverse().ToArray(), r1.Reverse().ToArray(), "test")!;
        Assert.Equal(s.Volume(), s2.Volume(), 9);
        // a vertex repeated in one ring (a triangle face) is fine; rings that do not match are refused
        var t1 = new[] { new Vec3(0, 3, 0), new Vec3(2, 3, 0), new Vec3(1, 3, 1), new Vec3(1, 3, 1) };
        Assert.NotNull(NurbsSolid.RuledLoft(r0, t1, "test"));
        Assert.Null(NurbsSolid.RuledLoft(r0, t1.Take(3).ToArray(), "test"));
    }

    [Fact]
    public void SkewedEnds_CrossSlopeKinksRunningIntoThem_SlabIsClosed_MeshAndBrep()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        foreach (var sp in b.Supports) sp.SkewDegrees = 35;
        // superelevation rows close to both ends: their radial kink lines meet the skewed end lines inside the deck (several per end)
        var al = doc.Alignment(b.AlignmentName);
        double s0 = b.Supports[0].Station, s1 = b.Supports[^1].Station;
        al.CrossSlope = new CrossSlopeTable
        {
            Rows =
            {
                new CrossSlopeRow(s0 - Ft(200), -0.02, -0.02), new CrossSlopeRow(s0 - Ft(4), -0.02, -0.02), new CrossSlopeRow(s0 - Ft(1), -0.03, -0.01),
                new CrossSlopeRow(s0 + Ft(2), -0.04, 0.0), new CrossSlopeRow(s0 + Ft(5), -0.05, 0.01), new CrossSlopeRow(s1 - Ft(3), -0.05, 0.01),
                new CrossSlopeRow(s1 + Ft(1), -0.03, -0.01), new CrossSlopeRow(s1 + Ft(200), -0.02, -0.02),
            },
        };
        doc.Project.Geometry.ExactCurvedGeometry = true;
        doc.Regenerate();
        var c = doc.Concrete();
        Assert.True(c.Deck!.HasExactNurbs, c.Nurbs?.Summary());
        var (bad, degen) = EdgeCheck(c.Deck);
        Assert.Equal(0, bad);
        Assert.Equal(0, degen);
        Assert.True(Math.Abs(Math.Abs(c.Deck.Volume()) / c.Deck.ExactVolume()!.Value - 1) < 0.002);
        AssertClosed(doc, "skew-kinks", "3D-DECK");
    }

    [Fact]
    public void KinkedEnd_BreakPointLeavingTheBlock_BlockoutAndEndBlockAreClosedSolids()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        doc.Regenerate();
        var al = doc.Alignment(b.AlignmentName);
        var dr = doc.Model().Framing.Deck!;
        var o = dr.BeginLinePoint; var t = dr.BeginLineDirection; var n = t.Left;
        // the right edge where the straight end crosses it, a break point 0.05 m inside it, the last piece turning back away from the deck at 60 deg:
        // the inset end's break point moves right (~0.3 m per m of inset) and leaves the 25 in. blockout across its width at x ~ 0.17 m
        // (at 0.2 m inside it stayed inside the blockout: it left only in the end block)
        // the point of the straight end line 0.05 m (in offset) inside the right edge: solved along the end line (on a skewed end the edge's projection
        // onto the end line is not where the edge crosses it)
        double sa = 0, sb = 60;
        double Off(double sp) => al.Inverse(o + t * sp).Offset - (b.Deck.RightEdgeOffset - 0.05);
        for (int it = 0; it < 80; it++) { double sm = (sa + sb) / 2; if (Off(sa) * Off(sm) <= 0) sb = sm; else sa = sm; }
        var v = o + t * ((sa + sb) / 2);
        b.Supports[0].DeckEndPoints = new[] { o - t * 40, v, v + (t * 0.5 - n * 0.866) * 20 }
            .Select(q => { var so = al.Inverse(q); return new SBM.Core.Bridge.DeckEndPoint(so.Station, so.Offset); }).ToList();
        b.Supports[0].ExpansionJoint = new SBM.Core.Bridge.ExpansionJointDefinition
        {
            Type = SBM.Core.Bridge.ExpansionJointType.Modular, BlockoutWidth = In(25), GapAt50F = In(9), BlockoutDepth = Ft(1) + In(2),
            EndBlock = new SBM.Core.Bridge.DeckEndBlockDefinition { Depth = Ft(2) + In(2), FlatLength = Ft(3) + In(4.5) },
        };
        doc.Project.Geometry.ExactCurvedGeometry = true;
        doc.Regenerate();
        Assert.False(doc.Model().Framing.HasErrors);
        var j = Assert.Single(doc.Concrete().Joints);
        Assert.True(j.Blockout.HasExactNurbs, doc.Concrete().Nurbs?.Summary());
        Assert.True(j.EndBlock!.Mesh.HasExactNurbs, doc.Concrete().Nurbs?.Summary());
        Assert.True(j.Blockout.Nurbs!.Count >= 2, "the blockout is split where the break point leaves it");
        AssertClosed(doc, "kink-leaves", "3D-JOINTS|3D-DECK");
    }

    [Fact]
    public void ConcreteDiaphragms_OnASkew_AreClosedRuledSolids()
    {
        var doc = ConcreteDiaphragmTests.Flat(spans: 2, skew: 25);
        doc.Regenerate();
        double zb = doc.Prestressed().Beams[0].BearingStart.Z - doc.Prestressed().Shape.Depth - In(1);
        var sec = new List<SBM.Core.Bridge.DiaphragmSectionPoint>
        {
            new(-Ft(1.5), SBM.Core.Bridge.DiaphragmZRef.Elevation, zb), new(Ft(1.5), SBM.Core.Bridge.DiaphragmZRef.Elevation, zb),
            new(Ft(1.5), SBM.Core.Bridge.DiaphragmZRef.Soffit), new(-Ft(1.5), SBM.Core.Bridge.DiaphragmZRef.Soffit),
        };
        doc.Bridge().Supports[1].Diaphragm = new SBM.Core.Bridge.ConcreteDiaphragmDefinition
        {
            Kind = SBM.Core.Bridge.ConcreteDiaphragmKind.PierContinuity,
            Parts = { new SBM.Core.Bridge.DiaphragmPart { Name = "pier", Section = sec } },
            Left = SBM.Core.Bridge.DiaphragmEnd.AtOffset(-Ft(9)), Right = SBM.Core.Bridge.DiaphragmEnd.Fascia(),
        };
        doc.Project.Geometry.ExactCurvedGeometry = true;
        doc.Regenerate();
        var d = Assert.Single(doc.Prestressed().ConcreteDiaphragms!.Diaphragms);
        Assert.True(d.Mesh.HasExactNurbs);
        Assert.True(Math.Abs(d.Mesh.ExactVolume()!.Value / d.Volume - 1) < 1e-4);
        var objs = AssertClosed(doc, "diaphragm", "3D-DIAPHRAGMS");
        Assert.Equal(d.Mesh.Nurbs!.Count, objs.Count);
        // OFF: no exact records (the meshes are the same)
        doc.Project.Geometry.ExactCurvedGeometry = false;
        doc.Regenerate();
        var d0 = Assert.Single(doc.Prestressed().ConcreteDiaphragms!.Diaphragms);
        Assert.Null(d0.Mesh.Nurbs);
        Assert.Equal(d.Mesh.TriangleCount, d0.Mesh.TriangleCount);
    }

    [Fact]
    public void TubGirderHaunches_AreClosedNurbsSweeps()
    {
        var (p, _, _) = TubGirderTests.Model();
        p.Geometry.ExactCurvedGeometry = true;
        var doc = SbmDocument.Attach(p);
        doc.Regenerate();
        var h = doc.Concrete().Haunches!;
        Assert.True(h.HasExactNurbs, doc.Concrete().Nurbs?.Summary());
        Assert.Equal(4, h.Nurbs!.Count);              // two tubs, a haunch over each top flange
        AssertClosed(doc, "tub", "3D-DECK");
    }

    [Fact]
    public void ArchDeck_SlabKnuckleZonesAndCurbs_AreClosedSweeps()
    {
        var (p, _) = ArchTests.Model();
        p.Geometry.ExactCurvedGeometry = true;
        var doc = SbmDocument.Attach(p);
        doc.Regenerate();
        var c = doc.Concrete();
        Assert.True(c.Deck!.HasExactNurbs);
        AssertClosed(doc, "arch", "3D-DECK");
    }
}
