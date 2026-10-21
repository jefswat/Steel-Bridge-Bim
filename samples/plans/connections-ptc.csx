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
        "end stiffeners 2 angles per web face at the CL bearing (7 in. legs on the web on 15 x 1 1/8 fills, 4 in. legs outstanding: the 15 in. fill and the two rivet lines per angle drawn on p.44), intermediate pairs at the equal spaces of each panel between the CL bearing / floor beam lines (crimped over the 1 1/8 flange angle legs), 6x4x1/2 pairs on 1 1/8 fills at the floor beams; height between the flange angle outstanding legs (9'-4 1/4), fills / crimp zone between the flange angle leg toes.");
    string stiffRivFlag = U("p.44", "stiffener rivets", "the elevation draws the stiffener rivets (1 in. carbon, girder note p.44) without pitch or gage",
        "end stiffeners 2 staggered lines per angle at 2 1/2 / 5 in. from the heel, 4 in. pitch; floor beam stiffeners 1 line at 2 1/4 in., 5 in. pitch; intermediate stiffeners 1 line at 2 1/4 in., 7 in. pitch (the p.43 maximum pitch); first rivet 2 in. from the stiffener ends.");
    string splFlag = U("p.43, p.44", "web splices", "p.43 marks two web splices ('CL Splice') in the 6-equal-space panels, 4 spaces from the middle floor beam (scaled 19.6 ft from it vs 20'-3 1/4 for 4 spaces); p.44 prints 2-Splice Pls 21 x 3/8 centred on the splice with the crimped stiffener beside it on 2-Fills 4 x 3/8 and 2-Splice Pls 6 1/4 x 3/8 on the flange angles; the plate height and the rivet pattern are drawn, not dimensioned",
        "21 x 3/8 plate each web face, 8'-2 high (114 1/2 - 2 x 8 - 1/2), centred on the 4th intermediate stiffener position from the middle floor beam in both middle panels (that stiffener beside the plate on the middle floor beam side, on 3/8 fills); 3 rivet lines each side of the joint at 1 3/4 / 4 3/4 / 7 3/4 in., 3 1/2 in. pitch; the 6 1/4 x 3/8 flange angle splice plates not built.");
    string longFlag = U("p.44", "longitudinal stiffener L 6 x 4 x 7/16 (C) on the inside of the girder", "the elevation draws it between the transverse stiffeners in every panel (end, floor beam and splice panels) with one rivet line, without its height",
        "4 in. leg on the web hanging from its heel 2'-5 1/2 below the back of the top flange angles (scaled), 6 in. leg outstanding toward the CL bridge, from end stiffener to end stiffener, interrupted 1/2 in. clear of every transverse stiffener / fill / splice plate, 1 in. rivets at 7 in. (the drawn pitch) 2 1/4 in. below the heel.");
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
                Rivets = new() { RC(2.5, hInP, 4, 2), RC(5.0, hInP, 4, 4) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "stiffeners 2-Ls 6x4x1/2 at the floor beams", Angle = "L6x4x1/2", PerFace = 1, Height = hPl, Distances = fbs.Select(D).ToList(), Material = "C", Source = "p.44", Unresolved = stiffFlag,
                FillThickness = In(1.125), FillHeight = hFillP, Rivets = new() { RC(2.25, hInP, 5, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "intermediate stiffeners 2-Ls 6x4x7/16", Angle = "L6x4x7/16", PerFace = 1, Height = hInt, Distances = inter.Select(D).ToList(), Material = "C", Source = "p.44", Unresolved = stiffFlag,
                CrimpThickness = In(1.125), CrimpHeight = hFill, Rivets = new() { RC(2.25, hIn, 7, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "web splice 2-Pls 21 x 3/8", PlateWidth = In(21), PlateThickness = In(0.375), Height = hSplice, Distances = new() { D(sp1), D(sp2) }, Material = "S", Source = "p.43 / p.44", Unresolved = splFlag,
                Rivets = new[] { -7.75, -4.75, -1.75, 1.75, 4.75, 7.75 }.Select(g => RC(g, hSplice / In(1), 3.5, 2)).ToList(), RivetDiameter = In(1), RivetUnresolved = splFlag },
            new() { Label = "intermediate stiffeners 2-Ls 6x4x7/16 beside the web splice (2-Fills 4 x 3/8)", Angle = "L6x4x7/16", PerFace = 1, LegToward = 1, Height = hInt, Distances = new() { D(sp1) + halfPl }, Material = "C", Source = "p.44", Unresolved = splFlag,
                FillThickness = In(0.375), FillHeight = hFill, CrimpThickness = In(1.125), CrimpHeight = hFill, Rivets = new() { RC(2.25, hIn, 7, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
            new() { Label = "intermediate stiffeners 2-Ls 6x4x7/16 beside the web splice (2-Fills 4 x 3/8), second splice", Angle = "L6x4x7/16", PerFace = 1, LegToward = -1, Height = hInt, Distances = new() { D(sp2) - halfPl }, Material = "C", Source = "p.44", Unresolved = splFlag,
                FillThickness = In(0.375), FillHeight = hFill, CrimpThickness = In(1.125), CrimpHeight = hFill, Rivets = new() { RC(2.25, hIn, 7, 2) }, RivetDiameter = In(1), RivetUnresolved = stiffRivFlag },
        };
        m.LongitudinalStiffeners = new()
        {
            new TrussLongitudinalStiffener { Label = "long. stiffener L 6x4x7/16 (C) on the inside of the girder", Angle = "L6x4x7/16", Side = 0, Y = In(57.25) - FI(2, 5.5), LegDown = true,
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
