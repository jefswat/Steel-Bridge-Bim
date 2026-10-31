// Plan check of the Bonner Unit 5 segmental model (build-bonner-unit5.csx). Run from the repo root:
//   sbm run "samples\plans\check-bonner-unit5.csx" "samples\plans\Bonner Unit 5 segmental.sbm"
// Compares the generated model with the extraction CSVs in docs/plan-data/bonner-b2500/segmental-unit5 (plan values) and prints
// PASS / DIFF rows (tolerances: 0.01 ft, 1/16 in, 1 lb / 0.5 %) plus the reported differences and the generator's warnings.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Segmental;

var inv = CultureInfo.InvariantCulture;
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
double D(string s) => double.Parse(s, inv);
int pass = 0, diff = 0;
var diffs = new List<string>();
void Row(string item, double model, double plan, double tol, string unit, string note = "")
{
    bool ok = Math.Abs(model - plan) <= tol + 1e-9;
    string s = $"{(ok ? "PASS" : "DIFF")}  {item}: model {model:0.###} vs plan {plan:0.###} {unit} (diff {model - plan:+0.###;-0.###}){(note != "" ? "  - " + note : "")}";
    if (ok) pass++; else { diff++; diffs.Add(s); }
    Print(s);
}
double ft = Ft(1), inch = In(1);
var sm = Doc.Segmental("Bonner Unit 5");
var def = sm.Definition;
var al = Doc.Framing("Bonner Unit 5").Alignment;

// ---------------------------------------------------------------- 1. segments: counts and lengths (segments.csv)
Print("== 1. Segments (segments.csv, S3-15..18)");
var segRows = Csv("segments.csv");
foreach (var (type, csv) in new[] { (SegmentType.Typical, "typical"), (SegmentType.Closure, "closure_joint"), (SegmentType.BentHalf, "bent_half"), (SegmentType.End, "end_segment") })
    Row($"count {type}", sm.Segments.Count(s => s.Type == type), segRows.Count(r => r[2] == csv), 0, "");
double worstLen = 0; string worstName = "";
foreach (var r in segRows)
{
    var s = sm.Segments.FirstOrDefault(x => x.Name == r[1]);
    if (s == null) { Row($"segment {r[1]} present", 0, 1, 0, ""); continue; }
    double d = Math.Abs(s.AlignmentLength / ft - D(r[5]));
    if (d > worstLen) { worstLen = d; worstName = r[1]; }
}
Row("max |segment length along -L- - plan length| (275 rows)", worstLen, 0, 0.01, "ft", worstName);
foreach (var sp in sm.Segments.GroupBy(s => s.Span))
    Row($"{sp.Key} total length along -L- (incl. end gap)", (sp.Max(s => s.Station1) - sp.Min(s => s.Station0)) / ft,
        sp.Key is "Span 19" or "Span 29" ? 200 - 10.0 / 12 : 350, 0.01, "ft");
foreach (var c in sm.Checks.Where(c => c.Item.StartsWith("max"))) Print($"INFO  {c.Item}: {c.Difference / inch:0.###} in ({c.Note})");
var chord = sm.Segments.Where(s => s.Type == SegmentType.Typical).Select(s => s.ChordLength / ft).ToList();
Print($"INFO  typical segment 3D chord lengths {chord.Min():0.0000} .. {chord.Max():0.0000} ft (plan 14'-0\" chords; joints placed at their -L- stations - UNRESOLVED convention)");

// ---------------------------------------------------------------- 2. depth / section (box-sections.csv, S3-20)
Print("== 2. Box depth and section (box-sections.csv, S3-20 p268)");
var gen = sm.Checks.Where(c => c.Item.StartsWith("depth at section") || c.Item.StartsWith("W/2")).ToList();
foreach (var c in gen) Row(c.Item, c.Model / inch, c.Plan / inch, 1.0 / 16, "in", c.Item.StartsWith("W/2") ? "4:1 web rule" : "");
foreach (var r in Csv("box-sections.csv"))
{
    double h = In(D(r[3])), t = In(D(r[10]));
    Row($"gross area section {r[0]} (no fillet)", SegmentalSection.Area(def.Box, h, t, noFillet: true) / (ft * ft), D(r[13]), 0.03, "ft2",
        "extraction polygon used the printed W/2; model uses the 4:1 rule (W/2 within 1/16\")");
    // model joint depths at this x (every cantilever: the joint at x from its bent)
    double x = Ft(D(r[1]));
    var joints = sm.Segments.Where(s => s.Type == SegmentType.Typical).SelectMany(s => new[] { (s, s.Station0, s.Depth0), (s, s.Station1, s.Depth1) })
        .Where(j => Math.Abs(Math.Abs(j.Item2 - Doc.Framing("Bonner Unit 5").Supports.First(b => b.Name == j.s.Bent).Station) - x) < 1e-6).ToList();
    if (joints.Count > 0)
        Row($"model joint depth at section {r[0]} ({joints.Count} joints, worst)", joints.Select(j => j.Item3 / inch).OrderBy(v => Math.Abs(v - D(r[3]))).Last(), D(r[3]), 1.0 / 16, "in");
}
Print($"INFO  depth with the fillet R 1'-0\": section 1 area {SegmentalSection.Area(def.Box, F(19), F(2)) / (ft * ft):0.000} ft2 (gross no-fillet table convention above)");
double F(double f, double i = 0) => Ft(f) + In(i);

