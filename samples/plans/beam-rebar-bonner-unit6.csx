// NCDOT B-2500 Bonner Unit 6 - FIB-96 girder MILD REINFORCING (PrestressedBeamDefinition.Reinforcement), as the girder sheets show it.
// No FDOT standard is cited in the set: the bars come from the plan sheets only. Called by build-bonner-unit6.csx before regeneration (re-runnable).
//   sbm run samples\plans\beam-rebar-bonner-unit6.csx "<model.sbm>"
// Plan data (p. = PDF page):
//   p.428 (S4-36, span 30), p.429 (S4-37, spans 31-34), p.430 (S4-38, span 35 G1-G2), p.431 (S4-39, span 35 G3-G4): ELEVATION OF GIRDER spacing chains,
//          REINFORCING STEEL FOR ONE GDR (bar lists), BAR TYPES (out-to-out dimensions), girder data table DIM "C".
//          Chains (from each end of the girder): "PAIRS S1 OR S5" 2 1/2" + 5 spa @ 3 1/2" + 21 spa @ 6" + 10 spa @ 1'-0" (S1), "C", then S5 @ 1'-6"
//          (span 30: 36 spa + 1'-6" + 1'-6" + 36 spa; spans 31-35: 37 spa + 1'-3" + 1'-3" + 37 spa); "S6 OR S8" 6" + 2 spa @ 7" + 7 spa @ 1'-6" +
//          8 spa @ 1'-3", "C", then S6 (span 30) / S8 (spans 31-35) spaced with the S5 bars. The chains close on DIM "A" (e.g. span 30: 1,899 1/2" = 158'-3 1/2").
//   p.438 (S4-46) Sections A-A / C-C / E-E: S1 1 3/4" clear to S3 (#3) at the web faces, S5 2 1/4" clear to the web faces (7" web), pairs = one bar each side
//          of the girder CL with the top 6" leg outward; S6 / S8 = U across the top flange; PROJECTION TABLE (S1 / S5 / S6 / S8 above the girder top).
//   Bar types (p.428-431): type 1 = 6" top leg + vertical + 8" bottom leg (Z: top leg out, bottom leg in); type 4 = U 3'-4 1/2" wide, legs 9 3/4" (S6) /
//          8 1/4" (S8), 6" returns.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Prestressed;

var br = Doc.Project.Bridges.First(b => b.Name == "Bonner Unit 6");
BeamBarPoint Pt(double a, double x, double y) => new(In(a), In(x), In(y));
List<double> Ins(IEnumerable<double> v) => v.Select(x => In(x)).ToList();
double F(double ft, double inch = 0) => Ft(ft) + In(inch);

// per span / girder group: (S1 length in, S5 length in, S6 / S8 projection, C in, sheet, bar list rows, listed weight)
record Group(string Sheet, double S1, double S5, double C, bool MidS6, (string mark, int n, double lenIn)[] List, double Weight, bool S1Varies, double A);
Group G(int span, int g)
{
    if (span == 30) return new("p.428 (S4-36)", 115, 115, 17.75, true,
        new[] { ("S1", 148, 115.0), ("S2", 54, 38.5), ("S3", 16, 99.5), ("S4", 302, 51.375), ("S5", 150, 115.0), ("S6", 111, 72.0), ("S7", 10, 73.0), ("V1", 16, 88.0) }, 3854, false, 1899.5);
    double c = span switch { 31 or 32 => 13.9375, 33 or 34 => 13.5625, _ => g switch { 1 => 13.625, 2 => 13.4375, 3 => 13.3125, _ => 13.1875 } };
    if (span < 35) return new("p.429 (S4-37)", 114.5, 112.5, c, false,
        new[] { ("S1", 148, 114.5), ("S2", 54, 38.5), ("S3", 16, 99.5), ("S4", 302, 51.375), ("S5", 154, 112.5), ("S6", 36, 72.0), ("S7", 20, 73.0), ("S8", 77, 69.0), ("V1", 16, 88.0) }, 3941, false, span <= 32 ? 1921.875 : 1921.125);
    if (g <= 2) return new("p.430 (S4-38)", 115, 112.5, c, false,
        new[] { ("S1", 148, 115.0), ("S2", 54, 38.5), ("S3", 16, 99.5), ("S4", 302, 51.375), ("S5", 154, 112.5), ("S6", 36, 72.0), ("S7", 20, 73.0), ("S8", 77, 69.0), ("V1", 16, 88.0) }, 3947, true, g == 1 ? 1921.25 : 1920.875);
    return new("p.431 (S4-39)", 115, 113.5, c, false,
        new[] { ("S1", 148, 115.0), ("S2", 54, 38.5), ("S3", 16, 99.5), ("S4", 302, 51.375), ("S5", 154, 113.5), ("S6", 36, 72.0), ("S7", 10, 73.0), ("S8", 77, 69.0), ("V1", 16, 88.0) }, 3864, false, g == 3 ? 1920.625 : 1920.375);
}

