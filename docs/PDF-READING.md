# Reading plan PDFs into a model

How plan data is taken from a plan-set PDF, for people and for agents. The project rules and the order of work are in
[plan-data/EXTRACTION.md](plan-data/EXTRACTION.md); this page is the practical how-to with the current tools.

## Rules (user's standing rules)

- **Never invent a value.** A value comes from the plans with its page, or it is an UNRESOLVED flag naming the page and a short plain reason
  ("UNRESOLVED (p.17): CMP casing bottom - it is the bottom of the EPS fill, which is in the road plans, not this set."). Build flags with
  `UnresolvedFlag.Text(pages, item, why, then)` / the scripts' `U(...)` helper.
- **Printed vs scaled.** A printed dimension is a fact. A length scaled from the drawing is allowed only with a dimension-only flag that states the
  page, the scale and how it was calibrated. A part whose existence is not certain goes to the 3D-UNRESOLVED layers; a part that is clearly drawn but
  not fully dimensioned stays on its normal layer with the flag (`Assumption.UnresolvedDimension`).
- **Plan conflicts are reported, never adjusted** (CONFLICT text with both values and pages). No code-rule defaults (AASHTO, AWS) where the plans are
  silent: flag potential noncompliance instead.
- Precision: 1/16 in., 0.01 ft. Tag each value: TEXT (text layer), RASTER (read on a raster clip; required for stacked fractions and graphics),
  DERIVED (computed from plan values; show the formula), UNRESOLVED.

## Tools (run Python with `python -I`)

| Tool | Use |
|---|---|
| PyMuPDF (`import fitz` / `pymupdf`) | text layer, word boxes, page rendering: `page.get_pixmap(dpi=200, clip=fitz.Rect(...))` for crops |
| `tools/plan_index_auto.py <pdf> <out.json>` | sheet index (page -> sheet no. -> title) from the title blocks; scanned pages by Tesseract OCR when installed. `sbm pipeline` runs it |
| `tools/plan_split.py` | splits a set into per-sheet files (optional; sheet links need no split) |
| `tools/plantext.py`, `tools/pdf_dims.py` | dimension strings INCLUDING the stacked / diagonal fraction glyphs plain extraction loses: `python -I tools/pdf_dims.py <pdf> --page N [--clip ...] [--json out] [--png overlay]` |
| `tools/pdfvec.py`, `tools/vecfont.py` | VECTOR geometry of CAD-plotted sheets (even without a text layer): strokes of a width in a region -> lines / arcs / closed loops in real inches at the drawing scale. Prefer it to raster scaling whenever the sheet is vector |
| `tools/ptc_gusset_outlines.py`, `tools/ptc_rivet_count.py`, `tools/bonner_*.py`, `tools/nht_*.py` | project-specific readers built on the above (gusset outlines, rivet symbols, tendon lanes, piles, bracing plates); copy their pattern for a new set |

Raster scans (e.g. the 1954 PTC set) have no text layer and no vectors: render at 150-300 dpi, calibrate the pixel scale on at least one printed
dimension near the detail (record it: "4.28 px/in at 150 dpi, checked on the 2'-5 dimension"), and flag every scaled value.

## Workflow

1. **Index** the set (`sbm pipeline` does it; or `plan_index_auto.py`). A curated `"sheets"` index in `samples/plans/sheet-links-<name>.json` always
   wins over the automatic one.
2. **Find the sheets for an element** without reading PDFs: `sbm db <model.sbm> --sheets-for <mark|name> [--json]` (pages per PDF, read-only) and
   `sbm db <model.sbm> --find-sheet <title|sheet no.>`.
3. **Read** the detail: text layer first (fast, exact), then raster crops to confirm fractions and anything graphic. Write the values into a CSV under
   `docs/plan-data/<project>/` with page, confidence and a note, or directly into the plan-data script `samples/plans/<topic>-<project>.csx`.
4. **Model** from the script (re-runnable, ends with `Doc.Save()`), never by hand-editing the .sbm.
5. **Link** the sheets: rules in `sheet-links-<name>.json` (title regex -> element selectors), so every element references the sheets used to model it.
6. **Agent review**: compare the model with the sheets (`sbm review`, element agent reviews, joint images as in `qc/ptc/main-span/work10/`) and record
   the results. Call it an agent review, never "check" or "QC".

## Tips

- Crop small: a 300 dpi clip of one detail reads better (for a person or an agent) than a whole sheet.
- Similar / opposite-hand details: model the drawn one, mirror in the script, and say in the flag which joint the data came from.
- When a drawn outline is traced, keep the trace (CSV of points + page + scale) so it can be re-checked later.
- Iowa DOT sets: stacked fractions are custom glyphs (shown as stray characters in the text layer) - the text locates, the raster confirms.
