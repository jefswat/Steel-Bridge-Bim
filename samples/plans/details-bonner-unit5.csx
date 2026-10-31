// NCDOT B-2500 Bonner Unit 5 segmental box - CONCRETE DETAILS and CRITICAL-SECTION REINFORCING (re-runnable; called by build-bonner-unit5.csx with
// "--no-regen", or standalone: sbm run "samples\plans\details-bonner-unit5.csx" "samples\plans\Bonner Unit 5 segmental.sbm" [out.sbm]).
// Data: docs/plan-data/bonner-b2500/segmental-unit5/DETAILS-DATA.md + anchor-blocks.csv, anchor-block-locations.csv, openings.csv, parapet.csv,
// typical-segment-bar-list.csv, critical-section-rebar.csv (every value with its PDF page). Generic inputs: SegmentalDefinition.BlockTypes / Blocks /
// Openings / Parapet / Bars (SegmentalDetails.cs). Rules (user): nothing invented - scaled / derived values are flagged (page + plain reason);
// items the plans do not dimension are recorded and NOT built; plan differences reported, never adjusted.
// USER DECISION 2026-10-30: "Dimensions are measured from the bulkhead face. These are short line match cast segments. So the form is built off the face
// further from the pier." -> block / pipe / opening table dimensions along a typical segment are measured from its TIP-SIDE (bulkhead) joint; the match-cast
// face is the pier side. Where "farther from the pier" is ambiguous (closure joints, bent halves, end segments) the items stay flagged.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Bridge;

const string BridgeName = "Bonner Unit 5";
bool noRegen = Args.Contains("--no-regen");
string outPath = Args.FirstOrDefault(a => !a.StartsWith("--")) ?? Doc.FilePath ?? @"samples\plans\Bonner Unit 5 segmental.sbm";
var br = Doc.Project.Bridges.First(b => b.Name == BridgeName);
var sg = br.Segmental ?? throw new InvalidOperationException("Run build-bonner-unit5.csx first (no segmental definition).");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "bonner-b2500", "segmental-unit5");
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
var segNames = sg.Spans.SelectMany(s => s.Segments).Where(s => s.Type == SegmentType.Typical).Select(s => s.Name).ToList();

