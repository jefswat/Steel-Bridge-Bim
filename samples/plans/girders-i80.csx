// Discontinuous girders of WB I-80 Units 2 and 3 at the piers, from the pier cap plans (design sheets 41-77): the plans
// dimension every girder along the CL pier; converted here to offsets normal to CL WB approach roadway (x cos skew,
// measured from the roadway girder: H in Unit 2, G in Unit 3). Replaces the even "spread between A and F" placeholder.
// Run after build-i80-wb.csx. Values in feet, left negative.
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
// Unit 3 girder ends (girder elevations, design sheet 115): B 60'-3 11/16 past Pier 12WB, C 53'-2 11/16 past Pier 13WB.
u3.Girders.Terminations = new()
{
    new SBM.Core.Bridge.GirderTermination { Girder = 1, Support = 1, Distance = Ft(60 + 3.6875 / 12) },
    new SBM.Core.Bridge.GirderTermination { Girder = 2, Support = 2, Distance = Ft(53 + 2.6875 / 12) },
};
var targets = new List<(SBM.Core.Bridge.BridgeDefinition b, int support, int girder, double ft)>();
void Set(SBM.Core.Bridge.BridgeDefinition b, int support, params (int girder, double ft)[] v)
{
    foreach (var (g, ft) in v)
    {
        targets.Add((b, support, g, ft));
        double old = b.Girders.OffsetsAtSupports[support][g] / Ft(1);
        b.Girders.OffsetsAtSupports[support][g] = Ft(ft);
        if (Math.Abs(old - ft) > 0.05) Print($"  {b.Name} {b.Supports[support].Name} girder {(char)('A' + g)}: {old:F2} -> {ft:F2} ft");
    }
}
// Unit 2 (A=0 .. E=4; F-K unchanged): 6WB, 7WB, 8WB (skew 42 41'27"), 9WB (38 38'00"), 10WB (33 43'51").
Set(u2, 1, (0, -66.23), (1, -56.33), (2, -46.26), (3, -36.96), (4, -29.98));
Set(u2, 2, (0, -53.21), (1, -45.40), (2, -36.81), (3, -30.36));
Set(u2, 3, (0, -43.92), (1, -37.29), (2, -30.69));
Set(u2, 4, (0, -36.24), (1, -29.48));
Set(u2, 5, (0, -34.62));
// Unit 3 (A=0, B=1, C=2): 12WB (23 40'36"), 13WB (18 48'56"), 14WB (14 04'22"), 15WB (9 19'56").
Set(u3, 1, (0, -61.30), (1, -53.58), (2, -45.64));
Set(u3, 2, (0, -46.87), (2, -40.92));
Set(u3, 3, (0, -40.51));
Set(u3, 4, (0, -40.49));
// Girders that have ended before a pier keep only a placeholder offset there (they have no bearing); keep the list in
// order by placing any out-of-order placeholder midway between its neighbors.
foreach (var b in new[] { u2, u3 })
    for (int j = 0; j < b.Girders.OffsetsAtSupports.Count; j++)
    {
        var o = b.Girders.OffsetsAtSupports[j];
        for (int g = 1; g + 1 < o.Count; g++)
            if (o[g] <= o[g - 1] + Ft(0.5) || o[g] >= o[g + 1] - Ft(0.5))
            {
                int nx = g + 1;
                while (nx + 1 < o.Count && o[nx] <= o[g - 1] + Ft(0.5)) nx++;
                o[g] = (o[g - 1] + o[nx]) / 2;
                Print($"  {b.Name} {b.Supports[j].Name}: placeholder for girder {(char)('A' + g)} moved to {o[g] / Ft(1):F2} ft");
            }
    }
// The plan positions are at the bearings, which on a skewed pier sit at other stations than the support's alignment
// point; a flaring girder's offset changes in between. Correct the inputs until every bearing is on its plan position.
for (int it = 0; it < 5; it++)
{
    Doc.Regenerate();
    double worst = 0;
    foreach (var (b, j, g, ft) in targets)
    {
        var br = Doc.Model(b.Name).Framing.Supports[j].Bearings[g];
        if (br == null) { if (it == 0) Print($"  {b.Name} {b.Supports[j].Name} girder {(char)('A' + g)}: no bearing in the model (plans show one)"); continue; }
        double actual = Doc.Alignment(b.AlignmentName).Inverse(br.TopOfSteel.XY).Offset / Ft(1);
        double err = ft - actual;
        worst = Math.Max(worst, Math.Abs(err));
        b.Girders.OffsetsAtSupports[j][g] += Ft(err);
    }
    Print($"  pass {it + 1}: largest bearing offset error {worst * 12:F2} in");
    if (worst < 0.01) break;
}
var r = Doc.Regenerate();
Print($"Regenerated {r.Bridges} bridges in {r.Elapsed.TotalSeconds:F1} s, {r.Errors.Count} errors.");
foreach (var e in r.Errors) Print("  ERROR " + e);
Doc.Save();
