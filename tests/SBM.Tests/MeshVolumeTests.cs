using SBM.Core.Geometry;
using SBM.Core.Solids;
using Xunit;

namespace SBM.Tests;

public class MeshVolumeTests
{
    /// <summary>Georeferenced parts (UTM: millions of meters out) must keep exact volumes; the origin-based tetrahedron
    /// sum lost every digit there and made masses/tonnage garbage.</summary>
    [Fact]
    public void VolumeIsExactFarFromTheOrigin()
    {
        var far = new Vec3(262302.16, 4568371.47, 350.0);
        var f = Frame3.FromXZ(far, new Vec3(0.7, -0.71, 0.02), new Vec3(0, 0, 1));
        var m = new Mesh();
        MeshBuilder.AddExtrusion(m, new[] { new Vec2(0, 0), new Vec2(0.127, 0), new Vec2(0.127, 0.0095), new Vec2(0.0095, 0.0095), new Vec2(0.0095, 0.127), new Vec2(0, 0.127) }, f, 0, 4.4);
        double area = 0.127 * 0.0095 + (0.127 - 0.0095) * 0.0095;
        Assert.Equal(area * 4.4, Math.Abs(m.Volume()), 9);
        // The recorded extrusion agrees.
        Assert.True(m.HasExactSolids);
        var e = m.Solids![0];
        Assert.Equal(area * 4.4, Math.Abs(MeshBuilder.SignedArea(e.Outline) * Vec3.Dot(e.Extrusion, e.Frame.Z)), 9);
    }
}
