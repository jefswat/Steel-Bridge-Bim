# Concrete-bridge features: design proposal (for review BEFORE implementation)

Status: PROPOSAL; **T1 (feature A, integral abutment) IMPLEMENTED** (see 2.7); the open questions in section 8 are still unanswered.
Author: design study agent, 2026-10-11. Scope = HANDOFF section 5 items 5, 6 and the
substructure parts of item 9 (round CIP pipe piles, curved-plate bearings, future-beam pedestals), for the two prestressed
plan sets:

- **Woodbury** = Iowa DOT Design 817, `Woodbury 0817 Sioux City I-29 NB over Floyd Blvd_Rev1.pdf` ("p." = PDF page, DS = p - 2).
- **Br02069** = MnDOT, `Br02069_Certified Plans_20250926.pdf` (PDF page = sheet).

Plan data already extracted: `woodbury-i29-floyd/SUMMARY.md`, `br02069-th65/SUMMARY.md`. Values below that are not in
those summaries were read for this study on 250-330 dpi raster clips (tag RASTER); DERIVED = computed from plan values.

User rules applied throughout: model the plans; no fallback guesses (anything the plans do not state is a generator
warning "UNRESOLVED" with the PDF page); standard items (Iowa 2087-BTCD / 4509-BTCD / 1036-BTD, MnDOT Fig. 5-397.560 /
B-details) are taken from the standard, not designed per bridge.

---------------------------------------------------------------------------------------------------------------------

## 0. Summary of the proposal

| # | feature | where it lives | new input | status of plan data |
|---|---|---|---|---|
| A | Integral abutment pile cap (Woodbury): stepped top, sloped bottom, ONE pile row on CL brg, one-sided wing extension, CMP casings | substructure (`SubstructureAbutment*.cs`) | `AbutmentDefinition.Kind = Integral` + `PileRowOffsets`, `CapTopFollowsSteps`, `WingExtensionDefinition`, `PileCasingDefinition` | complete except wing-pile position and casing length |
| B | Semi-integral stem abutment (Br02069): stepped stem with back ledge, seat, infill walls, mask walls to absolute top, explicit (future) pedestals, 12" CIP pipe piles | substructure | `StemSection` polygon, `InfillWallDefinition`, `MaskwallDefinition.TopElevation/Length`, `PedestalDefinition` list, `PileType.CipPipe` | complete except wall-end corners (N) |
| C | Concrete END diaphragms (both sets) and PIER continuity diaphragms (Woodbury) cast with the deck, beams embedded | NEW `Diaphragms/` generator, run after prestressed + substructure | `ConcreteDiaphragmDefinition` per support | Woodbury complete except the paving-notch reading; Br02069 bottom of the front part UNRESOLVED (p.21) |
| D | Steel intermediate diaphragms between prestressed beams (Woodbury C15x33.9 / W21x50 + W14x38) bolted through cast web holes | NEW `Diaphragms/` steel detailer (not CrossFrameDetailer) | `PsSteelDiaphragmDefinition` (hole chains per beam line, members per bay, Iowa 1036-BTD connection) | complete except the member elevation rule (question 4) |
| E | Abutment bearings for prestressed beams: Iowa 3x3 steel bar (integral), MnDOT curved-plate F1/E1 (B310/B311) | bearing builder called by PrestressedGenerator (AFTER the Prestressed/ agent lands) | `PrestressedBearingDefinition` per support | Br02069 sole plate B303 UNRESOLVED (p.30) |

Recommended order: A+B (substructure, one agent, sequential commits) in parallel with D (new files only); then C (needs
read access to the prestressed beams and the cap tops; hook-up after the Prestressed/ and Framing/ agents merge); then E;
then plan-data scripts and views. Details in section 7.

---------------------------------------------------------------------------------------------------------------------

## 1. What the code can already express (inventory)

