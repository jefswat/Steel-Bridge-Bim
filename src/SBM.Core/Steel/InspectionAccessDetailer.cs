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

    private void BuildInspectionAccess()
    {
        var ia = _def.InspectionAccess;
        if (ia is not { Enabled: true } || _def.Girders.IsPrestressed) return;
        int n = 0;
        foreach (var c in ia.Cables) BuildInspectionCable(ia, c, ++n);
    }

    /// <summary>Walkways run after the connections phase (they read the generated cross frames / cross girders).</summary>
    private void BuildInspectionWalkways()
    {
        var ia = _def.InspectionAccess;
        if (ia is not { Enabled: true } || _def.Girders.IsPrestressed) return;
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
        double side0 = c.LeftFace ? -1 : 1;
        var facePlates = new List<(double lo, double hi, string what)>();
        foreach (var st in _f.Stiffeners.Where(st => st.Girder == gi && st.GirderDistance > s0 - 1 && st.GirderDistance < s1 + 1))
        {
            if (c.LeftFace ? st.Side is not (StiffenerSide.Both or StiffenerSide.Left) : st.Side is not (StiffenerSide.Both or StiffenerSide.Right)) continue;
            var (_, tt, _) = _f.Girders[gi].Path.At(st.GirderDistance);
            double along = Vec2.Dot(st.Direction, tt) * side0, tw0 = _web[gi].Thickness(st.GirderDistance);
            double reach = tw0 / 2 + st.Plate.Width + Inch(1);
            double e0 = 0, e1 = along * reach;
            facePlates.Add((st.GirderDistance + Math.Min(e0, e1) - st.Plate.Thickness, st.GirderDistance + Math.Max(e0, e1) + st.Plate.Thickness,
                $"{st.Kind} stiffener at {In(st.GirderDistance)}"));
        }
        double anchorHalf = ia.AnchorBracketLength / 2;
        bool FreeForAnchor(double sx) => facePlates.All(f => f.hi <= sx - anchorHalf || f.lo >= sx + anchorHalf);
        int sections = Math.Max(1, (int)Math.Ceiling(length / ia.MaxAnchorSpacing - 1e-9));
        var anchors = new List<double> { s0 };
        for (int k = 1; k < sections; k++)
        {
            double target = s0 + length * k / sections;
            double near = stiff.Count > 0 ? stiff.MinBy(s => Math.Abs(s - target)) : target;
            if (ia.StiffenerRopeHole != null && stiff.Count > 1)
            {
                // as designed (cable sheet "TYPICAL CABLE ELEVATION"): an intermediate support is at 1/2 the stiffener spacing,
                // midway between the two cross frame stiffeners around the target
                // (the nearest such midpoint that is clear of stiffeners on the cable face, e.g. not on a pier's bearing stiffeners)
                var mids = Enumerable.Range(0, stiff.Count - 1).Select(j => (stiff[j] + stiff[j + 1]) / 2).OrderBy(q => Math.Abs(q - target)).ToList();
                near = mids.FirstOrDefault(FreeForAnchor, mids[0]);
            }
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
        // Stiffener plates on the cable face, as intervals along the girder (skewed plates sweep along it). As designed (cable sheet:
        // support brackets at cross frame stiffeners on the OTHER side of the web, Section C-C: the rope passes stiffeners on its own face
        // through a hole), a support bracket is not placed where a plate on the cable face takes its place; an anchorage there is reported.
        if (ia.StiffenerRopeHole != null)
        {
            var keep = new List<(double s, bool anchor)>();
            foreach (var it in items)
            {
                double half = it.anchor ? ia.AnchorBracketLength / 2 : (SteelCatalog.FindTee(ia.SupportBracket)?.FlangeWidth ?? Inch(6)) / 2;
                var hit = facePlates.FirstOrDefault(f => f.hi > it.s - half && f.lo < it.s + half);
                if (hit.what == null) { keep.Add(it); continue; }
                if (!it.anchor) continue;   // the rope passes this stiffener through its hole
                bool atEnd = Math.Abs(it.s - s0) < 1e-6 || Math.Abs(it.s - s1) < 1e-6;
                Warn(UnresolvedFlag.Text(ia.Source ?? "inspection cable details", $"G{gi + 1} inspection cable {mark}: {(atEnd ? "end" : "intermediate")} anchorage at {In(it.s)} along the girder",
                    atEnd ? $"it overlaps the {hit.what} on the cable face; the cable sheet places the end anchorage 8\" from the CL bearing with the bearing stiffeners \"not shown for clarity\""
                          : $"it overlaps the {hit.what} on the cable face and no stiffener midpoint near the 200'-0 section limit is clear",
                    "anchorage kept"));
                keep.Add(it);
            }
            items = keep;
        }
        double D = Sec.WebDepth, zRope = -(D - ia.RopeHeightAboveWebBottom);
        var bg = new BoltGeometry(ia.BracketBoltDiameter);
        double hd = bg.HoleDiameter;
        var path = _f.Girders[gi].Path;
        var rope = new List<(double s, Vec3 p, Vec3 tan, bool anchor, double len)>();
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
            rope.Add((s, Wd(0, ropeB, 0), tan, anchor, L));
        }
        // Rope holes in the girder stiffeners the rope passes on this face (cable sheet Section C-C).
        if (ia.StiffenerRopeHole is double rh)
        {
            var bySupport = new List<(double s, double d)>();
            if (ia.StiffenerRopeHoleAtSupports != null)
                for (int j = 0; j < _f.Supports.Count; j++)
                    if (ia.StiffenerRopeHoleAtSupports.TryGetValue(_def.SupportAt(j).Name, out double dj)) bySupport.Add((BearingPathDistance(gi, j), dj));
            double off = side * (_web[gi].Thickness(s0) / 2 + ia.RopeOffsetFromWeb);
            foreach (var st in _f.Stiffeners.Where(st => st.Girder == gi && st.GirderDistance > s0 && st.GirderDistance < s1))
            {
                if (c.LeftFace ? st.Side is not (StiffenerSide.Both or StiffenerSide.Left) : st.Side is not (StiffenerSide.Both or StiffenerSide.Right)) continue;
                double dia = rh;
                foreach (var (sb2, d2) in bySupport) if (Math.Abs(st.GirderDistance - sb2) < UnitsFt(3)) dia = d2;
                // rope line point in the (possibly skewed) stiffener plane
                double sx = st.GirderDistance;
                Vec2 R(double ss) { var (pp, tt, _) = path.At(ss); return pp.XY + tt.Right * off; }
                double F(double ss) => Vec2.Cross(R(ss) - st.Location.XY, st.Direction);
                for (int it = 0; it < 4; it++)
                {
                    double h = 1e-3, f0 = F(sx), df = (F(sx + h) - f0) / h;
                    if (Math.Abs(df) < 1e-9) break;
                    sx -= f0 / df;
                }
                var rp = R(sx);
                var hp = new Vec3(rp.X, rp.Y, path.At(sx).point.Z + zRope);
                AddStiffenerHole(gi, st.GirderDistance, hp, dia);
                // the rope passes through the hole: a point of the rope line (unless a bracket / anchorage is right there)
                if (rope.All(q => Math.Abs(q.s - sx) > Inch(6))) { var (_, tt, _) = path.At(sx); rope.Add((sx, hp, new Vec3(tt.X, tt.Y, 0), false, 0)); }
            }
            rope.Sort((p1, p2) => p1.s.CompareTo(p2.s));
        }
        if (rope.Count >= 2)
        {
            var rm = new Mesh();
            double rl = 0;
            if (ia.RopeEndsAtAnchorStem == true)
            {
                // one rope piece per section between anchorages, from stem end to stem end (shackle / turnbuckle hardware not modeled)
                var piece = new List<Vec3>();
                for (int i = 0; i < rope.Count; i++)
                {
                    var r = rope[i];
                    if (r.anchor && piece.Count > 0)
                    {
                        piece.Add(r.p - r.tan * (r.len / 2));
                        RolledBuilder.AddTube(rm, piece, ia.RopeDiameter / 2, 8);
                        for (int q = 1; q < piece.Count; q++) rl += (piece[q] - piece[q - 1]).Length;
                        piece = new List<Vec3>();
                    }
                    if (r.anchor) { if (i < rope.Count - 1) piece.Add(r.p + r.tan * (r.len / 2)); }
                    else piece.Add(r.p);
                }
            }
            else
            {
                var pts = rope.Select(r => r.p).ToList();
                RolledBuilder.AddTube(rm, pts, ia.RopeDiameter / 2, 8);
                for (int i = 1; i < pts.Count; i++) rl += (pts[i] - pts[i - 1]).Length;
            }
            _parts.Add(new SteelPart
            {
                Mark = $"{mark}-R", Category = PartCategory.InspectionCable, Assembly = mark, Mesh = rm, Length = rl,
                Description = $"{In(ia.RopeDiameter)} dia IWRC 6x19 galvanized wire rope", Material = "Galvanized IWRC 6x19 wire rope (RR-W-410)",
                NominalWeightLbPerFt = Math.Abs(ia.RopeDiameter - Inch(0.5)) < 1e-6 ? 0.42 : 0.42 * Math.Pow(ia.RopeDiameter / Inch(0.5), 2),
            });
        }
    }

    private static double UnitsFt(double ft) => Units.UnitSystem.Ft(ft);

}
