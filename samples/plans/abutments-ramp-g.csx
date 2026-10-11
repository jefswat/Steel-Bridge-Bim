// As-designed South abutment of Ramp G (Design 316, sheets p80-83): Ramp G Unit 1, first support, square (0 deg skew).
// Data: docs/plan-data/abutments.md section 3. Run AFTER build-ramp-g.csx and joints-ramp-g.csx (re-runnable):
//   sbm run samples/plans/abutments-ramp-g.csx model.sbm
// Plan values only; what the plans do not settle is raised as a generator warning (printed at the end).
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double F(double ft, double inch = 0) => Ft(ft) + In(inch);
const double CY = 0.764554857984;

// S along the support line at which the point (S, nOff) is at the given alignment offset (exact, via the alignment).
double SOf(SBM.Core.Alignments.Alignment al, SBM.Core.Framing.SupportLineResult sl, double offset, double nOff = 0)
{
    var t = Vec2.FromAngle(al.AngleAt(sl.Station));
    var nrm = new Vec2(-sl.Direction.Y, sl.Direction.X);
    if (Vec2.Dot(nrm, t) < 0) nrm = -nrm;
    double lo = -400, hi = 400;
    for (int i = 0; i < 80; i++)
    {
        double mid = (lo + hi) / 2;
        double o = al.Inverse(sl.AlignmentPoint + sl.Direction * mid + nrm * nOff).Offset;
        if (o < offset) lo = mid; else hi = mid;
    }
    return (lo + hi) / 2;
}

PlanBarLine B(string mark, int n, double len, double lb, bool varies = false) => new(mark, n, len, lb, varies);

var list = new List<PlanBarLine>
{
    B("8a1", 16, F(37, 6), 1602), B("5b1", 49, F(19, 2), 0), B("5b2", 6, F(8, 7), 0), B("6d1", 67, F(11, 4), 0), B("5d2", 35, F(11), 0),
    B("5d3", 31, F(4, 5), 0), B("5d4", 31, F(3, 8), 0), B("4d6", 31, F(8), 0), B("5e1", 92, F(11, 7), 736, true), B("5e2", 26, F(14, 7), 0),
    B("5e3", 56, F(5, 8), 0), B("6f1", 20, F(22, 2), 0), B("6f2", 24, F(21, 1), 0, true), B("6f3", 4, F(23, 1), 0), B("6f4", 8, F(5, 3), 0),
    B("6g1", 22, F(31, 8), 1046), B("5g2", 44, F(6, 1), 0), B("5g3", 2, F(31, 8), 0), B("6g4", 36, F(5, 5), 0), B("5h1", 24, F(4), 0),
    B("6h2", 20, F(11, 9), 0), B("6h3", 20, F(9), 0), B("5h4", 30, F(12), 0), B("5j1", 32, F(11, 6), 0), B("5j2", 32, F(11, 6), 0), B("8j3", 8, F(5), 0),
    B("5m1", 20, F(8, 1), 0), B("5n1", 20, F(4, 2), 0), B("5n2", 1, F(17, 5), 0), B("5d5", 16, F(3, 6), 58),
};
// Weights of the rows were not re-read (only the totals: epoxy 11,341 lb, stainless 58 lb) -> 0.

