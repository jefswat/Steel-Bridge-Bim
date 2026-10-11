using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>Small builders for rolled-shape pieces in an arbitrary 3D placement (used by the cross girder, inspection access).</summary>
internal static class RolledBuilder
{
    /// <summary>
    /// Angle piece with bolt holes in either leg. The heel (outer corner) is at <paramref name="origin"/>; the member axis
    /// runs along <paramref name="axis"/> for <paramref name="length"/>; leg A extends along <paramref name="dirA"/> with its
    /// thickness toward +<paramref name="dirB"/>; leg B extends along <paramref name="dirB"/> with its thickness toward
    /// +<paramref name="dirA"/>. Holes are (u along the axis, w along the leg from the heel). The AISC root fillet is added.
    /// </summary>
    public static void AddAngle(Mesh mesh, Vec3 origin, Vec3 axis, Vec3 dirA, Vec3 dirB, double length, double legA, double legB,
        double t, double rootRadius, IReadOnlyList<Hole> holesA, IReadOnlyList<Hole> holesB)
    {
        // Leg A: plane (axis, dirA), thickness along dirB.
        PlateBuilder.AddPrism(mesh, new[] { new Vec2(0, 0), new Vec2(length, 0), new Vec2(length, legA), new Vec2(0, legA) }, holesA,
            (u, w, th) => origin + axis * u + dirA * w + dirB * th, _ => 0, _ => t);
        // Leg B: plane (axis, dirB), from the inside face of leg A outward, thickness along dirA.
        PlateBuilder.AddPrism(mesh, new[] { new Vec2(0, t), new Vec2(length, t), new Vec2(length, legB), new Vec2(0, legB) }, holesB,
            (u, w, th) => origin + axis * u + dirB * w + dirA * th, _ => 0, _ => t);
        double r = rootRadius;
        if (r > 1e-5)
        {
            var sec = new List<Vec2> { new(t, t) };
            for (int k = 0; k <= 6; k++)
            {
                double th = Math.PI / 2 * k / 6;
                sec.Add(new Vec2(t + r - r * Math.Sin(th), t + r - r * Math.Cos(th)));
            }
            sec.Reverse(1, sec.Count - 1);
            Vec3[] Ring(double u) => sec.Select(q => origin + axis * u + dirA * q.X + dirB * q.Y).ToArray();
            MeshBuilder.AddLoft(mesh, sec.ToArray(), new[] { Ring(0), Ring(length) });
        }
    }

    /// <summary>Closed polygon (plane coordinates (p, q)) extruded between two parallel positions along <paramref name="axis"/>.</summary>
    public static void AddExtrusion(Mesh mesh, IReadOnlyList<Vec2> polygon, Vec3 origin, Vec3 dirP, Vec3 dirQ, Vec3 axis, double u0, double u1)
    {
        var a = polygon.Select(p => origin + dirP * p.X + dirQ * p.Y + axis * u0).ToArray();
        var b = polygon.Select(p => origin + dirP * p.X + dirQ * p.Y + axis * u1).ToArray();
        MeshBuilder.AddLoft(mesh, polygon as Vec2[] ?? polygon.ToArray(), new[] { a, b });
    }

    /// <summary>Round bar / rope of the given radius along a polyline (rings perpendicular to the local direction).</summary>
    public static void AddTube(Mesh mesh, IReadOnlyList<Vec3> pts, double radius, int segments = 8)
    {
        if (pts.Count < 2) return;
        var section = Enumerable.Range(0, segments).Select(i => Vec2.FromAngle(2 * Math.PI * i / segments) * radius).ToArray();
        var rings = new List<Vec3[]>();
        for (int i = 0; i < pts.Count; i++)
        {
            var d = (i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : (pts[i + 1] - pts[i]).Normalized() + (pts[i] - pts[i - 1]).Normalized()).Normalized();
            var u = Vec3.Cross(d, new Vec3(0, 0, 1));
            u = u.Length < 1e-6 ? new Vec3(1, 0, 0) : u.Normalized();
            var v = Vec3.Cross(d, u).Normalized();
            rings.Add(section.Select(q => pts[i] + u * q.X + v * q.Y).ToArray());
        }
        MeshBuilder.AddLoft(mesh, section, rings);
    }
}
