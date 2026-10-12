// Northaven Trail (TxDOT CSJ 0047-07-228): SUBSTRUCTURE reinforcing as the plans give it - abutments 1 / 16 (p.95 / 96), approach piers 2-9, 12-15
// (caps, columns, 48" drilled shafts, p.97-100), arch piers 10 / 11 (cap, pedestals, column, 78" drilled shafts, p.101-104).
// Each mark is a PlanBarSet (SubstructureReinforcementDefinition.PlanBarSets, unit frame S along the support line from the CL NHT point (right +), N normal
// (up-station +), Z elevation): every bar is placed from the sheet's dimensions (spacings, covers, bend diagrams); counts and listed lengths are the plan bar
// tables (docs/plan-data/nht-0047-07-228/bar-lists.csv). Bars are never moved for cover: the generator checks clear cover against the unit's concrete and
// reports violations. Called by build-nht.csx (one-line hook) after the substructure is defined; re-runnable on a built model:
//   sbm run samples\plans\rebar-nht-sub.csx "samples\plans\Northaven NHT.sbm" -- "samples\plans\Northaven NHT.sbm"
// Rules (user): model the plans; nothing invented - positions that are only drawn (not dimensioned) are SCALED and say so; what the sheets do not give is
// UNRESOLVED with the page and the reason; 1/16".
using System.IO;
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;

double I(double inch) => In(inch);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
Vec3 V(double s, double n, double z) => new(s, n, z);
var log = new List<string>();
void P(string s) { log.Add(s); }

// ---------------------------------------------------------------- plan bar tables (bar-lists.csv)
string DataDir()
{
    for (var dd = new DirectoryInfo(Environment.CurrentDirectory); dd != null; dd = dd.Parent)
    {
        var p = Path.Combine(dd.FullName, "docs", "plan-data", "nht-0047-07-228");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/nht-0047-07-228 not found above the current directory - run from the repo.");
}
string[] SplitCsv(string line)
{
    var r = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];
        if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = !q; }
        else if (c == ',' && !q) { r.Add(sb.ToString()); sb.Clear(); }
        else sb.Append(c);
    }
    r.Add(sb.ToString());
    return r.ToArray();
}
var table = File.ReadAllLines(Path.Combine(DataDir(), "bar-lists.csv")).Skip(1).Where(l => l.Trim() != "").Select(SplitCsv).ToList();
double LenFt(string s)
{
    var m = System.Text.RegularExpressions.Regex.Match(s.Replace(" ", ""), "^(\\d+)'-(\\d+(?:\\.\\d+)?)\"$");
    return m.Success ? Ft(int.Parse(m.Groups[1].Value)) + In(double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) : double.NaN;
}
(int no, string size, double len, double w, string type) Row(string unit, string mark)
{
    var r = table.First(x => x[0] == unit && x[1] == mark);
    return (int.Parse(r[3]), r[4], LenFt(r[5]), double.Parse(r[6]), r[2]);
}

// ---------------------------------------------------------------- model geometry (regenerated substructure units)
Doc.Regenerate();
SupportDefinition Owner(string name) => Doc.Project.Bridges.SelectMany(b => b.Supports).First(s => s.Name == name && s.Substructure is { Enabled: true });
SubstructureUnit UnitOf(string name)
{
    foreach (var b in Doc.Project.Bridges.Where(b => b.Arch == null))
    {
        var u = Doc.Model(b.Name).Substructure?.Units.FirstOrDefault(x => x.Name == name && x.Definition.Enabled);
        if (u != null) return u;
    }
    throw new InvalidOperationException($"substructure unit {name} not found - run build-nht.csx first");
}

// ---------------------------------------------------------------- bar helpers (unit frame)
PlanBarSet NewSet(string mark, string size, string loc, string element, double listed, string src, double cover, string shape = "STR", params double[] dims) =>
    new() { Mark = mark, Size = size, Location = loc, Element = element, ListedLength = listed, Source = src, Cover = cover, Shape = shape, Dims = dims };
// closed rectangular tie in the plane (a, b) about centre c: centerline half sizes ha (along a) and hb (along b), 135-deg hook tails of length t at the (+a, +b) corner
List<Vec3> Tie(Vec3 c, Vec3 a, Vec3 b, double ha, double hb, double t)
{
    Vec3 Cn(double x, double y) => c + a * (x * ha) + b * (y * hb);
    var k = Cn(1, 1);
    var d = (a + b) * (-1 / Math.Sqrt(2));
    var nrm = Vec3.Cross(a, b) * I(0.625);           // the second hook lies beside the first (one bar diameter), the bar does not retrace itself
    return new List<Vec3> { k + d * t, k, Cn(-1, 1), Cn(-1, -1), Cn(1, -1), k + nrm * 0.5, k + d * t + nrm };
}
Vec3 eS = V(1, 0, 0), eN = V(0, 1, 0), eZ = V(0, 0, 1);
// set the unit's reinforcing
void Apply(string support, List<PlanBarSet> sets, double capCover)
{
    var sd = Owner(support).Substructure!;
    sd.Reinforcement.Enabled = true;
    sd.Reinforcement.CapCover = capCover;
    sd.Reinforcement.CapBars.Clear();
    sd.Reinforcement.PlanBarSets = sets;
    int placed = sets.Sum(x => x.PlacedCount), un = sets.Sum(x => x.Unplaced);
    P($"{support}: {sets.Count} marks, {placed} bars placed{(un > 0 ? $", {un} schedule-only (UNRESOLVED)" : "")}");
}

