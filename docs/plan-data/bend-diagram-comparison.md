# Bar bend diagrams: plans vs model (Design 116 I-80 WB, Design 316 Ramp G)

Scope: every substructure bar list - 35 piers (16 on I-80 WB incl. 6A and 11WB, 19 on Ramp G) and 3 abutments (West, East, South) - compared mark by mark with the
model's `BarShape` of the same mark. Deck slab bars (slab bar-list sheets 189-190 of Design 116 and the Ramp G equivalents) were NOT compared: the deck reinforcing is known to be
wrong overall (see `footing-rebar-comparison.md`) and is not rebuilt here. Branch `feature/bends`.

## 1. Result at a glance

**Dimensions (every printed segment dimension, hook/lap/offset and pin diameter D of every bend diagram that could be decoded and has a model bar):**

| | count |
|---|---:|
| printed dimensions compared with the model (incl. pin diameters) | 2613 |
| within tolerance | 1886 |
| beyond tolerance (or missing in the model) | 727 |
| not decodable (arc bars with radii, "VARIES" bars with MAX/MIN, diagrams that do not reconcile) | 527 |
| decoded but no model bar of that mark exists (plinth hairpins, footing-to-plinth dowels ...) | 138 |

**Marks:** 702 bend-diagram marks decoded and compared: **466 match, 236 mismatch**; 106 not decodable; 23 decoded without a model bar.
Straight bars (length only): 499 match (within 1/2"), 94 differ (length, or the model bar is bent where the plan bar is straight), 63 have no model bar.

Tolerance = the printed precision: 1/2" for dimensions printed in whole or half inches, 1/4" for dimensions printed with 1/4 and finer fractions, 1/4" for pin diameters.

## 2. How the plans were read

* Plan convention (printed on every bend-detail sheet): **"ALL DIMENSIONS ARE OUT TO OUT. D = PIN DIAMETER."** The bar-list length is the plain sum of the out-to-out segment
  dimensions along the bar - no bend deductions: 5a13 = 5'-3 + 2 x 2'-0 = 9'-3 (111"), 6c1 hoop = 2 x (3'-7 + 6'-8) + 1'-0 + 1'-0 = 22'-6 (270"), 5m1 = 5'-1 + 2 x 2'-8 = 10'-5 (125"),
  8j3 = 2 x 2'-6 = 5'-0. A 180 degree hook counts its dotted "allowance" (6c3: 6'-8 + 8" + 1'-0 = 100"), its 4 1/2" tail extension is not in the sum. Every mark's printed dimensions
  were reconciled against its bar-list length (the "sum = list length" tag in the tables); marks that do not reconcile are reported "not decoded", never guessed.
* The dimension strings of the sheets are PDF text (not OCR); fractions are separate glyph codes (octal 201-217 = 1/2, 1/4, 3/4, 1/8, 3/8, 5/8, 7/8, 1/16, 3/16, 5/16, 7/16, 9/16, 11/16,
  13/16, 15/16 - each confirmed on a raster clip) and are decoded exactly. Dimensions were assigned to marks by position (diagram cells, dimension labels such as "5m1 / 5n1") and
  checked by the length sum; the sheets of the pier bar-list pages (Design 116 p. 22 ... 82, Design 316 p. 21 ... 77) share one diagram layout; the bend-detail columns of Pier 1WB and 1G (and the
  cap elevations / sections of 1WB, 4WB, 1G) were checked on raster clips, all others by the exact text decode plus the length sum of every mark; the three abutment sheets (116 p. 88 / 92, 316 p. 83) were READ ON RASTER CLIPS dimension by dimension (their diagrams are too irregular for the automatic
  assignment) and every row reconciles with its bar-list length (`tools/abut_hand.py`).
* Model side (after this work): `BarShape.Dimensions` = the printed out-to-out values, centerline = outline inset by half a bar diameter with the printed pin D, scheduled length = the
  listed length (`SubstructurePlanShapes.cs`). Model shapes that were NOT changed carry centerline segment lengths from the older generator; they are converted to out-to-out here
  (+ db at a segment between two bends, + db/2 at a segment ending at a bend) and merged when the kink is under 12 degrees (bars that follow the cap profile).
* Tables: dimensions are listed largest first ("plan vs model"); a difference is printed only when it is beyond the tolerance. Orientation (which leg is which) is not compared - a U bar
  with legs 2'-8 / base 5'-1 matches a model U bar with the same three values.
* Tooling: `tools/plantext.py` (content-stream text + fraction glyphs), `bendx.py` / `extract_all.py` / `shapes.py` (dimension extraction, sum reconciliation), `compare.py`,
  `make_doc.py`; model values come from `sbm run` scripts on a copy of the model (`BarMark.Shape`, `Diagnostics`).

## 3. What was changed in the model (shapes now equal to the plan diagrams)

All dimensions below equal the printed ones on every pier / abutment where the diagram was decoded (`SubstructurePlanShapes.cs`, inputs `PlanBarGroup.Shape/Dims/Pin`, data
appended to `samples/plans/pier-rebar-*.csx` as `;S=shape:dims:pin`, abutment lines as `.Shaped(...)` in `abutments-*.csx`).

| mark(s) | plan bend diagram | model before | model now |
|---|---|---|---|
| 5m1 / 5n1 pedestal bars (Ramp G: 5m1 4'-8 / 5n1 5'-8, legs 2'-3) | U: base 5'-1 / 5'-3 (I-80), legs 2'-8 | U with base clamped to the cap width and legs from the listed length (e.g. 5n1 on 1WB: base 5'-7 3/8, legs 2'-5 13/16) | U with the printed base and legs; 6 bars per pedestal at equal spaces, end bars 4" from the pedestal edges (pedestal plan "4, 5 EQ SPA, 4") |
| 5a13 / 6a14 end bars | U 5'-3 x 2 legs 2'-0 (wide caps 7'-3) | U 4'-7 3/8 + 2 x 2'-3 13/16 (derived) | U with the printed dims, 4 per end stacked at equal spaces |
| 5a14 / 6a15 end "vertical" bars | bar in the vertical longitudinal plane: leg 2'-0, top run 4'-0 (Ramp G 5'-0), slanted leg 10" (Ramp G 1'-0) with 2 1/2" run, parallel to the raked end face | straight vertical bar of the listed length (outside the cap near the beaks) | the printed shape, 6 per end across the width at 9" from the faces, top at the cap top less the cover, pushed in from the raked end face only as far as the clear cover needs |
| 5c5 / 5c6 (Ramp G 5c3 / 5c4) hairpins | U: base 5'-5 (7'-5) = full width less 3 1/2" clear at both faces, legs 2'-11 / 2'-0 (3'-2 / 3'-10) | narrow hairpin (base 4", legs of half the listed length) | the printed U, base across the width, legs hanging down from the top; stations in the plan's zone order and counts (2 bars per station) - see 5 |
| 4c7 / 4c5 transverse | U: base 4'-8 (7'-5) with 4 1/2" legs | U with base = length less 9" | the printed U (already equal on I-80; Ramp G base 7'-5) |
| 11g / 10g footing bottom mats | U: legs 2'-0 (24"), base e.g. 15'-6 / 17'-6 | legs 23" derived from the footing size | printed legs / base |
| 6c1 / 6c2 / 5n2 hoops | rectangle w x h with two 1'-0 (6") lap tails (3'-7 x 6'-8 on I-80 caps, 5'-2 x 5'-4 pedestal hoops) | stirrup 4'-8 wide x derived height with 135 degree hooks | the printed w x h with the lap tails, centred across the cap (the section sketch puts them off-centre, see 5) |
| 6b2 / 6b3 cantilever bottom | flat 2'-0 + slanted 9'-1 (8'-9 x 2'-4 1/2) | 3-segment polyline from the soffit profile | the printed flat + slant, starting at the end face, descending toward the columns |
| 5a11 / 5a12 (I-80) | straight, 5'-3 / 7'-2: short bars at the LEFT / RIGHT end of the cap ("5a11 (E.F.)", "5a12 (E.F.)" on the elevation) | straight bars centred mid-cap | same bars at the cap ends |
| abutment marks 5d3 4d6 4d8 5m1 5j1 6g4 6h2 6h3 5h4 5j2 5n2 5b2 (South) 5g2 8j3 | U, L, hat, hoop, V diagrams (hand-read) | straight schedule-only bars | schedule shapes with the printed dimensions (still no 3D bar: placements are not proven) |

## 4. Clear-cover containment before / after (`CheckContainment`, plan cover + bar radius, 1/8" tolerance)

Counts are bars outside their host concrete (units = piers with at least one such bar). Remaining violations are raised (the generator warning per unit), never resized.
Three columns separate the two things that changed: **A** = the baseline model (old shapes, old check); **B** = the same old shapes checked with the corrected relief rule;
**C** = the plan-true shapes (this work) with the corrected rule.

| bar kind (plan mark, what it is) | A: before (old shapes, old check) | B: old shapes, check with the relief fix | C: after (plan shapes + relief fix) |
|---|---:|---:|---:|
| 5m1 pedestal bars | 1036 (31) | 1036 (31) | 170 (4) |
| 5n1 pedestal bars | 609 (34) | 603 (34) | 169 (10) |
| 5a14 end bars | 207 (17) | 207 (17) | 0 (0) |
| 6a15 end bars | 71 (18) | 71 (18) | 3 (1) |
| 6b2 cantilever bottom | 0 (0) | 0 (0) | 136 (17) |
| 5a12 side faces | 34 (17) | 34 (17) | 34 (17) |
| 4c7 transverse | 46 (17) | 15 (1) | 0 (0) |
| 4c5 transverse | 60 (18) | 0 (0) | 0 (0) |
| 5n2 pedestal bars | 14 (1) | 14 (1) | 24 (1) |
| 5c5 hairpins | 0 (0) | 0 (0) | 32 (6) |
| 5c3 hairpins | 0 (0) | 0 (0) | 28 (1) |
| 5a15 end vertical | 9 (1) | 9 (1) | 9 (1) |
| 5n3 pedestal bars | 12 (1) | 8 (1) | 0 (0) |
| 5a13 end transverse | 0 (0) | 0 (0) | 17 (6) |
| 6a16 end vertical | 3 (1) | 3 (1) | 3 (1) |
| 5a11 side faces | 2 (1) | 2 (1) | 2 (1) |
| 5c4 hairpins | 0 (0) | 0 (0) | 4 (1) |
| 5n2 pedestal hoops | 0 (0) | 0 (0) | 2 (1) |
| **total** | **2103** | **2002** | **633** |

* **Check correction (A -> B, 2103 -> 2002)**: `LateralSetback` assumed the full 6" relief setback wherever a relief line did not exist (the 20" tips of the caps, where the plans' arcs start), so
  every bar near a cap tip was reported "outside the narrow section". Each relief line now continues level beyond its end. This alone clears 4c5 (60 -> 0) and most 4c7.
* **Plan-true shapes (B -> C, 2002 -> 633)**: pedestal U bars 5m1 / 5n1 1,639 -> 339, end bars 5a14 207 -> 0, 6a15 71 -> 3, 4c7 15 -> 0, 4c5 0, 5n3 8 -> 0.
  The bars were also made to follow the sloped cap tops (a level base goes under the lowest point of the top; stations at the ends of the top face) - an earlier build of this
  work had them stacked at one end of sloped caps (that error is fixed and not part of the numbers above).
* **Superseded 2026-10-11:** the cap-cover work brought the 633 to 178 (pedestal U bars in the pedestal top, 5n2 hoops round the named girders' pedestals, 5a13 in the
  full-width zone, 5a11 / 5a12 at their ends, 5a15 shape, per-face relief lines, pedestal blocks in the check); see `docs/plan-data/cap-cover-containment.md`. The list below is the 633 state.
* **Remaining (633 bars)**, all with the plan dimensions kept:
  * 5m1 / 5n1 pedestal bars (339): 5n1 on 1WB .. 4WB / 6WB (6 .. 12 bars, 3 3/16": the base 5'-3 of the end pedestal under the 5'-0 lower section); 5WB (36 bars, 21": 96" cap whose
    model section is narrower than the plan's at that pedestal); 11WB (24 + 56 bars, 1/2" .. 5 9/16"); 6WB (66 bars 1 1/2"); Ramp G 5G / 9G / 14G (24 bars, 5 1/2" .. 6": legs 2'-3 deeper than the
    cap near the beak) and 5G / 14G 5m1 (24 bars, 1 11/16"). The plans dimension neither the elevation of the base nor an alternative bar there, so nothing is moved: the cap depth / relief of the
    model at those stations is what to confirm.
  * 6b2 cantilever bottom bars (136): the printed flat 2'-0 + slant (rise 2'-4 1/2 in 8'-9) against the model's soffit: 1 5/16" (1WB .. 4WB, 16G), 1 7/8" (5WB) and 6 .. 18" on 6WB .. 15WB / 17G, whose
    model soffit near the tip is flatter than the printed slant (the soffit profiles of those piers were scripted from the elevation, not from this bar).
  * 5c hairpins (64): 4 3/16" at the stations over the shallow relief of 2WB, 3WB, 7WB, 9WB, 10WB, 13WB, 6A (5c3) and 19G - the leg is longer than the full-width zone of the model's relief arc there.
  * 5a13 / 6a14 end U bars (17), 5n2 pedestal bars (24, 1/2") and hoops (2, 3"), 5a15 / 6a16 / 5a11 of 6A (the 6A variants of the end bars were not given plan shapes).
  * 5a12 side faces (34): the long relief bars of the Ramp G caps (not the short I-80 end bars); straight at fractional heights, "cap bar layout not adapted to the relief" as before.

### Cap section "3 1/2" CL." (textured face)

Sections A-A and B-B of every pier cap (Design 116 p. 20 / sheet 19 and the Ramp G equivalents) show **3 1/2" CL.** between the cap face and the **vertical legs of the 5c hairpins**
(the big rounded outline in the section) at both faces, including the textured face with the "SEE DETAIL B" relief step; the face-side skin bars (5a6 .. 5a9, 6a3 .. 6a5 "(E.F.)", circled in the
section) lie inside those legs. 72" (96") cap width less 2 x 3 1/2" = 65" (89") = the printed base of 5c5 / 5c6 (5c3 / 5c4) - the dimension and the bar agree exactly, so the 3 1/2" governs
the **5c hairpin legs and base** only (it is the reason their base is 5'-5, not 5'-8). It does not govern the skin bars, hoops, or end bars (those keep the plan's 2" cover plus the hoop
diameter: top / bottom bar rows are dimensioned "3 1/2" to the bar centre" = 2" + 6 bar + half an 11 bar). Applied: the hairpin base width only (it is the printed dimension); the
model's cap cover (2") is unchanged.

**Answer to open question (5), 2026-10-11 (cap-cover work, `docs/plan-data/cap-cover-containment.md`):** the plans print the 3 1/2" CL. only from the two (full-width) cap faces to the
5c hairpin legs, at BOTH faces (Design 116 PDF 20 sections A-A / B-B, pier 6A PDF 40 sections A-A / B-B, Ramp G equivalents) - it is not a textured-face-only value. The containment
check now verifies it for the hairpins (`SubstructureGenerator.HairpinFaceClear`; all hairpins pass). For the other bars at the textured face the only statement is the MOCKUP note
"A SINGLE MAT OF NO. 5 REINFORCING BARS ... SHALL BE SET 2 INCHES CLEAR TO THE BOTTOM OF THE TEXTURED FACE" (PDF 83; texture B up to 1 1/2" deep, i.e. 3 1/2" from the form face);
no production cover is stated, so every pier with a relief step carries an UNRESOLVED warning and the check keeps the general 2" ("UNLESS OTHERWISE NOTED").

## 5. Unresolved items (raised, not decoded or not modeled)

1. **Hairpin / hoop station layouts**: the plan's gap dimensions between zones ("1'-4", "1'-0") are not read - stations are equal spaces from the margin (the plan's "4" / "6")
   with the plan's zone order and counts (`tools/zones.py`); two bars per station are placed side by side (the plan does not dimension their offset). The hoops 6c1 / 6c2 are
   centred across the cap although the section A-A sketch shows them 3" from one face (which face is not decoded).
2. **Top bars 11a1 / 11a2 (L: 37'-9 + 2'-0 leg; Ramp G 10a1 U 34'-9 + 2 x 1'-10)**: the shape is decoded but the layout (which bars end at which cap end with the leg down, 3'-0 min laps) is not;
   the model still places straight bars of the listed length centred on the cap. Bottom bars 8b1 / 9b1 and the relief bars 5a9 / 5a10 (sinuous, R = 25' .. 175') are likewise not modeled as drawn.
3. **Column and plinth bars**: hoops 6e1 (octagon with lapped tails), ties 5e2 (90 degree leg 6" + 180 degree hook with 4" tail), plinth hairpins 6e3, plinth hoops 6e4, plinth verticals,
   footing-to-plinth dowels, and the 6c3 / 6c4 ties (vertical run with a 180 degree hook) are decoded but the model's 135 degree-hook ties / crossties / flare bars are a different detail;
   not changed (column work belongs to the column-rebar model).
4. **Footing side bars 7f4 / 7f5 (L: run + 1'-2 leg) and 7g2 (U with 1'-0 legs, 10g2 variants)**: dimensions decoded; the end and direction of the leg are not dimensioned in the
   layout sheet - not changed.
5. **Abutments**: only 5b1 hoops, 5d2 / 6d1 verticals and 9a1 are placed in 3D; all other marks have schedule shapes only. 5g2 (West), 5d4, 5b3, 5b4 and the East 5b2 diagrams (hook with slant) are
   read and reconciled (see the tables) but not given a model shape (slanted segments need their angles). 5b1 in the model keeps 135 degree hooks (plan: 6" lap tails).
6. **Pedestal 5n2 hoops** (printed "AT GIRDERS C & D ONLY", Pier 1WB): (2026-10-11) the notes are read for 1WB-3WB (C & D), 4WB (B-D), 6WB (F-I) and 5WB
   ("AT AHEAD BRG. GIRDERS G-I & AT BACK BRG. GIRDERS B-D ONLY"); the hoops go round those pedestal blocks (`PlanBarGroup.Girders`, csx `;G=`). 5WB: the note selects 4
   shared blocks for 3 hoops - UNRESOLVED, mid-cap as before. 9G (4 hoops at one back pedestal): pedestals not set - UNRESOLVED, mid-cap.
7. **Pedestal size**: the plan pedestals (5'-6 x 5'-8 on 1WB) differ from the model's bearing-seat pedestals (58 x 58"), which changes where the 4" end bars fall.
8. **Deck slab bars**: not compared (see scope).

## 6. Per-unit tables

Column "plan diagram": dimensions as printed (ft-in), largest first; "(sum = list length)" = the sum rule holds; "(read on clip ...)" = abutment diagrams transcribed from raster clips.
Verdicts: **match** (all dimensions within tolerance and the same number of segments), **MISMATCH** (a dimension beyond tolerance, a missing / extra segment, a pin that differs, or a model
bar that is straight / bent where the plan is bent / straight), **not decoded**, **not modeled** (no model bar with that mark).

### Pier 1WB (Design 116, sheet PDF p.22)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 37'-9" + 2'-0" (sum = list length) ; D 1'-0" | 39'-9" | STR 39'-9" | 37'-9" vs 39'-9" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 37'-7" | STR 37'-7" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 46'-11" | STR 46'-11" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 45'-6" | STR 45'-6" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 42'-10" | STR 42'-10" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 46'-8" | STR 46'-8" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 42'-5" | STR 42'-5" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 28'-7" | STR 28'-7" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 124'-8 1/2", 47'-9 1/2", 124'-8 1/2" | 47'-8" | STR 47'-7" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 82'-3 1/2" | 47'-7" | STR 47'-7" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 5'-3" | STR 5'-3" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 7'-2" | STR 7'-2" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 41'-5" | STR 41'-5" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 9'-1" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-1" | CS 2'-0" + 9'-1" ; D 4 1/2" (as scheduled (A,B...)) | 9'-1" vs 9'-1"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-7" + 3'-7" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 22'-6" | HOOP 6'-8" + 3'-7" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-7" vs 3'-7"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-7" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 15'-8" | STR 15'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 17'-8" | STR 17'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 15'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 16'-10" | BENT 15'-6" (straight within 12 deg (follows the cap profile)) | 15'-8" vs 15'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 17'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 18'-10" | BENT 17'-6" (straight within 12 deg (follows the cap profile)) | 17'-8" vs 17'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 15'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 19'-6" | U 2'-0" + 15'-6" + 2'-0" ; D 1'-0" | 15'-6" vs 15'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 17'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 21'-6" | U 2'-0" + 17'-6" + 2'-0" ; D 1'-0" | 17'-6" vs 17'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-1" + 2'-8" + 2'-8" (sum = list length) ; D 2 1/2" | 10'-5" | U 2'-8" + 5'-1" + 2'-8" ; D 2 1/2" | 5'-1" vs 5'-1"; 2'-8" vs 2'-8"; 2'-8" vs 2'-8" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-3" + 2'-8" + 2'-8" (sum = list length) ; D 2 1/2" | 10'-7" | U 2'-8" + 5'-3" + 2'-8" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-8" vs 2'-8"; 2'-8" vs 2'-8" | match |
| 5n2 | CAP, PEDESTAL, HOOP | bent: 5'-4" + 5'-4" + 5'-2" + 5'-2" + 6" + 6" (sum = list length) ; D 2 1/2" | 22'-0" | HOOP 5'-4" + 5'-2" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 5'-4" vs 5'-4"; 5'-2" vs 5'-2"; 6" vs 6" | match |
| 11d1 | COLUMN, VERTICAL | straight | 24'-0" | STR 24'-0" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 16'-9" + 2'-0" (sum = list length) ; D 1'-0" | 18'-9" | L 16'-9" + 2'-0" ; D 1'-0" | 16'-9" vs 16'-9"; 2'-0" vs 2'-0" | match |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |

### Pier 2WB (Design 116, sheet PDF p.26)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 36'-9" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | STR 38'-9" | 36'-9" vs 38'-9" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 36'-7" | STR 36'-7" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 45'-5" | STR 45'-5" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 44'-0" | STR 44'-0" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 41'-4" | STR 41'-4" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 45'-3" | STR 45'-3" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 41'-1" | STR 41'-1" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 116'-2 1/2", 45'-3 1/2", 116'-2 1/2" | 46'-2" | STR 46'-1" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 77'-3 1/2" | 46'-1" | STR 46'-1" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 6'-11" | STR 6'-11" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 39'-10" | STR 39'-10" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 9'-1" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-1" | CS 2'-0" + 9'-1" ; D 4 1/2" (as scheduled (A,B...)) | 9'-1" vs 9'-1"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-7" + 3'-7" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 22'-6" | HOOP 6'-8" + 3'-7" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-7" vs 3'-7"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-7" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 28'-0" | STR 28'-0" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 12'-7" + 2'-0" (sum = list length) ; D 1'-0" | 14'-7" | L 12'-7" + 2'-0" ; D 1'-0" | 12'-7" vs 12'-7"; 2'-0" vs 2'-0" | match |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 15'-8" | STR 15'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 15'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 16'-10" | BENT 15'-6" (straight within 12 deg (follows the cap profile)) | 15'-8" vs 15'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 17'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 18'-10" | BENT 17'-6" (straight within 12 deg (follows the cap profile)) | 17'-8" vs 17'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 15'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 19'-6" | U 2'-0" + 15'-6" + 2'-0" ; D 1'-0" | 15'-6" vs 15'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 17'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 21'-6" | U 2'-0" + 17'-6" + 2'-0" ; D 1'-0" | 17'-6" vs 17'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-1" + 2'-8" + 2'-8" (sum = list length) ; D 2 1/2" | 10'-5" | U 2'-8" + 5'-1" + 2'-8" ; D 2 1/2" | 5'-1" vs 5'-1"; 2'-8" vs 2'-8"; 2'-8" vs 2'-8" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-3" + 2'-8" + 2'-8" (sum = list length) ; D 2 1/2" | 10'-7" | U 2'-8" + 5'-3" + 2'-8" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-8" vs 2'-8"; 2'-8" vs 2'-8" | match |
| 5n2 | CAP, PEDESTAL, HOOP | bent: 5'-4" + 5'-4" + 5'-2" + 5'-2" + 6" + 6" (sum = list length) ; D 2 1/2" | 22'-0" | HOOP 5'-4" + 5'-2" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 5'-4" vs 5'-4"; 5'-2" vs 5'-2"; 6" vs 6" | match |

### Pier 3WB (Design 116, sheet PDF p.30)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 36'-9" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | STR 38'-9" | 36'-9" vs 38'-9" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 36'-7" | STR 36'-7" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 45'-5" | STR 45'-5" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 44'-0" | STR 44'-0" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 41'-4" | STR 41'-4" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 45'-2" | STR 45'-2" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 41'-1" | STR 41'-1" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 116'-2 1/2", 45'-3 1/2", 116'-2 1/2" | 46'-2" | STR 46'-1" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 77'-3 1/2" | 46'-1" | STR 46'-1" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 6'-11" | STR 6'-11" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 39'-10" | STR 39'-10" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 9'-1" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-1" | CS 2'-0" + 9'-1" ; D 4 1/2" (as scheduled (A,B...)) | 9'-1" vs 9'-1"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-7" + 3'-7" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 22'-6" | HOOP 6'-8" + 3'-7" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-7" vs 3'-7"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-7" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN, VERTICAL | straight | 21'-0" | STR 21'-0" | +0.00" | match |
| 18d2 | COLUMN, VERTICAL | straight | 26'-0" | STR 26'-0" | +0.00" | match |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 13'-8" + 3'-5" (sum = list length) ; D 2'-0" | 17'-1" | L 13'-8" + 3'-5" ; D 2'-0" | 13'-8" vs 13'-8"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 8'-8" + 3'-5" (sum = list length) ; D 2'-0" | 12'-1" | L 8'-8" + 3'-5" ; D 2'-0" | 8'-8" vs 8'-8"; 3'-5" vs 3'-5" | match |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-0" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-0" | CROSSTIE 2'-8 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-0" vs 2'-8 1/2" (-3.48"); 6" vs missing | MISMATCH |
| 8f1 | FOOTING, TOP | straight | 21'-8" | STR 21'-8" | +0.00" | match |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM | U: 21'-8" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 25'-8" | U 2'-0" + 21'-8" + 2'-0" ; D 1'-0" | 21'-8" vs 21'-8"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 6'-1" + 2'-8" + 2'-8" (sum = list length) ; D 2 1/2" | 11'-5" | U 2'-8" + 6'-1" + 2'-8" ; D 2 1/2" | 6'-1" vs 6'-1"; 2'-8" vs 2'-8"; 2'-8" vs 2'-8" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-3" + 2'-8" + 2'-8" (sum = list length) ; D 2 1/2" | 10'-7" | U 2'-8" + 5'-3" + 2'-8" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-8" vs 2'-8"; 2'-8" vs 2'-8" | match |
| 5n2 | CAP, PEDESTAL, HOOP | bent: 6'-2" + 6'-2" + 5'-4" + 5'-4" + 6" + 6" (sum = list length) ; D 2 1/2" | 24'-0" | HOOP 6'-2" + 5'-4" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-2"; 5'-4" vs 5'-4"; 6" vs 6" | match |

### Pier 4WB (Design 116, sheet PDF p.34)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 37'-3" + 2'-0" (sum = list length) ; D 1'-0" | 39'-3" | STR 39'-3" | 37'-3" vs 39'-3" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 37'-1" | STR 37'-1" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 33'-5" | STR 33'-5" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | STR 32'-0" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 55'-1" | BENT 55'-1" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 33'-2" | STR 33'-2" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 29'-10" | STR 29'-10" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 20'-0" | STR 20'-0" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 25'-9 1/2", 61'-2 1/2", 61'-2 1/2" | 34'-3" | STR 34'-2" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 42'-3 1/2" | 34'-2" | STR 34'-2" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 3'-8" | STR 3'-8" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 4'-11" | STR 4'-11" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 47'-0" | STR 47'-0" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 9'-1" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-1" | CS 2'-0" + 9'-1" ; D 4 1/2" (as scheduled (A,B...)) | 9'-1" vs 9'-1"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-7" + 3'-7" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 22'-6" | HOOP 6'-8" + 3'-7" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-7" vs 3'-7"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-7" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 35'-0" | STR 35'-0" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-1" + 2'-0" (sum = list length) ; D 1'-0" | 15'-1" | L 13'-1" + 2'-0" ; D 1'-0" | 13'-1" vs 13'-1"; 2'-0" vs 2'-0" | match |
| 7d12 | PLINTH, VERTICAL | straight | 11'-10" | STR 17'-3" | +65.00" | length +65.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 5'-4" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 5'-4" vs 5'-5 3/4" (+1.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPIN | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 21'-2" | STR 21'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 14'-2" | STR 14'-2" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 21'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 22'-4" | BENT 21'-0" (straight within 12 deg (follows the cap profile)) | 21'-2" vs 21'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 14'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 15'-4" | BENT 14'-0" (straight within 12 deg (follows the cap profile)) | 14'-2" vs 14'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 21'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 25'-0" | U 2'-0" + 21'-0" + 2'-0" ; D 1'-0" | 21'-0" vs 21'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 7g2 | FOOTING, BOTTOM, LONGITUDINAL | bent: (not reconciled) ; D 5 1/4" | 16'-4" | BENT 1'-1 7/16" + 14'-2" + 1'-1 7/16" |  | not decoded |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-1" + 2'-9" + 2'-9" (sum = list length) ; D 2 1/2" | 10'-7" | U 2'-9" + 5'-1" + 2'-9" ; D 2 1/2" | 5'-1" vs 5'-1"; 2'-9" vs 2'-9"; 2'-9" vs 2'-9" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-3" + 2'-9" + 2'-9" (sum = list length) ; D 2 1/2" | 10'-9" | U 2'-9" + 5'-3" + 2'-9" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-9" vs 2'-9"; 2'-9" vs 2'-9" | match |
| 5n2 | CAP, PEDESTAL, HOOP | hoop: 5'-4" + 5'-2" + 6" + 6" (read on raster clip 2026-10-11; sum = list length) ; D 2 1/2" | 22'-0" | HOOP 5'-2" + 5'-4" + 6" ; D 2 1/2" | 5'-4" vs 5'-4"; 5'-2" vs 5'-2"; 6" vs 6" | match (hand-updated, cap-cover work) |

### Pier 5WB (Design 116, sheet PDF p.38)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 9a1 | CAP, TOP, LONGITUDINAL | L: 35'-10" + 1'-7" (sum = list length) ; D 9 1/2" | 37'-5" | STR 37'-5" | 35'-10" vs 37'-5" (+19.00"); 1'-7" vs missing pin: plan 9 1/2" / model none (straight) | MISMATCH (model straight) |
| 9a2 | CAP, TOP, LONGITUDINAL | straight | 35'-9" | STR 35'-9" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 33'-8" | STR 33'-8" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-6" | STR 32'-6" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 55'-1" | BENT 55'-1" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 33'-3" | STR 33'-3" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 29'-10" | STR 29'-10" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 20'-0" | STR 20'-0" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: 17'-6" + 15'-6" + 5" (sum = list length) ; R 25'-9 1/2", 61'-2 1/2", 61'-2 1/2" | 33'-5" | STR 33'-5" | 17'-6" vs 33'-5" (+191.00"); 15'-6" vs missing; 5" vs missing | MISMATCH (model straight) |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 42'-3 1/2" | 34'-6" | STR 34'-6" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 2'-11" | STR 2'-11" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 4'-10" | STR 4'-10" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 2 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, END, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 5a15 | CAP, END, VERTICAL | bent (read on raster clip 2026-10-11): 10" top leg + 4'-5" down the end face + 2'-0" slanted along the soffit bevel (6" drop) (sum = list length) ; D 2 1/2" | 7'-3" | A15 10" + 4'-5" + 2'-0" (6" drop) ; D 2 1/2", left end only (E=-1) | - | match (hand-updated, cap-cover work) |
| 9b1 | CAP, BOTTOM, LONGITUDINAL | straight | 47'-0" | BENT 47'-0" | +0.00" | MISMATCH (model bent) |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 9'-1" + 2'-0" (sum = list length) | 11'-1" | CS 2'-0" + 9'-1" ; D 4 1/2" (as scheduled (A,B...)) | 9'-1" vs 9'-1"; 2'-0" vs 2'-0" | match |
| 6b3 | CAP, BOTTOM, CANTILEVER | L: 8'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-0" | CS 2'-0" + 8'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-0" vs 8'-0"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-0" | HOOP 6'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | TIE 6'-8" + 6'-8" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: (not reconciled) ; D 4 1/2" | 8'-4" | BENT 10" + 6'-8" + 10" |  | not decoded |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 6" + 6'-8" + 6" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 13'-3" | U 2'-11" + 7'-5" + 2'-11" ; D 2 1/2" | 7'-5" vs 7'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 11'-5" | U 2'-0" + 7'-5" + 2'-0" ; D 2 1/2" | 7'-5" vs 7'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 6'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 7'-5" | U 4 1/2" + 6'-8" + 4 1/2" ; D 2" | 6'-8" vs 6'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 38'-0" | STR 38'-0" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 16'-9" + 2'-0" (sum = list length) ; D 1'-0" | 18'-9" | L 16'-9" + 2'-0" ; D 1'-0" | 16'-9" vs 16'-9"; 2'-0" vs 2'-0" | match |
| 7d12 | PLINTH, VERTICAL | straight | 12'-10" | STR 17'-9" | +59.00" | length +59.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 7'-8" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-7" + 2'-9" + 2'-9" (sum = list length) ; D 2 1/2" | 10'-1" | U 2'-9" + 4'-7" + 2'-9" ; D 2 1/2" | 4'-7" vs 4'-7"; 2'-9" vs 2'-9"; 2'-9" vs 2'-9" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 7'-3" + 2'-9" + 2'-9" (sum = list length) ; D 2 1/2" | 12'-9" | U 2'-9" + 7'-3" + 2'-9" ; D 2 1/2" | 7'-3" vs 7'-3"; 2'-9" vs 2'-9"; 2'-9" vs 2'-9" | match |
| 5n2 | CAP, PEDESTAL, HOOP | bent: 7'-4" + 7'-4" + 4'-8" + 4'-8" + 6" + 6" (sum = list length) ; D 2 1/2" | 25'-0" | HOOP 7'-4" + 4'-8" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 7'-4" vs 7'-4"; 4'-8" vs 4'-8"; 6" vs 6" | match |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 19'-8" | STR 19'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 15'-8" | STR 15'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 19'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 20'-10" | BENT 19'-6" (straight within 12 deg (follows the cap profile)) | 19'-8" vs 19'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 15'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 16'-10" | BENT 15'-6" (straight within 12 deg (follows the cap profile)) | 15'-8" vs 15'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | U: 19'-6" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 23'-2" | U 1'-10" + 19'-6" + 1'-10" ; D 10 3/4" | 19'-6" vs 19'-6"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 15'-6" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 19'-2" | U 1'-10" + 15'-6" + 1'-10" ; D 10 3/4" | 15'-6" vs 15'-6"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |

### Pier 6A (Design 116, sheet PDF p.42)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 51'-4" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 55'-0" | BENT 1'-9 1/16" + 51'-8 3/4" + 1'-9 1/16" (OTO from centerline) | 51'-4" vs 51'-8 3/4" (+4.75"); 1'-10" vs 1'-9 1/16" (-0.92"); 1'-10" vs 1'-9 1/16" (-0.92") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 51'-1" | STR 51'-1" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 51'-9" | BENT 51'-9" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 43'-10" | BENT 43'-10" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 27'-2" | BENT 27'-2" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 19'-1" | BENT 19'-1" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 51'-11" | BENT 51'-11" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 29'-0" | BENT 29'-0" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 63'-9 1/2" | 52'-10" | BENT 5 5/16" + 52'-1" + 4 15/16" |  | not decoded |
| 5a14 | CAP, ENDS, TRANSVERSE | straight | 11'-3" | BENT 11'-3" | +0.00" | MISMATCH (model bent) |
| 6a15 | CAP, END, VERTICAL | U: 4'-6" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 7'-6" | STR 7'-6" | 4'-6" vs 7'-6" (+36.00"); 2'-0" vs missing; 1'-0" vs missing pin: plan 4 1/2" / model none (straight) | MISMATCH (model straight) |
| 6a16 | CAP, END, VERTICAL | U: 4'-9" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 7'-9" | STR 7'-9" | 4'-9" vs 7'-9" (+36.00"); 2'-0" vs missing; 1'-0" vs missing pin: plan 4 1/2" / model none (straight) | MISMATCH (model straight) |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 21'-4 1/2" + 20'-7 1/2" + 10'-11" (sum = list length) ; D 6" | 52'-11" | BENT 31'-7 5/8" + 21'-3 1/4" (OTO from centerline) | 21'-4 1/2" vs 31'-7 5/8" (+123.10"); 20'-7 1/2" vs 21'-3 1/4" (+7.77"); 10'-11" vs missing pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-2" + 8'-2" + 4'-8" + 4'-8" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 27'-8" | HOOP 8'-2" + 4'-8" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-2" vs 8'-2"; 4'-8" vs 4'-8"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 4'-11" MIN., 8'-1" MAX. ; D 4 1/2" | VARIES | HOOP 8'-1" + 4'-8" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 2'-10" + 2'-10" (sum = list length) ; D 2 1/2" | 13'-1" | U 2'-10" + 7'-5" + 2'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 2'-10" vs 2'-10"; 2'-10" vs 2'-10" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-5" + 3'-5" (sum = list length) ; D 2 1/2" | 14'-3" | U 3'-5" + 7'-5" + 3'-5" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-5" vs 3'-5"; 3'-5" vs 3'-5" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 35'-11" | STR 35'-11" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 16'-9" + 2'-0" (sum = list length) ; D 1'-0" | 18'-9" | L 16'-9" + 2'-0" ; D 1'-0" | 16'-9" vs 16'-9"; 2'-0" vs 2'-0" | match |
| 6e1 | COLUMN, HOOPS | arcs: 6'-2" + 6'-2" + 3'-7 1/2" + 3'-5" + 2'-10" + 2'-10" (sum = list length) ; R 83'-8 1/2" ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 2'-10" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-2" + 2'-7" + 2'-7" (sum = list length) ; D 2 1/2" | 9'-4" | U 2'-7" + 4'-2" + 2'-7" ; D 2 1/2" | 4'-2" vs 4'-2"; 2'-7" vs 2'-7"; 2'-7" vs 2'-7" | match |
| 8m2 | CAP, STEP, LONGITUDINAL | straight | 51'-5" | STR 51'-5" | +0.00" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 3'-6" + 2'-7" + 2'-7" (sum = list length) ; D 2 1/2" | 8'-8" | U 2'-7" + 3'-6" + 2'-7" ; D 2 1/2" | 3'-6" vs 3'-6"; 2'-7" vs 2'-7"; 2'-7" vs 2'-7" | match |
| 6n2 | CAP, STEP, HAIRPIN | U: 3'-6" + 2'-10" + 2'-10" (sum = list length) ; D 4 1/2" | 9'-2" | U 2'-10" + 3'-6" + 2'-10" ; D 4 1/2" | 3'-6" vs 3'-6"; 2'-10" vs 2'-10"; 2'-10" vs 2'-10" | match |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 19'-8" | STR 19'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 28'-8" | STR 28'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 19'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 20'-10" | BENT 19'-6" (straight within 12 deg (follows the cap profile)) | 19'-8" vs 19'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 28'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 29'-10" | BENT 28'-6" (straight within 12 deg (follows the cap profile)) | 28'-8" vs 28'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | bent: (not reconciled) ; D 1'-0" | 23'-6" | U 2'-0" + 19'-6" + 2'-0" ; D 1'-0" |  | not decoded |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | bent: (not reconciled) ; D 1'-0" | 32'-6" | U 2'-0" + 28'-6" + 2'-0" ; D 1'-0" |  | not decoded |

### Pier 6WB (Design 116, sheet PDF p.46)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 43'-6" + 2'-0" (sum = list length) ; D 1'-0" | 45'-6" | STR 45'-6" | 43'-6" vs 45'-6" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 43'-3" | STR 43'-3" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 55'-5" | STR 55'-5" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 54'-2" | STR 54'-2" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 51'-6" | STR 51'-6" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 55'-2" | STR 55'-2" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 50'-6" | STR 50'-6" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 34'-0" | STR 34'-0" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: 27'-7" + 27'-7" + 10" (sum = list length) ; R 175'-8 1/2", 66'-3 1/2", 175'-8 1/2" | 56'-0" | STR 56'-0" | 27'-7" vs 56'-0" (+341.00"); 27'-7" vs missing; 10" vs missing | MISMATCH (model straight) |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: 36'-6" + 19'-4" + 3 1/2" (sum = list length) ; R 114'-3 1/2" | 56'-2" | STR 56'-0" | 36'-6" vs 56'-0" (+234.00"); 19'-4" vs missing; 3 1/2" vs missing | MISMATCH (model straight) |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 6'-4" | STR 6'-4" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 8'-8" | STR 8'-8" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 36'-1" | STR 36'-1" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 9'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-0" | CS 2'-0" + 9'-0" ; D 4 1/2" (as scheduled (A,B...)) | 9'-0" vs 9'-0"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-7" + 3'-7" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 22'-6" | HOOP 6'-8" + 3'-7" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-7" vs 3'-7"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-7" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN NO. 1, VERTICAL | straight | 45'-0" | - | no model mark | not modeled |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-1" + 2'-0" (sum = list length) ; D 1'-0" | 15'-1" | L 13'-1" + 2'-0" ; D 1'-0" | 13'-1" vs 13'-1"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN NO. 2, VERTICAL | straight | 45'-5" | - | no model mark | not modeled |
| 11d4 | COLUMN NO. 3, VERTICAL | straight | 45'-11" | - | no model mark | not modeled |
| 11d5 | COLUMN NO. 4, VERTICAL | straight | 46'-5" | - | no model mark | not modeled |
| 7d12 | PLINTH, VERTICAL | straight | 18'-10" | STR 24'-3" | +65.00" | length +65.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 5'-4" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 5'-4" vs 5'-5 3/4" (+1.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 19'-8" | STR 19'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 17'-8" | STR 17'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 19'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 20'-10" | BENT 19'-6" (straight within 12 deg (follows the cap profile)) | 19'-8" vs 19'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 17'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 18'-10" | BENT 17'-6" (straight within 12 deg (follows the cap profile)) | 17'-8" vs 17'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | U: 19'-6" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 23'-2" | U 1'-10" + 19'-6" + 1'-10" ; D 10 3/4" | 19'-6" vs 19'-6"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 17'-6" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 21'-2" | U 1'-10" + 17'-6" + 1'-10" ; D 10 3/4" | 17'-6" vs 17'-6"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | bent: (not reconciled) ; D 2 1/2" | 10'-7" | BENT 2'-3 5/16" + 6'-1 5/8" + 2'-3 5/16" |  | not decoded |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-3" + 2'-9" + 2'-9" (sum = list length) ; D 2 1/2" | 10'-9" | U 2'-9" + 5'-3" + 2'-9" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-9" vs 2'-9"; 2'-9" vs 2'-9" | match |
| 5n2 | CAP, PEDESTAL, HOOP | hoop: 5'-4" + 5'-2" + 6" + 6" (read on raster clip 2026-10-11; sum = list length) ; D 2 1/2" | 22'-0" | HOOP 5'-2" + 5'-4" + 6" ; D 2 1/2" | 5'-4" vs 5'-4"; 5'-2" vs 5'-2"; 6" vs 6" | match (hand-updated, cap-cover work) |

### Pier 7WB (Design 116, sheet PDF p.50)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 39'-1" + 2'-0" (sum = list length) ; D 1'-0" | 41'-1" | STR 41'-1" | 39'-1" vs 41'-1" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 39'-0" | STR 39'-0" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 48'-11" | STR 48'-11" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 47'-8" | STR 47'-8" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 45'-0" | STR 45'-0" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 48'-9" | STR 48'-9" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 44'-5" | STR 44'-5" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 29'-11" | STR 29'-11" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 135'-8 1/2", 53'-9 1/2", 135'-8 1/2" | 49'-5" | STR 49'-5" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 89'-3 1/2" | 49'-8" | STR 49'-8" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 5'-9" | STR 5'-9" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 7'-7" | STR 7'-7" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 43'-5" | STR 43'-5" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-11" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-11" | CS 2'-0" + 8'-11" ; D 4 1/2" (as scheduled (A,B...)) | 8'-11" vs 8'-11"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-7" + 3'-7" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 22'-6" | HOOP 6'-8" + 3'-7" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-7" vs 3'-7"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-7" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN NO. 1, VERTICAL | straight | 26'-11" | - | no model mark | not modeled |
| 18d2 | COLUMN NO. 1, VERTICAL | straight | 31'-11" | - | no model mark | not modeled |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 15'-9" + 3'-5" (sum = list length) ; D 2'-0" | 19'-2" | L 15'-9" + 3'-5" ; D 2'-0" | 15'-9" vs 15'-9"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 10'-9" + 3'-5" (sum = list length) ; D 2'-0" | 14'-2" | L 10'-9" + 3'-5" ; D 2'-0" | 10'-9" vs 10'-9"; 3'-5" vs 3'-5" | match |
| 18d5 | COLUMN NO. 2, VERTICAL | straight | 27'-9" | - | no model mark | not modeled |
| 18d6 | COLUMN NO. 2, VERTICAL | straight | 32'-9" | - | no model mark | not modeled |
| 18d7 | COLUMN NO. 3, VERTICAL | straight | 28'-8" | - | no model mark | not modeled |
| 18d8 | COLUMN NO. 3, VERTICAL | straight | 33'-8" | - | no model mark | not modeled |
| 18d9 | COLUMN, VERTICAL | straight | 25'-8" | STR 25'-8" | +0.00" | match |
| 7d12 | PLINTH, VERTICAL | straight | 11'-10" | STR 18'-2" | +76.00" | length +76.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-0" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-0" | CROSSTIE 2'-8 3/16" ; D 2 1/2" (as scheduled (A,B...)) | 3'-0" vs 2'-8 3/16" (-3.84"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 28'-8" | STR 28'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 19'-8" | STR 19'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 6'-7" | STR 6'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 28'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 29'-10" | BENT 28'-6" (straight within 12 deg (follows the cap profile)) | 28'-8" vs 28'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 19'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 20'-10" | BENT 19'-6" (straight within 12 deg (follows the cap profile)) | 19'-8" vs 19'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | bent: (not reconciled) ; D 1'-0" | 32'-6" | BENT 1'-11 11/16" + 28'-8" + 1'-11 11/16" |  | not decoded |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | bent: (not reconciled) ; D 1'-0" | 23'-6" | BENT 1'-11 11/16" + 19'-8" + 1'-11 11/16" |  | not decoded |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-11" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-5" | U 2'-3" + 5'-11" + 2'-3" ; D 2 1/2" | 5'-11" vs 5'-11"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-3" + 5'-4" + 2'-3" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 8WB (Design 116, sheet PDF p.54)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 46'-1" + 2'-0" (sum = list length) ; D 1'-0" | 48'-1" | STR 48'-1" | 46'-1" vs 48'-1" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 46'-0" | STR 46'-0" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 59'-5" | STR 59'-5" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 58'-2" | STR 58'-2" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 55'-6" | STR 55'-6" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 59'-3" | STR 59'-3" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 54'-2" | STR 54'-2" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 36'-7" | STR 36'-7" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 203'-2 1/2", 75'-9 1/2", 203'-2 1/2" | 60'-0" | STR 60'-0" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: 39'-0" + 20'-9" + 3" (sum = list length) ; R 129'-3 1/2" | 60'-0" | STR 60'-0" | 39'-0" vs 60'-0" (+252.00"); 20'-9" vs missing; 3" vs missing | MISMATCH (model straight) |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 6'-10" | STR 6'-10" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 9'-2" | STR 9'-2" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 38'-9" | STR 38'-9" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-11" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-11" | CS 2'-0" + 8'-11" ; D 4 1/2" (as scheduled (A,B...)) | 8'-11" vs 8'-11"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: (not reconciled) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" |  | not decoded |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN NO. 1, VERTICAL | straight | 30'-11" | - | no model mark | not modeled |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-1" + 2'-0" (sum = list length) ; D 1'-0" | 15'-1" | L 13'-1" + 2'-0" ; D 1'-0" | 13'-1" vs 13'-1"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN NO. 2, VERTICAL | straight | 31'-10" | - | no model mark | not modeled |
| 11d4 | COLUMN NO. 3, VERTICAL | straight | 32'-9" | - | no model mark | not modeled |
| 11d5 | COLUMN NO. 4, VERTICAL | straight | 33'-7" | - | no model mark | not modeled |
| 7d12 | PLINTH, VERTICAL | straight | 11'-10" | STR 17'-3" | +65.00" | length +65.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-3" + 2'-3" + 1'-9" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 2'-3" vs missing; 1'-9" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 3'-1" + 2'-10" + 2'-10" + 2'-3" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 14'-2" | STR 14'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 21'-2" | STR 21'-2" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 14'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 15'-4" | BENT 14'-0" (straight within 12 deg (follows the cap profile)) | 14'-2" vs 14'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 21'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 22'-4" | BENT 21'-0" (straight within 12 deg (follows the cap profile)) | 21'-2" vs 21'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | U: 14'-0" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 17'-8" | U 1'-10" + 14'-0" + 1'-10" ; D 10 3/4" | 14'-0" vs 14'-0"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 21'-0" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 24'-8" | U 1'-10" + 21'-0" + 1'-10" ; D 10 3/4" | 21'-0" vs 21'-0"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-2" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-8" | U 2'-3" + 5'-2" + 2'-3" ; D 2 1/2" | 5'-2" vs 5'-2"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-3" + 5'-4" + 2'-3" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 9WB (Design 116, sheet PDF p.58)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 40'-6" + 2'-0" (sum = list length) ; D 1'-0" | 42'-6" | STR 42'-6" | 40'-6" vs 42'-6" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 40'-4" | STR 40'-4" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 50'-11" | STR 50'-11" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 49'-8" | STR 49'-8" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 47'-0" | STR 47'-0" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 50'-9" | STR 50'-9" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 46'-4" | STR 46'-4" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 31'-2" | STR 31'-2" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 148'-2 1/2", 56'-3 1/2", 148'-2 1/2" | 51'-5" | STR 51'-5" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 96'-9 1/2" | 51'-9" | STR 51'-9" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 5'-9" | STR 5'-9" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 7'-11" | STR 7'-11" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 45'-5" | STR 45'-5" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-10" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-10" | CS 2'-0" + 8'-10" ; D 4 1/2" (as scheduled (A,B...)) | 8'-10" vs 8'-10"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN NO. 1, VERTICAL | straight | 29'-11" | - | no model mark | not modeled |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 12'-1" + 2'-0" (sum = list length) ; D 1'-0" | 14'-1" | L 12'-1" + 2'-0" ; D 1'-0" | 12'-1" vs 12'-1"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN NO. 2, VERTICAL | straight | 31'-5" | - | no model mark | not modeled |
| 11d4 | COLUMN NO. 3, VERTICAL | straight | 33'-0" | - | no model mark | not modeled |
| 7d12 | PLINTH, VERTICAL | straight | 10'-10" | STR 15'-3" | +53.00" | length +53.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 17'-2" | STR 17'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 25'-8" | STR 25'-8" | +0.00" | match |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 17'-2" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 21'-2" | U 2'-0" + 17'-2" + 2'-0" ; D 1'-0" | 17'-2" vs 17'-2"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 25'-8" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 29'-8" | U 2'-0" + 25'-8" + 2'-0" ; D 1'-0" | 25'-8" vs 25'-8"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-2" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-2" + 5'-2" + 2'-2" ; D 2 1/2" | 5'-2" vs 5'-2"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-8" | U 2'-2" + 5'-4" + 2'-2" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |

### Pier 10WB (Design 116, sheet PDF p.62)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 37'-10" + 2'-0" (sum = list length) ; D 1'-0" | 39'-10" | STR 39'-10" | 37'-10" vs 39'-10" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 37'-8" | STR 37'-8" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 46'-11" | STR 46'-11" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 45'-7" | STR 45'-7" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 42'-11" | STR 42'-11" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 46'-9" | STR 46'-9" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 42'-6" | STR 42'-6" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 28'-8" | STR 28'-8" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 125'-2 1/2", 47'-9 1/2", 125'-2 1/2" | 47'-5" | STR 47'-5" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 82'-3 1/2" | 47'-10" | STR 47'-10" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 5'-3" | STR 5'-3" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 7'-2" | STR 7'-2" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 41'-5" | STR 41'-5" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-10" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-10" | CS 2'-0" + 8'-10" ; D 4 1/2" (as scheduled (A,B...)) | 8'-10" vs 8'-10"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-7" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-7" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-7" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 3/16" + 4'-8" + 1'-3 3/16" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN NO. 1, VERTICAL | straight | 31'-11" | - | no model mark | not modeled |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 12'-1" + 2'-0" (sum = list length) ; D 1'-0" | 14'-1" | L 12'-1" + 2'-0" ; D 1'-0" | 12'-1" vs 12'-1"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN NO. 2, VERTICAL | straight | 33'-4" | - | no model mark | not modeled |
| 11d4 | COLUMN NO. 3, VERTICAL | straight | 34'-10" | - | no model mark | not modeled |
| 7d12 | PLINTH, VERTICAL | straight | 11'-10" | STR 16'-3" | +53.00" | length +53.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 17'-2" | STR 17'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 25'-8" | STR 25'-8" | +0.00" | match |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 17'-2" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 21'-2" | U 2'-0" + 17'-2" + 2'-0" ; D 1'-0" | 17'-2" vs 17'-2"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 25'-8" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 29'-8" | U 2'-0" + 25'-8" + 2'-0" ; D 1'-0" | 25'-8" vs 25'-8"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-2" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-2" + 5'-2" + 2'-2" ; D 2 1/2" | 5'-2" vs 5'-2"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-8" | U 2'-2" + 5'-4" + 2'-2" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |

### Pier 11WB (Design 116, sheet PDF p.66)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 9a1 | CAP, TOP, LONGITUDINAL | L: 47'-0" + 1'-7" (sum = list length) ; D 9 1/2" | 48'-7" | STR 48'-7" | 47'-0" vs 48'-7" (+19.00"); 1'-7" vs missing pin: plan 9 1/2" / model none (straight) | MISMATCH (model straight) |
| 9a2 | CAP, TOP, LONGITUDINAL | straight | 46'-10" | STR 46'-10" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 44'-10" | STR 44'-10" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 43'-10" | STR 43'-10" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 41'-6" | STR 41'-6" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 44'-3" | STR 44'-3" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 40'-3" | STR 40'-3" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 27'-1" | STR 27'-1" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 111'-8 1/2", 43'-3 1/2", 111'-8 1/2" | 45'-0" | STR 45'-0" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 73'-9 1/2" | 45'-4" | STR 45'-4" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 4'-11" | STR 4'-11" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 6'-9" | STR 6'-9" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 2 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, END, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 9b1 | CAP, BOTTOM, LONGITUDINAL | straight | 39'-9" | STR 39'-9" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 7'-10" + 2'-0" (sum = list length) ; D 4 1/2" | 9'-10" | CS 2'-0" + 7'-10" ; D 4 1/2" (as scheduled (A,B...)) | 7'-10" vs 7'-10"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 5'-3" + 5'-3" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-10" | HOOP 6'-8" + 5'-3" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 5'-3" vs 5'-3"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-7" MIN. ; D 4 1/2" | VARIES | TIE 6'-8" + 6'-8" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 10" + 6'-8" + 10" (OTO from centerline) | 6'-8" vs 6'-8"; 1'-0" vs 10" (-1.99"); 8" vs 10" (+2.01") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-7" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 6" + 6'-8" + 6" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | bent: (not reconciled) ; D 2 1/2" | 13'-3" | BENT 6'-5 13/16" + 4 5/8" + 6'-5 13/16" |  | not decoded |
| 5c6 | CAP, HAIRPINS, VERTICAL | bent: (not reconciled) ; D 2 1/2" | 11'-5" | BENT 5'-6 13/16" + 4 5/8" + 5'-6 13/16" |  | not decoded |
| 4c7 | CAP, TRANSVERSE | U: 6'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 7'-5" | U 4 1/2" + 6'-8" + 4 1/2" ; D 2" | 6'-8" vs 6'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 32'-10" | STR 32'-10" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 17'-9" + 2'-0" (sum = list length) ; D 1'-0" | 19'-9" | L 17'-9" + 2'-0" ; D 1'-0" | 17'-9" vs 17'-9"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN, VERTICAL | straight | 34'-10" | STR 34'-10" | +0.00" | match |
| 11d4 | COLUMN, VERTICAL | straight | 35'-11" | STR 35'-11" | +0.00" | match |
| 7d12 | PLINTH, VERTICAL | straight | 12'-10" | STR 18'-9" | +71.00" | length +71.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 7'-8" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | bent: (not reconciled) ; D 2 1/2" | 9'-0" | BENT 2'-3 5/16" + 4'-6 5/8" + 2'-3 5/16" |  | not decoded |
| 8m2 | CAP, STEP, LONGITUDINAL | straight | 45'-11" | STR 45'-11" | +0.00" | match |
| 6m3 | CAP, STEP, LONGITUDINAL | straight | 44'-6" | STR 44'-6" | +0.00" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 3'-7" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 7'-11" | U 2'-2" + 3'-7" + 2'-2" ; D 2 1/2" | 3'-7" vs 3'-7"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 5n2 | CAP, PEDESTAL, TRANSVERSE | U: 3'-5" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 7'-9" | U 2'-2" + 3'-5" + 2'-2" ; D 2 1/2" | 3'-5" vs 3'-5"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 6n3 | CAP, STEP, TRANSVERSE | U: 3'-7" + 3'-7" + 3'-5" (sum = list length) ; D 4 1/2" | 10'-7" | BENT 1'-11 1/2" + 6'-8" + 1'-11 1/2" (OTO from centerline) | 3'-7" vs 6'-8" (+37.00"); 3'-7" vs 1'-11 1/2" (-19.50"); 3'-5" vs 1'-11 1/2" (-17.50") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 7f1 | FOOTING, TOP | straight | 17'-8" | STR 17'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP | straight | VARIES | STR 14'-4" |  | ? |
| 7f3 | FOOTING, SIDE | straight | 6'-1" | STR 6'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE | L: 17'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 18'-10" | BENT 17'-6" (straight within 12 deg (follows the cap profile)) | 17'-8" vs 17'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE | L: 11'-0" + 1'-2" (sum = list length) ; D 5 1/4" | 12'-2" | STR 12'-2" | 11'-0" vs 12'-2" (+14.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH (model straight) |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 7f7 | FOOTING, SIDE | L: 9'-5" + 1'-2" (sum = list length) ; D 5 1/4" | 10'-7" | STR 10'-7" | 9'-5" vs 10'-7" (+14.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH (model straight) |
| 7f8 | FOOTING, SIDE | L: 11'-0" + 1'-2" (sum = list length) ; D 5 1/4" | 12'-2" | STR 12'-2" | 11'-0" vs 12'-2" (+14.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH (model straight) |
| 10g1 | FOOTING, BOTTOM | U: 17'-6" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 21'-2" | U 1'-10" + 17'-6" + 1'-10" ; D 10 3/4" | 17'-6" vs 17'-6"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 10g2 | FOOTING, BOTTOM | bent: (not reconciled) ; D 10 3/4" | VARIES | BENT 1 5/8" + 17'-8" + 1 5/8" |  | not decoded |

### Pier 12WB (Design 116, sheet PDF p.70)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | L: 43'-8" + 1'-10" (sum = list length) ; D 10 3/4" | 45'-6" | STR 45'-6" | 43'-8" vs 45'-6" (+22.00"); 1'-10" vs missing pin: plan 10 3/4" / model none (straight) | MISMATCH (model straight) |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 43'-7" | STR 43'-7" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 58'-0" | STR 58'-0" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 56'-8" | STR 56'-8" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 54'-0" | STR 54'-0" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 57'-9" | STR 57'-9" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 52'-11" | STR 52'-11" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-8" | STR 35'-8" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 72'-3 1/2", 193'-8 1/2" | 58'-5" | STR 58'-5" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 124'-9 1/2" | 58'-10" | STR 58'-10" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 6'-8" | STR 6'-8" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 9'-1" | STR 9'-1" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 10b1 | CAP, BOTTOM, LONGITUDINAL | straight | 36'-9" | STR 36'-9" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-9" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-9" | CS 2'-0" + 8'-9" ; D 4 1/2" (as scheduled (A,B...)) | 8'-9" vs 8'-9"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN NO. 1, VERTICAL | straight | 32'-10" | - | no model mark | not modeled |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 12'-7" + 2'-0" (sum = list length) ; D 1'-0" | 14'-7" | L 12'-7" + 2'-0" ; D 1'-0" | 12'-7" vs 12'-7"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN NO. 2, VERTICAL | straight | 34'-4" | - | no model mark | not modeled |
| 11d4 | COLUMN NO. 3, VERTICAL | straight | 35'-10" | - | no model mark | not modeled |
| 11d5 | COLUMN NO. 4, VERTICAL | straight | 37'-4" | - | no model mark | not modeled |
| 7d12 | PLINTH, VERTICAL | straight | 13'-10" | STR 18'-9" | +59.00" | length +59.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | arcs: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; R 193'-8 1/2" ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 20'-2" | STR 20'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 15'-8" | STR 15'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 20'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 21'-4" | BENT 20'-0" (straight within 12 deg (follows the cap profile)) | 20'-2" vs 20'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 15'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 16'-10" | BENT 15'-6" (straight within 12 deg (follows the cap profile)) | 15'-8" vs 15'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 20'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 24'-0" | U 2'-0" + 20'-0" + 2'-0" ; D 1'-0" | 20'-0" vs 20'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 7g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 15'-6" + 1'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 17'-10" | U 1'-2" + 15'-6" + 1'-2" ; D 5 1/4" | 15'-6" vs 15'-6"; 1'-2" vs 1'-2"; 1'-2" vs 1'-2" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-2" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-2" + 5'-2" + 2'-2" ; D 2 1/2" | 5'-2" vs 5'-2"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-8" | U 2'-2" + 5'-4" + 2'-2" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |

### Pier 13WB (Design 116, sheet PDF p.74)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 38'-10" + 2'-0" (sum = list length) ; D 1'-0" | 40'-10" | STR 40'-10" | 38'-10" vs 40'-10" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 38'-8" | STR 38'-8" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 48'-6" | STR 48'-6" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 47'-2" | STR 47'-2" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 44'-6" | STR 44'-6" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 48'-3" | STR 48'-3" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 44'-0" | STR 44'-0" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 29'-8" | STR 29'-8" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 133'-8 1/2", 50'-9 1/2", 133'-8 1/2" | 48'-11" | STR 48'-11" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 87'-9 1/2" | 49'-4" | STR 49'-4" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 7'-6" | STR 7'-6" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 42'-11" | STR 42'-11" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-9" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-9" | CS 2'-0" + 8'-9" ; D 4 1/2" (as scheduled (A,B...)) | 8'-9" vs 8'-9"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-6" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-6" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-6" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/16" + 4'-8" + 1'-3 1/16" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN NO. 1, VERTICAL | straight | 22'-10" | - | no model mark | not modeled |
| 18d2 | COLUMN NO. 1, VERTICAL | straight | 27'-10" | - | no model mark | not modeled |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 14'-8" + 3'-5" (sum = list length) ; D 2'-0" | 18'-1" | L 14'-8" + 3'-5" ; D 2'-0" | 14'-8" vs 14'-8"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 9'-8" + 3'-5" (sum = list length) ; D 2'-0" | 13'-1" | L 9'-8" + 3'-5" ; D 2'-0" | 9'-8" vs 9'-8"; 3'-5" vs 3'-5" | match |
| 18d5 | COLUMN NO. 2, VERTICAL | straight | 24'-8" | - | no model mark | not modeled |
| 18d6 | COLUMN NO. 2, VERTICAL | straight | 29'-8" | - | no model mark | not modeled |
| 18d7 | COLUMN NO. 3, VERTICAL | straight | 26'-6" | - | no model mark | not modeled |
| 18d8 | COLUMN NO. 3, VERTICAL | straight | 31'-6" | - | no model mark | not modeled |
| 7d12 | PLINTH, VERTICAL | straight | 11'-10" | STR 17'-2" | +64.00" | length +64.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-0" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-0" | CROSSTIE 2'-8 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-0" vs 2'-8 1/2" (-3.48"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 29'-8" | STR 29'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 23'-8" | STR 23'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDE, TRANSVERSE | L: 29'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 30'-10" | BENT 29'-6" (straight within 12 deg (follows the cap profile)) | 29'-8" vs 29'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDE, LONGITUDINAL | L: 23'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 24'-10" | BENT 23'-6" (straight within 12 deg (follows the cap profile)) | 23'-8" vs 23'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 29'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 33'-6" | U 2'-0" + 29'-6" + 2'-0" ; D 1'-0" | 29'-6" vs 29'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 23'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 27'-6" | U 2'-0" + 23'-6" + 2'-0" ; D 1'-0" | 23'-6" vs 23'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-2" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-2" + 5'-2" + 2'-2" ; D 2 1/2" | 5'-2" vs 5'-2"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-8" | U 2'-2" + 5'-4" + 2'-2" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |

### Pier 14WB (Design 116, sheet PDF p.78)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | L: 34'-4" + 1'-10" (sum = list length) ; D 10 3/4" | 36'-2" | STR 36'-2" | 34'-4" vs 36'-2" (+22.00"); 1'-10" vs missing pin: plan 10 3/4" / model none (straight) | MISMATCH (model straight) |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 44'-0" | STR 44'-0" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 42'-7" | STR 42'-7" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 39'-11" | STR 39'-11" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 43'-9" | STR 43'-9" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 39'-9" | STR 39'-9" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | STR 26'-9" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 109'-2 1/2", 42'-3 1/2", 109'-2 1/2" | 44'-6" | STR 44'-6" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 72'-3 1/2" | 44'-11" | STR 44'-11" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 4'-10" | STR 4'-10" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 6'-8" | STR 6'-8" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 10b1 | CAP, BOTTOM, LONGITUDINAL | straight | 37'-8" | STR 37'-8" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-9" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-9" | CS 2'-0" + 8'-9" ; D 4 1/2" (as scheduled (A,B...)) | 8'-9" vs 8'-9"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-6" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-6" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-6" MAX. ; D 4 1/2" | VARIES | BENT 1'-2 15/16" + 4'-8" + 1'-2 15/16" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN NO. 1, VERTICAL | straight | 31'-10" | - | no model mark | not modeled |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 12'-1" + 2'-0" (sum = list length) ; D 1'-0" | 14'-1" | L 12'-1" + 2'-0" ; D 1'-0" | 12'-1" vs 12'-1"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN NO. 2, VERTICAL | straight | 33'-6" | - | no model mark | not modeled |
| 11d4 | COLUMN NO. 3, VERTICAL | straight | 35'-2" | - | no model mark | not modeled |
| 7d12 | PLINTH, VERTICAL | straight | 11'-10" | STR 16'-3" | +53.00" | length +53.00" |
| 7d13 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 2'-6 1/2" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-1" + 2'-10" + 2'-10" + 2'-10" + 2'-6" (sum = list length) ; D 4 1/2" | 14'-1" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 20'-2" | STR 20'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 18'-8" | STR 18'-8" | +0.00" | match |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | U: 20'-2" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 23'-10" | U 1'-10" + 20'-2" + 1'-10" ; D 10 3/4" | 20'-2" vs 20'-2"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 18'-8" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 22'-4" | U 1'-10" + 18'-8" + 1'-10" ; D 10 3/4" | 18'-8" vs 18'-8"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-2" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-2" + 5'-2" + 2'-2" ; D 2 1/2" | 5'-2" vs 5'-2"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-8" | U 2'-2" + 5'-4" + 2'-2" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |

### Pier 15WB (Design 116, sheet PDF p.82)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | L: 33'-8" + 1'-10" (sum = list length) ; D 10 3/4" | 35'-6" | STR 35'-6" | 33'-8" vs 35'-6" (+22.00"); 1'-10" vs missing pin: plan 10 3/4" / model none (straight) | MISMATCH (model straight) |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 33'-7" | STR 33'-7" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 43'-0" | STR 43'-0" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 41'-7" | STR 41'-7" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 38'-11" | STR 38'-11" | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 42'-9" | STR 42'-9" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 38'-10" | STR 38'-10" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 26'-2" | STR 26'-2" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 104'-2 1/2", 40'-3 1/2" | 43'-5" | STR 43'-5" |  | not decoded |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: 28'-2" + 15'-4" + 4 1/2" (sum = list length) ; R 104'-2 1/2", 68'-9 1/2" | 43'-11" | STR 43'-11" | 28'-2" vs 43'-11" (+189.00"); 15'-4" vs missing; 4 1/2" vs missing | MISMATCH (model straight) |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 4'-8" | STR 4'-8" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 6'-6" | STR 6'-6" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 10b1 | CAP, BOTTOM, LONGITUDINAL | straight | 36'-8" | STR 36'-8" | +0.00" | match |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-9" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-9" | CS 2'-0" + 8'-9" ; D 4 1/2" (as scheduled (A,B...)) | 8'-9" vs 8'-9"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-6" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-6" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-6" MAX. ; D 4 1/2" | VARIES | BENT 1'-2 15/16" + 4'-8" + 1'-2 15/16" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-11" + 2'-11" (sum = list length) ; D 2 1/2" | 11'-3" | U 2'-11" + 5'-5" + 2'-11" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-11" vs 2'-11"; 2'-11" vs 2'-11" | match |
| 5c6 | CAP, HAIRPINS, VERTICAL | U: 5'-5" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-5" | U 2'-0" + 5'-5" + 2'-0" ; D 2 1/2" | 5'-5" vs 5'-5"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN NO. 1, VERTICAL | straight | 24'-10" | - | no model mark | not modeled |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 12'-1" + 2'-0" (sum = list length) ; D 1'-0" | 14'-1" | L 12'-1" + 2'-0" ; D 1'-0" | 12'-1" vs 12'-1"; 2'-0" vs 2'-0" | match |
| 11d3 | COLUMN NO. 2, VERTICAL | straight | 26'-6" | - | no model mark | not modeled |
| 11d4 | COLUMN NO. 3, VERTICAL | straight | 28'-2" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 4'-2" + 3'-5" + 2'-6 1/2" + 2'-6 1/2" + 1'-9" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-10" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6 1/2" vs missing; 1'-9" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-1" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-1" | CROSSTIE 2'-9 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 3'-1" vs 2'-9 1/2" (-3.49"); 6" vs missing | MISMATCH |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 20'-2" | STR 20'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 18'-8" | STR 18'-8" | +0.00" | match |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | U: 20'-2" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 23'-10" | U 1'-10" + 20'-2" + 1'-10" ; D 10 3/4" | 20'-2" vs 20'-2"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 18'-8" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 22'-4" | U 1'-10" + 18'-8" + 1'-10" ; D 10 3/4" | 18'-8" vs 18'-8"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-2" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-2" + 5'-2" + 2'-2" ; D 2 1/2" | 5'-2" vs 5'-2"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-2" + 2'-2" (sum = list length) ; D 2 1/2" | 9'-8" | U 2'-2" + 5'-4" + 2'-2" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-2" vs 2'-2"; 2'-2" vs 2'-2" | match |

### West Abut (Design 116, sheet PDF p.88)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 9a1 | FOOTING, LONGITUDINAL | straight | 48'-4" | STR 49'-3 7/8" | +11.88" | length +11.88" |
| 5b1 | FOOTING, HOOPS | hoop: 6'-4 1/2" + 3'-5" + 6" (read on clip; sum = list length) ; D 2 1/2" | 20'-7" | TIE 6'-4 1/2" + 3'-5" ; D 2 1/2" (as scheduled (A,B...)) | 6'-4 1/2" vs 6'-4 1/2"; 3'-5" vs 3'-5"; 6" vs missing | MISMATCH |
| 5b2 | FOOTING, HOOPS | hoopv: 3'-5" + 6" (read on clip) ; D 2 1/2" | VARIES | STR 16'-1 5/16" | 3'-5" vs 16'-1 5/16" (+152.29"); 6" vs missing pin: plan 2 1/2" / model none (straight) | MISMATCH (model straight) |
| 5b3 | FOOTING, END, HORIZONTAL | seg: 7'-3" + 1'-6" + 1'-6" + 1'-4" (read on clip; sum = list length) ; D 2 1/2" | 11'-7" | STR 11'-7" | 7'-3" vs 11'-7" (+52.00"); 1'-6" vs missing; 1'-6" vs missing; 1'-4" vs missing pin: plan 2 1/2" / model none (straight) | MISMATCH (model straight) |
| 5b4 | FOOTING, END, HORIZONTAL | seg: 10'-0" + 4'-6" + 3'-2" + 1'-4" (read on clip; sum = list length) ; D 2 1/2" | 19'-0" | STR 19'-0" | 10'-0" vs 19'-0" (+108.00"); 4'-6" vs missing; 3'-2" vs missing; 1'-4" vs missing pin: plan 2 1/2" / model none (straight) | MISMATCH (model straight) |
| 6d1 | BACKWALL, B.F., VERTICAL | straight | 13'-1" | STR 13'-1" | +0.00" | match |
| 5d2 | BACKWALL, F.F., VERTICAL | straight | 12'-9" | STR 12'-9" | +0.00" | match |
| 5d3 | PAVING NOTCH | seg: 2'-5" + 1'-0" + 1'-0" (read on clip; sum = list length) ; D 2 1/2" | 4'-5" | U 1'-0" + 2'-5" + 1'-0" ; D 2 1/2" | 2'-5" vs 2'-5"; 1'-0" vs 1'-0"; 1'-0" vs 1'-0" | match |
| 5d4 | PAVING NOTCH | seg: 1'-5" + 1'-3" + 1'-0" (read on clip; sum = list length) ; D 2 1/2" | 3'-8" | STR 3'-8" | 1'-5" vs 3'-8" (+27.00"); 1'-3" vs missing; 1'-0" vs missing pin: plan 2 1/2" / model none (straight) | MISMATCH (model straight) |
| 4d6 | BACKWALL, HAIRPIN, VERTICAL | seg: 3'-10" + 3'-10" + 1'-2" (read on clip; sum = list length) ; D 2" | 8'-10" | U 3'-10" + 1'-2" + 3'-10" ; D 2" | 3'-10" vs 3'-10"; 3'-10" vs 3'-10"; 1'-2" vs 1'-2" | match |
| 5d7 | BACKWALL EXTENSION, VERTICAL | straight | 14'-5" | STR 14'-5" | +0.00" | match |
| 4d8 | BACKWALL EXTENSION, HAIRPIN, VERTICAL | seg: 1'-2" + 1'-0" + 1'-0" (read on clip; sum = list length) ; D 2" | 3'-2" | U 1'-0" + 1'-2" + 1'-0" ; D 2" | 1'-2" vs 1'-2"; 1'-0" vs 1'-0"; 1'-0" vs 1'-0" | match |
| 5e1 | WINGWALL, VERTICAL | straight | VARIES | STR 13'-1" (min 8'-4 7/8") |  | ? |
| 5e2 | MASKWALL, VERTICAL | straight | 15'-6" | STR 15'-6" | +0.00" | match |
| 5e3 | MASKWALL, HORIZONTAL | straight | 10'-0" | STR 10'-0" | +0.00" | match |
| 5e4 | WINGWALL, VERTICAL | straight | VARIES | STR 12'-10" (min 8'-3 1/2") |  | ? |
| 6f1 | WINGWALL, HORIZONTAL | straight | 25'-8" | STR 25'-8" | +0.00" | match |
| 6f2 | WINGWALL, HORIZONTAL | straight | VARIES | STR 24'-8" (min 14'-10 3/4") |  | ? |
| 6f3 | WINGWALL, BOTTOM | straight | 26'-11" | STR 26'-11" | +0.00" | match |
| 6f4 | WINGWALL, DOWELS | straight | 6'-0" | STR 6'-0" | +0.00" | match |
| 6f5 | WINGWALL, HORIZONTAL | straight | 27'-0" | STR 27'-0" | +0.00" | match |
| 6f6 | WINGWALL, HORIZONTAL | straight | VARIES | STR 25'-11" (min 15'-5 1/4") |  | ? |
| 6f7 | WINGWALL, BOTTOM | straight | 28'-5" | STR 28'-5" | +0.00" | match |
| 6g1 | BACKWALL, LONGITUDINAL | straight | 45'-1" | STR 45'-1" | +0.00" | match |
| 5g2 | BACKWALL, DOWELS | seg: 2'-6" + 1'-3" + 10" (read on clip; sum = list length) ; D 3 3/4" | 4'-7" | STR 4'-7" | 2'-6" vs 4'-7" (+25.00"); 1'-3" vs missing; 10" vs missing pin: plan 3 3/4" / model none (straight) | MISMATCH (model straight) |
| 5g3 | PAVING NOTCH, LONGITUDINAL | straight | 42'-7" | STR 42'-7" | +0.00" | match |
| 6g4 | WINGWALL, DOWELS | seg: 5'-6" + 1'-0" (read on clip; sum = list length) ; D 4 1/2" | 6'-6" | L 1'-0" + 5'-6" ; D 4 1/2" | 5'-6" vs 5'-6"; 1'-0" vs 1'-0" | match |
| 6g5 | BACKWALL EXTENSION, HORIZONTAL | straight | 8'-0" | STR 8'-0" | +0.00" | match |
| 5h1 | COUNTERFORT, DOWELS | straight | 4'-0" | STR 4'-0" | +0.00" | match |
| 6h2 | COUNTERFORT NO. 1, VERTICAL | straight | 11'-5" | STR 11'-5" | +0.00" | match |
| 6h3 | COUNTERFORT NO. 2, VERTICAL | straight | 7'-11" | STR 7'-11" | +0.00" | match |
| 5h4 | COUNTERFORT, HORIZONTAL | seg: 4'-9" + 4'-9" + 1'-6" + 6" + 6" (read on clip; sum = list length) ; D 2 1/2" | 12'-0" | HAT 6" + 4'-9" + 1'-6" + 4'-9" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 4'-9" vs 4'-9"; 4'-9" vs 4'-9"; 1'-6" vs 1'-6"; 6" vs 6"; 6" vs 6" | match |
| 5j1 | WING FOOTING, LONGITUDINAL | seg: 6'-8" + 2'-5" + 2'-5" (read on clip; sum = list length) ; D 3 3/4" | 11'-6" | U 2'-5" + 6'-8" + 2'-5" ; D 3 3/4" | 6'-8" vs 6'-8"; 2'-5" vs 2'-5"; 2'-5" vs 2'-5" | match |
| 5j2 | WING FOOTING, TRANSVERSE | hoop: 2'-8" + 2'-7" + 6" (read on clip; sum = list length) ; D 2 1/2" | 11'-6" | HOOP 2'-8" + 2'-7" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 2'-8" vs 2'-8"; 2'-7" vs 2'-7"; 6" vs 6" | match |
| 8j3 | PILE UPLIFT ANCHORS | V: 2'-6" + 2'-6" (read on clip; sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 5m1 | BEAM STEPS, TRANSVERSE | seg: 3'-8" + 2'-3" + 2'-3" (read on clip; sum = list length) ; D 2 1/2" | 8'-2" | U 2'-3" + 3'-8" + 2'-3" ; D 2 1/2" | 3'-8" vs 3'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | BEAM STEPS, LONGITUDINAL | straight | 4'-2" | STR 4'-2" | +0.00" | match |
| 5n2 | BEAM STEPS, HOOP | hoop: 4'-2" + 3'-9" + 6" (read on clip; sum = list length) ; D 2 1/2" | 16'-10" | HOOP 4'-2" + 3'-9" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 4'-2" vs 4'-2"; 3'-9" vs 3'-9"; 6" vs 6" | match |
| 5d5 | PAVING NOTCH, DOWELS | straight | 3'-6" | STR 3'-6" | +0.00" | match |

### East Abut (Design 116, sheet PDF p.92)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 9a1 | FOOTING, LONGITUDINAL | straight | 45'11 | STR 45'-11 3/4" |  | ? |
| 5b1 | FOOTING, HOOPS | hoop: 6'-4 1/2" + 3'-5" + 6" (read on clip; sum = list length) ; D 2 1/2" | 20'-7" | TIE 6'-4 1/2" + 3'-5" ; D 2 1/2" (as scheduled (A,B...)) | 6'-4 1/2" vs 6'-4 1/2"; 3'-5" vs 3'-5"; 6" vs missing | MISMATCH |
| 5b2 | FOOTING, END, HORIZONTAL | seg: 6'-3" + 1'-6" + 1'-6" (read on clip; sum = list length) ; D 2 1/2" | 9'-3" | STR 9'-3" | 6'-3" vs 9'-3" (+36.00"); 1'-6" vs missing; 1'-6" vs missing pin: plan 2 1/2" / model none (straight) | MISMATCH (model straight) |
| 6d1 | BACKWALL, B.F., VERTICAL | straight | 10'-6" | STR 10'-6" | +0.00" | match |
| 5d2 | BACKWALL, F.F., VERTICAL | straight | 10'-2" | STR 10'-2" | +0.00" | match |
| 5d3 | PAVING NOTCH | seg: 2'-5" + 1'-0" + 1'-0" (read on clip; sum = list length) ; D 2 1/2" | 4'-5" | U 1'-0" + 2'-5" + 1'-0" ; D 2 1/2" | 2'-5" vs 2'-5"; 1'-0" vs 1'-0"; 1'-0" vs 1'-0" | match |
| 5d4 | PAVING NOTCH | seg: 1'-5" + 1'-3" + 1'-0" (read on clip; sum = list length) ; D 2 1/2" | 3'-8" | STR 3'-8" | 1'-5" vs 3'-8" (+27.00"); 1'-3" vs missing; 1'-0" vs missing pin: plan 2 1/2" / model none (straight) | MISMATCH (model straight) |
| 4d6 | BACKWALL, HAIRPIN, VERTICAL | seg: 3'-10" + 3'-10" + 1'-2" (read on clip; sum = list length) ; D 2" | 8'-10" | U 3'-10" + 1'-2" + 3'-10" ; D 2" | 3'-10" vs 3'-10"; 3'-10" vs 3'-10"; 1'-2" vs 1'-2" | match |
| 5d7 | BACKWALL EXTENSION, VERTICAL | straight | 12'-2" | STR 12'-2" | +0.00" | match |
| 4d8 | BACKWALL EXTENSION, HAIRPIN, VERT. | seg: 1'-2" + 1'-0" + 1'-0" (read on clip; sum = list length) ; D 2" | 3'-2" | U 1'-0" + 1'-2" + 1'-0" ; D 2" | 1'-2" vs 1'-2"; 1'-0" vs 1'-0"; 1'-0" vs 1'-0" | match |
| 5e1 | WINGWALL, VERTICAL | straight | VARIES | STR 11'-7" (min 7'-8 1/16") |  | ? |
| 5e2 | MASKWALL, VERTICAL | straight | 13'-8" | STR 13'-8" | +0.00" | match |
| 5e3 | MASKWALL, HORIZONTAL | straight | 6'-4" | STR 6'-4" | +0.00" | match |
| 6f1 | WINGWALL, HORIZONTAL | straight | 23'-3" | STR 23'-3" | +0.00" | match |
| 6f2 | WINGWALL, HORIZONTAL | straight | VARIES | STR 22'-1" |  | ? |
| 6f3 | WINGWALL, BOTTOM | straight | 24'-5" | STR 24'-5" | +0.00" | match |
| 6f4 | WINGWALL, DOWELS | straight | 5'-3" | STR 5'-3" | +0.00" | match |
| 6g1 | BACKWALL, LONGITUDINAL | straight | 42'-5" | STR 42'-5" | +0.00" | match |
| 5g2 | BACKWALL, DOWELS | seg: 5'-3" + 10" (read on clip; sum = list length) ; D 3 3/4" | 6'-1" | L 10" + 5'-3" ; D 3 3/4" | 5'-3" vs 5'-3"; 10" vs 10" | match |
| 5g3 | PAVING NOTCH, LONGITUDINAL | straight | 40'-1" | STR 40'-1" | +0.00" | match |
| 6g4 | WINGWALL, DOWELS | seg: 4'-5" + 1'-0" (read on clip; sum = list length) ; D 4 1/2" | 5'-5" | L 1'-0" + 4'-5" ; D 4 1/2" | 4'-5" vs 4'-5"; 1'-0" vs 1'-0" | match |
| 6g5 | BACKWALL EXTENSION, HORIZONTAL | straight | 6'-5" | STR 6'-5" | +0.00" | match |
| 5h1 | COUNTERFORT, DOWELS | straight | 4'-0" | STR 4'-0" | +0.00" | match |
| 6h2 | COUNTERFORT NO. 1, VERTICAL | straight | 11'-3" | STR 11'-3" | +0.00" | match |
| 6h3 | COUNTERFORT NO. 2, VERTICAL | straight | 7'-11" | STR 7'-11" | +0.00" | match |
| 5h4 | COUNTERFORT, HORIZONTAL | seg: 4'-9" + 4'-9" + 1'-6" + 6" + 6" (read on clip; sum = list length) ; D 2 1/2" | 12'-0" | HAT 6" + 4'-9" + 1'-6" + 4'-9" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 4'-9" vs 4'-9"; 4'-9" vs 4'-9"; 1'-6" vs 1'-6"; 6" vs 6"; 6" vs 6" | match |
| 5j1 | WING FOOTING, LONGITUDINAL | seg: 6'-8" + 2'-5" + 2'-5" (read on clip; sum = list length) ; D 3 3/4" | 11'-6" | U 2'-5" + 6'-8" + 2'-5" ; D 3 3/4" | 6'-8" vs 6'-8"; 2'-5" vs 2'-5"; 2'-5" vs 2'-5" | match |
| 5j2 | WING FOOTING, TRANSVERSE | hoop: 2'-8" + 2'-7" + 6" (read on clip; sum = list length) ; D 2 1/2" | 11'-6" | HOOP 2'-8" + 2'-7" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 2'-8" vs 2'-8"; 2'-7" vs 2'-7"; 6" vs 6" | match |
| 8j3 | PILE UPLIFT ANCHORS | V: 2'-6" + 2'-6" (read on clip; sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 5m1 | BEAM STEPS, TRANSVERSE | seg: 3'-9" + 2'-0" + 2'-0" (read on clip; sum = list length) ; D 2 1/2" | 7'-9" | U 2'-0" + 3'-9" + 2'-0" ; D 2 1/2" | 3'-9" vs 3'-9"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5n1 | BEAM STEPS, LONGITUDINAL | straight | 4'-2" | STR 4'-2" | +0.00" | match |
| 5d5 | PAVING NOTCH, DOWELS | straight | 3'-6" | STR 3'-6" | +0.00" | match |

### Pier 1G (Design 316, sheet PDF p.21)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 38'-5" | BENT 1'-11 7/16" + 34'-8 3/4" + 1'-11 7/16" (OTO from centerline) | 34'-9" vs 34'-8 3/4"; 1'-10" vs 1'-11 7/16" (+1.45"); 1'-10" vs 1'-11 7/16" (+1.45") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | STR 11'-9" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 39'-8 1/2", 29'-3 1/2" | 37'-6" | BENT 1'-5 3/4" + 35'-0 11/16" + 1'-0 11/16" |  | not decoded |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 13'-10" + 13'-6" + 10'-11" (sum = list length) ; D 6" | 38'-3" | BENT 7 13/16" + 13'-4 15/16" + 11'-1" + 13'-7 7/8" + 7 13/16" (OTO from centerline) | 13'-10" vs 13'-7 7/8" (-2.15"); 13'-6" vs 13'-4 15/16" (-1.07"); 10'-11" vs 11'-1" (+2.03"); model extra 7 13/16", 7 13/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 32'-10" | STR 32'-10" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 12'-6" + 2'-0" (sum = list length) ; D 1'-0" | 14'-6" | L 12'-6" + 2'-0" ; D 1'-0" | 12'-6" vs 12'-6"; 2'-0" vs 2'-0" | match |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 15'-8" | STR 15'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 28'-8" | STR 28'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 15'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 16'-10" | BENT 15'-6" (straight within 12 deg (follows the cap profile)) | 15'-8" vs 15'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 28'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 29'-10" | BENT 28'-6" (straight within 12 deg (follows the cap profile)) | 28'-8" vs 28'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 15'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 19'-6" | U 2'-0" + 15'-6" + 2'-0" ; D 1'-0" | 15'-6" vs 15'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 28'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 32'-6" | U 2'-0" + 28'-6" + 2'-0" ; D 1'-0" | 28'-6" vs 28'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 2G (Design 316, sheet PDF p.24)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 38'-5" | BENT 1'-11 7/16" + 34'-8 11/16" + 1'-11 7/16" (OTO from centerline) | 34'-9" vs 34'-8 11/16"; 1'-10" vs 1'-11 7/16" (+1.46"); 1'-10" vs 1'-11 7/16" (+1.45") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | STR 11'-9" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-5" + 18'-5" + 4 1/2" + 4 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-7" | BENT 1'-2 7/16" + 35'-0 5/8" + 1'-5 1/16" (OTO from centerline) | 18'-5" vs 35'-0 5/8" (+199.64"); 18'-5" vs 1'-5 1/16" (-203.92"); 4 1/2" vs 1'-2 7/16" (+9.92"); 4 1/2" vs missing | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 13'-7" + 13'-5" + 10'-11" (sum = list length) ; D 6" | 37'-11" | BENT 5 5/16" + 13'-7 13/16" + 11'-1" + 13'-6" + 5 5/16" (OTO from centerline) | 13'-7" vs 13'-7 13/16" (+0.82"); 13'-5" vs 13'-6" (+0.97"); 10'-11" vs 11'-1" (+2.01"); model extra 5 5/16", 5 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN, VERTICAL | straight | 24'-11" | STR 24'-11" | +0.00" | match |
| 18d2 | COLUMN, VERTICAL | straight | 29'-11" | STR 29'-11" | +0.00" | match |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 20'-8" + 3'-5" (sum = list length) ; D 2'-0" | 24'-1" | L 20'-8" + 3'-5" ; D 2'-0" | 20'-8" vs 20'-8"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 15'-8" + 3'-5" (sum = list length) ; D 2'-0" | 19'-1" | L 15'-8" + 3'-5" ; D 2'-0" | 15'-8" vs 15'-8"; 3'-5" vs 3'-5" | match |
| 7d5 | PLINTH VERTICAL | straight | 11'-10" | - | no model mark | not modeled |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 18d7 | COLUMN, VERTICAL | straight | 21'-9" | STR 21'-9" | +0.00" | match |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-9 3/4" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-9 3/4" (+2.77"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 31'-2" | STR 31'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 28'-8" | STR 28'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 6'-7" | STR 6'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 31'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 32'-4" | BENT 31'-0" (straight within 12 deg (follows the cap profile)) | 31'-2" vs 31'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 28'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 29'-10" | BENT 28'-6" (straight within 12 deg (follows the cap profile)) | 28'-8" vs 28'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | bent: (not reconciled) ; D 10 3/4" | 34'-8" | BENT 1'-9 5/8" + 31'-2" + 1'-9 5/8" |  | not decoded |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | bent: (not reconciled) ; D 10 3/4" | 32'-2" | BENT 1'-9 5/8" + 28'-8" + 1'-9 5/8" |  | not decoded |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-4" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-3" + 5'-4" + 2'-3" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 3G (Design 316, sheet PDF p.27)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 38'-5" | BENT 1'-11 1/2" + 34'-9 5/16" + 1'-11 1/2" (OTO from centerline) | 34'-9" vs 34'-9 5/16"; 1'-10" vs 1'-11 1/2" (+1.48"); 1'-10" vs 1'-11 1/2" (+1.48") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 46'-8" | STR 46'-8" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-0" + 2'-0" (sum = list length) ; D 1'-0" | 15'-0" | L 13'-0" + 2'-0" ; D 1'-0" | 13'-0" vs 13'-0"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 13'-10" | STR 19'-3" | +65.00" | length +65.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 17'-8" | STR 17'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 17'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 18'-10" | BENT 17'-6" (straight within 12 deg (follows the cap profile)) | 17'-8" vs 17'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 27'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 28'-10" | BENT 27'-6" (straight within 12 deg (follows the cap profile)) | 27'-8" vs 27'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 17'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 21'-6" | U 2'-0" + 17'-6" + 2'-0" ; D 1'-0" | 17'-6" vs 17'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 27'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 31'-6" | U 2'-0" + 27'-6" + 2'-0" ; D 1'-0" | 27'-6" vs 27'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 4G (Design 316, sheet PDF p.30)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | BENT 2'-1 9/16" + 34'-9 7/16" + 2'-1 9/16" (OTO from centerline) | 34'-9" vs 34'-9 7/16"; 2'-0" vs 2'-1 9/16" (+1.54"); 2'-0" vs 2'-1 9/16" (+1.54") pin: plan 1'-0" / model none (straight) | MISMATCH |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 53'-8" | STR 53'-8" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-0" + 2'-0" (sum = list length) ; D 1'-0" | 15'-0" | L 13'-0" + 2'-0" ; D 1'-0" | 13'-0" vs 13'-0"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 16'-10" | STR 22'-3" | +65.00" | length +65.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 26'-2" | STR 26'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 26'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 27'-4" | BENT 26'-0" (straight within 12 deg (follows the cap profile)) | 26'-2" vs 26'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 27'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 28'-10" | BENT 27'-6" (straight within 12 deg (follows the cap profile)) | 27'-8" vs 27'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 26'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 30'-0" | U 2'-0" + 26'-0" + 2'-0" ; D 1'-0" | 26'-0" vs 26'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 27'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 31'-6" | U 2'-0" + 27'-6" + 2'-0" ; D 1'-0" | 27'-6" vs 27'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 5G (Design 316, sheet PDF p.33)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 9a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-7" + 1'-7" (sum = list length) ; D 9 1/2" | 37'-11" | BENT 1'-8 3/8" + 34'-9 3/16" + 1'-8 3/8" (OTO from centerline) | 34'-9" vs 34'-9 3/16"; 1'-7" vs 1'-8 3/8" (+1.40"); 1'-7" vs 1'-8 3/8" (+1.40") pin: plan 9 1/2" / model none (straight) | MISMATCH |
| 9a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 52'-8" | STR 52'-8" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 18'-2" + 2'-0" (sum = list length) ; D 1'-0" | 20'-2" | L 18'-2" + 2'-0" ; D 1'-0" | 18'-2" vs 18'-2"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 15'-10" | STR 22'-3" | +77.00" | length +77.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 6'-1" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 23'-8" | STR 23'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 28'-8" | STR 28'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 6'-7" | STR 6'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 23'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 24'-10" | BENT 23'-6" (straight within 12 deg (follows the cap profile)) | 23'-8" vs 23'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 28'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 29'-10" | BENT 28'-6" (straight within 12 deg (follows the cap profile)) | 28'-8" vs 28'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 23'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 27'-6" | U 2'-0" + 23'-6" + 2'-0" ; D 1'-0" | 23'-6" vs 23'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 |  | U: 28'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 32'-6" | U 2'-0" + 28'-6" + 2'-0" ; D 1'-0" | 28'-6" vs 28'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |

### Pier 6G (Design 316, sheet PDF p.36)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | BENT 2'-1 9/16" + 34'-9 7/16" + 2'-1 9/16" (OTO from centerline) | 34'-9" vs 34'-9 7/16"; 2'-0" vs 2'-1 9/16" (+1.54"); 2'-0" vs 2'-1 9/16" (+1.54") pin: plan 1'-0" / model none (straight) | MISMATCH |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 57'-8" | STR 57'-8" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-0" + 2'-0" (sum = list length) ; D 1'-0" | 15'-0" | L 13'-0" + 2'-0" ; D 1'-0" | 13'-0" vs 13'-0"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 17'-10" | STR 23'-3" | +65.00" | length +65.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 26'-2" | STR 26'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 26'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 27'-4" | BENT 26'-0" (straight within 12 deg (follows the cap profile)) | 26'-2" vs 26'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 27'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 28'-10" | BENT 27'-6" (straight within 12 deg (follows the cap profile)) | 27'-8" vs 27'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 26'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 30'-0" | U 2'-0" + 26'-0" + 2'-0" ; D 1'-0" | 26'-0" vs 26'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 27'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 31'-6" | U 2'-0" + 27'-6" + 2'-0" ; D 1'-0" | 27'-6" vs 27'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 7G (Design 316, sheet PDF p.39)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 38'-5" | BENT 1'-11 1/2" + 34'-9 5/16" + 1'-11 1/2" (OTO from centerline) | 34'-9" vs 34'-9 5/16"; 1'-10" vs 1'-11 1/2" (+1.48"); 1'-10" vs 1'-11 1/2" (+1.48") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN, VERTICAL | straight | 37'-8" | STR 37'-8" | +0.00" | match |
| 18d2 | COLUMN, VERTICAL | straight | 42'-8" | STR 42'-8" | +0.00" | match |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 21'-8" + 3'-5" (sum = list length) ; D 2'-0" | 25'-1" | L 21'-8" + 3'-5" ; D 2'-0" | 21'-8" vs 21'-8"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 16'-8" + 3'-5" (sum = list length) ; D 2'-0" | 20'-1" | L 16'-8" + 3'-5" ; D 2'-0" | 16'-8" vs 16'-8"; 3'-5" vs 3'-5" | match |
| 7d5 | PLINTH VERTICAL | straight | 15'-10" | - | no model mark | not modeled |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 18d7 | COLUMN, VERTICAL | straight | 34'-6" | STR 34'-6" | +0.00" | match |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-9 3/4" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-9 3/4" (+2.77"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 35'-2" | STR 35'-2" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 7'-7" | STR 7'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 27'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 28'-10" | BENT 27'-6" (straight within 12 deg (follows the cap profile)) | 27'-8" vs 27'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 35'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 36'-4" | BENT 35'-0" (straight within 12 deg (follows the cap profile)) | 35'-2" vs 35'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 27'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 31'-6" | U 2'-0" + 27'-6" + 2'-0" ; D 1'-0" | 27'-6" vs 27'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 35'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 39'-0" | U 2'-0" + 35'-0" + 2'-0" ; D 1'-0" | 35'-0" vs 35'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-4" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-3" + 5'-4" + 2'-3" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 8G (Design 316, sheet PDF p.42)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 38'-5" | BENT 1'-11 1/2" + 34'-9 5/16" + 1'-11 1/2" (OTO from centerline) | 34'-9" vs 34'-9 5/16"; 1'-10" vs 1'-11 1/2" (+1.48"); 1'-10" vs 1'-11 1/2" (+1.48") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 14d1 | COLUMN, VERTICAL | straight | 52'-8" | STR 52'-8" | +0.00" | match |
| 14d2 | COLUMN, VERTICAL | straight | 57'-8" | STR 57'-8" | +0.00" | match |
| 14d3 | FOOTING TO COLUMN DOWEL | L: 21'-2" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 23'-9" | L 21'-2" + 2'-7" ; D 1'-6 1/4" | 21'-2" vs 21'-2"; 2'-7" vs 2'-7" | match |
| 14d4 | FOOTING TO COLUMN DOWEL | L: 16'-2" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 18'-9" | L 16'-2" + 2'-7" ; D 1'-6 1/4" | 16'-2" vs 16'-2"; 2'-7" vs 2'-7" | match |
| 7d5 | PLINTH, VERTICAL | straight | 20'-10" | STR 27'-9" | +83.00" | length +83.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/4" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/4" (+3.27"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 24'-8" | STR 24'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 33'-8" | STR 33'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 7'-1" | STR 7'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 24'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 25'-10" | BENT 24'-6" (straight within 12 deg (follows the cap profile)) | 24'-8" vs 24'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 33'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 34'-10" | BENT 33'-6" (straight within 12 deg (follows the cap profile)) | 33'-8" vs 33'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 24'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 28'-6" | U 2'-0" + 24'-6" + 2'-0" ; D 1'-0" | 24'-6" vs 24'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 33'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 37'-6" | U 2'-0" + 33'-6" + 2'-0" ; D 1'-0" | 33'-6" vs 33'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 9G (Design 316, sheet PDF p.45)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 9a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-7" + 1'-7" (sum = list length) ; D 9 1/2" | 37'-11" | BENT 1'-8 3/8" + 34'-9 3/16" + 1'-8 3/8" (OTO from centerline) | 34'-9" vs 34'-9 3/16"; 1'-7" vs 1'-8 3/8" (+1.40"); 1'-7" vs 1'-8 3/8" (+1.40") pin: plan 9 1/2" / model none (straight) | MISMATCH |
| 9a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 39'-7" | STR 39'-7" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 17'-2" + 2'-0" (sum = list length) ; D 1'-0" | 19'-2" | L 17'-2" + 2'-0" ; D 1'-0" | 17'-2" vs 17'-2"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 20'-10" | STR 26'-3" | +65.00" | length +65.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 6'-1" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-6" + 3'-1" + 3'-1" (sum = list length) ; D 2 1/2" | 10'-8" | U 3'-1" + 4'-6" + 3'-1" ; D 2 1/2" | 4'-6" vs 4'-6"; 3'-1" vs 3'-1"; 3'-1" vs 3'-1" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-7" + 3'-1" + 3'-1" (sum = list length) ; D 2 1/2" | 11'-9" | U 3'-1" + 5'-7" + 3'-1" ; D 2 1/2" | 5'-7" vs 5'-7"; 3'-1" vs 3'-1"; 3'-1" vs 3'-1" | match |
| 5n2 | CAP, PEDESTAL, HOOP | bent: 4'-8" + 4'-8" + 2'-8" + 2'-8" + 6" + 6" (sum = list length) ; D 2 1/2" | 15'-8" | HOOP 4'-8" + 2'-8" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 4'-8" vs 4'-8"; 2'-8" vs 2'-8"; 6" vs 6" | match |
| 5n3 | CAP, PEDESTAL, TRANSVERSE | U: 3'-1" + 3'-1" + 2'-6" (sum = list length) ; D 2 1/2" | 8'-8" | U 3'-1" + 2'-6" + 3'-1" ; D 2 1/2" | 3'-1" vs 3'-1"; 3'-1" vs 3'-1"; 2'-6" vs 2'-6" | match |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 23'-8" | STR 23'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 31'-2" | STR 31'-2" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 23'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 24'-10" | BENT 23'-6" (straight within 12 deg (follows the cap profile)) | 23'-8" vs 23'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 31'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 32'-4" | BENT 31'-0" (straight within 12 deg (follows the cap profile)) | 31'-2" vs 31'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 23'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 27'-6" | U 2'-0" + 23'-6" + 2'-0" ; D 1'-0" | 23'-6" vs 23'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 31'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 35'-0" | U 2'-0" + 31'-0" + 2'-0" ; D 1'-0" | 31'-0" vs 31'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |

### Pier 10G (Design 316, sheet PDF p.48)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | BENT 2'-1 9/16" + 34'-9 7/16" + 2'-1 9/16" (OTO from centerline) | 34'-9" vs 34'-9 7/16"; 2'-0" vs 2'-1 9/16" (+1.54"); 2'-0" vs 2'-1 9/16" (+1.54") pin: plan 1'-0" / model none (straight) | MISMATCH |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 14d1 | COLUMN, VERTICAL | straight | 50'-8" | STR 50'-8" | +0.00" | match |
| 14d2 | COLUMN, VERTICAL | straight | 55'-8" | STR 55'-8" | +0.00" | match |
| 14d3 | FOOTING TO COLUMN DOWEL | L: 21'-2" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 23'-9" | L 21'-2" + 2'-7" ; D 1'-6 1/4" | 21'-2" vs 21'-2"; 2'-7" vs 2'-7" | match |
| 14d4 | FOOTING TO COLUMN DOWEL | L: 16'-2" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 18'-9" | L 16'-2" + 2'-7" ; D 1'-6 1/4" | 16'-2" vs 16'-2"; 2'-7" vs 2'-7" | match |
| 7d5 | PLINTH, VERTICAL | straight | 19'-10" | STR 26'-9" | +83.00" | length +83.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 6'-1" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/4" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/4" (+3.27"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 24'-8" | STR 24'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 33'-8" | STR 33'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 7'-1" | STR 7'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 24'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 25'-10" | BENT 24'-6" (straight within 12 deg (follows the cap profile)) | 24'-8" vs 24'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 33'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 34'-10" | BENT 33'-6" (straight within 12 deg (follows the cap profile)) | 33'-8" vs 33'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 24'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 28'-6" | U 2'-0" + 24'-6" + 2'-0" ; D 1'-0" | 24'-6" vs 24'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 33'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 37'-6" | U 2'-0" + 33'-6" + 2'-0" ; D 1'-0" | 33'-6" vs 33'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |

### Pier 11G (Design 316, sheet PDF p.51)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | BENT 2'-1 9/16" + 34'-9 7/16" + 2'-1 9/16" (OTO from centerline) | 34'-9" vs 34'-9 7/16"; 2'-0" vs 2'-1 9/16" (+1.54"); 2'-0" vs 2'-1 9/16" (+1.54") pin: plan 1'-0" / model none (straight) | MISMATCH |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN, VERTICAL | straight | 47'-8" | STR 47'-8" | +0.00" | match |
| 18d2 | COLUMN, VERTICAL | straight | 52'-8" | STR 52'-8" | +0.00" | match |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 21'-2" + 3'-5" (sum = list length) ; D 2'-0" | 24'-7" | L 21'-2" + 3'-5" ; D 2'-0" | 21'-2" vs 21'-2"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 16'-2" + 3'-5" (sum = list length) ; D 2'-0" | 19'-7" | L 16'-2" + 3'-5" ; D 2'-0" | 16'-2" vs 16'-2"; 3'-5" vs 3'-5" | match |
| 7d5 | PLINTH, VERTICAL | straight | 19'-10" | STR 26'-8" | +82.00" | length +82.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 6'-1" | - | no model mark | not modeled |
| 18d7 | COLUMN, VERTICAL | straight | 44'-6" | STR 44'-6" | +0.00" | match |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-9 3/4" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-9 3/4" (+2.77"); 6" vs missing | MISMATCH |
| 6e4 | PLINTH, HOOPS | bent: 6'-11" + 6'-11" + 3'-11" + 3'-11" + 3'-6" + 1'-10" (sum = list length) ; D 4 1/2" | 27'-0" | TIE 10'-5" + 6'-11" + 3'-5 7/8" + 1'-10" ; D 4 1/2" (as scheduled (A,B...)) | 6'-11" vs 10'-5" (+42.00"); 3'-11" vs 6'-11" (+36.00"); 3'-6" vs 3'-5 7/8"; 1'-10" vs 1'-10" | MISMATCH |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-4" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-3" + 5'-4" + 2'-3" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 7f1 | FOOTING, TOP | straight | 42'-2" | STR 42'-2" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 7'-1" | STR 7'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE | L: 42'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 43'-4" | BENT 42'-0" (straight within 12 deg (follows the cap profile)) | 42'-2" vs 42'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 14g1 | FOOTING, BOTTOM | U: 42'-0" + 2'-7" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 47'-2" | U 2'-7" + 42'-0" + 2'-7" ; D 1'-6 1/4" | 42'-0" vs 42'-0"; 2'-7" vs 2'-7"; 2'-7" vs 2'-7" | match |

### Pier 12G (Design 316, sheet PDF p.54)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | BENT 2'-1 9/16" + 34'-9 7/16" + 2'-1 9/16" (OTO from centerline) | 34'-9" vs 34'-9 7/16"; 2'-0" vs 2'-1 9/16" (+1.54"); 2'-0" vs 2'-1 9/16" (+1.54") pin: plan 1'-0" / model none (straight) | MISMATCH |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN, VERTICAL | straight | 50'-8" | STR 50'-8" | +0.00" | match |
| 18d2 | COLUMN, VERTICAL | straight | 55'-8" | STR 55'-8" | +0.00" | match |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 21'-2" + 3'-5" (sum = list length) ; D 2'-0" | 24'-7" | L 21'-2" + 3'-5" ; D 2'-0" | 21'-2" vs 21'-2"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 16'-2" + 3'-5" (sum = list length) ; D 2'-0" | 19'-7" | L 16'-2" + 3'-5" ; D 2'-0" | 16'-2" vs 16'-2"; 3'-5" vs 3'-5" | match |
| 7d5 | PLINTH, VERTICAL | straight | 22'-10" | STR 29'-8" | +82.00" | length +82.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 18d7 | COLUMN, VERTICAL | straight | 47'-6" | STR 47'-6" | +0.00" | match |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-9 3/4" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-9 3/4" (+2.77"); 6" vs missing | MISMATCH |
| 6e4 | PLINTH, HOOPS | bent: 6'-11" + 6'-11" + 3'-11" + 3'-11" + 3'-6" + 1'-10" (sum = list length) ; D 4 1/2" | 27'-0" | TIE 10'-5" + 6'-11" + 3'-5 7/8" + 1'-10" ; D 4 1/2" (as scheduled (A,B...)) | 6'-11" vs 10'-5" (+42.00"); 3'-11" vs 6'-11" (+36.00"); 3'-6" vs 3'-5 7/8"; 1'-10" vs 1'-10" | MISMATCH |
| 7f1 | FOOTING, TOP | straight | 42'-2" | STR 42'-2" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 7'-1" | STR 7'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDE | L: 42'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 43'-4" | BENT 42'-0" (straight within 12 deg (follows the cap profile)) | 42'-2" vs 42'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 14g1 | FOOTING, BOTTOM | U: 42'-0" + 2'-7" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 47'-2" | U 2'-7" + 42'-0" + 2'-7" ; D 1'-6 1/4" | 42'-0" vs 42'-0"; 2'-7" vs 2'-7"; 2'-7" vs 2'-7" | match |
| 5m1 | CAP, PEDESTAL LONGITUDINAL | U: 5'-4" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-3" + 5'-4" + 2'-3" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 13G (Design 316, sheet PDF p.57)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 38'-9" | BENT 2'-1 9/16" + 34'-9 7/16" + 2'-1 9/16" (OTO from centerline) | 34'-9" vs 34'-9 7/16"; 2'-0" vs 2'-1 9/16" (+1.54"); 2'-0" vs 2'-1 9/16" (+1.54") pin: plan 1'-0" / model none (straight) | MISMATCH |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 55'-8" | STR 55'-8" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-0" + 2'-0" (sum = list length) ; D 1'-0" | 15'-0" | L 13'-0" + 2'-0" ; D 1'-0" | 13'-0" vs 13'-0"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 16'-10" | STR 22'-3" | +65.00" | length +65.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 26'-2" | STR 26'-2" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 26'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 27'-4" | BENT 26'-0" (straight within 12 deg (follows the cap profile)) | 26'-2" vs 26'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 27'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 28'-10" | BENT 27'-6" (straight within 12 deg (follows the cap profile)) | 27'-8" vs 27'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 26'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 30'-0" | U 2'-0" + 26'-0" + 2'-0" ; D 1'-0" | 26'-0" vs 26'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 27'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 31'-6" | U 2'-0" + 27'-6" + 2'-0" ; D 1'-0" | 27'-6" vs 27'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 14G (Design 316, sheet PDF p.60)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 9a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-7" + 1'-7" (sum = list length) ; D 9 1/2" | 37'-11" | BENT 1'-8 3/8" + 34'-9 3/16" + 1'-8 3/8" (OTO from centerline) | 34'-9" vs 34'-9 3/16"; 1'-7" vs 1'-8 3/8" (+1.40"); 1'-7" vs 1'-8 3/8" (+1.40") pin: plan 9 1/2" / model none (straight) | MISMATCH |
| 9a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-1" + 18'-1" + 1'-2 1/2" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-5" | BENT 10 7/16" + 35'-1 5/16" + 1'-6 3/8" (OTO from centerline) | 18'-1" vs 35'-1 5/16" (+204.34"); 18'-1" vs 1'-6 3/8" (-198.63"); 1'-2 1/2" vs 10 7/16" (-4.06") | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-1" + 13'-1" + 10'-11" (sum = list length) ; D 6" | 38'-1" | BENT 6 5/16" + 14'-0 15/16" + 11'-1 1/4" + 13'-1 3/16" + 6 5/16" (OTO from centerline) | 14'-1" vs 14'-0 15/16"; 13'-1" vs 13'-1 3/16"; 10'-11" vs 11'-1 1/4" (+2.23"); model extra 6 5/16", 6 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 52'-8" | STR 52'-8" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 17'-2" + 2'-0" (sum = list length) ; D 1'-0" | 19'-2" | L 17'-2" + 2'-0" ; D 1'-0" | 17'-2" vs 17'-2"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 15'-10" | STR 21'-3" | +65.00" | length +65.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 6'-1" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 21'-8" | STR 21'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 31'-2" | STR 31'-2" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 21'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 22'-10" | BENT 21'-6" (straight within 12 deg (follows the cap profile)) | 21'-8" vs 21'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 31'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 32'-4" | BENT 31'-0" (straight within 12 deg (follows the cap profile)) | 31'-2" vs 31'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 21'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 25'-6" | U 2'-0" + 21'-6" + 2'-0" ; D 1'-0" | 21'-6" vs 21'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 |  | U: 31'-0" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 35'-0" | U 2'-0" + 31'-0" + 2'-0" ; D 1'-0" | 31'-0" vs 31'-0"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |

### Pier 15G (Design 316, sheet PDF p.63)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 38'-5" | BENT 1'-11 1/2" + 34'-9" + 1'-11 1/2" (OTO from centerline) | 34'-9" vs 34'-9"; 1'-10" vs 1'-11 1/2" (+1.48"); 1'-10" vs 1'-11 1/2" (+1.48") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: 18'-10" + 18'-10" (sum = list length) ; R 29'-3 1/2", 39'-8 1/2" | 37'-8" | BENT 1'-0 5/16" + 35'-1" + 1'-7 13/16" (OTO from centerline) | 18'-10" vs 35'-1" (+195.02"); 18'-10" vs 1'-7 13/16" (-206.21"); model extra 1'-0 5/16" | MISMATCH |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-0" + 13'-3" + 10'-11" (sum = list length) ; D 6" | 38'-2" | BENT 7 5/16" + 13'-10 5/16" + 11'-1 1/8" + 13'-2 5/8" + 7 5/16" (OTO from centerline) | 14'-0" vs 13'-10 5/16" (-1.70"); 13'-3" vs 13'-2 5/8"; 10'-11" vs 11'-1 1/8" (+2.13"); model extra 7 5/16", 7 5/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 11d1 | COLUMN, VERTICAL | straight | 45'-9" | STR 45'-9" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 13'-0" + 2'-0" (sum = list length) ; D 1'-0" | 15'-0" | L 13'-0" + 2'-0" ; D 1'-0" | 13'-0" vs 13'-0"; 2'-0" vs 2'-0" | match |
| 7d5 | PLINTH, VERTICAL | straight | 13'-10" | STR 19'-3" | +65.00" | length +65.00" |
| 7d6 | FOOTING TO PLINTH DOWEL | straight | 5'-3" | - | no model mark | not modeled |
| 6e1 | COLUMN, HOOPS | bent: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 6e3 | PLINTH, HAIRPINS | bent: 3'-11" + 3'-11" + 3'-3" + 3'-1" + 3'-1" (sum = list length) ; D 4 1/2" | 17'-3" | - |  | not modeled |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 17'-8" | STR 17'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 5'-7" | STR 5'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 17'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 18'-10" | BENT 17'-6" (straight within 12 deg (follows the cap profile)) | 17'-8" vs 17'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 27'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 28'-10" | BENT 27'-6" (straight within 12 deg (follows the cap profile)) | 27'-8" vs 27'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 17'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 21'-6" | U 2'-0" + 17'-6" + 2'-0" ; D 1'-0" | 17'-6" vs 17'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 27'-6" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 31'-6" | U 2'-0" + 27'-6" + 2'-0" ; D 1'-0" | 27'-6" vs 27'-6"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-2" | U 2'-3" + 4'-8" + 2'-3" ; D 2 1/2" | 4'-8" vs 4'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 16G (Design 316, sheet PDF p.67)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 11a1 | CAP, TOP, LONGITUDINAL | L: 37'-3" + 2'-0" (sum = list length) ; D 1'-0" | 39'-3" | STR 39'-3" | 37'-3" vs 39'-3" (+24.00"); 2'-0" vs missing pin: plan 1'-0" / model none (straight) | MISMATCH (model straight) |
| 11a2 | CAP, TOP, LONGITUDINAL | straight | 37'-0" | STR 37'-0" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 33'-4" | STR 33'-4" | +0.00" | match |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | STR 32'-0" | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 55'-1" | BENT 55'-1" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 33'-2" | STR 33'-2" | +0.00" | match |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 29'-9" | STR 29'-9" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 20'-0" | STR 20'-0" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: 17'-2" + 16'-7" + 4 1/2" (sum = list length) ; R 25'-3 1/2", 61'-2 1/2" | 34'-2" | STR 34'-2" | 17'-2" vs 34'-2" (+204.00"); 16'-7" vs missing; 4 1/2" vs missing | MISMATCH (model straight) |
| 5a10 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 61'-2 1/2", 42'-3 1/2" | 34'-2" | STR 34'-2" |  | not decoded |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 3'-6" | STR 3'-6" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 4'-10" | STR 4'-10" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 11b1 | CAP, BOTTOM, LONGITUDINAL | straight | 47'-5" | BENT 47'-5" | +0.00" | MISMATCH (model bent) |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 9'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-0" | CS 2'-0" + 9'-0" ; D 4 1/2" (as scheduled (A,B...)) | 9'-0" vs 9'-0"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | bent: (not reconciled) ; D 2 1/2" | 11'-3" | BENT 5'-5 13/16" + 4 5/8" + 5'-5 13/16" |  | not decoded |
| 5c6 | CAP, HAIRPINS, VERTICAL | bent: (not reconciled) ; D 2 1/2" | 9'-5" | BENT 4'-6 13/16" + 4 5/8" + 4'-6 13/16" |  | not decoded |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 14d1 | COLUMN, VERTICAL | straight | 25'-0" | STR 25'-0" | +0.00" | match |
| 14d2 | COLUMN, VERTICAL | straight | 30'-0" | STR 30'-0" | +0.00" | match |
| 14d3 | FOOTING TO COLUMN DOWEL | L: 14'-3" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 16'-10" | L 14'-3" + 2'-7" ; D 1'-6 1/4" | 14'-3" vs 14'-3"; 2'-7" vs 2'-7" | match |
| 14d4 | FOOTING TO COLUMN DOWEL | L: 9'-3" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 11'-10" | L 9'-3" + 2'-7" ; D 1'-6 1/4" | 9'-3" vs 9'-3"; 2'-7" vs 2'-7" | match |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6" + 2'-6" + 2'-6" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-9" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-4" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-4" | CROSSTIE 3'-1 7/8" ; D 2 1/2" (as scheduled (A,B...)) | 3'-4" vs 3'-1 7/8" (-2.14"); 6" vs missing | MISMATCH |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 15'-8" | STR 15'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 21'-8" | STR 21'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDES | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 15'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 16'-10" | BENT 15'-6" (straight within 12 deg (follows the cap profile)) | 15'-8" vs 15'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 21'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 22'-10" | BENT 21'-6" (straight within 12 deg (follows the cap profile)) | 21'-8" vs 21'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | bent: (not reconciled) ; D 10 3/4" | 19'-2" | BENT 1'-9 5/8" + 15'-8" + 1'-9 5/8" |  | not decoded |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | bent: (not reconciled) ; D 10 3/4" | 25'-2" | BENT 1'-9 5/8" + 21'-8" + 1'-9 5/8" |  | not decoded |
| 5m1 | CAP, PEDESTAL, LONGITUDNAL | U: 6'-10" + 2'-1" + 2'-1" (sum = list length) ; D 2 1/2" | 11'-0" | U 2'-1" + 6'-10" + 2'-1" ; D 2 1/2" | 6'-10" vs 6'-10"; 2'-1" vs 2'-1"; 2'-1" vs 2'-1" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-1" + 2'-1" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-1" + 5'-4" + 2'-1" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-1" vs 2'-1"; 2'-1" vs 2'-1" | match |
| 6n2 | CAP, PEDESTAL, TRANSVERSE | U: 4'-0" + 4'-0" + 1'-3" (sum = list length) ; D 4 1/2" | 9'-3" | U 4'-0" + 1'-3" + 4'-0" ; D 4 1/2" | 4'-0" vs 4'-0"; 4'-0" vs 4'-0"; 1'-3" vs 1'-3" | match |

### Pier 17G (Design 316, sheet PDF p.71)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 52'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 56'-5" | BENT 1'-11 11/16" + 52'-8 7/16" + 1'-11 11/16" (OTO from centerline) | 52'-9" vs 52'-8 7/16" (-0.59"); 1'-10" vs 1'-11 11/16" (+1.67"); 1'-10" vs 1'-11 11/16" (+1.67") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 52'-6" | STR 52'-6" | +0.00" | match |
| 6a3 | CAP, SIDES, LONGITUDINAL | straight | 53'-2" | BENT 53'-2" | +0.00" | MISMATCH (model bent) |
| 6a4 | CAP, SIDES, LONGITUDINAL | straight | 50'-5" | BENT 50'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 6a5 | CAP, SIDES, LONGITUDINAL | straight | 45'-1" | BENT 45'-1" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a6 | CAP, SIDES, LONGITUDINAL | straight | 53'-4" | BENT 53'-4" | +0.00" | MISMATCH (model bent) |
| 5a7 | CAP, SIDES, LONGITUDINAL | straight | 25'-1" | STR 25'-1" | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 16'-9" | STR 16'-9" | +0.00" | match |
| 5a9 | CAP, SIDES, LONGITUDINAL | arcs: 21'-11" + 18'-5 1/2" + 14'-1" + 6 1/2" + 6 1/2" (sum = list length) ; R 30'-9 1/2", 43'-8 1/2", 18'-9 1/2" | 55'-6" | BENT 1'-1 5/16" + 53'-0 13/16" + 1'-4 3/4" (OTO from centerline) | 21'-11" vs 53'-0 13/16" (+373.82"); 18'-5 1/2" vs 1'-4 3/4" (-204.73"); 14'-1" vs 1'-1 5/16" (-155.66"); 6 1/2" vs missing; 6 1/2" vs missing | MISMATCH |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 2'-10" | STR 2'-10" | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | straight | 4'-0" | STR 4'-0" | +0.00" | match |
| 5a13 | CAP, ENDS, TRANSVERSE | U: 5'-3" + 2'-0" + 2'-0" (sum = list length) ; D 2 1/2" | 9'-3" | U 2'-0" + 5'-3" + 2'-0" ; D 2 1/2" | 5'-3" vs 5'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 5a14 | CAP, ENDS, VERTICAL | U: 4'-0" + 2'-0" + 10" (sum = list length) ; D 2 1/2" | 6'-10" | A14 2'-0" + 4'-0" + 10" ; D 2 1/2" | 4'-0" vs 4'-0"; 2'-0" vs 2'-0"; 10" vs 10" | match |
| 10b1 | CAP, BOTTOM, LONGITUDINAL | straight | 37'-5" | BENT 37'-5" | +0.00" | MISMATCH (model bent) |
| 6b2 | CAP, BOTTOM, CANTILEVER | L: 8'-11" + 2'-0" (sum = list length) ; D 4 1/2" | 10'-11" | CS 2'-0" + 8'-11" ; D 4 1/2" (as scheduled (A,B...)) | 8'-11" vs 8'-11"; 2'-0" vs 2'-0" | match |
| 6c1 | CAP, HOOPS | bent: 6'-8" + 6'-8" + 3'-10" + 3'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 23'-0" | HOOP 6'-8" + 3'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 6'-8" vs 6'-8"; 3'-10" vs 3'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 6'-8" MAX., 4'-6" MIN. ; D 4 1/2" | VARIES | HOOP 6'-8" + 3'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 6c3 | CAP, TIE | bent: 6'-8" + 1'-0" + 8" (sum = list length) ; D 4 1/2" | 8'-4" | BENT 1'-10" + 4'-8" + 1'-10" (OTO from centerline) | 6'-8" vs 4'-8" (-24.00"); 1'-0" vs 1'-10" (+10.00"); 8" vs 1'-10" (+14.00") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 6c4 | CAP, TIE, CANTILEVER | bent: (not reconciled) ; 4'-6" MIN., 6'-8" MAX. ; D 4 1/2" | VARIES | BENT 1'-3 1/2" + 4'-8" + 1'-3 1/2" |  | not decoded |
| 5c5 | CAP, HAIRPINS, VERTICAL | bent: (not reconciled) ; D 2 1/2" | 11'-3" | BENT 5'-5 13/16" + 4 5/8" + 5'-5 13/16" |  | not decoded |
| 5c6 | CAP, HAIRPINS, VERTICAL | bent: (not reconciled) ; D 2 1/2" | 9'-5" | BENT 4'-6 13/16" + 4 5/8" + 4'-6 13/16" |  | not decoded |
| 4c7 | CAP, TRANSVERSE | U: 4'-8" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 5'-5" | U 4 1/2" + 4'-8" + 4 1/2" ; D 2" | 4'-8" vs 4'-8"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 14d1 | COLUMN, VERTICAL | straight | 19'-10" | STR 19'-10" | +0.00" | match |
| 14d2 | COLUMN, VERTICAL | straight | 24'-10" | STR 24'-10" | +0.00" | match |
| 14d3 | FOOTING TO COLUMN DOWEL | L: 14'-3" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 16'-10" | L 14'-3" + 2'-7" ; D 1'-6 1/4" | 14'-3" vs 14'-3"; 2'-7" vs 2'-7" | match |
| 14d4 | FOOTING TO COLUMN DOWEL | L: 9'-3" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 11'-10" | L 9'-3" + 2'-7" ; D 1'-6 1/4" | 9'-3" vs 9'-3"; 2'-7" vs 2'-7" | match |
| 14d5 | COLUMN, VERTICAL | straight | 20'-7" | STR 20'-6" | -1.00" | length -1.00" |
| 14d6 | COLUMN, VERTICAL | straight | 25'-7" | STR 25'-6" | -1.00" | length -1.00" |
| 6e1 | COLUMN, HOOPS | bent: 4'-2" + 3'-5" + 3'-5" + 2'-6" + 2'-6" + 2'-6" + 1'-2 1/2" (sum = list length) ; D 4 1/2" | 19'-9" | TIE 5'-5 3/4" + 4'-2" ; D 4 1/2" (as scheduled (A,B...)) | 4'-2" vs 5'-5 3/4" (+15.78"); 3'-5" vs 4'-2" (+9.00"); 2'-6" vs missing; 1'-2 1/2" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 3'-4" + 6" + 6" (sum = list length) ; D 2 1/2" | 4'-4" | CROSSTIE 3'-1 7/8" ; D 2 1/2" (as scheduled (A,B...)) | 3'-4" vs 3'-1 7/8" (-2.14"); 6" vs missing | MISMATCH |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 15'-8" | STR 15'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 21'-8" | STR 21'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDES | straight | 5'-1" | STR 5'-1" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 15'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 16'-10" | BENT 15'-6" (straight within 12 deg (follows the cap profile)) | 15'-8" vs 15'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 21'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 22'-10" | BENT 21'-6" (straight within 12 deg (follows the cap profile)) | 21'-8" vs 21'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 10g1 | FOOTING, BOTTOM, TRANSVERSE | U: 15'-6" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 19'-2" | U 1'-10" + 15'-6" + 1'-10" ; D 10 3/4" | 15'-6" vs 15'-6"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 10g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 21'-6" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 25'-2" | U 1'-10" + 21'-6" + 1'-10" ; D 10 3/4" | 21'-6" vs 21'-6"; 1'-10" vs 1'-10"; 1'-10" vs 1'-10" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDNAL | U: 5'-8" + 2'-1" + 2'-1" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-1" + 5'-8" + 2'-1" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-1" vs 2'-1"; 2'-1" vs 2'-1" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-4" + 2'-1" + 2'-1" (sum = list length) ; D 2 1/2" | 9'-6" | U 2'-1" + 5'-4" + 2'-1" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-1" vs 2'-1"; 2'-1" vs 2'-1" | match |
| 6n2 | CAP, PEDESTAL, TRANSVERSE | U: 4'-0" + 4'-0" + 1'-3" (sum = list length) ; D 4 1/2" | 9'-3" | BENT 2'-3 3/8" + 4'-9 3/4" + 2'-3 3/8" (OTO from centerline) | 4'-0" vs 4'-9 3/4" (+9.75"); 4'-0" vs 2'-3 3/8" (-20.62"); 1'-3" vs 2'-3 3/8" (+12.38") pin: plan 4 1/2" / model none (straight) | MISMATCH |

### Pier 18G (Design 316, sheet PDF p.74)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 34'-9" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 38'-5" | BENT 1'-11 1/2" + 34'-9 5/16" + 1'-11 1/2" (OTO from centerline) | 34'-9" vs 34'-9 5/16"; 1'-10" vs 1'-11 1/2" (+1.48"); 1'-10" vs 1'-11 1/2" (+1.48") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 34'-3" | STR 34'-3" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 35'-1" | BENT 35'-1" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 32'-0" | BENT 32'-0" | +0.00" | MISMATCH (model bent) |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 26'-9" | BENT 26'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 21'-8" | BENT 21'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 16'-8" | BENT 16'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 35'-4" | BENT 35'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 23'-11" | BENT 23'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 19'-5" | BENT 19'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 11'-9" | BENT 11'-9" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 29'-3 1/2" | 37'-7" | BENT 1'-8 1/8" + 35'-1 5/16" + 10 11/16" |  | not decoded |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-5" | STR 5'-5" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 14'-2" + 13'-2" + 10'-11" (sum = list length) ; D 6" | 38'-3" | BENT 7 13/16" + 13'-1 3/16" + 11'-1 1/4" + 13'-11 7/8" + 7 13/16" (OTO from centerline) | 14'-2" vs 13'-11 7/8" (-2.10"); 13'-2" vs 13'-1 3/16" (-0.80"); 10'-11" vs 11'-1 1/4" (+2.23"); model extra 7 13/16", 7 13/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: 8'-8" + 8'-8" + 4'-10" + 4'-10" + 1'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 29'-0" | HOOP 8'-8" + 4'-10" + 1'-0" ; D 4 1/2" (as scheduled (A,B...)) | 8'-8" vs 8'-8"; 4'-10" vs 4'-10"; 1'-0" vs 1'-0" | match |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-7" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 18d1 | COLUMN, VERTICAL | straight | 14'-8" | STR 14'-8" | +0.00" | match |
| 18d2 | COLUMN, VERTICAL | straight | 19'-8" | STR 19'-8" | +0.00" | match |
| 18d3 | FOOTING TO COLUMN DOWEL | L: 20'-7" + 3'-5" (sum = list length) ; D 2'-0" | 24'-0" | L 20'-7" + 3'-5" ; D 2'-0" | 20'-7" vs 20'-7"; 3'-5" vs 3'-5" | match |
| 18d4 | FOOTING TO COLUMN DOWEL | L: 15'-7" + 3'-5" (sum = list length) ; D 2'-0" | 19'-0" | L 15'-7" + 3'-5" ; D 2'-0" | 15'-7" vs 15'-7"; 3'-5" vs 3'-5" | match |
| 18d5 | COLUMN, VERTICAL | straight | 11'-6" | STR 11'-6" | +0.00" | match |
| 6e1 | COLUMN, HOOPS | arcs: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; R 39'-8 1/2" ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-9 3/4" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-9 3/4" (+2.77"); 6" vs missing | MISMATCH |
| 8f1 | FOOTING, TOP, TRANSVERSE | straight | 42'-2" | STR 42'-2" | +0.00" | match |
| 8f2 | FOOTING, TOP, LONGITUDINAL | straight | 31'-8" | STR 31'-8" | +0.00" | match |
| 7f3 | FOOTING, SIDE | straight | 6'-7" | STR 6'-7" | +0.00" | match |
| 7f4 | FOOTING, SIDES, TRANSVERSE | L: 42'-2" + 1'-2" (sum = list length) ; D 5 1/4" | 43'-4" | BENT 42'-0" (straight within 12 deg (follows the cap profile)) | 42'-2" vs 42'-0" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 7f5 | FOOTING, SIDES, LONGITUDINAL | L: 31'-8" + 1'-2" (sum = list length) ; D 5 1/4" | 32'-10" | BENT 31'-6" (straight within 12 deg (follows the cap profile)) | 31'-8" vs 31'-6" (-2.00"); 1'-2" vs missing pin: plan 5 1/4" / model none (straight) | MISMATCH |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 14g1 | FOOTING, BOTTOM, TRANSVERSE | U: 42'-0" + 2'-7" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 47'-2" | U 2'-7" + 42'-0" + 2'-7" ; D 1'-6 1/4" | 42'-0" vs 42'-0"; 2'-7" vs 2'-7"; 2'-7" vs 2'-7" | match |
| 14g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 31'-6" + 2'-7" + 2'-7" (sum = list length) ; D 1'-6 1/4" | 36'-8" | U 2'-7" + 31'-6" + 2'-7" ; D 1'-6 1/4" | 31'-6" vs 31'-6"; 2'-7" vs 2'-7"; 2'-7" vs 2'-7" | match |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 5'-4" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 9'-10" | U 2'-3" + 5'-4" + 2'-3" ; D 2 1/2" | 5'-4" vs 5'-4"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 5'-8" + 2'-3" + 2'-3" (sum = list length) ; D 2 1/2" | 10'-2" | U 2'-3" + 5'-8" + 2'-3" ; D 2 1/2" | 5'-8" vs 5'-8"; 2'-3" vs 2'-3"; 2'-3" vs 2'-3" | match |

### Pier 19G (Design 316, sheet PDF p.77)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 10a1 | CAP, TOP, LONGITUDINAL | U: 39'-10" + 1'-10" + 1'-10" (sum = list length) ; D 10 3/4" | 43'-6" | BENT 2'-0 3/16" + 39'-8 11/16" + 2'-0 3/16" (OTO from centerline) | 39'-10" vs 39'-8 11/16" (-1.30"); 1'-10" vs 2'-0 3/16" (+2.20"); 1'-10" vs 2'-0 3/16" (+2.20") pin: plan 10 3/4" / model none (straight) | MISMATCH |
| 10a2 | CAP, TOP, LONGITUDINAL | straight | 39'-5" | STR 39'-5" | +0.00" | match |
| 8a3 | CAP, SIDES, LONGITUDINAL | straight | 40'-2" | BENT 40'-2" | +0.00" | MISMATCH (model bent) |
| 8a4 | CAP, SIDES, LONGITUDINAL | straight | 35'-11" | BENT 35'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a5 | CAP, SIDES, LONGITUDINAL | straight | 29'-8" | BENT 29'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a6 | CAP, SIDES, LONGITUDINAL | straight | 23'-8" | BENT 23'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 8a7 | CAP, SIDES, LONGITUDINAL | straight | 17'-8" | BENT 17'-8" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a8 | CAP, SIDES, LONGITUDINAL | straight | 40'-4" | BENT 40'-4" | +0.00" | MISMATCH (model bent) |
| 5a9 | CAP, SIDES, LONGITUDINAL | straight | 27'-11" | BENT 27'-11" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a10 | CAP, SIDES, LONGITUDINAL | straight | 23'-0" | BENT 23'-0" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a11 | CAP, SIDES, LONGITUDINAL | straight | 14'-6" | BENT 14'-6" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5a12 | CAP, SIDES, LONGITUDINAL | arcs: (not reconciled) ; R 34'-3 1/2" | 42'-7" | BENT 1'-0" + 40'-1 3/16" + 1'-6 7/8" |  | not decoded |
| 5a13 | CAP, SIDES, LONGITUDINAL | straight | 5'-8" | STR 5'-8" | +0.00" | match |
| 6a14 | CAP, ENDS, TRANSVERSE | U: 7'-3" + 2'-0" + 2'-0" (sum = list length) ; D 4 1/2" | 11'-3" | U 2'-0" + 7'-3" + 2'-0" ; D 4 1/2" | 7'-3" vs 7'-3"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 6a15 | CAP, ENDS, VERTICAL | U: 5'-0" + 2'-0" + 1'-0" (sum = list length) ; D 4 1/2" | 8'-0" | A14 2'-0" + 5'-0" + 1'-0" ; D 4 1/2" | 5'-0" vs 5'-0"; 2'-0" vs 2'-0"; 1'-0" vs 1'-0" | match |
| 8b1 | CAP, BOTTOM, LONGITUDINAL | U: 16'-5" + 15'-4" + 10'-11" (sum = list length) ; D 6" | 42'-8" | BENT 3 9/16" + 16'-8 1/16" + 11'-1 3/16" + 15'-4 15/16" + 3 9/16" (OTO from centerline) | 16'-5" vs 16'-8 1/16" (+3.07"); 15'-4" vs 15'-4 15/16" (+0.95"); 10'-11" vs 11'-1 3/16" (+2.16"); model extra 3 9/16", 3 9/16" pin: plan 6" / model none (straight) | MISMATCH |
| 6c1 | CAP, HOOPS | bent: (not reconciled) ; D 4 1/2" | 29'-0" | TIE 6'-8" + 7'-5 3/16" ; D 4 1/2" |  | not decoded |
| 6c2 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-8" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 5c3 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-2" + 3'-2" (sum = list length) ; D 2 1/2" | 13'-9" | U 3'-2" + 7'-5" + 3'-2" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-2" vs 3'-2"; 3'-2" vs 3'-2" | match |
| 5c4 | CAP, HAIRPINS, VERTICAL | U: 7'-5" + 3'-10" + 3'-10" (sum = list length) ; D 2 1/2" | 15'-1" | U 3'-10" + 7'-5" + 3'-10" ; D 2 1/2" | 7'-5" vs 7'-5"; 3'-10" vs 3'-10"; 3'-10" vs 3'-10" | match |
| 4c5 | CAP, TRANSVERSE | U: 7'-5" + 4 1/2" + 4 1/2" (sum = list length) ; D 2" | 8'-2" | U 4 1/2" + 7'-5" + 4 1/2" ; D 2" | 7'-5" vs 7'-5"; 4 1/2" vs 4 1/2"; 4 1/2" vs 4 1/2" | match |
| 6c6 | CAP, HOOPS, CANTILEVER | bent: (not reconciled) ; 5'-5" MIN., 8'-7" MAX. ; D 4 1/2" | VARIES | HOOP 8'-7" + 4'-10" + 1'-0" + 1'-0" ; D 4 1/2" |  | not decoded |
| 11d1 | COLUMN, VERTICAL | straight | 31'-9" | STR 31'-9" | +0.00" | match |
| 11d2 | FOOTING TO COLUMN DOWEL | L: 16'-2" + 2'-0" (sum = list length) ; D 1'-0" | 18'-2" | L 16'-2" + 2'-0" ; D 1'-0" | 16'-2" vs 16'-2"; 2'-0" vs 2'-0" | match |
| 6e1 | COLUMN, HOOPS | arcs: 6'-2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-7 1/2" + 3'-5" + 1'-0" (sum = list length) ; R 59'-8 1/2" ; D 4 1/2" | 25'-1" | TIE 6'-5 13/16" + 6'-2" ; D 4 1/2" (as scheduled (A,B...)) | 6'-2" vs 6'-5 13/16" (+3.79"); 3'-7 1/2" vs 6'-2" (+30.50"); 3'-5" vs missing; 1'-0" vs missing | MISMATCH |
| 5e2 | COLUMN, TIES | U: 4'-7" + 6" + 6" (sum = list length) ; D 2 1/2" | 5'-7" | CROSSTIE 4'-10 1/2" ; D 2 1/2" (as scheduled (A,B...)) | 4'-7" vs 4'-10 1/2" (+3.52"); 6" vs missing | MISMATCH |
| 5m1 | CAP, PEDESTAL, LONGITUDINAL | U: 4'-8" + 3'-4" + 3'-4" (sum = list length) ; D 2 1/2" | 11'-4" | U 3'-4" + 4'-8" + 3'-4" ; D 2 1/2" | 4'-8" vs 4'-8"; 3'-4" vs 3'-4"; 3'-4" vs 3'-4" | match |
| 8m2 | CAP STEP, TOP, LONGITUDINAL | straight | 39'-5" | STR 39'-5" | +0.00" | match |
| 5m3 | CAP STEP, SIDE, LONGITUDINAL | straight | 39'-5" | BENT 39'-5" (kinks < 12 deg: follows the cap profile) | +0.00" | match |
| 5n1 | CAP, PEDESTAL, TRANSVERSE | U: 3'-5" + 3'-4" + 3'-4" (sum = list length) ; D 2 1/2" | 10'-1" | U 3'-4" + 3'-5" + 3'-4" ; D 2 1/2" | 3'-5" vs 3'-5"; 3'-4" vs 3'-4"; 3'-4" vs 3'-4" | match |
| 6n2 | CAP STEP, TRANSVERSE | U: 3'-5" + 2'-9" + 2'-9" (sum = list length) ; D 4 1/2" | 8'-11" | BENT 1'-1 1/2" + 6'-8" + 1'-1 1/2" (OTO from centerline) | 3'-5" vs 6'-8" (+39.00"); 2'-9" vs 1'-1 1/2" (-19.51"); 2'-9" vs 1'-1 1/2" (-19.51") pin: plan 4 1/2" / model none (straight) | MISMATCH |
| 7f1 | FOOTING, TOP, TRANSVERSE | straight | 21'-8" | STR 21'-8" | +0.00" | match |
| 7f2 | FOOTING, TOP, LONGITUDINAL | straight | 27'-8" | STR 27'-8" | +0.00" | match |
| 8f6 | PILE UPLIFT ANCHORS | bent: 2'-6" + 2'-6" (sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 11g1 | FOOTING, BOTTOM, TRANSVERSE | U: 21'-8" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 25'-8" | U 2'-0" + 21'-8" + 2'-0" ; D 1'-0" | 21'-8" vs 21'-8"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |
| 11g2 | FOOTING, BOTTOM, LONGITUDINAL | U: 27'-8" + 2'-0" + 2'-0" (sum = list length) ; D 1'-0" | 31'-8" | U 2'-0" + 27'-8" + 2'-0" ; D 1'-0" | 27'-8" vs 27'-8"; 2'-0" vs 2'-0"; 2'-0" vs 2'-0" | match |

