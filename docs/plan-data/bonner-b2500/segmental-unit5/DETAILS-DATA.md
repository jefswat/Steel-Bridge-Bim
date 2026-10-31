# Bonner Unit 5 segmental box - PT blocks, concrete details, critical-section rebar (plan data, 2026-10-30)

PDF `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf` (vector sheets with a text layer). Every value has its page;
TEXT = printed, VECTOR = measured on the vector linework (scaled, flagged), DERIVED = from label positions or arithmetic. Nothing is invented:
open items are UNRESOLVED with page + reason. Data only - the generator / build script are not changed.

## 1. Files

| file | rows | content |
|---|---|---|
| `anchor-blocks.csv` | 55 | geometry of the bottom slab anchor block (p.297), top slab anchor block (p.298), erection PT bar blocks Type A / B / C + permanent bent bars (p.299-300), deviation blocks (p.302) |
| `anchor-block-locations.csv` | 105 | which segment carries the block for every B (bottom continuity) / C (top closure) tendon end, per layout sheet p.285-296 |
| `deviation-pipes.csv` | 16 | DEVIATION BLOCK PIPE VARIABLES (A-G, delta, R, L1-L3, length, dia) for spans 20-28 |
| `openings.csv` | 19 | A.O. / A.O.B. / T.A.O. (size + segments), bent diaphragm opening, utility opening, end segment diaphragm opening, pintle blockouts, vents, drains, survey blockouts, solar platform inserts |
| `keys.csv` | 12 | web shear key profile / spacing, top and bottom slab alignment keys (p.283) |
| `parapet.csv` | 8 | 1'-2" x 2'-6" concrete parapet, position, joints, end posts, bill (p.323-327) |
| `critical-section-rebar.csv` | 34 | typical segment bars (bill + bar types + placement), anchor block J / T bars, erection block P bars, deviation D bars, opening H bars; end / bent segment mark inventories |

`web-shear-keys.csv` (existing, 13 rows) is complete against both p.283 tables (sections 1-12 + bent control line).

## 2. Key readings

- **Bottom slab anchor block** (B tendons, SDI 22.6-PC, p.297): at each web / bottom slab corner; starts 4'-0" from the joint the anchor faces (face
  square to the tendon, "VARIES"), 8'-6" long, 1'-6" to the next joint; 2'-10" wide at the anchor end (2'-5 1/2" where the plan taper starts, ~6'-4 1/2"
  from the start, scaled), tapering to the web; section at the anchor = level top 2'-10" from the web face, 2'-0" vertical outer face on the bottom slab /
  fillet; anchor 1'-7" from the web face, 1'-1" above the slab top, 11" below the block top; block top slopes down to the slab at the taper end.
- **Top slab anchor block** (C tendons, SDI 12.6-PC, p.298): at each web / top slab corner under the slab; anchor face 3'-5" from the joint (at the block
  bottom), 6'-9" long, square end 3'-10" from the next joint; bottom face square to the web, 3'-2 1/4" from the web inside face; 2'-11 1/4" deck top to the
  block bottom at its free edge, inner face 1'-8" high; anchor 2'-7 1/4" below the deck top, 8'-10 1/2" from CL girder; tendon R 16'-0".
- **Block locations** (p.285-296, every sheet): B_k ends in segment k+4 of each cantilever arm toward the closure (B1 U5/D5 ... B7 U11/D11), C1 in 10,
  C2 in 11 - both arms of every closure; end spans: span 19 B1-B3 at 19-D7..D9 + 18-ES, C1/C2 at 19-D10/D11 + 18-ES; span 29 B1-B3 at 28-U7..U9, C1/C2 at
  28-U10/U11 + 29-ES. Erection bar blocks: Type A (top) and Type B (bottom) in every typical segment, 2'-11" from the joint; Type C only in D1 / U1.
- **Deviation blocks** (future PT, p.302): at every closure joint of spans 20-28, 3'-0" long centred on the CJ, web thickening inside the box from the
  top slab haunch to the bottom slab; 4" Sch. 40 pipes R 22'-0", 4'-0" long, two per web (P1 / P2) - table per span.
- **Openings**: A.O. 3'-0" x 4'-0" bottom slab on CL box (14 segments listed); A.O.B. 3'-0" dia bottom slab at every bent control line (split segment);
  T.A.O. 3'-6" x 2'-6" top slab (11 segments, one per span); bent diaphragm opening 5'-0" x 10'-0" with 1'-3" x 2'-0" top chamfers; utility opening
  1'-4" x 2'-1" octagon; end segment diaphragm opening 6'-0" high, 3'-4" x 2'-7 1/2" chamfers (width 10'-2" scaled).
- **Typical segment rebar** (p.269-270): the bill for XX-D3 / XX-U3 (17 marks, 8,511 lb) with bar types 1-7, spacing along the segment (A bars 15 @
  11 1/2", W / A5-A7 28 @ 6", B1 longitudinal @ 1'-0" with the REINFORCING VARIABLES counts A / B per segment group, W sizes #6 / #7 per group, W5 / W6
  substitutions in D11-D13 / U11-U13). Covers printed: 3" CLR. at the top slab, 2" CLR. (TYP. U.N.O.).

