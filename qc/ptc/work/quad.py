import sys,pymupdf
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
pg=int(sys.argv[1]); z=float(sys.argv[2]) if len(sys.argv)>2 else 2.0
W,H=1224,792
# plan area approx x 40..1140, y 20..775
xs=[40,590,1190]; ys=[20,400,780]
for i in range(2):
    for j in range(2):
        r=pymupdf.Rect(xs[i]-0,ys[j],xs[i+1]+0,ys[j+1]) if False else pymupdf.Rect(xs[i],ys[j],xs[i+1],ys[j+1])
        pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(z,z),clip=r); pm.save(f'r3/p/q{pg}_{j}{i}.png')
print('ok')
