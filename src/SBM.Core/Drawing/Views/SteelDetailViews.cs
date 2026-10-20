using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Steel details: a typical intermediate and support cross frame drawn in the frame plane exactly as detailed in the 3D
/// model (members as trimmed, gussets, bolts, work points), stiffener details (section and web elevation for bearing
/// stiffeners, connection plates and intermediate stiffeners), and general steel notes.
/// </summary>
public static class SteelDetailViews
{
    public static SheetView CrossFrame(ViewContext ctx, bool support)
    {
        var f = ctx.Framing;
        var def = f.Definition;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var all = ctx.Steel?.CrossFrameDetails.Where(c => c.AtSupport == support).ToList() ?? new();
        // Typical: the most common type, middle of the list (an interior bay away from ends).
        var common = all.GroupBy(c => c.Type).OrderByDescending(g => g.Count()).FirstOrDefault();
        var cf = common?.ElementAt(common.Count() / 2);
        string title = support ? "SUPPORT CROSS FRAME" : "INTERMEDIATE CROSS FRAME";
        if (cf == null)
        {
            d.Text($"{title}: NONE IN THIS BRIDGE", new Vec2(0, 0), d.TitleH, 0, TextAlign.Center);
            return new SheetView(title, "CF", dwg, d.Scale, "");
        }
        var sec = def.Girders.Section;
        double wd = sec.WebDepth, tw = sec.WebThickness;
        var tf = sec.TopFlange;
        var bf = sec.BottomFlange;

        // Girders (sections in the frame plane).
        foreach (var (x, z) in new[] { (0.0, cf.LeftWebTop), (cf.Span, cf.RightWebTop) })
        {
            d.Poly(SheetLayers.Steel, new List<Vec2>
            {
                new(x - tf.Width / 2, z + tf.Thickness), new(x + tf.Width / 2, z + tf.Thickness), new(x + tf.Width / 2, z), new(x + tw / 2, z),
                new(x + tw / 2, z - wd), new(x + bf.Width / 2, z - wd), new(x + bf.Width / 2, z - wd - bf.Thickness), new(x - bf.Width / 2, z - wd - bf.Thickness),
                new(x - bf.Width / 2, z - wd), new(x - tw / 2, z - wd), new(x - tw / 2, z), new(x - tf.Width / 2, z),
            }, true);
            d.Line(SheetLayers.Center, new Vec2(x, z + tf.Thickness + d.Paper(0.3)), new Vec2(x, z - wd - bf.Thickness - d.Paper(0.3)));
        }
        // Connection plates / bearing stiffeners (full depth).
        double clip = def.Stiffeners.CornerClip;
        foreach (var (x0, x1, z) in new[] { (cf.LeftWebFace, cf.LeftWebFace + cf.PlateWidth, cf.LeftWebTop), (cf.RightWebFace - cf.PlateWidth, cf.RightWebFace, cf.RightWebTop) })
            d.Rect(SheetLayers.Steel, x0, z - wd + (x0 > cf.Span / 2 ? 0 : 0), x1, z);
        // Gussets, members, filler, bolts, work points.
        foreach (var (_, outline) in cf.Gussets) d.Poly(SheetLayers.Steel, outline, true);
        foreach (var m in cf.Members)
        {
            d.Poly(m.Front ? SheetLayers.Steel : SheetLayers.SteelHidden, m.Outline, true);
            d.Line(SheetLayers.SteelHidden, m.HeelA, m.HeelB);
        }
        if (cf.Filler != null) d.Poly(SheetLayers.Steel, cf.Filler, true);
        double bd = cf.BoltDiameter > 0 ? cf.BoltDiameter : def.Bolts.Diameter;
        foreach (var b in cf.Bolts) d.Circle(SheetLayers.Steel, b, bd / 2);
        // Work lines (WP to WP) and work point symbols.
        foreach (var (_, w1, w2) in cf.WorkLines) d.Line(SheetLayers.Center, w1, w2);
        var wps = cf.WorkLines.SelectMany(w => new[] { w.Wp1, w.Wp2 }).Concat(cf.WorkPoints)
            .Aggregate(new List<Vec2>(), (l, q) => { if (!l.Any(o => Vec2.Distance(o, q) < 1e-4)) l.Add(q); return l; });
        foreach (var wp in wps)
        {
            double r = d.Paper(0.035);
            d.Circle(SheetLayers.Text, wp, r);
            d.Line(SheetLayers.Text, wp + new Vec2(-r * 1.6, 0), wp + new Vec2(r * 1.6, 0));
            d.Line(SheetLayers.Text, wp + new Vec2(0, -r * 1.6), wp + new Vec2(0, r * 1.6));
        }

        // Labels.
        var a = cf.WorkPoints[0];
        var b0 = cf.WorkPoints[1];
        var c = cf.WorkPoints[2];
        var dd = cf.WorkPoints[3];
        var labelled = new HashSet<string>();
        foreach (var m in cf.Members)
        {
            if (!labelled.Add(m.Code)) continue;
            var mid = Vec2.Lerp(Vec2.Lerp(m.Outline[0], m.Outline[2], 0.5), Vec2.Lerp(m.Outline[1], m.Outline[3], 0.5), 0.5);
            mid = Vec2.Lerp(m.Outline[0], m.Outline[1], 0.6) * 0.5 + Vec2.Lerp(m.Outline[3], m.Outline[2], 0.6) * 0.5;
            string role = m.Code switch { "TC" => "TOP CHORD", "BC" => "BOTTOM CHORD", "DIA" => "DIAPHRAGM", _ => "DIAGONAL" };
            var off = m.Code switch
            {
                "TC" => new Vec2(0.3, 0.55), "BC" => new Vec2(-0.7, -0.6), "D1" => new Vec2(1.5, -0.25), "D2" => new Vec2(0.9, 0.25), _ => new Vec2(0.3, 0.55),
            };
            d.Leader(mid, mid + new Vec2(d.Paper(off.X), d.Paper(off.Y)), $"{m.Angle} {role} ({m.Code})");
        }
        if (cf.Gussets.FirstOrDefault() is { Outline: var go })
        {
            var gp = go[0];
            d.Leader(gp, gp + new Vec2(-d.Paper(0.4), d.Paper(0.65)), $"PL {d.Short(cf.GussetThickness)} GUSSET (TYP)");
        }
        // Bolt notes come from the detailed bolt pattern of THIS frame (clusters of the recorded bolt positions).
        double boltD = cf.BoltDiameter > 0 ? cf.BoltDiameter : bd;
        string grade = ctx.Steel?.BoltGrade ?? def.Bolts.Grade;
        var seenNotes = new HashSet<string>();
        foreach (var grp in BoltGroups(cf.Bolts))
        {
            var cols = grp.Select(q => Math.Round(q.X / In(1.0 / 16))).Distinct().Count();
            var ys = grp.Select(q => q.Y).OrderBy(y => y).ToList();
            double pitchData = 0;
            for (int i = 1; i < ys.Count; i++) if (ys[i] - ys[i - 1] > 1e-4) { pitchData = pitchData == 0 ? ys[i] - ys[i - 1] : Math.Min(pitchData, ys[i] - ys[i - 1]); }
            bool upper = grp.Average(q => q.Y) > (cf.WorkPoints[0].Y + cf.WorkPoints[1].Y) / 2;
            string note = $"{(upper ? "TOP" : "BOTTOM")}: {grp.Count} - {d.Short(boltD)} DIA {grade} BOLTS" +
                (cols > 1 ? $" ({Math.Max(1, grp.Count / cols)} ROWS x {cols} COL.)" : "") + (pitchData > 0 ? $", {d.Short(pitchData)} PITCH" : "");
            if (!seenNotes.Add(note)) continue;
            var bp = grp.OrderByDescending(q => q.Y).First();
            d.Leader(bp, bp + new Vec2(-d.Paper(0.5), d.Paper(upper ? 0.5 : -0.75)), note);
            if (seenNotes.Count >= 4) break;
        }
        var plate = support ? def.Stiffeners.BearingStiffener : def.Stiffeners.ConnectionPlate;
        d.Leader(new Vec2(cf.RightWebFace - cf.PlateWidth / 2, cf.RightWebTop - wd * 0.15), new Vec2(cf.RightWebFace - cf.PlateWidth / 2 + d.Paper(0.6), cf.RightWebTop + d.Paper(0.6)),
            support ? $"BEARING STIFFENER PL {d.Short(plate.Width)} x {d.Short(plate.Thickness)}" : $"CONNECTION PL {d.Short(plate.Width)} x {d.Short(plate.Thickness)}");
        if (cf.Members.Count > 0 && cf.ChannelName.Length == 0)
        {
            var dm = cf.Members.FirstOrDefault(m => m.Code is not "TC" and not "BC" and not "DIA");
            var m0 = (dm.Outline ?? cf.Members[0].Outline)[1];
            d.Leader(m0, m0 + new Vec2(d.Paper(0.7), -d.Paper(1.3)), $"{d.Short(cf.WeldSize)} FILLET, HEEL AND TOE, {d.Short(cf.WeldLap)} LAP (TYP)");
        }

        // Dimensions: girder spacing, horizontal work point locations (from each web CL and between WPs), vertical work
        // point locations at both girders (below top of web, WP to WP, above top of bottom flange).
        double zTop = Math.Max(cf.LeftWebTop, cf.RightWebTop) + tf.Thickness;
        double zBot = Math.Min(cf.LeftWebTop, cf.RightWebTop) - wd - bf.Thickness;
        d.Dim(new Vec2(0, zTop + d.Paper(0.6)), new Vec2(cf.Span, zTop + d.Paper(0.6)), 0, $"{d.Len(cf.Span)} CL TO CL GIRDERS (ALONG FRAME)");
        var xs = new List<double> { 0 };
        xs.AddRange(wps.Select(q => q.X).Where(x => x > 1e-4 && x < cf.Span - 1e-4).Distinct().OrderBy(x => x)
            .Aggregate(new List<double>(), (l, x) => { if (l.Count == 0 || x - l[^1] > d.Paper(0.02)) l.Add(x); return l; }));
        xs.Add(cf.Span);
        d.DimChain(xs.Select(x => new Vec2(x, zBot - d.Paper(0.35))).ToList(), 0, i => d.Short(xs[i + 1] - xs[i]));
        foreach (var (x, webTop, sideOff) in new[] { (a.X, cf.LeftWebTop, -0.55), (c.X, cf.RightWebTop, 0.55) })
        {
            var col = wps.Where(q => Math.Abs(q.X - x) < 1e-4).Select(q => q.Y).OrderByDescending(y => y).ToList();
            var zs = new List<double> { webTop };
            zs.AddRange(col);
            zs.Add(webTop - wd);
            var pts = zs.Select(z => new Vec2(sideOff < 0 ? 0 : cf.Span, z)).ToList();
            for (int i = 1; i < pts.Count; i++)
            {
                string lbl = i == 1 ? $"{d.Short(zs[0] - zs[1])}" : i == pts.Count - 1 ? $"{d.Short(zs[i - 1] - zs[i])}" : $"{d.Short(zs[i - 1] - zs[i])} WP";
                d.Dim(pts[i - 1], pts[i], sideOff < 0 ? -0.9 : 0.9, lbl);
            }
        }
        d.Text("TOP OF WEB", new Vec2(-tf.Width / 2 - d.Paper(0.1), cf.LeftWebTop), d.SmallTextH, 0, TextAlign.Right);
        d.Text("TOP OF BOTT. FL.", new Vec2(-bf.Width / 2 - d.Paper(0.1), cf.LeftWebTop - wd), d.SmallTextH, 0, TextAlign.Right);
        d.Text("WP = WORK POINT; WORK LINES SHOWN AS CENTER LINES", new Vec2(cf.Span / 2, zBot - d.Paper(0.7)), d.SmallTextH, 0, TextAlign.Center);

        var box = dwg.Extents();
        string type = cf.Type switch { ResolvedCrossFrameType.XFrame => "X-FRAME", ResolvedCrossFrameType.KFrame => "K-FRAME", ResolvedCrossFrameType.InvertedKFrame => "INVERTED K-FRAME", _ => "DIAPHRAGM" };
        d.Title($"{title} — {type} ({cf.Mark} SHOWN)", support ? "Plumb, in the plane of the support line" : "Normal to the flanges; dimensions in the plane of the frame",
            new Vec2(cf.Span / 2, box.MinY - d.Paper(0.3)));
        return new SheetView(title, support ? "CF Support" : "CF Intermediate", dwg, d.Scale, $"{title.ToLowerInvariant()} as detailed ({cf.Mark}).");
    }

