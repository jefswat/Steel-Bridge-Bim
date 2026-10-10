// Cross slopes of WB I-80 per design sheet 190 ("Plan - top of slab cross slope & gore geometrics"), about the WB CL:
//  * left (north): lane 1 (0 to 12'-0 LT) 2.0% to 7596+04.61 (2.5%), 3.0% at 7596+55.86, 5.9% at 7599+53.11.
//    Lane 2 (12'-0 to 24'-0 LT) transitions from 2.0% at 7590+47.50 to 3.0% at 7591+50.00 (102.50' along the I-80 CL);
//    from 7591+50 everything beyond 12'-0 LT slopes 3.0% (to 7596+55.86, where the whole side is 3.0%).
//    The gore (hatched; Ramp A2 grades): 3.4% at Pier 6A varying LINEARLY to 3.0% at 7591+50, perpendicular to Ramp A2 (modeled below
//    from the Ramp A2 geometry), beyond the 3'-8 crash
//    cushion curb between the gutter points 65.08' and 68.76' LT of the I-80 CL (= 24.00' / 27.68' LT of the WB CL):
//    1023.21 / 1023.17 at 7591+06.7 (curb 1.09%), 1023.62 / 1023.51 at 7591+50 (3.0%) - sheet 190 Detail A.
//  * right (south), Unit 1: 2.0% to 12'-0 RT then 2.73% to 7590+73.29, transition to 2.0% across at 7591+48.11.
//  * right (south): superelevation transition over 410'-0 from -2.0% (crown) at 7591+48.11 to +2.0% at 7595+58.11, then
//    +2.5% at 7596+04.61, +3.0% at 7596+55.86 and +5.9% at 7599+53.11.
// The CL profile is derived from the I-80 P.G.L. 34'-0 right of the WB CL (CL = PGL - right slope x 34'), so it is
// re-derived here: the PGL is recovered from the current profile and table, then the new slopes are applied.
// Re-runnable. Run after build-i80-wb.csx (and geolocate-cbis.csx).
using SBM.Core.Alignments;

double R80 = Ft(2400), offWb = Ft(41.08), Rwb = R80 - offWb, pc80 = Sta("7597+73.11");
double Wb(string i80) { double s = Sta(i80); return s <= pc80 ? s : pc80 + (s - pc80) * Rwb / R80; }
var al = Doc.Alignment("I-80 WB");
double pglOff = Ft(34);
var old = al.CrossSlope.Clone();
var pgl = al.Profile.Vpis.Select(v => (v.Station, Pgl: v.Elevation + old.DeltaElevation(v.Station, pglOff))).ToList();

// Left: lane 1 slope, then breaks (distance LT, slope beyond); right: slope, then (beyond 12').
CrossSlopeRow Row(string sta, double left, double right, (double ft, double slope)[]? lt = null, double rOuter = double.NaN) =>
    new(Wb(sta), left, right)
    {
        LeftBreak = lt is { Length: > 0 } ? Ft(lt[0].ft) : 0, LeftOuterSlope = lt is { Length: > 0 } ? lt[0].slope : double.NaN,
        LeftMoreBreaks = lt is { Length: > 1 } ? lt.Skip(1).Select(x => new CrossSlopeBreak(Ft(x.ft), x.slope)).ToList() : null,
        RightBreak = double.IsNaN(rOuter) ? 0 : Ft(12), RightOuterSlope = rOuter,
    };
double Frac(string sta, string a, string b) => Math.Clamp((Sta(sta) - Sta(a)) / (Sta(b) - Sta(a)), 0, 1);
// Unit 1 south lanes 2 and 3: 2.73% to 7590+73.29, transition over 74.82' to 2.0% at 7591+48.11.
double R273(string sta) => -0.0273 + Frac(sta, "7590+73.29", "7591+48.11") * (0.0273 - 0.02);
// North lane 2: 2.0% at 7590+47.50 to 3.0% at 7591+50.00 (102.50').
double Lane2(string sta) => -0.02 - Frac(sta, "7590+47.50", "7591+50.00") * 0.01;
// Crash cushion curb (24.00' to 27.68' LT): 1.09% at 7591+06.74 to 3.0% at 7591+50.
double Curb(string sta) => -0.0109 - Frac(sta, "7591+06.74", "7591+50.00") * (0.03 - 0.0109);
(double, double)[] Lt(string sta, double gore = double.NaN) => double.IsNaN(gore)
    ? new[] { (12.0, Lane2(sta)) } : new[] { (12.0, Lane2(sta)), (24.0, Curb(sta)), (27.68, gore) };
