using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL tendon profiles (TendonDefinition.Lanes / Profile + SegmentalDefinition.DuctLanes; none = the straight joint-to-joint ducts of v1):
// every joint face is crossed in the tendon's lane (a hole of the bulkhead / match-cast face); lane changes happen INSIDE a segment between two
// P.I.s a printed distance from its faces, so the duct is already in the new lane at the next joint; in the anchoring segment the duct leaves its
// lane for the anchorage (segment tip face for cantilever tendons, the anchor face of the block = blister for continuity tendons). Corners are
// circular arcs of the printed radii (clamped to fit, reported). Built in box coordinates (station, x lateral, y down from the deck top),
// joint stations inserted exactly, mapped with the chord frames. Check rows: lane offset at the joints, angle change at the joints, ducts
// outside the concrete (CONFLICT, never moved).
public sealed partial class SegmentalGenerator
{
    private readonly record struct Ctrl(double S, double X, double Y, double R);
    /// <summary>Lane holes the current tendon must pass (joint station, x, y) - filled by the profile builders for the check rows.</summary>
    private readonly List<(double S, double X, double Y, SegmentalDuctLane L, int Side)> _expect = new();
    /// <summary>Plan conflicts found while building the current tendon's control points (P.I.s + radius that cannot fit before the next joint).</summary>
    private readonly List<string> _conflicts = new();
    private readonly List<(string Arm, double Angle, double Off, string Source)> _anchorExitChecks = new();
    private readonly List<(double Model, double Plan, string Source)> _endFaceChecks = new();
    private readonly List<(int Joint, double Model, double Plan, string Source)> _crossingChecks = new();
    /// <summary>Direction change at a joint face RELATIVE to its lane above which the duct is not straight through its hole.</summary>
    private const double JointKinkLimitDeg = 0.5;

