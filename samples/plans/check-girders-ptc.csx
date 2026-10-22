// Check of the PTC GIRDER SPANS 1-10 / 28-31 (girders-ptc.csx) against the DRB-4 stress sheet (p.43), steel details (p.44-45) and the p.42 quantities:
// support / bearing stations vs supports.csv, span (c-c bearing) lengths, girder depth / plate sizes vs the G1 schedule, cover plate lengths vs the
// elevation dimension lines, floor beam spacing, stringer lines, bracing counts, shoe seats vs the p.43 bottom-of-shoe table, steel weight by material
// vs p.42 (items 1 carbon + 4 silicon, per state), deck slab volume vs the concrete item.
//   sbm run samples\plans\check-girders-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
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
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river");
    if (Directory.Exists(Path.Combine(p, "girders"))) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo (docs/plan-data/ptc-delaware-river/girders not found).");
double ft = 0.3048;
double FtIn(double f, double i) => f + i / 12.0;
bool Walk(PartCategory c) => c is PartCategory.Grating or PartCategory.WalkwayStringer or PartCategory.WalkwayPost or PartCategory.WalkwayRail or PartCategory.WalkwayPlate;   // walkways = p.42 items 14 / 15
var seats = File.ReadAllLines(Path.Combine(dataDir, "girders", "seats.csv")).Skip(1).Where(l => l.Trim() != "").Select(l => l.Split(','))
    .ToDictionary(f => f[0], f => (sta: Sta(f[1]), gutter: double.Parse(f[2], inv), shoe: double.Parse(f[3], inv)));

