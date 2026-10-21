import csv,os
CK=r'C:\SBM\qc\ptc\results\checks.csv'
def nextid():
    with open(CK,encoding='utf-8') as f:
        last=f.read().rstrip().splitlines()[-1].split(',')[0]
    return int(last[1:])+1
def add(page,sheet,view,item,method,plan,model,tol,result=None,evidence='',kind='num'):
    diff=''
    if result is None:
        if kind=='num':
            diff=round(float(model)-float(plan),4); result='pass' if abs(diff)<=float(tol) else 'fail'
        else: result='pass' if str(plan)==str(model) else 'fail'
    elif kind=='num':
        try: diff=round(float(model)-float(plan),4)
        except Exception: diff=''
    i=nextid()
    with open(CK,'a',newline='',encoding='utf-8') as f:
        csv.writer(f).writerow([f'C{i:04d}',page,sheet,view,item,method,plan,model,diff,tol,result,evidence])
    return result
