using System.Globalization;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Core.Checks;

/// <summary>One line of a transparent elevation calculation.</summary>
/// <param name="Item">What this line is (e.g. "− Haunch").</param>
/// <param name="Basis">How the value was obtained (inputs and formula).</param>
/// <param name="Change">Change in elevation (NaN for the starting value).</param>
/// <param name="Elevation">Running elevation after this line.</param>
/// <param name="Result">Name of the elevation reached (e.g. "Top of web"), empty for intermediate lines.</param>
public sealed record CalcStep(string Item, string Basis, double Change, double Elevation, string Result = "");

public sealed class ElevationChain
{
    public required string Title { get; init; }
    public required double Station { get; init; }
    public required double Offset { get; init; }
    public List<CalcStep> Steps { get; } = new();
    /// <summary>Comparison with the generated model (e.g. "matches 3D model to 0.001 ft").</summary>
    public List<string> Checks { get; } = new();
    public bool Mismatch { get; set; }
    /// <summary>Differences between inputs found along the chain (e.g. seats that differ on a shared pedestal block, a seat beyond the
    /// as-designed cap). Reported only: the model is never adjusted. Also listed in <see cref="Checks"/> ("Reported: ...").</summary>
    public List<string> Reported { get; } = new();
    /// <summary>The substructure part of the chain as values (null = no substructure unit under this bearing).</summary>
    public SubstructureChainValues? Substructure { get; set; }
    public double Final => Steps.Count > 0 ? Steps[^1].Elevation : double.NaN;
}

