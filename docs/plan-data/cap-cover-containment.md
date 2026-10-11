# Pier cap bars vs clear cover (containment check), 2026-10-11

Scope: every plan-listed substructure bar (caps, pedestals, footings, columns) checked by `SubstructureGenerator.CheckContainment`
(`src/SBM.Core/Substructure/SubstructureContainment.cs`): bar surface to the concrete face minus the plan clear cover, tolerance 1/8".
Per-bar records: `SubstructureUnit.CoverViolations` (mark, kind, excess, governing face, S/N/Z of the worst point); listing script
`samples/plans/check-sub-cover.csx` (`sbm run samples\plans\check-sub-cover.csx model.sbm [-- detail]`). Standing rule: bars are never moved
to satisfy cover; a violation means the placement rule or an input is wrong (fixed below) or the plan's own data does not fit (kept, with evidence).

## Counts (I-80 WB + Ramp G model, all substructure units)

| kind | main before (565) | after |
|---|---|---|
| 6b2 cantilever bottom | 136 | 0 |
| 5m1 pedestal bars | 180 | 56 (11WB) |
| 5n1 pedestal bars | 91 | 36 (11WB 12, 9G 24) |
| 5n2 pedestal bars / hoops | 26 | 12 (11WB 5n2 transverse U) |
| 5c5 hairpins | 32 | 0 |
| 5a13 end transverse | 17 | 0 |
| 5a15 end vertical (5WB) | 9 | 0 |
| 5c3 hairpins (6A) | 28 | 28 |
| 5c4 hairpins (19G) | 4 | 4 |
| 6a15 / 6a16 end verticals (6A) | 6 | 6 |
| 5a11 side faces (6A) | 2 | 2 |
| 5a12 side faces (Ramp G relief bars) | 34 | 34 |
| **total** | **565** | **178** |

