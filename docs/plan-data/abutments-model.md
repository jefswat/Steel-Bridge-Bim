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
| footing + steps | 92.4 / 92.7 | 84.8 / 85.4 | 35.2 / 35.1 |
| backwall incl. extension | 66.1 / 62.4 | 49.3 / 47.0 | 21.5 / 19.4 |
| wingwalls (+ counterforts in model) | 38.8 / 42.6 | 31.6 / 35.4 | 30.2 / 34.0 |
| maskwall(s) | 4.1 / 4.1 | 2.4 / 2.6 | 4.4 / 4.8 |
| wing footings | 9.3 / 9.3 | 9.3 / 9.3 | 9.3 / 9.3 |
| total | 210.7 / 211.1 | 177.4 / 179.7 | 100.6 / 102.6 |
| rebar (epoxy + stainless) | 24,877 / 24,972 | 21,451 / 21,499 | 11,399 / 11,476 |
The plan wing quantity probably includes the counterforts in a different way; the plan splits are not itemized.

## Not placed / unresolved (all reported as generator warnings, nothing substituted)
Only the footing longitudinals, footing hoops at the pile pitch and the backwall verticals are placed in 3D; every other bar-list row is in the schedule at its printed
count and length ("schedule only"). Count differences between placement strings and the list (6d1/5d2, end hoops) are warnings. Pile uplift anchors 8j3 (circled piles),
maskwall position inside the 1'-2 zone, backwall extension extent/width, footing end chamfers (not modeled) and the West footing length (89.9 ft from the printed end offsets
vs 87.9 ft implied by the 9a1 / 6g1 bar lengths) are open.
