p=r'C:\SBM\qc\ptc\work\i_update.py'
t=open(p).read()
t=t.replace("for (i,sev,pg,el,fi,pv,mv,s,e) in new:\n    out.append({'id':i,'severity':sev,'page':pg,'element GlobalIds':el,",
"import re\nfor (i,sev,el,fi,pv,mv,s,e) in new:\n    m=re.search(r'p\\.([\\d\\-/ ,.p]+)',el); pg=m.group(1).strip() if m else '-'\n    out.append({'id':i,'severity':sev,'page':pg,'element GlobalIds':el,")
open(p,'w').write(t)
