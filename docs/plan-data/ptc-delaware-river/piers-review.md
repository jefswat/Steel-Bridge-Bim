# PTC Delaware River Bridge - approach pier review (to-do t06)

Read 2026-10-20 from PDF p.6-10 (piers 1-16) and p.33-37 (piers 21-30), at 110 dpi for the page and 170-300 dpi crops for every dimension quoted.
Compared row by row with `piers.csv` (the file is NOT edited; corrections are proposed below). Convention in `piers.csv`: `a` / `b` = shaft size ACROSS / ALONG the bridge, `base_*_printed` = shaft size at the footing top as printed, `ftg_len x ftg_wid` = footing across x along the bridge.

## 1. Twin or single shaft - answer

**Every approach pier is a TWIN-SHAFT bent: two shafts, one under each truss / girder line, 58'-0 apart (shoe to shoe), on ONE common footing.** No pier in p.6-10 / p.33-37 is a single central shaft.
There is NO wall between the shafts below the cap; the only member joining them is the cap beam (piers 11-16, 21-26); the low piers have nothing but the footing between the shafts.

| Pier group | Plan says | Pages |
|---|---|---|
| 1-9, 28-30 | Twin tapered shafts 6'-6 square at the top, batter 1/4" per 12" on every face (printed "1/4 / 12" on the elevations), tops El. 44.437 ... 73.230 (west) / 73.230 ... 65.765 (east). One footing: type A (piers 3-9, 28-30) = dumbbell 80'-0 x 22'-0 x 5'-0 (end blocks 17'-0 x 22'-0 under the shafts, 7'-0 tapers, web 32'-0 x 8'-0); type B (piers 1, 2) = plain rectangle 85'-0 x 16'-0 x 5'-0 with a construction joint at the bridge CL. Shoes 58'-0 apart, anchor bolts "not shown / see sheet 10". | p.10, p.37 |
| 10 and 27 | Twin shafts, stepped tops (girder side El. 55.962, truss side El. 76.918; lifts 40.88 / 49.88 / 58.88 / 67.88), each shaft carries TWO shoe lines 4'-8 3/4 apart (pier 10: sta 658+61.651 and 658+66.380; pier 27: 706+94.380 and 706+99.109). Dumbbell footing 86'-6 x 24'-0 x 7'-0 (end blocks 24'-0, 6'-0 tapers, web 26'-6 x 12'-0). Revisions: p.9 rev 1 7-27-54 "footing revised to clear PRR duct, added Note 3", rev 2 10-26-54 "changed shafts and girder brgs"; p.36 rev 1 10-26-54 "changed shafts and girder brgs". | p.9, p.36 |
| 11-16 and 21-26 | Twin tapered shafts joined at the TOP by a CAP BEAM (rigid frame, own bars, "particular care" notes); open between the shafts below the cap (no strut / wall). Cap depth at the bridge CL / at the shafts: 11, 12, 25, 26 = 8'-0 / 11'-3 3/8; 13, 24 = 9'-0 / 12'-2; 14, 23 = 10'-0 / 13'-1; 15, 16, 21, 22 = 10'-0 / 13'-0. One rectangular footing under both shafts. All these sheets carry rev 1, 11-5-54 "Width of shafts and affected dimensions changed" - the printed dimensions are the REVISED ones. | p.6, p.7, p.8, p.33, p.34, p.35 |

Taper: 1/4" per 12" is printed on every elevation; base size = top size + 2 x height x 1/48 reproduces the printed base of piers 1-10, 27 and 28 within 1/16". The shafts of 10-16 and 21-27 show the same batter with a "vertical face" on the inner (cap side) face.

## 2. Table: plan vs `piers.csv`

Footing dims: across the bridge x along the bridge x thickness, bottom / top elevation. "OK" = same within 1/16" (or 0.001 ft).

| Pier | Plan says | piers.csv says | Difference | Page |
|---|---|---|---|---|
| 1 | Twin 6'-6 sq top El. 44.437 (rev 7-27-54), base 7'-9 1/2 sq, footing B 85 x 16 x 5'-0, El. 8.0 / 13.0; new 48" dia R.C. pipe through the footing zone | same | OK | p.10 |
| 2 | top 47.381 (rev 7-27-54), base 7'-9 1/2 sq, footing B 85 x 16, El. 12.0 / 17.0 | same | OK | p.10 |
| 3 | top 50.836 (rev 7-27-54), base 7'-11 sq, footing A 80 x 22 x 5'-0, El. 12.0 / 17.0 | same | OK | p.10 |
| 4 | top 54.568, base 8'-0 1/2 sq, A, El. 12.5 / 17.5 | same | OK | p.10 |
| 5 | top 58.300, base 8'-1 3/4 sq, A, El. 14.0 / 19.0 | same | OK | p.10 |
| 6 | top 62.033, base 8'-2 1/2 sq, A, El. 16.0 / 21.0 | same | OK | p.10 |
| 7 | top 65.765, base 8'-3 1/2 sq, A, El. 18.0 / 23.0 | same | OK | p.10 |
| 8 | top 69.498, base 8'-3 1/2 sq, A, El. 21.5 / 26.5 | same | OK | p.10 |
| 9 | top 73.230, base 8'-5 3/4 sq, A, El. 20.5 / 25.5 | same | OK | p.10 |
| 10 | Twin, stepped (76.918 / 55.962); base 8'-6 3/4 across x 12'-6 along (B-B: 5'-9 + 12'-6 + 5'-9 = 24'-0); top 6'-6 across x 5'-11 1/2 along (upper block; the lower step block adds 4'-11 on the girder side); footing 86'-6 x 24 x 7'-0, El. 20.5 / 27.5 (rev 10-26-54) | a 6.5, b 10.6875, base 0 / 0; footing OK | base not filled: propose 8.5625 / 12.5. b = 10.6875 is not a printed value; printed along-bridge widths are 5'-11 1/2 (top block), 10'-10 1/2 (= 5'-11 1/2 + 4'-11, step block, derived) and 12'-6 (base). UNRESOLVED (p.9): which level b should stand for | p.9 |
| 11 | Twin + cap, top 62.312, 8'-4 3/4 x 9'-0, base 9'-2 1/4 x 10'-7, cap 8'-0 / 11'-3 3/8, footing 94 x 24 x 8'-0, El. 16.5 / 24.5 | same | OK | p.8 |
| 12 | Twin + cap, top 68.339, 8'-4 3/4 x 9'-0, base 9'-4 x 10'-10 1/2, cap as 11, footing 94 x 24 x 8'-0, El. 15.5 / 23.5 | base 0 / 0 | propose base 9.3333 / 10.875 (printed on the pier 12 half plan) | p.8 |
| 13 | Twin + cap, top 75.357, 9'-10 x 10'-0, base 11'-0 x 12'-4, cap 9'-0 / 12'-2, footing 96 x 24 x 8'-0, El. 11.0 / 19.0 | same | OK | p.7 |
| 14 | Twin + cap, top 83.005, 11'-2 1/2 x 11'-0, base 12'-6 3/4 x 13'-9, cap 10'-0 / 13'-1, footing 98 x 28 x 9'-0, El. 8.0 / 17.0 | same | OK | p.7 |
| 15 | Twin + cap, top 91.525, 12'-4 x 11'-0, base 14'-0 1/2 x 14'-4 3/4, cap 10'-0 / 13'-0, footing 98 x 40 x 8'-0, El. 2.0 / 10.0 | base_b 14.1042 | base_b -> 14.3958 (14'-4 3/4) | p.6 |
| 16 | Twin + cap, top 99.168, 12'-4 x 11'-0, base 14'-1 1/4 x 14'-6 1/2, cap 10'-0 / 13'-0, footing 100'-0 x 30 x 10'-0, El. 4.0 / 14.0 | ftg_len 98; base 14.0417 / 14.1042 | ftg_len 98 -> 100 (section D-D prints 100'-0); base_a -> 14.1042; base_b -> 14.5417 | p.6 |
| 21 | Twin + cap, top 99.168, base 14'-3 1/2 x 14'-11, footing 100'-0 x 30 x 10'-0, El. -5.0 / 5.0 (section A-A prints 100'-0) | ftg_len 98; base 0 / 0 | ftg_len 98 -> 100; base 14.2917 / 14.9167 | p.33 |
| 22 | Twin + cap, top 91.525, base 14'-0 1/2 x 14'-4 3/4, footing 98 x 40 x 8'-0, El. 2.0 / 10.0 | base 0 / 0 | base 14.0417 / 14.3958 (same as pier 15) | p.33 |
| 23 | Twin + cap, top 83.005, top 11'-2 1/4 x 11'-0, base 12'-7 1/4 x 13'-10, footing 98'-0 x 28 x 9'-0, El. 5.5 / 14.5 | ftg_len 96; base 0 / 0 | ftg_len 96 -> 98 (section A-A prints 98'-0); base 12.6042 / 13.8333 | p.34 |
| 24 | Twin + cap, top 75.357, 9'-10 x 10'-0, base 11'-2 x 12'-8 (top view 12'-8 1/4), footing 96 x 26 x 8'-0, El. 3.0 / 11.0 | base 0 / 0 | base 11.1667 / 12.6667 | p.34 |
| 25 | Twin + cap, top 68.339, 8'-4 3/4 x 9'-0, base 9'-6 1/2 x 11'-3 1/2, footing 94 x 27 x 8'-0, El. 5.0 / 13.0 | base 0 / 0 | base 9.5417 / 11.2917 | p.35 |
| 26 | Twin + cap, top 62.312, 8'-4 3/4 x 9'-0, base 9'-4 3/4 x 11'-0, footing 94 x 27 x 8'-0, El. 6.0 / 14.0 | base 0 / 0 | base 9.3958 / 11.0 | p.35 |
| 27 | Mirror of pier 10, twin, stepped; base 8'-11 1/4 across x 12'-10 1/2 along (5'-6 3/4 + 12'-10 1/2 + 5'-6 3/4); footing 86'-6 x 24 x 7'-0, El. 11.5 / 18.5; shafts and girder bearings changed 10-26-54 | base 0 / 0; footing OK | base 8.9375 / 12.875; b same remark as pier 10 | p.36 |
| 28 | Twin 6'-6 sq top 73.230, base 8'-7 1/2 sq, footing A 80 x 22 x 5'-0, El. 17.0 / 22.0, grade 27.0 | base 0 / 0 | base 8.625 / 8.625 | p.37 |
| 29 | top 69.498, base 8'-3 3/4 sq, A, El. 21.0 / 26.0 | same | OK | p.37 |
| 30 | top 65.765, base 8'-0 sq, A, El. 24.5 / 29.5 | same | OK | p.37 |

Piers 17-20 are outside this review (p.17, 27, 29).

## 3. Proposed corrections to `piers.csv` (not applied)

1. Pier 16 `ftg_len` 98 -> 100; `base_a_printed` 14.0417 -> 14.1042; `base_b_printed` 14.1042 -> 14.5417.
2. Pier 21 `ftg_len` 98 -> 100.
3. Pier 23 `ftg_len` 96 -> 98.
4. Pier 15 `base_b_printed` 14.1042 -> 14.3958.
5. Fill the missing bases (printed on the sheets): pier 10 8.5625 / 12.5; 12 9.3333 / 10.875; 21 14.2917 / 14.9167; 22 14.0417 / 14.3958; 23 12.6042 / 13.8333; 24 11.1667 / 12.6667; 25 9.5417 / 11.2917; 26 9.3958 / 11.0; 27 8.9375 / 12.875; 28 8.625 / 8.625.
6. Pier 10 / 27 `b` 10.6875: not a printed value (see table); decide which level it stands for.
7. Notes column: add "shaft widths revised 11-5-54" for piers 11-16 and 21-26 (p.6-8, p.33-35); 10 / 27 already carry their 10-26-54 note.

## 4. Other items read (not in piers.csv)

- Pier 1: new 48" dia R.C. pipe through the footing zone at El. +13.0, canal backfill to El. 25.0 (p.10, culvert sheet p.14).
- Pier 10: an existing P.R.R. 6-duct tile (concrete encased) crosses the footing corner (note 3, rev 1); the footing outline was revised to clear it (duct 21'-0 long, 4'-0 clear). UNRESOLVED (p.9): the exact notch outline - drawn only at 1/8" = 1'-0.
- The original (pre 11-5-54) shaft widths of piers 11-16 / 21-26 are not shown anywhere; only the revised ones are on the sheets.

## 5. Pages read poorly

None unreadable. p.9 pier 10 / p.36 pier 27 stepped shaft top (plan labels 6'-3 / 4'-11 / 5'-11 1/2 / 2'-2 3/8 crowd together) = medium confidence. p.34 pier 23 base along reads 13'-10 in the footing plan and 13'-10 1/4 in the top view (recorded 13'-10).
