// Field splices of Ramp G as designed (Design 316):
//  * Schedule = FIELD SPLICE DETAILS, design sheet 104 (PDF 106); transcription in docs/plan-data/ramp-g-splice-schedule.md.
//    Units 1-3: suffix A = girder A, B = girders B-D (different plates/bolts per group -> two definitions with the same
//    plan number, each listing its girders). Unit 4 (FS23-30): no suffix = all girders.
//    First bolt 2" from the splice CL, plate ends 1 1/2" past the last bolt, 1/4" gap. Flange bolt lines by MIN. flange
//    width: 18" = 2 1/2 & 7 1/2 staggered; 20" = 3, 8; 22" = 3, 9; 24" = 3, 6 1/2, 10; 26" = 3, 7, 11; 34" = 3, 6, 9, 12, 15.
//    7/8" bolts in 15/16" holes (Superstructure Notes, sheet 83 / PDF 85).
//  * Locations per girder from the girder elevations / girder data (PDF 91-102): distance along each girder from the CL
//    bearing at a pier (+ ahead, - back).
//  * Flange plates and web thickness per the girder elevations (below).
// Not modeled: corner clips of the 18" outer plates (not dimensioned); fill plates come from the actual flange thicknesses.
// Re-runnable; run after build-ramp-g.csx (and before cross-frames-ramp-g.csx).
using SBM.Core.Bridge;

double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double I(double inch) => In(inch);
// Flange: outer plate t x w, inner plates t x w (2), spaces @ spacing each side, MIN. flange width (selects the bolt lines).
FlangeSplicePattern Fl(double to, double wo, double ti, double wi, int n, double s, double minW)
{
    var p = new FlangeSplicePattern { OuterThickness = I(to), OuterWidth = I(wo), InnerThickness = I(ti), InnerWidth = I(wi), Spaces = n, Spacing = I(s) };
    double[] lines = minW switch
    {
        18 => new[] { 2.5, 7.5 },
        20 => new[] { 3.0, 8 },
        22 => new[] { 3.0, 9 },
        24 => new[] { 3, 6.5, 10 },
        26 => new[] { 3.0, 7, 11 },
        34 => new[] { 3.0, 6, 9, 12, 15 },
        _ => throw new ArgumentException($"no bolt-line rule for a {minW}\" flange"),
    };
    p.LineOffsets = lines.Select(I).ToList();
    p.Staggered = minW == 18;
    return p;
}
WebSplicePattern Web(double t, int cols, int rows, double rs, double S) =>
    new() { PlateThickness = I(t), ColumnSpaces = cols, ColumnSpacing = I(3), RowSpaces = rows, RowSpacing = I(rs), TopRowBelowWebTop = I(S) };
FieldSpliceDetail D(FlangeSplicePattern top, WebSplicePattern web, FlangeSplicePattern bot) => new()
{
    BoltDiameter = I(0.875), FirstBoltFromJoint = I(2), PlateEndDistance = I(1.5), Top = top, Web = web, Bottom = bot,
};

// ---- recurring patterns (sheet 104)
var t18a = Fl(0.375, 18, 0.4375, 8, 7, 2, 18);      // 3/8x18x2'-11, 7 @ 2
var t18b = Fl(0.375, 18, 0.4375, 8, 9, 2, 18);      // 3/8x18x3'-7, 9 @ 2
var w21 = Web(0.375, 1, 21, 3.5, 4.25);
var w20 = Web(0.375, 1, 20, 3.75, 3.5);
var w23 = Web(0.375, 1, 23, 3.25, 3.625);
var w25 = Web(0.375, 1, 25, 3, 3.5);
var w26 = Web(0.375, 1, 26, 3.25, 3.75);
var w28 = Web(0.375, 1, 28, 3, 4);
var w20x2 = Web(0.375, 2, 20, 4.25, 3.5);           // 19" plates, 2 @ 3 columns
var b20u1 = Fl(0.5, 20, 0.5625, 9, 4, 3, 20);       // 1/2x20x2'-7
var b20 = Fl(0.5, 20, 0.5625, 9, 5, 3, 20);         // 1/2x20x3'-1
var b22 = Fl(0.5, 22, 0.5625, 10, 5, 3, 22);        // 1/2x22x3'-1
var b22u3 = Fl(0.625, 22, 0.6875, 10, 6, 3, 22);    // 5/8x22x3'-7
var b24s = Fl(0.5625, 24, 0.625, 11, 4, 3, 24);     // 9/16x24x2'-7
var b24 = Fl(0.5625, 24, 0.625, 11, 5, 3, 24);      // 9/16x24x3'-1

