# Superelevation transition residual, I-80 WB Unit 2 spans 7WB-8WB / 8WB-9WB (2026-10-10)

Method: copies in `temp/xslope/` (`m.sbm` = copy of `samples/plans/I-80 WB and Ramp G.sbm`, `cross-slope-i80.csx` copy, variants `cs_v1/v2/v12.csx`,
`edge_strip.csx`), published `sbm.exe` only, no build, nothing in `samples/plans` or the model edited. Control: re-running the unmodified script on a copy
reproduces the baseline exactly. Evidence files: `temp/xslope/base.csv` (baseline per point), `o_<variant>.csv`, `tob_<variant>.txt`, `analytic.py`.

## 1. Plans vs input
- Sheet 190 (PDF 197): right transition -2.0% at 7591+48.11 -> +2.0% at 7595+58.11 (410'-0), then 2.5% at 7596+04.61 (46.50'), 3.0% at 7596+55.86
  (51.25'), 5.9% at 7599+53.11 (297.25'); legend "SUPERELEVATION TRANSITION (MEASURED ALONG I-80)". Linear in station per segment.
  `cross-slope-i80.csx` encodes the same stations/values, EXCEPT (a) the right-slope row is at 7591+50.00, not 7591+48.11 (1.89 ft late);
  (b) rows are linear in WB station, but the plan is linear in I-80 station: between 7596+55.86 and 7599+53.11 the P.C. at 7597+73.11 compresses WB
  stations (x0.98288), so the model slope at the P.C. is 4.156% vs plan-linear 4.144% (CL 0.004 ft low there).
- Profile (sheet 12, PDF 14): VPI 7570+00 EL 996.08, VPI 7596+25.00 EL 1029.61 VC 750', VPI 7621+00 EL 1016.10 (G1 +1.278%, G2 -0.546%) = model.
  Chord error of the 25-ft VPI table <= 0.002 ft. PGL 34'-0 right of WB CL (typical section PDF 94/95: 34'-0 + 7'-1). No mismatch found.

