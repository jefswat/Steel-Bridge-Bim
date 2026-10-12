// MnDOT Br. 02069 - ABUTMENT reinforcing (South p6 / p7 / p8 = A marks, North p12 / p13 / p14 = B marks) placed as the plan sheets dimension it, as
// PlanBarSet data (BridgeDefinition.PlanBars, support frame: W = S along the abutment = alignment offset (skew 0), U = x from CL brg toward the span,
// Z absolute). Called by build-br02069.csx before regeneration (re-runnable: replaces the abutment sets / bill lines).
//   sbm run samples\plans\rebar-abutments-br02069.csx "samples\plans\Br02069 TH65.sbm"
// Bill of reinforcement: docs/plan-data/br02069-th65/bar-lists.csv (p8 / p14); bend diagrams bend-diagrams.csv ("ALL BENT BAR DIMENSIONS ARE GIVEN
// OUT-TO-OUT"; the centerline is placed db/2 inside the out-to-out outline). Plan precision 1/16".
// Section (p11 A-A, p8 / p14 A-A, B-B): footing 6'-0" x 3'-0" (x -3'-3" .. +2'-9", bottom 917.75); stem x -2'-6" .. +1'-6"; back ledge (x -2'-6" .. -1'-0")
// at footing top + 1'-7 1/4" S / 1'-4 3/4" N; seat at the riser = ledge + 1'-0", seat front 923.27 S / 923.06 N; infill wall x -2'-6" .. -1'-0" on the ledge,
// WL +17'-7" .. +36'-0", top 927.73 / 927.80; W mask wall beside the footing (S -23'-7" .. -25'-0 1/2"), 917.75 .. 930.61 / 930.67.
// Read from the sheets (dimensioned unless marked DERIVED):
//   x01 #7 footing longit.: 9 top + 9 bottom at 6" + 8 sps @ 7 1/2" (A-A), inside the stirrups; 2 side bars at mid height inside the stirrup sides
//        (A-A dots; 20 = bill); 59'-1" = 59'-7" - 2 x 3" end clear.
//   x02 #5 closed stirrup 1'-9" x 5'-6" o-o: 3" clr top / sides, bottom on the pile tops (A-A "0" CLR", 1'-0" pile embedment); x03 #5 U (base 3'-8",
//        legs to the hook tops 3'-3" S / 3'-0" N) with 2" clr to the stem faces; base on the bottom longitudinal bars (DERIVED: the drawn base rests on
//        that row; hook tops then 2 3/4" S / 3 1/4" N under the ledge, i.e. under x06). 60 + 60 at 3 1/2" + 59 sps @ 1'-0" (p6 / p12 footing plan).
//        135 / 180-deg hook returns are not dimensioned (bend-diagrams.csv): not modeled (the bill length is scheduled).
//   x06 #5 inverted U (base 3'-8" at 2" clr under the ledge, legs 10") and x07 / x08M #5 inverted U in the seat (base 2'-2", legs 2'-1" S / 1'-11" N,
//        "PULL UP SEAT TIE TO 3" CLEAR" at the seat front, note 1) at every x03 station: x08M in the E 19 stations (infill zone), x07 the other 41 (p7 / p13).
//   Stem horizontals (A-A / B-B "1 SP. @ 11 1/2"" S / "@ 9"" N, lower row 3" above the footing top; 3" from the faces = 2" clr + #5 leg + #6/2):
//        x09 #6 59'-3": 2 F.F. rows + 1 at the riser face (upper row); x10 (30'-0", W) / x11 (29'-1", E) #6 B.F. 2 rows, interrupted at the contraction
//        joint 29'-4" (S) / 30'-10" (N) from the E end (1" each side, 2" end clear - DERIVED from the bill lengths, they add up exactly);
//        x12 #6 40'-0" (W) / x13M #6 22'-4" (E) at the seat top (under the x07 base) at the riser and front faces, lap 3'-1" (p7 "3'-1" MIN. LAP").
//   Infill wall (B-B): x17 #5 18'-1" 6 rows E.F. (4" from the top, 4" above the ledge, 5 equal spaces), inside the dowels; x05 #5 B.F. dowel L
//        (6'-7" + 10" foot toward the front, top under the x16 tie), x15 #5 F.F. dowel 6'-7" straight, x16 #5 U tie (base 1'-2" at 2" clr, legs 10"),
//        all at the 19 E stations ("SPA. W/ A506E").
//   Mask wall (p8 / p14 C-C, D-D, mask wall elevation; p11 C-C / D-D): x18 #5 12'-1" vertical E.F. at 8 positions from the front end 3 1/2" + 6 sps @ abt.
//        9" + 1'-0" (+ 7 1/2" to the back end), 2" clr, top at 2" clr; x21 #5 5'-8" horizontal E.F. 13 rows (3 1/2" above the 5" toe notch, 12 sps @ abt.
//        1'-0" = 11'-10 7/8", 3" under the top), inside the verticals, 2" from the front end; x24 #5 U ties (base 1'-0") - 8 caps over the vertical pairs,
//        13 at the horizontal rows' front ends; lug bars x19 (vertical, 4" step corner), x22 (horizontal, over the toe notch), x20 / x23 lug ties:
//        positions DERIVED from C-C / D-D (only the counts / spacing-with notes are dimensioned).
//   Footing / stem ties into the mask wall: x04 #5 L (4'-8" = 3'-4 1/2" embed + 1'-3 1/2" projection, 3'-5" leg up in the mask wall): 3 sets of 2 at the
//        footing longitudinal rows + 2 sets of 2 at the stem rows; x14 #5 L (4'-8" + 2'-0") 1 set of 2 at the seat-top bars; tied alongside those bars
//        (DERIVED: contact, one bar diameter sum inward).
//   Pedestals (p6 / p12 details, E-E / F-F): S Type A x25 #5 U (base 1'-10" across, legs 2'-7") 8 at 3 3/8" + 7" + 5 sps abt. 7" + 7" + 3 3/8";
//        x26 #5 U (base 4'-2" along, legs 2'-7") 5 at 3 5/8" + 5" + 7 1/4" + 2 @ 4 1/4" + 3 5/8" from the back face, pulled up to 2" clr (x25 under it);
//        Type B (future beams) x27M / x28M the same with legs 2'-2", 3" clr, x28M at 4" + 4 1/2" + 7" + 2 @ 4 1/4" + 4"; N Type C / D: 6 x25 / x27M at
//        4 3/8" + 5 sps abt. 6 5/8" + 4 3/8", 4 x26 / x28M at 4 3/8" + 3 sps abt. 6 3/8" + 4 3/8"; x29 #5 U ties (base 2'-0", legs 3'-5" S / 2'-10" N),
//        2 levels of 2 lapped U's, at 4" under the top and 4" above the seat, only at pedestals 9" or taller (note 1: G4 / G5 at both abutments).
using System.IO;
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Br02069 TH65 SB");
br.PlanBars ??= new PlanBarsDefinition();
var pbd = br.PlanBars;
pbd.Sets.RemoveAll(s => !s.Superstructure);
pbd.BarList.RemoveAll(l => !l.Superstructure);

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "br02069-th65");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/br02069-th65 not found");
}
string dir = DataDir();
List<string[]> Csv(string name)
{
    var res = new List<string[]>();
    foreach (var ln in File.ReadAllLines(Path.Combine(dir, name)).Skip(1))
    {
        if (ln.Trim().Length == 0) continue;
        var c = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
        foreach (char ch in ln) { if (ch == '"') q = !q; else if (ch == ',' && !q) { c.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); }
        c.Add(sb.ToString());
        res.Add(c.ToArray());
    }
    return res;
}
var bill = Csv("bar-lists.csv");
var seats = File.ReadAllLines(Path.Combine(dir, "seat-elevations.csv")).Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Split(',').Select(x => x.Trim()).ToArray()).ToList();