// ================================================================ 1. approach piers (p.97-100)
// Common (p.97 / 99 section A-A, elevation, bend diagrams; notes "cover dimensions are clear", "bar dimensions are out-to-out"):
//  cap 15'-0 x 3'-6, 2 1/2" clear to the stirrups (typ); stirrups S closed 3'-1 wide (out-to-out) with 135-deg hooks, heights = depth - 5" (S1 = 2'-1 / 3'-1,
//  S2 = the plan's average 1'-7 / 2'-1 over the tapered zone - each S2 is the local depth - 5"); A = 2 rows of 6 #11 inside the stirrups, 4" c-c between rows;
//  B = 4 #11 bottom row following the bend diagram; T side bars inside the stirrup legs at the levels SCALED from section A-A / the elevation (type 1:
//  T1 14", T2 20" below the cap top; type 2: T1-T4 14", 20", 26", 32"); U = 9 caps-top U bars (3'-1 x 6" legs) "8 SPA AT 2'-0 MAX" between 3" from each end;
//  D = #9 x 1'-8" galvanized dowels at the CL bearing of every girder seat (bearing seat detail: 8" above the seat), omitted at piers 4 / 13 / 7 (note 3).
//  Columns: 3" clear to the Z ties (typ), V bars 2" above the top of shaft (2" end cover) and 2'-3" (type 1) / 2'-0 (V2) / 2'-2" + 5'-3" leg (V1) into the cap;
//  Z ties from 3" below EL. C down at the plan spacing, counts = table of variable quantities. Shafts (p.98 / 100): FV verticals 2" below the top of shaft
//  to 3" above the tip (the bottom end is not dimensioned: the typical 3" clear is used), #4 spiral 3" below the top to 1'-0 above the tip with 1 1/2 flat
//  turns top and bottom (9" pitch; type 2: 6" pitch over the top 6'-0), FD dowels 6'-0 into the shaft + 6'-0 into the column next to the column bars.
var wideP = new HashSet<string> { "Pier 6", "Pier 7", "Pier 14", "Pier 15" };
var noDowel = new HashSet<string> { "Pier 4", "Pier 13", "Pier 7" };
foreach (var name in new[] { "Pier 2", "Pier 3", "Pier 4", "Pier 5", "Pier 6", "Pier 7", "Pier 8", "Pier 9", "Pier 12", "Pier 13", "Pier 14", "Pier 15" })
{
    var u = UnitOf(name);
    bool w = wideP.Contains(name);
    string capT = w ? "Pier cap type 2 (P6, 7, 14, 15)" : "Pier cap type 1 (P2-5, 8, 9, 12, 13)";
    string src = w ? "p.99-100" : "p.97-98";
    string colT = $"{name} column";
    var col = u.Columns[0];
    var ftg = u.Foundations[0];
    double zA = u.CapTopAt(0), zC = col.TopZ, zD = col.BottomZ, H = zC - zD;
    double nC = (u.CapN0 + u.CapN1) / 2;                 // cap centre line (N)
    double s0 = u.CapS0, s1 = u.CapS1;                    // cap ends
    var sets = new List<PlanBarSet>();
    double dS = I(0.625), dA = I(1.41), d5 = I(0.625);
    double cov = I(2.5);
    double hS = I(18.5) - dS / 2;                        // stirrup centerline half width (3'-1 out-to-out)
    double SoffitAt(double s) => u.CapBottomAt(s);

    // --- S stirrups
    var st1 = Row(capT, "S1"); var st2 = Row(capT, "S2");
    var S1 = NewSet("S1", "#5", $"{name} cap stirrups S1 (column zone)", "cap", st1.len, src, cov, "TIE");
    var S2 = NewSet("S2", "#5", $"{name} cap stirrups S2 (tapered zone, height = local depth - 5\")", "cap", 0, src, cov, "TIE");
    S2.Note = $"variable height (2 series of 16 / 19 bars, plan average {Ft2(st2.len)}): each stirrup follows the local cap depth; the schedule keeps the geometric lengths.";
    S1.PlanCount = st1.no; S1.PlanWeightLb = st1.w; S2.PlanCount = st2.no; S2.PlanWeightLb = st2.w;
    void Stirrup(PlanBarSet set, double s)
    {
        double top = zA - cov - dS / 2, bot = SoffitAt(s) + cov + dS / 2;
        set.Add(Tie(V(s, nC, (top + bot) / 2), eN, eZ, hS, (top - bot) / 2, I(5.5)));
    }
    if (!w)
    {
        // "BARS S ~ 3", 16 SPA AT 4 1/2" MAX = 5'-9", 3 SPA AT 1'-0", 16 SPA AT 4 1/2" MAX = 5'-9", 3"" (p.97 elevation)
        for (int k = 0; k < 16; k++) { Stirrup(S2, s0 + I(3 + 4.5 * k)); Stirrup(S2, s1 - I(3 + 4.5 * k)); }
        foreach (double x in new[] { -18.0, -6, 6, 18 }) Stirrup(S1, col.S + I(x));
    }
    else
    {
        // "BARS S SPA ~ 3", 19 SPA AT 6" MAX = 9'-0 1/2", 4 SPA AT 1'-0" MAX, 3 SPA AT 6" MAX = 1'-5 1/2", 3"" (p.99 elevation): 19 + 4 + 3 spaces close on the
        // column faces (9'-3 1/2" from the left end = the left column face).
        double cl = col.S - col.SizeS / 2, cr = col.S + col.SizeS / 2;
        double sp2 = (cl - (s0 + I(3))) / 19;
        for (int k = 0; k < 19; k++) Stirrup(S2, s0 + I(3) + k * sp2);
        for (int k = 0; k <= 4; k++) Stirrup(S1, cl + (cr - cl) * k / 4);
        double sp3 = (s1 - I(3) - cr) / 3;
        for (int k = 1; k <= 3; k++) Stirrup(S1, cr + k * sp3);
        if (sp2 > I(6) + I(1.0 / 16)) S2.Note += $" Spacing {sp2 / I(1):0.00}\" = the dimensioned 9'-0 1/2\" / 19 (\"6\" MAX\").";
    }
    sets.Add(S1); sets.Add(S2);

    // --- A top bars: 2 rows of 6 inside the stirrups, 4" c-c
    var ra = Row(capT, "A");
    var A = NewSet("A", "#11", $"{name} cap top bars A (2 rows of 6)", "cap", ra.len, src, cov, w ? "L" : "STR");
    A.PlanCount = ra.no; A.PlanWeightLb = ra.w;
    double nA = hS - dS / 2 - dA / 2;
    double zA1 = zA - cov - dS - dA / 2;
    for (int row = 0; row < 2; row++)
        for (int k = 0; k < 6; k++)
        {
            double n = nC - nA + 2 * nA * k / 5, z = zA1 - row * I(4);
            if (!w) A.Add(new[] { V(-ra.len / 2 + (s0 + s1) / 2, n, z), V(ra.len / 2 + (s0 + s1) / 2, n, z) });
            else
            {
                // BAR A (p.99): 14'-7" + 2'-0" leg down at the deep (column) end, out-to-out from 2 1/2" off the left end
                double sl = s0 + cov, sr = s1 - cov - dA / 2;
                A.Add(new[] { V(sl, n, z), V(sr, n, z), V(sr, n, z + dA / 2 - F(2)) });
            }
        }
    if (w) A.Dims = new[] { F(14, 7), F(2) };
    sets.Add(A);

    // --- B bottom bars (bend diagram)
    var rb = Row(capT, "B");
    var B = NewSet("B", "#11", $"{name} cap bottom bars B (bent to the soffit)", "cap", rb.len, src, cov, "BENT");
    B.PlanCount = rb.no; B.PlanWeightLb = rb.w;
    double zB = zC + cov + dS + dA / 2;
    for (int k = 0; k < 4; k++)
    {
        double n = nC - nA + 2 * nA * k / 3;
        if (!w)
        {
            // BAR B (p.97): 5'-6 3/4" sloped (11 1/2" rise), 3'-5 1/2" flat, 5'-6 3/4" sloped
            double hf = I(41.5) / 2, run = Math.Sqrt(Math.Pow(I(66.75), 2) - Math.Pow(I(11.5), 2));
            B.Add(new[] { V(col.S - hf - run, n, zB + I(11.5)), V(col.S - hf, n, zB), V(col.S + hf, n, zB), V(col.S + hf + run, n, zB + I(11.5)) });
            B.Dims = new[] { F(5, 6.75), F(3, 5.5), F(5, 6.75) };
        }
        else
        {
            // BAR B (p.99): 5'-9" flat from the deep end, 9'-1" sloped (8'-10" horizontal, 1'-11 1/2" rise)
            double sr = s1 - cov;                // 8'-10" + 5'-9" = 14'-7" out-to-out between 2 1/2" end covers
            B.Add(new[] { V(sr - F(5, 9) - F(8, 10), n, zB + F(1, 11.5)), V(sr - F(5, 9), n, zB), V(sr, n, zB) });
            B.Dims = new[] { F(9, 1), F(5, 9) };
        }
    }
    sets.Add(B);

    // --- T side bars (both faces), levels SCALED from section A-A
    double nT = hS - dS / 2 - d5 / 2;
    var levels = w ? new[] { 14.0, 20, 26, 32 } : new[] { 14.0, 20 };
    for (int k = 0; k < levels.Length; k++)
    {
        string mk = $"T{k + 1}";
        var rt = Row(capT, mk);
        var T = NewSet(mk, "#5", $"{name} cap side bars {mk} (each face)", "cap", rt.len, src, cov);
        T.PlanCount = rt.no; T.PlanWeightLb = rt.w;
        T.Note = $"level {levels[k]:0}\" below the cap top SCALED from section A-A ({src}; the side-bar levels are not dimensioned)" + (w ? "; bars end 2 1/2\" from the deep end (elevation)" : "; centred (elevation)");
        foreach (int sd in new[] { -1, 1 })
        {
            double n = nC + sd * nT, z = zA - I(levels[k]);
            if (!w) T.Add(new[] { V(col.S - rt.len / 2, n, z), V(col.S + rt.len / 2, n, z) });
            else { double sr = s1 - cov - dS; T.Add(new[] { V(sr - rt.len, n, z), V(sr, n, z) }); }
        }
        sets.Add(T);
    }

    // --- U top U bars, 9 at "8 SPA AT 2'-0 MAX" between 3" from each cap end
    var ru = Row(capT, "U");
    var U = NewSet("U", "#5", $"{name} cap top U bars", "cap", ru.len, src, cov, "U", F(0, 6), F(3, 1), F(0, 6));
    U.PlanCount = ru.no; U.PlanWeightLb = ru.w;
    double zU = zA - cov - d5 / 2;
    for (int k = 0; k <= 8; k++)
    {
        double s = s0 + I(3) + (s1 - s0 - I(6)) * k / 8;
        U.Add(new[] { V(s, nC - hS, zU - I(6) + d5 / 2), V(s, nC - hS, zU), V(s, nC + hS, zU), V(s, nC + hS, zU - I(6) + d5 / 2) });
    }
    U.Note = "U bars at the stirrup top level (section A-A), 9 equally spaced between 3\" from the cap ends (elevation \"8 SPA AT 2'-0 MAX\")";
    sets.Add(U);

    // --- D dowels at the bearing seats
    var rd = Row(capT, "D");
    var D = NewSet("D", "#9", $"{name} bearing seat dowels D (galvanized)", "cap", rd.len, src, cov);
    D.Coating = "galvanized"; D.CheckCover = false;
    D.Note = "dowel at the CL bearing of each girder seat, 8\" above the bearing seat (bearing seat detail), 1'-0 embedded - projects above the concrete by design (cover not checked)";
    if (!noDowel.Contains(name))
    {
        foreach (var pd in u.Pedestals) D.Add(new[] { V(pd.RowS, pd.RowN, pd.SeatZ + I(8)), V(pd.RowS, pd.RowN, pd.SeatZ + I(8) - rd.len) });
        D.PlanCount = rd.no; D.PlanWeightLb = rd.w;
        if (u.Pedestals.Count != 4) D.Unresolved = UnresolvedFlag.Text(src, $"{name} dowels D", $"the model has {u.Pedestals.Count} girder seats, the bar table lists 4 dowels");
    }
    else { D.PlanCount = 0; D.PlanWeightLb = 0; D.Note = $"omitted at {name} (note 3, {src})"; }
    if (D.Bars.Count > 0) sets.Add(D);

    // --- column: V bars + Z ties
    var colCov = I(3);
    if (!w)
    {
        var rv = Row(colT, "V"); var rz = Row(colT, "Z");
        bool dbl = rz.size == "#5";
        double dZ = dbl ? I(0.625) : I(0.5), dV = I(1.128);
        double c = colCov + dZ + dV / 2;
        var Vb = NewSet("V", "#9", $"{name} column 1 verticals V (20, 6 per face)", "column", rv.len, "p.97-98", colCov);
        Vb.PlanCount = rv.no; Vb.PlanWeightLb = rv.w; Vb.CoverBottom = I(2);
        var pos = new List<(double s, double n)>();
        double half = col.SizeS / 2 - c, sp = 2 * half / 5;
        for (int k = 0; k < 5; k++) { pos.Add((-half + k * sp, -half)); pos.Add((half, -half + k * sp)); pos.Add((half - k * sp, half)); pos.Add((-half, half - k * sp)); }
        foreach (var (ps, pn) in pos) Vb.Add(new[] { V(col.S + ps, col.N + pn, zD + I(2)), V(col.S + ps, col.N + pn, zD + I(2) + rv.len) });
        if (sp > I(5.5) + I(1.0 / 16)) Vb.Note = $"6 bars per face 3\" clear to the ties: {sp / I(1):0.000}\" c-c vs \"5 SPA AT 5 1/2\" MAX\" (p.97) - modeled with the 3\" clear cover";
        sets.Add(Vb);
        var Z = NewSet("Z", rz.size, $"{name} column 1 ties Z ({(dbl ? "double #5 at 3\"" : "#4 at 12\"")})", "column", rz.len, "p.97-98", colCov, "TIE");
        Z.PlanCount = rz.no; Z.PlanWeightLb = rz.w;
        int lev = dbl ? rz.no / 2 : rz.no;
        double pitch = dbl ? I(3) : I(12);
        double hz = col.SizeS / 2 - colCov - dZ / 2;
        for (int k = 0; k < lev; k++)
        {
            double z = zC - I(3) - dZ / 2 - k * pitch;
            Z.Add(Tie(V(col.S, col.N, z), eS, eN, hz, hz, I(6)));
            if (dbl) Z.Add(Tie(V(col.S, col.N, z - dZ), eS, eN, hz, hz, I(6)));
        }
        Z.Note = $"{lev} levels from 3\" below EL. C at {pitch / I(1):0}\"" + (dbl ? ", two #5 ties bundled per level (\"double #5 at 3\" spa\")" : "");
        sets.Add(Z);
        // FD dowels: 5 per face at 5 1/2" (section B-B), next to the column verticals, 6'-0 + 6'-0
        var FD = NewSet("FD", "#9", $"{name} footing 1 (drilled shaft) dowels FD (20)", "drilled shaft", F(12), "p.98", colCov);
        FD.Note = "5 per face at 5 1/2\" (section B-B \"4 SPA AT 5 1/2\" MAX\"), inside the column bar line by one bar diameter (lap in contact), 6'-0 below and 6'-0 above the top of shaft; length 12'-0 = 6'-0 + 6'-0 (not tabulated: shaft steel)";
        double hf = half - dV;
        for (int k = 0; k < 5; k++)
        {
            double x = I(-11 + 5.5 * k);
            foreach (var (ps, pn) in new[] { (x, -hf), (hf, x), (-x, hf), (-hf, -x) })
                FD.Add(new[] { V(col.S + ps, col.N + pn, zD - F(6)), V(col.S + ps, col.N + pn, zD + F(6)) });
        }
        sets.Add(FD);
        ShaftCage(sets, name, ftg, zD, 18, "#9", I(1.128), I(9), double.NaN, "p.98");
    }
    else
    {
        var r1 = Row(colT, "V1"); var r2 = Row(colT, "V2"); var rz = Row(colT, "Z");
        double dZ = I(0.5), dV = I(1.41);
        double c = colCov + dZ + dV / 2;
        double hs = col.SizeS / 2 - c, hn = col.SizeN / 2 - c;
        // S positions (section A-A p.99): corner, + 4 1/2", 5 equal spaces ("5 SPA AT 5 1/2\" MAX"), V1 inner row, 6", V1 outer row (corner)
        double x7 = hs, x6 = hs - I(6), x1 = -hs + I(4.5), sp = (x6 - x1) / 5;
        var xs = new List<double> { -hs, x1 }; for (int k = 1; k <= 4; k++) xs.Add(x1 + k * sp); xs.Add(x6); xs.Add(x7);
        double nsp = 2 * hn / 5;
        var V1 = NewSet("V1", "#11", $"{name} column 1 bars V1 (2 rows of 6 at the deep end, hooked 5'-3\" into the cap)", "column", r1.len, "p.99-100", colCov, "L", 0, F(5, 3));
        V1.PlanCount = r1.no; V1.PlanWeightLb = r1.w; V1.CoverBottom = I(2);
        V1.Note = $"(H - 2\") + 2'-2\" vertical + 5'-3\" leg toward the cantilever (BAR V1); the inner row's leg runs one bar diameter under the outer row's (not dimensioned); 5 spaces of {sp / I(1):0.00}\" vs \"5 SPA AT 5 1/2\" MAX\" (4 1/2\" + 5 x 5 1/2\" + 6\" do not close on the 3\" clear corner bars)";
        for (int row = 0; row < 2; row++)
        {
            double x = row == 0 ? x7 : x6, top = zC + F(2, 2) - dV / 2 - row * dV;
            for (int k = 0; k <= 5; k++)
            {
                double n = -hn + k * nsp;
                double vert = r1.len - F(5, 3) - row * dV;      // vertical leg so the developed length is the listed one (inner row: hook one bar lower)
                V1.Add(new[] { V(col.S + x, col.N + n, top - vert + dV / 2 + row * 0), V(col.S + x, col.N + n, top), V(col.S + x - F(5, 3) + dV / 2, col.N + n, top) });
            }
        }
        sets.Add(V1);
        var V2 = NewSet("V2", "#11", $"{name} column 1 bars V2 (16)", "column", r2.len, "p.99-100", colCov);
        V2.PlanCount = r2.no; V2.PlanWeightLb = r2.w; V2.CoverBottom = I(2);
        var p2 = new List<(double, double)>();
        for (int k = 0; k <= 5; k++) { p2.Add((xs[k], -hn)); p2.Add((xs[k], hn)); }
        for (int k = 1; k <= 4; k++) p2.Add((-hs, -hn + k * nsp));
        foreach (var (ps, pn) in p2) V2.Add(new[] { V(col.S + ps, col.N + pn, zD + I(2)), V(col.S + ps, col.N + pn, zD + I(2) + r2.len) });
        sets.Add(V2);
        var Z = NewSet("Z", "#4", $"{name} column 1 ties Z (#4 at 12\")", "column", rz.len, "p.99-100", colCov, "TIE");
        Z.PlanCount = rz.no; Z.PlanWeightLb = rz.w;
        for (int k = 0; k < rz.no; k++)
            Z.Add(Tie(V(col.S, col.N, zC - I(3) - dZ / 2 - k * I(12)), eS, eN, col.SizeS / 2 - colCov - dZ / 2, col.SizeN / 2 - colCov - dZ / 2, I(6)));
        sets.Add(Z);
        // FD (section A-A p.100: 6 top, 6 bottom, 2 left, 10 right = 24) inside the column bars by one bar diameter
        var FD = NewSet("FD", "#11", $"{name} footing 1 (drilled shaft) dowels FD (24)", "drilled shaft", F(12), "p.100", colCov);
        FD.Note = "24 = 6 top + 6 bottom (with the column bars x1..x6), 2 left (+-2 3/4\"), 2 rows of 5 at 5 1/2\" inside the V1 rows (section A-A / B-B p.100), one bar diameter inside the column bars, 6'-0 + 6'-0 (not tabulated: shaft steel); the right group lies outside the 48\" shaft cage - see the cover report";
        var pf = new List<(double, double)>();
        for (int k = 1; k <= 6; k++) { pf.Add((xs[k], -hn + dV)); pf.Add((xs[k], hn - dV)); }
        pf.Add((-hs + dV, -I(2.75))); pf.Add((-hs + dV, I(2.75)));
        foreach (double x in new[] { x6 - dV, x7 - dV }) for (int k = -2; k <= 2; k++) pf.Add((x, I(5.5 * k)));
        foreach (var (ps, pn) in pf) FD.Add(new[] { V(col.S + ps, col.N + pn, zD - F(6)), V(col.S + ps, col.N + pn, zD + F(6)) });
        sets.Add(FD);
        ShaftCage(sets, name, ftg, zD, 20, "#11", I(1.41), I(9), I(6), "p.100");
    }
    Apply(name, sets, cov);
}

// 48" approach shafts: FV verticals + #4 spiral (p.98 / 100). Not tabulated (shaft reinforcing is part of the drilled shaft item).
void ShaftCage(List<PlanBarSet> sets, string name, FoundationInfo f, double zTop, int nFv, string fvSize, double dFv, double pitch, double upperPitch, string src)
{
    double R = f.SizeS / 2, cov = I(3), dSp = I(0.5);
    double zTip = f.BottomZ;
    var FV = NewSet("FV", fvSize, $"{name} footing 1 (drilled shaft) verticals FV ({nFv})", "drilled shaft", 0, src, cov);
    FV.Note = $"{nFv} equally spaced, 3\" clear to the spiral, one on the +N axis (section); 2\" below the top of shaft to 3\" above the tip - the bottom end is not dimensioned (FD standard not in the set), the typical 3\" clear is used";
    double rv = R - cov - dSp - dFv / 2;
    for (int k = 0; k < nFv; k++)
    {
        double a = Math.PI / 2 + 2 * Math.PI * k / nFv;
        FV.Add(new[] { V(f.S + rv * Math.Cos(a), f.N + rv * Math.Sin(a), zTop - I(2)), V(f.S + rv * Math.Cos(a), f.N + rv * Math.Sin(a), zTip + I(3)) });
    }
    FV.CoverTop = I(2);
    sets.Add(FV);
    var SP = NewSet("SP", "#4", $"{name} footing 1 (drilled shaft) spiral (#4, {(double.IsNaN(upperPitch) ? "9\" pitch" : "6\" pitch over the top 6'-0, 9\" below")})", "drilled shaft", 0, src, cov, "SPIRAL");
    SP.Spirals.Add(new PlanSpiral { X = f.S, Y = f.N, Radius = R - cov - dSp / 2, TopZ = zTop - I(3) - dSp / 2, BottomZ = zTip + F(1) + dSp / 2, Pitch = pitch, FlatTurns = 1.5,
        UpperPitch = double.IsNaN(upperPitch) ? 0 : upperPitch, UpperZoneBottomZ = double.IsNaN(upperPitch) ? double.NaN : zTop - F(6) });
    SP.Note = "3\" below the top of shaft to 1'-0 above the tip (elevation), 1 1/2 flat turns top and bottom";
    sets.Add(SP);
}