| capability | code | Woodbury | Br02069 |
|---|---|---|---|
| As-designed abutment: footing with per-S bottom profile (`FootingBottom`), end lines parallel to the alignment, end notches | `AbutmentDefinition`, `SubstructureAbutment.cs` `BuildDesignedAbutment` / `AddSlab` | sloped pile-cap bottom: YES (2 points per abutment) | level footing: YES |
| Section chain normal to the support (x from CL brg): footing back/front, backwall thickness/position, corbel (paving notch) on the BACK face, wash, keyed backwall | same | backwall model does not fit: the integral "backwall" IS the diaphragm cast with the deck, and its corbel is the paving notch | stem with back ledge does not fit the backwall + corbel chain |
| Beam steps (seat blocks) per girder, top from the plan (`Steps`), length `StepLength`, from backwall front to `BearingToStepFront` | same | NO: Woodbury steps are the full-length stepped TOP of the pile cap (step diagram p.17), not blocks | NO: pedestals are separate blocks with plan sizes, 3 of them have no beam (future) |
| Maskwalls (thin end panels on the footing, top = footing top + Height) | `MaskwallDefinition` | n/a | partly: needs an absolute top, own length 6'-4 along F.F., full-height from the footing bottom (p.11 C-C) |
| Backwall extension beyond the footing end | `BackwallExtensionDefinition` | NO: the Woodbury wing extension is a wall + its own level-bottom footing block at an angle (p.15, p.17) | n/a |
| Wingwalls with counterforts and piled wing footings | `WingwallDefinition` | n/a (no wingwalls) | n/a (MSE walls) |
| Piles: `PileStations` x TWO rows (`PileBackRowToBearing`, `PileFrontRowToBearing`), `PileLength`, embedment | same | NO: one row on the CL brg | YES: 2 rows x 7, back 1'-9 / front 1'-3 |
| Pile section | `PileType { SteelHP, PrecastConcrete }`, `PileSection` | HP12x84: YES | 12" CIP pipe: NO |
| Pile casing (CMP in EPS) | none | NO | n/a |
| Prestressed beams, pads, `BearingSeat`s (2 per girder at bents) | `PrestressedGenerator` | beams/pads YES (pad 9 x 1 x 2'-4 1/2 via `PadLength/Width/Thickness`, `EndGapAtBent` 4", `BearingToEnd` 8"); abutment 3x3 bar bearing NO | curved-plate bearing NO (pad only) |
| Cross frames / diaphragms | `CrossFrameDetailer.cs` (partial `SteelModelGenerator`; channel diaphragm `BuildDiaphragm` with bolts through connection stiffeners) | skipped for prestressed (`FramingGenerator`: `if (!PS)`); the steel detailer is tied to steel girders (FramePlane between web faces, stiffener holes) | none needed |
| Concrete diaphragms (end / pier) | none (`ConcreteGenerator` = deck slab, haunch, barriers, end blocks for joints) | NO | NO |
| IFC classes in use | `IfcExporter`: Backwall/Wingwall/Maskwall -> IfcWall, Pedestal -> IfcColumn 'PEDESTAL', Pile -> IfcPile .DRIVEN., Footing -> IfcFooting, steel channels -> IfcMember, pads -> IfcBearing .ELASTOMERIC., PS beam -> IfcBeam .GIRDER. | | |

Conclusion: the existing `AbutmentDefinition` is the Iowa stub/seat abutment of the steel plan sets. Both prestressed sets need
(a) a different section model for the abutment body and (b) a diaphragm that belongs to the superstructure pour and embeds
the beams. The proposal keeps the current abutment path unchanged (default) and adds OPTIONAL pieces; nothing becomes a
default value - every new field is either required for its feature or reported UNRESOLVED.

---------------------------------------------------------------------------------------------------------------------

## 2. Feature A: Iowa integral abutment (Woodbury, std 2087-BTCD modified)

### 2.1 Plan geometry given

| item | value | p. | tag |
|---|---|---|---|
| Type | integral: pile cap ("abutment footing") + abutment diaphragm cast with the deck; no backwall, no joint; paving notch on the diaphragm back face | p.15, p.20 | TEXT |
| Pile cap width | 3'-0 normal to the abutment, piles centred (section B-B: 2 1/2" clear each side of the 24" CMP, pile on the CL) | p.15 B-B | RASTER |
| Pile cap position | piles on CL brg ("CL SOUTH ABUT. BRG. & CL PILES", p.17) and centred in the 3'-0 cap => cap +-1'-6 about CL brg | p.17, p.15 | RASTER / DERIVED |
| Deck end = diaphragm back face | deck end 1'-6 1/4 beyond CL S abut brg along CL NB; 1'-6 / cos(10 deg 09'36") = 1'-6.28" | p.6, p.22 | DERIVED (reconciles the back face at 1'-6 behind CL brg) |
| Cap top | stepped: one level step per beam, top = seat elevation (S D 1117.50 ... H 1118.31; N D 1117.89 ... H 1119.51); step rises d/e/f/g = S 2 1/2, 2 1/2, 2 1/4, 2 3/8, N 4 13/16, 4 15/16, 4 13/16, 4 15/16; steps at mid-bay ("ABUTMENT STEP DIAGRAM, looking ahead station") | p.17 | RASTER |
| Cap bottom | sloped, S 1113.71 (west end) -> 1114.60; N 1113.91 -> 1115.64; level for the last 3'-0 at the east (wing) end | p.15, p.16, p.17 | TEXT / RASTER |
| Depth at the ends (step diagram) | west end (step D to bottom) S 3'-9 1/2, N 3'-11 3/4; east end (step H to the level bottom) S 3'-8 1/2, N 3'-10 1/2 | p.17 | RASTER. DERIVED check: 1117.50-1113.71 = 3.79 = 3'-9 1/2; 1118.31-1114.60 = 3.71 = 3'-8 1/2; 1117.89-1113.91 = 3'-11 3/4; 1119.51-1115.64 = 3'-10 1/2 - all exact. **This closes Woodbury U8** (footing depth) |
| Construction joint | at the cap top (CJ between pile cap and diaphragm), keyway formed by beveled 2x8 | p.15 | RASTER |
| Length along CL brg | S 36'-10 3/16 (3 5/16 + 36'-6 7/8 to the CL abut x gutter line); N 37'-10 1/2 | p.17 | RASTER (U5: 36'-6 15/16 on p.15) |
| West end | vertical face on the stage-1/2 construction joint, splice bars project (8f2 4'-11 proj., 5d9/5d11/5d12) | p.15, p.17 | RASTER |
| Wing extension (east only) | 6'-6 along the gutter line, wall 1'-7 thick OUTBOARD of the gutter line on a 3'-0 footing (1'-5 inside + 1'-7 outside), wall to the diaphragm, 5 - 6g4 each face; corner angle 100 deg 09'36" (S) / 75 deg 38'46" (N) to the abutment; footing bottom LEVEL at 1114.60 (S) | p.15 A-A, p.17, p.20 A-A | RASTER |
| Wing extension top | table of wingwall elevations A / B / C: S @ east gutter 1123.86 / 1123.76 / 1123.82, N 1125.12 / 1125.08 / 1125.11 (A, B, C positions along the wall from the p.20 part longitudinal section) | p.20 | RASTER |
| Wing extension wall to footing | 1'-9 min. embedment of the 6g4 bars; wall top 3" + 8" bar covers | p.20 A-A | RASTER |
| Piles | 8 HP12x84: 7 on CL brg (S: 2'-6 from CL NB edge, 6 @ 5'-7 = 33'-6; N: 2'-6, 5'-0 9/16, 8 7/16, 5 @ 5'-9 = 28'-9) + 1 in the wing extension (centred across: 1'-6 + 1'-6) | p.17 | RASTER |
| Pile top | 2'-0 into the cap ("2'-0 TOP OF PILE" above the cap bottom); 3" clear below | p.15 B-B | RASTER |
| Pile head spiral | 7 turns No. 2 bar, 21" dia, 3" pitch | p.15 note | TEXT |
| CMP casings | 24" CMP around each pile through the EPS fill, filled with flowable fill (sand + bentonite top), blocking 3x12; 281 LF total (= 17.6 ft per pile DERIVED) | p.17, p.5, p.3 | TEXT |
| Beam seat | beam on a 3 x 3 x 2'-4 1/2 steel bar on the step; 3/4" dia x 1'-3 coil rods; 4t1 bars under beams | p.15, p.20 | RASTER |
| Beam embedment | beam end 8" beyond CL brg into the diaphragm (end of beam to CL brg 8"); coil-tie insert 3" up, 5" from the end ("BEAM COIL TIE LOCATIONS - INTEGRAL ABUT.") | p.20, p.31 | RASTER |
| Concrete check | S footing 18.1 cy, N 19.5 cy (pile cap only; diaphragm is in deck sections 1 / 3: 66.9 / 71.3 cy incl. deck) | p.17, p.26 | RASTER |

### 2.2 Proposed inputs (C# sketch; all optional, null = today's behaviour)

```csharp
public enum AbutmentKind { Stub /* today: footing + backwall + corbel + beam steps */, Integral, Stem }

public sealed class AbutmentDefinition            // existing class, new members only
{
    public AbutmentKind Kind { get; set; } = AbutmentKind.Stub;
    /// <summary>x (from CL brg, + toward the span) of each pile row. Empty = the two legacy rows
    /// (-PileBackRowToBearing, +PileFrontRowToBearing). Woodbury: [0].</summary>
    public List<double> PileRowOffsets { get; set; } = new();
    /// <summary>Integral: the cap top is level per beam step = Steps[i].TopElevation from the S of the step
    /// boundary to the next one; boundaries at mid-bay (p.17), or explicit:</summary>
    public List<double> StepBoundaryS { get; set; } = new();     // empty = mid-bay between girder CLs
    public WingExtensionDefinition? WingExtension { get; set; }
    public PileCasingDefinition? PileCasing { get; set; }
    public double PileSpiralDiameter { get; set; } = double.NaN; // schedule only (p.15 note)
}

/// Iowa integral wing extension: wall outboard of the gutter line on its own level-bottom footing block.
public sealed class WingExtensionDefinition
{
    public AbutmentEnd End { get; set; }
    public double GutterOffset { get; set; }            // alignment offset of the gutter line (wall inner face)
    public double Length { get; set; }                  // 6'-6 along the gutter line from the abutment back face
    public double WallThickness { get; set; }           // 1'-7 outboard
    public double FootingInside { get; set; }           // 1'-5 inboard of the gutter line (footing 3'-0)
    public double FootingBottomElevation { get; set; }  // 1114.60 (level)
    public List<(double U, double Top)> WallTop { get; set; } = new(); // A/B/C table points, U along the gutter line
    public List<FootingPoint> Piles { get; set; } = new();              // wing pile (UNRESOLVED position, U7)
}

public sealed class PileCasingDefinition                 // CMP through EPS
{
    public double Diameter { get; set; }                 // 24"
    public double BottomElevation { get; set; } = double.NaN; // bottom of EPS: NOT in the set -> UNRESOLVED
    public string Fill { get; set; } = "";               // "flowable fill" (p.17)
}
```

Seat steps reuse `AbutmentStepDefinition` (girder + top). `FootingBottom` keeps the two end points plus a third point where the
bottom turns level (3'-0 from the east end, p.17) - expressible today.

### 2.3 Generator approach

- `BuildDesignedAbutment` branches on `Kind`. `Integral`: the footing solid's top becomes the step polyline (level pieces
  between `StepBoundaryS`, vertical risers) via `AddSlab` with breaks at the risers (every face planar - same technique as
  today's sloped bottom); NO backwall / corbel / steps solids (the diaphragm is feature C). `CapTop` profile and
  `unit.Backwall` get the step line so the existing views and the elevation check see it.
- Pile rows from `PileRowOffsets`; pile head embedment `PileTopAboveFootingBottom` = 2'-0 (existing field).
- Wing extension: wall = `OffsetPath` loft along the gutter line (reuse `BuildWing` geometry code with the counterfort part
  off), footing block = level-bottom prism under it; both `SubElementKind.Wingwall` / `WingFooting` (existing kinds).
- CMP casing: hollow cylinder around each pile from the cap bottom to `BottomElevation`; when NaN -> warning
  "UNRESOLVED: CMP casing bottom = bottom of EPS fill, not in this set (p.17)" and no solid (quantity 281 LF reported as plan value).
- Spiral: schedule-only bar mark.

### 2.4 IFC

Pile cap = IfcFooting .PILE_CAP. (existing `footingType`); wing extension wall = IfcWall 'WINGWALL'; CMP casing =
IfcBuildingElementPart 'PILE_CASING' .USERDEFINED. nested to its IfcPile (IfcRelAggregates); EPS fill: not modeled (by
others; optionally IfcEarthworksFill later).

### 2.5 Checks

- step elevations vs `BearingSeat` z minus the 3" bar (warning > 1/8");
- depth at both ends vs step-diagram values (3'-9 1/2 / 3'-8 1/2 / 3'-11 3/4 / 3'-10 1/2);
- piles inside the cap with 3" clear to the bottom, 2'-0 embedment; pile centred in casing;
- footing concrete volume vs 18.1 / 19.5 cy;
- deck end vs diaphragm back face (feature C).

### 2.6 UNRESOLVED (Woodbury abutment)

| id | item | p. |
|---|---|---|
| WA-1 | wing-extension pile position along the extension (p.17 chains 1'-6 / 1'-6 across; along: 1'-0 / 5'-0 / 6'-6, not yet read unambiguously) = old U7 | p.17 |
| WA-2 | CMP casing bottom (bottom of EPS, road plans IM-NHS-029-7(49)) | p.17 |
| WA-3 | positions of wingwall elevation points A / B / C along the extension | p.20 |
| WA-4 | north abutment details (p.16) - read before scripting; mirror is NOT assumed | p.16 |
| WA-5 | abutment length 36'-6 15/16 (p.15) vs 36'-6 7/8 (p.7/p.17) = old U5 | p.15 |

### 2.7 Implementation status (T1, 2026-10-11)

Done: `AbutmentKind { Stub, Integral }` (default Stub = unchanged), `PileRowOffsets` (both kinds; empty = legacy two rows),
`CapBackToBearing` / `CapFrontToBearing` (required for Integral), stepped cap top from `Steps` with risers at `StepBoundaryS` or
`StepRisersAtMidBay`, end-depth check `PlanCapDepthLeft/Right`, `PlanCapVolume`, `StepToSeat`, `WingExtensionDefinition`
(wall + level footing + piles, `WallTopPoint` table), `PileCasingDefinition` (+ `AbutmentGeometry.Casings` schedule records),
`PileSpiralDefinition` (schedule-only mark), `SubElementKind.PileCasing`. Code: `Substructure/SubstructureAbutmentIntegral.cs`; the
stub path is untouched except the pile rows. Tests: `tests/SBM.Tests/IntegralAbutmentTests.cs` (11; the p.17 depths 3'-9 1/2,
3'-8 1/2, 3'-11 3/4, 3'-10 1/2 are reproduced from the step / bottom elevations within 1/8").

Deviations from the sketch in 2.2, all to avoid invented defaults: `StepBoundaryS` empty no longer means mid-bay - mid-bay needs
`StepRisersAtMidBay = true` (Q3); without either the risers are UNRESOLVED and the cap solid is not built (piles, depths and checks
still are). Wing extension: `FootingTopElevation` (wall bottom) and `PileTopAboveFootingBottom` are explicit inputs (not printed as far
as read) - NaN = UNRESOLVED; `PlanPileCount` > positioned piles = UNRESOLVED (WA-1). Casing: NaN bottom = UNRESOLVED (WA-2 / Q5),
no solid, records with unknown length; with a bottom, a 24-gon envelope solid (casing + fill) per pile. Spiral length uses the
printed diameter as the helix diameter. Integral rebar: nothing placed in 3D; the plan bar list is schedule-only.

Still open / waiting: Q3 (riser position), Q5 / WA-2 (casing bottom), WA-1 (wing pile position), WA-3 (A/B/C positions), WA-4 (north
abutment p.16), wing footing top elevation; Woodbury script `samples/plans/abutments-woodbury.csx` not written (needs WA-1/3/4).
T3: IFC mapping of `PileCasing` (currently falls to the IfcFooting default; only built once a casing bottom is entered) and the
abutment view panels for the stepped cap (views currently reuse the stub panels with the cap as the "backwall").

---------------------------------------------------------------------------------------------------------------------

## 3. Feature B: MnDOT stem abutment for a semi-integral end diaphragm (Br02069)

### 3.1 Plan geometry given

| item | value | p. | tag |
|---|---|---|---|
| Footing | 59'-7 (offset -23.58 to +36.00) x 6'-0 x 3'-0, bottom 917.75; back toe 9", stem 4'-0, front toe 1'-3 | p.4, p.5, p.11 A-A | TEXT / RASTER |
| Stem | 4'-0 on the footing: back ledge 1'-6 + seat 2'-6 (1'-0 behind CL brg + 1'-6 to the front face); ledge 1'-0 below the seat; footing -> ledge 1'-7 1/4 S / 1'-4 3/4 N; seat slopes 1" toward the front | p.11 A-A (note 3) | RASTER |
| Seat front | S 923.27, N 923.06 level; ledge S 922.35, N 922.15 DERIVED | p.5, p.10 | TEXT |
| Keyways | 2" x 12" CJ keyway on the footing top (note 5) | p.11 | TEXT |
| Pedestals | S: 10 x 4'-8 (along) x 2'-6 (across, 1'-0 + 1'-6 about CL brg), gaps 1'-4, ends 8" E / 3" W; N: 10 x 3'-6 x 2'-6, gaps 2'-6; 7 beam pedestals Type A + 3 FUTURE Type B (east) | p.5, p.10, p.6 | TEXT / RASTER |
| Pedestal tops | per pedestal (S 923.52, 923.64, 923.76 future; 923.88 ... beam ones = seat-elevations.csv) | p.5, p.10 | TEXT |
| Pedestal types | A = beam (anchor rods 1 1/2" @ +-1'-9 1/2, A525E/A526E, A529E only where pedestal >= 9"); B = future (A527M/A528M, no anchors); bars 2'-4 + 2'-4 about CL beam | p.6 | RASTER |
| Infill wall | 1'-6 thick on the back ledge, top 927.73 S / 927.80 N (level); S: from the east end over 18'-5 (the future-widening part, up to the end diaphragm with 1" Type B polystyrene, Detail C) | p.5, p.11 B-B, p.11 Detail C | TEXT / RASTER |
| Mask wall (west end) | 1'-5 1/2 thick, 6'-4 along the front face, top 930.61 S / 930.67 N, 12'-10 3/8 / 12'-11 high from the footing bottom 917.75, coping 1'-1 x 2", 4" beyond the wall, emblem recess | p.11 C-C, D-D, mask wall elevation | RASTER |
| Piles | 12" CIP pipe, 1/4" shell, 2 rows x 7 @ 9'-5 (ends 1'-6 W / 1'-7 E), back row 1'-9 behind CL brg, front 1'-3 ahead, 1'-0 into the footing, plumb | p.4, p.9, p.11 | RASTER |
| Pile lengths | S 13 x 95 + test 105; N 13 x 90 + test 100 | p.4, p.9 | TEXT |

### 3.2 Proposed inputs

```csharp
/// Stem kind: the abutment body above the footing is an arbitrary closed polygon in (x from CL brg, z) where z is an
/// absolute elevation that may vary with S through named reference lines (seat front, ledge, footing top).
public sealed class StemSectionPoint { public double X { get; set; } public string Ref { get; set; } = ""; public double Dz { get; set; } }
// Ref in { "FootingTop", "Ledge", "SeatFront", "SeatBack" }; each ref = List<CapProfilePoint> (1 point = level)

public sealed class AbutmentDefinition  // new members, Kind = Stem
{
    public List<StemSectionPoint> StemSection { get; set; } = new();
    public List<CapProfilePoint> SeatFront { get; set; } = new();   // 923.27 (level)
    public List<CapProfilePoint> Ledge { get; set; } = new();       // 922.35 DERIVED -> give as plan chain (1'-0 below seat back)
    public List<PedestalDefinition> Pedestals { get; set; } = new();
    public List<InfillWallDefinition> InfillWalls { get; set; } = new();
}

public sealed class PedestalDefinition
{
    public double CenterOffset { get; set; }  // alignment offset of the pedestal CL (or S); beam ones match a girder
    public double SizeAlong { get; set; }     // 4'-8 (S) / 3'-6 (N)
    public double SizeAcross { get; set; }    // 2'-6
    public double AcrossFromBearing { get; set; } // -1'-0 (back face 1'-0 behind CL brg)
    public double TopElevation { get; set; }
    public string Type { get; set; } = "";    // "A" / "B"
    public bool Future { get; set; }          // no beam: no seat check, IFC property Future = true
    public int Girder { get; set; } = -1;     // 1-based girder for the seat check; -1 = none
}

public sealed class InfillWallDefinition
{
    public double FromOffset { get; set; }  public double ToOffset { get; set; }   // alignment offsets of the ends
    public double X0 { get; set; }          public double X1 { get; set; }         // across (from CL brg), on the ledge
    public double TopElevation { get; set; }                                       // 927.73 level
}

public sealed class MaskwallDefinition   // existing, new optional members
{
    public double TopElevation { get; set; } = double.NaN;   // absolute top (930.61); NaN = Height above the footing top (today)
    public double Length { get; set; } = double.NaN;          // 6'-4 along F.F.; NaN = spans the footing width (today)
    public bool FromFootingBottom { get; set; }               // p.11 C-C
    public double CopingDepth { get; set; } double CopingStep { get; set; } // 1'-1 x 2"
}

public enum PileType { SteelHP, PrecastConcrete, CipPipe }      // + PileDiameter (12"), PileShellThickness (1/4")
```

### 3.3 Generator approach

- `Kind = Stem`: one `AddSlab` prism per `StemSection` between the footing end planes (same plane functions as today), each
  vertex z = its reference line at S + Dz; the 1" seat slope is two references (SeatFront, SeatBack). Footing as today
  (the generic rectangle section, keyway as a note).
- Pedestals: explicit `PedestalDefinition` blocks replace the seat-derived steps; beam pedestals are matched to
  `BearingSeat`s by girder (check), future ones get no seat. `unit.Pedestals` entries carry `Future`.
- Infill walls / mask walls: prisms between offset planes (existing `EndPlane`); mask wall with absolute top and own length.
- CIP pipe pile: round section (circle n=24) with the shell as a separate thin ring solid or a property (proposal: ONE
  IfcPile, ConstructionType COMPOSITE, property set ShellThickness = 1/4"; no separate shell solid).

### 3.4 IFC

Stem = IfcWall 'ABUTMENT_STEM' .RETAININGWALL. (or IfcFooting? no - a stem is a wall); pedestals = IfcColumn 'PEDESTAL'
(existing) + Pset `Future`; infill wall = IfcWall 'INFILL_WALL' .SOLIDWALL.; mask wall = existing; CIP pile = IfcPile
.DRIVEN. ConstructionType .COMPOSITE..

### 3.5 Checks

Pedestal top vs `BearingSeat` (beam ones); pedestal height >= 9" flags A529E (p.6 note 1); pedestal within the seat
outline; infill wall end vs end diaphragm end (1" gap, Detail C); pile rows within the footing (1'-6 / 1'-7 end
distances); abutment concrete vs the SOQ split 71 / 69 cy (p.2; includes the end diaphragm share - info line only).

### 3.6 UNRESOLVED (Br02069 substructure)

| id | item | p. |
|---|---|---|
| MB-1 | infill wall extent at the NORTH abutment and the mask-wall / infill corner (old U9) | p.10, p.3 |
| MB-2 | pile tip elevations (old U8; lengths are given, tips are DERIVED only) | p.4, p.9 |
| MB-3 | seat slope 1" "to front": is the seat back edge 1" higher, or the slope over 2'-6 measured differently | p.11 A-A |

---------------------------------------------------------------------------------------------------------------------

## 4. Feature C: concrete end diaphragms and pier continuity diaphragms (cast with the deck)

### 4.1 Plan geometry given

**Woodbury abutment diaphragm** (integral):

| item | value | p. | tag |
|---|---|---|---|
| Section | 3'-0 wide = pile cap width (front face = cap front face, back face = cap back face = deck end), from the CJ on the stepped cap top to the top of deck | p.15 B-B, p.20 | RASTER |
| Paving notch | on the back face at the top (5k1/5k2, 5d7, 1" chamfer); dims 1'-1 (constant depth), 1'-3, 1'-3, 1'-3 and "6 TYP." in B-B / longitudinal section | p.15 B-B, p.20 | RASTER - mapping of the dims to the outline still to be read (WC-1) |
| Beams | embedded: beam end 8" behind CL brg (inside the 1'-6 back half), beam on the 3x3 bar | p.20, p.15 | RASTER |
| Ends | west: stage CJ (vertical, flush with the cap end); east: runs into the wing extension wall | p.15 rear elev. | RASTER |
| Bars | 8g1 verticals both faces, 8g3, 5d2/5d3/5d6..5d8, 5k1/5k2; bar list p.26 | p.26 | TEXT |

**Woodbury pier continuity diaphragm** (beams continuous over the pier, "fixed pier"):

| item | value | p. | tag |
|---|---|---|---|
| Beam ends | CL brg 1'-0 each side of CL pier (along CL NB); end of beam 8" beyond the CL brg -> beam ends 4" from CL pier, 8" apart | p.20 longitudinal section, Detail A, p.21 | RASTER |
| Width | 2'-10 (dimension in "PART SECTION AT PIER"); DERIVED check: hoops 5e1/5e2 2'-6 o-o + 2 x 2" cover = 2'-10 | p.20, p.26 bend diagram | RASTER / DERIVED |
| Height | top of deck to the pier cap top (pedestal steps of the cap under each beam); hoops 5e1 5'-4 / 5e2 4'-11 tall | p.20, p.26 | RASTER |
| Ends | flush with the outside face of fascia beam H (Detail B: "diaphragm concrete finished smooth and flush to adjacent beam surfaces"); at beam D the diaphragm ends at the stage-2 edge of deck, 5d16/5d18 project 2'-3 | p.20 Detail B, part section at pier | RASTER |
| Interface | keyway (beveled 2x8) in the cap top between the beams; 1" preformed expansion joint filler around bearings, faces of steps, sides/ends of keyways; 1/4" filler (section B-B) | p.20 top of pier details | RASTER |
| Bearings | plain neoprene pad 9 x 1 x 2'-4 1/2 per beam end | p.20 | RASTER |
| Coil ties | fixed pier: two inserts 3" up, 5" and 9" from the beam end | p.31 | RASTER |
| Concrete | deck sections 4 / 5 (pier) 45.1 / 45.0 cy incl. deck | p.26 | RASTER |

**Br02069 end diaphragm** (semi-integral, MnDOT; std Fig. 5-397.560 p.17 for the beam connection):

| item | value | p. | tag |
|---|---|---|---|
| Width | 3'-0 = 1'-6 + 1'-6 about the CL end diaphragm; CL end diaphragm 1'-0 behind CL brg; front face 6" ahead of CL brg; back face = abutment back face (2'-6 behind CL brg) | p.20 plan, p.21 A-A / B-B | RASTER |
| Back part | 1'-3 (+3" gap) behind the stem seat drops down along the stem back face to the ledge ("VARIES", S609E B.F. 2 eq. spaces + 2 eq. spaces, 4" at the bottom); Detail A p.11: 3" Type B polystyrene vertical, 1" Type A horizontal, 1" backer rod | p.21, p.11 Detail A | RASTER |
| Top | deck top; paving bracket 8"; 1'-4 at the approach panel side; 4" / 5 1/2" / 8" chain to the approach panel | p.21 | RASTER |
| Ends | west: front face of diaphragm against the mask wall (1" polystyrene); east: 1" polystyrene Type B against the infill wall | p.20 | RASTER |
| Beams | 30MH ends embedded; 1 1/2" web holes for rebar D = 1'-1 1/4 above the bottom, E = 7 1/2 from the end, C = 3 1/2 (two holes); 2-S506E through beams | p.17 table (30MH row), p.20 | RASTER |
| Bars | S405E..S617S (bill of reinforcement p.21) | p.21 | TEXT |

### 4.2 Where it lives and why

The diaphragm is poured with the deck (Woodbury placement sections 1, 3, 4, 5 count "deck & diaphragm"; MnDOT bills it
with the superstructure) and its geometry depends on (a) the beams (embedment pockets), (b) the deck soffit / top and
(c) the substructure top it sits on (stepped cap / stem seat / pier cap). Therefore a NEW generator runs after the
prestressed beams AND the substructure:

- new folder `src/SBM.Core/Diaphragms/`: `ConcreteDiaphragmGenerator.cs`, `DiaphragmModel.cs` (+ later the steel detailer of feature D);
- `BridgeModel` gets `Diaphragms` (one line in `RegenerationCache` phase 2, after the substructure of the bridge);
- input: `SupportDefinition.Diaphragm` (new property, new file `Bridge/DiaphragmDefinition.cs` for the class).

```csharp
public enum ConcreteDiaphragmKind { IntegralEnd, SemiIntegralEnd, PierContinuity }

public sealed class ConcreteDiaphragmDefinition
{
    public ConcreteDiaphragmKind Kind { get; set; }
    /// <summary>Section normal to the support, x from CL brg (+ toward the span; at piers from CL pier, + ahead):
    /// closed polygon of (x, z-reference, dz). z refs: "DeckTop", "Soffit", "CapTop" (substructure top at S: stepped cap,
    /// stem seat, pier cap), "Ledge". The beams are cut out automatically.</summary>
    public List<StemSectionPoint> Section { get; set; } = new();
    /// <summary>Plan ends: alignment offsets (NaN = flush with the outside face of the fascia beam, Woodbury Detail B).</summary>
    public double LeftEndOffset { get; set; } = double.NaN;
    public double RightEndOffset { get; set; } = double.NaN;
    /// <summary>Gaps / fillers as plan items (schedule + thin solids optional): polystyrene 1" / 3", preformed filler 1".</summary>
    public List<DiaphragmFillerDefinition> Fillers { get; set; } = new();
    /// <summary>Cast-in items in the beam ends (coil-tie inserts, 1 1/2" web holes): x from the beam end, y above beam bottom.</summary>
    public List<(double FromEnd, double AboveBottom, double Diameter, string Kind)> BeamInserts { get; set; } = new();
    public List<PlanBarLine> PlanBarList { get; set; } = new();     // schedule-only marks first (same rule as abutments)
    public bool SplitAtSoffit { get; set; } = true;                  // deck above the soffit stays in the deck solid
}
```

Woodbury examples: abutment Section = [(-1'-6, CapTop), (+1'-6, CapTop), (+1'-6, Soffit), (-1'-6 + notch..., DeckTop ...)];
pier Section = [(-1'-5, CapTop), (+1'-5, CapTop), (+1'-5, Soffit), (-1'-5, Soffit)]. Br02069: front part from the seat level,
back part down to `Ledge` + 3" gap (bottom of the front part = WC-3 UNRESOLVED).

### 4.3 Geometry method (exact, no booleans)

- The beams of one span are parallel within the diaphragm, so a vertical plane parallel to the support line cuts every
  beam in the same outline, stretched by 1/cos(theta) along S (theta = beam axis to support normal) and shifted by
  N tan(theta). Build the diaphragm as PANELS from CL beam i to CL beam i+1: a simple polygon (no holes) in that plane:
  soffit line -> down the CL of beam i to the beam top -> along the right half of beam i's outline to its bottom -> cap top
  -> up beam i+1's left half -> its CL -> soffit. Panels are lofted between the planes x0..x1 with `MeshBuilder.AddLoft`
  (same vertex count each ring; top vertices follow the soffit / deck top at their own S, bottom vertices the cap top =
  the `AddSlab` technique). Beyond the beam end (x past the end face) the panel is the full rectangle (beam end plane =
  `PrestressedBeam` end face, parallel to the support at abutments, square at bents). End panels run from the fascia beam
  CL to `Left/RightEndOffset` or to the fascia outside face.
- Pier diaphragm: the same per side of the CL pier (two half-diaphragms with the back and ahead beams, joined in the 8"
  gap between beam ends as a full rectangle).
- Inputs read only: `PrestressedModel.Beams` (outline = `Shape.Outline()`, axis, end faces), `ConcreteModel` soffit
  (`DeckBottomAt`) and finished grade, `SubstructureUnit.CapTop` / pedestal tops. No edits to Prestressed/, Framing/,
  Concrete/ or Substructure/ code are needed for the solids (only accessors; if `DeckBottomAt` is private, add a public
  wrapper in a separate commit).

### 4.4 IFC

IfcBeam .DIAPHRAGM. (IFC4X3 `IfcBeamTypeEnum` has DIAPHRAGM) per support, ObjectType 'END_DIAPHRAGM' / 'PIER_DIAPHRAGM',
contained in the superstructure IfcBridgePart; panels merged into one body per support. Fillers = IfcBuildingElementPart
'JOINT_FILLER'; cast inserts = IfcDiscreteAccessory 'INSERT'. Bars: IfcReinforcingBar as for the abutments.

### 4.5 Checks

- beam end inside the diaphragm with the plan embedment (Woodbury 8" behind CL brg; Br02069 E = 7 1/2 + hole positions inside);
- diaphragm top meets the deck soffit (no gap) and the deck end = diaphragm back face (Woodbury 1'-6 1/4 along CL NB);
- pier: beam ends 8" apart, both inside the 2'-10, the diaphragm bottom on the cap top between the beams, pads 9" clear of
  the diaphragm faces;
- web holes / inserts vs strands (deflected strands at the ends rise to 4" below the top on Woodbury) - clearance warning;
- volume per support as info vs the placement table (not a hard check: the plan counts deck + diaphragm together).

### 4.6 UNRESOLVED (diaphragms)

| id | item | p. |
|---|---|---|
| WC-1 | Woodbury paving-notch outline on the diaphragm back face: which of 1'-1 / 1'-3 / 1'-3 / 6" belong to the notch vs the bar layout (std 2087-BTCD) | p.15 B-B, p.20 |
| WC-2 | Woodbury pier diaphragm: centred on the CL pier (DERIVED from the symmetric 1'-0 / 1'-0) - the 2'-10 is dimensioned only at the stage-joint end | p.20 |
| WC-3 | Br02069 end diaphragm: bottom of the front (span-side) part between beams (no elevation or depth; "VARIES" only on the back part) = old U2 | p.21 A-A / B-B |
| WC-4 | Br02069: exact top profile at the approach panel (4" / 5 1/2" / 8" / 1'-4 chain - read in the task) | p.21 |

---------------------------------------------------------------------------------------------------------------------

## 5. Feature D: steel intermediate diaphragms between prestressed beams (Woodbury, Iowa std 1036-1/2-BTD modified)

### 5.1 Plan geometry given (p.31 / DS 29, p.32 / DS 30)

| item | value | tag |
|---|---|---|
| Layout | one diaphragm line per span near midspan, staggered per bay; bolt-hole chains per beam line from the beam end (`diaphragm-bolt-holes.csv`) | RASTER |
| Members | C15x33.9 (11), W21x50 + W14x38 (1, between beam lines G and H in span 2 "over the traveled roadway", W14x38 under the W21x50 with 18 bolts, 8 equal spaces, 6" ends); lengths per bay: W21x50 6'-9 5/8 @ 7'-11 13/16; C15 6'-10 @ 8'-0 5/16 (4), 6'-9 1/2 @ 7'-11 13/16 (3), 6'-8 3/4 @ 7'-11 (4); W14x38 5'-4 7/8; total 4,258 lb | RASTER |
| Beam web holes | 1 1/2" dia CAST holes (drilling not allowed); two holes per connection, lower 1'-8 1/2 above the beam bottom, upper 9 3/8 above it ("Intermediate diaphragm bolt hole locations") | RASTER |
| Connection | bent PL 9 x 6 x 1/2 x 1'-1 3/8 on the diaphragm side + backing PL 6 x 3/8 x 1'-1 3/8 on the far face; 2 - 7/8" x 9 1/4 H.S. bolts (A449) through the web; 6" leg holes 1 1/16; 9" leg 1" x 2" horizontal slots, 4 @ 3 1/8 (3 @ 3 1/8) with 2" ends, 3 1/2" from the bend; member 1" x 1 1/2" vertical slots, 1 5/8 from the member end; 8 - 7/8 x 2 3/4 bolts per diaphragm; equal "a" dimensions | RASTER |
| Detail D vs F | D = bent plate at an interior beam (W21x50 / C15 pair), F = exterior bay; 2 x D, 22 x F connections (table) | RASTER |
| Galvanized; tightened before placing the deck | TEXT |

### 5.2 Why not CrossFrameDetailer

`CrossFrameDetailer` is a partial of `SteelModelGenerator` that needs steel girders: frame planes between WEB FACES of
plate girders, connection stiffeners to bolt to, `_stiffHoles` / `_holes` registers and `CrossFrameResult`s produced by
`FramingGenerator.BuildCrossFrames` - which is skipped for prestressed (`if (!PS)`) and Framing/ is being edited by
another agent. The Iowa diaphragm has no stiffener: it bolts through cast holes of a concrete web whose face is the
bulb-tee web (6 1/2" thick, BTD). A small standalone detailer reusing the catalog (`SteelCatalog`: C15x33.9, W21X50, W14X38
are in `AiscShapes.csv`), `SteelPart`, `BoltInstance`, `ChannelPolygon` is less risky and keeps the steel path untouched.

### 5.3 Proposed inputs (new file `Bridge/PsSteelDiaphragmDefinition.cs`)

```csharp
public sealed class PsDiaphragmHole            // one cast hole pair in a beam web (the CSV chain)
{
    public int Span { get; set; } public int Girder { get; set; }    // 1-based
    public double FromBeamEnd { get; set; }                          // along the beam from the end at the lower station
}
public sealed class PsDiaphragmBay
{
    public int Span { get; set; } public int LeftGirder { get; set; }  // bay LeftGirder..LeftGirder+1
    public string Member { get; set; } = "C15x33.9";                    // or "W21x50"
    public string? Lower { get; set; }                                   // "W14x38" (G-H span 2)
    public double PlanLength { get; set; } = double.NaN;                 // table length (check)
    public string Connection { get; set; } = "F";                        // "D" / "F" (std detail)
}
public sealed class PsSteelDiaphragmDefinition
{
    public List<PsDiaphragmHole> Holes { get; set; } = new();
    public List<PsDiaphragmBay> Bays { get; set; } = new();
    public double LowerHoleAboveBottom { get; set; }   // 1'-8 1/2 (standard 1036-BTD)
    public double HolePitch { get; set; }             // 9 3/8
    public double HoleDiameter { get; set; }          // 1 1/2
    public PsDiaphragmConnection Connection { get; set; } = new(); // bent PL 9x6x1/2x1'-1 3/8, backing PL 6x3/8, bolts, slots
    public double PlanSteelWeightLb { get; set; } = double.NaN;  // 4,258 (check)
}
```

The standard values (bent plate, hole heights, bolt sizes) are the Iowa 1036-BTD standard: one preset
`PsSteelDiaphragmDefinition.IowaBtd()` filled from p.31/p.32, not designed per bridge (user rule).

### 5.4 Generator approach (`Diaphragms/PsSteelDiaphragmDetailer.cs`)

Per bay: the two hole stations on the two beams (from the beam END along the beam axis, plan convention "increasing
station"), hole elevations from each beam bottom; web faces from the beam shape (web 6 1/2"); bent plate on the bay side,
backing plate on the far face, 2 bolts through the web; member between the 9" legs (end 1 5/8 beyond the last slot
CL, 4 bolts each end). Output `SteelPart`s (Category Channel / WideFlange / Plate), `BoltInstance`s, and the hole list
(for the beam views and the strand check) into `DiaphragmModel.Steel`.

### 5.5 IFC

Members IfcMember .MEMBER. ObjectType 'INTERMEDIATE_DIAPHRAGM' (as the existing channel diaphragms), plates IfcPlate,
bolts IfcMechanicalFastener .BOLT., one IfcElementAssembly per diaphragm (.BRACED_FRAME. or USERDEFINED 'DIAPHRAGM');
cast holes as IfcOpeningElement in the IfcBeam (optional; the beams are triangulated meshes - start with a property).

### 5.6 Checks

hole chain sums = beam end-to-end length per span (the CSV note); hole vs strand clearance (deflected strands at the
diaphragm station: BTD105 hold-down 10'-6 from midspan, BTD60 6'-0 - the holes are ~1'-6 off midspan, so within the
straight part; still compute); member length vs table; total weight vs 4,258 lb; bolts clear of the top/bottom flange fillets
(R 8).

### 5.7 UNRESOLVED (steel diaphragms)

| id | item | p. |
|---|---|---|
| WD-1 | member elevation / slope: the hole heights are fixed above each beam bottom but adjacent beam bottoms differ by spacing x cross slope (8'-0 x 5 % = 4.8"); p.32 labels the W21x50 / W14x38 "(LEVEL)" and requires equal "a" dimensions, while the slots take only 1/2" (1" x 1 1/2"). Is the member level (and how does the connection absorb 4.8"), or parallel to the line through the two hole pairs? | p.32 |
| WD-2 | which beam of each bay gets Detail D vs F (table counts only: 2 D, 22 F) | p.31 |

---------------------------------------------------------------------------------------------------------------------

## 6. Feature E: abutment bearings for prestressed beams (after the Prestressed/ agent)

| set | bearing | plan | tag |
|---|---|---|---|
| Woodbury abutments | 3 x 3 x 2'-4 1/2 steel bar under each beam on the step (beam bearing 3"), coil rods 3/4" x 1'-3 | p.15, p.20, p.25 | RASTER |
| Woodbury piers | plain neoprene 9 x 1 x 2'-4 1/2 (existing pad inputs; pad long side PARALLEL to the pier in the plan - today's pads are square to the girder: check with the Prestressed/ agent) | p.20 | RASTER |
| Br02069 S (fixed F1, B310) | pad 12 x 30 x 1/2, bearing plate 14 x 47 x 1 1/2, curved plate 4 1/2 x 32 x 1 1/4 R 16, L 3 1/4; anchor rods 1 1/2 @ +-1'-9 1/2 | p.31, p.5 | RASTER |
| Br02069 N (expansion E1, B311) | pad 12 x 30 x 3 3/4 (6 plates 1/8, 5 lam 1/2, 1/4 covers), plate 14 x 33 x 1 1/2, curved plate as F1, L 6 1/2 | p.31 | RASTER |
| Br02069 sole plate | B303, cast with the beam | p.30 | UNRESOLVED (old U6) |

Proposal: `PrestressedBearingDefinition` per support (new file), kinds `Pad` (today), `SteelBar` (w x h x L), `CurvedPlate`
(pad, bearing plate, curved plate radius / axis, anchor rods), built by a new `Bearings/PrestressedBearingBuilder.cs` and
CALLED from `PrestressedGenerator` (one call replacing the pad block) - that call is the only edit in Prestressed/ and waits
for the other agent. IFC: IfcBearing .CYLINDRICAL. (curved plate) as an IfcElementAssembly with the pad (.ELASTOMERIC.) and
plates (IfcPlate); steel bar = IfcBearing .USERDEFINED. 'STEEL_BAR'. The `BearingSeat` stack height feeds the existing
elevation check (Woodbury seat = beam bottom - 3"; Br02069 L 3 1/4 / 6 1/2).

---------------------------------------------------------------------------------------------------------------------

## 7. Implementation order and agent-sized tasks

Concurrency constraints now: one agent edits `Prestressed/` (beam catalog, per-span definitions, debonding, harping), one
edits `Framing/` (chords per span, PGL offset). Hot shared files: `Project/RegenerationCache.cs`, `SBM.IO/Ifc/IfcExporter.cs`,
`Bridge/BridgeDefinition.cs` (girder layout -> Framing agent), `Bridge/PrestressedDefinition.cs` (Prestressed agent).
Serialization: definitions serialize directly (new properties need no DTO work; keep `CurrentVersion` unless a field is renamed).

| task | content | files (exclusive) | depends on | can start |
|---|---|---|---|---|
| T1 Abutment kinds - Integral | `AbutmentKind`, `PileRowOffsets`, stepped cap top, wing extension, CMP casing (UNRESOLVED path), spiral schedule; tests; Woodbury S/N abutment script `samples/plans/abutments-woodbury.csx` (after reading WA-1/WA-3/WA-4) | `Bridge/AbutmentDefinition.cs`, `Substructure/SubstructureAbutment*.cs`, `Substructure/SubstructurePiles.cs` (casing), new tests | none | NOW |
| T2 Abutment kinds - Stem | `StemSection`, explicit `Pedestals` (future), infill walls, mask wall top/length, `PileType.CipPipe`; Br02069 script | same files as T1 + `Bridge/SubstructureDefinition.cs` (PileType) | T1 merged (same files: run T1 then T2, or one agent doing both as two commits) | after T1 |
| T3 IFC + views for T1/T2 | new IfcWall/IfcColumn/IfcPile mappings, casing part; `AbutmentViews` panels for stepped cap / stem | `SBM.IO/Ifc/IfcExporter.cs` (substructure block only), `Drawing/Views/AbutmentViews.cs` | T1, T2 | after T2 |
| T4 Steel intermediate diaphragms (prestressed) | `Bridge/PsSteelDiaphragmDefinition.cs`, `Diaphragms/PsSteelDiaphragmDetailer.cs`, `Diaphragms/DiaphragmModel.cs`, preset `IowaBtd()`, unit tests against `diaphragm-bolt-holes.csv` and the member table; NOT hooked into regeneration yet (tests call it with a generated PrestressedModel) | new files only | read-only use of `PrestressedModel` (API may change under the Prestressed agent: rebase) | NOW |
| T5 Concrete diaphragms | `Bridge/DiaphragmDefinition.cs`, `Diaphragms/ConcreteDiaphragmGenerator.cs` (panel method), checks; tests on a synthetic 2-span bridge | new files only | T1 (cap top steps), T2 (stem seat refs); read-only PrestressedModel / ConcreteModel | after T1 (T2 for Br02069) |
| T6 Hook-up | `SupportDefinition.Diaphragm` / bridge-level `PsSteelDiaphragms` property, `BridgeModel.Diaphragms`, phase-2 call in `RegenerationCache`, IFC export of diaphragms, 3D scene layer | `Bridge/BridgeDefinition.cs` (support class only), `Project/RegenerationCache.cs`, `SBM.IO/Ifc/IfcExporter.cs`, app scene builder | Prestressed + Framing agents MERGED; T4, T5 | after those merges |
| T7 Bearings | `PrestressedBearingDefinition`, `Bearings/PrestressedBearingBuilder.cs`, one call in `PrestressedGenerator`; 3x3 bar + curved plate F1/E1; seat stack in the elevation check | new files + one call site in Prestressed/ | Prestressed agent merged | after that merge |
| T8 Plan-data scripts + comparisons | `samples/plans/woodbury-*.csx`, `br02069-*.csx` for abutments / diaphragms / bearings; quantity and elevation comparisons into `docs/plan-data/.../SUMMARY.md` | samples + docs | T1-T7 | last |

Parallel now: T1 and T4 (disjoint files). T5 can be written against the T1 branch. T6 is the single integration point and
should be ONE agent after the Prestressed/ and Framing/ branches merge.

---------------------------------------------------------------------------------------------------------------------

## 8. Open questions for the user

1. Ownership: is it right to model the integral / end / pier diaphragms as SUPERSTRUCTURE elements (own generator after the
   substructure, IfcBeam .DIAPHRAGM.), split from the deck slab at the soffit, with the pile cap / stem / wing extension in the
   substructure? (Alternative: deck solid continues down into the diaphragm as one pour solid.)
2. Woodbury pier diaphragm: confirm it is centred on the CL pier, 2'-10 wide (p.20 dimension, consistent with the 5e1 hoops),
   ending flush with beam H outside face and at the stage-2 edge of deck at beam D.
3. Woodbury steps: the step diagram (p.17) places the risers between the beams - at MID-BAY, or at a dimension from each beam
   CL that the standard 2087-BTCD gives? (Proposal: mid-bay only if you confirm; otherwise UNRESOLVED.)
4. Steel diaphragm member elevation (WD-1): level as labelled on p.32, or along the line through the two beams' hole pairs
   (5 % cross slope = 4.8" difference per bay)?
5. CMP casings: model them with an UNRESOLVED bottom (no solid until the EPS bottom elevation is supplied), or do you have the
   road plans' EPS bottom elevation?
6. Br02069 end diaphragm front-part bottom (WC-3): leave UNRESOLVED (no solid below the beam top?) or do you accept the MnDOT
   semi-integral standard (B816 family) as the source - if so, which standard sheet/figure should be read?
7. Br02069 future pedestals (Type B): model them as substructure pedestals flagged Future (IFC property), no bearing/beam - OK?
8. CIP pipe pile: one IfcPile (COMPOSITE) with the 1/4" shell as a property, or a separate shell solid?
9. Woodbury pier pads: plan shows the 9 x 2'-4 1/2 pads parallel to the CL pier (p.20 part plan) while the generator places
   pads square to the girder - handled by the Prestressed/ agent or in T7?
10. Should the 1" / 3" polystyrene, preformed fillers and 2x8 keyways be 3D solids or notes/schedule items only?
