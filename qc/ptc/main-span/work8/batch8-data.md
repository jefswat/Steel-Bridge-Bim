# PTC main span - agent review batch 8: plan data read from the sheets (2026-10-30)

Source: `C:\SBMData\Original Existing Bridge Plans from PTC.pdf` (raster, 5060 x 3188 px per page = ~298 dpi native). All pixel values below are ABSOLUTE PAGE PIXELS AT 300 DPI
(render: `python -I work8/grid.py PAGE X0 Y0 X1 Y1 out.png [zoom] [step]`; dark runs along a line: `work8/line.py`). Crops in `work8/g*.png`, overviews `work8/p*_overview.png`.

Scale used: the joint sheets p.74-90 are 3/4 in. = 1 ft-0; I use **8.56 px/in at 300 dpi** = the `gusset-outlines.csv` basis (4.28 px/in at 150 dpi), so every length below is
consistent with the model gusset outlines. Check: the gusset edges I measured land within 0.2-1.5 in. of the outline rows (U6 44.4 vs 44.2, U8 51.6 vs 51.4, U10 49.8 vs 49.6,
U22 39.5 vs 39.5). A direct read of the p.76 "2'-6 min." / "2'-7 in. to in." dimensions gives 8.33-8.36 px/in (2-3 % less): the scaled lengths are +-3 % / +-1 in.
p.103 / p.104 / p.106 are 1/2 in. = 1 ft-0: measured 5.78 px/in (p.104 View B 8'-0 = 555 px; p.106 2'-0 = 139 px).

"Distance from WP" = along the member axis from the joint work point (WP from `gusset-outlines.csv` wp_px x 2, except U21 / L21 which I read: see 1.3).

---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## 1. 8b - web member fills between the members and the gussets (gus GN01, conn N05)

### 1.1 What the sheets show

Each fill is called out as "2-Fills b x t (C)" with a leader to the fill end. The fill lies between the web member's outer faces (channel backs / box web plates, in the
truss plane) and the gusset, from the member end over the whole gusset lap and a few inches PAST the gusset edge, where it carries one to four rows of FIELD rivets
(open circles) through fill + member only. Width b = the member's depth in the truss plane (18 in. channels; 21 / 26 in. box web plates). Every thickness = the
gap the model measures (GN01). Carbon steel (C) in every callout.

| joint | member | callout (page) | member end from WP | gusset edge from WP (outline) | fill end from WP | fill past the gusset edge | field rivets past the edge per face | crop |
|---|---|---|---|---|---|---|---|---|
| U6 | U6-L6 | 2-Fills 18 x 1 (C) (p.76) | 22.2 | 44.4 (44.2) | 51.2 | 6.8 | 5 (rows 3 + 2) | g76_U6fill.png |
| L7 | U7-L7 | 2-Fills 18 x 1 (C) (p.76) | 22.4 | 47.0 (46.3) | 52.8 | 5.8 | 5 (3 + 2) | g76_L7fill.png |
| U8 | U8-L8 | 2-Fills 18 x 3/4 (C) (p.77) | 22.8 | 51.6 (51.4) | 55.7 | 4.1 | 3 (one row) | g77_U8fill.png / g77_U8top.png |
| L9 | U9-L9 | 2-Fills 21 x 3/4 (C) (p.77) | 22.3 | 58.5 (57.0) | 63.2 | 4.7 | 4 (one row) | g77_L9fill.png |
| U10 | U10-L10 | 2-Fills 26 x 1 1/8 (C) (p.78) | 25.2 | 49.8 (49.6) | 63.8 | 14.0 | 14 (rows 3 / 4 / 3 / 4) | g78_U10fill.png, g78_U10call.png |
| U21 | U21-L21 | 2-18 x 1 1/8 Fills (C) (p.89, upper callout) | 18.1 | 54.6 | 62.5 | 7.9 | 5 (2 + 3) | g89_U21v_top.png |
| L21 | U21-L21 | 2-18 x 1 1/8 Fills (C) (p.89, lower callout) | 20.4 | 55.3 | 64.8 | 9.6 | 5 (3 + 2) | g89_U21v_bot.png |
| U21 | U21-L22 | 2-18 x 1 1/8 Fills (C) (p.89) | - | - | - | 4.8 (normal to the edge) | 4 (one row) | g89_U21diag2.png |
| L21 | U20-L21 | 2-21 x 1 1/8 Fills (p.89) | - | - | - | 18.6 (normal to the edge) | about 12-16 (low) | g89_L21diag.png |

