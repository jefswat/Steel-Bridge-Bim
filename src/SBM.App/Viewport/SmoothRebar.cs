using System.Numerics;
using HelixToolkit;
using HelixToolkit.SharpDX;
using SBM.Core.Geometry;

namespace SBM.App.Viewport;

/// <summary>
/// Near-view level of detail for reinforcing bars. The scene normally draws a bar as one hexagonal prism per centerline
/// segment (light, but bends look faceted and the joints overlap). Bars close to the view target are redrawn as ONE continuous
/// tube each: rings at every centerline vertex placed with parallel-transport frames (no twist), ring planes mitered between
/// the adjoining segments (no overlap, no gap), corners and coarse arcs rounded into steps of at most 10 degrees, and smooth
/// per-vertex normals around the circumference.
/// </summary>
public static class SmoothRebar
{
    /// <summary>Largest turn between consecutive segments of a smooth bar (degrees).</summary>
    public const double MaxTurnDeg = 10;
    /// <summary>Sides of the smooth tube (the far hexagon has 6).</summary>
    public const int Sides = 10;

    /// <summary>
    /// Rounds the corners of a centerline (hooks, 90-degree bends given as a single vertex) and subdivides turns above
    /// <see cref="MaxTurnDeg"/> into arcs of at most that angle per step. A corner takes at most half of each adjoining
    /// segment, so neighbouring roundings never overlap; where the centerline is already sampled finely nothing is added.
    /// </summary>
    public static List<Vec3> Densify(IReadOnlyList<Vec3> input, double barDiameter)
    {
        var p = new List<Vec3>(input.Count);
        foreach (var v in input)
            if (p.Count == 0 || (v - p[^1]).Length > 1e-5) p.Add(v);
        if (p.Count < 3) return p;
        double maxTurn = MaxTurnDeg * Math.PI / 180;
        double bendRadius = 2.0 * barDiameter; // centerline radius of a tight bend (about 3 db inside diameter...4 db)
        var o = new List<Vec3>(p.Count * 2) { p[0] };
        for (int i = 1; i + 1 < p.Count; i++)
        {
            var a = p[i - 1];
            var b = p[i];
            var c = p[i + 1];
            double la = (b - a).Length, lb = (c - b).Length;
            var u = (b - a) * (1 / la);
            var v = (c - b) * (1 / lb);
            double cos = Math.Clamp(Vec3.Dot(u, v), -1, 1);
            double theta = Math.Acos(cos);
            if (theta <= maxTurn || theta > 175 * Math.PI / 180) { o.Add(b); continue; }
            double half = Math.Tan(theta / 2);
            double d = Math.Min(bendRadius * half, Math.Min(0.5 * la, 0.5 * lb));
            if (d < 1e-6) { o.Add(b); continue; }
            double r = d / half;
            var start = b - u * d;
            var n = (v - u * cos).Normalized();          // from the first tangent point toward the circle center
            var center = start + n * r;
            var w = Vec3.Cross(u, v).Normalized();       // rotation axis of the turn
            var rs = start - center;
            var k = Vec3.Cross(w, rs);
            int steps = Math.Max(1, (int)Math.Ceiling(theta / maxTurn - 1e-9));
            for (int s = 0; s <= steps; s++)
            {
                double t = theta * s / steps;
                o.Add(center + rs * Math.Cos(t) + k * Math.Sin(t));
            }
        }
        o.Add(p[^1]);
        return o;
    }

