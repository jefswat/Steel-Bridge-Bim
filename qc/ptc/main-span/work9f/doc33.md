
## 33. Agent review 2026-11-02: joint fit (user: "Check gusset S-GL22-R ... fix anything you have evidence to support"; evidence `qc/ptc/main-span/work9f/`; tests `TrussJointFitTests`)

Clash rows of the main span (`sbm clash --bridge "Arch truss"`) between members of one joint, read against the joint sheets:
- L22 (p.90): '2-1" Gus. Pls. | 1/8 | 2-1" Gus. Pls.' = each half truss brings its own 1 in. gusset, 1/8 in. apart at the CL -> generic OPTIONAL
  `TrussJointDefinition.GussetSplitGap` (inner gusset cut along the joint's v axis through the point, marks `...-LA` / `-LB` / `-RA` / `-RB` = lower / higher station
  half; layers, rivets and edge angles keep the whole outline: the 1 1/8 splice gusset bridges the gap). The 1 1/8 centre plate's bottom edge was read 586 / 680 px
  (u -7.6 / +14.4 in.); re-read at 300 dpi it is centred on the CL, -7.2 / +7.1 in. (gusset-layers.csv).
- Web member ends: the bisector corner cut (2026-10-28, batch 4c) is not what the sheets draw. The verticals that run to the chord keep their full end and the diagonals
  are cut along their sides: U11 (p.79), U12 (p.81), U13 / U14 / U15 (p.82), U16 / U17 (p.87) and L6 (p.76), L8 (p.77), L15 / L16 (p.86), L17 (p.87), L18 (p.88) ->
  OPTIONAL `TrussMemberDefinition.WebsAbutAt` (points; the other web members meeting the member there are cut on its side face, `AbutCut`, before any setback /
  corner cut, and then left alone). The short verticals U7-L7 / U9-L9 stop square below the diagonals (p.76 / p.77) and U10-L10 above the crossing diagonals at L10
  (p.78) -> OPTIONAL `TrussMemberDefinition.SetBackClearAt` (square setback of any length until clear of the other web members, also of their end corners).
  The L10-U11 diagonal had run 17 in. into the post U11-L11 at U11 (only its edges crossed the post, so the corner rule never saw it).
- L11 (p.80 elevation): the lower chords L10-L11 / L11-L12 (deflection about 45 deg, beyond the 25 deg closure / mitre rule) end against the sides of the post U11-L11,
  which runs down through the joint round the bottom strut to the FIN. line -> OPTIONAL `TrussMemberDefinition.ChordsAbutAt` (chords ending at the point cut on the
  post's side faces). The post's part below the chords (and the cut of its cover plates round the strut box) is not dimensioned: not built, flagged UNRESOLVED (p.80).
- Finger dam supports 18 I 70 / 18 [ 58 (deck-ptc.csx): the plans show them in pieces between the stringer lines -> `TrussLineItemDefinition.Ranges` = the gaps between
  the flanges of the stringers present at the dam station (framing angles not built), every dam with supports (Piers 10 / 13 / 17 / 20 / 24 / 27, W22).
- Reported, not changed: FB22 x wind chord W21-W22 (0.26 in.) = the p.69 / p.94 plan conflict already flagged; N-BGFX* floor lateral plates x FB16 / FB18 / FB20 bottom
  flange (0.23-0.31 in.) = bracing plate generator (left to the lateral bracing review).
