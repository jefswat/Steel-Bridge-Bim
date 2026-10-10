using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>TxDOT prestressed girder sheet views: sections with the strand grid, a typical elevation, the girder schedule.</summary>
public static class PrestressedViews
{
    /// <summary>Girder sections at the CL and at the ends on the IGND 2" grid (open = available, filled = used).</summary>
    public static SheetView Sections(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var ps = ctx.Prestressed!;
        var shape = ps.Shape;
        var beam = Representative(ps);
        double gap = shape.TopWidth * 2.4;
        string S(double m) => u.FormatLength(m, LengthKind.Short);

        for (int k = 0; k < 2; k++)
        {
            bool atEnd = k == 1;
            double x0 = k * gap;
            var outline = shape.Outline().Select(q => new Vec2(x0 + q.X, q.Y)).ToList();
            d.Poly(SheetLayers.ConcreteCut, outline, true);
            d.Line(SheetLayers.Center, new Vec2(x0, -d.Paper(0.25)), new Vec2(x0, shape.Depth + d.Paper(0.25)));
            double rr = Math.Max(d.Paper(0.025), ps.Definition.StrandDiameter / 2);
            var used = beam?.Strands.Select(s => new Vec2(s.Section.X, atEnd ? s.EndHeight : s.Section.Y)).ToList() ?? new();
            foreach (var p in shape.StrandPositions())
                if (!used.Any(q => (q - p).Length < 1e-4)) d.Circle(SheetLayers.Grid, new Vec2(x0 + p.X, p.Y), rr * 0.8);
            foreach (var q in used) d.Circle(SheetLayers.Rebar, new Vec2(x0 + q.X, q.Y), rr, filled: true);

            // Dimensions: depth, flange widths, web, first row and grid.
            double ht = shape.TopWidth / 2, hb = shape.BottomWidth / 2;
            d.Dim(new Vec2(x0 + ht, 0), new Vec2(x0 + ht, shape.Depth), -0.35, $"{S(shape.Depth)} \"D\"");
            d.Dim(new Vec2(x0 - ht, shape.Depth), new Vec2(x0 + ht, shape.Depth), 0.25, S(shape.TopWidth));
            d.Dim(new Vec2(x0 + hb, 0), new Vec2(x0 - hb, 0), 0.3, S(shape.BottomWidth));
            double yWeb = shape.Depth * 0.55;
            d.Dim(new Vec2(x0 - shape.WebWidth / 2, yWeb), new Vec2(x0 + shape.WebWidth / 2, yWeb), 0.15, S(shape.WebWidth));
            d.Dim(new Vec2(x0 - hb, 0), new Vec2(x0 - hb, In(2.5)), 0.3, S(In(2.5)));
            double cg = used.Count > 0 ? used.Average(q => q.Y) : 0;
            if (used.Count > 0)
            {
                d.Line(SheetLayers.Center, new Vec2(x0 - hb - d.Paper(0.3), cg), new Vec2(x0 + hb + d.Paper(0.3), cg));
                d.Text($"C.G. STRANDS {S(cg)}\ne = {S(In(shape.Yb) - cg)}", new Vec2(x0 + hb + d.Paper(0.35), cg), d.SmallTextH, 0, TextAlign.Left);
            }
            d.Line(SheetLayers.Center, new Vec2(x0 - ht - d.Paper(0.2), In(shape.Yb)), new Vec2(x0 - shape.WebWidth, In(shape.Yb)));
            d.Text($"C.G. GIRDER\nYb = {S(In(shape.Yb))}", new Vec2(x0 - ht - d.Paper(0.25), In(shape.Yb)), d.SmallTextH, 0, TextAlign.Right);
            int nDep = used.Count(q => Math.Abs(q.X) < In(1.5) && beam!.Strands.Any(s => s.Depressed && Math.Abs(s.Section.X - q.X) < 1e-6));
            d.Title(atEnd ? "AT GIRDER ENDS" : "AT CL GIRDER",
                $"{used.Count} - {TxGirderCatalog.StrandSize(ps.Definition.StrandDiameter, u)} strands" + (atEnd && beam!.Strands.Any(s => s.Depressed) ? $", {beam.Strands.Count(s => s.Depressed)} depressed" : ""),
                new Vec2(x0, -d.Paper(0.75)));
        }
        d.Text($"TxDOT {shape.Name}: A = {shape.Area:N0} in², Ix = {shape.Ix:N0} in^4, Yt = {shape.Yt:0.00}\", Yb = {shape.Yb:0.00}\", {shape.WeightPlf:N0} plf (IGD)\n" +
               "Strand grid per IGND: rows at 2½\", 4½\", … ; positions A–G at 2\" each side of CL. Open circles = unused positions.",
            new Vec2(gap / 2, -d.Paper(1.45)), d.SmallTextH, 0, TextAlign.Center);
        return new SheetView("Girder sections", "PS Sections", dwg, d.Scale, "Tx girder sections at the CL and the ends with the strand pattern.");
    }

