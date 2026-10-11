// Complete top-of-slab check: model finished grade vs EVERY point of the plans' TOP OF SLAB ELEVATION tables
// (I-80 WB Design 116 sheets 192-201 / PDF 199-208; Ramp G Design 316 PDF 142-149).  Does not save the model.
//   sbm run samples\plans\check-top-of-slab-all.csx "samples\plans\I-80 WB and Ramp G.sbm"
// Data: samples\plans\top-of-slab-i80-rampg.csv  (P rows = plan values, B rows = pier / field-splice point numbers; parsed from the PDF).
// Plan note (sheet 194): numbered points are equally spaced along each line between the field splice and pier locations.
//   Girder lines: points placed along the framing girder path (bearing / splice distances), model = Alignment.ElevationAt(sta, off).
//   Edge / gutter / CL rows: located ONLY at support (pier/abutment) lines = intersection of the skewed bearing line with the
//   offset line (deck edge from the model deck, gutter = edge inboard by 1'-7 per the sheet 194 diagram, CL = offset 0).
//   Points between supports on those lines are UNRESOLVED (the plan does not define the line geometry) and are not in the statistics.
// Env: TOS_VERBOSE=1 prints every point; TOS_OUT=<file> writes a CSV of every point.
using System.Text;
const double Tol = 0.01;       // ft, plan precision
var verbose = Environment.GetEnvironmentVariable("TOS_VERBOSE") == "1";
var outPath = Environment.GetEnvironmentVariable("TOS_OUT");
string csvPath = new[] { @"samples\plans\top-of-slab-i80-rampg.csv", @"C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\samples\plans\top-of-slab-i80-rampg.csv" }.First(System.IO.File.Exists);

var plan = new List<(string Br, int U, int Pt, string Line, double Elev, int Pdf)>();
var bnds = new Dictionary<(string, int), List<(int Pt, string Kind, string Num)>>();
foreach (var ln in System.IO.File.ReadAllLines(csvPath))
{
    if (ln.StartsWith("#") || ln.Length < 3) continue;
    var c = ln.Split(',');
    if (c[0] == "B") { var k = (c[1], int.Parse(c[2])); if (!bnds.ContainsKey(k)) bnds[k] = new(); bnds[k].Add((int.Parse(c[3]), c[4], c[5])); }
    else plan.Add((c[1], int.Parse(c[2]), int.Parse(c[3]), c[4], double.Parse(c[5], System.Globalization.CultureInfo.InvariantCulture), int.Parse(c[6])));
}
string BridgeName(string br, int u) => br == "I-80" ? $"I-80 WB Unit {u}" : $"Ramp G Unit {u}";

var res = new List<(string Bridge, int Span, string SpanName, int Pt, string Line, double Sta, double Off, double Plan, double Model, int Pdf)>();
var unres = new List<(string Bridge, string Line, int Pt, int Pdf, string Why)>();

