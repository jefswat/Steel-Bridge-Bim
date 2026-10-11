using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Data tables for the plan set, paper units (inches x 0.0254): bearing data and elevation tables. Every value comes from the generated
/// framing / steel model; where the model has nothing the cell shows a dash.
/// </summary>
public static class TableViews
{
    private sealed record Col(string Header, double Width);

    private static List<(string title, SheetView view)> Paged(ViewContext ctx, string title, Col[] cols, List<string[]> rows, IList<string> notes, double areaW, double areaH, string stem)
    {
        var pages = new List<(string, SheetView)>();
        double rowH = 0.19;
        int perPage = Math.Max(5, (int)Math.Floor((areaH - 1.1 - 0.2 * notes.Count) / rowH) - 1);
        int nPages = Math.Max(1, (rows.Count + perPage - 1) / perPage);
        for (int p = 0; p < nPages; p++)
        {
            var dwg = new PlanDrawing();
            var d = new Drafter(dwg, ctx.Units, 1);
            string t = title + (nPages > 1 ? $" ({p + 1} OF {nPages})" : "");
            double tw = d.Paper(cols.Sum(c => c.Width)), rh = d.Paper(rowH);
            var page = rows.Skip(p * perPage).Take(perPage).ToList();
            d.Text(t, new Vec2(0, d.Paper(0.3)), d.Paper(0.14), 0, TextAlign.Left, SheetLayers.Title);
            d.Rect(SheetLayers.Text, 0, -rh * (page.Count + 1), tw, 0);
            double x = 0;
            foreach (var c in cols)
            {
                d.Text(c.Header, new Vec2(x + d.Paper(0.05), -rh / 2), d.Paper(0.075), 0, TextAlign.Left);
                x += d.Paper(c.Width);
                d.Line(SheetLayers.Text, new Vec2(x, 0), new Vec2(x, -rh * (page.Count + 1)));
            }
            d.Line(SheetLayers.Text, new Vec2(0, -rh), new Vec2(tw, -rh));
            for (int i = 0; i < page.Count; i++)
            {
                double ry = -rh * (i + 1);
                d.Line(SheetLayers.Text, new Vec2(0, ry - rh), new Vec2(tw, ry - rh));
                x = 0;
                for (int k = 0; k < cols.Length; k++)
                {
                    string v = k < page[i].Length ? page[i][k] : "";
                    d.Text(v, new Vec2(x + d.Paper(0.05), ry - rh / 2), d.Paper(0.075), 0, TextAlign.Left);
                    x += d.Paper(cols[k].Width);
                }
            }
            if (p == nPages - 1)
                for (int i = 0; i < notes.Count; i++)
                    d.Text(notes[i], new Vec2(0, -rh * (page.Count + 1) - d.Paper(0.25 + 0.17 * i)), d.Paper(0.075), 0, TextAlign.Left);
            dwg.Layer(SheetLayers.Grid).Circles.Add(new CircleItem(new Vec3(0, 0.0254 * 0.3, 0), 0));
            dwg.Layer(SheetLayers.Grid).Circles.Add(new CircleItem(new Vec3(0.0254 * (areaW - 0.1), -0.0254 * (areaH - 0.4), 0), 0));
            pages.Add((t, new SheetView(stem + (nPages > 1 ? $" {p + 1}" : ""), "Table", dwg, 1, "", PaperSpace: true)));
        }
        return pages;
    }

    public static List<(string title, SheetView view)> PagedPublic(ViewContext ctx, string title, (string, double)[] cols, List<string[]> rows, IList<string> notes, double areaW, double areaH, string stem)
        => Paged(ctx, title, cols.Select(c => new Col(c.Item1, c.Item2)).ToArray(), rows, notes, areaW, areaH, stem);

