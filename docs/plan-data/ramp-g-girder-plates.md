# Ramp G (Iowa DOT Design 316) - girder plate schedules, Units 2-4

Source: `B00169 - CBIS-ESI-Ramp G - Design 316.pdf`. PDF page = design sheet + 2 (title-block "SHEET NUMBER" = PDF page - 1).
Values are in ft-in exactly as printed; `PL t x w` = thickness x width in inches. "p95" = PDF page 95.

Reading method: the PDF fraction glyphs are custom-encoded, so plate sizes were decoded from the text layer
(consistent glyph map checked against zoomed raster images) and every dimension fraction was confirmed on a 250-300 dpi raster.
Items I could not fully confirm are marked UNCERTAIN.

## 0. How the elevation sheets are organised (all units)

- One sheet shows "Girder A" and "Girders B, C & D" (Unit 4: a single "Girder Elevation" valid for A-D).
- Dimensions are horizontal, no allowance for grade (note on each sheet).
- Plate changes happen at the **field splices** (FS) and at the **shop splices** that bracket the thick pier plate. Per pier:
  `[outer plate from FS] | [thicker plate] | [THICKEST plate, +-X ft of the pier] | [thicker plate] | [outer plate to FS]`
  where X is printed under the thick plate as two dimensions ("22'-6 | 22'-6" = X ahead/behind, web label repeated there).
  Web plate is constant in each unit (no web changes shown).
- The FS locations in the plate row are the splices: FS-to-pier distances for A are on the elevation (50'-0 / 65'-0),
  for B-D they say "VARIES" and are in the GIRDER DATA tables of the framing plan (section 5 below).
- The long dimensions under "TOP FLANGE"/"BOTTOM FLANGE" (e.g. `112'-3 15/16 COMPRESSION`) are stress zones (tension/
  compression lengths) along the girder, NOT plate lengths. They are listed for completeness.
- No plate-grade / Charpy / fracture-critical notes appear on these elevation sheets (nothing printed). Flange transition detail
  and "Detail A" are referenced to design sheet 91 (PDF 93 / 92-93 region), not these sheets.
- Shear stud spacing is printed on each sheet (see unit tables).

### Unit 1 format reference (PDF 92, Girder A, top flange only, read at 110 dpi - fractions UNCERTAIN, matches existing data)
`A top 7/8x18 (90'-0 comp) | 1"x20 | 1 7/8x20 (1G +-19'-0) | 1"x20 | 7/8x18 | 1 1/8x22 | 2x22 (2G 19'-0 behind / 17'-0 ahead) | 1 1/8x22 | 7/8x18 | 1 1/8x24 | 2x24 ...`
FS 1A..5A at 50'-0 from piers (A); B-D 50'-0 etc. Web 9/16x82. Unit 1 was already recorded; not re-verified here.

---

## 1. Unit 2 (Piers 5G BRG - 9G BRG, spans 6G-9G) - PDF 95 (design sheet 93, title-block sheet 94)

Web: **PL 9/16 x 82** throughout (p95). Piers 5G and 9G are bearing piers (abutment-type ends, "Detail A").
Field splices FS9..FS14; shop splices flank the pier plates.

### 1a. Girder A (p95) - from BRG pier 5G to BRG pier 9G

