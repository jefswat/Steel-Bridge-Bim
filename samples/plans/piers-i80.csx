// As-designed piers of WB I-80 (Design 116, pier detail sheets): cap elevation profiles, columns, footings and piles
// taken from the plans instead of the parametric bent. Run after the build scripts (works before or after geolocation).
// Plan coordinates: x along the cap from the CL pier in feet, right positive looking ahead station (left = north),
// elevations in feet (NAVD88 as on the plans). Pedestal tops are compared with the model's bearing seats.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var units = Doc.Units;
double F(double ft) => Ft(ft);
double FI(int ft, double inch) => Ft(ft + inch / 12.0);

// S of the model's support (the bridge's bearing line) for plan x measured along the CL pier from the CL pier, given
// where the roadway CL crosses the CL pier (xRoadway, plan x of the alignment) .
Func<double, double> SFromPlan(BridgeDefinition b, int j, double xRoadwayFt)
{
    var fr = Doc.Model(b.Name).Framing.Supports[j];
    var al = Doc.Alignment(b.AlignmentName);
    var d = fr.Direction; var n = d.Left; var t = Vec2.FromAngle(al.AngleAt(fr.Station)); if (Vec2.Dot(n, t) < 0) n = -n;
    double off = b.Supports[j].PierCenterOffset;
    // CL pier line = bearing line shifted by off along n; it crosses the alignment where (P - A)·n = off.
    double sAp = Vec2.Dot(t, d) * off / Vec2.Dot(t, n);
    return x => sAp + F(x - xRoadwayFt);
}

void Report(string bridge, int j, (string label, double planElevFt)[] seats)
{
    var md = Doc.Model(bridge);
    var unit = md.Substructure?.Units.FirstOrDefault(u => u.SupportIndex == j);
    if (unit == null) { Print($"  {bridge} support {j}: not built"); return; }
    var peds = unit.Pedestals.OrderBy(p => p.S).ToList();
    Print($"  {unit.Name}: {unit.CapTopDescription}; {peds.Count} pedestals");
    foreach (var p in peds)
        Print($"     seat at S {p.S / Ft(1),7:F2} ft  model seat {p.SeatZ / Ft(1):F2}  cap top {p.CapTopZ / Ft(1):F2}  pedestal {p.Height / In(1):F1} in");
    foreach (var (label, e) in seats) Print($"     plan pedestal top {label}: {e:F2}");
}

