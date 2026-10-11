# Camber, blocking and field haunch data (I-80 WB Design 116, Ramp G Design 316)

Source: `dist\SteelBridgeModeler\B00168 - CBIS-ESI-Westbound I-80 - Design 116.pdf` (PDF pp 132-157) and
`B00169 - CBIS-ESI-Ramp G - Design 316.pdf` (PDF pp 111-128). Numbers were taken from the PDF text layer by cell
coordinates (not OCR); decimals only. Everything the plans PRINT is recorded as printed; anything derived is
labelled OBSERVED RELATION (checked numerically against the printed data; NOT stated on the plans). Gaps are
marked **UNRESOLVED** with the PDF page. Machine-readable tables: `docs\plan-data\camber\*.csv` (list at the end).

**Correction 2026-10-11 (user decision 4):** in the Ramp G camber, steel and slab deflection CSVs the point just BEFORE each unnumbered
"MIDPOINT OF FIELD SECTION" column held the midpoint value (U1 pt 25; U2 7, 51, 72; U3 35, 83, 106; U4 55; 78 cells, PDF 111-125). Fixed from the text
layer (PDF 116 U2 pt 7 raster-confirmed 3.05 / 2.87 / 2.71 / 2.55 slab, 1.30 / 1.17 / 1.03 / 0.89 steel; PDF 115 camber 4.04 / 3.81 / 3.59 / 3.37). I-80 CSVs
compared cell by cell with the text layer: no shift. `camber-ramp-g.csx` re-run on the sample model.

PDF fractions are custom glyphs and the text layer is wrong for them (one glyph code stands for several
fractions), so every fraction below was read from raster clips (2.2x). Decimal table values are not affected.

## 0. Sheet map (PDF page : content)
- I-80: Unit 1 camber + blocking 132-133 (design sheets 128-129), slab+barrier deflection 134, steel deflection 135,
  haunch elevations 136, misc data table 137-138. Unit 2: camber + blocking 139-140, slab defl 141-142, steel defl
  143-144, haunch elev 145-146, misc 147-150. Unit 3: camber + blocking 151-152, slab defl 153, steel defl 154,
  haunch elev 155, misc 156-157.
- Ramp G: Unit 1 camber+blocking 111, deflections 112, haunch elev 113, misc 114. Unit 2: 115, 116, 117, 118.
  Unit 3: 119, 120 (slab), 121 (steel), 122, 123. Unit 4: 124, 125 (slab and steel), 126, 127-128.
- Units/girders: I-80 WB 3 units (U1 girders A-F, U2 A-K, U3 A-J); Ramp G 4 units (girders A-D). All 7 units present.
  Girder lines that stop inside a unit ("discontinuous": I-80 U2 B-E, U3 B-C) are covered in section 7.

