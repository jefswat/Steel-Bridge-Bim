// Camber / blocking check: the model's own girder geometry (finished grade, slab, haunch) + the plans' deflection inputs
// vs the plans' PRINTED girder-line haunch elevations (every numbered point) and BLOCKING values (every lettered point);
// plus the camber relation (printed camber vs total deflection minus its chord between the field splices).
// Does not save the model.   Run after camber-i80.csx and camber-ramp-g.csx:
//   sbm run samples\plans\check-blocking.csx "<model>.sbm"
// Model quantities (SBM.Core.Framing.CamberBlocking):  H = finished grade - slab + slab/barrier deflection (= plan "girder line haunch elevation");
//   blocking = no-load top of web (final top of web + steel + slab deflection) minus the same at the datum point.
// Tolerances: plan values are printed to 0.01 ft / 0.01 in, so |model - plan| > 0.01 ft is flagged (0.02 ft counted separately).
//   Camber relation tolerance 0.02 in (three rounded 0.01 in numbers).
// Env: CB_VERBOSE=1 prints every flagged point; CB_OUT=<file> writes a CSV of every compared point.
using System.Globalization;
using System.Text;
const double Tol = 0.01, Tol2 = 0.02, CamTol = 0.02;   // ft, ft, in
var verbose = Environment.GetEnvironmentVariable("CB_VERBOSE") == "1";
var outPath = Environment.GetEnvironmentVariable("CB_OUT");
string dataDir = new[] { @"docs\plan-data\camber", @"C:\SBM\docs\plan-data\camber" }.First(System.IO.Directory.Exists);
double Num(string s) => double.Parse(s.Trim().TrimEnd('*'), CultureInfo.InvariantCulture);
bool Has(string s) => s.Trim().Length > 0;
List<string[]> Table(string name) => System.IO.File.ReadAllLines(System.IO.Path.Combine(dataDir, name)).Select(l => l.Split(',')).ToList();
string St(IEnumerable<double> v) { var a = v.ToList(); return a.Count == 0 ? "-" : $"n={a.Count,4} mean {a.Average():+0.000;-0.000} |d|mean {a.Average(Math.Abs):0.000} |d|max {a.Max(Math.Abs):0.000}"; }
double Ft_(double m) => m / Ft(1);
double Rd(double v) => Math.Round(v, 4);

var cmp = new List<(string Br, int U, string Bridge, int G, int Pt, int Seg, string Kind, double S, double Sta, double Plan, double Model, double Grade)>();      // haunch elevation
var blk = new List<(string Bridge, int G, int Pt, string Letter, double Plan, double Model, double ModelCamberForm)>();
var cam = new List<(string Bridge, int G, int Pt, int Seg, double Plan, double FromDefl, double FromNoLoad)>();
var notes = new List<string>();
var outRows = new List<string> { "bridge,girder,point,kind,dist_ft,sta_ft,haunch_plan,haunch_model,haunch_d,blocking_plan,blocking_model,blocking_d,camber_plan_in,camber_from_defl_in" };