// type 1 (S1 / S5): vertical at x = xv (right bar of the pair; Mirror = the left one), top leg 6" outward, bottom leg 8" inward, total = listed length
BeamBarSet Type1(string mark, string size, double db, double xv, double listed, List<double> stations, string loc)
{
    double v = listed - 14, yTop = 96 + (5.25 + v - 96) - db / 2;     // bottom out-to-out 5 1/4" above the girder bottom (= p.438 "5 5/16"" to 1/16")
    double yBot = yTop - v + db;
    return new BeamBarSet
    {
        Mark = mark, Size = size, ShapeCode = "STIRRUP", Description = $"type 1: 6\" top leg out, {v / 12:0}'-{v % 12:0.##}\" vertical, 8\" bottom leg in (pair)",
        Location = loc, Mirror = true,
        Path = { Pt(0, xv - db / 2 + 6 - db / 2, yTop), Pt(0, xv, yTop), Pt(0, xv, yBot), Pt(0, xv + db / 2 - 8 + db / 2, yBot) },
        Stations = Ins(stations), PrintedDims = Ins(new[] { 6.0, v, 8 }), ListedLength = In(listed),
    };
}
// type 4 (S6 / S8): U across the top flange, 3'-4 1/2" out-to-out, legs h out-to-out, 6" returns inward, top at the projection
BeamBarSet Type4(string mark, double h, double proj, List<double> stations, double listed, string loc)
{
    double db = 0.5, yT = 96 + proj - db / 2, yB = 96 + proj - h + db / 2, xo = 20.25 - db / 2, xr = 20.25 - 6 + db / 2;
    return new BeamBarSet
    {
        Mark = mark, Size = "#4", ShapeCode = "U", Description = $"type 4: U 3'-4 1/2\" x {h}\" legs, 6\" returns, projects {proj}\" above the girder",
        Location = loc,
        Path = { Pt(0, -xr, yT), Pt(0, -xo, yT), Pt(0, -xo, yB), Pt(0, xo, yB), Pt(0, xo, yT), Pt(0, xr, yT) },
        Stations = Ins(stations), PrintedDims = Ins(new[] { 6.0, h, 40.5, h, 6 }), ListedLength = In(listed),
    };
}

