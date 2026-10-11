using System.Diagnostics;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Prestressed;

/// <summary>
/// Builds TxDOT prestressed I-girders (IGD / IGND) from the framing: one straight beam per span and girder line between
/// the support-line points of the framing (chorded in plan, straight in profile — no camber).
/// <list type="bullet">
/// <item>Abutments: the support station is the CL bearing; the girder end is <see cref="PrestressedBeamDefinition.BearingToEnd"/>
/// beyond it, measured normal to the support line (backwall); the end is plumb.</item>
/// <item>Interior bents: the girder end is <see cref="PrestressedBeamDefinition.EndGapAtBent"/> from the CL bent and the CL
/// bearing a further BearingToEnd, both measured along the girder; the end is square to the girder axis in elevation.</item>
/// <item>End faces are parallel to the support line in plan (skewed ends). Webs plumb, sections vertical.</item>
/// </list>
/// </summary>
public static class PrestressedGenerator
{
    public static PrestressedModel Generate(BridgeFraming f, UnitSystem u)
    {
        var sw = Stopwatch.StartNew();
        var def = f.Definition;
        var pd = def.Girders.Prestressed;
        var shape = TxGirderCatalog.Get(pd);
        var model = new PrestressedModel { Definition = pd, Shape = shape };
        int nG = f.Girders.Count;
        int nSpan = f.Supports.Count - 1;
        var beams = new PrestressedBeam?[nG, nSpan];
        var pads = new List<PrestressedPad>?[nG, nSpan];
        var seats = new List<BearingSeat>?[nG, nSpan];
        var warn = new System.Collections.Concurrent.ConcurrentQueue<string>();

        Parallel.For(0, nG * nSpan, k =>
        {
            int i = k / nSpan, j = k % nSpan;
            try
            {
                var (b, p, s) = BuildBeam(f, u, shape, pd, i, j, warn);
                beams[i, j] = b;
                pads[i, j] = p;
                seats[i, j] = s;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                warn.Enqueue($"G{i + 1} span {j + 1}: {ex.Message}");
            }
        });
        for (int j = 0; j < nSpan; j++)
            for (int i = 0; i < nG; i++)
            {
                if (beams[i, j] is { } b) model.Beams.Add(b);
                if (pads[i, j] is { } p) model.Pads.AddRange(p);
                if (seats[i, j] is { } s) model.BearingSeats.AddRange(s);
            }
        foreach (var w in warn) model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, w));
        model.GenerationTime = sw.Elapsed;
        return model;
    }

    private static (PrestressedBeam, List<PrestressedPad>, List<BearingSeat>) BuildBeam(BridgeFraming f, UnitSystem u, TxGirderShape shape,
        PrestressedBeamDefinition pd, int i, int j, System.Collections.Concurrent.ConcurrentQueue<string> warn)
    {
        var def = f.Definition;
        var g = f.Girders[i];
        var c = g.Spans[j];
        var supA = f.Supports[j];
        var supB = f.Supports[j + 1];
        var brA = supA.Bearings[i] ?? throw new InvalidOperationException("no bearing");
        var brB = supB.Bearings[i] ?? throw new InvalidOperationException("no bearing");
        var pa = c.Start;
        var pb = c.End;
        double lc = (pb - pa).Length;
        var t = (pb - pa) / lc;
        var right = t.Right;
        double zA = brA.TopOfSteel.Z, zB = brB.TopOfSteel.Z;
        double slope = (zB - zA) / lc;
        double D = shape.Depth;

        bool abutA = def.Supports[j].Type == SupportType.Abutment;
        bool abutB = def.Supports[j + 1].Type == SupportType.Abutment;
        // IGEB: girder ends are square at conventional interior bents (nearest corner EndGapAtBent from the CL bent),
        // parallel to the backwall and plumb at abutments.
        bool squareA = !abutA && pd.InteriorEnds == InteriorGirderEnds.Square;
        bool squareB = !abutB && pd.InteriorEnds == InteriorGirderEnds.Square;
        double Cos(SupportLineResult s) => Math.Max(0.2, Math.Abs(Vec2.Dot(t, s.Direction.Left)));
        // Support line in beam coordinates: s = x * k (x right of the girder CL).
        double Kx(SupportLineResult s) => Vec2.Dot(s.Direction, t) / Vec2.Dot(s.Direction, right);
        double kA = Kx(supA), kB = Kx(supB);
        double hwMax = Math.Max(shape.TopWidth, shape.BottomWidth) / 2;
        // End faces at the girder CL, and the nominal CL bearings (1'-0" from the CL bent along the girder; the support
        // station at abutments).
        double endA = abutA ? -pd.BearingToEnd / Cos(supA) : pd.EndGapAtBent + (squareA ? hwMax * Math.Abs(kA) : 0);
        double endB = abutB ? lc + pd.BearingToEnd / Cos(supB) : lc - pd.EndGapAtBent - (squareB ? hwMax * Math.Abs(kB) : 0);
        double brgA = abutA ? 0 : pd.EndGapAtBent + pd.BearingToEnd;
        double brgB = abutB ? lc : lc - pd.EndGapAtBent - pd.BearingToEnd;
        double fkA = squareA ? 0 : kA, fkB = squareB ? 0 : kB;
        // s of the end face at section point (x, y): skewed or square in plan; square to the axis in elevation at interior bents.
        double SA(double x, double y) => endA + x * fkA + (abutA ? 0 : slope * (D - y));
        double SB(double x, double y) => endB + x * fkB + (abutB ? 0 : slope * (D - y));
        Vec3 P(double s, double x, double y)
        {
            var q = pa + t * s + right * x;
            return new Vec3(q.X, q.Y, zA + slope * s - D + y);
        }

        var outline = shape.Outline();
        var mesh = new Mesh();
        MeshBuilder.AddLoft(mesh, outline, new[]
        {
            outline.Select(q => P(SA(q.X, q.Y), q.X, q.Y)).ToArray(),
            outline.Select(q => P(SB(q.X, q.Y), q.X, q.Y)).ToArray(),
        });

        double len = endB - endA;
        double skewA = def.Supports[j].SkewDegrees, skewB = def.Supports[j + 1].SkewDegrees;

        // ---- strands: explicit layout (PGSuper) or the IGND fill with the lowest A-position pairs depressed
        double spanL = brgB - brgA;
        double hold = pd.HarpPointFraction > 0 ? pd.HarpPointFraction * spanL : Math.Max(len / 20, Ft(5));
        double holdFromEnd = Math.Max(0, len / 2 - hold);
        var layout = new List<(Vec2 p, double yEnd, bool harped, double xEnd)>();
        if (pd.StrandLayout is { } sl) // explicit (possibly empty = no strands)
            layout.AddRange(sl.Select(q =>
            {
                double xe = double.IsNaN(q.XEnd) ? q.X : q.XEnd;
                bool h = q.Harped && (Math.Abs(q.YEnd - q.Y) > 1e-6 || Math.Abs(xe - q.X) > 1e-6);
                return (new Vec2(q.X, q.Y), h ? q.YEnd : q.Y, h, xe);
            }));
        else
        {
            var pos = shape.StrandPositions().Take(pd.StrandCount).ToList();
            double aX = In(1) + 1e-6;
            var aRows = pos.Where(p => Math.Abs(p.X) <= aX).Select(p => p.Y).Distinct().OrderBy(y => y).ToList();
            int depPairs = Math.Min(pd.DepressedStrands / 2, aRows.Count);
            if (pd.DepressedStrands / 2 > aRows.Count && i == 0 && j == 0)
                warn.Enqueue($"Only {aRows.Count * 2} strands are in A positions; {depPairs * 2} strands are depressed.");
            var depRows = aRows.Take(depPairs).ToList();
            double top = pd.DepressedTopAtEnd > 0 ? pd.DepressedTopAtEnd : shape.HighestAPosition;
            foreach (var p in pos)
            {
                int dr = Math.Abs(p.X) <= aX ? depRows.IndexOf(p.Y) : -1;
                // Keep the 2" spacing at the ends: the highest depressed row goes to the top position.
                layout.Add(dr < 0 ? (p, p.Y, false, p.X) : (p, top - In(2) * (depRows.Count - 1 - dr), true, p.X));
            }
        }
        var strands = new List<StrandInstance>();
        foreach (var (p, yEnd, harped, xEnd) in layout)
        {
            if (!harped)
            {
                strands.Add(new StrandInstance { Points = new[] { P(SA(p.X, p.Y), p.X, p.Y), P(SB(p.X, p.Y), p.X, p.Y) }, Section = p, EndHeight = p.Y });
                continue;
            }
            double h0 = endA + holdFromEnd, h1 = endB - holdFromEnd;
            strands.Add(new StrandInstance
            {
                Points = new[] { P(SA(xEnd, yEnd), xEnd, yEnd), P(h0, p.X, p.Y), P(h1, p.X, p.Y), P(SB(xEnd, yEnd), xEnd, yEnd) },
                Section = p, Depressed = true, EndHeight = yEnd,
            });
        }

        // ---- haunch along the beam (deck bottom − top of beam at the CL)
        double hMin = double.MaxValue, hMax = double.MinValue;
        var a = f.Alignment;
        for (int k = 0; k <= 20; k++)
        {
            double s = brgA + (brgB - brgA) * k / 20.0;
            var q = P(s, 0, D);
            var so = a.Inverse(q.XY);
            double h = def.Deck.FinishedElevation(a, so.Station, so.Offset) - def.Deck.Thickness - q.Z;
            hMin = Math.Min(hMin, h);
            hMax = Math.Max(hMax, h);
        }
        if (hMin < In(0.5))
            warn.Enqueue($"G{i + 1} span {j + 1}: haunch drops to {u.FormatLength(hMin, LengthKind.Short)} (no camber modeled) — increase the haunch.");

        string mark = $"G{i + 1}-S{j + 1}";
        var beam = new PrestressedBeam
        {
            Mark = mark, Girder = i, Span = j, Shape = shape, Mesh = mesh, Length = len, BearingSpan = brgB - brgA,
            TopStart = P(endA, 0, D), TopEnd = P(endB, 0, D), BearingStart = P(brgA, 0, D), BearingEnd = P(brgB, 0, D),
            Slope = slope, SkewStart = skewA, SkewEnd = skewB, StrandDiameter = pd.StrandDiameter, HoldDownFromEnd = holdFromEnd,
            MinHaunch = hMin, MaxHaunch = hMax,
        };
        beam.Strands.AddRange(strands);

        // ---- pads and seats: IGEB table sizes (or the input size), square to the girder, level. The pad sits on the
        // nominal CL bearing unless that would put it within 1" of the girder end.
        var padList = new List<PrestressedPad>();
        var seatList = new List<BearingSeat>();
        var padType = shape.IsTx ? shape.Type : shape.DepthIn > 58 ? TxGirderType.Tx62 : TxGirderType.Tx54;
        foreach (var (sNom, sup, end, abut, k, fk, eCl) in new[] { (brgA, supA, 0, abutA, kA, fkA, endA), (brgB, supB, 1, abutB, kB, fkB, endB) })
        {
            double skew = def.Supports[sup.Index].SkewDegrees;
            var (pl, pw, clipA, clipB, round) = pd.PadLength > 0 ? (pd.PadLength, pd.PadWidth, 0.0, 0.0, false) : TxGirderCatalog.IgebPad(padType, abut, skew);
            double sgn = end == 0 ? 1 : -1;
            double reach = (round ? pl / 2 : pl / 2) + In(1) + (round ? pl / 2 : pw / 2) * Math.Abs(fk);
            double sPad = abut ? sNom : end == 0 ? Math.Max(sNom, eCl + reach) : Math.Min(sNom, eCl - reach);
            var cBot = P(sPad, 0, 0);
            double z1 = cBot.Z, z0 = z1 - pd.PadThickness;
            // Plan outline in (u along the girder, x right of CL); clips on the corner nearest the skewed end face and its diagonal.
            var poly = new List<Vec2>();
            string desc;
            if (round)
            {
                for (int n = 0; n < 24; n++) poly.Add(Vec2.FromAngle(2 * Math.PI * n / 24) * (pl / 2));
                desc = $"{u.FormatLength(pl, LengthKind.Short)} dia";
            }
            else
            {
                double hl = pl / 2, hw = pw / 2;
                var corners = new[] { new Vec2(-hl, -hw), new Vec2(hl, -hw), new Vec2(hl, hw), new Vec2(-hl, hw) };
                // Corner toward the end face (u = −sgn side) on the acute side (x sign of k for the start end).
                double xs = Math.Sign(k == 0 ? 1 : k) * sgn;
                bool Clip(Vec2 q) => clipA > 0 && ((Math.Sign(q.X) == -sgn && Math.Sign(q.Y) == xs) || (Math.Sign(q.X) == sgn && Math.Sign(q.Y) == -xs));
                foreach (var cq in corners)
                {
                    if (!Clip(cq)) { poly.Add(cq); continue; }
                    // Two points replace the corner, in counter-clockwise order.
                    var alongU = new Vec2(cq.X - Math.Sign(cq.X) * clipA, cq.Y);
                    var alongX = new Vec2(cq.X, cq.Y - Math.Sign(cq.Y) * clipB);
                    bool uFirst = Math.Sign(cq.X) == Math.Sign(cq.Y);
                    poly.Add(uFirst ? alongX : alongU);
                    poly.Add(uFirst ? alongU : alongX);
                }
                desc = $"{u.FormatLength(pl, LengthKind.Short)} x {u.FormatLength(pw, LengthKind.Short)}" +
                       (clipA > 0 ? $", clipped {u.FormatLength(clipA, LengthKind.Short)} x {u.FormatLength(clipB, LengthKind.Short)}" : "");
            }
            if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
            var fr = new Frame3(new Vec3(cBot.X, cBot.Y, 0), new Vec3(t.X, t.Y, 0), new Vec3(right.X, right.Y, 0), new Vec3(0, 0, 1));
            var pm = new Mesh();
            MeshBuilder.AddExtrusion(pm, poly, fr, z0, z1);
            padList.Add(new PrestressedPad
            {
                Mark = $"{mark}-{(end == 0 ? "B" : "E")}", Support = sup.Name, Girder = i, Span = j, Mesh = pm,
                Center = cBot.XY.WithZ((z0 + z1) / 2), Length = pl, Width = round ? pl : pw, Thickness = pd.PadThickness,
                Description = $"Elastomeric {desc} x {u.FormatLength(pd.PadThickness, LengthKind.Short)}" + (pd.PadLength > 0 ? "" : " (IGEB)"),
                OffNominal = Math.Abs(sPad - sNom),
            });
            var so = a.Inverse(cBot.XY);
            seatList.Add(new BearingSeat(sup.Name, i, so.Station, z0, 0, "Elastomeric", "", cBot.XY, t, pl, round ? pl : pw));
        }
        return (beam, padList, seatList);
    }
}
