// Iowa DOT Design 817 (Woodbury, I-29 NB over Floyd Blvd) - concrete abutment (integral) and pier (continuity) diaphragms +
// preformed filler around the pier bearings + 2x8 keyways (concrete-bridge design feature C, docs/plan-data/concrete-bridge-design.md 4).
// Called by build-woodbury.csx before regeneration (re-runnable: replaces SupportDefinition.Diaphragm on the four supports).
//   sbm run samples\plans\diaphragms-woodbury.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"
// Plan data: docs/plan-data/woodbury-i29-floyd/SUMMARY.md (+ beam-information.csv seats), standard 2087-BTCD
// (C:\SBM\dist\standards\Iowa BT Beams Integral Abutment Bridges.pdf, PDF p.13 Part Section B-B / step diagram).
// Rules: plan values only; items the plans / standard do not dimension are UNRESOLVED (page + reason), never guessed.
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Structure == "I-29 NB over Floyd Blvd (Design 817)");
DiaphragmSectionPoint P(double x, DiaphragmZRef r, double dz = 0) => new(x, r, dz);
const DiaphragmZRef Bot = DiaphragmZRef.Bottom, Sof = DiaphragmZRef.Soffit, Top = DiaphragmZRef.DeckTop;
double clNb = Ft(27.00), gutter = Ft(27.00 + 36.00);   // CL NB roadway (stage-2 joint, west deck edge) and east gutter line (p.18)

// Seat (= cap step) elevations per beam line D..H (beam-information.csv, p.25; abutment = p.17 step diagram).
double[] sAbut = { 1117.50, 1117.71, 1117.92, 1118.11, 1118.31 };
double[] pier1 = { 1117.94, 1118.23, 1118.52, 1118.80, 1119.06 };
double[] pier2 = { 1118.21, 1118.62, 1119.03, 1119.43, 1119.82 };
double[] nAbut = { 1117.89, 1118.29, 1118.70, 1119.10, 1119.51 };
List<DiaphragmStep> Steps(double[] z) => z.Select((e, i) => new DiaphragmStep(i + 1, Ft(e))).ToList();

// Bottoms (user decision 11, 2026-10-11): abutments = the pile cap steps with the risers 1'-9 past the LOWER beam's CL along the CL brg (p.15 / p.16
// rear elevations; supported by the plan 'abutment footing' 18.1 / 19.5 CY) -> Explicit RiserS; piers = the p.9 step blocks (3'-8 along CL pier centred
// on the beam line, sides 78 deg 30' to CL pier) on the sloped J-K cap top -> Blocks. Both are filled in by substructure-woodbury.csx (it has the framing
// and the cap data), which build-woodbury.csx runs right after this script; run standalone, the diaphragms stay at these placeholders until then.
const string RiserWhy = "set by substructure-woodbury.csx (decision 11 risers / p.9 step blocks) - run it after this script";
const string KeyBevel = "keyway 'formed by beveled 2x8' (p.15 B-B / p.20; std 2087-BTCD p.13): the bevel is drawn but not dimensioned";

