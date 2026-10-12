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

// ---------------------------------------------------------------- generator messages
Print("== Generator warnings (prestressed / substructure)");
foreach (var d in ps.Diagnostics.Concat(sub.Diagnostics).Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info)) Print("   " + d.Message);
Print("== Generator info (prestressed)");
foreach (var d in ps.Diagnostics.Where(d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Info)) Print("   " + d.Message);
Print("");
Print($"SUMMARY: {pass} PASS / {diff} DIFF");
foreach (var s in diffs) Print("  " + s);