(The handoff's 2,103 was the state before the bend-diagram and pedestal-size work; that work brought it to 565 on main.)

## Fixed

1. **6b2 cantilever bottom bars (136 -> 0), placement rule.** The bar (2'-0 flat + slant, plan bend diagram) is now placed as low as the cover allows at each set-back
   from the tip (searched in 1/4" steps, every point sampled along the segments); before, its elevation was fixed from the soffit 1 ft inside the tip and only the
   set-back was searched, so no position fitted.
2. **5c5 / 5c6 hairpin zone order (32 -> 0), input.** `tools/zones.py` missed "8-5c5`BARS" (backtick = the sheets' wide space), so the right-end zone was read first on
   2WB, 3WB, 7WB, 9WB, 10WB, 13WB; regex fixed, data fixed in `pier-rebar-i80.csx`.
3. **Pedestal U bars 5m1 / 5n1 in the pedestal (I-80 1WB-6WB, Ramp G 5G / 14G), placement rule.** Pier elevations (e.g. Design 116 PDF 19, Pier 1WB "6-5m1" detail; pedestals
   8 1/16" .. 11 7/8" high, "*ELEV. 1000.32" on the pedestal top) show the U bases in the TOP of the pedestal (5n1 bases as dots on top, the 5m1 base just under them) and the legs
   hanging through the pedestal into the cap. The model put the base under the cap top, a pedestal height too deep: the legs reached below the relief line into the 6" narrower lower
   section (2" / 3 3/16" "violations"). Now: base = pedestal top - 2" (5n1), one 5n1 bar lower (5m1), only with the plan pedestal size (`PedestalSizeAlong/Across`).
4. **Missing U shapes (input).** 6WB 5m1 (shared diagram "5m1, 5n1", Design 116 PDF 46: 5'-1 / 5'-3, legs 2'-9, D = 2 1/2) and Ramp G 5G / 14G 5m1 (Design 316 PDF 33 / 60:
   4'-8 / 5'-8, legs 2'-3) were not given a shape (the extractor missed the shared diagram); added to the csx lines (raster-confirmed).
5. **5n2 pedestal hoops (input + rule).** The notes "5n2 (AT GIRDERS C & D ONLY)" (1WB PDF 19, 2WB PDF 23, 3WB PDF 27), "B-D" (4WB PDF 31), "F-I" (6WB PDF 43) are now data
   (`PlanBarGroup.Girders`, csx `;G=`); the hoops (diagram 5'-2 x 5'-4 + 6" tails, also read for 4WB PDF 34 / 6WB PDF 46) go round those pedestal blocks at mid-height (elevation
   not dimensioned; the long side along the block's long side). Before they were at mid-cap 3" under the cap top. 5WB: the note "AT AHEAD BRG. GIRDERS G-I & AT BACK BRG. GIRDERS
   B-D ONLY" selects 4 shared blocks for 3 hoops -> UNRESOLVED warning, mid-cap. 9G: pedestals not set -> UNRESOLVED.
6. **5a13 end U bars (17 -> 0), placement rule.** The U base (5'-3 of a 6'-0 cap) spans the width, so the stack must lie in the full-width zone above the relief line (5WB elevation
   PDF 35 / 37 "3 EQ. SPA.; 4-5a13 BARS" above the arc); the search tested only the S-Z outline. Now it also requires the full width at the base.
7. **5a15 (Pier 5WB, 9 -> 0), shape.** Bend diagram (PDF 38, raster): 10" top leg + 4'-5 + 2'-0 slanted with 6" drop = 7'-3; elevation PDF 35 / 37: the bar runs down the
   vertical end face and along the soffit bevel, at the LEFT end only ("9-5a15"; "9-5a14" at the right end); plan PDF 36: 9 across at 9" from the faces. New shape `A15`
   (`PlanA15`), `E=-1` for 5a15 and `E=1` for 5a14 (`PlanBarGroup.End` now also selects the end of CapEnd groups). Before: a straight 7'-3 vertical.
8. **5a11 / 5a12 short end skin bars (I-80), input + rule.** The csx parsers skipped `E=` on lines without a bend shape, so these bars sat at mid-cap; once at the ends, the
   centre "top corner + cover + L/2" put the end through the raked end face. Now the end keeps the cover to the end face at the bar's own height.
9. **Check false positives.** (a) `LateralSetback` pooled the relief lines of both faces: identical back / ahead lines counted each layer twice (2/3 of the setback where 1/3
   applies), and on 6A (own back / ahead profiles) the higher face's lines set both faces. Lines now carry their face and layer (`SubstructureUnit.ReliefLineTags`), each face
   counts its own layers once. (b) The pedestal blocks are now part of the host concrete for pedestal bars (`PedestalBarSlack`: cover to the block sides / top; under a block the
   cap top is not a free face).

## Question (5): which bars get the 3 1/2" clear at the textured face

The plans print **3 1/2" CL.** from the cap face to the **5c hairpin legs** at **both** faces of every cap section (Design 116 PDF 20 sections A-A / B-B; Pier 6A PDF 40
sections A-A / B-B; Ramp G equivalents) - the printed hairpin base is the cap width less 2 x 3 1/2". It is not a textured-face-only value and it does not apply to the skin bars,
hoops or end bars (they lie inside the hairpin legs or keep the general note "MINIMUM CLEAR DISTANCE ... 2" UNLESS OTHERWISE NOTED", e.g. PDF 22). Implemented: the check
verifies 3 1/2" from the full-width faces for every cap hairpin (`HairpinFaceClear`; all pass). For production bars at the textured face the plans state nothing; the only
statement is the MOCKUP note (PDF 83): "A SINGLE MAT OF NO. 5 REINFORCING BARS ... SHALL BE SET 2 INCHES CLEAR TO THE BOTTOM OF THE TEXTURED FACE" (texture B up to 1 1/2"
deep). Raised as an UNRESOLVED warning on every pier with a relief step; the other bars stay checked against 2" to the form face.

## Remaining (178), kept as warnings

* **11WB pedestal bars 5m1 / 5n1 / 5n2 (80)** - the plan pedestals are not set (two staggered rows, BACK / AHEAD across sizes 3'-11 / 3'-9, along size not printed;
  `pier-pedestals.md`); the U bars are sized for the plan pedestals and sit in the bearing-footprint pedestals. 5m1 is a bent bar whose diagram is not decoded.
* **9G 5n1 (24)** - pedestals not set (separate back / ahead pedestals about 11" apart, 5n1 + 5n3 groups); 5n1 base 5'-7 vs the footprint pedestals.
* **6A 5c3 hairpins (28)** - the cap top of 6A has an upper (Ramp A2) and a lower (Unit 2) half 0.75 ft apart (`pier-6a.csx`); the hairpins hang from the structural (lower)
  top across the full width, while the back face's relief band is measured from the upper half's top, 9" higher, so the 2'-10 legs end up to 4 3/16" below the back face's relief
  line. Section A-A (PDF 40) appears to show the legs ending at the first step. Which top the 6A band depth refers to is not dimensioned (UNRESOLVED input).
* **6A 6a15 / 6a16 (6), 5a11 (2)** - the 6A variants of the end bars have no decoded bend diagram (straight bars of the listed length); 6A 5a11 is 52'-10 long in a 52'-0 cap.
* **Ramp G 5a12 (34)** - the long relief bars (37'-5 in a 35'-0 / 37'-11 3/8 cap) follow the curved relief in the elevations (e.g. Design 316 PDF 20, 1G "5a12 (E.F.)");
  that path is not dimensioned, the model keeps a straight bar at a fractional height, too long for the raked ends there by 0.3" .. 1.2".
* **19G 5c4 (4)** - legs longer than the full-width zone of the explicit 19G relief figure at S 3.6 .. 4.0 m (the figure's CL-pier mark conflicts with the structural sheet:
  18'-0 1/2 vs 22'-0 from the corners, `pier-cap-aesthetics.md`).

## Re-run on the main model

`samples/plans/pier-rebar-i80.csx` and `samples/plans/pier-rebar-ramp-g.csx` (new shapes / `G=` / `E=` data; the generator changes need nothing else), then
`samples/plans/check-sub-cover.csx` to list the result.
