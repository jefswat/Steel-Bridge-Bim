using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Sheets;

/// <summary>A view that can be built at any scale (model length per paper length); paper-space views ignore the scale.</summary>
public sealed record ViewSpec(string Name, Func<double, SheetView> Build, ScaleFamily Family, bool PaperSpace = false);

public enum ScaleFamily
{
    /// <summary>Engineer's scales (1" = 10', 20', …) for plans and elevations.</summary>
    Engineering,
    /// <summary>Architectural scales (3/4" = 1'-0", 1" = 1'-0", …) for sections and details.</summary>
    Architectural,
}

/// <summary>A view placed in a rectangular region of the sheet (paper inches from the lower-left corner of the drawing area).</summary>
public sealed record Panel(ViewSpec View, double X0, double Y0, double X1, double Y1);

public sealed record SheetSpec(string Title, string Subtitle, IReadOnlyList<Panel> Panels);

/// <summary>A composed sheet: the drawing in paper units (meters on paper; 1" = 0.0254), its title and number.</summary>
public sealed class Sheet
{
    public required string Title { get; init; }
    public required string Number { get; set; }
    public string Subtitle { get; init; } = "";
    public required PlanDrawing Drawing { get; init; }
    public double WidthIn { get; init; }
    public double HeightIn { get; init; }
    public List<string> ScaleNotes { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>Paper size and title-block data.</summary>
public sealed record SheetFormat(double WidthIn = 17, double HeightIn = 11, string Project = "", string Bridge = "", string Date = "")
{
    public const double Left = 0.75, Right = 0.25, Top = 0.25, Bottom = 0.25, TitleBlockH = 1.1;
    /// <summary>Drawing area inside the border, above the title block (paper inches).</summary>
    public (double x0, double y0, double x1, double y1) Area => (Left + 0.15, Bottom + TitleBlockH + 0.15, WidthIn - Right - 0.15, HeightIn - Top - 0.15);
}

/// <summary>
/// Lays views out on sheets: each panel's view is built at the largest standard scale that fits its region (views are
/// rebuilt at that scale so text and dimensions are the right paper size), then placed, with a border and title block.
/// </summary>
public static class SheetComposer
{
    private const double In = 0.0254;

    public static readonly double[] Engineering = { 120, 240, 360, 480, 600, 720, 960, 1200, 1800, 2400 };
    /// <summary>Architectural scales, falling back to engineer's scales for very tall or long views.</summary>
    public static readonly double[] Architectural = { 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 120, 192, 240, 360, 480 };
    public static readonly double[] Metric = { 5, 10, 20, 25, 50, 75, 100, 200, 250, 500, 1000 };

    public static Sheet Compose(SheetSpec spec, SheetFormat fmt, UnitSystem u)
    {
        var dwg = new PlanDrawing();
        var sheet = new Sheet { Title = spec.Title, Subtitle = spec.Subtitle, Number = "", Drawing = dwg, WidthIn = fmt.WidthIn, HeightIn = fmt.HeightIn };
        var (ax0, ay0, ax1, ay1) = fmt.Area;
        foreach (var panel in spec.Panels)
        {
            double px0 = ax0 + panel.X0, py0 = ay0 + panel.Y0, px1 = ax0 + panel.X1, py1 = ay0 + panel.Y1;
            double w = px1 - px0, h = py1 - py0;
            SheetView? view = null;
            double scale = 1;
            if (panel.View.PaperSpace)
            {
                view = panel.View.Build(1);
            }
            else
            {
                var scales = !u.IsUS ? Metric : panel.View.Family == ScaleFamily.Engineering ? Engineering : Architectural;
                // Largest scale that fits; if none fits (paper-sized annotation can dominate), the closest fit.
                double bestRatio = double.MaxValue;
                foreach (double s in scales)
                {
                    var v = panel.View.Build(s);
                    var e = v.Drawing.Extents();
                    if (e.IsEmpty) { view = v; scale = s; break; }
                    double ratio = Math.Max(e.Width / s / In / w, e.Height / s / In / h);
                    if (ratio <= 1) { view = v; scale = s; break; }
                    if (ratio < bestRatio - 0.02) { bestRatio = ratio; view = v; scale = s; }
                }
            }
            if (!panel.View.PaperSpace && !view!.PaperSpace)
            {
                // Labels stay inside the panel at the chosen scale (centred on the view), so none runs off the border.
                var e0 = view.Drawing.Extents();
                double fw = Math.Max(e0.Width, w * In * scale), fh = Math.Max(e0.Height, h * In * scale);
                double cx = (e0.MinX + e0.MaxX) / 2, cy = (e0.MinY + e0.MaxY) / 2;
                LabelPlacer.Resolve(view.Drawing, e0.IsEmpty ? null : new Box2 { MinX = cx - fw / 2, MaxX = cx + fw / 2, MinY = cy - fh / 2, MaxY = cy + fh / 2 });
            }
            var ext = view!.Drawing.Extents();
            if (ext.IsEmpty) continue;
            double k = 1 / scale; // model meters → paper meters
            if (ext.Width * k / In > w + 1e-6 || ext.Height * k / In > h + 1e-6)
                sheet.Warnings.Add($"{panel.View.Name} does not fit its area at the smallest scale.");
            // Center the view in its panel.
            var c = ext.Center;
            var target = new Vec2((px0 + px1) / 2 * In, (py0 + py1) / 2 * In);
            Place(dwg, view.Drawing, p => target + (p - c) * k, k);
            if (!panel.View.PaperSpace) sheet.ScaleNotes.Add($"{panel.View.Name}: {DrawingOptions.ScaleText(scale, u)}");
        }
        TitleBlock(dwg, spec, fmt, sheet);
        return sheet;
    }

    /// <summary>Numbers the sheets and fills the sheet-number field.</summary>
    public static List<Sheet> Number(List<Sheet> sheets, string prefix = "S")
    {
        for (int i = 0; i < sheets.Count; i++)
        {
            sheets[i].Number = $"{prefix}-{i + 1}";
            var tl = sheets[i].Drawing.Layer(SheetLayers.Title);
            for (int t = 0; t < tl.Texts.Count; t++)
                if (tl.Texts[t].Text == "{SHEET}") tl.Texts[t] = tl.Texts[t] with { Text = $"{sheets[i].Number}  ({i + 1} OF {sheets.Count})" };
        }
        return sheets;
    }

    private static void Place(PlanDrawing target, PlanDrawing src, Func<Vec2, Vec2> map, double k)
    {
        Vec3 M(Vec3 p) { var q = map(p.XY); return new Vec3(q.X, q.Y, 0); }
        foreach (var l in src.Layers)
        {
            var t = target.Layer(l.Style);
            foreach (var pl in l.Polylines) t.Polylines.Add(pl with { Points = pl.Points.Select(M).ToArray() });
            foreach (var c in l.Circles) t.Circles.Add(c with { Center = M(c.Center), Radius = c.Radius * k });
            foreach (var tx in l.Texts) t.Texts.Add(tx with { Position = M(tx.Position), Height = tx.Height * k });
        }
    }

    private static void TitleBlock(PlanDrawing dwg, SheetSpec spec, SheetFormat fmt, Sheet sheet)
    {
        var d = new Drafter(dwg, new UnitSystem(), 1);
        double P(double x) => x * In;
        double W = fmt.WidthIn, H = fmt.HeightIn;
        // Border.
        d.Rect(SheetLayers.Title, P(SheetFormat.Left), P(SheetFormat.Bottom), P(W - SheetFormat.Right), P(H - SheetFormat.Top));
        // Title block across the bottom.
        double y0 = SheetFormat.Bottom, y1 = SheetFormat.Bottom + SheetFormat.TitleBlockH;
        d.Line(SheetLayers.Title, new Vec2(P(SheetFormat.Left), P(y1)), new Vec2(P(W - SheetFormat.Right), P(y1)));
        double xA = W - SheetFormat.Right - 3.0, xB = xA - 6.5, xC = xB - 3.2;
        foreach (double x in new[] { xA, xB, xC }) d.Line(SheetLayers.Title, new Vec2(P(x), P(y0)), new Vec2(P(x), P(y1)));
        double ym = (y0 + y1) / 2;
        void T(string s, double x, double y, double h, TextAlign a = TextAlign.Left) => d.Text(s, new Vec2(P(x), P(y)), P(h), 0, a, SheetLayers.Title);
        // Sheet title (center block).
        T(spec.Title.ToUpperInvariant(), (xB + xA) / 2, ym + 0.2, 0.2, TextAlign.Center);
        T(spec.Subtitle, (xB + xA) / 2, ym - 0.12, 0.1, TextAlign.Center);
        T(fmt.Bridge.ToUpperInvariant(), (xB + xA) / 2, y0 + 0.17, 0.11, TextAlign.Center);
        // Sheet number block.
        T("SHEET", xA + 0.12, y1 - 0.15, 0.08);
        T("{SHEET}", xA + 1.5, ym - 0.05, 0.2, TextAlign.Center);
        T(fmt.Date, xA + 1.5, y0 + 0.15, 0.08, TextAlign.Center);
        // Project block.
        T("PROJECT", xC + 0.12, y1 - 0.15, 0.08);
        T(fmt.Project.ToUpperInvariant(), xC + 0.12, ym, 0.12);
        T("STEEL BRIDGE MODELER", xC + 0.12, y0 + 0.15, 0.07);
        // Scales / notes block.
        T("SCALES", SheetFormat.Left + 0.12, y1 - 0.15, 0.08);
        var notes = sheet.ScaleNotes.Distinct().Take(5).ToList();
        for (int i = 0; i < notes.Count; i++) T(notes[i], SheetFormat.Left + 0.12, y1 - 0.33 - i * 0.14, 0.08);
        T("GEOMETRY FROM THE PARAMETRIC MODEL.", SheetFormat.Left + 0.12, y0 + 0.2, 0.055);
        T("NOT FOR CONSTRUCTION UNLESS SEALED.", SheetFormat.Left + 0.12, y0 + 0.1, 0.055);
    }
}
