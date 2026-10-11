// Architectural cap section on the as-designed pier caps of WB I-80 (Design 116, PDF 83-84) and Ramp G (Design 316, PDF 78-79).
// Data and reading of the sheets: docs/plan-data/pier-cap-aesthetics.md. Re-runnable; run AFTER piers-i80.csx, pier-6a.csx and
// piers-ramp-g.csx (it needs each pier's CapProfile; it only sets SubstructureDefinition.Aesthetics).
//  * Table piers 1WB-15WB (PDF 84), 16G / 17G (PDF 79): hill R1 - reverse point - sag R2 - reverse point - fan R3/R4/R5.
//  * Explicit figures: Pier 6A (PDF 83), 19G (PDF 78), generic Ramp G 1G-15G and 18G (PDF 78): long arc + fan from the point
//    of reverse curvature (see the md: the control-point depths on the sheets are reported below as residuals).
//  * Cap section (sections A-A / B-B, docs/plan-data/piles-capstep-dowels.md item 2): full width W above the relief curve, W - 1'-0
//    below it (6" setback on BOTH faces, three 2" steps along the three fan arcs, one 6" step elsewhere, treads drafted 7 deg).
//    W = 8'-0 for Ramp G 1G-15G, 18G, 19G and I-80 5WB, 6A, 11WB; 6'-0 for 16G, 17G and the other I-80 piers.
//  * Textured face (Texture B, one face only; not geometry): Pier 6A = near (back) face; everything else = far (ahead) face -- Ramp G
//    1G-15G/18G, 16G/17G and I-80 1WB-15WB have no near/far note on their figure (the 11WB / 19G figures say far).
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;

double FI(string s)                       // "22'-6", "35'-8", "125'-0"
{
    var p = s.Split('-');
    return double.Parse(p[0].Replace("'", "")) + double.Parse(p[1].Replace("\"", "")) / 12.0;
}
double F(double ft) => Ft(ft);

// name, L1, L2, L3, L4, R1, R2, R3, R4, R5 (PDF 84 table)
var i80 = new (string name, string l1, string l2, string l3, string l4, string r1, string r2, string r3, string r4, string r5)[]
{
    ("Pier 1WB", "90'-0", "22'-6", "30'-0", "2'-6", "47'-6", "125'-0", "82'-0", "71'-0", "62'-6"),
    ("Pier 2WB", "87'-0", "21'-9", "29'-0", "2'-6", "45'-0", "116'-6", "77'-0", "66'-6", "58'-0"),
    ("Pier 3WB", "87'-0", "21'-9", "29'-0", "2'-6", "45'-0", "116'-6", "77'-0", "66'-6", "58'-0"),
    ("Pier 4WB", "63'-0", "15'-9", "21'-0", "2'-3", "25'-6", "61'-6", "42'-0", "35'-6", "30'-0"),
    ("Pier 5WB", "63'-0", "15'-9", "21'-0", "2'-3", "25'-6", "61'-6", "42'-0", "35'-6", "30'-0"),
    ("Pier 6WB", "107'-0", "26'-9", "35'-8", "2'-9", "66'-0", "176'-0", "114'-0", "98'-6", "86'-6"),
    ("Pier 7WB", "94'-0", "23'-6", "31'-4", "2'-9", "53'-6", "136'-0", "89'-0", "75'-6", "64'-6"),
    ("Pier 8WB", "115'-0", "28'-9", "38'-4", "2'-9", "75'-6", "203'-6", "129'-0", "113'-6", "101'-0"),
    ("Pier 9WB", "98'-0", "24'-6", "32'-8", "2'-9", "56'-0", "148'-6", "96'-6", "82'-6", "71'-6"),
    ("Pier 10WB", "90'-0", "22'-6", "30'-0", "2'-6", "47'-6", "125'-6", "82'-0", "71'-6", "63'-6"),
    ("Pier 11WB", "85'-0", "21'-3", "28'-4", "2'-6", "43'-0", "112'-0", "73'-6", "62'-6", "54'-0"),
    ("Pier 12WB", "112'-0", "28'-0", "37'-4", "2'-9", "72'-0", "194'-0", "124'-6", "109'-0", "96'-6"),
    ("Pier 13WB", "93'-0", "23'-3", "31'-0", "2'-9", "50'-6", "134'-0", "87'-6", "74'-0", "63'-6"),
    ("Pier 14WB", "84'-0", "21'-0", "28'-0", "2'-6", "42'-0", "109'-6", "72'-0", "62'-0", "54'-0"),
    ("Pier 15WB", "82'-0", "20'-6", "27'-4", "2'-6", "40'-0", "104'-6", "68'-6", "59'-0", "51'-0"),
};
// 16G / 17G (PDF 79): the figure is the mirror image (fan at the LEFT end, L2 = fan side).
var rampTable = new (string name, string l1, string l2, string l3, string l4, string r1, string r2, string r3, string r4, string r5)[]
{
    ("Pier 16G", "63'-0", "21'-0", "15'-9", "2'-3", "25'-0", "61'-6", "42'-0", "35'-6", "30'-0"),
    ("Pier 17G", "53'-0", "17'-8", "13'-3", "2'-0", "18'-6", "44'-0", "30'-6", "26'-0", "22'-6"),
};

