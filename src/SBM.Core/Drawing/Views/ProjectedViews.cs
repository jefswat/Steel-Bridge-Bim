using SBM.Core.Drawing.Projection;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Sheet views made by the general 3D -> 2D projector (<see cref="ModelProjector"/>): true views of the generated solids with hidden lines,
/// instead of linework recreated from generator data. Today: the tied arch framing plan and a developed girder elevation (detail windows).
/// </summary>
public static class ProjectedViews
{
    /// <summary>A projection result as a sheet view at a scale: the projected linework (scale independent) + title, subtitle and scale.</summary>
    public static SheetView View(ProjectionResult r, string name, string title, string? subtitle, double scale, UnitSystem u)
    {
        var dwg = new PlanDrawing();
        foreach (var l in r.Drawing.Layers)
        {
            var t = dwg.Layer(l.Style);
            t.Polylines.AddRange(l.Polylines);
            t.Texts.AddRange(l.Texts);
            t.Circles.AddRange(l.Circles);
        }
        var d = new Drafter(dwg, u, scale);
        var e = r.Drawing.Extents();
        if (!e.IsEmpty)
            d.Title(title, (subtitle != null ? subtitle + " · " : "") + $"Scale {DrawingOptions.ScaleText(scale, u)}", new Vec2(e.Center.X, e.MinY - d.Paper(0.35)));
        return new SheetView(name, name, dwg, scale, "Projected from the 3D model (hidden-line removal; hidden edges dashed).");
    }

    /// <summary>Items of the bridge in a view context (steel without welds, concrete, substructure, prestressed).</summary>
    public static List<ProjectionItem> Items(ViewContext ctx, ProjectionSourceOptions? o = null) =>
        ModelProjectionSource.Items(ctx.Framing.Definition.Name, ctx.Steel, ctx.Concrete, ctx.Substructure, ctx.Prestressed, o);

    /// <summary>Top view of the tied arch (ribs, ties, floorbeams, bracing, knuckles, hangers, deck) with x along the arch's local X.</summary>
    public static ProjectionResult? ArchPlan(ViewContext ctx, ProjectionOptions? o = null, bool pt = false)
    {
        var am = ctx.Steel?.Arch;
        if (am == null) return null;
        var items = Items(ctx, new ProjectionSourceOptions { Layers = pt ? new[] { "-3D-ARCH-FORM-DECK" } : new[] { "-3D-ARCH-PT", "-3D-ARCH-FORM-DECK" }, Transparent = new[] { "3D-DECK" } });
        var x = am.Frame.X; var up = new Vec3(-x.Y, x.X, 0);   // horizontal local X to the right
        var view = new OrthographicView(am.Frame.Origin, new Vec3(0, 0, -1), up);
        var r = ModelProjector.Project(items, view, o ?? new ProjectionOptions());
        r.DropLayers("3D-DECK-HIDDEN");
        return r;
    }

    /// <summary>
    /// Developed elevation of one girder (viewed from the right, x = distance along the web CL from the girder start as on the girder
    /// elevation sheets, y = elevation): only that girder's parts (plates, stiffeners, splice plates, bearings), near plane just right of the
    /// widest flange so the next girder never hides it. <paramref name="window"/> = (s0, s1) distance range, null = whole girder.
    /// </summary>
    public static ProjectionResult? GirderElevation(ViewContext ctx, int gi, (double s0, double s1)? window = null, ProjectionOptions? o = null)
    {
        if (ctx.Steel == null || gi < 0 || gi >= ctx.Framing.Girders.Count) return null;
        var g = ctx.Framing.Girders[gi];
        var items = Items(ctx, new ProjectionSourceOptions { Concrete = false, Substructure = false, Prestressed = false, Girder = gi });
        if (items.Count == 0) return null;
        var sec = ctx.Framing.Definition.Girders.Section;
        double half = new[] { sec.Segments(Bridge.GirderPlate.TopFlange), sec.Segments(Bridge.GirderPlate.BottomFlange) }.SelectMany(s => s).Select(s => s.Width).DefaultIfEmpty(0.6).Max() / 2;
        var view = DevelopedView.AlongGirder(g);
        var dv = view.WithSlab(-(half + 0.15), null, name: $"girder {ctx.GirderName(gi)}");
        var opt = o ?? new ProjectionOptions();
        if (window is { } w) opt.Window = new Box2 { MinX = w.s0, MaxX = w.s1, MinY = double.MinValue / 4, MaxY = double.MaxValue / 4 };
        return ModelProjector.Project(items, dv, opt);
    }

