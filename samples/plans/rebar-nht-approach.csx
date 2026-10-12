// Northaven Trail (TxDOT CSJ 0047-07-228): APPROACH DECK SLAB reinforcing of the 5 Tx28 units (unit sheets p.109 / 111 / 113 / 115 / 117, typical
// transverse section, bar table, bars CS, plan notes). The sheets give no bar list: "reinforcing steel weight is calculated using an approximate factor of
// 2.3 lb/SF" (note 2) - the plan weights in bar-lists.csv are that estimate. Bars that the sheets locate are placed as PlanBarSets on each unit's deck
// (DeckReinforcementDefinition.PlanBarSets, project plan coordinates):
//  A  #4 top transverse "AT 9\" MAX", full width to 2" from the slab edges ("2\" END COV (TYP)"), each edge divided into the same number of equal spaces
//     (<= 9") between 2" from the deck-end lines - radial on the curves, fanned at the skewed Pier 10 / 11 ends (plan: "21 EQ SPA, 9\" MAX");
//  T  #4 top longitudinal "AT 9\" MAX": 22 lines across the 15'-8" slab, the outer ones in the curbs inside the CS legs (section); 2 1/2" clear to the top;
//  OA #5 top "spaced between bars A at overhang", from the curb inner face (SCALED 11 1/4" from the slab edge, section) to 2'-0 past CL girder (note 6);
//  D  #4 bottom longitudinal: 2 in the bottom corners of the curb stirrups + 1 at 5" inboard (section "5\"") each side;
//  C  #4 curb top: 2 per curb in the top corners of CS;  CS #4 curb stirrups 6" x 8 1/2" (BARS CS) "AT 9\" MAX", 2" clear to the slab edge and the curb top
//     (curb 10" wide, 4" high DERIVED: 8 1/2" + 2" + 2" = 12 1/2" = slab + curb).
// Longitudinal lines run between 2" from the deck-end lines and are split into equal pieces <= 60'-0 with the plan lap (material note 3: #4 = 1'-7");
// the sheets show no splice locations. NOT given by the sheets (UNRESOLVED): P (bottom, "SEE PCP"), UP, the thickened slab end bars G / H / J / M ("SEE IGTS")
// - those standards are not in the set. Called by build-nht.csx (hook); re-runnable:
//   sbm run samples\plans\rebar-nht-approach.csx "samples\plans\Northaven NHT.sbm" -- "samples\plans\Northaven NHT.sbm"
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double I(double inch) => In(inch);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
Doc.Regenerate();
var units = new (string name, string pages)[] { ("NHT Unit 1", "p.109"), ("NHT Unit 2", "p.111"), ("NHT Unit 3", "p.113"), ("NHT Unit 4", "p.115"), ("NHT Unit 5", "p.117") };
foreach (var (uname, pg) in units)
{
    var br = Doc.Project.Bridges.First(b => b.Name == uname);
    var m = Doc.Model(uname);
    var al = Doc.Project.FindAlignment(br.AlignmentName)!;
    var dr = m.Framing.Deck!;
    var deck = br.Deck;
    double eL = deck.LeftEdgeOffset, eR = deck.RightEdgeOffset, t = deck.Thickness;
    double Top(Vec2 p) { var iv = al.Inverse(p); return deck.FinishedElevation(al, iv.Station, iv.Offset); }
    // station on an offset line where the plan distance from the begin / end deck line equals 'inset'
    var st0 = al.Inverse(dr.BeginLinePoint).Station; var st1 = al.Inverse(dr.EndLinePoint).Station;
    double Cross(double off, double inset, bool begin)
    {
        double lo = st0 - F(30), hi = st1 + F(30);
        Func<double, double> g = s => (begin ? dr.FromBegin(al.PointAt(s, off)) : -dr.FromEnd(al.PointAt(s, off))) - (begin ? inset : -inset);
        // FromBegin increases with station, FromEnd decreases
        double a = begin ? st0 - F(30) : st1 - F(60), b = begin ? st0 + F(60) : st1 + F(30);
        for (int k = 0; k < 80; k++) { double mm = (a + b) / 2; if (g(mm) < 0) a = mm; else b = mm; }
        return (a + b) / 2;
    }
    // line along an offset between the end covers, sampled every ~1 ft, z = top - depth (or a function)
    List<Vec3> Line(double off, double endCover, Func<Vec2, double> z)
    {
        double a = Cross(off, endCover, true), b = Cross(off, endCover, false);
        int n = Math.Max(2, (int)Math.Ceiling((b - a) / F(1)));
        return Enumerable.Range(0, n + 1).Select(k => { var p = al.PointAt(a + (b - a) * k / n, off); return p.WithZ(z(p)); }).ToList();
    }
    List<Vec3[]> Split(List<Vec3> line, double maxLen, double lap)
    {
        var cum = new List<double> { 0 };
        for (int i = 1; i < line.Count; i++) cum.Add(cum[^1] + (line[i] - line[i - 1]).Length);
        double total = cum[^1];
        int pieces = total <= maxLen ? 1 : (int)Math.Ceiling((total - lap) / (maxLen - lap));
        double piece = (total + (pieces - 1) * lap) / pieces;
        Vec3 At(double s) { int i = cum.BinarySearch(s); if (i < 0) i = ~i; i = Math.Clamp(i, 1, cum.Count - 1); double u = (s - cum[i - 1]) / Math.Max(1e-12, cum[i] - cum[i - 1]); return line[i - 1] + (line[i] - line[i - 1]) * u; }
        var res = new List<Vec3[]>();
        for (int k = 0; k < pieces; k++)
        {
            double s0 = k * (piece - lap), s1 = Math.Min(total, s0 + piece);
            var pts = new List<Vec3> { At(s0) };
            for (int i = 0; i < cum.Count; i++) if (cum[i] > s0 + 1e-6 && cum[i] < s1 - 1e-6) pts.Add(line[i]);
            pts.Add(At(s1));
            res.Add(pts.ToArray());
        }
        return res;
    }
    // equal division of an offset line (arc length) into n spaces between the end covers
    List<Vec2> Divide(double off, double endCover, int n)
    {
        double a = Cross(off, endCover, true), b = Cross(off, endCover, false);
        int k = 400;
        var pts = Enumerable.Range(0, k + 1).Select(i => al.PointAt(a + (b - a) * i / k, off)).ToList();
        var cum = new List<double> { 0 };
        for (int i = 1; i < pts.Count; i++) cum.Add(cum[^1] + (pts[i] - pts[i - 1]).Length);
        var res = new List<Vec2>();
        for (int j = 0; j <= n; j++)
        {
            double s = cum[^1] * j / n;
            int i = cum.BinarySearch(s); if (i < 0) i = ~i; i = Math.Clamp(i, 1, cum.Count - 1);
            double u = (s - cum[i - 1]) / Math.Max(1e-12, cum[i] - cum[i - 1]);
            res.Add(Vec2.Lerp(pts[i - 1], pts[i], u));
        }
        return res;
    }
    double EdgeLen(double off, double endCover)
    {
        var d = Divide(off, endCover, 1);
        double a = Cross(off, endCover, true), b = Cross(off, endCover, false);
        double l = 0; var prev = al.PointAt(a, off);
        for (int i = 1; i <= 400; i++) { var p = al.PointAt(a + (b - a) * i / 400, off); l += (p - prev).Length; prev = p; }
        return l;
    }
    var sets = new List<PlanBarSet>();
    PlanBarSet New(string mark, string size, string loc, double cover, string shape = "STR") => new() { Mark = mark, Size = size, Location = $"{uname} {loc}", Element = "approach deck", Cover = cover, Shape = shape, Source = pg };
    double d4 = I(0.5), d5 = I(0.625);
    double endCov = I(2);

    // ---------------------------------------------------------------- A top transverse
    double oL = eL + endCov, oR = eR - endCov;                     // bar ends 2" from the slab edges
    double aEnd = endCov + d4 / 2;                                  // bar centre 2" clear from the deck-end lines
    int nA = (int)Math.Ceiling(Math.Max(EdgeLen(oL, aEnd), EdgeLen(oR, aEnd)) / I(9) - 1e-9);
    var pl = Divide(oL, aEnd, nA); var pr = Divide(oR, aEnd, nA);
    double zA = I(2.5) + d4 + d4 / 2;                               // under the T layer
    var A = New("A", "#4", "top transverse bars A (9\" max)", I(2)); A.CoverTop = I(2.5);
    Vec3[] Trans(Vec2 p0, Vec2 p1, double depth, double cut0 = 0, double cut1 = 1)
    {
        var a = Vec2.Lerp(p0, p1, cut0); var b = Vec2.Lerp(p0, p1, cut1);
        // crown point (CL NHT) if crossed
        double o0 = al.Inverse(a).Offset, o1 = al.Inverse(b).Offset;
        var pts = new List<Vec2> { a };
        if (o0 * o1 < 0) { double lo = 0, hi = 1; for (int k = 0; k < 50; k++) { double md = (lo + hi) / 2; if (Math.Sign(al.Inverse(Vec2.Lerp(a, b, md)).Offset) == Math.Sign(o0)) lo = md; else hi = md; } pts.Add(Vec2.Lerp(a, b, (lo + hi) / 2)); }
        pts.Add(b);
        return pts.Select(p => p.WithZ(Top(p) - depth)).ToArray();
    }
    for (int k = 0; k <= nA; k++) A.Add(Trans(pl[k], pr[k], zA));
    A.Note = $"{nA + 1} bars: each slab edge divided into {nA} equal spaces between 2\" from the deck-end lines (max {Math.Max(EdgeLen(oL, aEnd), EdgeLen(oR, aEnd)) / nA / I(1):0.00}\" on the longer edge); bars between corresponding points, 2\" from the slab edges, under the T layer, bent at the crown";
    sets.Add(A);

    // ---------------------------------------------------------------- T top longitudinal (22 lines)
    var T = New("T", "#4", "top longitudinal bars T (9\" max)", I(2)); T.CoverTop = I(2.5);
    double tIn = endCov + d4 + d4 / 2;          // inside the CS legs (CS 2" from the edge)
    int nT = 22;
    for (int j = 0; j < nT; j++)
    {
        double off = eL + tIn + (eR - eL - 2 * tIn) * j / (nT - 1);
        foreach (var b in Split(Line(off, endCov, p => Top(p) - I(2.5) - d4 / 2), F(60), F(1, 7))) T.Bars.Add(PlanBarSet.Encode(b));
    }
    T.Note = $"22 lines {(eR - eL - 2 * tIn) / (nT - 1) / I(1):0.00}\" apart across the slab (section: 20 between the curbs + 1 in each curb inside the CS leg), 2 1/2\" clear to the top; lines split into equal pieces <= 60'-0 with 1'-7\" laps (splice locations not shown)";
    sets.Add(T);

    // ---------------------------------------------------------------- OA #5 between the A bars at the overhangs, to 2'-0 past CL girder
    var OA = New("OA", "#5", "top overhang bars OA (between bars A, 2'-0 past CL girder)", I(2)); OA.CoverTop = I(2.5);
    var beams = m.Prestressed!.Beams;
    double? GirderParam(Vec2 p0, Vec2 p1, int girder)
    {
        // parameter along p0 -> p1 where the segment crosses the girder chord of that girder (any span)
        foreach (var bm in beams.Where(b => b.Girder == girder))
        {
            var q0 = bm.TopStart.XY; var q1 = bm.TopEnd.XY;
            var r = p1 - p0; var s = q1 - q0; double den = Vec2.Cross(r, s);
            if (Math.Abs(den) < 1e-12) continue;
            double u = Vec2.Cross(q0 - p0, s) / den, v = Vec2.Cross(q0 - p0, r) / den;
            if (u >= 0 && u <= 1 && v >= -0.02 && v <= 1.02) return u;
        }
        return null;
    }
    int missOA = 0;
    double oaStart = I(11.25);   // SCALED: from the slab edge (section, just inside the curb)
    for (int k = 0; k < nA; k++)
    {
        var a0 = Vec2.Lerp(pl[k], pl[k + 1], 0.5); var b0 = Vec2.Lerp(pr[k], pr[k + 1], 0.5);
        double w = (b0 - a0).Length;
        var dir = (b0 - a0) * (1 / w);
        // left overhang: girder #1 (index 0), right: girder #2 (index 1)
        foreach (int side in new[] { 0, 1 })
        {
            double? u = GirderParam(a0, b0, side);
            if (u == null) { missOA++; continue; }
            double edgeU = side == 0 ? (oaStart - endCov) / w : 1 - (oaStart - endCov) / w;
            double endU = u.Value + (side == 0 ? 1 : -1) * F(2) / w;
            OA.Add(Trans(a0, b0, I(2.5) + d4 + d5 / 2, Math.Min(edgeU, endU), Math.Max(edgeU, endU)));
        }
    }
    OA.Note = $"one between each pair of A bars at each overhang, from 11 1/4\" off the slab edge (SCALED, section) to 2'-0 past CL girder (note 6)" + (missOA > 0 ? $"; {missOA} positions without a girder crossing (deck ends) not placed" : "");
    sets.Add(OA);

    // ---------------------------------------------------------------- curbs: CS stirrups, C top bars, D bottom bars
    var CS = New("CS", "#4", "curb stirrups CS (6\" x 8 1/2\", 9\" max)", I(2), "TIE");
    CS.Dims = new[] { I(6), I(8.5) };
    var C = New("C", "#4", "curb top bars C", I(2));
    var D = New("D", "#4", "bottom longitudinal bars D (curb / overhang)", I(2));
    foreach (int side in new[] { -1, 1 })
    {
        double edge = side < 0 ? eL : eR;
        double inw = -side;                                          // toward the CL
        double oc = edge + inw * (endCov + I(3));                    // CS centre line (2" + 6"/2 from the edge)
        double csEnd = endCov + d4 / 2;
        int nCS = (int)Math.Ceiling(EdgeLen(oc, csEnd) / I(9) - 1e-9);
        var pc = Divide(oc, csEnd, nCS);
        foreach (var p in pc)
        {
            var iv = al.Inverse(p);
            var across = (al.PointAt(iv.Station, iv.Offset + 1) - al.PointAt(iv.Station, iv.Offset)).Normalized();
            double top = Top(p);
            double zt = top + I(4) - I(2) - d4 / 2, zb = top - t + I(2) + d4 / 2;
            double hw = I(3) - d4 / 2;
            Vec3 Q(double u, double z) => (p + across * u).WithZ(z);
            var corner = Q(side * hw, zt);
            var along = new Vec2(across.Y, -across.X) * d4;     // second hook beside the first (one bar diameter along the curb), so the bar does not retrace itself
            CS.Add(new[] { Q(side * hw - side * I(3.18), zt - I(3.18)), Q(side * hw, zt), Q(-side * hw, zt), Q(-side * hw, zb), Q(side * hw, zb), Q(side * hw, zt), (p + across * (side * hw - side * I(3.18)) + along).WithZ(zt - I(3.18)) });
        }
        // C: top corners, D: bottom corners + 5" inboard
        double cIn = I(3) - d4 - d4 / 2;
        foreach (double o in new[] { oc - cIn, oc + cIn })
            foreach (var b in Split(Line(o, endCov, p => Top(p) + I(4) - I(2) - d4 - d4 / 2), F(60), F(1, 7))) C.Bars.Add(PlanBarSet.Encode(b));
        var dOffs = new List<double> { oc - cIn, oc + cIn, oc + inw * (cIn + I(5)) };
        foreach (double o in dOffs)
            foreach (var b in Split(Line(o, endCov, p => Top(p) - t + I(2) + d4 + d4 / 2), F(60), F(1, 7))) D.Bars.Add(PlanBarSet.Encode(b));
    }
    CS.Note = "closed 6\" x 8 1/2\" (BARS CS) with 4 1/2\" hook tails, outer leg 2\" from the slab edge, 2\" clear below the curb top (curb 4\" high DERIVED from 8 1/2\" + 2\" + 2\" - 8 1/2\" slab), equal spaces <= 9\" between 2\" from the deck ends";
    C.Note = "2 per curb in the CS top corners; lines split <= 60'-0 with 1'-7\" laps";
    D.Note = "3 per side: the CS bottom corners and 5\" inboard (section); lines split <= 60'-0 with 1'-7\" laps; bottom clear 2\" (CS)";
    sets.Add(CS); sets.Add(C); sets.Add(D);

    // ---------------------------------------------------------------- marks the sheets name but do not detail
    var why = new Dictionary<string, string>
    {
        ["P"] = "bottom transverse bars P are marked \"P (BOTT); SEE PCP\": spacing and lengths are in the TxDOT PCP / PCP-FAB standards, not in the set",
        ["G/H/J/M"] = "thickened slab end bars G, H, J and M are \"SEE IGTS\" (thickened slab end standard, not in the set); the thickened end itself is not modeled either",
    };
    if (uname == "NHT Unit 3") why["UP"] = "bar UP (#4) is listed in the p.113 bar table but not shown in the plan or section";
    foreach (var (mk, txt) in why)
        sets.Add(new PlanBarSet { Mark = mk, Size = "#4", Location = $"{uname} {mk}", Element = "approach deck", Source = pg, Unresolved = UnresolvedFlag.Text(pg, $"{uname} deck bars {mk}", txt, "Not modeled; the plan weight (2.3 lb/SF) includes them.") });

    var r = br.Deck.Reinforcement;
    r.Enabled = true; r.Plan = null; r.EpoxyCoated = false; r.TopCover = I(2.5); r.SideCover = I(2);
    r.PlanBarSets = sets;
    Print($"{uname}: A {A.Bars.Count}, T {T.Bars.Count} pieces, OA {OA.Bars.Count}, CS {CS.Bars.Count}, C {C.Bars.Count}, D {D.Bars.Count} pieces");
}
if (Args.Length > 0) { Doc.Save(Args[0]); Print($"Saved {Args[0]}"); }
