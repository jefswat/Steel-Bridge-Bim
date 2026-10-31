// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - BRACING CONNECTIONS (step 4, 2026-10-21; TRUSS-DESIGN.md section 19):
//   lateral / strut connection plates (horizontal gusset plates + edge angles on the truss joints) with their rivets, and bracing member ends
//   trimmed so every corner stops clear of the truss joint, of the strut / floor beam passing the point and of the other bracing there
//   (generic inputs TrussJointDefaults.BracingCornerClearance / BracingStackClearance, TrussDefinition.BracingGussets).
// Re-runnable (replaces its own inputs); run AFTER truss-ptc-arch / truss-ptc-approach / girders-ptc / deck-ptc / connections-ptc / shoes-ptc / walkways-ptc /
// floor-connections-ptc and BEFORE rivets-ptc:
//   sbm run samples\plans\bracing-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Pages: p.56 (approach typical bracing details), p.48 / p.58 (approach rivet counts per member end), p.71 / p.97-100 (main span bracing systems and details).
// Every scaled value is a dimension-only flag (page + reason); values the sheets do not give are UNRESOLVED flags; nothing is adjusted.
// Dev options (not for the committed model): env BRACING_ONLY=<bridge name part> keeps only those bridges in memory (nothing saved), BRACING_CLASH=1 prints
// the clash summary of the kept bridges.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Steel;
using SBM.Core.Truss;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
string? only = Environment.GetEnvironmentVariable("BRACING_ONLY");
bool dev = !string.IsNullOrEmpty(only);
if (dev) Doc.Project.Bridges.RemoveAll(b => !b.Name.Contains(only!, StringComparison.OrdinalIgnoreCase));
var report = new List<string>();

var approach = new[] { "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27" };
const string Main = "PTC Arch truss 18-20";
TrussDefinition? T(string n) => Doc.Project.Bridges.FirstOrDefault(b => b.Name == n)?.Truss;

// ================================================================ 1. approach trusses (p.56; counts p.48 / p.58)
// p.56 TOP / BOTTOM LATERAL DETAILS: horizontal 7/16 gusset plates; at the truss a 7/16 plate on an edge angle L 4 x 3 1/2 x 7/16 riveted to the chord joint
// (L 7 x 4 x 5/8 (S) at the pier joints L8 / L10 with the box strut), at the CL apex one 7/16 plate on the strut. Rivets 7/8 carbon (p.56 note), counts per
// member END from the stress sheets: top laterals 10, bottom laterals 20, struts 20, box struts 40 (p.48 / p.58 "Riv. in ..." lines).
string apPlateFlag = U("p.56", "approach lateral / strut gusset plates (7/16) and edge angles",
    "the typical details draw the plates and angles without outline dimensions or gages; the top / bottom lateral level is not dimensioned (the members stay at the chord work point level)",
    "plates = envelope of the member laps (1'-6 lap, 1 1/2 edge), under the members (bottom face), 1/4 clear of the chord joint stack; edge angle on the stack face over the plate length; members stop 4 in. clear of the stack (angle leg room) and 1/2 in. clear of the strut / floor beam / other bracing; rivet lines 2 3/4 in. from the member CL (14 BP: also 5 1/2 in.), 3 in. pitch from the member end, the plate lengthened to take the printed count.");
string apSixFlag = U("p.48, p.58", "struts with '6-Riv. (each end)'",
    "the stress sheets print 20 / 40 / 6 rivets per strut end along stacked dimension lines whose extent cannot be separated at the scan resolution",
    "20 per end at every 14 BP 73 strut, 40 at the box struts.");
string apSwayFlag = U("p.56, p.48, p.58", "sway frame / hanger bracing connection plates (24 / 4 rivets each end)",
    "the sway frame sketches mark the members '14 BP 73 H' / '10 WF 33 H' but the orientation of the gusset plates relative to the member flanges is not drawn",
    "not built; the sway / hanger members stop 1/2 in. clear of the floor beam, the strut and the laterals.");