    /// <summary>Girder spacing along every support line (CL bearing to CL bearing, in the plane of the support line), station and skew.</summary>
    public static List<(string title, SheetView view)> GirderSpacing(ViewContext ctx, double areaW, double areaH)
    {
        var f = ctx.Framing;
        var u = ctx.Units;
        var a = f.Alignment;
        int ng = f.Girders.Count;
        var rows = new List<string[]>();
        foreach (var sl in f.Supports.Concat(f.AuxSupports))
        {
            var r = new List<string> { sl.Name.ToUpperInvariant(), u.FormatStation(sl.Station), sl.SkewDegrees == 0 ? "0" : $"{UnitSystem.FormatAngleDms(Math.Abs(sl.SkewDegrees) * Math.PI / 180, false)} {(sl.SkewDegrees > 0 ? "RT" : "LT")}" };
            for (int i = 0; i + 1 < ng; i++)
            {
                var b0 = i < sl.Bearings.Length ? sl.Bearings[i] : null; var b1 = sl.Bearings[i + 1];
                r.Add(b0 != null && b1 != null ? u.FormatLength(Vec2.Distance(a.PointAt(b0.Station, b0.Offset), a.PointAt(b1.Station, b1.Offset))) : "-");
            }
            rows.Add(r.ToArray());
        }
        var cols = new List<Col> { new("SUPPORT", 1.5), new("STATION (CL)", 1.4), new("SKEW", 1.4) };
        double w = Math.Min(1.15, (areaW - 4.4) / Math.Max(1, ng - 1));
        for (int i = 0; i + 1 < ng; i++) cols.Add(new Col($"G{i + 1}-G{i + 2}", w));
        var notes = new[] { "SPACING = DISTANCE BETWEEN ADJACENT CL BEARINGS ALONG THE SUPPORT LINE (PLAN, FROM THE MODEL). A DASH = A GIRDER OF THE PAIR HAS NO BEARING AT THAT SUPPORT.",
                            "SKEW: ANGLE OF THE SUPPORT LINE FROM THE RADIAL (NORMAL TO THE CL ALIGNMENT); RT = RIGHT END UP-STATION." };
        return Paged(ctx, "GIRDER SPACING AT THE SUPPORT LINES", cols.ToArray(), rows, notes, areaW, areaH, "Girder spacing");
    }

    private static string Off(UnitSystem u, double off) => $"{u.FormatLength(Math.Abs(off))} {(off < 0 ? "LT" : "RT")}";

    /// <summary>Bearing data: type, fixity, station/offset, bottom of steel and top of bearing (seat) elevations per girder and support.</summary>
    public static List<(string title, SheetView view)> BearingData(ViewContext ctx, double areaW, double areaH)
    {
        var f = ctx.Framing;
        var u = ctx.Units;
        var rows = new List<string[]>();
        foreach (var sl in f.Supports.Concat(f.AuxSupports))
            for (int gi = 0; gi < sl.Bearings.Length; gi++)
            {
                if (sl.Bearings[gi] is not { } br) continue;
                var seat = ctx.Steel?.BearingSeats.Where(x => x.Girder == gi && x.Support == sl.Name).OrderBy(x => Math.Abs(x.Station - br.Station)).FirstOrDefault();
                rows.Add(new[]
                {
                    sl.Name.ToUpperInvariant(), $"G{gi + 1}", seat?.Type ?? "-", seat is { Fixity.Length: > 0 } ? seat.Fixity : "-",
                    u.FormatStation(br.Station), Off(u, br.Offset), u.FormatLength(br.BottomOfSteelElevation, LengthKind.Coordinate),
                    seat != null ? u.FormatLength(seat.SeatElevation, LengthKind.Coordinate) : "-",
                    seat != null ? u.FormatLength(br.BottomOfSteelElevation - seat.SeatElevation, LengthKind.Short) : "-",
                });
            }
        if (rows.Count == 0) return new();
        var cols = new[] { new Col("SUPPORT", 1.9), new Col("GIRDER", 0.8), new Col("BEARING", 1.6), new Col("FIXITY", 1.5), new Col("STATION (CL BRG)", 1.6), new Col("OFFSET FROM CL", 1.5),
            new Col("BOTTOM OF STEEL EL", 1.8), new Col("TOP OF BEARING EL", 1.8), new Col("STACK HEIGHT", 1.2) };
        var notes = new[]
        {
            "STATION AND OFFSET: CL BEARING AT THE GIRDER WEB CENTERLINE, REFERENCED TO THE CL ALIGNMENT.",
            "TOP OF BEARING = BEARING SEAT (TOP OF PEDESTAL / CAP) AS GENERATED; STACK HEIGHT = BOTTOM OF STEEL MINUS TOP OF BEARING.",
        };
        return Paged(ctx, "BEARING DATA", cols, rows, notes, areaW, areaH, "Bearing data");
    }

