# bonner-b2500 - Unit 5 (spans 19-29): precast post-tensioned segmental concrete box girder

Source: "B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf", sheets S3-01..S3-144 = PDF p.249-392 (all current, see ../INDEX.md; none deleted).
"p." = PDF page. Format per docs/plan-data/EXTRACTION.md. Plan precision 1/16" (0.01 ft). No fallback guesses: anything unread or not reconciling is in UNRESOLVED.

Tags: `TEXT` read from the text layer (fractions are custom font glyphs, decoded with a glyph table calibrated on 300-400 dpi raster clips, see Method),
`RASTER` confirmed on a raster clip, `VECTOR` measured from the drawing's vector geometry (scale taken from a printed dimension, accuracy about 0.01 in),
`DERIVED` computed from other plan values (formula shown), `UNVERIFIED`, `UNRESOLVED`.

Files in this folder (all CSVs have page + sheet + tag columns):
`unit5-spans.csv`, `segments.csv` (275 segments/joints), `box-sections.csv`, `box-section-geometry.csv`, `geometry-control.csv`, `pt-tendon-schedule.csv` (217 rows),
`cantilever-tendon-anchors.csv`, `transverse-pt-stressing.csv`, `future-pt-pipes.csv`, `web-shear-keys.csv`, `bearing-data.csv`, `piers.csv`,
`typical-segment-bar-list.csv`, `reinforcement-callouts.csv`, `quantities.csv`, and `sheet-text/pNNN_S3-xx.txt` (decoded text layer of every sheet, for provenance; drawing geometry is not in it).

## Method notes
- The text layer carries dimensions but stacked fractions are glyphs. Table (verified on raster): 1/16 U+02C6, 1/8 U+201E, 3/16 U+2030, 1/4 U+201A, 5/16 U+0160, 3/8 U+2026, 7/16 U+2039, 1/2 U+2022, 9/16 U+0152, 5/8 U+2020, 3/4 U+0192, 13/16 U+017D, 7/8 U+2021 (11/16 and 15/16 do not occur in S3). Every key table below was also read on a 130-220 dpi clip.
- Vector measurements (S3-20): page scale 1.500 pt per inch (42'-7" = 766.4 pt); the box outline was read from the PDF drawing objects and checked against every printed dimension on the section (section 2).
- **Side finding for the general-drawings summary (../SUMMARY.md):** S0 p31 prints the Unit 6/9-13 span as 160'-10 1/16" (not 160'-10"), p30 160'-7 1/16", p32 "803'-2 1/16"" for a 5-span unit (summary says 804'-2"); the earlier pass dropped the 1/16 glyph. Not corrected here.

## 0. What the unit is
Navigation unit, 11 spans (19-29), 3,550'-0" along -L- (ARC/TAN), 12 supports (bents 18-29), STA 57+45.00 to 92+95.00. Variable-depth single-cell precast segmental concrete box girder (one box, 42'-7" out to out, carries the 40'-0" clear roadway),
post-tensioned, built by **balanced cantilever** erection from piers 19-28 with cast-in-place closure joints at mid-span (spans 20-28) and next to the end segments (spans 19, 29), then continuity tendons. Segments are epoxy-jointed match-cast precast with shear keys,
temporary 1 3/4" PT bars during erection, transverse PT in every segment (S3-61..65 construction sequence and erection procedure). Supported on precast hollow column segments + precast bent caps (bents 18-29) on 36" sq PS pile footings, disc bearings.
Both unit ends (bent 18, bent 29) have an **end segment** (ES) with a modular expansion joint to Unit 4 / Unit 6 (joint details are in S2 / S4 drawings, not in S3).
The unit is fixed (pinned, Type I bearings) at bents 23 and 24; other bents guided or free (Type II / III).