// ================================================================ 1. block types (p.297-302)
sg.BlockTypes = new List<SegmentalBlockType>
{
    new()
    {
        // p.297 (S3-49): starts 4'-0" from the joint the anchor faces, 8'-6" long, 1'-6" to the next joint; 2'-10" wide at the anchor end (from the web face
        // at the block top), 2'-5 1/2" where the plan taper starts; level top 2'-0" above the bottom slab top at the anchor (anchor 1'-1" above the slab + 11");
        // the top slopes straight down to the slab at the taper end.
        Name = "bottom slab anchor block", Kind = SegmentalBlockKind.AnchorBlock, From = SegmentalJointRef.BentSide, Start = F(4),
        Description = "Bottom slab PT anchor block (SDI 22.6-PC, B tendons)", Source = "p.297 (S3-49)",
        Stations = new()
        {
            new() { Along = 0, TopAboveSlab = In(24), WidthFromWeb = In(34) },
            new() { Along = In(76.5), TopAboveSlab = In(24) * (1 - 76.5 / 102), WidthFromWeb = In(29.5) },
            new() { Along = In(102), TopAboveSlab = 0, WidthFromWeb = 0 },
        },
        Unresolved = U("p.297", "bottom slab anchor block shape", "the straight / taper lengths (76 1/2\" + 25 1/2\") and the corner chamfer are scaled from the vector drawing, the anchor face is 'VARIES' (normal to the R 23'-0\" tendon, ~12 deg) and the block top is drawn, not dimensioned, between the anchor and the taper end; the side of the block the anchor faces is read from the tendon path (it rises from the bottom slab toward the bent side)",
            "anchor face square to the segment 4'-0\" from the bent-side joint, top straight from 2'-0\" above the slab at the anchor to the slab at 8'-6\" (6\" at the scaled taper start), plan width 2'-10\" -> 2'-5 1/2\" -> 0 at the web; corner chamfer not built."),
    },
    new()
    {
        // B3 start block in XX-13 of the end spans: the p.297 block mirrored (anchor face 4'-0" from the bulkhead face = toward the end segment, taper toward the bent)
        Name = "bottom slab anchor block (start)", Kind = SegmentalBlockKind.AnchorBlock, From = SegmentalJointRef.TipSide, Start = F(4),
        Description = "Bottom slab PT anchor block (SDI 22.6-PC, B3 start in the end span)", Source = "p.285, p.296, p.297 (S3-49)",
        Stations = new()
        {
            new() { Along = 0, TopAboveSlab = In(24), WidthFromWeb = In(34) },
            new() { Along = In(76.5), TopAboveSlab = In(24) * (1 - 76.5 / 102), WidthFromWeb = In(29.5) },
            new() { Along = In(102), TopAboveSlab = 0, WidthFromWeb = 0 },
        },
        Unresolved = U("p.285, 296, 297", "end-span B3 start block in XX-13", "p.285 / p.296 label a bottom slab anchor block for B3 in the end-span segment 13 but no sheet details it; p.297 details the block with its anchor face toward the bent (the tendon leaving toward the closure) - B3 leaves this block toward the bent",
            "the p.297 block mirrored: anchor face 4'-0\" from the bulkhead (end segment side) face, taper toward the bent; shape flags of the p.297 block apply."),
    },
    new()
    {
        // p.298 (S3-50): anchor face 3'-5" from the joint (block bottom), 6'-9" long, square end 3'-10" from the next joint; bottom face square to the web,
        // 3'-2 1/4" from the web inside face, its free edge 2'-11 1/4" below the deck top; inner face vertical.
        // the anchor face leans toward the bent going up (drawn ~15 deg from vertical, normal to the tendon): through the bottom corner 3'-5" from the bent-side
        // joint at 2'-11 1/4" and the anchor 3'-4" from it at 2'-7 1/4" (p.298 elevation chain 3'-4" + 5'-0" + 5'-8") = 4" deeper per 1" along, built as a 2"
        // ramp of the block bottom from 3'-3" (2'-3 1/4") to 3'-5"
        Name = "top slab anchor block", Kind = SegmentalBlockKind.AnchorBlock, From = SegmentalJointRef.BentSide, Start = F(3, 3),
        Description = "Top slab PT anchor block (SDI 12.6-PC, C tendons)", Source = "p.298 (S3-50)",
        Stations = new()
        {
            new() { Along = 0, BottomBelowDeck = F(2, 3.25), BottomSquareToWeb = F(3, 2.25) },
            new() { Along = In(2), BottomBelowDeck = F(2, 11.25), BottomSquareToWeb = F(3, 2.25) },
            new() { Along = F(6, 11), BottomBelowDeck = F(2, 11.25), BottomSquareToWeb = F(3, 2.25) },
        },
        Unresolved = U("p.298", "top slab anchor block shape", "the anchor face is drawn ~15 deg from vertical (normal to the R 16'-0\" tendon, scaled) and a second bottom line where the sloped bottom meets the web is drawn without a dimension; the inner face position from CL girder is not dimensioned (scaled 7'-1 1/4\")",
            "6'-9\" long from the bottom corner 3'-5\" off the bent-side joint, anchor face sloped through that corner and the anchor (3'-4\", 2'-7 1/4\" below the deck top; a 2\" ramp of the block bottom), bottom square to the web 3'-2 1/4\" long, its free edge 2'-11 1/4\" below the deck top, inner face vertical (its height and offset are check rows)."),
    },
    new()
    {
        // p.299 (S3-51) Section E-E: bars 2'-6" and 3'-2" from CL girder, block 6" + 8" + 6" = 1'-8" about them, 2'-0" long, 2'-11" from the joint, top 6" above
        // the bars = 1'-0 1/2" above the soffit.
        Name = "erection PT bar block Type B", Kind = SegmentalBlockKind.ErectionBarBlock, From = SegmentalJointRef.TipSide, Start = F(2, 11),
        Description = "Temporary bottom PT bar block Type B (1 3/4\" bars)", Source = "p.299 (S3-51)",
        Stations = new()
        {
            new() { Along = 0, InnerFromCl = F(2, 0), OuterFromCl = F(3, 8), TopAboveSoffit = In(12.5) },
            new() { Along = F(2), InnerFromCl = F(2, 0), OuterFromCl = F(3, 8), TopAboveSoffit = In(12.5) },
        },
        // 2'-11" from the BULKHEAD face = the tip-side joint (user decision 2026-10-30; was flagged).
    },
    new()
    {
        // p.300 (S3-52) Section B-B (read on the vector drawing): bottom face square to the web, 3'-3" long from the web inside face, its free (inner) end
        // 2'-8 1/4" below the deck top, inner face vertical (1'-5" high to the haunch underside), the bottom meets the web 9 3/4" lower; 1'-6" long, 2'-11"
        // from the BULKHEAD face (tip-side joint, user decision 2026-10-30). Bars 7'-9" / 9'-8" from CL girder (10'-7" to the web corner less 1'-11" + 11" / 11"),
        // 2'-0 1/2" / 2'-8 1/2" below the deck top.
        Name = "erection PT bar block Type A", Kind = SegmentalBlockKind.ErectionBarBlock, From = SegmentalJointRef.TipSide, Start = F(2, 11), Source = "p.300 (S3-52)",
        Description = "Temporary top PT bar block Type A (1 3/4\" bars)",
        Stations = new()
        {
            new() { Along = 0, BottomBelowDeck = F(2, 8.25), BottomSquareToWeb = F(3, 3) },
            new() { Along = F(1, 6), BottomBelowDeck = F(2, 8.25), BottomSquareToWeb = F(3, 3) },
        },
        Unresolved = U("p.300", "erection PT bar block Type A section", "Section B-B gives the inner corner 2'-8 1/4\" below the deck top, the 3'-3\" sloped bottom and 9 3/4\" / 1'-5\" heights; the bottom is drawn square to the web (1:4) - with it the drop to the web is 9 1/2\" and the inner face 1'-4 3/8\" (check rows), not the printed 9 3/4\" / 1'-5\"",
            "bottom square to the web 3'-3\" long with its free end 2'-8 1/4\" below the deck top, inner face vertical, top = the haunch underside."),
    },
    new()
    {
        // p.299 (S3-51) Section D-D: bar 6'-6" from CL girder; inner vertical face 9" inside the bar (5'-9" from CL), top 9" above the bar, bottom on the bottom
        // slab / fillet ("VARIES"), outer end against the web; "14'-6\" TO TOP SLAB" = bar to the DECK TOP (Section C-C: 4'-9" above the bent soffit + 14'-6"
        // = 19'-3" = the bent depth); 2'-0" long, 2'-11" from the bulkhead (tip-side) face of D1 / U1.
        Name = "erection PT bar block Type C", Kind = SegmentalBlockKind.ErectionBarBlock, From = SegmentalJointRef.TipSide, Start = F(2, 11), Source = "p.299 (S3-51)",
        Description = "Temporary bottom PT bar block Type C (D1 / U1)",
        Stations = new()
        {
            new() { Along = 0, InnerFromCl = F(5, 9), TopBelowDeck = F(14, 6) - In(9) },
            new() { Along = F(2), InnerFromCl = F(5, 9), TopBelowDeck = F(14, 6) - In(9) },
        },
        Unresolved = U("p.299", "erection PT bar block Type C height", "the bar is placed 14'-6\" below the deck top (the reading that closes Section C-C at the bent: 4'-9\" + 14'-6\" = 19'-3\"), but then the block face is about 3'-0\" high where Section D-D prints '1'-6 5/8\" (MIN.) AT FACE OF BLOCK' (CONFLICT, check row)",
            "inner face 5'-9\" from CL girder, top 13'-9\" below the deck top, down to the bottom slab / fillet, out to the web."),
    },
    new()
    {
        // p.302 (S3-54) Section along CL deviation block (spans 20-22), read on the drawing: the block face is parallel to the web, 1'-8" + 7" = 2'-3" normal to
        // the original web inside face (dashed); with the 1'-4" web that is 3'-7" total = the printed bottom width (and 44.4" horizontal at mid height, vector);
        // spans 23-28: 1'-6" + 3" = 1'-9" (3'-1" total). 3'-0" long centred on the CL closure joint (Section B-B: 1'-6" + 1'-6"); from the top slab haunch to the
        // bottom slab (the box void bounds it).
        Name = "deviation block spans 20-22", Kind = SegmentalBlockKind.DeviationBlock, From = SegmentalJointRef.Centre, Start = -F(1, 6), Source = "p.302 (S3-54)",
        Description = "Deviation block for future PT at the closure joint (web thickening 2'-3\", 3'-0\" long)",
        Stations = new() { new() { Along = 0, ThicknessFromWeb = F(2, 3) }, new() { Along = F(3), ThicknessFromWeb = F(2, 3) } },
        Unresolved = U("p.302", "deviation block section (spans 20-22)", "the printed chain 1'-8\" + 7\" is read as the thickening normal to the web (it reproduces the 3'-7\" bottom width with the 1'-4\" web); the dashed R transitions into the top haunch and the bottom fillet are drawn, not dimensioned",
            "web thickening 2'-3\" normal to the web inside face, bounded by the box void (haunch, fillets), 3'-0\" centred on the CL closure joint."),
    },
    new()
    {
        Name = "deviation block spans 23-28", Kind = SegmentalBlockKind.DeviationBlock, From = SegmentalJointRef.Centre, Start = -F(1, 6), Source = "p.302 (S3-54)",
        Description = "Deviation block for future PT at the closure joint (web thickening 1'-9\", 3'-0\" long)",
        Stations = new() { new() { Along = 0, ThicknessFromWeb = F(1, 9) }, new() { Along = F(3), ThicknessFromWeb = F(1, 9) } },
        Unresolved = U("p.302", "deviation block section (spans 23-28)", "the printed chain 1'-6\" + 3\" is read as the thickening normal to the web (it reproduces the 3'-1\" bottom width with the 1'-4\" web); the R transitions are drawn, not dimensioned",
            "web thickening 1'-9\" normal to the web inside face, bounded by the box void, 3'-0\" centred on the CL closure joint."),
    },
};

// ================================================================ 2. block placements
// p.285-296 (anchor-block-locations.csv): B_k anchors in segment k+4 of each arm toward a closure (spans 20-28: B1-B7 in 5-11), the end spans B1-B3 in 7-9;
// C1 in 10, C2 in 11; both webs. The layout sheets show bents 19, 20, 22, 24, 26, 28; the same pattern is used for 21, 23, 25, 27 (flag below).
var blocks = new List<SegmentalBlockPlacement>();
for (int bent = 19; bent <= 28; bent++)
    foreach (var arm in new[] { "D", "U" })
    {
        int span = arm == "D" ? bent : bent + 1;
        bool endSpan = span is 19 or 29;
        int nB = endSpan ? 3 : 7, first = endSpan ? 7 : 5;
        for (int k = 1; k <= nB; k++) blocks.Add(new() { Type = "bottom slab anchor block", Segment = $"{bent}-{arm}{first + k - 1}", Label = $"B{k} (span {span})", Source = "p.285-296" });
        // end spans: B3 starts in its own block in XX-13 (p.285 / p.296 B3 label on 19-D13 / 28-U13), anchor face toward the end segment
        if (endSpan) blocks.Add(new() { Type = "bottom slab anchor block (start)", Segment = $"{bent}-{arm}13", Label = $"B3 (span {span}) start", Source = "p.285, p.296" });
        blocks.Add(new() { Type = "top slab anchor block", Segment = $"{bent}-{arm}10", Label = $"C1 (span {span})", Source = "p.285-296" });
        blocks.Add(new() { Type = "top slab anchor block", Segment = $"{bent}-{arm}11", Label = $"C2 (span {span})", Source = "p.285-296" });
        blocks.Add(new() { Type = "erection PT bar block Type C", Segment = $"{bent}-{arm}1", Source = "p.299" });
    }
