using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Units;

namespace SBM.Core.Checks;

/// <summary>
/// Elevation chains for prestressed girders: profile grade → cross slope → top of deck → slab → haunch (to the top of the
/// straight beam) → beam depth → bearing pad → bearing seat → pedestal → cap. The beam is straight between its
/// support-line points, where top of beam = finished grade − slab − haunch; the haunch elsewhere is what remains.
/// Every value is recomputed from inputs and compared with the generated beams, pads, seats and substructure.
/// </summary>
public static partial class ElevationCheck
{
    /// <summary>Top of a prestressed beam at a plan point (girder CL), with the calculation text.</summary>
    private static (double z, string basis, int span) PsTopOfBeam(BridgeFraming f, int girder, Vec2 p, UnitSystem u)
    {
        var def = f.Definition;
        var a = f.Alignment;
        string E(double z) => u.FormatLength(z, LengthKind.Coordinate);
        string L(double m) => u.FormatLength(m);
        string Sh(double m) => u.FormatLength(m, LengthKind.Short);
        var g = f.Girders[girder];
        // Span whose chord contains the point (closest along-chord parameter in [0, 1]).
        int span = 0;
        double best = double.MaxValue;
        for (int j = 0; j < g.Spans.Count; j++)
        {
            var c = g.Spans[j];
            var t0 = (c.End - c.Start).Normalized();
            double s = Vec2.Dot(p - c.Start, t0) / (c.End - c.Start).Length;
            double outside = s < 0 ? -s : s > 1 ? s - 1 : 0;
            if (outside < best) { best = outside; span = j; }
        }
        var cs = g.Spans[span];
        double TopAtSupport(Vec2 q)
        {
            var so = a.Inverse(q);
            return def.Deck.FinishedElevation(a, so.Station, so.Offset) - def.Deck.Thickness - def.Deck.Haunch;
        }
        if (cs.LayoutBegin is { } lb && cs.LayoutEnd is { } le && def.Girders.SpanLayoutOf(span) is { } lay)
        {
            // Per-span girder layout: the beam is straight through its points on the layout's begin / end lines, where top of
            // beam = finished grade − slab − haunch (the layout's haunch at that end, else the deck haunch).
            double hb = lay.Begin.Haunches?[girder] ?? def.Deck.Haunch, he = lay.End.Haunches?[girder] ?? def.Deck.Haunch;
            double zb = TopAtPoint(lb.XY, hb), ze = TopAtPoint(le.XY, he);
            double ll = (le.XY - lb.XY).Length;
            var tl = (le.XY - lb.XY) / ll;
            double dl = Vec2.Dot(p - lb.XY, tl);
            double gl = (ze - zb) / ll;
            string Where(GirderSpanEnd e, int sup) => Math.Abs(e.LineShift) > 1e-9
                ? $"{f.Supports[sup].Name} line moved {L(e.LineShift)} along the reference offset {L(e.ReferenceOffset)}" : $"{f.Supports[sup].Name} line";
            string bl = $"span {span + 1} layout: top of beam on the {Where(lay.Begin, span)} {E(zb)} (finished grade − slab {Sh(def.Deck.Thickness)} − haunch {Sh(hb)}), " +
                        $"beam straight to the {Where(lay.End, span + 1)} {E(ze)} (haunch {Sh(he)}): grade {Pct(gl)} × {L(dl)} along the girder";
            return (zb + gl * dl, bl, span);
        }
        double zA = TopAtSupport(cs.Start), zB = TopAtSupport(cs.End);
        double lc = (cs.End - cs.Start).Length;
        var t = (cs.End - cs.Start) / lc;
        double dist = Vec2.Dot(p - cs.Start, t);
        double grade = (zB - zA) / lc;
        double z = zA + grade * dist;
        string basis = $"top of beam on the {f.Supports[span].Name} line {E(zA)} (finished grade − slab {Sh(def.Deck.Thickness)} − haunch {Sh(def.Deck.Haunch)}), " +
                       $"beam straight to the {f.Supports[span + 1].Name} line {E(zB)}: grade {Pct(grade)} × {L(dist)} along the girder";
        return (z, basis, span);

        double TopAtPoint(Vec2 q, double haunch)
        {
            var so = a.Inverse(q);
            return def.Deck.FinishedElevation(a, so.Station, so.Offset) - def.Deck.Thickness - haunch;
        }
    }

