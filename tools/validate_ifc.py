# Validates an IFC file with IfcOpenShell (schema + EXPRESS rules) and prints a summary of issues by type.
# usage: python -I tools/validate_ifc.py model.ifc [max_listed]
import sys, collections, logging, ifcopenshell, ifcopenshell.validate

path = sys.argv[1]
max_listed = int(sys.argv[2]) if len(sys.argv) > 2 else 40
f = ifcopenshell.open(path)
print(f"{path}: schema {f.schema}, {len(list(f))} entities")
log = ifcopenshell.validate.json_logger()
ifcopenshell.validate.validate(f, log, express_rules=True)
issues = log.statements
print(f"issues: {len(issues)}")
kinds = collections.Counter((i.get("type") or i.get("level") or "?", str(i.get("message", ""))[:90]) for i in issues)
for (k, m), n in kinds.most_common(max_listed):
    print(f"  {n:6d}  [{k}] {m}")
