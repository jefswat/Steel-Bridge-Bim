// As-designed piers of Ramp G (Design 316, pier detail sheets): cap elevation profiles, columns, footings and piles from
// the plans instead of the parametric bent. Run after the build scripts and piers-i80.csx.
// Plan coordinates: x along the cap from the CL pier in feet, right positive looking ahead station; elevations in feet.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double F(double ft) => Ft(ft);
double FI(int ft, double inch) => Ft(ft + inch / 12.0);

// S of the model's support for plan x along the CL pier (xRoadwayFt = plan x where the alignment crosses the CL pier).
Func<double, double> SFromPlan(BridgeDefinition b, int j, double xRoadwayFt)
{
    var fr = Doc.Model(b.Name).Framing.Supports[j];
    var al = Doc.Alignment(b.AlignmentName);
    var d = fr.Direction; var n = d.Left; var t = Vec2.FromAngle(al.AngleAt(fr.Station)); if (Vec2.Dot(n, t) < 0) n = -n;
    double off = b.Supports[j].PierCenterOffset;
    double sAp = Vec2.Dot(t, d) * off / Vec2.Dot(t, n);
    return x => sAp + F(x - xRoadwayFt);
}

void Report(string bridge, int j, (string label, double planElevFt)[] seats)
{
    var unit = Doc.Model(bridge).Substructure?.Units.FirstOrDefault(u => u.SupportIndex == j);
    if (unit == null) { Print($"  {bridge} support {j}: not built"); return; }
    Print($"  {unit.Name}: {unit.CapTopDescription}; {unit.Pedestals.Count} pedestals");
    foreach (var p in unit.Pedestals.OrderBy(p => p.S))
        Print($"     seat at S {p.S / Ft(1),7:F2} ft  model seat {p.SeatZ / Ft(1):F2}  cap top {p.CapTopZ / Ft(1):F2}  pedestal {p.Height / In(1):F1} in");
    foreach (var (label, e) in seats) Print($"     plan pedestal top {label}: {e:F2}");
}

FootingPoint Q(double s, double n) => new(F(s), F(n));
List<FootingPoint> Grid((double n, double[] s)[] rows) => rows.SelectMany(r => r.s.Select(x => Q(x, r.n))).ToList();

// Hammerhead cap with beak ends: top corners xL/xR (back-half top elevations eL/eR), tips xTL/xTR tipDepth below the top,
// soffit kinks at +-xK, kinkDepth below the top; ahead half raised by step (0 = flat top).
(List<CapProfilePoint> back, List<CapProfilePoint> ahead) Hammerhead(Func<double, double> S, double xL, double xR, double eL, double eR,
    double xTL, double xTR, double tipDepth, double xKL, double xKR, double kinkDepth, double step)
{
    double m = (eR - eL) / (xR - xL);
    double Top(double x) => eL + m * (x - xL);
    List<CapProfilePoint> P(double lift) => new()
    {
        new(S(xL), F(Top(xL) + lift)), new(S(xR), F(Top(xR) + lift)),
        new(S(xTR), F(Top(xR) - tipDepth)), new(S(xKR), F(Top(xKR) - kinkDepth)),
        new(S(xKL), F(Top(xKL) - kinkDepth)), new(S(xTL), F(Top(xL) - tipDepth)),
    };
    return (P(0), step > 0 ? P(step) : new());
}

