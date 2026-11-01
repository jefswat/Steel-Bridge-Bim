# Bonner Unit 5 - END SEGMENT (18-ES / 29-ES) geometry: agent review plan reading

Plan set: `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf` (PDF page numbers).
Main sheet: p.271 (S3-23 END SEGMENT DIMENSIONS). Also read: p.272 (S3-24 end segment PT), p.268 (S3-20 segment dimensions),
p.314 / 315 (vermin screen / access), p.317-320 (bearings), p.161 (S2-19, bent 18) and p.407 (S4-15, bent 29) expansion joint sections, p.333.

Method: PyMuPDF text layer + vector linework (`work/lines.py` dumps strokes of a page region, `work/txt.py` the words; crops in `img/`).
All p.271 views (ELEVATION, SECTION A-A, ELEVATION B-B, SECTION C-C, half plans) are drawn at **3/8" = 1'-0" = 2.25 pt per inch**
(checked: 9'-0" depth = 243.0 pt, 8'-8" = 234.0 pt, 42'-7" = 1150 pt). Values below are in inches.

Tags: **VECTOR** = dimension text on the sheet and/or reproduced by the vector coordinates (< 0.1"); **SCALED** = only drawn, measured
from the vectors; **NOT GIVEN** = not on the sheets; **CONFLICT** = two views / sheets disagree.

## 0. Frame

* x = transverse from CL girder, + right looking up-station; y = DOWN from the deck top, normal to it; s = along CL girder from the
  ANCHOR face (the top-slab end toward the bent control line / expansion joint) toward the span (closure joint) face. s = 0 .. 104.
* p.271 note 1: longitudinal dims along CL girder; the diaphragm is perpendicular to the bottom slab -> every face across the segment
  (anchor face, lower box face, recess back face, void back face, span face) is a plane s = const in this frame. The deck top and soffit
  are drawn parallel (constant depth 9'-0") and LEVEL across (no cross slope drawn; every view "SYMMETRICAL ABOUT CL GIRDER").
* View directions on p.271: ELEVATION left half = anchor face, right half = span face (x drawn symmetric). SECTION A-A = cut at CL girder,
  anchor face on the LEFT (page x 1655.3 = s 0). ELEVATION B-B = side view from the wing tip, anchor face on the RIGHT (page x 2340.8 = s 0).
  SECTION C-C = cut through the wing near x = 204 (page x of the C arrows ~1294), anchor face on the right. Half plans: span face at the top
  (page y 777.34 = s 104), anchor face at the bottom (page y 1011.34 = s 0); CL girder page x 831.2 in elevation and plans.

## 1. Outer envelope

| item | value | tag / page |
|---|---|---|
| length (top slab, s 0..104) | 8'-8" = 104 | VECTOR p.271 A-A / B-B / C-C / plan |
| lower box (webs + bottom slab) | starts 11" behind the anchor face: s 11..104, bottom length 7'-9" = 93 | VECTOR p.271 A-A "11"", B-B "7'-9"" |
| depth | 9'-0" = 108 (constant) | VECTOR p.271; = p.268 section 12 H = 9'-0" |
| deck width | 42'-7" = 511 (21'-3 1/2" = 255.5 each side) | VECTOR p.271 = p.268 |
| web outer face | x = 143.5 at y = 26 (9'-4" from the tip) to x = 123.0 at the soffit (10'-3" = W/2), slope 1:4 | VECTOR p.271 (9'-4", 10'-3", 4/1 symbol) = p.268 section 12 |
| web thickness | 1'-4" normal to the web | VECTOR p.271 / p.268 |
| bottom slab | 10" (y 98..108) | VECTOR p.271 "10"" = p.268 T |
| top slab (typical, span void) | 9" at CL (y 0..9) | VECTOR p.271 "9"" (A-A, elevation) |
| wing at the SPAN FACE (s 66..104) | typical: 10" at the tip, level 10" for 4'-1" (to x = 206.5), then sloping to y = 26 at the web outer face x = 143.5 (5'-3") | VECTOR p.271 plan "4'-1" / 5'-3"", elevation "10"" + "1'-4"" (B-B left: 10" + 1'-4" = 2'-2"), = p.268 |
| wing THICKENING (anchor end) | soffit level at y = 26 (2'-2") from the tip to the web for s 0..51; at the tip the soffit tapers 10" -> 26" over s 66 -> 51 (1'-3"); flat 10" for s 66..104 (3'-2") | VECTOR p.271 C-C "3'-2" / 1'-3" / 6'-6" / 2'-2"", B-B "3'-2" / 1'-3" / 4'-3"", plan dashed lines s = 66.0 / 51.0 |
| taper surface | plane through the tip line: y_t(s) = 10 + 16 (66 - s) / 15; wing soffit = max(typical sloped soffit, y_t), limited to 26. Its intersection with the typical soffit runs from (x 206.5, s 66) to (x 143.5, s 51) | VECTOR (plan dashed line page (366.6, 862.84) -> (508.4, 896.64) = (x -206.4, s 66.0) -> (x -143.5, s 51.0) reproduces exactly) |
| top slab nose s 0..11 | full width, y 0..26 (no web / bottom slab under it) | VECTOR p.271 A-A, B-B, elevation (wing tip 26" at the anchor face) |
| span face | = p.268 interior section 12 (H 9'-0", W/2 10'-3", T 10") | VECTOR |
| wing tip edge | 2-1" (triangle) groove (TYP.), p.268 Detail A (and 3/4" chamfer) | VECTOR text p.271 / p.268 |

Minor drawing difference: in B-B and C-C the drawn taper line ends at s = 52.67 (page 2222.3 / 1579.6), 1.67" short of the
dimensioned 1'-3" (extension line at page 1583.4 = s 51.0) and of the plan dashed line (s 51.0). Use s 51 (dimensioned).

## 2. Interior: recess, diaphragm, access passage, span void

Read from A-A (cut at CL), B-B hidden lines, both half plans (hidden lines) and p.272 Section A-A (cut through the anchors), which
draws the same outline with the anchors. The end segment is SOLID from s 26 to s 86 except the access passage; the anchor end has a
15" deep recess between the webs; the span end has the typical void for 1'-6".

| s range | what is there | tag |
|---|---|---|
| 0 .. 11 | top slab nose only (26" thick, expansion joint blockout on top s 0..26) | VECTOR (A-A "11"", "2'-2"", "1'-4"", "10"") |
| 11 | lower box face (webs + bottom slab end) | VECTOR |
| 11 .. 26 | ANCHOR RECESS: open between the web inner faces from the top slab underside (y 26) to the bottom slab top (y 98); back face at s = 26 (1'-3" deep). Two solid wedges in it carry the tendon anchors (item 4). Bottom slab notch at CL (item 4). | VECTOR (A-A "1'-3"", "9"", "1'-7"", "6"", "2'-4""; elevation recess outline; p.272 A-A) |
| 26 .. 86 | SOLID diaphragm (full height between the webs) pierced by the access passage | VECTOR (A-A "5'-0"" s 26 -> 86; bottom-slab half plan: no web line between s 26 and 86 at x 61..110 = cut through solid; p.272 A-A solid there) |
| 26 .. 66 | passage = DIAPHRAGM OPENING shape: 6'-0" high above the bottom slab top (y 26 .. 98), +-5'-1" wide (x +-61), top corners chamfered 3'-4" (horizontal) x 2'-7 1/2" (vertical) -> flat top +-1'-9" (x +-21) at y 26 | VECTOR (elevation "6'-0"", "5'-1"", "3'-4" (TYP.)", "2'-7 1/2" (TYP.)", "1'-9""; p.314 SECTION / DETAIL A "3'-6"" top = 2 x 1'-9") |
| 66 .. 86 | passage = rectangle x +-61 (5'-1"), from the 9" top slab underside (y 9) to the bottom slab top (y 98); face at s = 66 closes the chamfered part above | VECTOR (A-A: y 9 line s 66..104, vertical at s 66 from y 9 to 57.5, "3'-4"" s 26 -> 66, "1'-8"" s 66 -> 86; span-face elevation: x = 61 line from y 9 to 98; plan dashed x = +-61 s 26..86) |
| 86 .. 104 | typical box void (1'-6"), haunches + fillets of p.268 section 12; back face at s = 86 | VECTOR (A-A / plan "1'-6"", B-B dashed s 86; bottom haunch 6" x 2'-5 1/2") |

Void at the span face (s 86..104), VECTOR p.271 / p.268: top slab underside y 9 from CL to x 61 (5'-1"); top haunch straight to the
theoretical corner (x 127 = 5'-1" + 5'-6", y 26 = 2'-2") with an R = 1'-0" fillet (drawn tangent points (115.5, 23.0) on the haunch and
(124.1, 37.5) on the web - SCALED); web inner face 1:4 from (127, 26) to (110.5, 92); bottom haunch 6" over 2'-5 1/2" (x 110.5 -> 81) to
the bottom slab top y 98. ("9'-7" + 2 1/2"" are dimensioned on the fillet; the drawn haunch tangent point is at 115.5 = 9'-7 1/2".)

Diaphragm thickness: solid from the recess back face (s 26) to the void back face (s 86) = 5'-0" over the full section outside the
passage (|x| > 61); around the passage the solid is s 26..66 above the chamfers. A-A also dimensions "2'-2"" + "6'-6"" for the top
(top slab blockout s 0..26 and the rest).

Note: SEGMENTAL-DESIGN.md calls the interior "not fully dimensioned"; every s station above is dimensioned in A-A (2'-2" / 3'-4" /
1'-8" / 1'-6" top chain, 11" / 1'-3" / 5'-0" bottom chain). Only the R 1'-0" tangent points and the pocket items in 4/6/7 are scaled.

## 3. Diaphragm (access) opening

| item | value | tag |
|---|---|---|
| outline (same at s 11..66) | bottom = bottom slab top y 98, x +-61; vertical sides to y 57.5; chamfers to (+-21, 26); flat top y 26 | VECTOR (6'-0", 5'-1", 3'-4" (TYP.), 2'-7 1/2" (TYP.), 1'-9") |
| through | from the recess (s 26, the opening is inside the recess for s 11..26) to s 66; then the rectangle (item 2) to s 86 into the span void | VECTOR |
| bottom slab in front of it | notched at the anchor end (item 4) | VECTOR |
| screen / door | p.271 note 8: accepts the screen with door, p.314 (Detail A: bent 18 shown, bent 29 similar; door 3'-6" frame; conduit plate in lieu of screen beside the door, different at bent 18 (rectangular 1'-7" plate, 6 x 3 1/2" holes) and bent 29 (triangular plate)) | VECTOR text p.314 (screen hardware not geometry of the segment) |

Drawing difference (minor): the top-slab half plan draws the chamfer-top hidden line at x = +-23.5 (page 778.3), the elevation
dimensions +-21 (1'-9", page 783.9 / 878.4). Use +-21.

## 4. Anchor-face recesses / pockets (the closed outlines on the anchor-face half elevation)

The anchor-face half elevation shows (all solid lines, page coords -> x, y):

| outline | what it is | value | tag |
|---|---|---|---|
| A: web inner faces + chamfer + y 26 + y 98 (page 549.9/563.6/586.0, 270.74, 432.74) | outline of the ANCHOR RECESS (s 11 .. 26) between the webs | top y 26 from CL to x 119 (3'-4" + 6'-7"), chamfer 6 1/8" x 7 7/8" to (125.125, 33.875), web inner face 1:4 down to (109.1, 98), bottom slab top y 98 | VECTOR (dims 6 1/8", 3'-4", 6'-7", 7 7/8", 1'-4") |
| B: rectangle x 79..122.3, y 26..45 (page 653.5 / 313.54) | TOP anchor wedge (tendons C1 / C2, 12-0.6" anchors, p.272): solid wedge hanging from the top slab underside, inclined bearing face from (s 17, y 26) to (s 26, y 45) = 25.3 deg from vertical (p.272: tendons at 26 / 16 deg) | x 79 (6'-7") to the web inner face, y 26 .. 45 (1'-7"), s 17 .. 26 (9") | VECTOR (A-A "9"", "1'-7"", elevation 6'-7") |
| C: line y 80 from the web (x 113.9) to x 61 (page 392.24) | BOTTOM anchor wedge (tendons B1 / B2, 22-0.6" anchors): solid wedge on the bottom slab, bearing face from (s 26, y 80) to (s 20, y 98) = 18.4 deg (p.272: 18 deg) | x 61 .. web, y 80 .. 98, s 20 .. 26 | VECTOR (A-A "6"", "2'-4"" = 28 above the soffit) |
| D: back face s 26 between y 45 and 80 (x 61 .. web) | recess back face carrying the FUTURE PT anchors (p.272) | plane s = 26 | VECTOR |
| E: lines x +-36 / +-21 from y 98 to 108 (page 750.2 / 783.9) | BOTTOM SLAB NOTCH at the anchor end in front of the opening: bottom slab removed for |x| <= 21 at s 26 widening at 45 deg to |x| <= 36 at s 11 (full 10" depth) | plan (x, s): (-36, 11) (-21, 26) (21, 26) (36, 11) | VECTOR (elevation + plan "1'-9"", "1'-3""; A-A lines s 11..26 at y 98 / 108 + back face s 26) - purpose not labelled |
| F: opening outline (page 694 / 783.9) | the diaphragm opening, item 3 | | VECTOR |
| small items | threaded inserts, conduit holes, bearing pockets | item 6 / 7 | |

Top wedge minor difference: the top-slab plan draws the wedge's inner side at x = 81 (page 649) and its front edge at s = 17.6 (page
971.84); the elevation dimensions 6'-7" (x 79) and A-A 9" (s 17). Use 79 / 17.

Between x 61 and 79 above y 45 there is no wedge: the recess goes back to s 26 there (plan hidden side line x 81 from s 17.6 to 26 = the
wedge side wall; the fall-protection inserts at x 50 sit on that s 26 face).

## 5. Expansion joint blockout (top slab, anchor end)

| item | value | tag |
|---|---|---|
| along s | 2'-2" = 26 from the anchor face (s 0 .. 26) | VECTOR p.271 A-A / C-C "2'-2"", plan |
| depth | 1'-4" = 16 below the deck top (10" of top slab left under it, y 16 .. 26) | VECTOR p.271 C-C / A-A / elevation |
| across | full width less 9" at each wing tip: x -246.5 .. +246.5 | VECTOR p.271 elevation "9"" (page 256.4 -> 276.6 = 8.98") + plan (page 276.6) |
| end faces | vertical, square (s 26 face and the 9" ends parallel to s) | VECTOR |
| device / 2nd stage | joint: modular (p.271 note 4: S2 drawings bent 18, S4 drawings bent 29; p.333); CL joint 1'-0 1/2" +- (bent 18, p.161) / 1'-2 1/2" +- (bent 29, p.407) from the bent control line on the far side; p.333 DIM "A" table not in the set | VECTOR text; DIM "A" NOT GIVEN |

## 6. Bearings: plinth and pintle pockets

| item | value | tag |
|---|---|---|
| CL girder bearing line | x = +-99 (8'-3" TYP.) | VECTOR p.271 elevation, p.320 |
| CL bearing along s | 5'-6" from the bent control line, PLUMB (drawn parallel to the slanted BCL); at the soffit s = 52.3 (41.3 from the lower box face) | 5'-6" VECTOR (p.271 A-A + B-B, p.161, p.407); s 52.3 SCALED (A-A page 1773.07 at the soffit) |
| plinth | cast-in-place grouted plinth under the soffit (p.271 note 7 -> p.317-319), NOT part of the precast segment. Drawn: soffit length s 31.0 .. 74.8 (43.8"), bottom s 33.4 .. 71.3, 45 deg chamfered ends; across x 84.4 .. 114.2 at the soffit (29.8"), 86.9 .. 110.8 at the bottom; thickness 2.3" (s 33) to 3.7" (s 71) = level bottom under the inclined soffit; embed plate (~1") under it | SCALED p.271 (A-A, B-B, elevation); p.317 "where plinth thickness exceeds 3" reinforce ..." |
| pintle blockouts (pockets in the soffit) | 5 1/2" dia x 11" deep, shear stud in each (p.317 schematic); 2 studs per bearing at bents 18 / 29 (p.319); drawn at x = 99 -+ 6.05 (x 93.0 / 105.1), from the soffit up to y 97.0 (11.0") | 5 1/2" / 11" VECTOR text p.317; count VECTOR text p.319; x positions SCALED p.271 elevation; s position NOT GIVEN (not drawn in A-A / B-B / plans; presumably on the CL bearing) |
| bearing stack | provider-designed disc bearing (p.317 note 15); masonry / embed plate sizes in the p.318 table (not decoded here) | NOT read |

## 7. Small items

| item | value | tag |
|---|---|---|
| vent hole | 2 1/2" dia through the bottom slab, x = +-73.5 (6'-1 1/2"), s = 89.0 (1'-3" from the span face; 1'-6" = void back face), LOW SIDE of the segment only | VECTOR p.271 bottom-slab plan (circle page (996.5, 811.14), r 1.24") |
| electrical conduit holes | 3 1/2" dia, 2 holes, RIGHT OF CL ONLY: y = 61.0 (3'-11" above the soffit) and y = 51.0 (10" higher); x = 115.0 and 117.5 (drawn on a line parallel to the web inner face, centres ~3 1/4" inside it) | y VECTOR (dims 3'-11", 10"); x SCALED; drawn in both the anchor and the span half -> they run along s through the solid diaphragm (s 26 .. 86) - run NOT GIVEN explicitly (note 11 -> "Box girder lighting system" sheet) |
| fall protection threaded inserts | stainless inserts + eye bolts (note 10), on the recess back face s = 26, axis along s, at x = +-50 (4'-2"), y = 32 (6" below the top slab underside); drawn insert length ~13" (s 26 .. 39.3) | x, y VECTOR (4'-2", 6"); length SCALED |
| shear keys | on the SPAN face (note 2 -> "Bulkhead and shear key details") | already in keys.csv |
| vertical PT bars | p.272, 2'-0" from the span (CJ) face, see `../vertical-pt/vertical-pt.md` section 2 | VECTOR |

## 8. Bent control line, 18-ES vs 29-ES

| item | value | tag |
|---|---|---|
| BCL in A-A / B-B | drawn as a plumb line in the segment frame: tilted 2.14 deg (dx/dy = 0.0374) relative to the faces = the segment rising AWAY from the anchor face at ~3.7 % (A-A: top of the line leans toward the span). The faces stay square to the bottom slab (note 1) | VECTOR (A-A line page (1636.7,127.3)-(1623.4,483.4), straight) |
| BCL position | crosses the deck top 9.7" beyond the anchor face (s -9.7) and the soffit at s -13.7; = p.268 span 19 / 29 "9'-6"" (BCL to closure joint, horizontal along CL girder) - 8'-8" = 10" (repo segments.csv DERIVED) | SCALED p.271 (no dimension anchor face -> BCL) |
| CONFLICT p.161 / p.407 | the S2 / S4 joint sections draw the end segment schematically at another place: p.161 (bent 18) top-slab end 2.4" PAST the BCL toward the joint and the box CL bearing ~68" from it; p.407 (bent 29) top end ~10" past the BCL. p.271 / p.268 (end segment sheets) put the anchor face ~10" SHORT of the BCL and the bearing at s 52. Use p.271 / p.268; the S2 / S4 sketches are not to scale for the segment | SCALED (p.161 page BCL 1817.9, nose 1807.2, lower box 1828.0 region; p.407 BCL 705.2, end 752.9) |
| 29-ES | the same segment in the opposite hand: p.268 span 29 elevation puts 29-ES at the up-station end with its anchor face at bent 29 (18-ES: down-station end, anchor face at bent 18); p.314 "bent 18 shown, bent 29 similar". In the s frame (from the anchor face) the geometry is identical; x (right looking up-station) is NOT mirrored by reversing s | VECTOR text |
| differences 18 vs 29 | joint (S2 vs S4 drawings, CL joint 1'-0 1/2" vs 1'-2 1/2" from the BCL), conduit plate in the screen (p.314: rectangular vs triangular), vent hole on the LOW side (depends on each end's cross slope), grade at each end (the drawn 3.7 % is one sketch; take the model profile) | VECTOR text / NOT GIVEN which side is low |
| "RIGHT OF CL ONLY" (conduits) | the sheet does not say right looking up-station or looking at the face; both halves of the elevation draw the holes on the outer side | NOT GIVEN (direction) - flag |

## 9. Weight cross-check (CHECK, not resolved)

`work/volume.py` integrates the outline read above (fillet R 1'-0 and the plinth ignored): 1,135 ft3 = 42.0 CY = 85 ton at 150 pcf.
p.271 note 9: "estimated weight of the segment to be lifted is 110 ton". The current solid envelope model = 147 ton (SEGMENTAL-DESIGN.md).
The reading is 23 % below the note; filling the passage and the recess would add ~31 ton. No view supports a more solid interior
(A-A, B-B, both plans and p.272 A-A all show the recess, the 5'-0" passage and the 1'-6" void), so the note is reported, not used.

## 10. Polygons (inches, ready for a script)

Half outlines are for x >= 0 (mirror about x = 0); (x, y): y down from the deck top. (s, y): s from the anchor face.

```
# outer section, span face / typical (s 66..104), half, (x, y)
OUTER_TYP  = [(0,0),(255.5,0),(255.5,10),(206.5,10),(143.5,26),(123,108),(0,108)]
# outer section with the thickened wing (s 11..51), half
OUTER_THK  = [(0,0),(255.5,0),(255.5,26),(143.5,26),(123,108),(0,108)]
# s 51..66: wing soffit = max(typical, 10 + 16*(66-s)/15) up to 26 (tip taper plane)
# top slab nose (s 0..11), half
NOSE       = [(0,0),(255.5,0),(255.5,26),(0,26)]
# expansion joint blockout (s 0..26), removed, half
BLOCKOUT   = [(0,0),(246.5,0),(246.5,16),(0,16)]
# anchor recess (s 11..26), removed, half
RECESS     = [(0,26),(119,26),(125.125,33.875),(109.09,98),(0,98)]
# solid wedges left in the recess, (s, y), x ranges as noted
TOP_WEDGE  = [(17,26),(26,26),(26,45)]      # x 79 .. web inner face
BOT_WEDGE  = [(26,80),(26,98),(20,98)]      # x 61 .. web inner face
# bottom slab notch at the anchor end, removed through y 98..108, plan (x, s), full
SLAB_NOTCH = [(-36,11),(-21,26),(21,26),(36,11)]
# diaphragm opening / passage s 11..66 (inside the recess for s 11..26), removed, full
OPENING    = [(-61,98),(-61,57.5),(-21,26),(21,26),(61,57.5),(61,98)]
# passage s 66..86, removed, full
TUNNEL     = [(-61,9),(61,9),(61,98),(-61,98)]
# typical void s 86..104, half (R 1'-0 fillet at the theoretical corner (127,26):
#   tangent points ~ (115.5,23.0) on the haunch and (124.1,37.5) on the web)
VOID       = [(0,9),(61,9),(127,26),(110.5,92),(81,98),(0,98)]
# longitudinal section at CL (A-A), solid outline, (s, y)
AA_CL_TOP  = [(0,16),(26,16),(26,0),(104,0),(104,9),(66,9),(66,26),(0,26)]
AA_CL_BOT  = [(26,98),(104,98),(104,108),(26,108)]   # bottom slab at CL (notched back to s 26)
# small items
VENT       = (73.5, s=89.0, dia 2.5, through the bottom slab, low side only)
CONDUITS   = [(115.0, 61.0), (117.5, 51.0)]   # dia 3.5, right of CL only, along s through s 26..86 (run not given)
INSERTS    = [(-50, 32), (50, 32)]             # on the face s = 26, axis along s, ~13" long (scaled)
PINTLE     = [(93.0, s?), (105.1, s?)]         # each bearing (x = +-99): dia 5.5, 11" up from the soffit; s not given (CL brg s 52.3 at the soffit)
PLINTH     = soffit s 31.0..74.8 x 84.4..114.2 ; bottom s 33.4..71.3 x 86.9..110.8 ; 2.3" .. 3.7" thick (CIP, scaled)
```

## 11. Open / conflicting items

1. Anchor face -> bent control line: not dimensioned on p.271 (scaled 9.7" at the deck top); p.161 / p.407 sketches disagree (CONFLICT, schematic).
2. CL bearing along the segment: only "5'-6" from the BCL" (VECTOR); its s position (52.3 at the soffit) is scaled; pintle pocket s NOT GIVEN.
3. Plinth size and thickness: SCALED (p.317-319: grouted plinth, provider bearing); not part of the precast segment.
4. Conduit holes: x scaled, run along s not stated, "right of CL" direction not defined.
5. Vent hole: "low side" - which side depends on each end segment's cross slope (NOT GIVEN on the sheet).
6. Note 9 weight 110 ton vs 85 ton from the reading (CHECK).
7. Minor drawing differences (dimension governs): wing taper drawn 1.67" short; plan chamfer-top line x 23.5 vs 21; plan top wedge x 81 / s 17.6 vs 79 / 17.
8. Purpose of the bottom slab notch (E) not labelled (geometry fully dimensioned).
9. Modular joint DIM "A" (p.333 table) not in the set.
