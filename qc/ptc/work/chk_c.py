import csv,re,collections
from prof import *
from rec import add
R=list(csv.DictReader(open('geom.csv')))
rows=[]
# E-side girder shoe/gutter
E=[('Pier 28',70820.797,87.637,73.230),('Pier 29',70945.213,83.905,69.498),('Pier 30',71069.630,80.172,65.765),('E Abut',71192.797,76.478,62.033)]
for lab,sta,gut,shoe in E:
    c=[float(r['zmin']) for r in R if 'Girder spans 28' in r['bridge'] and r['cat']=='masonry plate' and abs((float(r['smin'])+float(r['smax']))/2-sta)<4]
    zm=min(c); gm=gutter(sta)
    rows.append((43,'DRB-4 3/68','Girder elevation shoe bottom',lab+' bottom of shoe El.','min z masonry plates',shoe,round(zm,3),round(zm-shoe,3),0.01,'pass' if abs(zm-shoe)<=.01 else 'fail','work/geom.csv'))
    rows.append((43,'DRB-4 3/68','Girder elevation gutter',lab+' gutter El.','IFC profile - 0.400',gut,round(gm,3),round(gm-gut,3),0.01,'pass' if abs(gm-gut)<=.01 else 'fail','work/prof.py'))
rows.append((43,'DRB-4 3/68','Girder elevation E abutment','E Abut: gutter El. 76.478 minus shoe 62.033 vs 14.407 at all other supports','plan-internal consistency',14.407,round(76.478-62.033,3),round(76.478-62.033-14.407,3),0.01,'conflict','p.43 prints shoe 62.033 (span 31 at 124\'-5 geometry) but gutter 76.478 at abut 711+92.797 (span 123\'-2, p.43 rev 6-1-54; p.1 gives 711+94.046). 3% from pier 30 gives shoe 62.070. Model uses printed 62.033.'))
# approach gutter p.47
for lab,sta,el in [('Pier 10 truss brg',65866.380,91.430),('Pier 11',66081.880,97.895),('Pier 12',66297.380,104.360),('Pier 13 brg',66512.880,110.825)]:
    g=gutter(sta); rows.append((47,'DRB-4 7/68','Elevation 3-span trusses gutter profile',lab+' gutter El','IFC profile -0.400',el,round(g,3),round(g-el,3),0.01,'pass' if abs(g-el)<=.01 else 'fail','work/prof.py'))
# approach truss geometry: panel spacing and depth
def axes(br,pref='N'):
    m={}
    for r in R:
        if r['bridge']==br and r['cat'] in('truss chord',) and r['mark'].startswith(pref+'-'):
            m[r['mark']]=([float(r[k]) for k in('a0s','a0z')],[float(r[k]) for k in('a1s','a1z')])
    return m
def z(a,s): (s0,z0),(s1,z1)=a; return z0+(s-s0)*(z1-z0)/(s1-s0)
for br,beg,n,pg,sh in [('PTC 3-span truss 11-13',65866.380,24,47,'DRB-4 7/68'),('PTC 3-span truss 25-27',70047.880,24,47,'DRB-4 7/68'),('PTC 4-span truss 14-17',66516.880,40,57,'DRB-4 17/68'),('PTC 4-span truss 21-24',68966.380,40,57,'DRB-4 17/68')]:
    ax=axes(br); U=[v for k,v in ax.items() if re.match(r'N-U',k) and re.match(r'N-U\d+-U\d+$',k)]; L=[v for k,v in ax.items() if re.match(r'N-L\d+-L\d+$',k)]
    s_all=sorted({round(min(v[0][0],v[1][0]),1) for v in U})
    smin=min(min(v[0][0],v[1][0]) for v in U+L); smax=max(max(v[0][0],v[1][0]) for v in U+L)
    # panel stations by hangers/verticals
    ps=sorted({round(float(r['a0s']),3) for r in R if r['bridge']==br and r['cat'] in('truss vertical','truss hanger') and r['mark'].startswith('N-') })
    dif=[b-a for a,b in zip(ps,ps[1:]) if b-a>1]
    dmax=max(abs(d-26.9375) for d in dif)
    rows.append((pg,sh,'Elevation approach truss panels',br+': panel spacing 26\'-11 1/4" (26.9375 ft)','spacing of vertical/hanger axes N side',26.9375,round(sum(dif)/len(dif),4),round(dmax,4),'1/16"=0.0052 ft','pass' if dmax<=0.0052 else 'fail','work/geom.csv (%d panels)'%len(dif)))
    # depth at panel stations
    depths=[]
    for s in ps:
        zu=[z(v,s) for v in U if min(v[0][0],v[1][0])-.01<=s<=max(v[0][0],v[1][0])+.01]
        zl=[z(v,s) for v in L if min(v[0][0],v[1][0])-.01<=s<=max(v[0][0],v[1][0])+.01]
        if zu and zl: depths.append(zu[0]-zl[0])
    dd=max(abs(d-27.5) for d in depths)
    rows.append((pg,sh,'Elevation approach truss depth',br+': truss depth U-L working points 27\'-6"','axis z U chord - L chord at panel stations',27.5,'%.4f..%.4f'%(min(depths),max(depths)),round(dd,4),'0.01 ft','pass' if dd<=0.01 else 'fail','work/geom.csv'))
    rows.append((pg,sh,'Elevation approach truss length',br+' truss length (first/last panel station)','vertical/hanger axes',beg,'%.3f..%.3f'%(ps[0],ps[-1]),round(ps[0]-beg,3),'0.01 ft','pass' if abs(ps[0]-beg)<=.01 else 'fail','work/geom.csv'))
for r in rows: print(r[3][:60],r[5],r[6],r[7],r[9])
add(rows)
