# PTC main span (Arch truss 18-20): combined fix list for approval (2026-10-27)

Sources: `connections-findings.md` (G / R / B / C / S / H rows), `bearings-walkways-sections-findings.md` (B / W / S / M rows; prefixed BW- below),
`gusset-layering-findings.md` (GL rows). Nothing has been changed yet. Each group = one fix batch; approve by number.

## 1. Generator changes (affect every truss model; optional inputs, other models unchanged)
| # | Fix | Rows |
|---|---|---|
| 1a | Rivet grip = contiguous steel along the shank for JOINT rivets (as connection rivets already do). 57,016 of 76,090 joint rivets have > 1/16 in. air in the grip. | GL08 |
| 1b | Built-up box / laced-channel members turned 90 deg (back-to-back vertical): option like `Rotated` for non-rolled sections. STRUT, LAT31, LAT50, L13-W14 floor lateral, FLAT21. | BW-S1..S3 |
| 1c | Stringers continuous over several floor beams (p.72: S9-S12 4-span, S13-S16 2-span) instead of one piece per bay. | BW-M2 |
| 1d | (Decision) Each ply / angle / cover plate of a member as its own element with mark + sheet, instead of one solid per member. | BW-M1 |

## 2. Bearings (truss floating)
| # | Fix | Rows |
|---|---|---|
| 2a | L11 / L11': gussets down to the p.80 Section E finished line (2'-4 below El. 98.2010) + shoe angles 1-L 8x6x7/8 each side, 1 1/2 tap bolts. Section F dims conflict (report). | BW-B1..B3 |
| 2b | L0 / L0': sole block 3/8 too high; add the 7/8 plate under the chord (p.73 "7/8 Tension Pl." - medium confidence); pin clear of the sole block. | BW-B4, B5 |
| 2c | Pier 19 masonry plate 9'-2 (as-built revision). | BW-B6 |

## 3. Gusset layering and plates under the gussets
| # | Fix | Rows |
|---|---|---|
| 3a | L11: 4th layer (3/4), in-to-in 26 3/4 (1 1/8 plates on the post webs), chord outer plies end at the joint; layer extents up L11-L12 per p.80 (scaled, flagged). Needs a 400-600 dpi read of p.80 sections B, D, G, H, K, L. | GL01-GL04, H01 |
| 3b | Joint plates from U8 / L8 on: outside web splice plates, inlay plates (L10), U22 pin plates - rows already in splices.csv. | GL07 |
| 3c | gusset-layers.csv clean-up (U0 in-to-in, rows for L1 / U22 / W14); read W14 (p.85) and L5 fills (p.75). | GL09-GL11, G03 |

## 4. Joint gussets and rivets
| # | Fix | Rows |
|---|---|---|
| 4a | rivets-ptc.csx: wind chord L13-W14 / rigid hanger L14-W14 rivets assigned to the diagonals (hanger 0 of 86; 52 overlapping heads at L14). | R08, R01 |
| 4b | Drawn joint rivets not placed (9,385 of 11,008 per face; L10-L14 worst): dump dropped positions, fix the causes. | R01, R02 |
| 4c | Web member ends overlapping 1/4-9/16 in. at most joints; verticals at L12-L14 end 10-14 in. high. | G01, R06 |
| 4d | Gusset free-edge angles 2-Ls 4x3 1/2x7/16 (U11: 5x3 1/2x7/16) at ~20 joints + their rivets. | G02 |
| 4e | Chord cover splice plates: 1 1/8 rivets (38 plates per truss half). | R04 |
| 4f | Rivets < 1 11/16 from gusset edges (374 per face); suspected outline misreads U11 / L10 / U5; L11 envelope (29 SF). | R05, G04-G07 |

## 5. Lateral / sway / portal bracing connections
| # | Fix | Rows |
|---|---|---|
| 5a | Lateral / strut plates on the laterals (now 2-3 in. off; 78 % of 20,838 rivets connect nothing); edge angles on the plate. Depends on 1b. | B01, B02 |
| 5b | Rivet counts per p.71 diagram (26 / 40 + special 28-50), not 40 / 36. Plan conflict rows C01-C02. | B03, C02 |
| 5c | Sway frames: strut connections at L1, L9, L10, L12 ("similar to L7 / L8", p.97); top connections to the floor beam (p.85: 2-1/2 gussets, 4-Ls 6x4x1/2, 1 in. rivets). | B04, B05 |
| 5d | L13 - L13 1/2: lower lateral double K (33 WF 130) + strut (p.98 13'-1 along CL chord). | B06 |
| 5e | Portal connections at U11 + U11 portal strut built-up box (p.71, now a solid envelope); LM0 apex plate; tongue beam WM22 K connection; F8 plate at L13. | B07-B11, BW-S4 |

## 6. Walkways
| # | Fix | Rows |
|---|---|---|
| 6a | FB11 walkway: El. 141.6035 (26.5 in. higher) + offsets 3'-0 3/8 to 5'-0 3/8 from CL floor beam; ladder length follows. | BW-W4, W5 |
| 6b | Add: p.105 top-chord catwalk with 1/2 wire-rope handline on L 2 1/2x2 1/2x3/8 posts ~10'-4, 3'-0 high; U22 beacon platform + railed walk; nav-light platforms W15 / W20 / W19'; south ladder piers 18/19; ladder cages, toe plates. Undimensioned parts flagged. | BW-W1..W3, W9, W10, W13 |
| 6c | Pier platform height 8 13/16; cross walk channel 8[13.75 vs 10[15.3 (plan conflict, report). | BW-W7, W11 |

## 7. Sheet links
| # | Fix | Rows |
|---|---|---|
| 7a | W14 gussets -> p.84 / 85; main floor beams / stringers off p.55 (approach sheet) onto p.72 etc.; wind chords + p.71; CL lateral LL-CL16 not under the N truss. | S01, S02, BW-M4..M6 |
