using SBM.Core.Bridge;
using SBM.Core.Drawing.Views;
using SBM.Core.Framing;

namespace SBM.Core.Drawing.Sheets;

/// <summary>The plan set for one bridge on 11x17 (or another format): every sheet composed with fitted scales.</summary>
public static class SheetSet
{
    public static ViewContext WithScale(ViewContext ctx, double s) => ctx with
    {
        Options = new DrawingOptions
        {
            LayoutScale = s, SectionScale = s, BentScale = s,
            TypicalSectionStation = ctx.Options.TypicalSectionStation, IncludeRebar = ctx.Options.IncludeRebar,
        },
    };

    /// <summary>Sheet specifications (cheap); each is composed on demand with <see cref="SheetComposer.Compose"/>.</summary>
    public static List<SheetSpec> Specs(ViewContext ctx, SheetFormat fmt)
    {
        var (ax0, ay0, ax1, ay1) = fmt.Area;
        double W = ax1 - ax0, H = ay1 - ay0;
        Panel Full(ViewSpec v) => new(v, 0, 0, W, H);
        var specs = new List<SheetSpec>();
        var f = ctx.Framing;
        string bridge = f.Definition.Name;

        specs.Add(new SheetSpec("Bridge layout", "Plan and elevation along the CL alignment",
            new[] { Full(new ViewSpec("Plan and elevation", s => LayoutViews.Layout(WithScale(ctx, s)), ScaleFamily.Engineering)) }));
        specs.Add(new SheetSpec("Typical section", $"Superstructure",
            new[] { Full(new ViewSpec("Typical section", s => SectionViews.TypicalSection(WithScale(ctx, s)), ScaleFamily.Architectural)) }));
        // A second typical section where the girder count differs: the span with the fewest girders (the first typical section is span 1).
        if (double.IsNaN(ctx.Options.TypicalSectionStation))
            foreach (var (sta, count) in TypicalStations(f).Skip(1).OrderBy(x => x.count).Take(1))
            {
                var ctx2 = ctx with { Options = new DrawingOptions { LayoutScale = ctx.Options.LayoutScale, SectionScale = ctx.Options.SectionScale, BentScale = ctx.Options.BentScale, TypicalSectionStation = sta, IncludeRebar = ctx.Options.IncludeRebar } };
                specs.Add(new SheetSpec($"Typical section {count} girders", $"Superstructure, {count} girders (STA {ctx.Units.FormatStation(sta)})",
                    new[] { Full(new ViewSpec("Typical section", s => SectionViews.TypicalSection(WithScale(ctx2, s)), ScaleFamily.Architectural)) }));
            }

        // Prestressed girders: one sheet (sections + elevation + schedule) instead of the steel girder and detail sheets.
        if (ctx.Prestressed is { Beams.Count: > 0 })
        {
            specs.Add(new SheetSpec("Prestressed girder details", $"TxDOT {ctx.Prestressed.Shape.Name} (IGD / IGND)", new[]
            {
                new Panel(new ViewSpec("Girder sections", s => PrestressedViews.Sections(WithScale(ctx, s)), ScaleFamily.Architectural), 0, H * 0.45, W * 0.45, H),
                new Panel(new ViewSpec("Girder elevation", s => PrestressedViews.Elevation(WithScale(ctx, s)), ScaleFamily.Engineering), W * 0.45, H * 0.45, W, H),
                new Panel(new ViewSpec("Girder schedule", _ => PrestressedViews.Schedule(ctx), ScaleFamily.Architectural, PaperSpace: true), 0, 0, W, H * 0.45),
            }));
            goto Substructure;
        }

        // Steel framing plans: spans grouped to about 330 ft of bridge per sheet.
        foreach (var (first, last) in FramingViews.SpanGroups(f, 330 * ctx.Units.LongUnit))
        {
            int a0 = first, a1 = last;
            string spans = a0 + 1 == a1 ? $"span {a1}" : $"spans {a0 + 1}-{a1}";
            specs.Add(new SheetSpec($"Framing plan {spans}", "Girders, cross frames, field splices and bearings",
                new[] { Full(new ViewSpec("Framing plan", s => FramingViews.FramingPlan(WithScale(ctx, s), a0, a1), ScaleFamily.Engineering)) }));
        }

        // Girder elevations, two per sheet.
        var girders = f.Girders.ToList();
        for (int i = 0; i < girders.Count; i += 2)
        {
            var panels = new List<Panel>();
            for (int k = 0; k < 2 && i + k < girders.Count; k++)
            {
                var g = girders[i + k];
                panels.Add(new Panel(new ViewSpec($"{g.Name} elevation", s => GirderViews.Elevation(WithScale(ctx, s), g.Index), ScaleFamily.Engineering),
                    0, H / 2 * (1 - k), W, H / 2 * (2 - k)));
            }
            string names = string.Join(" and ", panels.Select(p => p.View.Name.Split(' ')[0]));
            specs.Add(new SheetSpec($"Girder elevations {names}", "Plate sizes, splices, stiffeners and cross-frame locations", panels));
        }

        // Steel details.
        specs.Add(new SheetSpec("Steel details", "Cross frames and stiffeners", new[]
        {
            new Panel(new ViewSpec("Intermediate cross frame", s => SteelDetailViews.CrossFrame(WithScale(ctx, s), support: false), ScaleFamily.Architectural), 0, H * 0.5, W * 0.5, H),
            new Panel(new ViewSpec("Support cross frame", s => SteelDetailViews.CrossFrame(WithScale(ctx, s), support: true), ScaleFamily.Architectural), W * 0.5, H * 0.5, W, H),
            new Panel(new ViewSpec("Stiffener details", s => SteelDetailViews.Stiffeners(WithScale(ctx, s)), ScaleFamily.Architectural), 0, 0, W * 0.72, H * 0.5),
            new Panel(new ViewSpec("Steel notes", _ => SteelDetailViews.Notes(ctx), ScaleFamily.Architectural, PaperSpace: true), W * 0.72, 0, W, H * 0.5),
        }));

        // Field splice details (one per distinct type, two per sheet) and the schedule.
        var spliceGroups = SpliceViews.Groups(ctx);
        foreach (var gr in spliceGroups)
            specs.Add(new SheetSpec($"Field splice detail {gr.Letter}", $"Elevation and plan of bolted field splice type {gr.Letter}",
                new[] { Full(new ViewSpec($"Splice {gr.Letter}", s => SpliceViews.Detail(WithScale(ctx, s), gr), ScaleFamily.Architectural)) }));
        foreach (var (title, view) in SpliceViews.Schedule(ctx, W, H))
            specs.Add(new SheetSpec(title.Replace("FIELD SPLICE SCHEDULE", "Field splice schedule"), "Every field splice with its plates and bolts", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        var xgGroups = CrossGirderViews.Groups(ctx);
        for (int i = 0; i < xgGroups.Count; i += 2)
        {
            var panels = new List<Panel>();
            for (int k = 0; k < 2 && i + k < xgGroups.Count; k++)
            {
                var gr = xgGroups[i + k];
                panels.Add(new Panel(new ViewSpec(gr[0].Mark, s => CrossGirderViews.Detail(WithScale(ctx, s), gr), ScaleFamily.Architectural), 0, H / 2 * (1 - k), W, H / 2 * (2 - k)));
            }
            specs.Add(new SheetSpec("Cross girder details", "Cross girders carrying discontinuous girders", panels));
        }

    Substructure:
        // Bearing data and elevation tables.
        foreach (var (title, view) in TableViews.GirderSpacing(ctx, W, H))
            specs.Add(new SheetSpec("Girder spacing at supports", "Spacing, station and skew of every support line", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        foreach (var (title, view) in TableViews.BearingData(ctx, W, H))
            specs.Add(new SheetSpec(title.Replace("BEARING DATA", "Bearing data"), "Type, fixity, station and elevations at every bearing", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        foreach (var (title, view) in TableViews.CrossSlope(ctx, W, H))
            specs.Add(new SheetSpec("Cross slope geometrics", "Cross slope table of the alignment along the bridge", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        foreach (var (title, view) in TableViews.CamberBlocking(ctx, W, H))
            specs.Add(new SheetSpec("Camber and blocking", "Camber, deflections and blocking from the model", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        foreach (bool slab in new[] { true, false })
            foreach (var (title, view) in TableViews.ElevationTable(ctx, slab, W, H))
                specs.Add(new SheetSpec(slab ? "Top of slab elevations" : "Top of web elevations", "At the supports and quarter points along each girder", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        // Bearing details at the piers (anchor bolts).
        if (ctx.Substructure != null && ctx.Steel != null && ctx.Prestressed == null)
            foreach (var unit in ctx.Substructure.Units.Where(u => u.Type == SupportType.Pier))
                specs.Add(new SheetSpec($"{unit.Name} bearing details", "Anchor bolts, pedestal and cap clearances", new[]
                {
                    new Panel(new ViewSpec("Bearing plan", s => BearingViews.Plan(WithScale(ctx, s), unit), ScaleFamily.Architectural), 0, H * 0.42, W * 0.5, H),
                    new Panel(new ViewSpec("Section along girder", s => BearingViews.Section(WithScale(ctx, s), unit), ScaleFamily.Architectural), W * 0.5, H * 0.42, W, H),
                    new Panel(new ViewSpec("Anchor bolt checks", _ => BearingViews.Checks(ctx, unit), ScaleFamily.Architectural, PaperSpace: true), 0, 0, W, H * 0.42),
                }));

        // Pier cap plan and footing / pile layout.
        if (ctx.Substructure != null)
            foreach (var unit in ctx.Substructure.Units.Where(u => u.Type == SupportType.Pier))
            {
                var panels = new List<Panel> { new Panel(new ViewSpec(unit.Name + " cap plan", s => PierViews.CapPlan(WithScale(ctx, s), unit), ScaleFamily.Architectural), 0, 0, unit.Foundations.Count > 0 ? W * 0.5 : W, H) };
                if (unit.Foundations.Count > 0)
                    panels.Add(new Panel(new ViewSpec(unit.Name + " pile layout", s => PierViews.PileLayout(WithScale(ctx, s), unit), ScaleFamily.Architectural), W * 0.5, 0, W, H));
                specs.Add(new SheetSpec($"{unit.Name} cap plan and piles", "Cap plan with pedestals and columns, footing and pile layout", panels));
            }

        // Bents and abutments.
        if (ctx.Substructure != null)
            foreach (var unit in ctx.Substructure.Units)
                if (unit.Abutment != null)
                    specs.Add(new SheetSpec($"{unit.Name} abutment", "Rear elevation, section, piling and wing layout", new[]
                    {
                        new Panel(new ViewSpec(unit.Name + " rear elevation", s => AbutmentViews.Abutment(WithScale(ctx, s), unit, 0), ScaleFamily.Architectural), 0, H * 0.55, W, H),
                        new Panel(new ViewSpec(unit.Name + " piling", s => AbutmentViews.Abutment(WithScale(ctx, s), unit, 2), ScaleFamily.Architectural), 0, 0, W * 0.68, H * 0.55),
                        new Panel(new ViewSpec(unit.Name + " section", s => AbutmentViews.Abutment(WithScale(ctx, s), unit, 1), ScaleFamily.Architectural), W * 0.68, 0, W, H * 0.55),
                    }));
                else
                specs.Add(new SheetSpec($"{unit.Name} {(unit.Type == SupportType.Pier ? "bent" : "abutment")}", "Elevation and cap section",
                    new[] { Full(new ViewSpec(unit.Name, s => BentViews.Bent(WithScale(ctx, s), unit), ScaleFamily.Architectural)) }));

        foreach (var (title, view) in TableViews.Quantities(ctx, W, H))
            specs.Add(new SheetSpec("Estimated quantities", "Steel, concrete and reinforcing from the generated model", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));

        // Bar lists.
        foreach (var (title, view) in BarSheet.Pages(ctx, W, H))
            specs.Add(new SheetSpec(title.Replace("BAR LIST — ", "Bar list "), "Bar list and bending diagrams", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        return specs;
    }

    /// <summary>Stations (middle of the span) of the first span with each distinct number of girders.</summary>
    public static List<(double station, int count)> TypicalStations(BridgeFraming f)
    {
        var res = new List<(double, int)>();
        for (int j = 0; j + 1 < f.Supports.Count; j++)
        {
            double sta = (f.Supports[j].Station + f.Supports[j + 1].Station) / 2;
            int n = f.Girders.Count(g =>
            {
                double lo = Math.Min(g.Path.Station[0], g.Path.Station[^1]), hi = Math.Max(g.Path.Station[0], g.Path.Station[^1]);
                return sta >= lo && sta <= hi;
            });
            if (n > 0 && res.All(r => r.Item2 != n)) res.Add((sta, n));
        }
        return res;
    }

    public static List<Sheet> Build(ViewContext ctx, SheetFormat fmt)
    {
        var specs = Specs(ctx, fmt);
        var sheets = new Sheet[specs.Count];
        Parallel.For(0, specs.Count, i => sheets[i] = SheetComposer.Compose(specs[i], fmt, ctx.Units));
        return SheetComposer.Number(sheets.ToList());
    }
}
