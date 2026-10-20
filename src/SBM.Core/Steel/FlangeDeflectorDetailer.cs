using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Flange deflectors (<see cref="FlangeDeflectorDefinition"/>, Iowa DOT Design 1617 p.32): on the outside of each exterior girder's BOTTOM flange (the top
/// flange is under the deck), BackFromBearing before the CL bearing of the listed supports, a bolted angle (3" leg flat on the flange, 4" leg up) from the
/// web face across the flange at SkewDegrees from the transverse line, and the vertical drip plate that continues it beyond the flange edge (4" above the
/// flange top, the rest below). Runs in the CONNECTIONS phase: the bolts register holes in the bottom flange.
/// Not modeled (plans do not give them): the weld of the plate to the angle (size), the angle's heel notch to clear the flange-to-web weld, caulk.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private void BuildFlangeDeflectors()
    {
        var fd = _def.FlangeDeflectors;
        if (fd == null || fd.Supports.Count == 0 || _def.Girders.IsPrestressed || _f.Girders.Count < 2) return;
        foreach (var u in fd.Unresolved) Warn(u);
        var an = SteelCatalog.FindAngle(fd.Angle);
        double d = Sec.WebDepth;
        double ang = fd.SkewDegrees * Math.PI / 180, sa = Math.Sin(ang), ca = Math.Cos(ang);
        int ng = _f.Girders.Count;
        foreach (int sup in fd.Supports.OrderBy(x => x))
            foreach (int gi in new[] { 0, ng - 1 })
            {
                var g = _f.Girders[gi];
                double o = gi == 0 ? -1 : 1;                       // outside = away from the other girders (right positive)
                double sB = BearingPathDistance(gi, sup) - fd.BackFromBearing;
                string supName = _def.Supports.Count > sup ? _def.Supports[sup].Name : $"support {sup + 1}";
                if (sB <= 0 || sB >= g.Length) { Warn($"Flange deflector {g.Name} at {supName}: the location {_u.FormatLength(fd.BackFromBearing)} before the CL bearing is off the girder; not built."); continue; }
                var (p, t, _) = g.Path.At(sB);
                var T = new Vec3(t.X, t.Y, 0).Normalized();
                var outV = new Vec3(t.Right.X, t.Right.Y, 0).Normalized() * o;
                double tw = _web[gi].Thickness(sB), bf = _bot[gi].Width(sB), tbf = _bot[gi].Thickness(sB);
                var P0 = p + outV * (tw / 2) + Up * (-d);          // web face, top of the bottom flange
                var tdir = T * sa + outV * ca;
                var wdir = T * ca - outV * sa;
                double E = Math.Max(0, bf / 2 - tw / 2);            // web face to flange edge
                double lang = E / ca;                               // length of the heel line
                double tn = sa / ca;
                // plan (u along the 30 deg line, w across it toward the ahead side); the angle is mitred flush with the web face and with the flange edge
                var fr = new Frame3(P0, tdir, wdir, Vec3.Cross(tdir, wdir));
                bool flip = Vec3.Dot(fr.Z, Up) < 0;
                void Prism(Mesh m, List<Vec2> poly, double zb, double zt)
                {
                    if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
                    if (flip) MeshBuilder.AddExtrusion(m, poly, fr, -zt, -zb); else MeshBuilder.AddExtrusion(m, poly, fr, zb, zt);
                }
                double VL = fd.VerticalLeg, HL = fd.HorizontalLeg, ta = fd.AngleThickness;
                List<Vec2> Strip(double wd) => new() { new(0, 0), new(lang, 0), new((E + wd * sa) / ca, wd), new(wd * tn, wd) };
                string asm = $"{g.Name}-FD{sup + 1}";
                _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.FlangeDeflector, Girder = gi, Description = $"Flange deflector {g.Name} at {supName}" });
                string src = fd.Source.Length > 0 ? fd.Source : "p.32";
                var why = Assumption.UnresolvedDimension(
                    UnresolvedFlag.Text(src, "flange deflector angle length / plan position / drip plate attachment", "the detail dimensions the angle legs (4 x 3), the 30 deg line, the 5'-0 from the bearing and the plate (3 x 0'-7), not the angle length or the plate's weld; the 5'-0 is read as the up-grade distance of the web-end corner from the CL bearing",
                        "Angle from the web face to the flange edge along the 30 deg line (ends mitred), plate continuing it beyond the edge, welds not modeled."),
                    "angle to the flange edge, plate 3 x 0'-7 beyond it, 5'-0 before the CL bearing");
                var angMesh = new Mesh();
                Prism(angMesh, Strip(HL), 0, ta);                    // 3" leg flat on the flange
                Prism(angMesh, Strip(ta), ta, VL);                   // 4" leg up (above the flat leg)
                _parts.Add(new SteelPart
                {
                    Mark = $"{asm}-L", Category = PartCategory.Angle, Assembly = asm, Description = $"{fd.Angle} x {In(lang)} (flange deflector angle)",
                    Mesh = angMesh, Length = lang, Material = fd.Material, NominalWeightLbPerFt = an?.WeightPerFootLb ?? 0, Assumption = why,
                });
                var plMesh = new Mesh();
                double zUnder = fd.PlateHeight - VL;               // below the flange top
                double u0 = (E + fd.PlateThickness * sa) / ca;     // the plate stands just beyond the flange edge
                Prism(plMesh, new List<Vec2> { new(u0, 0), new(u0 + fd.PlateWidth, 0), new(u0 + fd.PlateWidth, fd.PlateThickness), new(u0, fd.PlateThickness) }, -zUnder, VL);
                _parts.Add(new SteelPart
                {
                    Mark = $"{asm}-PL", Category = PartCategory.ConnectorPlate, Assembly = asm,
                    Description = $"PL {In(fd.PlateWidth)} x {In(fd.PlateThickness)} x {In(fd.PlateHeight)} (flange deflector drip plate)",
                    Mesh = plMesh, Length = fd.PlateHeight, Material = fd.Material, Assumption = why,
                });
                // bolts through the horizontal leg and the flange
                foreach (double along in fd.BoltAlong)
                {
                    var head = P0 + tdir * along + wdir * fd.BoltGage + Up * ta;
                    double sh = sB + sa * along + ca * fd.BoltGage;
                    double outH = tw / 2 + ca * along - sa * fd.BoltGage;
                    _holes[gi].Bottom.Add(new Hole(new Vec2(sh, o * outH), fd.HoleDiameter));
                    AddCapBolt(head, new Vec3(0, 0, -1), tdir, ta + tbf, asm, fd.BoltDiameter);
                }
            }
    }
}
