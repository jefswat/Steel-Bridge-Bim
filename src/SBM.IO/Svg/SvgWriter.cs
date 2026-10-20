using System.Globalization;
using System.Text;
using SBM.Core.Drawing;

namespace SBM.IO.Svg;

/// <summary>
/// Minimal SVG writer for a <see cref="PlanDrawing"/> (model coordinates, y up -> flipped): one &lt;g&gt; per layer (id = layer name), polylines
/// with print colors / dashes (<see cref="Pdf.PdfWriter.PrintStyle"/>), element tags as data-tag attributes (projected views), texts, circles.
/// </summary>
public static class SvgWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(string path, PlanDrawing drawing, double widthPx = 2400, bool white = true)
    {
        var e = drawing.Extents();
        if (e.IsEmpty) e = new SBM.Core.Geometry.Box2 { MinX = 0, MinY = 0, MaxX = 1, MaxY = 1 };
        double pad = Math.Max(e.Width, e.Height) * 0.02;
        double w = e.Width + 2 * pad, h = e.Height + 2 * pad;
        double k = widthPx / Math.Max(w, 1e-9);
        string F(double v) => v.ToString("0.###", Inv);
        string X(double x) => F((x - e.MinX + pad) * k);
        string Y(double y) => F((e.MaxY + pad - y) * k);
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(w * k)}\" height=\"{F(h * k)}\" viewBox=\"0 0 {F(w * k)} {F(h * k)}\">\n");
        if (white) sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>\n");
        foreach (var l in drawing.Layers)
        {
            var (r, g, b, lw, _) = Pdf.PdfWriter.PrintStyle(l.Style);
            string col = $"rgb({(int)(r * 255)},{(int)(g * 255)},{(int)(b * 255)})";
            string dash = l.Style.LineStyle switch { LineStyle.Hidden => " stroke-dasharray=\"4 3\"", LineStyle.Dashed => " stroke-dasharray=\"8 4\"", LineStyle.Center => " stroke-dasharray=\"14 3 3 3\"", _ => "" };
            sb.Append($"<g id=\"{Esc(l.Name)}\" stroke=\"{col}\" stroke-width=\"{F(Math.Max(0.6, lw * 2.5))}\" fill=\"none\"{dash}>\n");
            foreach (var pl in l.Polylines)
            {
                if (pl.Points.Length < 2) continue;
                sb.Append(pl.Closed ? "<polygon points=\"" : "<polyline points=\"");
                foreach (var p in pl.Points) sb.Append(X(p.X)).Append(',').Append(Y(p.Y)).Append(' ');
                sb.Append('"');
                if (pl.Tag != null) sb.Append($" data-tag=\"{Esc(pl.Tag)}\"");
                sb.Append("/>\n");
            }
            foreach (var c in l.Circles)
                sb.Append($"<circle cx=\"{X(c.Center.X)}\" cy=\"{Y(c.Center.Y)}\" r=\"{F(c.Radius * k)}\"{(c.Filled ? $" fill=\"{col}\"" : "")}/>\n");
            foreach (var t in l.Texts)
            {
                string anchor = t.Align switch { TextAlign.Left => "start", TextAlign.Right => "end", _ => "middle" };
                double deg = -t.Rotation * 180 / Math.PI;
                sb.Append($"<text x=\"{X(t.Position.X)}\" y=\"{Y(t.Position.Y)}\" font-family=\"Helvetica,Arial\" font-size=\"{F(t.Height * k * 1.35)}\" fill=\"{col}\" stroke=\"none\" " +
                          $"text-anchor=\"{anchor}\" dominant-baseline=\"middle\" transform=\"rotate({F(deg)} {X(t.Position.X)} {Y(t.Position.Y)})\">{Esc(t.Text)}</text>\n");
            }
            sb.Append("</g>\n");
        }
        sb.Append("</svg>\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
