using SBM.Core.Alignments;
using SBM.Core.Framing;
using SBM.Core.Steel;
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

    private static List<(string title, SheetView view)> Paged(ViewContext ctx, string title, Col[] cols, List<string[]> rows, IList<string> notes, double areaW, double areaH, string stem, int blocks = 1)
    {
        var pages = new List<(string, SheetView)>();
        double rowH = 0.19, gap = 0.35;
        int perBlock = Math.Max(5, (int)Math.Floor((areaH - 1.1 - 0.2 * notes.Count) / rowH) - 1);
        int perPage = perBlock * blocks;
        int nPages = Math.Max(1, (rows.Count + perPage - 1) / perPage);
        for (int p = 0; p < nPages; p++)
        {
            var dwg = new PlanDrawing();
            var d = new Drafter(dwg, ctx.Units, 1);
            string t = title + (nPages > 1 ? $" ({p + 1} OF {nPages})" : "");
            double tw = d.Paper(cols.Sum(c => c.Width)), rh = d.Paper(rowH);
            var page = rows.Skip(p * perPage).Take(perPage).ToList();
            d.Text(t, new Vec2(0, d.Paper(0.3)), d.Paper(0.14), 0, TextAlign.Left, SheetLayers.Title);
            int usedBlocks = Math.Max(1, (page.Count + perBlock - 1) / perBlock);
            for (int bk = 0; bk < usedBlocks; bk++)
            {
                var part = page.Skip(bk * perBlock).Take(perBlock).ToList();
                double x0 = bk * (tw + d.Paper(gap));
                d.Rect(SheetLayers.Text, x0, -rh * (part.Count + 1), x0 + tw, 0);
                double x = x0;
                foreach (var c in cols)
                {
                    d.Text(c.Header, new Vec2(x + d.Paper(0.05), -rh / 2), d.Paper(0.075), 0, TextAlign.Left);
                    x += d.Paper(c.Width);
                    d.Line(SheetLayers.Text, new Vec2(x, 0), new Vec2(x, -rh * (part.Count + 1)));
                }
                d.Line(SheetLayers.Text, new Vec2(x0, -rh), new Vec2(x0 + tw, -rh));
                for (int i = 0; i < part.Count; i++)
                {
                    double ry = -rh * (i + 1);
                    d.Line(SheetLayers.Text, new Vec2(x0, ry - rh), new Vec2(x0 + tw, ry - rh));
                    x = x0;
                    for (int k = 0; k < cols.Length; k++)
                    {
                        string v = k < part[i].Length ? part[i][k] : "";
                        d.Text(v, new Vec2(x + d.Paper(0.05), ry - rh / 2), d.Paper(0.075), 0, TextAlign.Left);
                        x += d.Paper(cols[k].Width);
                    }
                }
            }
            int rowsInFirst = Math.Min(page.Count, perBlock);
            if (p == nPages - 1)
                for (int i = 0; i < notes.Count; i++)
                    d.Text(notes[i], new Vec2(0, -rh * (rowsInFirst + 1) - d.Paper(0.25 + 0.17 * i)), d.Paper(0.075), 0, TextAlign.Left);
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

    private static string Dec(UnitSystem u, double m) => u.IsUS ? $"{m / 0.0254:0.00}\"" : $"{m * 1000:0.0} mm";
    private static string Pct(double slope) => double.IsNaN(slope) ? "-" : $"{slope * 100:0.###}%";

    /// <summary>Camber, deflections and blocking per numbered point of every girder that has camber data.</summary>
    public static List<(string title, SheetView view)> CamberBlocking(ViewContext ctx, double areaW, double areaH)
    {
        var u = ctx.Units;
        var rows = new List<string[]>();
        if (ctx.Framing.Camber == null) return new();
        foreach (var g in ctx.Framing.Camber.Girders.OrderBy(g => g.Girder))
            foreach (var p in g.Points)
                rows.Add(new[] { g.Name, p.Number.ToString(), p.Label.Length > 0 ? p.Label : p.Kind, u.FormatStation(p.Station), Dec(u, p.Camber), Dec(u, p.SteelDeflection), Dec(u, p.SlabDeflection),
                    u.FormatLength(p.HaunchElevation, LengthKind.Coordinate), double.IsNaN(p.Blocking) ? "-" : Dec(u, p.Blocking) });
        if (rows.Count == 0) return new();
        var cols = new[] { new Col("GIR.", 0.5), new Col("PT", 0.45), new Col("LOCATION", 0.85), new Col("STATION", 0.95), new Col("CAMBER", 0.65), new Col("STEEL DEFL", 0.75), new Col("SLAB+BARR", 0.75),
            new Col("HAUNCH EL", 0.9), new Col("BLOCKING", 0.7) };
        var notes = new[] { "AS COMPUTED FROM THE MODEL'S CAMBER INPUT (DEFLECTIONS AND CAMBER ORDINATES AS GIVEN); BLOCKING = NO-LOAD TOP OF WEB ABOVE A HORIZONTAL LINE THROUGH THE DATUM POINT.",
                            "POINTS BETWEEN BEARINGS AND FIELD SPLICES ARE EQUALLY SPACED ALONG THE GIRDER. A DASH = NO DATUM POINT LOCATED." };
        return Paged(ctx, "CAMBER AND BLOCKING", cols, rows, notes, areaW, areaH, "Camber and blocking", blocks: 2);
    }

    /// <summary>The alignment's cross slope table (rows near the bridge): slopes and breaks.</summary>
    public static List<(string title, SheetView view)> CrossSlope(ViewContext ctx, double areaW, double areaH)
    {
        var u = ctx.Units;
        var a = ctx.Framing.Alignment;
        var (b, e) = LayoutViews.DeckEndStations(ctx.Framing);
        double margin = 100 * u.LongUnit;
        var all = a.CrossSlope.Rows.OrderBy(r => r.Station).ToList();
        var rows = new List<string[]>();
        for (int i = 0; i < all.Count; i++)
        {
            var r = all[i];
            bool near = r.Station >= b - margin && r.Station <= e + margin;
            bool bracket = (i + 1 < all.Count && all[i + 1].Station >= b - margin && r.Station < b - margin) || (i > 0 && all[i - 1].Station <= e + margin && r.Station > e + margin);
            if (!near && !bracket) continue;
            string Brk(double d, double sl, List<CrossSlopeBreak>? more) => d > 0 ? $"{u.FormatLength(d)} / {Pct(sl)}" + (more?.Count > 0 ? "; " + string.Join("; ", more.Select(m => $"{u.FormatLength(m.Distance)} / {Pct(m.Slope)}")) : "") : "-";
            rows.Add(new[] { u.FormatStation(r.Station), Pct(r.LeftSlope), Pct(r.RightSlope), Brk(r.LeftBreak, r.LeftOuterSlope, r.LeftMoreBreaks), Brk(r.RightBreak, r.RightOuterSlope, r.RightMoreBreaks) });
        }
        if (rows.Count == 0) return new();
        var cols = new[] { new Col("STATION", 1.6), new Col("LEFT SLOPE", 1.3), new Col("RIGHT SLOPE", 1.3), new Col("LEFT BREAKS (DISTANCE FROM PIVOT / SLOPE BEYOND)", 5.0), new Col("RIGHT BREAKS", 5.0) };
        var notes = new[] { $"CROSS SLOPE TABLE OF {a.Name.ToUpperInvariant()} (ROWS WITHIN 100 FT OF THE BRIDGE AND THE BRACKETING ROWS); SLOPE = RISE / RUN, NEGATIVE = FALLING AWAY FROM THE PIVOT; SLOPES INTERPOLATE BETWEEN STATIONS." };
        return Paged(ctx, "CROSS SLOPE GEOMETRICS", cols, rows, notes, areaW, areaH, "Cross slope");
    }

    /// <summary>Estimated quantities from the generated model: structural steel by assembly kind, bolts, studs, concrete and reinforcing.</summary>
    public static List<(string title, SheetView view)> Quantities(ViewContext ctx, double areaW, double areaH)
    {
        var u = ctx.Units;
        double wk = u.IsUS ? 2.20462 : 1; string wn = u.IsUS ? "LB" : "KG";
        double vk = u.IsUS ? 1.307951 : 1; string vn = u.IsUS ? "CY" : "M3";
        var rows = new List<string[]>();
        void Row(string item, string qty, string unit, string basis) => rows.Add(new[] { item, qty, unit, basis });
        if (ctx.Steel != null)
        {
            var st = ctx.Steel;
            var kinds = st.Assemblies.ToDictionary(a => a.Mark, a => a.Kind);
            double Sum(Func<SteelPart, bool> f) => st.Parts.Where(f).Sum(p => p.Mass) * wk;
            string[] names = { "GIRDER FIELD SECTIONS", "CROSS FRAMES AND CROSS GIRDERS", "FIELD SPLICES", "BEARINGS", "LATERAL BRACING" };
            var ak = new[] { AssemblyKind.GirderFieldSection, AssemblyKind.CrossFrame, AssemblyKind.FieldSplice, AssemblyKind.Bearing, AssemblyKind.LateralBracing };
            double total = 0;
            for (int i = 0; i < ak.Length; i++)
            {
                double m = Sum(p => kinds.TryGetValue(p.Assembly, out var k) && k == ak[i]);
                if (m > 0) { Row("STRUCTURAL STEEL - " + names[i], $"{m:N0}", wn, "SUM OF PART MASSES (PLATES BY VOLUME, ROLLED SHAPES BY NOMINAL WEIGHT)"); total += m; }
            }
            double insp = Sum(p => kinds.TryGetValue(p.Assembly, out var k) && (k == AssemblyKind.InspectionCable || k == AssemblyKind.InspectionWalkway));
            if (insp > 0) { Row("STRUCTURAL STEEL - INSPECTION ACCESS", $"{insp:N0}", wn, "SUM OF PART MASSES"); total += insp; }
            Row("STRUCTURAL STEEL - TOTAL", $"{total:N0}", wn, "");
            foreach (var g in st.Bolts.Where(b => b.Kind == BoltKind.Structural).GroupBy(b => Math.Round((b.Diameter > 0 ? b.Diameter : st.Bolt.Diameter) / 0.0254 * 1000) / 1000).OrderBy(g => g.Key))
                Row($"HIGH-STRENGTH BOLTS {st.BoltGrade}, {g.Key:0.###}\" DIA", $"{g.Count():N0}", "EACH", "COUNT OF BOLT INSTANCES");
            if (st.Bolts.Any(b => b.Kind == BoltKind.Anchor)) Row("ANCHOR BOLTS", $"{st.Bolts.Count(b => b.Kind == BoltKind.Anchor):N0}", "EACH", "COUNT");
            if (st.Studs.Count > 0) Row($"SHEAR STUDS {st.StudDiameter / 0.0254:0.###}\" DIA", $"{st.Studs.Count:N0}", "EACH", "COUNT");
        }
        if (ctx.Concrete is { } c)
        {
            if (c.DeckVolume > 0) Row("CONCRETE - DECK SLAB", $"{c.DeckVolume * vk:N1}", vn, "SOLID VOLUME (HAUNCHES SEPARATE)");
            if (c.HaunchVolume > 0) Row("CONCRETE - HAUNCHES", $"{c.HaunchVolume * vk:N1}", vn, "SOLID VOLUME");
            if (c.BarrierVolume > 0) Row("CONCRETE - BARRIER RAIL", $"{c.BarrierVolume * vk:N1}", vn, "SOLID VOLUME");
            if (c.RebarMass > 0) Row("REINFORCING STEEL - SUPERSTRUCTURE", $"{c.RebarMass * wk:N0}", wn, "SUM OF BAR MARKS (GENERATED BARS; AS-DESIGNED DECK / RAIL BARS MAY DIFFER)");
        }
        if (ctx.Substructure is { } sub)
        {
            if (sub.ConcreteVolume > 0) Row("CONCRETE - SUBSTRUCTURE", $"{sub.ConcreteVolume * vk:N1}", vn, "SOLID VOLUME OF THE SUPPORT UNITS GENERATED FOR THIS BRIDGE (CAPS, COLUMNS, FOOTINGS, ABUTMENTS)");
            if (sub.RebarMass > 0) Row("REINFORCING STEEL - SUBSTRUCTURE", $"{sub.RebarMass * wk:N0}", wn, "SUM OF BAR MARKS");
        }
        if (rows.Count == 0) return new();
        var cols = new[] { new Col("ITEM", 6.2), new Col("QUANTITY", 1.5), new Col("UNIT", 0.9), new Col("BASIS", 6.0) };
        var notes = new[] { "QUANTITIES OF THIS BRIDGE FROM THE GENERATED MODEL; THEY ARE NOT BID QUANTITIES (NO WASTE, NO ITEMS THE MODEL DOES NOT HAVE)." };
        return Paged(ctx, "ESTIMATED QUANTITIES", cols, rows, notes, areaW, areaH, "Quantities");
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
