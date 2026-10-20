import fitz
d=fitz.open(r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf")
print(len(d), d[0].rect, d[50].rect)
from PIL import Image
import io
# contact sheets 3x2 of pages at width 700
n=len(d)
for s in range(0,n,6):
    ims=[]
    for i in range(s,min(s+6,n)):
        p=d[i]; z=700/p.rect.width
        pm=p.get_pixmap(matrix=fitz.Matrix(z,z)); ims.append(Image.open(io.BytesIO(pm.tobytes("png"))).convert("RGB"))
    h=max(i.height for i in ims)
    W=Image.new("RGB",(2100,h*2),"white")
    for k,im in enumerate(ims): W.paste(im,((k%3)*700,(k//3)*h))
    W.save(f"cs_{s+1:03d}.png")
