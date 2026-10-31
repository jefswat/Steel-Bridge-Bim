using System.Xml.Linq;
using SBM.Core.Road;

namespace SBM.IO.LandXml;

/// <summary>
/// Reads corridor / grading feature lines from a LandXML file (Civil 3D export): <c>PlanFeatures/PlanFeature</c> with a <c>CoordGeom</c> of <c>Line</c>
/// (Start / End "N E Z"), <c>IrregularLine</c> / <c>Chain</c> (<c>PntList3D</c>), <c>Curve</c> (Start / Center / End, rot cw|ccw: the arc is sampled every
/// <= 5 degrees, elevation linear along it) and <c>Spiral</c> (Start / End only: a chord, reported). Coordinates in the file's linear unit -> meters.
/// </summary>
public static class LandXmlFeatureLines
{
    public sealed class Result
    {
        public List<RoadFeatureLine> Lines { get; } = new();
        public List<string> Messages { get; } = new();
    }

    public static Result Read(string path) => Read(XDocument.Load(path), Path.GetFileName(path));
    public static Result ReadString(string xml, string source = "LandXML") => Read(XDocument.Parse(xml), source);

    public static Result Read(XDocument doc, string source)
    {
        var res = new Result();
        var root = doc.Root ?? throw new InvalidDataException("Empty LandXML document.");
        var units = LandXmlPipeNetworks.Child(root, "Units");
        var u = units == null ? null : LandXmlPipeNetworks.Child(units, "Imperial") ?? LandXmlPipeNetworks.Child(units, "Metric");
        double sc = LandXmlPipeNetworks.Scale((string?)u?.Attribute("linearUnit") ?? (u?.Name.LocalName == "Imperial" ? "foot" : "meter"));
        int spirals = 0;
        foreach (var pf in LandXmlPipeNetworks.Descendants(root, "PlanFeature"))
        {
            var fl = new RoadFeatureLine { Name = (string?)pf.Attribute("name") ?? $"Feature line {res.Lines.Count + 1}" };
            var pts = fl.Points;
            void Add(double[] neZ)
            {
                if (neZ.Length < 2) return;
                var p = new RoadFeaturePoint(neZ[1] * sc, neZ[0] * sc, neZ.Length >= 3 ? neZ[2] * sc : double.NaN);
                if (pts.Count > 0 && Math.Abs(pts[^1].X - p.X) < 1e-7 && Math.Abs(pts[^1].Y - p.Y) < 1e-7) return;
                pts.Add(p);
            }
            double[] Pt(XElement e, string name) => LandXmlPipeNetworks.Child(e, name) is { } c ? LandXmlPipeNetworks.Numbers(c.Value) : Array.Empty<double>();
            var cg = LandXmlPipeNetworks.Child(pf, "CoordGeom");
            if (cg == null) { res.Messages.Add($"{fl.Name}: no CoordGeom - skipped."); continue; }
            foreach (var g in cg.Elements())
            {
                switch (g.Name.LocalName)
                {
                    case "Line": Add(Pt(g, "Start")); Add(Pt(g, "End")); break;
                    case "IrregularLine" or "Chain":
                        var pl = LandXmlPipeNetworks.Numbers(LandXmlPipeNetworks.Child(g, "PntList3D")?.Value ?? LandXmlPipeNetworks.Child(g, "PntList2D")?.Value);
                        int dim = LandXmlPipeNetworks.Child(g, "PntList3D") != null ? 3 : 2;
                        for (int i = 0; i + dim - 1 < pl.Length; i += dim) Add(pl[i..(i + dim)]);
                        break;
                    case "Curve":
                    {
                        var a = Pt(g, "Start"); var c = Pt(g, "Center"); var b = Pt(g, "End");
                        if (a.Length < 2 || c.Length < 2 || b.Length < 2) { Add(a); Add(b); break; }
                        bool cw = string.Equals((string?)g.Attribute("rot"), "cw", StringComparison.OrdinalIgnoreCase);
                        // plan angles in (E, N); N E in the file
                        double a0 = Math.Atan2(a[0] - c[0], a[1] - c[1]), a1 = Math.Atan2(b[0] - c[0], b[1] - c[1]);
                        double sweep = a1 - a0;
                        if (cw) { while (sweep > 0) sweep -= 2 * Math.PI; } else { while (sweep < 0) sweep += 2 * Math.PI; }
                        double r = Math.Sqrt(Math.Pow(a[0] - c[0], 2) + Math.Pow(a[1] - c[1], 2));
                        int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (5 * Math.PI / 180)));
                        double za = a.Length >= 3 ? a[2] : double.NaN, zb = b.Length >= 3 ? b[2] : double.NaN;
                        for (int i = 0; i <= n; i++)
                        {
                            double t = (double)i / n, ang = a0 + sweep * t;
                            Add(new[] { c[0] + r * Math.Sin(ang), c[1] + r * Math.Cos(ang), za + (zb - za) * t });
                        }
                        break;
                    }
                    case "Spiral": spirals++; Add(Pt(g, "Start")); Add(Pt(g, "End")); break;
                }
            }
            if (pts.Any(p => double.IsNaN(p.Z))) { res.Messages.Add($"{fl.Name}: points without elevation - skipped (a corridor feature line is 3D)."); continue; }
            if (pts.Count < 2) { res.Messages.Add($"{fl.Name}: fewer than 2 points - skipped."); continue; }
            res.Lines.Add(fl);
        }
        if (spirals > 0) res.Messages.Add($"{spirals} spiral segments of feature lines read as chords (Civil 3D usually exports corridor feature lines as lines / arcs).");
        return res;
    }
}
