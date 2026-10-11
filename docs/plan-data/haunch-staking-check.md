# Field haunch data and staking diagram vs the model (QC checks)

Model checked: copy of `samples/plans/I-80 WB and Ramp G.sbm` (I-80 WB Design 116 Units 1-3, Ramp G Design 316 Units 1-4).
Scripts: `samples/plans/check-haunch.csx`, `samples/plans/check-staking.csx` (`sbm run <script> "<model>.sbm"`; they change nothing and do not save).
Data: `docs/plan-data/haunch/*.csv`, `docs/plan-data/staking/*.csv` (every row carries its PDF page). Tolerance 1/16" (0.0052 ft) / 0.01 ft
unless stated. These are checks, not inputs: no model input was changed. Differences are reported with evidence.

## Summary

| check | compared | within tolerance | beyond | worst |
|---|---|---|---|---|
| (H1) model HAUNCH (finished grade - slab - top of web) vs plan nominal haunch | 4703 points | 4702 | 1 | +0.102" Ramp G U4 girder D pt 50 (STA 67497+89.38) |
| (H2) plan-implied HAUNCH (plan girder line haunch elev. - plan slab defl. - model top of web), tol 0.01 ft | 4697 | 4671 | 26 | +0.147" I-80 U3 A pt 104 (all marginal, = check-blocking's 0.01-0.019 ft set) |
| (H3) cross slope adjustment, per flange edge | 9406 | 9384 | 22 | -0.080" I-80 U2 girder G left edge pts 7-56 |
| (H4) model field haunch within plan ALLOWABLE MIN..MAX | 4703 | 4338 | 365 above MAX | +0.125"/+0.375" over MAX in I-80 U2 / U3 top-flange tension zones |
| (H5) plan consistency: misc-table slab defl. (1/16) vs decimal table (model input) | 4703 | 4690 | 13 | +0.092" Ramp G U2 pt 7 (model INPUT error, see below) |
| (S1) staked CL points (station along the alignment) | 38 | 38 | 0 | 0.000 ft |
| (S2) skews | 37 | 37 | 0 | 0.0000 deg (Pier 6A not comparable) |
| (S3) spans along the CL | 35 | 35 | 0 | 0.003 ft |
| (S4) footings (centre S, N, size S x N) | 69 | 60 | 9 | Pier 11WB & 19G: -10 11/16" along the pier |
| (S5) alignment ties (11WB/19G, 77'-6 7/8) | 3 | 3 | 0 | 0.04" |

**2026-10-11: findings 1-4 DECIDED by the user and APPLIED** (branch dec-i80; docs/HANDOFF.md section 4a-bis): 11WB from the PDF 63 cap plan
(11 5/16" at the ahead bearing row, chained from the right top corner; WB CL 0.937" right of the CL pier), 6A 13'-2 15/16, 6WB 2'-2 3/8, Ramp G camber
CSVs (all midpoint-column shifts, 78 cells). After: S4 footings 67/69 within 1/16"; the 2 beyond are PLAN CONFLICTS reported separately by check-staking
(11WB footing 1: staking 40'-7 vs pier details 40'-6, +1.01"; Ramp G 5G size, finding 5). H5 13 -> 0, H2 26 -> 19. The table above is the state BEFORE.

Model findings that needed a decision (inputs, NOT changed at the time of this check):
1. **Pier 11WB & 19G substructure is 10 11/16" too far left along the pier line** (I-80 Unit 2 support 6; cap profile, columns, footings).
   Staking PDF 16: footing centres 31'-1 15/16 left, 9'-5 1/16 and 30'-11 1/16 right of the staked CL point (I-80 STA 7603+32.45, 41.08' LT).
   Model: 31'-11 5/8 left, 8'-6 3/8 and 30'-0 3/8 right. Pier details PDF 63 (sheet 61, raster-confirmed) give the column spacing 40'-6 / 21'-6
   from the CL pier symmetry line, with CL girder G & CL WB roadway **11 5/16"** right of it on one bearing row and 9 1/2" on the other; the WB CL
   therefore crosses the CL pier about 29/32" right of the symmetry line (consistent with the staking: 9'-6 - 9'-5 1/16 = 15/16").
   `piers-i80.csx` uses "roadway CL 11 5/8" right of the CL pier" (`SFromPlan(b, j, 11.625 / 12)`) - 5/16 misread as 5/8 and applied at the
   CL pier instead of the bearing row. Plan-internal: staking footing 1 to 2 = 40'-7 (31'-1 15/16 + 9'-5 1/16) vs 40'-6 on the pier details (1").
2. **Pier 6A is 5/8" off along its pier line**: staking PDF 15 and Pier 6A details PDF 39 and 41 all print the footing/column centre
   **13'-2 15/16** from CL Ramp A2 (raster-confirmed on PDF 39); `pier-6a.csx` uses 13'-2 5/16 (`FI(13, 2.3125)`). Model - plan = -0.63".
3. **Pier 6WB footings/columns are 3/16" off**: staking PDF 16 and pier details PDF 43 (CL girder H & WB CL 15'-10 3/8 right of the CL pier;
   columns at 27'-4) and PDF 45 (2'-2 3/8) give the third column 2'-2 3/8 left of the WB CL; `piers-i80.csx` uses road = 2.1823 ft (2'-2 3/16).
   All four footings +0.19".
