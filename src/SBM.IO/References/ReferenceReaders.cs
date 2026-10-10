using System.Globalization;
using System.Xml;
using SBM.Core.Geometry;
using SBM.Core.References;

namespace SBM.IO.References;

/// <summary>
/// Readers for terrain and reference models. All parsers stream (no DOM), parse numbers from spans and return geometry
/// in meters, scaled by the file's unit (LandXML) or <paramref name="defaultUnit"/> (DXF/OBJ/STL have no units).
/// </summary>
public static class ReferenceReaders
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static ReferenceFormat Detect(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".xml" or ".landxml" => ReferenceFormat.LandXmlSurface,
        ".dxf" => ReferenceFormat.Dxf,
        ".obj" => ReferenceFormat.Obj,
        ".stl" => ReferenceFormat.Stl,
        ".flt" or ".hdr" or ".asc" => ReferenceFormat.Grid,
        _ => throw new InvalidDataException($"Unsupported reference format '{Path.GetExtension(path)}'. Use LandXML, DXF, OBJ, STL or a raster DEM (.flt/.hdr, .asc)."),
    };

    /// <summary>Loads a reference file. <paramref name="defaultUnit"/> = meters per file unit when the file doesn't say.</summary>
    public static ReferenceGeometry Load(ReferenceModelDefinition def, string fullPath, double defaultUnit)
    {
        var fmt = def.Format == ReferenceFormat.Auto ? Detect(fullPath) : def.Format;
        double scale = def.UnitScale > 0 ? def.UnitScale : defaultUnit;
        if (fmt == ReferenceFormat.Grid) return LoadGrid(def, fullPath);
        var g = fmt switch
        {
            ReferenceFormat.LandXmlSurface => ReadLandXmlSurface(fullPath, def.UnitScale > 0 ? def.UnitScale : 0),
            ReferenceFormat.Dxf => ReadDxf(fullPath, scale),
            ReferenceFormat.Obj => ReadObj(fullPath, scale),
            _ => ReadStl(fullPath, scale),
        };
        if (def.OffsetX != 0 || def.OffsetY != 0 || def.OffsetZ != 0)
        {
            var o = new Vec3(def.OffsetX, def.OffsetY, def.OffsetZ);
            for (int i = 0; i < g.Mesh.Positions.Count; i++) g.Mesh.Positions[i] += o;
            for (int k = 0; k < g.Polylines.Count; k++) g.Polylines[k] = g.Polylines[k].Select(p => p + o).ToArray();
        }
        return g;
    }

    private static double ParseD(ReadOnlySpan<char> s) => double.Parse(s, NumberStyles.Float, Inv);

    // ------------------------------------------------------------------ raster DEMs

    /// <summary>Raster DEM → full grid + decimated display mesh. Values and coordinates in meters (or × UnitScale).</summary>
    public static ReferenceGeometry LoadGrid(ReferenceModelDefinition def, string fullPath)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double scale = def.UnitScale > 0 ? def.UnitScale : 1.0;
        var grid = Path.GetExtension(fullPath).ToLowerInvariant() == ".asc" ? ReadAsciiGrid(fullPath, scale, def) : ReadFloatGrid(fullPath, scale, def);
        long read = sw.ElapsedMilliseconds;
        int stride = grid.StrideFor(def.DisplayMaxVertices > 0 ? def.DisplayMaxVertices : 4_000_000);
        var g = new ReferenceGeometry { Grid = grid, Mesh = grid.ToMesh(stride), SourceUnit = scale == 1 ? "meter" : $"{scale} m" };
        g.Messages.Add($"raster DEM {grid.Nx:N0} × {grid.Ny:N0} = {grid.Cells / 1e6:0.0} M cells at {grid.Cell:0.###} m (read {read} ms); " +
                       $"drawn every {stride} cell(s): {g.Mesh.TriangleCount:N0} triangles ({sw.ElapsedMilliseconds} ms). Elevations use the full grid.");
        return g;
    }

    private static Dictionary<string, string> ReadHeader(IEnumerable<string> lines)
    {
        var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var p = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 2) h[p[0].Trim()] = p[1].Trim();
        }
        return h;
    }

    private static ElevationGrid MakeGrid(Dictionary<string, string> h, float[] z, double scale, ReferenceModelDefinition def)
    {
        double D(string k) => double.Parse(h[k], NumberStyles.Float, Inv);
        int nx = (int)D("ncols"), ny = (int)D("nrows");
        double cell = D("cellsize");
        // Corner registration (xllcorner) → cell centers; center registration (xllcenter) as is.
        double x0 = h.ContainsKey("xllcenter") ? D("xllcenter") : D("xllcorner") + cell / 2;
        double y0 = h.ContainsKey("yllcenter") ? D("yllcenter") : D("yllcorner") + cell / 2;
        float nod = h.TryGetValue("NODATA_value", out var nv) ? float.Parse(nv, NumberStyles.Float, Inv) : -9999f;
        float s = (float)scale;
        System.Threading.Tasks.Parallel.For(0, ny, j =>
        {
            int o = j * nx;
            for (int i = 0; i < nx; i++)
            {
                float v = z[o + i];
                z[o + i] = v == nod || float.IsNaN(v) ? float.NaN : v * s;
            }
        });
        return new ElevationGrid
        {
            Nx = nx, Ny = ny, Cell = cell * scale, X0 = x0 * scale + def.OffsetX, Y0 = y0 * scale + def.OffsetY, Z = z, ZOffset = def.OffsetZ,
        };
    }

    /// <summary>ESRI float grid: .hdr (ncols, nrows, xllcorner/xllcenter, yllcorner, cellsize, NODATA_value, byteorder) + .flt float32.</summary>
    public static ElevationGrid ReadFloatGrid(string path, double scale, ReferenceModelDefinition def)
    {
        string hdr = Path.ChangeExtension(path, ".hdr"), flt = Path.ChangeExtension(path, ".flt");
        var h = ReadHeader(File.ReadLines(hdr));
        int nx = int.Parse(h["ncols"], Inv), ny = int.Parse(h["nrows"], Inv);
        var z = new float[(long)nx * ny];
        using (var fs = new FileStream(flt, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
        {
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(z.AsSpan());
            int got = 0;
            while (got < bytes.Length)
            {
                int n = fs.Read(bytes[got..]);
                if (n == 0) throw new InvalidDataException($"{Path.GetFileName(flt)} is shorter than {nx} × {ny} values.");
                got += n;
            }
        }
        if (h.TryGetValue("byteorder", out var bo) && bo.StartsWith("MSB", StringComparison.OrdinalIgnoreCase) == BitConverter.IsLittleEndian)
        {
            var ints = System.Runtime.InteropServices.MemoryMarshal.Cast<float, int>(z.AsSpan());
            for (int k = 0; k < ints.Length; k++) ints[k] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(ints[k]);
        }
        return MakeGrid(h, z, scale, def);
    }

    /// <summary>ESRI ASCII grid (.asc).</summary>
    public static ElevationGrid ReadAsciiGrid(string path, double scale, ReferenceModelDefinition def)
    {
        using var r = new StreamReader(path, System.Text.Encoding.ASCII, false, 1 << 20);
        var headerLines = new List<string>();
        string? line;
        while ((line = r.ReadLine()) != null)
        {
            if (line.Length > 0 && (char.IsDigit(line.TrimStart()[0]) || line.TrimStart()[0] == '-')) break;
            headerLines.Add(line);
        }
        var h = ReadHeader(headerLines);
        int nx = int.Parse(h["ncols"], Inv), ny = int.Parse(h["nrows"], Inv);
        var z = new float[(long)nx * ny];
        long k = 0;
        while (line != null && k < z.Length)
        {
            foreach (var tok in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                if (k < z.Length) z[k++] = float.Parse(tok, NumberStyles.Float, Inv);
            line = r.ReadLine();
        }
        return MakeGrid(h, z, scale, def);
    }

    // ------------------------------------------------------------------ LandXML TIN surfaces

    /// <summary>Reads every Surface/Definition (Pnts + Faces) in a LandXML file. Invisible faces (i="1") are skipped.</summary>
    public static ReferenceGeometry ReadLandXmlSurface(string path, double unitOverride = 0)
    {
        var g = new ReferenceGeometry();
        double scale = unitOverride;
        using var reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore });
        var idMap = new Dictionary<string, int>();
        int surfaces = 0, skipped = 0;
        reader.Read();
        while (!reader.EOF)
        {
            // ReadElementContentAsString advances past the element itself, so only call Read() when nothing was consumed.
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName is not ("P" or "F")) { if (reader.NodeType == XmlNodeType.Element) Handle(); reader.Read(); continue; }
            Handle();
        }
        void Handle()
        {
            switch (reader.LocalName)
            {
                case "Imperial":
                case "Metric":
                    if (scale <= 0)
                    {
                        string unit = reader.GetAttribute("linearUnit") ?? (reader.LocalName == "Imperial" ? "foot" : "meter");
                        g.SourceUnit = unit;
                        scale = unit.ToLowerInvariant() switch
                        {
                            "ussurveyfoot" => 1200.0 / 3937.0,
                            "foot" or "internationalfoot" => 0.3048,
                            "millimeter" => 0.001,
                            "centimeter" => 0.01,
                            _ => 1.0,
                        };
                    }
                    break;
                case "Surface":
                    surfaces++;
                    idMap.Clear();
                    break;
                case "P":
                {
                    string id = reader.GetAttribute("id") ?? (idMap.Count + 1).ToString(Inv);
                    var text = reader.ReadElementContentAsString().AsSpan();
                    Span<double> v = stackalloc double[3];
                    int n = ParseNumbers(text, v);
                    if (n < 2) return;
                    double s = scale > 0 ? scale : 1;
                    idMap[id] = g.Mesh.Add(new Vec3(v[1] * s, v[0] * s, (n > 2 ? v[2] : 0) * s));
                    break;
                }
                case "F":
                {
                    bool invisible = reader.GetAttribute("i") == "1";
                    var text = reader.ReadElementContentAsString();
                    if (invisible) { skipped++; return; }
                    var ids = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (ids.Length >= 3 && idMap.TryGetValue(ids[0], out int a) && idMap.TryGetValue(ids[1], out int b) && idMap.TryGetValue(ids[2], out int c))
                        g.Mesh.Tri(a, b, c);
                    break;
                }
            }
        }
        if (scale <= 0) g.Messages.Add("No <Units> in the file; coordinates were read as meters.");
        g.Messages.Add($"{surfaces} surface(s), {g.Mesh.Positions.Count:N0} points, {g.Mesh.TriangleCount:N0} triangles" + (skipped > 0 ? $", {skipped:N0} invisible faces skipped" : "") + ".");
        return g;
    }

    private static int ParseNumbers(ReadOnlySpan<char> text, Span<double> dst)
    {
        int n = 0, i = 0;
        while (i < text.Length && n < dst.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            if (i > start) dst[n++] = ParseD(text[start..i]);
        }
        return n;
    }

    // ------------------------------------------------------------------ DXF (3DFACE, LINE, POLYLINE/VERTEX, LWPOLYLINE)

    public static ReferenceGeometry ReadDxf(string path, double scale)
    {
        var g = new ReferenceGeometry { SourceUnit = "drawing units" };
        using var r = new StreamReader(path);
        string? code, value;
        bool inEntities = false;
        string? entity = null;
        var pts = new Dictionary<int, double>();
        List<Vec3>? poly = null;
        List<Vec3>? lw = null;
        bool polyClosed = false, lwClosed = false;
        double elevation = 0;
        int faces = 0, lines = 0;

        void Flush()
        {
            if (entity == null) return;
            double V(int c) => pts.TryGetValue(c, out double x) ? x * scale : 0;
            switch (entity)
            {
                case "3DFACE":
                {
                    var p0 = new Vec3(V(10), V(20), V(30));
                    var p1 = new Vec3(V(11), V(21), V(31));
                    var p2 = new Vec3(V(12), V(22), V(32));
                    var p3 = new Vec3(V(13), V(23), V(33));
                    int a = g.Mesh.Add(p0), b = g.Mesh.Add(p1), c = g.Mesh.Add(p2);
                    g.Mesh.Tri(a, b, c);
                    if ((p3 - p2).Length > 1e-9) { int d = g.Mesh.Add(p3); g.Mesh.Tri(a, c, d); }
                    faces++;
                    break;
                }
                case "LINE":
                    g.Polylines.Add(new[] { new Vec3(V(10), V(20), V(30)), new Vec3(V(11), V(21), V(31)) });
                    lines++;
                    break;
                case "VERTEX":
                    poly?.Add(new Vec3(V(10), V(20), V(30)));
                    break;
                case "LWPOLYLINE":
                    if (lw is { Count: > 1 })
                    {
                        if (lwClosed) lw.Add(lw[0]);
                        g.Polylines.Add(lw.ToArray());
                        lines++;
                    }
                    lw = null;
                    break;
            }
            pts.Clear();
        }

        while ((code = r.ReadLine()) != null && (value = r.ReadLine()) != null)
        {
            int c = int.Parse(code.AsSpan().Trim(), Inv);
            value = value.Trim();
            if (c == 0)
            {
                Flush();
                if (value == "SECTION") { entity = null; continue; }
                if (value == "ENDSEC") { inEntities = false; entity = null; continue; }
                if (value == "EOF") break;
                if (value == "SEQEND")
                {
                    if (poly is { Count: > 1 })
                    {
                        if (polyClosed) poly.Add(poly[0]);
                        g.Polylines.Add(poly.ToArray());
                        lines++;
                    }
                    poly = null;
                    entity = null;
                    continue;
                }
                entity = inEntities ? value : null;
                if (entity == "POLYLINE") { poly = new List<Vec3>(); polyClosed = false; }
                if (entity == "LWPOLYLINE") { lw = new List<Vec3>(); lwClosed = false; elevation = 0; }
                continue;
            }
            if (c == 2 && value == "ENTITIES") { inEntities = true; continue; }
            if (entity == null) continue;
            if (entity == "POLYLINE" && c == 70) polyClosed = (int.Parse(value, Inv) & 1) != 0;
            if (entity == "LWPOLYLINE")
            {
                if (c == 70) lwClosed = (int.Parse(value, Inv) & 1) != 0;
                else if (c == 38) elevation = double.Parse(value, Inv);
                else if (c == 10) pts[10] = double.Parse(value, Inv);
                else if (c == 20 && pts.TryGetValue(10, out double x)) { lw!.Add(new Vec3(x * scale, double.Parse(value, Inv) * scale, elevation * scale)); pts.Remove(10); }
                continue;
            }
            if (c is >= 10 and <= 39) pts[c] = double.Parse(value, Inv);
        }
        Flush();
        g.Messages.Add($"{faces:N0} faces, {lines:N0} lines/polylines.");
        return g;
    }

    // ------------------------------------------------------------------ OBJ

    public static ReferenceGeometry ReadObj(string path, double scale)
    {
        var g = new ReferenceGeometry { SourceUnit = "model units" };
        var verts = new List<Vec3>();
        Span<double> v = stackalloc double[3];
        foreach (var line in File.ReadLines(path))
        {
            var s = line.AsSpan().TrimStart();
            if (s.StartsWith("v "))
            {
                int n = ParseNumbers(s[2..], v);
                verts.Add(new Vec3(v[0] * scale, v[1] * scale, (n > 2 ? v[2] : 0) * scale));
            }
            else if (s.StartsWith("f "))
            {
                var parts = s[2..].ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var idx = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    int slash = parts[i].IndexOf('/');
                    int k = int.Parse(slash >= 0 ? parts[i][..slash] : parts[i], Inv);
                    idx[i] = k > 0 ? k - 1 : verts.Count + k;
                }
                int b0 = g.Mesh.Add(verts[idx[0]]);
                for (int i = 1; i + 1 < idx.Length; i++)
                {
                    int b1 = g.Mesh.Add(verts[idx[i]]), b2 = g.Mesh.Add(verts[idx[i + 1]]);
                    g.Mesh.Tri(b0, b1, b2);
                }
            }
        }
        g.Messages.Add($"{verts.Count:N0} vertices, {g.Mesh.TriangleCount:N0} triangles.");
        return g;
    }

    // ------------------------------------------------------------------ STL (binary or ASCII)

    public static ReferenceGeometry ReadStl(string path, double scale)
    {
        var g = new ReferenceGeometry { SourceUnit = "model units" };
        var bytes = File.ReadAllBytes(path);
        bool ascii = bytes.Length > 5 && System.Text.Encoding.ASCII.GetString(bytes, 0, 5) == "solid"
                     && System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1000)).Contains("facet");
        if (!ascii && bytes.Length >= 84)
        {
            int n = BitConverter.ToInt32(bytes, 80);
            for (int t = 0; t < n && 84 + t * 50 + 50 <= bytes.Length; t++)
            {
                int o = 84 + t * 50 + 12;
                int a = g.Mesh.Add(V(o)), b = g.Mesh.Add(V(o + 12)), c = g.Mesh.Add(V(o + 24));
                g.Mesh.Tri(a, b, c);
            }
            Vec3 V(int o) => new(BitConverter.ToSingle(bytes, o) * scale, BitConverter.ToSingle(bytes, o + 4) * scale, BitConverter.ToSingle(bytes, o + 8) * scale);
        }
        else
        {
            var tri = new List<Vec3>(3);
            Span<double> v = stackalloc double[3];
            foreach (var line in File.ReadLines(path))
            {
                var s = line.AsSpan().Trim();
                if (!s.StartsWith("vertex")) continue;
                ParseNumbers(s[6..], v);
                tri.Add(new Vec3(v[0] * scale, v[1] * scale, v[2] * scale));
                if (tri.Count == 3)
                {
                    int a = g.Mesh.Add(tri[0]), b = g.Mesh.Add(tri[1]), c = g.Mesh.Add(tri[2]);
                    g.Mesh.Tri(a, b, c);
                    tri.Clear();
                }
            }
        }
        g.Messages.Add($"{g.Mesh.TriangleCount:N0} triangles.");
        return g;
    }
}
