# Sheet link rules audit, 2026-10-19

User rule: "I only want sheets referenced that were used to inform the modeling." The code already passes only the nearest linked ancestor's
links down to an element and never passes general-role sheets down. This audit makes each rules file
(`samples/plans/sheet-links-*.json`) link an element / group only to sheets whose information was used to build or check it. Evidence = page
citations in the build / data / check scripts (`samples/plans/*.csx`), the plan-data folders (`docs/plan-data/<bridge>/SUMMARY.md`, CSV page columns),
UNRESOLVED texts. A sheet that was only read for context, or that the notes call "not modeled", is no longer linked: it shows up in the unused
report (`sbm db ... --unused`), which is the place for it. Sheets that were skipped only because no evidence was found are marked UNSURE (see the end).

Measured with `sbm db "samples/plans/<model>.sbm" <out.sqlite> --no-vacuum --unused <report.md>` on the shipped models. Coverage = elements with any
sheet / elements with a detail sheet, in % (identical before and after unless noted).

| bridge | links before | links after | sheets linked before | after | unlinked before | after | coverage (any / detail) |
|---|---|---|---|---|---|---|---|
| I-80 WB + Ramp G | 23,279 | 22,075 | 400 | 375 | 10 | 35 | 100 / 100 (unchanged) |
| Woodbury I-29 | 740 | 684 | 36 | 35 | 9 | 10 | 100 / 99.9 (unchanged) |
| Br 02069 TH65 | 130 | 122 | 32 | 29 | 6 | 9 | 100 / 99.9 (unchanged) |
| Northaven NHT | 1,878 | 1,649 | 77 | 75 | 75 | 77 | 100 / 99.9 (unchanged) |
| ODOT Bridge O tub | 196 | 195 | 26 | 26 | 23 | 23 | 100 / 100 (unchanged) |
| Bonner Unit 5 | 10,991 | 5,976 | 112 | 57 | 32 | 87 | 99.8 -> 99.6 / 99.6 (unit and alignment no longer carry a sheet) |
| Bonner Unit 6 | 1,376 | 1,276 | 54 | 32 | 44 | 66 | 99.9 / 99.8 (unchanged) |
| Nebraska St Ramp B | 610 | 585 | 52 | 49 | 17 | 20 | 100 / 100 (unchanged) |

General-role rules that stay (they stay on the unit / alignment only, never passed down): general notes where the notes were used (I-80 / Ramp G
steel notes: weathering-steel bolts A325 Type III, PDF 6, `cutoff-diaphragms-and-inspection.md`; Nebraska general notes PDF 4-5 = materials, HPS 70W,
fabrication notes in `fabrication-notes.csv`; Br02069 and Woodbury SOQ / quantity pages used for the quantity reconciliations in
`SUMMARY.md` sec. 7 and `check-woodbury.csx` / `check-br02069.csx`); situation plans and staking (alignment, curve data, support lines:
`check-staking.csx`, `build-*.csx` headers); NHT general notes + summary of quantities (seat tables, rail LF, joint LF, p.91).

## I-80 WB + Ramp G (`sheet-links-i80-rampg.json`)

Kept (evidence in the scripts named): pier details (`piers-i80.csx`, `piers-ramp-g.csx`, PDF cites per pier), pier / abutment aesthetics
(`pier-aesthetics.csx` PDF 83-84 / 78-79; `docs/plan-data/abutments.md` sec. 4 "Verified"), abutment details (`abutments-*.csx`, p85-92 / p80-83), typical
sections / superstructure notes (`deck-rebar-*.csx`, `field-splices-ramp-g.csx` PDF 85), steel layout / framing plans / girder data / girder elevations
(`girder-geometry-i80.csx`, `field-splices-*.csx`, `cross-frames-*.csx`), welding, cross frame, cross girder (`cross-girders-i80.csx` sheet 121),
field splice (`ramp-g-splice-schedule.md`), camber & blocking and field haunch (`camber-*.csx`, `check-blocking.csx`, `check-haunch.csx`), disc
bearings (`bearings.csx`), inspection access / cable / walkway (`inspection-*.csx`, PDF 163-174, 132-136), sign supports (`signs-i80.csx`), slab plan and
reinforcing, bar list, cross slope geometrics, top of slab (`deck-rebar-*.csx`, `cross-slope-i80.csx`, `check-top-of-slab*.csx`), expansion joints
(`joints-*.csx`), drainage (`drains-*.csx`, PDF 212-220 and 154-161), crash cushion end posts (`end-posts.csx`), lighting (`lighting.csx`),
barrier rail details by unit (barrier groups).

