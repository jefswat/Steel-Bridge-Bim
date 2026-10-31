using System.Globalization;
using System.Xml.Linq;
using SBM.Core.Utility;

namespace SBM.IO.LandXml;

/// <summary>Pipe networks read from a LandXML file (<see cref="LandXmlPipeNetworks.Read(XDocument)"/>).</summary>
public sealed class LandXmlPipeNetworkResult
{
    public List<UtilityNetworkDefinition> Networks { get; } = new();
    public List<string> Messages { get; } = new();
}

/// <summary>
/// Reads LandXML 1.x <c>PipeNetworks</c> (Civil 3D / OpenRoads export) into <see cref="UtilityNetworkDefinition"/>s: <c>Struct</c> (Center N E, elevRim,
/// elevSump, CircStruct diameter / RectStruct length x width, thickness, floorThickness, Invert elev / flowDir / refPipe, InletStruct / OutletStruct)
/// and <c>Pipe</c> (refStart, refEnd, CircPipe diameter / ElliPipe height x span / RectPipe height x width, thickness, material). Pipe inverts come from
/// the structures' Invert rows (the end of the pipe at that structure). Lengths in the file's linear unit, sizes in its <c>diameterUnit</c> (else the
/// linear unit, reported). Nothing missing is filled in: absent values stay NaN and the generator raises them as UNRESOLVED.
/// </summary>
public static class LandXmlPipeNetworks
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static LandXmlPipeNetworkResult Read(string path) => Read(XDocument.Load(path), Path.GetFileName(path));

    public static LandXmlPipeNetworkResult ReadString(string xml, string source = "LandXML") => Read(XDocument.Parse(xml), source);

    public static LandXmlPipeNetworkResult Read(XDocument doc, string source)
    {
        var res = new LandXmlPipeNetworkResult();
        var root = doc.Root ?? throw new InvalidDataException("Empty LandXML document.");
        var units = Child(root, "Units");
        var u = units == null ? null : Child(units, "Imperial") ?? Child(units, "Metric");
        string linear = (string?)u?.Attribute("linearUnit") ?? (u?.Name.LocalName == "Imperial" ? "foot" : "meter");
        double scale = Scale(linear);
        string? dia = (string?)u?.Attribute("diameterUnit");
        double dscale = dia != null ? Scale(dia) : scale;
        bool diaNote = dia == null;
        foreach (var pn in Descendants(root, "PipeNetwork"))
        {
            string name = (string?)pn.Attribute("name") ?? $"Pipe network {res.Networks.Count + 1}";
            string type = ((string?)pn.Attribute("pipeNetType") ?? "").ToLowerInvariant();
            var net = new UtilityNetworkDefinition
            {
                Name = name,
                System = type switch { "storm" => UtilitySystem.Storm, "sanitary" => UtilitySystem.Sanitary, "water" => UtilitySystem.Water, "gas" => UtilitySystem.Gas, _ => UtilitySystem.Other },
                Source = $"{source} PipeNetwork '{name}'",
            };
            if (type.Length == 0 || net.System == UtilitySystem.Other)
                res.Messages.Add($"{name}: pipeNetType '{type}' is not storm / sanitary / water / gas - system set to Other (set it in the model).");
            if (diaNote)
                net.Unresolved.Add($"UNRESOLVED ({source}): structure / pipe sizes - the file declares no diameterUnit; they were read in the linear unit ({linear}). Check the sizes.");
            var inverts = new Dictionary<(string pipe, string dir), double>(StringComparer.OrdinalIgnoreCase.WithTuple());
            foreach (var st in Descendants(pn, "Struct"))
            {
                var s = new UtilityStructureDefinition
                {
                    Name = (string?)st.Attribute("name") ?? $"S{net.Structures.Count + 1}",
                    Description = (string?)st.Attribute("desc") ?? "",
                    RimElevation = Attr(st, "elevRim") * scale,
                    SumpElevation = Attr(st, "elevSump") * scale,
                    Source = net.Source,
                };
                var c = Child(st, "Center");
                if (c != null && Numbers(c.Value) is { Length: >= 2 } n) { s.Y = n[0] * scale; s.X = n[1] * scale; }
                if (Child(st, "CircStruct") is { } cs)
                {
                    s.Shape = UtilityStructureShape.Round;
                    s.InnerDiameter = Attr(cs, "diameter") * dscale;
                    s.WallThickness = Attr(cs, "thickness") * dscale;
                    s.FloorThickness = Attr(cs, "floorThickness") * dscale;
                    s.Material = (string?)cs.Attribute("material") ?? "";
                }
                else if (Child(st, "RectStruct") is { } rs)
                {
                    s.Shape = UtilityStructureShape.Box;
                    s.InnerLength = Attr(rs, "length") * dscale;
                    s.InnerWidth = Attr(rs, "width") * dscale;
                    s.WallThickness = Attr(rs, "thickness") * dscale;
                    s.FloorThickness = Attr(rs, "floorThickness") * dscale;
                    s.Material = (string?)rs.Attribute("material") ?? "";
                    // RectStruct has no rotation attribute in LandXML 1.2: left to the generator (along the outlet pipe, flagged)
                }
                else s.Unresolved = $"UNRESOLVED ({source}): structure {s.Name} shape / size - no CircStruct or RectStruct in the file.";
                s.Kind = Child(st, "InletStruct") != null ? UtilityStructureKind.Inlet
                    : Child(st, "OutletStruct") != null ? UtilityStructureKind.Outfall
                    : KindFrom(s.Description);
                foreach (var iv in Children(st, "Invert"))
                {
                    string pipe = (string?)iv.Attribute("refPipe") ?? "";
                    string dir = ((string?)iv.Attribute("flowDir") ?? "").ToLowerInvariant();
                    double z = Attr(iv, "elev") * scale;
                    if (pipe.Length > 0 && !double.IsNaN(z)) inverts[(pipe, dir)] = z;
                }
                net.Structures.Add(s);
            }
            foreach (var pe in Descendants(pn, "Pipe"))
            {
                var p = new UtilityPipeDefinition
                {
                    Name = (string?)pe.Attribute("name") ?? $"P{net.Pipes.Count + 1}",
                    From = (string?)pe.Attribute("refStart") ?? "",
                    To = (string?)pe.Attribute("refEnd") ?? "",
                    Description = (string?)pe.Attribute("desc") ?? "",
                    Material = (string?)pe.Attribute("material") ?? "",
                    Source = net.Source,
                };
                if (Child(pe, "CircPipe") is { } cp)
                {
                    p.Shape = UtilityPipeShape.Circular;
                    p.InnerDiameter = Attr(cp, "diameter") * dscale;
                    p.WallThickness = Attr(cp, "thickness") * dscale;
                    if (p.Material.Length == 0) p.Material = (string?)cp.Attribute("material") ?? "";
                }
                else if (Child(pe, "ElliPipe") is { } ep)
                {
                    p.Shape = UtilityPipeShape.Elliptical;
                    p.InnerRise = Attr(ep, "height") * dscale;
                    p.InnerSpan = Attr(ep, "span") * dscale;
                    p.WallThickness = Attr(ep, "thickness") * dscale;
                    if (p.Material.Length == 0) p.Material = (string?)ep.Attribute("material") ?? "";
                }
                else if (Child(pe, "RectPipe") is { } rp)
                {
                    p.Shape = UtilityPipeShape.Box;
                    p.InnerRise = Attr(rp, "height") * dscale;
                    p.InnerSpan = Attr(rp, "width") * dscale;
                    p.WallThickness = Attr(rp, "thickness") * dscale;
                    if (p.Material.Length == 0) p.Material = (string?)rp.Attribute("material") ?? "";
                }
                else p.Unresolved = $"UNRESOLVED ({source}): pipe {p.Name} section - no CircPipe, ElliPipe or RectPipe in the file.";
                // the start invert is the structure's "out" invert for this pipe, the end invert its "in" invert
                if (inverts.TryGetValue((p.Name, "out"), out double i0)) p.InvertStart = i0;
                if (inverts.TryGetValue((p.Name, "in"), out double i1)) p.InvertEnd = i1;
                // a pipe whose ends are not at structures: Center points (start, end) when the file has them
                var centers = Children(pe, "Center").Select(x => Numbers(x.Value)).Where(x => x.Length >= 2).ToList();
                if (p.From.Length == 0 && centers.Count >= 1) { p.StartY = centers[0][0] * scale; p.StartX = centers[0][1] * scale; if (centers[0].Length >= 3 && double.IsNaN(p.InvertStart)) p.InvertStart = centers[0][2] * scale; }
                if (p.To.Length == 0 && centers.Count >= 2) { p.EndY = centers[^1][0] * scale; p.EndX = centers[^1][1] * scale; if (centers[^1].Length >= 3 && double.IsNaN(p.InvertEnd)) p.InvertEnd = centers[^1][2] * scale; }
                net.Pipes.Add(p);
            }
            res.Networks.Add(net);
            res.Messages.Add($"{name}: {net.System} network, {net.Structures.Count} structures, {net.Pipes.Count} pipes.");
        }
        return res;
    }

    /// <summary>Structure kind from the catalog description (Civil 3D part names); unknown = Other (the IFC entity says so).</summary>
    private static UtilityStructureKind KindFrom(string desc)
    {
        string d = desc.ToLowerInvariant();
        if (d.Contains("manhole") || d.Contains("cylindrical") || d.Contains("eccentric")) return UtilityStructureKind.Manhole;
        if (d.Contains("catch basin")) return UtilityStructureKind.CatchBasin;
        if (d.Contains("inlet") || d.Contains("grate")) return UtilityStructureKind.Inlet;
        if (d.Contains("junction")) return UtilityStructureKind.JunctionBox;
        if (d.Contains("headwall") || d.Contains("endwall")) return UtilityStructureKind.Headwall;
        if (d.Contains("cleanout")) return UtilityStructureKind.Cleanout;
        if (d.Contains("outfall") || d.Contains("outlet")) return UtilityStructureKind.Outfall;
        return UtilityStructureKind.Other;
    }

    internal static double Scale(string unit) => unit.ToLowerInvariant() switch
    {
        "meter" => 1, "millimeter" => 0.001, "centimeter" => 0.01, "kilometer" => 1000,
        "foot" or "internationalfoot" => 0.3048, "ussurveyfoot" => 1200.0 / 3937.0, "inch" => 0.0254, "mile" => 1609.344,
        _ => throw new InvalidDataException($"Unsupported unit '{unit}'."),
    };

    internal static IEnumerable<XElement> Descendants(XElement e, string local) => e.Descendants().Where(x => x.Name.LocalName == local);
    private static IEnumerable<XElement> Children(XElement e, string local) => e.Elements().Where(x => x.Name.LocalName == local);
    internal static XElement? Child(XElement e, string local) => e.Elements().FirstOrDefault(x => x.Name.LocalName == local);

    internal static double[] Numbers(string? text) =>
        (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(s => double.Parse(s, NumberStyles.Float, Inv)).ToArray();

    private static double Attr(XElement e, string name)
    {
        var a = (string?)e.Attribute(name);
        return a == null || !double.TryParse(a, NumberStyles.Float, Inv, out double v) ? double.NaN : v;
    }
}

internal static class TupleComparerExtensions
{
    /// <summary>Case-insensitive comparer for (string, string) keys.</summary>
    public static IEqualityComparer<(string, string)> WithTuple(this StringComparer c) => new TupleComparer(c);

    private sealed class TupleComparer(StringComparer c) : IEqualityComparer<(string, string)>
    {
        public bool Equals((string, string) x, (string, string) y) => c.Equals(x.Item1, y.Item1) && c.Equals(x.Item2, y.Item2);
        public int GetHashCode((string, string) o) => HashCode.Combine(c.GetHashCode(o.Item1), c.GetHashCode(o.Item2));
    }
}
