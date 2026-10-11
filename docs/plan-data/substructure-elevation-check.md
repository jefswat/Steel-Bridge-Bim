# Substructure elevation / height cross-check (plan sheets vs as-built model)

Generated 2026-10-10 by `samples\plans\check-substructure-elevations.csx` (re-runnable, does not edit or save the model) from `samples\plans\substructure-elevations-i80-rampg.csv` (every plan value with its PDF page).

- Model: `samples\plans\I-80 WB and Ramp G.sbm` (copy in `temp\subelev\model.sbm`, file time 2026-10-10 20:36, after the camber and deck-edge merges); run with `src\SBM.Cli\bin\Debug\net10.0\sbm.exe run samples\plans\check-substructure-elevations.csx <copy>`; `SUBELEV_OUT=<file>` writes every comparison as CSV. An earlier copy (20:03, before the merges) gave the same substructure results; only the derived pedestal seats on 6WB / 8WB and the Ramp G backwall-top check moved by <= 0.017 ft.
- Plans: Design 116 (B00168, WB I-80) and Design 316 (B00169, Ramp G); pier sheets = first (elevation) sheet of each pier, pile contract lengths from the pier-notes sheet, abutments from the abutment sheets. Decimal elevations were taken from the PDF text layer (each one was verified to exist on the cited page); every ft-in value with a fraction was read on 130-700 dpi clips (fractions are inch fractions: `9'-0 3/16` = 9.0156 ft). "PDF n" = PDF page.
- Model values are read at the same position as the plan item: corners / kinks from `SubstructureUnit.CapOutline` (S, Z), soffit at a column from `Columns[k].TopZ`, footing from `Foundations` (`TopZ`/`BottomZ`), pile head/length from `PileInfo`, pedestals from `Pedestals` (sorted by S, back/ahead rows split at `CapMidN`).
- Tolerance: a difference is flagged when it is larger than 0.01 ft (0.12 in; the plan elevations are rounded to 0.01 ft). Plan identities (closure) are flagged above 0.015 ft.

## 1. Result

- 887 plan values compared on 35 piers (I-80 WB 1WB-15WB incl. 6A and 11WB; Ramp G 1G-19G) plus 39 values on the 3 abutments; 43 more printed abutment / pier values (wings, backwall extensions, Ramp A2 pedestals, seal coat) have no model equivalent.
- **No transcription error was found in the pier elevation inputs.** Every cap-top corner, flare elevation, footing bottom and thickness, footing top, pile length and pile-head dimension of the 35 piers matches the plan to 0.00-0.01 ft (see the per-pier tables), except the items below.
- 70 flagged pier differences (> 0.01 ft): 54 are the cap-depth convention (section 2.1, not an input typo), 13 are derived pedestal seats / heights (section 2.3), 3 are real (section 2.2) or plan misprints (section 2.4).

## 2. Differences > 0.01 ft

### 2.1 Cap depth "7'-0" / "9'-0 (TYP.)" is perpendicular to the sloped top; the model offsets the soffit vertically (convention, <= 0.02 ft)

The plan draws the cap depth and beak dimensions on slanted dimension lines (perpendicular to the top line). The model offsets the soffit by the nominal value measured **vertically**, so on steep caps (slope 4-6 %) the soffit is D (1/cos - 1) = 0.008-0.012 ft too high, and the column heights that follow from it (Dim A / Dim B tables, soffit elevations at the columns) differ by 0.011-0.018 ft. Re-reading the plan dimension as perpendicular brings every one of these items to <= 0.009 ft (last two columns, computed by the script).

| Pier | Item | Plan | PDF | Model | Diff | Model, perpendicular reading | Diff |
|---|---|---|---|---|---|---|---|
| Pier 8WB | Column height, soffit to top of footing 1 | 25.000 | 51 | 25.011 | +0.0106 | 25.0076 | +0.0076 |
| Pier 8WB | Column height, soffit to flare 1 | 13.000 | 51 | 13.011 | +0.0106 | 13.0076 | +0.0076 |
| Pier 8WB | Soffit elev. at column 1 | 1007.330 | 51 | 1007.341 | +0.0106 | 1007.3376 | +0.0076 |
| Pier 10WB | Column height, soffit to top of footing 1 | 26.000 | 59 | 26.014 | +0.0136 | 26.0062 | +0.0062 |
| Pier 10WB | Column height, soffit to flare 1 | 14.000 | 59 | 14.014 | +0.0136 | 14.0062 | +0.0062 |
| Pier 10WB | Soffit elev. at column 1 | 1005.260 | 59 | 1005.274 | +0.0136 | 1005.2662 | +0.0062 |
| Pier 11WB | Column height, soffit to top of footing 1 | 27.000 | 63 | 27.012 | +0.0123 | 27.0036 | +0.0036 |
| Pier 11WB | Column height, soffit to top of footing 2 | 29.010 | 63 | 29.028 | +0.0173 | 29.0191 | +0.0087 |
| Pier 11WB | Column height, soffit to top of footing 3 | 30.083 | 63 | 30.098 | +0.0144 | 30.0890 | +0.0057 |
| Pier 11WB | Column height, soffit to flare 1 | 14.000 | 63 | 14.012 | +0.0123 | 14.0036 | +0.0036 |
| Pier 11WB | Column height, soffit to flare 2 | 16.010 | 63 | 16.028 | +0.0173 | 16.0191 | +0.0087 |
| Pier 11WB | Column height, soffit to flare 3 | 17.083 | 63 | 17.098 | +0.0144 | 17.0890 | +0.0057 |
| Pier 11WB | Soffit elev. at column 1 | 1004.510 | 63 | 1004.522 | +0.0123 | 1004.5136 | +0.0036 |
| Pier 11WB | Soffit elev. at column 2 | 1006.520 | 63 | 1006.538 | +0.0178 | 1006.5291 | +0.0091 |
| Pier 11WB | Soffit elev. at column 3 | 1007.590 | 63 | 1007.608 | +0.0177 | 1007.5990 | +0.0090 |
| Pier 12WB | Column height, soffit to top of footing 3 | 30.000 | 67 | 30.011 | +0.0109 | 30.0015 | +0.0015 |
| Pier 12WB | Column height, soffit to top of footing 4 | 31.500 | 67 | 31.513 | +0.0127 | 31.5033 | +0.0033 |
| Pier 12WB | Column height, soffit to flare 3 | 16.000 | 67 | 16.011 | +0.0109 | 16.0015 | +0.0015 |
| Pier 12WB | Column height, soffit to flare 4 | 17.500 | 67 | 17.513 | +0.0127 | 17.5033 | +0.0033 |
| Pier 12WB | Soffit elev. at column 3 | 1007.170 | 67 | 1007.181 | +0.0109 | 1007.1715 | +0.0015 |
| Pier 12WB | Soffit elev. at column 4 | 1008.670 | 67 | 1008.683 | +0.0127 | 1008.6733 | +0.0033 |
| Pier 15WB | Column height, soffit to top of footing 2 | 20.630 | 79 | 20.645 | +0.0148 | 20.6335 | +0.0032 |
| Pier 15WB | Column height, soffit to top of footing 3 | 22.271 | 79 | 22.282 | +0.0112 | 22.2705 | -0.0004 |
| Pier 15WB | Soffit elev. at column 2 | 1003.730 | 79 | 1003.745 | +0.0150 | 1003.7335 | +0.0035 |
| Pier 15WB | Soffit elev. at column 3 | 1005.370 | 79 | 1005.382 | +0.0120 | 1005.3705 | +0.0005 |
| Pier 3G | Cap depth at left kink (dim) | 9.000 | 25 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 3G | Cap depth at right kink (dim) | 9.000 | 25 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 4G | Cap depth at left kink (dim) | 9.000 | 28 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 4G | Cap depth at right kink (dim) | 9.000 | 28 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 5G | Cap depth at left kink (dim) | 9.000 | 31 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 5G | Cap depth at right kink (dim) | 9.000 | 31 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 6G | Cap depth at left kink (dim) | 9.000 | 34 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 6G | Cap depth at right kink (dim) | 9.000 | 34 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 7G | Cap depth at left kink (dim) | 9.000 | 37 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 7G | Cap depth at right kink (dim) | 9.000 | 37 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 8G | Cap depth at left kink (dim) | 9.000 | 40 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 8G | Cap depth at right kink (dim) | 9.000 | 40 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 9G | Cap depth at left kink (dim) | 9.000 | 43 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 9G | Cap depth at right kink (dim) | 9.000 | 43 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 10G | Cap depth at left kink (dim) | 9.000 | 46 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 10G | Cap depth at right kink (dim) | 9.000 | 46 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 11G | Cap depth at left kink (dim) | 9.000 | 49 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 11G | Cap depth at right kink (dim) | 9.000 | 49 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 12G | Cap depth at left kink (dim) | 9.000 | 52 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 12G | Cap depth at right kink (dim) | 9.000 | 52 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 13G | Cap depth at left kink (dim) | 9.000 | 55 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 13G | Cap depth at right kink (dim) | 9.000 | 55 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 14G | Cap depth at left kink (dim) | 9.000 | 58 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 14G | Cap depth at right kink (dim) | 9.000 | 58 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 15G | Cap depth at left kink (dim) | 9.000 | 61 | 9.012 | +0.0120 | 9.0033 | +0.0033 |
| Pier 18G | Cap depth at left kink (dim) | 9.000 | 72 | 9.020 | +0.0197 | 9.0040 | +0.0040 |
| Pier 18G | Cap depth at right kink (dim) | 9.000 | 72 | 9.020 | +0.0203 | 9.0046 | +0.0046 |
| Pier 19G | Column height, soffit to top of footing 1 | 24.000 | 75 | 24.014 | +0.0138 | 24.0031 | +0.0031 |
| Pier 19G | Soffit elev. at right kink | 1001.890 | 75 | 1001.902 | +0.0119 | 1001.8912 | +0.0012 |

