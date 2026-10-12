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

Print("");
Print($"SUMMARY: {pass} PASS / {diff} DIFF");
foreach (var d in diffs) Print("  " + d);
Print("");
Print("Generator warnings (UNRESOLVED / GAP):");
foreach (var d in sm.Diagnostics.Where(x => x.Severity != SBM.Core.Framing.DiagnosticSeverity.Info)) Print("  " + d.Message);
Print("Reported (Info):");
foreach (var d in sm.Diagnostics.Where(x => x.Severity == SBM.Core.Framing.DiagnosticSeverity.Info)) Print("  " + d.Message);
