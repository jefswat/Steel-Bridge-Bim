using SBM.Api;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using SBM.IO.Rhino3dm;
using RF = Rhino.FileIO;
using RG = Rhino.Geometry;

namespace SBM.Tests;

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
            var r = doc.Export3dm(path);
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
}
