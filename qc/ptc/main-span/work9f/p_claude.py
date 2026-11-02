p = 'CLAUDE.md'; b = open(p, 'rb').read(); s = b.decode('utf-8')
old = 'cover splice plate lengths G45, stitch rivets R05, L11 shoe stagger R09, bracing fills R14.\r\n'
assert s.count(old) == 1
s = s.replace(old, old + '''  JOINT FIT agent review (2026-11-02, TRUSS-DESIGN.md 33, tests `TrussJointFitTests`; all OPTIONAL, omitted when unset): `TrussJointDefinition.GussetSplitGap` (inner gusset = two
  plates per face split along v at the point, marks `-LA/-LB/-RA/-RB`; main L22 1/8 in., p.90), `TrussMemberDefinition.WebsAbutAt` (the other web members are cut on this
  member's side face, before setbacks / corner cuts), `SetBackClearAt` (square setback until clear), `ChordsAbutAt` (chords ending at the point cut on the post sides; main L11, p.80).
  PTC: verticals U11-U17 / L6 / L8 / L15-L18 keep their ends, U7 / U9 / L10 verticals set back square (connections-ptc.csx batch 4c); dam supports in pieces between the stringers (deck-ptc.csx).
'''.replace('\n', '\r\n'))
open(p, 'wb').write(s.encode('utf-8'))
print('ok')
