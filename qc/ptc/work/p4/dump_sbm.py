"""Read-only dump of the truss inputs in the PTC .sbm copy (sections, members, joints) in inches."""
import json, sys, os, collections
SBM = r'C:\SBM\temp\qc4\ptc.sbm'
OUT = os.path.dirname(os.path.abspath(__file__))
IN = 0.0254
def i(x):
    return None if x is None else round(x / IN, 4)
def fr(x):
    """inches -> text with 1/16"""
    if x is None: return '-'
    v = x / IN
    n = round(v * 16)
    w, r = divmod(n, 16)
    if r == 0: return '%d' % w
    from math import gcd
    g = gcd(r, 16)
    return ('%d %d/%d' % (w, r // g, 16 // g)) if w else '%d/%d' % (r // g, 16 // g)

def plates(lst):
    out = []
    for p in lst or []:
        s = '%sx%s' % (fr(p['Width']), fr(p['Thickness']))
        if p.get('Perforated'): s += ' perf'
        if p.get('Length'): s += ' L=%s' % fr(p['Length'])
        if p.get('Start'): s += ' start=%s' % fr(p['Start'])
        if p.get('Offset'): s += ' off=%s' % fr(p['Offset'])
        if p.get('BetweenGussets'): s += ' btwG'
        out.append(s)
    return out

d = json.load(open(SBM))
for b in d['Bridges']:
    t = b.get('Truss')
    if not t: continue
    name = b['Name']
    fn = os.path.join(OUT, 'dump_' + name.replace(' ', '_').replace('/', '-') + '.txt')
    use = collections.defaultdict(list)
    for m in t['Members']:
        use[m['Section']].append(m['Name'] + ('@' + m['Plane'] if m['Plane'] else ''))
    with open(fn, 'w') as f:
        f.write('# %s\n## SECTIONS\n' % name)
        for s in t['Sections']:
            k = s['Kind']
            f.write('\n[%s] kind=%s mat=%s label="%s"\n' % (s['Name'], k, s.get('Material'), s.get('Label')))
            keys = {}
            for key in ('WebDepth', 'WebsOutToOut', 'WebsBackToBack', 'AngleProjection', 'Depth', 'FlangeWidth', 'WebThickness', 'FlangeThickness', 'BackToBack', 'ShapeArea', 'Width', 'Thickness', 'Diameter'):
                if key in s and s[key]: keys[key] = fr(s[key])
            for key in ('Angle', 'Shape', 'Channel', 'AnglesOutside', 'AngleEdges', 'WebVertical', 'FlangesOut', 'Unresolved'):
                if key in s and s[key] not in (None, '', False):
                    keys[key] = s[key] if key != 'Unresolved' else s[key][:200]
            f.write('  ' + ' '.join('%s=%s' % kv for kv in keys.items()) + '\n')
            if s.get('WebPlies'):
                f.write('  plies(out->in): ' + '; '.join('%sx%s%s%s' % (fr(p['Depth']), fr(p['Thickness']), (' L=' + fr(p['Length'])) if p.get('Length') else '', (' st=' + fr(p['Start'])) if p.get('Start') else '') for p in s['WebPlies']) + '\n')
            if s.get('TopPlates'): f.write('  top: ' + '; '.join(plates(s['TopPlates'])) + '\n')
            if s.get('BottomPlates'): f.write('  bot: ' + '; '.join(plates(s['BottomPlates'])) + '\n')
            if s.get('Lacing'): f.write('  lacing: %s\n' % json.dumps(s['Lacing'])[:200])
            other = {k2: v for k2, v in s.items() if k2 not in ('Name', 'Kind', 'Label', 'Material', 'WebPlies', 'TopPlates', 'BottomPlates', 'Lacing') and k2 not in keys and v not in (None, 0, '', False, [], {})}
            if other: f.write('  other: ' + json.dumps(other)[:400] + '\n')
            f.write('  used by: ' + ', '.join(use.get(s['Name'], [])) + '\n')
        f.write('\n## JOINTS\n')
        for j in t['Joints']:
            f.write('\n<%s%s> t=%s ifd=%s nogusset=%s outline=%d\n' % (j['Point'], '@' + j['Plane'] if j.get('Plane') else '', fr(j.get('GussetThickness')), fr(j.get('GussetsInsideFaceDistance')), j.get('NoGusset'), len(j.get('Outline') or [])))
            for L in j.get('GussetLayers') or []:
                f.write('  layer t=%s outline=%d %s\n' % (fr(L.get('Thickness')), len(L.get('Outline') or []), (L.get('Unresolved') or '')[:120]))
            for p in j.get('Plates') or []:
                f.write('  plate ' + json.dumps({k2: (fr(v) if isinstance(v, float) else v) for k2, v in p.items() if v not in (None, 0, '', False, [])})[:400] + '\n')
            other = {k2: v for k2, v in j.items() if k2 not in ('Point', 'Plane', 'GussetThickness', 'GussetsInsideFaceDistance', 'NoGusset', 'Outline', 'GussetLayers', 'Plates', 'Rivets') and v not in (None, 0, '', False, [])}
            if other: f.write('  other: ' + json.dumps(other)[:300] + '\n')
        f.write('\n## MEMBER STIFFENERS / END CONNECTIONS\n')
        for m in t['Members']:
            ex = {k2: v for k2, v in m.items() if k2 in ('Stiffeners', 'EndConnections', 'DepthReductionStart', 'DepthReductionEnd') and v}
            if ex: f.write('%s@%s: %s\n' % (m['Name'], m['Plane'], json.dumps(ex)[:600]))
    print(fn)
