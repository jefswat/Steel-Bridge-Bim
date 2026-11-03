# Agent review 2026-11-02: main span gussets, lateral plates, L11 walkway (user request)

Model: `samples/plans/PTC Delaware River Bridge.sbm`, bridge "PTC Arch truss 18-20" (dump `dump.csx`, renders `render.py` -> `v/`, sheets `pg/`, clash `out/clash.csv`).

## S-GL22-R (p.90)
- Outline (1" outer gusset, 168 x 108 in.) and stack (1 1/8 centre plate outside the 1", gusset on the 2'-5 o.o. chord webs) agree with p.90 within the scaling.
- Differences: p.90 has TWO 1" plates per side with a 1/8 gap at the CL (`2-1" Gus. Pls. | 1/8 |`), the model one plate through the CL;
  the 1 1/8 centre plate's bottom edge is off-centre (u -15 .. +8 in.; p.90 symmetric about the CL, about 15 in. wide).

## Lateral connection plates (p.97 / p.98 / p.80): systematic
- 540 of 584 plates are the generic "2-1/2 Pls." envelope (median 7.3 SF), the p.97 default applied everywhere.
- N-BGL15-T: p.98 L15 = 2-3/4 Pls with 2-Ls 7 x 4 x 3/4 (bent) along the whole chord edge, plate about 6 ft along the chord with curved (radial) edges
  to the lateral; model = 1/2 in. diamond about 25 in. under the lateral end only, edge angle 7 x 4 x 1/2 over 25 in.
- L16-L21 (p.98 L16 / L17 details): one plate takes the strut AND the diagonal, edge angle 7 x 4 x 1/2 (bent) full length, 2'-10 min. - not built that way.
- S-BGL11-b-T: lateral LL-SL11-LM10 stops about 60 in. short of the chord and 35 in. from strut LS-L11; its plates are vertical 1 in. strips at the free end
  (rotated 2-13[ 50 section: its "top / bottom" faces are vertical) connected to nothing. p.80 Section B: laterals land on the strut / truss plates at L11.
- S-GU13-R exists in the main span (p.82) and both 4-span trusses (p.64); the main one's outline broadly follows p.82. If the lateral plate was meant: S-BGU13-T / -B.

## Walkway / ladders at strut L11 (p.104)
- Model: walkway on strut L11 sits ON the strut (grating on the top cover, channels 9 in. into the strut, posts through the connection plates; 9 clash rows),
  centred on the strut, about 7 m long.
- p.104 "Plan of walkway on bott. strut" / Sections C, D: walkway BESIDE the strut, 2'-0 wide from 11 13/16 (strut face, 1'-11 5/8 o.o.) to 2'-11 13/16 off CL strut,
  hung on 1-L 4 x 3 1/2 x 3/8, 11'-0 / 9'-0 panels to the CL bridge; ladder L11 N stiles run 26.96 -> 44.23 m (17.3 m) from the pier platform; p.104 ladder / cage and
  the cross walk ladder still to be traced.
