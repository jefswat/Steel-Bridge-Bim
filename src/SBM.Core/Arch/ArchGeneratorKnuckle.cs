using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Arch;

public static partial class ArchGenerator
{
    private sealed partial class Gen
    {
        /// <summary>Knuckle end-line frame: end points, unit direction, normal toward the span, chord crossing.</summary>
        private (Vec2 a, Vec2 b, Vec2 t, Vec2 n, Vec2 ctr)? EndLine(ArchKnuckleDefinition k)
        {
            if (!_pts.TryGetValue(k.EndFrom, out var a) || !_pts.TryGetValue(k.EndTo, out var b)) return null;
            var t = (b.XY - a.XY).Normalized();
            var n = t.Left;
            if (Vec2.Dot(n, -a.XY) < 0) n = -n;
            double sy = Math.Abs(b.Y - a.Y) > 1e-9 ? -a.Y / (b.Y - a.Y) : 0.5;
            return (a.XY, b.XY, t, n, Vec2.Lerp(a.XY, b.XY, sy));
        }

        /// <summary>Tie trim at a knuckle with a v2 detail: the tie field piece starts / ends at the joint = where the tie CL crosses the end flange inner
        /// edge (p.124: end flange 2" x 32" from the end web outside face), plus half the joint gap. Null = no knuckle detail at that end.</summary>
        private double? KnuckleJoint(ArchTieDefinition t, TieGeo geo, bool atStart)
        {
            string pt = atStart ? t.Start : t.End;
            foreach (var k in _d.Knuckles)
            {
                if (k.Detail == null || !k.Corners.Any(c => string.Equals(c.Point, pt, StringComparison.OrdinalIgnoreCase))) continue;
                if (EndLine(k) is not { } el) continue;
                double edge = k.EndFlangeWidth - k.EndWebOffset;
                double F(double s) => Vec2.Dot(geo.At(atStart ? s : geo.Length - s).p - el.a, el.n) - edge;
                double lo = 0, hi = Ft(10);
                if (Math.Sign(F(lo)) == Math.Sign(F(hi))) continue;
                for (int i = 0; i < 80; i++) { double m = (lo + hi) / 2; if (Math.Sign(F(m)) == Math.Sign(F(lo))) lo = m; else hi = m; }
                return (lo + hi) / 2;
            }
            return null;
        }

        private static List<Vec2> Fillet(List<Vec2> poly, int i, double r, int seg = 6)
        {
            var p = poly[(i - 1 + poly.Count) % poly.Count]; var v = poly[i]; var q = poly[(i + 1) % poly.Count];
            var d1 = (p - v).Normalized(); var d2 = (q - v).Normalized();
            double cos = Math.Clamp(Vec2.Dot(d1, d2), -1, 1), ang = Math.Acos(cos);
            if (ang < 1e-3 || ang > Math.PI - 1e-3) return poly;
            double tl = r / Math.Tan(ang / 2);
            if (tl > (p - v).Length * 0.9 || tl > (q - v).Length * 0.9) return poly;
            var a = v + d1 * tl; var b = v + d2 * tl;
            var bis = (d1 + d2).Normalized();
            var c = v + bis * (r / Math.Sin(ang / 2));
            double a0 = Math.Atan2(a.Y - c.Y, a.X - c.X), a1 = Math.Atan2(b.Y - c.Y, b.X - c.X);
            double da = a1 - a0; while (da > Math.PI) da -= 2 * Math.PI; while (da < -Math.PI) da += 2 * Math.PI;
            var arc = Enumerable.Range(0, seg + 1).Select(k => c + new Vec2(Math.Cos(a0 + da * k / seg), Math.Sin(a0 + da * k / seg)) * r).ToList();
            var res = new List<Vec2>(poly);
            res.RemoveAt(i);
            res.InsertRange(i, arc);
            return res;
        }

        private static List<Vec2> Ccw(List<Vec2> l) { if (MeshBuilder.SignedArea(l) < 0) l.Reverse(); return l; }