    /// <summary>Elevation of the longest beam (developed along its CL, true depth) with bearings, ends, hold-down points and strand profile.</summary>
    public static SheetView Elevation(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        var ps = ctx.Prestressed!;
        var pd = ps.Definition;
        var b = Representative(ps);
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        if (b == null) return new SheetView("Girder elevation", "PS Elevation", dwg, d.Scale, "");
        // Depth is exaggerated so the strand profile reads at an engineering scale.
        const double vx = 5;
        double L = b.Length, D = b.Shape.Depth * vx;
        double Y(double yAboveBottom) => yAboveBottom * vx - D;
        d.Rect(SheetLayers.Concrete, 0, -D, L, 0);
        double xb0 = pd.BearingToEnd, xb1 = L - pd.BearingToEnd;
        foreach (double xb in new[] { xb0, xb1 })
        {
            d.Line(SheetLayers.Center, new Vec2(xb, -D - d.Paper(0.35)), new Vec2(xb, d.Paper(0.25)));
            d.Poly(SheetLayers.Bearing, new[] { new Vec2(xb - d.Paper(0.06), -D - d.Paper(0.08)), new Vec2(xb + d.Paper(0.06), -D - d.Paper(0.08)), new Vec2(xb, -D) }, true);
        }
        bool abutA = ctx.Framing.Definition.Supports[b.Span].Type == SupportType.Abutment;
        bool abutB = ctx.Framing.Definition.Supports[b.Span + 1].Type == SupportType.Abutment;
        var straight = b.Strands.Where(s => !s.Depressed).ToList();
        var dep = b.Strands.Where(s => s.Depressed).ToList();
        if (straight.Count > 0)
        {
            double y = Y(straight.Average(s => s.Section.Y));
            d.Line(SheetLayers.Rebar, new Vec2(0, y), new Vec2(L, y));
            d.Leader(new Vec2(L * 0.7, y), new Vec2(L * 0.7 + d.Paper(0.25), -D - d.Paper(0.45)), $"C.G. OF {straight.Count} STRAIGHT STRANDS");
        }
        if (dep.Count > 0)
        {
            double ye = Y(dep.Average(s => s.EndHeight)), ym = Y(dep.Average(s => s.Section.Y));
            double h0 = b.HoldDownFromEnd, h1 = L - b.HoldDownFromEnd;
            d.Poly(SheetLayers.Rebar, new[] { new Vec2(0, ye), new Vec2(h0, ym), new Vec2(h1, ym), new Vec2(L, ye) });
            foreach (double h in new[] { h0, h1 })
                d.Line(SheetLayers.Center, new Vec2(h, -D - d.Paper(0.15)), new Vec2(h, d.Paper(0.15)));
            d.Dim(new Vec2(h0, 0), new Vec2(h1, 0), 0.3, $"{u.FormatLength(h1 - h0)} BETWEEN HOLD-DOWNS");
            d.Text("HOLD-DOWNS L/20 EACH SIDE OF CL (5'-0\" MIN)", new Vec2(L / 2, d.Paper(0.68)), d.SmallTextH, 0, TextAlign.Center);
            var at = new Vec2(h0 * 0.45, ye + (ym - ye) * 0.45);
            d.Leader(at, new Vec2(at.X + d.Paper(0.3), -D - d.Paper(0.45)), $"C.G. OF {dep.Count} DEPRESSED STRANDS");
        }
        d.Dim(new Vec2(0, -D), new Vec2(L, -D), -0.85, $"{u.FormatLength(L)} GIRDER LENGTH (END TO END)");
        d.Dim(new Vec2(xb0, -D), new Vec2(xb1, -D), -1.2, $"{u.FormatLength(xb1 - xb0)} CL BRG TO CL BRG");
        string EndNote(bool abut) => abut ? $"END OF GIRDER, {S(pd.BearingToEnd)} TO CL BRG\n(NORMAL TO BACKWALL, PLUMB)"
            : $"END OF GIRDER, {S(pd.EndGapAtBent)} FROM CL BENT\n{S(pd.BearingToEnd)} TO CL BRG (ALONG CL GIRDER)";
        d.Leader(new Vec2(0, -D * 0.15), new Vec2(d.Paper(0.45), d.Paper(1.05)), EndNote(abutA));
        d.Leader(new Vec2(L, -D * 0.15), new Vec2(L - d.Paper(0.45), d.Paper(1.05)), EndNote(abutB));
        d.Title($"GIRDER {b.Mark} ELEVATION", $"{b.Shape.Name} · along the CL girder · Scale {DrawingOptions.ScaleText(d.Scale, u)} (depth exaggerated {vx:0}x)",
            new Vec2(L / 2, -D - d.Paper(1.85)));
        return new SheetView("Girder elevation", "PS Elevation", dwg, d.Scale, "Typical prestressed girder elevation with strand profile.");
    }

