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
    "one plate on the top and one on the bottom face of the members = envelope of the member laps (1'-6 lap, 1 1/2 edge), lengthened to the counted rivets (2 lines per channel flange, 12 3/4 / 15 1/4 in. from the member CL, 3 in. pitch); edge angle on the stack face over the plate length; members stop 4 3/4 in. clear of the stack and 1/2 in. clear of the strut / other bracing.");
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
    var pts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    string Norm(string spec) => spec.Contains(":UM") || spec.Contains(":LM") ? "N:" + spec[(spec.IndexOf(':') + 1)..] : spec;
    var lat = t.Members.Where(m => m.Role is TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral && m.System is "Upper laterals" or "Lower laterals").ToList();
    var struts = t.Members.Where(m => m.Role == TrussMemberRole.Strut && m.System is "Upper laterals" or "Lower laterals").ToList();
    foreach (var m in lat) { pts.Add(Norm(m.From)); pts.Add(Norm(m.To)); }
    foreach (var m in struts) { pts.Add(m.From); pts.Add(m.To); }
    foreach (var spec in pts)
    {
        string pt = spec[(spec.IndexOf(':') + 1)..];
        bool apex = pt.StartsWith("UM") || pt.StartsWith("LM");
        var rv = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in lat) rv[m.Name] = m.Section == "LAT50" ? 24 : 20;
        foreach (var m in struts) rv[m.Name] = apex ? 12 : 18;
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
                EdgeAngle = apex ? "" : "L7x4x1/2", EdgeRivets = apex ? 0 : 13, Rivets = rv, RivetPitch = In(3), RivetGage = In(15.25), RivetGage2 = In(12.75),
                RivetDiameter = In(1), RivetMaterial = "1 in carbon steel rivet", Material = "C", Unresolved = mainPlateFlag, Source = "p.97 / p.98",
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
    "one 7/16 plate on the flange tops of the laterals at each lateral node (floor beam top level), envelope of the lateral laps, stopping 1/4 in. clear of the floor beam (the plate-to-floor-beam rivets and the fills are not built); 8 rivets 1 in. carbon per lateral end.");
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
            RivetsPerMember = 8, RivetPitch = In(3), RivetGage = In(2.75), RivetDiameter = In(1), RivetMaterial = "1 in carbon steel rivet", Material = "C",
            Unresolved = gLatFlag, Source = "p.45",
        });
    report.Add($"{gn}: {t.BracingGussets.Count} lateral plate definitions; laterals lowered {wt.YMax / 0.0254:0.00} in.");
}

// ================================================================ regenerate + save
var rep = Doc.Regenerate();
foreach (var b in Doc.Project.Bridges.Where(b => b.Truss != null && b.Truss.BracingGussets != null))
{
    var st = Doc.Model(b.Name).Steel; var tm = st?.Truss;
    if (st == null || tm == null) { Print($"{b.Name}: no model"); continue; }
    Print($"{b.Name}: bracing plates {st.Parts.Count(p => p.Mark.Contains("-BG") && p.Category == PartCategory.TrussConnectionPlate)}, edge angles {st.Parts.Count(p => p.Mark.EndsWith("-EA"))}, rivets {tm.Rivets:N0}, steel {st.SteelMass * 2.20462262:N0} lb");
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("bracing connection plates -") || d.Message.Contains("end connection / stiffener rivets -"))) Print("   " + d.Message);
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