foreach (var bn in approach)
{
    var t = T(bn); if (t == null) continue;
    t.JointDefaults.BracingCornerClearance = In(0.5);
    t.JointDefaults.BracingStackClearance = In(4.25);
    t.BracingGussets = new();
    t.Unresolved.RemoveAll(s => s.Contains("approach lateral / strut gusset plates") || s.Contains("'6-Riv. (each end)'") || s.Contains("sway frame / hanger bracing connection plates"));
    t.Unresolved.Add(apSixFlag); t.Unresolved.Add(apSwayFlag);
    var pts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    string Norm(string spec) => spec.Contains(":UM") || spec.Contains(":LM") ? "N:" + spec[(spec.IndexOf(':') + 1)..] : spec;   // CL points: one plate
    foreach (var m in t.Members.Where(m => m.Role is TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral)) { pts.Add(Norm(m.From)); pts.Add(Norm(m.To)); }
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.Strut && m.Name.StartsWith("BS"))) { pts.Add(m.From); pts.Add(m.To); }
    // batch 5d: the double K apexes on the floor beam F8 (LQ13): p.95 Sections B-E rivet the lateral gussets to the F8 bottom flange, but the 33 WF 130
    // diagonals lie in the steep L13 - L14 chord panel plane (L13 1/2 is 6.8 ft above L13) and the gusset outline / its seat on the flange are not drawn: not built (flag)
    pts.RemoveWhere(sp => sp.Contains(":LQ13"));
    t.Unresolved.RemoveAll(s => s.Contains("double K apex gussets on the floor beam F8"));
    t.Unresolved.Add(U("p.95, p.98", "double K apex gussets on the floor beam F8 (LQ13 / LQ13')",
        "p.95 Sections B-E rivet the lateral gussets to the F8 bottom flange but give no gusset outline, and the 33 WF 130 diagonals rise in the L13 - L14 chord panel plane (L13 1/2 is 6.8 ft above L13) while F8 is level",
        "not built; the diagonals stop clear of F8."));
    foreach (var spec in pts)
    {
        string pt = spec[(spec.IndexOf(':') + 1)..];
        bool top = pt.StartsWith("U"), apex = pt.StartsWith("UM") || pt.StartsWith("LM");
        string j = pt.TrimStart('U', 'L', 'M');
        var strut = t.Members.FirstOrDefault(m => m.Name == "BS" + j);
        bool box = strut?.Section == "STRUTBOX";
        var bg = new TrussBracingGussetDefinition
        {
            Point = spec, Label = "7/16 PL", Orientation = TrussBracingPlateOrientation.Horizontal, Side = TrussBracingPlateSide.Below, Thickness = In(7.0 / 16),
            EdgeAngle = apex ? "" : box ? "L7x4x5/8" : "L4x3-1/2x7/16", EdgeRivets = apex ? 0 : box ? 12 : top ? 8 : 9,
            RivetsPerMember = top ? 10 : 20, RivetPitch = In(3), RivetGage = In(2.75), RivetGage2 = top ? 0 : In(5.5), RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet",
            Material = box && !apex ? "S" : "C", Unresolved = apPlateFlag, Source = "p.56",
        };
        if (box && strut != null) bg.Rivets = new() { [strut.Name] = 40 };
        if (apex && !top && strut != null) bg.Rivets = new() { [strut.Name] = box ? 40 : 20 };
        t.BracingGussets.Add(bg);
    }
    report.Add($"{bn}: {t.BracingGussets.Count} bracing plate definitions");
}

