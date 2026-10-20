import csv,os,pickle,sys
F=r'C:\SBM\qc\ptc\results\checks.csv'
def nextid():
    return sum(1 for _ in open(F,encoding='utf-8'))  # header + n rows -> next index = n+1
def add(rows):
    # rows: [page,sheet,view,item,method,plan,model,diff,tol,result,evidence]
    n=sum(1 for _ in open(F,encoding='utf-8'))-1
    w=csv.writer(open(F,'a',newline='',encoding='utf-8'))
    for i,r in enumerate(rows):
        w.writerow(['C%04d'%(n+i+1)]+[ (float(x) if hasattr(x,'item') else x) for x in r])
    return n+1,n+len(rows)
if __name__=='__main__':
    allr=[]
    for f in sys.argv[2:]:
        for r in pickle.load(open(f,'rb')): allr.append(r)
    ev=sys.argv[1]
    rows=[]
    for r in allr:
        r=list(r);
        # r: pg,sh,view,item,method,plan,model,diff,tol,res,note
        note=r[10]
        rows.append([r[0],r[1],r[2],r[3],r[4],r[5],r[6],r[7],r[8],r[9],ev+(' ; '+note if note else '')])
    print(add(rows))
