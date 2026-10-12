using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>Layer set for 2D views (plan/elevation/section models). ACI colors read on both black and white backgrounds.</summary>
public static class SheetLayers
{
    public static readonly LayerStyle ConcreteCut = new("S-CONC-CUT", 230, 230, 230, 7, LineStyle.Continuous, 2);
    public static readonly LayerStyle Concrete = new("S-CONC", 200, 200, 200, 7);
    public static readonly LayerStyle ConcreteHidden = new("S-CONC-HIDDEN", 150, 150, 150, 8, LineStyle.Hidden);
    public static readonly LayerStyle Hatch = new("S-HATCH", 120, 120, 120, 8, LineStyle.Continuous, 0.5);
    public static readonly LayerStyle Steel = new("S-STEEL", 90, 160, 255, 5, LineStyle.Continuous, 1.5);
    public static readonly LayerStyle SteelHidden = new("S-STEEL-HIDDEN", 90, 140, 220, 5, LineStyle.Hidden);
    public static readonly LayerStyle Bearing = new("S-BEARING", 255, 200, 0, 2);
    public static readonly LayerStyle Rebar = new("S-REBAR", 255, 90, 90, 1);
    public static readonly LayerStyle Center = new("S-CENTER", 255, 120, 120, 1, LineStyle.Center);
    public static readonly LayerStyle Dims = new("S-DIMS", 0, 200, 120, 3, LineStyle.Continuous, 0.5);
    public static readonly LayerStyle Text = new("S-TEXT", 240, 240, 240, 7);
    /// <summary>Placed note blocks (lists, wrapped notes): the label placer keeps them fixed and moves other labels around them.</summary>
    public static readonly LayerStyle Notes = new("S-NOTES", 240, 240, 240, 7);
    public static readonly LayerStyle Title = new("S-TITLE", 255, 255, 255, 7, LineStyle.Continuous, 2);
    public static readonly LayerStyle Grid = new("S-GRID", 90, 90, 90, 9, LineStyle.Continuous, 0.25);
    public static readonly LayerStyle Ground = new("S-GROUND", 190, 150, 90, 42, LineStyle.Dashed);
    public static readonly LayerStyle Grade = new("S-GRADE", 255, 120, 120, 1, LineStyle.Center);
    public static readonly LayerStyle Barrier = new("S-BARRIER", 200, 200, 200, 7);
}

/// <summary>
/// Drafting primitives in model units (meters): text, dimensions with architectural ticks, leaders, hatching.
/// Paper sizes are given in paper inches and multiplied by the view's scale factor (model length per paper length).
/// All text is vertically middle-aligned at its position (left, center or right horizontally).
/// </summary>
public sealed class Drafter
{
    public PlanDrawing Drawing { get; }
    public UnitSystem Units { get; }
    /// <summary>Model length per paper length (e.g. 240 for 1" = 20'-0").</summary>
    public double Scale { get; }

    public Drafter(PlanDrawing drawing, UnitSystem units, double scale)
    {
        Drawing = drawing;
        Units = units;
        Scale = scale;
    }

    /// <summary>Paper inches → model meters.</summary>
    public double Paper(double inches) => inches * 0.0254 * Scale;
    public double TextH => Paper(0.09);
    public double SmallTextH => Paper(0.075);
    public double TitleH => Paper(0.16);

    public DrawingLayer L(LayerStyle s) => Drawing.Layer(s);

    public static Vec3 V(double x, double y) => new(x, y, 0);
    public static Vec3 V(Vec2 p) => new(p.X, p.Y, 0);

