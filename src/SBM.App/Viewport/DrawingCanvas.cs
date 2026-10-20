using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SBM.Core.Drawing;
using SBM.Core.Geometry;

namespace SBM.App.Viewport;

/// <summary>
/// Renders a 2D <see cref="PlanDrawing"/> (sheet view) with pan (middle or left drag) and wheel zoom about the cursor.
/// Text follows the DXF convention used by the writer: vertically middle-aligned, left / center / right.
/// </summary>
public sealed class DrawingCanvas : FrameworkElement
{
    private PlanDrawing? _drawing;
    private double _scale = 1;          // pixels per model meter
    private Vector _offset;             // screen = (x * scale + ox, -y * scale + oy)
    private Point? _drag;
    private Vector _dragOffset;
    private readonly Dictionary<string, Pen> _pens = new();
    private readonly Dictionary<string, Brush> _brushes = new();
    private static readonly Typeface Face = new("Segoe UI");

    /// <summary>Paper mode: print colors (as in the PDF) on a white sheet.</summary>
    public bool Paper { get; set; }
    /// <summary>Sheet size in inches drawn as white paper (paper mode).</summary>
    public (double w, double h) PaperSize { get; set; } = (17, 11);

    public PlanDrawing? Drawing
    {
        get => _drawing;
        set { _drawing = value; _pens.Clear(); _brushes.Clear(); ZoomExtents(); }
    }

