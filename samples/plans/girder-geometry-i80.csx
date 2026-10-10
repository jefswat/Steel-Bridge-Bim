// As-designed plan geometry of the WB I-80 girders that do NOT run parallel to the roadway CL (Design 116): the north
// girders of Unit 2 (A-E) and Unit 3 (A-C). Source: structural steel layout sheets 106-114 ("P.C. STA ..., R = ...",
// GIRDER RADIUS DATA on design sheets 107A, 108, 115) and the girder data tables; see docs/plan-data/girder-geometry-i80.md.
// Run after build-i80-wb.csx, girders-i80.csx and pier-6a.csx. The plan geometry REPLACES the offset-at-supports curve of
// these girders (those table values stay in the file but are ignored for them) so the girders-i80.csx / pier-6a.csx
// bearing iterations are superseded for A-E (U2) and A-C (U3); their targets are printed below as a cross-check.
// Plan points are "STA, d' LT" of CL I-80; the WB approach roadway CL (the model alignment) is concentric, 41.08' LT.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double ft = Ft(1);
double FI(int f, double i) => f + i / 12.0;
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
var al = Doc.Alignment(u2.AlignmentName);
double R80 = Ft(2400), off80 = Ft(41.08), Rwb = R80 - off80, pc80 = Sta("7597+73.11");
double Wb(string i80) { double s = Sta(i80); return s <= pc80 ? s : pc80 + (s - pc80) * Rwb / R80; }
Vec2 W(string sta, double ltFt) => al.PointAt(Wb(sta), -(Ft(ltFt) - off80));   // CL I-80 station / feet LT of CL I-80

foreach (var b in new[] { u2, u3 }) b.Girders.PlanGeometry.Clear();

// ---------------------------------------------------------------- helpers
Vec2 PointOnBearing(BridgeDefinition b, int j, double offsetFt)    // point on support j's CL bearing line at a given offset from the CL
{
    var s = b.Supports[j]; double d = al.StationToDistance(s.Station);
    var (a, ang) = al.EvaluateAtDistance(d);
    var dir = Vec2.FromAngle(ang).Right.Rotate(s.SkewDegrees * Math.PI / 180);
    double t0 = 0, t1 = Ft(offsetFt) , f0 = al.Inverse(a).Offset - Ft(offsetFt), f1 = al.Inverse(a + dir * t1).Offset - Ft(offsetFt);
    for (int it = 0; it < 40 && Math.Abs(f1) > 1e-9; it++) { double t2 = t1 - f1 * (t1 - t0) / (f1 - f0); t0 = t1; f0 = f1; t1 = t2; f1 = al.Inverse(a + dir * t1).Offset - Ft(offsetFt); }
    return a + dir * t1;
}
Vec2 BearingDir(BridgeDefinition b, int j)
{
    var s = b.Supports[j]; var (_, ang) = al.EvaluateAtDistance(al.StationToDistance(s.Station));
    return Vec2.FromAngle(ang).Right.Rotate(s.SkewDegrees * Math.PI / 180);
}
double ArcLen(double R, Vec2 p0, Vec2 p1) => 2 * R * Math.Asin(Vec2.Distance(p0, p1) / (2 * R));
// Arc (left turning) of radius R from p0 to p1: heading at p0.
Vec2 ArcHeadingLeft(double R, Vec2 p0, Vec2 p1)
{
    double a = Math.Asin(Vec2.Distance(p0, p1) / (2 * R));
    return (p1 - p0).Normalized().Rotate(-a);
}
void Add(BridgeDefinition b, int g, Vec2 start, Vec2 heading, params (GirderPlanElementKind k, double r, double len)[] els)
{
    var so = al.Inverse(start);
    double tang = al.AngleAt(so.Station);
    double dd = (tang - Math.Atan2(heading.Y, heading.X)) * 180 / Math.PI;
    while (dd > 180) dd -= 360; while (dd < -180) dd += 360;
    b.Girders.PlanGeometry.Add(new GirderPlanGeometry
    {
        Girder = g, StartStation = so.Station, StartOffset = so.Offset, StartDirectionDegrees = dd,
        Elements = els.Select(e => new GirderPlanElement { Kind = e.k, Radius = e.r, Length = e.len }).ToList(),
    });
}
const GirderPlanElementKind L = GirderPlanElementKind.Line, AL = GirderPlanElementKind.ArcLeft;

