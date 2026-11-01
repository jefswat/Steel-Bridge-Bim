import fitz,collections
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
c=collections.defaultdict(list)
for p in [334,335,336,343]:
    for sp in doc[p-1].get_texttrace():
        txt="".join(chr(ch[0]) for ch in sp["chars"])
        for ch in sp["chars"]:
            if ch[0]>127 and ch[0]!=0xb0:
                c[(hex(ch[0]),ch[1],sp["font"],round(sp["size"],1))].append((p,txt.encode("ascii","replace").decode(),[round(v) for v in ch[3]]))
for k,v in sorted(c.items()): print(k,len(v),v[:3])
