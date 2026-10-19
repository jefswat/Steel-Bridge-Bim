# Plan-review answers: Woodbury 817 and Br02069 (2026-10-13 review, items 10, 10a/b, 12a/b/c)

Read-only review. PDF page numbers (p.N) are 1-based PDF pages. Woodbury: PDF p = sheet - 1 = design sheet + 2. Br02069: PDF p = sheet.
Tags: TEXT = text layer, RASTER = read from the rendered drawing. Nothing was edited.

---
## 1. Woodbury item 10 - pier maskwall (the plans call it MASKWALL, never "checkwall") vs the barrier bump-out above

**Answer: no, they are not meant to match.** The plans draw them in two different frames and give no dimension tying them together.

| Item | Plan frame and dimensions | Page |
|---|---|---|
| Cap, reveal, maskwall | Cap plan is a rectangle 42'-1 5/8 x 4'-3 5/8, ends square to the CL pier (cap drawn at 90 deg to its own axis). East end: 4" projection x 2'-11 5/8 wide reveal (8" shoulder each side, 2'-1 13/16 each side of the pier CL); elevation: 5'-0 (pier 2) / 4'-11 (pier 1) from cap top K up to the maskwall top. Section C-C: maskwall slanted inner face, 8" / 1'-6 1/2 thick as the model reads it. Bars 5c4 / 5c5 / 5d20 "MASKWALL" | p.9 plan + elevation, p.10 elev. (View C-C), p.11 C-C, p.12 bar list (RASTER) |
| Skew of the pier | beam-to-pier plan angles printed on the cap plans: pier 1 11 deg 50'38" / 10 deg 12'27" (TYP.), pier 2 13 deg 55'07" / 11 deg 50'38" (TYP.); "extended tangent" 10 deg 09'36"; pedestal sides 78 deg 30'00" (TYP.). Plans say "design for variable skew (L.A.) tangent to roadway" | p.9 (RASTER), p.11 footing layout |
| Barrier bump-out | Square to the EDGE OF DECK: "90 deg 00'00" BARRIER BUMP-OUT" with CL bump-out where the CL pier meets the edge of deck; deck widening 1'-3 x 5'-2 (2'-7 + 2'-7); rail D-D/H-H brick face 4'-7 5/8 (2'-3 13/16 + 2'-3 13/16); strips G-G 5'-10 (2'-11 + 2'-11) | p.22 Detail C, p.38 D-D / G-G / H-H (RASTER) |
| Brick on the east end | View A-A / Detail E: maskwall end, 3 min / 5 max plain top; no angle given | p.13, p.14 |

Consequence: the maskwall faces run along the pier line; the bump-out faces run normal to the deck edge. They differ by the pier skew from the edge normal, about 10 deg to 14 deg, and the plans never reconcile this (UNRESOLVED: no plan statement that the maskwall follows the bump-out).

**Model now (substructure-woodbury.csx lines ~132-147, barrier-woodbury.csx ~64-80):** `CapBlocks` "maskwall" and "end reveal" are rectangles in the pier frame (S along the pier, N across), square to the cap; the barrier zones "bump-out Pier 1/2" use `Support = pier` and a width along the gutter (4'-7 5/8, strips 5'-10, deck widening 1'-3 x 5'-2), square to the edge per Detail C. That is the plan reading.
**Proposed input fix:** none. Do not rotate the CapBlocks to the barrier. If the user wants a different intent, it needs a plan source we do not have.

---
## 2. Woodbury 10a/10b - bar marks (bar lists: abutments/deck/diaphragms p.26 = DS 24; piers p.12 = DS 10; csv `woodbury-i29-floyd/bar-lists.csv`)

Note: the abutment sheets p.15-17 carry no bar list; they place the marks. The list is on p.26 (RASTER).

