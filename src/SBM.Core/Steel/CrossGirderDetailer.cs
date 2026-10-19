using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

public sealed partial class SteelModelGenerator
{
    // ================================================================== cross girders (discontinuous girders)

    /// <summary>Vertical shift of the top-of-web chord (left to right continuing girder) that places the top of the cross girder
    /// top flange below the discontinuous girder's top of web (0 when the definition leaves it flush), and the fraction of the
    /// chord where the discontinuous girder crosses.</summary>
    private (double dz, double frac) CrossGirderShift(CrossGirderResult x)
    {
        var xg = _def.Girders.CrossGirder;
        double frac = Vec2.Dot(x.Center.XY - x.Left.XY, (x.Right.XY - x.Left.XY)) / Math.Max(1e-12, (x.Right.XY - x.Left.XY).LengthSquared);
        if (xg.TopBelowDiscontinuousWebTop is not double off) return (0, frac);
        double chordZ = x.Left.Z + (x.Right.Z - x.Left.Z) * frac;
        return (x.Center.Z - off - chordZ, frac);
    }

    /// <summary>Top of the cross girder top flange at the left and right continuing girder web CLs: the top-of-web chord shifted by
    /// <see cref="CrossGirderShift"/> (legacy, sloped) or, with <c>CrossGirderSection.Level</c>, both at the elevation at the discontinuous girder.</summary>
    private (Vec3 a, Vec3 b) CrossGirderChord(CrossGirderResult x)
    {
        var (dz, frac) = CrossGirderShift(x);
        var a = new Vec3(x.Left.X, x.Left.Y, x.Left.Z + dz);
        var b = new Vec3(x.Right.X, x.Right.Y, x.Right.Z + dz);
        if (_def.Girders.CrossGirder.Level != true) return (a, b);
        double z = a.Z + (b.Z - a.Z) * frac;
        return (new Vec3(a.X, a.Y, z), new Vec3(b.X, b.Y, z));
    }

