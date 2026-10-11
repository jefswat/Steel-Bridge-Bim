// As-designed abutments of I-80 WB (Design 116): West abutment (Unit 1, first support) and East abutment (Unit 3, last support).
// Data: docs/plan-data/abutments.md (sheets p85-92). Run AFTER build-i80-wb.csx and joints-i80.csx (re-runnable):
//   sbm run samples\plans\abutments-i80.csx model.sbm
// Everything below is a printed plan value; derived values are computed from printed ones. Items the plans do not settle are NOT
// filled in with a rule: the generator raises a warning for each (printed at the end).
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

void Build(string bridgeName, bool first, string source, double leftEnd, double rightEnd, double footBottomLeft, double footBottomRight,
    double top0Off, double top0, double topCl, double top1Off, double top1, double[] stepsTop, double maskHeight,
    double extWidth, double extTop, double wingLen, double[] cfStart, double[] cfHeight,
    (double top, double topEnd, double bottom, double level, double[] footTops) wl, (double top, double topEnd, double bottom, double level, double[] footTops) wr,
    double leftGutter, double rightGutter, double hoopFirst, int hoopsPerPitch, int endHoops, int staples, int rowsPerFace, List<PlanBarLine> list)
{
    var b = Doc.Project.Bridges.First(x => x.Name == bridgeName);
    int si = first ? 0 : b.Supports.Count - 1;
    var sup = b.Supports[si];
    var sl = Doc.Framing(bridgeName).SupportLine(si);
    var al = Doc.Alignment(b.AlignmentName);
    double xc = (-(F(1, 10) + F(1.5) + In(1.5)) + F(3, 3)) / 2;        // footing mid-width x from the CL bearing (toward the span)
    double nc = (first ? 1 : -1) * xc;
    var ad = new AbutmentDefinition { Source = source, LeftEndOffset = leftEnd, RightEndOffset = rightEnd, PlanFootingWidth = F(6, 8.5), BackwallFrontToBearing = In(22) };
    ad.FootingBottom.Add(new CapProfilePoint(SOf(al, sl, leftEnd, nc), Ft(footBottomLeft)));
    if (Math.Abs(footBottomRight - footBottomLeft) > 1e-9) ad.FootingBottom.Add(new CapProfilePoint(SOf(al, sl, rightEnd, nc), Ft(footBottomRight)));
    ad.BackwallTop.Add(new CapProfilePoint(SOf(al, sl, top0Off), Ft(top0)));
    ad.BackwallTop.Add(new CapProfilePoint(SOf(al, sl, 0), Ft(topCl)));
    ad.BackwallTop.Add(new CapProfilePoint(SOf(al, sl, top1Off), Ft(top1)));
    for (int i = 0; i < stepsTop.Length; i++) if (!double.IsNaN(stepsTop[i])) ad.Steps.Add(new AbutmentStepDefinition(i + 1, Ft(stepsTop[i])));
    ad.Masks.Add(new MaskwallDefinition { End = AbutmentEnd.Left, Height = maskHeight, Thickness = In(10) });          // Setback 0: the panel is the OUTERMOST 10" of the zone, flush with the footing end line (p85/p89 plan, abutment-followups.md 5.1)
    ad.Extension = new BackwallExtensionDefinition { End = AbutmentEnd.Right, Width = extWidth, TopElevation = Ft(extTop) };
    // corbel / paving notch only between the wing inner faces (rear elevation p85 / p89: the ledge and taper lines stop at the wings; the top line and the
    // c.j. continue behind the wings to the maskwall / extension) - the backwall itself spans maskwall to extension
    ad.CorbelLeftOffset = leftGutter; ad.CorbelRightOffset = rightGutter;
    ad.PileBackRowToBearing = F(1, 11.5); ad.PileFrontRowToBearing = F(1, 9); ad.PileTopAboveFootingBottom = F(2);
    ad.PileLength = F(first ? 125 : 115);   // contract lengths: West 125 ft, East 115 ft (PDF 90; substructure-elevation-check.md)
    foreach (var (end, gut, w) in new[] { (AbutmentEnd.Left, leftGutter, wl), (AbutmentEnd.Right, rightGutter, wr) })
    {
        var wd = new WingwallDefinition
        {
            Name = end == AbutmentEnd.Left ? "A" : (first ? "F" : "J"), End = end, GutterOffset = gut, Thickness = F(1, 9), Length = wingLen,
            TopAtBackwall = Ft(w.top), TopAtEnd = Ft(w.topEnd), BottomAtBackwall = Ft(w.bottom), LevelReach = w.level, EndHeight = F(4),
        };
        for (int k = 0; k < cfStart.Length; k++)
            wd.Counterforts.Add(new CounterfortDefinition
            {
                Start = cfStart[k], Width = F(3), BodyWidth = F(1, 10), Projection = F(3, 6), Height = cfHeight[k], FootingTop = Ft(w.footTops[k]),
                HoopSpaces = k == 0 ? (first ? 9 : 8) : 5, VerticalBarsPerSide = 10, VerticalMark = k == 0 ? "6h2" : "6h3",
            });
        // 8j3 uplift anchors: the circled pile of each wing footing = the fill-side (inner) pile, footings No.1 and No.2 of every wing (p86/p90 legends, abutment-followups.md 2)
        wd.UpliftPiles.Add(new UpliftPileDefinition { Footing = 1, Pile = 2 }); wd.UpliftPiles.Add(new UpliftPileDefinition { Footing = 2, Pile = 2 });
        ad.Wings.Add(wd);
    }
    var rb = ad.Rebar;
    rb.FootingLongMark = "9a1"; rb.FootingRowBars = 7; rb.FootingLongPieces = 2; rb.FootingLongLap = F(9, 1);
    rb.HoopsPerPitch = hoopsPerPitch; rb.HoopFirstOffset = hoopFirst; rb.EndHoopsEach = endHoops; rb.EndStaplesEach = staples;
    rb.BackFaceBarLength = first ? F(13, 1) : F(10, 6); rb.FrontFaceBarLength = first ? F(12, 9) : F(10, 2);
    rb.BackwallLongRowsPerFace = rowsPerFace; rb.BackwallLongPieces = 2; rb.PlanBarList = list;
    if (first) rb.StepHoopGirders = new() { 3, 4 };           // 5n2 hoops at girders C and D only (printed)
    var sd = sup.ResolvedSubstructure.Clone();
    sd.Abutment = ad;
    sd.PileSection = "HP14x73"; sd.PileType = PileType.SteelHP;
    sd.BackwallGap = In(6);                                    // plans: 6" at 50 F between the deck end and the backwall face (1'-4 + 6" = 1'-10)
    sd.Reinforcement.EpoxyCoated = true;
    sup.Substructure = sd;
}

