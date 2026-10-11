// North deck edges of WB I-80 Units 2 and 3 as designed: straight tapers and circular (compound) curves, from the
// slab plans (design sheets 179-181 for Unit 2, 185-186 for Unit 3). Offsets on the sheets are LT of CL I-80; the model
// alignment is CL WB approach roadway, 41.08' LT of CL I-80 and concentric with it on the I-80 curve.
// Run after build-i80-wb.csx (any time; works before or after geolocate-cbis.csx since it goes through the alignment).
// Every P.C. / P.C.C. is checked for a tangent break: the plans' curves are tangent there, so a break means a misread.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double R80 = Ft(2400), offWb = Ft(41.08), Rwb = R80 - offWb;
double pc80 = Sta("7597+73.11");
double Wb(string i80) { double s = Sta(i80); return s <= pc80 ? s : pc80 + (s - pc80) * Rwb / R80; }

var al = Doc.Alignment("I-80 WB");
// Vertex: I-80 station, offset LT of CL I-80 (ft); Radius of the segment that ENDS at this vertex (0 = straight;
// arcs are centred to the north = left of up-station travel, inside the I-80 curve).
// I-80 station of a WB station (for messages in plan stationing).
double I80(double wb) => wb <= pc80 ? wb : pc80 + (wb - pc80) * R80 / Rwb;

// backTo/backRadiusFt: prepend a curve of that radius ending tangent at the first vertex (compound curve), run back to
// the station backTo (used where the plans give the P.C.C. and radius but the slab end is a skewed corner).
List<DeckEdgePoint> Edge(string name, (double sta, double ltFt, double radiusFt)[] v, double backTo = double.NaN, double backRadiusFt = 0)
{
    Vec2 P(double sta, double lt) => al.PointAt(sta, -(Ft(lt) - offWb));
    var pts = v.Select(x => P(x.sta, x.ltFt)).ToArray();
    var poly = new List<Vec2> { pts[0] };
    var tanIn = new List<Vec2>(); var tanOut = new List<Vec2>();
    for (int i = 1; i < pts.Length; i++)
    {
        Vec2 a = pts[i - 1], b = pts[i], d = b - a;
        double R = Ft(v[i].radiusFt);
        if (R <= 0)
        {
            var t = d * (1 / d.Length);
            tanOut.Add(t); tanIn.Add(t);
            poly.Add(b);
            continue;
        }
        double c = d.Length;
        if (c > 2 * R) throw new Exception($"{name}: chord {c / Ft(1):F1} ft exceeds the diameter of R = {v[i].radiusFt} ft.");
        var dn = d * (1 / c);
        var left = new Vec2(-dn.Y, dn.X);
        var ctr = (a + b) * 0.5 + left * Math.Sqrt(R * R - c * c / 4);
        double a0 = Math.Atan2(a.Y - ctr.Y, a.X - ctr.X), a1 = Math.Atan2(b.Y - ctr.Y, b.X - ctr.X);
        double sweep = a1 - a0;
        while (sweep > Math.PI) sweep -= 2 * Math.PI;
        while (sweep < -Math.PI) sweep += 2 * Math.PI;
        int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) * R / Ft(5)));    // ~5 ft chords: < 0.002 ft sag
        for (int k = 1; k <= n; k++) poly.Add(ctr + Vec2.FromAngle(a0 + sweep * k / n) * R);
        // Tangents at both ends (travel direction a -> b; the center is on the left, so the arc turns left).
        Vec2 Tan(double ang) { var r = Vec2.FromAngle(ang); return new Vec2(-r.Y, r.X) * Math.Sign(sweep); }
        tanOut.Add(Tan(a0)); tanIn.Add(Tan(a1));
    }
    for (int i = 1; i + 1 < pts.Length; i++)
    {
        var t0 = tanIn[i - 1]; var t1 = tanOut[i];
        double kink = Math.Atan2(t0.X * t1.Y - t0.Y * t1.X, t0.X * t1.X + t0.Y * t1.Y) * 180 / Math.PI;
        Print($"{name}: tangent break at STA {Doc.Units.FormatStation(I80(v[i].sta))} = {kink:F3} deg");
    }
    if (!double.IsNaN(backTo))
    {
        // Curve centred on the left of the first outgoing tangent, walked backward from the first vertex.
        double R = Ft(backRadiusFt);
        var t = tanOut[0];
        var ctr = pts[0] + new Vec2(-t.Y, t.X) * R;
        double a0 = Math.Atan2(pts[0].Y - ctr.Y, pts[0].X - ctr.X);
        var back = new List<Vec2>();
        for (int k = 1; k < 2000; k++)
        {
            var q = ctr + Vec2.FromAngle(a0 - k * Ft(5) / R) * R;    // backward = clockwise for a left-turning curve
            back.Add(q);
            if (al.Inverse(q).Station <= backTo) break;
        }
        back.Reverse();
        poly.InsertRange(0, back);
        var so = al.Inverse(back[0]);
        Print($"{name}: R = {backRadiusFt:F2}' curve run back from the P.C.C. reaches STA {Doc.Units.FormatStation(I80(so.Station))} at {(-so.Offset + offWb) / Ft(1):F2}' LT");
    }
    return poly.Select(p => { var so = al.Inverse(p); return new DeckEdgePoint { Station = so.Station, Offset = so.Offset }; }).ToList();
}

