using System.IO.Compression;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.IO.Kml;
using Xunit;

namespace SBM.Tests;

public class KmzTests
{
    [Theory]
    // reference values from tools/terrain/crs.py (the terrain pipeline's projection table)
    [InlineData("EPSG:2264", 600000.0, 180000.0, -79.10566927650595, 35.372597200919074)]
    [InlineData("EPSG:2272", 800000.0, 100000.0, -75.40034720259554, 40.20999004768217)]
    [InlineData("EPSG:32615", 520000.0, 5200000.0, -92.73715985898913, 46.95322751459104)]
    [InlineData("ESRI:103708", 150000.0, 40000.0, -93.29717224969738, 45.12093228707795)]
    [InlineData("EPSG:2276", 700000.0, 2100000.0, -97.4350381077427, 32.56388615479899)]
    public void MapProjections_Inverse_MatchesTheTerrainTable_AndRoundTrips(string crs, double x, double y, double lon, double lat)
    {
        var p = MapProjections.For(crs)!;
        var (lo, la) = p.Inverse(x, y);
        Assert.InRange(lo - lon, -1e-9, 1e-9);
        Assert.InRange(la - lat, -1e-9, 1e-9);
        var (x2, y2) = p.Forward(lo, la);
        Assert.InRange(x2 - x, -1e-3, 1e-3);   // Krueger series to n^3 (as the terrain tools): sub-mm
        Assert.InRange(y2 - y, -1e-3, 1e-3);
        Assert.Null(MapProjections.For("EPSG:9999"));
    }

    [Fact]
    public void LocalEnu_OriginIsZero_EastAndNorthAreMetres()
    {
        var enu = new LocalEnu(-79.0, 35.0, 100);
        var o = enu.ToEnu(-79.0, 35.0, 100);
        Assert.True(o.Length < 1e-6);
        var e = enu.ToEnu(-79.0 + 0.001, 35.0, 100);   // ~91.3 m east at 35 deg
        Assert.InRange(e.X, 91.0, 91.6);
        Assert.InRange(Math.Abs(e.Y), 0, 0.01);
        var u = enu.ToEnu(-79.0, 35.0, 110);
        Assert.InRange(u.Z - 10, -1e-6, 1e-6);
    }

    [Fact]
    public void Kmz_LowResolution_NoBars_FitsTheBudget_NeedsAGeoreference()
    {
        var p = SampleProject.Create();
        var cache = new RegenerationCache();
        var models = cache.Regenerate(p).OfType<BridgeModel>().ToList();
        string path = Path.Combine(Path.GetTempPath(), $"sbm-kmz-{Guid.NewGuid():N}.kmz");
        // local grid: refused with a plain reason
        var ex = Assert.Throws<InvalidOperationException>(() => KmzExporter.Export(path, p, models));
        Assert.Contains("georeference", ex.Message);
        // the sample's coordinates read as UTM 14N metres (any valid CRS will do for the test)
        p.Georeference = new GeoreferenceDefinition { CrsName = "EPSG:32614", GridScale = 1 };
        try
        {
            var r = KmzExporter.Export(path, p, models, new KmzOptions { MaxTriangles = 20_000 });
            Assert.True(r.Bridges >= 1);
            Assert.InRange(r.Triangles, 1, 20_000);
            Assert.True(r.SourceTriangles > r.Triangles);
            using (var z = ZipFile.OpenRead(path))
            {
                var kml = new StreamReader(z.GetEntry("doc.kml")!.Open()).ReadToEnd();
                Assert.Contains("<Model>", kml);
                Assert.Contains("<LineString>", kml);
                var dae = z.Entries.Single(e => e.FullName.StartsWith("models/") && e.FullName.EndsWith(".dae"));
                var text = new StreamReader(dae.Open()).ReadToEnd();
                Assert.Contains("<up_axis>Z_UP</up_axis>", text);
            }
            // a coarser budget coarsens the grid
            var r2 = KmzExporter.Export(path, p, models, new KmzOptions { MaxTriangles = 2_000 });
            Assert.True(r2.CellSize > r.CellSize || r2.Triangles <= 2_000);
        }
        finally { File.Delete(path); }
    }
}
