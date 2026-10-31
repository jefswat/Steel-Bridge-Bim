# PTC main span (bridge "PTC Arch truss 18-20") - gussets, rivets, lateral brace connections: RE-CHECK

Re-check of `connections-findings.md` (2026-10-27) on the model rebuilt on 2026-10-28 after the TODO.md batches 1-7. This is a review only: nothing in the .sbm, csx, csv or src/ was changed, and the old findings file was not edited.

## How it was measured

**Regeneration.** `work6/connections/recheck.csx` regenerated the committed `samples/plans/PTC Delaware River Bridge.sbm` once, in memory, with only the main span kept (no save, heavy lock used).

**Model size.** 10,796 parts (one part per piece now), 134,592 bolts and rivets, 849 members, 184 gusset stacks.

**What the script did, in the same run:**
- dumped parts, rivets, members, points, covers, stacks, checks, diagnostics and `TrussModel.DroppedRivets`;
- ran the batch-5 bracing probe (`work5/brace.csx` logic): rivet shank vs part overlap, and every bracing member end;
- ran the batch-1 joint stack and grip probe (`work4/layers.csx` logic);
- ran a main-span clash with bolts on: 448,953 rows, 1,335 of them non-bolt.

**Analysis scripts:** `work6/connections/ana.py`, `rclass.py` and `bb.py`.

**Outputs:** `work6/connections/out/`, `rivet_count_diffs.csv` and `rivets_suspect.csv`.

**Old numbers.** The old values are from `work/out` (2026-10-27) and were analysed the same way.

**Counting rules.**
- Per-face rivet counts are for the N truss and the unprimed half (the model is symmetric) unless a row says otherwise.
- Clash rows are per piece now (PartPerPiece), so the same contact can give more rows than before. Member pairs are compared where it matters.

## Counts

| Status | Rows |
|---|---|
| FIXED | 10 |
| PARTLY | 10 |
| STILL OPEN | 12 |
| NOW WRONG | 0 |
| checked OK, unchanged (R10) | 1 |
| **old rows total** | **33** |
| NEW | 8 |

H01-H04 (layering handoff) are not counted. See the note at the end.

## Top remaining problems (what still makes it "look wrong")

1. **NEW N01 / N02: rivet heads overlap at almost every lower chord joint.**
   - The rivets of the new bracing plates and floor connections overlap the joint gusset rivet heads.
   - 1,806 bolt-to-bolt rows; 1,300 of them are deeper than 1/4 in.
   - Worst at L11, L9, L6, L8, L16, L0 and F8 at L13.
   - Joint-to-joint overlaps improved: 692 -> 552 rows.
2. **R01: 13.8 % of the drawn joint rivets are still not placed (old 14.7 %).**
   - Worst groups are unchanged: L10 U9-L10 51 of 97, L10 L10-U11 56 / 98, L13 U12-L13 58 / 96, L12 U11-L12 66 / 96.
   - 3,233 drops per plane: 1,892 outside the member section, 1,049 with no clean stack, 253 out of range, 39 on a head.
3. **N05: about 800 joint rivets (both trusses) grip only the gusset plate(s).**
   - There is no member, fill or splice plate under them.
   - The grip fix of batch 1 now makes visible the gaps that used to be bridged by air.
4. **Lateral bracing connections still missing:**
   - tongue beam WM22 (B08);
   - U11 portal ends (B09);
   - floor lateral X at F8 / FX14-20 (B10, B11);
   - L13 1/2 double K far ends (B06);
   - sway frame point 12 bottom (B04).
5. **NEW N04: verticals and the hanger overlap the lower chord.**
   - U13-L13 and the hanger L14-W14 now end on a plane parallel to the chord, and both penetrate the kinked lower chord by 0.51 / 0.57 in.
6. **Gusset edge angles (G02) built at 12 joints only. Only 40 of the 144 angle parts carry rivets.**

## Status of the old rows

### G - gussets

**G01 - Web members overlap. Status: PARTLY.**
- Model now: web-to-web overlap is left at 2 joint pairs per truss half, each with N / S and primed copies:
  - U11: post U11-L11 x diagonal L10-U11, 0.563 in., 25 piece rows;
  - L10: U9-L10 x U10-L10, 0.307 in., 6 rows.
- Total 124 piece rows (old: 108 member rows at about 15 joints).
- All other joints are clear (`WebToWebClearance`).