void Piles(string bridgeName, bool first, double[] fromClCrossing, double[] fromRightEnd)
{
    var b = Doc.Project.Bridges.First(x => x.Name == bridgeName);
    int si = first ? 0 : b.Supports.Count - 1;
    var ad = b.Supports[si].Substructure!.Abutment!;
    var sl = Doc.Framing(bridgeName).SupportLine(si); var al = Doc.Alignment(b.AlignmentName);
    double xc = (-(F(1, 10) + F(1.5) + In(1.5)) + F(3, 3)) / 2, nc = (first ? 1 : -1) * xc;
    ad.PileStations.Clear();
    if (fromRightEnd != null) { double sR = SOf(al, sl, ad.RightEndOffset, nc); foreach (double d in fromRightEnd) ad.PileStations.Add(sR - Ft(d)); }
    else { double s0 = SOf(al, sl, 0, nc); foreach (double d in fromClCrossing) ad.PileStations.Add(s0 + Ft(d)); }
}

var westList = new List<PlanBarLine>
{
    B("9a1", 40, F(48, 4), 6573), B("5b1", 108, F(20, 7), 2319), B("5b2", 10, F(6, 3), 168, true), B("5b3", 5, F(11, 7), 60), B("5b4", 5, F(19), 99),
    B("6d1", 173, F(13, 1), 3400), B("5d2", 88, F(12, 9), 1170), B("5d3", 83, F(4, 5), 382).Shaped("U", In(2.5), In(12), In(29), In(12)), B("5d4", 83, F(3, 8), 317), B("4d6", 86, F(8, 10), 507).Shaped("U", In(2), In(46), In(14), In(46)),
    B("5d7", 11, F(14, 5), 165), B("4d8", 6, F(3, 2), 13).Shaped("U", In(2), In(12), In(14), In(12)), B("5e1", 52, F(13, 1), 456, true), B("5e2", 21, F(15, 6), 339), B("5e3", 30, F(10), 313),
    B("5e4", 54, F(12, 10), 467, true), B("6f1", 8, F(25, 8), 308), B("6f2", 16, F(24, 8), 358, true), B("6f3", 2, F(26, 11), 81), B("6f4", 8, F(6), 72),
    B("6f5", 8, F(27), 324), B("6f6", 16, F(25, 11), 371, true), B("6f7", 2, F(28, 5), 85), B("6g1", 44, F(45, 1), 2979), B("5g2", 22, F(4, 7), 105),
    B("5g3", 4, F(42, 7), 178), B("6g4", 44, F(6, 6), 430).Shaped("L", In(4.5), In(12), In(66)), B("6g5", 32, F(8), 385), B("5h1", 24, F(4), 100), B("6h2", 20, F(11, 5), 343),
    B("6h3", 20, F(7, 11), 238), B("5h4", 32, F(12), 401).Shaped("HAT", In(2.5), In(6), In(57), In(18), In(57), In(6)), B("5j1", 32, F(11, 6), 384).Shaped("U", In(3.75), In(29), In(80), In(29)), B("5j2", 32, F(11, 6), 384).Shaped("HOOP", In(2.5), In(32), In(31), In(6), In(6)), B("8j3", 8, F(5), 107).Shaped("V", In(6), In(30), In(32), In(25.5)),
    B("5m1", 24, F(8, 2), 204).Shaped("U", In(2.5), In(27), In(44), In(27)), B("5n1", 24, F(4, 2), 104), B("5n2", 2, F(16, 10), 35).Shaped("HOOP", In(2.5), In(50), In(45), In(6), In(6)), B("5d5", 42, F(3, 6), 153),
};
// East: counts and lengths per row as printed (p92); the weights of the rows were not re-read (only the totals: epoxy 21,309 lb, stainless 142 lb) -> 0.
var eastList = new List<PlanBarLine>
{
    B("9a1", 40, F(45, 11), 6245), B("5b1", 110, F(20, 7), 2362), B("5b2", 6, F(9, 3), 58), B("6d1", 162, F(10, 6), 0), B("5d2", 82, F(10, 2), 0),
    B("5d3", 79, F(4, 5), 0).Shaped("U", In(2.5), In(12), In(29), In(12)), B("5d4", 79, F(3, 8), 0), B("4d6", 80, F(8, 10), 0).Shaped("U", In(2), In(46), In(14), In(46)), B("5d7", 8, F(12, 2), 0), B("4d8", 4, F(3, 2), 0).Shaped("U", In(2), In(12), In(14), In(12)),
    B("5e1", 96, F(11, 7), 768, true), B("5e2", 15, F(13, 8), 0), B("5e3", 26, F(6, 4), 0), B("6f1", 16, F(23, 3), 0), B("6f2", 28, F(22, 1), 0, true),
    B("6f3", 4, F(24, 5), 0), B("6f4", 12, F(5, 3), 0), B("6g1", 36, F(42, 5), 0), B("5g2", 18, F(6, 1), 0).Shaped("L", In(3.75), In(10), In(63)), B("5g3", 4, F(40, 1), 0),
    B("6g4", 36, F(5, 5), 0).Shaped("L", In(4.5), In(12), In(53)), B("6g5", 28, F(6, 5), 0), B("5h1", 24, F(4), 0), B("6h2", 20, F(11, 3), 0), B("6h3", 20, F(7, 11), 0),
    B("5h4", 30, F(12), 0).Shaped("HAT", In(2.5), In(6), In(57), In(18), In(57), In(6)), B("5j1", 32, F(11, 6), 0).Shaped("U", In(3.75), In(29), In(80), In(29)), B("5j2", 32, F(11, 6), 0).Shaped("HOOP", In(2.5), In(32), In(31), In(6), In(6)), B("8j3", 8, F(5), 0).Shaped("V", In(6), In(30), In(32), In(25.5)), B("5m1", 32, F(7, 9), 0).Shaped("U", In(2.5), In(24), In(45), In(24)), B("5n1", 32, F(4, 2), 0),
    B("5d5", 39, F(3, 6), 142),
};

