using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Arch;

/// <summary>Upper lateral bracing connections at the rib panel points (plan p.129-140): gussets with true arcs, connection plates, stiffeners, bolts.</summary>
public static partial class ArchGenerator
{
    private sealed partial class Gen
    {
        /// <summary>One gusset leg placed on its brace: drawing (ξ along the drawn brace axis, η across) -> local frame on the brace flange.</summary>
        private sealed record LegMap(string Brace, Vec2 P0d, Vec2 Ad, Vec3 P0, Vec3 D, Vec3 V, Vec3 M, double AngleDrawing, double Angle3D, double Phi, Vec3 Web, Vec2 Weld1, Vec2 Weld2)
        {
            public Vec3 Map(Vec2 p)
            {
                var r = p - P0d;
                return P0 + D * Vec2.Dot(r, Ad) + V * Vec2.Dot(r, Ad.Left);
            }
        }

        /// <summary>Brace end trims (member name, true = its From end) -> new end point (local), computed before the members are built.</summary>
        private readonly Dictionary<(string Brace, bool AtFrom), (double Top, double Bottom)> _braceEnds = new();
        private readonly List<Action> _connectionParts = new();
        /// <summary>Web direction (local, unit, +Z side) of a brace at a connected end, from the gusset geometry.</summary>
        private readonly Dictionary<(string Brace, bool AtFrom), Vec3> _braceWeb = new();
        /// <summary>Gusset welded edges per connection (local end points on the plate face side, top flag, brace).</summary>
        private readonly Dictionary<string, List<(Vec3 A, Vec3 B, bool Top, string Brace)>> _weldEdges = new();

        private static Vec2 V2(ArchOutlinePoint p) => new(p.X, p.Y);

        /// <summary>Rib frame at a local point: CL point, radial (outward), tangent, binormal toward the bracing (local Y = 0 side).</summary>
        private (Vec3 Pc, Vec3 Rad, Vec3 Tan, Vec3 Nb) RibFrameAt(ArchRibResult rib, Vec3 p)
        {
            var d = p - rib.Center;
            double th = Math.Atan2(Vec3.Dot(d, rib.Up), Vec3.Dot(d, rib.ChordDir));
            var rad = (rib.ChordDir * Math.Cos(th) + rib.Up * Math.Sin(th)).Normalized();
            var pc = rib.Center + rad * rib.Radius;
            var nb = Vec3.Cross(rib.ChordDir, rib.Up).Normalized();
            if (Vec3.Dot(nb, new Vec3(0, -pc.Y, 0)) < 0) nb = -nb;
            var tan = Vec3.Cross(nb, rad).Normalized();
            return (pc, rad, tan, nb);
        }