foreach (var s in segNames)
{
    blocks.Add(new() { Type = "erection PT bar block Type B", Segment = s, Source = "p.299" });
    blocks.Add(new() { Type = "erection PT bar block Type A", Segment = s, Source = "p.300" });
}
for (int span = 20; span <= 28; span++) blocks.Add(new() { Type = span <= 22 ? "deviation block spans 20-22" : "deviation block spans 23-28", Segment = $"CJ{span}", Label = $"span {span}", Source = "p.302" });
sg.Blocks = blocks;
{
    // the rule against the layout-sheet rows (end-segment rows: the end span B / C tendons anchor in the end segment diaphragm, no block)
    var locs = Csv("anchor-block-locations.csv").Where(r => !r[4].EndsWith("-ES")).ToList();
    int hit = locs.Count(r => blocks.Any(b => b.Segment == r[4] && b.Type.StartsWith(r[3].StartsWith("TOP") ? "top" : "bottom") && b.Label.StartsWith(r[2] + " ")));
    Print($"Anchor blocks: rule reproduces {hit} of {locs.Count} layout-sheet rows (p.285-296; rows next to an end segment are listed as UNRESOLVED in the data).");
}
sg.Unresolved.RemoveAll(u => u.Contains("[details]"));
// ================================================================ 2b. PT bars (p.299-300) and future-PT pipes (p.302, p.308)
// Erection bars between the blocks: top 2 per web at every joint (Type A, both ducts), bottom 1 per web per joint alternating between the two ducts (p.299
// STAGGERED pattern, 8" stagger), D1 - U1 through the bent segment (Type A top / Type C bottom; the drawn couplers are not modeled). Temporary bars are
// removed and reused after erection: every joint's bars are drawn.
const string TypicalRx = @"^\d+-[DU]\d+$";
string barFlag(string item, string why) => U("p.299-300", item, why, "Bars straight between the far faces of the two blocks; anchor plates, nuts, couplers not modeled.");
sg.PtBars = new List<SegmentalPtBarDefinition>
{
    new() { Name = "top temporary PT bars", Kind = SegmentalPtBarKind.Temporary, Segments = TypicalRx, Pairing = SegmentalBarPairing.NextTowardTip, BlockType = "erection PT bar block Type A",
        Positions = new() { new() { X = F(7, 9), Depth = F(2, 0.5) }, new() { X = F(9, 8), Depth = F(2, 8.5) } }, Diameter = In(1.75), DuctDiameter = In(2.75), PrintedMinLength = F(14),
        Material = "1 3/4\" dia. PT bar (temporary)", Source = "p.300 (S3-52)",
        Unresolved = barFlag("top temporary PT bar ends and second bar depth", "the anchor plates / nuts are drawn on the block faces without dimensions, and the 8\" under the first bar is read as the drop to the second bar") },
    new() { Name = "bottom temporary PT bars", Kind = SegmentalPtBarKind.Temporary, Segments = TypicalRx, Pairing = SegmentalBarPairing.NextTowardTip, BlockType = "erection PT bar block Type B",
        Positions = new() { new() { X = F(2, 6), Height = In(6.5) }, new() { X = F(3, 2), Height = In(6.5) } }, Alternate = true, Diameter = In(1.75), DuctDiameter = In(2.75), PrintedMinLength = F(20, 3),
        Material = "1 3/4\" dia. PT bar (temporary)", Source = "p.299 (S3-51)",
        Unresolved = barFlag("bottom temporary PT bar ends and stagger", "the staggered pattern is drawn (8\" stagger) without saying which duct the first joint of an arm takes; the bars alternate from the 2'-6\" duct at the first joint") },
    new() { Name = "top temporary PT bars D1-U1", Kind = SegmentalPtBarKind.Temporary, Segments = @"^\d+-D1$", Pairing = SegmentalBarPairing.AcrossBent, BlockType = "erection PT bar block Type A",
        Positions = new() { new() { X = F(7, 9), Depth = F(2, 0.5) }, new() { X = F(9, 8), Depth = F(2, 8.5) } }, Diameter = In(1.75), DuctDiameter = In(2.75),
        Material = "1 3/4\" dia. PT bar (temporary)", Source = "p.300 (S3-52)",
        Unresolved = barFlag("top temporary PT bars D1 - U1", "they run through the bent segment in coupled pieces (min. 11'-7\" / 14'-6\"); the coupler positions are not dimensioned") },
    new() { Name = "bottom temporary PT bars D1-U1", Kind = SegmentalPtBarKind.Temporary, Segments = @"^\d+-D1$", Pairing = SegmentalBarPairing.AcrossBent, BlockType = "erection PT bar block Type C",
        Positions = new() { new() { X = F(6, 6), Depth = F(14, 6) } }, Diameter = In(1.75), DuctDiameter = In(2.75),
        Material = "1 3/4\" dia. PT bar (temporary)", Source = "p.299 (S3-51)",
        Unresolved = barFlag("bottom temporary PT bars D1 - U1", "they run through the bent segment with a coupler (min. 15'-0\" pieces); the coupler position is not dimensioned") },
};
sg.Unresolved.Add("[details] " + U("p.299-300", "temporary PT bars between the bent segment and U1, and the permanent 1 3/4\" bent PT bars with their pockets", "their anchor plates sit in the bent segment at positions that are drawn only ('3'-0\" MIN.'), and the pocket outline of the permanent bars (Section C-C) is not dimensioned", "Not built."));

// 4" Sch. 40 galvanized steel pipe (p.302 note 1): ASME B36.10M OD 4.500", wall 0.237".
double pipeOd = In(4.5), pipeWall = In(0.237);
var pipes = new List<SegmentalPipeDefinition>();
double In2(string v) => In(double.Parse(v, CultureInfo.InvariantCulture));
double Ft2(string v) => Ft(double.Parse(v, CultureInfo.InvariantCulture));
foreach (var r in Csv("deviation-pipes.csv"))
{
    // SPAN 20 / SPANS 21-22 / SPAN 23 / SPANS 24-27 / SPAN 28; P1-LEFT / P2-RIGHT / P1 (both sides)
    var nums = System.Text.RegularExpressions.Regex.Matches(r[0], @"\d+").Select(m => int.Parse(m.Value)).ToList();
    var spans = nums.Count == 2 ? Enumerable.Range(nums[0], nums[1] - nums[0] + 1).ToList() : nums;
    string tendon = r[1].Split(' ', '-')[0];
    int side = r[1].Contains("LEFT") ? -1 : r[1].Contains("RIGHT") ? 1 : 0;
    pipes.Add(new()
    {
        Name = $"deviation pipe {tendon}", Kind = SegmentalPipeKind.DeviationPipe, Placement = SegmentalPipePlacement.PiAtSegmentCentre,
        Segments = spans.Select(n => $"CJ{n}").ToList(), Side = side,
        A = In2(r[2]), B = In2(r[3]), C = In2(r[4]), D = In2(r[5]), F = In2(r[6]), G = In2(r[7]), DeltaDeg = double.Parse(r[8], CultureInfo.InvariantCulture),
        Radius = Ft2(r[9]), L1 = Ft2(r[10]), L2 = Ft2(r[11]), L3 = Ft2(r[12]), Length = Ft2(r[13]), OutsideDiameter = pipeOd, Wall = pipeWall,
        Material = "4\" Sch. 40 galvanized steel pipe (future PT)", Source = $"p.302 (S3-54) {r[0]}",
        Unresolved = U("p.302", "deviation pipe ends", "the table gives the CL heights A / C (above the soffit) and offsets D / G at the pipe ends; A / D are taken at the down-station end (Section B-B: DIM. A left, direction of stationing to the right)", "P.I. on the CL closure joint, legs L1 / L3 + R tan(delta/2)."),
    });
}
foreach (var r in Csv("future-pt-pipes.csv"))
{
    var nums = System.Text.RegularExpressions.Regex.Matches(r[0], @"\d+").Select(m => int.Parse(m.Value)).ToList();
    var bents = nums.Count == 2 ? Enumerable.Range(nums[0], nums[1] - nums[0] + 1).ToList() : nums;
    string tendon = r[1];
    int side = r[2] == "LEFT" ? -1 : r[2] == "RIGHT" ? 1 : 0;
    pipes.Add(new()
    {
        Name = $"future PT pipe {tendon}", Kind = SegmentalPipeKind.FuturePtPipe, Placement = SegmentalPipePlacement.OuterEndAtBentHalfFace,
        Bents = bents.Select(n => $"Bent {n}").ToList(), Side = side, UpStationHalf = tendon is "F1" or "F3", HeightsBelowDeck = true,
        A = In2(r[3]), B = In2(r[4]), C = In2(r[5]), D = In2(r[6]), F = In2(r[7]), G = In2(r[8]), DeltaDeg = double.Parse(r[9], CultureInfo.InvariantCulture),
        Radius = Ft2(r[10]), L1 = Ft2(r[11]), L2 = Ft2(r[12]), L3 = Ft2(r[13]), Length = Ft2(r[14]), OutsideDiameter = pipeOd, Wall = pipeWall,
        Material = "4\" galvanized steel pipe (future PT)", Source = $"p.308 (S3-60) bent {r[0]}",
        Unresolved = U("p.308", "future PT pipe position along the bent segment", "Detail A draws the pipe's outer (L1) end at the end face of the bent segment half without a dimension (a bent half has no bulkhead face farther from the pier to measure from); the half each pipe lies in is read from Detail A - Plan (F1 / F3 anchors down-station, P.I. up-station) and the wall from the p.302 Sch. 40 note",
            "outer end on the bent half's end face, DIM. A / B / C below the deck top, D / F / G from CL girder."),
    });
}
sg.Pipes = pipes;

