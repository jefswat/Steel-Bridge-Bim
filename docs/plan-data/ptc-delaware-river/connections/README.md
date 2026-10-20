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
