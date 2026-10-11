# Pier pedestal plan sizes (I-80 WB Design 116, Ramp G Design 316)

Evidence for the `PedestalSizeAlong / PedestalSizeAcross / PedestalShared` values set at the end of
`samples/plans/piers-i80.csx` and `samples/plans/piers-ramp-g.csx`. Sizes are S x N: S runs along the support line
(cap), N runs across it. Values are in inches, as the scripts set them.

Sources:
- Plan text cache `C:\SBMData\plan-text\design-116|316\pNNN.txt` (copy in `temp\plan-text\`). On these pages the design
  sheet number is the PDF page - 2.
- "Cap plan" is the first sheet of each pier, with the pedestal bar layout in the PIER CAP PLAN.
- "Bar list" is the pier's REINFORCING BAR LIST.
- The 5m1 / 5n1 bend-diagram bases come from `docs/plan-data/bend-diagram-comparison.md` (section 6), where they were
  checked against the list lengths.

Read 2026-10-11 from the text layer only. Python / PyMuPDF is not available in the sandbox, so no clip was rasterized.
Every pedestal value below is a whole inch, with no stacked-fraction glyph. The along / across assignment follows the
text layout: the along value is the horizontal "4, n EQ., 4" string on the cap plan, and the across value is the rotated
"x'-x  5 SPA." string.

## Rule that confirms the printed sizes

- Pedestal bars are U bars, 6 to 8 per pedestal in each direction:
  - 5m1 CAP, PEDESTAL, LONGITUDINAL: the base gives the along size.
  - 5n1 CAP, PEDESTAL, TRANSVERSE: the base gives the across size.
- The printed base, measured out to out, plus a fixed allowance equals the printed pedestal size:
  - I-80 1WB to 6WB: base + 5" (2 1/2" each face).
  - I-80 7WB to 15WB, and all of Ramp G: base + 4" (2" each face).
- Bar counts divided by the bars per pedestal give the number of pedestals. This is what identifies the shared blocks.

## Set by the scripts: I-80 WB (`piers-i80.csx`)

| Pier | PDF p. (sheet): cap plan / bar list | Printed along | Printed across | 5m1 base -> along | 5n1 base -> across | 5m1 / 5n1 count = pedestals x bars | Script (S x N, shared) | Match |
|---|---|---|---|---|---|---|---|---|
| 1WB | 19 (17) / 22 (20) | 4, 5 EQ., 4 = 5'-6 | 5'-8 (5 EQ. SPA.) | 5'-1 + 5 = 66 | 5'-3 + 5 = 68 | 36 / 36 = 6 x 6 | 66 x 68 | yes |
| 2WB | 23 (21) / 26 (24) | 5'-6 | 5'-8 | 5'-1 + 5 = 66 | 5'-3 + 5 = 68 | 36 / 36 = 6 x 6 | 66 x 68 | yes |
| 3WB | 27 (25) / 30 (28) | 4, **6** EQ., 4 = 6'-6 | 5'-8 | 6'-1 + 5 = 78 | 5'-3 + 5 = 68 | 36 / 42 = 6 x 6 / 6 x 7 | 78 x 68 | yes |
| 4WB | 31 (29) / 34 (32) | 5'-6 | 5'-8 | 5'-1 + 5 = 66 | 5'-3 + 5 = 68 | 36 / 36 | 66 x 68 | yes |
| 5WB | 35 (33) / 38 (36) | 4, 5 EQ. SPA., 4 = 5'-0 | 7'-8 (EQ.) | 4'-7 + 5 = 60 | 7'-3 + 5 = 92 | 48 / 36 = 6 x 8 / 6 x 6 | 60 x 92, **shared** | yes |
| 6WB | 43 (41) / 46 (44) | 5'-6 | 5'-8 | list 10'-7 - 2 x 2'-9 = 5'-1 + 5 = 66 (UNVERIFIED) | 5'-3 + 5 = 68 | 66 / 66 = 11 x 6 | 66 x 68 | yes |
| 7WB | 47 (45) / 50 (48) | 4, **6** EQ., 4 = 6'-3 | 5'-8 | 5'-11 + 4 = 75 | 5'-4 + 4 = 68 | 60 / 70 = 10 x 6 / 10 x 7 | 75 x 68 | yes |
| 8WB | 51 (49) / 54 (52) | 5'-6 | 5'-8 | 5'-2 + 4 = 66 | 5'-4 + 4 = 68 | 54 / 54 = 9 x 6 | 66 x 68 | yes |
| 9WB | 55 (53) / 58 (56) | 5'-6 | 5'-8 | 5'-2 + 4 = 66 | 5'-4 + 4 = 68 | 48 / 48 | 66 x 68 | yes |
| 10WB | 59 (57) / 62 (60) | 5'-6 | 5'-8 | 5'-2 + 4 = 66 | 5'-4 + 4 = 68 | 42 / 42 | 66 x 68 | yes |
| 12WB | 67 (65) / 70 (68) | 5'-6 | 5'-8 | 5'-2 + 4 = 66 | 5'-4 + 4 = 68 | 60 / 60 | 66 x 68 | yes |
| 13WB | 71 (69) / 74 (72) | 5'-6 | 5'-8 | 5'-2 + 4 = 66 | 5'-4 + 4 = 68 | 54 / 54 | 66 x 68 | yes |
| 14WB | 75 (73) / 78 (76) | 5'-6 | 5'-8 | 5'-2 + 4 = 66 | 5'-4 + 4 = 68 | 48 / 48 | 66 x 68 | yes |
| 15WB | 79 (77) / 82 (80) | 5'-6 | 5'-8 | 5'-2 + 4 = 66 | 5'-4 + 4 = 68 | 48 / 48 | 66 x 68 | yes |

Pier 5WB, shared block: 36 5n1 = 6 blocks x 6 bars, and 48 5m1 = 6 x 8. That is one block per girder line (girders A-F)
for the back and ahead bearings together. The 7'-8 across size is what makes room for both bearing rows.

## Set by the scripts: Ramp G (`piers-ramp-g.csx`)

Pedestal bar layout printed on the cap plan:
- Along: either "2'-6 | 2'-6" (= 5'-0, bars 5, 5 SPA. @ 10 = 4'-2, 5) or "2'-10 | 2'-10" (= 5'-8).
- Across: "6 | 5 SPA. @ 1'-0 = 5'-0 | 6" = 6'-0. The text layer prints "5'-0;", and the trailing glyph is not decoded.
- 16G and 17G use the I-80 style instead: an along total with "5'-8  5 SPA." across.

| Pier | PDF p. (sheet): cap plan / bar list | Printed along | Printed across | 5m1 base -> along | 5n1 base -> across | 5m1 / 5n1 count | Script (S x N, shared) | Match |
|---|---|---|---|---|---|---|---|---|
| 1G | 19 (17) / 21 (19) | 2'-6 + 2'-6 = 5'-0 | 6, 5 @ 1'-0, 6 = 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 = 4 x 6 | 60 x 72 | yes |
| 2G | 22 (20) / 24 (22) | 2'-10 + 2'-10 = 5'-8 | 6'-0 | 5'-4 + 4 = 68 | 5'-8 + 4 = 72 | 24 / 24 | 68 x 72 | yes |
| 3G | 25 (23) / 27 (25) | 5'-0 | 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 | 60 x 72 | yes |
| 4G | 28 (26) / 30 (28) | 5'-0 | 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 | 60 x 72 | yes |
| 5G | 31 (29) / 33 (31) | 5'-0 | 6'-0 | list 9'-2 - 2 x 2'-3 = 4'-8 + 4 = 60 (UNVERIFIED) | 5'-8 + 4 = 72 | 24 / 24 = 4 x 6 | 60 x 72, **shared** | yes |
| 6G | 34 (32) / 36 (34) | 5'-0 | 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 | 60 x 72 | yes |
| 7G | 37 (35) / 39 (37) | 5'-8 | 6'-0 | 5'-4 + 4 = 68 | 5'-8 + 4 = 72 | 24 / 24 | 68 x 72 | yes |
| 8G | 40 (38) / 42 (40) | 5'-0 | 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 | 60 x 72 | yes |
| 10G | 46 (44) / 48 (46) | 5'-0 | 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 | 60 x 72 | yes |
| 11G | 49 (47) / 51 (49) | 5'-8 | 6'-0 | 5'-4 + 4 = 68 | 5'-8 + 4 = 72 | 24 / 24 | 68 x 72 | yes |
| 12G | 52 (50) / 54 (52) | 5'-8 | 6'-0 | 5'-4 + 4 = 68 | 5'-8 + 4 = 72 | 24 / 24 | 68 x 72 | yes |
| 13G | 55 (53) / 57 (55) | 5'-0 | 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 | 60 x 72 | yes |
| 14G | 58 (56) / 60 (58) | 5'-0 | 6'-0 | list 9'-2 - 2 x 2'-3 = 4'-8 + 4 = 60 (UNVERIFIED) | 5'-8 + 4 = 72 | 24 / 24 = 4 x 6 | 60 x 72, **shared** | yes |
| 15G | 61 (59) / 63 (61) | 5'-0 | 6'-0 | 4'-8 + 4 = 60 | 5'-8 + 4 = 72 | 24 / 24 | 60 x 72 | yes |
| 16G | 64 (62) / 67 (65) | 7'-2 | 5'-8 (5 SPA.) | 6'-10 + 4 = 86 | 5'-4 + 4 = 68 | 24 / 32 = 4 x 6 / 4 x 8 | 86 x 68 | yes |
| 17G | 68 (66) / 71 (69) | 6'-0 | 5'-8 (5 SPA.) | 5'-8 + 4 = 72 | 5'-4 + 4 = 68 | 24 / 28 = 4 x 6 / 4 x 7 | 72 x 68 | yes |
| 18G | 72 (70) / 74 (72) | 5'-8 | 6'-0 | 5'-4 + 4 = 68 | 5'-8 + 4 = 72 | 24 / 24 | 68 x 72 | yes |

### Shared blocks at 5G and 14G

5G and 14G are unit-joint piers. Units 1 to 4 have 5, 4, 5 and 5 spans (span list on the pier sheets), so the joints
fall at 5G, 9G and 14G. Each joint pier carries a back and an ahead bearing on each of girders A-D. The bar list still
has only 24 5n1 and 24 5m1 = 4 blocks x 6. That means one 5'-0 x 6'-0 block per girder line carries both bearings.
9G, the third joint, has separate pedestals (see below).

## Not set (the bearing-footprint pedestals stay)

| Pier | PDF p. (sheet) | What the plans show (text layer) | Why it is not set |
|---|---|---|---|
| 6A (I-80) | 39 (37) / 42 (40) | <ul><li>Along: "4 SPA. @ 11 = 3'-8; 5-5n1 BARS" and "2'-3 \| 2'-3" (4'-6). 5m1 base 4'-2 + 4 = 4'-6.</li><li>Across: "3 SPA. @ 1'-0 = 3'-0; 4-5m1 BARS" and "3'-10". 5n1 base 3'-6 + 4 = 3'-10.</li><li>Bar list: 40 5m1 / 50 5n1 = 10 pedestals x 4 / 5.</li></ul> | <ul><li>The cap carries two bridges: the ahead bearings of I-80 Unit 2 girders A-E and the back bearings of the Ramp A2 bridge, which is not part of this design and not modeled. The 10 pedestals are both rows (script: "Ramp A2 pedestals share the cap").</li><li>6A is an auxiliary support built by `pier-6a.csx`, and the pedestal block in `piers-i80.csx` does not cover it.</li><li>The 4'-6 x 3'-10 size reconciles in the text, but the clip was not rasterized, so it is not confirmed that the I-80 row uses this size and orientation: UNVERIFIED.</li></ul> |
| 11WB (I-80) | 63 (61) / 66 (64) | <ul><li>Two rows, labeled BACK BRG. and AHEAD BRG., staggered across the CL pier.</li><li>Across: back row 3'-11 ("3'-11  3 SPA."; 5n1 base 3'-7 + 4 = 3'-11); ahead row 3'-9 (5n2 CAP, PEDESTAL, TRANSVERSE, base 3'-5 + 4 = 3'-9). The two rows differ.</li><li>Along: "4, 5 EQ., 4". 5m1 is a BENT bar (list 9'-0) whose diagram is not decoded.</li><li>A "2'-6 \| 2'-6 \| 5'-0" string sits at the far left of the cap plan and cannot be tied to the pedestals without a raster: UNVERIFIED.</li></ul> | <ul><li>Two different across sizes on one support. The input has a single size per support.</li><li>The along total is not established (script: "two staggered rows, along size not printed").</li></ul> |
| 9G (Ramp G) | 43 (41) / 45 (43) | <ul><li>Separate back and ahead pedestals on each girder line. Bar groups are 6-5n1 (back) and 6-5n3 (ahead), each "5 SPA. @ 10 = 4'-2" with "2'-6 \| 2'-6" (5'-0).</li><li>Across: "2 SPA. @ 1'-0 = 2'-0; 3-5m1 BARS" per pedestal.</li><li>Bar list: 24 5m1 = 8 pedestals x 3; 24 5n1 + 24 5n3 = 4 + 4 pedestals x 6.</li><li>Bases: 5m1 4'-6, 5n1 5'-7, 5n3 2'-6, all with 3'-1 legs. 5n1 5'-7 + 4 = 5'-11 does not equal the 5'-0 along layout, and 5n3 is a different bar. The base + 4" rule does not close.</li><li>Pedestal tops: back / ahead 1044.12 / 1043.20, 1043.01 / 1042.10, 1042.44 / 1041.53. Back is 0.91 to 0.92 ft (11") higher.</li></ul> | <ul><li>Two pedestals per girder line with different bars.</li><li>The bar bases do not reconcile with one block size.</li><li>`PedestalShared` is wrong here: the plans show separate blocks.</li></ul> |
| 19G (Ramp G) | 75 (73) / 77 (75) | <ul><li>The cap is on the shared "Pier 11WB & 19G" line and carries two rows.</li><li>Ramp G back bearings (girders A-D): along "5 SPA. @ 10 = 4'-2; 6-5n1" with "2'-6 \| 2'-6" (5'-0); across "3 SPA. @ 1'-0 = 3'-0; 4-5m1".</li><li>I-80 Unit 3 ahead bearings (girders A-C): a different layout ("2 SPA. @ 6 = 1'-0 \| 3 SPA. @ 8 = 2'-0 \| 2 SPA. @ 6 = 1'-0"), with a note to see the I-80 WB sheets.</li><li>Bar list: 28 5m1 / 42 5n1 = 7 pedestals x 4 / 6. 5m1 base 4'-8 + 4 = 5'-0. 5n1 base 3'-5 + 4 = 3'-9. The across total 3'-9 is not printed as such.</li></ul> | <ul><li>Two rows from two bridges with different bar layouts on one line.</li><li>The across size is only implied by the bar base.</li><li>The sizes of the I-80 row are not settled from this sheet.</li></ul> |

## Script vs plan

- No value differs. All 31 sizes set by the two scripts (14 I-80 + 17 Ramp G), and the shared flags on 5WB, 5G and
  14G, agree with the printed pedestal totals and with the 5m1 / 5n1 bend-diagram bases.
- Wording of the script comments:
  - `piers-ramp-g.csx`, NOT-set note (around line 271): "9G (separate back / ahead pedestals **17"** apart in
    elevation". The plan pedestal tops differ by 0.91 to 0.92 ft (11"). The same script's 9G note around line 140 says
    "~0.9 ft", which matches the plan. The 17" figure is not found on the sheet.
  - `piers-i80.csx`, header: '"4, 5 EQ. SPA., 4"'. 3WB and 7WB are printed "4, 6 EQ., 4" (7 bars). The values
    (78 / 75) follow the 6-space layout correctly.

## UNVERIFIED

- 6WB 5m1 base: the diagram is not decoded. 5'-1 is derived from the list length 10'-7 minus the 2'-9 legs of 5n1.
  The pedestal size itself is printed (5'-6).
- 5G and 14G 5m1 base: not in the bend-diagram comparison. 4'-8 is derived from the list length 9'-2 minus 2 x 2'-3.
  The along size itself is printed (2'-6 | 2'-6).
- Ramp G across "5 SPA. @ 1'-0 = 5'-0;": the trailing glyph is not decoded. The 6'-0 total is confirmed by
  5n1 5'-8 + 4.
- 6A 4'-6 x 3'-10 (orientation and whether it applies to the I-80 row), the 11WB "5'-0" along candidate, and the 19G
  I-80 row size: these need a raster clip.
- No fraction needed raster confirmation. Every pedestal dimension used is a whole inch. When PyMuPDF is available, a
  clip of each cap plan's pedestal callout would close the items above.
