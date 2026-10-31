using System.Text.RegularExpressions;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL PT hardware of the segmental box (SegmentalDefinition.PtBars / Pipes; null = nothing here, output unchanged):
// PT bars between the blocks of two segments (erection bars joint by joint, bars across the bent) and bent steel pipes for future PT (deviation block
// pipes, bent diaphragm pipes) from the plans' "PIPE VARIABLES" tables. Plan differences become check rows, never adjustments.
public sealed partial class SegmentalGenerator
{
    /// <summary>Along extent (station min / max) of every block placement whose stations were computed (also blocks buried in the slab), by
    /// (segment, block type lower case, side).</summary>
    private readonly Dictionary<(string seg, string type, int side), (double s0, double s1)> _blockSpans = new();

    // ------------------------------------------------------------------ PT bars

    private void BuildPtBars()
    {
        foreach (var bd in _d.PtBars!)
        {
            if (bd.Unresolved.Length > 0) Warn(bd.Unresolved);
            if (double.IsNaN(bd.Diameter) || bd.Positions.Count == 0 || bd.BlockType.Length == 0)
            {
                Warn(UnresolvedFlag.Text(Src(bd.Source), $"PT bars '{bd.Name}'", "the bar diameter, block type or bar positions are not given", "Not built."));
                continue;
            }
            var rx = new Regex(bd.Segments.Length > 0 ? bd.Segments : "^$", RegexOptions.IgnoreCase);
            string type = bd.BlockType.ToLowerInvariant();
            var kind = bd.Kind == SegmentalPtBarKind.Temporary ? SegmentalHardwareKind.TemporaryPtBar : SegmentalHardwareKind.PermanentPtBar;
            var lengths = new List<double>();
            int missing = 0;
            var assumption = bd.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(bd.Unresolved, "bar between the far faces of the two blocks at the listed positions") : null;
            foreach (var (r, d, _) in _segs.Where(x => x.d.Type == SegmentType.Typical && rx.IsMatch(x.r.Name)))
            {
                if (!_bentSta.TryGetValue(d.Bent, out double bs)) continue;
                bool down = r.Station1 <= bs + 1e-6;
                (SegmentResult r, SegmentDefinition d)? partner = null;
                if (bd.Pairing == SegmentalBarPairing.NextTowardTip)
                {
                    foreach (var (r2, d2, _) in _segs)
                        if (d2.Type == SegmentType.Typical && d2.Bent == d.Bent && (down ? Math.Abs(r2.Station1 - r.Station0) < 1e-4 : Math.Abs(r2.Station0 - r.Station1) < 1e-4))
                            partner = (r2, d2);
                }
                else
                {
                    if (!down) continue;                                         // one bar per pair: from the down-station D segment
                    var ups = _segs.Where(x => x.d.Type == SegmentType.Typical && x.d.Bent == d.Bent && x.r.Station0 >= bs - 1e-6).OrderBy(x => x.r.Station0).ToList();
                    var dns = _segs.Where(x => x.d.Type == SegmentType.Typical && x.d.Bent == d.Bent && x.r.Station1 <= bs + 1e-6).OrderByDescending(x => x.r.Station1).ToList();
                    if (dns.Count == 0 || dns[0].r != r || ups.Count == 0) continue;
                    partner = (ups[0].r, ups[0].d);
                }
                if (partner is not { } pt) continue;
                // joint index along the arm (alternating positions)
                int jointNo = (int)Math.Round(Math.Abs((down ? r.Station0 : r.Station1) - bs) / Math.Max(1e-6, r.Station1 - r.Station0));
                foreach (int side in bd.Sides == 0 ? new[] { -1, 1 } : new[] { Math.Sign(bd.Sides) })
                {
                    if (!_blockSpans.TryGetValue((r.Name, type, side), out var b0) || !_blockSpans.TryGetValue((pt.r.Name, type, side), out var b1)) { missing++; continue; }
                    bool partnerUp = pt.r.Station0 > r.Station0;
                    double sa = partnerUp ? b0.s0 : b0.s1, sb = partnerUp ? b1.s1 : b1.s0;   // far faces
                    var positions = bd.Alternate ? new[] { bd.Positions[jointNo % bd.Positions.Count] } : bd.Positions.ToArray();
                    int pn = 0;
                    foreach (var pos in positions)
                    {
                        pn++;
                        Vec3 P(SegmentResult rr, SegmentDefinition dd, double sta)
                        {
                            double y = !double.IsNaN(pos.Depth) ? pos.Depth : SectionIn(dd, rr, sta).h - pos.Height;
                            return ChordFrame(rr, sta).At(new Vec2(side * pos.X, y));
                        }
                        var a = P(r, d, sa); var b = P(pt.r, pt.d, sb);
                        var pts = new List<Vec3> { a, b };
                        double len = (b - a).Length;
                        lengths.Add(len);
                        string sideName = side < 0 ? "left" : "right";
                        string name = $"{r.Name}-{pt.r.Name} {bd.Name} {sideName}" + (positions.Length > 1 ? $" {pn}" : "");
                        _m.PtHardware.Add(new SegmentalHardwareResult
                        {
                            Name = name, Kind = kind, Segment = r.Name, Points = pts, OuterDiameter = bd.Diameter, Side = side,
                            Mesh = MiscMesh.Cylinder(a, b, bd.Diameter / 2, 16), Material = bd.Material, Assumption = assumption,
                            Description = $"{bd.Name} ({bd.Diameter / In(1):0.###}\" bar{(double.IsNaN(bd.DuctDiameter) ? "" : $" in a {bd.DuctDiameter / In(1):0.###}\" duct")}) from {r.Name} to {pt.r.Name}, {sideName} web, " +
                                $"{len / Ft(1):0.00} ft between the far block faces" + (bd.Source.Length > 0 ? $" ({bd.Source})" : ""),
                        });
                    }
                }
            }
            if (missing > 0) Warn($"Segmental PT bars '{bd.Name}': {missing} bar ends without a '{bd.BlockType}' block placement in the segment - not built.");
            if (lengths.Count == 0) continue;
            Info($"Segmental PT bars '{bd.Name}': {lengths.Count} built, {lengths.Min() / Ft(1):0.00} - {lengths.Max() / Ft(1):0.00} ft between the far block faces.");
            if (!double.IsNaN(bd.PrintedMinLength))
                _m.Checks.Add(new SegmentalCheck($"PT bars {bd.Name}: shortest length between the far block faces vs the printed minimum bar length ({bd.Source})",
                    lengths.Min(), bd.PrintedMinLength, lengths.Min() - bd.PrintedMinLength, "the bar extends beyond the anchor plates (nut + protrusion, coupler); model = block face to block face"));
        }
    }

    // ------------------------------------------------------------------ PT bars fixed in one segment (vertical bars of bent / end segments)

    private void BuildFixedBars()
    {
        foreach (var bd in _d.FixedBars!)
        {
            if (bd.Unresolved.Length > 0) Warn(bd.Unresolved);
            bool vertical = bd.Kind == SegmentalFixedBarKind.Vertical;
            if (double.IsNaN(bd.Diameter) || bd.Positions.Count == 0 || double.IsNaN(bd.TopDepth) && double.IsNaN(bd.BottomHeight)
                || vertical && (double.IsNaN(bd.TopDepth) || double.IsNaN(bd.BottomHeight)))
            {
                Warn(UnresolvedFlag.Text(Src(bd.Source), $"PT bars '{bd.Name}'", "the bar diameter, positions or end levels are not given", "Not built."));
                continue;
            }
            var rx = new Regex(bd.Segments, RegexOptions.IgnoreCase);
            var kind = vertical ? SegmentalHardwareKind.VerticalPtBar : SegmentalHardwareKind.PermanentPtBar;
            var assumption = bd.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(bd.Unresolved, "bars at the listed positions") : null;
            var lengths = new List<double>();
            int noSupport = 0;
            foreach (var (r, d, _) in _segs.Where(x => x.d.Type == bd.SegmentType && rx.IsMatch(x.r.Name)))
            {
                // support-side face: the face nearer the segment's own bent / support control line
                double sF, sO;
                if (_bentSta.TryGetValue(d.Bent, out double bs))
                {
                    bool lowNear = Math.Abs(r.Station0 - bs) <= Math.Abs(r.Station1 - bs);
                    (sF, sO) = lowNear ? (r.Station0, r.Station1) : (r.Station1, r.Station0);
                }
                else
                {
                    // no bent (end segment): the support side is the free face (no neighbouring segment = the anchor face at the support)
                    bool lowFree = !_segs.Any(o => o.r != r && Math.Abs(o.r.Station1 - r.Station0) < 1e-6);
                    bool highFree = !_segs.Any(o => o.r != r && Math.Abs(o.r.Station0 - r.Station1) < 1e-6);
                    if (lowFree == highFree) { noSupport++; continue; }
                    (sF, sO) = lowFree ? (r.Station0, r.Station1) : (r.Station1, r.Station0);
                }
                if (bd.From == SegmentalFaceRef.FarSide) (sF, sO) = (sO, sF);
                double dir = Math.Sign(sO - sF);
                int pn = 0;
                foreach (var pos in bd.Positions)
                {
                    pn++;
                    foreach (int side in bd.Sides == 0 ? new[] { -1, 1 } : new[] { Math.Sign(bd.Sides) })
                    {
                        Vec3 a, b;
                        if (vertical)
                        {
                            double sta = sF + dir * pos.Along;
                            double h = SectionIn(d, r, sta).h, xb = double.IsNaN(pos.XBottom) ? pos.X : pos.XBottom;
                            a = ChordFrame(r, sta).At(new Vec2(side * pos.X, bd.TopDepth));
                            b = ChordFrame(r, sta).At(new Vec2(side * xb, h - bd.BottomHeight));
                        }
                        else
                        {
                            double s0 = sF + dir * pos.Along, s1 = sF + dir * pos.AlongEnd;
                            double Y(double sta) => !double.IsNaN(bd.BottomHeight) ? SectionIn(d, r, sta).h - bd.BottomHeight : bd.TopDepth;
                            a = ChordFrame(r, s0).At(new Vec2(side * pos.X, Y(s0)));
                            b = ChordFrame(r, s1).At(new Vec2(side * pos.X, Y(s1)));
                        }
                        double len = (b - a).Length;
                        lengths.Add(len);
                        string sideName = side < 0 ? "left" : "right";
                        _m.PtHardware.Add(new SegmentalHardwareResult
                        {
                            Name = $"{r.Name} {bd.Name} {sideName}" + (bd.Positions.Count > 1 ? $" {pn}" : ""), Kind = kind, Segment = r.Name, Points = new List<Vec3> { a, b },
                            OuterDiameter = bd.Diameter, Side = side, Mesh = MiscMesh.Cylinder(a, b, bd.Diameter / 2, 16), Material = bd.Material, Assumption = assumption,
                            Description = $"{bd.Name} ({bd.Diameter / In(1):0.###}\" bar{(double.IsNaN(bd.DuctDiameter) ? "" : $" in a {bd.DuctDiameter / In(1):0.###}\" duct")}) in {r.Name}, {sideName}, " +
                                $"{len / Ft(1):0.00} ft between its anchor ends" + (bd.Source.Length > 0 ? $" ({bd.Source})" : ""),
                        });
                    }
                }
            }
            if (noSupport > 0) Warn($"Segmental PT bars '{bd.Name}': {noSupport} segments without a bent / support station - not built.");
            if (lengths.Count == 0) { Warn($"Segmental PT bars '{bd.Name}': no {bd.SegmentType} segment matches '{bd.Segments}' - none built."); continue; }
            Info($"Segmental PT bars '{bd.Name}': {lengths.Count} built, {lengths.Min() / Ft(1):0.00} - {lengths.Max() / Ft(1):0.00} ft between the anchor ends.");
            if (!double.IsNaN(bd.ListedLength))
                _m.Checks.Add(new SegmentalCheck($"PT bars {bd.Name}: length between the modeled anchor ends vs the printed bar length (ft) ({bd.Source})",
                    lengths.Average() / Ft(1), bd.ListedLength / Ft(1), (lengths.Average() - bd.ListedLength) / Ft(1),
                    $"range {lengths.Min() / Ft(1):0.00} - {lengths.Max() / Ft(1):0.00} ft; the printed length is anchor plate to anchor plate (note 8)"));
            if (!double.IsNaN(bd.PlanCount))
                _m.Checks.Add(new SegmentalCheck($"PT bars {bd.Name}: count vs the stressing data table ({bd.Source})", lengths.Count, bd.PlanCount, lengths.Count - bd.PlanCount, ""));
        }
    }

    private string Src(string s) => s.Length > 0 ? s : Pages;

    // ------------------------------------------------------------------ future PT pipes

    private void BuildPipes()
    {
        var notBuilt = new List<string>();
        foreach (var pd in _d.Pipes!)
        {
            if (pd.Unresolved.Length > 0) Warn(pd.Unresolved);
            var assumption = pd.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(pd.Unresolved, "pipe from the table offsets and tangent lengths") : null;
            if (new[] { pd.A, pd.B, pd.C, pd.D, pd.F, pd.G, pd.Radius, pd.L1, pd.L3, pd.DeltaDeg, pd.OutsideDiameter }.Any(double.IsNaN))
            {
                notBuilt.Add($"{pd.Name}: table values missing");
                continue;
            }
            var hosts = new List<(SegmentResult r, SegmentDefinition d, double sta, int dir)>();   // station of the P.I. (centre) or of the outer end
            if (pd.Placement == SegmentalPipePlacement.PiAtSegmentCentre)
                foreach (var nm in pd.Segments)
                {
                    if (SegByName(nm) is not { } sd) { notBuilt.Add($"{pd.Name}: segment {nm} not found"); continue; }
                    hosts.Add((sd.r, sd.d, (sd.r.Station0 + sd.r.Station1) / 2, 1));
                }
            else
                foreach (var bent in pd.Bents)
                {
                    if (!_bentSta.TryGetValue(bent, out double bs)) { notBuilt.Add($"{pd.Name}: bent {bent} not found"); continue; }
                    var half = _segs.FirstOrDefault(x => x.d.Type == SegmentType.BentHalf && x.d.Bent.Equals(bent, StringComparison.OrdinalIgnoreCase)
                        && (pd.UpStationHalf ? x.r.Station0 >= bs - 1e-6 : x.r.Station1 <= bs + 1e-6));
                    if (half.r == null) { notBuilt.Add($"{pd.Name}: no {(pd.UpStationHalf ? "up" : "down")}station bent half at {bent}"); continue; }
                    hosts.Add(pd.UpStationHalf ? (half.r, half.d, half.r.Station1, -1) : (half.r, half.d, half.r.Station0, 1));
                }
            double tanHalf = Math.Tan(pd.DeltaDeg * Math.PI / 360);
            double t1 = pd.L1 + pd.Radius * tanHalf, t3 = pd.L3 + pd.Radius * tanHalf;
            foreach (var (r, d, sta, dir) in hosts)
                foreach (int side in pd.Side == 0 ? new[] { -1, 1 } : new[] { Math.Sign(pd.Side) })
                {
                    double Y(double s, double v) => pd.HeightsBelowDeck ? v : SectionAt(s).h - v;
                    // section coordinates (x lateral, y down) of the three points; along offsets from the tangent lengths
                    double y1 = Y(sta, pd.A), yp = Y(sta, pd.B), y3 = Y(sta, pd.C);
                    double dx1 = pd.D - pd.F, dy1 = y1 - yp, dx3 = pd.G - pd.F, dy3 = y3 - yp;
                    double s1 = Math.Sqrt(Math.Max(0, t1 * t1 - dx1 * dx1 - dy1 * dy1)), s3 = Math.Sqrt(Math.Max(0, t3 * t3 - dx3 * dx3 - dy3 * dy3));
                    double sE1, sPi, sE3;
                    if (pd.Placement == SegmentalPipePlacement.PiAtSegmentCentre) { sPi = sta; sE1 = sta - s1; sE3 = sta + s3; }
                    else { sE1 = sta; sPi = sta + dir * s1; sE3 = sPi + dir * s3; }
                    // heights re-evaluated at each point's own station (the soffit may vary)
                    y1 = Y(sE1, pd.A); yp = Y(sPi, pd.B); y3 = Y(sE3, pd.C);
                    Vec3 P(double s, double x, double y) => FrameOnChord(s).At(new Vec2(side * x, y));
                    var e1 = P(sE1, pd.D, y1); var pi = P(sPi, pd.F, yp); var e3 = P(sE3, pd.G, y3);
                    var (pts, deltaModel) = FilletPath(e1, pi, e3, pd.Radius);
                    string sideName = side < 0 ? "left" : "right";
                    var res = new SegmentalHardwareResult
                    {
                        Name = $"{r.Name} {pd.Name} {sideName}", Kind = pd.Kind == SegmentalPipeKind.DeviationPipe ? SegmentalHardwareKind.DeviationPipe : SegmentalHardwareKind.FuturePtPipe,
                        Segment = r.Name, Points = pts, OuterDiameter = pd.OutsideDiameter, InnerDiameter = double.IsNaN(pd.Wall) ? 0 : pd.OutsideDiameter - 2 * pd.Wall, Side = side,
                        Mesh = MiscMesh.Pipe(pts, pd.OutsideDiameter / 2, double.IsNaN(pd.Wall) ? pd.OutsideDiameter / 2 * 0.9 : pd.OutsideDiameter / 2 - pd.Wall, 16),
                        Material = pd.Material, Assumption = assumption,
                        Description = $"{pd.Name}: {pd.OutsideDiameter / In(1):0.###}\" OD steel pipe for future PT, R = {pd.Radius / Ft(1):0.##} ft, {sideName} web" + (pd.Source.Length > 0 ? $" ({pd.Source})" : ""),
                    };
                    _m.PtHardware.Add(res);
                    string where = $"{r.Name} {pd.Name} {sideName}";
                    _m.Checks.Add(new SegmentalCheck($"{where}: deflection from the table offsets / tangent lengths vs the printed delta (deg) ({pd.Source})",
                        deltaModel, pd.DeltaDeg, deltaModel - pd.DeltaDeg, "legs from A-G and L1 / L3 + R tan(delta/2); the arc uses the printed R"));
                    if (!double.IsNaN(pd.Length))
                        _m.Checks.Add(new SegmentalCheck($"{where}: modeled pipe length vs the printed length ({pd.Source})", res.Length, pd.Length, res.Length - pd.Length, "L1 + R x model delta + L3"));
                }
        }
        if (notBuilt.Count > 0) Warn($"Segmental pipes: {notBuilt.Count} not built - {string.Join("; ", notBuilt.Take(6))}{(notBuilt.Count > 6 ? "; ..." : "")}.");
        int nDev = _m.PtHardware.Count(h => h.Kind == SegmentalHardwareKind.DeviationPipe), nFut = _m.PtHardware.Count(h => h.Kind == SegmentalHardwareKind.FuturePtPipe);
        if (nDev + nFut > 0) Info($"Segmental pipes: {nDev} deviation block pipes, {nFut} future PT pipes.");
    }

    /// <summary>Polyline E1 - P.I. - E3 with the corner replaced by a circular arc of radius R (tangent points R tan(delta/2) from the P.I., clamped to the
    /// legs); returns the points and the deflection (deg).</summary>
    internal static (List<Vec3> pts, double deltaDeg) FilletPath(Vec3 e1, Vec3 pi, Vec3 e3, double radius)
    {
        var u1 = (pi - e1); double l1 = u1.Length; u1 = u1 * (1 / l1);
        var u3 = (e3 - pi); double l3 = u3.Length; u3 = u3 * (1 / l3);
        double cs = Math.Clamp(Vec3.Dot(u1, u3), -1, 1), delta = Math.Acos(cs);
        var pts = new List<Vec3> { e1 };
        if (delta < 1e-6) { pts.Add(e3); return (pts, 0); }
        double t = Math.Min(radius * Math.Tan(delta / 2), 0.999 * Math.Min(l1, l3));
        double rr = t / Math.Tan(delta / 2);
        var a = pi - u1 * t; var b = pi + u3 * t;
        var bis = (u3 - u1).Normalized();                               // toward the centre
        var centre = pi + bis * (rr / Math.Cos(delta / 2));
        int n = Math.Max(4, (int)Math.Ceiling(delta / (2 * Math.PI / 180)));
        var va = a - centre; var vb = b - centre;
        for (int i = 0; i <= n; i++)
        {
            double f = (double)i / n;
            double w0 = Math.Sin((1 - f) * delta) / Math.Sin(delta), w1 = Math.Sin(f * delta) / Math.Sin(delta);
            pts.Add(centre + va * w0 + vb * w1);
        }
        pts.Add(e3);
        return (pts, delta * 180 / Math.PI);
    }
}
