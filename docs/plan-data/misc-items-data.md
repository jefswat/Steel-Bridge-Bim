# Misc items data: Ramp G deck drainage, I-80 sign supports, crash cushion end posts, lighting

Read-only extraction from the plan PDFs (every value below was confirmed on a rendered clip; fraction-valued dims were read
from the image, not the text layer). PDF page = 1-based page of the named PDF. "I-80" = `B00168 ... Design 116.pdf`, "Ramp G" =
`B00169 ... Design 316.pdf`. Tables as CSV in `docs/plan-data/misc/`. Standing rule: nothing is assumed; gaps are marked UNRESOLVED.
Corrections to `misc-items-data-check.md` are listed at the end.

Existing model: `DeckDrainDefinition` (Station, Left, FromGutter, Type, Outlet FreeFall|PipedToPier, SplashBasin, InletLength 24" x InletWidth 12",
PipeDiameter 8", DropBelowGirders) in `BridgeDefinition.Drains`; I-80 drains are entered, Ramp G has none.

---------------------------------------------------------------------------------------------------------------------------------

## (a) Ramp G deck drainage (Ramp G PDF 154-161 = design sheets 152-159)

### a1. Table of deck drains (PDF 160): `misc/ramp-g-deck-drains.csv` (25 rows, confirmed against the image)
Columns UNIT, LOCATION (pier + AHD/BACK = ahead/back of the pier line), STATION (Ramp G), CROSS SLOPE %, ANGLE "A", OFFSET FROM CL RAMP G
(LEFT/RIGHT only; no distance printed -> UNRESOLVED numeric offset, PDF 160), OUTLET, SPLASH BASIN. Footnotes: rows 22-25 "PIPED TO GROUND AT PIER 19G,
see collection pipe elevation sheet 157"; "for location of angle A see sheet 159". Totals on the sheet: 25 drains, 19 splash basins, 3 erosion stone
mattresses (Piers 5G, 9G, 14G). Angle A is the trench-floor/tube-end angle on section C-C (PDF 161), equal to the cross slope in the table (3d25' = 5.90%); the tube itself is vertical
("DRAIN TUBE IS TO BE VERTICAL", PDF 161).
Outlet: free fall (21), piped (4: left side, Piers 18G/19G). Splash basin (PDF 160): 10'-0 x 10'-0 plan, 2'-0 deep erosion stone (or Class E revetment) on
engineering fabric, centered directly under the drain, side slope 4:1 (typical section for existing grades) / berm 10'-0 with 3'-6 (typical section for berm slopes).

### a2. Inlet (trench drain, PDF 161, Std. Sheet 1054 modified)
- Placement: trench crosses the deck transversely; starts 1/2" clear of the gutter line (barrier face) and runs 4'-0 toward the deck (Part Plan at Drain; section A-A).
  Plan dims (Plan View of Drain Trench): 9 / EQ / EQ / 9 = 4'-0 along the trench (rod-strap spacing: 9", 15", 15", 9"); 3 +/- from the trench edge to the first strap hole.
- Width (along the roadway, section B-B): 10 +/- 1/2" clear between side plates (7 1/4" tube clear, 3" shoulders each side, 3/8" plates + 1/8" gaps);
  grate panel 2'-0 +/- 1" x 10 +/- 2" (two panels = the 4'-0, "Drain trench grate details"; 12 openings, pattern "similar" to the sketch).
- Depth: grate 1 1/2" ("*" depth by grate manufacturer); trench side walls 3 1/2" at the low end and 4 1/4" at the tube end in side view D-D; section C-C right end wall 6" with a 2" rise
  to the tube; floor slopes down toward the tube at angle A ("slope will vary based on depth of trench grate"). L2x2x3/8 trim angle around the opening (trimmed to the grate depth).
- Tube: 8 dia x 3/8 structural tube (or 8x8x3/8 square tube) , 7 1/4" clear, vertical, free-fall outlet; tube center 3'-0 from the barrier-side end of the trench
  (section C-C: 8" from the far end wall to the tube face, tube OD 8") i.e. tube center = 3'-0 1/2" from the gutter line. For the 4 piped drains the tube center is 1'-8 from CL girder A
  (Ramp G PDF 159 sections A-A/B-B, "1'-8"). Tube bottom for free fall: tube hangs to at least 1'-0 below the bottom of the girder web connection (PDF 161 section A-A "1'-0 MIN."),
  connected to the girder web by L5x3x1/4 x 0'-8 bolted to the girder (7/8 dia bolts, 4" min / 3 / 5 dims), welded 7 x 1/4 plate on the tube. A 9" drop from the deck underside to the
  pipe flange for the piped type (section B-B PDF 159).
- Mounting: 1/2" dia threaded rods with 3 hex nuts through 2 x 3/8 x 1'-2 bent bars (3 at 4'-0 spacing in plan), grate tie-downs 3/8 dia flat-head cap screws (Detail A) or hex head bolts (Alternate A),
  5a4 bars below top mat at 45 deg around the blockout (1'-6 +/- long, 2" clear), concrete blockout area with 45 deg haunch. Drain assembly 3/8 plate, galvanized; grates ferrous casting ASTM A48 Class 35B.
- Bottom transverse bar clearance: drain centered over the nearest bottom transverse bar (PDF 161 notes), bars cut with 1" clearance, longitudinal bars cut with 2" clearance.

### a3. Pier collection system (Piers 5G, 9G, 14G, 19G; PDF 154-159)
- Where: the deck joint over each of these piers (CL pier = CL expansion joint, 8" slab). A 1/4" reinforced neoprene trough runs in a U between each pair of girders (section B-B, PDF 156)
  under the joint, bolted with 1/2" threaded concrete inserts + 1/2" galvanized bolts at 1'-6 max (Detail A: 3 x 1/2 x 4 galvanized straps), 4" min lap (1'-0 lap, 2 offset). Slope 5.9% min
  (Piers 5G/9G/14G) and 4.9% min (Pier 19G) toward the collection end (girder D side at 5G/9G/14G, girder A side at 19G, as drawn on PDF 154); the 1'-8 shown below the deck at the collection end (PDF 154 elevations) is measured from the deck underside to the TOP OF THE TROUGH COLLECTION BOX (the dimension's lower arrow is on the box top).
- Trough collection box (PDF 156): 3/8 plate, 2'-2 long x 1'-0 wide, 1'-4 deep at the high end, bottom sloping 3" to 1'-7 at the low end, round-edged R = 1'-1 1/2 cut for the trough in the 2'-2 faces (centre 6 1/2 above),
  so the 2'-2 side runs ACROSS the joint (along the bridge; PDF 155 section A-A shows it centred on CL pier) and the low end with the pipe faces the pier collection box (back),
  8 dia drain pipe welded on the low side (CL pipe 1 1/2 above the low floor). Location (PDF 154): Pier 5G/9G/14G collection box 3'-6 from the roadway edge on the girder D side;
  Pier 19G: 4'-0 from the left end of the pier (measured along CL pier), 38'-10 15/16 overall, partial plan: 4'-9 from CL pier to the back bearing line side, 4'-0 from CL girder A.
- 8 dia pipe (ASTM A53 Gr B Sch 40) from the trough box: slopes down at 1 in 12 min (PDF 155), turns vertical 9" (at 50 F; expansion allowance) off the cap face and ends in the pier collection box; drain pipe
  flange: 1/2 plate + 1/2 neoprene gasket, A = 11", B = 1'-2 (8") / A = 1'-3, B = 1'-6 (12"), 4 holes 1" dia for 7/8 bolts, 1 1/2" rim (PDF 158).
- Pier collection box (PDF 157): steel 3/8 plate box HUNG ON THE BACK (down-station) FACE OF THE CAP (straps P3/8x4 on 1/2" stud anchors into the face; PDF 155 sections A-A / C-C and the PDF 157 elevations looking ahead show it on the back face),
  1'-4 along the face, 1'-6 out from the face at the top / 1'-4 at the bottom (outer wall tapers over the lower 1'-0), 2'-0 deep; inlet pipe CL 9" (at 50 F) off the face and 4" into the box; outlet pipe 1 1/2" clear of the face; 1'-6 wide cover (cover table PDF 158: 8" pipe A 1'-8, B 1'-6, C 8 5/8;
  12" pipe A 1'-6, B 1'-6, C 12 3/4), anchored with 1/2 dia concrete studs/slots, P 3/8 x 4 straps; top 1/4" min / 1" max below the top of cap (glyph confirmed on the raster; section A-A: 1" at CL box).
- Pier drain pipe (PDF 157): at Piers 5G/9G/14G the collection box is 12'-6 from CL pier along the cap (pier 9G looking ahead), 8 dia pipe from the box bottom, bend, then 8% slope min ALONG THE CAP FACE toward the CL pier
  (cleanouts at both bends), then vertical down the pier face with pipe supports (steel clamps on 3/4 dia rods into concrete inserts) every 6'-0 max, outlet 1'-0 +/- above a
  4'-0 x 4'-0 x 1'-0 erosion stone mattress (45 deg elbow, 1'-0 leg). At Pier 19G the collection box is 18'-3 7/8 from CL column, 12 dia pipe at 8% min, vertical with supports at 4'-0 max,
  ending 1" clear above the pavement (collar 3/8 plate, 18 dia pipe sleeve; top of pavement elev 981 +/-; "for continuation see roadway plans").
