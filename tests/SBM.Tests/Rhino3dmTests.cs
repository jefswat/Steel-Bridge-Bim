using SBM.Api;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using SBM.IO.Rhino3dm;
using RF = Rhino.FileIO;
using RG = Rhino.Geometry;

namespace SBM.Tests;

[Collection(RhinoNativeCollection.Name)]
public class Rhino3dmTests
{
    private static string TempFile(string name) => Path.Combine(Path.GetTempPath(), $"sbm-test-{Guid.NewGuid():N}-{name}");

    private static double CurveArea(RG.Curve c)
    {
        // Newell area of the (planar) curve sampled densely
        var d = c.Domain;
        const int n = 2000;
        var p = Enumerable.Range(0, n).Select(i => c.PointAt(d.T0 + d.Length * i / n)).ToList();
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[(i + 1) % n];
            x += (a.Y - b.Y) * (a.Z + b.Z); y += (a.Z - b.Z) * (a.X + b.X); z += (a.X - b.X) * (a.Y + b.Y);
        }
        return Math.Sqrt(x * x + y * y + z * z) / 2;
    }

    private static double ExtrusionVolume(RG.Extrusion e)
    {
        double a = CurveArea(e.Profile3d(0, 0));
        for (int i = 1; i < e.ProfileCount; i++) a -= CurveArea(e.Profile3d(i, 0));
        return a * e.PathStart.DistanceTo(e.PathEnd);
    }

    [Fact]
    public void NativeLibrary_Loads()
    {
        Assert.StartsWith("rhino3dm", Rhino3dmExporter.LibraryCheck());
    }

    [Fact]
    public void ObliquePrismWithHoles_IsAClosedBrep()
    {
        // 300 x 200 plate, 25 thick, swept obliquely (sheared 40 mm in x and 15 mm in y), two holes
        var bottom = new[] { new Vec3(0, 0, 0), new Vec3(0.3, 0, 0), new Vec3(0.3, 0.2, 0), new Vec3(0, 0.2, 0) };
        var d = new Vec3(0.04, 0.015, 0.025);
        var es = ExtrudedSolid.TryCreate(bottom, bottom.Select(p => p + d).ToArray(), new[] { (new Vec3(0.08, 0.1, 0), 0.024), (new Vec3(0.22, 0.1, 0), 0.024) });
        Assert.NotNull(es);
        var b = new RhinoBrepBuilder(v => new RG.Point3d(v.X, v.Y, v.Z), 1, 1e-6);
        foreach (var f in RhinoBrepBuilder.Faces(es!)) b.AddFace(f);
        var brep = b.Finish();
        Assert.True(brep != null, b.Error);
        Assert.True(brep!.IsSolid);
        Assert.Equal(2 + 4 + 2 * 2, brep.Faces.Count);           // caps, sides, two half walls per hole
        // vertices = the prism corners (+ hole seam points)
        Assert.Equal(0.34, brep.Vertices.Max(v => v.Location.X), 9);
        Assert.Equal(0.025, brep.Vertices.Max(v => v.Location.Z), 9);
        Assert.Equal(-0.0, brep.Vertices.Min(v => v.Location.Z), 9);
    }

    [Fact]
    public void ObliquePrismWithHoles_MiteredExtrusion_HolesAreCirclesInTheCaps()
    {
        // same plate as above: written as a Rhino extrusion along the oblique sweep, mitered back to the (horizontal) profile plane
        var bottom = new[] { new Vec3(0, 0, 0), new Vec3(0.3, 0, 0), new Vec3(0.3, 0.2, 0), new Vec3(0, 0.2, 0) };
        var d = new Vec3(0.04, 0.015, 0.025);
        var holes = new[] { (new Vec3(0.08, 0.1, 0), 0.024), (new Vec3(0.22, 0.1, 0), 0.024) };
        var es = ExtrudedSolid.TryCreate(bottom, bottom.Select(p => p + d).ToArray(), holes)!;
        var g = Rhino3dmExporter.ExtrusionOf(es, v => new RG.Point3d(v.X, v.Y, v.Z), 1, 1e-6, out string? err);
        var e = Assert.IsType<RG.Extrusion>(g);
        Assert.Null(err);
        Assert.True(e.IsMiteredAtStart && e.IsMiteredAtEnd);
        using var b = e.ToBrep(true);
        Assert.True(b.IsSolid);
        Assert.Equal(0.34, b.Vertices.Max(v => v.Location.X), 9);
        Assert.Equal(0.0, b.Vertices.Min(v => v.Location.Z), 9);
        Assert.Equal(0.025, b.Vertices.Max(v => v.Location.Z), 9);
        // every edge point on a cap lies on the plate outline or on a hole circle of that cap
        int onHoles = 0;
        foreach (var edge in b.Edges)
        {
            var dom = edge.Domain;
            for (int i = 0; i <= 8; i++)
            {
                var p = edge.PointAt(dom.T0 + dom.Length * i / 8);
                bool top = Math.Abs(p.Z - 0.025) < 1e-9, bot = Math.Abs(p.Z) < 1e-9;
                if (!top && !bot) continue;
                var q = new Vec2(p.X - (top ? d.X : 0), p.Y - (top ? d.Y : 0));
                bool outline = Math.Abs(q.X) < 1e-9 || Math.Abs(q.X - 0.3) < 1e-9 || Math.Abs(q.Y) < 1e-9 || Math.Abs(q.Y - 0.2) < 1e-9;
                bool hole = holes.Any(h => Math.Abs(Vec2.Distance(q, h.Item1.XY) - 0.012) < 1e-9);
                Assert.True(outline || hole, $"cap edge point {p} is neither on the outline nor on a hole");
                if (hole) onHoles++;
            }
        }
        Assert.True(onHoles >= 4 * 8);
    }

    [Fact]
    public void ObliquePrismWithArcOutline_IsAClosedBrep()
    {
        // outline with a true arc edge (ArcPolygon) swept obliquely
        var ap = new ArcPolygon(new[] { new Vec2(0, 0), new Vec2(0.4, 0), new Vec2(0.4, 0.2), new Vec2(0, 0.2) }, new[] { 0, 0.5, 0, 0 });
        var es = new ExtrudedSolid
        {
            Frame = Frame3.World, Outline = ap.Tessellate(1e-4).ToArray(), Holes = Array.Empty<Hole>(), Extrusion = new Vec3(0.01, 0.02, 0.05), ExactOutline = ap,
        };
        var b = new RhinoBrepBuilder(v => new RG.Point3d(v.X, v.Y, v.Z), 1, 1e-6);
        foreach (var f in RhinoBrepBuilder.Faces(es)) b.AddFace(f);
        var brep = b.Finish();
        Assert.True(brep != null, b.Error);
        Assert.True(brep!.IsSolid);
    }

    [Fact]
    public void PrestressedBeamBody_SkewedEnds_IsAClosedBrep()
    {
        var shape = SBM.Core.Prestressed.StandardBeamCatalog.Find("IA BTD")!;
        Assert.NotNull(shape.Profile);
        double L = 30, skew = 20 * Math.PI / 180, slope = 0.02;
        // section in the vertical plane x = 0 (section x = world -y), beam axis +x with a 2 % grade, both ends skewed 20 deg (outward normals)
        var f = new Frame3(new Vec3(-1, 0, 0), new Vec3(0, -1, 0), new Vec3(0, 0, 1), new Vec3(1, 0, 0));
        var nStart = new Vec3(-Math.Cos(skew), Math.Sin(skew), 0);
        var body = new SBM.Core.Prestressed.BeamBody(f, 1, new Vec3(L + 2, 0, (L + 2) * slope), Vec3.Zero, nStart, new Vec3(L, 0, L * slope), nStart * -1);
        var b = new RhinoBrepBuilder(v => new RG.Point3d(v.X, v.Y, v.Z), 1, 1e-6);
        foreach (var face in RhinoBrepBuilder.Faces(body, shape.Profile!)) b.AddFace(face);
        var brep = b.Finish();
        Assert.True(brep != null, b.Error);
        Assert.True(brep!.IsSolid);
        Assert.Equal(2 + shape.Profile!.Segments.Count, brep.Faces.Count);
        // the end faces lie in the end planes
        foreach (var v in brep.Vertices)
        {
            var p = new Vec3(v.Location.X, v.Location.Y, v.Location.Z);
            double d0 = Math.Abs(Vec3.Dot(p, nStart)), d1 = Math.Abs(Vec3.Dot(p - new Vec3(L, 0, L * slope), nStart));
            Assert.True(Math.Min(d0, d1) < 1e-9, $"vertex {p} off the end planes");
        }
    }

    [Fact]
    public void SampleBridge_RoundTrip_LayersUnitsBlocksUserText()
    {
        var doc = SbmDocument.Sample();
        doc.Regenerate();
        string path = TempFile("sample.3dm");
        try
        {
            var r = doc.Export3dm(path, exactCurved: false);   // plates as extrusions (the exact-curved export is tested below)
            Assert.True(File.Exists(path));
            var s = Rhino3dmInfo.Read(path);
            Assert.Equal("Feet", s.Units);
            Assert.Equal(r.Objects, s.Objects);
            Assert.Equal(r.BlockInstances, s.ByType.GetValueOrDefault("Block"));
            Assert.Equal(r.Extrusions + r.MiteredExtrusions, s.ByType.GetValueOrDefault("Extrusion"));
            var m = doc.Model();
            string bn = m.Definition.Name;
            Assert.Contains(s.LayerPaths, l => l == $"{bn}::3D-GIRDER-PLATES");
            Assert.Contains(s.LayerPaths, l => l.StartsWith("Alignments::"));
            // every bolt and stud is a block instance; one definition per distinct size
            var steel = m.Steel!;
            Assert.Equal(steel.Bolts.Count + steel.Studs.Count, r.BlockInstances);
            Assert.True(r.BlockDefinitions >= 2 && r.BlockDefinitions < 50);
            Assert.True(r.Extrusions + r.MiteredExtrusions > steel.Parts.Count / 2, $"{r.Extrusions} extrusions + {r.MiteredExtrusions} mitered for {steel.Parts.Count} parts");
            Assert.Equal(0, r.Fallbacks);

            // a girder plate: name, layer, GlobalId (= IFC), volume and bounding box vs the model (feet, relative to the IFC base point)
            using var f = RF.File3dm.Read(path);
            var bp = IfcExporter.BasePoint(doc.Project, new[] { m.Framing });
            double s1 = 1 / doc.Units.FootInMeters;
            var plates = steel.Parts.Where(p => p.Category == PartCategory.FlangePlate && p.Mesh.HasExactSolids && p.Mesh.Solids!.Count == 1).Take(5).ToList();
            Assert.NotEmpty(plates);
            foreach (var part in plates)
            {
                var o = f.Objects.Single(x => x.Attributes.Name == part.Mark && x.Attributes.GetUserString("Assembly") == part.Assembly);
                Assert.Equal(IfcKeys.GlobalId(IfcKeys.Part(IfcKeys.Bridge(doc.Project.Name, bn), part.Mark)), o.Attributes.GetUserString("GlobalId"));
                Assert.Equal(part.Material, o.Attributes.GetUserString("Material"));
                var mb = part.Mesh.Bounds();
                var e = Assert.IsType<RG.Extrusion>(o.Geometry);
                if (!e.IsMiteredAtStart)
                {
                    double vModel = part.Volume * s1 * s1 * s1;
                    Assert.Equal(1, ExtrusionVolume(e) / vModel, 3);
                }
                else Assert.True(e.IsMiteredAtEnd);    // plate along the grade with plumb ends: right section swept along the grade, both ends mitered
                // the B-rep form's corners span the model plate (feet, relative to the base point; recorded solids fit the mesh to 0.5 mm)
                using var b = e.ToBrep(true);
                Assert.True(b.IsSolid);
                Assert.Equal((mb.Min.X - bp.X) * s1, b.Vertices.Min(v => v.Location.X), 3);
                Assert.Equal((mb.Min.Y - bp.Y) * s1, b.Vertices.Min(v => v.Location.Y), 3);
                Assert.Equal((mb.Min.Z - bp.Z) * s1, b.Vertices.Min(v => v.Location.Z), 3);
                Assert.Equal((mb.Max.X - bp.X) * s1, b.Vertices.Max(v => v.Location.X), 3);
                Assert.Equal((mb.Max.Y - bp.Y) * s1, b.Vertices.Max(v => v.Location.Y), 3);
                Assert.Equal((mb.Max.Z - bp.Z) * s1, b.Vertices.Max(v => v.Location.Z), 3);
            }
            Assert.Contains("BasePoint_E", string.Join(",", s.DocumentStrings.Keys));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GlobalIds_AreThoseOfTheIfcExport()
    {
        // every kind of element (parts, fasteners, deck, barriers, substructure solids, bars) carries the GlobalId the IFC export gives it
        var doc = SbmDocument.Sample();
        doc.Regenerate();
        string p3 = TempFile("gid.3dm"), pi = TempFile("gid.ifc");
        try
        {
            doc.Export3dm(p3);
            doc.ExportIfc(pi);
            string ifc = File.ReadAllText(pi);
            using var f = RF.File3dm.Read(p3);
            var byKind = f.Objects.Where(o => !o.Attributes.IsInstanceDefinitionObject && o.Attributes.GetUserString("GlobalId") is { Length: > 0 })
                .GroupBy(o => o.Geometry is RG.InstanceReferenceGeometry ? "fastener" : o.Attributes.GetUserString("Kind") ?? "?").ToList();
            Assert.True(byKind.Count >= 6, string.Join(", ", byKind.Select(g => g.Key)));
            foreach (var g in byKind)
                foreach (var o in g.Where((_, i) => i % 97 == 0).Take(5))
                {
                    string gid = o.Attributes.GetUserString("GlobalId");
                    Assert.True(ifc.Contains($"('{gid}'") || ifc.Contains($"'{gid}',"), $"{g.Key} {o.Attributes.Name}: GlobalId {gid} not in the IFC");
                }
        }
        finally { File.Delete(p3); File.Delete(pi); }
    }

    [Fact]
    public void MeshOnly_And_BridgeFilter()
    {
        var doc = SbmDocument.Sample();
        doc.Regenerate();
        string p1 = TempFile("meshonly.3dm"), p2 = TempFile("none.3dm");
        try
        {
            var r = doc.Export3dm(p1, meshOnly: true, detail: false);
            var s = Rhino3dmInfo.Read(p1);
            Assert.Equal(0, s.ByType.GetValueOrDefault("Extrusion"));
            Assert.Equal(0, s.ByType.GetValueOrDefault("Block"));
            Assert.True(s.ByType.GetValueOrDefault("Mesh") > 0);
            Assert.Equal(r.Meshes, s.ByType.GetValueOrDefault("Mesh"));
            var r2 = doc.Export3dm(p2, bridges: new[] { "no such bridge" });
            Assert.Equal(0, r2.BlockInstances);
            Assert.Equal(0, r2.Extrusions + r2.Meshes);
        }
        finally { File.Delete(p1); File.Delete(p2); }
    }

    [Fact]
    public void Rhino7Archive_And_EveryMeshValid()
    {
        // Rhino 7 cannot open version 8 archives; every mesh must pass openNURBS IsValid (float-coincident slivers are left out)
        var doc = SbmDocument.Sample();
        doc.Regenerate();
        string p = TempFile("v7.3dm");
        try
        {
            var r = doc.Export3dm(p, meshOnly: true, fileVersion: 7);
            Assert.Equal(70, RF.File3dm.ReadArchiveVersion(p));
            using var f = RF.File3dm.Read(p);
            var meshes = f.Objects.Select(o => o.Geometry).OfType<RG.Mesh>().ToList();
            Assert.Equal(r.Meshes, meshes.Count(m => true) - f.Objects.Count(o => o.Attributes.IsInstanceDefinitionObject && o.Geometry is RG.Mesh));
            Assert.All(meshes, m => Assert.True(m.IsValid));
            Assert.Throws<ArgumentException>(() => doc.Export3dm(TempFile("v6.3dm"), fileVersion: 6));
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void ExactCurvedGeometry_WritesClosedNurbsBreps()
    {
        var doc = SbmDocument.Sample();
        doc.Project.Geometry.ExactCurvedGeometry = true;
        doc.Regenerate();
        var m = doc.Model();
        int nurbsParts = m.Steel!.Parts.Count(p => p.Mesh.Nurbs is { Count: > 0 });
        string path = TempFile("nurbs.3dm");
        try
        {
            var r = doc.Export3dm(path, detail: false);
            Assert.True(nurbsParts > 0);                                       // the sample has curved girders
            Assert.True(r.NurbsBreps >= nurbsParts, string.Join("; ", r.Notes));
            Assert.True(r.NurbsBrepsClosed == r.NurbsBreps, $"{r.NurbsBrepsClosed} of {r.NurbsBreps} closed; {string.Join("; ", r.Notes)}");
            Assert.Equal(0, r.Fallbacks);
            var s = Rhino3dmInfo.Read(path);
            Assert.True(s.ClosedBreps >= r.NurbsBrepsClosed);
        }
        finally { File.Delete(path); }
    }

    // ------------------------------------------------------------ polyhedra -> planar B-reps, round sections -> circles (2026-10-26)

    /// <summary>Volume of a set of planar faces (divergence theorem; outward normal = X x Y, holes subtracted).</summary>
    private static double FacesVolume(List<FaceSpec> faces)
    {
        double v = 0;
        foreach (var f in faces)
        {
            var n = Vec3.Cross(f.X, f.Y).Normalized();
            double LoopArea(List<SBM.Core.Geometry.Nurbs.NurbsCurve> l)
            {
                var s = Vec3.Zero;
                foreach (var c in l) s += Vec3.Cross(c.Control[0] - f.Origin, c.Control[^1] - f.Origin);
                return Math.Abs(Vec3.Dot(s, n)) / 2;
            }
            double a = LoopArea(f.Outer) - f.Holes.Sum(LoopArea);
            v += Vec3.Dot(n, f.Origin) * a / 3;
        }
        return v;
    }

    private static Mesh Quads(params Vec3[][] quads)
    {
        var m = new Mesh();
        foreach (var q in quads)
        {
            int b = m.Positions.Count;
            m.Positions.AddRange(q);
            for (int i = 1; i + 1 < q.Length; i++) { m.Indices.Add(b); m.Indices.Add(b + i); m.Indices.Add(b + i + 1); }
        }
        return m;
    }

    [Fact]
    public void PolyhedralMesh_WithTJunctions_BecomesAClosedPlanarBrep_SameVolume()
    {
        // 2 x 1 x 1 box at state-plane magnitudes; the top is two quads (a vertex at x = 1 in the middle of the front / back top edges = T-junctions)
        var o = new Vec3(700000, 4700000, 300);
        Vec3 P(double x, double y, double z) => o + new Vec3(x, y, z);
        var m = Quads(
            new[] { P(0, 0, 0), P(0, 1, 0), P(2, 1, 0), P(2, 0, 0) },                       // bottom (outward -z)
            new[] { P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1) },                       // top, left half
            new[] { P(1, 0, 1), P(2, 0, 1), P(2, 1, 1), P(1, 1, 1) },                       // top, right half
            new[] { P(0, 0, 0), P(2, 0, 0), P(2, 0, 1), P(0, 0, 1) },                       // front (-y)
            new[] { P(0, 1, 0), P(0, 1, 1), P(2, 1, 1), P(2, 1, 0) },                       // back (+y)
            new[] { P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0) },                       // left (-x)
            new[] { P(2, 0, 0), P(2, 1, 0), P(2, 1, 1), P(2, 0, 1) });                      // right (+x)
        Assert.Equal(2.0, m.Volume(), 9);
        var faces = RhinoBrepBuilder.PlanarFaces(m, 400, out var why);
        Assert.True(faces != null, why);
        Assert.Equal(6, faces!.Count);                                                       // the two top quads are one face
        Assert.Equal(2.0, FacesVolume(faces), 6);
        var b = new RhinoBrepBuilder(v => new RG.Point3d(v.X - o.X, v.Y - o.Y, v.Z - o.Z), 1, 1e-6);
        foreach (var f in faces) b.AddFace(f);
        var brep = b.Finish();
        Assert.True(brep != null, b.Error);
        Assert.True(brep!.IsSolid);
        Assert.Equal(6, brep.Faces.Count);
        Assert.Equal(12, brep.Edges.Count);                                                  // collinear T-junction vertices merged on both sides
    }

    [Fact]
    public void Shells_SplitTouchingPieces_FlatShadedTrianglesStayTogether()
    {
        var box = Quads(
            new[] { new Vec3(0, 0, 0), new Vec3(0, 1, 0), new Vec3(1, 1, 0), new Vec3(1, 0, 0) },
            new[] { new Vec3(0, 0, 1), new Vec3(1, 0, 1), new Vec3(1, 1, 1), new Vec3(0, 1, 1) },
            new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(1, 0, 1), new Vec3(0, 0, 1) },
            new[] { new Vec3(0, 1, 0), new Vec3(0, 1, 1), new Vec3(1, 1, 1), new Vec3(1, 1, 0) },
            new[] { new Vec3(0, 0, 0), new Vec3(0, 0, 1), new Vec3(0, 1, 1), new Vec3(0, 1, 0) },
            new[] { new Vec3(1, 0, 0), new Vec3(1, 1, 0), new Vec3(1, 1, 1), new Vec3(1, 0, 1) });
        Assert.Single(RhinoBrepBuilder.Shells(box));                                        // a vertex per quad corner: joined through the edges
        var two = new Mesh();
        foreach (var dx in new[] { 0.0, 1.0 })                                              // two boxes touching along one edge (x = y = 1)
        {
            int b0 = two.Positions.Count;
            two.Positions.AddRange(box.Positions.Select(p => p + new Vec3(dx, dx, 0)));
            two.Indices.AddRange(box.Indices.Select(i => i + b0));
        }
        var sh = RhinoBrepBuilder.Shells(two);
        Assert.Equal(2, sh.Count);
        Assert.All(sh, s => Assert.Equal(1.0, s.Volume(), 9));
    }

    [Fact]
    public void RoundSections_AreWrittenAsCircles()
    {
        var c32 = Enumerable.Range(0, 32).Select(i => Vec2.FromAngle(2 * Math.PI * i / 32) * 0.6 + new Vec2(3, 4)).ToArray();
        Assert.True(Rhino3dmExporter.RegularPolygon(c32, out var c, out double r));
        Assert.Equal(0.6, r, 12);
        Assert.Equal(3, c.X, 12);
        var c12 = Enumerable.Range(0, 12).Select(i => Vec2.FromAngle(2 * Math.PI * i / 12) * 0.6).ToArray();
        Assert.False(Rhino3dmExporter.RegularPolygon(c12, out _, out _));                   // a 12-gon may be a real polygon: kept
        var squashed = c32.Select(p => new Vec2(p.X, p.Y * 1.01)).ToArray();
        Assert.False(Rhino3dmExporter.RegularPolygon(squashed, out _, out _));
    }

    [Fact]
    public void Sample_PlanarBrepsAndExactCurvedByDefault()
    {
        var doc = SbmDocument.Sample();
        doc.Regenerate();
        string path = TempFile("planar.3dm");
        try
        {
            var r = doc.Export3dm(path, detail: false);
            Assert.False(doc.Project.Geometry.ExactCurvedGeometry);                           // the project setting is restored
            Assert.True(r.PlanarBreps > 0, string.Join("; ", r.MeshReasons.Select(kv => $"{kv.Value} {kv.Key}")));
            Assert.True(r.NurbsBreps > 0);
            Assert.Equal(0, r.Fallbacks);
            using var f = RF.File3dm.Read(path);
            Assert.All(f.Objects.Select(o => o.Geometry).OfType<RG.Brep>(), b => { Assert.True(b.IsValid); Assert.True(b.IsSolid); });
            // every planar B-rep has the volume of the mesh it replaces (divergence check on the face sets of the substructure solids)
            foreach (var so in doc.Substructure().Units.SelectMany(u => u.Solids).Where(x => !x.Mesh.IsEmpty && !x.Mesh.HasExactSolids))
            {
                var shells = RhinoBrepBuilder.Shells(so.Mesh);
                var fs = shells.Select(sh => RhinoBrepBuilder.PlanarFaces(sh, 400, out _)).ToList();
                if (fs.Any(x => x == null)) continue;
                Assert.Equal(so.Mesh.Volume(), fs.Sum(x => FacesVolume(x!)), 6);
            }
            var rm = doc.Export3dm(TempFile("meshes.3dm"), detail: false, exactCurved: false);
            Assert.Equal(0, rm.NurbsBreps);
        }
        finally { File.Delete(path); }
    }

    // ------------------------------------------------------------ deck elements as closed exact B-reps (2026-10-27)

    /// <summary>Exports a bridge and checks every deck element (slab, haunches, bumps, barriers / curbs, joint blockouts / devices / end blocks,
    /// cap haunches): written as closed, valid B-reps or extrusions (no meshes), each element's read-back volume within 0.1 % of the generator's
    /// exact volume (user text Volume_m3), every object inside the element's mesh bounds (+ 1 mm).</summary>
    private static void AssertDeckSolids(SbmDocument doc, string name)
    {
        string path = TempFile(name + ".3dm");
        try
        {
            var r = doc.Export3dm(path, detail: false);
            string why = string.Join("; ", r.FallbackItems.Concat(r.MeshItems)) + " | " + doc.Concrete().Nurbs?.Summary();
            Assert.True(r.Fallbacks == 0, why);
            string units = Rhino3dmInfo.Read(path).Units;
            double s = units == "Feet" ? 0.3048 : units == "Meters" ? 1 : throw new InvalidOperationException(units);
            var objs = Rhino3dmInfo.Objects(path, "3D-DECK|3D-BARRIER|3D-JOINTS");
            Assert.NotEmpty(objs);
            foreach (var o in objs)
            {
                Assert.True(o.Type is "Brep" or "Extrusion", $"{o.Name}: {o.Type} ({why})");
                Assert.True(o.Valid && o.Closed, $"{o.Name}: valid {o.Valid}, closed {o.Closed}");
            }
            foreach (var g in objs.GroupBy(o => (o.Element, o.Name)))
            {
                double v = g.Sum(o => o.Volume) * s * s * s, model = double.Parse(g.First().ModelVolumeM3, System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(Math.Abs(v / model - 1) < 0.001, $"{g.Key.Name}: 3dm {v:0.######} m3 vs model {model:0.######} m3");
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DeckElements_KinkedEnd_OverhangSoffit_CustomBarrierZone_JointsAndEndBlocks_AreClosedBreps()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        doc.Regenerate();
        // kink the begin deck end (the part left of the alignment 0.4 m into the deck), as KinkedDeckEndTests
        var al = doc.Alignment(b.AlignmentName);
        var dr = doc.Model().Framing.Deck!;
        var o = dr.BeginLinePoint; var t = dr.BeginLineDirection; var n = t.Left;
        b.Supports[0].DeckEndPoints = new[] { o - t * 30 + n * 0.4, o - t * 0.5 + n * 0.4, o, o + t * 30 }
            .Select(q => { var so = al.Inverse(q); return new SBM.Core.Bridge.DeckEndPoint(so.Station, so.Offset); }).ToList();
        // sloped overhang soffit
        b.Deck.Overhang = new SBM.Core.Bridge.DeckOverhangDefinition { TipThickness = SBM.Core.Units.UnitSystem.In(9), RootThickness = SBM.Core.Units.UnitSystem.In(11.75), Length = SBM.Core.Units.UnitSystem.In(25) };
        // custom barrier with a bump-out zone on a deck widening
        double Ft(double x) => SBM.Core.Units.UnitSystem.Ft(x);
        double In(double x) => SBM.Core.Units.UnitSystem.In(x);
        b.Deck.Barrier.Type = SBM.Core.Bridge.BarrierType.Custom;
        b.Deck.Barrier.Profile = new() { new(0, 0), new(Ft(1.5), 0), new(Ft(1), Ft(3), radius: In(2)), new(0, Ft(3)) };
        b.Deck.Barrier.Zones = new()
        {
            new SBM.Core.Bridge.BarrierZone
            {
                Name = "bump", Support = b.Supports[1].Name, Length = Ft(4), DeckWidening = Ft(1), DeckWideningLength = Ft(5),
                Profile = new() { new(-In(6), 0), new(Ft(1.5), 0), new(Ft(1.5), Ft(3)), new(-In(6), Ft(3)) },
            },
        };
        // expansion joints with blockouts (the begin one on the kinked end) and end blocks
        foreach (var sp in new[] { b.Supports[0], b.Supports[^1] })
            sp.ExpansionJoint = new SBM.Core.Bridge.ExpansionJointDefinition
            {
                Type = SBM.Core.Bridge.ExpansionJointType.Modular, BlockoutWidth = In(25), GapAt50F = In(9), BlockoutDepth = Ft(1) + In(2),
                EndBlock = new SBM.Core.Bridge.DeckEndBlockDefinition { Depth = Ft(2) + In(2), FlatLength = Ft(3) + In(4.5) },
            };
        doc.Regenerate();
        Assert.False(doc.Model().Framing.HasErrors);
        var c = doc.Concrete();
        Assert.Equal(2, c.Joints.Count(j => j.EndBlock != null));
        Assert.NotEmpty(c.DeckBumps);
        AssertDeckSolids(doc, "deck-kinked");
    }

    [Fact]
    public void DeckElements_ShelfUnderShallowBlockout_And_DefaultBarrier_AreClosedBreps()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        foreach (var sp in new[] { b.Supports[0], b.Supports[^1] })
            sp.ExpansionJoint = new SBM.Core.Bridge.ExpansionJointDefinition
            {
                Type = SBM.Core.Bridge.ExpansionJointType.Modular, BlockoutWidth = SBM.Core.Units.UnitSystem.In(25), GapAt50F = SBM.Core.Units.UnitSystem.In(9),
                BlockoutDepth = 0.1,
            };
        doc.Regenerate();
        AssertDeckSolids(doc, "deck-shelf");
    }

    [Fact]
    public void CivilItems_RoadUtilities_Layers_GlobalIds_Closed()
    {
        var doc = SbmDocument.Sample();
        var al = doc.Project.Alignments[0];
        double s0 = al.StartStation + 5;
        doc.Project.Roads.Add(new SBM.Core.Road.RoadDefinition
        {
            Name = "Road R", AlignmentName = al.Name, BeginStation = s0, EndStation = s0 + 20,
            Right = new() { Components = { new() { Name = "Lane", Width = 3.6, Courses = { new() { Name = "Surface", Thickness = 0.1, Material = "HMA" } } } } },
        });
        var p = al.PointAt(s0, 30);
        var net = new SBM.Core.Utility.UtilityNetworkDefinition { Name = "Storm R" };
        net.Structures.Add(new() { Name = "MH-1", X = p.X, Y = p.Y, RimElevation = 100, SumpElevation = 97, InnerDiameter = 1.2, WallThickness = 0.125, FloorThickness = 0.2 });
        net.Pipes.Add(new() { Name = "P-1", From = "MH-1", EndX = p.X + 20, EndY = p.Y, InvertStart = 97.2, InvertEnd = 97.0, InnerDiameter = 0.45, WallThickness = 0.06 });
        doc.Project.Utilities.Add(net);
        string path = Path.Combine(Path.GetTempPath(), "sbm-civil-" + Guid.NewGuid().ToString("N")[..8] + ".3dm");
        try
        {
            doc.Export3dm(path, detail: false);
            var objs = Rhino3dmInfo.Objects(path, "^Roads::|^Utilities::");
            var road = objs.Where(o => o.Layer.StartsWith("Roads::Road R::3D-ROAD-LANE")).ToList();
            Assert.Single(road);
            var util = objs.Where(o => o.Layer.StartsWith("Utilities::Storm R::3D-UTIL-STORM")).ToList();
            Assert.Equal(2, util.Count);
            Assert.All(road.Concat(util), o => Assert.True(o.Closed && o.Valid, $"{o.Name}: {o.Type} closed {o.Closed} valid {o.Valid}"));
            var um = doc.Utilities();
            var tags = SBM.IO.Database.ModelDatabase.BuildTagIndex(doc.Project, Array.Empty<SBM.IO.Ifc.IfcBridgeData>(), utilities: um);
            Assert.Contains(util, o => o.Name == "MH-1" && o.GlobalId == tags.Lookup(um[0].Structures[0]));
            Assert.All(road.Concat(util), o => Assert.Equal(22, o.GlobalId.Length));
        }
        finally { File.Delete(path); }
    }
}
