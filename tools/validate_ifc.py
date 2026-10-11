"""Validates an IFC file with IfcOpenShell: schema, EXPRESS rules and (optionally) geometry.

usage:
  python -I tools/validate_ifc.py model.ifc [options]

  --express-sample N   EXPRESS rules (type + entity WHERE rules) on at most N instances per entity type
                       (0 = every instance; default 0). Global (file) rules always see the whole file.
  --no-express         schema validation only
  --no-schema          skip schema validation (EXPRESS / geometry only)
  --geom N             mesh up to N products per IFC class with ifcopenshell.geom (0 = off, default)
  --geom-all REGEX     also mesh EVERY product whose "Class Name Description" matches REGEX
                       (e.g. "IfcReinforcingBar.*Pier [4-7]WB column")
  --tol F              volume mismatch tolerance (default 0.02 = 2 %)
  --seed N             sampling seed (default 1)
  --json OUT           write the full result (issue groups, geometry failures) as JSON
  --max-listed N       issue groups printed (default 60)

Reference volumes for the geometry check (file length unit converted to m):
  IfcReinforcingBar / IfcTendon: pi/4 * NominalDiameter^2 * BarLength (the swept disk);
  other products: SBM_Fabrication.Mass / density (2400 kg/m3 for concrete materials, 7850 for steel), when present
  (gratings skipped: their mass is the open grid's). Volumes are taken about the mesh centroid; meshes that are
  not closed shells (OpenCascade dropped faces) are counted as "open" and flagged in the volume list.
  Note: IfcOpenShell meshes a swept disk as a polygon, so bars come out ~1.1 % under pi/4 d^2 L.
Mismatches above --tol and elements that fail to tessellate are listed.
"""
import argparse, collections, json, math, random, re, sys, time

import ifcopenshell
import ifcopenshell.validate
import ifcopenshell.express.rule_executor


class SampledFile:
    """Proxy for ifcopenshell.file that iterates / by_type()s a per-type sample (for rule_executor.run)."""

    def __init__(self, f, per_type, seed):
        self._f = f
        rnd = random.Random(seed)
        by = collections.defaultdict(list)
        for inst in f:
            by[inst.is_a()].append(inst)
        self.sample = {}
        for t, lst in by.items():
            self.sample[t] = lst if len(lst) <= per_type else rnd.sample(lst, per_type)
        self.totals = {t: len(l) for t, l in by.items()}

    def __iter__(self):
        for lst in self.sample.values():
            yield from lst

    def by_type(self, t, include_subtypes=True):
        # file-scope (global) rules and entity rules both come here; small types are complete anyway
        out = []
        for name, lst in self.sample.items():
            if name.upper() == t.upper():
                out.extend(lst)
            elif include_subtypes:
                try:
                    if lst and lst[0].is_a(t):
                        out.extend(lst)
                except Exception:
                    pass
        return out

    def __getattr__(self, name):
        return getattr(self._f, name)


def group_issues(statements):
    groups = collections.OrderedDict()
    for s in statements:
        inst = s.get("instance")
        kind = s.get("type") or "?"
        attr = s.get("attribute") or ""
        msg = str(s.get("message", ""))
        # rule name for EXPRESS errors is the attribute (TYPE.RULE); schema errors: entity.attr + first message line
        first = re.sub(r"#\d+", "#", msg.strip().split("\n")[0])[:120]
        ent = inst.is_a() if isinstance(inst, ifcopenshell.entity_instance) else ""
        key = (kind, ent, attr, first if not attr or kind == "schema" else "")
        g = groups.setdefault(key, {"type": kind, "entity": ent, "rule": attr, "message": first, "count": 0, "examples": []})
        g["count"] += 1
        if len(g["examples"]) < 5:
            g["examples"].append(f"#{inst.id()}" if isinstance(inst, ifcopenshell.entity_instance) else str(inst)[:80])
    return sorted(groups.values(), key=lambda g: -g["count"])


def length_scale(f):
    try:
        import ifcopenshell.util.unit
        return ifcopenshell.util.unit.calculate_unit_scale(f)
    except Exception:
        return 1.0