## 1. General notes (transcribed; I-80 Unit 1 camber sheet PDF 132; Ramp G PDF 111 has the same text except as noted)
1. FOR BLOCKING DATA TABLE, SEE DESIGN SHEET 129.
2. FOR UNIT 1 DEFLECTION ORDINATES, SEE DESIGN SHEETS 130 AND 131.
   (Other unit sheets: I-80 U3 PDF 151 "FOR CAMBER NOTES, SEE DESIGN SHEET 128 ... BLOCKING DATA TABLE, SEE DESIGN SHEET 148 ...
   UNIT 3 DEFLECTION ORDINATES, SEE DESIGN SHEETS 149 AND 150"; Ramp G U1 "SEE DESIGN SHEET 110"; U2 PDF 115 "FOR CAMBER NOTES, SEE DESIGN SHEET 109 ... UNIT 2 DEFLECTION ORDINATES, SEE DESIGN SHEET 114".)
3. CAMBER ORDINATES ARE MEASURED FROM A CHORD LINE BETWEEN FIELD SPLICES. UPWARD CAMBERS ARE POSITIVE.
4. DEFLECTION ORDINATES FOR CAMBER INCLUDE DEFLECTIONS DUE TO ALL DEAD LOADS EXCEPT FUTURE WEARING SURFACE. DOWNWARD DEFLECTIONS ARE POSITIVE.
5. TOP OF GIRDER ELEVATIONS FOR HAUNCH CALCULATIONS SHALL BE SURVEYED PRIOR TO THE PLACEMENT OF FORMS.
6. HAUNCH THICKENING DIAGRAM NOT PROVIDED BECAUSE THE HAUNCH DIMENSION FROM BOTTOM OF SLAB TO TOP OF GIRDER WEB SHOULD THEORETICALLY BE A CONSTANT DIMENSION. (SEE TYPICAL SLAB AND NOMINAL HAUNCH DETAIL, DESIGN SHEET 92). GIRDER WEB SHALL BE CUT TO COMPENSATE FOR DEAD LOAD DEFLECTION AND VERTICAL CURVE CORRECTION.  (Ramp G: "...TYPICAL SLAB AND HAUNCH DETAIL, DESIGN SHEET 83".)
7. CAMBER VALUES MUST BE MAINTAINED AT THE CENTER LINE OF ABUTMENT AND PIER BEARINGS.
8. CAMBER VALUES ARE GIVEN FOR THE GIRDERS IN THE NO LOAD POSITION.
9. FOR INDIVIDUAL GIRDER SPAN LENGTHS AND DISTANCE TO FIELD SPLICES, SEE GIRDER FRAMING AND GIRDER ELEVATION SHEETS.
10. FOR LOCATION OF POINTS, SEE TOP OF SLAB DIAGRAMS ON TOP OF SLAB ELEVATION SHEETS.
11. (I-80) THE DEFLECTIONS SHOWN IN THE DEFLECTION DIAGRAMS AND MISCELLANEOUS DATA TABLES IN THESE PLANS WERE COMPUTED ASSUMING THAT THE INSPECTION WALKWAYS WOULD BE INSTALLED PRIOR TO CONCRETE DECK SLAB. IF AN ALTERNATE SEQUENCE IS CHOSEN WHEN ERECTING THE BRIDGE, THE CONTRACTOR SHALL PROVIDE CALCULATIONS FOR REVISED CAMBER AND BLOCKING DATA, DEFLECTION DATA AND GIRDER LINE HAUNCH DATA WITH THE SHOP DRAWINGS.
    Ramp G PDF 111: only the tail "...DEFLECTION DATA AND GIRDER LINE HAUNCH DATA WITH THE SHOP DRAWINGS." was found beside the notes;
    the beginning of that note was not located: **UNRESOLVED** (PDF 111).

Printed table components (complete list; nothing else is tabulated): (a) CAMBER ORDINATES (inches), (b) DEFLECTION ORDINATES DUE TO
WEIGHT OF STRUCTURAL STEEL (inches, down +), (c) DEFLECTION ORDINATES DUE TO WEIGHT OF SLAB AND BARRIERS (inches, down +),
(d) BLOCKING DATA (feet), (e) TABLE OF GIRDER LINE HAUNCH ELEVATIONS (feet), (f) MISCELLANEOUS DATA TABLE (inches/fractions).
There is NO printed column for superimposed dead load, vertical curve / profile, or "total camber" as a sum; FWS is excluded (note 4);
barrier is lumped with slab. "Vertical curve correction" is mentioned only in note 6 (web cut) and is not a separate table.

## 2. Diagram and the printed meaning of the points and of BLOCKING
"CAMBER & BLOCKING DIAGRAM (UNIT n)" (PDF 132/139/151; Ramp G 111/115/119/124), in words:
- A cambered curve labelled "TOP OF WEB BEFORE DEFLECTION" is drawn over a straight "CHORD LINE (TYP.)" joining the points at the
  field splices (F.S.). The vertical gap between curve and chord is labelled "CAMBER ORDINATES".
- Under it a straight "HORIZONTAL LINE" carries circled point numbers (1, 7, 19, 25, ...). Vertical dimension lines run from the
  horizontal line UP to the top-of-web curve at those points and carry letter labels "A", "B", "C"... ("(KEEP)" is printed beside A, D, G,
  J, M, P on I-80 U1 = the bearing points; B-labelled leaders are at the splices). These lettered vertical dimensions are the BLOCKING DATA.
- Table "BLOCKING DATA (UNIT n) (FEET)": one value per lettered point per girder line. Columns are headed ABUT. BRG., F.S. NO. n, PIER NO. nWB, ...
  (so blocking is given per girder line over the whole UNIT, at bearings and at every field splice - not per shop-assembled field section).

```
        top of web before deflection (cambered, no load)
            .--.                  .--.          chord line = straight line between field-splice points
     A   .-'    '-.   C        .-'    '-.        camber ordinate = vertical gap curve/chord (+ = curve above chord)
     |  /  F.S.1   \  |  pier  /          \
     | |            | | (KEEP)|            |    blocking letter = vertical dimension, HORIZONTAL LINE up to the curve
  ---+-+------------+-+-------+------------+--- HORIZONTAL LINE (per girder line); circled point numbers underneath
     1 7            19 25                       units: blocking in FEET, camber in INCHES
```
Not stated in words anywhere on the sheets: the elevation of the horizontal line (see R2), whether the dimension is to top of web or flange
beyond the curve label "TOP OF WEB", and whether the profile/cross slope are "included". OBSERVED (R2 below): they are included, because
blocking = girder line haunch elevation (which follows the finished grade, cross slope and slab) + steel deflection, minus a per-girder constant.

Point numbering: every unit has points 1..N (I-80 U1 143, U2 149, U3 107; Ramp G U1 90, U2 78, U3 113, U4 109). Between key points the diagram
prints the segment LENGTH, "n SPA." and "@ spacing", "MEASURED ALONG" one girder line: I-80 U1 girder F, I-80 U2 CL girder K, I-80 U3 CL girder J,
Ramp G all units CL girder A. Points are equally spaced inside each segment; spacing is printed rounded with (+)/(-). Distances start at point 1
= CL bearing at the unit start (the first diagram dimension line). The key points: CL bearings (abutment / pier), field splices F.S. n, and
midpoints of field sections (labelled "MIDPOINT OF FIELD SECTION" over the camber table). Camber is 0.00 at every F.S. point (chord ends)
and non-zero at piers/abutments (note 7). How the same point number falls on the other girders is defined on the TOP OF SLAB ELEVATION sheets
(note 10), which are not part of this extraction: **UNRESOLVED** (which line each point lies on for girders other than the measuring girder;
radial/skew/normal).

Segments as printed (read from clips; I-80 U1 PDF 132, U2 PDF 139, U3 PDF 151, Ramp G PDF 111/115/119/124):
- I80 unit 1 (along girder F): 50'-0 (6 spa @ 8'-4); 103'-9 1/2 (12 spa @ 8'-7 13/16(-)); 55'-0 (6 spa @ 9'-2); 80'-6 (10 spa @ 8'-0 5/8(-)); 126'-8 1/2 (14 spa @ 9'-0 5/8(-)); 69'-0 (8 spa @ 8'-7 1/2); 64'-0 (8 spa @ 8'-0); 137'-0 (14 spa @ 9'-9 7/16(-)); 64'-0 (8 spa @ 8'-0); 70'-0 (8 spa @ 8'-9); 66'-7 3/4 (12 spa @ 5'-6 5/8(+)); 70'-0 (8 spa @ 8'-9); 64'-0 (8 spa @ 8'-0); 126'-3 (14 spa @ 9'-0 3/16(+)); 45'-0 (6 spa @ 7'-6)
- I80 unit 2 (along girder K): 45'-0 (6 spa @ 7'-6); 113'-6 (12 spa @ 9'-5 1/2); 50'-0 (6 spa @ 8'-4); 70'-0 (8 spa @ 8'-9); 122'-0 (14 spa @ 8'-8 9/16(+)); 68'-0 (8 spa @ 8'-6); 67'-0 (8 spa @ 8'-4 1/2); 134'-6 1/4 (14 spa @ 9'-7 5/16(-)); 64'-9 (8 spa @ 8'-1 1/8); 61'-0 (8 spa @ 7'-7 1/2); 97'-10 1/8 (12 spa @ 8'-1 7/8(-)); 40'-9 (6 spa @ 6'-9 1/2); 39'-0 (6 spa @ 6'-6); 83'-11 9/16 (10 spa @ 8'-4 3/4(+)); 43'-0 (6 spa @ 7'-2); 33'-0 (4 spa @ 8'-3); 105'-8 11/16 (12 spa @ 8'-9 3/4(-))
- I80 unit 3 (along girder J): 108'-3 11/16 (12 spa @ 9'-0 5/16(-)); 50'-0 (6 spa @ 8'-4); 52'-0 (6 spa @ 8'-8); 81'-11 5/16 (10 spa @ 8'-2 3/8(+)); 61'-0 (8 spa @ 7'-7 1/2); 46'-0 (8 spa @ 5'-9); 97'-0 3/16 (10 spa @ 9'-8 7/16(-)); 52'-0 (6 spa @ 8'-8); 48'-0 (6 spa @ 8'-0); 95'-0 11/16 (10 spa @ 9'-6 1/16(+)); 52'-0 (6 spa @ 8'-8); 50'-0 (6 spa @ 8'-4); 110'-0 1/16 (12 spa @ 9'-2(+))
- RG unit 1 (along girder A): 95'-0 (10 spa @ 9'-6); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 90'-0 (9 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 90'-5 3/4 (10 spa @ 9'-0 5/8(-)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 90'-10 13/16 (10 spa @ 9'-1 1/16(+)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 99'-2 1/2 (10 spa @ 9'-11 1/16(-))
- RG unit 2 (along girder A): 124'-3 15/16 (13 spa @ 9'-6 3/4(+)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 114'-0 1/8 (12 spa @ 9'-6(+)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 102'-11 1/2 (11 spa @ 9'-4 5/16(+)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 109'-3 1/8 (11 spa @ 9'-11 3/16(+))
- RG unit 3 (along girder A): 134'-5 3/8 (14 spa @ 9'-7 1/4(-)); 65'-0 (7 spa @ 9'-3 7/16(-)); 65'-0 (7 spa @ 9'-3 7/16(-)); 126'-2 1/2 (13 spa @ 9'-8 1/2); 65'-0 (7 spa @ 9'-3 7/16(-)); 65'-0 (7 spa @ 9'-3 7/16(-)); 106'-0 1/2 (11 spa @ 9'-7 11/16(-)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 126'-0 13/16 (13 spa @ 9'-8 3/8(-)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 129'-4 1/4 (13 spa @ 9'-11 3/8(+))
- RG unit 4 (along girder A): 116'-3 1/2 (12 spa @ 9'-8 5/16(-)); 50'-0 (5 spa @ 10'-0); 50'-0 (5 spa @ 10'-0); 72'-8 9/16 (12 spa @ 6'-0 11/16(+)); 68'-6 (7 spa @ 9'-9 7/16(-)); 62'-0 3/8 (7 spa @ 8'-10 5/16); 127'-2 1/2 (13 spa @ 9'-9 7/16(-)); 69'-9 5/8 (8 spa @ 8'-8 11/16(+)); 63'-4 5/8 (7 spa @ 9'-0 11/16(-)); 97'-1 9/16 (10 spa @ 9'-8 9/16(-)); 49'-0 15/16 (5 spa @ 9'-9 13/16(-)); 49'-0 15/16 (5 spa @ 9'-9 13/16(-)); 97'-6 7/8 (12 spa @ 8'-1 9/16(+))

