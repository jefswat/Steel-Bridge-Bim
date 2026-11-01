# Bonner Unit 5 - precast bent caps (bents 18-29) and substructure post-tensioning: agent review plan reading

Plan set: `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf` (PDF page numbers). Method: PyMuPDF text layer +
vector linework (cap outlines, pocket / duct / key rectangles, dimension line ends), crops rendered to verify (`work-caps/img/`).
Scripts: `work-caps/vec.py` (lines + text in a window), `rects.py` (solid 1.80 pt rectangles -> bent frame), `keys.py` (dashed key clusters),
`make_csv.py` (writes the CSVs beside this file). Tags: **VECTOR** = dimensioned and/or reproduced by vector coordinates (< 0.1");
**SCALED** = only drawn; **NOT GIVEN** = not on the sheets; **CONFLICT** = two plan statements disagree (reported, not resolved).

Frame: S along the bent line from **CL CAP** (+ right looking up-station), N from the **bent control line** (+ up-station), z down from the cap
top. CL cap = CL footing / column / cap, which is offset LEFT of -L- by 4 7/8" (18), 10 3/8" (19-22), 5 3/4" (23-28), 2 11/16" (29)
(Detail C, p.334-352; already in MISC-DATA.md and built by misc-bonner-unit5.csx). Plan scales: bent 18 / 29 cap plans 3/8" = 1'-0"
(27 pt/ft), bents 19-28 1/2" = 1'-0" (36 pt/ft).

Common to all caps (VECTOR text unless noted):
- ONE precast piece per bent, Special 8.0 ksi (SUMMARY.md). "MATCH CAST P/C CAP WITH P/C COLUMN SEGMENT BELOW" (p.354 / 359 / 363 / 367 / 371).
  The cap sits directly on the top column segment (no grout bed; the only 3" grout bed / UHMW shims are at the column base EL 17.00, p.383
  SHIM DETAIL + note 4). Joint material at the cap / column match-cast joint (epoxy or dry): **NOT GIVEN** on the substructure sheets.
  PT ducts are coupled / sealed across every column segment joint (p.383 note 8, TYPICAL P/C JOINT).
- Cap top LEVEL at the printed top of bent (piers.csv values = plan: 68.25 / 64.25 / 68.75 x3 / 68.83 x5 / 65.25 / 70.00). Bottom = top of column.
- Full-depth ACCESS OPENING into the hollow column with 3" x 3" corner fillets, covered by an Acudor stainless roof hatch bolted to the cap
  with cast-in inserts (Detail B p.354 / 359). Sections p.358 A-A and p.362 A-A / C-C show it through the whole depth.
- Drip bead: (2) 1" triangular grooves, 3 1/2" from the edge, all around the soffit (DRIP BEAD DETAIL p.356) - VECTOR text, groove spacing SCALED.
- CIP bearing pedestals on the cap top, "INSTALL AFTER PT BARS ARE STRESSED AND LOCKED OFF" (p.359 / 363 / 367); anchor bolt blockouts =
  corrugated non-metallic 5" I.D. tubes (p.356): bents 19-28 1'-8" below the cap top + 1'-2" above (cut to the pedestal), bents 18 / 29 1'-0"
  below + 1'-0" (Type A) or 2'-2" (Type B) above; Type A = 18G5, 18G6, 30G3, 30G4, span 29 right; Type B = 18G1-18G4, span 19 right and
  left, 30G1, 30G2, span 29 left.
- Grounding conduit (box girder lighting) vertical through the cap at bents 18 and 29 only: S = -6'-4 1/2"+- (VECTOR circle at S -6.372), N 0;
  diameter NOT GIVEN.

## 1. Bent 18 cap (p.354-358, S3-106..110)

Plan (VECTOR, p.354 outline lines at 1.80 pt; same figures on p.355 / 356 / 357 / 358):
- T shape, 5'-0" deep, PRISMATIC (no taper; front / rear / side views and sections A-A, B-B, C-C p.357-358 rectangular). 16'-0" total in N.
- WIDE part 43'-6" (S +-21'-9") from N = -6'-6" to N = +1'-6" (**8'-0" deep**, left dims 8'-0" / 8'-0"; vector y 707.3 -> 491.4 = 215.9 pt = 8.00 ft).
- STEM 23'-0" (S +-11'-6") from N = +2'-6" to N = +9'-6" (up-station = span 19 = Unit 5 side).
- Re-entrant corners: 1'-0" x 1'-0" 45 deg chamfers (S +-12'-6", N +1'-6") -> (S +-11'-6", N +2'-6") (dims "1'-0" (TYP.)" both ways; vector 27 x 27 pt).
- Outer corners square (no chamfer drawn); vertical edge chamfers NOT GIVEN.
- Plan area 533.0 sf; volume 98.7 CY gross, 96.4 CY net of opening + pockets (DERIVED).
- Access opening 3'-0" (S +-1'-6") x 3'-0", N -1'-9" .. +1'-3" (4'-9" from the down-station face) - VECTOR.
- Column 16'-0" (S) x 11'-0" (N) centred on CL cap / bent control line (p.334, p.376).
- PT pockets: 8 x 1'-3" square, 1'-6" deep, inside the cap (bars at S +-3'-0", N +-4'-9" and S +-7'-3", N +-2'-6"); see section 6.
- Shear keys in the cap soffit (dashed, VECTOR clusters): N faces S 0, +-4'-0" at N +-4'-9"; S faces S +-7'-3" at N +-1'-8" (10). Front view
  chain 3'-3" / 4'-0" / 4'-0" / 3'-3" (p.354) agrees. CONFLICT with the column: see section 7.
