# As-designed abutments in the model (feature/abutments)

Input: `SubstructureDefinition.Abutment` (`AbutmentDefinition`, optional; null = the standard parametric abutment). Data: `abutments.md`.
Scripts (run in this order after the build + joints scripts): `samples/plans/abutments-i80.csx` (West = Unit 1 first support, East = Unit 3 last
support), `samples/plans/abutments-ramp-g.csx` (South = Ramp G Unit 1 first support).

## What is built (SubstructureAbutment.cs)
Footing prism with ends parallel to the alignment (alignment offsets), level or sloped bottom, 1" wash in front of the backwall, backwall keyed 3'-10 up;
backwall in three pours (lower / upper with corbel + 45 deg taper / device pour above the ledge) following the crown line (4 parallel lines + c.j.);
beam steps per girder (plan top elevations; warning if the bearing seat differs > 1/8"); maskwalls; backwall extension; piles at the plan stations
(two rows normal to the line, top 2'-0 above the footing bottom, contract length from the pile top); wingwalls outboard of the gutter line (path
along the alignment offset, near end cut by the backwall back face, level reach then slope), counterforts, 7'-0 x 3'-0 x 3'-0 wing footings, 2 piles each.
Views: `AbutmentViews` (rear elevation, section through the abutment at the tallest step, piling and wing layout) = 3 panels on the abutment sheet.
IFC: footing/wing footing IfcFooting PILE_CAP, backwall and wingwall IfcWall RETAININGWALL, counterfort IfcWall USERDEFINED ('COUNTERFORT'),
maskwall IfcWall SOLIDWALL ('MASKWALL'), beam steps IfcColumn USERDEFINED PEDESTAL, piles IfcPile DRIVEN. (Not validated with IfcOpenShell here: not installed.)
Database: groups backwall / maskwall / "Wingwall X" (wall, counterforts, wing footings, wing piles) / footing; sheet links via the existing abutment rule.

## Backwall front 22" vs 17" (reported conflict)
The as-designed abutment takes the plan dimension (`BackwallFrontToBearing` 22" I-80, 17" Ramp G). Deck end to CL bearing is 1'-4 / 1'-2 1/2, so the gap
deck end to backwall face is 6" / 2 1/2" = the plans' "6" @ 50 F" / "2 1/2"": the joint opening and the deck end line already agree; the old 17" came from
`BackwallGap` = 1" in the parametric abutment. The scripts set `BackwallGap` to the plan gap; a note and a warning (when they disagree) are generated.

## Plan vs model quantities (CY; rebar lb)
| | West plan / model | East plan / model | Ramp G plan / model |
|---|---|---|---|
| footing + steps | 92.4 / 91.9 | 84.8 / 85.4 | 35.2 / 35.1 |
| backwall incl. extension | 66.1 / 66.0 | 49.3 / 49.3 | 21.5 / 21.6 |
| wingwalls (+ counterforts in model) | 38.8 / 38.3 | 31.6 / 31.2 | 30.2 / 29.8 |
| maskwall(s) | 4.1 / 4.6 | 2.4 / 2.6 | 4.4 / 4.8 |
| wing footings | 9.3 / 9.3 | 9.3 / 9.3 | 9.3 / 9.3 |
| total | 210.7 / 210.1 | 177.4 / 177.8 | 100.6 / 100.6 |
| rebar (epoxy + stainless) | 24,877 / 24,966 | 21,451 / 21,610 | 11,399 / 11,476 |
Model values from `sbm run` of abutments-i80.csx / abutments-ramp-g.csx (2026-10-11); footing + steps = footing + pedestal (beam step) solids.

## Backwall extent and corbel ends (Ramp G 19.4 -> 21.6 CY)
Plan quantity (PDF p83, CONCRETE PLACEMENT QUANTITIES, South Abut.): BACKWALL BELOW CONSTR. JOINT 11.5 + ABOVE CONSTR. JOINT 10.0 = 21.5 CY.
The model gave 19.4 CY because the script ran the backwall gutter to gutter (`BackwallLeft/RightOffset` = -6'-0 / +26'-0, the 32'-0 roadway).
REAR ELEVATION (PDF p80, raster-checked): the top line and the construction joint continue as dashed lines BEHIND the 1'-9 wings and the 4" to the
10" maskwalls ("LEVEL" at the A end, "SLOPED" at the D end); only the ledge / corbel-face / taper lines stop at the wing inner faces (gutter lines).
So the backwall runs maskwall inner face to maskwall inner face (-8'-1 / +28'-1 = 36'-2) with the corbel / paving notch only over the 32'-0 between
the wings. Generator: new `AbutmentDefinition.CorbelLeftOffset / CorbelRightOffset` (NaN = backwall ends) - the upper pour is a plain wall outside them;
the rear elevation view draws the corbel lines only between them plus the wing inner-face lines. `AbutmentRebarDefinition.BackwallStringStartOffset`
keeps the printed 6d1 / 5d2 strings measured from the gutter (unchanged placement). Corroboration: the bar list prints 67 6d1 / 35 5d2 vs the
63 / 31 of the printed strings over the 32'-0 - 4 more bars each, consistent with bars in the wall behind the wings (still a NOT-adjusted count warning).
Model now: lower 11.10 + upper (corbel) 8.30 + device pour 2.18 = 21.6 CY (plan 21.5).
I-80 (p85 / p89) shows the same arrangement; abutments-i80.csx sets the corbel offsets to the gutter lines (backwall maskwall to extension unchanged):
West 66.0 / East 49.3 CY vs plan 66.1 / 49.3.

CONFLICT (minor, not adjusted): the plan pour split. Model below / above c.j. = 11.1 / 10.5 CY with the c.j. 3" below the bottom of the taper
(SECTION p80; 5'-6 1/8 ** c.j. to the bottom of the backwall at the back face at the CL, matches the model). With the c.j. AT the taper bottom (no 3")
the model gives 11.6 / 10.0 CY - i.e. the plan split appears to have been computed without the 3". Totals agree; the script prints this as CONFLICT.

## Wing bottom on the wing footings (2026-10-11)
Plan wing elevations (I-80 PDF 87 A-A / K-K, PDF 91, Ramp G PDF 82 A-A / B-B; raster 220-300 dpi): the wall bottom is the sloped line from the backwall (after the 1'-6 level
reach on the West) to the free-end bottom, passing through the near top corners of the wing footings; OVER each wing footing the wall comes down to the footing top, with a
vertical step at the footing's far end. The model ran the sloped line over the footings, leaving a gap (0 to ~1'-1) between the wall and the footing beside each counterfort.
`WingwallDefinition.BottomOnFootings` (set by both scripts) fills it. Wingwalls + counterforts: West 32.0 + 6.7 = 38.7 (plan 38.8), East 24.9 + 6.7 = 31.6 (31.6),
South 23.7 + 6.5 = 30.2 (30.2) CY.

## Plan view footing outline
`AbutmentGeometry.FootingOutline` is the plan outline of the main footing built by the generator from the same end planes, notches and corner pieces
as the footing solid; `AbutmentViews` (piling and wing layout) draws it instead of a 4-corner parallelogram, so the West footing notches show.
Test: `FootingNotch_RemovesTheAcuteCorner_AndMaskwallIsFlushWithTheEndLine` (outline area = footing bottom face area; the view polyline = the outline).

## Not placed / unresolved (all reported as generator warnings, nothing substituted)
Only the footing longitudinals, footing hoops at the pile pitch and the backwall verticals are placed in 3D; every other bar-list row is in the schedule at its printed
count and length ("schedule only"). Count differences between placement strings and the list (6d1/5d2, end hoops) are warnings. Pile uplift anchors 8j3 (circled piles),
maskwall position inside the 1'-2 zone, backwall extension extent/width, footing end chamfers (not modeled) and the West footing length (89.9 ft from the printed end offsets
vs 87.9 ft implied by the 9a1 / 6g1 bar lengths) are open.
