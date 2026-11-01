# Bonner Unit 5 - temporary erection PT bars at the bent segment, D1 and U1 (agent review)

Plan set: `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf`. Pages read: p.299 (S3-51 BOTTOM ERECTION PT BAR ANCHOR
BLOCK DETAILS), p.300 (S3-52 TOP ERECTION PT BAR ANCHOR BLOCK DETAILS), p.278 (S3-30 BENT SEGMENT POST-TENSIONING DETAILS), p.313 (S3-65 ERECTION
PROCEDURE), p.309-312 (CONSTRUCTION SEQUENCE: no bar information beyond "follow the cantilever erection procedure"), p.284 (note 9 only refers to p.299 / p.300 / p.313),
p.307 (anchor protection of the PERMANENT bars only).
Scripts: `work-erection/dumptext.py` (text blocks), `long.py` / `lines.py` (vector lines in a page region), `lengths.py` (piece positions + length check).
Renders: `img/p300_tA/tB/tC.png` (top views), `img/p299_bA/bB.png` (bottom views), `img/p300_AA.png`, `img/p299_CC.png`, `img/p278_BB.png`, `img/p313_st.png`, `img/p300_cd.png`.

Tags: VECTOR = printed text / dimension on the sheet (or a position read from the vector linework that agrees with a printed dimension to < 0.1"); SCALED = measured
on the vector linework, not dimensioned; NOT GIVEN = not on any sheet read. Coordinates: s = along the girder from the bent control line (CL of the split bent segments),
+ up-station (U side). The elevations on p.299 / p.300 are at 36 pt per foot (10'-0" bent segment = 359.9 pt); the bent CL is at x = 446.0 (p.300 "at bent"),
1946.6 (p.300 D1-U1), 555.1 (p.299 bent-U1), 1838.8 (p.299 D1-U1).

## 1. Geometry that carries the anchors

| item | value | tag |
|---|---|---|
| bent segment = two 5'-0" halves DB / UB, match cast at the bent CL; outer ends = BULKHEAD FACE | 5'-0" + 5'-0" | VECTOR p.299 / p.300 / p.278 |
| D1 / U1 length, their joint face to the bent = MATCH CAST FACE, far end = (FIXED) BULKHEAD | 14'-0" | VECTOR p.299 / p.300 |
| top anchor face inside the bent segment: face of the top end block, 6" inside each bulkhead face | s = +-4'-6" | VECTOR ("6"" dims p.300, all three top views; vector face at 284.1 / 608.0 = 4.497 ft) |
| top end block depth below the deck top (Section A-A = view on the bulkhead end) | 9" slab + 3'-6", 6'-10" wide, then the web fillet | VECTOR p.300 A-A |
| bent diaphragm walls (one per half): inner face 1'-6" from the bent CL, vertical PT bars 1'-0" further, wall outer face | s = +-3'-6" (2'-0" walls) | inner face + bar VECTOR (p.278 B-B "1'-6"", "1'-0" (TYP.)"); outer face SCALED 3.50 ft on p.278 / p.299 / p.300 |
| between the wall outer face and the bulkhead face the bent half is an open box (bottom slab + fillet only) below the top block | s = 3'-6" .. 5'-0" | SCALED (p.299 / p.300 elevations, p.278 B-B) |
| Type A block (top, D1 / U1 and every typical segment): 1'-6" long, tip-side face 2'-11" from the segment's bulkhead (tip) face | tip face at s = -+16'-1", other face -+14'-7" | VECTOR p.300 |
| Type C block (bottom, "AT SEGMENTS D1 AND U1 ONLY"): 2'-0" long (TYP.), tip-side face 2'-11" from the fixed bulkhead | tip face s = -+16'-1", other face -+14'-1" | VECTOR p.299 |
| top bar lines (2 per web): 7'-9" and 9'-8" from CL girder (10'-7" - 11" - 1'-11", 10'-7" - 11"), 2'-0 1/2" and 2'-8 1/2" below the deck top | 4 bars per bent | VECTOR p.300 A-A, p.278 |
| bottom bar lines D1-U1 (1 per web): 6'-6" from CL girder, 4'-9" above the bent soffit (= 14'-6" to the top slab) | 2 bars per bent | VECTOR p.299 C-C / D-D, p.278 B-B |
| typical bottom bars (Type B blocks): 2'-6" from CL girder, staggered 8" | (not in this zone) | VECTOR p.299 |
| coupler: 3 1/8" dia x 6 3/4"; bar end 3 5/8" (MIN.) past the spherical nut = coupler centre (3 3/8" each side) | gap nut-coupler ~1/4" | VECTOR (dims) / gap SCALED, coupler detail p.299 / p.300 |
| anchor plate and spherical nut | plate ~2 1/4" thick, nut ~2 7/8" long; plate width NOT GIVEN | SCALED (elevations 6.7 pt / 8.6 pt); vendor items |
| bar | 1 3/4" dia., 150 ksi, in 2 3/4" dia. ducts | VECTOR p.299 / p.300 / p.313 note 1 |
| stressing tail ("* = MINIMUM REQUIRED LENGTH FOR STRESSING") | 3'-0" (MIN.)(TYP.) past the nut | VECTOR p.300 (drawn 2'-9"), p.299 typical |

Where the plates bear (question 3):
- Top bent bars: on the outward face of the TOP END BLOCK of each bent half, 6" inside the bulkhead face (s = +-4'-6"). The 6" between that face and the bulkhead face
  is a recess of the end region below the top slab (the top slab and webs run to the bulkhead face; vector outline p.300). It is not a pocket that is filled: no
  fill note points to it and nothing can reach it once D1 / U1 are placed. Section A-A draws the two plates (tilted squares) on that face.
- Top D1 / U1 bars: on the tip-side face of the Type A block (2'-11" from the tip face), loose plate + spherical nut outward (stressing end).
- Bottom U1 bar, dead end: on the outward (down-station) face of the DB diaphragm wall, s = -3'-6" (SCALED), inside the open 1'-6" end zone of DB. Bottom D1 / U1
  stressing ends: on the tip-side face of the Type C block.
- No pocket / blockout for any temporary bar anchor: the plates bear on exposed block / wall faces inside the box (coupler detail: plate on a concrete face). The
  only "FILL IN POCKET AFTER STRESSING. SEE NOTE 3" (p.299) is at the PERMANENT bottom bent bars (bulkhead-face pockets, p.278 LONGITUDINAL PT BAR DETAILS:
  10 1/2" x 10 1/2" pocket, 10" x 10" plate). Note 3 on p.299 / p.300 (bonding agent before filling the blockout) applies to that pocket.

## 2. Pieces (question 1)

Length check with SCALED hardware (`lengths.py`): plate 2 1/4", nut 2 7/8", tail 3'-0", coupler protrusion 3 5/8".

| piece | top / bottom | from (anchor) | to (anchor) | printed min. length | geometry check | coupler | installed / stressed | final state |
|---|---|---|---|---|---|---|---|---|
| **T-B** bent piece, 4 (2 per web, 7'-9" / 9'-8" from CL, 2'-0 1/2" / 2'-8 1/2" below top) | top | DB top end block face s = -4'-6" (plate + nut, STRESSING END, 3'-0" tail into the D1 side, bar end s = -7'-11") | UB top end block face s = +4'-6" (plate + nut, dead end, 3 5/8" past the nut, bar end s = +5'-2 3/4") | 13'-3" VECTOR p.300 "TEMPORARY PT BARS AT BENT" | 13'-1.9" (+1.1" spare in the min.) | receives C-U at its UB end and C-D at the end of its 3'-0" tail | steps 2-5: both halves erected, epoxy, "STRESS PERMANENT PT BAR IN THE BOTTOM AND TEMPORARY PT BAR IN THE TOP TO SQUEEZE SEGMENTS TOGETHER"; 75 ksi (top bars) | destressed and removed, duct grouted |
| **C-U** coupler, 4 | top | on the T-B dead end | - | 6 3/4" x 3 1/8" VECTOR | s = +4'-11.4" .. +5'-6.1" (SCALED gap) | joins T-B and T-U1 | step 7 | removed |
| **T-U1** piece, 4 | top | coupler C-U, s = +5'-2 3/4" (crosses the bent / U1 joint) | U1 Type A block tip face s = +16'-1" (plate + nut, STRESSING END, 3'-0" tail) | 14'-6" VECTOR p.300 "BETWEEN BENT & U1" | 14'-3.4" (2.6" spare) | C-U | steps 6-7: U1 on the shoring tower, epoxy UB-U1, stress top + bottom; 75 ksi | removed, grouted |
| **C-D** coupler, 4 | top | on the end of the T-B stressing tail, inside D1 | - | as above | s = -7'-7.7" .. -8'-2.5" (2'-8" to 3'-2" inside D1); drawn at -7.35 .. -8.02 ft (SCALED) | joins T-B and T-D1 | step 9 | removed |
| **T-D1** piece, 4 | top | D1 Type A block tip face s = -16'-1" (plate + nut, STRESSING END, 3'-0" tail) | coupler C-D at s = -7'-11" | 11'-7" VECTOR p.300 "BETWEEN D1 AND U1" | 11'-7.0" (exact) | C-D | steps 8-9: D1 on the shoring tower, epoxy DB-D1, stress top + bottom; 75 ksi | removed, grouted |
| **B-U1** piece, 2 (6'-6" from CL, 4'-9" above the bent soffit) | bottom | DB diaphragm wall outer face s = -3'-6" SCALED (plate + nut, dead end, bar end ~4.6" past the nut SCALED, >= 3 5/8") | U1 Type C block tip face s = +16'-1" (plate + nut, STRESSING END, 3'-0" tail) | 23'-7" VECTOR p.299 "BETWEEN BENT & U1" | 23'-8.9" (needs 1.9" more than the printed min.) | its DB end receives C-B | steps 6-7; 75 ksi ("BOTTOM BARS ANCHORED IN SEGMENT U1 AND D1 - 75 KSI") | removed, grouted |
| **C-B** "BAR COUPLER", 2 | bottom | on the B-U1 dead end, in the open end zone of DB | - | as above | s = -3'-11.4" .. -4'-6.1" (fits the 3'-6" .. 5'-0" open zone) | joins B-U1 and B-D1 | step 9 | removed |
| **B-D1** piece, 2 | bottom | D1 Type C block tip face s = -16'-1" (plate + nut, STRESSING END, 3'-0" tail) | coupler C-B at s = -4'-2 3/4" | 15'-0" VECTOR p.299 "BETWEEN D1 & U1" | 15'-3.4" (needs 3.4" more than the printed min.) | C-B | steps 8-9; 75 ksi | removed, grouted |
| permanent bottom bent bars (for reference; not temporary), 4 per bent (3'-0" / 6'-4" from CL, 1'-5" above the soffit) | bottom | DB bulkhead-face pocket | UB bulkhead-face pocket | 9'-11 1/2" min. VECTOR p.299; 8'-3" plate to plate VECTOR p.278 table (note 8: plate to plate, jacking length extra) - consistent | - | none | step 5, 200 kip (p.278 table rev. 1) | remain, pocket filled (p.299 note 3) |

So the "D1 - U1" top bar is THREE coupled pieces per duct line (T-D1 + C-D + T-B + C-U + T-U1) with two anchor plates + nuts left on the bent block faces
(s = +-4'-6"), and the "D1 - U1" bottom bar is TWO pieces (B-D1 + C-B + B-U1) with the dead-end plate + nut on the DB wall face. Every piece is stressed by
itself from its own stressing end; the couplers pass the D1 piece's force into the already anchored bent / U1 piece.

## 3. Erection sequence for this zone (question 2) - p.313 CANTILEVER ERECTION PROCEDURE (TYPICAL), quoted

1. "ERECT TEMPORARY SHORING TOWER." 2. "ERECT AND POSITION FIRST OF TWO BENT SEGMENTS BY CRANE. STABILIZE THE SEGMENT." 3. "ERECT SECOND SEGMENT OF BENT SEGMENT PAIR,
POSITION AND STABILIZE THE SEGMENT BY CRANE." 4. "APPLY EPOXY USING APPROVED PROCEDURES TO SEGMENT FACE BETWEEN ERECTED BENT SEGMENTS UB AND DB."
5. "STRESS PERMANENT PT BAR IN THE BOTTOM AND TEMPORARY PT BAR IN THE TOP TO SQUEEZE SEGMENTS TOGETHER. RELEASE SEGMENT FROM CRANE." -> stage 1 = T-B (4) + permanent bars.
6. "LIFT BY CRANE AND POSITION SEGMENT U1 ON SHORING TOWER." 7. "APPLY EPOXY TO SEGMENT FACE BETWEEN UB AND U1. STRESS TEMPORARY PT BARS TOP AND BOTTOM." -> stage 2 = T-U1 (4, via C-U) + B-U1 (2).
8. "LIFT BY CRANE AND POSITION SEGMENT D1 ON SHORING TOWER." 9. "APPLY EPOXY TO SEGMENT FACE BETWEEN DB AND D1. STRESS TEMPORARY PT BARS TOP AND BOTTOM." -> stage 3 = T-D1 (4, via C-D) + B-D1 (2, via C-B).
10. "STRESS PERMANENT CANTILEVER TENDONS T1. LOWER JACKS SUPPORTING SEGMENTS UB AND DB." 11-12 alignment / plinth grout. 13-17: U2, D2 with the typical bars (Type A / B blocks), T2.
18-24: U3, D3, T3. 25-31: repeat. 32. "AT A MINIMUM, TEMPORARY PT BARS ARE TO REMAIN INSTALLED TO THIRD SEGMENT FROM TIP OF CANTILEVER AS LIFTING DEVICE ADVANCES."
33. remove erection equipment. 34. "GROUT ALL CANTILEVER TENDONS."

TEMPORARY PT BAR NOTES (p.313): 1. "TEMPORARY PT BARS ARE ASSUMED TO BE 1 3/4" Ø 150 KSI BARS JACKED AS FOLLOWS: TOP BARS IN SEGMENT - 75 KSI; BOTTOM BARS IN SEGMENT (TYP.)
- 45 KSI; BOTTOM BARS ANCHORED IN SEGMENT U1 AND D1 - 75 KSI". 2. "STRESSING SEQUENCE UTILIZED SHALL BRING SEGMENTS TOGETHER UNIFORMLY. STRESSING SEQUENCE OF TEMPORARY
PT BARS TO BE SUBMITTED TO THE ENGINEER FOR APPROVAL." 3. "AT COMPLETION OF CANTILEVER, ALL TEMPORARY PT BARS SHALL BE DESTRESSED AND REMOVED." 4. "EMPTY DUCTS SHALL BE GROUTED."
Step 5-7 diagrams (p.313 "STEPS 1 THRU 7" / "8 THRU 12") draw the top bar through the bent segment into U1 and the bottom bar from the DB side into U1, then both into D1.

Which bars remain: none. All temporary bars, couplers, plates and nuts are destressed and removed at completion of the cantilever (note 3; removal of the
plates / nuts / couplers is implied - they are loose hardware on exposed faces; not stated separately: NOT GIVEN). The 2 3/4" ducts are grouted (note 4). No bar
is cut off and left. Step 32 allows bars to be removed progressively behind the lifting device, keeping at least the bars to the third segment from the tip;
it does not say whether the D1 / bent / U1 bars come out before the cantilever is complete (contractor's submitted sequence, note 2).

## 4. The 3'-0" (MIN.) protrusion (question 4)

"* = MINIMUM REQUIRED LENGTH FOR STRESSING" (p.299 / p.300): jacking length past the nut, not a permanent stub. On the bent piece T-B the D1-side tail is
additionally the coupling stub for T-D1 (its end receives C-D, and the T-D1 length 11'-7" fits exactly when the tail is 3'-0" - `lengths.py`). The tails are
removed with the bars (note 3). Nothing remains in the finished bridge.

## 5. Open / conflicting items

1. CONFLICT (geometry, p.300): coupler C-U = plate + nut + coupler ~12" beyond the s = 4'-6" block face, but only 6" to the bulkhead face; the coupler
   (3 1/8" dia.) is drawn crossing the bent / U1 joint, about 6" into U1, where the plans show only the 2 3/4" duct. A coupler recess or enlarged duct at the
   U1 match-cast end is NOT GIVEN.
2. CONFLICT (geometry, p.300): coupler C-D sits 2'-8" .. 3'-2" inside D1 (end of the 3'-0" tail), inside the D1 2 3/4" duct (coupler 3 1/8"). How the coupler is
   placed / passes is NOT GIVEN (drawn without a duct around it).
3. DIFF (p.299): the bottom minimum lengths are 1.9" (B-U1 23'-7") and 3.4" (B-D1 15'-0") shorter than the geometry with the scaled hardware, i.e. the printed
   minimums leave less than 3'-0" tail unless the plate / nut are thinner than drawn (vendor items). Top pieces fit (13'-3" +1.1", 14'-6" +2.6", 11'-7" 0).
4. SCALED: DB / UB wall outer face 3'-6" (no printed dimension; from p.278 1'-6" + 2 x 1'-0" symmetry and the vectors), plate thickness / width, nut size,
   the 1/4" nut-coupler gap, the B-U1 dead-end protrusion (~4.6").
