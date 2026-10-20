# PTC Delaware River Bridge: summary

**Plan set:** "Original Existing Bridge Plans from PTC.pdf" (C:\SBMData). The 1954 contract drawings with as-built revisions to 1957, 108 raster pages. Index is in INDEX.md, location in LOCATION.md.

**Designer and owners:** George S. Richardson, Consulting Engineer, Pittsburgh. Associates: Moran, Proctor, Mueser & Rutledge (New York) and Harbeson, Hough, Livingston & Larson (Philadelphia). Owners: Pennsylvania Turnpike Commission and New Jersey Turnpike Authority.

**Contracts:**

| DRB number | Contract | Content |
|---|---|---|
| DRB-1 | PN 1 | River piers |
| DRB-2 | PN 2 | PA substructure |
| DRB-3 | PN 3 | NJ substructure |
| DRB-4 | PN 4 | Steel superstructure, 68 sheets (PDF page = sheet + 40) |
| DRB-10 | — | Borings |

## Structure
Riveted steel, AASHTO 1953, H20-S16-44 live load. Reinforced concrete deck on rolled-beam stringers and floor beams. Trusses 87'-0 c-c, 76'-6" curb to curb (p.42, 47, 72, 107). One tangent alignment, S 69°54'15" E. All supports are square.

| Spans | Structure | Pages |
|---|---|---|
| 1-10, 28-31 | Plate girders with floor beams and WF stringers (123'-2", 124'-5", 121'-5") | p.1, 43-46 |
| 11-13, 25-27 | 3-span continuous deck trusses, 3 x 215'-6" = 646'-6", 9 panels @ 26'-11 1/2" | p.47-53 |
| 14-17, 21-24 | 4-span continuous trusses, 4 x 269'-4 1/2" = 1077'-6" | p.57-68 |
| 18-20 | Main river spans: arch-type continuous truss 341' + 682' + 341', 11 + 22 + 11 panels @ 31'-0 | p.69-100 |

**Main spans 18-20:**
- Crown at about El +163.9.
- Span 19 has a suspended floor on 4" bridge-rope suspenders.
- Shoes: fixed at piers 18/19; hold-down and shear lock at 17/20.
- Also detailed: U11 portal, upper laterals and sway bracing, crown jacking scheme.

**Deck:**
- About 6 1/2" slab, #4/#5 bars, parapets with aluminium railing, a sealed longitudinal CL joint.
- Expansion dams at piers 1-9 and 28-30, at 10, 13, 17, 20, 24, 27, and at mid-span W22 (p.46, 54, 68, 91, 92).

**Substructure:**
- **Approach piers:** every approach pier is a TWIN-SHAFT bent on one footing (piers-review.md): free tapered shafts at piers 1-9 / 28-30 (footings A / B, p.10 / p.37), stepped shafts at 10 / 27 (p.9 / p.36), shafts joined by a haunched CAP BEAM at 11-16 / 21-26 (rev 11-5-54, p.6-8 / p.33-35).
- **Piers 17/20:** on piles, footing 121'-1 1/2" x 33', top El +107.682 (p.17, 27, 29).
- **Piers 18/19:** cofferdam-built footings 158'-10" x 53' on piles with a tremie seal, shafts 87'-0 c-c, top El +87.701. Fender system with dolphins (p.17-21, 23, 25).
- **Abutments:** concrete with wings; culvert at the west end (p.11, 12, 14, 38, 39).

## What the set has and lacks
**Has:**
- Stress sheets, joints, shoes, floor system, bracing, expansion dams, walkways and parapet.
- Piers, abutments, cofferdam, fender and borings.
- As-built revisions and quantities.

**Lacks:**
- DRB-10 sheets 3-6 (only sheets 7-9 of the boring logs are present).
- Shop drawings (American Bridge Division contracts Q 5700-5708, p.42).
- Alignment plans and survey coordinates.

**Possibly absent (not confirmed either way):**
- A camber diagram sheet; p.71 refers to one.
- Per-bar rebar lists.

## Legibility
- Scans are 1-bit and sharp at 105-150 dpi; stress tables and joint sheets need 200-260 dpi.
- Worst pages:
  - p.22, 24, 26: boring-log title blocks
  - p.28, 30: test piles
  - p.2, 3: small text
  - p.47, 57, 69, 70, 72: dense tables
- p.4, 15, 31, 41 carry handwriting.
- There is no text layer, so any OCR needs visual checks.
- Content notes were read for p.1-3, 5, 16, 17, 32, 41, 42, 47, 69, 72, 107. For the joint sheets and the substructure sheets they are inferred from the titles.

## Modeling phases
1. **Location, alignment, profile:** p.1, 5, 16, 32, 42, 69 (crest curve), 107.
2. **Main arch truss geometry:**
   - p.69, 70: panel points, lengths, elevation table
   - p.72: floor plan
   - p.73-90: joints
   - p.94: suspenders
   - p.96: shoes
   - p.97-100: bracing and portal
   - p.90: crown and jacking
3. **Approach trusses and girder spans:**
   - p.47, 57, 48, 58: geometry and floors
   - p.49-53, 59-67: joints and shoes
   - p.54-56, 68: expansion dams and typicals
   - p.43-46: girder spans
4. **Piers, abutments, foundations:** p.17, 27, 29, 6-10, 33-37, 11, 12, 38, 39.
5. **Deck and rail:** p.107, 108, 91, 92, 101-106, checked against the p.42 quantities.