// The support that owns the cap of a pier name (as-designed profile present).
(SupportDefinition sp, SubstructureDefinition sd)? Find(string name)
{
    foreach (var b in Doc.Project.Bridges)
        foreach (var s in b.Supports.Concat(b.AuxiliarySupports))
            if (s.Name == name && s.Substructure is { } d && d.CapProfile.Count >= 3) return (s, d);
    return null;
}

// Geometry of the face used: (top line origin/direction, length, left/right tip offsets) from the cap profile polygon.
// Profile order in the plan scripts: top corner left, top corner right, right tip, kink, kink, left tip.
(double l1, double tipL, double tipR) Top(List<CapProfilePoint> p)
{
    var o = new Vec2(p[0].S, p[0].Elevation); var e = new Vec2(p[1].S, p[1].Elevation);
    var t = (e - o).Normalized();
    double At(CapProfilePoint q) => Vec2.Dot(new Vec2(q.S, q.Elevation) - o, t);
    return ((e - o).Length, -At(p[5]), At(p[2]) - (e - o).Length);
}

double WidthFt(string name) => name is "Pier 5WB" or "Pier 6A" or "Pier 11WB" || (name.EndsWith("G") && name != "Pier 16G" && name != "Pier 17G") ? 8.0 : 6.0;

int done = 0;
void Apply(string name, CapFaceSide face, Func<(double l1, double tipL, double tipR), CapAestheticsDefinition> make, double? expectL1Ft = null)
{
    if (Find(name) is not { } f) { Print($"  {name}: no as-designed cap -- skipped"); return; }
    var prof = face == CapFaceSide.Ahead && f.sd.CapProfileAhead.Count >= 3 ? f.sd.CapProfileAhead : f.sd.CapProfile;
    var g = Top(prof);
    if (expectL1Ft is double ex && Math.Abs(g.l1 / Ft(1) - ex) > 0.25)
        Print($"  {name}: CONFLICT top length of the modeled cap {g.l1 / Ft(1):F2} ft vs L1 {ex:F2} ft on the aesthetic sheet");
    double w = WidthFt(name);
    if (Math.Abs(f.sd.CapWidth - F(w)) > In(0.1))
    {
        Print($"  {name}: cap width {f.sd.CapWidth / Ft(1):F2} ft in the model, {w:F2} ft on sections A-A/B-B -- set to {w:F2} ft");
        f.sd.CapWidth = F(w);
    }
    var ae = make(g);
    ae.Face = face;
    f.sd.Aesthetics = ae;
    done++;
}

foreach (var p in i80)
    Apply(p.name, CapFaceSide.Ahead, g => CapBandBuilder.FromTable(g.l1, F(FI(p.l2)), F(FI(p.l3)), F(FI(p.r1)), F(FI(p.r2)), F(FI(p.r3)), F(FI(p.r4)), F(FI(p.r5)), false), FI(p.l1));
foreach (var p in rampTable)
    Apply(p.name, CapFaceSide.Ahead, g => CapBandBuilder.FromTable(g.l1, F(FI(p.l3)), F(FI(p.l2)), F(FI(p.r1)), F(FI(p.r2)), F(FI(p.r3)), F(FI(p.r4)), F(FI(p.r5)), true), FI(p.l1));

// ---- explicit figures. Control-point depths on the sheets (perpendicular to the top) for the residual report.
double d6 = FI("6'-6") + 0.25 / 12;
// Pier 6A (PDF 83): textured NEAR face; tip 6'-9 deep; point of reverse curvature at the CL pier (5'-0), 12'-10 1/8 + 13'-0 from the
// left top corner; R = 84'-0; fan R 63'-6 / 60'-0 / 59'-6 with control points 7'-0, 10'-0, 13'-0 beyond it at 4'-6, 4'-0, 3'-6 deep.
// The band depths are measured from the LOWER half's top (1010.75 / 1012.51, the face-of-cap elevations the structural sheets dimension the beak 5'-0 / 8'-6
// from, Design 116 PDF 39 / 41) on BOTH faces: section A-A (PDF 40) ends the 5c3 hairpins hung from that top at the first step, and 5a11 (E.F.) is one bar
// for both faces (docs/plan-data/cap-cover-containment.md). Before, the back face was measured from its own (upper, +0.75 ft) top.
Apply("Pier 6A", CapFaceSide.Back, g => { var a6 = CapBandBuilder.FromLongArc(g.l1, F(FI("12'-10") + 0.125 / 12 + 13.0), F(5.0), g.tipL, F(6.75), F(84.0),
    F(63.5), F(60.0), F(59.5), false); a6.BandFromLowerTop = true; return a6; }, 52.0);