string Ft2(double m) { double inch = Math.Round(m / In(1) * 16) / 16; int f = (int)Math.Floor(inch / 12 + 1e-9); return $"{f}'-{inch - f * 12:0.###}\""; }

// ================================================================ 2. abutments 1 and 16 (p.95, 96)
// Section A-A: cap 3'-6 x 2'-6, 2 1/2" clear (typ), 3 1/2" clear at the bottom; backwall 1'-0 on the cap behind the face of backwall (support line).
// A 10 #11 (4 top, 2 at mid-height, 4 bottom), 6" from the cap ends (corner details "6\" ~ BARS A"); S 13 closed 3'-1 x 2'-0 with 6" hooks at 9" from
// each end, 6 1/2" (the outer shaft face), and 9 between the inner shaft faces at 9" ("8 SPA AT 9\" MAX = 6'-0"); U 4 (2 per end: top and bottom), C-shaped
// in plan, back leg 2'-11 1/2" along N 3" clear from the cap end, legs 2'-6"; V 16 hairpins (7" x 5'-1 3/4") 2" below the backwall top, at 1'-0 max from 3"
// off each backwall end; H 8 (4 levels x 2 faces) inside the V legs, 2 1/2" from each backwall end, levels 3" (under the V bend), 9" ("6\""), and two more at
// equal spaces to the cap top ("3 SPA AT 1'-0 MAX"). Abutment 16: U1 (right end) / U2 (left end, back leg 1'-5 1/4" + 2" jog + 1'-5 1/4" along the skewed end).
foreach (var (name, src, csvU) in new[] { ("Abutment 1", "p.95", "Abutment 1"), ("Abutment 16", "p.96", "Abutment 16") })
{
    var u = UnitOf(name);
    var sets = new List<PlanBarSet>();
    double s0 = u.CapS0, s1 = u.CapS1, zC = u.CapTopAt(0), zBot = u.CapBottomAt(0);
    double nC = (u.CapN0 + u.CapN1) / 2;
    double cov = I(2.5), covB = I(3.5), dS = I(0.625), dA = I(1.41), d6 = I(0.75), d5 = I(0.625);
    double hS = (u.CapN1 - u.CapN0) / 2 - cov - dS / 2;
    // S
    var rs = Row(csvU, "S");
    var S = NewSet("S", "#5", $"{name} cap stirrups S", "cap", rs.len, src, cov, "TIE");
    S.CoverBottom = covB; S.PlanCount = rs.no; S.PlanWeightLb = rs.w;
    var fs = u.Foundations.OrderBy(f => f.S).ToList();
    var stations = new List<double> { s0 + I(9), fs[0].S - fs[0].SizeS / 2, fs[1].S + fs[1].SizeS / 2, s1 - I(9) };
    double ia = fs[0].S + fs[0].SizeS / 2, ib = fs[1].S - fs[1].SizeS / 2;
    for (int k = 0; k <= 8; k++) stations.Add(ia + (ib - ia) * k / 8);
    foreach (double s in stations.OrderBy(x => x))
    {
        double top = zC - cov - dS / 2, bot = zBot + covB + dS / 2;
        S.Add(Tie(V(s, nC, (top + bot) / 2), eN, eZ, hS, (top - bot) / 2, I(6)));
    }
    S.Note = $"9\" from each end, then at the outer shaft faces (\"2 BARS S AT 6 1/2\" MAX\": {(fs[0].S - fs[0].SizeS / 2 - s0 - I(9)) / I(1):0.00}\"), 9 between the inner shaft faces at {(ib - ia) / 8 / I(1):0.00}\"";
    sets.Add(S);
    // A
    var ra = Row(csvU, "A");
    var A = NewSet("A", "#11", $"{name} cap longitudinal bars A", "cap", ra.len, src, cov);
    A.CoverBottom = covB; A.PlanCount = ra.no; A.PlanWeightLb = ra.w;
    double nA = hS - dS / 2 - dA / 2, zTop = zC - cov - dS - dA / 2, zLow = zBot + covB + dS + dA / 2;
    double sm = (s0 + s1) / 2;
    void Along(PlanBarSet set, double n, double z, double len) => set.Add(new[] { V(sm - len / 2, n, z), V(sm + len / 2, n, z) });
    for (int k = 0; k < 4; k++) { double n = nC - nA + 2 * nA * k / 3; Along(A, n, zTop, ra.len); Along(A, n, zLow, ra.len); }
    Along(A, nC - nA, (zTop + zLow) / 2, ra.len); Along(A, nC + nA, (zTop + zLow) / 2, ra.len);
    sets.Add(A);
    // U (C in plan)
    double hU = I(35.5) / 2 - d6 / 2;
    foreach (var mk in name == "Abutment 1" ? new[] { "U" } : new[] { "U1", "U2" })
    {
        var ru = Row(csvU, mk);
        var U = NewSet(mk, "#6", $"{name} cap end U bars {mk} (C in plan, top and bottom)", "cap", ru.len, src, cov, "U", F(2, 6), F(2, 11.5), F(2, 6));
        U.CoverBottom = covB; U.PlanCount = ru.no; U.PlanWeightLb = ru.w;
        U.Note = "at the top and bottom bar levels just inside the A layers (elevation: one at each corner), back leg 3\" clear from the end face (corner details)";
        var ends = mk == "U" ? new[] { -1, 1 } : mk == "U1" ? new[] { 1 } : new[] { -1 };
        foreach (int e in ends)
            foreach (double z in new[] { zTop - dA / 2 - d6 / 2, zLow + dA / 2 + d6 / 2 })
            {
                double sb = e < 0 ? s0 + I(3) + d6 / 2 : s1 - I(3) - d6 / 2, sl = sb - e * (F(2, 6) - d6);
                if (mk == "U2")
                {
                    U.Add(new[] { V(sl, nC - hU, z), V(sb, nC - hU, z), V(sb, nC - I(0.0), z), V(sb - e * I(2), nC, z), V(sb - e * I(2), nC + hU, z), V(sl - e * I(2), nC + hU, z) });
                    U.Dims = new[] { F(2, 6), F(1, 5.25), I(2), F(1, 5.25), F(2, 6) };
                    U.Shape = "U-JOG";
                }
                else U.Add(new[] { V(sl, nC - hU, z), V(sb, nC - hU, z), V(sb, nC + hU, z), V(sl, nC + hU, z) });
            }
        sets.Add(U);
    }
    // V hairpins + H in the backwall
    var bw = u.Backwall!.Value;
    double nb = (bw.N0 + bw.N1) / 2, hb = (bw.N1 - bw.N0) / 2;
    double TopAt(double s) => SubstructureUnit.Interp(bw.Top, s);
    var rv = Row(csvU, "V");
    var Vh = NewSet("V", "#5", $"{name} backwall hairpins V", "backwall", rv.len, src, cov, "HAIRPIN", F(5, 1.75), I(7), F(5, 1.75));
    Vh.CoverTop = I(2); Vh.PlanCount = rv.no; Vh.PlanWeightLb = rv.w;
    double hv = I(3.5) - d5 / 2;
    double legV = F(5, 1.75) - d5 / 2;
    for (int k = 0; k < 16; k++)
    {
        double s = s0 + I(3) + (s1 - s0 - I(6)) * k / 15;
        double zt = TopAt(s) - I(2) - d5 / 2;
        Vh.Add(new[] { V(s, nb - hv, zt - legV), V(s, nb - hv, zt), V(s, nb + hv, zt), V(s, nb + hv, zt - legV) });
    }
    Vh.Note = $"16 at {(s1 - s0 - I(6)) / 15 / I(1):0.00}\" (\"AT 1'-0\" MAX, 3\" FROM EDGE OF BACKWALL\"), bend 2\" clear below the backwall top (follows the trail surface), legs 5'-1 3/4\" out-to-out into the cap";
    sets.Add(Vh);
    var rh = Row(csvU, "H");
    var Hh = NewSet("H", "#6", $"{name} backwall horizontal bars H (4 levels each face)", "backwall", rh.len, src, cov);
    Hh.CoverTop = I(2); Hh.PlanCount = rh.no; Hh.PlanWeightLb = rh.w;
    double minTop = bw.Top.Min(p => p.Y);
    double h1 = I(2) + d5 + d6 / 2, h2 = h1 + I(6), gap = (minTop - zC - h2) / 3;
    foreach (double dz in new[] { h1, h2, h2 + gap, h2 + 2 * gap })
        foreach (int sd in new[] { -1, 1 })
            Along(Hh, nb + sd * (hv - d5 / 2 - d6 / 2), minTop - dz, rh.len);
    Hh.Note = $"inside the V legs; levels below the lowest backwall top: {h1 / I(1):0.0}\" (under the V bend), +6\", then 2 equal spaces of {gap / I(1):0.0}\" (\"3 SPA AT 1'-0\" MAX\" to the cap top); 2 1/2\" from each end";
    sets.Add(Hh);
    Apply(name, sets, cov);
    P($"{name}: drilled shaft cages (2 x 36\") - UNRESOLVED (p.95/96 note 2: 'see common foundation details (FD) standard', not in the set)");
    var sdef = Owner(name).Substructure!;
    sdef.Unresolved ??= new();
    sdef.Unresolved.RemoveAll(x => x.Contains("drilled shaft reinforcing"));
    sdef.Unresolved.Add(UnresolvedFlag.Text(src, $"{name} drilled shaft reinforcing (2 x 36\")", "the sheet refers to the common foundation details standard (FD) for all shaft details; FD is not in the plan set", "No shaft bars modeled."));
}

