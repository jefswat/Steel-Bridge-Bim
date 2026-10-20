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
        Pt("SW" + P(n, pr), s, Gutter(n) - In(FbTopBelowGutterIn + FbDepthIn), "CL of the floor beam bottom (sway frame apex)", Ft(43.5), -Ft(43.5));
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
foreach (var (k, r) in webs) if (r["section"] != "POST") Add(WebMember(k, r));
// Post U11-L11 (p.69 "Elevation of Post U11-L11", main material; p.79): upper section 2'-6 1/8 o-o webs, 1'-11 3/4 b-b Ls
Add(new TrussSectionDefinition
{
    Name = "POST U11-L11", Kind = TrussSectionKind.Box, Material = "S", WebDepth = In(34),
    Label = "4-Ls 5x5x7/8; 2-Cov. Pls (23-12) x 1 1/8; 2-Web Pls 18x7/8; 2-Web Pls 34x9/16 o.s., 34x1 1/8, 4-Web Pls 34x3/4 i.s. (upper section)",
    WebPlies = { new TrussPly(In(34), In(9.0 / 16), "web PL 34 x 9/16 o.s."), new TrussPly(In(34), In(1.125), "web PL 34 x 1 1/8"), new TrussPly(In(34), In(0.75), "web PL 34 x 3/4"),
        new TrussPly(In(34), In(0.75), "web PL 34 x 3/4 i.s."), new TrussPly(In(18), In(7.0 / 8), "web PL 18 x 7/8") },
    WebsOutToOut = FI(2, 6.125), WebsBackToBack = FI(1, 11.75), Angle = "L5x5x7/8", AngleProjection = In(0.25),
    TopPlates = { new TrussPlate(In(23), In(1.125), true, "Cov. PL (23-12) x 1 1/8") }, BottomPlates = { new TrussPlate(In(23), In(1.125), true, "Cov. PL (23-12) x 1 1/8") },
    Source = "p.69 / p.79",
    Unresolved = U("p.69", "post U11-L11 section along its length", "the 34 x 1 1/8 and 34 x 9/16 web plates run only over parts of the post (9'-0 / 5'-0 zones about the floor beam) and the lower section is 2'-2 3/4 o-o webs",
        "the upper section (2'-6 1/8 o-o webs, all plies) is built over the full length."),
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
// Floor beams (p.72 schedule; p.94 Section; partial-length cover plates not modeled)
string fbPartial = U("p.72", "partial-length floor beam cover plates (F5 18x1/2x40'-0, F6 2-18x5/8x46'-0 + 18x5/8x60'-0, F7 25x9/16 x40'/56', F8 46'/76' plates and extra angles)",
    "they are listed with lengths but their position along the beam is on the detail sheets p.85 / p.93-96 (not read)", "only the full-length (F.L.) plates are built.");
TrussSectionDefinition Fb(string name, string label, double topT, double botT, double topW = 18) => Add(new TrussSectionDefinition
{
    Name = name, Kind = TrussSectionKind.H, WebVertical = true, Material = "S", Label = label, WebDepth = In(106), WebPlies = { new TrussPly(In(106), In(5.0 / 8)) },
    Angle = "L8x8x3/4", AngleProjection = In(0.25), TopPlates = topT > 0 ? new() { new TrussPlate(In(topW), In(topT)) } : new(), BottomPlates = botT > 0 ? new() { new TrussPlate(In(topW), In(botT)) } : new(),
    Source = "p.72", Unresolved = fbPartial,
});
Fb("F5", "F5: 1-Pl 106x5/8; 4-Ls 8x8x3/4; 1-Pl 18x1/2 F.L. top", 0.5, 0);
Fb("F6", "F6: 1-Pl 106x5/8; 4-Ls 8x8x3/4; 1-Pl 18x5/8 F.L. top", 0.625, 0);
Fb("F8", "F8: 1-Pl 106x5/8; 4-Ls 8x8x3/4; 2-Pls 18x5/8 F.L.", 0.625, 0.625);
Add(new TrussSectionDefinition
{
    Name = "F7", Kind = TrussSectionKind.Box, Material = "S", Label = "F7: 2-Pls 106x7/16; 4-Ls 8x4x3/4; 2-Pls 25x9/16 F.L.", WebDepth = In(106), WebPlies = { new TrussPly(In(106), In(7.0 / 16)) },
    WebsOutToOut = In(25), Angle = "L8x4x3/4", AngleProjection = In(0.25), TopPlates = { new TrussPlate(In(25), In(9.0 / 16)) }, BottomPlates = { new TrussPlate(In(25), In(9.0 / 16)) }, Source = "p.72",
    Unresolved = U("p.72, p.93", "two-web floor beam F7 at W22 (width, angle position)", "p.72 lists the material only; the detail sheet p.93 (Sheet 53) was not read", "webs 25 in. out to out (the cover width), angles inside."),
});
// Stringers (p.72 schedule; 1954 WF = AISC v16 W of the same nominal size / weight)
Add(new TrussSectionDefinition { Name = "W21x62", Kind = TrussSectionKind.Rolled, Designation = "W21x62", Material = "C", Label = "21 WF 62 (S9 / S13)", Source = "p.72" });
Add(new TrussSectionDefinition { Name = "W24x84", Kind = TrussSectionKind.Rolled, Designation = "W24x84", Material = "C", Label = "24 WF 84 (S10 / S14)", Source = "p.72" });
Add(new TrussSectionDefinition { Name = "W24x94", Kind = TrussSectionKind.Rolled, Designation = "W24x94", Material = "C", Label = "24 WF 94 (S11 / S12 / S15 / S16)", Source = "p.72" });
// Wind chord + floor laterals 14 BP 73 (= HP14x73; HP shapes are not in the program's AISC extract: AISC v16 HP14x73 dimensions)
Add(new TrussSectionDefinition { Name = "HP14x73", Kind = TrussSectionKind.Rolled, Designation = "HP14x73", Depth = In(13.6), FlangeWidth = In(14.6), WebThickness = In(0.505), FlangeThickness = In(0.505),
    WeightLbPerFt = 73, Material = "S", Label = "14 BP 73", Source = "p.71",
    Unresolved = U("p.71", "orientation of the 14 BP 73 wind chord / floor lateral web", "the bracing diagram gives the section only", "web vertical.") });
Add(new TrussSectionDefinition { Name = "L13-W14", Kind = TrussSectionKind.LacedChannels, Channel = "C15x50", BackToBack = In(13.625), Material = "S",
    TopPlates = { new TrussPlate(In(13), In(0.5), true, "PL (13-6) x 1/2") }, BottomPlates = { new TrussPlate(In(13), In(0.5), true, "PL (13-6) x 1/2") },
    Label = "2-15[ 50.0; 2-Web Pls 12x1/2; 2-Pls (13-6)x1/2; 1'-1 5/8 b.b. [s", Source = "p.71",
    Unresolved = U("p.71", "wind chord panel L13-W14 web plates 2-12 x 1/2", "their position in the section is only sketched", "channels + covers built, the 12 x 1/2 web plates are not.") });
// Bracing (p.71 / p.79): laced / perforated channel members
string bracingDetail = U("p.71, p.97-100", "bracing connections and the exact lateral / sway / portal geometry", "p.71 gives the systems as diagrams and sections; the bracing detail sheets p.97-100 were not read",
    "members run work point to work point (chord CL / strut mid points) with automatic end setbacks.");
TrussSectionDefinition Laced(string name, string ch, double bb, double cw, double ct, string label) => Add(new TrussSectionDefinition
{
    Name = name, Kind = TrussSectionKind.LacedChannels, Channel = ch, BackToBack = bb, Material = ch.StartsWith("MC18") ? "S" : "C", Label = label, Source = "p.71",
    TopPlates = { new TrussPlate(cw, ct, true) }, BottomPlates = { new TrussPlate(cw, ct, true) }, Unresolved = bracingDetail,
});
Laced("STRUT", "MC18x45.8", FI(2, 8.25), In(29.5), In(0.5), "2-18[ 45.8 (S); 2-Pls (29 1/2-13 1/2) x 1/2; 2'-8 1/4 b.b. [s");
Laced("LAT31", "MC13x31.8", FI(2, 8.25), In(29.5), In(0.5), "2-13[ 31.8; 2-Pls (29 1/2-13 1/2) x 1/2; 2'-8 1/4 b.b. [s");
Laced("LAT50", "MC13x50", FI(2, 8.25), In(29.5), In(0.5), "2-13[ 50; 2-Pls (29 1/2-13 1/2) x 1/2; 2'-8 1/4 b.b. [s");
Laced("SWAY", "C15x33.9", In(13), In(12.5), In(0.375), "2-15[ 33.9; 2-Pls (12 1/2-6) x 3/8; 1'-1\"");
Laced("SWAY11", "MC18x42.7", FI(1, 11.625), In(22), In(0.375), "2-18[ 42.7; 2-Cov. Pls (22-12) x 3/8; 1'-11 5/8 b.b.");
Laced("FLAT21", "C12x25", In(13), In(13), In(0.375), "2-12[ 25; 2-Pls 13 x 3/8");
Add(new TrussSectionDefinition { Name = "STRUT L0", Kind = TrussSectionKind.Box, Material = "S", WebDepth = In(30), WebPlies = { new TrussPly(In(30), In(0.5)) }, WebsBackToBack = In(18),
    Angle = "L5x5x7/8", AngleProjection = In(0.25), TopPlates = { new TrussPlate(In(17), In(7.0 / 8)) }, BottomPlates = { new TrussPlate(In(17), In(7.0 / 8)) },
    Label = "2-Cov. Pls 17x7/8; 4-Ls 5x5x7/8; 2-Web Pls (30-13 1/2) x 1/2; 1'-6 / 2'-6 1/2 b.b. Ls", Source = "p.71", Unresolved = bracingDetail + " Web manholes 13 1/2 x 24 @ 4'-6 not cut." });
Add(new TrussSectionDefinition { Name = "STRUT L11", Kind = TrussSectionKind.Box, Material = "S", WebDepth = In(40), WebPlies = { new TrussPly(In(40), In(9.0 / 16)) }, WebsBackToBack = In(22.5),
    Angle = "L7x4x9/16", AngleProjection = In(0.25), TopPlates = { new TrussPlate(In(22), In(9.0 / 16)) }, BottomPlates = { new TrussPlate(In(22), In(9.0 / 16)) },
    Label = "2-Cov. Pls 22x9/16; 4-Ls 7x4x9/16; 2-Web Pls (40-16) x 9/16; 1'-10 1/2 / 3'-4 1/2 b.b. Ls", Source = "p.71", Unresolved = bracingDetail + " Web manholes 16 x 24 @ 4'-6 not cut." });
Add(new TrussSectionDefinition { Name = "PORTAL U11", Kind = TrussSectionKind.Nominal, Width = In(24.1875), Depth = In(34.5), Material = "S", Label = "portal strut U11 (p.79 Section C)", Source = "p.79",
    Unresolved = U("p.79, p.100", "portal strut and portal bracing at U11", "p.79 Section C lists plates 34 1/2 x 11/16, 24 1/2 x 7/16, (33-13 1/2) x 11/16, (35 1/2-13 1/2) x 9/16 and angles without their arrangement; the portal sheet p.100 was not read",
        "nominal envelope 2'-0 3/16 x 2'-10 1/2 between the U11 points; portal knee bracing not built.") });

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
    Mem("PORTAL-U" + P(11, pr), "N:U" + P(11, pr), "S:U" + P(11, pr), "PORTAL U11", TrussMemberRole.Portal, "", system: "Portals");
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
    // sway frames: V below the floor beam at points 0-10, 12 (2-15[ 33.9) and 11 (2-18[ 42.7)
    foreach (int n in Enumerable.Range(0, 13))
    {
        string sec = n == 11 ? "SWAY11" : "SWAY";
        Mem($"SW-N{P(n, pr)}", "N:L" + P(n, pr), "N:SW" + P(n, pr), sec, TrussMemberRole.Sway, "", system: "Sway frames");
        Mem($"SW-S{P(n, pr)}", "S:L" + P(n, pr), "S:SW" + P(n, pr), sec, TrussMemberRole.Sway, "", system: "Sway frames");
    }
    // floor lateral system span 19 (wind chords 87'-0 c.c.): X in panel pairs W14-W16-W18-W20, W20-W21 and W21-W22 (2-12[ 25)
    foreach (var (a, b, sec) in new[] { (14, 16, "HP14x73"), (16, 18, "HP14x73"), (18, 20, "HP14x73"), (20, 21, "HP14x73"), (21, 22, "FLAT21") })
    {
        if (pr && b == 22) { Mem($"FL-X{P(a, pr)}-22a", "N:W" + P(a, pr), "S:W22", sec, TrussMemberRole.LowerLateral, "", system: "Floor laterals"); Mem($"FL-X{P(a, pr)}-22b", "S:W" + P(a, pr), "N:W22", sec, TrussMemberRole.LowerLateral, "", system: "Floor laterals"); continue; }
        Mem($"FL-X{P(a, pr)}-{P(b, pr)}a", "N:W" + P(a, pr), "S:W" + P(b, pr), sec, TrussMemberRole.LowerLateral, "", system: "Floor laterals");
        Mem($"FL-X{P(a, pr)}-{P(b, pr)}b", "S:W" + P(a, pr), "N:W" + P(b, pr), sec, TrussMemberRole.LowerLateral, "", system: "Floor laterals");
    }
}
UG("p.71", "floor lateral pattern W14-W22", "the diagram summary gives X bracing 'in pairs of panels' W14-W21 and 2-12[ 25 members W21-W22-W21' without the crossing / connection details",
    "X in W14-16, 16-18, 18-20, 20-21 (14 BP 73) and W21-22 (2-12[ 25).");
UG("p.71", "lower lateral double K L13-L13 1/2 and the 33 WF 130 strut at L13 1/2; the inverted portal L13 1/2-L15 (Sheets 44 / 45)", "the L13 1/2 point is not dimensioned on the sheets read", "not built.");
UG("p.71", "sway bracing above the floor beams and the portal knee bracing", "only the V frames below the floor beams are tabulated on p.71", "V frames 0-12 built; nothing above the floor beams except the U11 portal strut envelope.");

// ================================================================ 5. floor: stringers on the floor beams + deck slab
t.Floor = new TrussFloorDefinition
{
    StringersOnTop = true, StringerTopBelowGrade = In(7.75), Source = "p.72 / p.94",
    Unresolved = U("p.94", "stringer bearings (Section D) and diaphragms 15[ 33.9", "the bearing heights per line are not decoded and the diaphragms are only located (midway between floor beams)",
        "stringers continuous over the floor beams, top = finished grade - 7 3/4 slab; one straight piece per bay; bearings / diaphragms not built."),
};
foreach (var (off, sec) in new[] { (-39 - 1 / 12.0, "W21x62"), (-33.0, "W24x84"), (-24.75, "W24x94"), (-16.5, "W24x94"), (-8.25, "W24x94"), (0.0, "W24x94"),
    (8.25, "W24x94"), (16.5, "W24x94"), (24.75, "W24x94"), (33.0, "W24x84"), (39 + 1 / 12.0, "W21x62") })
    t.Floor.Stringers.Add(new TrussStringerLine { Name = off == 0 ? "CL" : (off < 0 ? "N" : "S") + Math.Abs(off).ToString("0.##", inv), Offset = Ft(off), Section = sec });
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
                Rope = "4\" bridge rope, breaking strength 730 T (p.70)", Source = "p.70 / p.94", Unresolved = suspTop + " " + suspCount,
            });
    }
unresolved.Add(suspTop); unresolved.Add(suspCount);
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
UG("p.69, p.74", "order of the web plies of the chords", "the sections list the plies without their order; the half section L13-L17 shows inlays at the joints", "plies stacked from the outside in the printed order, angles against the innermost ply deeper than their leg.");
UG("p.73", "hold-down rollers and shear lock at piers 17 / 20", "detailed on p.73 but not part of this phase", "not built.");
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
