# I-80 WB elevation mismatches: investigation (2026-10-10)

Method: copy of `samples/plans/I-80 WB and Ramp G.sbm` in `temp/elev/`; published `sbm.exe` only; no project file edited.
Scripts/data in `temp/elev/`: `resid.csx` (full-precision TOS/TOB at every I-80 bearing, plan tables embedded),
`mat2.csx` (model - plan TOS at EVERY plan table point), `tos_all.json` (all plan top-of-slab values, parsed from the PDF text layer),
`bounds.json` (pier / field-splice point numbers), `gore.csx`, `anchors.csx`, `fs_fix.csx`, `gore_try1.csx`.

## Key finding on method
Top-of-slab tables (design sheets 192-201, PDF 199-208) give every girder line at ~28 points per span. Plan note
(sheet 194, PDF 201): "numbered points are equally spaced along each line and between the field splice and pier locations".
`mat2.csx` reproduces them (equal spacing between bearing / splice distances along each girder). Result:
**Unit 1 closes everywhere (all 5 spans, all 6 girders, |d| <= 0.11", mean ~0).** Profile, cross slope, girder offsets,
haunch, slab and flange thickness (except 2WB bottom flange) are right for Unit 1. Plan values are rounded to 0.01 ft (+-0.06"), so
|d| <= 0.12" with no trend is a match.

## Current residuals (model - plan, inches)
Top of bearing (check-top-of-bearing): U1 2WB +0.2..+0.3 (A-F); U2 8WB -0.3 A-C, -0.1..-0.2 others; 9WB A -0.2; 10WB A -0.2;
U3 11WB B -0.2; 12WB A,B -0.1/-0.2. Everything else within 0.1.
Top of slab at bearings (full precision, `resid.csx`): U1 all within 0.08. U2 6A A +0.21 B +0.16 C +0.11 D +0.08 E -0.02;
8WB A -0.39 B -0.27 F -0.19 G -0.15 H -0.12 I -0.15 J -0.07 K -0.08; 9WB A -0.19; 10WB A -0.15; 11WB F/H/J -0.10..-0.11.
U3 11WB B -0.26 C -0.14; 12WB A -0.15 H -0.11; 13WB A -0.12; E Abut A -0.12. Small bias of about -0.05" on U3 D-J and U2 east spans
(0.004 ft, below tolerance, see U-5).
Chain (TOS - TOB) matches the plan at every bearing except U1 2WB (plan 9.66-9.67 ft, model 9.646).

## Diagnoses

### D1. Unit 1 Pier 2WB top of bearing +0.2..+0.3": CONFIRMED input error (bottom flange thickness)
Plan: sheet 102 girder elevation (PDF 104) at Pier 2WB: top flange PL 2 1/4 x 32, **bottom flange PL 2 1/2 x 34** (raster-checked,
`temp/elev/s102_bf*.png`). Model has 2.25" bottom (Pier 1WB 2 1/4 is correct: plan TOS-TOB 9.65 there).
Top of slab at 2WB already matches (+0.03), so profile/cross slope are fine; the plan's own TOS-TOB = 9.66-9.67 implies bf 2.48".
Change: `samples/plans/field-splices-i80.csx` line 135, `Pier(B, 34, 1.5, 2.25, 1.5, 2, 25, 25, 5)` -> `Pier(B, 34, 1.5, 2.5, ...)`
(the same peak value in `build-i80-wb.csx` Unit 1 plate tuple `P(2)` is superseded by this layout, change for consistency).
Verified on copy (`fs_fix.csx`): 2WB TOB residuals now -0.05..+0.06" (were +0.2..+0.3); TOS-TOB 9.667 = plan. No other bearing changed.

### D2. "Girder A at Pier 6A top of slab -1.0"": CONFIRMED transcription error in the check script
`check-top-of-slab.csx` line 8 has 1021.62 for A at Pier 6A. The plan table (sheet 195, PDF 202, point 1, girder line A) prints **1021.52**
(raster-checked, `temp/elev/u2_p1_clip.png`; 1021.62 is point 2). Correct that value. Model is 1021.538, i.e. +0.21", not -1.0"; there is
no conflict with the north edge (plan edge 1021.47, A 1021.52). Remaining +0.21" is D3.

### D3. Gore (Unit 2 girders A-E, Pier 6A to 7591+50): INPUT ERROR in the gore cross-slope rows, partly verified
Plan (sheet 190, PDF 197, Detail "*"): gore slope changes from 3.4% (6A line) to 3.0% (7591+50) with a transition marked "perpendicular to
Ramp A2", i.e. linearly across the gore. `cross-slope-i80.csx` holds 3.4% to 7591+48.11 then drops to 3.0% by 7591+50 (1.9 ft), which makes a
0.26 ft step in the surface. Evidence: `mat2.csx` points 1-7 of A-E: residual 0 at point 1 and 7, but -0.2" ... -1.96" (A), -1.57 (B), -1.16 (C),
-0.70 (D) at points 2-6, growing with offset. Curb anchor points on sheet 190 (1023.21/1023.17 at 7591+06.7, 1023.62/1023.51 at 7591+50)
match the model to 0.04" (`anchors.csx`).
Test on copy (`gore_try1.csx`): row 7591+48.11 gore break slope = -3.017% (linear 3.4% -> 3.0% between 7591+05 and 7591+50) instead of -3.4%.
Result: points 5-7 close (<= 0.10"), A/B/C points 1-4 improve from -1.5" to +0.5..+0.1" but now **+0.52" A, +0.37 B, +0.24 C, +0.13 D at point 1**
(trend with offset). Not closed: the plan's gore is a plane tied to the Ramp A2 grade (about 1.45-1.5% along A vs I-80 1.28%) and the slope is
measured along skewed lines (plane fit of plan A-E: gradient 3.41%, direction rotated 16 deg from the WB normal; edge elevations on sheet 190:
1021.45 at 6A, 1022.06 at 7591+50 vs model 1021.49 / 1022.02). Probable remaining causes (UNCERTAIN): station/slope reference of the transition
(Ramp A2 station vs WB station), the north slab edge offset (-77.57) vs plan A-to-edge spacing (plan edge-A drop 0.045-0.05 ft implies about 1.3 ft,
model 0.99 ft), and the Ramp A2 profile. Proposal: replace the two gore rows by 3.4% at the 6A line and 3.0% at 7591+50 (linear), then fit
against the sheet-195 points 1-7 of A-E; a true fix may need a gore plane defined from the Ramp A2 PGL (new input).

### D4. Unit 2 girders A-C and Unit 3 girders A, B (north, flared): CAUSE IDENTIFIED, fix needs plan geometry input (hypothesis, not verified numerically)
`mat2.csx` mid-span residuals: U2 span 7WB-8WB A -0.20 mean (C -0.33), 8WB-9WB A -0.41, B -0.64 (max -0.95); U3 span 0 A -0.97 mean (max -1.91),
B -0.52 (-1.00); U3 span 2 A -0.57 (-1.03). Girders D-J and F-K, which are parallel to the CL, are within rounding.
Plan evidence: GIRDER RADIUS DATA, sheets 107/107A (PDF 109/110): U2 A R=1749'-6 then 2324'-5, B 1480'-6, C 8000'-0; sheet 112 (PDF 115, PDF 119
table): U3 A 2268'-5 / 1318'-5 / 2318'-5, B 2796'-10, C 2795'-0, D-J 2324'-5..2393'-5 (parallel). The model builds A-C as offset-linear curves
parallel to the alignment between support offsets (`OffsetsAtSupports`, "spread evenly" where the plans say VARIES in build-i80-wb.csx
`SpreadBetween`), so mid-span position differs by up to ~1-2.7 ft where the cross slope is 4-5.9%, and the offsets at the piers themselves are guesses.
Bearing residuals at 8WB A-C (-0.3") and U3 11WB B (-0.26") are the same cause: slope x offset error. Estimated: 8WB A/B plan offsets about
0.7/0.5 ft nearer the CL than modeled (-43.9 -> about -43.2, -37.3 -> about -36.8), UNCERTAIN, read the plan pier dimensions (sheets 106-108, PDF 108-111)
before changing. Needed inputs: per-girder plan geometry (P.C. stations: U2 7601+39.20 at 75.58' LT, U3 A 7606+59.36 at 89.13' LT of I-80 CL, radii above)
and the plan offsets at each pier. The model has no explicit girder-arc input, only offsets at supports.

### D5. Unit 2 span 7WB-8WB H and spans downstream: small systematic bias
Girder H (offset 0) in span 7WB-8WB is -0.13" mean (-0.2" at points 59-85), U2/U3 D-K about -0.05". Offset 0 is independent of cross slope, so it is the CL
profile in the superelevation transition zone (7595+58 to 7599+53) or the I-80 radial to WB station mapping. Below 0.01 ft; UNEXPLAINED, check
`Wb()` mapping and the right-slope interpolation (CL = PGL - rs x 34 ft) after D3/D4.

## Not investigated / unresolved
- Exact plan offsets of A-E at Unit 2 piers and A-B at Unit 3 piers (D4); gore plane definition (D3); U3 point-13 residual of A (-0.84" at FS22)
  suggests the FS22 location on A may also differ.
- Unit 3 span residuals for B/C at 12WB-13WB were not individually reviewed.
- Haunch is 3.5" everywhere in the model; plan "field haunch data" sheets (PDF 136-157) were not compared. The TOS-TOB chain closes without them.
