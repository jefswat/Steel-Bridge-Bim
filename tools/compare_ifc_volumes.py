"""Compares element volumes of two exports of the same model (matched by GlobalId), e.g. Parametric vs ReferenceView.

usage:
  python -I tools/compare_ifc_volumes.py reference.ifc other.ifc [--changed-only] [--max N] [--tol F] [--seed N] [--json OUT]

  --changed-only  only products whose Body in OTHER uses an IfcPolygonalFaceSet (the solids ReferenceView rewrites)
  --max N         at most N products per IFC class (random sample, default 0 = all)
  --tol F         relative volume tolerance (default 0.005 = 0.5 %)

Meshes with ifcopenshell.geom (local coordinates), volume about the mesh centroid (tools/validate_ifc.py mesh_volume).
The files are opened one after the other (OTHER first, then REFERENCE) to keep memory down on full models.
Also reports products missing from either side and meshes of OTHER that are not closed shells.
"""
import argparse, collections, gc, json, os, random, sys, time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ifcopenshell, ifcopenshell.geom
from validate_ifc import mesh_volume, length_scale


def body_items(p):
    for rep in (p.Representation.Representations if p.Representation else []):
        if rep.RepresentationIdentifier == "Body":
            yield from rep.Items


def products(f):
    for p in f.by_type("IfcProduct"):
        if p.Representation is None or p.is_a("IfcSpatialElement") or p.is_a("IfcAlignment"):
            continue
        yield p


def mesh_all(f, gids):
    s = ifcopenshell.geom.settings()
    s.set("use-world-coords", False)
    sc = length_scale(f)
    out = {}
    for gid in gids:
        try:
            p = f.by_guid(gid)
        except Exception:
            out[gid] = None
            continue
        try:
            g = ifcopenshell.geom.create_shape(s, p).geometry
            vol, open_edges = mesh_volume(g)
            out[gid] = (abs(vol), open_edges, p.is_a(), p.Name or "", (p.Description or "")[:50])
        except Exception as e:
            out[gid] = ("fail", str(e)[:120], p.is_a(), p.Name or "", "")
    return out, sc


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("ref"); ap.add_argument("other")
    ap.add_argument("--changed-only", action="store_true")
    ap.add_argument("--max", type=int, default=0)
    ap.add_argument("--tol", type=float, default=0.005)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--json")
    a = ap.parse_args()
    t0 = time.time()

    f = ifcopenshell.open(a.other)
    by = collections.defaultdict(list)
    for p in products(f):
        if a.changed_only and not any(i.is_a("IfcPolygonalFaceSet") for i in body_items(p)):
            continue
        by[p.is_a()].append(p.GlobalId)
    rnd = random.Random(a.seed)
    gids = []
    for cls, lst in sorted(by.items()):
        gids += lst if not a.max or len(lst) <= a.max else rnd.sample(lst, a.max)
    print(f"{len(gids)} products to compare ({', '.join(f'{k} {len(v)}' for k, v in sorted(by.items()))})", flush=True)
    vo, so = mesh_all(f, gids)
    del f; gc.collect()
    print(f"  meshed other in {time.time() - t0:.0f} s", flush=True)
    f = ifcopenshell.open(a.ref)
    vr, sr = mesh_all(f, gids)
    del f; gc.collect()
    print(f"  meshed reference in {time.time() - t0:.0f} s", flush=True)

    stats = collections.defaultdict(lambda: collections.Counter())
    bad, worst = [], []
    for gid in gids:
        o, r = vo.get(gid), vr.get(gid)
        cls = (o or r or (None, None, "?"))[2]
        st = stats[cls]
        if o is None or r is None:
            st["missing"] += 1; bad.append({"gid": gid, "class": cls, "why": "missing"}); continue
        if o[0] == "fail" or r[0] == "fail":
            st["failed"] += 1; bad.append({"gid": gid, "class": cls, "name": o[3], "why": "fail", "other": o[1] if o[0] == "fail" else "", "ref": r[1] if r[0] == "fail" else ""}); continue
        vo_m3, vr_m3 = o[0] * so ** 3, r[0] * sr ** 3
        st["compared"] += 1
        if o[1]: st["open (other)"] += 1
        if r[1]: st["open (ref)"] += 1
        ratio = vo_m3 / vr_m3 if vr_m3 > 0 else float("inf")
        worst.append((abs(ratio - 1), ratio, gid, cls, o[3], o[4], vo_m3, vr_m3, o[1], r[1]))
        if abs(ratio - 1) > a.tol:
            st["> tol"] += 1
    worst.sort(reverse=True)
    print(f"\n{'class':28} {'compared':>9} {'> tol':>6} {'failed':>6} {'missing':>7} {'open(other)':>11} {'open(ref)':>9}")
    for cls, st in sorted(stats.items()):
        print(f"{cls:28} {st['compared']:9} {st['> tol']:6} {st['failed']:6} {st['missing']:7} {st['open (other)']:11} {st['open (ref)']:9}")
    print(f"\nworst ratios (other / reference), tol {a.tol:.2%}:")
    for d, ratio, gid, cls, name, desc, v1, v2, oo, orf in worst[:15]:
        print(f"  {ratio:.5f}  {cls} {name} '{desc}'  {v1:.6g} vs {v2:.6g} m3  open {oo}/{orf}  {gid}")
    if worst:
        ds = sorted(x[0] for x in worst)
        print(f"median |ratio-1| {ds[len(ds) // 2]:.2e}, max {ds[-1]:.2e}")
    for b in bad[:20]:
        print("  BAD", b)
    print(f"done in {time.time() - t0:.0f} s")
    if a.json:
        with open(a.json, "w") as fh:
            json.dump({"stats": stats, "bad": bad, "worst": worst[:200]}, fh, indent=1, default=str)
    return 1 if bad or any(st["> tol"] for st in stats.values()) else 0


if __name__ == "__main__":
    sys.exit(main())