5. NOT GIVEN: plate size and the bearing surface preparation at the bent block faces (no pocket, no fill note); whether the D1 / U1 bars are removed before
   cantilever completion (note 2 / step 32); jacking force in kips (only stress 75 ksi; bar area is a product value, not on the sheets).
6. The p.299 / p.300 notes 3 (blockout fill) apply to the permanent bar pockets, not to the temporary anchors.

## 6. Modeling recommendation (question 5)

FINAL STATE (finished bridge, what the default model should show):
- No temporary bars, couplers, anchor plates or nuts anywhere (p.313 notes 3). Keep the Type A / B / C anchor blocks and the bent top end blocks (concrete stays).
- The 2 3/4" temporary ducts are grout-filled (note 4): model at most as grouted duct voids / nothing; they do not carry a bar.
- Keep the permanent bottom bent bars (FixedBars, 8'-3" plate to plate) with their filled pockets.
- Today's model puts the temporary hardware (one straight D1-U1 bar, couplers at +-5'-0") into the finished model; recommend tagging all
  `SegmentalPtBarKind.Temporary` items as an erection-stage set (not built in the final state), or at least flag them "removed at completion of cantilever (p.313 note 3)".

ERECTION STAGES (separate stage sets, per bent; s from the bent CL, + up-station):
- Stage 1 (steps 1-5): T-B x 4 (s -7'-11" .. +5'-2 3/4"; plates + nuts at +-4'-6" on the top end block faces; tail toward D1); permanent bottom bars.
- Stage 2 (steps 6-7, U1): C-U x 4 (s +4'-11.4" .. +5'-6.1"), T-U1 x 4 (s +5'-2 3/4" .. U1 Type A face +16'-1" + plate, nut, 3'-0" tail); B-U1 x 2 (plate + nut on the DB wall
  face -3'-6", bar from -4'-2 3/4" to the U1 Type C face +16'-1" + plate, nut, 3'-0" tail).
- Stage 3 (steps 8-9, D1): C-D x 4 (s -7'-7.7" .. -8'-2.5"), T-D1 x 4 (D1 Type A face -16'-1" + plate, nut, 3'-0" tail .. -7'-11"); C-B x 2 (s -3'-11.4" .. -4'-6.1"),
  B-D1 x 2 (D1 Type C face -16'-1" + plate, nut, 3'-0" tail .. -4'-2 3/4").
- Later stages: typical joint bars (Type A / B) per segment pair; all removed at completion of the cantilever (or progressively, keeping at least the last
  three segments from the tip, step 32).
Lengths: use the printed minimums where they fit the geometry (T-B 13'-3", T-U1 14'-6", T-D1 11'-7") and the geometry where it exceeds them (B-U1 ~23'-9", B-D1
~15'-3 1/2"), with a check row against the printed minimum; carry items 1-2 as CONFLICT flags on the couplers C-U / C-D.