// ================================================================ 2b'. PT bars fixed in one segment (agent review read qc/bonner-unit5/vertical-pt: vector linework + text layer)
// p.278 (S3-30) bent segment: VERTICAL PT BAR 1 3/4" Gr. 150 in 2 3/4" O.D. ducts, 4 per half (Section A-A half plans: 2 each side of CL), 3'-8" and 6'-0" from CL
// girder (3'-8" + 2'-4"), plumb (square to the deck top), 2'-6" from the CL split (Section B-B 1'-6" + 1'-0"); top anchor plate face 10 1/2" below the deck top
// (stressing pocket, TOP detail), bottom (dead end) plate face 6 1/2" above the soffit (BOTTOM detail); 17'-10" plate to plate (table). LONGITUDINAL PT BAR 3'-4" /
// 6'-4" from CL, 1'-5" above the bent soffit, through the split, plate faces 8 1/2" inside each bulkhead face (+-5'-0"). p.272 (S3-24) end segment: 4 per segment
// in the webs, inclined with the web (8'-11" / 10'-3 3/4" from CL at the top, 7'-0" / 8'-4 3/4" at the bottom), 2'-0" from the closure-joint face, anchors per the
// p.278 details (note 7); 7'-10" (table).
sg.FixedBars = new()
{
    new() { Name = "vertical PT bar", Kind = SegmentalFixedBarKind.Vertical, SegmentType = SegmentType.BentHalf, From = SegmentalFaceRef.SupportSide,
        Positions = { new() { Along = F(2, 6), X = F(3, 8) }, new() { Along = F(2, 6), X = F(6) } }, TopDepth = In(10.5), BottomHeight = In(6.5),
        Diameter = In(1.75), DuctDiameter = In(2.75), ListedLength = F(17, 10), PlanCount = 18 * 4, Material = "1 3/4\" PT bar, Grade 150", Source = "p.278 (S3-30)",
        Unresolved = U("p.278", "bent segment vertical PT bars", "PLAN CONFLICT: the stressing table lists 18 segments (72 bars) while the 10 cantilever bents have 20 bent halves with 4 bars each (Section A-A half plans); anchor plate / pocket sizes are drawn, not dimensioned",
            "4 bars in every bent half (80), plate face to plate face as dimensioned (count check row)."),
    },
    new() { Name = "vertical PT bar", Kind = SegmentalFixedBarKind.Vertical, SegmentType = SegmentType.End, From = SegmentalFaceRef.FarSide,
        Positions = { new() { Along = F(2), X = F(8, 11), XBottom = F(7) }, new() { Along = F(2), X = F(10, 3.75), XBottom = F(8, 4.75) } }, TopDepth = In(10.5), BottomHeight = In(6.5),
        Diameter = In(1.75), DuctDiameter = In(2.75), ListedLength = F(7, 10), PlanCount = 2 * 4, Material = "1 3/4\" PT bar, Grade 150", Source = "p.272 (S3-24), p.278",
        Unresolved = U("p.272, 278", "end segment vertical PT bars", "p.272 does not dimension the anchor levels (note 7 refers to the p.278 bar details: 10 1/2\" / 6 1/2\") and draws only one end segment (29-ES taken as the opposite hand from its own closure-joint face)",
            "2 bars per web, parallel to the web, 2'-0\" from the closure-joint face, anchor levels of p.278."),
    },
    new() { Name = "longitudinal PT bar", Kind = SegmentalFixedBarKind.Longitudinal, SegmentType = SegmentType.BentHalf, From = SegmentalFaceRef.SupportSide,
        Positions = { new() { Along = 0, AlongEnd = F(5) - In(8.5), X = F(3, 4) }, new() { Along = 0, AlongEnd = F(5) - In(8.5), X = F(6, 4) } }, BottomHeight = F(1, 5),
        Diameter = In(1.75), PlanCount = 9 * 4 * 2, Material = "1 3/4\" PT bar, Grade 150", Source = "p.278 (S3-30), p.299 (S3-51)",
        Unresolved = U("p.278, 299", "bent segment longitudinal PT bars", "PLAN CONFLICT: the table lists 8'-3\" and 9 bent segments, but the plates 8 1/2\" inside the +-5'-0\" bulkhead faces give 8'-7\" (p.299 minimum bar 9'-11 1/2\" = 8'-7\" + 2 x 8 1/4\" agrees) and there are 10 bents; one bar runs through both halves",
            "Each bar is built as two pieces, split face to plate face in each half (8'-7\" plate to plate); 4 bars per bent (count check row counts pieces against 2 x 36)."),
    },
};

