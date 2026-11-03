using SBM.Core.Bridge;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ rivets AS DRAWN (TrussRivetGroup.Positions, QC PTC I052)
        private int _posPlaced, _posMoved, _posDropped, _dropFree, _dropRun, _dropRange, _dropOutside, _posOnePly;
        private readonly SortedSet<string> _onePlyJoints = new(StringComparer.Ordinal);

        /// <summary>Number of section pieces in the contiguous run from the outer face on side <paramref name="sx"/> at the shank axis (0 = none).</summary>
        private int RunPieces(Inst m, Vec3 at, int sx)
        {
            double len = (m.B - m.A).Length, cut = len - m.SA - m.SB;
            double s = Vec3.Dot(at - (m.A + m.Axis * m.SA), m.Axis);
            double y = Vec3.Dot(at - m.A, m.Y) - m.Def.OffsetZ;
            var iv = new List<(double lo, double hi)>();
            foreach (var pc in m.Sec.Pieces)
            {
                if (PieceSpan(m, pc, cut) is not { } sp || s < sp.ps - 1e-6 || s > sp.ps + sp.pl + 1e-6) continue;
                var poly = pc.Polygon; var xs = new List<double>();
                for (int a = 0, b = poly.Length - 1; a < poly.Length; b = a++)
                {
                    var p = poly[a]; var q = poly[b];
                    if ((p.Y > y) == (q.Y > y)) continue;
                    xs.Add(sx * (p.X + (q.X - p.X) * (y - p.Y) / (q.Y - p.Y)));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2) if (xs[k + 1] > 1e-6) iv.Add((xs[k], xs[k + 1]));
            }
            if (iv.Count == 0) return 0;
            iv.Sort((p, q) => q.hi.CompareTo(p.hi));
            double lo = iv[0].lo; int n = 1; bool grew = true;
            var used = new HashSet<int> { 0 };
            while (grew)
            {
                grew = false;
                for (int k = 0; k < iv.Count; k++)
                    if (!used.Contains(k) && iv[k].hi >= lo - 5e-4 && iv[k].lo < lo - 1e-9) { lo = iv[k].lo; used.Add(k); n++; grew = true; }
            }
            return n;
        }
        /// <summary>Rivets placed as drawn inside the gusset, per joint context (gusset rivet cover record).</summary>
        private readonly Dictionary<JointCtx, int> _jointDrawn = new(ReferenceEqualityComparer.Instance);

        /// <summary>Rivets of one member at a joint at the drawn positions (<see cref="TrussRivetGroup.Positions"/>, joint u / v). A position whose shank would not
        /// find a clean plate stack under both heads (section rule) or whose head would sit on another rivet's head is moved to the nearest spot within 3/4 in.
        /// that works (the drawings are read from a raster sheet and the member sections are the model's), else dropped (reported).</summary>
        private void PositionRivets(JointCtx c, Inst m, TrussRivetGroup rg)
        {
            double dia = rg.Diameter > 0 ? rg.Diameter : _d.JointDefaults.RivetDiameter;
            if (dia <= 0) { Warn($"Truss: rivets of joint {c.Point} member {m.Mark} - diameter missing; not placed."); return; }
            double e = rg.EdgeDistance > 0 ? rg.EdgeDistance : 1.5 * dia;
            bool beyond = rg.Beyond;
            bool start = StartsAt(m, c.Plane, c.Point);
            bool through = !start && !(m.PlaneB.Equals(c.Plane, StringComparison.OrdinalIgnoreCase) && m.PointB.Equals(c.Point, StringComparison.OrdinalIgnoreCase));
            int sxPos = Vec3.Dot(m.X, c.N) >= 0 ? 1 : -1;
            bool sectionRule = m.Sec.Pieces.Count > 0;
            (double xo, double t)? RunAt(Vec3 c0, int side) => sectionRule ? SectionRun(m, c0, side * sxPos, dia) : (0, 0);
            var asu = RivetFlag(rg.Unresolved, beyond
                ? "positions as drawn just outside the gusset (fills / splice plates / member end)"
                : "positions as drawn on the joint sheet (rivet symbols read from the raster sheet)");
            int placed = 0, moved = 0, dropped = 0, onePly = 0;
            var dm = through ? m.Axis : Away(m, start);
            foreach (var p0 in rg.Positions!)
            {
                // drawn position in the joint (u, v) frame (member frame: along the member from the working point + section y)
                var p = rg.PositionsInMemberFrame ? new Vec2(Vec3.Dot(dm * p0.U + m.Y * p0.V, c.U), Vec3.Dot(dm * p0.U + m.Y * p0.V, c.V)) : new Vec2(p0.U, p0.V);
                Vec2? ok = null;
                foreach (var off in Nudges())
                {
                    var q = new Vec2(p.X + off.X, p.Y + off.Y);
                    var c0 = c.P + c.U * q.X + c.V * q.Y;
                    if (!beyond && !Free(c, q, dia)) continue;
                    if (sectionRule && (RunAt(c0, -1) == null || RunAt(c0, 1) == null)) continue;
                    ok = q;
                    if (off.X != 0 || off.Y != 0) moved++;
                    break;
                }
                if (ok is not { } uv)
                {
                    dropped++;
                    var c1 = c.P + c.U * p.X + c.V * p.Y;
                    double s1 = Vec3.Dot(c1 - (m.A + m.Axis * m.SA), m.Axis), cut1 = (m.B - m.A).Length - m.SA - m.SB;
                    double yl = Vec3.Dot(c1 - m.A, m.Y) - m.Def.OffsetZ;
                    string why;
                    if (s1 < 0 || s1 > cut1) { _dropRange++; why = "range"; }
                    else if (!beyond && !Free(c, p, dia)) { _dropFree++; why = "head"; }
                    else
                    {
                        _dropRun++;
                        if (yl > m.Sec.YMax || yl < m.Sec.YMin) { _dropOutside++; why = "stack-outside"; } else why = "stack";
                    }
                    Model.DroppedRivets.Add(new TrussDroppedRivet(c.Plane, c.Point, m.Mark, p.X, p.Y, s1, yl, dia, beyond, why));
                    continue;
                }
                var at = c.P + c.U * uv.X + c.V * uv.Y;
                var d1 = through ? (Vec3.Dot(at - c.P, m.Axis) >= 0 ? m.Axis : -m.Axis) : Away(m, start);
                if (!beyond) { NoteRivet(c, at, e); Occupy(c, uv, dia); _jointDrawn[c] = (_jointDrawn.TryGetValue(c, out int nd) ? nd : 0) + 1; }
                double ins = IsChord(m.Def.Role) ? PlatesAt(_insidePlates, c, at) : 0;
                // OPTIONAL DropOnePlyBeyondRivets: a beyond symbol whose shank would fasten one member piece only (nothing else there in the model) is not placed
                if (beyond && sectionRule && _d.JointDefaults.DropOnePlyBeyondRivets && ins <= 0 && PlatesAt(_outsidePlates, c, at) <= 0
                    && RunPieces(m, at, sxPos) <= 1 && RunPieces(m, at, -sxPos) <= 1)
                {
                    double s2 = Vec3.Dot(at - (m.A + m.Axis * m.SA), m.Axis), y2 = Vec3.Dot(at - m.A, m.Y) - m.Def.OffsetZ;
                    Model.DroppedRivets.Add(new TrussDroppedRivet(c.Plane, c.Point, m.Mark, uv.X, uv.Y, s2, y2, dia, true, "one-ply"));
                    onePly++;
                    _onePlyJoints.Add($"{c.Point} ({c.Plane}) {m.Mark}");
                    continue;
                }
                foreach (int side in new[] { -1, 1 })
                {
                    var run = RunAt(at, side);
                    if (beyond)
                    {
                        double xo = sectionRule ? run!.Value.xo : (m.Sec.WebOuterX > 0 ? m.Sec.WebOuterX : m.Sec.HalfWidth);
                        double tt = sectionRule ? run!.Value.t : m.Sec.OuterPly;
                        double outs = PlatesAt(_outsidePlates, c, at);
                        AddRivet(at + c.N * (side * (xo + outs)), c.N * (-side), d1, outs + tt + ins, dia, c.Asm, rg.Material, asu);
                    }
                    else
                    {
                        double stack = StackAt(c, at);
                        double g = sectionRule ? stack + Math.Max(0, c.X - run!.Value.xo) + run.Value.t + ins : Grip(c, m, at);
                        AddRivet(at + c.N * (side * (c.X + stack)), c.N * (-side), d1, g, dia, c.Asm, rg.Material, asu);
                    }
                }
                placed++;
            }
            _posPlaced += placed; _posMoved += moved; _posDropped += dropped; _posOnePly += onePly;
            Info($"Truss: joint {c.Point} ({c.Plane}) member {m.Mark}: {2 * placed} rivets {TrussSections.FormatIn(dia)} as drawn ({placed} per {(beyond ? "member face beyond the gusset" : "gusset face")}"
                + (moved > 0 ? $", {moved} moved up to 3/4 in. onto a clean plate stack" : "") + (dropped > 0 ? $", {dropped} dropped" : "") + $"){(rg.Source.Length > 0 ? " (" + rg.Source + ")" : "")}.");
            if (dropped > 0)
                Warn($"Truss CHECK joint {c.Point} ({c.Plane}) member {m.Mark}: {dropped} of {rg.Positions!.Count} drawn rivet positions{(beyond ? " beyond the gusset" : "")} have no clean plate stack under both heads (or a free head spot) within 3/4 in. - not placed.");
        }

        /// <summary>Offsets tried for a drawn rivet: none, then rings of 1/8 .. 3/4 in. (16 directions).</summary>
        private static IEnumerable<Vec2> Nudges()
        {
            yield return new Vec2(0, 0);
            for (int r = 1; r <= 6; r++)
            {
                double rr = In(r / 8.0);
                for (int k = 0; k < 16; k++)
                {
                    double a = 2 * Math.PI * k / 16;
                    yield return new Vec2(rr * Math.Cos(a), rr * Math.Sin(a));
                }
            }
        }

        private void ReportPositionRivets()
        {
            if (_posOnePly > 0)
                Warn($"Truss CHECK rivets: {_posOnePly} rivet symbols drawn just beyond the gussets would fasten one member piece only (no splice plate, fill, second ply or angle there in the model: the plate they belong to is not identified on the sheets) - not placed (TrussModel.DroppedRivets, reason one-ply): {string.Join("; ", _onePlyJoints)}.");
            if (_posPlaced + _posDropped == 0) return;
            Info($"Truss {_d.Name}: {_posPlaced} rivets per face placed as drawn ({_posMoved} moved up to 3/4 in. onto a clean plate stack / free head spot, {_posDropped} dropped: {_dropRange} beyond the member's cut length, {_dropFree} on another rivet's head, {_dropRun} without a clean plate stack, {_dropOutside} of them outside the member section's in-plane extent: symbols that are not gusset rivets - rivets drawn in profile at member edges, text - or a member narrower than drawn).");
        }
    }
}
