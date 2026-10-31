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
        foreach (var w in warn.Distinct()) model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, w));
        foreach (var grp in model.Beams.Where(b => b.IsStandardLength && Math.Abs(b.LengthDifference) <= In(1.0 / 16)).GroupBy(b => b.Span))
        {
            var worst = grp.OrderByDescending(b => Math.Abs(b.LengthDifference)).First();
            model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info,
                $"Standard beam fit, span {grp.Key + 1}: {grp.Count()} {worst.Designation} beam(s) at the standard CL-CL bearing length; layout bearings differ by at most " +
                $"{Math.Abs(worst.LengthDifference) / In(1):0.000}\" ({worst.Mark}) - within 1/16\", the beam sits on the layout bearings (no placement rule needed)."));
        }
        // Beams modeled on an assumption the plans do not fix: one warning per reason.
        foreach (var grp in model.Beams.Where(b => b.Assumption != null).GroupBy(b => (b.Assumption!.Reason, b.Assumption.Basis)))
            model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning,
                $"UNRESOLVED (prestressed): {grp.Key.Reason} — {grp.Count()} beam(s): {string.Join(", ", grp.Select(b => b.Mark))}. Modeled: {grp.Key.Basis}."));
        // Plan items of the beam definitions that are not on the sheets.
        var seen = new HashSet<string>();
        var used = model.Beams.Select(b => b.Definition).OfType<PrestressedBeamDefinition>().Distinct().ToList();
        foreach (var d in used)
            foreach (var r in d.Unresolved ?? new List<string>())
                if (seen.Add(r))
                    model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"UNRESOLVED (prestressed {(d.Designation != "" ? d.Designation : TxGirderCatalog.Get(d).Name)}): {r}"));
        // Plan camber tables (recorded, not applied to the geometry): final = camber - SDL deflection, to the printed 1/16".
        foreach (var d in used.Where(d => d.CamberTable is { Count: > 0 }))
        {
            var tab = d.CamberTable!;
            // tolerance: half the printed 1/16" step + the rounding of the two printed 0.001 ft values
            int ok = tab.Count(p => Math.Abs(p.FinalCamber - (p.Camber - p.Deflection)) <= In(1.0 / 32) + Ft(0.001) + 1e-9);
            var peak = tab.OrderByDescending(p => p.Camber).First();
            string beamList = string.Join(", ", model.Beams.Where(b => ReferenceEquals(b.Definition, d)).Select(b => b.Mark));
            model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info,
                $"Camber table {(d.Designation != "" ? d.Designation : TxGirderCatalog.Get(d).Name)} ({d.CamberSource ?? d.Source}; {beamList}): {tab.Count} points, girder-alone camber up to " +
                $"{u.FormatLength(peak.Camber, LengthKind.Short)} at {peak.Fraction:0.00} L; final = camber - SDL deflection to the printed precision in {ok} of {tab.Count} cells. " +
                "The beams are modeled in their final (uncambered) shape."));
        }
        var soles = model.Pads.Where(p => p.SolePlate != null).ToList();
        if (soles.Count > 0)
        {
            var worst = soles.OrderByDescending(p => Math.Abs(p.SolePlate!.TaperMinusSlope)).First();
            model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info,
                $"Sole plates: {soles.Count} tapered plates; plan taper vs the modeled beam slope (no camber / end rotation) differs by up to " +
                $"{worst.SolePlate!.TaperMinusSlope * 100:0.00}% ({worst.SolePlate.Mark}); the stack uses each plate's thickness at the CL bearing."));
        }
        if (model.Beams.Any(b => b.Strands.Any(s => s.Debonded)))
            model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning,
                UnresolvedFlag.Text(used.FirstOrDefault(d => d.Source != "")?.Source is { } src ? src : "beam sheet (debonding table)", "prestressed debond sheathing product / size",
                    "the plans give the debonded lengths only, the sheathing is not specified or dimensioned",
                    $"The sheathing is drawn {u.FormatLength(used.FirstOrDefault()?.SheathThickness ?? In(1.0 / 16), LengthKind.Short)} thick around the strand (display only, IfcTendonConduit).")));
        BuildBarMarks(model, u);
        model.GenerationTime = sw.Elapsed;
        return model;
    }

    /// <summary>Schedule unit of the beam bars of a definition: "{designation} beams".</summary>
    public static string BarUnit(PrestressedBeamDefinition pd, TxGirderShape shape) =>
        pd.Reinforcement?.Unit is { Length: > 0 } un ? un : $"{(pd.Designation != "" ? pd.Designation : shape.Name)} beams";

    /// <summary>Beam bar marks (one per unit + mark, counts over all beams) and the checks: UNRESOLVED items, bars crossing an end face,
    /// bar list counts per beam, weight per beam vs the printed weight.</summary>
    private static void BuildBarMarks(PrestressedModel model, UnitSystem u)
    {
        const double Lb = 2.2046226218;
        var groups = model.Beams.Where(b => b.Definition?.Reinforcement != null).GroupBy(b => BarUnit(b.Definition!, b.Shape)).ToList();
        var seen = new HashSet<string>();
        foreach (var g in groups)
        {
            string unit = g.Key;
            var rd = g.First().Definition!.Reinforcement!;
            foreach (var set in rd.Bars.Where(x => x.Unresolved.Length == 0 && x.Path.Count > 0))
            {
                if (model.BarMarks.Any(m => m.Unit == unit && m.Mark == set.Mark)) continue;
                int count = g.Sum(b => b.Bars.Count(x => x.Mark == set.Mark));
                model.BarMarks.Add(new Concrete.BarMark
                {
                    Mark = set.Mark, Size = Concrete.RebarCatalog.Size(set.Size), Shape = PrestressedBeamRebar.Shape(set), Unit = unit,
                    Location = set.Location != "" ? set.Location : set.Description, Epoxy = set.Epoxy, Coating = set.Coating, Count = count,
                });
            }
            foreach (var r in rd.Unresolved.Concat(rd.Bars.Where(x => x.Unresolved.Length > 0).Select(x => x.Unresolved)))
                if (seen.Add(r)) model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, r.StartsWith("UNRESOLVED") ? $"{r} [{unit}]" : $"UNRESOLVED (beam reinforcing {unit}): {r}"));
            foreach (var note in rd.Notes)
                if (seen.Add(note)) model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Beam reinforcing {unit}: {note}"));
            // bars crossing an end face (skewed ends; the standards draw square ends)
            var outside = g.SelectMany(b => b.BarsOutsideEnd.Select(kv => (b, mark: kv.Key, kv.Value.Count, kv.Value.Max))).GroupBy(x => x.mark).ToList();
            foreach (var o in outside)
                model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, UnresolvedFlag.Text(rd.Source,
                    $"{unit} bar {o.Key} at a skewed beam end",
                    $"{o.Sum(x => x.Count)} bar(s) in {o.Count()} beam(s) ({string.Join(", ", o.Select(x => x.b.Mark).Distinct())}) reach up to " +
                    $"{u.FormatLength(o.Max(x => x.Max), LengthKind.Short)} beyond the skewed end face; the standard sheet dimensions the bar stations from a square beam end and does not say how they are set out at a skewed end",
                    "Modeled square to the beam at the dimensioned stations measured along the beam CL (not moved).")));
            // bar list per beam
            if (rd.BarList is { Count: > 0 } list)
            {
                var bad = new List<string>();
                foreach (var b in g)
                    foreach (var row in list)
                    {
                        int n = b.Bars.Count(x => x.Mark == row.Mark);
                        if (n != row.Count) bad.Add($"{b.Mark} {row.Mark} {n} placed / {row.Count} listed");
                    }
                foreach (var m in model.BarMarks.Where(m => m.Unit == unit))
                    if (list.FirstOrDefault(r => r.Mark == m.Mark) is { } row && row.Length > 0 && Math.Abs(m.Length - row.Length) > In(1.0 / 16))
                        bad.Add($"{m.Mark} length {u.FormatLength(m.Length)} vs listed {u.FormatLength(row.Length)}");
                foreach (var m in model.BarMarks.Where(m => m.Unit == unit && list.All(r => r.Mark != m.Mark))) bad.Add($"{m.Mark} not in the bar list");
                if (bad.Count > 0)
                    model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"CHECK (beam bar list, {rd.Source}): {unit}: {string.Join("; ", bad.Distinct().Take(20))}{(bad.Count > 20 ? $" ... ({bad.Count} rows)" : "")}."));
                else
                    model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Beam bar list {unit}: every beam has the listed count of every mark ({list.Count} marks, {list.Sum(r => r.Count)} bars per beam)."));
            }
            // weight per beam (scheduled lengths)
            var first = g.First();
            double perBeam = first.Bars.Sum(x => { var m = model.MarkOf(x); return m.Length * m.Size.MassPerLength; }) * Lb;
            string listed = rd.ListedWeightLb > 0 ? $"; printed {rd.ListedWeightLb:N0} lb ({rd.ListedWeightSource}), difference {perBeam - rd.ListedWeightLb:+0;-0;0} lb" : "; no printed weight";
            model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Beam reinforcing {unit}: {first.Bars.Count} bars, {perBeam:N0} lb per beam ({g.Count()} beams){listed}."));
        }
    }

    private static (PrestressedBeam, List<PrestressedPad>, List<BearingSeat>) BuildBeam(BridgeFraming f, UnitSystem u, TxGirderShape baseShape,
        PrestressedBeamDefinition basePd, int i, int j, System.Collections.Concurrent.ConcurrentQueue<string> warn)
    {
        // Per-span / per-beam-line definition (BTD55 / BTD105 / BTD60 in one bridge).
        var pd = basePd.For(j, i);
        var shape = TxGirderCatalog.Get(pd);
        if (Math.Abs(shape.Depth - baseShape.Depth) > 1e-6)
            warn.Enqueue($"G{i + 1} span {j + 1}: {shape.Name} is {u.FormatLength(shape.Depth, LengthKind.Short)} deep but the framing (top of beam, haunch, seats) uses the base beam depth {u.FormatLength(baseShape.Depth, LengthKind.Short)}.");
        var def = f.Definition;
        var g = f.Girders[i];
        var c = g.Spans[j];
        var supA = f.Supports[j];
        var supB = f.Supports[j + 1];
        // Per-span bearing points (a span layout may give the girder different points each side of an interior support).
        var brA = f.SpanBearing(i, j, false) ?? throw new InvalidOperationException("no bearing");
        var brB = f.SpanBearing(i, j, true) ?? throw new InvalidOperationException("no bearing");
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
        // Per-span layout: the layout's bearing points are the CL bearings (e.g. Woodbury "1'-0 from CL pier along the beam").
        if (c.LayoutBegin is { } lb0) brgA = Vec2.Dot(lb0.XY - pa, t);
        if (c.LayoutEnd is { } lb1) brgB = Vec2.Dot(lb1.XY - pa, t);
        // Span-layout bearing offsets (GirderSpanEnd.BearingOffset, the layout line is the CL pier / face of backwall): the beam ends
        // BearingToEnd beyond its CL bearing (TxDOT IGND / IGEB girder length = C-C bearing + 2 x 9"), along the girder CL when the
        // offset is measured along the girder, normal to the support line when it is measured normal to it.
        if (def.Girders.SpanLayoutOf(j) is { } spl)
        {
            if (c.LayoutBegin != null && spl.Begin.BearingOffset > 0)
                endA = brgA - (spl.Begin.BearingMeasure == BearingOffsetMeasure.NormalToSupport ? pd.BearingToEndAt(false) / Cos(supA) : pd.BearingToEndAt(false));
            if (c.LayoutEnd != null && spl.End.BearingOffset > 0)
                endB = brgB + (spl.End.BearingMeasure == BearingOffsetMeasure.NormalToSupport ? pd.BearingToEndAt(true) / Cos(supB) : pd.BearingToEndAt(true));
        }
        double fkA = squareA ? 0 : kA, fkB = squareB ? 0 : kB;
        string typeName = pd.Designation != "" ? pd.Designation : shape.Name;

        // Standard beam: built at the STANDARD length (CL-CL bearing + overall, along the CL), never stretched to the layout;
        // the difference against the layout bearings is a CHECK warning, the placement between them an input (UNRESOLVED when
        // the plans don't state it and the lengths differ).
        // Lengths along the sloped beam = plan-chord parameter s × kSlope (the section is kept vertical).
        double kSlope = Math.Sqrt(1 + slope * slope);
        double layoutSpan = (brgB - brgA) * kSlope, lengthDiff = 0;
        double fitTol = In(1.0 / 16);
        bool standardLength = pd.StandardBearingLength > 0;
        Assumption? assumption = null;
        if (standardLength)
        {
            double lsTrue = pd.StandardBearingLength;
            double lo = pd.StandardOverallLength > 0 ? pd.StandardOverallLength : lsTrue + 2 * pd.BearingToEnd;
            lengthDiff = lsTrue - layoutSpan;
            double ls = lsTrue / kSlope, e = (lo - lsTrue) / 2 / kSlope; // in s
            // Effective placement: AtPierBearing resolves to the end at the (single) pier; null / ambiguous = undecided.
            StandardBeamPlacement? place = pd.Placement switch
            {
                StandardBeamPlacement.AtPierBearing when abutA && !abutB => StandardBeamPlacement.AtEndBearing,
                StandardBeamPlacement.AtPierBearing when !abutA && abutB => StandardBeamPlacement.AtStartBearing,
                StandardBeamPlacement.AtPierBearing => null,
                var p => p,
            };
            double a0 = place switch
            {
                StandardBeamPlacement.AtStartBearing => brgA,
                StandardBeamPlacement.AtEndBearing => brgB - ls,
                _ => brgA - lengthDiff / kSlope / 2,
            };
            brgA = a0;
            brgB = a0 + ls;
            endA = brgA - e;
            endB = brgB + e;
            // Within 1/16": the standard beam fits the layout bearings (Info per span in Generate); no placement rule needed.
            if (Math.Abs(lengthDiff) > fitTol)
            {
                string L(double m) => u.FormatLength(m);
                string how = place switch
                {
                    StandardBeamPlacement.AtStartBearing => "standard CL bearing on the layout bearing at the start",
                    StandardBeamPlacement.AtEndBearing => "standard CL bearing on the layout bearing at the end",
                    StandardBeamPlacement.Centered => "centred between the layout bearings",
                    _ => "placement NOT SET - drawn centred between the layout bearings for display only",
                };
                warn.Enqueue($"CHECK (standard beam length): G{i + 1} span {j + 1} {typeName}: standard CL-CL bearing {L(lsTrue)} vs layout {L(layoutSpan)} along the beam " +
                             $"(standard {(lengthDiff > 0 ? "longer" : "shorter")} by {u.FormatLength(Math.Abs(lengthDiff), LengthKind.Short)}); the beam is modeled at the standard length " +
                             $"({how}{(pd.PlacementBasis != "" ? ", " + pd.PlacementBasis : "")}), not stretched.");
                if (place == null)
                    assumption = Assumption.Unresolved(
                        (pd.Placement == StandardBeamPlacement.AtPierBearing
                            ? $"placement 'at the pier bearing' of the standard {typeName} beam is ambiguous (both or neither end at a pier)"
                            : $"placement of the standard {typeName} beam is not set: the layout CL-CL bearing distance differs from its standard CL-CL bearing length by more than 1/16\" and the plans do not say where it sits between the layout bearings") +
                        (pd.Source != "" ? $" ({pd.Source})" : ""),
                        "centred between the layout bearings (display only)");
            }
        }
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
        double holdFromEnd = pd.HoldDownFromMidspan > 0 ? Math.Max(0, len / 2 - pd.HoldDownFromMidspan / kSlope) : Math.Max(0, len / 2 - hold);
        var layout = new List<(Vec2 p, double yEnd, bool harped, double xEnd)>();
        var debond = new List<(double s, double e, string sym)>();
        var own = new List<(double dia, string? label)>();
        if (StrandPatterns.Resolve(pd, D) is { } sl) // explicit (possibly empty = no strands), grid-column harping applied
        {
            own.AddRange(sl.Select(q => (q.Diameter, q.Label)));
            layout.AddRange(sl.Select(q =>
            {
                double xe = double.IsNaN(q.XEnd) ? q.X : q.XEnd;
                bool h = q.Harped && (Math.Abs(q.YEnd - q.Y) > 1e-6 || Math.Abs(xe - q.X) > 1e-6);
                return (new Vec2(q.X, q.Y), h ? q.YEnd : q.Y, h, xe);
            }));
            debond.AddRange(sl.Select(q => (q.DebondStart, q.DebondEnd, q.DebondSymbol)));
            if (sl.Any(q => q.DebondStart + q.DebondEnd > len))
                warn.Enqueue($"G{i + 1} span {j + 1}: debond lengths exceed the beam length {u.FormatLength(len)}.");
        }
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
        for (int n = 0; n < layout.Count; n++)
        {
            var (p, yEnd, harped, xEnd) = layout[n];
            var (dbS, dbE, sym) = n < debond.Count ? debond[n] : (0.0, 0.0, "");
            var (dia, label) = n < own.Count ? own[n] : (0.0, null);
            if (!harped)
            {
                strands.Add(new StrandInstance
                {
                    Points = new[] { P(SA(p.X, p.Y), p.X, p.Y), P(SB(p.X, p.Y), p.X, p.Y) }, Section = p, EndHeight = p.Y,
                    DebondStart = dbS, DebondEnd = dbE, DebondSymbol = sym, Diameter = dia, Label = label,
                });
                continue;
            }
            double h0 = endA + holdFromEnd, h1 = endB - holdFromEnd;
            strands.Add(new StrandInstance
            {
                Points = new[] { P(SA(xEnd, yEnd), xEnd, yEnd), P(h0, p.X, p.Y), P(h1, p.X, p.Y), P(SB(xEnd, yEnd), xEnd, yEnd) },
                Section = p, Depressed = true, EndHeight = yEnd, DebondStart = dbS, DebondEnd = dbE, DebondSymbol = sym, Diameter = dia, Label = label,
            });
        }

        // ---- exact body for shapes with arcs (IFC: clipped extrusion of the true profile)
        BeamBody? body = null;
        if (shape.Profile != null)
        {
            double sLo = outline.Min(q => SA(q.X, q.Y)) - 0.01, sHi = outline.Max(q => SB(q.X, q.Y)) + 0.01;
            var up = new Vec3(0, 0, 1);
            var t3 = new Vec3(t.X, t.Y, 0);
            var bx = Vec3.Cross(up, t3).Normalized();
            var org = P(sLo, 0, 0);
            (Vec3 pt, Vec3 nrm) Plane(Func<double, double, double> sf, double sign)
            {
                var p0 = P(sf(0, 0), 0, 0);
                var n = Vec3.Cross(P(sf(1, 0), 1, 0) - p0, P(sf(0, 1), 0, 1) - p0).Normalized();
                return (p0, Vec3.Dot(n, t3) * sign < 0 ? -n : n);
            }
            var (pa0, na) = Plane(SA, -1);
            var (pb0, nb) = Plane(SB, 1);
            body = new BeamBody(new Frame3(org, bx, up, Vec3.Cross(bx, up)), Vec3.Dot(bx, new Vec3(right.X, right.Y, 0)),
                P(sHi, 0, 0) - org, pa0, na, pb0, nb);
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
            Definition = pd, Designation = pd.Designation, LayoutBearingSpan = layoutSpan, LengthDifference = lengthDiff,
            IsStandardLength = standardLength, Assumption = assumption, Body = body,
        };
        beam.Strands.AddRange(strands);
        // ---- mild reinforcing (OPTIONAL): stirrups / end-zone / longitudinal bars of the standard, along the beam as built
        if (pd.Reinforcement is { } rd)
        {
            var placed = PrestressedBeamRebar.Place(rd, BarUnit(pd, shape), new PrestressedBeamRebar.BeamFrame(P, SA, SB, kSlope, D));
            beam.Bars.AddRange(placed.Bars);
            foreach (var kv in placed.OutsideEnd) beam.BarsOutsideEnd[kv.Key] = kv.Value;
        }

        // ---- pads and seats: IGEB table sizes (or the input size), square to the girder, level. The pad sits on the
        // nominal CL bearing unless that would put it within 1" of the girder end.
        var padList = new List<PrestressedPad>();
        var seatList = new List<BearingSeat>();
        var padType = shape.IsTx ? shape.Type : shape.DepthIn > 58 ? TxGirderType.Tx62 : TxGirderType.Tx54;
        if (shape.IsStateStandard && pd.PadLength <= 0 && (pd.StartBearing == null || pd.EndBearing == null))
            warn.Enqueue(UnresolvedFlag.Text(pd.Source != "" ? pd.Source : "bearing sheet", $"prestressed {typeName} bearing pad size", "no pad size was entered for this beam (PadLength = 0), so the plan pad is unknown to the model", "The TxDOT IGEB pad table is used as a placeholder."));
        foreach (var (sNom, sup, end, abut, k, fk, eCl) in new[] { (brgA, supA, 0, abutA, kA, fkA, endA), (brgB, supB, 1, abutB, kB, fkB, endB) })
        {
            double skew = def.Supports[sup.Index].SkewDegrees;
            var bd = end == 0 ? pd.StartBearing : pd.EndBearing;
            var (pl, pw, clipA, clipB, round) = bd != null ? (bd.Length, bd.Width, 0.0, 0.0, false)
                : pd.PadLength > 0 ? (pd.PadLength, pd.PadWidth, 0.0, 0.0, false) : TxGirderCatalog.IgebPad(padType, abut, skew);
            double padT = bd?.Thickness ?? pd.PadThickness;
            double sgn = end == 0 ? 1 : -1;
            double reach = (round ? pl / 2 : pl / 2) + In(1) + (round ? pl / 2 : pw / 2) * Math.Abs(fk);
            double sPad = abut ? sNom : end == 0 ? Math.Max(sNom, eCl + reach) : Math.Min(sNom, eCl - reach);
            var cBot = P(sPad, 0, 0);
            var sp = bd?.SolePlate;
            double tSole = sp?.ThicknessAtBearing ?? 0;
            double z1 = cBot.Z - tSole, z0 = z1 - padT;
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
            // Pad axis: along the beam, or (interior supports, PierPadOrientation = ParallelToSupport) along the support-line normal
            // so the pad width lies along the support line (Woodbury p.20).
            bool parallel = !abut && pd.PierPadOrientation == PadOrientation.ParallelToSupport;
            var ax = t;
            if (parallel)
            {
                ax = sup.Direction.Left;
                if (Vec2.Dot(ax, t) < 0) ax = -ax;
            }
            var fr = new Frame3(new Vec3(cBot.X, cBot.Y, 0), new Vec3(ax.X, ax.Y, 0), new Vec3(ax.Right.X, ax.Right.Y, 0), new Vec3(0, 0, 1));
            var pm = new Mesh();
            MeshBuilder.AddExtrusion(pm, poly, fr, z0, z1);
            string padMark = $"{mark}-{(end == 0 ? "B" : "E")}";
            PrestressedSolePlate? sole = null;
            if (sp != null)
            {
                // Tapered sole plate: level bottom on the pad, top = plane through the printed end thicknesses, CL bearing at BearingAt from
                // the back-station end (u = 0 at the CL bearing, + = ahead station = along the beam axis t).
                double u0 = -sp.BearingAt, u1 = sp.Length - sp.BearingAt;
                var sec = new[] { new Vec2(u0, 0), new Vec2(u1, 0), new Vec2(u1, sp.ThicknessAhead), new Vec2(u0, sp.ThicknessBack) };
                var ux = new Vec3(t.X, t.Y, 0); var xx = new Vec3(right.X, right.Y, 0);
                Vec3 W(Vec2 q, double x) => new Vec3(cBot.X, cBot.Y, z1) + ux * q.X + xx * x + new Vec3(0, 0, q.Y);
                var sm = new Mesh();
                MeshBuilder.AddLoft(sm, sec, new[] { sec.Select(q => W(q, -sp.Width / 2)).ToArray(), sec.Select(q => W(q, sp.Width / 2)).ToArray() });
                double taper = sp.Length > 0 ? (sp.ThicknessBack - sp.ThicknessAhead) / sp.Length : 0;
                sole = new PrestressedSolePlate
                {
                    Mark = sp.Mark != "" ? $"{padMark} {sp.Mark}" : padMark + " sole plate", Mesh = sm, Length = sp.Length, Width = sp.Width,
                    ThicknessBack = sp.ThicknessBack, ThicknessAhead = sp.ThicknessAhead, ThicknessAtBearing = tSole, TaperMinusSlope = taper + slope,
                    Description = $"Sole plate {(sp.Mark != "" ? sp.Mark + " " : "")}{u.FormatLength(sp.Length, LengthKind.Short)} x {u.FormatLength(sp.Width, LengthKind.Short)}, " +
                                  $"{u.FormatLength(sp.ThicknessBack, LengthKind.Short)} to {u.FormatLength(sp.ThicknessAhead, LengthKind.Short)}, {u.FormatLength(tSole, LengthKind.Short)} at CL bearing" +
                                  (sp.Source != "" ? $" ({sp.Source})" : ""),
                };
            }
            padList.Add(new PrestressedPad
            {
                Mark = padMark, Support = sup.Name, Girder = i, Span = j, Mesh = pm,
                Center = cBot.XY.WithZ((z0 + z1) / 2), Length = pl, Width = round ? pl : pw, Thickness = padT,
                Description = bd != null
                    ? $"{(bd.Mark != "" ? bd.Mark + " " : "")}elastomeric {desc} x {u.FormatLength(padT, LengthKind.Short)}{(bd.Description != "" ? ", " + bd.Description : "")}"
                    : $"Elastomeric {desc} x {u.FormatLength(pd.PadThickness, LengthKind.Short)}" + (pd.PadLength > 0 ? "" : " (IGEB)") +
                      (parallel ? ", parallel to the support" : ""),
                OffNominal = Math.Abs(sPad - sNom), BearingMark = bd?.Mark ?? "", SolePlate = sole,
            });
            var so = a.Inverse(cBot.XY);
            seatList.Add(new BearingSeat(sup.Name, i, so.Station, z0, 0, "Elastomeric", "", cBot.XY, ax, pl, round ? pl : pw));
        }
        return (beam, padList, seatList);
    }
}
