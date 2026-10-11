// Iowa DOT Design 817 (Woodbury, I-29 NB over Floyd Blvd) - SUBSTRUCTURE: Piers 1 and 2 (p.9-12) and the integral abutments S / N (p.15-17,
// std 2087-BTCD modified). Called by build-woodbury.csx after diaphragms-woodbury.csx (re-runnable: replaces SupportDefinition.Substructure).
//   sbm run samples\plans\substructure-woodbury.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"
// Plan data: docs/plan-data/woodbury-i29-floyd/SUMMARY.md section 4 (+ bar-lists.csv, bend-diagrams.csv). Rules: plan values only; whatever the plans
// do not dimension is UNRESOLVED (page + reason) - the generator then does not build that part. Differences are printed, never hidden.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var br = Doc.Project.Bridges.First(b => b.Structure == "I-29 NB over Floyd Blvd (Design 817)");
foreach (var s in br.Supports) if (s.Substructure != null) s.Substructure.Enabled = false;
Doc.Regenerate(includeSteel: false);                     // framing (support lines, beam chords) for the positions below
var fr = Doc.Framing(br.Name);
var al = Doc.Alignment(br.AlignmentName);
double ft = Ft(1);
double FI(int f, double inch) => Ft(f) + In(inch);
string Fmt(double m) => $"{m / ft:F4} ft ({m / In(1):F3}\")";
var notes = new List<string>();
void Diff(string what, double plan, double model, double tol)
{
    double d = model - plan;
    string st = Math.Abs(d) <= tol + 1e-9 ? "ok" : "DIFFERENCE";
    Print($"  {what}: plan {plan / ft:F4} ft, model {model / ft:F4} ft, diff {d / In(1):+0.000;-0.000}\" {st}");
}

// ---------------------------------------------------------------- support-line helpers (unit frame of SubstructureGenerator)
Vec2 D(int k) => fr.Supports[k].Direction;
Vec2 Nv(int k) { var t = Vec2.FromAngle(al.AngleAt(fr.Supports[k].Station)); var n = D(k).Left; return Vec2.Dot(n, t) < 0 ? -n : n; }
double SAt(int k, Vec2 p) => Vec2.Dot(p - fr.Supports[k].AlignmentPoint, D(k));
// S on support line k where it crosses the alignment offset e
double SOffset(int k, double e)
{
    double s = e;
    for (int i = 0; i < 40; i++)
    {
        var p = fr.Supports[k].AlignmentPoint + D(k) * s;
        double f = al.Inverse(p).Offset - e;
        if (Math.Abs(f) < 1e-10) break;
        double h = 1e-3, df = (al.Inverse(p + D(k) * h).Offset - al.Inverse(p - D(k) * h).Offset) / (2 * h);
        s -= f / df;
    }
    return s;
}
// S where beam line g (0 = D) of span j crosses support line k
double SBeam(int k, int j, int g)
{
    var c = fr.Girders[g].Spans[j];
    var dir = (c.End - c.Start).Normalized();
    Numerics.IntersectLines(fr.Supports[k].AlignmentPoint, D(k), c.Start, dir, out double t, out _);
    return t;
}

// ================================================================ PIERS 1 and 2 (p.9 pier details, p.11 sections / footing, p.12 bar list + notes)
// Table of pier elevations (p.9): D..H = low step / step tops (= beam seats p.25), J / K = cap top at the west (CJ) / east end, L / M / N = cap bottom at
// columns 2 / 3 / 4. Bottom of footing 1090.00. Maskwall at the east end 4'-11 (pier 1) / 5'-0 (pier 2) above K, its top 1 1/2" lower at the inner edge.
var pierData = new[]
{
    (k: 1, steps: new[] { 1117.94, 1118.23, 1118.52, 1118.80, 1119.06 }, J: 1117.25, K: 1118.76, L: 1112.36, M: 1113.01, N: 1113.65, mask: Len("4'-11\""), dimA: new[] { "17'-4 5/16\"", "18'-0 1/8\"", "18'-7 13/16\"" }),
    (k: 2, steps: new[] { 1118.21, 1118.62, 1119.03, 1119.43, 1119.82 }, J: 1117.42, K: 1119.54, L: 1112.57, M: 1113.48, N: 1114.39, mask: Len("5'-0\""), dimA: new[] { "17'-6 13/16\"", "18'-5 3/4\"", "19'-4 11/16\"" }),
};
double capLen = Len("42'-1 5/8\""), reveal = In(4), revealW = Len("2'-11 5/8\""), capW = Len("4'-3 5/8\"");
double colEnd = Len("2'-11 13/16\""), colSp = Len("18'-1\"");
// Bar list p.12 (per pier; footing marks split over the 3 footings) and bend diagrams (bend-diagrams.csv, out to out, D = pin).
PlanBarGroup G(string mark, PlanBarKind kind, string size, int count, string len, bool top = false, int order = 1, bool alongN = false) =>
    new() { Mark = mark, Kind = kind, Size = size, Count = count, Length = Len(len + "\""), Top = top, Order = order, AlongN = alongN };