    private void ApplyTendonProfiles()
    {
        var lanes = (_d.DuctLanes ?? new()).GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var hosts = _m.Segments.Select(s => s.Mesh).Concat(_m.Blocks.Where(b => b.Kind != SegmentalBlockKind.ShearKey).Select(b => b.Mesh)).Where(m => !m.IsEmpty).ToList();
        RebarCover? cover = null;
        int built = 0, kept = 0, clamped = 0, outside = 0, kinks = 0;
        double worstJoint = 0, worstAngle = 0;
        var keptWhy = new Dictionary<string, int>();
        var joints = _m.Segments.SelectMany(s => new[] { s.Station0, s.Station1 }).Distinct().OrderBy(s => s).ToList();
        foreach (var t in _d.Tendons.Where(t => t.Profile != null))
        {
            var p = t.Profile!;
            if (p.Unresolved.Length > 0) Warn(p.Unresolved);
            foreach (var r in _m.Tendons.Where(x => x.Group != TendonGroup.Transverse && (x.Name == $"{t.Location} {t.Name}" || x.Name == $"{t.Location} {t.Name} L" || x.Name == $"{t.Location} {t.Name} R")))
            {
                int side = r.Name.EndsWith(" L") ? -1 : 1;
                string why = "";
                _expect.Clear(); _conflicts.Clear(); _anchorExitChecks.Clear(); _endFaceChecks.Clear(); _crossingChecks.Clear();
                List<Ctrl>? ctrl = t.Layout switch
                {
                    TendonLayout.Cantilever => CantileverCtrl(t, p, lanes, side, ref why),
                    TendonLayout.CentredOnClosure => BlisterCtrl(t, p, lanes, side, ref why),
                    TendonLayout.FromEndSegment => EndSpanCtrl(t, p, lanes, side, ref why),
                    _ => null,
                };
                if (ctrl == null)
                {
                    if (why.Length == 0) why = "no profile rule for this layout";
                    kept++; keptWhy[why] = keptWhy.GetValueOrDefault(why) + 1;
                    continue;
                }
                var misfit = new List<(double S, double R, double Fit)>();
                var (path, nClamp) = FilletCtrl(ctrl, misfit);
                clamped += nClamp;
                foreach (var (ms, mr, mf) in misfit)
                    _conflicts.Add($"curve at station {ms / Ft(1):0.00} ft: R = {mr / Ft(1):0.#} ft does not fit between its P.I. and the next joint / P.I. (R = {mf / Ft(1):0.#} ft fits; built so)");
                // exact joint points
                double s0 = path[0].S, s1 = path[^1].S;
                foreach (double js in joints.Where(j => j > s0 + 1e-6 && j < s1 - 1e-6))
                {
                    if (path.Any(q => Math.Abs(q.S - js) < 1e-6)) continue;
                    int i = path.FindIndex(q => q.S > js);
                    if (i <= 0) continue;
                    var a = path[i - 1]; var b = path[i];
                    double u = (js - a.S) / Math.Max(1e-12, b.S - a.S);
                    path.Insert(i, new Ctrl(js, a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u, 0));
                }
                var pts = path.Select(q => FrameOnChord(q.S).At(new Vec2(q.X, q.Y))).ToList();
                r.Points.Clear(); r.Points.AddRange(pts);
                built++;
                // lane offsets and angle change at every joint the tendon crosses
                double maxOff = 0, maxAng = 0; string offAt = "", angAt = "";
                var kinkAt = new List<string>();
                for (int i = 1; i + 1 < path.Count; i++)
                {
                    if (!joints.Any(j => Math.Abs(j - path[i].S) < 1e-7)) continue;
                    if ((pts[i] - pts[i - 1]).Length < 1e-6 || (pts[i + 1] - pts[i]).Length < 1e-6) continue;
                    var d0 = (pts[i] - pts[i - 1]).Normalized(); var d1 = (pts[i + 1] - pts[i]).Normalized();
                    double ang = Math.Acos(Math.Clamp(Vec3.Dot(d0, d1), -1, 1)) * 180 / Math.PI;
                    if (ang > maxAng) { maxAng = ang; angAt = $"{path[i].S / Ft(1):0.00}"; }
                    var hit = _expect.Where(e => Math.Abs(e.S - path[i].S) < 1e-6).ToList();
                    if (hit.Count > 0)
                    {
                        var w = hit[0];
                        // kink RELATIVE to the lane (the lane itself follows the box and kinks with its chords): slope of (duct - lane) before / after the face
                        (double, double) Rel(Ctrl q) => (q.X - w.Side * XOf(w.L, q.S), q.Y - YOf(w.L, q.S));
                        var r0 = Rel(path[i - 1]); var r1 = Rel(path[i]); var r2 = Rel(path[i + 1]);
                        double ds0 = path[i].S - path[i - 1].S, ds1 = path[i + 1].S - path[i].S;
                        double kx = (r2.Item1 - r1.Item1) / ds1 - (r1.Item1 - r0.Item1) / ds0, ky = (r2.Item2 - r1.Item2) / ds1 - (r1.Item2 - r0.Item2) / ds0;
                        double rel = Math.Atan(Math.Sqrt(kx * kx + ky * ky)) * 180 / Math.PI;
                        if (rel > JointKinkLimitDeg) kinkAt.Add($"{path[i].S / Ft(1):0.00} ({rel:0.##} deg off the lane)");
                        double off = Math.Sqrt((w.X - path[i].X) * (w.X - path[i].X) + (w.Y - path[i].Y) * (w.Y - path[i].Y));
                        if (off > maxOff) { maxOff = off; offAt = $"{path[i].S / Ft(1):0.00}"; }
                    }
                }
                worstJoint = Math.Max(worstJoint, maxOff); worstAngle = Math.Max(worstAngle, maxAng);
                foreach (var (mod, pl, src) in _endFaceChecks)
                    _m.Checks.Add(new SegmentalCheck($"tendon {r.Name}: end segment pipe bend vs the printed bend (deg) ({src})", mod, pl, mod - pl,
                        "pipe from the end segment anchor to its bend P.I. (or to the span face when no P.I. is given), then on toward the closure joint hole"));
                foreach (var (j, mod, pl, src) in _crossingChecks)
                    _m.Checks.Add(new SegmentalCheck($"tendon {r.Name}: P.I. after the straight crossing of joint {j} where the line meets the next lane, from the face (ft) vs printed ({src})",
                        mod / Ft(1), pl / Ft(1), (mod - pl) / Ft(1), "built where the printed angle line meets the lane (the lanes narrow with the box; the printed distance assumes 9\" lane steps)"));
                foreach (var (arm, ang, off, src) in _anchorExitChecks)
                    _m.Checks.Add(new SegmentalCheck($"tendon {r.Name} ({arm}): anchor P.I. at the printed {ang:0.00} deg exit vs on the lane / diagonal through the next joint hole (in) ({src})",
                        off / In(1), 0, off / In(1), "+ = beyond the lane / diagonal (away from the anchor); built at the printed angle"));
                _m.Checks.Add(new SegmentalCheck($"tendon {r.Name}: largest duct offset from its lane hole at a joint face (in) ({p.Source})", maxOff / In(1), 0, maxOff / In(1),
                    offAt.Length > 0 ? $"at station {offAt} ft" : "every joint in its lane"));
                _m.Checks.Add(new SegmentalCheck($"tendon {r.Name}: largest angle change at a joint (deg) ({p.Source})", maxAng, 0, maxAng,
                    angAt.Length > 0 ? $"at station {angAt} ft (a corner where a neighbour joint lane was not read, or a curve across the joint)" : ""));
                // user rule: a lane change is complete before the next joint (the duct crosses every face straight, in its hole); a direction change at
                // a joint face larger than the chord kinks of the box is reported per tendon, never adjusted
                if (kinkAt.Count > 0 || _conflicts.Count > 0)
                {
                    kinks++;
                    string reason = _conflicts.Count > 0 ? string.Join("; ", _conflicts.Distinct())
                        : "a joint lane next to it is not read from the layout plan, so the lane change between the read joints is not placed inside one segment";
                    _m.Checks.Add(new SegmentalCheck($"CONFLICT tendon {r.Name}: not in its new lane before the joint ({p.Source})", kinkAt.Count, 0, kinkAt.Count,
                        (kinkAt.Count > 0 ? "corner at station " + string.Join(", ", kinkAt) + " ft: " : "") + reason + "; never moved"));
                }
                cover ??= new RebarCover(hosts);
                double rad = Math.Sqrt(r.Strands * _d.StrandArea / Math.PI);
                var (slack, at, face) = cover.Check(pts, rad, 0, 0, 0);
                if (slack < -In(1.0 / 16))
                {
                    outside++;
                    _m.Checks.Add(new SegmentalCheck($"CONFLICT tendon {r.Name}: strand bundle outside the concrete (in) ({p.Source})", -slack / In(1), 0, -slack / In(1),
                        $"near ({at.X:0.00}, {at.Y:0.00}, {at.Z:0.00}) {face}; never moved"));
                }
                if (p.Unresolved.Length > 0) r.Assumption = Assumption.UnresolvedDimension(p.Unresolved, "lanes / deviation profile as read");
            }
        }
        foreach (var (why, n) in keptWhy) Warn($"Segmental tendon profiles: {n} tendons keep the straight duct - {why}.");
        if (built > 0)
            Info($"Segmental tendon profiles: {built} tendons through their lanes (largest lane offset at a joint {worstJoint / In(1):0.###} in, largest angle change at a joint {worstAngle:0.###} deg)" +
                 (clamped > 0 ? $", {clamped} curves with a smaller radius than printed (no room between the P.I.s)" : "") + ".");
        if (kinks > 0) Warn($"Segmental tendon profiles: CONFLICT - {kinks} tendons change direction at a joint face by more than {JointKinkLimitDeg} deg or have P.I.s + R that do not fit before the next joint (check rows 'not in its new lane before the joint'; never moved).");
        if (outside > 0) Warn($"Segmental tendon profiles: CONFLICT - {outside} tendons have part of the strand bundle outside the concrete (check rows; never moved).");
    }

    private SegmentalTendonLane? LaneRow(TendonDefinition t, string arm, int joint) =>
        t.Lanes?.FirstOrDefault(l => l.Arm.Equals(arm, StringComparison.OrdinalIgnoreCase) && l.Joint == joint && l.Lane.Length > 0);