        /// <summary>Plans the connections: brace end trims now (members are built next), plates after the members (queued).</summary>
        private void PlanBracingConnections(WideFlangeSize w, Dictionary<string, (Vec3 a, Vec3 b)> axesLocal)
        {
            foreach (var c in _d.BracingConnections)
            {
                var rib = _ribs.FirstOrDefault(r => string.Equals(r.Name, c.Rib, StringComparison.OrdinalIgnoreCase));
                var ribDef = _d.Ribs.FirstOrDefault(r => string.Equals(r.Name, c.Rib, StringComparison.OrdinalIgnoreCase));
                var rw = ribDef == null ? null : SteelCatalog.FindWideFlange(ribDef.Section);
                if (rib == null || rw == null || P(c.Point) is not { } wp) { Warn($"Arch: bracing connection {c.Name}: rib '{c.Rib}' or point '{c.Point}' missing - not built."); continue; }
                var (pc, rad, tan, nb) = RibFrameAt(rib, wp);
                string src = c.Source.Length > 0 ? c.Source : "p.129-140";
                // WP across / along the rib radius (checks: B-B 8 1/4" CL rib to WP; A-A WP to the web centre line).
                double across = Vec3.Dot(wp - pc, nb), radial = Vec3.Dot(wp - pc, rad);
                double faceOut = rw.Depth / 2 + c.ConnectionPlateThickness;
                Check($"bracing {c.Name}: WP to the connection plate face (rib CL + d/2 + 7/8\")", faceOut, across, Tol, L, $"{src}: p.128 table point vs W14x132 + PL; B-B prints 8 1/4\" CL rib to WP");
                if (!double.IsNaN(c.CheckWpFromRibCl)) Check($"bracing {c.Name}: CL rib to WP across the rib (B-B)", c.CheckWpFromRibCl, across, Tol, L, $"{src} B-B vs the p.128 table point");
                if (!double.IsNaN(c.WebAxisY)) Check($"bracing {c.Name}: WP radially off the rib web CL (A-A)", -c.WebAxisY, radial, Tol, L, $"{src} A-A (vector, scaled) vs the p.128 table point on the R 126'-0 rib");
                // A-A frame: origin = WP on the rib flange outer face, x = radial x toward-viewer (seen from the bracing), y = radial, z = nb.
                var oA = wp - nb * (across - rw.Depth / 2);
                var xA = Vec3.Cross(rad, nb).Normalized();
                var fA = new Frame3(oA, xA, rad, nb);
                var faceP = oA + nb * c.ConnectionPlateThickness;

                // Braces at the point: direction from the WP into the brace, member frame (web along Z).
                var braces = new List<(string Name, bool AtFrom, Vec3 D, Vec3 M, Vec3 V)>();
                foreach (var br in _d.Bracing)
                {
                    if (!axesLocal.TryGetValue(br.Name, out var ax)) continue;
                    bool atFrom = string.Equals(br.From, c.Point, StringComparison.OrdinalIgnoreCase), atTo = string.Equals(br.To, c.Point, StringComparison.OrdinalIgnoreCase);
                    if (!atFrom && !atTo) continue;
                    var d = (atFrom ? ax.b - ax.a : ax.a - ax.b).Normalized();
                    var axw = (ax.b - ax.a).Normalized();
                    var m = (new Vec3(0, 0, 1) - axw * Vec3.Dot(axw, new Vec3(0, 0, 1))).Normalized();   // member frame as BuildBracing (web along local Z)
                    braces.Add((br.Name, atFrom, d, m, Vec3.Cross(d, m).Normalized()));
                }
                if (braces.Count == 0) { Warn($"Arch: bracing connection {c.Name}: no bracing member ends at {c.Point}."); continue; }

                var endBy = new Dictionary<string, List<(double Lam, bool Top)>>();
                foreach (var (g, top) in new[] { (c.TopGusset, true), (c.BottomGusset, false) })
                {
                    if (g == null || g.Plate.Outline.Count < 3) continue;
                    var maps = MapGusset(c, g, top, braces, w, nb, tan, faceP, src);
                    if (maps == null) continue;
                    foreach (var (leg, mp) in maps)
                    {
                        var bk = braces.First(x => x.Name == mp.Brace);
                        if (top || !_braceWeb.ContainsKey((mp.Brace, bk.AtFrom))) _braceWeb[(mp.Brace, bk.AtFrom)] = mp.Web;
                        else Check($"bracing {c.Name}: {mp.Brace} roll from the top vs the bottom gusset", 0, Math.Acos(Math.Min(1, Vec3.Dot(_braceWeb[(mp.Brace, bk.AtFrom)], mp.Web))) * 180 / Math.PI, 0.25,
                            x => $"{x:F3} deg", $"{src}: both gusset views");
                        var e3 = mp.Map(new Vec2(leg.EndX, leg.EndY));
                        var b = braces.First(x => x.Name == mp.Brace);
                        double lam = Vec3.Dot(e3 - wp, b.D);
                        (endBy.TryGetValue(mp.Brace, out var l) ? l : endBy[mp.Brace] = new()).Add((lam, top));
                    }
                    foreach (var (_, mp) in maps)
                        (_weldEdges.TryGetValue(c.Name, out var we) ? we : _weldEdges[c.Name] = new()).Add((mp.Map(mp.Weld1), mp.Map(mp.Weld2), top, mp.Brace));
                    var gg = g; bool tt = top; var mm = maps;
                    _connectionParts.Add(() => BuildGusset(c, gg, tt, mm, w, src));
                }
                foreach (var (bn, ends) in endBy)
                {
                    // The dashed W8x28 end under each gusset = where that flange ends: the end is cut on a plane through both flange end lines.
                    var b = braces.First(x => x.Name == bn);
                    double lt = ends.FirstOrDefault(x => x.Top).Lam, lb = ends.FirstOrDefault(x => !x.Top).Lam;
                    if (!ends.Any(x => x.Top)) lt = lb;
                    if (!ends.Any(x => !x.Top)) lb = lt;
                    _braceEnds[(bn, b.AtFrom)] = (lt, lb);
                    var web = _braceWeb.TryGetValue((bn, b.AtFrom), out var wv) ? wv : b.M;
                    var vv = Vec3.Cross(b.D, web).Normalized();
                    var cutN = Vec3.Cross(vv, b.D * (lt - lb) + web * w.Depth).Normalized();
                    double toFace = Math.Acos(Math.Min(1, Math.Abs(Vec3.Dot(cutN, nb)))) * 180 / Math.PI;
                    Info($"Arch bracing {c.Name}: {bn} flanges end {L(lt)} (top) / {L(lb)} (bottom) from the WP along the axis; end cut {toFace:F1} deg off the rib flange face ({src}, dashed brace ends in the gusset views).");
                }
                var cc = c; var rr = rib; var rwf = rw;
                _connectionParts.Add(() => BuildConnectionPlates(cc, fA, rad, nb, rwf, src));
            }
        }