**Program gaps:** the program has no generic inputs yet for a riveted truss with gusset joints, bridge-rope suspenders, or cofferdam-built pier footings. They would have to be added (decision for the user).

## Phase 1a status (2026-10-19): location only - DONE
- Model `samples/plans/PTC Delaware River Bridge.sbm` = `build-ptc-delaware-river.csx`; check `check-ptc-delaware-river.csx` 132 PASS / 0 DIFF; links `sheet-links-ptc-delaware-river.json` (alignment + 7 units linked only to the sheets used: p.1, 5, 6, 7, 9, 11, 16, 32, 34, 36, 38, 42, 43, 69, 107).
- Seven bridges, each `SupportLinesOnly` (new generic option, see CLAUDE.md): girder spans 1-10, 3-span truss 11-13, 4-span truss 14-17, arch truss 18-20, 4-span truss 21-24, 3-span truss 25-27, girder spans 28-31; 31 spans, 38 support lines. Stations, expansion dams, notches: `supports.csv`.
- Alignment: LOCAL grid (origin CL brg W abutment 646+21.463), one tangent S 69 54'15" E, no georeference. Profile = printed gutter profile on the CL + crown 1/8"/ft (p.107). NGVD29 as printed.
- Resolved: E abutment CL bearing 711+92.797 (p.38 / 32 / 42 / 43; p.1 711+94.046 is the superseded original), total 6571'-4" as built; piers 10 / 27 girder shoes 658+61.651 / 706+99.109; piers 14 / 15 read on p.7 / p.6.
- UNRESOLVED: meridian of the bearing; map coordinates / CRS (georeference waits for the user's download OK: aerial / LiDAR fit in PA South EPSG:2272 + NAVD88); survey vs international feet; entry grade of the west curve (-0.5 % derived) and the profile beyond the structure ends (derived -3 %); whether the p.1 centre expansion dams (658+64.568 / 706+96.192) moved with the 10-26-54 shoe shift; expansion dam stations at the other piers (dams are at the pier CLs, not printed); the east paving notch as built (p.1 712+27.047 original vs structure end 712+27.547); state line station; deck, trusses, girders, substructure NOT modeled (no program features yet).
- (superseded by the phases below; kept for the record) Not yet read in detail: p.8 and p.10 (piers 1-12 sheets; their stations were taken from p.5 / p.43), p.17 / 27 / 29 (piers 17-20 footings; stations from p.16), p.33 / 35 / 37 (piers 21-22, 25-26, 28-30; stations from p.32).

## Status 2026-10-20: whole bridge, everything visible (no reinforcement by request)
Build order on `samples/plans/PTC Delaware River Bridge.sbm` (each script re-runnable; the truss / girder scripts rebuild `Truss`, so the deck and connection scripts run after them):
`build-ptc-delaware-river.csx` (alignment, supports; calls `substructure-ptc.csx`) -> `truss-ptc-arch.csx` -> `truss-ptc-approach.csx` -> `girders-ptc.csx` -> `deck-ptc.csx` -> `connections-ptc.csx`.
- Piers: piers-review.md corrections applied to piers.csv (footing lengths piers 16 / 21 = 100'-0, 23 = 98'-0; printed shaft bases filled for 10, 12, 15, 16, 21-28; piers 14 / 23 shaft top 11'-2 1/4 re-read from the p.7 / p.34 dimension chain). Legs are lofted from the printed top to the PRINTED base (the plan dimension governs over the 1/4 in per 12 in batter, differences up to 3/8 in). Cap beams at 11-16 / 21-26 checked against p.6-8 / p.33-35 (depth at the CL and at the shafts). Piers 10 / 27: the along-bridge base (12'-6 / 12'-10 1/2) differs by 2.3 in from the stepped-shaft geometry - reported DIFF, UNRESOLVED (p.9 / p.36).
- Deck, curbs + parapets, aluminum railing, 18 expansion dams, walkways: `deck-ptc.csx` (TRUSS-DESIGN.md section 9).
- Visible steel connections (main truss gussets / chord splices, girder stiffeners / web splices, stringer diaphragms, shoe body material): `connections-ptc.csx`.
- Checks: `check-ptc-delaware-river.csx` 373 PASS / 47 DIFF (was 284 / 0: +2 shaft rows per pier, cap beam depths and the p.42 quantity section 7, whose DIFFs are the reported steel shortfalls, castings envelopes, grating / walkway and wing-railing gaps), `check-girders-ptc.csx` 125 / 6, `check-truss-ptc-approach.csx` 1014 / 12, `check-truss-ptc.csx` 381 / 11.
- Steel vs p.42 items 1-6: 32.78 M lb of 39.28 M lb (83.5 %); not modeled: rivet heads, perforations / manholes (built solid, i.e. overstated), lacing / connection angles, approach chord splices and cover plate lengths (not read), floor beam connections, inlays, bracing gussets, walkway framing, ladders, platforms, pier diaphragms 18 WF 64.
- Still UNRESOLVED (flags with page + reason in the model): gusset outlines (drawn, not dimensioned), rivet patterns, approach truss splice / cover plate extents (p.50-67), dam teeth and supports, railing post layout per panel (p.108), abutment wing railings / parapets, walkway framing / ladders / platforms (p.101-106), girder-span walkways (no sheet), slab edge under the parapet, shoe bodies (envelopes), hold-down / shear locks (p.73), pier 10 / 27 shaft along-bridge base, georeference (approximate).
