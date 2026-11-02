using System.Globalization;
using System.IO.Compression;
using System.Text;
using SBM.Core.Drawing.Projection;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;

namespace SBM.IO.Kml;

/// <summary>Options of <see cref="KmzExporter"/>.</summary>
public sealed class KmzOptions
{
    /// <summary>Triangle budget for the whole file (after simplification); the vertex grid is coarsened until it fits.</summary>
    public int MaxTriangles { get; set; } = 150_000;
    /// <summary>Starting vertex grid (m): vertices are snapped to it, collapsed triangles dropped (vertex clustering).</summary>
    public double CellSize { get; set; } = 0.10;
    /// <summary>Alignments as lines (profile heights).</summary>
    public bool Alignments { get; set; } = true;
    /// <summary>Bridge names to write (null = all).</summary>
    public IReadOnlyList<string>? Bridges { get; set; }
}

public sealed class KmzResult
{
    public int Bridges { get; set; }
    public int Triangles { get; set; }
    public int SourceTriangles { get; set; }
    public double CellSize { get; set; }
    public int Alignments { get; set; }
    public long Bytes { get; set; }
    public List<string> Notes { get; } = new();
}

/// <summary>
/// Google Earth KMZ (doc.kml + one COLLADA model per bridge) at LOW resolution: solids only (no bars, strands, fasteners, rivets, welds, studs), every
/// mesh simplified by vertex clustering on a grid that is coarsened until the whole file fits <see cref="KmzOptions.MaxTriangles"/>. Needs a georeference
/// whose CRS is in <see cref="MapProjections"/>: project (m) -> map (GridScale about the scale centre) -> NAD83 lon / lat (taken as WGS84) -> local
/// east-north-up of the bridge's model origin (what Google Earth uses for a placed model). Heights: the model elevations (metres, the project's vertical
/// datum) as KML absolute altitudes - Google Earth's heights are EGM96, so NAVD88 / NGVD29 models sit within about a metre.
/// </summary>
public static class KmzExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static KmzResult Export(string path, BridgeProject project, IReadOnlyList<BridgeModel> models, KmzOptions? options = null)
    {
        options ??= new KmzOptions();
        var geo = GeoTransform.For(project.Georeference)
            ?? throw new InvalidOperationException(project.Georeference == null
                ? "KMZ export needs a georeference (Project.Georeference with a CRS): the model is on a local grid."
                : $"KMZ export: CRS '{project.Georeference.CrsName}' is not in the projection table (SBM.Core/Geometry/MapProjection.cs).");
        var res = new KmzResult();
        // solids per bridge, grouped by colour
        var opt = new ProjectionSourceOptions { Bolts = false, Welds = false, Rebar = false };
        var bridges = new List<(string name, List<(Mesh mesh, (byte r, byte g, byte b) c)> items)>();
        foreach (var m in models)
        {
            if (options.Bridges != null && !options.Bridges.Contains(m.Definition.Name, StringComparer.OrdinalIgnoreCase)) continue;
            var items = ModelProjectionSource.Items(m, opt).Where(i => i.Mesh is { IsEmpty: false }).Select(i => (i.Mesh!, Colour(i))).ToList();
            if (items.Count == 0) continue;
            res.SourceTriangles += items.Sum(i => i.Item1.Indices.Count / 3);
            bridges.Add((m.Definition.Name, items));
        }
        // coarsen the grid until the budget fits
        double cell = options.CellSize > 0 ? options.CellSize : 0.1;
        List<(string name, Dictionary<(byte, byte, byte), (List<Vec3> v, List<int> t)> groups)> simplified;
        for (int pass = 0; ; pass++)
        {
            simplified = bridges.Select(b => (b.name, Cluster(b.items, cell))).ToList();
            int n = simplified.Sum(b => b.groups.Values.Sum(g => g.t.Count / 3));
            if (n <= options.MaxTriangles || pass >= 12) { res.Triangles = n; break; }
            cell *= 1.5;
        }
        res.CellSize = cell;
        var kml = new StringBuilder();
        kml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<kml xmlns=\"http://www.opengis.net/kml/2.2\">\n<Document>\n");
        kml.Append($"<name>{X(Path.GetFileNameWithoutExtension(path))}</name>\n");
        kml.Append($"<description>{X($"Steel Bridge Modeler, low resolution (vertex grid {cell * 1000:0} mm, {res.Triangles:N0} triangles; no bars, fasteners or welds). " +
            $"CRS {project.Georeference!.CrsName}, heights = model elevations ({(project.Georeference.VerticalDatum is { Length: > 0 } vd ? vd : "vertical datum not stated")}) as absolute altitudes.")}</description>\n");
        using var fs = File.Create(path);
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            int bi = 0;
            foreach (var (name, groups) in simplified)
            {
                var all = groups.Values.SelectMany(g => g.v).ToList();
                if (all.Count == 0) continue;
                double cx = (all.Min(p => p.X) + all.Max(p => p.X)) / 2, cy = (all.Min(p => p.Y) + all.Max(p => p.Y)) / 2, cz = all.Min(p => p.Z);
                var (lon0, lat0) = geo.LonLat(cx, cy);
                var enu = new LocalEnu(lon0, lat0, cz);
                string file = $"models/bridge{++bi}.dae";
                var e = zip.CreateEntry(file, CompressionLevel.Optimal);
                using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                    WriteDae(w, name, groups, p => { var (lo, la) = geo.LonLat(p.X, p.Y); return enu.ToEnu(lo, la, p.Z); });
                kml.Append($"<Placemark><name>{X(name)}</name><Model><altitudeMode>absolute</altitudeMode><Location><longitude>{F(lon0, 9)}</longitude><latitude>{F(lat0, 9)}</latitude>" +
                           $"<altitude>{F(cz, 3)}</altitude></Location><Orientation><heading>0</heading><tilt>0</tilt><roll>0</roll></Orientation><Scale><x>1</x><y>1</y><z>1</z></Scale>" +
                           $"<Link><href>{file}</href></Link></Model></Placemark>\n");
                res.Bridges++;
            }
            if (options.Alignments)
            {
                var used = models.Select(m => m.Definition.AlignmentName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                kml.Append("<Style id=\"al\"><LineStyle><color>ff00d7ff</color><width>2</width></LineStyle></Style>\n");
                foreach (var al in project.Alignments.Where(a => used.Contains(a.Name) && a.Length > 0))
                {
                    var sb = new StringBuilder();
                    int n = Math.Max(2, (int)Math.Ceiling(al.Length / 10.0) + 1);
                    bool prof = !al.Profile.IsEmpty;
                    for (int i = 0; i < n; i++)
                    {
                        double d = al.Length * i / (n - 1);
                        var p = al.PointAtDistance(d, 0);
                        var (lo, la) = geo.LonLat(p.X, p.Y);
                        double z = prof ? al.ElevationAt(al.DistanceToStation(d)) : 0;
                        sb.Append(F(lo, 9)).Append(',').Append(F(la, 9)).Append(',').Append(F(z, 3)).Append(' ');
                    }
                    kml.Append($"<Placemark><name>{X(al.Name)}</name><styleUrl>#al</styleUrl><LineString><altitudeMode>{(prof ? "absolute" : "clampToGround")}</altitudeMode>" +
                               $"<coordinates>{sb.ToString().TrimEnd()}</coordinates></LineString></Placemark>\n");
                    res.Alignments++;
                }
            }
            kml.Append("</Document>\n</kml>\n");
            var ke = zip.CreateEntry("doc.kml", CompressionLevel.Optimal);
            using var kw = new StreamWriter(ke.Open(), new UTF8Encoding(false));
            kw.Write(kml.ToString());
        }
        res.Bytes = new FileInfo(path).Length;
        if (res.Triangles > options.MaxTriangles) res.Notes.Add($"still {res.Triangles:N0} triangles after 12 coarsening passes (budget {options.MaxTriangles:N0})");
        return res;
    }

    /// <summary>Vertex clustering: snap to the grid, merge, drop collapsed / repeated triangles (per colour group, shared vertices).</summary>
    private static Dictionary<(byte, byte, byte), (List<Vec3> v, List<int> t)> Cluster(List<(Mesh mesh, (byte r, byte g, byte b) c)> items, double cell)
    {
        var groups = new Dictionary<(byte, byte, byte), (List<Vec3> v, List<int> t)>();
        var index = new Dictionary<(byte, byte, byte), Dictionary<(long, long, long), int>>();
        var tris = new Dictionary<(byte, byte, byte), HashSet<(int, int, int)>>();
        foreach (var (mesh, c) in items)
        {
            if (!groups.TryGetValue(c, out var g)) { g = (new List<Vec3>(), new List<int>()); groups[c] = g; index[c] = new(); tris[c] = new(); }
            var idx = index[c]; var seen = tris[c];
            var map = new int[mesh.Positions.Count];
            for (int i = 0; i < map.Length; i++)
            {
                var p = mesh.Positions[i];
                var k = ((long)Math.Round(p.X / cell), (long)Math.Round(p.Y / cell), (long)Math.Round(p.Z / cell));
                if (!idx.TryGetValue(k, out int vi)) { vi = g.v.Count; g.v.Add(new Vec3(k.Item1 * cell, k.Item2 * cell, k.Item3 * cell)); idx[k] = vi; }
                map[i] = vi;
            }
            for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
            {
                int a = map[mesh.Indices[i]], b = map[mesh.Indices[i + 1]], d = map[mesh.Indices[i + 2]];
                if (a == b || b == d || a == d) continue;
                // the same triangle once (either winding: a collapsed thin plate leaves its two faces on top of each other)
                int lo = Math.Min(a, Math.Min(b, d)), hi = Math.Max(a, Math.Max(b, d)), mid = a + b + d - lo - hi;
                if (!seen.Add((lo, mid, hi))) continue;
                g.t.Add(a); g.t.Add(b); g.t.Add(d);
            }
        }
        // drop vertices no triangle uses
        foreach (var c in groups.Keys.ToList())
        {
            var (v, t) = groups[c];
            var used = new int[v.Count]; Array.Fill(used, -1);
            var nv = new List<Vec3>();
            for (int i = 0; i < t.Count; i++) { int o = t[i]; if (used[o] < 0) { used[o] = nv.Count; nv.Add(v[o]); } t[i] = used[o]; }
            groups[c] = (nv, t);
        }
        return groups;
    }

    private static (byte, byte, byte) Colour(ProjectionItem i)
    {
        foreach (var l in ModelLayers.Steel) if (l.name == i.Layer) return (l.r, l.g, l.b);
        string n = i.Layer.ToUpperInvariant();
        if (n.Contains("DECK") || n.Contains("HAUNCH") || n.Contains("SLAB")) return (205, 205, 200);
        if (n.Contains("BARRIER") || n.Contains("CURB") || n.Contains("PARAPET")) return (225, 225, 220);
        if (n.Contains("SUB") || n.Contains("PIER") || n.Contains("FOOT") || n.Contains("PILE") || n.Contains("SEGMENT") || n.Contains("DIAPHRAGM") || n.Contains("BEAM")) return (185, 185, 180);
        return (150, 160, 175);
    }

    private static void WriteDae(StreamWriter w, string name, Dictionary<(byte, byte, byte), (List<Vec3> v, List<int> t)> groups, Func<Vec3, Vec3> toLocal)
    {
        w.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<COLLADA xmlns=\"http://www.collada.org/2005/11/COLLADASchema\" version=\"1.4.1\">\n");
        w.Write("<asset><contributor><authoring_tool>Steel Bridge Modeler</authoring_tool></contributor><unit name=\"meter\" meter=\"1\"/><up_axis>Z_UP</up_axis></asset>\n");
        var keys = groups.Keys.Where(k => groups[k].t.Count > 0).ToList();
        w.Write("<library_effects>\n");
        for (int i = 0; i < keys.Count; i++)
        {
            var (r, g, b) = keys[i];
            w.Write($"<effect id=\"fx{i}\"><profile_COMMON><technique sid=\"c\"><lambert><diffuse><color>{F(r / 255.0, 3)} {F(g / 255.0, 3)} {F(b / 255.0, 3)} 1</color></diffuse></lambert></technique><extra><technique profile=\"GOOGLEEARTH\"><double_sided>1</double_sided></technique></extra></profile_COMMON></effect>\n");
        }
        w.Write("</library_effects>\n<library_materials>\n");
        for (int i = 0; i < keys.Count; i++) w.Write($"<material id=\"m{i}\"><instance_effect url=\"#fx{i}\"/></material>\n");
        w.Write("</library_materials>\n<library_geometries>\n");
        for (int i = 0; i < keys.Count; i++)
        {
            var (v, t) = groups[keys[i]];
            w.Write($"<geometry id=\"g{i}\"><mesh><source id=\"g{i}p\"><float_array id=\"g{i}pa\" count=\"{v.Count * 3}\">");
            foreach (var p in v) { var q = toLocal(p); w.Write(F(q.X, 3)); w.Write(' '); w.Write(F(q.Y, 3)); w.Write(' '); w.Write(F(q.Z, 3)); w.Write(' '); }
            w.Write($"</float_array><technique_common><accessor source=\"#g{i}pa\" count=\"{v.Count}\" stride=\"3\"><param name=\"X\" type=\"float\"/><param name=\"Y\" type=\"float\"/><param name=\"Z\" type=\"float\"/></accessor></technique_common></source>");
            w.Write($"<vertices id=\"g{i}v\"><input semantic=\"POSITION\" source=\"#g{i}p\"/></vertices><triangles material=\"mat\" count=\"{t.Count / 3}\"><input semantic=\"VERTEX\" source=\"#g{i}v\" offset=\"0\"/><p>");
            var sb = new StringBuilder(t.Count * 7);
            foreach (int k in t) sb.Append(k.ToString(Inv)).Append(' ');
            w.Write(sb.ToString().TrimEnd());
            w.Write("</p></triangles></mesh></geometry>\n");
        }
        w.Write("</library_geometries>\n<library_visual_scenes><visual_scene id=\"s\">");
        w.Write($"<node id=\"n\" name=\"{X(name)}\">");
        for (int i = 0; i < keys.Count; i++)
            w.Write($"<instance_geometry url=\"#g{i}\"><bind_material><technique_common><instance_material symbol=\"mat\" target=\"#m{i}\"><bind_vertex_input semantic=\"UVSET0\" input_semantic=\"TEXCOORD\" input_set=\"0\"/></instance_material></technique_common></bind_material></instance_geometry>");
        w.Write("</node></visual_scene></library_visual_scenes>\n<scene><instance_visual_scene url=\"#s\"/></scene>\n</COLLADA>\n");
    }

    private static string F(double v, int dec) => v.ToString("F" + dec, Inv);
    private static string X(string s) => System.Security.SecurityElement.Escape(s) ?? "";
}
