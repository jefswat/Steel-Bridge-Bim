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

Rivet rules by sheet (main truss): chord webs to gussets / splice plates / fillers 1 1/4 in H.S. (joints U6-U14, U20-U21, L11, L15, L21 and most chord joints), 1 1/8 in H.S. in
joints U1-U5, U16-U19, L16-L19 and everywhere for cover-plate splices and web members to gussets; stitch rivets 1 in carbon steel; bracing 1 in carbon (7/8 in stitch). All rivets on the
approach truss sheets p.50 are 1 in H.S.

UNRESOLVED / not read
- Main truss: rivet counts for p.88 and p.89 joints (U18-L21) and p.90 not made; gusset outlines are drawn, not dimensioned (use the printed in-to-in widths and the 2'-6 min./10 in setback rules).
- Approach trusses: joint sheets p.51-53 and p.59-67 NOT read in this pass (only p.50); approach cover plate / fill lengths UNRESOLVED.
- Shoes / dams / stringer sheets p.46, 48, 49, 54, 55, 56, 58, 68, 71, 72, 73, 85, 91-96 not read in this pass (outside the scope list or time-boxed).
- Girder spans: stiffener rivet pitch printed only as maximum pitch rules; span 10 / 28 end details on sheet 9 (PDF p.49) not read; top cover plate length 86'-0 vs the 25'-6 / 37'-0 / 45'-0 dimension lines unchanged.
- Digit-risk readings (low confidence rows): U18-L21 plate thicknesses read at 135 dpi overview, p.89 U20 / U21 / L20 / L21 splice plates, p.45 stringer splice plates, U3 / L2 approach splice bars.
- Weight check (plan items 1-6 on p.42): carbon+silicon steel 3.5+3.82 M lb girder spans, 7.91+8.12 M lb approach trusses, 5.73+10.2 M lb main river spans = 39.28 M lb total.
