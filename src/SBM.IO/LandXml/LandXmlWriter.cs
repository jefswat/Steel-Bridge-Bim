using System.Globalization;
using System.Xml.Linq;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.IO.LandXml;

/// <summary>Writes alignments (horizontal + profile) to LandXML 1.2 in the project's display units.</summary>
public static class LandXmlWriter
{
    private static readonly XNamespace Ns = "http://www.landxml.org/schema/LandXML-1.2";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(string path, IEnumerable<Alignment> alignments, UnitSystem units) =>
        Build(alignments, units).Save(path);

    /// <summary>Writes a TIN surface (LandXML Surfaces/Surface/Definition with Pnts and Faces) in display units.</summary>
    public static void WriteSurface(string path, string name, SBM.Core.Solids.Mesh tin, UnitSystem units)
    {
        double f = 1 / units.LongUnit;
        using var w = new StreamWriter(path);
        w.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        w.WriteLine("<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\">");
        w.WriteLine(units.IsUS
            ? $"  <Units><Imperial linearUnit=\"{(units.Foot == FootDefinition.USSurvey ? "USSurveyFoot" : "foot")}\" areaUnit=\"squareFoot\" volumeUnit=\"cubicYard\" angularUnit=\"decimal degrees\" directionUnit=\"decimal degrees\"/></Units>"
            : "  <Units><Metric linearUnit=\"meter\" areaUnit=\"squareMeter\" volumeUnit=\"cubicMeter\" angularUnit=\"decimal degrees\" directionUnit=\"decimal degrees\"/></Units>");
        w.WriteLine($"  <Surfaces><Surface name=\"{System.Security.SecurityElement.Escape(name)}\"><Definition surfType=\"TIN\"><Pnts>");
        for (int i = 0; i < tin.Positions.Count; i++)
        {
            var p = tin.Positions[i];
            w.WriteLine($"    <P id=\"{i + 1}\">{(p.Y * f).ToString("0.###", Inv)} {(p.X * f).ToString("0.###", Inv)} {(p.Z * f).ToString("0.###", Inv)}</P>");
        }
        w.WriteLine("  </Pnts><Faces>");
        for (int i = 0; i < tin.Indices.Count; i += 3)
            w.WriteLine($"    <F>{tin.Indices[i] + 1} {tin.Indices[i + 1] + 1} {tin.Indices[i + 2] + 1}</F>");
        w.WriteLine("  </Faces></Definition></Surface></Surfaces>");
        w.WriteLine("</LandXML>");
    }

    public static XDocument Build(IEnumerable<Alignment> alignments, UnitSystem units)
    {
        double f = 1 / units.LongUnit;
        string F(double v) => (v * f).ToString("0.########", Inv);
        string Pt(Vec2 p) => $"{F(p.Y)} {F(p.X)}";

        XElement unitsEl = units.IsUS
            ? new XElement(Ns + "Imperial", new XAttribute("areaUnit", "squareFoot"), new XAttribute("linearUnit", units.Foot == FootDefinition.USSurvey ? "USSurveyFoot" : "foot"),
                new XAttribute("volumeUnit", "cubicFeet"), new XAttribute("temperatureUnit", "fahrenheit"), new XAttribute("pressureUnit", "inHG"),
                new XAttribute("angularUnit", "decimal degrees"), new XAttribute("directionUnit", "decimal degrees"))
            : new XElement(Ns + "Metric", new XAttribute("areaUnit", "squareMeter"), new XAttribute("linearUnit", "meter"),
                new XAttribute("volumeUnit", "cubicMeter"), new XAttribute("temperatureUnit", "celsius"), new XAttribute("pressureUnit", "milliBars"),
                new XAttribute("angularUnit", "decimal degrees"), new XAttribute("directionUnit", "decimal degrees"));

        var als = new XElement(Ns + "Alignments");
        foreach (var a in alignments)
        {
            var cg = new XElement(Ns + "CoordGeom");
            foreach (var e in a.Elements)
            {
                double rot = Math.Sign(e.K0 + e.K1);
                switch (e.Kind)
                {
                    case HorizontalElementKind.Line:
                        cg.Add(new XElement(Ns + "Line", new XAttribute("staStart", F(a.DistanceToStation(e.StartDistance))), new XAttribute("length", F(e.Length)),
                            new XElement(Ns + "Start", Pt(e.Start)), new XElement(Ns + "End", Pt(e.EndPoint))));
                        break;
                    case HorizontalElementKind.Arc:
                    {
                        var c = e.Start + e.TangentAt(0).Left * (1 / e.K0);
                        cg.Add(new XElement(Ns + "Curve", new XAttribute("rot", rot > 0 ? "ccw" : "cw"),
                            new XAttribute("staStart", F(a.DistanceToStation(e.StartDistance))),
                            new XAttribute("length", F(e.Length)), new XAttribute("radius", F(e.Radius)),
                            new XElement(Ns + "Start", Pt(e.Start)), new XElement(Ns + "Center", Pt(c)), new XElement(Ns + "End", Pt(e.EndPoint))));
                        break;
                    }
                    case HorizontalElementKind.Spiral:
                    {
                        Numerics.IntersectLines(e.Start, e.TangentAt(0), e.EndPoint, e.TangentAt(e.Length), out double t, out _);
                        var pi = e.Start + e.TangentAt(0) * t;
                        string R(double k) => Math.Abs(k) < 1e-15 ? "INF" : F(1 / Math.Abs(k));
                        cg.Add(new XElement(Ns + "Spiral", new XAttribute("rot", rot > 0 ? "ccw" : "cw"), new XAttribute("spiType", "clothoid"),
                            new XAttribute("staStart", F(a.DistanceToStation(e.StartDistance))),
                            new XAttribute("length", F(e.Length)), new XAttribute("radiusStart", R(e.K0)), new XAttribute("radiusEnd", R(e.K1)),
                            new XElement(Ns + "Start", Pt(e.Start)), new XElement(Ns + "PI", Pt(pi)), new XElement(Ns + "End", Pt(e.EndPoint))));
                        break;
                    }
                }
            }

            var al = new XElement(Ns + "Alignment", new XAttribute("name", a.Name), new XAttribute("length", F(a.Length)),
                new XAttribute("staStart", F(a.StartStation)), new XAttribute("desc", a.Description), cg);

            foreach (var eq in a.StationEquations)
                al.Add(new XElement(Ns + "StaEquation", new XAttribute("staBack", F(eq.StationBack)), new XAttribute("staAhead", F(eq.StationAhead)),
                    new XAttribute("staInternal", F(a.StartStation + eq.Distance))));

            if (!a.Profile.IsEmpty)
            {
                var pa = new XElement(Ns + "ProfAlign", new XAttribute("name", a.Name + " Profile"));
                foreach (var v in a.Profile.Vpis)
                {
                    string txt = $"{F(v.Station)} {F(v.Elevation)}";
                    if (v.CurveLength <= 0) pa.Add(new XElement(Ns + "PVI", txt));
                    else if (Math.Abs(v.LengthIn - v.LengthOut) < 1e-9) pa.Add(new XElement(Ns + "ParaCurve", new XAttribute("length", F(v.CurveLength)), txt));
                    else pa.Add(new XElement(Ns + "UnsymParaCurve", new XAttribute("lengthIn", F(v.LengthIn)), new XAttribute("lengthOut", F(v.LengthOut)), txt));
                }
                al.Add(new XElement(Ns + "Profile", new XAttribute("name", a.Name + " Profile"), pa));
            }
            als.Add(al);
        }

        var root = new XElement(Ns + "LandXML",
            new XAttribute("version", "1.2"),
            new XAttribute("date", DateTime.Now.ToString("yyyy-MM-dd", Inv)),
            new XAttribute("time", DateTime.Now.ToString("HH:mm:ss", Inv)),
            new XElement(Ns + "Units", unitsEl),
            new XElement(Ns + "Application", new XAttribute("name", "Steel Bridge Modeler"), new XAttribute("manufacturer", "SBM"), new XAttribute("version", "0.1")),
            als);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }
}
