using System.Collections.Concurrent;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>
/// Plan-driven deck reinforcing (<see cref="DeckPlanReinforcement"/>): transverse zones, longitudinal bays, and the reconciliation with the
/// plan bar list. Nothing is invented: bar-list rows that cannot be placed stay in the schedule at the printed count, every difference
/// between the placed bars and the list is a diagnostic.
/// </summary>
public sealed partial class ConcreteGenerator
{
    private sealed record PlanBar(string Mark, RebarSize Size, string Loc, double Nominal, double Length, Vec3[] Points, BarShape? Shape = null);

    private void BuildPlanDeckRebar()
    {
        var r = Deck.Reinforcement;
        var plan = r.Plan!;
        var bag = new ConcurrentBag<PlanBar>();
        double t = Deck.Thickness, c = r.SideCover;
        double dTopT = plan.Transverse.Where(z => z.Layer == DeckBarLayer.Top).Select(z => RebarCatalog.Size(z.Size).Diameter).DefaultIfEmpty(RebarCatalog.Size(r.TopTransverseSize).Diameter).Max();
        double dBotT = plan.Transverse.Where(z => z.Layer == DeckBarLayer.Bottom).Select(z => RebarCatalog.Size(z.Size).Diameter).DefaultIfEmpty(RebarCatalog.Size(r.BottomTransverseSize).Diameter).Max();
        double ZT(DeckBarLayer l, double d) => l == DeckBarLayer.Top ? -r.TopCover - d / 2 : -t + r.BottomCover + d / 2;
        double ZL(DeckBarLayer l, double d) => l == DeckBarLayer.Top ? -r.TopCover - dTopT - d / 2 : -t + r.BottomCover + dBotT + d / 2;

        var lineD = TransverseLineDistances(plan);
        int zi = 0;
        foreach (var z in plan.Transverse) { BuildTransverseZone(z, zi, lineD[zi], bag, c, ZT); zi++; }
        zi = 0;
        BuildLongRuns(plan, bag, ZL);
        BuildEndDamBars(plan, bag);

        // ---- marks
        var marks = new List<BarMark>();
        var places = new List<BarPlacement>();
        foreach (var g in bag.GroupBy(b => b.Mark).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var first = g.First();
            var lens = g.Select(b => b.Length).ToList();
            double min = lens.Min(), max = lens.Max();
            bool fixedLen = max - min <= In(1) + 1e-9;
            double nominal = fixedLen ? (g.Any(b => b.Nominal > 0) ? g.First(b => b.Nominal > 0).Nominal : Math.Round(lens.Average() / In(1)) * In(1)) : max;
            string loc = plan.PlanBarList.FirstOrDefault(l => l.Mark == g.Key)?.Location is { Length: > 0 } s ? s : first.Loc;
            // end dam groups: bars of the mark that are counted but not modeled (their end's layout is not dimensioned)
            int unplaced = plan.EndDam.Where(e => e.Mark == g.Key).Sum(e => e.Unplaced);
            if (unplaced > 0) loc += $" ({lens.Count} in 3D, {unplaced} not modeled - UNRESOLVED)";
            marks.Add(new BarMark
            {
                Mark = g.Key, Size = first.Size, Shape = first.Shape ?? BarShape.Straight(nominal), Location = loc, Epoxy = r.EpoxyCoated, Count = lens.Count + unplaced,
                VaryingLengths = fixedLen || unplaced > 0 ? null : lens.Select(l => Math.Round(l / In(1)) * In(1)).ToList(),
            });
            places.AddRange(g.Select(b => new BarPlacement { Mark = g.Key, Points = b.Points }));
        }
        Reconcile(plan, marks);
        lock (_m) { _m.Marks.AddRange(marks); _m.Bars.AddRange(places); }
    }

    // ------------------------------------------------------------------ transverse zones