// ================================================================ 2. main span (p.97 lower laterals L1-L13, p.98 lower L15-L22 + upper U12-U22, notes p.97 / p.98)
// Every lateral / strut connection is a PAIR of 1/2 in. gusset plates ("2-1/2 Pls.") on the top and bottom faces of the laced channel members, with
// "2-Ls 7 x 4 x 1/2" along the chord (one per plate; bent at L15 / L16 / L17 where the chord breaks); the plates begin at the W.P. line 1'-2 1/2 from the
// truss CL = the gusset plate face. Rivets in bracing gusset plates 1 in. carbon (p.97 / p.98 notes). Counts per member per plate read on the details:
// struts 18 (3 lines x 6), 2-13[ 31.8 laterals 20, 2-13[ 50 laterals 24, edge angles 13 (L1 / L10 / L12 / L15-L17 drawn), apex: strut 12.
string mainPlateFlag = U("p.97, p.98", "main span lateral / strut gusset plates (2-1/2 Pls.) and edge angles (2-Ls 7 x 4 x 1/2)",
    "the details draw the plates, angles and rivets without outline dimensions or gages (the 1'-2 1/2 W.P. line and the 2'-10 min. / 1'-10 member end distances are the only dimensions)",
    "one plate on the top and one on the bottom face of the members, in the plane of the members it connects = envelope of the member laps (1'-6 lap, 1 1/2 edge), lengthened to the p.71 rivet counts and carried to 1/4 in. from the stack under the edge angle; fills where a member face stands off the plate (Section A '1/2 Fill' is the only printed fill); 4 rivet lines spread across each member's width 2 1/2 in. from its sides, 3 in. pitch; edge angle on the stack face over the plate length; members stop 4 3/4 in. clear of the stack and 1/2 in. clear of the strut / other bracing.");
string mainBoxFlag = U("p.71, p.97", "box strut L0 / L11 connection to the chord joint and to the lateral plates",
    "the box struts (30 / 40 in. webs) are drawn in the bracing diagram only; their end connection and how the 1/2 in. lateral plates meet them are not detailed",
    "the lateral plates lie on the laterals and stop 1/4 in. clear of the box strut; the strut connection is not built.");
string mainSwayFlag = U("p.97, p.71", "main span sway frame connections (sway bracing at L7 'as shown', L8 similar; 2-15[ 33.9 / 2-18[ 42.7 at L11)",
    "the sway frame connection is drawn at one point only (L7) and the plates and angles are not dimensioned",
    "not built; the sway members stop 1/2 in. clear of the strut, the laterals and the floor beam.");
string mainFloorLatFlag = U("p.99", "floor lateral (14 BP 73 X / 2-12[ 25 K) connections at the wind chord and floor beams",
    "the suspended floor lateral sheet was not read in this pass",
    "not built; the members stop clear of the wind chord joint and of each other.");
string mainPortalFlag = U("p.100, p.79", "portal at U11: portal strut (box 34 1/2 deep) and the curved knee braces ('2-[ 26 b.b. / Cov. Pl 36 1/2' with 6 x 6 angles, 1 in. rivets)",
    "p.100 draws the portal knee braces as curved built-up members with section cuts but their curve (radius / centre) and the plate widths along the curve are not dimensioned",
    "the portal strut stays the nominal envelope between the U11 points; the knee braces are not built.");