## 1. Unit layout
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Bents / WP stations | 18 57+45.00, 19 59+45.00, 20 62+95.00, 21 66+45.00, 22 69+95.00, 23 73+45.00, 24 76+95.00, 25 80+45.00, 26 83+95.00, 27 87+45.00, 28 90+95.00, 29 92+95.00 | sta | p267 S3-19 (geometry control table); p251-253 | TEXT | same as ../bents.csv; all bents radial |
| Spans | 19: 200'-0"; 20-28: 350'-0" each; 29: 200'-0" | ft | p263-266 S3-15..18 | TEXT | sum 2 x 200 + 9 x 350 = 3,550 (checked) |
| Span / segment length basis | "All dimensions are measured along centerline -L-. Span lengths are horizontal projections along centerline girder. Segment lengths are chord lengths along centerline of girder." Segments are straight chords on the curved alignment ("segment chording") | | p263 notes, p268 note 1 | TEXT | relation of the 350'-0" -L- arc to the girder chord not given: UNRESOLVED |
| Construction method | Balanced cantilever from each pier 19-28 (split bent segment pair, then U1/D1 ... in pairs, crane + beam-and-winch), closure joint pours, continuity PT; shoring towers at the cantilever piers | | p309-313 S3-61..65 | TEXT | sequence below |
| Unit-end elements | 18-ES at bent 18 and 29-ES at bent 29 (end segments), each in span 19 / 29 next to a 3'-6" closure joint | | p263, p266 | TEXT | |
| Expansion joints | Modular expansion joint seals at bent 18 and bent 29 (Unit 4 / Unit 6 ends); joint details "bound under separate cover with S2 / S4 drawings"; blockout in the ES; total design movement 30.00 in at bents 18 and 29 | | p271 note 4, p333 S3-85, p319 | TEXT | joint gap/setting values not in S3: UNRESOLVED |
| Fixity | bents 23, 24 pinned (Type I, longitudinal horizontal force 859-872 kip); bents 19-22, 25-28 Type II (guided parallel to the girder CL); bent 18 L / 29 L Type II, 18 R / 29 R Type III (non-guided) | | p317 S3-69, p319 S3-71 | TEXT/RASTER | matches ../bents.csv FIX at 23, 24 |

