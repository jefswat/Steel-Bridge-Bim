using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Sheets for items the model has but the girder / pier sheets do not show: deck and pier drains, sign support brackets, crash cushion end
/// posts, light pole bases and conduits (location plan + schedule), and the dapped girder ends (true-scale detail from the generated dap).
/// </summary>
public static class MiscViews
{
    /// <summary>One located item: group, name, description, plan centroid, element count.</summary>
    public sealed record Item(string Group, string Name, string Description, Vec2 At, int Elements);

    public static List<Item> Items(ViewContext ctx)
    {
        var list = new List<Item>();
        var sub = ctx.Substructure;
        if (sub == null) return list;
        Vec2 Centroid(IEnumerable<Solids.Mesh> meshes)
        {
            var b = Box2.Empty;
            foreach (var m in meshes) foreach (var p in m.Positions) b.Add(p.XY);
            return b.IsEmpty ? Vec2.Zero : b.Center;
        }
        // Pier / deck drains of the substructure (one item per drain: inlet + downspout + basin).
        var inlets = sub.Drainage.Where(x => x.Kind == SubElementKind.DeckDrain).ToList();
        string Key(SubSolid x) => inlets.Where(i => x.Name.StartsWith(i.Name, StringComparison.Ordinal)).OrderByDescending(i => i.Name.Length).FirstOrDefault()?.Name ?? x.Name;
        foreach (var g in sub.Drainage.GroupBy(Key))
        {
            var inlet = g.FirstOrDefault(x => x.Kind == SubElementKind.DeckDrain);
            string desc = string.Join("; ", g.Select(x => x.Description).Distinct());
            list.Add(new Item("DRAINAGE", g.Key, desc, Centroid(inlet != null ? new[] { inlet.Mesh } : g.Select(x => x.Mesh)), g.Count()));
        }
        if (sub.Misc is { } misc)
            foreach (var a in misc.Assemblies)
            {
                var els = misc.Elements.Where(e => e.Assembly == a.Name).ToList();
                if (els.Count == 0) continue;
                list.Add(new Item(a.Group switch { MiscGroup.SignSupport => "SIGN SUPPORT", MiscGroup.CrashCushion => "CRASH CUSHION", _ => a.Group.ToString().ToUpperInvariant() },
                    a.Name, a.Description, Centroid(els.Select(e => e.Mesh)), els.Count));
            }
        return list;
    }

    public static bool Any(ViewContext ctx) => Items(ctx).Count > 0;

    /// <summary>Developed plan of the deck with every item at its location (numbered symbol by group).</summary>
    public static SheetView LocationPlan(ViewContext ctx)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        Vec2 Dev(Vec2 p) { var so = a.Inverse(p); return new Vec2(so.Station, -so.Offset); }
        if (f.Deck != null) d.Poly(SheetLayers.Concrete, f.Deck.Outline.Select(p => Dev(p.XY)), true);
        foreach (var g in f.Girders) d.Poly(SheetLayers.SteelHidden, g.WebLine.Select(p => Dev(p.XY)));
        foreach (var sl in f.Supports)
        {
            var pts = sl.Bearings.OfType<BearingResult>().Select(b => new Vec2(b.Station, -b.Offset)).OrderBy(p => p.Y).ToList();
            if (pts.Count < 2) continue;
            var dir = (pts[^1] - pts[0]).Normalized();
            d.Line(SheetLayers.Center, pts[0] - dir * d.Paper(0.25), pts[^1] + dir * d.Paper(0.25));
            d.TextAlong(sl.Name.ToUpperInvariant(), pts[^1] + dir * d.Paper(0.3), dir, d.SmallTextH);
        }
        var items = Items(ctx);
        var groups = items.Select(i => i.Group).Distinct().ToList();
        string Sym(string g) => g switch { "DRAINAGE" => "D", "SIGN SUPPORT" => "S", "CRASH CUSHION" => "C", "LIGHTING" => "L", _ => g[..1] };
        var count = new Dictionary<string, int>();
        foreach (var it in items)
        {
            var p = Dev(it.At);
            int n = count[it.Group] = count.GetValueOrDefault(it.Group) + 1;
            d.Circle(SheetLayers.Steel, p, d.Paper(0.06));
            d.Text($"{Sym(it.Group)}{n}", p + new Vec2(0, d.Paper(0.13)), d.SmallTextH * 0.85, 0, TextAlign.Center);
        }
        var box = dwg.Extents();
        d.Text(string.Join("   ", groups.Select(g => $"{Sym(g)} = {g}")) + "   (NUMBERS AS IN THE SCHEDULE)", new Vec2(box.MinX, box.MinY - d.Paper(0.25)), d.SmallTextH, 0, TextAlign.Left, SheetLayers.Notes);
        box = dwg.Extents();
        d.Title("MISCELLANEOUS ITEMS - LOCATION PLAN", $"Drains, sign supports, crash cushions, lighting from the model; developed along the CL alignment · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((box.MinX + box.MaxX) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView("Misc items plan", "Misc items plan", dwg, d.Scale, "Location plan of the miscellaneous items.");
    }

