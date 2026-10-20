// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - APPROACH TRUSSES as generic TrussDefinitions (SBM.Core.Truss.TrussGenerator):
//   "PTC 3-span truss 11-13" and its New Jersey twin "PTC 3-span truss 25-27"  (3 x 215'-6, 24 panels @ 26'-11 1/4, p.47-56, joint sheets p.50-53, p.59)
//   "PTC 4-span truss 14-17" and its twin "PTC 4-span truss 21-24"             (4 x 269'-4 1/2, 40 panels @ 26'-11 1/4, p.57-68)
// DECK trusses, Warren with verticals, 27'-6 deep (vertical), trusses 58'-0 c-c (29'-0 each side of the bridge CL, p.55 / p.58 / p.59; NOT the 87'-0 of the main span),
// one continuous +3.00 % gutter grade: chords parallel to it, posts and hangers truly vertical. Floor beams every panel point ON TOP of the top chords (back of the
// floor beam 2'-8 3/4 under the gutter line, p.55), stringers on the floor beams (4-span continuous), 6 3/4 slab (p.107).
// The 25-27 / 21-24 units are the same sheets in the opposite hand (p.1 / p.42 / p.47 / p.57 print both pier numberings): joint 0 = the LOW end = pier 27 / 24
// (stations 706+94.380 / 700+43.880 DEcreasing), the grade rises toward joint 0' (pier 24 / 20) exactly as on the Pennsylvania side.
// On the PTC model (build-ptc-delaware-river.csx: four SupportLinesOnly bridges) the script turns each into the truss bridge (SupportLinesOnly off, Girders.Count = 0,
// Truss = this data), re-runnable (it rebuilds Truss every run):
//   sbm run samples\plans\truss-ptc-approach.csx "samples\plans\PTC Delaware River Bridge.sbm"
//   sbm run samples\plans\check-truss-ptc-approach.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Data: docs/plan-data/ptc-delaware-river/truss-approach/{members,joints}.csv (extracted from the raster sheets by the superstructure agents; every row names its page).
// PDF "C:\SBMData\Original Existing Bridge Plans from PTC.pdf", "p." = PDF page (DRB-4 sheet = page - 40). User rules: as-built set only; nothing invented: every value the
// sheets do not give is an UNRESOLVED flag with the page and a plain reason; plan-vs-plan differences are reported, never adjusted.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? "PTC Delaware River Bridge.sbm");
var inv = CultureInfo.InvariantCulture;
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "truss-approach");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/ptc-delaware-river/truss-approach not found above the current directory.");
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
double Inch(string s)
{
    double v = 0;
    foreach (var part in s.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        v += part.Contains('/') ? D(part.Split('/')[0]) / D(part.Split('/')[1]) : D(part);
    return In(v);
}
double FI(double ft, double inch) => Ft(ft) + In(inch);

var members = Csv("members.csv");
var jointRows = Csv("joints.csv");

// ---------------------------------------------------------------- units (p.1 / p.5 / p.32 / p.42 / p.47 / p.57)
const double PanelFt = 26.9375;                                  // p.47 / p.57: 26'-11 1/4 horizontal (the 26.9496 of the tables is the slope length)
double Panel = Ft(PanelFt);
const double DepthFt = 27.5;                                     // p.47 / p.57 "27'-6 (Meas. Vert.)"
const double GutterToTopChordFt = 3.5;                           // p.47 / p.57 (printed at the 0' end): gutter line down to the top chord work line
const double TrussOffsetFt = 29.0;                               // p.55 / p.58 / p.59: 58'-0 c-c, 29'-0 each side of the bridge CL
const double BracketFt = 10.75;                                  // p.48 / p.58: floor beam cantilever 10'-9 beyond each truss
// pier seat elevations = tops of the piers = bottoms of the shoes (p.5 / p.32 site plan elevations; p.49 / p.52 / p.59 / p.63 / p.67 shoe sheets; p.27 pier 17 / 20 107.682)
double[] seatEl3 = { 55.962, 62.312, 68.339, 75.357 };           // piers 10 / 11 / 12 / 13 (and 27 / 26 / 25 / 24)
double[] seatEl4 = { 75.357, 83.005, 91.525, 99.168, 107.682 };  // piers 13 / 14 / 15 / 16 / 17 (and 24 / 23 / 22 / 21 / 20)
var units = new[]
{
    new UnitDef("PTC 3-span truss 11-13", "A", Sta("658+66.380"), +1, 91.430, 24, 12, new[] { 0, 8, 16, 24 }, new[] { "Pier 10 truss brg", "Pier 11", "Pier 12", "Pier 13 W brg" }, new[] { "E", "F", "E", "E" }, seatEl3, "p.5, p.9", 2856000),
    new UnitDef("PTC 4-span truss 14-17", "B", Sta("665+16.880"), +1, 110.945, 40, 20, new[] { 0, 10, 20, 30, 40 }, new[] { "Pier 13 E brg", "Pier 14", "Pier 15", "Pier 16", "Pier 17 W brg" }, new[] { "E", "E", "F", "E", "E" }, seatEl4, "p.5, p.7, p.16", 5159000),
    new UnitDef("PTC 4-span truss 21-24", "B", Sta("700+43.880"), -1, 110.945, 40, 20, new[] { 0, 10, 20, 30, 40 }, new[] { "Pier 24 W brg", "Pier 23", "Pier 22", "Pier 21", "Pier 20 E brg" }, new[] { "E", "E", "F", "E", "E" }, seatEl4, "p.32, p.34", 5159000),
    new UnitDef("PTC 3-span truss 25-27", "A", Sta("706+94.380"), -1, 91.430, 24, 12, new[] { 0, 8, 16, 24 }, new[] { "Pier 27 truss brg", "Pier 26", "Pier 25", "Pier 24 E brg" }, new[] { "E", "F", "E", "E" }, seatEl3, "p.32, p.36", 2856000),
};

// ---------------------------------------------------------------- shared section builders
var wf14 = SteelCatalog.WideFlanges.Where(w => w.Name.StartsWith("W14x", StringComparison.OrdinalIgnoreCase)).OrderBy(w => w.AiscArea).ToList();
var wf16 = SteelCatalog.WideFlanges.Where(w => w.Name.StartsWith("W16x", StringComparison.OrdinalIgnoreCase)).OrderBy(w => w.AiscArea).ToList();
// 1954 "14 WF n" / "16 WF n" are not all in AISC v16: dimensions interpolated between the AISC shapes bracketing the PRINTED gross area (p.47 / p.57 member tables)
TrussSectionDefinition OldWF(string printed, double areaSqIn, string material, string pages)
{
    var fam = printed.StartsWith("16") ? wf16 : wf14;
    double a = areaSqIn * 0.00064516;
    var lo = fam.LastOrDefault(w => w.AiscArea <= a) ?? fam[0];
    var hi = fam.FirstOrDefault(w => w.AiscArea >= a) ?? fam[^1];
    double t = hi.AiscArea - lo.AiscArea > 1e-9 ? (a - lo.AiscArea) / (hi.AiscArea - lo.AiscArea) : 0;
    double Lerp(double x, double y) => x + (y - x) * t;
    return new TrussSectionDefinition
    {
        Name = printed, Kind = TrussSectionKind.Rolled, Designation = printed, Rotated = true,
        Depth = Lerp(lo.Depth, hi.Depth), FlangeWidth = Lerp(lo.FlangeWidth, hi.FlangeWidth), WebThickness = Lerp(lo.WebThickness, hi.WebThickness), FlangeThickness = Lerp(lo.FlangeThickness, hi.FlangeThickness),
        WeightLbPerFt = areaSqIn * 3.4, Material = material, Label = $"{printed} ({areaSqIn:0.##} sq.in.)", Source = pages,
        Unresolved = U(pages, $"rolled shape {printed} (1954 series) dimensions", $"the sheets print the designation and the gross area {areaSqIn:0.##} sq.in., not d / bf / tw / tf, and the 1954 series differs from AISC v16",
            $"d / bf / tw / tf interpolated by area between AISC {lo.Name} and {hi.Name}, weight 3.4 lb/ft per sq.in.; flanges in planes parallel to the truss plane with the gusset pairs outside them (p.50 / p.60: gusset in-to-in = depth + fills)."),
    };
}

// ---------------------------------------------------------------- build every unit found in the model
var summary = new List<string>();
foreach (var u in units)
{
    var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == u.Bridge);
    if (br == null) { Print($"No bridge '{u.Bridge}' in this model - skipped (run on the PTC model)."); continue; }
    var al = Doc.Project.FindAlignment(br.AlignmentName)!;
    br.SupportLinesOnly = false;
    br.Girders.Count = 0;
    br.Girders.Spacings = new();
    bool four = u.Kind == "B";
    int N = u.N, C = u.C;
    double dirSign = u.Dir;
    var unresolved = new List<string>();
    void UG(string pages, string item, string why, string then = "") => unresolved.Add(U(pages, item, why, then));
    string pg = four ? "p.57" : "p.47";                         // stress sheet
    string pgFloor = four ? "p.58" : "p.48";
    string jointPages = four ? "p.59-67" : "p.49-53";

    string J(int m) => m <= C ? m.ToString() : $"{2 * C - m}'";
    double StaOf(int m) => u.Sta0 + dirSign * m * Panel;
    double Gutter(int m) => Ft(u.Gutter0 + 0.03 * m * PanelFt);  // p.47 / p.57: +3.00 % gutter line profile (printed at the piers: 91.430 / 97.895 / 104.360 / 110.825, 110.945 .. 143.270)
    double LowerEl(int m) => Gutter(m) - Ft(GutterToTopChordFt + DepthFt);
    double UpperEl(int m) => LowerEl(m) + Ft(DepthFt);
    const double FbTopBelowGutterIn = 32.75;                     // p.55: gutter line to back of floor beam angles 2'-8 3/4
    const double FbDepthIn = 75.5;                               // p.55: web 75 + angles = 6'-3 1/2 b. to b. angles
    double FbEl(int m) => Gutter(m) - In(FbTopBelowGutterIn + FbDepthIn / 2);
    double FbBottomEl(int m) => Gutter(m) - In(FbTopBelowGutterIn + FbDepthIn);

    var t = new TrussDefinition
    {
        Name = u.Bridge.Replace("PTC ", "") + " (approach)", Source = "PTC DRB-4 (as-built 1954-57)", DefaultMaterial = "Structural steel (C / S not given)",
        Materials = { ["C"] = "Structural carbon steel (C)", ["S"] = "Structural silicon steel (S)" },   // p.47 / p.57: (C) carbon, (S) silicon
        PlanSteelWeightLb = u.PlanLb, PlanSteelWeightSource = "p.42 items 2 + 5 (carbon + silicon approach truss spans, one unit)",
    };
    t.Planes.Add(new TrussPlaneDefinition { Name = "N", Offset = -Ft(TrussOffsetFt), Description = "north truss (left of the alignment), p.55 / p.58 29'-0 from the CL bridge" });
    t.Planes.Add(new TrussPlaneDefinition { Name = "S", Offset = Ft(TrussOffsetFt), Description = "south truss (right), p.55 / p.58" });

    // ---- points (station along the alignment + elevation; the floor beam / lateral CL points sit at the same station)
    void Pt(string name, double station, double z, string src, double? offN = null, double? offS = null)
    {
        if (offN == null) { t.Points.Add(new TrussPointDefinition(name, station, z, src)); return; }
        t.Points.Add(new TrussPointDefinition(name, station, z, src) { Offset = offN.Value, Planes = new() { "N" } });
        t.Points.Add(new TrussPointDefinition(name, station, z, src) { Offset = offS!.Value, Planes = new() { "S" } });
    }
    for (int m = 0; m <= N; m++)
    {
        string j = J(m); double s = StaOf(m);
        Pt("L" + j, s, LowerEl(m), $"{pg} gutter line profile - 3'-6 - 27'-6 (bottom chord work point)");
        Pt("U" + j, s, UpperEl(m), $"{pg} gutter line profile - 3'-6 (top chord work point, verticals plumb)");
        Pt("FB" + j, s, FbEl(m), "p.55 gutter - 2'-8 3/4 - half of 6'-3 1/2 (floor beam axis at the truss)");
        t.Points.Add(new TrussPointDefinition("FT" + j, s, FbEl(m), "p.48 / p.58 / p.55 floor beam bracket tip, 10'-9 beyond the truss") { Offset = -Ft(BracketFt), Planes = new() { "N" } });
        t.Points.Add(new TrussPointDefinition("FT" + j, s, FbEl(m), "p.48 / p.58 / p.55 floor beam bracket tip, 10'-9 beyond the truss") { Offset = Ft(BracketFt), Planes = new() { "S" } });
        Pt("UM" + j, s, UpperEl(m), "CL of the top lateral K (apex at the floor beam)", Ft(TrussOffsetFt), -Ft(TrussOffsetFt));
        Pt("LM" + j, s, LowerEl(m), "CL of the bottom strut", Ft(TrussOffsetFt), -Ft(TrussOffsetFt));
        Pt("SW" + j, s, FbBottomEl(m), "bottom of the floor beam at the truss (sway V top)");
        Pt("SWM" + j, s, FbBottomEl(m), "bottom of the floor beam at the CL (hanger bracing top)", Ft(TrussOffsetFt), -Ft(TrussOffsetFt));
    }

    // ---- sections
    var secs = new Dictionary<string, TrussSectionDefinition>();
    TrussSectionDefinition Add(TrussSectionDefinition s) { if (!secs.ContainsKey(s.Name)) { t.Sections.Add(s); secs[s.Name] = s; } return secs[s.Name]; }
    string sheetOf = four ? "p.57" : "p.47";
    var rows = members.Where(r => r["unit"] == u.Kind).ToList();
    foreach (var r in rows.OrderBy(r => (r["plates"] != "" ? 1 : 0) + (r["area_sqin"] == "" ? 2 : 0)).Where(r => r["area_sqin"] != ""))      // plain rows first: the printed area of a plated row includes the plates
    {
        string nm = r["section"];
        if (nm.Contains("WF") && !secs.ContainsKey(nm)) Add(OldWF(nm, D(r["area_sqin"]), r["material"], sheetOf));
    }
    // cover plates on 14 WF chords / diagonals (p.57): listed in the section column, not built (see the flag)
    foreach (var r in rows.Where(r => r["plates"] != ""))
        UG("p.57", $"cover plates {r["plates"]} on {r["section"]} (member {r["row"]})", "the member table lists them as part of the section (area includes them) but the joint sheets draw them only between the splices and the sheet gives no length / position",
            "not built (the rolled shape alone; the gross area in the table is larger by the plates).");
    // floor beams (p.55 / p.48 / p.58): web 75 x 7/16, 4 angles 6x6x1/2, cover plates 14 x 1/2 (full length top built; partial plates not)
    string fbPartial = U("p.55, p.48, p.58", "partial-length floor beam cover plates (F.L. + 28'-0 / 40'-0 / 20'-0 bottom plates, F3 / F4 / B1 plate counts 2 / 4 / 1)", "the lengths are listed on p.55 but their positions along the beam were not decoded; the bracket (B1) web is drawn tapering 6'-3 1/2 to 2'-0 1/2",
        "only the full-length top plate 14 x 1/2 is built (the B1 bracket taper 6'-3 1/2 -> 2'-0 1/2 is modeled).");
    foreach (var (nm, lbl) in new[] { ("F3", "F3 (end floor beam): 1-Pl 75x7/16; 4-Ls 6x6x1/2; 2-Pls 14x1/2"), ("F4", "F4 (typical): 1-Pl 75x7/16; 4-Ls 6x6x1/2; 4-Pls 14x1/2"), ("B1", "B1 (bracket): 1-Pl 75x7/16; 4-Ls 6x6x1/2; 1-Pl 14x1/2") })
        Add(new TrussSectionDefinition
        {
            Name = nm, Kind = TrussSectionKind.H, WebVertical = true, Material = "S", Label = lbl, WebDepth = In(75), WebPlies = { new TrussPly(In(75), In(7.0 / 16)) },
            Angle = "L6x6x1/2", AngleProjection = In(0.25), TopPlates = { new TrussPlate(In(14), In(0.5)) }, Source = pgFloor + " / p.55", Unresolved = fbPartial,
        });
    // stringers: S5 21 WF 62, S6-S8 24 WF 76 (S6 printed 24 WF 78 on p.58, same section modulus)
    Add(new TrussSectionDefinition { Name = "W21x62", Kind = TrussSectionKind.Rolled, Designation = "W21x62", Material = "C", Label = "21 WF 62 (S5)", Source = pgFloor });
    Add(new TrussSectionDefinition { Name = "W24x76", Kind = TrussSectionKind.Rolled, Designation = "W24x76", Material = "C", Label = "24 WF 76 (S6 / S7 / S8)", Source = pgFloor });
    // bracing (p.48 / p.56 / p.58): 14 BP 73 laterals / struts / sway, ST 6 WF 29 top laterals, 10 WF 33 hangers, 2-12[25 box struts at the piers
    string bracingDetail = U("p.56, p.48, p.58", "bracing member orientation, gusset outlines (7/16 plates) and connections", "p.56 gives the sections and the 7/16 gusset note, not the member orientation or outlines",
        "members run work point to work point with automatic end setbacks; web of 14 BP 73 vertical.");
    Add(new TrussSectionDefinition { Name = "HP14x73", Kind = TrussSectionKind.Rolled, Designation = "HP14x73", Depth = In(13.6), FlangeWidth = In(14.6), WebThickness = In(0.505), FlangeThickness = In(0.505),
        WeightLbPerFt = 73, Material = "C", Label = "14 BP 73", Source = "p.48 / p.56", Unresolved = U("p.48, p.56", "14 BP 73 shape", "HP shapes are not in the program's AISC extract and the sheet gives the designation only", "AISC v16 HP14x73 dimensions (web vertical).") });
    Add(new TrussSectionDefinition { Name = "WT6x29", Kind = TrussSectionKind.Rolled, Designation = "WT6x29", Material = "C", Label = "ST 6 WF 29", Source = "p.48 / p.56", Unresolved = bracingDetail });
    Add(new TrussSectionDefinition { Name = "W10x33", Kind = TrussSectionKind.Rolled, Designation = "W10x33", Material = "C", Label = "10 WF 33", Source = "p.48 / p.58 / p.56", Unresolved = bracingDetail });
    Add(new TrussSectionDefinition { Name = "STRUTBOX", Kind = TrussSectionKind.LacedChannels, Channel = "C12x25", BackToBack = In(13), Material = "C", Label = "2-12[ 25; 2-Pls (13-6) x 3/8 (box strut at the piers)", Source = "p.48 / p.56 / p.58",
        TopPlates = { new TrussPlate(In(13), In(0.375), true, "PL (13-6) x 3/8") }, BottomPlates = { new TrussPlate(In(13), In(0.375), true, "PL (13-6) x 3/8") },
        Unresolved = U("p.56, p.58", "box strut 2-12[ 25 + 2 Pls (13-6) x 3/8 at the piers", "the sketch draws it 1'-1 5/8 deep with 6 x 12 hand holes but the back-to-back of the channels is not dimensioned", "channels 13 in. back to back with the 13 in. perforated plates, as the main-span flat strut.") });

    // ---- members
    void Mem(string name, string from, string to, string sec, TrussMemberRole role, string panel, double planLen = 0, string plane = "", string system = "", double? sa = null, double? sb = null, string src = "")
        => t.Members.Add(new TrussMemberDefinition { Name = name, From = from, To = to, Section = sec, Role = role, Panel = panel, PlanLength = planLen, Plane = plane, System = system, StartSetback = sa, EndSetback = sb, Source = src });
    var byRow = rows.ToDictionary(r => r["group"] + "|" + r["row"], r => r);
    string Sec(Dictionary<string, string> r) => r["section"];
    // chords: one member per printed row (2 panels, through the odd-numbered point), mirrored about the centre joint
    void Chord(string kind, int a, int b, string rowName)
    {
        var r = byRow[(kind == "U" ? "top" : "bottom") + "|" + rowName];
        var role = kind == "U" ? TrussMemberRole.UpperChord : TrussMemberRole.LowerChord;
        double len = (b - a) * D(r["length_ft"]);
        Mem($"{kind}{J(a)}-{kind}{J(b)}", kind + J(a), kind + J(b), Sec(r), role, $"Panel {J(a)}-{J(b)}", Ft(len), src: $"{r["page"]} member table row {rowName}");
    }
    foreach (var rn in rows.Where(r => r["group"] == "top").Select(r => r["row"]))
    {
        var m = System.Text.RegularExpressions.Regex.Match(rn, @"^U(\d+)-U(\d+)('?)$");
        int a = int.Parse(m.Groups[1].Value);
        if (m.Groups[3].Value == "'") { Chord("U", a, 2 * C - a, rn); continue; }      // centre row U11-U11' / U19-U19' (through the centre joint)
        int b = int.Parse(m.Groups[2].Value);
        Chord("U", a, b, rn);
        Chord("U", 2 * C - b, 2 * C - a, rn);
    }
    foreach (var rn in rows.Where(r => r["group"] == "bottom").Select(r => r["row"]))
    {
        var m = System.Text.RegularExpressions.Regex.Match(rn, @"^L(\d+)-L(\d+)$");
        int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
        Chord("L", a, b, rn);
        Chord("L", 2 * C - b, 2 * C - a, rn);
    }
    // diagonals: panel m -> m+1; m even L(m)-U(m+1) (long, 39.0766), m odd U(m)-L(m+1) (short, 37.9221); sections by the mirrored left-half row
    for (int m = 0; m < N; m++)
    {
        int k = m < C ? m : N - 1 - m;
        string rowName = k % 2 == 0 ? $"L{k}-U{k + 1}" : $"U{k}-L{k + 1}";
        var r = byRow["diag|" + rowName];
        bool even = m % 2 == 0;
        string from = even ? "L" + J(m) : "U" + J(m), to = even ? "U" + J(m + 1) : "L" + J(m + 1);
        Mem($"{from}-{to}", from, to, Sec(r), TrussMemberRole.Diagonal, $"Panel {J(m)}-{J(m + 1)}", Ft(even ? 39.0766 : 37.9221), src: $"{r["page"]} member table row {rowName}");
    }
    // verticals: posts at even points, hangers (14 WF 61, carbon) at odd points
    for (int m = 0; m <= N; m++)
    {
        int k = Math.Min(m, N - m);
        bool post = m % 2 == 0;
        Dictionary<string, string> r;
        if (!post) r = byRow["hanger|HANGERS"];
        else if (!four) r = k == 0 ? byRow["post|U0-L0"] : k == 8 ? byRow["post|U8-L8"] : byRow["post|OTHER INTERIOR POSTS"];
        else r = k == 0 ? byRow["post|U0-L0"] : (new[] { 2, 4, 6, 10, 20 }.Contains(k) ? byRow["post|U2-L2,U4-L4,U6-L6,U10-L10,U20-L20"] : byRow["post|U8-L8,U12-L12,U14-L14,U16-L16,U18-L18"]);
        Mem($"U{J(m)}-L{J(m)}", "L" + J(m), "U" + J(m), Sec(r), post ? TrussMemberRole.Post : TrussMemberRole.Hanger, $"Point {J(m)}", Ft(DepthFt), src: $"{r["page"]} member table ({r["row"]})");
    }

    // bottom chord ends at the unit-end joints L0 / L0' (p.59 ELEVATION, "shoes for both ends of the 3-span and 4-span trusses alike"): the 14 WF chord with its
    // fills and the gussets run past the vertical line through the pin toward the pier CL; a chord cut square at L0 left the sole plate (22 in.) half outside the
    // steel above it. Scaled (no dimension): 10 1/2 in. (p.59: the two pins 4'-0 apart = 97.7 pt -> 24.4 pt/ft; chord ends 21.3 / 22.1 pt from the pins).
    double l0Ext = In(10.5);
    string l0Flag = U("p.59, p.49", "bottom chord / gusset end beyond L0 and L0'", "the ELEVATION draws the chord and the gussets ending about 10 1/2 in. beyond the vertical line through the shoe pin but prints no dimension (scaled from the 4'-0 between the two units' pins)",
        "end chords extended 10 1/2 in. past L0 / L0' (scaled); the gusset envelope follows the chord end.");
    unresolved.Add(l0Flag);
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.LowerChord && (m.From == "L0" || m.To == "L0'")))
    {
        if (m.From == "L0") m.StartSetback = -l0Ext; else m.EndSetback = -l0Ext;
        string secFlag = t.Sections.FirstOrDefault(s => s.Name == m.Section)?.Unresolved ?? "";
        m.Unresolved = string.Join(" ", new[] { secFlag, l0Flag }.Where(x => x.Length > 0));
    }

    // floor beams (F3 at the end joints, F4 elsewhere) between the trusses + B1 brackets (10'-9 cantilevers). The beam passes the truss at the top chord level:
    // ends set back from the truss CL by half the gusset-to-gusset width plus the gusset (p.55 shows the bracket on the outside of the chord).
    string fbEnd = U("p.55", "floor beam end connections at the truss (bracket / top chord connection)", "p.55 draws the connection on the top chord (2 tension plates, bent plate) but without the dimensions needed to build it",
        "floor beam between the trusses and the brackets stop 9 1/2 in. from the truss CL (half of a typical 17 in. gusset-to-gusset width + gusset), plain butt.");
    unresolved.Add(fbEnd);
    for (int m = 0; m <= N; m++)
    {
        string j = J(m);
        string sec = m == 0 || m == N ? "F3" : "F4";
        Mem("FB" + j, "N:FB" + j, "S:FB" + j, sec, TrussMemberRole.FloorBeam, $"Bay {j}", sa: In(9.5), sb: In(9.5), src: pgFloor + " / p.55 (floor beam F3 / F4)");
        Mem("BR-N" + j, "N:FT" + j, "N:FB" + j, "B1", TrussMemberRole.FloorBeam, $"Bay {j}", sa: 0, sb: In(9.5), src: pgFloor + " / p.55 (bracket B1)");
        Mem("BR-S" + j, "S:FT" + j, "S:FB" + j, "B1", TrussMemberRole.FloorBeam, $"Bay {j}", sa: 0, sb: In(9.5), src: pgFloor + " / p.55 (bracket B1)");
    }
    // B1 brackets are TAPERED (p.55 floor beam elevation): 6'-3 1/2 b. to b. of bracket angles at the truss, bottom angles sloping straight up to 2'-0 1/2 at the tip
    // (the tip is 8 in. beyond the outside stringer S5, p.48 / p.58 half plan); the top stays level with the floor beam.
    foreach (var m in t.Members.Where(m => m.Section == "B1")) m.DepthReductionStart = In(75.5 - 24.5);

    // laterals (p.48 / p.58 plans): K diagonals from the truss corners at one floor beam to the CL apex at the neighbouring one; same pattern top and bottom.
    // Left half: panels before the first pier joint: apex at the panel's first point, corners at the next; after it: apex at the next, corners at the first
    // (diamond about the pier joint); mirrored about the centre joint (X at the centre).
    int firstPier = u.PierM[1];
    foreach (var (plane, prefix, sec, role, sys, apexName) in new[]
    {
        ("top", "U", "WT6x29", TrussMemberRole.UpperLateral, "Top laterals", "UM"),
        ("bottom", "L", "HP14x73", TrussMemberRole.LowerLateral, "Bottom laterals", "LM"),
    })
    {
        for (int m = 0; m < N; m++)
        {
            int k = m < C ? m : N - 1 - m;                       // left-half panel index k -> panel k..k+1
            bool apexFirst = k < firstPier;                      // apex at the first point of the left-half panel
            int apexLeftHalf = apexFirst ? k : k + 1, cornerLeftHalf = apexFirst ? k + 1 : k;
            int apexM = m < C ? apexLeftHalf : N - apexLeftHalf, cornerM = m < C ? cornerLeftHalf : N - cornerLeftHalf;
            foreach (var pl in new[] { "N", "S" })
                Mem($"{sys[0]}L-{pl}{J(cornerM)}-{J(apexM)}", $"{pl}:{prefix}{J(cornerM)}", $"{pl}:{apexName}{J(apexM)}", sec, role, "", system: sys, src: pgFloor + $" {plane} lateral plan");
        }
    }
    // bottom struts at every point (14 BP 73; the 2-12[25 box at the pier joints and the centre joint of the 4-span unit)
    var boxJoints = new HashSet<int>(u.PierM.Skip(1).Take(u.PierM.Length - 2));
    for (int m = 0; m <= N; m++)
        Mem("BS" + J(m), "N:L" + J(m), "S:L" + J(m), boxJoints.Contains(m) ? "STRUTBOX" : "HP14x73", TrussMemberRole.Strut, "", system: "Bottom laterals", src: pgFloor + " bottom lateral plan / p.56 (struts)");
    // sway V frames (14 BP 73 H) at the printed joints, 10 WF 33 hanger bracing at the others (p.48 / p.58 elevation notes)
    int[] swayM = four ? new[] { 0, 3, 7, 10, 13, 17, 20 } : new[] { 0, 4, 8, 12 };
    var swayAll = new HashSet<int>(swayM.Concat(swayM.Select(x => N - x)));
    for (int m = 0; m <= N; m++)
    {
        if (swayAll.Contains(m))
            foreach (var pl in new[] { "N", "S" })
                Mem($"SW-{pl}{J(m)}", $"{pl}:SW{J(m)}", $"{pl}:LM{J(m)}", "HP14x73", TrussMemberRole.Sway, "", system: "Sway frames", src: pgFloor + " sway bracing (V, 24 rivets each end)");
        else
            Mem($"HB{J(m)}", "N:SWM" + J(m), "N:LM" + J(m), "W10x33", TrussMemberRole.Strut, "", system: "Hanger bracing", src: pgFloor + " hanger at panel points having no sway bracing (10 WF 33)");
    }
    UG("p.48, p.58", "top / bottom lateral K pattern and the end panels", "read from the half plans (K from the CL apex to the corners of the next floor beam, diamond about the pier joints, X at the centre) at 250-400 dpi; the first panel is drawn with the apex at the end joint",
        "pattern as read; transverse top struts = the floor beams.");
    UG("p.48, p.58", "location of 10 WF 33 hanger bracing", "the sketch shows it vertical within the 58'-0 frame under the floor beam without the exact line", "built vertical on the CL from the floor beam bottom to the bottom strut at every point without sway bracing.");
    UG("p.48, p.56, p.58", "lateral / strut rivet counts (10 / 20 / 40 / 6 per end), 7/16 gusset plates", "counts printed in zones of the plan whose allocation is ambiguous; gusset outlines not drawn", "not placed / not built.");
    UG(pg + ", " + pgFloor, "camber", "no camber ordinates are printed on these sheets (p.47: floor beams and brackets need not be cambered; stringers uncambered)", "geometry = final shape.");

    // ---- floor: stringers on the floor beams + deck slab (p.48 / p.58 plans, p.107 slab, p.55 bearing details)
    t.Floor = new TrussFloorDefinition
    {
        StringersOnTop = true, StringerTopBelowGrade = In(7.75), Source = pgFloor + " / p.107",
        Unresolved = U("p.55, p.107, p.48", "stringer bearings (variable bearing plates, sliding 'S' / fixed 'F' bearings), diaphragms 15 C 33.9, splices and which mark lies on which line",
            "the printed stringer-to-slab dimension is 7 3/4 (p.107) and the floor beam top is level 2'-8 3/4 under the gutter line (p.55): the bearing plate between them varies 1.1 - 5.9 in. across the crown (\"thicknesses ... shall be adjusted\", p.55) and is not dimensioned; the half plan names S5-S8 but not their lines",
            "stringer tops 7 3/4 under the finished grade (p.107), continuous over the floor beams, one straight piece per bay; the gap above each floor beam is NOT filled."),
    };
    // stringer lines (p.48 / p.58 half plans, p.55 bearing details): 4 spaces @ 8'-3 = 33'-0 each side of the CL, then the OUTSIDE (edge) stringer S5 21 WF 62
    // 6'-1 beyond the 33'-0 line = 39'-1 from the CL, 10 in. outside the gutter line (38'-3) under the curb / parapet and 8 in. inside the bracket tip (39'-9).
    // (Until 2026-10-20 S5 was built 8 in. outside the 33'-0 line - the 8 in. read as the stringer-to-stringer space - so the edge stringer was missing.)
    foreach (var (off, sec, nm) in new[] { (-39.0 - 1.0 / 12, "W21x62", "N39.08"), (-33.0, "W24x76", "N33"), (-24.75, "W24x76", "N24.75"), (-16.5, "W24x76", "N16.5"), (-8.25, "W24x76", "N8.25"), (0.0, "W24x76", "CL"),
        (8.25, "W24x76", "S8.25"), (16.5, "W24x76", "S16.5"), (24.75, "W24x76", "S24.75"), (33.0, "W24x76", "S33"), (39.0 + 1.0 / 12, "W21x62", "S39.08") })
        t.Floor.Stringers.Add(new TrussStringerLine { Name = nm, Offset = Ft(off), Section = sec });
    UG("p.48, p.58", "stringer mark per line (S6 / S7 / S8 = 24 WF 76)", "the half plans name S5 - S8 at the lines but the 24 WF 76 marks differ only in their stresses; S5 (21 WF 62) is the outside line at 39'-1 (p.55: 6'-1 beyond the 33'-0 line, 10 in. outside the gutter line)",
        "S5 = 21 WF 62 at 39'-1 from the CL, all other lines 24 WF 76 (S6 printed 24 WF 78 on p.58).");
    double sBeg = Math.Min(StaOf(0), StaOf(N)), sEnd = Math.Max(StaOf(0), StaOf(N));
    t.Floor.Deck = new TrussDeckDefinition
    {
        Thickness = In(6.75), LeftOffset = -Ft(38.25), RightOffset = Ft(38.25), BeginStation = sBeg, EndStation = sEnd, Source = "p.107",
        Unresolved = U("p.107, p.54, p.68", "slab thickness (6 3/4 on p.107, '6 1/4' read at low confidence on p.55), haunch over the stringers, curbs / parapets, railing, pavement breaks and the expansion dams at the unit ends",
            "p.107 gives 6 3/4 three times; the haunch and parapets belong to the deck / parapet work and the dam sheets p.54 / p.68 are not modeled yet", "6 3/4 slab between the curb lines (76'-6), joint 0 to joint 0'."),
    };

    // ---- shoes (one per truss plane at each pier joint): seat = bottom of shoe = top of pier (p.5 / p.32)
    for (int i = 0; i < u.PierM.Length; i++)
    {
        int m = u.PierM[i];
        string pt = "L" + J(m), type = u.PierType[i], sup = u.PierSupport[i];
        double seat = Ft(u.Seats[i]);
        double total = LowerEl(m) - seat;                         // bottom chord work point to bottom of shoe
        bool endShoe = i == 0 || i == u.PierM.Length - 1;
        foreach (var pl in new[] { "N", "S" })
        {
            TrussShoeDefinition sh;
            if (type == "F")
            {
                // fixed: p.52 (pier 11 / 26) / p.67 (15 / 22): 5 1/2 pin 3'-6 5/8, CL pin to bottom of shoe 3'-0 1/4, masonry plate 60 x 6 x 5'-6, 8 anchor bolts 3"
                sh = new TrussShoeDefinition
                {
                    Name = $"{pl}-{pt}", Plane = pl, Point = pt, Support = sup, Type = TrussShoeType.Fixed, PinDiameter = In(5.5), PinLength = FI(3, 6.625), Height = FI(3, 0.25),
                    PinBelowPoint = total - FI(3, 0.25), BodyLength = In(29.875), BodyTopLength = In(22), BodyWidth = FI(5, 3.5), MasonryLength = In(60), MasonryWidth = FI(5, 6), MasonryThickness = In(6),
                    SeatElevation = seat, AnchorBolts = 8, AnchorDiameter = In(3), Source = four ? "p.67" : "p.52",
                    Unresolved = U(four ? "p.67" : "p.52", "fixed shoe parts (stem plates 2-13x9x2'-5 5/8, 3 wing plates 29 7/8 x 4 x 5'-3 1/2, 4 end stiffeners, sole plate 22 x 10 3/16, inlay)", "the plates are listed but not drawn piece by piece in a form that can be placed from the raster",
                        "trapezoidal envelope: base = wing plate width 29 7/8 x 5'-3 1/2, top = 22 sole plate."),
                };
            }
            else if (endShoe)
            {
                // expansion at the unit ends (p.59 "shoes for both ends of the 3-span and 4-span trusses alike"): 5 1/2 pin 3'-2 1/8, CL pin 3'-1 1/2 above the bottom, masonry 36 x 6 1/2 x 5'-0, 4 x 2 1/4 anchor bolts
                sh = new TrussShoeDefinition
                {
                    Name = $"{pl}-{pt}", Plane = pl, Point = pt, Support = sup, Type = TrussShoeType.Expansion, PinDiameter = In(5.5), PinLength = FI(3, 2.125), Height = FI(3, 1.5),
                    PinBelowPoint = total - FI(3, 1.5), BodyLength = In(24), BodyTopLength = In(22), BodyWidth = FI(3, 8), MasonryLength = In(36), MasonryWidth = FI(5, 0), MasonryThickness = In(6.5),
                    SeatElevation = seat, AnchorBolts = 4, AnchorDiameter = In(2.25), Source = "p.59 / p.49",
                    Unresolved = U("p.59, p.49", "expansion shoe parts at the unit end (rocker / stem 23 1/8 x 11 x 2'-10, base 24 x 6 x 3'-8, wings, sole plate 22 x 7 9/16 or 9)", "listed in the material list but not drawn plate by plate", "trapezoidal envelope: base plate 24 x 3'-8, top = 22 sole plate."),
                };
            }
            else
            {
                // intermediate rocker: 3-span pier 12 / 25 (p.49: pin 6, masonry 48 x 8 x 5'-3, 6 x 2 1/4 anchor bolts) or 4-span piers 14 / 16 / 21 / 23 (p.63: pin 8 x 3'-4 5/8, masonry 54 x 9 1/2 x 5'-9)
                double pin = four ? 8 : 6;
                double pinFromBottom = total - FI(1, 5.5625);   // UNRESOLVED split of the printed total 5'-0 1/4 (p.49 / p.63): pin below the work point taken from the end shoe
                sh = new TrussShoeDefinition
                {
                    Name = $"{pl}-{pt}", Plane = pl, Point = pt, Support = sup, Type = TrussShoeType.Expansion, PinDiameter = In(pin), PinLength = four ? FI(3, 4.625) : FI(3, 2.625),
                    Height = pinFromBottom, PinBelowPoint = total - pinFromBottom, BodyLength = four ? In(30) : In(24), BodyTopLength = In(22), BodyWidth = four ? FI(2, 11.5) : FI(2, 10.5),
                    MasonryLength = four ? In(54) : In(48), MasonryWidth = four ? FI(5, 9) : FI(5, 3), MasonryThickness = four ? In(9.5) : In(8),
                    SeatElevation = seat, AnchorBolts = four ? 0 : 6, AnchorDiameter = four ? 0 : In(2.25), Source = four ? "p.63" : "p.49",
                    Unresolved = U(four ? "p.63" : "p.49", "intermediate rocker shoe: rocker " + (four ? "30 x 6 3/4 x 4'-0, stem 23 1/8 x 14 x 2'-11 1/2" : "24 x 6 x 3'-5 (R 2'-9), stem 26 5/8 x 12 x 2'-10 1/2") + " and the position of the pin within the printed 5'-0 1/4 from the work point to the bottom of the shoe",
                        "the total is printed (work point to bottom of shoe 5'-0 1/4) but the split between pin and rocker is not dimensioned in a form read here" + (four ? "; the anchor bolt count / size is not read" : ""), "trapezoidal envelope; pin 1'-5 9/16 below the work point as at the unit-end shoe."),
                };
            }
            t.Shoes.Add(sh);
        }
    }
    UG("p.54, p.68", "expansion dams at the unit ends and the pier joints", "the dam sheets give dimension strings and clearance tables but are not modeled in this phase", "not built; deck / floor system end at joint 0 and 0'.");
    UG("p.101-106", "inspection walkways, ladders and platforms under the trusses; light pole brackets", "not part of the truss steel (separate sheets); brackets for light poles are on DRB-6 (not in the set)", "not built.");
    unresolved.Add(fbPartial);

    // ---- joints: gusset pairs (p.50-53 / p.59-67); the sheets give the thickness and the in-to-in width, drawn outlines are not dimensioned
    t.JointDefaults = new TrussJointDefaults
    {
        AutoGussets = true, Lap = FI(2, 6), Margin = 0, RivetDiameter = In(1),
        Unresolved = U(jointPages, "gusset plate outlines and rivet counts", "the joint sheets draw the gussets and the 1 in. high-strength rivets without outline dimensions or counts", "convex envelope of the member ends + 2'-6 lap; thickness and in-to-in width from the sheet where read, else 3/4 flagged."),
    };
    for (int m = 0; m <= N; m++)
        foreach (var ul in new[] { "U", "L" })
        {
            string j = J(m);
            string key = ul + j.TrimEnd('\'');
            int k = Math.Min(m, N - m);
            string src = null;
            if (k == 0)
            {
                // unit end joints: p.59 (3-span end = "span 13 side" U0' / L0'; 4-span end = "span 14 side" U0 / L0)
                key = four ? ul + "0" : ul + "0'";
                src = "p.59 joint " + key + (four ? " (span 14 side)" : " (span 13 side; p.49 says 'similar to L0' / U0'')");
            }
            var r = (k == 0 ? jointRows.Where(x => x["unit"] == "B") : jointRows.Where(x => x["unit"] == u.Kind)).FirstOrDefault(x => x["joint"] == key);
            if (r == null || r["gusset_in"] == "") { UG(jointPages, $"gusset thickness of joint {ul}{j}", "the joint sheet was not read for this joint (not printed or not extracted)", "3/4 in. built, flagged."); continue; }
            var jd = new TrussJointDefinition { Point = ul + j, GussetThickness = In(D(r["gusset_in"])), Source = (src ?? ("p." + r["page"] + " joint " + key)) + ", " + r["confidence"] };
            if (r["in_to_in_in"] != "") jd.GussetsInsideFaceDistance = In(D(r["in_to_in_in"]));
            if (r["confidence"] != "high")
                jd.Unresolved = U("p." + r["page"], $"gusset thickness / width of joint {ul}{j}", $"read from a 130-230 dpi render ({r["confidence"]} confidence: 5/8, 3/4 and 7/8 can be confused - two misreads were found at U10 / L10); {r["note"]}", "value as read.");
            t.Joints.Add(jd);
        }
    UG(jointPages, "L8 / L0 second gusset pairs (7/8 + 3/4 outer / inner at L8 of the 3-span unit; 3/4 + 1/2 at L0 of both units), fills, splice plates, lug angles, diaphragms, stitch rivets", "listed per joint without extents",
        "only one gusset pair per joint (the outer / first listed) is built; fills and splices not built.");
    UG(jointPages, "plan weight check (p.42): floor steel, bracing, shoes, expansion dams, splices and fills are in the p.42 totals", "the totals are per unit (carbon + silicon, items 2 + 5) and include all steel of the unit", "the check script reports the model steel as a share of the p.42 figure.");
    t.Unresolved.AddRange(unresolved.Distinct());
    br.Truss = t;
    summary.Add($"{u.Bridge}: {t.Members.Count} member definitions, {t.Shoes.Count} shoes, {t.Joints.Count} joint definitions");
}

