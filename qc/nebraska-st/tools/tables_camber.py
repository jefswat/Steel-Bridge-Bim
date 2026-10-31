import sys,re,json; sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from rows import rows
from qclib import *
def gtab(pg,box,y0=None,y1=None):
    """return dict girder-> list of floats concatenated by blocks in y order"""
    out={g:[] for g in "ABCD"}
    for y,r in rows(pg,*box):
        if y0 and y<y0: continue
        if y1 and y>y1: continue
        t=[w for x,w in r]
        if t[:2]==["GIRDER","LINE"] and len(t)>2 and t[2] in "ABCD":
            vals=[]
            for w in t[3:]:
                try: vals.append(float(w))
                except: break
            out[t[2]]+=vals
    return out
cam=gtab(36,(80,340,1150,600),y1=600)           # camber ordinates 67 pts
blk=gtab(36,(80,640,500,720))
sd=gtab(37,(60,40,1150,340))                      # slab+barrier deflection
st=gtab(37,(60,375,1150,720))                     # steel deflection
hv=gtab(38,(60,40,1150,500))                      # haunch elev
for n,t in [("camber",cam),("slab+barrier defl",sd),("steel defl",st),("haunch elev",hv)]:
    print(n,{g:len(v) for g,v in t.items()})
print("blocking",blk)
json.dump(dict(cam=cam,blk=blk,sd=sd,st=st,hv=hv),open(BASE+"/extract/camber_tables.json","w"))
cols=["page","table","girder","point","value","source_quote"]
for n,t,pg in [("camber ordinate (in)",cam,36),("deflection slab+barrier (in)",sd,37),("deflection steel (in)",st,37),("girder line haunch elevation (ft)",hv,38)]:
    for g,v in t.items():
        for i,x in enumerate(v): ext("camber_tables",cols,[pg,n,g,i+1,x,"p%d table"%pg])
pts=[1,17,23,29,39,45,51,67]
for g,v in blk.items():
    for p,x in zip(pts,v): ext("camber_tables",cols,[36,"blocking (ft)",g,p,x,"p36 blocking data"])
