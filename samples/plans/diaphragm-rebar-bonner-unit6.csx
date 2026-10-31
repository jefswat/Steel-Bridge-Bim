// NCDOT B-2500 Bonner Unit 6 - DIAPHRAGM BARS as deck plan bar sets (Element "diaphragm"). Needs the generated girders (run AFTER a regeneration):
// build-bonner-unit6.csx calls it after its first regeneration and regenerates again. Re-runnable.
//   sbm run samples\plans\diaphragm-rebar-bonner-unit6.csx "<model.sbm>"
// Plan data (p. = PDF page):
//   p.407 (S4-15) SECTION L-L (continuous bent diaphragms): "6-#4K302 (EA. FACE) @ 1'-0\" CTS. = 5'-0\"" in the stack "8 SPA. @ 1'-0\" = 8'-0\"" from the bottom
//          K310 row (on the 3" high bar bolster at the diaphragm bottom) up: K310, K309, 6 x K302, K304 (1'-0" each); "3\" CLR. TO 'S' & 'U' BARS"; diaphragm 2'-4".
//   p.455 bill: K302 #4 10'-8" x 252 (= 6 rows x 2 faces x 3 bays x 7 diaphragms); 10'-8" in the 11'-6" bays = 1 1/2" clear to the 7" webs.
// Placed: K302 (all 7 diaphragms), S305 / U309 (bent diaphragms) and H7 (temporary access openings, p.445). The other marks stay schedule only (flags give the reasons).
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var br = Doc.Project.Bridges.First(b => b.Name == "Bonner Unit 6");
var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
var plan = br.Deck.Reinforcement.Plan ?? throw new InvalidOperationException("run deck-rebar-bonner-unit6.csx first");
var ps = Doc.Prestressed("Bonner Unit 6");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double[] gOff = { -F(17, 3), -F(5, 9), F(5, 9), F(17, 3) };

