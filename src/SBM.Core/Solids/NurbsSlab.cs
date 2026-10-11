using SBM.Core.Geometry;
using SBM.Core.Geometry.Nurbs;

namespace SBM.Core.Solids;

/// <summary>
/// Slab of constant vertical depth under a curved top surface, built from bands across (between the columns where the top may kink:
/// deck edges, crown / pivot, gutter lines, kinked-end break points). Each band's top is one tensor NURBS surface (u along the slab
/// from its begin end, v across the band); the row curves of all bands share one u knot vector and neighbouring bands share their
/// boundary row exactly. The generator supplies the EXACT top point of row v of band b at u (<see cref="Create"/>).
/// </summary>
public sealed class NurbsSlab
{
    public required NurbsSurface[] Top { get; init; }
    public required double Depth { get; init; }
    public required double MaxDeviation { get; init; }
    public required double Tolerance { get; init; }

    /// <summary>Fits the slab; null with the reason when the tolerance is not reached.</summary>
    /// <param name="row">(band, v) -> evaluator of the exact top along u in [0, 1]; called once per row.</param>
    public static NurbsSlab? Create(int bands, Func<int, double, Func<double, Vec3>> row, double depth, double tol, out string reason, int maxRows = 32)
    {
        reason = "";
        var nv = Enumerable.Repeat(3, bands).ToArray();
        for (int iter = 0; iter < 6; iter++)
        {
            // rows: band b, j = 0..nv[b]; row (b, 0) is row (b-1, nv[b-1])
            var fns = new List<Func<double, Vec3>>();
            var index = new int[bands][];
            for (int b = 0; b < bands; b++)
            {
                index[b] = new int[nv[b] + 1];
                for (int j = 0; j <= nv[b]; j++)
                {
                    if (j == 0 && b > 0) { index[b][0] = index[b - 1][nv[b - 1]]; continue; }
                    index[b][j] = fns.Count;
                    fns.Add(row(b, (double)j / nv[b]));
                }
            }
            var fit = NurbsFit.FitCommon(fns.Count, u => fns.Select(f => f(u)).ToArray(), 0, 1, Array.Empty<double>(), tol * 0.5, 3, 0.1);
            if (!fit.Converged) { reason = $"rows: fit {fit.MaxDeviation * 1000:0.###} mm > {tol * 500:0.###} mm"; return null; }
            int nu = fit.Control[0].Length;
            var top = new NurbsSurface[bands];
            double dev = fit.MaxDeviation;
            bool again = false;
            for (int b = 0; b < bands; b++)
            {
                var t = Enumerable.Range(0, nv[b] + 1).Select(j => (double)j / nv[b]).ToArray();
                double[] kv = Array.Empty<double>();
                var ctrl = new Vec3[nu, nv[b] + 1];
                for (int i = 0; i < nu; i++)
                {
                    var (k, c) = NurbsFit.Interpolate(t, t.Select((_, j) => fit.Control[index[b][j]][i]).ToArray(), 3);
                    kv = k;
                    for (int j = 0; j < c.Length; j++) ctrl[i, j] = c[j];
                }
                top[b] = new NurbsSurface(3, Math.Min(3, nv[b]), fit.Knots, kv, ctrl);
                // check between the rows
                double bd = 0;
                for (int j = 0; j < nv[b]; j++)
                {
                    double v = (j + 0.5) / nv[b];
                    var ex = row(b, v);
                    for (int k = 0; k <= 64; k++)
                    {
                        double u = k / 64.0;
                        bd = Math.Max(bd, (top[b].PointAt(u, v) - ex(u)).Length);
                    }
                    // and between the u knots
                    foreach (double u in fit.Knots.Distinct().Zip(fit.Knots.Distinct().Skip(1), (a, c) => (a + c) / 2))
                        bd = Math.Max(bd, (top[b].PointAt(u, v) - ex(u)).Length);
                }
                dev = Math.Max(dev, bd);
                if (bd > tol)
                {
                    if (nv[b] * 2 > maxRows) { reason = $"band {b + 1}: across {bd * 1000:0.###} mm > {tol * 1000:0.###} mm"; return null; }
                    nv[b] *= 2;
                    again = true;
                }
            }
            if (!again) return new NurbsSlab { Top = top, Depth = depth, MaxDeviation = dev, Tolerance = tol };
        }
        reason = "no convergence";
        return null;
    }

    public Vec3 TopAt(int band, double v, double u) => Top[band].PointAt(u, v);

    /// <summary>Parameters u along the slab within <paramref name="chord"/> on every band boundary row.</summary>
    public List<double> Samples(double chord)
    {
        var rows = new List<NurbsCurve> { Top[0].Boundary(true, false) };
        rows.AddRange(Top.Select(s => s.Boundary(true, true)));
        return NurbsSweep.SampleCurves(rows, chord);
    }

    /// <summary>Closed B-rep: band tops and bottoms, a ruled end face per band at each end, ruled side faces.</summary>
    public NurbsSolid ToSolid(string kind)
    {
        var solid = new NurbsSolid { Kind = kind, Tolerance = Tolerance, MaxDeviation = MaxDeviation };
        var down = new Vec3(0, 0, -Depth);
        void Add(NurbsSurface s, Vec3 outward, string role)
        {
            var (p, su, sv) = s.Eval((s.U0 + s.U1) / 2, (s.V0 + s.V1) / 2);
            if (Vec3.Dot(Vec3.Cross(su, sv), outward) < 0) s = s.ReversedV();
            solid.Faces.Add(new NurbsFace { Surface = s, Role = role });
        }
        for (int b = 0; b < Top.Length; b++)
        {
            var t = Top[b];
            Add(t, new Vec3(0, 0, 1), "top");
            Add(t.Translated(down), new Vec3(0, 0, -1), "bottom");
            foreach (bool end in new[] { false, true })
            {
                var c = t.Boundary(false, end);
                double vm = 0.5;
                var along = t.PointAt(end ? 1 - 1e-4 : 1e-4, vm) - t.PointAt(end ? 1 : 0, vm);   // into the slab
                Add(NurbsSurface.Ruled(c, c.Translated(down)), -along, end ? "end 1" : "end 0");
            }
        }
        foreach (bool right in new[] { false, true })
        {
            var t = Top[right ? Top.Length - 1 : 0];
            var c = t.Boundary(true, right);
            var (_, _, sv) = t.Eval(0.5, right ? t.V1 : t.V0);
            Add(NurbsSurface.Ruled(c, c.Translated(down)), right ? sv : -sv, right ? "side 1" : "side 0");
        }
        return solid;
    }
}
