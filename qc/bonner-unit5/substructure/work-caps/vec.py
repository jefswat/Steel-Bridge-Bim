import pymupdf as fitz,sys
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); minlen=float(sys.argv[6]) if len(sys.argv)>6 else 5
pg=doc[p-1]; R=fitz.Rect(x0,y0,x1,y1)
mode=sys.argv[7] if len(sys.argv)>7 else 'lt'
if 'l' in mode:
  for d in pg.get_drawings():
    w=d.get('width') or 0; dash=d.get('dashes')
    for it in d['items']:
        if it[0]=='l':
            a,b=it[1],it[2]
            if R.contains(a) and R.contains(b) and abs(a-b)>=minlen:
                print('L %.2f %.2f %.2f %.2f w%.2f %s'%(a.x,a.y,b.x,b.y,w,dash if dash and dash!='[] 0' else ''))
        elif it[0]=='re':
            r=it[1]
            if R.intersects(r) and max(r.width,r.height)>=minlen: print('RE',r,'w%.2f'%w)
        elif it[0]=='c':
            a,b=it[1],it[4]
            if R.contains(a): print('C %.2f %.2f %.2f %.2f w%.2f'%(a.x,a.y,b.x,b.y,w))
if 't' in mode:
  for b in pg.get_text('dict',clip=R)['blocks']:
    for l in b.get('lines',[]):
        s=''.join(sp['text'] for sp in l['spans']).strip()
        if s: print('T %.1f %.1f %.1f %.1f dir%s |%s'%(*l['bbox'],l['dir'],s))
