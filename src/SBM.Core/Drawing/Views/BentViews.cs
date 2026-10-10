using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Bent / abutment views for one support: an elevation along the (skewed) support line looking up-station, with the
/// deck and girders cut above, pedestals and bearings, cap, columns, foundations, ground and reinforcing; and a
/// section through the cap (Section A–A) beside it. X = distance along the support line from the alignment (right
/// positive), Y = true elevation.
/// </summary>
public static class BentViews
{
    public static SheetView Bent(ViewContext ctx, SubstructureUnit unit)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var def = f.Definition;
        var sd = unit.Definition;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.BentScale);
        var sl = f.SupportLine(unit.SupportIndex);
        var sec = def.Girders.Section;
        double k = Math.Max(0.2, Math.Abs(Vec2.Dot(unit.D, a.RightNormalAt(unit.Station))));   // offset per unit S

        // ---- deck cut along the support line (with barriers) and girders cut at the bearings
        double sL = def.Deck.LeftAt(unit.Station) / k, sR = def.Deck.RightAt(unit.Station) / k;
        double Grade(double s) { var so = a.Inverse(unit.Plan(s, 0)); return a.ElevationAt(so.Station, so.Offset); }
        int nS = 40;
        var deckTop = Enumerable.Range(0, nS + 1).Select(i => sL + (sR - sL) * i / nS).Select(s => new Vec2(s, Grade(s))).ToList();
        d.CutConcrete(deckTop.Concat(Enumerable.Reverse(deckTop).Select(p => new Vec2(p.X, p.Y - def.Deck.Thickness))).ToList(), 0.06);
        var prof = BarrierProfiles.For(def.Deck.Barrier);
        if (prof != null)
            foreach (var (side, on) in new[] { (-1, def.Deck.Barrier.Left), (1, def.Deck.Barrier.Right) })
            {
                if (!on) continue;
                double edge = side < 0 ? sL : sR;
                d.CutConcrete(prof.Select(q => new Vec2(edge - side * q.X / k, (q.Y < 1e-9 ? Grade(edge - side * q.X / k) : Grade(edge) + q.Y))).ToList(), 0.06);
            }
        foreach (var br in sl.Bearings)
        {
            if (br == null) continue;
            double s = Vec2.Dot(br.TopOfSteel.XY - unit.Origin, unit.D);
            double z = br.TopOfSteel.Z - f.PlateAtBearing(br, GirderPlate.TopFlange).Thickness; // top of web
            var tf = f.PlateAtBearing(br, GirderPlate.TopFlange);
            var bf = f.PlateAtBearing(br, GirderPlate.BottomFlange);
            // Girder cut by the support plane: widths along the skewed line grow by 1/k.
            double tw = sec.WebThickness / 2 / k, ht = tf.Width / 2 / k, hb = bf.Width / 2 / k;
            d.Poly(SheetLayers.Steel, new List<Vec2>
            {
                new(s - ht, z + tf.Thickness), new(s + ht, z + tf.Thickness), new(s + ht, z), new(s + tw, z), new(s + tw, z - sec.WebDepth),
                new(s + hb, z - sec.WebDepth), new(s + hb, z - sec.WebDepth - bf.Thickness), new(s - hb, z - sec.WebDepth - bf.Thickness),
                new(s - hb, z - sec.WebDepth), new(s - tw, z - sec.WebDepth), new(s - tw, z), new(s - ht, z),
            }, true);
            // Haunch between the flange and the slab.
            d.CutConcrete(new List<Vec2> { new(s - ht, z + tf.Thickness), new(s + ht, z + tf.Thickness), new(s + ht, Grade(s + ht) - def.Deck.Thickness), new(s - ht, Grade(s - ht) - def.Deck.Thickness) }, 0.06);
            d.Text($"G{br.Girder + 1}", new Vec2(s, Grade(s) + d.Paper(0.75)), d.TextH, 0, TextAlign.Center);
        }

        // ---- pedestals and bearings
        foreach (var p in unit.Pedestals)
        {
            double s0 = p.S - p.LengthS / 2, s1 = p.S + p.LengthS / 2;
            if (p.Height >= In(0.125))
            {
                // A pedestal stands on its own half of a stepped cap; the part below the front (back-station) half's top
                // is behind that face, so it is drawn hidden.
                double b0 = unit.CapTopAt(s0, p.N), b1 = unit.CapTopAt(s1, p.N);
                double f0 = unit.CapTopAt(s0), f1 = unit.CapTopAt(s1);
                bool behind = unit.CapTopAhead.Count > 0 && p.N > unit.CapMidN && (f0 > b0 + 1e-4 || f1 > b1 + 1e-4);
                if (!behind)
                    d.Poly(SheetLayers.Concrete, new[] { new Vec2(s0, b0), new Vec2(s1, b1), new Vec2(s1, p.SeatZ), new Vec2(s0, p.SeatZ) }, true);
                else
                {
                    d.Poly(SheetLayers.ConcreteHidden, new[] { new Vec2(s0, b0), new Vec2(s1, b1), new Vec2(s1, Math.Min(p.SeatZ, f1)), new Vec2(s0, Math.Min(p.SeatZ, f0)) }, true);
                    if (p.SeatZ > Math.Max(f0, f1))
                        d.Poly(SheetLayers.Concrete, new[] { new Vec2(s0, f0), new Vec2(s1, f1), new Vec2(s1, p.SeatZ), new Vec2(s0, p.SeatZ) }, true);
                }
            }
            var br = p.Girder >= 0 && p.Girder < sl.Bearings.Length ? sl.Bearings[p.Girder] : null;   // other units' seats on a shared pier: no bearing here
            if (br != null)
            {
                double bw = def.BearingsAt(sl.Index).Footprint(def.SupportAt(sl.Index).FixityOf(p.Girder)).Width / 2;
                d.Rect(SheetLayers.Bearing, p.S - bw, p.SeatZ, p.S + bw, br.BottomOfSteelElevation);
            }
            d.Leader(new Vec2(p.S, p.SeatZ), new Vec2(p.S + d.Paper(0.25), p.SeatZ - d.Paper(0.55) - (p.Girder % 2) * d.Paper(0.25)),
                $"G{p.Girder + 1} SEAT {d.Elev(p.SeatZ)}, PED {d.Short(p.Height)}");
        }

        // ---- cap (and backwall)
        var capPoly = unit.CapOutline.Count >= 3 ? unit.CapOutline : unit.CapTop.Concat(Enumerable.Reverse(unit.CapBottom)).ToList();
        d.Poly(SheetLayers.Concrete, capPoly, true);
        if (unit.CapOutlineAhead.Count >= 3) d.Poly(SheetLayers.ConcreteHidden, unit.CapOutlineAhead, true);   // stepped ahead half
        foreach (var rl in unit.ReliefLines) d.Poly(SheetLayers.Concrete, rl);                                   // texture band lines
        if (unit.Backwall is { } bw2)
        {
            var wall = bw2.Top.Concat(Enumerable.Reverse(unit.CapTop.Where(p => p.X >= bw2.Top[0].X - 1e-9 && p.X <= bw2.Top[^1].X + 1e-9))).ToList();
            d.Poly(SheetLayers.ConcreteHidden, wall, true); // behind the cap front face and the deck
        }

        // ---- columns and foundations, ground
        double Ground(double s) => ctx.Ground?.Invoke(unit.Plan(s, (unit.CapN0 + unit.CapN1) / 2)) ?? double.NaN;
        double zLow = unit.CapBottom.Min(p => p.Y);
        foreach (var c in unit.Columns)
        {
            double h = c.SizeS / 2;
            Vertical(d, c.S - h, c.S + h, c.BottomZ, unit.CapBottomAt(c.S), Ground(c.S));
            zLow = Math.Min(zLow, c.BottomZ);
        }
        // Deep foundations are drawn with a break: the top 10 ft, then the tip elevation is noted.
        double BreakZ(double top, double tip) => top - tip > Ft(15) ? top - Ft(10) : tip;
        void BreakLine(double x0, double x1, double z)
        {
            double a = d.Paper(0.06), w = x1 - x0;
            d.Poly(SheetLayers.Concrete, new[] { new Vec2(x0 - a, z), new Vec2(x0 + w * 0.4, z), new Vec2(x0 + w * 0.47, z + a), new Vec2(x0 + w * 0.53, z - a), new Vec2(x0 + w * 0.6, z), new Vec2(x1 + a, z) });
        }
        var cutZ = new Dictionary<double, double>();
        foreach (var fd in unit.Foundations)
        {
            if (fd.SizeS > 0)
            {
                double h = fd.SizeS / 2;
                double zb = fd.Type == FoundationType.DrilledShaft ? BreakZ(fd.TopZ, fd.BottomZ) : fd.BottomZ;
                Vertical(d, fd.S - h, fd.S + h, zb, fd.TopZ, Ground(fd.S));
                if (zb > fd.BottomZ) { BreakLine(fd.S - h, fd.S + h, zb); cutZ[fd.S] = zb; }
                zLow = Math.Min(zLow, zb);
            }
            foreach (var pile in fd.Piles.Where(pp => Math.Abs(pp.N - fd.Piles.Min(q => q.N)) < 1e-6))
            {
                double zb = BreakZ(pile.TopZ, pile.TipZ);
                Vertical(d, pile.S - In(7), pile.S + In(7), zb, pile.TopZ, double.PositiveInfinity, SheetLayers.SteelHidden);
                if (zb > pile.TipZ) BreakLine(pile.S - In(7), pile.S + In(7), zb);
                zLow = Math.Min(zLow, zb);
            }
            if (fd.Piles.Count > 0) cutZ[fd.S] = BreakZ(fd.Piles[0].TopZ, fd.Piles[0].TipZ);
        }
        if (ctx.Ground != null)
        {
            var gl = new List<Vec2>();
            for (double s = unit.CapS0 - Ft(12); s <= unit.CapS1 + Ft(12); s += 0.25)
                if (!double.IsNaN(Ground(s))) gl.Add(new Vec2(s, Ground(s)));
            if (gl.Count > 1)
            {
                d.Poly(SheetLayers.Ground, gl);
                d.Leader(gl[2], gl[2] + new Vec2(-d.Paper(0.2), -d.Paper(0.4)), "EXISTING GROUND");
            }
        }

        // ---- reinforcing projected onto the elevation (front half of the cap only, to keep it readable)
        var marks = ctx.Substructure?.Marks.Where(m => m.Unit == unit.Name).ToDictionary(m => m.Mark) ?? new();
        if (ctx.Options.IncludeRebar && ctx.Substructure != null)
        {
            double nMid = (unit.CapN0 + unit.CapN1) / 2;
            var labelled = new HashSet<string>();
            var centers = unit.Columns.Select(c => c.S).Concat(unit.Foundations.Select(x => x.S)).Distinct().ToList();
            double Center(double s) => centers.Count == 0 ? s : centers.OrderBy(c => Math.Abs(c - s)).First();
            // Column / shaft verticals: only the two silhouette bars of each member.
            var bars = ctx.Substructure.Bars.Where(x => x.Unit == unit.Name).ToList();
            var silhouette = new Dictionary<double, double>();
            foreach (var b in bars.Where(x => x.Mark[0] is 'V' or 'D'))
            {
                double s = Vec2.Dot(b.Points[0].XY - unit.Origin, unit.D), c = Center(s);
                silhouette[c] = Math.Max(silhouette.GetValueOrDefault(c), Math.Abs(s - c));
            }
            foreach (var b in bars)
            {
                var pts = b.Points.Select(p => (s: Vec2.Dot(p.XY - unit.Origin, unit.D), n: Vec2.Dot(p.XY - unit.Origin, unit.N), z: p.Z)).ToList();
                bool vertical = pts.Count == 2 && Math.Abs(pts[0].s - pts[1].s) < In(0.5);
                marks.TryGetValue(b.Mark, out var mk0);
                if (b.Mark[0] is 'V' or 'D')
                {
                    double c = Center(pts[0].s);
                    if (Math.Abs(pts[0].s - c) < silhouette[c] - In(2)) continue;
                }
                // Shaft bars stop at the break line.
                if (b.Mark[0] is 'D' or 'E' && cutZ.Count > 0)
                {
                    double c = Center(pts[0].s);
                    if (cutZ.TryGetValue(c, out double zc)) pts = pts.Select(p => (p.s, p.n, Math.Max(p.z, zc))).ToList();
                }
                else if (mk0?.Shape.Code == "SPIRAL")
                {
                    // A short sample of each spiral at the top of the member; the rest is noted.
                    double zTop = pts.Max(p => p.z);
                    pts = pts.Where(p => p.z >= zTop - Ft(3)).ToList();
                    if (pts.Count < 2) continue;
                }
                else if (b.Mark[0] is 'W' or 'H') continue; // backwall bars: shown in Section A-A
                else if (pts.Average(p => p.n) > nMid + In(1) && mk0?.Shape.Code != "TIE") continue; // cap bars: front half
                d.Poly(SheetLayers.Rebar, pts.Select(p => new Vec2(p.s, p.z)));
                if (marks.TryGetValue(b.Mark, out var mk) && labelled.Add(b.Mark[..1]) && !vertical)
                {
                    var mid = pts[pts.Count / 2];
                    d.Leader(new Vec2(mid.s, mid.z), new Vec2(mid.s + d.Paper(0.4), mid.z + d.Paper(0.25) * (labelled.Count % 3 - 1)),
                        $"BARS {mk.Mark} ({mk.Size.Name})");
                }
            }
        }

        // ---- dimensions and elevations
        double zCapTop = unit.CapTop.Max(p => p.Y);
        double yDims = deckTopMax(deckTop) + (prof?.Max(q => q.Y) ?? 0) + d.Paper(0.6);
        var brgS = unit.Pedestals.Select(p => p.S).OrderBy(x => x).ToList();
        if (brgS.Count > 1)
            d.DimChain(brgS.Select(s => new Vec2(s, yDims)).ToList(), 0);
        d.Dim(new Vec2(unit.CapS0, zLow - d.Paper(0.6)), new Vec2(unit.CapS1, zLow - d.Paper(0.6)), 0, $"{d.Len(unit.CapS1 - unit.CapS0)} CAP (ALONG SKEW)");
        if (unit.Columns.Count > 1)
            d.DimChain(unit.Columns.Select(c => new Vec2(c.S, zLow - d.Paper(0.3))).ToList(), 0);
        // Top corners (not the beak tips of an as-designed cap) and, for those caps, the soffit low point.
        var topLine = unit.CapTopLine();
        d.ElevationTag(topLine[0], $"TOP OF CAP EL {d.Elev(topLine[0].Y)}", right: false);
        d.ElevationTag(topLine[^1], $"TOP OF CAP EL {d.Elev(topLine[^1].Y)}", right: true);
        var bot = unit.CapOutline.Count >= 3 ? unit.CapBottom.MinBy(p => p.Y) : unit.CapBottom[0];
        d.ElevationTag(bot, $"BOTT. OF CAP EL {d.Elev(bot.Y)}", right: false);
        foreach (var c in unit.Columns.Take(1))
            d.ElevationTag(new Vec2(c.S - c.SizeS / 2, c.BottomZ), $"TOP OF {(sd.Foundation == FoundationType.DrilledShaft ? "SHAFT" : "FOOTING")} EL {d.Elev(c.BottomZ)}", right: false);
        foreach (var fd in unit.Foundations.Take(1))
        {
            string tip = fd.Type == FoundationType.DrilledShaft ? $"{d.Short(fd.SizeS)} DIA SHAFT, {d.Len(fd.TopZ - fd.BottomZ)} LONG, TIP EL {d.Elev(fd.BottomZ)}"
                : fd.Piles.Count > 0 ? $"{fd.Piles.Count} PILES {(sd.PileType == PileType.SteelHP ? sd.PileSection : d.Short(sd.PileSize) + " SQ")}, TIP EL {d.Elev(fd.Piles[0].TipZ)}"
                : $"FOOTING {d.Len(fd.SizeS)} x {d.Len(fd.SizeN)} x {d.Short(sd.FootingThickness)}";
            double tz = cutZ.TryGetValue(fd.S, out double zc0) ? zc0 : fd.Piles.Count > 0 ? fd.Piles[0].TipZ : fd.BottomZ;
            d.Leader(new Vec2(fd.S, tz + d.Paper(0.1)), new Vec2(fd.S + d.Paper(0.5), tz - d.Paper(0.3)), tip);
        }
        if (unit.Columns.FirstOrDefault() is { } col)
            d.Leader(new Vec2(col.S + col.SizeS / 2, (col.TopZ + col.BottomZ) / 2), new Vec2(col.S + col.SizeS / 2 + d.Paper(0.5), (col.TopZ + col.BottomZ) / 2 + d.Paper(0.3)),
                col.Shape == ColumnShape.Round ? $"{d.Short(col.SizeS)} DIA COLUMN (TYP)" : $"{d.Short(col.SizeS)} x {d.Short(col.SizeN)} COLUMN (TYP)");

        // ---- CL alignment and notes
        d.Line(SheetLayers.Center, new Vec2(0, zLow - d.Paper(0.9)), new Vec2(0, yDims + d.Paper(0.5)));
        d.Text(a.Name.StartsWith("CL", StringComparison.OrdinalIgnoreCase) ? a.Name.ToUpperInvariant() : "CL " + a.Name.ToUpperInvariant(),
            new Vec2(0, yDims + d.Paper(0.65)), d.TextH, 0, TextAlign.Center);
        // Abutments are viewed from the span toward the backwall: the begin abutment looks down-station (mirrored).
        bool mirror = unit.Type == SupportType.Abutment && unit.SupportIndex == 0;
        if (mirror) dwg.MirrorX();
        string looking = unit.Type == SupportType.Abutment ? (mirror ? "Looking down-station toward the backwall" : "Looking up-station toward the backwall") : "Looking up-station";
        var box = dwg.Extents();
        string kind = unit.Type == SupportType.Pier ? "BENT" : "ABUTMENT";
        d.Title($"{unit.Name.ToUpperInvariant()} — {kind} ELEVATION", $"{looking}, dimensions along the support line (skew {UnitSystem.FormatAngleDms(Math.Abs(sl.SkewDegrees) * Math.PI / 180, false)}) · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((unit.CapS0 + unit.CapS1) / 2, box.MinY - d.Paper(0.45)));
        d.Text($"CAP TOP: {unit.CapTopDescription.ToUpperInvariant()}", new Vec2(unit.CapS0, box.MinY - d.Paper(1.2)), d.TextH, 0, TextAlign.Left);
        d.Text($"PEDESTALS: {d.Short(unit.Pedestals.Min(p => p.Height))} TO {d.Short(unit.Pedestals.Max(p => p.Height))} HIGH AT CL BEARING", new Vec2(unit.CapS0, box.MinY - d.Paper(1.4)), d.TextH, 0, TextAlign.Left);

        // ---- Section A-A through the cap (to the right)
        CapSection(d, ctx, unit, box.MaxX + d.Paper(2.0), marks);
        return new SheetView($"{unit.Name} elevation and cap section", unit.Name.Replace(" ", ""), dwg, d.Scale,
            $"{kind.ToLowerInvariant()} elevation along the support line with pedestals, columns, foundations and reinforcing; Section A–A through the cap.");
    }

    private static double deckTopMax(List<Vec2> top) => top.Max(p => p.Y);

    /// <summary>Section through the cap at a column (or the middle): X = across the cap (up-station to the right), Y = elevation.</summary>
    private static void CapSection(Drafter d, ViewContext ctx, SubstructureUnit unit, double xLeft, Dictionary<string, BarMark> marks)
    {
        var sd = unit.Definition;
        var r = sd.Reinforcement;
        double sCut = unit.Columns.Count > 0 ? unit.Columns[unit.Columns.Count / 2].S : (unit.CapS0 + unit.CapS1) / 2;
        double zt = unit.CapTopAt(sCut), zb = unit.CapBottomAt(sCut);
        double x0 = xLeft, w = unit.CapN1 - unit.CapN0;
        double X(double n) => x0 + (n - unit.CapN0);
        d.CutConcrete(new List<Vec2> { new(X(unit.CapN0), zb), new(X(unit.CapN1), zb), new(X(unit.CapN1), zt), new(X(unit.CapN0), zt) }, 0.06);
        if (unit.Backwall is { } bw)
        {
            double wt = SubstructureUnit.Interp(bw.Top, sCut);
            d.CutConcrete(new List<Vec2> { new(X(bw.N0), zt), new(X(bw.N1), zt), new(X(bw.N1), wt), new(X(bw.N0), wt) }, 0.06);
        }
        // Pedestal + bearing beyond (nearest girder).
        var ped = unit.Pedestals.OrderBy(p => Math.Abs(p.S - sCut)).FirstOrDefault();
        if (ped != null && ped.Height >= In(0.125))
            d.Rect(SheetLayers.Concrete, X(ped.N - ped.WidthN / 2), zt, X(ped.N + ped.WidthN / 2), ped.SeatZ);
        // Column below (visible).
        if (unit.Columns.FirstOrDefault(c => Math.Abs(c.S - sCut) < 1e-6) is { } col)
        {
            double h = col.SizeN / 2;
            double zEnd = zb - Math.Min(d.Paper(1.0), zb - col.BottomZ);
            d.Line(SheetLayers.Concrete, new Vec2(X(col.N - h), zb), new Vec2(X(col.N - h), zEnd));
            d.Line(SheetLayers.Concrete, new Vec2(X(col.N + h), zb), new Vec2(X(col.N + h), zEnd));
            // Break line.
            d.Poly(SheetLayers.Concrete, new[] { new Vec2(X(col.N - h) - d.Paper(0.1), zEnd), new Vec2(X(col.N), zEnd + d.Paper(0.06)), new Vec2(X(col.N), zEnd - d.Paper(0.06)), new Vec2(X(col.N + h) + d.Paper(0.1), zEnd) });
        }
        // Reinforcing in section: the nearest stirrup as drawn, longitudinal bars as dots.
        if (ctx.Options.IncludeRebar && ctx.Substructure != null && r.Enabled)
        {
            var bars = ctx.Substructure.Bars.Where(b => b.Unit == unit.Name).ToList();
            var stir = bars.Where(b => marks.TryGetValue(b.Mark, out var m) && m.Shape.Code == "TIE" && b.Mark.StartsWith('S'))
                .OrderBy(b => Math.Abs(Vec2.Dot(b.Points[0].XY - unit.Origin, unit.D) - sCut)).FirstOrDefault();
            if (stir != null)
                d.Poly(SheetLayers.Rebar, stir.Points.Select(p => new Vec2(X(Vec2.Dot(p.XY - unit.Origin, unit.N)), p.Z)));
            // Backwall bars in section (both faces): verticals as lines, horizontals as dots.
            foreach (var b in bars.Where(b => b.Mark[0] is 'W'))
            {
                double sb = Vec2.Dot(b.Points[0].XY - unit.Origin, unit.D);
                if (Math.Abs(sb - sCut) > r.BackwallBarSpacing / 2) continue;
                d.Poly(SheetLayers.Rebar, b.Points.Select(p => new Vec2(X(Vec2.Dot(p.XY - unit.Origin, unit.N)), p.Z)));
            }
            foreach (var b in bars.Where(b => b.Mark[0] is 'A' or 'B' or 'C' or 'H'))
            {
                // Longitudinal bars crossing the cut: interpolate at sCut.
                var pts = b.Points;
                for (int i = 1; i < pts.Length; i++)
                {
                    double sa = Vec2.Dot(pts[i - 1].XY - unit.Origin, unit.D), sb = Vec2.Dot(pts[i].XY - unit.Origin, unit.D);
                    if ((sa - sCut) * (sb - sCut) > 0 || Math.Abs(sb - sa) < 1e-9) continue;
                    var q = Vec3.Lerp(pts[i - 1], pts[i], (sCut - sa) / (sb - sa));
                    double rad = Math.Max(d.Paper(0.012), marks[b.Mark].Size.Diameter / 2);
                    d.Circle(SheetLayers.Rebar, new Vec2(X(Vec2.Dot(q.XY - unit.Origin, unit.N)), q.Z), rad, true);
                    break;
                }
            }
            var label = new List<string>();
            foreach (var key in new[] { "A", "B", "C", "S" })
                if (marks.Values.Where(m => m.Mark.StartsWith(key)).OrderByDescending(m => m.Count).FirstOrDefault() is { } m)
                    label.Add(key switch
                    {
                        "A" => $"BARS {m.Mark}: {r.CapTopBarCount} {m.Size.Name} TOP",
                        "B" => $"BARS {m.Mark}: {r.CapBottomBarCount} {m.Size.Name} BOTTOM",
                        "C" => $"BARS {m.Mark}: {r.CapSideBarsPerFace} {m.Size.Name} EACH FACE",
                        _ => $"BARS {m.Mark}: {m.Size.Name} STIRRUPS @ {d.Short(r.CapStirrupSpacing)} MAX",
                    });
            for (int i = 0; i < label.Count; i++)
                d.Text(label[i], new Vec2(X(unit.CapN1) + d.Paper(0.3), zt - d.Paper(0.15) - i * d.TextH * 1.6), d.TextH, 0, TextAlign.Left);
        }
        d.Dim(new Vec2(X(unit.CapN0), zb), new Vec2(X(unit.CapN1), zb), -0.35, d.Short(w));
        d.Dim(new Vec2(X(unit.CapN0), zb), new Vec2(X(unit.CapN0), zt), 0.35, d.Short(zt - zb));
        string at = Math.Abs(sCut) < In(1) ? "at the CL alignment" : $"{d.Len(Math.Abs(sCut))} {(sCut < 0 ? "left" : "right")} of the CL alignment";
        d.Title("SECTION A–A", $"Through the cap {at} · Scale {DrawingOptions.ScaleText(d.Scale, ctx.Units)}",
            new Vec2(X((unit.CapN0 + unit.CapN1) / 2), zb - d.Paper(1.6)));
    }

    /// <summary>Vertical member; below the ground line it is hidden.</summary>
    private static void Vertical(Drafter d, double x0, double x1, double z0, double z1, double ground, LayerStyle? onlyLayer = null)
    {
        double zg = double.IsNaN(ground) ? z0 : Math.Clamp(ground, z0, z1);
        var vis = onlyLayer ?? SheetLayers.Concrete;
        var hid = onlyLayer ?? SheetLayers.ConcreteHidden;
        if (z1 > zg) { d.Line(vis, new Vec2(x0, zg), new Vec2(x0, z1)); d.Line(vis, new Vec2(x1, zg), new Vec2(x1, z1)); }
        if (zg > z0) { d.Line(hid, new Vec2(x0, z0), new Vec2(x0, zg)); d.Line(hid, new Vec2(x1, z0), new Vec2(x1, zg)); d.Line(hid, new Vec2(x0, z0), new Vec2(x1, z0)); }
        else d.Line(vis, new Vec2(x0, z0), new Vec2(x1, z0));
    }
}
