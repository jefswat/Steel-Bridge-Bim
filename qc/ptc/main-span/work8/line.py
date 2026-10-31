# line.py page x0 y0 x1 y1 [width]: dark runs along a line (300 dpi px); reports distance along the line in px
import sys, fitz, math
PDF = r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf"
pg=int(sys.argv[1]); x0,y0,x1,y1=[float(a) for a in sys.argv[2:6]]
p=fitz.open(PDF)[pg-1]; k=72/300
X0,X1=min(x0,x1)-5,max(x0,x1)+5; Y0,Y1=min(y0,y1)-5,max(y0,y1)+5
pix=p.get_pixmap(matrix=fitz.Matrix(300/72,300/72),clip=fitz.Rect(X0*k,Y0*k,X1*k,Y1*k),colorspace=fitz.csGRAY)
W,H=pix.width,pix.height; s=pix.samples
L=math.hypot(x1-x0,y1-y0); n=int(L)
runs=[];cur=None
for i in range(n+1):
    t=i/n; x=x0+(x1-x0)*t-X0; y=y0+(y1-y0)*t-Y0
    xi,yi=int(round(x)),int(round(y))
    dark = 0<=xi<W and 0<=yi<H and s[yi*W+xi]<128
    if dark and cur is None: cur=i
    if not dark and cur is not None: runs.append((cur,i-1)); cur=None
if cur is not None: runs.append((cur,n))
print(" ".join(f"{(a+b)/2:.0f}[{b-a+1}]@({x0+(x1-x0)*(a+b)/2/n:.0f},{y0+(y1-y0)*(a+b)/2/n:.0f})" for a,b in runs))
