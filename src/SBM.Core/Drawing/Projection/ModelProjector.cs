using System.Collections.Concurrent;
using System.Diagnostics;
using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Drawing.Projection;

/// <summary>
/// General 3D -> 2D projection of model solids with hidden-line removal (pure, no UI).
/// <list type="number">
/// <item>Feature edges per mesh (<see cref="FeatureEdges"/>: plane-merged faces, sharp / boundary edges, smooth edges kept only where they
/// are silhouettes for this view; exact <see cref="ExtrudedSolid"/> recipe edges when the recipes describe the whole mesh).</item>
/// <item>Edges and occluding triangles are mapped to view space (x, y, depth) by an <see cref="IViewMap"/> (orthographic or developed),
/// clipped to the depth slab (the near plane cuts solids: the cut outline is drawn and the cut is capped so the solid stays opaque) and to
/// an optional 2D window.</item>
/// <item>Hidden-line removal: <see cref="HlrMethod.Exact"/> splits every edge exactly against the occluding triangles (2D BVH with
/// per-node min depth, parallel over edges); <see cref="HlrMethod.DepthBuffer"/> samples a z-buffer (interactive use).</item>
/// </list>
/// Output: a <see cref="PlanDrawing"/> with the 3D layer names (visible) and "&lt;layer&gt;-HIDDEN" (dashed), every polyline tagged with
/// its element key. Hook for <c>sbm section</c>: a section view = an <see cref="OrthographicView"/> whose origin is on the cut plane with
/// <c>Near = 0</c>; the cut outline comes back on the visible layers and everything beyond is the "beyond" projection.
/// </summary>
public static class ModelProjector
{
    public const string HiddenSuffix = "-HIDDEN";

    private struct Seg { public Vec3 A, B; public int Item; }
    private readonly record struct Piece(Vec3 A, Vec3 B, int Item, bool Hidden);

