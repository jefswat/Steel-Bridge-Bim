// MnDOT Br. 02069 (T.H. 65 S.B. over 117th Ave) - semi-integral concrete END DIAPHRAGMS at both abutments + polystyrene fillers
// (concrete-bridge design feature C). Called by build-br02069.csx before regeneration (re-runnable: replaces SupportDefinition.Diaphragm).
//   sbm run samples\plans\diaphragms-br02069.csx "samples\plans\Br02069 TH65.sbm"
// Plan data: docs/plan-data/br02069-th65/SUMMARY.md sec. 3 (U2 closed) / sec. 4; MnDOT Fig. 5-397.560 (beam connection table, 30MH row).
// Rules: plan values only; items not dimensioned are UNRESOLVED (page + reason), never guessed.
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Br02069 TH65 SB");
DiaphragmSectionPoint P(double x, DiaphragmZRef r, double dz = 0) => new(x, r, dz);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
const DiaphragmZRef Sof = DiaphragmZRef.Soffit, Top = DiaphragmZRef.DeckTop, El = DiaphragmZRef.Elevation, Bot = DiaphragmZRef.Bottom;

// Section normal to the CL brg, x + toward the span (p.21 SECTION A-A between beams / B-B at beams, p.11 A-A + Detail A, p.20 plan):
//   back face = abutment back face 2'-6 behind CL brg (chain 1'-3 + 3" + 1'-0); CL end diaphragm 1'-0 behind CL brg (1'-6 + 1'-6); front face 6" ahead (3'-0 thick);
//   back part x -2'-6 .. -1'-3: on the back ledge on 1" Type A polystyrene (bottom = ledge + 1"); paving bracket at the back-face top 8" wide
//     (x -2'-6 .. -1'-10 = end of slab 1'-10 from CL brg), top 1'-4 below the deck top; behind the slab end the part top is the deck soffit.
//   3" Type B polystyrene between the diaphragm face x -1'-3 and the stem seat riser x -1'-0, from the ledge up to the 3" x 3" chamfer (p.11 Detail A).
//   front part x -1'-3 .. +6": bottom = BOTTOM OF THE BEAMS - p.21 A-A draws the diaphragm bottom between the beams as the continuation of the beam-bottom
//     line (vector linework: the diaphragm bottom and the beam bottom beyond are one line, 3'-6 below the deck top in the schematic = 9" slab + 3" + 30" beam),
//     B-B the same at the beams; p.11 A-A draws it 3" (the bearing) above the pedestal top; 3" x 3" chamfer at the back-bottom corner (p.11 Detail A);
//     front face 6" ahead of CL brg with the 1'-0 x 1'-0 fillet under the slab (A-A vertical 1'-0, B-B horizontal 1'-0; S405E). Closes user decision 14.
//   Bottom between the beams: straight between the bottom-flange edges (DiaphragmRiserRule.BeamBottoms); beyond the fascia beams level at the fascia
//   beam bottom (0'-10 1/2 each end; the plans draw no break there).
ConcreteDiaphragmDefinition End(string name, double ledge, string pages) => new()
{
    Kind = ConcreteDiaphragmKind.SemiIntegralEnd, Name = name, Source = pages,
    Material = "Structural concrete (cast with the deck; superstructure quantities)",
    Risers = DiaphragmRiserRule.BeamBottoms,
    OpenBelowBeams = true,          // the beams sit on bearings: under the beam bottoms is the bearing zone, not diaphragm concrete
    Parts =
    {
        new DiaphragmPart
        {
            Name = "back part",
            Section =
            {
                P(-F(2, 6), El, Ft(ledge) + In(1)), P(-F(1, 3), El, Ft(ledge) + In(1)), P(-F(1, 3), Sof), P(-F(1, 10), Sof),
                P(-F(1, 10), Top, -F(1, 4)), P(-F(2, 6), Top, -F(1, 4)),
            },
        },
        new DiaphragmPart
        {
            Name = "front part",
            Section =
            {
                P(-F(1, 3), Bot, -In(3)), P(-F(1, 0), Bot), P(In(6), Bot), P(In(6), Sof, -F(1, 0)), P(F(1, 6), Sof), P(-F(1, 3), Sof),
            },
        },
    },
    // 41'-0 = deck edges (U2 closed: mask-wall inner face -23'-7 / infill-wall W end +17'-7 less 1" polystyrene each end; p.3, p.5, p.11 Detail C)
    Left = DiaphragmEnd.AtOffset(-F(23, 6), "W end: 1\" polystyrene to the mask wall inner face (-23'-7), p.3 / p.11"),
    Right = DiaphragmEnd.AtOffset(F(17, 6), "E end: 1\" Type B polystyrene to the infill wall W end (+17'-7), p.11 Detail C"),
    // MnDOT Fig. 5-397.560 diaphragm dimension table, 30MH: E = 7 1/2" from the beam end, D = 1'-1 1/4" to the lower hole, C = 3 1/2" between holes, 1 1/2" dia.
    BeamHoles =
    {
        new DiaphragmBeamHole { FromEnd = In(7.5), AboveBottom = F(1, 1.25), Diameter = In(1.5), Source = "p.17 / Fig. 5-397.560 30MH: E 7 1/2, D 1'-1 1/4" },
        new DiaphragmBeamHole { FromEnd = In(7.5), AboveBottom = F(1, 1.25) + In(3.5), Diameter = In(1.5), Source = "Fig. 5-397.560 30MH: C 3 1/2" },
    },
    Fillers =
    {
        new DiaphragmFillerDefinition { Name = "polystyrene W end", Material = "1\" Type B polystyrene", Kind = DiaphragmFillerKind.EndFace, Side = -1, Thickness = In(1), Source = "p.3, p.19, p.21" },
        new DiaphragmFillerDefinition { Name = "polystyrene E end", Material = "1\" Type B polystyrene", Kind = DiaphragmFillerKind.EndFace, Side = 1, Thickness = In(1), Source = "p.11 Detail C" },
        new DiaphragmFillerDefinition
        {
            Name = "polystyrene on ledge", Material = "1\" Type A polystyrene", Kind = DiaphragmFillerKind.Section, Thickness = In(1), Source = "p.11 Detail A, p.21",
            Section = { P(-F(2, 6), El, Ft(ledge)), P(-F(1, 3), El, Ft(ledge)), P(-F(1, 3), El, Ft(ledge) + In(1)), P(-F(2, 6), El, Ft(ledge) + In(1)) },
        },
        new DiaphragmFillerDefinition
        {
            Name = "polystyrene at seat riser", Material = "3\" Type B polystyrene", Kind = DiaphragmFillerKind.Section, Thickness = In(3), Source = "p.11 Detail A, p.21 (the 3\" of the 1'-3 + 3\" + 1'-0 chain; ledge up to the 3\" x 3\" chamfer)",
            Section = { P(-F(1, 3), El, Ft(ledge)), P(-F(1, 0), El, Ft(ledge)), P(-F(1, 0), Bot, -In(3)), P(-F(1, 3), Bot, -In(3)) },
        },
    },
    Unresolved =
    {
        "1\" backer rod at the back face and 1/2\" x 7\" bituminous felt on the paving bracket (p.21, p.11 Detail A) not modeled (no solids requested)",
    },
};
br.Supports[0].Diaphragm = End("South Abutment end diaphragm", 920.75 + 19.25 / 12, "p.20, p.21, p.11 Detail A / C; ledge S = footing top 920.75 + 1'-7 1/4 (p.11 note 3) = 922.354");
br.Supports[1].Diaphragm = End("North Abutment end diaphragm", 920.75 + 16.75 / 12, "p.20, p.21, p.11 Detail A / C; ledge N = footing top 920.75 + 1'-4 3/4 (p.11 note 3) = 922.146");
br.Supports[0].Diaphragm!.Unresolved.Add("3\" dia. PVC sleeve (lighting conduit) through the S diaphragm 1'-0+/- from WL, 2'-0 below the top (p.20 note 1): position '+/-' - not modeled");
Print("Br02069 end diaphragms set: South / North Abutment");