// ---------------------------------------------------------------- 3. geometry control (S3-19 p267) and elevations
Print("== 3. Geometry control (geometry-control.csv, S3-19 p267) and top of bent (piers.csv)");
var gc = Csv("geometry-control.csv");
var piers = Csv("piers.csv").ToDictionary(r => "Bent " + r[0]);
foreach (var b in sm.BentRows)
{
    var g = gc.First(r => r[0] == b.Bent.ToUpperInvariant());
    Row($"{b.Bent} cross slope", Math.Abs(b.CrossSlope) * 100, D(g[2].TrimEnd('%')), 0.005, "%");
    Row($"{b.Bent} box height", b.Depth / ft, D(g[3]), 0.005, "ft");
    Row($"{b.Bent} DIM B", Math.Abs(b.DimB) / inch, D(g[4]), 1.0 / 16, "in", "soffit CL offset across");
    Row($"{b.Bent} long. slope", Math.Abs(b.Grade) * 100, Math.Abs(D(g[5].TrimEnd('%'))), 0.005, "%");
    Row($"{b.Bent} DIM A", Math.Abs(b.DimA) / inch, D(g[6]), 1.0 / 16, "in", "soffit CL offset along");
    double tob = D(piers[b.Bent][2]);
    double gap = b.SoffitZAtWp / ft - tob;
    double planGap = b.Bent is "Bent 19" or "Bent 20" or "Bent 21" or "Bent 22" ? 2.40 : b.Bent is "Bent 18" ? 2.47 : b.Bent is "Bent 29" ? 2.16 : 2.32;
    Row($"{b.Bent} soffit below the WP - top of bent (PGL {b.Wp.Z / ft:0.00}, soffit {b.SoffitZAtWp / ft:0.00}, TOB {tob:0.00})", gap, planGap, 0.01, "ft",
        (b.Bent is "Bent 18" or "Bent 29" ? "extraction value DERIVED (2.47 / 2.16)" : "extraction cross-check 2.40 / 2.32") +
        $"; extraction took PGL - H vertically, the model soffit is H normal to the sloped top: H x (1/cos - 1) = {b.Depth / ft * (Math.Sqrt(1 + b.CrossSlope * b.CrossSlope + b.Grade * b.Grade) - 1):0.000} ft lower");
}

// ---------------------------------------------------------------- 4. tendons (pt-tendon-schedule.csv, S3-57/58)
Print("== 4. Longitudinal tendons (pt-tendon-schedule.csv)");
var sched = Csv("pt-tendon-schedule.csv");
foreach (var g in new[] { ('T', TendonGroup.Cantilever), ('B', TendonGroup.Bottom), ('C', TendonGroup.Closure) })
{
    var rows = sched.Where(r => r[1][0] == g.Item1).ToList();
    Row($"{g.Item2} tendons: count", sm.Tendons.Count(t => t.Group == g.Item2), rows.Sum(r => int.Parse(r[2])), 0, "");
    double planW = rows.Sum(r => D(r[6]));
    double modelPlanW = sm.Tendons.Where(t => t.Group == g.Item2).Sum(t => t.PlanWeightLb);
    Row($"{g.Item2} tendons: sum of plan weights carried by the model", modelPlanW, planW, 1, "lb");
    double modelW = sm.StrandWeightLb(t => t.Group == g.Item2);
    Row($"{g.Item2} tendons: weight from MODEL lengths (strands x L x 0.74 lb/ft)", modelW, planW, planW * 0.005, "lb",
        "model length = duct path between the anchor faces (plan lengths stop short of the faces) on the curved box (L / R ducts on the outside / inside of curve 2)");
}
// cantilever lengths per designation (model geometry = anchor face to anchor face)
double worstT = 0; string worstTn = "";
foreach (var t in sm.Tendons.Where(t => t.Group == TendonGroup.Cantilever))
{
    double d = (t.Length - t.PlanLength) / ft;
    if (Math.Abs(d) > Math.Abs(worstT)) { worstT = d; worstTn = t.Name; }
}
var tl = sm.Tendons.Where(t => t.Group == TendonGroup.Cantilever).Select(t => (t.Length - t.PlanLength) / ft).ToList();
Print($"INFO  cantilever tendon model length (anchor-segment tip faces) - plan length: {tl.Min():+0.00;-0.00} .. {tl.Max():+0.00;-0.00} ft (worst {worstTn}); the plan lengths stop short of the faces (anchorage recess, not dimensioned)");
Row("total longitudinal strand weight (plan schedule sum)", sm.Tendons.Where(t => t.Group != TendonGroup.Transverse).Sum(t => t.PlanWeightLb), 1068208, 1, "lb");

// ---------------------------------------------------------------- 5. transverse PT (transverse-pt-stressing.csv)
Print("== 5. Transverse PT (S3-24 / S3-30 / S3-34)");
var tt = sm.Tendons.Where(t => t.Group == TendonGroup.Transverse).ToList();
double W(Func<TendonResult, bool> f) => tt.Where(f).Sum(t => t.Strands * t.Length / ft * def.StrandWeightLbPerFt);
bool InType(TendonResult t, SegmentType st) => sm.Segments.First(s => s.Name == t.Owner).Type == st;
Row("typical segments: tendons", tt.Count(t => InType(t, SegmentType.Typical)), 242 * 4, 0, "");
Row("closure joints: tendons", tt.Count(t => InType(t, SegmentType.Closure)), 9 * 2 + 2 * 1, 0, "");
Row("transverse PT weight typical + closure (printed total 121,624 lb)", W(t => InType(t, SegmentType.Typical) || InType(t, SegmentType.Closure)), 121624, 25, "lb", "plan total = rounded 492.4 / 246.2 / 123.1 lb per segment x count; model = exact 4 x 41'-7\" x 0.74 lb/ft");
Row("bent segment F1 & F2 tendons", tt.Count(t => InType(t, SegmentType.BentHalf)), 18 * 2, 0, "", "plan table: 18 bent segments; model: 20 halves (UNRESOLVED p.278)");
Row("bent segment F1 & F2 weight", W(t => InType(t, SegmentType.BentHalf)), 4420.8, 1, "lb");
Row("end segment E1-E4 tendons", tt.Count(t => InType(t, SegmentType.End)), 8, 0, "");
Row("end segment E1-E4 weight", W(t => InType(t, SegmentType.End)), 984.8, 1, "lb");

