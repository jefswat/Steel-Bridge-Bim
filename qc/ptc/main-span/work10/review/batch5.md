# Agent review batch 5: PTC main span, primed half U14' - U7' (2026-11-02)

Joints: U14' L13' U13' L12' U12' L11' U11' L10' U10' L9' U9' L8' U8' L7' U7', both truss planes N and S. This review is read-only. I compared the model images
`png/{S,N}_<joint>p.png` with the unprimed images, the raw dumps in `out/`, `connections/splices.csv` and the joint sheets p.76-83.
Findings: `batch5.csv` (87 rows). My scratch scripts are in `review/b5work/`:
- `mirror2.py`: primed vs mirrored unprimed, per part vertex sets plus rivet midpoints
- `ns.py`: N vs S
- `rivout.py`: rivets across the truss plane that are not in any gusset / splice / fill / connection plate
- `chordbot.py`: gusset edge vs chord face along u
- `outline.py`: gusset / edge angle outlines

## Verdict counts

| verdict | major | minor | info | total |
|---|---|---|---|---|
| CONFIRMED | 17 | 10 | 15 | 42 |
| OK | 0 | 0 | 42 | 42 |
| UNSURE | 0 | 1 | 2 | 3 |

## Mirror / opposite hand and N vs S (15 of 15 joints OK)
- I mirrored every unprimed joint in u about its W.P. Every primed part vertex then lies within 0.75 in. of the matching unprimed vertex (radius 100 in.). This covers
  gussets, gusset layers, edge angles, splice / fill plates, bracing plates and fills, and members. Rivet midpoints match within 1 in. (at most 4 of 260-966 without a twin).
  The W.P. stations are symmetric about L22 = 682+80.38 and the elevations are equal. Diagonals land on the mirrored side and the edge angles sit on the mirrored edges,
  which is the "opposite hand" the sheets ask for.
- N and S are identical, mirrored across the CL.
- Only two things are not mirrored:
  - The manhole / perforation hole positions in the perforated cover plates of the bracing members and verticals, offset 13-36 in. That layout is already UNRESOLVED.
  - The L11' shoe. It is an expansion shoe with a roller and the L11 shoe is fixed (p.96), which is intended.
- So every finding below also applies to the unprimed joint (batch 2), and the other way round.

## Systematic patterns (counts over 15 joints x 2 planes)
1. **Chord field splice material is missing or incomplete at 14 of 14 joints that have it on the sheet.** L11' has gusset layers and no splice rows.
   - The sheets call out cover splice plates, i.s. / o.s. web splice plates, inlay plates and fills (splices.csv).
   - 9 joints have none of them: U14', L13', U13', L12', U12', U11', L10', U9', L8'.
   - U10', L9' and U8' have only the o.s. web plate, cut to the gusset.
   - L7' and U7' have part of the set; the cover splice plates and inlays are missing.
   - This is the same gap batch 4 found for joints 14-21.
2. **Gusset chord edges do not follow the model chord at 5 of 15 joints.** The outline was sheared onto a chord direction about 4-6 deg off the model chord:
   - U9': top edge up to 11.8 in. under the chord top.
   - U13': up to 8.9 in. under.
   - L8': bottom edge 10.2 in. above the chord bottom at one end and 4.2 in. below it at the other.
   - U14': gusset 5.3 in. proud of the chord top.
   - U12': up to 4.6 in. under.
   - At the other 10 joints the chord edge is within about 2 in.
3. **Drawn rivets outside the modeled gusset at 3 joints** (single-ply grips, i.e. rivets through one member plate only):
   - U14': 7 chord rivets per face beyond the gusset end, and 4 diagonal rivets per face below the bottom edge. The end follows the fill end line instead of the plate edge (p.82).
   - U12': 3 diagonal rivets per face.
   - U7': 4 hanger rivets per face below the gusset.
   - U9' and U13' also have chord rivets above the gusset top; that comes from pattern 2.
4. **Edge angles shorter than the drawn free edge at 3 joints:**
   - L13' R1: 76 in. vs a 109 in. edge, so the bottom 27 in. above the chord has no angle; the sheet draws the angle down to the chord.
   - L13' R2: 16 in. short at the vertical.
   - U12' R1: 15 in. short at the vertical corner.
   - U13' R1: 7 in. short.
   - The other edge angles stop 3-7 in. from both ends, which looks deliberate.
5. **The user's "same gusset issues everywhere": partly confirmed.**
   - Every primed gusset is an exact mirror of its unprimed gusset, so any unprimed gusset error repeats in the other half.
   - The general topology matches the sheets at every joint: which edges carry edge angles, top across the vertical, curved bottom at L13', layers at L10' / L11'.
   - Only 5 of 15 joints show a clear outline misfit (pattern 2), and 3 have drawn rivets off the plate (pattern 3).
   - The one issue that really is everywhere is the missing splice material (pattern 1).

Other:
- The image titles print the hundreds of the station rounded, so stations at +50 or more show 100 ft too high (L13' shows 686+59.38; the true station is 685+59.38).
- The walkway items left out of the images are not mirrored: the top-chord catwalk exists only in the unprimed half, and the L11 / L11' ladders and platform posts differ. I did not review them.

## Sheet crops used
`batch5_U14p_p82.png`, `batch5_U14p_p82b.png`, `batch5_U14p_p82c.png`, `batch5_U13p_p82.png`, `batch5_L13p_p83.png`, `batch5_L13p_p83_gusset.png`, `batch5_U12p_p81.png`,
`batch5_L12p_p81.png`, `batch5_U11p_p79.png`, `batch5_L11p_p80.png`, `batch5_U10p_p78.png`, `batch5_L10p_p78.png`, `batch5_U9p_p77.png`, `batch5_L9p_p77.png`,
`batch5_U8p_L8p_p77.png`, `batch5_U7p_p76.png`, `batch5_L7p_p76.png`.

## Limits
- I compared the outlines by topology plus the chord-edge fit and rivet containment. I did not overlay every edge dimension on a calibrated sheet; the outlines are scaled traces (+-1 in., flagged).
- L13' was calibrated on printed dimensions: 2'-9 3/4 below the gutter grade El. 151.9080 puts the gusset top 100 in. above the W.P., which matches the model.