// ---------------------------------------------------------------- Pier 11WB & 19G (design sheets 61-62)
// Cap 8'-0 wide, 7'-0 deep below the back-half top; back half (Unit 2, 102" girders) top 1010.95 (left) to 1015.18
// (right) at the cap-end top corners x = -42.5 / +42.5; ahead half (Unit 3, 76" girders) 2.03 ft higher (1012.98 to
// 1017.21). Ends: raked faces from the top corners out to tips 4'-6 below the back top at x = -43.479 / +43.927
// (87'-4 7/8 overall), then the soffit runs back down to the soffit kink 6" outside the outer column faces
// (x = -35.5 / +35.5); soffit parallel to the back top between the kinks. Curved soffit lines are aesthetic only.
// Columns: octagonal 8'-0 x 4'-6 (chamfers 2'-4 x 1'-3) above elev. 990.51, flared 9'-0 x 5'-6 (2'-8 1/2 x 1'-5 5/8)
// below; at x = -31.0, +9.5, +31.0. Footings 18'-0 x 18'-0 x 6'-6, bottom 971.01 (top 977.51); HP14x73 piles, 70 ft.
// The roadway CL (girder G) is 11 5/8" right of the CL pier.
{
    var b = Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 2");
    int j = b.Supports.Count - 1;
    var S = SFromPlan(b, j, 11.625 / 12);
    double topL = 1010.95, topR = 1015.18, step = 1012.98 - 1010.95;
    double m = (topR - topL) / 85.0;
    double Top(double x) => topL + m * (x + 42.5);
    var sd = b.Supports[j].Substructure ?? SubstructureDefinition.Default(SupportType.Pier);
    sd = sd.Clone();
    CapProfilePoint P(double x, double e) => new(S(x), F(e));
    List<CapProfilePoint> Profile(double lift) => new()
    {
        P(-42.5, Top(-42.5) + lift), P(42.5, Top(42.5) + lift),            // top corners
        P(43.927, Top(42.5) - 4.5),                                         // right tip
        P(35.5, Top(35.5) - 7.0),                                           // right soffit kink
        P(-35.5, Top(-35.5) - 7.0),                                         // left soffit kink
        P(-43.479, Top(-42.5) - 4.5),                                       // left tip
    };
    sd.CapProfile = Profile(0);
    sd.CapProfileAhead = Profile(step);
    sd.CapWidth = F(8); sd.CapOffset = 0;
    sd.ColumnCount = 3; sd.FirstColumnOffset = S(-31.0); sd.ColumnSpacings = new() { F(40.5), F(21.5) };
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = F(8); sd.ColumnDepth = F(4.5);
    sd.ColumnChamferS = FI(2, 4); sd.ColumnChamferN = FI(1, 3);
    sd.ColumnFlareElevation = F(990.51); sd.ColumnFlareWidth = F(9); sd.ColumnFlareDepth = F(5.5);
    sd.ColumnFlareChamferS = FI(2, 8.5); sd.ColumnFlareChamferN = FI(1, 5.625);
    sd.Foundation = FoundationType.PileCap;
    sd.FootingThickness = F(6.5); sd.FoundationTopElevation = F(971.01 + 6.5);
    sd.FootingLength = F(18); sd.FootingWidth = F(18);
    sd.PileSection = "HP14x73"; sd.PileLength = F(70);                  // contract length 70 ft (sheet 64 notes)
    // Pile layout (sheet 63): rows across the cap at N = +7.5, +4.5, +1.5, 0, -1.5, -4.5, -7.5 ft (up = ahead);
    // footing 3 full 18'-0 square (29 piles); footings 1 and 2 have a corner clipped 11'-3 / 11'-3 around the existing
    // 48" storm sewer (26 piles each; footing 2 = footing 1 rotated 180 deg).
    FootingPoint Q(double ss, double nn) => new(F(ss), F(nn));
    var full = new (double n, double[] s)[]
    {
        (7.5, new[] { -7.5, -4.5, -1.5, 1.5, 4.5, 7.5 }), (4.5, new[] { -7.5, -4.5, 0, 4.5, 7.5 }), (1.5, new[] { -7.5, 7.5 }),
        (0, new[] { -4.5, 0, 4.5 }), (-1.5, new[] { -7.5, 7.5 }), (-4.5, new[] { -7.5, -4.5, 0, 4.5, 7.5 }),
        (-7.5, new[] { -7.5, -4.5, -1.5, 1.5, 4.5, 7.5 }),
    };
    var f1 = new (double n, double[] s)[]
    {
        (7.5, new[] { -7.5, -4.5, -1.5, 1.5 }), (4.5, new[] { -7.5, -4.5, 0, 4.5 }), (1.5, new[] { -7.5, 7.5 }),
        (0, new[] { -4.5, 0, 4.5 }), (-1.5, new[] { -7.5, 7.5 }), (-4.5, new[] { -7.5, -4.5, 0, 4.5, 7.5 }),
        (-7.5, new[] { -7.5, -4.5, -1.5, 1.5, 4.5, 7.5 }),
    };
    List<FootingPoint> Piles((double n, double[] s)[] rows, double sign) => rows.SelectMany(r => r.s.Select(x => Q(sign * x, sign * r.n))).ToList();
    var clip1 = new List<FootingPoint> { Q(-9, -9), Q(9, -9), Q(9, 2.25), Q(2.25, 9), Q(-9, 9) };
    sd.Footings = new()
    {
        new FootingDefinition { Column = 1, Outline = clip1, Piles = Piles(f1, 1) },
        new FootingDefinition { Column = 2, Outline = clip1.Select(p => new FootingPoint(-p.S, -p.N)).ToList(), Piles = Piles(f1, -1) },
        new FootingDefinition { Column = 3, Piles = Piles(full, 1) },
    };
    Print($"  piles per footing: {string.Join(", ", sd.Footings.Select(x => x.Piles.Count))} (plans: 26, 26, 29)");
    b.Supports[j].Substructure = sd;
    Doc.Regenerate();
    Report(b.Name, j, new[]
    {
        ("back (Unit 2), left to right", 1011.54), ("", 1012.19), ("", 1012.83), ("", 1013.48), ("", 1014.12), ("", 1014.77), ("", 1015.41),
        ("ahead (Unit 3), left to right", 1013.66), ("", 1014.30), ("", 1014.94), ("", 1015.59), ("", 1016.24), ("", 1016.88), ("", 1017.53),
    });
}

