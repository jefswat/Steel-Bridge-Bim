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
                    // BATCH 4e (R04): 'All Cover Plate Splices: 1 1/8 in. dia high strength rivets' (p.70) - the counted rivets of the cover splice plates (splices.csv
                    // rivet_count = the symbols drawn in the flange plan view, rivet_rows gage lines); fills get none of their own (the plate's rivets go through them)
                    int rc = !fill && (item.Contains("cover") || item.Contains("flange")) && Num(r["rivet_count"]) is > 0 and var rcv ? (int)rcv : 0;
                    jd.Plates.Add(new TrussJointPlate
                    {
                        RivetCount = rc, RivetRows = rc > 0 ? (int)(Num(r["rivet_rows"]) ?? 5) : 0, RivetDiameter = rc > 0 ? In(Num(r["rivet_dia_in"]) ?? 1.125) : 0, RivetMaterial = rc > 0 ? "High strength rivet steel" : null,
                        RivetUnresolved = rc > 0 ? U("p." + r["page"], $"rivets of the {r["item"]} at {pt}", $"the flange plan view draws {rc} rivets (image-counted: {r["rivet_note"]}) on {r["rivet_rows"]} gage lines without gages or pitch (p.70: 4 in. max. pitch)",
                            $"{rc} rivets per plate, {r["rivet_rows"]} lines equally spaced across the plate, equally spaced along it, 1.5 d edge distance; grip = the steel under the plate.") : null,
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
    // BATCH 3b (2026-10-28, GL07): the plates between the chord web and the gusset from U8 / L9 on. The sheets give the gussets an in-to-in wider than the 2'-5 o. to o. chord
    // webs and list outside web splice / pin plates exactly that thick ((in-to-in - 29) / 2); their extent lines carry no figures -> built UNDER THE GUSSET (TrussJointPlate.UnderGusset:
    // length = the gusset outline over the 34 in. web band), flagged. Inside splice plates, inlay plates and fills are inside the web stack (no gusset float) - not built.
    {
        int nUg = 0;
        // (joint, page, plates outside the web: thickness list in. inner -> outer, callout, source note)
        var ug = new (string j, int page, double[] t, string label, string note)[]
        {
            ("U8", 77, new[] { 0.75 }, "2-Spl. Pls. 34 x 3/4 - o.s.", "gussets 2'-6 1/2 in. to in. (p.77) over the 2'-5 chord"),
            ("L9", 77, new[] { 0.75 }, "2-Spl. Pls. 34 x 3/4 - o.s.", "gussets 2'-6 1/2 in. to in. (p.77)"),
            ("U10", 78, new[] { 1.125 }, "2-Splice Plates 34 x 1 1/8 - o.s.", "gussets 2'-7 1/4 in. to in. (p.78)"),
            ("U21", 89, new[] { 1.125 }, "2-O.S. Splice Pls 34 x 1 1/8", "gussets 2'-7 1/4 in. to in. (p.89)"),
            ("L21", 89, new[] { 1.125 }, "2-O.S. Splice Pls 34 x 1 1/8", "gussets 2'-7 1/4 in. to in. (p.89; '2-O.S. Spl. Pls 34 x 1 1/8' legible at 300 dpi, batch 8 re-read)"),
            ("U22", 90, new[] { 1.125, 1.0, 1.0 }, "pin plates 2-Pls 34 x 1 1/8, 34 x 1, 34 x 1", "Section H 2'-11 1/4 o. o. pin plates = 2'-5 + 2 x (1 1/8 + 1 + 1) (p.90); the 31 1/2 x 7/8 inside pin plates are not built"),
        };
        foreach (var (j, page, ts, label, note) in ug)
        {
            int n = int.Parse(j.Substring(1));
            foreach (bool pr in new[] { false, true })
            {
                if (pr && n == 22) continue;
                string pt = j[0] + (pr ? n + "'" : n.ToString());
                var jd = t.Joints.LastOrDefault(x => x.Point == pt);
                if (jd == null) { jd = new TrussJointDefinition { Point = pt }; t.Joints.Add(jd); }
                jd.Plates ??= new List<TrussJointPlate>();
                int k = 0;
                foreach (var th in ts)
                {
                    k++;
                    jd.Plates.Add(new TrussJointPlate
                    {
                        Face = TrussPlateFace.WebOutside, Width = In(34), Thickness = In(th), UnderGusset = true, Label = ts.Length > 1 ? $"{label} (plate {k})" : label, Material = "S",
                        Source = $"p.{page} (batch 3b)",
                        Unresolved = U($"p.{page}", $"{label} at {pt}", $"the plates fill the space between the 2'-5 chord webs and the gussets ({note}); their extent lines along the chord are drawn without figures",
                            "plate under the gusset only (length = the gusset over the 34 in. web band), stacked on the chord web."),
                    });
                    nUg++;
                }
            }
        }
        report.Add($"main truss: {nUg} outside web splice / pin plate definitions under the gussets (U8, L9, U10, U21, L21, U22; both halves)");
    }
    // BATCH 8b (GN01 / N05, 2026-10-30): fills between the web members and the gussets, read from the joint sheets (qc/ptc/main-span/work8/batch8-data.md section 1).
    // Callouts give width x thickness; the extents are SCALED (3/4 in. = 1 ft-0, 8.56 px/in at 300 dpi = the gusset-outlines basis, +-1 in.): a0 = member end,
    // a1 = fill end, both from the WP along the member; beyond = drawn length past the gusset edge (diagonals: added to the gusset outline reach along the member).
    {
        int nF = 0;
        var missing = new List<string>();
        var fills = new (string j, string mem, int page, double w, double[] t, double a0, double a1, double beyond, string label)[]
        {
            ("U6",  "U6-L6",   76, 18, new[] { 1.0 },   22.2, 51.2, 6.8,  "2-Fills 18 x 1 (C)"),
            ("L7",  "U7-L7",   76, 18, new[] { 1.0 },   22.4, 52.8, 5.8,  "2-Fills 18 x 1 (C)"),
            ("U8",  "U8-L8",   77, 18, new[] { 0.75 },  22.8, 55.7, 4.1,  "2-Fills 18 x 3/4 (C)"),
            ("L9",  "U9-L9",   77, 21, new[] { 0.75 },  22.3, 63.2, 4.7,  "2-Fills 21 x 3/4 (C)"),
            ("U10", "U10-L10", 78, 26, new[] { 1.125 }, 25.2, 63.8, 14.0, "2-Fills 26 x 1 1/8 (C)"),
            ("U21", "U21-L21", 89, 18, new[] { 1.125 }, 18.1, 62.5, 7.9,  "2-18 x 1 1/8 Fills (C)"),
            ("L21", "U21-L21", 89, 18, new[] { 1.125 }, 20.4, 64.8, 9.6,  "2-18 x 1 1/8 Fills (C)"),
            ("U21", "U21-L22", 89, 18, new[] { 1.125 }, double.NaN, double.NaN, 4.8,  "2-18 x 1 1/8 Fills (C)"),
            ("L21", "U20-L21", 89, 21, new[] { 1.125 }, double.NaN, double.NaN, 18.6, "2-21 x 1 1/8 Fills"),
            // U22 (GN02): the vertical's stepped fills (p.90 "4-Fills 18 x 1 1/8 + 2-Fills 18 x 7/8" = 3 1/8 per face), the member-side one first
            ("U22", "U22-L22", 90, 18, new[] { 0.875, 1.125, 1.125 }, 21.1, double.NaN, 0, "4-Fills 18 x 1 1/8 (C), 2-Fills 18 x 7/8 (C)"),
        };
        double[] u22Ends = { 50.1, 46.7, 43.1 };
        string Prime(string m) => System.Text.RegularExpressions.Regex.Replace(m, @"([A-Z]+)(\d+)", x => int.Parse(x.Groups[2].Value) == 22 ? x.Value : x.Value + "'");
        string Bare(string n) => n.Contains(':') ? n[(n.IndexOf(':') + 1)..] : n;
        // distance from the WP to where the member axis leaves the joint's gusset outline (u along the station, v up, as the outline); 0 = no outline
        double ReachAlong(TrussJointDefinition jd, string mem, string pt)
        {
            if (jd.Outline is not { Count: >= 3 }) return 0;
            var md = t.Members.FirstOrDefault(m => m.Name == mem);
            if (md == null) return 0;
            string other = Bare(md.From) == pt ? Bare(md.To) : Bare(md.From);
            var P = t.Points.FirstOrDefault(q => q.Name == pt); var Q = t.Points.FirstOrDefault(q => q.Name == other);
            if (P == null || Q == null) return 0;
            double du = Q.Station - P.Station, dv = Q.Elevation - P.Elevation, l = Math.Sqrt(du * du + dv * dv);
            var ol = jd.Outline.Select(q => (q.U, q.V)).ToList();
            bool Inside(double x, double y)
            {
                bool c = false;
                for (int i = 0, k = ol.Count - 1; i < ol.Count; k = i++)
                    if ((ol[i].V > y) != (ol[k].V > y) && x < (ol[k].U - ol[i].U) * (y - ol[i].V) / (ol[k].V - ol[i].V) + ol[i].U) c = !c;
                return c;
            }
            double r = 0;
            for (double sx = 0.01; sx < 6; sx += 0.005) { if (Inside(du / l * sx, dv / l * sx)) r = sx; else if (sx > r + 0.3) break; }
            return r;
        }
        foreach (var (j, mem, page, w, ts, a0, a1, beyond, label) in fills)
        {
            int n = int.Parse(j.Substring(1));
            foreach (bool pr in new[] { false, true })
            {
                if (pr && n == 22) continue;
                string pt = pr ? j + "'" : j, mn = pr ? Prime(mem) : mem;
                var jd = t.Joints.LastOrDefault(x => x.Point == pt && x.Outline is { Count: >= 3 }) ?? t.Joints.LastOrDefault(x => x.Point == pt);
                if (jd == null) { jd = new TrussJointDefinition { Point = pt }; t.Joints.Add(jd); }
                if (!t.Members.Any(m => m.Name == mn)) { missing.Add($"{mn} at {pt}"); continue; }
                jd.Plates ??= new List<TrussJointPlate>();
                double lo = double.IsNaN(a0) ? 0 : In(a0);
                for (int k = 0; k < ts.Length; k++)
                {
                    double hi = mem == "U22-L22" ? In(u22Ends[k]) : !double.IsNaN(a1) ? In(a1) : ReachAlong(jd, mn, pt) + In(beyond);
                    if (hi <= lo + In(1)) { missing.Add($"{mn} at {pt} (no gusset outline to measure the reach)"); continue; }
                    jd.Plates.Add(new TrussJointPlate
                    {
                        Member = mn, Face = TrussPlateFace.WebOutside, Fill = true, Width = In(w), Thickness = In(ts[k]), Length = hi - lo, Offset = (lo + hi) / 2,
                        Label = ts.Length > 1 ? $"{label} (fill {k + 1})" : label, Material = "C", Source = $"p.{page} (batch 8b)",
                        Unresolved = U($"p.{page}", $"{label} on {mn} at {pt}", "the callout gives width x thickness only; the fill extent (member end to a few inches past the gusset edge, where it carries field rivets) is drawn, not dimensioned",
                            $"fill from the member end to {hi / In(1):0.0} in. from the WP along the member (scaled 3/4 in. = 1 ft-0, +-1 in.)" + (ts.Length > 1 ? "; stepped fills ending one rivet row apart, the longest on the member (order inferred from the leaders)" : "") + "."),
                    });
                    nF++;
                }
            }
        }
        report.Add($"main truss: {nF} web member fill definitions (batch 8b: U6, L7, U8, L9, U10, U21, L21, U22; both halves)" + (missing.Count > 0 ? "; not built: " + string.Join("; ", missing) : ""));
    }
    // the arch script's general flag on splices is now partly built: replace it
    t.Unresolved.RemoveAll(s => s.Contains("chord splice plates, inlay plates, fills"));
    t.Unresolved.Add(U("p.69, p.74-90", "inlay plates, lug angles, stiffeners, diaphragms, stitch rivets and the splice plates whose extent was not read",
        $"they are listed per joint without extents ({nSkip} splice / fill callouts have no length in splices.csv)", "chord cover / web splice plates and fills with a scaled length are built (connections-ptc.csx); the rest is not built."));
}

// ================================================================ 2. girder spans: stiffeners (fills, crimps, rivets), the two web splices and the longitudinal stiffener (p.43 / p.44)
foreach (var name in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var br = Doc.Project.Bridges.First(b => b.Name == name);
    var t = br.Truss!;
    var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
    var tm = Doc.Model(name).Steel?.Truss;
    if (tm == null) { report.Add($"{name}: no truss model"); continue; }
    double hStiff = FI(9, 6.5) - 2 * In(1.125);    // between the outstanding legs of the flange angles (9'-6 1/2 b.b. Ls - 2 x 1 1/8)
    double hSplice = In(114.5 - 2 * 8 - 0.5);        // between the vertical legs of the flange angles (UNRESOLVED: not printed)
    double hFill = In(114.5 - 2 * 8 - 0.25);          // fills / crimp zone between the flange angle vertical leg toes (1/8 in. clear each end)
    TrussRivetColumn RC(double gIn, double hIn0, double pitchIn, double firstIn) => new TrussRivetColumn(In(gIn), (int)Math.Floor((hIn0 - 2 * firstIn) / pitchIn + 1e-9) + 1, In(pitchIn), In(firstIn));
    // stiffener ends clear of the flange angle root fillets (3/8 in.; plumb stiffeners 1/2 in.: they lean 3 % against the girder on the grade), plumb fills 1/4 in. shorter
    double hInt = hStiff - In(0.75), hPl = hStiff - In(1), hFillP = hFill - In(0.5);
    double hIn = hInt / In(1), hInP = hPl / In(1);
    string stiffFlag = U("p.43, p.44", "stiffener layout", "the elevation gives the stiffeners as equal spaces per floor beam panel (9 / 6 / 6 / 9) and the sizes (end 4-Ls 7x4x3/4 + 2-Fills 15x1 1/8, intermediate 2-Ls 6x4x7/16 crimped, 2-Ls 6x4x1/2 + 2-1 1/8 fills at the floor beams) without station dimensions",
        "end stiffeners 2 angles per web face at the CL bearing (7 in. legs on the web on 15 x 1 1/8 fills, 4 in. legs outstanding: the 15 in. fill and the two rivet lines per angle drawn on p.44), intermediate pairs at the equal spaces of each panel between the CL bearing / floor beam lines (crimped over the 1 1/8 flange angle legs), 6x4x1/2 pairs on 1 1/8 fills at the floor beams; height between the flange angle outstanding legs (9'-4 1/4), fills / crimp zone between the flange angle leg toes; "
        + "UNRESOLVED (p.44): p.44 also gives '2-Fills 4 x 3/8' at the first intermediate stiffener at each girder end; their purpose is not shown - not built (agent review girders G16).");
    string stiffRivFlag = U("p.44", "stiffener rivets", "the elevation draws the stiffener rivets (1 in. carbon, girder note p.44) without pitch or gage",
        "end stiffeners 2 staggered lines per angle at 2 1/2 / 5 in. from the heel, 6 in. pitch per line; floor beam and intermediate stiffeners 1 line at 2 1/4 in., 6 in. pitch (the pitch drawn on p.44, scaled: agent review girders G10); first rivet 2 in. from the stiffener ends.");
    string splFlag = U("p.43, p.44", "web splices", "p.43 marks two web splices ('CL Splice') in the 6-equal-space panels, 4 spaces from the middle floor beam (scaled 19.6 ft from it vs 20'-3 1/4 for 4 spaces); p.44 prints 2-Splice Pls 21 x 3/8 centred on the splice with the crimped stiffener beside it on 2-Fills 4 x 3/8 and 2-Splice Pls 6 1/4 x 3/8 on the flange angles; the plate height and the rivet pattern are drawn, not dimensioned",
        "21 x 3/8 plate each web face, 8'-2 high (114 1/2 - 2 x 8 - 1/2), centred on the 4th intermediate stiffener position from the middle floor beam in both middle panels (that stiffener beside the plate on the middle floor beam side, on 3/8 fills); 3 rivet lines each side of the joint at 1 3/4 / 5 1/4 / 8 3/4 in. (columns about 3 1/2 apart as drawn, scaled: agent review girders G11), 3 1/2 in. pitch; the 6 1/4 x 3/8 flange angle splice plates not built.");
    string longFlag = U("p.44", "longitudinal stiffener L 6 x 4 x 7/16 (C) on the inside of the girder", "the elevation draws it between the transverse stiffeners in every panel (end, floor beam and splice panels) with one rivet line, without its height",
        "6 in. leg on the web hanging from its heel 1'-9 1/2 below the back of the top flange angles (scaled 1'-9 1/2 to 2'-3 1/2 on p.44: agent review girders G17), 4 in. leg outstanding toward the CL bridge, from end stiffener to end stiffener, interrupted 1/2 in. clear of every transverse stiffener / fill / splice plate, 1 in. rivets at 7 in. (the drawn pitch) 2 1/4 in. below the heel.");
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
        // web splices: 4 spaces from the middle floor beam in both middle panels (p.43 'CL Splice')
        double sp1 = bA + 2 * pitch - 4 * pitch / 6, sp2 = bA + 2 * pitch + 4 * pitch / 6;
        inter.RemoveAll(x => Math.Abs(x - sp1) < 0.01 || Math.Abs(x - sp2) < 0.01);
        double halfPl = In(10.5) + In(1.0 / 16);
        m.Stiffeners = new List<TrussStiffenerSet>
        {
            new() { Label = "end stiffeners 4-Ls 7x4x3/4", Angle = "L4x7x3/4", PerFace = 2, Height = hPl, Distances = new() { D(bA), D(bB) }, Material = "S", Source = "p.44", Unresolved = stiffFlag,
                FillThickness = In(1.125), FillWidth = (In(15) - In(7.0 / 16)) / 2, FillHeight = hFillP,
                Rivets = new() { RC(2.5, hInP, 6, 2), RC(5.0, hInP, 6, 5) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "stiffeners 2-Ls 6x4x1/2 at the floor beams", Angle = "L6x4x1/2", PerFace = 1, Height = hPl, Distances = fbs.Select(D).ToList(), Material = "C", Source = "p.44", Unresolved = stiffFlag,
                FillThickness = In(1.125), FillHeight = hFillP, Rivets = new() { RC(2.25, hInP, 6, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "intermediate stiffeners 2-Ls 6x4x7/16", Angle = "L6x4x7/16", PerFace = 1, Height = hInt, Distances = inter.Select(D).ToList(), Material = "C", Source = "p.44", Unresolved = stiffFlag,
                CrimpThickness = In(1.125), CrimpHeight = hFill, Rivets = new() { RC(2.25, hIn, 6, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "web splice 2-Pls 21 x 3/8", PlateWidth = In(21), PlateThickness = In(0.375), Height = hSplice, Distances = new() { D(sp1), D(sp2) }, Material = "S", Source = "p.43 / p.44", Unresolved = splFlag,
                Rivets = new[] { -8.75, -5.25, -1.75, 1.75, 5.25, 8.75 }.Select(g => RC(g, hSplice / In(1), 3.5, 2)).ToList(), RivetDiameter = In(1), RivetUnresolved = splFlag },
            new() { Label = "intermediate stiffeners 2-Ls 6x4x7/16 beside the web splice (2-Fills 4 x 3/8)", Angle = "L6x4x7/16", PerFace = 1, LegToward = 1, Height = hInt, Distances = new() { D(sp1) + halfPl }, Material = "C", Source = "p.44", Unresolved = splFlag,
                FillThickness = In(0.375), FillHeight = hFill, CrimpThickness = In(1.125), CrimpHeight = hFill, Rivets = new() { RC(2.25, hIn, 6, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "intermediate stiffeners 2-Ls 6x4x7/16 beside the web splice (2-Fills 4 x 3/8), second splice", Angle = "L6x4x7/16", PerFace = 1, LegToward = -1, Height = hInt, Distances = new() { D(sp2) - halfPl }, Material = "C", Source = "p.44", Unresolved = splFlag,
                FillThickness = In(0.375), FillHeight = hFill, CrimpThickness = In(1.125), CrimpHeight = hFill, Rivets = new() { RC(2.25, hIn, 6, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
        };
        m.LongitudinalStiffeners = new()
        {
            new TrussLongitudinalStiffener { Label = "long. stiffener L 6x4x7/16 (C) on the inside of the girder", Angle = "L4x6x7/16", Side = 0, Y = In(57.25) - In(21.5), LegDown = true,
                From = D(bA), To = D(bB), Clearance = In(0.5), RivetPitch = In(7), RivetGage = In(2.25), RivetEdge = In(1.5), RivetDiameter = In(1), RivetMaterial = "1 in carbon steel rivet",
                Material = "C", Source = "p.44", Unresolved = longFlag },
        };
        nG++;
    }
    var g1 = t.Section("G1");
    if (g1 != null) g1.Unresolved = U("p.43, p.44", "flange angle splice plates at the web splices",
        "the 2-Splice Pls 6 1/4 x 3/8 on the flange angles at the web splices are drawn without their length",
        "built: web, 4 flange angles, the cover plates (the F.L. plate splices 37'-0 each side of the CL lie under the 86'-0 plate, whose last 6'-0 each end is the 'additional material for splice', p.44: no extra plate), stiffeners with fills / crimps, the web splices and the longitudinal stiffener (connections-ptc.csx); not built: the flange angle splice plates.");
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
    // BATCH 4d inputs
    var edgeZones = Csv("rivet-count-zones.csv").Where(z => z["structure"] == "main" && z["member"] == "SKIP" && z["note"].Contains("angle strip") && z["note"].Contains("edge")).ToList();
    var connRows = Csv("rivet-counts-connections.csv").Where(c => c["structure"] == "main").ToList();
    var edgeSkipped = new List<string>(); int nEdge = 0;
    List<(double x, double y)> PxPts(string str) => str.Split(';').Where(q => q.Trim().Length > 0).Select(q => q.Split(',')).Select(a => (double.Parse(a[0], inv), double.Parse(a[1], inv))).ToList();
    // segment a-b clipped to a convex polygon (Cyrus-Beck); null = outside
    ((double x, double y) a, (double x, double y) b)? ClipToConvex((double x, double y) a, (double x, double y) b, List<(double x, double y)> poly)
    {
        double area2 = 0; for (int i = 0; i < poly.Count; i++) { var p0 = poly[i]; var p1 = poly[(i + 1) % poly.Count]; area2 += p0.x * p1.y - p1.x * p0.y; }
        double sgn = area2 >= 0 ? 1 : -1, t0 = 0, t1 = 1, dx = b.x - a.x, dy = b.y - a.y;
        for (int i = 0; i < poly.Count; i++)
        {
            var p0 = poly[i]; var p1 = poly[(i + 1) % poly.Count];
            double nx = -(p1.y - p0.y) * sgn, ny = (p1.x - p0.x) * sgn;      // inward normal
            double num = nx * (a.x - p0.x) + ny * (a.y - p0.y), den = nx * dx + ny * dy;
            if (Math.Abs(den) < 1e-12) { if (num < 0) return null; continue; }
            double tt = -num / den;
            if (den > 0) t0 = Math.Max(t0, tt); else t1 = Math.Min(t1, tt);
            if (t0 > t1) return null;
        }
        return ((a.x + dx * t0, a.y + dy * t0), (a.x + dx * t1, a.y + dy * t1));
    }
    foreach (var (bn, key, dir) in units)
    {
        var t = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn)?.Truss;
        if (t == null) { report.Add($"gusset outlines: no truss bridge '{bn}' - skipped"); continue; }
        foreach (var j in t.Joints) { j.Outline = new(); j.EdgeAngles = null; }
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
            // BATCH 4d (2026-10-28, G02): gusset free-edge angles 2-Ls 4 x 3 1/2 x 7/16 (U11: 5 x 3 1/2 x 7/16) - the strips the rivet counting marked on the drawn
            // gusset edges (rivet-count-zones.csv SKIP '... angle strip ... gusset edge'): the outline edge running through the strip, clipped to it, = the angle
            if (key == "main")
            {
                var zs = edgeZones.Where(z => z["joint"].Trim() == r["joint"].Trim()).ToList();
                if (zs.Count > 0)
                {
                    var opx = PxPts(r["outline_px"]); var wp = PxPts(r["wp_px"])[0]; double ppi = double.Parse(r["px_per_in"], inv);
                    var eas = new List<TrussGussetEdgeAngle>();
                    foreach (var z in zs)
                    {
                        var zp = PxPts(z["zone_px"]);
                        (double x, double y) best0 = default, best1 = default; double bl = 0;
                        for (int i = 0; i < opx.Count; i++)
                        {
                            var a = opx[i]; var b = opx[(i + 1) % opx.Count];
                            if (ClipToConvex(a, b, zp) is { } cl)
                            {
                                double l = Math.Sqrt((cl.b.x - cl.a.x) * (cl.b.x - cl.a.x) + (cl.b.y - cl.a.y) * (cl.b.y - cl.a.y));
                                if (l > bl) { bl = l; best0 = cl.a; best1 = cl.b; }
                            }
                        }
                        string note = z["note"];
                        if (bl < 4 * ppi) { edgeSkipped.Add($"{bn.Replace("PTC ", "")} {mj}: no drawn gusset edge through the strip '{note}' (outline {r["joint"]})"); continue; }
                        TrussUV ToUv((double x, double y) q) { double uu = (q.x - wp.x) / ppi, vv = -(q.y - wp.y) / ppi; return new TrussUV(sign * In(uu), In(vv + shear * uu)); }
                        var q0 = ToUv(best0); var q1 = ToUv(best1);
                        bool big = note.Contains("5 x 3 1/2");
                        // rivets: the counted 'angle member' groups of the joint (rivet-counts-connections.csv) when the side (left / right) names one strip only
                        string sideWord = note.Contains("left") ? "left" : note.Contains("right") ? "right" : "";
                        int nRiv = 0; double dia = 0;
                        if (sideWord.Length > 0 && zs.Count(o => o["note"].Contains(sideWord)) == 1)
                        {
                            var cr = connRows.FirstOrDefault(c => c["location"].Trim() == r["joint"].Trim() && c["kind"] == "angle member" && c["connection"].Contains(sideWord));
                            if (cr != null) { nRiv = int.Parse(cr["total"]); dia = double.Parse(cr["rivet_dia_in"], inv); }
                        }
                        eas.Add(new TrussGussetEdgeAngle
                        {
                            Angle = big ? "L5x3-1/2x7/16" : "L4x3-1/2x7/16", U0 = q0.U, V0 = q0.V, U1 = q1.U, V1 = q1.V, Rivets = nRiv, RivetDiameter = In(dia > 0 ? dia : 1.125), Material = "C",
                            Label = big ? "L 5 x 3 1/2 x 7/16 (C)" : "L 4 x 3 1/2 x 7/16 (C)", Source = $"p.{r["page"]} {note}",
                            Unresolved = U("p." + r["page"], $"gusset edge angle at {mj}", "the joint sheet draws the angle along the free gusset edge with its callout (" + (big ? "2-Ls 5 x 3 1/2 x 7/16" : "Ls 4 x 3 1/2 x 7/16") + ") but no length, leg orientation or rivet pitch"
                                + (note.Contains(" L 4") || note.StartsWith("stiffener") ? "; the callout reads 'L' (one angle), built on each gusset face" : ""),
                                "angle on the outer face of the gusset stack along the drawn edge (strip scaled, +-1 in.), 4 in. leg on the gusset, heel at the edge; "
                                + (nRiv > 0 ? $"{nRiv} rivets (as counted) equally spaced on the leg's centre line." : "rivets not placed (not counted for this strip).")),
                        });
                    }
                    foreach (var jd in t.Joints.Where(x => x.Point == mj)) jd.EdgeAngles = eas.Count > 0 ? eas : null;
                    nEdge += eas.Count;
                }
            }
        }
    }
    report.Add($"gusset outlines: {nOwn} model joints from their own drawing, {nSim} from a 'similar to' joint, {nNone} without an outline (envelope kept): {string.Join(", ", none.Take(30))}{(none.Count > 30 ? ", ..." : "")}");
    report.Add($"gusset edge angles (batch 4d): {nEdge} edge angle definitions on {edgeZones.Count} drawn strips (both halves / similar joints){(edgeSkipped.Count > 0 ? "; not built: " + string.Join("; ", edgeSkipped.Take(10)) : "")}");
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
        }
        if (st == "main" && dj == "L11")
        {
            // BATCH 3a (2026-10-28, GL01 / GL02): Half Section E '2'-5 o. to o. 1 1/8 Gus. Pls.' over the post webs '2'-2 3/4 o. to o.' -> 2'-2 3/4 in. to in.; the 1 1/8
            // gusset is inlaid in the chord outer 1 1/8 web layer ('End of 1 1/8 webs' on both chords, TrussPly.InlaidAt below); 1 1/8 + 3 x 3/4 per face.
            var w0 = Num(inner["in_to_in_in"]);
            if (w0 is > 0) jd.GussetsInsideFaceDistance = In(w0.Value);
            jd.Unresolved = U(pg, $"gusset stack of joint {mj}", "Half Section E prints '2'-5 o. to o. 1 1/8 Gus. Pls.' and '2'-2 3/4 o. to o. Web Pls.' (post) and labels four plates (1 1/8 + 3 x 3/4); the plate outlines and the ends of the 3/4 plates along the chords are drawn, not dimensioned, and not separable from the stacked layers",
                "1 1/8 gussets on the post webs (2'-2 3/4 in. to in.), inlaid in the chord outer 1 1/8 web layer; outline = envelope of the member laps; 3/4 plates stacked outside (second and outer 3/4 ending at the Section E steps, scaled).");
            report.Add($"gusset layers {bn} {mj}: 1 1/8 in. to in. {jd.GussetsInsideFaceDistance / 0.0254:0.###} in. (p.80 Section E), {rows.Count - 1} layers of 3/4 outside");
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
    // BATCH 4c (2026-10-28, G01 / R06): web member ends. (1) the verticals U12-L12 / U13-L13 / U14-L14 end on a plane parallel to the steep lower chord (p.81 / p.83 /
    // p.84 draw their rivet fields down to about 19 in. above the work point; a square end stopped where its far corner cleared the chord, 30-33 in.);
    // (2) web members meeting at a point stop clear of each other (the sheets draw the member ends with a gap, not dimensioned -> 0 in.: just clear).
    {
        var tm4 = Doc.Project.Bridges.First(b => b.Name == "PTC Arch truss 18-20").Truss!;
        int nPar = 0;
        // + the rigid hanger L14-W14 under the sloped lower chord (p.84 draws its rivet field from about 14 in. below the work point, the square end stopped at 31 in.)
        foreach (var m in tm4.Members.Where(m => m.Role == TrussMemberRole.Vertical && System.Text.RegularExpressions.Regex.IsMatch(m.Name, @"^U1[234]'?-L1[234]'?$")
                 || m.Role == TrussMemberRole.Hanger && System.Text.RegularExpressions.Regex.IsMatch(m.Name, @"^(L14'?-W14'?|W14'?-L14'?)$")))
        {
            if (m.To.StartsWith("L")) m.EndCut = TrussEndCut.ParallelToChord; else if (m.From.StartsWith("L")) m.StartCut = TrussEndCut.ParallelToChord;
            nPar++;
        }
        // batch 8 (recheck N08, 2026-10-30): the wind chord L13-W14 ran 0.53 in. into the lower chord at L13 (chord closure cut); p.83 draws it clear of the chord:
        // its L13 end on a plane parallel to the lower chord faces (WebMemberClearance off them), like the verticals
        foreach (var m in tm4.Members.Where(m => m.Role == TrussMemberRole.WindChord && System.Text.RegularExpressions.Regex.IsMatch(m.Name, @"^(L13'?-W14'?|W14'?-L13'?)$")))
        {
            if (m.From.StartsWith("L")) m.StartCut = TrussEndCut.ParallelToChord; else if (m.To.StartsWith("L")) m.EndCut = TrussEndCut.ParallelToChord;
            nPar++;
        }
        tm4.JointDefaults.WebToWebClearance = 0;
        tm4.Unresolved.RemoveAll(x => x.Contains("web member ends at the panel points"));
        tm4.Unresolved.Add(U("p.73-90", "web member ends at the panel points", "the joint sheets draw the diagonal / vertical ends with a gap between them and the verticals U12-L12 / U13-L13 / U14-L14 cut along the sloped lower chord, without dimensions",
            "web members set back just clear of each other (0 in.); the three verticals end on a plane parallel to the lower chord face (WebMemberClearance off it)."));
        report.Add($"web member ends (batch 4c): {nPar} verticals / rigid hangers end parallel to the lower chord; web-to-web clearance 0 in. (just clear)");
    }
    // BATCH 3a (GL02): the chords L10-L11 / L11-L12 '2'-5 o. to o. 1 1/8 web plates' (Views G / K); the elevation prints 'End of 1 1/8 webs' on both chords at L11 -> the outer
    // 1 1/8 web ply stops at the L11 gusset, which is inlaid in its layer (TrussPly.InlaidAt). Its far ends (L10 / L12) keep running: there the plans replace it by
    // '2-Inlay Plates 34 x 1 1/8' in the same layer (p.78 / p.81) - geometrically the same plate layer, the ply end / inlay joint is not modeled (flag below).
    {
        var tm = Doc.Project.Bridges.First(b => b.Name == "PTC Arch truss 18-20").Truss!;
        foreach (var sn in new[] { "CH L10-L11", "CH L11-L12" })
        {
            var sd = tm.Section(sn);
            var ply = sd?.WebPlies.FirstOrDefault();
            if (ply == null || Math.Abs(ply.Thickness - In(1.125)) > 1e-6) { report.Add($"inlaid ply: section {sn} has no outer 34 x 1 1/8 ply - skipped"); continue; }
            ply.InlaidAt = new() { "L11", "L11'" };
            report.Add($"inlaid ply: {sn} outer {ply.Label} stops at the L11 / L11' gusset (p.80 'End of 1 1/8 webs')");
        }
        tm.Unresolved.RemoveAll(x => x.Contains("chord web ply ends and the L11 splice"));
        tm.Unresolved.Add(U("p.78, p.80, p.81", "chord web ply ends and the L11 splice", "the elevation prints 'End of 1 1/8 webs', 'End of 2-o.s. 3/4 webs', 'End of two i.s. 3/4 webs', '2-Spl. Pls. 34 x 7/8 (S) outside', '2-Spl. Pls. 31 1/2 x 1/2 (S) inside' and '1-3/4 Fill (C)' at L11 with extent lines but no figures, and the arrangement of the splice plates in the web stack is not determinable from the sections",
            "only the outer 1 1/8 web ply stops at the L11 gusset (inlaid gusset); the 3/4 web plies run through, the L11 splice plates / fill and the inlay plates at L10 / L12 (same layer as the continuous ply) are not built."));
        // BATCH 3c (GL10): W14 gussets 2'-5 in. to in. (p.85 PLAN at panel point W14); thickness not given
        foreach (var r in Csv("gusset-layers.csv").Where(r => r["structure"] == "main" && r["joint"] == "W14"))
            foreach (var pt in new[] { "W14", "W14'" })
            {
                var jw = tm.Joints.LastOrDefault(j => j.Point == pt && j.Plane.Length == 0);
                if (jw == null) { jw = new TrussJointDefinition { Point = pt, Source = "gusset-layers.csv p.85" }; tm.Joints.Add(jw); }
                if (Num(r["in_to_in_in"]) is > 0 and var wi) jw.GussetsInsideFaceDistance = In(wi);
                jw.Unresolved = U("p.85", $"gusset plates of joint {pt}", "the PLAN at panel point W14 prints '2'-5 In to In Guss.' but no gusset thickness and no outline dimensions (p.84 / p.85)",
                    "gussets 2'-5 in. to in. outside the hanger, 3/4 in. default thickness, envelope outline.");
                report.Add($"W14: {pt} gussets {jw.GussetsInsideFaceDistance / 0.0254:0.##} in. to in. (p.85; thickness not given)");
            }
    }
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
    int nFill = 0, nFillJ = 0, nBeyond = 0, nSpl = 0, nOuter = 0, nOmit = 0, nPrinted = 0, nPrintedNone = 0, nThin = 0;
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
        // agent review approach A08-A10 (2026-10-30): fills PRINTED on the joint sheets (approach-fills.csv) govern; a computed fill under 1/8 in. is not built
        // unless printed; joints whose sheet says "Omit Fills" (truss-approach/joints.csv) get none
        var printedFills = File.Exists(Path.Combine(dataDir, "approach-fills.csv")) ? Csv("approach-fills.csv").Where(r => SK(r["structure"]) == st).ToList() : new List<Dictionary<string, string>>();
        string unitL = st == "approach3" ? "A" : "B";
        var omitJ = File.Exists(Path.Combine(dataDir, "..", "truss-approach", "joints.csv"))
            ? File.ReadAllLines(Path.Combine(dataDir, "..", "truss-approach", "joints.csv")).Skip(1).Where(l => l.StartsWith(unitL + ",") && l.Contains("Omit Fills", StringComparison.OrdinalIgnoreCase)).Select(l => l.Split(',')[1].Trim()).ToHashSet()
            : new HashSet<string>();
        foreach (var jd in t.Joints.Where(j => j.Plane.Length == 0 && j.GussetsInsideFaceDistance > 0 && j.Outline.Count >= 3 && tokenRx.IsMatch(j.Point)))
        {
            jd.Plates = null;
            if (omitJ.Contains(jd.Point.TrimEnd('\''))) { nOmit++; continue; }
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
                // printed fill of this member at this joint (own sheet joint or the other half, member name in the sheet's orientation)
                string Mq(string n) => tokenRx.Match(n) is { Success: true } mq && int.Parse(mq.Groups[2].Value) != C ? (n.EndsWith("'") ? n.TrimEnd('\'') : n + "'") : n;
                string bareJ = pt.TrimEnd('\''); bool primed = pt.EndsWith("'");
                string sheetName = primed ? string.Join("-", new[] { Mq(m.From), Mq(m.To) }) : m.Name;
                var pf = printedFills.FirstOrDefault(r => r["joint"] == bareJ && (r["member"] == sheetName || r["member"] == string.Join("-", sheetName.Split('-').Reverse())));
                if (pf != null && (Num(pf["thk_in"]) ?? 0) <= 0) { nPrintedNone++; continue; }
                if (pf == null && tq < In(0.125) - 1e-9) { if (tq >= In(1.0 / 16) - 1e-9) nThin++; continue; }
                // printed widths are the flange width to 1/2 in. (label); the solid is kept within the (interpolated) flange so it cannot hang past the member
                double nominal = Math.Round(sd.FlangeWidth / In(0.5)) * In(0.5), width = Math.Min(nominal, sd.FlangeWidth);
                if (pf != null)
                {
                    tq = In(Num(pf["thk_in"])!.Value); nominal = In(Num(pf["width_in"])!.Value); width = Math.Min(nominal, sd.FlangeWidth); nPrinted++;
                }
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
                string why = pf != null
                    ? U("p." + pf["page"], $"fills between the flanges of {m.Name} and the gussets at {pt}", $"the joint sheet prints the fill (2-Fills {SBM.Core.Truss.TrussSections.FormatIn(nominal)} x {SBM.Core.Truss.TrussSections.FormatIn(tq)}) and draws it, but does not dimension its length (approach-fills.csv; computed in-to-in - depth would be {SBM.Core.Truss.TrussSections.FormatIn(Math.Max(0, gap))})",
                        "one fill per flange (pair) at the printed size from the member end over the lap under the drawn gusset outline" + (ext > 0 ? $" and {SBM.Core.Truss.TrussSections.FormatIn(ext)} past the gusset edge where the sheet draws rivets beyond it" : "") + ".")
                    : U(pg, $"fills between the flanges of {m.Name} and the gussets at {pt}",
                    $"the joint sheet lists the fills per member end (2-Fills b x t) and draws them, but dimensions neither their length nor every thickness, and this callout is not read into approach-fills.csv yet; thickness = (gusset in-to-in {SBM.Core.Truss.TrussSections.FormatIn(inToIn)} - member depth {SBM.Core.Truss.TrussSections.FormatIn(depth)}{(both && covers.Count > 0 ? " incl. its cover plates" : "")}) / 2 (the 1954 14 WF depths are interpolated, see the section flags), width = the flange width to 1/2 in.",
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
                        Label = $"2-Fills {SBM.Core.Truss.TrussSections.FormatIn(nominal)} x {SBM.Core.Truss.TrussSections.FormatIn(tq)}{(pf != null && pf["material"].Length > 0 ? $" ({pf["material"]})" : "")}", Material = pf != null && pf["material"].Length > 0 ? pf["material"] : sd.Material,
                        Source = pf != null ? $"p.{pf["page"]} (printed fill, approach-fills.csv)" : $"{pg} (fill = in-to-in - depth)", Unresolved = why,
                    });
                    nFill++; nHere++;
                    if (ext > 0) nBeyond++;
                }
            }
            if (nHere > 0) nFillJ++;
        }
        // ---- (b) chord splice material read from the sheets (approach-splices.csv; 3-span unit p.50-53): ranges u0..u1 along the chord in the sheet frame (inches from
        // the work point, + toward the higher panel number), split at the work point onto the chord member on each side (the model splits the chords at the panel
        // points; some sheets draw the CL of splice off the point, e.g. p.52 L8 1'-8 3/4 right of it - the plates stay where drawn). Data joint -> model joints:
        // the drawn half and the other half of the unit (same panel topology, mirrored), both units (the New Jersey unit is the same sheets).
        foreach (var r in spl.Where(r => SK(r["structure"]) == st))
        {
            string dj = r["joint"].Trim();
            foreach (var mj in new[] { dj, dj.EndsWith("'") ? dj.TrimEnd('\'') : dj + "'" }.Distinct())
            {
                if (tokenRx.Match(mj) is { Success: true } mm0 && int.Parse(mm0.Groups[2].Value) == C && mj.EndsWith("'")) continue;
                bool mirror = mj != dj;
                var jd = t.Joints.LastOrDefault(x => x.Point == mj && x.Plane.Length == 0);
                var P = Pt(mj);
                if (jd == null || P == null) continue;
                // chord members at the model joint by panel side (left = toward the lower panel number), or the chord passing through
                var chs = mems.Where(x => IsChordR(x.Role) && (x.From == mj || x.To == mj) && x.From[0] == mj[0] && x.To[0] == mj[0]).ToList();
                TrussMemberDefinition? Side(int sgn) => chs.FirstOrDefault(x => Math.Sign(Ord(x.From == mj ? x.To : x.From) - Ord(mj)) == sgn);
                var thrC = mems.FirstOrDefault(x => IsChordR(x.Role) && x.From[0] == mj[0] && x.To[0] == mj[0] && tokenRx.IsMatch(x.From) && tokenRx.IsMatch(x.To)
                    && Math.Min(Ord(x.From), Ord(x.To)) < Ord(mj) && Math.Max(Ord(x.From), Ord(x.To)) > Ord(mj));
                double u0 = In(Num(r["u0_in"]) ?? 0), u1 = In(Num(r["u1_in"]) ?? 0), w = In(Num(r["width_in"]) ?? 0), th = In(Num(r["thk_in"]) ?? 0);
                string kind = r["kind"];
                string flag = U("p." + r["page"], $"{r["label"]} at {mj}", $"size as printed; the extent is scaled from the joint sheet ({r["evidence"]}; {r["confidence"]} confidence, about +-1 in.)",
                    "plate as printed, extent as scaled" + (mirror ? ", mirrored from the drawn half" : "") + (kind == "bars" ? "; bars on the inside faces of the flanges, above and below the web, 1/4 in. inside the flange tips" : "") + ".");
                if (kind == "outside")
                {
                    // flange splice plates outside the gussets: a gusset layer (not clipped to the gusset) = a rectangle along the chord, centred on the chord axis
                    var cs = thrC != null ? new[] { thrC } : chs.ToArray();
                    double du = 0, dv = 0;
                    foreach (var x in cs) { var a = Pt(x.From)!; var b2 = Pt(x.To)!; double sx = b2.Station - a.Station, sy = b2.Elevation - a.Elevation; if (sx < 0) { sx = -sx; sy = -sy; } double l = Math.Sqrt(sx * sx + sy * sy); du += sx / l; dv += sy / l; }
                    double ln = Math.Sqrt(du * du + dv * dv); du /= ln; dv /= ln;
                    // sheet u (toward the higher panel number) -> model u along the higher stations: + for the PA unit, - for the mirrored half / the NJ unit
                    string? hiPt = thrC != null ? (Ord(thrC.To) > Ord(thrC.From) ? thrC.To : thrC.From) : (Side(1) is { } rs ? (rs.From == mj ? rs.To : rs.From) : null);
                    int sg = hiPt != null && Pt(hiPt) is { } hp ? Math.Sign(hp.Station - P.Station) : dir;
                    double a0 = sg > 0 ? u0 : -u1, a1 = sg > 0 ? u1 : -u0;
                    var poly = new[] { (a0, -w / 2), (a1, -w / 2), (a1, w / 2), (a0, w / 2) }.Select(q => new TrussUV { U = du * q.Item1 - dv * q.Item2, V = dv * q.Item1 + du * q.Item2 }).ToList();
                    (jd.GussetLayers ??= new()).Add(new TrussGussetLayer { Thickness = th, Outline = poly, ClipToGusset = false, Label = r["label"], Material = r["material"], Unresolved = flag, Source = $"approach-splices.csv p.{r["page"]}" });
                    nOuter++;
                    continue;
                }
                // plates on one member: the part of [u0, u1] on each side of the point, measured along that member away from the point
                var parts = new List<(TrussMemberDefinition m, double a0, double a1)>();
                if (thrC != null)
                {
                    int s0 = Math.Sign(Ord(thrC.To) - Ord(thrC.From));   // + = the member runs toward the higher panel number
                    parts.Add((thrC, s0 > 0 ? u0 : -u1, s0 > 0 ? u1 : -u0));
                }
                else
                {
                    if (u0 < 0 && Side(-1) is { } lm && (r["side"] is "" or "L")) parts.Add((lm, Math.Max(0, -u1), -u0));
                    if (u1 > 0 && Side(1) is { } rm && (r["side"] is "" or "R")) parts.Add((rm, Math.Max(0, u0), u1));
                }
                foreach (var (m, a0, a1) in parts.Where(q => q.a1 - q.a0 > In(0.5)))
                {
                    var sd = Sec(m);
                    var faces = kind == "bars" ? new[] { TrussPlateFace.WebInside, TrussPlateFace.WebInside } : new[] { TrussPlateFace.Top, TrussPlateFace.Bottom };
                    double cOff = kind == "bars" && sd != null ? sd.FlangeWidth / 2 - w / 2 - In(0.25) : 0;
                    for (int i = 0; i < faces.Length; i++)
                        (jd.Plates ??= new()).Add(new TrussJointPlate
                        {
                            Member = m.Name, Face = faces[i], Fill = kind == "webfill", Width = w, Thickness = th, Length = a1 - a0, Offset = (a0 + a1) / 2, CenterOffset = kind == "bars" ? (i == 0 ? cOff : -cOff) : 0,
                            Label = r["label"], Material = r["material"], Source = $"approach-splices.csv p.{r["page"]} ({r["confidence"]})", Unresolved = flag,
                        });
                    nSpl++;
                }
            }
        }
        int n0 = t.Unresolved.RemoveAll(s => s.Contains("fills, splice plates, lug angles, diaphragms and stitch rivets of the approach truss joints"));
        t.Unresolved.Add(U(pg, "lug angles, diaphragms and stitch rivets of the approach truss joints, and the fills drawn outside the joints (vertical stub ends, floor beam connection fills)",
            "they are listed on the joint sheets but not modeled (the truss generator has no input for them yet)",
            "not built. Built from the sheets: gusset layers (section 5), fills between the member flanges and the gussets (section 6a)" + (st == "approach3" ? ", chord splice bars / web splice plates / flange splice plates (section 6b, approach-splices.csv)." : ".")));
        if (st == "approach4")
            t.Unresolved.Add(U("p.60-67", "chord splice material of the 4-span joints (Splice Bars 6 x 1 1/8, Splice Pls 10 x t on the webs, flange Splice Pls 16 x 1 1/8 / 16 x 15/16, fills)",
                "the 4-span sheets draw the splice bars and the flange splice plates in the plan views only (edge on, outside the flange lines), so which face of the web the bars lie on and how the flange plates stack with the gussets is not shown; their lengths were not read joint by joint",
                "not built; the fills between the member flanges and the gussets are built (section 6a)."));
    }
    report.Add($"approach fills: {nFill} fill definitions (pairs) on {nFillJ} model joints, {nBeyond} past the gusset edge to the drawn beyond rivets; splice material: {nSpl} plates / bars on members, {nOuter} plates outside the gussets (approach-splices.csv); agent review A08-A10: {nPrinted} fills at the printed size (approach-fills.csv), {nPrintedNone} member ends with no fill printed, {nThin} computed fills under 1/8 in. not built, {nOmit} joints with \"Omit Fills\"");
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
