using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Lateral (plan) bracing in selected bays at the top or bottom flange. Panel points are the cross-frame locations.
/// At each panel point a horizontal gusset is bolted to the flange (on the bay side of the web), coped around the
/// connection stiffener; single angles are welded to the gusset (heel and toe) with the connected leg flat on it.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private void BuildLateralBracing()
    {
        var lb = _def.LateralBracing;
        if (!lb.Enabled || _f.Girders.Count < 2) return;
        int nSpan = _f.Girders[0].Spans.Count;
        foreach (int bay1 in lb.Bays.Distinct())
        {
            int bay = bay1 - 1;
            if (bay < 0 || bay >= _f.Girders.Count - 1) { Warn($"Lateral bracing: bay {bay1} does not exist."); continue; }
            for (int span = 0; span < nSpan; span++)
            {
                if (lb.Spans.Count > 0 && !lb.Spans.Contains(span + 1)) continue;
                var pts = _f.CrossFrames.Where(c => c.Bay == bay && (c.Span == span || (c.AtSupport && c.SupportIndex == span + 1)))
                    .GroupBy(c => Math.Round(c.LeftGlobalDistance, 3)).Select(gr => gr.First())
                    .OrderBy(c => c.LeftGlobalDistance).ToList();
                if (pts.Count < 2) continue;
                string mark = $"LB-S{span + 1}-B{bay1}";
                _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.LateralBracing,
                    Description = $"{lb.Flange} flange lateral bracing, span {span + 1}, G{bay + 1}–G{bay + 2}" });
                BuildLateralBay(mark, bay, pts);
            }
        }
    }

    private sealed record PanelPoint(int Girder, double S, Vec2 Direction, int Side);

    private void BuildLateralBay(string mark, int bay, List<CrossFrameResult> frames)
    {
        var lb = _def.LateralBracing;
        var angle = SteelCatalog.Angle(lb.Angle);
        bool bottom = lb.Flange == LateralFlange.Bottom;
        double tg = lb.GussetThickness, d = Sec.WebDepth;
        double clr = _def.CrossFrames.MemberClearance, lap = _def.CrossFrames.WeldLap, ws = _def.CrossFrames.MemberWeldSize;
        double e = _def.Bolts.EdgeDistance, p = _def.Bolts.Pitch;
        double headR = _model.Bolt.HeadAcrossFlats / Math.Sqrt(3);

        // Members between panel points (left girder = bay, right girder = bay + 1).
        var members = new List<(PanelPoint a, PanelPoint b)>();
        for (int k = 0; k + 1 < frames.Count; k++)
        {
            var L0 = new PanelPoint(bay, frames[k].LeftGlobalDistance, frames[k].Direction, +1);
            var R0 = new PanelPoint(bay + 1, frames[k].RightGlobalDistance, frames[k].Direction, -1);
            var L1 = new PanelPoint(bay, frames[k + 1].LeftGlobalDistance, frames[k + 1].Direction, +1);
            var R1 = new PanelPoint(bay + 1, frames[k + 1].RightGlobalDistance, frames[k + 1].Direction, -1);
            members.Add(lb.Pattern == LateralPattern.Warren && k % 2 == 1 ? (R0, L1) : (L0, R1));
        }

        // Panel point geometry: gusset plane, bolt group (on the bay side of the web, both sides of the stiffener).
        var gusset = new Dictionary<(int, long), (PanelPoint pp, List<Vec2> sr, List<Vec2> bolts)>();
        (PanelPoint pp, List<Vec2> sr, List<Vec2> bolts) Panel(PanelPoint pp)
        {
            var key = (pp.Girder, (long)Math.Round(pp.S * 1000));
            if (gusset.TryGetValue(key, out var v)) return v;
            var path = _f.Girders[pp.Girder].Path;
            var (_, t, _) = path.At(pp.S);
            double tw = _web[pp.Girder].Thickness(pp.S);
            var stiff = StiffenerAt(pp.Girder, pp.S);
            double tp = stiff?.Plate.Thickness ?? 0;
            // Stiffener direction in (s, r) coordinates and its normal.
            var dsr = new Vec2(Vec2.Dot(pp.Direction, t), Vec2.Dot(pp.Direction, t.Right));
            double cosS = Math.Max(Math.Abs(dsr.Y), 0.3);
            double off = (tp / 2 + Inch(1.25)) / cosS;
            int n = Math.Max(2, lb.BoltsPerConnection);
            int rowsEach = Math.Max(1, (n + 3) / 4);
            var bolts = new List<Vec2>();
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < rowsEach; row++)
                    for (int line = 0; line < 2; line++)
                    {
                        double r = pp.Side * (tw / 2 + _def.SpliceDetail.FirstLineFromWeb + line * _def.SpliceDetail.FlangeGage);
                        double s = pp.S + side * (off + row * p) + r * dsr.X / Math.Max(Math.Abs(dsr.Y), 1e-6) * Math.Sign(dsr.Y);
                        bolts.Add(new Vec2(s, r));
                    }
            v = (pp, new List<Vec2>(), bolts);
            gusset[key] = v;
            return v;
        }

        var weld = new Mesh();
        double weldLen = 0;
        int mn = 1;
        foreach (var (pa, pb) in members)
        {
            var ga = _f.Girders[pa.Girder].Path;
            var gb = _f.Girders[pb.Girder].Path;
            double za = bottom ? -d + tg : -tg, zb = za;
            var wp1 = GirderPoint(ga, pa.S, 0, za);
            var wp2 = GirderPoint(gb, pb.S, 0, zb);
            var a = (wp2 - wp1).Normalized();
            double len = (wp2 - wp1).Length;
            var q = Vec3.Cross(Up, a).Normalized();
            var nl = Vec3.Cross(a, q).Normalized();
            if (nl.Z < 0) nl = -nl;
            int o = bottom ? 1 : -1;
            double ybar = angle.CentroidFromHeel, heel = -ybar, mlo = heel, mhi = heel + angle.LegA;

            double TrimFrom(PanelPoint pp, Vec3 start, Vec3 dirA)
            {
                var (pan, _, bolts) = Panel(pp);
                var path = _f.Girders[pp.Girder].Path;
                var (pt, tan, _) = path.At(pp.S);
                var stiff = StiffenerAt(pp.Girder, pp.S);
                double tp = stiff?.Plate.Thickness ?? 0;
                double tw = _web[pp.Girder].Thickness(pp.S);
                var dir2 = pp.Direction;
                var far = start + dirA * len;
                double sideOfPlane = Math.Sign(Vec2.Cross(dir2, far.XY - pt.XY));
                for (double tt = 0; tt < len / 2; tt += Inch(0.25))
                {
                    bool ok = true;
                    foreach (double m in new[] { mlo, mhi })
                    {
                        var c = (start + dirA * tt + q * m).XY;
                        var rel = c - pt.XY;
                        if (sideOfPlane * Vec2.Cross(dir2, rel) < tp / 2 + clr) { ok = false; break; }
                        if (pp.Side * Vec2.Dot(rel, tan.Right) < tw / 2 + clr) { ok = false; break; }
                    }
                    if (ok)
                        foreach (var bsr in bolts)
                        {
                            var bw = GirderPoint(path, bsr.X, bsr.Y, 0).XY;
                            double mb = Vec2.Dot(bw - start.XY, q.XY);
                            if (mb < mlo - headR - clr || mb > mhi + headR + clr) continue;
                            if (Vec2.Dot(bw - start.XY, dirA.XY) > tt - headR - clr) { ok = false; break; }
                        }
                    if (ok) return tt;
                }
                return len / 2;
            }

            double t0 = TrimFrom(pa, wp1, a);
            double t1 = len - TrimFrom(pb, wp2, -a);
            if (t1 - t0 < 2 * lap + Inch(1)) { Warn($"{mark}: lateral member too short; check bay geometry."); continue; }

            var sec = SteelCatalog.AnglePolygon(angle, heel, 1, 0, o);
            Vec3[] Ring(double tt) => sec.Select(s => wp1 + a * tt + q * s.X + nl * s.Y).ToArray();
            var mesh = new Mesh();
            MeshBuilder.AddLoft(mesh, sec, new[] { Ring(t0), Ring(t1) });
            _parts.Add(new SteelPart { Mark = $"{mark}-L{mn++}", Category = PartCategory.Angle, Role = MemberRole.Lateral, Assembly = mark,
                Mesh = mesh, Length = t1 - t0, Description = angle.Name, Material = "ASTM A709 Gr. 36", NominalWeightLbPerFt = angle.WeightPerFootLb });

            foreach (var (s0, s1, pp) in new[] { (t0, t0 + lap, pa), (t1 - lap, t1, pb) })
            {
                var (_, sr, _) = Panel(pp);
                var path = _f.Girders[pp.Girder].Path;
                var (pt, tan, _) = path.At(pp.S);
                double margin = ws + Inch(0.25);
                foreach (double tt in new[] { s0, s1 })
                    foreach (double m in new[] { mlo - margin, mhi + margin })
                    {
                        var w = (wp1 + a * tt + q * m).XY - pt.XY;
                        sr.Add(new Vec2(pp.S + Vec2.Dot(w, tan), Vec2.Dot(w, tan.Right)));
                    }
                double toe = heel + angle.LegA;
                weldLen += weld.AddHeld(wp1 + a * s0 + q * heel, wp1 + a * s1 + q * heel, -q, nl * o, ws, HoldBack(ws), HoldBack(ws));   // short of the angle end / gusset edge
                weldLen += weld.AddHeld(wp1 + a * s0 + q * toe, wp1 + a * s1 + q * toe, q, nl * o, ws, HoldBack(ws), HoldBack(ws));
            }
        }

        // Gussets (coped around the stiffener) and bolts through gusset + flange.
        int gn = 1;
        foreach (var (pp, sr, bolts) in gusset.Values)
        {
            if (sr.Count == 0) continue;
            var path = _f.Girders[pp.Girder].Path;
            var (_, tan, _) = path.At(pp.S);
            double tw = _web[pp.Girder].Thickness(pp.S);
            var prof = bottom ? _bot[pp.Girder] : _top[pp.Girder];
            double tf = prof.Thickness(pp.S);
            var pts = sr.Concat(bolts.SelectMany(b => new[] { b + new Vec2(-e, -e), b + new Vec2(e, -e), b + new Vec2(e, e), b + new Vec2(-e, e) })).ToList();
            var hull = MeshBuilder.ConvexHull(pts);
            // Keep off the web and the flange-to-web weld.
            hull = PlateBuilder.ClipHalfPlane(hull, new Vec2(0, pp.Side), tw / 2 + _def.Stiffeners.FlangeToWebWeld + Inch(0.25));
            var stiff = StiffenerAt(pp.Girder, pp.S);
            var pieces = new List<List<Vec2>>();
            if (stiff != null)
            {
                var dsr = new Vec2(Vec2.Dot(pp.Direction, tan), Vec2.Dot(pp.Direction, tan.Right)) * pp.Side;
                var nsr = new Vec2(-dsr.Y, dsr.X);
                var c0 = new Vec2(pp.S, 0);
                double half = stiff.Plate.Thickness / 2 + Inch(0.25);
                double xf = tw / 2 / Math.Max(Math.Abs(dsr.Y), 0.2);
                double tip = xf + stiff.Plate.Width + Inch(0.25);
                pieces.Add(PlateBuilder.ClipHalfPlane(hull, nsr, Vec2.Dot(c0, nsr) + half));
                pieces.Add(PlateBuilder.ClipHalfPlane(hull, -nsr, -(Vec2.Dot(c0, nsr) - half)));
                var mid = PlateBuilder.ClipHalfPlane(hull, -nsr, -(Vec2.Dot(c0, nsr) + half));
                mid = PlateBuilder.ClipHalfPlane(mid, nsr, Vec2.Dot(c0, nsr) - half);
                pieces.Add(PlateBuilder.ClipHalfPlane(mid, dsr, Vec2.Dot(c0, dsr) + tip));
            }
            else pieces.Add(hull);

            var mesh = new Mesh();
            double z0 = bottom ? -d : -tg, z1 = bottom ? -d + tg : 0;
            foreach (var piece in pieces.Where(pc => pc.Count >= 3 && Math.Abs(MeshBuilder.SignedArea(pc)) > 1e-6))
            {
                var hs = bolts.Where(b => PointInConvex(piece, b)).Select(b => new Hole(b, HoleDia)).ToList();
                PlateBuilder.AddPrism(mesh, piece, hs, (s, r, z) => GirderPoint(path, s, r, z), _ => z0, _ => z1);
            }
            double w = hull.Max(x => x.X) - hull.Min(x => x.X), h = hull.Max(x => x.Y) - hull.Min(x => x.Y);
            _parts.Add(new SteelPart { Mark = $"{mark}-G{gn++}", Category = PartCategory.Gusset, Assembly = mark, Mesh = mesh, Length = Math.Max(w, h),
                Description = $"Lateral gusset {Pl(h, tg)} x {In(w)}" + (stiff != null ? ", coped at stiffener" : "") });

            double grip = tg + tf;
            double blen = _model.Bolt.LengthForGrip(grip);
            foreach (var b in bolts)
            {
                (bottom ? _holes[pp.Girder].Bottom : _holes[pp.Girder].Top).Add(new Hole(b, HoleDia));
                var origin = GirderPoint(path, b.X, b.Y, bottom ? -d + tg : -tg);
                var t3 = new Vec3(tan.X, tan.Y, 0);
                _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(origin, t3, bottom ? -Up : Up), Grip = grip, Length = blen, Assembly = mark,
                    Diameter = _model.Bolt.Diameter });
            }
        }
        _parts.Add(new SteelPart { Mark = $"{mark}-W", Category = PartCategory.Weld, Assembly = mark, Mesh = weld, Material = "E70XX",
            Length = weldLen, Description = $"Fillet weld {In(ws)}, lateral angles to gussets{HoldNote(ws)}", Assumption = WeldFlag(ws) });
    }

    private StiffenerResult? StiffenerAt(int girder, double s) =>
        _f.Stiffeners.FirstOrDefault(x => x.Girder == girder && x.Kind != StiffenerKind.Intermediate && Math.Abs(x.GirderDistance - s) < 0.02);

    private static bool PointInConvex(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        double sign = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            double c = Vec2.Cross(poly[(i + 1) % poly.Count] - poly[i], p - poly[i]);
            if (Math.Abs(c) < 1e-12) continue;
            if (sign == 0) sign = Math.Sign(c);
            else if (Math.Sign(c) != sign) return false;
        }
        return true;
    }
}