All lengths in inches, SCALED (3/4 in. = 1 ft-0, 8.56 px/in at 300 dpi), +-1 in. The fills of a member END only: at the opposite ends (L6 / U7 / L8 / U9 / L10 / ...)
the gusset in-to-in equals the member width and no fill is drawn (checked on p.76 for U6-L6 at L6 and U7-L7 at U7).

Side findings (correct the data):
- p.78 U10: the callout reads **"2-Fills 26 x 1 1/8 (C)"** (g78_U10call.png). `gussets.csv` row main,U10 vertical U10-L10 (top group) note says "2-Fills 26x1/16 (C)" - misread.
- p.89 L21: the outside web splice plates are legible at 300 dpi: **"2-O.S. Spl. Pls 34 x 1 1/8"**, "2-Inlay Pls 34 x 1", "2-I.S. Spl. Pls 31 1/2 x 1 1/8", "2-Fills 31 1/2 x 1/2"
  (p89_U21.png). `splices.csv` row 177 ("O.S. 34x1", low, "digits read at overview zoom") should read 34 x 1 1/8; the CONFLICT row in connections-ptc.csx
  batch 3b ("L21 ... splices.csv 'O.S. 34 x 1' vs 1 1/8") can then be dropped - the plan agrees with the 2'-7 1/4 in. to in.

### 1.2 Proposed data (target: `samples/plans/connections-ptc.csx`, section 1, after the BATCH 3b block)

Mechanism = the approach section 6a one (`TrussJointPlate.Member` + `Fill = true`, `Face = WebOutside` = one fill on each web face of the member, Offset = centre distance
from the WP into the member). Verticals: explicit a0 / a1 from the table. Diagonals at U21 / L21: a1 = reach of the model gusset outline along the member axis + the
drawn "past the edge" distance (same as section 6a `Reach(...) + ext`). Both halves (primes) and both planes (Plane "" = every plane), like the batch 3b block.

