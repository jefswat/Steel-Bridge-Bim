// Field splices of WB I-80 as designed (Design 116):
//  * Locations per girder from the GIRDER DATA tables (sheets 105, 106, 109-110, 112-114): distance along each girder from
//    the CL bearing at a pier / abutment (+ ahead, - back). Girders without a value ("-") are not spliced there.
//  * Plates and bolts from the FIELD SPLICE SCHEDULES (sheet 122): first bolt 2" from the splice CL, 1 1/2" plate edges,
//    1/4" gap; flange bolt lines 3" and 3+gage from the girder CL on 20" flanges (2 @ 3 1/2 gage on 24"), staggered
//    lines at 2 1/2" and 7 1/2" on 18" flanges; web rows down from Dim. "S" below the top of web. 7/8" bolts in 15/16"
//    holes (sheet 93 notes).
// Not modeled: corner clips of the staggered 18" flange plates; fill plates are generated from the actual flange
// thickness change. Re-runnable; run after build-i80-wb.csx / pier-6a.csx (and before cross-frames-i80.csx).
using SBM.Core.Bridge;

double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double I(double inch) => In(inch);
// Flange: outer plate t x w, inner plates t x w (2), spaces @ spacing, flange width (sets the bolt lines).
FlangeSplicePattern Fl(double to, double wo, double ti, double wi, int n, double s)
{
    var p = new FlangeSplicePattern { OuterThickness = I(to), OuterWidth = I(wo), InnerThickness = I(ti), InnerWidth = I(wi), Spaces = n, Spacing = I(s) };
    if (wo <= 18.01) { p.LineOffsets = new() { I(2.5), I(7.5) }; p.Staggered = true; }
    else if (wo >= 23.99) p.LineOffsets = new() { I(3), I(6.5), I(10) };
    else p.LineOffsets = new() { I(3), I(8) };
    return p;
}
WebSplicePattern Web(double t, int cols, double cs, int rows, double rs, double S) =>
    new() { PlateThickness = I(t), ColumnSpaces = cols, ColumnSpacing = I(cs), RowSpaces = rows, RowSpacing = I(rs), TopRowBelowWebTop = I(S) };
FieldSpliceDetail D(FlangeSplicePattern top, WebSplicePattern web, FlangeSplicePattern bot) => new()
{
    BoltDiameter = I(0.875), FirstBoltFromJoint = I(2), PlateEndDistance = I(1.5), Top = top, Web = web, Bottom = bot,
};

// ---- schedules (sheet 122)
var sch = new Dictionary<int, FieldSpliceDetail>();
void S(int[] nos, FieldSpliceDetail d) { foreach (int n in nos) sch[n] = d; }
var w25 = Web(0.375, 2, 3, 25, 3.75, 4.125);
var w21 = Web(0.375, 2, 3, 21, 4.5, 3.75);
S(new[] { 1 }, D(Fl(0.375, 20, 0.4375, 9, 3, 3), w25, Fl(0.5, 20, 0.5625, 9, 4, 3)));
S(new[] { 2 }, D(Fl(0.4375, 20, 0.5, 9, 5, 3), w25, Fl(0.5, 20, 0.5625, 9, 6, 3)));
S(new[] { 3, 4 }, D(Fl(0.375, 20, 0.4375, 9, 5, 3), Web(0.375, 2, 3, 22, 4.25, 4.25), Fl(0.5, 20, 0.5625, 9, 5, 3)));
S(new[] { 5 }, D(Fl(0.375, 20, 0.4375, 9, 5, 3), w25, Fl(0.5, 20, 0.5625, 9, 5, 3)));
S(new[] { 6 }, D(Fl(0.375, 20, 0.4375, 9, 4, 3), w21, Fl(0.5, 20, 0.5625, 9, 5, 3)));
S(new[] { 7 }, D(Fl(0.375, 18, 0.4375, 8, 9, 2), w21, Fl(0.375, 18, 0.4375, 8, 11, 2)));
S(new[] { 8 }, D(Fl(0.4375, 18, 0.5, 8, 11, 2), w25, Fl(0.4375, 18, 0.5, 8, 11, 2)));
S(new[] { 9 }, D(Fl(0.5, 24, 0.5625, 11, 4, 3), w21, Fl(0.5, 24, 0.5625, 11, 4, 3)));
S(new[] { 10 }, D(Fl(0.5, 24, 0.5625, 11, 3, 3), Web(0.375, 2, 3, 27, 3.5, 3.75), Fl(0.5625, 24, 0.625, 11, 4, 3)));
S(new[] { 11 }, D(Fl(0.375, 18, 0.4375, 8, 7, 2), w25, Fl(0.4375, 18, 0.5, 8, 7, 2)));
S(new[] { 12 }, D(Fl(0.4375, 20, 0.5, 9, 5, 3), w25, Fl(0.5, 20, 0.5625, 9, 6, 3)));
S(new[] { 13, 14, 17 }, D(Fl(0.375, 18, 0.4375, 8, 9, 2), w21, Fl(0.375, 18, 0.4375, 8, 9, 2)));
S(new[] { 15, 16 }, D(Fl(0.375, 20, 0.4375, 9, 5, 3), w21, Fl(0.5, 20, 0.5625, 9, 5, 3)));
S(new[] { 18, 19, 20, 21 }, D(Fl(0.375, 18, 0.4375, 8, 7, 2), w21, Fl(0.375, 18, 0.4375, 8, 7, 2)));
var w23 = Web(0.375, 1, 3, 23, 3, 3.5);
S(new[] { 22 }, D(Fl(0.375, 18, 0.4375, 8, 9, 2), w23, Fl(0.5625, 18, 0.625, 8, 9, 2)));
S(new[] { 23, 24, 25, 26, 27, 28 }, D(Fl(0.375, 18, 0.4375, 8, 9, 2), w23, Fl(0.5, 20, 0.5625, 9, 4, 3)));
S(new[] { 29 }, D(Fl(0.375, 20, 0.4375, 9, 4, 3), w23, Fl(0.5625, 20, 0.625, 9, 5, 3)));