foreach (var key in bnds.Keys.OrderBy(k => k.Item1).ThenBy(k => k.Item2))
{
    var (brc, u) = key;
    var bn = BridgeName(brc, u);
    var b = Doc.Project.Bridges.First(x => x.Name == bn);
    var al = Doc.Alignment(b.AlignmentName);
    var f = Doc.Model(bn).Framing;
    int nsup = b.Supports.Count;
    var bl = bnds[key].OrderBy(x => x.Pt).ToList();
    var piers = bl.Where(x => x.Kind != "FS").Select(x => x.Pt).ToList();
    if (piers.Count != nsup) { Print($"!! {bn}: plan has {piers.Count} supports, model {nsup}"); continue; }
    var fsAt = bl.Where(x => x.Kind == "FS").ToDictionary(x => x.Pt, x => int.Parse(x.Num));
    int SpanOf(int p) { int s = 0; for (int i = 0; i < nsup - 1; i++) if (p >= piers[i]) s = i; return s; }
    var loc = new Dictionary<(int, int), (double Sta, double Off)>();   // (point, girder)
    for (int sp = 0; sp < nsup - 1; sp++)
    {
        int pa = piers[sp], pb = piers[sp + 1];
        var inner = fsAt.Where(kv => kv.Key > pa && kv.Key < pb).OrderBy(kv => kv.Key).ToList();
        var bpts = new List<int> { pa }; bpts.AddRange(inner.Select(kv => kv.Key)); bpts.Add(pb);
        for (int g = 0; g < f.Girders.Count; g++)
        {
            var gl = f.Girders[g];
            var b0 = f.SupportLine(sp).Bearings[g];
            if (b0 == null && sp == 0) { for (int k = 0; k < b.AuxiliarySupports.Count; k++) { var bb = f.SupportLine(nsup + k).Bearings[g]; if (bb != null) b0 = bb; } }
            var b1 = f.SupportLine(sp + 1).Bearings[g];
            if (b0 == null || b1 == null) continue;
            double s0 = !double.IsNaN(b0.PathDistance) ? b0.PathDistance : gl.DistanceAt(sp, 0);
            double s1 = !double.IsNaN(b1.PathDistance) ? b1.PathDistance : gl.DistanceAt(sp, 1);
            var ds = new List<double> { s0 }; bool ok = true;
            foreach (var kv in inner)
            {
                var spl = f.Splices.FirstOrDefault(x => x.Girder == g && x.Number == kv.Value);
                if (spl == null) { ok = false; break; }
                ds.Add(spl.GirderDistance);
            }
            if (!ok) continue;
            ds.Add(s1);
            for (int k = 0; k < bpts.Count - 1; k++)
            {
                int n = bpts[k + 1] - bpts[k];
                for (int i = (k == 0 ? 0 : 1); i <= n; i++)
                {
                    var (p, _, _) = gl.Path.At(ds[k] + (ds[k + 1] - ds[k]) * i / n);
                    var so = al.Inverse(p.XY);
                    loc[(bpts[k] + i, g)] = (so.Station, so.Offset);
                }
            }
        }
    }
    // point on a support line at a given offset from the alignment (exact intersection)
    (double Sta, double Off) OnSupport(int sup, Func<double, double> offsetAtStation)
    {
        var line = f.SupportLine(sup);
        double target = offsetAtStation(line.Station), t = target;
        for (int it = 0; it < 12; it++)
        {
            var q = line.AlignmentPoint + line.Direction * t;
            var so = al.Inverse(q);
            target = offsetAtStation(so.Station);
            double err = so.Offset - target;
            t -= err;
            if (Math.Abs(err) < 1e-9) return (so.Station, so.Offset);
        }
        var q2 = line.AlignmentPoint + line.Direction * t; var s2 = al.Inverse(q2); return (s2.Station, s2.Offset);
    }
    double gut = Ft(1) * (1 + 7.0 / 12);   // 1'-7 edge to gutter line (sheet 194 diagram)
    foreach (var pr in plan.Where(x => x.Br == brc && x.U == u))
    {
        int g = -1; (double Sta, double Off) at = default; string why = null;
        if (pr.Line.StartsWith("G:"))
        {
            g = pr.Line[2] - 'A';
            if (!loc.TryGetValue((pr.Pt, g), out at)) why = "girder has no bearing/splice at the span or span boundary (location along the girder not derivable)";
        }
        else
        {
            int si = piers.IndexOf(pr.Pt);
            if (si < 0) why = "between support lines: the plan does not define the edge/gutter/CL line geometry or its field-splice crossings";
            else if (brc == "I-80" && u == 2 && si == 0 && pr.Line != "CL") why = "Unit 2 point 1: north edge lies on the 6A line, south on 5WB (two lines, plan does not say which for edge/gutter)";
            else
            {
                var d = b.Deck;
                Func<double, double> off = pr.Line switch
                {
                    "CL" => st => 0.0,
                    "EDGE_L" => st => d.LeftAt(st),
                    "EDGE_R" => st => d.RightAt(st),
                    "GUT_L" => st => d.LeftAt(st) + gut,
                    _ => st => d.RightAt(st) - gut,
                };
                at = OnSupport(si, off);
            }
        }
        if (why != null) { unres.Add((bn, pr.Line, pr.Pt, pr.Pdf, why)); continue; }
        double z = b.Deck.FinishedElevation(al, at.Sta, at.Off) / Ft(1);   // deck-aware: includes the optional level edge strip
        int span = SpanOf(pr.Pt);
        res.Add((bn, span, $"{b.Supports[span].Name}-{b.Supports[span + 1].Name}", pr.Pt, pr.Line, at.Sta / Ft(1), at.Off / Ft(1), pr.Elev, z, pr.Pdf));
    }
}