if (T(Main) is { } tm0)
{
    var t = tm0;
    t.JointDefaults.BracingCornerClearance = In(0.5);
    t.JointDefaults.BracingStackClearance = In(4.75);
    t.BracingGussets = new();
    t.Unresolved.RemoveAll(s => s.Contains("main span lateral / strut gusset plates") || s.Contains("main span sway frame connections") || s.Contains("floor lateral (14 BP 73 X") || s.Contains("portal at U11: portal strut"));
    t.Unresolved.RemoveAll(s => s.Contains("box strut L0 / L11 connection"));
    t.Unresolved.Add(mainSwayFlag); t.Unresolved.Add(mainFloorLatFlag); t.Unresolved.Add(mainPortalFlag); t.Unresolved.Add(mainBoxFlag);
    // p.95 (FLOOR BEAM & BRACING AT L13): the floor beam F8 at L13 carries the lower lateral system on its bottom flange (the "bracing work line" runs along it,
    // Sections B-E rivet the lateral gussets to it, its bottom flange is reinforced by 1-Cov. Pl. 18 x 5/8 x 76'-0 and 2-Ls 8 x 4 x 1 x 76'-0); there is no
    // separate 2-18[ 45.8 strut at L13 (truss-ptc-arch.csx built one from the p.71 diagram, overlapping the floor beam stiffeners / connection angles).
    int removedL13 = t.Members.RemoveAll(m => m.Name == "LS-L13" || m.Name == "LS-L13'");
    t.Unresolved.RemoveAll(s => s.Contains("lower strut at L13 = the floor beam F8"));
    t.Unresolved.Add(U("p.95, p.71", "lower strut at L13 = the floor beam F8 bottom flange",
        "p.95 draws the lateral gussets riveted to the F8 bottom flange along the bracing work line and no separate strut; the extra bottom flange angles 2-Ls 8 x 4 x 1 x 76'-0 are listed without their leg position",
        "the 2-18[ 45.8 strut at L13 / L13' is removed; the laterals end at the floor beam; the 2-Ls 8 x 4 x 1 x 76'-0 are not built."));
    t.Unresolved.RemoveAll(s => s.Contains("wind chord top vs the suspended floor beam bottom"));
    t.Unresolved.Add(U("p.69, p.94, p.99", "wind chord top vs the suspended floor beam bottom (overlap about 0.3 in.)",
        "the wind chord CL elevations (p.69 table) and the suspended floor beam depth / bottom (p.94) leave less than the 14 BP 73 half depth between them; the wind chord to floor beam connection (p.99) was not read",
        "both kept as printed; the overlap (and the floor beam end plates / edge angles over the wind chord) is reported, not adjusted."));
    report.Add($"{Main}: L13 struts removed {removedL13}");
    var pts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    string Norm(string spec) => spec.Contains(":UM") || spec.Contains(":LM") ? "N:" + spec[(spec.IndexOf(':') + 1)..] : spec;
    var lat = t.Members.Where(m => m.Role is TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral && m.System is "Upper laterals" or "Lower laterals").ToList();
    var struts = t.Members.Where(m => m.Role == TrussMemberRole.Strut && m.System is "Upper laterals" or "Lower laterals").ToList();
    foreach (var m in lat) { pts.Add(Norm(m.From)); pts.Add(Norm(m.To)); }
    foreach (var m in struts) { pts.Add(m.From); pts.Add(m.To); }
    // batch 5b (2026-10-28, p.71 BOTTOM / TOP CHORD LATERAL SYSTEM diagrams; Note A: the detail sheets govern only where the diagram gives no number):
    // rivets per member END (split over the two plates). Lower diagonals 26 (L0-L8, L16-L22), 32 (L8-L10), 36 (L10-L11), 40 (L11-L12), 28 (L13-L13 1/2),
    // 38 (L15-L16); lower struts 40, 46 (L8-L10), 50 at L12, 46 at L13 1/2, 16 at L22; upper diagonals 26, struts 40, 16 at U22. No number for L12-LM13
    // (detail sheet p.97: 24 per plate kept) and for the strut passing a K apex (p.97: 12 per plate kept).
    int Num(string spec) { var mm = System.Text.RegularExpressions.Regex.Match(spec, @"(\d+)"); return mm.Success ? int.Parse(mm.Value) : -1; }
    int? PerEnd(TrussMemberDefinition m)
    {
        string a = m.From[(m.From.IndexOf(':') + 1)..], b = m.To[(m.To.IndexOf(':') + 1)..];
        if (m.Role == TrussMemberRole.Strut)
        {
            int k = Num(a);
            if (a.StartsWith("L13H")) return 46;
            if (a.StartsWith("U")) return k == 22 ? 16 : 40;
            return k == 22 ? 16 : k == 12 ? 50 : k is 8 or 9 or 10 ? 46 : 40;
        }
        if (a.StartsWith("U") || a.StartsWith("UM")) return 26;
        if (a.StartsWith("LM") && b.StartsWith("LM")) return 26;   // LL-CL16 (L16-L22 range)
        if (a.Contains("L13H") || b.Contains("L13H") || b.StartsWith("LQ")) return 28;
        int kc = Num(a), ka = Num(b);
        if (ka == kc - 1) return kc <= 8 ? 26 : kc <= 10 ? 32 : 36;
        if (kc == 11) return 40;
        if (kc == 12) return null;
        if (kc == 15) return 38;
        return 26;
    }
    // batch 5d: the double K apexes on the floor beam F8 (LQ13): p.95 Sections B-E rivet the lateral gussets to the F8 bottom flange, but the 33 WF 130
    // diagonals lie in the steep L13 - L14 chord panel plane (L13 1/2 is 6.8 ft above L13) and the gusset outline / its seat on the flange are not drawn: not built (flag)
    pts.RemoveWhere(sp => sp.Contains(":LQ13"));
    t.Unresolved.RemoveAll(s => s.Contains("double K apex gussets on the floor beam F8"));
    t.Unresolved.Add(U("p.95, p.98", "double K apex gussets on the floor beam F8 (LQ13 / LQ13')",
        "p.95 Sections B-E rivet the lateral gussets to the F8 bottom flange but give no gusset outline, and the 33 WF 130 diagonals rise in the L13 - L14 chord panel plane (L13 1/2 is 6.8 ft above L13) while F8 is level",
        "not built; the diagonals stop clear of F8."));
    foreach (var spec in pts)
    {
        string pt = spec[(spec.IndexOf(':') + 1)..];
        bool apex = pt.StartsWith("UM") || pt.StartsWith("LM");
        bool quarter = pt.StartsWith("LQ");   // double K apex on the floor beam F8 (p.95: gussets riveted to its bottom flange, no edge angle)
        var rv = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in lat) rv[m.Name] = PerEnd(m) is { } pe ? (pe + 1) / 2 : 24;
        foreach (var m in struts) rv[m.Name] = apex ? 12 : PerEnd(m) is { } ps ? (ps + 1) / 2 : 18;
        // L0 / L11: the box struts (30 / 40 in. deep, p.71) are deeper than the laterals: one plate pair per lateral, lying on the lateral and stopping
        // clear of the strut
        var deep = struts.Where(m => m.Section.StartsWith("STRUT L") && (m.From == spec || m.To == spec)).ToList();
        var groups = deep.Count > 0
            ? lat.Where(m => Norm(m.From) == spec || Norm(m.To) == spec).Select(m => new List<string> { m.Name }).ToList()
            : new List<List<string>> { new() };
        int gi = 0;
        foreach (var members in groups)
            t.BracingGussets.Add(new TrussBracingGussetDefinition
            {
                Members = members, Suffix = groups.Count > 1 ? "-" + "abcd"[gi++] : "",
                Point = spec, Label = "2-1/2 Pls.", Orientation = TrussBracingPlateOrientation.Fitted, Side = TrussBracingPlateSide.Both, Thickness = In(0.5),
                EdgeAngle = apex || quarter || deep.Count > 0 ? "" : pt.StartsWith("L13H") ? "L7-1/2x4x1/2" : "L7x4x1/2", EdgeRivets = apex || quarter || deep.Count > 0 ? 0 : 13, Rivets = rv, RivetPitch = In(3), RivetGage = In(15.25), RivetGage2 = In(12.75),
                RivetDiameter = In(1), RivetMaterial = "1 in carbon steel rivet", Material = "C", Unresolved = mainPlateFlag, Source = "p.97 / p.98",
                // batch 5a: since batch 1b the members are 14 / 19 in. wide (channels top and bottom): plate in the plane of the members it connects, fills
                // where a member face stands off it, plate carried to the stack under the edge angle, 4 rivet lines across each member's own width
                MemberPlane = true, MemberFills = true, PlateToStack = !apex && deep.Count == 0, RivetGageEdge = In(2.5), RivetGapTolerance = In(3.0 / 16), ProbeFaces = true,
            });
    }
    report.Add($"{Main}: {t.BracingGussets.Count} bracing plate definitions");
}

