using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Typical transverse section of the superstructure at a station, looking up-station: X = offset (right positive),
/// Y = true elevation. Slab, haunches and barriers are cut (hatched); girders cut at the station; the next cross frame
/// up-station is shown beyond; deck reinforcing, dimensions and cross slopes are annotated.
/// </summary>
public static class SectionViews
{
    public static SheetView TypicalSection(ViewContext ctx)
    {
        var f = ctx.Framing;
        var a = f.Alignment;
        var def = f.Definition;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        double sta = !double.IsNaN(ctx.Options.TypicalSectionStation) ? ctx.Options.TypicalSectionStation
            : (f.Supports[0].Station + f.Supports[1].Station) / 2;
        var deck = def.Deck;
        double l = deck.LeftAt(sta), r = deck.RightAt(sta), t = deck.Thickness;
        double Top(double o) => deck.FinishedElevation(a, sta, o);

        // ---- slab (cut)
        var xs = new List<double> { l, r };
        double piv = a.CrossSlope.PivotOffset;
        if (piv > l && piv < r) xs.Add(piv);
        if (deck.EdgeStrip is { LowSideLevel: true, Width: > 0 } strip)
        {
            if (l + strip.Width < r) xs.Add(l + strip.Width);
            if (r - strip.Width > l) xs.Add(r - strip.Width);
        }
        xs.Sort();
        var slabTop = xs.Select(o => new Vec2(o, Top(o))).ToList();
        var slab = slabTop.Concat(Enumerable.Reverse(slabTop).Select(p => new Vec2(p.X, p.Y - t))).ToList();
        d.CutConcrete(slab, 0.06);

        // ---- girders, haunches
        var sec = def.Girders.Section;
        var tf = sec.SegmentAt(GirderPlate.TopFlange, sta);
        var bf = sec.SegmentAt(GirderPlate.BottomFlange, sta);
        // Prestressed: Tx girder outline, "top of web" = top of beam.
        bool ps = def.Girders.IsPrestressed;
        var shape = Prestressed.TxGirderCatalog.Get(def.Girders.Prestressed);
        double topW = ps ? shape.TopWidth : tf.Width, topT = ps ? 0 : tf.Thickness;
        double below = ps ? shape.Depth : sec.WebDepth + bf.Thickness;
        var gx = new List<(GirderResult g, double o, double z)>();
        foreach (var g in f.Girders)
        {
            if (ViewGeometry.GirderAtStation(g, a, sta) is not { } p) continue;
            double o = a.Inverse(p.XY).Offset;
            gx.Add((g, o, p.Z));
            double z = p.Z;
            // Haunch: flange width, from top of flange to the slab bottom (cut).
            double h0 = o - topW / 2, h1 = o + topW / 2;
            var haunch = new List<Vec2> { new(h0, z + topT), new(h1, z + topT), new(h1, Top(h1) - t), new(h0, Top(h0) - t) };
            d.CutConcrete(haunch, 0.06);
            if (ps)
            {
                // Beam at this station (straight between supports) and its strands at their midspan positions.
                double zb = z - shape.Depth;
                if (ctx.Prestressed?.Beams.FirstOrDefault(b => b.Girder == g.Index && b.Span == ViewGeometry.SpanAt(f, sta)) is { } bm)
                {
                    var outline = shape.Outline().Select(q => new Vec2(o + q.X, zb + q.Y)).ToList();
                    d.CutConcrete(outline, 0.1);
                    foreach (var st in bm.Strands)
                        d.Circle(SheetLayers.Rebar, new Vec2(o + st.Section.X, zb + st.Section.Y), Math.Max(bm.StrandDiameter / 2, d.Paper(0.012)), filled: true);
                }
                else d.CutConcrete(shape.Outline().Select(q => new Vec2(o + q.X, zb + q.Y)).ToList(), 0.1);
                d.Text(g.Name, new Vec2(o, zb - d.Paper(0.25)), d.TextH, 0, TextAlign.Center);
                continue;
            }
            // Plate girder cut (I shape).
            double tw = sec.WebThickness / 2;
            var ishape = new List<Vec2>
            {
                new(o - tf.Width / 2, z + tf.Thickness), new(o + tf.Width / 2, z + tf.Thickness), new(o + tf.Width / 2, z), new(o + tw, z),
                new(o + tw, z - sec.WebDepth), new(o + bf.Width / 2, z - sec.WebDepth), new(o + bf.Width / 2, z - sec.WebDepth - bf.Thickness),
                new(o - bf.Width / 2, z - sec.WebDepth - bf.Thickness), new(o - bf.Width / 2, z - sec.WebDepth), new(o - tw, z - sec.WebDepth),
                new(o - tw, z), new(o - tf.Width / 2, z),
            };
            d.Poly(SheetLayers.Steel, ishape, true);
            d.Text(g.Name, new Vec2(o, z - sec.WebDepth - bf.Thickness - d.Paper(0.25)), d.TextH, 0, TextAlign.Center);
        }

        // ---- cross frames beyond (the first intermediate frame up-station in each bay)
        var cfDef = def.CrossFrames;
        for (int bay = 0; bay + 1 < gx.Count; bay++)
        {
            var cf = f.CrossFrames.Where(c => c.Bay == bay && !c.AtSupport && c.Station >= sta).OrderBy(c => c.Station).FirstOrDefault()
                     ?? f.CrossFrames.Where(c => c.Bay == bay && !c.AtSupport).OrderBy(c => Math.Abs(c.Station - sta)).FirstOrDefault();
            if (cf == null) continue;
            var (g1, o1, z1) = gx[bay];
            var (g2, o2, z2) = gx[bay + 1];
            double cw = In(3.5); // connection plate width beyond the web (work point near the plate edge)
            var A = new Vec2(o1 + cw, z1 - cfDef.TopWorkPointOffset);
            var B = new Vec2(o1 + cw, z1 - sec.WebDepth + cfDef.BottomWorkPointOffset);
            var C = new Vec2(o2 - cw, z2 - cfDef.TopWorkPointOffset);
            var D = new Vec2(o2 - cw, z2 - sec.WebDepth + cfDef.BottomWorkPointOffset);
            var lyr = SheetLayers.SteelHidden;
            // The frame as detailed (angles, gussets, bolts), mapped from its plane into the section: x scales between the
            // two webs, z follows the line between the two tops of web.
            if (ctx.Steel?.CrossFrameDetails.FirstOrDefault(x => x.Mark == cf.Mark) is { } det)
            {
                Vec2 Map(Vec2 q)
                {
                    double t = q.X / det.Span;
                    double zRef = det.LeftWebTop + (det.RightWebTop - det.LeftWebTop) * t;
                    return new Vec2(o1 + (o2 - o1) * t, z1 + (z2 - z1) * t + (q.Y - zRef));
                }
                foreach (var (_, outline) in det.Gussets) d.Poly(SheetLayers.Steel, outline.Select(Map), true);
                foreach (var m in det.Members) d.Poly(m.Front ? SheetLayers.Steel : SheetLayers.SteelHidden, m.Outline.Select(Map), true);
                if (det.Filler != null) d.Poly(SheetLayers.Steel, det.Filler.Select(Map), true);
                foreach (var b in det.Bolts) d.Circle(SheetLayers.Steel, Map(b), def.Bolts.Diameter / 2);
                // Leader to the top chord / diaphragm; as-designed styles may have neither (e.g. a strut or WT chord only).
                int li = det.Members.FindIndex(m => m.Code is "TC" or "DIA");
                if (li < 0) li = det.Members.FindIndex(m => m.Outline.Length > 2);
                if (bay == 0 && li >= 0)
                    d.Leader(Map(det.Members[li].Outline[2]), Map(det.Members[li].Outline[2]) + new Vec2(d.Paper(0.3), d.Paper(0.9)),
                        $"{ResolvedName(cf.Type)} {cf.Mark} (BEYOND), {cfDef.ChordAngle} CHORDS, {cfDef.DiagonalAngle} DIAGONALS");
            }
            else
            switch (cf.Type)
            {
                case ResolvedCrossFrameType.XFrame:
                    d.Line(lyr, A, C); d.Line(lyr, B, D); d.Line(lyr, A, D); d.Line(lyr, B, C);
                    break;
                case ResolvedCrossFrameType.KFrame:
                {
                    var m = Vec2.Lerp(B, D, 0.5);
                    d.Line(lyr, A, C); d.Line(lyr, B, D); d.Line(lyr, A, m); d.Line(lyr, C, m);
                    break;
                }
                case ResolvedCrossFrameType.InvertedKFrame:
                {
                    var m = Vec2.Lerp(A, C, 0.5);
                    d.Line(lyr, A, C); d.Line(lyr, B, D); d.Line(lyr, B, m); d.Line(lyr, D, m);
                    break;
                }
                default:
                    d.Rect(lyr, A.X, B.Y, C.X, A.Y);
                    break;
            }
            // Connection plates (seen beyond) on each web.
            d.Line(SheetLayers.Steel, new Vec2(o1 + sec.WebThickness / 2 + def.Stiffeners.ConnectionPlate.Width, z1), new Vec2(o1 + sec.WebThickness / 2 + def.Stiffeners.ConnectionPlate.Width, z1 - sec.WebDepth));
            d.Line(SheetLayers.Steel, new Vec2(o2 - sec.WebThickness / 2 - def.Stiffeners.ConnectionPlate.Width, z2), new Vec2(o2 - sec.WebThickness / 2 - def.Stiffeners.ConnectionPlate.Width, z2 - sec.WebDepth));
            if (bay == 0 && ctx.Steel == null)
                d.Leader(Vec2.Lerp(A, D, 0.3), Vec2.Lerp(A, D, 0.3) + new Vec2(-d.Paper(0.3), -d.Paper(0.9)),
                    $"{ResolvedName(cf.Type)} (BEYOND), {cfDef.ChordAngle} CHORDS, {cfDef.DiagonalAngle} DIAGONALS");
        }

        // ---- barriers (cut)
        var prof = BarrierProfiles.For(deck.Barrier);
        if (prof != null)
            foreach (var (side, on) in new[] { (-1, deck.Barrier.Left), (1, deck.Barrier.Right) })
            {
                if (!on) continue;
                double edge = side < 0 ? l : r;
                double zEdge = Top(edge);
                var poly = prof.Select(q => new Vec2(edge - side * q.X, q.Y < 1e-9 ? Top(edge - side * q.X) : zEdge + q.Y)).ToList();
                d.CutConcrete(poly, 0.06);
                if (side < 0)
                    d.Leader(poly[3], poly[3] + new Vec2(-d.Paper(0.4), d.Paper(0.35)),
                        deck.Barrier.Type == BarrierType.TxDotSstr ? "TYPE SSTR RAIL (RL-SSTR)" : $"{ChoiceName(deck.Barrier.Type)} BARRIER");
            }

        // ---- reinforcing (transverse bars as lines, longitudinal as dots)
        var rf = deck.Reinforcement;
        if (rf.Enabled && ctx.Options.IncludeRebar)
        {
            var tt = RebarCatalog.Size(rf.TopTransverseSize);
            var bt = RebarCatalog.Size(rf.BottomTransverseSize);
            var tl = RebarCatalog.Size(rf.TopLongitudinalSize);
            var bl = RebarCatalog.Size(rf.BottomLongitudinalSize);
            double x0 = l + rf.SideCover, x1 = r - rf.SideCover;
            var xr = xs.Where(x => x > x0 && x < x1).Prepend(x0).Append(x1).ToList();
            var topBar = xr.Select(o => new Vec2(o, Top(o) - rf.TopCover - tt.Diameter / 2)).ToList();
            var botBar = xr.Select(o => new Vec2(o, Top(o) - t + rf.BottomCover + bt.Diameter / 2)).ToList();
            d.Poly(SheetLayers.Rebar, topBar);
            d.Poly(SheetLayers.Rebar, botBar);
            double dot = Math.Max(d.Paper(0.01), tl.Diameter / 2);
            for (double o = x0 + rf.TopLongitudinalSpacing / 2; o < x1; o += rf.TopLongitudinalSpacing)
                d.Circle(SheetLayers.Rebar, new Vec2(o, Top(o) - rf.TopCover - tt.Diameter - tl.Diameter / 2), dot, true);
            for (double o = x0 + rf.BottomLongitudinalSpacing / 2; o < x1; o += rf.BottomLongitudinalSpacing)
                d.Circle(SheetLayers.Rebar, new Vec2(o, Top(o) - t + rf.BottomCover + bt.Diameter + bl.Diameter / 2), dot, true);
            double lx = gx.Count > 1 ? (gx[^2].o + gx[^1].o) / 2 : (x0 + x1) / 2;
            string S(double v) => u.FormatLength(v, LengthKind.Short);
            d.Leader(new Vec2(lx, Top(lx) - rf.TopCover), new Vec2(lx + d.Paper(0.5), Top(lx) + d.Paper(0.55)),
                $"{rf.TopTransverseSize} @ {S(rf.TopTransverseSpacing)} TOP TRANSV., {rf.TopLongitudinalSize} @ {S(rf.TopLongitudinalSpacing)} LONGIT. ({S(rf.TopCover)} CLR)");
            d.Leader(new Vec2(lx + d.Paper(0.2), Top(lx) - t + rf.BottomCover), new Vec2(lx + d.Paper(0.7), Top(lx) - t - d.Paper(1.1)),
                $"{rf.BottomTransverseSize} @ {S(rf.BottomTransverseSpacing)} BOTT. TRANSV., {rf.BottomLongitudinalSize} @ {S(rf.BottomLongitudinalSpacing)} LONGIT. ({S(rf.BottomCover)} CLR)");
        }

        // ---- dimensions
        double zTopMax = slabTop.Max(p => p.Y) + (prof != null ? prof.Max(q => q.Y) : 0);
        double yDim = zTopMax + d.Paper(0.35);
        var chain = new List<Vec2> { new(l, yDim) };
        chain.AddRange(gx.Select(x => new Vec2(x.o, yDim)));
        chain.Add(new Vec2(r, yDim));
        d.DimChain(chain, 0);
        d.Dim(new Vec2(l, yDim + d.Paper(0.35)), new Vec2(r, yDim + d.Paper(0.35)), 0, $"{u.FormatLength(r - l)} OUT TO OUT OF DECK");
        double zBotMin = gx.Count > 0 ? gx.Min(x => x.z) - below : slab.Min(p => p.Y);
        if (gx.Count > 1)
        {
            var gchain = gx.Select(x => new Vec2(x.o, zBotMin - d.Paper(0.55))).ToList();
            d.DimChain(gchain, 0);
        }
        // Slab thickness and haunch at the first interior bay.
        if (gx.Count > 1)
        {
            double xo = (gx[0].o + gx[1].o) / 2 + d.Paper(0.6);
            d.Dim(new Vec2(xo, Top(xo) - t), new Vec2(xo, Top(xo)), -0.25, u.FormatLength(t, LengthKind.Short));
            var (g0, o0, z0) = gx[1];
            d.Leader(new Vec2(o0 - topW / 2, z0 + deck.Haunch / 2), new Vec2(o0 - topW / 2 - d.Paper(0.5), z0 - d.Paper(0.35)),
                ps ? $"{u.FormatLength(deck.Haunch, LengthKind.Short)} HAUNCH AT SUPPORTS\n(TOP OF BEAM TO BOTT. OF SLAB)"
                   : $"{u.FormatLength(deck.Haunch, LengthKind.Short)} HAUNCH AT CL GIRDER (TOP OF WEB TO BOTT. OF SLAB)");
            if (ps)
            {
                var pd = def.Girders.Prestressed;
                d.Leader(new Vec2(o0 + shape.WebWidth / 2, z0 - shape.Depth / 2), new Vec2(o0 + d.Paper(0.5), zBotMin - d.Paper(1.0)),
                    $"TxDOT {shape.Name} (IGD)\n{pd.StrandCount} - {Prestressed.TxGirderCatalog.StrandSize(pd.StrandDiameter, u)} STRANDS ({pd.DepressedStrands} DEPRESSED)");
            }
            else
            d.Leader(new Vec2(o0 + sec.WebThickness / 2, z0 - sec.WebDepth / 2), new Vec2(o0 + d.Paper(0.6), z0 - sec.WebDepth / 2 - d.Paper(0.2)),
                $"WEB {u.FormatLength(sec.WebDepth, LengthKind.Short)} x {u.FormatLength(sec.WebThickness, LengthKind.Short)}");
        }

        // ---- CL / PGL, cross slopes
        double zc = Top(0);
        d.Line(SheetLayers.Center, new Vec2(0, zBotMin - d.Paper(1.0)), new Vec2(0, zTopMax + d.Paper(1.1)));
        d.Text(a.Name.StartsWith("CL", StringComparison.OrdinalIgnoreCase) ? a.Name.ToUpperInvariant() : "CL " + a.Name.ToUpperInvariant(),
            new Vec2(0, zTopMax + d.Paper(1.25)), d.TextH, 0, TextAlign.Center);
        if (Math.Abs(piv) > 1e-6)
            d.Line(SheetLayers.Grade, new Vec2(piv, zBotMin - d.Paper(0.3)), new Vec2(piv, zTopMax + d.Paper(0.8)));
        var (sl, sr) = a.CrossSlope.SlopesAt(sta);
        CrossSlopeArrow(d, (l + Math.Min(piv, 0)) / 2 + (l - 0) * 0.15, Top, sl, true);
        CrossSlopeArrow(d, (r + Math.Max(piv, 0)) / 2 - (r - 0) * 0.15, Top, sr, false);
        d.ElevationTag(new Vec2(piv, Top(piv)), $"PGL EL {d.Elev(Top(piv))}");

        var box = dwg.Extents();
        d.Title($"TYPICAL SECTION AT STA {u.FormatStation(sta)}", $"Looking up-station · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((l + r) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView("Typical section", "Typical Section", dwg, d.Scale,
            $"Transverse section at STA {u.FormatStation(sta)} (slab, haunches, girders, cross frame beyond, rail, reinforcing).");
    }