- Bearings / pedestals: see section 8.
- Adjoining unit (span 18, 6 girders 18G1-18G6 at S -18.349, -11.094, -3.839, +3.417, +10.672, +17.927: chain 7'-3 1/16" x2, 3'-10 1/16" | 3'-5",
  7'-3 1/16" x2, VECTOR text) on the wide part, CL span 18 bearing N = -3'-2"; elastomeric pads 1'-2" x 3'-0" (Detail A).

## 2. Bents 19-22 cap (p.359-362, S3-111..114)

- 18'-0" (S +-9'-0") x 13'-0" (N +-6'-6"), 5'-6" high: upper 2'-6" vertical, lower 3'-0" tapered to 17'-4" x 12'-4" at the soffit (4" per side) -
  VECTOR (outline + front / side view 2'-6" / 3'-0" / 4" (TYP.) and 8'-8" / 6'-2" bottom dims, p.359 / 361). Same for 23-28. Model already has this.
- Small corner chamfers: vertical edges ~3/4" (vector 2.3 pt), soffit edge ~3/4" (SCALED, no dimension).
- Volume 46.5 CY gross, 41.9 CY net of opening + pockets (DERIVED).
- Access opening 3'-0" (S +-1'-6") x 5'-6" (N +-2'-9"), 3" x 3" fillets, centred - VECTOR (643.8-751.8 / 370.9-568.9 pt).
- PT pockets: 8 x (1'-3" wide x 2'-5" long printed) x 1'-6" deep, OPEN on the cap top AND the side face (notches): N-face pockets at
  S +-2'-3", bars N +-4'-9" (1'-9" from the face); S-face pockets at N +-2'-0", bars S +-7'-3". Duct spacing 6'-9" / 4'-6" / 6'-9" and
  4'-6" / 4'-0" / 4'-6" (VECTOR). The front / side views draw the pockets as visible (solid) notches.
- Shear keys in the soffit "(TYP. 12 PLACES)": N faces S 0, +-4'-6" (front view 4'-2" / 4'-6" / 4'-6" / 4'-2" on the 17'-4" soffit; plan clusters
  at S +-4.500); S faces N 0, +-2'-8" at S +-7'-3" (side view; clusters N +-2.678).
- No corner ducts at the soffit (column groups: only group D reaches segment 4, p.378 table).
- Pedestal elevations (ELEVATION TABLE p.359): 19 L 65.24 / R 64.71; 20-22 L 69.74 / R 69.27.
- Bent 19 top column segment 4 has its own sheet (p.379); no cap difference between 19 and 20-22 except the elevations.

## 3. Bents 23-24 cap (p.363-366, S3-115..118)

- Same body, opening, taper, pocket size as 19-22 (VECTOR rectangles: identical outline and opening).
- PT pockets: N faces at S +-1'-11 1/4" (duct spacing 7'-0 3/4" / 3'-10 1/2" / 7'-0 3/4"; rectangles S -2.562..-1.312), S faces at N +-2'-0".
- **4 corner ducts** "6" DIA. x 2'-0" PT DUCT AT BOT. OF CAP (TYP. AT EA. CORNER)", 1'-11 1/2" from both faces (S +-7.042, N +-4.542) - VECTOR text;
  drawn at 2'-0" (circles at S +-7.00, N +-4.50): 1/2" drafting difference.
- Shear keys 12: N faces S 0, +-3'-10 1/2" (front view 4'-9 1/2" / 3'-10 1/2" x2 / 4'-9 1/2"); S faces N 0, +-2'-8".
- Pedestals: 6 anchor bolts per pedestal (3 x 2) instead of 4. Elevations 23 / 24: L 69.62 / R 69.36.

## 4. Bents 25-28 cap (p.367-370, S3-119..122)

- Same body / opening / taper / pocket size.
- PT pockets: N faces at S +-3'-0" (6'-0" / 6'-0" / 6'-0"), S faces at N +-2'-6" (4'-0" / 5'-0" / 4'-0").
- 4 corner ducts 6" x 2'-0" at the soffit, 1'-11 1/2" from both faces (as 23-24).
- Shear keys "(TYP. 10 PLACES)": N faces S 0, +-4'-0" (4'-8" / 4'-0" / 4'-0" / 4'-8"); S faces N +-1'-8" at S +-7'-3" (side view 4'-6" / 1'-8" / 1'-8" / 4'-6").
- Elevations: 25-27 L 69.62 / R 69.36; 28 L 66.04 / R 65.78. Bent 28 top column segment has its own sheet (p.379).

## 5. Bent 29 cap (p.371-375, S3-123..127)

- Bent 18 mirrored about the bent control line (N -> -N; "opposite hand"): wide 43'-6" part from N -1'-6" to +6'-6" (toward span 30, Unit 6),
  stem 23'-0" from N -2'-6" to -9'-6" (toward span 29 = Unit 5), 1'-0" chamfers at (S +-12'-6", N -1'-6") -> (S +-11'-6", N -2'-6"). VECTOR
  (outline y 285.6 / 501.5 / 528.5 / 717.5 pt about the control line 461.0).
- 5'-0" deep prismatic, top EL 70.00. Access opening 3'-0" x 3'-0", N -1'-3" .. +1'-9" (mirror of 18). Same 8 pockets, same keys (front view
  3'-3" / 4'-0" / 4'-0" / 3'-3"), conduit at S -6'-4 1/2"+-.
- Adjoining girders 30G1-30G4 at S -17.026, -5.526, +5.974, +17.474 (11'-6" + 5'-6 5/16" | 5'-11 11/16" + 11'-6"); CL span 30 bearing N +3'-2"
  (3'-4" from the up-station face); CL span 29 bearing N -5'-6".

## 6. Substructure post-tensioning (p.383-384 S3-135 / 136; column sheets p.334-353, p.376-381)

System (VECTOR text unless noted):
- 2 1/2" high-strength PT bars ("2 1/2" HS BAR ANCHOR PL DETAIL" p.383; "2 1/2" STAINLESS STEEL" label at the PT bar in the footing, p.336).
  Standard bars = uncoated ASTM A722 above the first coupler; STAINLESS PT bars below it (p.334 notes 5 / 6). Couplers stainless unless the whole
  coupler is >= 12' above MHW (p.383 note 10).
- Duct 4" I.D. unless noted (p.383 note 1); "2'-0" OVERSIZED DUCT" at couplers and at anchorages (6" max.) (AT STRESSING AND COUPLING LOCATION,
  ANCHOR DETAIL AT P/C COLUMN JOINT).
- Dead end: stainless anchor plate embedded in the footing / CIP column base with nut, heat-shrink seal (DEAD-END ASSEMBLY). Bars run up through
  the CIP column base and the precast column segments, coupled at segment joints, each group ending at the top of a column segment (anchor plate
  cast into that segment, nut in the 2'-0" oversized duct of the segment above) or at the cap top (column sheets: "PT DUCT GROUP PRESENT" tables
  p.376 / 378 / 380 / 381, PT BAR TERMINATION p.334 / 337 / 340 / 343 / 348 / 351).
- At the cap top (TOP OF BENT CAP, p.383): anchor plate CAST INTO the cap at the bottom of the blockout, PT bar nut / washer, grout cap, grout
  outlet pipe, bar cut after stressing "to accommodate grout cap and concrete cover"; blockout formed with corrugated duct or sides roughened to
  1/4" amplitude; CONST. JT. at the pocket faces (pocket concrete placed after stressing; fill material by implication, not named).
- Anchor plate (2 1/2" HS BAR ANCHOR PL DETAIL): 12" x 12" x 3 1/2", keyhole grout vent (1 1/16" R, 3/4" slot), hole dims 2 1/4" / 1 5/8"
  (VECTOR text). Note 9: "details shown depict design intent only; final details by the PT supplier". Plate top = pocket bottom, i.e. 1'-6"
  below the cap top (SCALED from the schematic detail + 1'-6" BLOCKOUT dims p.354 / 359).
- Stressing: from the cap top (and at couplers per stage); "STRESS PT BARS SYMMETRICALLY ABOUT BOTH THE CL OF COLUMN AND THE BENT CONTROL
  LINE ... UNTIL ALL BARS ARE LOCKED OFF AT A GIVEN ELEVATION" (p.376 note 7 and the other column sheets). Grout at the column base:
  2,000 / 4,000 / 8,000 psi before prestressing column segment 1 / segment 3 / the superstructure (p.383 note 4).
- Jacking force, lock-off force, elongations, bar lengths, coupler / nut sizes: **NOT GIVEN** (no table on p.383-384 or the column sheets;
  note 7 refers to the PT special provision).

Bars that reach the CAP (all plumb, at the cap pocket centres; ducts straight through the cap):

| cap | bars to the cap top (pockets) | corner bars ending at the cap soffit | column groups (top segment) |
|---|---|---|---|
| 18 / 29 | 8: (+-3'-0", +-4'-9"), (+-7'-3", +-2'-6") | none (corner bars terminate at the top of segment 2, p.334) | B (p.376: segs 3-5 = B) |
| 19-22 | 8: (+-2'-3", +-4'-9"), (+-7'-3", +-2'-0") | none | D (p.378: seg 4 = D) |
| 23-24 | 8: (+-1'-11 1/4", +-4'-9"), (+-7'-3", +-2'-0") | 4: (+-7'-0 1/2", +-4'-6 1/2") in 6" x 2'-0" ducts | E + D corners (p.380) |
| 25-28 | 8: (+-3'-0", +-4'-9"), (+-7'-3", +-2'-6") | 4: (+-7'-0 1/2", +-4'-6 1/2") in 6" x 2'-0" ducts | D + C corners (p.381) |

Cross-check (VECTOR): the cap pocket positions equal the column duct positions of the group(s) present in the top segment, 9" from the column
faces (column 16'-0" x 11'-0"; corner ducts 11 1/2" from both column faces = 1'-11 1/2" from the cap faces). The corner bars at 23-28 are
anchored at the column-top / cap joint (ANCHOR DETAIL AT P/C COLUMN JOINT: plate cast into the segment, nut in the 6" x 2'-0" oversized duct
above = the cap's soffit ducts).

## 7. Shear keys: cap soffit vs top of the column (CONFLICT)

Key detail p.384: typical 9 1/2" x 6 1/2" (11" x 8" at the face with 3/4" bevels), alternate 6 1/2" x 6 1/2" (8" x 8"), 1 1/4" deep, centred
on the "CL PT ducts and shear keys" line 9" from the column faces. Which type the cap uses: drawn ~7 1/2" squares on N faces (SCALED), NOT GIVEN.

| cap | cap soffit keys (cap sheets) | column top-segment keys (column sheets, after the "omit from top of segment n" notes) | |
|---|---|---|---|
| 18 / 29 | N faces S 0, +-4'-0"; S faces N +-1'-8" (10) | N faces S 0, +-4'-0"; S faces N 0, +-3'-6" (p.376) | CONFLICT on the S faces |
| 19-22 | N faces S 0, +-4'-6"; S faces N 0, +-2'-8" (12) | N faces S 0, +-3'-6"; S faces N 0, +-2'-8" (p.378, notes X / Y / Z) | CONFLICT on the N faces |
| 23-24 | N faces S 0, +-3'-10 1/2"; S faces N 0, +-2'-8" (12) | N faces S 0, +-1'-3 1/2" (note V omits +-3'-10 1/2" and +-5'-2" at the top of seg. 4); S faces N 0, +-2'-8" (p.380) | CONFLICT on the N faces |
| 25-28 | N faces S 0, +-4'-0"; S faces N +-1'-8" (10) | N faces S 0, +-4'-0"; S faces N 0, +-3'-6" (p.381) | CONFLICT on the S faces |

The cap is match cast on the column segment, so the two must coincide; both are dimensioned and drawn consistently on their own sheets.
User / plan decision; until then model the cap sheet positions (or omit keys).

## 8. Pedestals (cap sheets) vs model (build-bonner-unit5.csx)

| bent | pedestal | centre S | centre N | S x N | top EL | model |
|---|---|---|---|---|---|---|
| 18 | span 19 L / R | -7'-6" / +7'-6" | +5'-6" | 6'-2" x 4'-2" | 69.93 / 69.19 | DIFF: model centres it on the CL bearing (S +-8'-3"); plan 3'-10" inboard / 2'-4" outboard of the CL bearing (p.354 / 355) -> centre 9" inboard |
| 29 | span 29 L / R | -7'-6" / +7'-6" | -5'-6" | 6'-2" x 4'-2" | 71.20 / 70.79 | same 9" DIFF |
| 19-28 | L / R | -5'-3" / +5'-3" | 0 | 6'-0" x 6'-0" | table | matches |
| 18 | 18G1-18G6 (span 18) | girder lines | -3'-2" | 5'-4" x 2'-2" | 70.43 70.10 69.78 69.45 69.12 68.80 | not modeled (other unit) |
| 29 | 30G1-30G4 (span 30) | girder lines | +3'-2" | 5'-4" x 2'-2" | 71.42 71.13 70.84 70.55 | not modeled (other unit) |

Top elevations in the model `pedTop` = plan for all 12 bents. N of the 4'-2" pedestal = centred on the span 19 bearing (vector dim 327.1-439.6 pt).
Pedestals sit over the filled S-face pockets at 19-28 (both are within S +-6'-8" .. +-9'-0").

## 9. Differences: plans vs repo data / model

1. **Bent 18 / 29 outline** (build script CapOutline): wide part modeled 6'-6" deep (N -6'-6"..0 at 18), plan 8'-0" (N -6'-6"..+1'-6");
   stem modeled from N 0, plan from N +2'-6" with 1'-0" chamfers. Model 501.25 sf vs plan 533.0 sf (-31.75 sf, about -5.9 CY per cap). The
   script comment "43'-6" x 6'-6"" is the control-line-to-face distance, not the wide part depth. Re-entrant chamfers: the existing flag can be
   cleared (they are dimensioned).
2. Access openings (3'-0" x 3'-0" end bents, 3'-0" x 5'-6" others, full depth, 3" fillets): not modeled.
3. PT pockets (8 per cap) and corner soffit ducts (23-28): not modeled. Cap PT bars: not modeled.
4. End bent pedestal centre (9" inboard): see section 8.
5. Shear keys / drip grooves / 3/4" chamfers: not modeled (small).
6. piers.csv: cap heights, TOB correct; "precast_cap_height" fine. No cap plan data there (consistent with this file now).

## 10. Open / conflicting items

- CONFLICT: cap soffit shear keys vs column top-segment keys, all four cap types (section 7).
- CONFLICT (minor): pocket length printed 2'-5" (p.359 / 363 / 367), drawn 2'-4" (83.9 pt), duct offset 1'-9" + half pocket 7 1/2" = 2'-4 1/2".
- CONFLICT (minor): corner soffit ducts printed 1'-11 1/2" from the faces, drawn 2'-0".
- Drafting: p.359 front view draws the N-face pockets 1'-1" wide (39 pt) vs 1'-3" in plan; plan governs.
- NOT GIVEN: PT jacking / lock-off forces, elongations, bar lengths, coupler and nut sizes (PT special provision / supplier, note 9);
  cap-to-column joint material (epoxy?); pocket fill material (concrete implied); shear key type used in the cap; conduit diameter;
  vertical edge chamfer size (drawn ~3/4"); drip groove geometry beyond "(2) 1" triangular grooves, 3 1/2"".
- Cap reinforcing (p.355-358, 360-362, 364-366, 368-370, 372-375; bar list p.382 gives no quantities: "will vary at each precast element") not read here.

## 11. Data (CSV beside this file, written by work-caps/make_csv.py)

- `cap-outlines.csv` - plan polygons (S, N in decimal ft and ft-in); 19-28 top rectangle + soffit rectangle.
- `cap-pt-pockets.csv` - bar / pocket centres, pocket size, depth 1'-6", open face.
- `cap-corner-ducts.csv` - 6" x 2'-0" soffit ducts (23-28).
- `cap-shear-keys.csv` - key centres from the cap sheets (CONFLICT flag).
- `cap-pedestals.csv` - pedestal centre / size / top EL per bent incl. the adjoining units' pedestals on bents 18 / 29.

Bent 18 outline (S, N ft), counter-clockwise: (-21.75,-6.5) (21.75,-6.5) (21.75,1.5) (12.5,1.5) (11.5,2.5) (11.5,9.5) (-11.5,9.5) (-11.5,2.5)
(-12.5,1.5) (-21.75,1.5); z 0 to -5.00 below EL 68.25. Bent 29 = same with N negated (top EL 70.00).
Bents 19-28: top (+-9.0, +-6.5) z 0..-2.50; soffit (+-8.6667, +-6.1667) at z -5.50; linear taper between.
Openings: end bents S +-1.5, N -1.75..+1.25 (18) / -1.25..+1.75 (29); 19-28 S +-1.5, N +-2.75; fillets 0.25 x 0.25 at 45 deg.
