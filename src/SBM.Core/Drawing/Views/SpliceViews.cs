using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Field splice details drawn from the 3D splice assemblies the detailer generated: one elevation + plan per distinct
/// splice type, and a schedule of every splice. Plates are the silhouettes of the generated parts, bolts their positions.
/// </summary>
public static class SpliceViews
{
    public sealed record SpliceGroup(string Letter, string Signature, List<FieldSpliceResult> Splices)
    {
        public FieldSpliceResult First => Splices[0];
    }

    public static string MarkOf(FieldSpliceResult sp) => $"FS{(sp.Number > 0 ? sp.Number : sp.SpliceIndex + 1)}-G{sp.Girder + 1}";

    /// <summary>Distinct splice types (same plates and bolt count), lettered A, B, ...</summary>
    public static List<SpliceGroup> Groups(ViewContext ctx)
    {
        var res = new List<SpliceGroup>();
        if (ctx.Steel == null) return res;
        var bySig = new Dictionary<string, List<FieldSpliceResult>>();
        var order = new List<string>();
        foreach (var sp in ctx.Framing.Splices.OrderBy(s => s.Girder).ThenBy(s => s.GirderDistance))
        {
            string mark = MarkOf(sp);
            var parts = ctx.Steel.Parts.Where(p => p.Assembly == mark).Select(p => p.Description).OrderBy(x => x).ToList();
            if (parts.Count == 0) continue;
            int nb = ctx.Steel.Bolts.Count(b => b.Assembly == mark);
            string sig = string.Join("|", parts) + "#" + nb;
            if (!bySig.TryGetValue(sig, out var l)) { bySig[sig] = l = new(); order.Add(sig); }
            l.Add(sp);
        }
        for (int i = 0; i < order.Count; i++)
            res.Add(new SpliceGroup(((char)('A' + i % 26)).ToString(), order[i], bySig[order[i]]));
        return res;
    }

    private static List<double> Distinct(IEnumerable<double> v, double tol)
    {
        var l = new List<double>();
        foreach (double x in v.OrderBy(x => x)) if (l.Count == 0 || x - l[^1] > tol) l.Add(x);
        return l;
    }

    /// <summary>A chain of dimensions along a line; uniform spacings collapse to one "n SPA. @ pitch = total" dimension.</summary>
    private static void Chain(Drafter d, IReadOnlyList<double> v, Func<double, Vec2> pt, double offset)
    {
        if (v.Count < 2) return;
        double pitch = v[1] - v[0];
        bool uniform = v.Count > 3 && Enumerable.Range(1, v.Count - 1).All(i => Math.Abs(v[i] - v[i - 1] - pitch) < 0.0015);
        if (uniform) d.Dim(pt(v[0]), pt(v[^1]), offset, $"{v.Count - 1} SPA. @ {d.Short(pitch)} = {d.Short(v[^1] - v[0])}");
        else for (int i = 1; i < v.Count; i++) d.Dim(pt(v[i - 1]), pt(v[i]), offset, d.Short(v[i] - v[i - 1]));
    }

    private static string Uc(string s) => s.ToUpperInvariant();

    public static SheetView Detail(ViewContext ctx, SpliceGroup grp)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var steel = ctx.Steel!;
        var sp = grp.First;
        string mark = MarkOf(sp);
        var parts = steel.Parts.Where(p => p.Assembly == mark && p.Category != PartCategory.Weld).ToList();
        var bolts = steel.Bolts.Where(b => b.Assembly == mark).ToList();
        var o = sp.Location;
        var t = new Vec3(sp.GirderTangent.X, sp.GirderTangent.Y, 0).Normalized();
        var lat = new Vec3(sp.GirderTangent.Y, -sp.GirderTangent.X, 0);   // to the right of the girder direction
        double X(Vec3 p) => Vec3.Dot(p - o, t);
        double Lat(Vec3 p) => Vec3.Dot(p - o, lat);
        double webD = ctx.Framing.Definition.Girders.Section.WebDepth;
        double grade = ctx.Framing.Girders[sp.Girder].Path.GradeAt(sp.GirderDistance);
        // Elevation z is measured from the sloping girder line (so rows of bolts and plate edges are level in the drawing).
        double Zl(Vec3 q) => q.Z - o.Z - grade * X(q);

