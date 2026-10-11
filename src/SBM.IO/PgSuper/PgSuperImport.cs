using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.IO.PgSuper;

/// <summary>Result of applying a PGSuper bridge to an alignment.</summary>
public sealed record PgSuperImportResult(BridgeDefinition Bridge, List<string> Notes, List<string> Warnings);

/// <summary>
/// Applies a PGSuper bridge description to one of this project's alignments: spans (pier station differences) start at
/// the chosen begin station, so the PGSuper alignment itself is not needed. Mapped:
/// <list type="bullet">
/// <item>Piers → supports (first/last = abutments, station moved to the CL bearing; interior = CL pier) with skews.</item>
/// <item>Girder count, spacing (along-pier spacing converted to normal), reference girder offset → G1 offset.</item>
/// <item>Deck edges, slab and overhang thickness; "A" dimension − slab = haunch (top of girder to bottom of slab).</item>
/// <item>Girder: Tx28–Tx70 by name, otherwise the library's Precast I-Beam dimensions as a custom section.</item>
/// <item>Strands of the governing girder: library grid filled in PGSuper's order (global order for "total" input,
/// straight/harped grids otherwise), harped strands adjusted at the ends and harping points per the offset inputs;
/// harping points per the library location; strand size from the material key; f'c, f'ci.</item>
/// <item>Bearing / end distances and the bearing pad size.</item>
/// </list>
/// </summary>
public static class PgSuperImport
{
    public static PgSuperImportResult Apply(PgsBridge pg, Alignment alignment, double beginStation, string bridgeName, UnitSystem u)
    {
        var notes = new List<string>();
        var warnings = new List<string>(pg.Warnings);
        string L(double m) => u.FormatLength(m);
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        var def = new BridgeDefinition { Name = bridgeName, AlignmentName = alignment.Name };
        def.FieldSplices.Clear();

        // ---- supports
        double s0 = pg.Piers[0].Station;
        int n = pg.Piers.Count;
        for (int k = 0; k < n; k++)
        {
            var p = pg.Piers[k];
            bool abut = k == 0 || k == n - 1;
            double sta = beginStation + (p.Station - s0);
            double skew = p.SkewDeg;
            if (double.IsNaN(skew))
            {
                // Bearing orientation: skew = angle from the alignment normal.
                double brg = ParseBearing(p.Orientation);
                var (_, ang) = alignment.EvaluateAtDistance(alignment.StationToDistance(sta));
                double normal = ang - Math.PI / 2;
                double d = Numerics.RadToDeg(Math.IEEERemainder(brg - normal, Math.PI));
                skew = d;
                notes.Add($"Pier {k + 1}: orientation '{p.Orientation}' → skew {skew:0.###}° on this alignment.");
            }
            // Abutments: PGSuper stations the abutment line; SBM stations the CL bearing (offset normal to the pier).
            double cos = Math.Cos(Numerics.DegToRad(skew));
            if (k == 0) sta += p.AheadBearingOffset / cos;
            if (k == n - 1) sta -= p.BackBearingOffset / cos;
            def.Supports.Add(new SupportDefinition
            {
                Name = abut ? $"Abut {(k == 0 ? 1 : 2)}" : $"Bent {k + 1}",
                Type = abut ? SupportType.Abutment : SupportType.Pier,
                Station = sta, SkewDegrees = Math.Round(skew, 6),
                GirderEndExtension = abut ? (k == 0 ? p.AheadEndDistance : p.BackEndDistance) : In(9),
            });
        }
        notes.Add($"{n} supports, {n - 1} span(s): " + string.Join(", ", Enumerable.Range(0, n - 1).Select(k => L(pg.Piers[k + 1].Station - pg.Piers[k].Station))) +
                  $" (pier to pier), starting at STA {u.FormatStation(beginStation)}.");

        if (pg.Girders.Count == 0)
            warnings.Add($"No girder data found (PGSuper {pg.Version} file layout) — girder count and strands were not read. Open and save the file in current PGSuper/BridgeLink, then import again.");

        // ---- girders
        var g = def.Girders;
        g.Kind = GirderKind.TxDotPrestressed;
        g.PlanLayout = GirderPlanLayout.Chorded;
        g.Count = Math.Max(1, pg.GirderCount);
        double spacing = pg.Spacings.FirstOrDefault();
        if (pg.SpacingMeasureType == 1)
        {
            double c = Math.Cos(Numerics.DegToRad(pg.Piers.Select(p => double.IsNaN(p.SkewDeg) ? 0 : p.SkewDeg).FirstOrDefault()));
            notes.Add($"Spacing {L(spacing)} along the pier → {L(spacing * c)} normal to the alignment.");
            spacing *= c;
        }
        g.Spacings = Enumerable.Repeat(spacing, Math.Max(0, g.Count - 1)).ToList();
        double refOff = pg.RefGirderOffset + (pg.RefGirderOffsetType == 1 ? pg.AlignmentOffset : 0);
        // Reference girder −1 = center of the girder group; otherwise index of the reference girder.
        double refIndex = pg.RefGirder < 0 ? (g.Count - 1) / 2.0 : pg.RefGirder;
        g.FirstGirderOffset = refOff - refIndex * spacing;
        notes.Add($"{g.Count} girders at {L(spacing)}, G1 at {L(g.FirstGirderOffset)} from the alignment.");

        // ---- section
        var ps = g.Prestressed;
        if (TxGirderCatalog.ParseTx(pg.GirderName) is { } tx) { ps.Type = tx; notes.Add($"Girder {pg.GirderName} → TxDOT {tx} (IGD)."); }
        else if (pg.Dims != null)
        {
            ps.Type = TxGirderType.Custom;
            ps.CustomSection = pg.Dims;
            var shp = TxGirderShape.FromDims(pg.Dims);
            notes.Add($"Girder {pg.GirderName}: custom precast I-beam from the PGSuper library ({S(shp.Depth)} deep, {S(shp.TopWidth)} top flange, A = {shp.Area:0.#} in²).");
            ps.InteriorEnds = InteriorGirderEnds.Skewed;
        }
        else throw new InvalidDataException($"Girder '{pg.GirderName}' ({pg.SectionName}) is not a precast I-beam; it can't be modeled yet.");
        var shape = TxGirderCatalog.Get(ps);
        // PGSuper girder ends are parallel to the pier line.
        ps.InteriorEnds = InteriorGirderEnds.Skewed;

        // ---- ends and bearings (interior pier: CL pier → CL bearing, CL bearing → end of girder)
        var inner = n > 2 ? pg.Piers[1] : null;
        double endDist = pg.Piers[0].AheadEndDistance;
        ps.BearingToEnd = endDist > 0 ? endDist : In(9);
        if (inner != null) ps.EndGapAtBent = Math.Max(0, inner.AheadBearingOffset - inner.AheadEndDistance);
        if (pg.PadLength > 0 && pg.PadWidth > 0)
        {
            ps.PadLength = pg.PadLength;
            ps.PadWidth = pg.PadWidth;
            ps.PadThickness = pg.PadHeight > 0 ? pg.PadHeight : In(2.75);
        }
        notes.Add($"End of girder {S(ps.BearingToEnd)} from the CL bearing" + (inner != null ? $"; {S(ps.EndGapAtBent)} from the CL pier at interior piers" : "") +
                  (ps.PadLength > 0 ? $"; pads {S(ps.PadLength)} x {S(ps.PadWidth)} x {S(ps.PadThickness)}." : "."));

        // ---- strands (governing girder = the first interior girder; differences reported)
        var gd = pg.Girders.Count > 2 ? pg.Girders[1] : pg.Girders.FirstOrDefault();
        if (gd != null)
        {
            var diff = pg.Girders.Where(x => x.Key != gd.Key).Select(x => $"G{x.Index + 1}").ToList();
            if (diff.Count > 0) warnings.Add($"Girders {string.Join(", ", diff)} have different strands or concrete in PGSuper; G{gd.Index + 1}'s are used for all girders.");
            ps.StrandDiameter = gd.StrandDiameter;
            if (gd.FcKsi > 0) ps.ConcreteStrength = Math.Round(gd.FcKsi, 3);
            if (gd.FciKsi > 0) ps.ReleaseStrength = Math.Round(gd.FciKsi, 3);
            var layout = StrandLayout(pg, gd, shape, warnings);
            if (layout != null)
            {
                ps.StrandLayout = layout;
                ps.StrandCount = layout.Count;
                ps.DepressedStrands = layout.Count(x => x.Harped);
                if (layout.Count == 0) warnings.Add("The PGSuper girders have no strands yet (not designed); the beams are modeled without strands.");
            }
            if (gd.DebondedStraight > 0 && layout != null)
            {
                int nd = layout.Count(x => x.DebondStart > 0 || x.DebondEnd > 0);
                notes.Add($"{gd.Debond.Count} PGSuper debond entries: {nd} straight strands debonded (sheathing modeled).");
                if (gd.Debond.Count < gd.DebondedStraight)
                    warnings.Add($"PGSuper lists {gd.DebondedStraight} debonded strand entries but only {gd.Debond.Count} DebondInfo records were read; the others are modeled bonded.");
            }
            notes.Add($"Strands: {layout?.Count ?? 0} – {TxGirderCatalog.StrandSize(gd.StrandDiameter, u)} ({gd.NumStraight} straight, {gd.NumHarped} harped), f'c {ps.ConcreteStrength:0.0#} ksi, f'ci {ps.ReleaseStrength:0.0#} ksi.");
        }
        // Harping points: library location (fraction of span from the girder CL), else TxDOT L/20.
        if (pg.HarpPointMeasure == 0 && pg.HarpPointReference == 2) ps.HarpPointFraction = pg.HarpPointLocation;
        else if (pg.HarpPointMeasure == 2) warnings.Add("Harping points are given as an absolute distance in PGSuper; L/20 each side of midspan is used.");

        // ---- deck
        var deck = def.Deck;
        double shift = pg.DeckEdgeMeasureType == 1 ? pg.AlignmentOffset : 0;
        if (pg.LeftEdge > 0 || pg.RightEdge > 0)
        {
            deck.LeftEdgeOffset = shift - pg.LeftEdge;
            deck.RightEdgeOffset = shift + pg.RightEdge;
        }
        if (pg.SlabThickness > 0) deck.Thickness = pg.SlabThickness;
        if (pg.OverhangDepth > 0) deck.OverhangThickness = pg.OverhangDepth;
        if (pg.SlabOffset > 0)
        {
            deck.Haunch = Math.Max(0, pg.SlabOffset - deck.Thickness);
            notes.Add($"\"A\" dimension {S(pg.SlabOffset)} − slab {S(deck.Thickness)} = haunch {S(deck.Haunch)} (top of girder to bottom of slab, set at the supports; PGSuper's camber is not modeled).");
        }
        string rails = (pg.LeftRail + " " + pg.RightRail).ToUpperInvariant();
        deck.Barrier.Type = rails.Contains("SSTR") ? BarrierType.TxDotSstr : rails.Contains("SINGLE SLOPE") ? BarrierType.SingleSlope
            : rails.Contains("F-SHAPE") || rails.Contains("F SHAPE") ? BarrierType.FShape : deck.Barrier.Type;
        if (!string.IsNullOrWhiteSpace(pg.LeftRail)) notes.Add($"Railing '{pg.LeftRail}' → {deck.Barrier.Type}.");

        foreach (var e in def.Validate()) warnings.Add(e);
        return new PgSuperImportResult(def, notes, warnings);
    }

