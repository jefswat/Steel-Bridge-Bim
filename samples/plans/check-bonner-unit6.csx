// Plan check of the Bonner Unit 6 FIB model (build-bonner-unit6.csx). Run from the repo root:
//   sbm run "samples\plans\check-bonner-unit6.csx" "samples\plans\Bonner Unit 6 FIB.sbm"
// Compares the generated model with the extraction CSVs in docs/plan-data/bonner-b2500/fib-unit6 (plan values) and prints PASS / DIFF rows
// (tolerances: 0.01 ft, 1/16 in, 0.1 CY) plus the generator's warnings. DIFF rows are reported plan differences, never fixed.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Substructure;

var inv = CultureInfo.InvariantCulture;
string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "bonner-b2500", "fib-unit6");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
List<string[]> Csv(string name)
{
    var rows = new List<string[]>();
    foreach (var line in File.ReadAllLines(Path.Combine(dataDir, name)).Skip(1))
    {
        if (line.Trim() == "") continue;
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
        rows.Add(cells.ToArray());
    }
    return rows;
}
double D(string s) => double.Parse(s, inv);
int pass = 0, diff = 0;
var diffs = new List<string>();
void Row(string item, double model, double plan, double tol, string unit, string note = "")
{
    bool ok = Math.Abs(model - plan) <= tol + 1e-9;
    string s = $"{(ok ? "PASS" : "DIFF")}  {item}: model {model:0.####} vs plan {plan:0.####} {unit} (diff {model - plan:+0.####;-0.####}){(note != "" ? "  - " + note : "")}";
    if (ok) pass++; else { diff++; diffs.Add(s); }
    Print(s);
}
double ft = Ft(1), inch = In(1), cy = Math.Pow(Ft(1), 3) * 27;
const string Name = "Bonner Unit 6";
Doc.Regenerate();
var b = Doc.Bridge(Name);
var fr = Doc.Framing(Name);
var al = fr.Alignment;
var ps = Doc.Prestressed(Name);
var sub = Doc.Substructure(Name);
string[] bents = { "Bent 29", "Bent 30", "Bent 31", "Bent 32", "Bent 33", "Bent 34", "Bent 35" };

// ---------------------------------------------------------------- 1. unit length (p395, unit6-spans-bents.csv)
Print("== 1. Unit layout (p395, p422-424)");
Row("unit length bent 29 - bent 35 along -L-", (b.Supports[^1].Station - b.Supports[0].Station) / ft, 965.0, 0.01, "ft");
for (int k = 0; k < 6; k++) Row($"span {30 + k} along -L-", (b.Supports[k + 1].Station - b.Supports[k].Station) / ft, 160 + 10 / 12.0, 0.01, "ft", "160'-10\"");
Row("girder count", ps.Beams.Count, 24, 0, "");

// ---------------------------------------------------------------- 2. bearing-to-bearing per span and girder (girders.csv b-b, p422-424)
Print("== 2. Bearing to bearing (plan, horizontal) per span / girder");
var girderRows = Csv("girders.csv");
string[] GRow(int span, int g) => girderRows.First(r => r[0].Split(',').Select(int.Parse).Contains(span) && (r[1] == "G1-G4" || r[1] == $"G{g}"));
foreach (var bm in ps.Beams.OrderBy(x => x.Span).ThenBy(x => x.Girder))
{
    int span = 30 + bm.Span, g = bm.Girder + 1;
    var r = GRow(span, g);
    Row($"b-b span {span} G{g}", bm.BearingSpan / inch, Len(r[10]) / inch, 1.0 / 16, "in", $"{r[10]}; bent stations printed to 0.01 ft (span by stations {(b.Supports[bm.Span + 1].Station - b.Supports[bm.Span].Station) / ft:0.00} ft)");
}

