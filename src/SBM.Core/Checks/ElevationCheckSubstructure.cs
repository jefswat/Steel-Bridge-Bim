using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using SBM.Core.Units;

namespace SBM.Core.Checks;

/// <summary>
/// The substructure part of a bearing chain, as values (for scripts that reconcile the chain with the plan elevations).
/// Elevations in m; NaN = not reached (no column / footing / piles under this bearing).
/// </summary>
public sealed record SubstructureChainValues(string Unit, int Column, double SeatS, double SeatN, double SeatZ, double PedestalTop, double CapTop,
    double CapBottomAtSeat, double ColumnTop, double FlareZ, double FoundationTop, double FoundationBottom, double PileHead);

/// <summary>
/// Below the bearing seat: pedestal (plan size / shared block top = highest seat) → cap top (as-designed CapProfile polygon, stepped
/// CapProfileAhead, cap section step) → cap bottom under the seat → soffit at the column that carries it (= column top) → flare →
/// footing top → footing bottom → pile head. Every value is recomputed from the <see cref="SubstructureDefinition"/> inputs with the
/// source stated, then compared with the <see cref="SubstructureGenerator"/> result. MUST mirror the generator: tests fail if they drift.
/// Differences between inputs (seats that differ on a shared block, seats beyond the cap) are REPORTED, never adjusted.
/// </summary>
public static partial class ElevationCheck
{
    private const double SubTol = 0.0005; // m, chain vs generated substructure

    /// <summary>The unit and pedestal that carry the seat at plan point <paramref name="seatXY"/>: the own unit at the support first, then
    /// the units of the other bridges (a shared pier is built by its owner bridge). Matched by the seat position (S, N within 3 mm).</summary>
    internal static (SubstructureUnit unit, PedestalInfo ped)? FindPedestal(SubstructureModel? sub, IEnumerable<SubstructureModel>? others, int support,
        int girder, Vec2 seatXY)
    {
        static PedestalInfo? Match(SubstructureUnit unit, Vec2 p, int girder, bool byGirder)
        {
            double s = Vec2.Dot(p - unit.Origin, unit.D), n = Vec2.Dot(p - unit.Origin, unit.N);
            return unit.Pedestals.Where(q => Math.Abs(q.RowS - s) < 0.003 && (double.IsNaN(q.SeatN) || Math.Abs(q.RowN - n) < 0.003)
                    && (!byGirder || q.Girder == girder))
                .OrderBy(q => Math.Abs(q.RowN - n)).FirstOrDefault();
        }
        if (sub?.Units.FirstOrDefault(x => x.SupportIndex == support) is { } own)
        {
            if (Match(own, seatXY, girder, true) is { } p) return (own, p);
            if (Match(own, seatXY, girder, false) is { } p2) return (own, p2);
        }
        foreach (var m in others ?? Enumerable.Empty<SubstructureModel>())
            foreach (var unit in m.Units)
                if (Match(unit, seatXY, girder, false) is { } p) return (unit, p);
        // Legacy fallback: the own unit's pedestal of this girder nearest to the seat.
        if (sub?.Units.FirstOrDefault(x => x.SupportIndex == support) is { } u0)
        {
            double n0 = Vec2.Dot(seatXY - u0.Origin, u0.N);
            if (u0.Pedestals.Where(q => q.Girder == girder).OrderBy(q => Math.Abs(q.RowN - n0)).FirstOrDefault() is { } p) return (u0, p);
        }
        return null;
    }

