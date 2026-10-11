// Checks the model's finished grade (top of slab) at the girder bearings against the plan's "Table of top of slab
// elevations" (I-80 Design 116, Unit 2 sheet 195: points 1 = CL brg Pier 5WB/6A, 25 = CL Pier 6WB, 55 = CL Pier 7WB).
// NaN = no value in the table (discontinuous girder).
double N = double.NaN;
var plan = new List<(string Bridge, string Support, double[] Tos)>
{
    ("I-80 WB Unit 2", "Pier 6A", new[] { 1021.52 /* A at point 1 (6A), sheet 195 / PDF 202; was 1021.62 = point 2 */, 1021.91, 1022.29, 1022.68, 1023.01 }),
    ("I-80 WB Unit 2", "Pier 5WB", new[] { N, N, N, N, N, 1023.25, 1023.54, 1023.77, 1023.54, 1023.27, 1023.00 }),
    ("I-80 WB Unit 2", "Pier 6WB", new[] { 1023.87, 1024.17, 1024.47, 1024.75, N, 1025.17, 1025.51, 1025.74, 1025.70, 1025.66, 1025.62 }),
    ("I-80 WB Unit 2", "Pier 7WB", new[] { 1025.45, 1025.68, 1025.94, N, N, 1026.36, 1026.70, 1026.95, 1027.20, 1027.46, 1027.71 }),
};
foreach (var (bn, sn, tos) in plan)
{
    var b = Doc.Project.Bridges.First(x => x.Name == bn);
    var al = Doc.Alignment(b.AlignmentName);
    var f = Doc.Model(bn).Framing;
    int j = Enumerable.Range(0, b.Supports.Count + b.AuxiliarySupports.Count).First(k => b.SupportAt(k).Name == sn);
    var line = f.SupportLine(j);
    var cells = new List<string>();
    for (int g = 0; g < tos.Length; g++)
    {
        if (double.IsNaN(tos[g])) { cells.Add("   . "); continue; }
        var br = line.Bearings.ElementAtOrDefault(g);
        if (br == null) { cells.Add("  n/a"); continue; }
        var so = al.Inverse(br.TopOfSteel.XY);
        double z = b.Deck.FinishedElevation(al, so.Station, so.Offset);
        cells.Add($"{(z / Ft(1) - tos[g]) * 12,5:+0.0;-0.0}");
    }
    Print($"{bn,-15} {sn,-9} model - plan top of slab (in): {string.Join(" ", cells)}");
}
