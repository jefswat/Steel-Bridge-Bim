import csv,collections,re,sys
d=sys.argv[1]
R=list(csv.DictReader(open(d+'/brace-rivets.csv')))
def kind(m):
    if '-EA' in m: return 'EA'
    if re.search(r'-F\d+$',m): return 'F'
    if '-BG' in m: return 'PL'
    return 'M'
c=collections.Counter()
for r in R:
    ps=r['parts'].split('|') if r['parts'] else []
    ks='+'.join(sorted(set(kind(p) for p in ps)))
    pt=''
    for p in ps:
        mm=re.search(r'-BG([A-Z]+)',p)
        if mm: pt=mm.group(1); break
    c[(r['class'],ks,pt)]+=1
for k,v in sorted(c.items(),key=lambda x:-x[1])[:25]: print(v,k)
