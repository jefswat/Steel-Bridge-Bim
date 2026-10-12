// Plan vs model check for MnDOT Br. 02069 (T.H. 65 S.B.), model built by samples/plans/build-br02069.csx:
//   sbm run "samples\plans\check-br02069.csx" "samples\plans\Br02069 TH65.sbm"        (run from the repo root; read-only)
// Checks (tolerance 0.01 ft unless noted; plan values are printed to 0.01 ft / 0.001 ft for coordinates):
//   working points A-F, CP1, CP2 (p3 / p1 / p34, working-points.csv): plan X/Y vs model (bearing points / working line);
//   top of roadway at the working points and at begin / end bridge (p1, p3);
//   bridge seats / pedestal tops (p3, p5, p10, seat-elevations.csv): model bearing seat (bottom of the bearing) and the as-designed
//   pedestal (beam step) top; the pedestals for the future beam lines (+21/+27/+33) are built now (standalone pedestals, p5 / p10; bars p8 / p14);
//   pedestal sizes (p5/p6, p10/p12); beam shape MN 30MH and debonding A/B/C (p16);
//   span 99.25 (p1, p15), beam length 100.50 (p16), begin / end bridge stations (p1), deck area 4220 SF (SOQ p2, nearest SF).
// Prints a pass / fail table and a summary line. Horizontal datum not stated (U1): X/Y are the plan's local grid.
using System.IO;
using SBM.Core.Geometry;

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "br02069-th65");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/br02069-th65 not found above the current directory - run from the repo.");
}
string dir = DataDir();
List<string[]> Csv(string name) => File.ReadAllLines(Path.Combine(dir, name)).Skip(1).Where(l => l.Trim().Length > 0)
    .Select(l => l.Split(',').Select(x => x.Trim()).ToArray()).ToList();
