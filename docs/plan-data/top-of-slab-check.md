# Top-of-slab check vs plan tables (I-80 WB Design 116, Ramp G Design 316), 2026-10-10

Run: `sbm.exe run samples\plans\check-top-of-slab-all.csx "samples\plans\I-80 WB and Ramp G.sbm"` (read-only; `TOS_VERBOSE=1` prints every point,
`TOS_OUT=file.csv` writes every point). Data: `samples\plans\top-of-slab-i80-rampg.csv` (P rows = plan values, B rows = pier / field-splice point numbers),
parsed from the PDF text layer (I-80 PDF 199-203, 206-207; Ramp G PDF 142, 144, 146, 148). The parse of the I-80 girder lines reproduces the earlier
`temp/elev/tos_all.json` exactly (3089 values). Residual = model - plan, ft; plan precision 0.01 ft, so flags are |d| > 0.01 ft.

## Method
Plan note (sheet 194, PDF 201): numbered points are equally spaced along each line between field splice and pier locations.
- Girder lines: point i placed along the framing girder path between consecutive bearing / splice distances (U2 A-E begin at 6A); model = `Alignment.ElevationAt(station, offset)`
  at that point (the surface the deck follows).
- Edge / gutter / CL rows: only the points at piers and abutments are located (intersection of the skewed bearing line with the offset line: model deck edge,
  gutter = edge inboard 1'-7 per the sheet 194 diagram, CL = offset 0). U1 and Ramp G CL rows are separate lines; for U2 (H) and U3 (G) the CL row is the girder row.
- UNRESOLVED (no guessing, excluded from all statistics): 3496 plan points = every edge / gutter / CL(U1, Ramp G) point between supports (the plan does not define
  where those lines cross the field-splice lines), and the four edge/gutter values at I-80 U2 point 1 (north on the 6A line, south on 5WB; plan does not say).
  PDF pages: I-80 199, 200, 202, 203, 206, 207; Ramp G 142, 144, 146, 148. Dash cells ("-", discontinuous girders B-E U2, B-C U3) have no plan value and are not counted.

## Result
Checked 4842 points: I-80 3167 (3089 girder-line + 78 at supports), Ramp G 1675 (1560 + 115). Overall mean -0.003 ft, min -0.101, max +0.021; 265 points beyond 0.01 ft.
| group | n | mean | min | max | beyond 0.01 |
|---|---|---|---|---|---|
| I-80 girder lines | 3089 | -0.0027 | -0.021 | +0.015 | 205 |
| I-80 gutter + CL at supports | 42 | -0.002 | -0.013 | +0.021 | 4 |
| I-80 edge at supports | 36 | -0.041 | -0.101 | 0.000 | 25 |
| Ramp G girder lines | 1560 | -0.0020 | -0.019 | +0.007 | 6 |
| Ramp G gutter + CL at supports | 69 | -0.002 | -0.034 | +0.006 | 1 |
| Ramp G edge at supports | 46 | -0.041 | -0.100 | +0.006 | 24 |

Unit 1 of I-80 closes on every girder (|d| <= 0.009). Ramp G units 2-4 close (<= 0.010) except one point. All of I-80 U2 spans 6WB-7WB, 9WB-10WB, 10WB-11WB and U3 spans 11WB-12WB, 12WB-13WB are within 0.010
(so the 11WB being corrected by another agent is not visible in these spans at the moment; recheck after that change). Small uniform bias about -0.004 ft (-0.05") on most U2/U3 and Ramp G U3-4 lines is below tolerance.

## Clusters beyond 0.01 ft and likely causes
1. EDGE rows at supports (49 of 82 points, -0.03 .. -0.10): gutter rows on the same lines match (|d| <= 0.009 except below), girders match. Pattern: on every DOWNHILL edge (surface falls outward:
   Ramp G right edge U2, U3, part of U4; Ramp G left edge U4 end; I-80 north edge U2/U3; both edges U1) the plan edge elevation equals the gutter elevation (+-0.02, a flat strip),
   whereas the model continues the cross-slope plane (about slope x 1'-7 lower; residual = -0.09 at 5.9%). On uphill edges (Ramp G left U2/U3, d <= 0.006) the plan follows the plane.
   Cause: plan convention at the slab edge outside the gutter line (not a profile / cross-slope error). Open question for the engineer: is the slab top outside the gutter flat (barrier toe) in the plans? PDF 142/144/146/148/199-203/206-207.
2. I-80 U2 span 7WB-8WB, all girders A-J (133 of 270 flagged, points 57-84, stations 7595+93 to 7598+02): mean -0.011, worst -0.021; girder H (offset 0, independent of cross slope) is -0.011 too and rises
   from -0.003 at 7WB; trend slightly larger on the north side (A..C -0.012, I..J -0.008, K ~ -0.004). Same continuing in 8WB-9WB (56 flagged, A/B/F/G mean -0.007..-0.009, points 85-104, PDF 203).
   Station range = the right-slope superelevation transition (7595+58 to 7599+53). Likely input: `cross-slope-i80.csx` right-slope interpolation / CL = PGL - rs x 34 ft in the transition (rs about 0.0004 too large), already noted as D5 in i80-elevation-investigation.md. PDF 202, 203.
3. I-80 U3 girder A (offset -40.5, flared north girder), spans 13WB-14WB, 14WB-15WB, 15WB-East Abut (14 points, -0.011 .. -0.014, trend toward the east abutment: -0.014 at point 107). Other U3 girders (D-J) and
   G (offset 0) are within -0.009. Likely girder A offset / plan-geometry (about 0.2 ft toward the CL at 5.9% slope; see D4 in the investigation) rather than profile. PDF 206, 207.
4. I-80 U2 girder A points 3-4 (+0.012, +0.015, PDF 202): gore plane near Pier 6A (remaining residual of the Ramp A2 gore, D3 resolved note, was +0.18" at A pt 4).
5. Ramp G Unit 1 span 2G-3G (points 44-47, PDF 142): girders D (-0.019, -0.017, -0.011) and C (-0.012, -0.011) beyond 0.01, A and B within 0.007; sta 6747043-6747071, offset 15-25 ft right, inside the
   superelevation transition (rows 67468+74.69 -> 67470+96.69, build-ramp-g.csx lines 88-89). Likely linear-transition shape / full-super station (slope too high by about 0.08%). Ramp G U4 girder C point 47 (-0.010) is on the tolerance.
6. Gutter rows beyond 0.01 (4 + 1): I-80 U3 GUT_L at 11WB (+0.021, and EDGE_L -0.071; north deck edge variable width near the 11WB support line, PDF 206), U3 12WB (+0.011), U2 8WB (-0.013), I-80 U1 CL at 5WB (-0.011),
   Ramp G U4 GUT_R / EDGE_R at the end (Pier 19G, -0.034 / -0.042, PDF 148, deck-end width). Single points; deck-edge offsets at those support lines are the probable inputs (11WB lines are being corrected separately).

## Notes
- The ABUT/PIER boundary at the last point of I-80 U3 parsed at 106 and was forced to 107 (last point of the unit) in the CSV; all other boundary points match `temp/elev/bounds.json` and the PDF header text.
- The model's edge offsets and the 1'-7 gutter strip are taken from the model deck / diagram, not independently measured; edge/gutter residuals therefore include any error in those inputs.
- Per-point output of this run: `temp\tos\resid.csv`, `temp\tos\out.txt`; parse scripts: `temp\tos\parse_all.py`, `temp\tos\mkcsv.py`.
