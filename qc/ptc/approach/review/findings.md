# PTC approach trusses - gusset and rivet agent review, 2026-10-30

Scope: 3-span units "11-13" / "25-27" (unit A, p.47-53, p.59) and 4-span units "14-17" / "21-24" (unit B, p.57-67); the NJ units are the PA sheets in the opposite hand.
Model: scratch copy of `PTC Delaware River Bridge.sbm` (saved 12:56, before `JointRivetsShareHoles`), `sbm clash --bolts` on 11-13 and 14-17. Sheets re-read at 250-400 dpi:
p.50 U1 / L1, p.52 L8, p.60 U1 / U2 / U3 / L2, p.67 U20 / L20; everything else as previously read into the data files.

Counts: 30 rows - 7 OK, 17 DIFF, 6 UNRESOLVED, 0 CONFLICT.

## 1. Gussets

| id | joint(s) | item | plan (page) | model | verdict | fix |
|---|---|---|---|---|---|---|
| A01 | all 132 joint definitions (3-span 50, 4-span 82) | thickness / in-to-in | truss-approach/joints.csv | identical everywhere; 25-27 / 21-24 = 11-13 / 14-17 | OK | - |
| A02 | 4-span U1, U2, U3, L2 | thickness / in-to-in | p.60: 3/4 (16 3/4), 5/8 (16 3/4), 7/8 (16 3/4), 7/8 (17 3/8), re-read 250-400 dpi | same | OK | gussets.csv approach4 L2 note "17 1/8" -> "2-7/8 Gussets (17 3/8 in. to in.), re-read p.60"; confidence high |
| A03 | 3-span U1, L1 | thickness / in-to-in | p.50: 5/8 (16); 5/8 (14 3/8) | same | OK | - |
| A04 | 3-span L8 / L8' | layer order | p.52: "2-7/8 Gussets (17 5/8 in. to in.)" on the large outline, "2-3/4 Gussets" on the central plate over it | 7/8 inner + 3/4 outer | OK | reword gussets.csv approach3 L8 note: 7/8 on the members, 3/4 over the central zone (outside) |
| A05 | 4-span U20 | thickness | p.67: "17 1/4 in. to in.", "see U8 sheet 12" (3-span U8 = 5/8) | 5/8, flagged | UNRESOLVED (p.67: thickness by reference only) | gussets.csv approach4 U20 `gusset_thk_in` -> 0.625, note "by reference to U8 sheet 12 (p.52)" |
| A06 | 3-span U4/U6/U10/L5; 4-span U6/U8/U12/U14/U16/U18, L7/L9/L11/L13/L17/L19 | thickness of "similar to" joints | in-to-in printed, "similar to U2 / L1" | referenced joint's thickness, flagged | UNRESOLVED | none |
| A07 | 3-span U1 | fills | p.50: U0-U1 2-Fills 14 1/2 x 5/16 (C); U1-L2 14 1/2 x 3/4; U1-L1 10 x 1 (C) above / 10 x 5/8 (C) below the floor beam; none on L0-U1 | U0-U1 13 x 15/16 + web fills 10 x 1/4; U1-L2 14 1/2 x 13/16; U1-L1 one 10 x 1 1/16; L0-U1 16 x 1/16; U1-U3 16 x 1/8 | DIFF | see A08 |
| A08 | all approach joints (connections-ptc.csx 6a) | fill size | printed "2-Fills b x t" per member end (p.50: L2 14 1/2 x 11/16 and x 3/4, L2-U3 15 1/2 x 1/2 (C), U2-L2 12 x 13/16; U2 12 x 13/16, 10 x 5/8; U3 15 1/2 x 3/4 each diagonal, vertical 10 x 5/8 / 9/16 / 3/4; L1 10 x 3/16, chord 1-5/8 Fill (C)) | computed (in-to-in - interpolated depth) / 2 to 1/16 in.; 1/16 in. chord fills not printed | DIFF | new `connections/approach-fills.csv` with every printed callout (p.50-53, p.59-67); section 6a uses it, computed fills flagged, none under 1/8 in. unless printed |
| A09 | 3-span L11 / L11' | fills | p.53 "similar to L1 ... Omit Fills" | 2-Fills 10 x 1/16 on U11-L11 | DIFF | 6a: skip fills where the joints.csv note says "Omit Fills" |
| A10 | 4-span L13 / L13' | fills | p.64 similar to L1, 14 in. to in., no fill printed | 10 x 1/16 computed | UNRESOLVED | with A08 |
| A11 | 3-span U8 / U8' | 16 WF 96 hanger vs gussets | p.52 2-5/8 Gussets 16 1/2 in. to in. | interpolated shape 0.21 in. too wide per side (8 clashes 3/16 in.; 4-span 8 at 1/16 in.) | DIFF | truss-ptc-approach.csx `OldWF`: explicit 1954 dims where the interpolated depth exceeds the in-to-in (1954 AISC table not in the set) |
| A12 | shoes 3-span L0/L0', 4-span L0/L0'/L10/L20 | gussets vs shoe pieces | p.59 / 63 / 67 | SolePlate x Gusset 24 / 32 rows up to 5 1/4 in. (sole PL 22 wide between gussets 16 1/8-16 7/8 apart); stem x gusset 1.1-1.9 in.; gusset x pin 2.6-2.9 in. | DIFF (sole / stem), OK (pin: holes not cut) | shoes-ptc.csx: sole / stem pieces between the gussets; re-read the section views |
| A13 | 4-span chord splices | splice plates / bars / web plates | p.60-67 | not built (approach-splices.csv has 3-span rows only) | UNRESOLVED (not read) | add approach4 rows |
| A14 | gusset outlines (4-span L2 worst) | outline vs drawn rivets | outlines scaled +-1 in. | rivets under 1.5 d from the edge: 3-span 340, 4-span 708; 4-span L2 16, min 0.09 in. | DIFF | re-trace approach4 L2 in gusset-outlines.csv (p.60); then 3-span L2 / L0, 4-span L1 / L0 |
| A15 | 3-span L2 / L4 / U9 | flange splice plates outside the gussets | approach-splices.csv L2 16 x 3/4 (low), L4 15 1/2 x 1 1/8 (low), U9 16 x 1/2 (medium) | built as outer gusset layers | UNRESOLVED (extents scaled) | confirm with A13 |

