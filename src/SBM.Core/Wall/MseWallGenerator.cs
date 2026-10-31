using SBM.Core.Alignments;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Wall;

/// <summary>Kind of an MSE wall element (<see cref="WallElement"/>).</summary>
public enum WallElementRole { Facing, FacingPanel, LevelingPad, Coping, ReinforcedZone, Reinforcement, PileSleeve }

/// <summary>One element of an MSE wall: its solids (one closed loft each), stations and, for reinforcements / zones, the reinforcement length.</summary>
public sealed class WallElement
{
    public WallElementRole Role { get; init; }
    /// <summary>1-based number within the role.</summary>
    public int Index { get; init; }
    /// <summary>Joint panel (facing, facing panels, coping); 0 = not split at the joints (leveling pad, zone, reinforcement).</summary>
    public int Panel { get; init; }
    public string Name { get; init; } = "";
    public double FromStation { get; init; }
    public double ToStation { get; init; }
    public List<Mesh> Solids { get; } = new();
    /// <summary>Reinforcement length (zones and reinforcements); NaN otherwise.</summary>
    public double Length { get; init; } = double.NaN;
    /// <summary>Reinforcement level above the leveling pad top (reinforcements); NaN otherwise.</summary>
    public double Level { get; init; } = double.NaN;
    public Assumption? Assumption { get; set; }
    public double Volume => Solids.Sum(m => Math.Abs(m.Volume()));
}

