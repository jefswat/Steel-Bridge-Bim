// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - REMAINING VISIBLE DETAILS (step 5, 2026-10-21; TRUSS-DESIGN.md section 20):
//   1. sway frame gusset plates + hanger bracing connection angles (approach p.56 / p.48 / p.58, main p.97 L7 "L8 similar")
//   (further sections are appended below: portal knee braces, floor lateral connections, floor beam ends, stringer splices / tension plates)
// Re-runnable (replaces its own inputs); run AFTER bracing-ptc and BEFORE rivets-ptc:
//   sbm run samples\plans\details-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Every scaled value is a dimension-only flag (page + reason); values the sheets do not give are UNRESOLVED flags; nothing is adjusted.
// Dev options: env DETAILS_ONLY=<bridge name part> keeps only those bridges (nothing saved unless DETAILS_OUT), DETAILS_CLASH=1 prints the clash summary.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Steel;
using SBM.Core.Truss;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
string? only = Environment.GetEnvironmentVariable("DETAILS_ONLY");
bool dev = !string.IsNullOrEmpty(only);
if (dev) Doc.Project.Bridges.RemoveAll(b => !b.Name.Contains(only!, StringComparison.OrdinalIgnoreCase));
var report = new List<string>();
TrussRivetColumn Col(double gageIn, int n, double pitchIn, double firstIn) => new TrussRivetColumn(In(gageIn), n, In(pitchIn), In(firstIn));
TrussSectionDefinition Clone(TrussSectionDefinition s) => System.Text.Json.JsonSerializer.Deserialize<TrussSectionDefinition>(System.Text.Json.JsonSerializer.Serialize(s))!;

var approach = new[] { "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27" };
const string Main = "PTC Arch truss 18-20";
TrussDefinition? T(string n) => Doc.Project.Bridges.FirstOrDefault(b => b.Name == n)?.Truss;
void Flag(TrussDefinition t, string key, string? text) { t.Unresolved.RemoveAll(s => s.Contains(key)); if (text != null) t.Unresolved.Add(text); }

// ================================================================ 1. sway frame gusset plates and hanger bracing connections
// p.56 (Lo section under the bottom strut at the CL): "Sway bracing gusset plates" = two vertical plates in the transverse plane, one on each face of the sway
// diagonals, standing on the strut top flange on "Ls 4 x 3 1/2 x 7/16"; p.58 box strut section: "Sway bracing gus. Pls." on the box strut the same way.
// The diagonals are "14 BP 73 H" and the hanger bracing "10 WF 33 H" (p.48 / p.58 sway sketches): the two plates lie on the FLANGES, so the shapes are turned
// with the web normal to the sway plane (the plates are 13.6 in. apart = the HP depth). Rivets per end: 24 (sway), 4 (hanger bracing) (p.48 / p.58).
// Top ends: "Bk. Ls on Fl. Bm." (bracket angles on the floor beam) - not detailed anywhere: not built (flag).
// p.56 L1 plan: "10 WF 33 at panel points without sway bracing, Ls 5 x 4 x 3/8" = connection angles on the hanger bracing web at the bottom strut.
string swayRotFlag = U("p.48, p.58, p.56", "orientation of the 14 BP 73 H sway diagonals and 10 WF 33 H hanger bracing",
    "the sketches mark the shapes 'H' without a section; the p.56 / p.58 sections draw the two sway gusset plates on the member flanges",
    "the shapes are turned 90 degrees (web normal to the sway plane, flanges on the plates).");
string swayPlFlag = U("p.56, p.58, p.48", "sway gusset plates at the bottom strut (thickness, outline, edge angle rivets)",
    "the sections draw two plates on the strut with Ls 4 x 3 1/2 x 7/16 but give no plate thickness, outline or rivet layout; p.48 / p.58 give 24 rivets per sway end",
    "7/16 plates (the thickness of the lateral plates on the same sheet), envelope of the member laps (1'-6, 1 1/2 edge) stopping 1/4 in. above the strut, 12 rivets per diagonal per plate (24 per end) in 4 lines 2 3/4 / 5 1/2 in. from the member CL at 3 in.; edge angle along the strut on the plate's outer face, 4 rivets per leg.");