// ---------------------------------------------------------------- 3. girder spacing / offsets at the CL bearings (p404, p422-424, p481)
Print("== 3. Girder spacing at the CL bearings and offsets from -L-");
double[] planOff = { -17.25, -5.75, 5.75, 17.25 };
double worstOff = 0, worstSp = 0; string wOff = "", wSp = "";
foreach (var grp in ps.Beams.GroupBy(x => x.Span))
    foreach (bool atEnd in new[] { false, true })
    {
        var pts = grp.OrderBy(x => x.Girder).Select(x => (atEnd ? x.BearingEnd : x.BearingStart).XY).ToList();
        for (int g = 0; g < 4; g++)
        {
            double off = al.Inverse(pts[g]).Offset / ft - planOff[g];
            if (Math.Abs(off) > Math.Abs(worstOff)) { worstOff = off; wOff = $"span {30 + grp.Key} G{g + 1} {(atEnd ? "end" : "start")}"; }
        }
        for (int g = 0; g < 3; g++)
        {
            double sp = (pts[g + 1] - pts[g]).Length / ft - 11.5;
            if (Math.Abs(sp) > Math.Abs(worstSp)) { worstSp = sp; wSp = $"span {30 + grp.Key} G{g + 1}-G{g + 2} {(atEnd ? "end" : "start")}"; }
        }
    }
Row($"largest girder offset difference from -17'-3 / -5'-9 / 5'-9 / 17'-3 ({wOff})", worstOff, 0, 0.01, "ft");
Row($"largest spacing difference from 11'-6\" between CL bearings ({wSp})", worstSp, 0, 0.01, "ft");

// ---------------------------------------------------------------- 4. seat-to-deck stack at bents 30-34 (40 girder ends)
Print("== 4. Bearing seat (model) vs plan pedestal top (p483 / p486) and deck vs profile (seat-to-deck-check.csv), bents 30-34");
var pedRows = Csv("pedestals-cap-elevations.csv").ToDictionary(r => int.Parse(r[0]));
var stackRows = Csv("seat-to-deck-check.csv");
double worstSeat = 0; int nSeat = 0;
foreach (var r in stackRows)
{
    int bent = int.Parse(r[0]); bool back = r[1] == "back"; int span = int.Parse(r[2]); int g = int.Parse(r[3].Substring(1));
    var pad = ps.Pads.First(p => p.Support == $"Bent {bent}" && p.Span == span - 30 && p.Girder == g - 1);
    double seat = pad.Center.Z - pad.Thickness / 2;
    double planPed = D(pedRows[bent][(back ? 1 : 5) + g - 1]);
    nSeat++;
    worstSeat = Math.Max(worstSeat, Math.Abs(seat / ft - planPed));
    var so = al.Inverse(pad.Center.XY);
    double deck = b.Deck.FinishedElevation(al, so.Station, so.Offset);
    Row($"seat bent {bent} {r[1]} G{g} ({pad.BearingMark} + {pad.SolePlate?.Mark})", seat / ft, planPed, 0.01, "ft",
        $"deck at CL brg model {deck / ft:0.000} vs derived {D(r[9]):0.000}; sole plate {pad.SolePlate?.ThicknessAtBearing / inch:0.000}\" at CL brg");
}
Row("girder ends checked at bents 30-34", nSeat, 40, 0, "");
Print($"   largest |seat - plan pedestal| = {worstSeat:0.000} ft (plan elevations printed to 0.01 ft)");
// Bent 35 back (span 35) seats: pedestal "A".."D" of p486
foreach (var pad in ps.Pads.Where(p => p.Support == "Bent 35").OrderBy(p => p.Girder))
    Row($"seat bent 35 back G{pad.Girder + 1}", (pad.Center.Z - pad.Thickness / 2) / ft, D(pedRows[35][1 + pad.Girder]), 0.01, "ft");
foreach (var pad in ps.Pads.Where(p => p.Support == "Bent 29").OrderBy(p => p.Girder))
    Print($"   info  seat bent 29 ahead G{pad.Girder + 1}: {(pad.Center.Z - pad.Thickness / 2) / ft:0.000} ft (bent 29 pedestals are on the Unit 5 sheets - not checked)");