Key points (blocking letter = point number; circled numbers under each diagram):
- I80 unit 1: A=1, B=7, C=19, D=25, E=35, F=49, G=57, H=65, I=79, J=87, K=95, L=107, M=115, N=123, O=137, P=143
- I80 unit 2: A=1, B=7, C=19, D=25, E=33, F=47, G=55, H=63, I=77, J=85, K=93, L=105, M=111, N=117, O=127, P=133, Q=137, R=149
- I80 unit 3: A=1, B=13, C=19, D=25, E=35, F=43, G=51, H=61, I=67, J=73, K=83, L=89, M=95, N=107
- RG unit 1: A=1, B=11, C=16, D=21, E=30, F=35, G=40, H=50, I=55, J=60, K=70, L=75, M=80, N=90
- RG unit 2: A=1, B=14, C=19, D=24, E=36, F=41, G=46, H=57, I=62, J=67, K=78
- RG unit 3: A=1, B=15, C=22, D=29, E=42, F=49, G=56, H=67, I=72, J=77, K=90, L=95, M=100, N=113
- RG unit 4: A=1, B=13, C=18, D=23, E=35, F=42, G=49, H=62, I=70, J=77, K=87, L=92, M=97, N=109

## 3. Observed relations between the printed tables (NOT printed on the plans; use for validation only)
R1 camber(i) = D(i) - chord_D(i), D = steel deflection + slab/barrier deflection (both down +, inches), chord_D = straight line of D between the
adjacent F.S. points (camber 0.00) weighted by distance along the girder. Checked I-80 Unit 1, girder A and F, all field sections: error <= 0.02 in
(rounding) except girder A sections 123-137 and 137-143 where it reaches 0.14 / 0.24 in (the distances along girder A differ from the F-girder
point spacing; UNRESOLVED which spacing the plan used there). Not yet checked for other units/girders (their point spacing along each girder is unresolved, see above).
Consequence: the printed camber contains NO profile/vertical-curve term; it is deflection relative to the splice chord.
R2 blocking(letter) = H(point) - H(datum point) + steel_deflection(point)/12   [feet; H = girder line haunch elevation (ft); steel deflection in inches]
where datum point = the girder-line end whose blocking is printed 0.00. Checked every girder, every lettered point of all 7 units
(I-80 U1 A-F, U2 A-K, U3 A,D-J, Ramp G U1-U4 A-D; discontinuous girder tails excepted): max error 0.012 ft (blocking is printed to 0.01 ft). At bearings
the steel deflection is 0 so blocking = H - H_datum exactly; at splices it differs by steel deflection/12.
R3 blocking(i) at a non-splice lettered point = blocking chord between the neighbouring splice letters (by distance) + camber(i)/12. Checked I-80 Unit 1
all six girders: max 0.008 ft.
Reading of R1-R3: haunch elevation H = elevation of the girder top at slab pour (steel load deflection already in, slab deflection still to come) =
final profile-based position + slab/barrier deflection; blocking (no-load web top) = H + steel deflection relative to the datum. This reading is a hypothesis, not a plan statement.
Datum observation: zero blocking is at point 1 for ascending girders and at the LAST point for descending ones (I-80 U2 F,G,H = point 149; I-80 U3 = point 107; Ramp G U3 = 113, U4 = 109).