// (number, detail for girder A, detail for girders B-D); same object twice = all girders alike.
var sch = new Dictionary<int, (FieldSpliceDetail a, FieldSpliceDetail bd)>();
void S(int no, FieldSpliceDetail a, FieldSpliceDetail? bd = null) => sch[no] = (a, bd ?? a);
// Unit 1
S(1, D(t18a, w21, t18b)); S(2, D(t18a, w21, t18b));
S(3, D(t18b, w20, t18b));
S(4, D(t18b, w21, b20u1));
S(5, D(t18b, w23, b20u1), D(t18a, w21, b20u1));
S(6, D(t18b, w23, b20u1), D(t18a, w21, t18b));
S(7, D(t18b, w23, b20u1), D(t18a, w21, t18b));
S(8, D(t18b, w23, b22), D(t18a, w21, t18b));
// Unit 2
var bd2 = D(t18a, w21, b20);
S(9, D(Fl(0.5, 18, 0.625, 8, 11, 2, 18), w25, Fl(0.625, 24, 0.6875, 11, 5, 3, 24)), bd2);
S(10, D(Fl(0.4375, 18, 0.5, 8, 9, 2, 18), w25, b24), bd2);
S(11, D(t18b, w23, b24s), bd2);
S(12, D(Fl(0.4375, 18, 0.5, 8, 9, 2, 18), w25, b24), bd2);
S(13, D(t18b, w23, b24s), bd2);
S(14, D(t18b, w23, b24s), bd2);
// Unit 3
var t20u3 = Fl(0.375, 20, 0.4375, 9, 5, 3, 20);     // 3/8x20x3'-1
var t24u3 = Fl(0.625, 24, 0.6875, 11, 5, 3, 24);    // 5/8x24x3'-1
var b34 = Fl(0.8125, 34, 0.875, 16, 6, 3, 34);      // 13/16x34x3'-7
var b26 = Fl(0.5, 26, 0.5625, 12, 5, 3, 26);        // 1/2x26x3'-1
var t18u3 = Fl(0.4375, 18, 0.5, 8, 11, 2, 18);      // 7/16x18x4'-3
var b18u3 = Fl(0.4375, 18, 0.5, 8, 9, 2, 18);       // 7/16x18x3'-7
var t20s = Fl(0.375, 20, 0.4375, 9, 4, 3, 20);      // 3/8x20x2'-7
S(15, D(Fl(0.5, 20, 0.5625, 9, 6, 3, 20), w20x2, Fl(0.6875, 24, 0.75, 11, 5, 3, 24)), D(t20u3, w26, Fl(0.5, 22, 0.5625, 10, 6, 3, 22)));
S(16, D(t24u3, w28, b34), D(t20u3, w26, b26));
S(17, D(t24u3, w28, b34), D(t20u3, w26, b26));
S(18, D(t18u3, w20x2, b22u3), D(t18b, w26, b18u3));
S(19, D(t18u3, w20x2, b22u3), D(t18b, w26, b18u3));
S(20, D(t20s, w28, b22u3), D(t18b, w26, b20));
S(21, D(t20s, w28, b22u3), D(t18b, w26, b20));
S(22, D(Fl(0.4375, 20, 0.5, 9, 5, 3, 20), Web(0.375, 2, 21, 4, 4), b22u3), D(t18b, w26, b20));
// Unit 4 (all girders)
var t20u4 = Fl(0.4375, 20, 0.5, 9, 5, 3, 20);       // 7/16x20x3'-1
S(23, D(t18a, w26, b22));
S(24, D(t18a, w26, t18a));
S(25, D(t18b, w28, t18b));
S(26, D(t20u4, w28, b24));
S(27, D(t20u4, w28, b24));
S(28, D(t18b, w28, t18b));
S(29, D(t18a, w26, t18a));
S(30, D(t18a, w26, b22));

// ---- locations: (splice no., support index, offset per girder A-D)
void Unit(string name, (int no, int sup, double[] off)[] rows)
{
    var b = Doc.Project.Bridges.First(x => x.Name == name);
    b.FieldSpliceGap = I(0.25);
    var old = b.FieldSplices.ToList();
    b.FieldSplices = new();
    foreach (var (no, sup, off) in rows)
    {
        var (da, dbd) = sch[no];
        double sta = old.Count > 0 ? old[Math.Min(b.FieldSplices.Count, old.Count - 1)].Station : 0;
        IEnumerable<int[]> groups = ReferenceEquals(da, dbd) ? new[] { new[] { 0, 1, 2, 3 } } : new[] { new[] { 0 }, new[] { 1, 2, 3 } };
        foreach (var grp in groups)
        {
            var fs = new FieldSpliceDefinition { Number = no, Detail = (grp[0] == 0 ? da : dbd).Clone(), Station = sta };
            foreach (int g in grp) fs.Girders.Add(new GirderSpliceLocation { Girder = g, Support = sup, Offset = off[g] });
            b.FieldSplices.Add(fs);
        }
    }
}
double[] All(double v) => new[] { v, v, v, v };
double[] Neg(double[] v) => v.Select(x => -x).ToArray();
double[] set50 = { F(50), F(49, 5.9375), F(48, 11.875), F(48, 5.75) };
double[] set65 = { F(65), F(64, 4.125), F(63, 8.1875), F(63, 0.3125) };

