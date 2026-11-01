import fitz,re,sys,csv
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
FR={0x192:'3/4',0x201a:'3/8',0x201e:'5/8',0x2020:'1/8',0x2021:'7/8',0x2022:'1/2',0x2026:'1/4',0x2030:'?'}
def fix(s): return "".join((" "+FR.get(ord(c),'?')) if ord(c)>127 else c for c in s)
UW={3:.376,4:.668,5:1.043,6:1.502,7:2.044,8:2.670,9:3.400,10:4.303,11:5.313,14:7.650}
def ft(s):
    m=re.match(r"(\d+)'-?(\d+)?(?: (\d+)/(\d+))?\"?",s.replace("40-0","40'-0"))
    if not m: return None
    v=int(m.group(1))+ (int(m.group(2)) if m.group(2) else 0)/12
    if m.group(3): v+=int(m.group(3))/int(m.group(4))/12
    return v
GROUP={336:"18,29",339:"19",342:"20,21,22",347:"23,24",350:"25",353:"26,27,28"}
rows=[]
for p in map(int,sys.argv[1:]):
    pg=doc[p-1]; W=pg.get_text("words")
    for h in [w for w in W if w[4]=="WEIGHT" and w[1]<120]:
        row=[w for w in W if abs(w[1]-h[1])<4 and h[0]-480<w[0]<=h[0]]
        cols={}
        for w in row: cols.setdefault(w[4],(w[0]+w[2])/2)
        if "BAR" not in cols: continue
        if "DIM" in cols: cols["DIMA"]=cols.pop("DIM")+12
        xb=cols["BAR"]
        marks=sorted([w for w in W if re.match(r"^[A-Z]\d+$",w[4]) and abs((w[0]+w[2])/2-xb)<15 and h[1]<w[1]<h[1]+1100],key=lambda w:w[1])
        for m in marks:
            ym=(m[1]+m[3])/2; r={"page":p,"bents":GROUP[p],"bar":m[4]}
            for n,k in [("NO.","no"),("SIZE","size"),("TYPE","type"),("MAT'L","mat"),("LENGTH","length"),("DIMA","dimA"),("WEIGHT","weight")]:
                if n not in cols: r[k]="";continue
                tol=40 if n in("LENGTH","WEIGHT","DIMA") else 16
                c=sorted([w for w in W if abs((w[0]+w[2])/2-cols[n])<tol and abs((w[1]+w[3])/2-ym)<8 and w is not m],key=lambda w:w[0])
                txt=[fix(w[4]) for w in c]
                if k in("mat","no"): txt=txt[:1]
                r[k]=" ".join(txt)
            L=ft(r["length"]); 
            try: calc=int(r["no"])*L*UW[int(r["size"])]; r["calc_lb"]=round(calc); r["diff_lb"]=round(calc-float(r["weight"]))
            except Exception as e: r["calc_lb"]="";r["diff_lb"]=""
            rows.append(r)
w=csv.DictWriter(open(sys.stdout.fileno(),"w",newline="",closefd=False),fieldnames=list(rows[0].keys()))
w.writeheader(); w.writerows(rows)