Likely source (not a typo; one convention): `samples\plans\piers-i80.csx` lines 60-61 (Pier 11WB soffit kinks `Top(+-35.5) - 7.0`) and 190-191 (piers 1WB-15WB `Top(kR) - 7`, `Top(kL) - 7`); `samples\plans\piers-ramp-g.csx` line 63 (Pier 19G `Hammerhead(..., 5.5, -5.5, 5.5, 9.0, 1.20)`: kink depth 9.0 vertical, so the kink soffit comes out 1001.366 / 1001.902 vs printed 1001.36 / 1001.89) and line 214 (16G / 17G kink depth 7.0, effect <= 0.002 ft). Piers 1G-15G and 18G use the printed soffit elevations directly, so only their *depth* check differs (9.020 vs the vertical string 9'-0 3/16 = 9.016 is fine). Using `D / cos(atan(top slope))` as the vertical offset (or the printed soffit elevations / Dim A tables) removes all of them.

### 2.2 Real differences in the pier inputs

| Pier | Item | Plan | PDF | Model | Diff | Likely wrong input |
|---|---|---|---|---|---|---|
| Pier 6A | Cap top, right corner | 1013.280 | 39 | 1013.260 | -0.0200 | `pier-6a.csx` line 75 `step = 1011.50 - 1010.75` is applied to both ends (`Prof(step)`, lines 86-87). The plan prints 1011.50 / 1010.75 at the left end (step 0.75) but 1013.28 / 1012.51 at the right end (step 0.77): the two halves have slightly different slopes (3.42 % and 3.38 %). Fix: separate left / right lifts (0.75 and 0.77). |
| Pier 19G | Top to soffit at column (dim) | 9.010 | 75 | 9.000 | -0.0104 | Same convention as 2.1: `piers-ramp-g.csx` line 63 uses a vertical 9.0 below the top at the kinks and the column; the plan vertical string 9'-0 1/8 (9.0104) is the perpendicular 9'-0 on a 4.9 % slope. |

Borderline (exactly 0.010 ft, not flagged): Pier 19G ahead-half right corner, plan 1012.77 vs model 1012.76 (`piers-ramp-g.csx` line 63 uses a constant step 1.20; the plan steps are 1.20 left and 1.21 right).

### 2.3 Pedestal seats and heights (derived values, not substructure inputs)

The seat elevations come from the superstructure chain (grade - slab - haunch - web - flange - sole plate - bearing, see `check-top-of-bearing.csx`), not from a pier input; the cap top and the pedestal height follow. Differences are <= 0.02 ft. The plan prints one set of seat elevations on the shared piers 5WB, 5G and 14G, which carry a back and an ahead bearing row in the model; the ahead row differs from the printed value by up to 0.02 ft.

| Pier | Item | Plan | PDF | Model | Diff |
|---|---|---|---|---|---|
| Pier 5WB | Pedestal top (*ELEV.) 1 (row A) | 1012.9300 ft | 35 | 1012.9130 | -0.0170 ft |
| Pier 5WB | Pedestal top (*ELEV.) 2 (row A) | 1013.2200 ft | 35 | 1013.2078 | -0.0122 ft |
| Pier 5WB | Pedestal height (printed) 1 (row A) | 7.3750 in | 35 | 7.1156 | -0.2594 in |
| Pier 5WB | Pedestal height (printed) 2 (row A) | 10.8125 in | 35 | 10.6534 | -0.1591 in |
| Pier 5WB | Pedestal height (printed) 4 (row B) | 10.7500 in | 35 | 10.5958 | -0.1542 in |
| Pier 6WB | Pedestal top (*ELEV.) 4 | 1014.3800 ft | 43 | 1014.3697 | -0.0103 ft |
| Pier 8WB | Pedestal top (*ELEV.) 6 | 1016.2800 ft | 51 | 1016.2693 | -0.0107 ft |
| Pier 14WB | Pedestal top (*ELEV.) 1 | 1010.1500 ft | 75 | 1010.1389 | -0.0111 ft |
| Pier 5G | Pedestal top (*ELEV.) 4 (row A) | 1028.7000 ft | 31 | 1028.7101 | +0.0101 ft |
| Pier 14G | Pedestal top (*ELEV.) 1 (row A) | 1028.2500 ft | 58 | 1028.2695 | +0.0195 ft |
| Pier 16G | Pedestal height (printed) 1 | 5.8125 in | 64 | 5.6586 | -0.1539 in |
| Pier 16G | Pedestal height (printed) 2 | 4.3750 in | 64 | 4.2308 | -0.1442 in |
| Pier 19G | Pedestal top (*ELEV.) 2 (row A) | 1012.2400 ft | 75 | 1012.2293 | -0.0107 ft |

Cross-check with the bearing elevation chains (2026-10-11, `samples\plans\check-substructure-chains.csx`): all 244 bearing
chains (both bridges) now run seat → pedestal → as-designed cap → column → footing → pile head and match the generated
units (0 mismatches; before: only 216 chains reached the cap, 18 as-designed abutment bearings mismatched because the old
chain ignored the beam steps). Chain values vs this plan data: 779 compared on 35 piers, 13 flagged = exactly the 13 seat /
pedestal-height rows of the table above; cap top under the seat (226), column top (35), flare (48), footing top / bottom
(68 each) and pile head (68) all within 0.01 ft. The chains also REPORT 6 seats that sit lower than their shared pedestal
block (block top = highest carried seat): Pier 5WB G6 (3/16"), Pier 5G Ramp G Unit 1 G2/G3/G4 and Unit 2 G1 (1/8"-3/16"),
Pier 14G Unit 3 G1 (5/16"; the printed 1028.25 is within 0.004 ft of the lower seat 1028.246; the block top / higher seat is 1028.270). Reported only.

### 2.4 Plan misprint (the plan does not close)

| Pier | Item | Plan | PDF | Reconciled value | Note |
|---|---|---|---|---|---|
| Pier 5G | Overall vertical dimension | 60'-0 3/16 (60.0156) | 31 | 61'-0 3/16 (model 61.0150) | The segments printed on the same line (9'-0 3/16 + 29'-0 + 16'-0 + 7'-0) add to 61'-0 3/16, and the elevations (top 1030.08 / 1028.01, footing bottom 968.03) give 61.015 ft. The plan's overall string is one foot short; the model agrees with the segments and the elevations. |

### 2.5 Abutments (model = parametric abutment; the as-designed abutment feature is still being built)

The abutment support lines are still the parametric default (cap = footing + step zone, level or sloped through the seats, 3 piles per wing foundation), so most differences below are expected until the as-designed abutment lands. The pile contract lengths (East 115 ft and South 125 ft vs the 125 / 100 ft entered) and the East footing elevation (1003.0 entered vs 1003.44 printed at the low end) are plain input values in the build scripts and are worth fixing now.

| Abutment | Item | Plan | PDF | Model | Diff | Note / source |
|---|---|---|---|---|---|---|
| West Abut | Top of backwall at CL bearing | 1008.1500 | 86 | 1008.1027 | -0.0473 | backwall top follows finished grade at the back face; plan ref. elevation is at the CL bearing (crown line) |
| West Abut | Bottom of footing 1 | 992.8200 | 85 | 992.3887 | -0.4313 | model cap bottom follows the seats (parallel to the top); plan footing bottom is level (West, South) or sloped 5.82 % (East). `Abut(992.82)` / `Abut(1003.0)` store the plan footing BOTTOM in `FoundationTopElevation` (`build-i80-wb.csx` lines 104, 176, 328) and the generator ignores it for abutments; East plan low end is 1003.44, not 1003.0 |
| West Abut | Bottom of footing 2 | 992.8200 | 85 | 992.6490 | -0.1710 | model cap bottom follows the seats (parallel to the top); plan footing bottom is level (West, South) or sloped 5.82 % (East). `Abut(992.82)` / `Abut(1003.0)` store the plan footing BOTTOM in `FoundationTopElevation` (`build-i80-wb.csx` lines 104, 176, 328) and the generator ignores it for abutments; East plan low end is 1003.44, not 1003.0 |
| West Abut | Footing depth | 4.0000 | 86 | 4.5000 | +0.5000 | `CapDepth` 4.5 ft (`build-i80-wb.csx` line 101) / 3.0 ft default (Ramp G); plan footing 4'-0 |
| West Abut | Top of pile above footing bottom | 2.0000 | 86 | 1.0000 | -1.0000 | default `PileEmbedment` = 1'-0 (`SubstructureDefinition.cs` line 369); plan: pile top 2'-0 above the bottom of footing |
| West Abut | Footing bottom to wash / seat base | 4.0833 | 86 | 4.5000 | +0.4167 | plan 4'-1 to the wash surface (seat base) |
| East Abut | Top of backwall at CL bearing | 1018.7100 | 89 | 1018.6815 | -0.0285 | backwall top follows finished grade at the back face; plan ref. elevation is at the CL bearing (crown line) |
| East Abut | Bottom of footing 1 | 1003.4400 | 89 | 1003.2944 | -0.1456 | model cap bottom follows the seats (parallel to the top); plan footing bottom is level (West, South) or sloped 5.82 % (East). `Abut(992.82)` / `Abut(1003.0)` store the plan footing BOTTOM in `FoundationTopElevation` (`build-i80-wb.csx` lines 104, 176, 328) and the generator ignores it for abutments; East plan low end is 1003.44, not 1003.0 |
| East Abut | Bottom of footing 2 | 1008.2700 | 89 | 1007.9741 | -0.2959 | model cap bottom follows the seats (parallel to the top); plan footing bottom is level (West, South) or sloped 5.82 % (East). `Abut(992.82)` / `Abut(1003.0)` store the plan footing BOTTOM in `FoundationTopElevation` (`build-i80-wb.csx` lines 104, 176, 328) and the generator ignores it for abutments; East plan low end is 1003.44, not 1003.0 |
| East Abut | Footing depth | 4.0000 | 89 | 4.5000 | +0.5000 | `CapDepth` 4.5 ft (`build-i80-wb.csx` line 101) / 3.0 ft default (Ramp G); plan footing 4'-0 |
| East Abut | Top of pile above footing bottom | 2.0000 | 89 | 1.0000 | -1.0000 | default `PileEmbedment` = 1'-0 (`SubstructureDefinition.cs` line 369); plan: pile top 2'-0 above the bottom of footing |
| East Abut | Pile contract length | 115.0000 | 90 | 125.0000 | +10.0000 | input: `build-i80-wb.csx` line 103 (`Abut`: `PileLength = Ft(125)`, shared by West and East) / `build-ramp-g.csx` line 235 (`Ft(100)`) |
| East Abut | Footing bottom to wash / seat base | 4.0833 | 89 | 4.5000 | +0.4167 | plan 4'-1 to the wash surface (seat base) |
| South Abut | Top of backwall at CL bearing | 1004.2200 | 80 | 1004.1413 | -0.0787 | backwall top follows finished grade at the back face; plan ref. elevation is at the CL bearing (crown line) |
| South Abut | Bottom of footing 1 | 991.1800 | 80 | 992.2134 | +1.0334 | model cap bottom follows the seats (parallel to the top); plan footing bottom is level (West, South) or sloped 5.82 % (East). `Abut(992.82)` / `Abut(1003.0)` store the plan footing BOTTOM in `FoundationTopElevation` (`build-i80-wb.csx` lines 104, 176, 328) and the generator ignores it for abutments; East plan low end is 1003.44, not 1003.0 |
| South Abut | Bottom of footing 2 | 991.1800 | 80 | 992.8934 | +1.7134 | model cap bottom follows the seats (parallel to the top); plan footing bottom is level (West, South) or sloped 5.82 % (East). `Abut(992.82)` / `Abut(1003.0)` store the plan footing BOTTOM in `FoundationTopElevation` (`build-i80-wb.csx` lines 104, 176, 328) and the generator ignores it for abutments; East plan low end is 1003.44, not 1003.0 |
| South Abut | Footing depth | 4.0000 | 80 | 3.0000 | -1.0000 | `CapDepth` 4.5 ft (`build-i80-wb.csx` line 101) / 3.0 ft default (Ramp G); plan footing 4'-0 |
| South Abut | Top of pile above footing bottom | 2.0000 | 80 | 1.0000 | -1.0000 | default `PileEmbedment` = 1'-0 (`SubstructureDefinition.cs` line 369); plan: pile top 2'-0 above the bottom of footing |
| South Abut | Pile contract length | 125.0000 | 81 | 100.0000 | -25.0000 | input: `build-i80-wb.csx` line 103 (`Abut`: `PileLength = Ft(125)`, shared by West and East) / `build-ramp-g.csx` line 235 (`Ft(100)`) |
| South Abut | Footing bottom to wash / seat base | 4.0833 | 80 | 3.0000 | -1.0833 | plan 4'-1 to the wash surface (seat base) |

Abutment beam-step tops match (<= 0.01 ft): East Abut, South Abut, West Abut.

## 3. Dimension strings

299 plan identities were evaluated (flare elevation - flare height = footing top; soffit elevation - Dim A = footing top; soffit - Dim B = flare elevation; Dim A - Dim B = flare height; corner-to-tip + tip-to-soffit = cap depth; cap depth + column height (+ flare) + footing thickness = overall; top elevation at the column - footing bottom = overall; printed pedestal height = *ELEV. - level cap top). 2 do not close by more than 0.015 ft: Pier 5G cap depth + column height (+ flare) + footing thickness = overall (61.01562 vs 60.01562); Pier 5G top elevation at the column - footing bottom = overall (61.01500 vs 60.01562). The model's own strings close by construction (cap depth + column + footing = top - footing bottom, checked for every column).

Closer to the limit (0.008-0.015 ft, plan rounding): Pier 2WB pedestal 2: printed *ELEV. - cap top = printed height (-0.0131); Pier 2WB pedestal 4: printed *ELEV. - cap top = printed height (-0.0140).

## 4. UNRESOLVED / not modeled

- Concrete seal coat under the 6WB footings, 3'-0 thick if used (PDF 45, Excavate and dewater notes): not modeled; the elevation of the seal-coat bottom is not printed (UNRESOLVED, depends on water elevation 975 at construction).
- Pier 6A: the 5 upper-row pedestals (1012.00, 1012.40, 1012.80, 1013.20, 1013.60, PDF 39) belong to the Ramp A2 bridge, which is not in the model. Pedestal heights are not printed on 6A.
- Pedestal heights are printed only on 1WB-5WB and 16G (read and compared). On 6WB-15WB, 11WB, 17G, 18G, 19G and 1G-15G the plan prints seat elevations only; the heights follow from them. Pier 9G back row: seat 1044.12 vs cap top 1042.72 = 1.40 ft (16.8 in) pedestals (model 16.8-17.2 in); the plan shows no height, so this cannot be confirmed (the plan elevations match).
- Beak tip elevations and pile tip elevations are not printed on any pier sheet (only the dimensions, compared above, and the contract length); minimum pile embedment ("IN NO CASE ... LESS THAN n FEET") and the 1" clear / pile embedment of the pile heads into the footing are not modeled except the 1'-0 head-above-bottom (compared, matches).
- Ground line elevations are not printed on the pier sheets (the plinth detail on the aesthetic sheets only says 1'-0 min below proposed ground line); `Foundations.GroundZ` comes from the terrain.
- Abutment backwall top: the model value is read at S = 0 (the support line's crossing of the approach CL, which the staking diagrams give as the CL abutment bearing station); the plan's "ELEV. ... @ CL abut. brg." is the top of the upper backwall at the crown line, but how the model's backwall-top rule (finished grade at the back face) relates to it is UNRESOLVED (differences 0.03-0.08 ft, PDF 86 / 89 / 80).
- Cap depth convention on the I-80 end faces: the 4'-6 / 2'-6 / 7'-0 strings are drawn vertical on the end face and were compared as vertical (difference 0.000); if they are perpendicular the beak items would move by <= 0.008 ft, below the tolerance, so the two readings cannot be told apart from the plan.
- Wing walls, counterforts, wing footings, backwall extensions and maskwalls of the abutments (printed elevations listed in the abutment table) have no model equivalent; the as-designed abutment will need them.
- Aesthetic band depths (relief curve, sheets PDF 83-84 / 78-79) and the cap section step are geometry, not elevations, and were not part of this check.

## 5. Per-pier tables

Columns: plan value, PDF page, model value, difference (model - plan); ft unless noted; `**` = difference > 0.01 ft (0.12 in). Pile / footing rows compare every footing and show the worst one.

### Pier 1WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 999.3300 | 19 | 999.3300 | +0.0000 |  |
| Cap top, right corner | 999.3300 | 19 | 999.3300 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 19 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 19 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 19 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 19 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 19 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 19 | 7.0000 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 18.0000 | 19 | 18.0000 | +0.0000 |  |
| Column height, soffit to top of footing 2 | 18.0000 | 19 | 18.0000 | +0.0000 |  |
| Column height, soffit to top of footing 3 | 18.0000 | 19 | 18.0000 | +0.0000 |  |
| Bottom of footing | 968.8300 | 19 | 968.8300 | +0.0000 |  |
| Footing thickness | 5.5000 | 19 | 5.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 30.5000 | 19 | 30.5000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 19 | 1.0000 | +0.0000 |  |
| Pile contract length | 100.0000 | 22 | 100.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 999.6600 | 19 | 999.6609 | +0.0009 |  |
| Pedestal top (*ELEV.) 2 | 1000.0400 | 19 | 1000.0377 | -0.0023 |  |
| Pedestal top (*ELEV.) 3 | 1000.4000 | 19 | 1000.4027 | +0.0027 |  |
| Pedestal top (*ELEV.) 4 | 1000.3200 | 19 | 1000.3228 | +0.0028 |  |
| Pedestal top (*ELEV.) 5 | 1000.1600 | 19 | 1000.1617 | +0.0016 |  |
| Pedestal top (*ELEV.) 6 | 1000.0000 | 19 | 999.9984 | -0.0016 |  |
| Pedestal height (printed) 1 | 4.000 in | 19 | 3.971 in | -0.0286 in |  |
| Pedestal height (printed) 2 | 8.500 in | 19 | 8.492 in | -0.0079 in |  |
| Pedestal height (printed) 3 | 12.812 in | 19 | 12.873 in | +0.0604 in |  |
| Pedestal height (printed) 4 | 11.875 in | 19 | 11.914 in | +0.0386 in |  |
| Pedestal height (printed) 5 | 9.938 in | 19 | 9.980 in | +0.0423 in |  |
| Pedestal height (printed) 6 | 8.062 in | 19 | 8.021 in | -0.0420 in |  |

### Pier 2WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1002.8800 | 23 | 1002.8800 | -0.0000 |  |
| Cap top, right corner | 1002.8800 | 23 | 1002.8800 | -0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 23 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 23 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 23 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 23 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 23 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 23 | 7.0000 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 22.0000 | 23 | 22.0000 | -0.0000 |  |
| Column height, soffit to top of footing 2 | 22.0000 | 23 | 22.0000 | -0.0000 |  |
| Column height, soffit to top of footing 3 | 22.0000 | 23 | 22.0000 | -0.0000 |  |
| Bottom of footing | 968.3800 | 23 | 968.3800 | +0.0000 |  |
| Footing thickness | 5.5000 | 23 | 5.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 34.5000 | 23 | 34.5000 | -0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 23 | 1.0000 | +0.0000 |  |
| Pile contract length | 90.0000 | 26 | 90.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1003.2100 | 23 | 1003.2061 | -0.0040 |  |
| Pedestal top (*ELEV.) 2 | 1003.5700 | 23 | 1003.5752 | +0.0052 |  |
| Pedestal top (*ELEV.) 3 | 1003.9300 | 23 | 1003.9327 | +0.0027 |  |
| Pedestal top (*ELEV.) 4 | 1003.8400 | 23 | 1003.8452 | +0.0052 |  |
| Pedestal top (*ELEV.) 5 | 1003.6800 | 23 | 1003.6765 | -0.0035 |  |
| Pedestal top (*ELEV.) 6 | 1003.5100 | 23 | 1003.5056 | -0.0044 |  |
| Pedestal height (printed) 1 | 4.000 in | 23 | 3.913 in | -0.0874 in |  |
| Pedestal height (printed) 2 | 8.438 in | 23 | 8.342 in | -0.0950 in |  |
| Pedestal height (printed) 3 | 12.625 in | 23 | 12.632 in | +0.0074 in |  |
| Pedestal height (printed) 4 | 11.688 in | 23 | 11.582 in | -0.1052 in |  |
| Pedestal height (printed) 5 | 9.625 in | 23 | 9.558 in | -0.0673 in |  |
| Pedestal height (printed) 6 | 7.625 in | 23 | 7.507 in | -0.1176 in |  |

### Pier 3WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1006.3000 | 27 | 1006.3000 | +0.0000 |  |
| Cap top, right corner | 1006.3000 | 27 | 1006.3000 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 27 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 27 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 27 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 27 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 27 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 27 | 7.0000 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 25.0000 | 27 | 25.0000 | +0.0000 |  |
| Column height, soffit to top of footing 2 | 25.0000 | 27 | 25.0000 | +0.0000 |  |
| Column height, soffit to top of footing 3 | 25.0000 | 27 | 25.0000 | +0.0000 |  |
| Bottom of footing | 969.3000 | 27 | 969.3000 | -0.0000 |  |
| Footing thickness | 5.0000 | 27 | 5.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 37.0000 | 27 | 37.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 27 | 1.0000 | +0.0000 |  |
| Pile contract length | 95.0000 | 30 | 95.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1006.6300 | 27 | 1006.6327 | +0.0027 |  |
| Pedestal top (*ELEV.) 2 | 1007.0000 | 27 | 1007.0018 | +0.0018 |  |
| Pedestal top (*ELEV.) 3 | 1007.3600 | 27 | 1007.3593 | -0.0007 |  |
| Pedestal top (*ELEV.) 4 | 1007.2700 | 27 | 1007.2718 | +0.0018 |  |
| Pedestal top (*ELEV.) 5 | 1007.1000 | 27 | 1007.1031 | +0.0031 |  |
| Pedestal top (*ELEV.) 6 | 1006.9300 | 27 | 1006.9322 | +0.0022 |  |
| Pedestal height (printed) 1 | 4.000 in | 27 | 3.992 in | -0.0082 in |  |
| Pedestal height (printed) 2 | 8.438 in | 27 | 8.422 in | -0.0158 in |  |
| Pedestal height (printed) 3 | 12.750 in | 27 | 12.712 in | -0.0384 in |  |
| Pedestal height (printed) 4 | 11.688 in | 27 | 11.662 in | -0.0260 in |  |
| Pedestal height (printed) 5 | 9.625 in | 27 | 9.637 in | +0.0118 in |  |
| Pedestal height (printed) 6 | 7.625 in | 27 | 7.587 in | -0.0384 in |  |

### Pier 4WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1009.2400 | 31 | 1009.2400 | +0.0000 |  |
| Cap top, right corner | 1009.2400 | 31 | 1009.2400 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 31 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 31 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 31 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 31 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 31 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 31 | 7.0000 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 29.0000 | 31 | 29.0000 | +0.0000 |  |
| Column height, soffit to top of footing 2 | 29.0000 | 31 | 29.0000 | +0.0000 |  |
| Column height, soffit to flare 1 | 17.0000 | 31 | 17.0000 | +0.0000 |  |
| Column height, soffit to flare 2 | 17.0000 | 31 | 17.0000 | +0.0000 |  |
| Flare elevation | 985.2400 | 31 | 985.2400 | +0.0000 |  |
| Flare height (flare to footing top) | 12.0000 | 31 | 12.0000 | +0.0000 |  |
| Bottom of footing | 967.2400 | 31 | 967.2400 | -0.0000 |  |
| Footing thickness | 6.0000 | 31 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 42.0000 | 31 | 42.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 31 | 1.0000 | +0.0000 |  |
| Pile contract length | 75.0000 | 34 | 75.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1009.9800 | 31 | 1009.9763 | -0.0037 |  |
| Pedestal top (*ELEV.) 2 | 1010.2100 | 31 | 1010.2046 | -0.0054 |  |
| Pedestal top (*ELEV.) 3 | 1010.4200 | 31 | 1010.4213 | +0.0013 |  |
| Pedestal top (*ELEV.) 4 | 1010.1900 | 31 | 1010.1930 | +0.0030 |  |
| Pedestal top (*ELEV.) 5 | 1009.8800 | 31 | 1009.8834 | +0.0034 |  |
| Pedestal top (*ELEV.) 6 | 1009.5700 | 31 | 1009.5717 | +0.0018 |  |
| Pedestal height (printed) 1 | 8.938 in | 31 | 8.836 in | -0.1019 in |  |
| Pedestal height (printed) 2 | 11.688 in | 31 | 11.576 in | -0.1119 in |  |
| Pedestal height (printed) 3 | 14.188 in | 31 | 14.176 in | -0.0119 in |  |
| Pedestal height (printed) 4 | 11.438 in | 31 | 11.436 in | -0.0019 in |  |
| Pedestal height (printed) 5 | 7.750 in | 31 | 7.721 in | -0.0289 in |  |
| Pedestal height (printed) 6 | 4.000 in | 31 | 3.981 in | -0.0190 in |  |

### Pier 5WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1012.3200 | 35 | 1012.3200 | -0.0000 |  |
| Cap top, right corner | 1012.3200 | 35 | 1012.3200 | -0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 35 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 35 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 35 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 35 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 35 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 35 | 7.0000 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 32.0000 | 35 | 32.0000 | -0.0000 |  |
| Column height, soffit to top of footing 2 | 32.0000 | 35 | 32.0000 | -0.0000 |  |
| Column height, soffit to flare 1 | 19.0000 | 35 | 19.0000 | -0.0000 |  |
| Column height, soffit to flare 2 | 19.0000 | 35 | 19.0000 | -0.0000 |  |
| Flare elevation | 986.3200 | 35 | 986.3200 | +0.0000 |  |
| Flare height (flare to footing top) | 13.0000 | 35 | 13.0000 | +0.0000 |  |
| Bottom of footing | 967.8200 | 35 | 967.8200 | +0.0000 |  |
| Footing thickness | 5.5000 | 35 | 5.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 44.5000 | 35 | 44.5000 | -0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 35 | 1.0000 | +0.0000 |  |
| Pile contract length | 75.0000 | 38 | 75.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 (row B) | 1012.9300 | 35 | 1012.9273 | -0.0027 |  |
| Pedestal top (*ELEV.) 2 (row B) | 1013.2200 | 35 | 1013.2147 | -0.0053 |  |
| Pedestal top (*ELEV.) 3 (row B) | 1013.4300 | 35 | 1013.4313 | +0.0013 |  |
| Pedestal top (*ELEV.) 4 (row B) | 1013.2100 | 35 | 1013.2030 | -0.0070 |  |
| Pedestal top (*ELEV.) 5 (row B) | 1012.9300 | 35 | 1012.9277 | -0.0023 |  |
| Pedestal top (*ELEV.) 6 (row B) | 1012.6500 | 35 | 1012.6513 | +0.0013 |  |
| Pedestal top (*ELEV.) 1 (row A) | 1012.9300 | 35 | 1012.9130 | -0.0170 | ** |
| Pedestal top (*ELEV.) 2 (row A) | 1013.2200 | 35 | 1013.2078 | -0.0122 | ** |
| Pedestal top (*ELEV.) 3 (row A) | 1013.4300 | 35 | 1013.4378 | +0.0078 |  |
| Pedestal top (*ELEV.) 4 (row A) | 1013.2100 | 35 | 1013.2078 | -0.0022 |  |
| Pedestal top (*ELEV.) 5 (row A) | 1012.9300 | 35 | 1012.9346 | +0.0046 |  |
| Pedestal top (*ELEV.) 6 (row A) | 1012.6500 | 35 | 1012.6595 | +0.0095 |  |
| Pedestal height (printed) 1 (row B) | 7.375 in | 35 | 7.288 in | -0.0875 in |  |
| Pedestal height (printed) 2 (row B) | 10.812 in | 35 | 10.736 in | -0.0767 in |  |
| Pedestal height (printed) 3 (row B) | 13.375 in | 35 | 13.336 in | -0.0392 in |  |
| Pedestal height (printed) 4 (row B) | 10.750 in | 35 | 10.596 in | -0.1542 in | ** |
| Pedestal height (printed) 5 (row B) | 7.375 in | 35 | 7.293 in | -0.0822 in |  |
| Pedestal height (printed) 6 (row B) | 4.000 in | 35 | 3.975 in | -0.0249 in |  |
| Pedestal height (printed) 1 (row A) | 7.375 in | 35 | 7.116 in | -0.2594 in | ** |
| Pedestal height (printed) 2 (row A) | 10.812 in | 35 | 10.653 in | -0.1591 in | ** |
| Pedestal height (printed) 3 (row A) | 13.375 in | 35 | 13.413 in | +0.0384 in |  |
| Pedestal height (printed) 4 (row A) | 10.750 in | 35 | 10.653 in | -0.0966 in |  |
| Pedestal height (printed) 5 (row A) | 7.375 in | 35 | 7.375 in | +0.0004 in |  |
| Pedestal height (printed) 6 (row A) | 4.000 in | 35 | 4.074 in | +0.0738 in |  |

### Pier 6WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1013.0700 | 43 | 1013.0700 | +0.0000 |  |
| Cap top, right corner | 1014.9200 | 43 | 1014.9200 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 43 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 43 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 43 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 43 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 43 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 43 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1006.2800 | 43 | 1006.2861 | +0.0061 |  |
| Soffit elev. at column 2 | 1006.7500 | 43 | 1006.7587 | +0.0087 |  |
| Soffit elev. at column 3 | 1007.2300 | 43 | 1007.2313 | +0.0013 |  |
| Soffit elev. at column 4 | 1007.7000 | 43 | 1007.7039 | +0.0039 |  |
| Column height, soffit to top of footing 1 | 39.0000 | 43 | 39.0061 | +0.0061 |  |
| Column height, soffit to top of footing 2 | 39.4688 | 43 | 39.4787 | +0.0100 |  |
| Column height, soffit to top of footing 3 | 39.9479 | 43 | 39.9513 | +0.0034 |  |
| Column height, soffit to top of footing 4 | 40.4219 | 43 | 40.4239 | +0.0020 |  |
| Column height, soffit to flare 1 | 20.0000 | 43 | 20.0061 | +0.0061 |  |
| Column height, soffit to flare 2 | 20.4688 | 43 | 20.4787 | +0.0100 |  |
| Column height, soffit to flare 3 | 20.9479 | 43 | 20.9513 | +0.0034 |  |
| Column height, soffit to flare 4 | 21.4219 | 43 | 21.4239 | +0.0020 |  |
| Flare elevation | 986.2800 | 43 | 986.2800 | +0.0000 |  |
| Flare height (flare to footing top) | 19.0000 | 43 | 19.0000 | +0.0000 |  |
| Bottom of footing | 961.2800 | 43 | 961.2800 | +0.0000 |  |
| Footing thickness | 6.0000 | 43 | 6.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 43 | 1.0000 | +0.0000 |  |
| Pile contract length | 65.0000 | 46 | 65.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1013.5000 | 43 | 1013.4913 | -0.0087 |  |
| Pedestal top (*ELEV.) 2 | 1013.7900 | 43 | 1013.7884 | -0.0016 |  |
| Pedestal top (*ELEV.) 3 | 1014.1000 | 43 | 1014.0906 | -0.0094 |  |
| Pedestal top (*ELEV.) 4 | 1014.3800 | 43 | 1014.3697 | -0.0103 | ** |
| Pedestal top (*ELEV.) 5 | 1014.5800 | 43 | 1014.5802 | +0.0002 |  |
| Pedestal top (*ELEV.) 6 | 1014.7900 | 43 | 1014.7895 | -0.0005 |  |
| Pedestal top (*ELEV.) 7 | 1015.1300 | 43 | 1015.1295 | -0.0005 |  |
| Pedestal top (*ELEV.) 8 | 1015.3600 | 43 | 1015.3595 | -0.0005 |  |
| Pedestal top (*ELEV.) 9 | 1015.3200 | 43 | 1015.3183 | -0.0017 |  |
| Pedestal top (*ELEV.) 10 | 1015.2800 | 43 | 1015.2771 | -0.0029 |  |
| Pedestal top (*ELEV.) 11 | 1015.2400 | 43 | 1015.2359 | -0.0041 |  |

Printed, no model equivalent: concrete seal coat thickness under the footings (excavate and dewater notes, 'if used', water elev. 975) 3.00

### Pier 6A

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1011.5000 | 39 | 1011.5000 | +0.0000 |  |
| Cap top, right corner | 1013.2800 | 39 | 1013.2600 | -0.0200 | ** |
| Cap top, left corner (ahead half) | 1010.7500 | 39 | 1010.7500 | +0.0000 |  |
| Cap top, right corner (ahead half) | 1012.5100 | 39 | 1012.5100 | -0.0000 |  |
| Top corner to beak tip, left (dim) (ahead half) | 5.0000 | 39 | 5.0000 | +0.0000 |  |
| Top corner to beak tip, right (dim) (ahead half) | 5.0000 | 39 | 5.0000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) (ahead half) | 3.5000 | 39 | - | - | plan only |
| Beak tip to soffit line, right (dim) (ahead half) | 3.5000 | 39 | - | - | plan only |
| Cap depth at left kink (dim) (ahead half) | 8.5000 | 39 | 8.5038 | +0.0039 |  |
| Cap depth at right kink (dim) (ahead half) | 8.5000 | 39 | 8.5061 | +0.0062 |  |
| Soffit elev. at left kink | 1002.9400 | 39 | 1002.9400 | +0.0000 |  |
| Soffit elev. at right kink | 1003.3100 | 39 | 1003.3100 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 29.0000 | 39 | 29.0050 | +0.0050 |  |
| Top to soffit at column (dim) (ahead half) | 8.5052 | 39 | 8.5050 | -0.0002 |  |
| Bottom of footing | 968.6200 | 39 | 968.6200 | +0.0000 |  |
| Footing thickness | 5.5000 | 39 | 5.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) (ahead half) | 43.0052 | 39 | 43.0100 | +0.0048 |  |
| Top of pile above footing bottom | 1.0000 | 39 | 1.0000 | +0.0000 |  |
| Pile contract length | 75.0000 | 42 | 75.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1011.2400 | 39 | 1011.2459 | +0.0059 |  |
| Pedestal top (*ELEV.) 2 | 1011.6300 | 39 | 1011.6332 | +0.0032 |  |
| Pedestal top (*ELEV.) 3 | 1012.0100 | 39 | 1012.0115 | +0.0015 |  |
| Pedestal top (*ELEV.) 4 | 1012.4100 | 39 | 1012.4028 | -0.0072 |  |
| Pedestal top (*ELEV.) 5 | 1012.7300 | 39 | 1012.7292 | -0.0008 |  |

Printed, no model equivalent: pedestal top, Ramp A2 bridge row (not part of this design), 1 from the left 1012.00; pedestal top, Ramp A2 bridge row (not part of this design), 2 from the left 1012.40; pedestal top, Ramp A2 bridge row (not part of this design), 3 from the left 1012.80; pedestal top, Ramp A2 bridge row (not part of this design), 4 from the left 1013.20; pedestal top, Ramp A2 bridge row (not part of this design), 5 from the left 1013.60

### Pier 7WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1014.6000 | 47 | 1014.6000 | +0.0000 |  |
| Cap top, right corner | 1017.0100 | 47 | 1017.0100 | -0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 47 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 47 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 47 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 47 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 47 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 47 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1007.9200 | 47 | 1007.9205 | +0.0005 |  |
| Soffit elev. at column 2 | 1008.8000 | 47 | 1008.8050 | +0.0050 |  |
| Soffit elev. at column 3 | 1009.6900 | 47 | 1009.6895 | -0.0005 |  |
| Column height, soffit to top of footing 1 | 31.0000 | 47 | 31.0005 | +0.0005 |  |
| Column height, soffit to top of footing 2 | 31.8854 | 47 | 31.8850 | -0.0004 |  |
| Column height, soffit to top of footing 3 | 32.7708 | 47 | 32.7695 | -0.0013 |  |
| Column height, soffit to flare 1 | 19.0000 | 47 | 19.0005 | +0.0005 |  |
| Column height, soffit to flare 2 | 19.8854 | 47 | 19.8850 | -0.0004 |  |
| Column height, soffit to flare 3 | 20.7708 | 47 | 20.7695 | -0.0013 |  |
| Flare elevation | 988.9200 | 47 | 988.9200 | +0.0000 |  |
| Flare height (flare to footing top) | 12.0000 | 47 | 12.0000 | +0.0000 |  |
| Bottom of footing | 969.9200 | 47 | 969.9200 | +0.0000 |  |
| Footing thickness | 7.0000 | 47 | 7.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 47 | 1.0000 | +0.0000 |  |
| Pile contract length | 75.0000 | 50 | 75.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1015.0800 | 47 | 1015.0715 | -0.0085 |  |
| Pedestal top (*ELEV.) 2 | 1015.3100 | 47 | 1015.3059 | -0.0041 |  |
| Pedestal top (*ELEV.) 3 | 1015.5700 | 47 | 1015.5640 | -0.0060 |  |
| Pedestal top (*ELEV.) 4 | 1015.7600 | 47 | 1015.7575 | -0.0025 |  |
| Pedestal top (*ELEV.) 5 | 1015.9800 | 47 | 1015.9784 | -0.0016 |  |
| Pedestal top (*ELEV.) 6 | 1016.3200 | 47 | 1016.3194 | -0.0006 |  |
| Pedestal top (*ELEV.) 7 | 1016.5800 | 47 | 1016.5720 | -0.0080 |  |
| Pedestal top (*ELEV.) 8 | 1016.8300 | 47 | 1016.8246 | -0.0054 |  |
| Pedestal top (*ELEV.) 9 | 1017.0800 | 47 | 1017.0772 | -0.0028 |  |
| Pedestal top (*ELEV.) 10 | 1017.3300 | 47 | 1017.3298 | -0.0002 |  |

### Pier 8WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1013.9700 | 51 | 1013.9700 | +0.0000 |  |
| Cap top, right corner | 1017.3800 | 51 | 1017.3800 | -0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 51 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 51 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 51 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 51 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 51 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 51 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1007.3300 | 51 | 1007.3406 | +0.0106 | ** (convention) |
| Soffit elev. at column 2 | 1008.2300 | 51 | 1008.2302 | +0.0002 |  |
| Soffit elev. at column 3 | 1009.1200 | 51 | 1009.1198 | -0.0002 |  |
| Soffit elev. at column 4 | 1010.0000 | 51 | 1010.0094 | +0.0094 |  |
| Column height, soffit to top of footing 1 | 25.0000 | 51 | 25.0106 | +0.0106 | ** (convention) |
| Column height, soffit to top of footing 2 | 25.9010 | 51 | 25.9002 | -0.0008 |  |
| Column height, soffit to top of footing 3 | 26.7917 | 51 | 26.7898 | -0.0019 |  |
| Column height, soffit to top of footing 4 | 27.6719 | 51 | 27.6793 | +0.0075 |  |
| Column height, soffit to flare 1 | 13.0000 | 51 | 13.0107 | +0.0106 | ** (convention) |
| Column height, soffit to flare 2 | 13.9010 | 51 | 13.9002 | -0.0008 |  |
| Column height, soffit to flare 3 | 14.7917 | 51 | 14.7898 | -0.0019 |  |
| Column height, soffit to flare 4 | 15.6719 | 51 | 15.6793 | +0.0075 |  |
| Flare elevation | 994.3300 | 51 | 994.3300 | +0.0000 |  |
| Flare height (flare to footing top) | 12.0000 | 51 | 12.0000 | +0.0000 |  |
| Bottom of footing | 976.3300 | 51 | 976.3300 | +0.0000 |  |
| Footing thickness | 6.0000 | 51 | 6.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 51 | 1.0000 | +0.0000 |  |
| Pile contract length | 85.0000 | 54 | 85.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1014.6400 | 51 | 1014.6350 | -0.0050 |  |
| Pedestal top (*ELEV.) 2 | 1014.8700 | 51 | 1014.8611 | -0.0089 |  |
| Pedestal top (*ELEV.) 3 | 1015.1000 | 51 | 1015.0907 | -0.0093 |  |
| Pedestal top (*ELEV.) 4 | 1015.3700 | 51 | 1015.3658 | -0.0042 |  |
| Pedestal top (*ELEV.) 5 | 1015.8100 | 51 | 1015.8082 | -0.0018 |  |
| Pedestal top (*ELEV.) 6 | 1016.2800 | 51 | 1016.2693 | -0.0107 | ** |
| Pedestal top (*ELEV.) 7 | 1016.7600 | 51 | 1016.7554 | -0.0046 |  |
| Pedestal top (*ELEV.) 8 | 1017.2700 | 51 | 1017.2612 | -0.0088 |  |
| Pedestal top (*ELEV.) 9 | 1017.7900 | 51 | 1017.7891 | -0.0009 |  |

### Pier 9WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1012.5700 | 55 | 1012.5700 | +0.0000 |  |
| Cap top, right corner | 1016.7500 | 55 | 1016.7500 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 55 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 55 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 55 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 55 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 55 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 55 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1006.1000 | 55 | 1006.1032 | +0.0032 |  |
| Soffit elev. at column 2 | 1007.6500 | 55 | 1007.6600 | +0.0100 |  |
| Soffit elev. at column 3 | 1009.2100 | 55 | 1009.2168 | +0.0068 |  |
| Column height, soffit to top of footing 1 | 24.0000 | 55 | 24.0032 | +0.0032 |  |
| Column height, soffit to top of footing 2 | 25.5521 | 55 | 25.5600 | +0.0079 |  |
| Column height, soffit to top of footing 3 | 27.1094 | 55 | 27.1168 | +0.0075 |  |
| Column height, soffit to flare 1 | 13.0000 | 55 | 13.0032 | +0.0032 |  |
| Column height, soffit to flare 2 | 14.5521 | 55 | 14.5600 | +0.0079 |  |
| Column height, soffit to flare 3 | 16.1094 | 55 | 16.1168 | +0.0075 |  |
| Flare elevation | 993.1000 | 55 | 993.1000 | +0.0000 |  |
| Flare height (flare to footing top) | 11.0000 | 55 | 11.0000 | +0.0000 |  |
| Bottom of footing | 977.1000 | 55 | 977.1000 | +0.0000 |  |
| Footing thickness | 5.0000 | 55 | 5.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 55 | 1.0000 | +0.0000 |  |
| Pile contract length | 80.0000 | 58 | 80.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1013.1900 | 55 | 1013.1815 | -0.0085 |  |
| Pedestal top (*ELEV.) 2 | 1013.5500 | 55 | 1013.5471 | -0.0029 |  |
| Pedestal top (*ELEV.) 3 | 1013.9000 | 55 | 1013.8950 | -0.0050 |  |
| Pedestal top (*ELEV.) 4 | 1014.5300 | 55 | 1014.5226 | -0.0075 |  |
| Pedestal top (*ELEV.) 5 | 1015.1500 | 55 | 1015.1497 | -0.0003 |  |
| Pedestal top (*ELEV.) 6 | 1015.7800 | 55 | 1015.7775 | -0.0026 |  |
| Pedestal top (*ELEV.) 7 | 1016.4100 | 55 | 1016.4059 | -0.0041 |  |
| Pedestal top (*ELEV.) 8 | 1017.0400 | 55 | 1017.0349 | -0.0051 |  |

### Pier 10WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1011.7000 | 59 | 1011.7000 | +0.0000 |  |
| Cap top, right corner | 1015.8300 | 59 | 1015.8300 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 59 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 59 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 59 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 59 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 59 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 59 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1005.2600 | 59 | 1005.2736 | +0.0136 | ** (convention) |
| Soffit elev. at column 2 | 1006.7600 | 59 | 1006.7650 | +0.0050 |  |
| Soffit elev. at column 3 | 1008.2500 | 59 | 1008.2564 | +0.0064 |  |
| Column height, soffit to top of footing 1 | 26.0000 | 59 | 26.0136 | +0.0136 | ** (convention) |
| Column height, soffit to top of footing 2 | 27.5000 | 59 | 27.5050 | +0.0050 |  |
| Column height, soffit to top of footing 3 | 28.9896 | 59 | 28.9964 | +0.0068 |  |
| Column height, soffit to flare 1 | 14.0000 | 59 | 14.0136 | +0.0136 | ** (convention) |
| Column height, soffit to flare 2 | 15.5000 | 59 | 15.5050 | +0.0050 |  |
| Column height, soffit to flare 3 | 16.9896 | 59 | 16.9964 | +0.0068 |  |
| Flare elevation | 991.2600 | 59 | 991.2600 | +0.0000 |  |
| Flare height (flare to footing top) | 12.0000 | 59 | 12.0000 | +0.0000 |  |
| Bottom of footing | 974.2600 | 59 | 974.2600 | +0.0000 |  |
| Footing thickness | 5.0000 | 59 | 5.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 59 | 1.0000 | +0.0000 |  |
| Pile contract length | 85.0000 | 62 | 85.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1012.3200 | 59 | 1012.3129 | -0.0071 |  |
| Pedestal top (*ELEV.) 2 | 1012.9500 | 59 | 1012.9460 | -0.0040 |  |
| Pedestal top (*ELEV.) 3 | 1013.5900 | 59 | 1013.5811 | -0.0089 |  |
| Pedestal top (*ELEV.) 4 | 1014.2200 | 59 | 1014.2167 | -0.0033 |  |
| Pedestal top (*ELEV.) 5 | 1014.8600 | 59 | 1014.8528 | -0.0072 |  |
| Pedestal top (*ELEV.) 6 | 1015.4900 | 59 | 1015.4894 | -0.0006 |  |
| Pedestal top (*ELEV.) 7 | 1016.1300 | 59 | 1016.1265 | -0.0035 |  |

### Pier 11WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1010.9500 | 63 | 1010.9500 | -0.0000 |  |
| Cap top, right corner | 1015.1800 | 63 | 1015.1800 | +0.0000 |  |
| Cap top, left corner (ahead half) | 1012.9800 | 63 | 1012.9800 | -0.0000 |  |
| Cap top, right corner (ahead half) | 1017.2100 | 63 | 1017.2100 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 63 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 63 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 63 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 63 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 63 | 7.0000 | -0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 63 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1004.5100 | 63 | 1004.5223 | +0.0123 | ** (convention) |
| Soffit elev. at column 2 | 1006.5200 | 63 | 1006.5378 | +0.0178 | ** (convention) |
| Soffit elev. at column 3 | 1007.5900 | 63 | 1007.6077 | +0.0177 | ** (convention) |
| Column height, soffit to top of footing 1 | 27.0000 | 63 | 27.0123 | +0.0123 | ** (convention) |
| Column height, soffit to top of footing 2 | 29.0104 | 63 | 29.0278 | +0.0173 | ** (convention) |
| Column height, soffit to top of footing 3 | 30.0833 | 63 | 30.0977 | +0.0144 | ** (convention) |
| Column height, soffit to flare 1 | 14.0000 | 63 | 14.0123 | +0.0123 | ** (convention) |
| Column height, soffit to flare 2 | 16.0104 | 63 | 16.0278 | +0.0173 | ** (convention) |
| Column height, soffit to flare 3 | 17.0833 | 63 | 17.0977 | +0.0144 | ** (convention) |
| Flare elevation | 990.5100 | 63 | 990.5100 | -0.0000 |  |
| Flare height (flare to footing top) | 13.0000 | 63 | 13.0000 | +0.0000 |  |
| Bottom of footing | 971.0100 | 63 | 971.0100 | -0.0000 |  |
| Footing thickness | 6.5000 | 63 | 6.5000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 63 | 1.0000 | +0.0000 |  |
| Pile contract length | 70.0000 | 66 | 70.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 (row B) | 1011.5400 | 63 | 1011.5396 | -0.0004 |  |
| Pedestal top (*ELEV.) 2 (row B) | 1012.1900 | 63 | 1012.1818 | -0.0082 |  |
| Pedestal top (*ELEV.) 3 (row B) | 1012.8300 | 63 | 1012.8265 | -0.0035 |  |
| Pedestal top (*ELEV.) 4 (row B) | 1013.4800 | 63 | 1013.4715 | -0.0085 |  |
| Pedestal top (*ELEV.) 5 (row B) | 1014.1200 | 63 | 1014.1169 | -0.0031 |  |
| Pedestal top (*ELEV.) 6 (row B) | 1014.7700 | 63 | 1014.7627 | -0.0073 |  |
| Pedestal top (*ELEV.) 7 (row B) | 1015.4100 | 63 | 1015.4089 | -0.0011 |  |
| Pedestal top (*ELEV.) 1 (row A) | 1013.6600 | 63 | 1013.6516 | -0.0084 |  |
| Pedestal top (*ELEV.) 2 (row A) | 1014.3000 | 63 | 1014.2961 | -0.0039 |  |
| Pedestal top (*ELEV.) 3 (row A) | 1014.9400 | 63 | 1014.9409 | +0.0009 |  |
| Pedestal top (*ELEV.) 4 (row A) | 1015.5900 | 63 | 1015.5861 | -0.0039 |  |
| Pedestal top (*ELEV.) 5 (row A) | 1016.2400 | 63 | 1016.2316 | -0.0084 |  |
| Pedestal top (*ELEV.) 6 (row A) | 1016.8800 | 63 | 1016.8775 | -0.0025 |  |
| Pedestal top (*ELEV.) 7 (row A) | 1017.5300 | 63 | 1017.5238 | -0.0062 |  |

### Pier 12WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1010.5300 | 67 | 1010.5300 | +0.0000 |  |
| Cap top, right corner | 1016.3300 | 67 | 1016.3300 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 67 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 67 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 67 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 67 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 67 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 67 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1004.1700 | 67 | 1004.1773 | +0.0073 |  |
| Soffit elev. at column 2 | 1005.6700 | 67 | 1005.6791 | +0.0091 |  |
| Soffit elev. at column 3 | 1007.1700 | 67 | 1007.1809 | +0.0109 | ** (convention) |
| Soffit elev. at column 4 | 1008.6700 | 67 | 1008.6827 | +0.0127 | ** (convention) |
| Column height, soffit to top of footing 1 | 27.0000 | 67 | 27.0073 | +0.0073 |  |
| Column height, soffit to top of footing 2 | 28.5000 | 67 | 28.5091 | +0.0091 |  |
| Column height, soffit to top of footing 3 | 30.0000 | 67 | 30.0109 | +0.0109 | ** (convention) |
| Column height, soffit to top of footing 4 | 31.5000 | 67 | 31.5127 | +0.0127 | ** (convention) |
| Column height, soffit to flare 1 | 13.0000 | 67 | 13.0073 | +0.0073 |  |
| Column height, soffit to flare 2 | 14.5000 | 67 | 14.5091 | +0.0091 |  |
| Column height, soffit to flare 3 | 16.0000 | 67 | 16.0109 | +0.0109 | ** (convention) |
| Column height, soffit to flare 4 | 17.5000 | 67 | 17.5127 | +0.0127 | ** (convention) |
| Flare elevation | 991.1700 | 67 | 991.1700 | -0.0000 |  |
| Flare height (flare to footing top) | 14.0000 | 67 | 14.0000 | +0.0000 |  |
| Bottom of footing | 971.6700 | 67 | 971.6700 | -0.0000 |  |
| Footing thickness | 5.5000 | 67 | 5.5000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 67 | 1.0000 | +0.0000 |  |
| Pile contract length | 80.0000 | 70 | 80.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1011.1900 | 67 | 1011.1886 | -0.0014 |  |
| Pedestal top (*ELEV.) 2 | 1011.6300 | 67 | 1011.6225 | -0.0075 |  |
| Pedestal top (*ELEV.) 3 | 1012.0700 | 67 | 1012.0681 | -0.0019 |  |
| Pedestal top (*ELEV.) 4 | 1012.7000 | 67 | 1012.6918 | -0.0082 |  |
| Pedestal top (*ELEV.) 5 | 1013.3500 | 67 | 1013.3416 | -0.0084 |  |
| Pedestal top (*ELEV.) 6 | 1014.0000 | 67 | 1013.9916 | -0.0084 |  |
| Pedestal top (*ELEV.) 7 | 1014.6500 | 67 | 1014.6419 | -0.0080 |  |
| Pedestal top (*ELEV.) 8 | 1015.3000 | 67 | 1015.2926 | -0.0074 |  |
| Pedestal top (*ELEV.) 9 | 1015.9500 | 67 | 1015.9435 | -0.0065 |  |
| Pedestal top (*ELEV.) 10 | 1016.6000 | 67 | 1016.5948 | -0.0052 |  |

### Pier 13WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1010.2300 | 71 | 1010.2300 | +0.0000 |  |
| Cap top, right corner | 1015.2500 | 71 | 1015.2500 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 71 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 71 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 71 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 71 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 71 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 71 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1003.9000 | 71 | 1003.9047 | +0.0047 |  |
| Soffit elev. at column 2 | 1005.7300 | 71 | 1005.7400 | +0.0100 |  |
| Soffit elev. at column 3 | 1007.5700 | 71 | 1007.5753 | +0.0053 |  |
| Column height, soffit to top of footing 1 | 27.0000 | 71 | 27.0047 | +0.0047 |  |
| Column height, soffit to top of footing 2 | 28.8333 | 71 | 28.8400 | +0.0067 |  |
| Column height, soffit to top of footing 3 | 30.6719 | 71 | 30.6753 | +0.0034 |  |
| Column height, soffit to flare 1 | 15.0000 | 71 | 15.0047 | +0.0047 |  |
| Column height, soffit to flare 2 | 16.8333 | 71 | 16.8400 | +0.0067 |  |
| Column height, soffit to flare 3 | 18.6719 | 71 | 18.6753 | +0.0034 |  |
| Flare elevation | 988.9000 | 71 | 988.9000 | +0.0000 |  |
| Flare height (flare to footing top) | 12.0000 | 71 | 12.0000 | +0.0000 |  |
| Bottom of footing | 970.9000 | 71 | 970.9000 | +0.0000 |  |
| Footing thickness | 6.0000 | 71 | 6.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 71 | 1.0000 | +0.0000 |  |
| Pile contract length | 65.0000 | 74 | 65.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1010.8900 | 71 | 1010.8839 | -0.0062 |  |
| Pedestal top (*ELEV.) 2 | 1011.2300 | 71 | 1011.2256 | -0.0044 |  |
| Pedestal top (*ELEV.) 3 | 1011.5900 | 71 | 1011.5870 | -0.0029 |  |
| Pedestal top (*ELEV.) 4 | 1012.2500 | 71 | 1012.2432 | -0.0068 |  |
| Pedestal top (*ELEV.) 5 | 1012.9000 | 71 | 1012.8996 | -0.0004 |  |
| Pedestal top (*ELEV.) 6 | 1013.5600 | 71 | 1013.5562 | -0.0038 |  |
| Pedestal top (*ELEV.) 7 | 1014.2200 | 71 | 1014.2131 | -0.0069 |  |
| Pedestal top (*ELEV.) 8 | 1014.8700 | 71 | 1014.8702 | +0.0002 |  |
| Pedestal top (*ELEV.) 9 | 1015.5300 | 71 | 1015.5275 | -0.0026 |  |

### Pier 14WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1009.4900 | 75 | 1009.4900 | +0.0000 |  |
| Cap top, right corner | 1014.1700 | 75 | 1014.1700 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 75 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 75 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 75 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 75 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 75 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 75 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1003.1800 | 75 | 1003.1864 | +0.0064 |  |
| Soffit elev. at column 2 | 1004.8200 | 75 | 1004.8300 | +0.0100 |  |
| Soffit elev. at column 3 | 1006.4700 | 75 | 1006.4736 | +0.0036 |  |
| Column height, soffit to top of footing 1 | 26.0000 | 75 | 26.0064 | +0.0064 |  |
| Column height, soffit to top of footing 2 | 27.6406 | 75 | 27.6500 | +0.0094 |  |
| Column height, soffit to top of footing 3 | 29.2917 | 75 | 29.2936 | +0.0019 |  |
| Column height, soffit to flare 1 | 14.0000 | 75 | 14.0064 | +0.0064 |  |
| Column height, soffit to flare 2 | 15.6406 | 75 | 15.6500 | +0.0094 |  |
| Column height, soffit to flare 3 | 17.2917 | 75 | 17.2936 | +0.0019 |  |
| Flare elevation | 989.1800 | 75 | 989.1800 | +0.0000 |  |
| Flare height (flare to footing top) | 12.0000 | 75 | 12.0000 | +0.0000 |  |
| Bottom of footing | 972.1800 | 75 | 972.1800 | +0.0000 |  |
| Footing thickness | 5.0000 | 75 | 5.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 75 | 1.0000 | +0.0000 |  |
| Pile contract length | 85.0000 | 78 | 85.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1010.1500 | 75 | 1010.1389 | -0.0111 | ** |
| Pedestal top (*ELEV.) 2 | 1010.4900 | 75 | 1010.4851 | -0.0049 |  |
| Pedestal top (*ELEV.) 3 | 1011.1500 | 75 | 1011.1472 | -0.0028 |  |
| Pedestal top (*ELEV.) 4 | 1011.8100 | 75 | 1011.8094 | -0.0006 |  |
| Pedestal top (*ELEV.) 5 | 1012.4800 | 75 | 1012.4718 | -0.0082 |  |
| Pedestal top (*ELEV.) 6 | 1013.1400 | 75 | 1013.1344 | -0.0056 |  |
| Pedestal top (*ELEV.) 7 | 1013.8000 | 75 | 1013.7971 | -0.0029 |  |
| Pedestal top (*ELEV.) 8 | 1014.4600 | 75 | 1014.4600 | +0.0000 |  |

### Pier 15WB

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1008.3900 | 79 | 1008.3900 | +0.0000 |  |
| Cap top, right corner | 1013.1000 | 79 | 1013.1000 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 79 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 79 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 79 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 79 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 79 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 79 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1002.1000 | 79 | 1002.1080 | +0.0080 |  |
| Soffit elev. at column 2 | 1003.7300 | 79 | 1003.7450 | +0.0150 | ** (convention) |
| Soffit elev. at column 3 | 1005.3700 | 79 | 1005.3820 | +0.0120 | ** (convention) |
| Column height, soffit to top of footing 1 | 19.0000 | 79 | 19.0080 | +0.0080 |  |
| Column height, soffit to top of footing 2 | 20.6302 | 79 | 20.6450 | +0.0148 | ** (convention) |
| Column height, soffit to top of footing 3 | 22.2708 | 79 | 22.2820 | +0.0112 | ** (convention) |
| Bottom of footing | 978.1000 | 79 | 978.1000 | +0.0000 |  |
| Footing thickness | 5.0000 | 79 | 5.0000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 79 | 1.0000 | +0.0000 |  |
| Pile contract length | 85.0000 | 82 | 85.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1009.0400 | 79 | 1009.0337 | -0.0063 |  |
| Pedestal top (*ELEV.) 2 | 1009.3900 | 79 | 1009.3843 | -0.0057 |  |
| Pedestal top (*ELEV.) 3 | 1010.0600 | 79 | 1010.0521 | -0.0079 |  |
| Pedestal top (*ELEV.) 4 | 1010.7200 | 79 | 1010.7199 | -0.0001 |  |
| Pedestal top (*ELEV.) 5 | 1011.3900 | 79 | 1011.3879 | -0.0021 |  |
| Pedestal top (*ELEV.) 6 | 1012.0600 | 79 | 1012.0559 | -0.0041 |  |
| Pedestal top (*ELEV.) 7 | 1012.7300 | 79 | 1012.7241 | -0.0059 |  |
| Pedestal top (*ELEV.) 8 | 1013.4000 | 79 | 1013.3923 | -0.0077 |  |

### Pier 1G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1000.7900 | 19 | 1000.7900 | +0.0000 |  |
| Cap top, right corner | 1001.4900 | 19 | 1001.4900 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 19 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 19 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 19 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 19 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 19 | 9.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 9.0000 | 19 | 9.0000 | +0.0000 |  |
| Soffit elev. at left kink | 992.0300 | 19 | 992.0300 | +0.0000 |  |
| Soffit elev. at right kink | 992.2500 | 19 | 992.2500 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 25.0000 | 19 | 25.0000 | +0.0000 |  |
| Top to soffit at column (dim) | 9.0000 | 19 | 9.0000 | +0.0000 |  |
| Bottom of footing | 961.6400 | 19 | 961.6400 | -0.0000 |  |
| Footing thickness | 5.5000 | 19 | 5.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 39.5000 | 19 | 39.5000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 19 | 1.0000 | +0.0000 |  |
| Pile contract length | 90.0000 | 21 | 90.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1001.2300 | 19 | 1001.2301 | +0.0001 |  |
| Pedestal top (*ELEV.) 2 | 1001.4200 | 19 | 1001.4234 | +0.0034 |  |
| Pedestal top (*ELEV.) 3 | 1001.6200 | 19 | 1001.6168 | -0.0032 |  |
| Pedestal top (*ELEV.) 4 | 1001.8100 | 19 | 1001.8101 | +0.0001 |  |

### Pier 2G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1008.6200 | 22 | 1008.6200 | +0.0000 |  |
| Cap top, right corner | 1008.2900 | 22 | 1008.2900 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 22 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 22 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 22 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 22 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 22 | 8.9969 | -0.0031 |  |
| Cap depth at right kink (dim) | 9.0000 | 22 | 9.0031 | +0.0031 |  |
| Soffit elev. at left kink | 999.5100 | 22 | 999.5100 | -0.0000 |  |
| Soffit elev. at right kink | 999.4000 | 22 | 999.4000 | +0.0000 |  |
| Column height, soffit to flare 1 | 20.0000 | 22 | 19.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0000 | 22 | 9.0000 | +0.0000 |  |
| Flare elevation | 979.4600 | 22 | 979.4600 | +0.0000 |  |
| Flare height (flare to footing top) | 12.0000 | 22 | 12.0000 | +0.0000 |  |
| Bottom of footing | 960.4600 | 22 | 960.4600 | +0.0000 |  |
| Footing thickness | 7.0000 | 22 | 7.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 48.0000 | 22 | 47.9950 | -0.0050 |  |
| Top of pile above footing bottom | 1.0000 | 22 | 1.0000 | +0.0000 |  |
| Pile contract length | 90.0000 | 24 | 90.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1008.9500 | 22 | 1008.9526 | +0.0026 |  |
| Pedestal top (*ELEV.) 2 | 1008.8600 | 22 | 1008.8604 | +0.0004 |  |
| Pedestal top (*ELEV.) 3 | 1008.7700 | 22 | 1008.7682 | -0.0018 |  |
| Pedestal top (*ELEV.) 4 | 1008.6800 | 22 | 1008.6759 | -0.0040 |  |

### Pier 3G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1016.4700 | 25 | 1016.4700 | +0.0000 |  |
| Cap top, right corner | 1014.4000 | 25 | 1014.4000 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 25 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 25 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 25 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 25 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 25 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 25 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1006.7400 | 25 | 1006.7400 | +0.0000 |  |
| Soffit elev. at right kink | 1006.0900 | 25 | 1006.0900 | +0.0000 |  |
| Column height, soffit to flare 1 | 25.0000 | 25 | 24.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 25 | 9.0200 | +0.0044 |  |
| Flare elevation | 981.4200 | 25 | 981.4200 | +0.0000 |  |
| Flare height (flare to footing top) | 14.0000 | 25 | 14.0000 | +0.0000 |  |
| Bottom of footing | 961.4200 | 25 | 961.4200 | +0.0000 |  |
| Footing thickness | 6.0000 | 25 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 54.0156 | 25 | 54.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 25 | 1.0000 | +0.0000 |  |
| Pile contract length | 90.0000 | 27 | 90.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1016.7700 | 25 | 1016.7752 | +0.0052 |  |
| Pedestal top (*ELEV.) 2 | 1016.2000 | 25 | 1016.2048 | +0.0048 |  |
| Pedestal top (*ELEV.) 3 | 1015.6300 | 25 | 1015.6345 | +0.0045 |  |
| Pedestal top (*ELEV.) 4 | 1015.0600 | 25 | 1015.0642 | +0.0042 |  |

### Pier 4G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1024.0700 | 28 | 1024.0700 | +0.0000 |  |
| Cap top, right corner | 1022.0000 | 28 | 1022.0000 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 28 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 28 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 28 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 28 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 28 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 28 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1014.3400 | 28 | 1014.3400 | +0.0000 |  |
| Soffit elev. at right kink | 1013.6900 | 28 | 1013.6900 | +0.0000 |  |
| Column height, soffit to flare 1 | 29.0000 | 28 | 28.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 28 | 9.0200 | +0.0044 |  |
| Flare elevation | 985.0200 | 28 | 985.0200 | +0.0000 |  |
| Flare height (flare to footing top) | 17.0000 | 28 | 17.0000 | +0.0000 |  |
| Bottom of footing | 962.0200 | 28 | 962.0200 | +0.0000 |  |
| Footing thickness | 6.0000 | 28 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 61.0156 | 28 | 61.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 28 | 1.0000 | +0.0000 |  |
| Pile contract length | 90.0000 | 30 | 90.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1024.3700 | 28 | 1024.3752 | +0.0052 |  |
| Pedestal top (*ELEV.) 2 | 1023.8000 | 28 | 1023.8048 | +0.0048 |  |
| Pedestal top (*ELEV.) 3 | 1023.2300 | 28 | 1023.2345 | +0.0045 |  |
| Pedestal top (*ELEV.) 4 | 1022.6600 | 28 | 1022.6642 | +0.0042 |  |

### Pier 5G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1030.0800 | 31 | 1030.0800 | +0.0000 |  |
| Cap top, right corner | 1028.0100 | 31 | 1028.0100 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 31 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 31 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 31 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 31 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 31 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 31 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1020.3500 | 31 | 1020.3500 | -0.0000 |  |
| Soffit elev. at right kink | 1019.7000 | 31 | 1019.7000 | +0.0000 |  |
| Column height, soffit to flare 1 | 29.0000 | 31 | 28.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 31 | 9.0200 | +0.0044 |  |
| Flare elevation | 991.0300 | 31 | 991.0300 | +0.0000 |  |
| Flare height (flare to footing top) | 16.0000 | 31 | 16.0000 | +0.0000 |  |
| Bottom of footing | 968.0300 | 31 | 968.0300 | +0.0000 |  |
| Footing thickness | 7.0000 | 31 | 7.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 60.0156 | 31 | 61.0150 | +0.9994 | ** |
| Top of pile above footing bottom | 1.0000 | 31 | 1.0000 | +0.0000 |  |
| Pile contract length | 95.0000 | 33 | 95.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 (row B) | 1030.3800 | 31 | 1030.3884 | +0.0084 |  |
| Pedestal top (*ELEV.) 2 (row B) | 1029.8400 | 31 | 1029.8383 | -0.0017 |  |
| Pedestal top (*ELEV.) 3 (row B) | 1029.2700 | 31 | 1029.2673 | -0.0027 |  |
| Pedestal top (*ELEV.) 4 (row B) | 1028.7000 | 31 | 1028.6963 | -0.0037 |  |
| Pedestal top (*ELEV.) 1 (row A) | 1030.3800 | 31 | 1030.3776 | -0.0024 |  |
| Pedestal top (*ELEV.) 2 (row A) | 1029.8400 | 31 | 1029.8495 | +0.0095 |  |
| Pedestal top (*ELEV.) 3 (row A) | 1029.2700 | 31 | 1029.2798 | +0.0098 |  |
| Pedestal top (*ELEV.) 4 (row A) | 1028.7000 | 31 | 1028.7101 | +0.0101 | ** |

### Pier 6G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1036.6200 | 34 | 1036.6200 | +0.0000 |  |
| Cap top, right corner | 1034.5500 | 34 | 1034.5500 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 34 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 34 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 34 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 34 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 34 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 34 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1026.8900 | 34 | 1026.8900 | +0.0000 |  |
| Soffit elev. at right kink | 1026.2400 | 34 | 1026.2400 | +0.0000 |  |
| Column height, soffit to flare 1 | 32.0000 | 34 | 31.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 34 | 9.0200 | +0.0044 |  |
| Flare elevation | 994.5700 | 34 | 994.5700 | +0.0000 |  |
| Flare height (flare to footing top) | 18.0000 | 34 | 18.0000 | +0.0000 |  |
| Bottom of footing | 970.5700 | 34 | 970.5700 | +0.0000 |  |
| Footing thickness | 6.0000 | 34 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 65.0156 | 34 | 65.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 34 | 1.0000 | +0.0000 |  |
| Pile contract length | 105.0000 | 36 | 105.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1036.9200 | 34 | 1036.9250 | +0.0050 |  |
| Pedestal top (*ELEV.) 2 | 1036.3700 | 34 | 1036.3755 | +0.0055 |  |
| Pedestal top (*ELEV.) 3 | 1035.8000 | 34 | 1035.8052 | +0.0052 |  |
| Pedestal top (*ELEV.) 4 | 1035.2300 | 34 | 1035.2349 | +0.0049 |  |

### Pier 7G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1041.9200 | 37 | 1041.9200 | +0.0000 |  |
| Cap top, right corner | 1039.8500 | 37 | 1039.8500 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 37 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 37 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 37 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 37 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 37 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 37 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1032.1900 | 37 | 1032.1900 | +0.0000 |  |
| Soffit elev. at right kink | 1031.5400 | 37 | 1031.5400 | +0.0000 |  |
| Column height, soffit to flare 1 | 29.0000 | 37 | 28.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 37 | 9.0200 | +0.0044 |  |
| Flare elevation | 1002.8700 | 37 | 1002.8700 | +0.0000 |  |
| Flare height (flare to footing top) | 16.0000 | 37 | 16.0000 | +0.0000 |  |
| Bottom of footing | 978.8700 | 37 | 978.8700 | +0.0000 |  |
| Footing thickness | 8.0000 | 37 | 8.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 62.0156 | 37 | 62.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 37 | 1.0000 | +0.0000 |  |
| Pile contract length | 100.0000 | 39 | 100.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1042.2200 | 37 | 1042.2227 | +0.0027 |  |
| Pedestal top (*ELEV.) 2 | 1041.6600 | 37 | 1041.6628 | +0.0028 |  |
| Pedestal top (*ELEV.) 3 | 1041.0900 | 37 | 1041.0925 | +0.0025 |  |
| Pedestal top (*ELEV.) 4 | 1040.5200 | 37 | 1040.5221 | +0.0021 |  |

### Pier 8G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1044.0400 | 40 | 1044.0400 | +0.0000 |  |
| Cap top, right corner | 1041.9700 | 40 | 1041.9700 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 40 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 40 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 40 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 40 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 40 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 40 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1034.3100 | 40 | 1034.3100 | +0.0000 |  |
| Soffit elev. at right kink | 1033.6600 | 40 | 1033.6600 | +0.0000 |  |
| Column height, soffit to flare 1 | 39.0000 | 40 | 38.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 40 | 9.0200 | +0.0044 |  |
| Flare elevation | 994.9900 | 40 | 994.9900 | +0.0000 |  |
| Flare height (flare to footing top) | 21.0000 | 40 | 21.0000 | +0.0000 |  |
| Bottom of footing | 966.4900 | 40 | 966.4900 | +0.0000 |  |
| Footing thickness | 7.5000 | 40 | 7.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 76.5156 | 40 | 76.5150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 40 | 1.0000 | +0.0000 |  |
| Pile contract length | 90.0000 | 42 | 90.0000 | -0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1044.3400 | 40 | 1044.3348 | -0.0052 |  |
| Pedestal top (*ELEV.) 2 | 1043.7800 | 40 | 1043.7748 | -0.0052 |  |
| Pedestal top (*ELEV.) 3 | 1043.2000 | 40 | 1043.2045 | +0.0045 |  |
| Pedestal top (*ELEV.) 4 | 1042.6300 | 40 | 1042.6342 | +0.0042 |  |

### Pier 9G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1042.9000 | 43 | 1042.9000 | +0.0000 |  |
| Cap top, right corner | 1040.8300 | 43 | 1040.8300 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 43 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 43 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 43 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 43 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 43 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 43 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1033.1700 | 43 | 1033.1700 | +0.0000 |  |
| Soffit elev. at right kink | 1032.5200 | 43 | 1032.5200 | +0.0000 |  |
| Column height, soffit to flare 1 | 38.0000 | 43 | 37.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 43 | 9.0200 | +0.0044 |  |
| Flare elevation | 994.8500 | 43 | 994.8500 | +0.0000 |  |
| Flare height (flare to footing top) | 21.0000 | 43 | 21.0000 | +0.0000 |  |
| Bottom of footing | 967.8500 | 43 | 967.8500 | +0.0000 |  |
| Footing thickness | 6.0000 | 43 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 74.0156 | 43 | 74.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 43 | 1.0000 | +0.0000 |  |
| Pile contract length | 95.0000 | 45 | 95.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 (row B) | 1044.1200 | 43 | 1044.1217 | +0.0017 |  |
| Pedestal top (*ELEV.) 2 (row B) | 1043.5800 | 43 | 1043.5827 | +0.0027 |  |
| Pedestal top (*ELEV.) 3 (row B) | 1043.0100 | 43 | 1043.0124 | +0.0024 |  |
| Pedestal top (*ELEV.) 4 (row B) | 1042.4400 | 43 | 1042.4422 | +0.0022 |  |
| Pedestal top (*ELEV.) 1 (row A) | 1043.2000 | 43 | 1043.2005 | +0.0005 |  |
| Pedestal top (*ELEV.) 2 (row A) | 1042.6700 | 43 | 1042.6717 | +0.0017 |  |
| Pedestal top (*ELEV.) 3 (row A) | 1042.1000 | 43 | 1042.1013 | +0.0013 |  |
| Pedestal top (*ELEV.) 4 (row A) | 1041.5300 | 43 | 1041.5309 | +0.0009 |  |

### Pier 10G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1041.8600 | 46 | 1041.8600 | +0.0000 |  |
| Cap top, right corner | 1039.7900 | 46 | 1039.7900 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 46 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 46 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 46 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 46 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 46 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 46 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1032.1300 | 46 | 1032.1300 | +0.0000 |  |
| Soffit elev. at right kink | 1031.4800 | 46 | 1031.4800 | +0.0000 |  |
| Column height, soffit to flare 1 | 38.0000 | 46 | 37.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 46 | 9.0200 | +0.0044 |  |
| Flare elevation | 993.8100 | 46 | 993.8100 | +0.0000 |  |
| Flare height (flare to footing top) | 20.0000 | 46 | 20.0000 | +0.0000 |  |
| Bottom of footing | 966.3100 | 46 | 966.3100 | +0.0000 |  |
| Footing thickness | 7.5000 | 46 | 7.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 74.5156 | 46 | 74.5150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 46 | 1.0000 | +0.0000 |  |
| Pile contract length | 90.0000 | 48 | 90.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1042.1600 | 46 | 1042.1548 | -0.0052 |  |
| Pedestal top (*ELEV.) 2 | 1041.6100 | 46 | 1041.6053 | -0.0047 |  |
| Pedestal top (*ELEV.) 3 | 1041.0400 | 46 | 1041.0350 | -0.0050 |  |
| Pedestal top (*ELEV.) 4 | 1040.4700 | 46 | 1040.4646 | -0.0054 |  |

### Pier 11G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1040.5500 | 49 | 1040.5500 | +0.0000 |  |
| Cap top, right corner | 1038.4800 | 49 | 1038.4800 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 49 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 49 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 49 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 49 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 49 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 49 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1030.8200 | 49 | 1030.8200 | +0.0000 |  |
| Soffit elev. at right kink | 1030.1700 | 49 | 1030.1700 | +0.0000 |  |
| Column height, soffit to flare 1 | 35.0000 | 49 | 34.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 49 | 9.0200 | +0.0044 |  |
| Flare elevation | 995.5000 | 49 | 995.5000 | +0.0000 |  |
| Flare height (flare to footing top) | 20.0000 | 49 | 20.0000 | +0.0000 |  |
| Bottom of footing | 968.0000 | 49 | 968.0000 | +0.0000 |  |
| Footing thickness | 7.5000 | 49 | 7.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 71.5156 | 49 | 71.5150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 49 | 1.0000 | +0.0000 |  |
| Pile contract length | 85.0000 | 51 | 85.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1040.8500 | 49 | 1040.8430 | -0.0070 |  |
| Pedestal top (*ELEV.) 2 | 1040.2800 | 49 | 1040.2727 | -0.0073 |  |
| Pedestal top (*ELEV.) 3 | 1039.7100 | 49 | 1039.7023 | -0.0077 |  |
| Pedestal top (*ELEV.) 4 | 1039.1400 | 49 | 1039.1320 | -0.0080 |  |

### Pier 12G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1037.3700 | 52 | 1037.3700 | +0.0000 |  |
| Cap top, right corner | 1035.3000 | 52 | 1035.3000 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 52 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 52 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 52 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 52 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 52 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 52 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1027.6400 | 52 | 1027.6400 | +0.0000 |  |
| Soffit elev. at right kink | 1026.9900 | 52 | 1026.9900 | +0.0000 |  |
| Column height, soffit to flare 1 | 35.0000 | 52 | 34.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 52 | 9.0200 | +0.0044 |  |
| Flare elevation | 992.3200 | 52 | 992.3200 | +0.0000 |  |
| Flare height (flare to footing top) | 23.0000 | 52 | 23.0000 | +0.0000 |  |
| Bottom of footing | 961.8200 | 52 | 961.8200 | +0.0000 |  |
| Footing thickness | 7.5000 | 52 | 7.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 74.5156 | 52 | 74.5150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 52 | 1.0000 | +0.0000 |  |
| Pile contract length | 60.0000 | 54 | 60.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1037.6700 | 52 | 1037.6695 | -0.0006 |  |
| Pedestal top (*ELEV.) 2 | 1037.1100 | 52 | 1037.1095 | -0.0005 |  |
| Pedestal top (*ELEV.) 3 | 1036.5400 | 52 | 1036.5392 | -0.0008 |  |
| Pedestal top (*ELEV.) 4 | 1035.9700 | 52 | 1035.9689 | -0.0011 |  |

### Pier 13G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1032.1300 | 55 | 1032.1300 | +0.0000 |  |
| Cap top, right corner | 1030.0600 | 55 | 1030.0600 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 55 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 55 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 55 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 55 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 55 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 55 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1022.4000 | 55 | 1022.4000 | +0.0000 |  |
| Soffit elev. at right kink | 1021.7500 | 55 | 1021.7500 | +0.0000 |  |
| Column height, soffit to flare 1 | 31.0000 | 55 | 30.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 55 | 9.0200 | +0.0044 |  |
| Flare elevation | 991.0800 | 55 | 991.0800 | -0.0000 |  |
| Flare height (flare to footing top) | 17.0000 | 55 | 17.0000 | +0.0000 |  |
| Bottom of footing | 968.0800 | 55 | 968.0800 | -0.0000 |  |
| Footing thickness | 6.0000 | 55 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 63.0156 | 55 | 63.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 55 | 1.0000 | +0.0000 |  |
| Pile contract length | 60.0000 | 57 | 60.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1032.4300 | 55 | 1032.4222 | -0.0078 |  |
| Pedestal top (*ELEV.) 2 | 1031.8600 | 55 | 1031.8518 | -0.0082 |  |
| Pedestal top (*ELEV.) 3 | 1031.2900 | 55 | 1031.2815 | -0.0085 |  |
| Pedestal top (*ELEV.) 4 | 1030.7200 | 55 | 1030.7112 | -0.0088 |  |

### Pier 14G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1027.9500 | 58 | 1027.9500 | +0.0000 |  |
| Cap top, right corner | 1025.8800 | 58 | 1025.8800 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 58 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 58 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 58 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 58 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 58 | 9.0203 | +0.0203 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 58 | 9.0197 | +0.0197 | ** (convention) |
| Soffit elev. at left kink | 1018.2200 | 58 | 1018.2200 | +0.0000 |  |
| Soffit elev. at right kink | 1017.5700 | 58 | 1017.5700 | +0.0000 |  |
| Column height, soffit to flare 1 | 29.0000 | 58 | 28.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 58 | 9.0200 | +0.0044 |  |
| Flare elevation | 988.9000 | 58 | 988.9000 | +0.0000 |  |
| Flare height (flare to footing top) | 16.0000 | 58 | 16.0000 | +0.0000 |  |
| Bottom of footing | 966.9000 | 58 | 966.9000 | +0.0000 |  |
| Footing thickness | 6.0000 | 58 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 60.0156 | 58 | 60.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 58 | 1.0000 | +0.0000 |  |
| Pile contract length | 65.0000 | 60 | 65.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 (row B) | 1028.2500 | 58 | 1028.2459 | -0.0041 |  |
| Pedestal top (*ELEV.) 2 (row B) | 1027.7000 | 58 | 1027.7072 | +0.0072 |  |
| Pedestal top (*ELEV.) 3 (row B) | 1027.1300 | 58 | 1027.1372 | +0.0073 |  |
| Pedestal top (*ELEV.) 4 (row B) | 1026.5600 | 58 | 1026.5673 | +0.0073 |  |
| Pedestal top (*ELEV.) 1 (row A) | 1028.2500 | 58 | 1028.2695 | +0.0195 | ** |
| Pedestal top (*ELEV.) 2 (row A) | 1027.7000 | 58 | 1027.6988 | -0.0012 |  |
| Pedestal top (*ELEV.) 3 (row A) | 1027.1300 | 58 | 1027.1281 | -0.0019 |  |
| Pedestal top (*ELEV.) 4 (row A) | 1026.5600 | 58 | 1026.5574 | -0.0026 |  |

### Pier 15G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1023.9500 | 61 | 1023.9500 | -0.0000 |  |
| Cap top, right corner | 1022.4100 | 61 | 1022.4100 | -0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 61 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 61 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 61 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 61 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 61 | 9.0120 | +0.0120 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 61 | 9.0080 | +0.0080 |  |
| Soffit elev. at left kink | 1014.4100 | 61 | 1014.4100 | -0.0000 |  |
| Soffit elev. at right kink | 1013.9300 | 61 | 1013.9300 | +0.0000 |  |
| Column height, soffit to flare 1 | 24.0000 | 61 | 24.0000 | +0.0000 |  |
| Top to soffit at column (dim) | 9.0104 | 61 | 9.0100 | -0.0004 |  |
| Flare elevation | 990.1700 | 61 | 990.1700 | -0.0000 |  |
| Flare height (flare to footing top) | 14.0000 | 61 | 14.0000 | +0.0000 |  |
| Bottom of footing | 970.1700 | 61 | 970.1700 | -0.0000 |  |
| Footing thickness | 6.0000 | 61 | 6.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 53.0104 | 61 | 53.0100 | -0.0004 |  |
| Top of pile above footing bottom | 1.0000 | 61 | 1.0000 | +0.0000 |  |
| Pile contract length | 65.0000 | 63 | 65.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1024.2600 | 61 | 1024.2579 | -0.0021 |  |
| Pedestal top (*ELEV.) 2 | 1023.8400 | 61 | 1023.8338 | -0.0062 |  |
| Pedestal top (*ELEV.) 3 | 1023.4100 | 61 | 1023.4097 | -0.0003 |  |
| Pedestal top (*ELEV.) 4 | 1022.9900 | 61 | 1022.9856 | -0.0043 |  |

### Pier 16G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1018.9800 | 64 | 1018.9800 | -0.0000 |  |
| Cap top, right corner | 1018.9800 | 64 | 1018.9800 | -0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 64 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 64 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 64 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 64 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 64 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 64 | 7.0000 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 29.0000 | 64 | 29.0000 | +0.0000 |  |
| Column height, soffit to top of footing 2 | 29.0000 | 64 | 29.0000 | +0.0000 |  |
| Bottom of footing | 977.4800 | 64 | 977.4800 | -0.0000 |  |
| Footing thickness | 5.5000 | 64 | 5.5000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 41.5000 | 64 | 41.5000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 64 | 1.0000 | +0.0000 |  |
| Pile contract length | 70.0000 | 67 | 70.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1019.4600 | 64 | 1019.4515 | -0.0084 |  |
| Pedestal top (*ELEV.) 2 | 1019.3400 | 64 | 1019.3326 | -0.0074 |  |
| Pedestal top (*ELEV.) 3 | 1019.3100 | 64 | 1019.3090 | -0.0010 |  |
| Pedestal top (*ELEV.) 4 | 1019.3800 | 64 | 1019.3809 | +0.0009 |  |
| Pedestal height (printed) 1 | 5.812 in | 64 | 5.659 in | -0.1539 in | ** |
| Pedestal height (printed) 2 | 4.375 in | 64 | 4.231 in | -0.1442 in | ** |
| Pedestal height (printed) 3 | 4.000 in | 64 | 3.948 in | -0.0519 in |  |
| Pedestal height (printed) 4 | 4.812 in | 64 | 4.810 in | -0.0021 in |  |

### Pier 17G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1012.7500 | 68 | 1012.7500 | +0.0000 |  |
| Cap top, right corner | 1014.0800 | 68 | 1014.0800 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 4.5000 | 68 | 4.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 4.5000 | 68 | 4.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 2.5000 | 68 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 2.5000 | 68 | - | - | plan only |
| Cap depth at left kink (dim) | 7.0000 | 68 | 7.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 7.0000 | 68 | 7.0000 | +0.0000 |  |
| Soffit elev. at column 1 | 1006.0600 | 68 | 1006.0637 | +0.0037 |  |
| Soffit elev. at column 2 | 1006.7600 | 68 | 1006.7663 | +0.0063 |  |
| Column height, soffit to top of footing 1 | 24.0000 | 68 | 24.0037 | +0.0037 |  |
| Column height, soffit to top of footing 2 | 24.7031 | 68 | 24.7063 | +0.0032 |  |
| Bottom of footing | 976.5600 | 68 | 976.5600 | +0.0000 |  |
| Footing thickness | 5.5000 | 68 | 5.5000 | +0.0000 |  |
| Top of pile above footing bottom | 1.0000 | 68 | 1.0000 | +0.0000 |  |
| Pile contract length | 70.0000 | 71 | 70.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1013.2800 | 68 | 1013.2796 | -0.0004 |  |
| Pedestal top (*ELEV.) 2 | 1013.6400 | 68 | 1013.6348 | -0.0052 |  |
| Pedestal top (*ELEV.) 3 | 1014.0100 | 68 | 1014.0019 | -0.0081 |  |
| Pedestal top (*ELEV.) 4 | 1014.3800 | 68 | 1014.3802 | +0.0002 |  |

### Pier 18G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1009.9300 | 72 | 1009.9300 | +0.0000 |  |
| Cap top, right corner | 1012.0000 | 72 | 1012.0000 | +0.0000 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 72 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 72 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 72 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 72 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 72 | 9.0197 | +0.0197 | ** (convention) |
| Cap depth at right kink (dim) | 9.0000 | 72 | 9.0203 | +0.0203 | ** (convention) |
| Soffit elev. at left kink | 1001.6200 | 72 | 1001.6200 | +0.0000 |  |
| Soffit elev. at right kink | 1002.2700 | 72 | 1002.2700 | +0.0000 |  |
| Column height, soffit to top of footing 1 | 22.0000 | 72 | 21.9950 | -0.0050 |  |
| Top to soffit at column (dim) | 9.0156 | 72 | 9.0200 | +0.0044 |  |
| Bottom of footing | 972.9500 | 72 | 972.9500 | -0.0000 |  |
| Footing thickness | 7.0000 | 72 | 7.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 38.0156 | 72 | 38.0150 | -0.0006 |  |
| Top of pile above footing bottom | 1.0000 | 72 | 1.0000 | +0.0000 |  |
| Pile contract length | 70.0000 | 74 | 70.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 | 1010.5900 | 72 | 1010.5874 | -0.0026 |  |
| Pedestal top (*ELEV.) 2 | 1011.1600 | 72 | 1011.1578 | -0.0022 |  |
| Pedestal top (*ELEV.) 3 | 1011.7300 | 72 | 1011.7281 | -0.0019 |  |
| Pedestal top (*ELEV.) 4 | 1012.3000 | 72 | 1012.2984 | -0.0016 |  |

### Pier 19G

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Cap top, left corner | 1009.6100 | 75 | 1009.6100 | +0.0000 |  |
| Cap top, right corner | 1011.5600 | 75 | 1011.5600 | +0.0000 |  |
| Cap top, left corner (ahead half) | 1010.8100 | 75 | 1010.8100 | +0.0000 |  |
| Cap top, right corner (ahead half) | 1012.7700 | 75 | 1012.7600 | -0.0100 |  |
| Top corner to beak tip, left (dim) | 5.5000 | 75 | 5.5000 | +0.0000 |  |
| Top corner to beak tip, right (dim) | 5.5000 | 75 | 5.5000 | +0.0000 |  |
| Beak tip to soffit line, left (dim) | 3.5000 | 75 | - | - | plan only |
| Beak tip to soffit line, right (dim) | 3.5000 | 75 | - | - | plan only |
| Cap depth at left kink (dim) | 9.0000 | 75 | 9.0000 | +0.0000 |  |
| Cap depth at right kink (dim) | 9.0000 | 75 | 9.0000 | -0.0000 |  |
| Soffit elev. at left kink | 1001.3600 | 75 | 1001.3656 | +0.0056 |  |
| Soffit elev. at right kink | 1001.8900 | 75 | 1001.9019 | +0.0119 | ** (convention) |
| Column height, soffit to top of footing 1 | 24.0000 | 75 | 24.0138 | +0.0138 | ** (convention) |
| Top to soffit at column (dim) | 9.0104 | 75 | 9.0000 | -0.0104 | ** |
| Bottom of footing | 972.6200 | 75 | 972.6200 | +0.0000 |  |
| Footing thickness | 5.0000 | 75 | 5.0000 | +0.0000 |  |
| Overall (top at column to bottom of footing) | 38.0104 | 75 | 38.0138 | +0.0033 |  |
| Top of pile above footing bottom | 1.0000 | 75 | 1.0000 | +0.0000 |  |
| Pile contract length | 70.0000 | 77 | 70.0000 | +0.0000 |  |
| Pedestal top (*ELEV.) 1 (row B) | 1010.2000 | 75 | 1010.1981 | -0.0019 |  |
| Pedestal top (*ELEV.) 2 (row B) | 1010.7600 | 75 | 1010.7589 | -0.0011 |  |
| Pedestal top (*ELEV.) 3 (row B) | 1011.3200 | 75 | 1011.3199 | -0.0001 |  |
| Pedestal top (*ELEV.) 4 (row B) | 1011.8800 | 75 | 1011.8810 | +0.0010 |  |
| Pedestal top (*ELEV.) 1 (row A) | 1011.5000 | 75 | 1011.4912 | -0.0088 |  |
| Pedestal top (*ELEV.) 2 (row A) | 1012.2400 | 75 | 1012.2293 | -0.0107 | ** |
| Pedestal top (*ELEV.) 3 (row A) | 1012.9400 | 75 | 1012.9394 | -0.0006 |  |

### West Abut

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Bottom of footing 1 | 992.8200 | 85 | 992.3887 | -0.4313 | ** |
| Bottom of footing 2 | 992.8200 | 85 | 992.6490 | -0.1710 | ** |
| Footing depth | 4.0000 | 86 | 4.5000 | +0.5000 | ** |
| Footing bottom to wash / seat base | 4.0833 | 86 | 4.5000 | +0.4167 | ** |
| Top of backwall at CL bearing | 1008.1500 | 86 | 1008.1027 | -0.0473 | ** |
| Pile contract length | 125.0000 | 86 | 125.0000 | +0.0000 |  |
| Top of pile above footing bottom | 2.0000 | 86 | 1.0000 | -1.0000 | ** |
| Beam step top (*ELEV.) 1 | 997.1500 | 85 | 997.1464 | -0.0036 |  |
| Beam step top (*ELEV.) 2 | 997.5100 | 85 | 997.5156 | +0.0056 |  |
| Beam step top (*ELEV.) 3 | 997.8700 | 85 | 997.8731 | +0.0031 |  |
| Beam step top (*ELEV.) 4 | 997.7800 | 85 | 997.7855 | +0.0055 |  |
| Beam step top (*ELEV.) 5 | 997.6200 | 85 | 997.6168 | -0.0032 |  |
| Beam step top (*ELEV.) 6 | 997.4500 | 85 | 997.4460 | -0.0040 |  |

Printed, no model equivalent: bottom of backwall (rear elevation) 996.65; footing bottom to bottom of backwall, 3'-10 3.83; top of backwall extension (girder F end) 1007.65; wing A-A top at the backwall (girder A end) 1007.32; wing A-A top at the free end 1006.97; wing K-K top at the backwall (girder F end) 1007.65; wing K-K top at the free end 1007.30; wing A-A bottom at the backwall 993.82; wing K-K bottom at the backwall 994.17; wing footing top, A-A No.1 996.68; wing footing top, A-A No.2 1000.27; wing footing top, K-K No.1 997.41; wing footing top, K-K No.2 1000.78

### East Abut

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Bottom of footing 1 | 1003.4400 | 89 | 1003.2944 | -0.1456 | ** |
| Bottom of footing 2 | 1008.2700 | 89 | 1007.9741 | -0.2959 | ** |
| Footing depth | 4.0000 | 89 | 4.5000 | +0.5000 | ** |
| Footing bottom to wash / seat base | 4.0833 | 89 | 4.5000 | +0.4167 | ** |
| Top of backwall at CL bearing | 1018.7100 | 89 | 1018.6815 | -0.0285 | ** |
| Pile contract length | 115.0000 | 90 | 125.0000 | +10.0000 | ** |
| Top of pile above footing bottom | 2.0000 | 89 | 1.0000 | -1.0000 | ** |
| Beam step top (*ELEV.) 1 | 1008.2000 | 89 | 1008.1903 | -0.0097 |  |
| Beam step top (*ELEV.) 2 | 1008.5400 | 89 | 1008.5446 | +0.0046 |  |
| Beam step top (*ELEV.) 3 | 1009.2200 | 89 | 1009.2168 | -0.0032 |  |
| Beam step top (*ELEV.) 4 | 1009.8900 | 89 | 1009.8892 | -0.0008 |  |
| Beam step top (*ELEV.) 5 | 1010.5700 | 89 | 1010.5616 | -0.0084 |  |
| Beam step top (*ELEV.) 6 | 1011.2300 | 89 | 1011.2340 | +0.0040 |  |
| Beam step top (*ELEV.) 7 | 1011.9100 | 89 | 1011.9065 | -0.0035 |  |
| Beam step top (*ELEV.) 8 | 1012.5800 | 89 | 1012.5791 | -0.0009 |  |

Printed, no model equivalent: bottom of backwall extension (girder J end) 1008.46; top of backwall extension (girder J end) 1021.09; wing K-K (J end) top at the backwall 1020.80; wing K-K top at the free end 1020.67; wing A-A (A end) top at the backwall 1016.24; wing A-A top at the free end 1016.11; wing K-K bottom at the backwall 1008.80; wing A-A bottom at the backwall 1004.24; wing footing top, K-K No.1 1010.78; wing footing top, K-K No.2 1014.15; wing footing top, A-A No.1 1006.29; wing footing top, A-A No.2 1009.61

### South Abut

| Item | Plan | PDF | Model | Diff | |
|---|---|---|---|---|---|
| Bottom of footing 1 | 991.1800 | 80 | 992.2134 | +1.0334 | ** |
| Bottom of footing 2 | 991.1800 | 80 | 992.8934 | +1.7134 | ** |
| Footing depth | 4.0000 | 80 | 3.0000 | -1.0000 | ** |
| Footing bottom to wash / seat base | 4.0833 | 80 | 3.0000 | -1.0833 | ** |
| Top of backwall at CL bearing | 1004.2200 | 80 | 1004.1413 | -0.0787 | ** |
| Pile contract length | 125.0000 | 81 | 100.0000 | -25.0000 | ** |
| Top of pile above footing bottom | 2.0000 | 80 | 1.0000 | -1.0000 | ** |
| Beam step top (*ELEV.) 1 | 995.5100 | 80 | 995.5134 | +0.0034 |  |
| Beam step top (*ELEV.) 2 | 995.7100 | 80 | 995.7068 | -0.0032 |  |
| Beam step top (*ELEV.) 3 | 995.9000 | 80 | 995.9001 | +0.0001 |  |
| Beam step top (*ELEV.) 4 | 996.0900 | 80 | 996.0934 | +0.0034 |  |

Printed, no model equivalent: bottom of backwall (rear elevation) 995.01; footing bottom to bottom of backwall, 3'-10 3.83; wing A-A top at the backwall 1003.98; wing A-A top at the free end 1003.08; wing B-B top at the backwall 1004.66; wing B-B top at the free end 1003.74; wing A-A bottom at the backwall 991.98; wing B-B bottom at the backwall 992.66; wing footing top, A-A No.1 994.00; wing footing top, A-A No.2 996.54; wing footing top, B-B No.1 994.68; wing footing top, B-B No.2 997.21

