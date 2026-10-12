// Woodbury County Design 817 (I-29 NB over Floyd Blvd) - mild reinforcing of the Iowa standard BTD beams (span 1 BTD55, span 2 BTD105,
// span 3 BTD60) FROM THE STANDARD SHEETS (user rule: standard beams are modeled from the standard):
//   4730 (data + bent bar details + bar list), 4732 BTD55, 4733 BTD60, 4742 BTD105.
// The plan set prints the standard sheets it was built with (PDF p.27-30 = DS 25-28, sheets issued 05-04 / rev. 08-09 (4742 rev. 10-07));
// that edition GOVERNS this bridge ("model the plans"). The current re-issue (04-2024, dist\standards\Iowa Beams.pdf p.98 / 100 / 101 / 110)
// moved the end-zone chain (3" + 5 @ 4 1/2" + 5", 6b4 4 positions, 4e1 15) and the 5a1 lengths (laps 3'-5): the catalog has both
// (StandardBeamRebar.IowaBtd(designation, edition)); this script reports the 2024 weights for comparison.
// Called by build-woodbury.csx before regeneration (re-runnable: replaces PrestressedBeamDefinition.Reinforcement).
//   sbm run samples\plans\beam-rebar-woodbury.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"
using SBM.Core.Bridge;
using SBM.Core.Prestressed;

var br = Doc.Project.Bridges.First(b => b.Girders.IsPrestressed);
foreach (var d in br.Girders.Prestressed.AllDefinitions())
{
    d.Reinforcement = StandardBeamRebar.IowaBtd(d.Designation, IowaBtdEdition.Issue2004Rev2009)
        ?? throw new Exception($"no Iowa BTD reinforcing for {d.Designation}");
    var cur = StandardBeamRebar.IowaBtd(d.Designation, IowaBtdEdition.Reissue2024)!;
    Print($"Woodbury {d.Designation}: plan edition {d.Reinforcement.ListedWeightLb:N0} lb/beam ({d.Reinforcement.Source}); " +
          $"2024 re-issue {cur.ListedWeightLb:N0} lb/beam ({cur.Source})");
}
