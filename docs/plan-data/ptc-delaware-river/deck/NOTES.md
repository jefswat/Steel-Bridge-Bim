# PTC deck / parapet / walkway / dam / bearing data - notes

Read 2026-10-20 (non-steel items of to-do t08) from the PDF pages below. Columns: id, item, location, parameter, value_printed (as the sheet prints it, no double quotes), value_in (decimal inches where a single number), page (1-based PDF page), confidence, note. UNRESOLVED items say the page and the plain reason.

| File | Rows | Pages read |
|---|---|---|
| deck.csv | 19 | 107 (all details at 230-450 dpi), 55 |
| parapets.csv | 28 | 107, 108 (railing / parapet / abutment parapet / notes), 91 |
| walkways.csv | 17 | 101 (read), 103 (read), 44; 102, 104, 105, 106 NOT read |
| expansion-dams.csv | 28 | 46, 54, 68, 91, 92 (Section A of each at 260-270 dpi) |
| bearings-extra.csv | 23 | 44, 55 |

## Main findings
- Slab 6 3/4 in with a 1 in thickening over each stringer (7 3/4 in), crown 1/8 in per ft each side of the CL, 76'-6 curb to curb; longitudinal CL joint with a 1 in key and a 1/4 x 1 in sealed joint; pipe sockets at 12'-6 centres.
- Curb 2'-0 x 9 in high + parapet wall 10 in x 1'-9 above the curb top, aluminum oval rail 1'-6 above the parapet (3'-3 above the curb), posts at about 5'-8 centres. No sidewalk on the bridge.
- Three dam types: sliding plate with a serpentine seal at piers 1-9 / 28-30 (p.46); finger dams at pier 10 / 27 (p.54), 13 / 17 / 24 (p.68), 20 (p.91, steel parapet corners), and the mid-span W22 (p.92); every dam crosses 38'-3 from the curb to the bridge CL (108 spaces at 4 1/4).
- Service walkways exist only at the approach truss spans (floor beam bottom flange level) and at the main span piers 18 / 19 (floor beam and L11 bottom strut), 2'-0 wide, grating, hand rail, ladders.

## Not resolved / read poorly
- p.102, 104, 105, 106 (walkways continued) not read. p.45 / p.55 (deck at abutments "sheets 5, 15, 51") not read for the abutment deck breaks.
- Digits read at crowded spots (low confidence): railing joint gaps at pier 10 and W22 (p.108), W22 finger opening table (p.92), walkway expansion details (p.101), expansion shoe footprint (p.44), walkway elevations (p.103).
- The "gaps of 1.7-5.9 in. over the floor beams" cannot be settled from p.55 (no gap is printed); see bearings-extra B21.
- Deck reinforcing direction / layer of #5 @ 5 3/4, #4 @ 11, #5 @ 11 not stated on p.107.