string swayTopFlag = U("p.48, p.58", "sway diagonal top connection ('Bk. Ls on Fl. Bm.' = bracket angles on the floor beam)",
    "the sway sketches name the bracket angles only; no sheet details the top connection",
    "not built; the diagonals stop 1/2 in. clear of the floor beam and the truss joint.");
string hbFlag = U("p.56, p.48, p.58", "hanger bracing 10 WF 33 connection angles (Ls 5 x 4 x 3/8) and rivets",
    "p.56 draws the angles at the bottom strut in plan only (no length, gage or outstanding leg); the top 'Bk. Ls on Floor Beams' are not drawn; p.48 / p.58 give 4 rivets per end",
    "2 angles L 5 x 4 x 3/8 (4 in. leg outstanding) at each end, 6 1/2 in. long between the flanges, back on the face probed beyond the cut (strut top / floor beam bottom), 4 web rivets (2 x 2) + 2 per outstanding leg, 7/8 in. carbon.");
foreach (var bn in approach)
{
    var t = T(bn); if (t == null) continue;
    Flag(t, "orientation of the 14 BP 73 H sway diagonals", swayRotFlag);
    Flag(t, "sway frame / hanger bracing connection plates", null);   // bracing-ptc.csx flag: now built
    Flag(t, "sway diagonal top connection", swayTopFlag);
    foreach (var (src, dst) in new[] { ("HP14x73", "HP14x73-H"), ("W10x33", "W10x33-H") })
    {
        t.Sections.RemoveAll(s => s.Name == dst);
        var s0 = t.Section(src)!; var s1 = Clone(s0);
        s1.Name = dst; s1.Rotated = true; s1.Label = s0.Label + " H";
        t.Sections.Add(s1);
    }
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.Sway)) m.Section = "HP14x73-H";
    var hbs = t.Members.Where(m => m.System == "Hanger bracing").ToList();
    foreach (var m in hbs)
    {
        m.Section = "W10x33-H";
        m.EndConnections = new()
        {
            new TrussEndConnection { End = TrussMemberEnd.Both, Label = "2-Ls 5x4x3/8", Angle = "L4x5x3/8", Top = In(3.25), Length = In(6.5),
                WebRivets = new() { Col(1.75, 2, 3, 1.75), Col(3.75, 2, 3, 1.75) }, OutRivets = new() { Col(2.25, 2, 3, 1.75) },
                RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet", Material = "C", Source = "p.56", Unresolved = hbFlag },
        };
    }
    t.BracingGussets ??= new();
    t.BracingGussets.RemoveAll(b => b.Label == "sway gus. PL");
    var lm = t.Members.Where(m => m.Role == TrussMemberRole.Sway).Select(m => m.To).Where(p => p.Contains(":LM")).Select(p => "N:" + p[(p.IndexOf(':') + 1)..]).Distinct().ToList();
    foreach (var spec in lm)
        t.BracingGussets.Add(new TrussBracingGussetDefinition
        {
            Point = spec, Label = "sway gus. PL", Orientation = TrussBracingPlateOrientation.Transverse, Side = TrussBracingPlateSide.Both, Thickness = In(7.0 / 16),
            EdgeAngle = "L4x3-1/2x7/16", EdgeAngleOnCrossing = true, EdgeAngleOutside = true, EdgeRivets = 4,
            RivetsPerMember = 12, RivetPitch = In(3), RivetGage = In(5.5), RivetGage2 = In(2.75), RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet",
            Material = "C", Unresolved = swayPlFlag, Source = "p.56 / p.58",
        });
    report.Add($"{bn}: {lm.Count} sway plate pairs, {hbs.Count} hanger bracing members with end angles");
}

