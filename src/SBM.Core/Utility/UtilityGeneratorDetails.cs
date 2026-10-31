using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Utility;

/// <summary>A further solid of a structure: "cone", "frame", "cover" or "grate" (the barrel / walls are <see cref="UtilityStructure.Mesh"/>).</summary>
public sealed record UtilityPart(string Kind, string Name, Mesh Mesh)
{
    public double Volume => Math.Abs(Mesh.Volume());
}

/// <summary>Opening in a structure wall for a pipe / duct bank (IFC IfcOpeningElement voiding the structure): the pipe's OUTSIDE section swept through the wall.</summary>
public sealed class UtilityOpening
{
    public required UtilityStructure Structure { get; init; }
    /// <summary>Pipe or duct bank name.</summary>
    public required string Link { get; init; }
    public required Mesh Mesh { get; init; }
    /// <summary>Concrete taken out of the wall: outside section area x wall length along the link axis (m3).</summary>
    public double Deduction { get; init; }
}

/// <summary>A hanger of a bridge-hung pipe: from the outside crown straight up to the lowest bridge face above (<see cref="Above"/>); <see cref="Mesh"/> empty when the rod
/// diameter is not given.</summary>
public sealed class UtilityHanger
{
    public required UtilityPipe Pipe { get; init; }
    public required string Name { get; init; }
    public Vec3 Bottom { get; init; }
    public Vec3 Top { get; init; }
    public string Above { get; init; } = "";
    public Mesh Mesh { get; init; } = new();
    /// <summary>Plan distance from the pipe start.</summary>
    public double Distance { get; init; }
    public double Length => Top.Z - Bottom.Z;
}

/// <summary>A generated duct bank: concrete encasement (closed, conduit holes cut) and conduits (closed rings), straight between the ends at the bottom-centre points.</summary>
public sealed class UtilityDuctBank
{
    public required UtilityDuctBankDefinition Definition { get; init; }
    public UtilityStructure? FromStructure { get; init; }
    public UtilityStructure? ToStructure { get; init; }
    /// <summary>Bottom-centre points of the encasement at the ends.</summary>
    public Vec3 Start { get; init; }
    public Vec3 End { get; init; }
    public Mesh Encasement { get; init; } = new();
    public List<Mesh> Conduits { get; } = new();
    public double Length => Vec2.Distance(Start.XY, End.XY);
    public double ConcreteVolume => Math.Abs(Encasement.Volume());
    /// <summary>Conduit length (all conduits, along the axis), m.</summary>
    public double ConduitLength { get; init; }
}

public static partial class UtilityGenerator
{
    /// <summary>
    /// After the bridges / walls / roads are generated: hangers of the bridge-hung pipes (rods up to the bridge's lowest face over each hanger point), then the clash /
    /// clearance check (<see cref="UtilityClashCheck"/>). Call once per regeneration with the same obstacle list.
    /// </summary>
    public static void Finish(IReadOnlyList<UtilityModel> networks, IReadOnlyList<UtilityObstacle> obstacles, Units.UnitSystem? units = null)
    {
        foreach (var n in networks) Hang(n, obstacles);
        UtilityClashCheck.Run(networks, obstacles, units);
    }

