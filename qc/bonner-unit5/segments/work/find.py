import pymupdf, sys, re
doc = pymupdf.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
pat=re.compile(sys.argv[1], re.I)
for i in range(int(sys.argv[2])-1, int(sys.argv[3])):
    t=" ".join(w[4] for w in doc[i].get_text("words"))
    for m in pat.finditer(t):
        print(i+1, t[max(0,m.start()-100):m.end()+150].replace("\n"," ")); break