        /// <summary>Places each leg of a gusset drawing on its brace; null when it cannot be matched.</summary>
        private List<(ArchGussetLeg Leg, LegMap Map)>? MapGusset(ArchBracingConnection c, ArchGussetPlate g, bool top, List<(string Name, bool AtFrom, Vec3 D, Vec3 M, Vec3 V)> braces,
            WideFlangeSize w, Vec3 nb, Vec3 tan, Vec3 faceP, string src)
        {
            string side = top ? "top" : "bottom";
            var o = g.Plate.Outline;
            int n = o.Count;
            // Weld edges and the legs' parts (split at the bend line).
            var welds = Enumerable.Range(0, n).Where(i => o[i].Weld).ToList();
            if (g.Legs.Count == 0 || welds.Count == 0) { Warn($"Arch: bracing connection {c.Name} {side} gusset: no brace / welded edge in the drawing - not built."); return null; }
            if (g.Legs.Count != braces.Count) Warn($"Arch CHECK bracing {c.Name} {side} gusset: {g.Legs.Count} braces drawn, {braces.Count} members end at {c.Point}.");
            double s = top ? 1 : -1;
            var result = new List<(ArchGussetLeg, LegMap)>();
            var candidates = new List<(ArchGussetLeg Leg, int Weld, string Brace, LegMap Map, double Err)>();
            foreach (var leg in g.Legs)
            {
                var e = new Vec2(leg.EndX, leg.EndY); var ad = new Vec2(leg.DirX, leg.DirY).Normalized();
                // weld edge of this leg: the welded edge nearest to the drawn brace end (one per side of the bend)
                int wi = welds.OrderBy(i => DistToSeg(e, V2(o[i]), V2(o[(i + 1) % n]))).First();
                var w1 = V2(o[wi]); var w2 = V2(o[(wi + 1) % n]);
                var wd = (w2 - w1).Normalized(); if (Vec2.Dot(wd, ad) < 0) wd = -wd;
                double angD = Math.Acos(Math.Clamp(Vec2.Dot(wd, ad), -1, 1));
                // intersection of the drawn brace axis with the weld line
                double den = Vec2.Cross(ad, wd);
                if (Math.Abs(den) < 1e-9) continue;
                double tAx = Vec2.Cross(w1 - e, wd) / den;
                var p0d = e + ad * tAx;
                foreach (var b in braces)
                {
                    // contact face on the brace flange, weld line = that face / outer face against the connection plate face (the corner farther
                    // toward the brace touches: a square-cut gusset edge on a skewed tee joint)
                    // The gusset lies on the brace flange and its welded edge on the connection plate face: the edge direction w is the line of the
                    // plate face at the drawn angle to the brace axis (two solutions, the one along the rib = nearest the rib tangent), and the brace
                    // web is normal to the plane (D, w): the members are ROLLED about their axis (not web-vertical) so the gussets follow the rib.
                    var dp = b.D - nb * Vec3.Dot(b.D, nb);
                    double cb = dp.Length;
                    // drawn angle smaller than the brace-to-plate angle: impossible for this brace (more than 1 deg) or a plan rounding (< 1 deg:
                    // the edge is taken along the brace projection and the deficit is a CHECK)
                    double minAng = Math.Acos(Math.Min(1, cb));
                    if (angD < minAng - Numerics.DegToRad(1.0)) continue;
                    if (angD < minAng) Check($"bracing {c.Name} {side} gusset ({b.Name}): drawn brace-to-edge angle vs the smallest possible (brace to the plate face)",
                        minAng * 180 / Math.PI, angD * 180 / Math.PI, 0.05, x => $"{x:F3} deg", $"{src}: gusset view (vector) vs the p.128 points and the 5 deg rib plane");
                    var e1 = dp * (1 / cb); var e2 = Vec3.Cross(nb, e1).Normalized();
                    double cg = Math.Clamp(Math.Cos(angD) / cb, -1, 1), sgm = Math.Sqrt(1 - cg * cg);
                    var wa = e1 * cg + e2 * sgm; var wb2 = e1 * cg - e2 * sgm;
                    double phA = Math.Acos(Math.Min(1, Math.Abs(Vec3.Dot(wa, tan)))), phB = Math.Acos(Math.Min(1, Math.Abs(Vec3.Dot(wb2, tan))));
                    var w3 = phA <= phB ? wa : wb2; double phi = Math.Min(phA, phB);
                    var web = Vec3.Cross(b.D, w3).Normalized();
                    if (web.Z < 0) web = -web;
                    var bm = web; var bv = Vec3.Cross(b.D, bm).Normalized();
                    var q = P(c.Point)!.Value + bm * (s * w.Depth / 2);
                    Vec3? Line(Vec3 planePt)
                    {
                        // point on (plane through planePt, normal b.M) ∩ (plane through faceP, normal nb) nearest to planePt: along the in-plane normal to w3
                        var u = Vec3.Cross(bm, w3).Normalized();     // in the gusset plane, normal to the weld line
                        double dn = Vec3.Dot(u, nb);
                        if (Math.Abs(dn) < 1e-9) return null;
                        return planePt + u * (Vec3.Dot(faceP - planePt, nb) / dn);
                    }
                    var lc = Line(q); var lo = Line(q + bm * (s * c.GussetThickness));
                    if (lc == null || lo == null) continue;
                    var inPlane = (b.D - w3 * Vec3.Dot(b.D, w3)).Normalized();   // toward the brace, normal to the weld line in the plane
                    var loC = lo.Value - bm * (s * c.GussetThickness);
                    var wl = Vec3.Dot(lc.Value - q, inPlane) >= Vec3.Dot(loC - q, inPlane) ? lc.Value : loC;
                    // brace axis on the contact face: q + D t; weld line: wl + w3 u -> intersection (2D in D / V)
                    var r0 = wl - q;
                    double a11 = 1, a12 = -Vec3.Dot(w3, b.D), a21 = 0, a22 = -Vec3.Dot(w3, bv);
                    double r1 = Vec3.Dot(r0, b.D), r2 = Vec3.Dot(r0, bv);
                    double det = a11 * a22 - a12 * a21;
                    if (Math.Abs(det) < 1e-12) continue;
                    double uu = (a11 * r2 - a21 * r1) / det, tt = r1 - a12 * uu;
                    var p0 = q + b.D * tt;
                    double ang3 = Math.Acos(Math.Clamp(Vec3.Dot(w3, b.D), -1, 1));
                    // η axis: V or -V so that the drawn weld direction lands on the 3D weld direction side
                    double sg = Vec3.Dot(w3, bv) * Vec2.Dot(wd, ad.Left);
                    var v = sg < 0 ? -bv : bv;
                    candidates.Add((leg, wi, b.Name, new LegMap(b.Name, p0d, ad, p0, b.D, v, bm * s, angD, ang3, phi, web, w1, w2), phi));
                }
            }
            // assignment: each drawn leg to a distinct brace, smallest total angle difference
            var legs = g.Legs.ToList();
            var bestSum = double.MaxValue; List<(ArchGussetLeg, LegMap)>? best = null;
            foreach (var perm in Permutations(braces.Select(b => b.Name).ToList(), legs.Count))
            {
                double sum = 0; var pick = new List<(ArchGussetLeg, LegMap)>(); bool ok = true;
                for (int i = 0; i < legs.Count; i++)
                {
                    var cand = candidates.FirstOrDefault(x => x.Leg == legs[i] && x.Brace == perm[i]);
                    if (cand.Map == null) { ok = false; break; }
                    sum += cand.Err; pick.Add((legs[i], cand.Map));
                }
                if (ok && sum < bestSum) { bestSum = sum; best = pick; }
            }
            if (best == null) { Warn($"Arch: bracing connection {c.Name} {side} gusset: the drawn braces do not match the members at {c.Point} - not built."); return null; }
            foreach (var (_, mp) in best)
            {
                double phiDeg = mp.Phi * 180 / Math.PI;
                double plan = c.CheckWeldAnglesDeg.Count == 0 ? double.NaN : c.CheckWeldAnglesDeg.OrderBy(a => Math.Abs(a - phiDeg)).First();
                if (!double.IsNaN(plan)) Check($"bracing {c.Name} {side} gusset ({mp.Brace}): welded edge to the rib axis in the plate face", plan, phiDeg, 0.25, x => $"{x:F3} deg",
                    $"{src}: VIEW A-A printed angle vs the edge placed from the drawn gusset angle {mp.AngleDrawing * 180 / Math.PI:F3} deg and the p.128 points");
                double roll = Math.Atan2(Vec3.Dot(mp.Web, Vec3.Cross(mp.D, VerticalWeb(mp.D))), Vec3.Dot(mp.Web, VerticalWeb(mp.D))) * 180 / Math.PI;
                Info($"Arch bracing {c.Name} {side} gusset: {mp.Brace} rolled {roll:F2} deg from web-vertical (gusset plane through the brace and its welded edge).");
            }
            return best;
        }

