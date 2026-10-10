# Piles (batter), cap section step, footing dowels / column splices - Ramp G (Design 316) and I-80 WB (Design 116)

STATUS (all three items now read for every pier in both sets; see the status table at the end):
- Item 1 BATTERED PILES: COMPLETE (layout pages read on raster).
- Item 2 CAP SECTION STEP: COMPLETE. One common detail; sections A-A / B-B raster-read for 1G-19G; I-80 sections raster-read for 1WB, 5WB, 6A, 11WB and
  text-layer-fingerprinted (all dimension strings and positions) for the rest.
- Item 3 DOWELS / COUPLERS: COMPLETE for geometry that is drawn or listed (bar lists of every pier, bent-bar shapes, printed MIN. LAP labels, elevations of
  every pier raster-read). Coupler elevations and the bottom-of-dowel position are DERIVED from bar lengths (they are not dimensioned) - see section 3.

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

Sources: sections A-A / B-B on each pier's first detail sheet (Ramp G, PDF = elevation page; 16G p65, 17G p69) or second sheet (I-80: p20, 24, 28, ... 80),
Detail A / Detail B on the aesthetics sheets (Ramp G PDF 78/79, I-80 PDF 83/84; raster p78 read this session). Section positions A/B are marked on the pier elevation.

### 2.1 The detail (identical for every pier; only the widths differ)
- The cap is a plain rectangle in section ABOVE the relief curve (top width W) and NARROWER BELOW it: lower width = W - 1'-0, i.e. a 6" setback on EACH of the two
  faces (symmetric about the cap CL; both sections show both faces stepped). The soffit is the lower width the whole way: bottom bars / pedestal seats sit in it.
- Where the step is: at the relief curve (the S-shaped curve drawn on the cap elevation = the boundary of the textured band). Its depth below the top is therefore
  NOT constant ("VAR." on the sections; band depths and arc radii are in `pier-cap-aesthetics.md`). It runs the full cap length, tip to tip: at the fan end three
  nested arcs start on the soffit line a little back from the beak tip (spacing L4, "2 @ 3/4 = 1 1/2" between the arcs) and merge into one curve; at the other end
  the single curve runs out on the soffit near the beak (raster 1G p19). Texture B is applied to ONE face only (aesthetics doc), but the setback is on BOTH faces.
- Form of the setback: vertical riser, tread DRAFTED 7 deg 0'0" MIN from horizontal (Detail B: "NO CHAMFER", note "APPLY 7 DEGREE (MIN.) DRAFT AT ALL SETBACKS").
  The tread slopes down toward the lower (inset) face (read on the small Detail B sketch: UNCERTAIN, only which end is high). Not square.
- The 6" per face is built as EITHER one 6" step (section away from the fan end, "6 (TYP.)") OR three steps "3 @ 2 = 0'-6 (TYP.)" (section cut through the fan end,
  where the three arcs are crossed). Total setback = 6" per face in both cases. Vertical spacing of the three steps is not dimensioned on the sections (equal spacing
  assumed from "2 @ 3/4 = 1 1/2" at the tip: UNCERTAIN).
- End conditions at the beaks: Detail A (tip, 75 deg raked end face): "1/2" SETBACK (TYP.)", 3" and 3" from the work point W.P., Texture B one side / none other side,
  "CORNERS SHALL NOT BE CHAMFERED". No chamfer anywhere on the cap (Detail B "NO CHAMFER"); the only bevel on the pier sheets is the 2x10 keyway.
