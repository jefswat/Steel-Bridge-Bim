// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - GIRDER SPANS 1-10 (Pennsylvania) and 28-31 (New Jersey): simple riveted plate girder spans
// (two girders G1 at 58'-0 c-c, floor beams F1 / F2 + brackets B1, 4-span continuous stringers S1-S4, bottom laterals, 6 3/4 slab) as a generic
// TrussDefinition (SBM.Core.Truss.TrussGenerator, member role Girder + floor system; design note docs/plan-data/ptc-delaware-river/GIRDERS.md).
// Route: the truss generator (riveted built-up members + floor system + shoes), NOT the welded I-girder generator (it has no riveted angles) and NOT a
// third generator. Sheets: DRB-4 stress sheet 3 (PDF p.43), steel details 4 (p.44), floor beam / bracket / laterals / stringer splices 5 (p.45).
// On the PTC model (build-ptc-delaware-river.csx: "PTC Girder spans 1-10" / "PTC Girder spans 28-31" = support lines only on "CL Roadway (PTC)")
// the script turns those bridges into truss-generator bridges (SupportLinesOnly off, Girders.Count = 0, Truss = this data):
//   sbm run samples\plans\girders-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"   
//   sbm run samples\plans\check-girders-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Data: docs/plan-data/ptc-delaware-river/girders/seats.csv (p.43 bottom-of-shoe table), supports.csv (the supports). "p." = PDF page (DRB-4 sheet = page - 40).
// User rules: as-built set only; nothing invented: every value the sheets do not give is an UNRESOLVED flag with the page and a plain reason;
// plan-vs-plan differences are reported, never adjusted.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? "girders-ptc.sbm");
var inv = CultureInfo.InvariantCulture;
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double FI(double ft, double inch) => Ft(ft) + In(inch);

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "girders");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/ptc-delaware-river/girders not found above the current directory.");
var seatRows = File.ReadAllLines(Path.Combine(dataDir, "seats.csv")).Skip(1).Where(l => l.Trim() != "").Select(l => l.Split(','))
    .ToDictionary(f => f[0], f => (gutter: double.Parse(f[2], inv), shoe: double.Parse(f[3], inv)));

// ================================================================ constants (pages in the comments)
const double GirderCC = 58.0;                       // p.43 plan: 58'-0 C. to C. Girders (29'-0 each side of the CL bridge)
const double CurbOff = 38.25;                       // p.43 / p.107: 38'-3 CL roadway to curb
double bearToPier = FI(1, 3);                       // p.43 / p.44: CL bearing to CL pier 1'-3 (simple spans)
double endBeyondBrg = FI(1, 1);                     // p.44 elevation: 1'-1 from the CL bearing to the girder end (stiffener angles straddle the bearing)
double topAngleBelowGutter = FI(2, 9.375);          // p.43 / p.44: gutter grade to the BACK of the top flange angles 2'-9 3/8 (medium: 3/8 read at 300 dpi)
double girderDepthV = FI(9, 6.5625);                // p.44: 9'-6 9/16 b. to b. Ls VERTICALLY (= 9'-6 1/2 normal to grade, p.43)
double fbAngleBackBelowGutter = FI(2, 9.0625);      // p.43 section A-A: gutter grade to back of floor beam / bracket flange Ls 2'-9 1/16 (= girder FL cover top flush, p.45 note)
double fbDepth = In(75.5);                          // 6'-3 1/2 b. to b. Ls (p.43 / p.45)
double bracketEnd = Ft(39 + 1.0 / 12 + 0.5);        // UNRESOLVED: bracket B1 outer end = S1 line (39'-1) + 6"
double lowerLatNodeAboveFbTop = 0;                  // laterals at the top of the floor beam cover plates (see the flag)

