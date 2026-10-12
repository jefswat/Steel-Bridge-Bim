using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>Deck plan (developed along the CL alignment): deck outline, barrier lines, CL bearing / support lines, beams below (dashed),
/// deck widths at the supports, length along the CL, and the deck data from the model (slab, haunch, volumes, area, reinforcing).</summary>
public static class DeckViews
{
    public static SheetView DeckPlan(ViewContext ctx)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var u = ctx.Units;
        var def = f.Definition;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        Vec2 Dev(Vec2 p) { var so = a.Inverse(p); return new Vec2(so.Station, -so.Offset); }
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        if (f.Deck != null)
        {
            var outline = f.Deck.Outline.Select(p => Dev(p.XY)).ToList();
            d.Poly(SheetLayers.Concrete, outline, true);
            foreach (var bl in f.Deck.BarrierLines) d.Poly(SheetLayers.Barrier, bl.Select(p => Dev(p.XY)));
        }
        foreach (var g in f.Girders) d.Poly(SheetLayers.SteelHidden, g.WebLine.Select(p => Dev(p.XY)));
        var (b, e) = LayoutViews.DeckEndStations(f);
        // CL alignment with station ticks.
        d.Line(SheetLayers.Center, new Vec2(b - d.Paper(0.3), 0), new Vec2(e + d.Paper(0.3), 0));
        double major = 100 * u.LongUnit;
        for (double sta = Math.Ceiling(b / major) * major; sta <= e; sta += major)
        {
            d.Line(SheetLayers.Center, new Vec2(sta, -d.Paper(0.06)), new Vec2(sta, d.Paper(0.06)));
            d.Text(u.FormatStation(sta, 0), new Vec2(sta, d.Paper(0.16)), d.SmallTextH * 0.85, 0, TextAlign.Center);
        }
        d.Text((a.Name.StartsWith("CL", StringComparison.OrdinalIgnoreCase) ? a.Name : "CL " + a.Name).ToUpperInvariant(), new Vec2(b - d.Paper(0.35), 0), d.SmallTextH, 0, TextAlign.Right);
        // Support lines through the bearings, deck width at each support (out to out, normal to the CL).
        foreach (var sl in f.Supports)
        {
            var pts = sl.Bearings.OfType<BearingResult>().Select(x => new Vec2(x.Station, -x.Offset)).OrderBy(p => p.Y).ToList();
            if (pts.Count >= 2)
            {
                var dir = (pts[^1] - pts[0]).Normalized();
                d.Line(SheetLayers.Center, pts[0] - dir * d.Paper(0.5), pts[^1] + dir * d.Paper(0.5));
                d.TextAlong($"CL BRG {sl.Name.ToUpperInvariant()}", pts[^1] + dir * d.Paper(0.55), dir, d.SmallTextH);
            }
            double l = def.Deck.LeftAt(sl.Station), r = def.Deck.RightAt(sl.Station);
            double x = sl.Station + d.Paper(0.2);
            d.Dim(new Vec2(x, -r), new Vec2(x, -l), 0, $"{u.FormatLength(r - l)}");
        }
        var box = dwg.Extents();
        d.Dim(new Vec2(b, box.MaxY + d.Paper(0.2)), new Vec2(e, box.MaxY + d.Paper(0.2)), 0, $"{u.FormatLength(e - b)} DECK (ALONG CL, END TO END)");
        // Deck data from the model.
        double vk = u.IsUS ? 1.307951 : 1; string vn = u.IsUS ? "CY" : "M3";
        var notes = new List<string> { $"SLAB {S(def.Deck.Thickness)} THICK; HAUNCH {S(def.Deck.Haunch)} AT THE SUPPORTS (SEE THE TYPICAL SECTION)." };
        if (f.Deck != null)
        {
            var o = f.Deck.Outline.Select(p => p.XY).ToList();
            double area = Math.Abs(Solids.MeshBuilder.SignedArea(o));
            notes.Add($"DECK AREA (PLAN) {(u.IsUS ? $"{area / 0.09290304:N0} SF" : $"{area:N1} M2")}.");
        }
        if (ctx.Concrete is { } c)
        {
            notes.Add($"CONCRETE: DECK {c.DeckVolume * vk:N1} {vn}, HAUNCHES {c.HaunchVolume * vk:N1} {vn}" + (c.BarrierVolume > 0 ? $", BARRIER {c.BarrierVolume * vk:N1} {vn}" : "") + " (MODEL SOLIDS).");
            notes.Add(c.Marks.Count > 0 ? $"DECK / BARRIER REINFORCING: {c.Marks.Count} MARKS, {c.Marks.Sum(m => m.TotalMass) * (u.IsUS ? 2.20462 : 1):N0} {(u.IsUS ? "LB" : "KG")} (SEE THE BAR LIST)."
                                        : "DECK REINFORCING: NOT IN THE MODEL (NO DECK BARS GENERATED FOR THIS BRIDGE).");
        }
        if (def.Deck.Barrier.Type != Bridge.BarrierType.None)
            notes.Add($"BARRIER: {(def.Deck.Barrier.ProfileName ?? def.Deck.Barrier.Type.ToString()).ToUpperInvariant()}{(def.Deck.Barrier.Left ? " LEFT" : "")}{(def.Deck.Barrier.Right ? " RIGHT" : "")}.");
        box = dwg.Extents();
        double yn = box.MinY - d.Paper(0.25);
        foreach (var n in notes) { d.Text(n, new Vec2(b, yn), d.SmallTextH, 0, TextAlign.Left, SheetLayers.Notes); yn -= d.SmallTextH * 1.6; }
        box = dwg.Extents();
        d.Title("DECK PLAN", $"Developed along the CL alignment; beams dashed; deck widths normal to the CL at the supports · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((b + e) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView("Deck plan", "Deck plan", dwg, d.Scale, "Deck plan with widths, support lines and deck data.");
    }
}