// ---------------------------------------------------------------- 6. concrete per segment type
Print("== 6. Concrete volume per segment type (not printed in S3: UNRESOLVED; weights at 150 pcf for comparison with the lifting weights)");
foreach (var g in sm.Segments.GroupBy(s => s.Type))
{
    double cy = g.Sum(s => s.Volume) / Math.Pow(ft, 3) / 27;
    Print($"INFO  {g.Key}: {g.Count()} pieces, {cy:0.0} CY total, {cy / g.Count():0.00} CY each (avg), {g.Average(s => s.Volume) / Math.Pow(ft, 3) * 0.150 / 2:0.0} ton each at 150 pcf");
}
var es = sm.Segments.First(s => s.Type == SegmentType.End);
var bh = sm.Segments.First(s => s.Type == SegmentType.BentHalf);
Row("end segment weight at 150 pcf (plan: 110 ton to be lifted, p271 note 9)", es.Volume / Math.Pow(ft, 3) * 0.150 / 2, 110, 5, "ton", "solid envelope - interior UNRESOLVED");
Row("bent segment half weight at 150 pcf (plan: 100 ton, p277 note 6)", bh.Volume / Math.Pow(ft, 3) * 0.150 / 2, 100, 5, "ton", "diaphragm solid, openings not modeled");
Print($"INFO  total box concrete {sm.ConcreteVolume / Math.Pow(ft, 3) / 27:0} CY (no plan quantity)");

// ---------------------------------------------------------------- 7. bearings and pedestals
Print("== 7. Bearings (bearing-data.csv S3-71, pedestal elevations on the cap sheets)");
Row("bearings", sm.Bearings.Count, 24, 0, "");
foreach (var c in sm.Checks.Where(c => c.Item.Contains("pedestal top")))
    Print($"INFO  {c.Item}: {c.Difference / inch:+0.00;-0.00} in");
var gaps = sm.Checks.Where(c => c.Item.Contains("pedestal top")).Select(c => c.Difference / inch).ToList();
if (gaps.Count > 0) Print($"INFO  soffit - (pedestal + bearing): {gaps.Min():0.00} .. {gaps.Max():0.00} in (plinth / set-up, not dimensioned)");

// ---------------------------------------------------------------- 8. bents
Print("== 8. Bents (piers.csv)");
foreach (var u in sm.Substructure!.Units)
{
    var cap = u.Solids.FirstOrDefault(s => s.Kind == SBM.Core.Substructure.SubElementKind.Cap);
    if (cap != null) Row($"{u.Name} top of cap", cap.Mesh.Bounds().Max.Z / ft, D(piers[u.Name][2]), 0.005, "ft");
    var ftg = u.Solids.FirstOrDefault(s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing);
    if (ftg != null)
    {
        Row($"{u.Name} footing bottom", ftg.Mesh.Bounds().Min.Z / ft, D(piers[u.Name][4]), 0.005, "ft");
        Row($"{u.Name} footing concrete (BOM, CIP)", Math.Abs(ftg.Mesh.Volume()) / Math.Pow(ft, 3) / 27, D(piers[u.Name][13]), 0.5, "CY", "sloped top modeled flat");
    }
}

