# Location data - Delaware River Bridge (PTC / NJTA, 1954)

Source: `C:\SBMData\Original Existing Bridge Plans from PTC.pdf` (raster, 108 pages; "p." = PDF page, sheet numbers in INDEX.md). Read 2026-10-19 from renders at 105-260 dpi.
Everything below is read from the plans unless marked DERIVED (arithmetic on plan values) or WEB / OPEN.

## 1. Stationing system
- Stationing is "according to Pennsylvania Turnpike Commission" (general note 2, p.5, p.16, p.32), increasing from Pennsylvania (west) to New Jersey (east). Feet; one continuous baseline, no station equation seen.
- Contract limits (title sheets): PN 2 Sta 644+60 to 674+00 (p.4); PN 1 675+00 to 691+00 (p.15); PN 3 691+00 to 713+30 (p.31); PN 4 (steel) 645+53.713 to 712+27.547, 6,673.834 ft (p.41).
- Bridge CL = alignment CL: "CL of Roadway S 69-54-15 E (from Penna. Turnpike)" (p.42); same bearing on p.5, p.16, p.32. The bridge is symmetric about the CL (p.107).
- Overall length 6572'-7" between abutment bearings, "measured horizontally" (p.1, p.42).

## 2. Alignment (plan)
- Plan view: ONE tangent, bearing S 69 deg 54' 15" E over the whole structure (p.5, p.16, p.32, p.42). DERIVED azimuth from north = 110 deg 05' 45" (reference meridian not stated: the north arrows are drawn ~20 deg off the sheet vertical, consistent with this bearing; the base is the Turnpike's own bearing system, probably grid / true, NOT magnetic - OPEN).
- No horizontal curves, PI, curve table, skew angles or superelevation on any sheet: all supports are normal to the CL (square piers, p.5, p.32). Expansion pier pairs have two bearing lines 4'-0 apart (piers 13/14, 17/20 side etc., p.1).
- Roadway: 76'-6" curb to curb, 38'-3" CL to curb each side, 3 x 12'-0 lanes per side plus painted strips, CL joint (p.107). Trusses 87'-0 c-c (p.42 Section A, p.72). R/W: 100 ft each side at the PA end (p.5), 150 ft each side in NJ (p.32).

## 3. Profile (vertical)
- Constant +3.00 % up from the west, crest vertical curve, -3.00 % down to the east (p.1, p.16, p.42, p.69).
- Crest: PVC 675+98.380 El 143.390, PVI 682+80.380 El 163.850, PVT 689+62.380 El 143.390, L = 1364.000 ft, M = 10.230 ft (p.1, p.69) - checks (1364 x 0.06 / 8 = 10.23). "Gutter line profile" = profile grade (p.5, p.32, p.107 "Gutter Line = Grade Profile").
- West end vertical curve: VPI 644+67.140 El 49.454, L = 1060 ft, M.O. 4.637, PVT 649+97.140 El 65.354 (p.5, p.42); DERIVED entry grade -0.5 %.
- East end: gutter profile -3.00 % to the east abutment; abutment gutter El about +51.154 (p.32).

## 4. Datum and water levels
- "All elevations refer to U.S.C. & G.S. sea level datum 1929" (p.5, p.16, p.32, p.2 note 1) = NGVD 29.
- Tidal data (p.5, p.16): mean tidal heights at 11,800 sec-ft mean annual fresh water flow at Trenton: L.W. -2.1, H.W. +4.4 (MLW / MHW; p.16 also MSL 0.0); extreme L.W. -7.4 (Jan 25 1945), extreme H.W. +9.1 (Aug 24 1933), observations by the Corps of Engineers. M.H.W. El +4.4 (USGS) also on p.1.
- Proposed channel: 400 ft wide, bottom El -42.9, centered at the bridge center Sta 682+80.38 (p.16); existing channel 300 ft (p.16), CL existing ~50 ft west of the proposed CL (p.16; p.42 shows 150 ft / 250 ft dimensions about the channel CLs). Shore lines (el -2.1 MLW / +4.4 MHW) near Sta 676 and 689+90 (p.16).
- NGVD29 -> NAVD88 offset is NOT on the plans (OPEN, compute with NOAA VDatum / VERTCON; order of a foot, do not assume).