// Pile pattern helpers (rows across the cap: N with the positions along it).
FootingPoint GQ(double s, double n) => new(F(s), F(n));
List<FootingPoint> Grid((double n, double[] s)[] rows) => rows.SelectMany(r => r.s.Select(x => GQ(x, r.n))).ToList();
(double n, double[] s)[] Rows(params (double n, double[] s)[] r) => r;
double[] L(params double[] x) => x;

// ---------------------------------------------------------------- Piers 1WB-10WB and 12WB-15WB (design sheets 18-80)
// Common: cap 6'-0 wide, 7'-0 deep below the top line; 75 deg raked ends from the top corners (+-half) down 4'-6 to the
// tips (extL / extR beyond the corners; Pier 5WB's left end is square); the soffit runs from each tip to a kink 6"
// outside the outer column face, then parallel to the top (Unit 1: level top and bottom; Units 2-3: sloped). Columns
// 8'-0 x 4'-6 octagonal (2'-4 x 1'-3 chamfers), flared below the given elevation to 9'-0 x 5'-6 (2'-8 1/2 x 1'-5 5/8).
// Pile layouts (sheets "pile layout"; S along the cap, N across): rows of positions, identical for every footing.
// The roadway CL crosses the CL pier `road` ft from column `refCol` (pile layout dimension; the side is not stated, so
// both are tried and the one that puts every bearing seat on the cap with the most uniform pedestals is kept).
var r1 = Rows((6, L(-7, -3.5, 0, 3.5, 7)), (2, L(-7, -3.5, 3.5, 7)), (-2, L(-7, -3.5, 3.5, 7)), (-6, L(-7, -3.5, 0, 3.5, 7)));
var r3 = Rows((9, L(-9, -4.5, 0, 4.5, 9)), (4.5, L(-9, -4.5, 4.5, 9)), (0, L(-9, 0, 9)), (-4.5, L(-9, -4.5, 4.5, 9)), (-9, L(-9, -4.5, 0, 4.5, 9)));
var q4 = L(-5.25, -1.75, 1.75, 5.25);
var r4 = Rows((8.75, q4), (5.25, q4), (1.75, q4), (-1.75, q4), (-5.25, q4), (-8.75, q4));
var q5 = L(-6, -2, 2, 6);
var r5 = Rows((8, q5), (4, L(-6, 0, 6)), (0, L(-6, 6)), (-4, L(-6, 0, 6)), (-8, q5));
var r6 = Rows((8, L(-7, -3.5, 0, 3.5, 7)), (4, L(-7, -3.5, 3.5, 7)), (0, L(-7, 0, 7)), (-4, L(-7, -3.5, 3.5, 7)), (-8, L(-7, -3.5, 0, 3.5, 7)));
var r7 = Rows((12.5, L(-8, -4, 0, 4, 8)), (7.5, L(-8, -4, 4, 8)), (5, L(0)), (2.5, L(-8, 8)), (0, L(-4, 4)), (-2.5, L(-8, 8)),
              (-5, L(0)), (-7.5, L(-8, -4, 4, 8)), (-12.5, L(-8, -4, 0, 4, 8)));