// ================================================================ 2c. tendon lanes and deviation profiles (p.283, p.285-296, p.297, p.303-304)
// Lanes = the duct holes of the joint faces (S3-35 p.283: top slab rows A / B ducts 1-11, bottom slab row C ducts 1-7; row C 5" above the soffit as in the
// build script). The lane of every T / B tendon at every joint is read from the VECTOR layout plans by tools/bonner_tendon_lanes.py -> tendon-lanes.csv
// (bents 21 / 23 / 25 / 27 take the sheet of bent 20 / 22 / 24 / 26, like the anchor blocks). p.303: cantilever anchors 9" inside the tip (bulkhead) face,
// 1'-1 1/2" below the deck top (Section B-B), P.I. 9'-0" beyond the anchor, lane changes between P.I.s 3'-6" from both faces, R = 35'-0" (note 3: minimum).
// p.297: bottom slab anchor at the block's anchor face, 1'-1" above the slab top, 1'-7" from the web face at the block top (2'-0" above the slab), P.I. 6'-0"
// from the anchor, R = 23'-0".
{
    double[] rowAx = { F(15, 8.5), F(14, 11.5), F(14, 2.5), F(13, 5.5), F(12, 4.5), F(11, 3.5), F(10, 2.5), F(9, 1.5), F(8, 4.5), F(7, 7.5), F(6, 10.5) };
    var duct = new List<SegmentalDuctLane>();
    for (int i = 0; i < 11; i++)
    {
        duct.Add(new() { Name = $"A{i + 1}", X = rowAx[i], Depth = In(7.25) });
        duct.Add(new() { Name = $"B{i + 1}", X = rowAx[i], Depth = In(16.25) });
    }
    // row C: 1'-6" from the soffit corner, then 9" (p.304 plans dimension the lanes from the web side; the box narrows toward the bents)
    for (int i = 0; i < 7; i++) duct.Add(new() { Name = $"C{i + 1}", X = F(1, 6) + In(9) * i, Height = In(5), FromSoffitCorner = true });
    sg.DuctLanes = duct;
    var laneRows = Csv("tendon-lanes.csv");
    int SheetBent(int b) => b is 21 or 23 or 25 or 27 ? b - 1 : b;
    var tProfile = new SegmentalTendonProfile
    {
        AnchorLane = "A7", AnchorSetback = In(9), AnchorDepth = F(1, 1.5), AnchorPiFromFace = In(9) + F(9), AnchorRadius = F(35), ChangePiFromFace = F(3, 6), ChangeRadius = F(35),
        // p.303: one-lane steps in consecutive segments are one straight 3.07 deg diagonal (9" per 14'-0") through the holes: anchor P.I. on it (2 3/4" off the lane
        // = 4'-3" x tan 3.07 deg), last segment P.I. 3'-6" from its tip face on it (2 1/4"), then 3'-6" from its bent face in the lane
        ChainAngleDeg = 3.07,
        // the diagonal is dimensioned for T7-T12 only; T13 is drawn as a lane-parallel S-curve with both P.I.s on their lanes (p.303 TENDON T13 plan)
        ChainTendons = "T7,T8,T9,T10,T11,T12",
        // p.303 plans: straight from the anchor at the printed exit angle to the P.I. 9'-0" beyond it (5.40 deg x 9'-0" = 10.21" + 2 3/4" on the 3.07 deg
        // diagonal = 1'-1" = one 13" lane; 6.86 deg x 9'-0" = 1'-1" = the next lane at the P.I.)
        AnchorExits = new() { new() { Tendons = "T7,T8,T9,T10,T11,T12", AngleDeg = 5.40, Source = "p.303 (T8, T10, T12 / T7, T9, T11)" },
                              new() { Tendons = "T3,T4,T5,T6,T14", AngleDeg = 6.86, Source = "p.303 (T3, T5 / T4, T6, T14)" } },
        Source = "p.303 (S3-55), lanes p.285-296",
        Unresolved = U("p.285-296, 303", "cantilever tendon lanes and lane changes", "the lanes are read from the layout plans' vector lines (each tendon traced along its drawn line from the bent label; joint = the lane ticks / segment label centre); joints a sheet does not let us read (anchor ends of the curved sheets p.285-289) take the lane of the other arm or of the cantilevers with the same anchor segments where those agree (tagged MATCHED in tendon-lanes.csv), row B ducts are read at the bent only; PLAN CONFLICT (names only): the p.303 plan figures label the T3 / T5 / T8 / T10 / T12 anchor 'P1' and the T7 / T9 / T11 anchor 'P2', but their lane ticks and the p.285-296 layouts put them at 12'-4 1/2\" (A5) / 10'-2 1/2\" (A7) from the CL, which Section B-B calls P2 / P1 - the lane positions are modeled; the 3'-6\" lane-change P.I.s are dimensioned for T7-T13 and used for every lane change (where R = 35'-0\" does not fit before the next joint: CONFLICT row per tendon); bents 21, 23, 25, 27 are not drawn",
            "Ducts through their lane at every joint, lane changes inside the segment between P.I.s 3'-6\" from its faces, R = 35'-0\"; anchors 9\" inside the tip face 1'-1 1/2\" below the deck top."),
    };
    // p.304 continuity tendon plans ("TOWARD BENT" = left in every figure): lane changes between P.I.s 4'-0" from the bent-side face and 3'-0" from the
    // bulkhead face (B2 / B3, B3 / B4 and B4 / B5 figures), R = 22'-0" (TYP.); 5.00 deg straight face crossings for B2 / B4; anchor deviation p.297.
    var bProfile = new SegmentalTendonProfile
    {
        AnchorBlockType = "bottom slab anchor block", AnchorFromWeb = F(1, 7), AnchorWebLevelAboveSlab = F(2), AnchorAboveSlab = F(1, 1), AnchorPiFromAnchor = F(6), AnchorRadius = F(23),
        ChangePiFromFace = F(3), ChangePiFromBentFace = F(4), ChangeRadius = F(22), Source = "p.297 (S3-49), p.304 (S3-56), lanes p.285-296",
        // p.304 figures matched to their segments by the lane pattern (the figures do not name the segment): one-lane changes of B1 (XX-D6 / U6, lane 2 -> 1)
        // and B6 (XX-D11 / U11, lane 2 -> 3) have their P.I.s 2'-6" and 7'-0" from the bent-side face
        LaneChangePis = new() { new() { Tendons = "B1", Segment = 6, FirstPiFromBentFace = F(2, 6), SecondPiFromBentFace = F(7), Source = "p.304 CONTINUITY TENDON - PLAN (TENDON B1)" },
                                new() { Tendons = "B6", Segment = 11, FirstPiFromBentFace = F(2, 6), SecondPiFromBentFace = F(7), Source = "p.304 CONTINUITY TENDON - PLAN (TENDON B6)" },
                                // first P.I. of the B2 / B4 figures whose far face is a 5.00 deg crossing
                                new() { Tendons = "B2", Segment = 7, FirstPiFromBentFace = F(6, 6), Source = "p.304 (TENDON B2 - SPAN 20 TO SPAN 28)" },
                                new() { Tendons = "B4", Segment = 9, FirstPiFromBentFace = F(6, 6), Source = "p.304 (TENDONS B2, B3 AND B4)" } },
        // p.304: B2 crosses joints 7 / 8 and B4 joint 9 on a straight 5.00 deg diagonal through the hole; P.I. 2'-6" before the face (2 5/8" = 2'-6" x tan 5 deg
        // off the lane) and after it where the line meets the next lane: 14'-0" - 6'-4 5/8" = 7'-7 3/8" (B2 / XX-8), 14'-0" - 5'-5 1/8" = 8'-6 7/8" (B2 / XX-9,
        // = 9" / tan 5 deg, one lane), 8'-7" (B4 / XX-10, = 9" / tan 5 deg)
        FaceCrossings = new() { new() { Tendons = "B2", Joint = 7, AngleDeg = 5, PiBeforeFace = F(2, 6), PiAfterFace = F(7, 7.375), Source = "p.304 B2 / B2 + B3 figures" },
                                new() { Tendons = "B2", Joint = 8, AngleDeg = 5, PiBeforeFace = F(2, 6), PiAfterFace = F(8, 6.875), Source = "p.304 B2 + B3 / B2, B3 + B4 figures" },
                                new() { Tendons = "B4", Joint = 9, AngleDeg = 5, PiBeforeFace = F(2, 6), PiAfterFace = F(8, 7), Source = "p.304 B2, B3 + B4 / B4 + B5 figures" } },
        Unresolved = U("p.297, 304", "bottom continuity tendon lane changes and blister deviation", "p.304 prints the lane-change P.I.s per tendon in figures that do not name their segment; the 4'-0\" (from the bent face, 'TOWARD BENT' side) / 3'-0\" (from the bulkhead face) pair of the B2 / B3 and B4 / B5 figures is used for every other change (matched to segments by the lane pattern: B1 in XX-6 and B6 in XX-11 use their 2'-6\" / 7'-0\" pairs; B3 in XX-8 / XX-9, B5 in XX-10 = 4'-0\" / 3'-0\"; B2 in XX-7..9 and B4 in XX-9 / XX-10 cross the joint faces at 5.00 deg on a straight diagonal (P.I. 2'-6\" before the face, 2 5/8\" = 2'-6\" x tan 5 deg off the lane, and where the line meets the next lane after it) - modeled as such (FaceCrossings), the printed P.I. distances after the face equal 9\" / tan 5 deg), p.297 shows the duct straight in plan inside the block (the deviation is vertical: P.I. 6'-0\" from the anchor, R = 23'-0\", anchor face toward the bent, tendon leaving toward the closure), the anchor face is 'VARIES' (square to the tendon), and joints a sheet does not let us read (all of p.295 28-D, parts of p.286 / 288 / 292) take the lane pattern every fully read interior cantilever shares (tagged TYPICAL in tendon-lanes.csv)",
            "Lanes per joint from the layout plans; from the anchor on the block face the duct reaches its tip-face lane at a P.I. 6'-0\" from the anchor (R = 23'-0\"), so it crosses every joint straight in its lane; curves that do not fit = CONFLICT rows."),
    };
    // closure tendons C1 / C2 (spans 20-28): top slab anchor blocks in XX-10 / XX-11 (p.285-296), both in duct location 8 (9'-1 1/2" from the CL girder:
    // p.290 plan lines and the 'C1 | C2' text on the lane-8 row, same on p.285-296). p.298 elevation: anchor 3'-4" from the bent-side face, 2'-7 1/4" below the
    // deck top, 8'-10 1/2" from the CL girder (Section A-A), P.I. 5'-0" from the anchor at the row B level, R = 16'-0". C2 stays in row B (note 1); C1 is in row B
    // at the anchor and in row A from segment XX-11 on (p.290 elevation P.I.s, dashed / solid plan lines): the rise 1'-4 1/4" -> 7 1/4" between P.I.s 3'-3"
    // from each face of XX-11, R = 50'-0" (p.304 TOP SLAB TENDON - ELEVATION)
    var cProfile = new SegmentalTendonProfile
    {
        AnchorBlockType = "top slab anchor block", AnchorBelowTop = F(2, 7.25), AnchorFromCl = F(8, 10.5), AnchorFromBentFace = F(3, 4), AnchorPiFromAnchor = F(5), AnchorRadius = F(16),
        ChangeRadius = F(50), ChangePiFromFace = F(3, 3), ChangePiFromBentFace = F(3, 3), Source = "p.290, p.298 (S3-50), p.304 (S3-56), lanes p.285-296",
        Unresolved = U("p.290, 304", "closure tendon C1 row change", "PLAN CONFLICT: the p.304 TOP SLAB TENDON - ELEVATION draws row A on the 'TOWARD BENT' side of the change, but p.290 (elevation P.I.s, dashed / solid plan lines) puts C1 in row B at its anchor (bent) side and in row A toward the closure, and p.304 names no segment",
            "p.290 is modeled: C1 in row B at the XX-10 tip face, row A from the XX-11 tip face on (P.I.s 3'-3\" from both faces of XX-11, R = 50'-0\"); C2 in row B; both in lane 8."),
    };
    int withC = 0;
    foreach (var t in sg.Tendons.Where(t => t.Group == TendonGroup.Closure && t.Layout == TendonLayout.CentredOnClosure))
    {
        int aIdx = t.Name == "C1" ? 10 : 11;
        t.Lanes = new();
        foreach (var arm in new[] { "U", "D" }) for (int k = aIdx; k <= 20; k++) t.Lanes.Add(new() { Arm = arm, Joint = k, Lane = t.Name == "C1" && k > 10 ? "A8" : "B8" });
        t.Profile = cProfile.Clone(); withC++;
    }
    Print($"Closure tendons C1 / C2 with profiles: {withC}");
    // end-span C1 / C2 (spans 19 / 29): lanes read on p.285 / p.296 (vector; C1 lane 10 row A at the end segment to joint 12, lane 8 from joint 11, row B at joint 10;
    // C2 lane 8 row B). p.272 SECTION / Section A-A: anchors on the end segment anchor face 6'-6" above the soffit (1'-7" + 1'-6" + 1'-6" + 1'-11"), C2 / C1 9'-1 1/2" /
    // 7'-7 1/2" from the CL (= lanes 8 / 10); 3" galvanized pipes rising to their bend P.I. 3'-10" from the anchor face: C1 (upper, row A) R = 10'-0" 26 deg, C2 (lower,
    // row B) R = 12'-0" 16 deg. p.304 TOP SLAB TENDON - PLAN (C1 - SPAN 19 AND 29): lane 10 -> 8 in XX-12, P.I.s 3'-0" from the bent-side face and 4'-0" from the bulkhead
    // face ('TOWARD BENT 18 OR BENT 29' side), R = 22'-0"; the row change in XX-11 as in spans 20-28
    var ecProfile = cProfile.Clone();
    ecProfile.EndFaceAnchors = new()
    {
        new() { Tendons = "C1", FromCl = F(7, 7.5), AboveSoffit = F(6, 6), SpanFaceFromCl = F(7, 7.5), BendRadius = F(10), BendDeg = 26, BendPiFromAnchor = F(3, 10), Source = "p.272 (C1)" },
        new() { Tendons = "C2", FromCl = F(9, 1.5), AboveSoffit = F(6, 6), SpanFaceFromCl = F(9, 1.5), BendRadius = F(12), BendDeg = 16, BendPiFromAnchor = F(3, 10), Source = "p.272 (C2)" },
    };
    ecProfile.LaneChangePis = new() { new() { Tendons = "C1", Segment = 12, FirstPiFromBentFace = F(3), SecondPiFromBulkheadFace = F(4), Radius = F(22), Source = "p.304 TOP SLAB TENDON - PLAN (C1 - SPAN 19 AND 29)" } };
    ecProfile.Source = "p.272, p.285 / p.296 (lanes), p.298, p.304";
    ecProfile.Unresolved = U("p.285, 296, 304", "end-span closure tendons C1 / C2", "PLAN CONFLICT (as in spans 20-28): the p.304 C1 elevation puts row A on its 'TOWARD BENT' side while p.285 / p.296 put C1 in row A on the end segment side and row B at its anchor; the p.285 / p.296 plan line styles swap between C1 and C2 where they share lane 8 in XX-11 / XX-12 (the elevations, labels and anchor blocks decide); the layout sheets draw the XX-12 P.I.s about 3.4 / 3.6 ft from the faces (schematic)",
        "p.285 / p.296 elevations and labels modeled: C1 row A from the end segment, lane 10 -> 8 in XX-12 at the p.304 P.I.s, row A -> B in XX-11 (3'-3\" P.I.s, R = 50'-0\"); C2 lane 8 row B; p.272 anchors and pipe bends.");

    // end-span continuity tendons B1-B3 (spans 19 / 29): lanes traced on p.285 / p.296 from the start symbol to the anchor (= anchor-block-locations.csv);
    // B1 / B2 start on the end segment anchor face (p.272 SECTION: 8'-0" / 6'-6" from the CL girder, 1'-7" above the soffit, leaving the span face at
    // 8'-4 3/4" / 7'-0"; SECTION A-A galvanized pipe, bend R = 10'-0" at 18 deg), B3 at its bottom slab anchor block in XX-13; anchors in XX-7 / 8 / 9
    var eProfile = bProfile.Clone();
    eProfile.EndFaceAnchors = new()
    {
        // p.272 Section A-A: the pipe falls from the anchor (1'-7" above the soffit) to its bend P.I. 3'-6" from the anchor at the row C level, 18 deg, R = 10'-0"
        new() { Tendons = "B1", FromCl = F(8), AboveSoffit = F(1, 7), SpanFaceFromCl = F(8, 4.75), BendRadius = F(10), BendDeg = 18, BendPiFromAnchor = F(3, 6), Source = "p.272 (B1)" },
        new() { Tendons = "B2", FromCl = F(6, 6), AboveSoffit = F(1, 7), SpanFaceFromCl = F(7), BendRadius = F(10), BendDeg = 18, BendPiFromAnchor = F(3, 6), Source = "p.272 (B2)" },
    };
    // p.304 CONTINUITY TENDON - PLAN (TENDON B1, span 19 to span 29): its lane changes have the P.I.s 2'-6" / 7'-0" from the bent-side face in every span
    eProfile.LaneChangePis!.Add(new() { Tendons = "B1", Segment = 0, FirstPiFromBentFace = F(2, 6), SecondPiFromBentFace = F(7), Source = "p.304 CONTINUITY TENDON - PLAN (TENDON B1)" });
    eProfile.Unresolved = U("p.272, 283, 285, 296", "end-span tendon B1 / B2 pipe in the end segment", "PLAN CONFLICT: the p.272 span-face offsets (B1 8'-4 3/4\", B2 7'-0\" from the CL) do not lie on the lane holes of the first match-cast joint (p.283 / p.285 / p.296: 8'-9\" / 8'-0\" / 6'-6\"), and no detail bends the duct between them",
        "Pipe straight from the anchor to its bend P.I. 3'-6\" from the anchor at the lane height of the closure joint hole (R = 10'-0\", check row vs the printed 18 deg), straight in plan to the span face offset, then to the joint hole (the direction change at the span face = a curve of the same radius); lanes per joint and anchors as traced on p.285 / p.296.");
    int withLanes = 0;
    foreach (var t in sg.Tendons.Where(t => t.Group == TendonGroup.Cantilever || (t.Group == TendonGroup.Bottom && t.Layout is TendonLayout.CentredOnClosure or TendonLayout.FromEndSegment)
        || (t.Group == TendonGroup.Closure && t.Layout == TendonLayout.FromEndSegment)))
    {
        var list = new List<SegmentalTendonLane>();
        if (t.Group == TendonGroup.Cantilever)
        {
            int bent = int.Parse(t.Location.Substring(5));
            foreach (var r in laneRows.Where(r => int.Parse(r[0]) == SheetBent(bent) && r[2] == t.Name && r[4] != "?"))
                list.Add(new() { Arm = r[1], Joint = int.Parse(r[3]), Lane = r[5] + r[4] });
            if (list.Count > 0) { t.Lanes = list; t.Profile = tProfile.Clone(); withLanes++; }
        }
        else
        {
            int span = int.Parse(t.Location.Substring(5));
            foreach (var r in laneRows.Where(r => r[2] == t.Name && r[4] != "?" && ((r[1] == "U" && int.Parse(r[0]) == SheetBent(span - 1)) || (r[1] == "D" && int.Parse(r[0]) == SheetBent(span)))))
                list.Add(new() { Arm = r[1], Joint = int.Parse(r[3]), Lane = (t.Group == TendonGroup.Closure ? r[5] : "C") + r[4] });
            if (list.Count > 0) { t.Lanes = list; t.Profile = (t.Group == TendonGroup.Closure ? ecProfile : t.Layout == TendonLayout.FromEndSegment ? eProfile : bProfile).Clone(); withLanes++; }
        }
    }
    Print($"Tendon lanes: {withLanes} tendon designations with lanes / profiles ({laneRows.Count} lane rows from tendon-lanes.csv).");
}
sg.Unresolved.Add("[details] " + U("p.285-296", "anchor block pattern at bents 21, 23, 25 and 27", "the layout sheets show bents 19, 20, 22, 24, 26 and 28 only (the key elevations that name the other bents were not read)", "The identical pattern of every sheet is used (B_k in segment k+4, end spans k+6; C1 / C2 in 10 / 11)."));

