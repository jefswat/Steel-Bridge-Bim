// NCDOT B-2500 Bonner Unit 6 - cast-in-place BENT DIAPHRAGMS (interior bents 30-34, continuous for live load) and EXPANSION JOINT DIAPHRAGMS
// (unit ends: bent 29 modular joint, bent 35 Transflex joint). Called by build-bonner-unit6.csx before regeneration (re-runnable: replaces
// SupportDefinition.Diaphragm on the bents of "Bonner Unit 6").
//   sbm run samples\plans\diaphragms-bonner-unit6.csx "<model.sbm>"
// Plan data (PDF "B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf", p. = PDF page):
//   p.406 (S4-14) TYPICAL SECTION (interior bent diaphragms) + SECTION A-A: diaphragm 2'-4" = 1'-2" each side of the bent control line, ends 3'-7 1/2"
//          outside CL girders 1 / 4 at the bent control line (deck edge 4'-0 1/2" outside -> 5" inside the deck edge), bottom on the girder-bottom line.
//   p.407 (S4-15) SECTION L-L / M-M (continuous bent diaphragms): faces on the CL bearings (1'-2" from the bent control line), full depth from the deck
//          soffit to the bottom of the girders; SECTION K-K (expansion joint diaphragm at bent 29): 1'-10" (MIN.) from the girder end, bottom = the girder bottom line (re-read 2026-10-31: the 1'-3" is the lowest K302 row above
//          the girder bottom, 8" x 8" deck-to-diaphragm fillet on the span side; CL bearing 3'-2" from the bent control line, girder end 11" beyond it.
//   p.408 (S4-16) SECTION N-N (expansion joint diaphragms): faces 5" from the bent control line, 1'-10" (MIN.), bottom on the girder bottom line (the 1'-3" = lowest K302 row),
//          CL bearing 1'-4"; PLAN AT EXPANSION JOINTS: deck-to-diaphragm fillet.
//   p.405 (S4-13) TYPICAL SECTION - UNIT 6 (expansion joint diaphragms at bent 29) / UNITS 6 THRU 8 (bent 35): the expansion joint diaphragm runs to
//          the deck edge with a SLOPED outer face, from the deck edge at the soffit down to 5" outside the exterior girder bottom flange edge.
// Rules: plan values only; what the program cannot represent is reported (GAP), what the plans do not give is UNRESOLVED (page + reason).
using SBM.Core;
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Bonner Unit 6");
DiaphragmSectionPoint P(double xIn, DiaphragmZRef r, double dzIn = 0) => new(In(xIn), r, In(dzIn));
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
const DiaphragmZRef Sof = DiaphragmZRef.Soffit, Bot = DiaphragmZRef.Bottom;