// ================================================================ 3. girder spans (p.43 plan, p.45 "Bracing conn. at CL floor bm." + the floor beam / girder plan)
// p.45: the ST 6 WF 29 bottom laterals connect to 7/16 (C) plates at the floor beam top flange level ("7/16 Pl. (C)", 13/16 / 7/16 fills, 1 in carbon rivets, 4 x 2
// drawn per lateral end), at the CL floor beam and at the girder / floor beam intersection, and pass under the stringers ("typical lateral connection to
// stringers at each intersection"). girders-ptc.csx put the lateral work points on the floor beam top: the tee (flange up) is lowered so its flange top is
// the floor beam top (it crossed the stringer bottom flanges by 0.59 in.), the ends stop clear of the floor beams and the girder.
string gLatFlag = U("p.45", "girder-span lateral gusset plates (7/16 Pl.) and fills",
    "the plan view draws the plates over the floor beam flange with fills (13/16, 7/16) and the tension plate but gives no outline, level or fill extent",
    "one 7/16 plate on the flange tops of the laterals at each lateral node (floor beam top level), envelope of the lateral laps, stopping 1/4 in. clear of the floor beam (the plate-to-floor-beam rivets and the fills are not built); 10 rivets 7/8 in. carbon per lateral end (2 x 5 drawn, p.45).");
