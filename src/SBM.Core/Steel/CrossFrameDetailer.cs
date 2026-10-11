using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Cross-frame detailing in the plane of the frame. Plane coordinates are (x, z): x = plan distance from the left
/// girder's web centerline along the frame line; z = "elevation" measured along the plane's up axis from the left
/// girder's top of web (offset by that point's true elevation so values read like elevations). The up axis is plumb
/// for support frames and normal to the flanges for intermediate frames. v = distance normal to the plane. The
/// connection stiffener occupies v ∈ [−tp/2, tp/2]; gussets lap its +v face (v ∈ [tp/2, tp/2 + tg]); angles are
/// welded to the outer (front) gusset face, except the second X diagonal which is on the back face with a filler plate
/// and bolt at the crossing.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private sealed class FramePlane
    {
        public required Vec3 Origin { get; init; }
        public required Vec3 U { get; init; }
        public required Vec3 Wt { get; init; }
        public Vec3 N => Vec3.Cross(Wt, U).Normalized();
        public Vec3 W(Vec2 xz, double v) => Origin + U * xz.X + N * v + Wt * (xz.Y - Origin.Z);
        public Vec3 Dir(Vec2 xz) => U * xz.X + Wt * xz.Y;
        /// <summary>Plane coordinates of a world point (x, z, v).</summary>
        public (Vec2 xz, double v) Local(Vec3 p)
        {
            var d = p - Origin;
            return (new Vec2(Vec3.Dot(d, U), Origin.Z + Vec3.Dot(d, Wt)), Vec3.Dot(d, N));
        }
    }

    private enum EndKind { LeftGirder, RightGirder, Center }

    /// <summary>
    /// A frame member: single angle (connected leg on the gusset face), channel (web on the gusset face; Pair = back-to-back
    /// channels straddling the gussets) or rolled beam (web in the frame plane, bolted through the web).
    /// </summary>
    private sealed class Member
    {
        public required string Code { get; init; }
        public required MemberRole Role { get; init; }
        public AngleSize? Angle { get; init; }
        public ChannelSize? Channel { get; init; }
        public WideFlangeSize? Beam { get; init; }
        /// <summary>Tee: stem lapped on the gusset face, flange at the bottom (−m side).</summary>
        public TeeSize? Tee { get; init; }
        public bool Pair { get; init; }
        public required Vec2 Wp1 { get; init; }
        public required Vec2 Wp2 { get; init; }
        public required bool Front { get; init; }
        /// <summary>Angles: +1 = connected leg extends toward +m from the heel; −1 toward −m.</summary>
        public required int LegDir { get; init; }
        public required EndKind End1 { get; init; }
        public required EndKind End2 { get; init; }
        /// <summary>Gusset key at each end (e.g. "TL", "BR", "C").</summary>
        public required string Gusset1 { get; init; }
        public required string Gusset2 { get; init; }
        public List<(Vec2 nrm, double c)> Start { get; } = new();
        public List<(Vec2 nrm, double c)> Finish { get; } = new();

        public string SectionName => Angle?.Name ?? (Channel != null ? (Pair ? "2-" : "") + Channel.Name : Beam?.Name ?? Tee?.Name ?? "");
        public double Depth => Channel?.Depth ?? Beam?.Depth ?? Tee?.Depth ?? 0;
        /// <summary>Lowest m of the part lapped on the gusset (a tee's flange stays below the gusset).</summary>
        public double LapLo => Tee != null ? MLo + Tee.FlangeThickness : MLo;
        public Vec2 A => (Wp2 - Wp1).Normalized();
        public Vec2 M => new(-A.Y, A.X);
        public double Heel => Angle != null ? -LegDir * Angle.CentroidFromHeel : 0;
        public double MLo => Angle != null ? Math.Min(Heel, Heel + LegDir * Angle.LegA) : -Depth / 2;
        public double MHi => Angle != null ? Math.Max(Heel, Heel + LegDir * Angle.LegA) : Depth / 2;
        public double T0, T1;
        public Vec2 At(double t, double m) => Wp1 + A * t + M * m;

        /// <summary>Half-plane through the member's long edge facing away from it, offset by clearance (for clearing other members).</summary>
        public (Vec2 nrm, double c) Beyond(bool positiveM, double clearance) => positiveM
            ? (M, Vec2.Dot(Wp1, M) + MHi + clearance)
            : (-M, -(Vec2.Dot(Wp1, M) + MLo - clearance));
    }

    /// <summary>Smallest t at which the member's footprint satisfies dot(p, nrm) ≥ c (works for either end).</summary>
    private static double Trim(Member m, Vec2 nrm, double c)
    {
        double k = Vec2.Dot(m.M, nrm);
        double minMk = Math.Min(m.MLo * k, m.MHi * k);
        double da = Vec2.Dot(m.A, nrm);
        if (Math.Abs(da) < 1e-9) return double.NaN;
        return (c - Vec2.Dot(m.Wp1, nrm) - minMk) / da;
    }

    /// <summary>Section of a styled member: angle, channel, "2-" + channel (back to back) or W shape. Unknown names are
    /// reported and replaced by the definition's chord angle (so the frame still shows; the warning says so).</summary>
    private (AngleSize? a, ChannelSize? c, WideFlangeSize? w, TeeSize? t, bool pair) Section(string name, string mark, string member)
    {
        string n = name.Trim();
        bool pair = n.StartsWith("2-") || n.StartsWith("2 ");
        if (pair) n = n[2..].Trim();
        if (SteelCatalog.FindAngle(n) is { } a && !pair) return (a, null, null, null, false);
        if (SteelCatalog.FindChannel(n) is { } c) return (null, c, null, null, pair);
        if (SteelCatalog.FindWideFlange(n) is { } w && !pair) return (null, null, w, null, false);
        if (SteelCatalog.FindTee(n) is { } t && !pair) return (null, null, null, t, false);
        Warn($"{mark}: {member} section '{name}' is not in the catalog; {_def.CrossFrames.ChordAngle} used.");
        return (SteelCatalog.Angle(_def.CrossFrames.ChordAngle), null, null, null, false);
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _unresolvedShown = new();

    private void BuildCrossFrame(CrossFrameResult cf)
    {
        var def = _def.CrossFrames;
        var st = cf.Style != "" ? def.FindStyle(cf.Style) : null;
        if (st != null)
            foreach (var note in st.Unresolved)
                if (_unresolvedShown.TryAdd(st.Name + "|" + note, true))
                    Warn($"Cross frame Type {st.Name}: UNRESOLVED (not dimensioned on the plans, nothing invented) - {note}");
        var bolts = _def.Bolts;
        var gl = _f.Girders[cf.Bay];
        var gr = _f.Girders[cf.RightGirder];
        var (pl, tl, stl) = gl.Path.At(cf.LeftGlobalDistance);
        var (pr, tr, str) = gr.Path.At(cf.RightGlobalDistance);
        var u2 = (pr.XY - pl.XY);
        double span = u2.Length;
        if (span < 0.3) return;
        u2 /= span;
        var u3 = new Vec3(u2.X, u2.Y, 0);
        // Support frames are plumb; intermediate frames are normal to the flanges (average of the two girders).
        var wt = cf.AtSupport ? Up
            : (FlangeNormal(gl.Path, cf.LeftGlobalDistance, u3) + FlangeNormal(gr.Path, cf.RightGlobalDistance, u3)).Normalized();
        wt = (wt - u3 * Vec3.Dot(wt, u3)).Normalized();
        var plane = new FramePlane { Origin = pl, U = u3, Wt = wt };
        // Right girder's top of web in plane coordinates (its "elevation" along the tilted up axis).
        double prZ = plane.Local(pr).xz.Y;

        var connPlate = cf.AtSupport ? _def.Stiffeners.BearingStiffener : _def.Stiffeners.ConnectionPlate;
        double tp = connPlate.Thickness, wcp = connPlate.Width, clr = def.MemberClearance;
        double tg = st?.GussetThickness ?? def.GussetThickness;
        double e = st is { BoltEdgeDistance: > 0 } ? st.BoltEdgeDistance : bolts.EdgeDistance;
        double pitch = st is { BoltPitch: > 0 } ? st.BoltPitch : bolts.Pitch;
        int nb = Math.Max(1, st is { BoltsPerConnection: > 0 } ? st.BoltsPerConnection : bolts.BoltsPerConnection);
        var bg = st is { BoltDiameter: > 0 } ? new BoltGeometry(st.BoltDiameter) : _model.Bolt;
        double hole = bg.HoleDiameter;
        double topWp = st != null && !double.IsNaN(st.TopWorkPointOffset) ? st.TopWorkPointOffset : def.TopWorkPointOffset;
        double botWp = st != null && !double.IsNaN(st.BottomWorkPointOffset) ? st.BottomWorkPointOffset : def.BottomWorkPointOffset;
        double d = Sec.WebDepth;

        double twl = _web[cf.Bay].Thickness(cf.LeftGlobalDistance), twr = _web[cf.RightGirder].Thickness(cf.RightGlobalDistance);
        double xfl = twl / 2 / Math.Max(Math.Abs(Vec2.Cross(tl, u2)), 0.2);
        double xfr = span - twr / 2 / Math.Max(Math.Abs(Vec2.Cross(tr, u2)), 0.2);
        double xbl = xfl + wcp - e, xbr = xfr - wcp + e;
        double xcl = xfl + wcp + clr, xcr = xfr - wcp - clr;
        if (xcr - xcl < 0.5)
        {
            Warn($"{cf.Mark}: girders are too close for a cross frame ({_u.FormatLength(span)}).");
            return;
        }

        var A = new Vec2(xbl, pl.Z - topWp);
        var B = new Vec2(xbl, pl.Z - d + botWp);
        var C = new Vec2(xbr, prZ - topWp);
        var D = new Vec2(xbr, prZ - d + botWp);

        string mark = cf.Mark;
        _assemblies.Add(new SteelAssembly
        {
            Mark = mark, Kind = AssemblyKind.CrossFrame,
            Description = $"{(cf.AtSupport ? "Support " : "")}{(st != null ? $"Type {st.Name} " : "")}{ChoiceName(cf.Type)} G{cf.Bay + 1}–G{cf.RightGirder + 1}, span {cf.Span + 1}",
        });

        double vB = tp / 2, vF = tp / 2 + tg;
        var weldMesh = new Mesh();
        var det = new CrossFrameDetail2D
        {
            Mark = mark, AtSupport = cf.AtSupport, Type = cf.Type, Span = span, LeftWebTop = pl.Z, RightWebTop = prZ, LeftWebFace = xfl, RightWebFace = xfr,
            PlateWidth = wcp, PlateThickness = tp, GussetThickness = tg, WorkPoints = new[] { A, B, C, D }, WeldLap = def.WeldLap, WeldSize = def.MemberWeldSize,
            ChannelName = cf.Type == ResolvedCrossFrameType.Diaphragm ? def.DiaphragmChannel : "",
            BoltDiameter = bg.Diameter, StyleName = st?.Name ?? "",
        };
        _cfDetails.Add(det);

        if (cf.Type == ResolvedCrossFrameType.Diaphragm)
        {
            BuildDiaphragm(cf, plane, A, B, C, D, xcl - clr + Inch(0.5), xcr + clr - Inch(0.5), tp, nb, e, pitch, det);
            return;
        }
        if (cf.Type == ResolvedCrossFrameType.PlateDiaphragm && st != null)
        {
            BuildPlateDiaphragm(cf, st, plane, A, B, C, D, xbl, xbr, xcl, xcr, tp, e, bg, det);
            return;
        }

        // Member sections: the style's, or the definition's angles.
        Member Make(string code, MemberRole role, string section, string legacyAngle, Vec2 w1, Vec2 w2, bool front, int legDir,
            EndKind e1, EndKind e2, string g1, string g2)
        {
            var (a, c, w, t, pair) = st != null ? Section(section, mark, code) : (SteelCatalog.Angle(legacyAngle), null, null, null, false);
            return new Member { Code = code, Role = role, Angle = a, Channel = c, Beam = w, Tee = t, Pair = pair, Wp1 = w1, Wp2 = w2, Front = front,
                LegDir = legDir, End1 = e1, End2 = e2, Gusset1 = g1, Gusset2 = g2 };
        }
        string chordAngle = cf.AtSupport ? def.SupportChordAngle : def.ChordAngle;
        string diagAngle = cf.AtSupport ? def.SupportDiagonalAngle : def.DiagonalAngle;
        var members = new List<Member>();
        bool hasTop = cf.Type != ResolvedCrossFrameType.Strut && (st == null || st.TopChord != "");
        var top = hasTop ? Make("TC", MemberRole.TopChord, st?.TopChord ?? "", chordAngle, A, C, true, -1, EndKind.LeftGirder, EndKind.RightGirder, "TL", "TR") : null;
        var bot = Make("BC", MemberRole.BottomChord, st?.BottomChord ?? "", chordAngle, B, D, true, 1, EndKind.LeftGirder, EndKind.RightGirder, "BL", "BR");
        if (top != null) members.Add(top);
        members.Add(bot);
        bool beamTop = top?.Beam != null;

        Vec2? center = null;
        switch (cf.Type)
        {
            case ResolvedCrossFrameType.XFrame when top != null:
            {
                var d1 = Make("D1", MemberRole.Diagonal, st?.Diagonal ?? "", diagAngle, A, D, true, 1, EndKind.LeftGirder, EndKind.RightGirder, "TL", "BR");
                var d2 = Make("D2", MemberRole.Diagonal, st?.Diagonal ?? "", diagAngle, B, C, false, 1, EndKind.LeftGirder, EndKind.RightGirder, "BL", "TR");
                // D1 shares the front face with both chords: keep it below the top chord and above the bottom chord.
                d1.Start.Add(top.Beyond(false, clr));
                d1.Finish.Add(bot.Beyond(true, clr));
                members.Add(d1);
                members.Add(d2);
                break;
            }
            case ResolvedCrossFrameType.KFrame:
            {
                var m = Vec2.Lerp(B, D, 0.5);
                center = m;
                var dl = Make("D1", MemberRole.Diagonal, st?.Diagonal ?? "", diagAngle, A, m, true, 1, EndKind.LeftGirder, EndKind.Center, "TL", "C");
                var dr = Make("D2", MemberRole.Diagonal, st?.Diagonal ?? "", diagAngle, C, m, true, -1, EndKind.RightGirder, EndKind.Center, "TR", "C");
                foreach (var x in new[] { dl, dr })
                {
                    if (top != null) x.Start.Add(top.Beyond(false, clr));
                    x.Finish.Add(bot.Beyond(true, clr));
                }
                dl.Finish.Add((new Vec2(-1, 0), -(m.X - clr / 2)));
                dr.Finish.Add((new Vec2(1, 0), m.X + clr / 2));
                members.Add(dl);
                members.Add(dr);
                break;
            }
            case ResolvedCrossFrameType.InvertedKFrame when top != null:
            {
                // Apex on the top chord's work line; under a rolled beam the diagonals stop below its bottom flange.
                var m = Vec2.Lerp(A, C, 0.5);
                center = m;
                var dl = Make("D1", MemberRole.Diagonal, st?.Diagonal ?? "", diagAngle, B, m, true, -1, EndKind.LeftGirder, EndKind.Center, "BL", "C");
                var dr = Make("D2", MemberRole.Diagonal, st?.Diagonal ?? "", diagAngle, D, m, true, 1, EndKind.RightGirder, EndKind.Center, "BR", "C");
                foreach (var x in new[] { dl, dr })
                {
                    x.Start.Add(bot.Beyond(true, clr));
                    x.Finish.Add(top.Beyond(false, clr));
                }
                dl.Finish.Add((new Vec2(-1, 0), -(m.X - clr / 2)));
                dr.Finish.Add((new Vec2(1, 0), m.X + clr / 2));
                members.Add(dl);
                members.Add(dr);
                break;
            }
        }

        // Clear the connection stiffeners at girder ends, then compute cut lengths.
        foreach (var m in members)
        {
            void AddGirderClear(EndKind k, List<(Vec2, double)> list)
            {
                if (k == EndKind.LeftGirder) list.Add((new Vec2(1, 0), xcl));
                else if (k == EndKind.RightGirder) list.Add((new Vec2(-1, 0), -xcr));
            }
            AddGirderClear(m.End1, m.Start);
            AddGirderClear(m.End2, m.Finish);
            m.T0 = m.Start.Select(h => Trim(m, h.nrm, h.c)).Where(t => !double.IsNaN(t)).DefaultIfEmpty(0).Max();
            m.T1 = m.Finish.Select(h => Trim(m, h.nrm, h.c)).Where(t => !double.IsNaN(t)).DefaultIfEmpty((m.Wp2 - m.Wp1).Length).Min();
            if (m.T1 - m.T0 < 2 * def.WeldLap + Inch(1))
                Warn($"{mark}: {m.Code} is too short ({_u.FormatLength(Math.Max(0, m.T1 - m.T0))}) for the weld laps; check work points and bay width.");
        }

        // X-frame crossing: where the gage lines of the two diagonals cross (filler + bolt + holes).
        Vec2? crossing = null;
        if (cf.Type == ResolvedCrossFrameType.XFrame && members.Count(m => m.Role == MemberRole.Diagonal) == 2
            && members.Where(m => m.Role == MemberRole.Diagonal).All(m => m.Angle != null))
        {
            var d1 = members.First(m => m.Code == "D1");
            var d2 = members.First(m => m.Code == "D2");
            var g1 = d1.Wp1 + d1.M * (d1.Heel + d1.LegDir * d1.Angle!.LegA / 2);
            var g2 = d2.Wp1 + d2.M * (d2.Heel + d2.LegDir * d2.Angle!.LegA / 2);
            if (Numerics.IntersectLines(g1, d1.A, g2, d2.A, out double ta, out _)) crossing = g1 + d1.A * ta;
        }

        // Members, lap welds, and gusset footprints.
        var gussetPts = new Dictionary<string, List<Vec2>>();
        void AddPts(string key, IEnumerable<Vec2> pts)
        {
            if (!gussetPts.TryGetValue(key, out var l)) gussetPts[key] = l = new List<Vec2>();
            l.AddRange(pts);
        }
        double lap = def.WeldLap, ws = def.MemberWeldSize, margin = ws + Inch(0.25);
        int weldRuns = 0;
        foreach (var m in members)
        {
            if (m.T1 <= m.T0) continue;
            int o = m.Front ? 1 : -1;
            double vBase = m.Front ? vF : vB;
            var mesh = new Mesh();
            void Loft(Vec2[] sec, double t0, double t1)
            {
                Vec3[] Ring(double t) => sec.Select(q => plane.W(m.At(t, q.X), q.Y)).ToArray();
                MeshBuilder.AddLoft(mesh, sec, new[] { Ring(t0), Ring(t1) });
            }
            var category = PartCategory.Angle;
            if (m.Angle != null)
            {
                if (crossing is { } xp && m.Role == MemberRole.Diagonal)
                {
                    double tc = Vec2.Dot(xp - m.Wp1, m.A), mc = Vec2.Dot(xp - m.Wp1, m.M);
                    AddAngleWithHole(mesh, m, plane, vBase, o, new Hole(new Vec2(tc, mc), hole));
                }
                else Loft(SteelCatalog.AnglePolygon(m.Angle, m.Heel, m.LegDir, vBase, o), m.T0, m.T1);
            }
            else if (m.Channel != null)
            {
                category = PartCategory.Channel;
                Loft(SteelCatalog.ChannelPolygon(m.Channel, vF, 1), m.T0, m.T1);
                if (m.Pair) Loft(SteelCatalog.ChannelPolygon(m.Channel, vB, -1), m.T0, m.T1);
            }
            else if (m.Tee is { } te)
            {
                // Stem on the gusset face, flange at the bottom centred on the stem.
                category = PartCategory.Channel;
                Loft(SteelCatalog.TeePolygon(te, vF), m.T0, m.T1);
            }
            else if (m.Beam is { } w)
            {
                // Web on the stiffener face (gusset plane), flanges centred on it; flanges coped clear of the stiffeners,
                // the web runs on to the bolt columns.
                category = PartCategory.Channel;
                double h = w.Depth / 2, tf = w.FlangeThickness;
                Loft(SteelCatalog.WideFlangePolygon(w, vB), m.T0, m.T1);
                var webSec = new[] { new Vec2(-h + tf, vB), new Vec2(h - tf, vB), new Vec2(h - tf, vB + w.WebThickness), new Vec2(-h + tf, vB + w.WebThickness) };
                double tL = (xbl - e - m.Wp1.X) / m.A.X, tR = (xbr + e - m.Wp1.X) / m.A.X;
                if (tL < m.T0) Loft(webSec, tL, m.T0);
                if (tR > m.T1) Loft(webSec, m.T1, tR);
            }
            det.WorkLines.Add((m.Code, m.Wp1, m.Wp2));
            det.Members.Add((m.Code, m.SectionName, new[] { m.At(m.T0, m.MLo), m.At(m.T1, m.MLo), m.At(m.T1, m.MHi), m.At(m.T0, m.MHi) },
                m.At(m.T0, m.Heel), m.At(m.T1, m.Heel), m.Front));
            _parts.Add(new SteelPart
            {
                Mark = $"{mark}-{m.Code}", Category = category, Role = m.Role, Assembly = mark, Mesh = mesh,
                Length = m.T1 - m.T0, Description = m.SectionName, Material = "ASTM A709 Gr. 36",
                NominalWeightLbPerFt = (m.Angle?.WeightPerFootLb ?? m.Channel?.WeightPerFootLb ?? m.Beam?.WeightPerFootLb ?? m.Tee?.WeightPerFootLb ?? 0) * (m.Pair ? 2 : 1),
            });
            if (m.Beam != null) continue;                    // bolted through its web, no gusset laps

            foreach (var (t0, t1, key) in new[] { (m.T0, m.T0 + lap, m.Gusset1), (m.T1 - lap, m.T1, m.Gusset2) })
            {
                double lo = m.Tee != null ? m.LapLo + Inch(0.25) : m.MLo - margin;     // a tee's flange stays below the gusset
                AddPts(key, new[] { m.At(t0, lo), m.At(t0, m.MHi + margin), m.At(t1, lo), m.At(t1, m.MHi + margin) });
                // Fillet welds along both long edges of the lap (angle heel and toe / channel web edges), each channel of a pair.
                var mDir = plane.Dir(m.M);
                foreach (var (vb, oo) in m.Pair ? new[] { (vF, 1), (vB, -1) } : new[] { (vBase, o) })
                {
                    weldMesh.AddWeld(plane.W(m.At(t0, m.LapLo), vb), plane.W(m.At(t1, m.LapLo), vb), mDir * -1, plane.N * oo, ws);
                    weldMesh.AddWeld(plane.W(m.At(t0, m.MHi), vb), plane.W(m.At(t1, m.MHi), vb), mDir, plane.N * oo, ws);
                    weldRuns += 2;
                }
            }
        }

        // Tie plates across the bottom flanges of back-to-back channels (plans: ½" tie plates near the bay centre).
        if (st != null && bot.Channel != null && st.TiePlateOffsets.Count > 0 && bot.T1 > bot.T0)
        {
            // Under both flanges of back-to-back channels, or under the single channel's flange.
            var ch = bot.Channel;
            double tc0 = (bot.T0 + bot.T1) / 2, tpl = st.TiePlateThickness;
            double va = bot.Pair ? vB - ch.FlangeWidth : vF, vb2 = vF + ch.FlangeWidth;
            var sec = new[] { new Vec2(bot.MLo - tpl, va), new Vec2(bot.MLo, va), new Vec2(bot.MLo, vb2), new Vec2(bot.MLo - tpl, vb2) };
            int k = 1;
            foreach (double off in st.TiePlateOffsets)
            {
                double t0 = tc0 + off - st.TiePlateLength / 2, t1 = tc0 + off + st.TiePlateLength / 2;
                if (t0 < bot.T0 || t1 > bot.T1) { Warn($"{mark}: tie plate at {_u.FormatLength(off)} from the bay centre is beyond the bottom chord."); continue; }
                var tm = new Mesh();
                Vec3[] Ring(double t) => sec.Select(q => plane.W(bot.At(t, q.X), q.Y)).ToArray();
                MeshBuilder.AddLoft(tm, sec, new[] { Ring(t0), Ring(t1) });
                _parts.Add(new SteelPart { Mark = $"{mark}-TP{k++}", Category = PartCategory.FillerPlate, Assembly = mark, Mesh = tm, Length = st.TiePlateLength,
                    Description = $"Tie plate {Pl(vb2 - va, tpl)} x {In(st.TiePlateLength)}" });
            }
        }

        // Gussets at girders: bolt group + member laps; bolted through the connection stiffener. A rolled-beam top chord is
        // bolted through its own web instead (no top gussets).
        var wps = new Dictionary<string, (Vec2 wp, double webTop, int girder, double s)>
        {
            ["TL"] = (A, pl.Z, cf.Bay, cf.LeftGlobalDistance), ["BL"] = (B, pl.Z, cf.Bay, cf.LeftGlobalDistance),
            ["TR"] = (C, prZ, cf.RightGirder, cf.RightGlobalDistance), ["BR"] = (D, prZ, cf.RightGirder, cf.RightGlobalDistance),
        };
        foreach (var (key, (wp, webTop, girder, s)) in wps)
        {
            bool beamHere = beamTop && key[0] == 'T';
            if (!beamHere && !gussetPts.ContainsKey(key)) continue;
            double grip = tp + (beamHere ? top!.Beam!.WebThickness : tg);
            double boltLen = bg.LengthForGrip(grip);
            // Rows centred on the work point; columns from the plate edge (1 3/4" in) toward the web at the gage.
            int rows = key[0] == 'T' && st is { TopBoltRows: > 0 } ? st.TopBoltRows : nb;
            double rowPitch = key[0] == 'B' && st is { BottomBoltPitch: > 0 } ? st.BottomBoltPitch : pitch;
            int cols = Math.Max(1, st?.BoltColumns ?? 1);
            double gage = st?.BoltGage ?? 0, sideX = key[1] == 'L' ? -1 : 1;
            var bpts = new List<Vec2>();
            for (int col = 0; col < cols; col++)
                for (int i = 0; i < rows; i++)
                    bpts.Add(new Vec2(wp.X + sideX * col * gage, wp.Y + (key[0] == 'B' ? st?.BottomBoltOffset ?? 0 : 0) + (i - (rows - 1) / 2.0) * rowPitch));
            det.Bolts.AddRange(bpts);
            foreach (var b in bpts)
            {
                _bolts.Add(new BoltInstance
                {
                    Frame = Frame3.FromXZ(plane.W(b, -tp / 2), plane.U, plane.N),
                    Grip = grip, Length = boltLen, Assembly = mark, Diameter = bg.Diameter,
                });
                AddStiffenerHole(girder, s, plane.W(b, 0), hole);
            }
            if (beamHere) continue;
            AddPts(key, bpts.SelectMany(b => new[] { b + new Vec2(-e, -e), b + new Vec2(e, -e), b + new Vec2(e, e), b + new Vec2(-e, e) }));
            var hull = MeshBuilder.ConvexHull(gussetPts[key]);
            double zTop = hull.Max(q => q.Y), zBot = hull.Min(q => q.Y);
            if (key[0] == 'T' && zTop > webTop - Inch(0.5))
                Warn($"{mark}: top gusset at G{girder + 1} reaches the top flange; lower the top work point.");
            if (key[0] == 'B' && zBot < webTop - d + Inch(0.5))
                Warn($"{mark}: bottom gusset at G{girder + 1} reaches the bottom flange; raise the bottom work point.");
            AddGusset(mark, $"G{key}", hull, bpts.Select(b => new Hole(b, hole)).ToList(), plane, vB, vF, tg);
            det.Gussets.Add(($"G{key}", hull.ToArray()));
        }

        if (center is { } cpt && gussetPts.TryGetValue("C", out var cpts))
        {
            var chordM = cf.Type == ResolvedCrossFrameType.KFrame ? bot : top!;
            double x0 = cpts.Min(q => q.X) - margin, x1 = cpts.Max(q => q.X) + margin;
            double ta = (x0 - chordM.Wp1.X) / chordM.A.X, tb = (x1 - chordM.Wp1.X) / chordM.A.X;
            if (chordM.Beam != null)
                // Hangs from the beam's bottom flange (welded to it).
                cpts.AddRange(new[] { chordM.At(ta, chordM.MLo), chordM.At(tb, chordM.MLo) });
            else
                // Also carries the continuous chord through the connection.
            {
                double lo = chordM.Tee != null ? chordM.LapLo + Inch(0.25) : chordM.MLo - margin;
                cpts.AddRange(new[] { chordM.At(ta, lo), chordM.At(ta, chordM.MHi + margin), chordM.At(tb, lo), chordM.At(tb, chordM.MHi + margin) });
            }
            var chull = MeshBuilder.ConvexHull(cpts);
            if (chordM.Beam != null)
            {
                // Clip off anything above the beam's bottom flange.
                var nrm = -chordM.M; double c0 = -(Vec2.Dot(chordM.Wp1, chordM.M) + chordM.MLo);
                chull = PlateBuilder.ClipHalfPlane(chull, nrm, c0);
            }
            AddGusset(mark, "GC", chull, Array.Empty<Hole>(), plane, vB, vF, tg);
            det.Gussets.Add(("GC", chull.ToArray()));
        }

        if (crossing is { } xc)
        {
            var d1 = members.First(m => m.Code == "D1");
            var d2 = members.First(m => m.Code == "D2");
            double hs = Math.Max(Math.Min(d1.Angle!.LegA, d2.Angle!.LegA), 2 * e) / 2;
            var filler = new[] { xc + new Vec2(-hs, -hs), xc + new Vec2(hs, -hs), xc + new Vec2(hs, hs), xc + new Vec2(-hs, hs) };
            det.Filler = filler;
            det.Bolts.Add(xc);
            var fm = new Mesh();
            PlateBuilder.AddPrism(fm, filler, new[] { new Hole(xc, hole) }, (a, b, v) => plane.W(new Vec2(a, b), v), _ => vB, _ => vF);
            _parts.Add(new SteelPart { Mark = $"{mark}-F1", Category = PartCategory.FillerPlate, Assembly = mark, Mesh = fm, Length = 2 * hs,
                Description = $"Filler {Pl(2 * hs, tg)} x {In(2 * hs)}" });
            double g = d1.Angle.Thickness + tg + d2.Angle.Thickness;
            _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(plane.W(xc, vB - d2.Angle.Thickness), plane.U, plane.N), Grip = g,
                Length = bg.LengthForGrip(g), Assembly = mark, Diameter = bg.Diameter });
        }

        _parts.Add(new SteelPart { Mark = $"{mark}-W", Category = PartCategory.Weld, Assembly = mark, Mesh = weldMesh, Material = "E70XX",
            Length = weldRuns * lap, Description = $"Fillet weld {In(ws)}, members to gussets" });
    }

    /// <summary>
    /// Built-up plate diaphragm: web plate on the stiffener face from bolt column to bolt column (bolted to both connection
    /// plates), flange plates welded along its top and bottom edges between the stiffeners. Top / bottom = work points.
    /// </summary>
    private void BuildPlateDiaphragm(CrossFrameResult cf, CrossFrameStyle st, FramePlane plane, Vec2 a, Vec2 b, Vec2 c, Vec2 d,
        double xbl, double xbr, double xcl, double xcr, double tp, double e, BoltGeometry bg, CrossFrameDetail2D det)
    {
        double tw = st.WebPlateThickness, bf = st.FlangePlateWidth, tf = st.FlangePlateThickness;
        double vB = tp / 2, vc = vB + tw / 2;
        // Top / bottom lines through the work points (they follow the girders' relative elevation).
        double Zt(double x) => a.Y + (c.Y - a.Y) * (x - a.X) / (c.X - a.X);
        double Zb(double x) => b.Y + (d.Y - b.Y) * (x - b.X) / (d.X - b.X);
        double x0 = xbl - e, x1 = xbr + e;
        var web = new List<Vec2> { new(x0, Zb(x0) + tf), new(x1, Zb(x1) + tf), new(x1, Zt(x1) - tf), new(x0, Zt(x0) - tf) };
        int n = Math.Max(2, st.DiaphragmBolts);
        double hole = bg.HoleDiameter, grip = tp + tw;
        double webH = Math.Min(Zt(xbl) - Zb(xbl), Zt(xbr) - Zb(xbr)) - 2 * tf;
        double pitch = st.DiaphragmBoltPitch > 0 ? st.DiaphragmBoltPitch : (webH - 2 * st.DiaphragmBoltEnd) / (n - 1);
        var holes = new List<Hole>();
        foreach (var (x, wp, girder, s) in new[] { (xbl, (a + b) * 0.5, cf.Bay, cf.LeftGlobalDistance), (xbr, (c + d) * 0.5, cf.RightGirder, cf.RightGlobalDistance) })
        {
            double zc = (Zt(x) + Zb(x)) / 2;
            for (int i = 0; i < n; i++)
            {
                var bp = new Vec2(x, zc + (i - (n - 1) / 2.0) * pitch);
                holes.Add(new Hole(bp, hole));
                det.Bolts.Add(bp);
                _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(plane.W(bp, -tp / 2), plane.U, plane.N), Grip = grip,
                    Length = bg.LengthForGrip(grip), Assembly = cf.Mark, Diameter = bg.Diameter });
                AddStiffenerHole(girder, s, plane.W(bp, 0), hole);
            }
        }
        if ((n - 1) * pitch > webH - 2 * e)
            Warn($"{cf.Mark}: {n} diaphragm bolts at {_u.FormatLength(pitch)} do not fit the web plate depth.");
        var wm = new Mesh();
        PlateBuilder.AddPrism(wm, web, holes, (p, q, v) => plane.W(new Vec2(p, q), v), _ => vB, _ => vB + tw);
        double h = web.Max(q => q.Y) - web.Min(q => q.Y);
        _parts.Add(new SteelPart { Mark = $"{cf.Mark}-WEB", Category = PartCategory.WebPlate, Role = MemberRole.Diaphragm, Assembly = cf.Mark, Mesh = wm,
            Length = x1 - x0, Description = $"Diaphragm web {Pl(h, tw)} x {In(x1 - x0)}", Material = "ASTM A709 Gr. 50W" });
        det.Members.Add(("WEB", $"PL {In(tw)}", web.ToArray(), web[0], web[1], true));
        // Flange plates between the stiffeners (top below the top line, bottom above the bottom line).
        int k = 1;
        foreach (bool topF in new[] { true, false })
        {
            Func<double, double> z = topF ? Zt : Zb;
            double s0 = topF ? -tf : 0, s1 = topF ? 0 : tf;
            var fm = new Mesh();
            var outline = new[] { new Vec2(xcl, z(xcl) + s0), new Vec2(xcr, z(xcr) + s0), new Vec2(xcr, z(xcr) + s1), new Vec2(xcl, z(xcl) + s1) };
            PlateBuilder.AddPrism(fm, outline, Array.Empty<Hole>(), (p, q, v) => plane.W(new Vec2(p, q), v), _ => vc - bf / 2, _ => vc + bf / 2);
            _parts.Add(new SteelPart { Mark = $"{cf.Mark}-FL{k++}", Category = PartCategory.FlangePlate, Role = MemberRole.Diaphragm, Assembly = cf.Mark, Mesh = fm,
                Length = xcr - xcl, Description = $"Diaphragm flange {Pl(bf, tf)} x {In(xcr - xcl)}", Material = "ASTM A709 Gr. 50W" });
            det.Members.Add((topF ? "TF" : "BF", $"PL {In(tf)}x{In(bf)}", outline, outline[0], outline[1], true));
        }
    }

    /// <summary>Angle built as connected leg (with a bolt hole) + outstanding leg, for members bolted through the leg.</summary>
    private static void AddAngleWithHole(Mesh mesh, Member m, FramePlane plane, double vBase, int o, Hole hole)
    {
        var a = m.Angle!;
        double heel = m.Heel, toe = heel + m.LegDir * a.LegA, legEnd = heel + m.LegDir * a.Thickness;
        Vec3 Map(double t, double mm, double v) => plane.W(m.At(t, mm), v);
        // Connected leg: outline (t, m), thickness v.
        PlateBuilder.AddPrism(mesh, new[] { new Vec2(m.T0, Math.Min(heel, toe)), new Vec2(m.T1, Math.Min(heel, toe)), new Vec2(m.T1, Math.Max(heel, toe)), new Vec2(m.T0, Math.Max(heel, toe)) },
            new[] { hole }, (t, mm, v) => Map(t, mm, v), _ => vBase, _ => vBase + o * a.Thickness);
        // Outstanding leg: outline (t, v), thickness m.
        double v0 = vBase + o * a.Thickness, v1 = vBase + o * a.LegB;
        PlateBuilder.AddPrism(mesh, new[] { new Vec2(m.T0, Math.Min(v0, v1)), new Vec2(m.T1, Math.Min(v0, v1)), new Vec2(m.T1, Math.Max(v0, v1)), new Vec2(m.T0, Math.Max(v0, v1)) },
            Array.Empty<Hole>(), (t, v, mm) => Map(t, mm, v), _ => Math.Min(heel, legEnd), _ => Math.Max(heel, legEnd));
        // AISC root fillet between the legs.
        double r = a.RootRadius;
        if (r > 0)
        {
            int sgn = m.LegDir;
            var c = new Vec2(heel + sgn * (a.Thickness + r), vBase + o * (a.Thickness + r));
            var sec = new List<Vec2> { new(heel + sgn * a.Thickness, vBase + o * a.Thickness) };
            for (int k = 0; k <= 6; k++)
            {
                double th = Math.PI / 2 * k / 6;
                sec.Add(new Vec2(c.X - sgn * r * Math.Sin(th), c.Y - o * r * Math.Cos(th)));
            }
            sec.Reverse(1, sec.Count - 1);
            Vec3[] Ring(double t) => sec.Select(q => Map(t, q.X, q.Y)).ToArray();
            MeshBuilder.AddLoft(mesh, sec.ToArray(), new[] { Ring(m.T0), Ring(m.T1) });
        }
    }

    private void AddGusset(string mark, string code, List<Vec2> hull, IReadOnlyList<Hole> holes, FramePlane plane, double v0, double v1, double tg)
    {
        if (hull.Count < 3) return;
        var mesh = new Mesh();
        PlateBuilder.AddPrism(mesh, hull, holes, (a, b, v) => plane.W(new Vec2(a, b), v), _ => v0, _ => v1);
        double w = hull.Max(q => q.X) - hull.Min(q => q.X), h = hull.Max(q => q.Y) - hull.Min(q => q.Y);
        _parts.Add(new SteelPart { Mark = $"{mark}-{code}", Category = PartCategory.Gusset, Assembly = mark, Mesh = mesh, Length = Math.Max(w, h),
            Description = $"Gusset {Pl(w, tg)} x {In(h)}" + (holes.Count > 0 ? $", {holes.Count} holes {In(holes[0].Diameter)}" : "") });
    }

    private void BuildDiaphragm(CrossFrameResult cf, FramePlane plane, Vec2 a, Vec2 b, Vec2 c, Vec2 d, double x0, double x1,
        double tp, int nb, double e, double pitch, CrossFrameDetail2D det)
    {
        var ch = SteelCatalog.Channel(_def.CrossFrames.DiaphragmChannel);
        var l = Vec2.Lerp(a, b, 0.5);
        var r = Vec2.Lerp(c, d, 0.5);
        var dir = (r - l).Normalized();
        var m = new Vec2(-dir.Y, dir.X);
        double t0 = (x0 - l.X) / dir.X, t1 = (x1 - l.X) / dir.X;
        var sec = SteelCatalog.ChannelPolygon(ch, tp / 2, 1);
        Vec3[] Ring(double t) => sec.Select(q => plane.W(l + dir * t + m * q.X, q.Y)).ToArray();
        var mesh = new Mesh();
        MeshBuilder.AddLoft(mesh, sec, new[] { Ring(t0), Ring(t1) });
        det.Members.Add(("DIA", ch.Name, new[] { l + dir * t0 + m * (-ch.Depth / 2), l + dir * t1 + m * (-ch.Depth / 2), l + dir * t1 + m * (ch.Depth / 2), l + dir * t0 + m * (ch.Depth / 2) },
            l + dir * t0, l + dir * t1, true));
        _parts.Add(new SteelPart { Mark = $"{cf.Mark}-DIA", Category = PartCategory.Channel, Role = MemberRole.Diaphragm, Assembly = cf.Mark,
            Mesh = mesh, Length = t1 - t0, Description = ch.Name, Material = "ASTM A709 Gr. 36", NominalWeightLbPerFt = ch.WeightPerFootLb });

        int n = Math.Max(2, Math.Min(nb + 1, (int)Math.Floor((ch.Depth - 2 * ch.FlangeThickness - 2 * e) / pitch) + 1));
        double grip = tp + ch.WebThickness;
        foreach (var (wp, girder, s) in new[] { (a, cf.Bay, cf.LeftGlobalDistance), (c, cf.RightGirder, cf.RightGlobalDistance) })
        {
            double t = (wp.X - l.X) / dir.X;
            var cpt = l + dir * t;
            for (int i = 0; i < n; i++)
            {
                var bp = cpt + m * ((i - (n - 1) / 2.0) * pitch);
                det.Bolts.Add(bp);
                _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(plane.W(bp, -tp / 2), plane.U, plane.N), Grip = grip,
                    Length = _model.Bolt.LengthForGrip(grip), Assembly = cf.Mark, Diameter = _model.Bolt.Diameter });
                AddStiffenerHole(girder, s, plane.W(bp, 0), HoleDia);
            }
        }
    }

    private static string ChoiceName(ResolvedCrossFrameType t) => t switch
    {
        ResolvedCrossFrameType.XFrame => "X-frame",
        ResolvedCrossFrameType.KFrame => "K-frame",
        ResolvedCrossFrameType.InvertedKFrame => "Inverted K-frame",
        ResolvedCrossFrameType.Strut => "Strut",
        ResolvedCrossFrameType.PlateDiaphragm => "Plate diaphragm",
        _ => "Diaphragm",
    };

}
