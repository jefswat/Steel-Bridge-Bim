using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SBM.Api;
using SBM.Core.Drawing;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Cli;

/// <summary><c>sbm centerlines</c>: geometric centerline + section centroid line of every element (read-only, the model is never saved).</summary>
public static class CenterlinesCommand
{
    public const string Help = """
        sbm centerlines <model.sbm> [--bridge NAME] [--kind part,sub,deck,...] [--category Column,Cap,...] [--match REGEX] [--welds]
                        [--stations N] [--csv f.csv] [--json f.json] [--dxf f.dxf]
          Per element (steel parts, prestressed beams, deck, haunches, barriers, diaphragms, substructure solids, misc items) two 3D lines along its axis:
            GEOMETRIC = centre of the section's extents, CENTROID = area centroid of the section (holes subtracted).
          Straight extrusions are exact (recorded profile); other solids are cut at N stations (default 24) normal to the long principal axis
          (snapped plumb / level within 2 deg), then square to the path (curved members); partial end sections (skewed ends) are not used and the
          lines run straight to the element's extreme points. Kinds: part psbeam pad diaphragm deck haunch barrier caphaunch joint sub misc drain.
          Coordinates: world E, N, Z in project units (ft US / m SI). DXF: layers CL-GEOMETRIC / CL-CENTROID (3D polylines, project units).
          Without an output file: a count per category.
        """;

    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine(Help); return 0; }
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var doc = SbmDocument.Open(args[1]);
        var kinds = Opt("--kind")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cats = Opt("--category")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rx = Opt("--match") is { } m ? new Regex(m, RegexOptions.IgnoreCase) : null;
        var opts = new SBM.Core.Solids.CenterlineOptions { Stations = int.Parse(Opt("--stations") ?? "24", CultureInfo.InvariantCulture) };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        doc.MeasureModel();   // regenerates once (a second Regenerate call is not free)
        double regen = sw.Elapsed.TotalSeconds;
        var rows = doc.Centerlines(Opt("--bridge"), e =>
            (kinds == null || kinds.Contains(e.Ref.Kind)) && (cats == null || cats.Contains(e.Category))
            && (rx == null || rx.IsMatch(e.Ref.Mark) || rx.IsMatch(e.Description) || (e.Ref.Assembly != null && rx.IsMatch(e.Ref.Assembly))), opts, args.Contains("--welds"));
        var u = doc.Units;
        double L(double meters) => u.ToDisplay(meters, LengthKind.Long);
        string unit = u.IsUS ? "ft" : "m";
        string F(double v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
        string Pts(IEnumerable<Vec3> p) => string.Join(" ", p.Select(q => $"{F(L(q.X))},{F(L(q.Y))},{F(L(q.Z))}"));

        if (Opt("--csv") is { } csv)
        {
            using var w = new StreamWriter(csv);
            w.WriteLine($"bridge,kind,mark,assembly,unit,global_id,category,description,method,length_{unit},geometric_points_{unit},centroid_points_{unit},max_offset_{unit},note");
            foreach (var r in rows)
                w.WriteLine(string.Join(",", new[] { r.Ref.Bridge, r.Ref.Kind, r.Ref.Mark, r.Ref.Assembly ?? "", r.Ref.Unit ?? "", r.Ref.GlobalId ?? "", r.Category, r.Description,
                    r.Line.Method, F(L(r.Line.Length)), Pts(r.Line.Geometric), Pts(r.Line.Centroid), F(L(MaxOffset(r.Line))), r.Line.Note ?? "" }.Select(Csv)));
        }
        if (Opt("--json") is { } json)
        {
            var o = rows.Select(r => new
            {
                r.Ref.Bridge, r.Ref.Kind, r.Ref.Mark, r.Ref.Assembly, r.Ref.Unit, r.Ref.GlobalId, r.Category, r.Description, r.Line.Method, r.Line.Note,
                Length = L(r.Line.Length), LengthUnit = unit,
                Geometric = r.Line.Geometric.Select(q => new[] { L(q.X), L(q.Y), L(q.Z) }),
                Centroid = r.Line.Centroid.Select(q => new[] { L(q.X), L(q.Y), L(q.Z) }),
            });
            File.WriteAllText(json, JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (Opt("--dxf") is { } dxf)
        {
            var d = new PlanDrawing();
            var geo = d.Layer(new LayerStyle("CL-GEOMETRIC", 0, 200, 255, 4, LineStyle.Center));
            var cen = d.Layer(new LayerStyle("CL-CENTROID", 255, 120, 0, 30));
            foreach (var r in rows)
            {
                if (r.Line.Geometric.Count >= 2) geo.Polylines.Add(new Polyline3(r.Line.Geometric.ToArray(), false, r.Ref.Mark));
                if (r.Line.Centroid.Count >= 2) cen.Polylines.Add(new Polyline3(r.Line.Centroid.ToArray(), false, r.Ref.Mark));
            }
            SBM.IO.Dxf.DxfWriter.Write(dxf, d, u.LongUnit);
        }
        Console.WriteLine($"{rows.Count} elements, regeneration {regen:0.0} s, centerlines {sw.Elapsed.TotalSeconds - regen:0.0} s");
        foreach (var g in rows.GroupBy(r => (r.Ref.Kind, r.Category)).OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.Category))
            Console.WriteLine($"  {g.Key.Kind,-10} {g.Key.Category,-24} {g.Count(),7}  exact {g.Count(r => r.Line.Method == "extrusion"),7}  sections {g.Count(r => r.Line.Method == "sections" && r.Line.Note == null),7}"
                + (g.Count(r => r.Line.Note != null) is int bad && bad > 0 ? $"  no line {bad}" : ""));
        return 0;
    }

    /// <summary>Largest distance between the two lines (sampled at the centroid line's points).</summary>
    public static double MaxOffset(SBM.Core.Solids.ElementCenterline l)
    {
        if (l.Geometric.Count < 2 || l.Centroid.Count < 2) return 0;
        double best = 0;
        foreach (var p in l.Centroid)
        {
            double d = double.MaxValue;
            for (int i = 1; i < l.Geometric.Count; i++) d = Math.Min(d, SegDist(p, l.Geometric[i - 1], l.Geometric[i]));
            best = Math.Max(best, d);
        }
        return best;
    }

    private static double SegDist(Vec3 p, Vec3 a, Vec3 b)
    {
        var ab = b - a; double t = Math.Clamp(Vec3.Dot(p - a, ab) / Math.Max(1e-18, Vec3.Dot(ab, ab)), 0, 1);
        return (p - (a + ab * t)).Length;
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
