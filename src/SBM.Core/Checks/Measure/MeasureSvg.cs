using System.Globalization;
using System.Text;
using SBM.Core.Geometry;

namespace SBM.Core.Checks.Measure;

/// <summary>Minimal SVG writer for the measure drawings: panels in model meters (y up), each scaled to fit, stacked vertically with a title.</summary>
public sealed class MeasureSvg
{
    private sealed class Panel
    {
        public string Title = "";
        public Box2 Box = Box2.Empty;
        public readonly List<Action<StringBuilder, Func<Vec2, (double X, double Y)>, double>> Items = new();
    }

    private readonly List<Panel> _panels = new();
    private Panel Cur => _panels[^1];
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public double Width { get; set; } = 1100;
    public double MaxPanelHeight { get; set; } = 850;

    public void NewPanel(string title) => _panels.Add(new Panel { Title = title });

    private static string F(double v) => v.ToString("0.##", Inv);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    public void Polygon(IReadOnlyList<Vec2> pts, string stroke, string fill = "none", double width = 1.2, bool dashed = false, double fillOpacity = 0.25)
    {
        if (pts.Count < 2) return;
        foreach (var p in pts) Cur.Box.Add(p);
        var list = pts.ToList();
        Cur.Items.Add((sb, T, _) =>
        {
            sb.Append("<polygon points=\"");
            foreach (var p in list) { var (x, y) = T(p); sb.Append(F(x)).Append(',').Append(F(y)).Append(' '); }
            sb.Append($"\" fill=\"{fill}\" fill-opacity=\"{F(fillOpacity)}\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\"{(dashed ? " stroke-dasharray=\"6,4\"" : "")}/>\n");
        });
    }

    /// <summary>A filled region with holes (even-odd path).</summary>
    public void Region(IReadOnlyList<Vec2> outer, IEnumerable<IReadOnlyList<Vec2>> holes, string stroke, string fill, double fillOpacity = 0.25, double width = 1.2, string? hatch = null)
    {
        if (outer.Count < 3) return;
        foreach (var p in outer) Cur.Box.Add(p);
        var loops = new List<List<Vec2>> { outer.ToList() };
        loops.AddRange(holes.Where(h => h.Count >= 3).Select(h => h.ToList()));
        Cur.Items.Add((sb, T, _) =>
        {
            sb.Append("<path d=\"");
            foreach (var l in loops)
            {
                for (int i = 0; i < l.Count; i++) { var (x, y) = T(l[i]); sb.Append(i == 0 ? "M" : "L").Append(F(x)).Append(',').Append(F(y)).Append(' '); }
                sb.Append("Z ");
            }
            string f = hatch != null ? $"url(#{hatch})" : fill;
            sb.Append($"\" fill=\"{f}\" fill-opacity=\"{F(hatch != null ? 1 : fillOpacity)}\" fill-rule=\"evenodd\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\"/>\n");
        });
    }

    public void Line(Vec2 a, Vec2 b, string stroke, double width = 1, bool dashed = false)
    {
        Cur.Box.Add(a); Cur.Box.Add(b);
        Cur.Items.Add((sb, T, _) =>
        {
            var (x1, y1) = T(a); var (x2, y2) = T(b);
            sb.Append($"<line x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\"{(dashed ? " stroke-dasharray=\"5,3\"" : "")}/>\n");
        });
    }

    public void Polyline(IReadOnlyList<Vec2> pts, string stroke, double width = 1.5)
    {
        if (pts.Count < 2) return;
        foreach (var p in pts) Cur.Box.Add(p);
        var list = pts.ToList();
        Cur.Items.Add((sb, T, _) =>
        {
            sb.Append("<polyline points=\"");
            foreach (var p in list) { var (x, y) = T(p); sb.Append(F(x)).Append(',').Append(F(y)).Append(' '); }
            sb.Append($"\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\"/>\n");
        });
    }

    /// <summary>Circle of a model radius (at least <paramref name="minPx"/> px).</summary>
    public void Circle(Vec2 c, double r, string stroke, string fill = "none", double minPx = 1.5, double width = 1)
    {
        Cur.Box.Add(c + new Vec2(r, r)); Cur.Box.Add(c - new Vec2(r, r));
        Cur.Items.Add((sb, T, s) =>
        {
            var (x, y) = T(c);
            sb.Append($"<circle cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"{F(Math.Max(minPx, r * s))}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\"/>\n");
        });
    }

    public void Cross(Vec2 c, double sizePx, string stroke)
    {
        Cur.Box.Add(c);
        Cur.Items.Add((sb, T, _) =>
        {
            var (x, y) = T(c);
            sb.Append($"<path d=\"M{F(x - sizePx)},{F(y)} L{F(x + sizePx)},{F(y)} M{F(x)},{F(y - sizePx)} L{F(x)},{F(y + sizePx)}\" stroke=\"{stroke}\" stroke-width=\"1.2\"/>\n");
        });
    }

    public void Text(Vec2 at, string text, double sizePx = 11, string color = "#222", string anchor = "middle", double dxPx = 0, double dyPx = 0, double rotateDeg = 0)
    {
        Cur.Items.Add((sb, T, _) =>
        {
            var (x, y) = T(at);
            x += dxPx; y += dyPx;
            string rot = Math.Abs(rotateDeg) > 1e-6 ? $" transform=\"rotate({F(-rotateDeg)} {F(x)} {F(y)})\"" : "";
            sb.Append($"<text x=\"{F(x)}\" y=\"{F(y)}\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"{F(sizePx)}\" fill=\"{color}\" text-anchor=\"{anchor}\" dominant-baseline=\"middle\"{rot}>{Esc(text)}</text>\n");
        });
    }

    /// <summary>Aligned dimension a-b offset by <paramref name="offsetPx"/> (positive = left of a->b) with the text.</summary>
    public void Dim(Vec2 a, Vec2 b, string text, double offsetPx = 18, string color = "#0a5")
    {
        Cur.Box.Add(a); Cur.Box.Add(b);
        Cur.Items.Add((sb, T, _) =>
        {
            var (x1, y1) = T(a); var (x2, y2) = T(b);
            double dx = x2 - x1, dy = y2 - y1, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1) return;
            double nx = dy / l * offsetPx, ny = -dx / l * offsetPx;   // screen y is down
            double ax = x1 + nx, ay = y1 + ny, bx = x2 + nx, by = y2 + ny;
            sb.Append($"<path d=\"M{F(x1)},{F(y1)} L{F(ax)},{F(ay)} M{F(x2)},{F(y2)} L{F(bx)},{F(by)} M{F(ax)},{F(ay)} L{F(bx)},{F(by)}\" stroke=\"{color}\" stroke-width=\"0.7\" fill=\"none\"/>\n");
            double ang = Math.Atan2(by - ay, bx - ax) * 180 / Math.PI;
            if (ang > 90) ang -= 180; if (ang < -90) ang += 180;
            double mx = (ax + bx) / 2 + nx / offsetPx * 7, my = (ay + by) / 2 + ny / offsetPx * 7;
            sb.Append($"<text x=\"{F(mx)}\" y=\"{F(my)}\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"10\" fill=\"{color}\" text-anchor=\"middle\" dominant-baseline=\"middle\" transform=\"rotate({F(ang)} {F(mx)} {F(my)})\">{Esc(text)}</text>\n");
        });
    }

    public void Note(string text) => Cur.Title += "\n" + text;

    public string Render()
    {
        var sb = new StringBuilder();
        double y0 = 10;
        var body = new StringBuilder();
        foreach (var p in _panels)
        {
            if (p.Box.IsEmpty) p.Box.Add(Vec2.Zero);
            var titleLines = p.Title.Split('\n');
            double margin = 60;
            double bw = Math.Max(p.Box.Width, 1e-6), bh = Math.Max(p.Box.Height, 1e-6);
            double s = Math.Min((Width - 2 * margin) / bw, (MaxPanelHeight - 2 * margin) / bh);
            double ph = bh * s + 2 * margin;
            double top = y0 + titleLines.Length * 16 + 4;
            foreach (var (t, i) in titleLines.Select((t, i) => (t, i)))
                body.Append($"<text x=\"12\" y=\"{F(y0 + 12 + 16 * i)}\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"{(i == 0 ? 14 : 11)}\" font-weight=\"{(i == 0 ? "bold" : "normal")}\" fill=\"#111\">{Esc(t)}</text>\n");
            var box = p.Box;
            (double, double) T(Vec2 q) => (margin + (q.X - box.MinX) * s, top + margin + (box.MaxY - q.Y) * s);
            body.Append($"<rect x=\"4\" y=\"{F(top)}\" width=\"{F(Width - 8)}\" height=\"{F(ph)}\" fill=\"none\" stroke=\"#ccc\"/>\n");
            foreach (var it in p.Items) it(body, T, s);
            // scale bar: a round length near 1/5 of the width
            double target = bw / 5, bar = Math.Pow(10, Math.Floor(Math.Log10(target / 0.0254))) * 0.0254;
            foreach (double k in new[] { 2, 5, 10 }) if (bar * k <= target) { bar *= k; break; }
            double sx = margin, sy = top + ph - 15;
            body.Append($"<path d=\"M{F(sx)},{F(sy)} L{F(sx + bar * s)},{F(sy)}\" stroke=\"#333\" stroke-width=\"2\"/><text x=\"{F(sx + bar * s + 6)}\" y=\"{F(sy)}\" font-family=\"Arial\" font-size=\"10\" dominant-baseline=\"middle\">{Esc(Fmt.FtIn(bar))}</text>\n");
            y0 = top + ph + 14;
        }
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(Width)}\" height=\"{F(y0)}\" viewBox=\"0 0 {F(Width)} {F(y0)}\">\n");
        sb.Append("<defs><pattern id=\"hatch\" width=\"6\" height=\"6\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\"><line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"#8a7f6a\" stroke-width=\"1\"/></pattern>" +
                  "<pattern id=\"hatchsteel\" width=\"4\" height=\"4\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(-45)\"><line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"4\" stroke=\"#2c5fa8\" stroke-width=\"1\"/></pattern></defs>\n");
        sb.Append($"<rect width=\"100%\" height=\"100%\" fill=\"white\"/>\n");
        sb.Append(body);
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    public static readonly string[] Palette = { "#1f77b4", "#d62728", "#2ca02c", "#9467bd", "#ff7f0e", "#8c564b", "#e377c2", "#17becf", "#bcbd22", "#7f7f7f" };

    // ------------------------------------------------------------ drawings of the results

    public static string Plate(PlateResult r)
    {
        var svg = new MeasureSvg();
        svg.NewPanel($"{r.Mark} ({r.Assembly}) - {r.Description}");
        svg.Note($"t = {r.Thickness.Text}, {r.Width.Text} (u) x {r.Height.Text} (v); u = {r.Reference}; {r.Holes.Count} holes; source: {r.Source}");
        if (r.Shape is not { } s) return svg.Render();
        svg.Region(s.Outer, s.Inner, "#1f4e9c", "#9ec1f0", 0.35, 1.4);
        foreach (var piece in s.OtherPieces) svg.Polygon(piece, "#999", "#ddd", 1, true);
        foreach (var h in r.Holes) svg.Cross(h.C, 4, "#c00");
        // overall dimensions
        double w = s.Outer.Max(p => p.X), h2 = s.Outer.Max(p => p.Y);
        svg.Dim(new Vec2(0, 0), new Vec2(w, 0), r.Width.Text, -26);
        svg.Dim(new Vec2(0, 0), new Vec2(0, h2), r.Height.Text, 26);
        // edge lengths
        foreach (var e in r.Edges.Where(e => e.Type == "line" && e.Length.In > 0.5))
        {
            var a = new Vec2(e.From[0], e.From[1]) * Scale(r); var b = new Vec2(e.To[0], e.To[1]) * Scale(r);
            svg.Text((a + b) * 0.5, $"{e.No}: {e.Length.Text}", 9, "#555", "middle", 0, 0, Math.Abs(e.AngleDeg % 180) is var aa && aa > 90 ? aa - 180 : aa);
        }
        foreach (var f in r.Features.Where(f => f.At != null)) svg.Text(new Vec2(f.At![0], f.At[1]) * Scale(r), f.Kind, 9, "#a50", "middle", 0, 12);
        // hole groups: spacing text near the first hole of each group
        foreach (var g in r.HoleGroups)
        {
            var at = new Vec2(g.Min[0], g.Max[1]) * Scale(r);
            svg.Text(at, $"G{g.Group}: {g.Rows}x{g.Columns}  u {g.SpacingU}  v {g.SpacingV}", 9, "#c00", "start", 0, -12);
            string ed = string.Join("  ", g.EdgeDistance.Select(kv => $"{kv.Key} {kv.Value.Text}"));
            svg.Text(at, "edge " + ed, 9, "#c00", "start", 0, -24);
        }
        svg.Text(new Vec2(0, 0), "origin (u, v) = 0, 0", 9, "#555", "start", 4, 12);
        return svg.Render();
    }

    /// <summary>Short display units back to meters (the result lists coordinates in in / mm).</summary>
    private static double Scale(PlateResult r) => r.Units == "mm" ? 0.001 : 0.0254;

    public static string Assembly(AssemblyResult r)
    {
        var svg = new MeasureSvg();
        double sc = r.Units == "mm" ? 0.001 : 0.0254;
        foreach (var p in r.Planes)
        {
            svg.NewPanel($"{r.Mark} plane {p.No}: {p.Name}");
            svg.Note($"n = ({p.Normal[0]:0.###}, {p.Normal[1]:0.###}, {p.Normal[2]:0.###}), u = {p.Reference}; {p.Bolts.Count} bolts" +
                     (p.BoltsPerSide.Count > 0 ? " (" + string.Join(", ", p.BoltsPerSide.Select(kv => $"{kv.Value} {kv.Key}")) + ")" : "") +
                     (p.JointGaps.Count > 0 ? "; joint gap " + string.Join(", ", p.JointGaps.Select(j => j.Gap.Text)) : "") +
                     (p.StackGaps.Count > 0 ? "; stack gaps " + string.Join(", ", p.StackGaps.Select(g => $"{g.Lower}/{g.Upper} {g.Gap.Text}")) : ""));
            int ci = 0;
            foreach (var (mark, nb, shape, segs) in p.Drawing)
            {
                string col = nb ? "#888" : Palette[ci++ % Palette.Length];
                if (shape is { Outer.Count: >= 3 })
                {
                    svg.Region(shape.Outer, shape.Inner, col, col, 0.12, 1.3);
                    var c = Geo2.Centroid(shape.Outer);
                    svg.Text(c, mark, 10, col, "middle", 0, 0);
                }
                if (segs != null)
                {
                    foreach (var (a, b) in segs) svg.Line(a, b, col, 1, true);
                    if (segs.Count > 0) svg.Text(segs[0].A, mark, 9, col, "start", 3, -8);
                }
            }
            foreach (var (at, d) in p.BoltPts) svg.Circle(at, d / 2, "#000", "#333", 2, 0.8);
            foreach (var j in p.JointGaps) svg.Line(new Vec2(j.AtA[0], j.AtA[1]) * sc, new Vec2(j.AtB[0], j.AtB[1]) * sc, "#d00", 2);
            foreach (var g in p.BoltGroups)
                svg.Text(new Vec2(g.Min[0], g.Max[1]) * sc, $"{g.Holes} = {g.Rows}x{g.Columns}  u {g.SpacingU}  v {g.SpacingV}", 9, "#000", "start", 0, -10);
        }
        return svg.Render();
    }
}
