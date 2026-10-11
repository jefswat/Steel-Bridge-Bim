using System.Collections.Concurrent;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>
/// End dam diaphragm bars (<see cref="DeckEndDamBar"/>): bend-diagram shapes in the plane normal to the deck-end line, placed by their outside outline in the
/// plan's end dam section and repeated along the joint at the plan's dimensioned positions from the girder lines. Nothing is adjusted: a bay whose width does
/// not close the printed spacing is reported.
/// </summary>
public sealed partial class ConcreteGenerator
{
    /// <summary>Plan point where a girder web line crosses a deck-end line (begin / end; kinked lines too), the web line extended beyond its ends.</summary>
    private Vec2? GirderAtEnd(int g, bool begin)
    {
        var web = _f.Girders[g].WebLine;
        if (web.Count < 2) return null;
        Func<Vec2, double> dist = begin ? DR.FromBegin : DR.FromEnd;
        var pts = web.Select(p => p.XY).ToList();
        // extend 10 m beyond both ends
        pts.Insert(0, pts[0] + (pts[0] - pts[1]).Normalized() * 10);
        pts.Add(pts[^1] + (pts[^1] - pts[^2]).Normalized() * 10);
        var range = begin ? Enumerable.Range(0, pts.Count - 1) : Enumerable.Range(0, pts.Count - 1).Reverse();
        foreach (int i in range)
        {
            double d0 = dist(pts[i]), d1 = dist(pts[i + 1]);
            if ((d0 >= 0) == (d1 >= 0)) continue;
            return Vec2.Lerp(pts[i], pts[i + 1], d0 / (d0 - d1));
        }
        return null;
    }

    private void BuildEndDamBars(DeckPlanReinforcement plan, ConcurrentBag<PlanBar> bag)
    {
        foreach (var grp in plan.EndDam)
        {
            string where = $"Deck end dam bars {grp.Mark}";
            if (grp.Unresolved.Length > 0) Warn($"UNRESOLVED (deck end dam {grp.Mark}): {grp.Unresolved}");
            var size = RebarCatalog.Size(grp.Size);
            double listed = grp.ListedLength;
            // shape in section coordinates (X into the deck, Y up = -drop)
            BarShape shape;
            Vec2[] center;
            if (grp.HoopWidth is double w)
            {
                var (sh, _) = SubstructureGenerator.PlanHoop(size, w, grp.HoopHeight, grp.HoopLapSide, grp.HoopLapTop, grp.Pin, listed);
                shape = sh;
                // local u across (tails at u = w), v up -> X = corner.X + (w - u), Y = -(corner.Drop + h - v)
                double x0 = grp.HoopCorner.X, d0 = grp.HoopCorner.Drop, h = grp.HoopHeight;
                center = sh.Centerline.Select(q => new Vec2(x0 + (w - q.X), -(d0 + h - q.Y))).ToArray();
            }
            else if (grp.Outside is { Count: >= 2 } outl)
            {
                var o = outl.Select(q => new Vec2(q.X, -q.Drop)).ToList();
                var dims = grp.Dims.Count > 0 ? grp.Dims.ToArray() : Enumerable.Range(0, o.Count - 1).Select(i => Vec2.Distance(o[i], o[i + 1])).ToArray();
                var (sh, _) = SubstructureGenerator.PlanBent(size, o, dims, grp.BodySide, "END DAM", $"End dam bar {string.Join(" + ", dims.Select(d => _u.FormatLength(d, LengthKind.Short)))} out to out", grp.Pin, listed);
                shape = sh;
                center = sh.Centerline;
            }
            else { Warn($"{where}: no outline / hoop given - not placed."); continue; }
            double sum = shape.Dimensions.Sum(d => d.Value) + (grp.HoopWidth != null ? grp.HoopWidth.Value + grp.HoopHeight : 0);
            if (listed > 0 && Math.Abs(sum - listed) > In(0.5))
                Warn($"{where}: the bend diagram dimensions add up to {_u.FormatLength(sum, LengthKind.Short)}, the bar list gives {_u.FormatLength(listed, LengthKind.Short)} (not adjusted).");

            foreach (bool begin in new[] { true, false })
            {
                if (grp.Ends == DeckEndSide.Begin && !begin || grp.Ends == DeckEndSide.End && begin) continue;
                var poly = begin ? DR.BeginPolyline : DR.EndPolyline;
                var t = poly?.Direction ?? (begin ? DR.BeginLineDirection : DR.EndLineDirection);
                var n = poly?.OverallNormal ?? (begin ? DR.BeginLineDirection.Left : -DR.EndLineDirection.Left);
                string endName = _def.Supports[begin ? 0 : _def.Supports.Count - 1].Name;
                var gpts = new List<Vec2>();
                for (int g = 0; g < _f.Girders.Count; g++)
                    if (GirderAtEnd(g, begin) is Vec2 p) gpts.Add(p);
                if (gpts.Count < 2) { Warn($"{where} at {endName}: fewer than two girder lines reach the deck end - not placed."); continue; }
                gpts = gpts.OrderBy(p => Vec2.Dot(p, t)).ToList();
                var bases = new List<Vec2>();
                var badBays = new List<string>();
                for (int i = 0; i + 1 < gpts.Count; i++)
                {
                    double bay = Vec2.Dot(gpts[i + 1] - gpts[i], t);
                    for (int k = 0; k < grp.PerBay; k++) bases.Add(gpts[i] + t * (grp.FromGirder + k * grp.Spacing));
                    double last = bay - grp.FromGirder - (grp.PerBay - 1) * grp.Spacing;
                    if (grp.PerBay > 0 && Math.Abs(last - grp.FromGirder) > In(1.0 / 16))
                        badBays.Add($"bay {i + 1} {_u.FormatLength(bay, LengthKind.Short)}: last bar {_u.FormatLength(last, LengthKind.Short)} from the next girder");
                }
                foreach (double d in grp.Overhang) { bases.Add(gpts[0] - t * d); bases.Add(gpts[^1] + t * d); }
                if (badBays.Count > 0)
                    Warn($"{where} at {endName}: the printed layout ({_u.FormatLength(grp.FromGirder, LengthKind.Short)} + {grp.PerBay - 1} @ {_u.FormatLength(grp.Spacing, LengthKind.Short)}) does not close in {string.Join("; ", badBays)} - not adjusted.");
                foreach (var b0 in bases)
                {
                    var b = b0 + t * grp.SideOffset;
                    var pts3 = center.Select(q =>
                    {
                        var p = b + n * q.X;
                        var so = _a.Inverse(p);
                        return p.WithZ(Deck.FinishedElevation(_a, so.Station, so.Offset) + q.Y);
                    }).ToArray();
                    bag.Add(new PlanBar(grp.Mark, size, "END DAM DIAPH.", listed, listed, pts3, shape));
                }
                lock (_m.Notes) _m.Notes.Add($"  end dam {grp.Mark} at {endName}: {bases.Count} bars ({gpts.Count - 1} bays x {grp.PerBay} + {2 * grp.Overhang.Count} outside the exterior girders).");
            }
        }
        foreach (var s in plan.Unresolved) Warn($"UNRESOLVED (deck reinforcing): {s}");
    }
}
