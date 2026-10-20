"""Scans an IFC for the ORIGINATOR's interpretation (QC independence leaks): plan / page references, UNRESOLVED, "scaled",
"assumed", inch-fraction size strings, section designations, and any text that cites a sheet / note / PDF.

usage:
  python -I tools/check_ifc_leaks.py model.ifc [--expect-clean] [--examples N] [--json OUT]

Every string attribute and every string property value (IfcPropertySingleValue, IfcLabel / IfcText / IfcIdentifier) is scanned,
GlobalIds excluded. Prints counts per leak kind and per (entity, attribute / property), plus examples. Exit status 1 with
--expect-clean when any leak is found (use it on `sbm export --for-check` output; normal exports are expected to leak).
Regexes mirror SBM.IO.Ifc.IfcCheckFilter.
"""
import argparse, collections, json, re, sys
import ifcopenshell

KINDS = {
    "page_ref": re.compile(r"\bpp?\.\s*\d|\bpdf\b|\bsheets?\b|\bOT\d|\bdetail [A-Z0-9]\b|\brev\.?\s*\d|\(\d{2}-\d{4}", re.I),
    "note": re.compile(r"\bnotes?\b|\bsee\b", re.I),
    "UNRESOLVED": re.compile(r"\bUNRESOLVED\b", re.I),
    "scaled": re.compile(r"\bscaled\b|\bvectors?\b|\binferred\b|\bderived\b", re.I),
    "assumed": re.compile(r"\bassum", re.I),
    "plan_text": re.compile(r"\bplans?\b|\bnot given\b|\bnot shown\b", re.I),
    "inch_size": re.compile("\\d+\\s*/\\s*\\d+|\\d\\s*(?:\"|″|\\bin\\b|\\binch)", re.I),
    "designation": re.compile(r"\b(?:WT|W|HP|MC|C|L|PL|HSS|TS|S|M)\s?\d+(?:\.\d+)?\s?[xX×]\s?\d"),
}
SKIP_ATTRS = {"GlobalId", "type", "id", "PredefinedType"}  # PredefinedType is an enumeration (e.g. SHEET), not text


def strings(e):
    """(label, text) of every string held by entity e: string attributes and property values."""
    t = e.is_a()
    if t == "IfcPropertySingleValue":
        v = e.NominalValue
        if v is not None and isinstance(getattr(v, "wrappedValue", None), str):
            yield f"{t}.{e.Name}", v.wrappedValue
        return
    for k, v in e.get_info(recursive=False).items():
        if k in SKIP_ATTRS:
            continue
        if isinstance(v, str):
            yield f"{t}.{k}", v


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("ifc")
    ap.add_argument("--expect-clean", action="store_true")
    ap.add_argument("--examples", type=int, default=3)
    ap.add_argument("--json")
    a = ap.parse_args()
    f = ifcopenshell.open(a.ifc)
    by_kind = collections.Counter()
    by_where = collections.Counter()
    examples = collections.defaultdict(list)
    n_strings = 0
    leaking = set()
    for e in f:
        for where, text in strings(e):
            if not text:
                continue
            n_strings += 1
            for kind, rx in KINDS.items():
                if rx.search(text):
                    by_kind[kind] += 1
                    by_where[(where, kind)] += 1
                    leaking.add(e.id())
                    ex = examples[(where, kind)]
                    if len(ex) < a.examples and text not in ex:
                        ex.append(text if len(text) <= 120 else text[:117] + "...")
    total = sum(by_kind.values())
    print(f"{a.ifc}: {n_strings:,} strings scanned, {total:,} leak hits in {len(leaking):,} entities")
    for kind in KINDS:
        print(f"  {kind:12s} {by_kind.get(kind, 0):8,}")
    for (where, kind), n in by_where.most_common(25):
        print(f"    {n:7,}  {kind:11s} {where}   e.g. {examples[(where, kind)]}")
    if a.json:
        with open(a.json, "w") as fh:
            json.dump({"file": a.ifc, "strings": n_strings, "hits": total, "entities": len(leaking), "by_kind": dict(by_kind),
                       "by_where": [{"where": w, "kind": k, "count": n, "examples": examples[(w, k)]} for (w, k), n in by_where.most_common()]}, fh, indent=1)
    if a.expect_clean and total:
        print("LEAKS FOUND")
        sys.exit(1)
    print("clean" if not total else "(leaks present: expected for a normal export)")


if __name__ == "__main__":
    main()