```csharp
    // BATCH 8b (GN01 / N05, 2026-10-30): fills between the web members and the gussets, read from the joint sheets (work8/batch8-data.md section 1).
    // Callouts give width x thickness; the extents are SCALED (3/4 in. = 1 ft-0, 8.56 px/in at 300 dpi = the gusset-outlines basis, +-1 in.):
    // a0 = member end, a1 = fill end, both from the WP along the member; beyond = drawn length past the gusset edge (diagonals: added to the model outline reach).
    {
        int nF = 0;
        var fills = new (string j, string mem, int page, double w, double[] t, double a0, double a1, double beyond, string label)[]
        {
            ("U6",  "U6-L6",   76, 18, new[] { 1.0 },   22.2, 51.2, 6.8,  "2-Fills 18 x 1 (C)"),
            ("L7",  "U7-L7",   76, 18, new[] { 1.0 },   22.4, 52.8, 5.8,  "2-Fills 18 x 1 (C)"),
            ("U8",  "U8-L8",   77, 18, new[] { 0.75 },  22.8, 55.7, 4.1,  "2-Fills 18 x 3/4 (C)"),
            ("L9",  "U9-L9",   77, 21, new[] { 0.75 },  22.3, 63.2, 4.7,  "2-Fills 21 x 3/4 (C)"),
            ("U10", "U10-L10", 78, 26, new[] { 1.125 }, 25.2, 63.8, 14.0, "2-Fills 26 x 1 1/8 (C)"),
            ("U21", "U21-L21", 89, 18, new[] { 1.125 }, 18.1, 62.5, 7.9,  "2-18 x 1 1/8 Fills (C)"),
            ("L21", "U21-L21", 89, 18, new[] { 1.125 }, 20.4, 64.8, 9.6,  "2-18 x 1 1/8 Fills (C)"),
            ("U21", "U21-L22", 89, 18, new[] { 1.125 }, double.NaN, double.NaN, 4.8,  "2-18 x 1 1/8 Fills (C)"),   // a1 = outline reach + beyond
            ("L21", "U20-L21", 89, 21, new[] { 1.125 }, double.NaN, double.NaN, 18.6, "2-21 x 1 1/8 Fills"),
            // U22 (section 2.1): stepped fills, listed member side first (the generator stacks the first listed fill on the member)
            ("U22", "U22-L22", 90, 18, new[] { 0.875, 1.125, 1.125 }, 21.1, double.NaN, 0, "4-Fills 18 x 1 1/8 (C), 2-Fills 18 x 7/8 (C)"),
        };
        double[] u22Ends = { 50.1, 46.7, 43.1 };   // 7/8 (on the member), 1 1/8, 1 1/8 (under the gusset): ends one field rivet row apart
        string Prime(string m) => System.Text.RegularExpressions.Regex.Replace(m, @"([A-Z]+)(\d+)", x => int.Parse(x.Groups[2].Value) == 22 ? x.Value : x.Value + "'");
        foreach (var (j, mem, page, w, ts, a0, a1, beyond, label) in fills)
        {
            int n = int.Parse(j.Substring(1));
            foreach (bool pr in new[] { false, true })
            {
                if (pr && n == 22 && mem == "U22-L22") continue;
                string pt = pr && n != 22 ? j + "'" : j, mn = pr ? Prime(mem) : mem;
                var jd = t.Joints.LastOrDefault(x => x.Point == pt && x.Plane.Length == 0) ?? t.Joints.LastOrDefault(x => x.Point == pt);
                if (jd == null) { jd = new TrussJointDefinition { Point = pt }; t.Joints.Add(jd); }
                jd.Plates ??= new List<TrussJointPlate>();
                double lo = double.IsNaN(a0) ? 0 : In(a0);
                for (int k = 0; k < ts.Length; k++)
                {
                    double hi = mem == "U22-L22" ? In(u22Ends[k]) : !double.IsNaN(a1) ? In(a1) : ReachAlong(jd, mn) + In(beyond);   // ReachAlong: see note below
                    jd.Plates.Add(new TrussJointPlate
                    {
                        Member = mn, Face = TrussPlateFace.WebOutside, Fill = true, Width = In(w), Thickness = In(ts[k]), Length = hi - lo, Offset = (lo + hi) / 2,
                        Label = ts.Length > 1 ? $"{label} (fill {k + 1})" : label, Material = "C", Source = $"p.{page} (batch 8b)",
                        Unresolved = U($"p.{page}", $"{label} on {mn} at {pt}", "the callout gives width x thickness only; the fill extent (member end to a few inches past the gusset edge, where it carries field rivets) is drawn, not dimensioned",
                            $"fill from the member end to {(hi / In(1)):0.0} in. from the WP along the member (scaled 3/4 in. = 1 ft-0, +-1 in.)" + (ts.Length > 1 ? "; stepped fills ending one rivet row apart, the longest on the member (order inferred from the leaders)" : "") + "."),
                    });
                    nF++;
                }
            }
        }
        report.Add($"main truss: {nF} web member fill definitions (batch 8b: U6, L7, U8, L9, U10, U21, L21, U22; both halves)");
    }
```

`ReachAlong(jd, member)` = distance from the WP to where the member axis leaves the joint's gusset outline (the approach section 6a `Reach(du, dv)` on `jd.Outline`,
direction = the model member direction at the joint). Only the two diagonals at U21 / L21 need it; if it is not wanted, use the scaled distances
U21-L22 a1 ~= gusset reach + 4.8, U20-L21 a1 ~= gusset reach + 18.6 once someone measures the reach on the model outline.

Rivets past the gusset edge: `rivet-counts.csv` already has `beyond` rows for these members (U6-L6 11, U7-L7 13, U8-L8 6, U9-L9 7, U10-L10 17, U22-L22 14; U21 / L21 have no rows);
with the fills built the generator's `Beyond` / `BeyondLength` mechanism can place them through fill + member.