/// <summary>
/// Builds hand-checkable elevation chains: profile grade → cross slope → top of deck → slab → haunch (top of web) →
/// web → bottom flange (bottom of steel / top of sole plate) → sole plate → bearing → bearing seat. Every value is
/// recomputed from inputs with the formula shown, then compared with the generated framing and 3D steel.
/// </summary>
public static partial class ElevationCheck
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Length in decimal inches or mm, for small values that fractional rounding would hide.</summary>
    private static string Dec(double m, UnitSystem u) =>
        u.IsUS ? (m / 0.0254).ToString("0.000", Inv) + "\"" : (m * 1000).ToString("0.0", Inv) + " mm";

    private static string Pct(double s) => (s * 100).ToString("+0.000;-0.000;0.000", Inv) + "%";

    /// <summary>Finished grade at a station/offset with the profile and cross-slope math shown.</summary>
    public static ElevationChain Grade(Alignment a, double station, double offset, UnitSystem u, string title, DeckDefinition? deck = null)
    {
        var ch = new ElevationChain { Title = title, Station = station, Offset = offset };
        AddGrade(ch, a, station, offset, u, deck);
        return ch;
    }

    private static double AddGrade(ElevationChain ch, Alignment a, double station, double offset, UnitSystem u, DeckDefinition? deck = null)
    {
        string E(double z) => u.FormatLength(z, LengthKind.Coordinate);
        var (pgl, how) = a.Profile.Explain(station, s => u.FormatStation(s), E, x => u.FormatLength(x));
        var cs = a.CrossSlope;
        double pivOff = cs.PivotOffsetAt(station);
        string pivHow = cs.PivotSource(station, s => u.FormatStation(s), x => u.FormatLength(x));
        string pglAt = pivHow.Length > 0
            ? $" — on the PGL {u.FormatLength(Math.Abs(pivOff))} {(pivOff < 0 ? "LT" : "RT")} of the alignment ({pivHow}); profile stations = alignment stations"
            : "";
        ch.Steps.Add(new CalcStep($"Profile grade at STA {u.FormatStation(station)}", how + pglAt, double.NaN, pgl, "Profile grade (PGL)"));

        var (l, r) = cs.SlopesAt(station);
        double d = offset - pivOff;
        double slope = d >= 0 ? r : l;
        double dz = cs.DeltaElevation(station, offset);
        string side = d >= 0 ? "right" : "left";
        string rows = CrossSlopeSource(cs, station, u);
        string pivot = pivHow.Length > 0 ? $" from pivot at {u.FormatLength(pivOff)}" : "";
        var sd = d >= 0 ? cs.SidesAt(station).right : cs.SidesAt(station).left;
        string slopeText = sd.Break > 0 && Math.Abs(d) > sd.Break
            ? $"{side} slope {string.Join(" + ", sd.Segments(Math.Abs(d)).Select(g => $"{Pct(g.Slope)} × {u.FormatLength(g.To - g.From)}"))} (breaks at {string.Join(", ", sd.Segments(Math.Abs(d)).Skip(1).Select(g => u.FormatLength(g.From)))}; {rows}){pivot}, offset {u.FormatLength(offset)} {(offset < 0 ? "LT" : "RT")}"
            : $"{side} slope {Pct(slope)} ({rows}) × {u.FormatLength(Math.Abs(d))}{pivot} (offset {u.FormatLength(offset)} {(offset < 0 ? "LT" : "RT")})";
        ch.Steps.Add(new CalcStep($"Cross slope × offset",
            slopeText + (d < 0 ? " — slope applies away from the pivot" : ""),
            dz, pgl + dz, "Top of deck (finished grade)"));
        double result = pgl + dz, levelAt;
        double model = deck != null ? deck.FinishedElevation(a, station, offset) : a.ElevationAt(station, offset);
        if (deck != null && deck.EdgeStrip is { } es && !double.IsNaN(levelAt = LevelOffset(deck, a, station, offset)))
        {
            double zg = a.ElevationAt(station, levelAt);
            ch.Steps.Add(new CalcStep("Edge strip level",
                $"offset {u.FormatLength(offset)} is outside the gutter line {u.FormatLength(levelAt)} (deck edge {(offset < 0 ? "LT" : "RT")} inboard {u.FormatLength(es.Width, LengthKind.Short)}) where the surface falls toward the barrier: " +
                $"deck placed level at the gutter elevation (plan note, typical section) instead of continuing the cross slope",
                zg - result, zg, "Top of deck (finished grade, level strip)"));
            result = zg;
        }
        if (Math.Abs(model - result) > 1e-6) { ch.Mismatch = true; ch.Checks.Add($"Finished grade differs from the alignment engine by {u.FormatLength(model - result, LengthKind.Short)}"); }
        return result;
    }

    private static double LevelOffset(DeckDefinition deck, Alignment a, double station, double offset)
    {
        deck.FinishedElevation(a, station, offset, out double levelAt);
        return levelAt;
    }

    private static string CrossSlopeSource(CrossSlopeTable cs, double station, UnitSystem u)
    {
        var rows = cs.Rows;
        if (rows.Count == 0) return "no cross slope";
        if (station <= rows[0].Station) return $"held from {u.FormatStation(rows[0].Station)}";
        if (station >= rows[^1].Station) return $"held from {u.FormatStation(rows[^1].Station)}";
        int i = 1;
        while (rows[i].Station < station) i++;
        var a = rows[i - 1];
        var b = rows[i];
        return $"interpolated {u.FormatStation(a.Station)} (L {Pct(a.LeftSlope)}, R {Pct(a.RightSlope)}) to {u.FormatStation(b.Station)} (L {Pct(b.LeftSlope)}, R {Pct(b.RightSlope)})";
    }

    /// <summary>Deck slab, haunch, web and bottom flange below top of deck at a girder. Returns (top of web, bottom of steel).</summary>
    private static (double topOfWeb, double bottomOfSteel) AddGirderSection(ElevationChain ch, BridgeDefinition def, double z, double station, UnitSystem u,
        GirderResult? g = null, double s = double.NaN)
    {
        string In(double m) => u.FormatLength(m, LengthKind.Short);
        double slab = def.Deck.Thickness, haunch = def.Deck.Haunch, web = def.Girders.Section.WebDepth;
        var sec = def.Girders.Section;
        // The girder's resolved plate runs when known (as-designed layouts), else the station-based plate changes.
        PlateSegment Seg(GirderPlate pl) => g?.PlateAt(pl, s) is { } r && !double.IsNaN(s)
            ? new PlateSegment { Plate = pl, Width = r.Width, Thickness = r.Thickness } : sec.SegmentAt(pl, station);
        var tf = Seg(GirderPlate.TopFlange);
        var bf = Seg(GirderPlate.BottomFlange);
        z -= slab;
        ch.Steps.Add(new CalcStep("− Deck slab", $"slab thickness {In(slab)}", -slab, z, "Bottom of deck"));
        z -= haunch;
        ch.Steps.Add(new CalcStep("− Haunch", $"haunch {In(haunch)} (top of web to bottom of deck, at girder CL)", -haunch, z, "Top of web"));
        double topOfWeb = z;
        ch.Steps.Add(new CalcStep("(info) + Top flange", $"top flange {u.FormatLength(tf.Width, LengthKind.Short)} × {In(tf.Thickness)} — affects top of steel and stud length only, not the chain",
            double.NaN, topOfWeb + tf.Thickness, "Top of steel"));
        z -= web;
        ch.Steps.Add(new CalcStep("− Web depth", $"web {In(web)}", -web, z, "Top of bottom flange"));
        z -= bf.Thickness;
        ch.Steps.Add(new CalcStep("− Bottom flange", $"bottom flange {u.FormatLength(bf.Width, LengthKind.Short)} × {In(bf.Thickness)} at this station", -bf.Thickness, z,
            "Bottom of steel"));
        return (topOfWeb, z);
    }

    /// <summary>Chain at a girder web centerline at any station (finished grade down to bottom of steel).</summary>
    public static ElevationChain GirderAt(BridgeFraming f, int girder, double station, UnitSystem u)
    {
        var g = f.Girders[girder];
        var path = g.Path;
        if (path.Count < 2) throw new ArgumentException("Girder has no geometry.");
        double lo = Math.Min(path.Station[0], path.Station[^1]), hi = Math.Max(path.Station[0], path.Station[^1]);
        if (station < lo - 1e-6 || station > hi + 1e-6)
            throw new ArgumentException($"{g.Name} runs from {u.FormatStation(lo)} to {u.FormatStation(hi)}.");
        // Station along the path is monotonic: bisect for the distance s.
        double s0 = 0, s1 = path.Length;
        bool inc = path.Station[^1] >= path.Station[0];
        for (int it = 0; it < 60; it++)
        {
            double sm = (s0 + s1) / 2;
            if ((path.At(sm).station < station) == inc) s0 = sm; else s1 = sm;
        }
        var (p, _, _) = path.At((s0 + s1) / 2);
        if (f.Definition.Girders.IsPrestressed)
        {
            var pch = PsGirderAt(f, girder, p.XY, $"{g.Name} CL at STA {u.FormatStation(station)}", u, out double top);
            double d = p.Z - top;
            string dIn = (d / 0.0254).ToString("+0.000;-0.000;0.000", Inv) + " in";
            if (Math.Abs(d) > 0.0016) { pch.Mismatch = true; pch.Checks.Add($"Model top of beam {u.FormatLength(p.Z, LengthKind.Coordinate)} differs by {dIn}"); }
            else pch.Checks.Add($"Top of beam matches the 3D girder line ({u.FormatLength(p.Z, LengthKind.Coordinate)}, difference {dIn}).");
            return pch;
        }
        var so = f.Alignment.Inverse(p.XY);
        var ch = new ElevationChain { Title = $"{g.Name} web centerline at STA {u.FormatStation(station)}", Station = so.Station, Offset = so.Offset };
        double z = AddGrade(ch, f.Alignment, so.Station, so.Offset, u, f.Definition.Deck);
        var (topOfWeb, _) = AddGirderSection(ch, f.Definition, z, so.Station, u, g, (s0 + s1) / 2);
        // The 3D girder is sampled along its length and interpolated linearly, so between samples it can sit a few
        // hundredths of an inch off the exact surface; 1/16" is the tolerance for a match.
        double diff = p.Z - topOfWeb;
        string diffIn = (diff / 0.0254).ToString("+0.000;-0.000;0.000", Inv) + " in";
        if (Math.Abs(diff) > 0.0016) { ch.Mismatch = true; ch.Checks.Add($"Model top of web {u.FormatLength(p.Z, LengthKind.Coordinate)} differs by {diffIn}"); }
        else ch.Checks.Add($"Top of web matches the 3D girder ({u.FormatLength(p.Z, LengthKind.Coordinate)}, difference {diffIn}).");
        return ch;
    }

    /// <summary>Full chain for one bearing (support index, girder index).</summary>
    public static ElevationChain Bearing(BridgeFraming f, SteelModel? steel, int support, int girder, UnitSystem u, Substructure.SubstructureModel? sub = null,
        IEnumerable<Substructure.SubstructureModel>? others = null)
    {
        var def = f.Definition;
        var a = f.Alignment;
        var br = f.SupportLine(support).Bearings[girder] ?? throw new ArgumentException("No bearing at that location.");
        string E(double z) => u.FormatLength(z, LengthKind.Coordinate);
        string In(double m) => u.FormatLength(m, LengthKind.Short);
        var ch = new ElevationChain
        {
            Title = $"{f.SupportLine(support).Name} — G{girder + 1} bearing (CL bearing at web CL)",
            Station = br.Station, Offset = br.Offset,
        };
        double z = AddGrade(ch, a, br.Station, br.Offset, u, def.Deck);

        var gb = f.Girders[girder];
        double sb = !double.IsNaN(br.PathDistance) ? br.PathDistance
            : gb.DistanceAt(Math.Min(support, gb.Spans.Count - 1), support == gb.Spans.Count ? 1 : 0);
        var (_, bottomOfSteel) = AddGirderSection(ch, def, z, br.Station, u, gb, sb);
        ch.Steps[^1] = ch.Steps[^1] with { Result = "Bottom of steel = top of sole plate (at CL bearing)" };
        double topOfSteel = ch.Steps.First(x => x.Result == "Top of steel").Elevation;

        // Compare with framing.
        if (Math.Abs(br.BottomOfSteelElevation - bottomOfSteel) > 0.0005 || Math.Abs(br.TopOfSteel.Z - topOfSteel) > 0.0005)
        {
            ch.Mismatch = true;
            ch.Checks.Add($"Framing bottom of steel {E(br.BottomOfSteelElevation)} differs by {In(br.BottomOfSteelElevation - bottomOfSteel)}");
        }
        else ch.Checks.Add($"Top of steel and bottom of steel match the framing model ({E(br.TopOfSteel.Z)}, {E(br.BottomOfSteelElevation)}).");
        z = bottomOfSteel;

        // Sole plate: tapered to the girder grade so its bottom is level.
        var g = f.Girders[girder];
        int nSpan = g.Spans.Count;
        double s = !double.IsNaN(br.PathDistance) ? br.PathDistance : g.DistanceAt(Math.Min(support, nSpan - 1), support == nSpan ? 1 : 0);
        double grade = g.Path.GradeAt(s);
        var bd = def.BearingsAt(support);
        double ls = bd.SolePlateLength;
        double soleAtCl = bd.SoleAtCl(grade);
        z -= soleAtCl;
        ch.Steps.Add(new CalcStep("− Sole plate at CL bearing",
            $"min {In(bd.SolePlateMinThickness)} + |girder grade {Pct(grade)}| × plate length {In(ls)} / 2 = {Dec(bd.SolePlateMinThickness, u)} + {Dec(Math.Abs(grade) * ls / 2, u)} = {Dec(soleAtCl, u)} (tapered; bottom level)",
            -soleAtCl, z, bd.Type == BearingType.Generic ? "Bottom of sole plate (top of bearing = bottom of steel above, as in the plans' bearing table)" : "Bottom of sole plate = top of bearing"));

        var fix = def.SupportAt(support).FixityOf(girder);
        if (bd.Type == BearingType.Elastomeric)
        {
            z -= bd.PadHeight;
            ch.Steps.Add(new CalcStep("− Elastomeric pad", $"pad {In(bd.PadLength)} × {In(bd.PadWidth)} × {In(bd.PadHeight)}", -bd.PadHeight, z, "Bottom of bearing = bearing seat"));
        }
        else if (bd.Type == BearingType.Generic)
        {
            double hb = Math.Max(0, bd.GenericBodyHeight(grade));
            z -= hb;
            ch.Steps.Add(new CalcStep("− Bearing (by manufacturer)",
                $"assumed height {In(bd.GenericHeight)} − sole {Dec(soleAtCl, u)} − masonry plate {In(bd.MasonryPlateThickness)} − pad {In(bd.MasonryPadThickness)} = {Dec(hb, u)} ({fix})", -hb, z));
            z -= bd.MasonryPlateThickness;
            ch.Steps.Add(new CalcStep("− Masonry plate", $"{In(bd.MasonryPlateThickness)}", -bd.MasonryPlateThickness, z, bd.MasonryPadThickness > 0 ? "" : "Bottom of bearing = bearing seat"));
            if (bd.MasonryPadThickness > 0)
            {
                z -= bd.MasonryPadThickness;
                ch.Steps.Add(new CalcStep("− Preformed masonry pad", $"{In(bd.MasonryPadThickness)}", -bd.MasonryPadThickness, z, "Bottom of bearing = bearing seat"));
            }
        }
        else
        {
            z -= bd.UpperPlateThickness;
            ch.Steps.Add(new CalcStep("− Upper bearing plate", $"{In(bd.UpperPlateThickness)}", -bd.UpperPlateThickness, z));
            if (fix != BearingFixity.Fixed)
            {
                z -= bd.PtfeThickness;
                ch.Steps.Add(new CalcStep("− PTFE sliding surface", $"{In(bd.PtfeThickness)} ({fix})", -bd.PtfeThickness, z));
            }
            z -= bd.DiscHeight;
            ch.Steps.Add(new CalcStep("− Urethane disc", $"{In(bd.DiscDiameter)} dia × {In(bd.DiscHeight)}", -bd.DiscHeight, z));
            z -= bd.MasonryPlateThickness;
            ch.Steps.Add(new CalcStep("− Masonry plate", $"{In(bd.MasonryPlateThickness)}", -bd.MasonryPlateThickness, z, bd.MasonryPadThickness > 0 ? "" : "Bottom of bearing = bearing seat"));
            if (bd.MasonryPadThickness > 0)
            {
                z -= bd.MasonryPadThickness;
                ch.Steps.Add(new CalcStep("− Preformed masonry pad", $"{In(bd.MasonryPadThickness)}", -bd.MasonryPadThickness, z, "Bottom of bearing = bearing seat"));
            }
        }

        if (steel?.BearingSeats.FirstOrDefault(x => x.Girder == girder && Math.Abs(x.Station - br.Station) < 1e-6) is { } seat)
        {
            double diff = seat.SeatElevation - z;
            if (Math.Abs(diff) > 0.0005) { ch.Mismatch = true; ch.Checks.Add($"3D bearing seat {E(seat.SeatElevation)} differs by {In(diff)}"); }
            else ch.Checks.Add($"Bearing seat matches the 3D model ({E(seat.SeatElevation)}).");
        }
        ch.Checks.Add($"Total depth top of deck to bearing seat: {u.FormatLength(ch.Steps[1].Elevation - z)}.");

        // Below the seat: pedestal, cap, column, footing, piles (ElevationCheckSubstructure.cs). The seat is where the generator puts it
        // (the framing bearing point); a shared pier may be built by another bridge (others).
        if (FindPedestal(sub, others, support, girder, br.TopOfSteel.XY) is { } hit)
            AddSubstructure(ch, hit.unit, hit.ped, br.TopOfSteel.XY, u);
        return ch;
    }

    /// <summary>All bearings of a bridge. <paramref name="others"/> = the substructure models of the other bridges (shared piers are built
    /// by one owner bridge; its unit carries the seats of every bridge on it).</summary>
    public static List<ElevationChain> AllBearings(BridgeFraming f, SteelModel? steel, UnitSystem u, Substructure.SubstructureModel? sub = null,
        Prestressed.PrestressedModel? prestressed = null, IEnumerable<Substructure.SubstructureModel>? others = null)
    {
        var o = others?.ToList();
        if (prestressed != null) return PrestressedBearings(f, prestressed, u, sub, o);
        var list = new List<ElevationChain>();
        foreach (var sl in f.Supports.Concat(f.AuxSupports))
            for (int i = 0; i < sl.Bearings.Length; i++)
                if (sl.Bearings[i] != null) list.Add(Bearing(f, steel, sl.Index, i, u, sub, o));
        return list;
    }

    /// <summary>Plain-text report (copy/paste or CSV-like) of chains.</summary>
    public static string ToText(IEnumerable<ElevationChain> chains, UnitSystem u)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in chains)
        {
            sb.AppendLine(ch.Title);
            sb.AppendLine($"  STA {u.FormatStation(ch.Station)}, offset {u.FormatLength(ch.Offset)}");
            foreach (var st in ch.Steps)
            {
                string change = double.IsNaN(st.Change) ? "" : (st.Change >= 0 ? "+" : "−") + u.FormatLength(Math.Abs(st.Change), Math.Abs(st.Change) < 0.3 ? LengthKind.Short : LengthKind.Long);
                sb.AppendLine($"  {st.Item,-30} {change,14}  = {u.FormatLength(st.Elevation, LengthKind.Coordinate),11}  {st.Result}");
                sb.AppendLine($"      {st.Basis}");
            }
            foreach (var c in ch.Checks) sb.AppendLine((c.Contains("differs") ? "  ! " : "  ✓ ") + c);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