        /// <summary>v2 knuckle (p.123-126): end flange, end web with PT openings and studs, deck ("end") plate on the end / tie / floorbeam flanges with
        /// studs, and per rib-to-tie corner the bent arch flange plates, stub web, stiffeners, tie joint gussets + bolts, fill / splice and jack cheese plates.</summary>
        private void BuildKnuckleV2(ArchKnuckleDefinition k, ArchKnuckleDetail kd)
        {
            if (EndLine(k) is not { } el) return;
            string asm = $"KNUCKLE-{k.Name}".ToUpperInvariant().Replace(' ', '-');
            AddAssembly(asm, AssemblyKind.ArchKnuckle, $"Knuckle {k.Name} (p.123-126): end flange, end web, end plate, arch flange plates, stiffeners, tie joints");
            var org = W(el.ctr.WithZ(0));
            var et = Wd(el.t.WithZ(0)); var en = Wd(el.n.WithZ(0));
            double half = k.EndLength / 2, o0 = -k.EndWebOffset, zef = k.EndFlangeBottomZ + k.EndFlangeThickness;
            // End flange.
            var fl = new Mesh();
            var cuts = !double.IsNaN(kd.SpliceHalfWidth) && k.Corners.Count == 2 ? k.Corners.Select(CornerChord).ToList() : null;
            if (cuts != null && cuts.All(x => x != null))
            {
                // p.124 partial plan: the end flange's outer ends are cut PARALLEL TO THE CHORD, flush with the splice plate side (+SpliceHalfWidth outward
                // of the T point), not square to the end line.
                var pts = new List<Vec2>();
                double cnA = 0;
                foreach (var (dn, first) in new[] { (o0, true), (o0 + k.EndFlangeWidth, false) })
                    foreach (var cc in first ? cuts : Enumerable.Reverse(cuts))
                    {
                        var (t0, c2, o2) = cc!.Value;
                        var q = t0 + o2 * kd.SpliceHalfWidth;                 // a point of the cut line (parallel to c2)
                        double cn = Vec2.Dot(c2, el.n); cnA = cn;
                        pts.Add(q + c2 * ((dn - Vec2.Dot(q - el.a, el.n)) / cn));
                    }
                var poly = Ccw(pts);
                PlateBuilder.AddPrism(fl, poly, Array.Empty<Hole>(), (x, y, z) => W(new Vec3(x, y, z)), _ => k.EndFlangeBottomZ, _ => zef);
                double lenW = Vec2.Distance(pts[0], pts[1]), lenE = Vec2.Distance(pts[2], pts[3]);
                AddPart($"{asm}-EF", PartCategory.KnucklePlate, asm, $"PL {L(k.EndFlangeThickness)} x {L(k.EndFlangeWidth)} end flange, {L(lenW)} at the end web / {L(lenE)} at the edge, ends cut parallel to the chords {L(kd.SpliceHalfWidth)} outside the T points (p.124 plan)", fl, k.Material, Math.Max(lenW, lenE));
                Info($"Arch knuckle {k.Name}: end flange ends parallel to the chords (p.124 plan, flush with the splice plate sides) instead of {L(k.EndLength)} square to the end line.");
            }
            else
            {
            RolledBuilder.AddExtrusion(fl, new[] { new Vec2(o0, k.EndFlangeBottomZ), new Vec2(o0 + k.EndFlangeWidth, k.EndFlangeBottomZ), new Vec2(o0 + k.EndFlangeWidth, zef), new Vec2(o0, zef) },
                org, en, _fr.Z, et, -half, half);
            AddPart($"{asm}-EF", PartCategory.KnucklePlate, asm, $"PL {L(k.EndFlangeThickness)} x {L(k.EndFlangeWidth)} x {L(k.EndLength)} end flange (p.124)", fl, k.Material, k.EndLength);
            }
            // End web with PT openings (obround) and studs on the inside face.
            double hEnd = !double.IsNaN(k.EndWebHeightAtEnds) ? k.EndWebHeightAtEnds : k.EndWebScaledHeightAtEnds;
            if (!double.IsNaN(hEnd))
            {
                var poly = Ccw(new List<Vec2> { new(-half, zef), new(half, zef), new(half, zef + hEnd), new(0, zef + k.EndWebHeightAtChord), new(-half, zef + hEnd) });
                var ops = k.EndWebOpeningsAt.Select(x => PlateBuilder.RoundedRectangle(new Vec2(x, zef + k.EndWebOpeningZ), k.EndWebOpeningLength, k.EndWebOpeningHeight, k.EndWebOpeningHeight * 0.45, 8)).ToList();
                var wf = new Frame3(org, et, _fr.Z, en);
                var web = new Mesh();
                if (ops.Count > 0) PlateBuilder.AddPlanarWithOpenings(web, poly, Array.Empty<Hole>(), ops, wf, o0, o0 + k.EndWebThickness);
                else PlateBuilder.AddPlanar(web, poly, Array.Empty<Hole>(), wf, o0, o0 + k.EndWebThickness);
                var asu = double.IsNaN(k.EndWebHeightAtEnds) && k.EndWebUnresolved.Length > 0 ? Assumption.UnresolvedDimension(k.EndWebUnresolved, $"end height {L(hEnd)} scaled") : null;
                AddPart($"{asm}-EW", PartCategory.KnucklePlate, asm, $"PL {L(k.EndWebThickness)} x {L(k.EndLength)} end web{(ops.Count > 0 ? $", {ops.Count} PT openings {L(k.EndWebOpeningLength)} x {L(k.EndWebOpeningHeight)}" : "")} (p.126)",
                    web, k.Material, k.EndLength, 0, asu);
                foreach (double x in k.EndWebStudsAt)
                {
                    var o = wf.ToWorld(x, zef + k.EndWebStudZ, o0 + k.EndWebThickness);
                    Model.Steel.Studs.Add(new StudInstance { Frame = new Frame3(o, et, _fr.Z, en), Length = k.EndWebStudLength, Assembly = asm + "-EP" });
                }
            }
            else Warn(k.EndWebUnresolved);
            // Deck ("end") plate: on the end flange (overlap), the tie flanges and the first floorbeam flange; bottom on the flange tops.
            BuildEndPlate(k, el, asm, zef);
            foreach (var c in k.Corners) KnuckleCorner(k, kd, c, el, asm, zef);
            foreach (var pt in k.PtAnchors) Model.Tendons.Add(($"{k.Name} PT anchor {pt.Name}", new List<Vec3> { W(pt.Vec) }, "PT anchorage (data)"));
        }