var k302 = new PlanBarSet
{
    Mark = "K302", Size = "#4", Element = "diaphragm", ListedLength = F(10, 8), CheckCover = false, PlanCount = 252, PlanWeightLb = 1796,
    Location = "Bent diaphragm, horizontal each face (p.407 L-L)", Source = "p.407 Section L-L, p.406 Section A-A, p.455",
    Note = "Rows from the K310 bottom row on the 3\" bolster (bar centre 3 3/4\" above the diaphragm bottom = girder bottom line) + 2'-0\" .. 7'-0\"; faces 3\" clear + #4 stirrup.",
};
int bentsDone = 0;
for (int bent = 30; bent <= 34; bent++)
{
    var sup = br.Supports.First(s => s.Name == $"Bent {bent}");
    int j = br.Supports.IndexOf(sup);
    // girder bottom at the bent control line: mean of the back (span j-1, CL brg at its end) and ahead (span j, start) beams, carried to x = 0 along their slope
    double[] zb = new double[4];
    for (int g = 0; g < 4; g++)
    {
        var back = ps.Beams.First(b => b.Span == j - 1 && b.Girder == g);
        var ahead = ps.Beams.First(b => b.Span == j && b.Girder == g);
        double xb = sup.Station - al.Inverse(back.BearingEnd.XY).Station, xa = al.Inverse(ahead.BearingStart.XY).Station - sup.Station;
        double zB = back.BearingEnd.Z - back.Shape.Depth + back.Slope * xb, zA = ahead.BearingStart.Z - ahead.Shape.Depth - ahead.Slope * xa;
        zb[g] = (zB + zA) / 2;
    }
    for (int bay = 0; bay < 3; bay++)
    {
        double o0 = gOff[bay] + In(3.5 + 1.5), o1 = gOff[bay + 1] - In(3.5 + 1.5);              // 1 1/2" clear to the 7" webs
        double Z(double o) => zb[bay] + (zb[bay + 1] - zb[bay]) * (o - gOff[bay]) / (gOff[bay + 1] - gOff[bay]);
        foreach (double x in new[] { -In(14 - 3 - 0.5 - 0.25), In(14 - 3 - 0.5 - 0.25) })
            for (int row = 0; row < 6; row++)
            {
                double dz = In(3.75 + 24 + 12 * row);
                k302.Add(new[] { al.PointAt(sup.Station + x, o0).WithZ(Z(o0) + dz), al.PointAt(sup.Station + x, o1).WithZ(Z(o1) + dz) });
            }
    }
    bentsDone++;
}
// girder bottom line at a support (x = 0 on the support line): beams of the span(s) at it, carried along their slope
double[] Bottoms(int j)
{
    var sup = br.Supports[j];
    var z = new double[4];
    for (int g = 0; g < 4; g++)
    {
        var v = new List<double>();
        var back = ps.Beams.FirstOrDefault(b => b.Span == j - 1 && b.Girder == g);
        var ahead = ps.Beams.FirstOrDefault(b => b.Span == j && b.Girder == g);
        if (back != null) v.Add(back.BearingEnd.Z - back.Shape.Depth + back.Slope * (sup.Station - al.Inverse(back.BearingEnd.XY).Station));
        if (ahead != null) v.Add(ahead.BearingStart.Z - ahead.Shape.Depth - ahead.Slope * (al.Inverse(ahead.BearingStart.XY).Station - sup.Station));
        z[g] = v.Average();
    }
    return z;
}
// slope of the girder bottom along the stations (to carry the x = 0 value to the diaphragm faces)
double grade = ps.Beams[0].Slope;
// ---- K302 at the expansion joint diaphragms (p.407 K-K / p.408 N-N): 6 rows @ 1'-0", the lowest 1'-3" above the girder bottom line (the diaphragm bottom,
// K301 on 2" bolsters); faces = the diaphragm faces (girder end face and 1'-10" (MIN.) from it) inside the #5 S301 (3" clear)
foreach (var (j, x0, x1) in new[] { (0, In(27), In(49)), (br.Supports.Count - 1, -In(27), -In(5)) })
{
    var sup = br.Supports[j];
    var zb = Bottoms(j);
    for (int bay = 0; bay < 3; bay++)
    {
        double o0 = gOff[bay] + In(5), o1 = gOff[bay + 1] - In(5);
        double Z(double o, double x) => zb[bay] + (zb[bay + 1] - zb[bay]) * (o - gOff[bay]) / (gOff[bay + 1] - gOff[bay]) + grade * x;
        foreach (double x in new[] { Math.Min(x0, x1) + In(3 + 0.625 + 0.25), Math.Max(x0, x1) - In(3 + 0.625 + 0.25) })
            for (int row = 0; row < 6; row++)
            {
                double dz = In(15 + 12 * row);
                k302.Add(new[] { al.PointAt(sup.Station + x, o0).WithZ(Z(o0, x) + dz), al.PointAt(sup.Station + x, o1).WithZ(Z(o1, x) + dz) });
            }
    }
}
k302.Note += " Expansion joint diaphragms: lowest row 1'-3\" above the girder bottom line (p.407 K-K / p.408 N-N), faces 3\" clear + #5 S301.";

// ---- S305 / U309 in the bent diaphragms (p.406 Section A-A + typical section, p.407 L-L): per bay from CL girder 8 1/2" + 2 spa @ 11" + "8-#4S305 EA. FACE &
// 8-#4U309 @ 11\" CTS. = 6'-5\"" + 2 spa @ 11" + 8 1/2" (= 11'-6"); overhangs 8 1/2" + 3 eq. spa = 2'-7" + 4" from CL girder 1 / 4 with "#4S305 E.F." and
// "#4U309" at two of them (the middle two, read from the drawing - dimension-only). U309 (type 2): U 1'-10" x 3'-10" legs, bottom on the 3" bolster;
// S305 (type 1): 7'-1" vertical + 8" leg, each face, top leg into the slab under the top mat (top position not dimensioned - dimension-only).
var s305 = new PlanBarSet { Mark = "S305", Size = "#4", Element = "diaphragm", Shape = "1", Dims = new[] { In(8), In(85) }, ListedLength = F(7, 9), CheckCover = false, PlanCount = 280, PlanWeightLb = 1450,
    Location = "Bent diaphragm verticals each face (p.406 A-A, p.407 L-L)", Source = "p.406, p.407, p.455",
    Unresolved = U("p.406 / p.407", "S305 top and the overhang S305 / U309 positions", "the S305 top leg is drawn into the slab without a dimension and the overhang chain (3 eq. spa.) does not say which spaces take S305 / U309", "S305 8\" leg 3 1/2\" + #5 + #6 below the deck top (under the top mat), overhang bars at the two middle points.") };