        // Elevation (looking at the web from the right) sits above the plan; plan Y = -lateral.
        var pe = new Dictionary<SteelPart, List<Vec2>>();
        var pp = new Dictionary<SteelPart, List<Vec2>>();
        foreach (var p in parts)
        {
            pe[p] = MeshBuilder.ConvexHull(p.Mesh.Positions.Select(q => new Vec2(X(q), Zl(q))));
            pp[p] = MeshBuilder.ConvexHull(p.Mesh.Positions.Select(q => new Vec2(X(q), -Lat(q))));
        }
        double xMin = parts.Min(p => pe[p].Min(q => q.X)), xMax = parts.Max(p => pe[p].Max(q => q.X));
        double latMin = parts.Min(p => pp[p].Min(q => q.Y)), latMax = parts.Max(p => pp[p].Max(q => q.Y));
        double planShift = -(webD + d.Paper(2.4)) - latMax;     // plan placed under the elevation
        Vec2 P2(Vec2 q) => new(q.X, q.Y + planShift);

        // Girder web lines and flanges behind the elevation.
        double ext = (xMax - xMin) * 0.12;
        d.Line(SheetLayers.Steel, new Vec2(xMin - ext, 0), new Vec2(xMax + ext, 0));
        d.Line(SheetLayers.Steel, new Vec2(xMin - ext, -webD), new Vec2(xMax + ext, -webD));
        double gap = ctx.Framing.Definition.FieldSpliceGap;
        d.Line(SheetLayers.Center, new Vec2(0, d.Paper(0.3)), new Vec2(0, -webD - d.Paper(0.3)));
        d.Line(SheetLayers.Center, new Vec2(0, latMax + planShift + d.Paper(0.3)), new Vec2(0, latMin + planShift - d.Paper(0.3)));
        d.Text("CL SPLICE", new Vec2(0, d.Paper(0.45)), d.SmallTextH, 0, TextAlign.Center);

        // Plates.
        bool IsWeb(SteelPart p) => p.Description.StartsWith("Web", StringComparison.OrdinalIgnoreCase);
        foreach (var p in parts)
        {
            bool web = IsWeb(p);
            if (web) d.Poly(SheetLayers.Steel, pe[p], true);
            else
            {
                d.Poly(SheetLayers.Steel, pp[p].Select(P2), true);
                // The flange plates also show in the elevation as thin strips at the flange level.
                d.Poly(SheetLayers.SteelHidden, pe[p], true);
            }
        }
        // Bolts: holes where the axis faces the viewer, otherwise the bolt as a line.
        double bd = bolts.Count > 0 && bolts[0].Diameter > 0 ? bolts[0].Diameter : steel.Bolt.Diameter;
        foreach (var b in bolts)
        {
            var axis = b.Frame.Z; var pt = b.Frame.Origin;
            double along = Math.Abs(Vec3.Dot(axis, lat));
            if (along > 0.7) d.Circle(SheetLayers.Steel, new Vec2(X(pt), Zl(pt)), (b.Diameter > 0 ? b.Diameter : bd) / 2);
            else
            {
                var p2 = pt + axis * b.Length;
                d.Circle(SheetLayers.Steel, P2(new Vec2(X(pt), -Lat(pt))), (b.Diameter > 0 ? b.Diameter : bd) / 2);
                _ = p2;
            }
        }