// p.97 "SWAY BRACING AT L7 AS SHOWN, SWAY BRACING AT L8 SIMILAR": at the CL apex "2-1/2 Pls." on the channel webs of the 2-15[ 33.9 diagonals, "2-Ls 3 1/2 x 3 x 3/8"
// along the top edge (field rivets) and "2-Ls 4 x 4 x 3/4" along the strut; ~12 shop rivets per diagonal per plate drawn. Other sway points: not detailed (flag kept).
string mainSwayFlag = U("p.97", "main span sway gusset plates at L7 / L8 (2-1/2 Pls., 2-Ls 3 1/2 x 3 x 3/8)",
    "the detail draws the plates, angles and rivets without outline dimensions or gages",
    "1/2 in. plates on both channel webs = envelope of the member laps clipped 1/4 in. clear of the floor beam and the strut, 12 rivets per diagonal per plate in 4 lines (5 1/2 / 2 1/2 in. from the member CL, 3 in. pitch); edge angle L 3 1/2 x 3 x 3/8 on the plate's outer face along the floor beam bottom, 10 rivets per leg; the strut's 4 x 4 x 3/4 angles are part of the strut section.");
if (T(Main) is { } tmS)
{
    var t = tmS;
    Flag(t, "main span sway gusset plates at L7 / L8", null);
    t.Unresolved.RemoveAll(s => s.Contains("main span sway frame connections"));
    t.Unresolved.Add(U("p.97, p.71", "main span sway frame connections other than L7 / L8 (2-15[ 33.9 / 2-18[ 42.7 at L11)",
        "p.97 details the sway connection at L7 only ('L8 similar'); the other points are not drawn",
        "not built; the sway members stop 1/2 in. clear of the strut, the laterals and the floor beam."));
    t.BracingGussets ??= new();
    t.BracingGussets.RemoveAll(b => b.Label == "2-1/2 Pls. (sway)");
    int n = 0;
    foreach (var p in new[] { "SW7", "SW8", "SW7'", "SW8'" })
    {
        if (!t.Members.Any(m => m.Role == TrussMemberRole.Sway && (m.To == "N:" + p || m.From == "N:" + p))) continue;
        t.BracingGussets.Add(new TrussBracingGussetDefinition
        {
            Point = "N:" + p, Label = "2-1/2 Pls. (sway)", Orientation = TrussBracingPlateOrientation.Transverse, Side = TrussBracingPlateSide.Both, Thickness = In(0.5),
            EdgeAngle = "L3-1/2x3x3/8", EdgeAngleOnCrossing = true, EdgeAngleOutside = true, EdgeRivets = 10,
            RivetsPerMember = 12, RivetPitch = In(3), RivetGage = In(5.5), RivetGage2 = In(2.5), RivetDiameter = In(1), RivetMaterial = "1 in carbon steel rivet",
            Material = "C", Unresolved = mainSwayFlag, Source = "p.97",
        });
        n++;
    }
    t.Unresolved.Add(mainSwayFlag);
    report.Add($"{Main}: {n} sway plate pairs (L7 / L8)");
}

// ================================================================ 2. portal knee braces at U11 / U11' (p.100)
// Dimensioned: knee working line meets the post CL 20'-1 1/8 below U11; straight part = box 2'-11 3/4 b.b. Ls (in the portal plane) x 2'-4 1/2 b.b. Ls, 2-Pls (34 1/2-14) x 1/2
// (the side plates, manholes 14 x 20 @ 4'-0), 2-Pls 28 x 1/2 (covers), 4-Ls 6 x 6 x 1/2; curved part "R = 20'-0 Back of Ls"; Section C-C developed 2'-3 + 4'-3 1/2 + 2'-3 from the
// splice to the CL bridge; portal strut working line through U11 (Section D-D), strut 2'-10 1/2 deep. The sheet is drawn with breaks in the post, the strut and the knee,
// so nothing can be scaled along them. Construction (all from the dimensions above + two readings, flagged): the knee is symmetric about the CL bridge with its
// centreline horizontal there and its top cover on the strut bottom; R = 20'-0 at the back of the TOP angles (the label points at the upper edge) -> centreline
// radius 18'-6 1/8; the straight part is the line through the 20'-1 1/8 point tangent to that arc; its end at the post stops 1/2 in. clear of the knee gussets
// (2'-5 in to in + 3/4 gussets). The curved part is modeled with the straight-part box (Section E-E / B-B: web 36 x 1/2 + L 6 x 6 x 1/2 + 5/8 gusset not built).
string kneeFlag = U("p.100", "portal knee braces at U11 (straight box + R 20'-0 curve)",
    "the portal sheet is drawn with breaks in the post, strut and knee: only the 20'-1 1/8 drop of the knee working line, R = 20'-0 (back of Ls), the box section and the developed lengths in Section C-C are dimensioned",
    "knee symmetric about the CL bridge, centreline horizontal there with the top cover on the strut bottom, R = 20'-0 at the back of the top angles, straight part tangent through the 20'-1 1/8 point, ending 1/2 in. clear of the post knee gussets; the curved part keeps the straight box section; manholes, splice, knee gussets (2-3/4, 5/8) and rivets not built.");
