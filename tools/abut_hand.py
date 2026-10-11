# Abutment bend diagrams read on raster clips (design 116 sheets 88 / 92, design 316 sheet 83); inches, out to out, in bar order.
# kind: seg = open bar (segments in order along the bar), hoop = rectangle w x h with two lap tails, V = vee (two arms).
# Every row's segment sum was checked against the printed bar-list length (see compare.py: "sum ok").
H = {}


def add(design, page, mark, kind, segs, pin, note=''):
    H.setdefault((design, page), {})[mark] = dict(kind=kind, segs=segs, pin=pin, note=note)


for d, p in (('116', 88), ('116', 92)):
    add(d, p, '5d3', 'seg', [12, 29, 12], 2.5, 'U')
    add(d, p, '4d6', 'seg', [46, 14, 46], 2, 'U')
    add(d, p, '4d8', 'seg', [12, 14, 12], 2, 'U')
    add(d, p, '5b1', 'hoop', [76.5, 41, 6, 6], 2.5, 'hoop w x h + 6 / 6 laps')
    add(d, p, '5h4', 'seg', [6, 57, 18, 57, 6], 2.5, 'hat')
    add(d, p, '5d4', 'seg', [12, 17, 15], 2.5, 'bent with 1\'-5 slant (1\'-0 x 1\'-0 projections)')
    add(d, p, '5j1', 'seg', [29, 80, 29], 3.75 if p == 88 else 3.75, 'U')
    add(d, p, '8j3', 'V', [30, 30], 6, 'width 2\'-8, depth 2\'-1 1/2')
    add(d, p, '5j2', 'hoop', [32, 31, 6, 6], 2.5, 'hoop w x h + 6 / 6 laps')
# West
add('116', 88, '5g2', 'seg', [10, 15, 30], 3.75, 'hook 10 + 1\'-3 + 2\'-6 slant (1\'-9 1/2 x 1\'-9 projections)')
add('116', 88, '5m1', 'seg', [27, 44, 27], 2.5, 'U')
add('116', 88, '6g4', 'seg', [12, 66], 4.5, 'L')
add('116', 88, '5n2', 'hoop', [50, 45, 6, 6], 2.5, 'hoop w x h + 6 / 6 laps')
add('116', 88, '5b4', 'seg', [54, 120, 16, 38], 2.5, 'C: 4\'-6 + 10\'-0 + 1\'-4 + 3\'-2 slant (2\'-3 x 2\'-2 1/2)')
add('116', 88, '5b3', 'seg', [18, 16, 87, 18], 2.5, 'two 1\'-6 slants, 1\'-4 and 7\'-3')
add('116', 88, '5b2', 'hoopv', [41, 6, 6], 2.5, 'hoop width 2\'-0 (MIN) to 6\'-3 (MAX) x 3\'-5 + 6 / 6 laps (VARIES)')
# East
add('116', 92, '5g2', 'seg', [10, 63], 3.75, 'L')
add('116', 92, '5m1', 'seg', [24, 45, 24], 2.5, 'U')
add('116', 92, '6g4', 'seg', [12, 53], 4.5, 'L')
add('116', 92, '5b2', 'seg', [18, 75, 18], 2.5, 'two 1\'-6 slants and 6\'-3 (1 3/4 offsets)')
# South (Ramp G)
add('316', 83, '5g2', 'seg', [10, 63], 3.75, 'L')
add('316', 83, '4d6', 'seg', [41, 14, 41], 2, 'U')
add('316', 83, '5m1', 'seg', [25, 47, 25], 2.5, 'U')
add('316', 83, '5b1', 'hoop', [68, 41, 6, 6], 2.5, 'hoop w x h + 6 / 6 laps')
add('316', 83, '5d3', 'seg', [12, 29, 12], 2.5, 'U')
add('316', 83, '5h4', 'seg', [6, 57, 18, 57, 6], 2.5, 'hat')
add('316', 83, '5d4', 'seg', [12, 17, 15], 2.5, 'bent with slant')
add('316', 83, '5j1', 'seg', [29, 80, 29], 2.5, 'U')
add('316', 83, '6g4', 'seg', [12, 53], 4.5, 'L')
add('316', 83, '8j3', 'V', [30, 30], 6, 'V')
add('316', 83, '5j2', 'hoop', [32, 31, 6, 6], 2.5, 'hoop')
add('316', 83, '5b2', 'seg', [18, 67, 18], 2.5, 'U')
add('316', 83, '6h2', 'seg', [129, 12], 4.5, 'L')
add('316', 83, '6h3', 'seg', [96, 12], 4.5, 'L')
add('316', 83, '5n2', 'hoop', [50, 48.5, 6, 6], 2.5, 'hoop')


def total(e):
    if e['kind'] == 'hoop': return 2 * (e['segs'][0] + e['segs'][1]) + e['segs'][2] + e['segs'][3]
    if e['kind'] == 'V': return sum(e['segs'])
    if e['kind'] == 'hoopv': return None
    return sum(e['segs'])
