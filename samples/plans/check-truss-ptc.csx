// Check of the PTC main truss spans 18-20 (truss-ptc-arch.csx) against the plan values: panel lengths, chord / wind chord / floor beam elevations
// vs the p.69 ELEVATIONS table, the model profile at the gutter line vs the table, member counts by role vs the p.69-71 diagrams, the generator's
// member length checks (p.69 / p.70 tables), steel weight by material vs the p.42 quantities, rope suspender length vs p.42, shoe seats vs p.73 / p.96.
//   sbm run samples\plans\check-truss-ptc.csx "samples\plans\PTC main truss 18-20.sbm"
// Read-only (never saves). Rows: PASS / DIFF (plan difference reported, never adjusted) / INFO.
using System.IO;
using System.Globalization;
using SBM.Core.Bridge;
using SBM.Core.Steel;

var inv = CultureInfo.InvariantCulture;
int pass = 0, diff = 0, info = 0;
void Row(string kind, string item, string plan, string model, string note = "")
{
    if (kind == "PASS") pass++; else if (kind == "DIFF") diff++; else info++;
    Print($"{kind,-5} {item}: plan {plan} | model {model}{(note == "" ? "" : " | " + note)}");
}
void Cmp(string item, double plan, double model, double tol, string unit, string note = "", int dec = 4)
{
    string f = "F" + dec;
    Row(Math.Abs(model - plan) <= tol ? "PASS" : "DIFF", item, plan.ToString(f, inv) + unit, model.ToString(f, inv) + unit, $"diff {(model - plan).ToString(f, inv)}{unit}" + (note == "" ? "" : "; " + note));
}
string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "truss");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
var elev = File.ReadAllLines(Path.Combine(dataDir!, "elevations.csv")).Skip(1).Where(l => l.Trim() != "").Select(l => l.Split(','))
    .ToDictionary(f => int.Parse(f[0]), f => (g: double.Parse(f[1], inv), l: double.Parse(f[2], inv), w: f[3] == "" ? double.NaN : double.Parse(f[3], inv)));

var rep = Doc.Regenerate();
var bdef = Doc.Project.Bridges.FirstOrDefault(b => b.Truss != null && b.Name == "PTC Arch truss 18-20") ?? Doc.Project.Bridges.FirstOrDefault(b => b.Truss != null);
if (bdef == null) { Print("No truss bridge in this model."); return; }
var bm = Doc.Model(bdef.Name);
var tm = bm.Steel!.Truss!;
var al = bm.Framing.Alignment;
double ft = 0.3048;
Print($"Bridge '{bdef.Name}' on '{al.Name}': {tm.Members.Count} members, {tm.Steel.Parts.Count} parts.");

// 1. panel lengths (stations of the L points, both planes)
Print("--- 1. panel lengths (p.69: 11 + 22 + 11 panels @ 31'-0)");
foreach (var pl in new[] { "N", "S" })
{
    var ls = tm.Points.Where(p => p.Plane == pl && System.Text.RegularExpressions.Regex.IsMatch(p.Name, @"^L\d+'?$")).OrderBy(p => p.Station).ToList();
    var d = ls.Zip(ls.Skip(1), (a, b) => (b.Station - a.Station) / ft).ToList();
    Cmp($"{pl} truss: number of panels L0..L0'", 44, d.Count, 0, "", dec: 0);
    Cmp($"{pl} truss: largest panel length difference from 31'-0", 0, d.Max(x => Math.Abs(x - 31)), 0.001, " ft");
    Cmp($"{pl} truss: L0 -> L0' (p.69 341 + 682 + 341)", 1364, (ls[^1].Station - ls[0].Station) / ft, 0.001, " ft", dec: 3);
}