// ---------------------------------------------------------------- 5. substructure: caps, columns, piles
Print("== 5. Bents 30-35: cap volume (p483 55.6 CY), column tops, piles (piles.csv, p460)");
var capRows = Csv("bent-columns-caps.csv").ToDictionary(r => int.Parse(r[0]));
var pileRows = Csv("piles.csv").ToDictionary(r => int.Parse(r[0]));
foreach (var u in sub.Units.OrderBy(u => u.Name))
{
    int n = int.Parse(u.Name.Split(' ')[1]);
    double capV = u.Solids.Where(s => s.Kind == SubElementKind.Cap).Sum(s => Math.Abs(s.Mesh.Volume())) / cy;
    Row($"bent {n} precast cap volume", capV, 55.6, 0.05, "CY", "42'-0\" x 6'-6\" x 5'-6\"");
    foreach (var col in u.Columns)
        Row($"bent {n} column {col.Index + 1} top (cap soffit) vs printed top of column + 2\" grout bed", col.TopZ / ft, D(capRows[n][3]) + 2 / 12.0, 0.01, "ft");
    int piles = u.Foundations.Sum(f => f.Piles.Count);
    Row($"bent {n} piles", piles, int.Parse(pileRows[n][2]), 0, "");
    double tip = u.Foundations.SelectMany(f => f.Piles).Max(p => p.TipZ) / ft;
    Row($"bent {n} highest pile tip vs 'no higher than'", tip, D(pileRows[n][4]), 0.01, "ft");
    if (n is 30 or 31)
    {
        double lf = u.Foundations.SelectMany(f => f.Piles).Sum(p => Math.Sqrt(Math.Pow(p.TopZ - p.TipZ, 2) + Math.Pow(p.TipS - p.S, 2) + Math.Pow(p.TipN - p.N, 2))) / ft;
        Row($"bent {n} pile length total", lf, 1611, 1.0, "LF", "p460 quantity 14 piles = 1,611 LF");
    }
    double ftgV = u.Solids.Where(s => s.Kind == SubElementKind.Footing).Sum(s => Math.Abs(s.Mesh.Volume())) / cy;
    Print($"   info  bent {n} footing {ftgV:0.0} CY (flat 6'-0\"; p460 prints 180.1 CY for bents 30 / 31 - not reconciled, see UNRESOLVED)");
}

// ---------------------------------------------------------------- 6. camber table (p446, 168 cells)
Print("== 6. Camber / dead-load deflection table (camber-dead-load-deflection.csv, p446)");
var camberRows = Csv("camber-dead-load-deflection.csv");
int cells = 0, cellOk = 0, dataOk = 0;
foreach (var bm in ps.Beams)
{
    var tab = bm.Definition!.CamberTable!;
    foreach (var p in tab)
    {
        cells++;
        if (Math.Abs(p.FinalCamber - (p.Camber - p.Deflection)) <= In(1.0 / 32) + Ft(0.001) + 1e-9) cellOk++;   // 1/32" + rounding of the two 0.001 ft values
    }
}
// the 8 printed groups (each used by 3 or more beams): the model data equals the CSV
int groupCells = camberRows.Count;
foreach (var r in camberRows)
{
    double fc = D(r[3]) * ft, sdl = D(r[4]) * ft, f0 = D(r[2]);
    if (ps.Beams.Any(x => x.Definition!.CamberTable!.Any(p => Math.Abs(p.Fraction - f0) < 1e-9 && Math.Abs(p.Camber - fc) < 1e-9 && Math.Abs(p.Deflection - sdl) < 1e-9))) dataOk++;
}
Row("camber table cells present in the model (CSV rows)", dataOk, groupCells, 0, "", "168 printed cells");
Row("model camber cells with final = (camber - SDL) to the printed precision (all 24 beams x 21 points)", cellOk, cells, 0, "");
var mid = ps.Beams.First(x => x.Span == 0 && x.Girder == 0).Definition!.CamberTable!.First(p => Math.Abs(p.Fraction - 0.5) < 1e-9);
Row("span 30 exterior girder camber at 0.50", mid.Camber / ft, 0.392, 0.0005, "ft");
Row("span 30 exterior girder final camber at 0.50", mid.FinalCamber / inch, 2.125, 1.0 / 16, "in", "2 1/8\"");