Top flange (shear studs: 1'-0 from end, 30 spa @ 14 = 35'-0 then 53 EQ spa; 2'-6 stud-free at each FS):

| Location | Plate | Extent (as printed) |
|---|---|---|
| begin 5G to FS9A | PL 1 1/8 x 18 | to FS9A (FS9A = 50'-0 before Pier 6G) |
| FS9A to 6G | PL 1 1/2 x 30 | ends 17'-0 before CL 6G |
| 6G | PL 2 1/8 x 30 | 17'-0 each side of CL Pier 6G |
| 6G to FS10A | PL 1 3/8 x 30 | FS10A = 50'-0 after 6G |
| FS10A to FS11A | PL 7/8 x 18 | |
| FS11A to 7G | PL 1 1/4 x 28 | ends 17'-0 before CL 7G |
| 7G | PL 2 x 28 | 17'-0 behind / 18'-0 ahead of CL 7G |
| 7G to FS12A | PL 1 3/8 x 28 | FS12A = 50'-0 after 7G |
| FS12A to FS13A | PL 7/8 x 18 | |
| FS13A to 8G | PL 1 1/4 x 26 | FS13A = 50'-0 before 8G; ends 17'-0 before 8G |
| 8G | PL 2 x 26 | 17'-0 / 17'-0 |
| 8G to FS14A | PL 1 3/8 x 26 | FS14A = 50'-0 after 8G |
| FS14A to 9G BRG | PL 7/8 x 18 | studs: 41 EQ spa then 26 spa @ 15 = 32'-6, 1'-0 to end |

Bottom flange:

| Location | Plate |
|---|---|
| begin to FS9A | PL 1 1/2 x 24 |
| FS9A..6G (17'-0 before) | PL 1 1/2 x 30 |
| 6G +-17'-0 | PL 2 1/8 x 30 |
| to FS10A | PL 1 1/2 x 30 |
| FS10A to FS11A | PL 1 1/4 x 24 |
| FS11A..7G | PL 1 3/8 x 28 |
| 7G (17'-0 / 18'-0) | PL 2 x 28 |
| 7G..FS12A | PL 1 3/8 x 28 |
| FS12A to FS13A | PL 1"x 24 |
| FS13A..8G | PL 1 3/8 x 26 |
| 8G +-17'-0 | PL 2 x 26 |
| 8G..FS14A | PL 1 3/8 x 26 |
| FS14A to 9G | PL 1 1/4 x 24 |

Stress zones, top: 112'-3 15/16 C | 62'-0 T | 76'-0 T | 80'-0 1/8 C | 58'-0 T | 72'-0 T | 74'-11 1/2 C | 56'-0 T | 58'-0 T | 101'-3 1/8 C.
Bottom: 133'-3 15/16 T | 41'-0 C | 46'-0 C | 138'-0 1/8 T | 30'-0 C | 45'-0 C | 112'-11 1/2 T | 45'-0 C | 38'-0 C | 121'-3 1/8 T.
UNCERTAIN: "112'-3 15/16" and "133'-3 15/16" (the text layer does not distinguish 15/16 from 1/2; read from raster).

Field-splice-to-pier: FS9A/10A/11A/12A/13A/14A all printed "50'-0" (p95).

### 1b. Girders B, C, D (p95)

Top:  PL 3/4 x 18 | FS9: PL 1 1/8 x 26 | 6G +-17'-0: PL 1 7/8 x 26 | PL 1 1/8 x 26 | PL 3/4 x 18 |
PL 1 x 24 | 7G (17'-6 behind / 17'-0 ahead): PL 1 7/8 x 24 | PL 1 1/8 x 24 | PL 3/4 x 18 |
PL 1 x 22 | 8G +-17'-0: PL 1 7/8 x 22 | PL 1 1/8 x 22 | PL 3/4 x 18 (to 9G BRG).
Bottom: PL 1 x 20 | PL 1 1/4 x 26 | 6G: PL 1 7/8 x 26 | PL 1 1/4 x 26 | PL 7/8 x 20 | PL 1 1/8 x 24 | 7G: PL 1 7/8 x 24 | PL 1 1/4 x 24 | PL 7/8 x 20 |
PL 1 1/8 x 22 | 8G: PL 1 7/8 x 22 | PL 1 1/8 x 22 | PL 7/8 x 20.
(Note: the 6G centre plate extents are 17'-0/17'-0, 7G 17'-6 behind and 17'-0 ahead (web label), 8G 17'-0/17'-0.)
Stress zones B-D top: VARIES C | 61'-0 T | 57'-0 T | VARIES C | 57'-0 T | 70'-0 T | VARIES C | 56'-0 T | 57'-0 T | VARIES C.
Bottom: VARIES T | 40'-0 C | 44'-0 C | VARIES T | 30'-0 C | 43'-0 C | VARIES T | 28'-0 C | 37'-0 C | VARIES T.
Studs: 15 spa @ 14 = 17'-6, 61 EQ, 60 EQ, 62 EQ, 60 EQ, 56 EQ, 66 EQ, 56 EQ, 13 spa @ 15 = 16'-3.
FS locations per girder: section 5.

---

## 2. Unit 3 (Piers 9G BRG - 14G BRG, spans 10G-14G) - PDF 98 (Piers 9G-12G) and PDF 99 (Piers 12G-14G)

Web: **PL 5/8 x 92** throughout (p98, p99). FS15..FS22.

### 2a. Girder A (p98/p99)

Top:
| Location | Plate | Extent |
|---|---|---|
| 9G BRG to FS15A | PL 1 x 20 | FS15A = 65'-0 before 10G |
| FS15A to 10G | PL 1 1/2 x 34 | ends 22'-6 before CL 10G |
| 10G | PL 2 1/4 x 34 | 22'-6 / 22'-6 |
| to FS16A | PL 1 1/2 x 34 | FS16A = 65'-0 after 10G |
| FS16A to FS17A | PL 1 1/2 x 24 | |
| FS17A to 11G | PL 1 1/2 x 34 | FS17A = 65'-0 before 11G |
| 11G | PL 2 x 34 | 22'-6 / 22'-6 |
| to FS18A | PL 1 1/2 x 34 | FS18A = 65'-0 after 11G |
| FS18A to FS19A | PL 7/8 x 18 | |
| FS19A to 12G | PL 1 1/4 x 28 | FS19A = 50'-0 before 12G |
| 12G | PL 2 x 28 | 18'-0 behind / 18'-0 ahead (p98, p99) |
| to FS20A | PL 1 1/4 x 28 | FS20A = 50'-0 after 12G |
| FS20A to FS21A | PL 7/8 x 20 | |
| FS21A to 13G | PL 1 1/4 x 30 | FS21A = 50'-0 before 13G |
| 13G | PL 2 x 30 | 18'-0 behind / 22'-6 ahead |
| to FS22A | PL 1 1/4 x 30 | FS22A = 50'-0 after 13G |
| FS22A to 14G BRG | PL 7/8 x 20 | |

Bottom: PL 1 5/8 x 24 | 10G: PL 1 5/8 x 34 | PL 2 1/4 x 34 (22'-6/22'-6) | PL 1 5/8 x 34 | PL 1 7/8 x 34 |
11G: PL 1 5/8 x 34 | PL 2 x 34 | PL 1 1/2 x 34 | PL 1 3/8 x 22 |
12G: PL 1 3/8 x 28 | PL 2 x 28 | PL 1 3/8 x 28 | PL 1 3/8 x 22 |
13G: PL 1 3/8 x 30 | PL 2 x 30 | PL 1 3/8 x 30 | PL 1 3/8 x 22 (to 14G BRG).
Stress zones top: 126'-5 3/8 C | 73'-0 T | 71'-0 T | 127'-2 1/2 C | 58'-0 T | 78'-0 T | 65'-0 1/2 C | 78'-0 T (p98) | 61'-0 T | 105'-0 13/16 C | 60'-0 T | 63'-0 T | 116'-4 1/4 C (p99).
Bottom: 151'-5 3/8 T | 48'-0 C | 57'-0 C | 162'-2 1/2 T | 37'-0 C | 53'-0 C | 121'-0 1/2 T | 47'-0 C (p98) | 33'-0 C | 146'-0 13/16 T | 47'-0 C | 42'-0 C | 137'-4 1/4 T (p99).
Studs: 1'-0; 15 spa @ 17 = 21'-3 at begin; 67/68/67/72/61/58/58/70/55/64 EQ; 15 spa @ 17 = 21'-3 at 14G end.

### 2b. Girders B, C, D (p98/p99)

Top: PL 7/8 x 20 | 10G: PL 1 3/8 x 30 | PL 2 x 30 | PL 1 3/8 x 30 (19'-6 / 19'-6) | PL 7/8 x 20 |
11G: PL 1 1/4 x 26 | PL 2 x 26 | PL 1 1/4 x 26 (19'-6/19'-6) | PL 7/8 x 18 |
12G: PL 1 1/8 x 24 | PL 1 7/8 x 24 | PL 1 1/8 x 24 (19'-6 ahead / 19'-6 behind) | PL 7/8 x 18 |
13G: PL 1 1/8 x 24 | PL 2 x 24 | PL 1 1/8 x 24 (19'-6/19'-6) | PL 7/8 x 18 (to 14G).
Bottom: PL 1 1/8 x 22 | 10G: PL 1 1/2 x 30 | PL 2 x 30 | PL 1 1/2 x 30 | PL 1 1/8 x 26 |
11G: PL 1 3/8 x 26 | PL 2 x 26 | PL 1 3/8 x 26 | PL 1 x 18 |
12G: PL 1 1/8 x 24 | PL 1 7/8 x 24 | PL 1 1/8 x 24 | PL 1 x 20 |
13G: PL 1 1/4 x 24 | PL 2 x 24 | PL 1 1/4 x 24 | PL 1 x 20.
Stress zones top: VARIES C | 73'-0 T | 71'-0 T | VARIES C | 70'-0 T | 78'-0 T | VARIES C | 60'-0 T | 60'-0 T (p99) | VARIES C | 60'-0 T | 63'-0 T | VARIES C.
Bottom: VARIES T | 47'-0 C | 55'-0 C | VARIES T | 36'-0 C | 52'-0 C | VARIES T | 31'-0 C | 32'-0 C | VARIES T | 29'-0 C(?) ...  -- see note below.
(UNCERTAIN: B-D bottom zone dimensions on p99: text gives 32'-0, VARIES, 32'-0, 41'-0 -- read "32'-0 C | VARIES T | 32'-0 C | 41'-0 C | VARIES T"; verify on the sheet.)
p98 B-D FS-to-pier dimensions all VARIES except as in section 5. Studs: 58 / 64 / 63 / 64 / 56 / 50 / 67 / 58 / 56 EQ spa; 14 spa @ 17 = 19'-10 at ends.

---

## 3. Unit 4 (Piers 14G BRG - 19G BRG, spans 15G-19G) - PDF 102 (design sheet 100; one drawing for ALL girders A-D)

Web: **PL 5/8 x 92** throughout. Field splices FS23..FS30 (one numbering for all four girders, no A/B suffix). Studs: 1'-0, 25 spa @ 8 = 16'-8 at 14G end;
74/70/90/116/91/126/126/66/74/75 EQ spa; 26 spa @ 8 = 17'-4 at 19G end.

Top flange: 
PL 7/8 x 18 (14G BRG to FS23) | PL 1 x 24 | **PL 1 3/4 x 24** (15G, 18'-0 / 18'-0) | PL 1 x 24 (to FS24) | PL 7/8 x 18 |
PL 1 3/8 x 32 | **PL 2 x 32** (16G, 19'-0 behind / 18'-0 ahead) | PL 1 3/8 x 32 | PL 1 x 20 |
PL 1 3/8 x 30 | **PL 2 x 30** (17G, 18'-0 behind / 19'-0 ahead) | PL 1 3/8 x 30 | PL 7/8 x 18 |
PL 1 x 24 | **PL 1 3/8 x 24** (18G, 19'-0 / 19'-0) | PL 1 x 24 | PL 7/8 x 18 (to 19G BRG).
Bottom flange:
PL 1 1/8 x 22 | PL 1 1/8 x 24 | **PL 1 3/4 x 24** (15G) | PL 1 x 24 | PL 7/8 x 18 |
PL 1 1/2 x 32 | **PL 2 x 32** (16G) | PL 1 1/2 x 32 | PL 1 1/4 x 24 |
PL 1 1/2 x 30 | **PL 2 1/8 x 30** (17G) | PL 1 1/2 x 30 | PL 7/8 x 18 |
PL 1 x 24 | **PL 1 3/8 x 24** (18G) | PL 1 x 24 | PL 1 x 22 (to 19G BRG).
(Plate change points are the FS (FS-to-pier per girder: section 5) and the pier thick-plate extents above.)
Stress zones top (first row, 14G-17G): VARIES C | 58'-0 T | 68'-0 T | VARIES C | 83'-0 T | 71'-0 T | VARIES C | 61'-0 T ... ; second row (17G-19G): 91'-0 T | VARIES C | 82'-0 T | 47'-0 T | VARIES C.
FS25 to Pier 16G on the elevation: 68'-6 (A..D identical in the table).
Bottom: VARIES T | 24'-0 C | 32'-0 C | VARIES T | 46'-0 C | 37'-0 C | VARIES T | 36'-0 C | (row 2) 44'-0 C | VARIES T | 29'-0 C | 25'-0 C | VARIES T.
UNCERTAIN: the T/C label assignment for the first and last "VARIES" zones was inferred from the pattern; the numbers themselves are as printed.

---

## 4. Unit 4 span / pier data (secondary request)

### 4a. Pier stations and skews (staking diagram PDF 17, framing PDF 100/101)
| Pier | Station on CL Ramp G | Angle printed | Source |
|---|---|---|---|
| 15G | 67494+84.70 | 90 deg (framing "90 deg 00'00""; pier radial) | PDF 17, 100 |
| 16G | 67496+82.70 | 56 deg 32'38" | PDF 17, PDF 100 (framing), staking |
| 17G | 67499+40.70 | 49 deg 00'00" | PDF 17, PDF 101 |
| 18G | 67501+45.70 | "90 deg to local tangent" (radial) | PDF 17, 101 |
| 19G | 67502+97.70 | 32 deg 55'29" (end bearing) | PDF 17, 101 |
Along CL Ramp G: 167'-0 (14G-15G), 198'-0, 258'-0, 205'-0, 152'-0. P.T. STA 67494+89.66 (R 950'-0 -> 1550'-0, spiral-free, P.C. STA 67497+34.48).
The printed 56 deg 32'38" is the angle between the pier line and the alignment tangent, i.e. a skew of 33 deg 27'22" from radial (and 41 deg 00'00" from radial for 17G, 57 deg 04'31" for 19G).

### 4b. Dimensions pinning Pier 16G on girder A (PDF 100, 101)
Span 16G along A: 4'-11 13/16 (CL pier 15G to P.T.) + 45'-0 3/16 (P.T. to FS24) = 50'-0 to FS24 (matches the 50'-0 on elev. PDF 102) +
72'-8 9/16 (FS24-FS25) + 68'-6 (FS25 to CL pier 16G) = **191'-2 9/16** (GIRDER DATA span length, p101). The framing plan (PDF 100)
also shows the cross-frame chain along A: 21'-4 9/16 + 8 spa @ 21'-2 3/4 = 169'-10 (= 191'-2 9/16 from CL pier 15G).
Beyond 16G on A: 58'-7 1/16 to P.C., 3'-5 1/8 to FS26, 127'-2 1/2 to FS27, 69'-9 5/8 to pier 17G = 259'-0 5/16.
CL span 16G = 198'-0 (staking and framing). Horizontal dimensions, no allowance for grade.
Sanity check: CL 198'-0 x (R+14.5)/R (~ +3.0) minus A-offset x tan(33 deg 27'22") (~ -9.6) gives ~191.4 ft, consistent with 191'-2 9/16.
The model value 193.48 ft is ~2.3 ft too long; a skew of about 27 deg from radial (instead of 33.46 deg) would give that. Check the pier-16G skew
sign/angle (plan: 56 deg 32'38" to tangent) and the 14.5 ft offset of girder A.

### 4c. Girder data tables, Unit 4 (PDF 101, "GIRDER DATA"; horizontal, all girders)
Span 15G (CL BRG 14G to CL Pier 15G):
| Girder | BRG 14G to FS23 | FS23 to 15G | Span length |
|---|---|---|---|
| A | 116'-3 1/2 | 50'-0 | 166'-3 1/2 |
| B | 115'-1 3/16 | 49'-5 15/16 | 164'-7 1/8 |
| C | 113'-10 13/16 | 48'-11 7/8 | 162'-10 11/16 |
| D | 112'-8 9/16 | 48'-5 3/4 | 161'-2 5/16 |

Span 16G:
| Girder | 15G to P.T. | P.T. to FS24 | FS24 to FS25 | FS25 to 16G | Span length |
|---|---|---|---|---|---|
| A | 4'-11 13/16 | 45'-0 3/16 | 72'-8 9/16 | 68'-6 | 191'-2 9/16 |
| B | 4'-11 3/16 | 45'-0 3/16 | 87'-4 1/8 | 68'-6 | 205'-9 1/2 |
| C | 4'-10 9/16 | 45'-0 3/16 | 101'-11 11/16 | 68'-6 | 220'-4 7/16 |
| D | 4'-10 | 45'-0 3/16 | 116'-7 3/16 | 68'-6 | 234'-11 3/8 |

Span 17G:
| Girder | 16G to P.C. | P.C. to FS26 | FS26 to FS27 | FS27 to 17G | Span length |
|---|---|---|---|---|---|
| A | 58'-7 1/16 | 3'-5 1/8 | 127'-2 1/2 | 69'-9 5/8 | 259'-0 5/16 |
| B | 43'-11 9/16 | 17'-10 7/8 | 125'-9 | 69'-2 1/8 | 256'-9 9/16 |
| C | 29'-4 | 32'-2 5/16 | 124'-4 5/8 | 68'-6 7/8 | 254'-5 13/16 |
| D | 14'-8 7/16 | 46'-3 9/16 | 123'-1 | 68'-0 | 252'-1 |

Span 18G:
| Girder | 17G to FS28 | FS28 to FS29 | FS29 to 18G | Span length |
|---|---|---|---|---|
| A | 63'-4 5/8 | 97'-1 9/16 | 49'-0 15/16 | 209'-7 1/8 |
| B | 62'-10 13/16 | 87'-5 5/8 | 49'-4 5/8 | 199'-9 1/16 |
| C | 62'-5 5/16 | 77'-10 7/16 | 49'-8 5/16 | 190'-0 1/16 |
| D | 62'-0 | 68'-4 1/16 | 50'-0 | 180'-4 1/16 |

Span 19G:
| Girder | 18G to FS30 | FS30 to BRG 19G | Span length |
|---|---|---|---|
| A | 49'-0 15/16 | 97'-6 7/8 | 146'-7 13/16 |
| B | 49'-4 5/8 | 104'-5 3/4 | 153'-10 3/8 |
| C | 49'-8 5/16 | 111'-4 7/16 | 161'-0 3/4 |
| D | 50'-0 | 118'-2 7/8 | 168'-2 7/8 |

Girder radius data (p101): BRG 14G to P.T.: A 954'-6, B 944'-10, C 935'-2, D 925'-6; P.C. to BRG 19G: A 1545'-6, B 1555'-2, C 1564'-10, D 1574'-6.
Other Unit 4 framing dims: bearing-line offsets at 14G: 1'-6 (at pier), 4'-6 A to ... (girder spacing 3 spa @ 9'-8 = 29'-0 measured on the skewed line, 5'-2 edge).
Cross-frame spacings along A: span 15G 7 spa @ 20'-9 = 145'-3 (+21'-0 1/2 at 14G); along D 7 spa @ 20'-1 7/16 = 140'-10 1/16.
2'-0 jacking stiffener each side of pier line (typ.) at 15G, 17G, 18G; at 16G 3'-3 each side.

---

## 5. Field splices and span lengths, Units 2 and 3 (GIRDER DATA tables, framing plans PDF 94 and PDF 97; B-D "VARIES" on the elevations)

### Unit 2 (PDF 94)
Span 6G: BRG 5G to FS9 / FS9 to CL 6G / span: A 124'-3 15/16 / 50'-0 / 174'-3 15/16; B 123'-0 5/8 / 49'-5 15/16 / 172'-6 9/16; C 121'-9 5/16 / 48'-11 7/8 / 170'-9 3/16; D 120'-6 1/16 / 48'-5 3/4 / 168'-11 13/16.
Span 7G: 6G to FS10 / FS10 to FS11 / FS11 to 7G / span: A 50'-0 / 114'-0 1/8 / 50'-0 / 214'-0 1/8; B 49'-5 15/16 / 112'-10 1/4 / 49'-5 15/16 / 211'-10 1/8; C 48'-11 7/8 / 111'-8 5/16 / 48'-11 7/8 / 209'-8 1/16; D 48'-5 3/4 / 110'-6 9/16 / 48'-5 3/4 / 207'-6 1/16.
Span 8G: 7G to FS12 / FS12 to FS13 / FS13 to 8G / span: A 50'-0 / 102'-11 1/2 / 50'-0 / 202'-11 1/2; B 49'-5 15/16 / 101'-10 15/16 / 49'-5 15/16 / 200'-10 13/16; C 48'-11 7/8 / 100'-10 3/8 / 48'-11 7/8 / 198'-10 1/8; D 48'-5 3/4 / 99'-10 / 48'-5 3/4 / 196'-9 1/2.
Span 9G: 8G to FS14 / FS14 to BRG 9G / span: A 50'-0 / 109'-3 1/8 / 159'-3 1/8; B 49'-5 15/16 / 108'-1 5/8 / 157'-7 9/16; C 48'-11 7/8 / 107'-0 1/8 / 156'-0; D 48'-5 3/4 / 105'-10 3/4 / 154'-4 1/2.
Girder radius (BRG 5G to BRG 9G): A 954'-6, B 944'-10, C 935'-2, D 925'-6.

### Unit 3 (PDF 97)
Span 10G: BRG 9G to FS15 / FS15 to 10G / span: A 134'-5 3/8 / 65'-0 / 199'-5 3/8; B 133'-0 13/16 / 64'-4 1/8 / 197'-4 15/16; C 131'-8 5/16 / 63'-8 3/16 / 195'-4 1/2; D 130'-3 13/16 / 63'-0 5/16 / 193'-4 1/8.
Span 11G: 10G to FS16 / FS16 to FS17 / FS17 to 11G / span: A 65'-0 / 126'-2 1/2 / 65'-0 / 256'-2 1/2; B 64'-4 1/8 / 124'-11 1/8 / 64'-4 1/8 / 253'-7 3/8; C 63'-8 3/16 / 123'-7 7/8 / 63'-8 3/16 / 251'-0 1/4; D 63'-0 5/16 / 122'-4 7/16 / 63'-0 5/16 / 248'-5 1/16.
Span 12G: 11G to FS18 / FS18 to FS19 / FS19 to 12G / span: A 65'-0 / 106'-0 1/2 / 50'-0 / 221'-0 1/2; B 64'-4 1/8 / 104'-11 9/16 / 49'-5 15/16 / 218'-9 5/8; C 63'-8 3/16 / 103'-10 11/16 / 48'-11 7/8 / 216'-6 3/4; D 63'-0 5/16 / 102'-9 7/8 / 48'-5 3/4 / 214'-3 15/16.
Span 13G: 12G to FS20 / FS20 to FS21 / FS21 to 13G / span: A 50'-0 / 126'-0 13/16 / 50'-0 / 226'-0 13/16; B 49'-5 15/16 / 124'-9 7/16 / 49'-5 15/16 / 223'-9 5/16; C 48'-11 7/8 / 123'-6 1/16 / 48'-11 7/8 / 221'-5 13/16; D 48'-5 3/4 / 122'-2 7/8 / 48'-5 3/4 / 219'-2 3/8.
Span 14G: 13G to FS22 / FS22 to BRG 14G / span: A 50'-0 / 129'-4 1/4 / 179'-4 1/4; B 49'-5 15/16 / 128'-0 5/16 / 177'-6 1/4; C 48'-11 7/8 / 126'-8 3/8 / 175'-8 1/4; D 48'-5 3/4 / 125'-4 9/16 / 173'-10 5/16.
Girder radius (BRG 9G to BRG 14G): A 954'-6, B 944'-10, C 935'-2, D 925'-6.
Cross-check: the elevation FS dims for Unit 3 A (65'-0 at 10G/11G, 50'-0 at 12G/13G) agree with these tables; the B-D elevation "VARIES" = these values.

## 6. UNCERTAIN / to verify
1. Unit 2 A top/bottom "112'-3 15/16" and "133'-3 15/16": fraction taken from raster only.
2. Unit 3 B-D bottom stress zones near 13G/14G and Unit 4 first/last T/C labels (section 2b, 3) - numbers as read, labels partially inferred.
3. Unit 1 sample line above read from a 110 dpi raster; recorded Unit 1 data is the authority.
4. Unit 4 pier 15G/18G skew "90 deg" is the angle to the local tangent (radial pier), not a skew angle value.
5. Unit 3 B-D bottom plate at 11G: first plate printed "1 1/8 x 26" (span 11G mid), then "1 3/8 x 26 | 2x26 | 1 3/8x26" at the pier - consistent between text and raster.