// Unit 1 (supports 0 South Abut, 1 Pier 1G .. 5 Pier 5G)
Unit("Ramp G Unit 1", new[]
{
    (1, 1, Neg(All(F(50)))), (2, 1, All(F(50))), (3, 2, Neg(All(F(50)))), (4, 2, All(F(50))),
    (5, 3, Neg(set50)), (6, 3, set50), (7, 4, Neg(set50)), (8, 4, set50),
});
// Unit 2 (0 Pier 5G .. 4 Pier 9G)
Unit("Ramp G Unit 2", new[]
{
    (9, 1, Neg(set50)), (10, 1, set50), (11, 2, Neg(set50)), (12, 2, set50), (13, 3, Neg(set50)), (14, 3, set50),
});
// Unit 3 (0 Pier 9G .. 5 Pier 14G)
Unit("Ramp G Unit 3", new[]
{
    (15, 1, Neg(set65)), (16, 1, set65), (17, 2, Neg(set65)), (18, 2, set65),
    (19, 3, Neg(set50)), (20, 3, set50), (21, 4, Neg(set50)), (22, 4, set50),
});
// Unit 4 (0 Pier 14G .. 5 Pier 19G)
Unit("Ramp G Unit 4", new[]
{
    (23, 1, Neg(set50)),
    (24, 1, new[] { F(50), F(50, 0.125), F(49, 10.75), F(49, 10.1875) }),
    (25, 2, Neg(All(F(68, 6)))),
    (26, 2, new[] { F(62, 0.1875), F(61, 10.4375), F(61, 6.3125), F(61) }),
    (27, 3, Neg(new[] { F(69, 9.625), F(69, 2.125), F(68, 6.875), F(68) })),
    (28, 3, new[] { F(63, 4.625), F(62, 10.8125), F(62, 5.1875), F(62) }),
    (29, 4, Neg(new[] { F(49, 0.9375), F(49, 4.625), F(49, 8.625), F(50) })),
    (30, 4, new[] { F(49, 0.9375), F(49, 4.625), F(49, 8.625), F(50) }),
});

// ---- flange plates per the girder elevations (PDF 92-93, 95, 98-99, 102; docs/plan-data/ramp-g-girder-plates.md): plates
// change at the field splices and at the shop splices bracketing the thick plate over each pier (extents back / ahead).
// Every run was checked against the splice schedule's MIN. flange widths. Unit 1 B-D pier-zone extents assumed = girder A.
PlateRun R(GirderPlate pl, double w, double t, object end, double off = 0) => end is int fs
    ? new PlateRun { Plate = pl, Width = I(w), Thickness = I(t), EndSplice = fs }
    : new PlateRun { Plate = pl, Width = I(w), Thickness = I(t), EndSupport = (int)(double)end, EndOffset = F(off) };
// Pier section: plate (w, t) from the previous splice to -back, thick (w, tk) to +ahead, plate (w, t2) to the next splice.
IEnumerable<PlateRun> Pier(GirderPlate pl, double w, double t, double tk, double t2, int sup, double back, double ahead, int nextFs) => new[]
{
    R(pl, w, t, (double)sup, -back), R(pl, w, tk, (double)sup, ahead), R(pl, w, t2, nextFs),
};
const GirderPlate T = GirderPlate.TopFlange, B = GirderPlate.BottomFlange;
void Layout(string name, IEnumerable<int> girders, IEnumerable<PlateRun> runs)
{
    var b = Doc.Project.Bridges.First(x => x.Name == name);
    b.Girders.Section.Layouts.RemoveAll(l => l.Girders.Intersect(girders).Any());
    b.Girders.Section.Layouts.Add(new GirderPlateLayout { Girders = girders.ToList(), Runs = runs.ToList() });
}
List<PlateRun> L(params IEnumerable<PlateRun>[] parts) => parts.SelectMany(x => x).ToList();
PlateRun[] One(GirderPlate pl, double w, double t, int fs) => new[] { R(pl, w, t, fs) };
int[] A = { 0 }, BD = { 1, 2, 3 }, ABCD = { 0, 1, 2, 3 };