// ---- West abutment: Unit 1, first support. Gutter lines 24'-0 (A) / 36'-0 (F) from the approach CL; footing ends 26'-11 / 37'-9.
Build("I-80 WB Unit 1", true, "Iowa DOT Design 116 West abutment, sheets 85-88",
    -F(26, 11), F(37, 9), 992.82, 992.82, -F(24), 1007.32, 1008.15, F(36), 1007.65,
    new[] { 997.15, 997.51, 997.87, 997.78, 997.62, 997.45 }, F(14, 2),
    ((F(2, 9.375) + F(3, 9.875)) / 2) / Math.Cos(44 * Math.PI / 180), 1007.65, F(31, 9), new[] { F(14, 3), F(24, 3) }, new[] { F(8, 10), F(5, 4) },
    (1007.32, 1006.97, 993.82, F(1, 6), new[] { 996.68, 1000.27 }), (1007.65, 1007.30, 994.17, F(1, 6), new[] { 997.41, 1000.78 }),
    -F(24), F(36), In(9), 9, 0, 5, 11, westList);
// Footing plan outline of the West abutment (p86 piling layout; abutment-followups.md 4): the end lines (offsets -26'-11 / +37'-9) end in horizontal notches = lines of constant
// station. y = distance along the approach CL (up-station +) from the crossing of the footing centre line with the CL approach roadway.
{
    var b1 = Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 1");
    var ad1 = b1.Supports[0].Substructure!.Abutment!;
    var sl1 = Doc.Framing("I-80 WB Unit 1").SupportLine(0); var al1 = Doc.Alignment(b1.AlignmentName);
    double xc1 = (-(F(1, 10) + F(1.5) + In(1.5)) + F(3, 3)) / 2;
    var t1 = Vec2.FromAngle(al1.AngleAt(sl1.Station)); var n1 = new Vec2(-sl1.Direction.Y, sl1.Direction.X); if (Vec2.Dot(n1, t1) < 0) n1 = -n1;
    double s1c = SOf(al1, sl1, 0, xc1);
    double staC = al1.Inverse(sl1.AlignmentPoint + sl1.Direction * s1c + n1 * xc1).Station;
    ad1.FootingNotches.Clear();
    ad1.FootingNotches.Add(new FootingNotchDefinition { End = AbutmentEnd.Left, FrontStation = staC + Ft(-18.514), BackStation = staC + Ft(-28.921) });
    ad1.FootingNotches.Add(new FootingNotchDefinition { End = AbutmentEnd.Right, FrontStation = staC + Ft(39.427) });
}
// Printed backwall vertical strings (6d1 at 6", 5d2 at 1'-0): West 171 / 86 spaces, East 159 / 79 spaces (the bar lists add 1 / 2 corner bars that the strings do not show)
Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 1").Supports[0].Substructure!.Abutment!.Rebar.BackFaceCount = 172;
Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 1").Supports[0].Substructure!.Abutment!.Rebar.FrontFaceCount = 87;
Piles("I-80 WB Unit 1", true, new[] { -33.568, -26.734, -19.901, -13.068, -6.234, 0.599, 7.432, 14.266, 21.099, 27.932, 34.766, 41.599, 48.432 }, null);