    public DrawingCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        SizeChanged += (_, _) => { if (_scale == 1) ZoomExtents(); else InvalidateVisual(); };
    }

    /// <summary>Zooms to a window given as fractions (0–1) of the drawing extents (x0, y0 bottom-left; x1, y1 top-right).</summary>
    public void ZoomWindow(double fx0, double fy0, double fx1, double fy1)
    {
        if (_drawing == null || ActualWidth < 2) return;
        var b = _drawing.Extents();
        double x0 = b.MinX + b.Width * fx0, x1 = b.MinX + b.Width * fx1, y0 = b.MinY + b.Height * fy0, y1 = b.MinY + b.Height * fy1;
        _scale = Math.Min(ActualWidth / (x1 - x0), ActualHeight / (y1 - y0));
        _offset = new Vector(ActualWidth / 2 - (x0 + x1) / 2 * _scale, ActualHeight / 2 + (y0 + y1) / 2 * _scale);
        InvalidateVisual();
    }

    /// <summary>Renders a drawing offscreen to PNG (optionally a window as fractions of the extents).</summary>
    public static void RenderToPng(PlanDrawing drawing, string path, int width, int height, double[]? window = null, bool paper = false)
    {
        var c = new DrawingCanvas { Width = width, Height = height, Paper = paper };
        c.Measure(new Size(width, height));
        c.Arrange(new Rect(0, 0, width, height));
        c._drawing = drawing;
        if (window is { Length: 4 }) c.ZoomWindow(window[0], window[1], window[2], window[3]);
        else c.ZoomExtents();
        c.UpdateLayout();
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(c);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using var fs = System.IO.File.Create(path);
        enc.Save(fs);
    }

    public void ZoomExtents()
    {
        if (_drawing == null || ActualWidth < 2 || ActualHeight < 2) { InvalidateVisual(); return; }
        var b = _drawing.Extents();
        if (Paper) { b = Box2.Empty; b.Add(new Vec2(0, 0)); b.Add(new Vec2(PaperSize.w * 0.0254, PaperSize.h * 0.0254)); }
        if (b.IsEmpty) { InvalidateVisual(); return; }
        double sx = ActualWidth * 0.94 / Math.Max(b.Width, 1e-6), sy = ActualHeight * 0.94 / Math.Max(b.Height, 1e-6);
        _scale = Math.Min(sx, sy);
        var c = b.Center;
        _offset = new Vector(ActualWidth / 2 - c.X * _scale, ActualHeight / 2 + c.Y * _scale);
        InvalidateVisual();
    }

    private Point S(Vec3 p) => new(p.X * _scale + _offset.X, -p.Y * _scale + _offset.Y);

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var m = e.GetPosition(this);
        double k = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _offset = new Vector(m.X - (m.X - _offset.X) * k, m.Y - (m.Y - _offset.Y) * k);
        _scale *= k;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2) { ZoomExtents(); return; }
        _drag = e.GetPosition(this);
        _dragOffset = _offset;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drag is { } d0)
        {
            _offset = _dragOffset + (e.GetPosition(this) - d0);
            InvalidateVisual();
        }
    }

    /// <summary>Click without drag (model coordinates): picking in projected views.</summary>
    public event Action<Vec2>? Clicked;

    /// <summary>Model point under a screen point.</summary>
    public Vec2 ToModel(Point p) => new((p.X - _offset.X) / _scale, -(p.Y - _offset.Y) / _scale);

    /// <summary>Model length of one screen pixel.</summary>
    public double PixelSize => 1 / _scale;

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_drag is { } d0 && (e.GetPosition(this) - d0).Length < 3 && e.ChangedButton == MouseButton.Left) Clicked?.Invoke(ToModel(e.GetPosition(this)));
        _drag = null;
        ReleaseMouseCapture();
    }

    private Pen PenFor(LayerStyle s)
    {
        if (_pens.TryGetValue(s.Name, out var p)) return p;
        var brush = BrushFor(s);
        p = new Pen(brush, Paper ? Math.Max(0.5, SBM.IO.Pdf.PdfWriter.PrintStyle(s).width * 2.2) : Math.Max(0.6, s.Weight * 0.9));
        p.DashStyle = s.LineStyle switch
        {
            LineStyle.Center => new DashStyle(new double[] { 12, 3, 3, 3 }, 0),
            LineStyle.Dashed => new DashStyle(new double[] { 6, 3 }, 0),
            LineStyle.Hidden => new DashStyle(new double[] { 3, 2.5 }, 0),
            _ => DashStyles.Solid,
        };
        p.Freeze();
        _pens[s.Name] = p;
        return p;
    }

    private Brush BrushFor(LayerStyle s)
    {
        if (_brushes.TryGetValue(s.Name, out var b)) return b;
        if (Paper)
        {
            var (r, g, bl, _, _) = SBM.IO.Pdf.PdfWriter.PrintStyle(s);
            b = new SolidColorBrush(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(bl * 255)));
        }
        else b = new SolidColorBrush(Color.FromRgb(s.R, s.G, s.B));
        b.Freeze();
        _brushes[s.Name] = b;
        return b;
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Paper ? Color.FromRgb(0x3A, 0x3C, 0x40) : Color.FromRgb(0x1B, 0x1D, 0x21)), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (_drawing == null) return;
        if (Paper)
        {
            var p0 = S(new Vec3(0, 0, 0));
            var p1 = S(new Vec3(PaperSize.w * 0.0254, PaperSize.h * 0.0254, 0));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null, new Rect(new Point(p0.X + 4, p1.Y + 4), new Point(p1.X + 4, p0.Y + 4)));
            dc.DrawRectangle(Brushes.White, null, new Rect(p0, p1));
        }
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var view = new Rect(-50, -50, ActualWidth + 100, ActualHeight + 100);
        foreach (var layer in _drawing.Layers)
        {
            var pen = PenFor(layer.Style);
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                foreach (var pl in layer.Polylines)
                {
                    if (pl.Points.Length < 2) continue;
                    g.BeginFigure(S(pl.Points[0]), false, pl.Closed);
                    for (int i = 1; i < pl.Points.Length; i++) g.LineTo(S(pl.Points[i]), true, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
            foreach (var c in layer.Circles)
            {
                double r = c.Radius * _scale;
                dc.DrawEllipse(c.Filled ? BrushFor(layer.Style) : null, pen, S(c.Center), r, r);
            }
            var brush = BrushFor(layer.Style);
            foreach (var t in layer.Texts)
            {
                double h = t.Height * _scale;
                if (h < 2.5) continue; // too small to read at this zoom
                var p = S(t.Position);
                if (!view.Contains(p)) continue;
                var ft = new FormattedText(t.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, h * 1.35, brush, dpi);
                double ang = SBM.IO.Dxf.DxfWriter.NormalizeTextAngle(t.Rotation);
                double ax = t.Align switch { TextAlign.Center => ft.Width / 2, TextAlign.Right => ft.Width, _ => 0 };
                dc.PushTransform(new RotateTransform(-ang, p.X, p.Y));
                dc.DrawText(ft, new Point(p.X - ax, p.Y - ft.Height / 2));
                dc.Pop();
            }
        }
    }
}
