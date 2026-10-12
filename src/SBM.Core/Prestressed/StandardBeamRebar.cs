using SBM.Core.Bridge;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Prestressed;

/// <summary>Edition of the Iowa BTD beam standard sheets (4730 data / 4732 BTD55 / 4733 BTD60 / 4742 BTD105).</summary>
public enum IowaBtdEdition
{
    /// <summary>Sheets issued 05-04, revised 08-09 (4742: 10-07), as printed in Woodbury Design 817 (2015), PDF p.27-30 = DS 25-28.</summary>
    Issue2004Rev2009,
    /// <summary>Sheets re-issued 04-2024 (Iowa Beams.pdf p.98 / 100 / 101 / 110 in dist\standards).</summary>
    Reissue2024,
}

/// <summary>
/// Mild reinforcing of standard precast beams as <see cref="BeamReinforcementDefinition"/>s (bars per the STANDARD sheet).
/// <list type="bullet">
/// <item>Iowa BTD55 / BTD60 / BTD105 (<see cref="IowaBtd"/>): stirrup / bar stations from the dimension chains of the beam elevation
/// (which bar sits on which station read from the to-scale CAD elevation and checked against the bar list counts), shapes from the
/// Bent Bar Detail of sheet 4730 (out-to-out, pins #4 D = 2", #5 D = 2 5/8" (5b1), #6 D = 4 1/2"), section positions from Sections
/// A-A / B-B / C-C, counts / lengths / weight from the 4730 bar list and data table.</item>
/// <item>MnDOT 30MH (<see cref="MnDot30Mh"/>): bar shapes of Fig. 5-397.501(B) (G301E, G303E, G402E, G505E, G508E, G806E); the stirrup
/// spacing is a designer input on that figure, so the plan's chain is a parameter.</item>
/// </list>
/// Centerline = out-to-out face - db / 2; a bar "spaced with" another one at the same dimensioned station is set against it
/// (<see cref="BeamBarSet.AlongOffset"/> = sum of the radii, drawn adjacent on the sheets).
/// </summary>
public static class StandardBeamRebar
{
    private static BeamBarPoint Pt(double a, double x, double y) => new(In(a), In(x), In(y));
    private static List<double> Ins(IEnumerable<double> v) => v.Select(In).ToList();
    private static List<double> Chain(double first, params (int, double)[] g) => PrestressedBeamRebar.Chain(first, g);

    /// <summary>Open polyline offset to the left (+1) or right (-1) of the direction of travel by d, mitred corners (2D).</summary>
    public static List<Vec2> Offset(IReadOnlyList<Vec2> p, double d, int side)
    {
        var n = new List<Vec2>();
        for (int i = 0; i + 1 < p.Count; i++) n.Add((p[i + 1] - p[i]).Normalized().Left * (side * d));
        var r = new List<Vec2> { p[0] + n[0] };
        for (int i = 1; i + 1 < p.Count; i++)
        {
            var a0 = p[i - 1] + n[i - 1];
            var da = p[i] - p[i - 1];
            var b0 = p[i] + n[i];
            var db = p[i + 1] - p[i];
            double den = Vec2.Cross(da, db);
            if (Math.Abs(den) < 1e-12) { r.Add(p[i] + n[i]); continue; }
            double t = Vec2.Cross(b0 - a0, db) / den;
            r.Add(a0 + da * t);
        }
        r.Add(p[^1] + n[^1]);
        return r;
    }

    // =========================================================================================== Iowa BTD