// ---------------------------------------------------------------- 9. concrete details + critical-section bars (details-bonner-unit5.csx, DETAILS-DATA.md)
if (def.Blocks is { Count: > 0 })
{
    Print("== 9. Concrete details (p.263-302, 316-327) and the XX-D3 / XX-U3 bill (p.270)");
    double cyd = Math.Pow(ft, 3) * 27;
    // anchor blocks: every B / C tendon end in the cantilever arms (anchor-block-locations.csv pattern) x 2 webs
    var locs = Csv("anchor-block-locations.csv").Where(r => !r[4].EndsWith("-ES")).ToList();
    var built = sm.Blocks.Where(b => b.Kind == SegmentalBlockKind.AnchorBlock).ToList();
    int hit = locs.Count(r => built.Any(b => b.Segment == r[4] && b.Type.StartsWith(r[3].StartsWith("TOP") ? "top" : "bottom") && b.Label.StartsWith(r[2] + " ")));
    Row("layout-sheet anchor block rows (p.285-296) with a built block", hit, locs.Count, 0, "rows", "rows next to the end segments are UNRESOLVED in the data");
    int bTendonEnds = def.Tendons.Where(t => t.Group == TendonGroup.Bottom).Sum(t => t.Location is "Span 19" or "Span 29" ? 1 : 2);
    int cTendonEnds = def.Tendons.Where(t => t.Group == TendonGroup.Closure).Sum(t => t.Location is "Span 19" or "Span 29" ? 1 : 2);
    Row("bottom slab anchor blocks = B tendon ends in the cantilever arms x 2 webs (pt-tendon-schedule.csv)", built.Count(b => b.Type == "bottom slab anchor block"), 2 * bTendonEnds, 0, "");
    Row("top slab anchor blocks = C tendon ends in the cantilever arms x 2 webs", built.Count(b => b.Type == "top slab anchor block"), 2 * cTendonEnds, 0, "");
    // top slab block: inner face height (printed 1'-8") and offset from CL girder (scaled 7'-1 1/4")
    var bx = def.Box;
    var dw = new Vec2(-1 / bx.WebSlope, 1).Normalized(); var ni = new Vec2(-dw.Y, dw.X); if (ni.X > 0) ni = new Vec2(-ni.X, -ni.Y);
    var c0 = new Vec2(bx.WebRootX - bx.WebHorizontal, bx.WebTopDepth);
    double depth = Ft(2) + In(11.25), lenB = Ft(3) + In(2.25);
    var q = c0 + dw * ((depth - c0.Y - ni.Y * lenB) / dw.Y) + ni * lenB;
    double haunchY = bx.TopSlab + (q.X - bx.TopSlabFlat) / (c0.X - bx.TopSlabFlat) * (bx.WebTopDepth - bx.TopSlab);
    Row("top slab anchor block inner face height (haunch underside to the block bottom, p.298)", (depth - haunchY) / inch, 20, 0.0625, "in", "from the printed 3'-2 1/4\" square bottom and 2'-11 1/4\"");
    Row("top slab anchor block inner face from CL girder (p.298, scaled)", q.X / inch, 85.3, 1.0, "in", "scaled value, not printed");
    Print($"INFO  blocks built {sm.Blocks.Count(b => b.Kind != SegmentalBlockKind.ShearKey)} ({string.Join(", ", sm.Blocks.Where(b => b.Kind != SegmentalBlockKind.ShearKey).GroupBy(b => b.Type).Select(g => $"{g.Count()} {g.Key}"))}), {sm.BlockVolume / cyd:0.0} CY");
    // joint keys (p.283): male web keys per joint vs the key table (rows 1-12 + bent control line), alignment keys per joint
    var keys = sm.Blocks.Where(b => b.Kind == SegmentalBlockKind.ShearKey).ToList();
    if (keys.Count > 0)
    {
        int KeysIn(SegmentalBlockResult b) => int.Parse(b.Label.Split(' ')[0]);
        foreach (var r in Csv("web-shear-keys.csv"))
        {
            string sec = r[0].StartsWith("BENT") ? "bent CL" : r[0];
            var sets = keys.Where(b => b.Type == "web shear keys" && b.Label.EndsWith("joint section " + sec)).ToList();
            if (sets.Count == 0) { Row($"web shear keys at section {sec}: joints with keys", 0, 1, 0, ""); continue; }
            Row($"web shear keys per web at section {sec} (p.283 'A', {sets.Count} webs)", sets.Max(KeysIn), int.Parse(r[1]), 0, "", sets.Min(KeysIn) == sets.Max(KeysIn) ? "" : "webs differ");
        }
        var top = keys.Where(b => b.Type == "top slab alignment keys").ToList();
        Row("top slab alignment keys per joint (p.283: CL + 19'-0\" each side)", top.Count > 0 ? top.Max(KeysIn) : 0, 3, 0, "");
        int nj = keys.Where(b => b.Type == "web shear keys").Select(b => b.Segment).Distinct().Count();
        Print($"INFO  joint keys: {keys.Sum(KeysIn)} male keys in {keys.Count} sets on {nj} match-cast joints, {sm.KeyVolume / cyd:0.00} CY (recorded; segments not cut, net zero)");
    }
    // openings
    Row("A.O. openings (p.263-266 labels)", sm.Openings.Count(o => o.Name.EndsWith(" A.O. access opening")), 14, 0, "");
    Row("T.A.O. openings (p.263-266 labels, one per span)", sm.Openings.Count(o => o.Name.Contains("T.A.O.")), 11, 0, "");
    Row("A.O.B. voids (one per bent half, bents 19-28)", sm.Openings.Count(o => o.Name.Contains("A.O.B.")), 20, 0, "");
    Print($"INFO  openings remove {sm.OpeningVolume / cyd:0.00} CY; net box concrete {sm.NetConcreteVolume / cyd:0} CY (segments {sm.ConcreteVolume / cyd:0} + blocks - openings)");
    // parapet (p.327 quantity table, labelled UNIT 3 on the Unit 5 sheet: CONFLICT reported)
    double parLen = sm.Parapets.Count > 0 ? 2 * sm.Segments.Sum(x => x.ChordLength) / ft : 0;
    Row("parapet length both sides (p.327 7,100 LF)", parLen, 7100, 5, "LF", "model = the box length (end segment faces), the plan LF runs bent 18 to bent 29 CL; table labelled UNIT 3 (CONFLICT, recorded)");
    Row("parapet concrete (p.327 Class AA 768.6 CY)", sm.ParapetVolume / cyd, 768.6, 768.6 * 0.01, "CY", "1/2 in. joints modeled; end posts not modeled");
    // parapet joints (p.323-326): 30'-0 chains per span, arcs and end lengths along the theoretical edge of deck
    foreach (var c in sm.Checks.Where(c => c.Item.StartsWith("parapet joints")))
        Row(c.Item, c.Model / ft, c.Plan / ft, 1.0 / 16 / 12, "ft");
    if (sm.ParapetJoints.Count > 0) Row("parapet joints per side (spans 19 / 29: 5 spaces, 20-28: 10 spaces -> 6 + 6 + 9 x 11)", sm.ParapetJoints.Count / 2.0, 6 + 6 + 9 * 11, 0, "");
    // bars (rebar-bonner-unit5.csx): XX-D3 / XX-U3 unit vs the p.270 bill (counts exact, lengths 1", weight 0.5 %)
    const string U3 = "typical segments XX-D3 / XX-U3";
    int nSeg = sm.Segments.Count(x => System.Text.RegularExpressions.Regex.IsMatch(x.Name, @"^\d+-[DU]3$"));
    Row("XX-D3 / XX-U3 segments with the bill", nSeg, 20, 0, "");
    double lb3 = sm.Marks.Where(m => m.Unit == U3).Sum(m => m.TotalMass) / 0.45359237;
    Row("typical segment XX-D3 / XX-U3 bar weight per segment (p.270 8,511 lb)", nSeg > 0 ? lb3 / nSeg : 0, 8511, 8511 * 0.005, "lb", "placed + counted bars; B1 = the bars on the drawn faces");
    foreach (var r in Csv("typical-segment-bar-list.csv").Where(r => r[0] != "" && !r[0].StartsWith("TOTAL")))
    {
        var mk = sm.Marks.FirstOrDefault(m => m.Mark == r[0] && m.Unit == U3);
        Row($"bar {r[0]} count per XX-D3 / XX-U3 segment (p.270)", mk == null || nSeg == 0 ? 0 : mk.Count / (double)nSeg, int.Parse(r[1]), 0, "");
        if (mk != null) Row($"bar {r[0]} out-to-out length (p.270 {r[4]})", mk.Length / inch, Len(r[4]) / inch, 1, "in", "model length = listed for fixed-shape bars; W bars from the faces + printed legs");
    }
    // every segment has placed bars
    foreach (var t in new[] { SegmentType.Typical, SegmentType.BentHalf, SegmentType.Closure, SegmentType.End })
    {
        var segsT = sm.Segments.Where(x => x.Type == t).ToList();
        int n = segsT.Count(x => sm.Bars.Any(b => SegmentHas(x, b)));
        Row($"{t} segments with placed bars", n, segsT.Count, 0, "");
    }
    bool SegmentHas(SegmentResult x, SBM.Core.Concrete.BarPlacement b)
    {
        var p0 = b.Points[b.Points.Length / 2];
        var a = x.Top1 - x.Top0; double u = SBM.Core.Geometry.Vec3.Dot(p0 - x.Top0, a) / SBM.Core.Geometry.Vec3.Dot(a, a);
        return u >= 0 && u <= 1 && b.Unit.Length > 0 && (x.Type switch { SegmentType.Typical => b.Unit.StartsWith("typical"), SegmentType.BentHalf => b.Unit == "bent segments", SegmentType.Closure => b.Unit == "closure joints", _ => b.Unit == "end segments" });
    }
    foreach (var g in sm.Marks.GroupBy(m => m.Unit))
        Print($"INFO  {g.Key}: {g.Count()} marks, {g.Sum(m => m.Count)} bars ({sm.Bars.Count(b => b.Unit == g.Key)} placed), {g.Sum(m => m.TotalMass) / 0.45359237:0} lb; counted only: {string.Join(", ", g.Where(m => m.Location.Contains("not modeled")).Select(m => m.Mark))}");
    Print($"INFO  bars placed {sm.Bars.Count}; scheduled {sm.Marks.Sum(m => m.Count)} ({sm.Marks.Sum(m => m.TotalMass) / 0.45359237:0} lb); cover violations {sm.CoverViolations.Count}");
    foreach (var v in sm.CoverViolations.GroupBy(v => (v.Unit, v.Mark))) Print($"INFO  cover: {v.Key.Mark} ({v.Key.Unit}) {v.Count()} checked bars short by up to {v.Max(x => x.Excess) / inch:0.###} in ({v.First().Face})");
}

