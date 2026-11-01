import fitz,collections
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
c=collections.defaultdict(list)
for p in list(range(334,354))+list(range(376,384)):
    for b in doc[p-1].get_text("dict")["blocks"]:
        for l in b.get("lines",[]):
            for s in l["spans"]:
                for ch in s["text"]:
                    if ord(ch)>127: c[(s["font"],ch)].append((p,s["text"],tuple(round(v) for v in s["bbox"])))
for k,v in sorted(c.items()): print(hex(ord(k[1])),len(v),[ (a,b.encode("ascii","replace").decode(),c) for a,b,c in v[:4]])
