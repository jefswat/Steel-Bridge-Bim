using System.Collections.Concurrent;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

// Piles at as-designed positions, plumb or battered.
public sealed partial class SubstructureGenerator
{
    /// <summary>One pile axis (world coordinates) for the clearance check.</summary>
    private readonly record struct PileAxis(string Where, int Index, Vec3 Head, Vec3 Tip, double Width, bool Battered);
    private readonly ConcurrentQueue<PileAxis> _pileAxes = new();

    /// <summary>
    /// Piles at explicit as-designed positions (relative to the footing center). Battered piles (FootingPoint.BatterDir*):
    /// the plan positions are the pile positions at the FOOTING BOTTOM plane ("pile dimensions are at bottom of footing");
    /// the axis rises through that point toward the inside (opposite the batter direction) at 1 horizontal per
    /// <paramref name="batterRatio"/> vertical, so the head (footing bottom + embedment) is slightly inside the plan point and
    /// the tip moves outward. The length is measured ALONG THE AXIS from the head centre (assumption, not stated on the plans).
    /// Head and tip are cut horizontal ("battered piles shall be trimmed to a horizontal line"). The cross-section stays
    /// perpendicular to the axis, in the same orientation as a plumb pile (flanges parallel to the cap line, web across it —
    /// minimal rotation of the plumb section), so the pile reads like its plumb neighbours; no rotation is invented.
    /// </summary>
    private void AddPilesAt(SubstructureUnit unit, SubstructureDefinition sd, FoundationInfo f, double s, double n, double capBottom,
        List<FootingPoint> at, double length, double batterRatio, string where)
    {
        double top = capBottom + sd.PileEmbedment;
        bool steel = sd.PileType == PileType.SteelHP;
        string size = steel ? sd.PileSection : $"{Sh(sd.PileSize)} square precast";
        var section = PileSection(sd);
        double width = sd.PileType == PileType.PrecastConcrete ? sd.PileSize : section.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y))) * 2;
        double h = batterRatio > 0 ? 1.0 / batterRatio : 0;
        foreach (var q in at)
        {
            double ps = s + q.S, pn = n + q.N;
            var m = new Mesh();
            string batter = "";
            PileInfo info;
            if (!q.IsBattered || h <= 0)
            {
                double tip = top - length;
                info = new PileInfo(ps, pn, top, tip);
                MeshBuilder.AddExtrusion(m, section, Frame(unit, ps, pn), tip, top);
            }
            else
            {
                // Direction vectors in cap coordinates (S, N, Z), z up. Dir = unit plan vector of the tip movement.
                double dl = Math.Sqrt(q.BatterDirS * q.BatterDirS + q.BatterDirN * q.BatterDirN);
                double us = q.BatterDirS / dl, un = q.BatterDirN / dl;
                var a = new Vec3(-us * h, -un * h, 1).Normalized();         // axis, tip to head
                double drop = length * a.Z;                                  // vertical tip-to-head distance
                double hs = ps - us * h * sd.PileEmbedment, hn = pn - un * h * sd.PileEmbedment;   // head centre
                double ts = hs + us * h * drop, tn = hn + un * h * drop;
                info = new PileInfo(hs, hn, top, top - drop, ts, tn);
                // Minimal rotation of the frame (x = S, y = N, z = up) taking z onto the axis (Rodrigues).
                var k = Vec3.Cross(new Vec3(0, 0, 1), a);
                double sinT = k.Length, cosT = a.Z;
                k = k.Normalized();
                Vec3 Rot(Vec3 v) => v * cosT + Vec3.Cross(k, v) * sinT + k * (Vec3.Dot(k, v) * (1 - cosT));
                var ex = Rot(new Vec3(1, 0, 0)); var ey = Rot(new Vec3(0, 1, 0));
                var head = new Vec3[section.Length];
                var tipRing = new Vec3[section.Length];
                for (int i = 0; i < section.Length; i++)
                {
                    var p = ex * section[i].X + ey * section[i].Y;           // offset from the axis, perpendicular to it
                    p += a * (-p.Z / a.Z);                                   // slide along the axis to the head plane (z = 0)
                    head[i] = unit.World(hs + p.X, hn + p.Y, top);
                    tipRing[i] = head[i] - UnitVec(unit, a) * length;
                }
                MeshBuilder.AddLoft(m, section, new[] { tipRing, head });
                batter = $", battered 1:{batterRatio:0.##} toward {BatterWord(unit, us, un)}";
            }
            f.Piles.Add(info);
            var hw = unit.World(info.S, info.N, info.TopZ); var tw = unit.World(info.TipS, info.TipN, info.TipZ);
            _pileAxes.Enqueue(new PileAxis(where, f.Piles.Count, hw, tw, width, info.IsBattered));
            unit.Solids.Add(new SubSolid(SubElementKind.Pile, $"{where} pile {f.Piles.Count}",
                $"Pile {size}, {L(length)}{(info.IsBattered ? " along axis" : "")}{batter}, tip {El(info.TipZ)}", m, IsConcrete: !steel));
        }
    }

    /// <summary>Cap-coordinate vector (S, N, Z) to world.</summary>
    private static Vec3 UnitVec(SubstructureUnit unit, Vec3 v) => new(unit.D.X * v.X + unit.N.X * v.Y, unit.D.Y * v.X + unit.N.Y * v.Y, v.Z);

    private static string BatterWord(SubstructureUnit unit, double us, double un)
    {
        string sPart = Math.Abs(us) > 0.01 ? (us > 0 ? "+S" : "-S") : "", nPart = Math.Abs(un) > 0.01 ? (un > 0 ? "+N" : "-N") : "";
        return sPart != "" && nPart != "" ? $"{nPart}/{sPart} (diagonal)" : sPart + nPart;
    }

    /// <summary>Warns where pile axes come closer than a pile width (battered tips running into other piles, also of
    /// another footing). Simple segment-to-segment axis distance; only pairs with at least one battered pile are tested.</summary>
    private void CheckPileClearances()
    {
        var all = _pileAxes.ToArray();
        var bat = all.Where(p => p.Battered).ToArray();
        if (bat.Length == 0) return;
        var hits = new Dictionary<(string, string), (int count, double min)>();
        foreach (var b in bat)
            foreach (var o in all)
            {
                if (o.Where == b.Where && o.Index == b.Index) continue;
                if (o.Battered && string.CompareOrdinal(o.Where + o.Index.ToString("D4"), b.Where + b.Index.ToString("D4")) < 0) continue;   // count a battered pair once
                double w = Math.Max(b.Width, o.Width);
                if (Math.Abs(b.Head.X - o.Head.X) > 40 || Math.Abs(b.Head.Y - o.Head.Y) > 40) continue;
                double d = SegmentDistance(b.Head, b.Tip, o.Head, o.Tip);
                if (d >= w) continue;
                var key = (b.Where, o.Where);
                hits.TryGetValue(key, out var h);
                hits[key] = (h.count + 1, h.count == 0 ? d : Math.Min(h.min, d));
            }
        foreach (var ((wa, wb), (count, min)) in hits.OrderBy(k => k.Key.Item1).ThenBy(k => k.Key.Item2))
            Warn(wa == wb
                ? $"{wa}: {count} pile pair(s) have axes closer than a pile width (min {Sh(min)}) — battered piles run into each other."
                : $"{wa} battered piles come within {Sh(min)} of {wb} piles ({count} pair(s)) — check the batter against the neighbouring footing.");
    }

    /// <summary>Minimum distance between segments p0-p1 and q0-q1 (Ericson, Real-Time Collision Detection).</summary>
    public static double SegmentDistance(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2)
    {
        var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2;
        double a = Vec3.Dot(d1, d1), e = Vec3.Dot(d2, d2), f = Vec3.Dot(d2, r), s, t;
        const double eps = 1e-12;
        if (a <= eps && e <= eps) return r.Length;
        if (a <= eps) { s = 0; t = Math.Clamp(f / e, 0, 1); }
        else
        {
            double c = Vec3.Dot(d1, r);
            if (e <= eps) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
            else
            {
                double b = Vec3.Dot(d1, d2), den = a * e - b * b;
                s = den > eps ? Math.Clamp((b * f - c * e) / den, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((b - c) / a, 0, 1); }
            }
        }
        return ((p1 + d1 * s) - (p2 + d2 * t)).Length;
    }
}