## 4. FIELD HAUNCH DATA - exact definition (notes under PDF 136, 113; quoted in section 1)
- TABLE OF GIRDER LINE HAUNCH ELEVATIONS (UNIT n): elevations in feet (e.g. 1006.76) for every girder line at every point 1..N, same point numbers as camber
  (I-80 PDF 136; 145-146; 155; Ramp G 113, 117, 122, 126).
- Field haunch = "GIRDER LINE HAUNCH ELEVATION" minus the SURVEYED elevation of the top of the top flange (after steel erection, before forms), at the points and field splices.
  The haunch elevation "includes adjustments for slab thickness and anticipated deflections". Haunch dimension bottom of slab to top of web is
  constant in theory (note 6); webs are cut for deflection and vertical curve. Allowable field haunch min/max are in the MISCELLANEOUS DATA TABLE.
- FIELD HAUNCH DETAIL (drawing under PDF 136, 113): top-of-slab line with slab thickness "8" at the girder, slab underside stepping down to a haunch
  that sits on the top flange; a dot on the slab-underside level at one flange edge with the dimension "CROSS SLOPE ADJUSTMENT" from that dot level down to the
  flange edge level, and "FIELD HAUNCH (SEE NOTE 1)" from the top flange up to the haunch (slab underside) level at girder line. Which exact point the dot marks
  (girder CL vs flange edge) is not stated in words: **UNRESOLVED** (PDF 136 / 113). Ramp G detail shows the same slab thickness "8".
