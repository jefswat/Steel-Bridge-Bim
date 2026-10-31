import re,sys
printed = {
'CH U1-U2':100.75,'CH U2-U3':100.75,'CH U3-U4':114.44,'CH U4-U5':114.44,'CH U5-U6':200.26,'CH U6-U7':203.26,'CH U7-U8':269.01,'CH U8-U9':272.01,'CH U9-U10':310.23,'CH U10-U11':313.23,
'CH U11-U12':289.61,'CH U12-U13':242.17,'CH U13-U14':190.67,'CH U14-U15':130.51,'CH U15-U16':96.50,'CH U16-U17':109.25,'CH U17-U18':112.25,'CH U18-U19':162.26,'CH U19-U20':217.70,'CH U20-U21':254.69,'CH U21-U22':270.44,
'CH L0-L2':100.75,'CH L2-L4':100.75,'CH L4-L5':155.42,'CH L5-L6':158.42,'CH L6-L7':233.67,'CH L7-L8':233.67,'CH L8-L9':286.00,'CH L9-L10':289.00,'CH L10-L11':313.50,'CH L11-L12':379.44,'CH L12-L13':327.13,
'CH L13-L14':325.25,'CH L14-L15':286.00,'CH L15-L16':170.50,'CH L16-L17':115.49,'CH L17-L18':112.25,'CH L18-L19':112.25,'CH L19-L20':165.52,'CH L20-L21':220.51,'CH L21-L22':259.67,
'WM U0-L0':67.12,'WM U1-L1':45.98,'WM U2-L2':54.76,'WM U3-L3':45.96,'WM U4-L4':54.76,'WM U5-L5':54.76,'WM U6-L6':54.76,'WM U7-L7':54.76,'WM U8-L8':54.76,'WM U9-L9':63.63,'WM U10-L10':74.00,
'POST U11-L11':293.67,'WM U12-L12':174.44,'WM U13-L13':156.32,'WM U14-L14':143.39,'WM U15-L15':133.51,'WM U16-L16':121.76,'WM U17-L17':108.76,'WM U18-L18':94.17,'WM U19-L19':77.48,'WM U20-L20':61.86,'WM U21-L21':54.76,'WM U22-L22':45.96,
'WM L0-U1':54.76,'WM U1-L2':65.46,'WM L2-U3':64.88,'WM U3-L4':78.00,'WM L4-U5':89.48,'WM U5-L6':94.98,'WM L6-U7':94.17,'WM U7-L8':92.73,'WM L8-U9':80.50,'WM U9-L10':77.26,'WM L10-U11':61.48,
'WM U11-L12':116.69,'WM U12-L13':119.94,'WM U13-L14':118.69,'WM U14-L15':116.69,'WM U15-L16':119.94,'WM U16-L17':119.94,'WM U17-L18':119.94,'WM U18-L19':112.01,'WM U19-L20':98.76,'WM U20-L21':76.74,'WM U21-L22':58.36,
}
cur=None; areas={}; pieces={}
for line in open('out/sections.txt',encoding='utf-8'):
    m=re.match(r'=== (.+?) kind=',line)
    if m: cur=m.group(1); pieces[cur]=[]; continue
    m=re.match(r'\s+area=([\d.]+)',line)
    if m and cur: areas[cur]=float(m.group(1))
    m=re.match(r'\s+piece (.+?)\s+x\[',line)
    if m and cur:
        a=float(re.search(r' A=([\d.]+)',line).group(1)); ln=re.search(r'len=([\d.]+)ft start=(\S*)',line)
        pieces[cur].append((m.group(1),a,line))
for k,v in printed.items():
    a=areas.get(k)
    if a is None: print(k,'MISSING'); continue
    # partial pieces: full-length area only
    full=sum(p[1] for p in pieces[k] if ' len=0ft' in p[2])
    d=a-v
    flag='' if abs(d)<=0.6 else '  <==='
    print(f'{k:14s} printed {v:8.2f} model {a:8.2f} (full-len {full:8.2f}) diff {d:+7.2f} ({d/v*100:+.1f}%){flag}')
