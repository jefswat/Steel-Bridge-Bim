import csv, os
PATH=r'C:\SBM\qc\ptc\work\r3\rows3.csv'
def add(page,sheet,view,item,method,plan,model,tol,result=None,evidence='',kind='num'):
    """tol numeric abs tolerance (kind num), or 'exact' string compare"""
    diff=''
    if result is None:
        if kind=='num':
            diff=round(float(model)-float(plan),4); result='pass' if abs(diff)<=float(tol) else 'fail'
        else:
            result='pass' if str(plan)==str(model) else 'fail'
    elif kind=='num':
        try: diff=round(float(model)-float(plan),4)
        except Exception: diff=''
    new=not os.path.exists(PATH)
    with open(PATH,'a',newline='',encoding='utf-8') as f:
        w=csv.writer(f)
        if new: w.writerow(['page','sheet','view_table','item','method','value_plan','value_model','diff','tolerance','result','evidence'])
        w.writerow([page,sheet,view,item,method,plan,model,diff,tol,result,evidence])
    return result
