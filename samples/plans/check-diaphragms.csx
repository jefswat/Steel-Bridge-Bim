// Read-only report of the concrete end / pier diaphragms (SupportDefinition.Diaphragm) of every prestressed bridge in the model:
// parts, volumes, length, embedded beams, cast holes, fillers / keyways, all diaphragm diagnostics (Info + warnings) and the plan
// dimension checks (section widths / heights from the generated solids). Used for Woodbury (Design 817) and Br02069.
//   sbm run samples\plans\check-diaphragms.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"
using SBM.Core.Diaphragms;

const double CY = 0.764554857984;
var rep = Doc.Regenerate();
foreach (var b in Doc.Project.Bridges)
{
    var m = Doc.Model(b.Name);
    var cd = m.Prestressed?.ConcreteDiaphragms;
    if (cd == null) continue;
    Print($"=== {b.Name}");
    foreach (var d in cd.Diaphragms)
    {
        var bb = d.Mesh.Bounds();
        Print($"  {d.Name} [{d.ObjectType}] {d.Parts.Count} part(s), {d.Volume / CY:0.00} CY, length at CL {Units.FormatLength(d.Length)}, " +
              $"z {Units.FormatLength(bb.Min.Z, SBM.Core.Units.LengthKind.Coordinate)} .. {Units.FormatLength(bb.Max.Z, SBM.Core.Units.LengthKind.Coordinate)}" +
              (d.Assumption != null ? $"  [UNRESOLVED: {d.Assumption.Basis}]" : ""));
        foreach (var p in d.Parts)
            Print($"    part {p.Name}: x {Units.FormatLength(p.XMin, SBM.Core.Units.LengthKind.Short)} .. {Units.FormatLength(p.XMax, SBM.Core.Units.LengthKind.Short)} " +
                  $"(width {Units.FormatLength(p.XMax - p.XMin, SBM.Core.Units.LengthKind.Short)}), {p.Volume / CY:0.00} CY, closed volume check {Math.Abs(p.Mesh.Volume()) / CY:0.000} CY");
        if (d.UnbuiltParts.Count > 0) Print($"    NOT BUILT (UNRESOLVED): {string.Join(", ", d.UnbuiltParts)}");
        foreach (var h in d.Holes.Take(4))
            Print($"    hole {h.Beam}: dia {Units.FormatLength(h.Diameter, SBM.Core.Units.LengthKind.Short)} at z {Units.FormatLength(h.Center.Z, SBM.Core.Units.LengthKind.Coordinate)}, in built part {h.InsideBuiltPart}, strand clearance {Units.FormatLength(h.StrandClearance, SBM.Core.Units.LengthKind.Short)}");
        if (d.Holes.Count > 4) Print($"    ... {d.Holes.Count} holes total");
    }
    foreach (var a in cd.Accessories)
        Print($"  {a.Kind} {a.Name}: {a.Material}, {a.Volume / CY:0.000} CY ({a.Volume / Math.Pow(Ft(1), 3):0.00} CF)" + (a.Assumption != null ? $"  [UNRESOLVED: {a.Assumption.Basis}]" : ""));
    Print($"  TOTAL diaphragm concrete {cd.ConcreteVolume / CY:0.00} CY");
    foreach (var dg in cd.Diagnostics) Print($"  {dg.Severity}: {dg.Message}");
}