## 2. Decomposition of the residual (model - plan, baseline)
Per bin of 6 points, fit res = a + br*(offset right) + bl*|offset left|, all 10 girders (`a` = girder H / CL):
6WB-7WB: a = +0.002..+0.004, br = -2e-4; 7WB-8WB..9WB: a = -0.0096..-0.0116 (7595+76 to 7598+50), br = +1.5e-4..+2.6e-4, bl ~ -1e-4..+0.5e-4; from 7599+50: a -0.004, br ~ 0.
So it is NOT a profile offset (H and the PGL line 34' right are 0.011 apart; K at +34.5 ft is only -0.003) and not pivot elevation: it is a slope-vs-station
effect of about 1.8e-4 (0.018%) in the right slope, sign flipping at Pier 7WB, vanishing at 7599+53.
- 6WB-7WB (+ side): explained by the 7591+48.11 start (rs low by 0.04*1.89/410 = 1.85e-4 -> CL +0.0063). CONFIRMED by plan text.
- 7WB-8WB, 8WB-9WB (- side): only ~0.004-0.005 explained by the I-80/WB station mapping at the P.C.; the rest (~ -0.006..-0.008 ft CL) is UNRESOLVED.
  The plan's own three labelled slopes (2.0/2.5/3.0/5.9%) and stations are all reproduced, so the plan text on PDF 197 does not contain the missing piece
  (candidates not readable from the text layer: rounding of the printed 2.5%/3.0% labels, transition not exactly linear). No value was estimated.

## 3. Tested corrections (copy; script rerun so profile re-derives; top of slab, all Unit 2 girder points, model - plan, ft)
| span | baseline mean / worst / >0.01 | V1 start 7591+48.11 | V2 row at P.C. (I-80-linear) | V1+V2 |
|---|---|---|---|---|
| 6WB-7WB | +0.0013 / -0.009 / 0 | -0.0001 / -0.010 / 0 | +0.0013 | -0.0001 |
| 7WB-8WB | -0.0100 / -0.021 / 126 | same | -0.0084 / -0.017 / 84 | -0.0084 / -0.017 / 84 |
| 8WB-9WB | -0.0073 / -0.020 / 50 | same | -0.0056 / -0.012 / 12 | -0.0056 / -0.012 / 12 |
| 9WB-10WB | -0.0038 | same | same | same |
Girder H mean 7WB-8WB: -0.0110 -> -0.0095; 8WB-9WB: -0.0068 -> -0.0056. Top of bearing (in): 7WB unchanged (-0.1..0.0); 8WB -0.2/-0.1 -> -0.1/0.0 (all <= -0.1);
9WB unchanged (-0.1..0.0). Remaining ~ -0.0095 ft (0.11") at 8WB is UNRESOLVED.

## 4. Proposed input change (both plan-supported, apply together)
In `samples/plans/cross-slope-i80.csx`, before the `foreach (var (v, p) in al.Profile.Vpis.Zip(pgl))` line (see `temp/xslope/cs_v12.csx`):
1. Row at `Wb("7591+48.11")` with right slope -0.02 and left sides from `SidesAt` of that station; set the existing `7591+50.00` row's right slope to
   -0.02 + 0.04*(Wb("7591+50.00")-Wb("7591+48.11"))/(Wb("7595+58.11")-Wb("7591+48.11")) = -0.019816.
2. Row at `Wb("7597+73.11")` (P.C.): left -rs, right +rs, rs = 0.03 + 0.029*(pc80 - Sta("7596+55.86"))/(Sta("7599+53.11") - Sta("7596+55.86")) = 4.1442%.
Result: 7WB-8WB worst -0.021 -> -0.017, 8WB-9WB -0.020 -> -0.012; the rest of the residual stays UNRESOLVED (PDF 197, 14).

## 5. Edge vs gutter (deck under the barrier) - plans settle it
- Typical sections PDF 94 (sheet 92, Unit 1) and PDF 95/96 (sheets 93-94, Unit 2/3): the 1'-7 strip outside the gutter line is labelled "LEVEL"; note
  "WHERE THE DECK SLOPES DOWNWARD TOWARDS THE BARRIER RAIL, THE DECK UNDER THE BARRIER RAIL SHALL BE PLACED LEVEL AND THE BARRIER RAIL SHALL BE PLACED
  VERTICAL. WHERE THE DECK SLOPES DOWNWARD AWAY FROM THE BARRIER RAIL, THE DECK UNDER THE BARRIER RAIL SHALL BE PLACED ALONG THE SAME CROSS SLOPE AS THE
  DECK" (PDF 94/95/96). Ramp G (Design 316) typical PDF 85 has the same note and shows 6'-0 / 26'-0 gutter dimensions (1'-7 strips beyond, 32'-0 roadway).
  Switch criterion = sign of the slope at that side (low side: level; high side: continues); no station is stated, it follows from the slope sign.
- Test on copy (`edge_strip.csx`): per side, break at the gutter offset with outer slope max(slope,0), plus a row at each sign change of the outer slope.
  I-80 right (36.0'): EDGE_R mean -0.017 -> -0.003, worst 0.048 -> 0.008, >0.01: 6 -> 0. Ramp G left (6.0'): EDGE_L worst 0.099 -> 0.006, >0.01: 6 -> 0.
  Ramp G right (26.0'): EDGE_R mean -0.066 -> -0.002, >0.01: 18 -> 1 (Pier 19G, -0.042, deck-end width, unrelated). Girders/gutters unchanged.
  Zero-crossing rows: I-80 right 7593+54.06; Ramp G 67469+30.89 and 67496+14.73.
- I-80 NORTH edge (EDGE_L, mean -0.065, all 18 flagged) is always the low side but the edge offset varies (U1 -25.583, U2 -37.6.., U3 -43.6.., deck-edges-i80.csx),
  and CrossSlopeRow breaks are constant-distance per row, so rows cannot express it. Proposed small Core option, OFF by default, per bridge deck:
  `DeckDefinition.LevelStripLowSide` (bool) + `LevelStripWidth` (1'-7): the finished-grade query beyond (edge - width) on a side whose slope at the gutter point is
  negative (falls toward the edge) returns the gutter elevation; where the slope is >= 0 the plane continues. Set in this project's scripts only. Not implemented.