## 5. Stations of supports (all on the CL, square)
West abutment (PA): CL bearing 646+21.463 (p.1, p.5); paving notch 646+17.796 (p.1).
Pennsylvania piers (p.5, p.7 = DRB-2/4, p.10): 1 = 647+44.630; 2 = 648+69.046; 3 = 649+93.463; 4 = 651+17.880; 5 = 652+42.297; 6 = 653+66.713; 7 = 654+91.130; 8 = 656+15.547; 9 = 657+39.963; 10 = 658+61.651 / 658+66.380 (two bearing lines 4'-8 3/4" apart; centre expansion dam 658+64.560, p.1); 11 = 660+81.880; 12 = 662+97.380; 13 = 665+14.880 (2'-0 each side, 4'-0 c.c. bearings); Pier 16 = 673+25.005 (p.6, read); 14 and 15 not read on p.7 / p.6: DERIVED from 665+16.88 + n x 269'-4 1/2": 14 = 667+86.255, 15 = 670+55.630 (matches the 4 spans @ 269'-4 1/2" = 1077'-6", p.1).
River piers (p.16, p.17, p.1): 17 = 675+96.38 (footing 675+96.13); 18 = 679+39.38; 19 = 686+21.38; 20 = 689+64.38 (footing 689+64.63). Main span panel points 22 + 11 + 11 @ 31'-0; mid-span (center of bridge, PVI) 682+80.38.
New Jersey piers (p.32, p.3): 21 = 692+35.755; 22 = 695+05.130; 23 = 697+74.505; 24 = 700+45.880 (+/- 2'-0); 25 = 702+63.380; 26 = 704+78.880; 27 = 706+94.380 / 706+99.109 (4'-8 3/4" apart; centre expansion dam 706+96.192); 28 = 708+20.797; 29 = 709+45.213; 30 = 710+69.630; East abutment: CL bearing 711+94.046 (p.1) - the site / boring plans print 711+92.797 (p.32, p.3: the abutment wall / CL abutment, 1.25 ft difference, OPEN which is bearing vs wall); paving notch 712+27.047 (p.1); R/W end 713+30.0.
Span lengths: 123'-2" (W abut to P1), 8 x 124'-5", 121'-5", 3 x 215'-6", 4 x 269'-4 1/2", 341' + 682' + 341' arch truss (spans 18-20 between piers 17..20 with 2'-0 / 4'-0 offsets), 4 x 269'-4 1/2", 3 x 215'-6", 121'-5", 3 x 124'-5", 123'-2" (p.1).

## 6. Crossings (all with their stations / plan features)
- Pennsylvania Canal and culvert at Pier 1 / W abutment (p.5, p.14); Route 13 (US 13) at about Sta 646+80 to 647+40 (p.5); Pennsylvania Railroad (multi-track) at about Sta 657+90 to 658+60, near Pier 10 (p.5, p.42); railroad spur (Frank Ford Supply Co. spur) to be relocated; Cherry Street / Wood Street / Cedar Avenue / Radcliffe Street (west side streets, p.5; Radcliffe Road to Pier 17, p.16, p.2); Delaware River, 400 ft channel, bridge center 682+80.38; "River Road" / relocated River Road at Pier 21 (Sta 692+35, p.32, p.3); Florence-Burlington township line at about Sta 719-720 (p.42).
- Min. vertical clearance above top of rail, Penna. R.R.: 43.0 (p.42).

## 7. State line
- NO state-line station is printed. The general plan (p.42) labels PENNSYLVANIA / BUCKS COUNTY and NEW JERSEY / BURLINGTON COUNTY between two dashed vertical lines in the river (the lines are about 1.4" apart at 1" = 300 ft, i.e. about 400 ft = the proposed channel, so they most likely mark the channel / jurisdiction limits, NOT the state line) - OPEN. The state line is at the river thalweg / mid river, about the bridge center 682+80 (WEB / OPEN: confirm with the PA/NJ boundary, e.g. NJGIN municipal / state boundary layer). Contract PN 1 / PN 3 split at Sta 691+00 is not the state line.

## 8. Survey control, ties, coordinates on the plans
- NO coordinates, state plane references, benchmarks, northings / eastings or lat / long anywhere in the 108 pages (searched all title sheets and site plans visually).
- Ties given (p.42, plan top): R/W monuments "with tack set in lead": at the CL 6" monuments, 36" deep, and at 100 ft (PA) / 150 ft (NJ) either side 4" monuments, 24" deep, flush with ground, at Stations 651+84.89, 656+84.59, 658+88.44, 671+89.44 (PA side, 100 ft R/W) and 694+21.89, 710+38.82 (NJ side, 150 ft R/W) (p.42). These are the only physical control ties; they are ON the CL and R/W lines of the bridge baseline.
- North arrows: p.5, p.16, p.32, p.42 (meridian type not stated).

## 9. Web coordinate and georeference plan
- WEB (Wikipedia, Delaware River-Turnpike Toll Bridge): about 40 deg 07' 01" N, 74 deg 49' 50" W (about 40.1169, -74.8306), which is near mid-bridge. Only good to about +/-100 m; not a control point.
- Location grid candidates (the bridge is in New Jersey SPCS / UTM 18N territory at 74.83 W; the plans do not choose): NAD83(2011) / New Jersey (ftUS) EPSG:3424 (central meridian 74.5 W, convergence about -0.2 deg here: grid azimuth about 110.1 +/- 0.2 deg), or NAD83 / Pennsylvania South EPSG:2272 (the PA Turnpike's side), or UTM 18N (EPSG:26918 / 6347). The model's georeference record takes CRS + map unit + grid scale; decide with the user which to use (state of the owner PTC = PA South is plausible, NJ State Plane the choice for a NJ site).
- What is still needed to georeference (two unknowns: a point and an azimuth; the plans give station along the line and a bearing in an unknown meridian):
  1. ONE point with known station and map coordinates (best: the face of a pier or the abutment bearing line on aerial / LiDAR, Pier 17 / 20 are on land, large: 14' x 121' footings and the 58 ft-between-shaft piers; 6572.6 ft length gives a scale check).
  2. The azimuth: measure the line through two piers / the deck CL on the same imagery and compare with 110 deg 05' 45" (+/- convergence), then fix the rotation.
  3. Vertical: NGVD29 -> NAVD88 correction.
- What I would use (nothing downloaded): current NJ (NJGIN / NJ OIT 2020 imagery and 2019-20 LiDAR) and PA (PASDA / PAMAP orthos and LiDAR) products; USGS 3DEP DEM / point cloud (also for the terrain); NGS datasheets + NJ / PA CORS / NGS control near Bristol / Florence for tie checks; the PA Turnpike / NJ Turnpike Authority as-built or R/W monuments (stations listed in Section 8: ask the owner for their coordinates, they are likely in the Turnpike's state-plane control); the Delaware River Joint Toll Bridge Commission / DRPA boundary files for the state line; USACE channel surveys for the thalweg. Cross-check with an I-95 / I-276 current as-built CL if the user can supply one (the structure may have been rehabilitated or widened since 1954, so the deck outline may differ: use the PIERS, not the deck edges).
- Plan checks available for the fit: pier stations (Section 5) at 0.001 ft; total length 6572'-7"; crest VC; spans 18-20 = 1364'-0 centered at 682+80.38.

## 10. Open questions
- Meridian of S 69-54-15 E; coordinates of the PTC baseline / monuments (ask the Turnpike).
- E abutment CL: 711+94.046 (p.1) vs 711+92.797 (p.32 / p.3).
- State line station.
- Whether the bridge was modified after 1954 (as-built revisions on the sheets run to 1957; later work unknown) - affects the aerial fit.