// ---------------------------------------------------------------- 10. piles, bent centring, drains, expansion joints (misc-bonner-unit5.csx, MISC-DATA.md)
if (def.Misc is { } md)
{
    Print("");
    Print("== 10. Piles / bent centring / deck drains / expansion joints (piles.csv, piles-bents.csv, p.161, 254-259, 271, 322, 327, 333, 335-352, 407)");
    var pb = Csv("piles-bents.csv").ToDictionary(r => "Bent " + r[0]);
    var pileCsv = Csv("piles.csv");
    var units = sm.Substructure?.Units ?? new List<SBM.Core.Substructure.SubstructureUnit>();
    int allPiles = 0; double allLf = 0, allPlanLf = 0;
    foreach (var u in units.OrderBy(x => x.Station))
    {
        if (!pb.TryGetValue(u.Name, out var r)) continue;
        var piles = u.Foundations.SelectMany(f => f.Piles).ToList();
        allPiles += piles.Count;
        Row($"{u.Name}: pile count", piles.Count, int.Parse(r[1]), 0, "", $"pile data p.{r[5]}, footing plan p.{r[9]}");
        double lf = piles.Sum(p => p.Length) / ft; allLf += lf; allPlanLf += D(r[7]);
        Row($"{u.Name}: total pile length head to tip", lf, D(r[7]), Math.Max(1, D(r[7]) * 0.005), "LF", $"bill of material p.{r[8]}; tips at 'no higher than' EL {r[3]}");
        Row($"{u.Name}: deepest tip vs 'pile tip no higher than'", piles.Min(p => p.TipZ) / ft, D(r[3]), 0.01, "ft");
        Row($"{u.Name}: battered piles (2:12)", piles.Count(p => p.IsBattered), pileCsv.Count(x => "Bent " + x[0] == u.Name && (x[4] != "0" || x[5] != "0")), 0, "");
        // Detail C: CL footing / column / cap left of -L- = the box soffit CL (DIM "B")
        var col = u.Columns.FirstOrDefault();
        double off = -In(D(r[12]));
        if (col != null) Row($"{u.Name}: column centre S (Detail C)", col.S / inch, off / inch, 1.0 / 16, "in", r[13]);
        var row = sm.BentRows.FirstOrDefault(x => x.Bent == u.Name);
        if (row != null) Row($"{u.Name}: Detail C offset vs model box soffit CL offset (DIM B)", -row.DimB / inch, D(r[12]), 1.0 / 8, "in", "model = box depth x sin(cross slope) at the bent CL");
    }
    Row("piles, all bents", allPiles, 284, 0, "", "S3-03..05 / pile data p.254-259");
    Print($"INFO  pile LF model {allLf:0} vs bills of material {allPlanLf:0}; 36\" square PS piles modeled solid (21\" void, p.42 not modeled)");
    var misc = sm.Substructure?.Misc;
    if (misc != null)
    {
        var drains = misc.Elements.Where(e => e.Kind == SBM.Core.Misc.MiscKind.DrainTrench).ToList();
        Row("deck drains (one per typical segment, p.322 note 4)", drains.Count, sm.Segments.Count(s => s.Type == SegmentType.Typical), 0, "");
        int wrongSide = 0; double worstEdge = 0;
        foreach (var d in drains)
        {
            var bb = d.Mesh.Bounds();
            var so = al.Inverse(new Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2));
            double low = al.ElevationAt(so.Station, Ft(21)) < al.ElevationAt(so.Station, -Ft(21)) ? 1 : -1;
            if (Math.Sign(so.Offset) != low) wrongSide++;
            worstEdge = Math.Max(worstEdge, Math.Abs(Math.Abs(so.Offset) - (Ft(21) + In(3.5) - Ft(1) - In(7.5))) / inch);
        }
        Row("deck drains not on the low side (p.322 note 3)", wrongSide, 0, 0, "");
        Row("deck drains: max |plan offset - (21'-3 1/2\" - 1'-7 1/2\")|", worstEdge, 0, 0.5, "in", "measured across the sloped top: horizontal projection on 4.5 % + chord offset");
        foreach (var j in md.Joints)
        {
            var dev = misc.Elements.FirstOrDefault(e => e.Kind == SBM.Core.Misc.MiscKind.JointDevice && e.Assembly == j.Name);
            var op = sm.Checks.FirstOrDefault(c => c.Item.StartsWith(j.Name + ": opening"));
            Print($"INFO  {j.Name}: device envelope {(dev != null ? "built (placeholder, flagged)" : "NOT built")}; derived opening {(op != null ? $"{op.Model / inch:0.##} in" : "-")} (DIM \"A\" not in the set)");
            Row($"{j.Name}: parapet end posts", misc.Elements.Count(e => e.Kind == SBM.Core.Misc.MiscKind.JointEndPost && e.Assembly == j.Name), 2, 0, "", "p.327 / p.333, segmental side only");
            Row($"{j.Name}: parapet cover plates", misc.Elements.Count(e => e.Kind == SBM.Core.Misc.MiscKind.JointCoverPlate && e.Assembly == j.Name), 2, 0, "", "p.333");
            var bo = sm.Openings.FirstOrDefault(o => o.Name.Contains(j.Name + " blockout"));
            Print($"INFO  {j.Name}: end segment blockout {(bo != null ? $"{bo.RemovedVolume / Math.Pow(ft, 3) / 27:0.00} CY removed from {bo.Segment}" : "not built")}");
        }
    }
    // 2-bar metal rail (S0-36 / S0-37, post spacing S3-80..83): posts = the p.38 anchor assembly table (Unit 5: 1189), 4 anchor bolts each
    var rails = Doc.Model("Bonner Unit 5").Steel?.Railings;
    if (rails != null && rails.Runs.Count > 0)
    {
        Row("2-bar metal rail posts (p.38 4-bolt anchor assemblies, Unit 5)", rails.Runs.Sum(r => r.Posts), 1189, 0, "");
        Row("2-bar metal rail anchor bolts (4 per post)", rails.Bolts.Count, 4 * 1189, 0, "");
        foreach (var r in rails.Runs) Print($"INFO  {r.Name}: {r.Length / ft:0.0} ft along the parapet face, {r.Posts} posts, {r.BeginStation / ft:0.00} - {r.EndStation / ft:0.00}");
        foreach (var d in rails.Diagnostics.Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info)) Print("INFO  rail: " + d.Message);
    }
    // Fixtures / small openings (misc-bonner-unit5.csx section 4b): solar array platforms (p.392 / 322 table, p.332 bill of material), nav light
    // sleeves (p.392), box girder lighting (p.388-391 symbols, p.387 panels), access doors (p.263-266 A.O. labels, A.O.B. at bents 19-28), vermin
    // screens (p.314), vent holes (p.268 note 3), survey disc blockouts (p.266 SURVEY MARKERS table: 55 N rows = pairs, 11 TP).
    var mel = sm.Substructure?.Misc?.Elements ?? new List<SBM.Core.Misc.MiscElement>();
    int Cnt(SBM.Core.Misc.MiscKind k, string desc = "") => mel.Count(e => e.Kind == k && (desc == "" || e.Description.StartsWith(desc)));
    var platAsm = sm.Substructure?.Misc?.Assemblies.Where(a => a.Group == SBM.Core.Misc.MiscGroup.SolarPlatform).Select(a => a.Name).Distinct().Count() ?? 0;
    Row("solar array support platforms (p.392 / 322 table, spans 20-28)", platAsm, 9, 0, "");
    foreach (var (mark, n) in new[] { ("B1 ", 4), ("B2 ", 4), ("B3 ", 4), ("B11 ", 4), ("B5 ", 6), ("B7 ", 1), ("B9 ", 2), ("B10 ", 5), ("P3 ", 4), ("P5 ", 4) })
        Row($"platform {mark.Trim()} per platform (p.332 bill of material)", platAsm == 0 ? 0 : (double)mel.Count(e => e.Group == SBM.Core.Misc.MiscGroup.SolarPlatform && e.Description.StartsWith(mark)) / platAsm, n, 0, "");
    Row("nav light conduit sleeves (p.392: one per platform station)", Cnt(SBM.Core.Misc.MiscKind.Conduit, "2 1/2\" PVC"), 9, 0, "");
    Row("box girder luminaires (p.388-391 symbols: 4 + 9 x 7 + 4)", Cnt(SBM.Core.Misc.MiscKind.Luminaire), 71, 0, "");
    Row("box girder luminaires type A (5'-0\" / 20 deg)", Cnt(SBM.Core.Misc.MiscKind.Luminaire, "LED luminaire 51 W (basis of design Cooper Valet LED tunnel), mounting height 5"), 31, 0, "");
    Row("panelboards (p.387: Panel A span 19, Panel B span 29)", Cnt(SBM.Core.Misc.MiscKind.Panelboard), 2, 0, "");
    Row("access doors A.O. (p.263-266 labels) + A.O.B. (bents 19-28)", Cnt(SBM.Core.Misc.MiscKind.AccessDoor), 14 + 10, 0, "");
    Row("vermin screens (p.314: anchor faces at bents 18 / 29)", mel.Where(e => e.Kind == SBM.Core.Misc.MiscKind.VerminScreen).Select(e => e.Assembly).Distinct().Count(), 2, 0, "");
    Row("vent holes (p.268 note 3: one per typical segment)", sm.Openings.Count(o => o.Name.EndsWith(" vent hole")), sm.Segments.Count(x => x.Type == SegmentType.Typical), 0, "");
    Row("survey disc blockouts N (p.266 table: 55 rows x left / right pair)", sm.Openings.Count(o => o.Name.Contains("survey disc blockout N")), 110, 0, "");
    Row("survey disc blockouts TP (p.266 table)", sm.Openings.Count(o => o.Name.Contains("survey disc blockout TP")), 11, 0, "");
    foreach (var c in sm.Checks.Where(c => c.Item.Contains("not fully inside the footing")))
        Row(c.Item, c.Model, 0, 0, "", c.Note);
}

