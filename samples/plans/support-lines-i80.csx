// Bearing lines of the shared expansion Pier 11WB (& 19G), I-80 WB Units 2 and 3. The framing plans dimension CL BRG to
// CL PIER 1'-8 normal to the pier on BOTH sides (Unit 2: sheet 111 PDF / framing plan; Unit 3 and Ramp G 19G: 1'-8),
// skew 27°27'55". build-i80-wb.csx placed the lines 1'-6 (Unit 2) / 1'-8 (Unit 3) back along the STATION, which on the
// skewed pier made the lines 3'-2 apart instead of 3'-4 and the spans touching 11WB +4.8" / +3" long on every girder.
// The bearing line is the CL pier line shifted PARALLEL by the normal offset (as in build-ramp-g.csx ParallelLine).
// Re-runnable; run after build-i80-wb.csx (and before girders-i80.csx in a full rebuild). On an existing model re-run
// piers-i80.csx, column-rebar.csx and pier-aesthetics.csx afterwards (the cap S origin moves with the support).
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double R80 = Ft(2400), offWb = Ft(41.08), Rwb = R80 - offWb, pc80 = Sta("7597+73.11");
double Wb(string i80) { double s = Sta(i80); return s <= pc80 ? s : pc80 + (s - pc80) * Rwb / R80; }
double Dms(int d, int m, double s) => d + m / 60.0 + s / 3600;
var al = Doc.Alignment("I-80 WB");

// Support line through the alignment at sta with skew (deg, from radial, + = right end up-station), shifted parallel by
// off (normal to it, + = down-station): station where it crosses the alignment and its skew there.
(double sta, double skew) ParallelLine(double sta, double skewDeg, double off)
{
    double k = skewDeg * Math.PI / 180;
    var t0 = Vec2.FromAngle(al.AngleAt(sta));
    var d = al.RightNormalAt(sta) * Math.Cos(k) + t0 * Math.Sin(k);
    var nrm = d.Left;
    if (Vec2.Dot(nrm, t0) < 0) nrm = -nrm;
    var q = al.PointAt(sta, 0) - nrm * off;
    double s = sta - off / Math.Cos(k);
    for (int it = 0; it < 8; it++)
        s -= Vec2.Dot(al.PointAt(s, 0) - q, nrm) / Vec2.Dot(Vec2.FromAngle(al.AngleAt(s)), nrm);
    var ts = Vec2.FromAngle(al.AngleAt(s));
    return (s, Math.Atan2(Vec2.Dot(d, ts), Vec2.Dot(d, al.RightNormalAt(s))) * 180 / Math.PI);
}

double p11 = Wb("7603+32.45"), skew11 = Dms(27, 27, 55), off = Ft(1 + 8 / 12.0);
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
foreach (var (b, sp, o) in new[] { (u2, u2.Supports[^1], off), (u3, u3.Supports[0], -off) })
{
    var (s, k) = ParallelLine(p11, skew11, o);
    Print($"{b.Name} {sp.Name}: station {Doc.Units.FormatStation(sp.Station)} -> {Doc.Units.FormatStation(s)}, skew {sp.SkewDegrees:F4} -> {k:F4}, CL brg-CL pier {sp.PierCenterOffset / Ft(1):F3} -> {o / Ft(1):F3} ft");
    sp.Station = s; sp.SkewDegrees = k; sp.PierCenterOffset = o;
}
var r = Doc.Regenerate();
Print($"Regenerated {r.Bridges} bridges, {r.Errors.Count} errors.");
foreach (var e in r.Errors) Print("  ERROR " + e);
Doc.Save();