// ---- interior bent diaphragms (bents 30-34): x -1'-2" .. +1'-2", soffit to the bottom of the girders (p.406 / p.407 L-L)
double interiorEnd = F(17, 3) + F(3, 7.5);                      // 20'-10 1/2" from -L- (p.406 Section A-A: 3'-7 1/2" from CL girder 4)
ConcreteDiaphragmDefinition Interior(int bent) => new()
{
    Kind = ConcreteDiaphragmKind.PierContinuity, Name = $"Bent {bent} diaphragm",
    Source = "p.406 (S4-14) typical section + Section A-A, p.407 (S4-15) Sections L-L / M-M",
    Material = "Sand-lightweight concrete (deck pour; superstructure quantities p.455)",
    Risers = DiaphragmRiserRule.BeamBottoms,
    // the part bottom IS the girder bottom line (BeamBottoms), so nothing of the part lies under a girder: no open-below panels needed
    Parts = { new DiaphragmPart { Name = "bent diaphragm", Section = { P(-14, Bot), P(14, Bot), P(14, Sof), P(-14, Sof) } } },
    Left = DiaphragmEnd.AtOffset(-interiorEnd, "p.406 Section A-A: 3'-7 1/2\" outside CL girder 1 at the bent control line"),
    Right = DiaphragmEnd.AtOffset(interiorEnd, "p.406 Section A-A: 3'-7 1/2\" outside CL girder 4 at the bent control line"),
    Unresolved =
    {
        UnresolvedFlag.Text("p.407", "blockouts around the sole plates and anchor bolts in the bent diaphragm (1\" min. clear)", "drawn in the plan views without a depth or a full outline",
            "Not modeled: the diaphragm bottom is the girder bottom, the sole plates are below it."),
    },
};
// ---- expansion joint diaphragms (unit ends): 1'-10" (MIN.) from the girder end face, bottom = girder bottom line (K301 on 2" bolsters at it), 8" fillet on the span side
// SLOPED end faces (p.405 typical sections - Unit 6 / Units 6 thru 8): from 5" outside the exterior girder bottom flange (CL girder 17'-3" + 1'-7" + 5" =
// 19'-3" from -L-) at the bottom to the deck edge (21'-3 1/2") at the soffit = DiaphragmEnd.Sloped (bottom offset, top offset).
// The open bearing zone under a girder end is as wide as the 4'-0" top flange, whose edge (17'-3" + 2'-0") lies on the 19'-3" line too: the bottom point is
// taken 1/16" (plan precision) outside it, else the end crosses the fascia girder's zone on the chorded span 35.
double endBot = F(17, 3) + In(19) + In(5) + In(1.0 / 16), endTop = F(21, 3.5);
ConcreteDiaphragmDefinition EndDia(string name, string pages, List<DiaphragmSectionPoint> section) => new()
{
    Kind = ConcreteDiaphragmKind.ExpansionJointEnd, Name = name, Source = pages,
    Material = "Sand-lightweight concrete (deck pour; superstructure quantities p.455)",
    Risers = DiaphragmRiserRule.BeamBottoms,
    OpenBelowBeams = true,          // bearing zone under the girder ends (bottom = girder bottom line)
    Parts = { new DiaphragmPart { Name = "expansion joint diaphragm", Section = section } },
    Left = DiaphragmEnd.Sloped(-endBot, -endTop, "p.405: sloped face, 5\" outside the girder 1 bottom flange edge at the bottom, deck edge at the soffit"),
    Right = DiaphragmEnd.Sloped(endBot, endTop, "p.405: sloped face, 5\" outside the girder 4 bottom flange edge at the bottom, deck edge at the soffit"),
    Unresolved =
    {
        UnresolvedFlag.Text("p.407 / p.408", "expansion joint diaphragm width 1'-10\" (MIN.)", "the plans give only a minimum width",
            "Modeled at the 1'-10\" minimum from the girder end face."),
        UnresolvedFlag.Text("p.405", "lower end of the sloped end face", "the face meets the bottom flange slope about 6\" above the girder bottom (drawn, not dimensioned; the 5\" is dimensioned there)",
            "One plane from 19'-3\" at the diaphragm bottom (girder bottom line) to the deck edge at the soffit."),
    },
};
// bent 29 (first support, span ahead): girder end 3'-2" - 11" = 2'-3" ahead of the bent control line (p.407 K-K, p.428), diaphragm to 4'-1" + fillet
var d29 = EndDia("Bent 29 expansion joint diaphragm", "p.407 (S4-15) Section K-K, p.405 (S4-13) typical section - Unit 6",
    new() { P(27, Bot), P(49, Bot), P(49, Sof, -8), P(57, Sof), P(27, Sof) });
// bent 35 (last support, span behind): girder end 1'-4" - 11" = 5" back (p.408 N-N "5\"" from the bent control line), diaphragm to -2'-3" + fillet
var d35 = EndDia("Bent 35 expansion joint diaphragm", "p.408 (S4-16) Section N-N, p.405 (S4-13) typical section - Units 6 thru 8",
    new() { P(-27, Bot), P(-5, Bot), P(-5, Sof), P(-35, Sof), P(-27, Sof, -8) });

foreach (var s in br.Supports)
{
    int n = int.Parse(s.Name.Replace("Bent ", ""));
    s.Diaphragm = n == 29 ? d29 : n == 35 ? d35 : Interior(n);
}
Print("Bonner Unit 6 diaphragms set: bents 30-34 bent diaphragms 2'-4\", bents 29 / 35 expansion joint diaphragms 1'-10\" (min.)");
