# PTC Delaware River Bridge - girder spans 1-10 and 28-31 (model note)

Script `samples/plans/girders-ptc.csx` (re-runnable on `PTC Delaware River Bridge.sbm`), check `samples/plans/check-girders-ptc.csx`
(119 PASS / 8 DIFF / 16 INFO), seats `girders/seats.csv` (p.43 bottom-of-shoe table). Sheets: DRB-4 sheets 3 / 4 / 5 = PDF p.43 / p.44 / p.45
(+ p.42 spans, p.46 dams not built, p.107 deck).

## Structural system (read from p.43-45)
14 SIMPLE spans (10 west + 4 east), each: two riveted plate girders G1 at 58'-0 c-c (29'-0 each side of the CL), floor beams at 4 spaces of
30'-5 3/4 (F1 at the ends, F2 x 3) with brackets B1 outside the girders, 11 stringer lines per span (S1 21 WF 62, S2 24 WF 84, S3 / S4 24 WF 94,
spacing 8'-3, S2 4'-0 outside the girder, S1 6'-1 further out; each 4-span continuous per span, CL splice), bottom laterals ST 6 WF 29 in the
zigzag of the plan, 6 3/4 slab (7 3/4 over the stringers). Bearings 1'-3 each side of the CL pier (c-c bearings 121'-11; spans 10 / 28 120'-5 1/4;
span 1 / 31 123'-2 from the abutment), girder ends 1'-1 beyond the CL bearing (p.44). G1: web 114 x 7/16, 4-Ls 8x8x1 1/8 (9'-6 1/2 b.b.),
cover plates 22 x 13/16: top F.L. + 86'-0 + 51'-0, bottom 90'-0 + 74'-0 + 51'-0. Fixed shoe at one end (p.44: sole plate, key, stem plate 18x9,
2 stiffeners, 2 wing plates, masonry 24 x 4 x 3'-0), expansion shoe at the other (rocker 17 5/8 x 9 x 3'-0, 2 dowels, masonry 19 x 3 1/2 x 4'-0).
Vertical stack: gutter grade - 2'-9 3/8 (back of the top angles) - 9'-6 9/16 (b.b. Ls vertically) = W.P., W.P. - 2'-0 1/2 = bottom of the masonry
plate (3" sole + 18" shoe + 3 1/2" masonry): 14'-4 7/16 in all, which the p.43 seat table reproduces exactly at the girder shoe of pier 10
(91.288 - 76.918 = 14.370).

## Route chosen: the truss generator (`TrussDefinition`), not the I-girder generator
The welded I-girder generator (`GirderLayoutDefinition`, plate layouts) has no riveted angles and no floor system that is made of members; the truss
generator already builds riveted built-up members (H section = web + 4 flange angles + cover plates), floor beams as members, generated stringer
lines + slab, shoes, IFC / database / viewport (design note TRUSS-DESIGN.md). So each girder is one `H` member (role `Girder`) between two points
(simple spans), exactly what the generator does for a truss chord. NO new generator. Generic additions (all OPTIONAL, other models unchanged):
- `TrussPlate.Length` / `TrussPlate.Offset`: partial-length cover plates (centred on the member, shifted by Offset) for H / Box / channel sections -
  needed by every riveted plate girder / floor beam (`TrussSectionPiece` carries them to `BuildMember`).
- `TrussMemberRole.Girder` -> `PartCategory.TrussGirder`, `AssemblyKind.PlateGirderLine` (IFC IfcBeam 'PLATE_GIRDER' in an IfcElementAssembly .GIRDER.,
  database part "plate girder", viewport layer 3D-TRUSS-GIRDERS). Tests `TrussTests.PlateGirderMember_PartialCoverPlates_AreCentredAndExact`.

## Modeled
Girders (web, 4 angles, all 6 cover plates at their lengths), floor beams F1 / F2 (halves girder -> CL, parallel to the roadway crown, flush with the
girder F.L. cover; listed partial plates F2 bottom 42' + 30', top 30', F1 bottom 16') and brackets B1, 44 stringer pieces per span (W shapes
of the same nominal weight), 8 laterals per span, fixed / expansion shoe envelopes + masonry plates with the p.43 seat elevation, one slab per bridge.
Gutter profile = the printed profile of the PTC model; the girder is the straight chord between its end W.P.s (final shape, camber not modeled).

## UNRESOLVED / flagged (each with page + reason in the model; none adjusted)
- (resolved 2026-10-20, user decision) top cover plate 86'-0 as printed on p.43 / p.44: no conflict, no flag.
- F.L. cover plate splice (CL splice 37'-0 + 6'-0 extra material), stiffener fills / crimps, long stiffeners, rivets (1" carbon, 1 1/8 H.S. in tension
  plates), girder camber (1 1/4" / 2") not built. (2026-10-20, connections-ptc.csx: end stiffeners 4-Ls 7x4x3/4, 2-Ls 6x4x1/2 at the floor beams,
  intermediate 2-Ls 6x4x7/16 at 9 / 6 / 6 / 9 equal spaces per panel and the mid-span web splice 2-Pls 21 x 3/8 ARE built, layout / heights flagged;
  deck, curbs, parapets, railing and the sliding-plate dams by deck-ptc.csx; check 125 PASS / 6 DIFF.)
- Floor beam / bracket connection angles, fills, B1 taper (constant 6'-3 1/2 depth, outer end = S1 + 6"), stringer splices / bearings / diaphragms
  (15[ 33.9, 18 WF 64), laterals (ST 6 WF 29 = AISC WT6x29, plane = top of the floor beam covers, gussets / tension plates not built).
- Spans 10 / 28: floor beam spacing (equal spaces), girder ends and end details on sheet 9 (not read); spans 28-31 fixed end assumed EAST (mirror).
- Shoe bodies = envelopes; sole plate bevel not dimensioned. Printed bottom-of-shoe is one value per pier: the ahead shoe of each pier is 0.075 ft
  higher than that seat on the 3 % grade (20 + 8 generator DIFF rows, expected).
- Expansion dams (p.46), curbs / parapets / walkway (p.107), scuppers, signal / light bracket extensions (p.45) not built; slab continuous over the 4" gaps.
- Weight: model 3.75 M lb (west) / 1.50 M lb (east) = 72 / 71 % of the p.42 items 1 + 4 (silicon 90 %, carbon 50 %: dams, diaphragms, splices, stiffeners).

## Update 2026-10-21: remaining visible steel, walkway, (TRUSS-DESIGN.md section 22)
Several bullets above are superseded:
- F.L. cover plate splice: p.44 'Splice for full length cover plate' = the F.L. plate spliced 37'-0 each side of the CL girder under the 86'-0 plate, whose last 6'-0 at each end
  IS the 'additional material for splice' (37 + 6 = 43 = 86 / 2). The 86'-0 plate (user decision 2026-10-20) therefore already contains it: nothing to add.
- Stiffeners (connections-ptc.csx, re-read on p.44): end stiffeners 4-Ls 7x4x3/4 have the 7 in. legs ON THE WEB (two rivet lines each, 2-Fills 15 x 1 1/8 = 2 x 7 + the
  floor beam web), the 4 in. legs outstanding; floor beam stiffeners 2-Ls 6x4x1/2 on 1 1/8 fills; intermediate 2-Ls 6x4x7/16 CRIMPED over the flange angle legs (new generic
  `TrussStiffenerSet.CrimpThickness / CrimpHeight`); stiffener rivets 1 in. (rule-based pitch, flagged); ends 3/8 / 1/2 in. clear of the flange angle root fillets.
- Web splices: p.43 marks TWO web splices ('CL Splice') 4 spaces from the middle floor beam in the 6-equal-space panels (they were at mid-span, on the middle floor beam
  stiffener); 2-Pls 21 x 3/8 + 3 rivet lines each side; the stiffener beside each splice on 2-Fills 4 x 3/8 (p.44). Flange angle splice plates 6 1/4 x 3/8: not built (no length).
- Longitudinal stiffener L 6x4x7/16 (C) on the inside of the girder (p.44), interrupted at every transverse stiffener (new generic `TrussMemberDefinition.LongitudinalStiffeners`),
  heel 2'-5 1/2 below the back of the top flange angles (scaled), 1 in. rivets @ 7 in.
- Tension plates 16 1/2 x 3/4 over the girders at every floor beam / bracket (p.44 Section A, p.45 plan) with 13/16 fills (one at the quarter points, two at the middle
  floor beam = the girder's extra cover plates) and 2 x 5 rivets 1 1/8 H.S. each side; length scaled 4'-3 (girder-details-ptc.csx).
- Stringer splices at the middle floor beam of every span (p.43 stringer diagram 'CL stringer splice', p.45 details; new generic `TrussStringerSpliceDefinition.Stations`).
- Expansion dam diaphragms at piers 1-9 / 28-30 (p.46 Section A / elevation): 18 WF 64 (AISC W18x65 dimensions at 64 lb/ft) between the stringer webs 3 7/8 in. beyond the
  end floor beam CL, 1-Pl 11 x 3/4 on top, 2-Ls 4x4x7/16 per stringer web (new generic `TrussClipAngles.PairHeelU`), stringers continued 9 3/8 in. past the end floor beam
  (new generic `TrussStringerLine.ExtendStart / ExtendEnd`); outer bays (21 WF 62) shallower (flag). 'Pc. 18 WF 64' finger plate supports, 12 [ 35 curb channels, bulkhead
  plates and anchor bars: not built (drawn, not dimensioned).
- CL inspection walk (p.43 Section A-A 'CL of inspection walk' 2'-0 from the CL, p.44 manholes): built by walkways-ptc.csx with the p.101 details, between the floor beam webs.
- Steel vs p.42 items 1 + 4: spans 1-10 85.5 % -> 91.1 %, spans 28-31 83.8 % -> 89.0 % (carbon 78.3 -> 86.9 % / 75.2 -> 82.7 %, silicon 92.0 -> 95.0 % / 91.8 -> 94.7 %).
  Still missing (why): the flange angle splice plates (no length), the dam finger plate supports / curb channels / bulkheads / anchor bars (drawn only), abutment diaphragms
  15 [ 33.9 and the stringer detail at the abutments (p.45, not dimensioned), stringer bearing / tension plate details, signal / light pole bracket extensions (DRB-6, not in
  the set), rivet heads beyond the rule-based lines (floor beam flange / cover rivets, lateral plates), camber.
- (2026-10-22, TRUSS-DESIGN.md section 25) p.43 / p.44 notes 'Floor beams and stiffener Ls shall be set normal to grade': now MODELED - the floor beam halves and the
  brackets are normal to the profile grade (generic `TrussMemberDefinition.NormalToGrade`, set in girders-ptc.csx), their ends cut in vertical planes at the girder
  (11 in. from its CL) and at the crown (`StartCut` / `EndCut`), the girder stiffeners at them and the end stiffener pairs normal to grade and moved onto the leaning
  web planes; tension plates / fills parallel to the grade, CL walkway pieces clear of the leaning webs. The floor beam x girder (140) and floor beam x floor beam (70)
  clash rows are gone.
