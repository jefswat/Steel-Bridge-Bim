# Nebraska St. Ramp B (Iowa DOT Design 1617) - plan review questions 8a, 8d, 8e, 8f (2026-10-13 review)

PDF: `C:\SBMData\B00167 - Nebraska Ramp B - Design 116.pdf`. Page numbers are 1-based PDF pages (= sheet number; design sheet = p - 1).
Model state read from `samples/plans/build-nebraska-st.csx` and by regenerating `Nebraska St Ramp B.sbm` in memory (read-only probes; nothing saved or edited).

## Q1 (8e) Checkwalls at a different skew than the barrier end

**Answer.** The word "checkwall" does not appear anywhere in the 71 pages (text search). The walls the plans put at an angle to the barrier ends are:

1. **Pier 1 end walls** (p.9, Pier Cap Plan + Pier Elevation): 4" thick x 3'-7 5/8 wide walls at both cap ends, 7'-7 (col 1 end) / 8'-4 1/2 (col 2 end) above the cap top (el 1115.03). They are drawn square to the cap; the cap / CL pier is at 6 deg 19'46" to the girders (p.9, p.27, p.43).
   The barrier bump-out at pier 1 (p.43 Detail C, 2'-7 + 2'-7 about CL pier, 8" widening) has its ends **90 deg 00'00" to the edge of deck**, while CL pier lies at 06 deg 19'46" to it. At pier 2 (Detail D) bump-out and CL pier are the same line, 90 deg. So the plans themselves show the pier 1 end walls (square to the pier line) 6 deg 19'46" off the barrier bump-out ends (square to the deck edge). This is as drawn, not a model error.
2. **S abutment backwall extension** (p.18 Part Plan View): 1'-6 thick x 4'-3 1/4 long, turned **5 deg 54'10"** off the backwall (bars 6g4, p.18-19 View F-F). The barrier at the abutments is std 1020SE "0 deg skew stub abutments with wing extensions" (p.54-55 title block), i.e. square. N abutment (p.20): no angled extension drawn. The 1 deg 17'03" (S) and 5 deg 53'33" (N) labels on p.18 / p.20 / p.22 are the girder / CL-bearing angle to the chord line (p.8), not wall angles.

**Model.** `Pier 1 end wall (column 1/2 end)` = CapBlocks in the cap frame (square to the pier line; csx ~L571-577) - matches p.9. Barrier bump-out zones: Support = pier, Length 3'-11 5/8, DeckWidening 8", DeckWideningLength 5'-2 (csx ~L732-733); I did not verify whether the bump-out ends are cut square to the deck edge (p.43 Detail C) or along the pier line. Abutment backwall extensions / wings / maskwalls: NOT modeled (UNRESOLVED flag in the csx, p.18 / p.20 / p.22).

**Proposed fix / status.** If the user's "checkwalls" are the pier 1 end walls: no input change, the plans show the angle; check that the pier 1 bump-out ends are square to the deck edge (p.43 Detail C 90 deg 00'00"); if they are cut along the 6 deg 19'46" pier line that is the model error (generator, not an input value). If the user means the S abutment backwall extension: plan gives 5 deg 54'10", 1'-6 x 4'-3 1/4 (p.18); model has none; N one UNRESOLVED (p.20, no angle). UNRESOLVED: which element the user calls "checkwall".

## Q2 (8d) Stiffeners past the top flange / welds to nothing

**Plan flange widths (p.28, same for all four girders; equal to the csx plate runs and the regenerated plates):**

| zone | top flange | bottom flange |
|---|---|---|
| S abut - FS1 | 1 x 18 | 1 x 22 |
| FS1 to 20'-0 before CL pier 1 | 1 x 22 | 1 1/8 x 22 |
| 20'-0 before to 19'-0 after CL pier 1 | 2 x 22 | 2 x 22 |
| to FS2 | 1 1/8 x 22 | 1 1/4 x 22 |
| FS2 - FS3 | 7/8 x 16 | 1 x 20 |
| FS3 to 23'-3 before CL pier 2 | 1 1/8 x 24 | 1 1/8 x 24 |
| 23'-3 before to 20'-6 after CL pier 2 | 2 x 24 | 2 1/4 x 24 |
| to FS4 | 1 x 24 | 1 1/8 x 24 |
| FS4 - N abut | 1 x 24 | 1 1/2 x 26 |

Web PL 9/16 x 76 throughout (p.28). The model's per-girder layout (csx L171-185) and generated plates (`G1-1-TF1 PL 18" x 1"` ...) agree, so **the steel top flange widths are right**. The handoff finding is true only for the concrete haunch: `ConcreteGenerator` reads the 18" base section instead of the layout (cap-haunch-status.md) - a code fix (use the layout segment per girder in BuildHaunches / NurbsHaunch / ConcreteCapHaunch), not an input.

**Stiffener sizes (p.27, p.32):** bearing + jacking PL 1 x 10 1/2 (S abut), PL 1 x 10 1/2 (pier 1), PL 1 1/8 x 12 1/2 (N abut) (p.32 table); cross frame connection PL 1/2 x 7 1/2 (p.32 C-C); intermediate PL 7/16 x 6 1/2 (p.32 D-D); jacking 1'-5 from CL brg at the abutments, 3'-3 each side at pier 1 (p.27); intermediate stiffeners at equal spaces between cross frames (p.27 note).
Details (p.32): bearing/jacking 45 deg clip 1 1/2 from CL web; cross frame and intermediate 45 deg clip 2 from CL web; stiffener-to-web fillet 5/16 both sides (bearing/jacking, A-A / B-B), 1/4 both sides (cross frame / intermediate, C-C / D-D); stiffener-to-flange welds "typical all" at Detail A, set 1/4 in from each plate edge; weld end X = 5T (2 1/4 min; T 9/16 -> 2 13/16) from the toe of the flange-to-web weld; bearing stiffeners mill to bear.

**What the model does differently (regenerated and measured):**
1. **Bearing and jacking stiffeners at both abutments overhang the top flange.** PL 1 x 10 1/2 pair on the 1 x 18 top flange at the S abut: 1.78" past each flange edge; PL 1 1/8 x 12 1/2 on the 1 x 24 flange at the N abut: about 0.8" past. The generator warns for all 16 (G1-G4, bearing + jacking, both abutments). **The plans do the same**: p.32 Sect. A-A shows the stiffener wider than the top flange with its outer top corner chamfered 45 deg back to the flange edge (plate sized for the 22 / 26" bottom flange). The model has no outer clip, so the weld across the top edge runs on air. Fix = generator (clip the outer top corner of a stiffener wider than the top flange to the flange edge, weld only the bearing part); the plan sizes themselves are right. Pier 1 (2 x 22 flange) is fine.
2. **Connection stiffeners are skewed in the model, square in the plan.** G3-2-CS3 is "skewed 5.6 deg" (G3-1 CS1..5 0.8..4.0 deg, G3-2 CS1..4 4.7 / 5.5 / 5.6 / 4.9 deg) because `br.CrossFrames.Orientation` is left at Auto (parallel to skew). p.27 shows the intermediate cross frames 90 deg 00'00" (TYP.) to the girders (span 3 "measured with respect to local tangent"); only the pier 1 frame is on the pier line (6 deg 19'46"). The csx comment says "all frames 90 deg to the girders" but never sets it. **I could not make G3-2-CS3 overhang a flange**: its 7 1/2 plates reach 7.78" from CL against an 11" half flange (22" wide) and end on the web top vertically. If the user sees CS3 sticking out, the skew is the likely contributor; UNRESOLVED without the user's view.
3. Clip: model `Stiffeners.CornerClip = 1.5"` for every kind (measured from the stiffener face); plan 1 1/2 (bearing/jacking) and 2 (cross frame / intermediate) from CL web. Weld size: csx does not set `Stiffeners.WeldSize` (plan 5/16 bearing/jacking, 1/4 others).

**Proposed input fix (tested in memory, not applied):** in `build-nebraska-st.csx` after `br.CrossFrames.IntermediateStyle = "INT";` (L292) add
`br.CrossFrames.Orientation = CrossFrameOrientation.NormalToGirders;`
Regenerated with it: all 88 connection stiffeners skew 0; intermediate frames 22.45 / 22.48 / 22.5 ft apart (plan 22'-5 7/16 along A, 6 @ 22'-6, 22'-6 1/8 along D, p.27); the 16 abutment overhang warnings remain (item 1). The pier 1 frame (6 deg 19'46") and the 20'-5 3/8 / 23'-0 end panels still need checking after the change.

## Q3 (8f) Integral steel pier cap stiffeners and welds (p.29-31)

| element | plan callout | page |
|---|---|---|
| Bearing stiffener PL 1 1/2 x 9, each face of each web (8) | 5/16 + 5/16 fillet (TYP.) to web; mill to bear at the bottom with 5/16 + 5/16 weld; **4 3/8" (NO WELD) at the top (TYP.)**; 1" 45 deg clip (TYP.) | p.31 B-B |
| Jacking stiffener PL 1 1/2 x 9 (8, 5'-6 inboard of CL brg) | same as bearing | p.31 C-C |
| Web-to-flange | continuous submerged arc weld, size not shown | p.31 A-A |
| Girder end plates 1/2 (HPS), connector PL 1/2 (HPS), diaphragm PL 3/4 | weld symbols on the elevation without sizes; 5/16 + 5/16 at the bottom bearing plate PL 2 1/2 x 24 "mill to bear typ."; "tight fit"; end plates square and flat after welding | p.30; notes p.29 |
| Handhole backing PL 1/2 x 4 1/2 x 0'-10 | resin bonded to web, tack weld; cover PL 1/2 x 10 x 1'-1 bolted | p.30 G-G |
| Mesh screen frame L2x2x1/4 | tack weld to web | p.31 D-D |
| Cover plates PL 3/4 x 40 3/8; splice plates | bolted; studs end welded | p.31, p.30 |

**Model.** The 16 cap bearing / jacking stiffeners (`CAP2-BS1..4, 13..16`, `CAP2-JS5..12`) each have a `-W` weld "Fillet weld 5/16", stiffener to web (4 3/8" no weld at the top)" - web side only; the plan's bottom 5/16 + 5/16 (mill to bear) is not modeled. **No weld parts** exist for the girder connection plates (`CAP2-C?-CP1/2` connector, `-DP1/2` diaphragm, `-BP` bottom plate, fills) or the handhole backing / end screen frames. Answer: bearing / jacking stiffeners should be welded (they are, except the bottom edge). Connector / diaphragm / end plates have weld symbols in the plan (p.30) but no sizes (UNRESOLVED size); handhole backing is resin bonded + tack only; cover, splice plates and fills are bolted.

## Q4 (8a) Begin of bridge: deck edge / end of slab / barrier end

Plan (S abutment; CL S abut brg = STA 2472+67.56, radial to curve 1; p.6, p.8, p.43):
- Deck end 1'-4 before CL brg along CL Ramp B, square (radial) to CL; deck 29'-2 out to out = 21'-7 left + 7'-7 right of CL / PGL (20'-0 / 6'-0 + 1'-7 gutter offsets) (p.43, p.24, p.18); 572'-7 1/2 out to out of deck along CL (p.43).
- Top of deck at CL S abut brg (p.47 point 1): left edge and gutter 1124.75, girder A 1124.81, B 1125.09, C 1125.38, CL 1125.49, D 1125.66, right gutter 1125.71, right edge 1125.77. No elevation is given at the deck end (1'-4 back): model derives it from the profile.
- Wing extension elevations (p.19 table, at gutter line): SW corner G 1124.75, H 1124.74; SE corner G 1125.73, H 1125.73, I 1125.76. Backwall top parallel to the crown line (p.18). p.18 rear elevation also shows 1124.36 at the left end (feature not identified: UNRESOLVED).
- Barrier (p.54 west, p.55 east): std 3'-8 barrier "0 deg skew stub abutments with wing extensions", 1'-7 base; S end: end of deck, 2" at 50 F gap to the 570'-2 standard section, special section A 1'-6 1/4 with construction joint (1'-0 1/4 / 3 / 3 / 4, 8 spa @ 0'-6 = 4'-0); bid length 578'-1 (west). Barrier ends are square. Beyond the deck the barrier is on the approach slab / wing (roadway plans).
- Expansion joint: steel extrusion, opening 2 1/2" at 50 F at the S abut (p.26, p.48-49).

Model at the begin (regenerated): deck slab starts at -1.34 ft from CL brg (= 1'-4), straight radial end; top z at offsets -21.58 / -20.00 = 1124.74 (soffit 1124.08), -18.34 1124.80, CL 1125.49, +7.58 1125.78 (plan 1124.75 / 1125.49 / 1125.77 at the CL brg). Barriers start at the same -1.34 ft (plan: the standard barrier starts 2" beyond the deck end and special section A carries across the joint: not modeled, p.54). I found **no vertical kink in the deck slab or barrier vertices within 8 ft of the S bearing**; the deck edge is flat 1124.74 from offset -21.58 to -20.00 (edge strip under the rail, p.24) then follows the 5 % crown. So the kinked "deck-edge linework" is likely in another line layer: the backwall top (csx ~L650-660, 5 points at offsets -21.583 / -20 / 0 / 6 / 7.583 on the finished grade at the backwall front face) or the wing / maskwall (not modeled). UNRESOLVED: which linework the user means.

## Unresolved
- "Checkwall" identity (term not in the plans).
- N abutment backwall extension angle (p.20); p.18 elevation 1124.36 meaning.
- Cap girder connection weld sizes (p.30 symbols carry none); cap flange-to-web weld size (p.31).
- Whether G3-2-CS3 overhangs in the user's view: not reproduced from mesh data.