### Abutments (count, length per p.26; coating E=epoxy, SS=stainless)
| Mark (no., length) | Location text | Placed in views |
|---|---|---|
| 8g1 (135, 8'-9) E | abut. vert. both faces | S p.15 rear elev., A-A, B-B; N p.16 rear elev., A-A, B-B; spacing notes 38 BF / 28 FF (S), 40 BF / 29 FF (N) |
| 8g3 (74, 15'-9) E | abut. diaph. vert. B.F. | p.15 A-A, B-B; p.16 A-A, B-B (36 / 38 at BF) |
| 8g5 (1, 8'-9) SS | abut. vert. F.F. | N p.16 A-A (1 front face) |
| 6g4 (20, 7'-3) E | diaph. wing ext. vert. | p.15 rear elev., A-A, B-B ("5-6g4 each face, 4 spa @ 1'-0"); p.16 same + C-C |
| 5d2 (24, 7'-3), 5d3 (8, 5'-3) E | diaph. longit. F.F. | p.15/16 A-A, B-B |
| 5d5 (3, 4'-5) E | S diaph. end | p.15 A-A |
| 5d19 (3, 5'-5) E | N diaph. end | p.16 A-A |
| 5d6 (4, 38'-3) E / 5d20 (4, 38'-9) E | S / N diaph. longit. B.F. | p.15 A-A, B-B / p.16 A-A, B-B |
| 5d7 (4, 38'-6) E | paving notch longit. | p.15, p.16 A-A, B-B |
| 5d8 (24, 7'-8) E | diaph. wing ext. longit. | p.15, p.16 rear elev., A-A, C-C |
| 5d9 (12, 5'-2), 5d11 (6, 5'-7), 5d12 (2, 4'-7) SS | diaph. longit. (projecting at construction joint, p.15/16 A-A: 2'-4 proj, 2'-2 min lap) | p.15, p.16 A-A |
| 5k1 (74, 4'-9), 5k2 (74, 3'-5) E | paving notch | p.15/16 B-B, A-A (36 / 38 at BF) |
| 5p1 (136, 10'-6), 5p2 (12, 10'-7), 5p4 (4, 10'-8) E | abut. hoops, hoops at ends | p.15/16 B-B and rear elev. (5p2); p.17 pile plans (5p1 double-hoop spacing, 5p4) |
| 6p3 (28, 6'-8) E | abut. bottom at piles (V) | p.15/16 B-B, p.17 |
| 8f1 (9, 37'-10) E / 8f5 (9, 38'-9) E | S / N footing longit. | p.15 B-B, p.17 S plan / p.16 B-B, p.17 N plan |
| 8f2 (18, 10'-4) SS | footing longit. dowels | p.17 both pile plans |
| 8f3 (8, 10'-2), 8f4 (4, 8'-8) S, 8f6 (4, 8'-5) N | abut. extension longit. | p.15/16 rear elev., B-B; p.17 |
| 4t1 (10, 5'-4) E | under beams at abutments | p.15/16 rear elev., A-A, B-B |
| #2 spiral (16, 38'-6) + spacers (48) | pile spiral | notes p.15 B-B / p.16 B-B (7 turns, 21" dia, 3" pitch) |
| 6a17 (522 SS), 6a1.. deck, 5b1 (504) | deck bars shown at abutments only as "6a" / "5b1" | p.15/16 B-B |
| 5c3, 5c14 | barrier bars on N C-C | p.16 C-C (tabulated with the barrier bars, DS 36/37) |

Also drawn on the abutment sheets, not bars: 3x3 x 2'-4 1/2 bearing bar, 3/4 dia x 1'-3 coil rods (S/N), keyways 2x8.

### Piers (p.12 list, per pier; placing views p.10 reinforcing plan / elevation, p.11 A-A, B-B, C-C, footing layout)
| Mark (count; length) | Role | Views |
|---|---|---|
| 10a1 (8; 50'-3), 10a2 (6; 41'-0) | cap top longit. | p.10 elev., p.11 A-A |
| 10b1 (14; 41'-0) | cap bottom longit. | p.10 elev., p.11 A-A |
| 5a5 (8; 41'-0) E | cap sides longit. ("5a5 (E.F.)" p.10 elev.) | p.10, p.11 A-A "5a5 (TYP.)" |
| 5c1 (140; 16'-4) | cap hoops, double hoop spacing at ends | p.10 plan/elev., p.11 A-A |
| 4c2 (42; 4'-7) | cap transverse U, 2'-0 ctrs | p.10 elev., p.11 A-A |
| 5c3 (12; 6'-9) U, 5c4 (11; 3'-10) U, 5c5 (5; 9'-11 tapered), 5d20 (5; 7'-1 / 7'-2) | cap-end U bars and maskwall | p.10 plan / elev., p.11 C-C |
| 8a7 (25; 6'-0) | cap dowels (pedestal region, 5 locations) | p.10 plan / elev., p.11 A-A |
| 5m1 (25; 3'-4), 5n1 (30; 7'-10 U) | pedestal long. / transv. | p.10 plan / elev. |
| 10d1/10d3/10d5 (28 each) + 10d2 (84, L, 15'-11) | column verticals + footing dowels | p.10 elev., p.11 B-B |
| 6d13-6d16 (8, 8, 4, 4) | column projection bars | p.10 elev., p.11 B-B |
| 5e1, 5e2 (90 hoops each), 5e3 (90 ties) | column hoops/ties; 28 / 29 / 30 eq. spa. | p.10 elev., p.11 B-B |
| 7f1, 7f2, 9g1, 7g2, 8f6 (48 V) | footing mats, uplift anchors | p.11 footing details, Detail 1, D-D |
| 10a3 (14; 13'-9), 10b2 (14; 12'-5), 5a6 (8; 7'-6) SS | splice bars at the WEST (stage 2) cap end | p.10 plan / elev. + SPLICE BAR NOTES |

**Horizontal shrinkage and temperature bars on the cap END faces: none are marked as such.**
- Horizontal bars on the cap SIDE faces: 5a5 (8 per pier, 41'-0, "CAP SIDES LONGITUDINAL", elevation p.10 label "5a5 (E.F.)"; section A-A p.11 "5a5 (TYP.)" dots on the side faces). Count per face / vertical spacing not readable with certainty (UNRESOLVED: p.11 A-A, 8 bars total).
- West cap end face (stage-2 joint): stainless 5a6 (8, 7'-6: 3'-9 projection + 3'-9 min embedment, p.10 elevation) projects as lap-splice to the future stage 2 cap; also 10a3 (8 top + 6 top as 8-10a3 / 6-10a3) and 10b2 (8 + 6) "2'-0 proj.", threaded for couplers (splice notes p.10). Pier 2 p.12 lists 5a6 as "CAP, TOP, LONGITUDINAL", pier 1 as "SIDES" (list inconsistency, RASTER).
- East cap end face: 5c3 / 5c4 U bars (p.10 elev. "5c3 & 5c4 BARS (PLACE AS SHOWN)", 4 eq. spa.) and maskwall 5c5 / 5d20. No end-face horizontal mark.

### Which are already placed in 3D (grep of the csx inputs)
- Placed: pier 10a1, 10a2, 10b1, 5a5, 5m1, 5n1 (`r.CapBars`); footing 7f1, 7f2, 9g1, 7g2, 8f6 (`fd.Bars`); column 28 #10 verticals + 10d2-style lap dowels + generic #5 ties derived from the plan's equal spaces (stand-ins for 10d1/3/5, 5e1-5e3). Pile spiral #2 (`PileSpiralDefinition`, 21" dia, 3" pitch, 7 turns) is placed.
- NOT placed (schedule only, `notes`/U17 in substructure-woodbury.csx): 5c1, 4c2, 5c3, 5c4, 5c5, 5d20, 8a7, 10a3, 10b2, 5a6, 6d13-6d16, 5e1-5e3 as such; all abutment bars ("integral abutment rebar (p.26 bar list) is schedule only"); all abutment-diaphragm and pier-diaphragm bars (diaphragms-woodbury.csx lines 74, 132); deck bars (`Deck.Reinforcement.Enabled = false`, build-woodbury.csx); barrier bars (barrier-woodbury.csx `Reinforcement = false`).

---
## 3. Br02069 item 12b - staged construction: bars projecting from the deck / barrier / diaphragm for the widening?

**Answer: no. The set shows no projecting bars at the east deck edge, barrier or end diaphragm.**
- p.2 transverse section: 41'-0 out-to-out deck with a barrier on BOTH edges; "18'-0 FUTURE WIDENING" and 3 beams @ 6'-0 are dashed, east of the east barrier (RASTER). p.19 section repeats the 41'-0 deck with the Type S barrier and R501M on the east edge, nothing projecting.
- p.20 end-diaphragm plan: east end stops at "1" polystyrene Type B (EA. END)" with no bars projecting; p.21 bar list ends all marks at the deck/diaphragm face; p.22 barrier layout (R501M... R518E) is complete on both edges.
- What the widening provisions ARE: substructure only - abutment footing, stem and infill wall run to WL +36'-0 with 2" x 12" keyways ("CONSTRUCTION JOINT 2" x 12" KEYWAY", p.4, p.5, p.9, p.10, p.11), 3 future pedestals built now (bars A527M/A528M, B527M/B528M; SOQ footnote p.2), 1" Type B polystyrene at the east end of the end diaphragm (p.11 Detail C), p.28 slope paving "of future widening". p.1 note: "staging limits and details: see grading plans" (not in this set).
- UNRESOLVED: how the future deck, barrier and diaphragm tie to the existing ones (not drawn in this set; Br. 02070 not included).
**Model now:** rebar-deck / rebar-diaphragms / rebar-barrier-br02069.csx place no projections; build-br02069.csx has the infill wall only in the E 18'-5 and no mask wall at the E end. **Fix: none.**

---
## 4. Br02069 item 12c - semi-integral abutment (p.11 A-A and Detail A; p.21 A-A / B-B)

**Answer: there is no cap or air gap above the pedestal. The end diaphragm is cast onto the beam ends (it is the diaphragm, with a 3'-0 thickness); the movement is allowed by the bearing zone under the beams, polystyrene fillers on the ledge and riser, and a backer-rod-sealed back face.** (SUMMARY.md sec. 8)

Dimensions (x from CL brg, + toward the span; p.21 A-A / B-B, p.11 A-A, Detail A, Detail C, notes 1-3):
| Interface | Value |
|---|---|
| Back face of abutment AND diaphragm | x -2'-6 (chain 1'-3 + 3" + 1'-0) |
| CL end diaphragm / front face of diaphragm | -1'-0 (1'-6 + 1'-6) / +6" |
| Front face of stem | +1'-6; stem 4'-0 (back ledge 1'-6 + seat 2'-6) on the 6'-0 x 3'-0 footing |
| Seat riser | at x -1'-0, 1'-0 above the back ledge (ledge = footing top + 1'-7 1/4 S / 1'-4 3/4 N, note 3); seat slopes 1" down to the front face |
| Polystyrene on ledge under diaphragm back part | 1" Type A, x -2'-6 .. -1'-3 |
| Polystyrene at the riser | 3" Type B, x -1'-3 .. -1'-0, ledge up to a 3" x 3" chamfer (Detail A) |
| Back face joint | 1" backer rod, caulk sealant (p.11 Detail B), membrane waterproofing strip 24" wide (p.11 note 1; p.19 corner detail) |
| Pedestal | x -1'-0 .. +1'-4 on the seat (2" short of the front face), top at least 2" above the seat back edge (p.11 A-A "2" MIN."); pedestals 2'-4 across |
| Diaphragm front part | bottom drawn as the beam-bottom line (p.21), 3" above the pedestal top = the bearing (p.11 A-A, vector); fillet 1'-0 x 1'-0 under the slab at the front face |
| Beam end | 7 1/2" behind CL brg, inside the diaphragm; sole plate cast with beam (p.16, p.17 table) |
| Ends | 1" Type B polystyrene to the mask wall (W) / infill wall W end +17'-7 (E), p.20, p.11 Detail C |
| Paving bracket | 8" x 1'-4 deep at the back-face top, 1/2" x 7" bituminous felt; approach panel bars S617S tie into the diaphragm, 4 1/2" clr |
| Materials | diaphragm and stem: structural concrete 3B52; deck/diaphragm bars chromium-epoxy/epoxy/stainless S marks (p.21) |

**Model now (diaphragms-br02069.csx):** back part and front part sections, `OpenBelowBeams = true`, the 1" Type A and 3" Type B fillers, 1" Type B end fillers; matches the plan. Open item (already known): front-part bottom vs "3" above the pedestal" is vector-read, GAP one pad thickness for both beam ends. **Fix: none.**

---
## 5. Br02069 item 12a - 30MH beam bar marks (p.16, std Fig. 5-397.501 (mod.); no bill, beam bars are paid with the beam; bends in bend-diagrams.csv)

| Mark | Shape | Placement |
|---|---|---|
| G301E | straight 2'-6, top flange transverse, 1 1/2" clr top | one at each stirrup position ("space with G505E, G508E or G402E"), p.16 |
| G303E | bottom flange bars: 2'-11 horizontal + 3 1/2" offset + 1'-10 sloped 4:12; 1 1/8" clr (note 10) | pairs: 2-G303E with G505E, with G508E, with G402E over the end region (extent not dimensioned, UNRESOLVED) |
| G402E | hairpin 2'-10 high x 4" wide | at every other stirrup position ("G402E SPG. except as noted") |
| G505E | C: 2'-1 vertical + 3'-6 top flange leg + 1'-0 bottom leg; 2" clr (1 1/4" to web faces) | 2-G505E at the first station |
| G508E | L: 2'-10 + 1'-0 leg; 7" projection above beam; note 11: each pair may be replaced by one No. 5 bulbed bar | 2-G508E at the 2 1/2" group stations |
| G806E | 4 longit. No. 8 in top flange: 3 1/2" + 3 sps @ 9" = 2'-3 + 3 1/2" (= 2'-10); 5'-1 min. lap splice; note 7: two inside bars may sit against the stirrup | whole length; length not given (UNRESOLVED) |

Stirrup chain from the end of beam (each half, symmetric about CL span; 100'-6 out to out, 99'-3 CL brg to CL brg, bearing 7 1/2" from end): 2" + 4 sps @ 2 1/2" + 9 sps @ 4" + 19 sps @ 9" + 27 sps @ 14" + 6" to CL span (2 + 10 + 36 + 171 + 378 + 6 = 603" = 100'-6 / 2, closes). Source p.16 beam elevation (RASTER). Which of the five end positions take G505E vs G508E: only leaders (not to scale); the model reads G505E at station 1 and G508E at the other four of the 2 1/2" group (U11 PARTLY CLOSED).
Also on p.16: end debonding A 4 x 9'-0, B 6 x 13'-0, C 4 x 17'-0 (14 total); 44 strands (rows 18 / 18 / 8, Y 2 / 4 / 6); 6" bond-breaker strips at flange tips.
**Model:** beam-rebar-br02069.csx `StandardBeamRebar.MnDot30Mh(stations, 1, 4, ...)` with the chain above = 60 stations per half, G505E 1, G508E 4, G402E remainder; G806E and the G402E-paired G303E zone are not placed (check-br02069: 4 N/A).
