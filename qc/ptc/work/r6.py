import sys,json
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
import rdet
M=json.load(open(r'C:\SBM\qc\ptc\work\r4.json'))
STN={'approach3':'approach3','approach4':'approach4','main':'main'}
out={}
for key in sys.argv[1:]:
    st,j=key.split(':')
    d,r=rdet.run(st,j,out=r'C:\SBM\qc\ptc\evidence\rivets-%s-%s.png'%(st,j),quiet=True)
    m=M.get(key)
    print(key,'plan dots',d,'rings',r,'total',d+r,'| model per face',max(m['clusters'].values()) if m and m['clusters'] else 0)
    out[key]=(d,r)
json.dump(out,open(r'C:\SBM\qc\ptc\work\r6.json','w'))