    /// <summary>Points along a deck edge from the begin deck-end line: cumulative distance → plan point and alignment distance.</summary>
    private sealed class EdgePath
    {
        public required Vec2[] P; public required double[] D; public required double[] S;
        /// <summary>Point of the edge at an alignment distance (also beyond the deck, along the same offset curve).</summary>
        public required Func<double, Vec2> PointAtD;
        public (Vec2 p, double d) At(double s)
        {
            if (s < 0)
            {
                double dd = D[0] + s * (D[1] - D[0]) / Math.Max(S[1] - S[0], 1e-9);
                return (PointAtD(dd), dd);
            }
            if (s > S[^1])
            {
                int n = S.Length - 1;
                double dd = D[n] + (s - S[n]) * (D[n] - D[n - 1]) / Math.Max(S[n] - S[n - 1], 1e-9);
                return (PointAtD(dd), dd);
            }
            if (s == 0) return (P[0], D[0]);
            int i = Array.BinarySearch(S, s);
            if (i < 0) i = ~i;
            double f = (s - S[i - 1]) / Math.Max(S[i] - S[i - 1], 1e-12);
            return (Vec2.Lerp(P[i - 1], P[i], f), D[i - 1] + (D[i] - D[i - 1]) * f);
        }
    }

    private readonly ConcurrentDictionary<DeckBarSide, EdgePath> _edgePaths = new();

    private EdgePath EdgeOf(DeckBarSide side) => _edgePaths.GetOrAdd(side, _ =>
    {
        Func<double, double> off = side == DeckBarSide.Left ? LeftAtD : RightAtD;
        var (b, e) = EndsVar(off);
        int n = Math.Max(8, (int)Math.Ceiling((e - b) / 0.5));
        var P = new Vec2[n + 1]; var D = new double[n + 1]; var S = new double[n + 1];
        for (int i = 0; i <= n; i++)
        {
            D[i] = b + (e - b) * i / n;
            P[i] = _a.PointAtDistance(D[i], off(D[i]));
            if (i > 0) S[i] = S[i - 1] + Vec2.Distance(P[i - 1], P[i]);
        }
        return new EdgePath { P = P, D = D, S = S, PointAtD = d => _a.PointAtDistance(d, off(d)) };
    });

    private bool LineAcrossVar(Vec2 a, Vec2 dir, Func<double, double> l, Func<double, double> r, double guess, out Vec2 p0, out Vec2 p1)
    {
        p0 = p1 = default;
        double w = Math.Abs(l(guess)) + Math.Abs(r(guess)) + 20;
        if (!_a.IntersectOffsetCurveWithLine(l, a, dir, guess - w, guess + w, guess, out double dl)) return false;
        if (!_a.IntersectOffsetCurveWithLine(r, a, dir, guess - w, guess + w, guess, out double dr)) return false;
        p0 = _a.PointAtDistance(dl, l(dl));
        p1 = _a.PointAtDistance(dr, r(dr));
        return true;
    }

    private static double SAtD(EdgePath e, double d)
    {
        if (d < e.D[0]) return (d - e.D[0]) * (e.S[1] - e.S[0]) / Math.Max(e.D[1] - e.D[0], 1e-9);
        int n = e.D.Length - 1;
        if (d > e.D[n]) return e.S[n] + (d - e.D[n]) * (e.S[n] - e.S[n - 1]) / Math.Max(e.D[n] - e.D[n - 1], 1e-9);
        int i = Array.BinarySearch(e.D, d);
        if (i >= 0) return e.S[i];
        i = ~i;
        return e.S[i - 1] + (e.S[i] - e.S[i - 1]) * (d - e.D[i - 1]) / (e.D[i] - e.D[i - 1]);
    }