| change | rule | evidence |
|---|---|---|
| narrowed | `general-notes`: now GENERAL NOTES only (was + estimated and itemized quantities) | no script or doc cites the quantity sheets; the notes are cited (bolt grade PDF 6) |
| removed | `reactions` (Moment & reaction tables, both sets) | `EXTRACTION.md`: reaction tables "kept as CSV for comparisons, not inputs"; the model has no load / reaction data (existing hint) |
| removed | `longitudinal-section` | no script, check or data doc cites the longitudinal sections as a source (only `sheets-vs-plans.md`, which maps generated sheets) |
| removed | `barrier-aesthetics` | "form liner / recess; not modeled" (barrier is the plain 44 in. F-shape, `build-i80-wb.csx` line 133) |
| removed | `barrier-all` (BARRIER RAIL DETAILS, PDF 228) | no citation; the unit barrier sheets stay |
| narrowed | `inspection-walkway` split by walkway type: Type B (Unit 3, Ramp G) keeps all pages; Type A (Units 1, 2) loses PDF 173 | `inspection-i80.csx`: PDF 173 Section T-T is cited for Type B only |
| hints added | estimated quantities, longitudinal section | unused-report explanation |

Link effect: 23,279 -> 22,075 (reactions ~500, walkway Type A page 173 = 699, rest small).

## Woodbury I-29 (`sheet-links-woodbury.json`)

Already page-limited. Kept as written: situation PDF 6 / staking 7 / staging 8, pier details (cap steps p.9, cap sections p.11, footings), pier / abutment
aesthetics, texture p.44, berm points p.6 / p.40, abutment details p.15-17, typical section p.18-19, camber p.24, longitudinal section p.20
(`SUMMARY.md` "Bearings ... p.20", wingwall elevations p.20), beam layout p.21, deck p.22-23, bar lists p.26, beam data p.25 / 27-30, steel diaphragms p.31,
top of deck p.33, haunch p.34, barrier p.38, general notes / quantities p.3-5 (bearing quantity p.4, notes p.5, quantity reconciliation p.3).

| change | rule | evidence |
|---|---|---|
| narrowed | `pier-details`: no longer links the neoprene pads (target now `pier` part only) | pads are located on the p.9 cap steps; new rule `pier-bearing-pads` (PDF 9 only) |
| split | `pier-footing-bars` (new, PDF 10, footing groups only) | footing mat bars p.10 (`SUMMARY.md` sec. 5, U15); piles keep `pier-foundation` p.9, 11, 12 |
| narrowed | `barrier`: PDF 38 only (was 38-39) | PDF 39 is the barrier bar list; "Not modeled: barrier bars (U23)" |
| removed | `lighting` | linked no element (rule warning); lighting is not in this model |

## Br 02069 TH65 (`sheet-links-br02069.json`)

Kept: general plan p.1 (alignment, profile, clearances, SOQ), transverse section p.2, layout p.3 (working points, seats), abutment sheets p.4-14 and
common p.11, framing p.15, beam p.16, diaphragm connection p.17, bearings p.30 / 31, pile splice p.29, survey p.34 / alignment tabulation 1 p.36, barrier p.22 / 24 / 25
(`SUMMARY.md` sec. 1-6, `diaphragms-br02069.csx`, `check-br02069.csx`).

| change | rule | evidence |
|---|---|---|
| narrowed | `superstructure` (deck): SUPERSTRUCTURE DETAILS 1-2 only (p.18-19) | p.20-21 are the end diaphragm: linked to its reinforcing group (`diaphragm-rebar`) and the abutment (`abutment-section-semi-integral`) |
| narrowed | `barrier`: p.22, 24, 25 (was + BRIDGE DETAILS 4, p.32) | p.32 (B801 joints / B830) appears only in the abutment joint row; no joint is modeled |
| removed | `barrier-cover-plate` (p.27) | "approach panel cover plates ... not modeled" |
| removed | `borings` (p.39) | recorded in `SUMMARY.md` but not used to build or check anything (pile lengths come from p.4 / 9) |
| hints added | borings, approach panel barrier | |

## Northaven NHT (`sheet-links-nht.json`)

Rule bases already cite pages. Kept all arch rules, approach-unit rules, foundation / abutment / pier rules, curbs, IGTS, mow strips, T80SS barrier, rail.

