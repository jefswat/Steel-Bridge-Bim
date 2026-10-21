# PTC Delaware River Bridge - connections and missing steel (plan reading, 2026-10-20)

Read from the 108-page raster PDF (PDF page numbers as INDEX.md). Rivet counts marked "image-counted" were made by pattern detection on 287 dpi renders (solid dots = shop rivets,
rings = open/field holes) and checked visually; accuracy about +-5 % for the chord web field, +-15 % for the small gusset groups; low-confidence rows are those where the count mixes groups.

Files
- `splices.csv` main truss chord splices U1-U22 / L2-L22 (cover splice plates, web splice plates, inlay plates, fills, per-splice rivet counts) p.74-90; approach 3-span unit joints 1-3 (p.50).
- `gussets.csv` gusset thickness / in-to-in, rivet diameters, per-group rivet counts and member lists at each main truss joint p.74-90; approach joints 1-3 (p.50).
- `cover-plates.csv` cover plate and fill extents (half longitudinal section L13-L17 p.69, L15 extra webs, girder cover plate lengths).
- `girder-details.csv` girder spans 1-10 / 28-31: web, stiffeners, splices, cover plates, shoes, floor system, schedules (p.43-45).
- `bracing.csv` sway / lateral / portal / suspended floor bracing connections (p.97-100).
- `quantities.csv` p.42 quantity table by span group.
- `shoes.csv` shoes (material lists, plate sizes, elevations): approach fixed / expansion shoes p.49 / 52 / 59 / 63 / 67, main L0 rocker shoes and shear lock p.73, channel pier shoes p.96.
- `dams.csv` expansion dams (finger plates, movement tables, notes) p.46 / 54 / 68 / 91 / 92.
- `floor.csv` floor beams, stringers, hangers, sockets, schedules p.48 / 55 / 58 / 72 / 85 / 93-95 (main floor beams F5-F8 in full).

Rivet rules by sheet (main truss): chord webs to gussets / splice plates / fillers 1 1/4 in H.S. (joints U6-U14, U20-U21, L11, L15, L21 and most chord joints), 1 1/8 in H.S. in
joints U1-U5, U16-U19, L16-L19 and everywhere for cover-plate splices and web members to gussets; stitch rivets 1 in carbon steel; bracing 1 in carbon (7/8 in stitch). All rivets on the
approach truss sheets p.50 are 1 in H.S.

