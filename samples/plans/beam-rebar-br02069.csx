// MnDOT Br. 02069 (T.H. 65 S.B. over 117th Ave) - mild reinforcing of the 30MH-101 beams (p16, Fig. 5-397.501 (mod.); standard
// MnDOT Fig. 5-397.501(B) "30MH ... with debonded strands", dist\standards\MnDOT fig7501(B) 30MH debonded.pdf).
// Called by build-br02069.csx before regeneration (re-runnable: replaces PrestressedBeamDefinition.Reinforcement).
//   sbm run samples\plans\beam-rebar-br02069.csx "samples\plans\Br02069 TH65.sbm"
// Plan data: docs/plan-data/br02069-th65/SUMMARY.md sec. 3 (beam bars) + bend-diagrams.csv (G bars); U11 items.
// Bar SHAPES and section positions are the standard's (same dims printed on p16); the stirrup CHAIN is the plan's (a designer input
// on the standard): each half from the end of beam 2" + 4 sps @ 2 1/2" + 9 sps @ 4" + 19 sps @ 9" + 27 sps @ 14" + 6" to CL span
// (2 + 10 + 36 + 171 + 378 + 6 = 603" = 100'-6" / 2) -> 60 stations per half, 120 per beam.
using SBM.Core.Bridge;
using SBM.Core.Prestressed;

var br = Doc.Project.Bridges.First(b => b.Name == "Br02069 TH65 SB");
var stations = PrestressedBeamRebar.Chain(2, (4, 2.5), (9, 4), (19, 9), (27, 14));
if (Math.Abs(stations[^1] + 6 - 603) > 1e-9) throw new Exception("chain does not close on CL span");
// first station 2-G505E, the other four stations of the 2 1/2" group 2-G508E (U11 reading, see the note in the definition)
var rd = StandardBeamRebar.MnDot30Mh(stations, 1, 4,
    "MnDOT Br. 02069 p16 (30MH-101, Fig. 5-397.501 mod.) + Fig. 5-397.501(B)", "Br02069 p16");
foreach (var d in br.Girders.Prestressed.AllDefinitions()) d.Reinforcement = rd.Clone();
Print($"Br02069 beam reinforcing set: {stations.Count} stirrup stations per half; G505E 1, G508E 4, G402E {stations.Count - 5} per half");
