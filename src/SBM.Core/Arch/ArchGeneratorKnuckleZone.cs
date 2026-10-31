using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Arch;

public static partial class ArchGenerator
{
    private sealed partial class Gen
    {
        /// <summary>Rib box at a knuckle corner (recorded by KnuckleCorner, local coordinates): the deck-side face of the inside arch flange plate (point,
        /// unit normal toward the deck) and the plate's intrados edge line (point, unit direction up the rib).</summary>
        private sealed record RibBox(string Tie, bool AtTieStart, Vec3 FacePt, Vec3 FaceN, Vec3 EdgeP0, Vec3 EdgeDir, string Knuckle);

        private readonly List<RibBox> _ribBoxes = new();

        /// <summary>Knuckle-zone outlines for the deck bars (v7, <see cref="ArchDeckDefinition.KnuckleZonesFromSteel"/>): per zone the plan polygon at the slab
        /// top (sides extended past the first floorbeam so that only steel faces and the end web bound it) - local plan coordinates.</summary>
        private readonly List<List<Vec2>> _zoneBarPolys = new();

        /// <summary>Plan line of the rib box face at local height z.</summary>
        private static (Vec2 p, Vec2 d) FaceLine(RibBox b, double z)
        {
            var n2 = b.FaceN.XY; double len = n2.Length;
            var nn = n2 * (1 / len);
            double c = -(z - b.FacePt.Z) * b.FaceN.Z / len;      // Dot(xy - FacePt.XY, nn) = c
            return (b.FacePt.XY + nn * c, nn.Left);
        }

        private static Vec2 EdgeAt(RibBox b, double z) => (b.EdgeP0 + b.EdgeDir * ((z - b.EdgeP0.Z) / b.EdgeDir.Z)).XY;

        private static Vec2 Intersect((Vec2 p, Vec2 d) l1, (Vec2 p, Vec2 d) l2)
        {
            double den = Vec2.Cross(l1.d, l2.d);
            double s = Vec2.Cross(l2.p - l1.p, l2.d) / den;
            return l1.p + l1.d * s;
        }

        /// <summary>Point of the tie web face on the deck side at distance s from the tie end (start / end), the end tangent extended beyond it.</summary>
        private static Vec2 WebFacePt(TieGeo g, bool atStart, double s, double lat)
        {
            double u = atStart ? s : g.Length - s;
            var (p, a) = g.At(Math.Clamp(u, 0, g.Length));
            var d = Vec2.FromAngle(a);
            if (u < 0) p += d * u; else if (u > g.Length) p += d * (u - g.Length);
            return p + d.Left * lat;
        }

        /// <summary>
        /// Slab boundary along one tie in a knuckle zone (plan, local), from the end web inside face to the floorbeam knot (s = sFb from the tie end), at
        /// local height z: the tie web inside face, except at the rib corner where it follows the deck-side face of the inside arch flange plate from the
        /// end web to the plate's intrados edge at height zNotch (then a step back to the web face). Extra = length beyond the knot along the web face.
        /// </summary>
        private List<Vec2>? ZoneSide(TieGeo g, bool atStart, double sFb, Vec2 other, ArchKnuckleDefinition k, double z, double zNotch, double extra, out int notchVertices)
        {
            notchVertices = 0;
            if (EndLine(k) is not { } el) return null;
            var (p0, a0) = g.At(atStart ? 0 : g.Length);
            var left = Vec2.FromAngle(a0).Left;
            double lat = (Vec2.Dot(left, other - p0) > 0 ? 1 : -1) * g.Def.WebThickness / 2;
            double dIn = k.EndWebInsideDn();
            double? sEn = TieCutS(g, atStart, lat, dIn, k);
            if (sEn is not { } sE) return null;
            var pts = new List<Vec2>();
            double sFrom = sE;
            var box = _ribBoxes.FirstOrDefault(b => b.Knuckle == k.Name && string.Equals(b.Tie, g.Def.Name, StringComparison.OrdinalIgnoreCase) && b.AtTieStart == atStart);
            if (box != null)
            {
                var face = FaceLine(box, z);
                var endFace = (el.a + el.n * dIn, el.t);
                var e = EdgeAt(box, zNotch);
                var nn = face.d.Right;
                // F > 0 past the notch end (toward the span)
                double dSpan = Vec2.Dot(face.d, WebFacePt(g, atStart, sFb, lat) - WebFacePt(g, atStart, sE, lat)) >= 0 ? 1 : -1;
                double F(double s) => dSpan * Vec2.Dot(WebFacePt(g, atStart, s, lat) - e, face.d);
                double lo = sE, hi = sFb;
                double s2;
                if (F(lo) >= 0) s2 = sE;
                else if (F(hi) <= 0) { s2 = sFb; Warn($"Arch: knuckle {k.Name} {g.Def.Name}: the inside arch flange plate reaches past the first floorbeam at the slab top - slab corner cut to the floorbeam."); }
                else { for (int i = 0; i < 60; i++) { double m = (lo + hi) / 2; if (F(m) < 0) lo = m; else hi = m; } s2 = (lo + hi) / 2; }
                var w2 = WebFacePt(g, atStart, s2, lat);
                pts.Add(Intersect(endFace, face));
                pts.Add(Intersect(face, (w2, nn)));
                pts.Add(w2);
                notchVertices = 2;
                sFrom = s2;
            }
            double sTo = sFb + extra;
            int n = Math.Max(1, (int)Math.Ceiling((sTo - sFrom) / In(3)));
            for (int i = box == null ? 0 : 1; i <= n; i++) pts.Add(WebFacePt(g, atStart, sFrom + (sTo - sFrom) * i / n, lat));
            return pts;
        }