PlanBarGroup U(PlanBarGroup g, double leg, double bas, double leg2, double pin) { g.Shape = "U"; g.Dims = new[] { In(leg), In(bas), In(leg2) }; g.Pin = In(pin); return g; }
foreach (var pd in pierData)
{
    int k = pd.k;
    var sup = br.Supports[k];
    // Position along the CL pier (cap plan p.9, "measured along CL pier"): cap end (CJ, west) to beam line D 6'-2 7/8. Pier 1 splits it 3'-1 3/4 (to the
    // W.P. = CL pier x CL NB roadway) + 3'-1 1/8; pier 2 draws the back / ahead beam D lines 1 3/16 apart (6'-2 7/8 and 6'-4 1/16 from the cap end).
    double dBack = SBeam(k, k - 1, 0), dAhead = SBeam(k, k, 0);
    // Pier 2 prints the chain 6'-2 7/8 (cap end to beam D) + 1 3/16 + 8'-0 11/16 (to beam E) = D-E 8'-1 7/8, and 6'-4 1/16 + 35'-9 9/16 from the
    // unlabelled tick 1 3/16 east of beam D: beam D is 6'-2 7/8 from the cap end on both piers (checked below for every beam line).
    double sD = (dBack + dAhead) / 2, sW = sD - Len("6'-2 7/8\""), sE = sW + capLen, sB = sE - reveal;
    {
        string[] pl1 = { "6'-2 7/8\"", "8'-1 7/8\"", "8'-1 7/8\"", "8'-1 7/8\"", "8'-1 7/8\"" }, pl2 = pl1;
        var pl = k == 1 ? pl1 : pl2;
        double prevB = sW, prevA = sW, cum = 0;
        for (int g = 0; g < 5; g++)
        {
            double b = SBeam(k, k - 1, g), a = SBeam(k, k, g);
            cum += Len(pl[g]);
            Print($"  beam {"DEFGH"[g]} at the CL pier: back-span line {(b - sW) / ft:F4} ft, ahead-span line {(a - sW) / ft:F4} ft from the cap end; plan {cum / ft:F4} ft (diff back {(b - sW - cum) / In(1):+0.000;-0.000}\", ahead {(a - sW - cum) / In(1):+0.000;-0.000}\")");
        }
    }
    double sWP = SOffset(k, Ft(27.00));
    Print($"Pier {k}: beam D crosses the CL pier at S {sD / ft:F3} / {Math.Max(dBack, dAhead) / ft:F3} ft (back {dBack / ft:F3}, ahead {dAhead / ft:F3}); cap west end S {sW / ft:F3} ft");
    if (k == 1)
    {
        Diff("Pier 1 cap end to W.P. (CL NB) along CL pier (3'-1 3/4)", Len("3'-1 3/4\""), sWP - sW, In(1.0 / 16));
    }
    // beam H to the east end 3'-3 1/4 (p.9)
    double hB = SBeam(k, k - 1, 4), hA = SBeam(k, k, 4);
    Diff($"Pier {k} beam H line to the east end (3'-3 1/4) [back / ahead beam H mean]", Len("3'-3 1/4\""), sE - (hB + hA) / 2, In(1.0 / 16));

    double J = Ft(pd.J), K = Ft(pd.K);
    double Top(double s) => J + (K - J) * (s - sW) / (sE - sW);
    double sL = sW + colEnd, sN = sL + 2 * colSp;
    double Bot(double s) => Ft(pd.L) + (Ft(pd.N) - Ft(pd.L)) * (s - sL) / (sN - sL);   // straight soffit through L and N; M checked
    Diff($"Pier {k} cap bottom at column 3 (M {pd.M:F2}) on the line L-N", Ft(pd.M), Bot(sL + colSp), Ft(0.01));
    Diff($"Pier {k} cap depth at the west end (5'-0)", Ft(5), J - Bot(sW), In(1.0 / 16));
    Diff($"Pier {k} cap depth at the east end (5'-0, p.9 elevation at K)", Ft(5), K - Bot(sE), In(1.0 / 16));
    Diff($"Pier {k} col 4 CL to the east end (2'-11 13/16)", colEnd, sE - sN, In(1.0 / 16));
    for (int c = 0; c < 3; c++)
        Diff($"Pier {k} column {c + 2} height DIM A ({pd.dimA[c]})", Len(pd.dimA[c]), Bot(sL + c * colSp) - Ft(1095.00), Ft(0.01));

    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.CapProfile = new()
    {
        new(sW, Top(sW)), new(sB, Top(sB)), new(sB, Bot(sB)), new(sW, Bot(sW)),
    };
    sd.CapWidth = capW; sd.CapOffset = 0;
    // Steps (p.9 cap plans): one level block per beam line carrying the back + ahead pads, 1'-10 + 1'-10 = 3'-8 along the CL pier, 4'-1 5/8 across
    // (1" from each cap face), sides at 78 deg 30' to the CL pier (TYP.) = 11 deg 30' from the cap normal, leaning the way the beams lean.
    sd.PedestalSizeAlong = Len("3'-8\""); sd.PedestalSizeAcross = Len("4'-1 5/8\""); sd.PedestalShared = true;
    {
        var c = fr.Girders[2].Spans[k];
        var t = (c.End - c.Start).Normalized();
        double ds = Vec2.Dot(t, D(k)), dn = Vec2.Dot(t, Nv(k));
        sd.PedestalSideAngleDeg = Math.Sign(ds / dn) * 11.5;
        Print($"  step block sides: {sd.PedestalSideAngleDeg:+0.0;-0.0} deg from the cap normal (beam F leans {Math.Atan(ds / dn) * 180 / Math.PI:+0.000;-0.000} deg)");
    }
    sd.MinPedestalHeight = 0;
    // Columns (p.9 elevation, p.11 section B-B): 5'-3 5/8 (along the pier) x 4'-3 5/8 + 4" x 2'-11 5/8 projections on both S faces (5'-11 5/8 overall).
    sd.ColumnCount = 3; sd.FirstColumnOffset = sL; sd.ColumnSpacings = new() { colSp, colSp };
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Len("5'-3 5/8\""); sd.ColumnDepth = capW;
    sd.ColumnProjectionS = In(4); sd.ColumnProjectionWidth = revealW;
    // Cap end (p.9 plan / elevation, p.11 C-C): the 4" x 2'-11 5/8 reveal projection on the east end face (cap soffit to the maskwall top) and the maskwall
    // (bar list "MASKWALL": 5c4 / 5c5 / 5d20) on the cap top: 1'-6 1/2 thick at the up-station face, 8" at the back face (inner face slanted, section C-C),
    // full cap width; top at the outer face = K + 4'-11 (pier 1) / 5'-0 (pier 2), 1 1/2" lower at the inner edge 1'-6 1/2 + 4" = 1'-10 1/2 from the outer face.
    double hw = capW / 2, maskTop = K + pd.mask, slope = In(1.5) / (Len("1'-6 1/2\"") + reveal);
    sd.CapBlocks = new()
    {
        new CapBlockDefinition
        {
            Name = "maskwall", Kind = CapBlockKind.Maskwall, Base = CapBlockBase.CapTop, TopElevation = maskTop, TopRefS = sE, TopSlopeS = slope,
            Outline = { new(sB - Len("1'-6 1/2\""), hw), new(sB, hw), new(sB, -hw), new(sB - In(8), -hw) }, Source = "p.9 elevation, p.11 section C-C",
        },
        new CapBlockDefinition
        {
            Name = "end reveal", Kind = CapBlockKind.Cap, Base = CapBlockBase.CapBottom, TopElevation = maskTop, TopRefS = sE, TopSlopeS = slope,
            Outline = { new(sB, -revealW / 2), new(sE, -revealW / 2), new(sE, revealW / 2), new(sB, revealW / 2) }, Source = "p.9 plan (4 x 2'-11 5/8), p.11 C-C",
        },
    };
    // Footings (p.9, p.11): 10'-0 along the pier x 13'-6 x 5'-0, square to the pier, centred on the column, bottom 1090.00. 12 HP12x84 per footing:
    // S = -3'-6, 0, +3'-6; N = +-5'-3 (outer rows: battered 1:4 AWAY from the CL pier, U6) and +-1'-9 (plumb). Positions at the footing bottom
    // (p.11 note). Circled piles (Detail 1, 2-8f6 each) = the 8 piles at S = +-3'-6. Webs along N (pile layout symbols). Pile notes p.12: 125 ft.
    sd.Foundation = FoundationType.PileCap;
    sd.FootingLength = Ft(10); sd.FootingWidth = Len("13'-6\""); sd.FootingThickness = Ft(5); sd.FoundationTopElevation = Ft(1095.00);
    sd.PileType = PileType.SteelHP; sd.PileSection = "HP12x84"; sd.PileLength = Ft(125); sd.PileEmbedment = Ft(1);   // "1'-0 TOP OF PILE" (p.10 elevation)
    var r = sd.Reinforcement;
    r.EpoxyCoated = true; r.CapCover = In(2.75); r.CapStirrupSize = "#5"; r.ColumnCover = In(2.75);
    sd.Footings = new();
    for (int c = 1; c <= 3; c++)
    {
        var fd = new FootingDefinition { Column = c, PileLength = Ft(125), BatterRatio = 4, PileWeb = PileWebMode.Default, UpliftBarsPerPile = 2 };
        foreach (double n in new[] { -5.25, -1.75, 1.75, 5.25 })
            foreach (double s in new[] { -3.5, 0, 3.5 })
                fd.Piles.Add(Math.Abs(n) > 5 ? new FootingPoint(Ft(s), Ft(n), 0, Math.Sign(n)) : new FootingPoint(Ft(s), Ft(n)));
        fd.UpliftPiles = Enumerable.Range(0, fd.Piles.Count).Where(i => Math.Abs(Math.Abs(fd.Piles[i].S) - Ft(3.5)) < 1e-6).ToList();
        // Footing bars (p.12 bar list per pier / 3; p.11 reinforcing layout; bend diagrams 9g1 U 1'-7;13'-2;1'-7 D 9 1/2, 7g2 U 1'-2;9'-8;1'-2 D 5 1/4).
        // Layer order of the two top / two bottom marks is not readable on the sheets: 9g1 / 7f1 (along N) are taken as the outer layers (UNRESOLVED, reported).
        fd.Bars = new()
        {
            G("7f1", PlanBarKind.FootingMat, "#7", 11, "13'-2", top: true, order: 1, alongN: true),
            G("7f2", PlanBarKind.FootingMat, "#7", 15, "9'-8", top: true, order: 2),
            U(G("9g1", PlanBarKind.FootingMat, "#9", 14, "16'-4", order: 1, alongN: true), 19, 158, 19, 9.5),
            U(G("7g2", PlanBarKind.FootingMat, "#7", 15, "12'-0", order: 2), 14, 116, 14, 5.25),
            G("8f6", PlanBarKind.UpliftAnchor, "#8", 16, "5'-0"),
        };
        sd.Footings.Add(fd);
    }
    // Cap / pedestal bars placed by the plan-bar pipeline (counts, sizes, listed lengths = p.12); the rest of the list is scheduled only (see notes).
    r.CapBars = new()
    {
        G("10a1", PlanBarKind.CapTop, "#10", 8, "50'-3"),        // U with splayed 4'-6 legs (bend diagram): the hook legs follow from the listed length
        G("10a2", PlanBarKind.CapTop, "#10", 6, "41'-0"),
        G("10b1", PlanBarKind.CapBottom, "#10", 14, "41'-0"),
        G("5a5", PlanBarKind.CapSide, "#5", 8, "41'-0"),
        G("5m1", PlanBarKind.PedestalLongitudinal, "#5", 25, "3'-4"),
        U(G("5n1", PlanBarKind.PedestalTransverse, "#5", 30, "7'-10"), 24, 46, 24, 2.5),
    };
    // Columns (p.11 B-B, p.10 elevation): 28 #10 verticals = 9 per long face (8 EQ. SPA.) and 7 per short face (6 EQ. SPA.), corners shared; lapped on
    // 10d2 L dowels (1'-10 hook + 14'-1), "10'-2 MIN. LAP (TYP.)"; #5 ties at the pitch DERIVED from the plan's equal spaces (col 2 / 3 / 4: 28 / 29 / 30
    // EQ. SPA. over the column height less 3" + 3").
    r.ColumnBarSize = "#10"; r.ColumnFaceBarsS = 9; r.ColumnFaceSpacingS = 0; r.ColumnFaceBarsN = 7; r.ColumnFaceSpacingN = 0; r.ColumnBarCount = 28;
    r.ColumnSplice = ColumnSpliceType.Lap; r.ColumnLapLength = Len("10'-2\""); r.DowelHookLength = Len("1'-10\""); r.DowelPin = In(10.75);
    r.ColumnSpiralSize = "#5";
    {
        double[] spaces = { 28, 29, 30 };
        double pitch = Enumerable.Range(0, 3).Average(c => (Bot(sL + c * colSp) - Ft(1095) - In(6)) / spaces[c]);
        r.ColumnSpiralPitch = pitch;
        Print($"  column tie pitch {pitch / In(1):F2}\" (mean of the three columns' equal spaces)");
    }
    r.FootingCover = In(3);
    sup.Substructure = sd;
}
notes.Add("Piers p.12 bar list NOT placed in 3D (no generator rule for these Woodbury details): 5c1 cap hoops (140, two 3'-0 x 4'-8 hoops per station), 4c2 cap transverse U, " +
          "5c3 end U, maskwall 5c4 / 5c5 / 5d20, 8a7 cap dowels, stainless splice bars 10a3 / 10b2 / 5a6 (project 2'-0 / 3'-9 past the stage-joint end), column hoops 5e1 / 5e2 " +
          "and ties 5e3 (generic #5 closed ties at the derived pitch are placed instead), projection bars 6d13-6d16.");