- MISCELLANEOUS DATA TABLE (UNIT n) rows (PDF 137-138, 147-150, 156-157; Ramp G 114, 118, 123, 127-128): "ANTICIPATED DEFLECTION DUE TO SLAB AND BARRIER (INCHES)" per girder
  line and point; "CROSS SLOPE ADJUSTMENTS (INCHES)" by girder group (A, B, C(R) & D(L) / C(L) / E-F, D(R)); "ALLOWABLE FIELD HAUNCH (INCHES)" MIN./MAX. for girder groups A-C and D-F.
  Transcribed sample I-80 U1 PDF 137 points 1-15 (read from clips): slab deflection row A = 0, 5/8, 1 3/16, 1 3/4, 2 1/4, 2 11/16, 3 1/16, 3 3/8, 3 9/16, 3 11/16, 3 11/16, 3 5/8, 3 1/2, 3 1/4, 2 15/16 (equal to the decimal slab-deflection
  table rounded to 1/16 in: OBSERVED); cross slope adjustments 3/16 (A, B, C(R) & D(L)), 1/16 (C(L)), 1/4 (E-F, D(R)) at points 1-15; allowable field haunch A-C min 11/16 max 3 1/2, D-F min 3/4 max 3 1/2 at points 1-15
  (later points show a smaller maximum, 2 1/2, in the text layer; not confirmed on clips). **UNRESOLVED / NOT TRANSCRIBED**: the rest of the misc data tables (all other points, units, Ramp G): they are fraction-valued, the text layer is
  unreliable for fractions, and they were not read cell by cell. The decimal haunch-elevation tables ARE complete.

## 5. Completeness
- Complete numeric tables (camber, steel defl, slab defl, haunch elevation: every point of every continuous girder; blocking: every letter): I-80 U1, U2, U3, Ramp G U1-U4. See CSV list.
- Girders/points: I-80 U2 girders B, C, D, E and I-80 U3 girders B, C have numbered point columns only up to where the printed point table stops (B 1-110, C 1-84, D 1-54, E 1-24 on U2;
  B 1-18, C 1-42 on U3) and then a separate small table "...OF DISCONTINUOUS GIRDERS" whose point numbers are not printed ("VARIES, FOR LOCATIONS OF POINTS SEE TOP OF SLAB ELEVATIONS ON DESIGN SHEET 198"
  = PDF 205 by the PDF = sheet + 7 offset; not read). Counts of the unnumbered values: U2 B 7, C 9, D 9, E 11; U3 B 9, C 9. Implied point numbers from the blocking letters: U2 B 111-117, C 85-93, D 55-63 (consistent);
  The tails are NOT on the common point numbering: the TOP OF SLAB ELEVATIONS sheet (design 198 = PDF 205; "TOP OF SLAB ELEVATIONS (FOR DISCONTINUOUS GIRDER E)" etc.) lays them out
  separately "MEASURED ALONG GIRDER x" with their own spacing: E 10 SPA @ 8'-8 (fraction glyph, UNREAD), D 8 SPA, C 8 SPA @ 7'-4 (glyph UNREAD), B 6 SPA @ 8'-4 (glyph UNREAD) - i.e. n spaces = the printed number of tail values minus one (E 11, D 9, C 9, B 7 values), which matches.
  So tail values are one per space point of that sheet, in printed order, start at the girder's own start point and end at its own end ("*"). Absolute distance/point mapping to the main numbering: **UNRESOLVED** (PDF 205 and Unit 3 equivalent sheet, not transcribed). The tails are in the `*-discontinuous-unnumbered.csv` files in printed order.
  A star ('*') in a table = "LOCATION AT END OF DISCONTINUOUS GIRDER".
- Blocking table I-80 U2 has 18 letters (A-R) for girders A,F-K, fewer for the discontinuous girders (B 14, C 11, D 8, E 5), I-80 U3 B 4, C 7.
- I-80 U2 camber sheet also prints points 148-149 for girders A, F-K in a separate small table (merged into the main CSV).
- The camber "total" is not printed as a sum; components are only steel and slab+barrier deflections (see section 1).
- Missing: none of the sheets listed in the brief; the index gives no extra camber sheet.