var u309 = new PlanBarSet { Mark = "U309", Size = "#4", Element = "diaphragm", Shape = "2", Dims = new[] { In(46), In(22), In(46) }, ListedLength = F(9, 6), CheckCover = false, PlanCount = 140, PlanWeightLb = 888,
    Location = "Bent diaphragm bottom U (p.406 A-A, p.407 L-L)", Source = "p.406, p.407, p.455" };
double xs = In(14 - 3 - 0.25);                                 // stirrup leg centre: 3" clear to the diaphragm faces
for (int bent = 30; bent <= 34; bent++)
{
    var sup = br.Supports.First(s => s.Name == $"Bent {bent}");
    int j = br.Supports.IndexOf(sup);
    var zb = Bottoms(j);
    double Zat(double o)
    {
        if (o <= gOff[0]) return zb[0];
        if (o >= gOff[3]) return zb[3];
        int k = o < gOff[1] ? 0 : o < gOff[2] ? 1 : 2;
        return zb[k] + (zb[k + 1] - zb[k]) * (o - gOff[k]) / (gOff[k + 1] - gOff[k]);
    }
    var offs = new List<double>();
    for (int bay = 0; bay < 3; bay++) for (int k = 0; k < 8; k++) offs.Add(gOff[bay] + In(8.5 + 22 + 11 * k));
    foreach (double sd in new[] { -1.0, 1 }) foreach (double d in new[] { 8.5 + 31.0 / 3, 8.5 + 62.0 / 3 }) offs.Add(sd < 0 ? gOff[0] - In(d) : gOff[3] + In(d));
    foreach (double o in offs)
    {
        double zb0 = Zat(o) + In(3 + 0.25);
        u309.Add(new[] { al.PointAt(sup.Station - xs, o).WithZ(zb0 + In(46 - 0.5)), al.PointAt(sup.Station - xs, o).WithZ(zb0), al.PointAt(sup.Station + xs, o).WithZ(zb0), al.PointAt(sup.Station + xs, o).WithZ(zb0 + In(46 - 0.5)) });
        double zTop = br.Deck.FinishedElevation(al, sup.Station, o) - In(3.5 + 0.625 + 0.75);
        foreach (double sx in new[] { -1.0, 1 })
        {
            double x = sx * (xs - In(0.5));
            s305.Add(new[] { al.PointAt(sup.Station + x, o).WithZ(zTop - In(85 - 0.5)), al.PointAt(sup.Station + x, o).WithZ(zTop), al.PointAt(sup.Station + x - sx * In(8), o).WithZ(zTop) });
        }
    }
}

// ---- H7 at the two temporary access openings (p.445 TEMPORARY ACCESS DETAIL, p.409 STA 93+35.00 / 93+71.00 -L-, 0.17' RT; 3'-2" top / 3'-0" bottom):
// the openings are TEMPORARY (repaired with MAP concrete, p.445 notes) - the final deck is solid, so no hole is cut. H7 #5 2'-10" inside each opening:
// top "6-#5H7 @ 6\" (MAX CTS)" across (A-bar direction), bottom "7-#5H7 @ 5 1/4\" (MAX CTS)" across + "4-#5H7 @ 8 1/2\"" along (B direction); 2 x 17 = 34 = bill.
var h7 = new PlanBarSet { Mark = "H7", Size = "#5", Element = "deck", ListedLength = F(2, 10), PlanCount = 34, PlanWeightLb = 101,
    Location = "Temporary access opening repair, inside the opening (p.445)", Source = "p.445, p.409",
    Unresolved = U("p.445", "H7 positions in the temporary access openings", "the groups are given at maximum spacings ('@ 6\" (MAX CTS)', '@ 5 1/4\"', '@ 8 1/2\"') without a first-bar dimension", "Groups centred in the opening (dimension-only).") };
