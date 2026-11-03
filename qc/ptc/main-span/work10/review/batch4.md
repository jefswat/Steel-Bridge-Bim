# Agent review batch 4: PTC main span, primed half L21' - L14' (2026-11-02)

Joints: L21' U21' L20' U20' L19' U19' L18' U18' L17' U17' L16' U16' L15' U15' L14', both truss planes N and S. Read-only review: model images
`png/{S,N}_<joint>p.png` compared with the unprimed images, the raw dumps in `out/` and the joint sheets p.82 / 84 / 86-89 (+ p.98 lateral plates).
Findings: `batch4.csv` (63 rows). The scratch scripts are in `review/b4tmp/` (mirror.py = primed vs unprimed / N vs S comparison, rivout.py = rivets with no plate
at the gusset plane, outline.py / bg.py = gusset / bracing plate hulls).

## Verdict counts

| verdict | major | minor | info | total |
|---|---|---|---|---|
| CONFIRMED | 27 | 7 | 0 | 34 |
| OK | 0 | 1 | 25 | 26 |
| UNSURE | 0 | 1 | 2 | 3 |

## Mirror / hand and N vs S (all 15 joints: OK)
- Every gusset, edge angle, splice, fill and bracing plate of each primed joint has the same extents as the unprimed joint mirrored in u, within 0.5 in.
  Rivet counts are equal within 2, and >= 99 % of the unprimed rivets have a mirrored twin within 0.6 in. The diagonal lands on the mirrored side and the edge angles
  are on the mirrored edges, which is what the sheets ask for ("opposite hand").
- N and S are identical: the same part set, and the same extents with v mirrored.
- So every finding below applies to the unprimed joint too (batch 3). The only primed-specific sheet content is the erection closure at U21' / L21' (see below).

## Systematic patterns (counts over 15 joints x 2 planes)
1. **The chord field splice material is missing at 15 of 15 joints.** Each joint sheet calls out I.S. web splice plates, inlay plates, fills, and top / bottom
   cover splice plates with fills (sizes in `docs/plan-data/ptc-delaware-river/connections/splices.csv`, incl. the CORRECTION rows). In the model, U21' and L21' get
   only one O.S. pair, cut to the gusset. L15' gets only the 18 x 1 web extras. The other 12 joints get none. The cause: the splices.csv rows for joints 14-21 have
   no length, so connections-ptc.csx skips them. Effect in the model: rivets that are drawn on these plates beyond the gusset are placed with nothing at the gusset
   plane level. Per plane: L14' 72, L15' 53, U19' 16, L16' 5, L19' 6, U20' / L20' / L21' 1 each.
2. **The lug angles on the diagonals are missing at 11 of 15 joints:** U15, U16, U17, L17, U18, L18, U19, L19, U20, L20, L14. The callouts read
   "2-Ls 8 x 4 x 1/2, n-1 in. Riv. o.s. leg". TRUSS-DESIGN.md already lists them as not built. Their o.s.-leg rivets ARE placed, so they float beside the diagonal
   (U15', U16', U17', U19', L17').
3. **Gusset outline vs the drawing: most joints are within the stated +-1-3 in.** That does not support the user's belief that the same gusset outline issue exists
   at every joint. 4 joints have edges that differ from the drawing by more than that:
   - U16': the bottom edge across the vertical is one 20 deg line. p.87 draws it square to the vertical, so the plate is about 8 in. short at the vertical and
     8 vertical rivets fall outside it. Major.
   - U21': the side edge is slanted and 1.5-6.7 in. short of the drawn plumb edge at 43.6 in.
   - U18': the top edge is 2-4 in. below the chord top on one panel.
   - U17' / U15': a few edge rivets lie on or just outside the edge.
4. **Wrong "similar to" source.** U21', L21' and L19' are labelled "taken from U20 / L20 / L18: details not shown". In fact p.89 draws U21 and L21, and p.88 draws L19.
   The L-joint outlines still agree within ~1-2 in. U21 does not (item 3).
5. **The erection closure at U21' / L21' is not modeled.** p.89 "PART ELEVATION AT U21' / L21'" shows a 31 1/2 x 15/16 splice plate shipped bolted in a temporary
   position, fills, a top / bottom plate shipped loose, and countersunk rivets. The model's U21' / L21' are plain mirrors of U21 / L21.
6. **Lateral plates (L15' and the L17-type plates at L17'-L21' / U-joints): OK or UNSURE.** L15' matches p.98 (3/4 plates, bent L7 x 4 x 3/4). The L17-type plates
   match the trace in shape, but sit 4.5-6.5 in. off along the bridge. That is probably the trace's origin convention on the sloped chord, so it is UNSURE.
7. **Not modeled, known:** the chord internal diaphragm at L14 (p.84 Section E).

## Sheet crops used (`review/`)
batch4_L21_p89.png, batch4_L21p_p89.png (part elevations U21' / L21'), batch4_U21_p89.png, batch4_U21_p89_edge.png (300 dpi, edge measurement),
batch4_L20_p89.png, batch4_U20_p89.png, batch4_L19_p88.png, batch4_U19_p88.png, batch4_U19_p88_fill.png, batch4_L18_p88.png, batch4_U18_p88.png,
batch4_L17_p87.png, batch4_U17_p87.png, batch4_U16_p87.png, batch4_U16_p87_bottom.png, batch4_L15_p86.png, batch4_L16_p86.png, batch4_U15_p82.png,
batch4_L14_p84.png.

Scale: 2'-10 1/2 b.b. Ls on p.89 = 298 px at 300 dpi (8.64 px/in.), checked against the vertical's 2'-0 1/2 b.b. Ls on p.88. Scaled values are +-1 in.