// ---- East abutment: Unit 3, last support. Gutter lines 36'-0 (J, right) / 42'-0 (A, left); footing ends 36+1'-9 and 42+2'-11 (the printed
// 37'-11 / 45'-1 1/2 are along the line = offset / cos 5.45); footing bottom 1003.44 (A end) to 1008.27 (J end).
// East backwall extension: printed 3'-3 1/2 from the cap to the footing end line (p89), cap perpendicular to the strip.
Build("I-80 WB Unit 3", false, "Iowa DOT Design 116 East abutment, sheets 89-92",
    -(F(42) + F(2, 11)), F(36) + F(1, 9), 1003.44, 1008.27, -F(42), 1016.24, 1018.71, F(36), 1020.80,
    new[] { 1008.20, double.NaN, double.NaN, 1008.54, 1009.22, 1009.89, 1010.57, 1011.23, 1011.91, 1012.58 }, F(12, 4),
    F(3, 3.5), 1021.09, F(27), new[] { F(9, 6), F(19, 6) }, new[] { F(8, 8), F(5, 4) },
    (1016.24, 1016.11, 1004.24, 0.0, new[] { 1006.29, 1009.61 }), (1020.80, 1020.67, 1008.80, 0.0, new[] { 1010.78, 1014.15 }),
    -F(42), F(36), In(8), 8, 3, 3, 9, eastList);
Piles("I-80 WB Unit 3", false, null,
    Enumerable.Range(0, 6).Select(i => 2.51 + 6.0 * i).Concat(Enumerable.Range(0, 8).Select(i => 38.51 + 6.0 * i)).ToArray());
{ var e3 = Doc.Project.Bridges.First(x => x.Name == "I-80 WB Unit 3"); var rb3 = e3.Supports[e3.Supports.Count - 1].Substructure!.Abutment!.Rebar; rb3.BackFaceCount = 160; rb3.FrontFaceCount = 80; }
Print("Extension: West axis length 4.59 ft = mean of the printed 2'-9 3/8 / cos44 and 3'-9 7/8 / cos44 (perpendicular cap); East printed 3'-3 1/2. East / South footing notches: not read (plain end lines).");

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var bn in new[] { "I-80 WB Unit 1", "I-80 WB Unit 3" })
{
    var m = Doc.Substructure(bn);
    foreach (var u in m.Units.Where(x => x.Abutment != null))
    {
        Print($"== {bn} / {u.Name}");
        foreach (var g in u.Solids.GroupBy(s => s.Kind)) Print($"  {g.Key}: {g.Count()} solids, {g.Sum(s => Math.Abs(s.Mesh.Volume())) / CY:F1} CY");
        double w = m.Marks.Where(x => x.Unit == u.Name).Sum(x => x.TotalMass) / 0.45359237;
        Print($"  rebar {w:F0} lb in {m.Marks.Count(x => x.Unit == u.Name)} marks");
    }
    foreach (var d in m.Diagnostics.Where(d => d.Message.Contains("Abut"))) Print($"  {d.Severity}: {d.Message}");
}
Doc.Save();