var b = Doc.Project.Bridges.First(x => x.Name == "Ramp G Unit 1");
var sup = b.Supports[0];
var sl = Doc.Framing("Ramp G Unit 1").SupportLine(0);
var al = Doc.Alignment(b.AlignmentName);
double xFoot = -(In(17) + F(1, 6) + In(1.5)), xFront = F(2, 11.5);
double nc = (xFoot + xFront) / 2;                                        // first support: N = +x
var ad = new AbutmentDefinition
{
    Source = "Iowa DOT Design 316 Ramp G South abutment, sheets 80-83", LeftEndOffset = -(F(6) + F(2, 11)), RightEndOffset = F(26) + F(2, 11),
    BackwallLeftOffset = -F(6), BackwallRightOffset = F(26),             // the backwall spans gutter to gutter (6g1 31'-8 + 4" cover = 32'-0)
    PlanFootingWidth = F(6), BackwallFrontToBearing = In(17), BearingToFootingFront = F(2, 11.5),
    LedgeBelowTop = F(1, 1),                                              // Ramp G: 1'-1 (3'-7 to the bottom of the taper)
    PileBackRowToBearing = F(1, 6.5), PileFrontRowToBearing = F(1, 5.5), PileTopAboveFootingBottom = F(2), PileLength = F(125),
};
ad.FootingBottom.Add(new CapProfilePoint(0, Ft(991.18)));
ad.BackwallTop.Add(new CapProfilePoint(SOf(al, sl, -F(6)), Ft(1003.98)));
ad.BackwallTop.Add(new CapProfilePoint(0, Ft(1004.22)));
ad.BackwallTop.Add(new CapProfilePoint(SOf(al, sl, F(26)), Ft(1004.66)));
double[] steps = { 995.51, 995.71, 995.90, 996.09 };
for (int i = 0; i < steps.Length; i++) ad.Steps.Add(new AbutmentStepDefinition(i + 1, Ft(steps[i])));
ad.Masks.Add(new MaskwallDefinition { End = AbutmentEnd.Left, Height = F(12, 7), Thickness = In(10), Setback = In(2) });
ad.Masks.Add(new MaskwallDefinition { End = AbutmentEnd.Right, Height = F(13, 3), Thickness = In(10), Setback = In(2) });
// piles: 6 stations from the A end (printed spacing 6'-10, 1'-10 to the end), two rows
double sA = SOf(al, sl, ad.LeftEndOffset, nc);
foreach (double d in new[] { 1.833, 8.667, 15.5, 22.333, 29.167, 36.0 }) ad.PileStations.Add(sA + Ft(d));
var wings = new[]
{
    (End: AbutmentEnd.Left, Name: "A", Gut: -F(6), Top: 1003.98, TopEnd: 1003.08, Bot: 991.98, Foot: new[] { 994.00, 996.54 }),
    (End: AbutmentEnd.Right, Name: "D", Gut: F(26), Top: 1004.66, TopEnd: 1003.74, Bot: 992.66, Foot: new[] { 994.68, 997.21 }),
};
foreach (var w in wings)
{
    var wd = new WingwallDefinition
    {
        Name = w.Name, End = w.End, GutterOffset = w.Gut, Thickness = F(1, 9), Length = F(25, 5), EndHeight = F(4), LevelReach = 0,
        TopAtBackwall = Ft(w.Top), TopAtEnd = Ft(w.TopEnd), BottomAtBackwall = Ft(w.Bot),
    };
    var cfStart = new[] { F(9, 5), F(17, 5) }; var cfH = new[] { F(8, 3), F(5, 6) };
    for (int k = 0; k < 2; k++)
        wd.Counterforts.Add(new CounterfortDefinition
        {
            Start = cfStart[k], Width = F(3), Projection = F(3, 6), Height = cfH[k], FootingTop = Ft(w.Foot[k]),
            HoopSpaces = k == 0 ? 8 : 5, VerticalBarsPerSide = 10, VerticalMark = k == 0 ? "6h2" : "6h3", VerticalFoot = F(1),
        });
    ad.Wings.Add(wd);
}
var rb = ad.Rebar;
rb.FootingLongMark = "8a1"; rb.FootingRowBars = 5; rb.FootingLongPieces = 1; rb.HoopsPerPitch = 9; rb.HoopFirstOffset = In(9);
rb.EndHoopsEach = 2; rb.EndStaplesEach = 3; rb.BackwallLongRowsPerFace = 11; rb.BackwallLongPieces = 1; rb.HoopWidth = F(5, 8); rb.PlanBarList = list;
rb.StepHoopGirders = new() { 4 };                                          // 5n2 hoop at girder D only (printed)
var sd = sup.ResolvedSubstructure.Clone();
sd.Abutment = ad;
sd.PileSection = "HP14x73"; sd.PileType = PileType.SteelHP;
sd.BackwallGap = F(0, 2.5);                                                // plans: 2 1/2" between the deck end (1'-2 1/2) and the backwall face (1'-5)
sd.Reinforcement.EpoxyCoated = true;
sup.Substructure = sd;
Print("UNRESOLVED: maskwall position inside its 1'-2 zone; Ramp G step transverse bars 5m1 (20 = 4 steps x 5 vs 4 per step on I-80); which wing footing piles are circled.");

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
var m = Doc.Substructure("Ramp G Unit 1");
foreach (var u in m.Units.Where(x => x.Abutment != null))
{
    Print($"== {u.Name}");
    foreach (var g in u.Solids.GroupBy(s => s.Kind)) Print($"  {g.Key}: {g.Count()} solids, {g.Sum(s => Math.Abs(s.Mesh.Volume())) / CY:F1} CY");
    double w = m.Marks.Where(x => x.Unit == u.Name).Sum(x => x.TotalMass) / 0.45359237;
    Print($"  rebar {w:F0} lb in {m.Marks.Count(x => x.Unit == u.Name)} marks");
}
foreach (var d in m.Diagnostics.Where(d => d.Message.Contains("South"))) Print($"  {d.Severity}: {d.Message}");
Doc.Save();
