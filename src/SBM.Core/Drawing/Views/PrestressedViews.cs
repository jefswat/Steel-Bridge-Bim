using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Prestressed beam sheet views: beam sections with strands (and beam bars when the model has them), beam elevations with the strand
/// profile, deflection and debonding, the beam data / debonding / bar tables per beam type, the girder schedule and notes, the beam
/// layout (framing plan), steel intermediate diaphragms and concrete diaphragm sections. Every number comes from the generated model.
/// </summary>
public static class PrestressedViews
{
    /// <summary>Paper table in a paper-space view (inches): header row + rows; returns the bottom y (paper inches, negative down).</summary>
    public static double Table(Drafter d, double x, double y, IReadOnlyList<(string h, double w)> cols, IReadOnlyList<string[]> rows, string? title = null, double rowH = 0.19)
    {
        double th = 0.075;
        if (title != null)
        {
            d.Text(title, new Vec2(d.Paper(x), d.Paper(y + 0.22)), d.Paper(0.11), 0, TextAlign.Left, SheetLayers.Title);
        }
        double tw = cols.Sum(c => c.w);
        int n = rows.Count + 1;
        d.Rect(SheetLayers.Text, d.Paper(x), d.Paper(y - rowH * n), d.Paper(x + tw), d.Paper(y));
        double cx = x;
        foreach (var (h, w) in cols)
        {
            d.Text(h, new Vec2(d.Paper(cx + 0.05), d.Paper(y - rowH / 2)), d.Paper(th), 0, TextAlign.Left);
            cx += w;
            d.Line(SheetLayers.Text, new Vec2(d.Paper(cx), d.Paper(y)), new Vec2(d.Paper(cx), d.Paper(y - rowH * n)));
        }
        for (int i = 0; i < rows.Count; i++)
        {
            double ry = y - rowH * (i + 1);
            d.Line(SheetLayers.Text, new Vec2(d.Paper(x), d.Paper(ry)), new Vec2(d.Paper(x + tw), d.Paper(ry)));
            cx = x;
            for (int k = 0; k < cols.Count; k++)
            {
                string v = k < rows[i].Length ? rows[i][k] : "";
                d.Text(Fit(v, cols[k].w, th), new Vec2(d.Paper(cx + 0.05), d.Paper(ry - rowH / 2)), d.Paper(th), 0, TextAlign.Left);
                cx += cols[k].w;
            }
        }
        return y - rowH * n;
    }

    /// <summary>Word wrap to a width in paper inches at a text height (Helvetica estimate).</summary>
    public static List<string> Wrap(string text, double widthIn, double th)
    {
        double max = widthIn / th;
        var lines = new List<string>();
        string cur = "";
        foreach (var w in text.Split(' '))
        {
            string t = cur == "" ? w : cur + " " + w;
            if (cur != "" && LabelPlacer.WidthInHeights(t) > max) { lines.Add(cur); cur = w; }
            else cur = t;
        }
        if (cur != "") lines.Add(cur);
        return lines;
    }

    /// <summary>Text cut to a column width (Helvetica estimate).</summary>
    private static string Fit(string s, double widthIn, double th)
    {
        double max = (widthIn - 0.08) / th;
        if (LabelPlacer.WidthInHeights(s) <= max) return s;
        while (s.Length > 1 && LabelPlacer.WidthInHeights(s + "...") > max) s = s[..^1];
        return s + "...";
    }

    // ------------------------------------------------------------------ beam frame

    /// <summary>Beam coordinates: x along the beam (plan) from the start end, lateral (right of the CL), height above the beam bottom.</summary>
    private readonly record struct BeamFrame(PrestressedBeam B)
    {
        Vec2 T => (B.TopEnd.XY - B.TopStart.XY).Normalized();
        double Lp => (B.TopEnd.XY - B.TopStart.XY).Length;
        public double X(Vec3 p) => Vec2.Dot(p.XY - B.TopStart.XY, T) * (B.Length / Math.Max(1e-9, Lp));
        public double Right(Vec3 p) => -Vec2.Dot(p.XY - B.TopStart.XY, T.Left);
        public double Up(Vec3 p)
        {
            double t = Math.Clamp(Vec2.Dot(p.XY - B.TopStart.XY, T) / Math.Max(1e-9, Lp), -1, 2);
            double zTop = B.TopStart.Z + (B.TopEnd.Z - B.TopStart.Z) * t;
            return p.Z - (zTop - B.Shape.Depth);
        }
    }

    /// <summary>A representative beam of a type: the longest with deflected or debonded strands, else the longest.</summary>
    public static PrestressedBeam? Representative(IEnumerable<PrestressedBeam> beams) =>
        beams.OrderByDescending(b => b.Strands.Any(s => s.Depressed || s.Debonded)).ThenByDescending(b => b.Length).ThenBy(b => b.Girder).FirstOrDefault();

    private static PrestressedBeam? RepOf(ViewContext ctx, string? type) =>
        Representative(ctx.Prestressed!.Beams.Where(b => type == null || b.TypeName == type));

    // ------------------------------------------------------------------ sections

    /// <summary>Beam sections at midspan and at the end with the strands (open grid positions for Tx shapes), debonded strands ringed,
    /// and the beam bars cut at those sections when the model has beam reinforcing.</summary>
    public static SheetView Sections(ViewContext ctx, string? type = null)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var ps = ctx.Prestressed!;
        var beam = RepOf(ctx, type);
        var shape = beam?.Shape ?? ps.Shape;
        var def = beam?.Definition ?? ps.Definition;
        double gap = shape.TopWidth * 2.4;
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        var bars = beam != null ? PsBars.Bars(beam) : Array.Empty<BarPlacement>();
        var fr = beam != null ? new BeamFrame(beam) : default;

