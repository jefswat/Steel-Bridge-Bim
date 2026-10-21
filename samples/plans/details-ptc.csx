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
                foreach (var c in g.OrderByDescending(c => c.Depth).Take(6)) Print($"      {c.A.Mark} ({c.A.Description[..Math.Min(60, c.A.Description.Length)]}) x {c.B.Mark} ({c.B.Description[..Math.Min(60, c.B.Description.Length)]}) {c.Depth / 0.0254:0.00} in");
        }
if (!dev) { Doc.Save(outPath); Print($"Saved {outPath}"); }
else if (Environment.GetEnvironmentVariable("DETAILS_OUT") is { Length: > 0 } devOut) { Doc.Save(devOut); Print($"Saved (dev, reduced) {devOut}"); }