// ---- locations: (splice no., support index, offsets per girder letter; NaN = no splice on that girder)
double N = double.NaN;
void Unit(string name, (int no, int sup, double[] off, int[]? supPerGirder)[] rows)
{
    var b = Doc.Project.Bridges.First(x => x.Name == name);
    b.FieldSpliceGap = I(0.25);
    var old = b.FieldSplices.ToList();
    b.FieldSplices = new();
    foreach (var (no, sup, off, sg) in rows)
    {
        var fs = new FieldSpliceDefinition { Number = no, Detail = sch[no].Clone(), Station = old.Count > 0 ? old[Math.Min(b.FieldSplices.Count, old.Count - 1)].Station : 0 };
        for (int g = 0; g < off.Length; g++)
            if (!double.IsNaN(off[g])) fs.Girders.Add(new GirderSpliceLocation { Girder = g, Support = sg?[g] ?? sup, Offset = off[g] });
        b.FieldSplices.Add(fs);
    }
}
double[] All(int n, double v) => Enumerable.Repeat(v, n).ToArray();

// Unit 1 (girders A-F; supports 0 West Abut .. 5 Pier 5WB): same for every girder (sheet 105).
Unit("I-80 WB Unit 1", new[]
{
    (1, 0, All(6, F(50)), (int[]?)null), (2, 1, All(6, -F(55)), null), (3, 1, All(6, F(80, 6)), null), (4, 2, All(6, -F(69)), null),
    (5, 2, All(6, F(64)), null), (6, 3, All(6, -F(64)), null), (7, 3, All(6, F(70)), null), (8, 4, All(6, -F(70)), null),
    (9, 4, All(6, F(64)), null), (10, 5, All(6, -F(45)), null),
});