        private static Vec3 VerticalWeb(Vec3 d) => (new Vec3(0, 0, 1) - d * d.Z).Normalized();

        private static IEnumerable<List<string>> Permutations(List<string> items, int k)
        {
            if (k == 0) { yield return new List<string>(); yield break; }
            for (int i = 0; i < items.Count; i++)
            {
                var rest = items.Where((_, j) => j != i).ToList();
                foreach (var p in Permutations(rest, k - 1)) { p.Insert(0, items[i]); yield return p; }
            }
        }

        private static double DistToSeg(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a; double l2 = Vec2.Dot(ab, ab);
            double t = l2 > 0 ? Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1) : 0;
            return Vec2.Distance(p, a + ab * t);
        }

        /// <summary>Gusset solid(s): the flat pattern split at the bend line, each part in its leg's plane (true arcs kept), holes and bolts.</summary>
        private void BuildGusset(ArchBracingConnection c, ArchGussetPlate g, bool top, List<(ArchGussetLeg Leg, LegMap Map)> maps, WideFlangeSize w, string src)
        {
            string side = top ? "top" : "bottom";
            string asm = ConnAsm(c);
            AddAssembly(asm, AssemblyKind.ArchBracing, $"Upper lateral bracing connection {c.Name} ({src})");
            var o = g.Plate.Outline;
            var full = new ArcPolygon(o.Select(V2).ToList(), o.Select(p => p.Radius).ToList());
            if (full.SignedArea() < 0) Warn($"Arch: bracing {c.Name} {side} gusset outline is clockwise (drawing frame).");
            var parts = new List<(ArcPolygon Poly, LegMap Map)>();
            if (maps.Count == 1 || g.BendA == null || g.BendB == null) parts.Add((full, maps[0].Map));
            else
            {
                var ba = new Vec2(g.BendA.X, g.BendA.Y); var bb = new Vec2(g.BendB.X, g.BendB.Y);
                int ia = Enumerable.Range(0, o.Count).OrderBy(i => Vec2.Distance(V2(o[i]), ba)).First();
                int ib = Enumerable.Range(0, o.Count).OrderBy(i => Vec2.Distance(V2(o[i]), bb)).First();
                if (Vec2.Distance(V2(o[ia]), ba) > Tol || Vec2.Distance(V2(o[ib]), bb) > Tol)
                    Warn($"Arch: bracing {c.Name} {side} gusset: bend line end not on an outline vertex ({L(Vec2.Distance(V2(o[ia]), ba))} / {L(Vec2.Distance(V2(o[ib]), bb))}).");
                foreach (var (sub, other) in new[] { (full.Sub(ia, ib), 0), (full.Sub(ib, ia), 1) })
                {
                    // the leg whose drawn brace end lies in this part
                    var tess = sub.Tessellate(1e-4);
                    var mp = maps.FirstOrDefault(x => Inside(new Vec2(x.Leg.EndX, x.Leg.EndY), tess)).Map;
                    if (mp == null) { Warn($"Arch: bracing {c.Name} {side} gusset: no drawn brace in one side of the bend - that side is not built."); continue; }
                    parts.Add((sub, mp));
                }
                if (parts.Count == 2)
                {
                    double gap = Math.Max((parts[0].Map.Map(ba) - parts[1].Map.Map(ba)).Length, (parts[0].Map.Map(bb) - parts[1].Map.Map(bb)).Length);
                    Check($"bracing {c.Name} {side} gusset: bend line placed by the two legs", 0, gap, In(1.0 / 8), L,
                        $"{src}: flat pattern bend line vs the intersection of the two brace flange planes (each leg on its own brace)");
                }
            }
            var bolt = Model.Steel.Bolt;
            var mesh = new Mesh();
            double area = 0; int nb = 0;
            foreach (var (poly, mp) in parts)
            {
                // frame on the contact face: x = D (ξ), y = ±V (η); thickness away from the brace flange (mp.M = outward normal of the flange)
                var fr = new Frame3(W(mp.P0), Wd(mp.D), Wd(mp.V), Wd(Vec3.Cross(mp.D, mp.V).Normalized()));
                double zs = Vec3.Dot(Vec3.Cross(mp.D, mp.V), mp.M) > 0 ? 1 : -1;
                var local = poly.Map(p => { var r = p - mp.P0d; return new Vec2(Vec2.Dot(r, mp.Ad), Vec2.Dot(r, mp.Ad.Left)); }, false);
                var tess = local.Tessellate(1e-4);
                var holes = new List<Hole>();
                foreach (var h in g.Plate.Holes)
                {
                    var hl = new Vec2(Vec2.Dot(new Vec2(h.X, h.Y) - mp.P0d, mp.Ad), Vec2.Dot(new Vec2(h.X, h.Y) - mp.P0d, mp.Ad.Left));
                    if (!Inside(hl, tess)) continue;
                    holes.Add(new Hole(hl, bolt.HoleDiameter));
                    // bolt: head on the gusset outer face, toward the brace flange
                    var head = fr.ToWorld(hl.X, hl.Y, zs * c.GussetThickness);
                    var zd = Wd(-mp.M);
                    var xd = Wd(mp.D);
                    double grip = c.GussetThickness + w.FlangeThickness;
                    Model.Steel.Bolts.Add(new BoltInstance { Frame = new Frame3(head, xd, Vec3.Cross(zd, xd).Normalized(), zd), Grip = grip, Length = bolt.LengthForGrip(grip), Assembly = asm });
                    nb++;
                }
                PlateBuilder.AddPlanarArc(mesh, local, holes, fr, 0, zs * c.GussetThickness);
                area += Math.Abs(local.SignedArea());
            }
            if (mesh.IsEmpty) return;
            int arcs = o.Count(p => p.Radius != 0);
            Model.Steel.Parts.Add(new SteelPart
            {
                Mark = $"{asm}-G{(top ? "T" : "B")}", Category = PartCategory.Gusset, Assembly = asm, Material = c.Material, Mesh = mesh,
                Description = $"PL {L(c.GussetThickness)} {side} gusset{(parts.Count > 1 ? " (bent)" : "")} at {c.Name}, {o.Count} edges ({arcs} arcs), {nb} holes ({src})",
                Length = Math.Sqrt(area),
            });
        }