### South Abut (Design 316, sheet PDF p.83)

| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |
|---|---|---|---|---|---|---|
| 8a1 | FOOTING, LONGITUDINAL | straight | 37'-6" | STR 37'-6" | +0.00" | match |
| 5b1 | FOOTING, HOOPS | hoop: 5'-8" + 3'-5" + 6" (read on clip; sum = list length) ; D 2 1/2" | 19'-2" | TIE 5'-8" + 3'-5" ; D 2 1/2" (as scheduled (A,B...)) | 5'-8" vs 5'-8"; 3'-5" vs 3'-5"; 6" vs missing | MISMATCH |
| 5b2 | FOOTING, END, HORIZONTAL | seg: 5'-7" + 1'-6" + 1'-6" (read on clip; sum = list length) ; D 2 1/2" | 8'-7" | U 1'-6" + 5'-7" + 1'-6" ; D 2 1/2" | 5'-7" vs 5'-7"; 1'-6" vs 1'-6"; 1'-6" vs 1'-6" | match |
| 6d1 | BACKWALL, B.F., VERTICAL | straight | 11'-4" | STR 11'-4" | +0.00" | match |
| 5d2 | BACKWALL, F.F., VERTICAL | straight | 11'-0" | STR 11'-0" | +0.00" | match |
| 5d3 | PAVING NOTCH | seg: 2'-5" + 1'-0" + 1'-0" (read on clip; sum = list length) ; D 2 1/2" | 4'-5" | U 1'-0" + 2'-5" + 1'-0" ; D 2 1/2" | 2'-5" vs 2'-5"; 1'-0" vs 1'-0"; 1'-0" vs 1'-0" | match |
| 5d4 | PAVING NOTCH | seg: 1'-5" + 1'-3" + 1'-0" (read on clip; sum = list length) ; D 2 1/2" | 3'-8" | STR 3'-8" | 1'-5" vs 3'-8" (+27.00"); 1'-3" vs missing; 1'-0" vs missing pin: plan 2 1/2" / model none (straight) | MISMATCH (model straight) |
| 4d6 | BACKWALL, HAIRPIN, VERTICAL | seg: 3'-5" + 3'-5" + 1'-2" (read on clip; sum = list length) ; D 2" | 8'-0" | U 3'-5" + 1'-2" + 3'-5" ; D 2" | 3'-5" vs 3'-5"; 3'-5" vs 3'-5"; 1'-2" vs 1'-2" | match |
| 5e1 | WINGWALL, VERTICAL | straight | VARIES | STR 11'-7" (min 7'-8 1/16") |  | ? |
| 5e2 | MASKWALL, VERTICAL | straight | 14'-7" | STR 14'-7" | +0.00" | match |
| 5e3 | MASKWALL, HORIZONTAL | straight | 5'-8" | STR 5'-8" | +0.00" | match |
| 6f1 | WINGWALL, HORIZONTAL | straight | 22'-2" | STR 22'-2" | +0.00" | match |
| 6f2 | WINGWALL, HORIZONTAL | straight | VARIES | STR 21'-1" |  | ? |
| 6f3 | WINGWALL, BOTTOM | straight | 23'-1" | STR 23'-1" | +0.00" | match |
| 6f4 | WINGWALL, DOWELS | straight | 5'-3" | STR 5'-3" | +0.00" | match |
| 6g1 | BACKWALL, LONGITUDINAL | straight | 31'-8" | STR 31'-8" | +0.00" | match |
| 5g2 | BACKWALL, DOWELS | seg: 5'-3" + 10" (read on clip; sum = list length) ; D 3 3/4" | 6'-1" | L 10" + 5'-3" ; D 3 3/4" | 5'-3" vs 5'-3"; 10" vs 10" | match |
| 5g3 | PAVING NOTCH, LONGITUDINAL | straight | 31'-8" | STR 31'-8" | +0.00" | match |
| 6g4 | WINGWALL, DOWELS | seg: 4'-5" + 1'-0" (read on clip; sum = list length) ; D 4 1/2" | 5'-5" | L 1'-0" + 4'-5" ; D 4 1/2" | 4'-5" vs 4'-5"; 1'-0" vs 1'-0" | match |
| 5h1 | COUNTERFORT, DOWELS | straight | 4'-0" | STR 4'-0" | +0.00" | match |
| 6h2 | COUNTERFORT 1, VERTICAL | seg: 10'-9" + 1'-0" (read on clip; sum = list length) ; D 4 1/2" | 11'-9" | L 10'-9" + 1'-0" ; D 4 1/2" | 10'-9" vs 10'-9"; 1'-0" vs 1'-0" | match |
| 6h3 | COUNTERFORT 2, VERTICAL | seg: 8'-0" + 1'-0" (read on clip; sum = list length) ; D 4 1/2" | 9'-0" | L 8'-0" + 1'-0" ; D 4 1/2" | 8'-0" vs 8'-0"; 1'-0" vs 1'-0" | match |
| 5h4 | COUNTERFORT, HORIZONTAL | seg: 4'-9" + 4'-9" + 1'-6" + 6" + 6" (read on clip; sum = list length) ; D 2 1/2" | 12'-0" | HAT 6" + 4'-9" + 1'-6" + 4'-9" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 4'-9" vs 4'-9"; 4'-9" vs 4'-9"; 1'-6" vs 1'-6"; 6" vs 6"; 6" vs 6" | match |
| 5j1 | WING FOOTING, LONGITUDINAL | seg: 6'-8" + 2'-5" + 2'-5" (read on clip; sum = list length) ; D 2 1/2" | 11'-6" | U 2'-5" + 6'-8" + 2'-5" ; D 2 1/2" | 6'-8" vs 6'-8"; 2'-5" vs 2'-5"; 2'-5" vs 2'-5" | match |
| 5j2 | WING FOOTING, TRANSVERSE | hoop: 2'-8" + 2'-7" + 6" (read on clip; sum = list length) ; D 2 1/2" | 11'-6" | HOOP 2'-8" + 2'-7" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 2'-8" vs 2'-8"; 2'-7" vs 2'-7"; 6" vs 6" | match |
| 8j3 | PILE UPLIFT ANCHORS | V: 2'-6" + 2'-6" (read on clip; sum = list length) ; D 6" | 5'-0" | V 2'-6" + 2'-6" ; D 6" | 2'-6" vs 2'-6"; 2'-6" vs 2'-6" | match |
| 5m1 | BEAM STEPS, TRANSVERSE | seg: 3'-11" + 2'-1" + 2'-1" (read on clip; sum = list length) ; D 2 1/2" | 8'-1" | U 2'-1" + 3'-11" + 2'-1" ; D 2 1/2" | 3'-11" vs 3'-11"; 2'-1" vs 2'-1"; 2'-1" vs 2'-1" | match |
| 5n1 | BEAM STEPS, LONGITUDINAL | straight | 4'-2" | STR 4'-2" | +0.00" | match |
| 5n2 | BEAM STEPS, HOOP | hoop: 4'-2" + 4'-0 1/2" + 6" (read on clip; sum = list length) ; D 2 1/2" | 17'-5" | HOOP 4'-2" + 4'-0 1/2" + 6" ; D 2 1/2" (as scheduled (A,B...)) | 4'-2" vs 4'-2"; 4'-0 1/2" vs 4'-0 1/2"; 6" vs 6" | match |
| 5d5 | PAVING NOTCH DOWELS | straight | 3'-6" | STR 3'-6" | +0.00" | match |