// ================================================================ 3. arch piers 10 and 11 (p.101-104)
// Bill p.104 (both piers, V2 = Pier 10 / V3 = Pier 11): per pier = half of every other row. Cap: A1 11 headed (#11 23'-6") top layer, M 8 bottom (4 from each end,
// alternating across, BARS M 2'-0 + 3'-0 drop over 10'-0 + 4'-0 flat), stirrups at 37 equally spaced stations 4" from the ends (S5-S19 / S21-S35 each
// side, S20 / S36 x 7 at the column), outer S5-S20 5'-7 wide, inner S21-S36 2'-4 wide, heights A per the p.104 table, tops 6" below the cap top (SCALED from
// section A-A p.103 - the bill heights with 2 1/2" clear tops would leave the M bars uncovered by the stirrups); side bars T in 6 levels per face (SCALED from
// section A-A: 15 3/4" below the top, 8 1/4" apart): T4 x 3, T3, T2, T1. Pedestals: E2 hoops (3 levels), E1 (6, "5 EQ SPA" along the long axis), E3 (3 across).
// Column: 28 bars on an 8 x 8 perimeter grid (section D-D), V2 / V3 headed at the top under the cap top layer; ties per 6" level (count = bill).
// Shaft (p.101): 28 #11 equally spaced (one on the pier line), #7 hoops at 6" max with a 4'-3" lap, 3" below the top to 1'-0 above the tip; V1 dowels
// (28 #11) 5'-8" above / 5'-8" below the top of shaft inside the column bars (the bill does not list them: "cost of V1 bars incidental to the shaft").
{
    const string capT = "Arch piers 10 & 11";
    foreach (var (name, vMark, pierNo) in new[] { ("Pier 10", "V2", 10), ("Pier 11", "V3", 11) })
    {
        var u = UnitOf(name);
        var sd = Owner(name).Substructure!;
        var col = u.Columns[0]; var f = u.Foundations[0];
        double zC = u.CapTopAt(col.S), zF = col.TopZ, cs = col.S;
        double nC = (u.CapN0 + u.CapN1) / 2;
        double cov = I(2.5), dS = I(0.625), d11 = I(1.41), d8 = I(1.0), d5 = I(0.625), d7 = I(0.875);
        var sets = new List<PlanBarSet>();
        (int no, double len, double w) Half(string mark) { var r = Row(capT, mark); return (r.no / 2, r.len, r.w / 2); }
        double capTopCover = I(6);   // SCALED (section A-A p.103), see header
        // stirrups
        double s0 = cs - F(12) + I(4), sp = (F(24) - I(8)) / 36;
        var marks = new Dictionary<string, PlanBarSet>();
        PlanBarSet Mk(string m, double width)
        {
            if (marks.TryGetValue(m, out var x)) return x;
            var h = Half(m);
            var set = NewSet(m, "#5", $"{name} cap stirrup {m} ({(width > F(3) ? "outer, 5'-7\" wide" : "inner, 2'-4\" wide")})", "cap", h.len, "p.103-104", cov, "TIE");
            set.PlanCount = h.no; set.PlanWeightLb = h.w;
            return marks[m] = set;
        }
        // A heights of S5..S20 (p.104 table)
        var aH = new Dictionary<int, double> { [5] = F(2, 2), [6] = F(2, 4.5), [7] = F(2, 7), [8] = F(2, 9.5), [9] = F(3), [10] = F(3, 2.5), [11] = F(3, 5), [12] = F(3, 7.5),
            [13] = F(3, 10), [14] = F(4), [15] = F(4, 3), [16] = F(4, 5), [17] = F(4, 7.5), [18] = F(4, 10), [19] = F(5, 0.5), [20] = F(5, 1) };
        for (int i = 0; i <= 36; i++)
        {
            int k = i <= 14 ? 5 + i : i <= 21 ? 20 : 5 + (36 - i);
            double s = s0 + i * sp, top = zC - capTopCover - dS / 2, a = aH[k] - dS;
            Mk($"S{k}", F(5, 7)).Add(Tie(V(s, nC, top - a / 2), eN, eZ, F(5, 7) / 2 - dS / 2, a / 2, I(4)));
            Mk($"S{k + 16}", F(2, 4)).Add(Tie(V(s, nC, top - a / 2), eN, eZ, F(2, 4) / 2 - dS / 2, a / 2, I(4)));
        }
        foreach (var m in marks.Values.OrderBy(x => int.Parse(x.Mark[1..])))
        {
            m.Note = $"37 stations equally spaced at {sp / I(1):0.00}\" from 4\" off each end (\"EQ. SPA. @ 8\"\"), height per the p.104 table, top 6\" below the cap top (SCALED, section A-A p.103)";
            sets.Add(m);
        }
        // A1 top bars (headed both ends)
        var ha = Half("A1");
        var A1 = NewSet("A1", "#11", $"{name} cap top bars A1 (headed both ends)", "cap", ha.len, "p.103-104", cov);
        A1.PlanCount = ha.no; A1.PlanWeightLb = ha.w;
        A1.Note = "11 equally spaced inside the outer stirrups (section A-A), under the stirrup top; heads not modeled";
        double nA = F(5, 7) / 2 - dS - d11 / 2, zA1 = zC - capTopCover - dS - d11 / 2;
        for (int k = 0; k < 11; k++) A1.Add(new[] { V(cs - ha.len / 2, nC - nA + 2 * nA * k / 10, zA1), V(cs + ha.len / 2, nC - nA + 2 * nA * k / 10, zA1) });
        sets.Add(A1);
        // M bottom bars
        var hm = Half("M");
        var M = NewSet("M", "#8", $"{name} cap bottom bars M (4 from each end, alternating)", "cap", hm.len, "p.103-104", cov, "BENT", F(2), Math.Sqrt(Math.Pow(F(10), 2) + Math.Pow(F(3), 2)), F(4));
        M.PlanCount = hm.no; M.PlanWeightLb = hm.w;
        double zMf = zF + (F(6) - capTopCover - aH[20]) + dS + d8 / 2;          // flat part inside the S20 stirrup bottoms
        double nM = F(5, 7) / 2 - dS - d8 / 2;
        for (int k = 0; k < 8; k++)
        {
            int e = k % 2 == 0 ? -1 : 1;
            double n = nC - nM + 2 * nM * k / 7;
            double sEnd = e < 0 ? u.CapS0 + cov + d8 / 2 : u.CapS1 - cov - d8 / 2;
            double sKink = sEnd - e * F(10), sFlat = sKink - e * F(4);
            M.Add(new[] { V(sEnd, n, zMf + F(3) + F(2) - d8 / 2), V(sEnd, n, zMf + F(3)), V(sKink, n, zMf), V(sFlat, n, zMf) });
        }
        M.Note = "8 equally spaced (section A-A), alternately from the left and right cap ends (4 + 4), vertical leg 2\" 1/2 clear from the end, flat 4'-0 laps across the column; flat level inside the S20 stirrup bottoms";
        sets.Add(M);
        // T side bars
        var lev = new[] { 15.75, 24, 32.25, 40.5, 48.75, 57 };
        var tm = new[] { "T4", "T4", "T4", "T3", "T2", "T1" };
        double nT = F(3) - cov - dS - d8 / 2;
        var tSets = new Dictionary<string, PlanBarSet>();
        for (int k = 0; k < 6; k++)
        {
            var h = Half(tm[k]);
            if (!tSets.TryGetValue(tm[k], out var T))
            {
                T = tSets[tm[k]] = NewSet(tm[k], "#8", $"{name} cap side bars {tm[k]} (each face)", "cap", h.len, "p.103-104", cov);
                T.PlanCount = h.no; T.PlanWeightLb = h.w;
                T.Note = "levels 15 3/4\" below the top, 8 1/4\" apart (\"5 EQ. SPA.\" SCALED, section A-A p.103), centred (elevation)";
            }
            foreach (int sdd in new[] { -1, 1 }) T.Add(new[] { V(cs - h.len / 2, nC + sdd * nT, zC - I(lev[k])), V(cs + h.len / 2, nC + sdd * nT, zC - I(lev[k])) });
        }
        var T1 = tSets["T1"];
        T1.Unplaced = T1.PlanCount!.Value - T1.Bars.Count;
        T1.Unresolved = UnresolvedFlag.Text("p.103-104", $"{name} cap side bars T1", $"the bill lists 16 T1 for both piers (8 per pier) but the elevation and section A-A show one T1 per face per pier (2); the other {T1.Unplaced} per pier are not located", "Counted as schedule-only bars.");
        foreach (var x in new[] { "T4", "T3", "T2", "T1" }) sets.Add(tSets[x]);
        // pedestals (arch bearings)
        var e1 = Half("E1"); var e2 = Half("E2"); var e3 = Half("E3");
        var E1 = NewSet("E1", "#5", $"{name} cap arch pedestal bars E1 (inverted U across)", "pedestal", e1.len, "p.103-104", cov, "U", F(3, 2), F(1, 0.75), F(3, 2));
        var E2 = NewSet("E2", "#5", $"{name} cap arch pedestal hoops E2", "pedestal", e2.len, "p.103-104", cov, "TIE");
        var E3 = NewSet("E3", "#5", $"{name} cap arch pedestal bars E3 (inverted U along)", "pedestal", e3.len, "p.103-104", cov, "U", F(3, 2), F(2, 8), F(3, 2));
        E1.PlanCount = e1.no; E1.PlanWeightLb = e1.w; E2.PlanCount = e2.no; E2.PlanWeightLb = e2.w; E3.PlanCount = e3.no; E3.PlanWeightLb = e3.w;
        foreach (var cb in sd.CapBlocks.Where(b => b.Kind == CapBlockKind.Pedestal))
        {
            var o = cb.Outline.Select(p => new Vec2(p.S, p.N)).ToList();
            var c = new Vec2(o.Average(p => p.X), o.Average(p => p.Y));
            var e01 = o[1] - o[0]; var e12 = o[2] - o[1];
            var ua = (e01.Length >= e12.Length ? e01 : e12).Normalized(); var va = ua.Left;
            double top = cb.TopElevation, baseZ = zC;
            Vec3 Pt(double x, double y, double z) { var q = c + ua * x + va * y; return V(q.X, q.Y, z); }
            Vec3 ua3 = V(ua.X, ua.Y, 0), va3 = V(va.X, va.Y, 0);
            // E2 hoops 2'-11 x 1'-2 (2 1/2" clear), 3 levels 2 1/2" clear from the pedestal top and the cap top, equal spaces
            double z1 = top - cov - d5 / 2, z3 = baseZ + cov + d5 / 2;
            for (int k = 0; k < 3; k++) { var cc = Pt(0, 0, z1 + (z3 - z1) * k / 2); E2.Add(Tie(cc, ua3, va3, F(2, 11) / 2 - d5 / 2, F(1, 2) / 2 - d5 / 2, I(4))); }
            // E1: 6 along the long axis inside the hoop ("E1 - 5 EQ SPA"), 1'-0 3/4 across, legs 3'-2 down, bend 2 1/2" clear below the top
            double xu = F(2, 11) / 2 - d5 - d5 / 2, yv = F(1, 0.75) / 2 - d5 / 2, zt = top - cov - d5 / 2, leg = F(3, 2) - d5 / 2;
            for (int k = 0; k < 6; k++) { double x = -xu + 2 * xu * k / 5; E1.Add(new[] { Pt(x, -yv, zt - leg), Pt(x, -yv, zt), Pt(x, yv, zt), Pt(x, yv, zt - leg) }); }
            // E3: 3 across ("E3 2 EQ SPA"), 2'-8 along, one bar under the E1 bends
            double xe = F(2, 8) / 2 - d5 / 2, z3t = zt - d5;
            for (int k = 0; k < 3; k++) { double y = -yv + 2 * yv * k / 2; E3.Add(new[] { Pt(-xe, y, z3t - leg), Pt(-xe, y, z3t), Pt(xe, y, z3t), Pt(xe, y, z3t - leg) }); }
        }
        E1.Note = "inside the E2 hoops, 6 along the 3'-4\" axis (\"E1 - 5 EQ. SPA.\", section B-B), bend 2 1/2\" clear below the pedestal top";
        E2.Note = "3 levels (\"E2. 2 EQ. SPA.\"), top and bottom hoops 2 1/2\" clear from the pedestal top and the cap top";
        E3.Note = "3 across the 1'-7\" axis (\"E3 2 EQ. SPA.\"), one bar diameter under the E1 bends";
        sets.Add(E1); sets.Add(E2); sets.Add(E3);
        // H1 (drawn, not in the bill)
        var H1 = NewSet("H1", "#5", $"{name} cap pedestal hairpins H1 (drawn on p.103 / 104, not in the bill)", "pedestal", F(7, 6), "p.103-104", cov, "HAIRPIN");
        H1.Unplaced = 6; H1.PlanCount = 0; H1.PlanWeightLb = 0;
        H1.Unresolved = UnresolvedFlag.Text("p.103-104", $"{name} pedestal bars H1", "BARS H1 (3'-6\" x 1'-0 3/4\" hairpin, 3 per pedestal in section C-C, enclosed end around two anchor bolts, 2'-6\" min past the farthest bolt) are drawn but not in the p.104 bill and their plan positions depend on the anchor bolt layout (bearing sheet 1 of 2, not in the set)",
            "Counted as 6 schedule-only bars per pier, developed length 7'-6\" DERIVED from the bend diagram; not in the plan weight.");
        sets.Add(H1);
        // column bars (8 x 8 perimeter grid) + ties
        var hv = Half(vMark); var rvv = Row(capT, vMark);
        var Vc = NewSet(vMark, "#11", $"{name} column 1 bars {vMark} (headed at the top)", "column", rvv.len, "p.103-104", I(3));
        Vc.PlanCount = rvv.no; Vc.PlanWeightLb = rvv.w;
        double cIn = I(3) + dS + d11 / 2, hc = col.SizeS / 2 - cIn, gsp = 2 * hc / 7;
        var grid = new List<(double, double)>();
        for (int k = 0; k < 7; k++) { grid.Add((-hc + k * gsp, -hc)); grid.Add((hc, -hc + k * gsp)); grid.Add((hc - k * gsp, hc)); grid.Add((-hc, hc - k * gsp)); }
        double vTop = zA1 - d11 / 2 - d11 / 2;          // head under the A1 layer
        foreach (var (ps, pn) in grid) Vc.Add(new[] { V(cs + ps, col.N + pn, vTop - rvv.len), V(cs + ps, col.N + pn, vTop) });
        Vc.Note = $"8 x 8 perimeter grid at {gsp / I(1):0.00}\" (section D-D \"EQ. SPA.\"), 3\" clear to S1; top (headed) under the A1 layer, bottom = top - listed length ({(vTop - rvv.len - f.TopZ) / Ft(1):0.00}' above the top of shaft); heads not modeled";
        sets.Add(Vc);
        var s1 = Half("S1"); var s2 = Half("S2"); var s3 = Half("S3"); var s4 = Half("S4");
        int nLev = s1.no;           // bill S1 per pier (36 / 30 DERIVED: 66 split by the column heights)
        nLev = pierNo == 10 ? 36 : 30;
        var S1c = NewSet("S1", "#5", $"{name} column 1 hoops S1", "column", s1.len, "p.103-104", I(3), "TIE");
        var S2c = NewSet("S2", "#5", $"{name} column 1 ties S2", "column", s2.len, "p.103-104", I(3), "TIE");
        var S3c = NewSet("S3", "#5", $"{name} column 1 ties S3", "column", s3.len, "p.103-104", I(3), "TIE");
        var S4c = NewSet("S4", "#5", $"{name} column 1 ties S4", "column", s4.len, "p.103-104", I(3), "TIE");
        // marks S1-S4 also exist in the cap? no: cap stirrups are S5-S36
        S1c.PlanCount = nLev; S3c.PlanCount = nLev; S2c.PlanCount = nLev / 2; S4c.PlanCount = nLev / 2;
        double wpc = Row(capT, "S1").w / 66;
        S1c.PlanWeightLb = Row(capT, "S1").w * nLev / 66; S3c.PlanWeightLb = Row(capT, "S3").w * nLev / 66;
        S2c.PlanWeightLb = Row(capT, "S2").w * (nLev / 2) / 33; S4c.PlanWeightLb = Row(capT, "S4").w * (nLev / 2) / 33;
        double zb = f.TopZ + I(3), ztt = zF - I(3), lsp = (ztt - zb) / (nLev - 1);
        double h1 = F(4) / 2 - dS / 2, hr = F(1, 10) / 2 - dS / 2;
        for (int k = 0; k < nLev; k++)
        {
            double z = zb + k * lsp;
            S1c.Add(Tie(V(cs, col.N, z), eS, eN, h1, h1, I(4)));
            if (k % 2 == 0)
            {
                S3c.Add(Tie(V(cs, col.N, z - dS), eS, eN, hr, h1, I(4)));                         // long along N, centred
                S2c.Add(Tie(V(cs, col.N + gsp, z - 2 * dS), eS, eN, h1, hr, I(4)));               // long along S, one bar space off centre
            }
            else
            {
                S3c.Add(Tie(V(cs, col.N, z - dS), eS, eN, h1, hr, I(4)));                         // long along S, centred
                S4c.Add(Tie(V(cs + gsp, col.N, z - 2 * dS), eS, eN, hr, h1, I(4)));               // long along N, one bar space off centre
            }
        }
        string arr = UnresolvedFlag.Text("p.103-104", $"{name} column tie arrangement S2 / S3 / S4", "section D-D draws two S2, two S3 and two S4 interior ties, while the bill (66 S1, 66 S3, 33 S2, 33 S4 for both piers) gives one S3 per 6\" level and one S2 or S4 per level alternately (\"S2 @ 12\" alternate w/ S4\"); which pair is used at which level is not shown",
            "Per level: S1; S3 centred, alternately long along N / along S; S2 (long along S) on the levels with S3 along N, S4 (long along N) on the others, each one bar space off centre as drawn.");
        S3c.Unresolved = arr;
        S1c.Note = $"{nLev} levels equally spaced {lsp / I(1):0.00}\" from 3\" above the top of shaft to 3\" below the cap soffit (\"EQ. SPA. @ 6\" MAX.\"); bill 66 for both piers split 36 / 30 by the column heights (DERIVED); 4\" hook tails";
        sets.Add(S1c); sets.Add(S2c); sets.Add(S3c); sets.Add(S4c);
        // shaft: 28 #11 + #7 hoops, V1 dowels
        double R = f.SizeS / 2;
        var SV = NewSet("SV", "#11", $"{name} footing 1 (drilled shaft) bars (28 #11)", "drilled shaft", 0, "p.101", I(3));
        SV.Note = "28 equally spaced on the 3\" clear cage, one on the pier line (section B-B); 3\" below the top of shaft to 1'-0 above the tip (elevation); not in the bill (shaft item)";
        double rs = R - I(3) - d7 - d11 / 2;
        for (int k = 0; k < 28; k++) { double a = 2 * Math.PI * k / 28; SV.Add(new[] { V(f.S + rs * Math.Cos(a), f.N + rs * Math.Sin(a), f.TopZ - I(3)), V(f.S + rs * Math.Cos(a), f.N + rs * Math.Sin(a), f.BottomZ + F(1)) }); }
        sets.Add(SV);
        var HP = NewSet("HP", "#7", $"{name} footing 1 (drilled shaft) hoops (#7 at 6\" max, 4'-3\" lap)", "drilled shaft", 0, "p.101", I(3), "HOOP");
        double rh = R - I(3) - d7 / 2, zh0 = f.TopZ - I(3) - d7 / 2, zh1 = f.BottomZ + F(1);
        int nh = (int)Math.Ceiling((zh0 - zh1) / I(6)) + 1;
        double lapA = I(51) / rh;
        for (int k = 0; k < nh; k++)
        {
            double z = zh0 - (zh0 - zh1) * k / (nh - 1), a0 = 0.6 * k;
            int np = 48;
            HP.Add(Enumerable.Range(0, np + 1).Select(j => { double a = a0 + (2 * Math.PI + lapA) * j / np; double r = rh - d7 * Math.Clamp((a - a0 - (2 * Math.PI - lapA)) / lapA, 0, 1); return V(f.S + r * Math.Cos(a), f.N + r * Math.Sin(a), z); }));   // the lapped end runs inside the start (in contact), no self-overlap
        }
        HP.Note = $"{nh} hoops equally spaced ({(zh0 - zh1) / (nh - 1) / I(1):0.00}\") from 3\" below the top of shaft to 1'-0 above the tip, 4'-3\" lap (section B-B), laps staggered; not in the bill";
        sets.Add(HP);
        var V1 = NewSet("V1", "#11", $"{name} footing 1 (drilled shaft) dowels V1 (28, to match the column bars)", "drilled shaft", F(11, 4), "p.101", I(3));
        V1.Note = "28 inside the column bar grid by one bar diameter (\"V1 BARS SPA. TO MATCH COLUMN BARS\", lap in contact), 5'-8\" above and 5'-8\" below the top of shaft; length 11'-4\" DERIVED (not in the bill: incidental to the shaft)";
        foreach (var (ps, pn) in grid)
        {
            double sx = ps - Math.Sign(ps) * (Math.Abs(ps) > hc - 1e-6 ? d11 : 0), nx = pn - Math.Sign(pn) * (Math.Abs(pn) > hc - 1e-6 ? d11 : 0);
            V1.Add(new[] { V(cs + sx, col.N + nx, f.TopZ - F(5, 8)), V(cs + sx, col.N + nx, f.TopZ + F(5, 8)) });
        }
        sets.Add(V1);
        Apply(name, sets, cov);
    }
}

foreach (var l in log) Print(l);
if (Args.Length > 0) { Doc.Save(Args[0]); Print($"Saved {Args[0]}"); }