// 2. elevations vs the p.69 table (model points are built from it: confirms the transcription is used as read) + derived U points
Print("--- 2. panel point elevations vs the p.69 ELEVATIONS table");
int okL = 0, okW = 0;
foreach (var pl in new[] { "N", "S" })
    foreach (var (n, e) in elev)
        foreach (var pr in new[] { "", "'" })
        {
            if (n == 22 && pr == "'") continue;
            var L = tm.Point(pl, $"L{n}{pr}");
            if (n == 13) { if (pl == "N" && pr == "") Row("DIFF", "L13 working point (CONFLICT p.69 table vs member lengths)", $"El {e.l:F4} (table)", $"El {L!.World.Z / ft:F4}", "the members close on 140.7620 = wind chord value (truss-ptc-arch.csx)"); continue; }
            if (Math.Abs(L!.World.Z / ft - e.l) < 0.00005) okL++; else Cmp($"{pl} L{n}{pr} elevation", e.l, L.World.Z / ft, 0.00005, " ft");
            if (!double.IsNaN(e.w) && n >= 14)
            {
                var W = tm.Point(pl, $"W{n}{pr}");
                if (Math.Abs(W!.World.Z / ft - e.w) < 0.00005) okW++; else Cmp($"{pl} W{n}{pr} elevation", e.w, W.World.Z / ft, 0.00005, " ft");
            }
        }
Row(okL == 2 * (2 * 23 - 1 - 2) ? "PASS" : "DIFF", "lower chord points at the table elevation (all but L13 / L13')", $"{2 * (2 * 23 - 1 - 2)}", $"{okL}");
Row(okW == 2 * (2 * 9 - 1) ? "PASS" : "DIFF", "wind chord points W14..W22..W14' at the table elevation", $"{2 * 17}", $"{okW}");
var u22 = tm.Point("N", "U22")!; var u11 = tm.Point("N", "U11")!;
Row("INFO", "derived upper chord crown U22 (L22 + U22-L22 32.0000, p.69 / p.70)", "-", $"El {u22.World.Z / ft:F4}");
Row("INFO", "derived U11 (L11 + post U11-L11 96.0000)", "-", $"El {u11.World.Z / ft:F4}");

// 3. the model profile at the gutter line (the PTC alignment) vs the p.69 gutter column
Print("--- 3. finished grade at the gutter line (38'-3 from CL, p.107) vs p.69 GUTTER GRADE LINE");
double sta0 = Sta("675+98.380"), worst = 0; string worstAt = "";
foreach (var (n, e) in elev)
    foreach (var (pr, k) in new[] { ("", n), ("'", 44 - n) })
    {
        if (n == 22 && pr == "'") continue;
        double s = sta0 + Ft(31) * k;
        foreach (double off in new[] { -38.25, 38.25 })
        {
            double z = al.ElevationAt(s, Ft(off)) / ft;
            if (Math.Abs(z - e.g) > Math.Abs(worst)) { worst = z - e.g; worstAt = $"{n}{pr} offset {off}"; }
        }
    }
Cmp($"largest gutter difference at the 45 panel points (at {worstAt})", 0, worst, 0.001, " ft");