    private static void Hang(UtilityModel m, IReadOnlyList<UtilityObstacle> obstacles)
    {
        var net = m.Definition;
        m.Hangers.Clear();
        void Warn(string t) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Network {net.Name}: {t}"));
        void Info(string t) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Network {net.Name}: {t}"));
        foreach (var p in m.Pipes.Where(p => p.IsBuilt && p.Definition.Hangers != null))
        {
            var h = p.Definition.Hangers!;
            var d = p.Definition;
            string src = h.Source.Length > 0 ? h.Source : d.Source.Length > 0 ? d.Source : net.Source;
            if (h.Unresolved.Length > 0) Warn(h.Unresolved);
            var mine = obstacles.Where(o => o.Bridge.Length > 0 && string.Equals(o.Bridge, h.Bridge, StringComparison.OrdinalIgnoreCase) && o.Kind == "bridge").ToList();
            if (mine.Count == 0) { Warn($"pipe {d.Name}: hung from bridge '{h.Bridge}', which is not generated / has no superstructure solids - no hangers."); continue; }
            if (!(h.Spacing > 0)) { Warn(UnresolvedFlag.Text(src, $"pipe {d.Name} hanger spacing", "not given", "no hangers built.")); continue; }
            double len = p.Length;
            var at = new List<double>();
            if (!double.IsNaN(h.FirstFromStart))
                for (double s = h.FirstFromStart; s <= len + 1e-6; s += h.Spacing) at.Add(s);
            else
            {
                int n = Math.Max(1, (int)Math.Ceiling(len / h.Spacing - 1e-9));
                for (int k = 0; k < n; k++) at.Add((k + 0.5) * len / n);
                Warn(UnresolvedFlag.Text(src, $"pipe {d.Name} hanger layout", "the first hanger position is not given",
                    $"{n} hangers in equal spaces of {len / n:0.###} m (not over the {h.Spacing:0.###} m spacing), end hangers half a space from the pipe ends."));
            }
            if (!(h.RodDiameter > 0)) Warn(UnresolvedFlag.Text(src, $"pipe {d.Name} hanger rod diameter", "not given", "hanger positions reported, rods not built."));
            double rise = d.Shape == UtilityPipeShape.Circular ? d.InnerDiameter : d.InnerRise;
            int i = 0, missing = 0;
            var lengths = new List<double>();
            foreach (var s in at)
            {
                double t = len > 0 ? s / len : 0;
                var inv = p.Start + (p.End - p.Start) * t;
                var crown = inv + new Vec3(0, 0, rise + d.WallThickness);   // outside crown (vertical; the axis tilt is ignored for the rod start)
                double best = double.PositiveInfinity; string above = "";
                foreach (var o in mine)
                {
                    var b = o.Mesh.Bounds();
                    if (crown.X < b.Min.X || crown.X > b.Max.X || crown.Y < b.Min.Y || crown.Y > b.Max.Y || b.Max.Z < crown.Z) continue;
                    double z = LowestAbove(o.Mesh, crown);
                    if (z < best) { best = z; above = o.Name; }
                }
                i++;
                if (double.IsPositiveInfinity(best)) { missing++; continue; }
                var top = new Vec3(crown.X, crown.Y, best);
                var mesh = new Mesh();
                if (h.RodDiameter > 0 && best - crown.Z > 1e-4)
                {
                    var loopA = Circle(crown, h.RodDiameter / 2, 8); var loopB = Circle(top, h.RodDiameter / 2, 8);
                    Prism(mesh, loopA, loopB);
                }
                m.Hangers.Add(new UtilityHanger { Pipe = p, Name = $"{d.Name} H{i}", Bottom = crown, Top = top, Above = above, Mesh = mesh, Distance = s });
                lengths.Add(best - crown.Z);
            }
            if (missing > 0) Warn($"pipe {d.Name}: {missing} of {at.Count} hanger points have no part of bridge '{h.Bridge}' above them - not built.");
            if (lengths.Count > 0)
                Info($"pipe {d.Name}: {lengths.Count} hangers{(h.Type.Length > 0 ? " (" + h.Type + ")" : "")} from bridge {h.Bridge}, rod length {lengths.Min():0.###} - {lengths.Max():0.###} m ({src}).");
        }
    }

    /// <summary>Lowest triangle hit straight above <paramref name="p"/> (vertical ray up); +infinity when none.</summary>
    private static double LowestAbove(Mesh m, Vec3 p)
    {
        double best = double.PositiveInfinity;
        for (int t = 0; t < m.TriangleCount; t++)
        {
            var a = m.Positions[m.Indices[3 * t]]; var b = m.Positions[m.Indices[3 * t + 1]]; var c = m.Positions[m.Indices[3 * t + 2]];
            double d = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double u = ((p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y)) / d;
            double v = ((b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y)) / d;
            if (u < -1e-9 || v < -1e-9 || u + v > 1 + 1e-9) continue;
            double z = a.Z + u * (b.Z - a.Z) + v * (c.Z - a.Z);
            if (z > p.Z && z < best) best = z;
        }
        return best;
    }