    public void Line(LayerStyle s, Vec2 a, Vec2 b) => L(s).Line(V(a), V(b));
    public void Poly(LayerStyle s, IEnumerable<Vec2> pts, bool closed = false) => L(s).Poly(pts.Select(V), closed);
    public void Rect(LayerStyle s, double x0, double y0, double x1, double y1) =>
        Poly(s, new[] { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) }, true);
    public void Circle(LayerStyle s, Vec2 c, double r, bool filled = false) => L(s).Circles.Add(new CircleItem(V(c), r, filled));

    /// <summary>Text; a newline character starts a new line below (the first line sits at <paramref name="at"/>).</summary>
    public void Text(string text, Vec2 at, double? height = null, double rotation = 0, TextAlign align = TextAlign.Left, LayerStyle? layer = null)
    {
        double h = height ?? TextH;
        var lines = text.Split('\n');
        var down = -Vec2.FromAngle(rotation).Left;
        for (int i = 0; i < lines.Length; i++)
            L(layer ?? SheetLayers.Text).Texts.Add(new TextItem(V(at + down * (i * h * 1.55)), lines[i], h, rotation, align));
    }

    /// <summary>
    /// Text that reads along a direction starting at <paramref name="at"/>; if the direction would print upside down, the
    /// text is turned around and right-justified so it still extends from the point in that direction.
    /// </summary>
    public void TextAlong(string text, Vec2 at, Vec2 dir, double? height = null, LayerStyle? layer = null)
    {
        double ang = Math.Atan2(dir.Y, dir.X);
        double deg = (ang * 180 / Math.PI % 360 + 360) % 360;
        bool flip = deg > 90 && deg <= 270;
        Text(text, at, height, flip ? ang + Math.PI : ang, flip ? TextAlign.Right : TextAlign.Left, layer);
    }

    /// <summary>Title with an underline, centered at <paramref name="at"/>.</summary>
    public void Title(string title, string? subtitle, Vec2 at)
    {
        Text(title, at, TitleH, 0, TextAlign.Center, SheetLayers.Title);
        double w = title.Length * TitleH * 0.85;
        Line(SheetLayers.Title, at + new Vec2(-w / 2, -TitleH * 0.9), at + new Vec2(w / 2, -TitleH * 0.9));
        if (subtitle != null) Text(subtitle, at + new Vec2(0, -TitleH * 1.9), TextH, 0, TextAlign.Center);
    }

    /// <summary>
    /// Linear dimension between a and b, measured along their direction, with the dimension line offset by
    /// <paramref name="offset"/> (paper inches, sign = side: positive to the left of a→b).
    /// </summary>
    public void Dim(Vec2 a, Vec2 b, double offset, string? text = null, double? value = null)
    {
        var d = b - a;
        double len = d.Length;
        if (len < 1e-9) return;
        var u = d / len;
        var n = u.Left;
        double off = Paper(offset);
        var a2 = a + n * off;
        var b2 = b + n * off;
        double gap = Paper(0.04) * Math.Sign(off == 0 ? 1 : off), ext = Paper(0.06) * Math.Sign(off == 0 ? 1 : off);
        var dims = L(SheetLayers.Dims);
        if (Math.Abs(off) > 1e-9)
        {
            dims.Line(V(a + n * gap), V(a2 + n * ext));
            dims.Line(V(b + n * gap), V(b2 + n * ext));
        }
        dims.Line(V(a2 - u * Paper(0.05)), V(b2 + u * Paper(0.05)));
        // Architectural ticks (45°).
        var t = (u + n).Normalized() * Paper(0.05);
        dims.Line(V(a2 - t), V(a2 + t));
        dims.Line(V(b2 - t), V(b2 + t));
        string label = text ?? Units.FormatLength(value ?? len);
        double ang = Math.Atan2(u.Y, u.X);
        // Text is middle-aligned: center it just outside the dimension line, on the side away from the object.
        var mid = (a2 + b2) / 2 + n * ((off >= 0 ? 1 : -1) * (Paper(0.03) + TextH / 2));
        Text(label, mid, TextH, ang, TextAlign.Center, SheetLayers.Dims);
    }

    /// <summary>Chain of dimensions along consecutive points.</summary>
    public void DimChain(IReadOnlyList<Vec2> pts, double offset, Func<int, string?>? text = null)
    {
        for (int i = 1; i < pts.Count; i++) Dim(pts[i - 1], pts[i], offset, text?.Invoke(i - 1));
    }

    /// <summary>Leader from a point to a note; the note sits at the end of a short shoulder.</summary>
    public void Leader(Vec2 target, Vec2 elbow, string note, LayerStyle? layer = null)
    {
        var lyr = L(layer ?? SheetLayers.Text);
        lyr.Line(V(target), V(elbow));
        bool right = elbow.X >= target.X;
        var end = elbow + new Vec2(Paper(0.15) * (right ? 1 : -1), 0);
        lyr.Line(V(elbow), V(end));
        // Arrowhead at the target.
        var dir = (target - elbow).Normalized();
        var back = target - dir * Paper(0.08);
        var side = dir.Left * Paper(0.025);
        lyr.Poly(new[] { V(target), V(back + side), V(back - side) }, true);
        Text(note, end + new Vec2(Paper(0.04) * (right ? 1 : -1), 0), TextH, 0, right ? TextAlign.Left : TextAlign.Right, layer);
    }

    /// <summary>
    /// AWS fillet weld symbol: arrow to the joint, reference line, fillet triangle(s) (below = arrow side, above = other
    /// side) with the size, and an optional tail note.
    /// </summary>
    public void WeldSymbol(Vec2 target, Vec2 elbow, string size, bool bothSides, string tail = "")
    {
        var lyr = L(SheetLayers.Text);
        lyr.Line(V(target), V(elbow));
        var dir = (target - elbow).Normalized();
        var back = target - dir * Paper(0.08);
        lyr.Poly(new[] { V(target), V(back + dir.Left * Paper(0.025)), V(back - dir.Left * Paper(0.025)) }, true);
        bool right = elbow.X >= target.X;
        double sgn = right ? 1 : -1;
        double refLen = Paper(0.75);
        var end = elbow + new Vec2(sgn * refLen, 0);
        lyr.Line(V(elbow), V(end));
        double tx = elbow.X + sgn * Paper(0.3), h = Paper(0.07);
        // Fillet: right triangle with the vertical leg on the left.
        lyr.Poly(new[] { V(new Vec2(tx, elbow.Y)), V(new Vec2(tx, elbow.Y - h)), V(new Vec2(tx + h, elbow.Y)) }, true);
        if (bothSides) lyr.Poly(new[] { V(new Vec2(tx, elbow.Y)), V(new Vec2(tx, elbow.Y + h)), V(new Vec2(tx + h, elbow.Y)) }, true);
        Text(size, new Vec2(tx - Paper(0.04), elbow.Y - h * 0.6), SmallTextH, 0, TextAlign.Right);
        if (bothSides) Text(size, new Vec2(tx - Paper(0.04), elbow.Y + h * 0.6), SmallTextH, 0, TextAlign.Right);
        if (tail.Length > 0)
        {
            lyr.Line(V(end), V(end + new Vec2(sgn * Paper(0.08), Paper(0.08))));
            lyr.Line(V(end), V(end + new Vec2(sgn * Paper(0.08), -Paper(0.08))));
            Text(tail, end + new Vec2(sgn * Paper(0.12), 0), SmallTextH, 0, right ? TextAlign.Left : TextAlign.Right);
        }
    }

    /// <summary>Elevation tag: a small triangle on a level line with the elevation text.</summary>
    public void ElevationTag(Vec2 at, string label, bool right = true)
    {
        var lyr = L(SheetLayers.Text);
        double h = Paper(0.06);
        lyr.Poly(new[] { V(at), V(at + new Vec2(-h * 0.6, h)), V(at + new Vec2(h * 0.6, h)) }, true);
        double len = Paper(0.9);
        var end = at + new Vec2(right ? len : -len, 0);
        lyr.Line(V(at), V(end));
        Text(label, at + new Vec2(right ? h : -h, h * 0.6 + SmallTextH * 0.6), SmallTextH, 0, right ? TextAlign.Left : TextAlign.Right);
    }

    /// <summary>Hatches a closed polygon with parallel lines (concrete cut: 45°, spacing in paper inches).</summary>
    public void Hatch(IReadOnlyList<Vec2> poly, double spacingPaper = 0.08, double angleDeg = 45, LayerStyle? layer = null)
    {
        if (poly.Count < 3) return;
        double a = angleDeg * Math.PI / 180;
        var dir = Vec2.FromAngle(a);
        var nrm = dir.Left;
        double sp = Paper(spacingPaper);
        double min = poly.Min(p => Vec2.Dot(p, nrm)), max = poly.Max(p => Vec2.Dot(p, nrm));
        var lyr = L(layer ?? SheetLayers.Hatch);
        for (double c = Math.Ceiling(min / sp) * sp; c <= max; c += sp)
        {
            var hits = new List<double>();
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i];
                var q = poly[(i + 1) % poly.Count];
                double dp = Vec2.Dot(p, nrm) - c, dq = Vec2.Dot(q, nrm) - c;
                if ((dp < 0) == (dq < 0) || Math.Abs(dp - dq) < 1e-15) continue;
                var x = p + (q - p) * (dp / (dp - dq));
                hits.Add(Vec2.Dot(x, dir));
            }
            hits.Sort();
            var o = nrm * c;
            for (int k = 0; k + 1 < hits.Count; k += 2)
                lyr.Line(V(o + dir * hits[k]), V(o + dir * hits[k + 1]));
        }
    }

    /// <summary>Section-cut concrete: outline on the cut layer plus hatch.</summary>
    public void CutConcrete(IReadOnlyList<Vec2> poly, double hatchSpacing = 0.08)
    {
        Poly(SheetLayers.ConcreteCut, poly, true);
        Hatch(poly, hatchSpacing);
    }

    public string Elev(double z) => Units.FormatLength(z, LengthKind.Coordinate);
    public string Len(double m) => Units.FormatLength(m);
    public string Short(double m) => Units.FormatLength(m, LengthKind.Short);
}
