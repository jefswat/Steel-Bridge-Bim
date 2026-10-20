import sys; sys.path.insert(0,'.'); from qc import *
import sqlite3
E=load_el(); G={r['guid']:r for r in E}
c=sqlite3.connect(r'file:C:/SBM/qc/odot-o/inputs/odot-o.sqlite?mode=ro',uri=True)
def members(gname):
    return [G[r[0]] for r in c.execute("select b.global_id from elements b join elements p on b.parent_global_id=p.global_id where p.name=?",(gname,)) if r[0] in G]
def axis(r):
    v,_=geom(r['guid']); cc=v.mean(0); w,U=np.linalg.eigh(np.cov((v-cc).T)); return U[:,2], cc
if __name__=='__main__':
    for gn in sys.argv[1:]:
        B=members(gn); print(gn,len(B))
        rows=[]
        for b in B:
            ax,cc=axis(b); s,o=sta_off(cc[0],cc[1]); rows.append((round(o,2),round(s,2),round(cc[2]+Z0,2),np.round(ax,2),round(b['d1']*12,2),round(b['d2']*12,2)))
        rows.sort(key=lambda x:(x[0],x[2]))
        for r in rows: print('  ',r)