// ---------------------------------------------------------------- PT hardware, erection / deviation blocks, tendon lanes (details-bonner-unit5.csx 1 / 2b / 2c)
{
    var hw = sm.PtHardware;
    if (hw.Count > 0 || sm.Blocks.Any(b => b.Kind == SegmentalBlockKind.DeviationBlock))
    {
        // erection bars: every joint between two typical segments of an arm (top 2 per web, bottom 1 per web), D1 - U1 through every bent
        var typ = sm.Segments.Where(x => x.Type == SegmentType.Typical).ToList();
        int armJoints = typ.GroupBy(x => x.Bent + (x.Name.Contains("-D") ? "D" : "U")).Sum(g => g.Count() - 1);
        int bents = typ.Select(x => x.Bent).Distinct().Count();
        Row("top temporary PT bars between consecutive typical segments (2 per web, p.300)", hw.Count(h => h.Kind == SBM.Core.Segmental.SegmentalHardwareKind.TemporaryPtBar && h.Name.Contains("top temporary PT bars ") && !h.Name.Contains("D1-U1")), 4 * armJoints, 0, "");
        Row("bottom temporary PT bars between consecutive typical segments (1 per web, staggered, p.299)", hw.Count(h => h.Name.Contains("bottom temporary PT bars ") && !h.Name.Contains("D1-U1")), 2 * armJoints, 0, "");
        Row("D1 - U1 temporary PT bars through the bent (2 top + 1 bottom per web)", hw.Count(h => h.Name.Contains("D1-U1")), 6 * bents, 0, "");
        foreach (var c in sm.Checks.Where(c => c.Item.StartsWith("PT bars ")))
            Print($"{(c.Model >= c.Plan - 1e-6 ? "INFO" : "INFO (shorter than the printed minimum bar)")}  {c.Item}: {c.Model / ft:0.00} ft vs {c.Plan / ft:0.00} ft - {c.Note}");
        Row("deviation blocks at the closure joints of spans 20-28 (2 webs, p.302)", sm.Blocks.Count(b => b.Kind == SegmentalBlockKind.DeviationBlock), 18, 0, "");
        Row("deviation block pipes (deviation-pipes.csv: 4 per closure, p.302)", hw.Count(h => h.Kind == SBM.Core.Segmental.SegmentalHardwareKind.DeviationPipe), 36, 0, "");
        Row("future PT pipes F1-F4 at bents 19-28 (8 per bent, p.308)", hw.Count(h => h.Kind == SBM.Core.Segmental.SegmentalHardwareKind.FuturePtPipe), 80, 0, "");
        var dl = sm.Checks.Where(c => c.Item.Contains("pipe") && c.Item.Contains("printed length")).ToList();
        if (dl.Count > 0) Row("pipe length L1 + R x delta + L3 vs the printed 4'-0\" (worst)", dl.OrderByDescending(c => Math.Abs(c.Difference)).First().Model / inch, 48, 1.0 / 16, "in", "delta from the table offsets");
        foreach (var kind in new[] { "deviation pipe", "future PT pipe" })
        {
            var dd = sm.Checks.Where(c => c.Item.Contains(kind) && c.Item.Contains("printed delta")).ToList();
            if (dd.Count == 0) continue;
            var w = dd.OrderByDescending(c => Math.Abs(c.Difference)).First();
            Row($"{kind}s: bend angle from the table offsets / tangent lengths vs the printed delta (worst of {dd.Count})", w.Model, w.Plan, 0.1, "deg", "plan table internal difference, reported (pipes built on the dimensioned offsets)");
        }
        // Type A section (p.300 B-B) from the printed free end 2'-8 1/4" and the 3'-3" bottom square to the web
        var bx = def.Box;
        double k = Math.Sqrt(1 + 1.0 / (bx.WebSlope * bx.WebSlope));
        double drop = 39 * (1 / bx.WebSlope) / k;
        Row("Type A block: drop of the bottom over 3'-3\" (square to the web) vs the printed 9 3/4\"", drop, 9.75, 1.0 / 16, "in", "bottom drawn square to the web (p.300 B-B)");
        double xWebIn = (bx.WebRootX - bx.WebHorizontal) / inch - ((32.25 + drop) - bx.WebTopDepth / inch) / bx.WebSlope;
        double xFree = xWebIn - 39 / k;
        double haunch = bx.TopSlab / inch + (bx.WebTopDepth - bx.TopSlab) / inch * Math.Clamp((xFree - bx.TopSlabFlat / inch) / (bx.TopHaunchRun / inch), 0, 1);
        Row("Type A block: inner face height (haunch underside to the free end) vs the printed 1'-5\"", 32.25 - haunch, 17, 1.0 / 16, "in", "from the printed 2'-8 1/4\" and 3'-3\"");
        var tc = sm.Blocks.Where(b => b.Type == "erection PT bar block Type C").ToList();
        if (tc.Count > 0)
        {
            double hmin = tc.Min(b => { var bb = b.Mesh.Bounds(); return (bb.Max.Z - bb.Min.Z) / inch; });
            Row("CONFLICT Type C block height (13'-9\" below the deck top to the slab / fillet) vs '1'-6 5/8\" (MIN.) AT FACE OF BLOCK' (p.299)", hmin, 18.625, 1.0 / 16, "in", "bar 14'-6\" below the deck top per Section C-C; reported, not adjusted");
        }
        // tendon lanes / profiles (p.285-296, 303, 297)
        var lr = sm.Checks.Where(c => c.Item.StartsWith("tendon ") && c.Item.Contains("lane hole")).ToList();
        if (lr.Count > 0)
        {
            Row("tendons through their lane holes: largest duct offset at a joint face", lr.Max(c => c.Model), 0, 1.0 / 16, "in", $"{lr.Count} tendons");
            Row("tendons through their lane holes: tendons with a joint offset > 1/16\"", lr.Count(c => c.Model > 1.0 / 16), 0, 0, "", "curves of a lane change reaching past a joint face");
            var an = sm.Checks.Where(c => c.Item.StartsWith("tendon ") && c.Item.Contains("angle change")).ToList();
            Print($"INFO  tendon angle change at the joints: largest {an.Max(c => c.Model):0.###} deg, {an.Count(c => c.Model > 0.5)} tendons above 0.5 deg (the duct follows its lane, which kinks with the box chords / the soffit; kinks RELATIVE to the lane are the CONFLICT rows below)");
            // p.303: straight from the anchor at the printed exit angle (5.40 / 6.86 deg) to the P.I. 9'-0" beyond it; the P.I. so found vs the one on the lane /
            // 3.07 deg diagonal through the next joint hole (the printed 2 3/4" closes the 1'-1" lane step)
            var ex = sm.Checks.Where(c => c.Item.StartsWith("tendon ") && c.Item.Contains("printed") && c.Item.Contains("exit")).ToList();
            if (ex.Count > 0)
                Row("cantilever tendons: anchor P.I. at the printed exit angle vs on the lane / 3.07 deg diagonal through the next joint hole (largest)", ex.Max(c => Math.Abs(c.Model)), 0, 1.0 / 8, "in",
                    $"{ex.Count} tendon ends (p.303 5.40 / 6.86 deg, 2 3/4\" on the diagonal)");
            Row("CONFLICT tendons with part of the strand bundle outside the concrete", sm.Checks.Count(c => c.Item.StartsWith("CONFLICT tendon") && c.Item.Contains("outside the concrete")), 0, 0, "", "never moved");
            Row("CONFLICT tendons whose lane-change curve (printed P.I.s + R) does not fit before the next joint", sm.Checks.Count(c => c.Item.StartsWith("CONFLICT tendon") && c.Item.Contains("not in its new lane")), 0, 0, "",
                "p.303 / 304 P.I.s and radii vs the lane changes read from p.285-296; built at the radius that fits, never moved");
        }
    }
}

Print("");
Print($"SUMMARY: {pass} PASS / {diff} DIFF");
foreach (var d in diffs) Print("  " + d);
Print("");
Print("Generator warnings (UNRESOLVED / GAP):");
foreach (var d in sm.Diagnostics.Where(x => x.Severity != SBM.Core.Framing.DiagnosticSeverity.Info)) Print("  " + d.Message);
Print("Reported (Info):");
foreach (var d in sm.Diagnostics.Where(x => x.Severity == SBM.Core.Framing.DiagnosticSeverity.Info)) Print("  " + d.Message);
