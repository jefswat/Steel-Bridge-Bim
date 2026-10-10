// Cross slopes of WB I-80 per design sheet 190 ("Plan - top of slab cross slope & gore geometrics"), about the WB CL:
//  * left (north): lane 1 (0 to 12'-0 LT) 2.0% to 7596+04.61 (2.5%), 3.0% at 7596+55.86, 5.9% at 7599+53.11.
//    Lane 2 (12'-0 to 24'-0 LT) transitions from 2.0% at 7590+47.50 to 3.0% at 7591+50.00 (102.50' along the I-80 CL);
//    from 7591+50 everything beyond 12'-0 LT slopes 3.0% (to 7596+55.86, where the whole side is 3.0%).
//    The gore (hatched; Ramp A2 grades) slopes 3.4% from Pier 6A to 7591+48.11, 3.0% at 7591+50, beyond the 3'-8 crash
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
al.CrossSlope.Rows = new()
{
    Row("7590+47.50", -0.02, -0.02, Lt("7590+47.50"), -0.0273),
    Row("7590+73.29", -0.02, -0.02, Lt("7590+73.29"), -0.0273),
    // Unit 2 begins at Pier 5WB / Pier 6A (about 7591+06): curb and gore over girders A-E start here.
    Row("7591+04.00", -0.02, -0.02, Lt("7591+04.00"), R273("7591+04.00")),
    Row("7591+05.00", -0.02, -0.02, Lt("7591+05.00", -0.034), R273("7591+05.00")),
    Row("7591+48.11", -0.02, -0.02, Lt("7591+48.11", -0.034)),
    Row("7591+50.00", -0.02, -0.02, new[] { (12.0, -0.03) }),
    Row("7595+58.11", -0.02, 0.02, new[] { (12.0, -0.03) }),
    Row("7596+04.61", -0.025, 0.025, new[] { (12.0, -0.03) }),
    Row("7596+55.86", -0.03, 0.03),
    Row("7599+53.11", -0.059, 0.059),
};
foreach (var (v, p) in al.Profile.Vpis.Zip(pgl)) v.Elevation = p.Pgl - al.CrossSlope.DeltaElevation(v.Station, pglOff);
foreach (var r in al.CrossSlope.Rows)
    Print($"  STA {Doc.Units.FormatStation(r.Station)}: left {r.LeftSlope:P1}{(r.LeftBreak > 0 ? $" to {r.LeftBreak / Ft(1):F0}' then {r.LeftOuterSlope:P2}" : "")}{string.Concat((r.LeftMoreBreaks ?? new()).Select(m => $" to {m.Distance / Ft(1):F2}' then {m.Slope:P2}"))}, right {r.RightSlope:P1}{(r.RightBreak > 0 ? $" to {r.RightBreak / Ft(1):F0}' then {r.RightOuterSlope:P2}" : "")}");
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
Doc.Save();
