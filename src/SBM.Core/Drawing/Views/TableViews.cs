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
                        // Cut to the column (Helvetica width estimate) so a long cell never runs into the next column.
                        double maxH = (cols[k].Width - 0.08) / 0.075;
                        if (LabelPlacer.WidthInHeights(v) > maxH)
                        {
                            while (v.Length > 1 && LabelPlacer.WidthInHeights(v + "...") > maxH) v = v[..^1];
                            v += "...";
                        }
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
            var r = new List<string> { sl.Name.ToUpperInvariant(), u.FormatStation(sl.Station), sl.SkewDegrees == 0 ? "0 DEG (SQUARE)" : $"{UnitSystem.FormatAngleDms(Math.Abs(sl.SkewDegrees) * Math.PI / 180, false)} {(sl.SkewDegrees > 0 ? "RT" : "LT")}" };
            for (int i = 0; i + 1 < ng; i++)
            {
                var b0 = i < sl.Bearings.Length ? sl.Bearings[i] : null; var b1 = sl.Bearings[i + 1];
                r.Add(b0 != null && b1 != null ? u.FormatLength(Vec2.Distance(a.PointAt(b0.Station, b0.Offset), a.PointAt(b1.Station, b1.Offset))) : "-");
            }
            rows.Add(r.ToArray());
        }
        var cols = new List<Col> { new("SUPPORT", 1.5), new("STATION (CL)", 1.4), new("SKEW", 1.4) };
        double w = Math.Min(1.15, (areaW - 4.4) / Math.Max(1, ng - 1));
        for (int i = 0; i + 1 < ng; i++) cols.Add(new Col($"{ctx.GirderName(i)}-{ctx.GirderName(i + 1)}", w));
        var notes = new[] { "SPACING = DISTANCE BETWEEN ADJACENT CL BEARINGS ALONG THE SUPPORT LINE (PLAN, FROM THE MODEL). A DASH = A GIRDER OF THE PAIR HAS NO BEARING AT THAT SUPPORT.",
                            "SKEW: ANGLE OF THE SUPPORT LINE FROM THE RADIAL (NORMAL TO THE CL ALIGNMENT); RT = RIGHT END UP-STATION." };
        return Paged(ctx, "GIRDER SPACING AT THE SUPPORT LINES", cols.ToArray(), rows, notes, areaW, areaH, "Girder spacing");
    }

    /// <summary>OPTIONAL standard cross frames: hole group shift of every frame end on its connection plate (from the model's report), sorted by girder line.</summary>
    public static List<(string title, SheetView view)> CrossFrameHoleShifts(ViewContext ctx, double areaW, double areaH)
    {
        var u = ctx.Units;
        var rep = ctx.Steel?.CrossFrameFit;
        if (rep == null || !rep.Rows.Any(r => r.Applied)) return new();
        string Sh(double m) => double.IsNaN(m) ? "-" : $"{(m >= 0 ? "+" : "-")}{u.FormatLength(Math.Abs(m), LengthKind.Short)} ({Dec(u, m)})";
        var rows = rep.Rows.Where(r => r.Applied).OrderBy(r => r.LeftGirder).ThenBy(r => r.LeftS)
            .Select(r => new[] { r.Mark, r.Family, $"{ctx.GirderName(r.LeftGirder)}-{ctx.GirderName(r.RightGirder)}", (r.Span + 1).ToString(), u.FormatStation(r.Station),
                r.LeftPlate, Sh(r.ShiftTopL), r.RightPlate, Sh(r.ShiftTopR), $"{r.TwistDegrees:0.000}", Dec(u, r.MaxGapBuilt), double.IsNaN(r.Headroom) ? "-" : Dec(u, r.Headroom) }).ToList();
        var cols = new[] { new Col("FRAME", 0.7), new Col("FAMILY", 0.6), new Col("BAY", 0.7), new Col("SPAN", 0.45), new Col("STATION", 1.0), new Col("LEFT PLATE", 1.1),
            new Col("SHIFT L", 1.25), new Col("RIGHT PLATE", 1.1), new Col("SHIFT R", 1.25), new Col("TWIST DEG", 0.75), new Col("GAP", 0.65), new Col("HEADROOM", 0.85) };
        var notes = new List<string> { "HOLES IN THE CONNECTION PLATES ARE LOCATED PER THIS TABLE; NO SLOTTED HOLES. SHIFT = HOLE GROUP POSITION ALONG THE PLATE FROM THE STANDARD PATTERN (+ = UP), ROUNDED, WITH THE EXACT VALUE.",
            "TWIST = ANGLE BETWEEN THE TWO CONNECTION PLATES ABOUT THE FRAME LINE; GAP = LARGEST FAR-GUSSET GAP ALONG A BOLT; HEADROOM = DEEPEST FRAME THAT FITS THERE LESS THE STANDARD DEPTH (NEGATIVE = DOES NOT FIT)." };
        foreach (var s in rep.Standards.Where(s => s.Applied))
            notes.Add($"STANDARD {s.Name.ToUpperInvariant()}: TOP WP {u.FormatLength(s.TopWorkPoint, LengthKind.Short)} BELOW THE REFERENCE AT THE DEFINED END, DEPTH {u.FormatLength(s.Depth, LengthKind.Short)}; DEEPEST COMMON DEPTH {Dec(u, s.MaxCommonDepth)} AT {s.GoverningMark}.");
        return Paged(ctx, "STANDARD CROSS FRAMES - HOLE SHIFT TABLE", cols, rows, notes, areaW, areaH, "Cross frame hole shifts");
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
                rows.Add(new[] { ctx.GirderName(g.Girder), p.Number.ToString(), p.Label.Length > 0 ? p.Label : p.Kind, u.FormatStation(p.Station), Dec(u, p.Camber), Dec(u, p.SteelDeflection), Dec(u, p.SlabDeflection),
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

    /// <summary>One estimated quantity row: plan-style group, item, quantity text, unit, basis; <see cref="Value"/> = the number (NaN = not modeled).</summary>
    public sealed record QuantityRow(string Group, string Item, double Value, string Quantity, string Unit, string Basis);

    /// <summary>
    /// Estimated quantities from the generated model in plan item groupings: superstructure (structural steel by assembly kind, steel
    /// diaphragms, prestressed beams per type, strand, deck / haunch / barrier / diaphragm concrete, bearing pads, reinforcing by coating),
    /// substructure per unit (structural concrete WITHOUT the concrete fill of pipe piles, which belongs to the piling item; reinforcing by
    /// coating), piling (LF and count per pile type, pile fill concrete separately) and the reinforcing groups the model does not have
    /// ("NOT MODELED" rows, so the totals can be reconciled with the plan's item list).
    /// </summary>
    public static List<QuantityRow> QuantityRows(ViewContext ctx)
    {
        var u = ctx.Units;
        double wk = u.IsUS ? 2.20462 : 1; string wn = u.IsUS ? "LB" : "KG";
        double vk = u.IsUS ? 1.307951 : 1; string vn = u.IsUS ? "CY" : "M3";
        double lk = 1 / u.LongUnit; string ln = u.IsUS ? "LF" : "M";
        var rows = new List<QuantityRow>();
        void Row(string group, string item, double v, string fmt, string unit, string basis) => rows.Add(new QuantityRow(group, item, v, v.ToString(fmt), unit, basis));
        void Missing(string group, string item, string basis) => rows.Add(new QuantityRow(group, item, double.NaN, "NOT MODELED", "", basis));
        const string Sup = "SUPERSTRUCTURE", Sub = "SUBSTRUCTURE", Pil = "PILING", Rf = "REINFORCING STEEL";
        var ps = ctx.Prestressed is { Beams.Count: > 0 } p0 ? p0 : null;

        // Coating class of a bar mark: epoxy / stainless / plain (plan pay items differ).
        static string Coat(Concrete.BarMark m)
        {
            string c = m.Coating.ToLowerInvariant();
            if (c.Contains("stainless")) return "STAINLESS STEEL";
            if (m.Epoxy || c.Contains("epoxy")) return "EPOXY COATED";
            if (c.Contains("galv")) return "GALVANIZED";
            return c.Length > 0 && !c.Contains("plain") && !c.Contains("black") && !c.Contains("uncoated") ? m.Coating.ToUpperInvariant() : "PLAIN";
        }
        void Rebar(string group, string where, IEnumerable<Concrete.BarMark> marks, string basis)
        {
            foreach (var g in marks.GroupBy(Coat).OrderBy(g => g.Key))
            {
                double m = g.Sum(x => x.TotalMass) * wk;
                if (m > 0) Row(group, $"REINFORCING STEEL, {g.Key} - {where}", m, "N0", wn, basis);
            }
        }

        // ---- structural steel
        if (ctx.Steel != null && (ctx.Steel.Parts.Count > 0 || ctx.Steel.Bolts.Count > 0))
        {
            var st = ctx.Steel;
            var kinds = st.Assemblies.ToDictionary(a => a.Mark, a => a.Kind);
            var diaph = ps?.SteelDiaphragms is { } sdm ? sdm.Assemblies.Select(a => a.Mark).ToHashSet() : new HashSet<string>();
            double Sum(Func<SteelPart, bool> f) => st.Parts.Where(f).Sum(p => p.Mass) * wk;
            string[] names = { "GIRDER FIELD SECTIONS", "CROSS FRAMES AND CROSS GIRDERS", "FIELD SPLICES", "BEARINGS", "LATERAL BRACING" };
            var ak = new[] { AssemblyKind.GirderFieldSection, AssemblyKind.CrossFrame, AssemblyKind.FieldSplice, AssemblyKind.Bearing, AssemblyKind.LateralBracing };
            double total = 0;
            double dm = Sum(p => diaph.Contains(p.Assembly));
            if (dm > 0) { Row(Sup, "STRUCTURAL STEEL - STEEL INTERMEDIATE DIAPHRAGMS", dm, "N0", wn, "SUM OF PART MASSES (ROLLED SHAPES BY NOMINAL WEIGHT)"); total += dm; }
            for (int i = 0; i < ak.Length; i++)
            {
                double m = Sum(p => !diaph.Contains(p.Assembly) && kinds.TryGetValue(p.Assembly, out var k) && k == ak[i]);
                if (m > 0) { Row(Sup, "STRUCTURAL STEEL - " + names[i], m, "N0", wn, "SUM OF PART MASSES (PLATES BY VOLUME, ROLLED SHAPES BY NOMINAL WEIGHT)"); total += m; }
            }
            double insp = Sum(p => kinds.TryGetValue(p.Assembly, out var k) && (k == AssemblyKind.InspectionCable || k == AssemblyKind.InspectionWalkway));
            if (insp > 0) { Row(Sup, "STRUCTURAL STEEL - INSPECTION ACCESS", insp, "N0", wn, "SUM OF PART MASSES"); total += insp; }
            if (total > 0) Row(Sup, "STRUCTURAL STEEL - TOTAL", total, "N0", wn, "");
            if (st.Railings is { Runs.Count: > 0 } rails)
                foreach (var g in rails.Runs.GroupBy(r => r.TypeName.Length > 0 ? r.TypeName : "PEDESTRIAN RAILING"))
                    Row(Sup, g.Key.ToUpperInvariant(), g.Sum(r => r.Length) / (u.IsUS ? 0.3048 : 1), "N1", u.IsUS ? "LF" : "M", "LENGTH ALONG THE RAILING REFERENCE LINE");
            if (st.Railings is { Runs.Count: > 0 } rails2 && rails2.Runs.Sum(r => r.FabricArea) > 0)
                Row(Sup, "PEDESTRIAN RAILING FENCE FABRIC", rails2.Runs.Sum(r => r.FabricArea) / (u.IsUS ? 0.09290304 : 1), "N0", u.IsUS ? "SF" : "M2", rails2.Runs.Sum(r => r.FabricMass) <= 0 ? "PANEL AREA, ONE FACE (FABRIC TYPE NOT DETERMINED: WEIGHT NOT COUNTED)" : "PANEL AREA, ONE FACE (NOMINAL WEIGHT PER AREA)");
            foreach (var g in st.Bolts.Where(b => b.Kind == BoltKind.Structural).GroupBy(b => Math.Round((b.Diameter > 0 ? b.Diameter : st.Bolt.Diameter) / 0.0254 * 1000) / 1000).OrderBy(g => g.Key))
                Row(Sup, $"HIGH-STRENGTH BOLTS {st.BoltGrade}, {g.Key:0.###}\" DIA", g.Count(), "N0", "EACH", "COUNT OF BOLT INSTANCES");
            if (st.Bolts.Any(b => b.Kind == BoltKind.Anchor)) Row(Sup, "ANCHOR BOLTS", st.Bolts.Count(b => b.Kind == BoltKind.Anchor), "N0", "EACH", "COUNT");
            if (st.Studs.Count > 0) Row(Sup, $"SHEAR STUDS {st.StudDiameter / 0.0254:0.###}\" DIA", st.Studs.Count, "N0", "EACH", "COUNT");
        }

        // ---- prestressed beams
        if (ps != null)
        {
            foreach (var (type, def, beams) in PsText.Types(ps))
            {
                Row(Sup, $"BEAMS, PRETENSIONED PRESTRESSED CONCRETE, {type}", beams.Count, "N0", "EACH", $"{PsText.TypeName(beams[0].Shape, type)}, f'c {def.ConcreteStrength:0.0#} KSI");
                Row(Sup, $"  {type} - LENGTH (END TO END)", beams.Sum(b => b.Length) * lk, "N1", ln, "SUM OF BEAM LENGTHS");
                Row(Sup, $"  {type} - BEAM CONCRETE", beams.Sum(b => b.Volume) * vk, "N1", vn, "SOLID VOLUME (PART OF THE BEAM ITEM)");
            }
            Row(Sup, "PRESTRESSING STRAND (IN THE BEAMS)", ps.StrandLength * lk, "N0", ln, $"SUM OF STRAND LENGTHS{(ps.DebondedLength > 0 ? $"; {ps.DebondedLength * lk:N0} {ln} DEBONDED (SHEATHED)" : "")}");
            foreach (var g in ps.Pads.GroupBy(p => p.Description != "" ? p.Description : "Pad"))
                Row(Sup, $"BEARING PADS - {g.Key.ToUpperInvariant()}", g.Count(), "N0", "EACH", "COUNT OF MODELED PADS");
            var beamBars = PsBars.Marks(ps);
            if (beamBars.Count > 0) Rebar(Rf, "PRESTRESSED BEAMS", beamBars, "SUM OF BEAM BAR MARKS (USUALLY PART OF THE BEAM ITEM)");
            else Missing(Rf, "REINFORCING STEEL - PRESTRESSED BEAMS", "BEAM BARS NOT IN THE MODEL (USUALLY PART OF THE BEAM ITEM)");
        }

        // ---- deck, haunches, barrier, diaphragms
        if (ctx.Concrete is { } c)
        {
            if (c.DeckVolume > 0) Row(Sup, "CONCRETE - DECK SLAB", c.DeckVolume * vk, "N1", vn, "SOLID VOLUME (HAUNCHES SEPARATE)");
            if (c.HaunchVolume > 0) Row(Sup, "CONCRETE - HAUNCHES", c.HaunchVolume * vk, "N1", vn, "SOLID VOLUME");
            foreach (var ch in c.CapHaunches) Row(Sup, $"CONCRETE - PIER CAP HAUNCH ({ch.Support.ToUpperInvariant()})", ch.Volume * vk, "N2", vn, "SOLID VOLUME (DECK PLACEMENT)");
            if (c.BarrierVolume > 0) Row(Sup, "CONCRETE - BARRIER RAIL", c.BarrierVolume * vk, "N1", vn, "SOLID VOLUME");
        }
        if (ps?.ConcreteDiaphragms is { Diaphragms.Count: > 0 } cdm)
            Row(Sup, "CONCRETE - END / PIER DIAPHRAGMS", cdm.ConcreteVolume * vk, "N1", vn, $"SOLID VOLUME ({string.Join(", ", cdm.Diaphragms.Select(d => $"{d.Support.ToUpperInvariant()} {d.Volume * vk:N1}"))})");
        if (ctx.Concrete is { } c2)
        {
            if (c2.Marks.Count > 0) Rebar(Rf, "SUPERSTRUCTURE (DECK / BARRIER)", c2.Marks, "SUM OF BAR MARKS");
            else if (c2.DeckVolume > 0) Missing(Rf, "REINFORCING STEEL - DECK AND BARRIER", "DECK / BARRIER BARS NOT IN THE MODEL");
        }
        if (ps?.ConcreteDiaphragms is { Diaphragms.Count: > 0 })
            Missing(Rf, "REINFORCING STEEL - CONCRETE DIAPHRAGMS", "DIAPHRAGM BARS NOT IN THE MODEL");

        // ---- substructure per unit, piling
        if (ctx.Substructure is { } sub)
        {
            double totC = 0, fill = 0;
            var piling = new Dictionary<string, (int n, double len)>();
            foreach (var unit in sub.Units)
            {
                double vol = unit.Solids.Where(x => x.IsConcrete && x.Kind != Substructure.SubElementKind.Pile && x.Kind != Substructure.SubElementKind.PileCasing).Sum(x => Math.Abs(x.Mesh.Volume()));
                double pf = unit.Solids.Where(x => x.IsConcrete && x.Kind == Substructure.SubElementKind.Pile).Sum(x => Math.Abs(x.Mesh.Volume()));
                fill += pf;
                if (vol > 0)
                {
                    var parts = unit.Solids.Where(x => x.IsConcrete && x.Kind != Substructure.SubElementKind.Pile && x.Kind != Substructure.SubElementKind.PileCasing)
                        .GroupBy(x => x.Kind).Select(g => $"{g.Key.ToString().ToUpperInvariant()} {g.Sum(x => Math.Abs(x.Mesh.Volume())) * vk:N1}");
                    Row(Sub, $"STRUCTURAL CONCRETE - {unit.Name.ToUpperInvariant()}", vol * vk, "N1", vn, "SOLID VOLUME: " + string.Join(", ", parts));
                    totC += vol;
                }
                var piles = unit.Solids.Where(x => x.Kind == Substructure.SubElementKind.Pile).ToList();
                var infos = unit.Foundations.SelectMany(fo => fo.Piles).ToList();
                if (piles.Count > 0)
                {
                    string type = PileType(piles[0].Description);
                    double len = infos.Count == piles.Count ? infos.Sum(pi => pi.Length) : piles.Sum(x => PileLength(x));
                    piling[type] = piling.TryGetValue(type, out var cur) ? (cur.n + piles.Count, cur.len + len) : (piles.Count, len);
                }
            }
            if (totC > 0) Row(Sub, "STRUCTURAL CONCRETE - SUBSTRUCTURE TOTAL", totC * vk, "N1", vn, "CAPS, COLUMNS, FOOTINGS / PILE CAPS, ABUTMENT WALLS, PEDESTALS (NO PILE FILL)");
            foreach (var (type, (n, len)) in piling.OrderBy(k => k.Key))
            {
                Row(Pil, $"PILES, {type.ToUpperInvariant()}", len * lk, "N0", ln, $"{n} PILES, SUM OF MODELED PILE LENGTHS");
            }
            if (fill > 0) Row(Pil, "CONCRETE FILL OF PIPE PILES", fill * vk, "N1", vn, "PART OF THE PILING ITEM, NOT STRUCTURAL CONCRETE");
            var casing = sub.Units.SelectMany(un => un.Solids).Where(x => x.Kind == Substructure.SubElementKind.PileCasing).ToList();
            if (casing.Count > 0) Row(Pil, "PILE CASING", casing.Sum(PileLength) * lk, "N0", ln, $"{casing.Count} CASINGS");
            foreach (var unit in sub.Units)
            {
                var marks = sub.Marks.Where(m => m.Unit == unit.Name).ToList();
                if (marks.Count > 0) Rebar(Rf, unit.Name.ToUpperInvariant(), marks, "SUM OF BAR MARKS");
                bool onlyPile = marks.Count > 0 && marks.All(m => m.Location.Contains("pile", StringComparison.OrdinalIgnoreCase) || m.Mark.Contains("pile", StringComparison.OrdinalIgnoreCase));
                if (marks.Count == 0 || onlyPile)
                    Missing(Rf, $"REINFORCING STEEL - {unit.Name.ToUpperInvariant()}{(onlyPile ? " (EXCEPT PILE BARS)" : "")}", unit.Abutment != null ? "ABUTMENT BARS NOT IN THE MODEL" : "BARS NOT IN THE MODEL");
            }
            var other = sub.Marks.Where(m => sub.Units.All(un => un.Name != m.Unit)).ToList();
            if (other.Count > 0) Rebar(Rf, "SUBSTRUCTURE (OTHER)", other, "SUM OF BAR MARKS");
        }
        // Reinforcing totals by coating (what the plan's pay items list).
        var rf = rows.Where(r => r.Group == Rf && !double.IsNaN(r.Value)).ToList();
        foreach (var g in rf.GroupBy(r => r.Item.Split(" - ")[0]).OrderBy(g => g.Key))
            Row(Rf, $"{g.Key} - TOTAL (MODELED BARS)", g.Sum(r => r.Value), "N0", wn, "SUM OF THE ROWS ABOVE");
        return rows;
    }

    private static string PileType(string description)
    {
        string d = description.StartsWith("Pile ", StringComparison.OrdinalIgnoreCase) ? description[5..] : description;
        int i = d.IndexOf(", ", StringComparison.Ordinal);
        // keep the shell thickness of pipe piles ("12" CIP concrete-filled pipe, 1/4" shell")
        if (i > 0 && d[(i + 2)..].Contains("shell", StringComparison.OrdinalIgnoreCase))
        {
            int j = d.IndexOf(", ", i + 2, StringComparison.Ordinal);
            return j > 0 ? d[..j] : d;
        }
        return i > 0 ? d[..i] : d;
    }

    private static double PileLength(Substructure.SubSolid s)
    {
        var b = s.Mesh.Bounds();
        return b.Max.Z - b.Min.Z;
    }

    /// <summary>Estimated quantities from the generated model, grouped like the plan item lists (see <see cref="QuantityRows"/>).</summary>
    public static List<(string title, SheetView view)> Quantities(ViewContext ctx, double areaW, double areaH)
    {
        var q = QuantityRows(ctx);
        if (q.Count == 0) return new();
        var rows = new List<string[]>();
        foreach (var g in q.GroupBy(r => r.Group))
        {
            rows.Add(new[] { g.Key, "", "", "" });
            rows.AddRange(g.Select(r => new[] { "  " + r.Item, r.Quantity, r.Unit, r.Basis }));
        }
        var cols = new[] { new Col("ITEM", 6.0), new Col("QUANTITY", 1.3), new Col("UNIT", 0.7), new Col("BASIS", 7.2) };
        var notes = new[]
        {
            "QUANTITIES OF THIS BRIDGE FROM THE GENERATED MODEL; THEY ARE NOT BID QUANTITIES (NO WASTE, NO ITEMS THE MODEL DOES NOT HAVE).",
            "NOT MODELED = THE PLAN HAS THE ITEM BUT THE MODEL HAS NO BARS FOR IT: THE REINFORCING TOTALS COVER THE MODELED BARS ONLY.",
        };
        return Paged(ctx, "ESTIMATED QUANTITIES", cols, rows, notes, areaW, areaH, "Quantities");
    }

    private static string Off(UnitSystem u, double off) => $"{u.FormatLength(Math.Abs(off))} {(off < 0 ? "LT" : "RT")}";

    /// <summary>Bearing data: type, fixity, station/offset, bottom of steel and top of bearing (seat) elevations per girder and support.</summary>
    public static List<(string title, SheetView view)> BearingData(ViewContext ctx, double areaW, double areaH)
    {
        var f = ctx.Framing;
        var u = ctx.Units;
        var rows = new List<string[]>();
        var ps = ctx.Prestressed;
        bool isPs = ps is { Beams.Count: > 0 };
        // Prestressed: seats and pads of the generated beams (back and ahead bearings at piers); steel: the bearing detailer's seats.
        var seats = isPs && ps!.BearingSeats.Count > 0 ? ps.BearingSeats : ctx.Steel?.BearingSeats ?? new List<BearingSeat>();
        var def = f.Definition;
        void Add(SupportLineResult sl, int si, BearingResult br, int gi, bool ahead)
        {
            var seat = seats.Where(x => x.Girder == gi && x.Support == sl.Name)
                .OrderBy(x => x.Location is { } loc ? Vec2.Distance(loc, f.Alignment.PointAt(br.Station, br.Offset)) : Math.Abs(x.Station - br.Station)).FirstOrDefault();
            string type, fix;
            if (isPs)
            {
                int span = ahead || si == 0 ? si : si - 1;
                var pad = ps!.Pads.FirstOrDefault(p => p.Support == sl.Name && p.Girder == gi && p.Span == span) ?? ps.Pads.FirstOrDefault(p => p.Support == sl.Name && p.Girder == gi);
                type = pad != null ? (pad.Description != "" ? pad.Description : $"Pad {u.FormatLength(pad.Length, LengthKind.Short)} x {u.FormatLength(pad.Width, LengthKind.Short)} x {u.FormatLength(pad.Thickness, LengthKind.Short)}")
                     : seat?.Type ?? "-";
                fix = si >= 0 ? PsText.Fixity(def, si, gi) : "";
                if (fix == "" && seat is { Fixity.Length: > 0 }) fix = PsText.FixityCode(seat.Fixity);
            }
            else
            {
                type = seat == null ? "-" : seat.Type == "Generic" ? "BY MANUFACTURER" : seat.Type.ToUpperInvariant();
                fix = seat is { Fixity.Length: > 0 } ? PsText.FixityCode(seat.Fixity) : "";
            }
            rows.Add(new[]
            {
                sl.Name.ToUpperInvariant() + (ahead ? " (AHEAD)" : sl.AheadBearings != null ? " (BACK)" : ""), ctx.GirderName(gi), type, fix == "" ? "NOT MODELED" : fix,
                u.FormatStation(br.Station), Off(u, br.Offset), u.FormatLength(br.BottomOfSteelElevation, LengthKind.Coordinate),
                seat != null ? u.FormatLength(seat.SeatElevation, LengthKind.Coordinate) : "-",
                seat != null ? u.FormatLength(br.BottomOfSteelElevation - seat.SeatElevation, LengthKind.Short) : "-",
            });
        }
        foreach (var sl in f.Supports.Concat(f.AuxSupports))
        {
            int si = f.Supports.IndexOf(sl);
            for (int gi = 0; gi < sl.Bearings.Length; gi++)
                if (sl.Bearings[gi] is { } br) Add(sl, si, br, gi, false);
            if (sl.AheadBearings is { } ah)
                for (int gi = 0; gi < ah.Length; gi++)
                    if (ah[gi] is { } br) Add(sl, si, br, gi, true);
        }
        if (rows.Count == 0) return new();
        var cols = new[] { new Col("SUPPORT", 1.9), new Col("GIRDER", 0.7), new Col("BEARING", isPs ? 2.9 : 1.6), new Col("FIXITY", 1.4), new Col("STATION (CL BRG)", 1.4), new Col("OFFSET FROM CL", 1.3),
            new Col(isPs ? "BOTTOM OF BEAM EL" : "BOTTOM OF STEEL EL", 1.6), new Col("TOP OF BEARING EL", 1.6), new Col("STACK HEIGHT", 1.1) };
        var notes = new List<string>
        {
            "STATION AND OFFSET: CL BEARING AT THE " + (isPs ? "BEAM" : "GIRDER WEB") + " CENTERLINE, REFERENCED TO THE CL ALIGNMENT.",
            "TOP OF BEARING = BEARING SEAT (TOP OF PEDESTAL / CAP / BEAM STEP) AS GENERATED; STACK HEIGHT = BOTTOM OF " + (isPs ? "BEAM" : "STEEL") + " MINUS TOP OF BEARING.",
            "FIXITY: FROM THE SUPPORT DEFINITION (FX FIXED, GE GUIDED EXPANSION, MD MULTIDIRECTIONAL EXPANSION; INTEGRAL ABUTMENTS); NOT MODELED = THE MODEL USES ITS DEFAULT.",
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
        var cols = new[] { new Col("LOCATION (ALONG EACH GIRDER)", 2.9) }.Concat(f.Girders.Select(g => new Col(ctx.GirderName(g.Index), Math.Min(1.1, (areaW - 3.0) / ng)))).ToArray();
        string title = slab ? "TOP OF SLAB ELEVATIONS AT GIRDER LINES" : "TOP OF WEB ELEVATIONS (FINAL SHAPE)";
        var notes = slab
            ? new[] { "TOP OF SLAB = FINISHED GRADE AT THE GIRDER WEB CENTERLINE (CROSS SLOPE, SUPERELEVATION AND ANY LEVEL EDGE STRIP APPLIED), FROM THE MODEL.",
                      "SPAN POINTS ARE FRACTIONS OF THE GIRDER LENGTH BETWEEN CL BEARINGS. A DASH = THE GIRDER DOES NOT EXIST AT THAT LOCATION." }
            : new[] { "TOP OF WEB = FINISHED GRADE - SLAB - HAUNCH, FROM THE MODEL (FINAL SHAPE; CAMBER AND BLOCKING ARE NOT INCLUDED).",
                      "SPAN POINTS ARE FRACTIONS OF THE GIRDER LENGTH BETWEEN CL BEARINGS. A DASH = THE GIRDER DOES NOT EXIST AT THAT LOCATION." };
        return Paged(ctx, title, cols, rows, notes, areaW, areaH, slab ? "Top of slab" : "Top of web");
    }
}