var rep = Doc.Regenerate();
var specs = new[] { ("PTC Girder spans 1-10", 1, 10, 2_496_000.0, 2_727_000.0, 2505.0), ("PTC Girder spans 28-31", 28, 4, 1_004_000.0, 1_093_000.0, 1035.0) };
double[] mass = new double[2];
foreach (var (bname, first, nSpans, planC, planS, planCY) in specs)
{
    var bdef = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bname);
    if (bdef?.Truss == null) { Print($"No truss model for {bname} (run girders-ptc.csx first)."); continue; }
    var bm = Doc.Model(bname);
    var tm = bm.Steel!.Truss!;
    var al = bm.Framing.Alignment;
    var t = bdef.Truss;
    Print($"=== {bname} on '{al.Name}': {tm.Members.Count} members, {tm.Steel.Parts.Count} parts, {tm.Shoes.Count} shoes");
    var sup = bdef.Supports.OrderBy(s => s.Station).ToList();

    // 1. spans: bearing stations, c-c lengths
    Print("--- 1. spans (p.42 / p.43: 123'-2 / 124'-5 pier to pier, simple spans, CL bearing 1'-3 from the CL pier)");
    for (int j = 0; j + 1 < sup.Count; j++)
    {
        int n = first + j;
        var ga = tm.Point("N", $"GA{n}")!; var gb = tm.Point("N", $"GB{n}")!;
        var wa = tm.Point("N", $"WA{n}")!; var wb = tm.Point("N", $"WB{n}")!;
        bool pierA = System.Text.RegularExpressions.Regex.IsMatch(sup[j].Name, @"^Pier \d+$"), pierB = System.Text.RegularExpressions.Regex.IsMatch(sup[j + 1].Name, @"^Pier \d+$");
        double expA = sup[j].Station + (pierA ? FtIn(1, 3) * ft : 0), expB = sup[j + 1].Station - (pierB ? FtIn(1, 3) * ft : 0);
        Cmp($"span {n} ({sup[j].Name} - {sup[j + 1].Name}): CL bearing stations vs supports.csv +/- 1'-3", 0, Math.Max(Math.Abs(wa.Station - expA), Math.Abs(wb.Station - expB)) / ft, 0.0005, " ft", dec: 4);
        double cc = (wb.Station - wa.Station) / ft;
        double plan = n == 10 || n == 28 ? FtIn(120, 5.25) : FtIn(121, 11);
        Cmp($"span {n}: c-c bearings", plan, cc, 0.001, " ft", n == 10 || n == 28 ? "p.43 120'-5 1/4 (spans 10 / 28)" : "p.43 121'-11 (spans 1-9, 29-31)", 3);
        double pp = (sup[j + 1].Station - sup[j].Station) / ft;
        double planPP = n == 1 || n == 31 ? FtIn(123, 2) : n == 10 || n == 28 ? FtIn(121, 8.25) : FtIn(124, 5);
        Cmp($"span {n}: {(pierA ? "CL pier" : "bearing")} to {(pierB ? "CL pier" : "bearing")} (p.42 elevation dimension line)", planPP, pp, 0.002, " ft", "", 3);
        Cmp($"span {n}: girder length end to end (horizontal) = c-c + 2 x 1'-1 (p.44)", FtIn(121, 11) + 2 * FtIn(1, 1) + (plan - FtIn(121, 11)), (gb.Station - ga.Station) / ft, 0.002, " ft", "", 3);
    }

    // 2. girders
    Print("--- 2. girder G1 (p.43 schedule, p.44 details)");
    var g1 = t.Sections.First(s => s.Name == "G1");
    Cmp("web plate depth x thickness: area furnished 49.88 sq in", 49.88, (g1.WebDepth / 0.0254) * (g1.WebPlies[0].Thickness / 0.0254), 0.01, " in2", dec: 3);
    Cmp("depth back to back of angles (normal to grade) 9'-6 1/2", FtIn(9, 6.5), (g1.WebDepth + 2 * g1.AngleProjection) / ft, 0.0005, " ft", dec: 4);
    Row(g1.Angle == "L8x8x1-1/8" ? "PASS" : "DIFF", "flange angles 4-Ls 8x8x1 1/8", "L8x8x1-1/8", g1.Angle);
    string Plates(List<TrussPlate> l) => string.Join(", ", l.Select(p => p.Length > 0 ? $"{p.Length / ft:0}'" : "F.L."));
    Row(Plates(g1.TopPlates) == "F.L., 86', 51'" ? "PASS" : "DIFF", "top cover plates 22 x 13/16: F.L., 86'-0, 51'-0", "F.L., 86', 51'", Plates(g1.TopPlates));
    Row(Plates(g1.BottomPlates) == "90', 74', 51'" ? "PASS" : "DIFF", "bottom cover plates 22 x 13/16: 90'-0, 74'-0, 51'-0", "90', 74', 51'", Plates(g1.BottomPlates));
    Row(Plates(g1.TopPlates) == "F.L., 86', 51'" ? "PASS" : "DIFF", "top cover plate 86'-0 confirmed by the user 2026-10-20 (p.43 stress sheet + p.44 label; the p.43 dimension lines 51 / 74 / 90 ft are not a conflict)", "86'-0 (top plate 2)", Plates(g1.TopPlates));
    foreach (var pl in new[] { "N", "S" })
    {
        var p5 = tm.Steel.Parts.FirstOrDefault(p => p.Mark == $"{pl}-G{first + Math.Min(4, nSpans - 1)}");
        if (p5 != null) Row("INFO", $"{pl} girder span {first + Math.Min(4, nSpans - 1)}: mass", "-", $"{p5.Mass * 2.20462262:N0} lb", $"{p5.Mass * 2.20462262 / ((tm.Points.First(x => x.Plane == pl && x.Name == $"GB{first + Math.Min(4, nSpans - 1)}").Station - tm.Points.First(x => x.Plane == pl && x.Name == $"GA{first + Math.Min(4, nSpans - 1)}").Station) / ft):N0} lb/ft (web + angles + covers only)");
    }
    var n1 = tm.Point("N", $"GA{first}")!; var s1 = tm.Point("S", $"GA{first}")!;
    Cmp("girder spacing c-c (p.43 58'-0)", 58, Math.Abs((n1.World.XY - s1.World.XY).Length) / ft, 0.001, " ft", dec: 3);
    var al0 = bm.Framing.Alignment;
    double st0 = n1.Station;
    double tbMid = al0.ElevationAt(st0, 38.25 * ft) - (n1.World.Z + FtIn(9, 6.5625) * ft / 2);
    Cmp("top of girder (back of top angles) below the gutter grade at the girder end (p.43 2'-9 3/8)", FtIn(2, 9.375), tbMid / ft, 0.0005, " ft", dec: 4);

    // 3. floor system
    Print("--- 3. floor system (p.43 plan: 4 spaces @ 30'-5 3/4 = 121'-11, F1 ends, F2 x 3, B1 brackets; stringers S1 / S2 / S3 / S4)");
    for (int j = 0; j < nSpans; j++)
    {
        int n = first + j;
        var fbs = tm.Members.Where(m => m.Mark.Contains($"-{n}-") && m.Role == TrussMemberRole.FloorBeam).ToList();
        if (j == 0 || j == nSpans - 1 || n == 10 || n == 28)
            Row(fbs.Count == 20 ? "PASS" : "DIFF", $"span {n}: floor beam + bracket members (5 lines x 2 planes x (half + bracket))", "20", fbs.Count.ToString());
        var f0 = tm.Point("N", $"F{n}_0")!; var f1 = tm.Point("N", $"F{n}_1")!;
        double sp = (f1.Station - f0.Station) / ft;
        double planSp = n == 10 || n == 28 ? FtIn(120, 5.25) / 4 : FtIn(30, 5.75);
        Cmp($"span {n}: floor beam spacing", planSp, sp, 0.002, " ft", n == 10 || n == 28 ? "spans 10 / 28: equal spaces of the c-c length (not printed on p.43: UNRESOLVED)" : "p.43 30'-5 3/4", 4);
    }
    var st = tm.Steel.Parts.Where(p => p.Category == PartCategory.TrussStringer).ToList();
    Row(st.Count == nSpans * 11 * 4 ? "PASS" : "DIFF", "stringer pieces (11 lines x 4 bays per span)", (nSpans * 44).ToString(), st.Count.ToString());
    var lin = st.GroupBy(p => System.Text.RegularExpressions.Regex.Replace(p.Description, @" \d+\+.*$", "")).Select(g => (g.Key, g.Count())).ToList();
    Row("INFO", "stringer pieces by section (21 WF 62 x 2 lines, 24 WF 84 x 2, 24 WF 94 x 7 per span)", "-", string.Join("; ", lin.Select(x => $"{x.Key} x{x.Item2}")));
    var lat = tm.Members.Where(m => m.Role == TrussMemberRole.LowerLateral).ToList();
    Row(lat.Count == nSpans * 8 ? "PASS" : "DIFF", "bottom laterals ST 6 WF 29 (8 per span, p.43 plan)", (nSpans * 8).ToString(), lat.Count.ToString());
    var sl = t.Floor!.Stringers.Where(s => s.Name.StartsWith($"{first}-")).ToList();
    Cmp("stringer lines: spacing S3-S3 8'-3", 8.25, Math.Abs(sl[3].Offset - sl[4].Offset) / ft, 0.0005, " ft");
    Cmp("S2 to G1 4'-0", 4.0, Math.Abs(sl[1].Offset) / ft - 29, 0.0005, " ft");
    Cmp("S1 to S2 6'-1", FtIn(6, 1), Math.Abs(sl[0].Offset - sl[1].Offset) / ft, 0.0005, " ft", dec: 4);
    Cmp("outer stringer lines S2 to S2: 8 spaces @ 8'-3 = 66'-0", 66, Math.Abs(sl[1].Offset - sl[9].Offset) / ft, 0.0005, " ft");
    // brackets B1 (p.45 note: as the approach truss spans, p.55: 10'-9 cantilever = S1 + 8 in., 2'-0 1/2 b. to b. of angles at the tip; flagged)
    {
        var br = tm.Steel.Parts.First(p => p.Mark == $"S-B1-{first}-1");
        // the bracket is set normal to grade (p.43 / p.44 notes, TRUSS-DESIGN.md section 25): its depth is measured along the leaning section (normal to the
        // grade at its station), not vertically (the 14 in. cover tilts with the grade)
        double brSta = br.Mesh.Positions.Average(v => al0.Inverse(v.XY).Station);
        double gB = al0.Profile.GradeAt(brSta);
        var tanB = SBM.Core.Geometry.Vec2.FromAngle(al0.AngleAt(brSta));
        var nB = new SBM.Core.Geometry.Vec3(-gB * tanB.X, -gB * tanB.Y, 1) * (1 / Math.Sqrt(1 + gB * gB));
        var pos = br.Mesh.Positions.Select(v => (o: al0.Inverse(v.XY).Offset, z: SBM.Core.Geometry.Vec3.Dot(v, nB))).ToList();
        double tip = pos.Max(q => q.o);
        var atTip = pos.Where(q => q.o > tip - 0.02).ToList();
        Cmp("bracket B1 tip from the CL: 29'-0 + 10'-9 = S1 + 8 in. (p.45 -> p.55 / p.48)", 39.75, tip / ft, 0.04, " ft", "tip cut in a vertical plane (section 25)", 4);
        Cmp("bracket B1 depth at the tip: 2'-0 1/2 b. to b. of angles + 1/2 cover plate (p.45 -> p.55)", 25.0 / 12, (atTip.Max(q => q.z) - atTip.Min(q => q.z)) / ft, 0.01, " ft", "measured normal to the grade", 4);
        var f2 = tm.Steel.Parts.First(p => p.Mark == $"S-F2-{first}-1");
        double o0 = f2.Mesh.Positions.Max(v => al0.Inverse(v.XY).Offset) / ft;
        Cmp("floor beam F2 end at the girder (11 in. = the girder cover plate edge; p.45 cover plates flush with the girder F.L. plate)", 29 - 11.0 / 12, o0, 0.04, " ft", "end cut square to the crowned beam axis: its bottom corner reaches 3/8 in. further", 4);
    }
    var d = t.Floor.Deck!;
    var curbs = (t.LineItems ?? new()).Where(li => li.Name.EndsWith("curb + parapet")).ToList();
    if (curbs.Count == 2) Cmp("slab: curb to curb 76'-6 (curb faces, deck-ptc.csx)", 76.5, Math.Abs(curbs[1].ReferenceOffset - curbs[0].ReferenceOffset) / ft, 0.0005, " ft");
    else Cmp("slab: curb to curb 76'-6 (no curbs: slab edges)", 76.5, (d.RightOffset - d.LeftOffset) / ft, 0.0005, " ft");
    Cmp("slab: outside to outside of the parapets 82'-2 (p.11 / p.38 / p.107)", 82 + 2.0 / 12, (d.RightOffset - d.LeftOffset) / ft, 0.0005, " ft", dec: 4);

    // 4. shoes vs the p.43 bottom-of-shoe table
    Print("--- 4. shoes (p.43 girder profile: bottom of shoe elevations at the bridge seats; p.44 W.P. to the masonry plate bottom 2'-0 1/2)");
    int okSeat = 0, dSeat = 0; double maxD = 0;
    foreach (var sh in tm.Shoes)
    {
        var def = t.Shoes.First(s => s.Name == sh.Name);
        if (!seats.TryGetValue(def.Support, out var sr)) { Row("INFO", $"shoe {sh.Name} seat", "-", def.Support, "no seat elevation in seats.csv"); continue; }
        double dz = (sh.SeatElevation - sr.shoe * ft) / ft;
        if (Math.Abs(dz) <= 1.0 / 96) okSeat++; else dSeat++;
    }
    Row("INFO", "shoe masonry plate bottom = the printed seat elevation (by construction)", $"{tm.Shoes.Count} shoes", $"{okSeat} within 1/8\" of the table, {dSeat} off", "model seat = the p.43 value");
    foreach (var c in tm.Checks.Where(c => c.Item.StartsWith("shoe")))
    { /* detailed rows below */ }
    int stackOk = tm.Checks.Count(c => c.Item.StartsWith("shoe") && c.Ok), stackBad = tm.Checks.Count(c => c.Item.StartsWith("shoe") && !c.Ok);
    maxD = tm.Checks.Where(c => c.Item.StartsWith("shoe")).Select(c => Math.Abs(c.Difference) / ft).DefaultIfEmpty(0).Max();
    Row(stackBad == 0 ? "PASS" : "DIFF", "W.P. (gutter - 2'-9 3/8 - 9'-6 9/16) less 2'-0 1/2 vs the p.43 seat (generator check per shoe)", $"{stackOk + stackBad} shoes", $"{stackOk} within 1/8\", {stackBad} off (max {maxD:F3} ft)",
        "the printed seat is one value per pier; the ahead shoe of a pier is 1'-3 up grade, i.e. 0.075 ft higher on 3 % than the back shoe: the model stack matches the back / end seats, the ahead seats differ by 2 x 1'-3 x 3 % (the beveled sole plate / masonry plate absorb it)");
    foreach (var kv in seats.Where(k => sup.Any(s => s.Name == k.Key)))
    {
        double g = al.ElevationAt(kv.Value.sta, 38.25 * ft) / ft;
        double diffTop = g - kv.Value.shoe;
        Row(Math.Abs(g - kv.Value.gutter) <= 0.003 ? "PASS" : "DIFF", $"{kv.Key}: model gutter profile vs p.43 gutter label", kv.Value.gutter.ToString("F3", inv), g.ToString("F3", inv),
            $"gutter - shoe bottom {diffTop:F3} ft (p.44 vertical stack 14'-4 7/16 = 14.370 at the bearing)");
    }

    // 5. weights
    Print("--- 5. steel weight and concrete");
    double lbS = tm.Steel.Parts.Where(p => (p.Material ?? "").Contains("(S)") && !SteelModel.IsTrussNonStructural(p.Category) && !Walk(p.Category)).Sum(p => p.Mass) * 2.20462262;
    double lbC = tm.Steel.Parts.Where(p => (p.Material ?? "").Contains("(C)") && !SteelModel.IsTrussNonStructural(p.Category) && !Walk(p.Category)).Sum(p => p.Mass) * 2.20462262;
    double lbOther = tm.Steel.SteelMass * 2.20462262 - lbS - lbC;
    Row("DIFF", "silicon steel (item 4: girders, floor beams, brackets, end stiffeners, plates)", $"{planS:N0} lb", $"{lbS:N0} lb ({lbS / planS:P0})", "not built: flange angle splice plates at the web splices, nose details; stiffener fills / crimps, web splices, tension plates (connections-ptc.csx, girder-details-ptc.csx) are in");
    Row("DIFF", "carbon steel (item 1: stringers, laterals, diaphragms, dams, bracing, castings listed apart)", $"{planC:N0} lb", $"{lbC:N0} lb ({lbC / planC:P0})", "not built: Pc. 18 WF 64 dam supports, 12 [ 35 curb channels, abutment diaphragms; longitudinal stiffeners, stringer splices, 18 WF 64 pier dam diaphragms (girder-details-ptc.csx), mid-panel 15[ 33.9, dam bands and shoe bodies (C) are in");
    Row("INFO", "other steel in the model (shoes / masonry plates as steel parts)", "-", $"{lbOther:N0} lb");
    Row("INFO", "total", $"{planC + planS:N0} lb", $"{tm.Steel.SteelMass * 2.20462262:N0} lb ({tm.Steel.SteelMass * 2.20462262 / (planC + planS):P1})");
    double cy = tm.SlabArea * (d.Thickness / ft) / 27.0 * 1.0;   // ft2 x ft / 27
    double cyDeck = ((bm.Concrete?.DeckVolume ?? 0) + (bm.Concrete?.HaunchVolume ?? 0) + (bm.Concrete?.BarrierVolume ?? 0)) / 0.764554857984;
    Row(Math.Abs(cyDeck - planCY) <= 0.03 * planCY ? "PASS" : "DIFF", "deck concrete: slab 6 3/4 + thickening + curbs / parapets (deck-ptc.csx) vs item 11 Class A (3 %)", $"{planCY:N0} CY", $"{cyDeck:N0} CY ({cyDeck / planCY:P1})");
    Row("INFO", "generator diagnostics", "-", $"{rep.Warnings.Count(w => w.StartsWith(bname))} warnings, {rep.Errors.Count(e => e.StartsWith(bname))} errors");
}
// 6. between the two bridges
var b1 = Doc.Project.Bridges.FirstOrDefault(b => b.Name == "PTC Girder spans 1-10"); var b2 = Doc.Project.Bridges.FirstOrDefault(b => b.Name == "PTC Girder spans 28-31");
if (b1?.Truss != null && b2?.Truss != null)
{
    var t1 = Doc.Model(b1.Name).Steel!.Truss!; var t2 = Doc.Model(b2.Name).Steel!.Truss!;
    var w10 = t1.Point("N", "WB10")!; var w28 = t2.Point("N", "WA28")!;
    Cmp("CL brg girder span 10 to CL brg girder span 28 (p.42 4,837'-5 1/2)", FtIn(4837, 5.5), (w28.Station - w10.Station) / ft, 0.002, " ft", dec: 3);
}
Print($"Summary: {pass} PASS / {diff} DIFF / {info} INFO");
