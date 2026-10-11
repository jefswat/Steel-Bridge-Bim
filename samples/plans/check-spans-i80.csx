// I-80 WB Units 1-3: span length per girder (CL bearing to CL bearing along the girder, horizontal) vs the plan GIRDER DATA tables
// (design sheets 106 [PDF 107, Unit 1], 106/107A/108 [PDF 108, 110, 111, Unit 2], 112/113A [PDF 115, 117, Unit 3]; "dimensions are horizontal
// with no allowance for grade"). Read from 200-240 dpi grayscale clips. Does not save; nothing is adjusted. Tolerance = plan precision 1/16".
// Spans a girder only partly covers (it ends or begins inside the span: Unit 2 D 8WB, C 9WB, B 10WB, E 7WB, Unit 3 B 13WB, C 14WB) are left
// out (null). Span index: Unit 1 0..4 = 1WB..5WB; Unit 2 0 = 6WB (from 5WB / 6A), 1..5 = 7WB..11WB; Unit 3 0..2 = 12WB..14WB.
using SBM.Core.Bridge;
double F(double ft, double inch = 0, double n = 0, double d = 16) => Ft(ft) + In(inch + (n == 0 ? 0 : n / d));
double? N = null;
double?[] R(params double?[] v) => v;
var plan = new (string unit, string[] girders, (string span, double?[] len)[] spans)[]
{
    ("I-80 WB Unit 1", new[] { "A", "B", "C", "D", "E", "F" }, new[]
    {
        ("1WB", R(F(205, 9, 15), F(206, 5, 1), F(207, 0, 3), F(207, 7, 5), F(208, 2, 7), F(208, 9, 8))),
        ("2WB", R(F(279, 2, 1), F(278, 6, 15), F(277, 11, 13), F(277, 4, 11), F(276, 9, 9), F(276, 2, 8))),
        ("3WB", R(F(265), F(265), F(265), F(265), F(265), F(265))),
        ("4WB", R(F(261, 9, 3), F(250, 8, 15), F(239, 8, 10), F(228, 8, 5), F(217, 8), F(206, 7, 12))),
        ("5WB", R(F(235, 3), F(235, 3), F(235, 3), F(235, 3), F(235, 3), F(235, 3))),
    }),
    ("I-80 WB Unit 2", new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K" }, new[]
    {
        ("6WB", R(F(205, 8), F(206, 3, 15), F(207), F(207, 8), F(208, 2, 12), F(208, 6), F(208, 6), F(208, 6), F(208, 6), F(208, 6), F(208, 6))),
        ("7WB", R(F(260, 3, 15), F(260, 2, 12), F(260, 2, 1), F(260, 1), N, F(260), F(260), F(260), F(260), F(260), F(260))),
        ("8WB", R(F(193, 1, 11), F(199, 3, 8), F(205, 4, 11), N, N, F(212, 5), F(223, 3, 5), F(234, 1, 1), F(244, 10, 5), F(255, 7), F(266, 3, 4))),
        ("9WB", R(F(209, 3, 6), F(209, 1, 13), N, N, N, F(201, 9), F(201, 3, 7), F(200, 10, 2), F(200, 4, 15), F(199, 11, 15), F(199, 7, 2))),
        ("10WB", R(F(171, 4, 11), N, N, N, N, F(169, 4, 14), F(168, 8, 5), F(167, 11, 15), F(167, 3, 11), F(166, 7, 9), F(165, 11, 9))),
        ("11WB", R(F(144, 8, 10), N, N, N, N, F(143, 8, 6), F(142, 8, 3), F(141, 8, 3), F(140, 8, 4), F(139, 8, 7), F(138, 8, 11))),
    }),
    ("I-80 WB Unit 3", new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" }, new[]
    {
        ("12WB", R(F(166, 0, 12), F(162, 9, 14), F(160, 2, 3), F(159, 2, 8), F(159, 0, 10), F(158, 10, 12), F(158, 8, 15), F(158, 7, 3), F(158, 5, 7), F(158, 3, 11))),
        ("13WB", R(F(202, 6, 15), N, F(198, 0, 11), F(196, 0, 4), F(195, 10, 2), F(195, 8), F(195, 5, 15), F(195, 3, 14), F(195, 1, 14), F(194, 11, 15))),
        ("14WB", R(F(197, 8, 12), N, N, F(195, 6, 3), F(195, 5, 2), F(195, 4, 2), F(195, 3, 2), F(195, 2, 2), F(195, 1, 2), F(195, 0, 3))),
    }),
};
Doc.Regenerate();
double worst = 0; int bad = 0, n = 0;
foreach (var (unit, names, spans) in plan)
{
    var fr = Doc.Model(unit).Framing;
    Print($"--- {unit} (model - plan, inches; '.' = not compared)");
    for (int j = 0; j < spans.Length; j++)
    {
        var sb = new System.Text.StringBuilder($"  span {spans[j].span,-5}");
        for (int g = 0; g < names.Length; g++)
        {
            double? p = spans[j].len[g];
            if (p == null || g >= fr.Girders.Count || j >= fr.Girders[g].Spans.Count) { sb.Append($" {names[g]}    .   "); continue; }
            double len = fr.Girders[g].Spans[j].Length;
            // Girders that begin on an auxiliary support line (Pier 6A, A-E): the span runs from that bearing, not from 5WB.
            if (j == 0 && fr.AuxSupports.FirstOrDefault(a => a.Bearings.Length > g && a.Bearings[g] != null)?.Bearings[g] is { } ab && !double.IsNaN(ab.PathDistance))
                len = fr.Girders[g].DistanceAt(1, 0) - ab.PathDistance;
            double d = (len - p.Value) / In(1);
            n++; worst = Math.Max(worst, Math.Abs(d));
            bool off = Math.Abs(d) > 1.0 / 16 + 1e-9; if (off) bad++;
            sb.Append($" {names[g]} {d,+6:F2}{(off ? "*" : " ")}");
        }
        Print(sb.ToString());
    }
}
Print($"{n} spans compared, worst {worst:F2} in, {bad} beyond 1/16 in (marked *).");
