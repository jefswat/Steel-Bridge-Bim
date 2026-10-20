# print words with positions in region
import sys, pymupdf as fitz
p=int(sys.argv[1]); 
r=fitz.Rect(*map(float,sys.argv[2:6])) if len(sys.argv)>5 else None
d=fitz.open(r'C:\SBMData\Vol 2_Phase 2B Bridge Combined.pdf')
pg=d[p-1]
# group by lines via get_text('dict')
for b in pg.get_text('dict',clip=r)['blocks']:
    for l in b.get('lines',[]):
        t=''.join(s['text'] for s in l['spans']).strip()
        if t: print('%6.0f %6.0f %s'%(l['bbox'][0],l['bbox'][1],t))