// Unit 2 (girders A-K; supports 0 Pier 5WB .. 6 Pier 11WB, 7 = Pier 6A where A-E begin).
int[] s11 = { 7, 7, 7, 7, 7, 0, 0, 0, 0, 0, 0 };
Unit("I-80 WB Unit 2", new[]
{
    (11, 0, new[] { F(41, 11.5625), F(42, 8.1875), F(43, 4.6875), F(44, 1.375), F(44, 8.5625), F(45), F(45), F(45), F(45), F(45), F(45) }, s11),
    (12, 1, new[] { -F(50, 0.75), -F(50, 0.5), -F(50, 0.375), -F(50, 0.1875), -F(50, 0.0625), -F(50), -F(50), -F(50), -F(50), -F(50), -F(50) }, (int[]?)null),
    (13, 1, new[] { F(70, 1.0625), F(70, 0.75), F(70, 0.5625), F(70, 0.25), N, F(70), F(70), F(70), F(70), F(70), F(70) }, null),
    (14, 2, new[] { -F(68, 1), -F(68, 0.75), -F(68, 0.5625), -F(68, 0.25), N, -F(68), -F(68), -F(68), -F(68), -F(68), -F(68) }, null),
    (15, 2, new[] { F(67, 1), F(67, 0.6875), F(67, 0.5625), N, N, F(67), F(67), F(67), F(67), F(67), F(67) }, null),
    (16, 3, new[] { -F(69, 0.875), -F(68, 6), -F(67, 2.4375), N, N, -F(65, 7.75), -F(65, 6.6875), -F(65, 5), -F(65, 2.8125), -F(65, 0.125), -F(64, 9) }, null),
    (17, 3, new[] { F(65, 5.9375), F(68, 2.4375), N, N, N, F(62, 2.375), F(61, 11.375), F(61, 8.4375), F(61, 5.5625), F(61, 2.75), F(61) }, null),
    (18, 4, new[] { -F(42, 5.25), -F(42, 1.3125), N, N, N, -F(41, 4.875), -F(41, 3.25), -F(41, 1.625), -F(41, 0.0625), -F(40, 10.5), -F(40, 9) }, null),
    (19, 4, new[] { F(40, 3.875), N, N, N, N, F(39, 7), F(39, 5.5625), F(39, 4.125), F(39, 2.6875), F(39, 1.3125), F(39) }, null),
    (20, 5, new[] { -F(43, 7.3125), N, N, N, N, -F(43, 5.8125), -F(43, 4.625), -F(43, 3.4375), -F(43, 2.25), -F(43, 1.125), -F(43) }, null),
    (21, 5, new[] { F(33, 5.0625), N, N, N, N, F(33, 4.1875), F(33, 3.3125), F(33, 2.4375), F(33, 1.625), F(33, 0.8125), F(33) }, null),
});

// Unit 3 (girders A-J; supports 0 Pier 11WB .. 5 East Abut).
Unit("I-80 WB Unit 3", new[]
{
    (22, 1, new[] { -F(50), -F(50), -F(47, 6), -F(50), -F(50), -F(50), -F(46), -F(50), -F(50), -F(50) }, (int[]?)null),
    (23, 1, new[] { F(52), N, F(52), F(56), F(52), F(52), F(52), F(52), F(55), F(52) }, null),
    (24, 2, new[] { -F(60, 11.5625), N, -F(61), -F(61), -F(61), -F(61), -F(61), -F(61), -F(61), -F(61) }, null),
    (25, 2, new[] { F(60), N, N, F(57, 3), F(55, 6), F(53, 6), F(51, 9), F(49, 9), F(48), F(46) }, null),
    (26, 3, new[] { -F(45), N, N, -F(45, 6), -F(46, 6), -F(47, 9), -F(48, 9), -F(50), -F(51), -F(52) }, null),
    (27, 3, new[] { F(48), N, N, F(48), F(48), F(48), F(48), F(48), F(48), F(48) }, null),
    (28, 4, new[] { -F(52), N, N, -F(52), -F(52), -F(52), -F(52), -F(52), -F(52), -F(52) }, null),
    (29, 4, new[] { F(50), N, N, F(50), F(50), F(50), F(50), F(50), F(50), F(50) }, null),
});

// ---- flange plates per the girder elevations (sheets 101-104, 106-108, 110-113A, 114, 116): plates change at the field
// splices (field sections) and at the shop splices dimensioned from the pier CL (thickened zone each side of the pier).
// Run ends: ("FS", n) = at field splice n; ("P", j, ft) = ft along the girder from the CL bearing at support j (- back).
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

// Unit 1 (all girders): pier zones 1WB +-22, 2WB +-25, 3WB +-20, 4WB 20 back / 19 ahead.
Layout("I-80 WB Unit 1", Enumerable.Range(0, 6), L(
    One(T, 20, 0.875, 2), Pier(T, 32, 1.375, 2.25, 1.375, 1, 22, 22, 3), One(T, 20, 0.875, 4), Pier(T, 32, 1.375, 2.25, 1.375, 2, 25, 25, 5),
    One(T, 20, 0.875, 6), Pier(T, 30, 1.25, 2, 1.25, 3, 20, 20, 7), One(T, 18, 0.875, 8), Pier(T, 34, 1.5, 2, 1.5, 4, 20, 19, 9), One(T, 24, 1.125, 0),
    One(B, 20, 0.875, 2), Pier(B, 34, 1.5, 2.25, 1.5, 1, 22, 22, 3), One(B, 20, 0.875, 4), Pier(B, 34, 1.5, 2.5, 1.5, 2, 25, 25, 5) /* 2WB: PL 2 1/2 x 34, sheet 102 / PDF 104 */,
    One(B, 20, 0.875, 6), Pier(B, 36, 1.5, 2, 1.5, 3, 20, 20, 7), One(B, 18, 0.875, 8), Pier(B, 34, 1.5, 2, 1.5, 4, 20, 19, 9), One(B, 24, 1.125, 0)));

