# PTC main span - bearings, walkways, sections, members: RE-CHECK (2026-10-28 evening)

Re-check of `bearings-walkways-sections-findings.md` (2026-10-27) on the model rebuilt tonight after batches 1-7 (`TODO.md`, TRUSS-DESIGN.md sections 26-31).
Read-only: `samples/plans/PTC Delaware River Bridge.sbm` regenerated in memory once ("PTC Arch truss 18-20" + "PTC 4-span truss 14-17" for the pier 17 unit, no save,
`temp\cli-main\sbm.exe run`, 45 s). Scratch: `qc/ptc/main-span/work6/bws/` (`recheck.csx`, `gaps.py`, `areas.py`, `out/shoes.txt` = vertical ray stacks at 21 points per shoe,
`out/walk-parts.csv`, `out/sections.txt`, `out/whole.tsv` (whole members), `out/pieces.tsv`, `out/members-res.tsv`). Sheet links from `sbm db --sheets-for` on the current
companion database. No .sbm / csx / csv / src changes. Values in inches unless noted; "rel WP" = relative to the panel point.

Totals over the 45 old rows: **FIXED 20 / PARTLY 9 / STILL OPEN 14 / NOW WRONG 0 / OK (unchanged) 2**; **NEW 7**.

## 1. Status of the old rows

