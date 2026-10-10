using System.Globalization;
using System.IO.Compression;
using System.Text;
using SBM.Core.Drawing;
using SBM.Core.Drawing.Sheets;

namespace SBM.IO.Pdf;

/// <summary>
/// Minimal vector PDF writer for composed sheets (one page per sheet, page size from the sheet in inches). Lines,
/// polylines, circles and Helvetica text; layer colors are mapped to print colors (black / gray, colored layers
/// darkened); line types become dash patterns. Content streams are Flate-compressed.
/// </summary>
public static class PdfWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const double PtPerMeter = 72 / 0.0254;

    public static void Write(string path, IReadOnlyList<Sheet> sheets, string title = "")
    {
        using var fs = File.Create(path);
        Write(fs, sheets, title);
    }

    public static void Write(Stream output, IReadOnlyList<Sheet> sheets, string title = "")
    {
        var objects = new List<byte[]>();
        int Add(string s) { objects.Add(Encoding.Latin1.GetBytes(s)); return objects.Count; }
        int AddBytes(byte[] b) { objects.Add(b); return objects.Count; }

        int catalog = Add("");      // 1, filled later
        int pagesId = Add("");      // 2, filled later
        int font = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var pageIds = new List<int>();
        foreach (var sh in sheets)
        {
            byte[] content = Content(sh);
            byte[] packed;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(content);
                packed = ms.ToArray();
            }
            var head = Encoding.Latin1.GetBytes($"<< /Length {packed.Length} /Filter /FlateDecode >>\nstream\n");
            var tail = Encoding.Latin1.GetBytes("\nendstream");
            int contentId = AddBytes(head.Concat(packed).Concat(tail).ToArray());
            double w = sh.WidthIn * 72, h = sh.HeightIn * 72;
            pageIds.Add(Add($"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {F(w)} {F(h)}] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {contentId} 0 R >>"));
        }
        objects[catalog - 1] = Encoding.Latin1.GetBytes($"<< /Type /Catalog /Pages {pagesId} 0 R >>");
        objects[pagesId - 1] = Encoding.Latin1.GetBytes($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(i => $"{i} 0 R"))}] /Count {pageIds.Count} >>");
        int info = Add($"<< /Title ({Escape(title)}) /Producer (Steel Bridge Modeler) >>");

        var offsets = new List<long>();
        void W(string s) { var b = Encoding.Latin1.GetBytes(s); output.Write(b); }
        long pos = 0;
        var buf = new MemoryStream();
        void B(byte[] b) { buf.Write(b); }
        B(Encoding.Latin1.GetBytes("%PDF-1.4\n%âãÏÓ\n"));
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(buf.Position);
            B(Encoding.Latin1.GetBytes($"{i + 1} 0 obj\n"));
            B(objects[i]);
            B(Encoding.Latin1.GetBytes("\nendobj\n"));
        }
        long xref = buf.Position;
        var sb = new StringBuilder();
        sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append(o.ToString("D10", Inv)).Append(" 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root {catalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        B(Encoding.Latin1.GetBytes(sb.ToString()));
        buf.Position = 0;
        buf.CopyTo(output);
        _ = pos;
    }

    private static string F(double v) => v.ToString("0.###", Inv);

    /// <summary>Print color (0–1 RGB), line width (pt) and dash pattern for a layer.</summary>
    public static (double r, double g, double b, double width, string dash) PrintStyle(LayerStyle s)
    {
        double r = s.R / 255.0, g = s.G / 255.0, b = s.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        bool gray = max - min < 0.12;
        double light = s.LineStyle == LineStyle.Hidden || s.Name.Contains("HATCH") || s.Name.Contains("GRID") ? 0.5 : 0;
        if (gray) (r, g, b) = (light, light, light);
        else { r *= 0.55; g *= 0.55; b *= 0.55; }
        string dash = s.LineStyle switch
        {
            LineStyle.Center => "[9 2 2 2] 0 d",
            LineStyle.Dashed => "[5 2.5] 0 d",
            LineStyle.Hidden => "[2.5 1.8] 0 d",
            _ => "[] 0 d",
        };
        return (r, g, b, Math.Max(0.18, 0.3 * s.Weight), dash);
    }

    private static byte[] Content(Sheet sh)
    {
        var sb = new StringBuilder();
        sb.Append("1 J 1 j\n");
        foreach (var layer in sh.Drawing.Layers)
        {
            var (r, g, b, w, dash) = PrintStyle(layer.Style);
            string rgb = $"{F(r)} {F(g)} {F(b)}";
            sb.Append($"{rgb} RG {rgb} rg {F(w)} w {dash}\n");
            foreach (var pl in layer.Polylines)
            {
                if (pl.Points.Length < 2) continue;
                sb.Append($"{F(pl.Points[0].X * PtPerMeter)} {F(pl.Points[0].Y * PtPerMeter)} m");
                for (int i = 1; i < pl.Points.Length; i++) sb.Append($" {F(pl.Points[i].X * PtPerMeter)} {F(pl.Points[i].Y * PtPerMeter)} l");
                sb.Append(pl.Closed ? " s\n" : " S\n");
            }
            foreach (var c in layer.Circles)
            {
                double cx = c.Center.X * PtPerMeter, cy = c.Center.Y * PtPerMeter, rr = c.Radius * PtPerMeter, k = 0.5523 * rr;
                sb.Append($"{F(cx + rr)} {F(cy)} m {F(cx + rr)} {F(cy + k)} {F(cx + k)} {F(cy + rr)} {F(cx)} {F(cy + rr)} c " +
                          $"{F(cx - k)} {F(cy + rr)} {F(cx - rr)} {F(cy + k)} {F(cx - rr)} {F(cy)} c " +
                          $"{F(cx - rr)} {F(cy - k)} {F(cx - k)} {F(cy - rr)} {F(cx)} {F(cy - rr)} c " +
                          $"{F(cx + k)} {F(cy - rr)} {F(cx + rr)} {F(cy - k)} {F(cx + rr)} {F(cy)} c {(c.Filled ? "b" : "s")}\n");
            }
            foreach (var t in layer.Texts)
            {
                if (string.IsNullOrEmpty(t.Text)) continue;
                double size = t.Height * PtPerMeter / 0.718;     // text height = cap height
                double width = TextWidth(t.Text) * size / 1000;
                double ang = SBM.IO.Dxf.DxfWriter.NormalizeTextAngle(t.Rotation) * Math.PI / 180;
                double ox = t.Align switch { TextAlign.Center => -width / 2, TextAlign.Right => -width, _ => 0 };
                double oy = -0.359 * size;                      // middle-aligned
                double ca = Math.Cos(ang), sa = Math.Sin(ang);
                double x = t.Position.X * PtPerMeter + ox * ca - oy * sa, y = t.Position.Y * PtPerMeter + ox * sa + oy * ca;
                sb.Append($"BT /F1 {F(size)} Tf {F(ca)} {F(sa)} {F(-sa)} {F(ca)} {F(x)} {F(y)} Tm ({Escape(t.Text)}) Tj ET\n");
            }
        }
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s)
        {
            char c = Map(ch);
            if (c is '(' or ')' or '\\') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Unicode → WinAnsi (characters outside it become '?').</summary>
    private static char Map(char c) => c switch
    {
        < '\u0080' => c,
        '°' or '·' or '×' or '½' or '¼' or '¾' => c,
        '–' => (char)0x96,
        '—' => (char)0x97,
        '…' => (char)0x85,
        '’' => '\'',
        '−' => '-',
        '▲' or '▼' => '^',
        _ when c <= 'ÿ' => c,
        _ => '?',
    };

    private static readonly int[] Widths =
    {
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556,
        278, 278, 584, 584, 584, 556, 1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722, 667,
        611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, 333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833,
        556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    };

    /// <summary>Helvetica advance width in 1/1000 em.</summary>
    public static double TextWidth(string s)
    {
        double w = 0;
        foreach (char ch in s)
        {
            char c = Map(ch);
            w += c >= 32 && c <= 126 ? Widths[c - 32] : c switch { (char)0x96 => 556, (char)0x97 => 1000, (char)0x85 => 1000, '°' => 400, '·' => 278, '×' => 584, _ => 556 };
        }
        return w;
    }
}
