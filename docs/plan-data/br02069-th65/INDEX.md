# Br. 02069 - T.H. 65 S.B. over 117th Ave (MnDOT) - sheet index

Plan set: `Br02069_Certified Plans_20250926.pdf` (39 pages, 1224 x 792 pt = 17" x 11"), certified 9/26/25, HDR for MnDOT.
S.P. 0208-02069 (T.H. 65=005), RDWY S.P. 0208-169, City of Blaine, Anoka County, Sec. 8 T31N R23W.
One span, 99'-3" CL-CL bearing, seven 30MH prestressed concrete beams, 0 deg skew, integral-type concrete end
diaphragms, pile-supported abutments behind MSE walls (Ret. Walls 35/36). Companion Br. 02070 (T.H. 65 N.B.) is NOT in this set.

## Page -> sheet -> title
PDF page = design sheet number for all 39 pages (checked against the LIST OF SHEETS on sheet 1 and the
"SHEET NO. n OF 39" field). Title-block logic (MnDOT/HDR sheets): title in the TITLE box at x 700-940 pt,
y 730-770 pt (top-left origin); sheet number at x 1000-1040, y 760-775; bridge no. at x 1130-1210, y 745-762.
MnDOT standard sheets (16, 17, 24, 25, 27, 33) have a different block ("TITLE:" line text, FIG. number); their
titles were read from the text layer. Text cache + scripts: `temp/br02069/` (not committed).

| PDF p. | Sheet | Title | Family | Step |
|---|---|---|---|---|
| 1 | 1 | General Plan and Elevation (+ list of sheets, design data) | general / layout | 1, 2 |
| 2 | 2 | Transverse Section, Construction Notes & SOQ | typical section / quantities | 2, 3, 7 |
| 3 | 3 | Bridge Layout (working points, top of roadway to seat) | layout / staking | 1, 2, 7 |
| 4 | 4 | South Abutment Details 1 | abutments | 4 |
| 5 | 5 | South Abutment Details 2 | abutments | 4 |
| 6 | 6 | South Abutment Reinforcement 1 | abutments / rebar | 5 |
| 7 | 7 | South Abutment Reinforcement 2 | abutments / rebar | 5 |
| 8 | 8 | South Abutment Reinforcement 3 | abutments / rebar | 5 |
| 9 | 9 | North Abutment Details 1 | abutments | 4 |
| 10 | 10 | North Abutment Details 2 | abutments | 4 |
| 11 | 11 | Abutment Details | abutments | 4 |
| 12 | 12 | North Abutment Reinforcement 1 | abutments / rebar | 5 |
| 13 | 13 | North Abutment Reinforcement 2 | abutments / rebar | 5 |
| 14 | 14 | North Abutment Reinforcement 3 | abutments / rebar | 5 |
| 15 | 15 | Framing Plan | framing | 2, 3 |
| 16 | 16 | 30MH Prestressed Concrete Beam (Pretensioned) 30MH-101, Beam B1 (std. Fig. 5-397.501 mod.) | girder | 3 |
| 17 | 17 | Prestressed Concrete Beam Diaphragm Connection Details (std.) | girder / diaphragms | 3 |
| 18 | 18 | Superstructure Details 1 | deck | 3, 5 |
| 19 | 19 | Superstructure Details 2 | deck / rebar | 5 |
| 20 | 20 | Superstructure Details 3 | deck / rebar | 5 |
| 21 | 21 | Superstructure Details 4 (bar list) | deck / rebar | 5 |
| 22 | 22 | Barrier Layout | rail | 6 |
| 23 | 23 | Pipe Railing Details | rail | 6 |
| 24 | 24 | Concrete Barrier 36" (Type S) integral/semi-integral abutment w/ guardrail connection (std.) | rail | 6 |
| 25 | 25 | Miscellaneous Concrete Barrier and Parapet Details (std.) | rail | 6 |
| 26 | 26 | Underpass Lighting Details | misc (lighting) | 6 |
| 27 | 27 | Bridge Approach Panel Barrier/Parapet Cover Plate Details (std.) | rail / joints | 6 |
| 28 | 28 | Concrete Slope Paving under Bridge | misc | 6 |
| 29-32 | 29-32 | Bridge Details 1-4 (MnDOT B-details: bearings B310/B311, sole plate B303, ...) | bearings / std. details | 3, 6 |
| 33 | 33 | As-Built Bridge Data (std. Fig. 5-397.900, blank form) | admin | - |
| 34 | 34 | Bridge Survey (bench marks, typical section TH 65, plat) | geometry | 1 |
| 35 | 35 | Profiles (117th Ave / ramps under the bridge) | geometry (under roads) | 1 |
| 36 | 36 | Alignment Tabulation 1 (NB65, SB65) | geometry | 1 |
| 37 | 37 | Alignment Tabulation 2 (EB117, WB117A, WB117B) | geometry (under roads) | 1 |
| 38 | 38 | Inplace Topography and Utilities | site | - |
| 39 | 39 | Borings | site / foundations | 4 |

## Families
| Family | Pages | Count |
|---|---|---|
| General / typical section / quantities | 1, 2, 33 | 3 |
| Geometry & layout (alignment, profile, staking) | 3, 34-37 | 5 |
| Abutments (details + reinforcement) | 4-14 | 11 |
| Framing / prestressed beam / diaphragm connection | 15-17 | 3 |
| Deck (superstructure details + bar list) | 18-21 | 4 |
| Barrier / railing / cover plates | 22-25, 27 | 5 |
| Misc (lighting, slope paving) | 26, 28 | 2 |
| Standard bridge details (bearings, sole plate, ...) | 29-32 | 4 |
| Site (topography/utilities, borings) | 38, 39 | 2 |
| Total | | 39 |

No piers, no steel, no joints/expansion devices on the bridge (ends are concrete end diaphragms + approach panels).

## Text layer
Real text (pypdfium2) on every page. Stacked fractions are NOT glyphs: they are split into separate small text
items (numerator and denominator on their own lines, e.g. `16'-11` / `8"` / `1` for 16'-11 1/8"), so text locates
and a 300 dpi raster clip confirms (tag RASTER). The diameter/CL symbol (Ꞓ) is lost or shows as `�`.
