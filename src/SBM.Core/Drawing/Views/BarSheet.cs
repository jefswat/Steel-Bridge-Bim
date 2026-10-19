using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Bar list and bending diagrams, drawn 1:1 in paper units (inches; written to DXF in inches, mm for SI).
/// One block per unit (superstructure, each support): the bar list table on the left, bending diagrams of every
/// non-straight mark on the right. Diagrams are not to scale; dimensions are out-to-out.
/// <see cref="Build"/> is one long sheet; <see cref="Pages"/> splits the blocks onto 11x17 drawing areas.
/// </summary>
public static class BarSheet
{
    private sealed record Layout((string head, double w)[] Cols, double DiagramX, double CellW, double CellH, int CellsPerRow);

    private static readonly Layout Wide = new(new[] { ("MARK", 0.9), ("SIZE", 0.6), ("NO.", 0.7), ("LENGTH", 1.6), ("TYPE", 1.1), ("A", 1.0), ("B", 1.0), ("C / D", 1.0), ("WEIGHT", 1.3), ("LOCATION", 8.3) },
        18.5, 1.8, 1.35, 8);

    private static readonly Layout Compact = new(new[] { ("MARK", 0.55), ("SIZE", 0.45), ("NO.", 0.5), ("LENGTH", 1.35), ("TYPE", 0.85), ("A", 0.7), ("B", 0.7), ("C / D", 0.7), ("WEIGHT", 0.75), ("LOCATION", 2.2) },
        8.95, 1.65, 1.4, 4);

    private static List<(string unit, List<BarMark> marks)> Groups(ViewContext ctx)
    {
        var groups = new List<(string unit, List<BarMark> marks)>();
        if (ctx.Concrete is { Marks.Count: > 0 } c) groups.Add(("SUPERSTRUCTURE", c.Marks));
        if (ctx.Substructure != null)
            foreach (var g in ctx.Substructure.Marks.GroupBy(m => m.Unit)) groups.Add((g.Key.ToUpperInvariant(), g.ToList()));
        return groups;
    }