// ---- Gore between Pier 6A and 7591+50 (sheet 190 detail "*", PDF 197; Ramp A2 PDF 9 and 186) ----
// The gore is a surface tied to the Ramp A2 P.G.L.: cross slope 3.4% at the Pier 6A line (Ramp A2 sta 61591+09.14) changing
// LINEARLY to 3.0% at Ramp A2 sta 61591+50.00 ("superelevation transition perpendicular to Ramp A2"), measured perpendicular to
// the Ramp A2 P.G.L. (R = 2986'-5, CL Ramp A2 = 61591+08.76 at I-80 7591+08.83, 107.43' LT and 61591+50.00 at I-80 7591+50.00,
// 105.08' LT; north slab edge 13'-7 from it, 118.65' LT at 7591+50.68). Elevations: 1021.45 (6A line) and 1022.06 (7591+50 line)
// at the north end of the 12'-0 dimension from the P.G.L. (the plan's own curb elevations at 6A, 1023.17, confirm the 12'-0).
// The model's slopes are measured normal to the I-80 WB CL, so the surface is represented by the effective slope beyond the curb
// (27.68' LT) at each station: the plane z = zPGL(s) - k(s) n (n = distance LT of the P.G.L.) re-expressed along the WB normal
// (the Ramp A2 heading is rotated about 3 deg from the I-80 radial: a 3.4% slope perpendicular to it is 3.48% along the WB normal).
// Rows every 5 ft hold the effective slope; at 7591+50.00 the I-80 slope is exactly 3.0% (curb 1023.51 to edge 1022.06 = 3.00%).
const double R2 = 2986 + 5.0 / 12, sA = 9.14, sB = 50.0, kA = 0.034, kB = 0.030, yWb0 = 41.08;
double x0 = Sta("7591+00") / Ft(1);
var p1 = (x: 8.83, y: 107.43); var p2 = (x: 50.0, y: 105.08);
double psiM = Math.Atan2(p2.y - p1.y, p2.x - p1.x), sM = 0.5 * (8.76 + 50.0);
(double x, double y) Raw(double s) { double a = (s - sM) / R2; return (R2 * (Math.Sin(psiM + a) - Math.Sin(psiM)), -R2 * (Math.Cos(psiM + a) - Math.Cos(psiM))); }
var o1 = Raw(8.76); var o2 = Raw(50.0);
double offX = 0.5 * (p1.x - o1.x + p2.x - o2.x), offY = 0.5 * (p1.y - o1.y + p2.y - o2.y);
(double x, double y) Pgl(double s) { var r = Raw(s); return (r.x + offX, r.y + offY); }
double Head(double s) => psiM + (s - sM) / R2;
(double s, double n) Foot(double x, double y)   // station along the P.G.L. and distance LT of it
{
    double lo = -20, hi = 120;
    for (int i = 0; i < 80; i++)
    {
        double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
        var a = Pgl(m1); var b = Pgl(m2);
        if (Math.Sqrt((x - a.x) * (x - a.x) + (y - a.y) * (y - a.y)) < Math.Sqrt((x - b.x) * (x - b.x) + (y - b.y) * (y - b.y))) hi = m2; else lo = m1;
    }
    double s = 0.5 * (lo + hi), h = Head(s); var q = Pgl(s);
    return (s, -Math.Sin(h) * (x - q.x) + Math.Cos(h) * (y - q.y));
}
double Kramp(double s) => kA + (kB - kA) * Math.Clamp((s - sA) / (sB - sA), 0, 1);
// P.G.L. elevation: linear in s through the two plan elevations 12'-0 LT of the P.G.L.
double zA = 1021.45 + kA * 12, nB = 0, yB = 105.08;
for (int i = 0; i < 60; i++) { nB = Foot(50.0, yB).n; yB += 12.0 - nB; }
double sBd = Foot(50.0, yB).s, zB = 1022.06 + Kramp(sBd) * 12;
double gPgl = (zB - zA) / (sBd - sA);
double ZGore(double xs, double yLeftI80) { var (s, n) = Foot(xs, yLeftI80); return zA + gPgl * (s - sA) - Kramp(s) * n; }
Print($"Ramp A2 P.G.L.: 12'-0 LT dots at s={sA:F2} and {sBd:F2}, P.G.L. grade {gPgl:P3}, elevation at 61591+09.14 {zA:F3}");