// Unit 1: 1G +-19, 2G 19 back / 17 ahead, 3G and 4G +-17.
Layout("Ramp G Unit 1", A, L(
    One(T, 18, 0.875, 1), Pier(T, 20, 1, 1.875, 1, 1, 19, 19, 2), One(T, 18, 0.875, 3), Pier(T, 22, 1.125, 2, 1.125, 2, 19, 17, 4),
    One(T, 18, 0.875, 5), Pier(T, 24, 1.125, 2, 1.125, 3, 17, 17, 6), One(T, 18, 0.875, 7), Pier(T, 24, 1.125, 2, 1.125, 4, 17, 17, 8), One(T, 18, 0.875, 0),
    One(B, 18, 0.875, 1), Pier(B, 20, 1.125, 1.875, 1.125, 1, 19, 19, 2), One(B, 18, 0.875, 3), Pier(B, 22, 1.125, 2, 1.125, 2, 19, 17, 4),
    One(B, 20, 1.125, 5), Pier(B, 24, 1.25, 2, 1.25, 3, 17, 17, 6), One(B, 20, 1.125, 7), Pier(B, 24, 1.25, 2, 1.25, 4, 17, 17, 8), One(B, 22, 1.125, 0)));
Layout("Ramp G Unit 1", BD, L(
    One(T, 18, 0.875, 1), Pier(T, 20, 1, 1.875, 1, 1, 19, 19, 2), One(T, 18, 0.875, 3), Pier(T, 20, 1.125, 2, 1.125, 2, 19, 17, 4),
    One(T, 18, 0.875, 5), Pier(T, 20, 1, 2, 1, 3, 17, 17, 6), One(T, 18, 0.875, 7), Pier(T, 20, 1, 2, 1, 4, 17, 17, 8), One(T, 18, 0.875, 0),
    One(B, 18, 0.875, 1), Pier(B, 20, 1.125, 1.875, 1.125, 1, 19, 19, 2), One(B, 18, 0.875, 3), Pier(B, 20, 1.125, 2, 1.125, 2, 19, 17, 4),
    One(B, 20, 0.875, 5), Pier(B, 20, 1.25, 2, 1.25, 3, 17, 17, 6), One(B, 18, 0.875, 7), Pier(B, 20, 1.125, 2, 1.125, 4, 17, 17, 8), One(B, 18, 0.875, 0)));

// Unit 2 (PDF 95): A 6G +-17, 7G 17/18, 8G +-17; B-D 6G +-17, 7G 17'-6 / 17, 8G +-17.
Layout("Ramp G Unit 2", A, L(
    One(T, 18, 1.125, 9), Pier(T, 30, 1.5, 2.125, 1.375, 1, 17, 17, 10), One(T, 18, 0.875, 11), Pier(T, 28, 1.25, 2, 1.375, 2, 17, 18, 12),
    One(T, 18, 0.875, 13), Pier(T, 26, 1.25, 2, 1.375, 3, 17, 17, 14), One(T, 18, 0.875, 0),
    One(B, 24, 1.5, 9), Pier(B, 30, 1.5, 2.125, 1.5, 1, 17, 17, 10), One(B, 24, 1.25, 11), Pier(B, 28, 1.375, 2, 1.375, 2, 17, 18, 12),
    One(B, 24, 1, 13), Pier(B, 26, 1.375, 2, 1.375, 3, 17, 17, 14), One(B, 24, 1.25, 0)));
Layout("Ramp G Unit 2", BD, L(
    One(T, 18, 0.75, 9), Pier(T, 26, 1.125, 1.875, 1.125, 1, 17, 17, 10), One(T, 18, 0.75, 11), Pier(T, 24, 1, 1.875, 1.125, 2, 17.5, 17, 12),
    One(T, 18, 0.75, 13), Pier(T, 22, 1, 1.875, 1.125, 3, 17, 17, 14), One(T, 18, 0.75, 0),
    One(B, 20, 1, 9), Pier(B, 26, 1.25, 1.875, 1.25, 1, 17, 17, 10), One(B, 20, 0.875, 11), Pier(B, 24, 1.125, 1.875, 1.25, 2, 17.5, 17, 12),
    One(B, 20, 0.875, 13), Pier(B, 22, 1.125, 1.875, 1.125, 3, 17, 17, 14), One(B, 20, 0.875, 0)));

