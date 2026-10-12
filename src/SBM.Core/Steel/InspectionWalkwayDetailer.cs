using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Inspection walkways in the girder bays (I-80 design sheets 165-170 = PDF 169-174, Ramp G PDF 135-136): grating on two stringers,
/// diaphragms, rail posts, top / intermediate rails, toe boards. Built AFTER the connections phase: with
/// <see cref="InspectionAccessDefinition.SupportOnFrames"/> the segment ends are read from the generated cross frames / diaphragms /
/// cross girders (strut top, web faces, members in the rail zone). Legacy (null): grating at a fixed height over the bottom of web,
/// segments a fixed distance short of each frame line.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private const string WalkwayMaterial = "ASTM A709 Gr. 50, galvanized";

    /// <summary>One end of a walkway segment, in plan about the point C where its support line (frame line / cross girder web CL) crosses
    /// the walkway centerline.</summary>
    private sealed class WalkEnd
    {
        public Vec2 C;
        /// <summary>Plan direction of the support line (grating ends are cut parallel to it).</summary>
        public Vec2 PlaneDir;
        /// <summary>C to the stringer end, along the walkway (plan).</summary>
        public double StringerU;
        /// <summary>Gap from the support line to the grating end (perpendicular); null = the grating ends square with the stringers.</summary>
        public double? GratingGap;
        /// <summary>Top of grating at the stringer end.</summary>
        public double GratingTop;
        /// <summary>C to the end rail post / rail ends at least (plan).</summary>
        public double RailU;
        /// <summary>Assembly whose parts the rails, posts and toe boards keep clear of (null = none).</summary>
        public string? Occupant;
        /// <summary>C to the heel of an extra end diaphragm (null = none).</summary>
        public double? EndDiaU;
    }

    private void BuildWalkways(InspectionAccessDefinition ia)
    {
        if (ia.DefaultWalkway == WalkwayType.None && ia.Walkways.Count == 0) return;
        bool asDesigned = ia.SupportOnFrames == true;
        double D = Sec.WebDepth;
        var byAsm = asDesigned
            ? _parts.Where(p => p.Category != PartCategory.Weld).GroupBy(p => p.Assembly).ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<string, List<SteelPart>>();
        // Legacy support point: the middle of each cross frame line between the girders, at the grating level.
        Vec3 Mid(Vec3 l, Vec3 r) => (l + r) * 0.5 + Up * (-D + ia.GratingAboveWebBottom);
        var jobs = new List<Action>();
        var pairs = _f.CrossFrames.GroupBy(cf => (cf.Bay, cf.RightGirder)).OrderBy(g => g.Key.Bay).ThenBy(g => g.Key.RightGirder).ToList();
        WalkwaySectionDefinition SecOf(WalkwayType t) => t == WalkwayType.A ? ia.TypeA : ia.TypeB;

        WalkEnd FrameEnd(CrossFrameResult cf, Vec2 into, Vec3 legacyMid, Vec3 legacyOther, WalkwayType type, string mark)
        {
            var c = legacyMid.XY;
            double Dp = (legacyOther.XY - c).Length;
            if (!asDesigned)
                return new WalkEnd { C = c, PlaneDir = into.Left, StringerU = ia.WalkwayEndClearance, RailU = ia.WalkwayEndClearance,
                    GratingTop = legacyMid.Z + (legacyOther.Z - legacyMid.Z) * ia.WalkwayEndClearance / Math.Max(1e-9, Dp) };
            var r = AsDesignedFrameEnd(ia, SecOf(type), cf, c, into, byAsm, mark);
            if (r != null) return r;
            return new WalkEnd { C = c, PlaneDir = cf.Direction, StringerU = ia.WalkwayEndClearance, RailU = ia.WalkwayEndClearance,
                GratingTop = legacyMid.Z, Occupant = cf.Mark };
        }

        foreach (var grp in pairs)
        {
            var def = ia.Walkways.FirstOrDefault(w => w.Bay == grp.Key.Bay && (w.RightGirder < 0 || w.RightGirder == grp.Key.RightGirder));
            var type = def?.Type ?? ia.DefaultWalkway;
            if (type == WalkwayType.None) continue;
            var frames = grp.OrderBy(cf => cf.LeftGlobalDistance).ToList();
            int i0 = Math.Clamp(def?.FirstLine ?? 0, 0, frames.Count - 1);
            int i1 = def == null || def.LastLine < 0 ? frames.Count - 1 : Math.Min(def.LastLine, frames.Count - 1);
            string bayName = $"{(char)('A' + grp.Key.Bay)}{(char)('A' + grp.Key.RightGirder)}";
            for (int k = i0; k < i1; k++)
            {
                var fa = frames[k]; var fb = frames[k + 1];
                string m = $"IW-{bayName}-{k + 1}";
                int left = grp.Key.Bay, right = grp.Key.RightGirder;
                var tp = type;
                jobs.Add(() =>
                {
                    var a = Mid(fa.Left, fa.Right); var b = Mid(fb.Left, fb.Right);
                    var e = (b.XY - a.XY).Normalized();
                    var ea = FrameEnd(fa, e, a, b, tp, m);
                    var eb = FrameEnd(fb, -e, b, a, tp, m);
                    BuildWalkwaySegment(ia, tp, ea, eb, m, left, right, byAsm);
                });
            }
        }
        // Bays interrupted by a cross girder: the walkway ends at the cross girder web on the near side (between each continuing girder and the
        // discontinuous girder, centred on the web openings) and starts again on the far side between the continuing girders (centred on the
        // discontinuous girder line when as designed: I-80 sheet 168 Section A-A "CL GIRDER E (DISCONTINUOUS) & CL WALKWAY (F.S.)").
        foreach (var x in _f.CrossGirders)
        {
            var xgp = _def.Girders.CrossGirder;
            var (dz, tc) = CrossGirderShift(x);
            var (ca, cb) = CrossGirderChord(x);
            Vec3 OnXg(double frac)
            {
                var q = Vec3.Lerp(ca, cb, frac);
                return new Vec3(q.X, q.Y, q.Z - xgp.FlangeThickness - xgp.WebDepth);   // bottom of the cross girder web
            }
            var pts = new (int l, int r, double frac, bool near)[]
            {
                (x.LeftGirder, x.Girder, tc / 2, true),
                (x.Girder, x.RightGirder, (tc + 1) / 2, true),
                (x.LeftGirder, x.RightGirder, asDesigned ? tc : 0.5, false),
            };
            foreach (var (l, r, frac, near) in pts)
            {
                var grp = pairs.FirstOrDefault(g => g.Key.Bay == l && g.Key.RightGirder == r);
                if (grp == null) continue;
                var def = ia.Walkways.FirstOrDefault(w => w.Bay == l && (w.RightGirder < 0 || w.RightGirder == r));
                var type = def?.Type ?? ia.DefaultWalkway;
                if (type == WalkwayType.None) continue;
                var frames = grp.OrderBy(cf => cf.LeftGlobalDistance).ToList();
                // distance on the bay's LEFT girder where the cross girder is (the discontinuous girder's end for the (disc, right) bay)
                double sRef = l == x.Girder ? _f.Girders[x.Girder].Length : x.LeftGirderDistance;
                var fr = near ? frames.LastOrDefault(cf => cf.LeftGlobalDistance < sRef) : frames.FirstOrDefault(cf => cf.LeftGlobalDistance > sRef);
                if (fr == null) continue;
                string m = $"IW-{(char)('A' + l)}{(char)('A' + r)}-X{x.Girder + 1}{(near ? "n" : "f")}";
                int ll = l, rr2 = r;
                var tp = type;
                var webBottom = OnXg(frac);
                jobs.Add(() =>
                {
                    var sec = SecOf(tp);
                    var mid = Mid(fr.Left, fr.Right);
                    var xgPoint = webBottom + Up * (asDesigned && sec.GratingAboveCrossGirderWebBottom is double g0 ? g0 : ia.GratingAboveWebBottom);
                    var e = (xgPoint.XY - mid.XY).Normalized();                    // frame toward the cross girder
                    var fe = FrameEnd(fr, near ? e : -e, mid, xgPoint, tp, m);
                    var xe = XgEnd(ia, sec, x, xgPoint, near ? -e : e, asDesigned);
                    if (near) BuildWalkwaySegment(ia, tp, fe, xe, m, ll, rr2, byAsm);
                    else BuildWalkwaySegment(ia, tp, xe, fe, m, ll, rr2, byAsm);
                });
            }
        }
        Parallel.ForEach(jobs, j => j());
    }

    /// <summary>Walkway end at a cross girder web: C on the web CL line, grating top given (<paramref name="gratingTop"/>).</summary>
    private WalkEnd XgEnd(InspectionAccessDefinition ia, WalkwaySectionDefinition sec, CrossGirderResult x, Vec3 gratingTop, Vec2 into, bool asDesigned)
    {
        var xgp = _def.Girders.CrossGirder;
        double tw2 = xgp.WebThickness / 2;
        if (!asDesigned)
        {
            double clear = tw2 + Inch(1);
            return new WalkEnd { C = gratingTop.XY, PlaneDir = x.Direction, StringerU = clear, RailU = clear, GratingTop = gratingTop.Z };
        }
        double su = tw2 + ia.StringerEndFromWeb;
        return new WalkEnd
        {
            C = gratingTop.XY, PlaneDir = x.Direction, StringerU = su, GratingGap = tw2 + ia.GratingClearOfWeb, GratingTop = gratingTop.Z,
            RailU = sec.RailEndFromCrossGirderWeb is double re ? tw2 + re : su, Occupant = x.Mark,
            EndDiaU = sec.EndDiaphragmClearOfCrossGirderFlange is double c ? xgp.FlangeWidth / 2 + c : su,
        };
    }

    /// <summary>As-designed walkway end at a cross frame / diaphragm (null = not readable: UNRESOLVED warning raised).</summary>
    private WalkEnd? AsDesignedFrameEnd(InspectionAccessDefinition ia, WalkwaySectionDefinition sec, CrossFrameResult cf, Vec2 c, Vec2 into,
        Dictionary<string, List<SteelPart>> byAsm, string mark)
    {
        string src = ia.Source ?? "inspection walkway details";
        if (!byAsm.TryGetValue(cf.Mark, out var parts)) { Warn(UnresolvedFlag.Text(src, $"{mark}: walkway end at {cf.Mark}", "the frame has no generated parts to support the walkway", "legacy end used")); return null; }
        var style = _def.CrossFrames.Styles.FirstOrDefault(s => s.Name == cf.Style);
        var n = into.Left;
        double W = sec.ClearWidth;
        double stack = StringerStack(sec);
        double Hw = Inch(12), step = Inch(1.0 / 8);
        // footprint along the walkway (u, + into the segment) of a set of meshes at the stringer lines and the CL; top z of the hits
        (double uMax, double zTop, int hits) Footprint(IEnumerable<SteelPart> ps)
        {
            var tris = NearTriangles(ps, c, Hw + W);
            double uMax = double.NegativeInfinity, zTop = double.NegativeInfinity; int hits = 0;
            foreach (double v in new[] { -W / 2, 0, W / 2 })
                for (double u = -Hw; u <= Hw + 1e-9; u += step)
                {
                    var p = c + into * u + n * v;
                    foreach (double z in VerticalHits(tris, p))
                    {
                        hits++;
                        if (u > uMax) uMax = u;
                        if (Math.Abs(v) > 1e-9 || true) zTop = Math.Max(zTop, z);
                    }
                }
            return (uMax, zTop, hits);
        }
        if (style?.Kind == CrossFrameStyleKind.PlateDiaphragm)
        {
            if (ia.GratingAboveDiaphragmFlange == null || !ia.GratingAboveDiaphragmFlange.TryGetValue(style.Name, out double above))
            {
                Warn(UnresolvedFlag.Text(src, $"{mark}: walkway elevation at plate diaphragm {cf.Mark} (Type {style.Name})",
                    "no grating height over this diaphragm type is given", "legacy end used"));
                return null;
            }
            var flanges = parts.Where(p => p.Category == PartCategory.FlangePlate).ToList();
            var zs = VerticalHits(NearTriangles(flanges, c, Inch(6)), c).OrderBy(z => z).ToList();
            var (uWeb, _, hw) = Footprint(parts.Where(p => p.Category == PartCategory.WebPlate));
            if (zs.Count < 2 || hw == 0)
            {
                Warn(UnresolvedFlag.Text(src, $"{mark}: walkway end at plate diaphragm {cf.Mark}", "its bottom flange / web is not found at the walkway", "legacy end used"));
                return null;
            }
            double su = uWeb + step / 2 + ia.StringerEndFromWeb;
            return new WalkEnd { C = c, PlaneDir = cf.Direction, StringerU = su, GratingTop = zs[1] + above, RailU = su, Occupant = cf.Mark };
        }
        var chord = parts.Where(p => p.Role == MemberRole.BottomChord).ToList();
        var (uS, zS, hs) = Footprint(chord);
        if (chord.Count == 0 || hs == 0)
        {
            Warn(UnresolvedFlag.Text(src, $"{mark}: walkway end at {cf.Mark}", "the frame has no bottom strut under the walkway to bear on", "legacy end used"));
            return null;
        }
        double sU = uS + step / 2 + ia.StringerEndFromFrame;
        return new WalkEnd { C = c, PlaneDir = cf.Direction, StringerU = sU, GratingGap = ia.GratingGapAtFrame, GratingTop = zS + stack, RailU = sU, Occupant = cf.Mark };
    }

    /// <summary>Stringer bottom to top of grating.</summary>
    private double StringerStack(WalkwaySectionDefinition sec)
    {
        if (sec.Stringer.StartsWith("C", StringComparison.OrdinalIgnoreCase) || sec.Stringer.StartsWith("MC", StringComparison.OrdinalIgnoreCase))
            return (SteelCatalog.FindChannel(sec.Stringer)?.Depth ?? 0) + sec.GratingThickness;
        return (SteelCatalog.FindAngle(sec.Stringer)?.Thickness ?? 0) + sec.GratingThickness;
    }

    private static List<(Vec3 a, Vec3 b, Vec3 c)> NearTriangles(IEnumerable<SteelPart> parts, Vec2 p, double r)
    {
        var res = new List<(Vec3, Vec3, Vec3)>();
        foreach (var part in parts)
        {
            var pos = part.Mesh.Positions; var idx = part.Mesh.Indices;
            for (int i = 0; i + 2 < idx.Count; i += 3)
            {
                var a = pos[idx[i]]; var b = pos[idx[i + 1]]; var cc = pos[idx[i + 2]];
                if (Math.Max(a.X, Math.Max(b.X, cc.X)) < p.X - r || Math.Min(a.X, Math.Min(b.X, cc.X)) > p.X + r) continue;
                if (Math.Max(a.Y, Math.Max(b.Y, cc.Y)) < p.Y - r || Math.Min(a.Y, Math.Min(b.Y, cc.Y)) > p.Y + r) continue;
                res.Add((a, b, cc));
            }
        }
        return res;
    }

    /// <summary>Elevations where the vertical line through plan point p crosses the triangles.</summary>
    private static IEnumerable<double> VerticalHits(List<(Vec3 a, Vec3 b, Vec3 c)> tris, Vec2 p)
    {
        foreach (var (a, b, c) in tris)
        {
            double x1 = a.X - p.X, y1 = a.Y - p.Y, x2 = b.X - p.X, y2 = b.Y - p.Y, x3 = c.X - p.X, y3 = c.Y - p.Y;
            double area = (x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1);
            if (Math.Abs(area) < 1e-12) continue;
            double w1 = (x2 * y3 - x3 * y2) / area, w2 = (x3 * y1 - x1 * y3) / area, w3 = 1 - w1 - w2;
            if (w1 < -1e-9 || w2 < -1e-9 || w3 < -1e-9) continue;
            yield return w1 * a.Z + w2 * b.Z + w3 * c.Z;
        }
    }

    private void BuildWalkwaySegment(InspectionAccessDefinition ia, WalkwayType type, WalkEnd A, WalkEnd B, string mark, int leftGirder, int rightGirder,
        Dictionary<string, List<SteelPart>> byAsm)
    {
        var sec = type == WalkwayType.A ? ia.TypeA : ia.TypeB;
        var dp = B.C - A.C;
        double Dp = dp.Length;
        if (Dp < 1e-3) return;
        var e = dp * (1 / Dp);
        var nPlan = e.Left;
        double uA = A.StringerU, uB = Dp - B.StringerU;
        if (uB - uA < UnitsFt(2)) { Warn($"{mark}: walkway segment of {In(uB - uA)} between its supports is too short; skipped."); return; }
        double slope = (B.GratingTop - A.GratingTop) / (uB - uA);
        double k = Math.Sqrt(1 + slope * slope);
        var axis = new Vec3(e.X, e.Y, slope).Normalized();
        var yh = new Vec3(nPlan.X, nPlan.Y, 0);                               // left of travel, horizontal (grating is level across)
        var zz = Vec3.Cross(axis, yh);                                       // up, tilted with the grade
        var O = A.C.WithZ(A.GratingTop - slope * uA);                        // top of grating on the CL at plan u = 0 (point C of end A)
        Vec3 Wd(double s, double v, double h) => O + axis * s + yh * v + zz * h;
        double sA = uA * k, sB = uB * k, Ls = sB - sA;
        double W = sec.ClearWidth, tg = sec.GratingThickness, Wg = sec.GratingWidth ?? W;
        bool typeA = type == WalkwayType.A;

        var stringer = typeA ? (Ls > sec.StringerSpanLimit ? sec.LongStringer : sec.Stringer) : sec.Stringer;
        var chan = typeA ? SteelCatalog.FindChannel(stringer) : null;
        var ang = typeA ? null : SteelCatalog.FindAngle(stringer);
        var postA = SteelCatalog.FindAngle(sec.PostAngle);
        var railA = SteelCatalog.FindAngle(sec.TopRailAngle);
        if ((typeA && chan == null) || (!typeA && ang == null) || postA == null || railA == null)
        {
            Warn($"{mark}: walkway section '{stringer}' / '{sec.PostAngle}' / '{sec.TopRailAngle}' not found in the AISC catalog; walkway skipped.");
            return;
        }
        string asm = mark;
        _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.InspectionWalkway, Girder = leftGirder,
            Description = $"Inspection walkway Type {type}, bay G{leftGirder + 1}–G{rightGirder + 1}, {In(Ls)} long" });

        double zStrBot = typeA ? -tg - chan!.Depth : -tg - ang!.Thickness;
        double diaT = typeA ? sec.DiaphragmPlateThickness : 0;
        var diaAngle = typeA ? null : SteelCatalog.FindAngle(sec.DiaphragmAngle);
        double zDiaTop = zStrBot;
        double zDiaBot = typeA ? zDiaTop - diaT : zDiaTop - (diaAngle?.LegA ?? Inch(3.5));
        double yOutDia = W / 2 + postA.LegA;
        double tPost = postA.Thickness, tRail = railA.Thickness;
        double zTop = sec.TopRailHeight;

        // ---- grating: ends parallel to the support lines when a gap is given, else square with the stringers
        double GratingEndU(WalkEnd end, bool atA, double v)
        {
            if (end.GratingGap is not double g) return atA ? uA : uB;
            var nl = end.PlaneDir.Left;
            if (Vec2.Dot(nl, atA ? e : -e) < 0) nl = -nl;                    // support line normal pointing into the segment
            double en = Vec2.Dot(atA ? e : -e, nl);
            if (Math.Abs(en) < 0.2) en = Math.Sign(en == 0 ? 1 : en) * 0.2;
            double off = (g - v * Vec2.Dot(nPlan, nl)) / en;                // distance from C along the walkway (into the segment)
            return atA ? off : Dp - off;
        }
        {
            var outline = new[]
            {
                new Vec2(GratingEndU(A, true, -Wg / 2) * k, -Wg / 2), new Vec2(GratingEndU(B, false, -Wg / 2) * k, -Wg / 2),
                new Vec2(GratingEndU(B, false, Wg / 2) * k, Wg / 2), new Vec2(GratingEndU(A, true, Wg / 2) * k, Wg / 2),
            };
            var gm = new Mesh();
            PlateBuilder.AddPrism(gm, outline, Array.Empty<Hole>(), (u, w, th) => Wd(u, w, th), _ => -tg, _ => 0);
            double gl = (outline[1].X - outline[0].X + outline[2].X - outline[3].X) / 2;
            _parts.Add(new SteelPart { Mark = $"{asm}-GR", Category = PartCategory.Grating, Assembly = asm, Mesh = gm, Length = gl,
                Description = $"Welded grating {In(tg)} deep x {In(Wg)} wide", Material = "Galvanized welded steel grating",
                NominalWeightLbPerFt = sec.GratingWeightLbPerSqFt * (Wg / 0.3048) });
        }

        // ---- stringers
        var o = Wd(sA, 0, 0);
        for (int i = 0; i < 2; i++)
        {
            double s = i == 0 ? -1 : 1;
            var m = new Mesh();
            if (typeA)
            {
                var poly = SteelCatalog.ChannelPolygon(chan!, s * W / 2, -(int)s);
                RolledBuilder.AddExtrusion(m, poly, o + zz * (-tg - chan!.Depth / 2), zz, yh, axis, 0, Ls);
            }
            else
            {
                var poly = SteelCatalog.AnglePolygon(ang!, zStrBot, 1, s * W / 2, -(int)s);
                RolledBuilder.AddExtrusion(m, poly, o, zz, yh, axis, 0, Ls);
            }
            _parts.Add(new SteelPart { Mark = $"{asm}-S{i + 1}", Category = PartCategory.WalkwayStringer, Assembly = asm, Mesh = m, Length = Ls,
                Description = typeA ? chan!.Name : ang!.Name, Material = WalkwayMaterial,
                NominalWeightLbPerFt = typeA ? chan!.WeightPerFootLb : ang!.WeightPerFootLb, Role = MemberRole.Diaphragm });
        }

        // ---- rails / posts / toe boards stop clear of the support members in their zone
        double band = W / 2 + postA.LegB + ia.RailClearOfFrame;
        double toeTop = sec.ToeBoard ? sec.ToeBoardClearance + sec.RailPlateWidth : 0;
        double railTopH = zTop + Inch(1);
        (double railA, double railB, double toeA, double toeB) limits = (A.RailU * k, (Dp - B.RailU) * k, GratingEndU(A, true, 0) * k, GratingEndU(B, false, 0) * k);
        foreach (var (end, atA) in new[] { (A, true), (B, false) })
        {
            if (end.Occupant == null || !byAsm.TryGetValue(end.Occupant, out var occ)) continue;
            double rail = atA ? double.NegativeInfinity : double.PositiveInfinity, toe = rail;
            foreach (var part in occ)
            {
                var pos = part.Mesh.Positions; var idx = part.Mesh.Indices;
                for (int t = 0; t + 2 < idx.Count; t += 3)
                {
                    var p0 = Local(pos[idx[t]]); var p1 = Local(pos[idx[t + 1]]); var p2 = Local(pos[idx[t + 2]]);
                    if (Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)) > band || Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)) < -band) continue;
                    if (Math.Min(p0.Z, Math.Min(p1.Z, p2.Z)) > railTopH || Math.Max(p0.Z, Math.Max(p1.Z, p2.Z)) < 0) continue;
                    foreach (var (q0, q1) in new[] { (p0, p1), (p1, p2), (p2, p0) })
                    {
                        int ns = Math.Max(1, (int)Math.Ceiling((q1 - q0).Length / Inch(0.5)));
                        for (int j = 0; j <= ns; j++)
                        {
                            var q = Vec3.Lerp(q0, q1, (double)j / ns);
                            if (Math.Abs(q.Y) > band || q.Z < 0 || q.Z > railTopH) continue;
                            if (atA ? q.X > Ls / 2 + sA : q.X < Ls / 2 + sA) continue;
                            if (atA) rail = Math.Max(rail, q.X); else rail = Math.Min(rail, q.X);
                            if (q.Z <= toeTop + Inch(0.5) && Math.Abs(q.Y) <= W / 2 + Inch(0.5))
                            { if (atA) toe = Math.Max(toe, q.X); else toe = Math.Min(toe, q.X); }
                        }
                    }
                }
            }
            if (atA)
            {
                if (!double.IsInfinity(rail)) limits.railA = Math.Max(limits.railA, rail + ia.RailClearOfFrame);
                if (!double.IsInfinity(toe)) limits.toeA = Math.Max(limits.toeA, toe + ia.RailClearOfFrame);
            }
            else
            {
                if (!double.IsInfinity(rail)) limits.railB = Math.Min(limits.railB, rail - ia.RailClearOfFrame);
                if (!double.IsInfinity(toe)) limits.toeB = Math.Min(limits.toeB, toe - ia.RailClearOfFrame);
            }
        }
        Vec3 Local(Vec3 q) { var r = q - O; return new Vec3(Vec3.Dot(r, axis), Vec3.Dot(r, yh), Vec3.Dot(r, zz)); }
        double rs = Math.Max(limits.railA, sA), re = Math.Min(limits.railB, sB);
        bool rails = re - rs > UnitsFt(2);
        if (!rails) Warn($"{mark}: no room for rail posts between the supports ({In(Math.Max(0, re - rs))}); rails and posts not modeled.");

        // ---- posts and diaphragms
        var diaPos = new List<(double s, int dir)>();
        var postPos = new List<(double s, int dir)>();
        if (rails)
        {
            int nSp = Math.Max(1, (int)Math.Ceiling((re - rs) / sec.MaxPostSpacing - 1e-9));
            for (int j = 0; j <= nSp; j++)
            {
                double u = rs + j * (re - rs) / nSp;
                int dirU = j * 2 <= nSp ? 1 : -1;
                double origin = j == 0 ? rs : j == nSp ? re : u - dirU * tPost / 2;
                postPos.Add((origin, dirU));
                diaPos.Add((u, 0));
            }
            if (typeA && sec.MidDiaphragmsOnLongSpans && Ls > sec.StringerSpanLimit)
                for (int j = 0; j < nSp; j++) diaPos.Add((rs + (j + 0.5) * (re - rs) / nSp, 0));
        }
        foreach (var (end, atA) in new[] { (A, true), (B, false) })
            if (end.EndDiaU is double du)
            {
                double s0 = atA ? du * k : (Dp - du) * k;
                if (diaPos.All(d => Math.Abs(d.s - s0) > Inch(6))) diaPos.Add((s0, atA ? 1 : -1));
            }
        diaPos.Sort((p, q) => p.s.CompareTo(q.s));
        int dn = 0;
        foreach (var (u, dir) in diaPos)
        {
            var m = new Mesh();
            if (typeA)
            {
                double w = sec.DiaphragmPlateWidth;
                double ud = dir > 0 ? u : dir < 0 ? u - w : u - w / 2;
                ud = Math.Clamp(ud, sA, Math.Max(sA, sB - w));
                MeshBuilder.AddBox(m, new Frame3(O, axis, yh, zz), ud, ud + w, -yOutDia, yOutDia, zDiaBot, zDiaTop);
                _parts.Add(new SteelPart { Mark = $"{asm}-D{++dn}", Category = PartCategory.WalkwayPlate, Assembly = asm, Mesh = m, Length = 2 * yOutDia,
                    Description = $"PL {In(sec.DiaphragmPlateThickness)} x {In(sec.DiaphragmPlateWidth)} diaphragm x {In(2 * yOutDia)}", Material = WalkwayMaterial });
            }
            else
            {
                double leg = diaAngle!.LegA;
                double heel = dir != 0 ? u : u - leg / 2;
                int legDir = dir < 0 ? -1 : 1;
                if (dir == 0) heel = Math.Clamp(heel, sA, Math.Max(sA, sB - leg));
                RolledBuilder.AddAngle(m, Wd(heel, -yOutDia, zDiaTop), yh, axis * legDir, -zz, 2 * yOutDia, diaAngle.LegA, diaAngle.LegB,
                    diaAngle.Thickness, 0, Array.Empty<Hole>(), Array.Empty<Hole>());
                _parts.Add(new SteelPart { Mark = $"{asm}-D{++dn}", Category = PartCategory.Angle, Assembly = asm, Mesh = m, Length = 2 * yOutDia, Role = MemberRole.Diaphragm,
                    Description = $"{diaAngle.Name} diaphragm x {In(2 * yOutDia)}", Material = WalkwayMaterial, NominalWeightLbPerFt = diaAngle.WeightPerFootLb });
            }
        }
        if (!rails) return;
        // rail posts: L3x3x5/16, one leg along the walkway on the post line (outside the clear width), the other outward; foot on the diaphragm
        double zPostBot = zDiaTop, zPostTop = zTop - tRail;
        var postPoly = new[] { new Vec2(0, 0), new Vec2(postA.LegA, 0), new Vec2(postA.LegA, tPost), new Vec2(tPost, tPost), new Vec2(tPost, postA.LegB), new Vec2(0, postA.LegB) };
        int pn = 0;
        foreach (var (u, dirU) in postPos)
            for (int i = 0; i < 2; i++)
            {
                double s = i == 0 ? -1 : 1;
                var m = new Mesh();
                RolledBuilder.AddExtrusion(m, postPoly, Wd(u, s * W / 2, 0), axis * dirU, yh * s, zz, zPostBot, zPostTop);
                _parts.Add(new SteelPart { Mark = $"{asm}-P{++pn}", Category = PartCategory.WalkwayPost, Assembly = asm, Mesh = m, Length = zPostTop - zPostBot,
                    Description = $"{postA.Name} rail post x {In(zPostTop - zPostBot)}", Material = WalkwayMaterial, NominalWeightLbPerFt = postA.WeightPerFootLb });
            }
        // top rails: horizontal leg on the post tops; vertical leg on the inside face of the posts (as designed) or in the post's outer leg plane (legacy)
        bool inside = sec.TopRailInside == true;
        var railPoly = inside
            ? new[] { new Vec2(-tRail, zTop), new Vec2(railA.LegA - tRail, zTop), new Vec2(railA.LegA - tRail, zTop - tRail), new Vec2(0, zTop - tRail),
                new Vec2(0, zTop - railA.LegB), new Vec2(-tRail, zTop - railA.LegB) }
            : new[] { new Vec2(0, zTop), new Vec2(railA.LegA, zTop), new Vec2(railA.LegA, zTop - tRail), new Vec2(tRail, zTop - tRail),
                new Vec2(tRail, zTop - railA.LegB), new Vec2(0, zTop - railA.LegB) };
        var fr = new Frame3(O, axis, yh, zz);
        double ts = Math.Max(limits.toeA, sA - Inch(12)), te = Math.Min(limits.toeB, sB + Inch(12));
        for (int i = 0; i < 2; i++)
        {
            double s = i == 0 ? -1 : 1;
            var m = new Mesh();
            RolledBuilder.AddExtrusion(m, railPoly, O + yh * (s * W / 2), yh * s, zz, axis, rs, re);
            _parts.Add(new SteelPart { Mark = $"{asm}-TR{i + 1}", Category = PartCategory.WalkwayRail, Assembly = asm, Mesh = m, Length = re - rs,
                Description = $"{railA.Name} top rail", Material = WalkwayMaterial, NominalWeightLbPerFt = railA.WeightPerFootLb });
            var im = new Mesh();
            double vi0 = s > 0 ? W / 2 - sec.RailPlateThickness : -W / 2, vi1 = s > 0 ? W / 2 : -W / 2 + sec.RailPlateThickness;
            MeshBuilder.AddBox(im, fr, rs, re, vi0, vi1, sec.IntermediateRailHeight - sec.RailPlateWidth / 2, sec.IntermediateRailHeight + sec.RailPlateWidth / 2);
            _parts.Add(new SteelPart { Mark = $"{asm}-IR{i + 1}", Category = PartCategory.WalkwayRail, Assembly = asm, Mesh = im, Length = re - rs,
                Description = $"PL {In(sec.RailPlateThickness)} x {In(sec.RailPlateWidth)} intermediate rail", Material = WalkwayMaterial });
            if (sec.ToeBoard)
            {
                // toe boards run with the grating (as designed, Section G-G: 1" gap over the strut), clear of the support members
                double t0 = A.GratingGap != null ? ts : rs, t1 = B.GratingGap != null ? te : re;
                if (t1 - t0 < Inch(6)) continue;
                var tb = new Mesh();
                MeshBuilder.AddBox(tb, fr, t0, t1, vi0, vi1, sec.ToeBoardClearance, sec.ToeBoardClearance + sec.RailPlateWidth);
                _parts.Add(new SteelPart { Mark = $"{asm}-TB{i + 1}", Category = PartCategory.WalkwayPlate, Assembly = asm, Mesh = tb, Length = t1 - t0,
                    Description = $"PL {In(sec.RailPlateThickness)} x {In(sec.RailPlateWidth)} toe board", Material = WalkwayMaterial });
            }
        }
    }
}