def ref_volume(el, scale, psets_cache):
    """Expected volume in m3, and its basis, or (None, None)."""
    if el.is_a("IfcReinforcingBar") or el.is_a("IfcTendon"):
        d = getattr(el, "NominalDiameter", None)
        L = getattr(el, "BarLength", None) if el.is_a("IfcReinforcingBar") else None
        if d and L:
            return math.pi / 4 * (d * scale) ** 2 * (L * scale), "pi/4 d^2 L"
        return None, None
    mass = None
    for rel in getattr(el, "IsDefinedBy", []) or []:
        if rel.is_a("IfcRelDefinesByProperties"):
            ps = rel.RelatingPropertyDefinition
            if ps.is_a("IfcPropertySet") and ps.Name == "SBM_Fabrication":
                for p in ps.HasProperties:
                    if p.Name == "Mass" and p.NominalValue is not None:
                        mass = p.NominalValue.wrappedValue
    if not mass:
        return None, None
    mat = ""
    for rel in getattr(el, "HasAssociations", []) or []:
        if rel.is_a("IfcRelAssociatesMaterial"):
            mat = getattr(rel.RelatingMaterial, "Name", "") or ""
    m = mat.lower()
    if "grating" in m:
        return None, None  # open-grid weight per area, not a solid density
    if "concrete" in m:
        rho = 2400.0
    elif any(k in m for k in ("astm", "steel", "a572", "a709", "a36", "a588")):
        rho = 7850.0
    else:
        return None, None
    return mass / rho, f"Mass/{rho:.0f} ({mat[:30]})"


def mesh_volume(geom):
    """(volume m3, open edges). Volume by the divergence theorem about the mesh centroid (an open mesh far from
    the origin would otherwise give arbitrary values); open edges = edges not shared by exactly two triangles,
    counted after welding vertices at 1e-6 m (0 = closed shell)."""
    import numpy as np
    v = np.asarray(geom.verts, dtype=float).reshape(-1, 3)
    t = np.asarray(geom.faces, dtype=np.int64).reshape(-1, 3)
    if len(t) == 0:
        return 0.0, 0
    v = v - v.mean(axis=0)
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    vol = float(np.einsum("ij,ij->i", a, np.cross(b, c)).sum() / 6.0)
    _, weld = np.unique(np.round(v / 1e-6).astype(np.int64), axis=0, return_inverse=True)
    w = weld.reshape(-1)[t]
    e = np.sort(np.concatenate([w[:, [0, 1]], w[:, [1, 2]], w[:, [2, 0]]]), axis=1)
    e = e[e[:, 0] != e[:, 1]]
    _, cnt = np.unique(e, axis=0, return_counts=True)
    return vol, int((cnt != 2).sum())


