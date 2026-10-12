using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Bridge layout views. <see cref="Layout"/> is a developed (straightened) plan with an elevation section cut along the
/// CL alignment directly below it, so stations line up (X = station, plan Y = −offset, elevation Y = true elevation).
/// <see cref="TruePlan"/> is the same plan in true state-plane coordinates.
/// </summary>
public static class LayoutViews
{
    public static SheetView Layout(ViewContext ctx)
    {
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, ctx.Units, ctx.Options.LayoutScale);
        var f = ctx.Framing;
        var a = f.Alignment;
        var (staBegin, staEnd) = DeckEndStations(f);

        // Elevation first (true elevations), then the developed plan above it.
        var elevBox = DrawElevation(d, ctx, staBegin, staEnd);
        // Plan Y = base − offset: the right deck edge (lowest in the plan) stays above the elevation.
        double planBase = elevBox.MaxY + d.Paper(0.55) + Math.Max(0, f.Definition.Deck.MaxRight) + d.Paper(0.6);
        Vec2 Map(Vec2 p)
        {
            var so = a.Inverse(p);
            return new Vec2(so.Station, planBase - so.Offset);
        }
        var planBox = DrawPlan(d, ctx, Map, developed: true, staBegin, staEnd);
        d.Title("PLAN (DEVELOPED ALONG CL ALIGNMENT)", "Stations align with the elevation below", new Vec2((staBegin + staEnd) / 2, planBox.MaxY + d.Paper(0.7)));
        d.Title("ELEVATION — SECTION ALONG CL ALIGNMENT (LOOKING LEFT)", $"Scale {DrawingOptions.ScaleText(d.Scale, ctx.Units)}",
            new Vec2((staBegin + staEnd) / 2, elevBox.MinY - d.Paper(0.5)));
        return new SheetView("Bridge layout (plan + elevation)", "Layout", dwg, d.Scale,
            "Developed plan over an elevation section along the CL alignment; X = station, elevation true.");
    }

    public static SheetView TruePlan(ViewContext ctx)
    {
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, ctx.Units, ctx.Options.LayoutScale);
        var (b, e) = DeckEndStations(ctx.Framing);
        var box = DrawPlan(d, ctx, p => p, developed: false, b, e);
        // North arrow at the upper left.
        var na = new Vec2(box.MinX - d.Paper(0.6), box.MaxY);
        double h = d.Paper(0.5);
        d.Poly(SheetLayers.Title, new[] { na + new Vec2(0, h), na + new Vec2(-h * 0.3, -h * 0.4), na + new Vec2(0, -h * 0.2), na + new Vec2(h * 0.3, -h * 0.4) }, true);
        d.Text("N", na + new Vec2(0, h + d.TextH), d.TextH * 1.3, 0, TextAlign.Center, SheetLayers.Title);
        d.Title("PLAN", $"True state-plane coordinates · Scale {DrawingOptions.ScaleText(d.Scale, ctx.Units)}", new Vec2(box.Center.X, box.MinY - d.Paper(0.5)));
        return new SheetView("Plan (true coordinates)", "Plan", dwg, d.Scale, "Bridge plan in true coordinates for referencing with roadway and survey files.");
    }

    /// <summary>Stations where the begin and end deck lines cross the alignment.</summary>
    public static (double begin, double end) DeckEndStations(BridgeFraming f)
    {
        var a = f.Alignment;
        double Cross(Vec2 p, Vec2 dir)
        {
            for (int i = 0; i < 4; i++)
            {
                var so = a.Inverse(p);
                double k = Vec2.Dot(dir, a.RightNormalAt(so.Station));
                if (Math.Abs(k) < 1e-6) break;
                p -= dir * (so.Offset / k);
            }
            return a.Inverse(p).Station;
        }
        if (f.Deck == null) return (f.Supports[0].Station, f.Supports[^1].Station);
        return (Cross(f.Deck.BeginLinePoint, f.Deck.BeginLineDirection), Cross(f.Deck.EndLinePoint, f.Deck.EndLineDirection));
    }

    // ================================================================== plan

    private static List<Vec2> Densify(IReadOnlyList<Vec2> pts, double step)
    {
        var res = new List<Vec2>();
        for (int i = 0; i < pts.Count; i++)
        {
            if (i > 0)
            {
                double len = Vec2.Distance(pts[i - 1], pts[i]);
                int n = (int)Math.Ceiling(len / step);
                for (int k = 1; k < n; k++) res.Add(Vec2.Lerp(pts[i - 1], pts[i], (double)k / n));
            }
            res.Add(pts[i]);
        }
        return res;
    }

    private static Box2 DrawPlan(Drafter d, ViewContext ctx, Func<Vec2, Vec2> map, bool developed, double staBegin, double staEnd)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var def = f.Definition;
        var u = ctx.Units;
        double step = developed ? 1.0 : 1e9;
        IEnumerable<Vec2> M(IEnumerable<Vec2> pts) => (developed ? Densify(pts.ToList(), step) : pts.ToList()).Select(map);
        double Rot(double sta) => developed ? 0 : a.AngleAt(sta);
        var box = Box2.Empty;
        void Track(IEnumerable<Vec2> pts) { foreach (var p in pts) box.Add(p); }

        // Deck outline and barriers.
        if (f.Deck != null)
        {
            var outline = M(f.Deck.Outline.Select(p => p.XY).Append(f.Deck.Outline[0].XY)).ToList();
            d.Poly(SheetLayers.Concrete, outline);
            Track(outline);
            foreach (var bl in f.Deck.BarrierLines) d.Poly(SheetLayers.Barrier, M(bl.Select(p => p.XY)));
        }

        // Girders (hidden under the deck) with labels at the begin end; at small scales only girders far enough from the last
        // labelled one (and always the first / last) are named, so the names never overprint.
        Vec2? lastLabel = null;
        for (int gi = 0; gi < f.Girders.Count; gi++)
        {
            var g = f.Girders[gi];
            d.Poly(SheetLayers.SteelHidden, M(g.WebLine.Select(p => p.XY)));
            var p0 = map(g.WebLine[0].XY);
            bool room = lastLabel is not { } q || Vec2.Distance(q, p0) > d.SmallTextH * 1.45;
            bool lastOne = gi == f.Girders.Count - 1;
            if (!room && !lastOne) continue;
            if (!room && lastOne) continue;
            d.Text(g.Name, p0 + new Vec2(-d.Paper(0.12), 0) * (developed ? 1 : 0), d.SmallTextH, Rot(staBegin), TextAlign.Right);
            lastLabel = p0;
        }

        // Alignment with station ticks.
        double unit = u.LongUnit;
        double s0 = Math.Max(a.StartStation, staBegin - 50 * unit), s1 = Math.Min(a.EndStation, staEnd + 50 * unit);
        var cl = new List<Vec2>();
        for (double s = s0; s <= s1 + 1e-9; s += Math.Max(0.5, (s1 - s0) / 400)) cl.Add(a.PointAt(s));
        d.Poly(SheetLayers.Center, M(cl));
        double minor = (u.IsUS ? 50 : 20) * unit, major = (u.IsUS ? 100 : 100) * unit;
        for (double sta = Math.Ceiling(s0 / minor) * minor; sta <= s1; sta += minor)
        {
            var p = a.PointAt(sta);
            var n = a.RightNormalAt(sta);
            double len = d.Paper(Math.Abs(Math.IEEERemainder(sta, major)) < 1e-6 ? 0.12 : 0.06);
            d.Line(SheetLayers.Center, map(p - n * len), map(p + n * len));
            if (Math.Abs(Math.IEEERemainder(sta, major)) < 1e-6)
                d.Text(u.FormatStation(sta, 0), map(p + n * (len + d.TextH)), d.SmallTextH, Rot(sta) + Math.PI / 2, TextAlign.Center);
        }
        var aLabelSta = s0 + d.Paper(0.3);
        d.Text(a.Name.StartsWith("CL", StringComparison.OrdinalIgnoreCase) ? a.Name : $"CL {a.Name}", map(a.PointAt(aLabelSta) - a.RightNormalAt(aLabelSta) * d.Paper(0.08)), d.TextH, Rot(aLabelSta), TextAlign.Left);

        // Support lines with labels.
        foreach (var sl in f.Supports)
        {
            double k = Math.Abs(Vec2.Dot(sl.Direction, a.RightNormalAt(sl.Station)));
            double sl0 = (def.Deck.MinLeft - d.Paper(0.5)) / Math.Max(k, 0.2), sl1 = (def.Deck.MaxRight + d.Paper(0.5)) / Math.Max(k, 0.2);
            var line = new[] { sl.AlignmentPoint + sl.Direction * sl0, sl.AlignmentPoint + sl.Direction * sl1 };
            var ml = M(line).ToList();
            d.Poly(SheetLayers.Center, ml);
            Track(ml);
            var top = ml[0];
            var dir = (ml[0] - ml[1]).Normalized();
            d.TextAlong($"CL BRG {sl.Name.ToUpperInvariant()}", top + dir * d.Paper(0.06), dir, d.SmallTextH);
            // Station and skew, horizontal, under the bottom end of the line.
            var bot = ml[^1] + new Vec2(0, -d.Paper(0.12));
            d.Text($"STA {u.FormatStation(sl.Station)}", bot, d.SmallTextH, 0, TextAlign.Center);
            if (sl.SkewDegrees != 0)
                d.Text($"SKEW {UnitSystem.FormatAngleDms(Math.Abs(sl.SkewDegrees) * Math.PI / 180, false)} {(sl.SkewDegrees > 0 ? "RT" : "LT")}",
                    bot + new Vec2(0, -d.SmallTextH * 1.5), d.SmallTextH, 0, TextAlign.Center);
            Track(new[] { bot + new Vec2(0, -d.SmallTextH * 2.5) });
        }

        // Substructure (hidden below the deck).
        if (ctx.Substructure != null)
            foreach (var unit2 in ctx.Substructure.Units)
            {
                Vec2 W(double s, double n) => unit2.Plan(s, n);
                d.Poly(SheetLayers.ConcreteHidden, M(new[] { W(unit2.CapS0, unit2.CapN0), W(unit2.CapS1, unit2.CapN0), W(unit2.CapS1, unit2.CapN1), W(unit2.CapS0, unit2.CapN1), W(unit2.CapS0, unit2.CapN0) }));
                foreach (var c in unit2.Columns)
                    d.Poly(SheetLayers.ConcreteHidden, M(SubstructurePlanBuilder.Outline(unit2, c.S, c.N, c.SizeS, c.SizeN, c.Shape == ColumnShape.Round, 0).Select(p => p.XY)), true);
                foreach (var fd in unit2.Foundations)
                {
                    if (fd.SizeS > 0)
                        d.Poly(SheetLayers.ConcreteHidden, M(SubstructurePlanBuilder.Outline(unit2, fd.S, fd.N, fd.SizeS, fd.SizeN, fd.Type == FoundationType.DrilledShaft, 0).Select(p => p.XY)), true);
                    foreach (var pile in fd.Piles)
                    {
                        d.Poly(SheetLayers.ConcreteHidden, M(SubstructurePlanBuilder.Outline(unit2, pile.S, pile.N, In(14), In(14), false, 0).Select(p => p.XY)), true);
                        if (pile.IsBattered)   // the tip runs out in the batter direction
                            d.Poly(SheetLayers.ConcreteHidden, M(new[] { unit2.Plan(pile.S, pile.N), unit2.Plan(pile.TipS, pile.TipN) }));
                    }
                }
            }

        // Span dimensions above the left deck edge, overall length above them.
        double dimOff = def.Deck.MinLeft - d.Paper(0.9);
        var spanPts = f.Supports.Select(sl => map(a.PointAt(sl.Station, dimOff))).ToList();
        for (int i = 1; i < spanPts.Count; i++)
            d.Dim(spanPts[i - 1], spanPts[i], 0, $"SPAN {i} = {u.FormatLength(f.Supports[i].Station - f.Supports[i - 1].Station)}");
        var o0 = map(a.PointAt(staBegin, dimOff - d.Paper(0.4)));
        var o1 = map(a.PointAt(staEnd, dimOff - d.Paper(0.4)));
        d.Dim(o0, o1, 0, $"{u.FormatLength(staEnd - staBegin)} BRIDGE LENGTH (ALONG CL, END OF DECK TO END OF DECK)");
        Track(new[] { o0, o1 });
        d.Text($"BEGIN BRIDGE  STA {u.FormatStation(staBegin)}", map(a.PointAt(staBegin, def.Deck.MaxRight + d.Paper(0.35))), d.SmallTextH, Rot(staBegin), TextAlign.Center);
        d.Text($"END BRIDGE  STA {u.FormatStation(staEnd)}", map(a.PointAt(staEnd, def.Deck.MaxRight + d.Paper(0.35))), d.SmallTextH, Rot(staEnd), TextAlign.Center);
        Track(new[] { map(a.PointAt(staBegin, def.Deck.MaxRight + d.Paper(0.5))) });
        return box;
    }

    // ================================================================== elevation

    private static Box2 DrawElevation(Drafter d, ViewContext ctx, double staBegin, double staEnd)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var def = f.Definition;
        var u = ctx.Units;
        var box = Box2.Empty;
        void Track(Vec2 p) => box.Add(p);

        // ---- deck slab cut at the CL (hatched)
        int n = Math.Max(2, (int)Math.Ceiling((staEnd - staBegin) / 0.5));
        var top = new List<Vec2>();
        for (int i = 0; i <= n; i++)
        {
            double s = staBegin + (staEnd - staBegin) * i / n;
            top.Add(new Vec2(s, a.ElevationAt(s, 0)));
        }
        var slab = top.Concat(Enumerable.Reverse(top).Select(p => new Vec2(p.X, p.Y - def.Deck.Thickness))).ToList();
        d.CutConcrete(slab, 0.05);
        foreach (var p in top) Track(p);

        // ---- barrier beyond (left edge)
        var (_, _, bh) = BarrierProfiles.PlanLines(def.Deck.Barrier);
        if (bh > 0 && def.Deck.Barrier.Left)
        {
            double eo = def.Deck.MinLeft;
            var bt = new List<Vec2>();
            for (int i = 0; i <= n; i++)
            {
                double s = staBegin + (staEnd - staBegin) * i / n;
                bt.Add(new Vec2(s, def.Deck.FinishedElevation(a, s, eo) + bh));
            }
            d.Poly(SheetLayers.Barrier, bt);
            d.Line(SheetLayers.Barrier, bt[0], new Vec2(bt[0].X, top[0].Y));
            d.Line(SheetLayers.Barrier, bt[^1], new Vec2(bt[^1].X, top[^1].Y));
            foreach (var p in bt) Track(p);
            d.Leader(bt[n / 3], bt[n / 3] + new Vec2(d.Paper(0.3), d.Paper(0.35)),
                def.Deck.Barrier.Type == BarrierType.TxDotSstr ? "TYPE SSTR RAIL (BEYOND)" : "BARRIER (BEYOND)");
        }

        // ---- girder beyond: the nearest girder at or left of the CL
        string? spliceNote = null;
        var g = NearestLeftGirder(f);
        var sec = def.Girders.Section;
        if (g != null)
        {
            var path = g.Path;
            var tf = new List<Vec2>(); var tfb = new List<Vec2>(); var bfT = new List<Vec2>(); var bfB = new List<Vec2>();
            for (int i = 0; i < path.Count; i++)
            {
                double sta = path.Station[i], z = path.WebTop[i].Z;
                double t1 = sec.SegmentAt(GirderPlate.TopFlange, sta).Thickness, b1 = sec.SegmentAt(GirderPlate.BottomFlange, sta).Thickness;
                tf.Add(new Vec2(sta, z + t1)); tfb.Add(new Vec2(sta, z));
                bfT.Add(new Vec2(sta, z - sec.WebDepth)); bfB.Add(new Vec2(sta, z - sec.WebDepth - b1));
            }
            foreach (var pl in new[] { tf, tfb, bfT, bfB }) d.Poly(SheetLayers.Steel, pl);
            d.Line(SheetLayers.Steel, tf[0], bfB[0]);
            d.Line(SheetLayers.Steel, tf[^1], bfB[^1]);
            foreach (var p in bfB) Track(p);
            // Stiffeners and connection plates on this girder.
            foreach (var st in f.Stiffeners.Where(s => s.Girder == g.Index))
            {
                var (pt, _, sta) = path.At(st.GirderDistance);
                double z = pt.Z;
                d.Line(SheetLayers.Steel, new Vec2(sta, z), new Vec2(sta, z - sec.WebDepth + (st.Kind == StiffenerKind.Intermediate ? def.Stiffeners.IntermediateBottomGap : 0)));
            }
            // Field splices: numbered tags under the girder (FS1, FS2, ...) and one note with their stations (no leader per splice).
            int spIx = 0;
            var spNotes = new List<string>();
            foreach (var sp in f.Splices.Where(s => s.Girder == g.Index).OrderBy(s => s.GirderDistance))
            {
                var (pt, _, sta) = path.At(sp.GirderDistance);
                var sd = def.SpliceDetail;
                double half = (sd.WebBoltColumns - 1) * def.Bolts.Pitch + def.Bolts.EdgeDistance + def.FieldSpliceGap / 2;
                d.Rect(SheetLayers.Steel, sta - half, pt.Z - sec.WebDepth + sd.WebPlateFlangeClearance, sta + half, pt.Z - sd.WebPlateFlangeClearance);
                string tag = $"FS{++spIx}";
                d.Text(tag, new Vec2(sta, pt.Z - sec.WebDepth - d.Paper(0.12)), d.SmallTextH * 0.85, Math.PI / 2, TextAlign.Right);
                spNotes.Add($"{tag} STA {u.FormatStation(sp.Location.XY == default ? sta : a.Inverse(sp.Location.XY).Station)}");
            }
            if (spNotes.Count > 0) spliceNote = $"FIELD SPLICES ({g.Name}): " + string.Join(", ", spNotes);
            d.Leader(new Vec2(tf[tf.Count / 4].X, (tf[tf.Count / 4].Y + bfB[bfB.Count / 4].Y) / 2),
                new Vec2(tf[tf.Count / 4].X + d.Paper(0.4), bfB[bfB.Count / 4].Y - d.Paper(0.45)), $"{g.Name} (BEYOND)");
        }

        // ---- supports: CL lines, bearings at the shown girder, substructure
        double zMin = box.MinY;
        double labelTop = double.MinValue, geoTop = box.MaxY;
        foreach (var sl in f.Supports)
        {
            var unit = ctx.Substructure?.Units.FirstOrDefault(x => x.SupportIndex == sl.Index);
            if (g != null && sl.Bearings[g.Index] is { } br)
            {
                var seat = ctx.Steel?.BearingSeats.FirstOrDefault(x => x.Girder == g.Index && Math.Abs(x.Station - br.Station) < 1e-6);
                double hw = def.BearingsAt(sl.Index).Footprint(def.SupportAt(sl.Index).FixityOf(g.Index)).Length / 2;
                if (seat != null)
                {
                    d.Rect(SheetLayers.Bearing, br.Station - hw, seat.SeatElevation, br.Station + hw, br.BottomOfSteelElevation);
                    // Seat elevations are tabulated on the bent and bearing sheets.
                    var ped = unit?.Pedestals.FirstOrDefault(p => p.Girder == g.Index);
                    if (ped != null && ped.Height > In(0.125))
                        d.Rect(SheetLayers.Concrete, br.Station - hw - unit!.Definition.PedestalProjection, ped.CapTopZ, br.Station + hw + unit.Definition.PedestalProjection, ped.SeatZ);
                }
            }
            if (unit != null) DrawUnitInElevation(d, ctx, unit, ref zMin);
            double zTop = a.ElevationAt(sl.Station, 0) + d.Paper(0.9);
            double zBot = zMin - d.Paper(0.2);
            d.Line(SheetLayers.Center, new Vec2(sl.Station, zBot), new Vec2(sl.Station, zTop));
            // Label above the deck, reading upward along the CL line.
            string lbl = $"CL BRG {sl.Name.ToUpperInvariant()}";
            d.Text(lbl, new Vec2(sl.Station - d.Paper(0.06), zTop + d.Paper(0.05)), d.SmallTextH, Math.PI / 2, TextAlign.Left);
            labelTop = Math.Max(labelTop, zTop + d.Paper(0.05) + d.SmallTextH * 0.65 * lbl.Length);
            geoTop = Math.Max(geoTop, zTop);
            Track(new Vec2(sl.Station, zBot));
        }
        zMin = Math.Min(zMin, box.MinY);

        // ---- existing ground along the CL
        if (ctx.Ground != null)
        {
            double unitL = u.LongUnit;
            var gl = new List<Vec2>();
            for (double s = staBegin - 30 * unitL; s <= staEnd + 30 * unitL; s += 0.5)
                if (ctx.Ground(a.PointAt(s)) is { } z) gl.Add(new Vec2(s, z));
            if (gl.Count > 1)
            {
                d.Poly(SheetLayers.Ground, gl);
                var gp = gl[gl.Count / 5];
                d.Leader(gp, gp + new Vec2(-d.Paper(0.3), -d.Paper(0.4)), "EXISTING GROUND AT CL");
                foreach (var p in gl) Track(p);
            }
        }

        // ---- elevation grid at the left and right ends (label step chosen so the numbers never overprint at small scales)
        double step = u.IsUS ? Ft(10) : 2;
        foreach (double st in u.IsUS ? new[] { Ft(10), Ft(20), Ft(50), Ft(100) } : new[] { 2.0, 5, 10, 20, 50 })
        {
            step = st;
            if (st >= d.SmallTextH * 1.6) break;
        }
        // Grid over the structure and ground only (labels above it are placed afterwards).
        double gz0 = Math.Floor(box.MinY / step) * step, gz1 = Math.Ceiling(Math.Max(box.MaxY, geoTop) / step) * step;
        double xl = box.MinX - d.Paper(0.6), xr = box.MaxX + d.Paper(0.6);
        d.Line(SheetLayers.Grid, new Vec2(xl, gz0), new Vec2(xl, gz1));
        d.Line(SheetLayers.Grid, new Vec2(xr, gz0), new Vec2(xr, gz1));
        for (double z = gz0; z <= gz1 + 1e-9; z += step)
        {
            d.Line(SheetLayers.Grid, new Vec2(xl - d.Paper(0.06), z), new Vec2(xl, z));
            d.Line(SheetLayers.Grid, new Vec2(xr, z), new Vec2(xr + d.Paper(0.06), z));
            d.Text(u.FormatLength(z, LengthKind.Coordinate).Split('.')[0], new Vec2(xl - d.Paper(0.1), z), d.SmallTextH, 0, TextAlign.Right, SheetLayers.Grid);
            d.Text(u.FormatLength(z, LengthKind.Coordinate).Split('.')[0], new Vec2(xr + d.Paper(0.1), z), d.SmallTextH, 0, TextAlign.Left, SheetLayers.Grid);
        }
        box.Add(new Vec2(xl - d.Paper(0.6), gz0));
        box.Add(new Vec2(xr + d.Paper(0.6), Math.Max(gz1, labelTop)));

        // ---- field splice stations (tags FS1.. on the girder), wrapped under the elevation
        if (spliceNote != null)
        {
            double width = Math.Max(4, (staEnd - staBegin) / d.Paper(1));
            var lines = PrestressedViews.Wrap(spliceNote, width, 0.075);
            double y0 = box.MinY - d.Paper(0.15);
            for (int i = 0; i < lines.Count; i++)
                d.Text(lines[i], new Vec2(staBegin, y0 - i * d.SmallTextH * 1.55), d.SmallTextH, 0, TextAlign.Left, SheetLayers.Notes);
            box.Add(new Vec2(box.MinX, y0 - lines.Count * d.SmallTextH * 1.55));
        }

        // ---- grade labels
        d.Leader(new Vec2(top[n / 6].X, top[n / 6].Y), new Vec2(top[n / 6].X - d.Paper(0.25), top[n / 6].Y + d.Paper(0.45)),
            $"TOP OF DECK AT CL ({u.FormatLength(def.Deck.Thickness, LengthKind.Short)} SLAB, CUT)");
        return box;
    }

    private static GirderResult? NearestLeftGirder(BridgeFraming f)
    {
        var a = f.Alignment;
        double mid = (f.Supports[0].Station + f.Supports[^1].Station) / 2;
        GirderResult? best = null;
        double bestOff = double.NegativeInfinity;
        foreach (var g in f.Girders)
        {
            int i = g.Path.Count / 2;
            double off = a.Inverse(g.Path.WebTop[i].XY).Offset;
            if (off <= 1e-3 && off > bestOff) { best = g; bestOff = off; }
        }
        return best ?? f.Girders.FirstOrDefault();
    }

    /// <summary>Cap (cut at the CL), backwall, and the nearest column / foundation left of the CL, projected to stations.</summary>
    private static void DrawUnitInElevation(Drafter d, ViewContext ctx, SubstructureUnit unit, ref double zMin)
    {
        var a = ctx.Framing.Alignment;
        double sCut = 0; // the support line crosses the alignment at S = 0
        double St(double s, double nn) => a.Inverse(unit.Plan(s, nn)).Station;
        double zt = unit.CapTopAt(sCut), zb = unit.CapBottomAt(sCut);
        double x0 = St(sCut, unit.CapN0), x1 = St(sCut, unit.CapN1);
        var cap = new List<Vec2> { new(x0, zb), new(x1, zb), new(x1, zt), new(x0, zt) };
        d.CutConcrete(cap, 0.05);
        if (unit.Backwall is { } bw)
        {
            double wt = SubstructureUnit.Interp(bw.Top, sCut);
            double b0 = St(sCut, bw.N0), b1 = St(sCut, bw.N1);
            d.CutConcrete(new List<Vec2> { new(b0, zt), new(b1, zt), new(b1, wt), new(b0, wt) }, 0.05);
        }
        zMin = Math.Min(zMin, zb);
        // Nearest column (or foundation under an abutment) on the left (S < 0), else the nearest overall.
        var col = unit.Columns.Where(c => c.S <= 0).OrderByDescending(c => c.S).FirstOrDefault() ?? unit.Columns.OrderBy(c => Math.Abs(c.S)).FirstOrDefault();
        double ground = double.NaN;
        if (col != null)
        {
            double xc = St(col.S, col.N);
            double h = col.SizeN / 2;
            ground = ctx.Ground?.Invoke(unit.Plan(col.S, col.N)) ?? double.NaN;
            double zTopCol = unit.CapBottomAt(col.S);
            DrawVertical(d, xc - h, xc + h, col.BottomZ, zTopCol, ground, SheetLayers.Concrete, SheetLayers.ConcreteHidden);
            zMin = Math.Min(zMin, col.BottomZ);
        }
        var fnd = col != null ? unit.Foundations.FirstOrDefault(x => Math.Abs(x.S - col.S) < 1e-6)
            : unit.Foundations.Where(x => x.S <= 0).OrderByDescending(x => x.S).FirstOrDefault() ?? unit.Foundations.FirstOrDefault();
        if (fnd != null)
        {
            double xc = St(fnd.S, fnd.N);
            if (double.IsNaN(ground)) ground = fnd.GroundZ;
            if (fnd.SizeN > 0)
                DrawVertical(d, xc - fnd.SizeN / 2, xc + fnd.SizeN / 2, fnd.BottomZ, fnd.TopZ, ground, SheetLayers.Concrete, SheetLayers.ConcreteHidden);
            zMin = Math.Min(zMin, fnd.BottomZ);
            foreach (var pile in fnd.Piles.Where(p => Math.Abs(p.S - fnd.Piles.Min(q => q.S)) < 1e-6))
            {
                double xp = St(pile.S, pile.N);
                if (!pile.IsBattered) DrawVertical(d, xp - In(7), xp + In(7), pile.TipZ, pile.TopZ, ground, SheetLayers.SteelHidden, SheetLayers.SteelHidden);
                else
                {
                    double xt = St(pile.TipS, pile.TipN);   // sloped in the developed elevation
                    d.Line(SheetLayers.SteelHidden, new Vec2(xp - In(7), pile.TopZ), new Vec2(xt - In(7), pile.TipZ));
                    d.Line(SheetLayers.SteelHidden, new Vec2(xp + In(7), pile.TopZ), new Vec2(xt + In(7), pile.TipZ));
                    d.Line(SheetLayers.SteelHidden, new Vec2(xt - In(7), pile.TipZ), new Vec2(xt + In(7), pile.TipZ));
                }
                zMin = Math.Min(zMin, pile.TipZ);
            }
            string what = fnd.Type switch
            {
                FoundationType.DrilledShaft => $"{d.Short(fnd.SizeS)} DIA DRILLED SHAFT, TIP EL {d.Elev(fnd.BottomZ)}",
                FoundationType.PileCap when fnd.Piles.Count > 0 => $"PILES, {PierViews.TipText(fnd.Piles, d).Replace(", ", "\n")}",
                FoundationType.SpreadFooting => $"SPREAD FOOTING, BOTTOM EL {d.Elev(fnd.BottomZ)}",
                _ => "",
            };
            if (what.Length > 0)
                d.Leader(new Vec2(xc, fnd.BottomZ + d.Paper(0.15)), new Vec2(xc + d.Paper(0.4), fnd.BottomZ - d.Paper(0.25)), what);
        }
        // Cap top tag on the side away from the bearing tag (left), a little below so they never collide.
        // Cap elevations are on the bent sheets.
    }

    /// <summary>Vertical member outline; the part below the ground line is hidden.</summary>
    private static void DrawVertical(Drafter d, double x0, double x1, double z0, double z1, double ground, LayerStyle visible, LayerStyle hidden)
    {
        if (double.IsNaN(ground) || ground >= z1) ground = double.IsNaN(ground) ? z0 : Math.Min(ground, z1);
        double zg = Math.Clamp(ground, z0, z1);
        if (z1 > zg)
        {
            d.Line(visible, new Vec2(x0, zg), new Vec2(x0, z1));
            d.Line(visible, new Vec2(x1, zg), new Vec2(x1, z1));
        }
        if (zg > z0)
        {
            d.Line(hidden, new Vec2(x0, z0), new Vec2(x0, zg));
            d.Line(hidden, new Vec2(x1, z0), new Vec2(x1, zg));
            d.Line(hidden, new Vec2(x0, z0), new Vec2(x1, z0));
        }
        else d.Line(visible, new Vec2(x0, z0), new Vec2(x1, z0));
    }
}