**G02 - Gusset free-edge angles. Status: PARTLY.**
- Model now: 144 edge-angle parts (`EA*`) at U11-U17 and L13-L17.
- Rivets on 40 of them: L13, U11, U12 and L16. The other 104 have no rivet.
- Still not built at U8, U9, L9, L10, L12, U18-U21, L18 and L19, although these are drawn on p.77-89.

**G03 - Hanger L14-W14 wider than the W14 gusset gap. Status: PARTLY.**
- Model now: in-to-in is 2'-5 and the hanger clash is gone.
- Still left:
  - the outline is an envelope (15.4 SF per plate);
  - the thickness is the 3/4 default (flagged);
  - see NEW N06 (gusset not riveted).

**G04 - L11 envelope. Status: STILL OPEN.**
- Model now: still the envelope.
- The stack is now 1 1/8 + 3 x 3/4, at 35.5 / 35.5 / 31.5 / 25.7 SF (was 29.1 SF).
- The outline has not been re-read (TODO 4f: "edges not separable").

**G05 - U11 right edge inside the drawn plate. Status: STILL OPEN.**
- Model now: the reading was kept (TODO 4f: the re-trace put drawn rivets outside the plate).
- U11 cover: 466 rivets per face; unchanged.

**G06 - L10 top edge. Status: FIXED.**
- Model now: re-traced 7 in. lower; 6 vertical rivets moved to 'beyond'.
- The inner stack is 101.7 SF.
- Not re-overlaid in this run.

**G07 - U5 top-left corner. Status: FIXED.**
- Model now: moved 8 in. right; 5 chord rivets moved to 'beyond'.
- Not re-overlaid.

### R - rivets

**R01 - Drawn joint rivets dropped. Status: PARTLY (marginal).**
- Placed per face, inside the gussets: 8,372 of 9,570 drawn. Beyond the gussets: 1,030 of 1,341.
- Total missing 13.8 % (old 14.7 %).
- 105 groups differ by more than 15 % (old 93; the rivet-counts.csv rows were re-split). Worst groups unchanged.
- `DroppedRivets` now gives the reason for every drop.

**R02 - Hanger / wind chord rivets assigned to the diagonals. Status: PARTLY.**
- Hanger L14-W14 at L14: 44 of 64 inside and 21 of 22 beyond (was 0).
- Wind chord L13-W14 at L13: 0 of 11 inside ("no clean plate stack") and 3 of 5 beyond.

**R03 - "unassigned" symbols not placed. Status: STILL OPEN.**
- rivet-counts.csv still has these unassigned symbols: L17 24, L20 19, L16 18, L18 14, L15 12, U11 5, L0 2, L13 2, L10 1.

**R04 - Chord cover splice plates unriveted. Status: PARTLY.**
- 250 of 262 joint splice / fill plates now have rivets.
- Still unriveted:
  - the U5 inside web splice plate `SPU5-7` and fill `FU5-1`;
  - the L0 fill `FL0-1`, for each truss and half.
- Plates without a scaled length are still not built.

**R05 - Rivets closer to the gusset edge than 1 11/16 in. / outside the plate. Status: STILL OPEN.**
- 41 of 46 faces are short: 380 rivets (old 374).
- Outside the plate: U16 -0.05, U17 -0.06, U18 -0.10 in. (unchanged).

**R06 - Vertical ends at L12-L14 too high. Status: FIXED.**
- The ends are now 12.0 / 14.1 / 13.0 in. above the WP (were 29.7-32.8).
- The fix introduced NEW N04.

**R07 - Rivets through one member only, beyond the gussets. Status: STILL OPEN.**
- Verticals: 276 rivets per truss with a 1/2 in. grip (unchanged).
- Diagonals: 348 one-member rivets per truss (unchanged).
- Chords: 2,140 rivets per truss grip pieces of the one chord only. These may be legitimate stitch / cover rivets; the plan has not been checked for them.

**R08 - Rivet heads overlapping. Status: PARTLY.**
- Joint-to-joint rivets: 552 rows, 64 of them deeper than 1/4 in. (old 692 / 172).
- L14 is resolved (was 84 / 52 per truss); L12 26, L10 20 and L7 18 rows are left.
- See NEW N01 / N02: overlaps with other rivet groups are much worse.

**R09 - U11 post right-edge rivet line skipped. Status: STILL OPEN.**
- rivet-count-zones.csv row 14 (SKIP 1510-1523 px) is unchanged.

**R10 - Diameters. Status: OK.**
- No rule or data change (rivet-rules-p70.csv untouched). Not re-measured.

### B - bracing connections

