using System.Diagnostics;
using SBM.Api;
using SBM.Core.Checks.Measure;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Cli;

/// <summary>
/// <c>sbm measure</c> and <c>sbm section</c>: read-only QC measurements on a regenerated model (QC-PLAN section 3). The model is opened and
/// regenerated in memory, never saved.
/// </summary>
public static class MeasureCommand
{
    public const string Help = """
        sbm measure <model.sbm> <query> [--bridge NAME] [--json] [--out result.json] [--svg drawing.svg] [--tol 1/8"]
          queries (several may be given, or --batch file with one query per line; one regeneration for all):
            --part MARK [--ref longest-edge|horizontal|holes|girder-tangent|alignment|x,y,z] [--leg N]
                      flatten a plate into its own plane: thickness, outline (edges, angles, clips, copes), holes, bolt pattern
                      (rows / columns, spacing chains, edge distances perpendicular to the edges)
            --assembly MARK [--ref ...] [--no-neighbours]
                      plates + bolts of an assembly collapsed into their common planes: per plane outlines, stack (fillers, gaps),
                      bolts (per side of the joint), joint gaps; SVG = one panel per plane
            --member MARK --at <distance|station> [--width all|LEN] [--dxf f] [--csv f]
                      true section normal to a member / girder (tangent incl. grade) at a distance from its start or a station
            --distance MARK_A MARK_B           minimum distance between two solids (negative = overlap depth)
            --point x,y[,z] | --station STA [--offset O]
                      elements on the vertical line there, deck top, top of web, seats (x,y,z in project units, E,N)
            --bar MARK[#n] [--unit UNIT]       bar centerline, bends (radius, pin), hooks, developed vs scheduled length
            --cover MARK [--unit UNIT]         clear cover per bar to each host face type (top / bottom / side)
            --spacing MARK [--along girder:G|alignment|axis|x,y,z]   sorted spacing chain, "n spa @ s"
            --laps MARK [--fc KSI]             lap lengths, clear distance, stagger of lapped bars
        sbm section <model.sbm> (--plane x,y,z,nx,ny,nz | --station STA [--offset O]) [--normal-to alignment|girder:G|member:MARK|plumb]
                    [--skew DEG] [--width LEN] [--height LEN] [--bridge NAME] [--bolts] [--no-bars] [--only text,text] [--dims]
                    [--svg f.svg] [--dxf f.dxf] [--csv f.csv] [--json] [--out f.json]
                      true plane cut through every solid in the plane's own frame (u right looking ahead, v up in the plane),
                      closed loops hatched, bars crossing as dots (mark, size, cover), bars in the plane as polylines
        Coordinates in JSON: plate / section 2D in inches (US) or mm (SI); world points in feet (US) or meters (SI); every length also as
        {in, text (ft-in to 1/16"), mm}.
        """;

    public static int Measure(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Usage:\n" + Help);
        var sw = Stopwatch.StartNew();
        var doc = SbmDocument.Open(args[1]);
        var queries = new List<string[]>();
        var global = Global(args);
        string? batch = Opt(args, "--batch");
        if (batch != null) foreach (var line in File.ReadAllLines(batch)) { var t = Tokens(line); if (t.Length > 0 && !t[0].StartsWith('#')) queries.Add(t); }
        else queries.AddRange(SplitQueries(args.Skip(2).ToArray()));
        if (queries.Count == 0) throw new ArgumentException("No query. " + Help);
        var r = doc.Regenerate();
        var mm = doc.MeasureModel();
        double regen = sw.Elapsed.TotalSeconds;
        var results = new List<object>();
        bool json = args.Contains("--json");
        int qi = 0;
        foreach (var q0 in queries)
        {
            var q = q0.Concat(global).ToArray();
            var qs = Stopwatch.StartNew();
            object res = Run(doc, mm, q, ++qi, queries.Count);
            results.Add(res);
            if (!json) Console.Write(Text(res, doc.Units));
            if (!json) Console.WriteLine($"  ({qs.Elapsed.TotalSeconds:0.00} s)");
        }
        if (json) Console.WriteLine(Fmt.ToJson(results.Count == 1 ? results[0] : results));
        if (Opt(args, "--out") is { } outp) { File.WriteAllText(outp, Fmt.ToJson(results.Count == 1 ? results[0] : results)); if (!json) Console.WriteLine($"  -> {outp}"); }
        if (!json) Console.WriteLine($"regeneration {regen:0.0} s, total {sw.Elapsed.TotalSeconds:0.0} s (model not saved)");
        return 0;
    }