| id | status | measured now (model) | remaining / note |
|---|---|---|---|
| B1 | FIXED | L11 / L11' (all 4): gusset stack bottom 95.7843 = WP -29.00 = forging seat top (SHOE-*-L11-01); gap 0.000. Chord bottom still -17.87 rel WP, but the stack carries to the seat | 2nd / outer 3/4 layers cut at the chord bottom per Section F (CONFLICT E vs F reported, F governs) |
| B2 | PARTLY | outer 1-L 8x8x7/8 x 4'-1 each side (-09, bottom -28.00 = shoulder top, contact) + 4 inner 1-L 8x6x7/8 x 9 1/4 (-10, bottom -29.00 = seat, contact), 1 1/4 tap bolts + rivets built | transverse plate drawn black in Section F not built; angle ends / gages / pitches flagged (drawn only) |
| B3 | FIXED (conflict reported) | 1 1/8 + first 3/4 carried down into the 1 in. seat (2'-6 1/2 b. to b.) | user to confirm "Section F governs" |
| B4 | PARTLY | L0 / L0' (4): chord cover -> 7/8 plate (FL0-1) -> sole: -0.008 (contact) at the pin and +-9 in along; sole top = WP -18.75 (1'-6 3/4 = plan) | 1.493 air under the chord angle legs beyond the 24 in. plate (dn = +12); plate identity / width flagged (no callout) |
| B5 | FIXED | pin top = sole bottom (WP -23.00), no overlap | - |
| B6 | FIXED | pier 19 masonry "PL 6 x 110 x 110"; masonry -> pier top 0.000 | 6 vs 6 3/4 thickness CONFLICT stays (flagged) |
| B7 | PARTLY | forging seat (-01, top -29) between shoulders (-02, top -28) modeled | 2 3/4 key / 2 13/16 keyway not modeled |
| B8 | STILL OPEN | shear lock / hold-downs / 5 in. anchor bolts not built (UNRESOLVED p.73 warning) | later phase |
| B9 | OK (unchanged) | pins on the WP vertical (du = dn = 0.000; WP -> pin 24.500 / 47.000 = p.73 / p.96); every shoe piece / masonry / pier top 0.000 | - |
| W1 | PARTLY | catwalk U0-U22 north truss: 2 ropes 1/2 in. 1'-0 each side of CL truss, 2 x 65 posts L 2 1/2x2 1/2x3/8, rope top 3'-0 above the chord (post N65 264.07 -> 267.07), rope ends at U22 | sheaves, turnbuckles, sockets, base angles, end posts not built (flag); rope solid passes through the posts (holes not cut, 188 contact rows by design) |
| W2 | PARTLY | U22 platform railing (6 posts, top / mid rail, toe plates on 4 sides, 3'-0 = 264.106 -> 267.106) + strut railing W / E (7 posts each, n -39.25 .. -3.25) built | no floor listed / beacon / 1-L 7x4 / clip Ls not built; posts stand on the strut connection plate top 264.106 vs p.105 El. 262.762 (= the U22 WP) - flagged |
| W3 | PARTLY | nav light platforms W15 / W20 / W19' N + S: grating 6'-0 x 4'-6 1/2 on 8 [ 13.75, outboard + both end rails, toe plates, 5 posts each | hangers L 4x4x3/8 / connection Ls not built; see NEW N1 / N2 for the walks to them |
| W4 | FIXED | walkway at FB11 / FB11': channel top 141.6035 = plan El. (grating top 141.562) | the "27.74 vs 26.5" difference reported by batch 6 is a measuring artefact - see NEW N4 |
| W5 | FIXED | 3.031 .. 5.031 ft from CL FB11 (= 3'-0 3/8 .. 5'-0 3/8) | - |
| W6 | STILL OPEN | no hangers L 4x4x3/8 to the stringers, no 2'-0 x 1'-0 return at the CL | flagged |
| W7 | FIXED | pier platform grating top 88.4354 = pier top 87.701 + 8.8125 (8 13/16) | - |
| W8 | STILL OPEN | pier platforms L11 / L11' N + S: rails A / B only on the two ends at u = grating ends, each spanning the 3'-3 1/16 width (n -38.167 .. -34.911); none on the 8'-0 side drawn in View B; hangers 4-Ls 4x4x3/8 not built | unchanged from the old row |
| W9 | FIXED | south ladders piers 18 / 19: rungs 89.41 .. 103.46 (15.05 ft ~ 15'-0 1/2) | - |
| W10 | PARTLY | north ladders: 10 hoops 2 1/2 x 3/8 R 1'-0 3/8 from 95.08 (6'-9 above platform) to 140.67-140.87 (10 in. under the walkway) + 5 inside bars | Ladder Support "X" (2-Ls 3x2 1/2x3/8, 5-Ls 6x6x3/8) not built; cage depth flagged |
| W11 | FIXED (conflict reported) | cross walks 10 [ 15.3, walks along the wind chord 8 [ 13.75; CONFLICT text on the cross walks | user decision open |
| W12 | FIXED (user decision 2026-10-28) | CL walkway W15-W15': one L 3x2 1/2x3/8 per side, top 3'-0 above the grating (146.64 vs 143.75 at W15), no posts / mid rail | see NEW N3 (rails unsupported) |
| W13 | FIXED | toe plates 5 x 3/8 on the U22 platform (4 sides) and all nav light platforms (outboard + ends) | - |
| S1 | FIXED | STRUT / LAT31 / LAT50 turned: channels top / bottom, 32 1/4 deep (y +-16.125) x 19 / 14 wide, perforated side plates 29 1/2 x 1/2 | - |
| S2 | PARTLY | L13-W14 turned (13 5/8 deep x 16 wide, plates (13-6) x 1/2 on the sides) | 2-Web Pls 12 x 1/2 still not built (flag) |
| S3 | FIXED | FLAT21 b.b. 13 5/8, turned, plates 13 x 3/8 on the sides | - |
| S4 | FIXED | PORTAL U11 / U11' = p.71 box: PL 34 1/2 x 11/16, PL (33-13 1/2) x 11/16, PL 24 1/2 x 7/16, PL (35 1/2-13 1/2) x 9/16, 4 corner Ls 6x6x1/2; area 102.8 sq in (was 834 envelope) | manholes, end / knee connections flagged; sketch plumb side 35.46 vs the 34 1/2 plate (reported in the label) |
| S5 | STILL OPEN | U13-L13 model 157.81 vs printed 156.32 | user decision (plan-vs-plan) |
| S6 | STILL OPEN | U13-L14 model 116.69 vs printed 118.69 | user decision |
| S7 | STILL OPEN | U14-L15 model 120.69 (28 in.) vs printed 116.69 | user decision 26 / 28 |
| S8 | STILL OPEN | U18-L18 model 26 in. cover (p.88), within 0.6 sq in of the printed 94.17 | noted, no change expected |
| S9 | STILL OPEN | POST U11-L11 332.17 at the floor beam zone vs 293.67 / 217.17 printed | as read in QC I011 |
| S10 | STILL OPEN | box web o. to o. 2'-5 assumed (flagged) | - |
| S11 | FIXED | perforations cut: STRUT L0 web PL 30 x 1/2 1,629 kg vs 1,937 solid (84 %), LS-L5 side PL 29 1/2 x 1/2 1,597 vs 1,905 kg | (the old row may have read the flag text; the pieces carry `Perforation` now) |
| S12 | STILL OPEN | F8 extra angles 2-Ls 9x4x1, 2-Ls 7x4x7/8, 2-Ls 8x4x1 x 76'-0 not built (flag p.72 / p.95) | - |
| S13 | PARTLY | strut L13 1/2 (STRUT31: 2-18 [ 45.8 + 31 in. plates) and the double K of W33x130 built | tongue beam 16 WF 88 + cover plates (W21-W22-W21') not built; F8 apex gussets not built; W33 ends weakly connected (flags) |
| S14 | STILL OPEN | LS-L13 absent (F8 = strut, p.95) vs the p.71 diagram arrow | user decision |
| S15 | STILL OPEN | HP14x73 web vertical assumed (flag) | - |
| S16 | OK (unchanged) | floor beam / stringer / suspender make-up = p.72 / p.70 | - |
| M1 | FIXED | PartPerPiece: e.g. LS-L0 = 8 parts (W1-W2, A1-A4, C1-C2) under the member, FB11 = 9 pieces; 5,534 piece parts in the main span | - |
| M2 | FIXED | 132 continuous stringer members (11 lines x 12: 2-span S13-S16 / 4-span S9-S12) | S11 / S12 line identity and the p.72 stringer splice still open (batch 1c note) |
| M3 | STILL OPEN | 16 floor lateral X diagonals still 32 pieces; a1 / b2 on plane N, a2 / b1 plane "" | keep split if p.99 governs; plane attribution still mixed |
| M4 | FIXED | LL-CL16 / LL-CL16' in the DB under BR-LOWER-LATERALS (not a north truss panel); sheets p.71 / 73 / 80 / 95 / 97 / 98 | definition plane still "N" (harmless; see NEW N7) |
| M5 | FIXED | main FB11 / stringers ST-N33-1: p.72 / 85 / 93 / 94 / 95, no p.55 | - |
| M6 | FIXED | N-W14-W15: p.69 / 70 / 71 | - |
| M7 | STILL OPEN | N-L10-L11: p.69 / 70 only (joint sheets p.79-81 not linked) | user rule question (unclear) |

## 2. New rows

| id | location | what is wrong | model vs plan (measured) | page / where | sev. | proposed fix | conf. |
|---|---|---|---|---|---|---|---|
| N1 | walks along the wind chords to the nav light platforms W15 / W20 / W19', N + S (6 walks, 13.5 / 8.5 / 3.5 ft) | top + mid rail angles on both sides but NO posts: 24 rail pieces float (the platform and cross walk railings next to them have posts) | 0 posts vs posts @ ~6'-0 like the cross walks (p.106 rail make-up: posts 3x3x5/16) | p.106 plan / Section B | missing | walkways-ptc.csx lines ~476-491 (`nav walk` block): add `Post(...)` along both rails like the cross walks (`Walk()`), or build the walk with `Walk(...)` | high (omission in the code); post spacing medium |
| N2 | same walks -> nav light platforms | walk grating (at the cross walk level) is 23.3 / 21.3 / 20.5 in. BELOW the platform grating at W15 / W20 / W19', with no step, ladder or rail return at the end | walk 140.959 / 141.911 / 141.805 vs platform 142.897 / 143.685 / 143.512 ft | p.106 (walk level not given, flagged "level not given") | unclear | read p.106 Section B / plan for how the walk reaches the platform (step / platform on the wind chord level); otherwise flag the step explicitly | medium |
| N3 | CL walkway W15-W15' (both sides, 14 pieces per side, ~30.7 ft each) | after the W12 decision the single rail angles have no support at all in the model (no posts by design, the 'Hand Rail Conn. to Fl. Beams 6x4x3/8 Ls' and PL 14 x 3/8 not built) - rails hang in the air between floor beams | 0 connections vs 1 connection per floor beam per side (p.106 MATERIAL BETWEEN FLOOR BEAMS) | p.106 | missing (flagged) | add the 6x4x3/8 connection angles at each floor beam (positions drawn only -> dimension-only flag) | medium |
| N4 | walkway at FB11 / FB11' | TRUSS-DESIGN 31 / TODO 6a report "FB bottom now 27.74 in. below (plan 26.5)" as a plan difference. It is not one: under the walkway (n -37 .. 0 ft) the FB11 bottom steps - flange angle bottom 139.396 (|n| > 30 ft, = 26.50 in. below 141.6035, exactly Section A's 2'-2 1/2), 60 ft bottom cover 139.344 (23-30 ft), 46 ft bottom cover 139.292 (< 23 ft, = 27.74) | 26.50 at the flange angles = plan | p.103 Section A | unclear (report text) | reword the batch 6 note (no conflict; Section A measures to the flange angles at the truss end); no model change | high |
| N5 | L11 / L11' shoe angles, tap bolts, rivets (SHOE-*-L11-09 / -10, fasteners) | linked only to p.96; they were modeled from p.80 Section F, Detail X and the p.80 note (1 1/4 tap bolts) | p.96 vs p.80 + p.96 | sheet-links-ptc-delaware-river.json (shoe rule) | wrong (link) | add p.80 to the L11 shoe angle / fastener elements (selector on category connection angle / tap bolt under SHOE-*-L11*) | high |
| N6 | all main span walkway / ladder / platform elements | inherit every sheet of their group: WALKWAYS -> p.103-106, PLATFORMS -> p.104-106; e.g. the catwalk (p.105 only), CL walkway (p.106), pier platforms (p.104), nav walks (p.106) each list 3-4 sheets | 3-4 sheets vs the 1-2 used | sheet-links rules walkways-main / platforms-main | unclear (user rule 2026-10-19: only the sheets used to model the element) | split the rules per run name (catwalk -> p.105, CL walkway / cross walks / nav -> p.106, FB11 walkway + ladders -> p.103 / 104, pier platforms -> p.104, U22 -> p.105) | medium |
| N7 | LL-LM13H-SLQ13 / LL-LM13H'-SLQ13' (double K W33x130 at L13 1/2) | plane "" while the mirror members LL-LM13H-NLQ13 / primes are on plane "N"; the CL member LL-CL16 also still carries plane "N" | plane N / "" / N for members of the same kind | truss-ptc-arch.csx / bracing-ptc.csx (Mem plane argument) | unclear (attribution only; DB grouping by system is right for LL-CL16) | give all CL / cross-plane lateral members the same plane value ("") | medium |

## Not determined
- How the nav light walks reach their platforms (N2) and the walk level along the wind chords - p.106 not re-read at high resolution in this run.
- Post spacing for the nav walks (N1) and the CL walkway rail connection positions (N3) are drawn only.
- No clash run (the batch 6 note lists 188 rope x post rows by design, 6 nav walk x bracing rows <= 0.31 in.); rivets / gussets / bracing connections are the other review's topic.