### Segment scheme (segments.csv, unit5-spans.csv)
Distances x are along the girder CL from the span's start bent CL (x = 0) to the end bent CL (x = span length). Names XX-Uk / XX-Dk: bent XX, k-th segment upstation / downstation of that bent; XX-UB / XX-DB: the two 5'-0" halves of the split bent segment at bent XX; ES = end segment; CJ = closure joint.
| span type | scheme (x in ft) | tag |
|---|---|---|
| spans 20-28 (350') | (n-1)-UB 0..5 ; (n-1)-U1..U12, 14'-0" each, 5..173 ; closure joint 4'-0" nominal 173..177 ; n-D12..D1, 14'-0" each, 177..345 ; n-DB 345..350 | TEXT (12 SEGMENTS @ 14'-0" = 168'-0" x 2, 5'-0", 4'-0" C.J., p263-266, p268) |
| span 19 (200') | 18-ES 8'-8" long, its anchor face at 9'-6" - 8'-8" = 10" from bent 18 CL (DERIVED) .. 9.5 ; closure joint 3'-6" 9.5..13.0 ; 19-D13..D1, 14'-0" each, 13..195 ; 19-DB 195..200 | TEXT + DERIVED (ES start) |
| span 29 (200') | 28-UB 0..5 ; 28-U1..U13, 5..187 ; closure joint 3'-6" 187..190.5 ; 29-ES 190.5..199.17 | TEXT + DERIVED |

Segment types: bent (split pair, 5'-0" each half, "B"), typical (14'-0"), closure (cast-in-place, nominal widths: "dimensions will be adjusted to reflect contractor's means and methods, bearing offsets, camber, temperature", p281 note), end segment (8'-8", S3-23).
Counts: typical 14' segments 242 (= 13 + 13 + 9 x 24), closure joints 11 (9 x 4'-0", 2 x 3'-6"), bent segments 20 (10 bents x DB/UB), end segments 2 (consistent with S3-34 except the bent-segment count, see cross-checks).
Weights stated: end segment 110 ton, split bent segment 100 ton (p271 note 9, p277 note 6). Typical segment weight: not given (UNRESOLVED).
Survey disc blockouts ("TP" / "N"), access openings (A.O., A.O.B., T.A.O.), navigation light conduit (N.L.), solar array attachment (S.A.P.) per segment: p263-266 and the span / station / designation table at the bottom of p266 (text in sheet-text; not tabulated).

### Erection sequence (p309-313, S3-61..65; contractor's information only)
Step 1-2 bent 23 (shoring tower, bearings, bent segments, cantilever); 3 bent 22; 4 closure span 23 (strong back and forms, unlock bearings at 22, stress continuity tendons span 23), then bent 21; 5 closure span 22, bent 20; 6 closure span 21; 7-8 bent 19 with 60 ton counterweight and end segment 18-ES; 9 closure span 19; 10 closure span 20, bent 24; 11 bent 25; 12 closure 25, bent 26; 13 closure 26, bent 27; 14 closure 27, bent 28; 15 segment 28-U13 and end segment 29-ES; 16 closure span 29; 17 closure span 28; 18 closure span 24 last (jack apart, max 600 ton centred 3'-3" below top of deck until the joint opens 1.25 in or 600 ton, pour, stress continuity tendons); 19 finish adjoining units, grind and groove deck, expansion joint devices, parapet and rail.
Cantilever erection (p313): shoring tower, two bent segments (DB, UB) epoxied and squeezed with permanent bottom PT bar + temporary top bar, then U1 / D1, stress cantilever tendon T1, then U2 / D2, T2, ...; temporary PT bars 1 3/4" dia grade 150 jacked top bars 75 ksi, bottom bars 45 ksi (bottom bars anchored in U1 / D1 75 ksi); temporary bars stay to the third segment from the tip; segment lifting device 130 kip centred 2 ft from the tip (general notes).

## 2. Box section
Common outline (S3-20 p268 "Interior section" and "Section at bent"; S3-14 p262; `RASTER`+`VECTOR`, coordinates in box-section-geometry.csv). Symmetric about the girder CL; depths measured **normal to the top surface**; the box is cast square to the cross-sloped top surface (see geometry control).
| item | value | tag | check |
|---|---|---|---|
| Overall top width | 42'-7" out to out (21'-3 1/2" each side of CL); 40'-0" clear roadway; 1'-3 1/2" from clear roadway edge to deck edge each side (varies "due to superelevation and segment chording"); 1/2" edge detail | TEXT/RASTER | S3-14 |
| Top slab at CL | 9" thick, flat to 5'-1" from CL, then haunch down to the web at 5'-1" + 5'-6" = 10'-7" from CL, 2'-2" below the top surface; R = 1'-0" fillet at the web / top slab corner | RASTER+VECTOR | vector 9.00", 61.0", 127.0", 26.05" |
| Wings (cantilevers) | tip 10" thick, flat for 4'-1" from the tip, then sloped underside over 5'-3" to 2'-2" thickness at the web outer face; 9'-4" from tip to web outer face at the top | RASTER+VECTOR | vector 10.00", 49.0", 63.0", 26.07" |
| Webs | outer face slope 4 vertical : 1 horizontal (drawn 0.2498), thickness 1'-4" normal to the web (vector 15.98"); outer face from (11'-11 1/2" from CL, 2'-2" below top) down to the soffit corner at W/2; inner face parallel, 16.49" horizontal | RASTER+VECTOR | |
| Bottom slab | thickness T (variable, table below); soffit width 2 x W/2; inner haunch at the webs: 6" rise over 2'-5 1/2" horizontal | RASTER+VECTOR | |
| Other | 2-1" grooves in the deck edge (Detail A: 1/8" flat, 1/16" typ., 3/4" chamfer, 3 1/2"); 2" dia vent hole centred 4'-1 1/2" from W/2 on the low side of each segment; 2 3/4" dia ducts for temporary 1 3/4" PT bars; 3 1/2" dia electrical conduit holes (right of CL only); threaded inserts for fall protection | TEXT/RASTER | p268, p271 |

### Variable depth profile (box-sections.csv)
Printed table S3-20 (p268, `RASTER` 220 dpi). Section j is at x = 5'-0" + 14'-0"(j-1) from the bent CL (joint planes: section 1 = face of the 5'-0" bent half = joint to segment 1). The elevation labels sections 12 again at the closure joint faces, in segment 13 of spans 19 / 29 and in the end segment: constant 9'-0" depth from x = 159 ft to the end of the span.

| section | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| x from bent CL (ft) | 5 | 19 | 33 | 47 | 61 | 75 | 89 | 103 | 117 | 131 | 145 | 159 |
| structure depth H | 19'-0" | 17'-3 1/8" | 15'-8 3/8" | 14'-3 1/2" | 13'-0 5/8" | 11'-11 3/4" | 11'-0 3/4" | 10'-3 7/8" | 9'-8 7/8" | 9'-4" | 9'-1" | 9'-0" |
| half-bottom slab width W/2 | 7'-9" | 8'-2 1/4" | 8'-6 7/8" | 8'-11 1/8" | 9'-2 7/8" | 9'-6" | 9'-8 7/8" | 9'-11" | 10'-0 3/4" | 10'-2" | 10'-2 3/4" | 10'-3" |
| bottom slab thickness T | 2'-0" | 1'-9 1/4" | 1'-6 3/8" | 1'-3 5/8" | 1'-0 3/4" | 10" | 10" | 10" | 10" | 10" | 10" | 10" |

- Depth at the bent CL: 19.25 ft (S3-19 box height) = 19'-3" (S3-29 section at bent); end segment depth 9'-0" (S3-19 bents 18 and 29, S3-23).
- Curve: the plans print no equation. The 12 values fit a parabola with vertex at x = 159 ft: H = 108 in + 0.0050599 in/ft^2 x (159 - x)^2 (so H(5) = 19'-0"), max residual 0.051" (< 1/16"): `DERIVED` reconstruction. Joint positions confirmed on the S3-20 elevation vector drawing (depths 9.00, 9.00, 9.083, 9.333, 9.75, 10.333, 11.083, 11.979, 13.063, 14.292 ft at successive joints from mid-span; drawn at equal horizontal / vertical scale 4.8 pt/ft).
- W/2 is fully determined by H through the 4:1 web slope: W/2 = 143.5" - (H - 26")/4; matches the table to 0.062" max (1/16"); S3-29 bent segment: H = 19'-3" (231") gives 92.25" = 7'-8 1/4" (printed 7'-8 1/4").
- Gross concrete area per section (polygon, no fillets, diaphragms, blisters): 126.1 ft2 (sec 1, T = 2'-0") ... 87.1 ft2 (sec 12): `DERIVED` (box-sections.csv).

### Pier (bent) segment and end segment (p277 S3-29, p271 S3-23)
| item | value | tag |
|---|---|---|
| Bent segment | split along the girder CL into downstation (DB) and upstation (UB) halves, each 5'-0" long, 42'-7" wide; H = 19'-3" at the CL; W/2 7'-8 1/4"; web thickness 1'-4"; diaphragm with openings (utility diaphragm opening, 3'-0" dia access opening in the bottom slab); 8" dia full-depth bearing pintle blockouts; cast-in-place plinth; weight 100 ton per half | TEXT/RASTER 60 dpi |
| Bearing line positions | two bearings per bent at 8'-3" each side of the girder CL (p271, p354 "8'-3" typ."); bearing 5'-6" from the bent control line along the girder at end bents 18 / 29 (p267) | TEXT/RASTER |
| End segment | 8'-8" long (6'-6" + 2'-2"), H = 9'-0", 42'-7" wide, solid end diaphragm with opening for the vermin screen door, expansion joint blockout in the anchor face, 4 transverse tendons E1-E4, vertical PT bars; weight 110 ton | TEXT/RASTER |
| Closure joint | cast-in-place, 4'-0" (spans 20-28) or 3'-6" (spans 19, 29), with deviator block for the future PT pipes and the transverse tendon(s) | TEXT |