    public static int Section(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Usage:\n" + Help);
        var sw = Stopwatch.StartNew();
        var doc = SbmDocument.Open(args[1]);
        doc.Regenerate();
        var mm = doc.MeasureModel();
        var res = RunSection(doc, mm, args.Skip(2).ToArray(), args.Contains("--json"));
        if (args.Contains("--json")) Console.WriteLine(Fmt.ToJson(res));
        else { Console.Write(ModelSection.ToText(res)); Console.WriteLine($"total {sw.Elapsed.TotalSeconds:0.0} s (model not saved)"); }
        if (Opt(args, "--out") is { } outp) File.WriteAllText(outp, Fmt.ToJson(res));
        return 0;
    }

    // ------------------------------------------------------------------ plumbing

    private static readonly HashSet<string> QueryStarts = new() { "--part", "--assembly", "--member", "--distance", "--point", "--station", "--bar", "--cover", "--spacing", "--laps" };
    private static readonly HashSet<string> GlobalOpts = new() { "--bridge", "--tol", "--unit" };

    /// <summary>Options that apply to every query (bridge, tolerance).</summary>
    private static string[] Global(string[] args)
    {
        var g = new List<string>();
        for (int i = 2; i < args.Length; i++)
            if (GlobalOpts.Contains(args[i]) && i + 1 < args.Length) { g.Add(args[i]); g.Add(args[i + 1]); i++; }
        return g.ToArray();
    }

    /// <summary>Splits "--part A --ref x --part B" into one option list per query.</summary>
    private static List<string[]> SplitQueries(string[] a)
    {
        var res = new List<List<string>>();
        for (int i = 0; i < a.Length; i++)
        {
            if (QueryStarts.Contains(a[i]) && !(a[i] == "--station" && res.Count > 0 && res[^1].Contains("--member"))) res.Add(new List<string>());
            if (res.Count == 0) continue;
            res[^1].Add(a[i]);
        }
        return res.Select(l => l.ToArray()).ToList();
    }

    private static string[] Tokens(string line)
    {
        var res = new List<string>(); var cur = new System.Text.StringBuilder(); bool quote = false;
        foreach (char c in line.Trim())
        {
            if (c == '"' && (cur.Length == 0 || quote)) { quote = !quote; continue; }
            if (char.IsWhiteSpace(c) && !quote) { if (cur.Length > 0) { res.Add(cur.ToString()); cur.Clear(); } continue; }
            cur.Append(c);
        }
        if (cur.Length > 0) res.Add(cur.ToString());
        return res.ToArray();
    }

    private static string? Opt(string[] a, string name) { int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }

    private static double ParseLong(SbmDocument doc, string text) => doc.Units.TryParseLength(text, LengthKind.Long, out double v) ? v : throw new ArgumentException($"Can't read length '{text}'.");
    private static double ParseShort(SbmDocument doc, string text) => doc.Units.TryParseLength(text, LengthKind.Short, out double v) ? v : throw new ArgumentException($"Can't read length '{text}'.");

