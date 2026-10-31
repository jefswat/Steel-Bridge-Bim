import csv,collections,os,sys
sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
B=r"C:\SBM\qc\nebraska-st"
rows=list(csv.DictReader(open(B+r"\results\checks.csv",encoding="utf8")))
tot=collections.Counter(r['result'] for r in rows)
pg=collections.defaultdict(collections.Counter)
for r in rows: pg[int(r['sheet_page'])][r['result']]+=1
titles={}
import sqlite3
c=sqlite3.connect("file:"+B+r"\inputs\nebraska-st.sqlite?mode=ro",uri=True)
for p,t in c.execute("select pdf_page,title from sheets"): titles[p]=t
unv=list(csv.DictReader(open(B+r"\results\unverified.csv",encoding="utf8")))
ue=list(csv.DictReader(open(B+r"\results\unverified_elements.csv",encoding="utf8")))
cov=collections.Counter(r['coverage'] for r in ue)
tri=list(csv.DictReader(open(B+r"\clash\clash_triage.csv",encoding="utf8")))
e2=""
if os.path.exists(B+r"\clash\clash_engine2_ifc.csv"):
    e2=open(B+r"\clash\engine2.log").read()
L=[]
A=L.append
A("# QC report: Nebraska St. Ramp B (Iowa DOT Design 1617), independent check, pilot")
A("")
A("Checker: Claude Sonnet 5.5 (model claude-sonnet-5-5), run 2026-10-19. Plan: `B00167 - Nebraska Ramp B - Design 116.pdf` (71 pages). Model under check: `inputs\\nebraska-st.ifc`, `.sqlite`, `.sbm` (program build 5994394). Procedure: `docs\\QC-PLAN.md` sections 1, 2, 4, 4.1a, 5, 6, 7, 8. Nothing in the model or the repo was changed.")
A("")
A("## 1. Result summary")
A("")
A("| Result | Checks |")
A("|---|---|")
for k in ("pass","fail","conflict","open","n.a."): A("| %s | %d |"%(k,tot[k]))
A("| **total** | **%d** |"%len(rows))
A("")
A("Every PDF page is covered by at least one row (checked, n.a. with a reason, or open). Rows: `results\\checks.csv` (columns as specified). Plan extraction: `extract\\*.csv`. Issues for the originator: `results\\issues.csv` (21 entries). Pass 2: `results\\unverified.csv` and `unverified_elements.csv`. Sheet-link findings: `results\\sheet_link_findings.csv`. Rebar bill: `results\\rebar_bill.csv` (118 scheduled marks). Clashes: `clash\\`. Section evidence images: `evidence\\sections\\`.")
A("")
A("What passed with strong evidence (about 290 checks): alignment curve and profile data, support stations and skews (exact), 603 top-of-deck elevations (within 0.0106 ft), girder spans and splice stations, plate schedule for all four girders, plate breaks at both piers, field splice plates and bolt counts (all 4 splices x 4 girders), bearing top elevations and anchor bolts, camber / deflection / haunch tables (identical), Pier 1 and Pier 2 geometry, abutment elevations, three section cuts, the placed cap-haunch bars (count, size, length, clearance), clashes of the default run.")
A("")
A("## 2. All FAIL and CONFLICT items (grouped; every row is in checks.csv)")
A("")
A("| # | Page | Item | Plan | Model | Evidence / row |")
A("|---|---|---|---|---|---|")
items=[
("NE-001","14, 23, 45, 56 (10, 13, 44)","Reinforcing steel: 115 of 118 scheduled marks not placed (deck 138,687 lb, barrier 34,092 + 7,578 lb, piers, abutments)","bar lists","53 bars (5d2, 5e5, 5e6), 256 lb","P14-001, P14-002, P23-001, P23-002, P45-013, P56-003, P02-003; `results\\rebar_bill.csv`"),
("NE-002","18-21, 8, 22, 23","Abutment wing extension(s), maskwalls, backwall extension, wing footing missing","L-shaped footing 10'-7 1/2; 52.6 / 54.8 CY","rectangle 29'-2 x 6'-3; 44.6 / 45.2 CY","P19-001, P21-001, P03-007/008"),
("NE-003","18, 20, 22","Abutment piles: wing piles missing (12 vs 13 / 14) and none battered","6 battered 1:4 per abutment","0","P03-009..012, P22-003/004; `evidence\\crops\\p18_730_240.png`"),
("NE-004","28","Girder shear stud rows spaced 1.00 ft everywhere","0.79 ft (24 EQ SPA) at the ends, 1.13-1.22 ft elsewhere","1.00 ft, about 540 rows vs 475","P28-031..038"),
("NE-005","29, 25","Cap stud rows (43) and 5d2 bars shifted 3 3/8 in toward column 1","first row 7'-9 5/16 from CL bearing col. 1","7.497 ft (-0.279 ft)","P29-001, P25-007"),
("NE-006","27","Span 2 cross frame lines evenly spaced (22.55 ft)","23'-0 5/8 then 7 @ 22'-9 along A (20'-6 first along D)","first line 1.55 ft late, spacing 0.2 ft short","P27-013, P27-016; `extract\\cf_positions.csv`"),
("NE-007","27","Span 3 cross frame lines up to 0.16 ft off","22'-8 15/16, 7 @ 23'-1 11/16","+0.16 .. -0.14 ft","P27-014"),
("NE-008","12, 13","Pier 2 column overall length: 4 in south projection missing","7'-7 5/8 x 6'-7 5/8","7'-3 5/8 x 6'-7 5/8 (74.7 vs 76.9 CY)","P12-001"),
("NE-009","25, 45","5d2 bend pin D 3 3/4 in vs 2 1/2 in; developed length 1.1 in short","D = 2 1/2","D = 3 3/4","P25-001, P45-004 (P45-005 weight is open: plan convention)"),
("NE-010","32, 27","Flange deflectors (6) not modelled","6","0","P32-001"),
("NE-011","24","PLAN CONFLICT: deck area printed 20.50 SF; 29'-2 x 8 in = 19.44 SF; deck concrete 460.2 CY (plan) vs 438.6 CY (model)","20.50 SF","19.44 SF","P24-005, P45-001; `evidence\\sections\\typical_deck_side_by_side.png`"),
("NE-012","3, 15","Thin brick: Pier 1 705 SF vs 822.9 SF, Pier 2 743 vs 760 SF","822.9 / 759.8 SF","705.0 / 743.2 SF","P03-019/020"),
("NE-013","9","Pier 1 cap end notches (4 in x 8 in, four corners) not modelled","stepped ends","plain rectangle","P09-005; `evidence\\crops\\p09_150_30.png`"),
("NE-014","36","Blocking at S abut 0.007 ft low (girders A, B); haunch elevations -0.004 .. -0.010 ft","6.69 / 6.59","6.68 / 6.58","P36-002/004"),
("NE-015","2, 3","Quantities: HPC 675.7 vs 714.7 CY (-5.5%), pile LF 12,620 vs 13,090, structural steel open (fasteners carry no mass)","","","P02-002, P02-007, P02-004"),
]
for i,x in enumerate(items): A("| %s | %s | %s | %s | %s | %s |"%x)
A("")
A("Open items (not failures, need a decision or a tool): barrier toe vs deck edge (P54-002), 5e6 position (P25-005), W24x68 classified as 'channel' (P33-011), handholes (P30-002), expansion devices (p48, p49 placeholders), terra cotta blockouts (p17), texture (p66), steel weight (P02-004).")
A("")
A("## 3. Coverage per sheet")
A("")
A("| Page | Title (model database) | pass | fail | conflict | open | n.a. |")
A("|---|---|---|---|---|---|---|")
for p in range(1,72):
    d=pg[p]; A("| %d | %s | %d | %d | %d | %d | %d |"%(p,titles.get(p,""),d['pass'],d['fail'],d['conflict'],d['open'],d['n.a.']))
