using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ mitred chord joints (TrussJointDefaults.MitredChordJoints, QC PTC I047)

        /// <summary>The chord member at the point the automatic chord end rule pairs with <paramref name="m"/> (the most nearly continuing one).</summary>
        private Inst? ChordPartner(Inst m, bool start, string pl, string pt)
        {
            var d1 = Away(m, start);
            return At(pl, pt).Where(o => o != m && IsChord(o.Def.Role) && o.Plane == m.Plane)
                .OrderBy(o => Vec3.Dot(d1, Away(o, StartsAt(o, pl, pt)))).FirstOrDefault();
        }

        /// <summary>Kinked chord joints (two chord members ending at the point that are each other's partner, deflection between 0 and 25 deg, no explicit setback,
        /// no chord passing through; a third chord at the point, e.g. a wind chord starting there, keeps its square end): both ends get the bisector plane through
        /// the point (normal = the mean chord direction). Runs after <see cref="Setbacks"/>
        /// (the square-cut extension stays the solid's reach; <see cref="MitreMesh"/> pulls the end faces back onto the plane).</summary>
        private void MitreChordJoints()
        {
            if (!_d.JointDefaults.MitredChordJoints) return;
            int n = 0;
            double maxDefl = 0;
            foreach (var m in _inst)
            {
                if (!IsChord(m.Def.Role) || IsCrossPlane(m.Def.Role) || m.Plane.Length == 0) continue;
                for (int end = 0; end < 2; end++)
                {
                    bool start = end == 0;
                    if ((start ? m.Def.StartSetback : m.Def.EndSetback) != null) continue;
                    string pl = start ? m.PlaneA : m.PlaneB, pt = start ? m.PointA : m.PointB;
                    if (Through(pl, pt).Any()) continue;
                    var o = ChordPartner(m, start, pl, pt);
                    if (o == null) continue;
                    bool oStart = StartsAt(o, pl, pt);
                    if ((oStart ? o.Def.StartSetback : o.Def.EndSetback) != null || ChordPartner(o, oStart, pl, pt) != m) continue;
                    var d1 = Away(m, start);
                    var d2 = Away(o, oStart);
                    double defl = Math.Acos(Math.Clamp(-Vec3.Dot(d1, d2), -1, 1));
                    if (defl < 1e-5 || defl >= Numerics.DegToRad(25)) continue;
                    var nrm = (d1 - d2).Normalized();
                    if (start) m.MitreA = (m.A, nrm); else m.MitreB = (m.B, nrm);
                    n++;
                    maxDefl = Math.Max(maxDefl, defl);
                }
            }
            if (n > 0)
                Info($"Truss {_d.Name}: {n / 2} kinked chord joints cut on the bisector plane through the panel point (one finished joint line; largest deflection {Numerics.RadToDeg(maxDefl):0.00} deg); the joint plates end on it.");
        }

        /// <summary>The member mesh with its end faces at mitred ends moved along the axis onto the joint plane (vertices on the square end planes only).
        /// The exact-extrusion recipes are dropped (the pieces are no longer straight extrusions; IFC writes the member as a face set).</summary>
        private static Mesh MitreMesh(Inst m, Mesh mesh, Vec3 a0, double cut)
        {
            var o = new Mesh();
            foreach (var p0 in mesh.Positions)
            {
                var p = p0;
                double s = Vec3.Dot(p - a0, m.Axis);
                if (m.MitreA is { } ma && Math.Abs(s) < 1e-6)
                {
                    double den = Vec3.Dot(ma.N, m.Axis);
                    if (Math.Abs(den) > 1e-9) p += m.Axis * Math.Clamp(-Vec3.Dot(ma.N, p - ma.P) / den, 0, cut / 2);
                }
                else if (m.MitreB is { } mb && Math.Abs(s - cut) < 1e-6)
                {
                    double den = Vec3.Dot(mb.N, m.Axis);
                    if (Math.Abs(den) > 1e-9) p -= m.Axis * Math.Clamp(Vec3.Dot(mb.N, p - mb.P) / den, 0, cut / 2);
                }
                o.Add(p);
            }
            o.Indices.AddRange(mesh.Indices);
            return o;
        }

        /// <summary>Directions of the two mitred chords at a joint (toward the higher station, lower-station chord first) with their in-plane up vectors, or null
        /// when the joint is not mitred.</summary>
        private (Vec3 uL, Vec3 yL, Vec3 uR, Vec3 yR)? JointMitre(string plane, string point, Vec3 P, Vec3 tan, Vec3 xv, Vec3 yv)
        {
            if (!_d.JointDefaults.MitredChordJoints) return null;
            var ends = At(plane, point).Where(i => IsChord(i.Def.Role) && i.Plane.Equals(plane, StringComparison.OrdinalIgnoreCase)
                && (StartsAt(i, plane, point) ? i.MitreA : i.MitreB) != null).ToList();
            if (ends.Count != 2) return null;
            Vec3? uL = null, uR = null;
            foreach (var c in ends)
            {
                var u = Vec3.Dot(c.Axis, tan) >= 0 ? c.Axis : -c.Axis;
                bool ahead = Vec3.Dot(Away(c, StartsAt(c, plane, point)), tan) > 0;
                if (ahead) uR = u; else uL = u;
            }
            if (uL == null || uR == null) return null;
            Vec3 Y(Vec3 u) { var y = Vec3.Cross(u, xv).Normalized(); return Vec3.Dot(y, yv) < 0 ? -y : y; }
            return (uL.Value, Y(uL.Value), uR.Value, Y(uR.Value));
        }

        /// <summary>Joint plate at a mitred joint: the part before the point follows the lower-station chord, the part after it the higher-station chord; an end at
        /// (or across) the point lies on the joint plane (normal <paramref name="n"/>). Each part = a quadrilateral in (along, up) extruded across (x0..x1) = exact.</summary>
        private static void MitredPlateBox(Mesh mesh, Vec3 P, Vec3 n, (Vec3 uL, Vec3 yL, Vec3 uR, Vec3 yR) mj, Vec3 xv, double a0, double a1, double x0, double x1, double y0, double y1)
        {
            const double eps = 1e-4;
            void Part(Vec3 u, Vec3 yS, double lo, double hi, bool cutLo, bool cutHi)
            {
                double den = Vec3.Dot(n, u);
                double OnPlane(double y) => Math.Abs(den) > 1e-9 ? -y * Vec3.Dot(n, yS) / den : 0;
                double Lo(double y) => cutLo ? OnPlane(y) : lo;
                double Hi(double y) => cutHi ? OnPlane(y) : hi;
                double ya = Math.Min(y0, y1), yb = Math.Max(y0, y1);
                if (Hi(ya) - Lo(ya) < 1e-5 || Hi(yb) - Lo(yb) < 1e-5) return;
                var poly = new[] { new Vec2(Lo(ya), ya), new Vec2(Hi(ya), ya), new Vec2(Hi(yb), yb), new Vec2(Lo(yb), yb) };
                double xa = Math.Min(x0, x1), xb = Math.Max(x0, x1);
                var ra = poly.Select(q => P + u * q.X + yS * q.Y + xv * xa).ToArray();
                var rb = poly.Select(q => P + u * q.X + yS * q.Y + xv * xb).ToArray();
                MeshBuilder.AddLoft(mesh, poly, new[] { ra, rb });
            }
            if (a0 < -eps) Part(mj.uL, mj.yL, a0, Math.Min(a1, 0), false, a1 > -eps);
            if (a1 > eps) Part(mj.uR, mj.yR, Math.Max(a0, 0), a1, a0 < eps, false);
        }

        // ------------------------------------------------------------------ web member ends clear of the joint plates (TrussJointDefaults.WebMemberClearOfJointPlates, QC PTC I048)

        /// <summary>Setback of web member <paramref name="m"/> at chord <paramref name="c"/> so its end stays <see cref="TrussJointDefaults.WebMemberClearOfJointPlates"/>
        /// clear of the Top / Bottom joint plates on the chord face it meets (stack = the plates whose length reaches the member's footprint); 0 = no such plates.</summary>
        private double ClearOfJointPlates(Inst m, Vec3 d1, double ym, Inst c, string pl, string pt)
        {
            double clr = _d.JointDefaults.WebMemberClearOfJointPlates;
            if (clr <= 0 || !_pts.TryGetValue((pl.ToUpperInvariant(), pt.ToUpperInvariant()), out var P)) return 0;
            bool top = Vec3.Dot(d1, c.Y) > 0;
            var face = top ? TrussPlateFace.Top : TrussPlateFace.Bottom;
            var plates = _d.Joints.Where(j => j.Point.Equals(pt, StringComparison.OrdinalIgnoreCase) && (j.Plane.Length == 0 || j.Plane.Equals(pl, StringComparison.OrdinalIgnoreCase)) && j.Plates != null)
                .SelectMany(j => j.Plates!).Where(p => p.Member == null && p.Face == face && p.Thickness > 0 && p.Length > 0 && p.Width > 0).ToList();
            if (plates.Count == 0) return 0;
            var tan = Tangent(StationOf(P));
            var cax = Vec3.Dot(c.Axis, tan) >= 0 ? c.Axis : -c.Axis;
            double cs = Vec3.Dot(d1, cax), sin = Math.Sqrt(Math.Max(0, 1 - cs * cs));
            if (sin < 0.1) return 0;
            double h = top ? c.Sec.YMax : -c.Sec.YMin;
            // footprint of the member on the chord face, along the chord (from the point toward the higher station)
            double ac = h * cs / sin, hw = ym / sin;
            double stack = plates.Where(p => p.Offset - p.Length / 2 < ac + hw && p.Offset + p.Length / 2 > ac - hw).Sum(p => p.Thickness);
            if (stack <= 0) return 0;
            return (h + stack + ym * Math.Abs(cs)) / sin + clr;
        }
    }
}