// ================================================================ 3. openings (openings.csv: p.263-266, 271, 277, 315, 316, 321)
sg.Openings = new List<SegmentalOpeningDefinition>
{
    new() { Name = "A.O. access opening", Kind = SegmentalOpeningKind.BottomSlab, Width = F(3), Length = F(4), Source = "p.316 / 321 (size), p.263-266 (labels)",
        Segments = new() { "19-D13", "19-U9", "20-D12", "20-U9", "21-D12", "22-D12", "23-D12", "24-D12", "25-D12", "26-D12", "26-U9", "27-D12", "28-D12", "28-U13" },
        Unresolved = U("p.263-266", "A.O. locations in spans 22-25 and 27", "those spans show one A.O. label (D12) where spans 20, 21 and 26 show two (U9 + D12)", "Built as labelled; the hinged door / frame are not modeled.") },
    new() { Name = "T.A.O. temporary access opening", Kind = SegmentalOpeningKind.TopSlab, Width = F(3, 6), Length = F(2, 6), Temporary = true, Source = "p.321 (size, note 2), p.263-266 (labels)",
        Segments = new() { "19-D2", "20-D2", "21-D2", "22-D2", "23-D2", "23-U2", "24-U2", "25-U2", "26-U2", "27-U2", "28-U2" } },
    new() { Name = "A.O.B. access opening at the bent", Kind = SegmentalOpeningKind.BottomSlab, Diameter = F(3), Source = "p.277 / 315",
        Bents = Enumerable.Range(19, 10).Select(n => $"Bent {n}").ToList() },
    new() { Name = "bent diaphragm opening", Kind = SegmentalOpeningKind.BentDiaphragm, Source = "p.277 (S3-29)",
        Outline = new() { new(-In(30), 0), new(In(30), 0), new(In(30), In(96)), new(In(15), In(120)), new(-In(15), In(120)), new(-In(30), In(96)) },
        Bents = Enumerable.Range(19, 10).Select(n => $"Bent {n}").ToList() },
    new() { Name = "utility diaphragm opening", Kind = SegmentalOpeningKind.BentDiaphragm, OutlineFromTop = true, Source = "p.277 (S3-29)",
        Outline = new() { new(-In(4), In(25)), new(In(4), In(25)), new(In(8), In(29)), new(In(8), In(46)), new(In(4), In(50)), new(-In(4), In(50)), new(-In(8), In(46)), new(-In(8), In(29)) },
        Bents = Enumerable.Range(19, 10).Select(n => $"Bent {n}").ToList() },
};
sg.Unresolved.Add("[details] " + U("p.271, 277", "end segment diaphragm opening, bearing pintle blockouts", "the end segment is modeled as a solid envelope (its opening width 10'-2\" is scaled) and the pintles go through the bent soffit with the bearing details", "Not built. (Vent holes, survey blockouts, deck drains, access doors, vermin screens, solar platforms and box lighting are built by misc-bonner-unit5.csx.)"));
sg.Unresolved.Add("[details] " + U("p.392, 263-266", "navigational lights and their conduit runs", "the lights, junction boxes and cable are by others and located only relative to the navigation channel, whose CL station is not given; only the cast-in conduit sleeves are dimensioned", "Not built (sleeves only, in misc-bonner-unit5.csx)."));

