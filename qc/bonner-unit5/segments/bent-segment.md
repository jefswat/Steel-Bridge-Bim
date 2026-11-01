# Bonner Unit 5 - bent segment (DB / UB halves, bents 19-28): agent review plan reading

Plan set: `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf` (PDF page numbers).
Method: PyMuPDF text layer + vector linework (`work/vec.py`, `work/circles.py`, `work/text.py`), coordinates converted by
`work/bent_numbers.py`; crops in `img/` (p277_elev, p277_sec, p277_hpb, p277_ins, p278_BB, p268, p279, p280, p316) checked by eye.
Tags: **VECTOR** = dimensioned and/or reproduced by vector coordinates (< 0.05 in); **SCALED** = drawn only; **NOT GIVEN**.

Scale of every view used (p.277 SECTION / ELEVATION / PLAN, p.278 SECTION / B-B): 3/8" = 1'-0" = 2.25 pt per inch.
p.277 SECTION: CL girder X = 885.40 pt, deck top Y = 291.04 pt. p.277 ELEVATION (a longitudinal section at CL girder, same Y): CL split
X = 2034.50, bulkhead face X = 1899.60 (59.96 in). p.278 Section B-B is the same drawing 48.62 pt higher (every line reproduces).

Frame: x transverse from CL girder (+ right looking up-station), y DOWN from the deck top normal to it (all sections are drawn with a
LEVEL deck top: section frame), s along CL girder from the CL split (bent CL) outward to the bulkhead face. Inches.
p.277 note 1: details drawn for the DOWNSTATION half; upstation half "similar" (= mirror about the split plane; x unchanged).

## 1. Half length, depths, soffit step