    /// <summary>Chain at a prestressed girder CL at any point: finished grade down to the bottom of the beam.</summary>
    private static ElevationChain PsGirderAt(BridgeFraming f, int girder, Vec2 p, string title, UnitSystem u, out double topOfBeam)
    {
        var def = f.Definition;
        var shape = TxGirderCatalog.Get(def.Girders.Prestressed);
        string Sh(double m) => u.FormatLength(m, LengthKind.Short);
        var so = f.Alignment.Inverse(p);
        var ch = new ElevationChain { Title = title, Station = so.Station, Offset = so.Offset };
        double z = AddGrade(ch, f.Alignment, so.Station, so.Offset, u, def.Deck);
        z -= def.Deck.Thickness;
        ch.Steps.Add(new CalcStep("− Deck slab", $"slab thickness {Sh(def.Deck.Thickness)}", -def.Deck.Thickness, z, "Bottom of deck"));
        var (zt, basis, _) = PsTopOfBeam(f, girder, p, u);
        ch.Steps.Add(new CalcStep("− Haunch (here)", $"bottom of deck − top of beam; {basis}", zt - z, zt, "Top of beam"));
        topOfBeam = zt;
        z = zt - shape.Depth;
        ch.Steps.Add(new CalcStep("− Beam depth", $"{shape.Name} D = {Sh(shape.Depth)}", -shape.Depth, z, "Bottom of beam"));
        return ch;
    }

    /// <summary>Chains at every prestressed beam end (pad center): two per girder at interior bents.</summary>
    public static List<ElevationChain> PrestressedBearings(BridgeFraming f, PrestressedModel ps, UnitSystem u, Substructure.SubstructureModel? sub = null,
        IReadOnlyList<Substructure.SubstructureModel>? others = null)
    {
        var list = new List<ElevationChain>();
        string E(double z) => u.FormatLength(z, LengthKind.Coordinate);
        string Sh(double m) => u.FormatLength(m, LengthKind.Short);
        foreach (var pad in ps.Pads.OrderBy(p => p.Support == f.Supports[0].Name ? 0 : 1).ThenBy(p => f.Supports.ToList().FindIndex(s => s.Name == p.Support)).ThenBy(p => p.Girder).ThenBy(p => p.Span))
        {
            int sup = f.Supports.ToList().FindIndex(s => s.Name == pad.Support);
            bool back = pad.Span < sup; // beam of the span behind the support
            string side = f.Supports[sup].Type == Bridge.SupportType.Pier ? (back ? " back" : " ahead") : "";
            var ch = PsGirderAt(f, pad.Girder, pad.Center.XY, $"{pad.Support} — G{pad.Girder + 1}{side} bearing (pad {pad.Mark}, span {pad.Span + 1})", u, out double top);
            var beam = ps.Beams.First(b => b.Girder == pad.Girder && b.Span == pad.Span);
            // Compare the top of beam with the generated beam (its top line at the bearing).
            double model = beam.TopStart.Z + (beam.TopEnd.Z - beam.TopStart.Z) * Vec2.Dot(pad.Center.XY - beam.TopStart.XY, (beam.TopEnd - beam.TopStart).XY.Normalized()) /
                           (beam.TopEnd - beam.TopStart).XY.Length;
            if (Math.Abs(model - top) > 0.0005) { ch.Mismatch = true; ch.Checks.Add($"3D beam top {E(model)} differs by {Sh(model - top)}"); }
            else ch.Checks.Add($"Top of beam matches the 3D beam ({E(model)}).");
            double z = ch.Final - pad.Thickness;
            ch.Steps.Add(new CalcStep("− Elastomeric pad", $"{pad.Description} (pad top level at the beam bottom)", -pad.Thickness, z, "Bottom of bearing = bearing seat"));
            if (pad.OffNominal > 1e-4)
                ch.Checks.Add($"Pad moved {Sh(pad.OffNominal)} off the nominal CL bearing to stay on the square girder end.");
            if (ps.BearingSeats.FirstOrDefault(s => s.Location is { } l && (l - pad.Center.XY).Length < 1e-4) is { } seat)
            {
                double diff = seat.SeatElevation - z;
                if (Math.Abs(diff) > 0.0005) { ch.Mismatch = true; ch.Checks.Add($"3D bearing seat {E(seat.SeatElevation)} differs by {Sh(diff)}"); }
                else ch.Checks.Add($"Bearing seat matches the 3D model ({E(seat.SeatElevation)}).");
            }
            ch.Checks.Add($"Total depth top of deck to bearing seat: {u.FormatLength(ch.Steps[1].Elevation - z)}.");
            if (FindPedestal(sub, others, sup, pad.Girder, pad.Center.XY) is { } hit)
                AddSubstructure(ch, hit.unit, hit.ped, pad.Center.XY, u);
            list.Add(ch);
        }
        return list;
    }
}
