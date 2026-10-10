using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.App.Editors;

/// <summary>
/// Draws a bar bending diagram: the bar to scale (centerline, width = bar diameter), out-to-out dimensions with
/// extension lines and arrows, pin diameter, and for variable-length marks the list of lengths.
/// </summary>
public sealed class BendDiagramView : FrameworkElement
{
    public static readonly DependencyProperty MarkProperty = DependencyProperty.Register(nameof(Mark), typeof(BarMark), typeof(BendDiagramView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public BarMark? Mark
    {
        get => (BarMark?)GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    public UnitSystem Units { get; set; } = new();

    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(230, 230, 230)));
    private static readonly Brush DimInk = Freeze(new SolidColorBrush(Color.FromRgb(120, 190, 255)));
    private static readonly Brush BarInk = Freeze(new SolidColorBrush(Color.FromRgb(110, 200, 110)));
    private static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(150, 150, 150)));
    private static Brush Freeze(Brush b) { b.Freeze(); return b; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 32)), null, new Rect(0, 0, ActualWidth, ActualHeight));
        var m = Mark;
        if (m == null)
        {
            Text(dc, "Select a bar mark", new Point(20, 20), 14, Muted);
            return;
        }
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        string title = $"BARS {m.Mark} ({m.Size.Name})" + (m.Epoxy ? "  — epoxy coated" : "");
        Text(dc, title, new Point(20, 14), 18, Ink, bold: true);
        Text(dc, $"{m.Count} required   ·   {m.Shape.Description}   ·   {m.Location}", new Point(20, 42), 12, Muted);

        var shape = m.Shape;
        var pts = shape.Centerline;
        bool straight = shape.Code == "STR";
        double margin = 70;
        var area = new Rect(margin, 90, Math.Max(10, ActualWidth - 2 * margin), Math.Max(10, ActualHeight - 90 - (m.Varies ? 150 : 70)));

        // Straight bars are drawn at a fixed nominal length so the diagram reads clearly.
        Vec2[] drawPts = straight ? new[] { new Vec2(0, 0), new Vec2(1, 0) } : pts;
        double minX = drawPts.Min(p => p.X), maxX = drawPts.Max(p => p.X), minY = drawPts.Min(p => p.Y), maxY = drawPts.Max(p => p.Y);
        double w = Math.Max(maxX - minX, 1e-6), h = Math.Max(maxY - minY, 1e-6);
        double scale = straight ? area.Width / w : Math.Min(area.Width / w, area.Height / h) * 0.85;
        double cx = area.X + area.Width / 2, cy = area.Y + area.Height / 2;
        Point P(Vec2 v) => new(cx + (v.X - (minX + maxX) / 2) * scale, cy - (v.Y - (minY + maxY) / 2) * scale);

        double thick = straight ? 6 : Math.Max(3, m.Size.Diameter * scale);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(P(drawPts[0]), false, false);
            g.PolyLineTo(drawPts.Skip(1).Select(P).ToList(), true, true);
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(BarInk, thick) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, geo);

        // Dimensions.
        var pen = new Pen(DimInk, 1);
        if (straight)
        {
            string len = m.Varies ? $"Varies {Units.FormatLength(m.MinLength)} to {Units.FormatLength(m.Length)}" : Units.FormatLength(m.Length);
            Dimension(dc, pen, P(drawPts[0]), P(drawPts[1]), new Vector(0, -1), 34, $"A = {len}");
        }
        else
        {
            foreach (var d in shape.Dimensions)
            {
                if (Vec2.Distance(d.From, d.To) < 1e-9) continue; // listed below (e.g. spiral height and pitch)
                var n = new Vector(d.Normal.X, -d.Normal.Y);
                Dimension(dc, pen, P(d.From), P(d.To), n, 30, $"{d.Letter} = {Units.FormatLength(d.Value, LengthKind.Short)}");
            }
        }

        double y = area.Bottom + 18;
        Text(dc, $"Developed length {Units.FormatLength(m.Length)}" + (m.Varies ? " (max)" : "") +
                 (shape.PinDiameter > 0 ? $"   ·   Pin diameter {Units.FormatLength(shape.PinDiameter, LengthKind.Short)}" : "") +
                 $"   ·   Weight {m.TotalMass * (Units.IsUS ? 2.20462 : 1):N0} {(Units.IsUS ? "lb" : "kg")}",
            new Point(20, y), 12, Ink);
        var listed = shape.Dimensions.Where(d => Vec2.Distance(d.From, d.To) < 1e-9).Select(d => $"{d.Letter} = {Units.FormatLength(d.Value, LengthKind.Short)}").ToList();
        Text(dc, "Dimensions are out-to-out of bar." + (listed.Count > 0 ? "   " + string.Join(",  ", listed) + (shape.Code == "SPIRAL" ? "  (H = height, P = pitch)" : "") : ""),
            new Point(20, y + 20), 11, Muted);
        if (m.Varies)
        {
            var lens = m.VaryingLengths!.GroupBy(x => x).OrderByDescending(g => g.Key).Select(g => $"{g.Count()} @ {Units.FormatLength(g.Key)}");
            Text(dc, "Lengths: " + string.Join(",  ", lens), new Point(20, y + 44), 11, Ink, maxWidth: ActualWidth - 40);
        }
    }

    private void Dimension(DrawingContext dc, Pen pen, Point a, Point b, Vector normal, double offset, string label)
    {
        normal.Normalize();
        var a2 = a + normal * offset;
        var b2 = b + normal * offset;
        dc.DrawLine(pen, a + normal * 4, a2 + normal * 6);
        dc.DrawLine(pen, b + normal * 4, b2 + normal * 6);
        dc.DrawLine(pen, a2, b2);
        var dir = b2 - a2;
        if (dir.Length < 1) return;
        dir.Normalize();
        var perp = new Vector(-dir.Y, dir.X);
        foreach (var (p, s) in new[] { (a2, 1.0), (b2, -1.0) })
        {
            var tip = p;
            var back = p + dir * 9 * s;
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(tip, true, true);
                g.LineTo(back + perp * 3.5, true, false);
                g.LineTo(back - perp * 3.5, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(DimInk, null, geo);
        }
        var mid = a2 + (b2 - a2) / 2 + normal * 12;
        var ft = Format(label, 13, DimInk, false);
        bool vertical = Math.Abs(dir.Y) > Math.Abs(dir.X);
        if (vertical)
        {
            dc.PushTransform(new RotateTransform(-90, mid.X, mid.Y));
            dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2));
            dc.Pop();
        }
        else dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2));
    }

    private FormattedText Format(string s, double size, Brush brush, bool bold) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            bold ? new Typeface(Face.FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal) : Face, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private void Text(DrawingContext dc, string s, Point p, double size, Brush brush, bool bold = false, double maxWidth = 0)
    {
        var ft = Format(s, size, brush, bold);
        if (maxWidth > 0) ft.MaxTextWidth = maxWidth;
        dc.DrawText(ft, p);
    }
}