    /// <summary>Lane of an arm at a joint; missing joints take the nearest read joint toward the bent (lane kept).</summary>
    private SegmentalDuctLane? LaneOf(TendonDefinition t, string arm, int joint, Dictionary<string, SegmentalDuctLane> lanes, out bool read)
    {
        read = false;
        for (int k = joint; k >= 0; k--)
            if (LaneRow(t, arm, k) is { } row && lanes.TryGetValue(row.Lane, out var l)) { read = k == joint; return l; }
        return null;
    }

    private double YOf(SegmentalDuctLane l, double sta) => !double.IsNaN(l.Depth) ? l.Depth : SectionAt(sta).h - l.Height;
    private double XOf(SegmentalDuctLane l, double sta) => l.FromSoffitCorner ? _d.Box.HalfSoffit(SectionAt(sta).h) - l.X : l.X;

    /// <summary>The lane a tendon must be in at a joint station (for the check rows): only joints whose lane was read.</summary>
    private (double X, double Y)? LaneAtStation(TendonDefinition t, double sta, Dictionary<string, SegmentalDuctLane> lanes, int side)
    {
        if (t.Lanes == null) return null;
        if (t.Layout == TendonLayout.Cantilever && _bentSta.TryGetValue(t.Location, out double bs))
        {
            bool down = sta < bs;
            string arm = down ? "D" : "U";
            for (int k = 1; k <= 20; k++)
            {
                var sg = Seg(t.Location, k, down);
                if (sg == null) break;
                double tip = down ? sg.Station0 : sg.Station1, bentFace = down ? sg.Station1 : sg.Station0;
                int? joint = Math.Abs(tip - sta) < 1e-6 ? k : Math.Abs(bentFace - sta) < 1e-6 ? k - 1 : null;
                if (joint is int j && LaneRow(t, arm, j) is { } row && lanes.TryGetValue(row.Lane, out var l))
                {
                    if (j == Math.Max(t.AnchorDown, t.AnchorUp) && j == (down ? t.AnchorDown : t.AnchorUp)) return null;   // anchor face: anchor depth, not the lane
                    return (side * XOf(l, sta), YOf(l, sta));
                }
            }
        }
        return null;
    }

    private List<Ctrl>? CantileverCtrl(TendonDefinition t, SegmentalTendonProfile p, Dictionary<string, SegmentalDuctLane> lanes, int side, ref string why)
    {
        if (new[] { p.AnchorSetback, p.AnchorDepth, p.AnchorPiFromFace, p.AnchorRadius }.Any(double.IsNaN)) { why = "anchor setback / depth / P.I. / radius not given"; return null; }
        var dn = Seg(t.Location, t.AnchorDown, true); var up = Seg(t.Location, t.AnchorUp, false);
        if (dn == null || up == null) { why = "anchor segment not found"; return null; }
        if (LaneOf(t, "D", 0, lanes, out _) == null || LaneOf(t, "U", 0, lanes, out _) == null) { why = "no lane read at the bent"; return null; }
        double c = p.ChangePiFromFace, rc = p.ChangeRadius, cb = double.IsNaN(p.ChangePiFromBentFace) ? p.ChangePiFromFace : p.ChangePiFromBentFace;
        var ctrl = new List<Ctrl>();
        SegmentalDuctLane Anchor(string arm, int n) => LaneRow(t, arm, n) is { } row && lanes.TryGetValue(row.Lane, out var l) ? l
            : lanes.TryGetValue(p.AnchorLane, out var a) ? a : LaneOf(t, arm, n, lanes, out _)!;
        // per arm, from the anchor toward the bent. b = station direction toward the bent (D +1, U -1); joint j = tip face of segment j.
        foreach (var (arm, n, b) in new[] { ("D", t.AnchorDown, 1.0), ("U", t.AnchorUp, -1.0) })
        {
            bool down = arm == "D";
            var aseg = down ? dn : up;
            double J(int j) { if (j == 0) { var s1 = Seg(t.Location, 1, down)!; return down ? s1.Station1 : s1.Station0; } var sj = Seg(t.Location, j, down)!; return down ? sj.Station0 : sj.Station1; }
            bool Has(int j) => j == 0 ? Seg(t.Location, 1, down) != null : Seg(t.Location, j, down) != null;
            SegmentalDuctLane? Ln(int j, out bool rd) => LaneOf(t, arm, j, lanes, out rd);
            double Hx(int j) => side * XOf(Ln(j, out _)!, J(j));
            // lane change in segment k (between joints k - 1 and k), both joints read; the anchor segment n "changes" when the next one does
            bool Chg(int k)
            {
                if (k < 1 || k > n - 1 || !Has(k) || !Has(k - 1)) return false;
                var a1 = Ln(k, out bool r1); var a0 = Ln(k - 1, out bool r0);
                return a1 != null && a0 != null && r1 && r0 && !a1.Name.Equals(a0.Name, StringComparison.OrdinalIgnoreCase);
            }
            int Dir(int k) => Math.Sign(Hx(k - 1) - Hx(k));
            bool chain = !double.IsNaN(p.ChainAngleDeg) && (p.ChainTendons == null || Listed(p.ChainTendons, t.Name));
            double tanC = chain ? Math.Tan(p.ChainAngleDeg * Math.PI / 180) : 0;
            // joint j crossed on the straight diagonal: one-lane steps in the same direction on both sides (the anchor segment side counts when it is the anchor)
            bool ChainJ(int j) => chain && j >= 1 && j <= n - 1 && Chg(j) && (j == n - 1 ? true : Chg(j + 1) && Dir(j + 1) == Dir(j));
            double DiagX(int j, double st) => Hx(j) + Dir(j) * tanC * b * (st - J(j));
            // anchor and its P.I. (AnchorPiFromFace from the tip face, in the lane of joint n - 1 or on the diagonal through its hole)
            var la = Anchor(arm, n);
            double sA = down ? aseg.Station0 + p.AnchorSetback : aseg.Station1 - p.AnchorSetback;
            ctrl.Add(new Ctrl(sA, side * XOf(la, sA), p.AnchorDepth, 0));
            var l0 = LaneOf(t, arm, n - 1, lanes, out _)!;
            double sPi = down ? aseg.Station0 + p.AnchorPiFromFace : aseg.Station1 - p.AnchorPiFromFace;
            double xPi = ChainJ(n - 1) ? DiagX(n - 1, sPi) : side * XOf(l0, sPi);
            var exit = p.AnchorExits?.FirstOrDefault(e => e.Tendons.Split(',').Any(q => q.Trim().Equals(t.Name, StringComparison.OrdinalIgnoreCase)) && !double.IsNaN(e.AngleDeg));
            if (exit != null)
            {
                // printed exit angle: straight from the anchor to the P.I.; the P.I. on the lane / diagonal through the next hole is the check (p.303 2 3/4")
                double xA = side * XOf(la, sA), dir = Math.Sign(xPi - xA);
                if (dir != 0)
                {
                    double xE = xA + dir * Math.Tan(exit.AngleDeg * Math.PI / 180) * Math.Abs(sPi - sA);
                    _anchorExitChecks.Add((arm, exit.AngleDeg, (xE - xPi) * dir, exit.Source));
                    xPi = xE;
                }
            }
            ctrl.Add(new Ctrl(sPi, xPi, YOf(l0, sPi), p.AnchorRadius));
            if (double.IsNaN(c)) continue;
            for (int k = n - 1; k >= 1; k--)
            {
                if (!Chg(k)) continue;
                var lt = Ln(k, out _)!; var lb = Ln(k - 1, out _)!;
                bool tipC = ChainJ(k), bentC = ChainJ(k - 1);
                double sTip = J(k) + b * c, sBent = J(k - 1) - b * cb;
                if (tipC && bentC) continue;                                   // straight hole to hole
                ctrl.Add(new Ctrl(sTip, tipC ? DiagX(k, sTip) : side * XOf(lt, sTip), YOf(lt, sTip), rc));
                ctrl.Add(new Ctrl(sBent, bentC ? DiagX(k - 1, sBent) : side * XOf(lb, sBent), YOf(lb, sBent), rc));
            }
        }
        // every joint whose lane was read is a point of the path (the duct crosses the face in its hole even next to joints not read)
        foreach (var (arm, last) in new[] { ("D", t.AnchorDown), ("U", t.AnchorUp) })
            for (int k = 0; k < last; k++)
            {
                if (LaneRow(t, arm, k) is not { } row || !lanes.TryGetValue(row.Lane, out var l)) continue;
                var sg = Seg(t.Location, Math.Max(1, k), arm == "D");
                if (sg == null) continue;
                double js = arm == "D" ? (k == 0 ? sg.Station1 : sg.Station0) : (k == 0 ? sg.Station0 : sg.Station1);
                _expect.Add((js, side * XOf(l, js), YOf(l, js), l, side));
                if (ctrl.Any(q => Math.Abs(q.S - js) < 1e-4)) continue;
                ctrl.Add(new Ctrl(js, side * XOf(l, js), YOf(l, js), 0));
            }
        ctrl.Sort((a, b) => a.S.CompareTo(b.S));
        return Monotone(ctrl, ref why);
    }

