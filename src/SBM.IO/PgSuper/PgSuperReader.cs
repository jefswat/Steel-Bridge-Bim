using System.Globalization;
using System.Xml.Linq;
using SBM.Core.Bridge;
using SBM.Core.Prestressed;
using static SBM.Core.Units.UnitSystem;

namespace SBM.IO.PgSuper;

/// <summary>One PGSuper pier (abutments are the first and last).</summary>
/// <remarks>End distances are normalized to "CL bearing to end of girder" (PGSuper may measure them from the pier line).</remarks>
public sealed record PgsPier(double Station, double SkewDeg, string Orientation,
    double BackBearingOffset, double BackEndDistance, double AheadBearingOffset, double AheadEndDistance);

/// <summary>One harped / straight strand position from the girder library grid (one or two strands: X &gt; 0 = a pair).</summary>
public sealed record PgsGridPoint(double X, double Y, double Xhp, double Yhp, double Xend, double Yend);

/// <summary>Per-girder (segment) prestressing and concrete.</summary>
public sealed class PgsGirderData
{
    public int Index { get; init; }
    public int StrandInputType { get; init; }
    public int NumStraight { get; init; }
    public int NumHarped { get; init; }
    public int NumPermanent { get; init; }
    public int EndMeasure { get; init; } = -1;
    public double EndOffset { get; init; }
    public int HpMeasure { get; init; } = -1;
    public double HpOffset { get; init; }
    public double StrandDiameter { get; init; }
    public double FcKsi { get; init; }
    public double FciKsi { get; init; }
    public int DebondedStraight { get; init; }
    /// <summary>Straight strand debonding (PGSuper CDebondData "DebondInfo": Strand = straight grid index, Length1 / Length2 =
    /// debonded length at the start / end of the girder, m). A pair grid entry debonds both strands.</summary>
    public List<(int GridIndex, double Start, double End)> Debond { get; init; } = new();
    public string Key => $"{StrandInputType}/{NumStraight}/{NumHarped}/{NumPermanent}/{EndMeasure}:{EndOffset:0.####}/{HpMeasure}:{HpOffset:0.####}/{StrandDiameter:0.####}/{FcKsi:0.##}/{FciKsi:0.##}";
}

