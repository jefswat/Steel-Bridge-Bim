using System.Globalization;
using System.Xml.Linq;
using SBM.Core.Alignments;
using SBM.Core.Geometry;

namespace SBM.IO.LandXml;

public sealed class LandXmlImportResult
{
    public List<Alignment> Alignments { get; } = new();
    public List<string> Messages { get; } = new();
    /// <summary>Linear unit declared in the file, for display.</summary>
    public string LinearUnit { get; set; } = "meter";
    /// <summary>True when the file declared US survey feet.</summary>
    public bool UsSurveyFeet { get; set; }
    public bool Imperial { get; set; }
}

/// <summary>
/// Reads alignments (horizontal geometry, station equations, profiles, superelevation) from LandXML 1.0–1.2.
/// Namespaces are ignored so files from Civil 3D, OpenRoads and others all load.
/// </summary>
public static class LandXmlReader
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static LandXmlImportResult Read(string path) => Read(XDocument.Load(path));

    public static LandXmlImportResult ReadString(string xml) => Read(XDocument.Parse(xml));

    public static LandXmlImportResult Read(XDocument doc)
    {
        var res = new LandXmlImportResult();
        var root = doc.Root ?? throw new InvalidDataException("Empty LandXML document.");
        double scale = ReadUnits(root, res);

        foreach (var al in Descendants(root, "Alignment"))
        {
            string name = (string?)al.Attribute("name") ?? $"Alignment {res.Alignments.Count + 1}";
            try
            {
                var a = ReadAlignment(al, scale, res.Messages, name);
                if (a != null) res.Alignments.Add(a);
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or AlignmentException)
            {
                res.Messages.Add($"{name}: skipped ({ex.Message}).");
            }
        }
        if (res.Alignments.Count == 0) res.Messages.Add("No alignments were found in the file.");
        return res;
    }

    private static IEnumerable<XElement> Descendants(XElement e, string local) => e.Descendants().Where(x => x.Name.LocalName == local);
    private static IEnumerable<XElement> Children(XElement e, string local) => e.Elements().Where(x => x.Name.LocalName == local);
    private static XElement? Child(XElement e, string local) => e.Elements().FirstOrDefault(x => x.Name.LocalName == local);

    private static double ReadUnits(XElement root, LandXmlImportResult res)
    {
        var units = Child(root, "Units");
        var imp = units != null ? Child(units, "Imperial") : null;
        var met = units != null ? Child(units, "Metric") : null;
        string unit = (string?)(imp ?? met)?.Attribute("linearUnit") ?? (imp != null ? "foot" : "meter");
        res.LinearUnit = unit;
        res.Imperial = imp != null;
        res.UsSurveyFeet = unit.Equals("USSurveyFoot", StringComparison.OrdinalIgnoreCase);
        return unit.ToLowerInvariant() switch
        {
            "meter" => 1,
            "millimeter" => 0.001,
            "centimeter" => 0.01,
            "kilometer" => 1000,
            "foot" or "internationalfoot" => 0.3048,
            "ussurveyfoot" => 1200.0 / 3937.0,
            "inch" => 0.0254,
            "mile" => 1609.344,
            _ => throw new InvalidDataException($"Unsupported linear unit '{unit}'."),
        };
    }

    private static double[] Numbers(string? text) =>
        (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s, NumberStyles.Float, Inv)).ToArray();

    /// <summary>LandXML points are "northing easting [elevation]".</summary>
    private static Vec2? Point(XElement parent, string local, double scale)
    {
        var e = Child(parent, local);
        if (e == null) return null;
        var n = Numbers(e.Value);
        if (n.Length < 2) return null;
        return new Vec2(n[1] * scale, n[0] * scale);
    }

    private static double Attr(XElement e, string name, double fallback = double.NaN)
    {
        var a = (string?)e.Attribute(name);
        if (a == null) return fallback;
        if (a.Equals("INF", StringComparison.OrdinalIgnoreCase) || a.Equals("Infinity", StringComparison.OrdinalIgnoreCase))
            return double.PositiveInfinity;
        return double.Parse(a, NumberStyles.Float, Inv);
    }

    private static Alignment? ReadAlignment(XElement al, double scale, List<string> msgs, string name)
    {
        var cg = Child(al, "CoordGeom");
        if (cg == null)
        {
            msgs.Add($"{name}: no CoordGeom; skipped.");
            return null;
        }

        var elements = new List<HorizontalElement>();
        foreach (var e in cg.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "Line":
                {
                    var s = Point(e, "Start", scale) ?? throw new InvalidDataException("Line without Start.");
                    var en = Point(e, "End", scale) ?? throw new InvalidDataException("Line without End.");
                    elements.Add(HorizontalElement.Line(s, en));
                    break;
                }
                case "Curve":
                    elements.Add(ReadCurve(e, scale));
                    break;
                case "Spiral":
                    elements.Add(ReadSpiral(e, scale, msgs, name));
                    break;
                case "IrregularLine":
                case "Chain":
                    msgs.Add($"{name}: '{e.Name.LocalName}' elements are not supported and were ignored.");
                    break;
            }
        }
        if (elements.Count == 0)
        {
            msgs.Add($"{name}: no horizontal elements; skipped.");
            return null;
        }

        var a = new Alignment(elements)
        {
            Name = name,
            Description = (string?)al.Attribute("desc") ?? "",
            StartStation = Attr(al, "staStart", 0) * scale,
        };
        foreach (var w in a.CheckContinuity(0.003)) msgs.Add($"{name}: {w}");

        foreach (var eq in Children(al, "StaEquation"))
        {
            double back = Attr(eq, "staBack") * scale, ahead = Attr(eq, "staAhead") * scale;
            double internalSta = Attr(eq, "staInternal", double.NaN) * scale;
            double dist = double.IsNaN(internalSta) ? a.StationToDistance(back) : internalSta - a.StartStation;
            a.StationEquations.Add(new StationEquation { Distance = dist, StationBack = back, StationAhead = ahead });
        }

        a.PiDefinition = PiDefinition.TryExtract(a.Elements);

        var profile = Descendants(al, "ProfAlign").FirstOrDefault();
        if (profile != null) a.Profile = ReadProfile(profile, scale);
        else msgs.Add($"{name}: no vertical profile (ProfAlign) found; elevations will be zero.");

        var supers = Children(al, "Superelevation").ToList();
        if (supers.Count > 0) a.CrossSlope = ReadSuperelevation(a, supers, scale, msgs, name);

        return a;
    }

    private static HorizontalElement ReadCurve(XElement e, double scale)
    {
        var s = Point(e, "Start", scale) ?? throw new InvalidDataException("Curve without Start.");
        var en = Point(e, "End", scale) ?? throw new InvalidDataException("Curve without End.");
        var c = Point(e, "Center", scale);
        bool cw = ((string?)e.Attribute("rot") ?? "ccw").Equals("cw", StringComparison.OrdinalIgnoreCase);
        double sign = cw ? -1 : 1;
        double radius = Attr(e, "radius") * scale;
        if (c == null)
        {
            if (double.IsNaN(radius)) throw new InvalidDataException("Curve needs Center or radius.");
            // Center from chord and radius (minor arc assumed).
            var mid = Vec2.Lerp(s, en, 0.5);
            var chord = en - s;
            double h = Math.Sqrt(Math.Max(0, radius * radius - chord.LengthSquared / 4));
            c = mid + chord.Normalized().Left * (h * sign);
        }
        var rs = s - c.Value;
        var re = en - c.Value;
        if (double.IsNaN(radius)) radius = rs.Length;
        var tangent = cw ? rs.Right.Normalized() : rs.Left.Normalized();
        double sweep = Math.Atan2(Vec2.Cross(rs, re), Vec2.Dot(rs, re)) * sign;
        if (sweep <= 0) sweep += 2 * Math.PI;
        double length = Attr(e, "length", double.NaN) * scale;
        if (double.IsNaN(length) || Math.Abs(length - radius * sweep) > 0.01) length = radius * sweep;
        return new HorizontalElement(s, tangent.Angle, length, sign / radius, sign / radius);
    }

    private static HorizontalElement ReadSpiral(XElement e, double scale, List<string> msgs, string name)
    {
        var s = Point(e, "Start", scale) ?? throw new InvalidDataException("Spiral without Start.");
        var en = Point(e, "End", scale) ?? throw new InvalidDataException("Spiral without End.");
        var pi = Point(e, "PI", scale);
        bool cw = ((string?)e.Attribute("rot") ?? "ccw").Equals("cw", StringComparison.OrdinalIgnoreCase);
        double sign = cw ? -1 : 1;
        double r0 = Attr(e, "radiusStart", double.PositiveInfinity) * scale;
        double r1 = Attr(e, "radiusEnd", double.PositiveInfinity) * scale;
        double len = Attr(e, "length") * scale;
        string type = (string?)e.Attribute("spiType") ?? "clothoid";
        if (!type.Equals("clothoid", StringComparison.OrdinalIgnoreCase))
            msgs.Add($"{name}: '{type}' spiral approximated as a clothoid.");
        double k0 = double.IsInfinity(r0) ? 0 : sign / r0;
        double k1 = double.IsInfinity(r1) ? 0 : sign / r1;
        if (double.IsNaN(len)) throw new InvalidDataException("Spiral without length.");

        double angle;
        if (pi != null) angle = (pi.Value - s).Angle;
        else
        {
            // Solve the start direction so the spiral ends at End.
            var probe = new HorizontalElement(s, 0, len, k0, k1);
            angle = (en - s).Angle - (probe.EndPoint - s).Angle;
        }
        var sp = new HorizontalElement(s, angle, len, k0, k1);
        double miss = Vec2.Distance(sp.EndPoint, en);
        if (miss > 0.005) msgs.Add($"{name}: spiral at ({s.X:F2}, {s.Y:F2}) misses its end point by {miss:F4} m.");
        return sp;
    }

    private static VerticalProfile ReadProfile(XElement pa, double scale)
    {
        var p = new VerticalProfile();
        foreach (var e in pa.Elements())
        {
            var n = Numbers(e.Value);
            if (n.Length < 2) continue;
            var v = new Vpi { Station = n[0] * scale, Elevation = n[1] * scale };
            switch (e.Name.LocalName)
            {
                case "PVI":
                    break;
                case "ParaCurve":
                case "CircCurve":
                    v.LengthIn = v.LengthOut = Attr(e, "length", 0) * scale / 2;
                    break;
                case "UnsymParaCurve":
                    v.LengthIn = Attr(e, "lengthIn", 0) * scale;
                    v.LengthOut = Attr(e, "lengthOut", 0) * scale;
                    break;
                default:
                    continue;
            }
            p.Vpis.Add(v);
        }
        return p;
    }

    /// <summary>
    /// Converts LandXML superelevation critical stations into a cross-slope table. Normal crown is assumed
    /// at −2%; the high side is taken as the outside of the curve at full superelevation.
    /// </summary>
    private static CrossSlopeTable ReadSuperelevation(Alignment a, List<XElement> supers, double scale, List<string> msgs, string name)
    {
        const double nc = -0.02;
        var rows = new List<CrossSlopeRow>();
        foreach (var se in supers)
        {
            double Sta(string local)
            {
                var c = Child(se, local);
                return c == null ? double.NaN : Numbers(c.Value).FirstOrDefault(double.NaN) * scale;
            }
            double beginRunout = Sta("BeginRunoutSta"), beginRunoff = Sta("BeginRunoffSta"), full = Sta("FullSuperSta");
            double runoff = Sta("RunoffSta"), startRunout = Sta("StartofRunoutSta"), endRunout = Sta("EndofRunoutSta");
            var eEl = Child(se, "FullSuperelev");
            double e = eEl == null ? double.NaN : Math.Abs(Numbers(eEl.Value).FirstOrDefault(double.NaN)) / 100;
            if (double.IsNaN(full) || double.IsNaN(e))
            {
                msgs.Add($"{name}: incomplete superelevation region skipped.");
                continue;
            }
            double k = a.CurvatureAtDistance(a.StationToDistance(full));
            bool left = k > 0; // left curve: left side low
            CrossSlopeRow Row(double sta, double inside, double outside) =>
                left ? new CrossSlopeRow(sta, inside, outside) : new CrossSlopeRow(sta, outside, inside);

            if (!double.IsNaN(beginRunout)) rows.Add(Row(beginRunout, nc, nc));
            if (!double.IsNaN(beginRunoff)) rows.Add(Row(beginRunoff, nc, 0));
            if (!double.IsNaN(beginRunoff)) rows.Add(Row(beginRunoff + (full - beginRunoff) * (-nc / e), nc, -nc));
            rows.Add(Row(full, -e, e));
            if (!double.IsNaN(runoff)) rows.Add(Row(runoff, -e, e));
            if (!double.IsNaN(runoff) && !double.IsNaN(startRunout)) rows.Add(Row(startRunout - (startRunout - runoff) * (-nc / e), nc, -nc));
            if (!double.IsNaN(startRunout)) rows.Add(Row(startRunout, nc, 0));
            if (!double.IsNaN(endRunout)) rows.Add(Row(endRunout, nc, nc));
        }
        if (rows.Count == 0) return CrossSlopeTable.NormalCrown();
        msgs.Add($"{name}: superelevation imported assuming a {nc * 100:0.#}% normal crown; review the cross-slope table.");
        return new CrossSlopeTable { Rows = rows.OrderBy(r => r.Station).ToList() };
    }
}
