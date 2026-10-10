# Footing, cap and column reinforcing: plan vs model (I-80 WB Design 116 and Ramp G Design 316)

Scope: every as-designed pier (35: 15 I-80 WB piers incl. 6A and 11WB, 19 Ramp G piers) and the three abutments (West and East I-80, South Ramp G).
Method: read-only. Plan bar lists were read from the PDF text layer with PyMuPDF word positions (table x-range of each pier sheet), and
every pier reconciles EXACTLY to the printed "REINFORCING STEEL - TOTAL (LBS.)" of its tables (35 of 35 piers; abutments reconcile to
24,724+153 / 21,309+142 / 11,341+58 lb). Two rows lost to the text layer were rebuilt from the printed totals
(2WB 7f2, 5G/14G 11g2; derived lengths agree with weights to 1 lb). Model values were read from a COPY of
`samples\plans\I-80 WB and Ramp G.sbm` (`temp\ftg-rebar\model.sbm`) with `sbm run` scripts (`q1/q2/q3.csx`, outputs `q*.out`),
mark lists of every unit in `q2.out`, plan bar lists in `planbars2.csv`. Nothing in the project was edited.
Plan lb = printed bar-list weights (all bars of that group on the pier; multi-footing piers list the sum of all footings).
Model lb = `BarMark.TotalMass` in lb. Bar-list lengths carry no fractions (nearest inch); weights are authoritative.

## 1. Verdict

**Footing reinforcing is NOT accurate: the model carries 383,454 lb against 939,176 lb on the plans (-59%).** Every one of the 35 pier
footing sets is 33% to 73% light (worst: 11G/12G -73%, 7G -70%, 8G/10G -68%). Cap reinforcing is -63% (300,213 vs 801,020 lb) and
every pier is 56% to 69% light. Columns are the only element near the plans (-5% overall, per pier -27% to +19%), with known approximations.

Cause: neither the footings nor the caps were ever given as-designed reinforcing. The model file uses the DEFAULTS of
`SubstructureReinforcementDefinition` for all footing bars and all cap bars (the only rebar scripted from the plans is the column,
`samples\plans\column-rebar.csx`). The defaults are one size/spacing/cover for the whole footing, 7 + 7 cap bars, 2 skin bars per face and #5 stirrups at 6".

## 2. What the generator produces today (`SubstructureRebar.cs`)