    private static double[] Numbers(string text) => text.Split(',', StringSplitOptions.TrimEntries).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static PlateFrameOptions FrameOptions(SbmDocument doc, MeasureModel mm, string[] q, Func<Vec3?> centroid, string? bridge, string defaultRef = "longest-edge")
    {
        var o = new PlateFrameOptions();
        if (Opt(q, "--tol") is { } t) o.Tolerance = ParseShort(doc, t);
        if (Opt(q, "--leg") is { } leg) o.Component = int.Parse(leg) - 1;
        string rf = Opt(q, "--ref") ?? defaultRef;
        var c = centroid();
        // ahead = the nearest girder's tangent (u points up-station when the reference gives only a line)
        if (c is { } cc)
            foreach (var m in mm.Filter(bridge))
            {
                var (g, s, d) = MeasureModel.NearestGirder(m, cc);
                if (g == null) continue;
                o.Ahead = MeasureModel.GirderTangent(g, s);
                if (rf is "girder-tangent" or "girder")
                {
                    o.Ref = "vector"; o.Vector = o.Ahead;
                    o.RefName = $"tangent of girder {MeasureModel.GirderName(m, g)} incl. grade {g.Path.GradeAt(s) * 100:0.###}% (projected into the plane)";
                }
                break;
            }
        if (rf == "alignment" && c is { } c2)
        {
            var m = mm.Filter(bridge).First();
            var a = m.Framing.Alignment;
            double sta = a.Inverse(c2.XY).Station;
            o.Ref = "vector"; o.Vector = MeasureModel.AlignmentTangent(a, sta); o.RefName = $"alignment {a.Name} tangent incl. grade at Sta {doc.Units.FormatStation(sta)}";
        }
        else if (rf.Contains(',')) { o.Ref = "vector"; o.Vector = Geo3.Parse(rf); o.RefName = $"vector ({rf})"; }
        else if (rf is "longest-edge" or "horizontal" or "holes") o.Ref = rf;
        else if (rf is not ("girder-tangent" or "girder" or "alignment")) throw new ArgumentException($"--ref {rf}: use longest-edge, horizontal, holes, girder-tangent, alignment or x,y,z");
        if (rf is "girder-tangent" or "girder" && o.Vector == null) throw new ArgumentException("--ref girder-tangent: no girder near the part.");
        return o;
    }

