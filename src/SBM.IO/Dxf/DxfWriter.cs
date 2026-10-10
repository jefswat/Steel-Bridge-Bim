using System.Globalization;
using System.Text;
using SBM.Core.Drawing;
using SBM.Core.Geometry;

namespace SBM.IO.Dxf;

/// <summary>
/// Writes a <see cref="PlanDrawing"/> as ASCII DXF (AutoCAD R12 / AC1009), the most widely compatible flavor:
/// MicroStation, Civil 3D, OpenRoads and most viewers read it. Coordinates are written in the given unit
/// (1 / metersPerUnit), with true elevations on 3D polylines.
/// </summary>
public static class DxfWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="flat">2D model: polylines are written as 2D polylines at Z = 0.</param>
    /// <param name="ltScale">Linetype scale ($LTSCALE); 0 = automatic.</param>
    public static void Write(string path, PlanDrawing drawing, double metersPerUnit, bool flat = false, double ltScale = 0)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        Write(w, drawing, metersPerUnit, flat, ltScale);
    }

    public static void Write(TextWriter w, PlanDrawing drawing, double metersPerUnit, bool flat = false, double ltScale = 0)
    {
        double f = 1 / metersPerUnit;
        void G(int code, string value) { w.Write(code.ToString(Inv)); w.Write('\n'); w.Write(value); w.Write('\n'); }
        void D(int code, double v) => G(code, v.ToString("0.0#########", Inv));

        var ext = drawing.Extents();
        if (ext.IsEmpty) ext = new Box2 { MinX = 0, MinY = 0, MaxX = 1, MaxY = 1 };

        // HEADER
        G(0, "SECTION"); G(2, "HEADER");
        G(9, "$ACADVER"); G(1, "AC1009");
        G(9, "$EXTMIN"); D(10, ext.MinX * f); D(20, ext.MinY * f); D(30, 0);
        G(9, "$EXTMAX"); D(10, ext.MaxX * f); D(20, ext.MaxY * f); D(30, 0);
        G(9, "$LTSCALE"); D(40, ltScale > 0 ? ltScale : metersPerUnit < 0.5 ? 10 : 3);
        G(0, "ENDSEC");

        // TABLES
        G(0, "SECTION"); G(2, "TABLES");
        G(0, "TABLE"); G(2, "LTYPE"); G(70, "4");
        WriteLtype(G, D, "CONTINUOUS", "Solid line", Array.Empty<double>());
        WriteLtype(G, D, "CENTER", "Center ____ _ ____ _", new[] { 1.25, -0.25, 0.25, -0.25 });
        WriteLtype(G, D, "DASHED", "Dashed __ __ __", new[] { 0.5, -0.25 });
        WriteLtype(G, D, "HIDDEN", "Hidden _ _ _", new[] { 0.25, -0.125 });
        G(0, "ENDTAB");

        var layers = drawing.Layers.ToList();
        G(0, "TABLE"); G(2, "LAYER"); G(70, layers.Count.ToString(Inv));
        foreach (var l in layers)
        {
            G(0, "LAYER"); G(2, l.Name); G(70, "0"); G(62, l.Style.Aci.ToString(Inv)); G(6, LtypeName(l.Style.LineStyle));
        }
        G(0, "ENDTAB");

        G(0, "TABLE"); G(2, "STYLE"); G(70, "1");
        G(0, "STYLE"); G(2, "STANDARD"); G(70, "0"); D(40, 0); D(41, 1); D(50, 0); G(71, "0"); D(42, 1); G(3, "txt"); G(4, "");
        G(0, "ENDTAB");
        G(0, "ENDSEC");

        // ENTITIES
        G(0, "SECTION"); G(2, "ENTITIES");
        foreach (var l in layers)
        {
            foreach (var pl in l.Polylines)
            {
                if (pl.Points.Length < 2) continue;
                if (pl.Points.Length == 2 && !pl.Closed)
                {
                    G(0, "LINE"); G(8, l.Name);
                    D(10, pl.Points[0].X * f); D(20, pl.Points[0].Y * f); D(30, pl.Points[0].Z * f);
                    D(11, pl.Points[1].X * f); D(21, pl.Points[1].Y * f); D(31, pl.Points[1].Z * f);
                    continue;
                }
                G(0, "POLYLINE"); G(8, l.Name); G(66, "1");
                D(10, 0); D(20, 0); D(30, 0);
                G(70, ((flat ? 0 : 8) | (pl.Closed ? 1 : 0)).ToString(Inv)); // 2D or 3D polyline
                foreach (var p in pl.Points)
                {
                    G(0, "VERTEX"); G(8, l.Name);
                    D(10, p.X * f); D(20, p.Y * f); D(30, flat ? 0 : p.Z * f);
                    G(70, flat ? "0" : "32");
                }
                G(0, "SEQEND"); G(8, l.Name);
            }
            foreach (var c in l.Circles)
            {
                G(0, "CIRCLE"); G(8, l.Name);
                D(10, c.Center.X * f); D(20, c.Center.Y * f); D(30, flat ? 0 : c.Center.Z * f);
                D(40, c.Radius * f);
                if (c.Filled)
                {
                    // Solid dot (rebar in section): a SOLID quad is the R12 fill primitive.
                    double r = c.Radius * f * 0.7;
                    G(0, "SOLID"); G(8, l.Name);
                    D(10, c.Center.X * f - r); D(20, c.Center.Y * f - r); D(30, 0);
                    D(11, c.Center.X * f + r); D(21, c.Center.Y * f - r); D(31, 0);
                    D(12, c.Center.X * f - r); D(22, c.Center.Y * f + r); D(32, 0);
                    D(13, c.Center.X * f + r); D(23, c.Center.Y * f + r); D(33, 0);
                }
            }
            foreach (var t in l.Texts)
            {
                G(0, "TEXT"); G(8, l.Name);
                D(10, t.Position.X * f); D(20, t.Position.Y * f); D(30, t.Position.Z * f);
                D(40, t.Height * f);
                G(1, Sanitize(t.Text));
                D(50, NormalizeTextAngle(t.Rotation));
                // Middle-aligned vertically (left, center or right horizontally).
                int h = t.Align switch { TextAlign.Center => 1, TextAlign.Right => 2, _ => 0 };
                G(72, h.ToString(Inv));
                D(11, t.Position.X * f); D(21, t.Position.Y * f); D(31, flat ? 0 : t.Position.Z * f);
                G(73, "2");
            }
        }
        G(0, "ENDSEC");
        G(0, "EOF");
    }

    /// <summary>Keeps text readable: rotations are flipped so text never reads upside down.</summary>
    public static double NormalizeTextAngle(double radians)
    {
        double deg = radians * 180 / Math.PI % 360;
        if (deg < 0) deg += 360;
        if (deg > 90 && deg <= 270) deg -= 180;
        if (deg < 0) deg += 360;
        return deg;
    }

    private static string Sanitize(string s) => s.Replace("°", "%%d").Replace("\n", " ");

    private static string LtypeName(LineStyle s) => s switch
    {
        LineStyle.Center => "CENTER",
        LineStyle.Dashed => "DASHED",
        LineStyle.Hidden => "HIDDEN",
        _ => "CONTINUOUS",
    };

    private static void WriteLtype(Action<int, string> g, Action<int, double> d, string name, string desc, double[] pattern)
    {
        g(0, "LTYPE"); g(2, name); g(70, "0"); g(3, desc); g(72, "65");
        g(73, pattern.Length.ToString(Inv));
        d(40, pattern.Sum(Math.Abs));
        foreach (var p in pattern) d(49, p);
    }
}