    /// <summary>Sheet specs of the projected views appended to the plan set (after every existing sheet, so sheet numbers do not move).</summary>
    public static IEnumerable<SheetSpec> Specs(ViewContext ctx, double W, double H)
    {
        if (ctx.Steel?.Arch != null)
        {
            var plan = new Lazy<ProjectionResult?>(() => ArchPlan(ctx));
            yield return new SheetSpec("Arch framing plan (projected)", "Top view of the 3D model: ribs, ties, floorbeams, bracing, knuckles, hangers, deck (hidden lines dashed)",
                new[] { new Panel(new ViewSpec("Arch plan (projected)", s => plan.Value is { } r ? View(r, "Arch plan (projected)", "ARCH SPAN FRAMING PLAN (PROJECTED FROM THE MODEL)",
                    "Top view, x along the arch chord", s, ctx.Units) : Empty(s), ScaleFamily.Engineering), 0, 0, W, H) });
            yield break;
        }
        if (ctx.Steel == null || ctx.Framing.Girders.Count == 0 || ctx.Prestressed is { Beams.Count: > 0 }) yield break;
        const int gi = 0;
        var g = ctx.Framing.Girders[gi];
        double L = g.Path.Length;
        string gn = ctx.GirderTitle(gi);
        double ft = ctx.Units.IsUS ? 0.3048 : 1;
        double half = 20 * ft;   // detail windows +- 20 ft
        var splice = ctx.Framing.Splices.Where(x => x.Girder == gi).Select(x => x.GirderDistance).OrderBy(x => x).Cast<double?>().FirstOrDefault();
        double? brg = null;
        for (int j = 1; j + 1 < ctx.Framing.Supports.Count && brg == null; j++)
            if (ctx.Framing.Supports[j].Bearings.ElementAtOrDefault(gi) != null && j < g.Spans.Count) brg = g.DistanceAt(j, 0);
        var whole = new Lazy<ProjectionResult?>(() => GirderElevation(ctx, gi));
        var panels = new List<Panel>
        {
            new(new ViewSpec($"{gn} elevation (projected)", s => whole.Value is { } r ? View(r, "Girder elevation (projected)", $"{gn} ELEVATION (PROJECTED FROM THE MODEL)",
                "Developed along the web CL, to scale", s, ctx.Units) : Empty(s), ScaleFamily.Engineering), 0, H * 0.62, W, H),
        };
        void Detail(double c, string what, double x0, double x1)
        {
            double a = Math.Max(-1, c - half), b = Math.Min(L + 1, c + half);
            var r = new Lazy<ProjectionResult?>(() => GirderElevation(ctx, gi, (a, b)));
            panels.Add(new Panel(new ViewSpec($"{gn} at {what}", s => r.Value is { } rr ? View(rr, $"Girder at {what}", $"{gn} AT {what.ToUpperInvariant()} (PROJECTED)",
                $"{ctx.Units.FormatLength(a)} to {ctx.Units.FormatLength(b)} along the girder", s, ctx.Units) : Empty(s), ScaleFamily.Architectural), x0, 0, x1, H * 0.6));
        }
        if (splice is { } sp) Detail(sp, "first field splice", 0, W * 0.5);
        if (brg is { } bp) Detail(bp, "first interior bearing", W * 0.5, W);
        yield return new SheetSpec($"{gn} elevation (projected)", "Developed girder elevation projected from the 3D model with hidden lines (no tessellation or schematic lines)", panels);
    }

    private static SheetView Empty(double s) => new("Empty", "Empty", new PlanDrawing(), s, "");
}