// ---------------------------------------------------------------- Unit 2 (A-E begin on Pier 6A; positions from the pier 6A cap plan)
double skew6a = -(3 + 39 / 60.0);
var (_, ang5) = al.EvaluateAtDistance(al.StationToDistance(u2.Supports[0].Station));
var dir6 = Vec2.FromAngle(ang5).Right.Rotate(skew6a * Math.PI / 180);
var ahead6 = new Vec2(-dir6.Y, dir6.X); if (Vec2.Dot(ahead6, Vec2.FromAngle(ang5)) < 0) ahead6 = -ahead6;
var col6 = al.PointAt(Sta("7591+08.83"), -(Ft(107.43) - off80)) + dir6 * Ft(FI(13, 2.3125));
var xs = new double[5]; xs[0] = -26 + FI(2, 5.5625); xs[1] = xs[0] + FI(11, 6.5625); xs[2] = xs[1] + FI(11, 2.8125); xs[3] = xs[2] + FI(11, 6.9375); xs[4] = xs[3] + FI(9, 7.5);
var s6a = xs.Select(x => col6 + dir6 * Ft(x) + ahead6 * Ft(1.5)).ToArray();

// A: tangent to P.C. 7597+72.96, 84.45' LT; R 1749'-6 to P.C. 7601+39.20, 75.58' LT; then R 2324'-5 (concentric with the CL) to the end.
var pcA1 = W("7597+72.96", 84.45); var pcA2 = W("7601+39.20", 75.58);
double RA1 = Ft(FI(1749, 6)), RA2 = Ft(FI(2324, 5));
Add(u2, 0, s6a[0], (pcA1 - s6a[0]).Normalized(), (L, 0, Vec2.Distance(s6a[0], pcA1)), (AL, RA1, ArcLen(RA1, pcA1, pcA2)), (AL, RA2, Ft(800)));
// B: tangent to P.C. 7598+07.31, 76.58' LT, then R 1480'-6.
var pcB = W("7598+07.31", 76.58);
Add(u2, 1, s6a[1], (pcB - s6a[1]).Normalized(), (L, 0, Vec2.Distance(s6a[1], pcB)), (AL, Ft(FI(1480, 6)), Ft(600)));
// C: tangent to P.C. 7596+43.14, 75.46' LT, then R 8000'-0.
var pcC = W("7596+43.14", 75.46);
Add(u2, 2, s6a[2], (pcC - s6a[2]).Normalized(), (L, 0, Vec2.Distance(s6a[2], pcC)), (AL, Ft(8000), Ft(600)));
// D and E: straight (no P.C. on the plans) through the layout dimensions at the 90 deg piers 7WB (D: 7'-4 3/8 left of F) and 6WB (E: 6'-11 3/4 left of F).
var d7 = PointOnBearing(u2, 2, -(23 + FI(7, 4.375)));
Add(u2, 3, s6a[3], (d7 - s6a[3]).Normalized(), (L, 0, Vec2.Distance(s6a[3], d7) + Ft(300)));
var e6 = PointOnBearing(u2, 1, -(23 + FI(6, 11.75)));
Add(u2, 4, s6a[4], (e6 - s6a[4]).Normalized(), (L, 0, Vec2.Distance(s6a[4], e6) + Ft(300)));