foreach (var (br, tag, label) in new[] { ("I80", "I-80 WB Unit", 3), ("RG", "Ramp G Unit", 4) }.Select(x => (x.Item1, x.Item2, x.Item3)))
for (int u = 1; u <= label; u++)
{
    string bn = $"{tag} {u}";
    var b = Doc.Project.Bridges.FirstOrDefault(x => x.Name == bn);
    var f = Doc.Model(bn).Framing;
    if (f.Camber == null) { notes.Add($"{bn}: no camber data in the model - run camber-*.csx first"); continue; }
    var H = Table($"{br}-unit{u}-haunch_elev_ft.csv");
    var B = Table($"{br}-unit{u}-blocking_ft.csv");
    var hdr = H[0];
    foreach (var gres in f.Camber.Girders)
    {
        string gl = $"{(char)('A' + gres.Girder)}";
        int col = Array.IndexOf(hdr, "girder_" + gl);
        if (col < 0) { notes.Add($"{bn} {gl}: no haunch column"); continue; }
        var gpath = f.Girders[gres.Girder].Path;
        var gcd = Doc.Project.Bridges.First(x => x.Name == bn).Girders.Camber.First(c => c.Girder == gres.Girder);
        var keyNums = gcd.Keys.Select(k => k.Number).OrderBy(x => x).ToList();
        // discontinuous girder: plan values of the tail (un-numbered, printed order) start at the key point before the girder end
        var endKey = gcd.Keys.FirstOrDefault(k => k.GirderEnd);
        int tailStart = endKey != null ? gcd.Keys.Where(k => k.Number < endKey.Number).Max(k => k.Number) : int.MaxValue;
        var tailH = new List<double>();
        if (endKey != null && System.IO.File.Exists(System.IO.Path.Combine(dataDir, $"{br}-unit{u}-haunch_elev_ft-discontinuous-unnumbered.csv")))
            tailH = Table($"{br}-unit{u}-haunch_elev_ft-discontinuous-unnumbered.csv").Skip(1).Where(r => r[0] == gl).OrderBy(r => int.Parse(r[1])).Select(r => Num(r[2])).ToList();
        int SegOf(int pt) { int s = 0; for (int i = 0; i < keyNums.Count; i++) if (pt >= keyNums[i]) s = i; return s; }
        foreach (var p in gres.Points)
        {
            var hrow = H.Skip(1).FirstOrDefault(r => int.Parse(r[0]) == p.Number);
            double planH = hrow != null && Has(hrow[col]) ? Num(hrow[col]) : double.NaN;
            if (p.Number >= tailStart && p.Number - tailStart < tailH.Count) planH = tailH[p.Number - tailStart];
            double dH = double.NaN;
            if (!double.IsNaN(planH))
            {
                double modelH = Ft_(p.HaunchElevation);
                cmp.Add((br, u, bn, gres.Girder, p.Number, SegOf(p.Number), p.Kind, Ft_(p.PathDistance), Ft_(p.Station), planH, modelH, gpath.GradeAt(p.PathDistance)));
                dH = modelH - planH;
            }
            string letter = "";
            double planB = double.NaN, mB = double.NaN, mBc = double.NaN;
            // starred value = location at the end of the discontinuous girder (the girder's own end point)
            var brow = B.Skip(1).FirstOrDefault(r => Has(r[col + 1]) && (r[col + 1].Contains('*') ? p.Kind == "end" : int.Parse(r[1]) == p.Number));
            if (brow != null && Has(brow[col + 1]) && !double.IsNaN(p.Blocking))
            {
                letter = brow[0]; planB = Num(brow[col + 1]); mB = Ft_(p.Blocking); mBc = double.IsNaN(p.BlockingFromCamber) ? double.NaN : Ft_(p.BlockingFromCamber);
                blk.Add((bn, gres.Girder, p.Number, letter, planB, mB, mBc));
            }
            if (!double.IsNaN(p.CamberFromDeflection)) cam.Add((bn, gres.Girder, p.Number, SegOf(p.Number), p.Camber / In(1), p.CamberFromDeflection / In(1), p.CamberFromNoLoad / In(1)));
            outRows.Add($"{bn},{gl},{p.Number},{p.Kind},{Ft_(p.PathDistance):F3},{Ft_(p.Station):F3},{planH},{Ft_(p.HaunchElevation):F4},{dH:F4},{planB},{mB:F4},{mB - planB:F4},{p.Camber / In(1):F3},{p.CamberFromDeflection / In(1):F3}");
        }
        // plan points in the tables that the model could not place (counted)
        var camCsvPts = H.Skip(1).Where(r => Has(r[col])).Select(r => int.Parse(r[0])).ToHashSet();
        int missing = camCsvPts.Count(n => gres.Points.All(p => p.Number != n));
        if (missing > 0) notes.Add($"{bn} {gl}: {missing} plan haunch points not compared (not located on the girder: {SBM.Core.Framing.CamberBlocking.Ranges(camCsvPts.Where(n => gres.Points.All(p => p.Number != n)))})");
        foreach (var row in B.Skip(1))
            if (Has(row[col + 1]) && (row[col + 1].Contains('*') ? gres.Points.All(p => p.Kind != "end") : gres.Points.All(p => p.Number != int.Parse(row[1]))))
                notes.Add($"{bn} {gl}: plan blocking {row[0]} (pt {row[1]}) = {row[col + 1]} not compared (point not located on the girder)");
        if (gres.DatumPoint == 0) notes.Add($"{bn} {gl}: no blocking datum - blocking not computed for this girder");
    }
}

