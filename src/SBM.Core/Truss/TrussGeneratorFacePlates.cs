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
        // ------------------------------------------------------------------ connection plates flat on member faces (TrussDefinition.FacePlates, PTC agent review 2026-11-03)

        /// <summary>One flat piece of a face plate: bearing face plane through <see cref="Bend"/> (U = <see cref="UBend"/> there), along <see cref="D"/>, across
        /// <see cref="A"/> (horizontal, left), normal <see cref="N"/> (up); the piece takes the outline part with U below / above the bend (<see cref="Low"/>).</summary>
        private sealed record FacePiece(Vec3 Bend, double UBend, Vec3 D, Vec3 A, Vec3 N, bool? Low)
        {
            public Vec3 At(double u, double v) => Bend + D * (u - UBend) + A * v;
        }

        private readonly List<(TrussFacePlateDefinition fp, List<FacePiece> pieces, int sideSign, string asm, string where, string pl, string pt, Vec3 P)> _faceRivetFrames = new();

        private List<SteelPart> PartsOf(Inst m) => Model.Steel.Parts.Where(p => p.Mesh is { IsEmpty: false } && ((p.Member ?? "") == m.Mark || p.Mark == m.Mark)).ToList();

        private Inst? FindInst(string name, string pl, string pt) =>
            _inst.Where(o => o.Def.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || o.Mark.Equals(name, StringComparison.OrdinalIgnoreCase))
                 .OrderBy(o => o.Plane.Equals(pl, StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();

        private static SteelLineProbe ProbeOf(IEnumerable<SteelPart> parts, Vec3 c, double r)
        {
            var lo = new Vec3(c.X - r, c.Y - r, c.Z - r); var hi = new Vec3(c.X + r, c.Y + r, c.Z + r);
            var pr = new SteelLineProbe(); int i = 0;
            foreach (var p in parts) pr.Add(p.Mesh, i++, lo, hi);
            return pr;
        }

        private static bool InPoly(IReadOnlyList<Vec2> poly, Vec2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < poly[i].X + (p.Y - poly[i].Y) * (poly[j].X - poly[i].X) / (poly[j].Y - poly[i].Y)) c = !c;
            return c;
        }

        /// <summary>Part of a polygon with U below (<paramref name="low"/>) or above <paramref name="ub"/>.</summary>
        private static List<Vec2> ClipU(List<Vec2> poly, double ub, bool low) => ClipHalf(poly, new Vec2(low ? -1 : 1, 0), low ? -ub : ub);

        private void BuildFacePlates()
        {
            if (_d.FacePlates is not { Count: > 0 } defs) return;
            int nPlates = 0, nFills = 0, nAngles = 0;
            foreach (var fp in defs)
            {
                string spec = fp.Point.Trim();
                var planes = spec.Contains(':') ? new[] { spec[..spec.IndexOf(':')].Trim() } : _d.Planes.Select(p => p.Name).ToArray();
                string ptName = spec.Contains(':') ? spec[(spec.IndexOf(':') + 1)..].Trim() : spec;
                foreach (var pl in planes)
                {
                    if (!_pts.TryGetValue((pl.ToUpperInvariant(), ptName.ToUpperInvariant()), out var P)) { if (spec.Contains(':')) Warn($"Truss: face plate at {spec} - the point is not defined; not built{Src}."); continue; }
                    string where = $"{pl}:{ptName}{fp.Suffix}";
                    if (fp.Thickness <= 0 || fp.Outline.Count < 3 || fp.Side == TrussBracingPlateSide.Both) { Warn($"Truss: face plate at {where} - thickness / outline missing or Side = Both; not built{Src}."); continue; }
                    double st = StationOf(P);
                    var tan = Tangent(st);
                    var left = Vec3.Cross(Up, tan).Normalized();
                    int side = fp.Side == TrussBracingPlateSide.Above ? 1 : -1;
                    // bearing members
                    var chords = _inst.Where(o => IsChord(o.Def.Role) && o.Def.Role != TrussMemberRole.WindChord && o.Plane.Equals(pl, StringComparison.OrdinalIgnoreCase)
                        && (At(pl, ptName).Contains(o) || Through(pl, ptName).Contains(o))).ToList();
                    var bear = fp.BearOn is { Count: > 0 } bo ? bo.Select(x => FindInst(x, pl, ptName)).Where(o => o != null).Select(o => o!).Distinct().ToList() : chords;
                    if (bear.Count == 0) { Warn($"Truss: face plate at {where} - no member to bear on; not built{Src}."); continue; }
                    var bearParts = bear.SelectMany(PartsOf).ToList();
                    var probe = ProbeOf(bearParts, P, 4.0);
                    var outline = fp.Outline.Select(q => new Vec2(q.U, q.V)).ToList();
                    if (MeshBuilder.SignedArea(outline) < 0) outline.Reverse();
                    // piece directions: chord faces (bent) or one member's flange plane
                    var dirs = new List<(Vec3 d, Vec3 n, int sideOfPoint)>();   // sideOfPoint: -1 = back chord, +1 = ahead, 0 = through / flat
                    if (fp.NormalFrom is { Length: > 0 } nfm)
                    {
                        var m = FindInst(nfm, pl, ptName);
                        if (m == null) { Warn($"Truss: face plate at {where} - NormalFrom member '{nfm}' not found; not built{Src}."); continue; }
                        var n = m.Y.Z >= 0 ? m.Y : -m.Y;
                        var d = (tan - n * Vec3.Dot(tan, n)).Normalized();
                        dirs.Add((d, n, 0));
                    }
                    else
                    {
                        foreach (var c in chords)
                        {
                            var d = Vec3.Dot(c.Axis, tan) >= 0 ? c.Axis : -c.Axis;
                            int sp = Through(pl, ptName).Contains(c) ? 0 : Vec3.Dot(Away(c, StartsAt(c, pl, ptName)), tan) < 0 ? -1 : 1;
                            var n = Vec3.Cross(d, left).Normalized(); if (n.Z < 0) n = -n;
                            if (dirs.Any(x => x.sideOfPoint == sp)) continue;
                            dirs.Add((d, n, sp));
                        }
                        if (dirs.Count == 0) { Warn($"Truss: face plate at {where} - no chord at the point for the bent plate; not built{Src}."); continue; }
                        if (dirs.Count > 2 || dirs.Any(x => x.sideOfPoint == 0) && dirs.Count > 1) dirs = dirs.Where(x => x.sideOfPoint == 0).Take(1).ToList();
                    }
                    // level of each piece: the extreme face of the bearing parts along its normal over the outline (probed on a grid)
                    var uvs = new List<Vec2>();
                    double u0 = outline.Min(q => q.X), u1 = outline.Max(q => q.X), v0 = outline.Min(q => q.Y), v1 = outline.Max(q => q.Y);
                    for (int iu = 0; iu <= 24; iu++) for (int iv = 0; iv <= 24; iv++)
                    {
                        var q = new Vec2(u0 + (u1 - u0) * iu / 24, v0 + (v1 - v0) * iv / 24);
                        if (InPoly(outline, q)) uvs.Add(q);
                    }
                    var lv = new List<(Vec3 d, Vec3 a, Vec3 n, int sp, double h)>();
                    foreach (var (d, n, sp) in dirs)
                    {
                        var a = Vec3.Cross(n, d).Normalized();
                        double best = double.NaN;
                        foreach (var q in uvs)
                        {
                            if (sp < 0 && q.X > 0 || sp > 0 && q.X < 0) continue;
                            foreach (var iv in probe.Intervals(P + d * q.X + a * q.Y, n, 3.0))
                            {
                                double f = side > 0 ? iv.t1 : iv.t0;
                                if (double.IsNaN(best) || (side > 0 ? f > best : f < best)) best = f;
                            }
                        }
                        if (double.IsNaN(best)) { Warn($"Truss: face plate at {where} - no face of {string.Join(" / ", bear.Select(b => b.Mark))} under the outline{(sp != 0 ? (sp < 0 ? " (back chord)" : " (ahead chord)") : "")}; not built{Src}."); lv.Clear(); break; }
                        lv.Add((d, a, n, sp, best));
                    }
                    if (lv.Count == 0) continue;
                    // pieces, bend line: where the two face planes meet (trace in the vertical plane along the tangent, t = horizontal distance from the point)
                    var pieces = new List<FacePiece>();
                    double Trace(int k, double t, out double slope)
                    {
                        var (d, _, n, _, h) = lv[k];
                        var q = P + n * h;
                        double dh = Vec3.Dot(d, tan); slope = d.Z / dh;
                        double tq = Vec3.Dot(q - P, tan);
                        return q.Z - P.Z + slope * (t - tq);
                    }
                    Vec3 OnPlane(int k, double t) { double z = Trace(k, t, out _); return P + tan * t + Up * z; }
                    if (lv.Count == 1)
                    {
                        var (d, a, n0, _, h0) = lv[0];
                        pieces.Add(new FacePiece(P + n0 * h0, 0, d, a, n0, null));
                    }
                    else
                    {
                        int kb = lv[0].sp < 0 ? 0 : 1, ka = 1 - kb;
                        double zb0 = Trace(kb, 0, out double sb), za0 = Trace(ka, 0, out double sa);
                        double tk = Math.Abs(sa - sb) < 1e-9 ? 0 : (zb0 - za0) / (sa - sb);
                        var bend = OnPlane(ka, tk);
                        // origin = foot of the perpendicular from the point on the up-station piece (a view normal to it shows the point there)
                        int ko = fp.OriginOnBack ? kb : ka;
                        double ub = Vec3.Dot(bend - (P + lv[ko].n * lv[ko].h), lv[ko].d);
                        pieces.Add(new FacePiece(bend, ub, lv[kb].d, lv[kb].a, lv[kb].n, true));
                        pieces.Add(new FacePiece(bend, ub, lv[ka].d, lv[ka].a, lv[ka].n, false));
                        double ang = Math.Acos(Math.Clamp(Vec3.Dot(lv[kb].n, lv[ka].n), -1, 1)) * 180 / Math.PI;
                        Info($"Truss: face plate at {where} - bent over the chord kink ({ang:0.00} deg), built as two flat pieces meeting on the bend line {L(tk)} from the point along the bridge{Src}.");
                    }
                    Assumption? asu = fp.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(fp.Unresolved + Src, "plate flat on the member faces as drawn / scaled") : null;
                    if (asu != null) Warn(fp.Unresolved);
                    string asm = bear.Select(b => b.Assembly).FirstOrDefault(s => s.Length > 0 && !s.StartsWith("TRUSS-")) ?? Asm("BR-UPPER-LATERALS", AssemblyKind.TrussBracing, $"{_d.Name} - Upper laterals");
                    string mark = $"{pl}-FP{ptName}{fp.Suffix}";
                    // plate pieces
                    var mesh = new Mesh();
                    double area = 0;
                    foreach (var pc in pieces)
                    {
                        var poly = pc.Low is { } low ? ClipU(outline, pc.UBend, low) : outline;
                        if (poly.Count < 3 || Math.Abs(MeshBuilder.SignedArea(poly)) < 1e-6) continue;
                        MeshBuilder.AddExtrusion(mesh, poly, new Frame3(pc.At(0, 0), pc.D, pc.A, pc.N), side > 0 ? 0 : -fp.Thickness, side > 0 ? fp.Thickness : 0);
                        area += Math.Abs(MeshBuilder.SignedArea(poly));
                    }
                    if (mesh.IsEmpty) { Warn($"Truss: face plate at {where} - outline empty; not built{Src}."); continue; }
                    Model.Steel.Parts.Add(new SteelPart { Mark = mark, Category = PartCategory.TrussConnectionPlate, Assembly = asm, Mesh = mesh, Material = Material(fp.Material), Length = u1 - u0, Assumption = asu,
                        Description = $"{(fp.Label.Length > 0 ? fp.Label : "connection plate")} {TrussSections.FormatIn(fp.Thickness)} at {ptName} ({area / 0.09290304:0.0} SF{(pieces.Count > 1 ? ", bent: two flat pieces" : "")})" });
                    Model.PanelOf[mark] = $"Joint {ptName}";
                    nPlates++;
                    // fills between the bearing face and a member face
                    int fi = 0;
                    foreach (var fl in fp.Fills ?? new())
                    {
                        var m = FindInst(fl.Member, pl, ptName);
                        if (m == null || fl.Outline.Count < 3) { Warn($"Truss: face plate at {where} - fill member '{fl.Member}' not found / outline missing; fill not built{Src}."); continue; }
                        var mpr = ProbeOf(PartsOf(m), P, 4.0);
                        var fo = fl.Outline.Select(q => new Vec2(q.U, q.V)).ToList();
                        if (MeshBuilder.SignedArea(fo) < 0) fo.Reverse();
                        var fmesh = new Mesh();
                        double gMin = double.MaxValue, gMax = 0; int miss = 0;
                        foreach (var pc in pieces)
                        {
                            var poly = pc.Low is { } low ? ClipU(fo, pc.UBend, low) : fo;
                            if (poly.Count < 3 || Math.Abs(MeshBuilder.SignedArea(poly)) < 1e-7) continue;
                            var dirIn = pc.N * (-side);                                   // from the bearing face toward the member
                            var gaps = poly.Select(q =>
                            {
                                var o = pc.At(q.X, q.Y);
                                var iv = mpr.Intervals(o, dirIn, 0.6).Where(x => x.t1 > -1e-4).OrderBy(x => x.t0).FirstOrDefault();
                                return iv == default ? double.NaN : Math.Max(0, iv.t0);
                            }).ToList();
                            if (gaps.All(double.IsNaN)) { miss += poly.Count; continue; }
                            double gAvg = gaps.Where(g => !double.IsNaN(g)).Average();
                            miss += gaps.Count(double.IsNaN);
                            gaps = gaps.Select(g => double.IsNaN(g) ? gAvg : g).ToList();
                            if (gaps.Max() < In(1.0 / 32)) continue;
                            gMin = Math.Min(gMin, gaps.Min()); gMax = Math.Max(gMax, gaps.Max());
                            var top = poly.Select(q => pc.At(q.X, q.Y)).ToArray();
                            var bot = poly.Select((q, i) => pc.At(q.X, q.Y) + dirIn * gaps[i]).ToArray();
                            MeshBuilder.AddLoft(fmesh, poly, new[] { top, bot }, gaps.Max() - gaps.Min() < 1e-5);
                        }
                        if (fmesh.IsEmpty) { Info($"Truss: face plate at {where} - fill on {m.Mark}: no gap (member face on the plate or not under the fill){Src}."); continue; }
                        string fmark = $"{mark}-F{++fi}";
                        string th = gMax - gMin < In(1.0 / 32) ? TrussSections.FormatIn(gMax) : $"{TrussSections.FormatIn(gMin)} - {TrussSections.FormatIn(gMax)} (beveled)";
                        Model.Steel.Parts.Add(new SteelPart { Mark = fmark, Category = PartCategory.FillerPlate, Assembly = asm, Mesh = fmesh, Material = Material(fl.Material.Length > 0 ? fl.Material : fp.Material), Assumption = asu,
                            Description = $"{(fl.Label.Length > 0 ? fl.Label + ": " : "")}fill {th} between the plate at {ptName} and {m.Mark}" });
                        Model.PanelOf[fmark] = $"Joint {ptName}";
                        nFills++;
                        if (fl.PlanThickness > 0)
                            Info($"Truss: face plate at {where} - fill on {m.Mark}: modeled {th} (gap between the plate and the member face) vs drawn {TrussSections.FormatIn(fl.PlanThickness)}{(miss > 0 ? $"; {miss} fill corners not over the member" : "")}{Src}.");
                    }
                    // edge angle: one leg on the plate's member-side face, the other on the truss-plane stack face
                    if (fp.EdgeAngle.Length > 0)
                    {
                        var ang = TrussSections.ParseAngle(fp.EdgeAngle);
                        if (ang == null && SteelCatalog.FindAngle(fp.EdgeAngle) is { } ac) ang = (ac.LegA, ac.LegB, ac.Thickness);
                        var pc = pieces[^1];
                        if (ang is not { } a) Warn($"Truss: face plate at {where} - edge angle '{fp.EdgeAngle}' not understood; not built{Src}.");
                        else
                        {
                            // stack face: probe from the plate edge nearest to the truss plane toward it (across), at the angle's mid length, half a leg off the plate
                            double sgn = Math.Sign(outline.Average(q => q.Y)); if (sgn == 0) sgn = 1;   // + = the plate lies to the left of the truss plane
                            double vEdge = sgn > 0 ? outline.Min(q => q.Y) : outline.Max(q => q.Y);
                            double um = (fp.EdgeAngleU0 + fp.EdgeAngleU1) / 2;
                            var outN = pc.N * (-side);                                     // away from the plate, toward the members (member-side face)
                            var towardStack = pc.A * (-sgn);
                            var others = Model.Steel.Parts.Where(p => p.Mark != mark && !p.Mark.StartsWith(mark + "-") && p.Mesh is { IsEmpty: false }).ToList();
                            var spr = ProbeOf(others, P, 3.0);
                            double back = In(6);
                            var o0 = pc.At(um, vEdge + sgn * back) + outN * (a.a / 2);
                            var hit = spr.Intervals(o0, towardStack, 1.0).Where(x => x.t0 > 0).OrderBy(x => x.t0).FirstOrDefault();
                            if (hit == default) Warn($"Truss: face plate at {where} - no truss-plane stack found across the plate edge for the edge angle; not built{Src}.");
                            else
                            {
                                double vFace = vEdge + sgn * back - sgn * hit.t0;           // V of the stack face
                                var inw = -towardStack;
                                var heel = pc.At(fp.EdgeAngleU0, vFace);
                                if (Math.Abs(vFace - vEdge) > In(0.5)) Info($"Truss: face plate at {where} - the stack face is {TrussSections.FormatIn(Math.Abs(vFace - vEdge))} {(sgn * (vFace - vEdge) < 0 ? "beyond" : "inside")} the plate edge (edge angle heel on the stack face){Src}.");
                                var sec = new[] { new Vec2(0, 0), new Vec2(a.b, 0), new Vec2(a.b, a.t), new Vec2(a.t, a.t), new Vec2(a.t, a.a), new Vec2(0, a.a) };
                                var ring = sec.Select(q => heel + inw * q.X + outN * q.Y).ToArray();
                                double len = fp.EdgeAngleU1 - fp.EdgeAngleU0;
                                var amesh = new Mesh();
                                MeshBuilder.AddLoft(amesh, sec, new[] { ring, ring.Select(p => p + pc.D * len).ToArray() });
                                string am = mark + "-EA";
                                Model.Steel.Parts.Add(new SteelPart { Mark = am, Category = PartCategory.TrussConnectionAngle, Assembly = asm, Mesh = amesh, Material = Material(fp.Material), Length = len, Assumption = asu,
                                    Description = $"edge angle {(fp.EdgeAngleLabel.Length > 0 ? fp.EdgeAngleLabel : fp.EdgeAngle)} x {TrussSections.FormatIn(len)} of the plate at {ptName}" });
                                Model.PanelOf[am] = $"Joint {ptName}";
                                nAngles++;
                                _faceAngleRivets.Add((fp, pc, heel, inw, outN, len, a, asm, where));
                            }
                        }
                    }
                    if (fp.Rivets is { Count: > 0 } || fp.MemberRivets is { Count: > 0 } || _faceAngleRivets.Any(x => x.fp == fp && x.where == where)) _faceRivetFrames.Add((fp, pieces, side, asm, where, pl, ptName, P));
                }
            }
            if (nPlates > 0) Info($"Truss: face plates - {nPlates} plates, {nFills} fills, {nAngles} edge angles{Src}.");
        }

        private readonly List<(TrussFacePlateDefinition fp, FacePiece pc, Vec3 heel, Vec3 inw, Vec3 outN, double len, (double a, double b, double t) ang, string asm, string where)> _faceAngleRivets = new();

        /// <summary>Rivets of the face plates and their edge angles, after every part is built: grip = contiguous steel along the shank; a rivet whose steel is
        /// the plate (angle leg) alone is not placed (counted); one rivet per hole (<see cref="MergeRivet"/>).</summary>
        private void PlaceFacePlateRivets()
        {
            if (_faceRivetFrames.Count == 0) return;
            var parts = Model.Steel.Parts.Where(p => p.Mesh is { IsEmpty: false }).ToList();
            int n = 0, lone = 0, shared = 0, miss = 0;
            var dropped = new Dictionary<string, int>();   // head over the head of a rivet already placed (no shared hole): not placed
            void One(Vec3 at, Vec3 ax, double dia, string? mat, string asm, Assumption? asu, double own, Vec3 xHint)
            {
                var pr = ProbeOf(parts, at, 0.8);
                if (pr.At(at, ax, 0.6) is not { } iv) { miss++; return; }
                if (iv.t1 - iv.t0 < own + In(1.0 / 32)) { lone++; return; }
                int d0 = dropped.Values.Sum();
                if (MergeRivet(at + ax * iv.t1, -ax, xHint, iv.t1 - iv.t0, dia, asm, mat, asu, dropped)) shared++; else if (dropped.Values.Sum() == d0) n++;
            }
            foreach (var (fp, pieces, side, asm, where, pl, ptName, P) in _faceRivetFrames)
            {
                var asu = RivetFlag((fp.RivetUnresolved.Length > 0 ? fp.RivetUnresolved : fp.Unresolved) is { Length: > 0 } s ? s + Src : null, "rivets of the face plate at the drawn / derived positions");
                foreach (var rv in fp.Rivets ?? new())
                {
                    var pc = pieces.Count == 1 ? pieces[0] : rv.U <= pieces[0].UBend ? pieces.First(p => p.Low == true) : pieces.First(p => p.Low == false);
                    var at = pc.At(rv.U, rv.V) + pc.N * (side * fp.Thickness / 2);
                    One(at, pc.N * (-side), rv.Diameter ?? fp.RivetDiameter, rv.Material ?? fp.RivetMaterial, asm, asu, fp.Thickness, pc.D);
                }
                FacePiece PieceAt(Vec3 x)
                {
                    if (pieces.Count == 1) return pieces[0];
                    var hi = pieces.First(p => p.Low == false);
                    double u = Vec3.Dot(x - hi.Bend, hi.D) + hi.UBend;
                    return u < hi.UBend ? pieces.First(p => p.Low == true) : hi;
                }
                foreach (var g in fp.MemberRivets ?? new())
                {
                    var m = FindInst(g.Member, pl, ptName);
                    if (m == null || g.Count <= 0 || g.Pitch <= 0) { Warn($"Truss: face plate at {where} - rivet group member '{g.Member}' not found / count or pitch missing{Src}."); continue; }
                    bool startEnd = (m.A - P).Length <= (m.B - P).Length;
                    var E = startEnd ? m.A + m.Axis * m.SA : m.B - m.Axis * m.SB;
                    var dir = startEnd ? m.Axis : -m.Axis;
                    var pc0 = PieceAt(E);
                    var dp = (dir - pc0.N * Vec3.Dot(dir, pc0.N)).Normalized();
                    var leftM = Vec3.Cross(pc0.N, dp).Normalized();
                    var pos = new List<(double s, int r)>();
                    int rows = Math.Max(1, g.Rows);
                    for (int r = 0; r < rows; r++)
                        for (int k = 0; k < g.Count; k++) pos.Add((g.FromEnd + k * g.Pitch + (g.Stagger && r % 2 == 1 ? g.Pitch / 2 : 0), r));
                    foreach (var (sAl, r) in pos.OrderBy(q => q.s).ThenBy(q => q.r).Take(g.Count))
                    {
                        double o = g.Offset + (r - (rows - 1) / 2.0) * g.Gage;
                        var x3 = E + dp * sAl + leftM * o;
                        var pc = PieceAt(x3);
                        var onFace = x3 - pc.N * Vec3.Dot(x3 - pc.Bend, pc.N);
                        One(onFace + pc.N * (side * fp.Thickness / 2), pc.N * (-side), g.Diameter ?? fp.RivetDiameter, g.Material ?? fp.RivetMaterial, asm, asu, fp.Thickness, pc.D);
                    }
                }
                foreach (var (_, pc, heel, inw, outN, len, a, asm2, _) in _faceAngleRivets.Where(x => x.fp == fp && x.where == where))
                {
                    double g = a.b / 2 + a.t / 2, g2 = a.a / 2 + a.t / 2;
                    int np = fp.EdgeAngleRivetsPlate, ns = fp.EdgeAngleRivetsStack;
                    int rowsP = Math.Max(1, fp.EdgeAngleRowsPlate);
                    for (int k = 0; k < np; k++)
                    {
                        int r = k % rowsP;
                        double sk = len * (k + 0.5) / np;
                        double gk = rowsP == 1 ? g : a.t + (a.b - a.t) * (r + 1) / (rowsP + 1);
                        var at = heel + pc.D * sk + inw * gk + outN * (a.t / 2);
                        One(at, -outN, fp.RivetDiameter, fp.RivetMaterial, asm2, asu, a.t, pc.D);
                    }
                    for (int k = 0; k < ns; k++)
                    {
                        double sk = len * (k + 0.5) / ns;
                        var at = heel + pc.D * sk + outN * g2 + inw * (a.t / 2);
                        One(at, -inw, fp.EdgeAngleStackRivetDiameter ?? fp.RivetDiameter, fp.EdgeAngleStackRivetMaterial ?? fp.RivetMaterial, asm2, asu, a.t, pc.D);
                    }
                }
            }
            Info($"Truss: face plate rivets - {n} placed{(shared > 0 ? $", {shared} in a hole already taken" : "")}{(lone > 0 ? $", {lone} positions grip the plate / angle leg only (not placed)" : "")}{(miss > 0 ? $", {miss} positions not in steel (not placed)" : "")}{(dropped.Count > 0 ? $", {dropped.Values.Sum()} with a head over the head of a rivet already placed (not placed)" : "")}{Src}.");
        }
    }
}
