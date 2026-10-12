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
