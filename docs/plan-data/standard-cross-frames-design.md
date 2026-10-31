# Standard ("horizontal") cross frames - design (2026-10-27)

Status: DESIGN ONLY, for the user's review. Nothing in `src/` or `tests/` has been changed. Implementation comes in a later run.

User's practice (2026-10-27): design-builders prefer every cross frame to be the SAME frame. The engineer sets the top-left work point
and ONE frame depth; the frame is straight (level chords), a little shorter than a full-depth frame, and the slop between the standard frame
and each girder goes into the HOLE POSITIONS on the connection plates (the hole group moves along the plate; no slotted holes). The
program finds and reports the deepest common frame and the governing location, flags every location where the entered depth does not fit, and
measures the twist between the two connection plates of each frame. It never picks the depth itself and never fills missing plan data
with code-rule defaults.

---------------------------------------------------------------------------------------------------------------------------------------

## 1. Current code path (as of main, 2026-10-27)

| What | Where |
|---|---|
| Inputs: `CrossFrameDefinition` (types, orientation, arrangement, members, `TopWorkPointOffset` 6" below top of web, `BottomWorkPointOffset` 6" above the bottom flange, `GussetThickness`, `MemberClearance`, `WeldLap`, `Styles`, `IntermediateStyle`, `SupportStyles`, `SupportBayStyles`, `Lines`, `Links`) | `src/SBM.Core/Bridge/BridgeDefinition.cs:736-810` (WP offsets :765-768) |
| `CrossFrameStyle` (kind, sections, bolts, columns / gage, `TopBoltRows`, `BottomBoltOffset`, per-style WP offsets NaN = definition's) | `BridgeDefinition.cs:826-879` (WP overrides :855-857) |
| Frame placement (plan): support frames per support line, aux support lines, `Links`, explicit `Lines`, automatic spacing / staggered / parallel-to-skew | `src/SBM.Core/Framing/FramingGenerator.cs:972-1181` (`BuildCrossFrames`; supports :978-1000, aux :1003-1021, `BuildLinks` :1183, `BuildFrameLines` :1231, automatic :1040) |
| Result per frame: `CrossFrameResult` (Bay, RightGirder, AtSupport, SupportIndex, Left/Right girder distances, Style) | `src/SBM.Core/Framing/FramingResults.cs:171-199` |
| Connection stiffeners derived from the intermediate frames (pairs through the web when both bays have a frame on one line) | `FramingGenerator.cs:1386-1413` (pair merge :1398) |
| Connections phase: `BuildCrossFrame` per frame (parallel) | `src/SBM.Core/Steel/SteelModelGenerator.cs:142` |
| Frame plane: U = plan direction left web point -> right web point (horizontal); up axis Wt = plumb at supports, else the AVERAGE of the two girders' flange normals; one plane through the LEFT top of web | `src/SBM.Core/Steel/CrossFrameDetailer.cs:119-133` |
| Flange normal (perpendicular to the girder's 3D tangent, orthogonal to the frame direction) | `SteelModelGenerator.cs:356-363` |
| Work points A (top left), B (bottom left), C (top right), D (bottom right): each end offset from ITS OWN girder's top of web (`pl.Z - topWp`, `prZ - topWp`) and web bottom (`- d + botWp`) -> the chords follow the girders' relative elevation, every frame is different | `CrossFrameDetailer.cs:143-161` |
| Bolt columns: `xbl = web face + plate width - edge distance` (left), mirrored right; member clear line `xcl / xcr` | `CrossFrameDetailer.cs:147-151` |
| Members, end trims (`Trim` half-planes), lap welds | `CrossFrameDetailer.cs:192-366` |
| Gussets: hull of member laps + bolt group; bolt rows centred on the WP (`BottomBoltOffset` for the bottom), columns at the gage; bolt instances; HOLE REGISTRATION into the connection plate `AddStiffenerHole(girder, s, plane.W(b, 0), hole)`; warnings when a gusset reaches a flange (1/2") | `CrossFrameDetailer.cs:389-431` |
| Plate diaphragm (web plate between the WP lines, bolt columns at `xbl/xbr`, holes registered) | `CrossFrameDetailer.cs:483-532` (top / bottom lines :489-490, holes :498-510) |
| Channel diaphragm (legacy `Diaphragm` type) | `CrossFrameDetailer.cs:575-608` |
| 2D record for drawings `CrossFrameDetail2D` (span, web tops, web faces, plate, WPs A-D, members, gussets, bolts) | `src/SBM.Core/Steel/SteelModel.cs:172-199`, filled `CrossFrameDetailer.cs:172-179` |
| Hole registry for stiffeners: `_stiffHoles[(girder, 1 cm bucket of s)]`, read +-1 bucket | `SteelModelGenerator.cs:58`, `:617-632` |
| Connection plate solid: plate frame y = plumb for bearing stiffeners, else the girder's OWN flange normal; registered holes converted to plate coordinates by PROJECTION (`x = dot(c - p, X)`, `y = dot(c - p, Y)`, the out-of-plane component is dropped) | `SteelModelGenerator.cs:650` (y axis), `:672-673` (projection) |
| Detail sheet: ONE "typical" frame (most common type, middle of the list) | `src/SBM.Core/Drawing/Views/SteelDetailViews.cs:17-160` (pick :24-27) |
| Database: cross frame assemblies, category / subtype parsed from the assembly DESCRIPTION by regex `CfName` | `src/SBM.IO/Database/ModelDatabase.cs:668-688` |
| Agent review: starting-value comparison skips `Unused` / `NotRead` inputs | `src/SBM.Api/AgentReview.cs:304-311`, `:535-570` |

Findings worth knowing before the feature:

1. **Hidden twist today.** The frame is built in one average plane through the LEFT top of web, but each connection plate is normal to
   ITS girder's flanges (`:650`). The registered hole centres are then projected onto the plate (`:672`), so any twist / offset between
   the frame plane and the plate disappears silently: the gusset sits a few hundredths of an inch off the plate face and nobody is told.
   Measured on Nebraska St Ramp B (below): up to 0.16 deg twist and the right plate 0.071" off the frame plane at the top of web.
2. Work points are per girder end, so frames on a cross slope are parallelograms that differ bay by bay; the only "same frame" today is
   the coincidence of level, equally spaced girders.
3. The intermediate frame's up axis and its connection plates are two independent decisions in two files (`CrossFrameDetailer.cs:128`
   and `SteelModelGenerator.cs:650`). Any orientation input has to drive both (the stiffener code is being edited by another agent now -
   coordinate the implementation).
4. Web depth is one value per bridge (`GirderSection.WebDepth`), but dapped girder ends (`DapDrop`, `SteelModelGenerator.cs:645`) lower
   the top flange near the bearings; the solver must read the real flange position, not `WebDepth`.

### Measurement on an existing model (read-only script, no save)

`sbm run xframe-measure.csx "samples/plans/Nebraska St Ramp B.sbm" --no-pipeline` (script kept in the session scratchpad; it reproduces the
frame plane and flange-normal math above):

| Nebraska St. Ramp B | value |
|---|---|
| Frames | 75 (2 styles, 22 explicit lines), web 76", WP offsets 6" / 6" -> today's WP depth 64" |
| Largest top-of-web difference across a bay, along the frame up axis | 4.600" (CF72, G3-G4, span 3) |
| Smallest common web window (top of web to top of bottom flange, overlap of both girders) | 71.392" (CF72) -> a standard frame there has 4.6" less room than the full-depth frame |
| Largest twist between the two connection plates | 0.163 deg (CF52, G1-G2, span 3); right plate 0.071" off the frame plane at the top of web |
| Distinct web-to-web spacings (1/16") | 2: 92" x 72 frames, 92 9/16" x 3 frames |

So even on a straight 4-girder bridge one standard depth would be ~4.6" shallower than the per-bay frames and there would be TWO standard
frame lengths (the 3 frames at 92 9/16"). With 64" WP depth entered and no clearance change, the frames at CF72 do not fit; the solver must
say exactly that.

---------------------------------------------------------------------------------------------------------------------------------------

## 2. Proposed inputs (all OPTIONAL; JSON omitted when null / default; null = today's output byte-identical)

### 2.1 `CrossFrameDefinition.Standard` : `StandardCrossFrameDefinition?` (null = off)

```csharp
/// The firm's STANDARD ("horizontal") cross frame: one frame geometry everywhere, slop taken up in the connection plate hole positions.
public sealed class StandardCrossFrameDefinition
{
    /// Which frames are standard. Default Intermediate (support frames stay as today); All; Support.
    public StandardFrameScope Scope { get; set; } = StandardFrameScope.Intermediate;
    /// Limit to these style names ("" = the definition's default members); empty = every frame in Scope.
    public List<string> Styles { get; set; } = new();

    /// Top-left work point: distance DOWN from Reference of the LEFT girder (the bay's lower-index girder), measured along the
    /// connection plate (the frame up axis). NaN = not given -> UNRESOLVED, standard not applied (frames built as today).
    public double TopWorkPoint { get; set; } = double.NaN;
    public StandardFrameReference Reference { get; set; } = StandardFrameReference.TopOfWeb;   // TopOfWeb (= underside of top flange) | TopOfSteel
    /// Top WP to bottom WP along the frame up axis. NaN = not given -> UNRESOLVED; the solver still REPORTS the maximum common depth.
    public double Depth { get; set; } = double.NaN;

    /// Required clear distances to the flanges (plans / firm standard). NaN = not given: the fit check reports the clearance it found
    /// at the governing frame and raises UNRESOLVED - no AASHTO / AISC value is substituted.
    public double TopClearance { get; set; } = double.NaN;
    public double BottomClearance { get; set; } = double.NaN;
    /// What the clearances are measured to: WorkPoint | BoltHoleEdge (outermost hole edge) | GussetEdge (default: the gusset outline,
    /// which is identical for every frame because the frame is standard).
    public StandardClearanceTo ClearanceTo { get; set; } = StandardClearanceTo.GussetEdge;

    /// Vertical placement: TopLeftFixed (default = the user's statement: top-left WP at TopWorkPoint below the left girder at every frame;
    /// only the right end's holes move) | Centered (each frame centred in its own window, both ends move; same frame, smaller shifts).
    public StandardFramePlacement Placement { get; set; } = StandardFramePlacement.TopLeftFixed;

    /// Frames are grouped into standard FAMILIES by work-point spacing rounded to this (horizontal slop cannot go into holes that only
    /// move along the plate). NaN = not given -> exact (1e-4 m) grouping and every family > 1 reported. See open question Q3.
    public double SpacingTolerance { get; set; } = double.NaN;

    /// Twist / far-gusset gap limits for flagging (NaN = report only, as INFO; the firm's practice is beat-to-fit, no limit is invented).
    public double TwistLimitDegrees { get; set; } = double.NaN;
    public double TwistGapLimit { get; set; } = double.NaN;

    public string Source { get; set; } = "";              // plan / firm standard reference for the report
    public List<string> Unresolved { get; set; } = new(); // raised as UNRESOLVED warnings (page + reason, UnresolvedFlag.Text)
}
public enum StandardFrameScope { Intermediate, Support, All }
public enum StandardFrameReference { TopOfWeb, TopOfSteel }
public enum StandardClearanceTo { GussetEdge, BoltHoleEdge, WorkPoint }
public enum StandardFramePlacement { TopLeftFixed, Centered }
```

Style-level override (OPTIONAL, for bridges with two standard frames, e.g. one K and one X, or a support style that is standard too):
`CrossFrameStyle.Standard` (`StandardCrossFrameDefinition?`, null = the definition's when the style is in its scope). Last match wins:
style's standard, then the definition's.

### 2.2 Frame / connection plate orientation (generic, not only for the standard)

`CrossFrameDefinition.IntermediatePlane` and `.SupportPlane` : `CrossFramePlaneOrientation?` (null = today: intermediate normal to the flanges,
support plumb). Values:

- `NormalToFlanges` - today's intermediate rule (top flange grade; equal to the bottom flange while flanges are parallel).
- `NormalToBottomFlange` - the user's wording; identical to `NormalToFlanges` until variable-depth girders exist (then it reads the bottom
  flange slope). Kept as its own value so the .sbm says what the plans say.
- `Plumb`.

Both the frame up axis (`CrossFrameDetailer.cs:128`) and the connection plate y axis (`SteelModelGenerator.cs:650`) read it: FramingGenerator
puts the resolved orientation on the `StiffenerResult` it derives from the frame (new optional `StiffenerResult.Up` / `Plumb`, set at
`FramingGenerator.cs:1407`), and BuildStiffener uses it instead of the kind-based rule. A through-web pair shared by two bays must agree (both
bays' frames come from the same rule, so they do; a mismatch is a warning, the pair is then split into two single plates - never forced).
`SupportPlane = NormalToFlanges` would tilt the frame at a PLUMB bearing stiffener: phase 1 keeps the bearing stiffener plumb and reports the
resulting frame-to-plate tilt through the twist report (Q5).

### 2.3 Optional report-only switch

`CrossFrameDefinition.ReportTwist` (bool, omitted when false): twist report for ALL frames even without a standard (shows the hidden twist of
finding 1 on existing models without changing geometry). Off by default so existing diagnostics stay identical.

### 2.4 Agent review

- `Standard == null`: nothing new is walked (null vs null).
- `Standard != null`: `NotRead` gets a `CrossFrameDefinition` case: `TopWorkPointOffset` / `BottomWorkPointOffset` are not read when the scope covers
  every frame (All, no style filter); `CrossFrameStyle.TopWorkPointOffset / BottomWorkPointOffset / BottomBoltOffset` not read for styles inside the
  scope. Note: `ModelingParameter` skips every name containing "Tolerance", but the spacing match IS a fabrication input the review must see -
  so the property is named `SpacingMatch` in the implementation (above it is called `SpacingTolerance` for readability) (Q3).

---------------------------------------------------------------------------------------------------------------------------------------

## 3. Geometry of the standard frame

Per frame (unchanged): plan direction U, frame up axis Wt (from the plane orientation), plane through the left top of web (today).
New for standard frames:

1. **Level chords.** Top WPs of both ends at the SAME frame z; bottom WPs at the same z (rectangle in the frame plane: "horizontal" frame).
   `z_T = zL - TopWorkPoint` (TopLeftFixed; `zL` = left Reference at the left plate) and `z_B = z_T - Depth`. Work points A, C at z_T;
   B, D at z_B; x unchanged (bolt columns `xbl / xbr`).
2. **Same members / gussets everywhere** (within a spacing family): with level chords and a fixed depth, the member work lines, trims, laps,
   gusset hulls and gusset holes depend only on the WP spacing (`xbr - xbl`), the style and the plate width. The detailer runs unchanged on
   A-D; the result is identical for every frame of a family (test: part outlines in frame coordinates equal to 1e-6 m).
3. **Plate diaphragms:** top / bottom lines level (`Zt`, `Zb` constants), bolt columns centred at the same z both ends -> one web plate.
   Channel diaphragm: the channel is level, centred on (z_T + z_B) / 2.
4. **Bolt rows** stay centred on the WP in the frame (`TopBoltRows`, `BottomBoltOffset` keep their meaning relative to the standard WPs).

## 4. Depth solver (report only - never chooses)

Evaluated for every frame F in scope and each end k = L, R, in that end's CONNECTION PLATE coordinates (y_k = the plate's own up axis; the
holes move along y_k):

- Plate origin = the girder's web CL at its top of web at the frame's girder distance (dapped ends: lowered by `DapDrop`).
- `yTop_k(x)` = underside of the top flange where the plate line at bolt-column distance x crosses it; `yBot_k(x)` = top of the bottom flange.
  Computed as plane intersections with the flange face planes at that point (flange level across the girder, sloped along it; a plumb
  plate on a skewed frame sees the grade times the skew component - exact, not `WebDepth`). Evaluated at the bolt column x AND at the
  gusset's inner / outer x (the highest top / lowest bottom over the gusset width governs).
- The standard frame's items at this end, mapped into plate coordinates: top WP `yT_k`, bottom WP `yB_k = yT_k - Depth * (Wt . y_k)^-1`
  (exact for the twist; ~Depth), plus the frame-constant extents above / below the WPs of what the clearances are measured to
  (`ClearanceTo`): gusset top above the top WP `gT_k`, gusset bottom below the bottom WP `gB_k` (from the standard frame's gusset hulls; for
  the left and right ends separately, they mirror), or the outer hole edges, or zero.
- Fit at end k:  `yT_k + gT_k <= yTop_k - TopClearance`  and  `yB_k - gB_k >= yBot_k + BottomClearance`.

Outputs (`StandardFrameReport`, see 6):

- **Maximum common depth for the entered top WP:** `Dmax = min over F, k of (yT_k - gB_k - yBot_k - BottomClearance)` (+ the gusset top check,
  which does not depend on Depth). Reported with the governing frame mark, end, girder, span, station and the window numbers that make it
  (top of web difference across the bay, web depth at the end, flange position, clearances). If the clearances are NaN, the report gives the
  depth to the flange face (clearance 0) and says "clearances not given - UNRESOLVED".
- **Top WP feasibility:** frames where the entered top WP is too close to the top flange at the RIGHT end (right girder lower than the left:
  with TopLeftFixed the frame top sits higher on the right girder) - listed with the shortfall; the minimum top WP that clears every frame.
- **Best pair (information):** the smallest top WP that clears every frame and the Dmax at that top WP. Printed as "deepest common frame: top
  WP x, depth D at <frame>"; NOT applied.
- **Per frame:** headroom = own maximum depth - entered Depth (negative = does not fit -> one warning line per frame, aggregated per bridge
  into "Standard cross frame: depth 5'-4 does not fit at 3 of 75 frames: CF72 (-0.61"), ..."). A frame that does not fit is still BUILT at
  the entered depth (the gusset then overlaps the flange - the clash check shows it); nothing is moved or shortened.
- Placement `Centered`: each frame's top WP = the middle of its own feasible band; same Dmax (the band is the same), smaller shifts.

The solver is a pure function in `SBM.Core/Steel/StandardCrossFrames.cs` (inputs: framing, definition, a per-end geometry callback) so tests can
run it without building solids. It runs in the CONNECTIONS phase before `BuildCrossFrame` (needs the gusset extents of the standard frame:
build ONE prototype frame per family in frame coordinates first - the detailer math already works in 2D, `CrossFrameDetail2D` - then evaluate).

## 5. Hole shifts

- **Standard hole pattern** at each end = the pattern the standard frame would have on a girder whose Reference is exactly `TopWorkPoint` above
  the top WP (left end by definition; right end on a level bay). Recorded once per family as y offsets below the Reference.
- **Shift** at end k = actual hole-group position along y_k minus the standard position (+ = up). TopLeftFixed: left shift = 0 apart from twist
  projection, right shift ~ -(top of web difference across the bay). Centered: both ends move by half.
- **Registration:** for each bolt, the hole centre on the plate = intersection of the bolt axis (frame normal N through the bolt) with the plate
  mid-plane, not the frame point (`plane.W(b, 0)`) - so the plate hole lies where the bolt really passes and the projection at
  `SteelModelGenerator.cs:672` no longer hides an offset. Registered through the same `AddStiffenerHole(girder, s, centre, dia)` (`_stiffHoles`;
  1 cm buckets unchanged). Gusset holes are unchanged (frame-constant). Plate diaphragm / channel diaphragm holes the same way.
- **Checks on the moved pattern** (generic, also useful today): hole edge distance to the plate's top corner clip and to the flange welds; hole
  group within the plate height; when a through-web PAIR carries frames from both bays, the two groups are on opposite plate faces and may sit at
  different heights - fine, but report if bolt heads / nuts of one group interfere with the other group's gusset (they share the plate).
- **Report** per frame end: shift (signed, fabrication rounding 1/16" shown next to the exact value), girder, station, plate mark (`CS`
  stiffener mark from `SectionMark`). Table sorted by girder line so the shop can drill the plates.

## 6. Twist measurement (never hidden, never forced)

Per frame (standard frames always; all frames with `ReportTwist`):

- **Twist angle** = signed angle from the left plate's up axis y_L to the right plate's y_R about U (both are orthogonal to U because the
  plates lie in the frame's plan direction): `atan2(dot(y_L x y_R, U), dot(y_L, y_R))`. Sources: adjacent girders at different grades (vertical
  curves, skewed frames on a crest / sag, curved girders with different arc lengths), plumb vs normal mixes.
- **Plate offset** of each end from the frame plane at its top of web (the right plate does not pass through the left-anchored plane when Wt is
  tilted: `dot(pR - pL, N)`).
- **Far-gusset gap** per bolt: distance along the bolt axis between the gusset's back face (frame plane +- tp/2) and the plate face.
  Reported as min / max over the bolts of each end (positive = gap to be drawn up, negative = interference in the model). The frame stays in the
  average plane (today's choice, splits the twist between the two ends; Q6 offers "fit to the left plate").
- Flags: above `TwistLimitDegrees` / `TwistGapLimit` -> warning "CHECK (cross frame twist): CF52 G1-G2 span 3 twist 0.163 deg, far gusset gap
  0.09" - beat to fit" ; limits NaN -> one INFO summary per bridge with the worst frame. Nothing is straightened.

## 7. Outputs

- **Diagnostics** (per bridge, generator Diagnostics, stable order): standard summary (families, entered depth, Dmax + governing frame, best
  pair), per-frame misfits (aggregated), UNRESOLVED for NaN inputs, twist summary / checks, spacing-family count > 1.
- **`SteelModel.StandardFrames`** (new, null when off): `StandardFrameReport` { families (spacing, count, style, prototype detail),
  Dmax, governing, rows per frame end: mark, girder, s, station, plate mark, headroom, shift, twist, gap min / max }. `SbmDocument` accessor;
  CLI `sbm crossframes <model.sbm> [--bridge] [--csv out.csv]` (prints the solver report and the hole-shift table; read-only).
- **Database:** cross frame assembly props `std_family`, `std_depth`, `headroom`, `shift_left`, `shift_right`, `twist_deg`, `twist_gap_max`
  (added in `ModelDatabase.cs:668-688`; do NOT change the assembly Description text - `CfName` parses it). Connection stiffener parts get
  `hole_shift`.
- **IFC:** optional Pset `SBM_StandardCrossFrame` on the cross frame assembly (family, depth, shifts, twist) - only when the standard is on.
- **Drawings** (`SteelDetailViews.CrossFrame`): when standard frames exist, the intermediate (or support) cross frame sheet draws the ONE
  standard frame per family (from the prototype `CrossFrameDetail2D`, level chords, girders drawn at the governing bay's relative elevation
  dashed + a level girder pair solid) with the depth and the top WP dimensioned from the Reference, plus a paged HOLE SHIFT TABLE
  (`TableViews.Paged`: frame mark, girder, station, left shift, right shift, twist, gap) and a note "Holes in the connection plates are
  located per the table; no slotted holes." The numbers come from the report (rule: notes that quote numbers come from the model).
- **Viewport:** no new layer. A misfit is a CHECK, not an unresolved plan item, so no `Assumption` flag; pick properties show headroom /
  shift / twist (Q7 asks whether a status layer is wanted).

## 8. Implementation steps (later run)

1. Inputs + JSON (omitted when null), Clone, `PropertyPath.KindOf` entries for the new lengths / angles, docs/API.md.
2. Orientation input wired through `StiffenerResult` and both up-axis decisions (coordinate with the stiffener work).
3. `StandardCrossFrames.cs`: families, prototype detail per family, solver, shifts, twist (pure).
4. `CrossFrameDetailer`: standard WPs (level), bolt-axis hole registration (standard frames only, so other outputs stay byte-identical),
   report rows.
5. Report / CLI / DB / IFC Pset / detail sheet + table.
6. AgentReview `NotRead` rules.
7. Run on Nebraska St Ramp B (TopWorkPoint 6", Depth 64", clearances NaN) and I-80 U2 (curved, vertical curve, staggered skew) - expected:
   CF72 reported as governing / misfit at 64", and measurable twist on I-80 (vertical curve, curved girders).

## 9. Tests to add (`StandardCrossFrameTests`)

- Off = identical: a sample bridge's parts, bolts, holes and IFC unchanged with `Standard = null` (and with `ReportTwist = false`).
- Level bridge, equal spacing: all frames one family; prototype outlines identical; shifts 0; twist 0.
- Constant cross slope: Dmax = web window - |dTOW| - clearances - gusset extents, governing = the bay with the largest dTOW; right shift = -dTOW
  (TopLeftFixed) / +-dTOW/2 (Centered).
- Entered depth 1" deeper than Dmax: exactly the governing frame(s) flagged with headroom -1"; frame still built.
- NaN TopWorkPoint / Depth / clearances: UNRESOLVED warnings, standard not applied (TopWorkPoint / Depth) or report with clearance 0.
- Vertical curve, frames normal to flanges: twist angle = difference of the two flange-normal angles (analytic), far-gusset gap = (bolt y -
  pivot) x sin(twist / 2) within 1e-6.
- Plumb plates on a skewed, graded girder: `yTop(x)` follows grade x skew component.
- Two spacings: two families reported; `SpacingTolerance` merges them only when within it.
- Hole registration: plate hole centre lies on the bolt axis (distance < 1e-9) and the plate hole list matches the frame's bolts.
- Plate diaphragm standard: one web plate outline for all frames.
- Reproducible order (parallel frames): report rows sorted by mark.

## 10. Open questions for the user

- **Q1 Placement.** "Top-left work point relative to the left girder" - fixed at EVERY frame (only the right-end holes move, shifts up to the
  full top-of-web difference, 4.6" on Nebraska), or should the program also offer "centred" (both ends move by half, same frame)? Is "left"
  always the lower-numbered girder, or the girder on the high side?
- **Q2 Reference.** Top WP measured from the top of web (underside of top flange) or from the top of the top flange (moves with flange
  thickness changes)? Along the connection plate, or vertical?
- **Q3 Different bay widths.** Holes only move along the plate, so frames with different WP spacing cannot be the same frame. One standard per
  spacing (families), or does the firm accept a horizontal tolerance (gusset holes oversize? no slots) - and what value? Nebraska has 92" x 72
  and 92 9/16" x 3.
- **Q4 Clearances.** What do the firm's standards measure the top / bottom clearance to (gusset edge, outer hole edge, WP) and what are the
  values? (No default will be invented.)
- **Q5 Support frames.** Are support frames ever standard? If support frames are normal to the flanges, does the bearing stiffener tilt too
  (contact with the sole plate) or stay plumb with a separate connection plate?
- **Q6 Twist.** Build the frame in the average plane (twist split between both ends, today) or fitted to the left plate (all twist at the far
  end, matches beat-to-fit on erection)? Any limit (angle / gap) the firm uses to call it out, or report only?
- **Q7** Should a frame that does not fit the entered depth be shown on a status layer in the viewport (like UNRESOLVED), or only listed?
- **Q8** Same standard for every style, or one standard per frame type (e.g. K at piers, X in spans)?

## User answers (2026-10-27)

1. Defined end: usually the TALLER girder (read: the end whose top of web is higher) carries the defined top work point; the other end's holes move. Make the defined end selectable (Taller default, Left, Right).
2. A standard frame is for SAME BAY conditions: one standard per spacing family (no horizontal tolerance across different bays).
3. Clearances are measured to the WORK POINT (values = engineering input; blank = flagged, never a default).
4. Support frames standard or not: "depends" -> per-support / per-style input; bearing stiffener plumb or tilted = input too.
5. Twist: EVALUATE averaged plane vs fitted to the defined end (gap per bolt at the far gusset, both options, on Nebraska + a curved / warped model) and report before choosing; keep both selectable if they differ meaningfully.
6. One standard PER FRAME TYPE (e.g. K at piers, X in spans).

---------------------------------------------------------------------------------------------------------------------------------------

## Plates on the frame plane (implemented 2026-10-27, user decision "Q1, make them flat"; frame plane default Midway)

Finding (twist evaluation): each connection plate used to be located where the frame line crossed its girder at THAT girder's top of web; a frame normal to
the flanges leans with the grade, so the far plate sat up / down station of the frame plane (top of web difference x grade), hidden by projecting the holes.

Rule now (default for every girder model; legacy = `CrossFrameDefinition.PlatesAtEachTopOfWeb`, byte-identical old output - Sample and Nebraska St Ramp B IFC
checked against a build of the code before the change, identical apart from FILE_NAME):

- A frame's station / location is defined at the TOP OF WEB of its REFERENCE girder: the `Lines` girder; else the standard's DefinedEnd (Left / Right; Taller
  = the girder with the highest top of web in the chain); else the left girder of the bay (lowest girder number of the chain).
- Frame plane = that point + the plan line's horizontal direction (`CrossFrameResult.PlaneDirection`) + the frame orientation's up axis (mean of the two flange
  normals for intermediate frames). Every intermediate connection plate is placed where that plane meets its girder's top of web. Frames of one line across
  several bays (sharing a through-web plate) are solved bay by bay outward from the reference girder, so a shared plate stays one plate.
- Frame ends AT a bearing (plan lines through skewed piers, the 6" snap) stay at the bearing (Info lists them; I-80 Unit 1: CF7, CF11, CF58, CF120, CF177).
  Support frames and links stay on the bearing stiffeners (plumb support frames are flat already).
- Frame plane modes (`CrossFrameDefinition.FramePlane`, a standard's own nullable `Plane` wins): Midway (DEFAULT, user 2026-10-27 "so it renders nice":
  members exact extrusions; with both plates on the plane it is the averaged plane; the remaining TRUE twist between the two plates shows as a far-gusset gap),
  Averaged, DefinedEnd, Warped (each end's gussets on its own plate = gap 0 at every bolt; members straight between the work points with the section rolled
  linearly between the two plate orientations; roll > 0.001 deg = faceted solid in IFC; plate / channel diaphragms and links are not warped).
- Holes: every frame's holes are where the bolt axis meets the real plate's mid-plane (the plate the framing built, incl. a pair's plan direction).

Per model (intermediate frames; inches; base = the code before the change; Midway / Warped = the new code; `sbm crossframes --twist [--frame-plane warped]`):

| bridge | frames | plate move max / median | far-gusset gap before (max / median) | Midway residual gap (max / median) | true twist max (deg) | Warped gap max | Warped roll max / median (deg) | leg-tip deviation max / median |
|---|---|---|---|---|---|---|---|---|
| Sample Bridge | 40 | 0.143 / 0.034 | 0.120 / 0.022 | 0.065 / 0.003 | 0.145 | 0.000 | 0.145 / 0.006 | 0.012 / 0.001 |
| Nebraska St Ramp B | 66 | 0.194 / 0.068 | 0.102 / 0.036 | 0.102 / 0.001 | 0.163 | 0.000 | 0.163 / 0.002 | 0.017 / 0.000 |
| I-80 WB Unit 1 | 257 | 0.130 / 0.035 | 0.143 / 0.035 | 0.055 / 0.000 | 0.064 | 0.000 | 0.064 / 0.000 | 0.008 / 0.000 |
| I-80 WB Unit 2 | 415 | 0.363 / 0.051 | 0.099 / 0.045 | 0.081 / 0.016 | 0.094 | 0.000 | 0.094 / 0.019 | 0.012 / 0.002 |
| I-80 WB Unit 3 | 289 | 0.369 / 0.180 | 0.127 / 0.045 | 0.104 / 0.001 | 0.164 | 0.001 | 0.164 / 0.002 | 0.021 / 0.000 |
| Ramp G Unit 1 | 111 | 0.829 / 0.273 | 0.386 / 0.273 | 0.136 / 0.016 | 0.197 | 0.000 | 0.197 / 0.024 | 0.024 / 0.003 |
| Ramp G Unit 2 | 105 | 0.829 / 0.204 | 0.278 / 0.128 | 0.023 / 0.008 | 0.034 | 0.000 | 0.034 / 0.012 | 0.004 / 0.001 |
| Ramp G Unit 3 | 147 | 0.488 / 0.137 | 0.164 / 0.085 | 0.015 / 0.006 | 0.019 | 0.000 | 0.019 / 0.007 | 0.002 / 0.001 |
| Ramp G Unit 4 | 132 | 0.488 / 0.149 | 0.291 / 0.145 | 0.179 / 0.011 | 0.195 | 0.000 | 0.195 / 0.014 | 0.024 / 0.002 |

Woodbury (prestressed) and ODOT Bridge O (tub) have no I-girder frames: untouched (0 frames). Nebraska check (`check-nebraska-st.csx`): unchanged (the p.27 lines
are measured along girder A = the reference girder; only the steel weight moves in the 3rd decimal). Warped Nebraska IFC: validate_ifc 0 schema / 0 EXPRESS issues,
every cross frame product meshes closed. Tests `CrossFramePlateTests`.