// ---------------------------------------------------------------- integral abutment diaphragm (p.15 B-B, p.16, p.20; std 2087-BTCD p.13 Part Section B-B)
// Section normal to the CL brg, x + toward the span: 3'-0 = pile-cap width, faces 1'-6 each side of CL brg (piles on CL brg, centred);
// bottom = stepped cap top (CJ), top = deck soffit (split at the soffit; the slab above stays in the deck). Back face = deck end
// (1'-6 1/4 along CL NB = 1'-6 normal). Paving notch / bracket on the back face (std B-B, same dims on p.15): 1'-1 constant depth from
// the top of deck to the bracket top, bracket 1'-3 behind the back face, 1'-3 vertical face, then 45 deg (1'-3 x 1'-3) back to the face;
// 1" chamfer at the outer top corner.
// U18 (closed 2026-10-11): section A-A (p.15 / p.16) dimensions the abutment east end 9 1/2 (CL beam H to the gutter line) + 1'-7 (gutter line to the
// wing outer face) = 2'-4 1/2: the 3'-0 diaphragm over the cap runs to the wing OUTER face (the rear elevation draws the same 1'-7 over the cap end).
// The paving-notch bracket behind the back face stops at the gutter line (outboard of it, behind the back face, is the wing extension wall).
List<DiaphragmSectionPoint> AbutMain() => new() { P(-Ft(1.5), Bot), P(Ft(1.5), Bot), P(Ft(1.5), Sof), P(-Ft(1.5), Sof) };
List<DiaphragmSectionPoint> AbutBracket() => new()
{
    P(-Ft(1.5), Top, -In(13 + 15 + 15)),                 // 45 deg from the face (1'-3 x 1'-3)
    P(-Ft(1.5), Top, -In(13)),
    P(-Ft(1.5) - In(15) + In(1), Top, -In(13)),          // 1" chamfer
    P(-Ft(1.5) - In(15), Top, -In(14)),
    P(-Ft(1.5) - In(15), Top, -In(13 + 15)),             // 1'-1 + 1'-3
};
ConcreteDiaphragmDefinition Abut(string name, double[] seats, DiaphragmEnd left, string pages) => new()
{
    Kind = ConcreteDiaphragmKind.IntegralEnd, Name = name, Source = pages + "; std 2087-BTCD p.13 B-B",
    Material = "Structural concrete (deck mix, cast with the deck)",
    Parts =
    {
        new DiaphragmPart
        {
            Name = "abutment diaphragm", Section = AbutMain(),
            Right = DiaphragmEnd.AtOffset(gutter + Len("1'-7\""), "east: wing outer face, 1'-7 outside the gutter line (p.15 / p.16 A-A: 9 1/2 + 1'-7 = 2'-4 1/2 from CL beam H) - U18"),
        },
        new DiaphragmPart { Name = "paving notch bracket", Section = AbutBracket() },
    },
    Left = left,
    Right = DiaphragmEnd.AtOffset(gutter, "east: into the wing extension wall, inner face = gutter line (p.15 A-A, p.17)"),
    BottomSteps = Steps(seats), Risers = DiaphragmRiserRule.Unresolved, RiserSource = RiserWhy,
    PlanBearingGap = In(3),                               // beam on the 3 x 3 x 2'-4 1/2 bar (p.25 BEAM BEARING 3)
    Keyways =
    {
        new DiaphragmKeywayDefinition
        {
            Name = "keyway", X = 0, Width = In(8), Depth = In(2), BetweenBeams = true, Source = "p.15 B-B; std 2087-BTCD p.13 (centred on CL brg / CL piles)",
            Unresolved = KeyBevel, Basis = "rectangular 8\" x 2\" (the printed 2x8 size), between the beams, centred on the CL brg",
        },
    },
    Unresolved =
    {
        "abutment diaphragm reinforcing (8g1, 8g3, 5d2..5d8, 5k1/5k2, p.26 bar list) not placed (schedule only)",
        "1/4\" filler of section B-B (p.20) not dimensioned in extent - not modeled",
    },
};
var sup = br.Supports;
sup[0].Diaphragm = Abut("S. Abut. diaphragm", sAbut,
    DiaphragmEnd.AtOffset(clNb - In(3.25), "west: stage CJ, abut. end 3 1/4 from CL NB measured perpendicular to the roadway (p.17; 3 5/16 along CL brg)"),
    "p.15, p.17, p.20");
sup[3].Diaphragm = Abut("N. Abut. diaphragm", nAbut,
    DiaphragmEnd.AlongLine(gutter, Len("37'-10 1/2\""), "west: abut. end / CJ 37'-10 1/2 along CL N abut brg from CL abut x gutter line (p.17)"),
    "p.16, p.17, p.20");

