// Fix pass 4 (user item, 2026-10-21): distributed joint rivet groups - grid spacing per member side of each joint.
// Reads the generator's Info lines "Truss: joint J (P) member M: n rivets d (k per gusset face, grid s)" and writes one CSV row per group
// plus, per joint, the ratio between the coarsest and the finest chord-side grid.
//   sbm run rivet_grid.csx model.sbm -- out.csv
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
var inv = CultureInfo.InvariantCulture;
string outPath = Args.Length > 0 ? Args[0] : "rivet_grid.csv";
Doc.Regenerate();
double ParseIn(string s)
{
    s = s.Replace("\"", "").Trim(); double v = 0;
    foreach (var p in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        v += p.Contains('/') ? double.Parse(p.Split('/')[0], inv) / double.Parse(p.Split('/')[1], inv) : double.Parse(p, inv);
    return v;
}
var rx = new Regex(@"^Truss: joint (\S+) \((\w+)\) member (\S+): (\d+) rivets (.+?)"" \((\d+) per (gusset face|member face beyond the gusset), grid ([^)]+?)""\)");
var w = new StreamWriter(outPath);
w.WriteLine("bridge,joint,plane,member,region,per_face,dia_in,grid_in,grid_over_dia");
foreach (var b in Doc.Project.Bridges.Where(b => b.Truss != null))
{
    var st = Doc.Model(b.Name).Steel;
    if (st == null) continue;
    foreach (var d in st.Diagnostics)
    {
        var m = rx.Match(d.Message);
        if (!m.Success) continue;
        double dia = ParseIn(m.Groups[5].Value), g = ParseIn(m.Groups[8].Value);
        w.WriteLine(string.Join(",", "\"" + b.Name + "\"", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[7].Value.StartsWith("gusset") ? "inside" : "beyond",
            m.Groups[6].Value, dia.ToString("0.###", inv), g.ToString("0.###", inv), (g / dia).ToString("0.00", inv)));
    }
}
w.Close();
Print("written " + outPath);
