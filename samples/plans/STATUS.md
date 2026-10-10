# I-80 WB + Ramp G test model: status (2026-10-09)

## Done
- **IFC export:** 2,108 MB down to 231 MB plain or 50 MB `.ifczip`.
  - Exact extrusions with round bolt holes.
  - Bolts typed by size.
  - Shared property sets.
  - Bar bends as arcs; spirals as fine polylines on a true helix.
  - Validated with IfcOpenShell: 0 schema issues.
- **Bug fix:** mesh volume at UTM coordinates. Steel tonnage is now 8,738 tons; the app previously showed 32,015.
- **Deck edges:** Unit 2/3 north edges follow the plan tapers and compound curves (`deck-edges-i80.csx`).
- **Optional as-designed substructure** (the standard parametric bent is still the default):
  - Cap profile polygon, with a stepped ahead half.
  - Octagonal columns with a flared base.
  - Per-footing outline and explicit pile positions.
  - Side-by-side shared piers (11WB and 19G).
- **Piers rebuilt from the sheets:**
  - I-80 1WB–15WB (`piers-i80.csx`).
  - Ramp G 1G–15G and 19G (`piers-ramp-g.csx`).
  - Pile counts match the plan notes.
- **Discontinuous girders:** A–E (Unit 2) and A–C (Unit 3) placed from the pier cap plans (`girders-i80.csx`).
- **Cross frames near field splices:** now flagged, never moved (opt-in `MoveClearOfSplices`).

## In progress (the last step was interrupted)
- **`girders-i80.csx`:** the patch that iterates girder offsets so each *bearing* lands on its plan position was not applied.
  - Inputs are offsets at the support's alignment station; on skewed piers the flaring girders land about 1.2 ft off.
  - Apply that loop, then re-run `girders-i80.csx`, `piers-i80.csx` and `piers-ramp-g.csx` on a fresh model.
  - Backup: `C:\SBMData\CouncilBluffs_I80\I-80 WB and Ramp G - before I-80 piers.sbm`.

## Known issues (my inputs vs. the plans)
1. **Pier 6A is missing.** Unit 2 girders A–E start on Pier 6A (hammerhead, design sheets 37–40), not on 5WB.
   - Needs a feature: girders that begin on their own support line.
2. **Gore cross slope at 6WB/7WB.** North girders sit about 1.2 ft high (20–24" pedestals).
   - Plan pedestal tops imply about 2.8% there; the model uses 2%.
   - Source: sheets 197–198 (top of slab cross slope & gore geometrics).
3. **Girder terminations wrong.** Unit 3 girder B (at 12WB) and C (at 13WB) appear on the cap plans but end earlier in the model.
4. **Seat elevations about 0.2–0.35 ft above plan pedestal tops.** Trace the bearing stack and haunch with the elevation check.
5. **Unit 3 north edge.** Possible misread of the P.C.C. at 7607+18.69 (1.1° tangent break); check sheets 185/191.
6. **Not yet from the plans:**
   - Ramp G 16G–18G (straddle bents).
   - Both abutments and wingwalls (the east abutment is generic).
   - Cross frames: plan types, chevron with 2-MC bottom chord, 1" bolts and plan locations.
   - Deck reinforcing (plan layout); bar M1 near FS4-G1 is reported wrong.
   - Drain routing per sheets 205–220.
   - Modular expansion joints (sheets 203–204); needs a decision.
   - Battered piles are modeled plumb.
   - Spiral meshing in IfcOpenShell is fine; round-column ties at 4WB–7WB mesh too big in IfcOpenShell (unexplained).

> Superseded by `docs/HANDOFF.md` (full list of remaining items).