        // Dimensions: plate lengths, bolt rows (pitch), web plate height, flange gages.
        var webs = parts.Where(IsWeb).ToList();
        if (webs.Count > 0)
        {
            var w = webs[0];
            double wx0 = pe[w].Min(q => q.X), wx1 = pe[w].Max(q => q.X), wz0 = pe[w].Min(q => q.Y), wz1 = pe[w].Max(q => q.Y);
            d.Dim(new Vec2(wx0, wz0), new Vec2(wx1, wz0), -0.35, d.Short(wx1 - wx0));
            d.Dim(new Vec2(wx1, wz0), new Vec2(wx1, wz1), -0.4, d.Short(wz1 - wz0));
        }
        var webBolts = bolts.Where(b => Math.Abs(Vec3.Dot(b.Frame.Z, lat)) > 0.7).ToList();
        if (webBolts.Count > 0)
        {
            // Pitch along the lowest row and the gage up the first column (one chain each).
            double zRow = webBolts.Min(b => Zl(b.Frame.Origin));
            var xs = Distinct(webBolts.Where(b => Math.Abs(Zl(b.Frame.Origin) - zRow) < 0.002).Select(b => X(b.Frame.Origin)), 0.002);
            Chain(d, xs, x => new Vec2(x, zRow), -0.2);
            double xCol = webBolts.Max(b => X(b.Frame.Origin));
            var zs = Distinct(webBolts.Where(b => Math.Abs(X(b.Frame.Origin) - xCol) < 0.002).Select(b => Zl(b.Frame.Origin)), 0.002);
            Chain(d, zs, z => new Vec2(xCol, z), -0.2);
        }
        var flBolts = bolts.Where(b => Math.Abs(b.Frame.Z.Z) > 0.7 && Zl(b.Frame.Origin) > -webD / 2).ToList();
        var fxs = new List<double>();
        if (flBolts.Count > 0)
        {
            double latRow = flBolts.Min(b => -Lat(b.Frame.Origin));
            fxs = Distinct(flBolts.Where(b => Math.Abs(-Lat(b.Frame.Origin) - latRow) < 0.002).Select(b => X(b.Frame.Origin)), 0.002);
            Chain(d, fxs, x => new Vec2(x, latRow + planShift), -0.3);
            double xRow = flBolts.Max(b => X(b.Frame.Origin));
            var fls = Distinct(flBolts.Where(b => Math.Abs(X(b.Frame.Origin) - xRow) < 0.002).Select(b => -Lat(b.Frame.Origin)), 0.002);
            Chain(d, fls, y => new Vec2(xRow, y + planShift), -0.3);
        }
        if (fxs.Count > 0) d.Dim(new Vec2(-gap / 2, latMax + planShift + d.Paper(0.0)), new Vec2(gap / 2, latMax + planShift), 0.55, d.Short(gap));