## 3. Geometry control (geometry-control.csv, S3-19 p267; S3-15..18)
| bent | station | cross slope | box height (ft) | DIM "B" (in) | long. slope | DIM "A" (in) |
|---|---|---|---|---|---|---|
| 18 | 57+45.00 | 4.50% | 9.00 | 4.855 | 3.75% | 4.047 |
| 19 | 59+45.00 | 4.50% | 19.25 | 10.384 | 2.43% | 5.612 |
| 20 | 62+95.00 | 4.50% | 19.25 | 10.384 | 0.13% | 0.300 |
| 21, 22 | 66+45.00, 69+95.00 | 4.50% | 19.25 | 10.384 | 0.00% | 0.000 |
| 23-27 | 73+45.00 ... 87+45.00 | 2.50% | 19.25 | 5.773 | 0.00% | 0.000 |
| 28 | 90+95.00 | 2.50% | 19.25 | 5.773 | -2.17% | 5.012 |
| 29 | 92+95.00 | 2.50% | 9.00 | 2.699 | -3.49% | 3.767 |

Meaning (DERIVED, verified on all 12 rows): the box at a bent is square to the **top surface** (rotated by the cross slope and longitudinal grade) while column / cap / footing are vertical, so the box CL at the bottom is offset from the bent CL by DIM "B" (transverse) = H x sin(atan(cross slope)) and DIM "A" (longitudinal) = H x sin(atan(long. slope)) (e.g. 231" x 0.044954 = 10.384"). Tabulated values reflect a mean temperature of 60 F and "do not reflect bearing offsets" (bearing initial offsets are in the bearing table p319; revision 1 08/30/16 added the asterisk for bents 19, 20, 27, 28). No casting curve / camber table is in the plans (UNRESOLVED).
- Cross slope from S0-03 (../SUMMARY.md): 4.5% to STA 70+29, 2.5% from STA 70+83 (transition between bents 22 and 23); direction of slope not resolved.
- Vertical: the profile grade at the CL is the top of the box; soffit EL = PGL - H. Top of bent EL + 2.40 ft = soffit at bents 19-22, + 2.32 ft at 23-28 (cross-checks).
- Survey disc blockouts: TP (2'-0" min from the barrier face on the high side of superelevation), N (14'-0" from CL, pairs on both sides) per p263 legend.
- Diaphragms: pier diaphragms in both bent halves; end-segment diaphragm; openings: A.O.B. (bent diaphragm), A.O. (bottom slab near mid-span, p263-266, p315-316), vermin screen door (p314). Deviators: deviation blocks at closure joints (S3-54 p302) carrying 4" dia sch. 40 steel pipes for future PT F1-F4 (R = 22'-0"); anchor blocks: top slab (S3-50 p298), bottom slab (S3-49 p297), temporary erection PT bar blocks (S3-51..53 p299-301).