// ================================================================ 4. parapet (parapet.csv: p.323-327)
sg.Parapet = new SegmentalParapetDefinition
{
    InsideOffset = F(20), Width = F(1, 2), Height = F(2, 6), Chamfer = In(0.75), JointSpacing = F(30), JointWidth = In(0.5), Material = "Class AA concrete (cast after all PT)",
    Source = "p.327 (S3-79), joints p.323-326",
    // 1/2" joints (p.323-326 parapet plans): per span "end + n SPA. @ 30'-0\" + end" along the THEORETICAL EDGE OF DECK (21'-3 1/2" from -L- = 20'-0" + 1'-2" + 1 1/2"
    // setback, p.327) between the bent control lines, equal ends; spans 21 / 22 'similar' to 20 and 25-28 'similar' to 24 (chains not printed for them).
    JointEdgeOffset = F(21, 3.5),
    JointRuns = new()
    {
        new() { From = "Bent 18", To = "Bent 19", Spaces = 5, PrintedArcA = F(201, 6.125), PrintedArcB = F(198, 5.875), PrintedEndA = F(25, 9.0625), PrintedEndB = F(24, 2.5), Source = "p.323" },
        new() { From = "Bent 19", To = "Bent 20", Spaces = 10, PrintedArcA = F(352, 7.75), PrintedArcB = F(347, 4.25), PrintedEndA = F(26, 3.875), PrintedEndB = F(23, 8.125), Source = "p.324" },
        new() { From = "Bent 20", To = "Bent 21", Spaces = 10, Source = "p.324 (span 21 similar to span 20)" },
        new() { From = "Bent 21", To = "Bent 22", Spaces = 10, Source = "p.324 (span 22 similar to span 20)" },
        new() { From = "Bent 22", To = "Bent 23", Spaces = 10, PrintedArcA = F(350, 6.75), PrintedArcB = F(349, 5.25), PrintedEndA = F(25, 3.375), PrintedEndB = F(24, 8.625), Source = "p.325" },
        new() { From = "Bent 23", To = "Bent 24", Spaces = 10, PrintedArcA = F(350), PrintedEndA = F(25), Source = "p.326" },
        new() { From = "Bent 24", To = "Bent 25", Spaces = 10, Source = "p.326 (span 25 similar to span 24)" },
        new() { From = "Bent 25", To = "Bent 26", Spaces = 10, Source = "p.326 (span 26 similar to span 24)" },
        new() { From = "Bent 26", To = "Bent 27", Spaces = 10, Source = "p.326 (span 27 similar to span 24)" },
        new() { From = "Bent 27", To = "Bent 28", Spaces = 10, Source = "p.326 (span 28 similar to span 24)" },
        new() { From = "Bent 28", To = "Bent 29", Spaces = 5, PrintedArcA = F(200), PrintedEndA = F(25), Source = "p.326" },
    },
    // The 2-bar metal rail (S0-36 / S0-37, p.37-38) and the joint end posts are built by misc-bonner-unit5.csx.
    Unresolved = U("p.327", "parapet joints at the bents", "the joints beside the bent / modular-joint end posts ('EXPANSION JOINTS', p.327) are not dimensioned",
        "1'-2\" x 2'-6\" parapet, plumb, inside face 20'-0\" from -L-, broken by the 1/2\" joints of the 30'-0\" chains (equal end lengths along the theoretical edge of deck); continuous through the bents (rail and end posts: misc-bonner-unit5.csx)."),
};

