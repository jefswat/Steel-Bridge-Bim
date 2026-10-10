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
                string v = vals[i];
                double maxChars = cols[i].w / 0.062;
                if (v.Length > maxChars) v = v[..Math.Max(1, (int)maxChars - 1)] + "…";
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

    /// <summary>One bending diagram in a cell whose top-left corner is <paramref name="o"/> (paper inches; small text).</summary>
    private static void Diagram(Drafter d, UnitSystem u, BarMark m, Vec2 o, double cellW, double cellH)
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
        // Bend radius at one bend (the lower right corner of ties), as the inside radius = pin / 2.
        if (shape.Bends.Count > 0 && shape.PinDiameter > 0)
        {
            var (c, r) = shape.Bends.OrderByDescending(bnd => bnd.Center.X - bnd.Center.Y).First();
            var arcPt = M(c + new Vec2(1, -1).Normalized() * r);
            dd.Leader(arcPt, arcPt + new Vec2(P(0.12), -P(0.1)), $"R = {u.FormatLength(shape.PinDiameter / 2, LengthKind.Short)} (INSIDE)");
        }
        var notes = new List<string>();
        foreach (var dim in shape.Dimensions.Where(q => Vec2.Distance(q.From, q.To) < 1e-9))
            notes.Add($"{dim.Letter} = {u.FormatLength(dim.Value, LengthKind.Short)}");
        if (shape.Code == "SPIRAL") notes.Insert(0, "H = HEIGHT, P = PITCH");
        if (shape.Code == "TIE") notes.Insert(0, "135° HOOKS, " + shape.Description.Split(',').Last().Trim().ToUpperInvariant());
        if (shape.PinDiameter > 0) notes.Add($"PIN {u.FormatLength(shape.PinDiameter, LengthKind.Short)}");
        if (notes.Count > 0) d.Text(string.Join("  ", notes), o + new Vec2(P(0.05), -P(cellH - 0.08)), P(0.045), 0, TextAlign.Left);
    }
}