**B01 - Lateral / strut plates float off the laterals. Status: FIXED.**
- Bracing rivets that grip only their own plate or angle: 144 (all on floor lateral FX angles, see B10). Old: 16,197 of 20,838.
- 23,438 rivets connect a plate to a member.

**B02 - Bracing edge angles connect nothing. Status: FIXED.**
- 516 of 524 edge angles have a connecting rivet.
- The 8 without one are the sway apex angles at L11 / L11' (`BGSW11-SW-B/T-EA`).
- New clashes from these angles are in N07.

**B03 - Rivet counts per end vs p.71. Status: FIXED.**
- Per end in the model:
  - upper laterals: all 88 ends have 26;
  - struts: 40 (92 ends), 46 (16), 50 (4), 16 (4);
  - lower laterals: 26 / 32 / 36 / 38 / 40 / 28 per p.71.
- check-truss-ptc reports 15 PASS rows.

**B04 - Sway apex connections missing. Status: PARTLY.**
- Apex plates are now built at every point 0-12.
- At point 12, the lower ends of sway members SW-N12 / SW-S12 (and the primed copies) have 0 connecting rivets, and plates `BGLM12-SW-B/T` have no rivet to a member.

**B05 - Sway top connections missing. Status: FIXED.**
- 52 top plate pairs on the floor beam bottoms (p.85). Sway ends without a rivet: 4 of 104 (point 12, B04).

**B06 - L13 1/2 double K / strut missing. Status: PARTLY.**
- L13 1/2 is built at 13'-1 along the chord.
- Still left:
  - ends without a connecting rivet:
    - B (at LQ13) of LL-NL13H-LQ13 / LL-SL13H-LQ13;
    - B of LL-LM13H-NLQ13 / LL-LM13H-SLQ13;
    - 2 rivets only at the L13 1/2 ends;
  - the apex gussets on F8 are not built (flagged).

**B07 - LM0 apex plate. Status: FIXED.**
- The LM0 plates are built.
- The LS-L0 strut shows 0 bracing-plate rivets. It connects through its p.73 end angles (`EC`), which this probe does not count.

**B08 - Tongue beam WM22 K connection. Status: STILL OPEN.**
- B ends of FL-K21-22N / S (and the primed copies): 0 rivets.
- No tongue beam (flag).

**B09 - Portal U11 connections. Status: STILL OPEN.**
- The portal box was rebuilt (CustomPieces), but all 4 portal ends have 0 rivets.
- The knee pieces clash the box by 0.28 in. (N07).

**B10 - Floor lateral connections W15-W21 / FX14-20. Status: STILL OPEN.**
- 144 FX angle rivets grip only their own angle.
- Plates `BGFX14/16/18/20-1/-2` have no rivet to a member.
- The 'a1' / 'b1' X halves have 1 rivet at end A.
- The FX plates sit 0.31 in. into the floor beam (6 rows).
- The web slot, fillers and floor beam angles are not built.

**B11 - L13 lateral on F8. Status: PARTLY.**
- The LM13 apex plates are 3/4 per p.95.
- The `BGL13H` plates "stop 1/4 clear of FL-X13-15a1 / b1". End A of FL-X13-15b1 (and FL-X13'-15'a1) has 0 rivets.

**B12 - L11 box strut lateral plates. Status: STILL OPEN.**
- `BGL11-a` "stops 1/4 clear of LS-L11 (crosses the plate, not connected)".
- LS-L11 has 2 rivets per end.

### C - plan-vs-plan

**C01 - Wind chord vs suspended floor beam about 0.3 in. Status: STILL OPEN (reported conflict).**
- Kept as printed. 12 rows of chord x floor beam at 0.28 in.
- This is the correct handling; it waits for a user decision.

**C02 - p.71 counts vs p.97 / p.98 details. Status: FIXED.**
- The diagram governs; check-truss-ptc has a CONFLICT row.

### S - sheet links

**S01 - W14 gussets on p.69 / p.70. Status: FIXED.**
- Now linked to p.84 + p.85 (`sbm db --sheets-for N-GW14-L`).

**S02 - L13-L17 gussets also on p.69. Status: STILL OPEN.**
- Kept on purpose (TODO 7a: p.69 was used for the L14 inlaid layer). This is a user decision.

## NEW rows

### N01 - Bracing rivets overlap the joint gusset rivet heads