    private static Vec3[] Circle(Vec3 c, double r, int n) =>
        Enumerable.Range(0, n).Select(i => new Vec3(c.X + r * Math.Cos(2 * Math.PI * i / n), c.Y + r * Math.Sin(2 * Math.PI * i / n), c.Z)).ToArray();

    /// <summary>Closed prism between two convex loops of equal length (side faces + fan caps), oriented outward.</summary>
    internal static void Prism(Mesh mesh, Vec3[] a, Vec3[] b)
    {
        int n = a.Length, t0 = mesh.TriangleCount;
        int ia = mesh.Positions.Count; foreach (var q in a) mesh.Add(q);
        int ib = mesh.Positions.Count; foreach (var q in b) mesh.Add(q);
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            mesh.Tri(ia + k, ia + k1, ib + k1); mesh.Tri(ia + k, ib + k1, ib + k);
        }
        for (int k = 1; k + 1 < n; k++) { mesh.Tri(ia, ia + k + 1, ia + k); mesh.Tri(ib, ib + k, ib + k + 1); }
        Orient(mesh, t0);
    }

    /// <summary>
    /// Opening for a link (pipe / duct bank) through a structure wall: its outside section (centre <paramref name="centreAt"/>(distance), axis <paramref name="axis"/>,
    /// right <paramref name="u"/>, up <paramref name="v"/>) swept from half the inside distance to beyond the outside face. Null for an envelope or unbuilt structure.
    /// </summary>
    private static UtilityOpening? Opening(UtilityStructure s, string link, Vec2 planDir, Func<double, Vec3> centreAt, Vec3 u, Vec3 v,
        double halfRise, double halfSpan, bool box, double slopeCos)
    {
        if (!s.IsBuilt || s.Definition.IsEnvelope) return null;
        double din = s.InsideDistance(planDir), dout = s.InsideDistance(planDir, s.Definition.WallThickness);
        if (!(dout > din)) return null;
        // the link runs AWAY from the structure centre along planDir: plan distances from the centre -> along the axis from the link end (at din)
        double a0 = (din * 0.5 - din) / slopeCos, a1 = (dout + 2 * halfSpan + 0.02 - din) / slopeCos;
        Vec3[] Loop(Vec3 c) => box
            ? new[] { (-halfSpan, -halfRise), (halfSpan, -halfRise), (halfSpan, halfRise), (-halfSpan, halfRise) }.Select(q => c + u * q.Item1 + v * q.Item2).ToArray()
            : Enumerable.Range(0, Sides).Select(i => { double t = 2 * Math.PI * i / Sides; return c + u * (halfSpan * Math.Cos(t)) + v * (halfRise * Math.Sin(t)); }).ToArray();
        var mesh = new Mesh();
        Prism(mesh, Loop(centreAt(a0)), Loop(centreAt(a1)));
        double area = box ? 4 * halfRise * halfSpan : Math.PI * halfRise * halfSpan;
        return new UtilityOpening { Structure = s, Link = link, Mesh = mesh, Deduction = area * (dout - din) / slopeCos };
    }

    /// <summary>Cone, frame and cover / grate of a structure; returns the elevation the walls stop at (the cone / frame bottom).</summary>
    private static double TopParts(UtilityStructure s, UtilityStructureDefinition d, string src, Action<string> warn, Action<string> info, Vec2? outletDir)
    {
        double top = s.Rim;
        var f = d.Frame;
        double frameBottom = top;
        if (f != null)
        {
            if (!(f.Height > 0)) warn(UnresolvedFlag.Text(f.Source.Length > 0 ? f.Source : src, $"structure {d.Name} frame height", "not given", "frame not built; walls to the rim."));
            else
            {
                frameBottom = top - f.Height;
                bool round = d.Shape == UtilityStructureShape.Round;
                double ro = round ? f.OuterDiameter : f.OuterLength, wo = round ? f.OuterDiameter : f.OuterWidth;
                double rq = round ? f.OpeningDiameter : f.OpeningLength, wq = round ? f.OpeningDiameter : f.OpeningWidth;
                if (!(ro > 0 && wo > 0 && rq > 0 && wq > 0 && rq < ro && wq < wo))
                    warn(UnresolvedFlag.Text(f.Source.Length > 0 ? f.Source : src, $"structure {d.Name} frame outside / opening size", "not given (or the opening is not inside the outside)", "frame not built; the walls stop at its bottom."));
                else
                {
                    var c = s.Plan;
                    var outer = Rect(round, ro, wo, s.Rotation).Select(q => (c + q).WithZ(frameBottom)).ToArray();
                    var inner = Rect(round, rq, wq, s.Rotation).Select(q => (c + q).WithZ(frameBottom)).ToArray();
                    var fm = new Mesh();
                    Ring(fm, outer, inner, outer.Select(q => new Vec3(q.X, q.Y, top)).ToArray(), inner.Select(q => new Vec3(q.X, q.Y, top)).ToArray());
                    string label = $"{d.Name} frame{(f.Designation.Length > 0 ? " " + f.Designation : "")}";
                    s.Parts.Add(new UtilityPart("frame", label, fm));
                    if (f.CoverThickness > 0)
                    {
                        var cm = new Mesh();
                        Prism(cm, inner.Select(q => new Vec3(q.X, q.Y, top - f.CoverThickness)).ToArray(), inner.Select(q => new Vec3(q.X, q.Y, top)).ToArray());
                        s.Parts.Add(new UtilityPart(f.Grate ? "grate" : "cover", $"{d.Name} {(f.Grate ? "grate" : "cover")}", cm));
                        if (f.Grate) info($"structure {d.Name}: grate built as a plate in the frame opening (bars / openings not modeled).");
                    }
                    else warn(UnresolvedFlag.Text(f.Source.Length > 0 ? f.Source : src, $"structure {d.Name} {(f.Grate ? "grate" : "cover")} thickness", "not given", "frame only."));
                }
            }
        }
        double wallTop = frameBottom;
        if (d.Cone is { } cone)
        {
            if (d.Shape != UtilityStructureShape.Round) warn($"structure {d.Name}: a cone is given on a box structure - not built.");
            else if (!(cone.Height > 0 && cone.TopInnerDiameter > 0 && cone.TopInnerDiameter < d.InnerDiameter))
                warn(UnresolvedFlag.Text(cone.Source.Length > 0 ? cone.Source : src, $"structure {d.Name} cone height / top opening", "not given (or not smaller than the barrel)", "walls to the frame bottom / rim."));
            else
            {
                double z1 = frameBottom, z0 = z1 - cone.Height;
                double R = d.InnerDiameter / 2, r = cone.TopInnerDiameter / 2, t = d.WallThickness;
                var c0 = s.Plan; var c1 = s.Plan;
                if (cone.Eccentric)
                {
                    double ang;
                    if (cone.EccentricDegrees is double ed) ang = ed * Math.PI / 180;
                    else
                    {
                        ang = outletDir is { } od ? Math.Atan2(od.Y, od.X) : 0;
                        warn(UnresolvedFlag.Text(cone.Source.Length > 0 ? cone.Source : src, $"structure {d.Name} eccentric cone direction", "not given",
                            outletDir != null ? "vertical side toward the outlet pipe." : "vertical side toward project +X."));
                    }
                    c1 = c0 + new Vec2(Math.Cos(ang), Math.Sin(ang)) * (R - r);
                }
                Vec3[] L(Vec2 c, double rad, double z) => Enumerable.Range(0, Sides).Select(i => new Vec3(c.X + rad * Math.Cos(2 * Math.PI * i / Sides), c.Y + rad * Math.Sin(2 * Math.PI * i / Sides), z)).ToArray();
                var cm = new Mesh();
                Ring(cm, L(c0, R + t, z0), L(c0, R, z0), L(c1, r + t, z1), L(c1, r, z1));
                s.Parts.Insert(0, new UtilityPart("cone", $"{d.Name} cone", cm));
                wallTop = z0;
            }
        }
        return wallTop;
    }

    /// <summary>Round (n-gon of diameter <paramref name="len"/>) or box outline (len x wid, rotated) about the origin, counter-clockwise.</summary>
    private static List<Vec2> Rect(bool round, double len, double wid, double rot)
    {
        if (round) return Enumerable.Range(0, Sides).Select(i => new Vec2(len / 2 * Math.Cos(2 * Math.PI * i / Sides), len / 2 * Math.Sin(2 * Math.PI * i / Sides))).ToList();
        double a = len / 2, b = wid / 2, c = Math.Cos(rot), s = Math.Sin(rot);
        return new[] { (-a, -b), (a, -b), (a, b), (-a, b) }.Select(q => new Vec2(q.Item1 * c - q.Item2 * s, q.Item1 * s + q.Item2 * c)).ToList();
    }

    /// <summary>Duct banks of a network: encasement (exact extrusion with the conduit holes) + conduit rings, ends at the structures' inside faces.</summary>
    private static void DuctBanks(UtilityModel m, Dictionary<string, UtilityStructure> byName, Action<string> warn, Action<string> info)
    {
        var net = m.Definition;
        foreach (var d in net.DuctBanks ?? new())
        {
            string src = d.Source.Length > 0 ? d.Source : net.Source;
            if (d.Unresolved.Length > 0) warn(d.Unresolved);
            UtilityStructure? fs = null, ts = null;
            if (d.From.Length > 0 && !byName.TryGetValue(d.From, out fs)) { warn($"duct bank {d.Name}: start structure '{d.From}' not found - not built."); continue; }
            if (d.To.Length > 0 && !byName.TryGetValue(d.To, out ts)) { warn($"duct bank {d.Name}: end structure '{d.To}' not found - not built."); continue; }
            Vec2? a = fs?.Plan ?? (double.IsNaN(d.StartX) || double.IsNaN(d.StartY) ? null : new Vec2(d.StartX, d.StartY));
            Vec2? b = ts?.Plan ?? (double.IsNaN(d.EndX) || double.IsNaN(d.EndY) ? null : new Vec2(d.EndX, d.EndY));
            if (a == null || b == null) { warn(UnresolvedFlag.Text(src, $"duct bank {d.Name} end point", "no structure and no free end coordinates", "not built.")); continue; }
            var miss = new List<string>();
            if (double.IsNaN(d.BottomStart) || double.IsNaN(d.BottomEnd)) miss.Add("bottom elevations");
            if (!(d.Width > 0 && d.Height > 0)) miss.Add("encasement width / height");
            if (d.Rows < 1 || d.Columns < 1) miss.Add("conduit rows / columns");
            if (!(d.ConduitDiameter > 0 && d.ConduitWall > 0)) miss.Add("conduit size / wall");
            if (d.Columns > 1 && !(d.HorizontalSpacing > 0)) miss.Add("horizontal conduit spacing");
            if (d.Rows > 1 && !(d.VerticalSpacing > 0)) miss.Add("vertical conduit spacing");
            if (miss.Count > 0) { warn(UnresolvedFlag.Text(src, $"duct bank {d.Name} {string.Join(", ", miss)}", "not given", "not built.")); continue; }
            var dir = b.Value - a.Value;
            if (dir.Length < 1e-6) { warn($"duct bank {d.Name}: both ends at the same plan point - not built."); continue; }
            dir = dir / dir.Length;
            var pa = a.Value + dir * (fs?.InsideDistance(dir) ?? 0);
            var pb = b.Value - dir * (ts?.InsideDistance(-dir) ?? 0);
            if (Vec2.Dot(pb - pa, dir) <= 1e-6) { warn($"duct bank {d.Name}: no length left between the structures' inside faces - not built."); continue; }
            var s0 = pa.WithZ(d.BottomStart); var s1 = pb.WithZ(d.BottomEnd);
            var axis = s1 - s0; double alen = axis.Length; var w = axis.Normalized();
            var u = Vec3.Cross(w, new Vec3(0, 0, 1)).Normalized(); var v = Vec3.Cross(u, w);
            double od = d.ConduitDiameter + 2 * d.ConduitWall;
            double gx = (d.Columns - 1) * (d.Columns > 1 ? d.HorizontalSpacing : 0), gy = (d.Rows - 1) * (d.Rows > 1 ? d.VerticalSpacing : 0);
            double x0, y0;
            if (!double.IsNaN(d.FirstConduitFromLeft) && !double.IsNaN(d.FirstConduitFromBottom)) { x0 = -d.Width / 2 + d.FirstConduitFromLeft; y0 = -d.Height / 2 + d.FirstConduitFromBottom; }
            else
            {
                x0 = -gx / 2; y0 = -gy / 2;
                warn(UnresolvedFlag.Text(src, $"duct bank {d.Name} conduit position", "the first conduit's distance from the encasement faces is not given", "conduit group centred in the encasement."));
            }
            var holes = new List<Hole>();
            for (int r = 0; r < d.Rows; r++)
                for (int c = 0; c < d.Columns; c++)
                    holes.Add(new Hole(new Vec2(x0 + c * (d.Columns > 1 ? d.HorizontalSpacing : 0), y0 + r * (d.Rows > 1 ? d.VerticalSpacing : 0)), od));
            double hw = d.Width / 2, hh = d.Height / 2;
            if (holes.Any(h => Math.Abs(h.Center.X) + od / 2 > hw - 1e-6 || Math.Abs(h.Center.Y) + od / 2 > hh - 1e-6))
            { warn($"duct bank {d.Name}: the conduits do not fit inside the {d.Width:0.###} x {d.Height:0.###} m encasement - not built (reported, not changed)."); continue; }
            if (holes.Count > 1 && Enumerable.Range(0, holes.Count).Any(i => Enumerable.Range(i + 1, holes.Count - i - 1).Any(j => Vec2.Distance(holes[i].Center, holes[j].Center) < od - 1e-6)))
            { warn($"duct bank {d.Name}: conduit spacing is less than the conduit outside diameter - not built."); continue; }
            var frame = new Frame3(s0 + v * hh, u, v, w);
            var enc = new Mesh();
            PlateBuilder.AddPlanar(enc, new[] { new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(hw, hh), new Vec2(-hw, hh) }, holes, frame, 0, alen);
            var bank = new UtilityDuctBank { Definition = d, FromStructure = fs, ToStructure = ts, Start = s0, End = s1, Encasement = enc, ConduitLength = alen * holes.Count };
            const int cs = PlateBuilder.HoleSegments;
            foreach (var h in holes)
            {
                // the same circumscribed 16-gon as the encasement hole (PlateBuilder.Circle), so the conduit fills it exactly
                Vec3[] Lp(double z, double rad) => Enumerable.Range(0, cs).Select(i => { var q = h.Center + Vec2.FromAngle(-2 * Math.PI * i / cs) * (rad / Math.Cos(Math.PI / cs)); return frame.ToWorld(q.X, q.Y, z); }).ToArray();
                var cm = new Mesh();
                Ring(cm, Lp(0, od / 2), Lp(0, d.ConduitDiameter / 2), Lp(alen, od / 2), Lp(alen, d.ConduitDiameter / 2));
                bank.Conduits.Add(cm);
            }
            m.DuctBanks.Add(bank);
            double cosS = Math.Max(1e-6, Vec2.Distance(s0.XY, s1.XY) / alen);
            foreach (var (st, end) in new[] { (fs, 0.0), (ts, alen) })
            {
                if (st == null) continue;
                var pd = end == 0 ? dir : -dir;
                var wd = end == 0 ? w : -w;
                var origin = (end == 0 ? s0 : s1) + v * hh;
                var op = Opening(st, d.Name, pd, x => origin + wd * x, u, v, hh, hw, true, cosS);
                if (op != null) st.Openings.Add(op);
            }
            info($"duct bank {d.Name}: {d.Rows} x {d.Columns} conduits, {bank.Length:0.##} m, encasement {bank.ConcreteVolume:0.###} m3 (conduit holes deducted).");
        }
    }
}
