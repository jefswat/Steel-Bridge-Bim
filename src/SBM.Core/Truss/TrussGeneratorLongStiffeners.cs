using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        /// <summary>Rivets of the longitudinal stiffeners (placed with the end connection / stiffener rivets, after every part is built).</summary>
        private readonly List<(Vec3 q, Vec3 dir, Vec3 hint, double dia, string asm, string? mat, Assumption? asu)> _longRivets = new();
        private int _longPieces;

        /// <summary>Along-member extent [min, max] of a stiffener set at distance <paramref name="d"/> on face <paramref name="sx"/>, measured at member y
        /// <paramref name="y"/> (plumb stiffeners lean relative to a member on a grade); null = the set does not reach that height.</summary>
        private (double a, double b)? StiffenerExtentAt(Inst m, TrussStiffenerSet set, double d, double y)
        {
            double yc = m.Sec.WebCenterY + m.Def.OffsetZ;
            double vb = set.Top is { } st0 ? st0 + m.Def.OffsetZ - set.Height : yc - set.Height / 2;     // bottom of the stiffener (member y, or vertical for plumb)
            var (ax2, vy) = StiffFrame(m, set, d);
            double upY = Vec3.Dot(vy, m.Y), upA = Vec3.Dot(vy, m.Axis);
            if (Math.Abs(upY) < 1e-6) return null;
            // along-member legs of the set from the heel at d
            var segs = new List<(double a0, double a1)>();
            if (set.PlateWidth > 0 && set.PlateThickness > 0) segs.Add((-set.PlateWidth / 2, set.PlateWidth / 2));
            else
            {
                var ad = TrussSections.ParseAngle(set.Angle);
                if (ad == null) return null;
                double legWeb = Math.Max(ad.Value.b, set.FillThickness > 0 && set.FillWidth > 0 ? set.FillWidth : 0);
                foreach (int sa in set.LegToward != 0 ? new[] { Math.Sign(set.LegToward) } : set.PerFace >= 2 ? new[] { -1, 1 } : new[] { 1 })
                    segs.Add((Math.Min(0, sa * legWeb), Math.Max(0, sa * legWeb)));
            }
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var (a0, a1) in segs)
                foreach (double a in new[] { a0, a1 })
                {
                    // point at section offset a along ax2 whose member y is the target: v (along vy) solves a Dot(ax2, Y) + v upY = y
                    double v = (y - a * Vec3.Dot(ax2, m.Y)) / upY;
                    if (v < vb - 1e-6 || v > vb + set.Height + 1e-6) return null;
                    double along = d + a * Vec3.Dot(ax2, m.Axis) + v * upA;
                    lo = Math.Min(lo, along); hi = Math.Max(hi, along);
                }
            return lo <= hi ? (lo, hi) : null;
        }

        /// <summary>+1 / -1 = the section +x / -x face of the member looks toward the alignment (the inside of a girder pair; +1 on the alignment).</summary>
        private int FaceTowardAlignment(Inst m)
        {
            var mid = (m.A + m.B) * 0.5;
            var inv = _al.Inverse(mid.XY);
            var c = _al.PointAt(inv.Station, 0);
            double dx = c.X - mid.X, dy = c.Y - mid.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < 1e-6) return 1;
            return m.X.X * dx + m.X.Y * dy >= 0 ? 1 : -1;
        }

        /// <summary>Longitudinal web stiffener angles (<see cref="TrussMemberDefinition.LongitudinalStiffeners"/>).</summary>
        private void BuildLongStiffeners(Inst m)
        {
            double tw = m.Sec.WebThickness > 0 ? m.Sec.WebThickness : 2 * m.Sec.HalfWidth;
            double len = (m.B - m.A).Length;
            int k = 0;
            foreach (var ls in m.Def.LongitudinalStiffeners!)
            {
                var ad = TrussSections.ParseAngle(ls.Angle);
                if (ad == null) { Warn($"Truss: longitudinal stiffener '{ls.Label}' of {m.Mark} - angle '{ls.Angle}' not understood; not built{Src}."); continue; }
                var (legOut, legWeb, t) = ad.Value;
                int sx = ls.Side != 0 ? Math.Sign(ls.Side) : FaceTowardAlignment(m);
                int ys = ls.LegDown ? -1 : 1;
                double y = ls.Y + m.Def.OffsetZ, yOther = y + ys * legWeb;
                double s0 = Math.Max(m.SA, ls.From ?? m.SA), s1 = Math.Min(len - m.SB, ls.To ?? len - m.SB);
                if (s1 - s0 < 1e-3) continue;
                // gaps at the stiffeners (both heights of the web leg), merged
                var gaps = new List<(double a, double b)>();
                if (ls.StopAtStiffeners && m.Def.Stiffeners != null)
                    foreach (var set in m.Def.Stiffeners)
                        foreach (double d in set.Distances)
                        {
                            var e1 = StiffenerExtentAt(m, set, d, y); var e2 = StiffenerExtentAt(m, set, d, yOther);
                            if (e1 == null && e2 == null) continue;
                            double a = Math.Min(e1?.a ?? double.MaxValue, e2?.a ?? double.MaxValue), b = Math.Max(e1?.b ?? double.MinValue, e2?.b ?? double.MinValue);
                            gaps.Add((a - ls.Clearance, b + ls.Clearance));
                        }
                gaps.Sort((p, q) => p.a.CompareTo(q.a));
                var pieces = new List<(double a, double b)>();
                double cur = s0;
                foreach (var (ga, gb) in gaps)
                {
                    if (gb <= cur) continue;
                    if (ga > cur) pieces.Add((cur, Math.Min(ga, s1)));
                    cur = Math.Max(cur, gb);
                    if (cur >= s1) break;
                }
                if (cur < s1) pieces.Add((cur, s1));
                pieces.RemoveAll(p => p.b - p.a < Math.Max(2 * t, 0.025));
                if (pieces.Count == 0) { Warn($"Truss: longitudinal stiffener '{ls.Label}' of {m.Mark} - no length left between the stiffeners; not built{Src}."); continue; }
                double x0 = sx * tw / 2;
                var poly = new[]
                {
                    new Vec2(x0, y), new Vec2(x0 + sx * legOut, y), new Vec2(x0 + sx * legOut, y + ys * t), new Vec2(x0 + sx * t, y + ys * t),
                    new Vec2(x0 + sx * t, y + ys * legWeb), new Vec2(x0, y + ys * legWeb),
                };
                if (MeshBuilder.SignedArea(poly.ToList()) < 0) Array.Reverse(poly);
                var mesh = new Mesh();
                Assumption? asu = ls.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(ls.Unresolved + Src, "longitudinal stiffener as listed / scaled") : null;
                if (asu != null) Warn(ls.Unresolved);
                var rasu = RivetFlag(ls.Unresolved.Length > 0 ? ls.Unresolved : null, "longitudinal stiffener rivets at the drawn pitch") ?? asu;
                double total = 0;
                foreach (var (a, b) in pieces)
                {
                    var ra = poly.Select(q => m.A + m.Axis * a + m.X * q.X + m.Y * q.Y).ToArray();
                    MeshBuilder.AddLoft(mesh, poly, new[] { ra, ra.Select(p => p + m.Axis * (b - a)).ToArray() });
                    total += b - a;
                    _longPieces++;
                    if (ls.RivetDiameter <= 0 || ls.RivetPitch <= 0) continue;
                    double L = b - a, e = Math.Max(ls.RivetEdge, ls.RivetDiameter);
                    if (L < 2 * e) continue;
                    int n = (int)Math.Floor((L - 2 * e) / ls.RivetPitch + 1e-9) + 1;
                    double first = a + (L - (n - 1) * ls.RivetPitch) / 2;
                    double yr = y + ys * (ls.RivetGage > 0 ? ls.RivetGage : legWeb / 2);
                    for (int i = 0; i < n; i++)
                        _longRivets.Add((m.A + m.Axis * (first + i * ls.RivetPitch) + m.Y * yr + m.X * (x0 + sx * t / 2), m.X * sx, m.Axis, ls.RivetDiameter, m.Assembly, ls.RivetMaterial, rasu));
                }
                k++;
                string mark = $"{m.Mark}-LS{k}";
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = mark, Category = PartCategory.IntermediateStiffener, Assembly = m.Assembly, Mesh = mesh, Material = Material(ls.Material), Length = total, Assumption = asu,
                    Description = $"{(ls.Label.Length > 0 ? ls.Label : "longitudinal stiffener " + ls.Angle)} on {m.Mark} ({pieces.Count} pieces){(ls.Source.Length > 0 ? " (" + ls.Source + ")" : "")}",
                });
                if (Model.PanelOf.TryGetValue(m.Mark, out var pnl)) Model.PanelOf[mark] = pnl;
            }
        }
    }
}