// ---------------------------------------------------------------- pier continuity diaphragms (p.20 part section at pier, Detail A/B; p.26 5e1/5e2)
// 2'-10 wide centred on the CL pier (user decision 10; hoops 5e1/5e2 2'-6 o-o + 2 x 2" cover), from the cap top steps to the soffit;
// ends: west at the stage-2 edge of deck (CL NB roadway), east flush with the outside face of beam H (Detail B). Two parts (back /
// ahead span beams) so each ends at its own fascia beam; beam ends 4" each side of CL pier (8" apart, Detail A).
// Bottom on the 1/4" preformed filler (p.20 Section B-B: 1/4" preformed expansion joint filler at the cap interface) -> bottom = step + 1/4".
List<DiaphragmSectionPoint> Half(double x0, double x1) => new() { P(x0, Bot, In(0.25)), P(x1, Bot, In(0.25)), P(x1, Sof), P(x0, Sof) };
ConcreteDiaphragmDefinition Pier(string name, double[] seats) => new()
{
    Kind = ConcreteDiaphragmKind.PierContinuity, Name = name, Source = "p.20 part section at pier, Details A / B; p.26",
    Material = "Structural concrete (deck mix, cast with the deck)",
    Parts =
    {
        new DiaphragmPart { Name = "back half", Section = Half(-Len("1'-5\""), 0) },
        new DiaphragmPart { Name = "ahead half", Section = Half(0, Len("1'-5\"")) },
    },
    Left = DiaphragmEnd.AtOffset(clNb, "west: stage-2 edge of deck at beam D side (p.20 part section at pier)"),
    Right = DiaphragmEnd.Fascia("east: flush with the outside face of beam H (p.20 Detail B)"),
    BottomSteps = Steps(seats), Risers = DiaphragmRiserRule.Unresolved, RiserSource = RiserWhy,
    PlanBearingGap = In(1),                               // plain neoprene 9 x 1 x 2'-4 1/2 (p.20, p.25 BEAM BEARING 1)
    OpenBelowBeams = true,                                // under the beams: pads + 1" filler strips, not diaphragm concrete (p.20 part plan)
    Fillers =
    {
        new DiaphragmFillerDefinition
        {
            Name = "filler around bearings", Material = "1\" preformed expansion joint filler", Kind = DiaphragmFillerKind.AroundBearings,
            Thickness = In(1), Source = "p.20 top of pier details: around bearings",
        },
        new DiaphragmFillerDefinition
        {
            Name = "interface filler", Material = "1/4\" preformed expansion joint filler", Kind = DiaphragmFillerKind.Interface,
            Thickness = In(0.25), X0 = -Len("1'-5\""), X1 = Len("1'-5\""), Source = "p.20 Section B-B (full diaphragm width, interrupted at the keyway)",
        },
    },
    Keyways =
    {
        new DiaphragmKeywayDefinition
        {
            Name = "keyway", X = 0, Width = In(8), Depth = In(2), TopAbove = In(0.25), BetweenBeams = true,
            Source = "p.20 top of pier details, Section B-B (centred on the CL pier)",
            Unresolved = KeyBevel + "; its 1/4\" filler lining (Section B-B) is not modeled",
            Basis = "rectangular 8\" x 2\" (the printed 2x8 size), between the beams, centred on the CL pier, joined to the diaphragm through the 1/4\" layer",
        },
    },
    Unresolved =
    {
        "1\" preformed filler on the faces of the steps and the sides / ends of the keyways (p.20 part plan): the filler on the step faces is not built (thin-layer kind for vertical faces not available), keyway bevel undimensioned - not modeled",
        "pier diaphragm reinforcing (5d13-5d18, 5e1/5e2/5e6/5e7, p.26) not placed (schedule only)",
        "junction boxes in the pier diaphragm (lighting, p.35-37) not modeled",
    },
};
sup[1].Diaphragm = Pier("Pier 1 diaphragm", pier1);
sup[2].Diaphragm = Pier("Pier 2 diaphragm", pier2);
Print("Woodbury diaphragms set: S. Abut., Pier 1, Pier 2, N. Abut.");
