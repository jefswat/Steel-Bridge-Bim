// Check of the PTC approach trusses (truss-ptc-approach.csx) against the plan values, all four units: "PTC 3-span truss 11-13", "PTC 4-span truss 14-17",
// "PTC 4-span truss 21-24", "PTC 3-span truss 25-27":
//   1 panel lengths / unit length (p.47 / p.57: panels @ 26'-11 1/4) and the stations of the support lines (p.1 / p.5 / p.32 / supports.csv)
//   2 gutter elevations at the piers (printed 91.430 .. 110.825 / 110.945 .. 143.270, p.47 / p.57) and the PTC model profile at the gutter line (38'-3 from CL) at EVERY panel point;
//     chord work points (gutter - 3'-6, - 31'-0) and the back of the floor beams (gutter - 2'-8 3/4, p.55)
//   3 member counts by role per truss line / cross-plane (stress sheets p.47 / p.57, floor and bracing sheets p.48 / p.58)
//   4 the generator's member length checks (printed geom lengths 26.9496 / 39.0766 / 37.9221 / 27.5000)
//   5 shoes: bottom of shoe = pier top (p.5 / p.32), shoe type per pier (fixed only at joint 8 / 20), work point to bottom of shoe closes with the printed heights
//   5b member material letter (C) / (S) per member table row (members.csv, p.47 / p.57)
//   5c member section gross area at mid-member vs the member table row (incl. cover plates, p.57)
//   6 steel weight by material vs the p.42 items 2 + 5 per unit
//   sbm run samples\plans\check-truss-ptc-approach.csx "samples\plans\PTC approach trusses.sbm"
// Read-only (never saves). Rows: PASS / DIFF (plan difference reported, never adjusted) / INFO.
using System.Globalization;
using System.IO;
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
double ft = 0.3048, lb = 2.20462262;
List<Dictionary<string, string>> MemberRows()
{
    string file = null;
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null && file == null; dir = dir.Parent)
    {
        var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "truss-approach", "members.csv");
        if (File.Exists(p)) file = p;
    }
    if (file == null) return new();
    List<string> Split(string line)
    {
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        foreach (char c in line) { if (c == '"') q = !q; else if (c == ',' && !q) { cells.Add(cur.ToString().Trim()); cur.Clear(); } else cur.Append(c); }
        cells.Add(cur.ToString().Trim());
        return cells;
    }
    var lines = File.ReadAllLines(file).Where(l => l.Trim() != "").ToList();
    var hdr = Split(lines[0]);
    return lines.Skip(1).Select(l => { var f = Split(l); return hdr.Select((h, i) => (h, v: i < f.Count ? f[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
List<Dictionary<string, string>> _layerRows = null;
List<Dictionary<string, string>> LayerRows()
{
    if (_layerRows != null) return _layerRows;
    _layerRows = new();
    string file = null;
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null && file == null; dir = dir.Parent)
    {
        var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "connections", "gusset-layers.csv");
        if (File.Exists(p)) file = p;
    }
    if (file == null) return _layerRows;
    List<string> Split(string line)
    {
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        foreach (char c in line) { if (c == '"') q = !q; else if (c == ',' && !q) { cells.Add(cur.ToString().Trim()); cur.Clear(); } else cur.Append(c); }
        cells.Add(cur.ToString().Trim());
        return cells;
    }
    var lines = File.ReadAllLines(file).Where(l => l.Trim() != "").ToList();
    var hdr = Split(lines[0]);
    _layerRows = lines.Skip(1).Select(l => { var f = Split(l); return hdr.Select((h, i) => (h, v: i < f.Count ? f[i] : "")).ToDictionary(x => x.h, x => x.v); }).Where(r => r["layers"] != "1").ToList();
    return _layerRows;
}
double Frac(string s) { double v = 0; foreach (var part in s.Split(' ', StringSplitOptions.RemoveEmptyEntries)) v += part.Contains('/') ? double.Parse(part.Split('/')[0], inv) / double.Parse(part.Split('/')[1], inv) : double.Parse(part, inv); return v; }
var rep = Doc.Regenerate();

// plan restatement per unit: printed gutter elevations at the pier joints, pier tops (bottoms of shoes), p.42 weights (carbon / silicon, items 2 / 5)
var units = new[]
{
    (name: "PTC 3-span truss 11-13", four: false, sta0: Sta("658+66.380"), dir: 1, piers: new[] { 0, 8, 16, 24 }, gutters: new[] { 91.430, 97.895, 104.360, 110.825 },
        tops: new[] { 55.962, 62.312, 68.339, 75.357 }, types: "EFEE", carbon: 1_532_000.0, silicon: 1_324_000.0, supports: new[] { "658+66.380", "660+81.880", "662+97.380", "665+12.880" }),
    (name: "PTC 4-span truss 14-17", four: true, sta0: Sta("665+16.880"), dir: 1, piers: new[] { 0, 10, 20, 30, 40 }, gutters: new[] { 110.945, 119.026, 127.108, 135.189, 143.270 },
        tops: new[] { 75.357, 83.005, 91.525, 99.168, 107.682 }, types: "EEFEE", carbon: 2_423_000.0, silicon: 2_736_000.0, supports: new[] { "665+16.880", "667+86.255", "670+55.630", "673+25.005", "675+94.380" }),
    (name: "PTC 4-span truss 21-24", four: true, sta0: Sta("700+43.880"), dir: -1, piers: new[] { 0, 10, 20, 30, 40 }, gutters: new[] { 110.945, 119.026, 127.108, 135.189, 143.270 },
        tops: new[] { 75.357, 83.005, 91.525, 99.168, 107.682 }, types: "EEFEE", carbon: 2_423_000.0, silicon: 2_736_000.0, supports: new[] { "700+43.880", "697+74.505", "695+05.130", "692+35.755", "689+66.380" }),
    (name: "PTC 3-span truss 25-27", four: false, sta0: Sta("706+94.380"), dir: -1, piers: new[] { 0, 8, 16, 24 }, gutters: new[] { 91.430, 97.895, 104.360, 110.825 },
        tops: new[] { 55.962, 62.312, 68.339, 75.357 }, types: "EFEE", carbon: 1_532_000.0, silicon: 1_324_000.0, supports: new[] { "706+94.380", "704+78.880", "702+63.380", "700+47.880" }),
};
double panelFt = 26.9375;
foreach (var u in units)
{
    Print($"=============== {u.name}");
    var bdef = Doc.Project.Bridges.FirstOrDefault(b => b.Name == u.name);
    if (bdef?.Truss == null) { Print("  no truss in this bridge (run truss-ptc-approach.csx first)"); continue; }
    var bm = Doc.Model(u.name);
    var tm = bm.Steel!.Truss!;
    var al = bm.Framing.Alignment;
    int N = u.four ? 40 : 24, C = u.four ? 20 : 12;
    string J(int m) => m <= C ? m.ToString() : $"{2 * C - m}'";
    Print($"{tm.Members.Count} members, {tm.Gussets} gussets, {tm.Shoes.Count} shoes, {tm.Steel.Parts.Count} parts, deck {(bm.Concrete?.Deck != null ? "built" : "none")}");

    // 1. panel lengths and stations
    Print("--- 1. panels and support lines");
    foreach (var pl in new[] { "N", "S" })
    {
        var ls = Enumerable.Range(0, N + 1).Select(m => tm.Point(pl, "L" + J(m))!).ToList();
        double worst = ls.Zip(ls.Skip(1), (a, b) => Math.Abs(Math.Abs(b.Station - a.Station) / ft - panelFt)).Max();
        Cmp($"{pl} truss: panel length 26'-11 1/4 (largest difference over {N} panels)", 0, worst, 0.0005, " ft");
        Cmp($"{pl} truss: joint 0 to 0' = {N} x 26'-11 1/4 ({(N * panelFt):F3} ft; {(u.four ? "p.57 1077'-6" : "p.47 646'-6")})", N * panelFt, Math.Abs(ls[^1].Station - ls[0].Station) / ft, 0.0005, " ft", dec: 3);
    }
    for (int i = 0; i < u.piers.Length; i++)
    {
        var sup = bdef.Supports[i];
        double st = tm.Point("N", "L" + J(u.piers[i]))!.Station;
        Cmp($"support {sup.Name}: station of the shoe line vs joint L{J(u.piers[i])}", Sta(u.supports[i]) / ft, st / ft, 0.002, " ft", $"printed {u.supports[i]}", 3);
    }

    // 2. elevations
    Print("--- 2. gutter line, chord work points, floor beams");
    for (int i = 0; i < u.piers.Length; i++)
    {
        var up = tm.Point("N", "U" + J(u.piers[i]))!;
        Cmp($"gutter El at joint {J(u.piers[i])} = top chord WP + 3'-6 (printed {u.gutters[i]:F3})", u.gutters[i], up.World.Z / ft + 3.5, 0.001, " ft", "the printed values are rounded to 0.001", dec: 3);
    }
    double wg = 0, wFb = 0, wTop = 0, wDepth = 0; int wgAt = 0;
    for (int m = 0; m <= N; m++)
        foreach (var pl in new[] { "N", "S" })
        {
            double s = u.sta0 + u.dir * m * panelFt * ft;
            double off = pl == "N" ? -38.25 : 38.25;
            double g = al.ElevationAt(s, off * ft) / ft;                       // PTC model profile at the gutter line
            var up = tm.Point(pl, "U" + J(m))!; var lo = tm.Point(pl, "L" + J(m))!; var fb = tm.Point(pl, "FB" + J(m))!;
            double dg = up.World.Z / ft + 3.5 - g;
            if (Math.Abs(dg) > Math.Abs(wg)) { wg = dg; wgAt = m; }
            wDepth = Math.Max(wDepth, Math.Abs((up.World.Z - lo.World.Z) / ft - 27.5));
            wFb = Math.Max(wFb, Math.Abs(g - (fb.World.Z / ft + 37.75 / 12) - 2.0 - 8.75 / 12));   // back of floor beam angles = gutter - 2'-8 3/4
        }
    Cmp($"PTC model profile at the gutter line vs top chord WP + 3'-6, all {2 * (N + 1)} points (largest at joint {wgAt})", 0, wg, 0.005, " ft", "the model carries the printed gutter profile + 38'-3 x 1/8 in. crown (p.107)", 4);
    Cmp("chord depth 27'-6 vertical at every joint (largest difference)", 0, wDepth, 0.00005, " ft");
    Cmp("back of the floor beam angles 2'-8 3/4 under the gutter line (p.55, largest difference)", 0, wFb, 0.001, " ft");

    // 3. member counts
    Print("--- 3. member counts");
    int Cn(TrussMemberRole r) => tm.Members.Count(m => m.Role == r);
    int topRows = u.four ? 21 : 13, botRows = u.four ? 20 : 12;
    Cmp("upper chord members (printed rows incl. mirror, x 2 trusses)", 2 * topRows, Cn(TrussMemberRole.UpperChord), 0, "", dec: 0);
    Cmp("lower chord members (x 2 trusses)", 2 * botRows, Cn(TrussMemberRole.LowerChord), 0, "", dec: 0);
    Cmp("diagonals (one per panel, x 2 trusses)", 2 * N, Cn(TrussMemberRole.Diagonal), 0, "", dec: 0);
    Cmp("posts at the even points (x 2 trusses)", 2 * (N / 2 + 1), Cn(TrussMemberRole.Post), 0, "", dec: 0);
    Cmp("hangers at the odd points (x 2 trusses)", 2 * (N / 2), Cn(TrussMemberRole.Hanger), 0, "", dec: 0);
    Cmp("floor beams: one per panel point + 2 brackets B1 each", 3 * (N + 1), Cn(TrussMemberRole.FloorBeam), 0, "", dec: 0);
    Cmp("stringers: 11 lines x panels (4-span / 3-span continuous)", 11 * N, Cn(TrussMemberRole.Stringer), 0, "", dec: 0);
    Cmp("top lateral diagonals (K, 2 per panel)", 2 * N, Cn(TrussMemberRole.UpperLateral), 0, "", dec: 0);
    Cmp("bottom lateral diagonals (K, 2 per panel)", 2 * N, Cn(TrussMemberRole.LowerLateral), 0, "", dec: 0);
    int sway = u.four ? 13 : 7;
    Cmp($"sway V frames at {sway} joints (2 diagonals each)", 2 * sway, Cn(TrussMemberRole.Sway), 0, "", dec: 0);
    Cmp("bottom struts at every joint + 10 WF 33 hanger bracing at the other joints", (N + 1) + (N + 1 - sway), Cn(TrussMemberRole.Strut), 0, "", dec: 0);
    Cmp("shoes (one per truss at each pier joint)", 2 * u.piers.Length, tm.Shoes.Count, 0, "", dec: 0);
    Row("INFO", "gusset plates (2 per joint and truss where a chord meets web members)", "-", $"{tm.Gussets}");

    // 3b. floor layout across the bridge (p.48 / p.58 half plans, p.55 floor beam elevation and bearing details)
    Print("--- 3b. floor layout (p.48 / p.58 / p.55)");
    var alU = bm.Framing.Alignment;
    var lines = bdef.Truss!.Floor!.Stringers.Select(s => s.Offset / ft).OrderBy(o => o).ToList();
    Cmp("stringer lines (4 @ 8'-3 each side of the CL + the outside S5 lines)", 11, lines.Count, 0, "", dec: 0);
    Cmp("outside stringer S5 (21 WF 62) from the CL: 33'-0 + 6'-1 (p.55; 10 in. outside the 38'-3 gutter line)", 39 + 1.0 / 12, Math.Max(-lines[0], lines[^1]), 0.0005, " ft", "", 4);
    Cmp("next stringer line 4 @ 8'-3 = 33'-0 (p.48 / p.58)", 33, Math.Max(-lines[1], lines[^2]), 0.0005, " ft", "", 4);
    (double o0, double o1, double z0, double z1) Ext(PartCategory? cat, string mark)
    {
        var p = tm.Steel.Parts.First(q => q.Mark == mark);
        double o0 = 1e9, o1 = -1e9, z0 = 1e9, z1 = -1e9;
        foreach (var v in p.Mesh.Positions) { var so = alU.Inverse(v.XY); o0 = Math.Min(o0, so.Offset); o1 = Math.Max(o1, so.Offset); }
        double tip = o1;
        foreach (var v in p.Mesh.Positions) { var so = alU.Inverse(v.XY); if (so.Offset > tip - 0.02) { z0 = Math.Min(z0, v.Z); z1 = Math.Max(z1, v.Z); } }
        return (o0, o1, z0, z1);
    }
    var brS = Ext(null, "BR-S1");
    Cmp("bracket B1 tip from the CL: 29'-0 + 10'-9 (p.48 / p.58 / p.55) = S5 + 8 in.", 39.75, brS.o1 / ft, 0.0005, " ft", "", 4);
    Cmp("bracket B1 depth at the tip: 2'-0 1/2 b. to b. of angles + 1/2 cover plate (p.55)", 25.0 / 12, (brS.z1 - brS.z0) / ft, 0.01, " ft", "", 4);
    var fbX = Ext(null, "FB1");
    Row("INFO", "floor beam F4 / bracket ends at the truss (p.55: end angles on the post flange; set back 9 1/2 in. from the truss CL, flagged)", "-",
        $"floor beam {fbX.o0 / ft:F3} .. {fbX.o1 / ft:F3} ft, bracket from {brS.o0 / ft:F3} ft");

    // 4. generator checks
    Print("--- 4. member lengths vs the printed geom lengths and generator checks");
    foreach (var c in tm.Checks.Where(c => !c.Item.StartsWith("steel weight")))
        if (c.Ok) pass++; else Row("DIFF", c.Item, c.Plan, c.Model, c.Note);
    Print($"      ({tm.Checks.Count(c => c.Ok && !c.Item.StartsWith("steel weight"))} generator length / shoe checks PASS)");

    // 5. shoes
    Print("--- 5. shoes (bottom of shoe = pier top, p.5 / p.32)");
    var expectedTotal = new Func<int, double>(i => u.types[i] == 'F' ? 4.5833 : (i == 0 || i == u.piers.Length - 1) ? double.NaN : 5.0208);
    for (int i = 0; i < u.piers.Length; i++)
    {
        foreach (var pl in new[] { "N", "S" })
        {
            var sh = tm.Shoes.FirstOrDefault(s => s.Name == $"{pl}-L{J(u.piers[i])}");
            if (sh == null) { Row("DIFF", $"shoe {pl}-L{J(u.piers[i])}", "present", "missing"); continue; }
            Cmp($"shoe {sh.Name} ({bdef.Supports[i].Name}): bottom of shoe", u.tops[i], sh.SeatElevation / ft, 0.001, " ft", "pier top printed on p.5 / p.32", 3);
            Row(sh.Type == (u.types[i] == 'F' ? TrussShoeType.Fixed : TrussShoeType.Expansion) ? "PASS" : "DIFF", $"shoe {sh.Name}: type", u.types[i] == 'F' ? "fixed" : "expansion", sh.Type.ToString());
            if (pl == "N" && !double.IsNaN(expectedTotal(i)))
            {
                double wp = tm.Point("N", "L" + J(u.piers[i]))!.World.Z / ft;
                Cmp($"work point L{J(u.piers[i])} to bottom of shoe (printed {expectedTotal(i) * 12:F2} in.)", expectedTotal(i), wp - sh.SeatElevation / ft, 0.002, " ft", "p.49 5'-0 1/4 rocker / p.52 4'-7 fixed", 4);
            }
            else if (pl == "N")
            {
                double wp = tm.Point("N", "L" + J(u.piers[i]))!.World.Z / ft;
                Row("INFO", $"work point L{J(u.piers[i])} to bottom of the unit-end shoe", "p.59: pin 3'-1 1/2 above the bottom", $"{wp - sh.SeatElevation / ft:F4} ft", "pin 1.463 ft below the work point (derived; 4'-5 5/8 at 3-span ends, 4'-7 1/16 at the 4-span ends)");
            }
        }
    }
    Row("INFO", "bearing seats published for the substructure (Location + axis)", $"{2 * u.piers.Length}", $"{bm.Steel.BearingSeats.Count}");
    // end chords: the chord + gussets run past the shoe pin at L0 / L0' (p.59 ELEVATION, scaled 10 1/2 in.); the shoe must lie under the chord
    foreach (var (pt, mk) in new[] { ("L0", "N-L0-L2"), ($"L{J(N)}", $"N-L{J(N - 2)}-L{J(N)}") })
    {
        var sh = tm.Shoes.First(s => s.Name == $"N-{pt}");
        var ch = tm.Steel.Parts.First(p => p.Mark == mk);
        double pinS = alU.Inverse(sh.Pin.XY).Station;
        var ss = ch.Mesh.Positions.Select(v => alU.Inverse(v.XY).Station).ToList();
        double past = (Math.Abs(ss.Min() - pinS) < Math.Abs(ss.Max() - pinS) ? pinS - ss.Min() : ss.Max() - pinS) / ft;
        Cmp($"chord {mk} end past the shoe pin {pt} (p.59, scaled; flagged)", 10.5 / 12, past, 0.03, " ft", "end cut square to the sloping chord (its corner reaches about 1/4 in. further); the sole plate (22 in.) reaches 11 in. past the pin", 4);
    }
    Cmp("CHECK warnings 'top of the shoe projects beyond the end of chord' (generator)", 0, rep.Warnings.Count(w => w.StartsWith(u.name) && w.Contains("beyond the end of chord")), 0, "", dec: 0);

    // 5b. member material (QC I002 / I003): every truss line member's part material letter = the (C) / (S) letter of its member table row (p.47 / p.57, members.csv)
    Print("--- 5b. member material (C) / (S) by member table row");
    {
        var csvRows = MemberRows().Where(r => r["unit"] == (u.four ? "B" : "A")).ToList();
        int ok = 0, bad = 0, none = 0;
        foreach (var md in bdef.Truss.Members.Where(m => m.Role is TrussMemberRole.UpperChord or TrussMemberRole.LowerChord or TrussMemberRole.Diagonal or TrussMemberRole.Post or TrussMemberRole.Hanger))
        {
            var mm = System.Text.RegularExpressions.Regex.Match(md.Source ?? "", @"member table (?:row (\S+)|\((.+)\))");
            string rn = mm.Success ? (mm.Groups[1].Success ? mm.Groups[1].Value : mm.Groups[2].Value) : "";
            var cr = csvRows.FirstOrDefault(r => r["row"] == rn);
            if (cr == null) { none++; continue; }
            foreach (var pl in new[] { "N", "S" })
            {
                var part = tm.Steel.Parts.FirstOrDefault(p => p.Mark == $"{pl}-{md.Name}");
                if (part == null) { none++; continue; }
                string letter = part.Material.Contains("silicon") ? "S" : part.Material.Contains("carbon") ? "C" : "?";
                if (letter == cr["material"]) ok++;
                else { bad++; Row("DIFF", $"material of {part.Mark} (row {rn}, {cr["section"]})", cr["material"], letter); }
            }
        }
        Cmp($"truss line members whose material letter differs from the member table row ({ok} agree, {none} without a row / part)", 0, bad, 0, "", dec: 0);
    }

    // 5c. gross area of each member section at mid-member vs the printed "GROSS SQ. IN." of its table row (QC pass 2: I009 cover plates, I017 1954 14 WF areas);
    // cover plates stop at the end gussets (p.60), so the member's mean area (volume / length) is lower - reported as INFO.
    Print("--- 5c. member section gross area (mid-member) vs the member table row");
    {
        var csvRows = MemberRows().Where(r => r["unit"] == (u.four ? "B" : "A") && r["area_sqin"] != "").ToList();
        var seen = new HashSet<string>();
        foreach (var md in bdef.Truss.Members.Where(m => m.Role is TrussMemberRole.UpperChord or TrussMemberRole.LowerChord or TrussMemberRole.Diagonal or TrussMemberRole.Post or TrussMemberRole.Hanger))
        {
            var mm = System.Text.RegularExpressions.Regex.Match(md.Source ?? "", @"member table (?:row (\S+)|\((.+)\))");
            string rn = mm.Success ? (mm.Groups[1].Success ? mm.Groups[1].Value : mm.Groups[2].Value) : "";
            var cr = csvRows.FirstOrDefault(r => r["row"] == rn);
            if (cr == null || !seen.Add(rn)) continue;
            var sd = bdef.Truss.Sections.First(x => x.Name == md.Section);
            double a = SBM.Core.Truss.TrussSections.Build(sd).Area / 0.00064516;
            double pa = double.Parse(cr["area_sqin"], inv);
            Cmp($"gross area {rn} ({md.Section})", pa, a, 0.01 * pa, " sq in", "", 2);
            if (cr["plates"] != "")
            {
                var part = tm.Steel.Parts.FirstOrDefault(p => p.Mark == $"N-{md.Name}");
                var res = tm.Members.FirstOrDefault(x => x.Mark == $"N-{md.Name}");
                if (part != null && res != null) Row("INFO", $"mean area {part.Mark} (volume / solid length; cover plates end at the gussets)", cr["area_sqin"] + " sq in", $"{part.Mesh.Volume() / res.CutLength / 0.00064516:F2} sq in");
            }
        }
    }

    // 5d. gusset layers per face (gusset-layers.csv, p.49 / 52 / 59 / 63 / 67): stacked joints by name, every other joint one plate per face
    Print("--- 5d. gusset layers (plates per face, total thickness)");
    {
        var stack = new Dictionary<string, (string key, int n, double tot, string pg)>();
        foreach (var r in LayerRows().Where(r => r["structure"].StartsWith(u.four ? "approach4" : "approach3") || (!u.four && r["joint"] == "L0'")))
        {
            if (!r["position"].StartsWith("inner")) continue;
            string dj = r["joint"];
            int n = int.Parse(r["layers"]);
            double tot = LayerRows().Where(x => x["structure"] == r["structure"] && x["joint"] == dj).Sum(x => Frac(x["thickness_in"]));
            string[] mj = dj == "L0'" ? new[] { "L0", "L0'" } : dj == "L0" ? (u.four ? new[] { "L0", "L0'" } : new string[0]) : dj == "L20" ? new[] { "L20" } : new[] { dj, dj + "'" };
            foreach (var j in mj) stack[j] = (dj, n, tot, r["page"]);
        }
        int single = 0, singleOk = 0;
        foreach (var gs in tm.GussetStacks.OrderBy(g => g.Plane).ThenBy(g => g.Point))
        {
            if (stack.TryGetValue(gs.Point, out var e))
            {
                Cmp($"gusset layers per face {gs.Plane} {gs.Point} (p.{e.pg}, data joint {e.key})", e.n, gs.Layers, 0, "", $"model {string.Join(" + ", gs.Thicknesses.Select(t => (t / 0.0254).ToString("0.###", inv)))} in.", 0);
                Cmp($"gusset stack thickness {gs.Plane} {gs.Point} (p.{e.pg})", e.tot, gs.TotalThickness / 0.0254, 1.0 / 64, " in", "", 4);
                for (int k = 1; k < gs.Layers; k++) Row("INFO", $"gusset layer {k + 1} area {gs.Plane} {gs.Point} (outline scaled, clipped to the inner gusset envelope)", "-", $"{gs.Areas[k] / 0.09290304:F1} SF per plate (inner {gs.Areas[0] / 0.09290304:F1} SF)");
            }
            else { single++; if (gs.Layers == 1) singleOk++; }
        }
        Cmp("joints with ONE gusset per face (all joint sheets re-checked 2026-10-20)", single, singleOk, 0, "", "", 0);

        // gusset outlines (gusset-outlines.csv, connections-ptc.csx 4b) and rivet cover: every joint rivet inside its inner gusset with its edge distance
        {
            var tdef = Doc.Project.Bridges.First(b => b.Name == u.name).Truss!;
            var withOutline = tm.GussetStacks.Where(g => tdef.Joints.Any(j => j.Point == g.Point && j.Outline.Count >= 3)).ToList();
            Row("INFO", "joints with a gusset outline read from the sheet (rest: envelope of the member laps)", "-", $"{withOutline.Count} of {tm.GussetStacks.Count} joint x plane; inner plates {withOutline.Sum(g => g.Areas[0]) / 0.09290304:N0} SF per face (outlines) + {tm.GussetStacks.Except(withOutline).Sum(g => g.Areas[0]) / 0.09290304:N0} SF (envelopes)");
            foreach (var rc in tm.GussetRivetCovers.OrderBy(c => c.Plane).ThenBy(c => c.Point))
            {
                // rivets placed AS DRAWN (QC I052): positions and outline are both read from the sheet (+-1 in.): every drawn rivet within 1 in. of the inside of the drawn outline
                if (rc.Drawn > 0)
                    Row(rc.MinEdge >= -0.0254 ? "PASS" : "DIFF", $"gusset covers its rivets {rc.Plane} {rc.Point} (rivets as drawn, outline from the sheet)", "every drawn rivet inside the drawn outline (both read from the sheet, +-1 in.)",
                        $"{rc.Rivets} rivets per face ({rc.Drawn} as drawn), smallest edge distance {rc.MinEdge / 0.0254:0.##} in., {rc.Short} closer than {rc.RequiredEdge / 0.0254:0.##} in.");
                else
                    Row(rc.Ok ? "PASS" : "DIFF", $"gusset covers its rivets {rc.Plane} {rc.Point} ({(rc.OutlineGiven ? "outline from the sheet" : "envelope")})", $"every rivet >= {rc.RequiredEdge / 0.0254:0.##} in. from the edge",
                        $"{rc.Rivets} rivets per face, smallest edge distance {rc.MinEdge / 0.0254:0.##} in.{(rc.Short > 0 ? $", {rc.Short} short / outside" : "")}");
            }
        }
    }

    // 6. weights
    Print("--- 6. weights (p.42 items 2 + 5, one unit)");
    var structural = tm.Steel.Parts.Where(p => p.Category is not (PartCategory.TrussShoeFixed or PartCategory.TrussShoeExpansion or PartCategory.TrussPin)).ToList();
    double carbon = structural.Where(p => p.Material.Contains("carbon")).Sum(p => p.Mass) * lb, silicon = structural.Where(p => p.Material.Contains("silicon")).Sum(p => p.Mass) * lb;
    double other = structural.Where(p => !p.Material.Contains("carbon") && !p.Material.Contains("silicon")).Sum(p => p.Mass) * lb;
    const string why = "not modeled: splice / fill plates, diaphragms, hand-hole details, rivet heads, partial cover plates; shoes excluded; 1954 14 WF dimensions interpolated; perforated struts solid";
    Cmp("structural carbon steel (C)", u.carbon, carbon, 0.05 * u.carbon, " lb", why, 0);
    Cmp("structural silicon steel (S)", u.silicon, silicon, 0.05 * u.silicon, " lb", why, 0);
    Cmp("structural steel C + S", u.carbon + u.silicon, carbon + silicon + other, 0.05 * (u.carbon + u.silicon), " lb", $"{(carbon + silicon + other) / (u.carbon + u.silicon):P1} of the plan quantity", 0);
    foreach (var g in structural.GroupBy(p => p.Category).OrderBy(g => g.Key)) Row("INFO", $"weight {g.Key}", "-", $"{g.Sum(p => p.Mass) * lb:N0} lb ({g.Count()} parts)");
    if (bm.Concrete?.Deck is { } dk) Row("INFO", "deck slab 6 3/4 between the curb lines", "-", $"{Math.Abs(dk.Volume()) / 0.764554857984:N0} CY");

    var warns = rep.Warnings.Where(w => w.StartsWith(u.name)).ToList();
    Print($"--- 7. {warns.Count} warnings ({warns.Count(w => w.Contains("UNRESOLVED"))} UNRESOLVED, {warns.Count(w => w.Contains("CHECK"))} CHECK)");
    foreach (var w in warns.Where(w => w.Contains("CHECK") && !w.Contains("steel weight")).Take(8)) Print("  " + (w.Length > 230 ? w.Substring(0, 230) : w));
}
Print($"SUMMARY: {pass} PASS / {diff} DIFF / {info} INFO");