    /// <summary>Iowa BTD beam reinforcing for a designation ("BTD55", "BTD60", "BTD105") and sheet edition; null if not transcribed.</summary>
    public static BeamReinforcementDefinition? IowaBtd(string designation, IowaBtdEdition edition)
    {
        string n = designation.ToUpperInvariant().Replace(" ", "");
        bool e15 = edition == IowaBtdEdition.Issue2004Rev2009;
        // ---- stations per half (inches from the end of beam): end zone (6b3 / 6b4 / 4d1 / 4e1 / 4c1) and the 5b1 chain
        List<double> ez, b3, b4, d1ez, c1extra, b1;
        double half;
        (int Count, double LenIn)[] list;   // 5a1, 5a2, 5b1, 6b3, 6b4, 4c1, 4d1, 4e1, 4h1
        double weight;
        string sheet, page;
        switch (n)
        {
            case "BTD55" when e15:
                half = 338; ez = new() { 1.5, 6, 10.5, 15, 21, 27, 33, 39, 45, 51 }; b3 = new() { 6, 15, 21, 27, 33, 39, 45, 51 }; b4 = new() { 1.5, 10.5 };
                d1ez = ez; b1 = Chain(59, (3, 9), (4, 12), (4, 15), (8, 18)); c1extra = Chain(203, (7, 18));
                list = new[] { (12, 350.0), (0, 0), (39, 128), (32, 69), (8, 61), (73, 31), (59, 77), (26, 38), (6, 96) };
                weight = 1602; sheet = "4732"; page = "Woodbury Design 817 PDF p.28 (DS 26)";
                break;
            case "BTD60" when e15:
                half = 368; ez = new() { 1.5, 6, 10.5, 15, 21, 27, 33, 39, 45, 51 }; b3 = new() { 6, 15, 21, 27, 33, 39, 45, 51 }; b4 = new() { 1.5, 10.5 };
                d1ez = ez; b1 = Chain(59, (5, 9), (2, 12), (4, 15), (10, 18)); c1extra = Chain(197, (9, 18));
                list = new[] { (12, 380.0), (0, 0), (43, 128), (32, 69), (8, 61), (81, 31), (63, 77), (26, 38), (6, 96) };
                weight = 1709; sheet = "4733"; page = "Woodbury Design 817 PDF p.29 (DS 27)";
                break;
            case "BTD105" when e15:
                half = 638; ez = new() { 1.5, 4.25, 7, 10.5, 14, 17.5, 21, 27, 33, 39, 45, 51 }; b3 = new() { 4.25, 10.5, 17.5, 27, 33, 39, 45, 51 };
                b4 = new() { 1.5, 7, 14, 21 }; d1ez = new() { 1.5, 4.25, 10.5, 17.5, 21, 27, 33, 39, 45, 51 };
                b1 = Chain(59, (9, 9), (6, 12), (8, 15), (17, 18)); c1extra = Chain(341, (16, 18));
                list = new[] { (12, 423.0), (6, 480), (81, 128), (32, 69), (16, 61), (133, 31), (101, 77), (26, 38), (6, 96) };
                weight = 2741; sheet = "4742"; page = "Woodbury Design 817 PDF p.30 (DS 28)";
                break;
            case "BTD55":
                half = 338; ez = new() { 1.5, 4.5, 9, 13.5, 18, 22.5, 27, 32, 38, 44, 50, 56 }; b3 = new() { 4.5, 13.5, 22.5, 32, 38, 44, 50, 56 };
                b4 = new() { 1.5, 9, 18, 27 }; d1ez = ez; b1 = Chain(62, (1, 6), (2, 9), (4, 12), (4, 15), (8, 18)); c1extra = Chain(203, (7, 18));
                list = new[] { (12, 357.0), (0, 0), (39, 128), (32, 69), (16, 61), (73, 31), (63, 77), (30, 38), (6, 96) };
                weight = 1696; sheet = "4732"; page = "Iowa Beams.pdf p.100";
                break;
            case "BTD60":
                half = 368; ez = new() { 1.5, 4.5, 9, 13.5, 18, 22.5, 27, 32, 38, 44, 50, 56 }; b3 = new() { 4.5, 13.5, 22.5, 32, 38, 44, 50, 56 };
                b4 = new() { 1.5, 9, 18, 27 }; d1ez = ez; b1 = Chain(62, (1, 6), (4, 9), (2, 12), (4, 15), (10, 18)); c1extra = Chain(197, (9, 18));
                list = new[] { (12, 387.0), (0, 0), (43, 128), (32, 69), (16, 61), (81, 31), (67, 77), (30, 38), (6, 96) };
                weight = 1803; sheet = "4733"; page = "Iowa Beams.pdf p.101";
                break;
            case "BTD105":
                half = 638; ez = new() { 1.5, 4.25, 7, 10.5, 14, 17.5, 21, 26, 32, 38, 44, 50, 56 }; b3 = new() { 4.25, 10.5, 17.5, 26, 32, 38, 44, 50, 56 };
                b4 = new() { 1.5, 7, 14, 21 }; d1ez = new() { 1.5, 4.25, 10.5, 17.5, 21, 26, 32, 38, 44, 50, 56 };
                b1 = Chain(62, (1, 6), (8, 9), (6, 12), (8, 15), (17, 18)); c1extra = Chain(341, (16, 18));
                list = new[] { (12, 438.0), (6, 480), (81, 128), (36, 69), (16, 61), (135, 31), (103, 77), (28, 38), (6, 96) };
                weight = 2795; sheet = "4742"; page = "Iowa Beams.pdf p.110";
                break;
            default:
                return null;
        }
        string dataPage = e15 ? "Woodbury Design 817 PDF p.27 (DS 25)" : "Iowa Beams.pdf p.98";
        string ed = e15 ? "issued 05-04, rev. 08-09, as printed in the plans" : "re-issued 04-2024";
        string src = $"Iowa DOT std {sheet} {n} + 4730 ({ed}): {page}, {dataPage}";
        var e1 = d1ez.Concat(b1.Where(s => s <= 77 + 1e-6)).ToList();
        var c1 = new List<double> { 1.5 }.Concat(b3).Concat(b1).Concat(c1extra).OrderBy(s => s).ToList();
        var d1 = d1ez.Concat(b1).ToList();
        // 5b1 / 6b3 sit on the dimensioned stations; 4c1 / 4d1 are drawn against them toward midspan, 4e1 toward the end
        double adj = 0.3125 + 0.25;
        var r = new BeamReinforcementDefinition
        {
            Source = src, ListedWeightLb = weight, ListedWeightSource = $"4730 BTD Beam Data, {dataPage}",
            BarList = new()
            {
                Row("5a1", list[0]), Row("5a2", list[1]), Row("5b1", list[2]), Row("6b3", list[3]), Row("6b4", list[4]),
                Row("4c1", list[5]), Row("4d1", list[6]), Row("4e1", list[7]), Row("4h1", list[8]),
            },
        };
        r.BarList.RemoveAll(x => x.Count == 0);
        // 5b1 web hairpin (epoxy), 5" above the beam, 1 5/16" clear to the web faces (3 7/8" o-o), legs 4'-9 o-o to the 6" feet 2" above the
        // bottom (feet toward midspan, both legs: the front view shows the leg ends turned), D = 2 5/8".
        r.Bars.Add(new BeamBarSet
        {
            Mark = "5b1", Size = "#5", Epoxy = true, ShapeCode = "HAIRPIN", Description = "web hairpin with 6 in feet, projects 5 in above the beam",
            Location = "web stirrup (5b1, 4c1, 4d1 chain)", BendRadius = In(1.625),
            Path = { Pt(5.6875, -1.625, 2.3125), Pt(0, -1.625, 2.3125), Pt(0, -1.625, 58.6875), Pt(0, 1.625, 58.6875), Pt(0, 1.625, 2.3125), Pt(5.6875, 1.625, 2.3125) },
            Stations = Ins(b1), PrintedDims = Ins(new[] { 6.0, 57, 3.875, 57, 6 }), ListedLength = In(list[2].LenIn),
        });
        // 6b3 end-zone Z bars in pairs (epoxy): 6" top leg across, 4'-8 1/2 vertical 1" clear to the web face, 6" foot outward; 5" above the beam.
        r.Bars.Add(new BeamBarSet
        {
            Mark = "6b3", Size = "#6", Epoxy = true, ShapeCode = "Z", Description = "end-zone bar (pair), 6 in top leg / 6 in foot",
            Location = "end zone, pairs (2-6b3)", BendRadius = In(2.625), Mirror = true,
            Path = { Pt(0, 3.75, 58.625), Pt(0, -1.875, 58.625), Pt(0, -1.875, 2.875), Pt(0, -7.5, 2.875) },
            Stations = Ins(b3), PrintedDims = Ins(new[] { 6.0, 56.5, 6 }), ListedLength = In(list[3].LenIn),
        });
        // 6b4 end-zone U (pairs with 6b3): 4'-1 vertical 1" clear to the web face, 6" legs across at the top (under 4c1) and bottom.
        r.Bars.Add(new BeamBarSet
        {
            Mark = "6b4", Size = "#6", ShapeCode = "U", Description = "end-zone U (pair), 6 in legs", Location = "end zone, pairs (2-6b4)",
            BendRadius = In(2.625), Mirror = true,
            Path = { Pt(0, 3.75, 51.375), Pt(0, -1.875, 51.375), Pt(0, -1.875, 3.125), Pt(0, 3.75, 3.125) },
            Stations = Ins(b4), PrintedDims = Ins(new[] { 6.0, 49, 6 }), ListedLength = In(list[4].LenIn),
        });
        // 4c1 top flange transverse 2'-7, 2" below the top.
        r.Bars.Add(new BeamBarSet
        {
            Mark = "4c1", Size = "#4", ShapeCode = "STR", Description = "top flange transverse", Location = "top flange transverse",
            Path = { Pt(0, -15.5, 52), Pt(0, 15.5, 52) }, Stations = Ins(c1), AlongOffset = In(adj), PrintedDims = Ins(new[] { 31.0 }), ListedLength = In(list[5].LenIn),
        });
        // 4d1 bottom flange stirrup: 2'-3 1/2 o-o on 1" clear, 5 1/2 sides, sloped legs (rise 5 1/8) to two 6" legs 4" apart (inside) in the web.
        var d1Out = new List<Vec2> { new(-2.5, 17.625), new(-2.5, 11.625), new(-13.75, 6.5), new(-13.75, 1.0), new(13.75, 1.0), new(13.75, 6.5), new(2.5, 11.625), new(2.5, 17.625) };
        var d1C = Offset(d1Out, 0.25, +1);
        var d1Set = new BeamBarSet
        {
            Mark = "4d1", Size = "#4", ShapeCode = "STIRRUP", Description = "bottom flange stirrup", Location = "bottom flange confinement",
            BendRadius = In(1.25), Stations = Ins(d1), AlongOffset = In(adj), PrintedDims = Ins(new[] { 6.0, 13, 5.5, 27.5, 5.5, 13, 6 }), ListedLength = In(list[6].LenIn),
        };
        d1Set.Path.AddRange(d1C.Select(q => Pt(0, q.X, q.Y)));
        r.Bars.Add(d1Set);
        // 4e1 bottom flange U over the strands (2'-3 1/2 o-o, 5" legs down), on the 6" strand row.
        r.Bars.Add(new BeamBarSet
        {
            Mark = "4e1", Size = "#4", ShapeCode = "U", Description = "bottom flange U (legs down)", Location = "end zone bottom flange",
            BendRadius = In(1.25), Path = { Pt(0, -13.5, 1.8), Pt(0, -13.5, 6.55), Pt(0, 13.5, 6.55), Pt(0, 13.5, 1.8) },
            Stations = Ins(e1), AlongOffset = In(-adj), PrintedDims = Ins(new[] { 5.0, 27.5, 5 }), ListedLength = In(list[7].LenIn),
        });
        // 4h1 horizontal hairpins in the web at the ends, 1'-2 + 2 @ 6 above the bottom, 3" o-o, 3'-11 legs from 3/4" off the end.
        r.Bars.Add(new BeamBarSet
        {
            Mark = "4h1", Size = "#4", ShapeCode = "HAIRPIN", Description = "horizontal web hairpin at the end (3 levels)", Location = "beam ends, web (4h1 bars)",
            BendRadius = In(1.25), Path = { Pt(47.75, -1.25, 14), Pt(1.0, -1.25, 14), Pt(1.0, 1.25, 14), Pt(47.75, 1.25, 14) },
            Copies = new() { new(0, 0), new(0, In(6)), new(0, In(12)) }, Stations = new() { 0 },
            PrintedDims = Ins(new[] { 47.0, 3, 47 }), ListedLength = In(list[8].LenIn),
        });
        // 5a1 (5a2) top flange longitudinal, 6 lines, 1 1/2" clear to the end of beam (at the bar), lapped at / about midspan.
        var lines = new List<BeamBarOffset> { new(In(-15), 0), new(In(-9.3125), 0), new(In(-2.6875), 0), new(In(2.6875), 0), new(In(9.3125), 0), new(In(15), 0) };
        r.Bars.Add(new BeamBarSet
        {
            Mark = "5a1", Size = "#5", ShapeCode = "STR", Description = "top flange longitudinal", Location = "top flange longitudinal, 6 lines",
            Path = { Pt(0, 0, 52.5625), Pt(list[0].LenIn, 0, 52.5625) }, Copies = lines, Stations = new() { In(1.5) }, StationAtBar = true,
            PrintedDims = Ins(new[] { list[0].LenIn }), ListedLength = In(list[0].LenIn),
        });
        if (list[1].Count > 0)
            r.Bars.Add(new BeamBarSet
            {
                Mark = "5a2", Size = "#5", ShapeCode = "STR", Description = "top flange longitudinal, centre", Location = "top flange longitudinal, 6 lines, centred on midspan",
                Path = { Pt(0, 0, 52.5625), Pt(list[1].LenIn, 0, 52.5625) }, Copies = lines.Select(o => new BeamBarOffset(o.X, o.Y)).ToList(),
                Stations = new() { In(half - list[1].LenIn / 2) }, Ends = BeamBarEnds.StartOnly, StationAtBar = true,
                PrintedDims = Ins(new[] { list[1].LenIn }), ListedLength = In(list[1].LenIn),
            });
        string sec = $"std {sheet} Sections A-A / B-B / C-C ({page})";
        r.Unresolved.Add(UnresolvedFlag.Text(page, "5a1 lateral positions of the four inner top flange lines",
            "Section A-A dimensions only the outer lines (2 in from the flange edges); the inner lines are drawn but not dimensioned",
            "Modeled at the positions scaled from the to-scale CAD section: 2 11/16 and 9 5/16 in each side of the CL."));
        r.Unresolved.Add(UnresolvedFlag.Text(page, "heights of 4d1 / 4e1 / 6b4 / 6b3 / 4c1 in the section",
            $"{sec} give only the projections (5 in), 1 5/16 in / 1 in clear to the web faces, 2 in to the top flange bars and the 4h1 levels; the bottom flange bars are drawn, not dimensioned",
            "Modeled: 4d1 1 in clear to the bottom, 4e1 resting on the 6 in strand row, 6b4 top just under 4c1 (4'-1 o-o down), 6b3 from 5 in above the beam (4'-8 1/2 o-o), 4c1 centre 2 in below the top (as drawn)."));
        r.Notes.Add($"{n} 5b1 / 4h1 (U9b): shapes as drawn on the 4730 Bent Bar Detail (5b1 legs 4'-9 o-o, 3 7/8 wide, D = 2 5/8, 6 in feet; 4h1 legs 3'-11, 3 in wide); " +
                    "the drawn out-to-out dimensions do not add up to the bar list lengths 10'-8 / 8'-0 (bend allowances not stated) - scheduled at the bar list length.");
        if (n == "BTD105" && !e15)
            r.Notes.Add("BTD105 (4730 re-issue 2024): the BTD Beam Data weight 2,795 lb is the bar list with 5a1 = 35'-6; the bar list prints 36'-6 (needed for the 3'-5 min. lap to 5a2) - the bar list governs (2,807 lb).");
        r.Notes.Add($"{n} bar stations: chains of the {sheet} elevation ({page}); which bar sits on which station read from the to-scale CAD elevation and matching the 4730 bar list counts per beam.");
        return r;
    }