    /// <summary>Alignment distance of every line of every transverse zone (lines are radial, so the distance fixes the line); also reports the edge lengths and gaps between runs.</summary>
    private List<double>[] TransverseLineDistances(DeckPlanReinforcement plan)
    {
        var res = new List<double>[plan.Transverse.Count];
        double Ft1 = Ft(1);
        var notes = new List<string>
        {
            $"Edge lengths from the begin to the end deck-end line: left {_u.FormatLength(EdgeOf(DeckBarSide.Left).S[^1], LengthKind.Long)}, right {_u.FormatLength(EdgeOf(DeckBarSide.Right).S[^1], LengthKind.Long)}.",
        };
        for (int i = 0; i < plan.Transverse.Count; i++)
        {
            var z = plan.Transverse[i];
            var list = new List<double>();
            res[i] = list;
            var edge = EdgeOf(z.Reference);
            if (z.BetweenZone >= 0)
            {
                if (z.BetweenZone >= i) { Warn($"Deck reinforcing, transverse zone {i + 1}: BetweenZone must refer to an earlier zone."); continue; }
                var bd = res[z.BetweenZone];
                for (int k = 0; k + 1 < bd.Count; k++) list.Add((bd[k] + bd[k + 1]) / 2);
                if (z.BetweenToNext)
                {
                    int nx = Enumerable.Range(z.BetweenZone + 1, plan.Transverse.Count - z.BetweenZone - 1)
                        .FirstOrDefault(q => plan.Transverse[q].Layer == plan.Transverse[z.BetweenZone].Layer && plan.Transverse[q].BetweenZone < 0, -1);
                    if (nx >= 0 && res[nx] is { Count: > 0 } nl && bd.Count > 0) list.Add((bd[^1] + nl[0]) / 2);
                    else if (nx < 0) Warn($"Deck reinforcing, transverse zone {i + 1}: no following zone for BetweenToNext.");
                }
                if (z.Count > 0 && z.Count < list.Count) list = res[i] = list.Take(z.Count).ToList();
                if (z.Count > 0 && z.Count > list.Count) Warn($"Deck reinforcing, transverse zone {i + 1} ({z.Name}): {z.Count} lines listed but only {list.Count} gaps between the lines of zone {z.BetweenZone + 1}.");
                continue;
            }
            double s0;
            if (z.AnchorToEnd) s0 = edge.S[^1] - z.StartDistance - (z.Count - 1) * z.Spacing;
            else if (z.StartAfterPrevious)
            {
                int pv = Enumerable.Range(0, i).Where(q => plan.Transverse[q].Layer == z.Layer && plan.Transverse[q].BetweenZone < 0).DefaultIfEmpty(-1).Last();
                if (pv < 0 || res[pv].Count == 0) { Warn($"Deck reinforcing, transverse zone {i + 1}: StartAfterPrevious without a previous zone."); continue; }
                s0 = SAtD(edge, res[pv][^1]) + z.StartDistance;
            }
            else s0 = z.StartDistance;
            int outside = 0;
            for (int k = 0; k < z.Count; k++)
            {
                double sk = s0 + k * z.Spacing;
                if (sk < -Ft(400) || sk > edge.S[^1] + Ft(400)) { outside++; continue; }
                list.Add(edge.At(sk).d);
            }
            if (outside > 0) Warn($"Deck reinforcing, transverse zone {i + 1} ({z.Name}): {outside} of {z.Count} lines lie more than 400 ft beyond the {z.Reference.ToString().ToLower()} edge ({_u.FormatLength(edge.S[^1], LengthKind.Long)} long) - not placed.");
            if (list.Count > 0)
                notes.Add($"Transverse zone {i + 1} ({z.Name}, {z.Layer}): {z.Count} lines, first at {_u.FormatLength(s0, LengthKind.Long)}, last at {_u.FormatLength(s0 + (z.Count - 1) * z.Spacing, LengthKind.Long)} along the {z.Reference.ToString().ToLower()} edge" +
                    (list.Count > 0 && z.Reference == DeckBarSide.Right ? "" : "") + ".");
        }
        // gap between consecutive runs of one layer, along the right edge (plan: "10")
        foreach (var layer in new[] { DeckBarLayer.Top, DeckBarLayer.Bottom })
        {
            var idx = Enumerable.Range(0, plan.Transverse.Count).Where(q => plan.Transverse[q].Layer == layer && plan.Transverse[q].BetweenZone < 0 && res[q].Count > 0).ToList();
            for (int a = 0; a + 1 < idx.Count; a++)
            {
                var er = EdgeOf(DeckBarSide.Right); var el = EdgeOf(DeckBarSide.Left);
                double gr = SAtD(er, res[idx[a + 1]][0]) - SAtD(er, res[idx[a]][^1]);
                double gl = SAtD(el, res[idx[a + 1]][0]) - SAtD(el, res[idx[a]][^1]);
                notes.Add($"{layer}: gap between zone {idx[a] + 1} (last line) and zone {idx[a + 1] + 1} (first line): {_u.FormatLength(gr, LengthKind.Short)} along the right edge, {_u.FormatLength(gl, LengthKind.Short)} along the left edge.");
            }
        }
        lock (_m.Notes) _m.Notes.AddRange(notes);
        return res;
    }

