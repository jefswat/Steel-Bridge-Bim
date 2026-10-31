using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ bracing member ends: every corner clear (TrussJointDefaults.BracingCornerClearance)
        private static bool IsBracing(TrussMemberRole r) => r is TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral or TrussMemberRole.Sway
            or TrussMemberRole.Portal or TrussMemberRole.Strut;
        /// <summary>Priority of a cross-plane member at a shared point: a member clears the members ending there with the same or a higher rank (struts /
        /// portals stay, laterals stop at them, sway frames stop at both).</summary>
        private static int Rank(TrussMemberRole r) => r switch
        {
            TrussMemberRole.FloorBeam or TrussMemberRole.Stringer => 3,
            TrussMemberRole.Strut or TrussMemberRole.Portal => 2,
            TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral => 1,
            _ => 0,
        };

        /// <summary>Half thickness of the truss-plane joint at a point (from the plane): the gusset inside face + the whole gusset stack, else the widest
        /// in-plane member there.</summary>
        private double StackHalf(string pl, string pt, IEnumerable<Inst> inPlane)
        {
            double h = inPlane.Select(i => i.Sec.HalfWidthAtJoints).DefaultIfEmpty(0).Max();
            if (JointMembers(pl, pt) is { } jm)
            {
                var chords = jm.here.Where(i => IsChord(i.Def.Role)).ToList();
                double X = jm.jdef is { GussetsInsideFaceDistance: > 0 } ? jm.jdef.GussetsInsideFaceDistance / 2 : (chords.Count > 0 ? chords : jm.here).Max(i => i.Sec.HalfWidthAtJoints);
                double tg = jm.jdef is { GussetThickness: > 0 } ? jm.jdef.GussetThickness : _d.JointDefaults.GussetThickness > 0 ? _d.JointDefaults.GussetThickness : In(0.75);
                tg += jm.jdef?.GussetLayers?.Sum(l => Math.Max(0, l.Thickness)) ?? 0;
                h = Math.Max(h, X + tg);
            }
            return h;
        }

        /// <summary>In-plane members at a point: ending there, chords through it, and any in-plane member whose axis passes the point (a sway frame
        /// end on a vertical between joints).</summary>
        private List<Inst> InPlaneAt(string pl, string pt, Vec3 P)
        {
            var l = At(pl, pt).Where(o => !IsCrossPlane(o.Def.Role)).Concat(Through(pl, pt)).ToList();
            foreach (var o in _inst)
            {
                if (IsCrossPlane(o.Def.Role) || o.Plane.Length == 0 || l.Contains(o)) continue;
                double len = (o.B - o.A).Length, ta = Vec3.Dot(P - o.A, o.Axis);
                if (ta > 0.01 && ta < len - 0.01 && (o.A + o.Axis * ta - P).Length < 0.005) l.Add(o);
            }
            return l.Distinct().ToList();
        }

        private static IEnumerable<(double x, double y)> Corners(Inst m) => new[] { (m.Sec.XMin, m.Sec.YMin), (m.Sec.XMax, m.Sec.YMin), (m.Sec.XMax, m.Sec.YMax), (m.Sec.XMin, m.Sec.YMax) }
            .Select(c => (c.Item1, c.Item2 + m.Def.OffsetZ));

        /// <summary>Earliest t (>= 0) from which a point moving as f0 + r t stays beyond every one of the given conditions "any satisfied" (each condition
        /// g0 + gr t > 0); null = never.</summary>
        private static double? FirstClear(IEnumerable<(double g0, double gr)> conds)
        {
            double? best = null;
            foreach (var (g0, gr) in conds)
            {
                double? t = gr > 1e-9 ? Math.Max(0, -g0 / gr) : (g0 > 0 ? 0 : null);
                if (t is { } tv && (best == null || tv < best)) best = tv;
            }
            return best;
        }

        /// <summary>Setback of a bracing member end so that every corner of its end section clears the truss-plane joint, the members passing the point
        /// and the bracing members of equal / higher rank ending there (<see cref="TrussJointDefaults.BracingCornerClearance"/>).</summary>
        private double CornerClearSetback(Inst m, bool start, string pl, string pt, double clr)
        {
            var P = start ? m.A : m.B;
            var d1 = Away(m, start);
            var offs = Corners(m).Select(c => m.X * c.x + m.Y * c.y).ToList();
            double T = 0;
            // (a) the truss-plane joint(s) at the point
            var inPlane = InPlaneAt(pl, pt, P).Where(o => o != m).ToList();
            foreach (var g in inPlane.GroupBy(o => o.Plane.Length > 0 ? o.Plane : pl, StringComparer.OrdinalIgnoreCase))
            {
                var n = Normal(g.Key, StationOf(P));
                double k = Vec3.Dot(d1, n);
                if (Math.Abs(k) < 0.2) continue;
                double s = Math.Sign(k), H = StackHalf(g.Key, pt, g), cs = _d.JointDefaults.BracingStackClearance > 0 ? _d.JointDefaults.BracingStackClearance : clr;
                foreach (var o in offs) T = Math.Max(T, (H + cs - s * Vec3.Dot(o, n)) / Math.Abs(k));
            }
            // (b) members passing near the point (any member: a strut through the apex of a K, a floor beam over a sway frame, a girder beside a lateral
            // node), (c) bracing members of equal / higher rank ending at the same point; a member steep to a bracing plate
            // defined at the point (a sway diagonal at a lateral plate) also clears the plate thickness
            double rm = offs.Max(o => o.Length);
            // a member steep to a bracing plate defined at the point (a sway diagonal at a lateral plate) clears the plate: the slab between the lowest and
            // highest face of the flatter bracing members there (over 1.5 m from the point) grown by the plate thickness
            if (_d.BracingGussets != null)
                foreach (var bg in _d.BracingGussets)
                {
                    string spec = bg.Point.Trim(), bpt = spec.Contains(':') ? spec[(spec.IndexOf(':') + 1)..].Trim() : spec;
                    var bpls = spec.Contains(':') ? new[] { spec[..spec.IndexOf(':')].Trim() } : _d.Planes.Select(x => x.Name).ToArray();
                    if (!bpls.Any(bp => _pts.TryGetValue((bp.ToUpperInvariant(), bpt.ToUpperInvariant()), out var bpP) && (bpP - P).Length < 0.005)) continue;
                    var bn = bg.Orientation == TrussBracingPlateOrientation.Transverse ? Tangent(StationOf(P)) : Up;
                    double k = Vec3.Dot(d1, bn);
                    if (Math.Abs(k) <= 0.25) continue;
                    double zlo = double.PositiveInfinity, zhi = double.NegativeInfinity;
                    foreach (var o in _inst)
                    {
                        if (o == m || !IsBracing(o.Def.Role) || Rank(o.Def.Role) <= Rank(m.Def.Role) || Math.Abs(Vec3.Dot(o.Axis, bn)) > 0.5) continue;
                        bool atA = (o.A - P).Length < 0.005, atB = (o.B - P).Length < 0.005;
                        double len = (o.B - o.A).Length, ta = Vec3.Dot(P - o.A, o.Axis);
                        bool through = !atA && !atB && ta > 0.01 && ta < len - 0.01 && (o.A + o.Axis * ta - P).Length < 0.005;
                        if (!atA && !atB && !through) continue;
                        var dirs = atA ? new[] { o.Axis } : atB ? new[] { -o.Axis } : new[] { o.Axis, -o.Axis };
                        foreach (var dd in dirs)
                            foreach (double sa in new[] { 0.0, 1.5 })
                                foreach (var c in Corners(o))
                                {
                                    double z = Vec3.Dot(dd * sa + o.X * c.x + o.Y * c.y, bn);
                                    zlo = Math.Min(zlo, z); zhi = Math.Max(zhi, z);
                                }
                    }
                    if (double.IsInfinity(zlo)) continue;
                    double side = bg.Side == TrussBracingPlateSide.Below ? 0 : bg.Thickness, sideLo = bg.Side == TrussBracingPlateSide.Above ? 0 : bg.Thickness;
                    foreach (var o in offs)
                    {
                        double z0 = Vec3.Dot(o, bn);
                        double tt = k > 0 ? (zhi + side + clr + In(1) - z0) / k : (zlo - sideLo - clr - In(1) - z0) / k;   // + 1 in.: a fitted plate tilts with the chord
                        T = Math.Max(T, tt);
                    }
                }
            foreach (var o in _inst)
            {
                if (o == m || inPlane.Contains(o)) continue;
                double len = (o.B - o.A).Length;
                double ro = Corners(o).Max(c => Math.Sqrt(c.x * c.x + c.y * c.y));
                bool atA = (o.A - P).Length < 0.005, atB = (o.B - P).Length < 0.005;
                double aLo, aHi;
                if ((atA || atB) && !IsCrossPlane(o.Def.Role)) continue;   // in-plane members ending here: the joint, rule (a)
                if (Math.Abs(Vec3.Dot(o.Axis, d1)) > 0.985) continue;   // parallel (a strut beside its floor beam): no end clearance to find
                if (atA || atB)
                {
                    if (Rank(o.Def.Role) < Rank(m.Def.Role)) continue;
                    if (Math.Abs(Vec3.Dot(o.Axis, d1)) > 0.985) continue;   // collinear continuation
                    aLo = atA ? -ro : double.NegativeInfinity; aHi = atA ? double.PositiveInfinity : len + ro;
                }
                else
                {
                    double ta = Vec3.Dot(P - o.A, o.Axis);
                    if (ta < -ro || ta > len + ro) continue;
                    var foot = o.A + o.Axis * Math.Clamp(ta, 0, len);
                    if ((P - foot).Length > rm + ro + clr) continue;
                    aLo = 0; aHi = len;
                }
                var origin = o.A + o.Y * o.Def.OffsetZ;
                double xlo = o.Sec.XMin - clr, xhi = o.Sec.XMax + clr, ylo = o.Sec.YMin - clr, yhi = o.Sec.YMax + clr;
                // separated when ALL corners lie beyond ONE face of the other's box (x, y or the along-axis ends): earliest such time
                var faces = new List<List<(double g0, double gr)>>();
                for (int f = 0; f < 6; f++) faces.Add(new());
                foreach (var off in offs)
                {
                    var rel = P + off - origin;
                    double a0 = Vec3.Dot(rel, o.Axis), ar = Vec3.Dot(d1, o.Axis), x0 = Vec3.Dot(rel, o.X), xr = Vec3.Dot(d1, o.X), y0 = Vec3.Dot(rel, o.Y), yr = Vec3.Dot(d1, o.Y);
                    faces[0].Add((x0 - xhi, xr)); faces[1].Add((xlo - x0, -xr)); faces[2].Add((y0 - yhi, yr)); faces[3].Add((ylo - y0, -yr));
                    faces[4].Add(double.IsNegativeInfinity(aLo) ? (-1, 0) : (aLo - clr - a0, -ar));
                    faces[5].Add(double.IsPositiveInfinity(aHi) ? (-1, 0) : (a0 - aHi - clr, ar));
                }
                double? Tm = null;
                foreach (var fc in faces)
                {
                    double tf = 0; bool can = true;
                    foreach (var c in fc) { if (FirstClear(new[] { c }) is { } tc) tf = Math.Max(tf, tc); else { can = false; break; } }
                    if (can && (Tm == null || tf < Tm)) Tm = tf;
                }
                if (Tm is { } tmv && tmv < 0.5 * (m.B - m.A).Length) T = Math.Max(T, tmv);   // (a clearance over half the member: not an end condition)
            }
            return T;
        }

        // ------------------------------------------------------------------ bracing connection plates (TrussDefinition.BracingGussets)
        private void BuildBracingGussets()
        {
            if (_d.BracingGussets is not { Count: > 0 } defs) return;
            var parts = Model.Steel.Parts;
            int nPlates = 0, nAngles = 0, nRiv = 0, nOut = 0, nNoSteel = 0, nToStack = 0, nFills = 0, nBridged = 0, nProbed = 0, nLone = 0;
            double fillMax = 0, bridgedMax = 0, probedMax = 0;
            var loneKind = new int[3];
            var loneAt = new Dictionary<string, int>();
            var loneEx = new List<string>();
            var missed = new List<string>();
            foreach (var bg in defs)
            {
                string spec = bg.Point.Trim();
                var planes = spec.Contains(':') ? new[] { spec[..spec.IndexOf(':')].Trim() } : _d.Planes.Select(p => p.Name).ToArray();
                string ptName = spec.Contains(':') ? spec[(spec.IndexOf(':') + 1)..].Trim() : spec;
                foreach (var pl in planes)
                {
                    if (!_pts.TryGetValue((pl.ToUpperInvariant(), ptName.ToUpperInvariant()), out var P)) { if (spec.Contains(':')) Warn($"Truss: bracing plate at {spec} - the point is not defined; not built{Src}."); continue; }
                    string where = $"{pl}:{ptName}{bg.Suffix}";
                    if (bg.Thickness <= 0) { Warn($"Truss: bracing plate at {where} - thickness missing; not built{Src}."); continue; }
                    double st = StationOf(P);
                    var tan = Tangent(st);
                    Vec3 n, u;
                    if (bg.Orientation == TrussBracingPlateOrientation.Transverse) { n = tan; u = Vec3.Cross(Up, tan).Normalized(); }   // u = left
                    else { n = Up; u = tan; }
                    var v = Vec3.Cross(n, u).Normalized();
                    Vec2 UV(Vec3 w) => new(Vec3.Dot(w - P, u), Vec3.Dot(w - P, v));
                    // connected members: ending at the point / passing it
                    var conn = new List<(Inst m, bool ends, bool start)>();
                    foreach (var o in _inst)
                    {
                        if (!IsCrossPlane(o.Def.Role)) continue;
                        bool named = bg.Members.Count > 0 && bg.Members.Any(x => x.Equals(o.Def.Name, StringComparison.OrdinalIgnoreCase) || x.Equals(o.Mark, StringComparison.OrdinalIgnoreCase));
                        if (bg.Members.Count > 0 && !named) continue;
                        if (bg.Members.Count == 0 && (!IsBracing(o.Def.Role) || (o.Def.Role == TrussMemberRole.Sway) != (bg.Orientation == TrussBracingPlateOrientation.Transverse) || Math.Abs(Vec3.Dot(o.Axis, n)) > 0.5)) continue;
                        bool atA = (o.A - P).Length < 0.005, atB = (o.B - P).Length < 0.005;
                        if (atA || atB) { conn.Add((o, true, atA)); continue; }
                        double len = (o.B - o.A).Length, ta = Vec3.Dot(P - o.A, o.Axis);
                        if (ta > 0.01 && ta < len - 0.01 && (o.A + o.Axis * ta - P).Length < 0.005) conn.Add((o, false, false));
                    }
                    if (conn.Count == 0) { Warn($"Truss: bracing plate at {where} - no bracing member ends at or passes the point; not built{Src}."); continue; }
                    if (bg.Orientation == TrussBracingPlateOrientation.Fitted)
                    {
                        // at a truss panel point: the plane through the chord direction there (mean of the chords) and the truss-plane normal (the
                        // strut direction), so the plate edge runs along the chord (the edge angle with it); elsewhere (one member, no edge angle) the plane of the connected members (the pair of axes most
                        // apart), one member = its own flange plane; normal up; u = the tangent projected into it
                        Vec3 best = default; double bl = 0;
                        var chords = InPlaneAt(pl, ptName, P).Where(o => IsChord(o.Def.Role) && o.Plane.Equals(pl, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (chords.Count > 0 && (conn.Count > 1 || bg.EdgeAngle.Length > 0) && !(bg.MemberPlane && conn.Count > 1))
                        {
                            var cd = chords.Select(o => Vec3.Dot(o.Axis, tan) >= 0 ? o.Axis : -o.Axis).Aggregate(Vec3.Zero, (a, b) => a + b).Normalized();
                            best = Vec3.Cross(cd, Normal(pl, st)); bl = best.Length;
                        }
                        else
                        {
                            for (int i = 0; i < conn.Count; i++)
                                for (int k = i + 1; k < conn.Count; k++)
                                {
                                    var c = Vec3.Cross(conn[i].m.Axis, conn[k].m.Axis);
                                    if (c.Length > bl) { bl = c.Length; best = c; }
                                }
                            if (bl <= 0.2) { best = conn[0].m.Y; bl = 1; }
                        }
                        if (bl > 0.2)
                        {
                            n = best.Normalized(); if (n.Z < 0) n = -n;
                            u = (tan - n * Vec3.Dot(tan, n)).Normalized();
                            v = Vec3.Cross(n, u).Normalized();
                        }
                    }
                    // member faces along n (each member's section corners at the point)
                    double lap = bg.Lap > 0 ? bg.Lap : In(18), margin = bg.Margin > 0 ? bg.Margin : In(1.5), gap = bg.Gap > 0 ? bg.Gap : In(0.25);
                    double pitch = bg.RivetPitch > 0 ? bg.RivetPitch : In(4);
                    int nLines = bg.RivetGage2 > 0 ? 4 : 2;
                    int CountOf(Inst m) => bg.RivetDiameter <= 0 ? 0 : bg.Rivets != null && (bg.Rivets.TryGetValue(m.Def.Name, out var c1) || bg.Rivets.TryGetValue(m.Mark, out c1)) ? c1 : bg.RivetsPerMember;
                    // lap of each member: the given lap, longer when its rivet lines need it (1 1/2 d end distances)
                    double LapOf(Inst m) { int c = CountOf(m); return c <= 0 ? lap : Math.Max(lap, 3 * bg.RivetDiameter + (Math.Ceiling(c / (double)nLines) - 1) * pitch); }
                    // face of a member along n over the part of it the plate laps (an inclined member: the lowest / highest point of the lap)
                    double Face(Inst m, bool max)
                    {
                        var c = conn.First(x => x.m == m);
                        var d = c.ends ? Away(m, c.start) : m.Axis;
                        double s0 = c.ends ? (c.start ? m.SA : m.SB) : -LapOf(m) / 2;
                        var vals = new[] { s0, s0 + LapOf(m) }.SelectMany(sa => Corners(m).Select(k => Vec3.Dot(d * sa + m.X * k.x + m.Y * k.y, n)));
                        return max ? vals.Max() : vals.Min();
                    }
                    (Vec3 dir, Vec3 across, double half) InPlane(Inst m, Vec3 d)
                    {
                        var dp = (d - n * Vec3.Dot(d, n)).Normalized();
                        var ac = Vec3.Cross(n, dp).Normalized();
                        double h = Corners(m).Max(c => Math.Abs(Vec3.Dot(m.X * c.x + m.Y * c.y, ac)));
                        return (dp, ac, h);
                    }
                    // outline
                    List<Vec2> outline;
                    if (bg.Outline is { Count: >= 3 } given) outline = given.Select(q => new Vec2(q.U, q.V)).ToList();
                    else
                    {
                        var pts = new List<Vec2>();
                        foreach (var (m, ends, start) in conn.Where(c => c.ends))
                        {
                            var (dp, ac, h) = InPlane(m, Away(m, start));
                            double s0 = start ? m.SA : m.SB;
                            foreach (double s in new[] { s0, s0 + LapOf(m) })
                                foreach (int sx in new[] { -1, 1 }) pts.Add(UV(P + Away(m, start) * s + ac * (sx * (h + margin))));
                        }
                        if (pts.Count == 0) pts.Add(new Vec2(0, 0));
                        foreach (var (m, _, _) in conn.Where(c => !c.ends))
                        {
                            var (dp, ac, h) = InPlane(m, m.Axis);
                            var dp2 = new Vec2(Vec3.Dot(dp, u), Vec3.Dot(dp, v));
                            double r = Math.Max(LapOf(m) / 2, pts.Max(q => Math.Abs(q.X * dp2.X + q.Y * dp2.Y)));
                            foreach (double s in new[] { -r, r }) foreach (int sx in new[] { -1, 1 }) pts.Add(UV(P + m.Axis * s + ac * (sx * (h + margin))));
                        }
                        outline = MeshBuilder.ConvexHull(pts);
                    }
                    // clear of the truss-plane joint stack (point on a truss plane with in-plane members)
                    var inPlane = InPlaneAt(pl, ptName, P).Where(o => o.Plane.Equals(pl, StringComparison.OrdinalIgnoreCase)).ToList();
                    Vec3 nT = default; double H0 = 0, sIn = 0;
                    if (inPlane.Count > 0 && !bg.NoStackClip)
                    {
                        nT = Normal(pl, st);
                        H0 = StackHalf(pl, ptName, inPlane);
                        sIn = Math.Sign(conn.Sum(c => Vec3.Dot(c.ends ? Away(c.m, c.start) : c.m.Axis, nT) * (c.ends ? 1 : 0)));
                        if (sIn == 0) sIn = -Math.Sign(_d.Planes.First(p => p.Name.Equals(pl, StringComparison.OrdinalIgnoreCase)).Offset);
                        var n2 = new Vec2(Vec3.Dot(nT, u), Vec3.Dot(nT, v)) * sIn;
                        if (n2.Length > 0.2)
                        {
                            outline = ClipHalf(outline, n2 / n2.Length, (H0 + gap) / n2.Length);
                            // PlateToStack: the plate reaches the stack (Gap from its face) under the edge angle: the outline part next to the stack is
                            // carried to that line (members stopping short of the stack leave the plate envelope short of it otherwise)
                            if (bg.PlateToStack && outline.Count >= 3)
                            {
                                var nu = n2 / n2.Length; double lim = (H0 + gap) / n2.Length;
                                double dmin = outline.Min(q => q.X * nu.X + q.Y * nu.Y);
                                if (dmin > lim + 1e-4)
                                {
                                    var ext = outline.Concat(outline.Where(q => q.X * nu.X + q.Y * nu.Y < dmin + 0.15).Select(q => q - nu * (q.X * nu.X + q.Y * nu.Y - lim))).ToList();
                                    outline = MeshBuilder.ConvexHull(ext);
                                    nToStack++;
                                }
                            }
                        }
                    }
                    (double z0, double z1) ZOf(TrussBracingPlateSide side)
                    {
                        bool below = side == TrussBracingPlateSide.Below;
                        double face = below ? conn.Min(c => Face(c.m, false)) : conn.Max(c => Face(c.m, true));
                        return below ? (face - bg.Thickness, face) : (face, face + bg.Thickness);
                    }
                    var sides = bg.Side == TrussBracingPlateSide.Both ? new[] { TrussBracingPlateSide.Below, TrussBracingPlateSide.Above } : new[] { bg.Side };
                    // members crossing the plate that are not connected to it (a floor beam through a lateral apex): the plate stops clear of their side
                    // face, on the side of the members ending here
                    var ending = conn.Where(c => c.ends).ToList();
                    (Vec3 inw, double c, int rank, double area, Inst o)? crossEdge = null;
                    Vec2 crossN2 = default;
                    if (ending.Count > 0 && bg.Outline is not { Count: >= 3 })
                    {
                        var cen = ending.Select(c => { var (dp, _, _) = InPlane(c.m, Away(c.m, c.start)); return UV(P + dp * ((c.start ? c.m.SA : c.m.SB) + LapOf(c.m) / 2)); })
                            .Aggregate(new Vec2(0, 0), (a, b) => a + b) / ending.Count;
                        foreach (var o in _inst)
                        {
                            if (conn.Any(c => c.m == o) || inPlane.Contains(o) || Math.Abs(Vec3.Dot(o.Axis, n)) > 0.7) continue;   // any member crossing the plate (a floor beam, a girder beside a lateral node)
                            double len = (o.B - o.A).Length, ta = Vec3.Dot(P - o.A, o.Axis);
                            if (ta < -0.3 || ta > len + 0.3) continue;
                            var foot = o.A + o.Axis * Math.Clamp(ta, 0, len) + o.Y * o.Def.OffsetZ;
                            var cz = Corners(o).Select(c => Vec3.Dot(foot - P + o.X * c.x + o.Y * (c.y - o.Def.OffsetZ), n)).ToList();
                            // (EdgeAngleOnCrossing: a member within an angle leg of the plate also carries the edge angle, e.g. a box strut narrower than the plate pair)
                            double grow = bg.EdgeAngleOnCrossing ? In(4) : 0;
                            if (!sides.Any(sd => { var (a0, a1) = ZOf(sd); return cz.Min() < a1 + grow - 1e-4 && cz.Max() > a0 - grow + 1e-4; })) continue;
                            var (dpo, aco, ho) = InPlane(o, o.Axis);
                            var f2 = UV(foot);
                            var ac2 = new Vec2(Vec3.Dot(aco, u), Vec3.Dot(aco, v));
                            if ((cen - f2).Length < 1e-6) continue;
                            double sg = Math.Sign((cen.X - f2.X) * ac2.X + (cen.Y - f2.Y) * ac2.Y);
                            if (sg == 0) continue;
                            var nrm = ac2 * sg;
                            double before = Math.Abs(MeshBuilder.SignedArea(outline));
                            outline = ClipHalf(outline, nrm, f2.X * nrm.X + f2.Y * nrm.Y + ho + bg.CrossingFaceOffset + gap);
                            double removed = before - (outline.Count >= 3 ? Math.Abs(MeshBuilder.SignedArea(outline)) : 0);
                            double cFace = f2.X * nrm.X + f2.Y * nrm.Y + ho + bg.CrossingFaceOffset;
                            // candidates for the edge angle: clipped, or (EdgeAngleOnCrossing) a face within 0.5 m of the plate
                            bool near = bg.EdgeAngleOnCrossing && outline.Count >= 3 && outline.Min(q => q.X * nrm.X + q.Y * nrm.Y) - cFace < 0.5;
                            if ((removed > 1e-6 || near) && (crossEdge == null || Rank(o.Def.Role) > crossEdge.Value.rank || Rank(o.Def.Role) == crossEdge.Value.rank && removed > crossEdge.Value.area))
                            {
                                crossEdge = ((u * nrm.X + v * nrm.Y).Normalized(), cFace, Rank(o.Def.Role), removed, o);
                                crossN2 = nrm;
                            }
                            if (outline.Count >= 3 && Math.Abs(MeshBuilder.SignedArea(outline)) < before - 1e-6) Info($"Truss: bracing plate at {where} stops {L(gap)} clear of {o.Mark} (crosses the plate, not connected to it){Src}.");
                        }
                    }
                    // ProbeFaces: the crossing member's face where the plate edge / edge angle meets it, from its built parts (the section box includes partial
                    // cover plates that stop short of the member end)
                    if (bg.ProbeFaces && bg.EdgeAngleOnCrossing && crossEdge is { } cp && outline.Count >= 3)
                    {
                        var e3 = Vec3.Cross(cp.inw, n).Normalized();
                        var e2p = new Vec2(Vec3.Dot(e3, u), Vec3.Dot(e3, v));
                        double zl = sides.Min(sd => ZOf(sd).z0), zh = sides.Max(sd => ZOf(sd).z1);
                        string pre = cp.o.Mark + "-";
                        if (ProbeFace(P, cp.inw, cp.c, e3, outline.Min(q => q.X * e2p.X + q.Y * e2p.Y), outline.Max(q => q.X * e2p.X + q.Y * e2p.Y), n, zl - In(4), zh + In(4),
                                pp => pp.Member == cp.o.Mark || pp.Mark.StartsWith(pre, StringComparison.OrdinalIgnoreCase)
                                    || pp.Mark.StartsWith($"{pl}-BG{ptName}-", StringComparison.OrdinalIgnoreCase)) is { } pc)
                        {
                            // (plates / fills of other bracing plates at the same point lie on the crossing member: the angle sits on them)
                            if (pc + bg.CrossingFaceOffset > cp.c + 1e-4) outline = ClipHalf(outline, crossN2, pc + bg.CrossingFaceOffset + gap);
                            if (Math.Abs(pc + bg.CrossingFaceOffset - cp.c) > In(1.0 / 32)) { nProbed++; probedMax = Math.Max(probedMax, Math.Abs(pc - cp.c)); }
                            crossEdge = (cp.inw, pc + bg.CrossingFaceOffset, cp.rank, cp.area, cp.o);
                        }
                    }
                    // the edge angle lies on the crossing member: the plate reaches down to Gap from its face (its outline projected onto that line is added)
                    if (bg.EdgeAngleOnCrossing && crossEdge is { } ce && outline.Count >= 3)
                    {
                        double lim = ce.c + gap;
                        double dmin = outline.Min(q => q.X * crossN2.X + q.Y * crossN2.Y);
                        if (dmin > lim + 1e-4)
                        {
                            // only the part of the outline next to the face is carried down (the far lap corners of a V would widen the base)
                            var ext = outline.Concat(outline.Where(q => q.X * crossN2.X + q.Y * crossN2.Y < dmin + 0.15)
                                .Select(q => q - crossN2 * (q.X * crossN2.X + q.Y * crossN2.Y - lim))).ToList();
                            outline = MeshBuilder.ConvexHull(ext);
                        }
                    }
                    // ProbeFaces: the stack face under the edge angle from the built parts (gusset / chord faces where the plate meets the stack); the plate is
                    // clipped / carried to Gap from that face
                    double? stackH = null;
                    if (bg.ProbeFaces && bg.EdgeAngle.Length > 0 && inPlane.Count > 0 && !bg.NoStackClip && outline.Count >= 3 && (!bg.EdgeAngleOnCrossing || bg.EdgeAngleAlsoOnStack))
                    {
                        var n2 = new Vec2(Vec3.Dot(nT, u), Vec3.Dot(nT, v)) * sIn;
                        var e3 = Vec3.Cross(nT, n);
                        if (n2.Length > 0.2 && e3.Length > 0.2)
                        {
                            e3 = e3.Normalized();
                            var e2p = new Vec2(Vec3.Dot(e3, u), Vec3.Dot(e3, v));
                            double zl = sides.Min(sd => ZOf(sd).z0), zh = sides.Max(sd => ZOf(sd).z1);
                            var ang0 = TrussSections.ParseAngle(bg.EdgeAngle);
                            double leg = ang0?.a ?? In(6);
                            var connMarks = conn.Select(c => c.m.Mark).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            stackH = ProbeFace(P, nT * sIn, H0, e3, outline.Min(q => q.X * e2p.X + q.Y * e2p.Y), outline.Max(q => q.X * e2p.X + q.Y * e2p.Y), n, zl - leg, zh + leg,
                                pp => !pp.Mark.Contains("-BG") && !(pp.Member != null && connMarks.Contains(pp.Member)) && !connMarks.Any(cm => pp.Mark.StartsWith(cm + "-", StringComparison.OrdinalIgnoreCase)));
                            if (stackH is { } hs)
                            {
                                if (Math.Abs(hs - H0) > In(1.0 / 32)) { nProbed++; probedMax = Math.Max(probedMax, Math.Abs(hs - H0)); }
                                var nu = n2 / n2.Length; double lim = (hs + gap) / n2.Length;
                                outline = ClipHalf(outline, nu, lim);
                                if (bg.PlateToStack && outline.Count >= 3)
                                {
                                    double dmin = outline.Min(q => q.X * nu.X + q.Y * nu.Y);
                                    if (dmin > lim + 1e-4)
                                        outline = MeshBuilder.ConvexHull(outline.Concat(outline.Where(q => q.X * nu.X + q.Y * nu.Y < dmin + 0.15).Select(q => q - nu * (q.X * nu.X + q.Y * nu.Y - lim))).ToList());
                                }
                            }
                        }
                    }
                    if (outline.Count < 3 || Math.Abs(MeshBuilder.SignedArea(outline)) < 1e-4) { Warn($"Truss: bracing plate at {where} - no plate area left; not built{Src}."); continue; }
                    if (MeshBuilder.SignedArea(outline) < 0) outline.Reverse();
                    Assumption? asu = bg.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(bg.Unresolved + Src, "plate envelope of the member laps / as listed") : null;
                    if (asu != null) Warn(bg.Unresolved);
                    string asm = conn[0].m.Assembly;
                    string lbl = bg.Label.Length > 0 ? bg.Label : $"PL {TrussSections.FormatIn(bg.Thickness)}";
                    double area = Math.Abs(MeshBuilder.SignedArea(outline));
                    var newMeshes = new List<Mesh>();
                    var plates = new List<(TrussBracingPlateSide side, double z0, double z1)>();
                    // edge angle plate legs on a plate's member-side face (plate uv: keep q . dir >= c): the fills on that face stop clear of them (recheck N07)
                    var angleClips = new List<(TrussBracingPlateSide side, Vec2 dir, double c)>();
                    foreach (var side in sides)
                    {
                        bool below = side == TrussBracingPlateSide.Below;
                        var (z0, z1) = ZOf(side);
                        var mesh = new Mesh();
                        MeshBuilder.AddExtrusion(mesh, outline, new Frame3(P, u, v, n), z0, z1);
                        string mark = $"{pl}-BG{ptName}{bg.Suffix}{(sides.Length > 1 ? (below ? "-B" : "-T") : "")}";
                        parts.Add(new SteelPart { Mark = mark, Category = PartCategory.TrussConnectionPlate, Assembly = asm, Mesh = mesh, Material = Material(bg.Material.Length > 0 ? bg.Material : conn[0].m.Sec.Definition.Material),
                            Length = outline.Max(q => q.X) - outline.Min(q => q.X), Assumption = asu, Description = $"bracing connection {lbl} at {ptName} ({area / 0.09290304:0.0} SF)" });
                        Model.PanelOf[mark] = $"Joint {ptName}";
                        newMeshes.Add(mesh); plates.Add((side, z0, z1)); nPlates++;
                        // edge angle on the truss-plane joint stack face: first leg on the stack face (toward the members), second on the plate's member-side face
                        bool onCross = bg.EdgeAngleOnCrossing && crossEdge != null;
                        if (bg.EdgeAngleOnCrossing && crossEdge == null && bg.EdgeAngle.Length > 0)
                            Warn($"Truss: bracing plate at {where} - no member crosses the plate edge for the edge angle {bg.EdgeAngle}; angle not built{Src}.");
                        // modes: on the crossing member / on the stack; EdgeAngleAlsoOnStack = both (a sway plate under a floor beam beside a truss post)
                        var modes = new List<bool>();
                        if (bg.EdgeAngle.Length > 0 && (onCross || inPlane.Count > 0 && !bg.EdgeAngleOnCrossing)) modes.Add(onCross);
                        if (bg.EdgeAngle.Length > 0 && onCross && bg.EdgeAngleAlsoOnStack && inPlane.Count > 0 && !bg.NoStackClip) modes.Add(false);
                        for (int mi = 0; mi < modes.Count; mi++)
                        {
                            onCross = modes[mi];
                            var ang = TrussSections.ParseAngle(bg.EdgeAngle);
                            if (ang == null && SteelCatalog.FindAngle(bg.EdgeAngle) is { } ac) ang = (ac.LegA, ac.LegB, ac.Thickness);
                            if (ang is not { } a) { Warn($"Truss: bracing plate at {where} - edge angle '{bg.EdgeAngle}' not understood; not built{Src}."); continue; }
                            var nFace = onCross ? crossEdge!.Value.inw : nT;
                            double sFace = onCross ? 1 : sIn;
                            double H = onCross ? crossEdge!.Value.c : stackH ?? H0;
                            var e = Vec3.Cross(nFace, n);
                            if (e.Length < 0.2) continue;
                            e = e.Normalized();
                            var inw = nFace * sFace;                              // from the stack / crossing member face toward the bracing
                            bool outer = bg.EdgeAngleOutside;
                            var outN = below != outer ? n : -n;                   // from the plate face the angle lies on, away from the plate
                            double zf = below != outer ? z1 : z0;                 // the plate face the angle lies on (n)
                            var e2 = new Vec2(Vec3.Dot(e, u), Vec3.Dot(e, v));
                            double eLo = outline.Min(q => q.X * e2.X + q.Y * e2.Y), eHi = outline.Max(q => q.X * e2.X + q.Y * e2.Y);
                            // second angle on the stack beside the angle on the crossing member: it stops gap clear of that angle's plate leg
                            if (!onCross && modes.Count > 1 && crossEdge is { } cx)
                            {
                                double ec = Vec3.Dot(e, cx.inw);
                                if (Math.Abs(ec) > 0.3)
                                {
                                    double tLim = (cx.c + a.b + gap - H * Vec3.Dot(nFace * sFace, cx.inw) - zf * Vec3.Dot(n, cx.inw)) / ec;
                                    if (ec > 0) eLo = Math.Max(eLo, tLim); else eHi = Math.Min(eHi, tLim);
                                }
                                if (eHi - eLo < In(3)) continue;
                            }
                            var heel = P + inw * H + n * zf + e * eLo;
                            if (!outer)
                            {
                                var iw2 = new Vec2(Vec3.Dot(inw, u), Vec3.Dot(inw, v));
                                if (iw2.Length > 0.3) angleClips.Add((side, iw2 / iw2.Length, (H + a.b + gap) * iw2.Length));
                            }
                            double tA = a.t;
                            var sec = new[] { new Vec2(0, 0), new Vec2(a.b, 0), new Vec2(a.b, tA), new Vec2(tA, tA), new Vec2(tA, a.a), new Vec2(0, a.a) };   // x = along inw, y = along outN
                            var ring = sec.Select(q => heel + inw * q.X + outN * q.Y).ToArray();
                            var amesh = new Mesh();
                            MeshBuilder.AddLoft(amesh, sec, new[] { ring, ring.Select(p => p + e * (eHi - eLo)).ToArray() });
                            string am = mark + (mi == 0 ? "-EA" : "-EA" + (mi + 1));
                            parts.Add(new SteelPart { Mark = am, Category = PartCategory.TrussConnectionAngle, Assembly = asm, Mesh = amesh, Material = Material(bg.Material.Length > 0 ? bg.Material : conn[0].m.Sec.Definition.Material),
                                Length = eHi - eLo, Assumption = asu, Description = $"edge angle {bg.EdgeAngle} x {L(eHi - eLo)} of the bracing plate at {ptName}" });
                            Model.PanelOf[am] = $"Joint {ptName}";
                            newMeshes.Add(amesh); nAngles++;
                            if (bg.EdgeRivets > 0 && bg.RivetDiameter > 0)
                            {
                                double len = eHi - eLo, pitchE = Math.Min(bg.RivetPitch > 0 ? bg.RivetPitch : In(4), (len - 3 * bg.RivetDiameter) / Math.Max(1, bg.EdgeRivets - 1));
                                for (int k = 0; k < bg.EdgeRivets; k++)
                                {
                                    double ea = eLo + len / 2 + (k - (bg.EdgeRivets - 1) / 2.0) * pitchE;
                                    // leg on the stack: shank along nT through the leg and the stack; leg on the plate: shank along n through the leg and the plate
                                    _bracingRivets.Add((P + inw * (H + tA / 2) + n * zf + outN * ((tA + a.a) / 2) + e * ea, inw, bg, asu, where, asm, 1));
                                    _bracingRivets.Add((P + inw * (H + (tA + a.b) / 2) + n * zf + outN * (tA / 2) + e * ea, n, bg, asu, where, asm, 2));
                                }
                            }
                        }
                    }
                    // MemberFills: a fill between each plate and a connected member whose face stands off the plate (the plate lies on the outermost
                    // member face; shallower / tilted members leave a gap), lap rectangle of the member clipped to the plate outline, thickness = the gap
                    // at the member's face nearest the plate over the lap
                    if (bg.MemberFills)
                        foreach (var (side, z0, z1) in plates)
                        {
                            bool below = side == TrussBracingPlateSide.Below;
                            int fi = 0;
                            foreach (var (m, ends, start) in conn)
                            {
                                // tapered (beveled) fill: from the plate face to the member's face plane over the lap (the member may lie in another plane)
                                var toward = below ? -n : n;
                                double yf0 = Vec3.Dot(m.Y, toward) > 0 ? m.Sec.YMax : m.Sec.YMin;
                                // only a FLAT member face gets a fill (a skewed box such as a portal strut has no face plane at its extreme corner)
                                var facePts = m.Sec.Pieces.SelectMany(pc => pc.Polygon).Where(q => Math.Abs(q.Y - yf0) < 0.001).ToList();
                                if (facePts.Count < 2 || facePts.Max(q => q.X) - facePts.Min(q => q.X) < 0.5 * (m.Sec.XMax - m.Sec.XMin)) continue;
                                double yf = yf0 + m.Def.OffsetZ;
                                var F0 = m.A + m.Y * yf;
                                double den = Vec3.Dot(n, m.Y);
                                if (Math.Abs(den) < 0.5) continue;
                                double zp = below ? z1 : z0;
                                var dd = ends ? Away(m, start) : m.Axis;
                                var (dp, ac, h) = InPlane(m, dd);
                                double a0 = ends ? (start ? m.SA : m.SB) : -LapOf(m) / 2, a1 = a0 + LapOf(m);
                                var rect = new List<Vec2> { UV(P + dp * a0 - ac * h), UV(P + dp * a1 - ac * h), UV(P + dp * a1 + ac * h), UV(P + dp * a0 + ac * h) };
                                if (MeshBuilder.SignedArea(rect) < 0) rect.Reverse();
                                for (int k = 0; k < outline.Count && rect.Count >= 3; k++)
                                {
                                    var e0 = outline[k]; var e1 = outline[(k + 1) % outline.Count];
                                    var nn = new Vec2(-(e1.Y - e0.Y), e1.X - e0.X); double ln = nn.Length; if (ln < 1e-9) continue; nn = nn / ln;
                                    rect = ClipHalf(rect, nn, e0.X * nn.X + e0.Y * nn.Y);
                                }
                                foreach (var (cs, cd, cc) in angleClips)
                                    if (cs == side && rect.Count >= 3) rect = ClipHalf(rect, cd, cc);
                                if (rect.Count < 3 || Math.Abs(MeshBuilder.SignedArea(rect)) < 1e-4) continue;
                                var top = rect.Select(q => P + u * q.X + v * q.Y + n * zp).ToArray();
                                var gaps = top.Select(Q => Vec3.Dot(F0 - Q, m.Y) / den * (below ? 1 : -1)).ToArray();
                                double gmax = gaps.Max(), gmin = gaps.Min();
                                if (gmax < In(1.0 / 16)) continue;
                                var bot = top.Select((Q, k) => Q + n * ((below ? 1 : -1) * Math.Max(gaps[k], 0.0005))).ToArray();
                                var fmesh = new Mesh();
                                MeshBuilder.AddLoft(fmesh, rect.ToArray(), new[] { top, bot });
                                string fm = $"{pl}-BG{ptName}{bg.Suffix}{(sides.Length > 1 ? (below ? "-B" : "-T") : "")}-F{++fi}";
                                string th = gmax - Math.Max(0, gmin) < In(1.0 / 32) ? L(gmax) : $"{L(Math.Max(0, gmin))} - {L(gmax)} (beveled)";
                                parts.Add(new SteelPart { Mark = fm, Category = PartCategory.FillerPlate, Assembly = asm, Mesh = fmesh, Material = Material(bg.Material.Length > 0 ? bg.Material : conn[0].m.Sec.Definition.Material),
                                    Length = LapOf(m), Assumption = asu, Description = $"fill {th} between the bracing plate at {ptName} and {m.Mark} (plate in the plane of the connected members)" });
                                Model.PanelOf[fm] = $"Joint {ptName}";
                                nFills++; fillMax = Math.Max(fillMax, gmax);
                            }
                        }
                    // member rivets (per plate)
                    if (bg.RivetDiameter > 0)
                        foreach (var (m, ends, start) in conn)
                        {
                            int cnt = CountOf(m);
                            if (cnt <= 0) continue;
                            var (dp, ac, h) = InPlane(m, ends ? Away(m, start) : m.Axis);
                            double g1 = bg.RivetGage > 0 ? bg.RivetGage : Math.Max(In(1.5), h / 2), g2 = bg.RivetGage2;
                            var gages = g2 > 0 ? new[] { g2, -g2, g1, -g1 } : new[] { g1, -g1 };
                            // RivetGageEdge: the lines spread evenly across the member's own width at the plate, the outer ones this far from its sides
                            if (bg.RivetGageEdge > 0)
                            {
                                int nl = g2 > 0 ? 4 : 2; double gm = Math.Max(0, h - bg.RivetGageEdge);
                                var gl = Enumerable.Range(0, nl).Select(k => -gm + 2 * gm * k / (nl - 1)).ToList();
                                gages = gl.OrderByDescending(x => Math.Abs(x)).ThenBy(x => x).ToArray();
                            }
                            int perLine = (int)Math.Ceiling(cnt / (double)gages.Length);
                            double s0 = ends ? (start ? m.SA : m.SB) + 1.5 * bg.RivetDiameter : -(perLine - 1) * pitch / 2;
                            int placed = 0;
                            for (int k = 0; k < perLine && placed < cnt; k++)
                                foreach (double gg in gages)
                                {
                                    if (placed >= cnt) break;
                                    var q = P + (ends ? Away(m, start) : m.Axis) * (s0 + k * pitch) + ac * gg;
                                    q -= n * Vec3.Dot(q - P, n);   // onto the plate plane through the point
                                    var q2 = UV(q);
                                    if (EdgeDistance(outline, q2) < 1.25 * bg.RivetDiameter) { nOut++; placed++; continue; }
                                    foreach (var (side, z0, z1) in plates)
                                        _bracingRivets.Add((q + n * ((z0 + z1) / 2), side == TrussBracingPlateSide.Below ? n : -n, bg, asu, where, asm, 0));
                                    placed++;
                                }
                        }
                }
            }
            // rivets: shank along the given direction through the contiguous steel at the point (plate + member face / angle leg + stack)
            if (_bracingRivets.Count > 0)
            {
                var boxes = parts.Select(p => p.Mesh is { IsEmpty: false } ? p.Mesh.Bounds() : default).ToList();
                foreach (var grp in _bracingRivets.GroupBy(r => r.where))
                {
                    var l = grp.ToList();
                    var lo = new Vec3(l.Min(r => r.p.X) - 0.6, l.Min(r => r.p.Y) - 0.6, l.Min(r => r.p.Z) - 0.6);
                    var hi = new Vec3(l.Max(r => r.p.X) + 0.6, l.Max(r => r.p.Y) + 0.6, l.Max(r => r.p.Z) + 0.6);
                    var pr = new SteelLineProbe();
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var b = boxes[i];
                        if (parts[i].Mesh is not { IsEmpty: false } || b.Max.X < lo.X || b.Min.X > hi.X || b.Max.Y < lo.Y || b.Min.Y > hi.Y || b.Max.Z < lo.Z || b.Min.Z > hi.Z) continue;
                        pr.Add(parts[i].Mesh, i, lo, hi);
                    }
                    foreach (var (p, dir, bg, asu, where, asmR, rk) in l)
                    {
                        if (pr.At(p, dir, 0.6) is not { } iv) { nNoSteel++; if (missed.Count < 3) missed.Add(where); continue; }
                        // RivetGapTolerance: the grip continues (toward the members) across air gaps up to the tolerance - a flat plate over members whose
                        // planes differ slightly (kinked lateral panels) is drawn up by the rivet; the gaps bridged are reported
                        if (bg.RivetGapTolerance > 0)
                        {
                            double t1 = iv.t1, gsum = 0;
                            foreach (var nx in pr.Intervals(p, dir, 0.6).Where(x => x.t0 > t1 - 1e-9).OrderBy(x => x.t0))
                            {
                                double gp = nx.t0 - t1;
                                if (gp > bg.RivetGapTolerance) break;
                                if (gp > 1e-5) gsum += gp;
                                t1 = Math.Max(t1, nx.t1);
                            }
                            if (gsum > 1e-5) { nBridged++; bridgedMax = Math.Max(bridgedMax, gsum); }
                            iv = (iv.t0, t1);
                        }
                        // ProbeFaces: a rivet position whose steel is one part only (nothing behind the plate / angle leg it starts in) is not placed
                        if (bg.ProbeFaces && iv.t1 - iv.t0 <= pr.OnePartThickness(p, dir) + In(1.0 / 32)) { if (loneEx.Count < 8 && rk == 0) { var nx = pr.Intervals(p, dir, 0.6).FirstOrDefault(x => x.t0 > iv.t1 + 1e-6); loneEx.Add($"{where} k{rk} next steel {(nx.t1 > 0 ? L(nx.t0 - iv.t1) : "none")}"); } nLone++; loneKind[rk]++; loneAt[$"{where}#{rk}"] = loneAt.GetValueOrDefault($"{where}#{rk}") + 1; continue; }
                        var rasu = RivetFlag(bg.Unresolved.Length > 0 ? bg.Unresolved : null, "bracing connection rivets at the drawn count, pitch / gage scaled") ?? asu;
                        _pendingConn.Add((p + dir * iv.t1, -dir, Math.Abs(dir.Z) > 0.9 ? Tangent(StationOf(p)) : Up, iv.t1 - iv.t0, bg.RivetDiameter, asmR, bg.RivetMaterial, rasu));
                        nRiv++;
                    }
                }
                _bracingRivets.Clear();
            }
            if (nBridged > 0) Warn($"Truss CHECK bracing rivets: {nBridged} rivets grip across air gaps up to the bracing plate's gap tolerance (largest {L(bridgedMax)}; plate and member planes differ slightly - drawn up by the rivet){Src}.");
            if (nProbed > 0) Info($"Truss: bracing plate edges / edge angles set on the probed member faces at {nProbed} places (largest move {L(probedMax)} from the section box){Src}.");
            if (nLone > 0) Warn($"Truss CHECK bracing rivets: {nLone} rivet positions pass one part only ({string.Join("; ", loneEx)}) (no member / stack behind the plate or angle leg there: member rivets {loneKind[0]}, angle leg on the stack / crossing member {loneKind[1]}, angle leg on the plate {loneKind[2]}; most at {string.Join(", ", loneAt.OrderByDescending(k => k.Value).Take(3).Select(k => k.Key + " " + k.Value))}); not placed{Src}.");
            if (nToStack > 0 || nFills > 0) Info($"Truss: bracing plates carried to the joint stack under the edge angle {nToStack}, fills between plate and member {nFills} (largest {L(fillMax)}){Src}.");
            Info($"Truss: bracing connection plates - {nPlates} plates, {nAngles} edge angles, {nRiv} rivets{(nOut > 0 ? $" ({nOut} rivet positions within 1 1/4 d of the plate edge, not placed)" : "")}{(nNoSteel > 0 ? $" ({nNoSteel} rivet positions not in steel, skipped; e.g. {string.Join("; ", missed)})" : "")}{Src}.");
        }

        private readonly List<(Vec3 p, Vec3 dir, TrussBracingGussetDefinition bg, Assumption? asu, string where, string asm, int kind)> _bracingRivets = new();

        /// <summary>Face of the steel (parts passing <paramref name="filter"/>) seen from the plate side along -<paramref name="inw"/>: the largest distance
        /// along <paramref name="inw"/> from <paramref name="P"/> at which a ray started 0.15 m beyond <paramref name="c"/> meets steel, over a 5 x 5 grid on
        /// the edge line (e) x plate normal (n) ranges; null = no steel met within 0.6 m.</summary>
        private double? ProbeFace(Vec3 P, Vec3 inw, double c, Vec3 e, double e0, double e1, Vec3 n, double n0, double n1, Func<SteelPart, bool> filter)
        {
            var parts = Model.Steel.Parts;
            var pts = new List<Vec3>();
            for (int i = 0; i < 5; i++) for (int j = 0; j < 5; j++) pts.Add(P + inw * (c + 0.15) + e * (e0 + (e1 - e0) * (i + 0.5) / 5) + n * (n0 + (n1 - n0) * j / 4));
            var lo = new Vec3(pts.Min(q => q.X) - 0.8, pts.Min(q => q.Y) - 0.8, pts.Min(q => q.Z) - 0.8);
            var hi = new Vec3(pts.Max(q => q.X) + 0.8, pts.Max(q => q.Y) + 0.8, pts.Max(q => q.Z) + 0.8);
            var pr = new SteelLineProbe();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Mesh is not { IsEmpty: false } mm || !filter(parts[i])) continue;
                var b = mm.Bounds();
                if (b.Max.X < lo.X || b.Min.X > hi.X || b.Max.Y < lo.Y || b.Min.Y > hi.Y || b.Max.Z < lo.Z || b.Min.Z > hi.Z) continue;
                pr.Add(mm, i, lo, hi);
            }
            if (pr.Count == 0) return null;
            double? best = null;
            foreach (var S in pts)
                foreach (var iv in pr.Intervals(S, -inw, 0.6))
                {
                    if (iv.t1 <= 0) continue;
                    double f = c + 0.15 - Math.Max(0, iv.t0);
                    if (best == null || f > best) best = f;
                    break;
                }
            return best;
        }

        /// <summary>Keeps the part of a convex polygon with p . nrm >= c.</summary>
        private static List<Vec2> ClipHalf(List<Vec2> poly, Vec2 nrm, double c)
        {
            var output = new List<Vec2>();
            for (int k = 0; k < poly.Count; k++)
            {
                var p = poly[k]; var q = poly[(k + 1) % poly.Count];
                double sp = p.X * nrm.X + p.Y * nrm.Y - c, sq = q.X * nrm.X + q.Y * nrm.Y - c;
                if (sp >= 0) output.Add(p);
                if ((sp >= 0) != (sq >= 0)) { double t = sp / (sp - sq); output.Add(new Vec2(p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t)); }
            }
            return output;
        }
    }
}
