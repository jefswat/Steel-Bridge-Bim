// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - GIRDER SPANS 1-10 / 28-31: remaining visible details (2026-10-21, TRUSS-DESIGN.md section 22):
//   1. stringer splices at the middle floor beam of every span (p.43 STRINGER DIAGRAM '4-span continuous, CL stringer splice', p.45 STRINGER SPLICES)
//   2. tension plates 16 1/2 x 3/4 over the girder at every floor beam / bracket (p.44 Section A + plan, p.45 plan) with the 13/16 fills and 1 1/8 H.S. rivets
//   3. expansion dam diaphragms 18 WF 64 + 1-Pl 11 x 3/4 + 2-Ls 4 x 4 x 7/16 at the girder-span piers 1-9 / 28-30 (p.46 Section A / elevation),
//      the stringers continued 9 3/8 in. past the end floor beam to them
// Re-runnable (replaces its own inputs); run AFTER details-ptc and BEFORE rivets-ptc:
//   sbm run samples\plans\girder-details-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Every scaled value is a dimension-only flag (page + reason); values the sheets do not give are UNRESOLVED flags; nothing is adjusted.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Truss;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double FI(double ft, double inch) => Ft(ft) + In(inch);
TrussUV UV(double uIn, double vIn) => new TrussUV { U = In(uIn), V = In(vIn) };
var girders = new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" };
var report = new List<string>();
const string OwnAsm = "GIRDER-DETAILS";
const double GirderOffFt = 29.0;

// steel top under a point (vertical rays through the regenerated model), relative to the finished grade there; excludes parts above 'below' (rel. grade)
string bestPart = "";
double TopUnder(SteelModel st, SBM.Core.Alignments.Alignment al, IEnumerable<(double sta, double off)> pts, double belowRel, double refSta)
{
    double best = double.NegativeInfinity;
    var P = pts.Select(q => (q.sta, q.off, xy: al.PointAt(q.sta, q.off), g: al.ElevationAt(refSta, q.off))).ToList();
    double x0 = P.Min(p => p.xy.X) - 0.05, x1 = P.Max(p => p.xy.X) + 0.05, y0 = P.Min(p => p.xy.Y) - 0.05, y1 = P.Max(p => p.xy.Y) + 0.05;
    foreach (var part in st.Parts)
    {
        if (part.Mesh is not { IsEmpty: false } m || part.Assembly.Contains(OwnAsm) || part.Mark.StartsWith("SPL-")) continue;
        var b = m.Bounds();
        if (b.Max.X < x0 || b.Min.X > x1 || b.Max.Y < y0 || b.Min.Y > y1) continue;
        var pos = m.Positions; var idx = m.Indices;
        foreach (var p in P)
        {
            if (b.Max.X < p.xy.X || b.Min.X > p.xy.X || b.Max.Y < p.xy.Y || b.Min.Y > p.xy.Y) continue;
            for (int i = 0; i + 2 < idx.Count; i += 3)
            {
                var A = pos[idx[i]]; var B = pos[idx[i + 1]]; var C = pos[idx[i + 2]];
                double d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
                if (Math.Abs(d) < 1e-14) continue;
                double l1 = ((B.Y - C.Y) * (p.xy.X - C.X) + (C.X - B.X) * (p.xy.Y - C.Y)) / d, l2 = ((C.Y - A.Y) * (p.xy.X - C.X) + (A.X - C.X) * (p.xy.Y - C.Y)) / d, l3 = 1 - l1 - l2;
                if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
                double z = l1 * A.Z + l2 * B.Z + l3 * C.Z - p.g;
                if (z < belowRel && z > best) { best = z; bestPart = part.Mark; }
            }
        }
    }
    return best;
}