UNRESOLVED / not read
- Main truss: rivet counts for p.88 and p.89 joints (U18-L21) and p.90 not made; gusset outlines are drawn, not dimensioned (use the printed in-to-in widths and the 2'-6 min./10 in setback rules).
- Approach trusses: joints read in the second pass (see below); cover plate / fill lengths are callouts only (no longitudinal extents except the flange-plan splice plates).
- Shoes / dams / stringer sheets p.46, 48, 49, 54, 55, 56, 58, 68, 71, 72, 73, 85, 91-96: read in the second pass.
- Girder spans: stiffener rivet pitch printed only as maximum pitch rules; span 10 / 28 end details on sheet 9 (PDF p.49) read in the second pass (girder-details.csv); top cover plate length 86'-0 vs the 25'-6 / 37'-0 / 45'-0 dimension lines unchanged.
- Digit-risk readings (low confidence rows): U18-L21 plate thicknesses read at 135 dpi overview, p.89 U20 / U21 / L20 / L21 splice plates, p.45 stringer splice plates, U3 / L2 approach splice bars.
- Weight check (plan items 1-6 on p.42): carbon+silicon steel 3.5+3.82 M lb girder spans, 7.91+8.12 M lb approach trusses, 5.73+10.2 M lb main river spans = 39.28 M lb total.


Second pass (2026-10-20, approach trusses, floor systems, dams, shoes)
Coverage by PDF page: DONE (callouts, plate sizes, fills, splice plates, gusset thickness / in-to-in; NO rivet counts) = p.51-53 (3-span joints 4-12), p.59-67 (4-span joints 0-U20 / L20), p.54 / 46 / 68 / 91 / 92 (dams), p.55 / 56 / 58 / 48 (approach floor system and bracing), p.49 (span 10 end, expansion shoe, strut-to-shoe), p.71 (main bracing member tables), p.72 (floor schedules, reactions), p.73 (L0 shoe, shear lock, U0 / L0 joint), p.85 (floor beam to L14), p.93 (floor beam at W22 and shear lock), p.94 (suspended floor beam, suspender socket), p.95 (floor beam and bracing at L13), p.96 (shoes at piers 18 / 19). Approach rows: unit A = `approach3 (unit A)` (3-span), unit B = `approach4 (unit B)` (4-span); NJ units are the same sheets in the opposite hand.
Approach callout reading: the splice bar callout printed as '6 x 1 1/8' is 6 in. wide x 1 1/8 in. thick (earlier U3 / L2 rows of p.50 read it as '6 x 1'-1/8'); splice and fill thicknesses were read at 130 dpi overview with crops at 170-220 dpi only for the shoe lists, so fraction digits of fills are medium / low confidence (flagged per row).
Rivet counts p.88-90: only shop (solid) rivets per sheet half were counted (gussets.csv rows with page 88 / 89 / 90, low confidence; ring + cross field holes are missed by the detector, so field counts and per-joint / per-group counts are still OPEN).
Still OPEN: rivet counts for ALL approach joints (p.50-53, p.59-67: groups are drawn with solid = shop and rings = field but not counted), p.88-90 per-joint rivet counts and open (field) holes, separating the whole-joint clusters at L10-L14 into groups, p.70 / p.74-84 / p.86-87 untouched this pass (read by the first pass), p.97-100 as in the first pass, p.101-108 walkways / parapet (not in scope).
UNRESOLVED (second pass): U20 gusset thickness (p.67); L0 gusset width in-to-in (p.73); S6 stringer 76 vs 78 lb/ft (p.58, resolved as 76 by p.48 / p.72); fixed shoe stem / wing plate dimension order (p.52 / p.67 digit mismatch 10 3/16 vs 10 7/16, 29 7/8 vs 29 3/4); signal-bridge and light-pole brackets are by others (contract PN 6, DRB-6 not in this PDF); the L13 skew bracing geometry (p.95 Sections E / F); stainless steel clamp pattern at W22 (p.93); p.54 / p.46 / p.68 point to 'sheet 51' for dam details not shown = DRB-4/51 = PDF p.91.


Third pass (2026-10-20, rivet counts, high-resolution re-reads)
New file: `rivet-rules-p70.csv` (p.70 special notes: rivet sizes per joint range, HS rule, pitch, reaming, rivet values).
Rivet counts (gussets.csv, rows appended; columns as before: rivet_open_field = rings = field, rivet_solid_shop = solid dots = shop, `rivet_total`):
- Approach 3-span p.51-53 (joints U5 L4 L6 U7 U8 U9 L8 U11 L10 L12 + the small member-splice and vertical-stub details) and 4-span p.59-67 (U0 U0' L0 L0' U1 U2 U3 L1 L2 U5 L4 L6 U7 U9 L8 L10 U11 U13 L12 U15 L14 L15 L16 U17 L18 U19 L20 + details). Joints that p.50 / 51-53 / 59-67 show as "details not shown" have no rows (U10, U12, U14, U16, U18, U4, U6, L1, L3, L5, L7, L9, L11, L13, L17, L19, U8' ... only the stub detail where one is drawn); p.50 U1-L3 rivet counts are NOT yet made (see Still OPEN).
- Main truss p.88 (U18 L18 U19 L19), p.89 (U20 L20 U21 L21), p.90 (L22, U22 crown), p.86 (L15 only): per group (chord web field left of the web splice line = field rings; right = shop dots; each vertical / diagonal inside the gusset; member-end ring groups; half-flange plan view per joint).
- Whole-joint clusters split into groups: L10 (p.78), L12 (p.81), L13 (p.83), L14 (p.84). The earlier cluster rows stay (they were whole-joint totals) and their group rows now sum to roughly the same figure.
Method: erosion (r = 3 px at 287 dpi) for solid dots, ring/ sector-complete annulus matcher for open holes (black cross / star symbols at the diagonal half-holes and "M N.S." holes are NOT counted), group polygons drawn by eye on the page raster; every group checked on an overlay image. Accuracy: dense triple layers (L8 p.52, L20 p.67, L10 p.63) are over-counted by ~15-25 %, small black dots at centre lines undercounted by ~10 % (marked low). `manual` details are cluster counts of the small member-splice drawings.

Corrections (do NOT overwrite the earlier values: the modeler applies these "CORRECTION" rows; each carries `supersedes` in the note):
1. shoes.csv p.67 L20 fixed shoe: sole 10 3/16 (NOT 10 7/16) and wing plates 29 7/8 (NOT 29 3/4); both sheets p.52 / p.67 are identical. Stem plates 2'-5 7/8 (not 5 3/8) on both sheets. The expansion shoe (p.63) sole 10 7/16 is a different shoe and correct.
2. shoes.csv p.73 L0 rocker shoe: wing plates 24 1/2 x 1 1/2 x 2'-8, stiffeners 24 1/2 x 1 1/2 x 0'-11 1/2 (thickness 1 1/2, not 1/2), rocker base 32 x 6 3/4 x 3'-0 (not 6 1/2).
3. splices.csv p.88 L19 (both callouts 31 1/2 x 7/8), p.89 U20 (I.S. 5/8 + 9/16, fills 1/2), U21 (I.S. 15/16), L20 (I.S. 3/4 and 13/16, fills 11/16, cover fills 3/16, cover 24 x 5/8), L21 (O.S. 34 x 1 1/8). U18 / U19 / L18 confirmed.
4. splices.csv p.50 U3 / L2 splice bars: callout reads 4-Splice Bars 6 x 1'-8 (width x LENGTH, foot mark visible, dimension line spans it); thickness not printed. This DIFFERS from the "6 x 1 1/8 thick, no printed length" note - confidence medium; if the modeler holds 6 x 1 1/8 from the 4-span sheets p.60-61 keep that as the thickness.
5. gussets.csv: U0 in-to-in is 29 in. (2'-5), the 34 in the earlier row is the web width; L0 in-to-in not printed (2-7/8 gussets, inferred 29); U20 (p.67) thickness 5/8 by the sheet's own reference to U8 sheet 12 (inferred, still UNRESOLVED as printed), 17 1/4 in. to in. confirmed.
6. rivet-rules-p70.csv: chord web rivets 1 1/4 in at U6-U13, U20-U22, L6-L15, L20-L22; 1 1/8 in at U0-U5, U14-U19, L0-L5, L16-L19 (the first-pass README had U14 in the 1 1/4 group).
7. girder-details.csv p.45 stringer splices re-read: still low (flange splice width digit uncertain).
Confirmed: W22 finger table (dams.csv), railing gaps by pier (dams.csv, p.108), walkway gaps Pier 13 / 10 (deck/walkways.csv W9a / W9b), walkway elevations (W14a: 151.0625 = gutter line; 141.6667 top walkway at CL; 141.6035 walkway at the truss), fixed / expansion girder shoe footprints p.44 (shoes.csv).

Pages done this pass: p.44 / 45 / 50 (re-reads), 51-53, 59-67 (counts), 70 (notes), 73 (shoe), 78 / 81 / 83 / 84 / 86 (partial) / 88-90 (counts), 92, 101, 103, 108 (re-reads).
Still OPEN: rivet counts for p.50 U1-L3, p.74-77 (first-pass counts exist for chord fields only), p.79 / p.80 (U11, L11), p.82 (U13-U15), p.86 L16, p.87 (U16 U17 L17), p.85; U15 / L16 / L17 flange plan splice counts; the digit risk of dense fills at 3-5 layer gussets; U10 / U12 / U14 etc. details-not-shown joints have no counts by definition. p.45 stringer splice flange plate widths. Walkway sheets p.102 / 104-106 not in this pass.
UNRESOLVED unchanged: U20 gusset thickness as printed; L0 in-to-in as printed; stainless clamp pattern at W22 (p.93); L13 skew bracing (p.95); signal / light pole brackets (by others).


Gusset layers (2026-10-20, `gusset-layers.csv`; every joint sheet p.49-53, 59-67, 73-90 re-checked for stacked gussets)
- STACKED (two or more gusset plates per face): approach 3-span L8 / L8' (7/8 + 3/4, p.52 / p.49), both 3-span end joints L0' (5/8 + 1/2, p.59), 4-span L0 / L0' (3/4 + 1/2, p.59),
  4-span L10 / L10' (7/8 + 7/8, p.63 - NEW, the earlier extraction had one pair), 4-span L20 (7/8 + 3/4, p.67), main L10 / L10' (3/4 + 1/2, p.78), main L22 crown (1 in. half gussets + 1 1/8 splice
  gusset, p.90), main L11 / L11' at the river piers (1 1/8 + 3/4 + 3/4 lower zone, p.80 Section E; LOW confidence). All other joints: one gusset per face (one thickness callout); chord web
  plies, inlay plates and splice plates of the main chords are splice material, not gusset layers (splices.csv).
- Stack order: the thicker / printed-in-to-in plate lies against the members (its in-to-in is the member width + fills); the second plate lies OUTSIDE it: its edges are drawn solid across the
  member rivet fields, which a plate between the member and the gusset could not do without a fill. This SUPERSEDES the "outer 7/8 + inner 3/4" wording of the gussets.csv / joints.csv
  L8 rows ("outer" there meant the larger outline). L22: the two 1 in. halves (one per half truss, 1/8 in. apart, flame cut on line X) are spliced by the 1 1/8 plates outside them.
- Outlines: the outer layers are SMALLER than the inner gusset (lower / central zone of the joint). Polygons in `outline_uv_in` are scaled from 300-450 dpi renders (u = drawing x, right +,
  which is toward the higher panel number on every sheet; v up; origin = the work point; inches): +-1 in. Dimension-only in the model.
- Not separated: the 1/8 in. gap between the L22 half gussets; the step positions of the L11 3/4 plates along the chords (three dimension lines in the elevation); fills between layers (none drawn).


Gusset outlines (2026-10-20, `gusset-outlines.csv`; reader `tools/ptc_gusset_outlines.py`)
- One row per DRAWN joint (the inner gusset = the plate with the 'in. to in.' callout, against the members / fills; stacked outer layers stay in gusset-layers.csv).
  Columns: page, dpi, `px_per_in` (sheet scale at that dpi from a printed dimension >= 3 ft measured on its extension lines, `scale_basis`), `wp_px` (work point = intersection
  of the member centre lines), `chord_px` (two points on the chord centre line: direction only), `outline_px` (polygon, absolute page pixels at that dpi) -> derived by
  `convert`: `drawn_chord_deg`, `outline_uv_in` (sheet frame: u = drawing x = toward the higher panel number, v up, origin = work point, inches), size and area per plate.
  `also_applies_to` = joints the sheet gives as "details not shown, similar to" this one; `evidence` = drawn edges / rivet fields / printed dimensions / symmetry.
- Method: grid-overlaid renders (`grid`, labels = absolute page pixels) at 200 dpi (150 dpi for the large main gussets), every polygon checked on an overlay of the page
  (`overlay`): edges on the drawn plate edges, every rivet of the groups through the plate inside with an edge distance of about 1 1/4 - 1 1/2 in. Edges hidden under members
  or splice plates continue the drawn edges (or the joint's symmetry). Reading accuracy about +-1 in. (+-3 px at 200 dpi on 3/4" = 1'-0 sheets).
- Use (connections-ptc.csx section 4b): rotated so the drawn chord lies on the model chord (the approach sheets draw the chords level; the trusses lie on the grade), mirrored
  for the primed half and for the New Jersey units; "similar to" joints take the drawn joint's outline (flagged). Every outline is a dimension-only UNRESOLVED flag (page + reason).