    public static SheetView Build(ViewContext ctx)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1); // paper inches
        double y = 0;
        d.Text("BAR LIST AND BENDING DIAGRAMS", new Vec2(0, y), d.Paper(0.25), 0, TextAlign.Left, SheetLayers.Title);
        d.Text($"{ctx.Framing.Definition.Name.ToUpperInvariant()} · DIMENSIONS ARE OUT-TO-OUT OF BAR · BENDING DIAGRAMS NOT TO SCALE",
            new Vec2(0, y - d.Paper(0.4)), d.Paper(0.11), 0, TextAlign.Left);
        y -= d.Paper(1.0);
        foreach (var (unit, marks) in Groups(ctx))
            y = Block(d, u, unit, marks, marks.Where(m => m.Shape.Code != "STR").ToList(), y, Wide) - d.Paper(0.8);
        return new SheetView("Bar bending sheet", "Bar Bending", dwg, 1, "Bar lists and bending diagrams for the superstructure and every substructure unit, 1:1 paper units.", PaperSpace: true);
    }

    /// <summary>One page per unit (continued when a unit has too many marks or diagrams for the area).</summary>
    public static List<(string title, SheetView view)> Pages(ViewContext ctx, double areaW, double areaH)
    {
        var u = ctx.Units;
        var pages = new List<(string, SheetView)>();
        int rowsPerPage = (int)Math.Floor((areaH - 1.0) / 0.2) - 1;
        int diagramsPerPage = Compact.CellsPerRow * (int)Math.Floor((areaH - 0.6) / Compact.CellH);
        foreach (var (unit, marks) in Groups(ctx))
        {
            var shaped = marks.Where(m => m.Shape.Code != "STR").ToList();
            int nPages = Math.Max((marks.Count + rowsPerPage - 1) / rowsPerPage, (shaped.Count + diagramsPerPage - 1) / diagramsPerPage);
            nPages = Math.Max(1, nPages);
            for (int p = 0; p < nPages; p++)
            {
                var dwg = new PlanDrawing();
                var d = new Drafter(dwg, u, 1);
                var rows = marks.Skip(p * rowsPerPage).Take(rowsPerPage).ToList();
                var diags = shaped.Skip(p * diagramsPerPage).Take(diagramsPerPage).ToList();
                string title = unit + (nPages > 1 ? $" ({p + 1} OF {nPages})" : "");
                Block(d, u, title, rows, diags, 0, Compact, showTotal: p == nPages - 1, totalMarks: marks);
                // Invisible anchors (zero-radius circles) span the full area so every page sits at the top-left of the sheet.
                dwg.Layer(SheetLayers.Grid).Circles.Add(new CircleItem(new Vec3(0, 0.0254 * 0.3, 0), 0));
                dwg.Layer(SheetLayers.Grid).Circles.Add(new CircleItem(new Vec3(0.0254 * (areaW - 0.1), -0.0254 * (areaH - 0.4), 0), 0));
                pages.Add(($"BAR LIST — {title}", new SheetView($"Bar list {title}", "Bars", dwg, 1, "", PaperSpace: true)));
            }
        }
        return pages;
    }

    private static double Block(Drafter d, UnitSystem u, string unit, List<BarMark> marks, List<BarMark> shaped, double yTop, Layout lay,
        bool showTotal = true, List<BarMark>? totalMarks = null)
    {
        double P(double inch) => d.Paper(inch);
        double th = P(0.085);
        var cols = lay.Cols;
        double tableW = P(cols.Sum(c => c.w));
        d.Text(unit, new Vec2(0, yTop), P(0.15), 0, TextAlign.Left, SheetLayers.Title);
        d.Line(SheetLayers.Title, new Vec2(0, yTop - P(0.13)), new Vec2(tableW, yTop - P(0.13)));

        // ---- table
        double rowH = P(0.2);
        double y = yTop - P(0.32);
        double x = 0;
        d.Rect(SheetLayers.Text, 0, y - rowH, tableW, y);
        foreach (var (head, w) in cols)
        {
            d.Text(head, new Vec2(x + P(0.05), y - rowH / 2), th, 0, TextAlign.Left);
            x += P(w);
            d.Line(SheetLayers.Text, new Vec2(x, y), new Vec2(x, y - rowH * (marks.Count + 1)));
        }
        double wUnit = u.IsUS ? 2.20462 : 1;
        string wName = u.IsUS ? "LB" : "KG";
        string Dim(BarMark m, params string[] letters) =>
            m.Shape.Code == "STR" ? "" : string.Join("", letters.Select(l => m.Shape.Dimensions.FirstOrDefault(q => q.Letter == l)).Where(q => q != null).Take(1)
                .Select(q => u.FormatLength(q!.Value, LengthKind.Short)));
        int row = 1;
        foreach (var m in marks)
        {
            double ry = y - rowH * row;
            d.Line(SheetLayers.Text, new Vec2(0, ry - rowH), new Vec2(tableW, ry - rowH));
            string len = m.Varies ? $"{u.FormatLength(m.MinLength)}–{u.FormatLength(m.Length)}" : u.FormatLength(m.Length);
            string type = m.Shape.Code switch { "STR" => "STRAIGHT", "TIE" => "TIE", "SPIRAL" => "SPIRAL", "BENT" => "BENT", "SSTR-S" => "S (SSTR)", "SSTR-U" => "U (SSTR)", _ => m.Shape.Code };
            var vals = new[]
            {
                m.Mark, m.Size.Name, m.Count.ToString(), len, type, Dim(m, "A"), Dim(m, "B"), Dim(m, "C", "D"),
                $"{m.TotalMass * wUnit:N0}", m.Location.ToUpperInvariant() + (m.Epoxy ? " (EPOXY)" : ""),
            };
            x = 0;
            for (int i = 0; i < cols.Length; i++)
            {
                // Cut to the column by the Helvetica width estimate (text height 0.085"), so nothing crosses the column line.
                string v = vals[i];
                double maxH = (cols[i].w - 0.1) / 0.085;
                if (LabelPlacer.WidthInHeights(v) > maxH)
                {
                    while (v.Length > 1 && LabelPlacer.WidthInHeights(v + "...") > maxH) v = v[..^1];
                    v += "...";
                }
                d.Text(v, new Vec2(x + P(0.05), ry - rowH / 2), th, 0, TextAlign.Left);
                x += P(cols[i].w);
            }
            row++;
        }
        d.Rect(SheetLayers.Text, 0, y - rowH * row, tableW, y);
        if (showTotal)
        {
            double total = (totalMarks ?? marks).Sum(m => m.TotalMass) * wUnit;
            d.Text($"TOTAL REINFORCING {total:N0} {wName}", new Vec2(tableW, y - rowH * row - P(0.2)), th * 1.1, 0, TextAlign.Right);
        }
        double tableBottom = y - rowH * row - P(0.4);

        // ---- bending diagrams
        double gy = yTop - P(0.32);
        for (int i = 0; i < shaped.Count; i++)
        {
            int cx = i % lay.CellsPerRow, cy = i / lay.CellsPerRow;
            var origin = new Vec2(P(lay.DiagramX + cx * lay.CellW), gy - P(cy * lay.CellH));
            Diagram(d, u, shaped[i], origin, lay.CellW, lay.CellH);
        }
        double diagBottom = gy - P(((shaped.Count + lay.CellsPerRow - 1) / lay.CellsPerRow) * lay.CellH);
        return Math.Min(tableBottom, diagBottom);
    }

    /// <summary>One bending diagram in a cell whose top-left corner is <paramref name="o"/> (paper inches; small text). Drawn on its own
    /// drawing first so the label placer can separate the dimension texts inside the cell (no overprint at short legs, nothing
    /// outside the cell), then merged into the page.</summary>
    private static void Diagram(Drafter page, UnitSystem u, BarMark m, Vec2 o, double cellW, double cellH)
    {
        var cell = new PlanDrawing();
        var d = new Drafter(cell, u, page.Scale);
        DiagramBody(d, u, m, o, cellW, cellH);
        double P(double inch) => d.Paper(inch);
        LabelPlacer.Resolve(cell, new Box2 { MinX = o.X + P(0.02), MaxX = o.X + P(cellW - 0.02), MinY = o.Y - P(cellH - 0.02), MaxY = o.Y - P(0.02) });
        foreach (var l in cell.Layers)
        {
            var t = page.Drawing.Layer(l.Style);
            t.Polylines.AddRange(l.Polylines);
            t.Texts.AddRange(l.Texts);
            t.Circles.AddRange(l.Circles);
        }
    }

    private static void DiagramBody(Drafter d, UnitSystem u, BarMark m, Vec2 o, double cellW, double cellH)
    {
        double P(double inch) => d.Paper(inch);
        var dd = new Drafter(d.Drawing, u, 0.62);              // smaller dimension text and offsets inside the cell
        d.Rect(SheetLayers.Grid, o.X, o.Y - P(cellH), o.X + P(cellW), o.Y);
        d.Text($"{m.Mark} ({m.Size.Name})", o + new Vec2(P(0.05), -P(0.09)), P(0.07), 0, TextAlign.Left, SheetLayers.Title);
        d.Text($"NO. {m.Count}  L = {u.FormatLength(m.Length)}", o + new Vec2(P(cellW - 0.05), -P(0.09)), P(0.05), 0, TextAlign.Right);
        var shape = m.Shape;
        var pts = shape.Centerline;
        // Fit the shape in the cell body (leaving room for dimensions).
        double bx0 = o.X + P(0.3), bx1 = o.X + P(cellW - 0.3), by0 = o.Y - P(cellH - 0.28), by1 = o.Y - P(0.3);
        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        double sc = Math.Min((bx1 - bx0) / Math.Max(maxX - minX, 1e-6), (by1 - by0) / Math.Max(maxY - minY, 1e-6));
        var c0 = new Vec2((bx0 + bx1) / 2, (by0 + by1) / 2);
        var cm = new Vec2((minX + maxX) / 2, (minY + maxY) / 2);
        Vec2 M(Vec2 p) => c0 + (p - cm) * sc;
        d.Poly(SheetLayers.Rebar, pts.Select(M));
        foreach (var dim in shape.Dimensions)
        {
            if (Vec2.Distance(dim.From, dim.To) < 1e-9) continue;
            var a = M(dim.From);
            var b = M(dim.To);
            var n = dim.Normal.Normalized();
            var dirAB = (b - a).Normalized();
            double side = Vec2.Dot(dirAB.Left, n) >= 0 ? 1 : -1;
            dd.Dim(a, b, 0.17 * side, $"{dim.Letter} = {u.FormatLength(dim.Value, LengthKind.Short)}");
        }
        var notes = new List<string>();
        // Pin diameter at EVERY bend (user review 6m; plan convention "D = PIN DIAMETER"): the shape's pin (printed / generator) or the standard
        // minimum the 3D bar is bent with (RebarBends.PinFor), labelled on the outside of each bend.
        var bendsAt = shape.Code == "SPIRAL" ? new List<(Vec2 at, Vec2 outward)>() : BendPoints(pts, RebarBends.CenterRadius(m));
        if (bendsAt.Count > 0)
        {
            string dTxt = $"D = {u.FormatLength(RebarBends.PinFor(m), LengthKind.Short)}";
            foreach (var (at, outward) in bendsAt)
                d.Text(dTxt, M(at) + outward * P(0.09), P(0.045), 0, TextAlign.Center);
            notes.Add("D = PIN DIAMETER" + (shape.PinDiameter > 0 ? "" : " (STD. MIN.)"));
        }
        foreach (var dim in shape.Dimensions.Where(q => Vec2.Distance(q.From, q.To) < 1e-9))
            notes.Add($"{dim.Letter} = {u.FormatLength(dim.Value, LengthKind.Short)}");
        if (shape.Code == "SPIRAL") notes.Insert(0, "H = HEIGHT, P = PITCH");
        if (shape.Code == "TIE") notes.Insert(0, "135° HOOKS, " + shape.Description.Split(',').Last().Trim().ToUpperInvariant());
        if (shape.PinDiameter > 0 && bendsAt.Count == 0 && shape.Code != "SPIRAL") notes.Add($"PIN {u.FormatLength(shape.PinDiameter, LengthKind.Short)}");
        if (notes.Count > 0) d.Text(string.Join("  ", notes), o + new Vec2(P(0.05), -P(cellH - 0.08)), P(0.045), 0, TextAlign.Left);
    }

    /// <summary>
    /// The bends of a diagram centerline: a sharp corner (one vertex) or a sampled arc (consecutive turning vertices less than ~0.6 bend radius apart,
    /// same sense) counts once when it turns more than 10 deg. Returns the middle point of each bend and the unit direction away from its center
    /// (where the pin label goes). A closed outline (first = last point) includes the closing corner.
    /// </summary>
    public static List<(Vec2 at, Vec2 outward)> BendPoints(IReadOnlyList<Vec2> input, double radius)
    {
        var p = new List<Vec2>();
        foreach (var q in input) if (p.Count == 0 || Vec2.Distance(q, p[^1]) > 1e-7) p.Add(q);
        var res = new List<(Vec2, Vec2)>();
        int n = p.Count;
        if (n < 3) return res;
        bool closed = n >= 4 && Vec2.Distance(p[0], p[^1]) < 1e-6;
        if (closed) { p.RemoveAt(n - 1); n--; }
        var turn = new double[n];
        for (int i = 0; i < n; i++)
        {
            if (!closed && (i == 0 || i == n - 1)) continue;
            var a = p[(i - 1 + n) % n]; var b = p[i]; var c = p[(i + 1) % n];
            var u = (b - a).Normalized(); var v = (c - b).Normalized();
            turn[i] = Math.Atan2(Vec2.Cross(u, v), Vec2.Dot(u, v));
        }
        double minTurn = 0.5 * Math.PI / 180, gap = 0.6 * radius;
        var used = new bool[n];
        int start = 0;
        if (closed)
        {
            // start the scan after a straight vertex so no bend is split across the wrap
            int s0 = Array.FindIndex(turn, x => Math.Abs(x) < minTurn);
            start = s0 < 0 ? 0 : s0;
        }
        for (int kk = 0; kk < n; kk++)
        {
            int i = (start + kk) % n;
            if (used[i] || Math.Abs(turn[i]) < minTurn) continue;
            var run = new List<int> { i };
            used[i] = true;
            int j = i;
            while (true)
            {
                int nx = (j + 1) % n;
                if (!closed && nx >= n - 1) break;
                if (used[nx] || Math.Abs(turn[nx]) < minTurn || Math.Sign(turn[nx]) != Math.Sign(turn[i]) || Vec2.Distance(p[j], p[nx]) > gap) break;
                run.Add(nx); used[nx] = true; j = nx;
            }
            double total = run.Sum(k => turn[k]);
            if (Math.Abs(total) < 10 * Math.PI / 180) continue;
            int first = run[0], last = run[^1];
            Vec2 at = run.Count % 2 == 1 ? p[run[run.Count / 2]] : (p[run[run.Count / 2 - 1]] + p[run[run.Count / 2]]) * 0.5;
            // outward = away from the turn center: the travel direction at the middle rotated away from the turning side
            var dIn = (p[first] - p[(first - 1 + n) % n]).Normalized();
            var dOut = (p[(last + 1) % n] - p[last]).Normalized();
            var mid = (dIn + dOut);
            mid = mid.Length > 1e-9 ? mid.Normalized() : dIn;
            var outward = total > 0 ? -mid.Left : mid.Left;
            res.Add((at, outward));
        }
        return res;
    }
}