if (T(Main) is { } tmK)
{
    var t = tmK;
    Flag(t, "portal at U11: portal strut", U("p.100, p.79", "portal strut at U11 (box 34 1/2 deep, Section D-D plates) and the knee brace connections",
        "Section D-D lists the strut plates (34 1/2 x 11/16, 24 1/2 x 7/16, (33-13 1/2) x 11/16, (35 1/2-13 1/2) x 9/16, 2-6 x 1/2 bars) around a skewed W.P. outline; the knee gussets / splice are drawn without outline dimensions",
        "the strut stays the nominal envelope between the U11 points; knee gussets, splice and rivets not built."));
    Flag(t, "portal knee braces at U11", kneeFlag);
    t.PlaneItems ??= new();
    t.PlaneItems.RemoveAll(p => p.Name.StartsWith("PK"));
    double h = In(18.375), zQ = -(Ft(20) + In(1.125)), zc = -In(17.25) - h, Rc = Ft(20) - In(17.875);
    foreach (var pr in new[] { "", "'" })
    {
        if (!t.Points.Any(p => p.Name == "U11" + pr)) { report.Add($"portal U11{pr}: point not found"); continue; }
        double Lh = Math.Abs(t.Planes.First(p => p.Name == "S").Offset - t.Planes.First(p => p.Name == "N").Offset) / 2;   // planes plumb, U11 points level across
        double dyp = (zc - Rc) - zQ;
        double F(double th) => Lh * Math.Sin(th) - dyp * Math.Cos(th) - Rc;
        double lo = 0, hi = Math.PI / 3;
        for (int k = 0; k < 80; k++) { double mid = (lo + hi) / 2; if (F(mid) > 0) hi = mid; else lo = mid; }
        double th = (lo + hi) / 2, uV = (zc - zQ) / Math.Tan(th), us = In(15.25) + In(0.5) + h * Math.Sin(th);
        var path = new List<TrussPathVertex> { new(us, zQ + us * Math.Tan(th)), new(uV, zc, Rc), new(Lh, zc) };
        double arc = Rc * th;
        report.Add($"portal U11{pr}: knee slope {th * 180 / Math.PI:0.00} deg, straight part {((uV - Rc * Math.Tan(th / 2)) - us) / Math.Cos(th) / 0.3048:0.00} ft, arc on the centreline {arc / 0.0254:0.0} in (C-C developed splice -> CL 105.5 in incl. the straight run past the splice)");
        void Item(string name, string label, params (double w, double v)[] poly) => t.PlaneItems.Add(new TrussPlaneItemDefinition
        {
            Name = $"PK{pr}-{name}", Label = $"portal knee brace U11{pr}: {label}", PointA = "N:U11" + pr, PointB = "S:U11" + pr, Kind = TrussPlaneItemKind.Sweep, Path = path,
            Section = poly.Select(q => new TrussUV(q.w, q.v)).ToList(), ArcStepDegrees = 3, Mirror = true, Role = TrussMemberRole.Portal, Panel = $"Portal U11{pr}",
            Material = "C", Unresolved = kneeFlag, Source = "p.100",
        });
        double tp = In(0.5);
        foreach (int s in new[] { 1, -1 })
        {
            string sd = s > 0 ? "A" : "B";
            Item($"SP{sd}", "side PL (34 1/2-14) x 1/2", (s * In(14.25), -In(17.25)), (s * In(14.75), -In(17.25)), (s * In(14.75), In(17.25)), (s * In(14.25), In(17.25)));
            Item($"CV{sd}", "cover PL 28 x 1/2", (-In(14), s * In(17.875)), (In(14), s * In(17.875)), (In(14), s * In(18.375)), (-In(14), s * In(18.375)));
            foreach (int r in new[] { 1, -1 })
            {
                // L 6 x 6 x 1/2 in the corner (W = s side, V = r side): backs on the side plate (W = +-14.25) and on the cover (V = +-17.875)
                double W0 = s * In(14.25), V0 = r * In(17.875);
                Item($"L{sd}{(r > 0 ? "T" : "B")}", "L 6 x 6 x 1/2",
                    (W0, V0), (W0 - s * In(6), V0), (W0 - s * In(6), V0 - r * tp), (W0 - s * tp, V0 - r * tp), (W0 - s * tp, V0 - r * In(6)), (W0, V0 - r * In(6)));
            }
        }
    }
    report.Add($"{Main}: {t.PlaneItems.Count} portal knee pieces (x 2 mirrored)");
}