| change | rule | evidence |
|---|---|---|
| removed | `erection` (approach units, "unit" target) | erection sequence p.92-93 is used for the arch only (`SUMMARY.md` U5 tie splices, arch bearing B6 / B7); `arch-erection` and `arch-tie-splices` stay |
| removed | `arch-forces` (p.121-122) | `SUMMARY.md`: "check data, not transcribed" |
| narrowed | `sej-joints`: unit sheets only 162.92' and 122.45' (p.113, 115) | `SUMMARY.md` sec. 6b / basis: "CL SEJ 9 1/4 in from CL Pier 10 / 11 (p.113, 115)" |

## ODOT Bridge O tub (`sheet-links-odot-o.json`)

Kept (cited in `build-odot-o-tub.csx` / `check-odot-o-tub.csx`): O-1 general plan, O-5 staking, Bridge N p.148 and p.180, pedestal elevations (p.262 / 264 / 266, check
only), typical section p.270, pour sequence p.272 (`DeckPours`), framing plan p.273, girder elevations p.278-279, deflections p.280, splices p.281, cross frames p.282,
end / pier diaphragms p.283-288, lateral bracing p.289, misc p.290 (studs, hatches), bearings p.292-293, parapet.

| change | rule | evidence |
|---|---|---|
| narrowed | `deck-reinforcing` (p.271) renamed `bearing-height`: bearing assemblies only | build script: "Deck reinforcing: the plan bar list (p.271-272) is not entered"; p.271 is used for 'H' at the bearings (`check-odot-o-tub.csx`) |

## Bonner Unit 5, segmental (`sheet-links-bonner-unit5.json`)

Evidence: the build reads `box-sections.csv`, `segments.csv`, `bearing-data.csv`, `piers.csv`, `cantilever-tendon-anchors.csv`, `pt-tendon-schedule.csv`
(pages in their columns); `check-bonner-unit5.csx` also uses `geometry-control.csv`, transverse PT (S3-24 / 30 / 34). Kept: typical section p.262, segment designations
p.263-266, geometry control p.267, segment dimensions p.268, closure joint p.281, schedule p.305-306, bearings p.317-319.

| change | rule | evidence |
|---|---|---|
| removed | `general` (index p.249-250) | only lists the sheets |
| narrowed | `foundation-layout`: footings only, alignment dropped | p.251-253 give bent stations, footing plans (`piers.csv`); curve data comes from the general drawings, not this set |
| removed | `pile-data` (p.254-259) | pile counts are read from p.251 (`piers.csv` "piles p251"); pile data sheets not cited |
| removed | `typical-reinforcement` (p.269-270) | "mild reinforcing (p269-282) - not modeled"; bar list not used by build or check |
| narrowed | `end-segment` / `bent-segment`: dimensions sheets only (p.271, p.277) | weights 110 / 100 ton checked; reinforcement and PT sheets "not modeled" |
| split | `transverse-pt-end` (p.272, E1-E4) and `transverse-pt-bent` (p.278, F1 / F2) | `check-bonner-unit5.csx` section 5 (S3-24 / S3-30); `transverse-pt` (p.282) keeps all |
| renamed / retargeted | `shear-keys` -> `duct-grid` (p.283): longitudinal tendons instead of segments | build: "Ducts (S3-35 p283) ... rows A / B / C" give the duct offsets; keys not modeled |
| split | `long-pt` -> `long-pt-schedule` (p.305-306, all tendons) + `long-pt-layout` (p.285-296, cantilever tendons only) | anchor segments of T tendons read from the layouts; B / C "layouts are drawn without anchor stations" (build UNRESOLVED p.285-306) |
| removed | `long-pt` notes p.284 and geometry details p.303-304, `pt-details` p.307 | not used (profiles / anchor geometry "drawn, not tabulated") |
| removed | `anchor-blocks` (p.297, 298, 302) | "not modeled" |
| removed | `erection` (p.309-313) | contractor information only |
| narrowed | `bearings`: BEARING DETAILS p.317-319 (not replacement details p.320) | `build-bonner-unit5.csx`: sizes p.318, data p.319, fixity p.317 |
| removed | `expansion-joint` (p.333) | "joint details bound with S2 / S4 ... (not read)" |
| narrowed | substructure bent sheets limited to the sheet the data came from: bents 18 / 29 p.334 + BOM p.336, bent 19 p.337 + 339, 20-22 p.340 + 342, 23-24 p.343 + 347, 25 p.348 + 350, 26-28 p.351 + 353; caps p.354, 359, 363, 367, 371; columns p.376-381 kept | `piers.csv` source column; build comments "cap sheets p354-375 ... p359 / p363 / p367 / p371" |