## 3. UNRESOLVED (page + plain reason)

- p.297 bottom slab block: plan straight length / taper length, corner chamfer and the elevation top-slope end height are drawn, not dimensioned (scaled).
- p.298 top slab block: inner face offset from CL girder (7'-1 1/4" scaled) not dimensioned; the second bottom line in elevation read as the sloped bottom meeting the web.
- p.300 Type A block: the vertical dims of Section B-B (2'-8 1/4", 2'-0 1/2", 1'-5", 8", 9 3/4") have no legible references in the text layer.
- p.299 permanent bent PT bar pockets: outline not resolved.
- p.302 deviation blocks: which faces the printed widths (1'-8", 9", 1'-0", 7", 2'-10", 3'-7"; 1'-6", 3", 2'-4", 3'-1") refer to; the inside face is drawn parallel to the web.
- p.287-295 layout sheets are "downstation bent n shown" - the key elevation hatching that says which other bents they cover was not read; the block
  pattern is identical on every sheet, so it was taken as typical.
- p.292: 24-U11 designation missing from the text layer (taken by position); p.285 / p.296 one B3 label each lands next to the end segment.
- p.263-266: spans 22-25 and 27 show one A.O. label (D12) while spans 20, 21, 26 show two (U9 + D12) - recorded as printed.
- p.271: end segment diaphragm opening width (10'-2") and expansion joint blockout depth per the S2 / S4 joint drawings (not in this set).
- p.269-270: bottom cover of the top slab; A10 / A12 count split; how type-2 / type-1 diagrams map onto one web corner (the printed type-1 dims are
  offsets: their sum does not reproduce the listed lengths - A6 12'-7 1/2" vs 11'-10", W3, W4); A16 and D-4 wire shapes (not in the bill).
- p.297-302, 321: block / deviation / opening bars (J, T, P, D, H) have counts and spacings but NO bill of materials and no bend dimensions.
- p.273-276 (end segment, 48 marks) and p.279-280 (bent segment, 44 marks): NO bill of materials in the set; shapes drawn only.
- p.322: deck drain offset from the edge and vent hole positions in the segment drawn only; p.263 survey blockout size not read.
- p.327: parapet end post outline above the parapet; quantity tables labelled "UNIT 3" on the Unit 5 parapet sheet (recorded as printed).

No CONFLICT rows beyond the "UNIT 3" label above.

## 4. Recommended data model (fits `SegmentalDefinition`, all OPTIONAL, omitted from the .sbm when empty)

```
SegmentalDefinition
  List<SegmentalBlockType> BlockTypes      // named outlines: "BottomSlabAnchor", "TopSlabAnchor", "TempBarA/B/C", "Deviation"
  List<SegmentalBlock>     Blocks          // placements
  List<SegmentalOpening>   Openings
  List<SegmentalKey>       Keys            // optional: shear / alignment key profiles (or keep as joint metadata only)
  SegmentalParapet?        Parapet
  List<SegmentalBarSet>    BarSets         // or reuse PlanBarSet with a segment frame

SegmentalBlockType { Name; Host Corner (WebBottom | WebTop | BottomSlab | Web);
  // section polygon in the segment section frame (x = from CL girder outward, y = down from the deck top), at the block start,
  // + an end section (taper) and plan widths - lofted between; or explicit (along, section) stations for tapers
  List<SectionStation> Stations; double AnchorFaceTiltDeg; string Source; string Unresolved; }
SegmentalBlock { string Type; string Segment (e.g. "20-U5"); int Side (-1 left, +1 right, 0 both);
  BlockReference From (DownJoint | UpJoint | TowardClosure | TowardBent); double Distance; string Tendon; string Source; string Unresolved; }
SegmentalOpening { Kind (BottomSlab | TopSlab | Diaphragm | Utility | Pintle | Vent | Drain); string Segment or Bent;
  outline polygon (in the slab plane: along / across; in a diaphragm: across / down) or Diameter; Chamfer; Temporary flag (T.A.O.) }
SegmentalBarSet { string Segment pattern ("XX-D3" / "*-U5..U9"); mark / size / shape (BarShape + printed dims); placement rule
  (Transverse | Longitudinal | WebFace) + region (top slab top/bottom mat, bottom slab, web outside/inside, block) + count / spacing along the
  segment + cover; ListedLength; Unresolved }
```

Blocks are solids added to the segment shell (union = separate closed solid touching the segment, like the cap blocks; IFC IfcBuildingElementPart
'ANCHOR_BLOCK' / 'DEVIATION_BLOCK' aggregated into the segment IfcBeam); openings = IfcOpeningElement voids (volume deducted); T.A.O. flagged temporary
(repaired = filled, model it as an opening with a Status note); deviation blocks belong to the closure joint element. Segment-relative placement keeps
the data short (the same typical block repeats in every arm), with the layout pattern "B_k at segment k+4 toward the closure" as rows.
