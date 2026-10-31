import sys,re; sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from rows import rows
from qclib import ext
def parse(pg,lst,box,fh="bar_list"):
    n=0
    for y,r in rows(pg,*box):
        t=" ".join(w for x,w in r)
        m=re.match(r"^(\d{1,2}[a-z]\d{1,2})\s+(.*?)\s+(\d+)\s+(\d+'-[\d ]+\"?)\s+([\d,]+)$",t)
        if m:
            ext(fh,["page","list","mark","location","count","length_text","weight_lb_text","source_quote"],[pg,lst,m.group(1),m.group(2),m.group(3),m.group(4),m.group(5),t]); n+=1
        else:
            if re.match(r"^\d{1,2}[a-z]\d",t): print("UNPARSED",t)
    print(lst,n)
if __name__=="__main__":
    parse(14,"PIER NO. 1 (p14)",(60,40,340,460))
    parse(14,"PIER NO. 2 (p14)",(610,40,900,360))
