# Agent review batch 3: main span joints L15-U22 (both trusses), 2026-11-02

Scope: L15 U15 L16 U16 L17 U17 L18 U18 L19 U19 L20 U20 L21 U21 L22 U22, planes N and S (32 images). Sheets p.82, p.86-90, p.98.
Method: each joint's traced outline (`connections/gusset-outlines.csv`, page px) was drawn on the 150 dpi sheet render and checked edge by edge (300 dpi
zooms where it looked off). The model gusset was taken from `out/tris_*.csv` and compared with the trace. Every part at the joint was listed with its n range.
Rivets were tested for the parts their shank passes. Callouts were read on the sheets.

## Verdict counts (74 rows)
| verdict | major | minor | info |
|---|---|---|---|
| CONFIRMED | 38 | 13 | - |
| OK | - | - | 18 |
| UNSURE | - | 4 | - |
| NOT SHOWN | - | - | 1 |

N and S are identical in the model at all 16 joints: same outlines, layers, edge angles, plates and rivet counts, with the lateral plates mirrored inward. So every row is "N+S".

## Is it "the same gusset issues everywhere"?
- **Gusset OUTLINES: mostly no.** 13 joints have their own drawing and an outline traced from it (L15 U15 L16 U16 L17 U17 L18 U18 U19 L20 U20 L22 U22). Of these, 11 follow the drawn edges within about 1-2 in. The model equals the trace (IoU 0.994-0.998). Two are off by 4-5 in.:
  - L18: the right edges are 3.5-4.7 in. too far out.
  - U20: the top-left corner is 4.3 in. above the chord top.
  
  Three joints reuse another joint's outline although p.88 / p.89 draw them separately: L19 (from L18), L21 (from L20) and U21 (from U20). U21 inherits the corner above the chord. The edge angles are on the edges the sheets draw at all 16 joints, but the U16 angle is short (22 11/16 in. vs about 30 in. drawn).
- **What is NOT at the gussets: yes, everywhere.**
  1. Chord splice material is missing at **16 / 16 joints**. Every joint in this range is a chord splice (FIN. line). The sheets call out cover splice plates 24 x t, inside web splice plates 31 1/2 x t, inlay plates 34 x t and fills, each with a drawn dimension line but no figure. `splices.csv` has no lengths for them, so `connections-ptc.csx` builds none. The exceptions are the U21 / L21 O.S. plates (under the gusset only), the U22 outer pin plates, the L15 18 x 1 extra webs and the L22 1 1/8 splice gusset.
  2. The suspender support diaphragm is missing at **8 / 8 lower joints L15-L22**. p.86 "TYPICAL CONNECTION OF ROPE SUSPENDER TO TRUSSES" shows 4-Ls 5 x 5 x 3/4 + 1-Web Pl. 29 x 3/4 between the gussets, with the pin 1'-6 above the Fin. web. p.87-89 refer to it ("see Sheet 46"). The model pin is only 7.2 in. long and floats between gussets 29-31 in. apart. Its height agrees within 1 in. The part flag still says "p.86 not read".
  3. Lug angles 2-Ls 8 x 4 x 1/2 are not modeled at **9 joints** (U15 U16 U17 L17 L18 U18 L19 L20 U20).
  4. The diagonal fills are missing at **U21 and L21**: 2-18 x 1 1/8 and 2-21 x 1 1/8 Fills. The diagonals stand 1 1/8 in. off the 2'-7 1/4 in-to-in gussets, with an open gap. Only the vertical fills are built.
  5. The U21 / L21 O.S. splice plates stop at the gusset edge. The sheet draws their field rivets about 21-24 in. beyond it, and those rivets are not placed.
  6. L22 is missing the chord diaphragms (4-Ls 5 x 3 1/2 x 1/2 + 1/2 web, F.I.E.) and the end angles 2-Ls 8 x 6 x 1/2 / 4 x 4 x 1/2, along with their rivet rows on the gusset.
- **Rivets**: no modeled rivet lies outside every plate. 106 rivet entries per truss (U15-U19, L17-L19) pass a single web ply just beyond the gusset edge, so they connect nothing. At L15 / L16, two drawn diagonal rivets per face lie 1-2.7 in. past the diagonal end, which is cut on the vertical's side, so the gusset is the only thing behind them.
- **Lateral / strut plates** match the p.98 L15 / L16 / L17 details: thickness, bent edge angles, strut landing; L18-L21 and U15-U21 follow p.98 "similar to L17". The L22 / U22 strut plates are still envelopes. p.98 points to Sheet 50 (p.90), which was not traced here.

## Crops used (review/)
batch3_L15_p86.png, batch3_L15_p86_suspender.png, batch3_U15_p82.png, batch3_U15_p82_splice.png, batch3_L16_p86.png, batch3_U16_p87.png,
batch3_U16_p87_edgeangle.png, batch3_L17_p87.png, batch3_L17_p98_lateral.png, batch3_U17_p87.png, batch3_L18_p88.png, batch3_L18_p88_rightedge.png,
batch3_U18_p88.png, batch3_L19_p88.png (model outline placed on the L19 drawing, W.P. by eye), batch3_U19_p88.png, batch3_L20_p89.png,
batch3_L20_p89_rightedge.png, batch3_U20_p89.png, batch3_U20_p89_topleft.png, batch3_L21_p89.png, batch3_U21_p89.png, batch3_L22_p90.png,
batch3_L22_p90_jacking.png, batch3_U22_p90.png. Red line = traced outline; blue circle = trace work point.
Scaled values use 4.28 px/in at 150 dpi (the gusset-outlines.csv basis) and 4.81 px/in at 160 dpi on p.98 (checked on the 18 in. strut).