double? Num(string s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;

const string BrName = "Br02069 TH65 SB";
const double Tol = 0.01;
double ft = Ft(1);
var b = Doc.Bridge(BrName);
var al = Doc.Alignment(b.AlignmentName);
Doc.Regenerate();
var fr = Doc.Framing(BrName);
var ps = Doc.Prestressed(BrName);
var sub = Doc.Substructure(BrName);

var rows = new List<(string group, string item, string plan, string model, string diff, string status, string note)>();
int pass = 0, fail = 0, na = 0;
void Row(string group, string item, double? plan, double? model, string note = "", double tol = Tol, string fmt = "F3")
{
    if (plan == null || model == null)
    {
        rows.Add((group, item, plan?.ToString(fmt) ?? "-", model?.ToString(fmt) ?? "-", "-", "N/A", note));
        na++;
        return;
    }
    double d = model.Value - plan.Value;
    bool ok = Math.Abs(d) <= tol + 1e-9;
    if (ok) pass++; else fail++;
    rows.Add((group, item, plan.Value.ToString(fmt), model.Value.ToString(fmt), d.ToString("+0.000;-0.000;0.000"), ok ? "PASS" : "FAIL", note));
}
double Fin(double sta, double off) => b.Deck.FinishedElevation(al, sta, off) / ft;
int GirderAt(double offFt) => (int)Math.Round((offFt + 21) / 6);
int SupportOf(string sta) => Math.Abs(Sta(sta) - b.Supports[0].Station) < Ft(0.01) ? 0 : 1;

// ---------------------------------------------------------------- working points
foreach (var r in Csv("working-points.csv"))
{
    string pt = r[0], sta = r[2];
    double? x = Num(r[4]), y = Num(r[5]), rdwy = Num(r[6]), seat = Num(r[8]);
    double off = Num(r[3]) ?? double.NaN;
    if (x == null || double.IsNaN(off)) { if (rdwy == null) continue; }
    Vec2? mp = null;
    string src = "";
    if (!double.IsNaN(off) && (pt is "A" or "B" or "E" or "F"))
    {
        var br = fr.Supports[SupportOf(sta)].Bearings[GirderAt(off)]!;
        mp = br.TopOfSteel.XY; src = $"CL brg G{GirderAt(off) + 1} (framing)";
    }
    else if (!double.IsNaN(off)) { mp = al.PointAt(Sta(sta), Ft(off)); src = "alignment sta/off"; }
    if (mp is { } p && x != null)
    {
        Row("Working points", $"{pt} X ({sta}, {off:+0.00;-0.00;0.00})", x, p.X / ft, src);
        Row("Working points", $"{pt} Y", y, p.Y / ft, src);
    }
    if (rdwy != null) Row("Top of roadway", $"{pt} {sta} off {(double.IsNaN(off) ? 0 : off):+0.00;-0.00;0.00}", rdwy, Fin(Sta(sta), double.IsNaN(off) ? 0 : Ft(off)), "PG - 0.02|off| (deck finished grade)");
    if (seat != null)
    {
        int g = GirderAt(off), j = SupportOf(sta);
        var bs = ps.BearingSeats.First(s => s.Support == b.Supports[j].Name && s.Girder == g);
        Row("Bridge seat (W.P.)", $"{pt} G{g + 1} {b.Supports[j].Name}", seat, bs.SeatElevation / ft, "model seat = bottom of bearing (" + (j == 1 ? "E1 L 6 1/2\")" : "F1 L 3 1/4\")"));
    }
}

// ---------------------------------------------------------------- pedestal tops (seat-elevations.csv)
foreach (var r in Csv("seat-elevations.csv"))
{
    string side = r[0];
    double off = Num(r[3])!.Value, plan = Num(r[5])!.Value;
    int j = side == "S" ? 0 : 1;
    string sname = b.Supports[j].Name;
    bool future = r[2].Contains("future");
    if (future)
    {
        // Built now for a future beam (p5 note 6 Type B / p10 note 6 Type D; bars in the bar lists p8 / p14 and quantities p4 / p9).
        var fp = sub.Units.First(u => u.Name == sname).Pedestals.FirstOrDefault(p => p.Girder < 0 && Math.Abs(p.S - Ft(off)) < Ft(0.01));
        Row("Pedestal tops", $"{side} ped {r[1]} off {off:+0.00;-0.00} (future beam, built now)", plan, fp?.SeatZ / ft, "standalone pedestal (plan input)" + (fp == null ? " - NOT FOUND in the model" : ""));
        Row("Pedestal size", $"{side} ped {r[1]} future beam along beam (ft)", 28.0 / 12, fp?.WidthN / ft, side == "S" ? "p6 Type B 2'-4\"" : "p12 Type D 2'-4\"");
        Row("Pedestal size", $"{side} ped {r[1]} future beam along abutment (ft)", side == "S" ? 56.0 / 12 : 42.0 / 12, fp?.LengthS / ft, side == "S" ? "p5 4'-8\"" : "p10 3'-6\"");
        Row("Pedestal size", $"{side} ped {r[1]} future beam centre offset (ft)", off, fp?.S / ft, side == "S" ? "p5 chain 3\" + 10 x 4'-8\" + 9 x 1'-4\" + 8\"" : "p10 chain 10\" + 10 x 3'-6\" + 9 x 2'-6\" + 1'-3\"");
        continue;
    }
    int g = GirderAt(off);
    var unit = sub.Units.First(u => u.Name == sname);
    var ped = unit.Pedestals.FirstOrDefault(p => p.Girder == g);
    Row("Pedestal tops", $"{side} ped {r[1]} G{g + 1} as-designed step top", plan, ped?.SeatZ / ft, "pedestal solid (plan input)");
    // Pedestal size (S Type A p5/p6: 4'-8" along the abutment; N Type C p10/p12: 3'-6"; both 2'-4" along the beam = 1'-0" behind + 1'-4" ahead of CL brg)
    Row("Pedestal size", $"{side} G{g + 1} along beam (ft)", 28.0 / 12, ped?.WidthN / ft, side == "S" ? "p6 Type A 2'-4\"" : "p12 Type C 2'-4\"");
    Row("Pedestal size", $"{side} G{g + 1} along abutment (ft)", side == "S" ? 56.0 / 12 : 42.0 / 12, ped?.LengthS / ft, side == "S" ? "p5 4'-8\"" : "p10 3'-6\"");
    var bs = ps.BearingSeats.First(s => s.Support == sname && s.Girder == g);
    Row("Bearing seats", $"{side} G{g + 1} off {off:+0.00;-0.00} model seat", plan, bs.SeatElevation / ft,
        j == 1 ? "PG - 2%|off| - 9 - 4 1/4 - 30 - 6 1/2\" (E1)" : "PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4\" (F1)");
}

// ---------------------------------------------------------------- span, beam, deck
Row("Layout", "Span 1 CL brg - CL brg (support stations)", 99.25, (b.Supports[1].Station - b.Supports[0].Station) / ft);
foreach (var beam in ps.Beams)
    Row("Layout", $"{beam.Mark} bearing span", 99.25, beam.BearingSpan / ft);
foreach (var beam in ps.Beams)
    Row("Layout", $"{beam.Mark} beam length o-o", 100.50, beam.Length / ft, "p16 100'-6\"");
{
    var st = fr.Deck!.Outline.Select(q => al.Inverse(q.XY)).ToList();
    Row("Layout", "Begin bridge (end of slab S) station", Sta("248+32.92") / ft, st.Min(s => s.Station) / ft, "p1 248+32.92");
    Row("Layout", "End bridge (end of slab N) station", Sta("249+35.83") / ft, st.Max(s => s.Station) / ft, "p1 249+35.83");
    Row("Layout", "Deck left edge offset", -23.50, st.Min(s => s.Offset) / ft);
    Row("Layout", "Deck right edge offset", 17.50, st.Max(s => s.Offset) / ft);
    double area = fr.Deck.Area / (ft * ft);
    Row("Layout", "Deck area (SF, SOQ nearest SF)", 4220, Math.Round(area), $"model {area:F1} SF = 41'-0\" x 102'-11\"", 0.5, "F1");
}
// one value of a group in ft (null if empty; a mixed group gives a value off by the spread -> FAIL)
double? Same(IEnumerable<double> v) { var l = v.ToList(); if (l.Count == 0) return null; double lo = l.Min(), hi = l.Max(); return (hi - lo < 1e-9 ? lo : lo - (hi - lo)) / ft; }
// Beam shape and debonding (p16): MnDOT 30MH from the standard catalog; A 4 @ 9'-0", B 6 @ 13'-0", C 4 @ 17'-0" each end.
foreach (var beam in ps.Beams)
{
    Row("Beam", $"{beam.Mark} shape {beam.Shape.Key} (1 = MN 30MH)", 1, beam.Shape.Key == "MN 30MH" ? 1 : 0, "StandardBeamCatalog", 0, "F0");
    Row("Beam", $"{beam.Mark} strands", 44, beam.Strands.Count, "p16", 0, "F0");
    foreach (var (sym, n, len) in new[] { ("A", 4, 9.0), ("B", 6, 13.0), ("C", 4, 17.0) })
    {
        var grp = beam.Strands.Where(x => x.DebondSymbol == sym).ToList();
        Row("Debonding", $"{beam.Mark} {sym} count", n, grp.Count, "p16", 0, "F0");
        Row("Debonding", $"{beam.Mark} {sym} length start (ft)", len, Same(grp.Select(x => x.DebondStart)), $"p16 {len}'-0\"");
        Row("Debonding", $"{beam.Mark} {sym} length end (ft)", len, Same(grp.Select(x => x.DebondEnd)), $"p16 {len}'-0\"");
    }
    Row("Debonding", $"{beam.Mark} debonded total", 14, beam.Strands.Count(x => x.Debonded), "p16", 0, "F0");
}
// Stool at CL brg (p3: 4 1/4") from the model: deck bottom - beam top at the bearing.
foreach (var beam in ps.Beams.Where(x => x.Girder == 0 || x.Girder == 6))
    foreach (var (pnt, end) in new[] { (beam.BearingStart, "S"), (beam.BearingEnd, "N") })
    {
        var so = al.Inverse(pnt.XY);
        double stool = (b.Deck.FinishedElevation(al, so.Station, so.Offset) - b.Deck.Thickness - pnt.Z) / In(1);
        Row("Stool", $"{beam.Mark} stool at CL brg {end} (in)", 4.25, stool, "p3 4 1/4\"; tolerance 1/16\"", 1.0 / 16, "F4");
    }

// Beam mild reinforcing (p16, Fig. 5-397.501 mod. + Fig. 5-397.501(B)): stirrup stations per half 2" + 4 sps @ 2 1/2" + 9 @ 4" + 19 @ 9" + 27 @ 14"
// (+ 6" to CL span) = 60 per half; G505E pairs at the first station, G508E pairs at the other four of the 2 1/2" group, G402E elsewhere,
// G301E at every station (+1/2" against the stirrup), 2-G303E with G505E / G508E. No bar bill (p16): weight printed as N/A.
{
    var chain = SBM.Core.Prestressed.PrestressedBeamRebar.Chain(2, (4, 2.5), (9, 4), (19, 9), (27, 14));
    Row("Beam rebar", "stirrup stations per half (p16 chain)", 60, chain.Count, "2 + 4@2 1/2 + 9@4 + 19@9 + 27@14", 0, "F0");
    Row("Beam rebar", "chain + 6\" to CL span = half beam (in)", 603, chain[^1] + 6, "100'-6\" / 2", 0, "F3");
    foreach (var beam in ps.Beams.Where(x => x.Girder == 0 || x.Girder == 6))
    {
        foreach (var (mk, n) in new[] { ("G402E", 110), ("G505E", 4), ("G508E", 16), ("G301E", 120), ("G303E", 20) })
            Row("Beam rebar", $"{beam.Mark} {mk} bars", n, beam.Bars.Count(x => x.Mark == mk), mk == "G303E" ? "with G505E / G508E only (G402E zone UNRESOLVED)" : "p16", 0, "F0");
        Row("Beam rebar", $"{beam.Mark} G806E bars", null, beam.Bars.Count(x => x.Mark == "G806E"), "UNRESOLVED: no length / end distance (p16)", 0, "F0");
        var ax = (beam.TopEnd - beam.TopStart).Normalized();
        double L = (beam.TopEnd - beam.TopStart).Length / In(1);
        var exp = chain.Select(v => v + 0.5).Concat(chain.Select(v => L - v - 0.5)).OrderBy(v => v).ToList();
        var mod = beam.Bars.Where(x => x.Mark == "G301E").Select(x => Vec3.Dot((x.Points[0] + x.Points[^1]) * 0.5 - beam.TopStart, ax) / In(1)).OrderBy(v => v).ToList();
        if (mod.Count == exp.Count)
            Row("Beam rebar", $"{beam.Mark} G301E worst station difference vs chain (in)", 0, exp.Zip(mod, (e, x) => x - e).OrderByDescending(Math.Abs).First(), "1/16\"", 1.0 / 16, "F4");
        double lb = beam.Bars.Sum(x => { var m = ps.MarkOf(x); return m.Length * m.Size.MassPerLength; }) * 2.2046226218;
        Row("Beam rebar", $"{beam.Mark} reinforcing per beam (lb)", null, lb, "no bar bill on p16 (paid with the beam); centerline lengths", 0, "F0");
    }
}

// ---------------------------------------------------------------- semi-integral abutment: p.21 SECTION A-A (between beams) / B-B (at beams), p.11 A-A / Detail A / C-C
// Vertical probes through the generated solids at (offset, x from CL brg toward the span): bottom / top of the diaphragm, polystyrene, stem, infill
// and mask wall vs the plan chain. Plan values: back face 2'-6" behind CL brg (1'-3" + 3" + 1'-0"), front face 6" ahead, paving bracket 8" x 1'-4",
// back part on the ledge + 1" (1" Type A), 3" Type B to the 3" x 3" chamfer, front part bottom = bottom of the beams, 1'-0" x 1'-0" fillet; stem
// ledge = footing top 920.75 + 1'-7 1/4" S / 1'-4 3/4" N (p.11 note 3), seat at the riser = ledge + 1'-0", seat front 923.27 / 923.06, front face
// 1'-6" ahead; infill wall only WL +17'-7" .. +36'-0" (top 927.73 / 927.80); mask wall beside the footing W end, 917.75 .. 930.61 / 930.67.
{
    var model = Doc.Model(BrName);
    var cdm = model.Prestressed!.ConcreteDiaphragms!;
    List<double> Hits(IEnumerable<SBM.Core.Solids.Mesh> meshes, Vec2 q)
    {
        var zs = new List<double>();
        foreach (var m in meshes)
            for (int t = 0; t + 2 < m.Indices.Count; t += 3)
            {
                var a = m.Positions[m.Indices[t]]; var bq = m.Positions[m.Indices[t + 1]]; var c = m.Positions[m.Indices[t + 2]];
                double d = (bq.Y - c.Y) * (a.X - c.X) + (c.X - bq.X) * (a.Y - c.Y);
                if (Math.Abs(d) < 1e-12) continue;
                double l1 = ((bq.Y - c.Y) * (q.X - c.X) + (c.X - bq.X) * (q.Y - c.Y)) / d, l2 = ((c.Y - a.Y) * (q.X - c.X) + (a.X - c.X) * (q.Y - c.Y)) / d, l3 = 1 - l1 - l2;
                if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
                zs.Add(l1 * a.Z + l2 * bq.Z + l3 * c.Z);
            }
        return zs.OrderBy(z => z).ToList();
    }
    double FtIn2(double f, double i = 0) => f + i / 12.0;
    var seatRows2 = Csv("seat-elevations.csv");
    foreach (int j in new[] { 0, 1 })
    {
        string side = j == 0 ? "S" : "N";
        var sup = b.Supports[j];
        var unit = sub.Units.First(u => u.Name == sup.Name);
        var dia = cdm.Diaphragms.First(d => d.SupportIndex == j);
        double ledge = 920.75 + (j == 0 ? 19.25 : 16.75) / 12, seatFront = j == 0 ? 923.27 : 923.06, Lb = (j == 0 ? 3.25 : 6.5) / 12;
        double infillTop = j == 0 ? 927.73 : 927.80, maskTop = j == 0 ? 930.61 : 930.67;
        Vec2 Pt(double off, double xIn) => al.PointAt(sup.Station + (j == 0 ? 1 : -1) * In(xIn), Ft(off));
        double DeckTop(double off, double xIn) { var q = Pt(off, xIn); var so = al.Inverse(q); return b.Deck.FinishedElevation(al, so.Station, so.Offset) / ft; }
        double PlanSeat(double off) => double.Parse(seatRows2.First(r => r[0] == side && Math.Abs(double.Parse(r[3]) - off) < 1e-6)[5]);
        double BeamBot(double off) => PlanSeat(off) + Lb;
        double? Bot(IEnumerable<SBM.Core.Solids.Mesh> m, double off, double xIn) { var h = Hits(m, Pt(off, xIn)); return h.Count > 0 ? h[0] / ft : null; }
        double? Top(IEnumerable<SBM.Core.Solids.Mesh> m, double off, double xIn) { var h = Hits(m, Pt(off, xIn)); return h.Count > 0 ? h[^1] / ft : null; }
        var dm = new[] { dia.Mesh };
        var poly3 = cdm.Accessories.Where(a => a.Support == sup.Name && a.Material.StartsWith("3\"")).Select(a => a.Mesh).ToList();
        var poly1 = cdm.Accessories.Where(a => a.Support == sup.Name && a.Material.Contains("Type A")).Select(a => a.Mesh).ToList();
        var stem = unit.Solids.Where(x => x.Name.EndsWith(" stem")).Select(x => x.Mesh).ToList();
        var foot = unit.Solids.Where(x => x.Kind == SBM.Core.Substructure.SubElementKind.Footing).Select(x => x.Mesh).ToList();
        var infill = unit.Solids.Where(x => x.Name.Contains("infill")).Select(x => x.Mesh).ToList();
        var mask = unit.Solids.Where(x => x.Kind == SBM.Core.Substructure.SubElementKind.Maskwall).Select(x => x.Mesh).ToList();
        string G = "Section A-A (p21)";
        double o = -18;   // A-A: between G1 (-21) and G2 (-15); beam bottom line at mid-bay = mean of the two beam bottoms (plan seat + L)
        double bbMid = (BeamBot(-21) + BeamBot(-15)) / 2;
        Row(G, $"{side} diaphragm back face x (ft)", -2.50, dia.Parts.Min(p => p.XMin) / ft, "1'-3\" + 3\" + 1'-0\" behind CL brg");
        Row(G, $"{side} diaphragm fillet end x (ft)", 1.50, dia.Parts.Max(p => p.XMax) / ft, "front face 6\" + 1'-0\" fillet");
        Row(G, $"{side} back part bottom at x -29\" (ledge + 1\")", ledge + 1.0 / 12, Bot(dm, o, -29), "1\" Type A polystyrene on the ledge");
        Row(G, $"{side} paving bracket top at x -29\" (deck top - 1'-4\")", DeckTop(o, -29) - FtIn2(1, 4), Top(dm, o, -29), "8\" x 1'-4\" notch");
        Row(G, $"{side} top at x -20\" (deck soffit)", DeckTop(o, -20) - 0.75, Top(dm, o, -20), "slab 9\" stays in the deck");
        Row(G, $"{side} chamfer at x -13 1/2\" (beam bottom - 1 1/2\")", bbMid - 1.5 / 12, Bot(dm, o, -13.5), "3\" x 3\" chamfer (p11 Detail A)");
        Row(G, $"{side} front part bottom at x -6\" (beam bottom line)", bbMid, Bot(dm, o, -6), "A-A: bottom = bottom of beams");
        Row(G, $"{side} front part bottom at x +5 1/2\" (beam bottom line)", bbMid, Bot(dm, o, 5.5), "front face 6\" ahead of CL brg");
        Row(G, $"{side} fillet bottom at x +6 1/2\" (soffit - 11 1/2\")", DeckTop(o, 6.5) - 0.75 - 11.5 / 12, Bot(dm, o, 6.5), "1'-0\" x 1'-0\" fillet");
        Row(G, $"{side} fillet bottom at x +17\" (soffit - 1\")", DeckTop(o, 17) - 0.75 - 1.0 / 12, Bot(dm, o, 17), "1'-0\" x 1'-0\" fillet");
        Row(G, $"{side} B-B at G2: bottom at x -10\" (beam bottom)", BeamBot(-15), Bot(dm, -15, -10), "B-B: beam end 7 1/2\" behind CL brg inside the diaphragm");
        Row(G, $"{side} 3\" polystyrene bottom at x -13 1/2\" (ledge)", ledge, Bot(poly3, o, -13.5), "p11 Detail A");
        Row(G, $"{side} 3\" polystyrene top at x -13 1/2\" (chamfer start)", bbMid - 3.0 / 12, Top(poly3, o, -13.5), "p11 Detail A");
        Row(G, $"{side} 1\" polystyrene at x -25\" (top = ledge + 1\")", ledge + 1.0 / 12, Top(poly1, o, -25), "p11 Detail A");
        Row(G, $"{side} stem ledge at x -20\"", ledge, Top(stem, o, -20), "p11 note 3");
        Row(G, $"{side} stem seat at x -11 7/8\" (riser, ledge + 1'-0\")", ledge + 1 - (1.0 / 8) / 30 * (ledge + 1 - seatFront), Top(stem, o, -11.875), "p11 A-A 1'-0\"");
        Row(G, $"{side} stem seat at x +17 7/8\" (front face)", seatFront + (1.0 / 8) / 30 * (ledge + 1 - seatFront), Top(stem, o, 17.875), "p5 / p10 seat front, 1\" slope");
        Row(G, $"{side} footing top in front of the stem x +2'-0\"", 920.75, Top(foot, o, 24), "p11 A-A: 1'-3\" toe at the footing top (was built up to the seat)");
        Row(G, $"{side} footing top behind the stem x -2'-9\"", 920.75, Top(foot, o, -33), "p11 A-A: 9\" back toe");
        Row(G, $"{side} no infill wall under the superstructure (o +17.5)", 0, infill.Sum(m => Hits(new[] { m }, Pt(17.5, -20)).Count), "p11 Detail C: infill W end +17'-7\"", 0, "F0");
        Row(G, $"{side} infill wall top at o +25 (E 18'-5\")", infillTop, Top(infill, 25, -20), "p5 / p10");
        Row(G, $"{side} infill wall bottom at o +25 (on the ledge)", ledge, Bot(infill, 25, -20), "p11 B-B");
        Row(G, $"{side} mask wall bottom at o -24.3 (beside the footing)", 917.75, Bot(mask, -24.3, 0), "p11 C-C");
        Row(G, $"{side} mask wall top at o -24.3", maskTop, Top(mask, -24.3, 0), "p11 C-C");
        Row(G, $"{side} no mask wall inside the footing end (o -23.5)", 0, mask.Sum(m => Hits(new[] { m }, Pt(-23.5, 0)).Count), "inner face on the footing W end line -23'-7\"", 0, "F0");
        // bearing between the pedestal top and the beam bottom at G2 (F1 L 3 1/4" S, E1 L 6 1/2" N)
        var pedG2 = unit.Pedestals.First(p => p.Girder == 1);
        var bmG2 = ps.Beams.First(x => x.Girder == 1);
        var brg = j == 0 ? bmG2.BearingStart : bmG2.BearingEnd;
        Row(G, $"{side} G2 beam bottom - pedestal top (in)", Lb * 12, (brg.Z - bmG2.Shape.Depth - pedG2.SeatZ) / In(1), j == 0 ? "F1 L 3 1/4\" (p31)" : "E1 L 6 1/2\" (p31)", 1.0 / 16, "F4");
        // concrete quantities (p4 / p9 SUMMARY OF QUANTITIES, structural concrete 3B52) - footing, stem, infill, mask wall, pedestals
        double cy = unit.Solids.Where(x => x.IsConcrete && x.Kind != SBM.Core.Substructure.SubElementKind.Pile).Sum(x => Math.Abs(x.Mesh.Volume())) / 0.764554857984;
        Row("Concrete (CY)", $"{side} abutment structural concrete", j == 0 ? 71 : 69, cy, $"{(j == 0 ? "p4" : "p9")} 3B52 (nearest CY); footing + stem + infill + mask + pedestals", 0.5, "F1");
        Row("Concrete (CY)", $"{side} end diaphragm (superstructure, no separate plan item)", null, dia.Volume / 0.764554857984, "back part + front part", 0, "F2");
    }
    double tot = sub.Units.Sum(u => u.Solids.Where(x => x.IsConcrete && x.Kind != SBM.Core.Substructure.SubElementKind.Pile).Sum(x => Math.Abs(x.Mesh.Volume()))) / 0.764554857984;
    Row("Concrete (CY)", "bridge structural concrete (abutments)", 140, tot, "p2 SOQ 140 CY = 71 + 69", 1.0, "F1");
}

// ---------------------------------------------------------------- reinforcement vs the plan bills (bar-lists.csv: p8, p14, p21, p24) and cover (report only)
// Every bill line: model count and weight (count x listed length x unit weight) vs the printed count / weight; totals by coating vs the summaries of
// quantities (p4 S abut. 7200 epoxy / 480 Cr-epoxy lb, p9 N 6950 / 390, p18 superstructure 5050 / 19100 / 250, p2 bridge 19200 / 19970 / 250).
// Cover: every placed bar sampled (vertices, segment midpoints, every 0.25 m); clear cover = distance to the nearest EXTERIOR face of the host concrete
// (substructure unit solids incl. piles / deck + haunch + barriers + diaphragms + beams; faces between two host solids are interfaces) - db/2; points
// outside every host solid are counted. Report only: nothing is moved.
{
    var model = Doc.Model(BrName);
    var cm = model.Concrete!;
    var lines = new List<string[]>();
    foreach (var ln in File.ReadAllLines(Path.Combine(dir, "bar-lists.csv")).Skip(1))
    {
        if (ln.Trim().Length == 0) continue;
        var cc = new List<string>(); var sbq = new System.Text.StringBuilder(); bool qq = false;
        foreach (char ch in ln) { if (ch == '"') qq = !qq; else if (ch == ',' && !qq) { cc.Add(sbq.ToString()); sbq.Clear(); } else sbq.Append(ch); }
        cc.Add(sbq.ToString());
        lines.Add(cc.ToArray());
    }
    var marks = new List<(string unit, SBM.Core.Concrete.BarMark m)>();
    marks.AddRange(sub.Marks.Select(m => (m.Unit, m)));
    marks.AddRange(cm.Marks.Select(m => ("", m)));
    const double LbKg = 2.2046226218;
    var byCoat = new Dictionary<string, double>();
    foreach (var r in lines)
    {
        string unit = r[0] == "South abutment" ? "South Abutment" : r[0] == "North abutment" ? "North Abutment" : "";
        var hit = marks.FirstOrDefault(x => x.m.Mark == r[1] && (x.unit == unit || unit == "" && x.unit == ""));
        double lenFt = double.Parse(r[6], System.Globalization.CultureInfo.InvariantCulture), lbPrinted = double.Parse(r[8], System.Globalization.CultureInfo.InvariantCulture);
        int cnt = int.Parse(r[4]);
        string src = r[9];
        bool sched = hit.m != null && hit.m.Location.Contains("schedule only");
        Row("Rebar bill", $"{r[1]} count ({r[7]})", cnt, hit.m?.Count, src + (sched ? " - schedule only (not placed)" : hit.m == null ? " - NOT IN THE MODEL" : ""), 0, "F0");
        double? lb = hit.m == null ? null : hit.m.Count * Ft(lenFt) * hit.m.Size.MassPerLength * LbKg;
        Row("Rebar bill", $"{r[1]} weight (lb)", lbPrinted, lb, "count x listed length x unit weight", 0.5, "F1");
        string coat = r[3].StartsWith("chromium") ? "Cr-epoxy" : r[3].StartsWith("stainless") ? "stainless" : "epoxy";
        string key = (unit == "" ? "super" : unit) + "|" + coat;
        byCoat[key] = byCoat.GetValueOrDefault(key) + (lb ?? 0);
    }
    foreach (var (key, planLb) in new[] { ("South Abutment|epoxy", 7200.0), ("South Abutment|Cr-epoxy", 480), ("North Abutment|epoxy", 6950), ("North Abutment|Cr-epoxy", 390),
        ("super|epoxy", 5050), ("super|Cr-epoxy", 19100), ("super|stainless", 250) })
        Row("Rebar totals (lb)", key.Replace("|", " "), planLb, byCoat.GetValueOrDefault(key), "summaries of quantities p4 / p9 / p18 (printed to the nearest 10 / 50 lb)", planLb >= 1000 ? 50 : 10, "F0");
    Row("Rebar totals (lb)", "bridge epoxy", 19200, byCoat.Where(k => k.Key.EndsWith("|epoxy")).Sum(k => k.Value), "p2 SOQ", 50, "F0");
    Row("Rebar totals (lb)", "bridge Cr-epoxy", 19970, byCoat.Where(k => k.Key.EndsWith("|Cr-epoxy")).Sum(k => k.Value), "p2 SOQ", 50, "F0");
    Row("Rebar totals (lb)", "bridge stainless", 250, byCoat.Where(k => k.Key.EndsWith("|stainless")).Sum(k => k.Value), "p2 SOQ", 10, "F0");

    // ---- cover: host meshes, exterior faces, sampled bars
    var cd = model.Prestressed!.ConcreteDiaphragms!;
    var superHosts = new List<SBM.Core.Solids.Mesh>();
    if (cm.Deck != null) superHosts.Add(cm.Deck);
    if (cm.Haunches != null) superHosts.Add(cm.Haunches);
    superHosts.AddRange(cm.Barriers.Select(b => b.Mesh));
    superHosts.AddRange(cd.Diaphragms.Select(d => d.Mesh));
    superHosts.AddRange(ps.Beams.Select(b => b.Mesh));
    var hostSets = new List<(string unit, List<SBM.Core.Solids.Mesh> meshes)> { ("", superHosts) };
    foreach (var u in sub.Units) hostSets.Add((u.Name, u.Solids.Select(s => s.Mesh).ToList()));
    var pbars = b.PlanBars!;
    double Need(string unit, string mark)
    {
        var st = pbars.Sets.FirstOrDefault(s => s.Mark == mark);
        if (st != null && st.Cover > 0) return st.Cover;
        if (mark is "S401M" or "S402M" or "S404M") return In(1);    // deck bottom 1" clr (p19); the longitudinal lines of both mats share the marks
        if (mark == "S403M") return In(2);                            // deck top 3" clr, ends 2" clr to the deck edges (p19 Detail A)
        return In(2);
    }
    foreach (var (unit, meshes) in hostSets)
    {
        var tri = new List<(Vec3 a, Vec3 b, Vec3 c, int m)>();
        for (int mi = 0; mi < meshes.Count; mi++)
        {
            var m = meshes[mi];
            for (int t = 0; t + 2 < m.Indices.Count; t += 3) tri.Add((m.Positions[m.Indices[t]], m.Positions[m.Indices[t + 1]], m.Positions[m.Indices[t + 2]], mi));
        }
        const double C = 0.3;
        (int, int) K2(double x, double y) => ((int)Math.Floor(x / C), (int)Math.Floor(y / C));
        var col = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < tri.Count; i++)
        {
            var (a, bb, c, _) = tri[i];
            var lo = K2(Math.Min(a.X, Math.Min(bb.X, c.X)), Math.Min(a.Y, Math.Min(bb.Y, c.Y))); var hi = K2(Math.Max(a.X, Math.Max(bb.X, c.X)), Math.Max(a.Y, Math.Max(bb.Y, c.Y)));
            for (int ix = lo.Item1; ix <= hi.Item1; ix++) for (int iy = lo.Item2; iy <= hi.Item2; iy++) { if (!col.TryGetValue((ix, iy), out var l)) col[(ix, iy)] = l = new(); l.Add(i); }
        }
        // inside test: vertical ray parity per mesh (tiny skew of the ray avoids edge hits)
        bool Inside(Vec3 p, int skip = -1)
        {
            if (!col.TryGetValue(K2(p.X, p.Y), out var l)) return false;
            var parity = new Dictionary<int, int>();
            foreach (int i in l)
            {
                var (a, bb, c, mi) = tri[i];
                if (mi == skip) continue;
                double qx = p.X + 1.234e-7, qy = p.Y + 2.345e-7;
                double d = (bb.Y - c.Y) * (a.X - c.X) + (c.X - bb.X) * (a.Y - c.Y);
                if (Math.Abs(d) < 1e-14) continue;
                double l1 = ((bb.Y - c.Y) * (qx - c.X) + (c.X - bb.X) * (qy - c.Y)) / d, l2 = ((c.Y - a.Y) * (qx - c.X) + (a.X - c.X) * (qy - c.Y)) / d, l3 = 1 - l1 - l2;
                if (l1 < 0 || l2 < 0 || l3 < 0) continue;
                if (l1 * a.Z + l2 * bb.Z + l3 * c.Z > p.Z) parity[mi] = parity.GetValueOrDefault(mi) + 1;
            }
            return parity.Values.Any(v => v % 2 == 1);
        }
        const double G = 0.15;
        (int, int, int) K3(Vec3 p) => ((int)Math.Floor(p.X / G), (int)Math.Floor(p.Y / G), (int)Math.Floor(p.Z / G));
        var grid = new Dictionary<(int, int, int), List<int>>();
        for (int i = 0; i < tri.Count; i++)
        {
            var (a, bb, c, _) = tri[i];
            var lo = K3(new Vec3(Math.Min(a.X, Math.Min(bb.X, c.X)), Math.Min(a.Y, Math.Min(bb.Y, c.Y)), Math.Min(a.Z, Math.Min(bb.Z, c.Z))));
            var hi = K3(new Vec3(Math.Max(a.X, Math.Max(bb.X, c.X)), Math.Max(a.Y, Math.Max(bb.Y, c.Y)), Math.Max(a.Z, Math.Max(bb.Z, c.Z))));
            for (int ix = lo.Item1; ix <= hi.Item1; ix++) for (int iy = lo.Item2; iy <= hi.Item2; iy++) for (int iz = lo.Item3; iz <= hi.Item3; iz++)
            { if (!grid.TryGetValue((ix, iy, iz), out var l)) grid[(ix, iy, iz)] = l = new(); l.Add(i); }
        }
        static Vec3 Closest(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
        {
            var ab = b - a; var ac = c - a; var ap = p - a;
            double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            var bp = p - b; double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b;
            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
            var cp = p - c; double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c;
            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
            double va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            double den = 1 / (va + vb + vc);
            return a + ab * (vb * den) + ac * (vc * den);
        }
        // distance to the nearest EXTERIOR surface point: a face point is exterior when one side of it (5 mm off) is outside every host solid
        // (judged at the nearest point itself, so a face partly covered by another solid - stem seat under a pedestal - counts only where it is open)
        double Dist(Vec3 p)
        {
            var k = K3(p);
            var cand = new List<(double d, Vec3 q, Vec3 n)>();
            var seen = new HashSet<int>();
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                if (grid.TryGetValue((k.Item1 + dx, k.Item2 + dy, k.Item3 + dz), out var l))
                    foreach (int i in l)
                    {
                        if (!seen.Add(i)) continue;
                        var (a, bb, c, _) = tri[i];
                        var nn = Vec3.Cross(bb - a, c - a);
                        if (nn.Length < 1e-12) continue;
                        var q = Closest(p, a, bb, c);
                        cand.Add(((q - p).Length, q, nn.Normalized()));
                    }
            foreach (var (d, q, nn) in cand.OrderBy(x => x.d))
                if (!Inside(q + nn * 0.005) || !Inside(q - nn * 0.005)) return d;
            return double.PositiveInfinity;
        }
        string Where(Vec3? q)
        {
            if (q == null) return "-";
            var so = al.Inverse(q.Value.XY);
            return $"sta {so.Station / ft:F2} off {so.Offset / ft:+0.00;-0.00} el {q.Value.Z / ft:F2}";
        }
        var bars = unit == "" ? cm.Bars.Where(x => x.Unit == "").ToList() : sub.Bars.Where(x => x.Unit == unit).ToList();
        foreach (var g in bars.GroupBy(x => x.Mark).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var mk = marks.First(x => x.m.Mark == g.Key && x.unit == unit).m;
            double r = mk.Size.Diameter / 2, minClr = double.PositiveInfinity; int outside = 0, n = 0;
            Vec3? worst = null, worstOut = null;
            foreach (var bar in g)
            {
                var pts = new List<Vec3>();
                for (int i = 0; i < bar.Points.Length; i++)
                {
                    pts.Add(bar.Points[i]);
                    if (i + 1 < bar.Points.Length)
                    {
                        var a = bar.Points[i]; var c = bar.Points[i + 1]; int k = Math.Max(1, (int)Math.Ceiling((c - a).Length / 0.25));
                        for (int q = 1; q < k; q++) pts.Add(a + (c - a) * ((double)q / k));
                    }
                }
                for (int pi = 0; pi < pts.Count; pi++)
                {
                    var p = pts[pi];
                    n++;
                    if (!Inside(p)) { outside++; if (worstOut == null) worstOut = p; continue; }
                    // clear cover: to the bar surface; at the bar's end points the end clearance is measured to the end of the bar
                    bool endPt = pi == 0 || pi == pts.Count - 1;
                    double cl = Dist(p) - (endPt ? 0 : r);
                    if (cl < minClr) { minClr = cl; worst = p; }
                }
            }
            double need = Need(unit, g.Key);
            string where = unit == "" ? "super" : unit.Split(' ')[0];
            // one-sided: PASS when the minimum clear cover is not less than the required cover - 1/16" and no sample point lies outside the concrete
            bool ok = outside == 0 && !double.IsInfinity(minClr) && minClr >= need - In(1.0 / 16);
            if (ok) pass++; else fail++;
            rows.Add(("Rebar cover (in)", $"{where} {g.Key} min clear ({g.Count()} bars; {outside}/{n} points outside)", (need / In(1)).ToString("F3"),
                double.IsInfinity(minClr) ? "-" : (minClr / In(1)).ToString("F3"), double.IsInfinity(minClr) ? "-" : ((minClr - need) / In(1)).ToString("+0.000;-0.000;0.000"),
                ok ? "PASS" : "FAIL", (outside > 0 ? "REPORT: points outside the modeled concrete" : "report only - bars are not moved") + (ok ? "" : $" at {Where(worstOut ?? worst)}")));
        }
    }
}

// ---------------------------------------------------------------- table
int w1 = rows.Max(r => r.item.Length);
Print($"{"Group",-20} {"Item".PadRight(w1)} {"Plan",12} {"Model",12} {"Diff",8}  Status  Note");
foreach (var r in rows)
    Print($"{r.group,-20} {r.item.PadRight(w1)} {r.plan,12} {r.model,12} {r.diff,8}  {r.status,-6}  {r.note}");
Print("");
Print($"SUMMARY: {pass} PASS, {fail} FAIL, {na} N/A (tolerance {Tol} ft; stool 1/16\"; area nearest SF)");
foreach (var g in rows.GroupBy(r => r.group))
    Print($"  {g.Key,-20} {g.Count(r => r.status == "PASS"),3} pass {g.Count(r => r.status == "FAIL"),3} fail {g.Count(r => r.status == "N/A"),3} n/a");
