// Camber / deflection / blocking-datum data of Ramp G Units 1-4 (Design 316, CAMBER & BLOCKING sheets PDF 111-128),
// from docs\plan-data\camber\RG-unit<n>-*.csv, onto GirderLayoutDefinition.Camber.  Re-runnable (replaces the camber data);
// run after build-ramp-g.csx and field-splices-ramp-g.csx.
//   sbm run samples\plans\camber-ramp-g.csx "<model>.sbm"
// Mapping (all from the plans, nothing assumed):
//  * Key points = the CL bearings (PIER/ABUT rows) and field splices (FS rows) of the plans' point numbering
//    (samples\plans\top-of-slab-i80-rampg.csv "B" rows); numbered points between two key points are equally spaced along EACH girder
//    (plan note on the top of slab sheets).
//  * Per point and girder: camber (in, up +), deflection due to steel and due to slab+barrier (in, down +).
//  * Blocking DATUM = the end of the girder's blocking row that the plans print as 0.00.  Both ends 0.00, or neither, or the datum in a
//    discontinuous girder's un-numbered tail => no datum is set (reported); nothing is substituted.
using System.Globalization;
using SBM.Core.Bridge;

const string Br = "RG", PlanBr = "RampG";
int[] units = { 1, 2, 3, 4 };
string BridgeName(int u) => $"Ramp G Unit {u}";

string dataDir = new[] { @"docs\plan-data\camber", @"C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\docs\plan-data\camber" }.First(System.IO.Directory.Exists);
string tosCsv = new[] { @"samples\plans\top-of-slab-i80-rampg.csv", @"C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\samples\plans\top-of-slab-i80-rampg.csv" }.First(System.IO.File.Exists);
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
        // datum: the end of the blocking row printed 0.00
        var vals = block.Skip(1).Where(r => Has(r[col + 1])).Select(r => (Pt: int.Parse(r[1]), V: Num(r[col + 1]))).OrderBy(x => x.Pt).ToList();
        string why = null;
        if (vals.Count == 0) why = "no blocking values printed";
        else
        {
            bool first0 = Math.Abs(vals[0].V) < 1e-9, last0 = Math.Abs(vals[^1].V) < 1e-9;
            if (first0 == last0) why = first0 ? "both ends print 0.00" : "neither end prints 0.00";
            else
            {
                int dp = first0 ? vals[0].Pt : vals[^1].Pt;
                if (gc.Points.All(p => p.Number != dp)) why = $"the datum (point {dp}, 0.00) is in the un-numbered tail (not loaded)";
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
