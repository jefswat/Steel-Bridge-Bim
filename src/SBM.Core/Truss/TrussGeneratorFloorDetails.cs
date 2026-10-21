using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        private int _spliceCount, _spliceRivets, _spliceRivetMiss;

        // ------------------------------------------------------------------ stringer splices (TrussFloorDefinition.Splices)
        /// <summary>Splice plates + rivets of one stringer line at the selected floor beam crossings (stringers on top: the pieces abut at the crossing).</summary>
        private void BuildStringerSplices(TrussFloorDefinition fl, TrussStringerLine line, TrussBuiltSection sec, double top, IReadOnlyList<double> crossSta,
            IReadOnlySet<double> jointSta, string asm)
        {
            if (fl.Splices is not { Count: > 0 } defs || !fl.StringersOnTop) return;
            var parts = Model.Steel.Parts;
            var wf = SteelCatalog.FindWideFlange(sec.Definition.Designation);
            double tw = sec.WebThickness > 0 ? sec.WebThickness : wf?.WebThickness ?? In(0.5);
            double tf = wf?.FlangeThickness ?? (sec.Definition.FlangeThickness > 0 ? sec.Definition.FlangeThickness : In(0.75));
            double fillet = wf != null ? Math.Max(0, wf.Kdes - wf.FlangeThickness) : In(0.5);
            double yTop = sec.YMax, yBot = sec.YMin, yc = (yTop + yBot) / 2;
            Vec3 Wp(double s) => _al.PointAt(s, line.Offset).WithZ(_al.ElevationAt(s, line.Offset) - top - sec.YMax);
            foreach (var sd in defs)
            {
                if (sd.Lines.Count > 0 && !sd.Lines.Contains(line.Name, StringComparer.OrdinalIgnoreCase)) continue;
                if (sd.Section.Length > 0 && !sd.Section.Equals(line.Section, StringComparison.OrdinalIgnoreCase)) continue;
                Assumption? asu = sd.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(sd.Unresolved + Src, "splice plates / rivets as listed") : null;
                if (asu != null) Warn(sd.Unresolved);
                for (int i = 1; i + 1 < crossSta.Count; i++)
                {
                    bool pick = sd.Crossings != null ? sd.Crossings.Contains(i) : sd.Every > 0 && i >= sd.First && (i - sd.First) % sd.Every == 0;
                    if (!pick || !jointSta.Contains(crossSta[i])) continue;
                    double sta = crossSta[i];
                    var pj = Wp(sta);
                    var ax = (Wp(crossSta[i + 1]) - Wp(crossSta[i - 1])).Normalized();
                    var xv = Vec3.Cross(Up, ax).Normalized();
                    var yv = Vec3.Cross(ax, xv).Normalized();
                    Vec3 W(double a, double x, double y) => pj + ax * a + xv * x + yv * y;
                    var mesh = new Mesh();
                    void Box(double a0, double a1, double x0, double x1, double y0, double y1)
                    {
                        if (a1 - a0 < 1e-6 || x1 - x0 < 1e-6 || y1 - y0 < 1e-6) return;
                        var poly = new[] { new Vec2(a0, y0), new Vec2(a1, y0), new Vec2(a1, y1), new Vec2(a0, y1) };
                        MeshBuilder.AddLoft(mesh, poly, new[] { poly.Select(q => W(q.X, x0, q.Y)).ToArray(), poly.Select(q => W(q.X, x1, q.Y)).ToArray() });
                    }
                    double a0 = -sd.Before, a1 = sd.After;
                    if (sd.WebPlateThickness > 0 && sd.WebPlateHeight > 0)
                        foreach (int s in new[] { -1, 1 })
                        {
                            double xa = s * tw / 2, xb = s * (tw / 2 + sd.WebPlateThickness);
                            Box(a0, a1, Math.Min(xa, xb), Math.Max(xa, xb), yc - sd.WebPlateHeight / 2, yc + sd.WebPlateHeight / 2);
                        }
                    if (sd.TopPlateThickness > 0 && sd.TopPlateWidth > 0) Box(a0, a1, -sd.TopPlateWidth / 2, sd.TopPlateWidth / 2, yTop, yTop + sd.TopPlateThickness);
                    if (sd.UnderPlateThickness > 0 && sd.UnderPlateWidth > 0)
                        foreach (int s in new[] { -1, 1 })
                        {
                            double xa = s * (tw / 2 + fillet + In(1.0 / 16)), xb = s * (tw / 2 + fillet + In(1.0 / 16) + sd.UnderPlateWidth);
                            Box(a0, a1, Math.Min(xa, xb), Math.Max(xa, xb), yTop - tf - sd.UnderPlateThickness, yTop - tf);
                        }
                    if (mesh.IsEmpty) continue;
                    _spliceCount++;
                    string mark = $"SPL-{line.Name}-{_spliceCount}";
                    parts.Add(new SteelPart { Mark = mark, Category = PartCategory.SplicePlate, Assembly = asm, Mesh = mesh, Material = Material(sd.Material), Length = a1 - a0, Assumption = asu,
                        Description = $"stringer splice {(sd.Label.Length > 0 ? sd.Label : "plates")}, line {line.Name} at {_u.FormatStation(sta)}" });
                    Model.PanelOf[mark] = $"Splice {line.Name}";
                    if (sd.RivetDiameter <= 0) continue;
                    // rivets: web (shank across the web), flange (shank through top plate + flange + under plate)
                    var bb = mesh.Bounds();
                    var lo = bb.Min - new Vec3(0.3, 0.3, 0.3); var hi = bb.Max + new Vec3(0.3, 0.3, 0.3);
                    var pr = new SteelLineProbe();
                    for (int k = 0; k < parts.Count; k++)
                    {
                        if (parts[k].Mesh is not { IsEmpty: false } m) continue;
                        var b = m.Bounds();
                        if (b.Max.X < lo.X || b.Min.X > hi.X || b.Max.Y < lo.Y || b.Min.Y > hi.Y || b.Max.Z < lo.Z || b.Min.Z > hi.Z) continue;
                        pr.Add(m, k, lo, hi);
                    }
                    var rasu = RivetFlag(sd.Unresolved.Length > 0 ? sd.Unresolved : null, "splice rivets at the drawn columns") ?? asu;
                    void Rivet(Vec3 p, Vec3 dir, Vec3 hint)
                    {
                        if (pr.At(p, dir, 0.3) is not { } iv) { _spliceRivetMiss++; return; }
                        MergeRivet(p + dir * iv.t1, -dir, hint, iv.t1 - iv.t0, sd.RivetDiameter, asm, sd.RivetMaterial, rasu);
                        _spliceRivets++;
                    }
                    foreach (double c in sd.WebRivetColumns)
                        for (int r = 0; r < sd.WebRivetRows; r++)
                            Rivet(W(c, 0, yc + (r - (sd.WebRivetRows - 1) / 2.0) * sd.WebRivetPitch), xv, ax);
                    foreach (double c in sd.FlangeRivetColumns)
                        foreach (int s in new[] { -1, 1 })
                            Rivet(W(c, s * sd.FlangeRivetGage, yTop - tf / 2), yv, ax);
                }
            }
        }
    }
}
