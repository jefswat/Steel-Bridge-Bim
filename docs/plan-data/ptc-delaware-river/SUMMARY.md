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
- **Approach piers:** concrete shafts on spread footings, types A and B (p.10). Piers 11/12 have a 94' x 24' x 8' footing and 58'-0 between shoes (p.8). Twin vs single shaft is not yet settled (p.6-10, 33-37).
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