- Pedestal blocks (and the 1" wash) are drawn above the cap-top line on the sections (9G, 6A, 11WB, 19G ...): they sit on the full-width part; not a wider cap.

### 2.2 Per pier
| Pier(s) | Top W | Lower W | Section with the 3 @ 2 stair | Section with single 6" step | Read |
|---|---|---|---|---|---|
| Ramp G 1G-15G, 18G, 19G (p19, 22, 25, ... 61, 72, 75) | 8'-0 | 7'-0 | A-A | B-B | raster, every pier |
| Ramp G 16G (p65), 17G (p69) | 6'-0 | 5'-0 | B-B | A-A | raster |
| I-80 1WB-4WB, 6WB-10WB, 12WB-15WB (p20, 24, 28, 32, 44, 48, 52, 56, 60, 68, 72, 76, 80) | 6'-0 | 5'-0 | B-B | A-A | 1WB raster; 2WB-4WB partial raster; rest by text positions identical (6'-0 / 5'-0 / 7'-0 / "0'-6" at the same coordinates) |
| I-80 5WB (p36), 11WB&19G (p64) | 8'-0 | 7'-0 | B-B | A-A | raster |
| I-80 6A (p40) | 8'-0 | 7'-0 | A-A | B-B | raster |

## 3. Footing dowels / column bar splices

### 3.1 Rules common to every pier (verified on the bar lists, bent bar shapes and elevations)
1. COLUMN BARS ("COLUMN, VERTICAL") are STRAIGHT (bar-list shape "-"). No hooks at the cap: they are simply embedded in the cap, ending BELOW the cap top.
   Bar bottom = top of footing (lap piers) or the coupler (coupler piers). Embedment above the column soffit (DERIVED = bar top - column height):
   - Ramp G 1G-15G, 18G, 19G: 7'-8 typical (1G 7'-10, 2G 7'-11, 15G/19G 7'-9, 18G 7'-7); the cap is 9'-0 deep at the pier CL, so bar tops are about 1'-4 below the cap top.
   - Ramp G 16G 6'-1, 17G 5'-11 (cap 7'-0 deep). I-80: 5'-10 to 6'-0 (7'-0 deep cap), 6A 6'-11. Per-pier values in 3.2 / 3.3.
   Column height used: Ramp G = soffit at CL - footing top (earlier docs / `piers-ramp-g.csx`); I-80 = the "TABLE OF COLUMN HEIGHTS" DIM. A on each pier sheet (footing top to soffit at that
   column CL; DIM. B = height above the plinth top, so plinth height = A - B) or the elevation dimension for 1WB-5WB and 6A.
2. DOWELS ("FOOTING TO COLUMN DOWEL") are L-bars, 90 deg hook at the BOTTOM, one per column bar. Out-to-out dimensions from the bent bar details:
   #11 dowel: straight leg + 2'-0 hook, D = 1'-0 | #14: + 2'-7, D = 1'-6 1/4 | #18: + 3'-5, D = 2'-0. (e.g. 11d2 14'-6 = 12'-6 + 2'-0; 14d3 21'-2 + 2'-7; 18d3 20'-8 + 3'-5.)
3. Embedment / bottom position: the hook leg lies on top of the BOTTOM footing mat (pile top is 1'-0 above the footing bottom, mat 1" above that). Position of the hook bottom above the
   footing bottom, DERIVED from the printed MIN. LAP labels and from round coupler elevations: 1'-4 on Ramp G, 1'-3 on I-80 (+-1"). So the dowel top above the footing top is
   (straight leg) - (footing thickness - 1'-4 [Ramp G] or 1'-3 [I-80]). This reproduces every printed lap exactly (e.g. 1G 12'-6 - 4'-2 = 8'-4; 1WB 16'-9 - 4'-3 = 12'-6).
4. Hook orientation: footing elevations (raster 9WB p55, 10WB p59) show the bars at the END faces of the column (+-S) with the hook leg in the plane of the drawing pointing OUTWARD
   (away from the column CL along the pier line). Bars on the long faces (+-N) are drawn end-on as dots, i.e. their hook runs along N; the sign is not shown (UNCERTAIN, outward assumed).
5. In the column section at the lap (Section D-D, and C-C above it) each dowel stands SIDE BY SIDE IN CONTACT with its column bar along the face (pairs of open/filled circles at the same
   perimeter positions; 46 + 46 bars on 1G D-D). Column bar positions (46 on 10'-0 x 6'-6: 7+7 long faces, 6+6 end faces, 5 x 4 chamfers) are those in the earlier docs.
6. LAP piers (#11): dowel projects above the footing top by the printed "n MIN. LAP (TYP.)": 8'-4 for non-coated bars, 12'-6 for EPOXY-COATED bars. All bars lap at the same
   level (no stagger) except 9G. Splice = contact lap, no couplers.
7. COUPLER piers (#14, #18; plan note: "THE VERTICAL COLUMN BARS SHALL BE SPLICED AT THE LOCATIONS SHOWN USING MECHANICAL SPLICE ASSEMBLIES ... consist of mechanical splicers and reinforcing
   splice bars as required"): two splice levels 5'-0 apart. Alternate column bars (d1 / d2) are cut to different lengths: the SHORT column bar d1 (and its LONG dowel d3) splice at the HIGH level,
   the LONG column bar d2 (and its SHORT dowel d4) at the LOW level, so the bar tops end at the same elevation (d2 = d1 + 5'-0, d3 = d4 + 5'-0). Coupler elevation = dowel top = column bar bottom.
   Splice (coupler) length, splice bars and coupler type (same coating as the bars, I.M. 451 App. E) are NOT shown (UNCERTAIN). Coupler counts (plan notes) are in 3.2 / 3.3.
   Mechanical splice piers (p3 notes): Ramp G 2G, 7G, 8G, 10G, 11G, 12G, 16G, 17G, 18G; I-80 3WB, 7WB, 13WB.
8. Bars that END AT THE TOP OF THE COLUMN ("BAR TERMINATES AT TOP OF COLUMN TO AVOID INTERFERENCE WITH CAP REINFORCING"): 4 bars per column on 2G (18d7), 7G (18d7), 11G (18d7), 12G (18d7),
   18G (18d5) and 12 bars (4 per column) on 7WB (18d9). They are the 4 bars at the middle of each octagon chamfer (the circled bars where the 5e2 crossties hook; marked with a half-filled symbol in
   section C-C) and they splice at the LOW coupler (they belong to the d4-dowel group: d4 count = d2 + d7). Top elevation, DERIVED: 2G 3" below the column top, 7G/11G/12G 6" below, 18G 7" below;
   7WB all 12 bars have the same length 25'-8 (top at 5'-0 + 25'-8 = 30'-8 above the footing top) so they stop 4", 1'-2 5/8" and 2'-1 1/4" below the three column tops (UNCERTAIN).
9. Plinth dowels (piers with an architectural plinth, Texture A): "FOOTING TO PLINTH DOWEL" 7d6 / 7d13 are STRAIGHT (no hook), embed in the footing 2'-0 (2'-3 / 2'-10 on the epoxy piers), lap with the
   straight plinth bars (7d5 / 7d12) 3'-3 (3'-10 / 4'-10 on the epoxy piers). Plinth bars/hairpins/hoops are not column steel and are not part of this item.

### 3.2 Ramp G (Design 316). T = footing thickness; plinth h = plinth top above footing top (from the plan flare elevation); H = footing top to soffit at the column CL.
| Pier | Bars | Column bars (marks, no. x length) | Dowels (no. x length = straight + hook) | Splice | Dowel top above footing top | T / plinth h / H | Bar top above soffit | Plinth dowels |
|---|---|---|---|---|---|---|---|---|
| 1G | #11, 1 col x 46 | d1 46 x 32'-10 | d2 46 x 14'-6 (12'-6 + 2'-0) | lap 8'-4 | 8'-4 | 5'-6 / - / 25'-0 | 7'-10 | none |
| 2G | #18, 1 x 46 | d1 23 x 24'-11, d2 19 x 29'-11, d7 4 x 21'-9 (ends at col. top) | d3 23 x 24'-1 (20'-8 + 3'-5), d4 23 x 19'-1 (15'-8 + 3'-5) | couplers, 46 non-coated | d3 15'-0, d4 10'-0 | 7'-0 / 12'-0 / 32'-0 | 7'-11 | 7d6 32 x 5'-3 (3'-3 lap / 2'-0 emb) |
| 3G | #11, 1 x 46 | d1 46 x 46'-8 | d2 46 x 15'-0 (13'-0 + 2'-0) | lap 8'-4 | 8'-4 | 6'-0 / 14'-0 / 39'-0 | 7'-8 | 7d6 32 x 5'-3 (3'-3 / 2'-0) |
| 4G | #11, 1 x 46 | d1 46 x 53'-8 | d2 46 x 15'-0 | lap 8'-4 | 8'-4 | 6'-0 / 17'-0 / 46'-0 | 7'-8 | 7d6 32 x 5'-3 |
| 5G | #11, 1 x 46 | d1 46 x 52'-8 | d2 46 x 20'-2 (18'-2 + 2'-0) | lap 12'-6 (epoxy) | 12'-6 | 7'-0 / 16'-0 / 45'-0 | 7'-8 | 7d6 32 x 6'-1 (3'-10 / 2'-3) |
| 6G | #11, 1 x 46 | d1 46 x 57'-8 | d2 46 x 15'-0 | lap 8'-4 | 8'-4 | 6'-0 / 18'-0 / 50'-0 | 7'-8 | 7d6 32 x 5'-3 |
| 7G | #18, 1 x 46 | d1 23 x 37'-8, d2 19 x 42'-8, d7 4 x 34'-6 | d3 23 x 25'-1 (21'-8 + 3'-5), d4 23 x 20'-1 (16'-8 + 3'-5) | couplers, 46 non-coated | 15'-0 / 10'-0 | 8'-0 / 16'-0 / 45'-0 | 7'-8 | 7d6 32 x 5'-3 |
| 8G | #14, 1 x 46 | d1 23 x 52'-8, d2 23 x 57'-8 | d3 23 x 23'-9 (21'-2 + 2'-7), d4 23 x 18'-9 (16'-2 + 2'-7) | couplers, 46 non-coated | 15'-0 / 10'-0 | 7'-6 / 21'-0 / 60'-0 | 7'-8 | 7d6 32 x 5'-3 |
| 9G | #11, 1 x 46 | d1 92 x 39'-7 = 46 lower + 46 upper bars, TWO-STAGE: lower from the footing top, upper lapped 12'-6 at mid-height | d2 46 x 19'-2 (17'-2 + 2'-0) | lap 12'-6 at the footing AND 12'-6 at mid-height (second "12'-6 MIN. LAP (TYP.)" printed above the plinth) | 12'-6; mid lap zone 27'-1 to 39'-7 above footing top (DERIVED: both d1 identical, top = 2 x 39'-7 - 12'-6) | 6'-0 / 21'-0 / 59'-0 | 7'-8 | 7d6 32 x 6'-1 (3'-10 / 2'-3) |
| 10G | #14, 1 x 46 | d1 23 x 50'-8, d2 23 x 55'-8 | d3 23 x 23'-9, d4 23 x 18'-9 | couplers, 46 EPOXY | 15'-0 / 10'-0 | 7'-6 / 20'-0 / 58'-0 | 7'-8 | 7d6 32 x 6'-1 (3'-10 / 2'-3) |
| 11G | #18, 1 x 46 | d1 23 x 47'-8, d2 19 x 52'-8, d7 4 x 44'-6 | d3 23 x 24'-7 (21'-2 + 3'-5), d4 23 x 19'-7 (16'-2 + 3'-5) | couplers, 46 EPOXY | 15'-0 / 10'-0 | 7'-6 / 20'-0 / 55'-0 | 7'-8 | 7d6 38 x 6'-1 (3'-10 / 2'-3) |
| 12G | #18, 1 x 46 | d1 23 x 50'-8, d2 19 x 55'-8, d7 4 x 47'-6 | d3 23 x 24'-7, d4 23 x 19'-7 | couplers, 46 non-coated | 15'-0 / 10'-0 | 7'-6 / 23'-0 / 58'-0 | 7'-8 | 7d6 38 x 5'-3 (3'-3 / 2'-0) |
| 13G | #11, 1 x 46 | d1 46 x 55'-8 | d2 46 x 15'-0 | lap 8'-4 | 8'-4 | 6'-0 / 17'-0 / 48'-0 | 7'-8 | 7d6 32 x 5'-3 |
| 14G | #11, 1 x 46 | d1 46 x 52'-8 | d2 46 x 19'-2 | lap 12'-6 (epoxy) | 12'-6 | 6'-0 / 16'-0 / 45'-0 | 7'-8 | 7d6 32 x 6'-1 (3'-10 / 2'-3) |
| 15G | #11, 1 x 46 | d1 46 x 45'-9 | d2 46 x 15'-0 | lap 8'-4 | 8'-4 | 6'-0 / 14'-0 / 38'-0 | 7'-9 | 7d6 32 x 5'-3 |
| 16G | #14, 2 cols x 28 | per col 14 d1 + 14 d2: d1 28 x 25'-0, d2 28 x 30'-0 | d3 28 x 16'-10 (14'-3 + 2'-7), d4 28 x 11'-10 (9'-3 + 2'-7) | couplers, 56 non-coated (28/col) | d3 10'-1, d4 5'-1 (drawn to scale on p64) | 5'-6 / - / 29'-0 | 6'-1 | none |
| 17G | #14, 2 cols x 28 | col 1: d1 14 x 19'-10, d2 14 x 24'-10; col 2: d5 14 x 20'-7, d6 14 x 25'-7 | d3 28 x 16'-10, d4 28 x 11'-10 (14 + 14 per column) | couplers, 56 non-coated | 10'-1 / 5'-1 | 5'-6 / - / col 1 24'-0, col 2 24'-8 7/16 | 5'-11 (col 2 5'-11 5/8) | none |
| 18G | #18, 1 x 46 | d1 23 x 14'-8, d2 19 x 19'-8, d5 4 x 11'-6 (ends at col. top) | d3 23 x 24'-0 (20'-7 + 3'-5), d4 23 x 19'-0 (15'-7 + 3'-5) | couplers, 46 non-coated | d3 14'-11, d4 9'-11 | 7'-0 / - / 22'-0 | 7'-7 | none |
| 19G | #11, 1 x 46 | d1 46 x 31'-9 | d2 46 x 18'-2 (16'-2 + 2'-0) | lap 12'-6 (epoxy) | 12'-6 | 5'-0 / - / 24'-0 | 7'-9 | none |
Notes: On 2G-15G the printed elevation is broken (zig-zag) so the drawn coupler positions are NOT to scale; positions above are from bar lengths. On 16G the elevation is to scale and agrees
(couplers about 10 ft and 5 ft above the footing top). Plinth heights / footing T from `samples/plans/piers-ramp-g.csx`. Straight/lap pier laps are the printed labels.

### 3.3 I-80 WB (Design 116). Columns are 8'-0 x 4'-6 octagons (32 bars each, #11) except 6A (10'-0 x 6'-6, 46 bars) and the #18 piers; one dowel per column bar.
| Pier | Bars | Column bars (no. x length) | Dowels (no. x length = straight + hook) | Splice | Dowel top above footing top | T / plinth h | Column height (DIM. A) / bar top above soffit | Plinth dowels |
|---|---|---|---|---|---|---|---|---|
| 1WB | #11, 3 x 32 | d1 96 x 24'-0 | d2 96 x 18'-9 (16'-9 + 2'-0) | lap 12'-6 (epoxy) | 12'-6 | 5'-6 / - | 18'-0 / 6'-0 | none |
| 2WB | #11, 3 x 32 | d1 96 x 28'-0 | d2 96 x 14'-7 (12'-7 + 2'-0) | lap 8'-4 | 8'-4 | 5'-6 / - | 22'-0 / 6'-0 | none |
| 3WB | #18, 3 x 32 | d1 48 x 21'-0, d2 48 x 26'-0 | d3 48 x 17'-1 (13'-8 + 3'-5), d4 48 x 12'-1 (8'-8 + 3'-5) | couplers, 96 non-coated | d3 9'-11, d4 4'-11 (+-1") | 5'-0 / - | 25'-0 / 5'-11 | none |
| 4WB | #11, 2 x 32 | d1 64 x 35'-0 | d2 64 x 15'-1 (13'-1 + 2'-0) | lap 8'-4 | 8'-4 | 6'-0 / 12'-0 | 29'-0 / 6'-0 | 7d13 52 x 5'-3 (3'-3 / 2'-0) |
| 5WB | #11, 2 x 32 | d1 64 x 38'-0 | d2 64 x 18'-9 | lap 12'-6 (epoxy) | 12'-6 | 5'-6 / 13'-0 | 32'-0 / 6'-0 | 7d13 52 x 7'-8 (4'-10 / 2'-10) |
| 6A | #11, 1 x 46 | d1 46 x 35'-11 | d2 46 x 18'-9 | lap 12'-6 (epoxy) | 12'-6 | 5'-6 / - | 29'-0 / 6'-11 | none |
| 6WB | #11, 4 x 32 | col 1 d1 32 x 45'-0; col 2 d3 32 x 45'-5; col 3 d4 32 x 45'-11; col 4 d5 32 x 46'-5 | d2 128 x 15'-1 (13'-1 + 2'-0) | lap 8'-4 | 8'-4 | 6'-0 / 19'-0 | A: 39'-0, 39'-5 5/8, 39'-11 3/8, 40'-5 1/16 / 6'-0 to 5'-11 15/16 | 7d13 104 x 5'-3 (3'-3 / 2'-0) |
| 7WB | #18, 3 x 34 | col 1: d1 20 x 26'-11, d2 10 x 31'-11; col 2: d5 20 x 27'-9, d6 10 x 32'-9; col 3: d7 20 x 28'-8, d8 10 x 33'-8; d9 12 x 25'-8 (4 per col, ends at col. top) | d3 60 x 19'-2 (15'-9 + 3'-5), d4 42 x 14'-2 (10'-9 + 3'-5) | couplers, 102 non-coated (34/col) | d3 10'-0, d4 5'-0 | 7'-0 / 12'-0 | A: 31'-0, 31'-10 5/8, 32'-9 1/4 / 5'-11, 5'-10 3/8, 5'-10 3/4 | 7d13 78 x 5'-3 |
| 8WB | #11, 4 x 32 | d1 30'-11 (col 1), d3 31'-10, d4 32'-9, d5 33'-7 (cols 2-4), 32 each | d2 128 x 15'-1 | lap 8'-4 | 8'-4 | 6'-0 / 12'-0 | A: 25'-0, 25'-10 13/16, 26'-9 1/2, 27'-8 1/16 / 5'-11, 5'-11 3/16, 5'-11 1/2, 5'-10 15/16 | 7d13 104 x 5'-3 |
| 9WB | #11, 3 x 32 | d1 29'-11, d3 31'-5, d4 33'-0 | d2 96 x 14'-1 (12'-1 + 2'-0) | lap 8'-4 | 8'-4 | 5'-0 / 11'-0 | A: 24'-0, 25'-6 5/8, 27'-1 5/16 / 5'-11, 5'-10 3/8, 5'-10 11/16 | 7d13 78 x 5'-3 |
| 10WB | #11, 3 x 32 | d1 31'-11, d3 33'-4, d4 34'-10 | d2 96 x 14'-1 | lap 8'-4 | 8'-4 | 5'-0 / 12'-0 | A: 26'-0, 27'-6, 28'-11 7/8 / 5'-11, 5'-10, 5'-10 1/8 | 7d13 78 x 5'-3 |
| 11WB (& 19G line) | #11, 3 x 32 | d1 32'-10, d3 34'-10, d4 35'-11 | d2 96 x 19'-9 (17'-9 + 2'-0) | lap 12'-6 (epoxy) | 12'-6 | 6'-6 / 13'-0 | A: 27'-0, 29'-0 1/8, 30'-1 / 5'-10, 5'-9 7/8, 5'-10 | 7d13 78 x 7'-8 (4'-10 / 2'-10) |
| 12WB | #11, 4 x 32 | d1 32'-10, d3 34'-4, d4 35'-10, d5 37'-4 | d2 128 x 14'-7 (12'-7 + 2'-0) | lap 8'-4 | 8'-4 | 5'-6 / 14'-0 | A: 27'-0, 28'-6, 30'-0, 31'-6 / 5'-10 each | 7d13 104 x 5'-3 |
| 13WB | #18, 3 x 32 | col 1: d1 16 x 22'-10, d2 16 x 27'-10; col 2: d5 16 x 24'-8, d6 16 x 29'-8; col 3: d7 16 x 26'-6, d8 16 x 31'-6 | d3 48 x 18'-1 (14'-8 + 3'-5), d4 48 x 13'-1 (9'-8 + 3'-5) | couplers, 96 non-coated | d3 9'-11, d4 4'-11 (+-1") | 6'-0 / 12'-0 | A: 27'-0, 28'-10, 30'-8 1/16 / 5'-9, 5'-9, 5'-8 15/16 | 7d13 78 x 5'-3 |
| 14WB | #11, 3 x 32 | d1 31'-10, d3 33'-6, d4 35'-2 | d2 96 x 14'-1 | lap 8'-4 | 8'-4 | 5'-0 / 12'-0 | A: 26'-0, 27'-7 11/16, 29'-3 1/2 / 5'-10, 5'-10 5/16, 5'-10 1/2 | 7d13 78 x 5'-3 |
| 15WB | #11, 3 x 32 | d1 24'-10, d3 26'-6, d4 28'-2 | d2 96 x 14'-1 | lap 8'-4 | 8'-4 | 5'-0 / - | A: 19'-0, 20'-7 9/16, 22'-3 1/4 / 5'-10, 5'-10 7/16, 5'-10 3/4 | none |
Notes: dowel hook: #11 2'-0, #18 3'-5 (D = 2'-0). 1WB-5WB and 6A column heights are the printed elevation dimensions. On 7WB the 4 d9 bars per column (chamfer middles) share the d4 coupler level.
Column "d1" etc. are named by column number on the multi-column piers (6WB-15WB: d1 = column 1, d3 = column 2, d4 = column 3, d5 = column 4; 7WB/13WB: d1,d2 col 1, d5,d6 col 2, d7,d8 col 3).

## Inputs the modeler needs
1. Piles (FootingDefinition): per-pile batter list `{S, N, DirS, DirN}` (unit vector of the outward tip movement) plus `BatterRatio` (4 normally, 6 for 11G); a generator rule "perimeter piles outward,
   corner piles 45 deg diagonal (flag)". Pile top stays at the footing-bottom plane position; tip offset = depth / ratio. Confirm whether contract length is along the axis.
2. Cap section step (optional on the as-designed cap): `CapLowerWidth` (= CapWidth - 1'-0 for every pier: 8'-0 -> 7'-0, 6'-0 -> 5'-0), setback 6" per face on BOTH faces,
   the step boundary = the relief curve of `pier-cap-aesthetics.md`, full tip to tip (depth from the top is the band depth, about 3'-6 perpendicular at the reverse-curvature points),
   `StepTreadDraft` = 7 deg min, `StepCount` 1 (single 6") or 3 x 2" (through the fan end). A model may use the single 6" step everywhere and add the 3 x 2" stair only in the fan zone.
   No chamfers; beak tip 1/2" setback (Detail A).
3. Column splices (new data on `SubstructureDefinition`; today `column-rebar.csx` runs the column bars continuous from the footing):
   - `DowelSize` (= column bar size), one dowel per column bar, `DowelHookLength` (#11 2'-0, #14 2'-7, #18 3'-5; pin D 1'-0 / 1'-6 1/4 / 2'-0), `DowelHookDirection` (outward from the column CL at the
     end faces; long-face sign UNCERTAIN), `DowelBottomAboveFooting` (1'-4 Ramp G / 1'-3 I-80, i.e. hook on the bottom mat), dowel projection above the footing (table above).
   - `SpliceType` Lap (`LapLength` 8'-4, or 12'-6 if epoxy-coated; column bars start at the footing top) or Coupler (`CouplerElevations` = (high, low) above the footing top, 5'-0 apart;
     high-level pair = short column bar d1 + long dowel d3, low-level pair = long column bar d2 + short dowel d4 [+ the terminating bars d7 / d5 / d9]); coupler length not given (UNCERTAIN).
   - Column bar top: `ColumnBarEmbedInCap` above the soffit (per pier above); d1/d2 tops at the same elevation; straight, no hooks.
   - Per-bar flag `TerminatesAtColumnTop` for the 4 chamfer-middle bars of 2G/7G/11G/12G/18G and the 12 bars of 7WB (a few inches below the column top).
   - 9G only: second splice at mid-height (lap 12'-6, zone 27'-1 to 39'-7 above the footing top; the 46 upper bars start at the lap bottom).
   - Multi-column piers: bar lengths differ column to column (sloped soffit): use the per-column lengths above.
   - Plinth dowels (only where a plinth exists): straight 7d6 / 7d13, embed 2'-0 (2'-3 / 2'-10 on epoxy piers), lap 3'-3 (3'-10 / 4'-10).

## UNCERTAIN
- Ratio 1:4 as H:V; diagonal batter along resultant vs per axis; pile length along battered axis.
- Mid-row S positions of 12WB/4WB; 19G plumb piles read at low resolution.
- Cap step: which end of the 7 deg draft tread is high (small sketch); vertical spacing of the three 2" risers (equal spacing assumed); right-end termination of the relief curve at the beak read from the 1G
  raster only; I-80 2WB-4WB, 6WB-10WB, 12WB-15WB section details confirmed from text positions plus partial raster (not every section drawn at full resolution).
- Dowels: hook direction sign on the long faces; "bottom of dowel on the mat" and the coupler elevations are DERIVED from bar lengths (+-1", Ramp G 1'-4 / I-80 1'-3 rule); coupler length, splice bars and coupler
  make not shown; 7WB d9 tops (same length in all three columns, 4" to 2'-1" below the column tops); position of the 4 terminating bars (middle bar of each chamfer) read from a small section raster.
- Column bar embedment into the cap is DERIVED (bar length - column height), not dimensioned; the column bars are assumed to start exactly at the footing top on lap piers (consistent to the inch on all piers).

## Status (complete = read and recorded; partial = read with a derived or uncertain element)
| Pier | Item 1 piles | Item 2 cap step | Item 3 dowels / splices |
|---|---|---|---|
| Ramp G 1G, 3G, 4G, 5G, 6G, 9G, 13G, 14G, 15G, 19G | complete | complete | complete (lap printed; long-face hook side partial) |
| Ramp G 2G, 7G, 8G, 10G, 11G, 12G | complete | complete | partial (couplers: elevations derived, coupler length not given) |
| Ramp G 16G, 17G, 18G | complete | complete | partial (couplers derived) |
| I-80 1WB, 2WB, 4WB, 5WB, 6A, 6WB, 8WB, 9WB, 10WB, 11WB, 12WB, 14WB, 15WB | complete | complete (1WB, 5WB, 6A, 11WB raster; others text positions + partial raster) | complete (lap printed; long-face hook side partial) |
| I-80 3WB, 7WB, 13WB | complete | complete (text + partial raster) | partial (couplers derived; 7WB d9 tops uncertain) |
| 11WB & 19G shared line (11WB I-80 p63-66, 19G Ramp G p75-77) | complete | complete | complete |
