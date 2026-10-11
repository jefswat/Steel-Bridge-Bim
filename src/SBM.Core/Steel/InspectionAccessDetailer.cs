using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Inspection access (I-80 design sheets 159-170, Ramp G sheets 130-134): wire-rope inspection cables on WT brackets bolted
/// through the girder web, and walkways in the girder bays. Runs in the CONNECTIONS phase because the bracket bolts register
/// holes in the girder webs.
///
/// Simplifications (the plans do not dimension them): rope hardware (thimbles, clips, shackles, turnbuckles) and the chains at
/// the openings are not modeled; tees are plain flange + stem plates (no root fillet); walkway grating is one solid slab
/// (mass from the grating's nominal weight); walkway end connections (bent L4x4x1/2 on 3/4" bolts to the cross frame bottom
/// strut) and the 5/16" bent-plate posts at skewed frames are not modeled.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private const string BracketMaterial = "ASTM A709 Gr. 50W";
    private const string WalkwayMaterial = "ASTM A709 Gr. 50, galvanized";

    private void BuildInspectionAccess()
    {
        var ia = _def.InspectionAccess;
        if (ia is not { Enabled: true } || _def.Girders.IsPrestressed) return;
        int n = 0;
        foreach (var c in ia.Cables) BuildInspectionCable(ia, c, ++n);
        BuildWalkways(ia);
    }

    /// <summary>Path distance along a girder of its CL bearing at a support (clamped to the girder).</summary>
    private double BearingPathDistance(int girder, int support)
    {
        var g = _f.Girders[girder];
        var br = support >= 0 && support < _f.Supports.Count ? _f.Supports[support].Bearings[girder] : null;
        if (br != null && !double.IsNaN(br.PathDistance)) return br.PathDistance;
        int nSpan = g.Spans.Count;
        double s = support < nSpan ? g.DistanceAt(support, 0) : g.DistanceAt(nSpan - 1, 1);
        return Math.Clamp(s, 0, g.Length);
    }

    // ================================================================== inspection cables

    private void BuildInspectionCable(InspectionAccessDefinition ia, InspectionCableDefinition c, int no)
    {
        if (c.Girder < 0 || c.Girder >= _f.Girders.Count) { Warn($"Inspection cable {no}: girder G{c.Girder + 1} does not exist."); return; }
        int nSup = _f.Supports.Count;
        int sb = c.BeginSupport, se = c.EndSupport < 0 ? nSup - 1 : c.EndSupport;
        if (sb < 0 || sb >= nSup || se < 0 || se >= nSup) { Warn($"Inspection cable {no}: support index out of range."); return; }
        var anchorTee = SteelCatalog.FindTee(ia.AnchorBracket);
        var supportTee = SteelCatalog.FindTee(ia.SupportBracket);
        if (anchorTee == null || supportTee == null)
        {
            Warn($"Inspection cable {no}: bracket section '{(anchorTee == null ? ia.AnchorBracket : ia.SupportBracket)}' is not in the AISC catalog; cable skipped.");
            return;
        }
        int gi = c.Girder;
        double s0 = BearingPathDistance(gi, sb) + c.BeginOffset, s1 = BearingPathDistance(gi, se) + c.EndOffset;
        double length = s1 - s0;
        if (length < UnitsFt(10)) { Warn($"G{gi + 1}: inspection cable run is only {In(length)}; skipped."); return; }
        string mark = $"IC{no}";
        string face = c.LeftFace ? "left" : "right";
        _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.InspectionCable, Girder = gi,
            Description = $"Inspection cable on the {face} face of G{gi + 1} web, {In(length)} long" });

        // End anchorages, intermediate supports (equal sections not longer than the maximum), brackets at cross-frame stiffeners.
        var stiff = _f.Stiffeners.Where(st => st.Girder == gi && st.Kind == StiffenerKind.ConnectionPlate).Select(st => st.GirderDistance)
            .Where(s => s > s0 && s < s1).OrderBy(s => s).ToList();
        int sections = Math.Max(1, (int)Math.Ceiling(length / ia.MaxAnchorSpacing - 1e-9));
        var anchors = new List<double> { s0 };
        for (int k = 1; k < sections; k++)
        {
            double target = s0 + length * k / sections;
            double near = stiff.Count > 0 ? stiff.MinBy(s => Math.Abs(s - target)) : target;
            anchors.Add(Math.Abs(near - target) <= 0.25 * length / sections ? near : target);
        }
        anchors.Add(s1);
        for (int k = 1; k < anchors.Count; k++)
            if (anchors[k] - anchors[k - 1] < UnitsFt(45) && sections > 1)
                Warn($"G{gi + 1}: inspection cable section of {In(anchors[k] - anchors[k - 1])} is shorter than the 45'-0 minimum.");
        var items = anchors.Select(s => (s, anchor: true)).ToList();
        foreach (double s in stiff)
            if (anchors.All(a => Math.Abs(a - s) > ia.BracketClearOfAnchor)) items.Add((s, false));
        items.Sort((a, b) => a.s.CompareTo(b.s));

        double side = c.LeftFace ? -1 : 1;
        double D = Sec.WebDepth, zRope = -(D - ia.RopeHeightAboveWebBottom);
        var bg = new BoltGeometry(ia.BracketBoltDiameter);
        double hd = bg.HoleDiameter;
        var path = _f.Girders[gi].Path;
        var rope = new List<Vec3>();
        int bn = 0;
        foreach (var (s, anchor) in items)
        {
            var tee = anchor ? anchorTee : supportTee;
            double L = anchor ? ia.AnchorBracketLength : ia.SupportBracketLength;
            var (p, t, _) = path.At(s);
            var right = new Vec3(t.Right.X, t.Right.Y, 0);
            var tan = new Vec3(t.X, t.Y, 0);
            var nOut = right * side;
            double twg = _web[gi].Thickness(s);
            var O = p + right * (side * twg / 2) + Up * zRope;                    // on the web face, at the rope height
            Vec3 Wd(double a, double b, double cc) => O + tan * a + nOut * b + Up * cc;   // a along the girder, b outward from the web, cc up
            double tf = tee.FlangeThickness, ts = tee.StemThickness, bf = tee.FlangeWidth, dd = tee.Depth;
            double e = ia.BracketBoltEdge;
            var bolts = new List<Vec2>();                                        // (a, cc) of each bolt
            var flange = new List<Vec2>();
            var mesh = new Mesh();
            double ropeB = ia.RopeOffsetFromWeb;
            if (anchor)
            {
                // Flange vertical on the web (bf tall), stem horizontal in the rope plane; bolt columns along the piece.
                int cols = Math.Max(2, (int)Math.Round((L - 2 * e) / Inch(3)) + 1);
                for (int i = 0; i < cols; i++)
                    foreach (double rr in new[] { -1.0, 1.0 })
                        bolts.Add(new Vec2(-L / 2 + e + i * (L - 2 * e) / (cols - 1), rr * (bf / 2 - e)));
                PlateBuilder.AddPrism(mesh, new[] { new Vec2(-L / 2, -bf / 2), new Vec2(L / 2, -bf / 2), new Vec2(L / 2, bf / 2), new Vec2(-L / 2, bf / 2) },
                    bolts.Select(q => new Hole(q, hd)).ToList(), (u, w, th) => Wd(u, th, w), _ => 0, _ => tf);
                // Stem: plane (a, b), thickness vertical; 5/8" shackle pin hole (11/16") at the rope line.
                PlateBuilder.AddPrism(mesh, new[] { new Vec2(-L / 2, tf), new Vec2(L / 2, tf), new Vec2(L / 2, dd), new Vec2(-L / 2, dd) },
                    new[] { new Hole(new Vec2(0, ropeB), Inch(11.0 / 16)) }, (u, w, th) => Wd(u, w, th), _ => -ts / 2, _ => ts / 2);
            }
            else
            {
                // Flange against the web (bf along the girder, L tall), stem vertical, perpendicular to the rope; rope hole in the stem.
                foreach (double aa in new[] { -1.0, 1.0 })
                    foreach (double rr in new[] { -1.0, 1.0 })
                        bolts.Add(new Vec2(aa * (bf / 2 - e), rr * (L / 2 - e)));
                PlateBuilder.AddPrism(mesh, new[] { new Vec2(-bf / 2, -L / 2), new Vec2(bf / 2, -L / 2), new Vec2(bf / 2, L / 2), new Vec2(-bf / 2, L / 2) },
                    bolts.Select(q => new Hole(q, hd)).ToList(), (u, w, th) => Wd(u, th, w), _ => 0, _ => tf);
                PlateBuilder.AddPrism(mesh, new[] { new Vec2(tf, -L / 2), new Vec2(dd, -L / 2), new Vec2(dd, L / 2), new Vec2(tf, L / 2) },
                    new[] { new Hole(new Vec2(ropeB, 0), Inch(0.875)) }, (u, w, th) => Wd(th, u, w), _ => -ts / 2, _ => ts / 2);
            }
            double grip = tf + twg;
            foreach (var q in bolts)
            {
                _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(Wd(q.X, tf, q.Y), tan, -nOut), Grip = grip, Length = bg.LengthForGrip(grip),
                    Assembly = mark, Diameter = bg.Diameter });
                _holes[gi].Web.Add(new Hole(new Vec2(s + q.X, zRope + q.Y), hd));   // girder web hole (s along the girder, z from top of web)
            }
            _parts.Add(new SteelPart
            {
                Mark = $"{mark}-B{++bn}", Category = PartCategory.InspectionBracket, Assembly = mark, Mesh = mesh, Length = L,
                Description = $"{tee.Name} x {In(L)} cable {(anchor ? "anchorage / support" : "support bracket")}, {bolts.Count} - {In(bg.Diameter)} bolts",
                Material = BracketMaterial, NominalWeightLbPerFt = tee.WeightPerFootLb,
            });
            rope.Add(Wd(0, ropeB, 0));
        }
        if (rope.Count >= 2)
        {
            var rm = new Mesh();
            RolledBuilder.AddTube(rm, rope, ia.RopeDiameter / 2, 8);
            double rl = 0;
            for (int i = 1; i < rope.Count; i++) rl += (rope[i] - rope[i - 1]).Length;
            _parts.Add(new SteelPart
            {
                Mark = $"{mark}-R", Category = PartCategory.InspectionCable, Assembly = mark, Mesh = rm, Length = rl,
                Description = $"{In(ia.RopeDiameter)} dia IWRC 6x19 galvanized wire rope", Material = "Galvanized IWRC 6x19 wire rope (RR-W-410)",
                NominalWeightLbPerFt = Math.Abs(ia.RopeDiameter - Inch(0.5)) < 1e-6 ? 0.42 : 0.42 * Math.Pow(ia.RopeDiameter / Inch(0.5), 2),
            });
        }
    }

    private static double UnitsFt(double ft) => Units.UnitSystem.Ft(ft);

    // ================================================================== walkways

    private void BuildWalkways(InspectionAccessDefinition ia)
    {
        if (ia.DefaultWalkway == WalkwayType.None && ia.Walkways.Count == 0) return;
        double D = Sec.WebDepth;
        // Supports of a walkway: the middle of each cross frame line between the girders, at the grating level.
        Vec3 Mid(Vec3 l, Vec3 r) => (l + r) * 0.5 + Up * (-D + ia.GratingAboveWebBottom);
        var jobs = new List<Action>();
        var pairs = _f.CrossFrames.GroupBy(cf => (cf.Bay, cf.RightGirder)).OrderBy(g => g.Key.Bay).ThenBy(g => g.Key.RightGirder).ToList();
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
                var a = Mid(frames[k].Left, frames[k].Right);
                var b = Mid(frames[k + 1].Left, frames[k + 1].Right);
                string m = $"IW-{bayName}-{k + 1}";
                int left = grp.Key.Bay, right = grp.Key.RightGirder;
                jobs.Add(() => BuildWalkwaySegment(ia, type, a, b, ia.WalkwayEndClearance, ia.WalkwayEndClearance, m, left, right));
            }
        }
        // Bays interrupted by a cross girder: the walkway ends 1" clear of the cross girder web (design sheet 167 detail) on the near
        // side (between each continuing girder and the discontinuous girder, through the web openings) and starts again on the far
        // side between the continuing girders. The grating is 2 1/2" above the bottom of the cross girder web there (sheet 165 note).
        foreach (var x in _f.CrossGirders)
        {
            var xgp = _def.Girders.CrossGirder;
            double clearXg = xgp.WebThickness / 2 + Inch(1);
            var (dz, tc) = CrossGirderShift(x);
            Vec3 OnXg(double frac)
            {
                var q = Vec3.Lerp(x.Left, x.Right, frac);
                return new Vec3(q.X, q.Y, q.Z + dz - xgp.FlangeThickness * 2 - xgp.WebDepth + ia.GratingAboveWebBottom + xgp.FlangeThickness);
            }
            var pts = new (int l, int r, Vec3 point, bool near)[]
            {
                (x.LeftGirder, x.Girder, OnXg(tc / 2), true),
                (x.Girder, x.RightGirder, OnXg((tc + 1) / 2), true),
                (x.LeftGirder, x.RightGirder, OnXg(0.5), false),
            };
            foreach (var (l, r, point, near) in pts)
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
                var a = near ? Mid(fr.Left, fr.Right) : point;
                var b = near ? point : Mid(fr.Left, fr.Right);
                string m = $"IW-{(char)('A' + l)}{(char)('A' + r)}-X{x.Girder + 1}{(near ? "n" : "f")}";
                int ll = l, rr2 = r;
                jobs.Add(() => BuildWalkwaySegment(ia, type, a, b, near ? ia.WalkwayEndClearance : clearXg, near ? clearXg : ia.WalkwayEndClearance, m, ll, rr2));
            }
        }
        Parallel.ForEach(jobs, j => j());
    }

    private void BuildWalkwaySegment(InspectionAccessDefinition ia, WalkwayType type, Vec3 A, Vec3 B, double clearA, double clearB, string mark, int leftGirder, int rightGirder)
    {
        var sec = type == WalkwayType.A ? ia.TypeA : ia.TypeB;
        var dir = B - A;
        double L0 = dir.Length;
        double Ls = L0 - clearA - clearB;
        if (L0 < 1e-3 || Ls < UnitsFt(2)) return;
        var axis = dir * (1.0 / L0);
        var yh = new Vec3(-axis.Y, axis.X, 0).Normalized();                  // left of travel, horizontal (grating is level across)
        var zz = Vec3.Cross(axis, yh);                                       // up, tilted with the grade
        var o = A + axis * clearA;
        var fr = new Frame3(o, axis, yh, zz);
        double W = sec.ClearWidth, tg = sec.GratingThickness;
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

        // grating (solid slab; nominal weight for the mass)
        var gm = new Mesh();
        MeshBuilder.AddBox(gm, fr, 0, Ls, -W / 2, W / 2, -tg, 0);
        _parts.Add(new SteelPart { Mark = $"{asm}-GR", Category = PartCategory.Grating, Assembly = asm, Mesh = gm, Length = Ls,
            Description = $"Welded grating {In(tg)} deep x {In(W)} wide", Material = "Galvanized welded steel grating",
            NominalWeightLbPerFt = sec.GratingWeightLbPerSqFt * (W / 0.3048) });

        // stringers
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

        // posts, diaphragms
        int nSp = Math.Max(1, (int)Math.Ceiling(Ls / sec.MaxPostSpacing - 1e-9));
        double tPost = postA.Thickness, tRail = railA.Thickness;
        double zTop = sec.TopRailHeight;
        var diaPos = new List<double>();
        for (int k = 0; k <= nSp; k++) diaPos.Add(k * Ls / nSp);
        if (typeA && sec.MidDiaphragmsOnLongSpans && Ls > sec.StringerSpanLimit)
            for (int k = 0; k < nSp; k++) diaPos.Add((k + 0.5) * Ls / nSp);
        diaPos.Sort();
        int dn = 0;
        foreach (double u in diaPos)
        {
            var m = new Mesh();
            if (typeA)
            {
                double hw = sec.DiaphragmPlateWidth / 2;
                double ud = Math.Clamp(u - hw, 0, Math.Max(0, Ls - 2 * hw));
                MeshBuilder.AddBox(m, fr, ud, ud + 2 * hw, -yOutDia, yOutDia, zDiaBot, zDiaTop);
                _parts.Add(new SteelPart { Mark = $"{asm}-D{++dn}", Category = PartCategory.WalkwayPlate, Assembly = asm, Mesh = m, Length = 2 * yOutDia,
                    Description = $"PL {In(sec.DiaphragmPlateThickness)} x {In(sec.DiaphragmPlateWidth)} diaphragm x {In(2 * yOutDia)}", Material = WalkwayMaterial });
            }
            else
            {
                double ua = Math.Clamp(u - diaAngle!.LegA / 2, 0, Math.Max(0, Ls - diaAngle.LegA));
                RolledBuilder.AddAngle(m, o + axis * ua + yh * (-yOutDia) + zz * zDiaTop, yh, axis, -zz, 2 * yOutDia, diaAngle.LegA, diaAngle.LegB,
                    diaAngle.Thickness, 0, Array.Empty<Hole>(), Array.Empty<Hole>());
                _parts.Add(new SteelPart { Mark = $"{asm}-D{++dn}", Category = PartCategory.Angle, Assembly = asm, Mesh = m, Length = 2 * yOutDia, Role = MemberRole.Diaphragm,
                    Description = $"{diaAngle.Name} diaphragm x {In(2 * yOutDia)}", Material = WalkwayMaterial, NominalWeightLbPerFt = diaAngle.WeightPerFootLb });
            }
        }
        // rail posts: L3x3x5/16, legs inward-of-the-rail face: y-leg across (3"), x-leg along the walkway (3"), foot on the diaphragm
        double zPostBot = zDiaTop, zPostTop = zTop - tRail;
        var postPoly = new[] { new Vec2(0, 0), new Vec2(postA.LegA, 0), new Vec2(postA.LegA, tPost), new Vec2(tPost, tPost), new Vec2(tPost, postA.LegB), new Vec2(0, postA.LegB) };
        int pn = 0;
        for (int k = 0; k <= nSp; k++)
        {
            double u = k * Ls / nSp;
            double dirU = k * 2 <= nSp ? 1 : -1;
            for (int i = 0; i < 2; i++)
            {
                double s = i == 0 ? -1 : 1;
                var m = new Mesh();
                RolledBuilder.AddExtrusion(m, postPoly, o + axis * (u - dirU * tPost / 2) + yh * (s * W / 2), axis * dirU, yh * s, zz, zPostBot, zPostTop);
                _parts.Add(new SteelPart { Mark = $"{asm}-P{++pn}", Category = PartCategory.WalkwayPost, Assembly = asm, Mesh = m, Length = zPostTop - zPostBot,
                    Description = $"{postA.Name} rail post x {In(zPostTop - zPostBot)}", Material = WalkwayMaterial, NominalWeightLbPerFt = postA.WeightPerFootLb });
            }
        }
        // top rails (angle: horizontal leg over the post, vertical leg down the inside), intermediate rails, toe boards
        var railPoly = new[] { new Vec2(0, zTop), new Vec2(railA.LegA, zTop), new Vec2(railA.LegA, zTop - tRail), new Vec2(tRail, zTop - tRail),
            new Vec2(tRail, zTop - railA.LegB), new Vec2(0, zTop - railA.LegB) };
        for (int i = 0; i < 2; i++)
        {
            double s = i == 0 ? -1 : 1;
            var m = new Mesh();
            RolledBuilder.AddExtrusion(m, railPoly, o + yh * (s * W / 2), yh * s, zz, axis, 0, Ls);
            _parts.Add(new SteelPart { Mark = $"{asm}-TR{i + 1}", Category = PartCategory.WalkwayRail, Assembly = asm, Mesh = m, Length = Ls,
                Description = $"{railA.Name} top rail", Material = WalkwayMaterial, NominalWeightLbPerFt = railA.WeightPerFootLb });
            var im = new Mesh();
            double vi0 = s > 0 ? W / 2 - sec.RailPlateThickness : -W / 2, vi1 = s > 0 ? W / 2 : -W / 2 + sec.RailPlateThickness;
            MeshBuilder.AddBox(im, fr, 0, Ls, vi0, vi1, sec.IntermediateRailHeight - sec.RailPlateWidth / 2, sec.IntermediateRailHeight + sec.RailPlateWidth / 2);
            _parts.Add(new SteelPart { Mark = $"{asm}-IR{i + 1}", Category = PartCategory.WalkwayRail, Assembly = asm, Mesh = im, Length = Ls,
                Description = $"PL {In(sec.RailPlateThickness)} x {In(sec.RailPlateWidth)} intermediate rail", Material = WalkwayMaterial });
            if (sec.ToeBoard)
            {
                var tb = new Mesh();
                MeshBuilder.AddBox(tb, fr, 0, Ls, vi0, vi1, sec.ToeBoardClearance, sec.ToeBoardClearance + sec.RailPlateWidth);
                _parts.Add(new SteelPart { Mark = $"{asm}-TB{i + 1}", Category = PartCategory.WalkwayPlate, Assembly = asm, Mesh = tb, Length = Ls,
                    Description = $"PL {In(sec.RailPlateThickness)} x {In(sec.RailPlateWidth)} toe board", Material = WalkwayMaterial });
            }
        }
    }
}
