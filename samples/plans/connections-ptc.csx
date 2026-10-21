// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - VISIBLE STEEL CONNECTIONS on the truss-generator bridges of the PTC model:
//   main river spans 18-20: gusset thickness + in-to-in width per joint (gussets.csv), chord splice plates / web splice plates / fills (splices.csv)
//   girder spans 1-10 / 28-31: end bearing stiffeners, intermediate stiffener angles, floor beam stiffeners, mid-span web splice plates (girder-details.csv)
// Generic inputs used: TrussJointDefinition.GussetThickness / GussetsInsideFaceDistance / Plates, TrussMemberDefinition.Stiffeners.
// Re-runnable; run AFTER truss-ptc-arch.csx, truss-ptc-approach.csx, girders-ptc.csx (they rebuild Truss) and deck-ptc.csx:
//   sbm run samples\plans\connections-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Data: docs/plan-data/ptc-delaware-river/connections/*.csv (every row names its page and confidence). Rules: as-built set only; values the sheets do not
// give are UNRESOLVED flags (page + plain reason); scaled extents are flagged as such; differences reported, never adjusted. Rivets are NOT placed
// (counts are image estimates, pitches / gages per group not read; ~10^5 instances would also load the viewport).
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
var inv = CultureInfo.InvariantCulture;
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double FI(double ft, double inch) => Ft(ft) + In(inch);

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "connections");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/ptc-delaware-river/connections not found.");
List<Dictionary<string, string>> Csv(string name)
{
    var lines = File.ReadAllLines(Path.Combine(dataDir, name));
    List<string> Split(string line)
    {
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = false; } else cur.Append(c); }
            else if (c == '"') q = true;
            else if (c == ',') { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cells.Add(cur.ToString());
        return cells;
    }
    var head = Split(lines[0]);
    return lines.Skip(1).Where(l => l.Trim() != "").Select(l => { var c = Split(l); return head.Select((h, i) => (h, v: i < c.Count ? c[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
double? Num(string s) => double.TryParse(s, NumberStyles.Float, inv, out var v) ? v : null;
var report = new List<string>();

// ================================================================ 1. main river spans: gussets + chord splices
{
    var br = Doc.Project.Bridges.First(b => b.Name == "PTC Arch truss 18-20");
    var t = br.Truss!;
    var gus = Csv("gussets.csv").Where(r => r["structure"] == "main").ToList();
    int gSet = 0, gConf = 0;
    foreach (var r in gus)
    {
        var th = Num(r["gusset_thk_in"]); var w = Num(r["in_to_in_in"]);
        if (th == null) continue;
        string j = r["joint"].Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(j, @"^[UL]\d+$")) continue;
        int n = int.Parse(j.Substring(1));
        foreach (bool pr in new[] { false, true })
        {
            if (pr && n == 22) continue;
            string pt = j[0] + (pr ? n + "'" : n.ToString());
            var jd = t.Joints.LastOrDefault(x => x.Point == pt);
            if (jd == null) { jd = new TrussJointDefinition { Point = pt }; t.Joints.Add(jd); }
            if (jd.GussetThickness > 0 && Math.Abs(jd.GussetThickness - In(th.Value)) > In(1.0 / 32)) { report.Add($"CONFLICT gusset {pt}: joints.csv {jd.GussetThickness / 0.0254:0.###} in vs gussets.csv {th:0.###} in (p.{r["page"]}) - gussets.csv used"); gConf++; }
            jd.GussetThickness = In(th.Value);
            if (w is > 0) jd.GussetsInsideFaceDistance = In(w.Value);
            jd.Source = (jd.Source.Length > 0 ? jd.Source + "; " : "") + $"gussets.csv p.{r["page"]} ({r["confidence"]}): {th:0.###} in, {(w is > 0 ? w + " in in-to-in" : "in-to-in not read")}";
            if (r["confidence"] != "high")
                jd.Unresolved = U("p." + r["page"], $"gusset plates of joint {pt}", $"thickness / in-to-in read from the joint sheet ({r["confidence"]} confidence); the outline is drawn, not dimensioned", "thickness and in-to-in as read; outline = convex envelope of the member laps + 2'-6.");
            gSet++;
        }
    }
    report.Add($"main truss: gusset thickness / in-to-in set on {gSet} joints ({gConf} conflicts with joints.csv)");

    // chord splices: rows with a width, thickness and (scaled) length; faces from the callout text
    var spl = Csv("splices.csv").Where(r => r["structure"] == "main").ToList();
    int nPl = 0, nSkip = 0;
    var skipped = new List<string>();
    foreach (var j in t.Joints) j.Plates = null;
    foreach (var r in spl)
    {
        string j = r["joint"].Trim(), item = r["item"].ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(j, @"^[UL]\d+$")) continue;
        var wid = Num(r["width_in"]); var thk = Num(r["thk_in"]); var len = Num(r["length_in"]);
        if (wid == null || thk == null || len == null || len <= 0)
        {
            if (!item.Contains("angles") && !item.Contains("vertical connection")) { nSkip++; skipped.Add($"{j} {r["item"]}"); }
            continue;
        }
        bool fill = item.Contains("fill");
        bool inlay = item.Contains("inlay");
        if (inlay) { nSkip++; skipped.Add($"{j} {r["item"]} (inlay: lies in the web ply, not an added plate)"); continue; }
        var faces = new List<TrussPlateFace>();
        // a single cover fill lies on the flange its callout names (QC I043: U1 "Fill 24 x 5/16" under the HALF BOTTOM FLANGE)
        if (item.Contains("cover") || item.Contains("flange"))
        {
            if (fill && r["qty"] == "1") faces.Add(item.Contains("bottom") ? TrussPlateFace.Bottom : TrussPlateFace.Top);
            else { faces.Add(TrussPlateFace.Top); faces.Add(TrussPlateFace.Bottom); }
        }
        else if (item.Contains("web") || item.Contains("splice plates") || item.Contains("fills"))
            faces.Add(item.Contains("outside") || item.Contains("o.s.") ? TrussPlateFace.WebOutside : TrussPlateFace.WebInside);
        else { nSkip++; skipped.Add($"{j} {r["item"]} (face not recognised)"); continue; }
        // along the chord: centred on the joint, or to one side ("left" = toward the lower panel number)
        var offs = new List<double>();
        if (item.Contains("each side")) { offs.Add(-1); offs.Add(1); }
        else if (item.Contains("left")) offs.Add(-1);
        else if (item.Contains("right")) offs.Add(1);
        else offs.Add(0);
        int n = int.Parse(j.Substring(1));
        string conf = r["confidence"];
        foreach (bool pr in new[] { false, true })
        {
            if (pr && n == 22) continue;
            string pt = j[0] + (pr ? n + "'" : n.ToString());
            var jd = t.Joints.LastOrDefault(x => x.Point == pt);
            if (jd == null) { jd = new TrussJointDefinition { Point = pt }; t.Joints.Add(jd); }
            jd.Plates ??= new List<TrussJointPlate>();
            foreach (var f in faces)
                foreach (double o in offs)
                {
                    // PA half: the lower panel number is down-station (left = -); NJ half (primes): mirrored
                    double sgn = pr ? -o : o;
                    jd.Plates.Add(new TrussJointPlate
                    {
                        Face = f, Width = In(wid.Value), Thickness = In(thk.Value), Length = In(len.Value), Offset = sgn * In(len.Value) / 2, Fill = fill,
                        Label = r["item"], Material = r["note"].Contains("(S)") ? "S" : "C", Source = $"p.{r["page"]} ({conf})",
                        Unresolved = U("p." + r["page"], $"{r["item"]} at {pt}", $"the length is scaled from the joint sheet ({r["extent_note"]}; {conf} confidence) and the plate order in the stack is drawn, not dimensioned",
                            "plate as listed, length as scaled, " + (o == 0 ? "centred on the joint" : "from the joint toward " + (o < 0 ? "the lower" : "the higher") + " panel number") + ", stacked on the steel under it (web, angle legs or the fills already placed)."),
                    });
                    nPl++;
                }
        }
    }
    report.Add($"main truss: {nPl} splice / fill plate definitions (both halves; WebInside / WebOutside = one plate per web); {nSkip} rows not built (no length / inlay): {string.Join("; ", skipped.Take(12))}{(skipped.Count > 12 ? "; ..." : "")}");
    // the arch script's general flag on splices is now partly built: replace it
    t.Unresolved.RemoveAll(s => s.Contains("chord splice plates, inlay plates, fills"));
    t.Unresolved.Add(U("p.69, p.74-90", "inlay plates, lug angles, stiffeners, diaphragms, stitch rivets and the splice plates whose extent was not read",
        $"they are listed per joint without extents ({nSkip} splice / fill callouts have no length in splices.csv)", "chord cover / web splice plates and fills with a scaled length are built (connections-ptc.csx); the rest is not built."));
}

// ================================================================ 2. girder spans: stiffeners and the mid-span web splice (p.43 / p.44)
foreach (var name in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var br = Doc.Project.Bridges.First(b => b.Name == name);
    var t = br.Truss!;
    var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
    var tm = Doc.Model(name).Steel?.Truss;
    if (tm == null) { report.Add($"{name}: no truss model"); continue; }
    double hStiff = FI(9, 6.5) - 2 * In(1.125);    // between the outstanding legs of the flange angles (9'-6 1/2 b.b. Ls - 2 x 1 1/8)
    double hSplice = In(114.5 - 2 * 8 - 0.5);        // between the vertical legs of the flange angles (UNRESOLVED: not printed)
    string stiffFlag = U("p.43, p.44", "stiffener layout", "the elevation gives the stiffeners as equal spaces per floor beam panel (9 / 6 / 6 / 9) and the sizes (end 4-Ls 7x4x3/4 + 2-Fills 15x1 1/8, intermediate 2-Ls 6x4x7/16 crimped, 2-Ls 6x4x1/2 at the floor beams) without station dimensions; fills, crimps and the longitudinal stiffener L 6x4x7/16 are drawn only",
        "end stiffeners 2 angles per web face at the CL bearing, intermediate pairs at the equal spaces of each panel between the CL bearing / floor beam lines, 6x4x1/2 pairs at the floor beams; height between the flange angle outstanding legs (9'-4 1/4); fills / crimps / long stiffener not built.");
    string splFlag = U("p.44", "mid-span web splice", "2-Splice Pls 21 x 3/8 are printed centred on the splice; their height is drawn between the flange angle legs, not dimensioned; the flange-angle splice plates 6 1/4 x 3/8 are not built", "21 x 3/8 plate each web face, 8'-2 high (114 1/2 - 2 x 8 - 1/2), centred at mid-length.");
    int nG = 0;
    foreach (var m in t.Members.Where(mm => mm.Role == TrussMemberRole.Girder))
    {
        var res = tm.Members.Where(x => x.Name == m.Name).ToList();
        if (res.Count == 0) continue;
        var r0 = res[0];
        double L3 = (r0.B - r0.A).Length;
        double s0 = al.Inverse(r0.A.XY).Station, s1 = al.Inverse(r0.B.XY).Station;
        double D(double sta) => (sta - s0) / (s1 - s0) * L3;
        double bA = s0 + FI(1, 1), bB = s1 - FI(1, 1);                 // CL bearings 1'-1 inside the girder ends (p.44)
        double pitch = (bB - bA) / 4;
        var fbs = Enumerable.Range(1, 3).Select(k => bA + k * pitch).ToList();
        var inter = new List<double>();
        int[] spaces = { 9, 6, 6, 9 };
        for (int p = 0; p < 4; p++)
            for (int i = 1; i < spaces[p]; i++) inter.Add(bA + p * pitch + i * pitch / spaces[p]);
        m.Stiffeners = new List<TrussStiffenerSet>
        {
            new() { Label = "end stiffeners 4-Ls 7x4x3/4", Angle = "L7x4x3/4", PerFace = 2, Height = hStiff, Distances = new() { D(bA), D(bB) }, Material = "S", Source = "p.44", Unresolved = stiffFlag },
            new() { Label = "stiffeners 2-Ls 6x4x1/2 at the floor beams", Angle = "L6x4x1/2", PerFace = 1, Height = hStiff, Distances = fbs.Select(D).ToList(), Material = "C", Source = "p.44", Unresolved = stiffFlag },
            new() { Label = "intermediate stiffeners 2-Ls 6x4x7/16", Angle = "L6x4x7/16", PerFace = 1, Height = hStiff, Distances = inter.Select(D).ToList(), Material = "C", Source = "p.44", Unresolved = stiffFlag },
            new() { Label = "web splice 2-Pls 21 x 3/8", PlateWidth = In(21), PlateThickness = In(0.375), Height = hSplice, Distances = new() { L3 / 2 }, Material = "S", Source = "p.44", Unresolved = splFlag },
        };
        nG++;
    }
    var g1 = t.Section("G1");
    if (g1 != null) g1.Unresolved = U("p.43, p.44", "full length cover plate splice, flange angle splice plates, fills, longitudinal stiffener, rivets",
        "the cover plate splice (CL 37'-0 from the CL girder + 6'-0 additional material), the 6 1/4 x 3/8 flange angle splice plates, the stiffener fills / crimps, the long stiffener L 6x4x7/16 and the rivet patterns are drawn / listed but not dimensioned station by station",
        "built: web, 4 flange angles, the cover plates, stiffeners and the web splice plates (connections-ptc.csx); not built: the items listed.");
    report.Add($"{name}: stiffeners + web splice on {nG} girder members");
}

// ================================================================ 3. stringer diaphragms 15 [ 33.9 at mid-panel (p.43 note, p.48 / p.58, p.94): one channel across all stringer lines
{
    var ch = SteelCatalog.FindChannel("C15x33.9");
    foreach (var br in Doc.Project.Bridges.Where(b => b.Name.StartsWith("PTC ") && b.Truss?.Floor != null))
    {
        var t = br.Truss!;
        var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
        var tm = Doc.Model(br.Name).Steel?.Truss;
        if (tm == null || ch == null) continue;
        t.LineItems ??= new();
        t.LineItems.RemoveAll(li => li.Name.StartsWith("diaphragm 15[33.9"));
        var fbSta = tm.Members.Where(m => m.Role == TrussMemberRole.FloorBeam).Select(m => al.Inverse(((m.A + m.B) * 0.5).XY).Station).OrderBy(s => s).ToList();
        var stas = new List<double>();
        foreach (var s in fbSta) if (stas.Count == 0 || s - stas[^1] > 0.5) stas.Add(s);
        double o0 = t.Floor!.Stringers.Min(s => s.Offset), o1 = t.Floor.Stringers.Max(s => s.Offset);
        double top = t.Floor.StringerTopBelowGrade + In(3);
        var prof = new List<TrussUV>
        {
            new() { U = -ch.FlangeWidth / 2, V = -top }, new() { U = ch.FlangeWidth / 2, V = -top }, new() { U = ch.FlangeWidth / 2, V = -top - ch.FlangeThickness },
            new() { U = -ch.FlangeWidth / 2 + ch.WebThickness, V = -top - ch.FlangeThickness }, new() { U = -ch.FlangeWidth / 2 + ch.WebThickness, V = -top - ch.Depth + ch.FlangeThickness },
            new() { U = ch.FlangeWidth / 2, V = -top - ch.Depth + ch.FlangeThickness }, new() { U = ch.FlangeWidth / 2, V = -top - ch.Depth }, new() { U = -ch.FlangeWidth / 2, V = -top - ch.Depth },
        };
        int k = 0;
        string flag = U("p.43, p.48, p.58, p.94", "stringer diaphragms 15 [ 33.9", "the notes place them at the centre of each floor panel (and at the abutments / piers, 18 WF 64 at the girder-span piers) without their height or connection angles",
            "one 15 [ 33.9 between each pair of adjacent stringer webs (web face to web face) at mid-panel, top 3 in. below the stringer tops, web vertical; connection angles, the end / pier diaphragms and the 18 WF 64 not built.");
        // QC I021: the channels frame BETWEEN the stringers (p.94 elevation: 15[ 33.9 typical diaph. with L 4 x 3 1/2 x 5/16 connection angles on the stringer webs);
        // one continuous channel crossed every stringer web. Bays between adjacent stringer lines, ends at the web faces.
        var lines = t.Floor.Stringers.OrderBy(s => s.Offset).Select(s => (s.Offset, tw: t.Section(s.Section) is { } sd ? SBM.Core.Truss.TrussSections.Build(sd).WebThickness : 0.0)).ToList();
        for (int i = 0; i + 1 < stas.Count; i++)
        {
            double gap = stas[i + 1] - stas[i];
            if (gap < Ft(16) || gap > Ft(40)) continue;     // floor panels only (not the dam gaps or span gaps)
            double sm = (stas[i] + stas[i + 1]) / 2;
            k++;
            for (int j = 0; j + 1 < lines.Count; j++)
                t.LineItems.Add(new TrussLineItemDefinition
                {
                    Name = $"diaphragm 15[33.9 #{k}{(char)('a' + j)}", Kind = TrussLineItemKind.Steel, Category = PartCategory.Channel, Transverse = true, Station = sm,
                    From = lines[j].Offset + lines[j].tw / 2, To = lines[j + 1].Offset - lines[j + 1].tw / 2,
                    Profile = prof, NominalWeightLbPerFt = 33.9, Material = "C", Assembly = "FLOOR", AssemblyDescription = $"{t.Name} - floor system", Source = "p.43 / p.48 / p.58 / p.94", Unresolved = flag,
                });
        }
        report.Add($"{br.Name}: {k} mid-panel stringer diaphragms 15[33.9");
        // the floor flag of the truss / girder scripts says "diaphragms not built": now only partly true
        if (k > 0) t.Floor.Unresolved = t.Floor.Unresolved.Replace("diaphragms not built", "diaphragms not built (except the mid-panel 15 [ 33.9, connections-ptc.csx)");
    }
}

// ================================================================ 4. shoe body material (shoes.csv): built up of carbon steel plates (p.44 girder shoes "all carbon steel C",
// p.49 / 52 / 59 / 63 / 67 approach shoes: stem, wing, stiffener plates and rockers (C), sole plates (S); p.73 main L0 rocker shoes (C)); only the main L11 / L11'
// shoes at piers 18 / 19 are a steel forging / casting (p.96). The envelope body is carbon steel then (p.42 item 9 lists no castings for the approach units).
int shoeC = 0;
foreach (var br in Doc.Project.Bridges.Where(b => b.Name.StartsWith("PTC ") && b.Truss != null))
    foreach (var sh in br.Truss!.Shoes)
    {
        bool castMain = br.Name.StartsWith("PTC Arch") && sh.Point.StartsWith("L11");
        sh.BodyMaterial = castMain ? null : "C";
        if (!castMain) shoeC++;
    }
report.Add($"shoe bodies of built-up carbon steel plates: {shoeC} (main L11 / L11' stay cast steel, p.96)");

// ================================================================ 4b. gusset plate OUTLINES (gusset-outlines.csv, 2026-10-20): the inner gusset of every drawn joint read from the joint
// sheets (tools/ptc_gusset_outlines.py: polygon + work point + chord direction in page pixels, scale from a printed dimension) in the sheet frame (u = drawing x =
// toward the higher panel number, v up, origin = work point, inches). Placed on the model joint by a VERTICAL SHEAR v += u (tan(model chord) - tan(drawn chord)) (mean
// direction of the chords at the point), then mirrored for the primed half of a unit and for the New Jersey units: the sheets draw one half of a unit on its grade with plumb
// verticals; the other half is the mirror image in member layout but lies on the same grade, so a mirror alone would tilt the chord edge by twice the grade and a rotation
// would tilt the plumb post edges - the shear keeps the chord edges on the chord and the post edges plumb (it also absorbs the small scan skew of the drawn chord).
// Joints the sheets give as "details not shown, similar to X" take X's outline (flagged). Joints without a drawing keep the envelope (JointDefaults flag).
// The shear is kept per (bridge, joint) for the stacked layers of section 5.
var jointShear = new Dictionary<(string bridge, string joint), double>();
{
    string dataRoot = Path.GetDirectoryName(dataDir)!;
    var rows = Csv("gusset-outlines.csv");
    // structure key: main / A (3-span) / B (4-span)
    string Key(Dictionary<string, string> r) => r["structure"] == "main" ? "main" : r["structure"].Contains("unit A") ? "A" : "B";
    var own = new Dictionary<(string, string), Dictionary<string, string>>();
    foreach (var r in rows) own[(Key(r), r["joint"].Trim())] = r;
    // similar-to references: the readers' also_applies_to + the approach joints.csv notes ("similar to U2 sheet 20": sheet n = PDF page n + 40)
    var similar = new Dictionary<(string, string), ((string, string) src, string why)>();
    foreach (var r in rows)
        foreach (var a in r["also_applies_to"].Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().Split(' ')[0]).Where(x => System.Text.RegularExpressions.Regex.IsMatch(x, @"^[UL]\d+'?$")))
            if (!own.ContainsKey((Key(r), a))) similar[(Key(r), a)] = ((Key(r), r["joint"].Trim()), $"p.{r["page"]}: details not shown, similar to {r["joint"]}");
    string UnitOfPage(int pg, string joint) => pg >= 49 && pg <= 53 ? "A" : pg >= 60 && pg <= 67 ? "B" : pg == 59 ? (joint.EndsWith("'") ? "A" : "B") : "main";
    var apRows = new List<Dictionary<string, string>>();
    {
        var lines = File.ReadAllLines(Path.Combine(dataRoot, "truss-approach", "joints.csv"));
        var head = lines[0].Split(',');
        foreach (var l in lines.Skip(1))
        {
            var m = System.Text.RegularExpressions.Regex.Match(l, @"similar to ([UL]\d+'?)(?:[^,]*?sheet (\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var m2 = System.Text.RegularExpressions.Regex.Match(l, @"see ([UL]\d+) sheet (\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success && !m2.Success) continue;
            var mm = m.Success ? m : m2;
            string unit = l.Split(',')[0], joint = l.Split(',')[1];
            string srcUnit = mm.Groups[2].Success ? UnitOfPage(int.Parse(mm.Groups[2].Value) + 40, mm.Groups[1].Value) : unit;
            var tgt = (unit, joint);
            if (!own.ContainsKey(tgt) && !similar.ContainsKey(tgt))
                similar[tgt] = ((srcUnit, mm.Groups[1].Value), $"truss-approach/joints.csv: {mm.Value}{(mm.Groups[2].Success ? "" : " (same unit)")}");
        }
    }
    List<(double u, double v)> UV(string s) => s.Split(';').Where(p => p.Trim().Length > 0).Select(p => p.Split(',')).Select(a => (double.Parse(a[0], inv), double.Parse(a[1], inv))).ToList();
    // mean chord direction at a point in the sheet-like frame (u' = sign x model u), degrees in (-90, 90]
    double? ChordDeg(TrussDefinition t, string joint, int sign)
    {
        var P = t.Points.FirstOrDefault(p => p.Name == joint);
        if (P == null) return null;
        double sx = 0, sy = 0;
        foreach (var m in t.Members.Where(m => (m.Role == TrussMemberRole.UpperChord || m.Role == TrussMemberRole.LowerChord) && !m.From.Contains(':') && (m.From == joint || m.To == joint)))
        {
            var Q = t.Points.FirstOrDefault(p => p.Name == (m.From == joint ? m.To : m.From));
            if (Q == null) continue;
            double dx = Q.Station - P.Station, dy = Q.Elevation - P.Elevation;
            if (dx < 0) { dx = -dx; dy = -dy; }
            double l = Math.Sqrt(dx * dx + dy * dy); if (l < 1e-9) continue;
            sx += dx / l; sy += dy / l;
        }
        if (sx == 0 && sy == 0) return null;
        double ux = sign * sx, uy = sy;
        if (ux < 0) { ux = -ux; uy = -uy; }
        return Math.Atan2(uy, ux) * 180 / Math.PI;
    }
    var units = new[] { ("PTC Arch truss 18-20", "main", 1), ("PTC 3-span truss 11-13", "A", 1), ("PTC 4-span truss 14-17", "B", 1), ("PTC 4-span truss 21-24", "B", -1), ("PTC 3-span truss 25-27", "A", -1) };
    int nOwn = 0, nSim = 0, nNone = 0; var none = new List<string>(); var bigRot = new List<string>(); double maxShear = 0;
    foreach (var (bn, key, dir) in units)
    {
        var t = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn)?.Truss;
        if (t == null) { report.Add($"gusset outlines: no truss bridge '{bn}' - skipped"); continue; }
        foreach (var j in t.Joints) j.Outline = new();
        var names = t.Points.Select(p => p.Name).Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^[UL]\d+'?$")).Distinct().ToList();
        int maxK = names.Select(n => int.Parse(n.TrimEnd('\'').Substring(1))).DefaultIfEmpty(0).Max();
        foreach (var mj in names)
        {
            if (t.Joints.Any(x => x.Point == mj && x.NoGusset)) continue;
            int k = int.Parse(mj.TrimEnd('\'').Substring(1));
            string ul = mj.Substring(0, 1);
            // data joint: unit ends of the approach trusses are drawn on p.59 (3-span: its high end U0' / L0'; 4-span: U0 / L0)
            string dj = key == "A" && k == 0 ? ul + "0'" : ul + k;
            int sign = (mj.EndsWith("'") == dj.EndsWith("'")) ? dir : -dir;
            Dictionary<string, string> r = null; string simWhy = "";
            if (own.TryGetValue((key, dj), out var r0)) r = r0;
            else if (similar.TryGetValue((key, dj), out var sm) && own.TryGetValue(sm.src, out var r1)) { r = r1; simWhy = sm.why; }
            if (r == null) { if (t.Joints.Any(x => x.Point == mj)) { nNone++; none.Add($"{bn.Replace("PTC ", "")} {mj}"); } continue; }
            if (!t.Joints.Any(x => x.Point == mj)) t.Joints.Add(new TrussJointDefinition { Point = mj });
            double drawn = double.Parse(r["drawn_chord_deg"], inv);
            double model = ChordDeg(t, mj, sign) ?? drawn;
            double shear = Math.Tan(model * Math.PI / 180) - Math.Tan(drawn * Math.PI / 180);
            if (Math.Abs(model - drawn) > 2.5) bigRot.Add($"{bn.Replace("PTC ", "")} {mj}: drawn {drawn:0.0} deg vs model {model:0.0} deg");
            maxShear = Math.Max(maxShear, Math.Abs(shear));
            jointShear[(bn, mj)] = shear;
            var outline = UV(r["outline_uv_in"]).Select(q => new TrussUV(sign * In(q.u), In(q.v + shear * q.u))).ToList();
            string pg = "p." + r["page"];
            string oText = U(pg, $"gusset plate outline of joint {mj}" + (simWhy.Length > 0 ? $" (taken from joint {r["joint"]}: {simWhy})" : ""),
                $"the plate is drawn, not dimensioned: outline scaled from a {r["dpi"]} dpi render ({r["confidence"]} confidence, about +-1 in.; evidence: {r["evidence"]})"
                + (simWhy.Length > 0 ? "; the sheet gives this joint only as similar to the drawn one" : ""),
                "outline as scaled, sheared onto the model chord (verticals kept plumb)" + (sign < 0 ? ", mirrored" : "") + ".");
            foreach (var jd in t.Joints.Where(x => x.Point == mj))
            {
                jd.Outline = outline;
                // keep the thickness reading flag of the approach joints; drop the old envelope wording
                string old = jd.Unresolved.Contains("convex envelope") ? "" : jd.Unresolved;
                jd.Unresolved = oText + (old.Length > 0 ? " " + old : "");
                jd.Source = System.Text.RegularExpressions.Regex.Replace(jd.Source, @"; outline gusset-outlines\.csv.*$", "") + $"; outline gusset-outlines.csv p.{r["page"]} joint {r["joint"]} ({r["confidence"]})";
            }
            if (simWhy.Length > 0) nSim++; else nOwn++;
        }
    }
    report.Add($"gusset outlines: {nOwn} model joints from their own drawing, {nSim} from a 'similar to' joint, {nNone} without an outline (envelope kept): {string.Join(", ", none.Take(30))}{(none.Count > 30 ? ", ..." : "")}");
    if (bigRot.Count > 0) report.Add($"gusset outlines: drawn vs model chord angle differs > 2.5 deg at {bigRot.Count} joints (approach NJ / primed halves: the sheets draw the other half on its grade; main: reading of the drawn chord; vertical shear applied, largest {Math.Atan(maxShear) * 180 / Math.PI:0.0} deg): {string.Join("; ", bigRot.Take(12))}");
}

// ================================================================ 5. stacked gusset layers (gusset-layers.csv, 2026-10-20): a second (third) gusset plate per face OUTSIDE the
// inner gusset at approach L0 / L0' / L8 / L10 / L20 and main L10 / L11 / L22. Outlines are scaled from the joint sheets in the sheet frame (u = drawing x = toward the
// higher panel number, v up, origin = work point, inches) -> mirrored for the primed half of a unit and for the New Jersey units (stations decreasing with the panel number).
{
    var lay = Csv("gusset-layers.csv").Where(r => r["layers"] != "1").ToList();
    // same placement as the inner gusset outline of the joint (section 4b): vertical shear onto the model chord, then mirrored
    List<TrussUV> Outline(string s, int sign, double shear)
    {
        return s.Trim().Length == 0 ? new() : s.Split(';').Select(p => p.Split(',')).Select(a => (u: double.Parse(a[0], inv), v: double.Parse(a[1], inv)))
            .Select(q => new TrussUV(sign * In(q.u), In(q.v + shear * q.u))).ToList();
    }
    double Thk(string s) { double v = 0; foreach (var part in s.Split(' ', StringSplitOptions.RemoveEmptyEntries)) v += part.Contains('/') ? double.Parse(part.Split('/')[0], inv) / double.Parse(part.Split('/')[1], inv) : double.Parse(part, inv); return In(v); }
    int nLayerJoints = 0;
    // (bridge, data structure, data joint, model joint, mirror sign)
    var targets = new List<(string bridge, string st, string dj, string mj, int sign)>();
    foreach (var (bn, dir, A) in new[] { ("PTC 3-span truss 11-13", 1, true), ("PTC 4-span truss 14-17", 1, false), ("PTC 4-span truss 21-24", -1, false), ("PTC 3-span truss 25-27", -1, true) })
    {
        string st = A ? "approach3 (unit A)" : "approach4 (unit B)";
        if (A)
        {
            targets.Add((bn, st, "L8", "L8", dir)); targets.Add((bn, st, "L8", "L8'", -dir));
            targets.Add((bn, st, "L0'", "L0'", dir)); targets.Add((bn, st, "L0'", "L0", -dir));     // p.59 draws the unit-A end at the high end (L0'); p.49: L0 similar, opposite hand
        }
        else
        {
            targets.Add((bn, "approach4 (unit B)", "L0", "L0", dir)); targets.Add((bn, "approach4 (unit B)", "L0", "L0'", -dir));
            targets.Add((bn, st, "L10", "L10", dir)); targets.Add((bn, st, "L10", "L10'", -dir));
            targets.Add((bn, st, "L20", "L20", dir));
        }
    }
    foreach (var j in new[] { "L10", "L11", "L22" })
    {
        targets.Add(("PTC Arch truss 18-20", "main", j, j, 1));
        if (j != "L22") targets.Add(("PTC Arch truss 18-20", "main", j, j + "'", -1));
    }
    foreach (var bn in targets.Select(x => x.bridge).Distinct())
        if (Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn)?.Truss is { } tt) foreach (var j in tt.Joints) j.GussetLayers = null;
    foreach (var (bn, st, dj, mj, sign) in targets)
    {
        var t = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn)?.Truss;
        if (t == null) { report.Add($"gusset layers: no truss bridge '{bn}' - skipped"); continue; }
        var rows = lay.Where(r => r["structure"] == st && r["joint"] == dj).ToList();
        var inner = rows.First(r => r["position"].StartsWith("inner"));
        var jd = t.Joints.LastOrDefault(x => x.Point == mj && x.Plane.Length == 0);
        if (jd == null) { jd = new TrussJointDefinition { Point = mj }; t.Joints.Add(jd); }
        string pg = "p." + inner["page"].Replace("; ", " / p.");
        string conf = inner["confidence"];
        double t0 = Thk(inner["thickness_in"]);
        if (Math.Abs(jd.GussetThickness - t0) > In(1.0 / 32))
        {
            report.Add($"gusset layers {bn} {mj}: inner gusset {jd.GussetThickness / 0.0254:0.###} -> {t0 / 0.0254:0.###} in ({pg})");
            jd.GussetThickness = t0;
            if (st == "main" && dj == "L11")
            {
                jd.GussetsInsideFaceDistance = In(29);
                jd.Unresolved = U(pg, $"gusset stack of joint {mj}", "Section E lists 1 1/8 + 3/4 + 3/4 gusset plates and prints '2'-5 o. to o. 1 1/8 Gus. Pls.' - the 1 1/8 plates may be inlaid in the chord web plane (the chord 1 1/8 webs end at the joint); the outlines are drawn, not dimensioned",
                    "1 1/8 gussets against the members at 2'-5 in. to in. (as every main joint), 3/4 plates stacked outside; outline = envelope of the member laps.");
            }
        }
        jd.GussetLayers = new();
        foreach (var r in rows.Where(r => !r["position"].StartsWith("inner")).OrderByDescending(r => int.Parse(r["layer_from_outside"])))
        {
            double tl = Thk(r["thickness_in"]);
            bool whole = r["outline_uv_in"].Trim().Length == 0;
            jd.GussetLayers.Add(new TrussGussetLayer
            {
                Thickness = tl, Outline = Outline(r["outline_uv_in"], sign, jointShear.TryGetValue((bn, mj), out var shL) ? shL : 0), Label = $"2-{r["thickness_in"]} Gussets ({r["position"]} layer)",
                Source = $"gusset-layers.csv {pg} ({conf}): {r["extent_note"]}",
                Unresolved = U(pg, $"{r["thickness_in"]} in. gusset layer at joint {mj}",
                    (whole ? "its extent is not separable from the inner gusset on the sheet" : "the plate is drawn, not dimensioned; outline scaled from a 300-450 dpi render (+-1 in.)")
                    + (conf == "low" ? "; LOW confidence reading of the stack (Section E)" : ""),
                    whole ? "same outline as the inner gusset (envelope)." : "outline as scaled, clipped to the inner gusset, stacked outside it."),
            });
        }
        nLayerJoints++;
    }
    report.Add($"gusset layers: {nLayerJoints} joint definitions with stacked gussets (gusset-layers.csv)");
    // QC I045 (2026-10-21): the approach script's flag said "only one gusset pair per joint is built"; the stacked layers are built here now -> keep only the
    // part that is still open (fills, splice plates, lug angles, diaphragms, stitch rivets).
    foreach (var bn in targets.Select(x => x.bridge).Distinct().Where(b => !b.Contains("Arch")))
    {
        var tt = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn)?.Truss;
        if (tt == null) continue;
        int n0 = tt.Unresolved.RemoveAll(s => s.Contains("second gusset pairs") && s.Contains("only one gusset pair per joint"));
        if (n0 == 0) continue;
        string pages = bn.Contains("3-span") ? "p.49-53, p.59" : "p.59-67";
        tt.Unresolved.Add(U(pages, "fills, splice plates, lug angles, diaphragms and stitch rivets of the approach truss joints", "listed per joint without extents",
            "not built. (The stacked gusset layers - 3-span L0 / L0' 5/8 + 1/2 and L8 / L8' 7/8 + 3/4, 4-span L0 / L0' 3/4 + 1/2, L10 / L10' 7/8 + 7/8, L20 7/8 + 3/4 - are built from gusset-layers.csv by connections-ptc.csx section 5.)"));
    }
}

