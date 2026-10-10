using SBM.Api;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.References;
using SBM.Core.Solids;
using SBM.Core.Units;
using SBM.IO.LandXml;
using SBM.IO.References;
using Xunit;

namespace SBM.Tests;

public class ReferenceAndApiTests
{
    private static string Temp(string ext) => Path.Combine(Path.GetTempPath(), $"sbm-test-{Guid.NewGuid():N}{ext}");

    [Fact]
    public void LandXmlSurface_RoundTrip_AndElevationQueries()
    {
        var a = SampleProject.CreateAlignment();
        var tin = SampleProject.CreateExistingGround(a, 10);
        string path = Temp(".xml");
        try
        {
            LandXmlWriter.WriteSurface(path, "EG", tin, new UnitSystem());
            var g = ReferenceReaders.ReadLandXmlSurface(path);
            Assert.Equal(tin.Positions.Count, g.Mesh.Positions.Count);
            Assert.Equal(tin.TriangleCount, g.Mesh.TriangleCount);
            var terrain = new TerrainModel(g.Mesh);
            // Vertices are written to 0.001 ft; queries at vertex locations reproduce elevations.
            var rnd = new Random(1);
            for (int i = 0; i < 200; i++)
            {
                int k = rnd.Next(tin.Positions.Count);
                var v = tin.Positions[k];
                // Query at the file's (rounded) coordinates: boundary points must lie on the stored TIN.
                Assert.True(terrain.TryElevation(g.Mesh.Positions[k].XY, out double z));
                Assert.True(Math.Abs(v.Z - z) < 0.0005, $"{v.Z} vs {z}"); // file precision 0.001 ft
            }
            Assert.False(terrain.TryElevation(new Vec2(-1e6, -1e6), out _));
            var (minor, major) = terrain.Contours(UnitSystem.Ft(2));
            Assert.NotEmpty(minor);
            Assert.NotEmpty(major);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Dxf_Obj_Stl_References_Load()
    {
        string dxf = Temp(".dxf"), obj = Temp(".obj"), stl = Temp(".stl");
        try
        {
            File.WriteAllText(dxf, "0\nSECTION\n2\nENTITIES\n0\n3DFACE\n8\nTIN\n10\n0\n20\n0\n30\n1\n11\n10\n21\n0\n31\n2\n12\n10\n22\n10\n32\n3\n13\n10\n23\n10\n33\n3\n0\nLINE\n8\nX\n10\n0\n20\n0\n30\n0\n11\n5\n21\n5\n31\n0\n0\nLWPOLYLINE\n8\nX\n90\n3\n70\n1\n10\n0\n20\n0\n10\n1\n20\n0\n10\n1\n20\n1\n0\nENDSEC\n0\nEOF\n");
            var d = ReferenceReaders.ReadDxf(dxf, 0.3048);
            Assert.Equal(1, d.Mesh.TriangleCount);
            Assert.Equal(2, d.Polylines.Count);
            Assert.Equal(4, d.Polylines[1].Length); // closed: first point repeated
            Assert.Equal(2 * 0.3048, d.Mesh.Positions[1].Z, 9);

            File.WriteAllText(obj, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf 1 2 3 4\n");
            Assert.Equal(2, ReferenceReaders.ReadObj(obj, 1).Mesh.TriangleCount);

            using (var w = new BinaryWriter(File.Create(stl)))
            {
                w.Write(new byte[80]);
                w.Write(1);
                foreach (float f in new float[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0 }) w.Write(f);
                w.Write((ushort)0);
            }
            Assert.Equal(1, ReferenceReaders.ReadStl(stl, 1).Mesh.TriangleCount);
        }
        finally { File.Delete(dxf); File.Delete(obj); File.Delete(stl); }
    }

    [Fact]
    public void PropertyPath_GetSetWithUnits()
    {
        var doc = SbmDocument.Sample();
        doc.Set("Bridges[0].Supports[1].SkewDegrees", "20°30'");
        Assert.Equal(20.5, doc.Bridge().Supports[1].SkewDegrees, 9);
        doc.Set("Bridges[\"Sample Bridge\"].Girders.Section.WebDepth", "60");     // plain number → inches for depth
        Assert.Equal(UnitSystem.In(60), doc.Bridge().Girders.Section.WebDepth, 9);
        doc.Set("Bridges[0].Deck.LeftEdgeOffset", "-22'-6\"");
        Assert.Equal(UnitSystem.Ft(-22.5), doc.Bridge().Deck.LeftEdgeOffset, 9);
        doc.Set("Bridges[0].Supports[0].Station", "21+05.50");
        Assert.Equal(UnitSystem.Ft(2105.5), doc.Bridge().Supports[0].Station, 9);
        doc.Set("Bridges[0].Bearings.Type", "elastomeric");
        Assert.Equal(SBM.Core.Bridge.BearingType.Elastomeric, doc.Bridge().Bearings.Type);
        int i = doc.Add("Bridges[0].FieldSplices", "Station=22+90");
        Assert.Equal(UnitSystem.Ft(2290), doc.Bridge().FieldSplices[i].Station, 9);
        doc.Remove($"Bridges[0].FieldSplices[{i}]");
        Assert.Equal(2, doc.Bridge().FieldSplices.Count);
        Assert.Equal("20.5°", doc.GetText("Bridges[0].Supports[1].SkewDegrees"));
        Assert.Throws<ArgumentException>(() => doc.Set("Bridges[0].Nope", "1"));
    }

    [Fact]
    public void Document_RegenerateIsIncremental_AndSaveOpenRoundTrips()
    {
        var doc = SbmDocument.Sample();
        var r1 = doc.Regenerate();
        Assert.Equal(1, r1.Regenerated);
        var r2 = doc.Regenerate();
        Assert.Equal(0, r2.Regenerated);
        Assert.Equal(1, r2.Reused);
        doc.Set("Bridges[0].Studs.Pitch", "9\"");
        var r3 = doc.Regenerate();
        Assert.Equal(1, r3.Regenerated);
        Assert.True(r3.Studs > r1.Studs);

        string path = Temp(".sbm");
        try
        {
            doc.Save(path);
            var back = SbmDocument.Open(path);
            Assert.Equal(UnitSystem.In(9), back.Bridge().Studs.Pitch, 9);
            Assert.Equal(r3.Parts, back.Regenerate().Parts);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Script_RunsAgainstDocument()
    {
        var doc = SbmDocument.Sample();
        var res = await ScriptHost.RunAsync("""
            Doc.Bridge().Girders.Count = 6;
            Doc.Bridge().Girders.Spacings = Enumerable.Repeat(Ft(8), 5).ToList();
            var r = Doc.Regenerate();
            Print($"girders={Doc.Framing().Girders.Count}");
            return r.CrossFrames;
            """, doc);
        Assert.True(res.Success, res.Error);
        Assert.Contains("girders=6", res.Output);
        Assert.True((int)res.ReturnValue! > 0);
    }
}