    /// <summary>Schedule of the items: mark, group, name, description, station / offset, element count.</summary>
    public static List<(string title, SheetView view)> Schedule(ViewContext ctx, double areaW, double areaH)
    {
        var u = ctx.Units;
        var a = ctx.Framing.Alignment;
        string Sym(string g) => g switch { "DRAINAGE" => "D", "SIGN SUPPORT" => "S", "CRASH CUSHION" => "C", "LIGHTING" => "L", _ => g[..1] };
        var count = new Dictionary<string, int>();
        var rows = new List<string[]>();
        foreach (var it in Items(ctx))
        {
            int n = count[it.Group] = count.GetValueOrDefault(it.Group) + 1;
            var so = a.Inverse(it.At);
            rows.Add(new[] { $"{Sym(it.Group)}{n}", it.Group, it.Name.ToUpperInvariant(), it.Description, u.FormatStation(so.Station), $"{u.FormatLength(Math.Abs(so.Offset))} {(so.Offset < 0 ? "LT" : "RT")}", it.Elements.ToString() });
        }
        if (rows.Count == 0) return new();
        return TableViews.PagedPublic(ctx, "MISCELLANEOUS ITEMS (FROM THE MODEL)",
            new[] { ("MARK", 0.5), ("GROUP", 1.15), ("ITEM", 2.9), ("DESCRIPTION", 8.0), ("STATION", 1.2), ("OFFSET", 1.1), ("PARTS", 0.5) }, rows,
            new[] { "STATION / OFFSET: CENTER OF THE ITEM'S MODELED PARTS, REFERENCED TO THE CL ALIGNMENT. DESCRIPTIONS AS GENERATED (SIZES, ELEVATIONS, ANCHORS)." },
            areaW, areaH, "Misc items");
    }

    /// <summary>Supports with dapped girder ends in the model.</summary>
    public static List<string> DapSupports(ViewContext ctx) =>
        ctx.Steel == null ? new() : ctx.Framing.Definition.Supports.Concat(ctx.Framing.Definition.AuxiliarySupports).Where(s => s.GirderEndDap != null).Select(s => s.Name).ToList();

