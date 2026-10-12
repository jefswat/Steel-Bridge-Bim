using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>Tied arch 2D views (minimal): elevation of the south arch (rib working line, tie, hangers, splices, floorbeam panel points) and
/// framing plan (ties, floorbeams, ribs and upper bracing projected, knuckle end lines), both in the local structure coordinate system.</summary>
public static class ArchViews
{
    public static SheetView Elevation(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        var am = ctx.Steel?.Arch;
        if (am == null) return new SheetView("Arch elevation", "Arch Elevation", dwg, d.Scale, "");
        var def = ctx.Framing.Definition.Arch!;
        Vec2 XZ(Vec3 local) => new(local.X, local.Z);
        var rib = am.Ribs.FirstOrDefault(r => r.Name.StartsWith("South", StringComparison.OrdinalIgnoreCase)) ?? am.Ribs.FirstOrDefault();
        if (rib == null) return new SheetView("Arch elevation", "Arch Elevation", dwg, d.Scale, "");
        // Rib working line and its flange outlines (W depth is normal to the arch plane: in elevation the rib shows its flange width).
        var pts = Enumerable.Range(0, 121).Select(i => XZ(rib.PointAt(rib.ThetaStart + (rib.ThetaEnd - rib.ThetaStart) * i / 120))).ToList();
        d.Poly(SheetLayers.Center, pts);
        var w = Steel.SteelCatalog.FindWideFlange(def.Ribs.First(r => r.Name == rib.Name).Section);
        if (w != null)
            foreach (int s in new[] { -1, 1 })
                d.Poly(SheetLayers.Steel, Enumerable.Range(0, 121).Select(i =>
                {
                    double th = rib.ThetaStart + (rib.ThetaEnd - rib.ThetaStart) * i / 120;
                    var p = rib.PointAt(th); var rad = (p - rib.Center).Normalized();
                    return XZ(p + rad * (s * w.FlangeWidth / 2));
                }));
        // Tie (working line at Z = 0) and its depth.
        var tie = am.Ties.FirstOrDefault(t => t.Name.StartsWith("South", StringComparison.OrdinalIgnoreCase)) ?? am.Ties.FirstOrDefault();
        double x0 = pts[0].X, x1 = pts[^1].X;
        var td = def.Ties.FirstOrDefault(t => t.Name == tie?.Name);
        double tdep = td != null ? td.FlangeThickness + td.WebHeight : 0;
        d.Rect(SheetLayers.Steel, x0, 0, x1, tdep);
        // Hangers (south): tie pin -> rib pin.
        foreach (var h in am.Hangers.Where(h => h.Name.EndsWith("-S")))
            d.Line(SheetLayers.SteelHidden, XZ(am.Local(h.TieAnchorWorld)), XZ(am.Local(h.RibAnchorWorld)));
        // Panel points, rib and tie splices.
        if (tie != null)
            foreach (var (name, p) in tie.PanelPoints)
                d.Line(SheetLayers.Grid, new Vec2(p.X, -d.Paper(0.06)), new Vec2(p.X, 0));
        foreach (var r in def.Ribs.Where(r => r.Name == rib.Name))
            foreach (var sn in r.Splices)
                if (def.Point(sn) is { } sp)
                {
                    var q = XZ(sp.Vec);
                    d.Circle(SheetLayers.Dims, q, d.Paper(0.04));
                    d.Text(sn, q + new Vec2(d.Paper(0.05), d.Paper(0.05)), d.SmallTextH);
                }
        if (td != null)
            foreach (var sp in td.SpliceAt)
            {
                double x = tie!.PanelPoints.Count > 0 ? XAlong(tie, sp.Distance, td) : 0;
                d.Line(SheetLayers.Dims, new Vec2(x, -d.Paper(0.1)), new Vec2(x, tdep + d.Paper(0.1)));
                d.Text(sp.Name, new Vec2(x, -d.Paper(0.25)), d.SmallTextH, 0, TextAlign.Center);
            }
        d.Dim(new Vec2(x0, 0), new Vec2(x1, 0), -0.6, $"{u.FormatLength(x1 - x0)} CHORD (LOCAL X)");
        var crown = pts.OrderByDescending(p => p.Y).First();
        d.Dim(new Vec2(crown.X, 0), crown, 0.4, $"{u.FormatLength(rib.Rise)} RISE");
        d.Title("ARCH ELEVATION (SOUTH ARCH)", $"Local structure coordinates (X along the chord, Z normal to the tilted plane) · R {u.FormatLength(rib.Radius)} · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((x0 + x1) / 2, -d.Paper(1.0)));
        return new SheetView("Arch elevation", "Arch Elevation", dwg, d.Scale, "Tied arch elevation: rib, tie, hangers, rib / tie splices.");
    }

    /// <summary>Local X of a tie chain distance (interpolated between the panel points / ends).</summary>
    private static double XAlong(Arch.ArchTieResult tie, double s, Bridge.ArchTieDefinition td)
    {
        var pp = td.PanelPoints.Zip(tie.PanelPoints, (a, b) => (a.Distance, b.Local.X)).OrderBy(x => x.Distance).ToList();
        for (int i = 1; i < pp.Count; i++)
            if (s <= pp[i].Distance) { double t = (s - pp[i - 1].Distance) / (pp[i].Distance - pp[i - 1].Distance); return pp[i - 1].X + (pp[i].X - pp[i - 1].X) * t; }
        return pp.Count > 0 ? pp[^1].X : 0;
    }

    public static SheetView FramingPlan(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        var am = ctx.Steel?.Arch;
        if (am == null) return new SheetView("Arch framing plan", "Arch Framing", dwg, d.Scale, "");
        var def = ctx.Framing.Definition.Arch!;
        Vec2 XY(Vec3 world) { var l = am.Local(world); return new Vec2(l.X, l.Y); }
        // Ties: CL from the panel points and the end points.
        foreach (var t in def.Ties)
        {
            var res = am.Ties.FirstOrDefault(x => x.Name == t.Name);
            if (res == null) continue;
            var line = new List<Vec2>();
            if (def.Point(t.Start) is { } a) line.Add(new Vec2(a.X, a.Y));
            line.AddRange(res.PanelPoints.Select(p => new Vec2(p.Local.X, p.Local.Y)));
            if (def.Point(t.End) is { } b) line.Add(new Vec2(b.X, b.Y));
            d.Poly(SheetLayers.Steel, line);
        }
        // Floorbeams.
        foreach (var fb in am.Floorbeams)
        {
            var a = XY(fb.EndA); var b = XY(fb.EndB);
            d.Line(SheetLayers.Steel, a, b);
            if (fb.Name is "FB1" or "FB17" or "FB33") d.Text(fb.Name, (a + b) * 0.5 + new Vec2(d.Paper(0.05), 0), d.SmallTextH, 90);
        }
        // Ribs projected (working line) and upper bracing.
        foreach (var rib in am.Ribs)
            d.Poly(SheetLayers.SteelHidden, Enumerable.Range(0, 81).Select(i => { var p = rib.PointAt(rib.ThetaStart + (rib.ThetaEnd - rib.ThetaStart) * i / 80); return new Vec2(p.X, p.Y); }));
        foreach (var br in def.Bracing)
            if (def.Point(br.From) is { } a && def.Point(br.To) is { } b) d.Line(SheetLayers.SteelHidden, new Vec2(a.X, a.Y), new Vec2(b.X, b.Y));
        // Knuckle end lines.
        foreach (var k in def.Knuckles)
            if (def.Point(k.EndFrom) is { } a && def.Point(k.EndTo) is { } b) d.Line(SheetLayers.Center, new Vec2(a.X, a.Y), new Vec2(b.X, b.Y));
        var xs = am.Floorbeams.SelectMany(f => new[] { XY(f.EndA), XY(f.EndB) }).ToList();
        double xmid = xs.Count > 0 ? (xs.Min(p => p.X) + xs.Max(p => p.X)) / 2 : 0, ymin = xs.Count > 0 ? xs.Min(p => p.Y) : 0;
        d.Title("ARCH SPAN FRAMING PLAN", $"Ties, floorbeams FB1-FB{am.Floorbeams.Count}, ribs and upper lateral bracing (dashed) in local plan · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2(xmid, ymin - d.Paper(1.2)));
        return new SheetView("Arch framing plan", "Arch Framing", dwg, d.Scale, "Tied arch framing plan.");
    }
}