- Cleanout: 4 dia raised hollow-square-head brass cleanout in a tapped hole in the pipe/cover.
- Hanger support (PDF 158): 5a5 bars through each concrete insert, 3/4 coil/B-line insert, 3/4 turnbuckle + 3/4 threaded rod + 3/4 eye nut + galvanized steel pipe clamp; length varies.

### a4. Span 19G deck drain collection pipe (PDF 159 = design 157): `misc/ramp-g-collection-pipe-span19G.csv`
- 12 dia pipe, theoretically on R = 1547.17' (parallels the curved bridge; fabricator may use chords between flexible couplers), slope 1.78% printed, running ahead along girder A,
  CL pipe 1'-8 right of CL girder A in plan (sections A-A/B-B), under the deck between girder A and B, with L3x3x3/8 support angles on 5/8 U-bolts (double nuts) at Type 1 cross frames
  (no angle at Type 2 cross frames); hangers 5'-0 max along the pipe. The 4 drain tubes drop 8 dia pipe (flange 9" below deck) into the 12" pipe through a 45 deg tee/cleanout.
- Flexible couplers (Dresser Style 38 / Aqualok Superfit / E.J. Prescott), 4 deg angular deflection, about 6" wide: 4'-0 upstream of drains 23 and 24, 2'-0 upstream of drain 25.
- End at Pier 19G: elbow down to the pier collection box (12" pipe). Trough collection box drain pipe (8") joins from the trough at the pier (PDF 154).
- Vertical dims from deck top at CL drain pipe to CL 12" pipe: table CSV. Note "dims may vary if fabricated in linear segments".
- Not printed: cross-frame stations along span 19G (take from the framing), drain-pipe tee geometry.