    /// <summary>True-scale elevation of the dapped end of the first girder dapped at <paramref name="support"/>: lowered top flange, slope, bends,
    /// cope tabs, CL bearing and the dap dimensions (from the generated <see cref="GirderEndDap"/>).</summary>
    public static SheetView DapDetail(ViewContext ctx, string support)
    {
        var f = ctx.Framing;
        var def = f.Definition;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        GirderEndDap? dap = null; GirderResult? gr = null;
        foreach (var g in f.Girders)
        {
            dap = GirderEndDap.For(def, f, g, _ => { }).FirstOrDefault(x => x.SupportName == support);
            if (dap != null) { gr = g; break; }
        }
        if (dap == null || gr == null) return new SheetView($"Dap {support}", "Dap", dwg, d.Scale, "");
        var sec = def.Girders.Section;
        double D = sec.WebDepth;
        double sMid = dap.AtStart ? 0 : gr.Length;
        var tfSeg = sec.Segments(GirderPlate.TopFlange)[dap.AtStart ? 0 : ^1];
        var bfSeg = sec.Segments(GirderPlate.BottomFlange)[dap.AtStart ? 0 : ^1];
        double tf = tfSeg.Thickness, tb = bfSeg.Thickness;
        double xMax = dap.Extent + Ft(2);
        // x = distance from the girder end into the girder (drawn to the right).
        var xs = Enumerable.Range(0, 241).Select(i => xMax * i / 240.0).Concat(dap.Samples().Select(dap.X)).Where(x => x >= 0 && x <= xMax).Distinct().OrderBy(x => x).ToList();
        var topWeb = xs.Select(x => new Vec2(x, D - dap.Drop(dap.S(x)))).ToList();
        d.Poly(SheetLayers.Steel, topWeb);
        d.Poly(SheetLayers.Steel, topWeb.Select(p => p + new Vec2(0, tf)));
        d.Line(SheetLayers.Steel, new Vec2(0, 0), new Vec2(xMax, 0));
        d.Line(SheetLayers.Steel, new Vec2(0, -tb), new Vec2(xMax, -tb));
        // Web end with the cope tabs (cut from the web edge at the top and bottom over the cope length).
        if (dap.HasCope)
        {
            var cx = dap.CopeSamples().Select(dap.X).Where(x => x >= 0).OrderBy(x => x).ToList();
            d.Poly(SheetLayers.Steel, cx.Select(x => new Vec2(x, topWeb[0].Y - dap.CopeDepth(dap.S(x)))));
            d.Poly(SheetLayers.Steel, cx.Select(x => new Vec2(x, dap.CopeDepth(dap.S(x)))));
        }
        d.Line(SheetLayers.Steel, new Vec2(0, -tb), new Vec2(0, topWeb[0].Y + tf));
        d.Poly(SheetLayers.Steel, new[] { new Vec2(xMax, -tb - d.Paper(0.1)), new Vec2(xMax + d.Paper(0.05), D / 2), new Vec2(xMax, D + tf + d.Paper(0.1)) });
        // CL bearing and the bearing under it.
        double xb = dap.XBearing;
        d.Line(SheetLayers.Center, new Vec2(xb, -tb - d.Paper(0.5)), new Vec2(xb, D + tf + d.Paper(0.3)));
        d.Text("CL BRG", new Vec2(xb, D + tf + d.Paper(0.42)), d.SmallTextH, 0, TextAlign.Center);
        d.Poly(SheetLayers.Bearing, new[] { new Vec2(xb, -tb), new Vec2(xb - d.Paper(0.08), -tb - d.Paper(0.12)), new Vec2(xb + d.Paper(0.08), -tb - d.Paper(0.12)) }, true);
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        var dd = dap.Def;
        double yTop = D + tf + d.Paper(0.75);
        double x2 = xb + dd.LowFlatFromBearing, x1 = x2 + dd.DropRun;
        var chain = new[] { 0, xb, x2, x1 };
        d.DimChain(chain.Select(x => new Vec2(x, yTop)).ToList(), 0, i => S(chain[i + 1] - chain[i]));
        d.Dim(new Vec2(x1 + d.Paper(0.3), D - dd.FlangeDrop), new Vec2(x1 + d.Paper(0.3), D), 0.2, $"{S(dd.FlangeDrop)} DROP");
        d.Dim(new Vec2(-d.Paper(0.35), 0), new Vec2(-d.Paper(0.35), D - dd.FlangeDrop), -0.2, $"{S(D - dd.FlangeDrop)} WEB AT END");
        var notes = new List<string>
        {
            $"TOP FLANGE LOWERED {S(dd.FlangeDrop)} OVER {S(dd.DropRun)}, BENDS R = {S(dd.BendRadius)}; LOW FLAT {S(dd.LowFlatFromBearing)} PAST CL BRG",
            dap.HasCope ? $"COPE TABS: INSET {S(dd.CopeInset)}, EDGE {S(dd.CopeEdge)}, RUN {S(dd.CopeRun)}, R = {S(dd.CopeRadius)}" : "NO COPE",
            $"WEB {S(D)} x {S(sec.WebThickness)}; TOP FLANGE {S(tfSeg.Width)} x {S(tf)}; BOTTOM FLANGE {S(bfSeg.Width)} x {S(tb)}" + (dd.Source != "" ? $"; SOURCE {dd.Source}" : ""),
        };
        int dappedGirders = f.Girders.Count(g => GirderEndDap.For(def, f, g, _ => { }).Any(x => x.SupportName == support));
        notes.Add($"{dappedGirders} GIRDER END(S) DAPPED AT {support.ToUpperInvariant()} (THE MODEL'S DAP DEFINITION FOR THIS SUPPORT)");
        // Notes beside the detail (wrapped to 4"), so the paper-sized text does not decide the scale.
        double yn = D + tf;
        foreach (var line in notes.SelectMany(n => PrestressedViews.Wrap(n, 4.0, 0.075)))
        {
            d.Text(line, new Vec2(xMax + d.Paper(0.6), yn), d.SmallTextH, 0, TextAlign.Left, SheetLayers.Notes);
            yn -= d.SmallTextH * 1.6;
        }
        var box = dwg.Extents();
        d.Title($"DAPPED GIRDER END AT {support.ToUpperInvariant()}", $"{gr.Name} {(dap.AtStart ? "start" : "end")}, elevation of the web CL, true scale · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2(xMax / 2, box.MinY - d.Paper(0.45)));
        return new SheetView($"Dap {support}", $"Dap {support}", dwg, d.Scale, "Dapped girder end detail from the generated dap.");
    }
}
