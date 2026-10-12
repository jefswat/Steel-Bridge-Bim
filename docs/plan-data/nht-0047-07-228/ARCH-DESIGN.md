# Northaven Trail tied arch (span 10) - design note (v1)

Plan set: TxDOT CSJ 0047-07-228 (HNTB 2020), arch sheets PDF p.119-155. Plan data: `SUMMARY.md` section 3a / 8 and the CSVs in this
folder. This note fixes the data model and the generator before the code; section 6 lists what v1 leaves for v2.

## 1. Where the arch lives
- The arch is its OWN `BridgeDefinition` ("Arch span 10") on the trail alignment with `Arch = ArchDefinition` (OPTIONAL, omitted from the
  .sbm when null -> every existing file and model is unchanged) and `Girders.Count = 0` (allowed only when `Arch` is set).
- Its two supports are the arch piers ("Pier 10", "Pier 11", same stations / skews as the approach units) WITHOUT a SharedPier link and with
  substructure disabled: the pier caps and the fixed-elevation arch pedestals belong to the approach units ("NHT Unit 3" last support Pier 10,
  "NHT Unit 4" first support Pier 11, CapBlocks "arch bearing south / north"). The arch bearings read the pedestal tops from the pier units'
  `SubstructureUnit.FixedSeats` (nearest seat in plan; the plan distance CL bearing -> pedestal seat point is printed, ~1 1/8"). The bearing seats are
  still published on `SteelModel.BearingSeats` with a plan Location (`RegenerationCache.ForeignSeats` can pass them to a shared pier if ever linked).
- Script `samples/plans/arch-nht.csx` (run AFTER `build-nht.csx`) adds / replaces that bridge; without an approach model it builds its own test
  alignment (curves 104-107 + TRAILNEW profile + 1 % crown) and two supports.

## 2. Local structure frame (p.119)
`ArchFrameDefinition`: two plan work points WP1 / WP2 (station + offset on the alignment) with their local x (CL1 = -100.5 ft, CL2 = +100.5 ft),
the elevation of the local origin (534.12), the tilt (1.039 deg, local XY = horizontal rotated DOWN about a horizontal axis) and the plan
direction of the tilt axis (the CL of pier 10 = the support line of the named support). Construction:
- plan X = unit(WP2 - WP1); origin plan point = WP1 + (0 - x1)/(x2 - x1) (WP2 - WP1);
- n = horizontal unit normal to the tilt axis on the +X side; local Z = n sin(t) + up cos(t); X = plan X lifted into the plane; Y = Z x X (left).
- reported, never adjusted: plan WP1-WP2 distance vs the local chord projected (U3: 200.926 vs 200.974), WP elevations from the plane vs the
  printed 535.74 / 532.49 (optional check inputs).

## 3. Members (all inputs in local feet-derived meters; generator `SBM.Core/Arch/ArchGenerator.cs`, pure function)
| item | input | generated |
|---|---|---|
| ribs | `ArchRibDefinition`: chord start/end (T0 / T34 local), radius 126'-0, rise 50'-0 (check), inclination 5 deg (toward +Y or -Y), AISC section W14x132 bent about Y-Y, splice points RS1-RS5 | circle in the inclined plane through both chord ends; W profile swept (web normal to the arch plane, flanges in it); pieces between splices (RS = data, 1/2" gap); knuckle stubs RS1 / RS5 down to the end flange = same envelope, flagged |
| ties | `ArchTieDefinition`: start point, element chain (line / arc R, left/right, length; R 342'-4 x 16 @ 5'-10, R 360'-8 x 16 @ 6'-1 3/4, straights 1'-7 15/16 / 8'-4 3/16), start direction solved so the chain closes on the end point (miss reported), panel points T1-T33 by distance, T section 7/8 x 11 web + 7/8 x 1'-2 flange, WP = bottom of flange, splices as data (U5) | web + flange plates swept along the plan chain (flat in local), Gr 50-F1 FCM |
| hangers | `ArchHangerDefinition` per cable: tie anchor TC, rib anchor HC (p.147), strand 1", plan length (check), socket sizes (nullable) | straight 1" strand solid TC-HC (new category `Hanger`), sockets only when sized |
| floorbeams | `ArchFloorbeamDefinition`: panel points on both ties (FBn = south Tn - north T'(34-n)), 1" web, 7/8 x 12 flange, 8 7/8 at the ends / 9 7/8 at the crown (CL NHT), 1/2" gap to the tie flange edge, PT openings A/B (data) | flange + crowned web plates |
| upper bracing | named local points (U1-U11, U1'-U11', UL1-UL10, p.128) + members (from, to), W8x28 | W8x28 pieces point to point (diagonal U(n)-UL(n)-U'(11-n) is straight in the table; the second diagonal kinks at UL - members to the table points) |
| knuckles | `ArchKnuckleDefinition`: end line (T34'-T0 / T0'-T34), FB1 line, 2" x 32" end flange, 1" end web 1'-0 1/8 at the chord, 1" deck plate, PT anchorage points (data) | plates; end web end heights and deck plate level UNRESOLVED (flagged parts) |
| deck | `ArchDeckDefinition`: slab between the tie web faces from end web to end web, top = finished grade of the alignment (profile + crown), 3" x 8" 12 ga form deck (profile from the L.B. Foster 3" bridge decking drawing, scaled), curbs 10" x 4" with inner faces 7'-0 off CL NHT, PT tendons (data) | slab solid (flutes filled), corrugated form deck sheet solid (steel), curbs |
| bearings | `ArchBearingDefinition` per corner: support, local point (CL brg 2" toward the span from T34'/T0/T0'/T34), plan pedestal top (p.102 EL A / E), stack (p.107: 1/8 pad, 2" masonry 3'-2 x 1'-5, 3" lower sole, 15 x 15 elastomer 3 5/8, beveled upper sole 3 1/8 / 3 / 2 7/8), anchor bolts 1 1/2" x 4, anchor pin 3" | stack built UP from the plan pedestal top; `BearingSeat` (Location, Axis, footprint) for the pier; B1-B3 chain differences reported (bearing top vs underside of steel) |

## 4. Hooks
- `RegenerationCache` phase 1: `b.Arch != null` -> `FramingGenerator` builds support lines only (no girders, no deck), `ArchGenerator.Generate(framing, units)`
  -> `ArchModel` {Steel (a `SteelModel` with arch parts / assemblies / anchor bolts / seats, `SteelModel.Arch` = the ArchModel), deck meshes, tendons, checks, diagnostics}.
  The bridge's `Steel` = that SteelModel and `Concrete` = a `ConcreteModel` with the arch slab (`Deck`) and curbs, so viewport, IFC, database,
  quantities and the cache key (definition JSON) need no arch-specific plumbing beyond new categories. Phase 2 skips arch bridges (no own substructure).
- New `PartCategory` values (appended): ArchRib, TiePlate, FloorbeamPlate, Hanger, HangerSocket, ArchBracing, KnucklePlate, FormDeck; `AssemblyKind`:
  ArchRib, ArchTie, ArchHanger, ArchFloorbeam, ArchBracing, ArchKnuckle (bearings reuse `Bearing`). `SteelPart.Assumption` (OPTIONAL) carries UNRESOLVED
  geometry to IFC `SBM_Status` and the viewport layer.
- IFC (IFC4X3_ADD2): rib IfcMember .ARCH_SEGMENT. in an IfcElementAssembly .ARCH.; tie plates IfcPlate FLANGE/WEB_PLATE in .GIRDER.; hangers IfcMember
  .SUSPENDER. (IFC 4.3 has no .CABLE.; IfcTendon is for prestressing) in .SUSPENSIONASSEMBLY., sockets IfcMechanicalFastener USERDEFINED 'CABLE_SOCKET';
  floorbeam plates IfcPlate in .GIRDER.; bracing IfcMember .BRACE. in .CROSS_BRACING.; knuckle plates IfcPlate USERDEFINED 'KNUCKLE_PLATE'; form deck
  IfcPlate .SHEET.; deck = IfcSlab (deck part) with the curbs as a second body item.
- Database: same categories -> element names / IFC entities; sheet links `sheet-links-nht.json` rules on the arch assemblies (p.119-155).
- Views/sheets: none in v1 (GAP); the 2D plan of the arch bridge is empty (no girders).

## 5. Checks (`samples/plans/check-arch-nht.csx`)
Hanger lengths TC-HC vs p.147 (+-1/16"), rib points on R 126'-0 / rise 50'-0 / 5 deg plane, tie points vs p.147 T points, floorbeam count and positions,
bearing chain (B1-B3), steel weights vs the sheet tables (rib 59,100, floor system 61,000, tie 33,200, bracing 21,600, knuckle 9,700, fin plates 3,800 lb).

## 6. v1 scope / GAPs for v2
v1: frame, ribs (+ splice positions), ties, 64 hangers, 33 floorbeams, upper bracing, knuckles (simplified plates), deck slab + form deck + curbs,
4 bearings with anchor bolts, PT tendons as data, IFC / viewport / database through the steel + concrete models, checks.
v2: rib / tie splice plates and bolts, cable fin plates (rib p.148, tie p.149) and cable connectors (p.150), hanger sockets / turnbuckles sized from
the vendor, floorbeam connection plates / bolts / studs / PT slots as holes, knuckle arch flange plates / gussets / stiffeners / cheese plates /
jacking, PT tendon solids (strand count p.155), deck rebar (p.154), railing, 2D sheets, camber (U4 - none in the plans).

## 7. v2 (2026-10-11) - what was added, from which sheet, what stays open
All inputs stay OPTIONAL (null / empty = v1 behaviour); data in `samples/plans/arch-nht.csx`, checks in `check-arch-nht.csx`.

| item | plan | model | open / flagged |
|---|---|---|---|
| Rib field splices RS1-RS5 (both ribs) | p.127 (note "RS1 and RS6 similar" -> all five) | `ArchRibDefinition.SpliceDetail` = `ArchSpliceDefinition`: 14 1/2 x 5/8 outside + 2 - 5 5/8 x 5/8 inside flange plates (flush with the outside plate edge, Section A-A), 2 - 10 x 3/4 web plates, 1 3/4 clips, staggered 7/8" bolts at 2" (68 per splice: flanges 2 x 24, web 20); plates flat in the plane tangent to the rib at the joint (<= 1/32" off the R 126' curve) | - |
| Tie field splices | p.144 detail; positions p.143 TS(1)-TS(4) | `ArchTieDefinition.SpliceAt` / `SpliceDetail` / `SpliceGap`: tie cut into 5 pieces; 2 - 9 x 3/4 web plates flush with the web top, 14 x 5/8 bottom + 2 - 6 x 5/8 top flange plates, 1'-6 1/2 (checked), 42 bolts | U5 flagged: framing plan p.143 + erection p.93 (5 segments) vs p.120 table / elevation and p.150 elevation (TS1-TS3); positions not dimensioned (mid panel, as the p.120 table points); north splices are not the rotation of the south ones |
| Rib pin / hanger geometry | p.147 vs p.148 | `ArchHangerDefinition.RibAnchor` = p.147 H(n) X2/Y2/Z2 (the pin), `RibWorkPoint` = HC(n) X2'/Y2'/Z2' (on the rib working line, R 126' within 0.0004'); plan length checked TC -> HC, strand pin to pin | CONFLICT reported: the p.147 labels are the reverse of the geometry (P1 = H offset off the arch plane, P2 = H-HC along the cable, P2 sin B = 10" - all 64 match to 1/16"); U6 resolved for the hangers |
| Cable fin plates | p.148 table (CSV), p.149 table (new CSV `cable-fin-plates-tie-girder.csv`), p.146 C-C, p.83 materials | `ArchFinSettings` + `ArchRibFin` / `ArchTieFin`: rib fin between the flange inner faces, top P4 below the working line, sides P3, R 2 1/4 around the pin, 1.378" hole; tie fin 7/8" bent plate, lower leg 8 x 7 centred on the floorbeam CL bolted to the tie web outside face (4 bolts), upper leg turned about the bend line (1/2" above the web) to the pin TC; checks angle B, P1, P2 (all OK), angle A, pin height, orientation | rib fin thickness (7/8 = tie fin, same socket) and 1" top clips flagged (p.148); tie: TC is 3/8" lower than bend + 4" and the cable lean differs from angle A by up to 1.7 deg (reported) |
| Cable connectors | p.150 | `ArchCableConnector` pairs C1xC2, C3xC4, C5xC6, C3xC6, C27xC28, C29xC30, C31xC32, C27xC30 (circled crossings, both arches), pendant plate + 2 sleeve brackets (UHMW-PE, 940 kg/m3, not steel) | p.150 is N.T.S.; table cables cross 0.06-1.3" apart (connector needs 1 3/16") - reported |
| Floorbeam connections | p.146 | `ArchFloorbeamConnection`: 1" stiffener in the floorbeam web plane (1" flat, 8" at the flange edge, 1 x 1 clip), 2 - 5/8 x 7 x 1'-0 1/2 web plates (10 bolts), 5/8 x 4 15/16 flange plates each side top + bottom (12 bolts) = 44 bolts per floorbeam | bottom flange plates flagged (Detail 1 vs Section B-B) |
| Floorbeam studs / PT slots / holes | p.145 | 32 horizontal 7/8" studs per web face at 7" (plan note 3 spacing 3 1/2-6", connection plates and slots excluded), 4 obround 2 1/4 x 5 1/2 slots per web from the schedule (A from the north tie, B from the south tie, pairs 1'-4) | stud length scaled 6 7/8 (flagged); 32 studs cannot keep <= 6" between the slots (max 6 7/16, reported); the 15/16" holes @ 8" (bar passages) not cut |
| Upper bracing | p.128 typical section (web vertical), p.141 / 142 (new CSV `upper-lateral-bracing-splices.csv`) | continuous member U(n)-UL(n)-U'(11-n) (UL on it: checked 0"), spliced pieces cut 1/2" off its flange tips (L2 sin(angle) = bf + 1"), top / bottom 7" splice plates with 12 bolts each; crossing angles checked in space (18 of 20 within 0.75 deg) | plate thickness flagged (1/2"); U-point gussets / connection plates / stiffeners (p.129-140, 12 sheets of per-point dimensions) NOT modeled (GAP); members still WP to WP at the ribs (flagged) |
| Knuckles | p.123-126 (re-read at 300-500 dpi) | `ArchKnuckleDetail` + `ArchKnuckleCorner` per rib end: bent 1" arch flange plates (rib flange width, legs NW 9 15/16 / 10 3/16, SW 10 1/2, R 6, 1" clip, from the end web to the RS joint), stub web (milled to bear), 1" stiffener 7" outside the springing (9 1/2), arch stiffener 1'-11 5/8 up the rib, knuckle tie web to the joint, tie joint at the end flange inner edge (derived from 32" / 1'-1 1/2) with 2 - 1/2" gussets (18 bolts), 1 1/8 fill + 1" splice / bearing plate, 1" jack cheese plates; end web with 4 - 14 x 3 PT openings and 15 studs; END PLATE (1" deck plate) ON the end / tie / floorbeam flanges (3" / 6" / 5" overlaps) with 7/8" studs in rows 10 5/8 apart; the W stub is no longer built; the tie field pieces start at the joints | flagged: end web end height (10 1/4 scaled), stub web thickness, arch stiffener extent, splice / fill plate outline + bolts, cheese plates, end plate stud columns; weight CONFLICT kept (end flanges alone 9,219 lb vs 9,700 knuckle total; end plates moved to the floor system per p.143 note 2) |
| Bearings | p.102 pier plan, p.107 | `ArchBearingDefinition.LongAxisAngleDeg` = 63 deg from the pier line (S right -> N up-station, as the approach pedestals): masonry / sole plates, pin and anchor bolts follow the pedestal; check: parallel to the pedestal (0.000 deg), 0.48 deg off the chord | - |
| PT | p.155, p.145, p.152 | `ArchPtSystem`: 4 tendons x 4 - 0.6" strands (DYWIDAG flat anchorage drawn with 4 wedges; 170 kips = 4 x 0.217 x 270 x 0.725), end web opening to end web opening through the floorbeam slots at 7" (6 1/8 above the end flange), flat anchorage envelopes, S2 zone bars at 1'-0 / 1'-4 / 2'-10 1/2 / 3'-2 1/2; IFC IfcTendon / IfcTendonAnchor | strand spacing / anchorage size scaled (flagged); duct not modeled |
| Deck form | p.152 panel | 3" x 8": pans 5 3/8 at the bottom, ribs 2 5/8 / 1 1/2 (v1 had the scaled brochure profile inverted); a corrugation under every D2 bar (`FlutesOnBarGrid`) | - |
| Deck bars | p.152 / 154 | `ArchDeckRebar`: A1 13'-10 top (centred on CL NHT), B1 18'-2 bottom @ 8; 27 D1 / D2 lines @ 8 (D2 in the pans, 1 3/4 clr), 40'-0 bars lapped 2'-8 from the west end; curb S1 #4 closed 6 x 9 @ 8 + 4 D4; bill check per mark | D1 / D2 / D4 162 / 162 / 48 x 40'-0 match; A1 306 vs 606 (one per 8" space modeled), B1 306 vs 303, S1 610 vs 606 (30" loop vs 3'-3 with hooks), D3 not modeled; lap positions / knuckle bends flagged |
| Railing | p.89 / 91 (ped rail high on the arch) | - | GAP (no pedestrian rail generator) |
| 2D | - | `ArchViews` Elevation (south arch: rib, tie, hangers, RS / TS) + Framing plan (ties, floorbeams, ribs + bracing dashed), `SheetSet` arch branch | - |
| Viewport / IFC | - | flagged steel parts on `3D-UNRESOLVED-STEEL`; new categories FinPlate, CableConnector, ArchConnectionPlate, ArchStiffener, PtStrand, PtAnchorage; AssemblyKind ArchSplice | deck slab stays 2 bodies (corrugated main slab + knuckle zones / curbs): not merged (not simple) |
