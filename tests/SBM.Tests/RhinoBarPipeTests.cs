using SBM.Api;
using SBM.Core.Geometry;
using SBM.IO.Rhino3dm;
using Xunit;
using RG = Rhino.Geometry;

namespace SBM.Tests;

/// <summary>Bars as exact closed pipes in the Rhino export (quarter cylinders, revolved torus patches, planar caps).</summary>
[Collection(RhinoNativeCollection.Name)]
public class RhinoBarPipeTests
{
    /// <summary>A #8 bar: 0.6 m leg, 90 degree bend (R 0.1 m) in the XY plane, 0.4 m leg, then a 135 degree hook bend in a vertical plane, 0.15 m tail.</summary>
    private static List<Vec3> BentBar()
    {
        var p = new List<Vec3>();
        for (int i = 0; i <= 6; i++) p.Add(new Vec3(-0.6 + 0.1 * i, 0, 0));
        for (int i = 1; i <= 12; i++) { double a = Math.PI / 2 * i / 12; p.Add(new Vec3(0.1 * Math.Sin(a), 0.1 - 0.1 * Math.Cos(a), 0)); }
        for (int i = 1; i <= 4; i++) p.Add(new Vec3(0.1, 0.1 + 0.1 * i, 0));
        var c = new Vec3(0.1, 0.5, 0.08);                                // second bend about the x axis, radius 0.08
        for (int i = 1; i <= 18; i++) { double a = 0.75 * Math.PI * i / 18; p.Add(new Vec3(0.1, 0.5 + 0.08 * Math.Sin(a), 0.08 - 0.08 * Math.Cos(a))); }
        var t = p[^1] - p[^2]; t = t * (1 / t.Length);
        p.Add(p[^1] + t * 0.15);
        return p;
    }

    [Fact]
    public void BentBar_IsOneClosedValidBrep()
    {
        double r = 0.0254 / 2;
        var faces = RhinoBarPipe.Faces(BentBar(), r, out var why);
        Assert.True(faces != null, why);
        var bb = new RhinoBrepBuilder(v => new RG.Point3d(v.X, v.Y, v.Z), 1, 1e-6);
        foreach (var f in faces!) bb.AddFace(f);
        var brep = bb.Finish();
        Assert.True(brep != null, bb.Error);
        Assert.True(brep!.IsValid);
        Assert.True(brep.IsSolid);
        Assert.Equal(6, brep.Faces.Count);                                   // 4 quarter faces + 2 caps, whatever the piece count
        // every circle point lies r from the centerline: sample the faces' control rows at u = 0 (exact circle points)
        foreach (var f in faces.Where(f => f.Surface != null))
            for (int j = 0; j < f.Surface!.CountV; j += f.Surface.CountV - 1)
            {
                var q = f.Surface.PointAt(f.Surface.U0, f.Surface.V0 + (f.Surface.V1 - f.Surface.V0) * j / (f.Surface.CountV - 1));
                double d = BentBar().Zip(BentBar().Skip(1), (a, b) => SegDist(q, a, b)).Min();
                Assert.InRange(d, r - 6e-4, r + 6e-4);                  // ArcFit tolerance 0.5 mm
            }
    }

    [Fact]
    public void HollowPipe_IsOneClosedValidBrep_WithTheWallVolume()
    {
        // bent steel pipe (deviation / future PT pipe): outer wall + reversed bore + annular caps, one solid whose volume is the wall only
        double ro = 0.0889 / 2, ri = ro - 0.0055;
        var faces = RhinoBarPipe.Faces(BentBar(), ro, out var why, ri);
        Assert.True(faces != null, why);
        var bb = new RhinoBrepBuilder(v => new RG.Point3d(v.X, v.Y, v.Z), 1, 1e-6);
        foreach (var f in faces!) bb.AddFace(f);
        var brep = bb.Finish();
        Assert.True(brep != null, bb.Error);
        Assert.True(brep!.IsValid);
        Assert.True(brep.IsSolid);
        Assert.Equal(10, brep.Faces.Count);                                  // 4 outer + 4 bore quarter faces + 2 annular caps
        double len = BentBar().Zip(BentBar().Skip(1), (a, b) => (b - a).Length).Sum();
        double wall = Math.PI * (ro * ro - ri * ri) * len;
        var full = RhinoBarPipe.Faces(BentBar(), ro, out _)!;
        var bf = new RhinoBrepBuilder(v => new RG.Point3d(v.X, v.Y, v.Z), 1, 1e-6);
        foreach (var f in full) bf.AddFace(f);
        double vHollow = Rhino3dmInfo.BrepVolume(brep), vFull = Rhino3dmInfo.BrepVolume(bf.Finish()!);
        Assert.True(vHollow > 0 && vHollow < vFull, $"{vHollow} vs {vFull}");
        Assert.InRange(vHollow / wall, 0.97, 1.03);                         // the centerline polyline cuts the bends a little short
        Assert.Null(RhinoBarPipe.Faces(BentBar(), ro, out _, ro));          // bore not inside the wall
    }

    [Fact]
    public void SampledBendOver90_IsOneArcPiece_AndAPipe()
    {
        // W1-like: 1.2 m leg, 104 deg bend R 0.065 sampled in 7 segments (~15 deg each, as RebarBends rounds), 0.55 m leg
        var p = new List<Vec3> { new(-1.2, 0, 0), new(0, 0, 0) };
        double R = 0.065, sweep = 104 * Math.PI / 180;
        for (int i = 1; i <= 7; i++) { double a = sweep * i / 7; p.Add(new Vec3(R * Math.Sin(a), R - R * Math.Cos(a), 0)); }
        var t = new Vec3(Math.Cos(sweep), Math.Sin(sweep), 0);
        p.Add(p[^1] + t * 0.55);
        var pcs = RhinoBarPipe.Fit(p);
        Assert.Equal(3, pcs.Count);
        Assert.True(pcs[1].IsArc && pcs[1].A == 1 && pcs[1].B == 8);
        Assert.NotNull(RhinoBarPipe.Faces(p, 0.01, out var why));
    }

    [Fact]
    public void RealKink_FallsBack()
    {
        var p = new List<Vec3> { new(0, 0, 0), new(1, 0, 0), new(1, 1, 0) };
        Assert.Null(RhinoBarPipe.Faces(p, 0.01, out var why));
        Assert.Contains("kink", why);
    }

    [Fact]
    public void SampleExport_BarSolids_AreClosedPipes()
    {
        var doc = SbmDocument.Sample();
        doc.Regenerate();
        string path = Path.Combine(Path.GetTempPath(), $"sbm-test-{Guid.NewGuid():N}-pipes.3dm");
        try
        {
            var r = doc.Export3dm(path, barSolids: true, exactCurved: false);
            int bars = doc.Model().Concrete!.Bars.Count;
            Assert.True(bars > 0);
            Assert.True(r.BarBreps > 0, string.Join("; ", r.MeshReasons.Select(kv => kv.Key + " " + kv.Value)));
            Assert.Equal(r.BarBreps, r.BarBrepsClosed);
            var s = Rhino3dmInfo.Read(path);
            Assert.True(s.ClosedBreps >= r.BarBreps);
        }
        finally { File.Delete(path); }
    }

    private static double SegDist(Vec3 q, Vec3 a, Vec3 b)
    {
        var ab = b - a; double t = Math.Clamp(Vec3.Dot(q - a, ab) / Vec3.Dot(ab, ab), 0, 1);
        return (q - (a + ab * t)).Length;
    }
}
