import sys, pymupdf
d=pymupdf.open(r"C:\SBMData\B00167 - Nebraska Ramp B - Design 116.pdf")
pg=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); z=float(sys.argv[6]) if len(sys.argv)>6 else 2.5
out=sys.argv[7] if len(sys.argv)>7 else f"evidence/crops/p{pg:02d}_{int(x0)}_{int(y0)}.png"
import os; os.makedirs("evidence/crops",exist_ok=True)
d[pg-1].get_pixmap(matrix=pymupdf.Matrix(z,z),clip=pymupdf.Rect(x0,y0,x1,y1)).save(out); print(out)
