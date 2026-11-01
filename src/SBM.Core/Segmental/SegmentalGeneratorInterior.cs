using System.Text.RegularExpressions;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL segment interiors (SegmentalDefinition.Interiors, SegmentalDetails.cs): a bent half / end segment built as consecutive closed pieces along it,
// each with the standard outer outline and its own void (or solid). null = the standard hollow / solid rule, output unchanged.
public sealed partial class SegmentalGenerator
{
    private sealed record InteriorPiece(SegmentalInteriorPiece P, List<Vec2>? V0, List<Vec2>? V1, int Cl, List<Vec2>? O0, List<Vec2>? O1, int OCl);
    private sealed record InteriorUse(SegmentalInteriorDefinition Def, double SF, double Dir, List<InteriorPiece> Pieces);

    private readonly Dictionary<string, InteriorUse> _interior = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Support-side face (sF) and other face (sO) of a segment: bent half = the face nearer its bent CL; a segment without a bent (end
    /// segment) = its free face (no neighbouring segment). Null when it cannot be told.</summary>
    private (double sF, double sO)? SupportFace(SegmentResult r, SegmentDefinition d)
    {
        if (_bentSta.TryGetValue(d.Bent, out double bs))
            return Math.Abs(r.Station0 - bs) <= Math.Abs(r.Station1 - bs) ? (r.Station0, r.Station1) : (r.Station1, r.Station0);
        bool lowFree = !_segs.Any(o => o.r != r && Math.Abs(o.r.Station1 - r.Station0) < 1e-6);
        bool highFree = !_segs.Any(o => o.r != r && Math.Abs(o.r.Station0 - r.Station1) < 1e-6);
        if (lowFree == highFree) return null;
        return lowFree ? (r.Station0, r.Station1) : (r.Station1, r.Station0);
    }

    /// <summary>Void outline in loft order: starts at the top point on x = 0, runs to +x first; returns the index of the bottom point on x = 0.</summary>
    private static (List<Vec2>? v, int cl) LoftVoid(List<PlanPoint> pts)
    {
        if (pts.Count < 3) return (null, -1);
        var l = pts.Select(p => p.ToVec2()).ToList();
        var onCl = Enumerable.Range(0, l.Count).Where(i => Math.Abs(l[i].X) < 1e-6).ToList();
        if (onCl.Count < 2) return (null, -1);
        int top = onCl.OrderBy(i => l[i].Y).First();
        l = l.Skip(top).Concat(l.Take(top)).ToList();
        if (l[1].X < 0) { l.Reverse(); l = l.Skip(l.Count - 1).Concat(l.Take(l.Count - 1)).ToList(); }
        int cl = Enumerable.Range(1, l.Count - 1).Where(i => Math.Abs(l[i].X) < 1e-6).OrderByDescending(i => l[i].Y).FirstOrDefault(-1);
        return cl < 0 ? (null, -1) : (l, cl);
    }

    /// <summary>Resolves the interiors per segment (after every segment result exists, before the solids are lofted).</summary>
    private void SetupInteriors()
    {
        if (_d.Interiors is not { Count: > 0 }) return;
        foreach (var def in _d.Interiors)
        {
            if (def.Unresolved.Length > 0) Warn(def.Unresolved);
            var bad = new List<string>();
            var pieces = new List<InteriorPiece>();
            foreach (var p in def.Pieces.OrderBy(p => p.From))
            {
                string at = $"piece {p.From / Ft(1):0.###}-{p.To / Ft(1):0.###} ft";
                if (!(p.To > p.From)) { bad.Add($"{at} has no length"); continue; }
                List<Vec2>? o0 = null, o1 = null; int oc = SegmentalSection.OuterCl;
                if (p.Outer0 != null)
                {
                    (o0, oc) = LoftVoid(p.Outer0);
                    int oc1; (o1, oc1) = p.Outer1 == null ? (o0, oc) : LoftVoid(p.Outer1);
                    if (o0 == null || o1 == null || o0.Count != o1.Count || oc != oc1) { bad.Add($"{at}: outer outlines need a top and a bottom point on x = 0 and the same point count at both ends"); continue; }
                }
                if (p.Void0.Count == 0) { pieces.Add(new(p, null, null, -1, o0, o1, oc)); continue; }
                var (v0, c0) = LoftVoid(p.Void0);
                var (v1, c1) = p.Void1 == null ? (v0, c0) : LoftVoid(p.Void1);
                if (v0 == null || v1 == null || v0.Count != v1.Count || c0 != c1)
                { bad.Add($"{at}: void outlines need a top and a bottom point on x = 0 and the same point count at both ends"); continue; }
                pieces.Add(new(p, v0, v1, c0, o0, o1, oc));
            }
            for (int i = 0; i + 1 < pieces.Count; i++)
                if (Math.Abs(pieces[i].P.To - pieces[i + 1].P.From) > 1e-6)
                    bad.Add($"gap / overlap between {pieces[i].P.To / Ft(1):0.###} and {pieces[i + 1].P.From / Ft(1):0.###} ft");
            if (bad.Count > 0 || pieces.Count == 0)
            {
                Warn($"Segmental interior '{def.Name}': not used ({(pieces.Count == 0 ? "no pieces" : string.Join("; ", bad))}) - the standard solid / hollow rule stays.");
                continue;
            }
            var rx = new Regex(def.Segments, RegexOptions.IgnoreCase);
            int n = 0, noFace = 0;
            foreach (var (r, d, _) in _segs.Where(x => x.d.Type == def.SegmentType && rx.IsMatch(x.r.Name)))
            {
                if (SupportFace(r, d) is not { } f) { noFace++; continue; }
                double len = Math.Abs(f.sO - f.sF);
                if (Math.Abs(pieces[0].P.From) > 1e-6 || Math.Abs(pieces[^1].P.To - len) > In(0.01))
                    Warn($"Segmental interior '{def.Name}': {r.Name} is {len / Ft(1):0.###} ft long, the pieces cover {pieces[0].P.From / Ft(1):0.###} - {pieces[^1].P.To / Ft(1):0.###} ft (built as listed, clipped to the segment).");
                _interior[r.Name] = new InteriorUse(def, f.sF, Math.Sign(f.sO - f.sF), pieces);
                n++;
            }
            if (noFace > 0) Warn($"Segmental interior '{def.Name}': {noFace} segments without a support-side face - standard rule kept.");
            Info($"Segmental interior '{def.Name}': {n} {def.SegmentType} segments built as {pieces.Count} pieces ({pieces.Count(p => p.V0 == null)} solid)" + (def.Source.Length > 0 ? $" ({def.Source})." : "."));
        }
    }

    /// <summary>Hollow flag and bottom slab thickness at the CL from the interior piece at a station (null = no interior there).</summary>
    private (bool hollow, double t)? InteriorAt(SegmentResult r, double sta, double h)
    {
        if (_interior.Count == 0 || !_interior.TryGetValue(r.Name, out var use)) return null;
        double dist = (sta - use.SF) * use.Dir;
        foreach (var (p, v0, v1, cl, _, _, _) in use.Pieces)
        {
            if (dist < p.From - 1e-9 || dist > p.To + 1e-9) continue;
            if (v0 == null) return (false, double.NaN);
            double u = Math.Clamp((dist - p.From) / (p.To - p.From), 0, 1);
            double yb = v0[cl].Y + (v1![cl].Y - v0[cl].Y) * u;
            return (true, h - yb);
        }
        return null;
    }

    /// <summary>Builds a segment from its interior pieces; false = no interior for it.</summary>
    private bool BuildInterior(SegmentResult r, SegmentDefinition d)
    {
        if (!_interior.TryGetValue(r.Name, out var use)) return false;
        double lo = Math.Min(r.Station0, r.Station1), hi = Math.Max(r.Station0, r.Station1);
        var standard = RingStations(r, d);
        var outside = new List<string>();
        foreach (var (p, v0, v1, cl, o0, o1, ocl) in use.Pieces)
        {
            double a = Math.Clamp(use.SF + use.Dir * p.From, lo, hi), b = Math.Clamp(use.SF + use.Dir * p.To, lo, hi);
            double s0 = Math.Min(a, b), s1 = Math.Max(a, b);
            if (s1 - s0 < 1e-6) continue;
            var stas = standard.Where(s => s > s0 + 1e-6 && s < s1 - 1e-6).Append(s0).Append(s1).OrderBy(s => s).ToList();
            var rings = new List<(SectionFrame, List<Vec2>, List<Vec2>?)>();
            foreach (double sta in stas)
            {
                var (h, _, _) = SectionBase(d, r, sta);
                var fr = Math.Abs(sta - r.Station0) < 1e-9 ? Frame(r.Station0) : Math.Abs(sta - r.Station1) < 1e-9 ? Frame(r.Station1) : ChordFrameBase(r, sta);
                List<Vec2>? v = null;
                double u = Math.Clamp(((sta - use.SF) * use.Dir - p.From) / (p.To - p.From), 0, 1);
                var outer = o0 != null ? o0.Select((q, i) => q + (o1![i] - q) * u).ToList() : SegmentalSection.Outer(_d.Box, h);
                if (v0 != null)
                {
                    v = v0.Select((q, i) => q + (v1![i] - q) * u).ToList();
                    if (v.Any(q => !InsidePolygon(outer, q))) outside.Add($"{(sta - use.SF) * use.Dir / Ft(1):0.##} ft");
                }
                rings.Add((fr, outer, v));
            }
            var pm = SegmentalLoft.Build(rings, ocl, Math.Max(cl, 0));
            r.Mesh.Append(pm);
            r.Pieces.Add(pm);                                    // one closed solid per piece (IFC items; the merged mesh has shared end faces)
        }
        if (outside.Count > 0)
            Warn($"Segmental interior '{use.Def.Name}': {r.Name} void outline reaches outside the outer outline at {string.Join(", ", outside.Distinct().Take(4))} (built as listed).");
        r.Assumption = use.Def.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(use.Def.Unresolved, $"interior '{use.Def.Name}' as listed") : null;
        return true;
    }

    private SectionFrame ChordFrameBase(SegmentResult r, double sta)
    {
        double u = (sta - r.Station0) / (r.Station1 - r.Station0);
        return Frame(sta) with { Origin = Vec3.Lerp(r.Top0, r.Top1, u) };
    }

    private static bool InsidePolygon(List<Vec2> poly, Vec2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
        if (c) return true;
        // on the boundary counts as inside (void touching the outer face = an opening through it)
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var e = poly[i] - poly[j];
            double t = Math.Clamp(Vec2.Dot(p - poly[j], e) / Math.Max(Vec2.Dot(e, e), 1e-18), 0, 1);
            if ((poly[j] + e * t - p).Length < 1e-6) return true;
        }
        return false;
    }
}