    private static void CrossSlopeArrow(Drafter d, double x, Func<double, double> top, double slope, bool leftSide)
    {
        double half = d.Paper(0.45);
        double z = top(x) + d.Paper(0.35);
        // Arrow points down-slope along the deck.
        var p0 = new Vec2(x - half, z + (top(x - half) - top(x)));
        var p1 = new Vec2(x + half, z + (top(x + half) - top(x)));
        bool downRight = top(x + half) < top(x - half);
        var tip = downRight ? p1 : p0;
        var tail = downRight ? p0 : p1;
        d.Line(SheetLayers.Text, tail, tip);
        var dir = (tip - tail).Normalized();
        var back = tip - dir * d.Paper(0.08);
        d.Poly(SheetLayers.Text, new[] { tip, back + dir.Left * d.Paper(0.025), back - dir.Left * d.Paper(0.025) }, true);
        d.Text($"{Math.Abs(slope) * 100:0.0##}%", new Vec2(x, z + d.Paper(0.12)), d.TextH, Math.Atan2(dir.Y, dir.X), TextAlign.Center);
    }

    private static string ResolvedName(ResolvedCrossFrameType t) => t switch
    {
        ResolvedCrossFrameType.XFrame => "X-FRAME",
        ResolvedCrossFrameType.KFrame => "K-FRAME",
        ResolvedCrossFrameType.InvertedKFrame => "INVERTED K-FRAME",
        _ => "DIAPHRAGM",
    };