// ---------------------------------------------------------------- Pier 19G (design sheets 73-75)
// Hammerhead on the "Pier 11WB & 19G" line next to the I-80 Pier 11WB cap; carries the Ramp G back bearings (girders
// A-D) and the I-80 Unit 3 ahead bearings of girders A-C. Cap 8'-0 wide: top corners x = -21.0 / +19.0, back-half top
// 1009.61 / 1011.56, ahead half 1.20 ft higher (1010.81 / 1012.77); tips at x = -22.203 / +20.703 (42'-11 5/16 overall)
// 5'-6 below the top; soffit 9'-0 below the top at kinks 6" outside the column faces (x = +-5.5; elev. 1001.36 /
// 1001.89 check). Column 10'-0 x 6'-6 octagonal (chamfers 3'-4 x 1'-9), 24'-0 high. Footing 28'-0 x 22'-0 x 5'-0,
// bottom 972.62; 20 HP14x73, 70 ft (outer piles battered 1:4 on the plans -- modeled plumb). Ramp G CL is 11'-10 1/4
// left of the CL pier (pile layout).
{
    var b = Doc.Project.Bridges.First(x => x.Name == "Ramp G Unit 4");
    int j = b.Supports.Count - 1;
    var S = SFromPlan(b, j, -FI(11, 10.25) / Ft(1));
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    (sd.CapProfile, sd.CapProfileAhead) = Hammerhead(S, -21.0, 19.0, 1009.61, 1011.56, -22.203, 20.703, 5.5, -5.5, 5.5, 9.0, 1.20);
    sd.CapWidth = F(8);
    sd.ColumnCount = 1; sd.FirstColumnOffset = S(0); sd.ColumnSpacings = new();
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = F(10); sd.ColumnDepth = FI(6, 6);
    sd.ColumnChamferS = FI(3, 4); sd.ColumnChamferN = FI(1, 9);
    sd.Foundation = FoundationType.PileCap; sd.FootingThickness = F(5); sd.FoundationTopElevation = F(972.62 + 5);
    sd.FootingLength = F(28); sd.FootingWidth = F(22);
    sd.PileSection = "HP14x73"; sd.PileLength = F(70);
    sd.Footings = new()
    {
        new FootingDefinition
        {
            Column = 1,
            Piles = Grid(new (double, double[])[]
            {
                (9, new[] { -12.0, -6, 0, 6, 12 }), (4.5, new[] { -12.0, 0, 12 }), (0, new[] { -12.0, -6, 6, 12 }),
                (-4.5, new[] { -12.0, 0, 12 }), (-9, new[] { -12.0, -6, 0, 6, 12 }),
            }),
        },
    };
    b.Supports[j].Substructure = sd;
    Doc.Regenerate();
    Report(b.Name, j, new[]
    {
        ("back (Ramp G A-D), left to right", 1010.20), ("", 1010.76), ("", 1011.32), ("", 1011.88),
        ("ahead (I-80 Unit 3 A-C), left to right", 1011.50), ("", 1012.24), ("", 1012.94),
    });
    Print($"  piles: {sd.Footings[0].Piles.Count} (plans: 20)");
}

// ---------------------------------------------------------------- Hammerhead piers 1G-15G (design sheets 18-60)
// Common to all (pier elevation sheets): cap 8'-0 wide; top corners 17'-6 each side of the CL pier; beak tips 5'-6 below
// the top corner at the outer ends of the overall dimension (left 1'-9 9/16 / right 1'-1 3/4 beyond the corners unless
// noted); soffit kinks 6" outside the 10'-0 column faces (x = +-5.5) at the soffit elevations shown; column 10'-0 x
// 6'-6 octagonal (3'-4 x 1'-9 chamfers) flaring below the flare elevation to 11'-0 x 7'-6 (3'-8 1/2 x 1'-11 3/8);
// the Ramp G baseline is 10'-0 left of the CL pier. Outer piles battered 1:4 on the plans are modeled plumb.
// NOTE: on most sheets the dimension string along the soffit closes 1'-0 longer than the one along the top; the top
// string is used (affects the tip x by up to 6").
// Pile patterns: rows across the cap (N, up-station +) with the positions along it.
(double n, double[] s)[] Rows(params (double n, double[] s)[] r) => r;
double[] L(params double[] x) => x;
var g4 = Rows((11.25, L(-12, -8, -4, 0, 4, 8, 12)), (6.75, L(-12, -8, 0, 8, 12)), (2.25, L(-12, -4, 4, 12)),
              (-2.25, L(-12, -4, 4, 12)), (-6.75, L(-12, -8, 0, 8, 12)), (-11.25, L(-12, -8, -4, 0, 4, 8, 12)));       // 32
var g3 = Rows((7, L(-12, -8, -4, 0, 4, 8, 12)), (3.5, L(-12, -8, 0, 8, 12)), (0, L(-12, -8, -4, 4, 8, 12)),
              (-3.5, L(-12, -8, 0, 8, 12)), (-7, L(-12, -8, -4, 0, 4, 8, 12)));                                           // 30
