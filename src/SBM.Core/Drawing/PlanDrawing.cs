using SBM.Core.Geometry;

namespace SBM.Core.Drawing;

public enum LineStyle { Continuous, Center, Dashed, Hidden }

public enum TextAlign { Left, Center, Right }

public sealed record Polyline3(Vec3[] Points, bool Closed = false);

public sealed record TextItem(Vec3 Position, string Text, double Height, double Rotation, TextAlign Align = TextAlign.Center);

public sealed record CircleItem(Vec3 Center, double Radius, bool Filled = false);

/// <summary>Display style for a layer: RGB for screen, AutoCAD color index for DXF.</summary>
public sealed record LayerStyle(string Name, byte R, byte G, byte B, int Aci, LineStyle LineStyle = LineStyle.Continuous, double Weight = 1.0);

public sealed class DrawingLayer
{
    public required LayerStyle Style { get; init; }
    public List<Polyline3> Polylines { get; } = new();
    public List<TextItem> Texts { get; } = new();
    public List<CircleItem> Circles { get; } = new();
    public string Name => Style.Name;

    public void Line(Vec3 a, Vec3 b) => Polylines.Add(new Polyline3(new[] { a, b }));
    public void Poly(IEnumerable<Vec3> pts, bool closed = false) => Polylines.Add(new Polyline3(pts.ToArray(), closed));
}

/// <summary>
/// Renderer-neutral 2D/3D linework (one per view). Consumed by the GPU viewport and by the DXF writer so the
/// screen and the exported file always match.
/// </summary>
public sealed class PlanDrawing
{
    private readonly Dictionary<string, DrawingLayer> _layers = new();
    public IEnumerable<DrawingLayer> Layers => _layers.Values;

    public DrawingLayer Layer(LayerStyle style)
    {
        if (!_layers.TryGetValue(style.Name, out var l))
        {
            l = new DrawingLayer { Style = style };
            _layers[style.Name] = l;
        }
        return l;
    }

    /// <summary>Mirrors everything about the vertical line X = <paramref name="x"/>; text stays readable.</summary>
    public void MirrorX(double x = 0)
    {
        Vec3 M(Vec3 p) => new(2 * x - p.X, p.Y, p.Z);
        foreach (var l in _layers.Values)
        {
            for (int i = 0; i < l.Polylines.Count; i++) l.Polylines[i] = l.Polylines[i] with { Points = l.Polylines[i].Points.Select(M).ToArray() };
            for (int i = 0; i < l.Circles.Count; i++) l.Circles[i] = l.Circles[i] with { Center = M(l.Circles[i].Center) };
            for (int i = 0; i < l.Texts.Count; i++)
            {
                var t = l.Texts[i];
                var align = t.Align switch { TextAlign.Left => TextAlign.Right, TextAlign.Right => TextAlign.Left, _ => TextAlign.Center };
                l.Texts[i] = t with { Position = M(t.Position), Rotation = -t.Rotation, Align = align };
            }
        }
    }

    public Box2 Extents()
    {
        var b = Box2.Empty;
        foreach (var l in _layers.Values)
        {
            foreach (var p in l.Polylines)
                foreach (var v in p.Points) b.Add(v.XY);
            foreach (var c in l.Circles) { b.Add(c.Center.XY + new Vec2(c.Radius, c.Radius)); b.Add(c.Center.XY - new Vec2(c.Radius, c.Radius)); }
            foreach (var t in l.Texts)
            {
                // Approximate text box (0.62 h per character), honoring alignment and rotation.
                double w = t.Text.Length * t.Height * 0.62;
                double x0 = t.Align switch { TextAlign.Center => -w / 2, TextAlign.Right => -w, _ => 0 };
                var dir = Vec2.FromAngle(t.Rotation);
                var up = dir.Left;
                foreach (var (x, y) in new[] { (x0, -t.Height / 2), (x0 + w, -t.Height / 2), (x0, t.Height / 2), (x0 + w, t.Height / 2) })
                    b.Add(t.Position.XY + dir * x + up * y);
            }
        }
        return b;
    }
}

/// <summary>Standard layer set. Colors chosen to read well on a dark CAD background.</summary>
public static class Layers
{
    public static readonly LayerStyle Alignment = new("SBM-ALIGNMENT", 255, 80, 80, 1, LineStyle.Center);
    public static readonly LayerStyle AlignmentTicks = new("SBM-ALIGNMENT-TICKS", 255, 80, 80, 1);
    public static readonly LayerStyle AlignmentText = new("SBM-ALIGNMENT-TEXT", 255, 140, 140, 1);
    public static readonly LayerStyle Deck = new("SBM-DECK-EDGE", 200, 200, 200, 8, LineStyle.Continuous, 1.5);
    public static readonly LayerStyle Barrier = new("SBM-BARRIER", 160, 160, 160, 9);
    public static readonly LayerStyle SupportLine = new("SBM-SUPPORT-CL", 255, 255, 0, 2, LineStyle.Center);
    public static readonly LayerStyle Bearing = new("SBM-BEARING", 255, 200, 0, 30);
    public static readonly LayerStyle GirderWeb = new("SBM-GIRDER-CL", 0, 200, 255, 4, LineStyle.Center);
    public static readonly LayerStyle GirderFlange = new("SBM-GIRDER-FLANGE", 0, 140, 255, 5, LineStyle.Continuous, 1.5);
    public static readonly LayerStyle CrossFrame = new("SBM-CROSSFRAME", 0, 255, 120, 3, LineStyle.Continuous, 1.5);
    public static readonly LayerStyle Stiffener = new("SBM-STIFFENER", 255, 0, 255, 6);
    public static readonly LayerStyle FieldSplice = new("SBM-FIELD-SPLICE", 255, 128, 0, 30, LineStyle.Continuous, 2);
    public static readonly LayerStyle Labels = new("SBM-LABELS", 230, 230, 230, 7);
    public static readonly LayerStyle SubCap = new("SBM-SUB-CAP", 210, 190, 150, 42, LineStyle.Continuous, 1.5);
    public static readonly LayerStyle SubPedestal = new("SBM-SUB-PEDESTAL", 230, 170, 110, 32);
    public static readonly LayerStyle SubColumn = new("SBM-SUB-COLUMN", 190, 160, 120, 33, LineStyle.Hidden);
    public static readonly LayerStyle SubFoundation = new("SBM-SUB-FOUNDATION", 150, 130, 100, 35, LineStyle.Hidden);
}
