"""Agent review findings (hand-compiled from the plan pages and work/families.txt / show2.py output) -> findings.csv / findings.md."""
import csv
from collections import Counter
R = []
def r(id, fam, item, plan, page, model, diff, verdict, cause=""):
    R.append(dict(id=id, family=fam, item=item, plan=plan, page=page, model=model, difference=diff, verdict=verdict, cause=cause))
# ---------------- cantilever T (p.303)
r("T01", "T1-T14 (240 cantilever tendons, 10 bents)", "anchor setback from bulkhead (tip) face", '9"', "p.303 elevations / plans", '9.000" all', "0", "MATCH")
r("T02", "T1-T14", "anchor depth below deck top", "1'-1 1/2\"", "p.303 Section B-B / elevations", "1'-1.500\" all", "0", "MATCH")
r("T03", "T1-T14", "anchor lateral from CL girder (P1 / P2)", "P1 10'-2 1/2\", P2 12'-4 1/2\"", "p.303 Section B-B", "A7 10'-2.5\" / A5 12'-4.5\"", "0", "MATCH")
r("T04", "T3,T5,T8,T10,T12 / T7,T9,T11", "anchor name in the plan figures", "figures label T3/T5/T8/T10/T12 anchor 'P1' and T7/T9/T11 'P2'; their lane ticks, the layouts and Section B-B make them P2 / P1", "p.303 figures vs p.303 B-B, p.285-296", "lane positions modeled", "names only", "PLAN CONFLICT", "plan labels")
r("T05", "T1-T14", "anchor P.I. from bulkhead face (9\" + 9'-0\")", "9'-9\"", "p.303", "9.750 ft all", "0", "MATCH")
r("T06", "row A (all but T5, T6, T14)", "vertical exit angle anchor -> P.I. (1'-1 1/2\" to 7 1/4\")", "3.31 deg", "p.303 row A elevation", "3.312 deg", "0.002 deg", "MATCH")
r("T07", "row B (T5, T6, T14)", "vertical exit angle (1'-1 1/2\" to 1'-4 1/4\")", "1.46 deg", "p.303 row B elevation", "1.459 deg", "0.001 deg", "MATCH")
r("T08", "all T", "duct row", "C2, T5, T6, T14 in row B ducts (note 1)", "p.290 note 1 (all layout sheets)", "T5, T6, T14 row B; others row A", "0", "MATCH")
r("T09", "T7-T12", "anchor plan exit angle", "5.40 deg", "p.303", "5.400 deg", "0", "MATCH")
r("T10", "T3, T4, T5, T6, T14", "anchor plan exit angle", "6.86 deg", "p.303", "6.860 deg", "0", "MATCH")
r("T11", "T7-T12", "anchor P.I. offset from the 3.07 deg diagonal through the next hole", "2 3/4\" (5.40 deg x 9'-0\" + 2 3/4\" = 1'-1\")", "p.303", "check rows <= 0.056\"", "0.056\"", "MATCH", "plan rounding of 5.40 / 3.07 deg")
r("T12", "T7-T12", "direction change at the joint after the anchor P.I.", "none drawn (straight 3.07 deg)", "p.303", "0.062-0.066 deg corner", "0.066 deg", "MATCH", "same rounding")
r("T13", "T1-T14", "anchor curve radius", "R = 35'-0\" (note 3: minimum)", "p.303", "35.000 ft all", "0", "MATCH")
r("T14", "T7-T13", "lane-change P.I.s from both faces", "3'-6\" / 3'-6\"", "p.303", "3.500 ft all", "0", "MATCH")
r("T15", "T7-T13", "lane-change radius", "R = 35'-0\"", "p.303", "35.000 ft all", "0", "MATCH")
r("T16", "T7-T12", "9\" lane step: 3.07 deg diagonal through the tip-face hole, P.I. 3'-6\" on it", "3.07 deg, 2 1/4\" off lane", "p.303", "3.070 deg, 2.253\" off lane", "0.003\"", "MATCH")
r("T17", "T13 (20 tendons)", "lane change in the segment next to the anchor segment", "S-curve with BOTH P.I.s on their lanes (1'-1\" step); duct straight in its lane through the joint", "p.303 (TENDON T13 plan)", "bulkhead-side P.I. on a 3.07 deg diagonal 2.253\" off the lane; joint crossed at 3.07 deg; anchor leg 1.451 deg AWAY from the new lane (anchor P.I. 2.735\" off)", "2.25-2.74\"; 1.45-3.07 deg", "DIFF", "generator: ChainAngleDeg applied to every tendon whose last segment changes lane (ChainJ(n-1)); p.303 dimensions the diagonal only for T7-T12")
r("T18", "T1, T2, T13", "anchor plan exit", "no plan figure", "p.303", "T1 / T2 0 deg (anchor in its lane); T13 see T17", "-", "NOT GIVEN", "p.303 details only T3-T12 and T14 anchors")
r("T19", "T1-T14", "direction change at joints", "only minor", "p.303 / p.285-296", "<= 0.066 deg", "<0.1 deg", "MATCH")
r("T20", "all", "tangent lengths / minimum straight lengths", "not stated (only R minimum, note 3)", "p.303", "-", "-", "NOT GIVEN")
# ---------------- bottom continuity B1-B7 spans 20-28 (p.297, p.304)
r("B01", "B1-B7 spans 20-28 (252 tendons)", "anchor height above bottom slab top", "1'-1\"", "p.297 View A-A", "1'-1\" (input)", "0", "MATCH")
r("B02", "B1-B7", "anchor lateral", "1'-7\" from web face at the block top (2'-0\" above slab)", "p.297 View A-A", "as printed (input)", "0", "MATCH")
r("B03", "B1-B7", "anchor station", "block 4'-0\" from bent face to the block face top; anchor face 'VARIES', 1 3/4\" chain", "p.297 plan / elevation", "anchor at 4'-0\" from bent face", "-", "NOT GIVEN", "anchor at the duct CL not dimensioned along the girder (scales ~2\" toward the bent of the block-top corner)")
r("B04", "B1-B7", "anchor to P.I. along CL", "6'-0\"", "p.297 elevation", "6.000 ft", "0", "MATCH")
r("B05", "B1-B7", "anchor deviation radius", "R = 23'-0\"", "p.297", "23.000 ft", "0", "MATCH")
r("B06", "B1-B7", "anchor deviation in plan", "duct straight in plan in the block", "p.297 plan", "0.08-0.93 deg plan component (anchor 0.1-1.2\" off the lane line)", "up to 0.93 deg", "PLAN CONFLICT", "p.297 anchor offset (1'-7\" from web at 2'-0\") does not lie on the p.283 row C lane (1'-6\" + n x 9\" from soffit corner); a straight plan duct cannot satisfy both")
r("B07", "B1, B6", "lane-change P.I.s from bent-side face", "2'-6\" / 7'-0\", R 22'", "p.304 B1 / B6 figures", "2.500 / 7.000 ft, R 22", "0", "MATCH")
r("B08", "B3, B5", "lane-change P.I.s", "4'-0\" from bent face / 3'-0\" from bulkhead face, R 22'", "p.304 B2+B3, B4+B5 figures", "4.000 / 11.000 ft from bent face, R 22", "0", "MATCH")
r("B09", "B2 (XX-7), B4 (XX-9)", "first P.I. then 5.00 deg crossing", "6'-6\" from bent face; P.I. 2'-6\" before the far face, 5.00 deg, 2 5/8\"", "p.304 B2 / B2,B3,B4 figures", "6.500 / 11.500 ft, 5.000 deg", "0", "MATCH")
r("B10", "B2 (XX-8)", "crossing P.I.s", "6'-4 5/8\" and 2'-6\" from far face, 10\" below the exit lane", "p.304 B2+B3 figure", "7.615 ft from bent face (= 6'-4 5/8\" from far face), 11.500 ft", "0", "MATCH", "plan self-consistent with 14'-0\" segments: 18\" - 7'-7 3/8\" x tan 5 deg = 10.0\"")
r("B11", "B2 (XX-9), B4 (XX-10)", "P.I. after the 5.00 deg entry, then straight in lane", "5'-5 1/8\" from far face (B2) / 8'-7\" from bent face (B4); no further P.I.", "p.304 B2,B3,B4 / B4+B5 figures", "8.573 / 8.583 ft OK; extra P.I. 3'-0\" from the far face (1.09-1.62 deg); entry P.I. deflects 6.27-6.97 deg (plan: 5.00 deg to lane-parallel)", "extra bend 1.1-1.6 deg; +1.3-2.0 deg at the P.I.", "DIFF", "generator: BlisterCtrl adds a ChangePiFromFace P.I. after a crossing; the 9\" at 5 deg lands ~1\" short of the narrowing row C lane")
r("B12", "B2, B4 (36 tendons)", "lane-change radius", "R = 22'-0\" (TYP.); note 3 minimum", "p.304, p.303 note 3", "19.95 / 20.53 / 20.74 ft (clamped; CONFLICT rows)", "-1.3 to -2.0 ft", "DIFF", "generator: FilletCtrl gives each curve only HALF of a leg shared with the next curve. R 22' needs T1+T2 = 52.0\" (B2 XX-7), 53.5\" (B4 XX-9), 19.8\" (B2 XX-9) on legs of 61.7 / 61.8 / 29.1\" - it fits")
r("B13", "B1-B7", "direction change at joint faces", "only minor (lanes follow the box)", "p.304 / p.285-296", "0.10-0.75 deg (soffit chord kinks; 0 relative to the lane)", "-", "MATCH")
r("B14", "B7", "lane changes", "no B7 figure; lane 2 throughout on the layouts", "p.290", "straight in C2", "-", "NOT GIVEN")
# ---------------- closure C1 / C2 spans 20-28 (p.298, p.290, p.304)
r("C01", "C1, C2 spans 20-28 (36 tendons)", "lane near the closure", "both in duct location 8 (9'-1 1/2\" from CL): p.290 plan lines on lane 8, text 'C1 | C2' on the lane-8 row", "p.290 (same on p.285-296)", "C1 lane 5 (12'-4 1/2\"), C2 lane 7 (10'-2 1/2\")", "C1 3'-3\", C2 1'-1\"", "DIFF", "model input (details-bonner-unit5.csx lanes 'B5' / 'B7')")
r("C02", "C1, C2", "anchor plan exit", "anchor 8'-10 1/2\" from CL to lane 8 at 9'-1 1/2\": 3\" in 5'-0\" (~2.9 deg)", "p.298 Section A-A, p.290", "C1 34.99 deg, C2 14.93 deg (curve deflection 36.2 / 18.8 deg, tangent 62.7\" / 31.8\")", "32 / 12 deg", "DIFF", "consequence of C01")
r("C03", "C1, C2", "anchor depth below deck top", "2'-7 1/4\" (A-A and elevation dimension to the anchor CL)", "p.298", "2'-5\" (flagged as scaled)", "2 1/4\"", "DIFF", "model input; the plan dimensions it")
r("C04", "C1, C2", "anchor from bent-side face", "3'-4\" (elevation chain 3'-4\" + 5'-0\" + 5'-8\")", "p.298", "3'-5\" (block bottom-corner chain)", "1\"", "DIFF", "model input")
r("C05", "C1, C2", "anchor to P.I.", "5'-0\" (dimensioned)", "p.298", "5'-0\" (P.I. at 8'-5\" vs plan 8'-4\" from bent face)", "1\" (from C04)", "DIFF")
r("C06", "C1, C2", "anchor vertical deviation", "2'-7 1/4\" -> 1'-4 1/4\" over 5'-0\" = 14.04 deg, R 16'-0\"", "p.298", "12.00 deg, R 16'", "2.04 deg", "DIFF", "from C03")
r("C07", "C1", "row change", "row B at the anchor, changes to row A in XX-11 (p.290 elevation P.I.s + dashed / solid plan lines; C2 row B per note 1 takes lane 8 row B)", "p.290", "row B throughout", "9\" vertical", "DIFF", "model input; row change not placed")
r("C08", "C1", "row-change profile", "7 1/4\" -> 1'-4 1/4\" between P.I.s 3'-3\" from each face, R 50'-0\"; drawn with row A on the 'TOWARD BENT' side", "p.304 TOP SLAB TENDON - ELEVATION", "not modeled", "-", "PLAN CONFLICT", "p.304 puts row A toward the bent; p.290 elevation / plan put row B at the anchor (bent) side; segment not named")
# ---------------- end spans 19 / 29
r("E01", "B1, B2 end spans (8)", "anchor on the end segment anchor face", "8'-0\" / 6'-6\" from CL, 1'-7\" above soffit", "p.272 Section", "8'-0\" / 6'-6\", 1'-7\"", "0", "MATCH")
r("E02", "B1, B2 end spans", "pipe vertical bend near the anchor", "18 deg, R = 10'-0\", P.I. 3'-6\" from the anchor (drop 1'-7\" -> row C ~5\")", "p.272 Section A-A", "no bend there: straight 7.67 deg down over the 8'-8\" end segment; combined bend 11.85 deg (B1) / 13.73 deg (B2), R 10', at the span face", "P.I. 5'-2\" off; 18 vs 11.8-13.7 deg", "DIFF", "model input / generator; the flag 'p.272 prints no bend position' is refuted - Section A-A dimensions the P.I. 3'-6\" and 18 deg")
r("E03", "B1, B2 end spans", "span-face lateral vs the lane at the first match-cast joint", "span face 8'-4 3/4\" (B1) / 7'-0\" (B2); joint-13 lanes C1 8'-9\", C2 8'-0\", C4 6'-6\"", "p.272 vs p.283 / p.285 / p.296", "6.45 deg (B1) / 8.13 deg (B2) plan corner at the closure-joint far face", "4 3/4\" / 6\" lateral inside the 3'-6\" closure", "PLAN CONFLICT", "no plan detail bends the duct between the p.272 span-face position and the lane hole; the model corner is the consequence")
r("E04", "B1 end spans", "lane-change P.I.s", "2'-6\" / 7'-0\" from bent face (figure titled 'SPAN 19 TO SPAN 29')", "p.304 B1 figure", "4'-0\" / 3'-0\" from the faces", "1'-6\" / 1'-0\"", "DIFF", "generator: EndSpanCtrl does not read LaneChangePis / FaceCrossings")
r("E05", "B2 end spans", "lane-change P.I.s", "4'-0\" / 3'-0\", R 22' ('B2 OR B5', TENDONS B2 - SPAN 19 AN SPAN 29)", "p.304", "4'-0\" / 3'-0\", R 22'", "0", "MATCH")
r("E06", "B3 end spans (8)", "profile", "start block in XX-13, anchor block XX-9 (p.297 block geometry applies)", "p.285, p.296, p.297", "straight duct (start block not built)", "6'-0\" / R 23' deviations missing at both ends", "DIFF", "model input (block table reads the XX-13 label as the end segment)")
r("E07", "C1, C2 end spans (8)", "anchor height on the end segment face", "6'-6\" above soffit (1'-7\" + 1'-6\" + 1'-6\" + 1'-11\" to the C anchor CL)", "p.272 Section", "straight duct at the row depth; anchor not modeled", "~5'-2\" vertical at the anchor", "DIFF", "the flag 'no height printed' is refuted")
r("E08", "C1, C2 end spans", "pipe bends", "P.I. 3'-10\" from the anchor face; C1 26 deg / C2 16 deg; R = 10'-0\" / 12'-0\"", "p.272 Section A-A", "not modeled (straight)", "-", "DIFF")
r("E09", "C1 end spans", "plan lane change", "3-lane S-curve, P.I.s 4'-0\" (toward bent 18 / 29) and 3'-0\", R 22'", "p.304 TOP SLAB TENDON - PLAN (C1 - SPAN 19 AND 29)", "not modeled", "-", "DIFF", "segment not named on p.304; p.285 / p.296 layouts would locate it")
r("G01", "all", "minimum radius (note 3)", ">= printed R", "p.303 note 3", "B2 / B4 19.95-20.74 ft < 22 ft (B12); all others = printed R", "-", "DIFF", "generator (B12)")

