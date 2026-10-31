# Agent review: is a plan-set model complete and traceable?

`sbm review <model.sbm>` (also the last step of `sbm pipeline`) flags what is wrong, or may be wrong because nobody has reviewed it yet, in a model
built from plan PDFs. Code: `SBM.Api/AgentReview.cs`, database queries `SBM.IO/Database/ModelDb.Review.cs`, CLI `SBM.Cli/ReviewCommand.cs`,
tests `AgentReviewTests`. Naming (user, 2026-10-27): this is the AGENT REVIEW; its findings are FLAGS.

## What it flags

| Kind | Flag |
| --- | --- |
| plan PDF | no PDF attached (`Doc.Project.PlanPdfs` empty; the flag names the PDFs the sheet index found and the line to add to the build script), an attached PDF that is not on this computer, a PDF whose SHA-256 fingerprint differs from the one recorded (revised / replaced set: every element modeled from it needs a new review) |
| sheet links | no sheet-links file, a links file that does not name the model, plan sets in the links file that are not attached, attached PDFs with no plan set, no / out-of-date companion database, every sheet no element links to (`v_unused_sheets`), every bridge / level / category of elements with no sheet they were modeled from (no non-general link on the element, an ancestor BELOW the bridge unit, or a member inside it: the bridge's general sheets do not count) |
| program default | every input still at the program's starting value (the value a new, empty object of that type has). Non-zero starting values are OPEN; zero, off, empty, NaN (automatic), negative indices (-1 = all), switches, kind / type choices (enums) and +-999 m sentinels are MINOR (listed with `--all`). Each object names the sheets linked to that support / item ("read on") |
| generator flag | UNRESOLVED / CONFLICT warnings and errors of the regeneration (other warnings are minor); the same flag raised for many items (numbers aside) is one flag with the count |

Inputs the generators do not read are not reviewed: optional objects left null (feature not used), objects with `Enabled = false`, girder / deck /
cross-frame / splice / bolt / stud / bearing inputs of bridges without girders (arch, truss, segmental, cable-stayed, support lines only), steel-only
inputs of prestressed bridges, `Girders.Prestressed` on steel bridges, `Girders.CrossGirder` without terminations, the bridge `Bearings` when every
support has its own, bearing fields of the other bearing types, substructure fields by support type / foundation type / column shape and those
replaced by as-designed inputs (`Plan`, `Abutment`, `CapProfile`, `NoCap`, `Footings`). Names, marks, assembly names, sources, notes and program
accuracy settings (`*Step`, `*Probe`, `*Tolerance`) are skipped. The rules are approximate: when in doubt an input stays in the review.

Project-level civil items are scanned the same way (2026-10-30): `Roads[...]`, `Walls[...]`, `Gradings[...]`, `Utilities[...]` (structures by X / Y skip the station / offset
inputs and the other shape's sizes, pipe ends at a structure skip the free-end coordinates, `Cover` is skipped when both inverts are given, `Sample*` spacings are
modeling parameters).

A starting value that repeats under one input pattern (4 or more places, e.g. every item of a script-built truss list) is ONE flag with the pattern
path (`Bridges[A].Truss.LineItems[*].Side`) and the number of places.

## Record

`<model>.agent-review.json` beside the .sbm (source: commit it):
- `pdfs`: file name, SHA-256, size, date. The first review records a PDF; `--accept-pdf FILE` records a new version after the re-review.
- `confirmed`: starting values the user confirmed are correct (`sbm review <model> --confirm PATH [--note TEXT]`; PATH = an input, an object or a
  pattern as the report prints it). A confirmation holds while the value is unchanged.

The database gets the table `agent_review` (rewritten each run, never by a database rebuild) and the view `v_agent_review_open`; meta `agent_review_at`.

## Pipeline

`sbm pipeline` runs the review last (with the regeneration it already did) and stays open while flags are open; `sbm pipeline --status` and
`sbm regen` count them without writing anything. A model is finished when the review says 0 open, or every open flag is a reported user decision.

## Not done yet

- The per-element comparison of the PDF with the model geometry (an agent reads the sheets linked to each element, `sbm db --sheets-for`, and
  measures the model, `sbm measure` / `sbm section`) and its staleness rule (a result goes stale when the element or a connected element changes).
- Values set by a concept starting shape (e.g. `CableStayedConcepts.TwoTower`) are not distinguished from plan values; their generators raise
  UNRESOLVED flags instead.

## First runs (2026-10-27)

- Nebraska St Ramp B: 265 open (no PDF in the model; 19 sheets nothing links to - lighting, drainage, slope protection, soil profiles; 243 starting values
  e.g. generic bearing body 9" / 16" x 16", masonry plates, anchor bolt grade) + 18 generator flags.
- PTC Delaware River Bridge (after `sbm pipeline`): 1 plan PDF, 274 sheet links (66 unused sheets incl. the main truss joint sheets p.73-100; the main
  truss lines / bracing / suspenders / shoes / floor system, every pier's substructure, walkways, railings and dams have no sheet of their own),
  62 starting values, 1,117 distinct generator flags.
- The "no PDF in the model" flag of these runs is fixed in the build scripts (2026-10-27: every plan-set `build-*.csx` adds its plan PDF to
  `Doc.Project.PlanPdfs`); the committed .sbm files still flag it until they are rebuilt.
