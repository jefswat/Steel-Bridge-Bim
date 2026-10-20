import fitz,io
from PIL import Image, ImageDraw
d=fitz.open(r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf")
clip=fitz.Rect(900,640,1224,792)
z=2.0
for s in range(0,108,12):
    W=Image.new("RGB",(648*3,304*4),"white")
    for k in range(12):
        i=s+k
        if i>=108: break
        pm=d[i].get_pixmap(matrix=fitz.Matrix(z,z),clip=clip)
        im=Image.open(io.BytesIO(pm.tobytes("png"))).convert("RGB")
        ImageDraw.Draw(im).text((5,5),f"p{i+1}",fill=(255,0,0))
        W.paste(im,((k%3)*648,(k//3)*304))
    W.save(f"tb_{s+1:03d}.png")