## 6. Worked example: I-80 Unit 1 girder line A, all points, all components (decimals exactly as printed)
Columns: point; blocking letter; distance along girder F from point 1 (computed from the printed segment lengths, ft); camber ordinate (in, up +);
deflection slab+barrier (in, down +); deflection steel (in, down +); girder line haunch elevation (ft); blocking (ft).
```csv
point,letter,dist_ft_along_F,camber_in,defl_slab_in,defl_steel_in,haunch_elev_ft,blocking_ft
1,A,0.000,0.00,0.00,0.00,1006.76,0.00
2,,8.333,0.15,0.62,0.25,1006.92,
3,,16.667,0.26,1.21,0.50,1007.07,
4,,25.000,0.32,1.77,0.73,1007.23,
5,,33.333,0.31,2.27,0.94,1007.37,
6,,41.667,0.20,2.71,1.12,1007.52,
7,B,50.000,0.00,3.08,1.27,1007.65,1.00
8,,58.649,0.60,3.37,1.39,1007.79,
9,,67.299,1.08,3.57,1.48,1007.91,
10,,75.948,1.42,3.68,1.52,1008.03,
11,,84.597,1.64,3.70,1.53,1008.14,
12,,93.246,1.73,3.64,1.50,1008.24,
13,,101.896,1.70,3.49,1.43,1008.33,
14,,110.545,1.57,3.26,1.33,1008.42,
15,,119.194,1.34,2.97,1.21,1008.50,
16,,127.844,1.05,2.63,1.06,1008.58,
17,,136.493,0.71,2.25,0.90,1008.66,
18,,145.142,0.35,1.87,0.74,1008.73,
19,C,153.792,0.00,1.48,0.58,1008.81,2.10
20,,162.958,-0.70,1.09,0.42,1008.89,
21,,172.125,-1.36,0.74,0.28,1008.98,
22,,181.292,-1.93,0.44,0.16,1009.07,
23,,190.458,-2.38,0.22,0.07,1009.17,
24,,199.625,-2.74,0.06,0.01,1009.28,
25,D,208.792,-2.97,0.00,0.00,1009.39,2.63
26,,216.842,-3.00,0.06,0.04,1009.50,
27,,224.892,-2.94,0.19,0.11,1009.61,
28,,232.942,-2.79,0.37,0.21,1009.73,
29,,240.992,-2.55,0.61,0.34,1009.85,
30,,249.042,-2.23,0.91,0.49,1009.98,
31,,257.092,-1.85,1.26,0.65,1010.11,
32,,265.142,-1.42,1.64,0.84,1010.25,
33,,273.192,-0.96,2.04,1.03,1010.38,
34,,281.242,-0.48,2.45,1.23,1010.52,
35,E,289.292,0.00,2.87,1.42,1010.66,4.02
36,,298.342,0.72,3.34,1.64,1010.81,
37,,307.393,1.38,3.77,1.84,1010.97,
38,,316.443,1.96,4.15,2.01,1011.12,
39,,325.494,2.43,4.45,2.15,1011.26,
40,,334.545,2.78,4.67,2.25,1011.40,
41,,343.595,2.99,4.79,2.31,1011.53,
42,,352.646,3.05,4.82,2.32,1011.65,
43,,361.696,2.97,4.74,2.28,1011.76,
44,,370.747,2.74,4.56,2.20,1011.86,
45,,379.798,2.38,4.29,2.08,1011.96,
46,,388.848,1.90,3.94,1.92,1012.05,
47,,397.899,1.33,3.52,1.73,1012.13,
48,,406.949,0.68,3.06,1.52,1012.21,
49,F,416.000,0.00,2.57,1.29,1012.29,5.64
50,,424.625,-0.56,2.12,1.08,1012.36,
51,,433.250,-1.11,1.68,0.87,1012.43,
52,,441.875,-1.62,1.26,0.67,1012.51,
53,,450.500,-2.07,0.89,0.48,1012.59,
54,,459.125,-2.44,0.58,0.32,1012.67,
55,,467.750,-2.73,0.32,0.19,1012.76,
56,,476.375,-2.93,0.12,0.08,1012.86,
57,G,485.000,-3.03,0.00,0.00,1012.96,6.20
58,,493.000,-2.93,0.01,-0.01,1013.06,
59,,501.000,-2.76,0.07,0.00,1013.17,
60,,509.000,-2.49,0.20,0.04,1013.28,
61,,517.000,-2.14,0.39,0.11,1013.40,
62,,525.000,-1.69,0.65,0.21,1013.52,
63,,533.000,-1.17,0.94,0.33,1013.65,
64,,541.000,-0.60,1.28,0.47,1013.78,
65,H,549.000,0.00,1.64,0.61,1013.91,7.20
66,,558.786,0.56,2.10,0.80,1014.07,
67,,568.571,1.14,2.58,1.00,1014.24,
68,,578.357,1.69,3.03,1.19,1014.40,
69,,588.143,2.17,3.44,1.36,1014.56,
70,,597.929,2.57,3.78,1.51,1014.71,
71,,607.714,2.84,4.04,1.62,1014.86,
72,,617.500,2.98,4.20,1.69,1015.00,
73,,627.286,2.97,4.25,1.72,1015.13,
74,,637.071,2.80,4.19,1.71,1015.25,
75,,646.857,2.48,4.03,1.64,1015.36,
76,,656.643,2.02,3.77,1.54,1015.46,
77,,666.429,1.44,3.41,1.40,1015.56,
78,,676.214,0.75,2.99,1.23,1015.65,
79,I,686.000,0.00,2.53,1.04,1015.73,9.06
80,,694.000,-0.42,2.13,0.88,1015.80,
81,,702.000,-0.84,1.73,0.72,1015.87,
82,,710.000,-1.24,1.35,0.56,1015.94,
83,,718.000,-1.60,0.99,0.41,1016.02,
84,,726.000,-1.91,0.67,0.28,1016.09,
85,,734.000,-2.15,0.40,0.16,1016.17,
86,,742.000,-2.34,0.17,0.07,1016.25,
87,J,750.000,-2.43,0.00,0.00,1016.34,9.58
88,,758.750,-2.39,-0.08,-0.03,1016.45,
89,,767.500,-2.26,-0.11,-0.03,1016.56,
90,,776.250,-2.05,-0.07,-0.01,1016.67,
91,,785.000,-1.75,0.02,0.04,1016.79,
92,,793.750,-1.37,0.17,0.11,1016.91,
93,,802.500,-0.95,0.36,0.20,1017.04,
94,,811.250,-0.49,0.57,0.29,1017.17,
95,K,820.000,0.00,0.80,0.40,1017.30,10.58
96,,825.554,0.40,1.08,0.52,1017.46,
97,,831.108,0.79,1.35,0.63,1017.61,
98,,836.662,1.13,1.58,0.73,1017.76,
99,,842.215,1.39,1.77,0.80,1017.90,
100,,847.769,1.56,1.89,0.85,1018.04,
101,,853.323,1.62,1.94,0.86,1018.18,
102,,858.877,1.57,1.90,0.84,1018.30,
103,,864.431,1.41,1.79,0.79,1018.42,
104,,869.984,1.14,1.60,0.71,1018.54,
105,,875.538,0.81,1.37,0.60,1018.65,
106,,881.092,0.41,1.09,0.48,1018.75,
107,L,886.646,0.00,0.81,0.35,1018.86,12.13
108,,895.396,-0.60,0.56,0.24,1018.95,
109,,904.146,-1.18,0.34,0.14,1019.04,
110,,912.896,-1.71,0.14,0.05,1019.14,
111,,921.646,-2.19,-0.02,-0.02,1019.24,
112,,930.396,-2.59,-0.12,-0.06,1019.34,
113,,939.146,-2.88,-0.15,-0.08,1019.45,
114,,947.896,-3.08,-0.12,-0.06,1019.56,
115,M,956.646,-3.15,0.00,0.00,1019.69,12.93
116,,964.646,-3.00,0.25,0.12,1019.81,
117,,972.646,-2.77,0.56,0.27,1019.94,
118,,980.646,-2.45,0.93,0.45,1020.07,
119,,988.646,-2.05,1.35,0.65,1020.21,
120,,996.646,-1.59,1.82,0.88,1020.35,
121,,1004.646,-1.08,2.32,1.11,1020.49,
122,,1012.646,-0.54,2.83,1.36,1020.64,
123,N,1020.646,0.00,3.36,1.60,1020.78,14.16
124,,1029.664,0.78,3.93,1.88,1020.95,
125,,1038.681,1.51,4.48,2.13,1021.11,
126,,1047.699,2.18,4.98,2.37,1021.26,
127,,1056.717,2.74,5.41,2.57,1021.42,
128,,1065.735,3.18,5.76,2.73,1021.56,
129,,1074.753,3.49,6.02,2.85,1021.70,
130,,1083.771,3.65,6.18,2.92,1021.82,
131,,1092.789,3.65,6.22,2.94,1021.94,
132,,1101.806,3.48,6.16,2.91,1022.05,
133,,1110.824,3.14,5.98,2.82,1022.15,
134,,1119.842,2.63,5.68,2.68,1022.24,
135,,1128.860,1.96,5.27,2.48,1022.33,
136,,1137.878,1.09,4.75,2.24,1022.39,
137,O,1146.896,0.00,4.14,1.95,1022.45,15.85
138,,1154.396,0.30,3.56,1.67,1022.49,
139,,1161.896,0.47,2.92,1.37,1022.52,
140,,1169.396,0.42,2.24,1.05,1022.54,
141,,1176.896,0.33,1.52,0.71,1022.55,
142,,1184.396,0.19,0.78,0.36,1022.56,
143,P,1191.896,0.00,0.00,0.00,1022.57,15.81
```
R2 check at the 16 blocking points for this girder (printed blocking vs H - H1 + steel/12; rounding of blocking is 0.01 ft):
```csv
letter,point,blocking_printed,H_pt-H_pt1 (ft),steel_defl_in/12 (ft),H-H1+steel/12,diff
A,1,0.00,0.00,0.0000,0.0000,+0.0000
B,7,1.00,0.89,0.1058,0.9958,+0.0042
C,19,2.10,2.05,0.0483,2.0983,+0.0017
D,25,2.63,2.63,0.0000,2.6300,+0.0000
E,35,4.02,3.90,0.1183,4.0183,+0.0017
F,49,5.64,5.53,0.1075,5.6375,+0.0025
G,57,6.20,6.20,0.0000,6.2000,-0.0000
H,65,7.20,7.15,0.0508,7.2008,-0.0008
I,79,9.06,8.97,0.0867,9.0567,+0.0033
J,87,9.58,9.58,0.0000,9.5800,-0.0000
K,95,10.58,10.54,0.0333,10.5733,+0.0067
L,107,12.13,12.10,0.0292,12.1292,+0.0008
M,115,12.93,12.93,0.0000,12.9300,-0.0000
N,123,14.16,14.02,0.1333,14.1533,+0.0067
O,137,15.85,15.69,0.1625,15.8525,-0.0025
P,143,15.81,15.81,0.0000,15.8100,-0.0000
```
Camber at piers (points 25, 57, 87, 115): -2.97, -3.03, -2.43, -3.15 in; at splices 0.00; at point 143 0.00.