    private List<Ctrl>? BlisterCtrl(TendonDefinition t, SegmentalTendonProfile p, Dictionary<string, SegmentalDuctLane> lanes, int side, ref string why)
    {
        bool topAnchor = !double.IsNaN(p.AnchorBelowTop);
        if (p.AnchorBlockType.Length == 0 || new[] { p.AnchorPiFromAnchor, p.AnchorRadius }.Any(double.IsNaN)
            || (!topAnchor && new[] { p.AnchorFromWeb, p.AnchorAboveSlab }.Any(double.IsNaN))) { why = "anchor block / anchor position / P.I. / radius not given"; return null; }
        string span = t.Location.Replace("Span ", "span ", StringComparison.OrdinalIgnoreCase);
        var blocks = _m.Blocks.Where(b => b.Type.Equals(p.AnchorBlockType, StringComparison.OrdinalIgnoreCase) && b.Side == side
            && b.Label.StartsWith(t.Name + " (", StringComparison.OrdinalIgnoreCase) && b.Label.Contains(span, StringComparison.OrdinalIgnoreCase)).ToList();
        if (blocks.Count != 2) { why = $"not two '{p.AnchorBlockType}' blocks serve it ({blocks.Count} found)"; return null; }
        var spans = blocks.Select(b => _blockSpans.TryGetValue((b.Segment, p.AnchorBlockType.ToLowerInvariant(), side), out var sp) ? sp : (double.NaN, double.NaN)).ToList();
        if (spans.Any(x => double.IsNaN(x.Item1))) { why = "anchor block stations not recorded"; return null; }
        var ordered = blocks.Zip(spans).OrderBy(z => z.Second.Item1).ToList();
        // the anchor face is the block end toward its bent: low-station block -> its low end, high-station block -> its high end
        double sA = ordered[0].Second.Item1, sB = ordered[1].Second.Item2;
        if (p.AnchorFromBentFace is double afb && SegByName(ordered[0].First.Segment) is { } lo0 && SegByName(ordered[1].First.Segment) is { } hi0)
        {
            // printed anchor position along the box from the anchor segment's bent-side face (low-station block: cantilever up-station of its bent)
            sA = lo0.r.Station0 + afb; sB = hi0.r.Station1 - afb;
        }
        var laneLow = ClosureLane(t, "U", lanes); var laneHigh = ClosureLane(t, "D", lanes);
        if (laneLow == null || laneHigh == null) { why = "closure-end lane not read"; return null; }
        (double x, double y) AnchorPt(double s)
        {
            // top slab anchor block: anchor AnchorBelowTop under the deck top, in plan in the tendon's lane at the anchor segment's tip face (no plan deviation drawn)
            if (topAnchor) return (double.NaN, p.AnchorBelowTop);
            var (h, tb, _) = SectionAt(s);
            double y = h - tb - p.AnchorAboveSlab;
            double yWeb = double.IsNaN(p.AnchorWebLevelAboveSlab) ? y : h - tb - p.AnchorWebLevelAboveSlab;
            return (side * (WebInsideX(yWeb) - p.AnchorFromWeb), y);
        }
        var a = AnchorPt(sA); var b = AnchorPt(sB);
        var lowSeg = SegByName(ordered[0].First.Segment); var highSeg = SegByName(ordered[1].First.Segment);
        if (lowSeg is not { } lo || highSeg is not { } hi) { why = "anchor block segments not found"; return null; }
        double c = p.ChangePiFromFace, cb = double.IsNaN(p.ChangePiFromBentFace) ? p.ChangePiFromFace : p.ChangePiFromBentFace, rc = p.ChangeRadius;
        SegmentalDuctLane? L(string arm, int k) => LaneRow(t, arm, k) is { } row && lanes.TryGetValue(row.Lane, out var l) ? l : null;
        var ctrl = new List<Ctrl> { new(sA, a.x, a.y, 0), new(sB, b.x, b.y, 0) };
        void Hold(double js, SegmentalDuctLane l) { _expect.Add((js, side * XOf(l, js), YOf(l, js), l, side)); ctrl.Add(new Ctrl(js, side * XOf(l, js), YOf(l, js), 0)); }
        foreach (var (arm, sg0, aIdx, anchorS, ax0, low) in new[] { ("U", lo.r, lo.d.Index, sA, a.x, true), ("D", hi.r, hi.d.Index, sB, b.x, false) })
        {
            double ax = ax0;
            string bent = (low ? lo.d : hi.d).Bent;
            // anchor segment: from the anchor straight in plan to its tip face (the plan's 5 deg entry), P.I. AnchorPiFromAnchor from the anchor at the lane height
            double tip = low ? sg0.Station1 : sg0.Station0;
            var la = L(arm, aIdx);
            if (la == null) { why = $"lane at the tip face of the anchor segment {sg0.Name} not read"; return null; }
            if (topAnchor)
            {
                ax = side * (double.IsNaN(p.AnchorFromCl) ? XOf(la, anchorS) : p.AnchorFromCl);
                int iA = ctrl.FindIndex(q => Math.Abs(q.S - anchorS) < 1e-9);
                ctrl[iA] = ctrl[iA] with { X = ax };
                if (low) a = (ax, a.y); else b = (ax, b.y);
            }
            // P.I. AnchorPiFromAnchor from the anchor IN the tip-face lane: the duct is in its lane before it reaches the joint (user rule); a P.I. whose
            // tangent would run past the tip face is a CONFLICT (the curve is then clamped to fit inside the segment)
            double sPi = anchorS + (low ? 1 : -1) * p.AnchorPiFromAnchor;
            ctrl.Add(new Ctrl(sPi, side * XOf(la, sPi), YOf(la, sPi), p.AnchorRadius));
            Hold(tip, la);
            {
                var d1 = new Vec3(sPi - anchorS, side * XOf(la, sPi) - ax, YOf(la, sPi) - (low ? a.y : b.y));
                double d2 = Math.Abs(tip - sPi);
                double delta = Math.Atan2(new Vec3(0, d1.Y, d1.Z).Length, Math.Abs(d1.X));
                double tan = p.AnchorRadius * Math.Tan(delta / 2);
                if (low ? sPi >= tip : sPi <= tip)
                    _conflicts.Add($"anchor P.I. {p.AnchorPiFromAnchor / Ft(1):0.##} ft from the anchor lies beyond the tip face of {sg0.Name} ({Math.Abs(tip - anchorS) / Ft(1):0.##} ft)");
                else if (tan > d2 + 1e-6)
                    _conflicts.Add($"anchor curve R = {p.AnchorRadius / Ft(1):0.#} ft needs {tan / In(1):0.#} in past the P.I., {d2 / In(1):0.#} in to the tip face of {sg0.Name}");
            }
            bool Is(string list) => list.Split(',').Any(n => n.Trim().Equals(t.Name, StringComparison.OrdinalIgnoreCase));
            double dS = low ? 1 : -1;                                       // station direction away from the bent
            // straight crossing of joint j at a printed angle: the two P.I.s on the line through the hole (no kink at the face)
            (Ctrl before, Ctrl after)? Cross(int j)
            {
                var fc = p.FaceCrossings?.FirstOrDefault(f => f.Joint == j && Is(f.Tendons));
                var sj = Seg(bent, j, !low); var lj = L(arm, j); var lp = L(arm, j - 1); var ln = L(arm, j + 1);
                if (fc == null || sj == null || lj == null) return null;
                double js = low ? sj.Station1 : sj.Station0;
                double hx = side * XOf(lj, js);
                var lo2 = lp ?? lj; var hi2 = ln ?? lj;
                double dir = Math.Sign(side * XOf(hi2, js) - side * XOf(lo2, js));
                double tan = Math.Tan(fc.AngleDeg * Math.PI / 180);
                double sb = js - dS * fc.PiBeforeFace, sa = js + dS * fc.PiAfterFace;
                var next = Seg(bent, j + 1, !low);
                bool nextCrossed = p.FaceCrossings!.Any(f => f.Joint == j + 1 && Is(f.Tendons));
                if (ln != null && next != null && !nextCrossed)
                {
                    // the duct is straight in its next lane after the P.I.: the P.I. is where the angle line meets that lane (bisection along the segment)
                    double Gap(double u) => (hx + dir * tan * u - side * XOf(ln, js + dS * u)) * dir;
                    double uLo = 0, uHi = Math.Abs(next.Station1 - next.Station0);
                    if (Gap(uLo) < 0 && Gap(uHi) > 0)
                    {
                        for (int it = 0; it < 60; it++) { double um = 0.5 * (uLo + uHi); if (Gap(um) < 0) uLo = um; else uHi = um; }
                        double u = 0.5 * (uLo + uHi);
                        _crossingChecks.Add((j, u, fc.PiAfterFace, fc.Source));
                        sa = js + dS * u;
                        return (new Ctrl(sb, hx - dir * tan * fc.PiBeforeFace, YOf(lj, sb), rc), new Ctrl(sa, side * XOf(ln, sa), YOf(ln, sa), rc));
                    }
                }
                return (new Ctrl(sb, hx - dir * tan * fc.PiBeforeFace, YOf(lj, sb), rc), new Ctrl(sa, hx + dir * tan * fc.PiAfterFace, YOf(lj, sa), rc));
            }
            for (int k = aIdx + 1; k <= 20; k++)
            {
                var sg = Seg(bent, k, !low);
                if (sg == null) break;
                var lb = L(arm, k - 1); var lt = L(arm, k);
                if (lt != null) Hold(low ? sg.Station1 : sg.Station0, lt);
                if (lb == null || lt == null || double.IsNaN(c)) continue;
                double bentFace = low ? sg.Station0 : sg.Station1, farFace = low ? sg.Station1 : sg.Station0;
                var cn = Cross(k - 1); var cf = Cross(k);
                var ov = p.LaneChangePis?.FirstOrDefault(o => (o.Segment == k || o.Segment == 0) && Is(o.Tendons));
                bool change = !lb.Name.Equals(lt.Name, StringComparison.OrdinalIgnoreCase);
                if (cn == null && cf == null)
                {
                    if (!change) continue;
                    if (ov != null && !double.IsNaN(ov.FirstPiFromBentFace) && (!double.IsNaN(ov.SecondPiFromBentFace) || ov.SecondPiFromBulkheadFace != null))
                    {
                        // printed pair for this tendon / segment, both from the bent-side face
                        double s1 = bentFace + dS * ov.FirstPiFromBentFace, s2 = ov.SecondPiFromBulkheadFace is double sb2 ? farFace - dS * sb2 : bentFace + dS * ov.SecondPiFromBentFace;
                        double ro = ov.Radius ?? rc;
                        ctrl.Add(new Ctrl(s1, side * XOf(lb, s1), YOf(lb, s1), ro));
                        ctrl.Add(new Ctrl(s2, side * XOf(lt, s2), YOf(lt, s2), ro));
                        continue;
                    }
                    // lane change inside the segment: P.I.s ChangePiFromFace from its bulkhead (tip) face and ChangePiFromBentFace from its bent-side face
                    double sn = bentFace + dS * cb, sf = farFace - dS * c;
                    ctrl.Add(new Ctrl(sn, side * XOf(lb, sn), YOf(lb, sn), rc));
                    ctrl.Add(new Ctrl(sf, side * XOf(lt, sf), YOf(lt, sf), rc));
                    continue;
                }
                // a face crossing on either face: the crossing P.I.s replace the lane-parallel P.I. on that side
                if (cn is { } n2) ctrl.Add(n2.after);
                else
                {
                    double d1 = ov != null && !double.IsNaN(ov.FirstPiFromBentFace) ? ov.FirstPiFromBentFace : cb;
                    double sn = bentFace + dS * d1;
                    ctrl.Add(new Ctrl(sn, side * XOf(lb, sn), YOf(lb, sn), rc));
                }
                // after a crossing of the near face the P.I. lies in the far lane: straight to the far face (p.304: no further P.I.)
                if (cf is { } f2) ctrl.Add(f2.before);
            }
        }
        var dup = ctrl.GroupBy(q => Math.Round(q.S, 4)).Select(g => g.First()).ToList();
        dup.Sort((x, y) => x.S.CompareTo(y.S));
        return Monotone(dup, ref why);
    }