// ================================================================ 4b. match-cast joint keys (p.283 'BULKHEAD AND SHEAR KEY DETAILS', keys.csv, web-shear-keys.csv)
// Section C-C: the segment face cast against the bulkhead is FEMALE, so the male keys are on the match-cast face = the face toward the cantilever bent of every
// segment beyond the bent. Profile (B-B / A-A / C-C): 1 1/4" deep, 3 7/8" at the base, 2 5/8" flat, R = 1" transitions (not modeled: straight 5/8" flanks).
// Web keys: 'A' male keys @ 6 1/2" = 'B', 1'-2" across the web, 'C' at the bottom of the key zone; C read as along the web from the soffit (outside corner) to the
// first key centre - with it every row ends at the same depth (2'-7 7/8" below the deck top, rows 1-12 and the bent control line), which supports the reading.
var keyRows = new List<SegmentalWebKeyRow>();
foreach (var r in Csv("web-shear-keys.csv"))
    keyRows.Add(new SegmentalWebKeyRow { Section = r[0].StartsWith("BENT") ? 0 : int.Parse(r[0]), Count = int.Parse(r[1]), Zone = Len(r[2]), BottomDistance = Len(r[3]) });
string keyFlag(string item, string why) => U("p.283", item, why, "Built as listed; the segment solids are not cut (male = female volume).");
sg.Reported.RemoveAll(r => r.Contains("[details]"));
sg.Reported.Add("[details] CONFLICT (p.323): span 19 parapet chain on the second edge: 24'-2 1/2\" + 5 @ 30'-0\" + 24'-2 1/2\" = 198'-5\" vs the printed arc 198'-5 7/8\" - the joints are laid out with equal ends from the arc (reported, not adjusted).");
sg.Keys = new List<SegmentalKeyDefinition>
{
    new() { Name = "web shear keys", Kind = SegmentalKeyKind.WebShear, Depth = In(1.25), BaseWidth = In(3.875), FlatWidth = In(2.625), Length = F(1, 2), Spacing = In(6.5),
        WebRows = keyRows, Source = "p.283 (S3-35)",
        Unresolved = keyFlag("web shear key positions", "the reference of 'C' (bottom of the key zone) is drawn, not dimensioned; it is read along the web from the soffit to the first key centre, and which bent half carries the male keys at the bent control line is not shown (the up-station half is used)") },
    new() { Name = "top slab alignment keys", Kind = SegmentalKeyKind.TopSlabAlignment, Depth = In(1.25), BaseWidth = In(3.875), FlatWidth = In(2.625), Length = F(3),
        Offsets = new() { -F(19), 0, F(19) }, CenterDepth = In(2.5) + In(3.875) / 2, Source = "p.283 (S3-35) elevation, alignment key detail, Section A-A" },
    new() { Name = "bottom slab alignment keys (Detail B)", Kind = SegmentalKeyKind.BottomSlabAlignment, Depth = In(1.25), BaseWidth = In(3.875), FlatWidth = In(2.625), Length = F(1, 6),
        Offsets = new() { -(F(4, 10) - In(9)), F(4, 10) - In(9) }, CenterDepth = In(5), FromSection = 1, ToSection = 1, Source = "p.283 Detail B (section 1 only)",
        Unresolved = keyFlag("bottom slab alignment keys at section 1", "Detail B dimensions the 4'-10\" to the detail edge taken as CL girder, and the second (lower) key 1'-0\" below is drawn in the 2'-0\" slab without its own depth reference") },
    new() { Name = "bottom slab alignment keys (Detail B, lower)", Kind = SegmentalKeyKind.BottomSlabAlignment, Depth = In(1.25), BaseWidth = In(3.875), FlatWidth = In(2.625), Length = F(1, 6),
        Offsets = new() { -(F(4, 10) - In(9)), F(4, 10) - In(9) }, CenterDepth = In(5) + F(1), FromSection = 1, ToSection = 1, Source = "p.283 Detail B (section 1 only)",
        Unresolved = keyFlag("lower bottom slab alignment keys at section 1", "drawn 1'-0\" below the upper key; reference not dimensioned") },
    new() { Name = "bottom slab alignment keys (Detail A)", Kind = SegmentalKeyKind.BottomSlabAlignment, Depth = In(1.25), BaseWidth = In(3.875), FlatWidth = In(2.625), Length = F(3),
        Offsets = new() { -(F(5, 6) - F(1, 6)), F(5, 6) - F(1, 6) }, CenterDepth = In(5), FromSection = 2, ToSection = 5, Source = "p.283 Detail A (sections 2-5), Section C-C (5\" to the key CL)",
        Unresolved = keyFlag("bottom slab alignment keys at sections 2-5", "Detail A dimensions 5'-6\" from the key's outer end to the detail edge, taken as CL girder") },
    new() { Name = "bottom slab alignment keys (mid-span)", Kind = SegmentalKeyKind.BottomSlabAlignment, Depth = In(1.25), BaseWidth = In(3.875), FlatWidth = In(2.625), Length = F(3),
        Offsets = new() { 0 }, CenterDepth = In(5), FromSection = 6, ToSection = 12, Source = "p.283 elevation 'section at mid-span' (3'-0\" U.O.N.)",
        Unresolved = keyFlag("bottom slab alignment keys at sections 6-12", "Details A / B cover sections 1-5 only; the mid-span elevation draws one key on CL girder") },
};
sg.Unresolved.Add("[details] " + U("p.283", "bottom slab alignment keys at the bent control line and the R = 1\" key transitions", "the bent control line section shows the bottom slab keys without dimensions (solid diaphragm zone) and the transitions are drawn as 1\" radii",
    "Bottom slab keys at the bent control line not built; key flanks straight (trapezoid)."));

// ================================================================ 5. reinforcing in every segment (rebar-bonner-unit5.csx: SegmentalDefinition.SectionBars)
{ var rr = await SBM.Api.ScriptHost.RunFileAsync(Path.Combine("samples", "plans", "rebar-bonner-unit5.csx"), Doc, new[] { "--no-regen" });
  Print(rr.Output.TrimEnd()); if (!rr.Success) Print($"ERROR in rebar-bonner-unit5.csx: {rr.Error}"); }

// ================================================================ 6. regenerate / save (standalone)
Print($"Details: {sg.BlockTypes.Count} block types, {sg.Blocks.Count} placements, {sg.PtBars.Count} PT bar sets, {sg.Pipes.Count} pipe rows, {sg.Openings.Count} opening definitions, parapet, {sg.SectionBars?.Count ?? 0} section bar sets.");
if (!noRegen)
{
    var rep = Doc.Regenerate();
    Print($"Regenerated in {rep.Elapsed.TotalSeconds:0.0} s; {rep.Errors.Count} errors");
    var sm = Doc.Segmental(BridgeName);
    Print($"Joint keys {sm.Blocks.Count(b => b.Kind == SegmentalBlockKind.ShearKey)} sets ({sm.KeyVolume / Math.Pow(Ft(1), 3) / 27:0.00} CY male), parapet joints {sm.ParapetJoints.Count}");
    Print($"Blocks {sm.Blocks.Count(b => b.Kind != SegmentalBlockKind.ShearKey)} ({sm.BlockVolume / Math.Pow(Ft(1), 3) / 27:0.0} CY), openings {sm.Openings.Count} (-{sm.OpeningVolume / Math.Pow(Ft(1), 3) / 27:0.00} CY), parapets {sm.ParapetVolume / Math.Pow(Ft(1), 3) / 27:0.0} CY, bars {sm.Bars.Count} placed / {sm.Marks.Sum(m => m.Count)} scheduled");
    Print($"PT hardware: {string.Join(", ", sm.PtHardware.GroupBy(h => h.Kind).Select(g => $"{g.Count()} {g.Key}"))}");
    Doc.Save(outPath);
    Print($"Saved {outPath}");
}