double tTop = In(3 + 0.3125), tBot = In(2.5 + 0.3125);
foreach (string staTxt in new[] { "93+35.00", "93+71.00" })
{
    double st = Sta(staTxt), oc = Ft(0.17);
    double ZT(double s0, double o) => br.Deck.FinishedElevation(al, s0, o) - tTop;
    double ZB(double s0, double o) => br.Deck.FinishedElevation(al, s0, o) - br.Deck.Thickness + tBot;
    for (int k = 0; k < 6; k++) { double s0 = st + In(-15 + 6 * k); h7.Add(new[] { al.PointAt(s0, oc - In(17)).WithZ(ZT(s0, oc - In(17))), al.PointAt(s0, oc + In(17)).WithZ(ZT(s0, oc + In(17))) }); }
    for (int k = 0; k < 7; k++) { double s0 = st + In(-15.75 + 5.25 * k); h7.Add(new[] { al.PointAt(s0, oc - In(17)).WithZ(ZB(s0, oc - In(17))), al.PointAt(s0, oc + In(17)).WithZ(ZB(s0, oc + In(17))) }); }
    for (int k = 0; k < 4; k++) { double o = oc + In(-12.75 + 8.5 * k); h7.Add(new[] { al.PointAt(st - In(17), o).WithZ(ZB(st - In(17), o) + In(0.625)), al.PointAt(st + In(17), o).WithZ(ZB(st + In(17), o) + In(0.625)) }); }
}

// ---- U301-U306 in the sloped ends of the expansion joint diaphragms (p.405: "#4U301 THRU #4U306 @ 1'-0\" CTS. EA. FACE", U301 1'-0" above the lower end of
// the sloped face, the top U306 10" above the one below): type 2 U lying flat, 1'-4" out-to-out across the 1'-10" diaphragm (3" clear), closed end 3" clear
// inside the sloped face, legs inward U301 1'-1", U302 1'-5", U303 1'-9", U304 2'-0", U305 2'-3", U306 2'-7" (x2); per sloped end 7 bars (bill: 4 x U301-U305, 8 x U306).
var uSets = new Dictionary<string, PlanBarSet>();
var uLeg = new Dictionary<string, double> { ["U301"] = 13, ["U302"] = 17, ["U303"] = 21, ["U304"] = 24, ["U305"] = 27, ["U306"] = 31 };
var uW = new Dictionary<string, int> { ["U301"] = 9, ["U302"] = 11, ["U303"] = 13, ["U304"] = 14, ["U305"] = 16, ["U306"] = 35 };
string uFlag = U("p.405", "U301-U306 heights in the sloped ends", "U301 is dimensioned 1'-0\" above the lower end of the sloped face, which is drawn about 6\" above the girder bottom (not dimensioned)",
    "Lower end taken 6\" above the girder bottom line (scaled); bars @ 1'-0\" + 10\" above.");