        private static bool Inside(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) c = !c;
            }
            return c;
        }

        private static string ConnAsm(ArchBracingConnection c) => $"UPPER-BRACING-{c.Name.Replace("'", "P")}";

        /// <summary>Connection plates (A-A, on the rib flange outer face) with bolts through the rib flange, and the stiffeners between the rib flanges.</summary>
        private void BuildConnectionPlates(ArchBracingConnection c, Frame3 fA, Vec3 rad, Vec3 nb, WideFlangeSize rw, string src)
        {
            string asm = ConnAsm(c);
            AddAssembly(asm, AssemblyKind.ArchBracing, $"Upper lateral bracing connection {c.Name} ({src})");
            var bolt = Model.Steel.Bolt;
            var fw = new Frame3(W(fA.Origin), Wd(fA.X), Wd(fA.Y), Wd(fA.Z));
            foreach (var (cp, tag) in new[] { (c.TopConnectionPlate, "top"), (c.BottomConnectionPlate, "bottom") })
            {
                if (cp == null || cp.Outline.Count < 3) continue;
                var poly = new ArcPolygon(cp.Outline.Select(V2).ToList(), cp.Outline.Select(p => p.Radius).ToList());
                var holes = cp.Holes.Select(h => new Hole(new Vec2(h.X, h.Y), bolt.HoleDiameter)).ToList();
                var m = new Mesh();
                PlateBuilder.AddPlanarArc(m, poly, holes, fw, 0, c.ConnectionPlateThickness);
                double grip = c.ConnectionPlateThickness + rw.FlangeThickness;
                foreach (var h in holes)
                {
                    var head = fw.ToWorld(h.Center.X, h.Center.Y, c.ConnectionPlateThickness);
                    var zd = -fw.Z;
                    Model.Steel.Bolts.Add(new BoltInstance { Frame = new Frame3(head, fw.X, Vec3.Cross(zd, fw.X).Normalized(), zd), Grip = grip, Length = bolt.LengthForGrip(grip), Assembly = asm });
                }
                var xs = cp.Outline.Select(p => p.X).ToList();
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = $"{asm}-CP{(tag == "top" ? "T" : "B")}", Category = PartCategory.ArchConnectionPlate, Assembly = asm, Material = c.Material, Mesh = m,
                    Description = $"PL {L(c.ConnectionPlateThickness)} {tag} connection plate at {c.Name}, {holes.Count} holes ({src} VIEW A-A)", Length = xs.Max() - xs.Min(),
                });
            }
            // Stiffeners: A-A gives the position along the rib (thickness) and the radial extent; across the rib they run between the flange
            // inner faces (B-B) with clips at the web-flange corners.
            double hIn = rw.Depth / 2 - rw.FlangeThickness;   // CL to flange inner face
            var asu = c.StiffenerUnresolved.Length > 0 ? Assumption.Unresolved(c.StiffenerUnresolved, "A-A position / radial extent (vector), flange to flange, B-B clips") : null;
            int k = 0;
            foreach (var st in c.Stiffeners)
            {
                if (st.Outline.Count < 4) continue;
                double x0 = st.Outline.Min(p => p.X), x1 = st.Outline.Max(p => p.X), y0 = st.Outline.Min(p => p.Y), y1 = st.Outline.Max(p => p.Y);
                double webY = double.IsNaN(c.WebAxisY) ? 0 : c.WebAxisY;
                bool above = (y0 + y1) / 2 > webY;
                double webFace = webY + (above ? 1 : -1) * rw.WebThickness / 2, edge = above ? y1 : y0;
                double toEdge = Math.Abs(edge - webY);
                Check($"bracing {c.Name}: stiffener {(above ? "above" : "below")} the web - web face (A-A)", webFace, above ? y0 : y1, In(1.0 / 8), L, $"{src} A-A dashed stiffener (vector) vs W14x132 tw / 2");
                Check($"bracing {c.Name}: stiffener {(above ? "above" : "below")} the web - outer edge from the web CL", rw.FlangeWidth / 2 - In(1), toEdge, In(1.0 / 8), L, $"{src} A-A (vector) vs B-B: 1\" inside the flange tips");
                // plate in (u = radial from A-A, v = across the rib from the flange outer face at fA.Origin), thickness along x
                double v0 = -(rw.Depth / 2 + hIn), v1 = -(rw.Depth / 2 - hIn);   // flange inner faces measured from the outer face (z of fA)
                double ua = above ? webFace : edge, ub = above ? edge : webFace;
                double cl = c.StiffenerClip;
                var pts = new List<Vec2>();
                if (above) pts.AddRange(new[] { new Vec2(ua, v0 + cl), new Vec2(ua + cl, v0), new Vec2(ub, v0), new Vec2(ub, v1), new Vec2(ua + cl, v1), new Vec2(ua, v1 - cl) });
                else pts.AddRange(new[] { new Vec2(ua, v0), new Vec2(ub - cl, v0), new Vec2(ub, v0 + cl), new Vec2(ub, v1 - cl), new Vec2(ub - cl, v1), new Vec2(ua, v1) });
                var poly = new ArcPolygon(pts).CounterClockwise();
                var fs = new Frame3(fw.ToWorld(x0, 0, 0), fw.Y, fw.Z, Vec3.Cross(fw.Y, fw.Z).Normalized());   // x = radial, y = nb, z = -A-A x
                double zdir = Vec3.Dot(fs.Z, fw.X) > 0 ? 1 : -1;
                var m = new Mesh();
                PlateBuilder.AddPlanarArc(m, poly, Array.Empty<Hole>(), fs, 0, zdir * (x1 - x0));
                Check($"bracing {c.Name}: stiffener thickness (A-A)", c.StiffenerThickness, x1 - x0, In(1.0 / 16), L, $"{src} A-A dashed (vector) vs the 1\" label");
                k++;
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = $"{asm}-ST{k}", Category = PartCategory.ArchStiffener, Assembly = asm, Material = c.StiffenerMaterial, Mesh = m, Assumption = asu,
                    Description = $"PL {L(x1 - x0)} rib stiffener {(above ? "above" : "below")} the web at {c.Name}, {L(Math.Abs(ub - ua))} x {L(v1 - v0)}, {L(cl)} clips ({src} A-A / B-B)",
                    Length = v1 - v0,
                });
            }
            // gusset welded edges vs the connection plates (D-D / H-H: gusset 1" inside the plate edge)
            if (!double.IsNaN(c.CheckGussetFromPlateEdge) && _weldEdges.TryGetValue(c.Name, out var edges))
                foreach (var (a, b, top, brace) in edges)
                {
                    var cp = top ? c.TopConnectionPlate : c.BottomConnectionPlate;
                    if (cp == null || cp.Outline.Count < 3) continue;
                    Vec2 ToA(Vec3 q) { var r = q - fA.Origin; return new Vec2(Vec3.Dot(r, fA.X), Vec3.Dot(r, fA.Y)); }
                    var m = (ToA(a) + ToA(b)) * 0.5;
                    var poly = cp.Outline.Select(V2).ToList();
                    double dmin = double.MaxValue;
                    for (int i = 0; i < poly.Count; i++) dmin = Math.Min(dmin, DistToSeg(m, poly[i], poly[(i + 1) % poly.Count]));
                    bool inside = Inside(m, poly);
                    Check($"bracing {c.Name}: {(top ? "top" : "bottom")} gusset ({brace}) welded edge to the connection plate edge", c.CheckGussetFromPlateEdge, inside ? dmin : -dmin, In(1.0 / 8), L,
                        $"{src}: D-D / H-H 1\" vs the gusset placed on its brace and the plate placed by VIEW A-A (negative = off the plate)");
                }
        }
    }
}
