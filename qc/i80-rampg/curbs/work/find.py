import fitz,sys,re
for path in sys.argv[1:]:
    d=fitz.open(path); print("==",path,len(d))
    for i,p in enumerate(d):
        t=p.get_text()
        hits=[m for m in re.finditer(r'(?i)curb',t)]
        if hits:
            ctx=set()
            for m in hits: ctx.add(t[max(0,m.start()-60):m.end()+60].replace('\n',' | '))
            print(f"p{i+1}: {len(hits)}"); 
            for c in list(ctx)[:6]: print("   ",c)