def geometry_check(f, per_class, all_regex, tol, seed, verbose=True):
    import ifcopenshell.geom
    settings = ifcopenshell.geom.settings()
    settings.set("use-world-coords", False)
    scale = length_scale(f)
    rnd = random.Random(seed)
    by = collections.defaultdict(list)
    for p in f.by_type("IfcProduct"):
        if p.Representation is None or p.is_a("IfcSpatialElement") or p.is_a("IfcAlignment"):
            continue
        by[p.is_a()].append(p)
    rx = re.compile(all_regex) if all_regex else None
    targets = {}
    for cls, lst in sorted(by.items()):
        pick = lst if len(lst) <= per_class else rnd.sample(lst, per_class)
        for p in pick:
            targets[p.id()] = (p, "sample")
        if rx:
            for p in lst:
                if rx.search(f"{cls} {p.Name or ''} {p.Description or ''}"):
                    targets[p.id()] = (p, "all")
    stats = collections.defaultdict(lambda: {"meshed": 0, "failed": 0, "open": 0, "compared": 0, "mismatch": 0})
    failures, mismatches, open_meshes = [], [], []
    t0 = time.time()
    for n, (pid, (p, why)) in enumerate(sorted(targets.items())):
        cls = p.is_a()
        st = stats[cls + (" [all]" if why == "all" else "")]
        try:
            shape = ifcopenshell.geom.create_shape(settings, p)
            vol, open_edges = mesh_volume(shape.geometry)
            vol = abs(vol)
            if len(shape.geometry.faces) == 0:
                raise RuntimeError("empty mesh")
        except Exception as e:
            st["failed"] += 1
            failures.append({"id": pid, "class": cls, "name": p.Name, "description": p.Description, "error": str(e)[:200]})
            continue
        st["meshed"] += 1
        if open_edges:
            st["open"] += 1
            open_meshes.append({"id": pid, "class": cls, "name": p.Name, "description": (p.Description or "")[:60], "open_edges": open_edges})
        ref, basis = ref_volume(p, scale, None)
        if ref:
            st["compared"] += 1
            ratio = vol / ref
            if abs(ratio - 1) > tol:
                st["mismatch"] += 1
                mismatches.append({"id": pid, "class": cls, "name": p.Name, "description": (p.Description or "")[:60],
                                   "mesh_m3": vol, "ref_m3": ref, "ratio": ratio, "basis": basis, "open_edges": open_edges})
        if verbose and n and n % 2000 == 0:
            print(f"  geometry {n}/{len(targets)} ({time.time() - t0:.0f} s)", flush=True)
    return {"stats": stats, "failures": failures, "open_meshes": open_meshes, "mismatches": mismatches, "targets": len(targets)}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("path")
    ap.add_argument("--express-sample", type=int, default=0)
    ap.add_argument("--no-express", action="store_true")
    ap.add_argument("--no-schema", action="store_true")
    ap.add_argument("--geom", type=int, default=0)
    ap.add_argument("--geom-all", default=None)
    ap.add_argument("--tol", type=float, default=0.02)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--json", default=None)
    ap.add_argument("--max-listed", type=int, default=60)
    a = ap.parse_args()

    t0 = time.time()
    ifcopenshell.ifcopenshell_wrapper.set_log_format_json()
    ifcopenshell.get_log()
    f = ifcopenshell.open(a.path)
    counts = collections.Counter(i.is_a() for i in f)
    print(f"{a.path}: schema {f.schema_identifier}, {sum(counts.values())} entities, opened in {time.time() - t0:.0f} s", flush=True)
    out = {"file": a.path, "entities": sum(counts.values()), "counts": dict(counts.most_common())}

    statements = []
    if not a.no_schema:
        t = time.time()
        log = ifcopenshell.validate.json_logger()
        ifcopenshell.validate.validate(f, log, express_rules=False)
        # parse-stage (C++) errors: only reported when validate() opens the path itself; collect them here
        ifcopenshell.validate.log_internal_cpp_errors(f, a.path, log)
        statements += log.statements
        print(f"schema validation: {len(log.statements)} issues ({time.time() - t:.0f} s)", flush=True)
    if not a.no_express:
        t = time.time()
        log = ifcopenshell.validate.json_logger()
        target = f if a.express_sample <= 0 else SampledFile(f, a.express_sample, a.seed)
        ifcopenshell.express.rule_executor.run(target, log)
        statements += log.statements
        scope = "all instances" if a.express_sample <= 0 else f"<= {a.express_sample} instances per type"
        print(f"EXPRESS rules ({scope}): {len(log.statements)} issues ({time.time() - t:.0f} s)", flush=True)
        out["express_scope"] = scope

    groups = group_issues(statements)
    out["issues"] = len(statements)
    out["issue_groups"] = groups
    print(f"issues: {len(statements)}")
    for g in groups[: a.max_listed]:
        print(f"  {g['count']:7d}  [{g['type']}] {g['entity']} {g['rule']} {g['message'][:90]}  e.g. {', '.join(g['examples'][:3])}")

    if a.geom > 0 or a.geom_all:
        t = time.time()
        res = geometry_check(f, a.geom, a.geom_all, a.tol, a.seed)
        print(f"geometry: {res['targets']} products meshed in {time.time() - t:.0f} s")
        print(f"  {'class':40s} {'meshed':>7s} {'failed':>7s} {'open':>6s} {'compared':>8s} {'>tol':>6s}")
        for cls, s in sorted(res["stats"].items()):
            print(f"  {cls:40s} {s['meshed']:7d} {s['failed']:7d} {s['open']:6d} {s['compared']:8d} {s['mismatch']:6d}")
        for x in res["failures"][:40]:
            print(f"  FAIL #{x['id']} {x['class']} {x['name']} {x['description']}: {x['error']}")
        for x in sorted(res["mismatches"], key=lambda m: -abs(m["ratio"] - 1))[:60]:
            print(f"  VOL  #{x['id']} {x['class']} {x['name']} {x['description']}: mesh {x['mesh_m3']:.5f} ref {x['ref_m3']:.5f} m3 ratio {x['ratio']:.3f} ({x['basis']}){' OPEN MESH' if x['open_edges'] else ''}")
        res["stats"] = {k: dict(v) for k, v in res["stats"].items()}
        out["geometry"] = res

    if a.json:
        with open(a.json, "w") as fh:
            json.dump(out, fh, indent=1, default=str)
    print(f"done in {time.time() - t0:.0f} s")


if __name__ == "__main__":
    main()