    public static ProjectionResult Project(IReadOnlyList<ProjectionItem> items, IViewMap view, ProjectionOptions? options = null)
    {
        var o = options ?? new ProjectionOptions();
        var stats = new ProjectionStats { Items = items.Count };
        var total = Stopwatch.StartNew();
        var po = new ParallelOptions { MaxDegreeOfParallelism = o.Threads > 0 ? o.Threads : -1 };

        // ---- 1. edges (view-independent extraction cached per mesh) -> view space
        var sw = Stopwatch.StartNew();
        var edgeSets = new MeshEdges?[items.Count];
        var segLists = new List<Seg>[items.Count];
        var cutLists = new List<Seg>?[items.Count];
        Parallel.For(0, items.Count, po, i =>
        {
            var it = items[i];
            var list = new List<Seg>();
            if (it.Mesh is { IsEmpty: false } m)
            {
                var me = FeatureEdges.Of(m, o.SharpAngleDegrees, o.UseRecipes, o.Holes, o.HoleSides);
                edgeSets[i] = me;
                Vec3 dConst = view.IsLinear ? view.ViewDir(Vec3.Zero) : default;
                foreach (var e in me.Edges)
                {
                    var d = view.IsLinear ? dConst : view.ViewDir((e.A + e.B) * 0.5);
                    if (!e.Drawn(d)) continue;
                    AddMapped(list, view, e.A, e.B, i, o);
                }
            }
            if (it.Lines != null)
                foreach (var pl in it.Lines)
                    for (int k = 0; k + 1 < pl.Length; k++) AddMapped(list, view, pl[k], pl[k + 1], i, o);
            segLists[i] = list;
        });
        stats.EdgeTime = sw.Elapsed;

        // ---- 2. occluders in view space (back faces of closed solids culled; slab-clipped; near-plane caps)
        sw.Restart();
        var triLists = new List<double>?[items.Count];
        long triCount = 0;
        Parallel.For(0, items.Count, po, i =>
        {
            var it = items[i];
            if (it.Mesh is not { IsEmpty: false } m) return;
            Interlocked.Add(ref triCount, m.TriangleCount);
            if (!it.Occludes || o.Hidden == HiddenLines.Off && view.Near == null) return;
            bool closed = edgeSets[i]?.Closed ?? false;
            var mp = new Vec3[m.Positions.Count];
            for (int k = 0; k < mp.Length; k++) mp[k] = view.Map(m.Positions[k]);
            var tris = new List<double>();
            List<(Vec2, Vec2)>? cut = view.Near != null && closed ? new() : null;
            var poly = new List<Vec3>(8);
            var tmp = new List<Vec3>(8);
            for (int t = 0; t + 2 < m.Indices.Count; t += 3)
            {
                var a = mp[m.Indices[t]]; var b = mp[m.Indices[t + 1]]; var c = mp[m.Indices[t + 2]];
                double area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (view.Near is { } nr && cut != null) CutSegment(a, b, c, nr, cut);
                if (Math.Abs(area) < 1e-12) continue;
                if (closed && area < 0) continue;
                if (view.Near == null && view.Far == null) { Push(tris, a, b, c); continue; }
                poly.Clear(); poly.Add(a); poly.Add(b); poly.Add(c);
                if (view.Near is { } n0) { ClipDepth(poly, tmp, n0, true); (poly, tmp) = (tmp, poly); }
                if (view.Far is { } f0 && poly.Count >= 3) { ClipDepth(poly, tmp, f0, false); (poly, tmp) = (tmp, poly); }
                for (int k = 1; k + 1 < poly.Count; k++) Push(tris, poly[0], poly[k], poly[k + 1]);
            }
            if (cut is { Count: > 0 })
            {
                // Cut outline (drawn, visible: nothing is in front of the near plane) + caps (opaque section faces).
                double nz = view.Near!.Value;
                var cl = new List<Seg>();
                foreach (var (p, q) in cut) cl.Add(new Seg { A = new Vec3(p.X, p.Y, nz), B = new Vec3(q.X, q.Y, nz), Item = i });
                cutLists[i] = cl;
                foreach (var (p, q, r) in CapTriangles(cut)) Push(tris, new Vec3(p.X, p.Y, nz), new Vec3(q.X, q.Y, nz), new Vec3(r.X, r.Y, nz));
            }
            triLists[i] = tris;
        });
        stats.Triangles = triCount;
        int nTri = triLists.Sum(l => l == null ? 0 : l.Count / 9);
        var T = new double[(long)nTri * 9];
        int[]? own = items.Any(it => !it.OccludesOthers) ? new int[nTri] : null;   // -1 = hides everything, else hides only that item's own edges
        {
            long at = 0;
            for (int i = 0; i < triLists.Length; i++)
            {
                var l = triLists[i];
                if (l == null) continue;
                l.CopyTo(0, T, (int)at, l.Count);
                if (own != null) Array.Fill(own, items[i].OccludesOthers ? -1 : i, (int)(at / 9), l.Count / 9);
                at += l.Count;
            }
        }
        stats.Occluders = nTri;
        stats.OccluderTime = sw.Elapsed;

        // ---- 3. hidden-line removal
        sw.Restart();
        var segs = new List<Seg>();
        foreach (var l in segLists) segs.AddRange(l);
        stats.Edges = segs.Count;
        if (o.Window is { } win) segs = segs.Select(s => ClipWindow(s, win)).Where(s => s.HasValue).Select(s => s!.Value).ToList();
        var pieces = new ConcurrentBag<List<Piece>>();
        if (o.Hidden == HiddenLines.Off || nTri == 0)
            pieces.Add(segs.Select(s => new Piece(s.A, s.B, s.Item, false)).ToList());
        else if (o.Method == HlrMethod.Exact)
        {
            var bvh = new TriBvh(T, nTri) { Own = own };
            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, segs.Count, Math.Max(64, segs.Count / (Environment.ProcessorCount * 8) + 1)), po,
                () => new List<Piece>(),
                (range, _, local) =>
                {
                    var iv = new List<(double, double)>();
                    var stack = new int[128];
                    for (int k = range.Item1; k < range.Item2; k++) Split(segs[k], bvh, o.DepthTolerance, iv, ref stack, local);
                    return local;
                },
                local => pieces.Add(local));
        }
        else
        {
            var zb = new DepthBuffer(T, nTri, segs, o, po, own);
            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, segs.Count, 512), po,
                () => new List<Piece>(),
                (range, _, local) => { for (int k = range.Item1; k < range.Item2; k++) zb.Split(segs[k], o.DepthTolerance, local); return local; },
                local => pieces.Add(local));
        }
        var all = pieces.SelectMany(p => p).ToList();
        foreach (var cl in cutLists)
            if (cl != null)
                foreach (var s in cl)
                    if ((o.Window is { } w2 ? ClipWindow(s, w2) : s) is { } c2) all.Add(new Piece(c2.A, c2.B, c2.Item, false));
        if (o.Merge) all = MergeOverlaps(all);
        stats.HlrTime = sw.Elapsed;

        // ---- 4. output drawing
        var dwg = new PlanDrawing();
        var result = new ProjectionResult { Drawing = dwg, Stats = stats, View = view };
        foreach (var it in items)
            result.Elements.TryAdd(it.Key, new ProjectedElement(it.Key, it.Layer, it.Kind, it.Name, it.Bridge, it.Source));
        Vec3 Out(Vec3 v) => new(v.X, v.Y, o.KeepDepth ? -v.Z : 0);
        foreach (var g in all.Where(p => !p.Hidden || o.Hidden == HiddenLines.Show).GroupBy(p => (p.Item, p.Hidden)).OrderBy(g => g.Key.Item).ThenBy(g => g.Key.Hidden))
        {
            var it = items[g.Key.Item];
            var layer = dwg.Layer(g.Key.Hidden ? ModelLayers.HiddenStyle(it.Layer) : ModelLayers.VisibleStyle(it.Layer));
            var list = g.OrderBy(p => p.A.X).ThenBy(p => p.A.Y).ThenBy(p => p.B.X).ThenBy(p => p.B.Y).ToList();
            foreach (var p in list)
            {
                double len = (p.B.XY - p.A.XY).Length;
                if (p.Hidden) { stats.HiddenPieces++; stats.HiddenLength += len; } else { stats.VisiblePieces++; stats.VisibleLength += len; }
            }
            foreach (var pl in o.Chain ? ChainPieces(list) : list.Select(p => new List<Vec3> { p.A, p.B }).ToList())
            {
                bool closedPl = pl.Count > 3 && (pl[0] - pl[^1]).Length < 1e-9;
                var pts = (closedPl ? pl.Take(pl.Count - 1) : pl).Select(Out).ToArray();
                layer.Polylines.Add(new Polyline3(pts, closedPl, it.Key));
            }
        }
        stats.Total = total.Elapsed;
        return result;
    }

    // ------------------------------------------------------------------ mapping and clipping

    private static void AddMapped(List<Seg> list, IViewMap view, Vec3 a, Vec3 b, int item, ProjectionOptions o)
    {
        if (view.IsLinear) { AddSlab(list, view, view.Map(a), view.Map(b), item); return; }
        int n = Math.Max(1, (int)Math.Ceiling((b - a).Length / Math.Max(o.MaxSegment, 1e-3)));
        var prev = view.Map(a);
        for (int k = 1; k <= n; k++)
        {
            var cur = view.Map(Vec3.Lerp(a, b, (double)k / n));
            AddSlab(list, view, prev, cur, item);
            prev = cur;
        }
    }

    private static void AddSlab(List<Seg> list, IViewMap view, Vec3 a, Vec3 b, int item)
    {
        double t0 = 0, t1 = 1, dd = b.Z - a.Z;
        if (view.Near is { } n)
        {
            if (Math.Abs(dd) < 1e-15) { if (a.Z < n) return; }
            else { double t = (n - a.Z) / dd; if (dd > 0) t0 = Math.Max(t0, t); else t1 = Math.Min(t1, t); }
        }
        if (view.Far is { } f)
        {
            if (Math.Abs(dd) < 1e-15) { if (a.Z > f) return; }
            else { double t = (f - a.Z) / dd; if (dd > 0) t1 = Math.Min(t1, t); else t0 = Math.Max(t0, t); }
        }
        if (t1 - t0 < 1e-12) return;
        var p = Vec3.Lerp(a, b, t0); var q = Vec3.Lerp(a, b, t1);
        if ((q.XY - p.XY).Length < 1e-9) return;   // edge seen end-on
        list.Add(new Seg { A = p, B = q, Item = item });
    }

    private static Seg? ClipWindow(Seg s, Box2 w)
    {
        double t0 = 0, t1 = 1;
        double dx = s.B.X - s.A.X, dy = s.B.Y - s.A.Y;
        bool Clip(double p, double q)
        {
            if (Math.Abs(p) < 1e-18) return q >= 0;
            double r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
        if (!Clip(-dx, s.A.X - w.MinX) || !Clip(dx, w.MaxX - s.A.X) || !Clip(-dy, s.A.Y - w.MinY) || !Clip(dy, w.MaxY - s.A.Y)) return null;
        if (t1 - t0 < 1e-12) return null;
        return new Seg { A = Vec3.Lerp(s.A, s.B, t0), B = Vec3.Lerp(s.A, s.B, t1), Item = s.Item };
    }

    private static void Push(List<double> t, Vec3 a, Vec3 b, Vec3 c)
    {
        t.Add(a.X); t.Add(a.Y); t.Add(a.Z); t.Add(b.X); t.Add(b.Y); t.Add(b.Z); t.Add(c.X); t.Add(c.Y); t.Add(c.Z);
    }

    /// <summary>Sutherland-Hodgman against depth >= plane (keepAbove) or depth &lt;= plane.</summary>
    private static void ClipDepth(List<Vec3> src, List<Vec3> dst, double plane, bool keepAbove)
    {
        dst.Clear();
        for (int i = 0; i < src.Count; i++)
        {
            var p = src[i]; var q = src[(i + 1) % src.Count];
            bool pin = keepAbove ? p.Z >= plane : p.Z <= plane, qin = keepAbove ? q.Z >= plane : q.Z <= plane;
            if (pin) dst.Add(p);
            if (pin != qin) dst.Add(Vec3.Lerp(p, q, (plane - p.Z) / (q.Z - p.Z)));
        }
    }

    private static void CutSegment(Vec3 a, Vec3 b, Vec3 c, double plane, List<(Vec2, Vec2)> cut)
    {
        double da = a.Z - plane, db = b.Z - plane, dc = c.Z - plane;
        if ((da > 0 && db > 0 && dc > 0) || (da < 0 && db < 0 && dc < 0)) return;
        Span<Vec2> pts = stackalloc Vec2[3];
        int n = 0;
        void E(Vec3 p, Vec3 q, double dp, double dq, Span<Vec2> buf)
        {
            if ((dp > 0) == (dq > 0) && dp != 0 && dq != 0) return;
            if (Math.Abs(dp - dq) < 1e-15 || n >= 3) return;
            double s = dp / (dp - dq);
            if (s < 0 || s > 1) return;
            buf[n++] = (p + (q - p) * s).XY;
        }
        E(a, b, da, db, pts); E(b, c, db, dc, pts); E(c, a, dc, da, pts);
        if (n < 2) return;
        var p0 = pts[0]; var p1 = pts[n - 1];
        if ((p1 - p0).Length > 1e-7) cut.Add((p0, p1));
    }

    /// <summary>Triangles filling the closed loops of a cut (even-odd nesting: holes stay open).</summary>
    private static IEnumerable<(Vec2, Vec2, Vec2)> CapTriangles(List<(Vec2, Vec2)> cut)
    {
        var loops = MeshSection.Loops(cut, 1e-5);
        var res = new List<(Vec2, Vec2, Vec2)>();
        if (loops.Count == 0) return res;
        static bool Inside(Vec2 p, List<Vec2> poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
            return c;
        }
        var depth = loops.Select((l, i) => loops.Where((m, j) => j != i && Inside(l[0], m)).Count()).ToList();
        for (int i = 0; i < loops.Count; i++)
        {
            if (depth[i] % 2 != 0) continue;
            var outer = loops[i].ToList();
            if (MeshBuilder.SignedArea(outer) < 0) outer.Reverse();
            var holes = new List<Vec2[]>();
            for (int j = 0; j < loops.Count; j++)
                if (depth[j] == depth[i] + 1 && Inside(loops[j][0], loops[i])) holes.Add(loops[j].ToArray());
            try
            {
                var (v, tris) = PlateBuilder.TriangulateWithHoles(outer, holes);
                foreach (var (a, b, c) in tris) res.Add((v[a], v[b], v[c]));
            }
            catch (Exception)
            {
                foreach (var (a, b, c) in MeshBuilder.Triangulate(outer)) res.Add((outer[a], outer[b], outer[c]));
            }
        }
        return res;
    }

    // ------------------------------------------------------------------ exact HLR

    /// <summary>2D BVH over view-space triangles (9 doubles each: x, y, depth per vertex) with the minimum depth per node.</summary>
    private sealed class TriBvh
    {
        public readonly double[] T;
        public int[]? Own { get; init; }
        public readonly int[] Order;
        public readonly List<(double x0, double y0, double x1, double y1, double minD, int a, int b, bool leaf)> Nodes = new();

        public TriBvh(double[] t, int n)
        {
            T = t;
            Order = Enumerable.Range(0, n).ToArray();
            var cx = new double[n]; var cy = new double[n];
            for (int i = 0; i < n; i++) { long k = (long)i * 9; cx[i] = (t[k] + t[k + 3] + t[k + 6]) / 3; cy[i] = (t[k + 1] + t[k + 4] + t[k + 7]) / 3; }
            if (n > 0) Build(0, n, cx, cy);
        }

        private int Build(int start, int count, double[] cx, double[] cy)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, md = double.MaxValue;
            double cx0 = double.MaxValue, cy0 = double.MaxValue, cx1 = double.MinValue, cy1 = double.MinValue;
            for (int i = start; i < start + count; i++)
            {
                long k = (long)Order[i] * 9;
                for (int v = 0; v < 3; v++)
                {
                    double x = T[k + 3 * v], y = T[k + 3 * v + 1], d = T[k + 3 * v + 2];
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; if (d < md) md = d;
                }
                double px = cx[Order[i]], py = cy[Order[i]];
                if (px < cx0) cx0 = px; if (px > cx1) cx1 = px; if (py < cy0) cy0 = py; if (py > cy1) cy1 = py;
            }
            int me = Nodes.Count;
            Nodes.Add((x0, y0, x1, y1, md, start, count, true));
            if (count <= 6) return me;
            bool alongX = cx1 - cx0 >= cy1 - cy0;
            double mid = alongX ? (cx0 + cx1) / 2 : (cy0 + cy1) / 2;
            var key = alongX ? cx : cy;
            int lo = start, hi = start + count - 1;
            while (lo <= hi)
            {
                if (key[Order[lo]] < mid) lo++;
                else { (Order[lo], Order[hi]) = (Order[hi], Order[lo]); hi--; }
            }
            int nLeft = lo - start;
            if (nLeft == 0 || nLeft == count)
            {
                Array.Sort(Order, start, count, Comparer<int>.Create((p, q) => key[p].CompareTo(key[q])));
                nLeft = count / 2;
            }
            int l = Build(start, nLeft, cx, cy);
            int r = Build(start + nLeft, count - nLeft, cx, cy);
            Nodes[me] = (x0, y0, x1, y1, md, l, r, false);
            return me;
        }
    }

    private static void Split(Seg s, TriBvh bvh, double tol, List<(double, double)> iv, ref int[] stack, List<Piece> outp)
    {
        iv.Clear();
        double sx0 = Math.Min(s.A.X, s.B.X), sx1 = Math.Max(s.A.X, s.B.X), sy0 = Math.Min(s.A.Y, s.B.Y), sy1 = Math.Max(s.A.Y, s.B.Y);
        double sMaxD = Math.Max(s.A.Z, s.B.Z);
        var T = bvh.T;
        int sp = 0;
        if (bvh.Nodes.Count > 0) stack[sp++] = 0;
        double covered = 0;   // rough early exit: fully hidden by one triangle
        while (sp > 0 && covered < 1)
        {
            var nd = bvh.Nodes[stack[--sp]];
            if (nd.minD >= sMaxD - tol || nd.x0 > sx1 || nd.x1 < sx0 || nd.y0 > sy1 || nd.y1 < sy0) continue;
            if (!nd.leaf)
            {
                if (sp + 2 >= stack.Length) Array.Resize(ref stack, stack.Length * 2);
                stack[sp++] = nd.a; stack[sp++] = nd.b;
                continue;
            }
            for (int i = nd.a; i < nd.a + nd.b; i++)
            {
                int ti = bvh.Order[i];
                if (bvh.Own is { } ow && ow[ti] >= 0 && ow[ti] != s.Item) continue;
                long k = (long)ti * 9;
                if (HiddenInterval(s, T, k, tol, out double h0, out double h1))
                {
                    iv.Add((h0, h1));
                    if (h0 <= 1e-12 && h1 >= 1 - 1e-12) covered = 1;
                }
            }
        }
        Emit(s, iv, outp);
    }

    /// <summary>Parameter range of the segment hidden by one triangle (inside it in 2D and behind its plane by more than the tolerance).</summary>
    private static bool HiddenInterval(Seg s, double[] T, long k, double tol, out double h0, out double h1)
    {
        h0 = h1 = 0;
        double x0 = T[k], y0 = T[k + 1], d0 = T[k + 2], x1 = T[k + 3], y1 = T[k + 4], d1 = T[k + 5], x2 = T[k + 6], y2 = T[k + 7], d2 = T[k + 8];
        double minD = Math.Min(d0, Math.Min(d1, d2));
        if (minD >= Math.Max(s.A.Z, s.B.Z) - tol) return false;
        double ex1 = x1 - x0, ey1 = y1 - y0, ex2 = x2 - x0, ey2 = y2 - y0;
        double area = ex1 * ey2 - ey1 * ex2;
        if (Math.Abs(area) < 1e-14) return false;
        double sg = area > 0 ? 1 : -1;
        double t0 = 0, t1 = 1;
        double ax = s.A.X, ay = s.A.Y, dx = s.B.X - ax, dy = s.B.Y - ay;
        for (int e = 0; e < 3; e++)
        {
            double px, py, qx, qy;
            if (e == 0) { px = x0; py = y0; qx = x1; qy = y1; } else if (e == 1) { px = x1; py = y1; qx = x2; qy = y2; } else { px = x2; py = y2; qx = x0; qy = y0; }
            double ux = qx - px, uy = qy - py;
            double alpha = sg * (ux * (ay - py) - uy * (ax - px));
            double beta = sg * (ux * dy - uy * dx);
            double eps = 1e-9 * Math.Sqrt(ux * ux + uy * uy);
            if (Math.Abs(beta) < 1e-18) { if (alpha <= eps) return false; continue; }
            double ts = (eps - alpha) / beta;
            if (beta > 0) { if (ts > t0) t0 = ts; } else { if (ts < t1) t1 = ts; }
            if (t0 >= t1) return false;
        }
        // Depth of the triangle plane at the segment ends (linear along the segment).
        double DepthAt(double px, double py)
        {
            double qx = px - x0, qy = py - y0;
            double u = (qx * ey2 - qy * ex2) / area, v = (ex1 * qy - ey1 * qx) / area;
            return d0 + u * (d1 - d0) + v * (d2 - d0);
        }
        double fA = s.A.Z - tol - DepthAt(s.A.X, s.A.Y), fB = s.B.Z - tol - DepthAt(s.B.X, s.B.Y);
        double f0 = fA + (fB - fA) * t0, f1 = fA + (fB - fA) * t1;
        if (f0 <= 0 && f1 <= 0) return false;
        if (f0 > 0 && f1 > 0) { h0 = t0; h1 = t1; return true; }
        double tr = t0 + (t1 - t0) * f0 / (f0 - f1);
        if (f0 > 0) { h0 = t0; h1 = tr; } else { h0 = tr; h1 = t1; }
        return h1 - h0 > 1e-12;
    }

    /// <summary>Visible pieces = complement of the hidden intervals; both go out.</summary>
    private static void Emit(Seg s, List<(double, double)> iv, List<Piece> outp)
    {
        double len = (s.B.XY - s.A.XY).Length;
        double minT = len > 0 ? 1e-7 / len : 1;
        void Add(double a, double b, bool hidden)
        {
            if (b - a < minT) return;
            outp.Add(new Piece(Vec3.Lerp(s.A, s.B, a), Vec3.Lerp(s.A, s.B, b), s.Item, hidden));
        }
        if (iv.Count == 0) { Add(0, 1, false); return; }
        iv.Sort((p, q) => p.Item1.CompareTo(q.Item1));
        double cur = 0;
        double hs = iv[0].Item1, he = iv[0].Item2;
        for (int i = 1; i <= iv.Count; i++)
        {
            if (i < iv.Count && iv[i].Item1 <= he + minT) { he = Math.Max(he, iv[i].Item2); continue; }
            Add(cur, Math.Max(cur, hs), false);
            Add(Math.Max(cur, hs), he, true);
            cur = Math.Max(cur, he);
            if (i < iv.Count) { hs = iv[i].Item1; he = iv[i].Item2; }
        }
        Add(cur, 1, false);
    }

    // ------------------------------------------------------------------ depth buffer HLR

    private sealed class DepthBuffer
    {
        private readonly int _w, _h;
        private readonly double _x0, _y0, _px, _d0;
        private readonly int[] _z;   // float bits of (depth - d0) >= 0: integer order = float order

        public DepthBuffer(double[] T, int n, List<Seg> segs, ProjectionOptions o, ParallelOptions po, int[]? own = null)
        {
            var b = Box2.Empty;
            double dmin = double.MaxValue;
            foreach (var s in segs) { b.Add(s.A.XY); b.Add(s.B.XY); dmin = Math.Min(dmin, Math.Min(s.A.Z, s.B.Z)); }
            for (long k = 0; k < (long)n * 9; k += 3) dmin = Math.Min(dmin, T[k + 2]);
            if (b.IsEmpty) b.Add(Vec2.Zero);
            _px = o.PixelSize > 0 ? o.PixelSize : Math.Max(Math.Max(b.Width, b.Height) / Math.Max(256, o.DepthBufferPixels), 1e-4);
            _x0 = b.MinX - 2 * _px; _y0 = b.MinY - 2 * _px;
            _w = Math.Min(16384, (int)Math.Ceiling((b.Width + 4 * _px) / _px) + 1);
            _h = Math.Min(16384, (int)Math.Ceiling((b.Height + 4 * _px) / _px) + 1);
            _d0 = dmin == double.MaxValue ? 0 : dmin - 1;
            _z = new int[(long)_w * _h];
            Array.Fill(_z, int.MaxValue);
            double x0c = _x0, y0c = _y0, pxc = _px, d0c = _d0;
            int w = _w, h = _h;
            var z = _z;
            Parallel.For(0, n, po, i =>
            {
                if (own != null && own[i] >= 0) return;   // self-only occluders (transparent layers) are not in the z-buffer
                long k = (long)i * 9;
                double ax = (T[k] - x0c) / pxc, ay = (T[k + 1] - y0c) / pxc, ad = T[k + 2] - d0c;
                double bx = (T[k + 3] - x0c) / pxc, by = (T[k + 4] - y0c) / pxc, bd = T[k + 5] - d0c;
                double cx = (T[k + 6] - x0c) / pxc, cy = (T[k + 7] - y0c) / pxc, cd = T[k + 8] - d0c;
                double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Math.Abs(area) < 1e-12) return;
                int ix0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), ix1 = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int iy0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), iy1 = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                for (int iy = iy0; iy <= iy1; iy++)
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        double px = ix + 0.5, py = iy + 0.5;
                        double w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) / area;
                        double w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) / area;
                        double w2 = 1 - w0 - w1;
                        if (w0 < -1e-9 || w1 < -1e-9 || w2 < -1e-9) continue;
                        float d = (float)Math.Max(0, w0 * ad + w1 * bd + w2 * cd);
                        int bits = BitConverter.SingleToInt32Bits(d);
                        long at = (long)iy * w + ix;
                        int cur = z[at];
                        while (bits < cur)
                        {
                            int prev = Interlocked.CompareExchange(ref z[at], bits, cur);
                            if (prev == cur) break;
                            cur = prev;
                        }
                    }
            });
        }

        private double At(double x, double y)
        {
            int ix = (int)Math.Floor((x - _x0) / _px), iy = (int)Math.Floor((y - _y0) / _px);
            if (ix < 0 || iy < 0 || ix >= _w || iy >= _h) return double.MaxValue;
            int bits = _z[(long)iy * _w + ix];
            return bits == int.MaxValue ? double.MaxValue : BitConverter.Int32BitsToSingle(bits) + _d0;
        }

        public void Split(Seg s, double tol, List<Piece> outp)
        {
            double len = (s.B.XY - s.A.XY).Length;
            int n = Math.Max(1, (int)Math.Ceiling(len / (_px * 0.5)));
            double slack = tol + 2 * _px;
            bool? state = null;
            double runStart = 0;
            for (int k = 0; k <= n; k++)
            {
                double t = (k + 0.5) / (n + 1);
                var p = Vec3.Lerp(s.A, s.B, t);
                bool hidden = p.Z > At(p.X, p.Y) + slack;
                if (state == null) state = hidden;
                else if (hidden != state)
                {
                    double tc = (k) / (double)(n + 1);
                    outp.Add(new Piece(Vec3.Lerp(s.A, s.B, runStart), Vec3.Lerp(s.A, s.B, tc), s.Item, state.Value));
                    runStart = tc; state = hidden;
                }
            }
            if (1 - runStart > 1e-9) outp.Add(new Piece(Vec3.Lerp(s.A, s.B, runStart), s.B, s.Item, state ?? false));
        }
    }

    // ------------------------------------------------------------------ coincident lines

    /// <summary>
    /// Collinear overlapping pieces (coincident edges of one solid or of touching solids, a back edge exactly under a front edge) become one
    /// line: per supporting 2D line, every elementary interval is drawn once - visible if any piece there is visible (visible wins over
    /// hidden), tagged with the lowest element index covering it.
    /// </summary>
    private static List<Piece> MergeOverlaps(List<Piece> all)
    {
        var buckets = new Dictionary<(long, long), List<int>>();
        var keyOf = new (double ang, double c)[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            var d = all[i].B.XY - all[i].A.XY;
            double ang = Math.Atan2(d.Y, d.X);
            if (ang < 0) ang += Math.PI;
            if (ang >= Math.PI - 1e-9) ang -= Math.PI;
            var u = Vec2.FromAngle(ang);
            double c = Vec2.Cross(u, all[i].A.XY);
            keyOf[i] = (ang, c);
            var k = ((long)Math.Round(ang * 1e6), (long)Math.Round(c * 1e5));
            if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<int>();
            l.Add(i);
        }
        var res = new List<Piece>(all.Count);
        foreach (var l in buckets.Values)
        {
            if (l.Count == 1) { res.Add(all[l[0]]); continue; }
            var u = Vec2.FromAngle(keyOf[l[0]].ang);
            var segs = l.Select(i => { double ta = Vec2.Dot(u, all[i].A.XY), tb = Vec2.Dot(u, all[i].B.XY); return (i, t0: Math.Min(ta, tb), t1: Math.Max(ta, tb)); })
                .OrderBy(s => s.t0).ToList();
            var cuts = segs.SelectMany(s => new[] { s.t0, s.t1 }).OrderBy(t => t).ToList();
            var uniq = new List<double>();
            foreach (double t in cuts) if (uniq.Count == 0 || t - uniq[^1] > 1e-7) uniq.Add(t);
            var active = new List<(int i, double t0, double t1)>();
            int next = 0;
            int curItem = -1; bool curHidden = false; double runStart = 0; int curPiece = -1;
            Vec3 At(int pi, double t)
            {
                var p = all[pi];
                double ta = Vec2.Dot(u, p.A.XY), tb = Vec2.Dot(u, p.B.XY);
                double f = Math.Abs(tb - ta) < 1e-15 ? 0 : (t - ta) / (tb - ta);
                return Vec3.Lerp(p.A, p.B, f);
            }
            void Flush(double tEnd)
            {
                if (curPiece >= 0 && tEnd - runStart > 1e-7) res.Add(new Piece(At(curPiece, runStart), At(curPiece, tEnd), curItem, curHidden));
            }
            for (int c = 0; c + 1 < uniq.Count; c++)
            {
                double ta = uniq[c], tb = uniq[c + 1], tm = (ta + tb) / 2;
                while (next < segs.Count && segs[next].t0 <= tm) active.Add(segs[next++]);
                active.RemoveAll(s => s.t1 < tm);
                int bestPiece = -1; bool bestHidden = true; int bestItem = int.MaxValue;
                foreach (var s in active)
                {
                    var p = all[s.i];
                    if (s.t0 > tm) continue;
                    if ((!p.Hidden && bestHidden) || (p.Hidden == bestHidden && p.Item < bestItem)) { bestPiece = s.i; bestHidden = p.Hidden; bestItem = p.Item; }
                }
                bool same = bestPiece >= 0 && curPiece >= 0 && bestItem == curItem && bestHidden == curHidden;
                if (!same)
                {
                    Flush(ta);
                    curPiece = bestPiece; curItem = bestItem; curHidden = bestHidden; runStart = ta;
                }
                else if (bestPiece != curPiece) { Flush(ta); curPiece = bestPiece; runStart = ta; }
            }
            if (uniq.Count > 0) Flush(uniq[^1]);
        }
        return res;
    }

    // ------------------------------------------------------------------ chaining

    private static List<List<Vec3>> ChainPieces(List<Piece> pieces)
    {
        const double Q = 1e-6;
        (long, long) K(Vec3 p) => ((long)Math.Round(p.X / Q), (long)Math.Round(p.Y / Q));
        var at = new Dictionary<(long, long), List<int>>();
        for (int i = 0; i < pieces.Count; i++)
            foreach (var p in new[] { pieces[i].A, pieces[i].B })
            {
                var k = K(p);
                if (!at.TryGetValue(k, out var l)) at[k] = l = new List<int>(2);
                l.Add(i);
            }
        var used = new bool[pieces.Count];
        var res = new List<List<Vec3>>();
        int Next(Vec3 p)
        {
            if (!at.TryGetValue(K(p), out var l)) return -1;
            foreach (int j in l) if (!used[j]) return j;
            return -1;
        }
        for (int i = 0; i < pieces.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var chain = new LinkedList<Vec3>();
            chain.AddLast(pieces[i].A); chain.AddLast(pieces[i].B);
            for (int dir = 0; dir < 2; dir++)
                while (true)
                {
                    var end = dir == 0 ? chain.Last!.Value : chain.First!.Value;
                    int j = Next(end);
                    if (j < 0) break;
                    used[j] = true;
                    var other = K(pieces[j].A) == K(end) ? pieces[j].B : pieces[j].A;
                    if (dir == 0) chain.AddLast(other); else chain.AddFirst(other);
                    if (K(chain.First!.Value) == K(chain.Last!.Value)) break;
                }
            res.Add(chain.ToList());
        }
        return res;
    }
}