    private static BeamBarListRow Row(string mark, (int Count, double LenIn) v) => new() { Mark = mark, Count = v.Count, Length = In(v.LenIn) };

    // =========================================================================================== MnDOT 30MH

    /// <summary>
    /// MnDOT 30MH beam bars (Fig. 5-397.501(B), "pretensioned with debonded strands"): shapes and section positions of the figure, the
    /// stirrup chain per half (<paramref name="stationsIn"/>, inches from the end of beam, symmetric about midspan) and how many of the first
    /// stations take the splitting reinforcement G505E (pairs) and G508E (pairs); every other station a G402E hairpin ("G402E SPG. EXCEPT AS
    /// NOTED"); G301E at every station ("SPACE WITH G505E, G508E OR G402E"), 2-G303E with every G505E / G508E. Pins are not shown on the
    /// sheets: G402E bends about its 4" o-o width, the others are modeled with sharp corners. G806E (no length) and the G303E paired with
    /// G402E (zone not dimensioned) are UNRESOLVED and not placed.
    /// </summary>
    public static BeamReinforcementDefinition MnDot30Mh(IReadOnlyList<double> stationsIn, int g505, int g508, string source, string pages)
    {
        var st = stationsIn.ToList();
        var s505 = st.Take(g505).ToList();
        var s508 = st.Skip(g505).Take(g508).ToList();
        var s402 = st.Skip(g505 + g508).ToList();
        double adj = 0.5; // G301E / G303E (#3) against the stirrup (#4 / #5)
        var r = new BeamReinforcementDefinition { Source = source };
        // G402E hairpin 2'-10 x 4 o-o, 7" above the beam
        r.Bars.Add(new BeamBarSet
        {
            Mark = "G402E", Size = "#4", Epoxy = true, ShapeCode = "HAIRPIN", Description = "web hairpin 2'-10 x 4 in o-o, projects 7 in above the beam",
            Location = "web stirrup (G402E spacing)", BendRadius = In(1.75),
            Path = { Pt(0, -1.75, 3), Pt(0, -1.75, 36.75), Pt(0, 1.75, 36.75), Pt(0, 1.75, 3) }, Stations = Ins(s402), PrintedDims = Ins(new[] { 34.0, 4 }),
        });
        // G505E C bar in the longitudinal plane, 1 1/4" clear to the web faces, 2" clear to the top: 2'-1 vertical, 3'-6 top flange leg, 1'-0 bottom leg (into the beam)
        r.Bars.Add(new BeamBarSet
        {
            Mark = "G505E", Size = "#5", Epoxy = true, ShapeCode = "C", Description = "end bar (pair): 2'-1 vertical, 3'-6 top flange leg, 1'-0 bottom leg",
            Location = "beam end, splitting reinforcement (2-G505E)", Mirror = true,
            Path = { Pt(41.6875, -1.6875, 27.6875), Pt(0, -1.6875, 27.6875), Pt(0, -1.6875, 3.3125), Pt(11.6875, -1.6875, 3.3125) },
            Stations = Ins(s505), PrintedDims = Ins(new[] { 42.0, 25, 12 }),
        });
        // G508E L: 2'-10 vertical (at the G505E lines, end view) projecting 7" above the beam, 1'-0 top leg across the beam
        r.Bars.Add(new BeamBarSet
        {
            Mark = "G508E", Size = "#5", Epoxy = true, ShapeCode = "L", Description = "end bar (pair): 2'-10 vertical, 1'-0 top leg, projects 7 in",
            Location = "beam end, splitting reinforcement (2-G508E)", Mirror = true,
            Path = { Pt(0, -1.6875, 3), Pt(0, -1.6875, 36.6875), Pt(0, 10.0, 36.6875) }, Stations = Ins(s508), PrintedDims = Ins(new[] { 34.0, 12 }),
        });
        // G301E straight 2'-6, 1 1/2" clear to the top, at every stirrup station
        r.Bars.Add(new BeamBarSet
        {
            Mark = "G301E", Size = "#3", Epoxy = true, ShapeCode = "STR", Description = "top flange transverse 2'-6", Location = "top flange transverse (with every stirrup)",
            Path = { Pt(0, -15, 28.3125), Pt(0, 15, 28.3125) }, Stations = Ins(st), AlongOffset = In(adj), PrintedDims = Ins(new[] { 30.0 }),
        });
        // G303E bottom flange bar: 2'-11 bottom, 3 1/2 offset, 1'-10 at 4:12; 1 1/8" clear to the bottom flange; pairs crossing
        double run = 22 * 12 / Math.Sqrt(160), rise = 22 * 4 / Math.Sqrt(160);
        var g3Out = new List<Vec2> { new(16.625, 1.125), new(-18.375, 1.125), new(-18.375, 4.625), new(-18.375 + run, 4.625 + rise) };
        var g3 = new BeamBarSet
        {
            Mark = "G303E", Size = "#3", Epoxy = true, ShapeCode = "BENT", Description = "bottom flange bar (pair): 2'-11 + 3 1/2 + 1'-10 @ 4:12",
            Location = "bottom flange (2-G303E with G505E / G508E)", Mirror = true, Stations = Ins(s505.Concat(s508)), AlongOffset = In(adj),
            PrintedDims = Ins(new[] { 35.0, 3.5, 22 }),
        };
        g3.Path.AddRange(Offset(g3Out, 0.1875, -1).Select(q => Pt(0, q.X, q.Y)));
        r.Bars.Add(g3);
        r.Bars.Add(new BeamBarSet
        {
            Mark = "G303E", Size = "#3", Epoxy = true, ShapeCode = "BENT", Location = "bottom flange (2-G303E with G402E)",
            Unresolved = UnresolvedFlag.Text(pages, "2-G303E spaced with G402E (bottom flange bars) - extent of the zone",
                "the dimension line runs into the break of the half elevation with no end tick, count or length, and Fig. 5-397.501(B) draws the same open line",
                "Not placed (G303E are placed only with G505E / G508E)."),
        });
        r.Bars.Add(new BeamBarSet
        {
            Mark = "G806E", Size = "#8", Epoxy = true, ShapeCode = "STR", Location = "top flange longitudinal 3 1/2 + 3 @ 9 + 3 1/2",
            Unresolved = UnresolvedFlag.Text(pages, "4-G806E top flange longitudinal bars - bar length / end distance",
                "the sheet has no bar bill and the elevation does not dimension where the bars stop (only a permissible splice, 5'-1 min. lap)",
                "Not placed (positions across the flange are known: 3 1/2 + 3 @ 9 + 3 1/2)."),
        });
        r.Notes.Add($"30MH G505E / G508E stations (U11, {pages}): leaders 2-G505E (first station) and 2-G508E + 2-G303E (SP. W/ G508E) over the rest of the first group; " +
                    "Fig. 5-397.501(B) marks that group '2 1/2\" MIN. SPG. FOR SPLITTING REINF.' and G402E 'SPG. EXCEPT AS NOTED' - so G505E at the first, G508E at the other " +
                    $"{g508} stations of the 2 1/2\" group, G402E from the next group on.");
        r.Notes.Add($"30MH bar lengths: no bar bill (beam bars are paid with the beam, {pages}); scheduled at the centerline length; pins not shown (G402E bent about its 4 in o-o width, others sharp). " +
                    "Optional 2 x 1/2 in top strands (contractor's option) not modeled.");
        return r;
    }
}
