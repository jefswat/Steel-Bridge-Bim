"""Writes the cap data CSVs (bent frame: S along the bent line from CL cap, + right looking up-station; N from the bent control line, + up-station; z below cap top)."""
import csv, math
OUT = r"C:/SBM/qc/bonner-unit5/substructure/"
def fi(x):
    s = '-' if x < 0 else ''; x = abs(x); f = int(x + 1e-9); i = (x - f) * 12
    return f"{s}{f}'-{round(i*16)/16:g}\""
types = {"18": [18], "19-22": [19,20,21,22], "23-24": [23,24], "25-28": [25,26,27,28], "29": [29]}
# outlines (top plan; 19-28 taper to the bottom outline)
T18 = [(-21.75,-6.5),(21.75,-6.5),(21.75,1.5),(12.5,1.5),(11.5,2.5),(11.5,9.5),(-11.5,9.5),(-11.5,2.5),(-12.5,1.5),(-21.75,1.5)]
T29 = [(s,-n) for s,n in reversed(T18)]
R = [(-9,-6.5),(9,-6.5),(9,6.5),(-9,6.5)]; RB = [(-26/3,-37/6),(26/3,-37/6),(26/3,37/6),(-26/3,37/6)]
with open(OUT+"cap-outlines.csv","w",newline="") as f:
    w = csv.writer(f); w.writerow(["cap_type","bents","polygon","vertex","S_ft","N_ft","S_ftin","N_ftin","z_below_top_ft","tag","page"])
    for t,poly,z,tag,pg in [("18",T18,"0 to 5.00 (prismatic)","VECTOR","p.354"),("29",T29,"0 to 5.00 (prismatic)","VECTOR","p.371")]:
        for i,(s,n) in enumerate(poly): w.writerow([t,"/".join(map(str,types[t])),"top=bottom",i+1,f"{s:.4f}",f"{n:.4f}",fi(s),fi(n),z,tag,pg])
    for t,pg in [("19-22","p.359/361"),("23-24","p.363/365"),("25-28","p.367/369")]:
        for i,(s,n) in enumerate(R): w.writerow([t,"/".join(map(str,types[t])),"top (z 0 to 2.50 vertical)",i+1,f"{s:.4f}",f"{n:.4f}",fi(s),fi(n),"0 / 2.50","VECTOR",pg])
        for i,(s,n) in enumerate(RB): w.writerow([t,"/".join(map(str,types[t])),"bottom (taper 2.50 -> 5.50)",i+1,f"{s:.4f}",f"{n:.4f}",fi(s),fi(n),"5.50","VECTOR",pg])
# PT bar pockets (stressing blockouts) at the cap top, 1'-6" deep; bar = pocket centre
pk = []
def add(t, s, n, ds, dn, open_face, tag, pg):
    pk.append([t, f"{s:.4f}", f"{n:.4f}", fi(s), fi(n), f"{ds:.4f}", f"{dn:.4f}", "1.50", open_face, tag, pg])
for t,pg,n4 in [("18","p.354",1),("29","p.371",1)]:
    for s,n in [(-3,4.75),(3,4.75),(-3,-4.75),(3,-4.75),(-7.25,2.5),(7.25,2.5),(-7.25,-2.5),(7.25,-2.5)]:
        add(t,s,n,1.25,1.25,"no (inside the cap)","VECTOR",pg)
for t,pg,sN,nS in [("19-22","p.359",2.25,2.0),("23-24","p.363",1.9375,2.0),("25-28","p.367",3.0,2.5)]:
    for sg in (-1,1):
        for sn in (-1,1):
            # N-face pocket: duct 1'-9" from the face, pocket from the face 2'-5" printed (2'-4" drawn) x 1'-3"
            add(t, sg*sN, sn*4.75, 1.25, 29/12, "yes: N face (+top)", "VECTOR (2'-5\" printed; drawn 2'-4\")", pg)
            add(t, sg*7.25, sn*nS, 29/12, 1.25, "yes: S face (end) (+top)", "VECTOR (2'-5\" printed; drawn 2'-4\")", pg)
with open(OUT+"cap-pt-pockets.csv","w",newline="") as f:
    w = csv.writer(f); w.writerow(["cap_type","bar_S_ft","bar_N_ft","bar_S","bar_N","pocket_size_S_ft","pocket_size_N_ft","pocket_depth_ft","open_to_side_face","tag","page"])
    w.writerows(pk)
# corner ducts at the cap bottom (6" dia x 2'-0" oversized duct, bar anchored at the column top)
with open(OUT+"cap-corner-ducts.csv","w",newline="") as f:
    w = csv.writer(f); w.writerow(["cap_type","S_ft","N_ft","diameter_in","height_above_cap_bottom_ft","tag","page"])
    for t,pg in [("23-24","p.363"),("25-28","p.367")]:
        for s in (-1,1):
            for n in (-1,1):
                w.writerow([t,f"{s*(9-23.5/12):.4f}",f"{n*(6.5-23.5/12):.4f}",6,2.0,"VECTOR text 1'-11 1/2\" from both faces (drawn 2'-0\")",pg])
