// Camber / deflection / blocking-datum data of WB I-80 Units 1-3 (Design 116, CAMBER & BLOCKING sheets PDF 132-157),
// from docs\plan-data\camber\I80-unit<n>-*.csv, onto GirderLayoutDefinition.Camber.  Re-runnable (replaces the camber data);
// run after build-i80-wb.csx, girders-i80.csx, pier-6a.csx, girder-geometry-i80.csx and field-splices-i80.csx.
//   sbm run samples\plans\camber-i80.csx "<model>.sbm"
// Mapping (all from the plans, nothing assumed):
//  * Key points = the CL bearings (PIER/ABUT rows) and field splices (FS rows) of the plans' point numbering
//    (samples\plans\top-of-slab-i80-rampg.csv "B" rows); numbered points between two key points are equally spaced along EACH girder
//    (plan note on the top of slab sheets). Girders A-E of Unit 2 begin on Pier 6A (point 1 = the auxiliary support).
//  * Per point and girder: camber (in, up +), deflection due to steel and due to slab+barrier (in, down +).
//  * Blocking DATUM = the end of the girder's blocking row that the plans print as 0.00.  Both ends 0.00, or neither, or the datum in a
//    discontinuous girder's un-numbered tail => no datum is set (reported); nothing is substituted.
//  * Discontinuous girders (I-80 U2 B-E, U3 B-C; Terminations): the main tables stop before the termination pier. Their un-numbered "DISCONTINUOUS
//    GIRDERS" tail tables (PDF 140-146, 152-155; printed order) are loaded as the points from the termination pier to the girder end: n values =
//    n-1 equal spaces from the CL bearing of the termination pier to the girder end (key `GirderEnd` = pier + the Termination distance), numbered from
//    the pier's point number. This is the mapping of PDF 205's "MEASURED ALONG GIRDER x" layout (value counts = printed spaces + 1); it is
//    CONFIRMED only by check-blocking.csx (haunch elevation / camber relation / blocking of the tail points), not stated on the plans in words.
//    Tail deflection tables: first of the two per girder (page order) = slab+barrier, second = steel (check-blocking.csx tests this).
using System.Globalization;
using SBM.Core.Bridge;

const string Br = "I80", PlanBr = "I-80";
int[] units = { 1, 2, 3 };
string BridgeName(int u) => $"I-80 WB Unit {u}";

const bool TailTable0IsSlab = true;
string dataDir = new[] { @"docs\plan-data\camber", @"C:\SBM\docs\plan-data\camber" }.First(System.IO.Directory.Exists);
string tosCsv = new[] { @"samples\plans\top-of-slab-i80-rampg.csv", @"C:\SBM\samples\plans\top-of-slab-i80-rampg.csv" }.First(System.IO.File.Exists);
double Num(string s) => double.Parse(s.Trim().TrimEnd('*'), CultureInfo.InvariantCulture);
bool Has(string s) => s.Trim().Length > 0;
// rows[r][c] of a plan table (first column = point or letter); returns header, rows
List<string[]> Table(string name) => System.IO.File.ReadAllLines(System.IO.Path.Combine(dataDir, name)).Select(l => l.Split(',')).ToList();

var bpts = new Dictionary<(string, int), List<(int Pt, string Kind, string Num)>>();
foreach (var ln in System.IO.File.ReadAllLines(tosCsv))
{
    if (!ln.StartsWith("B,")) continue;
    var c = ln.Split(',');
    var k = (c[1], int.Parse(c[2]));
    if (!bpts.ContainsKey(k)) bpts[k] = new();
    bpts[k].Add((int.Parse(c[3]), c[4], c[5]));
}

// tail values of one girder in printed order (TailTables: the two deflection tables per girder in page order)
List<List<double>> TailTables(string file, int g)
{
    var res = new List<List<double>>();
    var rows = Table(file).Skip(1).Where(r => r[0] == ((char)('A' + g)).ToString()).ToList();
    foreach (var grp in rows.GroupBy(r => r[1]).OrderBy(x => int.Parse(x.Key))) res.Add(grp.OrderBy(r => int.Parse(r[2])).Select(r => Num(r[3])).ToList());
    return res;
}
List<double> TailValues(string file, int g) =>
    Table(file).Skip(1).Where(r => r[0] == ((char)('A' + g)).ToString()).OrderBy(r => int.Parse(r[1])).Select(r => Num(r[2])).ToList();