const PlanBarZRef EL = PlanBarZRef.Elevation;
double I(double inch) => In(inch);
PlanBarPoint Pt(double wFt, double uIn, double zFt) => new(Ft(wFt), I(uIn), EL, Ft(zFt));

foreach (int j in new[] { 0, 1 })
{
    bool S = j == 0;
    string P = S ? "A" : "B";
    string unitName = S ? "South Abutment" : "North Abutment";
    string pg = S ? "p6 / p7 / p8" : "p12 / p13 / p14";
    // ---- bill lines of this abutment (count / length / weight as printed)
    foreach (var r in bill.Where(r => r[0] == (S ? "South abutment" : "North abutment")))
        pbd.BarList.Add(new PlanBarListLine
        {
            Superstructure = false, Support = j, Mark = r[1], Size = "#" + r[2], Coating = r[3], Count = int.Parse(r[4]), Length = Ft(double.Parse(r[6], System.Globalization.CultureInfo.InvariantCulture)),
            WeightLb = double.Parse(r[8], System.Globalization.CultureInfo.InvariantCulture), Location = r[7],
        });
    double Listed(string mark) => Ft(double.Parse(bill.First(r => r[1] == mark)[6], System.Globalization.CultureInfo.InvariantCulture));
    string Coat(string mark) => bill.First(r => r[1] == mark)[3];
    string Loc(string mark) => bill.First(r => r[1] == mark)[7];
    PlanBarSet Set(string mark, string name, IEnumerable<PlanBarPoint> path, IEnumerable<double>? posFt = null, string shape = "", params double[] dimsIn)
    {
        var s = new PlanBarSet
        {
            Name = name, Mark = mark, Size = "#" + mark.Substring(1, 1), Coating = Coat(mark), Location = Loc(mark), Source = pg, Frame = PlanBarFrame.Support, Support = j,
            Superstructure = false, ListedLength = Listed(mark), Shape = shape, Cover = mark.Contains("701") || mark.Contains("502") ? I(3) : I(2),
        };
        s.Path.AddRange(path);
        if (posFt != null) s.Positions.AddRange(posFt.Select(Ft));
        s.Dims.AddRange(dimsIn.Select(I));
        pbd.Sets.Add(s);
        return s;
    }
    double r5 = 0.625 / 2, r6 = 0.75 / 2, r7 = 0.875 / 2, r4 = 0.5 / 2;
    // ---- levels (ft) and x (in)
    double FB = 917.75, FT = 920.75, ledge = FT + (S ? 19.25 : 16.75) / 12, seatBack = ledge + 1, seatFront = S ? 923.27 : 923.06;
    double wallTop = S ? 927.73 : 927.80, maskTop = S ? 930.61 : 930.67;
    double xFB = -39, xFF = 33, xSB = -30, xR = -12, xSF = 18;
    double wW = -(23 + 7.0 / 12), wE = 36;                 // footing / stem end lines (offsets)
    double z(double inchAboveFB) => FB + inchAboveFB / 12;
    var st = Enumerable.Range(0, 60).Select(k => wW + 3.5 / 12 + k).ToList();      // x02 / x03 / x06 stations: 3 1/2" + 59 sps @ 1'-0"
    var stW = st.Take(41).ToList(); var stE = st.Skip(41).ToList();                  // x07 (41) / x08M + infill bars (19, E zone)

    // ---- footing longitudinal x01 (#7): 9 top + 9 bottom + 2 sides
    double zTop = 33 - 0.625 - r7, zBot = 12 + 0.625 + r7, zMid = (zTop + zBot) / 2;
    double w0 = wW + 3.0 / 12, w1 = wE - 3.0 / 12;
    for (int k = 0; k < 9; k++)
    {
        double u = xFB + 6 + 7.5 * k;
        Set(P + "701E", $"top {k + 1}", new[] { Pt(w0, u, z(zTop)), Pt(w1, u, z(zTop)) }, shape: "Straight");
        Set(P + "701E", $"bottom {k + 1}", new[] { Pt(w0, u, z(zBot)), Pt(w1, u, z(zBot)) }, shape: "Straight");
    }
    foreach (double u in new[] { xFB + 3 + 0.625 + r7, xFF - 3 - 0.625 - r7 })
        Set(P + "701E", "side", new[] { Pt(w0, u, z(zMid)), Pt(w1, u, z(zMid)) }, shape: "Straight");
    // ---- x02 closed stirrup, x03 U
    {
        double u0 = xFB + 3 + r5, u1 = xFF - 3 - r5, zb = 12 + r5, zt = 33 - r5;
        Set(P + "502E", "footing stirrups", new[] { Pt(0, u0 + 3, z(zt)), Pt(0, u0, z(zt)), Pt(0, u0, z(zb)), Pt(0, u1, z(zb)), Pt(0, u1, z(zt)), Pt(0, u0 + 3.75, z(zt)) }, st, "Closed stirrup (135-deg hooks not dimensioned; drawn closed with a 3\" start / end on the top leg)", 3, 21, 66, 21, 62.25);
        double leg = S ? 39 : 36, baseBot = zBot + r7;               // base on the bottom x01 row
        double ua = xSB + 2 + r5, ub = xSF - 2 - r5;
        Set(P + "503E", "footing / stem U", new[] { Pt(0, ua, z(baseBot + leg - r5)), Pt(0, ua, z(baseBot + r5)), Pt(0, ub, z(baseBot + r5)), Pt(0, ub, z(baseBot + leg - r5)) }, st,
            "U (180-deg hooks not dimensioned)", leg, 44, leg);
    }
    // ---- x06 inverted U under the ledge; x07 / x08M inverted U in the seat
    {
        double ua = xSB + 2 + r5, ub = xSF - 2 - r5, zb = ledge - (2 + r5) / 12, zl = ledge - (2 + 10 - r5) / 12;
        Set(P + "506E", "stem cap U", new[] { Pt(0, ua, zl), Pt(0, ua, zb), Pt(0, ub, zb), Pt(0, ub, zl) }, st, "U", 10, 44, 10);
        double legS = S ? 25 : 23, va = xR + 2 + r5, vb = xSF - 2 - r5, zs = seatFront - (3 + r5) / 12, zsl = seatFront - (3 + legS - r5) / 12;
        var seatU = new[] { Pt(0, va, zsl), Pt(0, va, zs), Pt(0, vb, zs), Pt(0, vb, zsl) };
        Set(P + "507E", "seat U", seatU, stW, "U", legS, 26, legS);
        Set(P + "508M", "seat U (infill zone)", seatU, stE, "U", legS, 26, legS);
    }
    // ---- stem horizontals
    double z1 = FT + 3.0 / 12, z2 = z1 + (S ? 11.5 : 9) / 12, z3 = seatFront - (3 + 0.625 + r6) / 12;
    double uBF = xSB + 3, uFF = xSF - 3, uRis = xR + 3;
    double h0 = wW + 2.0 / 12, h1 = wE - 2.0 / 12;
    double joint = wE - (S ? 29 + 4.0 / 12 : 30 + 10.0 / 12);
    foreach (var (u, zz) in new[] { (uFF, z1), (uFF, z2), (uRis, z2) })
        Set(P + "609E", "stem horiz.", new[] { Pt(h0, u, zz), Pt(h1, u, zz) }, shape: "Straight");
    foreach (double zz in new[] { z1, z2 })
    {
        Set(P + "610E", "stem B.F. W of the joint", new[] { Pt(h0, uBF, zz), Pt(h0 + Listed(P + "610E") / Ft(1), uBF, zz) }, shape: "Straight");
        Set(P + "611E", "stem B.F. E of the joint", new[] { Pt(h1 - Listed(P + "611E") / Ft(1), uBF, zz), Pt(h1, uBF, zz) }, shape: "Straight");
    }
    foreach (double u in new[] { uRis, uFF })
    {
        Set(P + "612E", "seat top W", new[] { Pt(h0, u, z3), Pt(h0 + Listed(P + "612E") / Ft(1), u, z3) }, shape: "Straight");
        Set(P + "613M", "seat top E", new[] { Pt(h1 - Listed(P + "613M") / Ft(1), u, z3), Pt(h1, u, z3) }, shape: "Straight");
    }
    // ---- infill wall (E 18'-5")
    {
        double iw0 = 17 + 7.0 / 12 + 2.0 / 12, iw1 = wE - 2.0 / 12, H = (wallTop - ledge) * 12;
        for (int k = 0; k <= 5; k++)
        {
            double zz = ledge + (4 + (H - 8) * k / 5) / 12;
            foreach (double u in new[] { xSB + 2 + 0.625 + r5, xR - 2 - 0.625 - r5 })
                Set(P + "517E", $"infill horiz. row {k + 1}", new[] { Pt(iw0, u, zz), Pt(iw1, u, zz) }, shape: "Straight");
        }
        double ua = xSB + 2 + r5, ub = xR - 2 - r5, zt = wallTop - (2 + r5) / 12;
        Set(P + "516E", "infill cap U", new[] { Pt(0, ua, wallTop - (12 - r5) / 12), Pt(0, ua, zt), Pt(0, ub, zt), Pt(0, ub, wallTop - (12 - r5) / 12) }, stE, "U", 10, 14, 10);
        double dt = wallTop - (2 + 0.625) / 12, vLeg = S ? 79 : 80, vb = dt - vLeg / 12;
        double ud = xSB + 2 + 0.625 + 0.625 + r5;                      // inside the tie leg
        Set(P + "505E", "infill B.F. dowel", new[] { Pt(0, ud, dt), Pt(0, ud, vb + r5 / 12), Pt(0, ud + 10 - r5, vb + r5 / 12) }, stE, "L", vLeg, 10);
        double uf = xR - 2 - 0.625 - 0.625 - r5;
        double fLen = Listed(P + "515E") / Ft(1) * 12;
        Set(P + "515E", "infill F.F. dowel", new[] { Pt(0, uf, dt), Pt(0, uf, dt - fLen / 12) }, stE, "Straight");
    }
    // ---- W mask wall (S range: inner face wW, outer face wW - 17 1/2")
    {
        double sIn = wW, sOut = wW - 17.5 / 12;
        double sv(double fromInner) => sIn - fromInner / 12;
        double so(double fromOuter) => sOut + fromOuter / 12;
        double mt = maskTop, mb = FB, notch = 5;
        var uV = new List<double> { xFF - 3.5 }; for (int k = 1; k <= 6; k++) uV.Add(xFF - 3.5 - 53.0 * k / 6); uV.Add(uV[^1] - 12);   // 8 vertical positions
        double vTop = mt - 2.0 / 12, vBot = vTop - Listed(P + "518E") / Ft(1);
        foreach (double u in uV)
        {
            Set(P + "518E", "mask vert. B.F.", new[] { new PlanBarPoint(Ft(sv(2 + r5)), I(u), EL, Ft(vBot)), new PlanBarPoint(Ft(sv(2 + r5)), I(u), EL, Ft(vTop)) }, shape: "Straight");
            Set(P + "518E", "mask vert. F.F.", new[] { new PlanBarPoint(Ft(so(2 + r5)), I(u), EL, Ft(vBot)), new PlanBarPoint(Ft(so(2 + r5)), I(u), EL, Ft(vTop)) }, shape: "Straight");
            // cap U tie over the pair: base 1'-0" o-o across the thickness, centred, 2" clr top, legs 10"
            double a = sv(17.5 / 2 - 6 + r5), b = sv(17.5 / 2 + 6 - r5), zc = mt - (2 + r5) / 12, zl = mt - (12 - r5) / 12;
            Set(P + "524E", "mask cap tie", new[] { new PlanBarPoint(Ft(a), I(u), EL, Ft(zl)), new PlanBarPoint(Ft(a), I(u), EL, Ft(zc)), new PlanBarPoint(Ft(b), I(u), EL, Ft(zc)), new PlanBarPoint(Ft(b), I(u), EL, Ft(zl)) }, shape: "U", dimsIn: new[] { 10.0, 12, 10 });
        }
        double hLen = Listed(P + "521E") / Ft(1) * 12, hu1 = xFF - 2, hu0 = hu1 - hLen;
        for (int k = 0; k <= 12; k++)
        {
            double zz = mb + (notch + 3.5 + 142.875 * k / 12) / 12;
            Set(P + "521E", $"mask horiz. B.F. row {k + 1}", new[] { new PlanBarPoint(Ft(sv(2 + 0.625 + r5)), I(hu0), EL, Ft(zz)), new PlanBarPoint(Ft(sv(2 + 0.625 + r5)), I(hu1), EL, Ft(zz)) }, shape: "Straight");
            Set(P + "521E", $"mask horiz. F.F. row {k + 1}", new[] { new PlanBarPoint(Ft(so(2 + 0.625 + r5)), I(hu0), EL, Ft(zz)), new PlanBarPoint(Ft(so(2 + 0.625 + r5)), I(hu1), EL, Ft(zz)) }, shape: "Straight");
            // row tie at the front end: horizontal U, base across the thickness 2" from the front end, legs 10" back
            double a = sv(17.5 / 2 - 6 + r5), b = sv(17.5 / 2 + 6 - r5), ue = xFF - 2 - r5, ul = xFF - 12 + r5;
            Set(P + "524E", "mask row tie", new[] { new PlanBarPoint(Ft(a), I(ul), EL, Ft(zz)), new PlanBarPoint(Ft(a), I(ue), EL, Ft(zz)), new PlanBarPoint(Ft(b), I(ue), EL, Ft(zz)), new PlanBarPoint(Ft(b), I(ul), EL, Ft(zz)) }, shape: "U", dimsIn: new[] { 10.0, 12, 10 });
            // lug tie (x23) at the back end (4" step), hooking the vertical lug x19: DERIVED from D-D (horizontal 1'-0", 4" leg)
            double ub = -43 + 2 + r4;
            Set(P + "423E", "mask lug tie", new[] { new PlanBarPoint(Ft(so(2 + 0.625 + r4)), I(ub + 12), EL, Ft(zz)), new PlanBarPoint(Ft(so(2 + 0.625 + r4)), I(ub), EL, Ft(zz)), new PlanBarPoint(Ft(so(2 + 0.625 + r4 + 4)), I(ub), EL, Ft(zz)) }, shape: "Lug tie (DERIVED position)", dimsIn: new[] { 12.0, 4 });
        }
        // vertical lug x19 at the outer back corner of the 4" step (D-D), from above the toe notch to 2" under the top (DERIVED; bill 12'-5")
        Set(P + "419E", "mask vert. lug", new[] { new PlanBarPoint(Ft(so(2 + r4)), I(-43 + 2 + r4), EL, Ft(mb + (notch + 2) / 12)), new PlanBarPoint(Ft(so(2 + r4)), I(-43 + 2 + r4), EL, Ft(mt - 2.0 / 12)) }, shape: "Straight (DERIVED position)");
        // horizontal lug x22 over the toe notch (C-C), 6'-0" along the wall from 2" inside the front end (DERIVED)
        double l22 = Listed(P + "422E") / Ft(1) * 12;
        Set(P + "422E", "mask horiz. lug", new[] { new PlanBarPoint(Ft(so(notch + 2 + r4)), I(xFF - 2 - l22), EL, Ft(mb + (notch + 2 + r4) / 12)), new PlanBarPoint(Ft(so(notch + 2 + r4)), I(xFF - 2), EL, Ft(mb + (notch + 2 + r4) / 12)) }, shape: "Straight (DERIVED position)");
        // vertical lug ties x20 at the 8 vertical positions, hooking x22 (C-C: up-leg + 1'-1" across + 5" down; DERIVED)
        foreach (double u in uV)
        {
            double zb = mb + (notch + 2 + 0.5 + r4) / 12;
            Set(P + "420E", "mask lug tie", new[] { new PlanBarPoint(Ft(so(notch + 2 + r4)), I(u), EL, Ft(zb + 8.0 / 12)), new PlanBarPoint(Ft(so(notch + 2 + r4)), I(u), EL, Ft(zb)),
                new PlanBarPoint(Ft(so(notch + 2 + r4) + 13.0 / 12 - 0.5 / 12), I(u), EL, Ft(zb)), new PlanBarPoint(Ft(so(notch + 2 + r4) + 13.0 / 12 - 0.5 / 12), I(u), EL, Ft(zb + 5.0 / 12)) }, shape: "Lug tie (DERIVED position)", dimsIn: new[] { 8.0, 13, 5 });
        }
        // ties from the footing / stem into the mask wall: L, 4'-8" along S (3'-4 1/2" embed + 1'-3 1/2" projection = 2" clr to the outer face), leg up
        void Tie(string mark, double u, double zz, double up, string what)
        {
            double sEnd = so(2 + r5), sStart = sIn + 40.5 / 12;
            Set(mark, what, new[] { new PlanBarPoint(Ft(sStart), I(u), EL, Ft(zz)), new PlanBarPoint(Ft(sEnd), I(u), EL, Ft(zz)), new PlanBarPoint(Ft(sEnd), I(u), EL, Ft(zz + (up - r5) / 12)) }, shape: "L", dimsIn: new[] { 56.0, up });
        }
        double dIn = r7 + r5;   // alongside a #7 (footing) / #6 (stem) bar
        foreach (double zz in new[] { z(zTop), z(zMid), z(zBot) })
            foreach (double u in new[] { xFB + 6 + dIn, xFF - 6 - dIn }) Tie(P + "504E", u, zz, 41, "footing tie into the mask wall");
        foreach (double zz in new[] { z1, z2 })
            foreach (double u in new[] { uBF + r6 + r5, uFF - r6 - r5 }) Tie(P + "504E", u, zz, 41, "stem tie into the mask wall");
        foreach (double u in new[] { uRis + r6 + r5, uFF - r6 - r5 }) Tie(P + "514E", u, z3, 24, "seat tie into the mask wall");
    }
    // ---- pedestals (S Type A / B 4'-8" along, N Type C / D 3'-6"; 2'-4" across: x -1'-0" .. +1'-4")
    {
        double len = S ? 56 : 42, ua0 = xR, ua1 = xR + 28;
        foreach (var row in seats.Where(r => r[0] == (S ? "S" : "N")))
        {
            double off = double.Parse(row[3]), top = double.Parse(row[5]);
            bool fut = row[2].Contains("future");
            double clr = fut ? 3 : 2, legT = fut ? 26 : 31;
            string tr = P + (fut ? "527M" : "525E"), lg = P + (fut ? "528M" : "526E");
            double sp0 = off - len / 24;                 // pedestal start (ft)
            // transverse U's (plane normal to the abutment): positions along
            var pos = S ? new List<double> { 3.375, 10.375 }.Concat(Enumerable.Range(1, 4).Select(k => 10.375 + 35.25 * k / 5)).Concat(new[] { 45.625, 52.625 }).ToList()
                        : Enumerable.Range(0, 6).Select(k => 4.375 + 33.25 * k / 5).ToList();
            double zl = top - (clr + 0.625 + r5) / 12, a = ua0 + 3 + r5, b = ua1 - 3 - r5;
            Set(tr, $"pedestal {off:+0;-0} trans.", new[] { Pt(0, a, zl - (legT - 2 * r5) / 12), Pt(0, a, zl), Pt(0, b, zl), Pt(0, b, zl - (legT - 2 * r5) / 12) }, pos.Select(p => sp0 + p / 12), "U", legT, 22, legT);
            // longitudinal U's (plane along the abutment) at the across positions
            var acr = S ? (fut ? new[] { 4, 8.5, 15.5, 19.75, 24 } : new[] { 3.625, 8.625, 15.875, 20.125, 24.375 }) : new[] { 4.375, 4.375 + 19.25 / 3, 4.375 + 2 * 19.25 / 3, 23.625 };
            double zt = top - (clr + r5) / 12, lb = (len - 6) / 2;
            foreach (double c in acr)
                Set(lg, $"pedestal {off:+0;-0} longit.", new[] { Pt(off - (lb - r5) / 12, ua0 + c, zt - (legT - 2 * r5) / 12), Pt(off - (lb - r5) / 12, ua0 + c, zt), Pt(off + (lb - r5) / 12, ua0 + c, zt), Pt(off + (lb - r5) / 12, ua0 + c, zt - (legT - 2 * r5) / 12) },
                    shape: "U", dimsIn: new[] { legT, len - 6, legT });
            // ties (note 1: pedestals 9" or taller - G4 / G5 at off -3 / +3)
            if (!fut && Math.Abs(off) < 4)
            {
                double seatMid = seatBack - (seatBack - seatFront) * (xR + 14 - xR) / 30;      // seat at the pedestal centre (x +2")
                double legX = S ? 41 : 34;
                foreach (double zz in new[] { top - 4.0 / 12, seatMid + 4.0 / 12 })
                {
                    double e0 = sp0 + (2 + r5) / 12, e1 = sp0 + (len - 2 - r5) / 12, ca = ua0 + 2 + r5, cb = ua1 - 2 - r5;
                    Set(P + "529E", $"pedestal {off:+0;-0} tie W", new[] { Pt(e0 + (legX - r5) / 12, ca, zz), Pt(e0, ca, zz), Pt(e0, cb, zz), Pt(e0 + (legX - r5) / 12, cb, zz) }, shape: "U", dimsIn: new[] { legX, 24, legX });
                    Set(P + "529E", $"pedestal {off:+0;-0} tie E", new[] { Pt(e1 - (legX - r5) / 12, ca, zz + 0.625 / 12), Pt(e1, ca, zz + 0.625 / 12), Pt(e1, cb, zz + 0.625 / 12), Pt(e1 - (legX - r5) / 12, cb, zz + 0.625 / 12) }, shape: "U", dimsIn: new[] { legX, 24, legX });
                }
            }
        }
    }
}
Print($"Br02069 abutment plan bars: {pbd.Sets.Count(s => !s.Superstructure)} sets, {pbd.BarList.Count(l => !l.Superstructure)} bill lines");