## Bonner Unit 6, FIB (`sheet-links-bonner-unit6.json`)

Kept: foundation layout p.395, pile data p.398-399, typical section p.404, framing p.422-424, spans / FIB p.428-431, p.438-439, camber p.446, bearings p.440-444, bents,
columns, caps (pages below).

| change | rule | evidence |
|---|---|---|
| removed | `general` (index) | sheet list only |
| narrowed | `plan-of-spans`: p.409 only | deck width, bent stations, unit ends; deck bars / drains / diaphragms (p.410-415) "not placed / not modeled" |
| removed | `bill-of-material` (p.455) | build: "Deck reinforcing ... bill of material p455 - not placed" |
| removed | `pour-sequence` (p.457) | "pour sequence and closures (p457) ... not modeled" |
| removed | `post-tensioning` (p.489-490) | column PT bars not modeled |
| narrowed | bent sheets: bents 30 / 31 p.458-460, bent 32 p.462, bents 33 / 35 p.465, bent 34 p.468; new `bents-side-view` (p.458) for bents 32-35 | build comments: footing plans (p.459 / 462 / 465 / 468), side view p.458 (pile heads), quantities p.460 |
| narrowed | columns p.477 (30-32) and p.479 (33-35); caps p.481 + 483 and p.484 + 486 | UNRESOLVED texts "p.458, 477", "p.464, 467, 479"; cap comments p.481 / 483 / 484 / 486 |

## Nebraska St Ramp B (`sheet-links-nebraska-st.json`)

Kept: all pier / abutment / superstructure / cap / bearing / deck / barrier / texture rules (`SUMMARY.md` page columns, `build-nebraska-st.csx` Source strings, `check-nebraska-st.csx`).

| change | rule | evidence |
|---|---|---|
| narrowed | `general-notes`: estimated quantities p.2 + general notes p.4-5 (was + summary of itemized quantities p.3) | p.2 quantities, p.4-5 notes cited (`SUMMARY.md` sec. 8, `quantities.csv`); p.3 never cited |
| narrowed | `pier-2`: no longer links the integral steel cap group | the cap is built from p.29-31 (`integral-cap`) |
| removed | `terra-cotta` (p.17) | "terra cotta panels ... not modeled" |
| removed | `reactions` (p.35) | "Moment & reaction table p.35 not transcribed (G11)"; hint added |

## UNSURE (kept or removed without a clear citation; please confirm)

1. I-80 / Ramp G welding details (PDF 120-121, 103) stay on all weld groups (2,900 + 800 links): the weld sizes are presumably from these sheets but no script cites them.
2. I-80 / Ramp G inspection walkway pages: PDF 171 / 172 are also cited for the end-post offsets of both walkway types (K-K, R-R), so only PDF 173 was removed from Type A; cross frame details and drainage pages were not narrowed (every page of the ranges is cited).
3. I-80 / Ramp G `longitudinal-section`, `barrier-all` and the quantity sheets were removed for lack of a citation, not because a note says "not used".
4. I-80 / Ramp G situation plans, staking, `girder-data` pages and the generic DISC BEARING DETAILS pages (PDF 158-159, 129-131) stay as they were (cited only as ranges or by design-sheet numbers that do not match the PDF pages).
5. NHT BRIDGE LAYOUT 1-5 stay on every unit and the alignment: the five layout sheets cover different spans but the PDF is raster, so the sheet-to-unit mapping could not be read.
6. Bonner Unit 5: layout sheets p.285-296 stay on all cantilever tendons (each sheet covers one or two bents, "shown" bents stand for similar ones; the exact bent per sheet is not stated); cap / bent sheets were narrowed to the cited first page, so later pages (reinforcement, pedestal tables if on p.360-362 etc.) are now unlinked if they were used silently.
7. Bonner Unit 5/6 `foundation-layout` / index removal drops the only sheet from the unit and alignment rows (coverage 99.8 -> 99.6 on Unit 5).
8. Br 02069 `diaphragm-connection` (p.17) stays on the prestressed girders (beam holes); the diaphragm is not a database element yet.
9. Woodbury `pier-bearing-pads` (p.9) and `pier-footing-bars` (p.10) are new, narrower rules replacing the broader pier-details link; if the pads are located from p.10-12 as well, add those pages.