// ================================================================ 6. approach truss joint FILLS and chord SPLICE material (QC I051, 2026-10-21; user: "Draw as shown")
// The approach members are 1954 14 WF shapes turned with the web across the truss plane: the gussets lie on the flange outer faces at the printed in-to-in,
// and the difference to each member's depth is made up by FILLS between the flange and the gusset (the sheets list them "2-Fills b x t" per member end and
// draw them to scale, 3/4 in. = 1 ft). Here:
//   (a) fills on every member lapping a gusset whose depth is less than the printed gusset in-to-in: thickness = (in-to-in - d) / 2 to 1/16 in. (the printed
//       fill sizes add up to it where read, e.g. p.62 U9: 2 x 7/8 = 1 3/4 = (17 5/8 - 14.12) / 2), width = the flange width to 1/2 in. (14 1/2 / 15 1/2 / 16 /
//       10 / 12 / 11 1/2 as printed), from the member end at the joint over the member's lap under the gusset (the drawn outline, section 4b) and past the
//       gusset edge as far as the sheet draws rivets beyond it (rivet-counts.csv 'beyond' extent + 1 1/2 in. edge distance);
//   (b) chord splice material read from the joint sheets (approach-splices.csv): web splice plates on the web (plan views), splice bars inside the flanges
//       (elevation, dashed), splice plates outside the gussets - sizes as printed, lengths scaled (dimension-only flags with page + reason).
// Generic inputs: TrussJointPlate.Member / CenterOffset (fills and bars on one member), TrussGussetLayer with ClipToGusset = false (plates outside the gussets).
{
    // structure key / data joint of each model joint (as section 4b: 3-span ends are drawn as U0' / L0' on p.59), similar-to joints
    var unitsA = new[] { ("PTC 3-span truss 11-13", "approach3", 1, 12), ("PTC 4-span truss 14-17", "approach4", 1, 20), ("PTC 4-span truss 21-24", "approach4", -1, 20), ("PTC 3-span truss 25-27", "approach3", -1, 12) };
    var cnt = Csv("rivet-counts.csv");
    string SK(string s) => s.Split(' ')[0];
    var beyondExt = cnt.Where(r => r["region"] == "beyond" && Num(r["beyond_extent_in"]) is > 0)
        .GroupBy(r => (SK(r["structure"]), r["joint"], r["member"])).ToDictionary(g => g.Key, g => g.Max(r => Num(r["beyond_extent_in"])!.Value));
    var drawnJoints = Csv("gusset-outlines.csv").Where(r => r["structure"] != "main").Select(r => (SK(r["structure"]), r["joint"].Trim())).ToHashSet();
    var tokenRx = new System.Text.RegularExpressions.Regex(@"^([UL])(\d+)('?)$");
    var spl = File.Exists(Path.Combine(dataDir, "approach-splices.csv")) ? Csv("approach-splices.csv") : new List<Dictionary<string, string>>();
    int nFill = 0, nFillJ = 0, nBeyond = 0, nSpl = 0, nOuter = 0;
    var fillChecks = new List<string>();
    foreach (var (bn, st, dir, C) in unitsA)
    {
        var t = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn)?.Truss;
        if (t == null) { report.Add($"approach fills: no truss bridge '{bn}' - skipped"); continue; }
        string pg = st == "approach3" ? "p.49-53, p.59" : "p.59-67";
        int Ord(string pt) { var m = tokenRx.Match(pt); if (!m.Success) return -1000; int n = int.Parse(m.Groups[2].Value); return m.Groups[3].Value == "'" ? 2 * C - n : n; }
        var mems = t.Members.Where(m => !m.From.Contains(':') && !m.To.Contains(':')).ToList();
        TrussSectionDefinition? Sec(TrussMemberDefinition m) => t.Sections.FirstOrDefault(s => s.Name == m.Section);
        TrussPointDefinition? Pt(string n) => t.Points.LastOrDefault(p => p.Name == n && (p.Planes == null || p.Planes.Count == 0));
        bool IsChordR(TrussMemberRole r) => r is TrussMemberRole.UpperChord or TrussMemberRole.LowerChord or TrussMemberRole.EndPost;
        foreach (var jd in t.Joints.Where(j => j.Plane.Length == 0 && j.GussetsInsideFaceDistance > 0 && j.Outline.Count >= 3 && tokenRx.IsMatch(j.Point)))
        {
            jd.Plates = null;
            string pt = jd.Point;
            var P = Pt(pt);
            if (P == null) continue;
            var at = mems.Where(m => m.From == pt || m.To == pt).ToList();
            // chord passing through the point
            var thr = mems.Where(m => IsChordR(m.Role) && m.From[0] == pt[0] && m.To[0] == pt[0] && tokenRx.IsMatch(m.From) && tokenRx.IsMatch(m.To)
                && Math.Min(Ord(m.From), Ord(m.To)) < Ord(pt) && Math.Max(Ord(m.From), Ord(m.To)) > Ord(pt)).ToList();
            // data joint on the sheets (for the drawn beyond extents): own drawing or the other half of the unit, mirrored
            string cp = pt.EndsWith("'") ? pt.TrimEnd('\'') : pt + "'";
            string dj = drawnJoints.Contains((st, pt)) ? pt : drawnJoints.Contains((st, cp)) ? cp : null;
            bool mirror = dj != null && dj != pt;
            var outline = jd.Outline.Select(q => new Vec2(q.U, q.V)).ToList();
            bool Inside(Vec2 p)
            {
                bool c = false;
                for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
                    if ((outline[i].Y > p.Y) != (outline[j].Y > p.Y) && p.X < (outline[j].X - outline[i].X) * (p.Y - outline[i].Y) / (outline[j].Y - outline[i].Y) + outline[i].X) c = !c;
                return c;
            }
            // reach of the gusset along a direction (u, v) from the work point
            double Reach(double du, double dv) { double r = 0; for (double s = 0.01; s < 6; s += 0.005) { if (Inside(new Vec2(du * s, dv * s))) r = s; else if (s > r + 0.3) break; } return r; }
            double inToIn = jd.GussetsInsideFaceDistance;
            int nHere = 0;
            foreach (var (m, both) in at.Select(m => (m, false)).Concat(thr.Select(m => (m, true))))
            {
                var sd = Sec(m);
                if (sd == null || sd.Kind != TrussSectionKind.Rolled || !sd.Rotated || sd.Depth <= 0) continue;
                // cover plates that run gusset edge to gusset edge of the member's END joints (p.57 4-span U3-U5, U9-U11, U9-L10, L10-U11): absent under an end
                // gusset, present under the gusset of a joint the member passes through (U4 / U10: "18 3/8 / 19 1/8 in to in = d + 2 t")
                var covers = sd.TopPlates.Concat(sd.BottomPlates).Where(q => q.BetweenGussets).ToList();
                double depth = sd.Depth + (both ? covers.Sum(q => q.Thickness) : 0);
                double gap = (inToIn - depth) / 2;
                double tq = Math.Round(gap / In(1.0 / 16)) * In(1.0 / 16);
                if (tq < In(1.0 / 16) - 1e-9) continue;
                // printed widths are the flange width to 1/2 in. (label); the solid is kept within the (interpolated) flange so it cannot hang past the member
                double nominal = Math.Round(sd.FlangeWidth / In(0.5)) * In(0.5), width = Math.Min(nominal, sd.FlangeWidth);
                // member direction away from the joint in the joint frame (u = toward the higher station, v up); through chords: both ways
                var dirs = new List<(int sgn, double du, double dv)>();
                foreach (var other in both ? new[] { m.From, m.To } : new[] { m.From == pt ? m.To : m.From })
                {
                    var Q = Pt(other);
                    if (Q == null) continue;
                    double du = Q.Station - P.Station, dv = Q.Elevation - P.Elevation, l = Math.Sqrt(du * du + dv * dv);
                    dirs.Add((other == m.To ? 1 : -1, du / l, dv / l));
                }
                // beyond extent of the drawn rivets on this member (own drawing / mirrored half; member class by direction)
                double ext = 0;
                if (dj != null && covers.Count == 0)   // a covered member's cover plates begin at the gusset edge: its fill ends there
                {
                    string Other(TrussMemberDefinition x) => x.From == pt ? x.To : x.From;
                    string Mp(string n) => !mirror ? n : (tokenRx.Match(n) is { Success: true } mm && int.Parse(mm.Groups[2].Value) != C ? (n.EndsWith("'") ? n.TrimEnd('\'') : n + "'") : n);
                    string dName = string.Join("-", new[] { Mp(m.From), Mp(m.To) });
                    var keys = beyondExt.Keys.Where(k => k.Item1 == st && k.Item2 == dj && (k.Item3 == dName || k.Item3 == string.Join("-", dName.Split('-').Reverse()) || k.Item3 == m.Name)).ToList();
                    foreach (var k in keys) ext = Math.Max(ext, In(beyondExt[k] + 1.5));
                }
                string why = U(pg, $"fills between the flanges of {m.Name} and the gussets at {pt}",
                    $"the joint sheet lists the fills per member end (2-Fills b x t) and draws them, but dimensions neither their length nor every thickness; thickness = (gusset in-to-in {SBM.Core.Truss.TrussSections.FormatIn(inToIn)} - member depth {SBM.Core.Truss.TrussSections.FormatIn(depth)}{(both && covers.Count > 0 ? " incl. its cover plates" : "")}) / 2 (the 1954 14 WF depths are interpolated, see the section flags), width = the flange width to 1/2 in.",
                    "one fill per flange (pair) from the member end over the lap under the drawn gusset outline" + (ext > 0 ? $" and {SBM.Core.Truss.TrussSections.FormatIn(ext)} past the gusset edge where the sheet draws rivets beyond it" : "") + ".");
                foreach (var (sgn, du, dv) in dirs)
                {
                    double r = Reach(du, dv);
                    if (r <= 0) continue;
                    double a1 = r + ext;
                    // through chord: offset along +axis (From -> To); member end: along the member away from the joint
                    double lo = both ? (sgn > 0 ? 0 : -a1) : 0, hi = both ? (sgn > 0 ? a1 : 0) : a1;
                    (jd.Plates ??= new()).Add(new TrussJointPlate
                    {
                        Member = m.Name, Face = TrussPlateFace.WebOutside, Fill = true, Width = width, Thickness = tq, Length = hi - lo, Offset = (lo + hi) / 2,
                        Label = $"2-Fills {SBM.Core.Truss.TrussSections.FormatIn(nominal)} x {SBM.Core.Truss.TrussSections.FormatIn(tq)}", Material = sd.Material, Source = $"{pg} (fill = in-to-in - depth)", Unresolved = why,
                    });
                    nFill++; nHere++;
                    if (ext > 0) nBeyond++;
                }
            }
            if (nHere > 0) nFillJ++;
        }
        // ---- (b) chord splice material read from the sheets (approach-splices.csv): data joint -> model joints (own / other half mirrored; NJ unit same sheets)
        foreach (var r in spl.Where(r => SK(r["structure"]) == st))
        {
            string dj = r["joint"].Trim();
            foreach (var mj in new[] { dj, dj.EndsWith("'") ? dj.TrimEnd('\'') : dj + "'" }.Distinct())
            {
                if (tokenRx.Match(mj) is { Success: true } mm0 && int.Parse(mm0.Groups[2].Value) == C && mj.EndsWith("'")) continue;
                bool mirror = mj != dj;
                var jd = t.Joints.LastOrDefault(x => x.Point == mj && x.Plane.Length == 0);
                if (jd == null) continue;
                // member of the data joint -> model member (the same panel points, primed on the other half)
                string Mp(string n) => !mirror ? n : (tokenRx.Match(n) is { Success: true } mm && int.Parse(mm.Groups[2].Value) != C ? (n.EndsWith("'") ? n.TrimEnd('\'') : n + "'") : n);
                var ends = r["member"].Split('-').Select(Mp).ToHashSet();
                var m = mems.FirstOrDefault(x => ends.SetEquals(new[] { x.From, x.To }));
                if (m == null) { report.Add($"approach splices {bn} {mj}: member {r["member"]} not found - skipped"); continue; }
                double w = In(Num(r["width_in"]) ?? 0), th = In(Num(r["thk_in"]) ?? 0), a0 = In(Num(r["from_in"]) ?? 0), a1 = In(Num(r["to_in"]) ?? 0);
                string face = r["face"];
                string flag = U("p." + r["page"], $"{r["label"]} at {mj}", $"size as printed; the length is scaled from the joint sheet ({r["evidence"]}; {r["confidence"]} confidence, about +-1 in.)",
                    "plate as printed, length as scaled" + (mirror ? ", mirrored from the drawn half" : "") + ".");
                if (face == "gusset-out")
                {
                    // plate outside the gussets along the chord member: a gusset layer not clipped to the gusset (its own rectangle in the joint frame)
                    // along the member away from the joint (a chord passing through: From -> To)
                    var P = Pt(mj); var Q = Pt(m.From != mj && m.To != mj ? m.To : (m.From == mj ? m.To : m.From));
                    double du = Q.Station - P.Station, dv = Q.Elevation - P.Elevation, l = Math.Sqrt(du * du + dv * dv); du /= l; dv /= l;
                    double nu = -dv, nv = du;
                    var poly = new[] { (a0, -w / 2), (a1, -w / 2), (a1, w / 2), (a0, w / 2) }.Select(q => new TrussUV { U = du * q.Item1 + nu * q.Item2, V = dv * q.Item1 + nv * q.Item2 }).ToList();
                    (jd.GussetLayers ??= new()).Add(new TrussGussetLayer { Thickness = th, Outline = poly, ClipToGusset = false, Label = r["label"] + " (outside the gussets)", Material = r["material"], Unresolved = flag, Source = $"approach-splices.csv p.{r["page"]}" });
                    nOuter++;
                    continue;
                }
                var pf = face switch { "web-top" => TrussPlateFace.Top, "web-bottom" => TrussPlateFace.Bottom, "flange-in" => TrussPlateFace.WebInside, _ => TrussPlateFace.WebOutside };
                (jd.Plates ??= new()).Add(new TrussJointPlate
                {
                    Member = m.Name, Face = pf, Fill = r["kind"] == "fill", Width = w, Thickness = th, Length = a1 - a0, Offset = (a0 + a1) / 2, CenterOffset = In(Num(r["y_in"]) ?? 0),
                    Label = r["label"], Material = r["material"], Source = $"approach-splices.csv p.{r["page"]} ({r["confidence"]})", Unresolved = flag,
                });
                nSpl++;
            }
        }
        int n0 = t.Unresolved.RemoveAll(s => s.Contains("fills, splice plates, lug angles, diaphragms and stitch rivets of the approach truss joints"));
        t.Unresolved.Add(U(pg, "lug angles, diaphragms and stitch rivets of the approach truss joints, and the fills drawn outside the joints (vertical stub ends, floor beam connection fills)",
            "they are listed on the joint sheets but not modeled (the truss generator has no input for them yet)",
            "not built. Built from the sheets: gusset layers (section 5), fills between the member flanges and the gussets (section 6a), chord splice plates / bars (section 6b)."));
    }
    report.Add($"approach fills: {nFill} fill definitions (pairs) on {nFillJ} model joints, {nBeyond} past the gusset edge to the drawn beyond rivets; splice material: {nSpl} plates / bars on members, {nOuter} plates outside the gussets (approach-splices.csv)");
}