public static partial class RetainingWallGenerator
{
    /// <summary>
    /// MSE wall (contract-plans envelope, user answer 7a.4). Section coordinates as the cantilever wall: u from the control line (front face of the facing at
    /// the top of wall) toward the back, z = elevation. Front face at height z: u = -batter (top - z); back face = front + facing thickness.
    /// </summary>
    private static RetainingWallModel GenerateMse(RetainingWallDefinition d, Alignment a, bool buildReinforcement)
    {
        var m = new RetainingWallModel { Definition = d };
        var e = d.Mse!;
        string pages = e.Source.Length > 0 ? e.Source : d.Source;
        void Warn(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Wall {d.Name}: {s}"));
        void Info(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Wall {d.Name}: {s}"));
        void Flag(string item, string why, string then) => Warn(UnresolvedFlag.Text(pages, item, why, then));
        foreach (var u in d.Unresolved) Warn(u);

        var missing = new List<string>();
        if (d.FaceSide == null) missing.Add("face side");
        if (d.TopOfWall.Count == 0) missing.Add("top of wall elevations");
        if (d.FootingTopSteps is not { Count: > 0 } && d.FootingTopProfile is not { Count: > 0 }) missing.Add("top of leveling pad elevations");
        if (missing.Count > 0) { Flag($"MSE wall {d.Name}", $"{string.Join(", ", missing)} not given", "wall not built."); return m; }
        double s0 = Math.Min(d.BeginStation, d.EndStation), s1 = Math.Max(d.BeginStation, d.EndStation);
        if (!(s1 - s0 > 1e-3)) { Warn("begin and end station are equal - not built."); return m; }

        double face = d.FaceSide == WallFaceSide.Right ? 1 : -1;
        double Top(double s) => Interp(d.TopOfWall, s);
        bool steps = d.FootingTopSteps is { Count: > 0 };
        var stepRows = steps ? d.FootingTopSteps!.OrderBy(x => x.Station).ToList() : new();
        // top of the leveling pad; on a step station: the step ahead, except at the end of a segment ending there (the step behind)
        double Pad(double s, bool atSegmentEnd = false)
        {
            if (!steps) return Interp(d.FootingTopProfile!, s);
            var row = stepRows[0];
            foreach (var r in stepRows) if (r.Station < s - 1e-9 || (!atSegmentEnd && Math.Abs(r.Station - s) <= 1e-9)) row = r;
            return row.Elevation;
        }
        Vec3 W(double s, double u, double z) => a.PointAt(s, d.Offset - face * u).WithZ(z);
        double sp = d.SampleSpacing > 0.05 ? d.SampleSpacing : 2.0;
        var towStations = d.TopOfWall.Select(r => r.Station).Concat(d.FootingTopProfile?.Select(r => r.Station) ?? Enumerable.Empty<double>()).ToList();
        List<double> Samples(double a0, double a1)
        {
            int n = Math.Max(1, (int)Math.Ceiling((a1 - a0) / sp));
            return Enumerable.Range(0, n + 1).Select(i => a0 + (a1 - a0) * i / n).Concat(towStations.Where(x => x > a0 + 1e-6 && x < a1 - 1e-6))
                .Distinct().OrderBy(x => x).ToList();
        }
        Mesh Loft(IReadOnlyList<Vec2> sec, List<Vec3[]> rings) { var mesh = new Mesh(); MeshBuilder.AddLoft(mesh, sec, rings, false); return mesh; }

        var joints = d.JointStations.Where(j => j > s0 + 1e-6 && j < s1 - 1e-6).OrderBy(x => x).ToList();
        var bounds = new List<double> { s0 }; bounds.AddRange(joints); bounds.Add(s1);
        for (int p = 0; p + 1 < bounds.Count; p++) m.Panels.Add(new WallPanel { Index = p + 1, FromStation = bounds[p], ToStation = bounds[p + 1] });
        var stepBreaks = steps ? stepRows.Select(r => r.Station).Where(x => x > s0 + 1e-6 && x < s1 - 1e-6).ToList() : new();
        List<(double a0, double a1)> Segments(IEnumerable<double> cuts)
        {
            var b = Dedupe(cuts, s0, s1); b.Insert(0, s0); b.Add(s1);
            return b.Zip(b.Skip(1)).ToList();
        }
        int PanelOf(double s) => m.Panels.Last(p => p.FromStation <= s + 1e-9).Index;
        foreach (var s in new[] { s0, s1 })
            if (Top(s) - Pad(s, s == s1) < 1e-3) { Warn($"top of wall is not above the top of the leveling pad at station {s:0.###} - not built."); m.Panels.Clear(); return m; }

        // ---- facing ----
        bool facingOk = !double.IsNaN(e.FacingThickness) && !double.IsNaN(e.FacingBatter) && e.FacingThickness > 0 && e.FacingBatter >= 0;
        if (!facingOk) Flag($"MSE wall {d.Name} facing", "facing thickness / batter not given (or thickness <= 0)", "facing and reinforced zone not built.");
        double UFront(double s, double z) => -e.FacingBatter * (Top(s) - z);
        double UBack(double s, double z) => UFront(s, z) + e.FacingThickness;
        bool grid = e.PanelWidth > 0 && e.PanelHeight > 0;
        if (facingOk)
        {
            var segs = Segments(joints.Concat(stepBreaks).Concat(grid ? GridLines(s0, s1, e.PanelWidth) : Enumerable.Empty<double>()));
            var facings = m.Panels.ToDictionary(p => p.Index, p => new WallElement
            {
                Role = WallElementRole.Facing, Index = p.Index, Panel = p.Index, Name = $"{d.Name} facing {p.Index}", FromStation = p.FromStation, ToStation = p.ToStation,
            });
            int panelNo = 0;
            foreach (var (a0, a1) in segs)
            {
                var st = Samples(a0, a1);
                double zf = Pad(0.5 * (a0 + a1));
                if (!grid)
                {
                    var rings = st.Select((s, i) => new[] { new Vec2(UFront(s, zf), zf), new Vec2(UBack(s, zf), zf), new Vec2(e.FacingThickness, Top(s)), new Vec2(0, Top(s)) }
                        .Select(q => W(s, q.X, q.Y)).ToArray()).ToList();
                    facings[PanelOf(a0)].Solids.Add(Loft(new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) }, rings));
                    continue;
                }
                double maxTop = st.Max(Top);
                for (int c = 0; zf + c * e.PanelHeight < maxTop - 1e-3; c++)
                {
                    double zb = zf + c * e.PanelHeight, zt = zb + e.PanelHeight;
                    var stc = st.Concat(Crossings(st, s => Top(s) - zb)).Concat(Crossings(st, s => Top(s) - zt)).Distinct().OrderBy(x => x).ToList();
                    foreach (var run in Runs(stc, s => Top(s) - zb))
                    {
                        // a course that ends where the top of wall meets its bottom keeps a 0.1 mm end face (one closed loft)
                        double Ztop(double s) => Math.Max(zb + 1e-4, Math.Min(zt, Top(s)));
                        var rings = run.Select(s => new[] { new Vec2(UFront(s, zb), zb), new Vec2(UBack(s, zb), zb), new Vec2(UBack(s, Ztop(s)), Ztop(s)), new Vec2(UFront(s, Ztop(s)), Ztop(s)) }
                            .Select(q => W(s, q.X, q.Y)).ToArray()).ToList();
                        var el = new WallElement
                        {
                            Role = WallElementRole.FacingPanel, Index = ++panelNo, Panel = PanelOf(a0), Name = $"{d.Name} facing panel {panelNo}",
                            FromStation = run[0], ToStation = run[^1],
                        };
                        el.Solids.Add(Loft(new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) }, rings));
                        m.Elements.Add(el);
                    }
                }
            }
            m.Elements.InsertRange(0, facings.Values);
            if (grid) Info($"facing = rectangular panel grid {e.PanelWidth:0.###} x {e.PanelHeight:0.###} m from the begin station and the leveling pad top; joint widths and the supplier's panel pattern are not modeled.");
            else Info("facing = envelope per joint panel (panel size not given).");
            m.FaceArea = 0;
            foreach (var (a0, a1) in Segments(stepBreaks))
            {
                var st = Samples(a0, a1);
                for (int i = 1; i < st.Count; i++)
                {
                    double h0 = Top(st[i - 1]) - Pad(st[i - 1]), h1 = Top(st[i]) - Pad(st[i], i == st.Count - 1);
                    m.FaceArea += (st[i] - st[i - 1]) * 0.5 * (h0 + h1) * Math.Sqrt(1 + e.FacingBatter * e.FacingBatter);
                }
            }
        }

        // ---- leveling pad ----
        bool padOk = !double.IsNaN(e.LevelingPadWidth) && !double.IsNaN(e.LevelingPadThickness) && !double.IsNaN(e.LevelingPadFrontProjection)
            && e.LevelingPadWidth > 0 && e.LevelingPadThickness > 0;
        if (!padOk) Flag($"MSE wall {d.Name} leveling pad", "leveling pad width / thickness / front projection not given", "leveling pad not built.");
        else if (!facingOk) Flag($"MSE wall {d.Name} leveling pad", "its position is taken from the facing front face, which is not given", "leveling pad not built.");
        else
        {
            int k = 0;
            foreach (var (a0, a1) in Segments(stepBreaks))
            {
                var st = Samples(a0, a1);
                double zf = Pad(0.5 * (a0 + a1));
                var rings = st.Select(s =>
                {
                    double u0 = UFront(s, zf) - e.LevelingPadFrontProjection, u1 = u0 + e.LevelingPadWidth, zb = zf - e.LevelingPadThickness;
                    return new[] { new Vec2(u0, zb), new Vec2(u1, zb), new Vec2(u1, zf), new Vec2(u0, zf) }.Select(q => W(s, q.X, q.Y)).ToArray();
                }).ToList();
                var el = new WallElement { Role = WallElementRole.LevelingPad, Index = ++k, Name = $"{d.Name} leveling pad {k}", FromStation = a0, ToStation = a1 };
                el.Solids.Add(Loft(new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) }, rings));
                m.Elements.Add(el);
            }
        }

        // ---- coping ----
        if (e.Coping is { } cp)
        {
            if (cp.Count < 3) Warn("coping section has fewer than 3 points - not built.");
            else
                foreach (var p in m.Panels)
                {
                    var st = Samples(p.FromStation, p.ToStation);
                    var sec = cp.Select(q => new Vec2(q.U, q.Z)).ToList();
                    var rings = st.Select(s => cp.Select(q => W(s, q.U, Top(s) + q.Z)).ToArray()).ToList();
                    var el = new WallElement { Role = WallElementRole.Coping, Index = p.Index, Panel = p.Index, Name = $"{d.Name} coping {p.Index}", FromStation = p.FromStation, ToStation = p.ToStation };
                    el.Solids.Add(Loft(sec, rings));
                    m.Elements.Add(el);
                }
        }

        // ---- reinforced zone ----
        double LengthAt(double s)
        {
            foreach (var r in e.LengthsByStation)
                if (s >= Math.Min(r.FromStation, r.ToStation) - 1e-9 && s <= Math.Max(r.FromStation, r.ToStation) + 1e-9) return r.Length;
            double h = Top(s) - Pad(s);
            foreach (var r in e.LengthsByHeight.OrderBy(r => r.UpToHeight))
                if (h <= r.UpToHeight + 1e-9) return r.Length;
            return double.NaN;
        }
        var zoneSegs = new List<(double a0, double a1, double len)>();
        if (facingOk)
        {
            if (e.LengthsByStation.Count == 0 && e.LengthsByHeight.Count == 0)
                Flag($"MSE wall {d.Name} reinforced zone", "reinforcement lengths not given (no station or height table; never derived from a height ratio)", "zone not built.");
            else
            {
                if (e.LengthFrom == null)
                    Flag($"MSE wall {d.Name} reinforced zone", "the plans do not say where the reinforcement length is measured from", "zone built from the back of the facing.");
                var cuts = stepBreaks.Concat(e.LengthsByStation.SelectMany(r => new[] { r.FromStation, r.ToStation }).Where(x => x > s0 + 1e-6 && x < s1 - 1e-6)).ToList();
                // height-table changes: scan at 0.25 m, bisect each change
                for (double x = s0; x < s1 - 1e-9; x += 0.25)
                {
                    double x1 = Math.Min(s1, x + 0.25), l0 = LengthAt(x), l1 = LengthAt(x1);
                    if (Same(l0, l1)) continue;
                    double lo = x, hi = x1;
                    for (int it = 0; it < 50; it++) { double mid = 0.5 * (lo + hi); if (Same(LengthAt(mid), l0)) lo = mid; else hi = mid; }
                    cuts.Add(0.5 * (lo + hi));
                }
                var open = new List<string>();
                foreach (var (a0, a1) in Segments(cuts))
                {
                    double len = LengthAt(0.5 * (a0 + a1));
                    if (double.IsNaN(len) || !(len > 0)) { open.Add($"{a0:0.###} - {a1:0.###}"); continue; }
                    zoneSegs.Add((a0, a1, len));
                }
                if (open.Count > 0) Flag($"MSE wall {d.Name} reinforced zone", $"no reinforcement length for stations {string.Join(", ", open)} (no table row covers them)", "zone not built there.");
                int k = 0;
                foreach (var (a0, a1, len) in zoneSegs)
                {
                    var st = Samples(a0, a1);
                    double zf = Pad(0.5 * (a0 + a1));
                    var rings = st.Select(s =>
                    {
                        double t = Top(s), b0 = ZoneBack(s, zf, len), b1 = ZoneBack(s, t, len);
                        return new[] { new Vec2(UBack(s, zf), zf), new Vec2(b0, zf), new Vec2(b1, t), new Vec2(UBack(s, t), t) }.Select(q => W(s, q.X, q.Y)).ToArray();
                    }).ToList();
                    var el = new WallElement { Role = WallElementRole.ReinforcedZone, Index = ++k, Name = $"{d.Name} reinforced zone {k}", FromStation = a0, ToStation = a1, Length = len };
                    el.Solids.Add(Loft(new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) }, rings));
                    m.Elements.Add(el);
                }
            }
        }
        double ZoneBack(double s, double z, double len) => (e.LengthFrom == MseLengthOrigin.FrontOfFacing ? UFront(s, z) : UBack(s, z)) + len;

        // ---- individual reinforcements (supplier drawings, LOD 400) ----
        if (e.Reinforcement is { } rf)
        {
            var rm = new List<string>();
            if (rf.Kind == null) rm.Add("kind (strip / grid)");
            if (rf.Levels.Count == 0) rm.Add("levels");
            if (!(rf.Thickness > 0)) rm.Add("thickness");
            if (rf.Kind == MseReinforcementKind.Strip)
            {
                if (!(rf.Spacing > 0)) rm.Add("spacing");
                if (double.IsNaN(rf.FirstFromBegin)) rm.Add("first strip from the begin station");
                if (!(rf.Width > 0)) rm.Add("width");
            }
            string rpages = rf.Source.Length > 0 ? rf.Source : pages;
            if (rm.Count > 0) Warn(UnresolvedFlag.Text(rpages, $"MSE wall {d.Name} reinforcement", $"{string.Join(", ", rm)} not given", "reinforcements not built (zone envelope only)."));
            else if (!buildReinforcement) Info("individual reinforcements not built below LOD 400 (reinforced zone envelope only).");
            else if (zoneSegs.Count == 0) Warn("reinforcements not built: no reinforced zone (lengths / facing missing).");
            else
            {
                int k = 0;
                string kind = rf.Kind == MseReinforcementKind.Strip ? "strip" : "grid";
                double th = rf.Thickness;
                foreach (var lv in rf.Levels.OrderBy(x => x))
                {
                    if (rf.Kind == MseReinforcementKind.Strip)
                    {
                        for (double s = s0 + rf.FirstFromBegin; s <= s1 + 1e-9; s += rf.Spacing)
                        {
                            var seg = zoneSegs.FirstOrDefault(z => s >= z.a0 - 1e-9 && s <= z.a1 + 1e-9);
                            if (seg.len == 0) continue;
                            double z = Pad(s) + lv;
                            if (z + 0.5 * th >= Top(s)) continue;
                            double u0 = UBack(s, z), u1 = ZoneBack(s, z, seg.len);
                            var tan = Vec2.FromAngle(a.AngleAt(s)).WithZ(0) * (0.5 * rf.Width);
                            var sec = new[] { new Vec2(-1, -1), new Vec2(1, -1), new Vec2(1, 1), new Vec2(-1, 1) };
                            var rings = new[] { u0, u1 }.Select(u => sec.Select(q => W(s, u, z + q.Y * 0.5 * th) + tan * q.X).ToArray()).ToList();
                            var el = new WallElement
                            {
                                Role = WallElementRole.Reinforcement, Index = ++k, Name = $"{d.Name} {kind} {k}", FromStation = s, ToStation = s, Length = u1 - u0, Level = lv,
                            };
                            el.Solids.Add(Loft(sec, rings));
                            m.Elements.Add(el);
                        }
                    }
                    else
                        foreach (var (a0, a1, len) in zoneSegs)
                        {
                            double zf = Pad(0.5 * (a0 + a1)), z = zf + lv;
                            var st = Samples(a0, a1);
                            st = st.Concat(Crossings(st, s => Top(s) - z - 0.5 * th)).Distinct().OrderBy(x => x).ToList();
                            foreach (var run in Runs(st, s => Top(s) - z - 0.5 * th))
                            {
                                var rings = run.Select(s => new[] { new Vec2(UBack(s, z), z - 0.5 * th), new Vec2(ZoneBack(s, z, len), z - 0.5 * th),
                                    new Vec2(ZoneBack(s, z, len), z + 0.5 * th), new Vec2(UBack(s, z), z + 0.5 * th) }.Select(q => W(s, q.X, q.Y)).ToArray()).ToList();
                                var el = new WallElement
                                {
                                    Role = WallElementRole.Reinforcement, Index = ++k, Name = $"{d.Name} {kind} {k}", FromStation = run[0], ToStation = run[^1], Length = len, Level = lv,
                                };
                                el.Solids.Add(Loft(new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) }, rings));
                                m.Elements.Add(el);
                            }
                        }
                }
                Info($"{k} {kind}s from the supplier drawings ({rpages}).");
            }
        }

        // plan lines: facing front at the top of wall, zone back at the pad top
        if (facingOk)
        {
            m.PlanLines.Add(Samples(s0, s1).Select(s => W(s, 0, Top(s))).ToArray());
            foreach (var (a0, a1, len) in zoneSegs) m.PlanLines.Add(Samples(a0, a1).Select(s => W(s, ZoneBack(s, Pad(s), len), Pad(s))).ToArray());
        }
        const double cy = 0.764554858;
        double V(WallElementRole r) => m.Elements.Where(x => x.Role == r || (r == WallElementRole.Facing && x.Role == WallElementRole.FacingPanel)).Sum(x => x.Volume);
        Info($"MSE: {m.Panels.Count} joint panels, facing {V(WallElementRole.Facing) / cy:0.0} CY, leveling pad {V(WallElementRole.LevelingPad) / cy:0.0} CY, coping {V(WallElementRole.Coping) / cy:0.0} CY, " +
             $"reinforced zone {V(WallElementRole.ReinforcedZone) / cy:0.0} CY, exposed face {m.FaceArea / 0.09290304:0} SF.");
        return m;
    }

    private static bool Same(double x, double y) => (double.IsNaN(x) && double.IsNaN(y)) || Math.Abs(x - y) < 1e-9;

    private static IEnumerable<double> GridLines(double s0, double s1, double w)
    {
        for (double s = s0 + w; s < s1 - 1e-6; s += w) yield return s;
    }

    private static List<double> Dedupe(IEnumerable<double> cuts, double s0, double s1)
    {
        var r = new List<double>();
        foreach (var c in cuts.Where(x => x > s0 + 1e-4 && x < s1 - 1e-4).OrderBy(x => x))
            if (r.Count == 0 || c - r[^1] > 1e-4) r.Add(c);
        return r;
    }

    /// <summary>Stations between consecutive samples where <paramref name="g"/> changes sign (bisection).</summary>
    private static IEnumerable<double> Crossings(List<double> st, Func<double, double> g)
    {
        for (int i = 0; i + 1 < st.Count; i++)
        {
            double lo = st[i], hi = st[i + 1], glo = g(lo), ghi = g(hi);
            if (glo == 0 || ghi == 0 || Math.Sign(glo) == Math.Sign(ghi)) continue;
            for (int it = 0; it < 60; it++)
            {
                double mid = 0.5 * (lo + hi), gm = g(mid);
                if (Math.Sign(gm) == Math.Sign(glo)) { lo = mid; glo = gm; } else hi = mid;
            }
            yield return 0.5 * (lo + hi);
        }
    }

    /// <summary>Maximal runs of the (sorted) stations where g &gt; 0, each with its bounding zero crossings.</summary>
    private static List<List<double>> Runs(List<double> st, Func<double, double> g)
    {
        const double eps = 1e-7;
        var runs = new List<List<double>>();
        List<double>? cur = null;
        for (int i = 0; i < st.Count; i++)
        {
            double v = g(st[i]);
            if (v > eps)
            {
                if (cur == null) { cur = new(); if (i > 0 && Math.Abs(g(st[i - 1])) <= 1e-6) cur.Add(st[i - 1]); }
                cur.Add(st[i]);
            }
            else if (cur != null)
            {
                if (Math.Abs(v) <= 1e-6) cur.Add(st[i]);
                runs.Add(cur); cur = null;
            }
        }
        if (cur != null) runs.Add(cur);
        return runs.Where(r => r.Count >= 2 && r[^1] - r[0] > 1e-4).ToList();
    }
}