// Unit 3 (PDF 98-99): A 10G/11G +-22'-6, 12G +-18, 13G 18 / 22'-6; B-D +-19'-6 at every pier.
Layout("Ramp G Unit 3", A, L(
    One(T, 20, 1, 15), Pier(T, 34, 1.5, 2.25, 1.5, 1, 22.5, 22.5, 16), One(T, 24, 1.5, 17), Pier(T, 34, 1.5, 2, 1.5, 2, 22.5, 22.5, 18),
    One(T, 18, 0.875, 19), Pier(T, 28, 1.25, 2, 1.25, 3, 18, 18, 20), One(T, 20, 0.875, 21), Pier(T, 30, 1.25, 2, 1.25, 4, 18, 22.5, 22),
    One(T, 20, 0.875, 0),
    One(B, 24, 1.625, 15), Pier(B, 34, 1.625, 2.25, 1.625, 1, 22.5, 22.5, 16), One(B, 34, 1.875, 17), Pier(B, 34, 1.625, 2, 1.5, 2, 22.5, 22.5, 18),
    One(B, 22, 1.375, 19), Pier(B, 28, 1.375, 2, 1.375, 3, 18, 18, 20), One(B, 22, 1.375, 21), Pier(B, 30, 1.375, 2, 1.375, 4, 18, 22.5, 22),
    One(B, 22, 1.375, 0)));
Layout("Ramp G Unit 3", BD, L(
    One(T, 20, 0.875, 15), Pier(T, 30, 1.375, 2, 1.375, 1, 19.5, 19.5, 16), One(T, 20, 0.875, 17), Pier(T, 26, 1.25, 2, 1.25, 2, 19.5, 19.5, 18),
    One(T, 18, 0.875, 19), Pier(T, 24, 1.125, 1.875, 1.125, 3, 19.5, 19.5, 20), One(T, 18, 0.875, 21), Pier(T, 24, 1.125, 2, 1.125, 4, 19.5, 19.5, 22),
    One(T, 18, 0.875, 0),
    One(B, 22, 1.125, 15), Pier(B, 30, 1.5, 2, 1.5, 1, 19.5, 19.5, 16), One(B, 26, 1.125, 17), Pier(B, 26, 1.375, 2, 1.375, 2, 19.5, 19.5, 18),
    One(B, 18, 1, 19), Pier(B, 24, 1.125, 1.875, 1.125, 3, 19.5, 19.5, 20), One(B, 20, 1, 21), Pier(B, 24, 1.25, 2, 1.25, 4, 19.5, 19.5, 22),
    One(B, 20, 1, 0)));

// Unit 4 (PDF 102, all girders): 15G +-18, 16G 19/18, 17G 18/19, 18G +-19.
Layout("Ramp G Unit 4", ABCD, L(
    One(T, 18, 0.875, 23), Pier(T, 24, 1, 1.75, 1, 1, 18, 18, 24), One(T, 18, 0.875, 25), Pier(T, 32, 1.375, 2, 1.375, 2, 19, 18, 26),
    One(T, 20, 1, 27), Pier(T, 30, 1.375, 2, 1.375, 3, 18, 19, 28), One(T, 18, 0.875, 29), Pier(T, 24, 1, 1.375, 1, 4, 19, 19, 30),
    One(T, 18, 0.875, 0),
    One(B, 22, 1.125, 23), Pier(B, 24, 1.125, 1.75, 1, 1, 18, 18, 24), One(B, 18, 0.875, 25), Pier(B, 32, 1.5, 2, 1.5, 2, 19, 18, 26),
    One(B, 24, 1.25, 27), Pier(B, 30, 1.5, 2.125, 1.5, 3, 18, 19, 28), One(B, 18, 0.875, 29), Pier(B, 24, 1, 1.375, 1, 4, 19, 19, 30),
    One(B, 22, 1, 0)));

// Webs (girder elevations): 9/16 x 82 in Units 1-2, 5/8 x 92 in Units 3-4.
foreach (var n in new[] { "Ramp G Unit 1", "Ramp G Unit 2" }) Doc.Project.Bridges.First(x => x.Name == n).Girders.Section.WebThickness = I(0.5625);

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var name in new[] { "Ramp G Unit 1", "Ramp G Unit 2", "Ramp G Unit 3", "Ramp G Unit 4" })
{
    var md = Doc.Model(name);
    Print($"{name}: {md.Framing.Splices.Count} girder splices, {md.Steel!.Bolts.Count(bb => bb.Assembly.StartsWith("FS"))} splice bolts");
    foreach (var d in md.Framing.Diagnostics.Concat(md.Steel.Diagnostics).Where(d => d.Message.Contains("plice") || d.Message.StartsWith("FS")).Take(15))
        Print("  " + d.Message);
}
Doc.Save();