// 4. member counts by role (p.69 - p.71 diagrams)
Print("--- 4. member counts");
int C(SBM.Core.Bridge.TrussMemberRole r, string pl = "") => tm.Members.Count(m => m.Role == r && (pl == "" || m.Plane == pl));
foreach (var pl in new[] { "N", "S" })
{
    Cmp($"{pl}: upper chord members U0-U1 .. U1'-U0' (p.69)", 44, C(TrussMemberRole.UpperChord, pl), 0, "", dec: 0);
    Cmp($"{pl}: lower chord members (L0-L2, L2-L4, L4-L5 .. L21-L22 and mirror, p.69)", 40, C(TrussMemberRole.LowerChord, pl), 0, "", dec: 0);
    Cmp($"{pl}: verticals U0-L0 .. U0'-L0' (p.70)", 45, C(TrussMemberRole.Vertical, pl), 0, "", dec: 0);
    Cmp($"{pl}: diagonals L0-U1 .. U21-L22 and mirror (p.70)", 44, C(TrussMemberRole.Diagonal, pl), 0, "", dec: 0);
    Cmp($"{pl}: wind chord L13-W14 .. W21-W22 and mirror (p.71)", 18, C(TrussMemberRole.WindChord, pl), 0, "", dec: 0);
    Cmp($"{pl}: rigid suspenders L14-W14 / L14'-W14' (p.70 / p.71)", 2, C(TrussMemberRole.Hanger, pl), 0, "", dec: 0);
}
Cmp("floor beams 0 .. 22 .. 0' (p.72)", 45, C(TrussMemberRole.FloorBeam), 0, "", dec: 0);
Cmp("stringers: 11 lines x 44 bays (p.72 / p.94)", 11 * 44, C(TrussMemberRole.Stringer), 0, "", dec: 0);
Cmp("rope suspenders 15 .. 22 .. 15' x 2 trusses (p.70 / p.94)", 30, tm.Suspenders.Count, 0, "", dec: 0);
Cmp("shoes L0 / L11 / L11' / L0' x 2 trusses (p.73 / p.96)", 8, tm.Shoes.Count, 0, "", dec: 0);
{
    // p.71 FLOOR LATERAL SYSTEM / p.99: two-panel X of 14 BP 73 crossing at the CL of floor beams W14 / W16 / W18 / W20 (and primes), K of 2-12[ 25 W21-W22-W21' (QC I026)
    var centres = new List<SBM.Core.Geometry.Vec3>();
    foreach (var w in new[] { "W14", "W16", "W18", "W20", "W14'", "W16'", "W18'", "W20'" })
    {
        var pn = tm.Points.FirstOrDefault(p => p.Plane == "N" && p.Name == w); var ps = tm.Points.FirstOrDefault(p => p.Plane == "S" && p.Name == w);
        if (pn != null && ps != null) centres.Add((pn.World + ps.World) * 0.5);
    }
    var fl = tm.Members.Where(m => m.Role == TrussMemberRole.LowerLateral && m.Section == "HP14x73").ToList();
    int atFb = fl.Count(m => centres.Any(c => ((m.A + m.B) * 0.5 - c).Length < 0.03));
    Cmp("floor lateral X members (14 BP 73) crossing at the CL of floor beams W14 / W16 / W18 / W20 and primes (p.71 / p.99)", 16, atFb, 0, "", $"{fl.Count} members", dec: 0);
    Cmp("floor lateral K members 2-12[ 25 W21-W22-W21' (p.71)", 4, tm.Members.Count(m => m.Role == TrussMemberRole.LowerLateral && m.Section == "FLAT21"), 0, "", dec: 0);
}
foreach (var g in tm.Members.Where(m => m.Plane == "").GroupBy(m => m.Role).OrderBy(g => g.Key)) Row("INFO", $"cross-plane members {g.Key}", "-", $"{g.Count()}");
Row("INFO", "gusset plates (2 per joint and truss)", "-", $"{tm.Gussets}");

// 5. generator checks (member lengths vs the p.69 / p.70 tables, suspenders, shoes)
Print("--- 5. member lengths (p.69 chords, p.70 verticals / diagonals) and generator checks");
foreach (var c in tm.Checks.Where(c => !c.Item.StartsWith("steel weight")))
    if (c.Ok) pass++; else Row("DIFF", c.Item, c.Plan, c.Model, c.Note);
Print($"      ({tm.Checks.Count(c => c.Ok && !c.Item.StartsWith("steel weight"))} generator checks PASS)");

