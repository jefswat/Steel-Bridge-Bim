using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Steel framing plan: girders, cross frames with their marks, field splices, bearings and support lines, drawn in the
/// developed plan (X = station along the CL alignment, Y = minus the offset) for a station range (one or more spans).
/// Everything comes from the generated framing; nothing is drawn that the model does not have.
/// </summary>
public static class FramingViews
{
    /// <summary>Consecutive span groups (first support index, last support index) no longer than <paramref name="maxLength"/> along the CL.</summary>
    public static List<(int first, int last)> SpanGroups(BridgeFraming f, double maxLength)
    {
        var groups = new List<(int, int)>();
        int i = 0, n = f.Supports.Count;
        while (i < n - 1)
        {
            int j = i + 1;
            while (j + 1 < n && Math.Abs(f.Supports[j + 1].Station - f.Supports[i].Station) <= maxLength) j++;
            groups.Add((i, j));
            i = j;
        }
        return groups;
    }

    private static void ClipPoly(Drafter d, LayerStyle layer, IReadOnlyList<Vec2> pts, double x0, double x1)
    {
        var cur = new List<Vec2>();
        void Flush() { if (cur.Count > 1) d.Poly(layer, cur); cur = new List<Vec2>(); }
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
            if (hi < x0 || lo > x1) { Flush(); continue; }
            double ta = 0, tb = 1;
            double dx = b.X - a.X;
            if (Math.Abs(dx) > 1e-12)
            {
                double t0 = (x0 - a.X) / dx, t1 = (x1 - a.X) / dx;
                ta = Math.Max(0, Math.Min(t0, t1)); tb = Math.Min(1, Math.Max(t0, t1));
            }
            var pa = Vec2.Lerp(a, b, ta); var pb = Vec2.Lerp(a, b, tb);
            if (cur.Count == 0 || Vec2.Distance(cur[^1], pa) > 1e-9) { Flush(); cur.Add(pa); }
            cur.Add(pb);
            if (tb < 1) Flush();
        }
        Flush();
    }

    private static void Mark(Drafter d, string text, Vec2 at, Vec2 dir)
    {
        double ang = Math.Atan2(dir.Y, dir.X);
        while (ang > Math.PI / 2) ang -= Math.PI;
        while (ang <= -Math.PI / 2) ang += Math.PI;
        if (Math.Abs(Math.Abs(ang) - Math.PI / 2) < Math.PI / 4) ang = Math.PI / 2;
        d.Text(text, at, d.SmallTextH * 0.8, ang, TextAlign.Center);
    }

    public static SheetView FramingPlan(ViewContext ctx, int firstSupport, int lastSupport)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        var s0 = f.Supports[firstSupport]; var s1 = f.Supports[lastSupport];
        bool atBegin = firstSupport == 0, atEnd = lastSupport == f.Supports.Count - 1;
        double margin = d.Paper(0.15);
        double x0 = s0.Station - (atBegin ? 25 * u.LongUnit : margin), x1 = s1.Station + (atEnd ? 25 * u.LongUnit : margin);
        Vec2 Dev(Vec3 p) { var so = a.Inverse(p.XY); return new Vec2(so.Station, -so.Offset); }
        Vec2 DevS(double sta, double off) => new(sta, -off);
        bool In(double sta) => sta >= x0 - 1e-6 && sta <= x1 + 1e-6;

        // Girders: web centerline, labels at both ends of the drawn part.
        foreach (var g in f.Girders)
        {
            var pts = g.WebLine.Select(Dev).ToList();
            ClipPoly(d, SheetLayers.Steel, pts, x0, x1);
            var inside = pts.Where(p => p.X >= x0 && p.X <= x1).ToList();
            if (inside.Count == 0) continue;
            d.Text(g.Name, inside[0] + new Vec2(-d.Paper(0.1), 0), d.SmallTextH, 0, TextAlign.Right);
            d.Text(g.Name, inside[^1] + new Vec2(d.Paper(0.1), 0), d.SmallTextH, 0, TextAlign.Left);
        }

        // Support lines and bearings.
        foreach (var sl in f.Supports.Concat(f.AuxSupports))
        {
            if (!In(sl.Station)) continue;
            var br = sl.Bearings.OfType<BearingResult>().ToList();
            if (br.Count == 0) continue;
            var pts = br.Select(b => DevS(b.Station, b.Offset)).OrderBy(p => p.Y).ToList();
            var dir = (pts[^1] - pts[0]).Normalized();
            var pa = pts[0] - dir * d.Paper(0.35); var pb = pts[^1] + dir * d.Paper(0.35);
            d.Line(SheetLayers.Center, pa, pb);
            d.TextAlong($"CL BRG {sl.Name.ToUpperInvariant()}", pb + dir * d.Paper(0.05), dir, d.SmallTextH);
            foreach (var b in br)
            {
                var p = DevS(b.Station, b.Offset);
                double h = d.Paper(0.05);
                d.Poly(SheetLayers.Bearing, new[] { p, p + new Vec2(-h * 0.7, -h), p + new Vec2(h * 0.7, -h) }, true);
            }
            // Girder spacing along the support line.
            string skew = sl.SkewDegrees != 0 ? $"\nSKEW {UnitSystem.FormatAngleDms(Math.Abs(sl.SkewDegrees) * Math.PI / 180, false)} {(sl.SkewDegrees > 0 ? "RT" : "LT")}" : "";
            d.Text($"STA {u.FormatStation(sl.Station)}{skew}", pa + new Vec2(0, -d.Paper(0.5)), d.SmallTextH, 0, TextAlign.Center);
        }

        // Cross frames (support frames heavier) with marks.
        foreach (var cf in f.CrossFrames)
        {
            var l = Dev(cf.Left); var r = Dev(cf.Right);
            var mid = (l + r) / 2;
            if (!In(mid.X)) continue;
            d.Line(cf.AtSupport ? SheetLayers.Steel : SheetLayers.SteelHidden, l, r);
            var dir = (r - l).Normalized();
            Mark(d, cf.Mark, mid + dir.Left * d.Paper(0.045), dir);
        }
        foreach (var xg in f.CrossGirders)
        {
            var l = Dev(xg.Left); var r = Dev(xg.Right);
            if (!In(((l + r) / 2).X)) continue;
            d.Line(SheetLayers.Steel, l, r);
            var dir = (r - l).Normalized();
            Mark(d, xg.Mark, (l + r) / 2 + dir.Left * d.Paper(0.045), dir);
        }

        // Field splices.
        foreach (var sp in f.Splices)
        {
            var p = Dev(sp.Location);
            if (!In(p.X)) continue;
            var n = new Vec2(-sp.GirderTangent.Y, sp.GirderTangent.X);
            double t = d.Paper(0.07);
            d.Line(SheetLayers.Steel, p - new Vec2(0, t), p + new Vec2(0, t));
            d.Line(SheetLayers.Steel, p - new Vec2(0, t) + new Vec2(d.Paper(0.02), 0), p + new Vec2(0, t) + new Vec2(d.Paper(0.02), 0));
            d.Text($"FS{sp.Number}", p + new Vec2(0, -t - d.Paper(0.06)), d.SmallTextH * 0.8, Math.PI / 2, TextAlign.Right);
        }

        // Station ticks along the CL and span dimensions above the left edge.
        double unit = u.LongUnit;
        double minor = (u.IsUS ? 50 : 20) * unit, major = 100 * unit;
        d.Line(SheetLayers.Center, DevS(x0, 0), DevS(x1, 0));
        for (double sta = Math.Ceiling(x0 / minor) * minor; sta <= x1; sta += minor)
        {
            double len = d.Paper(Math.Abs(Math.IEEERemainder(sta, major)) < 1e-6 ? 0.1 : 0.05);
            d.Line(SheetLayers.Center, DevS(sta, -len), DevS(sta, len));
            if (Math.Abs(Math.IEEERemainder(sta, major)) < 1e-6)
                d.Text(u.FormatStation(sta, 0), DevS(sta, len + d.TextH), d.SmallTextH, Math.PI / 2, TextAlign.Center);
        }
        d.Text($"CL {a.Name}", DevS(x0 + d.Paper(0.2), 0) + new Vec2(0, d.Paper(0.08)), d.SmallTextH, 0, TextAlign.Left);

        var box = dwg.Extents();
        double yTop = box.MaxY + d.Paper(0.5);
        for (int i = firstSupport + 1; i <= lastSupport; i++)
            d.Dim(new Vec2(f.Supports[i - 1].Station, yTop), new Vec2(f.Supports[i].Station, yTop), 0, $"SPAN {i} = {u.FormatLength(f.Supports[i].Station - f.Supports[i - 1].Station)}");
        d.Text("CF = CROSS FRAME (SOLID = AT SUPPORT, DASHED = INTERMEDIATE)   FS = FIELD SPLICE\nGIRDER SPACINGS: SEE THE GIRDER SPACING AT SUPPORTS SHEET",
            new Vec2((x0 + x1) / 2, yTop + d.Paper(0.65)), d.SmallTextH, 0, TextAlign.Center);
        box = dwg.Extents();
        string spans = firstSupport + 1 == lastSupport ? $"SPAN {lastSupport}" : $"SPANS {firstSupport + 1}-{lastSupport}";
        d.Title($"STEEL FRAMING PLAN — {spans}", $"Developed along the CL alignment · Scale {DrawingOptions.ScaleText(d.Scale, u)}", new Vec2((x0 + x1) / 2, box.MinY - d.Paper(0.5)));
        return new SheetView($"Framing plan {spans.ToLowerInvariant()}", $"Framing {firstSupport + 1}-{lastSupport}", dwg, d.Scale, "Steel framing plan: girders, cross frames with marks, field splices, bearings.");
    }

    /// <summary>Distance between bearing i-1 and i on the support line (bearings ordered by plan Y = minus offset).</summary>
    private static double SpaceAlong(Alignments.Alignment a, IReadOnlyList<BearingResult> br, int i)
    {
        var ordered = br.OrderBy(b => -b.Offset).ToList();   // left (negative offset) first, as plotted from the top down... matches Y = -offset ascending
        var pts = ordered.Select(b => a.PointAt(b.Station, b.Offset)).ToList();
        return Vec2.Distance(pts[i - 1], pts[i]);
    }
}