// ================================================================ 3. span 19 floor lateral connections (p.99)
// "FLOOR LATERAL CONN. AT FLOOR BEAM W14" / "AT FLOOR BEAMS W16, W18 & W20": the X diagonals END at the floor beam (rivet groups on each half), a bottom plate runs
// under the floor beam (3/4 Pl. at W14, 7/16 at W16-W20) and two top plates (7/16) stop at the floor beam ("Lateral details sym. abt. CL Bridge except as modified by
// plates and fillers on web of floor beam"), 1-L 3 1/2 x 3 x 3/8 along the floor beam; at W14 the halves toward L13 are "2-Web Pls 12 x 1/2, 2-15[ 50.0,
// 2-Pls (13-6) x 1/2" (= the L13-W14 wind chord section), the others 14 BP 73. "FOR WIND CHORD CONN. AT W15, W17 & W19" (Sections A / B): a 7/16 Pl. under the wind
// chord and the lateral ends ("Slot lateral Pl." in the floor beam web, 1-L 7 x 4 x 3/4 to the floor beam); W21 (K to the tongue beam) similar.
// Rivets 7/8 carbon (p.99 notes). Counts per lateral end read on the details: 14 BP 73 8 per plate, 2-15[ 50 24 per plate, at the wind chord 10.
string flXFlag = U("p.99", "floor lateral connection plates at the floor beams W14 / W16 / W18 / W20 (bottom 3/4 or 7/16 Pl., 2 top 7/16 Pls., L 3 1/2 x 3 x 3/8)",
    "the details draw plates, angles and rivets without outline dimensions or gages; the fillers on the floor beam web are not dimensioned",
    "the X diagonals are split at the crossing and stop clear of the floor beam; bottom plate = envelope of the four member laps under them, top plates one per side stopping 1/4 in. clear of the floor beam with the angle on the floor beam face, rivets 8 (14 BP 73) / 24 (2-15[ 50) per member per plate in 4 lines 5 3/4 / 4 in. from the member CL; fillers on the floor beam web not built.");
string flWFlag = U("p.99", "floor lateral plates at the wind chord W15 / W17 / W19 / W21 (7/16 Pl., 1-L 7 x 4 x 3/4 to the floor beam)",
    "Sections A / B draw the plate under the wind chord and the lateral ends with its rivets but no outline dimensions; the plate passes through a slot in the floor beam web",
    "one 7/16 plate under the lateral ends and the wind chord = envelope of the lateral laps (not clipped at the chord), 10 rivets per lateral; the angles to the floor beam and the web slot are not built.");
