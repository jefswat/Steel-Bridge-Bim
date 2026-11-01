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
            var hwCount = new int[3];   // plates, nuts, couplers
            string fin = bd.FinalState.Length > 0 ? $"; {bd.FinalState}" : "";
            var pieceLen = new Dictionary<string, List<double>>();
            int firstHw = _m.PtHardware.Count;
            foreach (var t in new[] { bd.Anchor?.Unresolved ?? "" }.Concat(bd.Couplers?.SelectMany(c => new[] { c.Unresolved, c.Anchor?.Unresolved ?? "" }) ?? Array.Empty<string>())
                         .Concat(bd.Pieces?.SelectMany(p => new[] { p.Start.Coupler?.Unresolved ?? "", p.End.Coupler?.Unresolved ?? "" }) ?? Array.Empty<string>())
                         .Where(t => t.Length > 0).Distinct())
                Warn(t);
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
                        if (bd.Pieces is { Count: > 0 })
                        {
                            BuildBarPieces(bd, name, side, a, b, sa, sb, bs, r.Name, kind, assumption, fin, hwCount, pieceLen);
                            continue;
                        }
                        if (bd.Anchor is { } ah)
                        {
                            AddBarAnchor(ah, r.Name, $"{name} {r.Name} end", a, a - b, side, null, hwCount);
                            AddBarAnchor(ah, pt.r.Name, $"{name} {pt.r.Name} end", b, b - a, side, null, hwCount);
                        }
                        if (bd.Couplers != null)
                            foreach (var cd in bd.Couplers)
                            {
                                if (double.IsNaN(cd.FromBentCl) || double.IsNaN(cd.Diameter) || double.IsNaN(cd.Length)) continue;
                                foreach (int sgn in new[] { -1, 1 })
                                {
                                    if (sgn < 0 && !cd.DownStation || sgn > 0 && !cd.UpStation) continue;
                                    double s = bs + sgn * cd.FromBentCl, u = (s - sa) / (sb - sa);
                                    if (u <= 0 || u >= 1) continue;
                                    var at = Vec3.Lerp(a, b, u);
                                    var outward = (b - a) * (sgn * (sb - sa) > 0 ? 1 : -1);
                                    var hw = cd.Anchor ?? bd.Anchor;
                                    var seg = SegAtStation(s) ?? r.Name;
                                    string where = sgn < 0 ? "down-station" : "up-station";
                                    var nutEnd = hw != null ? AddBarAnchor(hw, seg, $"{name} {where} coupler", at, outward, side, null, hwCount) : at;
                                    var o = outward.Normalized();
                                    var c0 = nutEnd + o * cd.GapAfterNut; var c1 = c0 + o * cd.Length;
                                    var flag = cd.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(cd.Unresolved, "coupler at the drawn position, size per the coupler detail") : null;
                                    _m.PtHardware.Add(new SegmentalHardwareResult
                                    {
                                        Name = $"{name} {where} coupler", Kind = SegmentalHardwareKind.BarCoupler, Segment = seg, Points = new List<Vec3> { c0, c1 }, OuterDiameter = cd.Diameter, Side = side,
                                        Mesh = MiscMesh.Cylinder(c0, c1, cd.Diameter / 2, 24), Material = "PT bar coupler (PT supplier)", Assumption = flag,
                                        Description = $"threaded bar coupler {cd.Diameter / In(1):0.###}\" dia. x {cd.Length / In(1):0.###}\" on {name}, {cd.FromBentCl / Ft(1):0.###} ft {where} of the bent CL"
                                            + (cd.Source.Length > 0 ? $" ({cd.Source})" : ""),
                                    });
                                    hwCount[2]++;
                                }
                            }
                        _m.PtHardware.Add(new SegmentalHardwareResult
                        {
                            Name = name, Kind = kind, Segment = r.Name, Points = pts, OuterDiameter = bd.Diameter, Side = side,
                            Mesh = MiscMesh.Cylinder(a, b, bd.Diameter / 2, 16), Material = bd.Material, Assumption = assumption,
                            Description = $"{bd.Name} ({bd.Diameter / In(1):0.###}\" bar{(double.IsNaN(bd.DuctDiameter) ? "" : $" in a {bd.DuctDiameter / In(1):0.###}\" duct")}) from {r.Name} to {pt.r.Name}, {sideName} web, " +
                                $"{len / Ft(1):0.00} ft between the far block faces" + (bd.Source.Length > 0 ? $" ({bd.Source})" : "") + fin,
                        });
                    }
                }
            }
            // temporary bars: the bar and every plate / nut / coupler built for it are ERECTION ONLY (removed at completion; kept as the erection set)
            if (kind == SegmentalHardwareKind.TemporaryPtBar)
                for (int i = firstHw; i < _m.PtHardware.Count; i++) { _m.PtHardware[i].ErectionOnly = true; _m.PtHardware[i].FinalState = bd.FinalState; }
            if (kind == SegmentalHardwareKind.TemporaryPtBar && _m.PtHardware.Count > firstHw)
                Info($"Segmental PT bars '{bd.Name}': {_m.PtHardware.Count - firstHw} elements marked ERECTION ONLY (layer 3D-SEGMENT-ERECTION-PT, IFC SBM_ConstructionPhase).");
            if (missing > 0) Warn($"Segmental PT bars '{bd.Name}': {missing} bar ends without a '{bd.BlockType}' block placement in the segment - not built.");
            if (lengths.Count == 0) continue;
            Info($"Segmental PT bars '{bd.Name}': {lengths.Count} built, {lengths.Min() / Ft(1):0.00} - {lengths.Max() / Ft(1):0.00} ft between the far block faces"
                + (hwCount.Sum() > 0 ? $"; {hwCount[0]} anchor plates, {hwCount[1]} nuts, {hwCount[2]} couplers." : "."));
            foreach (var pc in bd.Pieces ?? new List<SegmentalBarPiece>())
                if (!double.IsNaN(pc.PrintedMinLength) && pieceLen.TryGetValue(pc.Name, out var pl) && pl.Count > 0)
                    _m.Checks.Add(new SegmentalCheck($"PT bars {bd.Name} piece {pc.Name}: shortest bar (end to end, incl. tails) vs the printed minimum bar length ({bd.Source})",
                        pl.Min(), pc.PrintedMinLength, pl.Min() - pc.PrintedMinLength, $"{pl.Count} pieces, {pl.Min() / Ft(1):0.00} - {pl.Max() / Ft(1):0.00} ft; anchors / tails / couplers as listed"));
            if (!double.IsNaN(bd.PrintedMinLength) && bd.Pieces is not { Count: > 0 })
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
            var anchorCount = new int[3];
            if (bd.Anchors != null)
                foreach (var an in bd.Anchors.Where(x => x.Unresolved.Length > 0).Select(x => x.Unresolved).Distinct()) Warn(an);
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
                        string barName = $"{r.Name} {bd.Name} {sideName}" + (bd.Positions.Count > 1 ? $" {pn}" : "");
                        if (bd.Anchors != null)
                            foreach (var an in bd.Anchors) BuildBarAnchor(bd, an, r, barName, an.End == SegmentalBarEnd.Start ? a : b, an.End == SegmentalBarEnd.Start ? a - b : b - a,
                                sF + dir * (an.End == SegmentalBarEnd.Start ? pos.Along : vertical ? pos.Along : pos.AlongEnd), side, anchorCount);
                        _m.PtHardware.Add(new SegmentalHardwareResult
                        {
                            Name = barName, Kind = kind, Segment = r.Name, Points = new List<Vec3> { a, b },
                            OuterDiameter = bd.Diameter, Side = side, Mesh = MiscMesh.Cylinder(a, b, bd.Diameter / 2, 16), Material = bd.Material, Assumption = assumption,
                            Description = $"{bd.Name} ({bd.Diameter / In(1):0.###}\" bar{(double.IsNaN(bd.DuctDiameter) ? "" : $" in a {bd.DuctDiameter / In(1):0.###}\" duct")}) in {r.Name}, {sideName}, " +
                                $"{len / Ft(1):0.00} ft between its anchor ends" + (bd.Source.Length > 0 ? $" ({bd.Source})" : ""),
                        });
                    }
                }
            }
            if (noSupport > 0) Warn($"Segmental PT bars '{bd.Name}': {noSupport} segments without a bent / support station - not built.");
            if (lengths.Count == 0) { Warn($"Segmental PT bars '{bd.Name}': no {bd.SegmentType} segment matches '{bd.Segments}' - none built."); continue; }
            Info($"Segmental PT bars '{bd.Name}': {lengths.Count} built, {lengths.Min() / Ft(1):0.00} - {lengths.Max() / Ft(1):0.00} ft between the anchor ends"
                + (bd.Anchors != null ? $"; {anchorCount[0]} anchor plates, {anchorCount[2]} nuts, {anchorCount[1]} stressing pockets." : "."));
            if (!double.IsNaN(bd.ListedLength))
                _m.Checks.Add(new SegmentalCheck($"PT bars {bd.Name}: length between the modeled anchor ends vs the printed bar length (ft) ({bd.Source})",
                    lengths.Average() / Ft(1), bd.ListedLength / Ft(1), (lengths.Average() - bd.ListedLength) / Ft(1),
                    $"range {lengths.Min() / Ft(1):0.00} - {lengths.Max() / Ft(1):0.00} ft; the printed length is anchor plate to anchor plate (note 8)"));
            if (!double.IsNaN(bd.PlanCount))
                _m.Checks.Add(new SegmentalCheck($"PT bars {bd.Name}: count vs the stressing data table ({bd.Source})", lengths.Count, bd.PlanCount, lengths.Count - bd.PlanCount, ""));
        }
    }

    private string Src(string s) => s.Length > 0 ? s : Pages;

    /// <summary>Builds the pieces of one bar line a (down-station far block face, station sa) .. b (up-station, sb): per piece the bar from end to end
    /// (anchored ends carry plate + nut facing away from the piece and the tail past the nut), anchors and couplers.</summary>
    private void BuildBarPieces(SegmentalPtBarDefinition bd, string name, int side, Vec3 a, Vec3 b, double sa, double sb, double bs, string seg0,
        SegmentalHardwareKind kind, Assumption? assumption, string fin, int[] hwCount, Dictionary<string, List<double>> pieceLen)
    {
        var dir = (b - a).Normalized();
        Vec3 At(SegmentalBarPieceEnd e, bool start) => double.IsNaN(e.FromBentCl) ? (start ? a : b) : Vec3.Lerp(a, b, (bs + e.FromBentCl - sa) / (sb - sa));
        string SegOf(Vec3 p) => SegAtStation(sa + (sb - sa) * Vec3.Dot(p - a, dir) / Math.Max((b - a).Length, 1e-9)) ?? seg0;
        // pass 1: anchors and bar ends; pass 2: an un-anchored end at a coupler of another piece ends exactly at that coupler's centre (the bar end it
        // couples), then couplers and bars
        var ends = new List<(SegmentalBarPiece pc, string pname, Vec3 e0, Vec3 e1)>();
        foreach (var pc in bd.Pieces!)
        {
            string pname = $"{name} {pc.Name}";
            Vec3 p0 = At(pc.Start, true), p1 = At(pc.End, false);
            Vec3 BarEnd(SegmentalBarPieceEnd e, Vec3 p, Vec3 outward, string where)
            {
                if (!e.Anchor) return p;
                var hw = bd.Anchor ?? new SegmentalBarHardwareDefinition();
                var nutEnd = AddBarAnchor(hw, SegOf(p), $"{pname} {where}", p, outward, side, e.Stressing ? true : null, hwCount);
                return nutEnd + outward.Normalized() * e.Tail;
            }
            ends.Add((pc, pname, BarEnd(pc.Start, p0, -dir, "start"), BarEnd(pc.End, p1, dir, "end")));
        }
        var couplerAt = ends.SelectMany(x => new[] { (x.pc.Start, x.e0), (x.pc.End, x.e1) }).Where(x => x.Item1.Coupler != null).Select(x => x.Item2).ToList();
        Vec3 Snap(SegmentalBarPieceEnd e, Vec3 p)
        {
            if (e.Anchor || e.Coupler != null) return p;
            foreach (var c in couplerAt) if ((c - p).Length < In(3)) return c;
            return p;
        }
        foreach (var (pc, pname, s0, s1) in ends)
        {
            var e0 = Snap(pc.Start, s0); var e1 = Snap(pc.End, s1);
            foreach (var (e, pe, where) in new[] { (pc.Start, e0, "start"), (pc.End, e1, "end") })
            {
                if (e.Coupler is not { } cd || double.IsNaN(cd.Diameter) || double.IsNaN(cd.Length)) continue;
                var c0 = pe - dir * (cd.Length / 2); var c1 = pe + dir * (cd.Length / 2);
                var flag = cd.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(cd.Unresolved, "coupler centred on the bar end, size per the coupler detail") : null;
                _m.PtHardware.Add(new SegmentalHardwareResult
                {
                    Name = $"{pname} {where} coupler", Kind = SegmentalHardwareKind.BarCoupler, Segment = SegOf(pe), Points = new List<Vec3> { c0, c1 }, OuterDiameter = cd.Diameter, Side = side,
                    Mesh = MiscMesh.Cylinder(c0, c1, cd.Diameter / 2, 24), Material = "PT bar coupler (PT supplier)", Assumption = flag,
                    Description = $"threaded bar coupler {cd.Diameter / In(1):0.###}\" dia. x {cd.Length / In(1):0.###}\" centred on the {where} of {pname}" + (cd.Source.Length > 0 ? $" ({cd.Source})" : "") + fin,
                });
                hwCount[2]++;
            }
            double len = (e1 - e0).Length;
            if (!pieceLen.TryGetValue(pc.Name, out var l)) pieceLen[pc.Name] = l = new List<double>();
            l.Add(len);
            _m.PtHardware.Add(new SegmentalHardwareResult
            {
                Name = pname, Kind = kind, Segment = SegOf((e0 + e1) * 0.5), Points = new List<Vec3> { e0, e1 }, OuterDiameter = bd.Diameter, Side = side,
                Mesh = MiscMesh.Cylinder(e0, e1, bd.Diameter / 2, 16), Material = bd.Material, Assumption = assumption,
                Description = $"{bd.Name} piece {pc.Name} ({bd.Diameter / In(1):0.###}\" bar), {len / Ft(1):0.00} ft end to end" + (pc.Stage.Length > 0 ? $", {pc.Stage}" : "")
                    + (bd.Source.Length > 0 ? $" ({bd.Source})" : "") + fin,
            });
        }
    }

    /// <summary>Segment whose stations contain <paramref name="s"/> (first match).</summary>
    private string? SegAtStation(double s)
    {
        foreach (var (r, _, _) in _segs) if (s >= Math.Min(r.Station0, r.Station1) - 1e-6 && s <= Math.Max(r.Station0, r.Station1) + 1e-6) return r.Name;
        return null;
    }

    /// <summary>Anchor plate (bearing face at <paramref name="at"/>) + nut outward of it along <paramref name="outward"/>; returns the outer end of the nut (or plate).</summary>
    private Vec3 AddBarAnchor(SegmentalBarHardwareDefinition h, string seg, string name, Vec3 at, Vec3 outward, int side, bool? tensioning, int[] count)
    {
        if (double.IsNaN(h.PlateWidth) || double.IsNaN(h.PlateThickness)) return at;
        var o = outward.Normalized();
        var flag = h.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(h.Unresolved, "nominal plate / nut size as listed (PT supplier's detail governs)") : null;
        var refDir = Math.Abs(o.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0);
        var u = (refDir - o * Vec3.Dot(o, refDir)).Normalized();
        var v = Vec3.Cross(o, u);
        var sec = new[] { new Vec2(-0.5, -0.5), new Vec2(0.5, -0.5), new Vec2(0.5, 0.5), new Vec2(-0.5, 0.5) };
        Vec3[] Ring(Vec3 c) => sec.Select(q => c + u * (q.X * h.PlateWidth) + v * (q.Y * h.PlateWidth)).ToArray();
        var p1 = at + o * h.PlateThickness;
        var mesh = new Mesh();
        MeshBuilder.AddLoft(mesh, sec.Select(q => q * h.PlateWidth).ToList(), new[] { Ring(at), Ring(p1) });
        string src = h.Source.Length > 0 ? $" ({h.Source})" : "";
        _m.PtHardware.Add(new SegmentalHardwareResult
        {
            Name = name + " anchor plate", Kind = SegmentalHardwareKind.AnchorPlate, Segment = seg, Points = new List<Vec3> { at, p1 }, OuterDiameter = h.PlateWidth, Side = side,
            TensioningEnd = tensioning, Mesh = mesh, Material = "Steel anchor plate (PT supplier)", Assumption = flag,
            Description = $"anchor plate {h.PlateWidth / In(1):0.###}\" x {h.PlateWidth / In(1):0.###}\" x {h.PlateThickness / In(1):0.###}\" (nominal) of {name}{src}",
        });
        count[0]++;
        if (double.IsNaN(h.NutDiameter) || double.IsNaN(h.NutLength)) return p1;
        var n1 = p1 + o * h.NutLength;
        _m.PtHardware.Add(new SegmentalHardwareResult
        {
            Name = name + " nut", Kind = SegmentalHardwareKind.BarNut, Segment = seg, Points = new List<Vec3> { p1, n1 }, OuterDiameter = h.NutDiameter, Side = side,
            Mesh = MiscMesh.Cylinder(p1, n1, h.NutDiameter / 2, 24), Material = "PT bar nut (PT supplier)", Assumption = flag,
            Description = $"spherical nut {h.NutDiameter / In(1):0.###}\" dia. x {h.NutLength / In(1):0.###}\" (nominal) of {name}{src}",
        });
        count[1]++;
        return n1;
    }

    /// <summary>Anchor plate (+ stressing pocket) at one end of a fixed PT bar: <paramref name="end"/> = the bar end (plate bearing face), <paramref name="outward"/> =
    /// direction away from the bar. The pocket opens to the surface the bar points to (deck top / soffit when it runs mostly across the section's depth, else the
    /// segment face), its depth measured normal to that surface; the void runs 1" past the surface so it opens cleanly.</summary>
    private void BuildBarAnchor(SegmentalFixedBarDefinition bd, SegmentalBarAnchorDefinition an, SegmentResult r, string barName, Vec3 end, Vec3 outward, double sta, int side, int[] count)
    {
        if (double.IsNaN(an.PlateWidth) || double.IsNaN(an.PlateThickness)) return;
        var o = outward.Normalized();
        var f = ChordFrame(r, sta);
        var refDir = Math.Abs(Vec3.Dot(o, f.Lat)) < 0.9 ? f.Lat : f.Tangent;
        var u = (refDir - o * Vec3.Dot(o, refDir)).Normalized();
        var v = Vec3.Cross(o, u);
        string endName = an.End == SegmentalBarEnd.Start ? (bd.Kind == SegmentalFixedBarKind.Vertical ? "top" : "start") : (bd.Kind == SegmentalFixedBarKind.Vertical ? "bottom" : "end");
        var flag = an.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(an.Unresolved, "nominal plate / pocket size as listed (PT supplier's detail governs)") : null;
        string src = an.Source.Length > 0 ? an.Source : bd.Source;
        Mesh Prism(Vec3 c0, double w0, Vec3 c1, double w1, bool exact)
        {
            var sec = new[] { new Vec2(-0.5, -0.5), new Vec2(0.5, -0.5), new Vec2(0.5, 0.5), new Vec2(-0.5, 0.5) };
            Vec3[] Ring(Vec3 c, double w) => sec.Select(q => c + u * (q.X * w) + v * (q.Y * w)).ToArray();
            var m = new Mesh();
            MeshBuilder.AddLoft(m, sec.Select(q => q * w0).ToList(), new[] { Ring(c0, w0), Ring(c1, w1) }, exact);
            return m;
        }
        var outer = end + o * an.PlateThickness;
        _m.PtHardware.Add(new SegmentalHardwareResult
        {
            Name = $"{barName} {endName} anchor plate", Kind = SegmentalHardwareKind.AnchorPlate, Segment = r.Name, Points = new List<Vec3> { end, outer },
            OuterDiameter = an.PlateWidth, Side = side, TensioningEnd = an.Stressing, Mesh = Prism(end, an.PlateWidth, outer, an.PlateWidth, true), Material = "Steel anchor plate (PT supplier)",
            Assumption = flag,
            Description = $"{(an.Stressing ? "stressing" : "dead")} end anchor plate {an.PlateWidth / In(1):0.###}\" x {an.PlateWidth / In(1):0.###}\" x {an.PlateThickness / In(1):0.###}\" (nominal) of {barName}"
                + (src.Length > 0 ? $" ({src})" : ""),
        });
        count[0]++;
        double nutEnd = an.PlateThickness;
        if (!double.IsNaN(an.NutDiameter) && !double.IsNaN(an.NutLength))
        {
            var n1 = outer + o * an.NutLength;
            nutEnd += an.NutLength;
            _m.PtHardware.Add(new SegmentalHardwareResult
            {
                Name = $"{barName} {endName} nut", Kind = SegmentalHardwareKind.BarNut, Segment = r.Name, Points = new List<Vec3> { outer, n1 }, OuterDiameter = an.NutDiameter, Side = side,
                Mesh = MiscMesh.Cylinder(outer, n1, an.NutDiameter / 2, 24), Material = "PT bar nut (PT supplier)", Assumption = flag,
                Description = $"nut {an.NutDiameter / In(1):0.###}\" dia. x {an.NutLength / In(1):0.###}\" (nominal envelope; shape by the PT supplier) at the {endName} of {barName}"
                    + (src.Length > 0 ? $" ({src})" : ""),
            });
            count[2]++;
        }
        if (double.IsNaN(an.PocketDepth) || double.IsNaN(an.PocketInnerWidth) || double.IsNaN(an.PocketOuterWidth)) return;
        // surface the pocket opens to: deck top / soffit (section up) or the segment face (chord tangent)
        var n = Math.Abs(Vec3.Dot(o, f.Up)) >= Math.Abs(Vec3.Dot(o, f.Tangent)) ? f.Up : f.Tangent;
        double cos = Math.Abs(Vec3.Dot(o, n));
        if (cos < 0.3) { Warn($"Segmental PT bars '{bd.Name}': {barName} {endName} pocket not built - the bar meets the surface at {Math.Acos(cos) * 180 / Math.PI:0} deg from its normal."); return; }
        double len = an.PocketDepth / cos, over = In(1) / cos;
        if (nutEnd > len + 1e-9) Warn($"Segmental PT bars '{bd.Name}': {barName} {endName} plate + nut ({nutEnd / In(1):0.##}\") stick out of the {an.PocketDepth / In(1):0.##}\" pocket.");
        double wOver = an.PocketOuterWidth + (an.PocketOuterWidth - an.PocketInnerWidth) * over / len;
        _m.Openings.Add(new SegmentalOpeningResult
        {
            Name = $"{barName} {endName} stressing pocket", Kind = SegmentalOpeningKind.StressingPocket, Segment = r.Name,
            Mesh = Prism(end, an.PocketInnerWidth, end + o * (len + over), wOver, false), RemovedVolume = 0, Assumption = flag,
            Description = $"stressing pocket {an.PocketOuterWidth / In(1):0.###}\" at the surface to {an.PocketInnerWidth / In(1):0.###}\" at the plate, {an.PocketDepth / In(1):0.###}\" deep (nominal) "
                + $"at the {endName} of {barName}; grouted after stressing (no concrete deducted)" + (src.Length > 0 ? $" ({src})" : ""),
        });
        count[1]++;
    }

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