int totalPts = 0, totalGirders = 0;
foreach (int u in units)
{
    var b = Doc.Project.Bridges.First(x => x.Name == BridgeName(u));
    int nsup = b.Supports.Count;
    var keysAll = bpts[(PlanBr, u)].OrderBy(x => x.Pt).ToList();
    var supportPts = keysAll.Where(x => x.Kind != "FS").Select(x => x.Pt).ToList();
    if (supportPts.Count != nsup) { Print($"!! {b.Name}: plan has {supportPts.Count} support points, model {nsup} supports - skipped"); continue; }

    var camber = Table($"{Br}-unit{u}-camber_in.csv");
    var steel = Table($"{Br}-unit{u}-defl_steel_in.csv");
    var slab = Table($"{Br}-unit{u}-defl_slab_in.csv");
    var block = Table($"{Br}-unit{u}-blocking_ft.csv");
    var hdr = camber[0];
    // blocking-letter points must be exactly the key points
    var letterPts = block.Skip(1).Select(r => int.Parse(r[1])).OrderBy(x => x).ToList();
    var keyPts = keysAll.Select(x => x.Pt).OrderBy(x => x).ToList();
    if (!letterPts.SequenceEqual(keyPts)) Print($"!! {b.Name}: blocking letter points [{string.Join(",", letterPts)}] differ from key points [{string.Join(",", keyPts)}]");

    b.Girders.Camber = new();
    for (int col = 1; col < hdr.Length; col++)
    {
        int g = hdr[col].Last() - 'A';
        if (g < 0 || g >= b.Girders.Count) { Print($"!! {b.Name}: column {hdr[col]} has no girder"); continue; }
        var gc = new GirderCamberDefinition { Girder = g };
        // girder start: a girder with a Beginning starts on its auxiliary support (point 1)
        var beg = b.Girders.Beginnings.FirstOrDefault(x => x.Girder == g);
        int firstSupport = 0;
        if (beg != null) { int k = b.AuxiliarySupports.FindIndex(a => a.Name == beg.Support); if (k >= 0) firstSupport = nsup + k; }
        foreach (var kp in keysAll)
        {
            if (kp.Kind == "FS") gc.Keys.Add(new CamberKeyPoint { Number = kp.Pt, Splice = int.Parse(kp.Num) });
            else
            {
                int si = supportPts.IndexOf(kp.Pt);
                gc.Keys.Add(new CamberKeyPoint { Number = kp.Pt, Support = si == 0 ? firstSupport : si });
            }
        }
        // values
        for (int r = 1; r < camber.Count; r++)
        {
            if (!Has(camber[r][col]) || !Has(steel[r][col]) || !Has(slab[r][col])) continue;
            int pt = int.Parse(camber[r][0]);
            if (int.Parse(steel[r][0]) != pt || int.Parse(slab[r][0]) != pt) throw new Exception("tables misaligned");
            gc.Points.Add(new CamberPoint { Number = pt, Camber = In(Num(camber[r][col])), SteelDeflection = In(Num(steel[r][col])), SlabDeflection = In(Num(slab[r][col])) });
        }
        // discontinuous girder: tail from the termination pier to the girder end
        var term = b.Girders.Terminations.FirstOrDefault(x => x.Girder == g);
        int endPoint = -1;
        if (term != null)
        {
            var cT = TailValues($"{Br}-unit{u}-camber_in-discontinuous-unnumbered.csv", g);
            var oT = TailTables($"{Br}-unit{u}-other_DEFLECTION_ORDINATES_OF_DISCON-unnumbered.csv", g);
            int pier = supportPts[term.Support];
            if (cT.Count == 0 || oT.Count != 2 || oT[0].Count != cT.Count || oT[1].Count != cT.Count)
                Print($"!! {b.Name} girder {hdr[col].Last()}: tail tables missing or of unequal length - tail not loaded");
            else
            {
                gc.Points.RemoveAll(p => p.Number >= pier);          // the main tables end before the termination pier
                gc.Keys.RemoveAll(k => k.Number > pier);
                endPoint = pier + cT.Count - 1;
                gc.Keys.Add(new CamberKeyPoint { Number = endPoint, GirderEnd = true });
                var slabT = TailTable0IsSlab ? oT[0] : oT[1]; var steelT = TailTable0IsSlab ? oT[1] : oT[0];
                for (int i = 0; i < cT.Count; i++)
                    gc.Points.Add(new CamberPoint { Number = pier + i, Camber = In(cT[i]), SlabDeflection = In(slabT[i]), SteelDeflection = In(steelT[i]) });
            }
        }
        // datum: the end of the blocking row printed 0.00
        // a starred value (0.00*) is "location at end of discontinuous girder" = the girder end point (its own numbering)
        var vals = block.Skip(1).Where(r => Has(r[col + 1])).Select(r => (Pt: r[col + 1].Contains('*') && endPoint > 0 ? endPoint : int.Parse(r[1]), V: Num(r[col + 1]))).OrderBy(x => x.Pt).ToList();
        string why = null;
        if (vals.Count == 0) why = "no blocking values printed";
        else
        {
            bool first0 = Math.Abs(vals[0].V) < 1e-9, last0 = Math.Abs(vals[^1].V) < 1e-9;
            if (first0 == last0) why = first0 ? "both ends print 0.00" : "neither end prints 0.00";
            else
            {
                int dp = first0 ? vals[0].Pt : vals[^1].Pt;
                if (gc.Points.All(p => p.Number != dp)) why = $"the datum (point {dp}, 0.00) is not among the loaded points";
                else gc.DatumPoint = dp;
            }
        }
        if (why != null) Print($"  {b.Name} girder {hdr[col].Last()}: no blocking datum - {why}");
        b.Girders.Camber.Add(gc);
        totalPts += gc.Points.Count; totalGirders++;
    }
}
Print($"Camber data set on {totalGirders} girders, {totalPts} plan points.");
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var u in units)
{
    var f = Doc.Model(BridgeName(u)).Framing;
    foreach (var w in f.Camber?.Warnings ?? Enumerable.Empty<string>()) Print($"  {BridgeName(u)}: {w}");
}
Doc.Save();
