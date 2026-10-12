using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>
/// Light display tubes for bars / strands: ONE loft per polyline with mitred joints (the ring at an interior vertex lies in the bisector
/// plane, where the two straight prisms meet exactly) and the section frame parallel-transported along the bar (no twist). Compared with a
/// separate capped prism per segment this has no hidden interior caps: 2n triangles per segment instead of 2n + 2(n - 2).
/// Turns sharper than <see cref="MaxTurnDegrees"/> (hairpin returns) start a new loft.
/// </summary>
public static class TubeBuilder
{
    public const double MaxTurnDegrees = 150;

    public static void AddMitredTube(Mesh mesh, IReadOnlyList<Vec3> pts, double r, int sides = 6)
    {
        // Drop zero-length segments.
        var p = new List<Vec3>(pts.Count);
        foreach (var q in pts) if (p.Count == 0 || (q - p[^1]).Length > 1e-6) p.Add(q);
        if (p.Count < 2) return;
        var sec = Enumerable.Range(0, sides).Select(i => Vec2.FromAngle(2 * Math.PI * i / sides) * r).ToArray();
        double cosMax = Math.Cos(MaxTurnDegrees * Math.PI / 180);

        int start = 0;
        while (start < p.Count - 1)
        {
            var a = (p[start + 1] - p[start]).Normalized();
            var x = Vec3.Cross(Math.Abs(a.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0), a).Normalized();
            var y = Vec3.Cross(a, x);
            var rings = new List<Vec3[]> { sec.Select(q => p[start] + x * q.X + y * q.Y).ToArray() };
            int i = start + 1;
            for (; i < p.Count - 1; i++)
            {
                var b = (p[i + 1] - p[i]).Normalized();
                double c = Vec3.Dot(a, b);
                if (c < cosMax) break; // too sharp: end this loft here
                var n = (a + b).Normalized();
                double an = Vec3.Dot(a, n);
                // Ring = the current prism's edge lines cut by the bisector plane through p[i].
                var ring = new Vec3[sides];
                for (int k = 0; k < sides; k++)
                {
                    var off = x * sec[k].X + y * sec[k].Y;
                    double t = -Vec3.Dot(off, n) / an;
                    ring[k] = p[i] + off + a * t;
                }
                rings.Add(ring);
                // Transport the frame: rotate about a x b by the turn angle.
                if (c < 1 - 1e-12)
                {
                    x = Rotate(x, a, b);
                    y = Vec3.Cross(b, x).Normalized();
                    x = Vec3.Cross(y, b).Normalized();
                }
                a = b;
            }
            rings.Add(sec.Select(q => p[i] + x * q.X + y * q.Y).ToArray());
            MeshBuilder.AddLoft(mesh, sec, rings);
            start = i;
        }
    }

    /// <summary>Rotates v by the rotation that takes unit a to unit b (about a x b).</summary>
    private static Vec3 Rotate(Vec3 v, Vec3 a, Vec3 b)
    {
        var k = Vec3.Cross(a, b);
        double s = k.Length, c = Vec3.Dot(a, b);
        if (s < 1e-12) return v;
        k = k * (1 / s);
        // Rodrigues
        return v * c + Vec3.Cross(k, v) * s + k * (Vec3.Dot(k, v) * (1 - c));
    }
}