Print("== CAMBER / BLOCKING CHECK: model - plan");
Print($"Haunch elevation (ft): tolerance {Tol}; compared {cmp.Count} points; beyond {Tol}: {cmp.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol)}, beyond {Tol2}: {cmp.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol2)}");
Print($"Blocking (ft): compared {blk.Count} lettered values; beyond {Tol}: {blk.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol)}, beyond {Tol2}: {blk.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol2)}");
Print("-- by bridge unit: HAUNCH ELEVATION (ft)");
foreach (var gp in cmp.GroupBy(x => x.Bridge)) Print($"{gp.Key,-15} {St(gp.Select(x => x.Model - x.Plan))}  flagged {gp.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol)}");
Print("-- by bridge unit: BLOCKING (ft) at the lettered points");
foreach (var gp in blk.GroupBy(x => x.Bridge)) Print($"{gp.Key,-15} {St(gp.Select(x => x.Model - x.Plan))}  flagged {gp.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol)}");
Print("-- by bridge unit and girder");
foreach (var gp in cmp.GroupBy(x => (x.Bridge, x.G)))
{
    var bl = blk.Where(x => x.Bridge == gp.Key.Bridge && x.G == gp.Key.G).ToList();
    Print($"{gp.Key.Bridge,-15} {(char)('A' + gp.Key.G)}  H: {St(gp.Select(x => x.Model - x.Plan))} flagged {gp.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol),3} | block: {St(bl.Select(x => x.Model - x.Plan))} flagged {bl.Count(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol)}");
}

Print($"-- flagged haunch clusters (bridge, girder, segment between key points): flagged/total, mean d, likely cause");
foreach (var gp in cmp.GroupBy(x => (x.Bridge, x.G, x.Seg)).OrderBy(g => g.Key.Bridge).ThenBy(g => g.Key.G).ThenBy(g => g.Key.Seg))
{
    var fl = gp.Where(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol).ToList();
    if (fl.Count == 0) continue;
    var d = gp.Select(x => x.Model - x.Plan).ToList();
    double mean = d.Average(), sd = Math.Sqrt(d.Sum(x => (x - mean) * (x - mean)) / d.Count);
    var gr = gp.Select(x => x.Grade).ToList();
    double sgg = gr.Sum(x => x * x);
    double shift = sgg > 1e-12 ? gp.Zip(d, (x, dd) => x.Grade * dd).Sum() / sgg : double.NaN;      // ft along the girder (model high => points too far ahead)
    double ssRes = sgg > 1e-12 ? gp.Zip(d, (x, dd) => (dd - shift * x.Grade) * (dd - shift * x.Grade)).Sum() : double.NaN;
    double ssTot = d.Sum(x => x * x);
    string cause;
    if (Math.Abs(mean) > Tol && sd < 0.35 * Math.Abs(mean)) cause = $"constant offset {mean:+0.00;-0.00} ft - elevation of the girder line in this segment (cross slope / pivot / girder offset), not point location";
    else if (!double.IsNaN(shift) && ssTot > 0 && ssRes < 0.3 * ssTot) cause = $"slope-correlated: equivalent to the plan point being {shift:+0.0;-0.0} ft along the girder from the model's (key point / splice distance)";
    else cause = "no single pattern (local profile / cross slope shape, or mixed)";
    if (fl.Max(x => Math.Abs(x.Model - x.Plan)) <= Tol2 + 1e-9) cause = "marginal (<= 0.02 ft = two rounded plan values plus a small systematic offset, " + (mean < 0 ? "model low" : "model high") + "); " + cause;
    var w = fl.OrderByDescending(x => Math.Abs(x.Model - x.Plan)).First();
    Print($"{gp.Key.Bridge,-15} {(char)('A' + gp.Key.G)} seg {gp.Key.Seg,2} (pts {gp.Min(x => x.Pt)}-{gp.Max(x => x.Pt)}) {fl.Count,3}/{gp.Count(),3} mean {mean:+0.000;-0.000} sd {sd:0.000} worst {w.Model - w.Plan:+0.000;-0.000} @pt {w.Pt}: {cause}");
}

Print("-- flagged blocking values (|model - plan| > 0.01 ft)");
foreach (var x in blk.Where(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol).OrderBy(x => x.Bridge).ThenBy(x => x.G).ThenBy(x => x.Pt))
    Print($"{x.Bridge,-15} {(char)('A' + x.G)} {x.Letter} pt {x.Pt,3} plan {x.Plan,6:F2} model {x.Model,8:F3} d {x.Model - x.Plan:+0.000;-0.000}");