    // End-span tendon (span next to an end segment): from the end segment anchor face (EndFaceAnchors) or a start block in the span, through the lanes
    // read at every joint (lane changes inside the segment between the bent-face / bulkhead-face P.I.s), to the anchor block nearest the bent.
    private List<Ctrl>? EndSpanCtrl(TendonDefinition t, SegmentalTendonProfile p, Dictionary<string, SegmentalDuctLane> lanes, int side, ref string why)
    {
        bool topAnchor = !double.IsNaN(p.AnchorBelowTop);
        if (p.AnchorBlockType.Length == 0 || new[] { p.AnchorPiFromAnchor, p.AnchorRadius }.Any(double.IsNaN)
            || (!topAnchor && new[] { p.AnchorFromWeb, p.AnchorAboveSlab }.Any(double.IsNaN))) { why = "anchor block / anchor position / P.I. / radius not given"; return null; }
        var rowsT = t.Lanes?.Where(l => l.Lane.Length > 0).ToList() ?? new();
        if (rowsT.Count == 0) { why = "no lane read"; return null; }
        string arm = rowsT[0].Arm.ToUpperInvariant(); bool down = arm == "D";
        if (!int.TryParse(t.Location.Replace("Span ", "", StringComparison.OrdinalIgnoreCase), out int span)) { why = "span not found"; return null; }
        string bent = (down ? span : span - 1).ToString();
        string spanL = t.Location.Replace("Span ", "span ", StringComparison.OrdinalIgnoreCase);
        // a start block may be a mirrored copy of the anchor block type ("<type> (...)": anchor face away from the bent)
        bool OfType(string type) => type.Equals(p.AnchorBlockType, StringComparison.OrdinalIgnoreCase) || type.StartsWith(p.AnchorBlockType + " (", StringComparison.OrdinalIgnoreCase);
        var blocks = _m.Blocks.Where(b => OfType(b.Type) && b.Side == side
                && b.Label.StartsWith(t.Name + " (", StringComparison.OrdinalIgnoreCase) && b.Label.Contains(spanL, StringComparison.OrdinalIgnoreCase))
            .Select(b => (b, d: SegByName(b.Segment), sp: _blockSpans.TryGetValue((b.Segment, b.Type.ToLowerInvariant(), side), out var sp) ? sp : (double.NaN, double.NaN)))
            .Where(z => z.d != null && !double.IsNaN(z.sp.Item1)).OrderBy(z => z.d!.Value.d.Index).ToList();
        if (blocks.Count == 0) { why = "anchor block not found"; return null; }
        double dS = down ? -1 : 1;                                          // station direction away from the bent
        double TipFace(SegmentResult r) => down ? r.Station0 : r.Station1;
        double BentFace(SegmentResult r) => down ? r.Station1 : r.Station0;
        SegmentalDuctLane? L(int k) => LaneRow(t, arm, k) is { } row && lanes.TryGetValue(row.Lane, out var l) ? l : null;
        (double x, double y) AnchorPt(double s, SegmentalDuctLane lane)
        {
            if (topAnchor) return (side * (double.IsNaN(p.AnchorFromCl) ? XOf(lane, s) : p.AnchorFromCl), p.AnchorBelowTop);
            var (h, tb, _) = SectionAt(s);
            double y = h - tb - p.AnchorAboveSlab;
            double yWeb = double.IsNaN(p.AnchorWebLevelAboveSlab) ? y : h - tb - p.AnchorWebLevelAboveSlab;
            return (side * (WebInsideX(yWeb) - p.AnchorFromWeb), y);
        }
        var ctrl = new List<Ctrl>();
        void Hold(double js, SegmentalDuctLane l) { _expect.Add((js, side * XOf(l, js), YOf(l, js), l, side)); ctrl.Add(new Ctrl(js, side * XOf(l, js), YOf(l, js), 0)); }
        // anchor end (block nearest the bent): anchor face toward the bent, P.I. AnchorPiFromAnchor from it in the lane of the anchor segment's tip face
        var an = blocks[0]; var aSeg = an.d!.Value; int aIdx = aSeg.d.Index;
        bent = aSeg.d.Bent;                                                 // the segments' own bent key
        var la = L(aIdx);
        if (la == null) { why = $"lane at the tip face of the anchor segment {aSeg.r.Name} not read"; return null; }
        double sA = down ? an.sp.Item2 : an.sp.Item1;
        if (p.AnchorFromBentFace is double afb) sA = BentFace(aSeg.r) + dS * afb;
        var pa = AnchorPt(sA, la);
        ctrl.Add(new Ctrl(sA, pa.x, pa.y, 0));
        double sPiA = sA + dS * p.AnchorPiFromAnchor;
        ctrl.Add(new Ctrl(sPiA, side * XOf(la, sPiA), YOf(la, sPiA), p.AnchorRadius));
        int maxRead = rowsT.Where(r => r.Arm.Equals(arm, StringComparison.OrdinalIgnoreCase)).Max(r => r.Joint);
        if (blocks.Count > 1)
        {
            // start block in the span (p.285 / p.296 B3 in segment 13): its face away from the bent, P.I. toward the bent in the lane of its bent face
            var st = blocks[^1]; var sSeg = st.d!.Value; int kStart = sSeg.d.Index;
            var l0 = L(kStart - 1);
            if (l0 == null) { why = $"lane at the bent face of the start segment {sSeg.r.Name} not read"; return null; }
            double s0 = down ? st.sp.Item1 : st.sp.Item2;
            var ps = AnchorPt(s0, l0);
            ctrl.Add(new Ctrl(s0, ps.x, ps.y, 0));
            double sPi = s0 - dS * p.AnchorPiFromAnchor;
            ctrl.Add(new Ctrl(sPi, side * XOf(l0, sPi), YOf(l0, sPi), p.AnchorRadius));
            maxRead = Math.Min(maxRead, kStart - 1);
        }
        else
        {
            var ea = p.EndFaceAnchors?.FirstOrDefault(e => e.Tendons.Split(',').Any(q => q.Trim().Equals(t.Name, StringComparison.OrdinalIgnoreCase)));
            if (ea == null || double.IsNaN(ea.FromCl) || double.IsNaN(ea.SpanFaceFromCl)) { why = "end segment anchor not given"; return null; }
            if (ea.Unresolved.Length > 0) Warn(ea.Unresolved);
            string esName = $"{(down ? span - 1 : span)}-ES";
            var es = _m.Segments.FirstOrDefault(sg => sg.Name.Equals(esName, StringComparison.OrdinalIgnoreCase));
            var lEnd = L(maxRead);
            if (es == null || lEnd == null) { why = $"end segment {esName} / lane at the closure joint not found"; return null; }
            double sAnc = down ? es.Station0 : es.Station1, sFace = down ? es.Station1 : es.Station0;
            var (h, _, _) = SectionAt(sAnc);
            double yA = double.IsNaN(ea.AboveSoffit) ? YOf(lEnd, sAnc) : h - ea.AboveSoffit;
            ctrl.Add(new Ctrl(sAnc, side * ea.FromCl, yA, 0));
            double rB = double.IsNaN(ea.BendRadius) ? 0 : ea.BendRadius;
            var next = Seg(bent, maxRead, down);
            if (ea.BendPiFromAnchor is double bpi)
            {
                // p.272 Section A-A: the pipe falls / rises straight from the anchor to its bend P.I. at the lane height, then runs on to the span face offset;
                // the direction change at the span face toward the closure joint hole is a curve of the same radius (no bend is printed there)
                double len = Math.Abs(sFace - sAnc), dirF = Math.Sign(sFace - sAnc);
                double sB2 = sAnc + dirF * bpi, xB = ea.FromCl + (ea.SpanFaceFromCl - ea.FromCl) * (bpi / len);
                ctrl.Add(new Ctrl(sB2, side * xB, YOf(lEnd, sB2), rB));
                ctrl.Add(new Ctrl(sFace, side * ea.SpanFaceFromCl, YOf(lEnd, sFace), rB));
                if (!double.IsNaN(ea.BendDeg))
                {
                    var d0 = new Vec3(sB2 - sAnc, side * (xB - ea.FromCl), YOf(lEnd, sB2) - yA);
                    var d1 = new Vec3(sFace - sB2, side * (ea.SpanFaceFromCl - xB), YOf(lEnd, sFace) - YOf(lEnd, sB2));
                    _endFaceChecks.Add((Math.Acos(Math.Clamp(Vec3.Dot(d0.Normalized(), d1.Normalized()), -1, 1)) * 180 / Math.PI, ea.BendDeg, ea.Source));
                }
            }
            else
                ctrl.Add(new Ctrl(sFace, side * ea.SpanFaceFromCl, YOf(lEnd, sFace), rB));
            if (ea.BendPiFromAnchor == null && !double.IsNaN(ea.BendDeg) && next != null)
            {
                // deflection at the span face: the pipe (anchor -> face) vs the duct to the closure joint hole
                double js = TipFace(next);
                var d0 = new Vec3(sFace - sAnc, side * (ea.SpanFaceFromCl - ea.FromCl), YOf(lEnd, sFace) - yA);
                var d1 = new Vec3(js - sFace, side * (XOf(lEnd, js) - ea.SpanFaceFromCl), YOf(lEnd, js) - YOf(lEnd, sFace));
                double ang = Math.Acos(Math.Clamp(Vec3.Dot(d0.Normalized(), d1.Normalized()), -1, 1)) * 180 / Math.PI;
                _endFaceChecks.Add((ang, ea.BendDeg, ea.Source));
            }
        }
        // every joint between the start and the anchor segment in its lane; lane changes inside the segment (P.I.s from its bent / bulkhead faces)
        double cb = double.IsNaN(p.ChangePiFromBentFace) ? p.ChangePiFromFace : p.ChangePiFromBentFace;
        for (int k = aIdx; k <= maxRead; k++)
        {
            var sg = Seg(bent, k, down); var lt = L(k);
            if (sg == null || lt == null) continue;
            Hold(TipFace(sg), lt);
            if (k == aIdx || double.IsNaN(p.ChangePiFromFace)) continue;
            var lb = L(k - 1);
            if (lb == null || lb.Name.Equals(lt.Name, StringComparison.OrdinalIgnoreCase)) continue;
            double sn = BentFace(sg) + dS * cb, sf = TipFace(sg) - dS * p.ChangePiFromFace;
            var ov = p.LaneChangePis?.FirstOrDefault(o => (o.Segment == k || o.Segment == 0) && Listed(o.Tendons, t.Name));
            if (ov != null && !double.IsNaN(ov.FirstPiFromBentFace)) sn = BentFace(sg) + dS * ov.FirstPiFromBentFace;
            if (ov != null && !double.IsNaN(ov.SecondPiFromBentFace)) sf = BentFace(sg) + dS * ov.SecondPiFromBentFace;
            if (ov?.SecondPiFromBulkheadFace is double sbf) sf = TipFace(sg) - dS * sbf;
            double ro = ov?.Radius ?? p.ChangeRadius;
            ctrl.Add(new Ctrl(sn, side * XOf(lb, sn), YOf(lb, sn), ro));
            ctrl.Add(new Ctrl(sf, side * XOf(lt, sf), YOf(lt, sf), ro));
        }
        var dup = ctrl.GroupBy(q => Math.Round(q.S, 4)).Select(g => g.First()).ToList();
        dup.Sort((x, y) => x.S.CompareTo(y.S));
        return Monotone(dup, ref why);
    }