CrossSlopeRow Gore(string sta) { var r = Row(sta, -0.02, -0.02, Lt(sta, -0.034), R273(sta)); return r; }
var goreRows = new List<CrossSlopeRow>();
for (double fx = 5; fx < 49; fx += 5) goreRows.Add(Gore($"7591+{fx:00.00}"));
al.CrossSlope.Rows = new()
{
    Row("7590+47.50", -0.02, -0.02, Lt("7590+47.50"), -0.0273),
    Row("7590+73.29", -0.02, -0.02, Lt("7590+73.29"), -0.0273),
    // Unit 2 begins at Pier 5WB / Pier 6A (about 7591+06): curb and gore over girders A-E start here.
    Row("7591+04.00", -0.02, -0.02, Lt("7591+04.00"), R273("7591+04.00")),
};
al.CrossSlope.Rows.AddRange(goreRows);
al.CrossSlope.Rows.AddRange(new[]
{
    Row("7591+50.00", -0.02, -0.02, new[] { (12.0, -0.03) }),
    Row("7595+58.11", -0.02, 0.02, new[] { (12.0, -0.03) }),
    Row("7596+04.61", -0.025, 0.025, new[] { (12.0, -0.03) }),
    Row("7596+55.86", -0.03, 0.03),
    Row("7599+53.11", -0.059, 0.059),
});
// Per gore row: the crash-cushion curb (24.00' to 27.68' LT) ends on the Ramp A2 surface (plan: 1023.17 at 6A = surface 1023.175),
// so its slope is set from the model's elevation at 24.00' LT to the surface at 27.68' LT; the effective gore slope is the
// least-squares slope of the Ramp A2 surface from 27.68' LT out to 77' LT (the A-E range).
foreach (var r in goreRows)
{
    double xs = r.Station / Ft(1) - x0;
    double z24 = al.ElevationAt(r.Station, -Ft(24)) / Ft(1);
    double z27 = ZGore(xs, yWb0 + 27.68);
    var mb = r.LeftMoreBreaks!;
    mb[0] = new CrossSlopeBreak(mb[0].Distance, -(z24 - z27) / 3.68);
    double num = 0, den = 0;
    for (double y = 32; y <= 77.01; y += 0.5) { double d = y - 27.68; num += (z27 - ZGore(xs, yWb0 + y)) * d; den += d * d; }
    double k = num / den;
    mb[1] = new CrossSlopeBreak(mb[1].Distance, -k);
    Print($"  gore STA {Doc.Units.FormatStation(r.Station)}: curb {(z24 - z27) / 3.68:P2}, effective slope {k:P3} (Ramp A2 k {Kramp(Foot(xs, yWb0 + 54.8).s):P2})");
}
foreach (var (v, p) in al.Profile.Vpis.Zip(pgl)) v.Elevation = p.Pgl - al.CrossSlope.DeltaElevation(v.Station, pglOff);
foreach (var r in al.CrossSlope.Rows)
    Print($"  STA {Doc.Units.FormatStation(r.Station)}: left {r.LeftSlope:P1}{(r.LeftBreak > 0 ? $" to {r.LeftBreak / Ft(1):F0}' then {r.LeftOuterSlope:P2}" : "")}{string.Concat((r.LeftMoreBreaks ?? new()).Select(m => $" to {m.Distance / Ft(1):F2}' then {m.Slope:P2}"))}, right {r.RightSlope:P1}{(r.RightBreak > 0 ? $" to {r.RightBreak / Ft(1):F0}' then {r.RightOuterSlope:P2}" : "")}");
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
Doc.Save();
