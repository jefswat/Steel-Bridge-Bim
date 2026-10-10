// Checks the model against the plan bearing data tables: "top of bearing elevation" (= bottom of flange at the CL
// bearing) for every girder, and the seat = top of bearing - bearing height. Values in girder order (bearings present).
// I-80 WB Design 116 sheets 156-158; Ramp G Design 316 sheets 85-86.
var plan = new List<(string Bridge, string Support, double[] Tob)>
{
    ("I-80 WB Unit 1", "West Abut", new[] { 997.89, 998.26, 998.62, 998.53, 998.36, 998.19 }),
    ("I-80 WB Unit 1", "Pier 1WB", new[] { 1000.41, 1000.79, 1001.15, 1001.07, 1000.91, 1000.75 }),
    ("I-80 WB Unit 1", "Pier 2WB", new[] { 1003.96, 1004.32, 1004.68, 1004.59, 1004.43, 1004.26 }),
    ("I-80 WB Unit 1", "Pier 3WB", new[] { 1007.38, 1007.75, 1008.11, 1008.02, 1007.85, 1007.68 }),
    ("I-80 WB Unit 1", "Pier 4WB", new[] { 1010.73, 1010.96, 1011.17, 1010.94, 1010.63, 1010.32 }),
    ("I-80 WB Unit 1", "Pier 5WB", new[] { 1013.68, 1013.97, 1014.18, 1013.96, 1013.68, 1013.40 }),
    ("I-80 WB Unit 2", "Pier 6A", new[] { 1011.99, 1012.38, 1012.76, 1013.16, 1013.48 }),
    ("I-80 WB Unit 2", "Pier 5WB", new[] { 1013.72, 1014.01, 1014.24, 1014.01, 1013.74, 1013.46 }),
    ("I-80 WB Unit 2", "Pier 6WB", new[] { 1014.25, 1014.54, 1014.85, 1015.13, 1015.33, 1015.54, 1015.88, 1016.11, 1016.07, 1016.03, 1015.99 }),
    ("I-80 WB Unit 2", "Pier 7WB", new[] { 1015.83, 1016.06, 1016.32, 1016.51, 1016.73, 1017.07, 1017.33, 1017.58, 1017.83, 1018.08 }),
    ("I-80 WB Unit 2", "Pier 8WB", new[] { 1015.39, 1015.62, 1015.85, 1016.12, 1016.56, 1017.03, 1017.51, 1018.02, 1018.54 }),
    ("I-80 WB Unit 2", "Pier 9WB", new[] { 1013.94, 1014.30, 1014.65, 1015.28, 1015.90, 1016.53, 1017.16, 1017.79 }),
    ("I-80 WB Unit 2", "Pier 10WB", new[] { 1013.07, 1013.70, 1014.34, 1014.97, 1015.61, 1016.24, 1016.88 }),
    ("I-80 WB Unit 2", "Pier 11WB", new[] { 1012.29, 1012.94, 1013.58, 1014.23, 1014.87, 1015.52, 1016.16 }),
    ("I-80 WB Unit 3", "Pier 11WB", new[] { 1012.24, 1012.98, 1013.69, 1014.41, 1015.05, 1015.69, 1016.34, 1016.99, 1017.63, 1018.28 }),
    ("I-80 WB Unit 3", "Pier 12WB", new[] { 1011.94, 1012.38, 1012.82, 1013.45, 1014.10, 1014.75, 1015.40, 1016.05, 1016.70, 1017.35 }),
    ("I-80 WB Unit 3", "Pier 13WB", new[] { 1011.64, 1011.98, 1012.34, 1013.00, 1013.65, 1014.31, 1014.97, 1015.62, 1016.28 }),
    ("I-80 WB Unit 3", "Pier 14WB", new[] { 1010.90, 1011.24, 1011.90, 1012.56, 1013.23, 1013.89, 1014.55, 1015.21 }),
    ("I-80 WB Unit 3", "Pier 15WB", new[] { 1009.79, 1010.14, 1010.81, 1011.47, 1012.14, 1012.81, 1013.48, 1014.15 }),
    ("I-80 WB Unit 3", "East Abut", new[] { 1008.95, 1009.30, 1009.97, 1010.64, 1011.32, 1011.99, 1012.66, 1013.33 }),
    ("Ramp G Unit 1", "South Abut", new[] { 996.26, 996.46, 996.65, 996.84 }),
    ("Ramp G Unit 1", "Pier 1G", new[] { 1001.98, 1002.17, 1002.37, 1002.56 }),
    ("Ramp G Unit 1", "Pier 2G", new[] { 1009.70, 1009.61, 1009.52, 1009.43 }),
    ("Ramp G Unit 1", "Pier 3G", new[] { 1017.52, 1016.95, 1016.38, 1015.81 }),
    ("Ramp G Unit 1", "Pier 4G", new[] { 1025.12, 1024.55, 1023.98, 1023.41 }),
    ("Ramp G Unit 1", "Pier 5G", new[] { 1031.14, 1030.59, 1030.02, 1029.45 }),
    ("Ramp G Unit 2", "Pier 5G", new[] { 1031.23, 1030.70, 1030.13, 1029.56 }),
    ("Ramp G Unit 2", "Pier 6G", new[] { 1037.67, 1037.12, 1036.55, 1035.98 }),
    ("Ramp G Unit 2", "Pier 7G", new[] { 1042.97, 1042.41, 1041.84, 1041.27 }),
    ("Ramp G Unit 2", "Pier 8G", new[] { 1045.09, 1044.53, 1043.95, 1043.38 }),
    ("Ramp G Unit 2", "Pier 9G", new[] { 1044.87, 1044.33, 1043.76, 1043.19 }),
    ("Ramp G Unit 3", "Pier 9G", new[] { 1043.95, 1043.42, 1042.85, 1042.28 }),
    ("Ramp G Unit 3", "Pier 10G", new[] { 1042.91, 1042.36, 1041.79, 1041.22 }),
    ("Ramp G Unit 3", "Pier 11G", new[] { 1041.60, 1041.03, 1040.46, 1039.89 }),
    ("Ramp G Unit 3", "Pier 12G", new[] { 1038.42, 1037.86, 1037.29, 1036.72 }),
    ("Ramp G Unit 3", "Pier 13G", new[] { 1033.18, 1032.61, 1032.04, 1031.47 }),
    ("Ramp G Unit 3", "Pier 14G", new[] { 1029.03, 1028.49, 1027.92, 1027.35 }),
    ("Ramp G Unit 4", "Pier 14G", new[] { 1029.02, 1028.45, 1027.88, 1027.31 }),
    ("Ramp G Unit 4", "Pier 15G", new[] { 1025.01, 1024.59, 1024.16, 1023.74 }),
    ("Ramp G Unit 4", "Pier 16G", new[] { 1020.21, 1020.09, 1020.06, 1020.13 }),
    ("Ramp G Unit 4", "Pier 17G", new[] { 1014.03, 1014.39, 1014.76, 1015.13 }),
    ("Ramp G Unit 4", "Pier 18G", new[] { 1011.34, 1011.91, 1012.48, 1013.05 }),
    ("Ramp G Unit 4", "Pier 19G", new[] { 1010.95, 1011.51, 1012.07, 1012.63 }),
};
double worstAll = 0;
foreach (var (bn, sn, tob) in plan)
{
    var b = Doc.Project.Bridges.First(x => x.Name == bn);
    var f = Doc.Model(bn).Framing;
    int j = Enumerable.Range(0, b.Supports.Count + b.AuxiliarySupports.Count).First(k => b.SupportAt(k).Name == sn);
    var brs = f.SupportLine(j).Bearings.Where(x => x != null).ToList();
    if (brs.Count != tob.Length) { Print($"{bn} {sn}: {brs.Count} bearings in the model, {tob.Length} in the table"); continue; }
    var d = brs.Zip(tob, (br, t) => br!.BottomOfSteelElevation / Ft(1) - t).ToList();
    worstAll = Math.Max(worstAll, d.Max(Math.Abs));
    Print($"{bn,-15} {sn,-11} model - plan top of bearing: {string.Join(" ", d.Select(x => $"{x * 12,5:+0.0;-0.0}"))} in");
}
Print($"Largest difference {worstAll * 12:F2} in");