// ---------------------------------------------------------------- 7. girder concrete, length, strands (girders.csv, p428-431, p439)
Print("== 7. Girders: concrete vs quantity, length A, strands");
var shape = StandardBeamCatalog.Find("FL FIB-96")!;
Row("FIB-96 depth", shape.Depth / inch, 96, 1.0 / 16, "in", "p438 8'-0\"");
Row("FIB-96 area (exact outline) vs area implied by 49.9 CY over 158'-3 1/2\"", shape.Area / 144, 49.9 * 27 / (158 + 3.5 / 12), 0.01, "ft2");
foreach (var bm in ps.Beams.OrderBy(x => x.Span).ThenBy(x => x.Girder))
{
    int span = 30 + bm.Span, g = bm.Girder + 1;
    var r = GRow(span, g);
    Row($"concrete span {span} G{g}", bm.Volume / cy, D(r[6]), 0.1, "CY");
}
foreach (var bm in ps.Beams.Where(x => x.Girder == 0 || x.Span == 5).OrderBy(x => x.Span).ThenBy(x => x.Girder))
{
    int span = 30 + bm.Span, g = bm.Girder + 1;
    var r = GRow(span, g);
    double l3d = (bm.TopEnd - bm.TopStart).Length;
    Row($"girder length A span {span} G{g} (model along the sloped top)", l3d / inch, Len(r[2]) / inch, 1.0 / 16, "in", $"A = {r[2]}; plan length {bm.Length / ft:0.000} ft - see UNRESOLVED 'girder length A'");
}
var b0 = ps.Beams[0];
Row("strands per girder (0.6\")", b0.Strands.Count(s => s.Diameter == 0), 72, 0, "");
Row("slack strands per girder (1/2\")", b0.Strands.Count(s => Math.Abs(s.Diameter - In(0.5)) < 1e-9), 4, 0, "");
Row("draped strands per girder", b0.Strands.Count(s => s.Depressed), 8, 0, "");
Row("debonded strands per girder", b0.Strands.Count(s => s.Debonded), 20, 0, "");
Row("hold-down from midspan", (b0.Length / 2 - b0.HoldDownFromEnd) / ft, 5.0, 0.05, "ft", "p428 5'-0\" each side of CL (measured along the sloped beam)");
Print($"   girder concrete total {ps.ConcreteVolume / cy:0.0} CY (24 girders; plan 49.9 x 4 + 50.5 x 20 = {49.9 * 4 + 50.5 * 20:0.0} CY)");

// ---------------------------------------------------------------- 8. diaphragms, joints, parapet, deck and girder reinforcing (2026-10-30)
Print("== 8. Diaphragms (p.405-408), joints (p.454), parapet (p.448-451), deck bars (p.409-414, p.455), girder bars (p.428-431)");
var cm = Doc.Concrete(Name);
var dm = Doc.Diaphragms(Name);
Row("diaphragms built (bents 29-35)", dm.Diaphragms.Count, 7, 0, "");
foreach (var d in dm.Diaphragms.OrderBy(d => d.SupportIndex))
{
    double w = (d.Parts.Max(p => p.XMax) - d.Parts.Min(p => p.XMin)) / inch;
    bool end = d.Kind == SBM.Core.Bridge.ConcreteDiaphragmKind.ExpansionJointEnd;
    Row($"{d.Name} width normal to the bent line", w, end ? 22 + 8 : 28, 1.0 / 16, "in", end ? "1'-10\" (MIN.) + 8\" deck-to-diaphragm fillet (p.407 K-K / p.408 N-N)" : "2'-4\" (p.406 / p.407 L-L)");
    Row($"{d.Name} length along the bent line", d.Length / ft, end ? 2 * (17.25 + 19 / 12.0 + 5 / 12.0 + 1 / 192.0) : 2 * (17.25 + 3.625), 0.01, "ft",
        end ? "19'-3\" (+1/16\") each side at the bottom, sloped end faces to the deck edge at the soffit (p.405)" : "20'-10 1/2\" each side (p.406 Section A-A)");
    Print($"   info  {d.Name}: {d.Volume / cy:0.00} CY");
}
Print($"   info  diaphragm concrete total {dm.ConcreteVolume / cy:0.0} CY (the plans give no separate diaphragm quantity: p.455 sand-lightweight concrete 1,541.1 CY = deck + diaphragms)");
Row("deck + haunch + diaphragms vs sand-lightweight concrete quantity (p.455)", (cm.DeckVolume + cm.HaunchVolume + dm.ConcreteVolume) / cy, 1541.1, 15, "CY",
    "1 % tolerance: build-up varies along the girders (p.404), joint blockouts / end post concrete are other items");
