"""Compare the IFC 4.3 alignment curves of an export with SBM's own alignment evaluation.

Usage: python -I tools/check_ifc_alignment.py MODEL.ifc POINTS.json [--tol 0.001]
POINTS.json comes from `sbm run tools/ifc-alignment-points.csx model.sbm -- MODEL.ifc POINTS.json`:
per alignment name the samples [d, x, y, z] (distance along and grid coordinates, project length units).
Each IfcAlignment's 'Axis' curve (IfcGradientCurve, or the 2D IfcCompositeCurve without a profile) and its 'FootPrint'
composite curve are evaluated with ifcopenshell.geom (function_item_evaluator) at the same distances; the local result
is moved to grid coordinates with the IfcMapConversion. Prints the max plan / height deviation in mm per alignment and
the stationing referents; exit code 1 when a deviation exceeds the tolerance (meters).
"""
import argparse, json, math, sys
import ifcopenshell, ifcopenshell.geom, ifcopenshell.util.unit, ifcopenshell.util.element
import ifcopenshell.ifcopenshell_wrapper as W

ap = argparse.ArgumentParser()
ap.add_argument("ifc"); ap.add_argument("points"); ap.add_argument("--tol", type=float, default=0.001)
a = ap.parse_args()
f = ifcopenshell.open(a.ifc)
ref = json.load(open(a.points, encoding="utf-8"))
us = ifcopenshell.util.unit.calculate_unit_scale(f)  # project length unit -> m
mc = f.by_type("IfcMapConversion")
E, N, H = (mc[0].Eastings, mc[0].Northings, mc[0].OrthogonalHeight) if mc else (0.0, 0.0, 0.0)
settings = ifcopenshell.geom.settings()


def evaluator(curve):
    fn = W.map_shape(settings, curve)
    return fn, W.function_item_evaluator(settings, fn)


worst = 0.0
for al in f.by_type("IfcAlignment"):
    pts = ref.get(al.Name, {}).get("pts")
    if not pts:
        print(f"{al.Name}: no reference points"); continue
    reps = {r.RepresentationIdentifier: r for r in al.Representation.Representations}
    axis = reps["Axis"].Items[0]
    curves = [("Axis " + axis.is_a(), axis)]
    if "FootPrint" in reps: curves.append(("FootPrint " + reps["FootPrint"].Items[0].is_a(), reps["FootPrint"].Items[0]))
    for label, curve in curves:
        fn, ev = evaluator(curve)
        three_d = curve.is_a("IfcGradientCurve")
        dxy = dz = 0.0
        at_xy = at_z = None
        for d, x, y, z in pts:
            m = ev.evaluate(d * us)  # geometry kernel works in meters
            px, py, pz = m[0][3], m[1][3], m[2][3]
            e_xy = math.hypot(px - (x - E) * us, py - (y - N) * us)
            if e_xy > dxy: dxy, at_xy = e_xy, d
            if three_d:
                e_z = abs(pz - (z - H) * us)
                if e_z > dz: dz, at_z = e_z, d
        worst = max(worst, dxy, dz)
        length = fn.end() - fn.start()
        print(f"{al.Name} [{label}] {len(pts)} samples, curve length {length:.4f} m vs SBM {pts[-1][0] * us:.4f} m: "
              f"max plan dev {dxy * 1000:.4f} mm (at d={at_xy}), max height dev {dz * 1000:.4f} mm (at d={at_z})")
    for rel in al.IsNestedBy:
        for o in rel.RelatedObjects:
            if o.is_a("IfcReferent"):
                ps = ifcopenshell.util.element.get_psets(o).get("Pset_Stationing", {})
                lp = o.ObjectPlacement.RelativePlacement.Location
                print(f"   referent {o.Name!r} {o.PredefinedType} at DistanceAlong {lp.DistanceAlong.wrappedValue} "
                      f"Station {ps.get('Station')} IncomingStation {ps.get('IncomingStation')}")
print(f"WORST deviation {worst * 1000:.4f} mm (tolerance {a.tol * 1000:.1f} mm)")
sys.exit(1 if worst > a.tol else 0)