var q8 = L(-8.75, -5.25, -1.75, 1.75, 5.25, 8.75);
var r8 = Rows((5.25, q8), (1.75, L(-8.75, 8.75)), (0, L(-3.5, 3.5)), (-1.75, L(-8.75, 8.75)), (-5.25, q8));
var r9 = Rows((6.75, L(-11, -5.5, 0, 5.5, 11)), (2.25, L(-11, 0, 11)), (0, L(-5.5, 5.5)), (-2.25, L(-11, 0, 11)), (-6.75, L(-11, -5.5, 0, 5.5, 11)));
var r12 = Rows((8.25, q5), (2.75, q5), (-2.75, q5), (-8.25, q5));
var r13 = Rows((13, L(-10, -5, 0, 5, 10)), (6.5, L(-10, 0, 10)), (0, L(-10, -5, 5, 10)), (-6.5, L(-10, 0, 10)), (-13, L(-10, -5, 0, 5, 10)));
var q14 = L(-7.5, -2.5, 2.5, 7.5);
var r14 = Rows((8.25, q14), (2.75, q14), (-2.75, q14), (-8.25, q14));
var ip = new (string name, string bridge, int j, double half, double extL, double extR, double eL, double eR, double[] cols, double flare,
    double fS, double fN, double fT, double fBot, (double n, double[] s)[] piles, double pileFt, double refCol, double road, int expect, int side)[]
{
    ("Pier 1WB", "I-80 WB Unit 1", 1, 45, 1.2083, 1.2083, 999.33, 999.33, L(-32.5, 0, 32.5), double.NaN, 18, 16, 5.5, 968.83, r1, 100, 0, 8.5625, 18, -1),
    ("Pier 2WB", "I-80 WB Unit 1", 2, 43.5, 1.2083, 1.2083, 1002.88, 1002.88, L(-31, 0, 31), double.NaN, 18, 16, 5.5, 968.38, r1, 90, 0, 8.3385, 18, -1),
    ("Pier 3WB", "I-80 WB Unit 1", 3, 43.5, 1.2083, 1.2083, 1006.30, 1006.30, L(-31, 0, 31), double.NaN, 22, 22, 5, 969.30, r3, 95, 0, 8.3385, 21, -1),
    ("Pier 4WB", "I-80 WB Unit 1", 4, 31.5, 1.2083, 1.2083, 1009.24, 1009.24, L(-19, 19), 985.24, 14.5, 21.5, 6, 967.24, r4, 75, -19, 13.0, 24, 1),
    ("Pier 5WB", "I-80 WB Unit 1", 5, 31.5, 0, 1.2083, 1012.32, 1012.32, L(-19, 19), 986.32, 16, 20, 5.5, 967.82, r5, 75, -19, 13.2292, 16, 1),
    ("Pier 6WB", "I-80 WB Unit 2", 1, 53.5, 1.1302, 1.2813, 1013.07, 1014.92, L(-41, -41 + 27.3333, 41 - 27.3333, 41), 986.28, 18, 20, 6, 961.28, r6, 65, 41 - 27.3333, 2.1823, 21, 0),
    ("Pier 7WB", "I-80 WB Unit 2", 2, 47, 1.0885, 1.3229, 1014.60, 1017.01, L(-34.5, 0, 34.5), 988.92, 20, 29, 7, 969.92, r7, 75, 0, 9.3542, 26, 1),
    ("Pier 8WB", "I-80 WB Unit 2", 3, 57.5, 1.0729, 1.3385, 1013.97, 1017.38, L(-45, -15, 15, 45), 994.33, 21.5, 14.5, 6, 976.33, r8, 85, 15, 8.4635, 18, -1),
    ("Pier 9WB", "I-80 WB Unit 2", 4, 49, 1.0156, 1.3958, 1012.57, 1016.75, L(-36.5, 0, 36.5), 993.10, 26, 17.5, 5, 977.10, r9, 80, 0, 1.2188, 18, 1),
    ("Pier 10WB", "I-80 WB Unit 2", 5, 45, 1.0, 1.4115, 1011.70, 1015.83, L(-32.5, 0, 32.5), 991.26, 26, 17.5, 5, 974.26, r9, 85, 0, 0.1354, 18, 1),
    ("Pier 12WB", "I-80 WB Unit 3", 1, 56, 0.9688, 1.4375, 1010.53, 1016.33, L(-43.5, -14.5, 14.5, 43.5), 991.17, 16, 20.5, 5.5, 971.67, r12, 80, 14.5, 0.1615, 16, 1),
    ("Pier 13WB", "I-80 WB Unit 3", 2, 46.5, 0.9583, 1.4479, 1010.23, 1015.25, L(-34, 0, 34), 988.90, 24, 30, 6, 970.90, r13, 65, 0, 6.5521, 20, 1),
    ("Pier 14WB", "I-80 WB Unit 3", 3, 42, 0.9531, 1.4531, 1009.49, 1014.17, L(-29.5, 0, 29.5), 989.18, 19, 20.5, 5, 972.18, r14, 85, 0, 3.1042, 16, 1),
    ("Pier 15WB", "I-80 WB Unit 3", 4, 41, 0.9479, 1.4583, 1008.39, 1013.10, L(-28.5, 0, 28.5), double.NaN, 19, 20.5, 5, 978.10, r14, 85, 0, 3.0365, 16, 1),
};
foreach (var p in ip)
{
    var b = Doc.Project.Bridges.First(x => x.Name == p.bridge);
    if (b.Supports[p.j].Name != p.name) { Print($"  {p.name}: support {p.j} of {p.bridge} is {b.Supports[p.j].Name} -- skipped"); continue; }
    double m = (p.eR - p.eL) / (2 * p.half);
    double Top(double x) => p.eL + m * (x + p.half);
    double kL = p.cols.Min() - 4.5, kR = p.cols.Max() + 4.5;
    // Seats (S, Z) of this support from the current model (unchanged by the cap).
    var unit0 = Doc.Model(b.Name).Substructure!.Units.First(u => u.SupportIndex == p.j);
    var seats = unit0.Pedestals.Select(q => (q.S, q.SeatZ)).ToList();
    (Func<double, double> S, double score, string why) Try(double xRoad)
    {
        var S = SFromPlan(b, p.j, xRoad);
        double s0 = S(-p.half - p.extL), s1 = S(p.half + p.extR);
        int off = seats.Count(t => t.S < s0 || t.S > s1);
        // plan x of a model S: invert S(x) = S(0) + Ft(x)
        double X(double s) => (s - S(0)) / Ft(1);
        var h = seats.Select(t => t.SeatZ / Ft(1) - Top(X(t.S))).ToList();
        double spread = h.Count > 0 ? h.Max() - h.Min() : 0;
        bool below = h.Any(v => v < 0);
        return (S, off * 100 + (below ? 50 : 0) + spread, $"{off} seats off the cap, pedestals {h.DefaultIfEmpty().Min() * 12:F1} to {h.DefaultIfEmpty().Max() * 12:F1} in");
    }
    var a = Try(p.refCol - p.road); var c = Try(p.refCol + p.road);
    // Side read from the pile layout drawing (+1 right / -1 left of the column); 0 = not legible, use the seat fit.
    bool left = p.side < 0 || (p.side == 0 && a.score <= c.score);
    var best = left ? a : c;
    Print($"  {p.name}: roadway CL {(left ? "left" : "right")} of column at {p.refCol} ft{(p.side == 0 ? " (by seat fit)" : "")} ({best.why}; other side: {(left ? c.why : a.why)})");
    var Sx = best.S;
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.CapProfile = new()
    {
        new(Sx(-p.half), F(Top(-p.half))), new(Sx(p.half), F(Top(p.half))),
        new(Sx(p.half + p.extR), F(Top(p.half) - 4.5)), new(Sx(kR), F(Top(kR) - 7)),
        new(Sx(kL), F(Top(kL) - 7)), new(Sx(-p.half - p.extL), F(Top(-p.half) - 4.5)),
    };
    sd.CapWidth = F(6);
    sd.ColumnCount = p.cols.Length; sd.FirstColumnOffset = Sx(p.cols[0]);
    sd.ColumnSpacings = p.cols.Zip(p.cols.Skip(1), (x, y) => F(y - x)).ToList();
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = F(8); sd.ColumnDepth = FI(4, 6);
    sd.ColumnChamferS = FI(2, 4); sd.ColumnChamferN = FI(1, 3);
    if (!double.IsNaN(p.flare))
    {
        sd.ColumnFlareElevation = F(p.flare); sd.ColumnFlareWidth = F(9); sd.ColumnFlareDepth = FI(5, 6);
        sd.ColumnFlareChamferS = FI(2, 8.5); sd.ColumnFlareChamferN = FI(1, 5.625);
    }
    sd.Foundation = FoundationType.PileCap; sd.FootingThickness = F(p.fT); sd.FoundationTopElevation = F(p.fBot + p.fT);
    sd.FootingLength = F(p.fS); sd.FootingWidth = F(p.fN);
    sd.PileSection = "HP14x73"; sd.PileLength = F(p.pileFt);
    var pl = Grid(p.piles);
    sd.Footings = Enumerable.Range(1, p.cols.Length).Select(k => new FootingDefinition { Column = k, Piles = pl.Select(q => new FootingPoint(q.S, q.N)).ToList() }).ToList();
    // Battered piles (pile layout sheets, 1:4 in the direction shown): on I-80 only the two OUTER N rows batter, outward in N, no
    // diagonals and no S-edge piles; the other piers (1WB-3WB, 8WB, 9WB, 15WB) are plumb. Ratio read as 1 H : 4 V (to be confirmed).
    if (new[] { "Pier 4WB", "Pier 5WB", "Pier 6WB", "Pier 7WB", "Pier 10WB", "Pier 12WB", "Pier 13WB", "Pier 14WB" }.Contains(p.name))
        foreach (var fd in sd.Footings) fd.BatterPerimeter(4, false, false);
    b.Supports[p.j].Substructure = sd;
    Print($"    {p.name}: {sd.Footings.Sum(f => f.Piles.Count(q => q.IsBattered))} battered piles in {sd.Footings.Count} footing(s)");
    if (pl.Count != p.expect) Print($"    PILE COUNT {pl.Count} -- PLANS SAY {p.expect}");
}
Doc.Regenerate();
foreach (var b in Doc.Project.Bridges.Where(x => x.Name.StartsWith("I-80")))
    foreach (var u in Doc.Model(b.Name).Substructure?.Units ?? new())
        if (u.CapOutline.Count > 0)
            Print($"  {u.Name}: pedestals {u.Pedestals.Min(q => q.Height) / In(1):F1} to {u.Pedestals.Max(q => q.Height) / In(1):F1} in ({u.Pedestals.Count}), {u.Foundations.Sum(f => f.Piles.Count)} piles");

var rr = Doc.Regenerate();
Print($"Regenerated {rr.Bridges} bridges in {rr.Elapsed.TotalSeconds:F1} s, {rr.Errors.Count} errors.");
Doc.Save();