with open("C:/SBM/qc/bonner-unit5/pt-profiles/findings.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=list(R[0].keys())); w.writeheader(); w.writerows(R)
c = Counter(x["verdict"] for x in R); print(c)
md = ["# Bonner Unit 5 - agent review of the PT tendon bend profiles (2026-10-31)", "",
      "Model: `samples/plans/Bonner Unit 5 segmental.sbm` (a copy was regenerated read-only in `work/`, then deleted). Plans: B-2500 Revision 3 combined set, p.272, 283, 290, 297, 298, 303, 304.",
      "Method: `dump-tendons.csx` maps every longitudinal tendon (434 L/R) back to box coordinates (station, lateral, depth below the deck-top WP) through the generator's chord frames (residual 1e-10 m);",
      "`analyze2.py` recovers each arc (radius from the equal chords, tangent points, P.I. = tangent intersection, deflection in plan / elevation) and each corner; `families.py` aggregates per family (`work/families.txt`); `show2.py <tendon>` prints one tendon.",
      "Distances along a segment are from the bulkhead face (= face farther from the pier, user decision) or the bent-side face as stated. Tolerance: 1/4\", 0.1 deg, any radius.", "",
      "Verdicts: " + ", ".join(f"{k} {v}" for k, v in c.most_common()), "",
      "| id | family | item | plan (page) | model | difference | verdict | cause |", "|---|---|---|---|---|---|---|---|"]
for x in R:
    md.append(f"| {x['id']} | {x['family']} | {x['item']} | {x['plan']} ({x['page']}) | {x['model']} | {x['difference']} | **{x['verdict']}** | {x['cause']} |")
md += ["", "## Known items",
       "- 36 B2/B4 R 22' misfits: refuted as plan geometry. R 22' fits between the p.304 P.I.s; the generator's FilletCtrl gives each curve only half of a leg shared with the next curve (B12).",
       "- 8 end-span B1/B2 corners 6.45-8.33 deg: confirmed in the model; cause = p.272 span-face offsets that do not lie on the joint-13 lanes (plan conflict, E03). The 'exit not given' part is refuted: p.272 Section A-A gives the 18 deg / R 10' bend with its P.I. 3'-6\" from the anchor (E02).",
       "- B3 and C1/C2 end spans straight: confirmed (E06-E09); p.272 does print the C anchor height (6'-6\" above soffit) and the C pipe bends.",
       "- p.303 P1/P2 names swapped: confirmed (T04, names only).",
       "", "## New",
       "- C1/C2 (spans 20-28): wrong lanes (5 / 7 instead of lane 8, p.290) giving 35 / 15 deg plan kinks at the anchor; anchor depth 2'-5\" vs the dimensioned 2'-7 1/4\" (p.298); anchor 1\" off; C1 row B -> A change missing (C01-C08).",
       "- T13: the 3.07 deg diagonal rule is applied where p.303 draws a lane-parallel S-curve (T17).",
       "- B2/B4 after a 5 deg crossing: an extra P.I. 3'-0\" from the far face (B11). B1 end spans use 4'-0\"/3'-0\" instead of the B1 figure's 2'-6\"/7'-0\" (E04).",
       "", "Files: findings.csv, dump-tendons.csx, analyze.py, analyze2.py, show2.py, families.py, write_findings.py, crop.py / render.py (page images in img/), work/ (dumps, checks.tsv, diagnostics.txt)."]
open("C:/SBM/qc/bonner-unit5/pt-profiles/findings.md", "w", encoding="utf-8").write("\n".join(md) + "\n")
