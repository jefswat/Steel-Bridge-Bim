# Piles (batter), cap section step, footing dowels - Ramp G (Design 316) and I-80 WB (Design 116)

STATUS (extraction cut short by coordinator request):
- Item 1 BATTERED PILES: COMPLETE for every pier in both sets (layout pages read on raster).
- Item 2 CAP SECTION STEP: PARTIAL. Only 16G/17G/18G (from prior extraction `ramp-g-piers-16g-18g.md`, not re-read here). All other piers NOT READ.
- Item 3 DOWELS / COUPLERS: PARTIAL. Only bar-list data for 16G/17G/18G copied from the prior extraction. Hook shapes, laps, coupler elevations NOT READ for any pier.

"pNN" = PDF page. S = along the pier line (the cap line drawn horizontal on the pile layout), N = across it. Piles are given
relative to the footing centre. All battered layouts are symmetric about both axes, so the sign of N (up-station or not) does not matter.
Pile positions were read from the dimension strings and checked against pile counts in the notes.

## 1. Battered piles

Notes (verbatim wording, raster-confirmed p20/p21/p50):
- "PILE DIMENSIONS ARE AT BOTTOM OF FOOTING. BATTER PILES 1:4 IN DIRECTION SHOWN. nn - HP14x73 STEEL BEARING PILING ARE REQUIRED."
  The ratio is not defined as H:V in words. 1:4 is read as 1 horizontal : 4 vertical (UNCERTAIN, convention only).
- Pier note (p21, p22 I-80): "ALL BATTERED PILE SHALL BE TRIMMED TO A HORIZONTAL LINE TO AID IN THE PLACEMENT OF REINFORCING."
  So the pile top is cut horizontal. Plan dimensions are the pile positions at the footing bottom (the fixed point).
  Whether contract length is measured along the battered axis is NOT stated (UNCERTAIN). Pile top 1'-0 above footing bottom (16G elevation, prior doc).
- Drawing convention: a battered pile is drawn with a dashed pile outline running OUT beyond the footing edge in the direction the tip moves.
  Plumb piles are drawn as a plain H section. Corner piles are drawn on a diagonal; measured from the vector linework on p20 as exactly 45.0 deg
  in plan. Whether 1:4 applies along the diagonal or per component is UNCERTAIN.
- 11G (p50): batter is 1:6, not 1:4 ("IN ORDER TO AVOID CONFLICTS WITH ADJACENT PIER NO. 4A (NOT PART OF THIS DESIGN), PILES SHALL BE BATTERED 1:6 IN DIRECTION SHOWN").
- Plumb (no batter note): Ramp G 4G(p29), 5G(p32, HP14x117 x30), 6G(p35), 7G(p38), 16G, 17G; I-80 1WB, 2WB, 3WB, 8WB, 9WB, 11WB (footings 26/29 piles), 15WB.
- Rule everywhere: the perimeter piles batter OUTWARD normal to the nearest footing edge. Corner piles (where two edges meet in the pile grid) batter
  diagonally at 45 deg only on Ramp G piers 1G-3G, 8G-15G, 18G. Interior piles are plumb.
  Directions: +S/-S = along the pier line outward; +N/-N = across, outward. Unit vectors: edge pile on S=+x -> (+1,0); on N=+y -> (0,+1); corner -> (+-0.7071, +-0.7071).