Footing (`FootingBars`, line 709): for each footing with `SizeS > 0` (PileCap or SpreadFooting). Inputs: `FootingBarSize` (#8), `FootingBarSpacing` (12"), `FootingCover` (3"),
all on the unit's `SubstructureReinforcementDefinition` (one value per bent, no per-footing or per-mat control).
- Four layers, all the same bar and spacing: bottom mat (layer 1 along S at BottomZ + cover + db/2, layer 2 along N one bar higher), top mat (same, from TopZ down).
- Bar count `ceil(width/spacing)+1` evenly distributed over (dimension - 2 x cover); bar length = dimension - 2 x cover (3" each end); straight, no hooks; lap-spliced only if > MaxBarLength (never at these sizes).
- Marks F (bottom) and G (top), grouped by length/size (so F1/F2 = the two directions).
- NOT generated: side-face bars (vertical or horizontal), pile uplift anchors, hooks/bends, per-direction size/spacing, edge offsets, "clear above pile top" positioning, clipped/irregular outlines (11WB footing 3 "VARIES" bars), column dowels (the model column bar runs on into the footing; dowels/couplers "being reworked in another branch").
- The footing outline polygon (`FootingDefinition.Outline`) is used for the concrete but FootingBars only sees `SizeS x SizeN`.

Cap (`CapBars`, line 109): inputs `CapTopBarCount/Size` (7 x #11), `CapBottomBarCount/Size` (7 x #11), `CapSideBarsPerFace/Size` (2 x #5), `CapStirrupSize/Spacing` (#5 @ 6"), `CapCover` (2").
Top layer A follows the top line, bottom layer B follows the soffit relief (BENT), side bars C at equal heights, one closed stirrup (series S) per station at equal spaces; each stirrup has its own length (the cap depth varies) so the model has up to 75 stirrup marks per pier (1,420 in total) and the bar schedule is flooded with single-bar marks instead of the plans' few hoop marks.
NOT generated: bar groups with their own sizes/zones, second/third layers, cantilever bottom bars (b2/b3), hoops vs ties vs cantilever hoops (c1-c4), vertical hairpins (c5/c6), cap-transverse bars (c7), end bars (a13-a15), pedestal bars (m/n/hoop), cap steps (reinforcement of the stepped section), architectural relief steps.

Column: set from the plans by `column-rebar.csx`; see section 6.

## 3. Summary: plan lb vs model lb per pier

Footing = bar groups f/g incl. side bars and uplift anchors (8f6). Cap = a, b, c, m/n groups. Column = d verticals + footing-to-column dowels + e hoops/ties + plinth (flared base) groups
(model: column verticals + hoops + ties + flare bars/ties). Percent = (model - plan)/plan.
Footing verdict: OK < 5%, marginal < 15%, otherwise WRONG (even where a weight happened to be close the composition is wrong, see section 4). Cap verdict is WRONG for every pier (-56% to -69%).


| Pier | Footing plan lb | Footing model lb | diff | Cap plan lb (a-c, m-n) | Cap model lb | diff | Column plan lb (d, e, dowels, plinth) | Column model lb | diff | Footing verdict |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Pier 1WB | 19,727 | 9,484 | -52% | 35,824 | 12,357 | -66% | 25,686 | 18,722 | -27% | WRONG |
| Pier 2WB | 19,727 | 9,484 | -52% | 34,748 | 11,963 | -66% | 26,418 | 21,527 | -19% | WRONG |
| Pier 3WB | 33,046 | 15,844 | -52% | 34,856 | 11,963 | -66% | 55,020 | 52,080 | -5% | WRONG |
| Pier 4WB | 13,875 | 6,654 | -52% | 26,782 | 8,813 | -67% | 24,168 | 21,347 | -12% | WRONG |
| Pier 5WB | 15,012 | 7,017 | -53% | 21,990 | 8,746 | -60% | 27,291 | 22,776 | -17% | WRONG |
| Pier 10WB | 26,966 | 14,706 | -45% | 31,881 | 12,360 | -61% | 34,565 | 30,331 | -12% | WRONG |
| Pier 11WB | 19,819 | 10,653 | -46% | 30,979 | 12,439 | -60% | 38,993 | 32,524 | -17% | WRONG |
| Pier 6A | 16,477 | 6,320 | -62% | 23,054 | 7,674 | -67% | 16,405 | 12,983 | -21% | WRONG |
| Pier 6WB | 34,196 | 15,764 | -54% | 40,127 | 14,589 | -64% | 61,139 | 56,161 | -8% | WRONG |
| Pier 7WB | 50,143 | 18,960 | -62% | 41,610 | 12,883 | -69% | 75,263 | 74,124 | -2% | WRONG |
| Pier 8WB | 29,371 | 13,307 | -55% | 46,671 | 16,097 | -66% | 45,798 | 40,257 | -12% | WRONG |
| Pier 9WB | 26,966 | 14,706 | -45% | 34,648 | 13,410 | -61% | 32,892 | 28,596 | -13% | WRONG |
| Pier 12WB | 25,147 | 14,215 | -43% | 36,139 | 15,482 | -57% | 48,815 | 43,543 | -11% | WRONG |
| Pier 13WB | 54,311 | 23,485 | -57% | 33,636 | 12,755 | -62% | 66,505 | 64,471 | -3% | WRONG |
| Pier 14WB | 18,981 | 12,632 | -33% | 26,357 | 11,574 | -56% | 34,705 | 30,416 | -12% | WRONG |
| Pier 15WB | 18,981 | 12,632 | -33% | 25,787 | 11,311 | -56% | 25,057 | 20,337 | -19% | WRONG |
| Pier 1G | 12,510 | 5,070 | -59% | 13,284 | 5,243 | -61% | 14,265 | 12,028 | -16% | WRONG |
| Pier 2G | 26,771 | 9,836 | -63% | 13,294 | 5,243 | -61% | 35,583 | 35,235 | -1% | WRONG |
| Pier 3G | 13,883 | 5,500 | -60% | 13,280 | 5,251 | -60% | 21,376 | 19,805 | -7% | WRONG |
| Pier 4G | 21,527 | 7,991 | -63% | 14,333 | 5,251 | -63% | 24,186 | 24,189 | +0% | WRONG |
| Pier 5G | 17,067 | 7,569 | -56% | 12,338 | 5,251 | -57% | 25,056 | 24,014 | -4% | WRONG |
| Pier 6G | 21,527 | 7,991 | -63% | 14,333 | 5,251 | -63% | 25,715 | 25,742 | +0% | WRONG |
| Pier 7G | 35,931 | 10,707 | -70% | 13,297 | 5,251 | -61% | 46,099 | 54,928 | +19% | WRONG |
| Pier 8G | 29,169 | 9,230 | -68% | 13,280 | 5,251 | -60% | 36,295 | 39,818 | +10% | WRONG |
| Pier 9G | 20,820 | 8,154 | -61% | 12,698 | 5,251 | -59% | 33,404 | 29,424 | -12% | WRONG |
| Pier 10G | 29,169 | 9,230 | -68% | 14,333 | 5,251 | -63% | 35,355 | 38,661 | +9% | WRONG |
| Pier 11G | 72,038 | 19,288 | -73% | 14,350 | 5,251 | -63% | 54,713 | 63,279 | +16% | WRONG |
| Pier 12G | 72,038 | 19,288 | -73% | 14,350 | 5,251 | -63% | 57,263 | 66,009 | +15% | WRONG |
| Pier 13G | 21,527 | 7,991 | -63% | 14,333 | 5,251 | -63% | 24,936 | 24,845 | -0% | WRONG |
| Pier 14G | 19,840 | 7,481 | -62% | 12,338 | 5,251 | -57% | 24,811 | 22,217 | -10% | WRONG |
| Pier 15G | 13,883 | 5,500 | -60% | 13,283 | 5,249 | -60% | 20,978 | 19,393 | -8% | WRONG |
| Pier 16G | 16,174 | 7,711 | -52% | 26,315 | 8,813 | -67% | 22,024 | 21,258 | -3% | WRONG |
| Pier 17G | 16,174 | 7,711 | -52% | 16,894 | 7,039 | -58% | 19,356 | 18,696 | -3% | WRONG |
| Pier 18G | 44,646 | 14,634 | -67% | 13,301 | 5,251 | -61% | 26,094 | 25,463 | -2% | WRONG |
| Pier 19G | 11,737 | 6,707 | -43% | 16,297 | 5,945 | -64% | 14,723 | 11,158 | -24% | WRONG |
| **Total (35 piers)** | **939,176** | **383,454** | **-59%** | **801,020** | **300,213** | **-63%** | **1,200,952** | **1,146,352** | **-5%** | |



## 4. Footing: systematic differences (all 35 piers, 68 footings)

Totals over the 35 piers (lb): plan = top 191,037 + bottom 621,400 + side 104,311 + uplift 22,428 = 939,176; model = top 191,727 + bottom 191,727 (+ nothing else) = 383,454.

1. **Bottom mat is the big miss: plan 621,400 lb vs model 191,727 (-69%).** The plans use #11 (43 marks), #10 (18) and #14 (4: 11G, 12G, 18G) at 6" to 12" centres, e.g. 1WB 11g1/11g2 #11 @ 1'-0, 6A/7WB 11g @ 6", 5WB 10g1 @ 6", 11G/12G/18G 14g @ 7"-8" (#14 @ 7" = 3.9 in2/ft), 16G 10g1 @ 10". The model has #8 @ 12" (0.79 in2/ft): 20% to 52% of the plan steel area per foot.
2. **Top mat is right in total by coincidence, wrong in detail.** Plan top = #7 @ 1'-0 on 32 of 35 piers (#8 on 3WB and 18G; 11G/12G are #7 @ 6"). Model = #8 @ 12" with 3" end cover: heavier on small footings (1WB per footing 1,154 plan vs 1,581 model, +37%), much lighter on the big square footings (11G top 14,480 plan vs 9,644 model, -33%).
3. **Bottom bars have 90 degree hooks turned UP at both ends in the plans; the model bars are straight.** From the bar lists the extra length beyond the straight run (dimension - 4") is 3'-6 (#10), 3'-10 to 4'-0 (#11) and 5'-0 (#14), i.e. a leg of about 1'-9 / 1'-11 / 2'-6 per end (read on the 16G footing section p64: hook leg about 2'-2 high). Bend shape and bend diagram are missing in the model's bar schedule.
4. **Side bars missing: 104,311 lb in 29 of 35 piers** (none listed for 3WB, 9WB, 10WB, 14WB, 15WB, 19G). Per footing: 7f3 vertical bars, length = thickness - 5" (5'-1 for 5'-6), count about the perimeter at "ABT 1'-0 CTRS" (1WB 64, 16G 72, 18G 146 at 6'-7 for a 7'-0 footing); 7f4/7f5 horizontal side-face bars, 4 per face each way (length = dimension + about 10": 1WB 16'-10 and 18'-10).
5. **Pile uplift anchors 8f6 missing: 22,428 lb, all 35 piers** (#8 x 5'-0, 1WB 36 per footing, 16G 30, 18G 36; "SEE PILE UPLIFT ANCHOR DETAIL ... TYPICAL AT ALL CIRCLED PILES", sheets 19-20 etc.; the circled piles are marked on each pile layout). Bars-per-pile and welding/embedment are in the detail sheet and not yet decoded.
6. **Bottom mat clashes with the piles in 100% of the footings.** The model puts the first bottom layer at 3.5" above the footing bottom and the second at 4.5" (cover 3" + db/2); the pile heads are 12" above the bottom (`PileEmbedment`). Result: all 1,603 piles of all 68 pier footings are crossed by bottom bars (3,521 bar/pile crossings; `q3.out`). On the plans the bottom mat sits "1" CL." above the TOP OF PILE (about 13" above the footing bottom; 16G p64 section, 1'-0 pile embedment).
7. **Cover / edge offsets.** Plans: bars end 2" short of the face (bar length = dimension - 4", checked on every pier: top bar length + 4" = footing S or N), first bar 6" from the face (6" edge dimensions on every reinforcing layout), top clear 3" (as drawn), bottom: see 6. Model: 3" at the ends and all around, and a different bar count (`ceil(width/spacing)+1`, even distribution) instead of the plans' counts "n SPA @ 1'-0 (or ABT 6/7/8/9/10 in)". Footing outline sizes themselves agree with the plans on every pier (top bar length + 4" = model footing dimension).
8. **Irregular footings (11WB footing 3, 7f2 / 10g2 "VARIES", 7f7/7f8 side bars)** are not representable: `FootingBars` uses only `SizeS x SizeN`.
9. **Marks differ**: the model's F/G marks are by direction/length cluster; the plans' marks (7f1..7f5, 8f6, 10g1/2, 11g1/2, 14g1/2) carry size, group and top/bottom. A bar schedule/IFC cannot be matched to the plans.

Footing weights per pier are in the section 3 table; mat-level detail per footing follows in section 5.

## 5. Footing detail, plan vs model, every pier

"per footing" = plan total count / number of footings of the pier (all footings of a pier carry the same bars except 11WB footing 3, 6A single).
Model mark lists are per footing; the model lb column is the whole bottom (or top) mat of all footings of the pier, repeated on the first row of each mat.


#### Pier 1WB  (3 footings: 18.00x16.00x5.50 ft, 18 piles, 18.00x16.00x5.50 ft, 18 piles, 18.00x16.00x5.50 ft, 18 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 54 | 18 | 15'-8 | 1,729 | G1 #8 17x17'-6; G2 #8 19x15'-6 | 4,742 |
| Top mat | 7f2 | #7 | 48 | 16 | 17'-8 | 1,733 |  |  |
| Bottom mat | 11g1 | #11 | 54 | 18 | 19'-6 | 5,595 | F1 #8 17x17'-6; F2 #8 19x15'-6 | 4,742 |
| Bottom mat | 11g2 | #11 | 48 | 16 | 21'-6 | 5,483 |  |  |
| Side bars | 7f3 | #7 | 192 | 64 | 5'-1 | 1,995 | **none** | 0 |
| Side bars | 7f4 | #7 | 24 | 8 | 16'-10 | 826 |  |  |
| Side bars | 7f5 | #7 | 24 | 8 | 18'-10 | 924 |  |  |
| Pile uplift anchors | 8f6 | #8 | 108 | 36 | 5'-0 | 1,442 | **none** | 0 |

#### Pier 2WB  (3 footings: 18.00x16.00x5.50 ft, 18 piles, 18.00x16.00x5.50 ft, 18 piles, 18.00x16.00x5.50 ft, 18 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 54 | 18 | 15'-8 | 1,729 | G1 #8 17x17'-6; G2 #8 19x15'-6 | 4,742 |
| Top mat | 7f2 | #7 | 48 | 16 | 17'-8 | 1,733 |  |  |
| Bottom mat | 11g1 | #11 | 54 | 18 | 19'-6 | 5,595 | F1 #8 17x17'-6; F2 #8 19x15'-6 | 4,742 |
| Bottom mat | 11g2 | #11 | 48 | 16 | 21'-6 | 5,483 |  |  |
| Side bars | 7f3 | #7 | 192 | 64 | 5'-1 | 1,995 | **none** | 0 |
| Side bars | 7f4 | #7 | 24 | 8 | 16'-10 | 826 |  |  |
| Side bars | 7f5 | #7 | 24 | 8 | 18'-10 | 924 |  |  |
| Pile uplift anchors | 8f6 | #8 | 108 | 36 | 5'-0 | 1,442 | **none** | 0 |

#### Pier 3WB  (3 footings: 22.00x22.00x5.00 ft, 21 piles, 22.00x22.00x5.00 ft, 21 piles, 22.00x22.00x5.00 ft, 21 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 8f1 | #8 | 132 | 44 | 21'-8 | 7,636 | G #8 46x21'-6 | 7,922 |
| Bottom mat | 11g1 | #11 | 174 | 58 | 25'-8 | 23,728 | F #8 46x21'-6 | 7,922 |
| Pile uplift anchors | 8f6 | #8 | 126 | 42 | 5'-0 | 1,682 | **none** | 0 |

#### Pier 4WB  (2 footings: 14.50x21.50x6.00 ft, 24 piles, 14.50x21.50x6.00 ft, 24 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 30 | 15 | 21'-2 | 1,298 | G1 #8 15x21'-0; G2 #8 22x14'-0 | 3,327 |
| Top mat | 7f2 | #7 | 44 | 22 | 14'-2 | 1,274 |  |  |
| Bottom mat | 11g1 | #11 | 48 | 24 | 25'-0 | 6,376 | F1 #8 15x21'-0; F3 #8 22x14'-0 | 3,327 |
| Bottom mat | 7g2 | #7 | 44 | 22 | 16'-4 | 1,469 |  |  |
| Side bars | 7f3 | #7 | 140 | 70 | 5'-7 | 1,598 | **none** | 0 |
| Side bars | 7f4 | #7 | 20 | 10 | 22'-4 | 913 |  |  |
| Side bars | 7f5 | #7 | 20 | 10 | 15'-4 | 627 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 12 | 5'-0 | 320 | **none** | 0 |

#### Pier 5WB  (2 footings: 16.00x20.00x5.50 ft, 16 piles, 16.00x20.00x5.50 ft, 16 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 32 | 16 | 19'-8 | 1,286 | G1 #8 17x19'-6; G2 #8 21x15'-6 | 3,508 |
| Top mat | 7f2 | #7 | 40 | 20 | 15'-8 | 1,281 |  |  |
| Bottom mat | 10g1 | #10 | 62 | 31 | 23'-2 | 6,181 | F1 #8 17x19'-6; F3 #8 21x15'-6 | 3,508 |
| Bottom mat | 10g2 | #10 | 40 | 20 | 19'-2 | 3,299 |  |  |
| Side bars | 7f3 | #7 | 136 | 68 | 5'-1 | 1,413 | **none** | 0 |
| Side bars | 7f4 | #7 | 16 | 8 | 20'-10 | 681 |  |  |
| Side bars | 7f5 | #7 | 16 | 8 | 16'-10 | 551 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 12 | 5'-0 | 320 | **none** | 0 |

#### Pier 10WB  (3 footings: 26.00x17.50x5.00 ft, 18 piles, 26.00x17.50x5.00 ft, 18 piles, 26.00x17.50x5.00 ft, 18 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 78 | 26 | 17'-2 | 2,737 | G1 #8 18x25'-6; G2 #8 27x17'-0 | 7,353 |
| Top mat | 7f2 | #7 | 69 | 23 | 25'-8 | 3,620 |  |  |
| Bottom mat | 11g1 | #11 | 78 | 26 | 21'-2 | 8,772 | F1 #8 18x25'-6; F2 #8 27x17'-0 | 7,353 |
| Bottom mat | 11g2 | #11 | 69 | 23 | 29'-8 | 10,876 |  |  |
| Pile uplift anchors | 8f6 | #8 | 72 | 24 | 5'-0 | 961 | **none** | 0 |

#### Pier 11WB  (3 footings: 18.00x18.00x6.50 ft, 26 piles, 18.00x18.00x6.50 ft, 26 piles, 18.00x18.00x6.50 ft, 29 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 80 | 26.6667 | 17'-8 | 2,889 | G #8 38x17'-6 | 5,327 |
| Top mat | 7f2 | #7 | 28 | 9.33333 | VARIES | 820 |  |  |
| Bottom mat | 10g1 | #10 | 94 | 31.3333 | 21'-2 | 8,562 | F2 #8 38x17'-6 | 5,327 |
| Bottom mat | 10g2 | #10 | 32 | 10.6667 | VARIES | 2,456 |  |  |
| Side bars | 7f3 | #7 | 196 | 65.3333 | 6'-1 | 2,437 | **none** | 0 |
| Side bars | 7f4 | #7 | 40 | 13.3333 | 18'-10 | 1,540 |  |  |
| Side bars | 7f5 | #7 | 10 | 3.33333 | 12'-2 | 249 |  |  |
| Side bars | 7f7 | #7 | 10 | 3.33333 | 10'-7 | 216 |  |  |
| Side bars | 7f8 | #7 | 10 | 3.33333 | 12'-2 | 249 |  |  |
| Pile uplift anchors | 8f6 | #8 | 30 | 10 | 5'-0 | 401 | **none** | 0 |

#### Pier 6A  (1 footing: 29.00x20.00x5.50 ft, 24 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 29 | 29 | 19'-8 | 1,166 | G1 #8 21x28'-6; G2 #8 30x19'-6 | 3,160 |
| Top mat | 7f2 | #7 | 27 | 27 | 28'-8 | 1,582 |  |  |
| Bottom mat | 11g1 | #11 | 39 | 39 | 23'-6 | 4,869 | F1 #8 21x28'-6; F2 #8 30x19'-6 | 3,160 |
| Bottom mat | 11g2 | #11 | 39 | 39 | 32'-6 | 6,734 |  |  |
| Side bars | 7f3 | #7 | 94 | 94 | 5'-1 | 977 | **none** | 0 |
| Side bars | 7f4 | #7 | 8 | 8 | 20'-10 | 341 |  |  |
| Side bars | 7f5 | #7 | 8 | 8 | 29'-10 | 488 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 6WB  (4 footings: 18.00x20.00x6.00 ft, 21 piles, 18.00x20.00x6.00 ft, 21 piles, 18.00x20.00x6.00 ft, 21 piles, 18.00x20.00x6.00 ft, 21 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 72 | 18 | 19'-8 | 2,894 | G1 #8 19x19'-6; G2 #8 21x17'-6 | 7,882 |
| Top mat | 7f2 | #7 | 80 | 20 | 17'-8 | 2,889 |  |  |
| Bottom mat | 10g1 | #10 | 140 | 35 | 23'-2 | 13,956 | F2 #8 19x19'-6; F3 #8 21x17'-6 | 7,882 |
| Bottom mat | 10g2 | #10 | 80 | 20 | 21'-2 | 7,286 |  |  |
| Side bars | 7f3 | #7 | 288 | 72 | 5'-7 | 3,287 | **none** | 0 |
| Side bars | 7f4 | #7 | 40 | 10 | 20'-10 | 1,703 |  |  |
| Side bars | 7f5 | #7 | 40 | 10 | 18'-10 | 1,540 |  |  |
| Pile uplift anchors | 8f6 | #8 | 48 | 12 | 5'-0 | 641 | **none** | 0 |

#### Pier 7WB  (3 footings: 20.00x29.00x7.00 ft, 26 piles, 20.00x29.00x7.00 ft, 26 piles, 20.00x29.00x7.00 ft, 26 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 117 | 39 | 28'-8 | 6,856 | G1 #8 21x28'-6; G2 #8 30x19'-6 | 9,480 |
| Top mat | 7f2 | #7 | 87 | 29 | 19'-8 | 3,497 |  |  |
| Bottom mat | 11g1 | #11 | 117 | 39 | 32'-6 | 20,203 | F1 #8 21x28'-6; F2 #8 30x19'-6 | 9,480 |
| Bottom mat | 11g2 | #11 | 87 | 29 | 23'-6 | 10,862 |  |  |
| Side bars | 7f3 | #7 | 282 | 94 | 6'-7 | 3,795 | **none** | 0 |
| Side bars | 7f4 | #7 | 36 | 12 | 29'-10 | 2,195 |  |  |
| Side bars | 7f5 | #7 | 36 | 12 | 20'-10 | 1,533 |  |  |
| Pile uplift anchors | 8f6 | #8 | 90 | 30 | 5'-0 | 1,202 | **none** | 0 |

#### Pier 8WB  (4 footings: 21.50x14.50x6.00 ft, 18 piles, 21.50x14.50x6.00 ft, 18 piles, 21.50x14.50x6.00 ft, 18 piles, 21.50x14.50x6.00 ft, 18 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 88 | 22 | 14'-2 | 2,548 | G1 #8 15x21'-0; G2 #8 22x14'-0 | 6,654 |
| Top mat | 7f2 | #7 | 60 | 15 | 21'-2 | 2,596 |  |  |
| Bottom mat | 10g1 | #10 | 88 | 22 | 17'-8 | 6,690 | F1 #8 15x21'-0; F3 #8 22x14'-0 | 6,654 |
| Bottom mat | 10g2 | #10 | 88 | 22 | 24'-8 | 9,340 |  |  |
| Side bars | 7f3 | #7 | 280 | 70 | 5'-7 | 3,195 | **none** | 0 |
| Side bars | 7f4 | #7 | 40 | 10 | 15'-4 | 1,254 |  |  |
| Side bars | 7f5 | #7 | 40 | 10 | 22'-4 | 1,826 |  |  |
| Pile uplift anchors | 8f6 | #8 | 144 | 36 | 5'-0 | 1,922 | **none** | 0 |

#### Pier 9WB  (3 footings: 26.00x17.50x5.00 ft, 18 piles, 26.00x17.50x5.00 ft, 18 piles, 26.00x17.50x5.00 ft, 18 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 78 | 26 | 17'-2 | 2,737 | G1 #8 18x25'-6; G2 #8 27x17'-0 | 7,353 |
| Top mat | 7f2 | #7 | 69 | 23 | 25'-8 | 3,620 |  |  |
| Bottom mat | 11g1 | #11 | 78 | 26 | 21'-2 | 8,772 | F1 #8 18x25'-6; F2 #8 27x17'-0 | 7,353 |
| Bottom mat | 11g2 | #11 | 69 | 23 | 29'-8 | 10,876 |  |  |
| Pile uplift anchors | 8f6 | #8 | 72 | 24 | 5'-0 | 961 | **none** | 0 |

#### Pier 12WB  (4 footings: 16.00x20.50x5.50 ft, 16 piles, 16.00x20.50x5.50 ft, 16 piles, 16.00x20.50x5.50 ft, 16 piles, 16.00x20.50x5.50 ft, 16 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 64 | 16 | 20'-2 | 2,638 | G1 #8 17x20'-0; G2 #8 21x15'-6 | 7,108 |
| Top mat | 7f2 | #7 | 84 | 21 | 15'-8 | 2,690 |  |  |
| Bottom mat | 11g1 | #11 | 84 | 21 | 24'-0 | 10,711 | F1 #8 17x20'-0; F3 #8 21x15'-6 | 7,108 |
| Bottom mat | 7g2 | #7 | 84 | 21 | 17'-10 | 3,062 |  |  |
| Side bars | 7f3 | #7 | 280 | 70 | 5'-1 | 2,909 | **none** | 0 |
| Side bars | 7f4 | #7 | 32 | 8 | 21'-4 | 1,395 |  |  |
| Side bars | 7f5 | #7 | 32 | 8 | 16'-10 | 1,101 |  |  |
| Pile uplift anchors | 8f6 | #8 | 48 | 12 | 5'-0 | 641 | **none** | 0 |

#### Pier 13WB  (3 footings: 24.00x30.00x6.00 ft, 20 piles, 24.00x30.00x6.00 ft, 20 piles, 24.00x30.00x6.00 ft, 20 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 123 | 41 | 29'-8 | 7,459 | G1 #8 25x29'-6; G2 #8 31x23'-6 | 11,743 |
| Top mat | 7f2 | #7 | 90 | 30 | 23'-8 | 4,354 |  |  |
| Bottom mat | 11g1 | #11 | 123 | 41 | 33'-6 | 21,892 | F1 #8 25x29'-6; F2 #8 31x23'-6 | 11,743 |
| Bottom mat | 11g2 | #11 | 90 | 30 | 27'-6 | 13,150 |  |  |
| Side bars | 7f3 | #7 | 312 | 104 | 5'-7 | 3,561 | **none** | 0 |
| Side bars | 7f4 | #7 | 30 | 10 | 30'-10 | 1,891 |  |  |
| Side bars | 7f5 | #7 | 30 | 10 | 24'-10 | 1,523 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 12 | 5'-0 | 481 | **none** | 0 |

#### Pier 14WB  (3 footings: 19.00x20.50x5.00 ft, 16 piles, 19.00x20.50x5.00 ft, 16 piles, 19.00x20.50x5.00 ft, 16 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 57 | 19 | 20'-2 | 2,350 | G1 #8 20x20'-0; G2 #8 21x18'-6 | 6,316 |
| Top mat | 7f2 | #7 | 63 | 21 | 18'-8 | 2,404 |  |  |
| Bottom mat | 10g1 | #10 | 75 | 25 | 23'-10 | 7,692 | F1 #8 20x20'-0; F2 #8 21x18'-6 | 6,316 |
| Bottom mat | 10g2 | #10 | 63 | 21 | 22'-4 | 6,054 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 12 | 5'-0 | 481 | **none** | 0 |

#### Pier 15WB  (3 footings: 19.00x20.50x5.00 ft, 16 piles, 19.00x20.50x5.00 ft, 16 piles, 19.00x20.50x5.00 ft, 16 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 57 | 19 | 20'-2 | 2,350 | G1 #8 20x20'-0; G2 #8 21x18'-6 | 6,316 |
| Top mat | 7f2 | #7 | 63 | 21 | 18'-8 | 2,404 |  |  |
| Bottom mat | 10g1 | #10 | 75 | 25 | 23'-10 | 7,692 | F1 #8 20x20'-0; F2 #8 21x18'-6 | 6,316 |
| Bottom mat | 10g2 | #10 | 63 | 21 | 22'-4 | 6,054 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 12 | 5'-0 | 481 | **none** | 0 |

#### Pier 1G  (1 footing: 29.00x16.00x5.50 ft, 24 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 29 | 29 | 15'-8 | 929 | G1 #8 17x28'-6; G2 #8 30x15'-6 | 2,535 |
| Top mat | 7f2 | #7 | 24 | 24 | 28'-8 | 1,406 |  |  |
| Bottom mat | 11g1 | #11 | 29 | 29 | 19'-6 | 3,005 | F1 #8 17x28'-6; F2 #8 30x15'-6 | 2,535 |
| Bottom mat | 11g2 | #11 | 31 | 31 | 32'-6 | 5,353 |  |  |
| Side bars | 7f3 | #7 | 86 | 86 | 5'-1 | 894 | **none** | 0 |
| Side bars | 7f4 | #7 | 8 | 8 | 16'-10 | 275 |  |  |
| Side bars | 7f5 | #7 | 8 | 8 | 29'-10 | 488 |  |  |
| Pile uplift anchors | 8f6 | #8 | 12 | 12 | 5'-0 | 160 | **none** | 0 |

#### Pier 2G  (1 footing: 29.00x31.50x7.00 ft, 32 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 57 | 57 | 31'-2 | 3,631 | G1 #8 30x31'-0; G2 #8 32x28'-6 | 4,918 |
| Top mat | 7f2 | #7 | 42 | 42 | 28'-8 | 2,461 |  |  |
| Bottom mat | 10g1 | #10 | 57 | 57 | 34'-8 | 8,503 | F1 #8 30x31'-0; F2 #8 32x28'-6 | 4,918 |
| Bottom mat | 10g2 | #10 | 62 | 62 | 32'-2 | 8,582 |  |  |
| Side bars | 7f3 | #7 | 118 | 118 | 6'-7 | 1,588 | **none** | 0 |
| Side bars | 7f4 | #7 | 12 | 12 | 32'-4 | 793 |  |  |
| Side bars | 7f5 | #7 | 12 | 12 | 29'-10 | 732 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 36 | 5'-0 | 481 | **none** | 0 |

#### Pier 3G  (1 footing: 28.00x18.00x6.00 ft, 30 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 28 | 28 | 17'-8 | 1,011 | G1 #8 19x27'-6; G2 #8 29x17'-6 | 2,750 |
| Top mat | 7f2 | #7 | 24 | 24 | 27'-8 | 1,357 |  |  |
| Bottom mat | 11g1 | #11 | 28 | 28 | 21'-6 | 3,198 | F1 #8 19x27'-6; F3 #8 29x17'-6 | 2,750 |
| Bottom mat | 11g2 | #11 | 35 | 35 | 31'-6 | 5,858 |  |  |
| Side bars | 7f3 | #7 | 88 | 88 | 5'-7 | 1,004 | **none** | 0 |
| Side bars | 7f4 | #7 | 10 | 10 | 18'-10 | 385 |  |  |
| Side bars | 7f5 | #7 | 10 | 10 | 28'-10 | 589 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 36 | 5'-0 | 481 | **none** | 0 |

#### Pier 4G  (1 footing: 28.00x26.50x6.00 ft, 32 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 42 | 42 | 26'-2 | 2,246 | G1 #8 27x27'-6; G2 #8 29x26'-0 | 3,996 |
| Top mat | 7f2 | #7 | 35 | 35 | 27'-8 | 1,979 |  |  |
| Bottom mat | 11g1 | #11 | 55 | 55 | 30'-0 | 8,766 | F1 #8 27x27'-6; F2 #8 29x26'-0 | 3,996 |
| Bottom mat | 11g2 | #11 | 35 | 35 | 31'-6 | 5,858 |  |  |
| Side bars | 7f3 | #7 | 106 | 106 | 5'-7 | 1,210 | **none** | 0 |
| Side bars | 7f4 | #7 | 10 | 10 | 27'-4 | 559 |  |  |
| Side bars | 7f5 | #7 | 10 | 10 | 28'-10 | 589 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 5G  (1 footing: 29.00x24.00x7.00 ft, 30 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 29 | 29 | 23'-8 | 1,403 | G1 #8 25x28'-6; G2 #8 30x23'-6 | 3,785 |
| Top mat | 7f2 | #7 | 24 | 24 | 28'-8 | 1,406 |  |  |
| Bottom mat | 11g1 | #11 | 39 | 39 | 27'-6 | 5,698 | F1 #8 25x28'-6; F2 #8 30x23'-6 | 3,785 |
| Bottom mat | 11g2 | #11 | 32 | 32 | 32'-6 | 5,526 |  |  |
| Side bars | 7f3 | #7 | 102 | 102 | 6'-7 | 1,373 | **none** | 0 |
| Side bars | 7f4 | #7 | 12 | 12 | 24'-10 | 609 |  |  |
| Side bars | 7f5 | #7 | 12 | 12 | 29'-10 | 732 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 6G  (1 footing: 28.00x26.50x6.00 ft, 32 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 42 | 42 | 26'-2 | 2,246 | G1 #8 27x27'-6; G2 #8 29x26'-0 | 3,996 |
| Top mat | 7f2 | #7 | 35 | 35 | 27'-8 | 1,979 |  |  |
| Bottom mat | 11g1 | #11 | 55 | 55 | 30'-0 | 8,766 | F1 #8 27x27'-6; F2 #8 29x26'-0 | 3,996 |
| Bottom mat | 11g2 | #11 | 35 | 35 | 31'-6 | 5,858 |  |  |
| Side bars | 7f3 | #7 | 106 | 106 | 5'-7 | 1,210 | **none** | 0 |
| Side bars | 7f4 | #7 | 10 | 10 | 27'-4 | 559 |  |  |
| Side bars | 7f5 | #7 | 10 | 10 | 28'-10 | 589 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 7G  (1 footing: 35.50x28.00x8.00 ft, 49 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 70 | 70 | 27'-8 | 3,959 | G1 #8 29x35'-0; G2 #8 36x27'-6 | 5,353 |
| Top mat | 7f2 | #7 | 55 | 55 | 35'-2 | 3,953 |  |  |
| Bottom mat | 11g1 | #11 | 70 | 70 | 31'-6 | 11,715 | F1 #8 29x35'-0; F2 #8 36x27'-6 | 5,353 |
| Bottom mat | 11g2 | #11 | 55 | 55 | 39'-0 | 11,396 |  |  |
| Side bars | 7f3 | #7 | 124 | 124 | 7'-7 | 1,922 | **none** | 0 |
| Side bars | 7f4 | #7 | 14 | 14 | 28'-10 | 825 |  |  |
| Side bars | 7f5 | #7 | 14 | 14 | 36'-4 | 1,040 |  |  |
| Pile uplift anchors | 8f6 | #8 | 84 | 84 | 5'-0 | 1,121 | **none** | 0 |

#### Pier 8G  (1 footing: 34.00x25.00x7.50 ft, 45 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 45 | 45 | 24'-8 | 2,269 | G1 #8 26x33'-6; G2 #8 35x24'-6 | 4,615 |
| Top mat | 7f2 | #7 | 49 | 49 | 33'-8 | 3,372 |  |  |
| Bottom mat | 11g1 | #11 | 67 | 67 | 28'-6 | 10,145 | F1 #8 26x33'-6; F3 #8 35x24'-6 | 4,615 |
| Bottom mat | 11g2 | #11 | 49 | 49 | 37'-6 | 9,763 |  |  |
| Side bars | 7f3 | #7 | 114 | 114 | 7'-1 | 1,651 | **none** | 0 |
| Side bars | 7f4 | #7 | 12 | 12 | 25'-10 | 634 |  |  |
| Side bars | 7f5 | #7 | 12 | 12 | 34'-10 | 854 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 36 | 5'-0 | 481 | **none** | 0 |

#### Pier 9G  (1 footing: 31.50x24.00x6.00 ft, 28 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 42 | 42 | 23'-8 | 2,032 | G1 #8 25x31'-0; G2 #8 32x23'-6 | 4,077 |
| Top mat | 7f2 | #7 | 47 | 47 | 31'-2 | 2,994 |  |  |
| Bottom mat | 11g1 | #11 | 42 | 42 | 27'-6 | 6,137 | F1 #8 25x31'-0; F3 #8 32x23'-6 | 4,077 |
| Bottom mat | 11g2 | #11 | 36 | 36 | 35'-0 | 6,694 |  |  |
| Side bars | 7f3 | #7 | 108 | 108 | 5'-7 | 1,233 | **none** | 0 |
| Side bars | 7f4 | #7 | 10 | 10 | 24'-10 | 508 |  |  |
| Side bars | 7f5 | #7 | 10 | 10 | 32'-4 | 661 |  |  |
| Pile uplift anchors | 8f6 | #8 | 42 | 42 | 5'-0 | 561 | **none** | 0 |

#### Pier 10G  (1 footing: 34.00x25.00x7.50 ft, 45 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 45 | 45 | 24'-8 | 2,269 | G1 #8 26x33'-6; G2 #8 35x24'-6 | 4,615 |
| Top mat | 7f2 | #7 | 49 | 49 | 33'-8 | 3,372 |  |  |
| Bottom mat | 11g1 | #11 | 67 | 67 | 28'-6 | 10,145 | F1 #8 26x33'-6; F3 #8 35x24'-6 | 4,615 |
| Bottom mat | 11g2 | #11 | 49 | 49 | 37'-6 | 9,763 |  |  |
| Side bars | 7f3 | #7 | 114 | 114 | 7'-1 | 1,651 | **none** | 0 |
| Side bars | 7f4 | #7 | 12 | 12 | 25'-10 | 634 |  |  |
| Side bars | 7f5 | #7 | 12 | 12 | 34'-10 | 854 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 36 | 5'-0 | 481 | **none** | 0 |

#### Pier 11G  (1 footing: 42.50x42.50x7.50 ft, 57 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 168 | 168 | 42'-2 | 14,480 | G #8 86x42'-0 | 9,644 |
| Bottom mat | 14g1 | #14 | 146 | 146 | 47'-2 | 52,680 | F1 #8 86x42'-0 | 9,644 |
| Side bars | 7f3 | #7 | 168 | 168 | 7'-1 | 2,432 | **none** | 0 |
| Side bars | 7f4 | #7 | 24 | 24 | 43'-4 | 2,126 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 12G  (1 footing: 42.50x42.50x7.50 ft, 57 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 168 | 168 | 42'-2 | 14,480 | G #8 86x42'-0 | 9,644 |
| Bottom mat | 14g1 | #14 | 146 | 146 | 47'-2 | 52,680 | F1 #8 86x42'-0 | 9,644 |
| Side bars | 7f3 | #7 | 168 | 168 | 7'-1 | 2,432 | **none** | 0 |
| Side bars | 7f4 | #7 | 24 | 24 | 43'-4 | 2,126 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 13G  (1 footing: 28.00x26.50x6.00 ft, 32 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 42 | 42 | 26'-2 | 2,246 | G1 #8 27x27'-6; G2 #8 29x26'-0 | 3,996 |
| Top mat | 7f2 | #7 | 35 | 35 | 27'-8 | 1,979 |  |  |
| Bottom mat | 11g1 | #11 | 55 | 55 | 30'-0 | 8,766 | F1 #8 27x27'-6; F2 #8 29x26'-0 | 3,996 |
| Bottom mat | 11g2 | #11 | 35 | 35 | 31'-6 | 5,858 |  |  |
| Side bars | 7f3 | #7 | 106 | 106 | 5'-7 | 1,210 | **none** | 0 |
| Side bars | 7f4 | #7 | 10 | 10 | 27'-4 | 559 |  |  |
| Side bars | 7f5 | #7 | 10 | 10 | 28'-10 | 589 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 14G  (1 footing: 31.50x22.00x6.00 ft, 28 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 32 | 32 | 21'-8 | 1,417 | G1 #8 23x31'-0; G2 #8 32x21'-6 | 3,741 |
| Top mat | 7f2 | #7 | 33 | 33 | 31'-2 | 2,102 |  |  |
| Bottom mat | 11g1 | #11 | 42 | 42 | 25'-6 | 5,690 | F1 #8 23x31'-0; F2 #8 32x21'-6 | 3,741 |
| Bottom mat | 11g2 | #11 | 43 | 43 | 35'-0 | 7,996 |  |  |
| Side bars | 7f3 | #7 | 104 | 104 | 5'-7 | 1,187 | **none** | 0 |
| Side bars | 7f4 | #7 | 10 | 10 | 22'-10 | 467 |  |  |
| Side bars | 7f5 | #7 | 10 | 10 | 32'-4 | 661 |  |  |
| Pile uplift anchors | 8f6 | #8 | 24 | 24 | 5'-0 | 320 | **none** | 0 |

#### Pier 15G  (1 footing: 28.00x18.00x6.00 ft, 30 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 28 | 28 | 17'-8 | 1,011 | G1 #8 19x27'-6; G2 #8 29x17'-6 | 2,750 |
| Top mat | 7f2 | #7 | 24 | 24 | 27'-8 | 1,357 |  |  |
| Bottom mat | 11g1 | #11 | 28 | 28 | 21'-6 | 3,198 | F1 #8 19x27'-6; F3 #8 29x17'-6 | 2,750 |
| Bottom mat | 11g2 | #11 | 35 | 35 | 31'-6 | 5,858 |  |  |
| Side bars | 7f3 | #7 | 88 | 88 | 5'-7 | 1,004 | **none** | 0 |
| Side bars | 7f4 | #7 | 10 | 10 | 18'-10 | 385 |  |  |
| Side bars | 7f5 | #7 | 10 | 10 | 28'-10 | 589 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 36 | 5'-0 | 481 | **none** | 0 |

#### Pier 16G  (2 footings: 22.00x16.00x5.50 ft, 20 piles, 22.00x16.00x5.50 ft, 20 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 44 | 22 | 15'-8 | 1,409 | G1 #8 17x21'-6; G2 #8 23x15'-6 | 3,856 |
| Top mat | 7f2 | #7 | 38 | 19 | 21'-8 | 1,683 |  |  |
| Bottom mat | 10g1 | #10 | 52 | 26 | 19'-2 | 4,289 | F1 #8 17x21'-6; F2 #8 23x15'-6 | 3,856 |
| Bottom mat | 10g2 | #10 | 48 | 24 | 25'-2 | 5,198 |  |  |
| Side bars | 7f3 | #7 | 144 | 72 | 5'-1 | 1,496 | **none** | 0 |
| Side bars | 7f4 | #7 | 16 | 8 | 16'-10 | 551 |  |  |
| Side bars | 7f5 | #7 | 16 | 8 | 22'-10 | 747 |  |  |
| Pile uplift anchors | 8f6 | #8 | 60 | 30 | 5'-0 | 801 | **none** | 0 |

#### Pier 17G  (2 footings: 22.00x16.00x5.50 ft, 20 piles, 22.00x16.00x5.50 ft, 20 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 44 | 22 | 15'-8 | 1,409 | G1 #8 17x21'-6; G2 #8 23x15'-6 | 3,856 |
| Top mat | 7f2 | #7 | 38 | 19 | 21'-8 | 1,683 |  |  |
| Bottom mat | 10g1 | #10 | 52 | 26 | 19'-2 | 4,289 | F1 #8 17x21'-6; F2 #8 23x15'-6 | 3,856 |
| Bottom mat | 10g2 | #10 | 48 | 24 | 25'-2 | 5,198 |  |  |
| Side bars | 7f3 | #7 | 144 | 72 | 5'-1 | 1,496 | **none** | 0 |
| Side bars | 7f4 | #7 | 16 | 8 | 16'-10 | 551 |  |  |
| Side bars | 7f5 | #7 | 16 | 8 | 22'-10 | 747 |  |  |
| Pile uplift anchors | 8f6 | #8 | 60 | 30 | 5'-0 | 801 | **none** | 0 |

#### Pier 18G  (1 footing: 32.00x42.50x7.00 ft, 34 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 8f1 | #8 | 55 | 55 | 42'-2 | 6,192 | G1 #8 33x42'-0; G2 #8 43x31'-6 | 7,317 |
| Top mat | 8f2 | #8 | 43 | 43 | 31'-8 | 3,636 |  |  |
| Bottom mat | 14g1 | #14 | 48 | 48 | 47'-2 | 17,320 | F1 #8 33x42'-0; F2 #8 43x31'-6 | 7,317 |
| Bottom mat | 14g2 | #14 | 47 | 47 | 36'-8 | 13,184 |  |  |
| Side bars | 7f3 | #7 | 146 | 146 | 6'-7 | 1,965 | **none** | 0 |
| Side bars | 7f4 | #7 | 12 | 12 | 43'-4 | 1,063 |  |  |
| Side bars | 7f5 | #7 | 12 | 12 | 32'-10 | 805 |  |  |
| Pile uplift anchors | 8f6 | #8 | 36 | 36 | 5'-0 | 481 | **none** | 0 |

#### Pier 19G  (1 footing: 28.00x22.00x5.00 ft, 20 piles)

| Group | Plan mark | Size | Plan total count | per footing | Length | Plan lb (all) | Model mark(s) per footing | Model lb (all footings, whole mat) |
|---|---|---|---:|---:|---|---:|---|---:|
| Top mat | 7f1 | #7 | 28 | 28 | 21'-8 | 1,240 | G1 #8 23x27'-6; G2 #8 29x21'-6 | 3,354 |
| Top mat | 7f2 | #7 | 29 | 29 | 27'-8 | 1,640 |  |  |
| Bottom mat | 11g1 | #11 | 28 | 28 | 25'-8 | 3,818 | F1 #8 23x27'-6; F2 #8 29x21'-6 | 3,354 |
| Bottom mat | 11g2 | #11 | 29 | 29 | 31'-8 | 4,879 |  |  |
| Pile uplift anchors | 8f6 | #8 | 12 | 12 | 5'-0 | 160 | **none** | 0 |


## 6. Footing: generator gap list and proposed inputs

Can the current generator reproduce the plan footing reinforcing? **No.** It needs code, not only data. Gap list:

| # | Gap | Where | Needed |
|---|---|---|---|
| F1 | One size/spacing/cover for all layers and all footings of a bent | `FootingBars`, `SubstructureReinforcementDefinition.FootingBarSize/Spacing/Cover` | per footing, per mat/direction bar groups |
| F2 | No hooks/bends on mat bars | `FootingBars` emits `new[]{a,b}` straight runs | bent bar shapes (`BarShape` with 90 degree legs, bend diagram dims) for bottom bars |
| F3 | Bottom mat inside pile embedment | z = BottomZ + cover + db/2 | position "clear above pile top" (or elevation above bottom), second layer stacked on the first |
| F4 | Bar count by spacing only, centred layout; no edge offset or explicit count | `nb = ceil(width/sp)+1` | count + first-bar offset (6") or spacing "ABT n" + end clear (2") |
| F5 | No side bars (vertical, horizontal) | not generated | new `FootingSideBars` |
| F6 | No pile uplift anchors | not generated | new `PileUpliftAnchors` at chosen piles |
| F7 | Irregular footing outlines (11WB footing 3 "VARIES" bars, clipped corners) | uses SizeS x SizeN | clip bars to `FootingDefinition.Outline` with the same offsets |
| F8 | Marks | F/G by length cluster | plan mark names/prefix per group (`7f1`, `11g2`) as an optional label |
| F9 | Column dowels not modeled (317,351 lb in the plans, + plinth dowels) | column V passes into the footing | coordinate with the dowel/coupler rework branch |

Proposed inputs (new `FootingReinforcementDefinition` on `FootingDefinition`, `Column` = 0 meaning every footing of the pier; falls back to today's defaults when absent so existing files are unchanged):

```
FootingReinforcement
  Mats[]                     one entry per plan bar group (7f1, 7f2, 11g1, 11g2 ...)
    Layer           Top | Bottom
    RunAlong        S | N                      (the plan's longitudinal/transverse)
    Order           1 = outer (next to the face), 2 = next layer in
    Size            "#7" ... "#14"
    Count           18                         (or Spacing 1'-0 with Count derived)
    Spacing         1'-0                       (documentation + derivation)
    FirstOffset     6"                         (centreline of first bar from the face, each side)
    EndClear        2"                         (bar length = dimension - 2 x EndClear)
    PositionBy      ClearBelowTop 3"  |  ClearAbovePileTop 1"  |  ClearAboveBottom x
    Hook            None | Up | Down, Ends = Both | Start | End, Leg (out-to-out, e.g. 2'-2), BendDia
    Mark            "11g1" (optional label for the schedule)
  SideBars
    Vertical   { Size #7, Spacing 1'-0 (or Count 64), Clear 2" or explicit length, Offset from face }
    Horizontal { Size #7, PerFace 4 (S faces) and 4 (N faces), Elevations or equal spacing, Extension 10" }
  UpliftAnchors { Size #8, Length 5'-0, Piles [indices of the circled piles], BarsPerPile (from the detail sheet), Embed above/below pile top }
```
The data are all on the plan sheets (bar lists above) except the uplift detail (see section 10). Once F1-F7 exist, a plan-set script (like `column-rebar.csx`) can fill them from this document's tables and the totals can be asserted to match the printed totals.



## 7. Pier caps

### 7.1 Totals by group (35 piers, lb)

| Group | Plan | Model | Diff |
|---|---:|---:|---:|
| Top longitudinal (a1/a2: 14 to 60 bars #9-#11 in two staggered lengths) | 261,334 | 82,295 (7 x #11 per pier) | -69% |
| Bottom longitudinal incl. cantilever bottom bars (b1, b2, b3) | 141,416 | 86,903 (7 x #11 per pier) | -39% |
| Side / skin bars (a3-a12, "E.F.", follow the relief) | 80,969 | 9,148 (2 x #5 per face) | -89% |
| Hoops, cantilever hoops, ties, cantilever ties (c1-c4) | 230,554 | 121,867 (#5 @ 6", one mark per bar) | -47% |
| Vertical hairpins (c5/c6) | 40,427 | none | -100% |
| End bars (a13-a15) | 8,951 | none | -100% |
| Cap transverse (c7, #4 @ 2'-0) | 8,598 | none | -100% |
| Pedestal bars (m/n and pedestal hoop) | 26,892 | none | -100% |
| Other (cap step etc.) | 1,879 | none | -100% |
| **Total** | **801,020** | **300,213** | **-63%** |

### 7.2 Systematic differences

1. **Longitudinal steel is the generator default on every pier**: top 7 x #11 and bottom 7 x #11 for all 35 piers. Plans: Ramp G hammerhead caps top 14 + 14 x #9/#10/#11 in two staggered lengths (a1/a2) and bottom 8 x #8 (8b1); top 18 to 66 bars and bottom 8 to 60 bars of #8-#11 (the I-80 piers also carry 6b2/6b3 cantilever bars). Counts, sizes and the stagger are not represented, nor are the plan lap callouts ("3'-0 MIN. LAP", "11'-8 MIN. LAP", "3'-7 MIN. LAP"); the model computes AASHTO Class B laps instead.
2. **Skin bars**: 5 to 10 marks per pier (a3-a12, "E.F." = each face) that follow the architectural relief with different lengths and elevations (1WB: 6a3 20 x #6 plus 5a6-5a12); the model has 2 straight #5 per face (4 bars).
3. **Transverse steel**: plan hoops 6c1 (#6, in bays at about 1'-4, 194 on 1WB) + cantilever hoops 6c2 (VARIES) + ties 6c3/6c4 + hairpins 5c5/5c6 + 4c7 @ 2'-0. The model has one closed #5 stirrup per 6" at every station and nothing else (1WB 183 stirrups, 17.3-25.3 ft, against 393 hoops/ties in 4 marks). Stirrup weight is -47%: more spacing density but smaller bars and no cantilever hoops/ties.
4. **Hairpins, end bars, c7 and pedestal bars are not generated** (86,747 lb, 11% of the plan cap steel).
5. **Cap cover**: the plan note is 2" minimum clear unless noted; `CapCover` = 2" (consistent).
6. **Marks**: up to 75 single-bar stirrup marks per pier (1,420 in total) because each stirrup is cut to the local depth; the plans use c1/c2 plus a VARIES group for the cantilevers.

### 7.3 Generator gaps and proposed inputs

Only the inputs of section 2 exist: no bar groups with their own size/count/zones, no hoop zones, no hairpins, ends, transverse or pedestal bars, no second layer, no cantilever bottom bars, no relief-following skin bars. Needs code plus data:

```
CapBarGroups[]    (used instead of CapTop/Bottom/SideBar* when present)
    Mark "11a1", Layer Top | Bottom | Side | Cantilever, Size, Count
    Offsets          N offsets across the cap (explicit or equal spaces), layer index (1st, 2nd row)
    S0, S1  or Start + Length (the staggered a1/a2 lengths), Follow Top | Soffit | Relief | Fixed z, EndCover
    Lap to next group (callout, e.g. 3'-0)
CapHoopZones[]
    Mark "6c1", Kind Hoop | Tie | CantileverHoop | Hairpin | EndBar, Size, S0, S1, Spacing or Count
    Shape Closed (w x h follows cap depth, 135 degree hooks) | U | Straight; "VARIES" gives each bar its own length
CapPedestalBars[]  per bearing seat or "under all girders": m (longitudinal), n (transverse), hoop: size, count, length
CapTransverseBars  c7: size #4, spacing 2'-0, zone, length
```
The zones are on the cap elevations ("13 SPA. @ 1'-4 = 17'-4; 28-5c6 BARS", ...); the counts are in the bar lists below.

### 7.4 Per-pier cap detail (plan "mark count x length"; model "mark size count x length")


#### Pier 1WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 20x39'-9; 11a2 40x37'-7 | 12,211 | A #11 14x47.9ft | 3,564 |
| Bottom longitudinal (incl. cantilever) | 11b1 36x41'-5; 6b2 12x11'-1 | 8,122 | B1 #11 7x49.4ft; B2 #11 7x49.4ft | 3,675 |
| Side / skin longitudinal | 5a10 2x47'-7; 5a11 2x5'-3; 5a12 2x7'-2; 5a6 4x46'-8; 5a7 2x42'-5; 5a8 2x28'-7; 5a9 2x47'-8; 6a3 20x46'-11; 6a4 4x45'-6; 6a5 4x42'-10 | 2,506 | C1 #5 2x46.8ft; C2 #5 2x46.8ft; C3 #5 2x46.3ft; C4 #5 2x46.3ft | 388 |
| Hoops + ties (+ cantilever) | 6c1 194x22'-6; 6c2 68xVARIES; 6c3 97x8'-4; 6c4 34xVARIES | 10,217 | 18 marks (#5), 183 bars, 17.3-25.3 ft | 4,729 |
| Hairpins (vertical) | 5c5 72x11'-3; 5c6 64x9'-5 | 1,474 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 82x5'-5 | 297 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 36x10'-5; 5n1 36x10'-7; 5n2 2x22'-0 | 834 | **none** | 0 |

#### Pier 2WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 20x38'-9; 11a2 40x36'-7 | 11,893 | A #11 14x46.4ft | 3,453 |
| Bottom longitudinal (incl. cantilever) | 11b1 36x39'-10; 6b2 12x11'-1 | 7,819 | B1 #11 7x47.9ft; B2 #11 7x47.9ft | 3,564 |
| Side / skin longitudinal | 5a10 2x46'-1; 5a11 2x5'-1; 5a12 2x6'-11; 5a6 4x45'-3; 5a7 2x41'-1; 5a8 2x27'-8; 5a9 2x46'-2; 6a3 20x45'-5; 6a4 4x44'-0; 6a5 4x41'-4 | 2,426 | C1 #5 2x45.3ft; C2 #5 2x45.3ft; C3 #5 2x44.8ft; C4 #5 2x44.8ft | 376 |
| Hoops + ties (+ cantilever) | 6c1 186x22'-6; 6c2 68xVARIES; 6c3 93x8'-4; 6c4 34xVARIES | 9,897 | 18 marks (#5), 177 bars, 17.3-25.3 ft | 4,571 |
| Hairpins (vertical) | 5c5 72x11'-3; 5c6 60x9'-5 | 1,434 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 78x5'-5 | 282 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 36x10'-5; 5n1 36x10'-7; 5n2 2x22'-0 | 834 | **none** | 0 |

#### Pier 3WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 20x38'-9; 11a2 40x36'-7 | 11,893 | A #11 14x46.4ft | 3,453 |
| Bottom longitudinal (incl. cantilever) | 11b1 36x39'-10; 6b2 12x11'-1 | 7,819 | B1 #11 7x47.9ft; B2 #11 7x47.9ft | 3,564 |
| Side / skin longitudinal | 5a10 2x46'-1; 5a11 2x5'-1; 5a12 2x6'-11; 5a6 4x45'-2; 5a7 2x41'-1; 5a8 2x27'-8; 5a9 2x46'-2; 6a3 20x45'-5; 6a4 4x44'-0; 6a5 4x41'-4 | 2,425 | C1 #5 2x45.3ft; C2 #5 2x45.3ft; C3 #5 2x44.8ft; C4 #5 2x44.8ft | 376 |
| Hoops + ties (+ cantilever) | 6c1 186x22'-6; 6c2 68xVARIES; 6c3 93x8'-4; 6c4 34xVARIES | 9,897 | 18 marks (#5), 177 bars, 17.3-25.3 ft | 4,571 |
| Hairpins (vertical) | 5c5 72x11'-3; 5c6 60x9'-5 | 1,434 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 78x5'-5 | 282 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 36x11'-5; 5n1 42x10'-7; 5n2 2x24'-0 | 943 | **none** | 0 |

#### Pier 4WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 22x39'-3; 11a2 22x37'-1 | 8,923 | A #11 14x34.4ft | 2,560 |
| Bottom longitudinal (incl. cantilever) | 11b1 22x47'-0; 6b2 12x11'-1 | 5,694 | B1 #11 7x35.9ft; B2 #11 7x35.9ft | 2,671 |
| Side / skin longitudinal | 5a10 2x34'-2; 5a11 2x3'-8; 5a12 2x4'-11; 5a6 4x33'-2; 5a7 2x29'-10; 5a8 2x20'-0; 5a9 2x34'-3; 6a3 20x33'-5; 6a4 4x32'-0; 6a5 2x55'-1 | 1,763 | C1 #5 2x33.3ft; C2 #5 2x33.3ft; C3 #5 2x32.8ft; C4 #5 2x32.8ft | 276 |
| Hoops + ties (+ cantilever) | 6c1 142x22'-6; 6c2 68xVARIES; 6c3 71x8'-4; 6c4 34xVARIES | 8,135 | 18 marks (#5), 129 bars, 17.2-25.3 ft | 3,306 |
| Hairpins (vertical) | 5c5 50x11'-3; 5c6 46x9'-5 | 1,039 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 54x5'-5 | 195 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 36x10'-7; 5n1 36x10'-9; 5n2 3x22'-0 | 870 | **none** | 0 |

#### Pier 5WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 9a1 24x37'-5; 9a2 24x35'-9 | 5,970 | A #11 14x34.4ft | 2,560 |
| Bottom longitudinal (incl. cantilever) | 6b2 8x11'-1; 6b3 8x10'-0; 9b1 28x47'-0 | 4,727 | B1 #11 7x35.3ft; B2 #11 7x35.3ft | 2,628 |
| Side / skin longitudinal | 5a10 2x34'-6; 5a11 2x2'-11; 5a12 2x4'-10; 5a6 4x33'-3; 5a7 2x29'-10; 5a8 2x20'-0; 5a9 2x33'-5; 6a3 20x33'-8; 6a4 4x32'-6; 6a5 2x55'-1 | 1,772 | C1 #5 2x32.9ft; C2 #5 2x32.9ft; C3 #5 2x32.6ft; C4 #5 2x32.6ft | 273 |
| Hoops + ties (+ cantilever) | 6c1 110x25'-0; 6c2 48xVARIES; 6c3 55x8'-4; 6c4 24xVARIES | 6,726 | 34 marks (#5), 128 bars, 17.2-25.3 ft | 3,284 |
| Hairpins (vertical) | 5c5 50x13'-3; 5c6 46x11'-5 | 1,239 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x11'-3; 5a14 9x6'-10; 5a15 9x7'-3 | 226 | **none** | 0 |
| Cap transverse (c7) | 4c7 54x7'-5 | 268 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 48x10'-1; 5n1 36x12'-9; 5n2 3x25'-0 | 1,062 | **none** | 0 |

#### Pier 10WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 18x39'-10; 11a2 36x37'-8 | 11,013 | A #11 14x47.9ft | 3,564 |
| Bottom longitudinal (incl. cantilever) | 11b1 32x41'-5; 6b2 12x10'-10 | 7,236 | B1 #11 7x49.5ft; B2 #11 7x49.5ft | 3,678 |
| Side / skin longitudinal | 5a10 2x47'-10; 5a11 2x5'-3; 5a12 2x7'-2; 5a6 4x46'-9; 5a7 2x42'-6; 5a8 2x28'-8; 5a9 2x47'-5; 6a3 20x46'-11; 6a4 4x45'-7; 6a5 4x42'-11 | 2,510 | C1 #5 2x46.8ft; C2 #5 2x46.8ft; C3 #5 2x46.3ft; C4 #5 2x46.3ft | 389 |
| Hoops + ties (+ cantilever) | 6c1 158x23'-0; 6c2 52xVARIES; 6c3 79x8'-4; 6c4 26xVARIES | 8,349 | 35 marks (#5), 183 bars, 16.5-25.3 ft | 4,729 |
| Hairpins (vertical) | 5c5 72x11'-3; 5c6 64x9'-5 | 1,474 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 82x5'-5 | 297 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 42x9'-6; 5n1 42x9'-8 | 839 | **none** | 0 |

#### Pier 11WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 9a1 22x48'-7; 9a2 22x46'-10 | 7,137 | A #11 14x45.4ft | 3,378 |
| Bottom longitudinal (incl. cantilever) | 6b2 16x9'-10; 9b1 56x39'-9 | 7,804 | B1 #11 7x47.0ft; B2 #11 7x47.0ft | 3,495 |
| Side / skin longitudinal | 5a10 2x45'-4; 5a11 2x4'-11; 5a12 2x6'-9; 5a6 4x44'-3; 5a7 2x40'-3; 5a8 2x27'-1; 5a9 2x45'-0; 6a3 20x44'-10; 6a4 4x43'-10; 6a5 4x41'-6 | 2,397 | C1 #5 2x44.4ft; C2 #5 2x44.4ft; C3 #5 2x43.8ft; C4 #5 2x43.8ft | 368 |
| Hoops + ties (+ cantilever) | 6c1 138x25'-10; 6c2 32xVARIES; 6c3 69x8'-4; 6c4 16xVARIES | 7,536 | 31 marks (#5), 173 bars, 20.5-29.3 ft | 5,197 |
| Hairpins (vertical) | 5c5 72x13'-3; 5c6 58x11'-5 | 1,686 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x11'-3; 5a14 18x6'-10 | 222 | **none** | 0 |
| Cap transverse (c7) | 4c7 78x7'-5; 6n3 85x10'-7 | 1,737 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 56x9'-0; 5n1 42x7'-11; 5n2 42x7'-9 | 1,212 | **none** | 0 |
| Other | 6m3 4x44'-6; 8m2 8x45'-11 | 1,248 | **none** | 0 |

#### Pier 6A cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x55'-0; 10a2 28x51'-1 | 9,468 | A #11 7x51.8ft | 1,925 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x52'-11 | 1,130 | B #11 7x54.9ft | 2,043 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x52'-10; 5a8 4x51'-11; 5a9 2x29'-0; 8a3 12x51'-9; 8a4 2x43'-10; 8a5 2x35'-4; 8a6 2x27'-2; 8a7 2x19'-1 | 2,756 | C1 #5 2x53.6ft; C2 #5 2x52.6ft | 222 |
| Hoops + ties (+ cantilever) | 6c1 32x27'-8; 6c2 124xVARIES | 5,862 | 75 marks (#5), 108 bars, 21.2-33.8 ft | 3,486 |
| Hairpins (vertical) | 5c3 40x13'-1; 5c4 40x14'-3; 6n2 52x9'-2 | 1,857 | **none** | 0 |
| Ends (transverse / vertical) | 5a14 10x11'-3; 6a15 7x7'-6; 6a16 7x7'-9 | 329 | **none** | 0 |
| Cap transverse (c7) | 4c5 48x8'-2 | 262 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 40x9'-4; 5n1 50x8'-8 | 841 | **none** | 0 |
| Other | 8m2 4x51'-5 | 549 | **none** | 0 |

#### Pier 6WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 20x45'-6; 11a2 40x43'-3 | 14,026 | A #11 14x56.4ft | 4,196 |
| Bottom longitudinal (incl. cantilever) | 11b1 48x36'-1; 6b2 12x11'-0 | 9,400 | B1 #11 7x57.9ft; B2 #11 7x57.9ft | 4,308 |
| Side / skin longitudinal | 5a10 2x56'-2; 5a11 2x6'-4; 5a12 2x8'-8; 5a6 4x55'-2; 5a7 2x50'-6; 5a8 2x34'-0; 5a9 2x56'-0; 6a3 20x55'-5; 6a4 4x54'-2; 6a5 4x51'-6 | 2,970 | C1 #5 2x55.3ft; C2 #5 2x55.3ft; C3 #5 2x54.8ft; C4 #5 2x54.8ft | 459 |
| Hoops + ties (+ cantilever) | 6c1 186x22'-6; 6c2 68xVARIES; 6c3 93x8'-4; 6c4 34xVARIES | 9,897 | 31 marks (#5), 217 bars, 17.0-25.3 ft | 5,625 |
| Hairpins (vertical) | 5c5 86x11'-3; 5c6 76x9'-5 | 1,755 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 98x5'-5 | 355 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 66x10'-7; 5n1 66x10'-9; 5n2 4x22'-0 | 1,561 | **none** | 0 |

#### Pier 7WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 22x41'-1; 11a2 44x39'-0 | 13,919 | A #11 14x49.9ft | 3,713 |
| Bottom longitudinal (incl. cantilever) | 11b1 44x43'-5; 6b2 12x10'-11 | 10,347 | B1 #11 7x51.4ft; B2 #11 7x51.4ft | 3,825 |
| Side / skin longitudinal | 5a10 2x49'-8; 5a11 2x5'-9; 5a12 2x7'-7; 5a6 4x48'-9; 5a7 2x44'-5; 5a8 2x29'-11; 5a9 2x49'-5; 6a3 20x48'-11; 6a4 4x47'-8; 6a5 4x45'-0 | 2,618 | C1 #5 2x48.8ft; C2 #5 2x48.8ft; C3 #5 2x48.3ft; C4 #5 2x48.3ft | 405 |
| Hoops + ties (+ cantilever) | 6c1 222x22'-6; 6c2 68xVARIES; 6c3 111x8'-4; 6c4 34xVARIES | 11,338 | 34 marks (#5), 191 bars, 16.8-25.3 ft | 4,940 |
| Hairpins (vertical) | 5c5 78x11'-3; 5c6 64x9'-5 | 1,544 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 86x5'-5 | 311 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 60x10'-5; 5n1 70x9'-10 | 1,370 | **none** | 0 |

#### Pier 8WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 18x48'-1; 11a2 36x46'-0 | 13,396 | A #11 21x42.3ft | 4,723 |
| Bottom longitudinal (incl. cantilever) | 11b1 60x38'-9; 6b2 12x10'-11 | 12,550 | B1 #11 7x43.3ft; B2 #11 7x43.3ft; B3 #11 7x43.3ft | 4,834 |
| Side / skin longitudinal | 5a10 2x60'-0; 5a11 2x6'-10; 5a12 2x9'-2; 5a6 4x59'-3; 5a7 2x54'-2; 5a8 2x36'-7; 5a9 2x60'-0; 6a3 20x59'-5; 6a4 4x58'-2; 6a5 4x55'-6 | 3,186 | C1 #5 2x59.3ft; C2 #5 2x59.3ft; C3 #5 2x58.8ft; C4 #5 2x58.8ft | 493 |
| Hoops + ties (+ cantilever) | 6c1 282x23'-0; 6c2 68xVARIES; 6c3 141x8'-4; 6c4 34xVARIES | 14,005 | 34 marks (#5), 233 bars, 16.8-25.3 ft | 6,047 |
| Hairpins (vertical) | 5c5 94x11'-3; 5c6 80x9'-5 | 1,889 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 106x5'-5 | 384 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 54x9'-8; 5n1 54x9'-10 | 1,098 | **none** | 0 |

#### Pier 9WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 18x42'-6; 11a2 36x40'-4 | 11,778 | A #11 14x51.9ft | 3,862 |
| Bottom longitudinal (incl. cantilever) | 11b1 32x45'-5; 6b2 12x10'-10 | 7,917 | B1 #11 7x53.5ft; B2 #11 7x53.5ft | 3,976 |
| Side / skin longitudinal | 5a10 2x51'-9; 5a11 2x5'-9; 5a12 2x7'-11; 5a6 4x50'-9; 5a7 2x46'-4; 5a8 2x31'-2; 5a9 2x51'-5; 6a3 20x50'-11; 6a4 4x49'-8; 6a5 4x47'-0 | 2,728 | C1 #5 2x50.8ft; C2 #5 2x50.8ft; C3 #5 2x50.3ft; C4 #5 2x50.3ft | 422 |
| Hoops + ties (+ cantilever) | 6c1 178x23'-0; 6c2 52xVARIES; 6c3 89x8'-4; 6c4 26xVARIES | 9,173 | 35 marks (#5), 199 bars, 16.6-25.3 ft | 5,151 |
| Hairpins (vertical) | 5c5 78x11'-3; 5c6 70x9'-5 | 1,603 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 90x5'-5 | 326 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 48x9'-6; 5n1 48x9'-8 | 960 | **none** | 0 |

#### Pier 12WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 18x45'-6; 10a2 36x43'-7 | 10,275 | A #11 14x59.0ft | 4,388 |
| Bottom longitudinal (incl. cantilever) | 10b1 48x36'-9; 6b2 12x10'-9 | 7,784 | B1 #11 7x42.4ft; B2 #11 7x42.4ft; B3 #11 7x42.3ft | 4,724 |
| Side / skin longitudinal | 5a10 2x58'-10; 5a11 2x6'-8; 5a12 2x9'-1; 5a6 4x57'-9; 5a7 2x52'-11; 5a8 2x35'-8; 5a9 2x58'-5; 6a3 20x58'-0; 6a4 4x56'-8; 6a5 4x54'-0 | 3,109 | C1 #5 2x57.9ft; C2 #5 2x57.9ft; C3 #5 2x57.4ft; C4 #5 2x57.4ft | 481 |
| Hoops + ties (+ cantilever) | 6c1 218x23'-0; 6c2 68xVARIES; 6c3 109x8'-4; 6c4 34xVARIES | 11,393 | 34 marks (#5), 227 bars, 16.4-25.3 ft | 5,888 |
| Hairpins (vertical) | 5c5 92x11'-3; 5c6 78x9'-5 | 1,846 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 102x5'-5 | 369 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 60x9'-6; 5n1 60x9'-8 | 1,200 | **none** | 0 |

#### Pier 13WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 18x40'-10; 11a2 36x38'-8 | 11,301 | A #11 14x49.4ft | 3,676 |
| Bottom longitudinal (incl. cantilever) | 11b1 36x42'-11; 6b2 12x10'-9 | 8,403 | B1 #11 7x51.0ft; B2 #11 7x51.0ft | 3,791 |
| Side / skin longitudinal | 5a10 2x49'-4; 5a11 2x5'-5; 5a12 2x7'-6; 5a6 4x48'-3; 5a7 2x44'-0; 5a8 2x29'-8; 5a9 2x48'-11; 6a3 20x48'-6; 6a4 4x47'-2; 6a5 4x44'-6 | 2,594 | C1 #5 2x48.4ft; C2 #5 2x48.4ft; C3 #5 2x47.8ft; C4 #5 2x47.8ft | 401 |
| Hoops + ties (+ cantilever) | 6c1 170x23'-0; 6c2 36xVARIES; 6c3 85x8'-4; 6c4 18xVARIES | 8,248 | 33 marks (#5), 189 bars, 16.4-25.3 ft | 4,887 |
| Hairpins (vertical) | 5c5 78x11'-3; 5c6 64x9'-5 | 1,544 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 84x5'-5 | 304 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 54x9'-6; 5n1 54x9'-8 | 1,079 | **none** | 0 |

#### Pier 14WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 18x36'-2; 10a2 36x34'-3 | 8,107 | A #11 14x44.9ft | 3,341 |
| Bottom longitudinal (incl. cantilever) | 10b1 36x37'-8; 6b2 12x10'-9 | 6,029 | B1 #11 7x46.5ft; B2 #11 7x46.5ft | 3,456 |
| Side / skin longitudinal | 5a10 2x44'-11; 5a11 2x4'-10; 5a12 2x6'-8; 5a6 4x43'-9; 5a7 2x39'-9; 5a8 2x26'-9; 5a9 2x44'-6; 6a3 20x44'-0; 6a4 4x42'-7; 6a5 4x39'-11 | 2,351 | C1 #5 2x43.9ft; C2 #5 2x43.9ft; C3 #5 2x43.3ft; C4 #5 2x43.3ft | 364 |
| Hoops + ties (+ cantilever) | 6c1 138x23'-0; 6c2 40xVARIES; 6c3 69x8'-4; 6c4 20xVARIES | 7,088 | 33 marks (#5), 171 bars, 16.4-25.3 ft | 4,413 |
| Hairpins (vertical) | 5c5 70x11'-3; 5c6 58x9'-5 | 1,391 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 74x5'-5 | 268 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 48x9'-6; 5n1 48x9'-8 | 960 | **none** | 0 |

#### Pier 15WB cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 18x35'-6; 10a2 36x33'-7 | 7,952 | A #11 14x43.9ft | 3,267 |
| Bottom longitudinal (incl. cantilever) | 10b1 36x36'-8; 6b2 12x10'-9 | 5,874 | B1 #11 7x45.5ft; B2 #11 7x45.5ft | 3,382 |
| Side / skin longitudinal | 5a10 2x43'-11; 5a11 2x4'-8; 5a12 2x6'-6; 5a6 4x42'-9; 5a7 2x38'-10; 5a8 2x26'-2; 5a9 2x43'-5; 6a3 20x43'-0; 6a4 4x41'-7; 6a5 4x38'-11 | 2,297 | C1 #5 2x42.9ft; C2 #5 2x42.9ft; C3 #5 2x42.3ft; C4 #5 2x42.3ft | 355 |
| Hoops + ties (+ cantilever) | 6c1 134x23'-0; 6c2 40xVARIES; 6c3 67x8'-4; 6c4 20xVARIES | 6,925 | 33 marks (#5), 167 bars, 16.4-25.3 ft | 4,307 |
| Hairpins (vertical) | 5c5 68x11'-3; 5c6 56x9'-5 | 1,348 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 74x5'-5 | 268 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 48x9'-6; 5n1 48x9'-8 | 960 | **none** | 0 |

#### Pier 1G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x38'-5; 10a2 14x34'-3 | 4,377 | A #11 7x34.7ft | 1,289 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-3 | 817 | B #11 7x38.5ft | 1,432 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-6; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.9-33.3 ft | 2,370 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 2G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x38'-5; 10a2 14x34'-3 | 4,377 | A #11 7x34.7ft | 1,289 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x37'-11 | 810 | B #11 7x38.5ft | 1,432 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-7; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 44 marks (#5), 74 bars, 21.1-33.3 ft | 2,370 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-10; 5n1 24x10'-2 | 500 | **none** | 0 |

#### Pier 3G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x38'-5; 10a2 14x34'-3 | 4,377 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 4G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 14x38'-9; 11a2 14x34'-3 | 5,430 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 5G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 9a1 14x37'-11; 9a2 14x34'-3 | 3,435 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 6G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 14x38'-9; 11a2 14x34'-3 | 5,430 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 7G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x38'-5; 10a2 14x34'-3 | 4,377 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-10; 5n1 24x10'-2 | 500 | **none** | 0 |

#### Pier 8G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x38'-5; 10a2 14x34'-3 | 4,377 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 9G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 9a1 14x37'-11; 9a2 14x34'-3 | 3,435 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x10'-8; 5n1 24x11'-9; 5n2 4x15'-8; 5n3 24x8'-8 | 843 | **none** | 0 |

#### Pier 10G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 14x38'-9; 11a2 14x34'-3 | 5,430 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 11G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 14x38'-9; 11a2 14x34'-3 | 5,430 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-10; 5n1 24x10'-2 | 500 | **none** | 0 |

#### Pier 12G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 14x38'-9; 11a2 14x34'-3 | 5,430 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-10; 5n1 24x10'-2 | 500 | **none** | 0 |

#### Pier 13G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 14x38'-9; 11a2 14x34'-3 | 5,430 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 14G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 9a1 14x37'-11; 9a2 14x34'-3 | 3,435 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-1 | 813 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-5; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 15G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x38'-5; 10a2 14x34'-3 | 4,377 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-2 | 815 | B #11 7x38.5ft | 1,433 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-8; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,183 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 51 marks (#5), 74 bars, 20.5-33.3 ft | 2,372 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-2; 5n1 24x10'-2 | 483 | **none** | 0 |

#### Pier 16G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 11a1 22x39'-3; 11a2 22x37'-0 | 8,913 | A #11 14x34.4ft | 2,560 |
| Bottom longitudinal (incl. cantilever) | 11b1 22x47'-5; 6b2 12x11'-0 | 5,740 | B1 #11 7x35.9ft; B2 #11 7x35.9ft | 2,671 |
| Side / skin longitudinal | 5a10 2x34'-2; 5a11 2x3'-6; 5a12 2x4'-10; 5a6 4x33'-2; 5a7 2x29'-9; 5a8 2x20'-0; 5a9 2x34'-2; 6a3 20x33'-4; 6a4 4x32'-0; 6a5 2x55'-1 | 1,759 | C1 #5 2x33.3ft; C2 #5 2x33.3ft; C3 #5 2x32.8ft; C4 #5 2x32.8ft | 276 |
| Hoops + ties (+ cantilever) | 6c1 130x23'-0; 6c2 68xVARIES; 6c3 65x8'-4; 6c4 34xVARIES | 7,803 | 18 marks (#5), 129 bars, 17.3-25.3 ft | 3,306 |
| Hairpins (vertical) | 5c5 50x11'-3; 5c6 46x9'-5 | 1,039 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 54x5'-5 | 195 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x11'-0; 5n1 32x9'-6; 6n2 8x9'-3 | 703 | **none** | 0 |

#### Pier 17G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 9x56'-5; 10a2 9x52'-6 | 4,218 | A #11 7x52.8ft | 1,962 |
| Bottom longitudinal (incl. cantilever) | 10b1 18x37'-5; 6b2 12x10'-11 | 3,095 | B #11 7x55.8ft | 2,073 |
| Side / skin longitudinal | 5a11 2x2'-10; 5a12 2x4'-0; 5a6 2x53'-4; 5a7 2x25'-1; 5a8 2x16'-9; 5a9 2x55'-6; 6a3 10x53'-2; 6a4 2x50'-5; 6a5 2x45'-1 | 1,413 | C1 #5 2x54.5ft; C2 #5 2x53.5ft | 225 |
| Hoops + ties (+ cantilever) | 6c1 102x23'-0; 6c2 68xVARIES; 6c3 47x8'-4; 6c4 34xVARIES | 6,333 | 34 marks (#5), 109 bars, 16.8-25.3 ft | 2,779 |
| Hairpins (vertical) | 5c5 42x11'-3; 5c6 40x9'-5 | 886 | **none** | 0 |
| Ends (transverse / vertical) | 5a13 8x9'-3; 5a14 12x6'-10 | 163 | **none** | 0 |
| Cap transverse (c7) | 4c7 46x5'-5 | 166 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-10; 5n1 28x9'-6; 6n2 7x9'-3 | 620 | **none** | 0 |

#### Pier 18G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x38'-5; 10a2 14x34'-3 | 4,377 | A #11 7x34.8ft | 1,292 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x38'-3 | 817 | B #11 7x38.5ft | 1,434 |
| Side / skin longitudinal | 5a10 2x19'-5; 5a11 2x11'-9; 5a12 2x37'-7; 5a13 2x5'-5; 5a8 4x35'-4; 5a9 2x23'-11; 8a3 14x35'-1; 8a4 2x32'-0; 8a5 2x26'-9; 8a6 2x21'-8; 8a7 2x16'-8 | 2,182 | C1 #5 2x37.0ft; C2 #5 2x35.7ft | 152 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 72xVARIES | 4,188 | 50 marks (#5), 74 bars, 20.3-33.3 ft | 2,373 |
| Hairpins (vertical) | 5c3 26x13'-9; 5c4 28x15'-1 | 813 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 16x8'-2 | 87 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 24x9'-10; 5n1 24x10'-2 | 500 | **none** | 0 |

#### Pier 19G cap

| Group | Plan bars (mark count x length) | Plan lb | Model bars | Model lb |
|---|---|---:|---|---:|
| Top longitudinal | 10a1 14x43'-6; 10a2 14x39'-5; 8m2 4x39'-5 | 5,417 | A #11 7x39.8ft | 1,478 |
| Bottom longitudinal (incl. cantilever) | 8b1 8x42'-8 | 911 | B #11 7x43.4ft | 1,613 |
| Side / skin longitudinal | 5a10 2x23'-0; 5a11 2x14'-6; 5a12 2x42'-7; 5a13 2x5'-8; 5a8 4x40'-4; 5a9 2x27'-11; 8a3 14x40'-2; 8a4 2x35'-11; 8a5 2x29'-8; 8a6 2x23'-8; 8a7 2x17'-8 | 2,476 | C1 #5 2x41.9ft; C2 #5 2x40.7ft | 172 |
| Hoops + ties (+ cantilever) | 6c1 32x29'-0; 6c2 40xVARIES; 6c6 46xVARIES | 4,724 | 59 marks (#5), 84 bars, 20.5-33.3 ft | 2,682 |
| Hairpins (vertical) | 5c3 28x13'-9; 5c4 34x15'-1 | 937 | **none** | 0 |
| Ends (transverse / vertical) | 6a14 10x11'-3; 6a15 14x8'-0 | 337 | **none** | 0 |
| Cap transverse (c7) | 4c5 19x8'-2; 6n2 40x8'-11 | 640 | **none** | 0 |
| Pedestal m/n + hoops | 5m1 28x11'-4; 5n1 42x10'-1 | 773 | **none** | 0 |
| Other | 5m3 2x39'-5 | 82 | **none** | 0 |


## 8. Columns (marks d and e) against `column-rebar.csx`

Totals (35 piers, lb): vertical bars plan 623,996 + footing-to-column dowels 317,351 = 941,347 vs model verticals 861,736 (-8.5%); hoops 158,708 vs 146,215 (-8%); ties/crossties 23,538 vs 23,614 (0%); plinth (flare) groups 77,359 (+ plinth dowels, which are inside the dowel figure) vs model flare bars/ties 114,788 (+48%). Whole column group: 1,200,952 plan vs 1,146,352 model (-5%), per pier -27% to +19% (summary table).

Confirmed:
- **Bar sizes** match the plans on all 35 piers (#11 / #14 / #18). Vertical counts match on 26 piers (1WB 96, 11WB 3 x 32, 18G 46 ...).
- **Nine piers (4G, 5G, 6G, 7G, 8G, 10G, 11G, 12G, 13G) have 92 model verticals against 46 in the plans.** Each model vertical runs from the foundation mat to the cap (no dowel). Where that exceeds the 60 ft stock length the generator splits it into two lapped pieces (4G: 2 x 33.1 ft against one plan bar 53'-8 plus a 15'-0 dowel). The count of 92 is a splice artefact; the column group weight on those piers is 4G 0%, 5G -4%, 6G 0%, 13G 0%, 8G +10%, 10G +9%, 12G +15%, 11G +16%, 7G +19%.
- **Dowels and couplers: 0 of 317,351 lb are in the model** (known; being reworked). The model vertical is one continuous bar, so the plan's lap (8'-4 non-coated / 12'-6 epoxy contact lap) or coupler zone is not in the weight; I-80 piers with lap splices and the small Ramp G piers are 8% to 27% light (1WB -27%, 2WB -19%, 6A -21%, 19G -24%, 5WB/11WB -17%).
- **Hoops 6e1**: size #6 on every pier. Counts are equal on 15 piers; the model is short on 19 piers by 2 to 10 hoops (4WB 120/124, 5WB 132/136, 10WB, 11WB, 6WB, 7WB, 8WB, 9WB, 12WB, 13WB, 14WB, 2G, 7G, 10G-13G on flared columns, where the lapped plinth hoop pairs count as single hoops; 6A 60/70; 19G 50/58) and long by 2 on 15WB (130/128). Hoop length: plan 19'-10 or 25'-1 against model 18.5 or 23.7 ft (-5% to -7%: hook/lap allowance); ties 5e2 (#5): 4'-1 / 5'-7 plan against 3.9 / 6.0 ft model (-5% / +8%).
- **11G/12G inner plinth hoops 6e4 are missing**: plan 6e4 50 x 27'-0 (2,028 lb, 11G) and 56 x 27'-0 (2,271 lb, 12G).
- **18d9 / above-splice bars modelled full height**: on 11G/12G/18G the plan's column verticals above the splice are 18d1/18d2 (23 and 19 bars) and 18d7/18d5 (4 bars, shorter); the model has a single mark V of full height, which adds length (11G +16%, 12G +15%).
- **Flare / plinth**: the model's flare ties are full-wrap #6 @ 12" (P1, e.g. 11G 48 x 30.4 ft) while the plans use plinth hairpins 6e3 (#6, e.g. 11WB 84 x 14'-1) or hoops 6e4; model flare verticals #7 (F2) against plan 7d5/7d12 (+ dowels 7d6/7d13); net +48% on the plinth items.
- No other differences were found (bar sizes and bent-level vertical counts agree).

Per pier (weights in parentheses, lb; model lists the first four marks of a group):


| Pier | Verticals plan | Verticals model | Hoops plan | Hoops model | Ties plan | Ties model | Flare (model) | Dowels plan lb | Plinth plan lb |
|---|---|---|---|---|---|---|---|---:|---:|
| Pier 1WB | #11 96x24'-0 (12,241) | #11 96x29.6ft (15,089) | #6 114x19'-10 (3,396) | #6 114x18.5ft (3,164) | #5 114x4'-1 (486) | #5 114x3.9ft (468) | - | 9,563 | 0 |
| Pier 2WB | #11 96x28'-0 (14,281) | #11 96x33.6ft (17,129) | #6 138x19'-10 (4,111) | #6 138x18.5ft (3,831) | #5 138x4'-1 (588) | #5 138x3.9ft (567) | - | 7,438 | 0 |
| Pier 3WB | #18 48x21'-0; #18 48x26'-0 (30,682) | #18 96x36.1ft (47,110) | #6 156x19'-10 (4,647) | #6 156x18.5ft (4,330) | #5 156x4'-0 (651) | #5 156x3.9ft (639) | - | 19,040 | 0 |
| Pier 4WB | #11 64x35'-0 (11,901) | #11 64x41.1ft (13,970) | #6 124x19'-10 (3,694) | #6 120x18.5ft (3,331) | #5 124x4'-1 (528) | #5 120x3.9ft (493) | 100 bars/3,553 lb | 5,687 | 2,358 |
| Pier 5WB | #11 64x38'-0 (12,921) | #11 64x43.6ft (14,820) | #6 136x19'-10 (4,051) | #6 132x18.5ft (3,664) | #5 136x4'-1 (579) | #5 132x3.9ft (542) | 104 bars/3,750 lb | 7,191 | 2,549 |
| Pier 10WB | #11 32x31'-11; #11 32x33'-4; #11 32x34'-10 (17,015) | #11 32x40.1ft; #11 32x38.6ft; #11 32x37.1ft (19,679) | #6 176x19'-10 (5,243) | #6 172x18.5ft (4,774) | #5 176x4'-1 (750) | #5 172x3.9ft (706) | 150 bars/5,170 lb | 8,020 | 3,537 |
| Pier 11WB | #11 32x32'-10; #11 32x34'-10; #11 32x35'-11 (17,610) | #11 32x42.7ft; #11 32x41.7ft; #11 32x39.6ft (21,068) | #6 184x19'-10 (5,481) | #6 178x18.5ft (4,941) | #5 184x4'-1 (784) | #5 178x3.9ft (731) | 156 bars/5,784 lb | 11,295 | 3,823 |
| Pier 6A | #11 46x35'-11 (8,778) | #11 46x42.8ft (10,468) | #6 70x25'-1 (2,637) | #6 60x23.7ft (2,137) | #5 70x5'-7 (408) | #5 60x6.0ft (377) | - | 4,582 | 0 |
| Pier 6WB | #11 32x45'-0; #11 32x45'-5; #11 32x45'-11; #11 32x46'-5 (31,072) | #11 32x52.5ft; #11 32x52.1ft; #11 32x51.6ft; #11 32x51.1ft (35,236) | #6 332x19'-10 (9,890) | #6 324x18.5ft (8,994) | #5 332x4'-1 (1,414) | #5 324x3.9ft (1,331) | 256 bars/10,600 lb | 11,374 | 7,389 |
| Pier 7WB | #18 20x26'-11; #18 10x31'-11; #18 20x27'-9; #18 10x32'-9; #18 20x28'-8; #18 10x33'-8; #18 12x25'-8 (40,229) | #18 34x45.8ft; #18 34x44.9ft; #18 34x44.1ft (62,347) | #6 204x19'-10 (6,077) | #6 198x18.5ft (5,496) | #5 204x4'-0 (851) | #5 198x3.9ft (805) | 150 bars/5,476 lb | 24,569 | 3,537 |
| Pier 8WB | #11 32x30'-11; #11 32x31'-10; #11 32x32'-9; #11 32x33'-7 (21,946) | #11 32x39.8ft; #11 32x38.9ft; #11 32x38.0ft; #11 32x37.1ft (26,140) | #6 228x19'-10 (6,792) | #6 220x18.5ft (6,107) | #5 228x4'-1 (971) | #5 220x3.9ft (904) | 200 bars/7,106 lb | 11,374 | 4,715 |
| Pier 9WB | #11 32x29'-11; #11 32x31'-5; #11 32x33'-0 (16,038) | #11 32x38.2ft; #11 32x36.7ft; #11 32x35.1ft (18,702) | #6 164x19'-10 (4,886) | #6 160x18.5ft (4,441) | #5 164x4'-1 (698) | #5 160x3.9ft (657) | 144 bars/4,796 lb | 8,020 | 3,250 |
| Pier 12WB | #11 32x32'-10; #11 32x34'-4; #11 32x35'-10; #11 32x37'-4 (23,858) | #11 32x43.1ft; #11 32x41.6ft; #11 32x40.1ft; #11 32x38.6ft (27,769) | #6 248x19'-10 (7,388) | #6 244x18.5ft (6,773) | #5 248x4'-1 (1,056) | #5 244x3.9ft (1,002) | 216 bars/7,998 lb | 11,034 | 5,479 |
| Pier 13WB | #18 16x22'-10; #18 16x27'-10; #18 16x24'-8; #18 16x29'-8; #18 16x26'-6; #18 16x31'-6 (35,468) | #18 32x42.8ft; #18 32x40.9ft; #18 32x39.1ft (53,421) | #6 186x19'-10 (5,541) | #6 180x18.5ft (4,996) | #5 186x4'-0 (776) | #5 180x3.9ft (737) | 150 bars/5,316 lb | 21,183 | 3,537 |
| Pier 14WB | #11 32x31'-10; #11 32x33'-6; #11 32x35'-2 (17,087) | #11 32x40.4ft; #11 32x38.8ft; #11 32x37.1ft (19,764) | #6 178x19'-10 (5,303) | #6 172x18.5ft (4,774) | #5 178x4'-1 (758) | #5 172x3.9ft (706) | 150 bars/5,170 lb | 8,020 | 3,537 |
| Pier 15WB | #11 32x24'-10; #11 32x26'-6; #11 32x28'-2 (13,516) | #11 32x33.4ft; #11 32x31.8ft; #11 32x30.1ft (16,194) | #6 128x19'-10 (3,813) | #6 130x18.5ft (3,609) | #5 128x4'-1 (545) | #5 130x3.9ft (534) | - | 7,183 | 0 |
| Pier 1G | #11 46x32'-10 (8,024) | #11 46x38.6ft (9,430) | #6 62x25'-1 (2,336) | #6 62x23.7ft (2,208) | #5 62x5'-7 (361) | #5 62x6.0ft (390) | - | 3,544 | 0 |
| Pier 2G | #18 23x24'-11; #18 19x29'-11; #18 4x21'-9 (16,707) | #18 46x47.1ft (29,455) | #6 80x25'-1 (3,014) | #6 78x23.7ft (2,778) | #5 80x5'-7 (466) | #5 78x6.0ft (491) | 61 bars/2,510 lb | 13,845 | 1,551 |
| Pier 3G | #11 46x46'-8 (11,405) | #11 46x53.1ft (12,973) | #6 96x25'-1 (3,617) | #6 96x23.7ft (3,420) | #5 96x5'-7 (559) | #5 96x6.0ft (603) | 66 bars/2,809 lb | 4,009 | 1,786 |
| Pier 4G | #11 46x53'-8 (13,116) | #11 92x33.1ft (16,171) | #6 112x25'-1 (4,220) | #6 112x23.7ft (3,990) | #5 112x5'-7 (652) | #5 112x6.0ft (704) | 73 bars/3,325 lb | 4,009 | 2,189 |
| Pier 5G | #11 46x52'-8 (12,872) | #11 92x33.1ft (16,171) | #6 110x25'-1 (4,144) | #6 110x23.7ft (3,918) | #5 110x5'-7 (641) | #5 110x6.0ft (691) | 71 bars/3,234 lb | 5,327 | 2,072 |
| Pier 6G | #11 46x57'-8 (14,094) | #11 92x35.1ft (17,149) | #6 122x25'-1 (4,596) | #6 122x23.7ft (4,346) | #5 122x5'-7 (710) | #5 122x6.0ft (766) | 75 bars/3,481 lb | 4,009 | 2,306 |
| Pier 7G | #18 23x37'-8; #18 19x42'-8; #18 4x34'-6 (24,684) | #18 92x37.6ft (47,024) | #6 112x25'-1 (4,220) | #6 110x23.7ft (3,918) | #5 112x5'-7 (652) | #5 110x6.0ft (692) | 71 bars/3,294 lb | 14,471 | 2,072 |
| Pier 8G | #14 23x52'-8; #14 23x57'-8 (19,413) | #14 92x42.0ft (29,560) | #6 146x25'-1 (5,501) | #6 146x23.7ft (5,201) | #5 146x5'-7 (850) | #5 146x6.0ft (918) | 83 bars/4,140 lb | 7,821 | 2,710 |
| Pier 9G | #11 92x39'-7 (19,348) | #11 92x39.6ft (19,348) | #6 144x25'-1 (5,425) | #6 144x23.7ft (5,130) | #5 144x5'-7 (839) | #5 144x6.0ft (904) | 83 bars/4,042 lb | 5,082 | 2,710 |
| Pier 10G | #14 23x50'-8; #14 23x55'-8 (18,710) | #14 92x41.0ft (28,856) | #6 142x25'-1 (5,350) | #6 140x23.7ft (4,987) | #5 142x5'-7 (827) | #5 140x6.0ft (880) | 80 bars/3,938 lb | 7,876 | 2,592 |
| Pier 11G | #18 23x47'-8; #18 19x52'-8; #18 4x44'-6 (30,940) | #18 92x42.3ft (52,967) | #6 136x25'-1 (5,124) | #6 134x23.7ft (4,773) | #5 136x5'-7 (792) | #5 134x6.0ft (843) | 94 bars/4,696 lb | 14,289 | 3,568 |
| Pier 12G | #18 23x50'-8; #18 19x55'-8; #18 4x47'-6 (32,817) | #18 92x43.8ft (54,844) | #6 142x25'-1 (5,350) | #6 140x23.7ft (4,987) | #5 142x5'-7 (827) | #5 140x6.0ft (881) | 101 bars/5,297 lb | 14,224 | 4,045 |
| Pier 13G | #11 46x55'-8 (13,605) | #11 92x34.1ft (16,660) | #6 118x25'-1 (4,446) | #6 116x23.7ft (4,132) | #5 118x5'-7 (687) | #5 116x6.0ft (729) | 73 bars/3,325 lb | 4,009 | 2,189 |
| Pier 14G | #11 46x52'-8 (12,872) | #11 46x59.1ft (14,440) | #6 110x25'-1 (4,144) | #6 110x23.7ft (3,918) | #5 110x5'-7 (641) | #5 110x6.0ft (691) | 71 bars/3,168 lb | 5,082 | 2,072 |
| Pier 15G | #11 46x45'-9 (11,181) | #11 46x52.1ft (12,729) | #6 92x25'-1 (3,466) | #6 92x23.7ft (3,277) | #5 92x5'-7 (536) | #5 92x6.0ft (578) | 66 bars/2,809 lb | 4,009 | 1,786 |
| Pier 16G | #14 28x25'-0; #14 28x30'-0 (11,781) | #14 56x40.6ft (17,386) | #6 120x19'-9 (3,560) | #6 120x18.5ft (3,331) | #5 120x4'-4 (542) | #5 120x4.3ft (541) | - | 6,141 | 0 |
| Pier 17G | #14 14x19'-10; #14 14x24'-10; #14 14x20'-7; #14 14x25'-7 (9,728) | #14 28x36.3ft; #14 28x35.6ft (15,404) | #6 102x19'-9 (3,026) | #6 102x18.5ft (2,831) | #5 102x4'-4 (461) | #5 102x4.3ft (460) | - | 6,141 | 0 |
| Pier 18G | #18 23x14'-8; #18 19x19'-8; #18 4x11'-6 (10,296) | #18 46x37.1ft (23,199) | #6 54x25'-1 (2,034) | #6 54x23.7ft (1,924) | #5 54x5'-7 (314) | #5 54x6.0ft (340) | - | 13,450 | 0 |
| Pier 19G | #11 46x31'-9 (7,760) | #11 46x37.1ft (9,063) | #6 58x25'-1 (2,185) | #6 50x23.7ft (1,781) | #5 58x5'-7 (338) | #5 50x6.0ft (314) | - | 4,440 | 0 |



## 9. Abutments

The plans call the abutment pile-cap beam the "FOOTING" (9a1 / 8a1 longitudinal, 5b1 hoops, 5b2-5b4 end bars, uplift anchors 8j3), then backwall, paving notch, counterfort, wingwall, maskwall, beam-step and wing-footing groups. The model abutment is a cap (A/B #11, C #5 skin, S #5 stirrups) with a backwall mat (W verticals, H horizontals, #5), piles directly under the cap, no abutment footing (the four pile caps have `size 0 x 0`, so `FootingBars` produces nothing), no wings, counterforts, paving notch or beam steps.

| Abutment | Plan "footing" (pile-cap beam) lb | Model cap lb | Plan backwall + notch + counterfort lb | Model backwall lb | Plan wing + mask lb | Plan wing footing lb | Plan beam steps lb | Plan total lb | Model total lb |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| West (I-80, sheet 88) | 9,326 | 9,933 | 10,836 | 4,078 | 3,604 | 768 | 343 | 24,877 | 14,011 |
| East (I-80, sheet 92) | 8,772 | 9,483 | 8,711 | 2,968 | 2,802 | 768 | 398 | 21,451 | 12,451 |
| South (Ramp G, sheet 83) | 2,743 | 3,556 | 4,518 | 1,344 | 3,096 | 768 | 274 | 11,399 | 4,900 |
| **Total** | **20,841** | **22,972** | **24,065** | **8,390** | **9,502** | **2,304** | **1,015** | **57,727** | **31,362** |

The cap / pile-cap beam is close in total weight (+10%) but not in composition (plan 40 x #9 longitudinal 48'-4 plus 5b1 hoops #5 and end bars; model 28 x #11, 169 #5 stirrups and skin bars). The backwall is -65% (plans: 6d1/5d2 B.F./F.F. verticals, 6g1 longitudinal, hairpins, notch, extension; model: uniform #5 @ 12"). Wings, maskwalls, wing footings, beam steps and uplift anchors (about 13,000 lb) are absent. Abutments overall are -46%. The abutment "footing" (pile-cap beam) is the one place where the model's cap rebar is close in total, by coincidence of the two default 28 #11 bars against the plan's 40 #9 + hoops.

Plan bar lists and model marks per abutment:


#### West Abut

Plan bar list (all groups) vs model marks:

| Plan mark | Description | Count | Length | lb |
|---|---|---:|---|---:|
| 9a1 | FOOTING, LONGITUDINAL | 40 | 48'-4 | 6,573 |
| 5b1 | FOOTING, HOOPS | 108 | 20'-7 | 2,319 |
| 5b2 | FOOTING, HOOPS | 10 | VARIES | 168 |
| 5b3 | FOOTING, END, HORIZONTAL | 5 | 11'-7 | 60 |
| 5b4 | FOOTING, END, HORIZONTAL | 5 | 19'-0 | 99 |
| 6d1 | BACKWALL, B.F., VERTICAL | 173 | 13'-1 | 3,400 |
| 5d2 | BACKWALL, F.F., VERTICAL | 88 | 12'-9 | 1,170 |
| 5d3 | PAVING NOTCH | 83 | 4'-5 | 382 |
| 5d4 | PAVING NOTCH | 83 | 3'-8 | 317 |
| 4d6 | BACKWALL, HAIRPIN, VERTICAL | 86 | 8'-10 | 507 |
| 5d7 | BACKWALL EXTENSION, VERTICAL | 11 | 14'-5 | 165 |
| 4d8 | BACKWALL EXTENSION, HAIRPIN, VERTICAL | 6 | 3'-2 | 13 |
| 5e1 | WINGWALL, VERTICAL | 52 | VARIES | 456 |
| 5e2 | MASKWALL, VERTICAL | 21 | 15'-6 | 339 |
| 5e3 | MASKWALL, HORIZONTAL | 30 | 10'-0 | 313 |
| 5e4 | WINGWALL, VERTICAL | 54 | VARIES | 467 |
| 6f1 | WINGWALL, HORIZONTAL | 8 | 25'-8 | 308 |
| 6f2 | WINGWALL, HORIZONTAL | 16 | VARIES | 358 |
| 6f3 | WINGWALL, BOTTOM | 2 | 26'-11 | 81 |
| 6f4 | WINGWALL, DOWELS | 8 | 6'-0 | 72 |
| 6f5 | WINGWALL, HORIZONTAL | 8 | 27'-0 | 324 |
| 6f6 | WINGWALL, HORIZONTAL | 16 | VARIES | 371 |
| 6f7 | WINGWALL, BOTTOM | 2 | 28'-5 | 85 |
| 6g1 | BACKWALL, LONGITUDINAL | 44 | 45'-1 | 2,979 |
| 5g2 | BACKWALL, DOWELS | 22 | 4'-7 | 105 |
| 5g3 | PAVING NOTCH, LONGITUDINAL | 4 | 42'-7 | 178 |
| 6g4 | WINGWALL, DOWELS | 44 | 6'-6 | 430 |
| 6g5 | BACKWALL EXTENSION, HORIZONTAL | 32 | 8'-0 | 385 |
| 5h1 | COUNTERFORT, DOWELS | 24 | 4'-0 | 100 |
| 6h2 | COUNTERFORT NO. 1, VERTICAL | 20 | 11'-5 | 343 |
| 6h3 | COUNTERFORT NO. 2, VERTICAL | 20 | 7'-11 | 238 |
| 5h4 | COUNTERFORT, HORIZONTAL | 32 | 12'-0 | 401 |
| 5j1 | WING FOOTING, LONGITUDINAL | 32 | 11'-6 | 384 |
| 5j2 | WING FOOTING, TRANSVERSE | 32 | 11'-6 | 384 |
| 8j3 | PILE UPLIFT ANCHORS | 8 | 5'-0 | 107 |
| 5m1 | BEAM STEPS, TRANSVERSE | 24 | 8'-2 | 204 |
| 5n1 | BEAM STEPS, LONGITUDINAL | 24 | 4'-2 | 104 |
| 5n2 | BEAM STEPS, HOOP | 2 | 16'-10 | 35 |
| 5d5 | PAVING NOTCH, DOWELS | 42 | 3'-6 | 153 |
| **plan total** | | | | **24,877** |

| Model mark | Size | Count | Length ft | Shape | lb | Location |
|---|---|---:|---:|---|---:|---|
| A | #11 | 14 | 45.08 | STR | 3,353 | West Abut cap top |
| B | #11 | 14 | 45.08 | STR | 3,353 | West Abut cap bottom |
| C | #5 | 8 | 43.08 | STR | 360 | West Abut cap side faces |
| S | #5 | 169 | 16.27 | TIE | 2,867 | West Abut cap stirrups |
| W1 | #5 | 56 | 11.92 | STR | 696 | West Abut backwall verticals |
| W2 | #5 | 44 | 11.75 | STR | 539 | West Abut backwall verticals |
| W3 | #5 | 44 | 11.58 | STR | 532 | West Abut backwall verticals |
| W4 | #5 | 28 | 11.42 | STR | 333 | West Abut backwall verticals |
| H | #5 | 44 | 43.08 | STR | 1,977 | West Abut backwall horizontals |
| **model total** | | | | | **14,011** | |

#### East Abut

Plan bar list (all groups) vs model marks:

| Plan mark | Description | Count | Length | lb |
|---|---|---:|---|---:|
| 9a1 | FOOTING, LONGITUDINAL | 40 | 45'11 | 6,245 |
| 5b1 | FOOTING, HOOPS | 110 | 20'-7 | 2,362 |
| 5b2 | FOOTING, END, HORIZONTAL | 6 | 9'-3 | 58 |
| 6d1 | BACKWALL, B.F., VERTICAL | 162 | 10'-6 | 2,555 |
| 5d2 | BACKWALL, F.F., VERTICAL | 82 | 10'-2 | 870 |
| 5d3 | PAVING NOTCH | 79 | 4'-5 | 364 |
| 5d4 | PAVING NOTCH | 79 | 3'-8 | 302 |
| 4d6 | BACKWALL, HAIRPIN, VERTICAL | 80 | 8'-10 | 472 |
| 5d7 | BACKWALL EXTENSION, VERTICAL | 8 | 12'-2 | 102 |
| 4d8 | BACKWALL EXTENSION, HAIRPIN, VERT. | 4 | 3'-2 | 8 |
| 5e1 | WINGWALL, VERTICAL | 96 | VARIES | 768 |
| 5e2 | MASKWALL, VERTICAL | 15 | 13'-8 | 214 |
| 5e3 | MASKWALL, HORIZONTAL | 26 | 6'-4 | 172 |
| 6f1 | WINGWALL, HORIZONTAL | 16 | 23'-3 | 559 |
| 6f2 | WINGWALL, HORIZONTAL | 28 | VARIES | 554 |
| 6f3 | WINGWALL, BOTTOM | 4 | 24'-5 | 147 |
| 6f4 | WINGWALL, DOWELS | 12 | 5'-3 | 95 |
| 6g1 | BACKWALL, LONGITUDINAL | 36 | 42'-5 | 2,294 |
| 5g2 | BACKWALL, DOWELS | 18 | 6'-1 | 114 |
| 5g3 | PAVING NOTCH, LONGITUDINAL | 4 | 40'-1 | 167 |
| 6g4 | WINGWALL, DOWELS | 36 | 5'-5 | 293 |
| 6g5 | BACKWALL EXTENSION, HORIZONTAL | 28 | 6'-5 | 270 |
| 5h1 | COUNTERFORT, DOWELS | 24 | 4'-0 | 100 |
| 6h2 | COUNTERFORT NO. 1, VERTICAL | 20 | 11'-3 | 338 |
| 6h3 | COUNTERFORT NO. 2, VERTICAL | 20 | 7'-11 | 238 |
| 5h4 | COUNTERFORT, HORIZONTAL | 30 | 12'-0 | 375 |
| 5j1 | WING FOOTING, LONGITUDINAL | 32 | 11'-6 | 384 |
| 5j2 | WING FOOTING, TRANSVERSE | 32 | 11'-6 | 384 |
| 8j3 | PILE UPLIFT ANCHORS | 8 | 5'-0 | 107 |
| 5m1 | BEAM STEPS, TRANSVERSE | 32 | 7'-9 | 259 |
| 5n1 | BEAM STEPS, LONGITUDINAL | 32 | 4'-2 | 139 |
| 5d5 | PAVING NOTCH, DOWELS | 39 | 3'-6 | 142 |
| **plan total** | | | | **21,451** |

| Model mark | Size | Count | Length ft | Shape | lb | Location |
|---|---|---:|---:|---|---:|---|
| A | #11 | 14 | 43.08 | STR | 3,205 | East Abut cap top |
| B | #11 | 14 | 43.08 | STR | 3,205 | East Abut cap bottom |
| C | #5 | 8 | 41.08 | STR | 343 | East Abut cap side faces |
| S | #5 | 161 | 16.27 | TIE | 2,731 | East Abut cap stirrups |
| W | #5 | 164 | 9.33 | STR | 1,596 | East Abut backwall verticals |
| H | #5 | 32 | 41.08 | STR | 1,371 | East Abut backwall horizontals |
| **model total** | | | | | **12,451** | |

#### South Abut

Plan bar list (all groups) vs model marks:

| Plan mark | Description | Count | Length | lb |
|---|---|---:|---|---:|
| 8a1 | FOOTING, LONGITUDINAL | 16 | 37'-6 | 1,602 |
| 5b1 | FOOTING, HOOPS | 49 | 19'-2 | 980 |
| 5b2 | FOOTING, END, HORIZONTAL | 6 | 8'-7 | 54 |
| 6d1 | BACKWALL, B.F., VERTICAL | 67 | 11'-4 | 1,141 |
| 5d2 | BACKWALL, F.F., VERTICAL | 35 | 11'-0 | 402 |
| 5d3 | PAVING NOTCH | 31 | 4'-5 | 143 |
| 5d4 | PAVING NOTCH | 31 | 3'-8 | 119 |
| 4d6 | BACKWALL, HAIRPIN, VERTICAL | 31 | 8'-0 | 166 |
| 5e1 | WINGWALL, VERTICAL | 92 | VARIES | 736 |
| 5e2 | MASKWALL, VERTICAL | 26 | 14'-7 | 395 |
| 5e3 | MASKWALL, HORIZONTAL | 56 | 5'-8 | 331 |
| 6f1 | WINGWALL, HORIZONTAL | 20 | 22'-2 | 666 |
| 6f2 | WINGWALL, HORIZONTAL | 24 | VARIES | 473 |
| 6f3 | WINGWALL, BOTTOM | 4 | 23'-1 | 139 |
| 6f4 | WINGWALL, DOWELS | 8 | 5'-3 | 63 |
| 6g1 | BACKWALL, LONGITUDINAL | 22 | 31'-8 | 1,046 |
| 5g2 | BACKWALL, DOWELS | 44 | 6'-1 | 279 |
| 5g3 | PAVING NOTCH, LONGITUDINAL | 2 | 31'-8 | 66 |
| 6g4 | WINGWALL, DOWELS | 36 | 5'-5 | 293 |
| 5h1 | COUNTERFORT, DOWELS | 24 | 4'-0 | 100 |
| 6h2 | COUNTERFORT 1, VERTICAL | 20 | 11'-9 | 353 |
| 6h3 | COUNTERFORT 2, VERTICAL | 20 | 9'-0 | 270 |
| 5h4 | COUNTERFORT, HORIZONTAL | 30 | 12'-0 | 375 |
| 5j1 | WING FOOTING, LONGITUDINAL | 32 | 11'-6 | 384 |
| 5j2 | WING FOOTING, TRANSVERSE | 32 | 11'-6 | 384 |
| 8j3 | PILE UPLIFT ANCHORS | 8 | 5'-0 | 107 |
| 5m1 | BEAM STEPS, TRANSVERSE | 20 | 8'-1 | 169 |
| 5n1 | BEAM STEPS, LONGITUDINAL | 20 | 4'-2 | 87 |
| 5n2 | BEAM STEPS, HOOP | 1 | 17'-5 | 18 |
| 5d5 | PAVING NOTCH DOWELS | 16 | 3'-6 | 58 |
| **plan total** | | | | **11,399** |

| Model mark | Size | Count | Length ft | Shape | lb | Location |
|---|---|---:|---:|---|---:|---|
| A | #11 | 7 | 33.67 | STR | 1,252 | South Abut cap top |
| B | #11 | 7 | 33.67 | STR | 1,252 | South Abut cap bottom |
| C | #5 | 4 | 33.67 | STR | 140 | South Abut cap side faces |
| S | #5 | 68 | 12.85 | TIE | 911 | South Abut cap stirrups |
| W | #5 | 70 | 9.75 | STR | 712 | South Abut backwall verticals |
| H | #5 | 18 | 33.67 | STR | 632 | South Abut backwall horizontals |
| **model total** | | | | | **4,900** | |


## 10. Open points and confidence

- **Source**: every plan weight is from the printed bar lists and reconciles to the printed totals; lengths are to the nearest inch (no fractions in the lists). Spacing "SPA @" texts were read from the pier text layers only where consistent with the bar counts; counts are authoritative.
- **Footing bar shapes** (hooks on the bottom bars, 7f3 vertical, 7f4/7f5 horizontal) are inferred from bar-list lengths against the footing dimensions and one raster section (16G p64, 1WB p21); the individual "SHAPE" sketches / bent-bar diagrams were not decoded for every pier. Hook legs: about 1'-9 (#10), 1'-11 to 2'-0 (#11), 2'-6 (#14).
- **Pile uplift anchors 8f6**: count per circled pile, orientation and welding/embedment are in the detail on the first pier sheet (design sheets 19-20 and following) and not yet decoded; the bar-list counts are exact (e.g. 1WB 36, 16G 30, 18G 36 per footing).
- **Top mat**: top clear 3" (as drawn); first bar 6" from the face; ends 2" short of the face (verified on every pier by bar length + 4" = footing dimension).
- **Multi-footing piers**: bar lists give the total of all footings; per footing = total / count. 11WB footing 3 is irregular (VARIES bars, 7f7/7f8 side bars): its row cannot be split per footing exactly.
- **Not examined**: pile-top to bottom-mat clearance on the plans for piers other than 1WB and 16G (same detail note "1" CL." is typical), column cage clashes with footing mat, cap steel clashes.
- Run artifacts (scratch, not part of the project): `temp\ftg-rebar\` (`planbars2.csv`, `q1.out`, `q2.out`, `q3.out`, scripts).
