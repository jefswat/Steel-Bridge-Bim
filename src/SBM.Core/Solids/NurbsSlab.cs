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
    /// <summary>OPTIONAL bottom surfaces [panel][band] fitted from an exact bottom (null = the top translated down by <see cref="Depth"/>).
    /// They share the top's u knots and v parameters, so side and end faces are ruled between matching top / bottom boundary curves.</summary>
    public NurbsSurface[][]? Bottom { get; init; }
    public required double Depth { get; init; }
    public required double MaxDeviation { get; init; }
    public required double Tolerance { get; init; }
    public int Panels => Top.Length;

    /// <summary>Fits the slab; null with the reason when the tolerance is not reached. When every panel has the same number of bands, a band
    /// has the SAME rows across (count and v parameters) in every panel, so two panels meeting at a common boundary have identical cross curves
    /// there (the curves interpolate the same exact points): their faces share those edges exactly, also where a band has no length in one panel
    /// (beyond a skewed deck end).</summary>
    /// <param name="bandsOf">number of bands of a panel (panels may split differently across).</param>
    /// <param name="row">(panel, band, v) -> evaluator of the exact top along u in [0, 1]; called once per row.</param>
    /// <param name="bottomRow">OPTIONAL exact bottom (same signature as <paramref name="row"/>); null = constant vertical depth. Top and bottom
    /// rows are then fitted on ONE u knot vector per panel and interpolated across with the same rows, so every top / bottom boundary pair is
    /// compatible (ruled sides and ends).</param>
    public static NurbsSlab? Create(int panels, Func<int, int> bandsOf, Func<int, int, double, Func<double, Vec3>> row, double depth, double tol, out string reason, int maxRows = 32,
        Func<int, int, double, Func<double, Vec3>>? bottomRow = null)
    {
        reason = "";
        var bandCount = Enumerable.Range(0, panels).Select(bandsOf).ToArray();
        bool shared = bandCount.Distinct().Count() == 1;
        var nv = bandCount.Select(n => Enumerable.Repeat(3, n).ToArray()).ToArray();
        var top = new NurbsSurface[panels][];
        var bot = bottomRow != null ? new NurbsSurface[panels][] : null;
        var pdevs = new double[panels];
        var fails = new string?[panels];
        var need = new string?[panels][];
        var todo = Enumerable.Range(0, panels).ToList();
        while (todo.Count > 0)
        {
            // panels are independent: fit them in parallel (each result depends only on its own inputs, so the outcome is deterministic)
            Parallel.ForEach(todo, pn => FitPanel(pn, panels, nv[pn], row, bottomRow, tol, top, bot, pdevs, fails, need));
            if (fails.FirstOrDefault(f => f != null) is { } why) { reason = why; return null; }
            var next = new HashSet<int>();
            if (shared)
            {
                for (int b = 0; b < bandCount[0]; b++)
                {
                    int p = Array.FindIndex(need, x => x?[b] != null);
                    if (p < 0) continue;
                    if (nv[0][b] * 2 > maxRows) { reason = need[p]![b]!; return null; }
                    for (int q = 0; q < panels; q++) { nv[q][b] *= 2; next.Add(q); }
                }
            }
            else
                for (int p = 0; p < panels; p++)
                    for (int b = 0; b < bandCount[p]; b++)
                    {
                        if (need[p]?[b] == null) continue;
                        if (nv[p][b] * 2 > maxRows) { reason = need[p]![b]!; return null; }
                        nv[p][b] *= 2;
                        next.Add(p);
                    }
            todo = next.OrderBy(x => x).ToList();
        }
        return new NurbsSlab { Top = top, Bottom = bot, Depth = bottomRow != null ? 0 : depth, MaxDeviation = pdevs.Max(), Tolerance = tol };
    }

    /// <summary>One panel with the given rows per band: top (and bottom) rows on one u knot vector, interpolated across per band. need[pn][b] =
    /// the reason when band b misses the tolerance across (more rows wanted); fails[pn] = the reason when the rows themselves do not fit.</summary>
    private static void FitPanel(int pn, int panels, int[] nv, Func<int, int, double, Func<double, Vec3>> row, Func<int, int, double, Func<double, Vec3>>? bottomRow,
        double tol, NurbsSurface[][] top, NurbsSurface[][]? bot, double[] pdevs, string?[] fails, string?[][] need)
    {
        int bands = nv.Length;
        // rows: band b, j = 0..nv[b]; the top row (b, 0) is row (b-1, nv[b-1]); bottoms: every band its own rows (the bottom may step at a band
        // boundary: one-sided values, a riser face between)
        var fns = new List<Func<double, Vec3>>();
        var bfns = new List<Func<double, Vec3>>();
        var index = new int[bands][];
        var bindex = new int[bands][];
        for (int b = 0; b < bands; b++)
        {
            index[b] = new int[nv[b] + 1];
            bindex[b] = new int[nv[b] + 1];
            for (int j = 0; j <= nv[b]; j++)
            {
                double v = (double)j / nv[b];
                if (bottomRow != null) { bindex[b][j] = bfns.Count; bfns.Add(bottomRow(pn, b, v)); }
                if (j == 0 && b > 0) { index[b][0] = index[b - 1][nv[b - 1]]; continue; }
                index[b][j] = fns.Count;
                fns.Add(row(pn, b, v));
            }
        }
        int nr = fns.Count;
        var all = fns.Concat(bfns).ToList();
        var fit = NurbsFit.FitCommon(all.Count, u => all.Select(f => f(u)).ToArray(), 0, 1, Array.Empty<double>(), tol * 0.5, 3, 0.1);
        if (!fit.Converged) { fails[pn] = $"panel {pn + 1}/{panels}: rows {fit.MaxDeviation * 1000:0.###} mm > {tol * 500:0.###} mm"; return; }
        int nu = fit.Control[0].Length;
        var surf = new NurbsSurface[bands];
        var bsurf = new NurbsSurface[bands];
        double pdev = fit.MaxDeviation;
        var needs = new string?[bands];
        var uChecks = Enumerable.Range(0, 65).Select(k => k / 64.0)
            .Concat(fit.Knots.Distinct().Zip(fit.Knots.Distinct().Skip(1), (a, c) => (a + c) / 2)).ToList();
        for (int b = 0; b < bands; b++)
        {
            var t = Enumerable.Range(0, nv[b] + 1).Select(j => (double)j / nv[b]).ToArray();
            double bd = 0, bu = 0, bvv = 0;
            foreach (bool isBot in bottomRow != null ? new[] { false, true } : new[] { false })
            {
                double[] kv = Array.Empty<double>();
                var ctrl = new Vec3[nu, nv[b] + 1];
                for (int i = 0; i < nu; i++)
                {
                    var (k, c) = NurbsFit.Interpolate(t, t.Select((_, j) => fit.Control[isBot ? nr + bindex[b][j] : index[b][j]][i]).ToArray(), 3);
                    kv = k;
                    for (int j = 0; j < c.Length; j++) ctrl[i, j] = c[j];
                }
                var s = new NurbsSurface(3, Math.Min(3, nv[b]), fit.Knots, kv, ctrl);
                if (isBot) bsurf[b] = s; else surf[b] = s;
                for (int j = 0; j < nv[b]; j++)
                {
                    double v = (j + 0.5) / nv[b];
                    var ex = isBot ? bottomRow!(pn, b, v) : row(pn, b, v);
                    foreach (double u in uChecks)
                    {
                        // geometric distance (the same-parameter distance overstates it where rows slide along the surface)
                        var q = ex(u);
                        double e = (s.PointAt(u, v) - q).Length;
                        if (e > tol * 0.25) e = (s.ClosestPoint(q, u, v, 12).p - q).Length;
                        if (e > bd) { bd = e; bu = u; bvv = v; }
                    }
                }
            }
            pdev = Math.Max(pdev, bd);
            if (bd > tol) needs[b] = $"panel {pn + 1}/{panels}, band {b + 1}/{bands}: across {bd * 1000:0.###} mm > {tol * 1000:0.###} mm at u = {bu:0.####}, v = {bvv:0.####}";
        }
        top[pn] = surf;
        if (bot != null) bot[pn] = bsurf;
        pdevs[pn] = pdev;
        need[pn] = needs;
    }

    public Vec3 TopAt(int panel, int band, double v, double u) => Top[panel][band].PointAt(u, v);
    public Vec3 BottomAt(int panel, int band, double v, double u) => Bottom != null ? Bottom[panel][band].PointAt(u, v) : TopAt(panel, band, v, u) - new Vec3(0, 0, Depth);

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

    private static bool Steps(NurbsCurve a, NurbsCurve c) => a.Control.Zip(c.Control).Any(x => (x.First - x.Second).Length > 1e-9);

    /// <summary>The planar face of slab-end bands [b0, b1] (u = 0 of the first panel / u = 1 of the last) bounded by their top and bottom curves,
    /// the vertical lines at the run's sides and the risers between its bands; null when those curves do not lie in one plane (1 micron).</summary>
    private NurbsFace? PlanarEnd(bool end, Vec3 down, int b0, int b1)
    {
        int pn = end ? Panels - 1 : 0;
        var tops = Enumerable.Range(b0, b1 - b0 + 1).Select(b => Top[pn][b].Boundary(false, end)).ToList();
        var bots = Enumerable.Range(b0, b1 - b0 + 1).Select(b => Bottom != null ? Bottom[pn][b].Boundary(false, end) : Top[pn][b].Boundary(false, end).Translated(down)).ToList();
        static bool Dot(NurbsCurve c) => (c.PointAt(c.End) - c.PointAt(c.Start)).Length < 1e-9 && (c.PointAt((c.Start + c.End) / 2) - c.PointAt(c.Start)).Length < 1e-9;
        var loop = new List<NurbsCurve>();
        foreach (var c in tops) if (!Dot(c)) loop.Add(c);
        Vec3 Tend = tops[^1].PointAt(tops[^1].End), Bend = bots[^1].PointAt(bots[^1].End);
        if ((Tend - Bend).Length > 1e-9) loop.Add(Line(Tend, Bend));
        for (int b = bots.Count - 1; b >= 0; b--)
        {
            if (!Dot(bots[b])) loop.Add(Reversed(bots[b]));
            if (b > 0)
            {
                Vec3 p = bots[b].PointAt(bots[b].Start), q = bots[b - 1].PointAt(bots[b - 1].End);
                if ((p - q).Length > 1e-9) loop.Add(Line(p, q));
            }
        }
        Vec3 Bst = bots[0].PointAt(bots[0].Start), Tst = tops[0].PointAt(tops[0].Start);
        if ((Bst - Tst).Length > 1e-9) loop.Add(Line(Bst, Tst));
        if (loop.Count < 3) return null;
        // dense sampling (polygon for the consumers) and the plane test
        var poly = new List<Vec3>();
        foreach (var c in loop)
        {
            int n = c.Degree == 1 && c.Control.Length == 2 ? 1 : 32;
            for (int i = 0; i < n; i++) poly.Add(c.PointAt(c.Start + (c.End - c.Start) * i / n));
        }
        var nrm = Vec3.Zero;
        for (int i = 0; i < poly.Count; i++) nrm += Vec3.Cross(poly[i] - poly[0], poly[(i + 1) % poly.Count] - poly[0]);
        if (nrm.Length < 1e-12) return null;
        nrm = nrm.Normalized();
        if (poly.Any(q => Math.Abs(Vec3.Dot(q - poly[0], nrm)) > 1e-6)) return null;
        foreach (var c in loop) foreach (var q in c.Control) if (Math.Abs(Vec3.Dot(q - poly[0], nrm)) > 1e-6) return null;
        // outward: away from the slab (the far panel's middle)
        var fc = poly.Aggregate(Vec3.Zero, (s, q) => s + (q - poly[0])) * (1.0 / poly.Count) + poly[0];
        var inner = Enumerable.Range(0, Panels).Select(q => Top[q][Top[q].Length / 2].PointAt(0.5, 0.5)).MaxBy(x => (x - fc).Length);
        if (Vec3.Dot(nrm, fc - inner) < 0) poly.Reverse();
        return new NurbsFace { Polygon = poly.ToArray(), Loop = loop, Role = end ? "end 1" : "end 0" };
    }

    /// <summary>A slab end as planar faces over maximal runs of coplanar bands (a straight end = one face; a kinked end = one per straight
    /// piece); a band whose end is not planar keeps its ruled face (index list of those bands). Null when a bottom step falls between two runs
    /// or on a ruled band (the faces would not share their edges): the caller then uses the ruled faces throughout.</summary>
    private (List<NurbsFace> faces, List<int> ruled)? EndFaces(bool end, Vec3 down)
    {
        int pn = end ? Panels - 1 : 0, nb = Top[pn].Length;
        var faces = new List<NurbsFace>();
        var ruled = new List<int>();
        var runEnds = new List<int>();
        int b0 = 0;
        while (b0 < nb)
        {
            if (PlanarEnd(end, down, b0, b0) is not { } f) { ruled.Add(b0); runEnds.Add(b0); b0++; continue; }
            int b1 = b0;
            while (b1 + 1 < nb && PlanarEnd(end, down, b0, b1 + 1) is { } g) { f = g; b1++; }
            faces.Add(f);
            runEnds.Add(b1);
            b0 = b1 + 1;
        }
        if (Bottom != null)
            foreach (int b in runEnds.Where(b => b + 1 < nb))
            {
                var a = Bottom[pn][b].Boundary(false, end); var c = Bottom[pn][b + 1].Boundary(false, end);
                if ((a.PointAt(a.End) - c.PointAt(c.Start)).Length > 1e-9) return null;
            }
        return (faces, ruled);
    }

    private static NurbsCurve Line(Vec3 a, Vec3 b) => new(1, new double[] { 0, 0, 1, 1 }, new[] { a, b });

    private static NurbsCurve Reversed(NurbsCurve c)
    {
        double a = c.Knots[0], b = c.Knots[^1];
        return new NurbsCurve(c.Degree, c.Knots.Reverse().Select(k => a + b - k).ToArray(), c.Control.Reverse().ToArray(), c.Weights?.Reverse().ToArray());
    }

    /// <summary>Closed B-rep: panel x band tops and bottoms, a ruled end face per band at the slab ends, ruled side faces per panel.</summary>
    public NurbsSolid ToSolid(string kind)
    {
        var solid = new NurbsSolid { Kind = kind, Tolerance = Tolerance, MaxDeviation = MaxDeviation };
        var down = new Vec3(0, 0, -Depth);
        // each slab end as ONE planar face bounded by the top and bottom curves of all bands (+ straight sides and risers) when they lie in one
        // plane (straight deck-end line); else per-band ruled faces as before
        var endFaces = new[] { EndFaces(false, down), EndFaces(true, down) };
        foreach (var e in endFaces) if (e != null) solid.Faces.AddRange(e.Value.faces);
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
                var tb = Bottom?[pn][b];
                Add(t, new Vec3(0, 0, 1), "top");
                Add(tb ?? t.Translated(down), new Vec3(0, 0, -1), "bottom");
                foreach (bool end in new[] { false, true })
                {
                    if (end ? pn != Panels - 1 : pn != 0) continue;
                    if (endFaces[end ? 1 : 0] is { } ef && !ef.ruled.Contains(b)) continue;      // in a planar end face (added above)
                    var c = t.Boundary(false, end);
                    // outward: away from the farthest panel centre (end panels may be slivers along the end line: no local direction)
                    var fc = c.PointAt((c.Start + c.End) / 2);
                    var inner = Enumerable.Range(0, Panels).Select(q => Top[q][Top[q].Length / 2].PointAt(0.5, 0.5)).MaxBy(x => (x - fc).Length);
                    Add(NurbsSurface.Ruled(c, tb != null ? tb.Boundary(false, end) : c.Translated(down)), fc - inner, end ? "end 1" : "end 0");
                }
            }
        for (int pn = 0; pn < Panels; pn++)
            foreach (bool right in new[] { false, true })
            {
                int bi = right ? Top[pn].Length - 1 : 0;
                var t = Top[pn][bi];
                var c = t.Boundary(true, right);
                var sv = Vec3.Zero;
                for (int k = 0; k <= 8; k++) sv += t.Eval(k / 8.0, right ? t.V1 : t.V0).sv;
                Add(NurbsSurface.Ruled(c, Bottom != null ? Bottom[pn][bi].Boundary(true, right) : c.Translated(down)), right ? sv : -sv, right ? "side 1" : "side 0");
            }
        // risers where the bottom steps between two bands (a vertical face along the panel; outward toward the shallower side)
        if (Bottom != null)
            for (int pn = 0; pn < Panels; pn++)
                for (int b = 1; b < Top[pn].Length; b++)
                {
                    var a = Bottom[pn][b - 1].Boundary(true, true);
                    var c = Bottom[pn][b].Boundary(true, false);
                    if (!Steps(a, c)) continue;
                    var t = Top[pn][b];
                    var sv = Vec3.Zero;
                    for (int k = 0; k <= 8; k++) sv += t.Eval(k / 8.0, t.V0).sv;
                    double za = a.PointAt((a.Start + a.End) / 2).Z, zc = c.PointAt((c.Start + c.End) / 2).Z;
                    Add(NurbsSurface.Ruled(a, c), za < zc ? sv : -sv, "riser");
                }
        return solid;
    }
}