var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
double p11 = u2.Supports[^1].Station, sE = u3.Supports[^1].Station;

// Unit 2 (sheets 179-181): begin linear slab taper 7591+50.68, 118.65' LT; P.C. and end of taper 7597+73.11, 87.53' LT;
// R = 1746'-5 to P.C.C. 7601+39.20, 78.67' LT; R = 2321'-4 to Pier 11WB (75'-2 out to out = 37'-7 LT of CL WB).
u2.Deck.LeftEdgeTable = Edge("Unit 2", new[]
{
    // From the 6A end of slab to 7591+50.68 the north edge is 13'-7 left of CL Ramp A2 (sheet 179 / PDF 186; Ramp A2 CL
    // 7591+08.83, 107.43' LT per sheet 190 / PDF 197) -> 121.00' LT at the 6A line, not 118.65 held back to 6A (that put
    // girder A inside the level barrier strip; docs/plan-data/gore-6a-edge.md).
    (Wb("7591+09.60"), 121.00, 0.0),
    (Wb("7591+50.68"), 118.65, 0.0),
    (Wb("7597+73.11"), 87.53, 0.0),
    (Wb("7601+39.20"), 78.67, 1746 + 5 / 12.0),
    (p11, 41.08 + 37 + 7 / 12.0, 2321 + 4 / 12.0),
});
// Unit 3 (sheets 185-186): slab corner at Pier 11WB & 19G 7602+91.16, 117.11' LT (detail B, sheet 191); R = 2350'-0 to
// P.C.C. 7607+18.69, 88.74' LT; R = 1315'-4 to P.C.C. 7608+82.10, 84.67' LT; R = 2315'-4 to the east abutment
// (81'-2 out to out = 43'-7 LT of CL WB).
// Through the Detail B corner (sheet 191). Check: this curve gives 112.67' LT at 7603+60.00, vs. 111.43' LT gutter
// + 1'-7 = 113.01' LT on sheet 191 (0.34 ft) -- but it meets the R = 1315'-4 curve at the P.C.C. with a ~1.1 deg break,
// Checked against the north-edge lengths on sheets 185-186: corner -> 2nd P.C.C. = 567.77 ft here vs 567.73 ft on the plan,
// so the points and radii are as drawn and the ~1.1 deg break is in the plan geometry itself (tangency would need the
// P.C.C. at 90.93' LT or R = 1928.5'). Modeled per plan.
// (Running the R = 2350'-0 curve back tangent from the P.C.C. instead misses the corner by 7.8 ft.)
u3.Deck.LeftEdgeTable = Edge("Unit 3", new[]
{
    (Wb("7602+91.16"), 117.11, 0.0),
    (Wb("7607+18.69"), 88.74, 2350.0),
    (Wb("7608+82.10"), 84.67, 1315 + 4 / 12.0),
    (sE, 41.08 + 43 + 7 / 12.0, 2315 + 4 / 12.0),
});
Print($"Unit 2 edge table: {u2.Deck.LeftEdgeTable.Count} points; Unit 3: {u3.Deck.LeftEdgeTable.Count} points.");
var r = Doc.Regenerate();
Print($"Regenerated {r.Bridges} bridges in {r.Elapsed.TotalSeconds:F1} s, {r.Errors.Count} errors.");
Doc.Save();