4. **Ramp G Unit 2 point 7 deflections are taken from the wrong column**: `docs/plan-data/camber/RG-unit2-defl_slab_in.csv` point 7 =
   3.13 / 2.95 / 2.78 / 2.61 is the unnumbered "MIDPOINT OF FIELD SECTION" column of the decimal table (PDF 116); point 7 is 3.05 / 2.87 / 2.71 / 2.55.
   The misc data table (PDF 118, raster-confirmed: 3 1/16, 2 7/8, 2 11/16, 2 9/16) agrees with 3.05..2.55. The same column offset should be
   checked in the RG U2 steel deflection and camber tables at point 7 (not verified here). This is why H2 / check-blocking flag Ramp G U2 A pt 7.

Plan-internal inconsistencies (model follows the detail sheets; nothing to change):
5. **Ramp G Pier 5G footing**: staking PDF 14 shows 31'-6 x 22'-0; Pier 5G details PDF 32 (sheet 30) show 29'-0 x 24'-0 with a 30-pile layout
   that needs 24'-0 across. The model has 29'-0 x 24'-0. The staking value equals Pier 14G's footing (31'-6 x 22'-0) - likely a copy error on the staking sheet.
6. **Allowable field haunch MAX in top-flange tension zones** (H4): the nominal geometry itself exceeds the printed MAX. I-80 U2 (piers 9WB/10WB
   regions, girders A-K, points 105-107, 114-117, 127-129, 136-137; 84 points): MAX 2 1/2" but 3 1/2" - 7/8" flange = 2 5/8". I-80 U3 (girders A-J
   at points 13-16, 22-27, 35-39, 47-51, 61-64, 70-73, 83-86, 93-95; 281 points): MAX 2 1/4" but 2 5/8" (7/8" flange) / 2 3/8" (1 1/8" flange).
   The flange plates are the plan's (e.g. Girder B PDF 112: PL 7/8 x 18 between FS 18 and the 25'-0 peak zone at Pier 9WB). OBSERVED rule (not
   printed): with a = the girder's cross slope adjustment, compression zones MIN = a + 1/2, MAX = min(3 1/2, 4 - a); tension zones MIN = max(0, a - 1/2),
   MAX = 3 - a (fits 4,520 of 4,703 points; I-80 U1 165, I-80 U2 17 and Ramp G U2 1 point fit neither). Under that rule the exceedance means the haunch at the high
   flange edge (field haunch + a) is above 3" where the flange is thin. The intent of MAX in tension zones is **UNRESOLVED** (not stated on PDF 136/113).

## 1. FIELD HAUNCH DATA

### What the plan's haunch numbers mean (I-80 PDF 136-157, Ramp G PDF 113-128)
- TYP. SLAB & NOMINAL HAUNCH DETAIL (I-80 sheet 92 / PDF 94; Ramp G sheet 83 / PDF 85, raster): slab 8"; top of slab to top of web 11 1/2"
  (Ramp G Unit 3: 12"); "3 1/2" NOMINAL HAUNCH" (Ramp G "(UNITS 1, 2 & 4)", "4" NOMINAL HAUNCH (UNIT 3)") from the bottom of slab to the top of
  the WEB; "THEORETICALLY THIS IS A CONSTANT DIMENSION ALONG THE GIRDER". This is the user's HAUNCH convention (top of web to bottom of deck).
  Model `Deck.Thickness` 8", `Deck.Haunch` 3 1/2" (Ramp G U3 4") - match.
- FIELD HAUNCH (notes 1-2, PDF 136 / 113) = GIRDER LINE HAUNCH ELEVATION - SURVEYED top of the top flange; i.e. slab underside at pour minus top of
  flange = HAUNCH - top flange thickness. It is not the user's HAUNCH; the check derives it from the model (H4).
- TABLE OF GIRDER LINE HAUNCH ELEVATIONS (ft): already in `docs/plan-data/camber/*-haunch_elev_ft.csv` and checked by `check-blocking.csx`.
  Re-extracted here with page numbers (`haunch/*-girder_line_haunch_elev.csv`, 4,649 cells): identical to the camber CSVs in every cell.
- MISCELLANEOUS DATA TABLE (inches, stacked fractions; `haunch/*-misc_data.csv`, 11,218 values incl. the discontinuous-girder tables of I-80 U2/U3):
  ANTICIPATED DEFLECTION DUE TO SLAB AND BARRIER per girder; CROSS SLOPE ADJUSTMENTS per girder group and flange edge ("(L) and (R) ... correspond
  to the left and right edge of the girder top flange looking upstation", PDF 137); ALLOWABLE FIELD HAUNCH MIN./MAX. per girder group.

### Extraction (how the fractions were read)
The text layer omits repeated values: identical cells are drawn as vector copies with no text item (e.g. PDF 147 girder A point 23 has no text
at all), and the text cache shows several fractions as the same glyph (cp1252 undefined bytes). Every character on these sheets is a stroked path,
so the values were read from the paths: glyph shapes learned from text items whose paths spell them (39 shapes, no two characters within
tolerance of each other), then every path classified and grouped into cells by row and column. The octal code of a fraction (\201..\217) was
checked against the drawn glyph on raster clips for every code / shape combination (40 clips, all agree). Cross-checks: slab deflections
4,703 of 4,703 cells read; vs the decimal tables 4,690 within 1/32" + rounding, the rest are H5 below; haunch elevations identical to the
earlier extraction. Columns of the discontinuous-girder tables are numbered 1..n from the CL pier (mapped to point numbers as check-blocking does).
Reproduce: `python -I tools/haunch_extract.py "dist/SteelBridgeModeler/B00168 - CBIS-ESI-Westbound I-80 - Design 116.pdf" "dist/SteelBridgeModeler/B00169 - CBIS-ESI-Ramp G - Design 316.pdf" docs/plan-data/haunch` (needs PyMuPDF; `tools/vecfont.py` + `tools/plantext.py`; regenerates the 14 CSVs byte-identical).

### Results (`check-haunch.csx`)
- H1 model HAUNCH: 4702 / 4703 within 1/16" (mean 0.000"). Worst: Ramp G U4 D pt 50 (STA 67497+89.38) +0.102" - an isolated point where the plan's
  cross slope adjustment jumps (7/8" at pt 49 to 9/16" at pt 50: superelevation breakpoint); the model web top is interpolated between girder samples
  across the kink. Next: Ramp G U1 D pt 50 -0.059" (splice/plate change), I-80 U2 K pt 99 +0.046".
- H2 plan-implied HAUNCH: 4671 / 4697 within 0.01 ft; 26 beyond (+0.125 to +0.147", I-80 U2 17, U3 7, Ramp G U2 A pt 7 -0.142" (finding 4),
  Ramp G U3 D pt 106 +0.125"). Same marginal set as check-blocking's haunch elevation residuals (docs/plan-data/camber-blocking.md section 9):
  the plan's girder line haunch elevation is 0.01 ft above the model's final web + 3 1/2" + slab deflection there.
- H3 cross slope adjustment: 9384 / 9406 within 1/16". The 22 beyond are all I-80 U2 girder G LEFT edge, points 7-56 (STA 7591+52 to 7595+60):
  plan 1/2" (group "A, F & G(L)", PDF 147/148) vs model 0.42" (G(R) agrees). The plan treats G's left half like the outer girders (steeper slope);
  the model's cross slope break lies further left. Likely cause: the position of a cross slope break relative to girder G's left flange edge
  (note PDF 136: "A cross slope break point may be located above the girder top flange") - the cross-slope input around the Ramp A2 gore.
  At top flange plate changes (610 values) the better of the two widths is used.
- H4 allowable field haunch: inside 4338, below MIN 0, above MAX 365 (finding 6).
- H5 plan consistency: 13 cells beyond 1/32" + 0.005": Ramp G U2 pt 7 (4 girders, finding 4), and 0.04-0.06" at RG U1 C25, U2 B51, B72, D72,
  U3 A35, D83, D106, U4 A55, D55 (fraction rounding differences inside the plan, under 1/16").

## 2. STAKING DIAGRAMS (I-80 PDF 15-17, Ramp G PDF 14-17)

### What the sheets give
- **No northing / easting anywhere** on the staking sheets (or a stated grid datum). Staked points are stations and offsets: I-80 supports
  "STA. x, 41.08' LT." of CL I-80 (= CL westbound approach roadway), Pier 6A "I-80 STA. 7591+08.83, 107.43' LT. = CL Ramp A2 STA. 61591+08.76",
  Pier 11WB & 19G tie "I-80 STA. 7602+94.96, 109.63' LT. = CL Ramp G STA. 67502+97.70", Ramp G supports by Ramp G station. Grid datum (state
  plane / UTM / project) for the staking: **UNRESOLVED** - not stated; no comparison in grid coordinates was made, no transformation was fitted.
  The model's UTM 15N placement (geolocate-cbis.csx) is a rigid move and does not affect any number below.
- Curve data printed: P.C. STA 7597+73.11, R = 2400.00' (CL I-80); Ramp G P.C. 67470+46.89, R = 950', P.T. 67494+89.66, P.C. 67497+34.48, R = 1550'.
  I-80 stations map to the concentric WB CL by the radius ratio; the printed spans "measured along CL westbound approach roadway" confirm it
  (234'-1 1/16 = 196.72 + 38.02 x 2358.92 / 2400).
- Skews (DMS, ◕ = measured to the local tangent), footing sizes and footing positions along each pier line (dimension chains), spans.
- CSVs: `staking/supports.csv` (38 points), `staking/footings.csv` (69 footings), `staking/spans.csv`, `staking/ties.csv`. Text-layer values are
  tagged TEXT; every fraction and every dimension chain was read on raster clips (RASTER / DERIVED).

### Results (`check-staking.csx`)
- S1 points: 38/38 - the model CL pier lines (support line + PierCenterOffset for shared piers) cross the alignments at the staked stations (0.0000 ft).
  (The supports were built from these stations, so this mainly confirms the I-80/WB station mapping and the shared-pier offsets.)
- S2 skews: 37/37 within 0.01 deg (measured to the local tangent at the CL pier, so the radial expansion piers 5G/9G/14G and 11WB come out exact).
  Pier 6A is "90° to the local tangent of Ramp A2" - Ramp A2 is not in the model, not compared.
- S3 spans: 35/35 within 0.01 ft (max 0.003 ft).
- S4 footings: 60/69 within 1/16" in centre and size. Beyond: Pier 11WB & 19G x3 (-9.68", -10.68", -10.68": finding 1), Pier 6A (-0.63": finding 2),
  Pier 6WB x4 (+0.19": finding 3), Ramp G Pier 5G size (finding 5). All Ramp G single footings sit 10'-0 right of CL Ramp G as staked.
  Abutment footings: Ramp G South Abut compared (37'-10 x 6'-0, centre 10'-0 right - matches); I-80 West/East abutment dimension strings (26'-11,
  37'-9, 33'-4 3/8, 50'-0 9/16, 45'-1 1/2 ...) are measured to abutment/wing ends in mixed directions and were not compared (UNRESOLVED meaning).
- S5 ties: I-80 7602+94.96 109.63' LT and Ramp G 67502+97.70 are 0.02" apart in the model; the model CL Pier 11WB line passes 0.04" from the Ramp G
  point and crosses CL Ramp G 77.573 ft from the WB CL (plan 77'-6 7/8 = 77.573); CL Pier 19G (Ramp G U4) and CL Pier 11WB (I-80 U2) are the same
  line within 0.2" of arc. Pier 6A: the Ramp A2 tie point lies on the model CL Pier 6A (0.00") at 66.350 ft normal to the WB CL (plan 66'-4 1/8 = 66.344).

## UNRESOLVED
- Grid datum of the staking diagrams (no coordinates printed) - PDF 15-17 / 14-17.
- Intent of ALLOWABLE FIELD HAUNCH MAX in top-flange tension zones (finding 6) - PDF 136 notes, 149-150, 156-157.
- Direction of the Ramp G Pier 19G "11'-10 1/4" dimension (read as along the pier line; model matches within 0.02") - PDF 17.
- I-80 abutment staking dimension strings (not compared).
