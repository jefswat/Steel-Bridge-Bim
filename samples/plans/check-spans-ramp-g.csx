// Ramp G Units 1-4: span lengths per girder (CL bearing / CL pier) vs the plan GIRDER DATA (PDF 90-91, 94, 97, 101). Does not save.
using SBM.Core.Bridge;
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
var plan = new Dictionary<string, double[][]>
{
    ["Ramp G Unit 1"] = new[]   // PDF 90 (tangent, radial: CL dims) and GIRDER DATA PDF 91
    {
        new[] { F(145), F(145), F(145), F(145) },
        new[] { F(190), F(190), F(190), F(190) },
        new[] { F(190, 5.75), F(189, 5.4375), F(188, 5.125), F(187, 4.8125) },
        new[] { F(190, 10.8125), F(188, 11.625), F(187, 0.375), F(185, 1.1875) },
        new[] { F(149, 2.5), F(147, 8.1875), F(146, 1.875), F(144, 7.5625) },
    },
    ["Ramp G Unit 2"] = new[]
    {
        new[] { F(174, 3.9375), F(172, 6.5625), F(170, 9.1875), F(168, 11.8125) },
        new[] { F(214, 0.125), F(211, 10.125), F(209, 8.0625), F(207, 6.0625) },
        new[] { F(202, 11.5), F(200, 10.8125), F(198, 10.125), F(196, 9.5) },
        new[] { F(159, 3.125), F(157, 7.5625), F(156, 0), F(154, 4.5) },
    },
    ["Ramp G Unit 3"] = new[]
    {
        new[] { F(199, 5.375), F(197, 4.9375), F(195, 4.5), F(193, 4.125) },
        new[] { F(256, 2.5), F(253, 7.375), F(251, 0.25), F(248, 5.0625) },
        new[] { F(221, 0.5), F(218, 9.625), F(216, 6.75), F(214, 3.9375) },
        new[] { F(226, 0.8125), F(223, 9.3125), F(221, 5.8125), F(219, 2.375) },
        new[] { F(179, 4.25), F(177, 6.25), F(175, 8.25), F(173, 10.3125) },
    },
    ["Ramp G Unit 4"] = new[]
    {
        new[] { F(166, 3.5), F(164, 7.125), F(162, 10.6875), F(161, 2.3125) },
        new[] { F(191, 2.5625), F(205, 9.5), F(220, 4.4375), F(234, 11.375) },
        new[] { F(259, 0.3125), F(256, 9.5625), F(254, 5.8125), F(252, 1) },
        new[] { F(209, 7.125), F(199, 9.0625), F(190, 0.0625), F(180, 4.0625) },
        new[] { F(146, 7.8125), F(153, 10.375), F(161, 0.75), F(168, 2.875) },
    },
};
Doc.Regenerate();
foreach (var (name, spans) in plan)
{
    var fr = Doc.Model(name).Framing;
    var b = Doc.Project.Bridges.First(x => x.Name == name);
    Print($"--- {name} (model - plan, inches); supports {string.Join(", ", b.Supports.Select(s => s.Name))}");
    for (int j = 0; j < spans.Length; j++)
        Print($"  span {j + 1}: " + string.Join("  ", Enumerable.Range(0, 4).Select(g => $"{"ABCD"[g]} {(fr.Girders[g].Spans[j].Length - spans[j][g]) / 0.0254,7:F2}")));
}

