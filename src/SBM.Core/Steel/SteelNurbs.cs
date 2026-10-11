using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>Exact-geometry option: curved girder / splice plates as NURBS, their meshes tessellated from the NURBS.</summary>
public sealed partial class SteelModelGenerator
{
    private Project.GeometrySettings? _geo;
    private double _fitTol;

    /// <summary>
    /// Long plate along girder <paramref name="path"/>: with the exact-geometry option and a curved plate, fits the NURBS plate to the
    /// path's EXACT geometry (span curves + deck surface), tessellates it (today's breaks, or the chord tolerance) and records the
    /// NURBS on the mesh; otherwise builds the plate exactly as before. <paramref name="web"/> = map arguments (s, z, r).
    /// </summary>
    private void LongPlate(Mesh mesh, GirderPath path, double a, double b, IEnumerable<double> breaks, IEnumerable<double> plateBreaks,
        Func<double, double> w0, Func<double, double> w1, Func<double, double> t0, Func<double, double> t1, IReadOnlyList<Hole> holes,
        Func<double, double, double, Vec3> map, bool web, string kind)
    {
        if (_geo == null) { PlateBuilder.AddLongPlate(mesh, a, b, breaks, w0, w1, t0, t1, holes, map); return; }
        Func<double, double, double, Vec3> exact = web ? (s, z, r) => path.ExactPoint(s, r, z) : (s, r, z) => path.ExactPoint(s, r, z);
        var pb = plateBreaks.Where(x => x > a && x < b).ToList();
        var hard = path.ExactBreaks.Concat(pb).Concat(path.Exact == null ? path.S : Enumerable.Empty<double>()).ToList();
        if (!IsCurved(exact, a, b, w0, w1, t0, t1, hard))
        {
            PlateBuilder.AddLongPlate(mesh, a, b, breaks, w0, w1, t0, t1, holes, map);
            return;
        }
        string? reason = null;
        foreach (var h in holes)
        {
            double s = h.Center.X, w = h.Center.Y, r = h.Diameter / 2 + 1e-5;
            if (s - r <= a || s + r >= b || w - r <= w0(s) || w + r >= w1(s)) { reason = "hole at the plate edge"; break; }
        }
        var sw = reason == null ? NurbsSweep.Plate(a, b, w0, w1, t0, t1, exact, hard, _fitTol, out reason) : null;
        if (sw == null)
        {
            _model.Nurbs!.Fallback(kind, reason!);
            PlateBuilder.AddLongPlate(mesh, a, b, breaks, w0, w1, t0, t1, holes, map);
            return;
        }
        var ss = _geo.ChordTolerance > 0 ? sw.Samples(_geo.ChordTolerance, pb) : breaks;
        int tri0 = mesh.TriangleCount;
        PlateBuilder.AddLongPlate(mesh, a, b, ss, w0, w1, t0, t1, holes, sw.PlateMap);
        mesh.AddNurbs(sw.ToSolid(kind, holes), tri0);
        _model.Nurbs!.Built(kind, sw.MaxDeviation);
    }

    /// <summary>True when the plate's corner lines leave the straight chords between the hard breaks by more than a micron.</summary>
    private static bool IsCurved(Func<double, double, double, Vec3> map, double a, double b, Func<double, double> w0, Func<double, double> w1,
        Func<double, double> t0, Func<double, double> t1, List<double> hard)
    {
        var cuts = hard.Where(x => x > a && x < b).Append(a).Append(b).Distinct().OrderBy(x => x).ToList();
        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            double s0 = cuts[i], s1 = cuts[i + 1];
            foreach (var (wf, tf) in new[] { (w0, t0), (w1, t1) })
            {
                var p0 = map(s0, wf(s0), tf(s0));
                var p1 = map(s1, wf(s1), tf(s1));
                for (int k = 1; k < 8; k++)
                {
                    double s = s0 + (s1 - s0) * k / 8;
                    var q = map(s, wf(s), tf(s));
                    var e = Vec3.Lerp(p0, p1, (double)k / 8) - q;
                    if (e.Length > 1e-6) return true;
                }
            }
        }
        return false;
    }
}