foreach (var bn in girders)
{
    var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn);
    var t = br?.Truss;
    if (t?.Floor == null) { report.Add($"{bn}: no truss floor - skipped"); continue; }
    var al = Doc.Project.FindAlignment(br!.AlignmentName)!;
    var st0 = Doc.Model(bn).Steel!;
    var tm0 = st0.Truss!;
    t.LineItems ??= new();
    t.LineItems.RemoveAll(li => li.Assembly == OwnAsm);
    foreach (var l in t.Floor.Stringers) { l.ExtendStart = 0; l.ExtendEnd = 0; }
    t.Floor.Splices = null;
    t.Unresolved.RemoveAll(s => s.Contains("stringer / floor beam tension plates") || s.Contains("girder-span expansion dam steel") || s.Contains("pieces 18 WF 64 under the finger plates"));
    // floor beam stations per span (girder member W.P.s; floor beams at the CL bearings + 3 equal spaces)
    var spans = t.Members.Where(m => m.Role == TrussMemberRole.Girder).Select(m => int.Parse(m.Name.Substring(1))).Distinct().OrderBy(n => n).ToList();
    double Sta(string pt) => t.Points.First(p => p.Name == pt).Station;
    var fbSta = spans.ToDictionary(n => n, n => { double a = Sta($"WA{n}"), b = Sta($"WB{n}"); return Enumerable.Range(0, 5).Select(k => a + k * (b - a) / 4).ToArray(); });

    // ============================================================ 1. stringer splices at the middle floor beam (p.43 diagram, p.45 details)
    // p.45 STRINGER SPLICES (all (C)): 24 WF 84 & 94: 2-Pls 18 1/2 x 3/8, 1-Pl 9 x 1/2 N.S., 1-Pl 4 x 9/16 F.S.; 21 WF 62: 2-Pls 18 1/2 x 3/8, 1-Pl 8 1/2 x 3/8 N.S.,
    // 1-Pl 3 1/2 x 7/16 F.S.; drawn 7 web rows, 6 columns (2 field + 4 shop) with the joint ('Fin.') between the 3rd and 4th; floor system rivets 7/8 carbon (p.45 note).
    string splFlag = U("p.45, p.43", "girder-span stringer splices (plate lengths, rivet pitch and gages)",
        "the p.45 splice details give the plate sizes and the drawn rivet pattern (7 rows, 6 columns, 2 flange lines) but no lengths, pitch or gages; the p.43 stringer diagram puts the splice at the middle support of the 4-span continuous stringer",
        "plates 9 in. each side of the joint, columns 1 1/2 / 4 1/2 / 7 1/2 in. each side, web rows at 2 1/2 in., flange lines at the AISC workable gage 5 1/2 in.; F.S. plates one each side of the web clear of the root fillet; at the middle floor beam of every span.");
    double[] cols = { -7.5, -4.5, -1.5, 1.5, 4.5, 7.5 };
    TrussStringerSpliceDefinition Spl(string sec, string label, double topW, double topT, double fsW, double fsT) => new()
    {
        Label = label, Section = sec, Stations = spans.Select(n => fbSta[n][2]).ToList(), Before = In(9), After = In(9), WebPlateHeight = In(18.5), WebPlateThickness = In(0.375),
        TopPlateWidth = topW, TopPlateThickness = topT, UnderPlateWidth = fsW, UnderPlateThickness = fsT,
        WebRivetColumns = cols.Select(c => In(c)).ToList(), WebRivetRows = 7, WebRivetPitch = In(2.5), FlangeRivetColumns = cols.Select(c => In(c)).ToList(), FlangeRivetGage = In(2.75),
        RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet", Material = "C", Unresolved = splFlag, Source = "p.43 / p.45",
    };
    t.Floor.Splices = new()
    {
        Spl("W24x84", "24 WF 84: 2-Pls 18 1/2 x 3/8, 1-Pl 9 x 1/2 N.S., 2-Pls 4 x 9/16 F.S.", In(9), In(0.5), In(4), In(9.0 / 16)),
        Spl("W24x94", "24 WF 94: 2-Pls 18 1/2 x 3/8, 1-Pl 9 x 1/2 N.S., 2-Pls 4 x 9/16 F.S.", In(9), In(0.5), In(4), In(9.0 / 16)),
        Spl("W21x62", "21 WF 62: 2-Pls 18 1/2 x 3/8, 1-Pl 8 1/2 x 3/8 N.S., 2-Pls 3 1/2 x 7/16 F.S.", In(8.5), In(0.375), In(3.5), In(7.0 / 16)),
    };
    t.Floor.Unresolved = t.Floor.Unresolved.Replace("bearing plates, splices, clip angles and diaphragms not built", "bearing plates, clip angles and the abutment diaphragms not built (splices, mid-panel diaphragms and the pier dam diaphragms are, girder-details-ptc.csx)");

    // ============================================================ 2. tension plates over the girder (p.44 Section A + plan, p.45 plan)
    // 'Tension Plate 16 1/2 x 3/4' across the girder top from the bracket B1 to the floor beam at every floor beam, '1 1/8 in. H.S. Rivets, holes reamed to a metal
    // templet' (2 lines x 5 each side drawn), '13/16 Fill - Omit at 1/4 Point' + '13/16 Fill' on the bracket / floor beam covers = the girder's extra cover plates
    // (86'-0 at the quarter points, 86'-0 + 51'-0 at the middle floor beam), none at the end floor beams. Length scaled 4'-3 (p.45) / 4'-5 (p.44).
    string tpFlag = U("p.44, p.45", "tension plates 16 1/2 x 3/4 over the girders (length, rivet layout, fills)",
        "the plate is called out 16 1/2 x 3/4 with 1 1/8 in. H.S. rivets but its length and the rivet pitch are only drawn (scaled 4'-3 on p.45, 4'-5 on p.44); the floor beams are plumb with level tops while the girder top follows the 3 % grade, so the plate cannot lie on both",
        "4'-3 long centred on the girder, level along the bridge and parallel to the roadway crown across it, its bottom 1/32 in. above the highest steel under it (probed: girder cover plates, floor beam / bracket covers); 13/16 fills (one at the quarter points, two at the middle floor beam) under it on the floor beam and bracket sides, from the girder cover plate edge to the plate end, filling the gap; 2 x 5 rivets 1 1/8 in. H.S. each side at 3 in. (lines 3 in. each side of the floor beam web, first 1 1/2 in. from the plate end).");
    int nTp = 0, nFill = 0;
    double half = In(25.5), halfW = In(8.25), coverHalf = In(11);
    foreach (var n in spans)
        for (int k = 0; k < 5; k++)
        {
            double sta = fbSta[n][k];
            int nCov = k == 2 ? 3 : (k == 1 || k == 3) ? 2 : 1;
            foreach (int sg in new[] { -1, 1 })
            {
                double go = sg * Ft(GirderOffFt);
                var pts = new List<(double, double)>();
                for (int iu = -2; iu <= 2; iu++)
                    for (int iw = -10; iw <= 10; iw++) pts.Add((sta + iu * halfW / 2, go + iw * half / 10));
                double top = TopUnder(st0, al, pts, -In(20), sta);
                if (double.IsNegativeInfinity(top)) { report.Add($"{bn}: no steel under the tension plate at span {n} floor beam {k} ({(sg < 0 ? "N" : "S")})"); continue; }
                if (n == 5 && Environment.GetEnvironmentVariable("DEBUG_TP") == "1") Print($"TP {n}-{k}{(sg < 0 ? "N" : "S")}: top {top / In(1):0.###} in rel. grade, by {bestPart}");
                double vb = top + In(1.0 / 32);              // plate bottom relative to the grade at each offset (the plate follows the crown across)
                string name = $"tension plate {n}-{k}{(sg < 0 ? "N" : "S")}";
                var rivets = new List<TrussLineItemRivet>();
                foreach (int side in new[] { -1, 1 })
                    foreach (double uu in new[] { -3.0, 3.0 })
                        for (int c = 0; c < 5; c++)
                            rivets.Add(new TrussLineItemRivet { U = In(uu), W = go + side * In(24 - 3 * c), V = vb + In(0.375), Axis = "V", Diameter = In(1.125), Material = "1 1/8 in H.S. rivet" });
                t.LineItems.Add(new TrussLineItemDefinition
                {
                    Name = name, Kind = TrussLineItemKind.Steel, Category = PartCategory.SplicePlate, Transverse = true, Station = sta, From = go - half, To = go + half, FromGrade = true,
                    Profile = new() { new TrussUV(-halfW, vb), new TrussUV(halfW, vb), new TrussUV(halfW, vb + In(0.75)), new TrussUV(-halfW, vb + In(0.75)) },
                    Material = "S", Assembly = OwnAsm, AssemblyDescription = $"{t.Name} - floor beam / girder tension plates and dam diaphragms", Source = "p.44 / p.45", Unresolved = tpFlag, Rivets = rivets,
                });
                nTp++;
                if (nCov < 2) continue;
                foreach (int side in new[] { -1, 1 })
                {
                    double o0 = go + side * (coverHalf + In(1.0 / 16)), o1 = go + side * half;
                    var fp = new List<(double, double)>();
                    for (int iu = -2; iu <= 2; iu++) for (int iw = 0; iw <= 6; iw++) fp.Add((sta + iu * In(7) / 2, o0 + (o1 - o0) * iw / 6));
                    double fb = TopUnder(st0, al, fp, -In(20), sta);
                    if (double.IsNegativeInfinity(fb) || vb - fb < In(1.0 / 16)) continue;
                    t.LineItems.Add(new TrussLineItemDefinition
                    {
                        Name = $"{name} fill {(side < 0 ? "a" : "b")}", Kind = TrussLineItemKind.Steel, Category = PartCategory.FillerPlate, Transverse = true, Station = sta,
                        From = Math.Min(o0, o1), To = Math.Max(o0, o1), FromGrade = true,
                        Profile = new() { new TrussUV(-In(7), fb), new TrussUV(In(7), fb), new TrussUV(In(7), vb), new TrussUV(-In(7), vb) },
                        Material = "S", Assembly = OwnAsm, AssemblyDescription = $"{t.Name} - floor beam / girder tension plates and dam diaphragms", Source = "p.44 / p.45",
                        Unresolved = tpFlag + $" Fill {(vb - fb) / In(1):0.###} in. (plan {nCov - 1} x 13/16).",
                    });
                    nFill++;
                }
            }
        }
    report.Add($"{bn}: {nTp} tension plates, {nFill} fills");

    // ============================================================ 3. expansion dam diaphragms at the girder-span piers (p.46)
    // Section A (CL dam = CL pier, end floor beams 2'-6 c-c = the CL bearings 1'-3 from it): stringer ends 11 1/4 apart (normal @ 68 F) = 5 5/8 from the CL dam,
    // 18 WF 64 web 5 1/2 inside the stringer end (= 3 7/8 beyond the end floor beam CL), on 2-Ls 4 x 4 x 7/16 per stringer web (5 rivets per leg drawn), top 10 3/4 below
    // the gutter line to the top of '1-Pl 11 x 3/4' on it (elevation); 'Pc. 18 WF 64 (Typ.)' supports under the finger plate and the 12 [ 35 curb channel are drawn,
    // not dimensioned (not built). All material carbon, rivets 7/8 carbon (p.46 notes).
    var w18 = SteelCatalog.FindWideFlange("W18x65")!;
    string damFlag = U("p.46", "girder-span dam diaphragms 18 WF 64 (station, top, connection angles)",
        "Section A gives the stringer ends (11 1/4 apart at 68 F), the diaphragm 5 1/2 inside them and the top of the 1-Pl 11 x 3/4 10 3/4 below the grade; the 1954 18 WF 64 is not in the AISC v16 extract and the angle length / rivet pitch are drawn only",
        "AISC W18x65 dimensions at the nominal 64 lb/ft, web plane 3 7/8 in. beyond the end floor beam CL toward the dam, top flange 11 1/2 in. below the grade, between the stringer web faces of every bay; 1-Pl 11 x 3/4 on top; 2-Ls 4 x 4 x 7/16 x 1'-3 straddling the diaphragm web on each stringer web, 2 3/4 in. below its top, 5 rivets @ 3 in. per leg; the stringers continued to 5 5/8 in. from the CL dam.");
    string pcFlag = U("p.46", "pieces 18 WF 64 under the finger plates, 12 [ 35 curb channel, bulkhead plate and anchor bars of the girder-span dams",
        "the elevation draws the 'Pc. 18 WF 64 (Typ.)' supports at about 1'-4 1/2 spacing on the 11 x 3/4 plate, the 12 [ 35 and the parapet bulkhead without dimensions",
        "not built.");
    t.Unresolved.Add(pcFlag);
    var damPiers = br.Supports.Where(s => System.Text.RegularExpressions.Regex.Match(s.Name, @"^Pier (\d+)$") is { Success: true } mm && int.Parse(mm.Groups[1].Value) is (>= 1 and <= 9) or (>= 28 and <= 30)).ToList();
    var lines = t.Floor.Stringers.Select(s => (s.Offset, tw: TrussSections.Build(t.Section(s.Section)!).WebThickness, w21: s.Section == "W21x62")).GroupBy(x => Math.Round(x.Offset, 4)).Select(g => g.First()).OrderBy(x => x.Offset).ToList();
    double vTop = -In(11.5), d = w18.Depth, bf = w18.FlangeWidth, tf = w18.FlangeThickness, tw = w18.WebThickness;
    List<TrussUV> IShape(double dd) => new()
    {
        new(-bf / 2, vTop), new(bf / 2, vTop), new(bf / 2, vTop - tf), new(tw / 2, vTop - tf), new(tw / 2, vTop - dd + tf), new(bf / 2, vTop - dd + tf), new(bf / 2, vTop - dd),
        new(-bf / 2, vTop - dd), new(-bf / 2, vTop - dd + tf), new(-tw / 2, vTop - dd + tf), new(-tw / 2, vTop - tf), new(-bf / 2, vTop - tf),
    };
    var prof = IShape(d);
    // outer bays (S1 21 WF 62 - S2): the 18 in. section would pass below the 21 WF bottom flange and its seat; the elevation draws its bottom at the 21 WF bottom ->
    // built 3/8 in. above the 21 WF bottom flange (shallower piece, UNRESOLVED depth)
    var w21 = SteelCatalog.FindWideFlange("W21x62")!;
    double dOuter = (t.Floor.StringerTopBelowGrade + w21.Depth - w21.FlangeThickness - In(0.375)) + vTop;
    var profOuter = IShape(dOuter);
    string outerFlag = U("p.46", "dam diaphragm in the outer bays (S1 21 WF 62 - S2)", "the elevation draws the 18 WF 64 in the 6'-1 bay with its bottom at the 21 WF bottom; an 18 in. section 11 1/2 in. below the grade would pass 1 in. below the 21 WF and its seat; the cope / reduced depth is not shown",
        $"built {dOuter / In(1):0.##} in. deep (bottom 3/8 in. above the 21 WF bottom flange), steel density, clip angles 1'-0 with 4 rivets.");
    var pl = new List<TrussUV> { new(-In(5.5), vTop), new(In(5.5), vTop), new(In(5.5), vTop + In(0.75)), new(-In(5.5), vTop + In(0.75)) };
    int nDia = 0, nExt = 0;
    foreach (var pier in damPiers)
        foreach (int dir in new[] { -1, 1 })                               // -1 = the span ending at the pier (back), +1 = the span starting there (ahead)
        {
            double brg = pier.Station + dir * FI(1, 3);
            // stringer lines of that span end continue 9 3/8 in. toward the dam
            foreach (var l in t.Floor.Stringers)
            {
                if (dir > 0 && l.FromStation is { } fs && Math.Abs(fs - brg) < 0.05) { l.ExtendStart = In(9.375); nExt++; }
                if (dir < 0 && l.ToStation is { } ts && Math.Abs(ts - brg) < 0.05) { l.ExtendEnd = In(9.375); nExt++; }
            }
            double sDia = brg - dir * In(3.875);
            for (int j = 0; j + 1 < lines.Count; j++)
            {
                double from = lines[j].Offset + lines[j].tw / 2, to = lines[j + 1].Offset - lines[j + 1].tw / 2;
                string nm = $"dam diaphragm {pier.Name} {(dir < 0 ? "back" : "ahead")} {(char)('a' + j)}";
                bool outer = lines[j].w21 || lines[j + 1].w21;
                int nr = outer ? 4 : 5;
                t.LineItems.Add(new TrussLineItemDefinition
                {
                    Name = nm, Kind = TrussLineItemKind.Steel, Category = PartCategory.TrussFloorBeam,
                    Transverse = true, Station = sDia, From = from, To = to, FromGrade = true, Profile = outer ? profOuter : prof, NominalWeightLbPerFt = outer ? 0 : 64, Density = outer ? 7850 : 0, Material = "C",
                    Assembly = OwnAsm, AssemblyDescription = $"{t.Name} - floor beam / girder tension plates and dam diaphragms", Source = "p.46", Unresolved = outer ? damFlag + " " + outerFlag : damFlag,
                    ClipAngles = new TrussClipAngles
                    {
                        Angle = "L4x4x7/16", HeelU = -tw / 2, Toward = -1, PairHeelU = tw / 2, Top = vTop - In(2.75), Length = outer ? In(12) : In(14.5), RivetDiameter = In(0.875), Material = "C",
                        ItemLegRivets = new TrussRivetColumn(In(2.5), nr, In(3), In(1.5)), WebLegRivets = new TrussRivetColumn(In(2.5), nr, In(3), In(1.5)), Unresolved = damFlag,
                    },
                });
                t.LineItems.Add(new TrussLineItemDefinition
                {
                    Name = nm + " Pl 11 x 3/4", Kind = TrussLineItemKind.Steel, Category = PartCategory.SplicePlate, Transverse = true, Station = sDia, From = from, To = to, FromGrade = true, Profile = pl,
                    Material = "C", Assembly = OwnAsm, AssemblyDescription = $"{t.Name} - floor beam / girder tension plates and dam diaphragms", Source = "p.46", Unresolved = damFlag,
                });
                nDia++;
            }
        }
    report.Add($"{bn}: {nDia} dam diaphragm pieces at {damPiers.Count} piers, {nExt} stringer ends continued");
}