foreach (var gn in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var t = T(gn); if (t == null) continue;
    t.JointDefaults.BracingCornerClearance = In(0.5);
    t.BracingGussets = new();
    t.Unresolved.RemoveAll(s => s.Contains("girder-span lateral gusset plates"));
    var wt = SBM.Core.Truss.TrussSections.Build(t.Section("LAT")!);
    var lats = t.Members.Where(m => m.Role == TrussMemberRole.LowerLateral).ToList();
    foreach (var m in lats) { m.StartSetback = null; m.EndSetback = null; m.OffsetZ = -wt.YMax; }
    var pts = new SortedSet<string>(lats.SelectMany(m => new[] { m.From, m.To }).Select(sp => sp.Contains(":LM") ? "N:" + sp[(sp.IndexOf(':') + 1)..] : sp), StringComparer.OrdinalIgnoreCase);   // CL nodes: one plate
    foreach (var spec in pts)
        t.BracingGussets.Add(new TrussBracingGussetDefinition
        {
            Point = spec, Label = "7/16 Pl. (C)", Orientation = TrussBracingPlateOrientation.Horizontal, Side = TrussBracingPlateSide.Above, Thickness = In(7.0 / 16),
            // agent review girders G22: p.45 draws 2 x 5 = 10 rivets per lateral end, 7/8 carbon (floor / bracing rivets 7/8 unless noted, p.44)
            RivetsPerMember = 10, RivetPitch = In(3), RivetGage = In(2.75), RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet", Material = "C",
            Unresolved = gLatFlag, Source = "p.45",
        });
    report.Add($"{gn}: {t.BracingGussets.Count} lateral plate definitions; laterals lowered {wt.YMax / 0.0254:0.00} in.");
}