# shear keys in the cap bottom (cap sheets) vs column top segment
keys = {"18":[(0,4.75),(4,4.75),(-4,4.75),(0,-4.75),(4,-4.75),(-4,-4.75),(7.25,5/3),(7.25,-5/3),(-7.25,5/3),(-7.25,-5/3)],
        "19-22":[(s,n) for s in (0,4.5,-4.5) for n in (4.75,-4.75)]+[(s,n) for s in (7.25,-7.25) for n in (0,8/3,-8/3)],
        "23-24":[(s,n) for s in (0,3.875,-3.875) for n in (4.75,-4.75)]+[(s,n) for s in (7.25,-7.25) for n in (0,8/3,-8/3)],
        "25-28":[(s,n) for s in (0,4,-4) for n in (4.75,-4.75)]+[(s,n) for s in (7.25,-7.25) for n in (5/3,-5/3)]}
keys["29"] = keys["18"]
with open(OUT+"cap-shear-keys.csv","w",newline="") as f:
    w = csv.writer(f); w.writerow(["cap_type","S_ft","N_ft","tag","page"])
    for t,l in keys.items():
        for s,n in l: w.writerow([t,f"{s:.4f}",f"{n:.4f}","VECTOR (cap plan dashed keys + front / side view chains); position CONFLICTS with the column top segment - see caps.md", {"18":"p.354/357","29":"p.371/374","19-22":"p.359","23-24":"p.363","25-28":"p.367"}[t]])
# pedestals
ped = []
E18 = {"18G1":(-18.3490,70.43),"18G2":(-11.0938,70.10),"18G3":(-3.8385,69.78),"18G4":(3.4167,69.45),"18G5":(10.6719,69.12),"18G6":(17.9271,68.80)}
for g,(s,z) in E18.items(): ped.append(["18",g+" (span 18, other unit)",f"{s:.4f}","-3.1667",5.3333,2.1667,z,"VECTOR (dims p.354/355; EL p.354)"])
ped += [["18","span 19 left brg (CL brg S -8.25)","-7.5000","5.5000",6.1667,4.1667,69.93,"VECTOR (3'-10\" inboard / 2'-4\" outboard of CL brg, 4'-2\" centred, p.354/355)"],
        ["18","span 19 right brg (CL brg S +8.25)","7.5000","5.5000",6.1667,4.1667,69.19,"VECTOR"]]
E29 = {"30G1":(-17.0260,71.42),"30G2":(-5.5260,71.13),"30G3":(5.9740,70.84),"30G4":(17.4740,70.55)}
for g,(s,z) in E29.items(): ped.append(["29",g+" (span 30, other unit)",f"{s:.4f}","3.1667",5.3333,2.1667,z,"VECTOR (dims p.371; EL p.371)"])
ped += [["29","span 29 left brg (CL brg S -8.25)","-7.5000","-5.5000",6.1667,4.1667,71.20,"VECTOR (p.371)"],
        ["29","span 29 right brg (CL brg S +8.25)","7.5000","-5.5000",6.1667,4.1667,70.79,"VECTOR (p.371)"]]
EL = {19:(65.24,64.71),20:(69.74,69.27),21:(69.74,69.27),22:(69.74,69.27),23:(69.62,69.36),24:(69.62,69.36),25:(69.62,69.36),26:(69.62,69.36),27:(69.62,69.36),28:(66.04,65.78)}
for b,(l,r) in EL.items():
    ped += [[str(b),"left brg","-5.2500","0.0000",6.0,6.0,l,"VECTOR (elevation table p.359/363/367)"],[str(b),"right brg","5.2500","0.0000",6.0,6.0,r,"VECTOR"]]
with open(OUT+"cap-pedestals.csv","w",newline="") as f:
    w = csv.writer(f); w.writerow(["bent","pedestal","centre_S_ft","centre_N_ft","size_S_ft","size_N_ft","top_EL_ft","tag"])
    w.writerows(ped)
# volumes (derived)
def area(p): return abs(sum(p[i][0]*p[i-1][1]-p[i-1][0]*p[i][1] for i in range(len(p))))/2
A18 = area(T18); open18 = 9 - 4*0.5*0.25**2
v18 = (A18*5 - open18*5 - 8*1.25*1.25*1.5)
A1, A2 = 18*13, (52/3)*(37/3)
vR = A1*2.5 + 3/3*(A1+A2+math.sqrt(A1*A2)); openR = 3*5.5 - 4*0.5*0.25**2
vRn = vR - openR*5.5 - 8*1.25*(29/12)*1.5
print(f"bent 18/29 plan area {A18:.2f} sf, gross {A18*5/27:.2f} CY, net (opening, pockets) {v18/27:.2f} CY")
print(f"bents 19-28 gross {vR/27:.2f} CY, net {vRn/27:.2f} CY; model outline 18: {43.5*6.5+23*9.5:.2f} sf")
