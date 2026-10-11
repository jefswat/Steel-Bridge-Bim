using System.Collections.Concurrent;
using System.Diagnostics;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// Multi-column bents and abutment caps with their foundations. For each support: bearing seats come from the steel
/// model; the cap top is shaped (sloped / crowned / level / following the seats) and set so the shortest pedestal is the
/// minimum height; pedestals make up each seat; columns run from the cap bottom to the foundation top; foundations are
/// drilled shafts, spread footings or pile caps on piles. Supports are generated in parallel.
/// </summary>
public sealed partial class SubstructureGenerator
{
    private static readonly Vec3 Up = new(0, 0, 1);
    private readonly BridgeFraming _f;
    private readonly IReadOnlyList<BearingSeat> _seatList;
    private readonly UnitSystem _u;
    private readonly Func<Vec2, double?>? _ground;
    private readonly ConcurrentQueue<Diagnostic> _diag = new();
    private readonly ConcurrentQueue<string> _notes = new();

    private SubstructureGenerator(BridgeFraming f, IReadOnlyList<BearingSeat> seats, UnitSystem u, Func<Vec2, double?>? ground)
    {
        _f = f;
        _seatList = seats;
        _u = u;
        _ground = ground;
    }

    /// <param name="ground">Existing ground elevation at a plan point (terrain), used for automatic foundation tops.</param>
    public static SubstructureModel Generate(BridgeFraming framing, SteelModel steel, UnitSystem units, Func<Vec2, double?>? ground = null) =>
        Generate(framing, steel.BearingSeats, units, ground);

    /// <summary>From explicit bearing seats (steel bearings, or prestressed beam pads — two per girder at interior bents).</summary>
    public static SubstructureModel Generate(BridgeFraming framing, IReadOnlyList<BearingSeat> seats, UnitSystem units, Func<Vec2, double?>? ground = null) =>
        Generate(framing, seats, units, ground, null, null);

    /// <param name="foreignSeats">Shared piers: bearing seats of OTHER bridges on this support (Location and Axis set), by support index.</param>
    /// <param name="skipSupport">Supports not built here (shared piers owned by another bridge).</param>
    public static SubstructureModel Generate(BridgeFraming framing, IReadOnlyList<BearingSeat> seats, UnitSystem units, Func<Vec2, double?>? ground,
        IReadOnlyDictionary<int, List<(string label, BearingSeat seat)>>? foreignSeats, Func<int, bool>? skipSupport)
    {
        var sw = Stopwatch.StartNew();
        var g = new SubstructureGenerator(framing, seats, units, ground) { _foreign = foreignSeats };
        var m = new SubstructureModel();
        // Supports, then auxiliary supports (girders beginning on their own line); framing indices run across both.
        var defs = framing.Definition.Supports.Concat(framing.Definition.AuxiliarySupports).ToList();
        var units2 = new SubstructureUnit?[defs.Count];
        if (!framing.HasErrors)
            Parallel.For(0, defs.Count, j =>
            {
                var sub = defs[j].ResolvedSubstructure;
                if (!sub.Enabled || skipSupport?.Invoke(j) == true) return;
                try { units2[j] = g.Build(j, sub); }
                catch (Exception ex) { g.Warn($"{defs[j].Name}: substructure could not be generated ({ex.Message}) {(ex is InvalidOperationException ? "" : string.Join(" | ", (ex.StackTrace ?? "").Split((char)10).Take(5).Select(l => l.Trim())))}."); }
            });
        m.Units.AddRange(units2.OfType<SubstructureUnit>());
        g.CheckPileClearances();
        g.BuildRebar(m);
        g.BuildDrainage(m);
        m.Diagnostics.AddRange(g._diag);
        m.Notes.AddRange(g._notes.OrderBy(n => n));
        m.GenerationTime = sw.Elapsed;
        return m;
    }

