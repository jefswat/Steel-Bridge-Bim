"""Validate an IFC file against one or more IDS files with ifctester.

Usage: python -I tools/run_ids.py MODEL.ifc IDS_DIR_OR_FILE [--out DIR]
Prints a per-IDS summary (applicable / passed / failed specs) and the failing
specifications with failure counts and sample reasons; with --out also writes
an HTML and a JSON report per IDS file.
"""
import argparse, glob, os, sys, collections
import ifcopenshell
from ifctester import ids, reporter

ap = argparse.ArgumentParser()
ap.add_argument("ifc"); ap.add_argument("ids"); ap.add_argument("--out")
ap.add_argument("--samples", type=int, default=2)
a = ap.parse_args()
files = sorted(glob.glob(os.path.join(a.ids, "*.ids"))) if os.path.isdir(a.ids) else [a.ids]
model = ifcopenshell.open(a.ifc)
print(f"{os.path.basename(a.ifc)}: schema {model.schema_identifier}, {len(list(model))} entities")
for f in files:
    spec = ids.open(f)
    spec.validate(model)
    name = os.path.splitext(os.path.basename(f))[0]
    st = collections.Counter()
    fails = []
    for s in spec.specifications:
        applicable = len(s.applicable_entities) if hasattr(s, "applicable_entities") else 0
        if s.status is None: st["n/a"] += 1
        elif s.status: st["pass"] += 1
        else:
            st["FAIL"] += 1
            reasons = collections.Counter()
            nfail = 0
            for r in s.requirements:
                for fe in getattr(r, "failures", []) or []:
                    nfail += 1
                    reasons[str(fe.get("reason", fe) if isinstance(fe, dict) else fe)[:160]] += 1
            fails.append((s.name, applicable, nfail, reasons.most_common(a.samples), s.minOccurs, s.maxOccurs))
    print(f"\n== {name}: {len(spec.specifications)} specs | pass {st['pass']} | FAIL {st['FAIL']} | not applicable {st['n/a']}")
    for n, app, nf, rs, mn, mx in fails:
        print(f"  FAIL {n}  (applicable {app}, failures {nf}, occurs {mn}..{mx})")
        for r, c in rs:
            print(f"       {c}x {r}")
    if a.out:
        os.makedirs(a.out, exist_ok=True)
        h = reporter.Html(spec); h.report(); h.to_file(os.path.join(a.out, name + ".html"))
        j = reporter.Json(spec); j.report(); j.to_file(os.path.join(a.out, name + ".json"))