    /// <summary>Upper (or lower) crossing of a closed (S, elevation) polygon with the vertical at s, as the generator's envelope,
    /// with the edge that gives it (for the basis text). NaN when s is outside the polygon.</summary>
    private static (double z, CapProfilePoint a, CapProfilePoint b) Crossing(List<CapProfilePoint> poly, double s, bool upper)
    {
        double best = double.NaN;
        CapProfilePoint ea = poly[0], eb = poly[0];
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            if (s < Math.Min(a.S, b.S) || s > Math.Max(a.S, b.S)) continue;
            double z = Math.Abs(b.S - a.S) < 1e-9 ? (upper ? Math.Max(a.Elevation, b.Elevation) : Math.Min(a.Elevation, b.Elevation))
                : a.Elevation + (b.Elevation - a.Elevation) * (s - a.S) / (b.S - a.S);
            if (double.IsNaN(best) || (upper ? z > best : z < best)) { best = z; ea = a; eb = b; }
        }
        return (best, ea, eb);
    }

    private static string EdgeText(CapProfilePoint a, CapProfilePoint b, double s, UnitSystem u)
    {
        string E(double z) => u.FormatLength(z, LengthKind.Coordinate);
        string L(double x) => u.FormatLength(x);
        if (Math.Abs(b.S - a.S) < 1e-9) return $"vertical edge at S {L(a.S)} ({E(a.Elevation)} / {E(b.Elevation)})";
        if (Math.Abs(b.Elevation - a.Elevation) < 1e-9) return $"level edge {E(a.Elevation)} from S {L(Math.Min(a.S, b.S))} to {L(Math.Max(a.S, b.S))}";
        var (p, q) = a.S < b.S ? (a, b) : (b, a);
        return $"edge (S {L(p.S)}, {E(p.Elevation)}) to (S {L(q.S)}, {E(q.Elevation)}): {E(p.Elevation)} {(q.Elevation >= p.Elevation ? "+" : "−")} " +
               $"{u.FormatLength(Math.Abs(q.Elevation - p.Elevation), LengthKind.Short)} × {L(s - p.S)} / {L(q.S - p.S)}";
    }

    /// <summary>Cap top at (s, n) recomputed from the inputs, with its source. Parametric caps: the generated top line (its rule is stated).</summary>
    private static (double z, string how) CapTopFromInputs(SubstructureUnit unit, double s, double n, UnitSystem u, List<string> reported)
    {
        var sd = unit.Definition;
        string Sh(double m) => u.FormatLength(m, LengthKind.Short);
        if (sd.CapProfile.Count < 3 || unit.Abutment != null)
            return (unit.CapTopAt(s, n), $"{unit.CapTopDescription} (parametric; set so the shortest pedestal is {Sh(sd.MinPedestalHeight)})");
        bool ahead = sd.CapProfileAhead.Count >= 3 && n > unit.CapMidN;
        var poly = ahead ? sd.CapProfileAhead : sd.CapProfile;
        string which = ahead
            ? $"CapProfileAhead (stepped cap: the seat is {Sh(n - unit.CapMidN)} up-station of the CL pier, on the ahead half)"
            : sd.CapProfileAhead.Count >= 3 ? $"CapProfile (stepped cap: the seat is {Sh(unit.CapMidN - n)} back of the CL pier, on the back half)" : "CapProfile";
        double lo = poly.Min(p => p.S), hi = poly.Max(p => p.S);
        if (s < lo || s > hi)
        {
            reported.Add($"Seat at S {u.FormatLength(s)} is beyond the as-designed cap (profile S {u.FormatLength(lo)} to {u.FormatLength(hi)}): the generator extrapolates the top line there - reported, not adjusted.");
            return (unit.CapTopAt(s, n), $"{which}: seat beyond the polygon, top line extrapolated by the generator");
        }
        var (z, a, b) = Crossing(poly, s, true);
        return (z, $"as designed, {which} (plan pier elevation) upper envelope at S {u.FormatLength(s)}: {EdgeText(a, b, s, u)}");
    }

    /// <summary>Cap soffit at s recomputed from the inputs (the generator hangs the columns off the MAIN profile's lower envelope).</summary>
    private static (double z, string how) SoffitFromInputs(SubstructureUnit unit, double s, UnitSystem u)
    {
        var sd = unit.Definition;
        string Sh(double m) => u.FormatLength(m, LengthKind.Short);
        if (sd.CapProfile.Count < 3 || unit.Abutment != null)
            return (unit.CapBottomAt(s), sd.BottomShape == CapBottomShape.Level ? $"level bottom {Sh(sd.CapDepth)} below the lowest cap top" : $"bottom parallel to the top, depth {Sh(sd.CapDepth)}");
        double lo = sd.CapProfile.Min(p => p.S), hi = sd.CapProfile.Max(p => p.S);
        if (s < lo || s > hi) return (unit.CapBottomAt(s), "beyond the as-designed cap: soffit line extrapolated by the generator");
        var (z, a, b) = Crossing(sd.CapProfile, s, false);
        return (z, $"as designed, CapProfile lower envelope at S {u.FormatLength(s)}: {EdgeText(a, b, s, u)}" +
                   (sd.CapProfileAhead.Count >= 3 ? " (the soffit comes from the main profile on both halves)" : ""));
    }

    /// <summary>Appends the substructure chain below the bearing seat (the chain's current final elevation).</summary>
    internal static void AddSubstructure(ElevationChain ch, SubstructureUnit unit, PedestalInfo ped, Vec2 seatXY, UnitSystem u)
    {
        string E(double z) => u.FormatLength(z, LengthKind.Coordinate);
        string Sh(double m) => u.FormatLength(m, LengthKind.Short);
        string L(double m) => u.FormatLength(m);
        string Info(string s) => "(info) " + s;
        var sd = unit.Definition;
        double s = Vec2.Dot(seatXY - unit.Origin, unit.D), n = Vec2.Dot(seatXY - unit.Origin, unit.N);
        double seatZ = ch.Final, z = seatZ;
        var reported = new List<string>();
        void Compare(string what, double model, double chain)
        {
            if (double.IsNaN(model) || double.IsNaN(chain)) return;
            if (Math.Abs(model - chain) > SubTol) { ch.Mismatch = true; ch.Checks.Add($"{what} in the substructure model {E(model)} differs by {Sh(model - chain)}"); }
        }
        bool mismatchBefore = ch.Mismatch;
        ch.Mismatch = false;
        if (unit.Name != "" && !ch.Title.StartsWith(unit.Name)) ch.Checks.Add($"Substructure from unit {unit.Name} (shared pier built by another bridge).");

        // ---- 1. pedestal: plan size, shared block (as-designed abutment: the beam step top is a plan input)
        if (unit.Abutment is { } abS && abS.Def.Steps.FirstOrDefault(x => x.Girder == ped.Girder + 1) is { } step)
        {
            double top = step.TopElevation;
            ch.Steps.Add(new CalcStep("± Beam step top (plan)", $"AbutmentDefinition step G{ped.Girder + 1} TopElevation {E(top)} (plan input; the step follows the plan, not the seat)",
                top - z, top, "Top of beam step"));
            if (Math.Abs(top - z) > UnitSystem.In(0.125))
                reported.Add($"Beam step top {E(top)} (plan) differs from the bearing seat {E(z)} by {Sh(top - z)} - reported, not adjusted.");
            z = top;
        }
        else Compare("Pedestal seat", ped.SeatZ, seatZ);
        var block = unit.Pedestals.Where(p => Math.Abs(p.S - ped.S) < 1e-6 && Math.Abs(p.N - ped.N) < 1e-6 && Math.Abs(p.LengthS - ped.LengthS) < 1e-9).ToList();
        bool planSize = unit.Abutment == null && sd.PedestalSizeAlong > 0 && sd.PedestalSizeAcross > 0;
        if (unit.Abutment != null)
            ch.Steps.Add(new CalcStep(Info("Beam step"), $"as-designed abutment beam step {Sh(ped.LengthS)} along the support × {Sh(ped.WidthN)} (AbutmentDefinition)", double.NaN, z));
        else
            ch.Steps.Add(new CalcStep(Info("Pedestal plan size"), planSize
                ? $"{Sh(ped.LengthS)} × {Sh(ped.WidthN)} = plan size (PedestalSizeAlong × PedestalSizeAcross), centred on {(block.Count > 1 ? "the mean of the carried seats' footprints" : "the bearing footprint")} at S {L(ped.S)}, N {Sh(ped.N)}"
                : $"{Sh(ped.LengthS)} × {Sh(ped.WidthN)} = bearing footprint + {Sh(sd.PedestalProjection)} each side (PedestalProjection; no plan size entered)",
                double.NaN, z));
        double pedTop = z;
        if (block.Count > 1)
        {
            double top = block.Max(p => p.SeatZ);
            string seats = string.Join(", ", block.OrderBy(p => p.RowN).Select(p => $"{(p.Girder >= 1000 ? "other bridge" : $"G{p.Girder + 1}")} at N {Sh(p.RowN)} {E(p.SeatZ)}"));
            ch.Steps.Add(new CalcStep("+ Shared pedestal block", $"one block per girder line carries {block.Count} seats ({seats}); PedestalShared: block top = the highest seat",
                top - z, top, "Top of shared pedestal block"));
            if (top - z > UnitSystem.In(0.125))
                reported.Add($"Seat {E(z)} differs from the shared pedestal top {E(top)} by {Sh(top - z)} (the bearing stacks of the carried seats differ) - reported, not adjusted.");
            if (unit.PedestalBlocks.FirstOrDefault(b => Math.Abs(b.S - ped.S) < 1e-6 && Math.Abs(b.N - ped.N) < 1e-6) is { } pb) Compare("Shared pedestal top", pb.SeatZ, top);
            z = pedTop = top;
        }

        // ---- 2. cap top under the seat
        var (capTop, how) = CapTopFromInputs(unit, s, n, u, reported);
        if (unit.Abutment is { } ag)
        {
            double baseZ = ag.SeatBaseZ(s);
            how = $"as-designed abutment: footing bottom {E(ag.FootingBottom(s))} + footing depth {Sh(ag.Def.FootingDepth)} + wash {Sh(ag.Def.WashHeight)} (step base)";
            capTop = baseZ;
        }
        double h = z - capTop;
        ch.Steps.Add(new CalcStep(unit.Abutment != null ? "− Beam step" : "− Pedestal", $"{(unit.Abutment != null ? "step top" : block.Count > 1 ? "block top" : "seat")} − cap top; cap top {how}" +
            (h < UnitSystem.In(0.125) && unit.Abutment == null ? " (no pedestal built)" : ""), -h, capTop, unit.Abutment != null ? "Top of cap under the bearing (step base)" : "Top of cap under the bearing"));
        Compare("Cap top under the seat", ped.CapTopZ, capTop);
        if (unit.Abutment == null) Compare("Cap top line", unit.CapTopAt(s, n), capTop);
        Compare(unit.Abutment != null ? "Beam step height" : "Pedestal height", ped.Height, unit.Abutment != null ? z - capTop : seatZ - capTop);
        if (unit.Abutment == null && h < -UnitSystem.In(0.25))
            reported.Add($"Seat is {Sh(-h)} BELOW the cap top - check the bearing stack and the cap elevations (reported, not adjusted).");
        if (sd.Aesthetics is { Layers.Count: > 0 } ae && unit.Abutment == null && sd.CapProfile.Count >= 3)
        {
            int side = n > unit.CapMidN ? 1 : -1;
            var zs = unit.ReliefLines.Select((l, i) => (l, tag: unit.ReliefLineTags[i]))
                .Where(x => x.tag.Side == side && x.l.Count > 1 && s >= x.l.Min(p => p.X) && s <= x.l.Max(p => p.X))
                .Select(x => SubstructureUnit.Interp(x.l.OrderBy(p => p.X).ToList(), s)).ToList();
            ch.Steps.Add(new CalcStep(Info("Cap section step"), $"cap {Sh(unit.CapN1 - unit.CapN0)} wide above the relief curve, {Sh(unit.CapN1 - unit.CapN0 - 2 * ae.StepSetback)} below " +
                $"(StepSetback {Sh(ae.StepSetback)} per face); the full-width strips are flush with the cap top, so the seat sits on the same top" +
                (zs.Count > 0 ? $"; {(side > 0 ? "ahead" : "back")} face relief line at this S: {E(zs.Min())} (full width {Sh(capTop - zs.Min())} below the top)" : "; no relief line at this S"),
                double.NaN, capTop));
        }

        // ---- 3. cap bottom under the seat
        double capBot;
        string howB;
        if (unit.Abutment is { } ab)
        {
            capBot = ab.FootingBottom(s);
            howB = $"footing depth {Sh(ab.Def.FootingDepth)} + wash {Sh(ab.Def.WashHeight)} (AbutmentDefinition)";
        }
        else (capBot, howB) = SoffitFromInputs(unit, s, u);
        ch.Steps.Add(new CalcStep(unit.Abutment != null ? "− Wash + footing depth" : "− Cap depth", $"{Sh(capTop - capBot)} under the bearing; {howB}",
            -(capTop - capBot), capBot, unit.Abutment != null ? "Bottom of footing under the bearing" : "Bottom of cap under the bearing"));
        Compare("Cap bottom under the seat", unit.CapBottomAt(s), capBot);
        z = capBot;
        double colTop = double.NaN, flareZ = double.NaN, fTop = double.NaN, fBot = double.NaN, pileHead = double.NaN;
        int colIdx = -1;

        // ---- 4. column that carries the bearing (nearest along the cap) and its foundation
        FoundationInfo? fnd = null;
        if (unit.Abutment == null && unit.Columns.Count > 0)
        {
            var col = unit.Columns.OrderBy(c => Math.Abs(c.S - s)).First();
            colIdx = col.Index;
            var (zc, howC) = SoffitFromInputs(unit, col.S, u);
            double ds = col.S - s;
            ch.Steps.Add(new CalcStep($"± Soffit to column {col.Index + 1}", $"column {col.Index + 1} at S {L(col.S)} (nearest column, {Sh(Math.Abs(ds))} {(ds >= 0 ? "right" : "left")} of the bearing); soffit {howC}",
                zc - z, zc, $"Bottom of cap at column {col.Index + 1} = top of column"));
            Compare($"Column {col.Index + 1} top", col.TopZ, zc);
            z = colTop = zc;
            fnd = unit.Foundations.OrderBy(f => Math.Abs(f.S - col.S) + Math.Abs(f.N - col.N)).FirstOrDefault();
            // Foundation top: the generator's rule.
            string howF;
            if (!double.IsNaN(sd.FoundationTopElevation)) { fTop = sd.FoundationTopElevation; howF = "FoundationTopElevation (input, plan footing top)"; }
            else if (fnd != null && !double.IsNaN(fnd.GroundZ)) { fTop = fnd.GroundZ - sd.GroundCover; howF = $"ground {E(fnd.GroundZ)} (terrain at the column) − GroundCover {Sh(sd.GroundCover)}"; }
            else { fTop = zc - UnitSystem.Ft(20); howF = "no input and no terrain: 20 ft below the cap bottom (generator default)"; }
            string size = col.Shape == ColumnShape.Round ? $"{Sh(col.SizeS)} dia" : $"{Sh(col.SizeS)} × {Sh(col.SizeN)}" +
                (sd.ColumnChamferS > 0 || sd.ColumnChamferN > 0 ? $" octagonal ({Sh(sd.ColumnChamferS)} × {Sh(sd.ColumnChamferN)} chamfers)" : "");
            string top = sd.Foundation == FoundationType.DrilledShaft ? "Top of drilled shaft" : "Top of footing";
            if (!double.IsNaN(sd.ColumnFlareElevation) && sd.ColumnFlareElevation > fTop && sd.ColumnFlareElevation < zc && sd.ColumnShape == ColumnShape.Rectangular)
            {
                flareZ = sd.ColumnFlareElevation;
                ch.Steps.Add(new CalcStep("− Column shaft", $"{size}, down to ColumnFlareElevation {E(flareZ)} (input)", flareZ - z, flareZ, "Top of flared base"));
                ch.Steps.Add(new CalcStep("− Flared base", $"{Sh(sd.ColumnFlareWidth)} × {Sh(sd.ColumnFlareDepth)} flare down to the foundation top; foundation top = {howF}", fTop - flareZ, fTop, top));
            }
            else ch.Steps.Add(new CalcStep("− Column", $"{size}, {L(zc - fTop)} high; foundation top = {howF}", fTop - z, fTop, top));
            Compare($"Column {col.Index + 1} bottom", col.BottomZ, fTop);
            z = fTop;
        }
        else if (unit.Abutment == null && unit.Foundations.Count > 0)
        {
            // Parametric abutment: foundations directly under the cap.
            fnd = unit.Foundations.OrderBy(f => Math.Abs(f.S - s)).First();
            var (zf, howC) = SoffitFromInputs(unit, fnd.S, u);
            ch.Steps.Add(new CalcStep("± Cap bottom to foundation", $"nearest foundation at S {L(fnd.S)}; cap bottom {howC}", zf - z, zf, "Top of foundation (under the cap)"));
            z = fTop = zf;
        }
        if (fnd != null)
        {
            Compare("Foundation top", fnd.TopZ, fTop);
            switch (sd.Foundation)
            {
                case FoundationType.DrilledShaft:
                    fBot = z - sd.ShaftLength;
                    ch.Steps.Add(new CalcStep("− Drilled shaft", $"{Sh(sd.ShaftDiameter)} dia, ShaftLength {L(sd.ShaftLength)}", -sd.ShaftLength, fBot, "Tip of drilled shaft"));
                    Compare("Shaft tip", fnd.BottomZ, fBot);
                    break;
                case FoundationType.SpreadFooting:
                case FoundationType.PileCap:
                    if (fnd.TopZ - fnd.BottomZ > 1e-9)
                    {
                        fBot = z - sd.FootingThickness;
                        ch.Steps.Add(new CalcStep("− Footing", $"FootingThickness {Sh(sd.FootingThickness)} ({L(fnd.SizeS)} × {L(fnd.SizeN)})", -sd.FootingThickness, fBot, "Bottom of footing"));
                        Compare("Footing bottom", fnd.BottomZ, fBot);
                        z = fBot;
                    }
                    else fBot = z;
                    if (sd.Foundation == FoundationType.PileCap && fnd.Piles.Count > 0)
                    {
                        pileHead = z + sd.PileEmbedment;
                        ch.Steps.Add(new CalcStep("+ Pile embedment", $"PileEmbedment {Sh(sd.PileEmbedment)}: pile heads above the footing bottom (cut horizontal)", sd.PileEmbedment, pileHead, "Pile head"));
                        double worst = fnd.Piles.Select(p => p.TopZ).OrderByDescending(t => Math.Abs(t - pileHead)).First();
                        Compare("Pile head", worst, pileHead);
                        var fd = colIdx >= 0 ? sd.Footings.FirstOrDefault(x => x.Column == colIdx + 1) : null;
                        double len = fd != null && !double.IsNaN(fd.PileLength) ? fd.PileLength : sd.PileLength;
                        int bat = fnd.Piles.Count(p => p.IsBattered);
                        ch.Steps.Add(new CalcStep(Info("Piles"), $"{fnd.Piles.Count} {sd.PileSection} piles, length {L(len)} ({(fd != null && !double.IsNaN(fd.PileLength) ? "footing PileLength" : "pier PileLength")}); " +
                            $"plumb tip {E(pileHead - len)}{(bat > 0 ? $"; {bat} battered 1:{fd?.BatterRatio ?? 4:0.##} (length along the axis)" : "")}", double.NaN, pileHead));
                    }
                    break;
            }
        }

        if (!ch.Mismatch) ch.Checks.Add($"Substructure chain matches the generated unit: seat {E(seatZ)}, cap top {E(capTop)}, pedestal {Sh(seatZ - capTop)}" +
            (double.IsNaN(colTop) ? "" : $", column {colIdx + 1} top {E(colTop)}") + (double.IsNaN(fTop) ? "" : $", foundation top {E(fTop)}") +
            (double.IsNaN(fBot) ? "" : $", bottom {E(fBot)}") + (double.IsNaN(pileHead) ? "" : $", pile head {E(pileHead)}") + ".");
        ch.Mismatch |= mismatchBefore;
        foreach (var r in reported) { ch.Reported.Add(r); ch.Checks.Add("Reported: " + r); }
        ch.Substructure = new SubstructureChainValues(unit.Name, colIdx, s, n, seatZ, pedTop, capTop, capBot, colTop, flareZ, fTop, fBot, pileHead);
    }
}