        private void BuildEndPlate(ArchKnuckleDefinition k, (Vec2 a, Vec2 b, Vec2 t, Vec2 n, Vec2 ctr) el, string asm, double zef)
        {
            if (k.DeckPlate.Count != 4 || k.DeckPlate.Any(p => P(p) == null)) return;
            var q = k.DeckPlate.Select(p => P(p)!.Value.XY).ToList();   // [end from, tie point, tie point, end to]
            // Lines: q0-q1 (tie), q1-q2 (floorbeam), q2-q3 (tie), end-flange line; offsets inward.
            (Vec2 p, Vec2 d) Off(Vec2 p0, Vec2 p1, double off)
            {
                var d = (p1 - p0).Normalized(); var nn = d.Left;
                var cen = (q[0] + q[1] + q[2] + q[3]) * 0.25;
                if (Vec2.Dot(cen - p0, nn) < 0) nn = -nn;
                return (p0 + nn * off, d);
            }
            Vec2 X((Vec2 p, Vec2 d) l1, (Vec2 p, Vec2 d) l2)
            {
                double den = Vec2.Cross(l1.d, l2.d);
                double s = Vec2.Cross(l2.p - l1.p, l2.d) / den;
                return l1.p + l1.d * s;
            }
            double eTie = double.IsNaN(k.DeckPlateEdgeFromTieCL) ? 0 : k.DeckPlateEdgeFromTieCL;
            double eFb = double.IsNaN(k.DeckPlateEdgeFromFloorbeamCL) ? 0 : k.DeckPlateEdgeFromFloorbeamCL;
            double eEnd = (k.EndFlangeWidth - k.EndWebOffset) - (double.IsNaN(k.DeckPlateOverEndFlange) ? 0 : k.DeckPlateOverEndFlange);
            var lT1 = Off(q[0], q[1], eTie); var lFb = Off(q[1], q[2], eFb); var lT2 = Off(q[2], q[3], eTie);
            var lEnd = (el.a + el.n * eEnd, el.t);
            var outline = Ccw(new List<Vec2> { X(lEnd, lT1), X(lT1, lFb), X(lFb, lT2), X(lT2, lEnd) });
            double zb = zef, zt = zef + k.DeckPlateThickness;
            var dp = new Mesh();
            MeshBuilder.AddExtrusion(dp, outline, _fr, zb, zt);
            string ep = asm + "-EP";
            AddAssembly(ep, AssemblyKind.ArchKnuckle, $"Knuckle {k.Name} end plate (p.126): 1\" deck plate and shear studs (floor system quantity, p.143 note 2)");
            AddPart($"{ep}-DP", PartCategory.KnucklePlate, ep, $"PL {L(k.DeckPlateThickness)} deck plate (END PLATE, p.126), on the end / tie / floorbeam flanges", dp, k.Material, 0);
            if (Math.Abs(zt - k.DeckPlateTopZ) > Tol) Warn($"Arch CHECK knuckle {k.Name}: deck plate top at {L(zt)} (on the flange tops, p.124 / 125 overlaps) vs the slab bottom input {L(k.DeckPlateTopZ)}.");
            // Studs: rows parallel to the end line; 8" rows that fall on the end flange start at its top, 7" rows on the deck plate.
            if (k.DeckStudRows.Count > 0 && k.DeckStudSpacing > 0)
            {
                // Clip region: the deck plate outline extended to the end web inside face, less the stud edge clearance.
                var region = Ccw(new List<Vec2> { X((el.a + el.n * (k.EndWebThickness - k.EndWebOffset), el.t), lT1), X(lT1, lFb), X(lFb, lT2), X(lT2, (el.a + el.n * (k.EndWebThickness - k.EndWebOffset), el.t)) });
                bool Inside(Vec2 p)
                {
                    for (int i = 0; i < region.Count; i++)
                    {
                        var a = region[i]; var b = region[(i + 1) % region.Count];
                        double dd = Vec2.Cross(b - a, p - a) / (b - a).Length;
                        if (dd < k.DeckStudEdge) return false;
                    }
                    return true;
                }
                int ns = 0;
                foreach (var row in k.DeckStudRows)
                {
                    double dn = row.X;
                    bool onPlate = dn > eEnd;
                    double z0 = onPlate ? zt : zef;
                    var p0 = el.ctr + el.n * dn;
                    for (int i = -60; i <= 60; i++)
                    {
                        var p = p0 + el.t * (i * k.DeckStudSpacing);
                        if (!Inside(p)) continue;
                        Model.Steel.Studs.Add(new StudInstance { Frame = new Frame3(W(p.WithZ(z0)), _fr.X, _fr.Y, _fr.Z), Length = row.Z, Assembly = ep });
                        ns++;
                    }
                }
                if (k.DeckStudUnresolved.Length > 0) Warn(k.DeckStudUnresolved);
                Info($"Arch: knuckle {k.Name} end plate: {ns} studs in {k.DeckStudRows.Count} rows.");
            }
        }