if (T(Main) is { } tmF)
{
    var t = tmF;
    Flag(t, "floor lateral (14 BP 73 X / 2-12[ 25 K) connections", null);
    Flag(t, "floor lateral connection plates at the floor beams W14", flXFlag);
    Flag(t, "floor lateral plates at the wind chord W15", flWFlag);
    Flag(t, "floor lateral connections at L13 and at the tongue beam", U("p.95, p.99, p.93", "floor lateral connections at L13 (floor beam F8) and at the tongue beam W22",
        "p.95 draws the lateral gussets on the F8 bottom flange and p.93 / p.99 the tongue beam and shear lock at W22 without outline dimensions",
        "not built; the laterals stop clear of the floor beam / joint."));
    var planeN = t.Planes.First(p => p.Name == "N");
    t.BracingGussets ??= new();
    t.BracingGussets.RemoveAll(b => b.Label.StartsWith("floor lat."));
    int split = 0;
    foreach (var pr in new[] { "", "'" })
    {
        string P(int n) => n == 22 ? "22" : $"{n}{pr}";
        foreach (var (a, b) in new[] { (13, 15), (15, 17), (17, 19), (19, 21) })
        {
            int c = (a + b) / 2;
            string A = (a == 13 ? "L" : "W") + P(a), B = "W" + P(b), FX = "FX" + P(c);
            var pa = t.Points.First(p => p.Name == A && (p.Planes == null || p.Planes.Count == 0 || p.Planes.Contains("N")));
            var pb = t.Points.First(p => p.Name == B && (p.Planes == null || p.Planes.Count == 0 || p.Planes.Contains("N")));
            t.Points.RemoveAll(p => p.Name == FX);
            t.Points.Add(new TrussPointDefinition(FX, (pa.Station + pb.Station) / 2, (pa.Elevation + pb.Elevation) / 2)
                { Offset = -planeN.Offset + (pa.Offset + pb.Offset) / 2, Planes = new() { "N" }, Source = "p.99 crossing of the floor lateral X at the CL of floor beam " + c });
            string Xa = $"FL-X{P(a)}-{P(b)}a", Xb = $"FL-X{P(a)}-{P(b)}b";
            var ma = t.Members.FirstOrDefault(m => m.Name == Xa); var mb = t.Members.FirstOrDefault(m => m.Name == Xb);
            if (ma != null && mb != null)
            {
                // N:A -> S:B and S:A -> N:B become four halves ending at the crossing
                foreach (var (m0, from, to) in new[] { (ma, ma.From, ma.To), (mb, mb.From, mb.To) })
                {
                    t.Members.Remove(m0);
                    var h1 = Clone2(m0); h1.Name = m0.Name + "1"; h1.From = from; h1.To = "N:" + FX; if (a == 13) h1.Section = "L13-W14";
                    var h2 = Clone2(m0); h2.Name = m0.Name + "2"; h2.From = "N:" + FX; h2.To = to;
                    t.Members.Add(h1); t.Members.Add(h2);
                    split++;
                }
            }
            string X1a = Xa + "1", X1b = Xb + "1", X2a = Xa + "2", X2b = Xb + "2";
            var rv = new Dictionary<string, int> { [X1a] = a == 13 ? 24 : 8, [X1b] = a == 13 ? 24 : 8, [X2a] = 8, [X2b] = 8 };
            TrussBracingGussetDefinition G(string label, TrussBracingPlateSide side, double th, List<string> mem, string suffix, bool angle) => new()
            {
                Point = "N:" + FX, Label = label, Orientation = TrussBracingPlateOrientation.Horizontal, Side = side, Thickness = th, Members = mem, Suffix = suffix,
                EdgeAngle = angle ? "L3-1/2x3x3/8" : "", EdgeAngleOnCrossing = angle, EdgeAngleOutside = angle, EdgeRivets = angle ? 5 : 0,
                Rivets = rv, RivetPitch = In(3), RivetGage = In(5.75), RivetGage2 = In(4), RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet",
                Material = "C", Unresolved = flXFlag, Source = "p.99",
            };
            t.BracingGussets.Add(G("floor lat. bottom PL", TrussBracingPlateSide.Below, c == 14 ? In(0.75) : In(7.0 / 16), new() { X1a, X1b, X2a, X2b }, "", false));
            t.BracingGussets.Add(G("floor lat. top PL", TrussBracingPlateSide.Above, In(7.0 / 16), new() { X1a, X1b }, "-1", true));
            t.BracingGussets.Add(G("floor lat. top PL", TrussBracingPlateSide.Above, In(7.0 / 16), new() { X2a, X2b }, "-2", true));
        }
        foreach (int w in new[] { 15, 17, 19, 21 })
            foreach (var pl in new[] { "N", "S" })
                t.BracingGussets.Add(new TrussBracingGussetDefinition
                {
                    Point = $"{pl}:W{P(w)}", Label = "floor lat. wind chord PL", Orientation = TrussBracingPlateOrientation.Horizontal, Side = TrussBracingPlateSide.Below, Thickness = In(7.0 / 16),
                    Members = t.Members.Where(m => m.System == "Floor laterals" && (m.From == $"{pl}:W{P(w)}" || m.To == $"{pl}:W{P(w)}")).Select(m => m.Name).ToList(), NoStackClip = true,
                    RivetsPerMember = 10, RivetPitch = In(3), RivetGage = In(5.75), RivetGage2 = In(4), RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet",
                    Material = "C", Unresolved = flWFlag, Source = "p.99",
                });
    }
    report.Add($"{Main}: floor lateral X split at the floor beams ({split} members -> halves), {t.BracingGussets.Count(b => b.Label.StartsWith("floor lat."))} floor lateral plate definitions");
}
TrussMemberDefinition Clone2(TrussMemberDefinition m) => System.Text.Json.JsonSerializer.Deserialize<TrussMemberDefinition>(System.Text.Json.JsonSerializer.Serialize(m))!;