### Ramp G (all p numbers are the pile layout page; contract length / min embedment from the following page)
| Pier | p | Footing S x N | Piles | Ratio | S positions (ft) | N rows (ft) | Battered piles | Plumb piles | Contract / min embed |
|---|---|---|---|---|---|---|---|---|---|
| 1G | 20 | 29'-0 x 16'-0 | 24 | 1:4 | +-12.5, +-7.5, +-2.5 | +-6, +-2 | 16: 4 corners (+-12.5,+-6) diagonal; 8 on N=+-6 at S=+-7.5,+-2.5 (out in N); 4 at S=+-12.5,N=+-2 (out in S) | 8: N=+-2, S=+-7.5,+-2.5 | 90 ft / 55 |
| 2G | 23 | 29'-0 x 31'-6 | 32 | 1:4 | +-12.5, +-7.5, +-2.5 | +-13.75, +-8.25, +-2.75 | 20: 4 corners diagonal; 8 on N=+-13.75 at S=+-7.5,+-2.5; 8 at S=+-12.5 for N=+-8.25,+-2.75 | 12: N=+-8.25 S=+-7.5,+-2.5 (8); N=+-2.75 S=+-2.5 (4) | 90 / 75 |
| 3G | 26 | 28'-0 x 18'-0 | 30 | 1:4 | +-12,+-8,+-4,0 | +-7, +-3.5, 0 | 20: 4 corners diag; 10 on N=+-7 at S=+-8,+-4,0; 6 at S=+-12 for N=+-3.5, 0 | 10: N=+-3.5 S=+-8,0 (6); N=0 S=+-8,+-4 (4) | 90 / 60 |
| 8G | 41 | 34'-0 x 25'-0 | 45 | 1:4 | +-15,+-10,+-5,0 | +-10.5,+-7,+-3.5,0 | 24: 4 corners diag; 10 on N=+-10.5 at S=-10..10 step 5; 10 at S=+-15 for N=+-7,+-3.5,0 | 21: N=+-7 S=-10..10 (10); N=+-3.5 S=+-10,+-5 (8); N=0 S=+-10,0 (3) | 90 / 65 |
| 9G | 44 | 31'-6 x 24'-0 | 28 | 1:4 | +-13.75,+-8.25,+-2.75 | +-10,+-5,0 | 18: 4 corners diag; 8 on N=+-10 at S=+-8.25,+-2.75; 6 at S=+-13.75 for N=+-5,0 | 10: N=+-5 S=+-8.25,+-2.75 (8); N=0 S=+-2.75 (2) | 95 / 65 |
| 10G | 47 | 34'-0 x 25'-0 | 45 | 1:4 | same grid as 8G | same as 8G | same as 8G (24) | same as 8G (21) | 90 / 55 |
| 11G | 50 | 42'-6 x 42'-6 | 57 | 1:6 | +-19.25,+-13.75,+-8.25,+-2.75 (and 0 at N=+-5.5) | +-19.25,+-13.75,+-8.25,+-2.75, 0, +-5.5 (S=0 only) | 28: 4 corners diag; 12 on N=+-19.25 at S=+-13.75,+-8.25,+-2.75; 12 at S=+-19.25 for N=+-13.75,+-8.25,+-2.75 | 29: N=+-13.75 S=+-13.75..+-2.75 (12); N=+-8.25 S=+-13.75,+-8.25 (8); N=+-2.75 S=+-13.75 (4); N=0 S=0,+-8.25 (3); N=+-5.5 S=0 (2) | 85 / 45 |
| 12G | 53 | 42'-6 x 42'-6 | 57 | 1:4 | identical to 11G | identical to 11G | identical to 11G (28) | identical (29) | 60 / 50 |
| 13G | 56 | 28'-0 x 26'-6 | 32 | 1:4 | +-12,+-8,+-4,0 | +-11.25,+-6.75,+-2.25 | 22: 4 corners diag; 10 on N=+-11.25 at S=+-8,+-4,0; 8 at S=+-12 for N=+-6.75,+-2.25 | 10: N=+-6.75 S=+-8,0 (6); N=+-2.25 S=+-4 (4) | 60 / 50 |
| 14G | 59 | 31'-6 x 22'-0 | 28 | 1:4 | +-13.75,+-8.25,+-2.75 | +-9,+-4.5,0 | 18: 4 corners diag; 8 on N=+-9 at S=+-8.25,+-2.75; 6 at S=+-13.75 for N=+-4.5,0 | 10: N=+-4.5 S=+-8.25,+-2.75 (8); N=0 S=+-2.75 (2) | 65 / 55 |
| 15G | 62 | 28'-0 x 18'-0 | 30 | 1:4 | identical to 3G | identical to 3G | identical to 3G (20) | identical (10) | 65 / 55 |
| 18G | 73 | 32'-0 x 42'-6 | 34 | 1:4 | +-14, +-7, 0 | +-19.25,+-13.75,+-8.25,+-2.75 | 22: 4 corners diag; 6 on N=+-19.25 at S=+-7,0; 12 at S=+-14 for N=+-13.75,+-8.25,+-2.75 | 12: N=+-13.75 S=+-7,0 (6); N=+-8.25 S=0 (2); N=+-2.75 S=+-7 (4) | 70 / 55 |
| 19G | 76 | 28'-0 x 22'-0 | 20 | 1:4 | +-12,+-6,0 | +-9,+-4.5,0 | 10: ALL of rows N=+-9 (S=+-12,+-6,0), outward in N, NO diagonal corners | 10: N=+-4.5 S=+-12,0 (hmm see note) | 70 / 60 |
Note 19G plumb piles: N=+-4.5 at S=-12, 0, +12 (3 each = 6) and N=0 at S=+-12, +-6 (4); total 10. (the N=+-4.5 S positions read from raster as -12, 0, +12.)
Ramp G contract lengths 16G/17G: 70 ft. Contract length row for 1G..: from the pile-note pages.