        private void KnuckleCorner(ArchKnuckleDefinition k, ArchKnuckleDetail kd, ArchKnuckleCorner c, (Vec2 a, Vec2 b, Vec2 t, Vec2 n, Vec2 ctr) el, string asm, double zef)
        {
            if (P(c.Point) is not { } T) return;
            var rr = _ribs.FirstOrDefault(r => string.Equals(r.Name, c.Rib, StringComparison.OrdinalIgnoreCase));
            var rdef = _d.Ribs.FirstOrDefault(r => string.Equals(r.Name, c.Rib, StringComparison.OrdinalIgnoreCase));
            if (rr == null || rdef == null || !_ties.TryGetValue(c.Tie, out var tg)) { Warn($"Arch: knuckle {k.Name} corner {c.Point}: rib / tie not built."); return; }
            var w = SteelCatalog.FindWideFlange(rdef.Section);
            if (w == null) return;
            bool atStart = (rr.PointAt(rr.ThetaStart) - T).Length < (rr.PointAt(rr.ThetaEnd) - T).Length;
            var cIn = atStart ? rr.ChordDir : -rr.ChordDir;
            double th = atStart ? rr.ThetaStart : rr.ThetaEnd;
            var dP = rr.ChordDir * (-Math.Sin(th)) + rr.Up * Math.Cos(th);   // dP/dtheta (unit)
            var tan = (atStart ? -dP : dP).Normalized();                      // up the rib into the span
            var up = rr.Up;
            var bn = Vec3.Cross(rr.ChordDir, rr.Up).Normalized();
            int sOut = Math.Sign(bn.Y * T.Y); if (sOut == 0) sOut = 1;
            var t2 = new Vec2(Vec3.Dot(tan, cIn), Vec3.Dot(tan, up)).Normalized();
            var n2 = t2.Left;                                                // toward the extrados (end side)
            double hw = kd.FlangeHalfWidth, tp = kd.FlangePlateThickness;
            // Rib splice above the stub (built-up stub ends half the splice gap short of it).
            double sCut = double.NaN;
            if (P(c.Splice) is { } rs) sCut = Vec3.Dot(rs - T, tan) - rdef.SpliceGap / 2;
            if (double.IsNaN(sCut) || sCut <= 0) { Warn($"Arch: knuckle {k.Name} corner {c.Point}: splice '{c.Splice}' not found - corner not built."); return; }
            string cm = $"{asm}-{c.Point.Replace("'", "P")}";
            var mat = kd.Material;
            // End web inside face in plan: distance from the end line toward the span.
            double dIn = -k.EndWebOffset + k.EndWebThickness;
            double Dn(Vec3 p) => Vec2.Dot(p.XY - el.a, el.n);
            // ---- bent arch flange plates (outside / inside)
            foreach (var (outside, h) in new[] { (true, c.OutsideLegHeight), (false, c.InsideLegHeight) })
            {
                double b = (outside ? sOut : -sOut) * (w.Depth / 2 - tp / 2);
                var o3 = T + bn * b;
                double vb = (zef - o3.Z) / up.Z;
                // u where the plate meets the end web inside face at the bottom edge.
                double d0 = Dn(o3 + up * vb), du = Vec2.Dot(cIn.XY, el.n);
                double uEnd = (dIn - d0) / du;
                double uI = (-hw - vb * n2.Y) / n2.X;
                double uE = (hw - (vb + h) * n2.Y) / n2.X;
                var p4 = t2 * sCut - n2 * hw; var p5 = t2 * sCut + n2 * hw;
                var poly = new List<Vec2> { new(uEnd, vb + kd.BottomClip), new(uEnd + kd.BottomClip, vb), new(uI, vb), p4, p5, new(uE, vb + h), new(uEnd, vb + h) };
                poly = Fillet(poly, 5, kd.BendRadius);
                poly = Fillet(poly, 2, kd.BendRadius);
                poly = Ccw(poly);
                var m = new Mesh();
                // Mill to bear (p.124 A-A / B-B): the bottom end of the plate lies in the end flange top plane (the plate leans with the rib, so a cut square
                // to the plate would leave a wedge gap against the flat flange).
                double vBot = vb;
                PlateBuilder.AddPrism(m, poly, Array.Empty<Hole>(), (u, v, t) =>
                    W(o3 + bn * t + cIn * u + up * (Math.Abs(v - vBot) < 1e-9 ? (zef - o3.Z - cIn.Z * u - bn.Z * t) / up.Z : v)), _ => -tp / 2, _ => tp / 2);
                AddPart($"{cm}-AF{(outside ? "O" : "I")}", PartCategory.KnucklePlate, asm,
                    $"PL {L(tp)} {(outside ? "outside" : "inside")} arch flange plate, bent (leg {L(h)}, R {L(kd.BendRadius)}) at {c.Point} (p.124/125)", m, mat, sCut + Math.Abs(uEnd));
            }
            // ---- stub web (between the flange plates, along the rib tangent, milled to bear on the end flange)
            {
                double hwW = w.Depth / 2 - tp;
                var nW = Vec3.Cross(tan, bn).Normalized();
                double Sb(double wv) => (zef - T.Z - wv * bn.Z) / tan.Z;
                var poly = Ccw(new List<Vec2> { new(Sb(-hwW), -hwW), new(sCut, -hwW), new(sCut, hwW), new(Sb(hwW), hwW) });
                var m = new Mesh();
                // Mill to bear on the end flange top: the bottom end is cut in that (horizontal) plane, not square to the web.
                PlateBuilder.AddPrism(m, poly, Array.Empty<Hole>(), (s, wv, t) =>
                    W(T + tan * (Math.Abs(s - sCut) > 1e-9 ? (zef - T.Z - bn.Z * wv - nW.Z * t) / tan.Z : s) + bn * wv + nW * t), _ => -kd.StubWebThickness / 2, _ => kd.StubWebThickness / 2);
                var asu = kd.StubWebUnresolved.Length > 0 ? Assumption.UnresolvedDimension(kd.StubWebUnresolved, $"PL {L(kd.StubWebThickness)}") : null;
                AddPart($"{cm}-SW", PartCategory.KnucklePlate, asm, $"PL {L(kd.StubWebThickness)} arch stub web at {c.Point}, milled to bear (p.124 A-A)", m, mat, sCut, 0, asu);
                // Arch stiffener: normal to the rib at ArchStiffenerAlongRib, intrados side, web face to the flange tip.
                var nI = -(cIn * n2.X + up * n2.Y).Normalized();
                var sp = Ccw(new List<Vec2> { new(-hwW, kd.StubWebThickness / 2), new(hwW, kd.StubWebThickness / 2), new(hwW, hw), new(-hwW, hw) });
                var ms = new Mesh();
                var o = T + tan * kd.ArchStiffenerAlongRib;
                PlateBuilder.AddPrism(ms, sp, Array.Empty<Hole>(), (wv, r, t) => W(o + bn * wv + nI * r + tan * t), _ => -kd.ArchStiffenerThickness / 2, _ => kd.ArchStiffenerThickness / 2);
                var asu2 = kd.ArchStiffenerUnresolved.Length > 0 ? Assumption.UnresolvedDimension(kd.ArchStiffenerUnresolved, "web face to the flange tip, intrados side") : null;
                AddPart($"{cm}-AS", PartCategory.KnucklePlate, asm, $"PL {L(kd.ArchStiffenerThickness)} arch stiffener at {c.Point} ({L(kd.ArchStiffenerAlongRib)} along the rib, p.124 A-A)", ms, mat, 2 * hwW, 0, asu2);
                // Vertical stiffener: normal to the chord, StiffenerFromSpringing outward of the springing, between the flange plates.
                var ov = T - cIn * kd.StiffenerFromSpringing;
                double vb0 = (zef - ov.Z) / up.Z;
                var vp = Ccw(new List<Vec2> { new(-hwW, vb0), new(hwW, vb0), new(hwW, vb0 + kd.StiffenerHeight / up.Z), new(-hwW, vb0 + kd.StiffenerHeight / up.Z) });
                var mv = new Mesh();
                double hand = Vec3.Dot(Vec3.Cross(bn, up), cIn) >= 0 ? 1 : -1;   // the (wv, v, t) map must be right-handed or the plate comes out inside out
                // Bottom on the end flange top and top level, both horizontal (the plate leans with the rib plane).
                PlateBuilder.AddPrism(mv, vp, Array.Empty<Hole>(), (wv, v, t) =>
                    W(ov + bn * wv + cIn * (hand * t) + up * ((zef + (v > vb0 + 1e-9 ? kd.StiffenerHeight : 0) - ov.Z - bn.Z * wv - cIn.Z * hand * t) / up.Z)), _ => -kd.StiffenerThickness / 2, _ => kd.StiffenerThickness / 2);
                AddPart($"{cm}-ST", PartCategory.KnucklePlate, asm, $"PL {L(kd.StiffenerThickness)} stiffener at {c.Point} ({L(kd.StiffenerFromSpringing)} outside the springing, {L(kd.StiffenerHeight)} high, p.124 B-B)", mv, mat, 2 * hwW);
            }
            // ---- tie joint: tie web stub (knuckle side), gussets + bolts, fill + splice under the tie flange, cheese plates at the jacks
            var tdef = tg.Def;
            bool tieAtStart = string.Equals(tdef.Start, c.Point, StringComparison.OrdinalIgnoreCase);
            double? sjN = KnuckleJoint(tdef, tg, tieAtStart);
            if (sjN is not { } sj) { Warn($"Arch: knuckle {k.Name} corner {c.Point}: the tie does not cross the end flange edge - joint not built."); return; }
            (Vec2 p, Vec2 dir, Vec2 left) TA(double s)
            {
                // Beyond the tie end (s < 0, toward the end web) the end tangent is extended.
                var (p, a) = tg.At(tieAtStart ? Math.Max(0, s) : tg.Length - Math.Max(0, s));
                var d = Vec2.FromAngle(a) * (tieAtStart ? 1 : -1);
                if (s < 0) p += d * s;
                return (p, d, d.Left);
            }
            Vec3 TieMap(double s, double z, double lat) { var (p, _, l) = TA(s); return W((p + l * lat).WithZ(z)); }
            double zt0 = tdef.FlangeThickness, zw = tdef.FlangeThickness + tdef.WebHeight, g2 = kd.JointGap / 2;
            {
                // Knuckle tie web from the stub web to the joint (trapezoid: the stub web leans at the rib angle).
                double cot = Vec3.Dot(tan, cIn) / tan.Z;
                double sw0 = zt0 * cot + kd.StubWebThickness / 2 / Math.Max(0.2, Math.Sqrt(1 - Math.Pow(Vec3.Dot(tan, cIn), 2)));
                double sw1 = zw * cot + (sw0 - zt0 * cot);
                var poly = Ccw(new List<Vec2> { new(sw0, zt0), new(sj - g2, zt0), new(sj - g2, zw), new(sw1, zw) });
                var m = new Mesh();
                PlateBuilder.AddPrism(m, poly, Array.Empty<Hole>(), (s, z, t) => TieMap(s, z, t), _ => -tdef.WebThickness / 2, _ => tdef.WebThickness / 2);
                AddPart($"{cm}-TW", PartCategory.KnucklePlate, asm, $"PL {L(tdef.WebThickness)} knuckle tie web at {c.Point} (to the tie joint at the end flange edge)", m, mat, sj);
            }
            {
                double lg = kd.GussetBoltFirst + (kd.GussetBoltColumns - 1) * kd.GussetBoltPitch + kd.GussetEdge;
                double gt = zw - kd.GussetTopBelowWeb, gb = gt - kd.GussetHeight;
                var holes = new List<Hole>();
                foreach (var rz in kd.GussetRows)
                    for (int i = 0; i < kd.GussetBoltColumns; i++)
                        foreach (int sg in new[] { -1, 1 }) holes.Add(new Hole(new Vec2(sj + sg * (kd.GussetBoltFirst + i * kd.GussetBoltPitch), gt - rz), Model.Steel.Bolt.HoleDiameter));
                var outline = ClippedRect(sj - lg, sj + lg, gb, gt, kd.GussetClip);
                var m = new Mesh();
                foreach (int sd in new[] { 1, -1 })
                    PlateBuilder.AddPrism(m, Ccw(outline), holes, (s, z, t) => TieMap(s, z, t), _ => sd > 0 ? tdef.WebThickness / 2 : -tdef.WebThickness / 2 - kd.GussetThickness,
                        _ => sd > 0 ? tdef.WebThickness / 2 + kd.GussetThickness : -tdef.WebThickness / 2);
                AddPart($"{cm}-GU", PartCategory.KnucklePlate, asm, $"2 - PL {L(kd.GussetThickness)} x {L(kd.GussetHeight)} x {L(2 * lg)} gusset plates at the tie joint {c.Point} (p.124/125 A-A)", m, mat, 2 * lg);
                double grip = 2 * kd.GussetThickness + tdef.WebThickness;
                foreach (var hl in holes)
                    Model.Steel.Bolts.Add(new BoltInstance { Frame = BoltFrame((s, z, t) => TieMap(s, z, t), hl.Center.X, hl.Center.Y, tdef.WebThickness / 2 + kd.GussetThickness, -1), Grip = grip, Length = Model.Steel.Bolt.LengthForGrip(grip), Assembly = asm });
            }
            {
                // Fill under the tie flange (tie side of the joint) and the 1" plate under the end flange that splices it to the tie, bearing / jack plates.
                double hwF = tdef.FlangeWidth / 2;
                var asu = kd.SpliceUnresolved.Length > 0 ? Assumption.UnresolvedDimension(kd.SpliceUnresolved, $"splice plate from the end web to {L(kd.SplicePlateLength / 2)} past the joint, {L(tdef.FlangeWidth)} wide; bolts not modeled") : null;
                if (!double.IsNaN(kd.SpliceHalfWidth) && !double.IsNaN(kd.SpliceBeyondJoint))
                    SpliceFromPlan(k, kd, c, el, T, cIn, cm, asm, mat, g2, asu);
                else
                {
                var mf = new Mesh();
                PlateBuilder.AddPrism(mf, Ccw(ClippedRect(sj + g2, sj + kd.SplicePlateLength / 2, -hwF, hwF, 0)), Array.Empty<Hole>(), (s, lat, z) => TieMap(s, z, lat), _ => -kd.FillThickness, _ => 0);
                AddPart($"{cm}-FI", PartCategory.KnucklePlate, asm, $"PL {L(kd.FillThickness)} fill under the tie flange at {c.Point} (p.124 A-A)", mf, mat, kd.SplicePlateLength / 2, 0, asu);
                // Splice / bearing plate: from the end web outside face along the tie to past the joint.
                double sStart = (-k.EndWebOffset - Vec2.Dot(TA(0).p - el.a, el.n)) / Math.Max(0.2, Vec2.Dot(TA(0).dir, el.n));
                var ms = new Mesh();
                PlateBuilder.AddPrism(ms, Ccw(ClippedRect(sStart, sj + kd.SplicePlateLength / 2, -hwF, hwF, 0)), Array.Empty<Hole>(), (s, lat, z) => TieMap(s, z, lat),
                    _ => k.EndFlangeBottomZ - kd.SplicePlateThickness, _ => k.EndFlangeBottomZ);
                AddPart($"{cm}-SP", PartCategory.KnucklePlate, asm, $"PL {L(kd.SplicePlateThickness)} splice / bearing plate under the end flange at {c.Point} (p.124 A-A)", ms, mat, sj + kd.SplicePlateLength / 2 - sStart, 0, asu);
                }
                // Jack cheese plates (CL bearing BearingFromSpringing into the span; jacks +- JackFromBearing along the chord).
                var asuC = kd.CheeseUnresolved.Length > 0 ? Assumption.UnresolvedDimension(kd.CheeseUnresolved, $"{L(kd.CheeseLength)} x {L(kd.CheeseWidth)} scaled") : null;
                var mc = new Mesh();
                foreach (int sg in new[] { -1, 1 })
                {
                    double s0 = kd.BearingFromSpringing + sg * kd.JackFromBearing;
                    PlateBuilder.AddPrism(mc, Ccw(ClippedRect(s0 - kd.CheeseLength / 2, s0 + kd.CheeseLength / 2, -kd.CheeseWidth / 2, kd.CheeseWidth / 2, 0)), Array.Empty<Hole>(),
                        (s, lat, z) => TieMap(s, z, lat), _ => k.EndFlangeBottomZ - kd.SplicePlateThickness - kd.CheeseThickness, _ => k.EndFlangeBottomZ - kd.SplicePlateThickness);
                }
                AddPart($"{cm}-CH", PartCategory.KnucklePlate, asm, $"2 - PL {L(kd.CheeseThickness)} jack cheese plates at {c.Point} (jacks {L(kd.JackFromBearing)} each side of CL bearing, p.124 A-A)", mc, mat, kd.CheeseLength, 0, asuC);
            }
        }
            /// <summary>
        /// The 1" splice plate under the end flange (and the 1 1/8" fill under the tie flange) as drawn in plan (p.124 partial plan, dashed
        /// "1\" SPLICE PL AND FILL PL", vector): from the end web outside face to an edge parallel to the joint (end flange edge) SpliceBeyondJoint past
        /// it, +- SpliceHalfWidth about the chord through the T point, the acute end corner cut. The fill = the same outline past the joint.
        /// This is the plate the knuckle bears on: the bearing's upper sole plate is under it (p.107 Section A-A).
        /// </summary>
        /// <summary>Corner T point (local plan), the tie chord direction from it toward the other end of the same tie, and the outward normal.</summary>
        private (Vec2 T, Vec2 C, Vec2 O)? CornerChord(ArchKnuckleCorner c)
        {
            if (P(c.Point) is not { } t || !_ties.TryGetValue(c.Tie, out var tg)) return null;
            string other = string.Equals(tg.Def.Start, c.Point, StringComparison.OrdinalIgnoreCase) ? tg.Def.End : tg.Def.Start;
            if (P(other) is not { } t2) return null;
            var c2 = (t2.XY - t.XY).Normalized();
            var o2 = c2.Left; if (o2.Y * t.Y < 0) o2 = -o2;
            return (t.XY, c2, o2);
        }