    /// <summary>Strand positions for one girder: PGSuper's fill of the library grid, with the harped offsets applied.</summary>
    public static List<StrandPoint>? StrandLayout(PgsBridge pg, PgsGirderData gd, TxGirderShape shape, List<string> warnings)
    {
        // Grid entries in PGSuper's fill order; an entry is a pair when any of its X values is non-zero.
        var straight = new List<(PgsGridPoint p, int n)>();
        int GridIndex(PgsGridPoint p) { for (int k = 0; k < pg.StraightGrid.Count; k++) if (ReferenceEquals(pg.StraightGrid[k], p)) return k; return -1; }
        var harped = new List<(PgsGridPoint p, int n)>();
        bool Pair(PgsGridPoint p, bool h) => (h ? Math.Max(p.X, Math.Max(p.Xhp, p.Xend)) : p.X) > 1e-6;
        void Fill(IEnumerable<(bool h, PgsGridPoint p)> order, int nStraight, int nHarped, int nTotal)
        {
            int ns = 0, nh = 0;
            foreach (var (h, p) in order)
            {
                int c = Pair(p, h) ? 2 : 1;
                int have = ns + nh;
                int room = nTotal >= 0 ? nTotal - have : h ? nHarped - nh : nStraight - ns;
                if (room <= 0) continue;
                // Odd harped count: the last pair position takes a single strand at the CL.
                if (c == 2 && room == 1 && h && pg.OddHarped) c = 1;
                if (c > room) continue;
                (h ? harped : straight).Add((p, c));
                if (h) nh += c; else ns += c;
            }
        }
        switch (gd.StrandInputType)
        {
            case 0: // total permanent strands, global fill order
                Fill(pg.GlobalOrder.Where(o => o.Index >= 0 && o.Index < (o.Harped ? pg.HarpedGrid : pg.StraightGrid).Count)
                    .Select(o => (o.Harped, (o.Harped ? pg.HarpedGrid : pg.StraightGrid)[o.Index])), 0, 0, gd.NumPermanent);
                break;
            case 1: // straight + harped counts, each grid in its own order
                Fill(pg.StraightGrid.Select(p => (false, p)), gd.NumStraight, 0, -1);
                Fill(pg.HarpedGrid.Select(p => (true, p)), 0, gd.NumHarped, -1);
                break;
            default:
                warnings.Add("Strands are entered by direct selection / rows in PGSuper; the IGND fill is used instead.");
                return null;
        }
        int got = straight.Sum(x => x.n) + harped.Sum(x => x.n);
        int want = gd.StrandInputType == 0 ? gd.NumPermanent : gd.NumStraight + gd.NumHarped;
        if (got != want) warnings.Add($"Only {got} of {want} strands fit the library grid in PGSuper's fill order.");

        var pts = new List<StrandPoint>();
        foreach (var (p, c) in straight)
        {
            // Debonding of this grid entry (both strands of a pair).
            int gi = GridIndex(p);
            var db = gd.Debond.FirstOrDefault(d => d.GridIndex == gi);
            foreach (double x in c == 2 ? new[] { -p.X, p.X } : new[] { 0.0 })
                pts.Add(new StrandPoint { X = x, Y = p.Y, YEnd = p.Y, DebondStart = Math.Max(0, db.Start), DebondEnd = Math.Max(0, db.End) });
        }
        foreach (var d in gd.Debond.Where(d => !straight.Any(s => GridIndex(s.p) == d.GridIndex)))
            warnings.Add($"PGSuper debonds straight grid position {d.GridIndex}, which is not filled; ignored.");
        if (harped.Count > 0)
        {
            var hp = new List<(double x, double y, double xe, double ye)>();
            foreach (var (p, c) in harped)
                if (c == 2) { hp.Add((-p.Xhp, p.Yhp, -p.Xend, p.Yend)); hp.Add((p.Xhp, p.Yhp, p.Xend, p.Yend)); }
                else hp.Add((0, p.Yhp, 0, p.Yend));
            double H = shape.Depth;
            // Shift the harped group vertically (HarpedStrandOffsetType): 0 CG from top, 1 CG from bottom, 2 top strand
            // to top, 3 top strand to bottom, 4 bottom strand to bottom, 5 eccentricity (not applied), −1 legacy (not applied).
            double Shift(IReadOnlyList<double> ys, int measure, double offset)
            {
                if (ys.Count == 0) return 0;
                double cg = ys.Average(), top = ys.Max(), bot = ys.Min();
                return measure switch
                {
                    0 => H - offset - cg,
                    1 => offset - cg,
                    2 => H - offset - top,
                    3 => offset - top,
                    4 => offset - bot,
                    _ => 0,
                };
            }
            double dEnd = Shift(hp.Select(q => q.ye).ToList(), gd.EndMeasure, gd.EndOffset);
            double dHp = Shift(hp.Select(q => q.y).ToList(), gd.HpMeasure, gd.HpOffset);
            if (gd.EndMeasure is 5 or -1 || gd.HpMeasure is 5 or -1)
                warnings.Add("A harped strand offset is given as an eccentricity / legacy value; the library grid position is used there.");
            foreach (var (x, y, xe, ye) in hp)
                pts.Add(new StrandPoint { X = x, Y = Math.Clamp(y + dHp, 0, H), XEnd = xe, YEnd = Math.Clamp(ye + dEnd, 0, H), Harped = true });
        }
        return pts;
    }

    /// <summary>"N 45 30 00 E" → azimuth angle in radians measured counter-clockwise from east (the alignment's convention).</summary>
    public static double ParseBearing(string s)
    {
        var p = s.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nums = p.Skip(1).Take(p.Length - 2).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        double deg = (nums.Length > 0 ? nums[0] : 0) + (nums.Length > 1 ? nums[1] / 60 : 0) + (nums.Length > 2 ? nums[2] / 3600 : 0);
        // Quadrant bearing → azimuth from north (clockwise) → math angle.
        double az = (p[0], p[^1]) switch { ("N", "E") => deg, ("S", "E") => 180 - deg, ("S", "W") => 180 + deg, _ => 360 - deg };
        return Numerics.DegToRad(90 - az);
    }
}