string Kind(string l) => l.StartsWith("G:") ? "girder" : l == "CL" ? "CL" : l.StartsWith("EDGE") ? "edge" : "gutter";
double Rd(double v) => Math.Round(v, 4);
string St(IEnumerable<double> v) { var a = v.ToList(); return a.Count == 0 ? "-" : $"n={a.Count,4} mean {a.Average():+0.000;-0.000} min {a.Min():+0.000;-0.000} max {a.Max():+0.000;-0.000} |d|max {a.Max(x => Math.Abs(x)):0.000}"; }
var D = res.Select(r => (r, d: r.Model - r.Plan)).ToList();
Print($"== TOP-OF-SLAB CHECK: model - plan (ft); tolerance {Tol} ft; plan CSV {csvPath}");
Print($"Points in plan CSV {plan.Count}; checked {res.Count}; UNRESOLVED (excluded) {unres.Count}");
foreach (var bridge in new[] { "I-80", "Ramp G" })
{
    var q = D.Where(x => x.r.Bridge.StartsWith(bridge)).ToList();
    Print($"-- {bridge}: checked {q.Count} ({q.Count(x => Kind(x.r.Line) == "girder")} girder-line, {q.Count(x => Kind(x.r.Line) != "girder")} edge/gutter/CL at supports); beyond tol: {q.Count(x => Math.Abs(Rd(x.d)) > Tol)}");
    Print("   " + St(q.Select(x => x.d)));
}
Print("-- by bridge unit / line");
foreach (var gp in D.GroupBy(x => (x.r.Bridge, x.r.Line)).OrderBy(g => g.Key.Bridge).ThenBy(g => g.Key.Line))
    Print($"{gp.Key.Bridge,-15} {gp.Key.Line,-7} {St(gp.Select(x => x.d))}  flagged {gp.Count(x => Math.Abs(Rd(x.d)) > Tol)}");
Print("-- by bridge unit / span (girder lines only)");
foreach (var gp in D.Where(x => Kind(x.r.Line) == "girder").GroupBy(x => (x.r.Bridge, x.r.Span, x.r.SpanName)).OrderBy(g => g.Key.Bridge).ThenBy(g => g.Key.Span))
    Print($"{gp.Key.Bridge,-15} {gp.Key.SpanName,-20} {St(gp.Select(x => x.d))}  flagged {gp.Count(x => Math.Abs(Rd(x.d)) > Tol)}");
Print("-- flagged clusters (bridge unit, span, line): flagged/total, mean, worst point (PDF)");
foreach (var gp in D.GroupBy(x => (x.r.Bridge, x.r.Span, x.r.SpanName, x.r.Line)).OrderBy(g => g.Key.Bridge).ThenBy(g => g.Key.Span).ThenBy(g => g.Key.Line))
{
    var fl = gp.Where(x => Math.Abs(Rd(x.d)) > Tol).ToList();
    if (fl.Count == 0) continue;
    var w = fl.OrderByDescending(x => Math.Abs(x.d)).First();
    Print($"{gp.Key.Bridge,-15} {gp.Key.SpanName,-20} {gp.Key.Line,-7} {fl.Count,3}/{gp.Count(),3} mean {gp.Average(x => x.d):+0.000;-0.000} worst {w.d:+0.000;-0.000} @pt {w.r.Pt} (PDF {w.r.Pdf}, sta {w.r.Sta:F2} off {w.r.Off:F2}) pts {fl.Min(x => x.r.Pt)}-{fl.Max(x => x.r.Pt)}");
}
Print("-- UNRESOLVED (excluded from statistics)");
foreach (var gp in unres.GroupBy(x => (x.Bridge, x.Why)).OrderBy(g => g.Key.Bridge))
    Print($"{gp.Key.Bridge,-15} {gp.Count(),4} pts  lines {string.Join(",", gp.Select(x => x.Line).Distinct().OrderBy(x => x))}  PDF {string.Join(",", gp.Select(x => x.Pdf).Distinct().OrderBy(x => x))} :: {gp.Key.Why}");
if (verbose)
    foreach (var x in D) Print($"{x.r.Bridge,-15} pt {x.r.Pt,3} {x.r.Line,-7} sta {x.r.Sta,10:F2} off {x.r.Off,8:F3} plan {x.r.Plan,8:F2} model {x.r.Model,9:F3} d {x.d:+0.000;-0.000}");
if (!string.IsNullOrEmpty(outPath))
    System.IO.File.WriteAllLines(outPath, new[] { "bridge,span,point,line,sta_ft,off_ft,plan,model,d_ft,pdf" }.Concat(D.Select(x => $"{x.r.Bridge},{x.r.SpanName},{x.r.Pt},{x.r.Line},{x.r.Sta:F3},{x.r.Off:F3},{x.r.Plan:F2},{x.r.Model:F4},{x.d:F4},{x.r.Pdf}")));