        private void SpliceFromPlan(ArchKnuckleDefinition k, ArchKnuckleDetail kd, ArchKnuckleCorner c, (Vec2 a, Vec2 b, Vec2 t, Vec2 n, Vec2 ctr) el, Vec3 T,
            Vec3 cIn, string cm, string asm, string mat, double g2, Assumption? asu)
        {
            if (CornerChord(c) is not { } cc) return;
            var (t0, c2, o2) = cc;                                                // T, chord T -> other end of the tie (p.124 "CL T0'-T34' CHORD"), outward
            double edge = k.EndFlangeWidth - k.EndWebOffset;
            double cn = Vec2.Dot(c2, el.n);
            if (Math.Abs(cn) < 0.2) { Warn($"Arch: knuckle {k.Name} corner {c.Point}: chord nearly parallel to the end line - splice plate not built."); return; }
            double U(double dn, double v) => (dn - Vec2.Dot(t0 + o2 * v - el.a, el.n)) / cn;    // u along the chord where the line Dn = dn meets lateral v
            Vec2 P(double u, double v) => t0 + c2 * u + o2 * v;
            double hw = kd.SpliceHalfWidth, dWeb = -k.EndWebOffset, dEnd = edge + kd.SpliceBeyondJoint;
            var A = P(U(dWeb, hw), hw); var B = P(U(dEnd, hw), hw); var C = P(U(dEnd, -hw), -hw); var D = P(U(dWeb, -hw), -hw);
            bool acuteOut = U(dEnd, hw) > U(dEnd, -hw);
            var poly = new List<Vec2> { A };
            if (acuteOut && kd.SpliceChamferSide > 0) { poly.Add(B - c2 * kd.SpliceChamferSide); poly.Add(B + (C - B).Normalized() * kd.SpliceChamferEnd); } else poly.Add(B);
            if (!acuteOut && kd.SpliceChamferSide > 0) { poly.Add(C + (B - C).Normalized() * kd.SpliceChamferEnd); poly.Add(C - c2 * kd.SpliceChamferSide); } else poly.Add(C);
            poly.Add(D);
            poly = Ccw(poly);
            // bolt rows (end flange to splice) along the chord
            var holes = new List<Hole>();
            var bolt = Model.Steel.Bolt;
            if (kd.SpliceBoltCount > 0)
                foreach (double v in new[] { kd.SpliceBoltRowOffset, -kd.SpliceBoltRowOffset })
                    for (int i = 0; i < kd.SpliceBoltCount; i++)
                        holes.Add(new Hole(P(U(dWeb, v) + kd.SpliceBoltFirst + i * kd.SpliceBoltPitch, v), bolt.HoleDiameter));
            Vec3 Map(double x, double y, double z) => W(new Vec3(x, y, z));
            var ms = new Mesh();
            PlateBuilder.AddPrism(ms, poly, holes, Map, _ => k.EndFlangeBottomZ - kd.SplicePlateThickness, _ => k.EndFlangeBottomZ);
            double len = Math.Max(Vec2.Distance(A, B), Vec2.Distance(D, C));
            AddPart($"{cm}-SP", PartCategory.KnucklePlate, asm, $"PL {L(kd.SplicePlateThickness)} x {L(2 * hw)} splice / bearing plate under the end flange at {c.Point}, end web to {L(kd.SpliceBeyondJoint)} past the joint, acute corner cut {L(kd.SpliceChamferSide)} x {L(kd.SpliceChamferEnd)}, {holes.Count} holes (p.124 plan + A-A / B-B)", ms, mat, len, 0, asu);
            double grip = k.EndFlangeThickness + kd.SplicePlateThickness;
            foreach (var h in holes)
                Model.Steel.Bolts.Add(new BoltInstance { Frame = BoltFrame(Map, h.Center.X, h.Center.Y, k.EndFlangeBottomZ + k.EndFlangeThickness, -1), Grip = grip, Length = bolt.LengthForGrip(grip), Assembly = asm });
            // fill: the outline past the joint (Dn >= edge + gap / 2)
            var fill = ClipHalf(poly, p => Vec2.Dot(p - el.a, el.n) - (edge + g2));
            if (fill.Count >= 3)
            {
                var mf = new Mesh();
                PlateBuilder.AddPrism(mf, Ccw(fill), Array.Empty<Hole>(), Map, _ => -kd.FillThickness, _ => 0);
                AddPart($"{cm}-FI", PartCategory.KnucklePlate, asm, $"PL {L(kd.FillThickness)} fill under the tie flange at {c.Point}, splice outline past the joint (p.124 plan + A-A)", mf, mat, kd.SpliceBeyondJoint, 0, asu);
            }
        }

        /// <summary>Part of a convex / simple polygon where f(p) >= 0 (Sutherland-Hodgman, one half-plane).</summary>
        private static List<Vec2> ClipHalf(List<Vec2> poly, Func<Vec2, double> f)
        {
            var r = new List<Vec2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                double fa = f(a), fb = f(b);
                if (fa >= 0) r.Add(a);
                if ((fa >= 0) != (fb >= 0)) r.Add(a + (b - a) * (fa / (fa - fb)));
            }
            return r;
        }
    }
}
