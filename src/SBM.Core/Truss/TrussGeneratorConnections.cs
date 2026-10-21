using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    /// <summary>
    /// Contiguous steel along a line through a set of closed part meshes (the triangles near a connection): entering / leaving by the triangle normals
    /// (outward), so touching or overlapping solids of one mesh are handled; intervals of all parts are united, gaps under 1 mm closed.
    /// </summary>
    internal sealed class SteelLineProbe
    {
        private readonly List<(Vec3 a, Vec3 b, Vec3 c, int part)> _tris = new();
        public int Count => _tris.Count;

        public void Add(Mesh mesh, int part, Vec3 lo, Vec3 hi)
        {
            var pos = mesh.Positions; var idx = mesh.Indices;
            for (int i = 0; i + 2 < idx.Count; i += 3)
            {
                var a = pos[idx[i]]; var b = pos[idx[i + 1]]; var c = pos[idx[i + 2]];
                if (Math.Max(a.X, Math.Max(b.X, c.X)) < lo.X || Math.Min(a.X, Math.Min(b.X, c.X)) > hi.X) continue;
                if (Math.Max(a.Y, Math.Max(b.Y, c.Y)) < lo.Y || Math.Min(a.Y, Math.Min(b.Y, c.Y)) > hi.Y) continue;
                if (Math.Max(a.Z, Math.Max(b.Z, c.Z)) < lo.Z || Math.Min(a.Z, Math.Min(b.Z, c.Z)) > hi.Z) continue;
                _tris.Add((a, b, c, part));
            }
        }

        /// <summary>Solid intervals (t along <paramref name="dir"/> from <paramref name="o"/>, within +-<paramref name="reach"/>).</summary>
        public List<(double t0, double t1)> Intervals(Vec3 o, Vec3 dir, double reach)
        {
            var d = dir.Normalized();
            // tiny irrational shift: a rivet line through a triangle edge (diagonal of a rectangle) would be counted twice
            o += new Vec3(1.37e-6, -0.83e-6, 1.11e-6);
            var hits = new Dictionary<int, List<(double t, int s)>>();
            foreach (var (a, b, c, part) in _tris)
            {
                var e1 = b - a; var e2 = c - a;
                var p = Vec3.Cross(d, e2);
                double det = Vec3.Dot(e1, p);
                if (Math.Abs(det) < 1e-14) continue;
                double inv = 1 / det;
                var s = o - a;
                double u = Vec3.Dot(s, p) * inv;
                if (u < 0 || u > 1) continue;
                var q = Vec3.Cross(s, e1);
                double v = Vec3.Dot(d, q) * inv;
                if (v < 0 || u + v > 1) continue;
                double t = Vec3.Dot(e2, q) * inv;
                if (t < -reach || t > reach) continue;
                var n = Vec3.Cross(e1, e2);
                if (!hits.TryGetValue(part, out var l)) hits[part] = l = new();
                l.Add((t, Vec3.Dot(n, d) < 0 ? 1 : -1));
            }
            var all = new List<(double t0, double t1)>();
            foreach (var l in hits.Values)
            {
                // entries before exits at equal t: touching solids of one mesh stay one interval
                l.Sort((x, y) => x.t != y.t ? x.t.CompareTo(y.t) : y.s.CompareTo(x.s));
                int depth = 0; double start = 0;
                foreach (var (t, s) in l)
                {
                    if (s > 0) { if (depth == 0) start = t; depth++; }
                    else if (depth > 0) { depth--; if (depth == 0) all.Add((start, t)); }
                }
            }
            all.Sort((x, y) => x.t0.CompareTo(y.t0));
            var res = new List<(double t0, double t1)>();
            foreach (var iv in all)
            {
                if (res.Count > 0 && iv.t0 <= res[^1].t1 + 0.001) res[^1] = (res[^1].t0, Math.Max(res[^1].t1, iv.t1));
                else res.Add(iv);
            }
            return res;
        }

        /// <summary>The interval containing t = 0 (null = the point is not in steel).</summary>
        public (double t0, double t1)? At(Vec3 o, Vec3 dir, double reach)
        {
            foreach (var iv in Intervals(o, dir, reach)) if (iv.t0 <= 1e-6 && iv.t1 >= -1e-6) return iv;
            return null;
        }
    }

    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ rivets that share a hole with a rivet already placed
        private readonly List<(Vec3 o, Vec3 z, Vec3 x, double grip, double dia, string asm, string? mat, Assumption? asu)> _pendingConn = new();
        private Dictionary<(long, long, long), List<int>>? _rivetCells;
        private const double RivetCell = 0.15;
        private static (long, long, long) CellOf(Vec3 p) => ((long)Math.Floor(p.X / RivetCell), (long)Math.Floor(p.Y / RivetCell), (long)Math.Floor(p.Z / RivetCell));
        private void IndexRivet(int i)
        {
            var b = Model.Steel.Bolts[i];
            var mid = b.Frame.Origin + b.Frame.Z * (b.Grip / 2);
            var k = CellOf(mid);
            if (!_rivetCells!.TryGetValue(k, out var l)) _rivetCells[k] = l = new();
            l.Add(i);
        }

        /// <summary>Places a rivet unless one already placed shares its hole (parallel shank, axes closer than 0.8 (d1 + d2) = their button heads would overlap: two layouts of the same drawn rivet, shanks overlapping or
        /// touching along the axis): that rivet is replaced by ONE rivet through both stacks (its diameter and axis, union of the grips, head at this rivet's end).
        /// True = shared.</summary>
        private bool MergeRivet(Vec3 origin, Vec3 z, Vec3 xHint, double grip, double dia, string asm, string? material, Assumption? asu)
        {
            var bolts = Model.Steel.Bolts;
            if (_rivetCells == null) { _rivetCells = new(); for (int i = 0; i < bolts.Count; i++) if (bolts[i].Kind == BoltKind.Rivet) IndexRivet(i); }
            var zz = z.Normalized();
            var mid = origin + zz * (grip / 2);
            var (cx, cy, cz) = CellOf(mid);
            for (long ix = cx - 1; ix <= cx + 1; ix++)
                for (long iy = cy - 1; iy <= cy + 1; iy++)
                    for (long iz = cz - 1; iz <= cz + 1; iz++)
                    {
                        if (!_rivetCells.TryGetValue((ix, iy, iz), out var l)) continue;
                        foreach (int i in l)
                        {
                            var b = bolts[i];
                            if (b.Kind != BoltKind.Rivet || Math.Abs(Vec3.Dot(b.Frame.Z, zz)) < 0.985) continue;
                            // the other shank on this axis: t range, and the perpendicular distance of its axis
                            double ta = Vec3.Dot(b.Frame.Origin - origin, zz), tb = ta + b.Grip * Vec3.Dot(b.Frame.Z, zz);
                            var perp = (b.Frame.Origin - origin) - zz * ta;
                            if (perp.Length > 0.8 * (dia + b.Diameter)) continue;
                            double lo = Math.Min(ta, tb), hi = Math.Max(ta, tb);
                            if (lo > grip + 0.002 || hi < -0.002) continue;
                            double u0 = Math.Min(0, lo), u1 = Math.Max(grip, hi);
                            bolts[i] = new BoltInstance
                            {
                                Frame = new Frame3(origin + perp + zz * u0, b.Frame.X, Vec3.Cross(zz, b.Frame.X).Normalized(), zz), Grip = Grip16(u1 - u0), Length = Grip16(u1 - u0), Assembly = b.Assembly,
                                Kind = BoltKind.Rivet, Diameter = Math.Max(b.Diameter, dia), Material = b.Material ?? material, Assumption = b.Assumption ?? asu, FlushEnd = b.FlushEnd,
                            };
                            return true;
                        }
                    }
            AddRivet(origin, z, xHint, grip, dia, asm, material, asu);
            IndexRivet(bolts.Count - 1);
            return false;
        }

        /// <summary>End connection / stiffener rivets after the joint rivets: one rivet per hole (an outstanding-leg rivet on a gusset where the joint group
        /// already has a rivet becomes that rivet, through the angle too).</summary>
        private void FlushConnectionRivets()
        {
            if (_pendingConn.Count == 0) return;
            int shared = 0;
            foreach (var r in _pendingConn) if (MergeRivet(r.o, r.z, r.x, r.grip, r.dia, r.asm, r.mat, r.asu)) shared++;
            Info($"Truss: end connection / stiffener rivets - {_pendingConn.Count - shared} placed, {shared} in a hole already taken by a joint / other rivet (one rivet through the whole stack, the larger diameter){Src}.");
            _pendingConn.Clear();
        }

        // ------------------------------------------------------------------ perforated plates (access holes cut)
        /// <summary>Builds a perforated plate piece (<see cref="TrussPerforation"/>): edge strips swept along the member + the pieces between the holes
        /// swept through the thickness. False = not possible (holes do not fit) -> the caller builds the plate solid.</summary>
        private bool Perforated(Mesh mesh, Inst m, Vec3 a0, TrussSectionPiece pc, double ps, double pl, double cut, out int holes)
        {
            holes = 0;
            var pf = pc.Perforation!;
            if (pf.Width <= 0 || pf.Length < pf.Width - 1e-9 || pf.Pitch < pf.Length) return false;
            double x0 = pc.Polygon.Min(q => q.X), x1 = pc.Polygon.Max(q => q.X), y0 = pc.Polygon.Min(q => q.Y), y1 = pc.Polygon.Max(q => q.Y);
            bool wideX = x1 - x0 >= y1 - y0;
            double w0 = wideX ? x0 : y0, w1 = wideX ? x1 : y1, t0 = wideX ? y0 : x0, t1 = wideX ? y1 : x1;
            double r = pf.Width / 2, c = (w0 + w1) / 2 + pf.CenterOffset;
            if (c - r < w0 + In(1) || c + r > w1 - In(1)) return false;
            // free length: between the end gussets (inside the piece's own span)
            if (!_gussetSpan.TryGetValue(m, out var g))
            {
                double len = (m.B - m.A).Length;
                _gussetSpan[m] = g = (Math.Max(0, GussetReach(m, true) - m.SA), Math.Min(cut, len - GussetReach(m, false) - m.SA));
            }
            double f0 = Math.Max(ps, g.a) + pf.EndClear, f1 = Math.Min(ps + pl, g.b) - pf.EndClear;
            double avail = f1 - f0;
            if (avail < pf.Length) return false;
            double mid = (f0 + f1) / 2, half = (pf.Length - pf.Width) / 2;
            var centres = new List<double>();
            int nMax = (int)Math.Floor((avail - pf.Length) / pf.Pitch) + 2;
            for (int k = -nMax; k <= nMax; k++)
            {
                double cc = mid + k * pf.Pitch + (pf.Stagger ? pf.Pitch / 2 : 0);
                if (cc - pf.Length / 2 >= f0 - 1e-9 && cc + pf.Length / 2 <= f1 + 1e-9) centres.Add(cc);
            }
            if (centres.Count == 0) return false;
            holes = centres.Count;
            Vec3 Map(double a, double w, double t) => a0 + m.Axis * a + (wideX ? m.X * w + m.Y * t : m.X * t + m.Y * w);
            // edge strips along the member
            foreach (var (sa, sb) in new[] { (w0, c - r), (c + r, w1) })
            {
                var poly = wideX ? new[] { new Vec2(sa, t0), new Vec2(sb, t0), new Vec2(sb, t1), new Vec2(sa, t1) } : new[] { new Vec2(t0, sa), new Vec2(t1, sa), new Vec2(t1, sb), new Vec2(t0, sb) };
                var ra = poly.Select(q => a0 + m.Axis * ps + m.X * q.X + m.Y * q.Y).ToArray();
                MeshBuilder.AddLoft(mesh, poly, new[] { ra, ra.Select(p => p + m.Axis * pl).ToArray() });
            }
            // middle strip pieces in (a, w), concave semicircle ends (8 segments each)
            const int seg = 8;
            IEnumerable<Vec2> ArcLeft(double ac) { for (int i = 0; i <= seg; i++) { double th = Math.PI * i / seg; yield return new Vec2(ac - r * Math.Sin(th), c - r * Math.Cos(th)); } }   // (ac, c-r) -> (ac, c+r) bulging to -a
            IEnumerable<Vec2> ArcRight(double ac) { for (int i = 0; i <= seg; i++) { double th = Math.PI * i / seg; yield return new Vec2(ac + r * Math.Sin(th), c + r * Math.Cos(th)); } }  // (ac, c+r) -> (ac, c-r) bulging to +a
            var pieces = new List<List<Vec2>>();
            double aEnd = ps + pl;
            var first = new List<Vec2> { new(ps, c - r) }; first.AddRange(ArcLeft(centres[0] - half)); first.Add(new(ps, c + r));
            pieces.Add(first);
            for (int k = 0; k + 1 < centres.Count; k++)
            {
                var p = new List<Vec2>(); p.AddRange(ArcLeft(centres[k + 1] - half)); p.AddRange(ArcRight(centres[k] + half));
                pieces.Add(p);
            }
            var last = new List<Vec2> { new(aEnd, c - r), new(aEnd, c + r) }; last.AddRange(ArcRight(centres[^1] + half));
            pieces.Add(last);
            foreach (var p in pieces)
            {
                var poly = p.ToArray();
                if (MeshBuilder.SignedArea(poly) < 0) Array.Reverse(poly);
                var ra = poly.Select(q => Map(q.X, q.Y, t0)).ToArray();
                var rb = poly.Select(q => Map(q.X, q.Y, t1)).ToArray();
                MeshBuilder.AddLoft(mesh, poly, new[] { ra, rb });
            }
            return true;
        }

        // ------------------------------------------------------------------ end connections (framed floor beams / brackets) and stiffener rivets
        private readonly List<(Inst m, TrussStiffenerSet set, double tw, double yc, Assumption? asu)> _stiffRivets = new();

        private Vec3 EndPoint(Inst m, bool start) => start ? m.A : m.B;

        private void BuildEndConnections()
        {
            var conns = _inst.Where(i => i.Def.EndConnections is { Count: > 0 }).ToList();
            if (conns.Count == 0 && _stiffRivets.Count == 0) return;
            // part bounding boxes once
            var parts = Model.Steel.Parts;
            var boxes = parts.Select(p => p.Mesh is { IsEmpty: false } ? p.Mesh.Bounds() : default).ToList();
            int nAngles = 0, nPlates = 0, nRiv = 0, nLoose = 0, nNoWeb = 0, nWebExt = 0;
            var backs = new List<string>();
            SteelLineProbe Probe(Vec3 lo, Vec3 hi, string? exclude, IEnumerable<Mesh>? extra = null)
            {
                var pr = new SteelLineProbe();
                for (int i = 0; i < parts.Count; i++)
                {
                    var b = boxes.Count > i ? boxes[i] : default;
                    if (parts[i].Mesh is not { IsEmpty: false } || parts[i].Mark == exclude) continue;
                    if (b.Max.X < lo.X || b.Min.X > hi.X || b.Max.Y < lo.Y || b.Min.Y > hi.Y || b.Max.Z < lo.Z || b.Min.Z > hi.Z) continue;
                    pr.Add(parts[i].Mesh, i, lo, hi);
                }
                int k = parts.Count;
                if (extra != null) foreach (var me in extra) pr.Add(me, k++, lo, hi);
                return pr;
            }
            (Vec3 lo, Vec3 hi) Box(IEnumerable<Vec3> pts, double grow)
            {
                var l = pts.ToList();
                return (new Vec3(l.Min(p => p.X) - grow, l.Min(p => p.Y) - grow, l.Min(p => p.Z) - grow), new Vec3(l.Max(p => p.X) + grow, l.Max(p => p.Y) + grow, l.Max(p => p.Z) + grow));
            }

            foreach (var m in conns)
            {
                double tw = m.Sec.WebThickness > 0 ? m.Sec.WebThickness : 2 * m.Sec.HalfWidth;
                double yWebTop = m.Sec.WebCenterY + (m.Sec.WebDepth > 0 ? m.Sec.WebDepth / 2 : m.Sec.YMax), yWebBot = m.Sec.WebCenterY - (m.Sec.WebDepth > 0 ? m.Sec.WebDepth / 2 : -m.Sec.YMin);
                if (m.Sec.Definition.Kind == TrussSectionKind.H && m.Sec.Definition.WebDepth > 0) { yWebTop = m.Sec.Definition.WebDepth / 2; yWebBot = -m.Sec.Definition.WebDepth / 2; }
                int ci = 0;
                foreach (var ec in m.Def.EndConnections!)
                {
                    ci++;
                    foreach (bool start in new[] { true, false })
                    {
                        if (ec.End == TrussMemberEnd.Start && !start || ec.End == TrussMemberEnd.End && start) continue;
                        var wp = EndPoint(m, start);
                        var din = start ? m.Axis : -m.Axis;        // from the end working point into the member
                        double setb = start ? m.SA : m.SB;
                        var Yv = m.Y; var Xv = m.X;
                        double zoff = m.Def.OffsetZ;
                        Vec3 P(double s, double x, double y) => wp + din * s + Xv * x + Yv * (y + zoff);
                        string endTag = start ? "A" : "B";
                        string where = $"{m.Mark} {(start ? m.PointA : m.PointB)} end";
                        (double legOut, double legWeb, double t)? ang = null;
                        if (ec.Angle.Length > 0)
                        {
                            ang = TrussSections.ParseAngle(ec.Angle);
                            if (ang == null && SteelCatalog.FindAngle(ec.Angle) is { } ac) ang = (ac.LegA, ac.LegB, ac.Thickness);
                            if (ang == null) { Warn($"Truss: end connection '{ec.Label}' of {where} - angle '{ec.Angle}' not understood; not built."); continue; }
                        }
                        if (ec.Length <= 0) { Warn($"Truss: end connection '{ec.Label}' of {where} - length missing; not built."); continue; }
                        double heel = tw / 2 + Math.Max(0, ec.FillThickness) + Math.Max(0, ec.StandOff);
                        double yTop = ec.Top, yBot = ec.Top - ec.Length;
                        // back of the outstanding legs / plate end
                        double back;
                        if (ec.Back is { } bk) back = bk;
                        else
                        {
                            // the whole bearing face of the angle / plate: 3 points across x 7 levels per side
                            double x0 = ang != null ? heel + In(0.125) : tw / 2 + In(0.0625), x1 = ang != null ? heel + ang.Value.legOut - In(0.125) : tw / 2 + Math.Max(ec.PlateThickness, In(0.25)) - In(0.0625);
                            var probePts = new List<Vec3>();
                            for (int iy = 0; iy < 7; iy++)
                            {
                                double y = yTop - 0.01 - (yTop - yBot - 0.02) * iy / 6;
                                foreach (double xr in new[] { x0, (x0 + x1) / 2, x1 })
                                    foreach (int sx in new[] { -1, 1 }) probePts.Add(P(setb, sx * xr, y));
                            }
                            var (lo, hi) = Box(probePts.Concat(probePts.Select(q => q - din * (setb + 0.05))), 0.05);
                            var pr = Probe(lo, hi, m.Mark);
                            double best = double.NegativeInfinity;
                            foreach (var q in probePts)
                            {
                                // first steel face met going from the cut end back toward the working point
                                foreach (var iv in pr.Intervals(q, -din, setb + 0.05))
                                    if (iv.t1 >= 0) { best = Math.Max(best, setb - iv.t0); break; }   // a cut end already inside the steel: its face toward the member
                            }
                            if (double.IsNegativeInfinity(best)) { back = 0; Warn($"Truss: end connection '{ec.Label}' of {where} - no steel face found between the member end and its working point; the angles stand at the working point."); }
                            else back = best;
                            back += ec.BackOffset;
                            backs.Add($"{where}: {L(back)}");
                        }
                        Assumption? asu = ec.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(ec.Unresolved + Src, "connection as listed") : null;
                        if (asu != null) Warn(ec.Unresolved);
                        var meshA = new Mesh(); var meshF = new Mesh(); var meshP = new Mesh(); var meshW = new Mesh();
                        // section (a along din from the working point, x across) prism over y [yBot, yTop]
                        void Prism(Mesh mesh, IReadOnlyList<Vec2> poly, double ya, double yb)
                        {
                            var pl = poly.ToArray();
                            if (MeshBuilder.SignedArea(pl) < 0) Array.Reverse(pl);
                            var ra = pl.Select(q => P(q.X, q.Y, ya)).ToArray();
                            MeshBuilder.AddLoft(mesh, pl, new[] { ra, ra.Select(p => p + Yv * (yb - ya)).ToArray() });
                        }
                        foreach (int sx in new[] { -1, 1 })
                        {
                            double xw = sx * (tw / 2 + Math.Max(0, ec.StandOff));
                            if (ang is { } a)
                            {
                                double xh = sx * heel;
                                // outstanding leg along x from the heel (back at a = back), web leg along a into the member
                                Prism(meshA, new[] { new Vec2(back, xh), new Vec2(back + a.legWeb, xh), new Vec2(back + a.legWeb, xh + sx * a.t), new Vec2(back + a.t, xh + sx * a.t),
                                    new Vec2(back + a.t, xh + sx * a.legOut), new Vec2(back, xh + sx * a.legOut) }, yBot, yTop);
                                nAngles++;
                                if (ec.FillThickness > 0)
                                {
                                    double fw = ec.FillWidth > 0 ? ec.FillWidth : a.legWeb;
                                    double fTop = ec.FillTop ?? yTop, fLen = ec.FillLength > 0 ? ec.FillLength : ec.Length;
                                    Prism(meshF, new[] { new Vec2(back, xw), new Vec2(back + fw, xw), new Vec2(back + fw, xw + sx * ec.FillThickness), new Vec2(back, xw + sx * ec.FillThickness) }, fTop - fLen, fTop);
                                }
                            }
                            if (ec.PlateOutline is { Count: >= 3 } po && ec.PlateThickness > 0)
                            {
                                // plate in the web plane (u from the back along the member, v = section y), swept across the web normal
                                var pl2 = po.Select(q => new Vec2(back + q.U, q.V)).ToArray();
                                if (MeshBuilder.SignedArea(pl2) < 0) Array.Reverse(pl2);
                                var ra = pl2.Select(q => P(q.X, xw, q.Y)).ToArray();
                                MeshBuilder.AddLoft(meshP, pl2, new[] { ra, ra.Select(p => p + Xv * (sx * ec.PlateThickness)).ToArray() });
                                nPlates++;
                            }
                            else if (ec.PlateWidth > 0 && ec.PlateThickness > 0)
                            {
                                Prism(meshP, new[] { new Vec2(back, xw), new Vec2(back + ec.PlateWidth, xw), new Vec2(back + ec.PlateWidth, xw + sx * ec.PlateThickness), new Vec2(back, xw + sx * ec.PlateThickness) }, yBot, yTop);
                                nPlates++;
                            }
                        }
                        // web extension: from the member cut back to the connection, over the connection height
                        if (ec.ExtendWeb && setb > back + ec.WebClearance + 1e-4)
                        {
                            double ya = Math.Max(yBot, yWebBot), yb = Math.Min(yTop, yWebTop);
                            if (yb > ya + 1e-4)
                            {
                                Prism(meshW, new[] { new Vec2(back + ec.WebClearance, -tw / 2), new Vec2(setb, -tw / 2), new Vec2(setb, tw / 2), new Vec2(back + ec.WebClearance, tw / 2) }, ya, yb);
                                nWebExt++;
                            }
                        }
                        string lbl = ec.Label.Length > 0 ? ec.Label : ec.Angle;
                        var newMeshes = new List<Mesh>();
                        void Part(Mesh mesh, string suffix, PartCategory cat, string desc, double length)
                        {
                            if (mesh.IsEmpty) return;
                            string mark = $"{m.Mark}-EC{ci}{endTag}{suffix}";
                            Model.Steel.Parts.Add(new SteelPart { Mark = mark, Category = cat, Assembly = m.Assembly, Mesh = mesh, Material = Material(ec.Material.Length > 0 ? ec.Material : m.Sec.Definition.Material),
                                Length = length, Assumption = asu, Description = desc });
                            boxes.Add(mesh.Bounds());
                            if (Model.PanelOf.TryGetValue(m.Mark, out var pnl)) Model.PanelOf[mark] = pnl;
                            newMeshes.Add(mesh);
                        }
                        Part(meshA, "", PartCategory.TrussConnectionAngle, $"connection {ec.Angle} x 2 x {L(ec.Length)} ({lbl}) at {where}", ec.Length);
                        Part(meshF, "F", PartCategory.TrussConnectionPlate, $"fill {L(ec.FillThickness)} x 2 under the connection angles ({lbl}) at {where}", ec.FillLength > 0 ? ec.FillLength : ec.Length);
                        Part(meshP, "P", PartCategory.TrussConnectionPlate, $"end PL {L(ec.PlateWidth)} x {L(ec.PlateThickness)} x 2 x {L(ec.Length)} ({lbl}) at {where}", ec.Length);
                        Part(meshW, "W", m.Def.Role == TrussMemberRole.FloorBeam ? PartCategory.TrussFloorBeam : CategoryOf(m.Def.Role), $"web {L(tw)} extended to the connection ({lbl}) at {where}", setb - back);
                        // rivets: web columns (shank across the web), outstanding leg columns (shank along the member)
                        if ((ec.WebRivets is { Count: > 0 } || ec.OutRivets is { Count: > 0 }) && ec.RivetDiameter > 0)
                        {
                            var corners = new List<Vec3>();
                            double reachX = heel + (ang?.legOut ?? ec.PlateThickness) + 0.3;
                            foreach (double y in new[] { yTop, yBot }) foreach (int sx in new[] { -1, 1 }) foreach (double s in new[] { back - 0.4, back + 0.6 }) corners.Add(P(s, sx * reachX, y));
                            var (lo, hi) = Box(corners, 0.05);
                            var pr = Probe(lo, hi, null, null);
                            var rasu = RivetFlag(ec.RivetUnresolved, "rivet columns at the drawn pitch / gage") ?? asu;
                            string asmR = m.Assembly;
                            foreach (var col in ec.WebRivets ?? new())
                                for (int k = 0; k < col.Count; k++)
                                {
                                    double y = yTop - col.First - k * col.Pitch;
                                    var q = P(back + col.Gage, 0, y);
                                    if (pr.At(q, Xv, 0.5) is not { } iv) { nNoWeb++; continue; }
                                    _pendingConn.Add((q + Xv * iv.t1, -Xv, din, iv.t1 - iv.t0, ec.RivetDiameter, asmR, ec.RivetMaterial, rasu));
                                    nRiv++;
                                }
                            if (ang is { } a2)
                                foreach (var col in ec.OutRivets ?? new())
                                    foreach (int sx in new[] { -1, 1 })
                                        for (int k = 0; k < col.Count; k++)
                                        {
                                            double y = yTop - col.First - k * col.Pitch;
                                            var q = P(back + a2.t / 2, sx * (heel + col.Gage), y);
                                            if (pr.At(q, din, 0.6) is not { } iv) { nNoWeb++; continue; }
                                            if (iv.t1 - iv.t0 < a2.t + In(1.0 / 16)) nLoose++;
                                            _pendingConn.Add((q + din * iv.t1, -din, Xv, iv.t1 - iv.t0, ec.OutRivetDiameter > 0 ? ec.OutRivetDiameter : ec.RivetDiameter, asmR, ec.RivetMaterial, rasu));
                                            nRiv++;
                                        }
                        }
                    }
                }
            }
            // stiffener rivets (web leg / fill / web)
            foreach (var (m, set, tw, yc, asu) in _stiffRivets)
            {
                foreach (double d in set.Distances)
                {
                    var corners = new List<Vec3>();
                    foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 }) foreach (int sa in new[] { -1, 1 })
                        corners.Add(m.A + m.Axis * (d + sa * 0.3) + m.X * (sx * 0.3) + m.Y * (yc + sy * set.Height / 2));
                    var (lo, hi) = Box(corners, 0.05);
                    var pr = Probe(lo, hi, null);
                    var rasu = RivetFlag(set.RivetUnresolved, "stiffener rivets at the drawn pitch") ?? asu;
                    int sgn = set.LegToward != 0 ? set.LegToward : 1;
                    foreach (var col in set.Rivets!)
                        for (int k = 0; k < col.Count; k++)
                        {
                            double y = (set.Top is { } st0 ? st0 + m.Def.OffsetZ : yc + set.Height / 2) - col.First - k * col.Pitch;
                            var (ax2, vy) = StiffFrame(m, set);
                            var q = m.A + m.Axis * d + ax2 * (sgn * col.Gage) + vy * y;
                            if (pr.At(q, m.X, 0.5) is not { } iv) { nNoWeb++; continue; }
                            _pendingConn.Add((q + m.X * iv.t1, -m.X, m.Axis, iv.t1 - iv.t0, set.RivetDiameter, m.Assembly, null, rasu));
                            nRiv++;
                        }
                }
            }
            Info($"Truss: end connections - {nAngles} connection angles, {nPlates} end plates, {nWebExt} web extensions, {nRiv} rivets{(nNoWeb > 0 ? $" ({nNoWeb} rivet positions not in steel, skipped)" : "")}{Src}.");
            if (nLoose > 0) Warn($"Truss CHECK end connections: {nLoose} rivets through the outstanding legs find no steel behind the leg (gap to the post / girder face over 1 mm; fills not drawn?) - placed with the leg as grip{Src}.");
            if (backs.Count > 0) Model.ConnectionBacks.AddRange(backs);
        }

        private readonly Dictionary<TrussLineItemDefinition, List<TrussLineItemRivet>> _clipRivets = new(ReferenceEqualityComparer.Instance);
        /// <summary>Clip angles of a transverse framed item (<see cref="TrussClipAngles"/>): one part per item, rivets added to the item's rivet list.</summary>
        private void BuildClipAngles(TrussLineItemDefinition li)
        {
            var ca = li.ClipAngles!;
            if (li.To <= li.From || li.Profile.Count < 3) return;     // the item itself is not built (BuildLineItem)
            var ad = TrussSections.ParseAngle(ca.Angle);
            if (ad == null && SteelCatalog.FindAngle(ca.Angle) is { } ac) ad = (ac.LegA, ac.LegB, ac.Thickness);
            if (ad == null || ca.Length <= 0) { Warn($"Truss: clip angles of {li.Name} - angle '{ca.Angle}' / length not understood; not built."); return; }
            var (legItem, legWeb, t) = ad.Value;
            int tw = ca.Toward >= 0 ? 1 : -1;
            var mesh = new Mesh();
            var cr = _clipRivets[li] = new List<TrussLineItemRivet>();
            foreach (var (w0, dir) in new[] { (li.From, 1), (li.To, -1) })
            {
                // (U, W) polygon: item leg U in [h, h + tw t] (on the item web), W from w0 into the item; web leg along U toward tw from the heel, W in [w0, w0 + dir t]
                double h = ca.HeelU;
                var poly = new List<Vec2>
                {
                    new(h, w0), new(h + tw * legWeb, w0), new(h + tw * legWeb, w0 + dir * t), new(h + tw * t, w0 + dir * t), new(h + tw * t, w0 + dir * legItem), new(h, w0 + dir * legItem),
                };
                var pl = poly.ToArray();
                if (MeshBuilder.SignedArea(pl) < 0) Array.Reverse(pl);
                double vb = ca.Top - ca.Length;
                Vec3 W(Vec2 q, double v) => _al.PointAt(li.Station + q.X, q.Y).WithZ((li.FromGrade ? _al.ElevationAt(li.Station, q.Y) : 0) + v);
                var ra = pl.Select(q => W(q, vb)).ToArray();
                MeshBuilder.AddLoft(mesh, pl, new[] { ra, ra.Select(p => p + Up * ca.Length).ToArray() });
                if (ca.ItemLegRivets is { Count: > 0 } ir)
                    for (int k = 0; k < ir.Count; k++)
                        cr.Add(new TrussLineItemRivet { U = h + tw * t / 2, W = w0 + dir * ir.Gage, V = ca.Top - ir.First - k * ir.Pitch, Axis = "U", Diameter = ca.RivetDiameter, Unresolved = ca.RivetUnresolved });
                if (ca.WebLegRivets is { Count: > 0 } wr)
                    for (int k = 0; k < wr.Count; k++)
                        cr.Add(new TrussLineItemRivet { U = h + tw * wr.Gage, W = w0 + dir * t / 2, V = ca.Top - wr.First - k * wr.Pitch, Axis = "W", Diameter = ca.RivetDiameter, Unresolved = ca.RivetUnresolved });
            }
            string asm = Asm(li.Assembly.Length > 0 ? li.Assembly : "DECK-ITEMS", AssemblyKind.InspectionWalkway, li.AssemblyDescription.Length > 0 ? li.AssemblyDescription : $"{_d.Name} - {li.Assembly}");
            Model.Steel.Parts.Add(new SteelPart
            {
                Mark = li.Name + "-CA", Category = PartCategory.TrussConnectionAngle, Assembly = asm, Mesh = mesh, Material = ca.Material.Length > 0 ? Material(ca.Material) : _d.DefaultMaterial,
                Length = ca.Length, Description = $"clip angles {ca.Angle} x 2 x {L(ca.Length)} ({li.Name})",
                Assumption = ca.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(ca.Unresolved, "clip angles at both ends of the item") : null,
            });
            if (ca.Unresolved.Length > 0) Warn(ca.Unresolved);
        }

        /// <summary>Rivets of steel line items (<see cref="TrussLineItemDefinition.Rivets"/>), after every part is built.</summary>
        private void PlaceLineItemRivets()
        {
            var parts = Model.Steel.Parts;
            var boxes = parts.Select(p => p.Mesh is { IsEmpty: false } ? p.Mesh.Bounds() : default).ToList();
            int n = 0, miss = 0, shared = 0;
            // one rivet per hole: two items whose angles sit back to back on one web (diaphragms of adjacent bays) list the same rivet twice
            foreach (var li in _d.LineItems!.Where(l => (l.Rivets is { Count: > 0 } || _clipRivets.ContainsKey(l)) && l.Kind == TrussLineItemKind.Steel))
            {
                string asm = li.Assembly.Length > 0 ? li.Assembly : "DECK-ITEMS";
                foreach (var rv in (li.Rivets ?? new()).Concat(_clipRivets.TryGetValue(li, out var cl) ? cl : new()))
                {
                    Vec3 at, du, dw;
                    if (li.Transverse)
                    {
                        var p0 = _al.PointAt(li.Station + rv.U, rv.W);
                        double z = (li.FromGrade ? _al.ElevationAt(li.Station, rv.W) : 0) + rv.V;
                        at = p0.WithZ(z);
                        du = Tangent(li.Station); dw = Vec3.Cross(du, Up).Normalized();
                    }
                    else
                    {
                        int side = li.Side >= 0 ? 1 : -1;
                        double off = li.ReferenceOffset + side * rv.W;
                        at = _al.PointAt(rv.U, off).WithZ((li.FromGrade ? _al.ElevationAt(rv.U, li.ReferenceOffset) : 0) + rv.V);
                        du = Tangent(rv.U); dw = Vec3.Cross(du, Up).Normalized() * side;
                    }
                    var ax = rv.Axis.ToUpperInvariant() switch { "U" => du, "V" => Up, _ => dw };
                    var lo = new Vec3(at.X - 0.4, at.Y - 0.4, at.Z - 0.4); var hi = new Vec3(at.X + 0.4, at.Y + 0.4, at.Z + 0.4);
                    var pr = new SteelLineProbe();
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var b = boxes[i];
                        if (parts[i].Mesh is not { IsEmpty: false } || b.Max.X < lo.X || b.Min.X > hi.X || b.Max.Y < lo.Y || b.Min.Y > hi.Y || b.Max.Z < lo.Z || b.Min.Z > hi.Z) continue;
                        pr.Add(parts[i].Mesh, i, lo, hi);
                    }
                    if (pr.At(at, ax, 0.35) is not { } iv) { miss++; continue; }
                    var asu = RivetFlag(rv.Unresolved ?? (li.Unresolved.Length > 0 ? li.Unresolved : null), "rivets of the line item at the drawn positions");
                    if (MergeRivet(at + ax * iv.t1, -ax, ax.Z > 0.9 ? du : Up, iv.t1 - iv.t0, rv.Diameter, asm, rv.Material, asu)) shared++; else n++;
                }
            }
            Info($"Truss: line item rivets - {n} placed{(shared > 0 ? $", {shared} in a hole already taken (angles back to back on one web / another group's rivet: one rivet through the whole stack)" : "")}{(miss > 0 ? $", {miss} positions not in steel (skipped)" : "")}{Src}.");
        }
    }
}
