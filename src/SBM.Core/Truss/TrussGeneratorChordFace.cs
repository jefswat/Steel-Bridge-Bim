using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ gusset edge on the chord face (TrussJointDefinition.ChordFaceEdge, PTC agent review 2026-11-02)

        /// <summary>Outer face line of a chord member at the point in the joint (u, v) frame: point on the line and its direction (away from the point), or null
        /// when the member is not in the plane. Face = extreme of the gusset-lapped pieces (every piece except cover plates and pieces between the gussets)
        /// toward +v (<paramref name="top"/>) or -v.</summary>
        private static (Vec2 p, Vec2 d)? ChordFaceLine(Inst m, Vec3 d1, bool top, Vec3 u, Vec3 v)
        {
            double du = Vec3.Dot(d1, u), dv = Vec3.Dot(d1, v), dl = Math.Sqrt(du * du + dv * dv);
            if (dl < 0.5) return null;
            var d = new Vec2(du / dl, dv / dl);
            var nUp = d.X >= 0 ? new Vec2(-d.Y, d.X) : new Vec2(d.Y, -d.X);       // in-plane normal of the chord with v > 0
            if (nUp.Y < 0) nUp = new Vec2(-nUp.X, -nUp.Y);
            var yuv = new Vec2(Vec3.Dot(m.Y, u), Vec3.Dot(m.Y, v));
            var pieces = m.Sec.Pieces.Where(q => q.Kind != "cover plate" && !q.BetweenGussets).ToList();
            if (pieces.Count == 0) pieces = m.Sec.Pieces;
            double best = double.NaN; Vec2 bp = default;
            foreach (var q in pieces.SelectMany(q => q.Polygon))
            {
                double off = q.Y + m.Def.OffsetZ;
                var w = new Vec2(yuv.X * off, yuv.Y * off);
                double s = (w.X * nUp.X + w.Y * nUp.Y) * (top ? 1 : -1);
                if (double.IsNaN(best) || s > best) { best = s; bp = w; }
            }
            return double.IsNaN(best) ? null : (bp, d);
        }

        private static Vec2? LineX(Vec2 p0, Vec2 d0, Vec2 p1, Vec2 d1)
        {
            double den = d0.X * d1.Y - d0.Y * d1.X;
            if (Math.Abs(den) < 1e-9) return null;
            double t = ((p1.X - p0.X) * d1.Y - (p1.Y - p0.Y) * d1.X) / den;
            return new Vec2(p0.X + d0.X * t, p0.Y + d0.Y * t);
        }

        /// <summary><see cref="TrussJointDefinition.ChordFaceEdge"/>: the chord-side edge chain of the outline replaced by the chord face lines (kink where they meet),
        /// the side edges next to it carried along their own direction onto the face. Returns the outline unchanged (warning) when no chain / face is found or a
        /// vertex would move more than 30 in.</summary>
        private List<Vec2> SnapToChordFace(List<Vec2> outline, TrussChordFaceEdge edge, string pl, string pt, List<Inst> here, Vec3 u, Vec3 v)
        {
            bool top = edge == TrussChordFaceEdge.Top;
            var role = top ? TrussMemberRole.UpperChord : TrussMemberRole.LowerChord;
            var chords = here.Where(i => i.Def.Role == role).ToList();
            if (chords.Count == 0) chords = here.Where(i => IsChord(i.Def.Role) && i.Def.Role != TrussMemberRole.WindChord && i.Def.Role != TrussMemberRole.EndPost).ToList();
            // face line per side (-1 = toward lower u, +1 = higher u); the most extreme one when several chords leave on one side
            (Vec2 p, Vec2 d)? left = null, right = null;
            foreach (var m in chords)
            {
                bool through = !StartsAt(m, pl, pt) && !(m.PlaneB.Equals(pl, StringComparison.OrdinalIgnoreCase) && m.PointB.Equals(pt, StringComparison.OrdinalIgnoreCase));
                foreach (var d1 in through ? new[] { m.Axis, -m.Axis } : new[] { Away(m, StartsAt(m, pl, pt)) })
                {
                    if (ChordFaceLine(m, d1, top, u, v) is not { } f) continue;
                    if (f.d.X > 0.1) { if (right is not { } r0 || (top ? f.p.Y > r0.p.Y : f.p.Y < r0.p.Y)) right = f; }
                    else if (f.d.X < -0.1) { if (left is not { } l0 || (top ? f.p.Y > l0.p.Y : f.p.Y < l0.p.Y)) left = f; }
                }
            }
            string what = $"gusset of joint {pt} ({pl}): {(top ? "top" : "bottom")} edge on the chord face";
            if (left == null && right == null) { Warn($"Truss: {what} - no chord at the point, outline as given{Src}."); return outline; }
            left ??= (right!.Value.p, new Vec2(-right.Value.d.X, -right.Value.d.Y));
            right ??= (left.Value.p, new Vec2(-left.Value.d.X, -left.Value.d.Y));
            var L0 = left.Value; var R0 = right.Value;
            var kink = LineX(L0.p, L0.d, R0.p, R0.d) ?? L0.p;
            Vec2 FaceN(double uu)
            {
                var d = uu < kink.X ? L0.d : R0.d;
                var n = new Vec2(-d.Y, d.X); if (n.Y < 0) n = new Vec2(-n.X, -n.Y);
                return top ? n : new Vec2(-n.X, -n.Y);
            }
            var poly = new List<Vec2>(outline);
            if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
            int nv = poly.Count;
            var isFace = new bool[nv];
            for (int i = 0; i < nv; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % nv];
                double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) continue;
                var no = new Vec2(dy / len, -dx / len);
                var fn = FaceN((a.X + b.X) / 2);
                isFace[i] = no.X * fn.X + no.Y * fn.Y > Math.Cos(35 * Math.PI / 180);
            }
            // longest contiguous chain of face edges (cyclic)
            int bestStart = -1, bestLen = 0;
            for (int s = 0; s < nv; s++)
            {
                if (!isFace[s] || isFace[(s - 1 + nv) % nv] && !isFace.All(x => x)) continue;
                int k = 0; while (k < nv && isFace[(s + k) % nv]) k++;
                if (k > bestLen) { bestLen = k; bestStart = s; }
            }
            if (bestStart < 0 || bestLen >= nv - 1) { Warn($"Truss: {what} - no {(top ? "upper" : "lower")} edge chain found in the outline, outline as given{Src}."); return outline; }
            int ia = bestStart, ib = (bestStart + bestLen) % nv;
            var pa = poly[(ia - 1 + nv) % nv]; var a0 = poly[ia]; var b0 = poly[ib]; var nb = poly[(ib + 1) % nv];
            (Vec2 p, Vec2 d) Face(double uu) => uu < kink.X ? L0 : R0;
            var aF = Face(a0.X); var bF = Face(b0.X);
            var a1 = LineX(pa, new Vec2(a0.X - pa.X, a0.Y - pa.Y), aF.p, aF.d);
            var b1 = LineX(nb, new Vec2(b0.X - nb.X, b0.Y - nb.Y), bF.p, bF.d);
            double lim = 30 * 0.0254;
            if (a1 is not { } A || b1 is not { } B || (A - a0).Length > lim || (B - b0).Length > lim)
            {
                Warn($"Truss: {what} - the side edges do not meet the chord face within 30 in., outline as given{Src}.");
                return outline;
            }
            var res = new List<Vec2>();
            // keep the vertices outside the chain: from nb round to pa, then A, (kink), B
            for (int k = 1; k <= nv - bestLen - 1; k++) res.Add(poly[(ib + k) % nv]);
            res.Add(A);
            if ((A.X - kink.X) * (B.X - kink.X) < 0) res.Add(kink);
            res.Add(B);
            double moved = Math.Max((A - a0).Length, (B - b0).Length);
            Info($"Truss: {what} (each chord's own face line{((A.X - kink.X) * (B.X - kink.X) < 0 ? ", kink at the face crossing" : "")}; end vertices moved up to {L(moved)}){Src}.");
            return res;
        }
    }
}
