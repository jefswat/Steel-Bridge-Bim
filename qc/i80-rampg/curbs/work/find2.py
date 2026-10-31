import fitz,sys,re
pat=re.compile(sys.argv[1],re.I)
for path in sys.argv[2:]:
    d=fitz.open(path); print("==",path[-40:])
    empty=0
    for i,p in enumerate(d):
        t=p.get_text()
        if len(t.strip())<50: empty+=1
        hits=list(pat.finditer(t))
        if hits:
            print(f"p{i+1}: {len(hits)}  ", " || ".join(sorted(set(t[max(0,m.start()-50):m.end()+50].replace('\n',' | ') for m in hits))[:4]))
    print("near-empty pages",empty)
