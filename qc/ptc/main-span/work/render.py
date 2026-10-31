import sys, fitz
pdf = fitz.open(r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf")
out = sys.argv[1]; dpi = int(sys.argv[2])
for a in sys.argv[3:]:
    if ':' in a:  # page:x0,y0,x1,y1 in fractions
        p, r = a.split(':'); x0,y0,x1,y1 = map(float, r.split(','))
        pg = pdf[int(p)-1]; R = pg.rect
        clip = fitz.Rect(R.x0+x0*R.width, R.y0+y0*R.height, R.x0+x1*R.width, R.y0+y1*R.height)
        pg.get_pixmap(dpi=dpi, clip=clip).save(f"{out}/p{p}_{x0:.2f}_{y0:.2f}_{dpi}.png")
    else:
        pg = pdf[int(a)-1]; pg.get_pixmap(dpi=dpi).save(f"{out}/p{a}_{dpi}.png"); print(a, pg.rect)