        // Labels: identical descriptions are grouped with a count.
        int k = 0;
        foreach (var gp in parts.GroupBy(p => p.Description).OrderByDescending(g => IsWeb(g.First()) ? 1e6 + pe[g.First()].Average(q => q.Y) : pp[g.First()].Average(q => q.Y)))
        {
            var p0 = gp.First();
            bool web = IsWeb(p0);
            var hull = web ? pe[p0] : pp[p0].Select(P2).ToList();
            var c = new Vec2(hull.Average(q => q.X), hull.Average(q => q.Y));
            var target = new Vec2(hull.Max(q => q.X) - (hull.Max(q => q.X) - hull.Min(q => q.X)) * 0.15, c.Y);
            var elbow = new Vec2(xMax + d.Paper(0.8), (web ? c.Y : planShift + latMax) - (web ? 0 : d.Paper(0.18 * k++)));
            d.Leader(target, elbow, $"{(gp.Count() > 1 ? gp.Count() + " - " : "")}{Uc(p0.Description)}");
        }
        if (bolts.Count > 0)
            d.Text($"{bolts.Count} - {d.Short(bd)} DIA {steel.BoltGrade} BOLTS ({bolts.Count(b => Math.Abs(Vec3.Dot(b.Frame.Z, lat)) > 0.7)} IN THE WEB PLATES)",
                new Vec2((xMin + xMax) / 2, planShift + latMin - d.Paper(1.2)), d.TextH, 0, TextAlign.Center);
        var box = dwg.Extents();
        string others = string.Join(", ", grp.Splices.Skip(1).Take(10).Select(MarkOf)) + (grp.Splices.Count > 11 ? ", ..." : "");
        d.Title($"FIELD SPLICE DETAIL {grp.Letter} — {mark}", others.Length > 0 ? $"Also: {others}" : "Elevation (web side) and plan of the flange splice", new Vec2((xMin + xMax) / 2, box.MinY - d.Paper(0.4)));
        return new SheetView($"Field splice detail {grp.Letter}", $"Splice {grp.Letter}", dwg, d.Scale, $"Field splice type {grp.Letter}: plates and bolts as generated.");
    }

    private static string Short(string desc)
    {
        string s = desc;
        foreach (var (pre, rep) in new[] { ("Outside top flange splice ", "OUT "), ("Outside bottom flange splice ", "OUT "), ("Inside top flange splice ", "IN "), ("Inside bottom flange splice ", "IN "), ("Web splice ", "WEB ") })
            if (s.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return rep + s[pre.Length..];
        if (s.StartsWith("Filler ", StringComparison.OrdinalIgnoreCase)) { int i = s.IndexOf(" ("); return "FILL " + (i > 0 ? s[7..i] : s[7..]); }
        return s;
    }

    /// <summary>Schedule of every field splice: station, type letter, plates and bolt count.</summary>
    public static List<(string title, SheetView view)> Schedule(ViewContext ctx, double areaW, double areaH)
    {
        var f = ctx.Framing; var u = ctx.Units; var steel = ctx.Steel;
        var groups = Groups(ctx);
        var pages = new List<(string, SheetView)>();
        if (steel == null || groups.Count == 0) return pages;
        var rows = new List<string[]>();
        foreach (var gr in groups)
            foreach (var sp in gr.Splices)
            {
                string mark = MarkOf(sp);
                string Plates(string key) => string.Join("; ", steel.Parts.Where(p => p.Assembly == mark && p.Description.Contains(key, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(p => Short(p.Description)).Select(g => (g.Count() > 1 ? g.Count() + " " : "") + g.Key));
                var so = f.Alignment.Inverse(sp.Location.XY);
                rows.Add(new[] { $"FS{(sp.Number > 0 ? sp.Number : sp.SpliceIndex + 1)}", $"G{sp.Girder + 1}", u.FormatStation(so.Station), gr.Letter,
                    Plates("top flange"), Plates("bottom flange"), Plates("Web"), steel.Bolts.Count(b => b.Assembly == mark).ToString() });
            }
        var cols = new[] { ("SPLICE", 0.8), ("GIRDER", 0.7), ("STATION", 1.3), ("TYPE", 0.6), ("TOP FLANGE PLATES", 4.2), ("BOTTOM FLANGE PLATES", 4.2), ("WEB PLATES", 2.3), ("BOLTS", 0.7) };
        var dwgPages = Paged2(ctx, "FIELD SPLICE SCHEDULE", cols, rows, new[] { "OUT = OUTSIDE SPLICE PLATE, IN = INSIDE SPLICE PLATE, FILL = FILLER; PLATE = WIDTH x THICKNESS x LENGTH. TYPE LETTERS ARE THE SPLICE DETAILS." }, areaW, areaH);
        return dwgPages;
    }

    private static List<(string, SheetView)> Paged2(ViewContext ctx, string title, (string, double)[] cols, List<string[]> rows, string[] notes, double areaW, double areaH)
        => TableViews.PagedPublic(ctx, title, cols, rows, notes, areaW, areaH, "Splice schedule");
}
