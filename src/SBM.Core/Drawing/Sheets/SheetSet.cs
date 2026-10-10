using SBM.Core.Bridge;
using SBM.Core.Drawing.Views;

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

    Substructure:
        // Bearing details at the piers (anchor bolts).
        if (ctx.Substructure != null && ctx.Steel != null)
            foreach (var unit in ctx.Substructure.Units.Where(u => u.Type == SupportType.Pier))
                specs.Add(new SheetSpec($"{unit.Name} bearing details", "Anchor bolts, pedestal and cap clearances", new[]
                {
                    new Panel(new ViewSpec("Bearing plan", s => BearingViews.Plan(WithScale(ctx, s), unit), ScaleFamily.Architectural), 0, H * 0.42, W * 0.5, H),
                    new Panel(new ViewSpec("Section along girder", s => BearingViews.Section(WithScale(ctx, s), unit), ScaleFamily.Architectural), W * 0.5, H * 0.42, W, H),
                    new Panel(new ViewSpec("Anchor bolt checks", _ => BearingViews.Checks(ctx, unit), ScaleFamily.Architectural, PaperSpace: true), 0, 0, W, H * 0.42),
                }));

        // Bents and abutments.
        if (ctx.Substructure != null)
            foreach (var unit in ctx.Substructure.Units)
                specs.Add(new SheetSpec($"{unit.Name} {(unit.Type == SupportType.Pier ? "bent" : "abutment")}", "Elevation and cap section",
                    new[] { Full(new ViewSpec(unit.Name, s => BentViews.Bent(WithScale(ctx, s), unit), ScaleFamily.Architectural)) }));

        // Bar lists.
        foreach (var (title, view) in BarSheet.Pages(ctx, W, H))
            specs.Add(new SheetSpec(title.Replace("BAR LIST — ", "Bar list "), "Bar list and bending diagrams", new[] { Full(new ViewSpec(title, _ => view, ScaleFamily.Architectural, PaperSpace: true)) }));
        return specs;
    }

    public static List<Sheet> Build(ViewContext ctx, SheetFormat fmt)
    {
        var specs = Specs(ctx, fmt);
        var sheets = new Sheet[specs.Count];
        Parallel.For(0, specs.Count, i => sheets[i] = SheetComposer.Compose(specs[i], fmt, ctx.Units));
        return SheetComposer.Number(sheets.ToList());
    }
}
