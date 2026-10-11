// Kinked ends of slab (SupportDefinition.DeckEndPoints) from the slab plans.  Re-runnable; run after joints-i80.csx / joints-ramp-g.csx, pier-6a.csx and
// deck-edges-i80.csx (it needs the Pier 6A support line and the deck-end offsets), and BEFORE deck-rebar-i80.csx / deck-rebar-ramp-g.csx.
//   sbm run samples\plans\deck-end-kinks.csx "temp\model.sbm"
//
// I-80 WB Unit 2, begin at Pier 5WB (design sheet 179 = PDF 186, DETAIL A; fractions read on 400-600 dpi clips):
//  * South of the kink the end of slab is 4 1/2" @ 50 F ahead of CL Pier 5WB (the support's DeckEndOffset 1'-1 1/2 behind the CL bearing);
//    north of it, 4 1/2" @ 50 F ahead of CL Pier 6A (= 1'-1 1/2 behind the CL bearing of Pier 6A, which is 1'-6 ahead of its CL pier, framing plan PDF 108).
//  * Two SLAB KINK POINTS joined by a short straight piece: the south one 25'-7 from CL westbound approach roadway, the north one 40'-3 3/4 from
//    CL Ramp A2, both "MEASURED ALONG END OF SLAB".  CL Ramp A2 crosses CL Pier 6A at I-80 STA 7591+08.83, 107.43' LT (PDF 108; = 66.35 ft left of the
//    WB alignment, see pier-6a.csx) and is radial to the pier ("90 00'00" MEASURED TO LOCAL TANGENT", Pier 6A cap plan PDF 39), so it crosses the end of
//    slab 4 1/2" ahead of that point, normal to the pier line.
// Ramp G Unit 4, end at Pier 19G (design sheet 139 = PDF 141, DETAILS A and B; Unit 4 slab plan sheet 138 = PDF 140):
//  * Detail A (right corner): the right edge (R = 1577'-7 concentric, 27'-7 RT) ends its curve at STA 67502+99.14, 27.58' RT and runs STRAIGHT to
//    STA 67503+14.94, 28.35' RT; from there a 5" step to STA 67503+14.97, 27.93' RT on the end of slab (4 1/2" @ 50 F from CL Pier 19G).
//    Modeled: right edge table along that straight line (points every foot, the table is linear in station), the step from the printed corner point to
//    the end-of-slab line at 28.35' - 5" (its station is printed for comparison).
//  * Detail B (left corner): a 5" step between the left edge and the end of slab, drawn perpendicular to the edge; the step is not coordinated: it is
//    modeled perpendicular (radial) to the edge as drawn - READING, see docs\plan-data\deck-end-blocks.md section 5.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double FI(int ft, double inch) => Ft(ft) + In(inch);
string S(double sta) => Doc.Units.FormatStation(sta);

// ------------------------------------------------------------------ I-80 WB Unit 2 at Pier 5WB / 6A
{
    var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
    var al = Doc.Alignment(u2.AlignmentName);
    u2.Supports[0].DeckEndPoints = null;                      // straight first, to read the plain lines
    var r0 = Doc.Regenerate();
    foreach (var e in r0.Errors) Print("  ERROR " + e);
    var f = Doc.Model(u2.Name).Framing;
    var dr = f.Deck!;
    // 5WB end of slab: the straight begin line; where it crosses the WB alignment (offset 0)
    var p5 = dr.BeginLinePoint; var t5 = dr.BeginLineDirection;  // left -> right
    double Off(Vec2 q) => al.Inverse(q).Offset;
    double u0 = 0, u1 = 1, g0 = Off(p5), g1 = Off(p5 + t5 * u1);
    for (int it = 0; it < 40 && Math.Abs(g1) > 1e-9 && g1 != g0; it++) { double u = u1 - g1 * (u1 - u0) / (g1 - g0); u0 = u1; g0 = g1; u1 = u; g1 = Off(p5 + t5 * u1); }
    var c5 = p5 + t5 * u1;
    var k1 = c5 - t5 * FI(25, 7);                               // 25'-7 left of CL WB approach roadway, along the end of slab
    // 6A end of slab: CL bearing line of the auxiliary support, moved 1'-1 1/2 back (4 1/2" ahead of CL pier)
    var aux = u2.AuxiliarySupports.First(s => s.Name == "Pier 6A");
    var l6 = f.SupportLine(u2.Supports.Count + u2.AuxiliarySupports.IndexOf(aux));
    var t6 = l6.Direction; var ahead6 = t6.Left;
    if (Vec2.Dot(ahead6, Vec2.FromAngle(al.AngleAt(aux.Station))) < 0) ahead6 = -ahead6;
    var e6 = l6.AlignmentPoint - ahead6 * FI(1, 1.5);
    var rampPt = al.PointAt(Sta("7591+08.83"), -(Ft(107.43) - Ft(41.08)));   // CL Pier 6A x CL Ramp A2
    double offLine = Vec2.Dot(rampPt - (l6.AlignmentPoint - ahead6 * Ft(1.5)), ahead6);
    var a2 = rampPt + ahead6 * In(4.5);                        // CL Ramp A2 (radial to the pier) x end of slab
    var k2 = a2 + t6 * FI(40, 3.75);                            // 40'-3 3/4 from CL Ramp A2, along the end of slab
    double k2OffLine = Vec2.Dot(k2 - e6, ahead6);
    var pts = new[] { k2 - t6 * Ft(120), k2, k1, k1 + t5 * Ft(160) };
    u2.Supports[0].DeckEndPoints = pts.Select(q => { var so = al.Inverse(q); return new DeckEndPoint(so.Station, so.Offset); }).ToList();
    var so1 = al.Inverse(k1); var so2 = al.Inverse(k2);
    Print($"I-80 WB Unit 2 begin (Pier 5WB / 6A):");
    Print($"  CL Pier 6A from the Ramp A2 point: {offLine / In(1):F3} in (should be 0); K2 is {k2OffLine / In(1):F3} in off the 6A end-of-slab line (should be 0)");
    Print($"  south kink K1 STA {S(so1.Station)}, {-so1.Offset / Ft(1):F2}' LT;  north kink K2 STA {S(so2.Station)}, {-so2.Offset / Ft(1):F2}' LT;  K1-K2 {Vec2.Distance(k1, k2) / In(1):F2} in");
    Print($"  angle between the 5WB and 6A end lines {Math.Acos(Math.Clamp(Vec2.Dot(t5, t6), -1, 1)) * 180 / Math.PI:F4} deg; the 6A line is {Vec2.Dot(k1 - e6, ahead6) / In(1):F2} in ahead of K1");
}