## 2. Rivets

| id | joint / member | item | plan (page) | model | verdict | fix |
|---|---|---|---|---|---|---|
| R01 | all | diameter | p.50 / 52 / 60: 1 in. H.S., tack 7/8 carbon, p.60 cover stitch 7/8 carbon | joint 1 in., floor / bracing 7/8 | OK | - |
| R02 | all | count | symbols per face: 3-span 4,327 inside + 846 beyond; 4-span 7,909 + 1,948 | per plane: 3-span 9,886 = 2 x 5,173 - 460 dropped; 4-span 18,654 = 2 x 9,857 - 1,060 dropped | OK (every symbol placed or listed dropped) | - |
| R03 | "similar to" joints (3-span U10: U2's positions on the 14 WF 219 copied onto the 14 WF 87) | copied positions on a narrower member | p.53 / 61-66 | stack-outside drops: 3-span 312, 4-span 714 (U10: 8 per plane) | DIFF | rivets-ptc.csx similar-to copy: scale v by the member width ratio or fall back to the count grid |
| R04 | 4-span U11 / U11', U11-L11 | member assignment | p.64 | 37 positions u -16..+12.4 in. on the 14 WF 61 (half width ~5 in.): 14 per plane dropped | DIFF (data) | reassign rivet-positions.csv rows with abs(u) > 5.5 (u<0 -> L10-U11, u>0 -> U11-L12) after checking p.64; same in rivet-counts.csv; check 4-span L10 and 3-span L8 |
| R05 | 3-span 128, 4-span 244 | dropped beyond the cut end | - | dropped | DIFF | compare setbacks with the drawn member ends; keep positions within 1 in. of the cut |
| R06 | 4-span U5 / U5' (U3-U5 11 per plane); 20 / 102 total | dropped: no clean stack | p.61 2-Pls 16 x 15/16 between the gussets | rivets over the gap cover-plate end / gusset edge dropped | DIFF | check the `BetweenGussets` cover plate ends at U5 against p.61 |
| R07 | 3-span L8 6, L0' 5; 4-span L0 6, L10 9, L12 1, L20 12 (per face) | unassigned symbols | shoe / pin zones | not placed | UNRESOLVED | assign on the sheets or record as shoe connection rivets |
| R08 | 3-span 2 truss + 2 floor + 1 lateral; 4-span 13 truss | grip | - | 18.4-20.75 in.: shank lying in a member web probed as the grip | DIFF (generator) | SteelLineProbe / joint grips: drop a shank lying in a web plane |
| R09 | Bolt x Bolt | clash rows | - | 3-span 36, 4-span 86 (main 650): (a) R08 long grips 2 / 10; (b) head overlaps 0.18-0.30 in. at chord splices (gusset group vs splice group on one hole: 3-span L4/L4' 16, L8' 4, 4-span 16); (c) heads touching 0.06-0.11 in. (~16 / 60) = one hole digitised twice | DIFF | after (a): re-run rivets-ptc with `JointRivetsShareHoles` and the clash; remaining = duplicate symbols in rivet-positions.csv |
| R10 | mirror units | symmetry | opposite hand | 41,714 vs 41,716 and 73,818 vs 73,814; L8 8 vs L8' 4 clash rows | DIFF (minor) | deterministic connection rivet merge order |
| R11 | 4-span cover-plated members (U3-U5, U9-U11, U9-L10, L10-U11) | stitch rivets | p.60 note 7/8 carbon; pitch not read | none | UNRESOLVED | read pitch / gage, then `RivetLines` |
| R12 | floor / bracing connection rivets | counts / pitch | p.48 / 55 / 56 / 58 | not reviewed | UNRESOLVED | separate pass |
| R13 | other clashes | railing, walkway, bracing edge angle x 14 BP 73 | - | 3/16 in. / other reviews | OK | - |
| R14 | grips | range | - | 5/8-5 in. apart from R08 | OK | - |
| R15 | NJ units | panel numbering | opposite hand | identical by panel number | OK | - |

## 3. Data edits (ready)

- gussets.csv: approach4 L2 note (A02), approach3 L8 note (A04), approach4 U20 thickness 0.625 (A05).
- new `connections/approach-fills.csv`, p.50 rows: U1 (U0-U1 14 1/2 x 5/16 (C); U1-L2 14 1/2 x 3/4; U1-L1 10 x 1 (C) above / 10 x 5/8 (C) below the floor beam); L1 (U1-L1 10 x 3/16 (C);
  chord 1-5/8 Fill (C)); L2 (14 1/2 x 11/16 and 14 1/2 x 3/4; L2-U3 15 1/2 x 1/2 (C); U2-L2 12 x 13/16); U2 (U2-L2 12 x 13/16; 10 x 5/8); U3 (diagonals 15 1/2 x 3/4 (C) each;
  vertical 10 x 5/8 / 9/16 / 3/4 (C), extents to scale). p.51-53 and p.59-67 still to read.
- rivet-positions.csv / rivet-counts.csv: reassign approach4 U11-L11 rows with abs(u) > 5.5 in. after checking p.64 (R04).

## 4. Script / generator edits

- connections-ptc.csx 6a: printed fills first; no fill under 1/8 in. unless printed; honour "Omit Fills" (A08-A10).
- truss-ptc-approach.csx `OldWF`: explicit 1954 dims (A11; table needed).
- shoes-ptc.csx: sole / stem against the gusset in-to-in (A12).
- rivets-ptc.csx: scale "similar to" copies (R03); cover plate `RivetLines` once read (R11).
- TrussGenerator: reject shanks lying in a web plane (R08); deterministic connection rivet merge order (R10).
- Before acting on R09 (b) / (c): re-run rivets-ptc and the clash.

## 5. Outcome (fix pass 2026-10-30, rebuilt model)

check-truss-ptc-approach: 1792 PASS / 16 DIFF / 168 INFO (the 1014 / 12 baseline was an older check version; 12 DIFF = the steel weight rows, 4 new = "gusset covers its rivets" N / S U11' of both 4-span units, see R04). Bolt x Bolt: 3-span 36 -> 22, 4-span 86 -> 20.

| id | outcome |
|---|---|
| A02, A04 | fixed (notes) |
| A05 | fixed: U20 gusset_thk 5/8 by the U8 reference, still UNRESOLVED as printed |
| A07-A10 | fixed: connections/approach-fills.csv (p.50 callouts; 32 fill pairs at the printed size, 4 member ends with no fill printed), computed fills under 1/8 in. not built (140), "Omit Fills" honoured (4 joints); p.51-53 / p.59-67 callouts NOT read (computed fills stay, flag says so) |
| R03 | fixed: 'similar to' drawn positions are copied only onto members of the same section; otherwise the count grid (2,200 rivets per face) |
| R04 | open: U11-L11 reassignment not done (p.64 not re-read); the new U11' gusset-cover DIFF rows point at the same rows |
| R08 | open (generator: shank lying in a web plane) |
| R09 | improved by `JointRivetsShareHoles` + inscribed heads |
| R10 | open (deterministic merge order) |
| A11, A12, A13, A14, A15, R05-R07, R11, R12 | open / UNRESOLVED as listed |