    /// <summary>Top of slab (finished grade at the girder line) or top of web elevations at the supports and quarter points of every span.</summary>
    public static List<(string title, SheetView view)> ElevationTable(ViewContext ctx, bool slab, double areaW, double areaH)
    {
        var f = ctx.Framing;
        var u = ctx.Units;
        var a = f.Alignment;
        var def = f.Definition;
        int ng = f.Girders.Count;
        var rows = new List<string[]>();
        string Val(GirderResult g, int span, double t)
        {
            if (span >= g.Spans.Count) return "-";
            double s = g.DistanceAt(span, t);
            if (s < -1e-6 || s > g.Path.Length + 1e-6) return "-";
            var (pt, _, sta) = g.Path.At(Math.Clamp(s, 0, g.Path.Length));
            double off = a.Inverse(pt.XY).Offset;
            double z = slab ? def.Deck.FinishedElevation(a, sta, off) : pt.Z;
            return u.FormatLength(z, LengthKind.Coordinate);
        }
        int nSpans = f.Supports.Count - 1;
        for (int j = 0; j < nSpans; j++)
        {
            foreach (double t in new[] { 0.0, 0.25, 0.5, 0.75 })
            {
                string label = t == 0 ? $"CL BRG {f.Supports[j].Name.ToUpperInvariant()}   STA {u.FormatStation(f.Supports[j].Station)}" : $"SPAN {j + 1}  {t * 100:0}%";
                rows.Add(new[] { label }.Concat(f.Girders.Select(g => Val(g, j, t))).ToArray());
            }
        }
        rows.Add(new[] { $"CL BRG {f.Supports[^1].Name.ToUpperInvariant()}   STA {u.FormatStation(f.Supports[^1].Station)}" }
            .Concat(f.Girders.Select(g => g.Spans.Count >= nSpans ? Val(g, nSpans - 1, 1.0) : "-")).ToArray());
        var cols = new[] { new Col("LOCATION (ALONG EACH GIRDER)", 2.9) }.Concat(f.Girders.Select(g => new Col(g.Name, Math.Min(1.1, (areaW - 3.0) / ng)))).ToArray();
        string title = slab ? "TOP OF SLAB ELEVATIONS AT GIRDER LINES" : "TOP OF WEB ELEVATIONS (FINAL SHAPE)";
        var notes = slab
            ? new[] { "TOP OF SLAB = FINISHED GRADE AT THE GIRDER WEB CENTERLINE (CROSS SLOPE, SUPERELEVATION AND ANY LEVEL EDGE STRIP APPLIED), FROM THE MODEL.",
                      "SPAN POINTS ARE FRACTIONS OF THE GIRDER LENGTH BETWEEN CL BEARINGS. A DASH = THE GIRDER DOES NOT EXIST AT THAT LOCATION." }
            : new[] { "TOP OF WEB = FINISHED GRADE - SLAB - HAUNCH, FROM THE MODEL (FINAL SHAPE; CAMBER AND BLOCKING ARE NOT INCLUDED).",
                      "SPAN POINTS ARE FRACTIONS OF THE GIRDER LENGTH BETWEEN CL BEARINGS. A DASH = THE GIRDER DOES NOT EXIST AT THAT LOCATION." };
        return Paged(ctx, title, cols, rows, notes, areaW, areaH, slab ? "Top of slab" : "Top of web");
    }
}
