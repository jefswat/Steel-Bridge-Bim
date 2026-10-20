import sys,json; sys.path.insert(0,'.'); from cut import *
from PIL import Image, ImageDraw
E=load_el(); M={r['mark']:r for r in E if r['mark']}
F=json.load(open('girder_feat.json'))
EV='C:/SBM/qc/odot-o/evidence/'
def ext(segs):
    a=np.array([q for sg in segs for q in sg]); return a
def side(plan_png, model_png, out, title):
    A=Image.open(plan_png).convert('RGB'); B=Image.open(model_png).convert('RGB')
    h=max(A.height,B.height); W=A.width+B.width+20
    C_=Image.new('RGB',(W,h+30),'white'); C_.paste(A,(0,30)); C_.paste(B,(A.width+20,30))
    ImageDraw.Draw(C_).text((10,8),title,fill='black'); C_.save(out)
res={}
# ---- 1: typical tub section, sta 303+80 (span 15, away from diaphragms) radial cut
P,n=plane_at_sta(30380)
o=cut(E,P,n,lambda r: r['cls'] in ('IfcPlate','IfcSlab') and ('G1-3-' in (r['mark'] or '') or 'G2-3-' in (r['mark'] or '') or r['mark']=='DECK'))
m={}
for g,(r,s) in o.items(): m[r['mark']]=ext(s)
def edge_pts(a): return a
for G in ['G1','G2']:
    wl,wr,bf=m[G+'-3-WL1'],m[G+'-3-WR1'],m[G+'-3-BF1']; tl,tr=m[G+'-3-TFL1'],m[G+'-3-TFR1']
    # web midplane lines: fit top/bottom ends: take web points, top = max z, bottom = min z
    def web_mid(a):
        top=a[a[:,1]>a[:,1].max()-0.02].mean(0); bot=a[a[:,1]<a[:,1].min()+0.02].mean(0); return top,bot
    (lt,lb),(rt,rb)=web_mid(wl),web_mid(wr)
    # top-of-web points: underside of TF at web: use TF min z near web u
    res[G]=dict(top_web_spacing_in=abs(lt[0]-rt[0])*12, bot_web_spacing_in=abs(lb[0]-rb[0])*12,
        web_slope_run_per_rise=abs((lt[0]-lb[0])/(lt[1]-lb[1])), BF_width_in=np.ptp(bf[:,0])*12/np.cos(np.arctan(0.06)),
        BF_thk_in=(np.ptp(bf[:,1])*12- np.ptp(bf[:,0])*12*0.06)*np.cos(np.arctan(0.06)), TF_width_in=np.ptp(tl[:,0])*12/np.cos(np.arctan(0.06)),
        CL_u=(lt[0]+rt[0])/2)
res['spacing_ft']=abs(res['G1']['CL_u']-res['G2']['CL_u'])
d=m['DECK']; res['deck_width_ft']=np.ptp(d[:,0]); res['deck_thk_in']=(np.ptp(d[:,1])-np.ptp(d[:,0])*0.06)*12*np.cos(np.arctan(0.06))
res['deck_left_edge_off']=d[:,0].max(); res['deck_right_edge_off']=d[:,0].min()
draw(o, EV+'model_sec_303p80.png', 'MODEL cut (IfcOpenShell) radial at STA 303+80, looking up-station; G1 left. Measured: '+
     'top web spacing %.2f in, bottom web spacing %.2f in, girder spacing %.3f ft'%(res['G1']['top_web_spacing_in'],res['G1']['bot_web_spacing_in'],res['spacing_ft']))
import subprocess
subprocess.run([sys.executable,'-I','-W','ignore','crop.py','273','820','380','1210','600','160',EV+'plan_p273_tubsection.png'])
subprocess.run([sys.executable,'-I','-W','ignore','crop.py','270','30','30','640','330','110',EV+'plan_p270_deck.png'])
side(EV+'plan_p273_tubsection.png',EV+'model_sec_303p80.png',EV+'S1_tub_section_side_by_side.png','S1: plan p.273 (O-19) TYPICAL TUB GIRDER SECTION  vs  model cut STA 303+80')
# ---- 2: pier diaphragm P14 cut along CL brg (radial at 302+93)
P,n=plane_at_sta(30293.0)
o2=cut(E,P,n,lambda r: r['cls'] in ('IfcPlate','IfcMember','IfcBearing') and any(k in (r['mark'] or '') for k in ('SD2','XSD2','B2-','G1-2-','G2-2-')))
m2={r['mark']:ext(s) for g,(r,s) in o2.items()}
for G in ['G1','G2']:
    w=m2[G+'-SD2-W']; 
    # opening: gaps in z along vertical line at web center
    res['SD2_'+G]=dict(web_thk_plane='n/a (cut in plane)', web_u_range_in=np.ptp(w[:,0])*12, web_z_range_in=np.ptp(w[:,1])*12)
xs=m2['XSD2-1-W']; res['XSD2_web_depth_in']=np.ptp(xs[:,1])*12; 
draw(o2, EV+'model_sec_P14.png','MODEL cut along CL bearing Pier 14 (STA 302+93.00), looking up-station')
subprocess.run([sys.executable,'-I','-W','ignore','crop.py','287','20','30','700','380','110',EV+'plan_p287_pierdiaph.png'])
side(EV+'plan_p287_pierdiaph.png',EV+'model_sec_P14.png',EV+'S2_pier14_diaphragm_side_by_side.png','S2: plan p.287 (O-33) SUPPORT DIAPHRAGM PIERS 14 & 15 vs model cut at CL brg P14')
# ---- 3: external intermediate diaphragm XD3 cut at its station
xd=M['XD3-W']; s3,_=sta_off(xd['cx'],xd['cy'])
P,n=plane_at_sta(s3)
o3=cut(E,P,n,lambda r: r['cls'] in ('IfcPlate','IfcMember') and any(k in (r['mark'] or '') for k in ('XD3','G1-3-','G2-3-','K2.6')))
m3={r['mark']:ext(s) for g,(r,s) in o3.items()}
res['XD3']=dict(sta=s3, web_depth_in=np.ptp(m3['XD3-W'][:,1])*12 if 'XD3-W' in m3 else None)
if 'XD3-W' in m3 and 'XD3-TF' in m3 and 'XD3-BF' in m3:
    tf,bf_,w=m3['XD3-TF'],m3['XD3-BF'],m3['XD3-W']
    res['XD3'].update(TF_thk_in=None)
# girder TF bottom vs XD TF top at XD web position (6" per plan)
draw(o3, EV+'model_sec_XD3.png','MODEL cut at external intermediate diaphragm XD3 STA %.2f'%s3)
subprocess.run([sys.executable,'-I','-W','ignore','crop.py','282','100','40','760','330','110',EV+'plan_p282_xd.png'])
side(EV+'plan_p282_xd.png',EV+'model_sec_XD3.png',EV+'S3_ext_interm_diaphragm_side_by_side.png','S3: plan p.282 (O-28) INTERMEDIATE BRACING DETAIL vs model cut at XD3')
json.dump(res,open('sections_res.json','w'),indent=1,default=float)
print(json.dumps(res,indent=1,default=float))