### I-80 WB (pile layout page; "other footings identical" on multi-column piers)
| Pier | p | Footing S x N | Piles | Ratio | S positions | N rows | Battered | Plumb | Contract / min embed |
|---|---|---|---|---|---|---|---|---|---|
| 4WB | 33 | 14'-6 x 21'-6 | 24 | 1:4 | +-5.25, +-1.75 | +-8.75, +-5.25, +-1.75 | 8: both outer rows N=+-8.75 (S=+-5.25,+-1.75), outward in N, no diagonals | 16 | 75 / not read |
| 5WB | 37 | 16'-0 x 20'-0 | 16 | 1:4 | outer rows +-6,+-2; middle rows +-6, 0 | +-8, +-4, 0 | 8: N=+-8 rows (S=+-6,+-2) outward in N | 8: N=+-4 S=+-6,0; N=0 S=+-6 | 75 / not read |
| 6A | 41 | 29'-0 x 20'-0 | 24 | 1:4 | +-12.5,+-7.5,+-2.5 | +-8,+-4,0 | 12: N=+-8 rows (6 each), outward N | 12 | 75 / not read |
| 6WB | 45 | 18'-0 x 20'-0 | 21 | 1:4 | +-7,+-3.5,0 | +-8,+-4,0 | 10: N=+-8 rows (5 each) | 11 | 65 / 45 |
| 7WB | 49 | 20'-0 x 29'-0 | 26 | 1:4 | +-8,+-4,0 | +-12.5,+-7.5,+-5(S=0),+-2.5,0 | 10: N=+-12.5 rows (5 each) | 16 | 75 / 60 |
| 10WB | 61 | 26'-0 x 17'-6 | 18 | 1:4 | +-11,+-5.5,0 | +-6.75,+-2.25,0 | 10: N=+-6.75 rows (5 each) | 8 | 85 / 70 |
| 12WB | 69 | 16'-0 x 20'-6 | 16 | 1:4 | +-6,+-2 | +-8.25,+-2.75 | 8: N=+-8.25 rows | 8 | 80 / 50 |
| 13WB | 73 | 24'-0 x 30'-0 | 20 | 1:4 | +-10,+-5,0 | +-13,+-6.5,0 | 10: N=+-13 rows (5 each) | 10 | 65 / 40 |
| 14WB | 77 | 19'-0 x 20'-6 | 16 | 1:4 | +-7.5,+-2.5 | +-8.25,+-2.75 | 8: N=+-8.25 rows | 8 | 85 / 40 |
I-80 contract lengths for plumb piers: 1WB 100 (75), 2WB 90 (75), 3WB 95, 8WB 85 (55), 9WB 80 (55), 11WB 70, 15WB 85 (50).
Mid-row S positions in 12WB/4WB read at low resolution (assumed the same 4-column grid; UNCERTAIN, 16/24 count agrees).
Footing orientation: footings are drawn with the cap line horizontal, so S = footing length along the pier line. On 4WB, 7WB, 13WB and some others the
footing is longer across the pier line than along it.

