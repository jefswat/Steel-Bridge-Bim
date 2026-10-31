using System.Text.RegularExpressions;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL concrete details of the segmental box (SegmentalDefinition.BlockTypes / Blocks / Openings / Parapet / Bars; all null = nothing here,
// output unchanged): blocks = the box VOID clipped by the block limits at each station, lofted (no overlap with the segment concrete); openings =
// void solids + the concrete they remove; parapets = plumb sweeps on the box top; segment bill bars = schedule marks + straight bars where the
// plan gives position and cover, checked for clear cover against the segment solids (reported, never moved).
public sealed partial class SegmentalGenerator
{
    private void BuildDetails()
    {
        if (_d.BlockTypes is { Count: > 0 } || _d.Blocks is { Count: > 0 }) BuildBlocks();
        if (_d.PtBars is { Count: > 0 }) BuildPtBars();
        if (_d.FixedBars is { Count: > 0 }) BuildFixedBars();
        if (_d.Pipes is { Count: > 0 }) BuildPipes();
        if (_d.Tendons.Any(t => t.Profile != null)) ApplyTendonProfiles();
        if (_d.Openings is { Count: > 0 }) BuildOpenings();
        if (_d.Keys is { Count: > 0 }) BuildKeys();
        if (_d.Parapet is { } pp) BuildParapet(pp);
        if (_d.Bars is { Count: > 0 }) BuildBars();
    }

    private (SegmentResult r, SegmentDefinition d)? SegByName(string name)
    {
        foreach (var (r, d, _) in _segs) if (r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return (r, d);
        return null;
    }

    /// <summary>Section frame at a station on the chord of a given segment.</summary>
    private SectionFrame ChordFrame(SegmentResult r, double sta)
    {
        double u = (sta - r.Station0) / (r.Station1 - r.Station0);
        return Frame(sta) with { Origin = Vec3.Lerp(r.Top0, r.Top1, u) };
    }

    /// <summary>Inside web face x at depth y (section coordinates, right web; the straight 4:1 line between the fillets).</summary>
    private double WebInsideX(double y)
    {
        var b = _d.Box;
        double xc = b.WebRootX - b.WebHorizontal;
        return xc - (y - b.WebTopDepth) / b.WebSlope;
    }

    // ------------------------------------------------------------------ polygon helpers

    /// <summary>Sutherland-Hodgman: keep the part of the polygon where f(p) >= 0 (f linear).</summary>
    private static List<Vec2> Clip(List<Vec2> poly, Func<Vec2, double> f)
    {
        var res = new List<Vec2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            double fa = f(a), fb = f(b);
            if (fa >= 0) res.Add(a);
            if ((fa >= 0) != (fb >= 0)) res.Add(a + (b - a) * (fa / (fa - fb)));
        }
        // drop repeated points
        var o = new List<Vec2>();
        foreach (var p in res) if (o.Count == 0 || Vec2.Distance(o[^1], p) > 1e-7) o.Add(p);
        if (o.Count > 1 && Vec2.Distance(o[0], o[^1]) <= 1e-7) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>CCW in (x, y), starting at the vertex with the largest x (ties: smallest y), with <paramref name="n"/> points (longest edges split).</summary>
    private static List<Vec2> Normalize(List<Vec2> p, int n)
    {
        var q = p.ToList();
        if (MeshBuilder.SignedArea(q) < 0) q.Reverse();
        int s = 0;
        for (int i = 1; i < q.Count; i++) if (q[i].X > q[s].X + 1e-9 || (Math.Abs(q[i].X - q[s].X) <= 1e-9 && q[i].Y < q[s].Y)) s = i;
        q = q.Skip(s).Concat(q.Take(s)).ToList();
        while (q.Count < n)
        {
            int best = 0; double bl = -1;
            for (int i = 0; i < q.Count; i++) { double l = Vec2.Distance(q[i], q[(i + 1) % q.Count]); if (l > bl) { bl = l; best = i; } }
            q.Insert(best + 1, (q[best] + q[(best + 1) % q.Count]) * 0.5);
        }
        return q;
    }

    /// <summary>Closed solid through rings of equal point count; the LAST ring may be a single point (apex: the block closes to a point on the host face).
    /// Caps: convex polygons fanned from their centroid (no zero-area triangles at collinear edge points - OpenCascade drops those and the shell opens),
    /// others ear-clipped; outward orientation fixed by the volume sign.</summary>
    private static Mesh Loft(IReadOnlyList<Vec3[]> rings, IReadOnlyList<List<Vec2>> polys)
    {
        var m = new Mesh();
        int n = rings[0].Length;
        bool apex = rings.Count > 1 && rings[^1].Length == 1;
        int full = apex ? rings.Count - 1 : rings.Count;
        for (int k = 0; k < full; k++) foreach (var p in rings[k]) m.Add(p);
        for (int k = 0; k + 1 < full; k++)
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                int a = k * n + i, b = k * n + i1, c = (k + 1) * n + i1, d = (k + 1) * n + i;
                m.Tri(a, b, c); m.Tri(a, c, d);
            }
        Cap(m, 0, rings[0], polys[0], true);
        if (apex)
        {
            int ap = m.Add(rings[^1][0]), last = (full - 1) * n;
            for (int i = 0; i < n; i++) m.Tri(last + i, last + (i + 1) % n, ap);
        }
        else Cap(m, (full - 1) * n, rings[full - 1], polys[full - 1], false);
        if (m.Volume() < 0)
            for (int i = 0; i < m.Indices.Count; i += 3) (m.Indices[i + 1], m.Indices[i + 2]) = (m.Indices[i + 2], m.Indices[i + 1]);
        return m;
    }

    /// <summary>End cap in the polygon's own winding (reversed for the first cap).</summary>
    private static void Cap(Mesh m, int start, Vec3[] ring, List<Vec2> poly, bool reverse)
    {
        int n = poly.Count;
        if (IsConvex(poly))
        {
            var c3 = new Vec3(ring.Average(p => p.X), ring.Average(p => p.Y), ring.Average(p => p.Z));
            int c = m.Add(c3);
            for (int i = 0; i < n; i++)
            {
                int a = start + i, b = start + (i + 1) % n;
                if (reverse) m.Tri(c, b, a); else m.Tri(c, a, b);
            }
            return;
        }
        foreach (var (a, b, cc) in CapTris(poly))
            if (reverse) m.Tri(start + a, start + cc, start + b); else m.Tri(start + a, start + b, start + cc);
    }

    /// <summary>Convex (collinear points allowed) with a non-zero area.</summary>
    private static bool IsConvex(List<Vec2> p)
    {
        double area = MeshBuilder.SignedArea(p);
        if (Math.Abs(area) < 1e-10) return false;
        double sg = Math.Sign(area);
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count]; var c = p[(i + 2) % p.Count];
            double cr = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (cr * sg < -1e-12) return false;
        }
        return true;
    }

    /// <summary>Cap triangles in the polygon's OWN winding (CW polygons: triangulated as CCW, mapped back and reversed).</summary>
    private static List<(int, int, int)> CapTris(List<Vec2> poly)
    {
        int n = poly.Count;
        if (MeshBuilder.SignedArea(poly) >= 0)
            return MeshBuilder.Triangulate(poly).Select(t => { var (a, b, c) = t; return CcwOf(poly, a, b, c); }).ToList();
        var rev = Enumerable.Range(0, n).Select(j => poly[n - 1 - j]).ToList();
        return MeshBuilder.Triangulate(rev).Select(t => { var (a, b, c) = CcwOf(rev, t.Item1, t.Item2, t.Item3); return (n - 1 - a, n - 1 - c, n - 1 - b); }).ToList();
    }

    private static (int, int, int) CcwOf(List<Vec2> p, int a, int b, int c)
    {
        double cr = (p[b].X - p[a].X) * (p[c].Y - p[a].Y) - (p[b].Y - p[a].Y) * (p[c].X - p[a].X);
        return cr >= 0 ? (a, b, c) : (a, c, b);
    }

    // ------------------------------------------------------------------ blocks

    private void BuildBlocks()
    {
        var types = (_d.BlockTypes ?? new()).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var t in types.Values)
        {
            if (t.NotBuilt.Length > 0) Warn(t.NotBuilt);
            else if (t.Unresolved.Length > 0) Warn(t.Unresolved);
        }
        var notBuilt = new Dictionary<string, int>();
        var buried = new Dictionary<string, int>();
        var skipped = new List<string>();
        foreach (var pl in _d.Blocks ?? new())
        {
            if (!types.TryGetValue(pl.Type, out var bt)) { skipped.Add($"{pl.Segment}: unknown block type '{pl.Type}'"); continue; }
            if (bt.NotBuilt.Length > 0 || bt.Stations.Count < 2) { notBuilt[bt.Name] = notBuilt.GetValueOrDefault(bt.Name) + (pl.Side == 0 ? 2 : 1); continue; }
            if (SegByName(pl.Segment) is not { } sd) { skipped.Add($"{pl.Segment}: segment not found"); continue; }
            var (r, d) = sd;
            double refJ, dir;
            if (bt.From == SegmentalJointRef.Centre) { refJ = (r.Station0 + r.Station1) / 2; dir = 1; }
            else
            {
                if (d.Type != SegmentType.Typical || !_bentSta.TryGetValue(d.Bent, out double bs)) { skipped.Add($"{pl.Segment}: not a typical cantilever segment"); continue; }
                bool down = r.Station1 <= bs + 1e-6;
                // bent-side joint and the direction into the segment (tip side = the bulkhead face, user decision 2026-10-30)
                double bentJ = down ? r.Station1 : r.Station0, tipJ = down ? r.Station0 : r.Station1;
                refJ = bt.From == SegmentalJointRef.BentSide ? bentJ : tipJ;
                dir = refJ == r.Station0 ? 1 : -1;
            }
            foreach (int side in pl.Side == 0 ? new[] { -1, 1 } : new[] { Math.Sign(pl.Side) })
            {
                var polys = new List<List<Vec2>>();
                var stas = new List<double>();
                bool ok = true;
                foreach (var st in bt.Stations)
                {
                    double sta = refJ + dir * (bt.Start + st.Along);
                    if (sta < r.Station0 - 1e-6 || sta > r.Station1 + 1e-6) { ok = false; break; }
                    var (h, tt, hollow) = SectionIn(d, r, sta);
                    if (!hollow) { ok = false; break; }
                    var poly = BlockSection(st, h, tt);
                    stas.Add(sta);
                    polys.Add(poly);
                }
                if (!ok || polys.Count == 0) { skipped.Add($"{pl.Segment} {bt.Name}: outside the segment / no hollow section"); continue; }
                _blockSpans[(r.Name, bt.Name.ToLowerInvariant(), side)] = (stas.Min(), stas.Max());
                if (polys[0].Count < 3 || Math.Abs(MeshBuilder.SignedArea(polys[0])) < 1e-8) { buried[bt.Name] = buried.GetValueOrDefault(bt.Name) + 1; continue; }
                // a section clipped to nothing closes the block: the block ends in an APEX at the previous section's lowest point (the edge on the host
                // face); later sections are dropped (a scaled-down copy left near-zero-area triangles that opened the shell in OpenCascade)
                Vec2? apex = null;
                for (int k = 1; k < polys.Count; k++)
                    if (polys[k].Count < 3 || Math.Abs(MeshBuilder.SignedArea(polys[k])) < 1e-8)
                    {
                        apex = polys[k - 1].OrderByDescending(p => p.Y).ThenByDescending(p => p.X).First();
                        stas = stas.Take(k + 1).ToList();
                        polys = polys.Take(k).ToList();
                        break;
                    }
                int nmax = polys.Max(p => p.Count);
                var norm = polys.Select(p => Normalize(p, nmax)).ToList();
                // mirror for the left web (x -> -x reverses the orientation; Loft fixes it)
                var placed = norm.Select(p => p.Select(q => new Vec2(side * q.X, q.Y)).ToList()).ToList();
                var rings = placed.Select((p, k) => { var f = ChordFrame(r, stas[k]); return p.Select(f.At).ToArray(); }).ToList();
                if (apex is { } ax) rings.Add(new[] { ChordFrame(r, stas[^1]).At(new Vec2(side * ax.X, ax.Y)) });
                var mesh = Loft(rings, placed);
                string sideName = side < 0 ? "left" : "right";
                var res = new SegmentalBlockResult
                {
                    Name = $"{r.Name} {bt.Name} {sideName}" + (pl.Label.Length > 0 ? $" ({pl.Label})" : ""), Type = bt.Name, Kind = bt.Kind, Segment = r.Name, Side = side,
                    Label = pl.Label, Mesh = mesh,
                    Description = $"{bt.Description} at the {sideName} web of {r.Name}" + (pl.Label.Length > 0 ? $", {pl.Label}" : "") + (bt.Source.Length > 0 ? $" ({bt.Source})" : ""),
                };
                if (bt.Unresolved.Length > 0) res.Assumption = Assumption.UnresolvedDimension(bt.Unresolved, "block shape from the printed / scaled dimensions as listed in the block type");
                _m.Blocks.Add(res);
            }
        }
        foreach (var (name, n) in buried) Info($"Segmental blocks: {n} '{name}' lie entirely inside the slab there (the bottom slab is at least as thick as the block top height) - no solid.");
        foreach (var (name, n) in notBuilt) Info($"Segmental blocks: {n} placements of '{name}' recorded, not built (see its UNRESOLVED warning).");
        if (skipped.Count > 0) Warn($"Segmental blocks: {skipped.Count} placements not built - {string.Join("; ", skipped.Take(6))}{(skipped.Count > 6 ? "; ..." : "")}.");
        if (_m.Blocks.Count > 0)
            Info($"Segmental blocks: {_m.Blocks.Count} built ({string.Join(", ", _m.Blocks.GroupBy(b => b.Type).Select(g => $"{g.Count()} {g.Key}"))}), {_m.BlockVolume / Math.Pow(Ft(1), 3) / 27:0.0} CY.");
    }

    /// <summary>Block section at one station: the right half of the box void clipped by the station limits (section coordinates, x toward the right web).</summary>
    private List<Vec2> BlockSection(SegmentalBlockStation st, double h, double t)
    {
        var poly = SegmentalSection.Void(_d.Box, h, t);
        poly = Clip(poly, p => p.X - 1e-6);
        double yTop = double.NaN;
        if (!double.IsNaN(st.TopAboveSlab)) yTop = h - t - st.TopAboveSlab;
        if (!double.IsNaN(st.TopAboveSoffit)) yTop = double.IsNaN(yTop) ? h - st.TopAboveSoffit : Math.Max(yTop, h - st.TopAboveSoffit);
        if (!double.IsNaN(st.TopBelowDeck)) yTop = double.IsNaN(yTop) ? st.TopBelowDeck : Math.Max(yTop, st.TopBelowDeck);
        if (!double.IsNaN(yTop)) { double y0 = yTop; poly = Clip(poly, p => p.Y - y0); }
        if (!double.IsNaN(st.ThicknessFromWeb))
        {
            double k = 1 / Math.Sqrt(1 + 1 / (_d.Box.WebSlope * _d.Box.WebSlope)), T = st.ThicknessFromWeb;
            poly = Clip(poly, p => T - (WebInsideX(p.Y) - p.X) * k);       // within T (normal to the web) of the web inside face
        }
        if (!double.IsNaN(st.WidthFromWeb) && !double.IsNaN(yTop)) { double x0 = WebInsideX(yTop) - st.WidthFromWeb; poly = Clip(poly, p => p.X - x0); }
        if (!double.IsNaN(st.InnerFromCl)) { double x0 = st.InnerFromCl; poly = Clip(poly, p => p.X - x0); }
        if (!double.IsNaN(st.OuterFromCl)) { double x1 = st.OuterFromCl; poly = Clip(poly, p => x1 - p.X); }
        if (!double.IsNaN(st.BottomBelowDeck) && !double.IsNaN(st.BottomSquareToWeb))
        {
            var q = TopBlockCorner(st.BottomBelowDeck, st.BottomSquareToWeb);
            var dw = new Vec2(-1 / _d.Box.WebSlope, 1).Normalized();       // down along the web
            poly = Clip(poly, p => -Vec2.Dot(p - q, dw));                   // above the bottom face (square to the web)
            poly = Clip(poly, p => p.X - q.X);                             // outside the vertical inner face
        }
        else if (!double.IsNaN(st.BottomBelowDeck)) { double y1 = st.BottomBelowDeck; poly = Clip(poly, p => y1 - p.Y); }
        return poly;
    }

    /// <summary>Free (inner) end of a top-slab block's bottom face: on the face square to the web inside face, <paramref name="len"/> from it, at depth <paramref name="depth"/>.</summary>
    public Vec2 TopBlockCorner(double depth, double len)
    {
        var b = _d.Box;
        var c = new Vec2(b.WebRootX - b.WebHorizontal, b.WebTopDepth);
        var dw = new Vec2(-1 / b.WebSlope, 1).Normalized();
        var ni = new Vec2(-dw.Y, dw.X);                                    // square to the web, pointing into the box (-x, up)
        if (ni.X > 0) ni = new Vec2(-ni.X, -ni.Y);
        double s = (depth - c.Y - ni.Y * len) / dw.Y;
        return c + dw * s + ni * len;
    }

    // ------------------------------------------------------------------ openings

    private void BuildOpenings()
    {
        var missing = new List<string>();
        const double m = 0.02;                       // void solids reach 2 cm beyond the concrete they cut (clean booleans)
        foreach (var od in _d.Openings!)
        {
            if (od.Unresolved.Length > 0) Warn(od.Unresolved);
            Assumption? flag = od.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(od.Unresolved, "opening as listed") : null;
            if (od.Kind is SegmentalOpeningKind.BottomSlab or SegmentalOpeningKind.TopSlab)
            {
                bool bottom = od.Kind == SegmentalOpeningKind.BottomSlab;
                var outline = SlabOutline(od);
                if (outline == null) { missing.Add($"{od.Name}: no size"); continue; }
                double area = Math.Abs(MeshBuilder.SignedArea(outline));
                var targets = new List<(SegmentResult r, SegmentDefinition d, double c)>();
                foreach (var name in od.Segments)
                {
                    if (SegByName(name) is not { } sd) { missing.Add($"{od.Name}: segment {name} not found"); continue; }
                    targets.Add((sd.r, sd.d, (sd.r.Station0 + sd.r.Station1) / 2));
                }
                foreach (double st in od.Stations)
                {
                    var hit = _segs.FirstOrDefault(x => st >= x.r.Station0 - 1e-9 && st <= x.r.Station1 + 1e-9);
                    if (hit.r == null) { missing.Add($"{od.Name}: station {st / Ft(1):0.00} outside the unit"); continue; }
                    targets.Add((hit.r, hit.d, st));
                }
                foreach (var (r, d, c) in targets)
                {
                    string name = r.Name;
                    var (h, t, hollow) = SectionIn(d, r, c);
                    if (!hollow && double.IsNaN(od.Depth)) { missing.Add($"{od.Name}: {name} is solid at its centre"); continue; }
                    var segOutline = outline;
                    if (od.Side != null || od.FromSoffitCorner)
                    {
                        // OPTIONAL side rule: |CenterOffset| on the chosen side, from -L- or inward from the soffit corner.
                        int sgn = od.Side switch
                        {
                            SegmentalDrainSide.Left => -1, SegmentalDrainSide.Right => 1,
                            SegmentalDrainSide.LowSide => ChordFrame(r, c).Lat.Z <= 0 ? 1 : -1,
                            SegmentalDrainSide.HighSide => ChordFrame(r, c).Lat.Z <= 0 ? -1 : 1,
                            _ => Math.Sign(od.CenterOffset) >= 0 ? 1 : -1,
                        };
                        double ax = od.FromSoffitCorner ? _d.Box.HalfSoffit(h) - Math.Abs(od.CenterOffset) : Math.Abs(od.CenterOffset);
                        segOutline = outline.Select(q => new Vec2(q.X - od.CenterOffset + sgn * ax, q.Y)).ToList();
                    }
                    bool recess = !double.IsNaN(od.Depth);
                    double y0 = bottom ? h + m : -m, y1 = bottom ? (recess ? h - od.Depth : h - t - m) : (recess ? od.Depth : _d.Box.TopSlab + m);
                    var mesh = SlabVoid(r, c, segOutline, y0, y1, 1);
                    double thk = recess ? od.Depth : bottom ? t : _d.Box.TopSlab;
                    _m.Openings.Add(new SegmentalOpeningResult
                    {
                        Name = $"{r.Name} {od.Name}", Kind = od.Kind, Segment = r.Name, Mesh = mesh, Temporary = od.Temporary,
                        RemovedVolume = od.Temporary ? 0 : area * thk, Assumption = flag,
                        Description = $"{od.Name} {(recess ? $"{od.Depth / In(1):0.##} in deep recess in" : "through")} the {(bottom ? "bottom" : "top")} slab of {r.Name}" + (od.Temporary ? " (temporary: repaired, no concrete deducted)" : "") + (od.Source.Length > 0 ? $" ({od.Source})" : ""),
                    });
                }
                foreach (var bent in od.Bents)
                {
                    if (!_bentSta.TryGetValue(bent, out double bs)) { missing.Add($"{od.Name}: bent {bent} not found"); continue; }
                    foreach (var (r, d, _) in _segs.Where(x => x.d.Type == SegmentType.BentHalf && x.d.Bent.Equals(bent, StringComparison.OrdinalIgnoreCase)))
                    {
                        int sgn = r.Station0 < bs - 1e-9 ? -1 : 1;                      // half on this side of the bent CL
                        double sIn = bs + sgn * 1e-4;
                        var (h, t, _) = SectionIn(d, r, sIn);
                        var half = Clip(outline, p => sgn * p.Y);                      // (x, along) - keep this half
                        if (half.Count < 3) continue;
                        double y0 = bottom ? h + m : -m, y1 = bottom ? h - t - m : _d.Box.TopSlab + m;
                        var mesh = SlabVoid(r, bs, half, y0, y1, 1);
                        double thk = bottom ? t : _d.Box.TopSlab;
                        _m.Openings.Add(new SegmentalOpeningResult
                        {
                            Name = $"{r.Name} {od.Name}", Kind = od.Kind, Segment = r.Name, Mesh = mesh, Temporary = od.Temporary,
                            RemovedVolume = od.Temporary ? 0 : Math.Abs(MeshBuilder.SignedArea(half)) * thk, Assumption = flag,
                            Description = $"{od.Name} at the {bent} CL, {(sgn < 0 ? "down" : "up")}station half" + (od.Source.Length > 0 ? $" ({od.Source})" : ""),
                        });
                    }
                }
            }
            else
            {
                if (od.Outline.Count < 3 || double.IsNaN(_d.BentDiaphragm)) { missing.Add($"{od.Name}: outline / diaphragm length missing"); continue; }
                foreach (var bent in od.Bents)
                {
                    if (!_bentSta.TryGetValue(bent, out double bs)) { missing.Add($"{od.Name}: bent {bent} not found"); continue; }
                    foreach (var (r, d, _) in _segs.Where(x => x.d.Type == SegmentType.BentHalf && x.d.Bent.Equals(bent, StringComparison.OrdinalIgnoreCase)))
                    {
                        int sgn = r.Station0 < bs - 1e-9 ? -1 : 1;
                        var (h, t, _) = SectionIn(d, r, bs + sgn * 1e-4);
                        var sec = od.Outline.Select(p => new Vec2(p.S, od.OutlineFromTop ? p.N : h - t - p.N)).ToList();
                        if (MeshBuilder.SignedArea(sec) < 0) sec.Reverse();
                        double a0 = bs, a1 = bs + sgn * (_d.BentDiaphragm + m);
                        var stas = new[] { a0, a1 };
                        var rings = stas.Select(s => { var f = ChordFrame(r, s); return sec.Select(f.At).ToArray(); }).ToList();
                        var mesh = Loft(rings, new[] { sec, sec });
                        _m.Openings.Add(new SegmentalOpeningResult
                        {
                            Name = $"{r.Name} {od.Name}", Kind = od.Kind, Segment = r.Name, Mesh = mesh, Temporary = od.Temporary,
                            RemovedVolume = od.Temporary ? 0 : Math.Abs(MeshBuilder.SignedArea(sec)) * _d.BentDiaphragm, Assumption = flag,
                            Description = $"{od.Name} through the {bent} diaphragm, {(sgn < 0 ? "down" : "up")}station half" + (od.Source.Length > 0 ? $" ({od.Source})" : ""),
                        });
                    }
                }
            }
        }
        if (missing.Count > 0) Warn($"Segmental openings: {missing.Count} not built - {string.Join("; ", missing.Take(6))}{(missing.Count > 6 ? "; ..." : "")}.");
        if (_m.Openings.Count > 0)
            Info($"Segmental openings: {_m.Openings.Count} ({string.Join(", ", _m.Openings.GroupBy(o => o.Kind).Select(g => $"{g.Count()} {g.Key}"))}), concrete removed {_m.OpeningVolume / Math.Pow(Ft(1), 3) / 27:0.00} CY.");
    }

    /// <summary>Slab opening outline in (x across, along) about its centre: rectangle Width x Length or a 32-gon of the Diameter (equal area).</summary>
    private static List<Vec2>? SlabOutline(SegmentalOpeningDefinition od)
    {
        if (!double.IsNaN(od.Diameter) && od.Diameter > 0)
        {
            const int n = 32;
            double r = od.Diameter / 2 * Math.Sqrt(2 * Math.PI / (n * Math.Sin(2 * Math.PI / n)));
            return Enumerable.Range(0, n).Select(i => new Vec2(od.CenterOffset + r * Math.Cos(2 * Math.PI * i / n), r * Math.Sin(2 * Math.PI * i / n))).ToList();
        }
        if (double.IsNaN(od.Width) || double.IsNaN(od.Length)) return null;
        double w = od.Width / 2, l = od.Length / 2, c = od.CenterOffset;
        return new List<Vec2> { new(c - w, -l), new(c + w, -l), new(c + w, l), new(c - w, l) };
    }

    /// <summary>Void prism of a slab opening: outline (x, along from <paramref name="centre"/>) between section depths y0 and y1.</summary>
    private Mesh SlabVoid(SegmentResult r, double centre, List<Vec2> outline, double y0, double y1, int _)
    {
        Vec3 P(Vec2 q, double y) => ChordFrame(r, centre + q.Y).At(new Vec2(q.X, y));
        var rings = new List<Vec3[]> { outline.Select(q => P(q, y0)).ToArray(), outline.Select(q => P(q, y1)).ToArray() };
        return Loft(rings, new[] { outline, outline });
    }

    // ------------------------------------------------------------------ parapet

    private void BuildParapet(SegmentalParapetDefinition pp)
    {
        if (pp.Unresolved.Length > 0) Warn(pp.Unresolved);
        if (double.IsNaN(pp.InsideOffset) || double.IsNaN(pp.Width) || double.IsNaN(pp.Height) || _segs.Count == 0)
        {
            Warn(UnresolvedFlag.Text(Pages, "segmental parapet", "its position or section is not given", "Parapet not built."));
            return;
        }
        var stas = _segs.SelectMany(x => new[] { x.r.Station0, x.r.Station1 }).Distinct().OrderBy(s => s).ToList();
        double c = pp.Chamfer, W = pp.Width, H = pp.Height;
        var sec = new List<Vec2> { new(0, 0), new(W, 0), new(W, H - c), new(W - c, H), new(c, H), new(0, H - c) };
        var up = new Vec3(0, 0, 1);
        foreach (int side in new[] { -1, 1 })
        {
            // pieces between the expansion joints (OPTIONAL JointRuns), else one piece over the box
            var cuts = ParapetJointStations(pp, side, stas[0], stas[^1]);
            var bounds = new List<(double a, double b)>();
            double at = stas[0];
            foreach (double j in cuts) { bounds.Add((at, j - pp.JointWidth / 2)); at = j + pp.JointWidth / 2; }
            bounds.Add((at, stas[^1]));
            var mesh = new Mesh();
            foreach (var (s0, s1) in bounds)
            {
                if (s1 - s0 < 1e-3) continue;
                var ps = new List<double> { s0 };
                ps.AddRange(stas.Where(x => x > s0 + 1e-4 && x < s1 - 1e-4));
                ps.Add(s1);
                var rings = new List<Vec3[]>();
                foreach (double s in ps)
                {
                    var f = FrameOnChord(s);
                    var e = new Vec3(f.Lat.X, f.Lat.Y, 0).Normalized() * side;   // outward, horizontal
                    var ib = f.At(new Vec2(side * pp.InsideOffset, 0));
                    var ob = f.At(new Vec2(side * (pp.InsideOffset + W), 0));
                    rings.Add(new[] { ib, ob, ob + up * (H - c), ob + up * H - e * c, ib + up * H + e * c, ib + up * (H - c) });
                }
                MeshBuilder.AddLoft(mesh, sec, rings, false);
            }
            _m.Parapets.Add(((side < 0 ? "Left" : "Right") + " parapet", mesh));
        }
        if (pp.Unresolved.Length > 0) _m.ParapetAssumption = Assumption.UnresolvedDimension(pp.Unresolved, "continuous plumb section on the box top");
        Info($"Segmental parapets: 2 x {(stas[^1] - stas[0]) / Ft(1):0.0} ft (box ends), {_m.ParapetVolume / Math.Pow(Ft(1), 3) / 27:0.0} CY"
            + (_m.ParapetJoints.Count > 0 ? $", {_m.ParapetJoints.Count(j => j.Side < 0)} / {_m.ParapetJoints.Count(j => j.Side > 0)} expansion joints left / right." : "."));
    }

    /// <summary>Joint stations of one parapet side from the OPTIONAL joint runs: per run the deck-edge arc (at JointEdgeOffset) between its ends, Spaces x
    /// JointSpacing centred (equal end lengths); printed end lengths / arcs become check rows (each printed pair matched to the closer side).</summary>
    private List<double> ParapetJointStations(SegmentalParapetDefinition pp, int side, double box0, double box1)
    {
        var res = new List<double>();
        if (pp.JointRuns is not { Count: > 0 } runs) return res;
        if (double.IsNaN(pp.JointEdgeOffset) || double.IsNaN(pp.JointSpacing) || double.IsNaN(pp.JointWidth))
        {
            Warn(UnresolvedFlag.Text(Pages, "parapet joints", "the deck-edge offset, joint spacing or joint width is not given", "Parapet built continuous."));
            return res;
        }
        double off = side * pp.JointEdgeOffset;
        foreach (var run in runs)
        {
            double a = run.From.Length == 0 ? box0 : _bentSta.TryGetValue(run.From, out var sa) ? sa : double.NaN;
            double b = run.To.Length == 0 ? box1 : _bentSta.TryGetValue(run.To, out var sb) ? sb : double.NaN;
            if (double.IsNaN(a) || double.IsNaN(b) || b <= a || run.Spaces <= 0) { Warn($"Segmental parapet joints: run {run.From} - {run.To} not resolved; no joints."); continue; }
            // arc along the edge line: dense samples
            int n = Math.Max(8, (int)Math.Ceiling((b - a) / 0.5));
            var st = new double[n + 1]; var arc = new double[n + 1];
            Vec2 prev = default;
            for (int i = 0; i <= n; i++)
            {
                st[i] = a + (b - a) * i / n;
                var q = _a.PointAt(st[i], off);
                arc[i] = i == 0 ? 0 : arc[i - 1] + (q - prev).Length;
                prev = q;
            }
            double L = arc[n], end = (L - run.Spaces * pp.JointSpacing) / 2;
            if (end < 0) { Warn($"Segmental parapet joints: run {run.From} - {run.To} is shorter ({L / Ft(1):0.00} ft) than {run.Spaces} spaces; no joints."); continue; }
            for (int k = 0; k <= run.Spaces; k++)
            {
                double target = end + k * pp.JointSpacing;
                int i = Array.FindIndex(arc, v => v >= target);
                if (i <= 0) i = 1;
                double s = st[i - 1] + (st[i] - st[i - 1]) * (target - arc[i - 1]) / Math.Max(1e-12, arc[i] - arc[i - 1]);
                if (s - pp.JointWidth / 2 > box0 && s + pp.JointWidth / 2 < box1) { res.Add(s); _m.ParapetJoints.Add((side, s)); }
            }
            string where = $"parapet joints {(side < 0 ? "left" : "right")} {(run.From.Length > 0 ? run.From : "unit end")} - {(run.To.Length > 0 ? run.To : "unit end")}";
            // printed values: the closer of A / B to this side's model value
            void Row(string what, double model, double pa, double pb)
            {
                var cands = new[] { pa, pb }.Where(v => !double.IsNaN(v)).ToList();
                if (cands.Count == 0) return;
                double plan = cands.OrderBy(v => Math.Abs(v - model)).First();
                _m.Checks.Add(new SegmentalCheck($"{where}: {what} ({run.Source})", model, plan, model - plan, "end lengths equal by the plan chain; the printed pair is matched to the closer side"));
            }
            Row("arc along the edge of deck", L, run.PrintedArcA, run.PrintedArcB);
            Row("end length", end, run.PrintedEndA, run.PrintedEndB);
        }
        res.Sort();
        return res;
    }

    // ------------------------------------------------------------------ bars

    private void BuildBars()
    {
        var defs = _d.Bars!;
        var segNames = _segs.Select(x => x.r.Name).ToList();
        var violations = new Dictionary<string, (int n, double worst)>();
        foreach (var g in defs.GroupBy(b => b.Mark))
        {
            var first = g.First();
            var size = RebarCatalog.Size(first.Size);
            int schedule = 0, placed = 0;
            var notes = new List<string>();
            var hosts = new List<Mesh>();
            foreach (var e in g)
            {
                var rx = new Regex(e.Segments.Length > 0 ? e.Segments : "^$", RegexOptions.IgnoreCase);
                var segs = _segs.Where(x => rx.IsMatch(x.r.Name)).ToList();
                schedule += e.CountPerSegment * segs.Count;
                if (e.Unresolved.Length > 0 && !notes.Contains(e.Unresolved)) notes.Add(e.Unresolved);
                if (e.Placement == SegmentalBarPlacement.ScheduleOnly) continue;
                if (double.IsNaN(e.FaceDistance) || e.Count <= 0 || e.ListedLength <= 0) { notes.Add($"{e.Mark} {e.Placement}: face distance / count / length missing - not placed"); continue; }
                foreach (var (r, d, _) in segs)
                {
                    hosts.Add(r.Mesh);
                    double mid = (r.Station0 + r.Station1) / 2, L = e.ListedLength;
                    var bars = new List<Vec3[]>();
                    for (int i = 0; i < e.Count; i++)
                    {
                        double o = (i - (e.Count - 1) / 2.0) * e.Spacing;
                        switch (e.Placement)
                        {
                            case SegmentalBarPlacement.TransverseTop:
                            case SegmentalBarPlacement.TransverseSoffit:
                            {
                                double sta = mid + o;
                                var f = ChordFrame(r, sta);
                                double y = e.Placement == SegmentalBarPlacement.TransverseTop ? e.FaceDistance : SectionIn(d, r, sta).h - e.FaceDistance;
                                bars.Add(new[] { f.At(new Vec2(-L / 2, y)), f.At(new Vec2(L / 2, y)) });
                                break;
                            }
                            default:
                            {
                                double s0 = mid - L / 2, s1 = mid + L / 2;
                                bool top = e.Placement == SegmentalBarPlacement.LongitudinalTop;
                                double Y(double s) => top ? e.FaceDistance : SectionIn(d, r, s).h - e.FaceDistance;
                                bars.Add(new[] { ChordFrame(r, s0).At(new Vec2(o, Y(s0))), ChordFrame(r, s1).At(new Vec2(o, Y(s1))) });
                                break;
                            }
                        }
                    }
                    var cover = new RebarCover(new[] { r.Mesh });
                    for (int k = 0; k < bars.Count; k++)
                    {
                        _m.Bars.Add(new BarPlacement { Mark = e.Mark, Points = bars[k], Group = "Segment", Unit = e.Unit });
                        placed++;
                        if (!double.IsNaN(e.Cover))
                        {
                            var (slack, at, face) = cover.Check(bars[k], size.Diameter / 2, e.Cover, e.CoverBottom, e.CoverTop);
                            if (slack < -In(1.0 / 16))
                            {
                                _m.CoverViolations.Add(new PlanBarCoverViolation(e.Unit, e.Mark, r.Name, k + 1, -slack, at, face));
                                var v = violations.GetValueOrDefault(e.Mark);
                                violations[e.Mark] = (v.n + 1, Math.Max(v.worst, -slack));
                            }
                        }
                    }
                }
            }
            string loc = first.Location + (placed < schedule ? $" ({schedule - placed} of {schedule} not modeled - UNRESOLVED{(notes.Count > 0 ? ": " + string.Join(" ", notes) : "")})" : "");
            var shape = first.ShapeCode is "STR" or "STR." ? BarShape.Straight(first.ListedLength)
                : new BarShape { Code = "TYPE " + first.ShapeCode, Description = first.PrintedDims, Centerline = new[] { new Vec2(0, 0), new Vec2(first.ListedLength, 0) }, FixedLength = first.ListedLength };
            _m.Marks.Add(new BarMark { Mark = first.Mark, Size = size, Shape = shape, Location = loc, Unit = first.Unit, Epoxy = first.Epoxy, Count = Math.Max(schedule, placed) });
        }
        foreach (var (mark, (n, worst)) in violations)
            Warn($"Segmental bars {mark}: {n} placed bars short of the plan clear cover by up to {worst / In(1):0.###} in (checked against the segment solids; bars not moved).");
        var bySeg = _m.Bars.GroupBy(b => b.Mark).Select(x => $"{x.Count()} {x.Key}");
        Info($"Segmental bars: {_m.Marks.Count} marks scheduled ({_m.Marks.Sum(x => x.Count)} bars, {_m.Marks.Sum(x => x.TotalMass) / 0.45359237:0} lb), {_m.Bars.Count} placed ({string.Join(", ", bySeg)}).");
    }
}
