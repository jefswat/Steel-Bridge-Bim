using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>
/// Display simplifications for the viewport memory manager. Bolt holes are most of the steel triangles (16-sided walls plus the faces
/// triangulated around them; ~324 k holes on I-80 + Ramp G = ~90 % of the steel), and the bolts drawn in them hide them anyway: a plate
/// whose mesh is fully described by its exact extrusions (<see cref="Mesh.HasExactSolids"/>) is redrawn from those extrusions WITHOUT the
/// holes. Other meshes are returned unchanged.
/// </summary>
public static class MeshLod
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Mesh, Mesh> Cache = new();

    /// <summary>True when <see cref="WithoutHoles"/> would change the mesh.</summary>
    public static bool HasDrawableHoles(Mesh m) => m.HasExactSolids && m.Solids!.Any(s => s.Holes.Length > 0);

    /// <summary>The mesh without its round holes (prisms of the recorded outlines), or the mesh itself.</summary>
    public static Mesh WithoutHoles(Mesh m)
    {
        if (!HasDrawableHoles(m)) return m;
        return Cache.GetValue(m, Build);
    }

    private static Mesh Build(Mesh m)
    {
        var r = new Mesh();
        foreach (var s in m.Solids!)
        {
            var f = s.Frame;
            var bottom = s.Outline.Select(q => f.ToWorld(q.X, q.Y, 0)).ToArray();
            var top = bottom.Select(q => q + s.Extrusion).ToArray();
            MeshBuilder.AddLoft(r, s.Outline, new[] { bottom, top });
        }
        return r;
    }

    /// <summary>Triangles of <see cref="WithoutHoles"/> without building it.</summary>
    public static int HoleFreeTriangles(Mesh m)
    {
        if (!HasDrawableHoles(m)) return m.TriangleCount;
        int t = 0;
        foreach (var s in m.Solids!) t += 2 * s.Outline.Length + 2 * Math.Max(0, s.Outline.Length - 2);
        return t;
    }
}
