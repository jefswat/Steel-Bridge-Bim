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
| element review | elements / parts / bars no ELEMENT AGENT REVIEW covers (one flag per bridge / level / category with the count), reviews whose element changed, reviews whose interface changed (a neighbour changed, appeared or vanished), reviews of elements no longer in the model; current reviews with a reported difference are minor |

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

## Element agent reviews (2026-11-01)

An agent compares elements with the sheets they were modeled from (`sbm db <model> --sheets-for <name>`, renders / vector reads of those pages, `sbm measure` /
`sbm section` or a dump script) and writes its findings (e.g. `qc/<model>/<topic>/*.md`). The result is recorded per element with
`sbm review <model.sbm> --record-elements FILE.json` (the companion database must be built after the model: `sbm pipeline` first):

```json
{"reviews": [{"bridge": "Bonner Unit 5", "level": "element", "category": "vertical PT bar", "name": "optional regex", "ids": ["optional GlobalIds"],
  "result": "agrees" | "differs", "sheets": ["p.278"], "note": "what was compared / what differs", "report": "qc/bonner-unit5/vertical-pt/vertical-pt.md"}]}
```

Each recorded element (`AgentReviewRecord.Elements`, in `<model>.agent-review.json`) keeps:
- its FINGERPRINT (`ModelDb.ReviewElements`): hash of what the database records about it (category, subtype, name, mark, material, mass, volume, length, box
  to 0.1 mm, properties); a container's fingerprint includes its children's, so reviewing a segment covers the blocks / bars under it and any change below stales it;
- its NEIGHBOURS (`ModelDb.Neighbours`): elements without children whose box touches its box within 2 mm, its own ancestors / descendants excluded (a box test:
  everything in contact and some elements that only share the box), stored as one hash of their GlobalIds + fingerprints and a count per category.

`sbm review` states every record: current, changed (own fingerprint differs: review it again), interface (a neighbour changed / appeared / vanished: review the
interface again; the categories whose counts moved are listed), missing. Coverage: an element / part / bar is covered by a review of itself or of a container
above it (below the unit level). AI-only (user decision 2026-10-26): there is no human sign-off state. Database: table `element_agent_review` (rewritten by
every review run), views `v_element_agent_review`, `v_element_agent_review_stale`, meta `element_agent_review_at`. Database freshness now compares the model with
meta `built_at` (writing review tables touches the file but does not rebuild the elements).

## Not done yet

- Showing the element review state in the app (properties panel / a viewport filter).
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

## Lessons from the PTC main span joint / bracing reviews (2026-11-02 / 03)

Two rounds on the PTC main span (180 truss joints, 233 bracing end points; files in `qc/ptc/main-span/work10` / `work11`, design notes
`docs/plan-data/ptc-delaware-river/TRUSS-DESIGN.md` sections 33-47). What carries over to other models:

- **Look at pictures, not only numbers.** One generic renderer (`work10/dump_joints.csx` + `render_joints.py`, `work11/dump_bracing.csx` +
  `render_bracing.py`: four views per point, part marks, rivets) let reviewers compare each joint with its sheet crop. The user's examples (S-GL22-R,
  N-BGL15-T, L11') were all visible at a glance; clash counts alone had missed them. Before / after PDFs: `work11/make_pdf2.py` (split under 30 MB).
- **"Connected" needs a definition.** A bracing end counts only when the member reaches its plate, is riveted to it with the drawn count, the plate is
  fastened to the truss / strut / floor beam, and nothing floats or overlaps (`work11/REVIEW2-BRIEF.md`). Count PLACED rivets whose shank crosses both
  parts - the old check counted the numbers written in the plate definitions and passed members with 0 real rivets. The generator now records this
  (`TrussModel.BracingConnections`, warnings for unriveted ends / unfastened plates / fills > 1 in.; `check-truss-ptc.csx` uses it).
- **A crude box audit is a screen, not a verdict.** `work11/bracing_audit.csx` (rivets inside the last 0.6 m of a member) found the problem areas but
  under-counted long laps (L11: 2-9 vs 36-40 placed). Confirm with shank-crossing counts or the generator's own audit.
- **Missing material hides behind correct outlines.** The user's "same gusset issues everywhere" was true for the material around the gussets (splice /
  inlay / fill plates without lengths were silently skipped, lug angles, diaphragms, connection angles) and for whole connection families (sway tops,
  apexes, floor laterals), not for most gusset outlines. A data row the script skips must raise a flag, never vanish.
- **Fixes create new problems; review again.** The second round found what the first round's fixes broke (stacking voids under longer plates, an inlay
  in the gusset layer, a post ply cut by an inlay trim, dropped rivets) - plan a re-check of every fixed joint, not only new areas.
- **Read the sheet notes for stage.** L22's 1 in. gussets were the ERECTION shape; a RIVETS note ("flame cut ... on lines X after field rivets have
  been driven") gives the final condition. Model the final condition (user decision), list erection items as not built.
- **Build order matters when the truss is rebuilt.** Scripts that probe steel built later (platform floors on bracing plates) must run again after it:
  `walkways-ptc.csx` after `bracing-ptc.csx` / `details-ptc.csx` (CLAUDE.md PTC build order).
- **Parallel agents are expensive.** Six to seven agents at once, each re-reading sheets and rebuilding the full PTC model, cost far more tokens than the
  user wanted (2026-11-03). Prefer one topic at a time, main-span-only rebuilds for diagnosis, and ask before fanning out.
