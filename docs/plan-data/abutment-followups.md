# Abutment follow-ups (plan-data investigation, read only)

Scope: the five open items of `abutments-model.md` for the as-designed abutments of I-80 WB (Design 116: West PDF 85-88, East PDF 89-92) and
Ramp G South (Design 316: PDF 80-83). "pN" = PDF page. No code, script or model was changed; the model was only queried
(`temp\cli-main\sbm.exe run temp\abutfu\q.csx` on a copy). Scratch (render/vector scripts, clips): `temp\abutfu\`.
Method: PyMuPDF vector extraction (`page.get_drawings()`: rings, vertical/horizontal lines, dots) plus 200-500 dpi grayscale clips; page scale of the
abutment plans = 7.197 pt per ft (checked on the printed 4'-0 pile spacing, 2'-11, 26'-11, 37'-9 dimensions: errors <= 0.01 ft).
"scaled" = measured from the drawing, not a printed dimension (accuracy about +-0.03 ft). Anything that cannot be read or reconciled is marked UNRESOLVED.

---------------------------------------------------------------------------------------------------------------------

## 1. Pile head 2'-0 (plan) vs 1.78 / 1.76 / 1.60 ft (check) - the CHECK is wrong, the model is right

Plan: main-footing piles end 2'-0 above the bottom of the footing ("2'-0 TOP OF PILES", sections p86 West, p89 East, p80 South; all three read on clips).
Wing-footing piles end 1'-0 above the bottom of the wing footing ("1'-0 TOP OF PILE" in section H-H p88/p92/p83 and in the pile uplift anchor detail
p86/p90/p81). Pile tops are not given as elevations anywhere; the East footing bottom is sloped (1008.27 to 1003.44) and the 2'-0 is a dimension in the
typical section, i.e. at every pile above the LOCAL footing bottom.

Model (queried per foundation, `temp\abutfu\q.csx`):

| Abutment | foundation 0 (main footing) | wing footings 1-4 | check average (`ABUT_PILE_HEAD`) |
|---|---|---|---|
| West | 26 piles, head - bottom = 2.000 | 8 piles, 1.000 | (26 x 2 + 8 x 1) / 34 = 1.765 |
| East | 28 piles, 2.000 above the LOCAL bottom (vs the mid-footing `BottomZ`: -0.27 .. +4.27) | 8 piles, 1.000 | (28 x 2 + 8) / 36 = 1.778 |
| South | 12 piles, 2.000 | 8 piles, 1.000 | (12 x 2 + 8) / 20 = 1.600 |

The three reported values are exactly the plain mean over main-footing plus wing-footing piles (and, for East, measured from the footing MID bottom).
Nothing in the model input has to change (`PileTopAboveFootingBottom` = 2'-0, `FootingPileTopAbove` = 1'-0 match the plans).

Correct check change (`samples\plans\check-substructure-elevations.csx`, `case "ABUT_PILE_HEAD"`, line 113): use `u.Foundations[0]` only and the local bottom,
`(p.TopZ - SubstructureUnit.Interp(u.CapBottom, p.S)) / F` for every main-footing pile (CapBottom is the (S, footing bottom) line, so East is handled), plan 2.0000 on
p86 / p89 / p80; add a second key (e.g. `ABUT_WING_PILE_HEAD`) over `u.Foundations.Skip(1)` with `(p.TopZ - q.BottomZ) / F`, plan 1.0000 (p88 / p92 / p83 H-H).
Expected after the change: 0.0000 differences.

---------------------------------------------------------------------------------------------------------------------

## 2. Uplift anchors 8j3 - the circled piles (all three abutments: 4 circled piles x 2 bars = 8 = bar-list count)

Vector-circle method on the piling-layout sheets (`temp\abutfu\circ2.py`: unfilled stroked circles, 6 curve segments, diameter 18-24 pt = 2.5 pile widths; pile symbols
are the 8-9 pt H shapes). The piling layouts of the main footing contain NO ring: all circled piles are wing-footing piles, shown in the typical wing-footing
figures. Rings found: West p86 (218.2,633.9) = A-end legend; (617.7,216.9) and (617.7,288.9) = F-end legends (plan + H-H figure); East p90 three rings (141.2,316.0),
(141.9,405.9), (802.2,249.3); South p81 four rings (257.4,321.9), (257.4,417.9), (587.3,321.9), (586.7,417.8) (the South sheet draws all four wing footings).
Each ring is concentric (< 0.1 pt) with exactly one of the two pile symbols of a 7'-0 x 3'-0 wing footing, and the sheet says "SEE PILE UPLIFT ANCHOR DETAIL,
TYP. AT ALL CIRCLED PILES".

Which pile: the one nearest the roadway centre line, i.e. the FILL-SIDE (inner) pile of each wing footing, 1'-6 from the fill-side footing edge:
- printed distances from the CL approach roadway to the circled pile: West A end 21'-9 (p86, "21'-9 (TYP.)" from the dash-dot CL line at x = 383.9 pt, which is
  24'-0 - 2'-3) and F end 33'-9 (p86, dimension ending exactly on the circled pile centre at x = 626.5 pt); East J end 33'-9 and A end 39'-9 (p90, "MEASURED RADIALLY TO
  CL WESTBOUND APPROACH ROADWAY"); South 3'-9 (A side) and 23'-9 (D side) from CL Ramp G (p81; = 6'-0 - 2'-3 and 26'-0 - 2'-3).
- Section H-H (West p88) puts the 8j3 bars on the LEFT pile = the pile under the counterfort side (fill side, left of the 1'-9 wall).
- So per wing, footing No.1 and No.2 each have the inner pile circled: 2 wings x 2 footings = 4 circled piles.

| Abutment | wing (end) | footing No. | footing centre from CL brg along the gutter line | circled pile offset | pile row |
|---|---|---|---|---|---|
| West | A (left) | 1, 2 | 15'-9, 25'-9 | 21'-9 left of CL approach | inner (fill side) |
| West | F (right) | 1, 2 | 15'-9, 25'-9 | 33'-9 right | inner |
| East | J (right) | 1, 2 | 11'-0, 21'-0 | 33'-9 right | inner |
| East | A (left) | 1, 2 | 11'-0, 21'-0 | 39'-9 left | inner |
| South | A (left) | 1, 2 | 10'-11, 18'-11 | 3'-9 left of CL Ramp G | inner |
| South | D (right) | 1, 2 | 10'-11, 18'-11 | 23'-9 right | inner |

Model mapping (`BuildWing`: pile 0 = outside pile, pile 1 = inner pile; foundations are [main, A No.1, A No.2, F/J No.1, F/J No.2]): circled pile = `Piles[1]` of
each wing-footing foundation; `WingwallDefinition.UpliftPiles` entries {Footing = counterfort/footing number 1 and 2, Pile = the inner (second) pile}.
Anchor geometry (West p86 G-G, identical on p90 / p81): 2 bars per pile, 4" apart along the web, V in the plane across the web, 1 1/2" dia hole drilled in the pile web
(field drilled, torch cutting not permitted), hole centre 6" above the bottom of the footing, pile top 1'-0 above the footing bottom; bar 8j3 = 2'-6 legs, 1'-4 + 1'-4 = 2'-8
wide, 2'-1 1/2 high, D = 6 (the V bend runs through the hole, legs rise in the footing). This is the same rule `SubstructurePlanBars.UpliftAnchors` already implements for the
pier 8f6 bars (2 instead of 3 bars per pile). Pile orientation in the legends: web along the 7'-0 direction (normal to the wall), flanges parallel to the wall
(read from the H symbols; same as the main piles drawn flanges parallel to the line). Orientation is the only item here that is a drawing reading, not a dimension.
The main-footing piles are designed for a 60 / 60 / 41 kip tension (notes) but carry no circle and no anchor.

---------------------------------------------------------------------------------------------------------------------

## 3. Schedule-only bars: placement per mark

Two findings that affect more than the bars:

A. COUNTERFORT WIDTH. The counterforts are 1'-10 wide along the wall, centred on the 3'-0 wing footing (7" | 1'-10 | 7", section E-E / F-F p87, p91, p82 "(TYP.)";
   outline in J-J p88). `CounterfortDefinition.Width` (3'-0) is used for both footing and counterfort, so the model counterforts are 1.17 ft too wide.
   Over 4 counterforts (heights 8'-10 / 5'-4 West) that is 4.3 CY, which is the "model 42.6 vs plan 38.8 CY wingwalls" difference (East 35.4 / 31.6, South 34.0 / 30.2: all
   about +3.8 CY). With 1'-10 the model total would be about 38.3 / 31.2 / 29.8 CY. Needs a separate counterfort width input (data: 1'-10, all three abutments).
B. FOOTING PLAN OUTLINE (West) - see item 4: not a parallelogram; end lines parallel to the approach CL with small notches.

Notation: "printed" = dimension/string on the sheet; "scaled" = measured; section letters as on the sheets.

### 3.1 Maskwall (5e2 vertical, 5e3 horizontal, 5g2 dowels) - complete for West / East / South
Plan: one wall (two on South) on the footing end line, 10" thick, outer face FLUSH with the footing end line (item 5), width = the end line length (West 10'-4 13/16, East
6'-8 7/8, South 6'-0), standing on the footing top (wash level 4'-1 above the footing bottom), keyway beveled 2x6 at its base, aesthetic recess 1 1/2" deep with 1'-0 borders,
1'-6 at the top (View C-C / D-D p87, p91, p82).
- 5e2 verticals (printed): West "20 SPA. @ ABT. 6 = 9'-10 13/16; 21-5e2", 3" from each end of the wall (3" + 9'-10 13/16 + 3" = 10'-4 13/16 closes); East "14 SPA. @ ABT. 5 1/2 = 6'-2 7/8; 15-5e2"
  (3" ends); South "12 SPA. @ 5 1/2 = 5'-6; 13-5e2" per wall (3" ends). Two layers: Section D-D shows the dots alternating between a back line and a front line (staggered,
  one layer per face, alternate bars); 5e2 embeds 1'-6 MIN into the footing (C-C). Lengths 15'-6 / 13'-8 / 14'-7 (list) = wall height + 1'-6 embed - 2" top cover for West (14'-2) and East (12'-4);
  South: 13'-3 (right wall) + 1'-6 - 2" = 14'-7 closes, the left wall (12'-7) would need 13'-11: the list prints one length, UNRESOLVED for the left wall.
- 5e3 horizontals (printed): West "15-5e3 EQ. SPA. (E.F.)", East "13-5e3 EQ. SPA. (E.F.)", South 14 per face per wall; first bar 3" below the wall top, last bar 3" above the keyway/footing
  top (the two "3" dimensions of C-C), equal spaces between; per face one layer (D-D: two horizontal lines = two faces); length 10'-0 / 6'-4 / 5'-8 = wall width - 2 x 3" - ...
  (10'-4 13/16 - 6" = 9'-10 13/16, list 10'-0: UNRESOLVED how the 2" difference is taken - lap/hook not drawn).
- Cover: D-D prints "3 1/2" CL." from the recessed face to the bars and the wall is 10" thick; the other bar line is not dimensioned (note: min clear 2").
- 5g2 dowels (West p85 "one at each 6g1", symbol on the part plan and part section): one 5g2 per 6g1 row at the maskwall/backwall junction (West 22, East 18, South 44 = 2 per row,
  one at each maskwall). Bent shape (all sheets, bent bar details): 10" top leg, 1'-3 vertical, 1'-9 1/2 vertical rise with 2'-6 diagonal and 1'-9 horizontal, D = 3 3/4; East/South
  5g2 6'-1 (5'-3 + 10" leg). Elevation of each dowel = the 6g1 row it ties to; which end of the 6g1 (plan position of the dowel across the backwall thickness) is UNRESOLVED.

### 3.2 Counterforts (6h2 / 6h3 vertical, 5h4 horizontal, 5h1 dowels) - complete
Section J-J (p88; same on p92 / p83) and H-H:
- Counterfort: projection 3'-6 behind the wall face (3" set-in from the fill-side footing edge), width 1'-10 (finding A), height above the footing top 8'-10 / 5'-4 (West), 8'-8 / 5'-4 (East),
  8'-3 / 5'-6 (South), measured from the footing top.
- 6h2 (No.1) / 6h3 (No.2) verticals: 10 per counterfort in a perimeter ring: 4 bars along each 3'-6 face at "3 SPA. @ 1'-0 = 3'-0" with 3" end cover (4 + 4, the two corner pairs included),
  plus 2 interior bars on the fill-side 1'-10 end face, equally spaced between the corner bars (3 equal spaces of about 5 1/3", scaled). Bars start in the footing (length 11'-5 West =
  counterfort 11'-10 overall less top/bottom cover, 6h3 7'-11); South bars are L bars with a 1'-0 foot (10'-9 + 1'-0 = 11'-9; 8'-0 + 1'-0 = 9'-0) - the foot orientation is UNRESOLVED.
- 5h4 horizontals (printed in H-H): first hoop 3" below the counterfort top, last 3" above the footing top, equal spaces: No.1 "9 EQUAL SPACES; 10-5h4" (West; East 8 spaces, 9 bars; South 8 spaces, 9 bars),
  No.2 "5 EQUAL SPACES; 6-5h4". Shape (bent bars, J-J): U staple (12'-0): 1'-6 closed end at the fill-side face, legs 4'-9 along the two long sides running through the counterfort into the wall,
  6" tails turned inward in the wall; D = 2 1/2.
- 5h1 dowels: 6 per counterfort (24 West/East, South 24 incl. both wings), in the wall zone, 2 rows 4" from each wall face (scaled 4.0") x 3 across along the wall, centred on the counterfort,
  4'-0 long = 2'-0 into the footing + 2'-0 above the footing top (H-H "2'-0"). Along-wall spacing is not printed; scaled 1'-1 (not 1'-0): UNRESOLVED to the inch.

### 3.3 Wing footings (5j1, 5j2, 8j3) - complete except as noted
7'-0 across (3'-9 fill side + 1'-9 wall + 1'-6 outside) x 3'-0 along the wall x 3'-0 deep, piles 1'-6 | 4'-0 | 1'-6, pile top 1'-0 above the bottom, 3" clear (H-H p88).
- 5j1 (8 per footing = "4-5J1 (E.F.)"): U bars 6'-8 across x 2'-5 legs, D = 3 3/4; 4 as bottom U (legs up) and 4 as top inverted U (legs down) - the closed rounded outline drawn in H-H is the pair; in
  plan 2 bars near each along-wall face at scaled 5.8" and 9.7" from the face (p86 legend footing: dots at 0.49 / 0.81 ft from each face). Bar-list: 32 = 4 footings x 8.
- 5j2 (8 per footing): vertical hoops 2'-8 x 2'-7 + 6" hook (plane along the wall), D = 2 1/2; positions across the 7'-0 are "PLACE AS SHOWN ... SEE SECTION H-H" with no dimension. Scaled from H-H
  (vector, 18.0 pt/ft), from the fill-side footing edge: 0.41, 0.96, 2.04, 2.92, 3.81, 4.69, 6.05, 6.60 ft (outer pairs mirror; four inner equal at 10 5/8"). The p86 legend figure scales to 0.39, 0.74, 2.28,
  3.10, 3.92, 4.74, 6.26, 6.63 ft: the two figures disagree by up to 0.25 ft, so the hoop positions are UNRESOLVED beyond "8 hoops, symmetric pairs near the ends, about 10" apart inside".
- 8j3: see item 2 (placed on the inner pile of every wing footing).

### 3.4 Wing walls (5e1/5e4 vertical, 6f1/6f2/6f3 and 6f5-6f7 horizontal, 6f4 and 6g4 dowels)
Printed strings: West "25 SPA. @ 1'-0 = 25'-0" (A wing, 5e1: 26 per face) and "26 SPA. @ 1'-0 = 26'-0" (F wing, 5e4: 27 per face) with 6" at the free end (31'-9 - 25'-0 - 6" puts the first A bar 6'-3 from
CL brg); East "23 SPA. @ 1'-0 = 23'-0" (24 per face, 4" at the end); South "22 SPA. @ 1'-0 = 22'-0; 23-5e1 BARS (E.F.)" with 3" at both ends of the 22'-6 wall. Bars (E.F. = each face) are 4" CLR. to the wall faces
(all sheets). Horizontals per face: West "11 SPA. @ 1'-0 = 11'-0" (12 rows = 4 x 6f1 full + 8 x 6f2 varying) + 1 bottom bar 6f3 following the sloped bottom = 13 per face (26 per wing; list 6f1 8, 6f2 16, 6f3 2); East "10 SPA. @ 1'-0 = 10'-0"
(11 rows = 4 + 7) + 6f3 = 12 per face; South "8 SPA. @ 1'-0 = 8'-0" + "2 SPA. @ 6 = 1'-0" (11 rows = 5 + 6) + 6f3. The top row is 3" below the top of the wall at the FREE end (dimension "3" at the top right of
View A-A), rows are level. Row lengths = the wall width at that elevation (6f2 24'-8 ... 5'-2 West, 22'-1 ... 4'-3 East, 21'-1 ... 5'-2 South), the lowest rows end on the sloped bottom; 6f3 follows it.
6f4 (West 4 per wing, 6'-0 with 2'-0 into the footing end) and 6g4 (22 per wing, 5'-6 + 1'-0 leg, D = 4 1/2) are the wing-to-backwall dowels, drawn as 11 per face at 1'-0 on the backwall back face
(View A-A: dot column at 1'-0 pitch = 11.45 pt in the view); the vertical position of the first one and the 6f4 spacing: UNRESOLVED (dots drawn, not dimensioned).
UNRESOLVED: the length rule of the verticals (5e1 13'-1 max at the first bar vs a 12.98 ft wall there; free end 3'-9 = 4'-0 - 3"): lengths are in the schedule as printed, not derivable from cover rules.

### 3.5 Backwall extension (5d7 vertical, 4d8 hairpin, 6g5 horizontal) - West/East only
Printed: West plan note "5 SPA. @ ABT. 1'-0; 6-5d7 (B.F.) & 6-4d8; 4 SPA. @ ABT. 1'-0; 5-5d7 (F.F.)"; East "3 SPA. @ ABT. 1'-0, 4-5d7 (E.F.) & 4-4d8" (View B-B). 6g5 (View B-B, both): "11-6g5 (E.F.) (MATCH 6g1)" =
the upper 11 horizontals line up with the 6g1 rows, then "5-6g5 (E.F.) (EQ. SPA.)" below down to the footing-bottom line (West 16 per face = 32; East 14 = 28). Waterstop on the end face: PVC ribbed, centre bulb type, 9" each side of
the face centre line, 3 1/2" + 5 1/2" embedment (PVC WATERSTOP DETAIL p85). 5d7 pairs 3" in from each face (detail), 4d8 between them.
Extent: see item 5. The 5d7 string (6 at 1'-0 on the back face = 5'-0) is longer than the part of the strip beyond the footing end line (3.87 ft along the back edge): how the bar strings relate to the strip start
is UNRESOLVED (the strip continues into the backwall; "ABT." = about).

### 3.6 Beam steps (5m1, 5n1, 5n2)
Printed: 5m1 4 per step (U, 3'-8 x 2'-3 legs, D = 2 1/2; East 3'-9 x 2'-0; South 3'-11 x 2'-1, "5 per step" counted from 20 / 4 steps, UNCERTAIN), 5n1 4 per step (straight 4'-2), 5n2 hoop only at girders C and D ("AT GIRDERS C & D ONLY"; South D only).
Drawn: 5m1 as 4 U bars running normal to the line and 5n1 as 4 bars along the line forming a grid in the 4'-6 x 4'-1 step (plan p85); the section shows the 5m1 top run just under the step top and
the 5n1 bars as 4 dots in one row. UNRESOLVED: the spacing of the 4 + 4 bars (not dimensioned), the elevation of the 5n2 hoop in the step, and the cover (only the general 2" min).

### 3.7 Corbel / paving notch (5d3, 5d4, 5d5, 5g3) and backwall horizontals (6g1) / hairpins (4d6)
Printed strings (West; East 78 / 38 spaces, South 30 / 15): "82 SPA. @ 1'-0 = 82'-0; 83-5d3 & 5d4" (5d3 U 2'-5 x 1'-0, 5d4 bent 1'-0 / 1'-5 slope / 1'-3, both D = 2 1/2), "41 SPA. @ 2'-0 = 82'-0; 42-5d5" (stainless dowels 3'-6), "85 SPA. @ 1'-0 = 85'-0" 4d6
(86), 4d8 / 4d6 hairpins 1'-2 wide with 3'-10 / 1'-0 legs (D = 2). Section (p86): 5d4 follows the 45 degree underside of the corbel (1'-3 x 1'-3 taper) with 1'-0 and 1'-3 legs on the faces; 5d3 straddles the ledge
(legs down); 5g3: 2 rows (two dots in the corbel, each lapped 2'-2 = 4 bars) at scaled 3 3/4" from the vertical face and 3 3/4" below the ledge (upper row) and 3 3/4" above the taper (lower row);
6g1: lower backwall "3 | 6 EQ. SPA. | 3" = 7 rows per face between 3" below the lower construction joint and 3" above the footing/backwall base, upper pour 4 rows per face (dots, not dimensioned), 22 rows total = 11 per face, lap 2'-11.
UNRESOLVED: the first-bar offsets of the 5d3/5d4/5d5/4d6 strings along the backwall (the plan shows 1 1/2" stagger dimensions between the string start lines; the bar rows on p85 are interleaved by about 3 3/8"), the upper-pour 6g1 row elevations
and the 5d5 embedment into the notch (3'-6 bar, 2" min clear only).

### 3.8 End hoops / footing end bars (5b2, 5b3, 5b4, end 5b1)
- West 5b4 (SW end) and 5b3 (NE end): one bar per 9a1 level ("5-5b4 / 5-5b3 (PLACE WITH 9a1 BARS)"): the 9a1 face levels are "4 EQ. SPA." = 5 levels (top, 3 intermediate, bottom). Plan shape follows the footing end:
  5b4 10'-0 along the SW end line (end line 10'-4 7/8 less 2 x 2" cover; scaled 10.41 ft), 4'-6 leg along the back (top), 1'-4 along the front notch then the 3'-2 diagonal along the 44 degree front face (rise 2'-2 1/2, run 2'-3);
  5b3 7'-3 along the NE end line (scaled 7.63 ft less 4"), 1'-4 / 1'-1 legs, two 1'-6 diagonals. These confirm the end-line polygon of item 4.
- West 5b2 hoops (10 = 5 each end): "4 SPA. @ 1'-0 = 4'-0; 5-5b2" with a "9" dimension in front; vertical hoops 3'-5 high + 6" hook, widths vary 2'-0 (min) to 6'-3 (max) with the oblique end cut (growth about 1.04 ft per ft along the axis = the
  46 degree end). UNRESOLVED: what the printed "9" is measured from (alternatives tried against the polygon: from the end line, from the acute tip) and therefore each hoop width; widths cannot be reproduced from the polygon with either reading.
- East 5b2 (6 = 3 each end, "PLACE WITH 9a1 BARS", U staple 6'-3 x 1'-6 legs) and South (6, 5'-7 x 1'-6): which 3 of the 5 levels is UNRESOLVED; East/South end 5b1 hoops: "2 SPA. @ 8 = 1'-4" at each end = 3 hoops per end (East 110 = 104 + 6; South 49 = 45 + 4:
  South shows 2 at each end, UNCERTAIN count vs the string).

### 3.9 Marks with NO placement on the sheets (UNRESOLVED, stay schedule-only or need an input from the user)
- 5n2 hoop elevation in the step; 5m1/5n1 spacing in the step; 5g2 plan position across the backwall; 5d7/4d8 start of the strings relative to the extension strip; West 5b2 widths/first position; East/South 5b2 levels;
  6g4 / 6f4 first-bar positions; wing 5e1 length rule; 5j2 hoop positions; 5h1 spacing; 6h2/6h3 South L-foot orientation; 6g1 upper-pour row elevations; 5d3-5d5 first-bar offsets.

---------------------------------------------------------------------------------------------------------------------

## 4. West footing length 89.9 ft (end offsets) vs 87.9 ft (9a1 / 6g1) - RESOLVED: both are right; they are different chords

Footing plan outline of the West abutment, from the vector outline of the piling plan (p86; scale 7.197 pt/ft, CL approach roadway = the dash-dash line at x = 383.9 pt). Seven vertices (x = horizontal offset from the CL
approach, left negative; y along the approach CL measured from the crossing of the footing centre line with the CL approach; scaled, all bar-checked):

| vertex | x (ft) | y (ft) | note |
|---|---|---|---|
| SW back, end line top | -26.914 (26'-11) | -18.514 | A end line = maskwall line |
| SW back, notch end | -23.996 (24'-0) | -18.514 | on the gutter line; back face (44 degrees) starts here; horizontal notch 2'-11 |
| NE back, gutter | +36.001 (36'-0) | +39.427 | back face ends |
| NE back, end line top | +37.752 (37'-9) | +39.427 | horizontal notch 1'-9 |
| NE front, end line | +37.752 | +31.799 | end line length 7.63 (printed 7'-5 9/16 on p85) |
| SW front, front face end | -25.122 | -28.921 | horizontal notch 1.79 ft (1'-9 3/4) |
| SW front, end line bottom | -26.914 | -28.921 | end line length 10.41 (= 10'-4 13/16 maskwall width) |

Back face 83.4 ft long (gutter to gutter); front face 87.4 ft; centre line 89.90 ft between the end lines (this is the 26'-11 + 37'-9 / cos 44 = 89.90 of the model). The 9a1 bars (one length, all 20 rows) are limited by the OUTER bar
lines: the chord of this outline along a line 2.56" (2" clear + half a #9) inside the back face is 87.89 ft and along the front face 87.83 ft; the bar run is 2 x 48'-4 - 9'-1 lap + 2 x 2" end clear = 87'-11 (87.92 ft): a match to 0.03-0.09 ft.
The 6g1 backwall bars give 87'-7. So the footing is NOT 87.9 ft long on its centre line; 87.9 is the length available to the straight bars at the face rows. The model footing prism (ends from `LeftEndOffset`/`RightEndOffset`, a parallelogram) therefore
has the right centre-line length; it differs from the plan only by the notches (a 4.1 ft2 triangle too small at the SW back corner, corners at the NE back and the two front ends too large) - the "end chamfers (not modeled)".
East closes with the printed values (2 x 45'-11 - 9'-1 + 4" = 83'-1 vs 83'-0 1/2); South 8a1 37'-6 + 4" = 37'-10 = the footing. Input change if the notches are wanted: a footing plan polygon (7 vertices above); no change to the lengths.

---------------------------------------------------------------------------------------------------------------------

## 5. Maskwall position and backwall extension

### 5.1 Maskwall inside the 1'-2 zone - printed (RESOLVED)
West/East p85 / p89 plan at the A end: three vertical lines: footing end line, a line "10" from it, and the gutter line, with dimensions "2'-11" (end line to gutter), "10" (end line to the second line) and "2'-1" (second line to gutter).
Vector check (p85, scale 7.2 pt/ft): lines at x = 164.9 (end line), 170.9 (6.0 pt = 10"), 185.9 (gutter, 2'-11 from the end line); the heavy line pair that is the maskwall is 164.9 / 170.9; the wing outer face is at 173.3 (1'-9 outboard of the gutter),
i.e. 4" clear to the wall. South p80: "10" (wall) + "4" (clear) + "1'-9" (wing) at both ends. Result: the maskwall is the OUTERMOST 10" of the zone: outer face flush with the footing end line, inner face 10" in, 4" clear to the wing.
Model: `MaskwallDefinition.Setback` must be 0 (currently 2" in `abutments-i80.csx` line 50 and `abutments-ramp-g.csx`); the warning at `SubstructureAbutment.cs` line 214 then no longer applies. The wall length = the footing end line (finding in item 4: 10'-4 13/16).

### 5.2 Backwall extension extent and width - printed geometry found; the "3'-5" East value was wrong
West (p85, F end; vector lines at 7.197 pt/ft): the extension is the backwall strip (1'-6 thick, same axis, collinear with the backwall back face) continuing beyond the footing end line (x = 37'-9) to a cap PERPENDICULAR to the strip.
Printed horizontal dimensions: "4'-6 3/8" from the gutter line (36'-0) to the back corner of the cap (x = 40.531), "2'-9 3/8" from the wing outer face / end line, "3'-9 7/8" from the end line to the front corner of the cap (x = 41.573); end line 7'-5 9/16 vertical.
Along the strip axis: back edge 2.781 / cos 44 = 3.866 ft, front edge 3.823 / cos 44 = 5.315 ft, mean 4.59 ft, cap width 1.50 ft (cap corners differ by 1.5 sin 44 = 1.04 ft = 41.573 - 40.531, closes). Concrete check: 4.59 x 1.5 x 14'-10 3/8 = 102 ft3 = 3.79 CY = plan 3.8 CY (closes), top ELEV 1007.65, bottom at the footing-bottom line 992.82.
The model uses `Width` = 4'-6 3/8 (a horizontal dimension to the back corner, not the axis length); the correct input is the mean axis length 4'-7 (4.59 ft) with a perpendicular cap, or the exact quadrilateral above.
East (p89, J end): "3'-3 1/2" horizontal from the cap to the footing end line (vector: cap at x = 133.9, end line 157.6, 23.7 pt = 3.29 ft) - NOT 3'-5. Concrete check: 3.29 x 1.5 x 12.7 ft (top 1021.09, bottom 1008.46 - 1008.27 on the slope) = 62.8 ft3 = 2.33 CY = plan 2.3 CY.
The earlier 3'-5 came from using the bar length 12'-2 instead of the extension height. With the skew 5 deg 26'43" the strip axis is almost normal to the end line: axis length 3.29 / cos 5.45 = 3.30 ft. East input: Width = 3'-3 1/2.
South has no extension. Remaining UNRESOLVED: the relation of the 5d7 strings to the strip start (3.5).

---------------------------------------------------------------------------------------------------------------------

## Suggested input / check changes (not done here)
1. check script: ABUT_PILE_HEAD per main-footing pile vs local bottom, new wing key = 1.0 (item 1).
2. `WingwallDefinition.UpliftPiles`: inner pile of footing 1 and 2 on every wing, 2 bars per pile (item 2).
3. `CounterfortDefinition`: separate counterfort width 1'-10 (finding A) - removes the +3.8 CY wingwall quantity difference.
4. Maskwall `Setback` = 0; West extension width 4.59 ft with a perpendicular cap, East 3'-3 1/2 (item 5).
5. Optional: West footing plan polygon (item 4).