    private void BuildTransverseZone(DeckTransverseZone z, int index, List<double> lineD, ConcurrentBag<PlanBar> bag, double cover, Func<DeckBarLayer, double, double> zT)
    {
        string zn = $"transverse zone {index + 1}{(z.Name.Length > 0 ? " (" + z.Name + ")" : "")}";
        if (lineD.Count == 0 || z.Pieces.Count == 0) { if (z.Pieces.Count == 0 || z.BetweenZone < 0) Warn($"Deck reinforcing, {zn}: no lines or no pieces - skipped."); return; }
        var size = RebarCatalog.Size(z.Size);
        double zOff = zT(z.Layer, size.Diameter);
        string loc = $"Deck {(z.Layer == DeckBarLayer.Top ? "top" : "bottom")} transverse";
        var pl = Deck.Reinforcement.Plan!;
        double clearB = pl.TransverseBeginClear > 0 ? pl.TransverseBeginClear : cover, clearE = pl.TransverseEndClear > 0 ? pl.TransverseEndClear : cover;
        int missed = 0, shortened = 0;
        var sink = new ConcurrentBag<PlanBar>();
        var stats = new ConcurrentDictionary<int, (double L, string pieces)>();
        Parallel.For(0, lineD.Count, i =>
        {
            double d = lineD[i];
            var tangent = Vec2.FromAngle(_a.EvaluateAtDistance(d).angle);
            var dir = tangent.Right;
            if (z.AngleDegrees != 0) dir = dir.Rotate(z.AngleDegrees * Math.PI / 180);
            var a = _a.PointAtDistance(d);
            if (!LineAcrossVar(a, dir, x => LeftAtD(x) + cover, x => RightAtD(x) - cover, d, out var p0, out var p1) || !ClipToEnds(ref p0, ref p1, clearB, clearE))
            { Interlocked.Increment(ref missed); return; }
            // reference point must lie on the line (the line is anchored at the reference edge point by construction when AngleDegrees = 0)
            double L = Vec2.Distance(p0, p1);
            var dirLine = (p1 - p0).Normalized();
            bool fromLeft = z.FirstFrom == DeckBarSide.Left;
            if (z.Alternate && i % 2 == 1) fromLeft = !fromLeft;
            var o = fromLeft ? p0 : p1;
            var u = fromLeft ? dirLine : -dirLine;
            double cursor = 0;
            bool clipped = false;
            var desc = new System.Text.StringBuilder();
            for (int k = 0; k < z.Pieces.Count; k++)
            {
                var pc = z.Pieces[k];
                bool last = k == z.Pieces.Count - 1;
                double len = pc.Length > 0 ? pc.Length : Math.Max(0, L - cursor + z.Lap);
                double st = last && k > 0 && pc.Length > 0 ? L - len : cursor;
                double en = st + len;
                if (st < -1e-6) { st = 0; clipped = true; }
                if (en > L + 1e-6) { en = L; clipped = true; }
                if (st >= L - 1e-6) { clipped = true; break; }
                if (en - st < In(1)) continue;
                var pts = SampleBar(o + u * st, o + u * en, zOff);
                sink.Add(new PlanBar(pc.Mark, size, loc, pc.Length, en - st, pts));
                desc.Append(desc.Length > 0 ? " + " : "").Append(pc.Mark).Append(' ').Append(_u.FormatLength(en - st, LengthKind.Long));
                cursor = en - z.Lap;
            }
            stats[i] = (L, desc.ToString());
            if (clipped) Interlocked.Increment(ref shortened);
        });
        foreach (var b in sink) bag.Add(b);
        if (stats.Count > 0)
        {
            int f = stats.Keys.Min(), l = stats.Keys.Max();
            lock (_m.Notes)
                _m.Notes.Add($"  {zn}: {stats.Count} lines; line {f + 1}: {_u.FormatLength(stats[f].L, LengthKind.Long)} = {stats[f].pieces}; line {l + 1}: {_u.FormatLength(stats[l].L, LengthKind.Long)} = {stats[l].pieces}; "
                           + $"shortest {_u.FormatLength(stats.Values.Min(v => v.L), LengthKind.Long)}, longest {_u.FormatLength(stats.Values.Max(v => v.L), LengthKind.Long)}.");
        }
        if (missed > 0) Warn($"Deck reinforcing, {zn}: {missed} of {lineD.Count} lines fall outside the deck - not placed.");
        if (shortened > 0) Warn($"Deck reinforcing, {zn}: {shortened} of {lineD.Count} lines are shorter than the listed fixed pieces; the pieces were shortened to the deck (plan marks for such lines are not distinguished).");
    }

    // ------------------------------------------------------------------ longitudinal zones

