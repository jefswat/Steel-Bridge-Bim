# Woodbury abutment + pier reinforcing - plan reading (task t70, review items 10a / 10b)

Source: `Woodbury 0817 Sioux City I-29 NB over Floyd Blvd_Rev1.pdf` (48 pages; Iowa DOT Design 817). PDF page p = sheet p-1 = design sheet p-2.
Pages read: p.10 (pier reinforcing plan / elevation, splice bar notes), p.11 (pier sections A-A cap, B-B column, C-C maskwall, footing layout, Detail 1 / D-D),
p.12 (pier bar lists, bent bars, pier notes), p.15 / p.16 (S / N abutment: rear elevation, Part Section A-A, B-B, C-C, spacing tables), p.17 (pile plans, step diagram),
p.26 (deck / abutment / diaphragm bar list + bent bars). All RASTER reading (the text layer's fraction glyphs are ambiguous), confidence RASTER unless a row says DERIVED / SCALED.

Files: `bar-list.csv` (107 rows: pier 2 x 36, abutment, pier-diaphragm rows of p.26), `placement.csv` (63 rows), `shapes.csv` (41 rows, bend diagram dims, from `../bend-diagrams.csv`).
Bar-list totals reconcile with the plan: Pier 1 epoxy 27,324 / stainless 1,639 / non-coated 5,854 lb; Pier 2 27,521 / 1,639 / 5,854; deck-abutment-diaphragm sheet epoxy 77,253 / stainless 5,818 / non-coated 165 lb
(the last includes deck / rail marks that are not in `bar-list.csv`).
Counts reconcile where the views give them: 5e1/5e2/5e3 = 29 + 30 + 31 = 90; 8g1 = 38 + 28 (S) + 40 + 29 (N) = 135 (+ 1 8g5); 8g3 = 36 + 38 = 74; 5k1 / 5k2 = 36 + 38 = 74; 6g4 = 2 x 5 x 2 abutments = 20;
7f1 33 = 3 x 11, 7f2 / 7g2 45 = 3 x 15, 9g1 42 = 3 x 14, 8f6 48 = 3 x 16 (8 circled piles x 2); #2 spiral 16 = 8 piles x 2 abutments, spacers 48 = 3 per spiral.

## Convention (sheet notes)
- "ALL DIMENSIONS ARE OUT TO OUT. D = PIN DIAMETER." Listed length = plain sum of the out-to-out segments (no bend deduction); a few round up to the next inch (see `../bend-diagrams.csv`).
- Cover: pier note p.12 / abutment notes p.15-16: "minimum clear distance from face of concrete to near reinforcing bar is to be 2 in unless otherwise noted or shown". Printed centre-line values: cap hoop CL 2 3/4 from the cap faces, cap top / bottom rows 3 1/2 CL, column bars 3 1/4 CL (across), 4 1/2 from the face ends (along), projection bars 2 3/4 CL, wing extension 2 3/8 CL, pile-cap section 3 in CL bottom / 2 1/2 in CL sides (p.15 B-B), footing 3 in dimensions at top and bottom (p.10).
- Coating: epoxy (cap, column, maskwall, abutment, diaphragm), stainless (stage-1/2 splice bars 10a3, 10b2, 5a6, 5d9, 5d11, 5d12, 5d15, 5d16, 5d18, 8f2, 8g5; projections as detailed, threaded for mechanical coupler: 10a3 / 10b2), non-coated (footing mats, 8f6, #2 spirals).
- Stage joint: the stainless bars project beyond the stage-1 end (pier cap west end; abutment end at the construction joint) 2'-0 (10a3 / 10b2), 3'-9 (5a6), 2'-4 (5d9), 4'-11 (8f2), with min embedments 11'-9 / 10'-5 / 3'-9 / min laps 2'-2 (5d9) / 4'-9 (8f2). Bar ends are to be kept clean for lapping / coupler; threaded length less than 1'-0 total (p.12 triangle note).

## Pier cap (p.10 / p.11 / p.12)
Cap 4'-3 5/8 x 5'-0 (4 in end reveals, as in the model). Top rows: 8 positions (4 + 3 eq spa 1'-6 5/16 + 7 + 3 eq spa 1'-6 5/16 + 4), second row 5 in lower with the 2 central positions omitted (6-10a2); bottom: lower row 8 positions (7 eq spa = 3'-7 5/8) + upper row 6 (6-10b1), 5 in apart; side bars 4 per face (5 eq spa = 3'-7 between the 5 in offsets). Hoops 5c1 are DOUBLE (two overlapped 3'-0 x 4'-8 hoops, offset across the width) at the printed chain (5 5/16, 6, 4 eq spa 4'-1, 28 @ 6, 4 eq spa 4'-1, 28 @ 6, 4 eq spa 4'-0, 1'-0 5/8). Pedestals: 5 per pier (one per beam line), 5n1 U (6 per pedestal, 5 eq spa, 3 in ends), 5m1 (5 per pedestal), 8a7 (5 dowels per pedestal, 6'-0 long, 3'-0 above the cap into the pier diaphragm, 3 in | 4 eq spa | 3 in). Maskwall (east end): 5-5c4 and 5-5c5 at 4 eq spa, 5d20 verticals (5), 6d16 bars from column 4's east projection face; 5c4 / 5c3 "place as shown", 2'-6 min embedment of 5c4 into the cap.
Columns (p.10 / p.11): 28 #10 per column (9 + 9 on the long faces at 8 eq spa = 4'-6 5/8, 5 per side at 6 eq spa = 3'-4 13/16), 6d projection bars 4 per projection face (3 eq spa), hoops 5e1 (2'-6 x 5'-6) + 5e2 (3'-7 1/2 x 4'-9) + alternate 5e3 ties at n equal spaces (28 / 29 / 30 eq spa, 3 in off the footing and 3 in under the soffit; printed "5a3" = 5e3). Footing: top mat 11-7f1 (10 spa @ ~11 in = 9'-4) x 15-7f2 (14 @ ~11 = 12'-10); bottom 14-9g1 (13 @ 8 1/2 = 9'-4) x 15-7g2; 10d2 dowels L 15'-11 at every column bar; 8f6 uplift V bars through the web holes of the 8 circled piles (Detail 1, 2-8f6 per pile).

## Abutment (integral, steel-frame section B-B p.15 / p.16)
Pile cap 3'-0 wide x 3'-3 (pile top 2'-0 above the cap bottom; piles in 24 in CMP casings), 5p1 double hoops around 8f1 / 8f5 (9 bars) and the 6p3 V bars at the piles; diaphragm cast with the deck: longitudinal 5d2 / 5d3 (front face, between the beams), 5d6 / 5d20 (back face, following the sloped / chamfered back face and the paving notch), paving notch 5d7 + 5k1 / 5k2, verticals 8g1 (both faces, into the pile cap 3'-3 embed) and 8g3 (back face, L 15'-9), 4t1 under every beam, wing extension (outer 3'-0 wing, level bottom of footing at El 1114.60 S / 1115.64 N) with 6g4 / 5d8 / 8f3 / 8f4 / 8f6 / 5p2. Footing bottom elevations 1113.71 (S, sloping to the wing) / 1113.91 (N) from p.15 / p.16.
Step diagram effect (p.17): the cap top / diaphragm bottom steps (D, E, F, G, H) change the vertical bar lengths only through the beam seat; bars are listed at one length (8g1 8'-9, 8g3 15'-9), i.e. the plan does not give per-bar lengths for the steps: 8g1 vertical extent on a step is UNRESOLVED (the plan list is one length).
Wing extension: the wing is 6'-6 along the gutter; 6g4 4 spa @ 1'-0 = 4'-0 with 5 bars per face and 1'-9 min embedment into the diaphragm.

## UNRESOLVED / OPEN (each: page, plain reason)
1. UNRESOLVED (p.10): 10a3 / 10b2 vs 10a1 / 10a2 / 10b1 at the stage joint end - the plan lists both sets in full (10a1 at 50'-3 and 41'-0 straight bars as well as 14 + 14 stainless bars) and does not say which positions the stainless bars take or where the epoxy bars stop.
2. UNRESOLVED (p.10 / p.11): 10a1 legs - leg vertical length, direction and the end at which they turn down: shape printed 4'-6 ; 41'-3 ; 4'-6 with 4'-5 15/16 vertical legs and a 2 1/2 in toe inset, which is as deep as the cap (5'-0 less cover); the cap bars the model places must not violate 2 in cover (leg 4'-6 in a 5'-0 cap).
3. UNRESOLVED (p.11): lateral positions of the 6 bars of the second top row (10a2) and the upper bottom row (10b1) - drawn only (SCALED), centre gap not dimensioned.
4. UNRESOLVED (p.10): 5c1 hoop chain ends 1 11/16 in short of the 42'-1 5/8 cap (the "6" dimension at the start is ambiguous); offset of the two overlapped hoops only scaled.
5. UNRESOLVED (p.10): 4c2 "@ ABT. 2'-0 CTRS" is approximate; 42 bars = 21 x 2 (top + bottom) is derived, which stations skip the pedestals is not shown.
6. UNRESOLVED (p.10): 5c3 (12) end split, 5c4 (11) split between maskwall (5) and cap end (6), spacing of the cap-end ones.
7. UNRESOLVED (p.10): 5m1 cover and lateral positions in the pedestal; 8a7 spacing run length (3'-8 pedestal gives 9 1/2 in DERIVED).
8. UNRESOLVED (p.10): vertical position of the 10d bar bottoms vs the 10d2 dowel (10'-2 min lap; the dowel 15'-11 = 1'-10 + 14'-1 reaches about 9'-5 above the footing top if bent on the bottom mat 4 in above the footing bottom, which is less than the 10'-2 lap). The model must therefore start the column bar below the footing top or the hook at 3 in different elevations - the plans do not say; and the dowel hook direction.
9. UNRESOLVED (p.11): footing mat layer order (7f1 vs 7f2, 9g1 vs 7g2) - existing U15; cover of the footing mats (only 3 in dims).
10. UNRESOLVED (p.12): Pier 2 5a6 is listed as "CAP, TOP, LONGITUDINAL" and Pier 1 as "SIDES" - plan inconsistency, drawing shows them with the side bars.
11. CONFLICT (p.12 vs p.11): maskwall hoop 5c5 listed 9'-11, printed segments 9'-7 (U9a); 5d20 has 5 bars vs "3 EQ SPA" (4 positions).
12. UNRESOLVED (p.15 / p.16 / p.26): abutment S / N splits of 5d2 (24), 5d3 (8), 5d7 (4), 5d8 (24), 5d9 (12), 5d11 (6), 5d12 (2), 8f2 (18), 8f3 (8), 5p1 (136), 5p2 (12), 5p4 (4), 6p3 (28), 4t1 (10) - only the pile plans / views show the marks; counts are not tabulated per abutment (only the 8g1 / 8g3 / 5k1 / 5k2 spacing tables give S / N counts).
13. UNRESOLVED (p.15): 8f1 / 8f5 - nine bars in the 3'-0 x 3'-3 pile cap section are drawn as dots only (no dimensions, 3 in / 2 1/2 in CL at the bottom / sides); 5p1 spacing chain along the abutment (only a typical 1'-0 and 4 eq spa ends).
14. UNRESOLVED (p.15): 5d2 / 5d3 per-gap distribution between beams (A-A dimensions 1'-5, 5 eq spa, 2'-2 typ. around each beam are bar clusters, not tied to marks), 8g1 equal spaces are not printed (counts only; "shift 8g1 bars in F.F. as necessary to miss beams").
15. UNRESOLVED (p.15 / p.16): the vertical extent of the stepped (D-H) pile cap top / diaphragm bottom vs the single listed bar length of 8g1 / 8g3 (list gives one length).
16. UNRESOLVED (p.15 / p.16): spiral (#2) vertical position at the pile top (only "7 turns, 21 in diameter, 3 in pitch" and the 2'-0 "top of pile" dimension) and the CMP casing bottom (U14).
17. NOT READ in this pass: pier diaphragm bars 5d10 / 5d13 / 5d14 / 5d15 / 5d16 / 5d17 / 5d18 / 5e1-5e7 (diaphragm) - superstructure (p.18-20 / p.26), their bend diagrams are in `shapes.csv`.
18. Pier cap end-face shrinkage and temperature bars (HANDOFF queue): none are on the Woodbury pier cap bar list - the plans do not provide them (nothing to place).

## Mark coverage
Pier marks (36 per pier): all 36 listed with placement in `placement.csv` (10a1-10a3, 10b1, 10b2, 5a5, 5a6, 8a7, 5c1-5c5, 4c2, 10d1-10d5, 6d13-6d16, 5d20, 5e1-5e3, 5m1, 5n1, 7f1, 7f2, 8f6, 9g1, 7g2, 10d2). Abutment marks: 5d2, 5d3, 5d5-5d9, 5d11, 5d12, 5d19, 5d20, 8f1-8f6, 8g1, 8g3, 8g5, 6g4, 5k1, 5k2, 5p1, 5p2, 5p4, 6p3, 4t1, #2 spiral + spacers (all of the p.26 abutment rows). Counts, lengths and weights = plan.
Weights: bar list weights as printed (reconciled above); placement.csv carries no weights.