        for (int k = 0; k < 2; k++)
        {
            bool atEnd = k == 1;
            double x0 = k * gap;
            var outline = shape.Outline().Select(q => new Vec2(x0 + q.X, q.Y)).ToList();
            d.Poly(SheetLayers.ConcreteCut, outline, true);
            d.Line(SheetLayers.Center, new Vec2(x0, -d.Paper(0.25)), new Vec2(x0, shape.Depth + d.Paper(0.25)));
            double rr = Math.Max(d.Paper(0.025), def.StrandDiameter / 2);
            var strands = beam?.Strands ?? new List<StrandInstance>();
            var used = strands.Select(s => new Vec2(s.Section.X, atEnd ? s.EndHeight : s.Section.Y)).ToList();
            if (shape.IsTx)
                foreach (var p in shape.StrandPositions())
                    if (!used.Any(q => (q - p).Length < 1e-4)) d.Circle(SheetLayers.Grid, new Vec2(x0 + p.X, p.Y), rr * 0.8);
            for (int i = 0; i < strands.Count; i++)
            {
                var q = used[i];
                d.Circle(SheetLayers.Rebar, new Vec2(x0 + q.X, q.Y), rr, filled: true);
                if (atEnd && strands[i].Debonded) d.Circle(SheetLayers.Rebar, new Vec2(x0 + q.X, q.Y), rr * 1.9);
            }
            // Beam bars at this section (generic: from the placed bars of the representative beam).
            if (bars.Count > 0)
            {
                double xs = atEnd ? Math.Min(beam!.Length / 2, def.BearingToEnd + In(12)) : beam!.Length / 2;
                DrawBarsAtSection(d, fr, bars, xs, x0, shape.Depth);
            }

            double ht = shape.TopWidth / 2, hb = shape.BottomWidth / 2;
            d.Dim(new Vec2(x0 + ht, 0), new Vec2(x0 + ht, shape.Depth), -0.35, $"{S(shape.Depth)}");
            d.Dim(new Vec2(x0 - ht, shape.Depth), new Vec2(x0 + ht, shape.Depth), 0.25, S(shape.TopWidth));
            d.Dim(new Vec2(x0 + hb, 0), new Vec2(x0 - hb, 0), 0.3, S(shape.BottomWidth));
            double yWeb = shape.Depth * 0.55;
            d.Dim(new Vec2(x0 - shape.WebWidth / 2, yWeb), new Vec2(x0 + shape.WebWidth / 2, yWeb), 0.15, S(shape.WebWidth));
            if (used.Count > 0)
            {
                double y1 = used.Min(q => q.Y);
                d.Dim(new Vec2(x0 - hb, 0), new Vec2(x0 - hb, y1), 0.3, S(y1));
                double cg = used.Average(q => q.Y);
                d.Line(SheetLayers.Center, new Vec2(x0 - hb - d.Paper(0.3), cg), new Vec2(x0 + hb + d.Paper(0.3), cg));
                d.Text($"C.G. STRANDS {S(cg)}\ne = {S(In(shape.Yb) - cg)}", new Vec2(x0 + hb + d.Paper(0.35), cg - d.Paper(0.12)), d.SmallTextH, 0, TextAlign.Left);
            }
            d.Line(SheetLayers.Center, new Vec2(x0 - ht - d.Paper(0.2), In(shape.Yb)), new Vec2(x0 - shape.WebWidth, In(shape.Yb)));
            d.Text($"C.G. BEAM\nYb = {S(In(shape.Yb))}", new Vec2(x0 - ht - d.Paper(0.25), In(shape.Yb)), d.SmallTextH, 0, TextAlign.Right);
            int nDefl = strands.Count(s => s.Depressed), nDeb = strands.Count(s => s.Debonded);
            string word = shape.IsTx ? "depressed" : "deflected";
            string sub = $"{used.Count} - {PsText.Strand(def, u)} strands" + (nDefl > 0 ? $", {nDefl} {word}" : "") + (atEnd && nDeb > 0 ? $", {nDeb} debonded (ringed)" : "");
            d.Title(atEnd ? "NEAR BEAM END" : "AT MIDSPAN", sub, new Vec2(x0, -d.Paper(0.75)));
        }
        double area = shape.PrintedArea ?? shape.Area, ix = shape.PrintedIx ?? shape.Ix, yb = shape.PrintedYb ?? shape.Yb;
        string printed = shape.PrintedArea != null ? " (printed values)" : "";
        string grid = shape.IsTx ? "\nStrand grid per IGND: rows at 2 1/2\", 4 1/2\", ... ; open circles = unused positions." : $"\nSection: {shape.Source}";
        d.Text($"{(type != null ? PsText.TypeName(shape, type) : PsText.ShapeName(shape))}: A = {area:N1} in², Ix = {ix:N0} in^4, Yb = {yb:0.00}\"{printed}, {shape.WeightPlf:N0} plf" + grid,
            new Vec2(gap / 2, -d.Paper(1.45)), d.SmallTextH, 0, TextAlign.Center);
        return new SheetView("Beam sections", "PS Sections" + (type != null ? " " + type : ""), dwg, d.Scale, "Prestressed beam sections at midspan and near the end with the strand pattern.");
    }

    /// <summary>Bars cut by the section at <paramref name="xs"/> along the beam: transverse bars (the one nearest the section per mark) as
    /// their projection, longitudinal bars crossing the section as dots.</summary>
    private static void DrawBarsAtSection(Drafter d, BeamFrame fr, IReadOnlyList<BarPlacement> bars, double xs, double x0, double depth)
    {
        double dot = d.Paper(0.018);
        var trans = new Dictionary<string, (double dist, BarPlacement bar)>();
        foreach (var b in bars)
        {
            var xsAlong = b.Points.Select(fr.X).ToList();
            double lo = xsAlong.Min(), hi = xsAlong.Max();
            if (hi - lo < depth * 0.6)
            {
                double dist = Math.Abs((lo + hi) / 2 - xs);
                if (!trans.TryGetValue(b.Mark, out var cur) || dist < cur.dist) trans[b.Mark] = (dist, b);
            }
            else if (lo <= xs && hi >= xs)
                for (int i = 0; i + 1 < b.Points.Length; i++)
                {
                    double a = fr.X(b.Points[i]), c = fr.X(b.Points[i + 1]);
                    if ((a - xs) * (c - xs) > 0 || Math.Abs(c - a) < 1e-9) continue;
                    var p = b.Points[i] + (b.Points[i + 1] - b.Points[i]) * ((xs - a) / (c - a));
                    d.Circle(SheetLayers.Rebar, new Vec2(x0 + fr.Right(p), fr.Up(p)), dot, filled: true);
                    break;
                }
        }
        foreach (var (_, b) in trans.Values)
            if (b.Points.Length > 1) d.Poly(SheetLayers.Rebar, b.Points.Select(p => new Vec2(x0 + fr.Right(p), fr.Up(p))));
    }

    // ------------------------------------------------------------------ elevation

    /// <summary>Elevation of a beam (developed along its CL, depth exaggerated) with bearings, ends, hold-down points, the strand profile,
    /// the debonded lengths and the beam bars (when modeled).</summary>
    public static SheetView Elevation(ViewContext ctx, string? type = null)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        var ps = ctx.Prestressed!;
        var b = RepOf(ctx, type);
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        if (b == null) return new SheetView("Beam elevation", "PS Elevation", dwg, d.Scale, "");
        var pd = b.Definition ?? ps.Definition;
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
        // Beam bars (projection on the beam's vertical plane).
        var fr = new BeamFrame(b);
        foreach (var bar in PsBars.Bars(b))
            if (bar.Points.Length > 1) d.Poly(SheetLayers.Rebar, bar.Points.Select(p => new Vec2(fr.X(p), Y(fr.Up(p)))));

        var straight = b.Strands.Where(s => !s.Depressed).ToList();
        var dep = b.Strands.Where(s => s.Depressed).ToList();
        string word = b.Shape.IsTx ? "DEPRESSED" : "DEFLECTED";
        if (straight.Count > 0)
        {
            double y = Y(straight.Average(s => s.Section.Y));
            d.Line(SheetLayers.Rebar, new Vec2(0, y), new Vec2(L, y));
            d.Leader(new Vec2(L * 0.5, y), new Vec2(L * 0.5 + d.Paper(0.25), d.Paper(0.3)), $"C.G. OF {straight.Count} STRAIGHT STRANDS");
        }
        if (dep.Count > 0)
        {
            double ye = Y(dep.Average(s => s.EndHeight)), ym = Y(dep.Average(s => s.Section.Y));
            double h0 = b.HoldDownFromEnd, h1 = L - b.HoldDownFromEnd;
            d.Poly(SheetLayers.Rebar, new[] { new Vec2(0, ye), new Vec2(h0, ym), new Vec2(h1, ym), new Vec2(L, ye) });
            foreach (double h in new[] { h0, h1 })
                d.Line(SheetLayers.Center, new Vec2(h, -D - d.Paper(0.15)), new Vec2(h, d.Paper(0.15)));
            d.Dim(new Vec2(h0, 0), new Vec2(h1, 0), 0.3, $"{u.FormatLength(h1 - h0)} BETWEEN HOLD-DOWNS");
            d.Text($"HOLD-DOWN POINTS {u.FormatLength((h1 - h0) / 2)} EACH SIDE OF MIDSPAN ({u.FormatLength(h0)} FROM THE ENDS)", new Vec2(L / 2, d.Paper(0.68)), d.SmallTextH, 0, TextAlign.Center);
            var at = new Vec2(h0 * 0.45, ye + (ym - ye) * 0.45);
            d.Leader(at, new Vec2(at.X + d.Paper(0.3), -D - d.Paper(0.45)), $"C.G. OF {dep.Count} {word} STRANDS");
        }
        // Debonded lengths: one bar per group and end at the group's height; labels stacked below the beam at each end.
        var groups = DebondGroups(b);
        int row = 0;
        foreach (bool atStart in new[] { true, false })
        {
            var gs = groups.Where(g => g.AtStart == atStart).ToList();
            if (gs.Count == 0) continue;
            double maxLen = gs.Max(g => g.Length);
            int r = 0;
            foreach (var (_, len, n, y, sym) in gs)
            {
                double x0 = atStart ? 0 : L - len, x1 = atStart ? len : L;
                double yy = Y(y) + d.Paper(0.02) * r;
                d.Line(SheetLayers.Rebar, new Vec2(x0, yy), new Vec2(x1, yy));
                d.Line(SheetLayers.Rebar, new Vec2(x0, yy + d.Paper(0.012)), new Vec2(x1, yy + d.Paper(0.012)));
                var tip = new Vec2(atStart ? x1 : x0, yy);
                double ex = atStart ? maxLen + d.Paper(0.4) : L - maxLen - d.Paper(0.4);
                d.Leader(tip, new Vec2(ex, -D - d.Paper(0.3 + 0.2 * r)), $"{n} STRANDS{(sym != "" ? " " + sym : "")} DEBONDED {u.FormatLength(len)}");
                r++;
            }
            row = Math.Max(row, r);
        }
        d.Dim(new Vec2(0, -D), new Vec2(L, -D), -0.85 - 0.22 * row, $"{u.FormatLength(L)} BEAM LENGTH (END TO END)");
        d.Dim(new Vec2(xb0, -D), new Vec2(xb1, -D), -1.2 - 0.22 * row, $"{u.FormatLength(xb1 - xb0)} CL BRG TO CL BRG");
        bool abutA = ctx.Framing.Definition.Supports[b.Span].Type == SupportType.Abutment;
        bool abutB = ctx.Framing.Definition.Supports[b.Span + 1].Type == SupportType.Abutment;
        string EndNote(bool abut) => !b.Shape.IsTx ? $"END OF BEAM, {S(pd.BearingToEnd)} TO CL BRG" : abut ? $"END OF GIRDER, {S(pd.BearingToEnd)} TO CL BRG\n(NORMAL TO BACKWALL, PLUMB)"
            : $"END OF GIRDER, {S(pd.EndGapAtBent)} FROM CL BENT\n{S(pd.BearingToEnd)} TO CL BRG (ALONG CL GIRDER)";
        d.Leader(new Vec2(0, -D * 0.15), new Vec2(-d.Paper(0.35), d.Paper(0.75)), EndNote(abutA));
        d.Leader(new Vec2(L, -D * 0.15), new Vec2(L + d.Paper(0.35), d.Paper(0.75)), EndNote(abutB));
        d.Title($"BEAM {b.TypeName} ELEVATION ({b.Mark})", $"Along the CL beam · Scale {DrawingOptions.ScaleText(d.Scale, u)} (depth exaggerated {vx:0}x)",
            new Vec2(L / 2, -D - d.Paper(1.85 + 0.22 * row)));
        return new SheetView("Beam elevation", "PS Elevation" + (type != null ? " " + type : ""), dwg, d.Scale, "Prestressed beam elevation with strand profile and debonding.");
    }

    /// <summary>Debonded strand groups of a beam: (at the start end?, debonded length, strand count, average height, symbol).</summary>
    public static List<(bool AtStart, double Length, int Count, double Y, string Symbol)> DebondGroups(PrestressedBeam b)
    {
        var res = new List<(bool, double, int, double, string)>();
        foreach (bool start in new[] { true, false })
            foreach (var g in b.Strands.Select(s => (s, len: start ? s.DebondStart : s.DebondEnd)).Where(x => x.len > 0)
                         .GroupBy(x => (Math.Round(x.len / In(1)), x.s.DebondSymbol)).OrderBy(g => g.Key.Item1))
                res.Add((start, g.Average(x => x.len), g.Count(), g.Average(x => x.s.EndHeight), g.Key.DebondSymbol));
        return res;
    }

    // ------------------------------------------------------------------ data tables per type

    /// <summary>Beam data, debonding and beam bar tables of one beam type (paper space).</summary>
    public static SheetView DataTables(ViewContext ctx, string type)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1);
        var ps = ctx.Prestressed!;
        var beams = ps.Beams.Where(b => b.TypeName == type).ToList();
        var b = Representative(beams)!;
        var pd = b.Definition ?? ps.Definition;
        var sh = b.Shape;
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        string W(double kg) => u.IsUS ? $"{kg * 2.20462:N0} lb ({kg * 2.20462 / 2000:0.0} tons)" : $"{kg / 1000:0.0} t";
        int nDefl = b.Strands.Count(s => s.Depressed), nDeb = b.Strands.Count(s => s.Debonded);
        double yb = In(sh.Yb);
        var rows = new List<string[]>
        {
            new[] { "BEAM", PsText.TypeName(sh, type) },
            new[] { "STANDARD / SOURCE", pd.Source != "" ? pd.Source : sh.Source },
            new[] { "NUMBER OF BEAMS", $"{beams.Count} ({string.Join(", ", beams.Select(x => x.Mark))})" },
            new[] { "LENGTH (END TO END)", Range(beams.Select(x => x.Length), v => u.FormatLength(v)) },
            new[] { "CL BRG TO CL BRG", Range(beams.Select(x => x.BearingSpan), v => u.FormatLength(v)) },
            new[] { "CONCRETE", $"f'c = {pd.ConcreteStrength:0.0#} ksi, f'ci = {pd.ReleaseStrength:0.0#} ksi (release)" },
            new[] { "STRANDS", $"{b.Strands.Count} - {PsText.Strand(pd, u)} dia., {pd.StrandStrength:0} ksi low-relaxation" },
            new[] { sh.IsTx ? "DEPRESSED STRANDS" : "DEFLECTED STRANDS", nDefl > 0 ? $"{nDefl}, hold-downs {u.FormatLength(b.HoldDownFromEnd)} from the ends" : "none" },
            new[] { "DEBONDED STRANDS", nDeb > 0 ? $"{nDeb} (see table)" : "none" },
            new[] { "C.G. STRANDS: MIDSPAN / END", $"{S(b.Strands.Average(s => s.Section.Y))} / {S(b.Strands.Average(s => s.EndHeight))} above the bottom" },
            new[] { "ECCENTRICITY: MIDSPAN / END", $"{S(yb - b.Strands.Average(s => s.Section.Y))} / {S(yb - b.Strands.Average(s => s.EndHeight))}" },
            new[] { "CONCRETE VOLUME / WEIGHT EACH", $"{(u.IsUS ? $"{b.Volume * 1.307951:0.0} CY" : $"{b.Volume:0.00} m3")}, {W(b.Mass)} at 155 pcf" },
            new[] { "HAUNCH AT CL BEAM MIN / MAX", $"{S(beams.Min(x => x.MinHaunch))} / {S(beams.Max(x => x.MaxHaunch))}" },
            new[] { "SKEW OF ENDS", Range(beams.SelectMany(x => new[] { x.SkewStart, x.SkewEnd }), v => $"{v:0.##} deg") },
        };
        if (beams.Any(x => !x.IsStandardLength && x.LengthDifference != 0))
            rows.Add(new[] { "STANDARD - LAYOUT LENGTH", Range(beams.Select(x => x.LengthDifference), v => S(v)) });
        rows.Add(new[] { "CAMBER", "not in the model (see the plan's beam data)" });
        double yEnd = Table(d, 0, 0, new[] { ("ITEM", 2.4), ("VALUE (FROM THE MODEL)", 4.6) }, rows, $"BEAM DATA - {type}");

        // Debonding table.
        var deb = b.Strands.Where(s => s.Debonded).GroupBy(s => (Math.Round(s.DebondStart / In(1)), Math.Round(s.DebondEnd / In(1)), s.DebondSymbol))
            .OrderBy(g => g.Key.Item1).Select(g => new[]
            {
                g.Key.DebondSymbol == "" ? "-" : g.Key.DebondSymbol, g.Count().ToString(),
                g.First().DebondStart > 0 ? u.FormatLength(g.First().DebondStart) : "-", g.First().DebondEnd > 0 ? u.FormatLength(g.First().DebondEnd) : "-",
                string.Join(" ", g.Select(s => $"({S(s.Section.X)},{S(s.Section.Y)})").Take(4)) + (g.Count() > 4 ? " ..." : ""),
            }).ToList();
        double x2 = 7.4;
        if (deb.Count > 0)
            Table(d, x2, 0, new[] { ("SYMBOL", 0.7), ("NO.", 0.45), ("FROM START", 1.0), ("FROM END", 1.0), ("POSITIONS (X, Y)", 4.4) }, deb, "DEBONDED STRANDS (SHEATHED LENGTH FROM THE BEAM END)");
        // Beam bars (generic, when the model has beam reinforcing).
        var marks = PsBars.MarksOf(ps, type);
        double yBars = deb.Count > 0 ? -0.6 - 0.19 * (deb.Count + 1) : 0;
        if (marks.Count > 0)
        {
            var bar = marks.OrderBy(m => m.Mark, StringComparer.OrdinalIgnoreCase).Select(m => new[]
            {
                m.Mark, m.Size.Name, m.Count.ToString(), m.Varies ? $"{u.FormatLength(m.MinLength)} - {u.FormatLength(m.Length)}" : u.FormatLength(m.Length),
                m.Location, u.IsUS ? $"{m.TotalMass * 2.20462:N0}" : $"{m.TotalMass:N0}",
            }).ToList();
            Table(d, x2, yBars, new[] { ("MARK", 0.7), ("SIZE", 0.5), ("NO.", 0.5), ("LENGTH", 1.3), ("LOCATION", 3.6), (u.IsUS ? "LB" : "KG", 0.9) }, bar,
                $"BEAM REINFORCING - {type} (ALL BEAMS OF THE TYPE)");
        }
        else
            d.Text("BEAM REINFORCING: NOT IN THE MODEL", new Vec2(d.Paper(x2), d.Paper(yBars - 0.1)), d.Paper(0.09), 0, TextAlign.Left);
        // Fit marker (keeps the composer's extents stable).
        dwg.Layer(SheetLayers.Grid).Circles.Add(new CircleItem(new Vec3(0, 0.0254 * 0.4, 0), 0));
        return new SheetView($"{type} data", $"PS Data {type}", dwg, 1, "", PaperSpace: true);
    }

    private static string Range(IEnumerable<double> v, Func<double, string> f)
    {
        var l = v.ToList();
        if (l.Count == 0) return "-";
        string a = f(l.Min()), b = f(l.Max());
        return a == b ? a : $"{a} to {b}";
    }

    // ------------------------------------------------------------------ schedule and notes

    /// <summary>Girder schedule (paper space): identical beams grouped by type, length, skews and strands; agency notes from the model.</summary>
    public static SheetView Schedule(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1);
        var ps = ctx.Prestressed!;
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        string W(double kg) => u.IsUS ? $"{kg * 2.20462 / 2000:0.0} T" : $"{kg / 1000:0.0} t";
        var groups = ps.Beams
            .GroupBy(b => (b.TypeName, Math.Round(b.Length / In(0.125)), Math.Round(b.SkewStart, 3), Math.Round(b.SkewEnd, 3), b.Strands.Count))
            .Select(g => g.ToList()).OrderBy(g => g.Min(b => b.Span)).ThenBy(g => g.Min(b => b.Girder)).ToList();
        var rows = new List<string[]>();
        foreach (var g in groups)
        {
            var b = g[0];
            var pd = b.Definition ?? ps.Definition;
            double yb = In(b.Shape.Yb);
            string marks = string.Join(", ", g.Select(x => x.Mark));
            if (marks.Length > 34) marks = $"{g[0].Mark} ... {g[^1].Mark}";
            rows.Add(new[]
            {
                marks, b.TypeName, g.Count.ToString(), u.FormatLength(b.Length), u.FormatLength(b.BearingSpan),
                $"{b.SkewStart:0.##}° / {b.SkewEnd:0.##}°", $"{b.Strands.Count} - {PsText.Strand(pd, u)}",
                $"{b.Strands.Count(s => s.Depressed)} / {b.Strands.Count(s => s.Debonded)}",
                $"{pd.ConcreteStrength:0.0#} / {pd.ReleaseStrength:0.0#}",
                $"{S(yb - b.Strands.Average(s => s.Section.Y))} / {S(yb - b.Strands.Average(s => s.EndHeight))}",
                $"{S(g.Min(x => x.MinHaunch))} / {S(g.Max(x => x.MaxHaunch))}", W(b.Mass),
            });
        }
        var cols = new (string, double)[]
        {
            ("BEAMS", 2.3), ("TYPE", 0.8), ("NO.", 0.45), ("LENGTH", 1.05), ("CL BRG-CL BRG", 1.15), ("SKEW BEG / END", 1.3), ("STRANDS", 1.05),
            (ps.Shape.IsTx ? "DEPR./DEB." : "DEFL./DEB.", 0.85), ("f'c / f'ci KSI", 1.0), ("\"e\" CL / END", 1.25), ("HAUNCH MIN / MAX", 1.4), ("WEIGHT EA.", 0.9),
        };
        double y = Table(d, 0, 0, cols, rows, "PRESTRESSED BEAM SCHEDULE", 0.2);
        var notes = PsText.Notes(ps, ctx.Framing.Definition, u);
        d.Text("NOTES", new Vec2(0, d.Paper(y - 0.35)), d.Paper(0.11), 0, TextAlign.Left, SheetLayers.Title);
        double yn = y - 0.62;
        double width = cols.Sum(c => c.Item2);
        for (int i = 0; i < notes.Count; i++)
        {
            var lines = Wrap($"{i + 1}. {notes[i]}", width, 0.08);
            for (int k = 0; k < lines.Count; k++, yn -= 0.17)
                d.Text((k > 0 ? "    " : "") + lines[k], new Vec2(0, d.Paper(yn)), d.Paper(0.08), 0, TextAlign.Left);
            yn -= 0.04;
        }
        return new SheetView("Beam schedule", "PS Schedule", dwg, 1, "", PaperSpace: true);
    }

    // ------------------------------------------------------------------ beam layout

    /// <summary>
    /// Prestressed beam layout (framing plan) of a group of spans in the developed plan (X = station along the CL alignment, Y = minus the
    /// offset): every beam outline (chorded, per span) with its mark and type, CL bearing lines with the beam spacing along them, CL pier
    /// lines, span lengths, steel diaphragm lines with marks and the concrete diaphragm footprints.
    /// </summary>
    public static SheetView BeamLayout(ViewContext ctx, int firstSupport, int lastSupport)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var u = ctx.Units;
        var ps = ctx.Prestressed!;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.LayoutScale);
        Vec2 Dev(Vec2 p) { var so = a.Inverse(p); return new Vec2(so.Station, -so.Offset); }
        var s0 = f.Supports[firstSupport]; var s1 = f.Supports[lastSupport];
        double x0 = s0.Station - d.Paper(0.6), x1 = s1.Station + d.Paper(0.6);

        // Beams: outline (top flange edges, ends from the beam body when it has one) + CL, mark at mid-length.
        foreach (var b in ps.Beams.Where(b => b.Span >= firstSupport && b.Span < lastSupport))
        {
            var t = (b.TopEnd.XY - b.TopStart.XY).Normalized();
            var n = t.Left;
            double hw = b.Shape.TopWidth / 2;
            Vec2 EndDir(Vec3 nrm) { var e = new Vec2(-nrm.Y, nrm.X); return e.Length < 1e-6 ? n : e.Normalized(); }
            var es = b.Body != null ? EndDir(b.Body.StartNormal) : n;
            var ee = b.Body != null ? EndDir(b.Body.EndNormal) : n;
            // Corners where the flange edge lines meet the end lines.
            Vec2 Corner(Vec2 p, Vec2 dir, double side)
            {
                var q = b.TopStart.XY + n * (side * hw);
                return Numerics.IntersectLines(q, t, p, dir, out double tt, out _) ? q + t * tt : p + dir * side * hw;
            }
            var pts = new[] { Corner(b.TopStart.XY, es, 1), Corner(b.TopEnd.XY, ee, 1), Corner(b.TopEnd.XY, ee, -1), Corner(b.TopStart.XY, es, -1) }.Select(Dev).ToList();
            d.Poly(SheetLayers.Concrete, pts, true);
            d.Line(SheetLayers.Center, Dev(b.TopStart.XY), Dev(b.TopEnd.XY));
            var mid = Dev((b.TopStart.XY + b.TopEnd.XY) / 2);
            var dir = Dev(b.TopEnd.XY) - Dev(b.TopStart.XY);
            d.TextAlong($"{b.Mark} {b.TypeName}", mid + new Vec2(-dir.Normalized().X * d.Paper(0.5), 0) + dir.Normalized().Left * d.Paper(0.06), dir, d.SmallTextH);
        }

        // Support lines: CL brg (back / ahead) with the beam spacing; CL pier from the substructure.
        var spanDimY = new List<double>();
        for (int si = firstSupport; si <= lastSupport; si++)
        {
            var sl = f.Supports[si];
            // Bearings = behind the support (the span before; at the first support the span after), AheadBearings = the span after.
            // below = spacing dimensions and label on the ahead (up-station) side of the line: the end abutment and the ahead line of a pier.
            var lines = new List<(BearingResult?[] br, string label, bool below)>();
            bool last = si == f.Supports.Count - 1;
            lines.Add((sl.Bearings, "CL BRG", last));
            if (sl.AheadBearings is { } ah) lines.Add((ah, "CL BRG", true));
            foreach (var (brs, label, below) in lines)
            {
                var pts = brs.OfType<BearingResult>().Select(x => new Vec2(x.Station, -x.Offset)).OrderBy(p => p.Y).ToList();
                if (pts.Count < 2) continue;
                var dir = (pts[^1] - pts[0]).Normalized();
                var pa = pts[0] - dir * d.Paper(0.3); var pb = pts[^1] + dir * d.Paper(0.3);
                d.Line(SheetLayers.Center, pa, pb);
                d.TextAlong($"{label} {sl.Name.ToUpperInvariant()}", pb + dir * d.Paper(0.05) + dir.Left * d.Paper(below ? -0.06 : 0.06), dir, d.SmallTextH * 0.9);
                // Spacing along the bearing line (plan distance between adjacent CL bearings), on the outside of the span.
                for (int i = 1; i < pts.Count; i++)
                {
                    var p0 = pts[i - 1]; var p1 = pts[i];
                    var w0 = a.PointAt(p0.X, -p0.Y); var w1 = a.PointAt(p1.X, -p1.Y);
                    d.Dim(p0, p1, below ? -0.3 : 0.3, u.FormatLength(Vec2.Distance(w0, w1)));
                }
            }
            var unit = ctx.Substructure?.Units.FirstOrDefault(x => x.SupportIndex == si);
            if (unit != null && unit.Type == SupportType.Pier && unit.CapS1 > unit.CapS0)
            {
                var c0 = Dev(unit.Plan(unit.CapS0, 0)); var c1 = Dev(unit.Plan(unit.CapS1, 0));
                var cd = (c1 - c0).Normalized();
                if (cd.Y < 0) { (c0, c1) = (c1, c0); cd = -cd; }
                d.Line(SheetLayers.Center, c0 - cd * d.Paper(0.15), c1 + cd * d.Paper(0.15));
                d.TextAlong($"CL {unit.Name.ToUpperInvariant()}", c0 - cd * d.Paper(0.2), -cd, d.SmallTextH * 0.9);
            }
            string skew = sl.SkewDegrees != 0 ? $", SKEW {UnitSystem.FormatAngleDms(Math.Abs(sl.SkewDegrees) * Math.PI / 180, false)} {(sl.SkewDegrees > 0 ? "RT" : "LT")}" : ", SKEW 0";
            var lo = sl.Bearings.OfType<BearingResult>().Select(x => new Vec2(x.Station, -x.Offset)).OrderByDescending(p => p.Y).FirstOrDefault();
            d.Text($"STA {u.FormatStation(sl.Station)}{skew}", new Vec2(sl.Station, lo.Y + d.Paper(0.75)), d.SmallTextH, 0, TextAlign.Center);
        }

        // Steel intermediate diaphragms: a line between the two cast holes of each bay, with the mark.
        if (ps.SteelDiaphragms is { } sd)
            foreach (var bay in sd.Bays)
            {
                var bd = bay.Bay;
                if (bd.Span < firstSupport + 1 || bd.Span > lastSupport) continue;
                var hl = sd.Holes.FirstOrDefault(h => h.Span == bd.Span && h.Girder == bd.LeftGirder && h.Index == bd.LeftHole);
                var hr = sd.Holes.FirstOrDefault(h => h.Span == bd.Span && h.Girder == bd.LeftGirder + 1 && h.Index == bd.RightHole);
                if (hl == null || hr == null) continue;
                var pl = Dev(hl.Lower.XY); var pr = Dev(hr.Lower.XY);
                d.Line(bay.Detailed ? SheetLayers.Steel : SheetLayers.SteelHidden, pl, pr);
                var dir = (pr - pl).Normalized();
                d.Text(bay.Mark, (pl + pr) / 2 + new Vec2(d.Paper(0.05), 0), d.SmallTextH * 0.75, 0, TextAlign.Left);
            }

        // Concrete diaphragms: plan footprint (hull of the solid), name.
        if (ps.ConcreteDiaphragms is { } cdm)
            foreach (var cd in cdm.Diaphragms.Where(x => x.SupportIndex >= firstSupport && x.SupportIndex <= lastSupport && !x.Mesh.IsEmpty))
            {
                var hull = Solids.MeshBuilder.ConvexHull(cd.Mesh.Positions.Select(p => Dev(p.XY)));
                if (hull.Count < 3) continue;
                d.Poly(SheetLayers.ConcreteHidden, hull, true);
            }

        // CL alignment with station ticks; span lengths CL brg - CL brg and CL support - CL support.
        double unitLen = u.LongUnit;
        double minor = (u.IsUS ? 25 : 10) * unitLen, major = 100 * unitLen;
        d.Line(SheetLayers.Center, new Vec2(x0, 0), new Vec2(x1, 0));
        for (double sta = Math.Ceiling(x0 / minor) * minor; sta <= x1; sta += minor)
        {
            bool mj = Math.Abs(Math.IEEERemainder(sta, major)) < 1e-6;
            d.Line(SheetLayers.Center, new Vec2(sta, -d.Paper(mj ? 0.08 : 0.04)), new Vec2(sta, d.Paper(mj ? 0.08 : 0.04)));
            if (mj) d.Text(u.FormatStation(sta, 0), new Vec2(sta, d.Paper(0.18)), d.SmallTextH * 0.85, 0, TextAlign.Center);
        }
        d.Text((a.Name.StartsWith("CL", StringComparison.OrdinalIgnoreCase) ? a.Name : "CL " + a.Name).ToUpperInvariant(), new Vec2(x0, d.Paper(0.12)), d.SmallTextH, 0, TextAlign.Left);
        var box = dwg.Extents();
        double yTop = box.MaxY + d.Paper(0.35);
        for (int i = firstSupport + 1; i <= lastSupport; i++)
        {
            var spanBeams = ps.Beams.Where(b => b.Span == i - 1).ToList();
            d.Dim(new Vec2(f.Supports[i - 1].Station, yTop), new Vec2(f.Supports[i].Station, yTop), 0, $"SPAN {i} = {u.FormatLength(f.Supports[i].Station - f.Supports[i - 1].Station)}");
            if (spanBeams.Count > 0)
                d.Text($"{string.Join(", ", spanBeams.Select(b => b.TypeName).Distinct())}: CL BRG - CL BRG {Range(spanBeams.Select(b => b.BearingSpan), v => u.FormatLength(v))}",
                    new Vec2((f.Supports[i - 1].Station + f.Supports[i].Station) / 2, yTop - d.Paper(0.2)), d.SmallTextH, 0, TextAlign.Center);
        }
        d.Text("SPANS ALONG THE CL ALIGNMENT BETWEEN THE SUPPORT STATIONS; BEAM SPACING = PLAN DISTANCE BETWEEN CL BEARINGS ALONG THE BEARING LINE.\n" +
               "SOLID LINE BETWEEN BEAMS = STEEL DIAPHRAGM (MARK), DASHED OUTLINE = CONCRETE DIAPHRAGM.",
            new Vec2((x0 + x1) / 2, yTop + d.Paper(0.6)), d.SmallTextH, 0, TextAlign.Center);
        box = dwg.Extents();
        string spans = firstSupport + 1 == lastSupport ? $"SPAN {lastSupport}" : $"SPANS {firstSupport + 1}-{lastSupport}";
        d.Title($"BEAM LAYOUT - {spans}", $"Developed along the CL alignment · Scale {DrawingOptions.ScaleText(d.Scale, u)}", new Vec2((x0 + x1) / 2, box.MinY - d.Paper(0.5)));
        return new SheetView($"Beam layout {spans.ToLowerInvariant()}", $"Beam layout {firstSupport + 1}-{lastSupport}", dwg, d.Scale, "Prestressed beam layout: beams, marks, bearing lines, spacing, diaphragms.");
    }

    // ------------------------------------------------------------------ steel diaphragms

    /// <summary>Typical steel intermediate diaphragm (one per distinct member, up to two): true section of the two beams through the bay and
    /// the diaphragm parts and bolts projected on it (looking along the left beam).</summary>
    public static SheetView SteelDiaphragmDetail(ViewContext ctx, string member)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var ps = ctx.Prestressed!;
        var sd = ps.SteelDiaphragms!;
        var steel = ctx.Steel;
        var bay = sd.Bays.Where(b => b.Detailed && b.Bay.Member == member).OrderBy(b => b.Bay.Span).ThenBy(b => b.Bay.LeftGirder).FirstOrDefault()
                  ?? sd.Bays.First(b => b.Bay.Member == member);
        var bd = bay.Bay;
        var hl = sd.Holes.First(h => h.Span == bd.Span && h.Girder == bd.LeftGirder && h.Index == bd.LeftHole);
        var hr = sd.Holes.First(h => h.Span == bd.Span && h.Girder == bd.LeftGirder + 1 && h.Index == bd.RightHole);
        // Diaphragm inputs are 1-based (span, beam line).
        var bl = ps.Beams.First(b => b.Span == bd.Span - 1 && b.Girder == bd.LeftGirder - 1);
        var br = ps.Beams.FirstOrDefault(b => b.Span == bd.Span - 1 && b.Girder == bd.LeftGirder);
        var t = (bl.TopEnd - bl.TopStart).Normalized();
        var tH = new Vec3(t.X, t.Y, 0).Normalized();
        var origin = new Vec3((hl.Lower.X + hr.Lower.X) / 2, (hl.Lower.Y + hr.Lower.Y) / 2, 0);
        var sec = new MeshSection(origin, tH, new Vec3(0, 0, 1));
        // x should run from the left beam to the right beam.
        if (sec.To2D(hr.Lower).X < sec.To2D(hl.Lower).X) sec = new MeshSection(origin, tH * -1, new Vec3(0, 0, 1));
        double xl = sec.To2D(hl.Lower).X, xr = sec.To2D(hr.Lower).X;
        foreach (var bm in new[] { bl, br })
            if (bm != null) sec.Draw(d, bm.Mesh, SheetLayers.ConcreteCut, hatchSpacing: 0.08);
        if (ctx.Concrete?.Deck is { } deck) sec.Draw(d, deck, SheetLayers.ConcreteCut, xl - In(30), xr + In(30));
        // Parts of the bay assembly projected (outline hulls); bolts as circles (axis along the view) or short bars.
        var parts = (steel?.Parts ?? new List<Steel.SteelPart>()).Where(p => p.Assembly == bay.Mark).ToList();
        if (parts.Count == 0) parts = sd.Parts.Where(p => p.Assembly == bay.Mark).ToList();
        foreach (var p in parts)
        {
            var hull = Solids.MeshBuilder.ConvexHull(p.Mesh.Positions.Select(sec.To2D));
            if (hull.Count >= 3) d.Poly(SheetLayers.Steel, hull, true);
        }
        var bolts = (steel?.Bolts ?? new List<Steel.BoltInstance>()).Where(b => b.Assembly == bay.Mark).ToList();
        if (bolts.Count == 0) bolts = sd.Bolts.Where(b => b.Assembly == bay.Mark).ToList();
        foreach (var b in bolts)
        {
            var c = sec.To2D(b.Frame.Origin);
            double r = (b.Diameter > 0 ? b.Diameter : In(0.875)) / 2;
            var ax = b.Frame.Z;
            double along = Math.Abs(Vec3.Dot(ax, sec.Normal));
            if (along > 0.7) d.Circle(SheetLayers.Steel, c, r);
            else
            {
                var e = new Vec2(Vec3.Dot(ax, sec.XAxis), Vec3.Dot(ax, sec.YAxis)).Normalized() * (b.Length / 2);
                d.Line(SheetLayers.Steel, c - e, c + e);
            }
        }
        // Cast holes and dimensions.
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        var pl2 = sec.To2D(hl.Lower); var pu2 = sec.To2D(hl.Upper);
        double zb = sec.To2D(bl.BearingStart).Y;
        foreach (var h in new[] { hl, hr })
        {
            d.Circle(SheetLayers.SteelHidden, sec.To2D(h.Lower), h.Diameter / 2);
            d.Circle(SheetLayers.SteelHidden, sec.To2D(h.Upper), h.Diameter / 2);
        }
        double yDim = parts.Count > 0 ? parts.SelectMany(p => p.Mesh.Positions).Max(p => sec.To2D(p).Y) + d.Paper(0.6) : pu2.Y + d.Paper(1);
        d.Dim(new Vec2(xl, yDim), new Vec2(xr, yDim), 0, $"{u.FormatLength(Math.Abs(xr - xl))} BEAM SPACING (AT THE HOLES)");
        double xd = xl - d.Paper(0.8);
        double bottom = BeamBottomAt(bl, hl.Lower, sec);
        d.Dim(new Vec2(xd, bottom), new Vec2(xd, pl2.Y), 0.25, S(pl2.Y - bottom));
        d.Dim(new Vec2(xd, pl2.Y), new Vec2(xd, pu2.Y), 0.25, S(pu2.Y - pl2.Y));
        var box = dwg.Extents();
        d.Leader(new Vec2((xl + xr) / 2, (pl2.Y + pu2.Y) / 2), new Vec2((xl + xr) / 2 + d.Paper(0.6), box.MinY - d.Paper(0.3)),
            $"{bd.Member}{(bd.Lower != null ? " + " + bd.Lower : "")}, {u.FormatLength(bay.Length)} LONG (BAY {bay.Mark})\n{bolts.Count} - {S(bolts.FirstOrDefault()?.Diameter ?? In(0.875))} DIA. BOLTS ({ctx.Framing.Definition.PsSteelDiaphragms?.BoltGrade})");
        box = dwg.Extents();
        d.Title($"STEEL DIAPHRAGM {bd.Member}", $"Bay {bay.Mark}: span {bd.Span}, G{bd.LeftGirder}-G{bd.LeftGirder + 1}, looking along the beams · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((xl + xr) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView($"Diaphragm {bd.Member}", $"PS Diaphragm {bd.Member}", dwg, d.Scale, "Steel intermediate diaphragm: beams cut through the bay, parts and bolts projected.");
    }

    private static double BeamBottomAt(PrestressedBeam b, Vec3 p, MeshSection sec)
    {
        var fr = new BeamFrame(b);
        return sec.To2D(p).Y - fr.Up(p);
    }

    /// <summary>Steel diaphragm bay table and the cast-hole chains per beam line (paper space).</summary>
    public static SheetView SteelDiaphragmTables(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1);
        var sd = ctx.Prestressed!.SteelDiaphragms!;
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        var rows = sd.Bays.OrderBy(b => b.Bay.Span).ThenBy(b => b.Bay.LeftGirder).ThenBy(b => b.Bay.LeftHole).Select(b => new[]
        {
            b.Mark, b.Bay.Span.ToString(), $"G{b.Bay.LeftGirder}-G{b.Bay.LeftGirder + 1}", $"{b.Bay.LeftHole} / {b.Bay.RightHole}", b.Bay.Member + (b.Bay.Lower != null ? " + " + b.Bay.Lower : ""),
            u.FormatLength(b.Length), S(b.HeightDifference), !b.Detailed ? "NOT DETAILED" : b.LeftFit == null || b.RightFit == null ? "-" : b.Fits ? "OK" : "CHECK", $"{b.MemberWeightLb + b.LowerWeightLb:N0}",
        }).ToList();
        double y = Table(d, 0, 0, new[] { ("MARK", 0.9), ("SPAN", 0.5), ("BEAMS", 0.8), ("HOLES L / R", 0.9), ("MEMBER", 1.5), ("LENGTH", 1.1), ("RISE", 0.7), ("SLOT FIT", 1.1), ("LB", 0.6) },
            rows, "STEEL DIAPHRAGMS (FROM THE MODEL)", 0.17);
        d.Text($"TOTAL STRUCTURAL STEEL (DIAPHRAGM PARTS): {sd.SteelWeightLb:N0} LB; BOLTS {sd.Bolts.Count}. RISE = RIGHT MINUS LEFT BOLT GROUP ELEVATION.",
            new Vec2(0, d.Paper(y - 0.22)), d.Paper(0.075), 0, TextAlign.Left);
        var chains = sd.Holes.GroupBy(h => (h.Span, h.Girder)).OrderBy(g => g.Key.Span).ThenBy(g => g.Key.Girder).Select(g => new[]
        {
            $"SPAN {g.Key.Span}", $"G{g.Key.Girder}", g.Select(h => h.Index).Distinct().Count().ToString(),
            string.Join(", ", g.GroupBy(h => h.Index).OrderBy(x => x.Key).Select(x => u.FormatLength(x.First().FromBeamEnd))),
        }).ToList();
        Table(d, 8.5, 0, new[] { ("SPAN", 0.7), ("BEAM", 0.5), ("NO.", 0.4), ("CAST HOLES FROM THE BEAM END (LOWER STATION)", 5.4) }, chains, "CAST HOLES IN THE BEAM WEBS", 0.17);
        return new SheetView("Diaphragm tables", "PS Diaphragm tables", dwg, 1, "", PaperSpace: true);
    }

    // ------------------------------------------------------------------ concrete diaphragms

    /// <summary>Section of everything at a support, normal to the support line (x = ahead of the support line): the concrete diaphragm,
    /// beams, substructure unit solids, deck and haunches, fillers, cut by a vertical plane at <paramref name="atGirder"/> (a beam line)
    /// or mid-bay after it (<paramref name="midBay"/>).</summary>
    public static SheetView SupportSection(ViewContext ctx, int supportIndex, int atGirder, bool midBay, string label)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var f = ctx.Framing;
        var ps = ctx.Prestressed;
        var sl = f.Supports[supportIndex];
        var brs = sl.Bearings.OfType<BearingResult>().ToList();
        var ahead = sl.AheadBearings?.OfType<BearingResult>().ToList();
        Vec2 P(BearingResult b) => f.Alignment.PointAt(b.Station, b.Offset);
        // Support line through the bearings (back and ahead averaged at piers).
        Vec2 BP(int g)
        {
            var pb = sl.Bearings.ElementAtOrDefault(g); var pa = sl.AheadBearings?.ElementAtOrDefault(g);
            if (pb != null && pa != null) return (P(pb) + P(pa)) / 2;
            return pb != null ? P(pb) : pa != null ? P(pa) : Vec2.Zero;
        }
        int ng = Math.Max(sl.Bearings.Length, sl.AheadBearings?.Length ?? 0);
        int g0 = Math.Clamp(atGirder, 0, Math.Max(0, ng - 1));
        var p0 = BP(g0);
        var p1 = midBay && g0 + 1 < ng ? BP(g0 + 1) : p0;
        var along = (BP(ng - 1) - BP(0)).Normalized();
        var at = midBay ? (p0 + p1) / 2 : p0;
        var origin = new Vec3(at.X, at.Y, 0);
        var sec = new MeshSection(origin, new Vec3(along.X, along.Y, 0), new Vec3(0, 0, 1));
        var fwd = f.Alignment.PointAt(sl.Station + 1) - f.Alignment.PointAt(sl.Station);
        if (Vec3.Dot(sec.XAxis, new Vec3(fwd.X, fwd.Y, 0)) < 0) sec = new MeshSection(origin, new Vec3(-along.X, -along.Y, 0), new Vec3(0, 0, 1));
        double w = Ft(7);
        bool any = false;
        void Cut(Solids.Mesh? m, LayerStyle lyr, bool hatch = true)
        {
            if (m == null || m.IsEmpty) return;
            var b = m.Bounds();
            // quick reject: plane distance range
            var corners = new[] { b.Min, b.Max, new Vec3(b.Min.X, b.Max.Y, b.Min.Z), new Vec3(b.Max.X, b.Min.Y, b.Min.Z) };
            var ds = corners.Select(c => Vec3.Dot(c - origin, sec.Normal)).ToList();
            if (ds.Min() > 0 || ds.Max() < 0) return;
            int before = dwg.Layers.Sum(l => l.Polylines.Count);
            sec.Draw(d, m, lyr, -w, w, hatch);
            any |= dwg.Layers.Sum(l => l.Polylines.Count) > before;
        }
        if (ps?.ConcreteDiaphragms is { } cdm)
        {
            foreach (var cd in cdm.Diaphragms.Where(x => x.SupportIndex == supportIndex)) Cut(cd.Mesh, SheetLayers.ConcreteCut);
            foreach (var ac in cdm.Accessories.Where(x => x.Support == sl.Name)) Cut(ac.Mesh, SheetLayers.Hatch, false);
        }
        if (ps != null)
            foreach (var b in ps.Beams.Where(b => b.Span == supportIndex - 1 || b.Span == supportIndex)) Cut(b.Mesh, SheetLayers.ConcreteCut);
        if (ps != null)
            foreach (var pad in ps.Pads.Where(p => p.Support == sl.Name)) Cut(pad.Mesh, SheetLayers.Bearing, false);
        var unit = ctx.Substructure?.Units.FirstOrDefault(x => x.SupportIndex == supportIndex);
        if (unit != null)
            foreach (var s in unit.Solids.Where(s => s.Kind != Substructure.SubElementKind.Pile)) Cut(s.Mesh, s.IsConcrete ? SheetLayers.ConcreteCut : SheetLayers.Steel);
        if (ctx.Concrete is { } c)
        {
            Cut(c.Deck, SheetLayers.ConcreteCut);
            Cut(c.Haunches, SheetLayers.ConcreteCut);
        }
        // Piles: only their top (3 ft below the lowest concrete cut), so the section keeps a detail scale.
        if (unit != null && any)
        {
            double yMin = dwg.Extents().MinY - Ft(3);
            foreach (var s in unit.Solids.Where(s => s.Kind == Substructure.SubElementKind.Pile))
                foreach (var (a0, b0) in MeshSection.ClipY(MeshSection.ClipX(sec.Cut(s.Mesh), -w, w), yMin, double.PositiveInfinity))
                    d.Line(SheetLayers.Steel, a0, b0);
        }
        // Diaphragm extents: width at its bottom, top / bottom elevations.
        if (ps?.ConcreteDiaphragms is { } cdx)
        {
            var segs = cdx.Diaphragms.Where(x => x.SupportIndex == supportIndex).SelectMany(x => MeshSection.ClipX(sec.Cut(x.Mesh), -w, w)).ToList();
            if (segs.Count > 0)
            {
                var pts = segs.SelectMany(q => new[] { q.A, q.B }).ToList();
                double xa = pts.Min(q => q.X), xb = pts.Max(q => q.X), ya = pts.Min(q => q.Y), yb = pts.Max(q => q.Y);
                double below = (ya - dwg.Extents().MinY) / d.Paper(1) + 0.3;
                d.Dim(new Vec2(xa, ya), new Vec2(xb, ya), -below, u.FormatLength(xb - xa, LengthKind.Short) + " DIAPHRAGM");
                d.ElevationTag(new Vec2(xb, yb), $"TOP OF DIAPHRAGM EL {d.Elev(yb)}");
                d.ElevationTag(new Vec2(xb, ya), $"BOTTOM EL {d.Elev(ya)}");
            }
        }
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        d.Line(SheetLayers.Center, new Vec2(0, dwg.Extents().MinY - d.Paper(0.2)), new Vec2(0, dwg.Extents().MaxY + d.Paper(0.3)));
        var box = dwg.Extents();
        d.Text(unit?.Type == SupportType.Pier ? $"CL {sl.Name.ToUpperInvariant()}" : $"CL BRG {sl.Name.ToUpperInvariant()}", new Vec2(0, box.MaxY + d.Paper(0.12)), d.SmallTextH, 0, TextAlign.Center);
        if (!any) d.Text("NOTHING MODELED AT THIS SECTION", new Vec2(0, 0), d.TextH, 0, TextAlign.Center);
        box = dwg.Extents();
        string where = midBay && g0 + 1 < ng ? $"mid-bay G{g0 + 1}-G{g0 + 2}" : $"CL beam G{g0 + 1}";
        d.Title($"SECTION {label}", $"{sl.Name}, {where}, normal to the support line (true section of the model) · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((box.MinX + box.MaxX) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView($"Section {label}", $"Support section {sl.Name} {label}", dwg, d.Scale, "Section through the support (diaphragm, beams, substructure, deck) from the model solids.");
    }

    /// <summary>Concrete diaphragm data (paper space): parts, volumes, length, holes, unbuilt parts, fillers.</summary>
    public static SheetView ConcreteDiaphragmTable(ViewContext ctx, IEnumerable<Diaphragms.ConcreteDiaphragm> list)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1);
        string V(double m3) => u.IsUS ? $"{m3 * 1.307951:0.00} CY" : $"{m3:0.00} m3";
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        var rows = new List<string[]>();
        foreach (var cd in list)
        {
            foreach (var p in cd.Parts)
                rows.Add(new[] { cd.Name, p.Name, $"{S(p.XMin)} to {S(p.XMax)}", V(p.Volume), u.FormatLength(cd.Length), cd.Material });
            foreach (var n in cd.UnbuiltParts) rows.Add(new[] { cd.Name, n, "NOT BUILT (UNRESOLVED IN THE PLANS)", "-", "-", "" });
            if (cd.Holes.Count > 0) rows.Add(new[] { cd.Name, $"{cd.Holes.Count} CAST HOLES IN THE BEAM WEBS", $"DIA. {S(cd.Holes[0].Diameter)}", "", "", "" });
        }
        Table(d, 0, 0, new[] { ("DIAPHRAGM", 2.2), ("PART", 2.6), ("X FROM SUPPORT LINE", 1.8), ("VOLUME", 0.9), ("LENGTH", 1.1), ("MATERIAL", 2.6) }, rows, "CONCRETE DIAPHRAGMS (FROM THE MODEL)", 0.17);
        return new SheetView("Diaphragm data", "PS Concrete diaphragms", dwg, 1, "", PaperSpace: true);
    }
}