    private static bool Listed(string list, string name) => list.Split(',').Any(n => n.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

    private SegmentalDuctLane? ClosureLane(TendonDefinition t, string arm, Dictionary<string, SegmentalDuctLane> lanes)
    {
        var row = t.Lanes?.Where(l => l.Arm.Equals(arm, StringComparison.OrdinalIgnoreCase) && l.Lane.Length > 0).OrderByDescending(l => l.Joint).FirstOrDefault();
        return row != null && lanes.TryGetValue(row.Lane, out var l) ? l : null;
    }

    private static List<Ctrl>? Monotone(List<Ctrl> c, ref string why)
    {
        for (int i = 1; i < c.Count; i++)
            if (c[i].S <= c[i - 1].S + 1e-4) { why = "profile points out of order (P.I.s overlap in a short segment)"; return null; }
        return c;
    }

    /// <summary>Corners replaced by circular arcs of their radius (box coordinates); tangents clamped where two curves need more than their shared leg (counted).</summary>
    private static (List<Ctrl>, int) FilletCtrl(List<Ctrl> c, List<(double S, double R, double Fit)>? misfit = null)
    {
        // joint hold points (R = 0) are kept even on a straight run: a curve may use its leg up to such a point, never past it (the duct is in its lane at
        // every face it crosses)
        var o = new List<Ctrl> { c[0] };
        int clamped = 0;
        Vec3 V(Ctrl q) => new(q.S, q.X, q.Y);
        // tangent length each corner needs (R tan(delta / 2)); a leg ending at a point without a curve (joint hold / anchor) may be used to its end, a leg
        // shared by two curves holds both tangents when they fit - otherwise both are reduced in proportion (counted, reported)
        int nc = c.Count;
        var need = new double[nc]; var dl = new double[nc]; var leg = new double[nc];
        for (int i = 1; i < nc; i++) leg[i] = (V(c[i]) - V(c[i - 1])).Length;
        for (int i = 1; i + 1 < nc; i++)
        {
            var e1 = (V(c[i]) - V(c[i - 1])).Normalized(); var e2 = (V(c[i + 1]) - V(c[i])).Normalized();
            dl[i] = Math.Acos(Math.Clamp(Vec3.Dot(e1, e2), -1, 1));
            need[i] = c[i].R > 0 && dl[i] >= 1e-7 ? c[i].R * Math.Tan(dl[i] / 2) : 0;
        }
        var fit = Enumerable.Repeat(1.0, nc).ToArray();
        for (int i = 1; i < nc; i++)
        {
            double sum = need[i - 1] + (i < nc - 1 ? need[i] : 0);
            if (sum > leg[i] + 1e-9) { double k = leg[i] / sum; fit[i - 1] = Math.Min(fit[i - 1], k); if (i < nc - 1) fit[i] = Math.Min(fit[i], k); }
        }
        for (int i = 1; i + 1 < c.Count; i++)
        {
            var pi = V(c[i]); var d1 = pi - V(c[i - 1]); var d2 = V(c[i + 1]) - pi;
            double l1 = d1.Length, l2 = d2.Length;
            var u1 = d1 * (1 / l1); var u2 = d2 * (1 / l2);
            double delta = dl[i];
            if (need[i] <= 0) { o.Add(c[i]); continue; }
            double t = need[i];
            if (fit[i] < 1 - 1e-12) { t = need[i] * fit[i]; misfit?.Add((c[i].S, c[i].R, t / Math.Tan(delta / 2))); clamped++; }
            double rr = t / Math.Tan(delta / 2);
            var a = pi - u1 * t; var b = pi + u2 * t;
            var centre = pi + (u2 - u1).Normalized() * (rr / Math.Cos(delta / 2));
            var va = a - centre; var vb = b - centre;
            int n = Math.Max(3, (int)Math.Ceiling(delta / (1.0 * Math.PI / 180)));
            for (int k = 0; k <= n; k++)
            {
                double f = (double)k / n;
                var q = centre + va * (Math.Sin((1 - f) * delta) / Math.Sin(delta)) + vb * (Math.Sin(f * delta) / Math.Sin(delta));
                o.Add(new Ctrl(q.X, q.Y, q.Z, 0));
            }
        }
        o.Add(c[^1]);
        return (o, clamped);
    }
}
