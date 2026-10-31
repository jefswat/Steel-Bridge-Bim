using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL reinforcing of every segment from face-referenced section paths (SegmentalDefinition.SectionBars, Bridge/SegmentalSectionBars.cs;
// null = nothing here, output unchanged). Each bar is resolved in the section at its own station (depth, bottom slab and hollow / solid from the
// segment), so one definition follows the variable-depth box. Bars are never moved: clear cover is checked against the segment solid and reported.
public sealed partial class SegmentalGenerator
{
    /// <summary>A face line of the section at one station: point, unit direction, unit normal INTO the concrete.</summary>
    private readonly record struct FaceLine(Vec2 P, Vec2 D, Vec2 N);

    private static readonly HashSet<string> VoidFaces = new(StringComparer.OrdinalIgnoreCase) { "TOPIN", "HAUNCH", "WEBIN", "BHAUNCH", "BOTIN" };

    /// <summary>Face line of the section (h, t) by name ("L:" = mirrored), null when it does not exist (void face of a solid section, unknown name).</summary>
    private FaceLine? SectionFace(string name, double h, double t, bool hollow)
    {
        bool left = name.StartsWith("L:", StringComparison.OrdinalIgnoreCase);
        string n = (left ? name[2..] : name).ToUpperInvariant();
        var b = _d.Box;
        double rt = b.TopWidth / 2, w2 = b.HalfSoffit(h);
        Vec2 a0, a1; bool isVoid = VoidFaces.Contains(n);
        if (isVoid && !hollow) return null;
        double xc = b.WebRootX - b.WebHorizontal, yc = b.WebTopDepth;
        double yh = h - t - b.BottomHaunchRise, x4 = xc - (yh - yc) / b.WebSlope, x5 = x4 - b.BottomHaunchRun;
        switch (n)
        {
            case "TOP": a0 = new(0, 0); a1 = new(rt, 0); break;
            case "TIP": a0 = new(rt, 0); a1 = new(rt, b.WingTip); break;
            case "WINGFLAT": a0 = new(rt, b.WingTip); a1 = new(rt - b.WingFlat, b.WingTip); break;
            case "WING": a0 = new(rt - b.WingFlat, b.WingTip); a1 = new(b.WebRootX, b.WebTopDepth); break;
            case "WEBOUT": a0 = new(b.WebRootX, b.WebTopDepth); a1 = new(w2, h); break;
            case "SOFFIT": a0 = new(w2, h); a1 = new(0, h); break;
            case "CL": a0 = new(0, 1); a1 = new(0, 0); break;
            case "TOPIN": a0 = new(0, b.TopSlab); a1 = new(b.TopSlabFlat, b.TopSlab); break;
            case "HAUNCH": a0 = new(b.TopSlabFlat, b.TopSlab); a1 = new(xc, yc); break;
            case "WEBIN": a0 = new(xc, yc); a1 = new(x4, yh); break;
            case "BHAUNCH": a0 = new(x4, yh); a1 = new(x5, h - t); break;
            case "BOTIN": a0 = new(x5, h - t); a1 = new(0, h - t); break;
            default: return null;
        }
        var d = (a1 - a0).Normalized();
        // outer chain and void chain are both listed with positive area: concrete is LEFT of an outer edge, RIGHT of a void edge
        var nrm = isVoid ? new Vec2(d.Y, -d.X) : new Vec2(-d.Y, d.X);
        if (left) { a0 = new(-a0.X, a0.Y); d = new(-d.X, d.Y); nrm = new(-nrm.X, nrm.Y); }
        return new FaceLine(a0, d, nrm);
    }

    private Vec2? FreeDir(SegmentalBarPoint v, double h, double t, bool hollow)
    {
        Vec2 dir;
        string f = v.Face.ToUpperInvariant();
        if (f == "H") dir = new Vec2(1, 0);
        else if (f == "V") dir = new Vec2(0, 1);
        else
        {
            var fl = SectionFace(v.Face, h, t, hollow);
            if (fl is null) return null;
            dir = fl.Value.D;
        }
        return dir.X * v.DirX + dir.Y * v.DirY < 0 ? dir * -1 : dir;
    }

    /// <summary>Resolves the set's path; with <see cref="SegmentalSectionBarSet.FitLeg"/> the free leg is solved for the listed out-to-out length.</summary>
    private List<Vec2>? ResolveSet(SegmentalSectionBarSet e, double h, double t, bool hollow, bool mirror, out string why)
    {
        if (e.FitLeg < 0 || e.FitLeg >= e.Path.Count || e.ListedLength <= 0 || double.IsNaN(e.Path[e.FitLeg].Length))
            return ResolvePath(e.Path, h, t, hollow, mirror, out why);
        double db = RebarCatalog.Size(e.Size).Diameter;
        var path = e.Path.Select(p => p.Clone()).ToList();
        double L = Math.Max(path[e.FitLeg].Length, 0.05);
        List<Vec2>? res = null; why = "";
        for (int it = 0; it < 4; it++)
        {
            path[e.FitLeg].Length = L;
            res = ResolvePath(path, h, t, hollow, mirror, out why);
            if (res is null) return null;
            double cur = OutToOut(res.Select(q => new Vec3(q.X, q.Y, 0)).ToList(), db);
            L += e.ListedLength - cur;
            if (L <= 0) { why = "listed length shorter than the fixed legs"; return null; }
        }
        return res;
    }

    private static Vec2? Intersect(Vec2 p, Vec2 d, Vec2 q, Vec2 e)
    {
        double den = d.X * e.Y - d.Y * e.X;
        if (Math.Abs(den) < 1e-12) return null;
        double s = ((q.X - p.X) * e.Y - (q.Y - p.Y) * e.X) / den;
        return p + d * s;
    }

    /// <summary>Resolves a path in the section (h, t, hollow); null when a face does not exist there. <paramref name="mirror"/> mirrors the result.</summary>
    private List<Vec2>? ResolvePath(IReadOnlyList<SegmentalBarPoint> path, double h, double t, bool hollow, bool mirror, out string why)
    {
        why = "";
        var pts = new List<Vec2>(path.Count);
        var hidden = new List<bool>(path.Count);
        bool firstFree = path.Count > 1 && !double.IsNaN(path[0].Length);
        for (int vi = firstFree ? 1 : 0; vi < path.Count; vi++)
        {
            var v = path[vi];
            Vec2 p;
            if (!double.IsNaN(v.Length))
            {
                if (pts.Count == 0) { why = "a free leg needs a previous point"; return null; }
                var dd = FreeDir(v, h, t, hollow);
                if (dd is null) { why = $"face {v.Face} not in the section"; return null; }
                p = pts[^1] + dd.Value * v.Length;
                pts.Add(p); hidden.Add(v.Hidden);
                continue;
            }
            {
                var f1 = SectionFace(v.Face, h, t, hollow);
                if (f1 is null) { why = $"face {v.Face} not in the section"; return null; }
                var p1 = f1.Value.P + f1.Value.N * v.Offset;
                if (v.Face2.Length > 0)
                {
                    var f2 = SectionFace(v.Face2, h, t, hollow);
                    if (f2 is null) { why = $"face {v.Face2} not in the section"; return null; }
                    var q = Intersect(p1, f1.Value.D, f2.Value.P + f2.Value.N * v.Offset2, f2.Value.D);
                    if (q is null) { why = $"faces {v.Face} / {v.Face2} parallel"; return null; }
                    p = q.Value;
                }
                else if (!double.IsNaN(v.X))
                {
                    var q = Intersect(p1, f1.Value.D, new Vec2(v.X, 0), new Vec2(0, 1));
                    if (q is null) { why = $"face {v.Face} is vertical, X given"; return null; }
                    p = q.Value;
                }
                else if (!double.IsNaN(v.Y))
                {
                    var q = Intersect(p1, f1.Value.D, new Vec2(0, v.Y), new Vec2(1, 0));
                    if (q is null) { why = $"face {v.Face} is horizontal, Y given"; return null; }
                    p = q.Value;
                }
                else { why = $"point on {v.Face} needs Face2, X, Y or Length"; return null; }
            }
            pts.Add(p); hidden.Add(v.Hidden);
        }
        if (firstFree)
        {
            var dd = FreeDir(path[0], h, t, hollow);
            if (dd is null || pts.Count == 0) { why = $"face {path[0].Face} not in the section"; return null; }
            pts.Insert(0, pts[0] + dd.Value * path[0].Length); hidden.Insert(0, path[0].Hidden);
        }
        for (int i = pts.Count - 1; i >= 0; i--) if (hidden[i]) pts.RemoveAt(i);
        if (mirror) for (int i = 0; i < pts.Count; i++) pts[i] = new Vec2(-pts[i].X, pts[i].Y);
        return pts;
    }

    /// <summary>Out-to-out length of a sharp centerline (segment sum + db tan(deflection / 2) per bend, hooks beyond 100 deg count db).</summary>
    internal static double OutToOut(IReadOnlyList<Vec3> p, double db)
    {
        double L = 0;
        for (int i = 0; i + 1 < p.Count; i++) L += (p[i + 1] - p[i]).Length;
        for (int i = 1; i + 1 < p.Count; i++)
        {
            var a = (p[i] - p[i - 1]).Normalized(); var c = (p[i + 1] - p[i]).Normalized();
            double th = Math.Acos(Math.Clamp(Vec3.Dot(a, c), -1, 1));
            if (th < 1e-3) continue;
            L += th > 100 * Math.PI / 180 ? db : db * Math.Tan(th / 2);
        }
        return L;
    }

    private (double s0, double s1, int dir) AlongFrame(SegmentResult r, SegmentDefinition d, SegmentalBarAlongRef rf)
    {
        bool bentAtEnd = d.Bent != "" && _bentSta.TryGetValue(d.Bent, out double bs) && Math.Abs(bs - r.Station1) < Math.Abs(bs - r.Station0);
        return rf switch
        {
            SegmentalBarAlongRef.End => (r.Station1, r.Station0, -1),
            SegmentalBarAlongRef.BentSide => bentAtEnd ? (r.Station1, r.Station0, -1) : (r.Station0, r.Station1, 1),
            SegmentalBarAlongRef.TipSide => bentAtEnd ? (r.Station0, r.Station1, 1) : (r.Station1, r.Station0, -1),
            _ => (r.Station0, r.Station1, 1),
        };
    }

    private List<double> TransverseStations(SegmentResult r, SegmentDefinition d, SegmentalSectionBarSet e)
    {
        var (s0, s1, dir) = AlongFrame(r, d, e.AlongRef == SegmentalBarAlongRef.Centre ? SegmentalBarAlongRef.Start : e.AlongRef);
        var l = new List<double>();
        if (e.Positions is { Count: > 0 }) { foreach (var x in e.Positions) l.Add(s0 + dir * x); return l; }
        if (e.Count <= 0) return l;
        if (e.Spacing <= 0 && e.Count > 1)
        {
            double f0 = double.IsNaN(e.First) ? 0 : e.First, len = r.Station1 - r.Station0;
            for (int i = 0; i < e.Count; i++) l.Add(r.Station0 + f0 + (len - 2 * f0) * i / (e.Count - 1));
            return l;
        }
        if (e.AlongRef == SegmentalBarAlongRef.Centre || double.IsNaN(e.First))
        {
            double mid = (r.Station0 + r.Station1) / 2;
            for (int i = 0; i < e.Count; i++) l.Add(mid + (i - (e.Count - 1) / 2.0) * e.Spacing);
        }
        else
            for (int i = 0; i < e.Count; i++) l.Add(s0 + dir * (e.First + i * e.Spacing));
        return l;
    }

    private sealed class BarOut
    {
        public required string Unit, Mark, Seg;
        public int Def;
        public required Vec3[] Pts;
        public bool Check;
        public List<Vec2>? Section;
    }

    private void BuildSectionBars()
    {
        var defs = _d.SectionBars!;
        var segs = _segs.ToList();
        // per segment, in segment order (deterministic): bars, unplaced counts, skip reasons
        var perSeg = new (List<BarOut> bars, Dictionary<int, int> unplaced, Dictionary<int, string> why)[segs.Count];
        var rx = defs.Select(e => new Regex(e.Segments.Length > 0 ? e.Segments : "^$", RegexOptions.IgnoreCase)).ToArray();
        Parallel.For(0, segs.Count, si =>
        {
            var (r, d, _) = segs[si];
            var bars = new List<BarOut>(); var unplaced = new Dictionary<int, int>(); var why = new Dictionary<int, string>();
            for (int k = 0; k < defs.Count; k++)
            {
                var e = defs[k];
                if (!rx[k].IsMatch(r.Name)) continue;
                int miss = e.Unplaced;
                var mirrors = e.Mirror ? new[] { false, true } : new[] { false };
                if (e.Kind == SegmentalSectionBarKind.Transverse)
                {
                    var stas = TransverseStations(r, d, e);
                    for (int i = 0; i < stas.Count; i++)
                    {
                        double sta = stas[i];
                        if (sta < r.Station0 - 1e-6 || sta > r.Station1 + 1e-6) { miss += mirrors.Length; why.TryAdd(k, "station outside the segment"); continue; }
                        var (h, t, hollow) = SectionIn(d, r, sta);
                        var f = ChordFrame(r, sta);
                        foreach (bool m in mirrors)
                        {
                            var p2 = ResolveSet(e, h, t, hollow, m, out var w);
                            if (p2 is null) { miss++; why.TryAdd(k, w); continue; }
                            bars.Add(new BarOut { Unit = e.Unit, Mark = e.Mark, Seg = r.Name, Def = k, Pts = p2.Select(f.At).ToArray(), Check = i == 0 || i == stas.Count - 1 || i == stas.Count / 2, Section = p2 });
                        }
                    }
                }
                else
                {
                    var (s0, s1, dir) = AlongFrame(r, d, e.AlongRef == SegmentalBarAlongRef.Centre ? SegmentalBarAlongRef.Start : e.AlongRef);
                    double len = Math.Abs(s1 - s0);
                    double a0 = e.EndClear, a1 = (double.IsNaN(e.ZoneLength) ? len : Math.Min(len, e.ZoneLength)) - (double.IsNaN(e.ZoneLength) ? e.EndClear : 0);
                    int ns = Math.Max(2, e.LongSamples);
                    var stas = Enumerable.Range(0, ns).Select(j => s0 + dir * (a0 + (a1 - a0) * j / (ns - 1))).ToList();
                    var lines = new List<List<Vec2>>();
                    string w = "";
                    foreach (double sta in stas)
                    {
                        var (h, t, hollow) = SectionIn(d, r, Math.Clamp(sta, r.Station0, r.Station1));
                        var p2 = ResolveSet(e, h, t, hollow, false, out w);
                        if (p2 is null) break;
                        lines.Add(p2);
                    }
                    int n = Math.Max(1, e.Count);
                    if (lines.Count < stas.Count || lines[0].Count < 1) { miss += n * mirrors.Length; why.TryAdd(k, w); }
                    else
                        foreach (bool m in mirrors)
                            for (int b = 0; b < n; b++)
                            {
                                var pts = new Vec3[stas.Count];
                                for (int j = 0; j < stas.Count; j++)
                                {
                                    var q = PointOnLine(lines[j], n, b, e.Spacing);
                                    if (m) q = new Vec2(-q.X, q.Y);
                                    pts[j] = ChordFrame(r, stas[j]).At(q);
                                }
                                bars.Add(new BarOut { Unit = e.Unit, Mark = e.Mark, Seg = r.Name, Def = k, Pts = pts, Check = true });
                            }
                }
                if (miss > 0) unplaced[k] = unplaced.GetValueOrDefault(k) + miss;
            }
            perSeg[si] = (bars, unplaced, why);
        });

        // cover (per segment solid), then marks per (unit, mark)
        var defOf = new Dictionary<(string, string), SegmentalSectionBarSet>();
        foreach (var e in defs) defOf.TryAdd((e.Unit, e.Mark), e);
        var viol = new ConcurrentBag<(int si, PlanBarCoverViolation v)>();
        int checkedBars = 0;
        Parallel.For(0, segs.Count, si =>
        {
            var bars = perSeg[si].bars;
            if (bars.Count == 0) return;
            var cover = new RebarCover(new[] { segs[si].r.Mesh });
            int nb = 0;
            for (int i = 0; i < bars.Count; i++)
            {
                var b = bars[i];
                if (!b.Check) continue;
                var e = defs[b.Def];
                if (double.IsNaN(e.Cover)) continue;
                nb++;
                var (slack, at, face) = cover.Check(b.Pts, RebarCatalog.Size(e.Size).Diameter / 2, e.Cover, e.CoverBottom, e.CoverTop);
                if (slack < -In(1.0 / 16)) viol.Add((si, new PlanBarCoverViolation(b.Unit, b.Mark, b.Seg, i + 1, -slack, at, face)));
            }
            Interlocked.Add(ref checkedBars, nb);
        });
        foreach (var (_, v) in viol.OrderBy(x => x.si).ThenBy(x => x.v.Mark, StringComparer.Ordinal).ThenBy(x => x.v.Bar)) _m.CoverViolations.Add(v);

        var lengths = new Dictionary<(string, string), List<double>>();
        var placed = new Dictionary<(string, string), int>();
        var shape2 = new Dictionary<(string, string), List<Vec2>>();
        var segsWith = new Dictionary<(string, string), HashSet<string>>();
        for (int si = 0; si < segs.Count; si++)
            foreach (var b in perSeg[si].bars)
            {
                var key = (b.Unit, b.Mark);
                var e = defOf[key];
                _m.Bars.Add(new BarPlacement { Mark = b.Mark, Points = b.Pts, Group = "Segment", Unit = b.Unit });
                placed[key] = placed.GetValueOrDefault(key) + 1;
                (lengths.TryGetValue(key, out var ll) ? ll : lengths[key] = new()).Add(OutToOut(b.Pts, RebarCatalog.Size(e.Size).Diameter));
                if (b.Section != null) shape2.TryAdd(key, b.Section);
                (segsWith.TryGetValue(key, out var hs) ? hs : segsWith[key] = new()).Add(b.Seg);
            }
        var unplacedBy = new Dictionary<(string, string), int>();
        var whyBy = new Dictionary<(string, string), HashSet<string>>();
        for (int si = 0; si < segs.Count; si++)
        {
            foreach (var (k, n) in perSeg[si].unplaced) { var key = (defs[k].Unit, defs[k].Mark); unplacedBy[key] = unplacedBy.GetValueOrDefault(key) + n; }
            foreach (var (k, w) in perSeg[si].why) { var key = (defs[k].Unit, defs[k].Mark); (whyBy.TryGetValue(key, out var hs) ? hs : whyBy[key] = new()).Add(w); }
        }
        foreach (var e in defs) if (e.Unresolved.Length > 0) Warn(e.Unresolved);
        var order = defs.Select(e => (e.Unit, e.Mark)).Distinct().ToList();
        foreach (var key in order)
        {
            var e = defOf[key];
            var size = RebarCatalog.Size(e.Size);
            int np = placed.GetValueOrDefault(key), nu = unplacedBy.GetValueOrDefault(key);
            if (np + nu == 0) continue;
            var notes = defs.Where(x => (x.Unit, x.Mark) == key && x.Unresolved.Length > 0).Select(x => x.Unresolved).Distinct().ToList();
            string loc = e.Location;
            if (nu > 0)
            {
                var w = whyBy.TryGetValue(key, out var ws) ? string.Join("; ", ws.Where(s => s.Length > 0)) : "";
                loc += $" ({nu} of {np + nu} not modeled - UNRESOLVED{(w.Length > 0 ? ": " + w : "")})";
            }
            // short heads only (the full flag texts are raised once as warnings): every bar's IFC description / DB row carries the mark location
            if (notes.Count > 0) loc += " [" + string.Join("; ", notes.SelectMany(SplitFlags).Distinct()) + "]";
            List<double>? varying = null;
            double length = e.ListedLength;
            if (length <= 0 && lengths.TryGetValue(key, out var ls) && ls.Count > 0)
            {
                double lo = ls.Min(), hi = ls.Max();
                if (hi - lo > In(1))
                {
                    varying = ls.Select(x => Math.Round(x / In(1)) * In(1)).ToList();
                    for (int i = 0; i < nu; i++) varying.Add(varying.Max());
                }
                length = Math.Round(ls.Average() / In(1)) * In(1);
            }
            if (length <= 0) length = In(1);
            BarShape shape;
            if (e.ShapeCode is "STR" or "STR." || !shape2.TryGetValue(key, out var sec) || sec.Count < 3)
                shape = new BarShape { Code = e.ShapeCode is "STR" or "STR." ? "STR" : "TYPE " + e.ShapeCode, Description = e.PrintedDims.Length > 0 ? e.PrintedDims : "straight",
                    Centerline = new[] { new Vec2(0, 0), new Vec2(length, 0) }, FixedLength = length };
            else
            {
                var o = sec[0];
                shape = new BarShape { Code = "TYPE " + e.ShapeCode, Description = e.PrintedDims, Centerline = sec.Select(p => new Vec2(p.X - o.X, o.Y - p.Y)).ToArray(), FixedLength = length };
            }
            _m.Marks.Add(new BarMark { Mark = e.Mark, Size = size, Shape = shape, Location = loc, Unit = e.Unit, Epoxy = e.Epoxy, Count = np + nu, VaryingLengths = varying });
        }
        foreach (var g in _m.CoverViolations.Where(v => defOf.ContainsKey((v.Unit, v.Mark))).GroupBy(v => (v.Unit, v.Mark)))
            Warn($"Segmental bars {g.Key.Mark} ({g.Key.Unit}): {g.Count()} checked bars short of the plan clear cover by up to {g.Max(v => v.Excess) / In(1):0.###} in (segment solid; bars not moved).");
        int nSeg = segs.Count(s => perSeg[segs.IndexOf(s)].bars.Count > 0);
        Info($"Segmental section bars: {placed.Values.Sum()} placed in {nSeg} of {segs.Count} segments, {unplacedBy.Values.Sum()} counted not placed, {order.Count} marks; " +
             $"{checkedBars} bars cover-checked (transverse: first / middle / last of each set per segment), {_m.CoverViolations.Count(v => defOf.ContainsKey((v.Unit, v.Mark)))} short.");
    }

    /// <summary>Heads of the UNRESOLVED flags in a note ("UNRESOLVED (p.x): item"), without the reasons.</summary>
    private static IEnumerable<string> SplitFlags(string note)
    {
        foreach (Match m in Regex.Matches(note, @"UNRESOLVED \([^)]*\): (.*?) - "))
            yield return m.Value[..^3];
    }

    /// <summary>Point b of n on a distribution polyline: equal spaces over its length (ends included), or centred at <paramref name="spacing"/>.</summary>
    private static Vec2 PointOnLine(List<Vec2> line, int n, int b, double spacing)
    {
        if (line.Count == 1) return line[0];
        double total = 0;
        for (int i = 0; i + 1 < line.Count; i++) total += Vec2.Distance(line[i], line[i + 1]);
        double s = n == 1 ? 0 : spacing > 0 ? total / 2 + (b - (n - 1) / 2.0) * spacing : total * b / (n - 1);
        s = Math.Clamp(s, 0, total);
        for (int i = 0; i + 1 < line.Count; i++)
        {
            double l = Vec2.Distance(line[i], line[i + 1]);
            if (s <= l || i + 2 == line.Count) return line[i] + (line[i + 1] - line[i]) * (l > 0 ? Math.Min(1, s / l) : 0);
            s -= l;
        }
        return line[^1];
    }
}