var g8 = Rows((10.5, L(-15, -10, -5, 0, 5, 10, 15)), (7, L(-15, -10, -5, 0, 5, 10, 15)), (3.5, L(-15, -10, -5, 5, 10, 15)),
              (0, L(-15, -10, 0, 10, 15)), (-3.5, L(-15, -10, -5, 5, 10, 15)), (-7, L(-15, -10, -5, 0, 5, 10, 15)),
              (-10.5, L(-15, -10, -5, 0, 5, 10, 15)));                                                                     // 45
var a6 = L(-13.75, -8.25, -2.75, 2.75, 8.25, 13.75);
double[] all8 = L(-19.25, -13.75, -8.25, -2.75, 2.75, 8.25, 13.75, 19.25);
var g11 = Rows((19.25, all8), (13.75, all8), (8.25, L(-19.25, -13.75, -8.25, 8.25, 13.75, 19.25)), (5.5, L(0)),
               (2.75, L(-19.25, -13.75, 13.75, 19.25)), (0, L(-8.25, 0, 8.25)), (-2.75, L(-19.25, -13.75, 13.75, 19.25)),
               (-5.5, L(0)), (-8.25, L(-19.25, -13.75, -8.25, 8.25, 13.75, 19.25)), (-13.75, all8), (-19.25, all8));       // 57
