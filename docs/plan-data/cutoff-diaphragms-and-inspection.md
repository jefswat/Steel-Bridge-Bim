# Girder cut-off cross girders and inspection access (I-80 WB Design 116, Ramp G Design 316)

Source PDFs in `dist\SteelBridgeModeler\`. "PDF n" = PDF page; "(sheet n)" = design sheet number. Fractions were read
from 200-500 dpi grayscale rasters (not the text layer). Rasters/scripts: `temp\cutoff\`. Values are as printed (inches
unless ft-in). UNCERTAIN = not fully resolved.

## PART 1 - Girder cut-off cross girders

### 1.1 Where they occur
Only I-80 WB. Ramp G (Design 316): no cross girders, discontinuous girders, or terminations (all 4 girders A-D run each
unit; text search of design-316 finds none). Ramp G access plans (PDF 132-133) show no cross girders.

I-80 locations (cross girder = "XG"; detail sheet 121 = PDF 125; typical for all):

| Unit | Discontinuous girder | Ends (CL XG) | Girders it frames between (orientation plan, PDF 125) | Source |
|---|---|---|---|---|
| 2 | E | 86'-8 1/16 past Pier 6WB | D and F | model script (build-i80-wb.csx); NOT re-read here (UNCERTAIN) |
| 2 | D | 64'-3 1/4 past Pier 7WB | C and F | girder data PDF 110 (sheet 107A), span 8WB row D = 64'-3 1/4 |
| 2 | C | 59'-1 1/16 past Pier 8WB | B and F | PDF 110, span 9WB row C = 59'-1 1/16 |
| 2 | B | 50'-3 1/2 past Pier 9WB | A and F | PDF 111 (sheet 108) B row (read at low res; UNCERTAIN to the 1/16) |
| 3 | B | 60'-3 11/16 past Pier 12WB | A and C | sheet 115 per docs/HANDOFF 2.3 (not re-read) |
| 3 | C | 53'-2 11/16 past Pier 13WB | A and D | same |

Plans note on each layout sheet: "FOR CROSS GIRDER DETAILS, SEE DESIGN SHEET 121" (PDF 110, 111, 112-114, 118-119).
Each XG is a single built-up plate girder spanning the continuing girders either side (Unit 2: neighbor on the left,
always F on the right; E's XG spans two girders away from D to F). The discontinuous girder is on its centre ("EQ. EQ."
to the hole CL). Distances from the exterior girder to the discontinuous girder CL, and to the interior girder, are
printed "VARIES" - no numbers on the sheet; derive from the layout.

Orientation (angle of each girder to CL XG, PDF 125; delta-marked = measured to the local tangent):
- Unit 2, XG for E: D 88-32-47, E 89-16-23, F 90-00-00. XG for D: C 87-55-00, D 88-32-47, F 90-00-00.
  XG for C: B 87-15-39, C 87-40-04, F 90-00-00 (delta). XG for B: A 89-14-44, B 90-00-00, F 90-00-00 (delta).
- Unit 3, XG for B: A 85-54-41, B 87-06-45, C 88-49-27 (delta). XG for C: A 87-20-39, C 88-00-13, D 90-00-00 (delta).
- Note on sheet: XGs and connection angles meet Charpy V-notch requirements (Art. 4152.02).

### 1.2 Cross girder section and details (PDF 125, sheet 121)

| Item | Unit 2 | Unit 3 |
|---|---|---|
| XG web | PL 9/16 x 80 | PL 3/4 x 60 |
| XG top and bottom flanges | PL 5/8 x 15 | PL 7/8 x 12 |
| Web-to-flange weld | 5/16 fillet both sides (callout) | same |
| Level | both flanges "LEVEL" | same |
| Opening in XG web at the discontinuous girder side | 3'-0 tall x 2'-0 wide, R = 6", CL hole on the centre between the discontinuous girder and each continuing girder | same |
| Edge plates of opening ("far side") | horizontal PL 5/8 x 6 above and below, 1/2" clear of opening (dashed) | same (PL 5/8 x 6 F.S.) |
| Dimension top of XG to first opening edge | 1'-10 | 1'-0 |
| XG stiffeners (vertical, near side) | PL 5/8 x 7 1/2 (N.S.) typ; vertical plates flanking opening about 6" from the opening sides | PL 1/2 x 6 (N.S.) typ |
| Stiffener at exterior girder edge | PL 1/2 x 6 1/2 | PL 1/2 x 6 1/2 |
| Stiffener welds | 1/4 fillet both sides (Sections C-C, D-D) | same |
| Stiffener may be moved up to 1 1/2" horizontally for walkway clearance (note) | yes | yes |
| Hole for wire rope | 7/8" dia in stiffener at the exterior girder, grind edge to 3/16" radius | same |

Connection angles (all at four locations per XG: both sides of each continuing girder web, plus the discontinuous girder
end), L5x5x1/2:
- Length: Unit 2, 10 spaces @ 6 = 5'-0, 2" end distances (angle 5'-4" long); Unit 3, 8 spaces @ 6 = 4'-0, 2" ends (4'-4").
- Pair of angles each side of the discontinuous girder web, one pair on the near and far face of the XG web at each
  continuing girder web (Sections B-B, F-F): angle bolted to the girder web and to the XG web with a single line of
  bolts at 6" pitch; edge dimensions on Sections B-B/F-F read as 3, 2, 3 (gage line 3" from the heel, 2" from the toe;
  the exact roles of the 2 and 3 are UNCERTAIN).
- Bolt diameter is NOT printed on sheet 121. General notes (PDF 6) say weathering-steel bolts A325 Type III. UNCERTAIN: 7/8"
  or 1" (I-80 cross frames use 1").
- Discontinuous girder web ends at the XG with a 3" gap marking ("3" typ.) between the girder end and the XG web, plus the
  two angle strips; its flanges stop short. No end plate, bearing stiffener or cope is shown; the top flange stub is drawn
  above the XG flange.
- Weld of end stiffener to the XG web: 1/4 fillet.

Vertical relationship (elevation views): the discontinuous girder top flange is ABOVE the XG top flange; dimension printed
11" in Unit 2 (top flange to top of XG top flange; reading of "11" vs a rotated "=" is UNCERTAIN) and 8" in Unit 3; the
angle tops are 8" (Unit 2) and 4" (Unit 3) below the XG top flange; Sections A-A / E-E show symmetric dimensions at the
bottom (6 and 8 / 4 and 2 1/2) so the XG is roughly centred in the discontinuous girder depth. No bottom flange
continuity plate, no top flange connection plate and no deck/haunch change are shown for these locations; the A-A/E-E
sections show the haunch outline (R = 6" fillets, 9" to each side of the XG flange) over the XG top flange.

Unit 3 and Unit 2 XG haunch: slab/haunch outline shown at R = 6" typ with 1'-5 (Unit 2) and 10 1/2 (Unit 3) distance
from XG flange top to the outline.

### 1.3 Model vs plans (src\SBM.Core\Steel\SteelModelGenerator.cs BuildCrossGirder, via CLI on a copy)

What the model does today (Unit 2: 4 XGs G2-G5; Unit 3: 2 XGs G2-G3; Unit 1: none):
- XG web PL 80 x 5/8 and flanges PL 15 x 5/8 (Unit 2); web 60 x 3/4, flanges 12 x 7/8 (Unit 3): the model inputs
  use the plan sizes except that the Unit 2 XG web thickness is 5/8 (plan 9/16).
- XG is a straight box web between the two continuing girders, ends 3/4" clear of the girder web faces (plan: connected
  through angles; the gap is not dimensioned).
- Top of XG top flange at the main girders' top of web; web hangs 1.25" below flange. Discontinuous girder end is cut
  0.75" + tw/2 short of the XG CL. 4 angles L5x5x1/2, 76" (Unit 2) / 56" (Unit 3) at the continuing girders and
  2 angles 94" / 68" at the discontinuous end (both parts have no holes or bolts).

Gaps and differences:
1. Vertical position: model puts XG top flange at top of web (flush under the girder flange); plans show it about 11" /
   8" below the discontinuous top flange (UNCERTAIN, see above).
2. Angle lengths: model 76"/56" and 94"/68" vs plan 5'-4" (64") and 4'-4" (52") listed; the model angle length is H - 4"
   from the XG depth rather than the dimensioned 64"/52" angle.
3. No bolts or holes in angles, XG web or girder webs (10/8 bolts per angle at 6" pitch, 2" ends, gage 3"/2").
4. Missing XG stiffeners (vertical PL 5/8 x 7 1/2 or PL 1/2 x 6), opening edge plates PL 5/8 x 6 (F.S.), the 3'-0 x 2'-0
   R6" web opening, 7/8" rope hole stiffener, and the PL 1/2 x 6 1/2 stiffener at the exterior girder edge.
5. Unit 2 XG web thickness 5/8 vs 9/16.
6. XG skew: the model runs the XG along the chord between the nearest continuing girders on a line square to the
   discontinuous girder; the plan orientation plan shows the actual crossing angles (85-54-41 to 90-00-00) - the model
   should be checked against those (it computes none).
7. Girder end detail: no end stiffener/clearance geometry shown or modeled; the model has no bottom flange plates at the XG.
8. The Unit 2 E termination value (86'-8 1/16) and Unit 3 values were not re-read in this pass.

### 1.4 What a model would need (minimal inputs, Part 1)
- Per XG: left/right continuing girder (auto), offsets to the CL XG, XG web (D x t), flanges (b x t), vertical offset of
  XG top flange below the discontinuous top flange (11"/8"), opening width/height/radius and its offset from the stiffeners,
  opening edge plate PL, vertical stiffener PL and positions, angle size/length/count/pitch/gage/edge, bolt dia,
  hole dia 13/16 or 15/16 (check general notes).
- Orientation angles are derivable; use the printed ones as checks.

## PART 2 - Inspection walkways, cables, platforms

### 2.1 What exists
- I-80 WB (sheets 159-170 = PDF 163-174): inspection cable system on Units 1-3 plus walkways in every girder bay (Type
  "A" walkway in Units 1 and 2, Type "B" in Unit 3); chains at the XG openings and cross frame locations.
- Ramp G (sheets 130-134 = PDF 132-136): walkway in all 3 bays (A-B, B-C, C-D) of all four units, cables on the outside
  faces of A and D.
- No ladders, hatches, catwalk platforms or access points at piers/abutments are drawn; the plans give none (no ladder or
  platform details found; cable systems end anchorage at bearing stiffener lines).

### 2.2 Inspection cable system (I-80 PDF 167-168; Ramp G PDF 134)
| Item | Value (PDF) |
|---|---|
| Rope | 1/2" dia IWRC 6x19 galvanized wire rope, extra heavy thimble, 4 rope clips (167) |
| Fittings | 5/8" bolt shackle, 3/4" drop forged turnbuckle, cotter pin, plate washers (167) |
| Support brackets | WT5x15 with 4-7/8" dia bolts at stiffener (167); WT5x44 with 6-7/8" dia bolts at end anchorage/intermediate supports (167) |
| WT5x44 bolt pattern | 2 rows of 3: horizontal 1 1/2, 3, 3, 1 1/2; rows 1 1/2" off the rope line |
| WT5x15 pattern | 2 x 2 bolts, 1 1/2" edges |
| Rope height | 4'-6 above bottom of web (167, 168) |
| Rope offset from web | 3" (sections C-C / B-B, 167) |
| Max spacing between end anchorages | 200'-0 max, 45'-0 min; intermediate supports at 1/2 stiffener spacing |
| End anchorage | 8" from CL bearing; 1'-0 / 2'-8 near sign supports (164); 74'-5 at Ramp G south abutment only |
| Bracket hole | 7/8" dia hole, ground to 3/16" radius; 1 3/4" hole (UNCERTAIN: "1 3/4" or 1 3/16") at piers 12WB-15WB in the stiffener |
| Design load | 10,000 lb point load between end anchorages |
| Locations | outside face of exterior girders (A and F/K/J) all units; between girders in Units 2-3 near the XGs (support brackets "ONLY SHOWN FOR CABLES BETWEEN GIRDERS") |

### 2.3 Walkway (I-80 PDF 169-174; Ramp G PDF 135-136)
| Item | Type "A" (I-80 U1 & U2) | Type "B" (I-80 U3, Ramp G) |
|---|---|---|
| Clear width between rail posts | 2'-1 (1'-0 1/2 each side of CL) | 2'-2 (1'-1 each side) |
| Grating | welded, 3/4 x 3/16 bearing bars at 1 3/16" centres, 1/4" cross rods at 4" max, 3/4" grating depth | same |
| Stringers | C7 channels: C7x12.2 span <= 25', C7x14.7 span > 25' (measured along CL walkway between supports) | L8x4x1/2 |
| Diaphragms | PL 3/8 x 5 at each rail post; also mid-distance between posts for spans > 25' | L3 1/2 x 3 1/2 x 5/16 |
| Top rail | L3x3x5/16 | L3x3x5/16 |
| Intermediate rail | PL 3/8 x 4 | PL 3/8 x 4 |
| Toe board | PL 3/8 x 4 | none shown (Ramp G: yes? UNCERTAIN) |
| Rail posts | L3x3x5/16 at non-skewed frames; 5/16" bent PL at skewed frames; max spacing 8'-6 | same |
| Heights | top rail 3'-6 above grating, intermediate rail 2'-0 above grating (dim "8" at the toe board area) | same |
| Level | grating level; stringers mill camber up | same |
| Position | on chords between supports, centred between girders at the supports; grating 2 1/2" above bottom of web at the XGs (Unit 3) |
| Support | each end bolted to cross frame bottom strut / cross girder with bent L4x4x1/2 (or 1/2" bent plate) on 3/4" H.S. bolts, vertical short slotted holes in stringers (notes) |
| Chains | 1/4" grade 30 proof coil chain with snap latch, 3 chains (typ) across the 2'-0 opening in top and intermediate rail at each cross frame, on 3/8" dia eye bolts (167-174) |
| Steel | A709 Gr 50, galvanized (Ramp G also; painted safety yellow at some legs: federal std 595B colour 13591) |
| Live load | 60 psf or single 500 lb |

Bolt/hole note (verified on raster, I-80 PDF 169 and Ramp G PDF 135): "ALL BOLTS SHALL BE 3/4" DIAMETER UNLESS OTHERWISE
NOTED. HOLES FOR 3/4" BOLTS SHALL BE 13/16" DIAMETER, UNLESS OTHERWISE NOTED." Exceptions: 7/8" bolts in cable brackets
(holes not stated; 15/16" shown for the XG bar), 3/8" eye bolts, 15/16" holes in the XG for 7/8" H.S. bolts and 3/4" bars.
Bolt spacing/edge clearance not shown: AASHTO.

Walkway-to-XG details (I-80 PDF 171-172): at each XG opening the walkway ends 1" clear of the XG web ("END OF WALKWAY
GRATING 1" CL"); bars: 3/4" dia, both sides, connected to the XG web through L6x4x1/2 x 0'-4 bent tabs with one 15/16"
hole for a 7/8" H.S. bolt; geometry on Section Z-Z: 3 + 3 = 6 from web to hole CL (6), tab 4" wide (2 + 2), edge
1/2". Bar centre is 3" off the XG web. Eye bolts and chains in 2 groups of 1'-0 spaced (1'-0, 1'-0 on A-A) across the
opening, 1 1/2 typ. Dimensions to the walkway from the girder web: 2'-6 each side of the XG.

Stud interference: none. Bearing stiffener and cross frame stiffener clearance: stiffener "may be moved up to 1 1/2"" for walkway.

### 2.4 Layout
- I-80 Unit 1 (PDF 163): walkway begins after the west abutment: bay A-B begins at the diaphragm Type 7 line 8" past the cable start; bays B-D begin at the next line, D-F at the next; walkway ends at the end cross frames at Piers 5WB/6A. Cable begins 8" past the abutment CL bearing line on the outside of A and F.
- Unit 2 (PDF 164-165): all bays; the XGs interrupt walkway at the E/D/C/B terminations and the walkway through-bay is joined across them. Cable on A/K outside faces.
- Unit 3 (PDF 166): same, walkway ends at east abutment diaphragm Type 8.
- Ramp G: all bays, units 1-4 (PDF 132-133).

### 2.5 Can a model do it?
Sufficient dimensions for a generic walkway: section dims, stringer table, grating, rail sizes and heights are complete.
Not given: bolt counts on bent angles, ends of walkway between supports (positions of posts are 8'-6 max, equal spaced),
brackets WT5x15/WT5x44 stem length and position along the stiffener, details of the exact walkway start/end stations
(measured on the plan, not dimensioned), anchorage location at stiffener lines.

### 2.6 What a model would need (minimal inputs, Part 2)
Per bridge/unit: walkway Type (A/B) per bay and start/end (support and offset or station); cable lines (girder face, height
4'-6 above web bottom, 3" offset, anchor spacing 45'-200', WT5x44/WT5x15 sizes and 7/8" bolts); walkway width, stringer
section by span length, rail heights, post spacing <= 8'-6, chain locations at the cross frames; XG opening bar tabs. These
are shop-detail level; model as simplified solids (grating plate, 2 stringers, 3 rails, posts, WT brackets).

## UNCERTAIN list
- XG vertical offsets (11" vs "="), and the 6/8 and 4/2 1/2 dimensions on A-A/E-E.
- Bolt diameter and edge/gage on the XG angles (not printed).
- Unit 2 termination E distance, Unit 3 values (not re-read), B to 1/16".
- Cable bracket 1 3/4" vs 1 3/16" hole; whether toe boards exist on Type B / Ramp G.

## Status 2026-10-12 (branch i80-clashes): clash check and fixes

Tool: `sbm clash <model> [--bridge NAME] [--csv out] [--tol LEN] [--bolts] [--welds] [--no-misc] [--no-concrete]` (`SBM.Core/Checks/SteelClashCheck.cs`,
`SbmDocument.Clashes`): part meshes, box broad phase, edge-in-solid narrow phase (depth = distance to the other surface, contact <= 1/16" ignored),
grouped by category pair with station / offset / nearest girder; drains / misc items are also tested against pier concrete. Tests `SteelClashCheckTests`.
Baseline (sample model before): 23,628 clashes, 21,000+ of them inside / around the walkways (I-80 U1 4180, U2 5097, U3 5777, Ramp G 2058 / 1873 / 2469 / 2174).

Fixed from the plans:
- Cross girder (sheet 121 = PDF 125): flanges LEVEL / web plumb (`CrossGirderSection.Level`; the sloped-chord web leaned up to 2 3/8" through the
  continuing girder web into the back-side cross frame stiffener and gusset); discontinuous girder end coped to a web tab (Sections A-A / E-E: tab 1/2"
  from the XG web, flanges stop 9" from the tab end, tab 6" / 2 1/2" inside the XG flanges, R 6"); connection angles follow the girder webs at the plan
  skew (orientation plan).
- Walkways (I-80 PDF 169-174, Ramp G PDF 135-136, `InspectionAccessDefinition.SupportOnFrames`): stringers bear on the cross frame bottom strut and end
  1 1/2" beyond it (G-G / C-C), grating 2'-0 wide, gratings 1" apart over the strut, top rail on the post tops with its leg inside, rails / posts / toe
  boards 1 1/2" clear of frame members, plate diaphragms 7 / 8 (I-80) 1'-4 / 6", Type 5 (Ramp G) 7" grating over the bottom flange, cross girder ends:
  grating 1" clear, stringers 1 1/2", rails 2'-6 from the web, grating 1'-0 (A) / 2 1/2" (B) over the XG web bottom, Type B end diaphragm beside the
  XG flange; far-side walkway on the discontinuous girder line.
- Inspection cables (PDF 167 / Ramp G 134): 7/8" (1 1/2" at Piers 12WB-15WB) rope holes in the stiffeners on the cable face with the rope passing
  through them, support brackets only where the stiffener is on the other side of the web, intermediate supports midway between stiffeners, rope ends
  at the anchorage stems.
- Sign supports: Unit 3 girder A brackets placed (PDF 117 chain 16'-1 15/16 + 12'-10 7/8 + 7'-4 1/8 from A's CL bearing, PDF 181 revised D-D,
  PDF 180 J-J); bracket flanges / web start 1/4" from the girder flange edges (Detail A, PDF 178 / 180).
- Drains: all pipes one piece with mitred joints (drainage notes Ramp G PDF 158); I-80 span 11WB 12" collector, collectors near Pier 12WB, pier
  collection boxes and pier pipes at Piers 11WB / 12WB (PDF 216-220, Section C-C jog at 11WB), flanges / cleanouts / support rods (`drains-i80.csx`).

Remaining / UNRESOLVED (with pages):
- End anchorages 8" from the CL bearing overlap skewed bearing stiffeners (PDF 167: stiffeners "not shown for clarity"); 7/8" rope holes in bearing
  stiffeners skewed 33-56 deg cannot pass the 1/2" rope through the 7/8" plate (PDF 167 Section C-C: hole too small for the skew) - plan conflicts, reported.
- Walkway / XG: grating cantilever over the strut follows the walkway grade (bent-plate seat not modeled), 1/16-5/64" at a few joints.
- Not in this pass: cross frame details at skewed supports (gusset vs web, bearing vs connection stiffener, bottom chord vs web), Ramp G connection
  stiffener vs gusset (1/16-1/4"), I-80 Piers 5WB / 6A joint drainage systems (PDF 216), collectors near 12WB cross frame couplers / supports (PDF 220),
  12WB outlet pavement elevation (PDF 217), pier collection box top range (PDF 216 / Ramp G 157).