int set = 0;
foreach (var sb in br.Girders.Prestressed.SpanBeams)
{
    int span = 30 + sb.Spans![0], g = sb.Girders![0] + 1;
    var gr = G(span, g);
    var s1 = PrestressedBeamRebar.Chain(2.5, (5, 3.5), (21, 6), (10, 12));                    // 37 per end
    double s5first = s1[^1] + gr.C;
    // per end k = 0..36 (span 30) / 0..37 (spans 31-35) @ 1'-6"; the bar at the girder CL is placed once from the start end at A / 2 (DIM "A", girders.csv):
    // the model girder can be a fraction of an inch longer than A (UNRESOLVED 'girder length A'), so the two end chains do not meet exactly
    var s5 = span == 30 ? PrestressedBeamRebar.Chain(s5first, (36, 18)) : PrestressedBeamRebar.Chain(s5first, (37, 18));
    var mid = new List<double> { gr.A / 2 };
    var s6 = PrestressedBeamRebar.Chain(6, (2, 7), (7, 18), (8, 15));                         // 18 per end
    double proj6 = 8.5, projS8 = 7;
    var rd = new BeamReinforcementDefinition
    {
        Source = $"NCDOT B-2500 {gr.Sheet} (elevation of girder, bar list, bar types) + p.438 (S4-46) sections / projection table",
        Unit = $"FIB-96 girders {(span == 30 ? "span 30" : span < 35 ? "spans 31-34" : g <= 2 ? "span 35 G1-G2" : "span 35 G3-G4")} ({gr.Sheet})",
        BarList = gr.List.Select(x => new BeamBarListRow { Mark = x.mark, Count = x.n, Length = In(x.lenIn) }).ToList(),
        ListedWeightLb = gr.Weight, ListedWeightSource = gr.Sheet + " reinforcing steel for one girder (incl. WWF)",
    };
    rd.Bars.Add(Type1("S1", "#5", 0.625, 3.5 - 1.75 - 0.375 - 0.3125, gr.S1, s1, "end zone pairs (S1), 1 3/4\" clr to S3"));
    rd.Bars.Add(Type1("S5", "#4", 0.5, 3.5 - 2.25 - 0.25, gr.S5, s5, "web pairs (S5) @ 1'-6\", 2 1/4\" clr to the web faces"));
    { var c5 = Type1("S5", "#4", 0.5, 3.5 - 2.25 - 0.25, gr.S5, mid, "web pair (S5) at the girder CL"); c5.Ends = BeamBarEnds.StartOnly; rd.Bars.Add(c5); }
    rd.Bars.Add(Type4("S6", 9.75, proj6, s6, 72, "top flange U (S6), girder ends"));
    var u1 = gr.MidS6 ? Type4("S6", 9.75, proj6, s5, 72, "top flange U (S6) spaced with S5") : Type4("S8", 8.25, projS8, s5, 69, "top flange U (S8) spaced with S5");
    var u2 = gr.MidS6 ? Type4("S6", 9.75, proj6, mid, 72, "top flange U (S6) at the girder CL") : Type4("S8", 8.25, projS8, mid, 69, "top flange U (S8) at the girder CL");
    u2.Ends = BeamBarEnds.StartOnly;
    rd.Bars.Add(u1); rd.Bars.Add(u2);
    if (gr.S1Varies)
        rd.Unresolved.Add(UnresolvedFlag.Text("p.430 / p.438", "S1 projection of span 35 G1-G2 (the 10 spa @ 1'-0\" group, '(+) VARIES')", "the projection table says 'varies' and refers to a sheet ('sheet 3 of 12') not transcribed",
            "All S1 at the listed 9'-7\" length (10 1/4\" projection)."));
    // S3 (#3, type 5): horizontal hairpin in the web at each end - closed end 1" clear to the girder end (p.428 "1\" CLR. TO S3 BARS"), legs 4'-0" along the
    // girder, 3 1/2" out-to-out = 1 3/4" clear to the web faces (p.438 A-A); 8 levels: 2'-2 1/2", + 6 spa @ 10", + 6" above the bottom (p.438 A-A, 96" closed).
    {
        const double d3 = 0.375; double x3 = 1.75 - d3 / 2, a0 = 1 + d3 / 2, a1 = 1 + 48;
        var lv = new List<double> { 26.5, 36.5, 46.5, 56.5, 66.5, 76.5, 86.5, 92.5 };
        rd.Bars.Add(new BeamBarSet
        {
            Mark = "S3", Size = "#3", ShapeCode = "HAIRPIN", Description = "type 5: hairpin 4'-0\" legs, 3 1/2\" out-to-out, horizontal in the web", Location = "girder end, web (8-S3 each end)",
            Path = { Pt(a1, -x3, 0), Pt(a0, -x3, 0), Pt(a0, x3, 0), Pt(a1, x3, 0) }, Copies = lv.Select(y => new BeamBarOffset(0, In(y))).ToList(),
            Stations = Ins(new[] { 0.0 }), PrintedDims = Ins(new[] { 48.0, 3.5, 48 }), ListedLength = In(99.5),
        });
    }
    // S4 (#3, type 2) pairs: bottom flange confinement, "2\" CLR. (TYP. U.N.O.)" to the bottom and the 7" vertical flange face (p.438 A-A): out-to-out 1'-11 1/2"
    // bottom run inward, 3 1/4" up the side, 9 5/8" slope (8 1/2" run, 4 1/2" rise) inward, 1'-3" top run inward; at the S2 stations (2-S4, DIM X 1 13/16"
    // clear to S2 = first station 2"), the S1 1'-0" group ("2-S4 SPA. WITH S1") and every S5 station ("2-S4 SPA. WITH S5").
    {
        const double d4 = 0.375; double h = d4 / 2;
        double xo = 19 - 2 - h, yb = 2 + h, yk = 2 + 3.25 - h, xk = xo - 8.5 + h * 0.5, yt = 2 + 3.25 + 4.5 - h;
        var path = new List<BeamBarPoint> { Pt(0, xo - 23.5 + h, yb), Pt(0, xo, yb), Pt(0, xo, yk), Pt(0, xk, yt), Pt(0, xk - 15 + h, yt) };
        var s2st = PrestressedBeamRebar.Chain(2.0, (5, 3.5), (21, 6));
        var st4 = s2st.Concat(s1.Skip(27)).ToList();
        BeamBarSet S4(List<double> st, string loc) => new()
        {
            Mark = "S4", Size = "#3", ShapeCode = "L", Description = "type 2: bottom flange bar (pair), 1'-11 1/2\" + 3 1/4\" + 9 5/8\" + 1'-3\"", Location = loc, Mirror = true,
            Path = path.Select(q => new BeamBarPoint(q.A, q.X, q.Y)).ToList(), Stations = Ins(st), PrintedDims = Ins(new[] { 23.5, 3.25, 9.625, 15 }), ListedLength = In(51.375),
        };
        rd.Bars.Add(S4(st4, "bottom flange pairs (2-S4) with S2 / S1"));
        rd.Bars.Add(S4(s5, "bottom flange pairs (2-S4) with S5"));
        var c4 = S4(mid, "bottom flange pair (2-S4) at the girder CL"); c4.Ends = BeamBarEnds.StartOnly; rd.Bars.Add(c4);
        rd.Notes.Add("S4: position from the general '2\" CLR. (TYP. U.N.O.)' note of p.438 (bottom and vertical flange face); the slope / top run follow the bar type, not the flange outline (dimension-only).");
    }
    rd.Unresolved.Add(UnresolvedFlag.Text(gr.Sheet + ", p.438", "girder bars S2 (bulb hat bar), S7 (#6 bent before shipment) and V1 (4-V1 each face)",
        "S2's height in the bulb and V1's vertical extent are drawn but not dimensioned, and S7's bent shape / row positions (View D-D) are not printed (listed as STR)",
        "Not placed; the per-girder bar list check reports them as missing."));
    rd.Unresolved.Add(UnresolvedFlag.Text(gr.Sheet + ", p.438", "welded wire fabric (D16 @ 18\", 4-W6.5) in the top flange", "no bar input for welded wire fabric (GAP); the weight is in the printed girder total only",
        "Not modeled (in the listed girder weight)."));
    rd.Notes.Add("S1 / S5 pairs: one bar each side of the girder CL at the same station (p.428 plan of girder: one S1 each side of the CL); top 6\" leg outward, bottom 8\" leg inward (type 1 drawn as a Z).");
    sb.Beam.Reinforcement = rd;
    set++;
}
Print($"Bonner Unit 6 beam reinforcing set on {set} span beams (S1, S3, S4, S5, S6 / S8 placed; S2, S7, V1, WWF reported)");