// ================================================================ 4. approach floor beam / bracket end connections: fills below the gussets (step 3 CHECK)
// p.50 U2 (3-span): "2-Fills 12 x 13/16" (vertical to gusset, continued below the gusset) + "2-Fills 10 x 5/8" from the gusset bottom to below the floor
// beam = 1 7/16 in. under the connection angles' outstanding legs; p.60 U2 (4-span): "2-Fills 11 1/2 x 5/8" + "2-Fills 10 x 3/4" = 1 3/8 in. (other U joints
// "similar"). floor-connections-ptc.csx stood the angles on the gussets, so below them the legs stood ~1 1/2 in. off the vertical flange and their rivets
// gripped the leg only (CHECK). The stack of fills is modeled as one fill per run of the stand-off under each angle (10 in. across both), thickness = the probed gap (compared with
// the printed sum).
string apOutFillFlag = U("p.50, p.60", "fills under the floor beam / bracket connection angles below the top chord gussets",
    "the joint sheets print two fills (12 x 13/16 + 10 x 5/8 on 3-span U2, 11 1/2 x 5/8 + 10 x 3/4 on 4-span U2; the other U joints 'similar') whose ends are drawn, not dimensioned",
    "one fill per run of stand-off and per angle, 4 1/2 in. from the angle heel (10 in. across both angles), as thick as the probed gap (compared with the printed sum in the Info line).");
int nOutFill = 0;
foreach (var bn in approach)
{
    var t = T(bn); if (t == null) continue;
    bool four = bn.Contains("4-span");
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.FloorBeam && m.EndConnections != null))
        foreach (var ec in m.EndConnections!.Where(e => e.Angle == "L4x4x3/4"))
        {
            ec.OutFillWidth = In(4.5);   // each side: 10 in. fill across the post flange, centred on the floor beam web ec.OutFillPlanThickness = four ? In(1.375) : In(1.4375);
            if (!ec.Unresolved.Contains("fills under the floor beam / bracket connection angles")) ec.Unresolved = (ec.Unresolved + " " + apOutFillFlag).Trim();
            nOutFill++;
        }
}
report.Add($"approach end connections with fills below the gussets: {nOutFill}");

// ================================================================ 5. stringer seats on the floor beams (p.55 / p.45 / p.94 bearing details)
// p.55 BEARING DETAIL (intermediate fixed points, pavement breaks) + ENLARGED DETAILS OF BEARING PLATE AND SOLE PLATE: the stringers bear on the floor
// beams through a sole plate "bevel to grade" (thickness "Varies") on a bearing plate (expansion points: 3 1/2 x 1 grooved plate), 8 1/2 in. wide under the
// 24 WF stringers, 8 in. under the 21 WF; the girder spans (p.45 "Bearing seat ...") and the main span (p.94 "for deck details see sheet 55") use the same
// details. The stringer work lines (top 7 3/4 in. below the grade) left 1.7 - 5.9 in. between the stringer bottoms and the floor beam tops: one seat plate
// per stringer and floor beam fills it (bearing + sole plate as one piece, thickness = the gap).
string seatFlag = U("p.55, p.45, p.94", "stringer seats (bearing plate + bevelled sole plate) on the floor beams",
    "the details give the plate widths (8 1/2 / 8 in.) and 'Varies' for the sole plate thickness, not the plate length along the stringer nor the split between bearing and sole plate",
    "one plate 14 in. along the stringer (the floor beam top flange width), the printed width across, thickness = the gap between the floor beam top and the stringer bottom (bevelled to grade); grooved plates, anchors and tension plates not built.");
int nSeatDefs = 0;
foreach (var b in Doc.Project.Bridges.Where(b => b.Truss?.Floor is { StringersOnTop: true }))
{
    var fl = b.Truss!.Floor!;
    fl.Seats = new TrussStringerSeatDefinition { Length = In(14), Width = In(8.5), Label = "bearing + sole PL", Material = "C", Unresolved = seatFlag, Source = "p.55" };
    foreach (var line in fl.Stringers) line.SeatWidth = line.Section.StartsWith("W21") ? In(8) : In(8.5);
    nSeatDefs++;
}
report.Add($"stringer seats on {nSeatDefs} bridges");