// ================================================================ regenerate + save
var rep = Doc.Regenerate();
foreach (var bn in girders)
{
    var st = Doc.Model(bn).Steel; var tm = st?.Truss;
    if (st == null || tm == null) { Print($"{bn}: no model"); continue; }
    var own = st.Parts.Where(p => p.Assembly.Contains(OwnAsm)).ToList();
    Print($"{bn}: details {own.Count} parts {own.Sum(p => p.Mass) * 2.20462262:N0} lb, splices {st.Parts.Count(p => p.Mark.StartsWith("SPL-"))} ({st.Parts.Where(p => p.Mark.StartsWith("SPL-")).Sum(p => p.Mass) * 2.20462262:N0} lb), rivets {tm.Rivets:N0}, steel {st.SteelMass * 2.20462262:N0} lb");
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("stringer splices -") || d.Message.Contains("line item rivets") || d.Message.Contains("longitudinal stiffeners") || d.Message.Contains("stiffener rivets"))) Print("   " + d.Message[..Math.Min(400, d.Message.Length)]);
    foreach (var e in rep.Errors.Where(e => e.StartsWith(bn))) Print("  ERROR " + e);
}
foreach (var s in report) Print("  " + s);
if (Environment.GetEnvironmentVariable("DETAILS_CLASH") == "1")
    foreach (var gb in girders) foreach (var r in Doc.Clashes(gb, new SBM.Core.Checks.ClashOptions { Bolts = false }))
        foreach (var g in r.Clashes.GroupBy(c => c.CategoryPair).OrderByDescending(g => g.Count()))
        {
            Print($"  CLASH {r.Bridge} | {g.Key} | {g.Count()} | max {g.Max(c => c.Depth) / 0.0254:0.00} in");
            foreach (var c in g.OrderByDescending(c => c.Depth).Take(3)) Print($"      {c.A.Mark} ({c.A.Description[..Math.Min(60, c.A.Description.Length)]}) x {c.B.Mark} ({c.B.Description[..Math.Min(60, c.B.Description.Length)]}) {c.Depth / 0.0254:0.00} in");
        }
Doc.Save(outPath);
Print($"Saved {outPath}");