// ================================================================ regenerate + save
var rep = Doc.Regenerate();
foreach (var name in new[] { "PTC Arch truss 18-20", "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var tm = Doc.Model(name).Steel?.Truss;
    if (tm == null) { Print($"{name}: no truss model"); continue; }
    var st = tm.Steel;
    double lb(PartCategory c) => st.Parts.Where(p => p.Category == c).Sum(p => p.Mass) * 2.20462262;
    Print($"{name}: {tm.Gussets} gussets ({lb(PartCategory.TrussGusset):N0} lb), {tm.SplicePlates} splice / fill plates ({lb(PartCategory.SplicePlate) + lb(PartCategory.FillerPlate):N0} lb), stiffener parts {st.Parts.Count(p => p.Category == PartCategory.IntermediateStiffener)} ({lb(PartCategory.IntermediateStiffener):N0} lb); steel {st.SteelMass * 2.20462262:N0} lb");
    foreach (var e in rep.Errors.Where(e => e.StartsWith(name))) Print("  ERROR " + e);
    foreach (var w in rep.Warnings.Where(w => w.StartsWith(name) && (w.Contains("not built") || w.Contains("no chord"))).Take(8)) Print("  W: " + w);
}
foreach (var s in report) Print("  " + s);
Doc.Save(outPath);
Print($"Saved {outPath}");