    private void Warn(string s) => _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Warning, s));
    private void Note(string s) => _notes.Enqueue(s);
    private string L(double m) => _u.FormatLength(m);
    private string Sh(double m) => _u.FormatLength(m, LengthKind.Short);
    private string El(double z) => _u.FormatLength(z, LengthKind.Coordinate);

    private sealed record Seat(int Girder, double S, double N, double Z, Vec2[] Footprint, string Label = "");
    private IReadOnlyDictionary<int, List<(string label, BearingSeat seat)>>? _foreign;

    private SubstructureUnit Build(int j, SubstructureDefinition sd)
    {
        var sl = _f.SupportLine(j);
        var def = _f.Definition;
        var a = _f.Alignment;
        var t = Vec2.FromAngle(a.AngleAt(sl.Station));
        var d = sl.Direction;
        var n = d.Left;
        if (Vec2.Dot(n, t) < 0) n = -n;
        var unit = new SubstructureUnit
        {
            SupportIndex = j, Name = sl.Name, Type = sl.Type, Definition = sd, Origin = sl.AlignmentPoint, D = d, N = n, Station = sl.Station,
        };
        double S(Vec2 p) => Vec2.Dot(p - sl.AlignmentPoint, d);
        double Nn(Vec2 p) => Vec2.Dot(p - sl.AlignmentPoint, n);

        // ---- bearing seats and footprints (masonry plate or pad, aligned with the girder)
        var bd = def.BearingsAt(j);
        var seats = new List<Seat>();
        for (int gi = 0; gi < sl.Bearings.Length; gi++)
        {
            var br = sl.Bearings[gi];
            if (br == null) continue;
            // Located seats (prestressed: one per beam end) or the single seat at the framing bearing (steel).
            var mine = _seatList.Where(x => x.Girder == gi && x.Location != null && x.Support == sl.Name).ToList();
            if (mine.Count == 0 && _seatList.FirstOrDefault(x => x.Girder == gi && x.Location == null && Math.Abs(x.Station - br.Station) < 1e-6) is { } one)
                mine.Add(one);
            foreach (var seat in mine)
            {
                Vec2 tx;
                if (seat.Axis is { } ax) tx = ax.Normalized();
                else
                {
                    var g = _f.Girders[gi];
                    int nSpan = g.Spans.Count;
                    double sGird = !double.IsNaN(br.PathDistance) ? br.PathDistance : g.DistanceAt(Math.Min(j, nSpan - 1), j == nSpan ? 1 : 0);
                    tx = g.Path.At(sGird).Item2.Normalized();
                }
                var ty = tx.Left;
                var p = seat.Location ?? br.TopOfSteel.XY;
                var (fl, fw) = bd.Footprint(def.SupportAt(j).FixityOf(gi));
                double l = seat.FootprintLength > 0 ? seat.FootprintLength : fl, w = seat.FootprintWidth > 0 ? seat.FootprintWidth : fw;
                var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                    .Select(c => p + tx * (c.Item1 * l / 2) + ty * (c.Item2 * w / 2))
                    .Select(q => new Vec2(S(q), Nn(q))).ToArray();
                seats.Add(new Seat(gi, S(p), Nn(p), seat.SeatElevation, corners));
            }
        }
        // Shared pier: bearings of the other units / bridges on this pier.
        if (_foreign != null && _foreign.TryGetValue(j, out var fs))
            foreach (var (label, seat) in fs)
            {
                if (seat.Location is not { } p || seat.Axis is not { } ax) continue;
                var tx = ax.Normalized();
                var ty = tx.Left;
                var (fl, fw) = bd.Footprint(def.SupportAt(j).ResolvedFixity);
                double l = seat.FootprintLength > 0 ? seat.FootprintLength : fl, w = seat.FootprintWidth > 0 ? seat.FootprintWidth : fw;
                var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                    .Select(c => p + tx * (c.Item1 * l / 2) + ty * (c.Item2 * w / 2))
                    .Select(q => new Vec2(S(q), Nn(q))).ToArray();
                seats.Add(new Seat(1000 + seats.Count, S(p), Nn(p), seat.SeatElevation, corners, label));
            }
        if (seats.Count == 0) throw new InvalidOperationException("no bearing seats");
        seats.Sort((x, y) => x.S.CompareTo(y.S));
        if (sl.Type == SupportType.Abutment && sd.Abutment != null) return BuildDesignedAbutment(j, unit, sd, sl, seats);

        // ---- cap extents along the support line
        double sMin = seats.Min(x => x.Footprint.Min(c => c.X)), sMax = seats.Max(x => x.Footprint.Max(c => c.X));
        double sCenter = (seats[0].S + seats[^1].S) / 2;
        double s0 = Math.Min(seats[0].S - sd.CapEndOverhang, sMin - sd.PedestalProjection);
        double s1 = Math.Max(seats[^1].S + sd.CapEndOverhang, sMax + sd.PedestalProjection);
        var colS = sl.Type == SupportType.Pier ? sd.ColumnOffsets(sCenter) : Array.Empty<double>();
        double colHalf = sd.ColumnShape == ColumnShape.Round ? sd.ColumnDiameter / 2 : sd.ColumnWidth / 2;
        var profile = sd.CapProfile.Count >= 3 ? sd.CapProfile.Select(p => new Vec2(p.S, p.Elevation)).ToList() : null;
        var profileAhead = profile != null && sd.CapProfileAhead.Count >= 3 ? sd.CapProfileAhead.Select(p => new Vec2(p.S, p.Elevation)).ToList() : null;
        if (profile != null)
        {
            s0 = profile.Min(p => p.X);
            s1 = profile.Max(p => p.X);
            // Side-by-side as-designed piers on one shared line: this cap takes only the seats that land on it.
            if (_foreign != null && _foreign.ContainsKey(j))
            {
                int before = seats.Count;
                seats.RemoveAll(x => x.S < s0 || x.S > s1);
                if (seats.Count < before) Note($"{sl.Name}: {before - seats.Count} bearing seat(s) on the shared line are beyond this cap (carried by the neighboring pier).");
                if (seats.Count == 0) throw new InvalidOperationException("no bearing seats on the as-designed cap");
            }
            foreach (var x in seats)
                if (x.Footprint.Any(q => q.X < s0 || q.X > s1))
                    Warn($"{sl.Name}: bearing {(x.Label != "" ? x.Label : $"G{x.Girder + 1}")} is not fully on the as-designed cap — check the cap profile against the bearing locations.");
        }
        foreach (double cs in colS)
        {
            if (profile != null)
            {
                if (cs - colHalf < s0 || cs + colHalf > s1) Warn($"{sl.Name}: column at {L(cs)} is not under the as-designed cap — check the column locations.");
                continue;
            }
            if (cs - colHalf < s0 || cs + colHalf > s1)
                Warn($"{sl.Name}: column at {L(cs)} extends past the cap end; the cap was lengthened.");
            s0 = Math.Min(s0, cs - colHalf - In(6));
            s1 = Math.Max(s1, cs + colHalf + In(6));
        }
        unit.CapS0 = s0;
        unit.CapS1 = s1;

        // ---- cap top shape g(s) (up to a constant), then lift so the shortest pedestal is the minimum height
        var breaks = new List<double> { s0, s1 };
        Func<double, double> shape;
        string how;
        switch (sd.TopShape)
        {
            case CapTopShape.Level:
                shape = _ => 0;
                how = "level";
                break;
            case CapTopShape.FollowSeats:
            {
                // One point per girder (two seats per girder at prestressed interior bents: lower one governs).
                var pts = seats.GroupBy(x => x.Girder).Select(gr => new Vec2(gr.Average(x => x.S), gr.Min(x => x.Z))).OrderBy(p => p.X).ToList();
                if (pts.Count == 1) pts.Add(new Vec2(pts[0].X + 1, pts[0].Y));
                shape = s => SubstructureUnit.Interp(pts, s);
                breaks.AddRange(seats.Select(x => x.S));
                how = "follows the bearing seats";
                break;
            }
            case CapTopShape.Crowned:
            {
                double sc = !double.IsNaN(sd.CrownOffset) ? sd.CrownOffset : CrownOnSupportLine(sl, a);
                double Grade(double s) { var q = sl.AlignmentPoint + d * s; var so = a.Inverse(q); return a.ElevationAt(so.Station, so.Offset); }
                double z0 = Grade(sc), dl = Math.Max(1, sc - seats[0].S), dr = Math.Max(1, seats[^1].S - sc);
                double ml = (z0 - Grade(sc - dl)) / dl, mr = (Grade(sc + dr) - z0) / dr;
                shape = s => s < sc ? ml * (s - sc) : mr * (s - sc);
                if (sc > s0 && sc < s1) breaks.Add(sc);
                how = $"crowned at {L(sc)}: {FormatSlope(ml)} left, {FormatSlope(mr)} right (parallel to the deck)";
                break;
            }
            default:
            {
                double m;
                if (!double.IsNaN(sd.CapSlope)) m = sd.CapSlope;
                else if (seats.Count == 1) m = 0;
                else
                {
                    // Least squares through the seats.
                    double sx = seats.Average(x => x.S), sz = seats.Average(x => x.Z);
                    double num = seats.Sum(x => (x.S - sx) * (x.Z - sz)), den = seats.Sum(x => (x.S - sx) * (x.S - sx));
                    m = den > 1e-9 ? num / den : 0;
                }
                shape = s => m * s;
                how = $"sloped {FormatSlope(m)}" + (double.IsNaN(sd.CapSlope) ? " (best fit through the seats)" : "");
                break;
            }
        }
        if (profile != null)
        {
            // Upper / lower envelopes of the as-designed polygon (pedestals sit on the top, columns hang off the bottom).
            var xs = profile.Select(p => Math.Clamp(p.X, s0 + 1e-6, s1 - 1e-6)).Append(s0 + 1e-6).Append(s1 - 1e-6).Distinct().OrderBy(x => x).ToList();
            foreach (double x in xs)
            {
                var (lo, hi) = Envelope(profile, x);
                unit.CapTop.Add(new Vec2(x, hi));
                unit.CapBottom.Add(new Vec2(x, lo));
            }
            unit.CapOutline.AddRange(profile);
            if (profileAhead != null) unit.CapOutlineAhead.AddRange(profileAhead);
            if (profileAhead != null)
                foreach (var x in profileAhead.Select(p => Math.Clamp(p.X, s0 + 1e-6, s1 - 1e-6)).Append(s0 + 1e-6).Append(s1 - 1e-6).Distinct().OrderBy(x => x))
                    unit.CapTopAhead.Add(new Vec2(x, Envelope(profileAhead, x).hi));
            how = profileAhead != null ? "as designed (plan elevation profile, stepped ahead half)" : "as designed (plan elevation profile)";
        }
        else
        {
            double c = seats.Min(x => x.Z - shape(x.S)) - sd.MinPedestalHeight;
            breaks = breaks.Where(b => b >= s0 - 1e-9 && b <= s1 + 1e-9).Distinct().OrderBy(b => b).ToList();
            foreach (double b in breaks) unit.CapTop.Add(new Vec2(b, shape(b) + c));
            if (sd.BottomShape == CapBottomShape.Level)
            {
                double zb = unit.CapTop.Min(p => p.Y) - sd.CapDepth;
                unit.CapBottom.Add(new Vec2(s0, zb));
                unit.CapBottom.Add(new Vec2(s1, zb));
            }
            else unit.CapBottom.AddRange(unit.CapTop.Select(p => new Vec2(p.X, p.Y - sd.CapDepth)));
        }
        unit.CapTopDescription = how;

        // ---- cap across the support line
        bool abut = sl.Type == SupportType.Abutment;
        double sigma = j == 0 ? 1 : -1; // abutments: direction from the bearing toward the span
        var sup = def.SupportAt(j);
        if (!abut)
        {
            // Shared expansion piers are centered on the CL pier, not on this unit's bearing line.
            double nPier = sd.CapOffset + sup.PierCenterOffset;
            unit.CapN0 = nPier - sd.CapWidth / 2;
            unit.CapN1 = nPier + sd.CapWidth / 2;
            unit.CapMidN = nPier;
        }
        else
        {
            double front = sigma * sd.BearingToCapFront;
            double back = sigma * (sd.BearingToCapFront - sd.CapWidth);
            double bwFront = -sigma * (sup.DeckEndOffset + sd.BackwallGap);
            double bwBack = bwFront - sigma * sd.BackwallThickness;
            if (sigma * bwBack < sigma * back) back = bwBack; // cap reaches under the backwall
            unit.CapN0 = Math.Min(front, back);
            unit.CapN1 = Math.Max(front, back);
            // Backwall top follows finished grade along its front face.
            var top = new List<Vec2>();
            int ns = Math.Max(2, (int)Math.Ceiling((s1 - s0) / 1.0));
            double nMid = (bwFront + bwBack) / 2;
            for (int i = 0; i <= ns; i++)
            {
                double s = s0 + (s1 - s0) * i / ns;
                var so = a.Inverse(unit.Plan(s, nMid));
                top.Add(new Vec2(s, a.ElevationAt(so.Station, so.Offset) - sd.BackwallTopBelowGrade));
            }
            unit.Backwall = (Math.Min(bwFront, bwBack), Math.Max(bwFront, bwBack), top);
        }

        // ---- pedestals: one per seat from the bearing footprint, or the plan size (optionally one block per girder line carrying several seats)
        double tallest = 0;
        bool planPed = sd.PedestalSizeAlong > 0 && sd.PedestalSizeAcross > 0;
        if (sd.PedestalShared && !planPed) Warn($"{sl.Name}: PedestalShared needs the plan pedestal size (PedestalSizeAlong / PedestalSizeAcross); ignored.");
        // Two rows (plan "BACK BRG." / "AHEAD BRG."): the ahead (+N of the CL pier) blocks may have their own across size, and the rows may abut on a printed line
        bool rows = planPed && !sd.PedestalShared && (sd.PedestalSizeAcrossAhead > 0 || !double.IsNaN(sd.PedestalRowJoint));
        if (!planPed && (sd.PedestalSizeAcrossAhead > 0 || !double.IsNaN(sd.PedestalRowJoint)))
            Warn($"{sl.Name}: PedestalSizeAcrossAhead / PedestalRowJoint need the plan pedestal size (PedestalSizeAlong / PedestalSizeAcross); ignored.");
        double AcrossOf(Seat x) => rows && sd.PedestalSizeAcrossAhead > 0 && x.N > unit.CapMidN ? sd.PedestalSizeAcrossAhead : sd.PedestalSizeAcross;
        var groups = new List<List<Seat>>();
        if (planPed && sd.PedestalShared)
        {
            foreach (var x in seats.OrderBy(q => q.S))
            {
                var g = groups.LastOrDefault();
                if (g != null && Math.Abs(x.S - g.Average(q => q.S)) < sd.PedestalSizeAlong / 2) g.Add(x); else groups.Add(new List<Seat> { x });
            }
        }
        else groups.AddRange(seats.Select(x => new List<Seat> { x }));
        foreach (var grp in groups)
        {
            double sMid, nMid, ps0, ps1, pn0, pn1;
            string whoP = string.Join("/", grp.Select(q => q.Label != "" ? q.Label : $"G{q.Girder + 1}"));
            if (planPed)
            {
                sMid = grp.Average(q => (q.Footprint.Min(f => f.X) + q.Footprint.Max(f => f.X)) / 2);
                nMid = grp.Average(q => (q.Footprint.Min(f => f.Y) + q.Footprint.Max(f => f.Y)) / 2);
                ps0 = sMid - sd.PedestalSizeAlong / 2; ps1 = sMid + sd.PedestalSizeAlong / 2;
                double across = AcrossOf(grp[0]);
                pn0 = nMid - across / 2; pn1 = nMid + across / 2;
                if (rows && !double.IsNaN(sd.PedestalRowJoint))
                {
                    double nj = unit.CapMidN + sd.PedestalRowJoint;
                    if (grp[0].N > unit.CapMidN) { pn0 = nj; pn1 = nj + across; } else { pn0 = nj - across; pn1 = nj; }
                    nMid = (pn0 + pn1) / 2;
                    if (pn0 < unit.CapN0 - 1e-6 || pn1 > unit.CapN1 + 1e-6) Warn($"{sl.Name}: plan pedestal {whoP} ({Sh(pn0 - unit.CapMidN)} .. {Sh(pn1 - unit.CapMidN)} from the CL pier) extends beyond the cap width.");
                }
                foreach (var q in grp)
                {
                    double out0 = Math.Max(ps0 - q.Footprint.Min(f => f.X), Math.Max(q.Footprint.Max(f => f.X) - ps1, Math.Max(pn0 - q.Footprint.Min(f => f.Y), q.Footprint.Max(f => f.Y) - pn1)));
                    if (out0 > In(0.125)) Warn($"{sl.Name}: bearing footprint at {whoP} extends {Sh(out0)} beyond the plan pedestal ({Sh(sd.PedestalSizeAlong)} x {Sh(across)}).");
                }
                double zMax = grp.Max(q => q.Z), zMin = grp.Min(q => q.Z);
                if (zMax - zMin > In(0.125))
                    Warn($"{sl.Name}: pedestal {whoP} carries {grp.Count} seats {Sh(zMax - zMin)} apart ({El(zMin)} to {El(zMax)}); the block top follows the highest seat - check the bearing stacks.");
            }
            else
            {
                var x0 = grp[0];
                ps0 = x0.Footprint.Min(q => q.X) - sd.PedestalProjection; ps1 = x0.Footprint.Max(q => q.X) + sd.PedestalProjection;
                pn0 = x0.Footprint.Min(q => q.Y) - sd.PedestalProjection; pn1 = x0.Footprint.Max(q => q.Y) + sd.PedestalProjection;
                sMid = (ps0 + ps1) / 2; nMid = (pn0 + pn1) / 2;
            }
            double zTop = grp.Max(q => q.Z);
            foreach (var x in grp)
            {
                var ped = new PedestalInfo(x.Girder, sMid, nMid, ps1 - ps0, pn1 - pn0, unit.CapTopAt(x.S, x.N), x.Z) { SeatS = x.S, SeatN = x.N };
                unit.Pedestals.Add(ped);
                tallest = Math.Max(tallest, ped.Height);
                if (profile != null && ped.Height < -In(0.25))
                    Warn($"{sl.Name}: bearing seat {(x.Label != "" ? x.Label : $"G{x.Girder + 1}")} is {Sh(-ped.Height)} BELOW the as-designed cap top — check the bearing stack and the cap elevations.");
            }
            var block = new PedestalInfo(grp[0].Girder, sMid, nMid, ps1 - ps0, pn1 - pn0, unit.CapTopAt(grp[0].S, grp[0].N), zTop);
            unit.PedestalBlocks.Add(block);
            if (block.Height < In(0.125)) continue;
            var mesh = new Mesh();
            var foot = new[] { new Vec2(ps0, pn0), new Vec2(ps1, pn0), new Vec2(ps1, pn1), new Vec2(ps0, pn1) };
            var bottom = foot.Select(q => unit.World(q.X, q.Y, planPed && grp.Count > 1 ? unit.CapTopAt(q.X, q.Y) : unit.CapTopAt(q.X, grp[0].N))).ToArray();
            var topR = foot.Select(q => unit.World(q.X, q.Y, zTop)).ToArray();
            MeshBuilder.AddLoft(mesh, foot, new[] { bottom, topR });
            string side = !planPed && seats.Count(o => o.Girder == grp[0].Girder) > 1 ? (grp[0].N < 0 ? " back" : " ahead") : "";
            unit.Solids.Add(new SubSolid(SubElementKind.Pedestal, $"{sl.Name} pedestal {whoP}{side}",
                $"Pedestal {Sh(ps1 - ps0)} x {Sh(pn1 - pn0)}{(planPed ? " (plan size)" : "")}, {Sh(block.Height)} high at CL bearing{(grp.Count > 1 ? $", {grp.Count} seats" : "")}", mesh));
        }
        if (planPed)
            for (int i = 0; i < unit.PedestalBlocks.Count; i++)
                for (int k = i + 1; k < unit.PedestalBlocks.Count; k++)
                {
                    var a1 = unit.PedestalBlocks[i]; var b1 = unit.PedestalBlocks[k];
                    if (Math.Abs(a1.S - b1.S) < (a1.LengthS + b1.LengthS) / 2 - In(0.125) && Math.Abs(a1.N - b1.N) < (a1.WidthN + b1.WidthN) / 2 - In(0.125))
                        Warn($"{sl.Name}: plan pedestals at S {Sh(a1.S)} and {Sh(b1.S)} overlap - check the pedestal size / seats.");
                }
        if (tallest > sd.MaxPedestalHeight)
            Warn($"{sl.Name}: tallest pedestal is {Sh(tallest)} (limit {Sh(sd.MaxPedestalHeight)}); consider a sloped, crowned or seat-following cap top.");
        Note($"{sl.Name}: cap top {how}; pedestals {Sh(unit.Pedestals.Min(p => p.Height))} to {Sh(tallest)}.");

        // ---- cap solid (profile in S-Z extruded across N)
        var capMesh = new Mesh();
        double capLen = s1 - s0;
        if (profile != null)
        {
            // Stepped cap: back half with the main profile, ahead half with its own (each extruded across its half).
            // With a cap section step the lower (core) section is narrower; the full-width strips are added by BuildCapStep.
            double sb = sd.Aesthetics is { Layers.Count: > 0 } aes ? aes.StepSetback : 0;
            double nLo = unit.CapN0 + sb, nHi = unit.CapN1 - sb;
            double nSplit = profileAhead != null ? unit.CapMidN : nHi;
            MeshBuilder.AddLoft(capMesh, profile, new[] { profile.Select(p => unit.World(p.X, nLo, p.Y)).ToArray(), profile.Select(p => unit.World(p.X, nSplit, p.Y)).ToArray() });
            if (profileAhead != null)
                MeshBuilder.AddLoft(capMesh, profileAhead, new[] { profileAhead.Select(p => unit.World(p.X, nSplit, p.Y)).ToArray(), profileAhead.Select(p => unit.World(p.X, nHi, p.Y)).ToArray() });
            double dMax = unit.CapTop.Zip(unit.CapBottom, (tp, bt) => tp.Y - bt.Y).Max(), dMin = unit.CapTop.Zip(unit.CapBottom, (tp, bt) => tp.Y - bt.Y).Min();
            unit.Solids.Add(new SubSolid(SubElementKind.Cap, $"{sl.Name} cap", $"{(abut ? "Abutment" : "Bent")} cap {L(capLen)} long, {Sh(unit.CapN1 - unit.CapN0 - 2 * sb)} wide, {Sh(dMin)} to {Sh(dMax)} deep (as designed{(sb > 0 ? ", lower section" : "")})", capMesh));
        }
        else
        {
            ExtrudeProfile(capMesh, unit, unit.CapTop, unit.CapBottom, unit.CapN0, unit.CapN1);
            unit.Solids.Add(new SubSolid(SubElementKind.Cap, $"{sl.Name} cap", $"{(abut ? "Abutment" : "Bent")} cap {L(capLen)} long, {Sh(unit.CapN1 - unit.CapN0)} wide, {Sh(sd.CapDepth)} deep", capMesh));
        }
        if (sd.Aesthetics is { Layers.Count: > 0 } ae)
        {
            if (profile == null) Warn($"{sl.Name}: cap section step ignored - it needs an as-designed cap profile.");
            else BuildCapStep(unit, sl.Name, ae, unit.CapOutline, unit.CapOutlineAhead.Count >= 3 ? unit.CapOutlineAhead : unit.CapOutline);
        }
        if (unit.Backwall is { } bw)
        {
            var bwMesh = new Mesh();
            ExtrudeProfile(bwMesh, unit, bw.Top, unit.CapTop.Select(p => p).ToList(), bw.N0, bw.N1);
            unit.Solids.Add(new SubSolid(SubElementKind.Backwall, $"{sl.Name} backwall", $"Backwall {Sh(sd.BackwallThickness)} thick", bwMesh));
        }

        // ---- columns and foundations
        if (!abut)
        {
            int k = 0;
            foreach (double cs in colS)
            {
                double nc = sd.CapOffset + sup.PierCenterOffset;
                double topZ = unit.CapBottomAt(cs);
                double groundZ = GroundAt(unit.Plan(cs, nc));
                double fTop = !double.IsNaN(sd.FoundationTopElevation) ? sd.FoundationTopElevation
                    : !double.IsNaN(groundZ) ? groundZ - sd.GroundCover : topZ - Ft(20);
                if (double.IsNaN(sd.FoundationTopElevation) && double.IsNaN(groundZ) && k == 0)
                    Note($"{sl.Name}: no terrain under the bent; foundation tops set 20 ft below the cap. Enter a foundation top elevation or attach terrain.");
                if (fTop >= topZ - Ft(1)) { Warn($"{sl.Name}: foundation top {El(fTop)} is at or above the cap bottom; column {k + 1} omitted."); k++; continue; }
                var col = new ColumnInfo(k, cs, nc, topZ, fTop, sd.ColumnShape,
                    sd.ColumnShape == ColumnShape.Round ? sd.ColumnDiameter : sd.ColumnWidth,
                    sd.ColumnShape == ColumnShape.Round ? sd.ColumnDiameter : sd.ColumnDepth);
                unit.Columns.Add(col);
                var cm = new Mesh();
                var sec = ColumnSection(sd);
                double zShaft = fTop;
                if (!double.IsNaN(sd.ColumnFlareElevation) && sd.ColumnFlareElevation > fTop && sd.ColumnFlareElevation < topZ && sd.ColumnShape == ColumnShape.Rectangular)
                {
                    // Flared base: its own (larger) section from the foundation up to the flare elevation.
                    var fsec = Octagon(sd.ColumnFlareWidth, sd.ColumnFlareDepth, sd.ColumnFlareChamferS, sd.ColumnFlareChamferN);
                    MeshBuilder.AddLoft(cm, fsec, new[] { fsec.Select(q => unit.World(cs + q.X, nc + q.Y, fTop)).ToArray(), fsec.Select(q => unit.World(cs + q.X, nc + q.Y, sd.ColumnFlareElevation)).ToArray() });
                    zShaft = sd.ColumnFlareElevation;
                }
                var botRing = sec.Select(q => unit.World(cs + q.X, nc + q.Y, zShaft)).ToArray();
                var topRing = sec.Select(q => unit.World(cs + q.X, nc + q.Y, unit.CapBottomAt(cs + q.X) + In(0.01))).ToArray();
                MeshBuilder.AddLoft(cm, sec, new[] { botRing, topRing });
                string size = sd.ColumnShape == ColumnShape.Round ? $"{Sh(sd.ColumnDiameter)} dia" : $"{Sh(sd.ColumnWidth)} x {Sh(sd.ColumnDepth)}";
                unit.Solids.Add(new SubSolid(SubElementKind.Column, $"{sl.Name} column {k + 1}", $"Column {size}, {L(topZ - fTop)} high", cm));
                AddFoundation(unit, sd, cs, nc, fTop, groundZ, k + 1);
                k++;
            }
        }
        else
        {
            int cnt = Math.Max(1, sd.AbutmentFoundationCount);
            double total = (cnt - 1) * sd.AbutmentFoundationSpacing;
            double sc = (s0 + s1) / 2;
            double nc = (unit.CapN0 + unit.CapN1) / 2;
            for (int k = 0; k < cnt; k++)
            {
                double s = sc - total / 2 + k * sd.AbutmentFoundationSpacing;
                AddFoundation(unit, sd, s, nc, unit.CapBottomAt(s), GroundAt(unit.Plan(s, nc)), k + 1, underCap: true);
            }
        }
        return unit;
    }

    // ================================================================== cap section step (aesthetic relief curve)

    /// <summary>
    /// The aesthetic cap section: the core (lower section, narrower by the setback on each face) is the cap solid itself; this adds
    /// the full-width strips above the relief curve on BOTH faces as layers stacked on the core face. Layer k (lowest line first)
    /// is the part of the cap outline above its boundary line, setback / count thick, flush with the cap top and the end faces
    /// (lofts of the clipped region), the tread under it drafted by <see cref="CapAestheticsDefinition.DraftDegrees"/>.
    /// Where the lines of the layers coincide they make one step of the summed thickness (single 6" step).
    /// </summary>
    private void BuildCapStep(SubstructureUnit unit, string name, CapAestheticsDefinition ae, List<Vec2> outlineBack, List<Vec2> outlineAhead)
    {
        int m = ae.Layers.Count;
        double h = ae.StepSetback / m, dd = h * Math.Tan(ae.DraftDegrees * Math.PI / 180);
        if (ae.StepSetback <= 0 || unit.CapN1 - unit.CapN0 <= 2 * ae.StepSetback + In(6))
        { Warn($"{name}: cap section step needs a setback and a cap wider than twice the setback."); return; }
        double cover = unit.Definition.Reinforcement.CapCover;
        Warn($"{name}: cap bars are laid out for the full {Sh(unit.CapN1 - unit.CapN0)} width; below the relief curve the cap is {Sh(ae.StepSetback)} narrower on each face, "
            + $"so the face bars and stirrups there lie {Sh(ae.StepSetback - cover)} OUTSIDE the lower section (the plans bend them with the relief) - cap bar layout not adapted.");
        var mesh = new Mesh();
        int built = 0;
        unit.ReliefLines.Clear(); unit.ReliefLineTags.Clear();

        void Face(List<Vec2> outline, double nCore, double dirN)
        {
            // Top line = top corners of the outline (upper envelope without the steep raked end faces).
            var xs = outline.Select(p => p.X).Distinct().OrderBy(x => x).ToList();
            var top = xs.Select(x => new Vec2(x, Envelope(outline, x).hi)).ToList();
            static bool Steep(Vec2 a, Vec2 b) => Math.Abs(b.Y - a.Y) > Math.Abs(b.X - a.X);
            while (top.Count > 2 && Steep(top[0], top[1])) top.RemoveAt(0);
            while (top.Count > 2 && Steep(top[^2], top[^1])) top.RemoveAt(top.Count - 1);
            var o = top[0];
            var t = (top[^1] - o).Normalized();
            var down = new Vec2(t.Y, -t.X);
            Vec2 Fr(Vec2 p) => new(Vec2.Dot(p - o, t), Vec2.Dot(p - o, down));
            Vec2 Un(Vec2 p) => o + t * p.X + down * p.Y;
            var cap = outline.Select(Fr).ToList();
            double area = 0;
            for (int i = 0; i < cap.Count; i++) area += Vec2.Cross(cap[i], cap[(i + 1) % cap.Count]);
            if (area < 0) cap.Reverse();
            for (int i = 0; i < cap.Count; i++)
                if (Vec2.Cross(cap[(i + 1) % cap.Count] - cap[i], cap[(i + 2) % cap.Count] - cap[(i + 1) % cap.Count]) < -1e-9)
                { Warn($"{name}: cap outline is not convex - cap section step skipped on one face."); return; }
            double minX = cap.Min(p => p.X) - 0.05, maxX = cap.Max(p => p.X) + 0.05;
            var edges = cap.Zip(cap.Skip(1).Append(cap[0]), (a, b) => (a, b)).ToList();

            List<Vec2> Pts(BandChain ch, double dy)
            {
                var pts = ch.Sample().Select(p => new Vec2(p.X, p.Y + dy)).ToList();
                if (pts.Count >= 2 && pts[0].X > minX) { var dv = pts[1] - pts[0]; if (dv.X > 1e-9) pts.Insert(0, pts[0] - dv * ((pts[0].X - minX + 0.5) / dv.X)); }
                if (pts.Count >= 2 && pts[^1].X < maxX) { var dv = pts[^1] - pts[^2]; if (dv.X > 1e-9) pts.Add(pts[^1] + dv * ((maxX - pts[^1].X + 0.5) / dv.X)); }
                return pts;
            }
            List<Vec2> Region(List<Vec2> line)
            {
                var sub = new List<Vec2>(line) { new(line[^1].X, -2.0), new(line[0].X, -2.0) };
                foreach (var (a, b) in edges)
                {
                    var inp = sub; sub = new List<Vec2>();
                    if (inp.Count == 0) break;
                    var prev = inp[^1];
                    foreach (var cur in inp)
                    {
                        double sc = Vec2.Cross(b - a, cur - a), sp = Vec2.Cross(b - a, prev - a);
                        if (sc >= 0)
                        {
                            if (sp < 0) sub.Add(prev + (cur - prev) * (sp / (sp - sc)));
                            sub.Add(cur);
                        }
                        else if (sp >= 0) sub.Add(prev + (cur - prev) * (sp / (sp - sc)));
                        prev = cur;
                    }
                }
                return sub;
            }
            for (int k = 0; k < m; k++)
            {
                var outer = Region(Pts(ae.Layers[k], 0));
                var baseR = Region(Pts(ae.Layers[k], dd));
                if (outer.Count < 3) continue;
                if (baseR.Count != outer.Count) baseR = outer;
                var keep = Enumerable.Range(0, outer.Count).Where(i => i == 0 || (outer[i] - outer[i - 1]).Length > 1e-7 || (baseR[i] - baseR[i - 1]).Length > 1e-7).ToList();
                var op = keep.Select(i => Un(outer[i])).ToList();
                var bp = keep.Select(i => Un(baseR[i])).ToList();
                double nb = nCore + dirN * k * h, no = nCore + dirN * (k + 1) * h;
                MeshBuilder.AddLoft(mesh, bp, new[] { bp.Select(p => unit.World(p.X, nb, p.Y)).ToArray(), op.Select(p => unit.World(p.X, no, p.Y)).ToArray() });
                built++;
            }
            // Lines for the 2D views: each layer line clipped to the cap, in (S, Z) (drawn once per face; identical for both when the profiles match).
            for (int li = 0; li < m; li++)
            {
                var ch = ae.Layers[li];
                if (ch.Nodes.Count < 2) continue;
                void AddRun(List<Vec2> rr) { unit.ReliefLines.Add(rr); unit.ReliefLineTags.Add(((int)dirN, li)); }
                var pts = Pts(ch, 0);
                var run = new List<Vec2>();
                for (int i = 1; i < pts.Count; i++)
                {
                    double t0 = 0, t1 = 1;
                    var dv = pts[i] - pts[i - 1];
                    bool ok = true;
                    foreach (var (a, b) in edges)
                    {
                        double num = Vec2.Cross(b - a, pts[i - 1] - a), den = Vec2.Cross(b - a, dv);
                        if (Math.Abs(den) < 1e-12) { if (num < 0) { ok = false; break; } continue; }
                        double tt = -num / den;
                        if (den > 0) t0 = Math.Max(t0, tt); else t1 = Math.Min(t1, tt);
                        if (t0 > t1) { ok = false; break; }
                    }
                    if (!ok) { if (run.Count > 1) AddRun(run); run = new List<Vec2>(); continue; }
                    var s0 = Un(pts[i - 1] + dv * t0); var s1 = Un(pts[i - 1] + dv * t1);
                    if (run.Count == 0 || (run[^1] - s0).Length > 1e-6) { if (run.Count > 1) AddRun(run); run = new List<Vec2> { s0 }; }
                    run.Add(s1);
                }
                if (run.Count > 1) AddRun(run);
            }
        }
        // Back face (N0, projects toward -N) and ahead face (N1, toward +N), each on its own half's outline.
        Face(outlineBack, unit.CapN0 + ae.StepSetback, -1);
        Face(outlineAhead, unit.CapN1 - ae.StepSetback, 1);
        if (built == 0) { Warn($"{name}: cap section step lies outside the cap - check the relief curve against the cap profile."); return; }
        unit.Solids.Add(new SubSolid(SubElementKind.Cap, $"{name} cap section step",
            $"Full-width strips above the relief curve, both faces: {m} layer(s) of {Sh(h)} = {Sh(ae.StepSetback)} setback, {ae.DraftDegrees:0.#} deg tread draft", mesh));
        Note($"{name}: cap section {Sh(unit.CapN1 - unit.CapN0)} wide above the relief curve, {Sh(unit.CapN1 - unit.CapN0 - 2 * ae.StepSetback)} below (texture band on the {(ae.Face == CapFaceSide.Ahead ? "ahead" : "back")} face); {ae.Source}.");
    }

    private double GroundAt(Vec2 p) => _ground?.Invoke(p) ?? double.NaN;

    // ================================================================== deck drainage

    private static Mesh Pipe(Vec3 a, Vec3 b, double r, int sides = 12)
    {
        var m = new Mesh();
        var axis = b - a;
        if (axis.Length < 1e-6) return m;
        var f = Frame3.FromXZ(a, Math.Abs(axis.Normalized().Z) < 0.95 ? Vec3.Cross(axis, Up) : new Vec3(1, 0, 0), axis);
        var sec = Enumerable.Range(0, sides).Select(i => Vec2.FromAngle(2 * Math.PI * i / sides) * r).ToArray();
        MeshBuilder.AddLoft(m, sec, new[] { sec.Select(q => f.ToWorld(q.X, q.Y, 0)).ToArray(), sec.Select(q => f.ToWorld(q.X, q.Y, axis.Length)).ToArray() });
        return m;
    }

    /// <summary>Drain inlets through the slab at the gutter, downspouts below the girders (free fall, with splash basins on
    /// the ground) or collector pipes run along the bridge to the nearest pier and down its first column.</summary>
    private void BuildDrainage(SubstructureModel m)
    {
        var def = _f.Definition;
        if (def.Drains.Count(d => d.Trench == null) == 0 || _f.HasErrors) return;
        var a = _f.Alignment;
        var deck = def.Deck;
        double depth = def.Girders.IsPrestressed ? Prestressed.TxGirderCatalog.Get(def.Girders.Prestressed).Depth
            : def.Girders.Section.TotalDepth;
        var (toe, _, _) = Concrete.BarrierProfiles.PlanLines(deck.Barrier);
        int k = 0;
        foreach (var dr in def.Drains.Where(d => d.Trench == null))
        {
            k++;
            string name = $"Drain {k} ({dr.Type}) STA {_u.FormatStation(dr.Station)} {(dr.Left ? "LT" : "RT")}";
            double edge = dr.Left ? deck.LeftAt(dr.Station) + toe + dr.FromGutter : deck.RightAt(dr.Station) - toe - dr.FromGutter;
            var p = a.PointAt(dr.Station, edge);
            double ang = a.AngleAt(dr.Station);
            var t = Vec2.FromAngle(ang);
            double zTop = deck.FinishedElevation(a, dr.Station, edge);
            double zSlabBot = zTop - deck.Thickness;
            // Inlet: steel box through the slab, 6" below it.
            var f = new Frame3(p.WithZ(0), new Vec3(t.X, t.Y, 0), new Vec3(-t.Y, t.X, 0), Up);
            var inlet = new Mesh();
            // Grate at the deck surface (¼" proud so it shows over the cross slope); the box passes through the slab.
            MeshBuilder.AddBox(inlet, f, -dr.InletLength / 2, dr.InletLength / 2, -dr.InletWidth / 2, dr.InletWidth / 2, zSlabBot - In(6), zTop + In(0.25));
            m.Drainage.Add(new SubSolid(SubElementKind.DeckDrain, name, $"Deck drain {dr.Type}, {Sh(dr.InletLength)} x {Sh(dr.InletWidth)} grate", inlet, IsConcrete: false));
            double r = dr.PipeDiameter / 2;
            double zGirderBot = zTop - deck.Thickness - deck.Haunch - depth;
            if (dr.Outlet == DrainOutlet.FreeFall)
            {
                double zEnd = zGirderBot - dr.DropBelowGirders;
                m.Drainage.Add(new SubSolid(SubElementKind.DrainPipe, name + " downspout",
                    $"{Sh(dr.PipeDiameter)} downspout, free fall, {L(zSlabBot - In(6) - zEnd)}", Pipe(p.WithZ(zSlabBot - In(6)), p.WithZ(zEnd), r), IsConcrete: false));
                if (dr.SplashBasin)
                {
                    double gz = GroundAt(p);
                    if (double.IsNaN(gz)) { Warn($"{name}: no terrain under the drain; splash basin omitted."); continue; }
                    var basin = new Mesh();
                    MeshBuilder.AddBox(basin, new Frame3(p.WithZ(0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), Up), -Ft(5), Ft(5), -Ft(5), Ft(5), gz - Ft(2), gz);
                    m.Drainage.Add(new SubSolid(SubElementKind.SplashBasin, name + " splash basin", "Splash basin 10'-0 x 10'-0, erosion stone 2'-0 deep on engineering fabric", basin, IsConcrete: false));
                }
            }
            else
            {
                // Collector: along the girders 1'-0 below the bottom flange to the nearest pier, then down to the ground.
                var piers = _f.Supports.Where(s => s.Type == SupportType.Pier).ToList();
                if (piers.Count == 0) { Warn($"{name}: piped to a pier but the bridge has no piers."); continue; }
                var pier = piers.OrderBy(s => Math.Abs(s.Station - dr.Station)).First();
                double zRun = zGirderBot - In(12);
                var q = a.PointAt(pier.Station, edge);
                double gz = GroundAt(q);
                double zDown = double.IsNaN(gz) ? zRun - Ft(20) : gz;
                m.Drainage.Add(new SubSolid(SubElementKind.DrainPipe, name + " drop", $"{Sh(dr.PipeDiameter)} drop pipe", Pipe(p.WithZ(zSlabBot - In(6)), p.WithZ(zRun), r), IsConcrete: false));
                m.Drainage.Add(new SubSolid(SubElementKind.DrainPipe, name + " collector", $"{Sh(dr.PipeDiameter)} collector pipe to {pier.Name}, {L((q - p).Length)}", Pipe(p.WithZ(zRun), q.WithZ(zRun), r), IsConcrete: false));
                m.Drainage.Add(new SubSolid(SubElementKind.DrainPipe, name + " pier downspout", $"{Sh(Math.Max(dr.PipeDiameter, In(12)))} pier downspout at {pier.Name}", Pipe(q.WithZ(zRun), q.WithZ(zDown), Math.Max(r, In(6))), IsConcrete: false));
            }
        }
        Note($"Deck drainage: {def.Drains.Count} drains, {m.Drainage.Count(x => x.Kind == SubElementKind.SplashBasin)} splash basins.");
    }

    /// <summary>Where the cross-slope pivot (crown) crosses the support line, as a distance along it.</summary>
    private static double CrownOnSupportLine(SupportLineResult sl, Alignments.Alignment a)
    {
        double pivot = a.CrossSlope.PivotOffset;
        var r = a.RightNormalAt(sl.Station);
        double dot = Vec2.Dot(sl.Direction, r);
        return Math.Abs(dot) > 1e-6 ? pivot / dot : 0;
    }

    private static Vec2[] ColumnSection(SubstructureDefinition sd)
    {
        if (sd.ColumnShape == ColumnShape.Rectangular) return Octagon(sd.ColumnWidth, sd.ColumnDepth, sd.ColumnChamferS, sd.ColumnChamferN);
        return Circle(sd.ColumnDiameter / 2, 32);
    }

    /// <summary>Rectangle w (along S) x d (along N) with corner chamfers cs x cn (0 = square), counter-clockwise.</summary>
    private static Vec2[] Octagon(double w, double d, double cs, double cn)
    {
        double a = w / 2, b = d / 2;
        if (cs <= 0 || cn <= 0) return new[] { new Vec2(-a, -b), new Vec2(a, -b), new Vec2(a, b), new Vec2(-a, b) };
        return new[]
        {
            new Vec2(-a + cs, -b), new Vec2(a - cs, -b), new Vec2(a, -b + cn), new Vec2(a, b - cn),
            new Vec2(a - cs, b), new Vec2(-a + cs, b), new Vec2(-a, b - cn), new Vec2(-a, -b + cn),
        };
    }

    private static Vec2[] Circle(double r, int n) => Enumerable.Range(0, n).Select(i => Vec2.FromAngle(2 * Math.PI * i / n) * r).ToArray();

    /// <summary>Solid between a top and bottom profile (S, Z) extruded across the cap from n0 to n1.</summary>
    /// <summary>Lowest and highest crossing of a closed polygon with the vertical line at s.</summary>
    private static (double lo, double hi) Envelope(List<Vec2> poly, double s)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            if ((s < Math.Min(a.X, b.X)) || (s > Math.Max(a.X, b.X))) continue;
            double z = Math.Abs(b.X - a.X) < 1e-9 ? Math.Max(a.Y, b.Y) : a.Y + (b.Y - a.Y) * (s - a.X) / (b.X - a.X);
            double z2 = Math.Abs(b.X - a.X) < 1e-9 ? Math.Min(a.Y, b.Y) : z;
            lo = Math.Min(lo, z2);
            hi = Math.Max(hi, z);
        }
        return (lo, hi);
    }

    private static void ExtrudeProfile(Mesh mesh, SubstructureUnit unit, List<Vec2> top, List<Vec2> bottom, double n0, double n1)
    {
        var ss = top.Select(p => p.X).Concat(bottom.Select(p => p.X)).Distinct().OrderBy(x => x).ToList();
        // Polygon in (S, Z): top left→right, bottom right→left.
        var poly = new List<Vec2>();
        foreach (double s in ss) poly.Add(new Vec2(s, SubstructureUnit.Interp(top, s)));
        foreach (double s in Enumerable.Reverse(ss)) poly.Add(new Vec2(s, SubstructureUnit.Interp(bottom, s)));
        var r0 = poly.Select(p => unit.World(p.X, n0, p.Y)).ToArray();
        var r1 = poly.Select(p => unit.World(p.X, n1, p.Y)).ToArray();
        MeshBuilder.AddLoft(mesh, poly, new[] { r0, r1 });
    }

    private void AddFoundation(SubstructureUnit unit, SubstructureDefinition sd, double s, double n, double top, double groundZ, int index, bool underCap = false)
    {
        string where = underCap ? $"{unit.Name} {(sd.Foundation == FoundationType.PileCap ? "pile group" : "foundation")} {index}" : $"{unit.Name} column {index}";
        switch (sd.Foundation)
        {
            case FoundationType.DrilledShaft:
            {
                double bottom = top - sd.ShaftLength;
                var f = new FoundationInfo { Type = sd.Foundation, S = s, N = n, TopZ = top, BottomZ = bottom, SizeS = sd.ShaftDiameter, SizeN = sd.ShaftDiameter, GroundZ = groundZ };
                unit.Foundations.Add(f);
                var sec = Circle(sd.ShaftDiameter / 2, 32);
                var m = new Mesh();
                double topZ = underCap ? top + In(0.01) : top;
                MeshBuilder.AddLoft(m, sec, new[]
                {
                    sec.Select(q => unit.World(s + q.X, n + q.Y, bottom)).ToArray(),
                    sec.Select(q => unit.World(s + q.X, n + q.Y, underCap ? unit.CapBottomAt(s + q.X) + In(0.01) : topZ)).ToArray(),
                });
                unit.Solids.Add(new SubSolid(SubElementKind.DrilledShaft, $"{where} drilled shaft", $"Drilled shaft {Sh(sd.ShaftDiameter)} dia x {L(sd.ShaftLength)}, tip {El(bottom)}", m));
                break;
            }
            case FoundationType.SpreadFooting:
            case FoundationType.PileCap:
            {
                bool pileCapUnderAbut = underCap && sd.Foundation == FoundationType.PileCap;
                double fTop = top, fBot = top - sd.FootingThickness;
                if (!pileCapUnderAbut)
                {
                    var f = new FoundationInfo
                    {
                        Type = sd.Foundation, S = s, N = n, TopZ = fTop, BottomZ = fBot, GroundZ = groundZ,
                        SizeS = underCap ? unit.CapS1 - unit.CapS0 : sd.FootingLength,
                        SizeN = sd.FootingWidth,
                    };
                    var m = new Mesh();
                    var frame = Frame(unit, s, n);
                    var fd = underCap ? null : sd.Footings.FirstOrDefault(x => x.Column == index);
                    string shapeNote = "";
                    if (fd is { Outline.Count: >= 3 })
                    {
                        // As-designed outline (e.g. a corner clipped around an existing utility).
                        var poly = fd.Outline.Select(p => new Vec2(p.S, p.N)).ToList();
                        if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
                        MeshBuilder.AddExtrusion(m, poly, frame, fBot, fTop);
                        shapeNote = ", as designed (clipped)";
                        f = new FoundationInfo
                        {
                            Type = f.Type, S = f.S, N = f.N, TopZ = f.TopZ, BottomZ = f.BottomZ, GroundZ = f.GroundZ,
                            SizeS = poly.Max(p => p.X) - poly.Min(p => p.X), SizeN = poly.Max(p => p.Y) - poly.Min(p => p.Y),
                        };
                    }
                    else MeshBuilder.AddBox(m, frame, -f.SizeS / 2, f.SizeS / 2, -f.SizeN / 2, f.SizeN / 2, fBot, fTop);
                    unit.Solids.Add(new SubSolid(SubElementKind.Footing, $"{where} footing",
                        $"{(sd.Foundation == FoundationType.PileCap ? "Pile cap" : "Spread footing")} {L(f.SizeS)} x {L(f.SizeN)} x {Sh(sd.FootingThickness)}{shapeNote}", m));
                    unit.Foundations.Add(f);
                    if (sd.Foundation == FoundationType.PileCap)
                    {
                        if (fd is { Piles.Count: > 0 }) AddPilesAt(unit, sd, f, s, n, fBot, fd.Piles, double.IsNaN(fd.PileLength) ? sd.PileLength : fd.PileLength, fd.BatterRatio, where, fd.PileWeb);
                        else AddPiles(unit, sd, f, s, n, fBot, sd.PileColumns, where);
                    }
                }
                else
                {
                    // Abutment cap on piles: one pile row per foundation position along the cap.
                    var f = new FoundationInfo { Type = sd.Foundation, S = s, N = n, TopZ = top, BottomZ = top, SizeS = 0, SizeN = 0, GroundZ = groundZ };
                    unit.Foundations.Add(f);
                    AddPiles(unit, sd, f, s, n, top, 1, where);
                }
                break;
            }
        }
        if (sd.Foundation != FoundationType.None && !double.IsNaN(groundZ) && top > groundZ + Ft(0.5) && !underCap)
            Warn($"{where}: foundation top {El(top)} is above the ground ({El(groundZ)}).");
    }

    private static Frame3 Frame(SubstructureUnit unit, double s, double n)
    {
        var o = unit.World(s, n, 0);
        var x = new Vec3(unit.D.X, unit.D.Y, 0);
        var y = new Vec3(unit.N.X, unit.N.Y, 0);
        // Frame with z up; x along the cap, y = up-station normal (may be left-handed; AddLoft fixes orientation).
        return new Frame3(o, x, y, Up);
    }

    private void AddPiles(SubstructureUnit unit, SubstructureDefinition sd, FoundationInfo f, double s, double n, double capBottom, int alongCount, string where)
    {
        double top = capBottom + sd.PileEmbedment, tip = top - sd.PileLength;
        for (int a = 0; a < alongCount; a++)
            for (int r = 0; r < sd.PileRows; r++)
            {
                double ps = s + (a - (alongCount - 1) / 2.0) * sd.PileSpacing;
                double pn = n + (r - (sd.PileRows - 1) / 2.0) * sd.PileSpacing;
                f.Piles.Add(new PileInfo(ps, pn, top, tip));
                var m = new Mesh();
                var sec = PileSection(sd);
                var frame = Frame(unit, ps, pn);
                MeshBuilder.AddExtrusion(m, sec, frame, tip, top);
                bool steel = sd.PileType == PileType.SteelHP;
                string size = steel ? sd.PileSection : $"{Sh(sd.PileSize)} square precast";
                unit.Solids.Add(new SubSolid(SubElementKind.Pile, $"{where} pile {f.Piles.Count}", $"Pile {size}, {L(sd.PileLength)}, tip {El(tip)}", m, IsConcrete: !steel));
            }
    }

    /// <summary>HP shapes (AISC): depth, flange width, flange thickness, web thickness (in).</summary>
    private static readonly Dictionary<string, (double d, double bf, double tf, double tw)> Hp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HP10x42"] = (9.70, 10.1, 0.420, 0.415), ["HP10x57"] = (9.99, 10.2, 0.565, 0.565),
        ["HP12x53"] = (11.8, 12.0, 0.435, 0.435), ["HP12x63"] = (11.9, 12.1, 0.515, 0.515), ["HP12x74"] = (12.1, 12.2, 0.610, 0.605),
        ["HP14x73"] = (13.6, 14.6, 0.505, 0.505), ["HP14x89"] = (13.8, 14.7, 0.615, 0.615), ["HP14x102"] = (14.0, 14.8, 0.705, 0.705),
        ["HP14x117"] = (14.2, 14.9, 0.805, 0.805), ["HP16x101"] = (15.5, 15.8, 0.685, 0.685), ["HP18x135"] = (17.5, 17.8, 0.750, 0.750),
    };

    public static IReadOnlyCollection<string> HpSections => Hp.Keys;

    private static Vec2[] PileSection(SubstructureDefinition sd)
    {
        if (sd.PileType == PileType.PrecastConcrete)
        {
            double h = sd.PileSize / 2;
            return new[] { new Vec2(-h, -h), new Vec2(h, -h), new Vec2(h, h), new Vec2(-h, h) };
        }
        var (d, bf, tf, tw) = Hp.TryGetValue(sd.PileSection.Replace(" ", ""), out var v) ? v : Hp["HP14x73"];
        double x = In(bf) / 2, y = In(d) / 2, t = In(tf), w = In(tw) / 2;
        // H section, flanges parallel to the cap (x), web across.
        return new[]
        {
            new Vec2(-x, -y), new Vec2(x, -y), new Vec2(x, -y + t), new Vec2(w, -y + t), new Vec2(w, y - t), new Vec2(x, y - t),
            new Vec2(x, y), new Vec2(-x, y), new Vec2(-x, y - t), new Vec2(-w, y - t), new Vec2(-w, -y + t), new Vec2(-x, -y + t),
        };
    }
}