        /// <summary>
        /// Knuckle-zone slab (v7): ring loft between the two tie-side boundaries (<see cref="ZoneSide"/>) from the end web inside face to the first / last
        /// floorbeam knot; bottom flat at the deck plate top, top at finished grade. Rings are straight lines across between points at the same normalised
        /// length of each side. Returns the top-surface area; adds the bar outline to <see cref="_zoneBarPolys"/>.
        /// </summary>
        private double KnuckleZoneSlab(Mesh target, TieGeo ts, TieGeo tn, (double s, double n) endKnot, (double s, double n) fbKnot, bool atStart,
            ArchKnuckleDefinition k, double zb, int topN)
        {
            // South tie: its start is at the start knuckle; north tie: its END is at the start knuckle (knots run S start -> end, N end -> start).
            bool sAtStart = atStart, nAtStart = !atStart;
            double sFbS = sAtStart ? fbKnot.s : ts.Length - fbKnot.s, sFbN = nAtStart ? fbKnot.n : tn.Length - fbKnot.n;
            var pS = ts.At(endKnot.s).p; var pN = tn.At(endKnot.n).p;
            double zTop = GradeZ((pS.X + pN.X) / 2, (pS.Y + pN.Y) / 2);
            // the notch end at the slab top over each corner
            double ZN(Vec2 p) => GradeZ(p.X, p.Y);
            var sb = ZoneSide(ts, sAtStart, sFbS, pN, k, zb, ZN(pS), 0, out _);
            var st = ZoneSide(ts, sAtStart, sFbS, pN, k, ZN(pS), ZN(pS), 0, out _);
            var nb = ZoneSide(tn, nAtStart, sFbN, pS, k, zb, ZN(pN), 0, out _);
            var nt = ZoneSide(tn, nAtStart, sFbN, pS, k, ZN(pN), ZN(pN), 0, out _);
            if (sb == null || st == null || nb == null || nt == null || sb.Count != st.Count || nb.Count != nt.Count) { Warn($"Arch: knuckle {k.Name} zone slab not built (end web / tie geometry)."); return 0; }
            // bar outline: the top boundaries extended 3 ft past the floorbeam (the floorbeam line is not a steel face for the bars)
            var sx = ZoneSide(ts, sAtStart, sFbS, pN, k, ZN(pS), ZN(pS), Ft(3), out _); var nx = ZoneSide(tn, nAtStart, sFbN, pS, k, ZN(pN), ZN(pN), Ft(3), out _);
            if (sx != null && nx != null) { var poly = new List<Vec2>(sx); poly.AddRange(Enumerable.Reverse(nx)); _zoneBarPolys.Add(poly); }
            static List<double> Params(List<Vec2> pl)
            {
                var c = new List<double> { 0 };
                for (int i = 1; i < pl.Count; i++) c.Add(c[^1] + (pl[i] - pl[i - 1]).Length);
                return c.Select(x => x / c[^1]).ToList();
            }
            var tS = Params(sb); var tN = Params(nb);
            static Vec2 AtT(List<Vec2> pl, List<double> tp, double t)
            {
                int i = tp.BinarySearch(t); if (i < 0) i = ~i; i = Math.Clamp(i, 1, tp.Count - 1);
                double f = (t - tp[i - 1]) / Math.Max(1e-12, tp[i] - tp[i - 1]);
                return pl[i - 1] + (pl[i] - pl[i - 1]) * Math.Clamp(f, 0, 1);
            }
            var ts_ = tS.Concat(tN).OrderBy(x => x).ToList();
            var tl = new List<double>();
            foreach (double t in ts_) if (tl.Count == 0 || t - tl[^1] > 1e-7) tl.Add(t);
            const int nBot = 12;
            var rings = new List<Vec3[]>();
            double area = 0;
            Vec3[] ZoneRing(double t)
            {
                var a0 = AtT(sb, tS, t); var b0 = AtT(nb, tN, t);
                var a1 = AtT(st, tS, t); var b1 = AtT(nt, tN, t);
                var r = new List<Vec3>();
                for (int i = 0; i <= nBot; i++) r.Add(W(Vec2.Lerp(a0, b0, (double)i / nBot).WithZ(zb)));
                for (int i = topN; i >= 0; i--) { var q = Vec2.Lerp(a1, b1, (double)i / topN); r.Add(W(q.WithZ(GradeZ(q.X, q.Y)))); }
                return r.ToArray();
            }
            foreach (double t in tl) rings.Add(ZoneRing(t));
            for (int i = 0; i + 1 < rings.Count; i++)
            {
                var r1 = rings[i]; var r2 = rings[i + 1];
                var n1 = r1[nBot + 1]; var s1 = r1[^1]; var n2 = r2[nBot + 1]; var s2 = r2[^1];
                area += 0.5 * (Vec3.Cross(n1 - s1, s2 - s1).Length + Vec3.Cross(s2 - n2, n1 - n2).Length);
            }
            var r0 = rings[0];
            var ex = (r0[nBot] - r0[0]).Normalized(); var ez = _fr.Z;
            var sec = r0.Select(p => new Vec2(Vec3.Dot(p - r0[0], ex), Vec3.Dot(p - r0[0], ez))).ToArray();
            int tri0 = target.TriangleCount;
            MeshBuilder.AddLoft(target, sec, rings);
            // exact: the ring function above, continuous in t; the sides are polylines, so their vertices are kinks
            ExactSweep(target, tri0, r0.Length, ZoneRing, tl, "knuckle zone slab");
            Info($"Arch knuckle {k.Name} zone slab: end web inside face to the {(atStart ? "first" : "last")} floorbeam, between the tie web inside faces; rib corners cut back to the inside arch flange plates (p.151).");
            return area;
        }