double[] x6 = L(-12.5, -7.5, -2.5, 2.5, 7.5, 12.5);
var piers = new (string name, double eL, double eR, double kL, double kR, double flare, double ftgBot, double ftgT,
    double ftgS, double ftgN, (double n, double[] s)[] piles, double pileFt, string section, double tipL, double tipR, double step, int expect)[]
{
    // 1G: column 10'-0 x 6'-6 full height (no flare); footing 29'-0 x 16'-0 x 5'-6.
    ("Pier 1G", 1000.79, 1001.49, 992.03, 992.25, double.NaN, 961.64, 5.5, 29, 16,
        Rows((6, x6), (2, x6), (-2, x6), (-6, x6)), 90, "HP14x73", FI(1, 4.375) / Ft(1), 1 + 7 / 12.0, 0, 24),
    ("Pier 2G", 1008.62, 1008.29, 999.51, 999.40, 979.46, 960.46, 7, 29, 31.5,
        Rows((13.75, x6), (8.25, x6), (2.75, L(-12.5, -2.5, 2.5, 12.5)), (-2.75, L(-12.5, -2.5, 2.5, 12.5)), (-8.25, x6), (-13.75, x6)),
        90, "HP14x73", FI(1, 6.3125) / Ft(1), FI(1, 5.0625) / Ft(1), 0, 32),
    ("Pier 3G", 1016.47, 1014.40, 1006.74, 1006.09, 981.42, 961.42, 6, 28, 18, g3, 90, "HP14x73", 1.797, 1.146, 0, 30),
    ("Pier 4G", 1024.07, 1022.00, 1014.34, 1013.69, 985.02, 962.02, 6, 28, 26.5, g4, 90, "HP14x73", 1.797, 1.146, 0, 32),
    ("Pier 5G", 1030.08, 1028.01, 1020.35, 1019.70, 991.03, 968.03, 7, 29, 24,
        Rows((10, x6), (5, x6), (0, x6), (-5, x6), (-10, x6)), 95, "HP14x117", 1.797, 1.146, 0, 30),
    ("Pier 6G", 1036.62, 1034.55, 1026.89, 1026.24, 994.57, 970.57, 6, 28, 26.5, g4, 105, "HP14x73", 1.797, 1.146, 0, 32),
    ("Pier 7G", 1041.92, 1039.85, 1032.19, 1031.54, 1002.87, 978.87, 8, 35.5, 28,
        Rows((12, L(-15.75, -11.25, -6.75, -2.25, 2.25, 6.75, 11.25, 15.75)), (8, L(-15.75, -11.25, -6.75, -2.25, 2.25, 6.75, 11.25, 15.75)),
             (4, L(-15.75, -11.25, -6.75, 6.75, 11.25, 15.75)), (0, L(-15.75, -11.25, 0, 11.25, 15.75)),
             (-4, L(-15.75, -11.25, -6.75, 6.75, 11.25, 15.75)), (-8, L(-15.75, -11.25, -6.75, -2.25, 2.25, 6.75, 11.25, 15.75)),
             (-12, L(-15.75, -11.25, -6.75, -2.25, 2.25, 6.75, 11.25, 15.75))), 100, "HP14x73", 1.797, 1.146, 0, 49),
    ("Pier 8G", 1044.04, 1041.97, 1034.31, 1033.66, 994.99, 966.49, 7.5, 34, 25, g8, 90, "HP14x73", 1.797, 1.146, 0, 45),
    // 9G: Unit 2 / Unit 3 joint; one cap top (1042.90 / 1040.83). Back pedestals are ~0.9 ft taller than the ahead ones
    // (pedestal tops 1044.12 back vs 1043.20 ahead at girder A) -- pedestals, not a stepped cap.
    ("Pier 9G", 1042.90, 1040.83, 1033.17, 1032.52, 994.85, 967.85, 6, 31.5, 24,
        Rows((10, a6), (5, a6), (0, L(-13.75, -2.75, 2.75, 13.75)), (-5, a6), (-10, a6)), 95, "HP14x73", 1.797, 1.146, 0, 28),
    ("Pier 10G", 1041.86, 1039.79, 1032.13, 1031.48, 993.81, 966.31, 7.5, 34, 25, g8, 90, "HP14x73", 1.797, 1.146, 0, 45),
    ("Pier 11G", 1040.55, 1038.48, 1030.82, 1030.17, 995.50, 968.00, 7.5, 42.5, 42.5, g11, 85, "HP14x73", 1.797, 1.146, 0, 57),
    ("Pier 12G", 1037.37, 1035.30, 1027.64, 1026.99, 992.32, 961.82, 7.5, 42.5, 42.5, g11, 60, "HP14x73", 1.797, 1.146, 0, 57),
    ("Pier 13G", 1032.13, 1030.06, 1022.40, 1021.75, 991.08, 968.08, 6, 28, 26.5, g4, 60, "HP14x73", 1.797, 1.146, 0, 32),
    ("Pier 14G", 1027.95, 1025.88, 1018.22, 1017.57, 988.90, 966.90, 6, 31.5, 22,
        Rows((9, a6), (4.5, a6), (0, L(-13.75, -2.75, 2.75, 13.75)), (-4.5, a6), (-9, a6)), 65, "HP14x73", 1.797, 1.146, 0, 28),
    ("Pier 15G", 1023.95, 1022.41, 1014.41, 1013.93, 990.17, 970.17, 6, 28, 18, g3, 65, "HP14x73", FI(1, 8.5625) / Ft(1), FI(1, 2.75) / Ft(1), 0, 30),
};
foreach (var p in piers)
{
    // The bridge whose support builds this pier (shared unit-end piers are built by the unit that owns them).
    var hit = Doc.Project.Bridges.Where(x => x.Name.StartsWith("Ramp G"))
        .SelectMany(x => x.Supports.Select((sp, j) => (b: x, j, sp))).FirstOrDefault(t => t.sp.Name == p.name && t.sp.ResolvedSubstructure.Enabled);
    if (hit.b == null) { Print($"  {p.name}: support not found"); continue; }
    var S = SFromPlan(hit.b, hit.j, -10.0);
    double m = (p.eR - p.eL) / 35.0;
    double Top(double x) => p.eL + m * (x + 17.5);
    List<CapProfilePoint> Prof(double lift) => new()
    {
        new(S(-17.5), F(Top(-17.5) + lift)), new(S(17.5), F(Top(17.5) + lift)),
        new(S(17.5 + p.tipR), F(Top(17.5) - 5.5)), new(S(5.5), F(p.kR)), new(S(-5.5), F(p.kL)), new(S(-17.5 - p.tipL), F(Top(-17.5) - 5.5)),
    };
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.CapProfile = Prof(0);
    sd.CapProfileAhead = p.step > 0 ? Prof(p.step) : new();
    sd.CapWidth = F(8);
    sd.ColumnCount = 1; sd.FirstColumnOffset = S(0); sd.ColumnSpacings = new();
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = F(10); sd.ColumnDepth = FI(6, 6);
    sd.ColumnChamferS = FI(3, 4); sd.ColumnChamferN = FI(1, 9);
    if (!double.IsNaN(p.flare))
    {
        sd.ColumnFlareElevation = F(p.flare); sd.ColumnFlareWidth = F(11); sd.ColumnFlareDepth = FI(7, 6);
        sd.ColumnFlareChamferS = FI(3, 8.5); sd.ColumnFlareChamferN = FI(1, 11.375);
    }
    sd.Foundation = FoundationType.PileCap; sd.FootingThickness = F(p.ftgT); sd.FoundationTopElevation = F(p.ftgBot + p.ftgT);
    sd.FootingLength = F(p.ftgS); sd.FootingWidth = F(p.ftgN);
    sd.PileSection = p.section; sd.PileLength = F(p.pileFt);
    sd.Footings = new() { new FootingDefinition { Column = 1, Piles = Grid(p.piles) } };
    hit.b.Supports[hit.j].Substructure = sd;
    int n = sd.Footings[0].Piles.Count;
    Print($"  {p.name} ({hit.b.Name}): {n} piles{(n == p.expect ? "" : $" -- PLANS SAY {p.expect}")}, {p.section} x {p.pileFt} ft");
}
// ---------------------------------------------------------------- Piers 16G and 17G (design sheets 62-69, PDF 64-71)
// Two-column bents (Ramp G passes between the columns; data docs/plan-data/ramp-g-piers-16g-18g.md). Cap 6'-0 wide,
// 7'-0 deep; beak ends: tip 4'-6 below the top corner (end face 75 deg), soffit kink 6" outside the outer column face.
// 16G: top 1018.98 level, corners x = +-31.5, tips +-32.708 (65'-5 overall), columns x = +-19 (38'-0).
// 17G: top 1012.75 (x = -26.5) to 1014.08 (x = +26.5), soffit parallel (1006.06 / 1006.76 at the columns), tips
// -27.594 / +27.818, columns x = +-14 (28'-0). Columns 8'-0 x 4'-6 octagonal (2'-4 x 1'-3 chamfers), no flare.
// Footings 22'-0 x 16'-0 x 5'-6 centred on each column, 20 HP14x73 x 70 ft (5 along x 4 across), plumb; pile tops 1'-0
// into the footing. Ramp G CL is 18'-1 11/16 (16G) / 15'-0 9/16 (17G) left of the CL pier. x positive = right looking
// ahead station (sheets "looking northeast"; checked by the seats below).
// Not modeled: the narrower lower cap section (6'-0 top / 5'-0 lower part, sections A-A), architectural relief.
var bent = Rows((6, L(-9, -4.5, 0, 4.5, 9)), (2, L(-9, -4.5, 0, 4.5, 9)), (-2, L(-9, -4.5, 0, 4.5, 9)), (-6, L(-9, -4.5, 0, 4.5, 9)));
foreach (var p in new (string name, double xRoad, double half, double eL, double eR, double tipL, double tipR, double col, double ftgBot, (string, double)[] seats)[]
{
    ("Pier 16G", -FI(18, 1.6875) / Ft(1), 31.5, 1018.98, 1018.98, -32.708, 32.708, 19, 977.48,
        new[] { ("A-D", 1019.46), ("", 1019.34), ("", 1019.31), ("", 1019.38) }),
    ("Pier 17G", -FI(15, 0.5625) / Ft(1), 26.5, 1012.75, 1014.08, -27.594, 27.818, 14, 976.56,
        new[] { ("A-D", 1013.28), ("", 1013.64), ("", 1014.01), ("", 1014.38) }),
})
{
    var b = Doc.Project.Bridges.First(x => x.Name == "Ramp G Unit 4");
    int j = b.Supports.FindIndex(s => s.Name == p.name);
    var S = SFromPlan(b, j, p.xRoad);
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    double kink = p.col + 4.5;
    (sd.CapProfile, sd.CapProfileAhead) = Hammerhead(S, -p.half, p.half, p.eL, p.eR, p.tipL, p.tipR, 4.5, -kink, kink, 7.0, 0);
    sd.CapWidth = F(6);
    sd.ColumnCount = 2; sd.FirstColumnOffset = S(-p.col); sd.ColumnSpacings = new() { F(2 * p.col) };
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = F(8); sd.ColumnDepth = FI(4, 6);
    sd.ColumnChamferS = FI(2, 4); sd.ColumnChamferN = FI(1, 3);
    sd.Foundation = FoundationType.PileCap; sd.FootingThickness = F(5.5); sd.FoundationTopElevation = F(p.ftgBot + 5.5);
    sd.FootingLength = F(22); sd.FootingWidth = F(16);
    sd.PileSection = "HP14x73"; sd.PileLength = F(70); sd.PileEmbedment = F(1);
    sd.Footings = new() { new FootingDefinition { Column = 1, Piles = Grid(bent) }, new FootingDefinition { Column = 2, Piles = Grid(bent) } };
    b.Supports[j].Substructure = sd;
    Doc.Regenerate();
    Report(b.Name, j, p.seats);
}