// 6. steel weight by material vs p.42 (main river spans: carbon 5,730,000 lb, silicon 10,200,000 lb)
Print("--- 6. weights (p.42 QUANTITIES, main river spans)");
double lb = 2.20462262;
var structural = tm.Steel.Parts.Where(p => p.Category is not (PartCategory.Suspender or PartCategory.SuspenderSocket or PartCategory.TrussPin or PartCategory.TrussShoeFixed or PartCategory.TrussShoeExpansion)).ToList();
double carbon = structural.Where(p => p.Material.Contains("carbon")).Sum(p => p.Mass) * lb, silicon = structural.Where(p => p.Material.Contains("silicon")).Sum(p => p.Mass) * lb;
double other = structural.Where(p => !p.Material.Contains("carbon") && !p.Material.Contains("silicon")).Sum(p => p.Mass) * lb;
Cmp("structural carbon steel (C)", 5_730_000, carbon, 0.05 * 5_730_000, " lb", "not modeled: splice / inlay / fill plates, diaphragms, lug angles, stiffeners, rivet heads; perforations built solid", 0);
Cmp("structural silicon steel (S)", 10_200_000, silicon, 0.05 * 10_200_000, " lb", "same", 0);
Cmp("structural steel C + S", 15_930_000, carbon + silicon + other, 0.05 * 15_930_000, " lb", $"{(carbon + silicon + other) / 15_930_000:P1} of the plan quantity", 0);
foreach (var g in structural.GroupBy(p => p.Category).OrderBy(g => g.Key)) Row("INFO", $"weight {g.Key}", "-", $"{g.Sum(p => p.Mass) * lb:N0} lb ({g.Count()} parts)");
double castings = tm.Steel.Parts.Where(p => p.Category is PartCategory.TrussShoeFixed or PartCategory.TrussShoeExpansion or PartCategory.SuspenderSocket).Sum(p => p.Mass) * lb;
Row("INFO", "steel castings (p.42 item 9: 190,100 lb) vs shoe bodies + sockets (envelopes)", "190,100 lb", $"{castings:N0} lb");
double rope = tm.Suspenders.Sum(s => s.Length * s.Ropes) / ft;
Cmp("bridge rope suspenders, pin to pin (p.42 item 7)", 1760, rope, 0.05 * 1760, " LF", "the quantity may be measured socket to socket / to the rope ends", 1);
if (bm.Concrete?.Deck is { } dk) Row("INFO", "deck slab 7 3/4 between the curb lines (p.42 item 11 Class A concrete 2,730 CY incl. curbs / parapets / dams)", "2,730 CY", $"{Math.Abs(dk.Volume()) / 0.764554857984:N0} CY");

// 7. shoes (p.73: bottom of shoe El. 107.682 at L0; p.96: bottom of shoes El. 87.701, pin El. 94.2843 derived)
Print("--- 7. shoes");
foreach (var s in tm.Shoes)
{
    double plan = s.Name.EndsWith("L0") || s.Name.EndsWith("L0'") ? 107.682 : 87.701;
    Cmp($"shoe {s.Name} at {s.Support}: bottom of shoe", plan, s.SeatElevation / ft, 0.001, " ft", dec: 3);
}
foreach (var seat in bm.Steel.BearingSeats.Take(1)) Row("INFO", "bearing seats published for the substructure (Location + axis)", "8", $"{bm.Steel.BearingSeats.Count}");
// shoe positions and the chord ends above them (p.73 ELEVATION: rocker pin on the vertical line through L0, chord + gussets about 1'-5 past it, scaled; p.96: pin under L11)
foreach (var s in tm.Shoes)
{
    string pt = s.Name.Substring(2);
    var P = tm.Point(s.Name.Substring(0, 1), pt)!;
    Cmp($"shoe {s.Name}: pin on the vertical line through {pt} (p.73 / p.96)", 0, (al.Inverse(s.Pin.XY).Station - P.Station) / ft, 0.0005, " ft");
    double below = (P.World.Z - s.Pin.Z) / ft, planBelow = pt.StartsWith("L0") ? 2 + 0.5 / 12 : 3 + 11.0 / 12;
    Cmp($"shoe {s.Name}: pin below the chord work point {pt} (p.73 2'-0 1/2 / p.96 3'-11)", planBelow, below, 0.001, " ft");
}
foreach (var (pl, mk, pt) in new[] { ("N", "N-L0-L2", "L0"), ("N", "N-L2'-L0'", "L0'"), ("S", "S-L0-L2", "L0"), ("S", "S-L2'-L0'", "L0'") })
{
    var sh = tm.Shoes.First(s => s.Name == $"{pl}-{pt}");
    var ch = tm.Steel.Parts.First(p => p.Mark == mk);
    double pinS = al.Inverse(sh.Pin.XY).Station;
    var ss = ch.Mesh.Positions.Select(v => al.Inverse(v.XY).Station).ToList();
    double past = (pt == "L0" ? pinS - ss.Min() : ss.Max() - pinS) / ft;
    Cmp($"chord {mk} end past the rocker pin {pt} (p.73, scaled 1'-5; flagged)", 17.0 / 12, past, 0.05, " ft", "end cut square to the 3 % chord: its lower corner reaches 1/2 in. further; sole plate 19 in. long = 9 1/2 in. each side of the pin", 4);
}
Cmp("CHECK warnings 'top of the shoe projects beyond the end of chord' (generator)", 0, rep.Warnings.Count(w => w.StartsWith(bdef.Name) && w.Contains("beyond the end of chord")), 0, "", dec: 0);
// floor (p.72 / p.85 / p.94): stringer lines and floor beam ends
var sOff = bdef.Truss!.Floor!.Stringers.Select(s => s.Offset / ft).OrderBy(o => o).ToList();
Cmp("stringer lines (p.72: S13 S14 S15 S15 S15 S16 S15 S15 S15 S14 S13)", 11, sOff.Count, 0, "", dec: 0);
Cmp("outside stringer S13 from the CL (p.85: 33'-0 + 6'-1; 10 in. outside the gutter, 4'-5 inside the truss)", 39 + 1.0 / 12, Math.Max(-sOff[0], sOff[^1]), 0.0005, " ft");
{
    var fb = tm.Steel.Parts.First(p => p.Mark == "FB1");
    double o1 = fb.Mesh.Positions.Max(v => al.Inverse(v.XY).Offset) / ft;
    Cmp("framed floor beam F6 end from the truss CL (p.85: floor beam finished at the inside face of the truss vertical, scaled 1'-2 3/4; built 1'-2 1/2)", 1 + 2.75 / 12, 43.5 - o1, 0.03, " ft", "end angles / connection material not built", 4);
}