// ================================================================ 6. main post U11-L11 access holes (p.81 note)
// p.81 note "MANHOLES: for all web members 12 x 20 @ 4'-0 c.c." covers the post U11-L11 too; its covers are "(23-12)" = 12 in. holes (p.69 / p.70).
string postPerfFlag = U("p.81, p.69", "post U11-L11 cover plate access holes",
    "the p.81 note gives 12 x 20 @ 4'-0 c.c. for all web members; the post elevation (p.69) is drawn with breaks, so the hole positions are not dimensioned",
    "holes centred between the end gussets, 10 in. clear of them, the second cover shifted half a space.");
if (T(Main)?.Section("POST U11-L11") is { } post)
    foreach (var (pl, bottom) in post.TopPlates.Select(x => (x, false)).Concat(post.BottomPlates.Select(x => (x, true))))
        if (pl.Perforated)
        {
            pl.Perforation = new TrussPerforation { Width = In(12), Length = In(20), Pitch = In(48), EndClear = In(10), Stagger = bottom, Label = "12 x 20 @ 4 ft", Unresolved = postPerfFlag };
            report.Add($"post U11-L11: {pl.Label} perforated 12 x 20 @ 4'-0");
        }

// ================================================================ regenerate + save
var rep = Doc.Regenerate();
foreach (var b in Doc.Project.Bridges.Where(b => b.Truss != null && b.Truss.BracingGussets != null))
{
    var st = Doc.Model(b.Name).Steel; var tm = st?.Truss;
    if (st == null || tm == null) { Print($"{b.Name}: no model"); continue; }
    Print($"{b.Name}: bracing plates {st.Parts.Count(p => p.Mark.Contains("-BG") && p.Category == PartCategory.TrussConnectionPlate)}, edge angles {st.Parts.Count(p => p.Mark.EndsWith("-EA"))}, rivets {tm.Rivets:N0}, steel {st.SteelMass * 2.20462262:N0} lb");
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("bracing connection plates -") || d.Message.Contains("end connection / stiffener rivets -") || d.Message.Contains("fills under the outstanding legs where") || d.Message.Contains("CHECK end connections") || d.Message.Contains("stringer seats"))) Print("   " + d.Message);
    foreach (var d in st.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info && (d.Message.Contains("bracing plate") || d.Message.Contains("setbacks"))).Take(10)) Print("   " + d.Severity + ": " + d.Message[..Math.Min(300, d.Message.Length)]);
    foreach (var e in rep.Errors.Where(e => e.StartsWith(b.Name))) Print("  ERROR " + e);
}
foreach (var s in report) Print("  " + s);
if (Environment.GetEnvironmentVariable("BRACING_CLASH") == "1")
    foreach (var r in Doc.Clashes(null, new SBM.Core.Checks.ClashOptions { Bolts = Environment.GetEnvironmentVariable("BRACING_BOLTS") == "1" }))
        foreach (var g in r.Clashes.GroupBy(c => c.CategoryPair).OrderByDescending(g => g.Count()))
        {
            Print($"  CLASH {r.Bridge} | {g.Key} | {g.Count()} | max {g.Max(c => c.Depth) / 0.0254:0.00} in");
            if (Environment.GetEnvironmentVariable("BRACING_DETAIL") is { Length: > 0 } det && g.Key.Contains(det))
                foreach (var c in g.OrderByDescending(c => c.Depth).Take(6)) Print($"      {c.A.Mark} ({c.A.Description[..Math.Min(50, c.A.Description.Length)]}) x {c.B.Mark} ({c.B.Description[..Math.Min(50, c.B.Description.Length)]}) {c.Depth / 0.0254:0.00} in");
        }
if (!dev) { Doc.Save(outPath); Print($"Saved {outPath}"); }
else if (Environment.GetEnvironmentVariable("BRACING_OUT") is { Length: > 0 } devOut) { Doc.Save(devOut); Print($"Saved (dev, reduced) {devOut}"); }