// ------------------------------------------------------------------ Ramp G Unit 4 at Pier 19G
{
    var u4 = Doc.Project.Bridges.First(b => b.Name == "Ramp G Unit 4");
    var al = Doc.Alignment(u4.AlignmentName);
    var d = u4.Deck;
    double rOff = d.RightEdgeOffset, lOff = d.LeftEdgeOffset;   // 27'-7 RT, 7'-7 LT (constant)
    u4.Supports[^1].DeckEndPoints = null;
    d.RightEdgeTable = null;
    var r0 = Doc.Regenerate();
    foreach (var e in r0.Errors) Print("  ERROR " + e);
    var dr = Doc.Model(u4.Name).Framing.Deck!;
    var pe = dr.EndLinePoint; var te = dr.EndLineDirection;     // straight end of slab, left -> right
    // where the straight end line crosses an offset
    Vec2 OnEnd(double off)
    {
        double Fo(double u) => al.Inverse(pe + te * u).Offset - off;
        double u0 = 0, u1 = 1, g0 = Fo(u0), g1 = Fo(u1);
        for (int it = 0; it < 40 && Math.Abs(g1) > 1e-9 && g1 != g0; it++) { double u = u1 - g1 * (u1 - u0) / (g1 - g0); u0 = u1; g0 = g1; u1 = u; g1 = Fo(u1); }
        return pe + te * u1;
    }
    // Detail A: straight right edge from the end of the R = 1577'-7 curve to the corner, then the 5" step onto the end of slab
    var p1 = al.PointAt(Sta("67502+99.14"), rOff);
    var p2 = al.PointAt(Sta("67503+14.94"), Ft(28.35));
    var p3 = OnEnd(Ft(28.35) - In(5));
    var tab = new List<DeckEdgePoint>();
    double len = Vec2.Distance(p1, p2);
    int n = Math.Max(2, (int)Math.Ceiling(len / Ft(1)));
    for (int i = 0; i <= n; i++) { var so = al.Inverse(Vec2.Lerp(p1, p2, (double)i / n)); tab.Add(new DeckEdgePoint { Station = so.Station, Offset = so.Offset }); }
    tab[0].Offset = rOff;
    d.RightEdgeTable = tab;
    // Detail B: 5" step perpendicular to the left edge
    var q = OnEnd(lOff + In(5));
    var qs = al.Inverse(q);
    var lc = al.PointAtDistance(qs.Distance, lOff);
    u4.Supports[^1].DeckEndPoints = new[] { lc, q, p3, p2 }.Select(x => { var so = al.Inverse(x); return new DeckEndPoint(so.Station, so.Offset); }).ToList();
    var s3 = al.Inverse(p3); var sq = al.Inverse(q);
    Print($"Ramp G Unit 4 end (Pier 19G):");
    Print($"  right edge: straight from STA {S(Sta("67502+99.14"))} ({rOff / Ft(1):F4}') to STA {S(Sta("67503+14.94"))} (28.35'), {len / Ft(1):F3} ft, {tab.Count} table rows");
    Print($"  Detail A step: end-of-slab point at 28.35' - 5\" = {s3.Offset / Ft(1):F3}' RT is at STA {S(s3.Station)} (plan STA 67503+14.97, 27.93' RT); step {Vec2.Distance(p2, p3) / In(1):F2} in");
    Print($"  Detail B step: STA {S(sq.Station)}, {-sq.Offset / Ft(1):F3}' LT to the left edge (radial, 5\")");
}

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var name in new[] { "I-80 WB Unit 2", "Ramp G Unit 4" })
{
    var b = Doc.Project.Bridges.First(x => x.Name == name);
    var al = Doc.Alignment(b.AlignmentName);
    var fr = Doc.Model(name).Framing;
    var o = fr.Deck!.Outline;
    Print($"== {name}: deck area {fr.Deck.Area / (Ft(1) * Ft(1)):N1} sf; outline corners:");
    foreach (var i in new[] { 0, o.Count - 2 })
    {
        var so = al.Inverse(o[i].XY);
        Print($"   STA {S(so.Station)}, {so.Offset / Ft(1):F3}'");
    }
    foreach (var dg in fr.Diagnostics.Where(x => x.Message.Contains("Deck end points"))) Print($"   {dg.Severity}: {dg.Message}");
    var cm = Doc.Concrete(name);
    foreach (var dg in cm.Diagnostics.Where(x => x.Message.Contains("Kinked deck end"))) Print($"   {dg.Severity}: {dg.Message}");
    foreach (var j in cm.Joints.Where(j => j.EndBlock != null)) Print($"   {j.Support}: blockout {j.BlockoutVolume / 0.764554857984:F2} CY, end block {j.EndBlock!.Volume / 0.764554857984:F2} CY");
}
Doc.Save();