## 7. UNRESOLVED list
1. Positions of points on girders other than the measuring girder (top-of-slab elevation sheets, note 10).
2. Mapping of the discontinuous-girder tail tables (I-80 U2 B-E PDF 140/142/144/146, U3 B-C 152/153/154/155) onto the common point numbers (own spacing on PDF 205 / U3 sheet); I-80 U2 E: 24 numbered + 11 tail = 35 vs its blocking letter E at point 33.
3. Where the datum ("HORIZONTAL LINE") elevation is defined: only observed (zero at lowest end), never stated.
4. Which point the FIELD HAUNCH DETAIL dot marks (CL vs edge); and the full misc-data tables (fractions).
5. R1 residual on I-80 U1 girder A end sections (0.14-0.24 in).
6. Ramp G PDF 111 note 11 text; the beginning is not located.
7. Segment fractions are from raster clips and should be confirmed against the girder elevation sheets (e.g. I-80 U3 PDF 151 "81'-11 5/16", "95'-0 11/16", "110'-0 1/16"; Ramp G U4 PDF 124 "72'-8 9/16", "49'-0 15/16").

## 8. Files in `docs\plan-data\camber\`
`<BR>-unit<n>-points.csv` (point, blocking letter, distance along the measuring girder, header text read), `-camber_in.csv`, `-defl_steel_in.csv`, `-defl_slab_in.csv`, `-haunch_elev_ft.csv`
(rows = points, columns = girder lines), `-blocking_ft.csv` (rows = letters), `*-unnumbered.csv` (discontinuous tails, printed order). BR = I80 or RG.
Cells: blank = not printed ("-" on the sheet). Column headings from the table are repeated in `-points.csv` col 4 (UNCERTAIN where it includes a stray "SPAN NO. nWB").