PlanBarSet USet(string m) => uSets.TryGetValue(m, out var x) ? x : uSets[m] = new PlanBarSet
{
    Mark = m, Size = "#4", Element = "diaphragm", Shape = "2", Dims = new[] { In(uLeg[m]), In(16), In(uLeg[m]) }, ListedLength = In(2 * uLeg[m] + 16), CheckCover = false,
    PlanCount = m == "U306" ? 8 : 4, PlanWeightLb = uW[m], Location = "Expansion joint diaphragm, sloped end (p.405)", Source = "p.405, p.455", Unresolved = uFlag,
};
double oBot = F(17, 3) + In(24), oTop = F(21, 3.5);
foreach (var (j, xm) in new[] { (0, In(38)), (br.Supports.Count - 1, -In(16)) })
{
    var sup = br.Supports[j];
    var zb = Bottoms(j);
    foreach (int side in new[] { -1, 1 })
    {
        int g = side < 0 ? 0 : 3;
        double zBot = zb[g] + grade * xm;
        double zSof = br.Deck.FinishedElevation(al, sup.Station + xm, side * oTop) - br.Deck.Thickness;
        double FaceOff(double z) => side * (oBot + (oTop - oBot) * (z - zBot) / (zSof - zBot));
        var marks = new[] { "U301", "U302", "U303", "U304", "U305", "U306", "U306" };
        for (int k = 0; k < 7; k++)
        {
            string m = marks[k];
            double z = zBot + In(6 + 12 * k - (k == 6 ? 2 : 0));
            double oClosed = FaceOff(z) - side * In(3 + 0.25);
            double oTip = oClosed - side * In(uLeg[m] - 0.25);
            double xa = xm - In(8 - 0.25), xb = xm + In(8 - 0.25);
            USet(m).Add(new[] { al.PointAt(sup.Station + xa, oTip).WithZ(z), al.PointAt(sup.Station + xa, oClosed).WithZ(z), al.PointAt(sup.Station + xb, oClosed).WithZ(z), al.PointAt(sup.Station + xb, oTip).WithZ(z) });
        }
    }
}

var sets = br.Deck.Reinforcement.PlanBarSets;
sets.RemoveAll(x => x.Element == "diaphragm" || x.Mark == "H7");
sets.AddRange(uSets.Values);
plan.PlanBarList.RemoveAll(l => uSets.ContainsKey(l.Mark));
sets.Add(k302); sets.Add(s305); sets.Add(u309); sets.Add(h7);
plan.PlanBarList.RemoveAll(l => l.Mark is "K302" or "S305" or "U309" or "H7");
plan.Unresolved.RemoveAll(u => u.Contains("diaphragm bars (E, G, K, S, U)"));
plan.Unresolved.Add(U("p.405", "diaphragm bars E301-E304 in the sloped ends", "they are drawn as sloped lines inside the wedge ('3\" CLR. MIN.' to the face only); their four lengths (5'-9\" to 8'-2\") do not tell which line each one is on or where it starts",
    "Schedule only."));
plan.Unresolved.Add(U("p.407 / p.408", "G302 (modular joint support boxes) and G301 (Transflex blockout)", "G302 sits '@ ea. support box' of the supplier-designed modular joint (box positions not in the plans); G301 '4-#5G301 @ 10\" CTS. (TYP. EA. GIRDER)' has no dimension to the girder or to the blockout",
    "Schedule only."));
plan.Unresolved.Add(U("p.405-408", "K301, K303-K307, K309 / K310, S301-S304, S307-S311, U308, U310 / U311", "drawn in Sections K-K, L-L, M-M, N-N and the typical sections, but the runs (K305 / K306 / K307 two-bar runs through the 1 1/2\" web holes: 32 K307 per bent diaphragm do not resolve into the drawn 16), the S309 tie pattern (82 per bay) and the end diaphragm stirrup chains are not dimensioned enough to place without guessing",
    "Schedule only at the bill counts and lengths."));
plan.Unresolved.Add(U("p.445", "access opening bars H1-H6", "H4 / H6 are mechanically coupled to the H7 outside the opening and H1 / H2 / H3 / H5 are drawn around it, but only H7 has a length and a group inside the opening that fixes it",
    "Schedule only; the openings are temporary (filled with MAP concrete), no hole is cut in the deck."));
plan.Unresolved.RemoveAll(u => u.StartsWith("UNRESOLVED (p.405-408, p.445)"));
Print($"Bonner Unit 6 diaphragm bars: K302 {k302.Bars.Count} (252), S305 {s305.Bars.Count} (280), U309 {u309.Bars.Count} (140); access opening H7 {h7.Bars.Count} (34)");