notes.Add("Piers: footing top / bottom layer order (7f1 vs 7f2, 9g1 vs 7g2) not readable on p.10 / p.11 - 7f1 and 9g1 placed as the outer layers (UNRESOLVED p.11).");
notes.Add("Piers: 3x12 x 4'-0 beveled keys (column / footing, p.9), 2x8 cap keyway and 2x12 x 3'-0 CJ keyway, thin veneer brick (p.13-14) not modeled.");

// ================================================================ INTEGRAL ABUTMENTS (p.15 S, p.16 N, p.17 pile plans + step diagram; std 2087-BTCD p.13)
double gutter = Ft(27.00 + 36.00);
// Wing extension: CL wing pile 5'-0 and wing end 6'-6 from CL abut. brg, measured ALONG the wing on the pile axis 1" outside the gutter line (p.17, U7);
// the model's U is measured along the gutter line from the cap back face (1'-6 behind CL brg). Distance along the gutter line from CL brg to the back face:
double BackAlongGutter(int k)
{
    double sg = k == 0 ? 1 : -1;
    var p0 = fr.Supports[k].AlignmentPoint + D(k) * SOffset(k, gutter);
    var tg = Vec2.FromAngle(al.AngleAt(al.Inverse(p0).Station));
    // the back face is the line parallel to CL brg at N = -sg * 1'-6; walking along the gutter (rearward = -sg along +N side)
    double cosA = Math.Abs(Vec2.Dot(tg, Nv(k)));
    return Ft(1.5) / cosA;
}
double AxisShift(int k)   // CL brg crossing of the pile axis (gutter + 1") vs of the gutter line, along the wing, rearward positive
{
    double sg = k == 0 ? 1 : -1;
    var pg = fr.Supports[k].AlignmentPoint + D(k) * SOffset(k, gutter);
    var pa = fr.Supports[k].AlignmentPoint + D(k) * SOffset(k, gutter + In(1));
    var tg = Vec2.FromAngle(al.AngleAt(al.Inverse(pg).Station));
    double rear = -sg;                                     // rearward = -N for S abut (span is +N), +N for N abut
    var rearDir = Vec2.Dot(tg, Nv(k)) * rear > 0 ? tg : -tg;
    return Vec2.Dot(pa - pg, rearDir);
}
var abutData = new[]
{
    (k: 0, name: "S", steps: new[] { 1117.50, 1117.71, 1117.92, 1118.11, 1118.31 }, botW: 1113.71, botE: 1114.60, dL: "3'-9 1/2", dR: "3'-8 1/2",
     vol: 18.1, pile: 155.0, firstSp: "5'-7", spaces: new[] { "5'-7", "5'-7", "5'-7", "5'-7", "5'-7", "5'-7" }, wallC: 1123.82, wallB: 1123.76, page: "p.15"),
    (k: 3, name: "N", steps: new[] { 1117.89, 1118.29, 1118.70, 1119.10, 1119.51 }, botW: 1113.91, botE: 1115.64, dL: "3'-11 3/4", dR: "3'-10 1/2",
     vol: 19.5, pile: 150.0, firstSp: "5'-9", spaces: new[] { "5'-9", "5'-9", "5'-9", "5'-9", "5'-9", "5'-9" }, wallC: 1125.11, wallB: 1125.08, page: "p.16"),
};
const double CY = 0.764554857984;
foreach (var ab in abutData)
{
    int k = ab.k;
    var sup = br.Supports[k];
    // Ends: west = stage construction joint (S: 3 1/4" from CL NB measured perpendicular to the roadway; N: 37'-10 1/2 along CL N abut brg from CL abut x
    // gutter line, p.17); east = outside face of the wing extension, 1'-7 outside the gutter line (p.15 A-A / p.17: the 3'-0 wing footing continues the cap).
    double sGut = SOffset(k, gutter);
    double leftOff = k == 0 ? Ft(27) - In(3.25) : al.Inverse(fr.Supports[k].AlignmentPoint + D(k) * (sGut - Len("37'-10 1/2\""))).Offset;
    double sLeft = SOffset(k, leftOff), sRight = SOffset(k, gutter + Len("1'-7\""));
    if (k == 0) Diff("S abut end to CL abut x gutter along CL brg (36'-10 3/16, p.17)", Len("36'-10 3/16\""), sGut - sLeft, In(1.0 / 16));
    else Diff("N abut end to the extended tangent along CL brg (7'-6 9/16, p.17) [ext. tangent 33.62' RT at the staking point]", Len("7'-6 9/16\""), SOffset(k, Ft(33.62)) - sLeft, In(1.0 / 16));
    var ad = new AbutmentDefinition
    {
        Kind = AbutmentKind.Integral, Source = $"Woodbury {ab.page}, p.17, std 2087-BTCD (modified)",
        LeftEndOffset = leftOff, RightEndOffset = gutter + Len("1'-7\""),
        CapBackToBearing = Ft(1.5), CapFrontToBearing = Ft(1.5),                 // 3'-0 cap centred on CL brg & CL piles (p.15 B-B, p.17)
        StepToSeat = In(3),                                                       // beam on the 3 x 3 x 2'-4 1/2 bar (p.25 BEAM BEARING 3")
        PlanCapDepthLeft = Len(ab.dL + "\""), PlanCapDepthRight = Len(ab.dR + "\""),
        PlanCapVolume = ab.vol * CY,
        PileRowOffsets = new() { 0 }, PileTopAboveFootingBottom = Ft(2), PileLength = Ft(ab.pile),
    };
    for (int g = 0; g < 5; g++) ad.Steps.Add(new AbutmentStepDefinition(g + 1, Ft(ab.steps[g])));
    // Risers (decision 11): NOT dimensioned anywhere - p.17 step diagram draws them about 3'-4 past the lower beam's CL, the p.15 / p.16 rear elevations
    // about 1'-9 (both scaled, inconsistent), std 2087-BTCD p.13 draws them without a dimension. -> UNRESOLVED: StepBoundaryS / StepRisersAtMidBay stay unset.
    // Bottom (p.15 / p.16 rear elevation, p.17 step diagram): sloped from the west end to the wing footing's inner face (1'-5 inside the gutter), then
    // LEVEL for the 3'-0 under the wing extension.
    ad.FootingBottom = new()
    {
        new(sLeft, Ft(ab.botW)), new(SOffset(k, gutter - Len("1'-5\"")), Ft(ab.botE)), new(sRight, Ft(ab.botE)),
    };
    // Piles on CL brg (p.17): end -2'-6- P1, then the spaces.
    double ps = sLeft + Ft(2.5);
    ad.PileStations.Add(ps);
    foreach (var sp in ab.spaces) ad.PileStations.Add(ps += Len(sp + "\""));
    Diff($"{ab.name} abut last pile P7 to CL abut x gutter ({(k == 0 ? "10 3/16" : "10 1/2")}, p.17)", In(k == 0 ? 10.1875 : 10.5), sGut - ad.PileStations[^1], In(1.0 / 16));
    // Wing extension (p.15 / p.16 section A-A, p.17, p.20): wall 1'-7 outboard of the gutter line on a 3'-0 footing (1'-5 inside), level bottom = the cap's
    // east end bottom; footing top = step H (DERIVED: drawn flush with step H on the rear elevations p.15 / p.16 and std p.13; the step diagram's east-end
    // depth 3'-8 1/2 / 3'-10 1/2 = Elev H - the level bottom exactly). Wall top: p.20 table at the east gutter line, C at the abutment back face (where the
    // p.20 end view's paving-notch outline is drawn) and B at the wing end (DERIVED positions - the end view does not dimension them).
    double back = BackAlongGutter(k), shift = AxisShift(k);
    double len = Len("6'-6\"") + shift - back, along = Ft(5) + shift - back;
    Print($"  {ab.name} wing: CL brg to cap back face along the gutter {Fmt(back)}, pile-axis brg crossing shift {shift / In(1):+0.000;-0.000}\" -> length {Fmt(len)}, pile at {Fmt(along)} from the back face");
    ad.WingExtension = new WingExtensionDefinition
    {
        End = AbutmentEnd.Right, GutterOffset = gutter, Length = len, WallThickness = Len("1'-7\""), FootingInside = Len("1'-5\""),
        FootingBottomElevation = Ft(ab.botE), FootingTopElevation = Ft(ab.steps[4]),
        WallTop = { new WallTopPoint(0, Ft(ab.wallC)), new WallTopPoint(len, Ft(ab.wallB)) },
        Piles = { new WingExtensionPileDefinition { Along = along, Across = In(1) } }, PlanPileCount = 1,
        PileTopAboveFootingBottom = Ft(2),                                       // "2'-0 TOP OF PILE" (p.15 B-B; the wing footing is part of the abutment footing)
    };
    ad.PileCasing = new PileCasingDefinition { Diameter = In(24), BottomElevation = double.NaN, Fill = "flowable fill (sand + bentonite top 10 ft)", PlanTotalLength = Ft(281) };
    ad.PileSpiral = new PileSpiralDefinition { BarSize = "#2", Diameter = In(21), Pitch = In(3), Turns = 7 };
    var sd = SubstructureDefinition.Default(SupportType.Abutment);
    sd.Abutment = ad; sd.PileType = PileType.SteelHP; sd.PileSection = "HP12x84"; sd.PileLength = Ft(ab.pile);
    sd.Reinforcement.EpoxyCoated = true;
    sup.Substructure = sd;
}
notes.Add("Abutments (decision 11): cap step riser positions are UNRESOLVED - not dimensioned on p.15 / p.16 / p.17 nor on std 2087-BTCD p.13; the p.17 step diagram draws them about 3'-4 past the lower beam CL, the p.15 / p.16 rear elevations about 1'-9 (scaled, inconsistent). The stepped pile cap solid is therefore NOT built (piles, wing extension, depths and checks are).");
notes.Add("Abutments: CMP casing bottom = bottom of EPS fill (road plans IM-NHS-029-7(49), not in this set) - UNRESOLVED, casings scheduled with unknown length (plan total 281 LF).");
notes.Add("Abutments: the 1'-7 wall over the cap end between the gutter line and the wing outer face (front of the cap back face) is neither in the diaphragm (ends at the gutter line) nor in the wing extension (starts at the cap back face) - not modeled.");
notes.Add("Abutments: integral abutment rebar (p.26 bar list) is schedule only (no 3D placement in the integral generator).");

foreach (var s in br.Supports) if (s.Substructure != null) s.Substructure.Enabled = true;
foreach (var n in notes) Print("NOTE " + n);
Print("Woodbury substructure set: Pier 1, Pier 2 (as designed), S. Abut., N. Abut. (integral).");
