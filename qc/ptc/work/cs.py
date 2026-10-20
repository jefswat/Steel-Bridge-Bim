import sys,pymupdf
from PIL import Image,ImageDraw
pages=[int(x) for x in sys.argv[2:]]
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
ims=[]
for n in pages:
    r=d[n-1].rect
    clip=pymupdf.Rect(r.width*0.70,r.height*0.72,r.width*0.995,r.height*0.965)
    pm=d[n-1].get_pixmap(matrix=pymupdf.Matrix(1.7,1.7),clip=clip)
    im=Image.frombytes('RGB',(pm.width,pm.height),pm.samples)
    ImageDraw.Draw(im).text((4,4),f'PAGE {n}',fill=(255,0,0))
    ims.append(im)
W=max(i.width for i in ims); H=sum(i.height for i in ims)
out=Image.new('RGB',(W,H),'white'); y=0
for i in ims: out.paste(i,(0,y)); y+=i.height
out.save(sys.argv[1]); print(out.size)