    private Func<double, double> RefOffset(DeckLongRef r, string where)
    {
        switch (r.Kind)
        {
            case DeckLongRefKind.LeftEdge: return LeftAtD;
            case DeckLongRefKind.RightEdge: return RightAtD;
            case DeckLongRefKind.Offset: { double o = r.Offset; return _ => o; }
            default:
                {
                    int gi = r.Girder - 1;
                    if (gi < 0 || gi >= _f.Girders.Count) { Warn($"Deck reinforcing, {where}: girder G{r.Girder} does not exist; the zone is skipped."); return null!; }
                    var web = _f.Girders[gi].WebLine;
                    var dd = new List<double>(); var oo = new List<double>();
                    foreach (var p in web)
                    {
                        var so = _a.Inverse(p.XY);
                        if (dd.Count > 0 && so.Distance <= dd[^1]) continue;
                        dd.Add(so.Distance); oo.Add(so.Offset);
                    }
                    if (dd.Count < 2) return null!;
                    return d =>
                    {
                        if (d <= dd[0]) return oo[0];
                        if (d >= dd[^1]) return oo[^1];
                        int i = dd.BinarySearch(d);
                        if (i >= 0) return oo[i];
                        i = ~i;
                        return oo[i - 1] + (oo[i] - oo[i - 1]) * (d - dd[i - 1]) / (dd[i] - dd[i - 1]);
                    };
                }
        }
    }

    private static bool SameRef(DeckLongRef a, DeckLongRef b) => a.Kind == b.Kind && a.Girder == b.Girder && Math.Abs(a.Offset - b.Offset) < 1e-9;

    private static bool Linkable(DeckLongZone a, DeckLongZone b) =>
        a.Positions == null && b.Positions == null && SameRef(a.Left, b.Left) && SameRef(a.Right, b.Right) && a.Mark == b.Mark && a.Size == b.Size && a.Alternate == b.Alternate
        && a.FirstLayer == b.FirstLayer && !double.IsNaN(a.EndStation) && !double.IsNaN(b.StartStation) && Math.Abs(a.EndStation - b.StartStation) < 1e-6;

