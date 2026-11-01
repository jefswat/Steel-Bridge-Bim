"""Erection PT bar pieces at the bent: positions (s from the bent CL, + up-station, ft) and length check vs the printed minimums.
Printed (VECTOR): bent halves 5'-0 each, top block faces 6" inside the bulkhead faces (p.300), D1 / U1 14'-0, Type A block 1'-6 at 2'-11
from the bulkhead (tip) face (p.300), Type C block 2'-0 at 2'-11 (p.299), stressing tail 3'-0 min, coupler protrusion 3 5/8 min to the
coupler centre, coupler 6 3/4 (coupler detail). SCALED: plate 2.25", nut 2.875" (coupler detail / elevations), DB diaphragm outer face 3'-6"."""
PL, NUT, TAIL, PROT, CPL = 2.25, 2.875, 36.0, 3.625, 6.75
HW = PL + NUT
ft = lambda i: f"{int(i//12)}'-{i%12:.1f}\""
top_face = 4*12 + 6          # bent top block faces +-4'-6"
blkA = (19*12 - 35)          # Type A block tip face, 16'-1"
blkC = (19*12 - 35)          # Type C block tip face, 16'-1"
wall = 42.0                  # DB / UB diaphragm outer face 3'-6" (SCALED)
rows = []
# top bent piece: dead end at +4'-6" (U1 side, 3 5/8 protrusion), stressing end at -4'-6" (D1 side, 3'-0 tail)
bent = HW + PROT + 2*top_face + HW + TAIL
rows.append(("T-B bent piece", -(top_face+HW+TAIL), top_face+HW+PROT, bent, 13*12+3))
cU = top_face + HW + PROT    # coupler centre U1 side
rows.append(("T-U1 piece", cU, blkA+HW+TAIL, (blkA - cU) + HW + TAIL, 14*12+6))
cD = -(top_face + HW + TAIL) # coupler centre D1 side = end of the bent piece tail
rows.append(("T-D1 piece", -(blkA+HW+TAIL), cD, (blkA + cD) + HW + TAIL, 11*12+7))
# bottom U1 piece: dead end on the DB wall face (-3'-6"), 3 5/8 protrusion, stressing at the U1 Type C block
u1b = PROT + HW + wall + blkC + HW + TAIL
rows.append(("B-U1 piece", -(wall+HW+PROT), blkC+HW+TAIL, u1b, 23*12+7))
cB = -(wall + HW + PROT)
rows.append(("B-D1 piece", -(blkC+HW+TAIL), cB, (blkC + cB) + HW + TAIL, 15*12))
for n, a, b, L, m in rows:
    print(f"{n:16s} s {a/12:+7.3f} .. {b/12:+7.3f} ft  length {ft(L):>10s}  printed min {ft(m):>9s}  diff {L-m:+5.1f} in")
print(f"top couplers: U1 side s {(cU-CPL/2)/12:+.3f}..{(cU+CPL/2)/12:+.3f} ft (joint at +5.000), D1 side s {(cD-CPL/2)/12:+.3f}..{(cD+CPL/2)/12:+.3f} ft")
print(f"bottom coupler: s {(cB-CPL/2)/12:+.3f}..{(cB+CPL/2)/12:+.3f} ft (open zone -3.5..-5.0)")