// ---------------------------------------------------------------- regenerate + save
var rep = Doc.Regenerate();
foreach (var u in units)
{
    var tm = Doc.Project.Bridges.Any(b => b.Name == u.Bridge) ? Doc.Model(u.Bridge).Steel?.Truss : null;
    foreach (var e in rep.Errors.Where(e => e.StartsWith(u.Bridge))) Print("ERROR " + e);
    if (tm == null) { Print($"{u.Bridge}: no truss model generated"); continue; }
    Print($"{u.Bridge}: {tm.Members.Count} members, {tm.Gussets} gussets, {tm.Shoes.Count} shoes, {tm.Steel.Parts.Count} parts, steel {tm.Steel.SteelMass * 2.20462262:N0} lb (p.42 unit: {u.PlanLb:N0}); checks {tm.Checks.Count(c => c.Ok)} OK / {tm.Checks.Count(c => !c.Ok)} DIFF; warnings {rep.Warnings.Count(w => w.StartsWith(u.Bridge))}");
    foreach (var c in tm.Checks.Where(c => !c.Ok).Take(12)) Print($"  DIFF {c.Item}: plan {c.Plan}, model {c.Model} {c.Note}");
}
Doc.Save(outPath);
Print($"Saved {outPath}");

record UnitDef(string Bridge, string Kind, double Sta0, int Dir, double Gutter0, int N, int C, int[] PierM, string[] PierSupport, string[] PierType, double[] Seats, string Pages, double PlanLb);
