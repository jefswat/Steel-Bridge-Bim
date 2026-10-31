# PTC girder spans 1-10 / 28-31 - agent review, 2026-10-30

Scope: girders-ptc.csx, connections-ptc.csx section 2, floor-connections-ptc.csx section 2, bracing-ptc.csx section 3, girder-details-ptc.csx, rivets-ptc.csx section 2
against p.43 (stress sheet), p.44 (steel details), p.45 (floor beam, laterals, stringer splices), p.46 (pier dams). Model values from the script definitions and GIRDERS.md
(no regenerated model: the heavy lock was busy). Scales: p.44 elevation ~2.0-2.1 pt/in, p.45 ~2.1 pt/in; rivets located with a ring finder on 600 dpi crops.

Counts: 24 rows - 11 OK, 10 DIFF, 2 UNRESOLVED, 1 CONFLICT / DIFF.

| id | item | plan (page) | model | verdict | fix |
|---|---|---|---|---|---|
| G01 | G1 section | 1-Pl 114x7/16, 4-Ls 8x8x1 1/8, 9'-6 1/2 b.b., S (p.43, p.44) | same | OK | - |
| G02 | cover plates | top F.L. + 86'-0 + 51'-0; bottom 90 / 74 / 51 (p.43), centred | same | OK | - |
| G03 | F.L. cover plate splice | 37'-0 from CL girder + 6'-0 additional material (p.43, p.44) | inside the 86'-0 plate | OK | - |
| G04 | fixed shoe height | p.44: Pier 1 2'-1 1/8, Pier 2 2'-1 1/4, other piers 2'-1 3/8 (stem "18x9xVaries"); expansion 2'-0 1/2 | 2'-0 1/2 for both | DIFF | girders-ptc.csx shoes: fixed Height by support = FI(2,1.125) / FI(2,1.25) / FI(2,1.375); the 0.075 ft "ahead shoe higher than the seat" rows should pass |
| G05 | fixed shoe at the abutments | p.44 "Abut." leader "3"" and "2'-1 3/8", total not legible | 2'-0 1/2 | UNRESOLVED | flag "p.44 abutment fixed-shoe height not legible"; confirm with the p.43 seat |
| G06 | fixed end of spans 28-31 | the p.44 heights fit only the uphill shoe of each pier pair = east end | east, flagged | OK | flag text: give this reason |
| G07 | flange angle-web rivets | p.43 staggered pitch 2 1/2 (end panel ~5/9) / 2 3/4 / 3 3/4 (FB to splice) / 4 1/2 (between splices) | 7 per line, staggered 3 1/2 everywhere | DIFF | generator OPTIONAL `TrussRivetLine.Zones` (from, to, pitch); rivets-ptc.csx per-line pitch 5 / 5 1/2 / 7 1/2 / 9, end-panel zone limit scaled + flagged |
| G08 | cover plate rivets | p.44: 4 lines, g2 = 3, max 4 3/4 per line, 3 3/4 over the 6'-0 splice zone | 7 per line | DIFF | same `Zones` input |
| G09 | rivet sizes | 1 in. carbon girder, 1 1/8 H.S. tension plates, 7/8 carbon floor / bracing unless noted (p.44 / 45) | 1 / 1 1/8 | OK | - |
| G10 | stiffener rivet pitch | drawn ~6 in. (intermediate, FB, end columns) | 7 / 5 / 4 (rule) | DIFF (low) | connections-ptc.csx sec. 2: RC(2.25, hIn, 6, 2); end stiffeners ~6 in. per line staggered |
| G11 | web splice rivets | 3 columns per side ~3 1/2 apart, rows 3 1/2 | columns 1 3/4 / 4 3/4 / 7 3/4 | DIFF (minor) | columns 1 3/4 / 5 1/4 / 8 3/4 (scaled) |
| G12 | flange angle splice plates 6 1/4 x 3/8 | p.44 drawn ~3'-0 (+-2 in.), off-centre ~1'-9 / 1'-3, clipped corners | not built | DIFF | build 6 1/4 x 3/8 x 3'-0 dimension-only flagged, top + bottom, both faces, both web splices |
| G13 | end stiffeners | 4-Ls 7x4x3/4 F.I.E. + 2-Fills 15x1 1/8, S | same | OK | - |
| G14 | floor beam stiffeners | 2-Ls 6x4x1/2 (C) + 1 1/8 fills | same | OK | - |
| G15 | intermediate stiffeners | 2-Ls 6x4x7/16 (C) crimped, 9/6/6/9 spaces | same | OK | - |
| G16 | first intermediate stiffener at the end | p.44 "...(Crimp) (C) / 2-Fills 4 x 3/8" | fills only at the splices | UNRESOLVED | stiffFlag: "p.44 also gives 2-Fills 4x3/8 at the first intermediate stiffener at the end; purpose not shown - not built" |
| G17 | longitudinal stiffener L6x4x7/16 | 6 in. leg on the web, ~1'-9 1/2 to 2'-3 1/2 below the top angle backs (scaled) | 4 in. leg on the web, heel 2'-5 1/2 | DIFF | connections-ptc.csx sec. 2: 6 in. leg on the web, heel Y = In(57.25) - In(21.5) (scaled, flag kept); check LegDown convention |
| G18 | web splices | 2-Pls 21x3/8 at "CL Splice" 4 spaces from the middle FB | same | OK | - |
| G19 | F1 / F2 / B1 | p.43 schedule + p.45 | same | OK | - |
| G20 | FB / bracket web to girder | 1 in. carbon, ~21 @ 3 1/2 (p.45) | 21 x 1 in. @ 3 1/2 | OK | - |
| G21 | corner plate rivets | 1 in. carbon reamed (p.45 note), ~15 per plate (visual, medium) | 7/8 in., 5 per plate | DIFF | floor-connections-ptc.csx sec. 2: RivetDiameter In(1), pattern as drawn (scaled, flag) |
| G22 | lateral end rivets at the CL floor beam | 2 x 5 = 10 per end (p.45), 7/8 carbon | 8 x 1 in. | DIFF | bracing-ptc.csx sec. 3: RivetsPerMember 10, RivetDiameter In(0.875) |
| G23 | tension plates 16 1/2 x 3/4 | 2 x 5 rivets 1 1/8 H.S. each side, ~51.7 in. (scaled) | 2 x 5, 4'-3 | OK | - |
| G24 | stringer splices | 24 WF: 2-Pls 18 1/2x3/8, 1-Pl 9x1/2 N.S., "1-Pl" 4x9/16 F.S., 7 rows; 21 WF: 2-Pls 16 1/2x3/8 (medium), 1-Pl 8 1/2x3/8 N.S., "1-Pl" 3 1/2x7/16 F.S., 6 rows | all 18 1/2, 7 rows, F.S. = 2 plates | CONFLICT / DIFF | girder-details-ptc.csx Spl("W21x62", ...): web plate 16 1/2, 6 rows (per-splice plate height argument); F.S. "1-Pl" reported as a plan question, two plates kept |

