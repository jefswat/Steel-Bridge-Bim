// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - MAIN RIVER SPANS 18-20: arch-type continuous riveted truss 341' + 682' + 341'
// (11 + 22 + 11 panels @ 31'-0, trusses 87'-0 c-c, suspended floor in span 19 on 4" bridge-rope suspenders) as a generic TrussDefinition
// (SBM.Core.Truss.TrussGenerator; design note docs/plan-data/ptc-delaware-river/TRUSS-DESIGN.md).
// On the PTC model (build-ptc-delaware-river.csx: bridge "PTC Arch truss 18-20" = support lines only on "CL Roadway (PTC)") the script turns that
// bridge into the truss bridge (SupportLinesOnly off, Girders.Count = 0, Truss = this data); without it the script builds its own TEST alignment
// (same tangent + crest profile + crown as the PTC build) and a 4-support bridge:
//   copy "samples\plans\PTC Delaware River Bridge.sbm" "samples\plans\PTC main truss 18-20.sbm"
//   sbm run samples\plans\truss-ptc-arch.csx "samples\plans\PTC main truss 18-20.sbm"
//   sbm run samples\plans\check-truss-ptc.csx "samples\plans\PTC main truss 18-20.sbm"
// or (test model)   sbm new temp\truss-test.sbm   +   sbm run samples\plans\truss-ptc-arch.csx temp\truss-test.sbm
// Data: docs/plan-data/ptc-delaware-river/truss/{elevations,chords,webs,joints}.csv (transcribed from the raster sheets; every row names its page).
// PDF "C:\SBMData\Original Existing Bridge Plans from PTC.pdf", "p." = PDF page (DRB-4 sheet = page - 40). User rules: as-built set only; nothing
// invented: every value the sheets do not give is an UNRESOLVED flag with the page and a plain reason; plan-vs-plan differences are reported.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? "truss-ptc-arch.sbm");
var inv = CultureInfo.InvariantCulture;
var unresolved = new List<string>();
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
void UG(string pages, string item, string why, string then = "") => unresolved.Add(U(pages, item, why, then));

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "truss");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/ptc-delaware-river/truss not found above the current directory.");
List<Dictionary<string, string>> Csv(string name)
{
    var lines = File.ReadAllLines(Path.Combine(dataDir, name)).Where(l => l.Trim() != "").ToList();
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
    var hdr = Split(lines[0]);
    return lines.Skip(1).Select(l => { var f = Split(l); return hdr.Select((h, i) => (h, v: i < f.Count ? f[i].Trim() : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
double D(string s) => double.Parse(s, inv);
// "1 1/8" / "15/16" / "34" / "31 1/2" (inches) -> meters
double Inch(string s)
{
    s = s.Trim();
    double v = 0;
    foreach (var part in s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        v += part.Contains('/') ? D(part.Split('/')[0]) / D(part.Split('/')[1]) : D(part);
    return In(v);
}
double FI(double ft, double inch) => Ft(ft) + In(inch);

// ================================================================ 1. alignment + supports (PTC model, else test)
const string BridgeName = "PTC Arch truss 18-20";
var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == BridgeName);
Alignment al;
bool testModel = br == null;
if (testModel)
{
    Print("No PTC model bridge '" + BridgeName + "' - building the TEST alignment (tangent S 69 54'15\" E, crest VC p.69, crown 1/8\"/ft p.107) and supports.");
    const string AlName = "CL Roadway (PTC truss test)";
    Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
    double heading = -(Math.PI / 2 - (69 + 54 / 60.0 + 15 / 3600.0) * Math.PI / 180);
    double staStart = Sta("670+00.00"), staEnd = Sta("696+00.00");
    al = new Alignment(new List<HorizontalElement> { new HorizontalElement(new Vec2(0, 0), heading, staEnd - staStart, 0, 0) }) { Name = AlName, StartStation = staStart };
    Doc.Project.Alignments.Add(al);
    double crown = 38.25 / 96.0;   // p.107: CL 38'-3 x 1/8" per ft above the gutter (= the printed profile)
    al.Profile = new VerticalProfile { Vpis = { new Vpi(staStart, Ft(163.850 - 0.03 * (68280.38 - 67000.0) + crown)), new Vpi(Sta("682+80.38"), Ft(163.850 + crown), Ft(1364.0)),
        new Vpi(staEnd, Ft(163.850 - 0.03 * (69600.0 - 68280.38) + crown)) } };
    al.CrossSlope = new CrossSlopeTable { PivotOffset = 0, Rows = { new CrossSlopeRow(staStart, -1.0 / 96, -1.0 / 96) } };
    br = new BridgeDefinition { Name = BridgeName, AlignmentName = AlName, Structure = "Delaware River Bridge (PTC / NJTA, 1954-57), main river spans 18-20" };
    br.FieldSplices.Clear();
    br.Deck.Barrier.Left = false; br.Deck.Barrier.Right = false; br.Deck.Reinforcement.Enabled = false;
    foreach (var (n, s) in new[] { ("Pier 17 main brg", "675+98.380"), ("Pier 18", "679+39.380"), ("Pier 19", "686+21.380"), ("Pier 20 main brg", "689+62.380") })
        br.Supports.Add(new SupportDefinition { Name = n, Type = SupportType.Pier, Station = Sta(s), Substructure = new SubstructureDefinition { Enabled = false } });
    Doc.Project.Bridges.RemoveAll(b => b.Name == BridgeName);
    Doc.Project.Bridges.Add(br);
}
else
{
    al = Doc.Project.FindAlignment(br.AlignmentName)!;
    Print($"PTC model found: bridge '{br.Name}' on '{al.Name}', supports {string.Join(", ", br.Supports.Select(s => $"{s.Name} {Doc.Units.FormatStation(s.Station, 3)}"))}.");
}
br.SupportLinesOnly = false;
br.Girders.Count = 0;
br.Girders.Spacings = new();

// ================================================================ 2. panel points (p.69 elevation table, p.70 vertical lengths)
double sta0 = Sta("675+98.380");                                // p.69 / p.1: CL bearing span 18 = V.P.C. 675+98.380 = L0
double Panel = Ft(31);                                          // p.69 / p.71: 11 + 22 + 11 panels @ 31'-0
var elev = Csv("elevations.csv").ToDictionary(r => int.Parse(r["panel"]), r => r);
var chords = Csv("chords.csv");
var webs = Csv("webs.csv").ToDictionary(r => r["member"], r => r);
var t = new TrussDefinition
{
    PartPerPiece = true,   // main span review BW-M1 / user 2026-10-27: every ply / angle / cover plate / gusset layer its own element (sub-element of its member / joint stack)
    Name = "Main truss spans 18-20", Source = "PTC DRB-4 (as-built 1954-57)", DefaultMaterial = "Structural steel (C / S not given)",
    Materials = { ["C"] = "Structural carbon steel (C)", ["S"] = "Structural silicon steel (S)" },   // p.70 note: "Silicon or Carbon Steel as noted by (S) or (C)"
    PlanSteelWeightLb = 5_730_000 + 10_200_000, PlanSteelWeightSource = "p.42 items 3 + 6, main river spans",
};
t.Planes.Add(new TrussPlaneDefinition { Name = "N", Offset = -Ft(43.5), Description = "north truss (left of the alignment), p.72 / p.94 43'-6 CL truss to CL bridge" });
t.Planes.Add(new TrussPlaneDefinition { Name = "S", Offset = Ft(43.5), Description = "south truss (right), p.72 / p.94" });
string P(int n, bool prime) => prime && n != 22 ? $"{n}'" : $"{n}";                      // panel point number, primes = New Jersey half (p.69)
double StaOf(int n, bool prime) => sta0 + Panel * (prime ? 44 - n : n);
double Gutter(int n) => Ft(D(elev[n]["gutter_ft"]));
// CONFLICT p.69: the ELEVATIONS table prints CL lower chord 140.7820 at point 13, but the four members meeting there (L12-L13 37.2427, L13-L14 36.3014,
// U12-L13 71.1126, U13-L14 64.1225, p.69 / p.70) all close on 140.7620 = the wind chord value of the same row: the working point is modeled at 140.7620.
double LowerEl(int n) => n == 13 ? Ft(D(elev[13]["wind_chord_ft"])) : Ft(D(elev[n]["lower_chord_ft"]));
UG("p.69, p.70", "CONFLICT (p.69 table vs p.69 / p.70 member lengths): working point L13", "the ELEVATIONS table prints 140.7820 for the CL of lower chord at 13 while the member lengths L12-L13, L13-L14, U12-L13 and U13-L14 all close on 140.7620 (the wind chord value of that row)",
    "L13 / L13' modeled at El. 140.7620 (the lengths govern; the 0.02 ft difference is reported by check-truss-ptc.csx).");
double VertLen(int n) => Ft(D(webs[$"U{n}-L{n}"]["length_ft"]));
var halves = new[] { false, true };
void Pt(string name, double station, double z, string src, double? offN = null, double? offS = null)
{
    if (offN == null) { t.Points.Add(new TrussPointDefinition(name, station, z, src)); return; }
    t.Points.Add(new TrussPointDefinition(name, station, z, src) { Offset = offN.Value, Planes = new() { "N" } });
    t.Points.Add(new TrussPointDefinition(name, station, z, src) { Offset = offS!.Value, Planes = new() { "S" } });
}
const double FbTopBelowGutterIn = 33.5;                         // p.94: gutter line to back of flange Ls 2'-9 1/2 (suspended floor beam)
const double FbDepthIn = 106.5;                                 // p.72 / p.94: web 106 + angles 1/4 each edge = 8'-10 1/2 b/b Ls
foreach (bool pr in halves)
    for (int n = 0; n <= 22; n++)
    {
        if (pr && n == 22) continue;
        double s = StaOf(n, pr);
        Pt("L" + P(n, pr), s, LowerEl(n), "p.69 ELEVATIONS (CL of lower chord)");
        Pt("U" + P(n, pr), s, LowerEl(n) + VertLen(n), "p.69 lower chord + p.70 vertical U-L length (verticals plumb)");
        if (n >= 14) Pt("W" + P(n, pr), s, Ft(D(elev[n]["wind_chord_ft"])), "p.69 ELEVATIONS (CL of wind chord)");
        // floor beam axis at the truss planes: top of flange Ls = gutter - 2'-9 1/2 (p.94), mid-depth of 8'-10 1/2
        Pt("FB" + P(n, pr), s, Gutter(n) - In(FbTopBelowGutterIn + FbDepthIn / 2), "p.69 gutter - p.94 2'-9 1/2 - half of 8'-10 1/2");
        // CL points (apex of K laterals, sway frames): same name on both planes, offset to the CL
        Pt("UM" + P(n, pr), s, LowerEl(n) + VertLen(n), "CL of the upper strut", Ft(43.5), -Ft(43.5));
        Pt("LM" + P(n, pr), s, LowerEl(n), "CL of the lower strut", Ft(43.5), -Ft(43.5));
        if (n == 22) Pt("WM22", s, Ft(D(elev[22]["wind_chord_ft"])), "CL of the floor lateral system at W22 (p.71 apex of the W21-W22 K)", Ft(43.5), -Ft(43.5));
        // sway frame V (batch 5c, p.71 SWAY BRACING sketch, p.97 cross sections at CL bridge, p.85 'Bracing Connection'): the diagonals run from the floor
        // beam bottom at the truss (top, gusset pair + 4-Ls 6 x 4 x 1/2 on the floor beam / post) DOWN to the lower strut at the CL (apex on the strut top,
        // 2-1/2 Pls + 2-Ls 4 x 4 x 3/4); the earlier model had the V upside down (apex at the floor beam bottom on the CL)
        Pt("SW" + P(n, pr), s, Gutter(n) - In(FbTopBelowGutterIn + FbDepthIn), "floor beam bottom at the truss (sway V top, p.71 / p.85)");
    }

// ================================================================ 3. sections
var secs = new Dictionary<string, TrussSectionDefinition>();
string Mat(string m) => m.Trim().TrimEnd('.');
string WebMemberWidthFlag = U("p.70, p.74, p.88", "out-to-out width of the verticals / diagonals",
    "the member sketches give the gages and the b. to b. of angles (depth) only; the joint sheets give 2'-5 in. to in. of the gussets",
    "Web members built 2'-5 out to out (between the gussets); long angle leg on the web.");
// "n-Pls DxT" items -> plies of ONE side (n / 2 each), in the printed order (outside -> inside, assumed)
List<TrussPly> Plies(IEnumerable<string> items)
{
    var plies = new List<TrussPly>();
    foreach (var it in items)
    {
        var m = System.Text.RegularExpressions.Regex.Match(it.Trim(), @"^(\d+)-Pls?\.?\s*([\d ./]+?)\s*x\s*([\d ./]+)$");
        if (!m.Success) throw new Exception("ply not understood: " + it);
        int n = int.Parse(m.Groups[1].Value) / 2;
        for (int k = 0; k < n; k++) plies.Add(new TrussPly(Inch(m.Groups[2].Value), Inch(m.Groups[3].Value), $"web PL {m.Groups[2].Value.Trim()} x {m.Groups[3].Value.Trim()}"));
    }
    return plies;
}
TrussPlate Cover(string it)
{
    var m = System.Text.RegularExpressions.Regex.Match(it.Trim(), @"^\d+-Pls?\.?\s*(\(([\d ./]+)-([\d ./]+)\)|([\d ./]+?))\s*x\s*([\d ./]+)$");
    if (!m.Success) throw new Exception("cover not understood: " + it);
    bool perf = m.Groups[2].Success && m.Groups[2].Value != "";
    string w = perf ? m.Groups[2].Value : m.Groups[4].Value;
    return new TrussPlate(Inch(w), Inch(m.Groups[5].Value), perf, it.Trim().Replace("Pls", "PL").Replace("Pl", "PL"));
}
string AngleName(string it)
{
    var m = System.Text.RegularExpressions.Regex.Match(it.Trim(), @"^\d+-Ls\s*(\d+)x(\d+)x([\d ./]+)$");
    if (!m.Success) throw new Exception("angle not understood: " + it);
    string th = m.Groups[3].Value.Trim().Replace(' ', '-');
    return $"L{m.Groups[1].Value}x{m.Groups[2].Value}x{th}";
}
TrussSectionDefinition Chord(Dictionary<string, string> r)
{
    var cov = r["cover_plates"].Split(';').Select(Cover).ToList();
    return new TrussSectionDefinition
    {
        Name = "CH " + r["member"], Kind = TrussSectionKind.Box, Label = $"{r["webs"]}; {r["angles"]}; {r["cover_plates"]}", Material = Mat(r["material"]),
        WebDepth = In(34), WebPlies = Plies(r["webs"].Split(';')), WebsOutToOut = FI(2, 5),             // p.69 1'-2 1/2 CL truss to O.S. main webs, p.74 / p.81 2'-5 o. to o.
        Angle = AngleName(r["angles"]), AngleProjection = In(0.25),                                    // p.70 / p.74: 2'-10 1/2 b. to b. Ls on 34 webs
        TopPlates = { cov[0] }, BottomPlates = { cov[1] }, Source = r["note"],
    };
}
// QC I012: gross area of ONE 1954 18 in channel = (p.70 full gross - cover plates) / 2: 42.7 (45.98 / 45.96 - 21.0), 45.8 (54.76 - 28.0), 51.9 (61.86 - 31.5,
// 58.36 - 28.0), 58.0 (65.46 - 31.5). The AISC v16 MC18 polygons (parallel flanges + fillets) are 0.29 sq in larger each (+0.58 per member).
var Channel1954 = new Dictionary<string, double> { ["42.7"] = 12.49, ["45.8"] = 13.38, ["51.9"] = 15.18, ["58.0"] = 16.98 };
TrussSectionDefinition WebMember(string name, Dictionary<string, string> r)
{
    string sec = r["section"];
    var items = sec.Split(';').Select(x => x.Trim()).ToList();
    var ch = System.Text.RegularExpressions.Regex.Match(items[0], @"^2-18\[\s*([\d.]+)$");
    if (ch.Success)
    {
        var cv = Cover(items[1]);
        return new TrussSectionDefinition
        {
            Name = "WM " + name, Kind = TrussSectionKind.LacedChannels, Label = sec, Material = Mat(r["material"]), Channel = "MC18x" + D(ch.Groups[1].Value).ToString("0.###", inv),
            ShapeArea = In(1) * In(1) * Channel1954[ch.Groups[1].Value.Trim()],
            BackToBack = FI(2, 5), TopPlates = { cv }, BottomPlates = { new TrussPlate(cv.Width, cv.Thickness, cv.Perforated, cv.Label) }, Source = r["note"],
            Unresolved = U("p.70, p.74", "1954 18 in channel '18[ " + ch.Groups[1].Value + "' of the channel verticals / diagonals and their width",
                "the sheets give the weight only (taken as AISC v16 MC18x" + ch.Groups[1].Value + ", same nominal weight) and no out-to-out width", "channel backs 2'-5 apart (in. to in. of the gussets), flanges inward under the perforated covers."),
        };
    }
    var plyItems = items.Where(x => x.Contains("Pls") && !x.Contains("(")).ToList();
    var ang = items.First(x => x.Contains("-Ls"));
    var cov2 = Cover(items.First(x => x.Contains("(")));
    double depth = Plies(new[] { plyItems[0] })[0].Depth;
    return new TrussSectionDefinition
    {
        Name = "WM " + name, Kind = TrussSectionKind.Box, Label = sec, Material = Mat(r["material"]), WebDepth = depth, WebPlies = Plies(plyItems),
        WebsOutToOut = FI(2, 5), Angle = AngleName(ang), AngleProjection = In(0.25),                   // p.78 / p.81 / p.88: b. to b. Ls = web + 1/2"
        TopPlates = { cov2 }, BottomPlates = { new TrussPlate(cov2.Width, cov2.Thickness, cov2.Perforated, cov2.Label) }, Source = r["note"], Unresolved = WebMemberWidthFlag,
    };
}
TrussSectionDefinition Add(TrussSectionDefinition s) { t.Sections.Add(s); secs[s.Name] = s; return s; }
foreach (var r in chords) Add(Chord(r));
// QC pass 4 (I041, 2026-10-21): L13-L14 and L14-L15 carry one more 34 x 1 1/8 ply OUTSIDE the 2'-5 line. The p.69 half longitudinal section L13-L17 draws
// it in the layer of the 1 1/8 gussets at L13 / L14 ("1 1/8 Inlaid Gusset Pl." at L14) / L15: the ply runs between the gussets, the gussets lie in its layer
// (2'-5 in to in, p.83 / p.84 / p.86) and the o.s. splice plates 34 x 1 / 34 x 1 1/8 cover the butts. p.84 prints 2'-0 5/8 (L13-L14) and 2'-2 3/4 (L14-L15)
// b. to b. Ls: 2'-0 5/8 + 2 x (1 1/8 + 1 1/8 + 1 1/16) = 2'-2 3/4 + 2 x (1 1/8 + 1 1/8) = 2'-7 1/4 out to out (TrussPly.BetweenGussets on the outer ply).
foreach (var (m, bb) in new[] { ("L13-L14", FI(2, 0.625)), ("L14-L15", FI(2, 2.75)) })
{
    var s = secs["CH " + m];
    s.WebsOutToOut = FI(2, 7.25); s.WebsBackToBack = bb;
    s.WebPlies[0].BetweenGussets = true;
    s.WebPlies[0].Label += " o.s. (between the inlaid gussets)";
    s.Source += "; p.69 half section / p.84 b. to b. Ls: 2'-7 1/4 o. to o., outer ply between the inlaid gussets (QC I041)";
}
foreach (var (k, r) in webs) if (r["section"] != "POST") Add(WebMember(k, r));
// Post U11-L11 (p.69 "Elevation of Post U11-L11", main material; p.79): upper section 2'-6 1/8 o-o webs, 1'-11 3/4 b-b Ls
double PostU11 = Ft(D(elev[11]["lower_chord_ft"]) + D(webs["U11-L11"]["length_ft"]));      // El of U11 (L11 + 96'-0)
double PostFbTop = Ft(D(elev[11]["gutter_ft"])) - In(FbTopBelowGutterIn);                   // top of the floor beam Ls at point 11 (p.94)
Add(new TrussSectionDefinition
{
    Name = "POST U11-L11", Kind = TrussSectionKind.Box, Material = "S", WebDepth = In(34),
    Label = "4-Ls 5x5x7/8; 2-Cov. Pls (23-12) x 1 1/8; 2-Web Pls 18x7/8; 2-Web Pls 34x9/16 o.s., 34x1 1/8, 4-Web Pls 34x3/4 i.s. (upper section)",
    // QC I011: the p.70 table full gross areas are 293.67 (upper part, above the floor beam connection) = all plies but the 34 x 9/16 o.s., and 217.17
    // (lower part) = without the 34 x 1 1/8 too; the elevation dimensions them as partial plies: 34 x 9/16 o.s. from 5'-0 +- above the floor beam to 5'-0 +-
    // below it, 34 x 1 1/8 from U11 to 9'-0 +- below the floor beam (8'-10 1/2 b-b Ls, top of Ls 2'-9 1/2 under the gutter, p.94). Start = from U11 (member start).
    WebPlies = { new TrussPly(In(34), In(9.0 / 16), "web PL 34 x 9/16 o.s.") { Start = PostU11 - (PostFbTop + Ft(5)), Length = Ft(5) + In(FbDepthIn) + Ft(5) },
        new TrussPly(In(34), In(1.125), "web PL 34 x 1 1/8") { Start = 0, Length = PostU11 - (PostFbTop - In(FbDepthIn) - Ft(9)) }, new TrussPly(In(34), In(0.75), "web PL 34 x 3/4"),
        new TrussPly(In(34), In(0.75), "web PL 34 x 3/4 i.s."), new TrussPly(In(18), In(7.0 / 8), "web PL 18 x 7/8") },
    WebsOutToOut = FI(2, 6.125), WebsBackToBack = FI(1, 11.75), Angle = "L5x5x7/8", AngleProjection = In(0.25),
    TopPlates = { new TrussPlate(In(23), In(1.125), true, "Cov. PL (23-12) x 1 1/8") }, BottomPlates = { new TrussPlate(In(23), In(1.125), true, "Cov. PL (23-12) x 1 1/8") },
    Source = "p.69 / p.79",
    Unresolved = U("p.69", "post U11-L11 partial web plates (34 x 9/16 o.s., 34 x 1 1/8)", "the elevation is drawn with breaks and gives the plate ends only as 5'-0 +- / 9'-0 +- from the floor beam; which plate starts at U11 is read from the p.70 table areas (293.67 upper / 217.17 lower part)",
        "34 x 1 1/8 from U11 to 9'-0 below the floor beam, 34 x 9/16 o.s. from 5'-0 above to 5'-0 below it (2'-6 1/8 o-o webs there, 2'-2 3/4 below the 1 1/8 plates)."),
});
// Rigid suspender 14 (p.70 suspenders table, cover plates p.84 = Sheet 44 not read)
Add(new TrussSectionDefinition
{
    Name = "SUSP 14", Kind = TrussSectionKind.Box, Material = "S", WebDepth = In(26), Label = "2-Pls 26x5/8; 4-Ls 8x8x1 1/8; 4-Pls 24x1 1/8 + 2-Pls 24x7/8",
    WebPlies = { new TrussPly(In(26), In(5.0 / 8)) }, WebsOutToOut = FI(2, 5), Angle = "L8x8x1-1/8", AngleProjection = In(0.25),
    TopPlates = { new TrussPlate(In(24), In(1.125)), new TrussPlate(In(24), In(1.125)), new TrussPlate(In(24), In(7.0 / 8)) },
    BottomPlates = { new TrussPlate(In(24), In(1.125)), new TrussPlate(In(24), In(1.125)), new TrussPlate(In(24), In(7.0 / 8)) }, Source = "p.70",
    Unresolved = U("p.70, p.84", "rigid suspender 14 cover plate arrangement and width", "p.70 refers the cover plates to Sheet 44 (p.84, not read) and gives no width",
        "2'-5 o-o webs, 2 x 24 x 1 1/8 + 24 x 7/8 on each face."),
});
// Floor beams (p.72 FLOOR BEAM SCHEDULE; p.94 typical suspended beam F6, p.95 beam at L13 F8; QC I027): full-length (F.L.) and partial cover plates.
// The partial plates are listed with their lengths only; p.94 / p.95 draw half a beam with the plates symmetric about the CL bridge (the drawn end of the
// 46'-0 top plate on p.94 scales 24.2 ft from the CL at 34.5 px/ft = 23'-0 within the reading), so all are centred on the CL bridge (flagged).
// Stacking outward: F.L. plate on the angles, then the longer partial plate, then the shorter one.
string fbPartial = U("p.72, p.94, p.95", "position of the partial-length floor beam cover plates along the beam", "the schedule gives the lengths only and the elevations draw half a beam, not dimensioned along it",
    "every partial plate centred on the CL bridge.");
TrussPlate Cp(double w, double t, double lenFt, string label) => new TrussPlate(In(w), In(t), false, label) { Length = lenFt > 0 ? Ft(lenFt) : 0 };
TrussSectionDefinition Fb(string name, string label, TrussPlate[] top, TrussPlate[] bot, string extra = "") => Add(new TrussSectionDefinition
{
    Name = name, Kind = TrussSectionKind.H, WebVertical = true, Material = "S", Label = label, WebDepth = In(106), WebPlies = { new TrussPly(In(106), In(5.0 / 8)) },
    Angle = "L8x8x3/4", AngleProjection = In(0.25), TopPlates = new(top), BottomPlates = new(bot),
    Source = "p.72", Unresolved = fbPartial + extra,
});
Fb("F5", "F5: 1-Pl 106x5/8; 4-Ls 8x8x3/4; 1-Pl 18x1/2 F.L. top; 1-Pl 18x1/2x40'-0 bot.",
    new[] { Cp(18, 0.5, 0, "Cov. PL 18 x 1/2 F.L.") }, new[] { Cp(18, 0.5, 40, "Cov. PL 18 x 1/2 x 40'-0") });
Fb("F6", "F6: 1-Pl 106x5/8; 4-Ls 8x8x3/4; 1-Pl 18x5/8 F.L. top; 2-Pls 18x5/8x46'-0 (top + bot.); 1-Pl 18x5/8x60'-0 bot.",
    new[] { Cp(18, 0.625, 0, "Cov. PL 18 x 5/8 F.L."), Cp(18, 0.625, 46, "Cov. PL 18 x 5/8 x 46'-0") },
    new[] { Cp(18, 0.625, 60, "Cov. PL 18 x 5/8 x 60'-0"), Cp(18, 0.625, 46, "Cov. PL 18 x 5/8 x 46'-0") });
Fb("F8", "F8: 1-Pl 106x5/8; 4-Ls 8x8x3/4 F.L.; 2-Pls 18x5/8 F.L.; 1-Pl 18x5/8x46'-0 top; 1-Pl 18x5/8x76'-0 bot. (2-Ls 9x4x1 F.L., 2-Ls 7x4x7/8 F.L., 2-Ls 8x4x1x76'-0 not built)",
    new[] { Cp(18, 0.625, 0, "Cov. PL 18 x 5/8 F.L."), Cp(18, 0.625, 46, "Cov. PL 18 x 5/8 x 46'-0") },
    new[] { Cp(18, 0.625, 0, "Cov. PL 18 x 5/8 F.L."), Cp(18, 0.625, 76, "Cov. PL 18 x 5/8 x 76'-0") },
    " " + U("p.72, p.95", "F8 extra angles 2-Ls 9x4x1 F.L., 2-Ls 7x4x7/8 F.L. and 2-Ls 8x4x1 x 76'-0 (floor beam at L13)",
        "Section B on p.95 draws them round the upper 3/4 wind chord gusset (1'-1 5/8 above the lower gusset) without stating which leg lies on the web, and the 76'-0 pair is not located along the beam",
        "not built (about 217 lb/ft per beam)."));
Add(new TrussSectionDefinition
{
    Name = "F7", Kind = TrussSectionKind.Box, Material = "S", Label = "F7: 2-Pls 106x7/16; 4-Ls 8x4x3/4; 2-Pls 25x9/16 F.L.; 2-Pls 25x9/16x56'-0; 2-Pls 25x9/16x40'-0", WebDepth = In(106), WebPlies = { new TrussPly(In(106), In(7.0 / 16)) },
    // p.93 (Sheet 53) Section A "2'-1 1/2 in to in Webs", Section B "2'-1 1/2 b.b. Ls": webs 25 1/2 in to in = 26 3/8 out to out, angles inside with
    // their backs on the webs (QC I046 2026-10-21; was 25 o. to o. = the cover width)
    WebsOutToOut = In(25.5 + 2 * 7.0 / 16), Angle = "L8x4x3/4", AngleProjection = In(0.25),
    TopPlates = { Cp(25, 0.5625, 0, "Cov. PL 25 x 9/16 F.L."), Cp(25, 0.5625, 56, "Cov. PL 25 x 9/16 x 56'-0"), Cp(25, 0.5625, 40, "Cov. PL 25 x 9/16 x 40'-0") },
    BottomPlates = { Cp(25, 0.5625, 0, "Cov. PL 25 x 9/16 F.L."), Cp(25, 0.5625, 56, "Cov. PL 25 x 9/16 x 56'-0"), Cp(25, 0.5625, 40, "Cov. PL 25 x 9/16 x 40'-0") }, Source = "p.72",
    Unresolved = fbPartial + " " + U("p.93", "two-web floor beam F7 at W22: end reinforcing plates (2-7/16 i.s. / o.s., 2-Pls 6 1/2 x 7/16) and web splice 4-Spl. Pls. 20 7/8 x 5/16 + fills",
        "the elevation (Sheet 53) draws them with their callouts, but their lengths along the beam and the splice position are not dimensioned (the beam is drawn with a break)",
        "not built; the member section is as drawn: webs 2'-1 1/2 in to in (Section A) with the angles inside, backs on the webs (Section B '2'-1 1/2 b.b. Ls')."
        + " The internal diaphragms (4-Ls 4 x 4 x 3/8 + 24 x 3/8 web near the end; 2-Ls 5 x 3 1/2 x 7/16 + 23 x 7/16 web at the stringer points) are not modeled: the truss member section has no internal diaphragm input."),
});
// Stringers (p.72 schedule; 1954 WF = AISC v16 W of the same nominal size / weight)
Add(new TrussSectionDefinition { Name = "W21x62", Kind = TrussSectionKind.Rolled, Designation = "W21x62", Material = "C", Label = "21 WF 62 (S9 / S13)", Source = "p.72" });
Add(new TrussSectionDefinition { Name = "W24x84", Kind = TrussSectionKind.Rolled, Designation = "W24x84", Material = "C", Label = "24 WF 84 (S10 / S14)", Source = "p.72" });
Add(new TrussSectionDefinition { Name = "W24x94", Kind = TrussSectionKind.Rolled, Designation = "W24x94", Material = "C", Label = "24 WF 94 (S11 / S12 / S15 / S16)", Source = "p.72" });
// Wind chord + floor laterals 14 BP 73 (= HP14x73; HP shapes are not in the program's AISC extract: AISC v16 HP14x73 dimensions)
Add(new TrussSectionDefinition { Name = "HP14x73", Kind = TrussSectionKind.Rolled, Designation = "HP14x73", Depth = In(13.6), FlangeWidth = In(14.6), WebThickness = In(0.505), FlangeThickness = In(0.505),
    WeightLbPerFt = 73, ShapeArea = In(1) * In(1) * 21.4, Material = "S", Label = "14 BP 73", Source = "p.71",   // ShapeArea: AISC HP14x73 21.4 sq in (the sharp-root polygon is 21.1, QC I032)
    Unresolved = U("p.71", "orientation of the 14 BP 73 wind chord / floor lateral web", "the bracing diagram gives the section only", "web vertical.") });
Add(new TrussSectionDefinition { Name = "L13-W14", Kind = TrussSectionKind.LacedChannels, Channel = "C15x50", BackToBack = In(13.625), Material = "S", Rotated = true,   // BW-S2: 1'-1 5/8 b.b. [ vertical
    TopPlates = { new TrussPlate(In(13), In(0.5), true, "PL (13-6) x 1/2") }, BottomPlates = { new TrussPlate(In(13), In(0.5), true, "PL (13-6) x 1/2") },
    Label = "2-15[ 50.0; 2-Web Pls 12x1/2; 2-Pls (13-6)x1/2; 1'-1 5/8 b.b. [s", Source = "p.71",
    Unresolved = U("p.71", "wind chord panel L13-W14 web plates 2-12 x 1/2", "their position in the section is only sketched", "channels + covers built, the 12 x 1/2 web plates are not.") });
// Bracing (p.71 / p.79): laced / perforated channel members
string bracingDetail = U("p.71, p.97-100", "bracing connections and the exact lateral / sway / portal geometry", "p.71 gives the systems as diagrams and sections; the bracing detail sheets p.97-100 were not read",
    "members run work point to work point (chord CL / strut mid points) with automatic end setbacks.");
// main span review BW-S1 / S3 (2026-10-27): the p.71 TRANSVERSE STRUTS / DIAGONAL / FLOOR LATERALS sketches give "2'-8 1/4 b.b. [" (W21-W22: "1'-1 5/8 b.b. [")
// as the VERTICAL dimension (channels top and bottom on the chord's lateral plates, perforated plates on the sides) -> turned 90 deg (Rotated)
TrussSectionDefinition Laced(string name, string ch, double bb, double cw, double ct, string label, bool turned = false) => Add(new TrussSectionDefinition
{
    Name = name, Kind = TrussSectionKind.LacedChannels, Channel = ch, BackToBack = bb, Material = ch.StartsWith("MC18") ? "S" : "C", Label = label, Source = "p.71", Rotated = turned,
    // QC I032: each channel at its 1954 gross area (p.70 for the 18 in channels, else weight / 3.4 lb/ft per sq in); the AISC v16 polygons are larger
    ShapeArea = In(1) * In(1) * (ch.StartsWith("MC18x") && Channel1954.TryGetValue(ch.Substring(5), out var a18) ? a18 : D(ch.Substring(ch.IndexOf('x') + 1)) / 3.4),
    TopPlates = { new TrussPlate(cw, ct, true) }, BottomPlates = { new TrussPlate(cw, ct, true) }, Unresolved = bracingDetail,
});
Laced("STRUT", "MC18x45.8", FI(2, 8.25), In(29.5), In(0.5), "2-18[ 45.8 (S); 2-Pls (29 1/2-13 1/2) x 1/2; 2'-8 1/4 b.b. [s", true);
Laced("LAT31", "MC13x31.8", FI(2, 8.25), In(29.5), In(0.5), "2-13[ 31.8; 2-Pls (29 1/2-13 1/2) x 1/2; 2'-8 1/4 b.b. [s", true);
Laced("LAT50", "MC13x50", FI(2, 8.25), In(29.5), In(0.5), "2-13[ 50; 2-Pls (29 1/2-13 1/2) x 1/2; 2'-8 1/4 b.b. [s", true);
Laced("SWAY", "C15x33.9", In(13), In(12.5), In(0.375), "2-15[ 33.9; 2-Pls (12 1/2-6) x 3/8; 1'-1\"");
Laced("SWAY11", "MC18x42.7", FI(1, 11.625), In(22), In(0.375), "2-18[ 42.7; 2-Cov. Pls (22-12) x 3/8; 1'-11 5/8 b.b.");
Laced("FLAT21", "C12x25", FI(1, 1.625), In(13), In(0.375), "2-12[ 25; 2-Pls (13-6) x 3/8; 1'-1 5/8 b.b. [s", true);   // p.71 W21-W22 (was 13 b.b., not turned)
Add(new TrussSectionDefinition { Name = "STRUT L0", Kind = TrussSectionKind.Box, Material = "S", WebDepth = In(30), WebPlies = { new TrussPly(In(30), In(0.5)) }, WebsBackToBack = In(18),
    Angle = "L5x5x7/8", AngleProjection = In(0.25), TopPlates = { new TrussPlate(In(17), In(7.0 / 8)) }, BottomPlates = { new TrussPlate(In(17), In(7.0 / 8)) },
    Label = "2-Cov. Pls 17x7/8; 4-Ls 5x5x7/8; 2-Web Pls (30-13 1/2) x 1/2; 1'-6 / 2'-6 1/2 b.b. Ls", Source = "p.71", Unresolved = bracingDetail + " Web manholes 13 1/2 x 24 @ 4'-6 not cut." });
Add(new TrussSectionDefinition { Name = "STRUT L11", Kind = TrussSectionKind.Box, Material = "S", WebDepth = In(40), WebPlies = { new TrussPly(In(40), In(9.0 / 16)) }, WebsBackToBack = In(22.5),
    Angle = "L7x4x9/16", AngleProjection = In(0.25), TopPlates = { new TrussPlate(In(22), In(9.0 / 16)) }, BottomPlates = { new TrussPlate(In(22), In(9.0 / 16)) },
    Label = "2-Cov. Pls 22x9/16; 4-Ls 7x4x9/16; 2-Web Pls (40-16) x 9/16; 1'-10 1/2 / 3'-4 1/2 b.b. Ls", Source = "p.71", Unresolved = bracingDetail + " Web manholes 16 x 24 @ 4'-6 not cut." });
// batch 5e (2026-10-28, review BW-S4): p.71 "Portal Strut at U11" dimensions the built-up box: W.P.s 1'-6 3/8 each side of CL U11-L11 at the bottom, the
// side toward U12 square to CL U11-U12 (1'-5 7/16 above / 1'-4 1/8 below it, "90 deg"), top and bottom parallel to CL U11-U12, the other side plumb;
// PL 34 1/2 x 11/16 (plumb side), PL (33-13 1/2) x 11/16 (square side, M.H. 13 1/2 x 24 @ 4'-6), PL 24 1/2 x 7/16 (top), PL (35 1/2-13 1/2) x 9/16 (bottom),
// 2-Ls 6 x 6 x 1/2 at the square corners, "2-Ls each composed of 2-Pls 6 x 1/2 welded together F.L." at the plumb side. Section frame: x toward U12, y up.
string portalFlag = U("p.71, p.79, p.100", "portal strut at U11: plate edges at the corners, manholes, strut-to-post and knee brace connections",
    "the sketch gives the W.P. outline, plate sizes and corner angles but not how the plates meet at the corners; the manholes and the p.100 end / knee connections are drawn without dimensions",
    "outline = the W.P. quadrilateral (out to out), plumb / square side plates full height, top / bottom between them, 6 x 6 x 1/2 angles inside the four corners; manholes not cut; end and knee connections not built.");
TrussSectionDefinition PortalU11(string name, double sgn)
{
    // inches in the section frame (x toward U12 for sgn = +1)
    double zA = LowerEl(11) + VertLen(11), zB = LowerEl(12) + VertLen(12);
    double th = Math.Atan2(zB - zA, Panel), sn = Math.Sin(th), cs = Math.Cos(th), tn = Math.Tan(th);
    double xc = 18.375 - 16.125 * sn;
    (double x, double y) BR = (xc + 16.125 * sn, xc * tn - 16.125 * cs), TR = (xc - 17.4375 * sn, xc * tn + 17.4375 * cs);
    (double x, double y) BL = (-18.375, BR.y - 36.75 * tn), TL = (-18.375, TR.y + (-18.375 - TR.x) * tn);
    // sides as lines (point, unit direction), inward normals
    (double x, double y) Sub((double x, double y) a, (double x, double y) b) => (a.x - b.x, a.y - b.y);
    (double x, double y) U2((double x, double y) a) { double l = Math.Sqrt(a.x * a.x + a.y * a.y); return (a.x / l, a.y / l); }
    (double x, double y) X((double x, double y) p1, (double x, double y) d1, (double x, double y) p2, (double x, double y) d2)
    { double den = d1.x * d2.y - d1.y * d2.x; double t = ((p2.x - p1.x) * d2.y - (p2.y - p1.y) * d2.x) / den; return (p1.x + d1.x * t, p1.y + d1.y * t); }
    var cen = ((BL.x + BR.x + TR.x + TL.x) / 4, (BL.y + BR.y + TR.y + TL.y) / 4);
    (double x, double y) Off((double x, double y) p, (double x, double y) d, double t)
    { var nrm = (-d.y, d.x); if (nrm.Item1 * (cen.Item1 - p.x) + nrm.Item2 * (cen.Item2 - p.y) < 0) nrm = (d.y, -d.x); return (p.x + nrm.Item1 * t, p.y + nrm.Item2 * t); }
    var dL = U2(Sub(TL, BL)); var dR = U2(Sub(TR, BR)); var dT = U2(Sub(TR, TL)); var dB = U2(Sub(BR, BL));
    double tL = 11.0 / 16, tR = 11.0 / 16, tT = 7.0 / 16, tB = 9.0 / 16, ta = 0.5, leg = 6;
    var iL = Off(BL, dL, tL); var iR = Off(BR, dR, tR); var iT = Off(TL, dT, tT); var iB = Off(BL, dB, tB);
    var pieces = new List<TrussSectionPieceDefinition>();
    void Piece(string label, string kind, params (double x, double y)[] pts) => pieces.Add(new TrussSectionPieceDefinition { Label = label, Kind = kind, Polygon = pts.Select(q => new TrussUV(In(q.x) * sgn, In(q.y))).ToList() });
    Piece("PL 34 1/2 x 11/16", "plate", BL, TL, X(iL, dL, TL, dT), X(iL, dL, BL, dB));
    Piece("PL (33-13 1/2) x 11/16", "plate", BR, X(iR, dR, BR, dB), X(iR, dR, TR, dT), TR);
    Piece("PL 24 1/2 x 7/16", "plate", X(iL, dL, TL, dT), X(iR, dR, TR, dT), X(iR, dR, iT, dT), X(iL, dL, iT, dT));
    Piece("PL (35 1/2-13 1/2) x 9/16", "plate", X(iL, dL, BL, dB), X(iL, dL, iB, dB), X(iR, dR, iB, dB), X(iR, dR, BR, dB));
    // corner angles inside the plates: legs along the two sides from the inner corner
    void Ang(string label, (double x, double y) p1, (double x, double y) d1, (double x, double y) p2, (double x, double y) d2)
    {
        var k = X(p1, d1, p2, d2); var k1 = X(Off(p1, d1, ta), d1, Off(p2, d2, ta), d2);
        var a = U2(Sub(cen, k)); double s1 = d1.x * a.x + d1.y * a.y >= 0 ? 1 : -1, s2 = d2.x * a.x + d2.y * a.y >= 0 ? 1 : -1;
        (double x, double y) A1 = (k.x + d1.x * s1 * leg, k.y + d1.y * s1 * leg), B1 = (k.x + d2.x * s2 * leg, k.y + d2.y * s2 * leg);
        var A1i = X(Off(p1, d1, ta), d1, A1, (d1.y, -d1.x)); var B1i = X(Off(p2, d2, ta), d2, B1, (d2.y, -d2.x));
        Piece(label, "angle", k, A1, A1i, k1, B1i, B1);
    }
    Ang("L 6 x 6 x 1/2", iR, dR, iT, dT); Ang("L 6 x 6 x 1/2", iR, dR, iB, dB);
    Ang("L 6 x 6 x 1/2 (2-Pls 6 x 1/2 welded F.L.)", iL, dL, iT, dT); Ang("L 6 x 6 x 1/2 (2-Pls 6 x 1/2 welded F.L.)", iL, dL, iB, dB);
    var sd = Add(new TrussSectionDefinition { Name = name, Kind = TrussSectionKind.Nominal, Width = In(36.75), Depth = In(TL.y - BL.y), Material = "S", Source = "p.71",
        Label = $"portal strut U11 (p.71): PL 34 1/2 x 11/16, PL (33-13 1/2) x 11/16, PL 24 1/2 x 7/16, PL (35 1/2-13 1/2) x 9/16, 4 corner Ls 6 x 6 x 1/2 (plumb side {TL.y - BL.y:0.00} in. vs the 34 1/2 plate)",
        CustomPieces = pieces, Unresolved = portalFlag });
    return sd;
}
PortalU11("PORTAL U11", 1);
PortalU11("PORTAL U11'", -1);

// ================================================================ 4. members
void Mem(string name, string from, string to, string sec, TrussMemberRole role, string panel, double planLen = 0, string plane = "", string system = "", double? sa = null, double? sb = null, string src = "")
    => t.Members.Add(new TrussMemberDefinition { Name = name, From = from, To = to, Section = sec, Role = role, Panel = panel, PlanLength = planLen, Plane = plane, System = system, StartSetback = sa, EndSetback = sb, Source = src });
(int a, int b) Ends(string member) { var m = System.Text.RegularExpressions.Regex.Matches(member, @"\d+"); return (int.Parse(m[0].Value), int.Parse(m[1].Value)); }
string PanelLabel(int a, int b, bool pr) => a == b ? $"Point {P(a, pr)}" : $"Panel {P(Math.Min(a, b), pr)}-{P(Math.Max(a, b), pr)}";
string Mirror(string member) => System.Text.RegularExpressions.Regex.Replace(member, @"([A-Z]+)(\d+)", m => int.Parse(m.Groups[2].Value) == 22 ? m.Value : m.Value + "'");
foreach (bool pr in halves)
{
    foreach (var r in chords)
    {
        var (a, b) = Ends(r["member"]);
        string nm = pr ? Mirror(r["member"]) : r["member"];
        if (pr) { var parts = nm.Split('-'); nm = parts[1] + "-" + parts[0]; }   // primed members run toward the crown as well
        var role = r["member"].StartsWith("U") ? TrussMemberRole.UpperChord : TrussMemberRole.LowerChord;
        var ends = nm.Split('-');
        Mem(nm, ends[0], ends[1], "CH " + r["member"], role, PanelLabel(a, b, pr), Ft(D(r["length_ft"])), src: "p.69 " + r["note"]);
    }
    foreach (var (k, r) in webs)
    {
        if (pr && k == "U22-L22") continue;
        var (a, b) = Ends(k);
        string nm = pr ? Mirror(k) : k;
        var ends = nm.Split('-');
        var role = a == b ? TrussMemberRole.Vertical : TrussMemberRole.Diagonal;
        Mem(nm, ends[0], ends[1], r["section"] == "POST" ? "POST U11-L11" : "WM " + k, role, PanelLabel(a, b, pr), Ft(D(r["length_ft"])), src: "p.70 " + r["note"]);
    }
    // wind chord (span 19) and the rigid suspender 14
    Mem(pr ? "W14'-L13'" : "L13-W14", pr ? "W14'" : "L13", pr ? "L13'" : "W14", "L13-W14", TrussMemberRole.WindChord, pr ? "Panel 13'-14'" : "Panel 13-14");
    for (int n = 14; n < 22; n++)
    {
        string a = "W" + P(n, pr), b = "W" + P(n + 1, pr);
        Mem(pr ? $"{b}-{a}" : $"{a}-{b}", pr ? b : a, pr ? a : b, "HP14x73", TrussMemberRole.WindChord, PanelLabel(n, n + 1, pr));
    }
    Mem("L" + P(14, pr) + "-W" + P(14, pr), "L" + P(14, pr), "W" + P(14, pr), "SUSP 14", TrussMemberRole.Hanger, $"Point {P(14, pr)}");
}

// lower chord ends at L0 / L0' (p.73 ELEVATION): the chord L0-L1 with its end diaphragm (4-Cann. Ls 8x8x3/4, 7/8 web) and the 7/8 gussets run past the vertical
// line through L0 (= the rocker pin) to the pier-side end; a chord cut square at L0 left the rocker / sole plate (19 in.) half outside the steel above it.
// Scaled from the drawing (no dimension): 1'-5 (p.73: CL chord to pin 2'-0 1/2 = 51.1 pt -> 25.0 pt/ft; chord end 35.7 pt before the pin line).
double l0Ext = FI(1, 5);
string l0Flag = U("p.73", "lower chord / gusset end beyond L0 and L0'", "the ELEVATION draws the chord with its end diaphragm and the gussets ending about 1'-5 beyond the vertical line through L0 but prints no dimension (scaled at 25.0 pt/ft from the 2'-0 1/2 chord-to-pin dimension)",
    "chord L0-L2 / L2'-L0' extended 1'-5 past L0 / L0' (scaled); the gusset envelope follows the chord end.");
unresolved.Add(l0Flag);
foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.LowerChord && (m.From == "L0" || m.To == "L0'")))
{
    if (m.From == "L0") m.StartSetback = -l0Ext; else m.EndSetback = -l0Ext;
    string secFlag = t.Sections.FirstOrDefault(s => s.Name == m.Section)?.Unresolved ?? "";
    m.Unresolved = string.Join(" ", new[] { secFlag, l0Flag }.Where(x => x.Length > 0));
}

// floor beams (p.72 marks; p.94 level); framed beams end at the truss members' inside line, suspended beams reach the suspender pin at the truss CL
string FbEnd = U("p.85, p.94", "floor beam end connections", "the framed (p.85) and suspended (p.94) end details were not read in full",
    "framed floor beams (points 0-13) end 1'-2 1/2 from the truss CL (the members' outer face line), suspended ones at the truss CL.");
unresolved.Add(FbEnd);
foreach (bool pr in halves)
    for (int n = 0; n <= 22; n++)
    {
        if (pr && n == 22) continue;
        string sec = n == 0 ? "F5" : n == 13 ? "F8" : n == 22 ? "F7" : "F6";
        double sb = n <= 13 ? FI(1, 2.5) : 0;
        Mem("FB" + P(n, pr), "N:FB" + P(n, pr), "S:FB" + P(n, pr), sec, TrussMemberRole.FloorBeam, "", sa: sb, sb: sb, src: "p.72 / p.94");
    }

// bracing (p.71)
void Strut(string pt, string sec, string sys) => Mem($"{sys[0]}S-{pt}", "N:" + pt, "S:" + pt, sec, TrussMemberRole.Strut, "", system: sys);
void KDiag(string from, string apex, string sec, string sys, TrussMemberRole role)
{
    Mem($"{sys[0]}L-N{from}-{apex}", "N:" + from, "N:" + apex, sec, role, "", system: sys);
    Mem($"{sys[0]}L-S{from}-{apex}", "S:" + from, "S:" + apex, sec, role, "", system: sys);
}
foreach (bool pr in halves)
{
    // upper laterals U11-U22: K from the U(n) corners to the mid strut U(n+1) (apex toward the crown); struts U12-U22
    for (int n = 12; n <= 22; n++) if (!(pr && n == 22)) Strut("U" + P(n, pr), "STRUT", "Upper laterals");
    for (int n = 11; n < 22; n++) KDiag("U" + P(n, pr), "UM" + P(n + 1, pr), "LAT31", "Upper laterals", TrussMemberRole.UpperLateral);
    Mem("PORTAL-U" + P(11, pr), "N:U" + P(11, pr), "S:U" + P(11, pr), pr ? "PORTAL U11'" : "PORTAL U11", TrussMemberRole.Portal, "", system: "Portals");
    // lower laterals: L0-L10 K apex at the lower-numbered strut, L10-L12 diamond about L11, L12-L13 K apex at L13, L15-L22 K apex at L(n+1)
    Strut("L" + P(0, pr), "STRUT L0", "Lower laterals");
    Strut("L" + P(11, pr), "STRUT L11", "Lower laterals");
    foreach (int n in Enumerable.Range(1, 10).Concat(new[] { 12, 13 }).Concat(Enumerable.Range(16, 7))) if (!(pr && n == 22)) Strut("L" + P(n, pr), "STRUT", "Lower laterals");
    for (int n = 0; n < 10; n++) KDiag("L" + P(n + 1, pr), "LM" + P(n, pr), "LAT31", "Lower laterals", TrussMemberRole.LowerLateral);
    KDiag("L" + P(11, pr), "LM" + P(10, pr), "LAT50", "Lower laterals", TrussMemberRole.LowerLateral);
    KDiag("L" + P(11, pr), "LM" + P(12, pr), "LAT50", "Lower laterals", TrussMemberRole.LowerLateral);
    KDiag("L" + P(12, pr), "LM" + P(13, pr), "LAT50", "Lower laterals", TrussMemberRole.LowerLateral);
    for (int n = 15; n < 22; n++) KDiag("L" + P(n, pr), "LM" + P(n + 1, pr), "LAT31", "Lower laterals", TrussMemberRole.LowerLateral);
    Mem("LL-CL" + P(16, pr), "N:LM" + P(16, pr), "N:LM" + P(17, pr), "LAT31", TrussMemberRole.LowerLateral, "", system: "Lower laterals");
    // sway frames: V below the floor beam at points 0-10, 12 (2-15[ 33.9) and 11 (2-18[ 42.7): from the floor beam bottom at each truss down to the
    // lower strut CL (p.71 sketch "At Pt. 0, and 1 to 10 incl., and 12" / "At U11 - L11", p.97 cross sections at CL bridge)
    foreach (int n in Enumerable.Range(0, 13))
    {
        string sec = n == 11 ? "SWAY11" : "SWAY";
        Mem($"SW-N{P(n, pr)}", "N:SW" + P(n, pr), "N:LM" + P(n, pr), sec, TrussMemberRole.Sway, "", system: "Sway frames");
        Mem($"SW-S{P(n, pr)}", "S:SW" + P(n, pr), "S:LM" + P(n, pr), sec, TrussMemberRole.Sway, "", system: "Sway frames");
    }
    // floor lateral system span 19 (wind chords 87'-0 c.c.), p.71 FLOOR LATERAL SYSTEM + p.99 (QC I026): two-panel X (14 BP 73) L13-W15, W15-W17,
    // W17-W19, W19-W21, crossing at the CL of floor beams W14 / W16 / W18 / W20 (p.99 "floor lateral conn. at floor beam W14" / "W16, W18 & W20"; wind
    // chord conns at W15 / W17 / W19); W21-W22 = K of 2-12[ 25 from the W21 wind chord points to the CL at W22 (the tongue beam / shear lock)
    foreach (var (a, b) in new[] { (13, 15), (15, 17), (17, 19), (19, 21) })
    {
        string A = (a == 13 ? "L" : "W") + P(a, pr), B = "W" + P(b, pr);
        Mem($"FL-X{P(a, pr)}-{P(b, pr)}a", "N:" + A, "S:" + B, "HP14x73", TrussMemberRole.LowerLateral, "", system: "Floor laterals");
        Mem($"FL-X{P(a, pr)}-{P(b, pr)}b", "S:" + A, "N:" + B, "HP14x73", TrussMemberRole.LowerLateral, "", system: "Floor laterals");
    }
    Mem($"FL-K{P(21, pr)}-22N", "N:W" + P(21, pr), "N:WM22", "FLAT21", TrussMemberRole.LowerLateral, "", system: "Floor laterals");
    Mem($"FL-K{P(21, pr)}-22S", "S:W" + P(21, pr), "S:WM22", "FLAT21", TrussMemberRole.LowerLateral, "", system: "Floor laterals");
}
UG("p.71, p.93, p.99", "floor lateral tongue beam 16 WF 88 + 2-Cov. Pls. 11 1/2 x 1/2 x 5'-6 (W21 - W22 - W21' on the CL, through the slot in the near-side web of F7) and the shear lock at W22",
    "p.93 Section E draws the tongue beam, its 2-7/16 lateral plates and the slot, but the beam length and its end at the shear lock are not dimensioned, and the 1954 16 WF 88 is not in the AISC v16 extract (its d / bf / tw / tf are not on the sheets)",
    "not built; the 2-12[ 25 K members end at the CL point of W22.");
// batch 5d (2026-10-28, review B06): p.98 BOTTOM CHORD BRACING ADJACENT TO L13 dimensions the L13 1/2 point "13'-1 Meas. along CL of Chord" from L13; the
// p.71 diagram draws the double K between L13 (floor beam F8) and L13 1/2: N:L13 1/2 -> F8 at the quarter point -> strut CL at L13 1/2 -> F8 quarter point -> S:L13 1/2
// (33 WF 130, "28" rivets each end) and the strut at L13 1/2 (2-18[ 45.8 (S) + 2-Pls (31-13 1/2) x 1/2, "46 at L13 1/2"). p.98 prints 21'-9 from the CL truss to
// the quarter point (= 87'-0 / 4).
Laced("STRUT31", "MC18x45.8", FI(2, 8.25), In(31), In(0.5), "2-18[ 45.8 (S); 2-Pls (31-13 1/2) x 1/2 (strut at L13 1/2)", true).Unresolved =
    U("p.71, p.98", "strut at L13 1/2: back to back of the 2-18[ 45.8", "p.71 / p.98 give the channels and the 31 in. side plates but no b.b. dimension (the other struts print 2'-8 1/4 b.b. with 29 1/2 in. plates)",
        "built at 2'-8 1/4 b.b. like the other struts.");
Add(new TrussSectionDefinition { Name = "W33x130", Kind = TrussSectionKind.Rolled, Designation = "W33x130", Material = "C", Label = "33 WF 130 (lower lateral double K L13 - L13 1/2)", Source = "p.98" });
foreach (bool pr in halves)
{
    string q = pr ? "'" : "";
    double s13 = StaOf(13, pr), s14 = StaOf(14, pr), z13 = LowerEl(13), z14 = LowerEl(14);
    double f = Ft(13 + 1.0 / 12) / Math.Sqrt((s14 - s13) * (s14 - s13) + (z14 - z13) * (z14 - z13));
    Pt("L13H" + q, s13 + (s14 - s13) * f, z13 + (z14 - z13) * f, "p.98: 13'-1 measured along the CL of chord from L13");
    Pt("LM13H" + q, s13 + (s14 - s13) * f, z13 + (z14 - z13) * f, "CL of the strut at L13 1/2", Ft(43.5), -Ft(43.5));
    Pt("LQ13" + q, s13, z13, "p.98 / p.71: double K apex on the floor beam F8, 21'-9 from the CL truss", Ft(21.75), -Ft(21.75));
    Strut("L13H" + q, "STRUT31", "Lower laterals");
    Mem($"LL-NL13H{q}-LQ13{q}", "N:L13H" + q, "N:LQ13" + q, "W33x130", TrussMemberRole.LowerLateral, "", system: "Lower laterals", src: "p.98 / p.71");
    Mem($"LL-SL13H{q}-LQ13{q}", "S:L13H" + q, "S:LQ13" + q, "W33x130", TrussMemberRole.LowerLateral, "", system: "Lower laterals", src: "p.98 / p.71");
    Mem($"LL-LM13H{q}-NLQ13{q}", "N:LM13H" + q, "N:LQ13" + q, "W33x130", TrussMemberRole.LowerLateral, "", system: "Lower laterals", src: "p.98 / p.71");
    Mem($"LL-LM13H{q}-SLQ13{q}", "N:LM13H" + q, "S:LQ13" + q, "W33x130", TrussMemberRole.LowerLateral, "", system: "Lower laterals", src: "p.98 / p.71");
}
UG("p.71, p.98, p.95", "lower lateral double K L13 - L13 1/2: apex points on the floor beam F8",
    "p.98 prints 21'-9 from the CL truss to the apex line (scaled to be the F8 quarter point) and the 13'-1 to L13 1/2; the apex elevation and the 33 WF 130 connection to the F8 bottom flange (p.95 Sections B-E) are drawn without dimensions",
    "apexes at the quarter points of the F8 at the L13 lower chord work point elevation; the diagonals stop clear of F8 (bracing corner rule).");
UG("p.71", "inverted portal L13 1/2 - L15 (Sheets 44 / 45)", "the portal is drawn on approach sheets 44 / 45 which belong to another structure",
    "not built.");
UG("p.71", "sway bracing above the floor beams and the portal knee bracing", "only the V frames below the floor beams are tabulated on p.71", "V frames 0-12 built; nothing above the floor beams except the U11 portal strut envelope.");

// ================================================================ 5. floor: stringers on the floor beams + deck slab
t.Floor = new TrussFloorDefinition
{
    StringersOnTop = true, StringerTopBelowGrade = In(7.75), Source = "p.72 / p.94",
    Unresolved = U("p.94, p.72", "stringer bearings (Section D), diaphragms 15[ 33.9 and the stringer splices", "the bearing heights per line are not decoded, the diaphragms are only located (midway between floor beams) and the p.72 splice details (24 WF 84 & 94 / 21 WF 62) are not built yet",
        "stringers on the floor beams as the p.72 continuous members (2-span S13-S16 panels 0-2 / 2'-0', 4-span S9-S12 between the pavement breaks 2-6-10-14-18-22), top = finished grade - 7 3/4 slab, one straight solid per bay (kinked at the floor beams); bearings / splices / diaphragms not built."),
};
// main span review BW-M2 (2026-10-27): p.72 stringer diagram - pavement breaks (= ends of stringers) at panels 2, 6, 10, 14, 18, 22 (symmetric about 22):
// 2-span continuous S13-S16 over panels 0-2 / 2'-0', 4-span continuous S9-S12 in between ("Stringer Splice" at the middle support, splice not built)
var runBays = new List<int> { 2, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 2 };
// types per line from the p.72 panel diagram: 2-span rows S13 / S14 / S15 x3 / S16 (CL); 4-span rows S9 / S10 / S11 / S12 - which inner 24 WF 94 line is S12
// is not legible (labels between the lines), so the three inner lines and the CL carry "S11 / S12"
string[] Types(string sec, bool cl) => runBays.Select(b => b == 2
    ? (sec == "W21x62" ? "S13" : sec == "W24x84" ? "S14" : cl ? "S16" : "S15")
    : (sec == "W21x62" ? "S9" : sec == "W24x84" ? "S10" : "S11 / S12")).ToArray();
foreach (var (off, sec) in new[] { (-39 - 1 / 12.0, "W21x62"), (-33.0, "W24x84"), (-24.75, "W24x94"), (-16.5, "W24x94"), (-8.25, "W24x94"), (0.0, "W24x94"),
    (8.25, "W24x94"), (16.5, "W24x94"), (24.75, "W24x94"), (33.0, "W24x84"), (39 + 1 / 12.0, "W21x62") })
    t.Floor.Stringers.Add(new TrussStringerLine { Name = off == 0 ? "CL" : (off < 0 ? "N" : "S") + Math.Abs(off).ToString("0.##", inv), Offset = Ft(off), Section = sec,
        ContinuousBays = runBays.ToList(), RunTypes = Types(sec, off == 0).ToList() });
t.Floor.Deck = new TrussDeckDefinition
{
    Thickness = In(7.75), LeftOffset = -Ft(38.25), RightOffset = Ft(38.25), BeginStation = StaOf(0, false), EndStation = StaOf(0, true), Source = "p.94 / p.107",
    Unresolved = U("p.107, p.91-92", "deck slab beyond the curb lines, curbs, parapets and the expansion dams at L0 / W22 / L0'", "the parapet / curb sheets (p.107-108) and the dam sheets (p.91-92) are not modeled yet",
        "7 3/4 slab between the curb lines (76'-6), continuous from L0 to L0'."),
};

// ================================================================ 6. suspenders (4" bridge rope, p.70 / p.94) and shoes (p.73 / p.96)
string suspTop = U("p.94, p.86", "upper pin of the rope suspenders", "p.94 details the socket and the floor beam end only; the suspender support diaphragm at the lower chord is on Sheet 46 (p.86, not read)",
    "upper pin 2'-11 1/4 below the CL of the lower chord (half chord depth 1'-5 1/4 + 1'-6 as at the floor beam).");
string suspCount = U("p.94", "number of ropes per suspender point", "View E-E draws one 4\" rope on the CL truss but does not state the count", "one rope per point and truss.");
foreach (bool pr in halves)
    for (int n = 15; n <= 22; n++)
    {
        if (pr && n == 22) continue;
        foreach (var pl in new[] { "N", "S" })
            t.Suspenders.Add(new SuspenderDefinition
            {
                Name = $"{pl}{P(n, pr)}", Plane = pl, Upper = "L" + P(n, pr), Lower = "W" + P(n, pr), LowerPinAbovePoint = FI(6, 10.25), UpperPinBelowPoint = FI(2, 11.25),
                RopeDiameter = In(4), Ropes = 1, SocketLength = FI(3, 6.25), SocketDiameter = FI(1, 1.75), PinDiameter = In(7.25),
                // p.94 SUSPENDER SOCKET (for top and bottom): open socket 3'-6 1/4 = 7 1/4 beyond the pin centre + 1'-8 jaw + 1'-3 basket, jaw 4" FIN. between
                // the lugs at the pin; the pins run ALONG the bridge (p.86 typical connection to the trusses: pin hole in the transverse 29 x 3/4 web plate;
                // p.94: pin through the floor beam end web / pin plates) - QC I024
                RopesAlongBridge = true, SocketBeyondPin = In(7.25), SocketJawWidth = In(4), SocketJawLength = FI(1, 8),
                Rope = "4\" bridge rope, breaking strength 730 T (p.70)", Source = "p.70 / p.94", Unresolved = suspTop + " " + suspCount,
            });
    }
unresolved.Add(suspTop); unresolved.Add(suspCount);
UG("p.94", "suspended floor beam end at the rope socket (pin plates 2-Pls 17 x 13/16 and 25 1/2 x 3/4, top flange angles / cover plates cut round the socket lugs)",
    "the elevation draws the open socket's lugs straddling the beam end from the pin (1'-6 below the back of the flange angles) up to the basket 2 in. above the flange, but the cut of the flange material and the pin plate outline are not dimensioned",
    "the floor beam keeps its full section to the truss CL; the socket jaw (4 in.) takes the web, the lugs overlap the top flange angles (clash rows, about 2 1/2 in.).");
foreach (var pl in new[] { "N", "S" })
{
    // L0 / L0': expansion rocker shoes (p.73): pin 2'-0 1/2 below the chord CL, 3'-5 pin to the bottom, masonry plate 44 x 5 x 4'-2, bottom El. 107.682
    foreach (var (pt, sup) in new[] { ("L0", "Pier 17 main brg"), ("L0'", "Pier 20 main brg") })
        t.Shoes.Add(new TrussShoeDefinition
        {
            Name = $"{pl}-{pt}", Plane = pl, Point = pt, Support = sup, Type = TrussShoeType.Expansion, PinDiameter = In(3), PinLength = FI(2, 7.5), PinBelowPoint = FI(2, 0.5), Height = FI(3, 5),
            BodyLength = FI(3, 0), BodyTopLength = In(19), BodyTopAbovePin = In(5.75), BodyWidth = In(27.5), MasonryLength = In(44), MasonryWidth = FI(4, 2), MasonryThickness = In(5), SeatElevation = Ft(107.682), Source = "p.73",
            Unresolved = U("p.73", "rocker shoe at " + pt, "the rocker (3'-0 radius), stem / wing / stiffener plates and the hold-down are drawn but the body is not modeled plate by plate", "trapezoidal envelope of the rocker body."),
        });
    // L11 (pier 18) fixed, L11' (pier 19) expansion (p.96): 10" pin 3'-11 below L11 (El. 94.2843), 6'-7 pin to bottom of shoe El. 87.701, base 9'-2 x 8'-8
    foreach (var (pt, sup, typ) in new[] { ("L11", "Pier 18", TrussShoeType.Fixed), ("L11'", "Pier 19", TrussShoeType.Expansion) })
        t.Shoes.Add(new TrussShoeDefinition
        {
            Name = $"{pl}-{pt}", Plane = pl, Point = pt, Support = sup, Type = typ, PinDiameter = In(10), PinLength = FI(4, 4), PinBelowPoint = FI(2, 4) + FI(1, 7), Height = FI(6, 7),
            BodyLength = FI(9, 2), BodyTopLength = FI(4, 1), BodyTopAbovePin = FI(1, 7), BodyWidth = FI(4, 4), MasonryLength = FI(9, 2), MasonryWidth = FI(8, 8), MasonryThickness = In(6), SeatElevation = Ft(87.701), Source = "p.96",
            Unresolved = typ == TrussShoeType.Fixed
                ? U("p.96", "fixed shoe at pier 18", "the cast steel body and forged top are drawn, not modeled piece by piece", "trapezoidal envelope from the 9'-2 base to the 4'-1 top forging, 6\" base.")
                : U("p.96", "expansion shoe at pier 19 (roller nest: 5 segmental rollers 2'-0 dia., link bars, inlay plate 84 x 2 3/4 x 8'-10)", "the rollers are not modeled; the masonry plate is printed 110\" x 6 3/4\" x 9'-2 while the side view shows 8'-8 / 6\"",
                    "same envelope as the fixed shoe, masonry 9'-2 x 8'-8 x 6\" (CONFLICT reported)."),
        });
}

// ================================================================ 7. joints (gussets p.69 / p.74-88; rivets drawn only)
t.JointDefaults = new TrussJointDefaults
{
    AutoGussets = true, Lap = FI(2, 6), Margin = 0, RivetDiameter = In(1.125),
    // QC I047 (p.74 U1 "FIN.", p.76 L6 "Fin."): ONE finished joint line through each kinked panel point, fills / splice fills dimensioned to it -> both chord
    // ends cut on the bisector plane through the point (re-checked at 300 dpi: the U1 line passes the work point within ~3/4 in. and leans like the bisector)
    MitredChordJoints = true,
    // QC I048 (p.74 U1-L1): the vertical channels end about 2 in. below the chord bottom, clear of the 24 x 1/2 cover splice plate (scaled 1 1/2 in. clear of it)
    WebMemberClearOfJointPlates = In(1.5),
    Unresolved = U("p.74-90", "gusset plate outlines", "the joint sheets draw the gussets at 3/4\" = 1'-0 without outline dimensions (only rivet group minimums 2'-6, 5\" half holes)",
        "convex envelope of the member ends + 2'-6 rivet group length (p.88 'Typical for all verticals and diagonals 2'-6 End Distance'); thickness from the joint sheet where read, else 3/4\" flagged."),
};
foreach (var r in Csv("joints.csv"))
    foreach (bool pr in halves)
    {
        var (n, _) = (int.Parse(System.Text.RegularExpressions.Regex.Match(r["point"], @"\d+").Value), 0);
        if (pr && n == 22) continue;
        t.Joints.Add(new TrussJointDefinition { Point = r["point"][0] + P(n, pr), GussetThickness = Inch(r["gusset_in"]), Source = r["page"] + " " + r["note"] });
    }
UG("p.70, p.74-90", "rivets", "the joint sheets draw the rivets and give their sizes (1 1/4 / 1 1/8 H.S., 1\" stitch, 7/8 floor) but print no counts", "rivets are not placed (the program places rivet groups when counts / pitch are given).");
UG("p.69, p.74-90", "chord splice plates, inlay plates, fills, lug angles, stiffeners, diaphragms and stitch rivets", "they are listed per joint without their extents", "not built (the steel weight check shows what is missing).");
UG("p.69, p.70", "perforated cover plates (manholes 12 x 20 / 11 1/2 x 20 at 3'-6 or 4'-0)", "spacing printed as approximate", "covers built solid (weight overstated by the manholes).");
// Ply order (QC I036 / I038, 2026-10-21): no longer a flag - the p.69 half longitudinal section L13-L17 draws the layers (34 plies outside, "18x1 Web & 1in Ls" in
// one layer, "31 1/2 x 5/8 Web" innermost) and p.81 / p.84 dimension "2'-5 o. to o. 1 1/8 webs" / b. to b. Ls; chords.csv lists the plies outside -> inside and the
// generator puts a ply that follows a ply fitting between the angle legs inside the angles (TrussSections.Box).
UG("p.73", "hold-down rollers and shear lock at piers 17 / 20", "detailed on p.73 but not part of this phase", "not built.");
// batch 5a (2026-10-28): the laced lateral / strut members lie in the plane of the lateral system (section up = the normal of the chord panel plane), so the
// 2-1/2 Pls. lie flat on them (p.97 / p.98 draw the plates on the member faces); the box struts L0 / L11 keep their plumb webs (end connections p.73 / p.80)
foreach (var m in t.Members.Where(m => m.System is "Upper laterals" or "Lower laterals" && !m.Section.StartsWith("STRUT L"))) m.RollToLateralPlane = true;
t.Unresolved.AddRange(unresolved.Distinct());
br.Truss = t;

// ================================================================ 8. regenerate + save
var rep = Doc.Regenerate();
var tm = Doc.Model(br.Name).Steel?.Truss;
foreach (var e in rep.Errors.Where(e => e.StartsWith(br.Name))) Print("ERROR " + e);
if (tm == null) { Print("ERROR: no truss model generated"); return; }
Print($"Truss: {tm.Members.Count} members, {tm.Gussets} gussets, {tm.Suspenders.Count} suspenders, {tm.Shoes.Count} shoes, {tm.Steel.Parts.Count} parts, steel {tm.Steel.SteelMass * 2.20462262:N0} lb (p.42: 15,930,000)");
Print($"Checks: {tm.Checks.Count(c => c.Ok)} OK / {tm.Checks.Count(c => !c.Ok)} DIFF; warnings {rep.Warnings.Count(w => w.StartsWith(br.Name))}");
foreach (var c in tm.Checks.Where(c => !c.Ok)) Print($"  DIFF {c.Item}: plan {c.Plan}, model {c.Model} {c.Note}");
Doc.Save(outPath);
Print($"Saved {outPath}");