Print("-- consistency inside the model: blocking from deflections vs blocking built from the camber (chord of no-load web top + camber), ft");
foreach (var gp in blk.Where(x => !double.IsNaN(x.ModelCamberForm)).GroupBy(x => x.Bridge)) Print($"{gp.Key,-15} {St(gp.Select(x => x.ModelCamberForm - x.Model))}   (difference = vertical-curve departure of the final top of web from its splice chord)");

Print($"-- CAMBER RELATION, tolerance {CamTol} in. Chord weights use the MODEL's distances along each girder (points equally spaced between key points).");
Print("   (a) camber vs total deflection (steel + slab/barrier) minus its chord between the splices  [no profile term]");
foreach (var gp in cam.GroupBy(x => x.Bridge))
    Print($"   {gp.Key,-15} n={gp.Count(),4} mean {gp.Average(x => x.Plan - x.FromDefl):+0.000;-0.000} |d|mean {gp.Average(x => Math.Abs(x.Plan - x.FromDefl)):0.000} |d|max {gp.Max(x => Math.Abs(x.Plan - x.FromDefl)):0.000} in; beyond tol {gp.Count(x => Math.Abs(x.Plan - x.FromDefl) > CamTol)}");
Print("   (b) camber vs the NO-LOAD top of web (model final top of web + deflections) minus its chord between the splices  [the plans' definition, includes the vertical curve]");
foreach (var gp in cam.GroupBy(x => x.Bridge))
    Print($"   {gp.Key,-15} n={gp.Count(),4} mean {gp.Average(x => x.Plan - x.FromNoLoad):+0.000;-0.000} |d|mean {gp.Average(x => Math.Abs(x.Plan - x.FromNoLoad)):0.000} |d|max {gp.Max(x => Math.Abs(x.Plan - x.FromNoLoad)):0.000} in; beyond tol {gp.Count(x => Math.Abs(x.Plan - x.FromNoLoad) > CamTol)}");
Print("   (b) segments where the plan's camber does not reconcile (any point beyond tolerance): bridge girder segment pts: flagged/total, worst");
foreach (var gp in cam.GroupBy(x => (x.Bridge, x.G, x.Seg)).OrderBy(g => g.Key.Bridge).ThenBy(g => g.Key.G).ThenBy(g => g.Key.Seg))
{
    var fl = gp.Where(x => Math.Abs(x.Plan - x.FromNoLoad) > CamTol).ToList();
    if (fl.Count == 0) continue;
    var w = fl.OrderByDescending(x => Math.Abs(x.Plan - x.FromNoLoad)).First();
    Print($"   {gp.Key.Bridge,-15} {(char)('A' + gp.Key.G)} seg {gp.Key.Seg,2} pts {gp.Min(x => x.Pt)}-{gp.Max(x => x.Pt)}: {fl.Count}/{gp.Count()}, worst {w.Plan - w.FromNoLoad:+0.00;-0.00} in @pt {w.Pt} (plan {w.Plan:F2}, from no-load shape {w.FromNoLoad:F2}, from deflection only {w.FromDefl:F2})");
}
// camber at the chord ends must be 0.00 by definition
var ends = new List<string>();
foreach (var bn in Doc.Project.Bridges.Select(x => x.Name))
    if (Doc.Model(bn).Framing.Camber is { } cr)
        foreach (var g in cr.Girders) foreach (var p in g.Points.Where(p => p.ChordEnd && Math.Abs(p.Camber) > In(0.005)))
            ends.Add($"{bn} {(char)('A' + g.Girder)} pt {p.Number}: camber {p.Camber / In(1):F2} in at a chord end");
Print($"-- chord ends with non-zero printed camber: {ends.Count}");
foreach (var e in ends.Take(30)) Print("   " + e);

Print("-- NOTES / not compared");
foreach (var n in notes) Print("   " + n);
if (verbose)
    foreach (var x in cmp.Where(x => Math.Abs(Rd(x.Model - x.Plan)) > Tol)) Print($"{x.Bridge,-15} {(char)('A' + x.G)} pt {x.Pt,3} sta {x.Sta,9:F2} s {x.S,8:F2} plan {x.Plan,8:F2} model {x.Model,9:F3} d {x.Model - x.Plan:+0.000;-0.000}");
if (!string.IsNullOrEmpty(outPath)) System.IO.File.WriteAllLines(outPath, outRows);