    private static object Run(SbmDocument doc, MeasureModel mm, string[] q, int no, int count)
    {
        string? bridge = Opt(q, "--bridge");
        string? svg = Opt(q, "--svg");
        if (svg != null && count > 1) svg = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(svg))!, Path.GetFileNameWithoutExtension(svg) + $"_{no}" + Path.GetExtension(svg));
        var notes = new List<string>();
        string? unit = Opt(q, "--unit");
        double tol = Opt(q, "--tol") is { } t ? ParseShort(doc, t) : 0.0254 / 8;
        switch (q[0])
        {
            case "--part":
            {
                var (m, p) = mm.Part(Need(q, "--part"), bridge, notes);
                var fo = FrameOptions(doc, mm, q, () => { var b = p.Mesh.Bounds(); return (b.Min + b.Max) * 0.5; }, m.Definition.Name);
                var id = mm.Ref(m.Definition.Name, "part", p.Mark, p.Assembly);
                var r = PlateMeasure.Measure(p.Mesh, id, p.Category.ToString(), p.Description, fo, doc.Units) ?? throw new ArgumentException($"{p.Mark} has no plate-like faces (two parallel opposite faces).");
                r.Notes.InsertRange(0, notes);
                if (svg != null) { File.WriteAllText(svg, MeasureSvg.Plate(r)); r.Notes.Add($"svg -> {svg}"); }
                return r;
            }
            case "--assembly":
            {
                var (m, a) = mm.Assembly(Need(q, "--assembly"), bridge, notes);
                var fo = FrameOptions(doc, mm, q, () =>
                {
                    var b = SBM.Core.Solids.Box3.Empty;
                    foreach (var p in m.Steel!.Parts.Where(p => p.Assembly == a.Mark)) { var bb = p.Mesh.Bounds(); b.Add(bb.Min); b.Add(bb.Max); }
                    return b.IsEmpty ? null : (b.Min + b.Max) * 0.5;
                }, m.Definition.Name, a.Kind is SBM.Core.Steel.AssemblyKind.FieldSplice or SBM.Core.Steel.AssemblyKind.GirderFieldSection ? "girder-tangent" : "longest-edge");
                var r = AssemblyMeasure.Measure(mm, m, a, new AssemblyOptions { Frame = fo, Neighbours = !q.Contains("--no-neighbours") });
                r.Notes.InsertRange(0, notes);
                if (svg != null) { File.WriteAllText(svg, MeasureSvg.Assembly(r)); r.Notes.Add($"svg -> {svg}"); }
                return r;
            }
            case "--member":
            {
                string mark = Need(q, "--member");
                string at = Opt(q, "--at") ?? throw new ArgumentException("--member needs --at <distance|station>.");
                double? dist = null, sta = null;
                if (at.Contains('+')) sta = doc.Station(at); else dist = ParseLong(doc, at);
                var (pl, _) = SectionPlanes.ForMember(mm, mark, bridge, dist, sta, notes);
                var so = SectionOpts(doc, q, bridge);
                if (Opt(q, "--width") is not "all")
                {
                    // window: the member's own cut inflated by the given width (default 1 ft) on every side
                    var own = ModelSection.Cut(mm, pl, new SectionOptions { Bridge = bridge, Bars = false, Only = { mark } });
                    var segs = own.Elements.SelectMany(e => e.Segs).ToList();
                    if (segs.Count > 0)
                    {
                        double grow = Opt(q, "--width") is { } w ? ParseShort(doc, w) : 0.3048;
                        so.U0 = segs.Min(s => Math.Min(s.A.X, s.B.X)) - grow; so.U1 = segs.Max(s => Math.Max(s.A.X, s.B.X)) + grow;
                        so.V0 = segs.Min(s => Math.Min(s.A.Y, s.B.Y)) - grow; so.V1 = segs.Max(s => Math.Max(s.A.Y, s.B.Y)) + grow;
                    }
                }
                var r = ModelSection.Cut(mm, pl, so);
                r.Notes.InsertRange(0, notes);
                WriteSectionFiles(doc, r, q, svg);
                return r;
            }
            case "--distance":
            {
                int i = Array.IndexOf(q, "--distance");
                if (i + 2 >= q.Length) throw new ArgumentException("--distance needs two marks.");
                return MeasureQueries.Distance(mm, q[i + 1], q[i + 2], bridge);
            }
            case "--point":
            case "--station":
            {
                Vec2 xy; double? z = null;
                if (q[0] == "--point")
                {
                    var n = Numbers(Need(q, "--point"));
                    xy = new Vec2(doc.Units.FromDisplay(n[0], LengthKind.Long), doc.Units.FromDisplay(n[1], LengthKind.Long));
                    if (n.Length > 2) z = doc.Units.FromDisplay(n[2], LengthKind.Long);
                }
                else
                {
                    var m = mm.Filter(bridge).First();
                    double sta = doc.Station(Need(q, "--station"));
                    double off = Opt(q, "--offset") is { } o ? ParseLong(doc, o) : 0;
                    xy = m.Framing.Alignment.PointAt(sta, off);
                }
                return MeasureQueries.Point(mm, xy, z, bridge);
            }
            case "--bar": return MeasureQueries.Bar(mm, Need(q, "--bar"), bridge, unit);
            case "--cover": return MeasureQueries.Cover(mm, Need(q, "--cover"), bridge, unit);
            case "--spacing": return MeasureQueries.Spacing(mm, Need(q, "--spacing"), bridge, unit, Opt(q, "--along") ?? "axis", tol);
            case "--laps": return MeasureQueries.Laps(mm, Need(q, "--laps"), bridge, unit, Opt(q, "--fc") is { } fc ? double.Parse(fc, System.Globalization.CultureInfo.InvariantCulture) : null);
        }
        throw new ArgumentException($"Unknown query {q[0]}.");
    }

    private static string Need(string[] q, string name) => Opt(q, name) ?? throw new ArgumentException($"{name} needs a value.");

    private static SectionOptions SectionOpts(SbmDocument doc, string[] q, string? bridge)
    {
        var so = new SectionOptions { Bridge = bridge, Bolts = q.Contains("--bolts"), Bars = !q.Contains("--no-bars"), Dims = q.Contains("--dims") };
        if (Opt(q, "--only") is { } only) so.Only.AddRange(only.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return so;
    }

    private static void WriteSectionFiles(SbmDocument doc, SectionResult r, string[] q, string? svg)
    {
        if (svg != null) { File.WriteAllText(svg, ModelSection.Svg(r)); r.Notes.Add($"svg -> {svg}"); }
        if (Opt(q, "--dxf") is { } dxf) { SBM.IO.Dxf.DxfWriter.Write(dxf, ModelSection.Drawing(r), doc.Units.IsUS ? 0.0254 : 0.001); r.Notes.Add($"dxf -> {dxf} (u, v in {doc.Units.ShortUnitName})"); }
        if (Opt(q, "--csv") is { } csv) { ModelSection.WriteCsv(csv, r); r.Notes.Add($"csv -> {csv}"); }
    }

    private static SectionResult RunSection(SbmDocument doc, MeasureModel mm, string[] q, bool json)
    {
        string? bridge = Opt(q, "--bridge");
        var notes = new List<string>();
        var so = SectionOpts(doc, q, bridge);
        SectionPlane pl;
        double? sta = null, off = null;
        double skew = Opt(q, "--skew") is { } sk ? double.Parse(sk, System.Globalization.CultureInfo.InvariantCulture) : 0;
        if (Opt(q, "--plane") is { } plane)
        {
            var n = Numbers(plane);
            if (n.Length != 6) throw new ArgumentException("--plane x,y,z,nx,ny,nz (point in project units, normal = viewing direction).");
            var p = new Vec3(doc.Units.FromDisplay(n[0], LengthKind.Long), doc.Units.FromDisplay(n[1], LengthKind.Long), doc.Units.FromDisplay(n[2], LengthKind.Long));
            pl = new SectionPlane(p, new Vec3(n[3], n[4], n[5])) { Description = $"plane through ({plane})" };
            if (Math.Abs(skew) > 1e-12) pl = pl.Skewed(skew);
        }
        else if (Opt(q, "--station") is { } st)
        {
            var m = mm.Filter(bridge).FirstOrDefault() ?? throw new ArgumentException($"No bridge '{bridge}'.");
            sta = doc.Station(st);
            off = Opt(q, "--offset") is { } o ? ParseLong(doc, o) : null;
            pl = SectionPlanes.AtStation(mm, m, sta.Value, off, Opt(q, "--normal-to"), skew, notes);
        }
        else throw new ArgumentException("sbm section needs --plane or --station.");
        if (Opt(q, "--width") is { } w && w != "all") { double hw = ParseLong(doc, w) / 2; so.U0 = -hw; so.U1 = hw; }
        if (Opt(q, "--height") is { } h) { double hh = ParseLong(doc, h) / 2; so.V0 = -hh; so.V1 = hh; }
        var r = ModelSection.Cut(mm, pl, so, sta, off);
        r.Notes.InsertRange(0, notes);
        WriteSectionFiles(doc, r, q, Opt(q, "--svg"));
        return r;
    }

    private static string Text(object r, UnitSystem u) => r switch
    {
        PlateResult p => PlateMeasure.ToText(p, u),
        AssemblyResult a => AssemblyMeasure.ToText(a),
        SectionResult s => ModelSection.ToText(s),
        _ => MeasureText.ToText(r),
    };
}