- **Location:** every lower-chord joint with lateral plates. Per truss half: L11 68 rows, L9 20, L6 18, L8 18, L16 18, L0 17, ...; both trusses and halves.
- **What is wrong:** the bracing plates are now carried to the gusset stack (`PlateToStack`). Their edge-angle and plate rivets are laid on their own 2 / 4-line pattern without looking at the joint rivets already there, so rivet heads overlap.
- **Model vs plan:**
  - model: 1,602 bolt-to-bolt rows `BR-* x TRUSS-*`, 1,128 of them deeper than 1/4 in. (old: 40 rows);
  - plan: drawn rivet spacing is at least 3 in.; the bracing edge-angle rivets go through the same gusset holes or lie between the joint rivets (p.97 L1 detail).
- **Sheet:** p.97 DETAILS SHOWN FOR L1; p.98; p.80 (L11).
- **Severity:** wrong. **Confidence:** high.
- **Proposed fix (generator):** run the bracing edge-angle / plate rivets through the same `MergeRivet` / head-clear rule as the joint rivets. A bracing rivet within 0.8 (d1 + d2) of a joint rivet should either become that rivet (one hole) or be dropped and counted. Report the count per point.

### N02 - Floor beam F8 / floor connection rivets overlap the L13 joint rivets

- **Location:** FB13 = F8 at L13 / L13', both trusses.
- **What is wrong:** same as N01, for the FLOOR rivet groups at L13.
- **Model vs plan:** 204 rows `FLOOR x TRUSS`, 172 of them deeper than 1/4 in. (old: 7). F8 at L13 alone has 58 rows per truss.
- **Sheet:** p.95 Sections B-E; p.83.
- **Severity:** wrong. **Confidence:** high.
- **Proposed fix (generator):** as N01, for the end-connection and stiffener rivets (`TrussEndConnection` rivets placed after the joint rivets).

### N03 - Joint rivet heads inside other steel

- **Location (per face, N):** L13 20 (heads inside the F8 angles `FB13-A2 / A4 / EC1A`), L11 16 (inside the strut end connection `LS-L11-EC1A / EC1AF`, 11 1/2 in. deep), L1 12, L3 12, L2 11, L4 5, U16 / U17 2-3.
- **What is wrong:** the head of a drawn joint rivet sits inside an end-connection angle laid on the gusset face. The rivet should go through that angle, or the angle should take the hole.
- **Model vs plan:** 346 buried heads over all faces (after batch 1: 222, then under the bracing edge angles; those are gone, these are new).
- **Sheet:** p.95 (F8), p.80 (L11 strut ends), p.85 (framed floor beam ends at L1-L4; not verified per joint).
- **Severity:** wrong. **Confidence:** medium (L13 / L11 high).
- **Proposed fix (generator):** extend the joint rivet grip to the end-connection angle that covers it (`JointRivetGripsFromSteel` stops at the gusset face; let the head side probe outward too), or merge with the connection's own rivet.

### N04 - Vertical U13-L13 and hanger L14-W14 overlap the lower chord

- **Location:** L13 (U13-L13 x L13-L14), L14 (L14-W14 x L13-L14 and x L14-L15); N / S and primed copies.
- **What is wrong:** the batch 4c `TrussEndCut.ParallelToChord` cut uses the mean chord face of the kinked chord. At these joints the end penetrates the steeper chord piece.
- **Model vs plan:**
  - model: U13-L13 into L13-L14 0.51 in. (19 piece rows per truss half); hanger 0.57 in. into L13-L14 and 0.17 in. into L14-L15;
  - plan: the members end clear of the chord (p.83 / p.84 dashed ends).
- **Sheet:** p.83, p.84.
- **Severity:** wrong. **Confidence:** high.
- **Proposed fix (generator):** for `ParallelToChord` on a kinked chord, cut on BOTH chord faces (the deeper of the two planes at each corner) plus `WebMemberClearance`, rather than on the mean face.

### N05 - Joint rivets that grip only the gusset plate(s)

- **Location (per truss half):** L13 26 (13 of them through edge angle `EAL13-R2` = OK), U12 24, L10 18 (through both layers GL10 + GL10-2 only), U9 16, L8 14, U11 14, U13 12, L15 12, L18 10, U14 10, U7 8, U15 8, U16 8, L17 8, L6 6, L16 6, U17 6, L12 2, L14 2.
- **What is wrong:** batch 1a (grip from the contiguous steel) made visible the gusset-to-member gaps that used to be bridged by air. These rivets have no member, fill, inlay or splice plate under the gusset.
- **Model vs plan:**
  - model: about 840 rivets over both trusses (420 per truss), 1 1/8 / 1 1/4 grip = the gusset alone;
  - overall air over the joint faces: 7,301 -> 2,554 after batch 3. The rest is at L21 56 / face, U21 51 and U5 31 (gaps up to 1.14 / 0.63 in.).