// Pier 19G (PDF 78): far face; tip 6'-6 1/4 deep, Q 5'-6 deep at 8'-3 1/2 + 9'-9 from the left top corner (the sheet's CL pier mark);
// R 60'-0; fan R 34'-0 / 28'-6 / 23'-0, control points 7'-2, 9'-1, 11'-0 beyond Q at 4'-7 1/2, 4'-3 1/2, 3'-10 1/2.
Apply("Pier 19G", CapFaceSide.Ahead, g => CapBandBuilder.FromLongArc(g.l1, F(FI("8'-3") + 0.5 / 12 + 9.75), F(5.5), g.tipL, F(d6), F(60.0),
    F(34.0), F(28.5), F(23.0), false), 40.0);
// Ramp G 1G-15G and 18G (PDF 78, second figure; fan at the left): cap 35'-0, Q at the CL pier (17'-6 from each top corner), 5'-6 deep;
// right tip 6'-6 1/4 deep, R 40'-0; fan R 29'-0 / 25'-6 / 21'-0 with control points 6'-0, 7'-9, 9'-6 left of Q at 4'-7 1/2, 4'-3 1/2, 3'-10 1/2.
foreach (int n in Enumerable.Range(1, 15).Append(18))
    Apply($"Pier {n}G", CapFaceSide.Ahead, g => CapBandBuilder.FromLongArc(g.l1, F(17.5), F(5.5), g.tipR, F(d6), F(40.0), F(29.0), F(25.5), F(21.0), true), 35.0);
Print($"  texture band set on {done} piers");

// ---- residuals of the modeled lines against the control-point depths printed on the sheets (inches)
void Residual(string name, bool mirrored, double xq, double[] dist, double[] labelFt)
{
    if (Find(name) is not { } f || f.sd.Aesthetics is not { } ae) return;
    var prof = ae.Face == CapFaceSide.Ahead && f.sd.CapProfileAhead.Count >= 3 ? f.sd.CapProfileAhead : f.sd.CapProfile;
    double l1 = Top(prof).l1;
    var lines = new[] { ae.Layers[0], ae.Layers[1], ae.Layers[2] };       // R5, R4, R3 pair with the nearest .. farthest control point
    var sb = new System.Text.StringBuilder($"  {name} control-point residuals (model - sheet, in):");
    for (int k = 0; k < 3; k++)
    {
        double x = mirrored ? l1 - (F(xq) + F(dist[k])) : F(xq) + F(dist[k]);
        sb.Append($" {CapBandBuilder.DepthAt(lines[k], x) / In(1) - labelFt[k] * 12:+0.0;-0.0}");
    }
    Print(sb.ToString());
}
Residual("Pier 6A", false, FI("12'-10") + 0.125 / 12 + 13.0, new[] { 7.0, 10.0, 13.0 }, new[] { 4.5, 4.0, 3.5 });
Residual("Pier 19G", false, FI("8'-3") + 0.5 / 12 + 9.75, new[] { 7.0 + 2 / 12.0, 9.0 + 1 / 12.0, 11.0 }, new[] { FI("4'-7") + 0.5 / 12, FI("4'-3") + 0.5 / 12, FI("3'-10") + 0.5 / 12 });
Residual("Pier 1G", true, 17.5, new[] { 6.0, 7.75, 9.5 }, new[] { FI("4'-7") + 0.5 / 12, FI("4'-3") + 0.5 / 12, FI("3'-10") + 0.5 / 12 });

var rr = Doc.Regenerate();
Print($"Regenerated {rr.Bridges} bridges in {rr.Elapsed.TotalSeconds:F1} s, {rr.Errors.Count} errors.");
foreach (var e in rr.Errors) Print("  ERROR " + e);
foreach (var b in Doc.Project.Bridges)
{
    var sm = Doc.Model(b.Name).Substructure;
    if (sm == null) continue;
    foreach (var d in sm.Diagnostics.Where(d => d.Message.Contains("cap section step") || d.Message.Contains("relief curve"))) Print("  WARNING " + d.Message);
}
Doc.Save();