    /// <summary>Connected clusters of bolt positions (neighbor distance within 1.6 x the largest nearest-neighbor spacing).</summary>
    public static List<List<Vec2>> BoltGroups(IReadOnlyList<Vec2> bolts)
    {
        var groups = new List<List<Vec2>>();
        if (bolts.Count == 0) return groups;
        double maxNn = 0;
        foreach (var b in bolts)
        {
            double nn = double.MaxValue;
            foreach (var o in bolts) if (o.X != b.X || o.Y != b.Y) nn = Math.Min(nn, Vec2.Distance(b, o));
            if (nn < double.MaxValue) maxNn = Math.Max(maxNn, nn);
        }
        double link = maxNn > 0 ? maxNn * 1.6 : 1;
        var left = bolts.ToList();
        while (left.Count > 0)
        {
            var g = new List<Vec2> { left[^1] };
            left.RemoveAt(left.Count - 1);
            for (int i = 0; i < g.Count; i++)
                for (int k = left.Count - 1; k >= 0; k--)
                    if (Vec2.Distance(g[i], left[k]) <= link) { g.Add(left[k]); left.RemoveAt(k); }
            groups.Add(g);
        }
        return groups;
    }

    /// <summary>
    /// Stiffener details as sections looking along the girder, with the web broken (top and bottom 10" shown) so the
    /// clips, fillet welds, weld hold backs at the clips and the hold back from the tension flange read at a large scale.
    /// </summary>
    public static SheetView Stiffeners(ViewContext ctx)
    {
        var f = ctx.Framing;
        var def = f.Definition;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var sec = def.Girders.Section;
        var st = def.Stiffeners;
        double wd = sec.WebDepth, tw = sec.WebThickness, clip = st.CornerClip, ws = st.WeldSize;
        // weld termination as generated (SteelModelGenerator stiffeners): hold-back past the clip / from the plate edges, web weld end below the flanges
        double hb = WeldTermination.HoldBack(def.Welds, ws), webEnd = clip + hb;
        string webEndNote = $"(CLIP + {d.Short(hb)})";
        if (def.Welds is { StiffenerWebEndFactor: > 0 } or { StiffenerWebEndMin: > 0 })
        {
            double x5 = Math.Max(def.Welds!.StiffenerWebEndFactor * tw, def.Welds.StiffenerWebEndMin);
            if (st.FlangeToWebWeld + x5 > webEnd) { webEnd = st.FlangeToWebWeld + x5; webEndNote = $"(X = {def.Welds.StiffenerWebEndFactor:0.#}T, MIN {d.Short(def.Welds.StiffenerWebEndMin)} = {d.Short(x5)} PAST F-W WELD TOE)"; }
        }
        var tf = sec.TopFlange;
        var bf = sec.BottomFlange;
        double ht = In(10);                                        // shown at the top and at the bottom of the web
        double cut = Math.Max(0, wd - 2 * ht);
        double Z(double z) => z >= -ht ? z : z + cut;              // break: the middle of the web is omitted
        Vec2 P(double x, double z) => new(x, Z(z));
        double x = 0;
        double gapX = Math.Max(bf.Width, tf.Width) + d.Paper(2.6);
        double zMid = -ht;

        void Break(double x0, double x1)
        {
            double a = d.Paper(0.05);
            d.Poly(SheetLayers.Steel, new[] { new Vec2(x0, zMid), new Vec2((x0 + x1) / 2 - a, zMid), new Vec2((x0 + x1) / 2, zMid + a * 1.5), new Vec2((x0 + x1) / 2, zMid - a * 1.5), new Vec2((x0 + x1) / 2 + a, zMid), new Vec2(x1, zMid) });
        }

        void Bead(List<Vec2> poly)
        {
            d.Poly(SheetLayers.Steel, poly, true);
            d.Hatch(poly, 0.02, 45);
        }

        void Detail(string name, PlateSize p, bool both, double bottomGap, string[] notes)
        {
            double cx = x;
            // Girder: flanges and the broken web.
            d.Rect(SheetLayers.Steel, cx - tf.Width / 2, 0, cx + tf.Width / 2, tf.Thickness);
            d.Rect(SheetLayers.Steel, cx - bf.Width / 2, Z(-wd) - bf.Thickness, cx + bf.Width / 2, Z(-wd));
            foreach (double sx in new[] { -tw / 2, tw / 2 })
            {
                d.Line(SheetLayers.Steel, new Vec2(cx + sx, 0), new Vec2(cx + sx, zMid + d.Paper(0.05)));
                d.Line(SheetLayers.Steel, new Vec2(cx + sx, zMid - d.Paper(0.05)), new Vec2(cx + sx, Z(-wd)));
            }
            foreach (int side in both ? new[] { -1, 1 } : new[] { 1 })
            {
                double x0 = cx + side * tw / 2, x1 = cx + side * (tw / 2 + p.Width);
                double zb = -wd + bottomGap;
                bool full = bottomGap <= 0;
                // Plate with clips at the web-flange corners (split at the break).
                var plate = new List<Vec2>
                {
                    P(x0 + side * clip, 0), P(x1, 0), P(x1, zb), full ? P(x0 + side * clip, zb) : P(x0, zb), full ? P(x0, zb + clip) : P(x0, zb), P(x0, -clip),
                };
                d.Poly(SheetLayers.Steel, plate, true);
                Break(Math.Min(x0, x1) - d.Paper(0.05), Math.Max(x0, x1) + d.Paper(0.05));
                // Fillet weld to the web, held back from the flange-to-web welds by the clips (and stopped at the plate
                // end for a cut-short stiffener).
                // Weld termination (Steel.WeldTermination, as generated): web weld ends webEnd below the flanges, flange welds hb short of the clip and the
                // outer plate edge, a cut-short stiffener's web weld hb short of its end; the beads end square.
                double webBottom = full ? zb + webEnd : zb + hb;
                Bead(new List<Vec2> { P(x0, -webEnd), P(x0 + side * ws, -webEnd), P(x0 + side * ws, -ht + In(0.5)), P(x0, -ht + In(0.5)) });
                Bead(new List<Vec2> { P(x0, -wd + ht - In(0.5)), P(x0 + side * ws, -wd + ht - In(0.5)), P(x0 + side * ws, webBottom), P(x0, webBottom) });
                // Fillet welds to the flanges outside the clips, held back from the clip and the plate edge.
                double fa = clip + hb, fb = p.Width - hb;
                if (fb > fa)
                {
                    Bead(new List<Vec2> { P(x0 + side * fa, 0), P(x0 + side * fb, 0), P(x0 + side * fb, -ws), P(x0 + side * fa, -ws) });
                    if (full) Bead(new List<Vec2> { P(x0 + side * fa, -wd), P(x0 + side * fb, -wd), P(x0 + side * fb, -wd + ws), P(x0 + side * fa, -wd + ws) });
                }
            }
            double xr = cx + tw / 2, xp = cx + tw / 2 + p.Width;
            d.Dim(new Vec2(xr, Z(-wd * 0.5) + In(4)), new Vec2(xp, Z(-wd * 0.5) + In(4)), 0.2, d.Short(p.Width));
            // Clip at the top (horizontal leg), weld hold back at the clip, hold back from the tension flange.
            d.Dim(new Vec2(xr, 0), new Vec2(xr + clip, 0), 0.35, d.Short(clip));
            d.Dim(new Vec2(xp, 0), new Vec2(xp, -clip), -0.3, d.Short(clip));
            d.Leader(new Vec2(xr + clip * 0.5, -clip * 0.5), new Vec2(xp + d.Paper(0.55), -clip - d.Paper(0.2)),
                $"{d.Short(clip)} CLIP (TYP)\nWELDS HELD BACK {d.Short(hb)}");
            // hold-back of the flange weld at the outer plate edge (as modeled)
            d.Dim(new Vec2(xp - hb, 0), new Vec2(xp, 0), 0.35, d.Short(hb));
            if (bottomGap > 0)
            {
                d.Dim(new Vec2(xp, Z(-wd + bottomGap)), new Vec2(xp, Z(-wd)), -0.3, d.Short(bottomGap));
                d.Leader(new Vec2(xr + p.Width * 0.5, Z(-wd + bottomGap * 0.5)), new Vec2(xp + d.Paper(0.55), Z(-wd) + d.Paper(0.25)),
                    $"{d.Short(bottomGap)} HOLD BACK FROM\nTENSION FLANGE\n(4tw–6tw = {d.Short(4 * tw)}–{d.Short(6 * tw)})");
            }
            else
            {
                d.Dim(new Vec2(xp, Z(-wd + clip)), new Vec2(xp, Z(-wd)), -0.3, d.Short(clip));
                d.Leader(new Vec2(xr + clip * 0.5, Z(-wd + clip * 0.5)), new Vec2(xp + d.Paper(0.55), Z(-wd) - d.Paper(0.05)), $"{d.Short(clip)} CLIP\nWELDS HELD BACK {d.Short(hb)}");
            }
            d.WeldSymbol(new Vec2(xr + ws, Z(-ht * 0.6)), new Vec2(xp + d.Paper(0.6), Z(-ht * 0.6) - d.Paper(0.35)), d.Short(ws), bothSides: true, tail: "TO WEB");
            d.WeldSymbol(new Vec2(xp - p.Width * 0.25, -ws), new Vec2(xp + d.Paper(0.6), tf.Thickness + d.Paper(0.35)), d.Short(ws), bothSides: true,
                tail: bottomGap > 0 ? "TO TOP FLANGE" : "TO BOTH FLANGES");
            d.Text($"WEB {d.Short(wd)} (BROKEN)", new Vec2(cx - tw / 2 - d.Paper(0.08), zMid), d.SmallTextH, 0, TextAlign.Right);
            double ty = Z(-wd) - bf.Thickness - d.Paper(0.35);
            d.Text(name, new Vec2(cx, ty), d.TextH * 1.2, 0, TextAlign.Center, SheetLayers.Title);
            d.Text($"PL {d.Short(p.Width)} x {d.Short(p.Thickness)}{(both ? " (PAIR)" : "")}", new Vec2(cx, ty - d.TextH * 1.8), d.TextH, 0, TextAlign.Center);
            for (int i = 0; i < notes.Length; i++)
                d.Text(notes[i], new Vec2(cx, ty - d.TextH * (3.3 + 1.5 * i)), d.SmallTextH, 0, TextAlign.Center);
            x += gapX;
        }

        Detail("BEARING STIFFENER", st.BearingStiffener, true, 0,
            new[] { "MILL TO BEAR ON BOTTOM FLANGE", "PLUMB, IN THE PLANE OF THE SUPPORT LINE" });
        Detail("CONNECTION PLATE", st.ConnectionPlate, false, 0,
            new[] { "IN THE CROSS-FRAME PLANE", "NORMAL TO THE FLANGES" });
        if (st.IntermediateMaxSpacing > 0)
            Detail("INTERMEDIATE STIFFENER", st.IntermediateStiffener, false, st.IntermediateBottomGap,
                new[] { "TIGHT FIT TO TOP FLANGE", $"MAX SPACING {d.Len(st.IntermediateMaxSpacing)}, {st.IntermediateSide.ToString().ToUpperInvariant()} FACE" });
        var box0 = dwg.Extents();
        double nx = (box0.MinX + box0.MaxX) / 2;
        d.Text($"FLANGE-TO-WEB WELDS: {d.Short(st.FlangeToWebWeld)} FILLET BOTH SIDES, CONTINUOUS, STOP {d.Short(WeldTermination.HoldBack(def.Welds, st.FlangeToWebWeld))} FROM PL ENDS.",
            new Vec2(nx, box0.MinY - d.Paper(0.2)), d.TextH, 0, TextAlign.Center);
        d.Text($"STIFFENER WELDS STOP {d.Short(hb)} SHORT OF THE CLIPS AND PLATE EDGES (DO NOT WRAP).",
            new Vec2(nx, box0.MinY - d.Paper(0.2) - d.TextH * 1.6), d.TextH, 0, TextAlign.Center);
        d.Text($"WEB WELDS END {d.Short(webEnd)} FROM THE FLANGES {webEndNote}.",
            new Vec2(nx, box0.MinY - d.Paper(0.2) - d.TextH * 3.2), d.TextH, 0, TextAlign.Center);
        d.Text(WeldTermination.FromPlans(def.Welds) ? $"HOLD-BACK PER {def.Welds!.Source.ToUpperInvariant()}" : "HOLD-BACK = WELD SIZE, MIN 1/4\" (AWS D1.5) - UNRESOLVED: NOT ON THE PLANS",
            new Vec2(nx, box0.MinY - d.Paper(0.2) - d.TextH * 4.7), d.SmallTextH, 0, TextAlign.Center);
        var box = dwg.Extents();
        d.Title("STIFFENER DETAILS", "Sections looking along the girder; web broken (not to scale vertically)", new Vec2((box.MinX + box.MaxX) / 2, box.MinY - d.Paper(0.3)));
        return new SheetView("Stiffener details", "Stiffeners", dwg, d.Scale, "Bearing stiffeners, connection plates and intermediate stiffeners with welds and hold backs.");
    }