- **Sheet:** joint sheets p.75-90 (fills / inlays / web splice plates callouts without extents; splices.csv rows with no length).
- **Severity:** missing. **Confidence:** medium.
- **Proposed fix (data):**
  - per joint, list which callout lies under these rivets (fill, inlay, splice), using `work6/connections/rivets_suspect.csv`, which has the positions;
  - build the plates whose extent can be scaled, and flag the others;
  - at L21 / U21 the 34 x 1 vs 1 1/8 CONFLICT (TODO 3b) is the likely cause.

### N06 - W14 gussets: the outer plate of each truss carries no rivet

- **Location:** W14 / W14': N-GW14-L, S-GW14-R and the primed copies.
- **What is wrong:** each W14 gusset pair is an envelope 3/4 plate (15.4 SF).
  - The inner plate has only the 16 floor-beam connection rivets (1 1/4) through it.
  - The outer plate has none.
  - There are no gusset rivets for the hanger L14-W14 or the wind chords L13-W14 / W14-W15 at W14, and rivet-counts.csv has no W14 joint rows.
- **Model vs plan:** model 0 / 16 rivets per plate; plan: p.85 bottom-left "DETAILS FOR FLOOR BEAM AND RIGID HANGER AT PANEL PT W14" draws the joint with rivets.
- **Sheet:** p.85, p.84.
- **Severity:** missing. **Confidence:** high (model), medium (plan detail).
- **Proposed fix (data):** read the W14 joint on p.85 (outline, thickness if printed, rivet symbols) into gusset-outlines.csv / rivet-counts.csv, as for the other joints.

### N07 - Clashes introduced by the batch 5 bracing pieces

- **Location:**
  - sway apex edge angle x lateral plate at LM points (`BGLM#-SW-B-EA` / `-SW-T-EA` x `BGLM#-T`): 0.41 in., 10 rows;
  - L13 1/2 edge angle x its fill (`BGL13H-*-EA` x `-F1`): 0.21 in., 8 rows;
  - portal knee pieces (`PK-CVA / SPA / LAT-M`) x the new portal box: 0.28 in., 6 rows;
  - sway / lateral plates x CL walkway posts / stringers / gratings (`BGLM#-SW`, `BGUM#-T`, ...): 0.19-0.57 in., about 40 rows. This group is left to the walkway review.
- **What is wrong:** new pieces were placed without clearing against each other.
- **Sheet:** p.97, p.98, p.100.
- **Severity:** wrong. **Confidence:** high.
- **Proposed fix (generator):** apex edge angle `Gap` against the lateral plate it meets; trim the knee pieces to the portal box (they were seated on the old envelope).

### N08 - Chord-to-chord overlaps at the L11 mitre and at the wind chord L13-W14

- **Location:** L11 (L10-L11 x L11-L12) and L13 (L13-W14 x L13-L14), both trusses and halves.
- **What is wrong:** the inner plies overlap at the L11 mitre, and the wind chord runs into the lower chord at L13. This existed before (1 member-pair row each in the old clash) but was not a row in the old list.
- **Model vs plan:**
  - model: 0.50 in., 21 piece rows per half at L11; 0.53 in., 10 rows at L13;
  - plan: p.80 / p.83 draw them clear.
- **Sheet:** p.80, p.83.
- **Severity:** wrong. **Confidence:** high.
- **Proposed fix (generator):** at L11, mitre the inner plies after `InlaidAt`. At L13, give the wind chord end a cut clear of the chord.

## Layering handoff rows (not counted)

- **H01 (L11):** now 4 layers (1 1/8 + 3 x 3/4 at 26 3/4 in-to-in, TODO 3a). The outline is still the envelope (G04).
- **H02 (L10):** 3/4 + 1/2 at 101.7 / 40.5 SF.
- **H03 / H04:** unchanged here.
- The dedicated layering re-check (recheck-gussets) covers these rows.

## Not determined in this run

- **Overlays.** The re-traced outlines (G05-G07) were not re-overlaid on the sheets. They are judged from the data changes and the TODO notes.
- **R07 chord rivets.** Whether the 2,140 chord one-member rivets per truss are legitimate stitch / cover rivets.
- **N03 at L1-L4.** The exact steel the buried heads sit in (L13 and L11 identified).
- **Plan reads.** The p.85 W14 joint, p.93 WM22, p.99 FX and p.100 portal connections were not read in this run (still open from before).