    private static string ChoiceName(BarrierType t) => t switch { BarrierType.FShape => "F-SHAPE", BarrierType.SingleSlope => "SINGLE SLOPE", _ => t.ToString().ToUpperInvariant() };
}

/// <summary>Geometry lookups shared by the views.</summary>
public static class ViewGeometry
{
    /// <summary>Span index containing a station (clamped to the first / last span).</summary>
    public static int SpanAt(BridgeFraming f, double station)
    {
        for (int j = 0; j + 1 < f.Supports.Count; j++)
            if (station <= f.Supports[j + 1].Station) return j;
        return Math.Max(0, f.Supports.Count - 2);
    }

    /// <summary>Top of web at the girder web centerline where the girder crosses a station (bisection on the path).</summary>
    public static Vec3? GirderAtStation(GirderResult g, Alignments.Alignment a, double station)
    {
        var path = g.Path;
        if (path.Count < 2) return null;
        double lo = Math.Min(path.Station[0], path.Station[^1]), hi = Math.Max(path.Station[0], path.Station[^1]);
        if (station < lo || station > hi) return null;
        double s0 = 0, s1 = path.Length;
        bool inc = path.Station[^1] >= path.Station[0];
        for (int it = 0; it < 60; it++)
        {
            double sm = (s0 + s1) / 2;
            if ((path.At(sm).station < station) == inc) s0 = sm; else s1 = sm;
        }
        return path.At((s0 + s1) / 2).point;
    }
}
