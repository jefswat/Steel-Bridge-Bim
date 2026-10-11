# Unit 3 bearing offsets vs framing-plan dimensions (I-80 WB U3, 11WB-13WB)

Method: copy of `samples/plans/I-80 WB and Ramp G.sbm` in `temp/u3off`, `temp/cli-main/sbm.exe run`; PDF 115 (sheet 112, 11WB/12WB) and
PDF 117 (sheet 113A, 13WB/14WB), clips at 4-12x. Scripts: `temp/u3off/{dump,radial,rad2,stag,ch,gg_test,tail}.csx`.

## 1. What the plan dimensions mean
- Skews (plan, "measured with respect to local tangent at the intersection of pier CL and CL WB approach roadway"): 11WB 27 27'55", 12WB 23 40'36",
  13WB 18 48'56", 14WB 14 04'22", 15WB 9 19'56", East Abut 5 26'43". Model: 12WB-East Abut identical (23.67667, 18.81556, 14.07278, 9.33222,
  5.44528); 11WB model skew 27.4197 is the same pier line shifted parallel 1'-8 (support-lines-i80.csx), so it is not a mismatch.
- "21'-11 3/16" (12WB) and "16'-1 15/16, 12'-10 7/8, 7'-4 1/8" (13WB) = HORIZONTAL cross frame spacing along girder A (square symbol) from A's
  bearing. They define radial frame lines (station constant) crossing A at those distances.
- The half-filled-circle dims "5'-0" (12WB) and "8'-0" (13WB) are on girder E, measured ALONG GIRDER E from E's bearing to that radial line.
  The 12WB 5'-0 is to the A+21'-11 3/16 line; the 13WB 8'-0 is to the FIRST line, A+16'-1 15/16 (not the second, and not "E->G").
- The earlier comparison in cross-frame-gaps.md section 3 used the wrong reference (radial through the next bearing), which is why 5.074 / 7.841 looked off.

## 2. Model vs plan (model along E from the E bearing to the radial, ft; + = radial ahead)
| dim | plan | model |
|---|---|---|
| 12WB E to A+21.9323 line | 5.0000 | 5.0069 |
| 13WB E to A+16.1615 line | 8.0000 | 7.9950 |
Both agree to 0.08" / 0.06". D-J spans (all piers) are within 0.03" (check-spans). So the D-J (parallel) girders, skews and support lines are right.
Skewed-pier dims along the pier line (ticks perpendicular to the pier): 12WB A-B 8'-5 1/4, B-C 8'-7 7/8 = inputs, model 8.4375 / 8.6563 exact.
At 11WB the A/B/C offsets 73'-1 1/4, 59'-10 7/8, 47'-2 5/8 reproduce exactly (-73.104, -59.906, -47.219).

## 3. The one real residual: girder C at 13WB (PDF 117, dims along the pier line, 6'-3 7/16 A-C and 6'-9 1/16 C-D, confirmed on a 12x clip)
Model chords: A-C 6.3447 (plan 6.2865, +0.70"), C-D 6.7069 (plan 6.7552, -0.58"); A-D 13.0516 vs 13.0417 (+0.12"). So A is right to about 0.1" and C sits
about 0.053 ft (0.64") too far toward D. Matches check-spans C 13WB +0.12" / A 13WB -0.18".
Cause: the single arc for C (R 2795'-0 from 11WB through the 12WB point c12). Sensitivity (girder-geometry-i80.csx on a copy, only R3C changed):
R 2795 -> A-C 6.3447, C-D 6.7069, C 13WB span +0.12"; R 2785 -> 6.2960 / 6.7557, span -0.10"; R 2750 -> 6.1226 / 6.9290, -0.90".
R = 2785'-0 reproduces both plan dims to 0.01 ft, but the plan prints R 2795'-0 (PDF 115 text "2795'-0"), so I did NOT adopt it. UNRESOLVED: why C
deviates (possible: the C arc is not one arc, the 12WB C point from the A bearing chain, or the plan R is a rounded design value). PDF 115 / 117 / 119 (design sheet 115 radius table).
Before/after with R 2785 on the copy: check-spans C 13WB +0.12 -> -0.10 (nothing else changed; 15 spans still beyond 1/16"); check-top-of-bearing identical;
check-top-of-slab-all Unit 3 unchanged except G:C max 0.009 (was 0.008), 12WB-13WB max 0.009 (was 0.009), flagged unchanged.

## 4. UNRESOLVED
- 12WB "6'-11 3/8" (half-circle, at J): the model puts the radial 6.948 ft before J's bearing at A+34.814 ft; the plan gives no A-string dim beyond
  21'-11 3/16 at 12WB (PDF 115), so its line cannot be checked (A+21.9323+12.9115 = 34.84 would be 0.03 ft off, but 12.9115 is the 13WB spacing).
- 13WB A+16'-1 15/16+12'-10 7/8 (29.073) and +7'-4 1/8 (36.417) lines: no second plan dim on other girders to compare (PDF 117).
- 14WB: the plan gives only the skew and 2'-0 jacking dims (PDF 117); no girder-to-girder dims, so nothing to compare (A 14WB span -0.11").
- Conclusion for cross-frame-gaps.md: Unit 3 lines at 12WB/13WB can be placed by the same A-string rule as Unit 2 (distances along A from A's bearing).
No input changed in the project; no model file edited.