var j29 = cm.Joints.FirstOrDefault(j => j.Support == "Bent 29"); var j35 = cm.Joints.FirstOrDefault(j => j.Support == "Bent 35");
Row("expansion joints built (bent 29 modular, bent 35 Transflex)", cm.Joints.Count, 2, 0, "");
if (j35 != null) Row("bent 35 Transflex blockout (magnesium ammonium phosphate concrete)", j35.BlockoutVolume / cy, 0.6, 0.1, "CY", "p.455 MAP concrete 0.6 CY; 1'-3 1/2\" x 4\" x length between the parapets");
if (j29 != null) Print($"   info  bent 29 modular joint blockout {j29.BlockoutVolume / cy:0.00} CY (limited to the 9\" slab; the 1'-1\" min. blockout depth reaches into the diaphragm), device envelope {j29.Length / ft:0.00} ft");
var s29d = b.Supports[0]; var s35d = b.Supports[^1];
Row("deck end at bent 29 ahead of the bent control line", -s29d.DeckEndOffset / inch, 14.5 + 17, 1.0 / 16, "in", "CL modular joint 1'-2 1/2\" (+-) + DIM A / 2 (2'-10\" / 2), p.407 / p.454");
Row("deck end at bent 35 back of the bent control line", -s35d.DeckEndOffset / inch, 4.25 / 2, 1.0 / 16, "in", "CL joint = bent control line, DIM A 4 1/4\" (p.454)");
Print($"   info  bent 35 deck face {(-s35d.DeckEndOffset) / inch:0.000}\" vs diaphragm face 5\" from the bent control line (p.408 N-N): the slab overhangs the diaphragm by {(5 - (-s35d.DeckEndOffset) / inch):0.000}\" (two plan dimensions, not adjusted)");
Row("parapet concrete incl. end posts (both sides)", cm.BarrierVolume / cy, 209.2, 0.1, "CY", "p.451 Class AA concrete quantities for parapet, Unit 6");
foreach (var bar in cm.Barriers)
    Row($"{bar.Name}: solid pieces (35 open joints + 2 end posts + 1)", cm.BarrierPieces.TryGetValue(bar.Name, out var pcs) ? pcs.Count : 1, 38, 0, "", "p.448: 1/2\" joints at bents 30-34 and 28'-5\" + 4 @ 26'-0\" from each bent line");
double MarkN(string m) => cm.Marks.Where(x => x.Mark == m).Sum(x => x.Count);
double MarkLb(Func<SBM.Core.Concrete.BarMark, bool> f) => cm.Marks.Where(f).Sum(x => x.TotalMass) * 2.2046226218;
Row("A101 lines placed (top 1924 + bottom 2199)", MarkN("A101"), 1924 + 2199, 0, "", "bill p.455 prints 4125 = 1924 + 2199 + 2 (note: each plan-of-span bill carries 2 additional A101)");
Row("A101 length", cm.Marks.First(x => x.Mark == "A101").Length / inch, 42 * 12 + 1, 1.0 / 16, "in", "42'-1\" (3\" clear at the deck edges)");
Row("B107 placed", MarkN("B107"), 44, 0, ""); Row("B108 placed", MarkN("B108"), 44, 0, ""); Row("B109 placed", MarkN("B109"), 792, 0, "");
Row("B108 (closing bar) length", cm.Marks.First(x => x.Mark == "B108").Length / inch, 360, 3, "in", "30'-0\" printed; the closing bar takes what the line leaves (3\" end covers)");
Row("deck bill weight (A, B, E, G, K, S, U, H marks; placed + schedule)", MarkLb(x => !x.Location.Contains("parapet")), 299412, 299412 * 0.005, "lb", "p.455 total stainless 299,412 lb");
Print($"   info  parapet bars Unit 6 (placed + schedule, p.451 Unit 6 column): {MarkLb(x => x.Location.Contains("parapet")):N0} lb");
Print($"   info  deck bars placed in 3D: {cm.Bars.Count}");
foreach (var (m, n) in new[] { ("B101", 145), ("B102", 290), ("B103", 280), ("S1", 1924), ("B1", 384), ("B6", 160), ("B7", 16), ("E1", 32), ("E2", 8), ("F1", 24), ("S2", 22), ("B105", 116), ("B106", 29) })
    Row($"{m} placed", cm.Bars.Count(x => x.Mark == m), n, 0, "", m.StartsWith("B10") ? "p.409 / p.414 top B bars" : "p.448 / p.450 / p.451 parapet and end posts (both sides)");
