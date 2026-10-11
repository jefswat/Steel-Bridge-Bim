# Checks IfcRoot GlobalIds in an IFC file: count, format (22 chars of the IFC base64 alphabet), duplicates.
# usage: python -I tools/check_ifc_ids.py model.ifc
import sys, re, collections, ifcopenshell
f = ifcopenshell.open(sys.argv[1])
roots = f.by_type("IfcRoot")
ok = re.compile(r"^[0-9A-Za-z_$]{22}$")
ids = [r.GlobalId for r in roots]
bad = [g for g in ids if not ok.match(g or "")]
dups = [(g, n) for g, n in collections.Counter(ids).items() if n > 1]
print(f"IfcRoot instances: {len(roots)}; malformed GlobalIds: {len(bad)}; duplicated GlobalIds: {len(dups)}")
for g, n in dups[:20]:
    users = [f"{r.is_a()}#{r.id()}" for r in roots if r.GlobalId == g][:4]
    print(f"  {g} x{n}: {', '.join(users)}")