    /// <summary>General steel notes (paper inches).</summary>
    public static SheetView Notes(ViewContext ctx)
    {
        var def = ctx.Framing.Definition;
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, 1);
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        // Materials as assigned in the model, grouped by part category.
        string Cats(IEnumerable<PartCategory> cs) => string.Join(", ", cs.Select(c => c switch
        {
            PartCategory.FlangePlate => "FLANGES", PartCategory.WebPlate => "WEBS", PartCategory.BearingStiffener or PartCategory.ConnectionStiffener or PartCategory.IntermediateStiffener => "STIFFENERS",
            PartCategory.Gusset => "GUSSETS", PartCategory.Angle => "ANGLES", PartCategory.Channel => "CHANNELS", PartCategory.SplicePlate => "SPLICE PLATES",
            PartCategory.FillerPlate => "FILLERS", PartCategory.SolePlate or PartCategory.BearingPlate or PartCategory.MasonryPlate => "BEARING PLATES",
            PartCategory.BearingDisc or PartCategory.ShearPin or PartCategory.GuideBar => "BEARING PARTS",
            PartCategory.InspectionBracket or PartCategory.Grating or PartCategory.WalkwayStringer or PartCategory.WalkwayPost or PartCategory.WalkwayRail or PartCategory.WalkwayPlate => "INSPECTION ACCESS",
            _ => System.Text.RegularExpressions.Regex.Replace(c.ToString(), "([a-z])([A-Z])", "$1 $2").ToUpperInvariant(),
        }).Distinct());
        var mats = ctx.Steel == null ? new List<(string, string)>() : ctx.Steel.Parts
            .Where(p => p.Category is not (PartCategory.Weld or PartCategory.BearingPad or PartCategory.Ptfe or PartCategory.InspectionCable or PartCategory.InspectionBracket or PartCategory.Grating or PartCategory.WalkwayStringer or PartCategory.WalkwayPost or PartCategory.WalkwayRail or PartCategory.WalkwayPlate) && p.Material.StartsWith("ASTM", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.Material).OrderBy(g => g.Key)
            .Select(g => (g.Key.ToUpperInvariant(), Cats(g.Select(p => p.Category).Distinct()))).ToList();
        var lines = new List<string> { "STRUCTURAL STEEL NOTES", "" };
        int n = 1;
        foreach (var (m, c) in mats)
        {
            lines.Add($"{n++}. {c}: {m}.");
        }
        double hole = def.Bolts.Diameter + In(1.0 / 16);
        lines.Add($"{n++}. HIGH-STRENGTH BOLTS: ASTM F3125 GR. {def.Bolts.Grade}, {S(def.Bolts.Diameter)} DIA AS A BRIDGE DEFAULT (FRAME BOLT NOTES GOVERN), {S(hole)} DIA HOLES, {S(def.Bolts.EdgeDistance)} EDGE DISTANCE.");
        lines.Add($"{n++}. CROSS FRAMES: INTERMEDIATE FRAMES AND CONNECTION PLATES ARE NORMAL TO THE FLANGES; SUPPORT FRAMES AND BEARING STIFFENERS ARE PLUMB.");
        if (def.CrossFrames.Orientation == CrossFrameOrientation.Auto)
            lines.Add($"{n++}. CROSS FRAMES PARALLEL TO SKEW IF SKEW <= {def.CrossFrames.SkewLimitDegrees:0} DEG, OTHERWISE NORMAL TO THE GIRDERS.");
        else lines.Add($"{n++}. CROSS FRAME ORIENTATION: {def.CrossFrames.Orientation.ToString().ToUpperInvariant()}.");
        lines.Add($"{n++}. SHEAR STUDS: {S(def.Studs.Diameter)} DIA, {def.Studs.RowsAcross} PER ROW @ {S(def.Studs.Pitch)}.");
        lines.Add($"{n++}. CROSS-FRAME DETAILS SHOW THE FRAME AS DETAILED IN THE 3D MODEL.");
        var wrapped = new List<string>();
        foreach (var ln0 in lines)
        {
            string ln = ln0;
            var cur = "";
            foreach (var w in ln.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (cur.Length + w.Length + 1 > 46 && cur.Trim().Length > 0) { wrapped.Add(cur); cur = "    " + w; }
                else cur = cur.Length == 0 ? w : cur + " " + w;
            }
            wrapped.Add(cur);
        }
        lines = wrapped;
        for (int i = 0; i < lines.Count; i++)
            d.Text(lines[i], new Vec2(0, -i * d.Paper(0.17)), d.Paper(i == 0 ? 0.13 : 0.08), 0, TextAlign.Left, i == 0 ? SheetLayers.Title : SheetLayers.Text);
        return new SheetView("Steel notes", "Notes", dwg, 1, "", PaperSpace: true);
    }
}