// 8. suspenders (pin-to-pin lengths; no plan table)
Print("--- 8. rope suspenders (pin to pin, N truss)");
foreach (var s in tm.Suspenders.Where(s => s.Plane == "N").OrderBy(s => s.LowerPin.X)) Row("INFO", $"suspender {s.Name}", "-", $"{s.Length / ft:F3} ft");

// 8b. gusset layers per face (gusset-layers.csv: L10 / L10' p.78, L11 / L11' p.80 Section E (low confidence), L22 p.90; every other joint one plate per face)
Print("--- 8b. gusset layers (plates per face, total thickness)");
{
    var layerFile = Path.Combine(Path.GetDirectoryName(dataDir!)!, "connections", "gusset-layers.csv");
    var plan = new Dictionary<string, (int n, double tot, string pg, string conf)>();
    if (File.Exists(layerFile))
    {
        List<string> Split(string line)
        {
            var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
            foreach (char c in line) { if (c == '"') q = !q; else if (c == ',' && !q) { cells.Add(cur.ToString().Trim()); cur.Clear(); } else cur.Append(c); }
            cells.Add(cur.ToString().Trim());
            return cells;
        }
        var lines = File.ReadAllLines(layerFile).Where(l => l.Trim() != "").ToList();
        var hdr = Split(lines[0]);
        var rows = lines.Skip(1).Select(l => { var f = Split(l); return hdr.Select((h, i) => (h, v: i < f.Count ? f[i] : "")).ToDictionary(x => x.h, x => x.v); })
            .Where(r => r["structure"] == "main" && r["layers"] != "1").ToList();
        double Frac(string s) { double v = 0; foreach (var part in s.Split(' ', StringSplitOptions.RemoveEmptyEntries)) v += part.Contains('/') ? double.Parse(part.Split('/')[0], inv) / double.Parse(part.Split('/')[1], inv) : double.Parse(part, inv); return v; }
        foreach (var g in rows.GroupBy(r => r["joint"]))
        {
            var e = (int.Parse(g.First()["layers"]), g.Sum(r => Frac(r["thickness_in"])), g.First()["page"], g.First()["confidence"]);
            plan[g.Key] = e;
            if (g.Key != "L22") plan[g.Key + "'"] = e;
        }
    }
    int single = 0, singleOk = 0;
    foreach (var gs in tm.GussetStacks.OrderBy(g => g.Plane).ThenBy(g => g.Point))
    {
        if (plan.TryGetValue(gs.Point, out var e))
        {
            Cmp($"gusset layers per face {gs.Plane} {gs.Point} (p.{e.pg}, {e.conf} confidence)", e.n, gs.Layers, 0, "", $"model {string.Join(" + ", gs.Thicknesses.Select(t => (t / 0.0254).ToString("0.###", inv)))} in.", 0);
            Cmp($"gusset stack thickness {gs.Plane} {gs.Point} (p.{e.pg})", e.tot, gs.TotalThickness / 0.0254, 1.0 / 64, " in", "", 4);
            for (int k = 1; k < gs.Layers; k++) Row("INFO", $"gusset layer {k + 1} area {gs.Plane} {gs.Point} (outline scaled / whole joint, clipped to the inner gusset envelope)", "-", $"{gs.Areas[k] / 0.09290304:F1} SF per plate (inner {gs.Areas[0] / 0.09290304:F1} SF)");
        }
        else { single++; if (gs.Layers == 1) singleOk++; }
    }
    Cmp("joints with ONE gusset per face (p.73-90 re-checked 2026-10-20)", single, singleOk, 0, "", "", 0);

    // gusset outlines (gusset-outlines.csv, connections-ptc.csx 4b) and rivet cover: every joint rivet inside its inner gusset with its edge distance
    {
        var tdef = Doc.Project.Bridges.First(b => b.Name == bdef.Name).Truss!;
        var withOutline = tm.GussetStacks.Where(g => tdef.Joints.Any(j => j.Point == g.Point && j.Outline.Count >= 3)).ToList();
        Row("INFO", "joints with a gusset outline read from the sheet (rest: envelope of the member laps)", "-", $"{withOutline.Count} of {tm.GussetStacks.Count} joint x plane; inner plates {withOutline.Sum(g => g.Areas[0]) / 0.09290304:N0} SF per face (outlines) + {tm.GussetStacks.Except(withOutline).Sum(g => g.Areas[0]) / 0.09290304:N0} SF (envelopes)");
        foreach (var rc in tm.GussetRivetCovers.OrderBy(c => c.Plane).ThenBy(c => c.Point))
            Row(rc.Ok ? "PASS" : "DIFF", $"gusset covers its rivets {rc.Plane} {rc.Point} ({(rc.OutlineGiven ? "outline from the sheet" : "envelope")})", $"every rivet >= {rc.RequiredEdge / 0.0254:0.##} in. from the edge",
                $"{rc.Rivets} rivets per face, smallest edge distance {rc.MinEdge / 0.0254:0.##} in.{(rc.Short > 0 ? $", {rc.Short} short / outside" : "")}");
    }
    var gp = tm.Steel.Parts.Where(p => p.Category == PartCategory.TrussGusset).ToList();
    Row("INFO", "gusset plates (all layers)", "-", $"{gp.Count} plates, {gp.Sum(p => p.Mass) * 2.20462262:N0} lb ({gp.Where(p => System.Text.RegularExpressions.Regex.IsMatch(p.Mark, @"-[LR]\d$")).Sum(p => p.Mass) * 2.20462262:N0} lb in the outer layers)");
}

// 9. warnings
var warns = rep.Warnings.Where(w => w.StartsWith(bdef.Name)).ToList();
Print($"--- 9. {warns.Count} warnings ({warns.Count(w => w.Contains("UNRESOLVED"))} UNRESOLVED, {warns.Count(w => w.Contains("CHECK"))} CHECK)");
foreach (var w in warns.Where(w => w.Contains("UNRESOLVED")).Take(60)) Print("  " + w);
Print($"SUMMARY: {pass} PASS / {diff} DIFF / {info} INFO");
