using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Girder elevation for plate sizes: developed along the web centerline (X = distance from the girder's start end,
/// true to scale), with the depth drawn schematically (not to scale) so plates can be labeled. Shows flange and web
/// plates per segment with lengths, field sections with weights, bearings, stiffeners and cross-frame locations.
/// </summary>
public static class GirderViews
{
    public static SheetView Elevation(ViewContext ctx, int gi)
    {
        var f = ctx.Framing;
        var def = f.Definition;
        var u = ctx.Units;
        var g = f.Girders[gi];
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        var sec = def.Girders.Section;
        double L = g.Path.Length;
        double H = d.Paper(0.6);                 // schematic depth on paper
        double tMax = Math.Max(sec.Segments(GirderPlate.TopFlange).Max(s => s.Thickness), sec.Segments(GirderPlate.BottomFlange).Max(s => s.Thickness));
        double Band(double t) => d.Paper(0.035 + 0.05 * t / tMax);

        // ---- plate segments → girder distances
        List<(double s0, double s1, PlateSegment seg)> Runs(GirderPlate plate)
        {
            var segs = sec.Segments(plate);
            var list = new List<(double, double, PlateSegment)>();
            double s0 = 0;
            for (int i = 0; i < segs.Count; i++)
            {
                double s1 = i == segs.Count - 1 || double.IsNaN(segs[i].EndStation) ? L : DistanceAtStation(g, segs[i].EndStation) ?? L;
                s1 = Math.Clamp(s1, s0, L);
                if (s1 > s0 + 1e-6) list.Add((s0, s1, segs[i]));
                s0 = s1;
            }
            return list;
        }
        string Pl(PlateSegment p) => $"PL {d.Short(p.Width)} x {d.Short(p.Thickness)}";

        var top = Runs(GirderPlate.TopFlange);
        var bot = Runs(GirderPlate.BottomFlange);
        var web = Runs(GirderPlate.Web);
        // Plate labels in rows: a label that does not fit beside its neighbours goes to the next row with a leader (pier zones have
        // short plates at 1" = 100'); everything above / below the plate labels moves out by the rows used.
        const double RowIn = 0.15;
        int[] Rows(List<(double c, string text)> labels)
        {
            var rows = new int[labels.Count];
            var ends = new List<double>();
            double gap = d.Paper(0.06);
            foreach (int i in Enumerable.Range(0, labels.Count).OrderBy(i => labels[i].c))
            {
                double hw = LabelPlacer.WidthInHeights(labels[i].text) * d.SmallTextH / 2;
                int r = 0;
                while (r < ends.Count && ends[r] + gap > labels[i].c - hw) r++;
                if (r == ends.Count) ends.Add(double.NegativeInfinity);
                ends[r] = labels[i].c + hw;
                rows[i] = r;
            }
            return rows;
        }
        var topLabels = top.Select(x => ((x.s0 + x.s1) / 2, $"TOP FL {Pl(x.seg)}")).ToList();
        var botLabels = bot.Select(x => ((x.s0 + x.s1) / 2, $"BOT FL {Pl(x.seg)}")).ToList();
        var topRows = Rows(topLabels);
        var botRows = Rows(botLabels);
        double topShift = RowIn * (topRows.DefaultIfEmpty(0).Max()), botShift = RowIn * (botRows.DefaultIfEmpty(0).Max());
        for (int i = 0; i < top.Count; i++)
        {
            var (s0, s1, p) = top[i];
            double t = Band(p.Thickness);
            d.Poly(SheetLayers.Steel, new[] { new Vec2(s0, H), new Vec2(s1, H), new Vec2(s1, H - t), new Vec2(s0, H - t) }, true);
            double y = H + d.Paper(0.12 + RowIn * topRows[i]);
            d.Text(topLabels[i].Item2, new Vec2(topLabels[i].Item1, y), d.SmallTextH, 0, TextAlign.Center);
            if (topRows[i] > 0) d.Line(SheetLayers.Text, new Vec2(topLabels[i].Item1, H), new Vec2(topLabels[i].Item1, y - d.SmallTextH * 0.6));
            d.Dim(new Vec2(s0, H), new Vec2(s1, H), 0.32 + topShift, d.Len(s1 - s0));
        }
        for (int i = 0; i < bot.Count; i++)
        {
            var (s0, s1, p) = bot[i];
            double t = Band(p.Thickness);
            d.Poly(SheetLayers.Steel, new[] { new Vec2(s0, 0), new Vec2(s1, 0), new Vec2(s1, t), new Vec2(s0, t) }, true);
            double y = -d.Paper(0.12 + RowIn * botRows[i]);
            d.Text(botLabels[i].Item2, new Vec2(botLabels[i].Item1, y), d.SmallTextH, 0, TextAlign.Center);
            if (botRows[i] > 0) d.Line(SheetLayers.Text, new Vec2(botLabels[i].Item1, 0), new Vec2(botLabels[i].Item1, y + d.SmallTextH * 0.6));
            d.Dim(new Vec2(s0, 0), new Vec2(s1, 0), -0.35 - botShift, d.Len(s1 - s0));
        }
        foreach (var (s0, s1, p) in web)
        {
            d.Line(SheetLayers.Steel, new Vec2(s0, H), new Vec2(s0, 0));
            d.Text($"WEB {d.Short(sec.WebDepth)} x {d.Short(p.Thickness)}", new Vec2((s0 + s1) / 2, H * 0.5), d.SmallTextH, 0, TextAlign.Center);
        }
        d.Line(SheetLayers.Steel, new Vec2(L, H), new Vec2(L, 0));

        // ---- stiffeners and connection plates (ticks), cross-frame marks
        foreach (var st in f.Stiffeners.Where(x => x.Girder == gi))
        {
            double s = st.GirderDistance;
            double z0 = st.Kind == StiffenerKind.Intermediate ? H * 0.2 : Band(tMax);
            d.Line(st.Kind == StiffenerKind.Bearing ? SheetLayers.Steel : SheetLayers.SteelHidden, new Vec2(s, z0), new Vec2(s, H - Band(tMax)));
        }
        foreach (var cf in f.CrossFrames.Where(c => c.Bay == gi || c.RightGirder == gi))
        {
            double s = cf.Bay == gi ? cf.LeftGlobalDistance : cf.RightGlobalDistance;
            if (cf.Bay == gi) d.Text(cf.Mark, new Vec2(s, H + d.Paper(0.55 + topShift)), d.SmallTextH * 0.85, Math.PI / 2, TextAlign.Left);
        }

        // ---- bearings, spans
        var brgS = new List<(double s, string name)>();
        int nSpan = g.Spans.Count;
        for (int j = 0; j < f.Supports.Count; j++)
        {
            if (f.Supports[j].Bearings.ElementAtOrDefault(gi) == null) continue;   // auxiliary supports are added below
            double s = g.DistanceAt(Math.Min(j, nSpan - 1), j == nSpan ? 1 : 0);
            brgS.Add((s, f.Supports[j].Name));
            double h = d.Paper(0.1);
            d.Poly(SheetLayers.Bearing, new[] { new Vec2(s, 0), new Vec2(s - h * 0.7, -h), new Vec2(s + h * 0.7, -h) }, true);
            d.Line(SheetLayers.Center, new Vec2(s, -d.Paper(1.3 + botShift)), new Vec2(s, H + d.Paper(0.3)));
            d.Text($"CL BRG {f.Supports[j].Name.ToUpperInvariant()}", new Vec2(s, -d.Paper(1.4 + botShift)), d.SmallTextH, 0, TextAlign.Center);
        }
        foreach (var al in f.AuxSupports)
        {
            if (al.Bearings.ElementAtOrDefault(gi) is not { } ab) continue;
            double s = ab.PathDistance;
            brgS.Add((s, al.Name));
            double h = d.Paper(0.1);
            d.Poly(SheetLayers.Bearing, new[] { new Vec2(s, 0), new Vec2(s - h * 0.7, -h), new Vec2(s + h * 0.7, -h) }, true);
            d.Line(SheetLayers.Center, new Vec2(s, -d.Paper(1.3 + botShift)), new Vec2(s, H + d.Paper(0.3)));
            d.Text($"CL BRG {al.Name.ToUpperInvariant()}", new Vec2(s, -d.Paper(1.4 + botShift)), d.SmallTextH, 0, TextAlign.Center);
        }
        brgS.Sort((x, y) => x.s.CompareTo(y.s));
        for (int i = 1; i < brgS.Count; i++)
            d.Dim(new Vec2(brgS[i - 1].s, -d.Paper(0.75 + botShift)), new Vec2(brgS[i].s, -d.Paper(0.75 + botShift)), 0, $"{d.Len(brgS[i].s - brgS[i - 1].s)} ALONG {g.Name}");

        // ---- field splices and field sections (with weights from the 3D model)
        var splices = f.Splices.Where(x => x.Girder == gi).Select(x => x.GirderDistance).OrderBy(x => x).ToList();
        foreach (double s in splices)
        {
            d.Line(SheetLayers.Steel, new Vec2(s, -d.Paper(0.05)), new Vec2(s, H + d.Paper(0.05)));
            d.Line(SheetLayers.Steel, new Vec2(s + d.Paper(0.02), -d.Paper(0.05)), new Vec2(s + d.Paper(0.02), H + d.Paper(0.05)));
            d.Text("FIELD SPLICE", new Vec2(s, H + d.Paper(1.0 + topShift)), d.SmallTextH, 0, TextAlign.Center);
        }
        var bounds = new List<double> { 0 };
        bounds.AddRange(splices);
        bounds.Add(L);
        var asms = ctx.Steel?.Assemblies.Where(a => a.Kind == AssemblyKind.GirderFieldSection && a.Girder == gi).Select(a => a.Mark)
            .OrderBy(m => m, SteelModelGenerator.MarkComparer.Instance).ToList() ?? new();
        double wUnit = u.IsUS ? 2.20462 : 1;
        for (int i = 1; i < bounds.Count; i++)
        {
            string label = d.Len(bounds[i] - bounds[i - 1]);
            if (i - 1 < asms.Count && ctx.Steel != null)
            {
                double mass = ctx.Steel.Parts.Where(p => p.Assembly == asms[i - 1] && p.Category != PartCategory.Weld).Sum(p => p.Mass) * wUnit;
                label = $"{asms[i - 1]}: {label}, {mass:N0} {(u.IsUS ? "LB" : "KG")}";
            }
            d.Dim(new Vec2(bounds[i - 1], -d.Paper(1.05 + botShift)), new Vec2(bounds[i], -d.Paper(1.05 + botShift)), 0, label);
        }
        d.Dim(new Vec2(0, H + d.Paper(1.25 + topShift)), new Vec2(L, H + d.Paper(1.25 + topShift)), 0, $"{d.Len(L)} {g.Name} (END TO END, ALONG WEB CL)");

        var box = dwg.Extents();
        d.Title($"{g.Name} ELEVATION", "Developed along the web CL; depth not to scale. Plate changes at radial lines; stiffeners: solid = bearing, dashed = connection / intermediate.",
            new Vec2(L / 2, box.MinY - d.Paper(0.3)));
        return new SheetView($"{g.Name} elevation", $"Girder {g.Name}", dwg, d.Scale, $"{g.Name}: plate sizes and lengths, field sections, bearings, stiffeners, cross frames.");
    }

    /// <summary>Distance along the girder where it crosses a station (radial line), or null outside the girder.</summary>
    public static double? DistanceAtStation(GirderResult g, double station)
    {
        var path = g.Path;
        if (path.Count < 2) return null;
        double lo = Math.Min(path.Station[0], path.Station[^1]), hi = Math.Max(path.Station[0], path.Station[^1]);
        if (station < lo || station > hi) return null;
        double s0 = 0, s1 = path.Length;
        bool inc = path.Station[^1] >= path.Station[0];
        for (int it = 0; it < 60; it++)
        {
            double sm = (s0 + s1) / 2;
            if ((path.At(sm).station < station) == inc) s0 = sm; else s1 = sm;
        }
        return (s0 + s1) / 2;
    }
}