// Unit 2 (all girders; B-E end at cross girders): 6WB +-18, 7WB +-18, 8WB 29/22, 9WB 25/22, 10WB 27/18.
Layout("I-80 WB Unit 2", Enumerable.Range(0, 11), L(
    One(T, 18, 0.875, 11), One(T, 20, 0.875, 12), Pier(T, 30, 1.375, 2, 1.375, 1, 18, 18, 13), One(T, 18, 0.875, 14),
    Pier(T, 32, 1.375, 2, 1.375, 2, 18, 18, 15), One(T, 20, 0.875, 16), Pier(T, 30, 1.375, 1.625, 1.375, 3, 29, 22, 17),
    One(T, 18, 0.875, 18), Pier(T, 18, 0.875, 1.375, 0.875, 4, 25, 22, 19), One(T, 18, 0.875, 20), Pier(T, 18, 0.875, 1.375, 0.875, 5, 27, 18, 21),
    One(T, 18, 0.875, 0),
    One(B, 18, 0.875, 11), One(B, 20, 0.875, 12), Pier(B, 30, 1.5, 2, 1.5, 1, 18, 18, 13), One(B, 18, 0.875, 14),
    Pier(B, 32, 1.5, 2, 1.5, 2, 18, 18, 15), One(B, 20, 0.875, 16), Pier(B, 34, 1.5, 1.75, 1.5, 3, 29, 22, 17),
    One(B, 18, 0.875, 18), Pier(B, 18, 0.875, 1.375, 0.875, 4, 25, 22, 19), One(B, 18, 0.875, 20), Pier(B, 18, 0.875, 1.375, 0.875, 5, 27, 18, 21),
    One(B, 18, 0.875, 0)));

// Unit 3: 12WB +-19, 13WB "Dim. A" back (per girder, sheet 113A) / 26 ahead, 14WB 25/23, 15WB 21/30.
double[] dimA = { 23.5, 0, 24, 24.5, 25.5, 26.5, 27.75, 28.75, 27, 31 };
for (int g = 0; g < 10; g++)
    Layout("I-80 WB Unit 3", new[] { g }, L(
        One(T, 18, 0.875, 22), Pier(T, 26, 1.125, 2, 1.125, 1, 19, 19, 23), One(T, 18, 0.875, 24), Pier(T, 26, 1.125, 2, 1.125, 2, dimA[g], 26, 25),
        One(T, 18, 0.875, 26), Pier(T, 26, 1.125, 2, 1.125, 3, 25, 23, 27), One(T, 18, 0.875, 28), Pier(T, 26, 1.125, 2, 1.125, 4, 21, 30, 29),
        One(T, 20, 0.875, 0),
        One(B, 18, 1.25, 22), Pier(B, 26, 1.25, 2, 1.125, 1, 19, 19, 23), One(B, 20, 0.875, 24), Pier(B, 26, 1.125, 2, 1.125, 2, dimA[g], 26, 25),
        One(B, 20, 0.875, 26), Pier(B, 26, 1.125, 2, 1.125, 3, 25, 23, 27), One(B, 20, 0.875, 28), Pier(B, 26, 1.125, 2, 1.25, 4, 21, 30, 29),
        One(B, 20, 1.25, 0)));

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var name in new[] { "I-80 WB Unit 1", "I-80 WB Unit 2", "I-80 WB Unit 3" })
{
    var md = Doc.Model(name);
    Print($"{name}: {md.Framing.Splices.Count} girder splices, {md.Steel!.Bolts.Count(bb => bb.Assembly.StartsWith("FS"))} splice bolts");
    foreach (var d in md.Framing.Diagnostics.Concat(md.Steel.Diagnostics).Where(d => d.Message.Contains("plice") || d.Message.StartsWith("FS")).Take(15))
        Print("  " + d.Message);
}
Doc.Save();