        /// <summary>End web face line (distance dn from the end line toward the span) in WORLD plan coordinates (point, unit direction).</summary>
        private (Vec2 p, Vec2 d)? EndFaceWorld(ArchKnuckleDefinition k, double dn)
        {
            if (EndLine(k) is not { } el) return null;
            var lp = el.a + el.n * dn;
            return (W(lp.WithZ(0)).XY, (W((lp + el.t).WithZ(0)) - W(lp.WithZ(0))).XY.Normalized());
        }

        /// <summary>Local plan coordinates of a world plan point at world elevation zw.</summary>
        private Vec2 LocalXY(Vec2 w, double zw)
        {
            var fr = Model.Frame; var d = w.WithZ(zw) - fr.Origin;
            return new Vec2(Vec3.Dot(d, fr.X), Vec3.Dot(d, fr.Y));
        }

        /// <summary>Signed clearance of a plan point to the knuckle-zone bar outline it lies in (positive inside; +inf when it is in none).</summary>
        private double ZoneClear(Vec2 p)
        {
            double best = double.PositiveInfinity;
            foreach (var poly in _zoneBarPolys)
            {
                bool inside = false; double dmin = double.MaxValue;
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    var a = poly[j]; var b = poly[i];
                    if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) inside = !inside;
                    var ab = b - a; double t = Math.Clamp(Vec2.Dot(p - a, ab) / Math.Max(1e-12, Vec2.Dot(ab, ab)), 0, 1);
                    dmin = Math.Min(dmin, (a + ab * t - p).Length);
                }
                // a point outside this outline is only relevant when it is near it (the outlines cover the knuckle zones only)
                if (inside) best = Math.Min(best, dmin);
                else if (dmin < Ft(1)) best = Math.Min(best, -dmin);
            }
            return best;
        }

        /// <summary>Length from p along dir (unit) that keeps at least <paramref name="clear"/> to the knuckle-zone steel / end web (max <paramref name="max"/>).</summary>
        private double ClearRun(Vec2 p, Vec2 dir, double max, double clear)
        {
            double step = In(0.25);
            double s = 0;
            if (ZoneClear(p) < clear) return 0;
            while (s + step <= max && ZoneClear(p + dir * (s + step)) >= clear) s += step;
            return Math.Min(s, max);
        }
    }
}