// ---------------------------------------------------------------- Unit 3
// A: from Pier 11WB (73'-1 1/4 left of G, measured normal to the roadway) R 2268'-5 to P.C. 7606+59.36, 89.13' LT; R 1318'-5 1/4 to P.C. 7608+82.10, 81.56' LT; R 2318'-5 1/4 to the abutment.
var a0 = PointOnBearing(u3, 0, -FI(73, 1.25));
var pcA3 = W("7606+59.36", 89.13); var pcA4 = W("7608+82.10", 81.56);
double R3a = Ft(FI(2268, 5)), R3b = Ft(FI(1318, 5.25)), R3c = Ft(FI(2318, 5.25));
Add(u3, 0, a0, ArcHeadingLeft(R3a, a0, pcA3), (AL, R3a, ArcLen(R3a, a0, pcA3)), (AL, R3b, ArcLen(R3b, pcA3, pcA4)), (AL, R3c, Ft(900)));
// B and C: single arcs from Pier 11WB (59'-10 7/8 and 47'-2 5/8 left of G, measured normal to the roadway) through Pier 12WB,
// where B is 8'-5 1/4 right of... i.e. A-B = 8'-5 1/4 and B-C = 8'-7 7/8 along the pier (layout sheet 112), from the A bearing.
var rA = Doc.Regenerate();
var a12br = Doc.Model(u3.Name).Framing.Supports[1].Bearings[0]!;
var a12 = al.PointAt(a12br.Station, a12br.Offset); var dir12 = BearingDir(u3, 1);
var b12 = a12 + dir12 * Ft(FI(8, 5.25)); var c12 = b12 + dir12 * Ft(FI(8, 7.875));
var b0 = PointOnBearing(u3, 0, -FI(59, 10.875)); var c0 = PointOnBearing(u3, 0, -FI(47, 2.625));
double R3B = Ft(FI(2796, 10)), R3C = Ft(FI(2795, 0));
Add(u3, 1, b0, ArcHeadingLeft(R3B, b0, b12), (AL, R3B, ArcLen(R3B, b0, b12) + Ft(300)));
Add(u3, 2, c0, ArcHeadingLeft(R3C, c0, c12), (AL, R3C, ArcLen(R3C, c0, c12) + Ft(500)));

var rr = Doc.Regenerate();
Print($"Regenerated {rr.Bridges} bridges, {rr.Errors.Count} errors.");
foreach (var e in rr.Errors) Print("  ERROR " + e);

// ---------------------------------------------------------------- checks against the plan girder data (horizontal span lengths along each girder)
// Span index: Unit 2 0 = 5WB-6WB(from 6A, so skipped), 1 = 7WB, 2 = 8WB, 3 = 9WB, 4 = 10WB; Unit 3 0 = 12WB, 1 = 13WB, 2 = 14WB.
// The 11WB support is about 0.2 ft (Unit 3) / 0.4 ft (Unit 2) away from the plan in the model for the parallel girders as well, so the 12WB / 11WB
// spans read that much long whatever the girder geometry.
var plan = new (BridgeDefinition b, int g, int span, double ft, string what)[]
{
    (u2, 0, 1, FI(260, 3.9375), "A 7WB"), (u2, 0, 2, FI(193, 1.6875), "A 8WB"), (u2, 0, 3, FI(209, 3.375), "A 9WB"), (u2, 0, 4, FI(171, 4.6875), "A 10WB"), (u2, 0, 5, FI(144, 8.625), "A 11WB"),
    (u2, 1, 1, FI(260, 2.75), "B 7WB"), (u2, 1, 2, FI(199, 3.5), "B 8WB"), (u2, 1, 3, FI(209, 1.8125), "B 9WB"),
    (u2, 2, 1, FI(260, 2.0625), "C 7WB"), (u2, 2, 2, FI(205, 4.6875), "C 8WB"), (u2, 3, 1, FI(260, 1), "D 7WB"),
    (u3, 0, 0, FI(166, 0.75), "A 12WB"), (u3, 0, 1, FI(202, 6.9375), "A 13WB"), (u3, 0, 2, FI(197, 8.75), "A 14WB"),
    (u3, 1, 0, FI(162, 9.875), "B 12WB"), (u3, 2, 0, FI(160, 2.1875), "C 12WB"), (u3, 2, 1, FI(198, 0.6875), "C 13WB"),
    (u3, 3, 0, FI(159, 2.5), "D 12WB"), (u3, 3, 1, FI(196, 0.25), "D 13WB"),
};
Print("Span length model - plan (inches):");
foreach (var (b, g, sp, plft, what) in plan)
{
    var len = Doc.Model(b.Name).Framing.Girders[g].Spans[sp].Length / ft;
    Print($"  {b.Name[^6..]} {what,-7} model {len,9:F3} ft  plan {plft,9:F3} ft  {(len - plft) * 12,6:+0.00;-0.00} in");
}
Doc.Save();