## 9. Model implementation and what the comparison proved (feature/camber)
Input `GirderLayoutDefinition.Camber` (`GirderCamberDefinition`: DatumPoint, Keys, Points). Keys = the plans' CL bearings (framing support index) and field
splices (FS number) with their point numbers (top-of-slab CSV "B" rows); points between consecutive keys are equally spaced along EACH girder (this is the
mapping of point numbers onto girders other than the measuring girder: UNRESOLVED item 1 is resolved by the comparison below). Points carry camber (in, up +),
steel deflection and slab+barrier deflection (in, down +). Scripts: `samples/plans/camber-i80.csx`, `camber-ramp-g.csx` (re-runnable; after the girder, splice and
termination scripts), check `samples/plans/check-blocking.csx`, report `sbm blocking <model> [bridge] [--all]`.
Computed by `SBM.Core.Framing.CamberBlocking` into `BridgeFraming.Camber` (model geometry stays the FINAL shape):
- H (plan "girder line haunch elevation") = finished grade - slab thickness + slab/barrier deflection = final top of web + haunch + slab deflection.
- No-load top of web N = final top of web + steel + slab/barrier deflection. BLOCKING = N - N(datum point) (= R2: H - H(datum) + steel deflection difference).
- Datum = the girder end whose blocking the plans print as 0.00 (an input, `DatumPoint`; observed, not printed). Ends of discontinuous girders use the girder end.
Verified results (model vs plan, all 27 + 16 girders; 4,703 haunch-elevation points, 595 blocking values; residuals in ft, plan precision 0.01):
| unit | H mean | H mean abs | H max abs | blocking mean abs | blocking max abs |
|---|---|---|---|---|---|
| I-80 U1 | 0.000 | 0.003 | 0.009 | 0.003 | 0.010 |
| I-80 U2 | -0.004 | 0.005 | 0.019 | 0.004 | 0.018 |
| I-80 U3 | -0.004 | 0.004 | 0.012 | 0.003 | 0.011 |
| Ramp G U1 | +0.001 | 0.003 | 0.009 | 0.002 | 0.005 |
| Ramp G U2 | 0.000 | 0.003 | 0.012 | 0.002 | 0.007 |
| Ramp G U3 | -0.003 | 0.003 | 0.010 | 0.002 | 0.005 |
| Ramp G U4 | -0.003 | 0.004 | 0.010 | 0.003 | 0.008 |
Nothing exceeds 0.02 ft. 106 points exceed 0.01 ft, all marginal (0.01-0.019): 96 in I-80 U2 (girders A-K, mostly A-C and F-H over Piers 6WB-9WB, where the model is 0.01-0.02 ft low,
the same spans where the top-of-slab check shows small residuals), 7 on I-80 U3 girder A, 3 on Ramp G. Plan precision is met everywhere; no input change is proposed.
Findings that CORRECT section 3:
- R1 is not general. The plans' camber is measured from the chord between the splices to the cambered (no-load) top of web, i.e. camber = (N - chord of N), which contains the
  vertical-curve departure of the profile from its chord. On the nearly straight grades of I-80 U1/U3 this equals "total deflection - chord of deflection" (R1), on I-80 U2 and
  Ramp G it does not (R1 off by up to 2.0 in, mean +0.2..0.7 in); with the model's profile the relation holds: mean abs residual 0.004-0.011 in, max 0.18 in (Ramp G U1 girder D,
  point 25), 5-10 points per unit beyond 0.02 in (I-80 U2: 177, mostly the first segment of A-E from Pier 6A and the 6WB-9WB bays). This explains the "A end sections off 0.14-0.24 in" (item 5): it is the profile term.
- Item 2 (tails of the discontinuous girders) is resolved by the data: the tail tables are the points from the CL bearing of the termination pier to the girder end (`Terminations`
  distance), n values = n-1 equal spaces, numbered from the pier's point number; the star (*) value is that end point (I-80 U2 E: tail 25..35 against letter E = 33, U3 B: 19..27 against
  letter D = 25 - letters are labels, the spacing is the girder's own). First of the two deflection tables per girder (page order) = slab+barrier, second = steel (swapping them
  worsens the haunch residuals to 0.05-0.09 ft). All tail haunch elevations, camber relations and blocking (incl. the datum 0.00* of U3 B/C) agree with the model within plan precision.
- Datum (item 3): the printed 0.00 end of each girder's blocking row (first or last letter; U3 A, U2 F-H and others have it at the last point).
Remaining UNRESOLVED: items 4 (field haunch detail dot, misc data tables in fractions), 6, 7 (segment fractions are not used: the model's own splice/bearing distances locate the points).