### a5. Inputs a builder needs (proposed)
Extend `DeckDrainDefinition` (all values above are printed unless marked):
- `InletOrientation` = Transverse; `InletAcrossDeck` 4'-0 (inlet length across), `InletAlongRoad` 10" (trench clear; 10 1/2 plus plates), `InletStartFromGutter` 1/2", `TubeFromGutter` 3'-0 1/2",
  `TubeDiameter` 8", `TubeWall` 3/8", `TrenchDepthHigh/Low` (3 1/2 / 4 1/4), `CrossSlopeAngle` (from the table or the deck), `GrateThickness` 1 1/2",
  `Outlet` = FreeFall | PipedToCollector, `SplashBasin` (10' x 10' x 2'), `CollectorPipeId`.
- New `DrainCollectorDefinition` list on `BridgeDefinition`: Name, Pipe dia (12"), Start/End station, offset from reference girder (1'-8 from CL girder A), vertical-from-deck table or slope (1.78%),
  hanger spacing 5'-0, coupler spacing/stations, end pier + box.
- New `PierDrainDefinition` on `SupportDefinition`: pier name, trough slope 5.9%/4.9%, trough collection box position, collection box position along the cap (12'-6 / 18'-3 7/8),
  pipe dia (8"/12"), vertical pipe support spacing (6'-0 / 4'-0), outlet type (erosion stone mattress 4'-0 x 4'-0 x 1'-0 / pavement).
Model elements and IFC classes: inlet trench = `IfcBuildingElementProxy` (PredefinedType USERDEFINED "DECK_DRAIN") or `IfcDistributionFlowTerminal` (WASTETERMINAL/FLOORTRAP); tube/pipes = `IfcPipeSegment`
(+ `IfcPipeFitting` for elbows/tees/flexible couplers JUNCTION/CONNECTOR, cleanouts `IfcPipeFitting` or `IfcDistributionFlowTerminal`); trough = `IfcBuildingElementProxy` (neoprene);
collection boxes = `IfcTank`/`IfcBuildingElementProxy` plates; hangers/clamps = `IfcDiscreteAccessory` / `IfcMechanicalFastener`; splash basin and stone mattress = `IfcBuildingElementProxy` (EROSION_STONE) or `IfcEarthworksFill`.

UNRESOLVED (Ramp G PDF 160/161): numeric offset of each drain from CL Ramp G (only LEFT/RIGHT); trench "end wall" depths conflict slightly between side view D-D and section C-C (kept both).

---------------------------------------------------------------------------------------------------------------------------------

## (b) I-80 sign supports and exit sign bases (I-80 PDF 176/178/180/181/182/183 = design 171A/172A/173A/174/175/176)

Voided sheets 171/172/173 (PDF 175/177/179) are not used. Revisions of 12-02-15 on sheets 171A-175 changed the location of the sign truss and details (cloud/strike-through on 174, 175).

### b1. Overhead sign truss support brackets (SOST-II 100-ft truss modified; truss, posts, panels by others)
Locations (location plans, PDF 176 and 180; reconciled with the pier stations on PDF 10 and 12):
- Unit 2 at Pier 7WB (CL pier 7595+76.39, 41.08' LT.): truss CL parallel to pier at Sta 7595+82.22, 41.08' LT. = pier + 5'-10 (2'-2 + 3'-8); posts at girders A (north) and K (south) 100'-0 apart;
  post lines 7'-4 apart, 2'-2 and 9'-6 ahead of CL pier (plan PDF 110 "2'-2 (TYP.)" at the pier).
- Unit 3 at Pier 13WB (CL pier 7606+94.77): truss parallel to the cross frames (skew 89d54'39"), Sta 7607+12.18, 41.08' LT.; posts at girders A and J, 90'-0 apart; post lines 7'-4 apart (7'-4 1/8 on curved girder A);
  at girder J 2'-0 and 9'-4 ahead of the pier; at girder A the dim 12'-10 7/8 starts from a vertical reference line whose meaning is UNRESOLVED (PDF 117).
- The station printed in the title block (7595+67.24, 41.08' LT.) is the bridge reference, not a sign location.
Bracket (one per post, 4 posts per truss; see `misc/sign-bracket-dims.csv`): welded H-shape stub built of 3/4 web plate between two MC18x45.8 (2'-2 x 2'-4 plan; web and stiffeners vertical in the final condition,
flanges follow the girder slope), 1 7/8 x 28 top plate carrying 8 x 1 1/2" dia anchor bolts (F1554 Gr 105, 6 nuts + 4 washers + DTI each) on a 1'-8 dia circle (pattern 22d30'); 1 7/8 x 12 3/4
anchor plates (level) each side; stiffeners; WT5x22.5 diagonal and horizontal bracing (details C-F); stub bolted to the girder web/flanges by a field-splice-type connection
(3/8 x 13 web splice each side, 5/8 flange splices with fills, detail G bolt pattern: top flange 2 @ 4" each side + 2" edge; bottom 2 @ 3"; row pitch 5"; 1 1/2" end distance; 1/4" gaps; detail A R = 2" re-entrant);
bearing/jacking stiffener lines at the pier and cross frames noted. Bracket height 8'-6 (U2) / 6'-4 (U3). HSS 14.000x0.500 post and 2'-2 dia x 2" base plate are "not part of this design" (placeholders);
base-plate nuts by others in a future contract. Elevation data: top of base 1026.33 N / 1028.33 S (U2), 1020.32 N / 1024.32 S (U3).
Unit 3 girder A section D-D (PDF 181) was revised: 5'-4 5/8 (was 5'-3 15/16) and 5'-9 1/2 (was 5'-8 1/4); angles 93d19'39" and 93d00'30" (old values struck; digits of the struck angles UNRESOLVED).
UNRESOLVED: which datum the "1'-3 3/4 ... " vertical dims (elevation line to level line) refer to, beyond "top of base elevation to the level line at the post, W/E posts" (PDF 178/180); bolt lengths (not printed).

2026-10-11 CORRECTION (PDF 178 Sections C-C / D-D / View J-J, PDF 181 revised D-D): Section D-D is a PLAN view - the bracket is a plate-girder cantilever
from the exterior girder (W.P. on CL girder) OUTBOARD to the post: CL girder to CL post 6'-3 (W) / 6'-7 7/16 (E) girder A at 92d52'02" to the girder (TYP.), 6'-1 3/4 at 90d
girder K, Unit 3 girder J 5'-0 3/8 at 89d49'28" (W) / 5'-0 1/4 at 90d (E); bracket ends 2'-3 beyond the CL post; the 8'-6 / 6'-4 "height" is the bracket web (= girder web);
the J-J "elevation line to level line" dims (1'-3 3/4 etc.) run from the top of base down to the top of the bracket 1 7/8 x 26 top flange. Model: `PostOutboard`,
`AngleToGirderDegrees`, `StubHeight` (signs-i80.csx).

### b2. Exit sign support bases (PDF 183 = design 176; Ramp A gore at Pier 6A / 5WB and Ramp G gore at Pier 11WB)
- Locations: north barrier at the gore, bracket on the north exterior girder ("GIRDER A OR F" on sheet 104 label; U2 "A OR K" sheet 108). Exact station/offset NOT printed (note says see sheets 104 and 108, which show only a tick
  on the north girder). Scaled from the plans: Unit 1 at the cross-frame line 50'-0 back of CL bearing Pier 5WB/6A (cross frames 25'-0 apart on the girder, tick on the second frame);
  Unit 2 about 60' back of Pier 11WB (3rd frame, scaled) UNRESOLVED. Plan (PDF 9, 11) shows the sign support on the north edge of the deck beside Ramp A2 / Ramp G with "(SEE ROADWAY PLANS)".
- Base (each, 2 bases): concrete block 2'-0 (along the barrier) x 6'-2 (outboard of the barrier face) x 2'-6 high on a steel outrigger bracket cantilevered from girder A
  (1 x 24 top flange plate, R = 6" sloping bottom plate, 11/16 web, 5/8 x 10 stiffener; bracket depth 2'-0; girder A web 8'-2 high with 19 spa @ 5 = 7'-11 splice rows); the 4'-0 x 1'-5 barrier blockout (allowable blockout limit 4'-0).
  Two W6x15 stub posts 3'-0 apart (1'-1 1/2 from the outboard end, 2'-0 1/2 from the barrier face), embed 2'-2 1/2, stub height 2'-6 + 3 1/2" projection (reference point), stub posts plumb; 7/8 dia x 6" end-welded studs;
  2 construction joints at the barrier, keyway 2x4. Concrete 1.6 cu yd each (3.2 total). Rebar: `misc/pole-base-exit-sign-rebar.csv` (189 lb epoxy each, 378 total).
- Placeholder: sign panels/posts above the stubs ("not part of this design").

### b3. Inputs a builder needs (proposed)
`SignSupportDefinition` list on `BridgeDefinition`: Name, Kind (OverheadTrussBracket | ExitSignBase), Support name + distance ahead of CL pier (2'-2 / 2'-0), Girders (A,K | A,J), PostSpacing 7'-4,
TrussSpan 100'-0/90'-0 (placeholder truss envelope), TopOfBase elevation per post, BracketHeight, section dims (D-D) per girder, plate/shape sizes, AnchorBoltGroup {count 8, dia 1 1/2, circle 1'-8, start angle 22.5 deg}, base plate 2'-2 dia x 2" (placeholder), HSS post 14.000x0.500 (placeholder).
Exit sign base: {Name, Support/pier, girder, distance back from CL bearing along girder (UNRESOLVED), block 2'-0 x 6'-2 x 2'-6, stub post positions, rebar list}.
Elements (IFC): bracket plates/shapes = `IfcPlate`/`IfcMember` in an `IfcElementAssembly`; anchor bolts = `IfcMechanicalFastener` ANCHORBOLT; placeholder post/base plate = `IfcBuildingElementProxy` (flag by others);
exit sign base = `IfcFooting` (or `IfcBuildingElementPart` aggregated to the deck) + `IfcReinforcingBar`; stub posts `IfcMember` COLUMN? use `IfcColumn` USERDEFINED; sign truss = `IfcBuildingElementProxy` envelope 100'-0 x ~? (height not in these plans, UNRESOLVED).

---------------------------------------------------------------------------------------------------------------------------------

## (c) Crash cushion end posts (I-80 PDF 229 Ramp A = design 222, PDF 230 Ramp G = design 223)

- Location: at the gore where the two bridge barriers diverge: Ramp A at "CL Pier 6A or 5WB", Ramp G at "CL Pier 19G & 11WB". The post sits at the end of the 3'-8 barrier rails (Std. Sheet 1020SE modified, 0 deg skew stub abutment) on the gore line.
  Distance from CL pier to the start of the end block is not dimensioned (scaled about 2'-1 in plan on Ramp A; UNRESOLVED).
- Geometry (elevation looking north / plan D-D / sections A-A, B-B, C-C): total end post 7'-6 = 5'-0 taper + 2'-6 constant; end block 2'-0 wide at the far 2'-6 and tapering in plan up to the two rail widths at the
  pier end (rails 1'-5 base width each, section C-C: 5 + 3 1/2 + 8 1/2 base steps); height 3'-8 at the rail end, tapering linearly to 2'-10 over the 5'-0, 2'-10 over the last 2'-6 (points of intersection 2'-10; section A-A 2'-0 x 2'-10, 2" cover);
  section B-B shows 7, 9, 6, 3, VAR. bar rows and a haunch (3" + 7") on the deck; each rail piece has the barrier profile (vertical inner face, 10* and 3* maximum notes). Deck cover 1" clear to deck reinforcing. Anchorage: vertical bars 6c1/6c3/5c5/5c6-5c9 (stainless, L-shaped) bent into the deck slab (3'-0 legs, 3" from the ends, 7 spa @ 1'-0 = 7'-0).
  Plan shape on Ramp A: symmetric wedge. On Ramp G the wedge is skewed (pier skew 27d27'55"): rail side lengths differ (the rails continue to a point on the skewed pier); plan dims other than the 7'-6 total are UNRESOLVED.
- Reinforcing: `misc/crash-cushion-end-post-bars.csv` (epoxy 373 lb / 450 lb; stainless 240 lb / 262 lb), bent bar shapes (PDF 229): 6c1 3'-3 x 3'-0 (D 4 1/2), 6c2 1'-8 x 1'-6 U, 6c3 3'-4..4'-0 x 3'-0,
  5c4 / 5c5 rail hoops (5c4 3'-3 1/4 and 2'-9 1/2 legs, D 2 1/2 / 3 3/4; 5c5 2'-0 x 10", chamfer 6 7/16), 5c6-5c9 jogged bars (1'-3 lead, 3'-0 foot, 8" and 9"/10" riser, D 2 1/2), 6d3/6d4/5d5 tapered horizontals (4'-9 / 4'-10 and 2'-4).
  Concrete 2.4 cu yd (Ramp A), 2.8 cu yd (Ramp G).
- Cushion: "PERMANENT SEVERE USE CRASH CUSHION - SEE MATERIALS I.M. 455": no geometry printed; bounding envelope UNRESOLVED (placeholder box with unknown length; width tapering from 2'-0).
Inputs: `CrashCushionEndPostDefinition` {Name, Support/pier, Side, Station of block start, length 7'-6, end width 2'-0, heights 3'-8 -> 2'-10, rail widths 1'-5 (2), rail gap UNRESOLVED, wedge direction, rebar list, Cushion placeholder {length, width, height}}.
Elements: `IfcBuildingElementProxy` (CRASH_CUSHION_END_POST) or `IfcRailing` USERDEFINED / `IfcBuildingElementPart` of the barrier; `IfcReinforcingBar`; cushion = `IfcBuildingElementProxy` placeholder (flagged by others).

---------------------------------------------------------------------------------------------------------------------------------

## (d) Lighting (I-80 PDF 233-236 = design 226-229; Ramp G PDF 168-170 = design 166-168)

Not in contract: poles, luminaires, ITS devices and all conductors ("cost of furnishing and installing poles, ITS devices and power/ITS conductors is not a part of this contract"). In contract: pole bases,
conduit, junction boxes, expansion fittings. Poles/luminaires: omit or flagged placeholder only; the plans give no pole height or geometry.

### d1. Pole bases (4 on I-80, 1 on Ramp G; `misc/lighting-items.csv`)
- 2026-10-11 CORRECTION (raster 220-450 dpi, PDF 235 Section D-D / section through base): the "5" above the block is the anchor bolt PROJECTION, not "top 5\" above the
  base ledge". The block is cast against the slab edge (keyway c.j.) with its bottom drawn flush with the slab soffit; 2'-7 1/2 at the deck edge = 1'-8 1/2 outer face + 11"
  arc drop over 1'-11 3/4 (R 3'-0, centre below the chord), 4 1/4 flat to the deck edge (chain 2'-4 vs 2'-6 projection does not close by 2"). Model: `BottomAtSlabSoffit`.
- Block (PDF 235 / Ramp G 169): 2'-0 wide x 2'-6 projection outboard of the barrier, top 5" above the base ledge, height 2'-7 1/2 at the barrier face and 11" at the outer edge (soffit curved R = 3'-0, 1'-11 3/4 across, 4 1/4 return);
  4'-4 allowable blockout in the barrier along the rail, barrier 1'-5 base width; CL pole base at the barrier-side of the 2'-0 block, anchor bolt circle 13 1/2" dia (I-80, 4 holes at 4 3/4 / 4 3/4) or 16" dia (Ramp G, 5 5/8 / 5 5/8),
  4 anchor bolts 1" dia x 1'-11 galvanized heavy hex nuts, anchor plate 15 1/4 x 3/4 x 1'-3 1/4 with 3" slot, thread 5", 4" nut zone; 2" rigid conduit (I-80 / Ramp G power 1 1/2" at Pier 8G) bends up to the base center. 1.0 cu yd each.
  Rebar: `misc/pole-base-exit-sign-rebar.csv`; blockout hoops 5x10/5x11 added at the barrier.
- Positions: I-80 south barrier at Pier 13WB (base 5'-0 back of CL pier), Pier 14WB and 15WB (CL base = CL pier), East Abutment (10'-0 max from the end of superstructure); Ramp G right barrier at Pier 8G (CL base = CL pier).

### d2. Junction boxes (LI-104 Type 1 flush cast iron in the barrier, Type 2 surface mount on pier tops)
Type 1 box: depth 5" into the barrier, 6" high (plan size not dimensioned: see Iowa DOT standard road plan LI-104; UNRESOLVED), 5/16" walls, 2" rigid conduit hubs (5 threads), 1/2" copper drain pipe, grounding buttons; power box and ITS box at piers 2'-6 min each side of the CL pier
(Piers 13WB-15WB / Pier 8G), 5'-0 between ITS and power boxes typical, 4'-0 min from expansion fittings, max spacing 500'-0, boxes remain within the textured lower portion of the barrier.
Positions in the barrier: I-80 south barrier at pier lines 5WB, 11WB, 13WB, 14WB, 15WB and abutments (PDF 233); I-80 north barrier at abutment and the underdeck plan stations 7583+40.50, 7588+69.64 (PDF 236);
Ramp G right barrier at Pier 8G, left/deck stations 67474+90.40, 67486+92.70 (CL Pier 11G), 67487+45.83 and stub-outs 67475+53.29, 67477+44.79 (2'-5 LT.).
Type 2 box: fastened to the pier top, 4" deep (Section B-B), for underdeck/high mast feed; 1 1/2" PVC-coated flexible conduit by others.

### d3. Conduit (barrier section, PDF 234): 2" dia rigid steel: ITS conduit 1'-8 above the barrier base (south barrier on I-80 only; right barrier on Ramp G), 5" from the traffic-side face; power conduit 7" above the base;
1" rigid steel in the deck slab for underdeck lighting and high mast (stub 3" min below deck bottom); 3'-0 (typ.) deep run down the abutment wing to a cap, expansion fittings at expansion joints
(I-80: 5" each way at abutment joints (5 req.), 7" each way at Piers 5WB and 11WB (4 req.), 2" each way at wingwall ends (4 req.); Ramp G: 3" abutment (3 req.), 6" at Piers 5G, 9G, 19G (7 req.), 8" at Pier 14G (1 req.)).
Ramp G power conduit runs the full length in the left barrier only. Polypropylene pull rope, 600 lb. Pier conduit 2" stub from the pier top (Section B-B) 1'-0 cap end. Counts and exact routes through the slab are schematic (UNRESOLVED coordinates).

### d4. Inputs a builder needs (proposed)
`LightingDefinition` on `BridgeDefinition`: `PoleBases` {Support name, Distance from CL support (back +), Side (barrier), BoltCircle 13 1/2" | 16", ProjectionBlock 2'-0 x 2'-6, depth profile, rebar}, `JunctionBoxes` {Type 1 | 2, Station or Support+offset, Side, Kind Power|ITS, size placeholder 1'-6 x 1'-0 x 5" (UNRESOLVED size)},
`Conduits` {Dia, Side, Kind Power|ITS|Slab, From/To station, height above barrier base 7" / 1'-8, expansion fittings at joints}. Placeholder pole marker (by others) only if flagged.
Elements (IFC): pole base = `IfcFooting`/`IfcBuildingElementPart` + anchor bolts `IfcMechanicalFastener` (ANCHORBOLT) + `IfcReinforcingBar`; junction box = `IfcJunctionBox` (PredefinedType POWER/DATA); conduit = `IfcCableCarrierSegment` (CONDUITSEGMENT) with `IfcCableCarrierFitting`; poles omitted or `IfcBuildingElementProxy` placeholder.

---------------------------------------------------------------------------------------------------------------------------------

## Corrections to `misc-items-data-check.md`
- Lighting anchor circle is 13 1/2" (I-80) and 16" (Ramp G), 4 bolts 1" x 1'-11 (not 13 1/4" / 8 bolts); pole base 2'-0 x 2'-6, not 4'-4 x 2'-6 (4'-4 is the barrier blockout length).
- Exit sign base is outboard of the barrier on an outrigger bracket from girder A, block 2'-0 x 6'-2 x 2'-6, locations only as ticks on sheets 104/108.
- Overhead sign locations: Unit 2 Pier 7WB (truss Sta 7595+82.22) and Unit 3 Pier 13WB (Sta 7607+12.18); the title-block station is not a location. Girders A+K (U2) and A+J (U3); unit 3 bracket height 6'-4 (unit 2: 8'-6).
- Ramp G drains: inlet is transverse 4'-0 x 10", tube 3'-0 1/2 from the gutter line; Pier 19G drain pipes are 8" drops into one 12" collection pipe 1'-8 right of CL girder A.

---------------------------------------------------------------------------------------------------------------------------------

## Model (feature/misc): what is built, clearance results, unresolved items

Scripts (re-runnable, optional inputs): `samples/plans/drains-ramp-g.csx`, `signs-i80.csx`, `end-posts.csx`, `lighting.csx`. Code: `SBM.Core/Misc/`. Every number below is a plan value or is flagged.

### Built
- **Ramp G drains 1-25**: trench (4'-0 x 10" + 3/8" plates, floor 4 1/4" below the grate; the 3 1/2" / 4 1/4" conflict stays raised) from 1/2" past the gutter line (deck edge - 1'-7),
  two grate panels, 8 x 3/8 vertical tube 3'-0 1/2 from the gutter line (the offset from CL Ramp G follows from the deck edge), free fall ending 1'-0 below the bottom flange (plan: 1'-0 min below
  the web connection, not locatable), splash basins 10' x 10' x 2' where the table says YES, piped drains 22-25 into the 12" collector. The table's cross slope is compared with the model.
- **12" collector (span 19G)**: A53 Sch 40 (OD 12.75"), 1'-8 right of CL girder A, elevations = deck top at the pipe minus the printed heights (drains 22-25; the cross frame rows have no station),
  couplers 4'-0 / 4'-0 / 2'-0 upstream of drains 23 / 24 / 25, hangers 5'-0 max, ends at the Pier 19G bearing line.
- **Pier joint systems** (all outside the concrete; fixed 2026-10 after the user saw the 5G pipe run diagonally through the cap): trough from the deck underside at the high end to the
  trough collection box, whose top is 1'-8 below the deck underside (PDF 154; trough falls 11.7% at 5G/9G/14G, 9.7% at 19G >= the printed 5.9% / 4.9% min; the deck underside itself falls 1'-8,
  which is why the old reading "1'-8 = 5.9% x run" matched), box 2'-2 across the joint, low end toward the cap; 8" pipe 1:12 back-station 3'-8 to a turn-down 9" off the BACK cap face, down 4" into the
  pier collection box hung on that face at 12'-6 / 18'-3 7/8 from CL column; pier pipe out of the box bottom 1 1/2" clear of the face, 8% along the face to CL column, then vertical (10 1/2" clear of
  the column face, which is 9" behind the cap face), clamps 6'-0 / 4'-0, 45 deg outlet + mattress. Pier 19G: the span collector now ends over the box inlet (STA 67502+90.18, 31 1/4" before the
  bearing line) and turns down into the box; the 8" trough pipe joins its elbow (section C-C). The printed box position and the collector line meet within 1/8" in plan.
  Before the fix the box was centred in the cap (N = CL column) and the pier pipe ran in a straight line in plan from there to the back face of the column (12'-6 along, 3'-7 across) at 8%, i.e. diagonally
  through the cap; the 19G collector ran to the bearing line and its drop overlapped the masonry plate.
- **Sign brackets** (4 on Unit 2 at Pier 7WB, 2 on Unit 3 at Pier 13WB): web, top plate 1 7/8, bottom flange plate, 4 stiffeners, 8 x 1 1/2" anchor bolts on the 1'-8 circle, base plate placeholder.
- **Lighting**: pole bases Pier 13WB / 14WB / 15WB (Unit 3) and Pier 8G (Ramp G Unit 2), 6 + 2 junction boxes (placeholders), ITS conduit (right barrier) / power conduit (Ramp G left barrier).
- **Bar lists** (schedule only, weights reproduce the plans): pole base 221 lb, exit sign base 189 lb, end posts epoxy 373 / 450 lb and stainless 240 / 262 lb.

### Drain clearance to the girders (surface to steel; negative = overlap); `drains-ramp-g.csx` prints it
Free-fall tubes (8" OD): 5 1/2" (drains 1, 2, 20, 21), 4 1/2" (3-8, 12, 18), 3 1/2" (13), 2 1/2" (11, 16, 17), 2 9/16" (19), **1 1/2" (9, 10, 15)**, **overlap 1/2" with the top flange
at drain 14** (Pier 10G back, G4 top flange, wide flange near the pier). Piped drains 22-25: tube 2 1/2" / 5 1/2" / 5 1/2" / 5 1/2", 8" drop pipe 3 9/16" / 6 3/16" (G1 top flange); the 12" collector
clears everything by 3/16" at its tightest (cross frame diagonal CF139-D1 at STA 67502+85.7 to 67502+92.8; TIGHT warning). Pier systems: pipe 1 vs a cross frame top chord 9 1/2", pier pipe 2' + from the bearing
elements; pipe 1 passes 6 5/16" under the Type 4 top chord at 5G/9G/14G (section A-A), the 19G collector drop clears G1 by 2 5/8" (the old masonry plate overlap is gone). The tube to CL girder A at drains 22-25 is
18 1/2" in the model against the plan's 1'-8: the 1 1/2" difference is reported, nothing is moved.

### UNRESOLVED (raised as warnings by the generator)
- Drain tube bottom for free fall ("1'-0 MIN below the girder web connection": connection location not printed); trench depth 3 1/2" vs 4 1/4"; 45 deg tee / cleanout geometry; upstream cleanout of the collector.
- Pier systems: box top 1/4" min / 1" max below the cap top (1/4" used), level of the bend under the box and stand-off of the vertical pipe from the column face (kept in the plane of the box outlet,
  no offset bends), pipe 1 plan route (straight, exactly 1:12), trough U width / sag (width = joint blockout), which point of the trough box the 3'-6 / 4'-0 locates (centre); conventions are listed in the generator warning "placement conventions to confirm". Pier 19G pipe routing from the collector end to the box.
- Sign supports: Unit 3 girder A brackets (reference of the 12'-10 7/8 dimension), the connection of the stub to the girder (the model's bracket web overlaps the girder web), MC18 / WT bracing / splice plates, anchor bolt lengths,
  HSS post height and truss size / elevation (placeholder base plate only). Exit sign bases: station (tick only), not placed.
- Crash cushion end posts: position from the CL pier, wedge direction, plan width at the pier end, skewed Ramp G plan, cushion geometry; not placed, bars scheduled.
- Lighting: East Abutment pole base (10'-0 max), which junction box is power / ITS, LI-104 box size (placeholder 18" x 6" x 5", flagged), underdeck boxes / stub-outs, expansion fittings, power conduit side on I-80, coating of the pole base bars.
