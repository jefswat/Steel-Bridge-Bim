import pymupdf as fitz, re, collections, sys
ROOT = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\dist\SteelBridgeModeler' + '\\'
for f, pgs in (('B00168 - CBIS-ESI-Westbound I-80 - Design 116.pdf', range(18, 95)), ('B00169 - CBIS-ESI-Ramp G - Design 316.pdf', None)):
    d = fitz.open(ROOT + f)
    cnt = collections.Counter()
    first = {}
    for i in (pgs or range(len(d))):
        c = d[i].read_contents().decode('latin-1')
        for m in re.finditer(r'\\(2[0-7][0-7])', c):
            cnt[m.group(1)] += 1
            first.setdefault(m.group(1), i + 1)
    print(f, len(d), sorted(cnt.items()), first)
