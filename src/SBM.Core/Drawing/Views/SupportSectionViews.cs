using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// True sections through a support from the generated solids (<see cref="MeshSection"/>): along the support line (cap / pile cap with its
/// steps, piles, diaphragm with the beams cut across, deck, wings) for integral and pile-supported abutments, annotated with the step tops,
/// cap bottom and pile data from the model.
/// </summary>
public static class SupportSectionViews
{
    /// <summary>Section along the support line at <paramref name="n"/> from the CL bearing (across the line), looking from the span
    /// for the first support and from the rear otherwise is avoided: x always runs like the abutment rear elevation (S mirrored at the end support).</summary>
    public static SheetView AlongSupport(ViewContext ctx, SubstructureUnit unit, double n, string label)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.BentScale);
        var f = ctx.Framing;
        var ps = ctx.Prestressed;
        int si = unit.SupportIndex;
        var sl = f.Supports[si];
        var o2 = unit.Plan(0, n);
        var origin = new Vec3(o2.X, o2.Y, 0);
        var nrm = new Vec3(unit.N.X, unit.N.Y, 0);
        double mir = unit.Abutment is { Sigma: < 0 } ? -1 : 1;
        var sec = new MeshSection(origin, nrm, new Vec3(0, 0, 1));
        var dir = new Vec3(unit.D.X, unit.D.Y, 0) * mir;
        if (Vec3.Dot(sec.XAxis, dir) < 0) sec = new MeshSection(origin, nrm * -1, new Vec3(0, 0, 1));
        double X(double s) => mir * s;          // x of a station along the line (s from the unit origin)
        double s0 = unit.CapS0, s1 = unit.CapS1;
        double xa = Math.Min(X(s0), X(s1)) - Ft(4), xb = Math.Max(X(s0), X(s1)) + Ft(4);

        // Concrete of the unit (cap / pile cap, walls, wings, pedestals), diaphragm, beams, deck.
        foreach (var s in unit.Solids.Where(s => s.Kind != SubElementKind.Pile && s.Kind != SubElementKind.PileCasing))
            sec.Draw(d, s.Mesh, s.IsConcrete ? SheetLayers.ConcreteCut : SheetLayers.Steel, xa, xb);
        if (ps?.ConcreteDiaphragms is { } cdm)
        {
            foreach (var cd in cdm.Diaphragms.Where(x => x.SupportIndex == si)) sec.Draw(d, cd.Mesh, SheetLayers.ConcreteCut, xa, xb);
            foreach (var ac in cdm.Accessories.Where(x => x.Support == sl.Name)) sec.Draw(d, ac.Mesh, SheetLayers.Hatch, xa, xb, hatch: false);
        }
        if (ps != null)
        {
            foreach (var b in ps.Beams.Where(b => b.Span == si - 1 || b.Span == si)) sec.Draw(d, b.Mesh, SheetLayers.ConcreteCut, xa, xb);
            foreach (var p in ps.Pads.Where(p => p.Support == sl.Name)) sec.Draw(d, p.Mesh, SheetLayers.Bearing, xa, xb, hatch: false);
        }
        if (ctx.Concrete is { } c)
        {
            if (c.Deck != null) sec.Draw(d, c.Deck, SheetLayers.ConcreteCut, xa, xb);
            if (c.Haunches != null) sec.Draw(d, c.Haunches, SheetLayers.ConcreteCut, xa, xb);
            foreach (var (_, m) in c.Barriers) sec.Draw(d, m, SheetLayers.ConcreteCut, xa, xb);
        }
        var box = dwg.Extents();
        bool any = !box.IsEmpty;
        double zCapBot = unit.CapBottom.Count > 0 ? unit.CapBottom.Min(p => p.Y) : box.MinY;

        // Piles: drawn down to 4 ft below the cap bottom (cut or seen), with the pile note.
        double yCut = zCapBot - Ft(4);
        var pileX = new List<double>();
        foreach (var fo in unit.Foundations)
            foreach (var p in fo.Piles)
            {
                double x = X(p.S);
                double w = In(6);
                double top = p.TopZ, bot = Math.Max(yCut, p.TipZ);
                d.Line(SheetLayers.SteelHidden, new Vec2(x - w, top), new Vec2(x - w, bot));
                d.Line(SheetLayers.SteelHidden, new Vec2(x + w, top), new Vec2(x + w, bot));
                d.Line(SheetLayers.SteelHidden, new Vec2(x - w * 1.6, bot - d.Paper(0.04)), new Vec2(x + w * 1.6, bot + d.Paper(0.04)));
                if (Math.Abs(p.N - n) < Ft(3)) pileX.Add(x);
            }
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        if (pileX.Count > 1)
            d.DimChain(pileX.Distinct().OrderBy(x => x).Select(x => new Vec2(x, yCut - d.Paper(0.25))).ToList(), 0);
        var piles = unit.Foundations.SelectMany(fo => fo.Piles).ToList();
        if (piles.Count > 0)
        {
            string tip = PierViews.TipText(piles, d);
            var pl = piles.Select(p => p.Length).ToList();
            string len = Math.Abs(pl.Max() - pl.Min()) < In(1) ? u.FormatLength(pl[0]) : $"{u.FormatLength(pl.Min())} TO {u.FormatLength(pl.Max())}";
            d.Text($"{piles.Count} PILES {unit.Definition.PileLabel(S)}, LENGTH {len} (MODEL), {tip}", new Vec2((X(s0) + X(s1)) / 2, yCut - d.Paper(0.7)), d.SmallTextH, 0, TextAlign.Center);
        }

        // Step tops / seats at the beam lines and the cap bottom at the ends.
        var seats = (ps?.BearingSeats ?? ctx.Steel?.BearingSeats ?? new List<Steel.BearingSeat>()).Where(x => x.Support == sl.Name).ToList();
        foreach (var gi in Enumerable.Range(0, sl.Bearings.Length))
        {
            if (sl.Bearings[gi] is not { } br) continue;
            var w = f.Alignment.PointAt(br.Station, br.Offset);
            double sb = Vec2.Dot(w - unit.Origin, unit.D);
            var seat = seats.Where(x => x.Girder == gi).OrderBy(x => x.Location is { } loc ? Vec2.Distance(loc, w) : 0).FirstOrDefault();
            double z = unit.Abutment?.StepTop?.Invoke(sb) ?? seat?.SeatElevation ?? double.NaN;
            if (double.IsNaN(z)) continue;
            d.Text($"G{gi + 1}\n{(unit.Abutment?.StepTop != null ? "STEP" : "SEAT")} {d.Elev(z)}", new Vec2(X(sb), zCapBot - d.Paper(0.12)), d.SmallTextH * 0.85, 0, TextAlign.Center);
        }
        if (unit.CapBottom.Count > 0)
        {
            var cb0 = unit.CapBottom[0]; var cb1 = unit.CapBottom[^1];
            d.ElevationTag(new Vec2(X(cb0.X), cb0.Y), $"BOTTOM EL {d.Elev(cb0.Y)}", right: X(cb0.X) > X(cb1.X));
            d.ElevationTag(new Vec2(X(cb1.X), cb1.Y), $"BOTTOM EL {d.Elev(cb1.Y)}", right: X(cb1.X) > X(cb0.X));
        }
        d.Dim(new Vec2(X(s0), yCut - d.Paper(1.05)), new Vec2(X(s1), yCut - d.Paper(1.05)), 0, $"{u.FormatLength(Math.Abs(s1 - s0))} {(unit.Type == SupportType.Abutment ? "ABUTMENT" : "CAP")} (ALONG THE SUPPORT LINE)");
        if (!any) d.Text("NOTHING MODELED AT THIS SECTION", new Vec2(0, 0), d.TextH, 0, TextAlign.Center);
        box = dwg.Extents();
        string where = Math.Abs(n) < 1e-6 ? "along the CL bearing" : $"{S(Math.Abs(n))} {(n > 0 ? "ahead of" : "behind")} the CL bearing";
        d.Title($"SECTION {label}", $"{unit.Name}, {where} (true section of the model, piles shown to 4 ft below the cap) · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((box.MinX + box.MaxX) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView($"Section {label}", $"{unit.Name.Replace(" ", "")}-along-{label}", dwg, d.Scale, "Section along the support line from the model solids.");
    }
}
