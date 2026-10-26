using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Lateral (plan) bracing in selected bays at the top or bottom flange. Panel points are the cross-frame locations.
/// At each panel point a horizontal gusset is bolted to the flange (on the bay side of the web), coped around the
/// connection stiffener; single angles are welded to the gusset (heel and toe) with the connected leg flat on it.
/// Either one layout (<see cref="LateralBracingDefinition.Bays"/> / Spans / Flange / Pattern) or ZONES
/// (<see cref="LateralBracingDefinition.Zones"/>): per span, bay, flange and panel range their own pattern, member, gusset, bolts and material.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private void BuildLateralBracing()
    {
        var lb = _def.LateralBracing;
        if (!lb.Enabled || _f.Girders.Count < 2) return;
        if (lb.Zones is { Count: > 0 }) { BuildLateralZones(lb); return; }
        int nSpan = _f.Girders[0].Spans.Count;
        var settings = new LateralSettings(lb.Flange, lb.Pattern, false, lb.Angle, lb.GussetThickness, lb.BoltsPerConnection, "ASTM A709 Gr. 36", null, "", null);
        foreach (int bay1 in lb.Bays.Distinct())
        {
            int bay = bay1 - 1;
            if (bay < 0 || bay >= _f.Girders.Count - 1) { Warn($"Lateral bracing: bay {bay1} does not exist."); continue; }
            for (int span = 0; span < nSpan; span++)
            {
                if (lb.Spans.Count > 0 && !lb.Spans.Contains(span + 1)) continue;
                var pts = LateralPanelPoints(bay, span);
                if (pts.Count < 2) continue;
                string mark = $"LB-S{span + 1}-B{bay1}";
                _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.LateralBracing,
                    Description = $"{lb.Flange} flange lateral bracing, span {span + 1}, G{bay + 1}–G{bay + 2}" });
                BuildLateralBay(bay, new() { new LateralRun(mark, pts, settings) });
            }
        }
    }

    /// <summary>Cross frame lines of a bay in a span (incl. the support frames at both ends), ordered along the bay's left girder.</summary>
    private List<CrossFrameResult> LateralPanelPoints(int bay, int span) =>
        _f.CrossFrames.Where(c => c.Bay == bay && (c.Span == span || (c.AtSupport && c.SupportIndex == span + 1)))
            .GroupBy(c => Math.Round(c.LeftGlobalDistance, 3)).Select(gr => gr.First())
            .OrderBy(c => c.LeftGlobalDistance).ToList();

    private sealed record LateralSettings(LateralFlange Flange, LateralPattern Pattern, bool StartRight, string Angle, double GussetThickness, int Bolts,
        string Material, string? GussetMaterial, string Source, Assumption? Flag);

    /// <summary>Consecutive panels of one bay / span / flange with the same zone; Frames = its panel points (panels + 1).</summary>
    private sealed record LateralRun(string Mark, List<CrossFrameResult> Frames, LateralSettings S);

    private void BuildLateralZones(LateralBracingDefinition lb)
    {
        var zones = lb.Zones!;
        int nSpan = _f.Girders[0].Spans.Count, nBay = _f.Girders.Count - 1;
        var used = new bool[zones.Count];
        var set = zones.Select(z =>
        {
            Assumption? flag = null;
            if (!string.IsNullOrWhiteSpace(z.Unresolved))
            {
                Warn($"Lateral bracing zone {ZoneLabel(z, zones)}: {z.Unresolved}");
                flag = Assumption.UnresolvedDimension(z.Unresolved!, "zone modeled as entered");
            }
            return new LateralSettings(z.Flange, z.Pattern, z.DiagonalStart == LateralDiagonalStart.RightGirder, z.Angle ?? lb.Angle,
                z.GussetThickness ?? lb.GussetThickness, z.BoltsPerConnection ?? lb.BoltsPerConnection, z.Material ?? "ASTM A709 Gr. 36", z.GussetMaterial, z.Source, flag);
        }).ToList();
        foreach (var z in zones)
        {
            foreach (int b in z.Bays.Where(b => b < 1 || b > nBay).Distinct())
                Warn($"Lateral bracing zone {ZoneLabel(z, zones)}: bay {b} does not exist (bays 1-{nBay}).");
            foreach (int s in z.Spans.Where(s => s < 1 || s > nSpan).Distinct())
                Warn($"Lateral bracing zone {ZoneLabel(z, zones)}: span {s} does not exist (spans 1-{nSpan}).");
        }

        for (int bay = 0; bay < nBay; bay++)
            for (int span = 0; span < nSpan; span++)
            {
                var pts = LateralPanelPoints(bay, span);
                if (pts.Count < 2) continue;
                double d0 = _f.Girders[bay].DistanceAt(span, 0.0);
                foreach (var flange in new[] { LateralFlange.Bottom, LateralFlange.Top })
                {
                    // zone of each panel (the last matching zone wins)
                    var owner = new int[pts.Count - 1];
                    for (int k = 0; k < owner.Length; k++)
                    {
                        owner[k] = -1;
                        for (int zi = 0; zi < zones.Count; zi++)
                        {
                            var z = zones[zi];
                            if (z.Flange != flange) continue;
                            if (z.Spans.Count > 0 && !z.Spans.Contains(span + 1)) continue;
                            if (z.Bays.Count > 0 && !z.Bays.Contains(bay + 1)) continue;
                            if (!PanelInZone(z, k, pts, d0)) continue;
                            if (owner[k] >= 0)
                                Warn($"Lateral bracing: zones {ZoneLabel(zones[owner[k]], zones)} and {ZoneLabel(z, zones)} overlap in span {span + 1}, bay {bay + 1}, "
                                    + $"{flange.ToString().ToLowerInvariant()} flange, panel {k + 1}-{k + 2}; the later zone is used.");
                            owner[k] = zi;
                        }
                    }
                    var runs = new List<(int zone, int k0, int k1)>();
                    for (int k = 0; k < owner.Length; k++)
                    {
                        if (owner[k] < 0) continue;
                        if (runs.Count > 0 && runs[^1].zone == owner[k] && runs[^1].k1 == k) runs[^1] = (owner[k], runs[^1].k0, k + 1);
                        else runs.Add((owner[k], k, k + 1));
                    }
                    if (runs.Count == 0) continue;
                    string baseMark = $"LB-S{span + 1}-B{bay + 1}" + (flange == LateralFlange.Top ? "-T" : "");
                    var list = new List<LateralRun>();
                    for (int r = 0; r < runs.Count; r++)
                    {
                        var (zi, k0, k1) = runs[r];
                        used[zi] = true;
                        var z = zones[zi];
                        string mark = runs.Count > 1 ? $"{baseMark}-R{r + 1}" : baseMark;
                        string label = z.Name.Length > 0 ? $" '{z.Name}'" : "";
                        _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.LateralBracing,
                            Description = $"{flange} flange lateral bracing{label}, span {span + 1}, G{bay + 1}–G{bay + 2}, panel points {k0 + 1}-{k1 + 1}, {z.Pattern}"
                                + (z.Source.Length > 0 ? $" ({z.Source})" : "") });
                        list.Add(new LateralRun(mark, pts.GetRange(k0, k1 - k0 + 1), set[zi]));
                    }
                    BuildLateralBay(bay, list);
                }
            }
        for (int zi = 0; zi < zones.Count; zi++)
            if (!used[zi]) Warn($"Lateral bracing zone {ZoneLabel(zones[zi], zones)}: no panel of the bridge falls in it (spans, bays, panel / distance range) - nothing built.");
    }

    private static string ZoneLabel(LateralBracingZone z, List<LateralBracingZone> all) => z.Name.Length > 0 ? $"'{z.Name}'" : $"#{all.IndexOf(z) + 1}";

    /// <summary>Panel k (between points k and k + 1) lies in the zone's range.</summary>
    private static bool PanelInZone(LateralBracingZone z, int k, List<CrossFrameResult> pts, double d0)
    {
        int n = pts.Count;
        int Idx(int p) => p > 0 ? p - 1 : n + p;   // 1-based, negative = from the last point
        if (z.FromPanel is int fp && k < Idx(fp)) return false;
        if (z.ToPanel is int tp && k + 1 > Idx(tp)) return false;
        const double tol = 0.001;
        double a = pts[k].LeftGlobalDistance - d0, b = pts[k + 1].LeftGlobalDistance - d0;
        if (z.FromDistance is double fd && a < fd - tol) return false;
        if (z.ToDistance is double td && b > td + tol) return false;
        return true;
    }

    private sealed record PanelPoint(int Girder, double S, Vec2 Direction, int Side);

    private sealed class LateralGusset
    {
        public required PanelPoint Pp;
        public required LateralRun Run;
        public List<Vec2> Sr = new();
        public List<Vec2> Bolts = new();
    }

    /// <summary>All runs of one bay / span / flange: members per run, gussets shared at the panel points runs have in common (owned by the first run).</summary>
    private void BuildLateralBay(int bay, List<LateralRun> runs)
    {
        bool bottom = runs[0].S.Flange == LateralFlange.Bottom;
        double d = Sec.WebDepth;
        double clr = _def.CrossFrames.MemberClearance, lap = _def.CrossFrames.WeldLap, ws = _def.CrossFrames.MemberWeldSize;
        double e = _def.Bolts.EdgeDistance, p = _def.Bolts.Pitch;
        double headR = _model.Bolt.HeadAcrossFlats / Math.Sqrt(3);

        // Panel point geometry: gusset plane, bolt group (on the bay side of the web, both sides of the stiffener).
        var gusset = new Dictionary<(int, long), LateralGusset>();
        var gussetOrder = new List<LateralGusset>();
        LateralGusset Panel(PanelPoint pp, LateralRun run)
        {
            var key = (pp.Girder, (long)Math.Round(pp.S * 1000));
            if (gusset.TryGetValue(key, out var v)) return v;
            var path = _f.Girders[pp.Girder].Path;
            var (_, t, _) = path.At(pp.S);
            double tw = _web[pp.Girder].Thickness(pp.S);
            var stiff = StiffenerAt(pp.Girder, pp.S);
            double tp = stiff?.Plate.Thickness ?? 0;
            // Stiffener direction in (s, r) coordinates and its normal.
            var dsr = new Vec2(Vec2.Dot(pp.Direction, t), Vec2.Dot(pp.Direction, t.Right));
            double cosS = Math.Max(Math.Abs(dsr.Y), 0.3);
            double off = (tp / 2 + Inch(1.25)) / cosS;
            int n = Math.Max(2, run.S.Bolts);
            int rowsEach = Math.Max(1, (n + 3) / 4);
            var bolts = new List<Vec2>();
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < rowsEach; row++)
                    for (int line = 0; line < 2; line++)
                    {
                        double r = pp.Side * (tw / 2 + _def.SpliceDetail.FirstLineFromWeb + line * _def.SpliceDetail.FlangeGage);
                        double s = pp.S + side * (off + row * p) + r * dsr.X / Math.Max(Math.Abs(dsr.Y), 1e-6) * Math.Sign(dsr.Y);
                        bolts.Add(new Vec2(s, r));
                    }
            v = new LateralGusset { Pp = pp, Run = run, Bolts = bolts };
            gusset[key] = v;
            gussetOrder.Add(v);
            return v;
        }

        var welds = new List<(LateralRun run, Mesh mesh, double len)>();
        foreach (var run in runs)
        {
            var S = run.S;
            var frames = run.Frames;
            var angle = SteelCatalog.Angle(S.Angle);
            string mark = run.Mark;
            var weld = new Mesh();
            double weldLen = 0;
            int mn = 1;

            // Members between panel points (left girder = bay, right girder = bay + 1). Split = cut clear of the crossing diagonal (X pattern).
            var members = new List<(PanelPoint a, PanelPoint b, bool split)>();
            for (int k = 0; k + 1 < frames.Count; k++)
            {
                var L0 = new PanelPoint(bay, frames[k].LeftGlobalDistance, frames[k].Direction, +1);
                var R0 = new PanelPoint(bay + 1, frames[k].RightGlobalDistance, frames[k].Direction, -1);
                var L1 = new PanelPoint(bay, frames[k + 1].LeftGlobalDistance, frames[k + 1].Direction, +1);
                var R1 = new PanelPoint(bay + 1, frames[k + 1].RightGlobalDistance, frames[k + 1].Direction, -1);
                bool fromRight = S.StartRight ^ (S.Pattern == LateralPattern.Warren && k % 2 == 1);
                members.Add(fromRight ? (R0, L1, false) : (L0, R1, false));
                if (S.Pattern == LateralPattern.X) members.Add(fromRight ? (L0, R1, true) : (R0, L1, true));
            }

            for (int mi = 0; mi < members.Count; mi++)
            {
                var (pa, pb, split) = members[mi];
                var ga = _f.Girders[pa.Girder].Path;
                var gb = _f.Girders[pb.Girder].Path;
                var gA = Panel(pa, run);
                var gB = Panel(pb, run);
                double tg = gA.Run.S.GussetThickness;
                if (Math.Abs(gB.Run.S.GussetThickness - tg) > 1e-6)
                    Warn($"{mark}: the gussets at the two ends of a lateral member differ in thickness ({In(tg)} / {In(gB.Run.S.GussetThickness)}); the member lies on the first one.");
                double za = bottom ? -d + tg : -tg, zb = za;
                var wp1 = GirderPoint(ga, pa.S, 0, za);
                var wp2 = GirderPoint(gb, pb.S, 0, zb);
                var a = (wp2 - wp1).Normalized();
                double len = (wp2 - wp1).Length;
                var q = Vec3.Cross(Up, a).Normalized();
                var nl = Vec3.Cross(a, q).Normalized();
                if (nl.Z < 0) nl = -nl;
                int o = bottom ? 1 : -1;
                double ybar = angle.CentroidFromHeel, heel = -ybar, mlo = heel, mhi = heel + angle.LegA;

                double TrimFrom(PanelPoint pp, Vec3 start, Vec3 dirA)
                {
                    var bolts = Panel(pp, run).Bolts;
                    var path = _f.Girders[pp.Girder].Path;
                    var (pt, tan, _) = path.At(pp.S);
                    var stiff = StiffenerAt(pp.Girder, pp.S);
                    double tp = stiff?.Plate.Thickness ?? 0;
                    double tw = _web[pp.Girder].Thickness(pp.S);
                    var dir2 = pp.Direction;
                    var far = start + dirA * len;
                    double sideOfPlane = Math.Sign(Vec2.Cross(dir2, far.XY - pt.XY));
                    for (double tt = 0; tt < len / 2; tt += Inch(0.25))
                    {
                        bool ok = true;
                        foreach (double m in new[] { mlo, mhi })
                        {
                            var c = (start + dirA * tt + q * m).XY;
                            var rel = c - pt.XY;
                            if (sideOfPlane * Vec2.Cross(dir2, rel) < tp / 2 + clr) { ok = false; break; }
                            if (pp.Side * Vec2.Dot(rel, tan.Right) < tw / 2 + clr) { ok = false; break; }
                        }
                        if (ok)
                            foreach (var bsr in bolts)
                            {
                                var bw = GirderPoint(path, bsr.X, bsr.Y, 0).XY;
                                double mb = Vec2.Dot(bw - start.XY, q.XY);
                                if (mb < mlo - headR - clr || mb > mhi + headR + clr) continue;
                                if (Vec2.Dot(bw - start.XY, dirA.XY) > tt - headR - clr) { ok = false; break; }
                            }
                        if (ok) return tt;
                    }
                    return len / 2;
                }

                double t0 = TrimFrom(pa, wp1, a);
                double t1 = len - TrimFrom(pb, wp2, -a);
                if (t1 - t0 < 2 * lap + Inch(1)) { Warn($"{mark}: lateral member too short; check bay geometry."); continue; }

                // Pieces along the member: the whole member, or (X pattern, second diagonal) two pieces cut clear of the continuous one.
                var pieces = new List<(double s0, double s1, bool weldStart, bool weldEnd)> { (t0, t1, true, true) };
                Assumption? flag = S.Flag;
                if (split && mi > 0)
                {
                    var (ca, cb, _) = members[mi - 1];
                    var c1 = GirderPoint(_f.Girders[ca.Girder].Path, ca.S, 0, za);
                    var c2 = GirderPoint(_f.Girders[cb.Girder].Path, cb.S, 0, za);
                    var a1 = (c2 - c1).Normalized();
                    var q1 = Vec3.Cross(Up, a1).Normalized();
                    double den = Vec3.Dot(q1, a);
                    if (Math.Abs(den) > 1e-6)
                    {
                        double lo = double.MaxValue, hi = double.MinValue;
                        foreach (double m in new[] { mlo, mhi })
                            foreach (double lim in new[] { mlo - clr, mhi + clr })
                            {
                                double tt = (lim - Vec3.Dot(q1, wp1 + q * m - c1)) / den;
                                lo = Math.Min(lo, tt); hi = Math.Max(hi, tt);
                            }
                        pieces = new() { (t0, lo, true, false), (hi, t1, false, true) };
                        pieces.RemoveAll(x => x.s1 - x.s0 < lap + Inch(1));
                        flag = Assumption.UnresolvedDimension(UnresolvedFlag.Text(S.Source, "X lateral bracing crossing",
                            "the connection of the two diagonals where they cross (plate, bolts) is not an input",
                            "The second diagonal is cut clear of the first; the crossing connection is not modeled."), "cut at the crossing");
                    }
                }

                foreach (var (s0, s1, weldStart, weldEnd) in pieces)
                {
                    var sec = SteelCatalog.AnglePolygon(angle, heel, 1, 0, o);
                    Vec3[] Ring(double tt) => sec.Select(s => wp1 + a * tt + q * s.X + nl * s.Y).ToArray();
                    var mesh = new Mesh();
                    MeshBuilder.AddLoft(mesh, sec, new[] { Ring(s0), Ring(s1) });
                    _parts.Add(new SteelPart { Mark = $"{mark}-L{mn++}", Category = PartCategory.Angle, Role = MemberRole.Lateral, Assembly = mark,
                        Mesh = mesh, Length = s1 - s0, Description = angle.Name, Material = S.Material, NominalWeightLbPerFt = angle.WeightPerFootLb, Assumption = flag });

                    var ends = new List<(double, double, PanelPoint)>();
                    if (weldStart) ends.Add((s0, s0 + lap, pa));
                    if (weldEnd) ends.Add((s1 - lap, s1, pb));
                    foreach (var (e0, e1, pp) in ends)
                    {
                        var sr = Panel(pp, run).Sr;
                        var path = _f.Girders[pp.Girder].Path;
                        var (pt, tan, _) = path.At(pp.S);
                        double margin = ws + Inch(0.25);
                        foreach (double tt in new[] { e0, e1 })
                            foreach (double m in new[] { mlo - margin, mhi + margin })
                            {
                                var w = (wp1 + a * tt + q * m).XY - pt.XY;
                                sr.Add(new Vec2(pp.S + Vec2.Dot(w, tan), Vec2.Dot(w, tan.Right)));
                            }
                        double toe = heel + angle.LegA;
                        weldLen += weld.AddHeld(wp1 + a * e0 + q * heel, wp1 + a * e1 + q * heel, -q, nl * o, ws, HoldBack(ws), HoldBack(ws));   // short of the angle end / gusset edge
                        weldLen += weld.AddHeld(wp1 + a * e0 + q * toe, wp1 + a * e1 + q * toe, q, nl * o, ws, HoldBack(ws), HoldBack(ws));
                    }
                }
            }
            welds.Add((run, weld, weldLen));
        }

        // Gussets (coped around the stiffener) and bolts through gusset + flange; numbered per owning run.
        var gn = runs.ToDictionary(r => r.Mark, _ => 1);
        foreach (var g in gussetOrder)
        {
            var (pp, sr, bolts, run) = (g.Pp, g.Sr, g.Bolts, g.Run);
            if (sr.Count == 0) continue;
            string mark = run.Mark;
            double tg = run.S.GussetThickness;
            var path = _f.Girders[pp.Girder].Path;
            var (_, tan, _) = path.At(pp.S);
            double tw = _web[pp.Girder].Thickness(pp.S);
            var prof = bottom ? _bot[pp.Girder] : _top[pp.Girder];
            double tf = prof.Thickness(pp.S);
            var pts = sr.Concat(bolts.SelectMany(b => new[] { b + new Vec2(-e, -e), b + new Vec2(e, -e), b + new Vec2(e, e), b + new Vec2(-e, e) })).ToList();
            var hull = MeshBuilder.ConvexHull(pts);
            // Keep off the web and the flange-to-web weld.
            hull = PlateBuilder.ClipHalfPlane(hull, new Vec2(0, pp.Side), tw / 2 + _def.Stiffeners.FlangeToWebWeld + Inch(0.25));
            var stiff = StiffenerAt(pp.Girder, pp.S);
            var pieces = new List<List<Vec2>>();
            if (stiff != null)
            {
                var dsr = new Vec2(Vec2.Dot(pp.Direction, tan), Vec2.Dot(pp.Direction, tan.Right)) * pp.Side;
                var nsr = new Vec2(-dsr.Y, dsr.X);
                var c0 = new Vec2(pp.S, 0);
                double half = stiff.Plate.Thickness / 2 + Inch(0.25);
                double xf = tw / 2 / Math.Max(Math.Abs(dsr.Y), 0.2);
                double tip = xf + stiff.Plate.Width + Inch(0.25);
                pieces.Add(PlateBuilder.ClipHalfPlane(hull, nsr, Vec2.Dot(c0, nsr) + half));
                pieces.Add(PlateBuilder.ClipHalfPlane(hull, -nsr, -(Vec2.Dot(c0, nsr) - half)));
                var mid = PlateBuilder.ClipHalfPlane(hull, -nsr, -(Vec2.Dot(c0, nsr) + half));
                mid = PlateBuilder.ClipHalfPlane(mid, nsr, Vec2.Dot(c0, nsr) - half);
                pieces.Add(PlateBuilder.ClipHalfPlane(mid, dsr, Vec2.Dot(c0, dsr) + tip));
            }
            else pieces.Add(hull);

            var mesh = new Mesh();
            double z0 = bottom ? -d : -tg, z1 = bottom ? -d + tg : 0;
            foreach (var piece in pieces.Where(pc => pc.Count >= 3 && Math.Abs(MeshBuilder.SignedArea(pc)) > 1e-6))
            {
                var hs = bolts.Where(b => PointInConvex(piece, b)).Select(b => new Hole(b, HoleDia)).ToList();
                PlateBuilder.AddPrism(mesh, piece, hs, (s, r, z) => GirderPoint(path, s, r, z), _ => z0, _ => z1);
            }
            double w = hull.Max(x => x.X) - hull.Min(x => x.X), h = hull.Max(x => x.Y) - hull.Min(x => x.Y);
            var part = new SteelPart { Mark = $"{mark}-G{gn[mark]++}", Category = PartCategory.Gusset, Assembly = mark, Mesh = mesh, Length = Math.Max(w, h),
                Description = $"Lateral gusset {Pl(h, tg)} x {In(w)}" + (stiff != null ? ", coped at stiffener" : ""), Assumption = run.S.Flag };
            if (run.S.GussetMaterial != null) part.Material = run.S.GussetMaterial;
            _parts.Add(part);

            double grip = tg + tf;
            double blen = _model.Bolt.LengthForGrip(grip);
            foreach (var b in bolts)
            {
                (bottom ? _holes[pp.Girder].Bottom : _holes[pp.Girder].Top).Add(new Hole(b, HoleDia));
                var origin = GirderPoint(path, b.X, b.Y, bottom ? -d + tg : -tg);
                var t3 = new Vec3(tan.X, tan.Y, 0);
                _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(origin, t3, bottom ? -Up : Up), Grip = grip, Length = blen, Assembly = mark,
                    Diameter = _model.Bolt.Diameter });
            }
        }
        foreach (var (run, weld, weldLen) in welds)
            _parts.Add(new SteelPart { Mark = $"{run.Mark}-W", Category = PartCategory.Weld, Assembly = run.Mark, Mesh = weld, Material = "E70XX",
                Length = weldLen, Description = $"Fillet weld {In(ws)}, lateral angles to gussets{HoldNote(ws)}", Assumption = WeldFlag(ws) });
    }

    private StiffenerResult? StiffenerAt(int girder, double s) =>
        _f.Stiffeners.FirstOrDefault(x => x.Girder == girder && x.Kind != StiffenerKind.Intermediate && Math.Abs(x.GirderDistance - s) < 0.02);

    private static bool PointInConvex(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        double sign = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            double c = Vec2.Cross(poly[(i + 1) % poly.Count] - poly[i], p - poly[i]);
            if (Math.Abs(c) < 1e-12) continue;
            if (sign == 0) sign = Math.Sign(c);
            else if (Math.Sign(c) != sign) return false;
        }
        return true;
    }
}