## 2. Cap section step (narrower lower part)

STATUS: PARTIAL. Only the facts below are known; no raster of detail B or sections A-A/B-B was read in this session.
| Pier | Source | Known | Not read |
|---|---|---|---|
| 16G, 17G | p65 / p69 sections A-A, B-B, p79 | top width 6'-0; lower part 5'-0 (6" inset each side, from prior extraction); depth varies; relief depth steps 3/4" (2 @ 3/4 = 1 1/2) at beak ends; Detail B on p78: "NO CHAMFER", 7 deg 0'0" MIN draft at all setbacks (aesthetics doc) | exact elevation of the step, whether it follows the relief curve, drafted or square, end conditions |
| 18G | p72 | top width 8'-0; relief area bottom 7'-0 wide (prior doc) | step elevation/extent |
| all others (1G-15G, 19G, 1WB-15WB, 6A, 11WB&19G) | | NOT READ | everything |
Aesthetic doc (`pier-cap-aesthetics.md`): texture on one face only, 7 deg min draft at all setbacks, corners not chamfered ("CORNERS SHALL NOT BE CHAMFERED", Detail A).

## 3. Footing dowels / column bar splices

STATUS: PARTIAL, bar-list data only (from `ramp-g-piers-16g-18g.md`; hook shapes, lap lengths, coupler elevations NOT read).
| Pier | Column bars | Dowels | Notes |
|---|---|---|---|
| 16G | 14d1 28 x 25'-0, 14d2 28 x 30'-0 (staggered, per column) | 14d3 28 x 16'-10, 14d4 28 x 11'-10 "FOOTING TO COLUMN DOWEL" | mechanical splices at 16G/17G/18G (p3 notes); coupler bars/elevation NOT read |
| 17G | 14d1 14 x 19'-10, 14d2 14 x 24'-10 (col 1); 14d5 14 x 20'-7, 14d6 14 x 25'-7 (col 2) | 14d3 28 x 16'-10, 14d4 28 x 11'-10 | same |
| 18G | 18d1 23 x 14'-8, 18d2 19 x 19'-8 | 18d3 23 x 24'-0, 18d4 23 x 19'-0 | 18d5 4 x 11'-6 terminates at top of column to avoid cap-bar interference |
| all other piers | NOT READ | NOT READ | |
Dowel difference between d3 and d4 = 5'-0 and d1/d2 = 5'-0 (16G, 17G); 18G d3/d4 = 5'-0, d1/d2 = 5'-0: staggered splices.

## Inputs the modeler needs
1. Piles (FootingDefinition): per-pile batter list `{S, N, DirS, DirN}` (unit vector of the outward tip movement) plus a footing `BatterRatio` (V per H, 4 normally; 6 for 11G). Provide a generator rule
   "perimeter piles outward, corner piles 45 deg diagonal (flag)" so the batter piles above can be matched to existing positions by (S,N). Pile top stays at the footing-bottom plane position; tip offset = depth / ratio.
   Confirm whether contract length is along the axis.
2. Cap section: `LowerWidth`, `StepDepth` (from top or soffit), `StepDraftDegrees` (7 min), extent (full length or between relief control points). Data not yet extracted.
3. Column rebar: `DowelSize`, `DowelLengths` (two alternating), `DowelHook` (bottom hook type, length, orientation), `ProjectionAboveFooting`, `SpliceType` (lap or coupler), `LapLength` or `CouplerElevations`, stagger offset (5'-0 on 16G-18G), and per-bar "terminates at column top" flag (18d5).

## UNCERTAIN
- Ratio 1:4 as H:V; diagonal batter along resultant vs per axis; pile length along battered axis.
- Mid-row S positions of 12WB/4WB; 19G plumb piles read at low resolution.
- Cap step geometry (Item 2) and dowel hooks/laps/coupler elevations (Item 3): not extracted.
