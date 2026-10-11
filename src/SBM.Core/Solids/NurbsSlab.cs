using SBM.Core.Geometry;
using SBM.Core.Geometry.Nurbs;

namespace SBM.Core.Solids;

/// <summary>
/// Slab of constant vertical depth under a curved top surface. Across, it is split into BANDS at the columns where the top may kink
/// (deck edges, crown / pivot, gutter lines, kinked-end break points); along, into PANELS at lines where it kinks across the whole
/// width (radial lines at cross-slope table stations), so every kink is a face boundary. Each panel x band top is one tensor NURBS
/// surface (u along the panel, v across the band). Rows of a panel share one u knot vector; neighbouring bands share their boundary
/// row exactly, and neighbouring panels their boundary cross curve (same rows across, same exact end points). The generator supplies
/// the EXACT top point of row v of band b in panel p at u (<see cref="Create"/>).
/// </summary>
public sealed class NurbsSlab
{
    /// <summary>Top surfaces [panel][band].</summary>
    public required NurbsSurface[][] Top { get; init; }
    public required double Depth { get; init; }
    public required double MaxDeviation { get; init; }
    public required double Tolerance { get; init; }
    public int Panels => Top.Length;

    /// <summary>Fits the slab; null with the reason when the tolerance is not reached.</summary>
    /// <param name="bandsOf">number of bands of a panel (panels may split differently across).</param>
    /// <param name="row">(panel, band, v) -> evaluator of the exact top along u in [0, 1]; called once per row.</param>
    public static NurbsSlab? Create(int panels, Func<int, int> bandsOf, Func<int, int, double, Func<double, Vec3>> row, double depth, double tol, out string reason, int maxRows = 32)
    {
        reason = "";
        var top = new NurbsSurface[panels][];
        var pdevs = new double[panels];
        var fails = new string?[panels];
        // panels are independent: fit them in parallel (each result depends only on its own inputs, so the outcome is deterministic)
        Parallel.For(0, panels, pn =>
        {
            int bands = bandsOf(pn);
            var nv = Enumerable.Repeat(3, bands).ToArray();
            for (int iter = 0; ; iter++)
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
                        fns.Add(row(pn, b, (double)j / nv[b]));
                    }
                }
                var fit = NurbsFit.FitCommon(fns.Count, u => fns.Select(f => f(u)).ToArray(), 0, 1, Array.Empty<double>(), tol * 0.5, 3, 0.1);
                if (!fit.Converged) { fails[pn] = $"panel {pn + 1}/{panels}: rows {fit.MaxDeviation * 1000:0.###} mm > {tol * 500:0.###} mm"; return; }
                int nu = fit.Control[0].Length;
                var surf = new NurbsSurface[bands];
                double pdev = fit.MaxDeviation;
                bool again = false;
                var uChecks = Enumerable.Range(0, 65).Select(k => k / 64.0)
                    .Concat(fit.Knots.Distinct().Zip(fit.Knots.Distinct().Skip(1), (a, c) => (a + c) / 2)).ToList();
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
                    var s = surf[b] = new NurbsSurface(3, Math.Min(3, nv[b]), fit.Knots, kv, ctrl);
                    double bd = 0, bu = 0, bvv = 0;
                    for (int j = 0; j < nv[b]; j++)
                    {
                        double v = (j + 0.5) / nv[b];
                        var ex = row(pn, b, v);
                        foreach (double u in uChecks)
                        {
                            // geometric distance (the same-parameter distance overstates it where rows slide along the surface)
                            var q = ex(u);
                            double e = (s.PointAt(u, v) - q).Length;
                            if (e > tol * 0.25) e = (s.ClosestPoint(q, u, v, 12).p - q).Length;
                            if (e > bd) { bd = e; bu = u; bvv = v; }
                        }
                    }
                    pdev = Math.Max(pdev, bd);
                    if (bd > tol)
                    {
                        if (nv[b] * 2 > maxRows) { fails[pn] = $"panel {pn + 1}/{panels}, band {b + 1}/{bands}: across {bd * 1000:0.###} mm > {tol * 1000:0.###} mm at u = {bu:0.####}, v = {bvv:0.####}"; return; }
                        nv[b] *= 2;
                        again = true;
                    }
                }
                if (!again) { top[pn] = surf; pdevs[pn] = pdev; break; }
            }
        });
        if (fails.FirstOrDefault(f => f != null) is { } why) { reason = why; return null; }
        return new NurbsSlab { Top = top, Depth = depth, MaxDeviation = pdevs.Max(), Tolerance = tol };
    }

    public Vec3 TopAt(int panel, int band, double v, double u) => Top[panel][band].PointAt(u, v);

    /// <summary>Parameters u of one panel within <paramref name="chord"/> on every band boundary row.</summary>
    public List<double> Samples(int panel, double chord)
    {
        var rows = new List<NurbsCurve> { Top[panel][0].Boundary(true, false) };
        rows.AddRange(Top[panel].Select(s => s.Boundary(true, true)));
        return NurbsSweep.SampleCurves(rows, chord);
    }

    /// <summary>Integral of Su x Sv over the face (its area vector).</summary>
    public static Vec3 AreaVector(NurbsSurface s)
    {
        var a = Vec3.Zero;
        for (int i = s.DegreeU; i < s.CountU; i++)
        {
            if (s.KnotsU[i + 1] <= s.KnotsU[i]) continue;
            for (int j = s.DegreeV; j < s.CountV; j++)
            {
                if (s.KnotsV[j + 1] <= s.KnotsV[j]) continue;
                foreach (var (u, wu) in Gauss.Points(3, s.KnotsU[i], s.KnotsU[i + 1]))
                    foreach (var (v, wv) in Gauss.Points(3, s.KnotsV[j], s.KnotsV[j + 1]))
                    {
                        var (_, su, sv) = s.Eval(u, v);
                        a += Vec3.Cross(su, sv) * (wu * wv);
                    }
            }
        }
        return a;
    }

    /// <summary>Closed B-rep: panel x band tops and bottoms, a ruled end face per band at the slab ends, ruled side faces per panel.</summary>
    public NurbsSolid ToSolid(string kind)
    {
        var solid = new NurbsSolid { Kind = kind, Tolerance = Tolerance, MaxDeviation = MaxDeviation };
        var down = new Vec3(0, 0, -Depth);
        void Add(NurbsSurface s, Vec3 outward, string role)
        {
            // orientation from the whole face's area vector (robust where parts of a face are degenerate, e.g. at clamped panels)
            var av = AreaVector(s);
            if (av.Length < 1e-12) return;   // zero-area face (a panel row of no length): nothing to bound
            if (Vec3.Dot(av, outward) < 0) s = s.ReversedV();
            solid.Faces.Add(new NurbsFace { Surface = s, Role = role });
        }
        for (int pn = 0; pn < Panels; pn++)
            for (int b = 0; b < Top[pn].Length; b++)
            {
                var t = Top[pn][b];
                Add(t, new Vec3(0, 0, 1), "top");
                Add(t.Translated(down), new Vec3(0, 0, -1), "bottom");
                foreach (bool end in new[] { false, true })
                {
                    if (end ? pn != Panels - 1 : pn != 0) continue;
                    var c = t.Boundary(false, end);
                    // outward: away from the farthest panel centre (end panels may be slivers along the end line: no local direction)
                    var fc = c.PointAt((c.Start + c.End) / 2);
                    var inner = Enumerable.Range(0, Panels).Select(q => Top[q][Top[q].Length / 2].PointAt(0.5, 0.5)).MaxBy(x => (x - fc).Length);
                    Add(NurbsSurface.Ruled(c, c.Translated(down)), fc - inner, end ? "end 1" : "end 0");
                }
            }
        for (int pn = 0; pn < Panels; pn++)
            foreach (bool right in new[] { false, true })
            {
                var t = Top[pn][right ? Top[pn].Length - 1 : 0];
                var c = t.Boundary(true, right);
                var sv = Vec3.Zero;
                for (int k = 0; k <= 8; k++) sv += t.Eval(k / 8.0, right ? t.V1 : t.V0).sv;
                Add(NurbsSurface.Ruled(c, c.Translated(down)), right ? sv : -sv, right ? "side 1" : "side 0");
            }
        return solid;
    }
}