    /// <summary>
    /// Built-up cross girder between the two continuing girders, connected to their webs with pairs of angles (L5x5x1/2);
    /// the discontinuous girder frames into its web with angles. Runs in the CONNECTIONS phase: the angle bolts register
    /// holes in the continuing girders' webs and in the discontinuous girder's web.
    ///
    /// Frame f: x along the cross girder (left girder web CL at x = 0), y across (−y = near side = back station, the side
    /// the discontinuous girder comes from; +y = far side), z up; z = 0 at the top of the top flange.
    /// The angle legs are modelled square to the cross girder web; the girder webs are treated as perpendicular to it when
    /// locating holes (the plan angles are 85°-90°, so a leg may stand off a skewed web by up to ~0.3" at its toe).
    /// </summary>
    private void BuildCrossGirder(CrossGirderResult x)
    {
        var xg = _def.Girders.CrossGirder;
        string mark = x.Mark;
        _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.CrossFrame, Girder = x.Girder,
            Description = $"Cross girder G{x.LeftGirder + 1}–G{x.RightGirder + 1} carrying discontinuous G{x.Girder + 1}" });
        double twL = _web[x.LeftGirder].Thickness(x.LeftGirderDistance), twR = _web[x.RightGirder].Thickness(x.RightGirderDistance);
        double gap = xg.EndGap;
        var (a, b) = CrossGirderChord(x);                                     // top of the top flange at the two continuing girder CLs
        double Ltot = (b - a).Length;
        var dirPlan = (b.XY - a.XY).Normalized();
        double tcFrac = Vec2.Dot(x.Center.XY - a.XY, dirPlan) / Math.Max(1e-9, (b.XY - a.XY).Length);
        double xd = tcFrac * Ltot;                                            // discontinuous girder CL along the cross girder
        var f = Frame3.FromXZ(a, b - a, Up);
        double D = xg.WebDepth, tw = xg.WebThickness, bf = xg.FlangeWidth, tf = xg.FlangeThickness;
        double x0 = twL / 2 + gap, x1 = Ltot - twR / 2 - gap;
        double zWebTop = -tf, zWebBot = -tf - D;
        double leg = xg.AngleLeg, ta = xg.AngleThickness;
        double angleLen = xg.AngleLength > 0 ? xg.AngleLength : D - Inch(4);
        var angleSec = SteelCatalog.FindAngle($"L{leg / 0.0254:0.##}x{leg / 0.0254:0.##}x{(Math.Abs(ta - 0.5 * 0.0254) < 1e-6 ? "1/2" : (ta / 0.0254).ToString("0.###"))}");
        double rootR = angleSec?.RootRadius ?? 0;
        string angleName = angleSec?.Name ?? $"L{In(leg)}x{In(leg)}x{In(ta)}";
        var bg = xg.BoltDiameter > 0 ? new BoltGeometry(xg.BoltDiameter) : _model.Bolt;
        double hd = bg.HoleDiameter;
        bool bolted = xg.AngleBoltPitch > 0;

        // Bolt rows along an angle of length len whose lower end is at zLow (cross girder z).
        List<double> Rows(double zLow, double len)
        {
            var rows = new List<double>();
            if (!bolted) return rows;
            int n = (int)Math.Floor((len - 2 * xg.AngleBoltEndDistance) / xg.AngleBoltPitch + 1e-6) + 1;
            for (int i = 0; i < n; i++) rows.Add(zLow + xg.AngleBoltEndDistance + i * xg.AngleBoltPitch);
            return rows;
        }

        var webHolesXG = new List<Hole>();
        var angleParts = new List<(string code, Mesh mesh, string desc, double len, MemberRole role)>();
        // z of the lower end of the angles, centred in the web between the flange faces.
        double zLowC = zWebBot + (D - angleLen) / 2;
        var rowsC = Rows(zLowC, angleLen);
        Vec3 W(double xx, double yy, double zz) => f.ToWorld(xx, yy, zz);

        // Hole in a continuing/discontinuous girder web for a bolt axis through world point P.
        void GirderWebHole(int girder, double sRef, Vec3 p)
        {
            var path = _f.Girders[girder].Path;
            var (pr, tr, _) = path.At(sRef);
            double s = sRef + Vec2.Dot(p.XY - pr.XY, tr);
            double z = p.Z - path.At(s).point.Z;
            _holes[girder].Web.Add(new Hole(new Vec2(s, z), hd));
        }

        // Girder web faces in the cross girder's plan frame: the connection angle leg on a girder web lies IN that web (the plan angle between
        // the girder and the cross girder is 85-90 deg, orientation plan sheet 121): heel = where the girder web face meets the cross girder
        // web face, girder leg along the girder, cross girder leg along the cross girder (the angle is opened / closed to the skew).
        var ex = new Vec2(f.X.X, f.X.Y).Normalized(); var ey = new Vec2(f.Y.X, f.Y.Y).Normalized();
        var a2 = a.XY;
        (Vec2 c, Vec2 t) Local(Vec2 p, Vec2 tw2) => (new Vec2(Vec2.Dot(p - a2, ex), Vec2.Dot(p - a2, ey)), new Vec2(Vec2.Dot(tw2, ex), Vec2.Dot(tw2, ey)).Normalized());
        // heel x on the cross girder face y = ySide, girder-web leg direction (local, toward the angle's side), face normal (local, toward the angle)
        (double xHeel, Vec2 legB, Vec2 nFace) Heel(Vec2 cl, Vec2 tl, double twg, double towardX, double ySide)
        {
            var nl = new Vec2(-tl.Y, tl.X);
            if (nl.X * towardX < 0) nl = -nl;
            var fp = cl + nl * (twg / 2);
            double u = Math.Abs(tl.Y) < 1e-9 ? 0 : (ySide - fp.Y) / tl.Y;
            var lb = tl * Math.Sign(tl.Y * ySide == 0 ? 1 : tl.Y * ySide);
            return (fp.X + tl.X * u, lb, nl);
        }
        Vec3 L3(Vec2 d) => (f.X * d.X + f.Y * d.Y);

        // ---- connection angles at the two continuing girders (near and far faces of the cross girder web)
        int an = 0;
        foreach (var (sgn, girder, sRef, twg) in new[] { (1.0, x.LeftGirder, x.LeftGirderDistance, twL), (-1.0, x.RightGirder, x.RightGirderDistance, twR) })
        {
            var (pg, tg, _) = _f.Girders[girder].Path.At(sRef);
            var (cl, tl) = Local(pg.XY, tg);
            cl = new Vec2(cl.X, cl.Y);
            foreach (double side in new[] { -1.0, 1.0 })
            {
                var (xw, lb, nl) = Heel(cl, tl, twg, sgn, side * tw / 2);
                var dirB = L3(lb); var nW = L3(nl);
                var m = new Mesh();
                var holesA = new List<Hole>(); var holesB = new List<Hole>();
                var heel0 = W(xw, side * tw / 2, 0);
                foreach (double z in rowsC)
                {
                    holesA.Add(new Hole(new Vec2(z - zLowC, xg.AngleBoltGage), hd));
                    holesB.Add(new Hole(new Vec2(z - zLowC, xg.AngleBoltGage), hd));
                    if (side < 0)
                    {
                        // Bolt through both angles' XG-web legs and the web (head on the near face).
                        double gripA = 2 * ta + tw;
                        _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(W(xw + sgn * xg.AngleBoltGage, -(tw / 2 + ta), z), f.X, f.Y), Grip = gripA,
                            Length = bg.LengthForGrip(gripA), Assembly = mark, Diameter = bg.Diameter });
                        webHolesXG.Add(new Hole(new Vec2(xw + sgn * xg.AngleBoltGage, z - 0), hd));
                    }
                    // Bolt through this angle's girder-web leg and the girder web (head on the angle).
                    double gripB = ta + twg;
                    var pb = heel0 + dirB * xg.AngleBoltGage + f.Z * z;
                    _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(pb + nW * ta, dirB, -nW), Grip = gripB,
                        Length = bg.LengthForGrip(gripB), Assembly = mark, Diameter = bg.Diameter });
                    GirderWebHole(girder, sRef, pb);
                }
                RolledBuilder.AddAngle(m, W(xw, side * tw / 2, zLowC), f.Z, f.X * sgn, dirB, angleLen, leg, leg, ta, rootR, holesA, holesB);
                angleParts.Add(($"A{++an}", m, $"{angleName} x {In(angleLen)}", angleLen, MemberRole.Diaphragm));
            }
        }

        // ---- discontinuous girder end: angles on both sides of its web, bolted to the near face of the cross girder web
        var g = _f.Girders[x.Girder];
        double twg0 = _web[x.Girder].Thickness(g.Length);
        double endLen = xg.EndAngleLength > 0 ? xg.EndAngleLength : angleLen;
        if (xg.EndAngleLength > 0 || bolted)
        {
            double zLowE = zWebBot + (D - endLen) / 2;
            var rowsE = Rows(zLowE, endLen);
            var pathG = g.Path;
            var (pe, te, _) = pathG.At(g.Length);
            var (ce, tle) = Local(pe.XY, te);
            var heels = new Dictionary<double, (double xh, Vec2 lb, Vec2 nl)>();
            foreach (double sgn in new[] { -1.0, 1.0 }) heels[sgn] = Heel(ce, tle, twg0, sgn, -tw / 2);
            foreach (double sgn in new[] { -1.0, 1.0 })
            {
                var m = new Mesh();
                var holesA = new List<Hole>(); var holesB = new List<Hole>();
                var (xh, lb, nl) = heels[sgn];
                var dirB = L3(lb); var nW = L3(nl);
                foreach (double z in rowsE)
                {
                    holesA.Add(new Hole(new Vec2(z - zLowE, xg.AngleBoltGage), hd));
                    holesB.Add(new Hole(new Vec2(z - zLowE, xg.AngleBoltGage), hd));
                    // XG-web leg bolt: head on the angle, through the cross girder web, nut on the far face.
                    double gripA = ta + tw;
                    _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(W(xh + sgn * xg.AngleBoltGage, -(tw / 2 + ta), z), f.X, f.Y), Grip = gripA,
                        Length = bg.LengthForGrip(gripA), Assembly = mark, Diameter = bg.Diameter });
                    webHolesXG.Add(new Hole(new Vec2(xh + sgn * xg.AngleBoltGage, z), hd));
                    // Girder-web bolt: one bolt through both angles' legs and the girder web (placed once, from the +side angle).
                    if (sgn > 0)
                    {
                        double gripB = 2 * ta + twg0;
                        var pb = W(xh, -tw / 2, z) + dirB * xg.AngleBoltGage;
                        _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(pb + nW * ta, dirB, -nW), Grip = gripB,
                            Length = bg.LengthForGrip(gripB), Assembly = mark, Diameter = bg.Diameter });
                        GirderWebHole(x.Girder, g.Length, pb - nW * (twg0 / 2));
                    }
                }
                RolledBuilder.AddAngle(m, W(xh, -tw / 2, zLowE), f.Z, f.X * sgn, dirB, endLen, leg, leg, ta, rootR, holesA, holesB);
                angleParts.Add(($"E{(sgn > 0 ? 2 : 1)}", m, $"{angleName} x {In(endLen)} (end)", endLen, MemberRole.Diaphragm));
            }
        }
        else
        {
            // Legacy: angles on both sides of the discontinuous girder web, no bolts.
            var (pe, te, _) = g.Path.At(g.Length);
            var fe = Frame3.FromXZ(pe, new Vec3(te.X, te.Y, 0), Up);
            foreach (double side in new[] { -1.0, 1.0 })
            {
                var m = new Mesh();
                double zb = -_def.Girders.Section.WebDepth + Inch(4), zt = -Inch(4);
                MeshBuilder.AddBox(m, fe, -leg, gap, side > 0 ? twg0 / 2 : -twg0 / 2 - ta, side > 0 ? twg0 / 2 + ta : -twg0 / 2, zb, zt);
                MeshBuilder.AddBox(m, fe, gap - ta, gap, side > 0 ? twg0 / 2 : -twg0 / 2 - leg, side > 0 ? twg0 / 2 + leg : -twg0 / 2, zb, zt);
                angleParts.Add(($"E{(side > 0 ? 2 : 1)}", m, $"{angleName} end connection angle", zt - zb, MemberRole.Diaphragm));
            }
        }

        // ---- web opening beside the discontinuous girder (one each side), its edge plates and the flanking stiffeners
        var openings = new List<Vec2[]>();
        var extra = new List<(string code, Mesh mesh, string desc, double len, PartCategory cat)>();
        var welds = new Mesh(); bool anyWeld = false; double weldLen = 0;
        bool hasOpening = xg.OpeningWidth > 0 && xg.OpeningHeight > 0;
        if (hasOpening)
        {
            double zTopPlate = -xg.OpeningPlateFromTop;                         // z of the upper edge plate CL
            double zTopEdge = zTopPlate - xg.OpeningPlateToOpening;
            double zBotEdge = zTopEdge - xg.OpeningHeight;
            double zBotPlate = zBotEdge - xg.OpeningPlateToOpening;
            double zMid = (zTopEdge + zBotEdge) / 2;
            var near = new List<(double xc, double xlo, double xhi)>();
            // centres midway between each continuing girder CL and the discontinuous girder CL; bounds = angle toes
            near.Add(((0 + xd) / 2, twL / 2 + leg, xd - twg0 / 2 - leg));
            near.Add(((xd + Ltot) / 2, xd + twg0 / 2 + leg, Ltot - twR / 2 - leg));
            int ip = 0, istf = 0;
            foreach (var (xc, xlo, xhi) in near)
            {
                if (xc - xg.OpeningWidth / 2 < xlo || xc + xg.OpeningWidth / 2 > xhi)
                { Warn($"{mark}: the web opening does not fit between the connection angles ({In(xhi - xlo)} available)."); continue; }
                openings.Add(PlateBuilder.RoundedRectangle(new Vec2(xc, zMid), xg.OpeningWidth, xg.OpeningHeight, xg.OpeningRadius));
                if (xg.OpeningPlateWidth > 0 && xg.OpeningPlateThickness > 0)
                {
                    double pxl = xlo + xg.OpeningPlateEndClear, pxh = xhi - xg.OpeningPlateEndClear;
                    if (pxh > pxl)
                        foreach (double zc in new[] { zTopPlate, zBotPlate })
                        {
                            var m = new Mesh();
                            MeshBuilder.AddBox(m, f, pxl, pxh, tw / 2, tw / 2 + xg.OpeningPlateWidth, zc - xg.OpeningPlateThickness / 2, zc + xg.OpeningPlateThickness / 2);
                            extra.Add(($"P{++ip}", m, $"{Pl(xg.OpeningPlateWidth, xg.OpeningPlateThickness)} x {In(pxh - pxl)} (F.S.)", pxh - pxl, PartCategory.IntermediateStiffener));
                        }
                }
                if (xg.StiffenerWidth > 0 && xg.StiffenerThickness > 0)
                    foreach (double sgn in new[] { -1.0, 1.0 })
                    {
                        double xs = xc + sgn * (xg.OpeningWidth / 2 + xg.StiffenerFromOpening), th = xg.StiffenerThickness;
                        if (xs - th / 2 < xlo || xs + th / 2 > xhi)
                            Warn($"{mark}: a stiffener at {In(xs)} conflicts with the connection angles (the plans allow moving it up to 1 1/2\" horizontally).");
                        if (xg.StiffenerWidth + tw / 2 > bf / 2 + Inch(0.5))
                            Warn($"{mark}: stiffener {Pl(xg.StiffenerWidth, th)} projects beyond the {In(bf)} flange edge.");
                        var m = new Mesh();
                        MeshBuilder.AddBox(m, f, xs - th / 2, xs + th / 2, -tw / 2 - xg.StiffenerWidth, -tw / 2, zWebBot, zWebTop);
                        extra.Add(($"S{++istf}", m, $"{Pl(xg.StiffenerWidth, th)} x {In(D)} (N.S.)", D, PartCategory.IntermediateStiffener));
                        if (xg.StiffenerWeld > 0)
                        {
                            foreach (double e in new[] { -1.0, 1.0 })
                                weldLen += welds.AddHeld(W(xs + e * th / 2, -tw / 2, zWebTop), W(xs + e * th / 2, -tw / 2, zWebBot), f.X * e, -f.Y, xg.StiffenerWeld,
                                    HoldBack(xg.StiffenerWeld), HoldBack(xg.StiffenerWeld));   // short of the flanges (plate corners)
                            anyWeld = true;
                        }
                    }
            }
        }

        // ---- plates: web (holes + openings), flanges
        var web = new Mesh();
        var fw = new Frame3(f.Origin, f.X, f.Z, -f.Y);
        var outline = new[] { new Vec2(x0, zWebBot), new Vec2(x1, zWebBot), new Vec2(x1, zWebTop), new Vec2(x0, zWebTop) };
        if (openings.Count > 0) PlateBuilder.AddPlanarWithOpenings(web, outline, webHolesXG, openings, fw, -tw / 2, tw / 2);
        else PlateBuilder.AddPlanar(web, outline, webHolesXG, fw, -tw / 2, tw / 2);
        var top = new Mesh(); MeshBuilder.AddBox(top, f, x0, x1, -bf / 2, bf / 2, -tf, 0);
        var bot = new Mesh(); MeshBuilder.AddBox(bot, f, x0, x1, -bf / 2, bf / 2, -2 * tf - D, -tf - D);
        double len = x1 - x0;
        string webDesc = $"Cross girder web {Pl(D, tw)}" + (openings.Count > 0 ? $", {openings.Count} openings {In(xg.OpeningWidth)} x {In(xg.OpeningHeight)}" : "")
                         + (webHolesXG.Count > 0 ? $", {webHolesXG.Count} holes {In(hd)}" : "");
        _parts.Add(new SteelPart { Mark = $"{mark}-W", Category = PartCategory.WebPlate, Assembly = mark, Mesh = web, Length = len, Description = webDesc });
        _parts.Add(new SteelPart { Mark = $"{mark}-TF", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = top, Length = len, Description = $"Cross girder flange {Pl(bf, tf)}" });
        _parts.Add(new SteelPart { Mark = $"{mark}-BF", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = bot, Length = len, Description = $"Cross girder flange {Pl(bf, tf)}" });
        if (xg.WebFlangeWeld > 0)
        {
            foreach (double sd in new[] { -1.0, 1.0 })
            {
                double hw = HoldBack(xg.WebFlangeWeld);           // short of the plate ends
                weldLen += welds.AddHeld(W(x0, sd * tw / 2, -tf), W(x1, sd * tw / 2, -tf), f.Y * sd, -f.Z, xg.WebFlangeWeld, hw, hw);
                weldLen += welds.AddHeld(W(x0, sd * tw / 2, zWebBot), W(x1, sd * tw / 2, zWebBot), f.Y * sd, f.Z, xg.WebFlangeWeld, hw, hw);
            }
            anyWeld = true;
        }
        if (anyWeld)
            _parts.Add(new SteelPart { Mark = $"{mark}-WLD", Category = PartCategory.Weld, Assembly = mark, Mesh = welds, Material = "E70XX", Length = weldLen,
                Description = $"Fillet weld {In(Math.Max(xg.WebFlangeWeld, xg.StiffenerWeld))}, cross girder{HoldNote(Math.Max(xg.WebFlangeWeld, xg.StiffenerWeld))}",
                Assumption = WeldFlag(Math.Max(xg.WebFlangeWeld, xg.StiffenerWeld)) });
        foreach (var (code, m, desc, l, role) in angleParts)
            _parts.Add(new SteelPart { Mark = $"{mark}-{code}", Category = PartCategory.Angle, Assembly = mark, Mesh = m, Length = l, Description = desc, Role = role,
                NominalWeightLbPerFt = angleSec?.WeightPerFootLb ?? 0 });
        foreach (var (code, m, desc, l, cat) in extra)
            _parts.Add(new SteelPart { Mark = $"{mark}-{code}", Category = cat, Assembly = mark, Mesh = m, Length = l, Description = desc });
    }
}
