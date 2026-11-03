using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    /// <summary>Test hook: the filleted path of a plane item (corners replaced by the circumscribed arc polygon).</summary>
    public static List<Vec2> FilletPathForTest(IReadOnlyList<TrussPathVertex> path, double stepDeg) => Gen.FilletPath(path, stepDeg);

    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ pieces in a vertical plane through two points (TrussDefinition.PlaneItems)
        /// <summary>Path vertices with every filleted corner replaced by tangent arc points (at most <paramref name="stepDeg"/> per piece).</summary>
        internal static List<Vec2> FilletPath(IReadOnlyList<TrussPathVertex> path, double stepDeg, List<string>? notes = null)
        {
            var o = new List<Vec2>();
            for (int i = 0; i < path.Count; i++)
            {
                var p = new Vec2(path[i].U, path[i].V);
                if (i == 0 || i == path.Count - 1 || path[i].Radius <= 0) { o.Add(p); continue; }
                var a = (p - new Vec2(path[i - 1].U, path[i - 1].V)); var b = (new Vec2(path[i + 1].U, path[i + 1].V) - p);
                double la = a.Length, lb = b.Length;
                if (la < 1e-9 || lb < 1e-9) { o.Add(p); continue; }
                a /= la; b /= lb;
                double turn = Math.Acos(Math.Clamp(a.X * b.X + a.Y * b.Y, -1, 1));
                if (turn < 1e-6) { o.Add(p); continue; }
                double R = path[i].Radius, T = R * Math.Tan(turn / 2);
                if (T > Math.Min(la, lb) + 1e-9) { notes?.Add($"fillet R {R:0.###} m at vertex {i} needs {T:0.###} m of tangent, the legs are {la:0.###} / {lb:0.###} m: sharp corner kept"); o.Add(p); continue; }
                double side = Math.Sign(a.X * b.Y - a.Y * b.X);                 // + = turning left
                var t1 = p - a * T;
                var c = t1 + new Vec2(-a.Y, a.X) * (side * R);
                // circumscribed polygon of the arc: the first / last pieces run along the tangents at the arc ends (square piece ends stay square to the
                // straight legs, e.g. a mirrored half meeting its twin on a vertical plane)
                int n = Math.Max(1, (int)Math.Ceiling(turn / (stepDeg * Math.PI / 180)));
                double a0 = Math.Atan2(t1.Y - c.Y, t1.X - c.X), dA = turn / n, rr = R / Math.Cos(dA / 2);
                o.Add(t1);
                for (int k = 1; k <= n; k++)
                {
                    double ang = a0 + side * dA * (k - 0.5);
                    o.Add(c + new Vec2(Math.Cos(ang), Math.Sin(ang)) * rr);
                }
                o.Add(p + b * T);
            }
            // drop zero-length pieces
            var r = new List<Vec2>();
            foreach (var q in o) if (r.Count == 0 || (q - r[^1]).Length > 1e-6) r.Add(q);
            return r;
        }

        /// <summary>Frames of the plane items that carry rivets (<see cref="TrussPlaneItemDefinition.Rivets"/>): origin, U, V, W, length A-B (mirror), assembly.</summary>
        private readonly List<(TrussPlaneItemDefinition it, Vec3 a, Vec3 u, Vec3 v, Vec3 w, double L, bool mirror, string asm)> _itemRivetFrames = new();

        private void BuildPlaneItems()
        {
            if (_d.PlaneItems is not { Count: > 0 } items) return;
            int nParts = 0; double mass = 0;
            foreach (var it in items)
            {
                Vec3 a, u, w, vAx = Up;
                double L = 0;
                if (it.Frame is { } fr)
                {
                    // OPTIONAL explicit frame: origin at (station, offset, elevation), U = the tangent turned toward the right
                    var t2 = Vec2.FromAngle(_al.AngleAt(fr.Station));
                    double th = fr.AngleDegrees * Math.PI / 180;
                    var u2 = t2 * Math.Cos(th) + t2.Right * Math.Sin(th);
                    a = _al.PointAt(fr.Station, fr.Offset).WithZ(fr.Elevation);
                    u = new Vec3(u2.X, u2.Y, 0).Normalized();
                    if (fr.Horizontal) { vAx = Vec3.Cross(Up, u).Normalized(); w = Up; }
                    else w = Vec3.Cross(u, Up).Normalized();
                    // OPTIONAL pitch (U rises) then roll (about U)
                    if (fr.PitchDegrees != 0)
                    {
                        double pp = fr.PitchDegrees * Math.PI / 180;
                        var u1 = (u * Math.Cos(pp) + Up * Math.Sin(pp)).Normalized();
                        if (fr.Horizontal) w = Vec3.Cross(u1, vAx).Normalized();
                        else vAx = Vec3.Cross(w, u1).Normalized();
                        u = u1;
                    }
                    if (fr.RollDegrees != 0)
                    {
                        double rr = fr.RollDegrees * Math.PI / 180;
                        var v1 = (vAx * Math.Cos(rr) + w * Math.Sin(rr)).Normalized();
                        w = Vec3.Cross(u, v1).Normalized(); vAx = v1;
                    }
                    if (it.Mirror) Warn($"Truss: plane item {it.Name} - Mirror is not used with an explicit frame{Src}.");
                }
                else
                {
                    var (_, _, A) = Resolve(it.PointA, _d.Planes.FirstOrDefault()?.Name ?? "");
                    var (_, _, B) = Resolve(it.PointB, _d.Planes.FirstOrDefault()?.Name ?? "");
                    if (A is not { } pa || B is not { } pb) { Warn($"Truss: plane item {it.Name} - point {it.PointA} or {it.PointB} not defined; not built{Src}."); continue; }
                    a = pa;
                    var d2 = new Vec2(pb.X - pa.X, pb.Y - pa.Y);
                    L = d2.Length;
                    if (L < 1e-6) { Warn($"Truss: plane item {it.Name} - its two points are plumb over each other; not built{Src}."); continue; }
                    u = new Vec3(d2.X / L, d2.Y / L, 0);
                    w = Vec3.Cross(u, Up).Normalized();
                }
                Assumption? asu = it.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(it.Unresolved + Src, "piece as listed / scaled") : null;
                if (asu != null) Warn(it.Unresolved);
                string asm = Asm(it.Assembly.Length > 0 ? it.Assembly : "BR-PORTALS", it.AssemblyKind ?? AssemblyKind.TrussBracing, $"{_d.Name} - {(it.AssemblyDescription.Length > 0 ? it.AssemblyDescription : "Portals")}");
                var notes = new List<string>();
                var path = it.Kind == TrussPlaneItemKind.Sweep ? FilletPath(it.Path, it.ArcStepDegrees > 0 ? it.ArcStepDegrees : 5, notes) : new List<Vec2>();
                foreach (var nt in notes) Warn($"Truss: plane item {it.Name} - {nt}{Src}.");
                if (it.Rivets is { Count: > 0 }) _itemRivetFrames.Add((it, a, u, vAx, w, L, it.Mirror && it.Frame == null, asm));
                foreach (bool mir in it.Mirror && it.Frame == null ? new[] { false, true } : new[] { false })
                {
                    Vec3 W(double uu, double vv, double ww) => a + u * (mir ? L - uu : uu) + vAx * vv + w * ww;
                    var mesh = new Mesh();
                    double len = 0;
                    if (it.Kind == TrussPlaneItemKind.Plate)
                    {
                        if (it.Outline.Count < 3 || Math.Abs(it.W1 - it.W0) < 1e-6) { Warn($"Truss: plane item {it.Name} - outline or thickness missing; not built{Src}."); break; }
                        var sec = it.Outline.Select(q => new Vec2(q.U, q.V)).ToArray();
                        MeshBuilder.AddLoft(mesh, sec, new[] { sec.Select(q => W(q.X, q.Y, it.W0)).ToArray(), sec.Select(q => W(q.X, q.Y, it.W1)).ToArray() });
                        len = sec.Max(q => q.X) - sec.Min(q => q.X);
                    }
                    else
                    {
                        if (path.Count < 2 || it.Section.Count < 3) { Warn($"Truss: plane item {it.Name} - path or section missing; not built{Src}."); break; }
                        var sec = it.Section.Select(q => new Vec2(q.U, q.V)).ToArray();
                        for (int i = 0; i + 1 < path.Count; i++)
                        {
                            var p0 = path[i]; var p1 = path[i + 1];
                            var dd = p1 - p0; double l = dd.Length;
                            if (l < 1e-6) continue;
                            var left = new Vec2(-dd.Y / l, dd.X / l);
                            MeshBuilder.AddLoft(mesh, sec, new[]
                            {
                                sec.Select(q => W(p0.X + left.X * q.Y, p0.Y + left.Y * q.Y, q.X)).ToArray(),
                                sec.Select(q => W(p1.X + left.X * q.Y, p1.Y + left.Y * q.Y, q.X)).ToArray(),
                            });
                            len += l;
                        }
                    }
                    if (mesh.IsEmpty) continue;
                    string mark = it.Name + (mir ? "-M" : "");
                    var part = new SteelPart { Mark = mark, Category = it.Category ?? CategoryOf(it.Role), Assembly = asm, Mesh = mesh, Material = Material(it.Material), Density = it.Density ?? 0,
                        Length = len, Assumption = asu, Description = $"{(it.Label.Length > 0 ? it.Label : it.Name)}{(it.Source.Length > 0 ? $" ({it.Source})" : "")}" };
                    Model.Steel.Parts.Add(part);
                    if (it.Panel.Length > 0) Model.PanelOf[mark] = it.Panel;
                    nParts++; mass += part.Mass;
                }
            }
            if (nParts > 0) Info($"Truss: plane items - {nParts} pieces, {mass * 2.20462262:N0} lb{Src}.");
        }

        /// <summary>Rivets of the plane items (<see cref="TrussPlaneItemDefinition.Rivets"/>), after every part is built: grip = the contiguous steel along the
        /// shank through the given point; one rivet per hole (<see cref="MergeRivet"/>).</summary>
        private void PlacePlaneItemRivets()
        {
            if (_itemRivetFrames.Count == 0) return;
            var parts = Model.Steel.Parts;
            var boxes = parts.Select(p => p.Mesh is { IsEmpty: false } ? p.Mesh.Bounds() : default).ToList();
            int n = 0, miss = 0, shared = 0, bridged = 0; double bridgedMax = 0;
            foreach (var (it, a, u, vAx, w, L, mirror, asm) in _itemRivetFrames)
                foreach (bool mir in mirror ? new[] { false, true } : new[] { false })
                    foreach (var rv in it.Rivets!)
                    {
                        var at = a + u * (mir ? L - rv.U : rv.U) + vAx * rv.V + w * rv.W;
                        var axr = u * (mir ? -rv.AxisU : rv.AxisU) + vAx * rv.AxisV + w * rv.AxisW;
                        if (axr.Length < 1e-9) { miss++; continue; }
                        var ax = axr.Normalized();
                        var lo = new Vec3(at.X - 0.4, at.Y - 0.4, at.Z - 0.4); var hi = new Vec3(at.X + 0.4, at.Y + 0.4, at.Z + 0.4);
                        var pr = new SteelLineProbe();
                        for (int i = 0; i < parts.Count; i++)
                        {
                            var b = boxes[i];
                            if (parts[i].Mesh is not { IsEmpty: false } || b.Max.X < lo.X || b.Min.X > hi.X || b.Max.Y < lo.Y || b.Min.Y > hi.Y || b.Max.Z < lo.Z || b.Min.Z > hi.Z) continue;
                            pr.Add(parts[i].Mesh, i, lo, hi);
                        }
                        if (pr.At(at, ax, 0.35) is not { } iv) { miss++; continue; }
                        if (it.RivetGapTolerance > 0)
                        {
                            // OPTIONAL: the grip continues across air gaps up to the tolerance on both sides of the point
                            var ivs = pr.Intervals(at, ax, 0.35);
                            double t0 = iv.t0, t1 = iv.t1, gsum = 0;
                            foreach (var nx in ivs.Where(x => x.t0 > t1 - 1e-9).OrderBy(x => x.t0)) { double gp = nx.t0 - t1; if (gp > it.RivetGapTolerance) break; if (gp > 1e-5) gsum += gp; t1 = Math.Max(t1, nx.t1); }
                            foreach (var nx in ivs.Where(x => x.t1 < t0 + 1e-9).OrderByDescending(x => x.t1)) { double gp = t0 - nx.t1; if (gp > it.RivetGapTolerance) break; if (gp > 1e-5) gsum += gp; t0 = Math.Min(t0, nx.t0); }
                            if (gsum > 1e-5) { bridged++; bridgedMax = Math.Max(bridgedMax, gsum); }
                            iv = (t0, t1);
                        }
                        var asu = RivetFlag(rv.Unresolved ?? (it.Unresolved.Length > 0 ? it.Unresolved : null), "rivets of the plane item at the given points");
                        if (MergeRivet(at + ax * iv.t1, -ax, Math.Abs(ax.Z) > 0.9 ? u : Up, iv.t1 - iv.t0, rv.Diameter, asm, rv.Material, asu)) shared++; else n++;
                    }
            Info($"Truss: plane item rivets - {n} placed{(shared > 0 ? $", {shared} in a hole already taken (one rivet through the whole stack)" : "")}{(miss > 0 ? $", {miss} positions not in steel (skipped)" : "")}{(bridged > 0 ? $", {bridged} across air gaps (max {bridgedMax / 0.0254:0.000} in.)" : "")}{Src}.");
        }
    }
}