    /// <summary>Girder schedule (paper space): identical beams grouped by length, skews and strands.</summary>
    public static SheetView Schedule(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1);
        var ps = ctx.Prestressed!;
        var pd = ps.Definition;
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        string W(double kg) => u.IsUS ? $"{kg * 2.20462 / 2000:0.0} T" : $"{kg / 1000:0.0} t";
        var rows = ps.Beams
            .GroupBy(b => (Math.Round(b.Length / In(0.125)), Math.Round(b.SkewStart, 3), Math.Round(b.SkewEnd, 3), b.Strands.Count))
            .Select(g => g.ToList()).ToList();
        var cols = new (string h, double w)[]
        {
            ("GIRDERS", 2.3), ("TYPE", 0.6), ("NO.", 0.45), ("LENGTH", 1.15), ("CL BRG-CL BRG", 1.25), ("SKEW BEG / END", 1.25), ("STRANDS", 1.25),
            ("DEPR.", 0.55), ("\"e\" CL / END", 1.25), ("HAUNCH MIN / MAX", 1.45), ("WEIGHT EA.", 0.9),
        };
        double rowH = d.Paper(0.2), tw = d.Paper(cols.Sum(c => c.w));
        d.Text("PRESTRESSED GIRDER SCHEDULE", new Vec2(0, d.Paper(0.3)), d.Paper(0.14), 0, TextAlign.Left, SheetLayers.Title);
        int n = rows.Count + 1;
        d.Rect(SheetLayers.Text, 0, -rowH * n, tw, 0);
        double x = 0;
        foreach (var (h, w) in cols)
        {
            d.Text(h, new Vec2(x + d.Paper(0.05), -rowH / 2), d.Paper(0.08), 0, TextAlign.Left);
            x += d.Paper(w);
            d.Line(SheetLayers.Text, new Vec2(x, 0), new Vec2(x, -rowH * n));
        }
        double yb = In(ps.Shape.Yb);
        for (int i = 0; i < rows.Count; i++)
        {
            var g = rows[i];
            var b = g[0];
            double ry = -rowH * (i + 1);
            d.Line(SheetLayers.Text, new Vec2(0, ry), new Vec2(tw, ry));
            double eMid = yb - b.Strands.Average(s => s.Section.Y), eEnd = yb - b.Strands.Average(s => s.EndHeight);
            string marks = string.Join(", ", g.Select(x => x.Mark));
            if (marks.Length > 34) marks = $"{g[0].Mark} … {g[^1].Mark}";
            var vals = new[]
            {
                marks, b.Shape.Name, g.Count.ToString(), u.FormatLength(b.Length), u.FormatLength(b.BearingSpan),
                $"{b.SkewStart:0.##}° / {b.SkewEnd:0.##}°", $"{b.Strands.Count} - {TxGirderCatalog.StrandSize(pd.StrandDiameter, u)}", b.Strands.Count(s => s.Depressed).ToString(),
                $"{S(eMid)} / {S(eEnd)}", $"{S(g.Min(x => x.MinHaunch))} / {S(g.Max(x => x.MaxHaunch))}", W(b.Mass),
            };
            x = 0;
            for (int k = 0; k < cols.Length; k++)
            {
                bool warn = k == 9 && g.Min(x => x.MinHaunch) < In(0.5);
                d.Text(vals[k], new Vec2(x + d.Paper(0.05), ry - rowH / 2), d.Paper(0.08), 0, TextAlign.Left, warn ? SheetLayers.Rebar : SheetLayers.Text);
                x += d.Paper(cols[k].w);
            }
        }
        var notes = new[]
        {
            $"Girders per TxDOT standard IGD (I-girder details) and IGND (strand grid). Class H concrete, f'c = {pd.ConcreteStrength:0.0#} ksi, f'ci = {pd.ReleaseStrength:0.0#} ksi.",
            $"{TxGirderCatalog.StrandSize(pd.StrandDiameter, u)} dia., {pd.StrandStrength:0} ksi low-relaxation strands. Depressed strands from the A positions, 2\" spacing at the ends.",
            $"Interior bents: end of girder {S(pd.EndGapAtBent)} from CL bent, CL bearing {S(pd.BearingToEnd)} from the end (along CL girder). Abutments: station = CL bearing.",
            $"Elastomeric pads {S(pd.PadLength)} x {S(pd.PadWidth)} x {S(pd.PadThickness)} (verify with TxDOT IGEB). Haunch set at the supports; camber is not modeled.",
            "Geometry only — strand counts and strengths are inputs, not a design.",
        };
        for (int i = 0; i < notes.Length; i++)
            d.Text(notes[i], new Vec2(0, -rowH * n - d.Paper(0.25 + 0.17 * i)), d.Paper(0.08), 0, TextAlign.Left);
        return new SheetView("Girder schedule", "PS Schedule", dwg, 1, "", PaperSpace: true);
    }

    /// <summary>The longest beam with depressed strands (else the longest).</summary>
    private static PrestressedBeam? Representative(PrestressedModel ps) =>
        ps.Beams.OrderByDescending(b => b.Strands.Any(s => s.Depressed)).ThenByDescending(b => b.Length).FirstOrDefault();
}
