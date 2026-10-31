using SBM.Core.Bridge;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ rivets AS DRAWN (TrussRivetGroup.Positions, QC PTC I052)
        private int _posPlaced, _posMoved, _posDropped, _dropFree, _dropRun, _dropRange, _dropOutside;
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
            int placed = 0, moved = 0, dropped = 0;
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
            _posPlaced += placed; _posMoved += moved; _posDropped += dropped;
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
            if (_posPlaced + _posDropped == 0) return;
            Info($"Truss {_d.Name}: {_posPlaced} rivets per face placed as drawn ({_posMoved} moved up to 3/4 in. onto a clean plate stack / free head spot, {_posDropped} dropped: {_dropRange} beyond the member's cut length, {_dropFree} on another rivet's head, {_dropRun} without a clean plate stack, {_dropOutside} of them outside the member section's in-plane extent: symbols that are not gusset rivets - rivets drawn in profile at member edges, text - or a member narrower than drawn).");
        }
    }
}