Row("B104 placed (spans 31-34 + end spans)", cm.Bars.Count(x => x.Mark == "B104"), 261, 0, "", "spans 31-34: 2 x 29'-4\" lapped 2'-2\" close the gap exactly; end spans at the minimum laps (reading)");
Row("K302 placed (7 diaphragms)", cm.Bars.Count(x => x.Mark == "K302"), 252, 0, "", "6 rows x 2 faces x 3 bays (p.407 L-L / K-K, p.408 N-N)");
foreach (var (m, n) in new[] { ("S305", 280), ("U309", 140), ("H7", 34), ("U301", 4), ("U302", 4), ("U303", 4), ("U304", 4), ("U305", 4), ("U306", 8) }) Row($"{m} placed", cm.Bars.Count(x => x.Mark == m), n, 0, "", m == "H7" ? "p.445 temporary access openings (2 x 17)" : m.StartsWith("U30") && m != "U309" ? "p.405 sloped ends of the expansion joint diaphragms" : "p.406 A-A / p.407 L-L bent diaphragms");
Row("B104 schedule count", MarkN("B104"), 261, 0, "");
// rail (p.452, p.38)

var steelRail = Doc.Model(Name).Steel;
var rr = steelRail?.Railings;
int nPosts = rr?.Runs.Sum(x => x.Posts) ?? 0;
Row("2-bar metal rail posts (both sides)", nPosts, 314, 0, "", "p.38 4-bolt anchor assembly table, Unit 6 = 314; chains p.452");
// deck drains (p.409-414)
var drn = sub.Drainage.Where(x => x.Kind == SubElementKind.DrainPipe).ToList();
Row("6\" PVC pipe deck drains", drn.Count, 11 + 5 * 12, 0, "", "p.409 11 (span 30), p.410-414 12 per span");
foreach (var grp in ps.Beams.GroupBy(x => x.Span == 0 ? "30" : x.Span < 5 ? "31-34" : x.Girder < 2 ? "35 G1-G2" : "35 G3-G4"))
{
    var bm = grp.First();
    int N(string m) => bm.Bars.Count(x => x.Mark == m);
    var gr = GRow(30 + bm.Span, bm.Girder + 1);
    Row($"girder bars span {grp.Key}: S1 per girder", N("S1"), 148, 0, "");
    Row($"girder bars span {grp.Key}: S5 per girder", N("S5"), grp.Key == "30" ? 150 : 154, 0, "");
    Row($"girder bars span {grp.Key}: S6 per girder", N("S6"), grp.Key == "30" ? 111 : 36, 0, "");
    if (grp.Key != "30") Row($"girder bars span {grp.Key}: S8 per girder", N("S8"), 77, 0, "");
    Row($"girder bars span {grp.Key}: S3 per girder", N("S3"), 16, 0, "", "8-S3 each end (p.438 A-A)");
    Row($"girder bars span {grp.Key}: S4 per girder", N("S4"), 302, 0, "", "bar list 302; chain = 2 x (27 S2 + 10 S1 + S5 stations)");
}
Print($"   info  girder bars placed {ps.Beams.Sum(x => x.Bars.Count)} ({ps.RebarMass * 2.2046226218:N0} lb); S2 / S7 / V1 / WWF not placed (REMAINING.md)");

// ---------------------------------------------------------------- generator messages
Print("== Generator warnings (prestressed / substructure)");
foreach (var d in ps.Diagnostics.Concat(sub.Diagnostics).Concat(dm.Diagnostics).Concat(cm.Diagnostics).Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info)) Print("   " + d.Message);
Print("== Generator info (prestressed)");
foreach (var d in ps.Diagnostics.Where(d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Info)) Print("   " + d.Message);
Print("");
Print($"SUMMARY: {pass} PASS / {diff} DIFF");
foreach (var s in diffs) Print("  " + s);