// ================================================================ regenerate + save
var rep = Doc.Regenerate();
foreach (var b in Doc.Project.Bridges.Where(b => b.Truss != null))
{
    var st = Doc.Model(b.Name).Steel; var tm = st?.Truss;
    if (st == null || tm == null) { Print($"{b.Name}: no model"); continue; }
    Print($"{b.Name}: connection plates {st.Parts.Count(p => p.Category == PartCategory.TrussConnectionPlate)}, connection angles {st.Parts.Count(p => p.Category == PartCategory.TrussConnectionAngle)}, rivets {tm.Rivets:N0}, steel {st.SteelMass * 2.20462262:N0} lb");
    var sw = st.Parts.Where(p => p.Description.Contains("sway gus. PL") || p.Description.Contains("(sway)")).ToList();
    if (sw.Count > 0) Print($"   sway plates {sw.Count(p => p.Category == PartCategory.TrussConnectionPlate)} ({sw.Where(p => p.Category == PartCategory.TrussConnectionPlate).Sum(p => p.Mass) * 2.20462262:N0} lb), sway edge angles {st.Parts.Count(p => p.Mark.EndsWith("-EA") && sw.Any(q => p.Mark.StartsWith(q.Mark)))}");
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("bracing connection plates -") || d.Message.Contains("end connections -") || d.Message.Contains("CHECK"))) Print("   " + d.Message[..Math.Min(400, d.Message.Length)]);
    foreach (var d in st.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info && d.Message.Contains("not built") && !d.Message.StartsWith("UNRESOLVED")).Take(10)) Print("   " + d.Severity + ": " + d.Message[..Math.Min(300, d.Message.Length)]);
    foreach (var e in rep.Errors.Where(e => e.StartsWith(b.Name))) Print("  ERROR " + e);
}
foreach (var s in report) Print("  " + s);
if (Environment.GetEnvironmentVariable("DETAILS_CLASH") == "1")
    foreach (var r in Doc.Clashes(null, new SBM.Core.Checks.ClashOptions { Bolts = Environment.GetEnvironmentVariable("DETAILS_BOLTS") == "1" }))
        foreach (var g in r.Clashes.GroupBy(c => c.CategoryPair).OrderByDescending(g => g.Count()))
        {
            Print($"  CLASH {r.Bridge} | {g.Key} | {g.Count()} | max {g.Max(c => c.Depth) / 0.0254:0.00} in");
            if (Environment.GetEnvironmentVariable("DETAILS_DETAIL") is { Length: > 0 } det && g.Key.Contains(det))
                foreach (var c in g.OrderByDescending(c => c.Depth).Take(40)) Print($"      {c.A.Mark} ({c.A.Description[..Math.Min(60, c.A.Description.Length)]}) x {c.B.Mark} ({c.B.Description[..Math.Min(60, c.B.Description.Length)]}) {c.Depth / 0.0254:0.00} in");
        }
if (!dev) { Doc.Save(outPath); Print($"Saved {outPath}"); }
else if (Environment.GetEnvironmentVariable("DETAILS_OUT") is { Length: > 0 } devOut) { Doc.Save(devOut); Print($"Saved (dev, reduced) {devOut}"); }