| item | value | tag |
|---|---|---|
| half length (split to bulkhead face) | 5'-0" (60.0) | VECTOR p.277 ELEVATION "5'-0"", 59.96 |
| depth at the bulkhead face (CL) | 19'-0" (228.0); bottom slab 2'-0", W/2 7'-9" = p.268 table SECTION 1 | VECTOR p.277 elev (227.96), p.268 |
| depth from s = 0 to s = 3'-6" (CL) | 19'-3" (231.0) = bent depth | VECTOR p.277 SECTION "19'-3"" (230.98) |
| soffit step | 3" (dim "3"" p.277 elev + section; 6.80 pt = 3.02") | VECTOR |
| soffit profile | LINEAR taper over 1'-6" from the bulkhead face: y = 228 at s = 60 -> 231 at s = 42, flat 231 from s = 42 to the split (same line in p.278 B-B, mid point at s = 52 on it) | VECTOR (elev dims "1'-6"" + "3""; lines 1899.6/803.94 -> 1940.1/810.74) |
| web outer face | constant, 1:4 batter continues down 3"; soffit half width 92.25 (15'-4 1/2" overall, dimensioned at the LOWERED soffit) vs 93.0 at the bulkhead (7'-9") | VECTOR |
| CIP plinth under the bent soffit | "PLINTH (CAST-IN-PLACE)" ~4 3/4" under the soffit from about s = 35 1/2" to the split, sloped ends / bottom under each bearing - not precast | SCALED (no dims) |

Soffit (s, y) at CL girder: `[(0, 231), (42, 231), (60, 228)]`.

## 2. Pier diaphragm and the void around it  (CONFLICT with today's model)

The bent half is NOT solid from the split to 3'-6". Each half has a **2'-0" transverse diaphragm from s = 1'-6" to s = 3'-6"**, with
HOLLOW box cells on both sides of it:

| zone (s from split) | what | tag |
|---|---|---|
| 0 - 18 | hollow cell (the two halves' cells form one 3'-0" cell across the split); bottom slab 2'-9" thick (top y 198, soffit 231); top slab 9" + top blocks | VECTOR p.277 elev dims 1'-6" / 2'-0" / 1'-6" (s 18.00 / 41.96); p.278 B-B; p.280 D-D / E-E (bars) |
| 18 - 42 | solid diaphragm, web to web, top slab to bottom slab (y 9 .. 198), with the openings of item 3 | VECTOR |
| 42 - 60 | hollow cell; bottom slab top rises from y 198 (s 42) to y 204 at CL (s 60) while the soffit rises 231 -> 228 (bottom slab 2'-9" -> 2'-0"); typical bottom haunch appears toward s 60; top block from s 42 to s 54 only (6" short of the bulkhead face, dim "6"") | VECTOR |

Model today: `BentDiaphragm = 3'-6"` (solid from the split). Plans: solid only s 18-42. Consistent with it: vertical PT bars at 2'-6" =
diaphragm centre (vertical-pt.md); p.299 bottom temporary bar dead end on the diaphragm face 3'-6" from the bent CL (= the s 42 face).

Thicknesses inside the bent segment:
- top slab 9" between |x| = 5'-1" (p.268 typical haunch beyond: to y 26 at the theoretical web corner |x| = 127, R = 1'-0" fillet) - unchanged. VECTOR
- webs: same lines as the typical section (inner face |x| = 84 + 0.25 (198 - y), outer |x| = 92.25 + 0.25 (231 - y), 1'-4" normal). No thickening drawn. VECTOR
- bottom slab: 2'-9" (33") flat web to web for s 0-42 (no bottom haunch: slab top y 198 meets the web inner face at |x| = 84); at the bulkhead face (s 60)
  the p.268 section 1 slab: 2'-0" flat for |x| <= 54.5, then 2'-5 1/2" x 6" haunch to the web at (84, 198). The surface between s 42 and s 60 is the ruled
  surface between those two sections (p.277 half plan bottom draws the flat / haunch boundary as a straight diagonal from (84, s 42) to (54.5, s 60)). VECTOR
- TOP BLOCKS (under the top slab, both sides of CL): from the web inner face to |x| = 38.76, bottom level y = 51 (3'-6" below the top slab underside),
  top = top slab / haunch / fillet; s 0-18 (continuous across the split into the other half: plan top dashed edge s -18 .. +18) and s 42-54.
  Width dimensioned 6'-10" (TYP.) from the block face to the web inner face at the block bottom (120.76 - 82 = 38.76, = drawn 38.76).
  VECTOR (p.277 SECTION / elevation / half plan top; p.278 B-B; p.280 C-C). With the diaphragm they form one solid under the top slab
  outside |x| = 38.76 from s 0 to s 54. They carry the F1 / F2 future PT anchors (p.278 B-B: F1 1'-3", F2 3'-9" from the split) and the top temp PT ducts.

## 3. Openings through the diaphragm (each straight through along s, s 18 -> 42, both halves)

| opening | outline | tag |
|---|---|---|
| DIAPHRAGM OPENING (walk-through) | 5'-0" wide (+-2'-6" TYP.), 10'-0" high from the bottom slab top (y 198 -> 78), top corners chamfered 1'-3" horizontal x 2'-0" vertical (top flat +-15") | VECTOR p.277 SECTION dims 2'-6" / 1'-3" / 2'-0" / 10'-0"; elev lines y 78 / 102 between s 18 and 42 |
| UTILITY DIAPHRAGM OPENING | octagon 1'-4" wide x 2'-1" high, 4" chamfers, on CL girder, top 1'-4" below the top slab underside (y 25 -> 50) | VECTOR p.277 SECTION + UTILITY DIAPHRAGM OPENING DETAIL; elev lines between s 18 and 42 |
| 3 1/2" ELECTRICAL CONDUIT HOLES (RIGHT OF CL ONLY) | 2 round holes 3 1/2" dia: (83.5, 165.5) and (91.0, 153.5) = 6'-11 1/2" and +7 1/2" from CL, 5'-5 1/2" and 6'-5 1/2" above the bent soffit | VECTOR (dims 6'-11 1/2", 7 1/2", 5'-5 1/2", 1'-0"; circle 7.9 pt) |
| conduit holes along s | drawn only on the diaphragm face; that they run straight through the 2'-0" diaphragm is the reading of the section (p.387 note C: conduit access through the diaphragms) | SCALED / inferred |
| 2 3/4" ducts (temp PT at +-6'-6", 4'-9" above the soffit; vertical PT) | not needed (already modeled as hardware) | - |

Same x / y in DB and UB (mirror is about the split only); conduit holes stay RIGHT of CL in both halves (note "RIGHT OF CL ONLY").

## 4. Top / bottom slab openings

| item | value | tag |
|---|---|---|
| 3'-0" dia ACCESS OPENING through the bottom slab | circle R 1'-6" centred on CL girder ON THE SPLIT (half in each half), full bottom slab depth (y 198 -> 231); its edge is tangent to the diaphragm faces s = +-18 | VECTOR p.277 SECTION "3'-0" dia", HALF PLAN BOTTOM "R = 1'-6"" centred at the split; p.279 A-A quarter circle at the split; p.280 "OUTLINE OF ACCESS OPENING" |
| access door / frame | p.316 (A.O. details) is a 3'-0" x 4'-0" hatch at the segments listed on "Segment designations" - not this round opening; no door / grating detail for the bent opening | NOT GIVEN |
| top slab openings | none on p.277-280 (p.278 label "UTILITY ACCESS OPENING" points to the diaphragm octagon) | VECTOR (absence) |
| vent / drain holes | p.268 note 3 vent hole is for the typical segments; nothing for the bent cells | NOT GIVEN |

## 5. Bearing pintle blockouts

8" dia FULL DEPTH blockouts through the bottom slab (y 198 -> 231), 4 per bearing = 2 per half per web side:
x = +-4'-3" and +-5'-9" from CL girder (51 / 69; = CL bearing 5'-3" - 1'-0" and + 6": section "1'-0" (TYP.)" / "6" (TYP.)"),
s = 1'-1" from the split in each half (in the hollow-cell zone). VECTOR (p.277 HALF PLAN BOTTOM 4'-3" + 1'-6", 1'-1"; circles 18.0 pt = 8").
CL bearing 5'-3" from CL girder (2'-5 1/4" inside the 15'-4 1/2" soffit edge). VECTOR. Pocket detail: "BEARING DETAILS 3 of 3" (not read).

## 6. Differences between bents / halves

- p.277-280 are generic ("XX-DB / XX-UB"); no bent-specific dimension or note on these sheets; bents 23 / 24 not singled out. No difference found.
- DB vs UB: note 1 - upstation similar = mirror about the split; conduit holes right of CL in both.
- p.263-268 revision 1 note "dimensions do not reflect adjustments for bearing offsets where initially required, see bearing details sheet" (bents 19, 20,
  27, 28): concerns the erection offsets / closure lengths, not the bent segment shape; bearing details sheets not read - OPEN only if the pintles move.
- Modular joints are at bents 18 / 29 (end segments), not bent halves.

## Conflicts / open items

1. **CONFLICT (model vs plans)**: model `BentDiaphragm = 3'-6"` solid from the split; plans: diaphragm s 1'-6" .. 3'-6" (2'-0"), hollow cell s 0 .. 1'-6"
   (a 3'-0" cell across the split, entered by the access opening) and s 3'-6" .. 5'-0".
2. Top blocks (3'-6" deep, s 0-18 and 42-54) are not dimensioned along s by themselves: s values from the elevation dims 1'-6" / 6" (VECTOR); transverse edge
   from the 6'-10" dim (VECTOR). Fillets / chamfers at the block edges: none drawn.
3. Conduit holes' run along s: inferred (through the diaphragm).
4. CIP plinth: drawn only (SCALED), not precast.
5. p.277 and p.278 agree everywhere checked (no view conflict found).

## Polygons (inches; section frame x right +, y down from deck top; s from the split)

```
# void of the hollow cell, s 0 .. 18 (and s 42 .. 54 above the bottom slab of item 1/2):
VOID_BENT_CELL = [(-38.76, 9.0), (38.76, 9.0), (38.76, 51.0), (120.75, 51.0), (84.0, 198.0),
                  (-84.0, 198.0), (-120.75, 51.0), (-38.76, 51.0)]
# s 54 .. 60: no top block -> typical top (haunch (+-61, 9) -> (+-127, 26) theoretical corner, R 12 fillet) and the bottom
# of the s-dependent slab below; at s = 60 the void = p.268 section 1:
VOID_BULKHEAD_S60 = [(-61.0, 9.0), (61.0, 9.0), (127.0, 26.0),   # (127,26) = theoretical corner, R=12 fillet with the web
                     (84.0, 198.0), (54.5, 204.0), (-54.5, 204.0), (-84.0, 198.0), (-127.0, 26.0)]
# bottom slab top along CL girder: s 0..42 y 198 ; s 42 -> 60 linear 198 -> 204 (at |x|=84 always 198)
# soffit along CL girder (s, y):
SOFFIT = [(0, 231.0), (42, 231.0), (60, 228.0)]
# web inner face  |x| = 84 + 0.25*(198 - y) ; web outer face |x| = 92.25 + 0.25*(231 - y)

DIAPHRAGM_S = (18.0, 42.0)          # solid between these, x web to web, y 9 .. 198
DIAPHRAGM_OPENING = [(-30.0, 198.0), (-30.0, 102.0), (-15.0, 78.0), (15.0, 78.0), (30.0, 102.0), (30.0, 198.0)]
UTILITY_OPENING   = [(-4.0, 25.0), (4.0, 25.0), (8.0, 29.0), (8.0, 46.0), (4.0, 50.0), (-4.0, 50.0), (-8.0, 46.0), (-8.0, 29.0)]
CONDUIT_HOLES     = [((83.5, 165.5), 3.5), ((91.0, 153.5), 3.5)]      # (centre, dia), right of CL only, through s 18..42

TOP_BLOCK_S = [(0.0, 18.0), (42.0, 54.0)]   # |x| from 38.76 to the web, y from the top slab underside to 51
ACCESS_OPENING  = {"centre_x": 0.0, "centre_s": 0.0, "dia": 36.0, "y": (198.0, 231.0)}   # half in each half
PINTLE_BLOCKOUTS = [(x, 13.0) for x in (-69.0, -51.0, 51.0, 69.0)]   # (x, s) per half, dia 8, y 198 .. 231
```
