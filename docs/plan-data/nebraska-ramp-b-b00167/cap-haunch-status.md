# Pier 2 integral steel cap - concrete haunch (status 2026-10-12, branch neb-haunch)

Plans: p.25 Section A-A + Section near Pier No. 2 (haunch, 5d2 / 5e5 / 5e6), p.29 plan (edge of slab jogs round the cap), p.43 Detail D (8" x 2'-7 + 2'-7
deck bump-out), p.31 Section A-A (cap studs 7/8 x 9), p.45 bar list + 5d2 bend diagram (6 + 8 + 3'-0 + 8 + 6, D = 2 1/2). No separate plan CY: the haunch is
inside deck placement section 5 (83.1 CY, p.45).

Modeled (generic, `IntegralSteelCapDefinition.Haunch` -> `ConcreteGenerator` ConcreteCapHaunch.cs, cap frame shared with the steel via `Steel/IntegralCapFrame.cs`):
40 3/8 wide (flange out to out, plumb sides), cap steel (flange or top cover plate) to deck soffit, deck edge to deck edge incl. the bump-out widening, stops
at the girder haunches; 10 pieces, 1.47 CY, depth 3.93..6.35 in. Bars 5d2 x38 (211 lb), 5e5 x9 (50 lb), 5e6 x6 (15 lb) = plan counts / weights.
IFC IfcBuildingElementPart 'PIER_CAP_HAUNCH' aggregated into the deck slab (validator 0 issues); database element "pier cap haunch"; viewport 3D-DECK +
3D-REBAR-CAP-HAUNCH; check row in check-nebraska-st.csx section 8.

Open / next steps:
- FINDING (pre-existing, all girder haunches): `ConcreteGenerator` reads the top flange width from `Girders.Section.SegmentAt` (the base section, 18")
  instead of the per-girder plate layout (p.28: 24" at pier 2). Girder haunches are therefore 18" wide near the piers and the cap haunch bays end
  3" too far into the girder lines (5e5 end clearance 5" vs the plan's 2"; 5e6 3 1/2-3 11/16"). Fix: use the layout segment per girder in BuildHaunches,
  NurbsHaunch and ConcreteCapHaunch, then rebuild.
- Cover check: one 5d2 (bar 1, at the column 1 overhang end) reported 2.4" outside the concrete (hook beyond the haunch/deck edge?) and one 5e6 0.35"
  short of top cover - review in 3D.
- UNRESOLVED (p.25): 5e5 / 5e6 corner bar position inside the 5d2 (not dimensioned; nested in the 2 1/2" pin bends).
- Not done: before/after snapshots (dist\showcase\neb-haunch), section 5 placement-quantity comparison (clip deck volumes between the p.43 joints),
  I-80 / Ramp G review (those sets have no integral steel caps known to this agent - not checked).