### 1.3 Confidence / UNRESOLVED
- Sizes: high (all callouts legible at 300 dpi; each thickness = the model gap).
- Extents: medium (scaled, +-1 in.; U21 / L21 WP read by me: U21 (2822, 945), L21 (2822, 2065) at 300 dpi, +-5 px; p.89 U21 / L21 joints have no outline row of their own - they use U20 / L20's).
- U20-L21 fill past the gusset edge 18.6 in. with ~12-16 field rivets: low (the edge lines are dense; re-check on g89_L21diag.png).
- UNRESOLVED (stays flagged): fill lengths are not dimensioned on any sheet; smaller gaps at U7, U9, U12-U17, L8, L13, L15-L18 (GN01 "2-7 per face") were not read in this pass.

---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## 2. 8e - U22 (GN02), U11 post (GN03), L11 outer layer edge (GN04)

### 2.1 GN02 U22: it is the vertical's FILLS, not the pin plates
p.90 elevation of U22 (p90_U22.png, g90_U22v.png, g90_U22v2.png): the vertical U22-L22 (2-18 [ 42.7) carries **"4-Fills 18 x 1 1/8 (C)"** and **"2-Fills 18 x 7/8 (C)"**
= per face 1 1/8 + 1 1/8 + 7/8 = **3 1/8 in.** = exactly the model's 3.13 in. air (the gussets stand out by the three pin plates 1 1/8 + 1 + 1 on the chord). The pin plates are
on the chord only and do not run down over the vertical's rivets.
- Member end 21.1 in. below the WP, gusset bottom edge 39.5 in. (outline -39.5), 16 shop rivets (4 x 4) inside the gusset, then three rows of 4 FIELD rivets past the edge
  with plate-end lines at 43.1, 46.7 and 50.1 in. below the WP: stepped fills ending one rivet row apart. The 1 1/8 leader points at the first step (43.1), the 7/8 leader at
  the last line (50.1); order in the stack inferred: gusset | 1 1/8 (to 43.1) | 1 1/8 (to 46.7) | 7/8 (to 50.1) | channel back.
- Data: the U22 row in the section 1.2 block (three fills, member side first). Confidence: sizes high, extents medium (scaled), stack order medium (inferred).

### 2.2 GN03 U11: the sheets carry ALL post plies up to the U11 gusset
- p.79 U11 elevation callout (p79_post_label.png): "U11-L11: 2-Cov. Pls (23-12) x 1 1/8, 4-Ls 5 x 5 x 7/8, **4-Web Pls 34 x 3/4, 2-Web Pls 18 x 7/8, 2-Web Pls 34 x 1 1/8** (S)
  (for complete main material see stress sheet)" - the 34 x 9/16 o.s. is NOT in the U11 list (it is only at the floor beam, p.79 left view: 34 x 3/4, 34 x 3/4, 34 x 1 1/8, 34 x 9/16).
- Gusset in-to-in at U11 "2'-5 in. in Gus. Pls." (p.79 Section C and left view) = 1'-11 3/4 b.b. Ls + 2 x (1 1/8 + 3/4 + 3/4) = 29.0: the gusset bears on the 1 1/8 plies.
- The U11 elevation (p79_U11post.png, g79_U11top.png) draws the post plies continuous from the post top (under the 3/4 Bent Pl. (S), about 20 in. below the WP at the post CL,
  scaled) down past the gusset, no "End of ..." note on the post.
- So in the model the 1 1/8 + 3/4 + 3/4 plies (W2-W4 of `POST U11-L11`) must be present from the post top through the U11 gusset. The script (truss-ptc-arch.csx line ~255) gives
  the 1 1/8 `Start = 0` "from U11 (member start)" and the 3/4 plies full length, yet the model has 2 5/8 in. (= 1 1/8 + 3/4 + 3/4) of air 26-31 in. below U11: a generator /
  ply-span issue (check the member direction of `U11-L11` vs `Start`, and whether a ply with a partial outer ply (9/16, listed first) keeps its stacked position), not plan data.
- No data rows; confidence high for the plan facts.

### 2.3 GN04 L11 outer 3/4 layer edge vs the rivet row
p.80 Half Section E (p80_secE.png, g80_secE.png, g80_el.png): El. 98.2010 line at y = 1945; outer 3/4 plate step at y = 1568 = **44.0 in. above El. 98.2010** (8.56 px/in;
43.5 at the 2'-5 dimension's 8.67 px/in). `gusset-layers.csv` row 19 has v = 45 ("3'-9"; the review read 3'-5). The drawn post-web rivet rows next to the step: y 1560 = 45.0 in.
(0.9 in. ABOVE the step) and y 1600 = 40.3 in. (3.7 in. below); row pitch here about 4.6 in. The sheet itself draws a row just above the plate end (the 2 in. heads of 1 1/4 in. rivets
would ride ~0.1 in. over the edge at the drawing accuracy +-1 in.).
- Proposed data (target `docs/plan-data/ptc-delaware-river/connections/gusset-layers.csv`, row 19 `outline_uv_in`): `"-200,-200; 200,-200; 200,44; -200,44"` with the extent note
  "Section E: step 3'-8 above El. 98.2010 (300 dpi, 8.56 px/in; 3'-7 1/2 at the 2'-5 dimension), drawn 0.9 in. below a post-web rivet row and 3.7 in. above the next". This moves
  the edge 1 in. down (0.56 -> ~1.5 in. to the model row at 45.56; head radius 1.0 in.: clear).
- Confidence: medium (scaled section, +-1 in.). The model rows (45.56 / 42.78) come from the elevation positions, not from Section E; their spacing (2.8 in.) differs from the
  section's (4.6 in.) - not reconciled here. A generic guard (layer edge within a head radius of a rivet of the layer below) is still the better fix.

---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## 3. 8g - walkway connections

### 3.1 CL walkway hand rail connection to the floor beams (bws N3), p.106
p.106 "MATERIAL BETWEEN FLOOR BEAMS" (p106_main.png): "3/4 x 3/16 Grating (See Sheet 61); 10 [s @ 15.3; 1-PL 14 x 3/8 x 2'-0, 2 Conn. Ls 4 x 4 x 3/8; **Hand Railing 3 x 2 1/2 x 5/16**
(Grind all Shear Edges Smooth); **Hand Rail Conn. to Fl. Beams 6 x 4 x 3/8 Ls**". NOTE (p.106): "Hand Rails & Toe Pls - 3/4 dia Rivet Bolts Button Hd., Hex. Nut; Rail Post to Walkway [s - 3/4 dia Rivet Bolts".

MAIN SPAN view at the floor beam (1/2 in. = 1 ft-0, 5.78 px/in; g106_mainsec.png, g106_chan.png): two vertical angles, one each side of the walkway, on the floor beam web face:
- heel at the back of the walk channel (channels 2'-0 b.-b. = 24.2 in. scaled), i.e. 12 in. each side of the walkway CL; the leg on the floor beam web points OUTWARD from the
  channel back (drawn 3.5-4 in. wide = the 4 in. leg), the 6 in. leg stands out along the walkway (the rail angles connect to it);
- bottom at the walk channel bottom (y 1145), top 4'-6 above it (y 830, scaled 54 in.; = about 3'-8 above the grating top) - scaled, +-2 in.;
- 10 open (field) rivets per angle through the floor beam web, pitch ~6 in. (image-counted).
- Data (target walkways-ptc.csx `WalkCl`, one pair per floor beam per CL walkway, both sides of each floor beam web where the walk passes, flagged dimension-only):
  `L6x4x3/8`, vertical, heel (u = floor beam web face, n = walk CL +- 12 in.), 4 in. leg on the web outward, 6 in. leg along the walk toward the span, z from the channel bottom to
  +54 in. (scaled), 10 rivets 3/4 in. (rivet bolts per the note) at ~6 in. pitch into the floor beam web; the rail 3 x 2 1/2 x 5/16 bolted (3/4 rivet bolt) to the 6 in. leg.
- **The rail is 3 x 2 1/2 x 5/16** (p.106 material list); the model's W12 run uses L 3 x 2 1/2 x 3/8 - change the thickness to 5/16.
- PL 14 x 3/8 x 2'-0 + 2 Conn. Ls 4 x 4 x 3/8: listed, not identifiable in the views at this resolution (probably the channel spacer / seat at the floor beam) - UNRESOLVED.
- Note for the W12 decision (one rail per side): the "EXPANSION DETAIL OF WALKWAY AT PIER 17" elevation (g106_exp.png) draws the rail at the pier joint as three horizontal
  sliding bars per side (main side levels 1'-6 3/4, + 1'-2, + 1'-0, + 6 3/4 up the 6 x 4 angle); reported only, the user decision stands.
- Confidence: sizes high; positions medium (scaled, 1/2 in. scale).

### 3.2 FB11 walkway hangers (W6), p.103
- Notes (g103_notes.png / g103_notes2.png): "Conn. Ls to stringers 9 x 4 x 1/2; **Hangers 4 x 4 x 3/8 - 7/8 dia Rivets to Stringer, 3/4 dia Rivets to Walkway**; Rail Post to walkway 3/4 dia Rivet
  bolts - Button Hd. & Hex. Nut"; railing "Top Rail 3 x 3 x 5/16, Mid Rail 3 x 2 1/2 x 5/16, Posts 3 x 3 x 5/16 spaced @ 6'-0 +-"; walkway "3/4 x 3/16 Grating, 10 [s @ 15.3, Conn Ls 6 x 6 x 3/8 - 3/4 rivets".
- Elevation (g103_hanger.png): ONE "2-Hangers to stringer" location per walkway half: a vertical L 4 (4 in. wide drawn) from below the walk channel up past the top rail (broken
  - continues to the stringer), 2 rivets into the walk channel, and the top / mid rails riveted to it (it doubles as a post).
- Position along the floor beam: NOT DETERMINABLE - the elevation is drawn with breaks (the 43'-6 CL truss to CL bridge dimension spans 2,746 px = 5.26 px/in vs 5.8 px/in
  local, and the rail runs are interrupted); scaled positions from the truss / from the CL disagree (25'-5 vs 13'-10 3/4 from the truss/CL). "To stringer" = on a stringer line:
  main span stringers at 0 / 8'-3 / 16'-6 / 24'-9 / 33'-0 from the CL (p.94 "4 @ 8'-3"), p.85 layout 38'-3 / 5'-3 / 4'-5 / 6'-1 from the gutter. UNRESOLVED: which stringer.
- Return at the CL: the elevation's right end "2'-0 | 1'-0" = the FB11 walk ends ON the CL walkway (which runs 1'-0 to 3'-0 north of the CL bridge, p.101 / p.106): the
  "2'-0 x 1'-0 return" of the earlier review row is that crossing, not a separate piece.
- Proposed data: hanger pair 2-Ls 4x4x3/8 on the walk channels at the stringer line nearest the scaled position (user / agent choice), from the channel bottom to the stringer
  bottom flange, Conn. L 9x4x1/2 to the stringer, 7/8 rivets to the stringer, 3/4 to the walk; flag position + length dimension-only. Confidence: sizes high, position low.

### 3.3 Pier platform rails per View B + hangers (W8), p.104
- View B (g104_viewB.png; 8'-0 = 555 px): the 8'-0 long side has a **full top rail (41.5 in. above the grating, scaled) and mid rail (22 in.)**, posts at both ends (2.6 in. in from the
  platform ends) and two intermediate verticals at 34.3 / 62.3 in. from the left end (+-14 in. about the platform centre, 2'-4 apart, scaled) that continue UP (broken) = the
  **hangers 4-Ls 4 x 4 x 3/8 to the bottom strut**, also serving as rail posts.
- Plan "INSPECTION PLATFORM at PIER 18" (g104_plan.png): View B is taken from the side AWAY from the truss (arrow below the platform looking toward the truss) -> the full rail is on the
  INBOARD (CL-bridge) long side. Section A (cut through the ladder, looking toward the truss; g104_secA.png) shows the truss-side long side with posts only at the two ends (the
  ladder comes up through that side) and the end posts' short rail returns = rails across the two 3'-3 1/16 ends.
- So: rails on the inboard 8'-0 side (top + mid, 4 posts incl. the 2 hangers) + both short ends (as now); truss side open except the end posts. Hangers: 2 per long side?
  View B shows 2 on its side; "4-Ls" -> 2 per long side at the same stations (the truss-side pair is behind the ladder in Section A) - medium.
- Notes: "Platform Hangers to Bott Strut: 4-Ls 4 x 4 x 3/8 - 7/8 dia Rivets to Strut, 3/4 dia Rivets to Platform"; railing as p.103.
- Proposed data (walkways-ptc.csx `Platform()` for L11 / L11', N and S): add the inboard long-side rail (top 3 x 3 x 5/16 at +41.5 in., mid 3 x 2 1/2 x 5/16 at +22 in. above the
  grating, scaled), posts at u = 2.6 in. from each end + the two hangers at u = 48 +- 14 in. (from the platform end), hangers L 4x4x3/8 from the platform channel up to the bottom
  strut (length = model strut bottom - platform; not dimensioned, drawing broken). Confidence: medium (scaled; hanger length UNRESOLVED).

---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## 4. 8h - remaining items (partly)

### 4.1 L13-W14 "2-Web Pls 12 x 1/2" (p.71 FLOOR LATERALS, Panel L13-W14) - DONE (section geometry)
g71_L13W14.png / g71_box.png: section = "2-Web Pls 12 x 1/2; 2-15 [s 50.0; 2-Pls (13-6) x 1/2", channels top and bottom, backs 1'-1 5/8 apart (vertical), flanges toward each other,
side plates (13-6) x 1/2 (H.H. 6 x 12 @ 2'-6 c.c.) on the outside of the flange toes, "Horiz. axis" across. The two 12 x 1/2 web plates are drawn INSIDE the channels, one on each
channel web, centred between its flanges. (p.71 is "SCALE NONE": position by the callout only.)
Proposed (target truss-ptc-arch.csx, the `FL L13-W14` section; `TrussSectionDefinition.CustomPieces` in the section frame of the rotated LacedChannels, x across = the channel depth
direction, y up): two plates x = -6 .. +6 in., y = +(6.8125 - 0.716) - 0.5 .. +(6.8125 - 0.716) = **+5.597 .. +6.097 in.** and mirrored **-6.097 .. -5.597 in.** (backs at +-6.8125 = 1'-1 5/8 / 2,
C15x50 tw = 0.716), full member length; check the frame axes of the rotated section before applying. Confidence: arrangement medium (drawn, small), sizes high.

### 4.2 U11 portal ends (B09) - PARTLY (no exact data)
p.100 draws the portal strut / knee braces but not the strut-to-post end; p.79 Section C (p79_secC.png / p79_secC2.png) shows the strut end on the inner face of the
"2'-5 in. in Gus. Pls." with a connection piece carrying one line of 5 shop rivets to the strut web and a small bracket at the bottom cover; size not labelled in the section,
rivets to the gusset not drawn; p.79 note: "Rivets in lateral, portal and sway bracing connections 1 in. carbon steel, except as noted". UNRESOLVED: angle size, rivets to the gusset.

### 4.3 Floor laterals FX14-20 at W16 / W18 / W20 (B10) - PARTLY (callouts only)
p.99 "FLOOR LATERAL CONN. AT FLOOR BEAMS W16, W18 & W20" (p99_W16.png): 14 BP 73 laterals on 7/16 Pl. (one above = quarter top section, one below = quarter bottom section),
free edge stiffened by 1-L 3 1/2 x 3 x 3/8 (rivets at 3 1/2 in.), plate to the floor beam by 1-L 4 1/2 x 3 1/2 x 1/2 N.S. + 1-L 3 1/2 x 3 1/2 x 1/2 F.S. ("Bend Line"), 7/8 carbon rivets;
about 12 shop rivets per lateral end, about 10 field rivets in the floor beam line (image-counted, low). Section D/E/F (floor beam web): 2-3/8 Pls, 2-3/8 Fills, 3/4 Bent Pl., 1-L 8 x 8 x 1/2,
1-7/16 Fill, 1-3/4 Fill, 1-L 4 1/2 x 3 1/2 x 1/2, 1-L 3 1/2 x 3 1/2 x 1/2, 1-L 6 x 4 x 3/8; "slot lateral Pl." through the web (Sections A at W15/W17/W19/W21). Notes: "All rivets in bracing
gusset plates shall be 1 in. carbon unless noted; stitch rivets 7/8 carbon; rivets shown in Secs. D, E & F 7/8 carbon". Positions not scaled in this pass - UNRESOLVED.

### 4.4 Not read in this pass
- R01 drawn joint rivets L10 (U9-L10) / L13 (U12-L13): the positions are already in `rivet-positions.csv`; the drop is a placement issue (clean stack), not plan data.
- WM22 bracing connection (p.93), L13 1/2 K far ends (p.98), F8 at L13: not read (time). Edge angles G02, GL03, inlay plates GL07, L22 split GL06, L5 fills GL11, L0 B4,
  shear lock B8, forging key B7, sections S5-S9 / S14: not in this pass.
