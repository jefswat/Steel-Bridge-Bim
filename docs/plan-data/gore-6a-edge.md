# I-80 WB Unit 2, girder A at Pier 6A vs the 1'-7 level edge strip (resolved 2026-10-10)

Conflict: with `DeckDefinition.EdgeStrip` the model's north edge at the 6A line was 118.65' LT of CL I-80 (-77.57 from the WB alignment), only 0.99 ft from girder A (117.66' LT,
-76.58), i.e. girder A inside the strip. Result: top of slab +0.027 / +0.017 ft at A points 1 / 2, top of bearing A at 6A +0.3 in, 17 blocking values > 0.02 ft.

## Finding: hypothesis (b) - the model's north edge before 7591+50.68 is wrong; the strip rule itself applies
1. Model: `deck-edges-i80.csx` starts `Unit 2.LeftEdgeTable` at 7591+50.68, 118.65' LT; `DeckDefinition.Interp` holds that offset constant back to the 6A line.
2. Plan: slab plan sheet 179 (PDF 186) - north edge from the 6A line to the "BEGIN LINEAR SLAB TAPER STA. 7591+50.68, 118.65' LT" is the line 13'-7 from CL RAMP A2
   (R = 2986'-5, practically straight over 42 ft). Ramp A2 CL (sheet 190, PDF 197; already used in `cross-slope-i80.csx`): 107.43' LT at I-80 7591+08.83, 105.08' LT at 7591+50.00
   (slope -0.0571 LT/ft). Edge = CL + 13.583 ft normal to Ramp A2 = +0.775 ft in station, +13.56 ft LT. Check: reproduces 7591+50.78, 118.64' LT vs plan 7591+50.68, 118.65' LT.
   The edge therefore runs from about 7591+09.6, 121.0' LT (6A corner) to 118.65' LT; it is NOT parallel to the CL, the model had it 2.35 ft too close at 6A.
   Girder A (117.66' LT at 6A, unchanged) to edge = 3.34 ft; gutter line is 1'-7 inside the edge, 1.76 ft outboard of A: A is NOT in the strip.
3. Hypothesis (a) is rejected: the NORTH rail (sheet 218, PDF 225: 1184'-7 end to end, Pier 6A to Pier 11WB) runs along this edge from the 6A end of slab. The crash cushion end post
   (sheet 222, PDF 229, detail A on PDF 197) sits at 65-69' LT on the gutter line of the Ramp A bridge end, not on the north edge.
4. Independent plan evidence that the strip applies at 6A: sheet 195 (PDF 202) prints EDGE_L = GUT_L at every Unit 2 point including point 1 (1021.47 = 1021.47; 149 points,
   differences 0.00-0.02), i.e. the edge is level with the gutter, which is the typical-section "LEVEL" note (PDF 94-96). Plan A - edge = 0.05 ft at point 1; model now
   0.062 (gutter 1.76 ft x 3.4%), before 0.034 only by accident of the wrong edge.

## Test on a copy (temp\gore6a\model.sbm, script temp\gore6a\edge6a.csx; nothing under samples\plans changed)
Edge point inserted first in `LeftEdgeTable`: station 7591+09.60, 121.00' LT of CL I-80 (-79.92 from the WB alignment).

| check | before | after |
|---|---|---|
| top of slab, A pt 1 / pt 2 | +0.027 / +0.017 | within 0.010 (worst A now +0.013 @pt 4; Unit 2 |d|max 0.027 -> 0.023, flagged 139 -> 137 overall) |
| top of bearing, U2 girder A at 6A | +0.3 in (largest difference 0.31) | +0.1 in (largest 0.13) |
| blocking: values > 0.02 / > 0.01 ft | 17 / 27 | 0 / 19 |
| U2 girder A blocking |d|max (n = 18) | 0.036 | 0.015 |
| haunch > 0.02 ft | 1 | 0 |

## Proposed input (needs no new option)
In `samples/plans/deck-edges-i80.csx` add as the FIRST vertex of the Unit 2 edge: `(Wb("7591+09.60"), 121.00, 0.0)`, before `7591+50.68, 118.65`, with the derivation above as comment.
Then re-run `deck-edge-strip.csx`-regenerated model. Not printed on the plans: the corner station/offset (derived from the 13'-7 dimension and the Ramp A2 CL; accurate to about 0.1 ft).
Remaining after the fix: U2 girder A 6A top of bearing +0.1 in and small 0.01-0.015 ft blocking residuals, the known ~ -0.008 ft bias at 7WB-9WB.