// ---------------------------------------------------------------- Pier 18G (design sheets 70-72, PDF 72-74)
// Hammerhead, radial: top corners x = +-17.5 at 1009.93 / 1012.00, tips -18.646 / +19.297 5'-6 below the top, soffit kinks
// x = +-5.5 at 1001.62 / 1002.27; cap 8'-0 wide. Column 10'-0 x 6'-6 octagonal (3'-4 x 1'-9), no flare. Footing 32'-0
// along the pier x 42'-6 x 7'-0, bottom 972.95; 34 HP14x73 x 70 ft. Ramp G CL 10'-0 left of the CL pier.
// 22 piles are battered 1:4 on the plans (S = +-14 columns, outer rows, corners diagonal) -- modeled plumb (no batter input yet).
{
    var b = Doc.Project.Bridges.First(x => x.Name == "Ramp G Unit 4");
    int j = b.Supports.FindIndex(s => s.Name == "Pier 18G");
    var S = SFromPlan(b, j, -10.0);
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.CapProfile = new()
    {
        new(S(-17.5), F(1009.93)), new(S(17.5), F(1012.00)), new(S(19.297), F(1012.00 - 5.5)), new(S(5.5), F(1002.27)),
        new(S(-5.5), F(1001.62)), new(S(-18.646), F(1009.93 - 5.5)),
    };
    sd.CapWidth = F(8);
    sd.ColumnCount = 1; sd.FirstColumnOffset = S(0); sd.ColumnSpacings = new();
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = F(10); sd.ColumnDepth = FI(6, 6);
    sd.ColumnChamferS = FI(3, 4); sd.ColumnChamferN = FI(1, 9);
    sd.Foundation = FoundationType.PileCap; sd.FootingThickness = F(7); sd.FoundationTopElevation = F(972.95 + 7);
    sd.FootingLength = F(32); sd.FootingWidth = F(42.5);
    sd.PileSection = "HP14x73"; sd.PileLength = F(70); sd.PileEmbedment = F(1);
    double[] s5 = L(-14, -7, 0, 7, 14);
    sd.Footings = new()
    {
        new FootingDefinition
        {
            Column = 1,
            Piles = Grid(Rows((19.25, s5), (13.75, s5), (8.25, L(-14, 0, 14)), (2.75, L(-14, -7, 7, 14)),
                              (-2.75, L(-14, -7, 7, 14)), (-8.25, L(-14, 0, 14)), (-13.75, s5), (-19.25, s5))),
        },
    };
    b.Supports[j].Substructure = sd;
    Doc.Regenerate();
    Report(b.Name, j, new[] { ("A-D", 1010.59), ("", 1011.16), ("", 1011.73), ("", 1012.30) });
    Print($"  piles: {sd.Footings[0].Piles.Count} (plans: 34)");
}

Doc.Regenerate();
foreach (var b in Doc.Project.Bridges.Where(x => x.Name.StartsWith("Ramp G")))
    foreach (var u in Doc.Model(b.Name).Substructure?.Units ?? new())
        if (u.CapOutline.Count > 0)
            Print($"  {u.Name}: pedestals {u.Pedestals.Min(q => q.Height) / In(1):F1} to {u.Pedestals.Max(q => q.Height) / In(1):F1} in ({u.Pedestals.Count})");

var rr = Doc.Regenerate();
Print($"Regenerated {rr.Bridges} bridges in {rr.Elapsed.TotalSeconds:F1} s, {rr.Errors.Count} errors.");
foreach (var e in rr.Errors) Print("  ERROR " + e);
Doc.Save();