## 4. Post-tensioning
Materials / parameters (general notes p18): 0.6" dia 7-wire strand ASTM A416 grade 270 low relaxation, Ep 28,500 ksi, polyethylene duct friction 0.23, wobble 0.0002 /ft, anchor set 3/8", max jacking 216 ksi, 189 ksi at anchor after set, 200 ksi away; PT bar ASTM A722 grade 150 type II (Ep 30,000 ksi, jacking 108 ksi, 105 ksi at anchor, 96 ksi after losses, anchor set 1/16"); substructure stainless PT bars ASTM A564 630. Minimum duct radius 25'-0" (S3-36 note 5), transverse 20'-0" (p282). Segmental duct couplers at all segment joints.

| tendon group | data | source / tag |
|---|---|---|
| Cantilever (top slab) tendons T1..T14 | per bent 19..28: 2 tendons per designation (one per web side); T1-T5, T7-T13 at bents 19-21 and 26-27; T6 additionally at bents 22-25; T14 at bents 19 and 28 only; 22 x 0.6" (jacking 980 kip; T2 960 kip) or 18 x 0.6" (825 kip) for T7, T8, T11, T14 at bent 19 and the mirror T7, T8, T11, T14 at bent 28; length 35'-9" (T1) to 344'-9" (T13), 358'-9" (T14), steps of 28'-0" / 28'-3" per additional segment pair; stressing end alternates UP / DOWN sta; elongation 3.1-29.9 in | pt-tendon-schedule.csv (S3-57/58, p305-306, TEXT; header row raster-confirmed 130 dpi) |
| Anchor segments of cantilever tendons | T1..T5 in segments 1..5, T6 with T5, T7..T13 in segments 6..12, T14 in segment 12 (segment 13 where the cantilever has a 13th segment: 19-D13, 28-U13); tendon runs over the pier and anchors in the same-numbered segment on the other side | cantilever-tendon-anchors.csv (DERIVED from plan labels p285-296, S3-37..48) |
| Bottom slab continuity tendons B1..B7 | spans 20-28: 7 pairs, 20 x 0.6" 930 kip, lengths 220'-6", 193'-0", 165'-0", 137'-0", 109'-0", 80'-9", 52'-9"; spans 19 and 29: B1-B3, 16 x 0.6" 745 kip (104'-9", 91'-0", 62'-9"); ducts / anchorages sized for 22 x 0.6" (one contingency strand per tendon, note 3) | pt-tendon-schedule.csv (S3-58) |
| Closure ("C") tendons C1, C2 | 12 x 0.6" 540 kip, 81'-9" and 53'-9" (spans 20-28); 64'-0" and 49'-9" (spans 19, 29); top-slab continuity across the closure joint, anchors drawn in segments 10 and 11 | S3-58; DERIVED anchor segments |
| Stressing / grouting | top slab, bottom slab and mid-span closure tendons stressed sequentially in pairs as labelled; all ducts grouted; contingency ducts at each pier grouted after the contingency tendon is stressed or after the final cantilever tendon | S3-36 p284 |
| Contingency ducts | empty 22 x 0.6" duct with hardware (dashed in plan, e.g. T14); cantilever tendon 14 in spans 19 and 29 sized for 22 x 0.6" with 4 contingency strands | S3-36 notes 1, 15 |
| Future PT (FPT) | 2 x 22-0.6" tendons per web in 4" dia sch. 40 galvanized steel pipes F1-F4 through deviation blocks at the closure joints (strand, anchors, wedges not in this contract) | future-pt-pipes.csv (S3-60 p308) |
| Transverse PT | every segment: 4 tendons 4 x 0.6", 41'-7", 45.5 kip, elongation 3.50 in, single-end alternating; 6000 psi before stressing (3500 psi in closure joints); bent segment: vertical PT bars (4 per half, 1 3/4" dia, 283 kip), longitudinal PT bars (4 per half, 200 kip after rev. 1), F1 / F2 tendons; end segment: 4 vertical bars, E1-E4 | transverse-pt-stressing.csv (S3-24, S3-30, S3-34) |
| Temporary PT bars | 1 3/4" dia grade 150 in 2 3/4" ducts, top 75 ksi, bottom 45 ksi; anchor blocks type A / B / C | p299-301, p313 |

- Quantities: transverse PT total 121,624 lb; schedule rows sum to 1,068,208 lb of longitudinal strand (T 774,968; B 267,524; C 25,716), weights = 0.74 lb/ft per strand (all 217 rows checked).
- Tendon vertical profiles (duct P.I. heights), plan coordinates and anchorage geometry (S3-55/56 p303-304: anchor angles 3.31, 5.40, 6.86, 1.46, 3.07 deg, R = 35'-0" min, P1 / P2 anchors, SDI 22K6 / 22.6-PC anchors) are drawing data, not tabulated: UNRESOLVED as numeric tables.

## 5. Piers, bearings, joints, shear keys
- Piers (piers.csv): bents 18-29 each have 4 (bents 18, 29: 5) hollow precast column segments on a cast-in-place column base (11'-0" / 10'-0" / 9'-6" high) up to EL 17.00, 3" grout bed, precast bent cap (5'-0" or 5'-6" high); top of bent EL 68.25 (18), 64.25 (19), 68.75 (20-22), 68.83 (23-27), 65.25 (28), 70.00 (29); footing top EL 6.00 / 7.00 / 7.50 (see ../bents.csv), bottom of footing EL -0.50, MHW 0.40; 36" sq PS piles at 2:12 batter 18, 20, 24, 24, 24, 30, 30, 24, 24, 24, 24, 18 (bents 18..29); footing plan 36'-0" x 36'-0" (18, 29), 36'-0" x 43'-6" (19, 26-28), 36'-0" x 51'-0" (20-22, 25), 58'-6" x 51'-0" chamfered (23, 24). Column and cap plan sizes and reinforcement: S3-106..S3-136 (not tabulated, UNRESOLVED).
- Bearings (bearing-data.csv, S3-69..72 p317-320): disc bearings (Polytron disc, PTFE / stainless sliding surfaces, anchor rods ASTM F1554), 2 per bent, Types I (pinned), II (guided parallel to the girder CL), III (non-guided); design loads (DC 232-4160 kip, DW, PT, LL+I max / min, WS), longitudinal movement per column, initial offsets (-6.00 at bent 18 ... +6.00 at 29), assumed bearing height 14 or 20 in (20.642 at bent 19 R), temperature correction per 10 F; plan dimension summary and longitudinal restraint bars 350 kip at bents 21, 22, 25, 26 (p318); bearing replacement jacking notes (p320); CIP pedestal + plinth.
- Joints: epoxy match-cast joints with shear keys on the segment span face; web keys per section (web-shear-keys.csv, S3-35: 27 male keys at 6 1/2" in section 1 down to 11 in sections 10-12, 27 at the bent), top and bottom slab alignment keys, duct rows A / B (11 each at 9" in the top slab) and C (7 in the bottom slab, S3-35); closure joint (S3-33); expansion joints at the unit ends (modular, S2 / S4 sheets).

## 6. Reinforcing and quantities
- Typical segment (XX-D3 / U3) bar list, 17 bar marks, 8,511 lb: typical-segment-bar-list.csv (S3-22 p270). Reinforcing is otherwise given as callouts and spacings on S3-21..S3-33 (reinforcement-callouts.csv, no per-segment bar lists).
- Not printed in S3: superstructure concrete volume, strand total, mild steel total, other segments' bar lists (UNRESOLVED). Printed: transverse PT weights, tendon schedule, substructure CIP quantities per bent (quantities.csv, BOMs p336, 339, 342, 347, 350, 353).
- Cover: 2" in the box (external and internal), top slab top 3" (2.5" after grinding). Concrete: precast segmental box girders Special 6.0 ksi (lifting 2.5 ksi), CIP closure joint concrete 2.5 ksi before stressing, precast columns / bent caps Special 8.0 ksi; reinforcing ASTM A615 gr 60 in the segmental structure, stainless ASTM A955 in precast columns near MHW; 25% fly ash or 40% slag minimum; calcium nitrite in precast superstructure.
- Parapets (S3-75..79) are cast after all PT in the unit (note 1, S3-14): 2'-6" parapet with 2 bar metal rail; rail post spacing S3-80..83; solar array platform S3-84; box girder lighting S3-137..143.

## Standards referenced (general notes p17-20)
AASHTO LRFD Bridge Design Specifications 5th Ed. 2010 (interims and errata through 2010); AASHTO LRFD Bridge Construction Specifications 3rd Ed. 2010; NCDOT Standard Specifications for Roads and Structures July 2006; NCDOT Structure Design Manual (revisions through 03/11/2011); CEB-FIP Model Code 1990 (creep / shrinkage, RH 75%, loading age 60 days); AASHTO Guide Specification for Design of Bridges Vulnerable to Coastal Storms 2008; AASHTO Guide Design Specification for Bridge Temporary Works 1st Ed. 1995 (interims through 2008); RFP B-2500 April 26, 2011 (addenda 1-4); project special provisions (precast segmental bridge construction, epoxy jointing of precast segments, post tensioning and grout, disc bearings, stainless steel PT bars); ASTM A416, A722, A564, A615, A955, F1554, A563, F436, A153, A1022, F593; AASHTO M203, M270 gr 50. Loads HL-93, NC legal loads. Segmental stress limits (p19): compression 0.6 f'c temporary, 2.7 / 3.6 ksi service; tension none longitudinal, 0.23 ksi transverse, principal web 0.27 ksi.

## Cross-checks
| check | result |
|---|---|
| segment lengths sum to spans | spans 20-28: 5 + 168 + 4 + 168 + 5 = 350 (all 9 spans, 275 segment rows); span 19 = 8'-8" + 3'-6" + 182 + 5 = 199'-2" + 10" end gap (DERIVED) = 200; span 29 same. OK |
| segment counts vs transverse PT table | 242 typical, 9 + 2 closure joints, 2 end segments agree with S3-34 / S3-24. Bent segments: S3-30 table gives 18 (vertical PT bar, F1&F2) and 9 (longitudinal bar) vs 20 halves here: DISCREPANCY (UNRESOLVED) |
| W/2 vs 4:1 web slope | 12/12 sections within 0.062"; bent section exact |
| depth table vs parabola | 12/12 within 0.051"; joint depths from the elevation vector drawing agree with sections 4-12 (0.02 ft) |
| section 1 bottom slab | printed T = 2'-0"; the "section at bent" sketch on S3-20 is drawn with T = 20" (VECTOR): table taken as governing, sketch not to scale (UNRESOLVED note) |
| tendon weights | weight per tendon = strands x length x 0.74 lb/ft (217/217 rows); total = n x each (217/217) |
| transverse PT table | 119,161 + 2,216 + 247 = 121,624 lb (printed total); 242 x 492.4 = 119,161 |
| top of bent vs soffit | soffit = profile grade - H: bent 19 PGL 85.90 - 19.25 = 66.65 vs TOB 64.25 (gap 2.40); bents 20-22 71.15 vs 68.75 (2.40); bents 23-27 71.15 vs 68.83 (2.32); bent 28 67.57 vs 65.25 (2.32); bent 18 2.47, bent 29 2.16 (PGL from the vertical curves in ../SUMMARY.md, DERIVED): consistent with a 20" assumed bearing height + pedestal / plinth |
| column + cap heights | 17.00 + 0.25 + segments + cap = printed top of bent for 18, 20, 23, 25, 26, 27, 28, 29 (bent 19: the 4th segment 5'-6" was derived from the top of bent, not independent) |
| bearing fixity vs general | bents 23, 24 pinned = FIX in ../bents.csv |
| DIM A / B vs H x sin(slope) | 12/12 rows within 0.005" |
| tendon stressing forces | jacking force per strand 44.5-46.6 kip (980/22, 930/20, 745/16); consistent with 216 ksi max jacking stress x 0.217 in2 = 46.9 kip |

## UNRESOLVED
| item | pdf page | reason |
|---|---|---|
| Equation / source of the variable depth curve | p268 | plan prints only the 12-value table; the parabola is a fit (max 0.051"); soffit shape between the bent CL (19'-3") and joint 1 (19'-0") 5'-0" away is not dimensioned |
| Bottom slab thickness at the pier segment and at section 1 | p268, p277 | table T = 2'-0" vs drawn 20" in the "section at bent" sketch; S3-29 dimensions (2'-3", 2'-0", 3") not resolved to a thickness |
| Casting curve, camber, segment survey coordinates | p267 | not in the plans (bearing-offset notes only) |
| Relation of span / segment lengths (chord along the girder CL) to arc length along -L- | p263, p251 | both stated, no conversion given |
| B (bottom slab) tendon anchor segments, tendon vertical profiles / P.I. coordinates, anchor block sizes | p285-304 | drawings only; meaning of dashed T5 / T6 ducts not confirmed |
| T6 present only at bents 22-25, T14 only at bents 19 and 28 | p305 | as printed; no explanation |
| Bent segment count 18 vs 20 | p278 S3-30 | table "number of segments" = 18 (vertical bars, F1&F2) and 9 (longitudinal bars); 10 bents x 2 = 20 |
| Superstructure concrete volume, strand total, mild steel total, other segments' bar lists | S3 | not printed |
| Typical segment weight | S3 | only end segment (110 ton) and bent half (100 ton) |
| Expansion joint opening and movement settings at bents 18 and 29 | S2 / S4 sheets | "bound under separate cover", not read |
| Column plan sizes, cap sizes, cap / column / footing bar lists | p334-384 | not tabulated (text in sheet-text/) |
| Own top-of-bent EL for bents 21, 22, 24 | p340, p343 | one value printed per shared sheet (20-22; 23-24); bent 19 fourth segment height derived |
| Bearing transverse position vs the webs | p271, p277, p317 | "8'-3" typ." bearing lines vs 7'-8 1/4" half bottom width at the bent: plinth / pedestal geometry not resolved |
| Web key table column C meaning | p283 | legend shows "A" keys @ 6 1/2" = "B"; "C" location not verified |
| Survey blockout / access opening location tables | p263-266 | not tabulated |
| Cross slope direction and the 4.5% to 2.5% transition inside the unit | S0 p4 | not resolved (see ../SUMMARY.md) |

## Model mapping (what a segmental generator needs)
SBM has no segmental box feature. Data model sketch (inputs; sources above):

```
SegmentalUnit
  Alignment ref: -L- (existing); box CL = -L-, segments are straight chords
  Bents[] { id 18..29, sta, columns vertical, box square to deck: DimA / DimB offsets (geometry-control.csv),
            bearings[2] { offset from CL +-8'-3" (UNRESOLVED vs webs), type I/II/III, height, movements }, TOB elevation, cap / column / footing (piers.csv) }
  Spans[] { id 19..29, bentStart, bentEnd, length, closure length (3'-6" | 4'-0"), end segment at the unit ends }
  SegmentSequence per span (segments.csv): { name, type BentHalf | Typical | Closure | End, x0, x1 from span start, sectionAtStart, sectionAtEnd }
  DepthProfile: sections j = 1..12 at x = 5' + 14'(j-1): H, W2 (or 4:1 rule), T; H = 9'-0" for x >= 159 ft; bent CL H = 19.25 ft; ES H = 9'-0";
                interpolation between joints: parabola H(x) = 108 in + 0.0050599 in/ft^2 (159-x)^2 (DERIVED fit) - shop may use other
  BoxCrossSection(H, W2, T): polygon of box-section-geometry.csv (top slab 9", wings 10" -> 2'-2", web 4:1 x 1'-4", haunch 6" x 2'-5 1/2", fillet R 1'-0")
  Orientation: section plane normal to the deck top surface; cross slope (4.5% / 2.5%) and grade from the alignment tables; vertical = top of box at the PGL
  Joints: match-cast epoxy joints, web keys (A, B, C), alignment keys; closure widths; end faces
  Diaphragms: bent halves (5'-0", openings), ES (8'-8", openings, expansion joint blockout)
  Tendons: cantilever T1..T14 (strands, force, length, stressing end, anchor segment index), B1..B7 and C1..C2 per span, future pipes F1..F4,
           transverse 4 x 0.6" per segment, temporary bars 1 3/4", vertical bars in the bent segments; profiles (UNRESOLVED as numbers)
  Anchor / deviator blocks (sizes UNRESOLVED), duct rows A / B / C positions (S3-35)
  Parapet, rail, drains (S3-14, S3-75..83), lighting conduit
```

Existing SBM inputs that apply: alignment (PI table), VerticalProfile, CrossSlopeTable, supports and bearings, parapet / rail features, precast bent caps / columns / pile footings (partial). Missing: variable-depth box sweep along a curved alignment with per-joint rotation (cross slope), segment joints, PT tendon geometry, bent segment / end segment solids, balanced-cantilever stage data.