Not re-checked: the p.43 lateral pattern sequence, p.46 dam diaphragms, stringer seats, abutment diaphragms, camber, CL walkway.

## Outcome (fix pass 2026-10-30, rebuilt model)

check-girders-ptc 125 PASS / 6 DIFF -> 128 PASS / 9 DIFF / 14 INFO (both girder units; DIFF rows = slab width 82'-2, W.P. vs seat 2 shoes per unit (was more, max now 0.072 / 0.075 ft), steel weights, deck concrete).

| id | outcome |
|---|---|
| G04 | fixed: fixed shoe Height by pier (Pier 1 2'-1 1/8, Pier 2 2'-1 1/4, other piers 2'-1 3/8) |
| G05 | flagged: abutment fixed-shoe height not legible, 2'-0 1/2 kept |
| G06 | flag text gives the reason for the east fixed ends of spans 28-31 |
| G07 / G08 | fixed: generic `TrussRivetLine.Zones`; flange angle lines 5 / 5 1/2 / 7 1/2 / 9 in. per line by zone, cover plate lines 4 3/4 (3 3/4 over 37'-0 - 43'-0), zone limits scaled, flagged |
| G10 | fixed: stiffener rivets 6 in. pitch (end stiffeners 2 staggered lines) |
| G11 | fixed: web splice columns 1 3/4 / 5 1/4 / 8 3/4 |
| G12 | open: flange angle splice plates not built |
| G16 | flagged (2-Fills 4 x 3/8 at the first intermediate stiffener, not built) |
| G17 | fixed: L 6 x 4 x 7/16 with the 6 in. leg on the web, heel 1'-9 1/2 below the top angle backs (scaled) |
| G21 | fixed: corner plate rivets 1 in. (count 5 per plate kept, about 15 drawn - flagged) |
| G22 | fixed: 10 x 7/8 rivets per lateral end |
| G24 | fixed: 21 WF 62 splice web plates 16 1/2, 6 rows; F.S. "1-Pl" reported as a plan question (two plates kept) |