    /// <summary>
    /// Longitudinal zones in runs: consecutive zones between the same two reference lines whose station ranges abut ("24 EQ. SPA." changing to "22 EQ. SPA.") share their
    /// bars - a line of one zone continues as the nearest line of the same layer in the next zone (a small lateral shift at the boundary) when there is one, else the bar
    /// ends / a new one starts. Bars are lap-spliced in stock lengths along the whole track, so the bar count follows the length of the lines, not the number of zones.
    /// </summary>
    private void BuildLongRuns(DeckPlanReinforcement plan, ConcurrentBag<PlanBar> bag, Func<DeckBarLayer, double, double> zL)
    {
        var zones = plan.Longitudinal;
        var used = new bool[zones.Count];
        int runNo = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            if (used[i]) continue;
            var chain = new List<DeckLongZone> { zones[i] };
            used[i] = true;
            // follow the zones that continue this one (same reference lines, mark and size, the next starting where this ends) - not necessarily adjacent in the list
            while (true)
            {
                int nx = Enumerable.Range(0, zones.Count).FirstOrDefault(k => !used[k] && Linkable(chain[^1], zones[k]), -1);
                if (nx < 0) break;
                used[nx] = true; chain.Add(zones[nx]);
            }
            BuildLongRun(chain, ++runNo, bag, plan, zL);
        }
    }

    private sealed class LongTrack { public List<(int zone, int k)> Segs = new(); public DeckBarLayer Layer; }

    private void BuildLongRun(List<DeckLongZone> run, int runNo, ConcurrentBag<PlanBar> bag, DeckPlanReinforcement plan, Func<DeckBarLayer, double, double> zL)
    {
        var z0 = run[0];
        string rn = $"longitudinal run {runNo} ({z0.Name}{(run.Count > 1 ? $" .. {run[^1].Name}" : "")})";
        var size = RebarCatalog.Size(z0.Size);
        if (z0.StockLength <= z0.Lap + Ft(1) || z0.Mark.Length == 0) { Warn($"Deck reinforcing, {rn}: stock length / lap / mark missing - skipped."); return; }
        var oL = new Func<double, double>[run.Count]; var oR = new Func<double, double>[run.Count];
        for (int q = 0; q < run.Count; q++)
        {
            oL[q] = RefOffset(run[q].Left, rn); oR[q] = RefOffset(run[q].Right, rn);
            if (oL[q] == null || oR[q] == null) return;
        }
        double coverE = plan.LongitudinalEndCover > 0 ? plan.LongitudinalEndCover : Deck.Reinforcement.SideCover;
        double coverB = plan.LongitudinalBeginCover > 0 ? plan.LongitudinalBeginCover : coverE;
        double zTop = zL(DeckBarLayer.Top, size.Diameter), zBot = zL(DeckBarLayer.Bottom, size.Diameter);
        string closure = z0.ClosureMark.Length > 0 ? z0.ClosureMark : z0.Mark;
        int Lines(DeckLongZone z) => z.Positions is { Count: > 0 } ? z.Positions.Count : z.Spaces + 1;
        DeckBarLayer LayerOf(DeckLongZone z, int k) =>
            z.PositionLayers is { Count: > 0 } pl && k < pl.Count ? pl[k] : z.Alternate && k % 2 == 1 ? (z.FirstLayer == DeckBarLayer.Top ? DeckBarLayer.Bottom : DeckBarLayer.Top) : z.FirstLayer;
        Func<double, double> OffOf(int q, int k)
        {
            var z = run[q];
            if (z.Positions is { Count: > 0 } pos) { double v = pos[k]; return z.PositionsFromRight ? d => oR[q](d) - v : d => oL[q](d) + v; }
            double f = z.Spaces == 0 ? 0 : (double)k / z.Spaces;
            return d => { double l = oL[q](d) + z.LeftMargin, r = oR[q](d) - z.RightMargin; return l + (r - l) * f; };
        }

        // tracks: link lines of consecutive zones by layer and nearest fraction across the bay
        var tracks = new List<LongTrack>();
        var open = new Dictionary<int, LongTrack>();      // line index of the previous zone -> its track
        for (int q = 0; q < run.Count; q++)
        {
            int nq = Lines(run[q]);
            var next = new Dictionary<int, LongTrack>();
            if (q > 0 && run[q].Positions == null)
            {
                double Fr(DeckLongZone z, int k) => z.Spaces == 0 ? 0 : (double)k / z.Spaces;
                foreach (var layer in new[] { DeckBarLayer.Top, DeckBarLayer.Bottom })
                {
                    var prevK = open.Keys.Where(k => open[k].Layer == layer).ToList();
                    var curK = Enumerable.Range(0, nq).Where(k => LayerOf(run[q], k) == layer).ToList();
                    double thr = 0.6 * 2.0 / Math.Max(1, Math.Min(Math.Max(run[q - 1].Spaces, 1), Math.Max(run[q].Spaces, 1)));
                    var pairs = (from a in prevK from b in curK select (a, b, d: Math.Abs(Fr(run[q - 1], a) - Fr(run[q], b)))).Where(t => t.d <= thr).OrderBy(t => t.d).ToList();
                    var usedA = new HashSet<int>(); var usedB = new HashSet<int>();
                    foreach (var (a, b, _) in pairs)
                    {
                        if (usedA.Contains(a) || usedB.Contains(b)) continue;
                        usedA.Add(a); usedB.Add(b);
                        open[a].Segs.Add((q, b));
                        next[b] = open[a];
                    }
                }
            }
            for (int k = 0; k < nq; k++)
                if (!next.ContainsKey(k))
                {
                    var t = new LongTrack { Layer = LayerOf(run[q], k) };
                    t.Segs.Add((q, k));
                    tracks.Add(t); next[k] = t;
                }
            open = next;
        }

        var sink = new ConcurrentBag<PlanBar>();
        int bad = 0;
        string loc = "Deck longitudinal, top & bottom";
        Parallel.ForEach(tracks, t =>
        {
            double zOff = t.Layer == DeckBarLayer.Top ? zTop : zBot;
            var pts = new List<Vec3>(); var cum = new List<double>();
            int lastZone = t.Segs[^1].zone;
            foreach (var (q, k) in t.Segs)
            {
                var z = run[q];
                var off = OffOf(q, k);
                var (eb, ee) = EndsVar(off, coverB, coverE);
                double d0 = double.IsNaN(z.StartStation) ? eb : Math.Max(eb, _a.StationToDistance(z.StartStation));
                // a track that stops at the end of this zone laps Lap into the next one; a continuing track just runs on
                double d1 = double.IsNaN(z.EndStation) ? ee : Math.Min(ee, _a.StationToDistance(z.EndStation) + (q == lastZone && q < run.Count - 1 ? z.Lap : 0));
                if (d1 - d0 < Ft(0.5)) continue;
                int n = Math.Max(2, (int)Math.Ceiling((d1 - d0) / 1.0));
                for (int s = 0; s <= n; s++)
                {
                    double d = d0 + (d1 - d0) * s / n;
                    double o = off(d);
                    var p = _a.PointAtDistance(d, o);
                    pts.Add(p.WithZ(Deck.FinishedElevation(_a, _a.DistanceToStation(d), o) + zOff));
                    cum.Add(cum.Count == 0 ? 0 : cum[^1] + Vec2.Distance(pts[^2].XY, p));
                }
            }
            if (pts.Count < 2 || cum[^1] < Ft(2)) { Interlocked.Increment(ref bad); return; }
            var P = pts.ToArray(); var C = cum.ToArray();
            double Lt = C[^1];
            void Emit(double a, double b, string mark, double nominal) => sink.Add(new PlanBar(mark, size, loc, nominal, b - a, SubPolyline2D(P, C, a, b)));
            if (Lt <= z0.StockLength + 1e-6)
            {
                bool stock = Math.Abs(Lt - z0.StockLength) <= In(1);
                Emit(0, Lt, stock ? z0.Mark : closure, stock ? z0.StockLength : 0);
                return;
            }
            int npc = (int)Math.Ceiling((Lt - z0.Lap) / (z0.StockLength - z0.Lap) - 1e-9);
            for (int q = 0; q < npc; q++)
            {
                double a = q * (z0.StockLength - z0.Lap);
                if (q < npc - 1) { Emit(a, a + z0.StockLength, z0.Mark, z0.StockLength); continue; }
                if (z0.LastIsStock) Emit(Lt - z0.StockLength, Lt, z0.Mark, z0.StockLength);
                else Emit(a, Lt, closure, 0);
            }
        });
        foreach (var b in sink) bag.Add(b);
        if (sink.Count > 0)
            lock (_m.Notes)
                _m.Notes.Add($"  {rn}: {tracks.Count} bar lines, {sink.Count} bars, {sink.Sum(b => b.Length) / 0.3048:N0} ft ({sink.Sum(b => b.Length * b.Size.MassPerLength) * 2.2046226218:N0} lb), zones {string.Join("/", run.Select(z => z.Positions != null ? z.Positions.Count : z.Spaces))}");
        if (bad > 0) Warn($"Deck reinforcing, {rn}: {bad} of {tracks.Count} bar lines are shorter than 2 ft or outside the deck (check the station range) - not placed.");
    }

    private static Vec3[] SubPolyline2D(Vec3[] pts, double[] cum, double a, double b)
    {
        Vec3 At(double s)
        {
            if (s <= 0) return pts[0];
            int i = Array.FindIndex(cum, x => x >= s);
            if (i <= 0) return pts[^1];
            return Vec3.Lerp(pts[i - 1], pts[i], (s - cum[i - 1]) / Math.Max(cum[i] - cum[i - 1], 1e-12));
        }
        var list = new List<Vec3> { At(a) };
        for (int i = 0; i < pts.Length; i++) if (cum[i] > a + 1e-9 && cum[i] < b - 1e-9) list.Add(pts[i]);
        list.Add(At(b));
        return list.ToArray();
    }

    // ------------------------------------------------------------------ plan bar list reconciliation

    private void Reconcile(DeckPlanReinforcement plan, List<BarMark> marks)
    {
        const double Lb = 2.2046226218;
        var byMark = marks.ToDictionary(m => m.Mark);
        var notes = new List<string>();
        double planTotal = 0, modelPlaced = 0, modelSched = 0;
        var sched = new List<BarMark>();
        foreach (var fam in plan.Families)
        {
            var rows = plan.PlanBarList.Where(l => l.Mark.StartsWith(fam)).ToList();
            var placed = marks.Where(m => m.Mark.StartsWith(fam)).ToList();
            if (rows.Count == 0 || placed.Count == 0) continue;
            double pw = rows.Sum(l => l.WeightLb), mw = placed.Sum(m => m.TotalMass) * Lb;
            int pc = rows.Sum(l => l.Count), mc = placed.Sum(m => m.Count);
            planTotal += pw; modelPlaced += mw;
            notes.Add($"family {fam}: model {mc} bars ({string.Join(", ", placed.OrderBy(m => m.Mark).Select(m => $"{m.Mark} x{m.Count}"))}) = {mw:N0} lb; plan {pc} bars ({string.Join(", ", rows.Select(l => $"{l.Mark} x{l.Count}"))}) = {pw:N0} lb ({(pw > 0 ? (mw / pw - 1) * 100 : 0):+0.0;-0.0}%)");
            if (pw > 0 && Math.Abs(mw / pw - 1) > plan.WeightTolerance)
                Warn($"Deck reinforcing vs plan bar list, mark family {fam}: model {mc} bars / {mw:N0} lb, plan {pc} bars / {pw:N0} lb ({(mw / pw - 1) * 100:+0.0;-0.0}%) - not adjusted.");
        }
        bool InFamily(string mark) => plan.Families.Any(f => mark.StartsWith(f));
        foreach (var line in plan.PlanBarList)
        {
            if (InFamily(line.Mark)) continue;
            planTotal += line.WeightLb;
            if (byMark.TryGetValue(line.Mark, out var m))
            {
                double w = m.TotalMass * Lb;
                modelPlaced += w;
                bool countOk = m.Count == line.Count;
                bool wOk = line.WeightLb <= 0 || Math.Abs(w - line.WeightLb) <= plan.WeightTolerance * line.WeightLb;
                notes.Add($"{line.Mark}: model {m.Count} x {_u.FormatLength(m.Varies ? m.MinLength : m.Length, LengthKind.Short)}{(m.Varies ? "-" + _u.FormatLength(m.Length, LengthKind.Short) : "")} = {w:N0} lb; plan {line.Count} x {(line.Varies ? "VARIES" : _u.FormatLength(line.Length, LengthKind.Short))} = {line.WeightLb:N0} lb ({(line.WeightLb > 0 ? (w / line.WeightLb - 1) * 100 : 0):+0.0;-0.0}%)");
                if (!countOk || !wOk)
                    Warn($"Deck reinforcing vs plan bar list, mark {line.Mark}: model {m.Count} bars / {w:N0} lb, plan {line.Count} bars / {line.WeightLb:N0} lb ({(line.WeightLb > 0 ? (w / line.WeightLb - 1) * 100 : 0):+0.0;-0.0}%) - not adjusted.");
                continue;
            }
            // not placed: schedule only at the printed count / length
            var size = RebarCatalog.Size(line.Size);
            List<double>? varying = null;
            if (line.Varies && line.WeightLb > 0 && line.Count > 1)
            {
                double avg = line.WeightLb * 0.45359237 / size.MassPerLength / line.Count;
                varying = Enumerable.Repeat(avg, line.Count).ToList();
                varying[0] = line.Length;
            }
            double ww = line.WeightLb > 0 ? line.WeightLb : line.Count * line.Length * size.MassPerLength * Lb;
            modelSched += ww;
            sched.Add(new BarMark
            {
                Mark = line.Mark, Size = size, Shape = BarShape.Straight(line.Length), Location = (line.Location.Length > 0 ? line.Location : "Deck (plan bar list)") + " (schedule only)",
                Epoxy = Deck.Reinforcement.EpoxyCoated, Count = line.Count, VaryingLengths = varying,
            });
            notes.Add($"{line.Mark}: not placed in 3D - schedule only, {line.Count} x {(line.Varies ? "VARIES" : _u.FormatLength(line.Length, LengthKind.Short))} = {ww:N0} lb");
        }
        foreach (var m in marks.Where(m => !InFamily(m.Mark) && plan.PlanBarList.All(l => l.Mark != m.Mark)))
            Warn($"Deck reinforcing: mark {m.Mark} ({m.Count} bars, {m.TotalMass * Lb:N0} lb) is placed but is not in the plan bar list.");
        marks.AddRange(sched);
        lock (_m.Notes)
        {
            _m.Notes.Add($"Plan-driven deck reinforcing: {plan.PlanBarList.Count} bar-list rows; placed {modelPlaced:N0} lb in 3D, schedule only {modelSched:N0} lb, plan list {planTotal:N0} lb ({(planTotal > 0 ? ((modelPlaced + modelSched) / planTotal - 1) * 100 : 0):+0.0;-0.0}%).");
            _m.Notes.AddRange(notes.Select(n => "  " + n));
        }
        if (modelSched > 0)
            Warn($"Deck reinforcing: {sched.Count} plan bar-list rows ({string.Join(", ", sched.Select(s => s.Mark))}) are in the schedule only ({modelSched:N0} lb) - not placed in 3D.");
    }
}