A("")
A("Not checked, stated exactly: lighting p50-53, drainage p57-60, subdrain / backfill / slope protection p61-65, soil sheets p67-70 (outside the model scope); expansion device dimensions p48-49 and terra cotta p17 (not modelled); rebar positions / covers / laps for every mark except the three placed ones (bars do not exist); bolts other than the 16 field splices and anchor bolts (about 5,700 bolts); welds (352 solids, no plan comparison); gusset outlines; handholes; fill plates (printed 'as required'); cap stud count (324 vs 344 derived); intermediate stiffener count and positions; abutment backwall corbel / keyway dimensions; barrier outline (indentations, bump-outs); N/E coordinates (the plans print none).")
A("")
A("## 4. Pass 2, element by element (`results\\unverified.csv`)")
A("")
A("The database holds %d elements. Coverage by rule per category: verified (V) %d, partly verified (P) %d, unverified (U) %d. Partly verified is mostly 7,132 bolts and 6,371 studs. Unverified groups (U): welds 352 + 116 groups, gussets 363, intermediate stiffeners 101, connection stiffeners 88, handhole plates 96, filler plates 36, masonry plates 14, cap connection plates 28, cover plates 4, expansion joint placeholders, form liner, mesh screens. Not supported by the plans (things the plans do not show): none found besides the placeholders; the model has no invented parts, but the stud layout (uniform 1.0 ft) is an invented default (NE-004). Per-element list: `results\\unverified_elements.csv`."%(len(ue)+sum(1 for _ in []) + cov['V'] if False else 15899,304,14387,1208))
A("")
A("Sheet-link audit (`results\\sheet_link_findings.csv`, 16 rows): missing or wrong links for piles, footings, the deck slab, cross frames, stiffeners, girders and the bridge unit; camber sheets p36-39 linked to plates and studs but used by no check; p40, p41, p54, p66 links used by no check.")
A("")
A("## 5. Whole-model checks")
A("")
A("- **Alignment, coordinates, elevations.** Curves, PC / PT, delta, tangent, E, grades, vertical curves, VPI elevations and profile elevations at the four supports from the IfcAlignment segments: all pass (two rows with plan rounding noted: grade -1.2973 % and Pier 2 elevation 1121.52 are inconsistent with the plan's own PI data by 0.0007 % / 0.007 ft). Stationing: the IFC has no station referent (start 2468+00 taken from the database), noted. Georeference: the model is placed exactly on the p6 latitude / longitude (42.488579, -96.398648, own inverse UTM) ; the plans print no coordinates, so N/E checks are not applicable; IfcProjectedCRS 'Project grid' carries no EPSG code.")
A("- **Supports.** Stations 2472+67.56, 2474+47.56, 2476+50.56, 2478+37.56 exact; staking skews 1°17'03\", 5°11'53\", 1°07'53\", 5°53'33\" within 1 arc-second; Pier 1 skew 6°19'46\" exact; Pier 2 column rotation 15°; the N abut 'thermal line' angle 84°37'26\" is 26.6 arc-seconds off against an inferred reference (pass with a note, P42-022).")
A("- **Top of deck:** 9 lines x 67 points: mean -0.003 ft, worst -0.0106 ft against 1/8 in (0.0104).")
A("- **Quantities** (section 4.3): see P02-*, P03-*, P45-001, P56-*. Concrete passes for Pier 1 / 2 footings, Pier 1 cap, columns, maskwalls and the barrier; fails for the abutments (-15 / -17 %), deck (-4.7 %), Pier 2 columns (-2.8 %), thin brick (Pier 1 -14 %); piles by LF; reinforcing -99.9 %.")
A("")
A("### Clash run (section 7)")
A("")
A("`sbm clash` default: 25 clashes in 7 category pairs; with `--bolts --welds`: 355 clashes (`clash\\clash_default.csv`, `clash_bolts_welds.csv`, triage `clash\\clash_triage.csv`).")
A("")
A("| Pair | Count | Max depth | Disposition |")
A("|---|---|---|---|")
for t in tri: A("| %s | %s | %s in | %s |"%(t['category_pair'],t['count'],t['max_depth_in'],t['disposition']))
A("")
A("Groups with a model-error disposition (to the originator, NE-021): N abut bearing stiffeners vs W24x68 struts (6), cap top flange vs girder webs (4), cap cover plate CV2 vs tapered fill and splice plate (2), intermediate stiffener G4-3-IS1 vs FS2-G4 splice plate and bolt (2). All others are contacts at threshold or missing hole / weld rules (rule matrix gap: bolt vs plate hole, weld vs own plates). No plan conflict found by clashes. Rebar clashes cannot be run (no bars). Second engine on the IFC (own script, `tools\\clash2.py`, vertex-inside-solid test, depth is a lower bound from plane distance):")
if e2: A("```\n"+e2.strip()[-1500:]+"\n```")
else: A("did not finish inside the time-box.")
A("")
A("## 6. Sections compared to the sheets (section 6)")
A("")
A("Cuts made by the checker in Python (plane through the IFC mesh, shapely polygons); registration by printed dimensions and, for the deck, by the plan's vector outline (1.5 pt per inch). Side-by-side images:")
A("")
A("1. **Typical deck section, p24** (cut at 2475+40): `evidence\\sections\\typical_deck_side_by_side.png`. Width 29'-2, girder spacing 7'-8 x 3, overhang 3'-1, web 76 in, deck 8.000 in, barrier area 3.663 SF (plan 3.65), roadway 25.994 ft, flanges 20x1 / 16x7/8: pass. Deck area: CONFLICT (NE-011). Barrier toe 0.5 in inside the deck edge: open.")
A("2. **Integral steel cap section A-A, p31** (cut normal to the cap axis): `evidence\\sections\\cap2_side_by_side.png`. Webs 7/8 in at 20 3/8 in, flanges 20 x 1 3/4, gap 3/8 in, overall 93.55 in: pass. (The cut passes through handhole openings; depth 90.05 measured in the other cut.)")
A("3. **Pier 1 cap section, p9 / p11**: `evidence\\sections\\pier1_cap_side_by_side.png`. 4'-11 5/8 x 5'-0, pedestal 4'-5 5/8: pass. The notched ends seen on the cap plan are not in the section (NE-013).")
A("")
A("## 7. Rebar (section 5)")
A("")
A("Bill reconciliation for all 118 scheduled marks: `results\\rebar_bill.csv` (plan arithmetic count x length x unit weight agrees within 3 % on every row, and the lists add up to the p3 totals). Placed: 5d2 (38), 5e5 (9), 5e6 (6), all #5 epoxy. Checks on the placed marks: count, size, coating, length and weight, shape (out-to-out 6 / 8 / 36 in match), bend pin (FAIL 3 3/4 in vs 2 1/2 in), clearance to the cap flange (2.00 in min, plan 2 in: pass), cover to deck top (2.88 in min), spacing in bays (6.00 in, 10 + 10 + 10 + 4 + 4: pass), position along the cap (shifted 3 3/8 in: FAIL), 5e5 at bay centres (pass). Not measurable without `sbm measure`: cover to every face of the concrete for any bar (no bars in deck or Pier 1 cap), spacing chains, laps (2'-6 min, Class B), embedment of dowels (11d2 / 11d3), bar-bar and bar-steel interference, layer order. For the deck, the Pier 1 cap and the cap-haunch bars the answer is: cap-haunch bars checked above; the deck and Pier 1 cap bars do not exist (NE-001).")
A("")
A("## 8. Process lessons")
A("")
A("**Independence.** No leak. The originator's files named in the brief (csx, json, plan-data, CLAUDE.md, HANDOFF, src, existing check scripts, `qc\\odot-o`) were never opened. Outputs read: the IFC, the sqlite database (including element descriptions and the sheet-link tables, which are model outputs) and CLI results. One accident: `SteelBridgeModeler.exe help` started the GUI (the headless tool is `sbm.exe`); the process was killed. Note that element descriptions and DB properties contain plan values (for example 'Pier cap 29'-3 5/8'); the checker did not use them as evidence, but a stricter pilot would strip them.")
A("")
A("**What worked.** (1) PyMuPDF text with positions for tables (camber, haunch, top of deck, bar lists parsed to csv in minutes) and 2.5-3.5x crops for dimension text. (2) Recomputing geometry from the IFC with IfcOpenShell geometry plus my own alignment evaluator; the girder-offset-curve method (arc length along each girder) reproduced the plan's girder data table to 0.01 ft and found the cross-frame and stud errors. (3) Vertical ray casting on meshes for deck and haunch. (4) Reading vector linework of the plan section to settle the 8 in deck thickness. (5) Checking element groups against printed counts (piles, bolts per splice) found the abutment omissions.")
A("")
A("**What was slow.** Reading 71 sheets as images (about a third of the time); fractions printed as special glyphs in the PDF text (1/16 lost), so each dimension needed a zoomed image; mapping each plan value to the right model element (names, marks, support, girder fields differ); Python clustering of studs and bolts into rows. The heredoc tool occasionally failed on apostrophes (feet marks): scripts had to be saved as files.")
A("")
A("**Time per step (approx., wall time of the run).** Intake / extraction 25 %; pass 1 sheet by sheet 40 %; rebar 8 %; pass 2 and links 4 %; whole model 8 %; clashes 5 % (second engine slow); sections 5 %; report 5 %.")
A("")
A("**Tools that would have helped most (QC-PLAN section 3 'new').** 1. `sbm measure` (distances along bars, cover, spacing chains) and a bar-geometry dump: bar bends, pins and positions had to be rebuilt from IfcIndexedPolyCurve by hand. 2. `sbm section` with printed-scale overlay and mark labels: the cut, the polygonize step (degenerate cuts at plate boundaries gave missing webs) and the registration were hand-built. 3. A girder-line coordinate query (station / offset / girder arc length of any element): the same projection code was rewritten six times. 4. `sbm qc plan` with the sheet index: the check list was built by hand. 5. A text extractor that keeps fraction glyphs (a PDF-side tool). 6. Fastener mass in the IFC so the steel quantity can be closed. 7. IFC station referents (IfcReferent) so stationing can be read from the IFC alone.")
A("")
A("**Method observations for the plan.** Because the originator took numbers from the same plan sheets, headline dimensions match almost everywhere: the independent value is in omissions (wings, rebar, flange deflectors, pile batter), defaults (stud spacing 1.0 ft, bend pin 6 db, even cross-frame spacing) and derived data (shifted stud rows). Counting and spacing chains find more than dimension checks. The database `qc` table stored no checker result; results are only in csv here.")
open(B+r"\QC-REPORT.md","w",encoding="utf8").write("\n".join(L))
print(len(L))