var bridgeSpecs = new[]
{
    (name: "PTC Girder spans 1-10", firstSpan: 1, fixedAtBegin: true),
    (name: "PTC Girder spans 28-31", firstSpan: 28, fixedAtBegin: false),
};
var report = new List<string>();
foreach (var spec in bridgeSpecs)
{
    var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == spec.name);
    if (br == null) { Print($"Bridge '{spec.name}' not found in this model - run on the PTC model (build-ptc-delaware-river.csx)."); continue; }
    var al = Doc.Project.FindAlignment(br.AlignmentName)!;
    var sup = br.Supports.OrderBy(s => s.Station).ToList();
    br.SupportLinesOnly = false;
    br.Girders.Count = 0;
    br.Girders.Spacings = new();
    var unresolved = new List<string>();
    void UG(string pages, string item, string why, string then = "") => unresolved.Add(U(pages, item, why, then));

    double Gutter(double sta) => al.ElevationAt(sta, Ft(CurbOff));                       // printed gutter profile (p.43): right edge, symmetric about the CL (crown 1/8"/ft)
    double TopBack(double sta) => Gutter(sta) - topAngleBelowGutter;                      // back of the top flange angles
    double GirderMid(double sta) => TopBack(sta) - girderDepthV / 2;                      // girder working line = mid-depth of the web
    double Wp(double sta) => TopBack(sta) - girderDepthV;                                 // W.P. = back of the bottom angles (p.44)
    double FbAxisAtGirder(double sta) => Gutter(sta) - fbAngleBackBelowGutter - fbDepth / 2;
    double GradeAt(double sta, double off) => al.ElevationAt(sta, off);
    // floor beam axis at an offset: crowned parallel to the roadway grade, flush with the girder FL cover plate at the girder lines
    double FbAxis(double sta, double off) => FbAxisAtGirder(sta) + GradeAt(sta, off) - GradeAt(sta, Math.Sign(off == 0 ? 1 : off) * Ft(GirderCC / 2));
    double FbCoverTop(double sta, double off) => FbAxis(sta, off) + fbDepth / 2 + In(0.5);

    var t = new TrussDefinition
    {
        Name = spec.name.Replace("PTC ", ""), Source = "PTC DRB-4 sheets 3-5 (as-built 1954-57)", DefaultMaterial = "Structural steel (C / S not given)",
        Materials = { ["C"] = "Structural carbon steel (C)", ["S"] = "Structural silicon steel (S)" },   // p.43 / p.44 notes: silicon steel except carbon where noted (C)
    };
    t.Planes.Add(new TrussPlaneDefinition { Name = "N", Offset = -Ft(GirderCC / 2), Description = "north girder G1 (left of the alignment), p.43 58'-0 c-c" });
    t.Planes.Add(new TrussPlaneDefinition { Name = "S", Offset = Ft(GirderCC / 2), Description = "south girder G1 (right), p.43" });
    t.JointDefaults.AutoGussets = false;

    // ------------------------------------------------------------ sections
    string stiffNotBuilt = U("p.43, p.44", "girder web stiffeners, end stiffeners and fills, cover plate splice, rivets",
        "the end stiffeners (4-Ls 7x4x3/4 + fills 15x1 1/8), intermediate stiffener pairs (2-Ls 6x4x7/16, 9 / 6 / 6 / 9 equal spaces per panel), long stiffeners, the full length cover plate splice (CL splice 37'-0 from the CL girder + 6'-0 additional material) and the rivet patterns are drawn / listed but not dimensioned station by station",
        "built: web, 4 flange angles and the cover plates; not built: the items listed.");
    string topPlateConflict = U("p.43, p.44", "CONFLICT: top cover plate length 86'-0",
        "the stress sheet and the p.44 label both print 86'-0 but the elevation dimension lines (25'-6 / 37'-0 / 45'-0 each side of the CL = 51 / 74 / 90 ft) match the BOTTOM plates (90 / 74 / 51) and no dimension gives 86",
        "the top plates are built as printed (full length + 86'-0 + 51'-0, centred); the 86'-0 is reported as a plan conflict.");
    TrussPlate Pl(double w, double t_, string label, double len = 0, double off = 0) => new TrussPlate(w, t_, false, label) { Length = len, Offset = off };
    var cw = In(22); var ct = In(13.0 / 16);
    t.Sections.Add(new TrussSectionDefinition
    {
        Name = "G1", Kind = TrussSectionKind.H, WebVertical = true, Material = "S",
        Label = "G1: 1-Pl 114x7/16; 4-Ls 8x8x1 1/8; top 1-Pl 22x13/16 F.L., x 86'-0, x 51'-0; bottom 1-Pl 22x13/16 x 90'-0, x 74'-0, x 51'-0; 9'-6 1/2 b.b. Ls",
        WebDepth = In(114), WebPlies = { new TrussPly(In(114), In(7.0 / 16), "web PL 114 x 7/16") }, Angle = "L8x8x1-1/8", AngleProjection = In(0.25),   // 9'-6 1/2 b.b. = 114 + 2 x 1/4
        TopPlates = { Pl(cw, ct, "cover PL 22 x 13/16 F.L."), Pl(cw, ct, "cover PL 22 x 13/16 x 86'-0", Ft(86)), Pl(cw, ct, "cover PL 22 x 13/16 x 51'-0", Ft(51)) },
        BottomPlates = { Pl(cw, ct, "cover PL 22 x 13/16 x 90'-0", Ft(90)), Pl(cw, ct, "cover PL 22 x 13/16 x 74'-0", Ft(74)), Pl(cw, ct, "cover PL 22 x 13/16 x 51'-0", Ft(51)) },
        Source = "p.43 / p.44", Unresolved = stiffNotBuilt + " " + topPlateConflict,
    });
    // floor beam halves (girder -> CL): partial plates are measured from the CL (F2: bottom 42'-0 + 30'-0, top 30'-0; F1: bottom 16'-0)
    double crown = Ft(GirderCC / 2) / 96.0;                                           // 1/8" per ft over 29'-0 (p.107)
    double halfCut = Math.Sqrt(Ft(GirderCC / 2) * Ft(GirderCC / 2) + crown * crown) - In(11);   // girder-side setback 11" (cover plate half width)
    double FromCl(double plateLen) => halfCut / 2 - plateLen / 4;                      // centre offset of a plate that is plateLen long over BOTH halves, flush at the CL
    var fw = In(14); var ft_ = In(0.5);
    string fbPartial = U("p.43, p.45", "floor beam / bracket connections, end angles, fills and partial plate positions",
        "the connection angles (2-Ls 5x3 1/2x7/16 + fills), stiffeners, 2-Ls 6x4x3/8 gussets and rivets are drawn but not counted; the partial cover plates are listed by length only (taken centred on the beam CL)",
        "web and flange angles + the listed cover plates are built; the beam ends 11 in. from the girder CL (edge of the girder cover plate), connection material not built.");
    string fbCrown = U("p.43, p.45", "floor beam top profile", "p.43 section A-A shows the beam tops following the roadway crown; p.45 says the cover plates are flush with the girder F.L. plate at the girders",
        "beam halves straight from each girder (flush with its F.L. cover top) to the CL, parallel to the roadway grade.");
    TrussSectionDefinition FbSec(string name, string label, List<TrussPlate> top, List<TrussPlate> bot, string extra = "") => new TrussSectionDefinition
    {
        Name = name, Kind = TrussSectionKind.H, WebVertical = true, Material = "S", Label = label, WebDepth = In(75), WebPlies = { new TrussPly(In(75), In(7.0 / 16), "web PL 75 x 7/16") },
        Angle = "L6x6x1/2", AngleProjection = In(0.25), TopPlates = top, BottomPlates = bot, Source = "p.43 / p.45", Unresolved = fbPartial + " " + fbCrown + extra,
    };
    t.Sections.Add(FbSec("F1", "F1: Web 75x7/16; 4-Ls 6x6x1/2; top 1-Pl 14x1/2 F.L.; bottom 1-Pl 14x1/2 x 16'-0", new() { Pl(fw, ft_, "cover PL 14 x 1/2 F.L.") },
        new() { Pl(fw, ft_, "cover PL 14 x 1/2 x 16'-0", Ft(8), FromCl(Ft(16))) }));   // 16'-0 over both halves = 8'-0 each, flush at the CL
    t.Sections.Add(FbSec("F2", "F2: Web 75x7/16; 4-Ls 6x6x1/2; top 1-Pl 14x1/2 F.L. + 1-Pl 14x1/2 x 30'-0; bottom 1-Pl 14x1/2 x 42'-0 + 1-Pl 14x1/2 x 30'-0",
        new() { Pl(fw, ft_, "cover PL 14 x 1/2 F.L."), Pl(fw, ft_, "cover PL 14 x 1/2 x 30'-0", Ft(15), FromCl(Ft(30))) },
        new() { Pl(fw, ft_, "cover PL 14 x 1/2 x 42'-0", Ft(21), FromCl(Ft(42))), Pl(fw, ft_, "cover PL 14 x 1/2 x 30'-0", Ft(15), FromCl(Ft(30))) }));
    t.Sections.Add(new TrussSectionDefinition
    {
        Name = "B1", Kind = TrussSectionKind.H, WebVertical = true, Material = "S", Label = "B1: 7/16 web; 4-Ls 6x6x1/2 (+ diagonal 2-Ls 6x6x1/2); top 1-Pl 14x1/2 F.L.; 6'-3 1/2 b.b. Ls",
        WebDepth = In(75), WebPlies = { new TrussPly(In(75), In(7.0 / 16), "web PL 75 x 7/16") }, Angle = "L6x6x1/2", AngleProjection = In(0.25), TopPlates = { Pl(fw, ft_, "cover PL 14 x 1/2 F.L.") },
        Source = "p.43 / p.45",
        Unresolved = fbPartial + " " + fbCrown + " " + U("p.43, p.45", "bracket B1 depth along its length", "the bracket bottom slopes up toward the outer end (diagonal 2-Ls 6x6x1/2, 2-7/16 plates) but no end depth is dimensioned on the sheets read",
            "constant 6'-3 1/2 depth over the whole bracket (weight overstated); outer end = S1 line + 6\"."),
    });
    // stringers: 1954 WF = AISC v16 W of the same nominal size and weight (not identical dimensions)
    string wfFlag = U("p.43", "old WF designations 21 WF 62 / 24 WF 84 / 24 WF 94", "the 1954 rolled shapes pre-date the W series; their flange / web dimensions are not on the sheets", "AISC v16 W21x62 / W24x84 / W24x94 of the same nominal weight (dimensions differ slightly).");
    foreach (var (n, d) in new[] { ("W21x62", "21 WF 62 (S1)"), ("W24x84", "24 WF 84 (S2)"), ("W24x94", "24 WF 94 (S3 / S4)") })
        t.Sections.Add(new TrussSectionDefinition { Name = n, Kind = TrussSectionKind.Rolled, Designation = n, Material = "C", Label = d, Source = "p.43", Unresolved = wfFlag });
    t.Sections.Add(new TrussSectionDefinition
    {
        Name = "LAT", Kind = TrussSectionKind.Rolled, Designation = "WT6x29", Material = "C", Label = "ST 6 WF 29 (C)", Source = "p.43 / p.45",
        Unresolved = U("p.43, p.45", "lateral bracing ST 6 WF 29: shape, orientation and plane", "the 1954 structural tee cut from a WF is taken as AISC v16 WT6x29 (same nominal weight); the plan view gives the pattern only and p.45 shows the connection to the stringers (4 x 3/8 horizontal leg angle) and a 7/16 gusset at the CL floor beam without dimensions",
            "WT6x29 (flange up) running between the work points at the top of the floor beam cover plates; gussets, tension plates and rivets (10 per end) not built."),
    });

    // ------------------------------------------------------------ spans, points, members
    var spans = new List<(int n, double sa, double sb, double ga, double gb, string supA, string supB, bool fixedA)>();
    string[] pierLike(string s) => new[] { s };
    bool IsCl(SupportDefinition s) => System.Text.RegularExpressions.Regex.IsMatch(s.Name, @"^Pier \d+$");     // CL pier of a simple-span pier (shoes 1'-3 each side)
    for (int j = 0; j + 1 < sup.Count; j++)
    {
        int n = spec.firstSpan + j;
        double a = sup[j].Station + (IsCl(sup[j]) ? bearToPier : 0), b = sup[j + 1].Station - (IsCl(sup[j + 1]) ? bearToPier : 0);
        spans.Add((n, a, b, a - endBeyondBrg, b + endBeyondBrg, sup[j].Name, sup[j + 1].Name, spec.fixedAtBegin));
    }
    foreach (var s in spans)
    {
        double len = (s.sb - s.sa) / 0.3048;
        string src = "p.43 / p.44";
        // points: girder ends, W.P. at the bearings
        t.Points.Add(new TrussPointDefinition($"GA{s.n}", s.ga, GirderMid(s.ga), src + " girder end 1'-1 beyond the CL bearing, mid-depth of the web"));
        t.Points.Add(new TrussPointDefinition($"GB{s.n}", s.gb, GirderMid(s.gb), src));
        // W.P. on the girder CHORD at the bearing (the chord of the profile points at the girder ends, final shape; camber not modeled)
        double zGa = GirderMid(s.ga), zGb = GirderMid(s.gb);
        double WpOnChord(double sta) => zGa + (zGb - zGa) * (sta - s.ga) / (s.gb - s.ga) - girderDepthV / 2;
        t.Points.Add(new TrussPointDefinition($"WA{s.n}", s.sa, WpOnChord(s.sa), "p.44 W.P. = back of the bottom angles at the CL bearing"));
        t.Points.Add(new TrussPointDefinition($"WB{s.n}", s.sb, WpOnChord(s.sb), "p.44"));
        double pitch = (s.sb - s.sa) / 4;                                                   // p.43: 4 spaces @ 30'-5 3/4 = 121'-11 c-c bearings (spans 1-9, 29-31)
        for (int k = 0; k < 5; k++)
        {
            double st = s.sa + k * pitch;
            double go = Ft(GirderCC / 2), co = go + (bracketEnd - go);
            t.Points.Add(new TrussPointDefinition($"F{s.n}_{k}", st, FbAxis(st, go), "p.43 / p.45 floor beam axis at the girder"));
            foreach (var (pl, sg) in new[] { ("N", -1), ("S", 1) })
            {
                t.Points.Add(new TrussPointDefinition($"C{s.n}_{k}", st, FbAxis(st, 0), "floor beam axis at the CL (crown)") { Offset = -sg * go, Planes = new() { pl } });
                t.Points.Add(new TrussPointDefinition($"X{s.n}_{k}", st, FbAxis(st, sg * bracketEnd), "bracket outer end") { Offset = sg * (bracketEnd - go), Planes = new() { pl } });
                t.Points.Add(new TrussPointDefinition($"LM{s.n}_{k}", st, FbCoverTop(st, 0) + lowerLatNodeAboveFbTop, "lateral node at the CL floor beam") { Offset = -sg * go, Planes = new() { pl } });
            }
            t.Points.Add(new TrussPointDefinition($"LG{s.n}_{k}", st, FbCoverTop(st, go) + lowerLatNodeAboveFbTop, "lateral node at the girder"));
        }
        // members: girder, floor beam halves, brackets
        string panel = $"Span {s.n}";
        t.Members.Add(new TrussMemberDefinition { Name = $"G{s.n}", From = $"GA{s.n}", To = $"GB{s.n}", Section = "G1", Role = TrussMemberRole.Girder, Panel = panel, Source = src });
        for (int k = 0; k < 5; k++)
        {
            string fsec = k == 0 || k == 4 ? "F1" : "F2";
            foreach (var pl in new[] { "N", "S" })
            {
                t.Members.Add(new TrussMemberDefinition { Name = $"{pl}-{(fsec == "F1" ? "F1" : "F2")}-{s.n}-{k}", From = $"{pl}:F{s.n}_{k}", To = $"{pl}:C{s.n}_{k}", Section = fsec, Role = TrussMemberRole.FloorBeam,
                    Panel = panel, StartSetback = In(11), EndSetback = 0, Source = "p.43 / p.45" });
                t.Members.Add(new TrussMemberDefinition { Name = $"{pl}-B1-{s.n}-{k}", From = $"{pl}:F{s.n}_{k}", To = $"{pl}:X{s.n}_{k}", Section = "B1", Role = TrussMemberRole.FloorBeam,
                    Panel = panel, StartSetback = In(11), EndSetback = 0, Source = "p.43 / p.45" });
            }
        }
        // bottom laterals (p.43 plan): CL0-G1 / CL1-G2 / G2-CL3 / G3-CL4 on each side
        double latLen = Math.Sqrt(Ft(GirderCC / 2) * Ft(GirderCC / 2) + pitch * pitch);
        double latSetGirder = In(11) / (Ft(GirderCC / 2) / latLen);                         // clears the 22" cover plate of the girder
        foreach (var pl in new[] { "N", "S" })
            foreach (var (a, b, cl) in new[] { (0, 1, true), (1, 2, true), (3, 2, false), (4, 3, false) })
            {
                string fromP = $"{pl}:LM{s.n}_{a}", toP = $"{pl}:LG{s.n}_{b}";
                t.Members.Add(new TrussMemberDefinition { Name = $"LAT-{pl}-{s.n}-{a}{b}", From = fromP, To = toP, Section = "LAT", Role = TrussMemberRole.LowerLateral, Panel = panel,
                    System = $"Bottom laterals span {s.n}", StartSetback = 0, EndSetback = latSetGirder, Source = "p.43 plan" });
            }
        // stringer lines of this span (limited to its four floor beam bays)
        var lines = new[] { (-39 - 1 / 12.0, "W21x62", "S1"), (-33.0, "W24x84", "S2"), (-24.75, "W24x94", "S3"), (-16.5, "W24x94", "S3"), (-8.25, "W24x94", "S3"), (0.0, "W24x94", "S4"),
            (8.25, "W24x94", "S3"), (16.5, "W24x94", "S3"), (24.75, "W24x94", "S3"), (33.0, "W24x84", "S2"), (39 + 1 / 12.0, "W21x62", "S1") };
        t.Floor ??= new TrussFloorDefinition { StringersOnTop = true, StringerTopBelowGrade = In(7.75), Source = "p.43 / p.107" };
        foreach (var (off, sec, mk) in lines)
            t.Floor.Stringers.Add(new TrussStringerLine { Name = $"{s.n}-{mk}-{(off < 0 ? "N" : off > 0 ? "S" : "C")}{Math.Abs(off).ToString("0.##", inv)}", Offset = Ft(off), Section = sec,
                FromStation = s.sa - 0.001, ToStation = s.sb + 0.001 });
        // shoes: fixed at one end (p.43 FIX / EXP), expansion at the other
        foreach (var pl in new[] { "N", "S" })
            foreach (var (end, pt, supName, sta) in new[] { ("a", $"WA{s.n}", s.supA, s.sa), ("b", $"WB{s.n}", s.supB, s.sb) })
            {
                bool fixedHere = (end == "a") == s.fixedA;
                var typ = fixedHere ? TrussShoeType.Fixed : TrussShoeType.Expansion;
                double seat = seatRows.TryGetValue(supName, out var sr) ? Ft(sr.shoe) : double.NaN;
                t.Shoes.Add(new TrussShoeDefinition
                {
                    Name = $"{pl}-{s.n}{end}", Plane = pl, Point = pt, Support = supName, Type = typ, PinDiameter = 0, PinBelowPoint = 0, Height = FI(2, 0.5),    // W.P. to the bottom of the masonry plate 2'-0 1/2 (p.44: 3" sole + 18" shoe + 3 1/2" masonry)
                    BodyLength = typ == TrussShoeType.Fixed ? In(9) : In(9), BodyTopLength = In(9), BodyWidth = typ == TrussShoeType.Fixed ? In(18) : FI(3, 0), BodyTopAbovePin = 0,
                    MasonryLength = typ == TrussShoeType.Fixed ? In(24) : In(19), MasonryWidth = typ == TrussShoeType.Fixed ? FI(3, 0) : FI(4, 0), MasonryThickness = typ == TrussShoeType.Fixed ? In(4) : In(3.5),
                    SeatElevation = seat, Source = "p.43 / p.44",
                    Unresolved = typ == TrussShoeType.Fixed
                        ? U("p.44", "fixed shoe body (sole plate 11x4 3/4x1'-6 beveled, key, stem plate 18x9, 2 stiffeners 7x1, 2 wing plates 15x1x1'-10)", "the plates are listed with their sizes but their arrangement is only drawn; the sole plate bevel is not dimensioned", "one envelope 9 in. x 18 in. x (W.P. to the masonry plate) including the sole plate; masonry plate 24 x 4 x 3'-0 as listed.")
                        : U("p.44", "expansion shoe body (sole plate, 17 5/8 x 9 x 3'-0 rocker, key, 2 stainless dowels 2 in x 3 in)", "the rocker radius (1'-6) and the groove are drawn, the sole plate bevel is not dimensioned", "one envelope 9 in. x 3'-0 x (W.P. to the masonry plate) including the sole plate; masonry plate 19 x 3 1/2 x 4'-0 as listed."),
                });
            }
    }
    t.Floor!.Deck = new TrussDeckDefinition
    {
        Thickness = In(7.75), LeftOffset = -Ft(CurbOff), RightOffset = Ft(CurbOff), BeginStation = spans[0].ga, EndStation = spans[^1].gb, Source = "p.43 / p.107",
        Unresolved = U("p.107, p.46", "deck slab: 6 3/4 slab + haunch, curbs, parapets, expansion dams, walkway", "p.107 gives the 6 3/4 slab with 7 3/4 typical over the stringers; the dams (p.46) and the curb / parapet sheets are not modeled here",
            "one 7 3/4 slab between the curb lines (76'-6) from the first to the last girder end, continuous over the 4 in. gaps at the piers (the expansion dam fills them)."),
    };
    t.Floor.Unresolved = U("p.43, p.45", "stringer bearings, splices and diaphragms", "the stringers are 4-span continuous with a CL splice per line (p.45 splice plates 24 WF: 4x9/16 + 9x1/2 + 2 x 18 1/2x3/8) and bearing plates thickened for the dead load camber; the diaphragms 15[ 33.9 mid-panel and 18 WF 64 at the piers are only located",
        "stringers built as straight W pieces between consecutive floor beams (CL to CL) with their tops 7 3/4 under the finished grade; bearing plates, splices, clip angles and diaphragms not built.");
    // ------------------------------------------------------------ plan flags
    UG("p.43, p.44", "camber of the girders (1 1/4\" spans 1-3, 2\" others, vertical curve) and the girder profile", "the girders are cambered for dead load and the vertical curve; the camber diagram gives the maximum only",
        "girders are the straight chord between the W.P.s at their ends on the printed gutter profile (final shape); the profile curve (spans 1-3, up to 3/4\" above the chord) is not followed.");
    UG("p.43, p.44", "fixed / expansion end of the girder spans 28-31", "the stress sheet draws the spans with the fixed end away from the river (spans 1-10 fixed at the west end); the same drawing serves spans 28-31",
        "spans 28-31 mirrored: fixed at the EAST end of each span (to be confirmed on sheets 9 / 67 / 68, not read).");
    UG("p.43, p.44", "girder end and bearing position of spans 10 and 28", "p.43 gives the 120'-5 1/4 c-c bearings and 'alike except expansion end' but the floor beam spacing (30'-5 3/4 is for 121'-11) and the end detail are on sheet 9",
        "floor beams at equal spaces of the c-c bearing length (4 spaces), girder ends 1'-1 beyond the bearings, cover plates as spans 1-9.");
    UG("p.46", "expansion dams at the piers and the end dams / backwall joints", "p.46 dimensions the finger plate dam (edge members 12[ 35, 18 WF 64 diaphragms with 2-Ls 4x4x7/16, anchor bars) but it is a separate pay item",
        "not built; the 4 in. gap between adjacent girder ends is bridged by the slab.");
    UG("p.43", "inspection walk (CL 2'-0 from the CL bridge), scuppers, signal / light pole bracket extensions (p.45)", "shown on the plan / sections without dimensions I could read", "not built.");
    t.Unresolved.AddRange(unresolved.Distinct());
    // plan quantities p.42 (items 1 + 4 per state): compared by the check; also the generator's own weight check
    if (spec.firstSpan == 1) { t.PlanSteelWeightLb = 2_496_000 + 2_727_000; t.PlanSteelWeightSource = "p.42 items 1 + 4, Pennsylvania girder spans (incl. dams, shoes, walkway, other pay items)"; }
    else { t.PlanSteelWeightLb = 1_004_000 + 1_093_000; t.PlanSteelWeightSource = "p.42 items 1 + 4, New Jersey girder spans (incl. dams, shoes, walkway, other pay items)"; }
    br.Truss = t;
}

var rep = Doc.Regenerate();
foreach (var spec in bridgeSpecs)
{
    var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == spec.name);
    if (br == null) continue;
    foreach (var e in rep.Errors.Where(e => e.StartsWith(br.Name))) Print("ERROR " + e);
    var tm = Doc.Model(br.Name).Steel?.Truss;
    if (tm == null) { Print($"ERROR: no truss model generated for {br.Name}"); continue; }
    Print($"{br.Name}: {tm.Members.Count} members, {tm.Shoes.Count} shoes, {tm.Steel.Parts.Count} parts, steel {tm.Steel.SteelMass * 2.20462262:N0} lb (p.42: {br.Truss!.PlanSteelWeightLb:N0} lb incl. other items); checks {tm.Checks.Count(c => c.Ok)} OK / {tm.Checks.Count(c => !c.Ok)} DIFF; warnings {rep.Warnings.Count(w => w.StartsWith(br.Name))}");
}
Doc.Save(outPath);
Print($"Saved {outPath}");
