# ODOT Bridge O tub unit (spans 14-16): plan-review answers, 2026-10-13

READ-ONLY review. Nothing was edited. PDF page = "Vol 2_Phase 2B Bridge Combined.pdf" (O-n = PDF p.253+n). Sheets read and viewed: p.270, 273, 278, 281, 282, 283-288, 289, 290 (p.279 text only). Dimensions marked "scaled" were measured from the vector linework (Detail A on p.289 scales 3.00 pt per inch, checked against the printed 8" WP offset). They are NOT plan dimensions and stay UNRESOLVED as numbers.

Observations outside the 7 questions:
* The "TYPICAL TUB GIRDER SECTION" on p.270 shows 10'-7"* depth, 10'-10 15/16" web, 20'-3 13/16" top, 15'-0 5/16" bottom. It is a template section and contradicts p.273 and p.281 (4'-2" deep, 8'-0" between WPs, bottom 5'-11" + 2 x 3" = 77"), which the model follows.
* p.289 still says "GIRDER NO. 7 SHOWN, GIRDER NO. 6 SIMILAR"; p.290 note 1 lists piers 5, 7, 12, 13, 14 (leftovers from another job). No effect on the model.

---

## 1. (item 5) Top lateral bracing WT 9x38 (p.289 O-35, p.273)

**Plan (p.289)**
* Layout: WT 9x38 Warren pattern between working points (W.P.) on the CL top flange line of each web. W.P. is 8" along CL top flange each side of the CL stiffener at a top strut (or of a CL support diaphragm). At pier 13 (skewed) the first W.P. is 1'-0" along CL top flange from the CL support diaphragm. Top strut WT 6x29 at the CL stiffener.
* Notes: 1" dia A325 bolts, direct tension indicators (section 506), holes 1/16" larger than the bolts (1 1/16"), calibrated wrench not allowed.
* WT end / clearance: **the sheet gives no dimension for the WT end or for a clearance.** Detail A shows the WT flange outline (hidden lines, under the girder top flange) ending in an edge parallel to the girder axis, with the corner on the stiffener side clipped at about 45 deg. Scaled: that end edge is 2 13/16" to 2 7/8" (transverse to the girder) from the W.P. line, i.e. about 4 3/16" along the WT axis (WT axis about 43 deg to the girder axis). Not dimensioned: UNRESOLVED as a number.
* Detail A hole pattern, **6 holes per WT end**, 1" A325 bolts in 1 1/16" holes. Printed dimensions: pitch 3" and 3" along the WT axis; across the 11 1/16" WT flange 1 3/4" | 1 3/4" | 4 1/16" | 1 3/4" | 1 3/4" (= 11 1/16"); 1 3/4" from the WT end edge to the first hole row (TYP); 1 3/4" from the inner edge of the top flange to the lower hole row. Reading: two groups of 3 holes, one each side of the stem zone (4 1/16" wide), each a staggered row. Group A holes are 1 3/4", 3 1/2", 1 3/4" from the near flange edge at u = 0, 3, 6" along the axis; group B is mirrored (3 1/2", 1 3/4", 3 1/2" from the far edge) at u = 6, 9, 12". Scaled hole centres, left WT end, x toward the CL stiffener, y toward the tub centre, origin at the W.P. (+/- 1/16"): (-14 3/4, 8 3/4), (-11 3/8, 7 15/16), (-10 3/8, 4 5/8), (-6 3/8, 8 3/4), (-3, 8), (-2, 4 5/8).
* **Gusset / connection plate: none on p.289.** Detail A ("typical lateral bracing connection") shows only the WT flange outline and the 6 bolts: no plate, weld or gusset note. So the WT flange bolts directly to the girder top flange (inference from the hidden-line drawing; the sheet has no section). Vertical position of the WT flange against the top flange is not given: UNRESOLVED. (Compare p.282 Detail A: top strut WT 6x29 flange 1" below the top flange.)

**Code / inputs now** (`TubGirderGenerator.TubLaterals`, lines 541-618; csx line 279-281)
* `trim = Inch(4) / (2*T)` is a fraction of the WP-to-WP chord (2T = 96"): it is **4" transverse, = len/24 along the member (about 6 to 6 1/2" along a WT at 43 deg)**, not 4" along the WT. Plan, scaled: about 2 7/8" transverse (4 3/16" along). The model WT end is cut square to its own axis; the plan end is cut parallel to the girder axis with one clipped corner.
* Bolts: `BoltsPerLine = 3`, `BoltPitch = 3"`, `BoltGage = 4 1/16"`, first bolt 1 1/2" from the cut end: 2 parallel lines of 3 along the WT axis. Bolts with offset > half top-flange width - 1 1/4" are dropped ("off the flange"). The plan pattern is the staggered one above.
* No gusset built (consistent with the plan). `WorkPointFromPanelPoint = 8"` and `WorkPointFromSupport[0] = 12"` match p.289.

**Proposed change (not applied)**
1. `TubLateralBracingDefinition`: replace the hard-coded `Inch(4)/(2T)` by a property `EndSetback` (transverse distance, WP line to WT end edge); build script sets 2 7/8" with an UNRESOLVED flag "p.289 Detail A: WT end edge not dimensioned, scaled 2 13/16 to 2 7/8". Cut the WT flange end parallel to the girder axis, clip the stiffener-side corner (scaled 45 deg, flagged).
2. Replace `BoltsPerLine/BoltPitch/BoltGage` by an explicit hole list in WT-local (u along axis, v from the flange edge): group A (0,1.75) (3,3.5) (6,1.75); group B (6,7.5625) (9,9.3125) (12,7.5625). The u origin relative to the end edge is scaled only (flag). Do not drop holes "off the flange" (the plan holes are on the flange by drawing).
3. Add an UNRESOLVED flag for the WT vertical position against the top flange (no section on p.289).

---

## 2. (7b) Connection plates over the top flange

| Where | Plate | Plan p. | Model |
|---|---|---|---|
| Lateral bracing ends | none (WT flange direct to the girder flange, Detail A) | 289 | none (matches) |
| Top strut / internal cross frame | none over the flange. Stiffener PL 3/4 x 10 with a CLIP 1 1/2 x 3 at its top (Detail A), WT 6x29 strut bolted to the stiffener; gusset PL 3/4 at the K frame (Detail C); TAB PL 3/4 x 10 x 1'-0" at the bottom (Detail B1) | 282 | strut, stiffener, gusset, tab built; clip not built |
| External intermediate diaphragm | none (angles on the webs) | 282 | none (matches) |
| **Support (pier) diaphragms** | **TOP CONNECTION PL 1" x 12" bolted over the girder top flange and the diaphragm top flange.** Interior flange: 4'-3" long, 2 rows of holes, 7" gage, 2 1/2" edge, 2" end dims, 3 spa @ 3 1/2" = 10 1/2" each side of a 6" central gap. Exterior flange: 1'-5 1/2" long, 2 rows x 4 holes (2", 5 1/2", 4 1/2" dims). Pier 13 p.283-284, pier 16 p.285-286, piers 14-15 p.287-288 | 283-288 | **not built** (csx "Not built") |
| Support diaphragm top flange / studs | diaphragm top flange PL 1 x 12 (1 1/2 x 12 interior at piers 14-15); studs 2 - 7/8" x 7" @ 3 1/2" gage, 1'-0" max | 283, 285, 287 | built |

p.273 note 5: "transverse stiffeners and connection plates are welded to both flanges" (see Q7).

Proposed: add the top connection plate (PL 1 x 12; 4'-3" interior, 1'-5 1/2" exterior; hole pattern from the p.284/286/288 plan views, 1" bolts). Their position along the skew is shown only in the plan views: transcribe per pier before building (UNRESOLVED until then).

---

## 3. (7c) Diaphragm audit, starting at FS3-G2-FP1

"FS3-G2-FP1" is a model mark (field splice 3, G2, top flange plate 1; p.281: top plate PL 3/4 x 21 x 3'-2 1/2", filler PL 1/8 x 1'-9" x 1'-7 7/8" at splice 3 only). It is a splice part, not a diaphragm, and has no diaphragm counterpart on the plans. Audit by diaphragm type:

| Type (plan) | Plan members, plates, bolts (p.) | Model | Difference |
|---|---|---|---|
| Top strut, every panel point | WT 6x29, flange 1" below the top flange, bolted through the stiffener; stiffener PL 3/4 x 10; clip 1 1/2 x 3 (282 Det. A) | WT6x29, `StrutBelowWebTop` 1", 2 x 2 bolts, stiffener 3/4 x 10 | clip not built |
| Internal cross frame (K, solid points) | 2 L5x5x5/8, 3 bolts each end, gusset PL 3/4 (Det. C), tab PL 3/4 x 10 x 1'-0", stiffener 3/4 x 10 (282) | same; gusset outline 22" x 14" assumed | gusset outline and diagonal work lines UNRESOLVED (already flagged) |
| External intermediate diaphragm (6) | web PL 5/8 x 3'-2" (6" above and below inside 4'-2"), flanges PL 3/4 x 12, 2-L8x8x3/4 each end, 1/2" clear to the diaphragm web, girder-web leg 8 holes (7 spa @ 3 1/2" = 2'-0 1/2"), diaphragm-web leg 9 holes (8 spa = 2'-4"), 2" end dims, no studs (282) | same: angle 32", 8 / 9 bolts, no studs | none found (8 / 9 assignment confirmed by zoom) |
| End support diaphragm, pier 13 (283-284) | interior web PL 3/4, top flange PL 1 x 12, bearing stiffeners 1 x 8 both faces 1'-6" apart; exterior web PL 3/4, flanges PL 1 x 12, 2 bent PL 1/2 x 16 each end (8 spa = 2'-4" and 9 spa = 2'-7 1/2", length 2'-11 1/2"); top connection PL 1 x 12, bottom connection PL 1; corner clips 3 x 1 1/2; studs 2 @ 3 1/2" | web 3/4, flange 12 x 1, stiffeners 8 x 1 @ 1'-6", bent PL (L8x8x1/2), 9 / 10 bolts, 35.5" | connection plates, corner clips not built |
| End support diaphragm, pier 16 (285-286) | as pier 13 but 2-L8x8x1/2 angles (not bent plates), 8 spa / 9 spa | Angles L8x8x1/2, 9 / 10 bolts | plates, clips not built |
| Pier diaphragms 14, 15 (287-288) | see Q4 | see Q4 | see Q4 |

Misreading in the build script: the Unresolved text "exterior diaphragm height 2'-11 1/2"" (csx, support 0). On p.284 the 2'-11 1/2" is the **length of the bent plate** (9 spa @ 3 1/2" = 2'-7 1/2" + 2 x 2"), not a diaphragm height. The exterior support diaphragm web has no height dimension (drawn between its flanges, full depth): the model's full-depth build is consistent; reword the flag.

---

## 4. (7e) Pier diaphragms at the intermediate bents (piers 14, 15: p.287 O-33, p.288 O-34)

**Plan** (elevation 1/2" = 1'-0"; 17'-8" along CL bearing between WPs)
* INTERIOR support diaphragm (inside each tub): web PL 1 1/2" full depth; top flange PL 1 1/2 x 12 under the 1" x 12" top connection plate; ACCESS OPENING 1'-6" wide x 2'-8" high, R 8", 11" above the bottom flange (p.288 elevation). Bearing stiffeners PL 1 1/4 x 8 ("INS & FS": inside and far side), finished to bear on the bottom flange, tight fit at the top flange, normal to the bottom flange, 2'-0" apart. Section A-A: a STRUT PL between the two bearing stiffeners at the bottom of the opening, with 1" x 1" clips; thickness and width not given (UNRESOLVED).
* EXTERIOR support diaphragm (between tubs): web PL 3/4", flanges PL 1 x 12 top and bottom, 2-L8x8x1/2 to each tub web, girder-web leg 11 holes (10 spa @ 3 1/2" = 2'-11"), diaphragm-web leg 12 holes (11 spa @ 3 1/2" = 3'-2 1/2"), 3 3/4" / 4 9/16" / 2" end dims; typical angle to girder web: L8x8x1/2, 8" bolt line.
* TOP connection PL 1" x 12": interior flange 4'-3", exterior flange 1'-5 1/2" (Q2). BOTTOM connection PL 1" (Q5).
* Studs 2 - 7/8" x 7" @ 3 1/2" gage, 1'-0" max, 6" end dims. Corner clips 3 1/2" x 1 1/2" (p.288).
* Welds 5/16" fillet both sides typical. Notes: M270 Gr 50W, Zone 2 non-fracture critical, 1" A325 type 3 with DTI, holes 1 1/16", fillet welds stop 3/8" (+/- 1/8") from edges of angles / stiffener plates.

**Generator** (`TubSupportDiaphragm` + `TubDiaphragmBetween`; csx supports 1 and 2): interior web 1 1/2", interior top flange 12 x 1 1/2, exterior web 3/4", exterior flange 12 x 1, bearing stiffeners 8 x 1 1/4 at 2'-0" both faces, opening 1'-6" x 2'-8" R 8" at 11", angles L8x8x1/2 x 42.5" (3'-6 1/2"), bolts 11 / 12 at 3 1/2", studs at 12" max, +/-1 3/4".

**Differences:** top connection plates, bottom connection plates, corner clips and the A-A strut PL with 1" x 1" clips are not built; fillet welds not modeled. Angle length 3'-6 1/2" to be reconciled with the end dims printed on p.288 when transcribed. All other values read match.

**Proposed:** add optional members to `TubSupportDiaphragmDefinition` for the top connection plates (1 x 12 x 4'-3" / 1'-5 1/2"), the bottom connection plate (Q5), corner clips, and the strut PL (thickness flagged UNRESOLVED).

---

## 5. (7f) Bottom connection plates at the support diaphragms (p.284, 286, 288)

* The plate lies **beneath the bottom flange** ("BOTT. CONNECTION PL BENEATH (1")", p.284 / 286; "BOTT. CONNECTION PL 1"", p.288). It is **bolted** (black bolt symbols in the elevation, "SEE PLAN VIEW FOR BOLT SPA.", note 4: 1" A325) to the tub bottom flange and to the exterior support diaphragm bottom flange (PL 1 x 12): it laps both and carries the diaphragm bottom flange across to the girder bottom flange. It does not connect to a stiffener or to the web; the 3" x 1 1/2" corner clip sits at the web / bottom flange corner beside it. No weld callout on the plate itself.
* Dimensions (p.284 plan): 1" thick, 2'-2" overall along the skew direction. Diaphragm end 1'-0" wide (7" gage + 2 x 2 1/2"), 2 rows x 4 holes, 3 spa @ 3 1/2" = 10 1/2", 2" end dims. Girder end 1'-4" wide (8" + 8" about the CL), 8 holes in 2 columns x 4 rows in the tub bottom flange, 2" / 3 1/2" / 5" dims; "end of girder" 3" from the CL pier. The plate tapers from 1'-4" to 1'-0" (taper length not printed: UNRESOLVED). A 6" dia anchor-bolt hole is shown at pier 13 (top contact plate omitted).
* Piers 14-15 and 16: same layout (p.288, p.286), plate 1".
* Model: not built.
* Proposed: PL 1 x 12 (1'-4" at the girder end), 2'-2" long, with the 8 + 8 hole pattern, under the bottom flange; add the holes to the bottom flange and the exterior diaphragm bottom flange.

---

## 6. (7a) Girder end at pier 13 (G2) and the support skew

* "T3.9-TS" is not printed on the plan pages read: it is a model label (UNRESOLVED as a plan mark).
* Plan skew data: p.273 gives the **angles between the end support diaphragm line and the girders: 69 deg 02' 38" (G1) and 69 deg 18' 54" (G2)** at pier 13; 18'-11 13/16" along CL bearing between the girders (p.283); CL pier 1'-0" from CL bearing; W.P. at STA 301+53.72. p.254: pier 13 line to tangent 68 deg 35' 04.23", i.e. skew 21 deg 24' 55.8" (the model's 21 deg 20' 53" / 21.348 deg comes from the girder spans). Piers 14-15 and 16 are radial (p.273 "90 deg (TYP.) unless noted").
* Girder end and end diaphragm at the skew (p.273, 283-284): the internal and external end diaphragms lie on the skewed support line, the plan view marks the "END OF GIRDER" line parallel to that line, 3" from the CL pier at the bottom flange detail (p.284). Girder end 9 11/16" beyond CL bearing (p.278; 9" at pier 16). p.278 note 5: girder ends and end diaphragm web plates plumb after total dead load, end diaphragm flange plates parallel to the girder flanges.
* Model: skew 21.348 deg (csx lines 115-145, angles to the girders 44' / 33' smaller than p.273, already flagged). The diaphragm is built in the support-line plane (`AtInPlane`). The plates run along the girder station, so the girder end is probably cut square to the girder, not on the skew (code read only, not checked in the model).
* Proposed: add a skewed-end option so top flange, web and bottom flange end on the support line (9 11/16" at the girder CL, UNRESOLVED at which point of the end line it is measured).

---

## 7. (7d) Weld callouts on the tub girder sheets

Read from the drawings (fraction glyphs in the text layer are garbled): all symbols are fillet welds unless noted. The model builds no welds (csx G() line: flange-to-web and stiffener fillet welds of the tubs not modeled).

| Page | Weld | Type, size |
|---|---|---|
| 270 note 2 | studs "weld compression flange only" | stud welds |
| 273 notes 5, 7, 8 | transverse stiffeners and connection plates welded to both flanges; fillet welds stop 3/8" (+/- 1/8") from edges of angles / stiffener plates; weld sizes AWS minimum unless noted | notes |
| 278 / 279 | web to top flange and bottom flange, 3 callouts per girder elevation | fillet 5/16" both sides (TYP.) |
| 278 note 6 | bottom flange tension zones and webs fracture critical, fabricated per AASHTO/AWS D1.5 | note |
| 281 | field splice | none (bolted) |
| 282 | external diaphragm web to top and bottom flanges | fillet 5/16" both sides, TYP. |
| 282 | internal cross frame (Detail B1 tab and stiffener, Detail C gusset) | fillet 5/16" |
| 282 note 6 | fillet welds stop 3/8" (+/- 1/8") from edges | note |
| 283, 285 | interior and exterior support diaphragm web to flanges; bearing stiffeners ("weld bearing stiffeners to bottom flange, tight fit at top flange") | fillet 5/16" both sides, TYP. |
| 284, 286 | partial elevation (3 callouts), top connections at exterior / interior flange, bottom connection, bent PL / angle to diaphragm web; "FINISH TO BEAR BEFORE WELDING" at bearing stiffeners | fillet 5/16" both sides |
| 287, 288 | as 283-286 plus Section A-A strut plate / bearing stiffener | fillet 5/16" both sides, TYP. |
| 290 | door stop, drip plate (stop weld 1/2" from end), expanded metal tack welds; "ALL WELDS ARE AWS MINIMUMS UNLESS NOTED" | fillet 5/16", tack |

Counts per page are approximate (read from images); transcribe weld by weld before modeling.

---

## Summary of proposed changes (none applied)
1. Lateral WT end: replace the 4" / 96" fraction trim by an explicit end setback (scaled 2 7/8" transverse, flagged), end cut parallel to the girder axis; replace the 2 x 3 bolt lines by the Detail A 6-hole staggered pattern.
2. Add support-diaphragm top connection plates (1 x 12; 4'-3", 1'-5 1/2"), bottom connection plates (1" x 2'-2"), corner clips and the strut PL; reword the "exterior diaphragm height 2'-11 1/2"" flag.
3. Girder end at pier 13 on the skew; confirm the skew (p.254: 21 deg 24' 55.8" vs model 21.348 deg).
4. Welds: all 5/16" fillet, none modeled; transcribe per page first.
