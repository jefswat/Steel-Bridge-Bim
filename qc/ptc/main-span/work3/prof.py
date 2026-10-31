import sys, fitz
pdf=fitz.open(r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf")
pg=int(sys.argv[1]); dpi=int(sys.argv[2]); fx0,fx1=float(sys.argv[3]),float(sys.argv[4])
p=pdf[pg-1]; R=p.rect
for fy in sys.argv[5:]:
    fy=float(fy)
    clip=fitz.Rect(R.x0+fx0*R.width, R.y0+fy*R.height, R.x0+fx1*R.width, R.y0+fy*R.height+72.0/dpi*3)
    pix=p.get_pixmap(dpi=dpi,clip=clip,colorspace=fitz.csGRAY)
    w=pix.width; s=pix.samples
    row=[min(s[i], s[w+i], s[2*w+i]) for i in range(w)]
    runs=[];i=0
    while i<w:
        if row[i]<128:
            j=i
            while j<w and row[j]<128: j+=1
            runs.append((i,j-i)); i=j
        else: i+=1
    print(fy, ' '.join(f"{a}[{b}]" for a,b in runs))