    /// <summary>
    /// Appends one smooth tube. Positions are relative to <paramref name="origin"/>. Returns the number of triangles added.
    /// </summary>
    public static int AddTube(Vector3Collection pos, Vector3Collection nrm, IntCollection idx, IReadOnlyList<Vec3> centerline,
        double diameter, Vec3 origin)
    {
        var c = Densify(centerline, diameter);
        int n = c.Count;
        if (n < 2) return 0;
        double r = diameter / 2;
        // Tangents: the mean of the adjoining segment directions (the miter plane), end tangents = the end segments.
        var seg = new Vec3[n - 1];
        for (int i = 0; i + 1 < n; i++) seg[i] = (c[i + 1] - c[i]).Normalized();
        var tan = new Vec3[n];
        tan[0] = seg[0];
        tan[n - 1] = seg[n - 2];
        for (int i = 1; i + 1 < n; i++)
        {
            var m = seg[i - 1] + seg[i];
            tan[i] = m.Length > 1e-9 ? m.Normalized() : seg[i];
        }
        // Parallel transport of the ring axes: rotate the previous normal by the minimal rotation tangent[i-1] -> tangent[i].
        var nor = new Vec3[n];
        var t0 = tan[0];
        var up = Math.Abs(t0.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0);
        nor[0] = Vec3.Cross(up, t0).Normalized();
        for (int i = 1; i < n; i++)
        {
            var a = tan[i - 1];
            var b = tan[i];
            var axis = Vec3.Cross(a, b);
            double s = axis.Length, cs = Math.Clamp(Vec3.Dot(a, b), -1, 1);
            var v = nor[i - 1];
            if (s > 1e-12)
            {
                var k = axis * (1 / s);
                double ang = Math.Atan2(s, cs);
                v = v * Math.Cos(ang) + Vec3.Cross(k, v) * Math.Sin(ang) + k * (Vec3.Dot(k, v) * (1 - Math.Cos(ang)));
            }
            v = v - b * Vec3.Dot(v, b);
            nor[i] = v.Normalized();
        }

        int baseIndex = pos.Count;
        int ringStride = Sides;
        for (int i = 0; i < n; i++)
        {
            var bin = Vec3.Cross(tan[i], nor[i]);               // (normal, binormal, tangent) is right-handed
            // The ring lies in the miter plane; stretch it along the bend so the tube keeps its radius across the joint.
            double stretch = 1;
            if (i > 0 && i + 1 < n)
            {
                double cosHalf = Vec3.Dot(tan[i], seg[i]);
                if (cosHalf > 0.2) stretch = 1 / cosHalf;
            }
            var bendDir = i > 0 && i + 1 < n ? (seg[i] - seg[i - 1]) : default;
            bendDir = bendDir.Length > 1e-9 ? bendDir.Normalized() : default;
            for (int j = 0; j < Sides; j++)
            {
                double phi = 2 * Math.PI * j / Sides;
                var radial = nor[i] * Math.Cos(phi) + bin * Math.Sin(phi);
                var off = radial * r;
                if (stretch > 1 && bendDir.Length > 0) off = off + bendDir * (Vec3.Dot(off, bendDir) * (stretch - 1));
                var q = c[i] + off - origin;
                pos.Add(new Vector3((float)q.X, (float)q.Y, (float)q.Z));
                nrm.Add(new Vector3((float)radial.X, (float)radial.Y, (float)radial.Z));
            }
        }
        int tris = 0;
        for (int i = 0; i + 1 < n; i++)
            for (int j = 0; j < Sides; j++)
            {
                int j2 = (j + 1) % Sides;
                int a0 = baseIndex + i * ringStride + j, a1 = baseIndex + i * ringStride + j2;
                int b0 = baseIndex + (i + 1) * ringStride + j, b1 = baseIndex + (i + 1) * ringStride + j2;
                idx.Add(a0); idx.Add(a1); idx.Add(b0);
                idx.Add(a1); idx.Add(b1); idx.Add(b0);
                tris += 2;
            }
        // End caps: own vertices with the axis as normal.
        tris += Cap(pos, nrm, idx, c[0] - origin, -tan[0], baseIndex, true);
        tris += Cap(pos, nrm, idx, c[n - 1] - origin, tan[n - 1], baseIndex + (n - 1) * ringStride, false);
        return tris;
    }

    private static int Cap(Vector3Collection pos, Vector3Collection nrm, IntCollection idx, Vec3 center, Vec3 normal, int ringStart, bool start)
    {
        int ci = pos.Count;
        var cn = new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z);
        pos.Add(new Vector3((float)center.X, (float)center.Y, (float)center.Z));
        nrm.Add(cn);
        for (int j = 0; j < Sides; j++) { pos.Add(pos[ringStart + j]); nrm.Add(cn); }
        for (int j = 0; j < Sides; j++)
        {
            int a = ci + 1 + j, b = ci + 1 + (j + 1) % Sides;
            if (start) { idx.Add(ci); idx.Add(b); idx.Add(a); }
            else { idx.Add(ci); idx.Add(a); idx.Add(b); }
        }
        return Sides;
    }

    /// <summary>Accumulates smooth tubes into one merged mesh with a pick table (one tag per bar).</summary>
    public sealed class Batch(Vec3 origin)
    {
        private readonly Vector3Collection _pos = new(), _nrm = new();
        private readonly IntCollection _idx = new();
        private readonly PickTable _table = new();
        public int Triangles { get; private set; }
        public List<RebarPick> Used { get; } = new();

        public void Add(RebarPick bar)
        {
            int added = AddTube(_pos, _nrm, _idx, bar.Bar.Points, bar.Mark.Size.Diameter, origin);
            if (added == 0) return;
            _table.TriStart.Add(Triangles);
            _table.Tags.Add(bar);
            Used.Add(bar);
            Triangles += added;
        }

        /// <summary>The mesh (registered with its pick table) or null when empty.</summary>
        public MeshGeometry3D? Finish()
        {
            if (_pos.Count == 0) return null;
            var g = new MeshGeometry3D { Positions = _pos, Normals = _nrm, Indices = _idx };
            g.UpdateBounds();
            SceneBuilder.Picks.AddOrUpdate(g, _table);
            return g;
        }
    }
}

/// <summary>Near-view rebar level of detail settings (there is no user settings store yet; change here or use the snapshot options).</summary>
public static class RebarLod
{
    public static bool Enabled = true;
    /// <summary>Bars whose bounds come within this distance (ft) of the view-center line are drawn as smooth tubes.</summary>
    public static double RadiusFt = 20;
    /// <summary>The smooth version is used only while the view (orthographic width) is narrower than this (ft).</summary>
    public static double SwitchWidthFt = 60;
    /// <summary>Upper bound of smooth triangles (the nearest bars win).</summary>
    public static int MaxTriangles = 1_000_000;

    /// <summary>Snapshot/diagnostic options: <c>--nolod</c>, <c>--lodradius ft</c>, <c>--lodwidth ft</c>.</summary>
    public static void ParseArgs(string[] args)
    {
        if (args.Contains("--nolod")) Enabled = false;
        double V(string key, double d)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
        }
        RadiusFt = V("--lodradius", RadiusFt);
        SwitchWidthFt = V("--lodwidth", SwitchWidthFt);
    }
}
