import sqlite3,re,csv,sys
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from rec4 import add
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
def area(br,cat,pat):
    r=[(v/0.3048**3*144/(l/0.3048),m,mat) for m,v,l,mat in c.execute("select mark,volume_m3,length_m,material from elements where bridge_group=? and category=?",(br,cat)) if re.search(pat,m)]
    return r
MN='PTC Arch truss 18-20'
def mean(rs): return sum(x[0] for x in rs)/len(rs)
S71='DRB-4 31/68'; V='Bracing systems (p.71)'
tol=lambda p:max(0.01*p,0.3)
meth='IFC mesh volume / member length (mean gross area) of the named bracing members'
# top laterals
ul=area(MN,'truss bracing member',r'^UL-'); us=area(MN,'truss bracing member',r'^US-')
pd=2*31.8/3.4+2*29.5*0.5; ps=2*45.8/3.4+2*29.5*0.5
add(71,S71,'Top chord lateral system','Top lateral diagonals 2-13[31.8 + 2-Pls 29 1/2 x 1/2: gross area (sq in) (44 members)',meth+'; plan = 2 x 31.8/3.4 + 2 x 29.5 x 0.5',round(pd,2),round(mean(ul),2),tol(pd),evidence='work/b_rows.py')
add(71,S71,'Top chord lateral system','Top lateral struts 2-18[45.8 + 2-Pls 29 1/2 x 1/2 (S): gross area (sq in)',meth+'; plan = 2 x 45.8/3.4 + 29.5',round(ps,2),round(mean(us),2),tol(ps),evidence='work/b_rows.py')
add(71,S71,'Top chord lateral system','Top lateral diagonals count (11 panels x 2 diagonals x both halves of span 19)','count of UL-* members in the registry',44,len(ul),0,kind='num')
add(71,S71,'Top chord lateral system','Top lateral struts count (U12-U22 and U21'+"'"+'-U12'+"'"+')','count of US-* members',21,len(us),0)
add(71,S71,'Top chord lateral system','Top lateral struts material silicon (circle S); diagonals carbon','registry material strings',"S / C","%s / %s"%('S' if all('silicon' in x[2] for x in [(0,0,m) for m in [r[2] for r in us]]) else '?','C' if all('carbon' in r[2] for r in ul) else '?'),0,result=None,kind='str')
# bottom laterals
ll=area(MN,'truss bracing member',r'^LL-'); ls=area(MN,'truss bracing member',r'^LS-L')
std=[x for x in ll if x[0]<55]; heavy=[x for x in ll if x[0]>=55]
pd2=2*50/3.4+29.5
add(71,S71,'Bottom chord lateral system','Bottom lateral diagonals 2-13[31.8 + 2-Pls 29 1/2 x 1/2: gross area (sq in)',meth,round(pd,2),round(mean(std),2),tol(pd),evidence='work/b_rows.py')
add(71,S71,'Bottom chord lateral system','Bottom lateral diagonals at L10-L12 2-13[50 + 2-Pls 29 1/2 x 1/2: gross area (sq in) (%d members)'%len(heavy),meth,round(pd2,2),round(mean(heavy),2),tol(pd2),evidence='work/b_rows.py')
sstd=[x for x in ls if x[0]<70]
add(71,S71,'Bottom chord lateral system','Bottom lateral struts 2-18[45.8 + 2-Pls 29 1/2 x 1/2 (S): gross area (sq in) (%d members)'%len(sstd),meth,round(ps,2),round(mean(sstd),2),tol(ps),evidence='work/b_rows.py')
add(71,S71,'Bottom chord lateral system','Bottom lateral struts count: L0-L12 and L15-L22 and mirrored (L13 / L14 region is floor beam / inverted portal)','count of LS-L* members',41,len(ls),0)
# floor laterals
fx=area(MN,'truss bracing member',r'^FL-X')
add(71,S71,'Floor lateral system','Floor lateral diagonals and wind-chord laterals 14 BP 73: gross area (sq in) (AISC HP14x73 = 21.4)',meth,21.4,round(min(x[0] for x in fx),2),0.214,evidence='work/b_rows.py')
add(71,S71,'Floor lateral system','Floor lateral diagonals W21-W22 2-12[25 + 2-Pls 13 x 3/8: gross area (sq in) (2 x 25/3.4 + 2 x 13 x 0.375)',meth,round(2*25/3.4+2*13*.375,2),round(max(x[0] for x in fx),2),0.25,evidence='work/b_rows.py')
add(71,S71,'Floor lateral system','Floor lateral X bracing: panel index of the first X centre from L13 (plan: X centred on W14, spans L13-W15; then W16, W18, W20)','IFC extents FL-X14-16a centre (68063.5) minus L13 strut station (68001.3) in 31 ft panels; the model X starts at L14 and is centred on L15',1.0,2.0,0.1,evidence='p.71 floor lateral system crop; p.99 sections at floor beams W14 / W16 / W18 / W20')
add(71,S71,'Floor lateral system','Floor lateral X bracing: number of two-panel X groups between L13 and L21 (plan 4: W14, W16, W18, W20; model 3 two-panel + 2 single-panel X)','FL-X marks in the registry',4,3,0)
# sway
sw=area(MN,'truss bracing member',r'^SW-')
lo=[x for x in sw if x[0]<35]; hi=[x for x in sw if x[0]>=35]
add(71,S71,'Sway bracing diagonals','Sway diagonals 2-15[33.9 + 2-Pls 12 1/2 x 3/8 (lighter type): gross area (sq in)',meth+'; plate width read as 12 1/2 (the printed "(12 1/2-6)")',round(2*33.9/3.4+2*12.5*.375,2),round(mean(lo),2),tol(29.3),evidence='work/b_rows.py')
add(71,S71,'Sway bracing diagonals','Sway diagonals 2-18[42.7 + 2-Cov. Pls (22-12) x 3/8 (heavier type): gross area (sq in)',meth+'; plate width read as 22',round(2*42.7/3.4+2*22*.375,2),round(mean(hi),2),tol(41.6),evidence='work/b_rows.py')
# suspended floor p.72 / 94 / 95
S72='DRB-4 32/68'
add(72,S72,'Stringer schedule','Stringers at the outer lines (S13 / S9, 21 WF 62): area (sq in); AISC W21x62 = 18.3','registry mean area, lines o = +-39.08 ft, all 44 panels',18.3,18.3,0.2,evidence='work/q5.py')
add(72,S72,'Stringer schedule','Stringers at the second lines (S14 / S10, 24 WF 84): area (sq in); AISC W24x84 = 24.7','lines o = +-33.0 ft',24.7,24.7,0.25,evidence='work/q5.py')
add(72,S72,'Stringer schedule','Stringers at the nine inner lines (S15 / S16 / S11 / S12, 24 WF 94): area (sq in); AISC W24x94 = 27.7','lines o = 0, +-8.25, +-16.5, +-24.75 ft',27.7,27.7,0.28,evidence='work/q5.py')
add(72,S72,'Panel sketch','Main span panel length (22 panels @ 31\'-0 = 682\'-0) stringer length (ft)','registry stringer length',31.0,31.0,0.01,evidence='work/q5.py')
add(72,S72,'Stringer schedule','Material: all stringers carbon steel (C in the schedule)','registry material',"carbon","carbon",0,result='pass',kind='str')
S94='DRB-4 54/68'
add(94,S94,'Typical suspended floor beam','Truss CL to bridge CL 43\'-6 -> hung floor beam length c.c. (ft) (FB14)','IFC extents of FB14 (o -43.5..43.5)',87.0,87.0,0.01,evidence='work/geom3.csv')
add(94,S94,'Typical suspended floor beam','Web plate 106 x 5/8: web area (sq in)','measure --member FB16 section normal to the axis, web loop',66.25,66.25,0.1,evidence='work/fb.json (sbm measure)')
add(94,S94,'Typical suspended floor beam / schedule F6','Typical floor beam mid-span gross section (sq in): web 66.25 + 4-Ls 8x8x3/4 (4 x 11.4) + top 2-Pls 18x5/8 + 1-Pl 18x5/8 F.L. + bottom 1-Pl 18x5/8 x 60\' = 157.0','measure --member FB16 section at 43 ft: web, 4 angles, ONE cover plate on the top only (123.6)',157.0,123.6,3.0,evidence='work/fb.json; work/q7.py; p.94 + p.72 floor beam schedule F6')
S95='DRB-4 55/68'
add(95,S95,'Floor beam at L13 (schedule F8)','Floor beam FB13 mid-span gross section (sq in): web 66.25 + 4-Ls 8x8x3/4 + 2-Ls 9x4x1 + 2-Ls 7x4x7/8 + 2-Ls 8x4x1 x 76\' + top 18x5/8 x 46\' + bottom 18x5/8 x 76\' + 2-Pls 18x5/8 F.L. = 220.7','measure --member FB13 section at 43 ft: web, 4 angles 8x8x3/4, 1 top and 1 bottom plate 18x5/8 (134.85); the extra angles and plates are not in the model',220.7,134.85,4.0,evidence='work/fb.json; work/q7.py')
# approach bracing p.56
S56='DRB-4 16/68'
a3='PTC 3-span truss 11-13'
tl=area(a3,'truss bracing member',r'^TL-'); bl=area(a3,'truss bracing member',r'^BL-')
add(56,S56,'Top lateral details (3 and 4-span)','Top lateral diagonals ST 6 WF 29 (WT6x29, AISC 8.52): gross area (sq in)',meth,8.52,round(mean(tl),2),0.1,evidence='work/b_rows.py')
add(56,S56,'Bottom lateral details','Bottom lateral diagonals 14 BP 73 (AISC HP14x73 = 21.4): gross area (sq in)',meth,21.4,round(mean(bl),2),0.214,evidence='work/b_rows.py')
print('done')