/// <summary>The bridge description read from a PGSuper project (all SI: m, Pa) — geometry only.</summary>
public sealed class PgsBridge
{
    public string Source { get; init; } = "";
    public string Version { get; init; } = "";
    public string BridgeName { get; init; } = "";
    public string GirderName { get; init; } = "";
    public string SectionName { get; set; } = "";
    public PrecastIBeamDims? Dims { get; set; }
    public List<PgsPier> Piers { get; } = new();
    public int GirderCount { get; set; }
    public List<double> Spacings { get; } = new();
    /// <summary>pgsTypes::MeasurementType: 0 = normal to the alignment/pier, 1 = along the pier.</summary>
    public int SpacingMeasureType { get; set; }
    public int RefGirder { get; set; } = -1;
    public double RefGirderOffset { get; set; }
    public int RefGirderOffsetType { get; set; }
    public double AlignmentOffset { get; set; }
    public double SlabThickness { get; set; }
    public double OverhangDepth { get; set; }
    public double LeftEdge { get; set; }
    public double RightEdge { get; set; }
    public int DeckEdgeMeasureType { get; set; }
    /// <summary>"A" dimension: top of slab to top of girder at the CL bearing.</summary>
    public double SlabOffset { get; set; }
    public double Fillet { get; set; }
    public double PadLength { get; set; }
    public double PadWidth { get; set; }
    public double PadHeight { get; set; }
    public string LeftRail { get; set; } = "";
    public string RightRail { get; set; } = "";
    public List<PgsGridPoint> StraightGrid { get; } = new();
    public List<PgsGridPoint> HarpedGrid { get; } = new();
    public List<(bool Harped, int Index)> GlobalOrder { get; } = new();
    public double HarpPointLocation { get; set; } = 0.05;
    public int HarpPointReference { get; set; } = 2;
    public int HarpPointMeasure { get; set; }
    /// <summary>Library allows an odd number of harped strands (the odd one sits at the CL).</summary>
    public bool OddHarped { get; set; }
    public List<PgsGirderData> Girders { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Reads the bridge description from a PGSuper / BridgeLink project file (*.pgs, XML, PGSuper 6+ "BridgeDescription"
/// schema) or from the ReportExportAgent export (*_export.xml — its &lt;Input&gt; holds the original .pgs tree).
/// </summary>
public static class PgSuperReader
{
    private const double KsiPa = 6894757.293168;

    public static PgsBridge Read(string path)
    {
        var doc = XDocument.Load(path);
        // The project root (direct file) or the copy inside an export file.
        var root = doc.Root?.Name.LocalName == "PGSuper" ? doc.Root : doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "PGSuper");
        if (root == null) throw new InvalidDataException("Not a PGSuper project (no <PGSuper> element).");
        var bd = root.Descendants("BridgeDescription").FirstOrDefault()
            ?? throw new InvalidDataException("This PGSuper file uses the pre-6.0 schema (no BridgeDescription). Open and save it in PGSuper/BridgeLink 6 or newer first.");
        var b = new PgsBridge
        {
            Source = path,
            Version = root.Element("Version")?.Value ?? root.Attribute("version")?.Value ?? "",
            BridgeName = root.Descendants("ProjectProperties").FirstOrDefault()?.Element("BridgeName")?.Value ?? "",
            GirderName = bd.Element("Girder")?.Value ?? "",
        };

        // ---- girder layout
        b.Spacings.Add(D(bd, "GirderSpacing"));
        b.SpacingMeasureType = (int)D(bd, "MeasurementType");
        b.RefGirder = (int)D(bd, "RefGirder", -1);
        b.RefGirderOffset = D(bd, "RefGirderOffset");
        b.RefGirderOffsetType = (int)D(bd, "RefGirderOffsetType");
        b.AlignmentOffset = D(bd, "AlignmentOffset");
        b.SlabOffset = D(bd, "SlabOffset");
        b.Fillet = D(bd, "Fillet");
        if ((int)D(bd, "GirderSpacingType") != 0)
            b.Warnings.Add("Non-uniform girder spacing: the first spacing is used for all bays.");

        // ---- piers
        foreach (var p in bd.Element("Piers")?.Elements("PierDataDetails") ?? Enumerable.Empty<XElement>())
        {
            var back = p.Element("Back");
            var ahead = p.Element("Ahead");
            string orient = p.Element("Orientation")?.Value ?? "NORMAL";
            // End distance → CL bearing to end of girder (FromPier… types measure from the pier line).
            double End(XElement? side)
            {
                double e = D(side, "GirderEndDistance"), off = D(side, "GirderBearingOffset");
                return (side?.Element("EndDistanceMeasurementType")?.Value ?? "").StartsWith("FromPier") ? off - e : e;
            }
            b.Piers.Add(new PgsPier(D(p, "Station"), ParseSkew(orient, b.Warnings), orient,
                D(back, "GirderBearingOffset"), End(back), D(ahead, "GirderBearingOffset"), End(ahead)));
            if (b.Piers.Count == 1 && ahead?.Descendants("BearingData2").FirstOrDefault() is { } brg)
            {
                b.PadLength = D(brg, "Length");
                b.PadWidth = D(brg, "Width");
                int n = (int)D(brg, "NumIntLayers");
                b.PadHeight = 2 * D(brg, "CoverThickness") + n * D(brg, "ElastomerThickness") + (n + 1) * D(brg, "ShimThickness");
            }
        }
        if (b.Piers.Count < 2) throw new InvalidDataException("The PGSuper bridge has fewer than two piers.");

        // ---- deck and railings
        var deck = bd.Element("Deck");
        b.SlabThickness = D(deck, "GrossDepth");
        b.OverhangDepth = D(deck, "LeftOverhangEdgeDepth");
        if (deck?.Element("DeckEdgePoints")?.Elements("DeckPoint").ToList() is { Count: > 0 } pts)
        {
            b.LeftEdge = D(pts[0], "LeftEdge");
            b.RightEdge = D(pts[0], "RightEdge");
            b.DeckEdgeMeasureType = (int)D(pts[0], "MeasurementType");
            if (pts.Count > 1) b.Warnings.Add($"{pts.Count} deck edge points: only the first (constant width) is used.");
        }
        b.LeftRail = bd.Element("LeftRailingSystem")?.Descendants("ExteriorRailingName").FirstOrDefault()?.Value ?? "";
        b.RightRail = bd.Element("RightRailingSystem")?.Descendants("ExteriorRailingName").FirstOrDefault()?.Value ?? "";

        // ---- girders (first group; one segment each)
        var groups = bd.Element("GirderGroups")?.Elements("GirderGroup").ToList() ?? new();
        if (groups.Count > 1) b.Warnings.Add($"{groups.Count} girder groups: the first group's girders are used for every span.");
        var girders = groups.FirstOrDefault()?.Element("Girders")?.Elements("Girder").ToList() ?? new();
        b.GirderCount = girders.Count > 0 ? girders.Count : (int)D(groups.FirstOrDefault()?.Element("Girders"), "Count");
        for (int i = 0; i < girders.Count; i++)
        {
            var seg = girders[i].Element("PrecastSegment");
            var ps = seg?.Element("PrestressData");
            var conc = seg?.Element("Concrete");
            if (ps == null) continue;
            int key = (int)D(ps, "StraightStrandMaterialKey", 66054);
            b.Girders.Add(new PgsGirderData
            {
                Index = i,
                StrandInputType = (int)D(ps, "NumPermStrandsType"),
                NumStraight = (int)D(ps, "NumStraightStrands"),
                NumHarped = (int)D(ps, "NumHarpedStrands"),
                NumPermanent = (int)D(ps, "NumPermanentStrands"),
                EndMeasure = (int)D(ps, "HsoEndMeasurement", -1),
                EndOffset = D(ps, "HpOffsetAtStart"),
                HpMeasure = (int)D(ps, "HsoHpMeasurement", -1),
                HpOffset = D(ps, "HpOffsetAtHp1"),
                StrandDiameter = StrandDiameter(key),
                FcKsi = D(conc, "Fc") / KsiPa,
                FciKsi = D(conc, "Fci") / KsiPa,
                DebondedStraight = (int)D(ps.Element("StraightStrandDebonding"), "DebondInfoCount"),
                Debond = (ps.Element("StraightStrandDebonding")?.Elements("DebondInfo") ?? Enumerable.Empty<XElement>())
                    .Select(x => ((int)D(x, "Strand", -1), D(x, "Length1"), D(x, "Length2"))).Where(x => x.Item1 >= 0).ToList(),
            });
        }

        // ---- girder library entry: section and strand grid
        var lib = root.Descendants("GirderLibraryEntry").FirstOrDefault(e => e.Element("Name")?.Value == b.GirderName)
            ?? root.Descendants("GirderLibraryEntry").FirstOrDefault();
        if (lib != null)
        {
            b.SectionName = lib.Element("SectionName")?.Value ?? "";
            if (lib.Descendants("IBeamDimensions").FirstOrDefault() is { } dim)
            {
                double G(string n) => D(dim, n);
                b.Dims = new PrecastIBeamDims
                {
                    Name = b.GirderName, D1 = G("D1"), D2 = G("D2"), D3 = G("D3"), D4 = G("D4"), D5 = G("D5"), D6 = G("D6"), H = G("H"),
                    T1 = G("T1"), T2 = G("T2"), W1 = G("W1"), W2 = G("W2"), W3 = G("W3"), W4 = G("W4"), W5 = G("W5"), C1 = G("C1"),
                };
            }
            else b.Warnings.Add($"Girder '{b.GirderName}' is a {lib.Element("SectionName")?.Value} — only precast I-beams are modeled; the section was not read.");
            foreach (var s in lib.Element("StraightStrands")?.Elements("StrandLocation") ?? Enumerable.Empty<XElement>())
                b.StraightGrid.Add(new PgsGridPoint(D(s, "Xstart"), D(s, "Ystart"), D(s, "Xstart"), D(s, "Ystart"), D(s, "Xend"), D(s, "Yend")));
            foreach (var s in lib.Element("HarpedStrands")?.Elements("StrandLocation") ?? Enumerable.Empty<XElement>())
                b.HarpedGrid.Add(new PgsGridPoint(D(s, "Xstart"), D(s, "Ystart"), D(s, "Xhp"), D(s, "Yhp"), D(s, "Xend"), D(s, "Yend")));
            foreach (var g in lib.Elements("GlobalStrandOrder"))
                b.GlobalOrder.Add((g.Element("StrandType")?.Value == "Harped", (int)D(g, "LocalSortOrder")));
            b.HarpPointLocation = D(lib, "HarpingPointLocation", 0.05);
            b.HarpPointReference = (int)D(lib, "HarpingPointReference", 2);
            b.HarpPointMeasure = (int)D(lib, "HarpingPointMeasure");
            b.OddHarped = D(lib, "OddNumberOfHarpedStrands") != 0;
        }
        else b.Warnings.Add($"Girder library entry '{b.GirderName}' was not found in the file.");
        return b;
    }

    /// <summary>Strand diameter from a WBFL strand pool key (size bits 0x10–0x1000).</summary>
    public static double StrandDiameter(int key) => (key & 0x1FF0) switch
    {
        0x0010 => In(0.25), 0x0020 => In(5.0 / 16), 0x0040 => In(0.375), 0x0080 => In(7.0 / 16), 0x0100 => In(0.5),
        0x0200 => In(0.6), 0x0400 => In(0.7), 0x0800 => In(0.52), 0x1000 => In(0.62), _ => In(0.6),
    };

    /// <summary>
    /// Pier orientation → skew in degrees (SBM convention: positive = right end of the support line up-station).
    /// "NORMAL", a skew "15 30 00.0 L|R" (L = counter-clockwise from the normal = positive), or a bearing "N 45 00 00 E"
    /// (returned as NaN — needs the alignment direction).
    /// </summary>
    public static double ParseSkew(string orientation, List<string>? warnings = null)
    {
        var o = orientation.Trim().ToUpperInvariant();
        if (o is "" or "NORMAL") return 0;
        var parts = o.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if ((parts[0] == "N" || parts[0] == "S") && (parts[^1] == "E" || parts[^1] == "W")) return double.NaN;
        double sign = parts[^1] == "R" ? -1 : 1;
        var nums = parts.Where(p => p is not ("L" or "R")).Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        double deg = nums.Length > 0 ? nums[0] : 0;
        if (nums.Length > 1) deg += nums[1] / 60;
        if (nums.Length > 2) deg += nums[2] / 3600;
        return sign * deg;
    }

    private static double D(XElement? e, string name, double fallback = 0) =>
        e?.Element(name) is { } x && double.TryParse(x.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
