# Plan-set extraction standard

How plan data is pulled from a PDF plan set into the model, so every project is summarized the same way and the
summary maps 1:1 onto model inputs (`samples/plans/*.csx`). Lessons from I-80 WB / Ramp G (Designs 116/316, ~414 sheets).

## Rules (user)
- Model the plans. No fallback guesses: a value that cannot be read or does not reconcile is listed as UNRESOLVED
  (what is missing + PDF page), never filled in.
- Plan precision: 1/16", 0.01 ft. 0.5" is not a match.
- Every value carries its source (PDF page + design sheet) and a confidence tag:
  `TEXT` (read from the text layer, no fractions involved), `RASTER` (confirmed on a raster clip - required for every
  stacked fraction and every value read from graphics), `DERIVED` (computed from other plan values - show the formula),
  `UNVERIFIED`, `UNRESOLVED`.

## Tools
- Python: `C:\SBM\temp\pytools\python.exe -I` (embedded 3.12). Use **pypdfium2** (text + rendering); PyMuPDF does
  NOT load in the sandbox (no VC++ runtime).
- Text cache + sheet index first: one `pNNN.txt` per page + `INDEX.md` (PDF page -> design sheet -> drawing title)
  under `C:\SBM\temp\plan-text\<set>\`. Read the cache; rasterize only small clips (300 dpi of a note / table row).
- Stacked fractions are custom font glyphs in Iowa DOT sets (shown as ƒ Ž ‡ � in text) - text locates, raster confirms.

## Order of work (each step = one summary section; steps 1-4 are the critical path for a first model)
0. **Index** the set: page -> sheet -> title; group pages into sheet families (general, situation/staking, layout,
   typical section, framing, girder, bearings, piers, abutments, deck, rail, joints, drains, misc, quantities).
1. **Geometry control**: horizontal alignment (PIs/curves/spirals, stations, coordinates + datum/projection),
   vertical profile (PVIs, VC lengths), superelevation / cross-slope table, station equations. Staking diagram
   coordinates of the support/bearing points (used later as a check).
2. **Layout**: supports (name, station, skew, type), spans, units/joints, deck edges and widths, barrier, girder count
   and offsets at supports, deck end lines, aux/shared support lines.
3. **Superstructure**: girder type (steel plate / rolled / prestressed shape), plate or strand schedules, field
   splices, cross frames / diaphragms (types + lines), bearings (types, data table, top of bearing), haunch/slab
   thickness, camber/blocking or haunch tables, studs.
4. **Substructure** per unit: cap (profile, sizes), columns, footings, piles (type, length, batter, layout),
   pedestals/seats with top elevations, abutments + wingwalls.
5. **Reinforcing**: bar lists as CSV (mark, size, count, length, shape, dims, unit), bend diagrams, placement rules.
6. **Misc**: joints, drains, inspection access, signs, lighting, aesthetics, utilities.
7. **Checks data**: estimated quantities (by item), top-of-slab / top-of-bearing / haunch / camber tables, moment &
   reaction tables - kept as CSV for comparisons, not inputs.

## Output layout (one folder per plan set)
`docs/plan-data/<set>/`
- `INDEX.md` - sheet index + families with page ranges + which step uses them.
- `SUMMARY.md` - sections 1-7 above; each item a table row: `item | value | unit | PDF p. / sheet | tag | note`.
  Ends with **UNRESOLVED** (all open items) and **Model mapping** (which model input / csx each section feeds and
  which features the program lacks for this set).
- `*.csv` - long tables (bar lists, girder/strand data, bearing tables, elevation tables), header row + source column.

## Efficiency (for a new project, typically ~half the I-80/Ramp G size)
- One indexing pass, then parallel agents by family (geometry+layout | superstructure | substructure | rebar),
  each writing only its SUMMARY section / CSVs; one checking agent reconciles cross-family values (spans vs girder
  data, seats vs bearing table, quantities).
- Build the model in the critical-path order 1 -> 4 and run the plan checks (spans, top of bearing, top of slab)
  after each step: input errors show up early (Ramp G girder line 1'-6 off was found by a span check).
- Write inputs as re-runnable csx scripts, one per topic, run in a fixed order.
