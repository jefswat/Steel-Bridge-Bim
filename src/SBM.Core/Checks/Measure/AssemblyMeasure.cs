using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using System.Text.Json.Serialization;

namespace SBM.Core.Checks.Measure;

public sealed class AssemblyOptions
{
    public PlateFrameOptions Frame { get; set; } = new();
    /// <summary>Also collect the parts of OTHER assemblies that lie in the assembly's planes near it (girder webs / flanges at a splice, the girder
    /// web or stiffener a cross frame connects to): drawn and used for joint gaps and bolts per side.</summary>
    public bool Neighbours { get; set; } = true;
    /// <summary>Plates within this distance (m) of each other along the normal belong to one plane group (stacks with fillers).</summary>
    public double StackGap { get; set; } = 0.0254 * 4;
}

public sealed class PlaneLayer
{
    public required string Mark { get; init; }
    public string Assembly { get; init; } = "";
    public string Category { get; init; } = "";
    public bool Neighbour { get; init; }
    public int Leg { get; init; }
    public Len Thickness { get; init; }
    /// <summary>Near / far face along the plane normal from the plane origin.</summary>
    public Len NearFace { get; init; }
    public Len FarFace { get; init; }
    public Len? Width { get; init; }
    public Len? Height { get; init; }
    public int Holes { get; init; }
    public List<double[]>? Outline { get; init; }
    public List<HoleGroup>? HoleGroups { get; init; }
    public List<string>? HoleGroupGaps { get; init; }
    public string Source { get; init; } = "";
}

public sealed class PlaneBolt
{
    public int No { get; init; }
    public double[] At { get; init; } = Array.Empty<double>();
    public Len Diameter { get; init; }
    public Len Grip { get; init; }
    /// <summary>Plates of the group the bolt passes through (in stack order) and their total thickness + gaps.</summary>
    public List<string> Through { get; init; } = new();
    public Len Stack { get; init; }
    public string? Side { get; set; }
}

public sealed class StackGap
{
    public required string Lower { get; init; }
    public required string Upper { get; init; }
    /// <summary>Clear gap between the facing faces (negative = the plates overlap / interpenetrate).</summary>
    public required Len Gap { get; init; }
}

public sealed class JointGap
{
    public required string A { get; init; }
    public required string B { get; init; }
    public required Len Gap { get; init; }
    public double[] AtA { get; init; } = Array.Empty<double>();
    public double[] AtB { get; init; } = Array.Empty<double>();
}

public sealed class AssemblyPlane
{
    public int No { get; init; }
    public string Name { get; init; } = "";
    public double[] Origin { get; init; } = Array.Empty<double>();
    public double[] U { get; init; } = Array.Empty<double>();
    public double[] V { get; init; } = Array.Empty<double>();
    public double[] Normal { get; init; } = Array.Empty<double>();
    public string Reference { get; init; } = "";
    public double NormalFromVerticalDeg { get; init; }
    public List<PlaneLayer> Layers { get; init; } = new();
    public List<StackGap> StackGaps { get; init; } = new();
    public List<JointGap> JointGaps { get; init; } = new();
    public List<PlaneBolt> Bolts { get; init; } = new();
    public List<HoleGroup> BoltGroups { get; init; } = new();
    public Dictionary<string, int> BoltsPerSide { get; init; } = new();
    public List<string> Notes { get; init; } = new();
    [JsonIgnore] public List<(string Mark, bool Neighbour, FlatShape? Shape, List<(Vec2 A, Vec2 B)>? Segs)> Drawing { get; } = new();
    [JsonIgnore] public List<(Vec2 At, double D)> BoltPts { get; } = new();
}

public sealed class AssemblyResult
{
    public string Query { get; init; } = "assembly";
    public required string Bridge { get; init; }
    public required string Mark { get; init; }
    public string Kind { get; init; } = "";
    public string Description { get; init; } = "";
    public string? GlobalId { get; init; }
    public string Units { get; init; } = "in";
    public int Parts { get; init; }
    public int Bolts { get; init; }
    public int BoltsPlaced { get; init; }
    public List<AssemblyPlane> Planes { get; init; } = new();
    /// <summary>Parts of the assembly that are not plate-like (welds, curved pieces) and are not in any plane group.</summary>
    public List<string> NotInPlanes { get; init; } = new();
    public List<string> Notes { get; init; } = new();
}

/// <summary>
/// Collapses an assembly's plates and bolts into their common planes (web plane, flange planes, frame plane...): per plane the plate outlines and holes in
/// one 2D frame, the stack along the normal (fillers, gaps), the bolts and how many are on each side of the joint, and the gaps between the joined plates.
/// </summary>
public static class AssemblyMeasure
{
    private const double CosParallel = 0.99966;   // 1.5 deg

    private sealed class Sheet
    {
        public required SteelPart Part { get; init; }
        public required PlateComponent C { get; init; }
        public required int Leg { get; init; }
        public required bool Neighbour { get; init; }
        public double Lo, Hi;           // faces along the group normal
        public FlatShape? Shape;        // group frame (members)
        public List<(Vec2 A, Vec2 B)>? Segs;  // group frame (neighbours, clipped)
        public Box2 Box = Box2.Empty;
    }

    public static AssemblyResult Measure(MeasureModel mm, BridgeModel m, SteelAssembly asm, AssemblyOptions o)
    {
        var u = mm.Units;
        var s = m.Steel!;
        var notes = new List<string>();
        var parts = s.Parts.Where(p => p.Assembly == asm.Mark).ToList();
        var bolts = s.Bolts.Where(b => b.Assembly == asm.Mark).ToList();
        var sheets = new List<Sheet>();
        var notIn = new List<string>();
        foreach (var p in parts)
        {
            if (p.Category == PartCategory.Weld) { notIn.Add($"{p.Mark} (weld)"); continue; }
            var comps = PlateGeometry.Components(p.Mesh);
            if (comps.Count == 0 || comps[0].Coverage < 0.3) { notIn.Add($"{p.Mark} ({p.Category}, not plate-like)"); continue; }
            for (int k = 0; k < comps.Count; k++)
                if (comps[k].FaceArea >= 0.2 * comps[0].FaceArea) sheets.Add(new Sheet { Part = p, C = comps[k], Leg = k, Neighbour = false });
        }
        if (sheets.Count == 0) throw new ArgumentException($"Assembly {asm.Mark} has no plate-like parts.");

        // plane groups: parallel normals, then stacks along the normal
        var groups = new List<List<Sheet>>();
        var dirs = new List<(Vec3 N, List<Sheet> L)>();
        foreach (var sh in sheets.OrderByDescending(x => x.C.FaceArea))
        {
            int k = dirs.FindIndex(d => Math.Abs(Vec3.Dot(d.N, sh.C.Normal)) > CosParallel);
            if (k < 0) dirs.Add((sh.C.Normal, new List<Sheet> { sh })); else dirs[k].L.Add(sh);
        }
        foreach (var (n, list) in dirs)
        {
            var o0 = list[0].C.MidPoint;
            foreach (var sh in list)
            {
                double off = Vec3.Dot(sh.C.MidPoint - o0, n);
                sh.Lo = off - sh.C.Thickness / 2; sh.Hi = off + sh.C.Thickness / 2;
            }
            var sorted = list.OrderBy(x => x.Lo).ToList();
            var cur = new List<Sheet> { sorted[0] };
            double hi = sorted[0].Hi;
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].Lo - hi > o.StackGap) { groups.Add(cur); cur = new List<Sheet>(); hi = double.MinValue; }
                cur.Add(sorted[i]); hi = Math.Max(hi, sorted[i].Hi);
            }
            groups.Add(cur);
        }
        groups = groups.OrderByDescending(g => g.Sum(x => x.C.FaceArea)).ToList();

        // assembly box for neighbours
        var abox = Box3.Empty;
        foreach (var p in parts) { var b = p.Mesh.Bounds(); abox.Add(b.Min); abox.Add(b.Max); }
        double grow = 0.0254 * 2;
        var nbParts = !o.Neighbours ? new List<SteelPart>() : s.Parts.Where(p => p.Assembly != asm.Mark && p.Category != PartCategory.Weld && !p.Mesh.IsEmpty).Where(p =>
        {
            var b = p.Mesh.Bounds();
            return b.Min.X <= abox.Max.X + grow && b.Max.X >= abox.Min.X - grow && b.Min.Y <= abox.Max.Y + grow && b.Max.Y >= abox.Min.Y - grow && b.Min.Z <= abox.Max.Z + grow && b.Max.Z >= abox.Min.Z - grow;
        }).ToList();
        // neighbours are cropped to the triangles near the assembly (long girder plates: thousands of triangles far away)
        var cropBox = new Box3 { Min = abox.Min - new Vec3(0.15, 0.15, 0.15), Max = abox.Max + new Vec3(0.15, 0.15, 0.15) };
        Prof("start");
        var nbComps = nbParts.Select(p => { var cm = Crop(p.Mesh, cropBox); return (p, mesh: cm, comps: PlateGeometry.Components(cm)); }).Where(x => !x.mesh.IsEmpty).ToList();

        var res = new AssemblyResult
        {
            Bridge = m.Definition.Name, Mark = asm.Mark, Kind = asm.Kind.ToString(), Description = asm.Description, Units = u.ShortUnitName,
            GlobalId = mm.Ref(m.Definition.Name, "assembly", asm.Mark).GlobalId, Parts = parts.Count, Bolts = bolts.Count, NotInPlanes = notIn, Notes = notes,
        };
        var placed = new HashSet<BoltInstance>();
        int gno = 0;
        foreach (var g in groups)
        {
            gno++;
            var main = g.OrderByDescending(x => x.C.FaceArea).First();
            var fo = new PlateFrameOptions { Ref = o.Frame.Ref, Vector = o.Frame.Vector, RefName = o.Frame.RefName, Ahead = o.Frame.Ahead, Tolerance = o.Frame.Tolerance };
            var fnotes = new List<string>();
            var gf = PlateMeasure.FrameFor(main.Part.Mesh, main.C, fo, fnotes, out string refText);
            var N = gf.N;
            // stack offsets along the oriented normal
            foreach (var sh in g)
            {
                double off = Vec3.Dot(sh.C.MidPoint - gf.Origin, N);
                sh.Lo = off - sh.C.Thickness / 2; sh.Hi = off + sh.C.Thickness / 2;
            }
            Prof("// outlines in the group ");
            // outlines in the group frame
            foreach (var sh in g)
            {
                sh.Shape = ShapeIn(sh.Part.Mesh, sh.C, gf);
                foreach (var q in sh.Shape.Outer) sh.Box.Add(q);
            }
            var win = Box2.Empty;
            foreach (var sh in g) win.Add(sh.Box);
            double lo = g.Min(x => x.Lo), hi = g.Max(x => x.Hi);
            // neighbours in this plane
            var nbs = new List<Sheet>();
            var winG = win.Inflate(0.0254 * 4);
            foreach (var (p, cmesh, comps) in nbComps)
                for (int k = 0; k < Math.Min(2, comps.Count); k++)
                {
                    var c = comps[k];
                    if (Math.Abs(Vec3.Dot(c.Normal, N)) < CosParallel) continue;
                    double off = Vec3.Dot(c.MidPoint - gf.Origin, N);
                    double nlo = off - c.Thickness / 2, nhi = off + c.Thickness / 2;
                    if (nlo > hi + o.StackGap || nhi < lo - o.StackGap) continue;
                    var segs = SegmentsIn(cmesh, c, gf, winG);
                    if (segs.Count == 0) continue;
                    var sh = new Sheet { Part = p, C = c, Leg = k, Neighbour = true, Lo = nlo, Hi = nhi, Segs = segs };
                    foreach (var (a, b) in segs) { sh.Box.Add(a); sh.Box.Add(b); }
                    nbs.Add(sh);
                    break;
                }
            // shift the frame origin to the lower-left of the plates
            var shift = new Vec2(win.MinX, win.MinY);
            var f = new PlateFrame { Origin = gf.Origin + gf.U * shift.X + gf.V * shift.Y, U = gf.U, V = gf.V, N = N };
            double dOff = 0;   // origin keeps its position along the normal (the main plate's mid plane)
            foreach (var sh in g.Concat(nbs))
            {
                if (sh.Shape != null) sh.Shape = Shift(sh.Shape, shift);
                if (sh.Segs != null) sh.Segs = sh.Segs.Select(q => (q.A - shift, q.B - shift)).ToList();
                var b = Box2.Empty;
                if (sh.Shape != null) foreach (var q in sh.Shape.Outer) b.Add(q);
                if (sh.Segs != null) foreach (var (a, bb) in sh.Segs) { b.Add(a); b.Add(bb); }
                sh.Box = b;
            }
            double S(double v) => Fmt.Short(u, v);
            double[] P2(Vec2 q) => new[] { S(q.X), S(q.Y) };
            var plane = new AssemblyPlane
            {
                No = gno, Name = PlaneName(g, N), Origin = PlateMeasure.World(u, f.Origin), U = Geo3.Arr(f.U), V = Geo3.Arr(f.V), Normal = Geo3.Arr(N), Reference = refText,
                NormalFromVerticalDeg = Fmt.R(Math.Acos(Math.Min(1, Math.Abs(N.Z))) * 180 / Math.PI, 3), Notes = fnotes,
            };
            // layers
            foreach (var sh in g.Concat(nbs).OrderBy(x => x.Lo).ThenBy(x => x.Part.Mark, StringComparer.Ordinal))
            {
                List<HoleGroup>? hg = null; List<string>? hgaps = null; int nh = 0;
                if (sh.Shape is { Outer.Count: >= 3 } shp)
                {
                    var (_, _, _, raw) = PlateMeasure.AnalyzeOutline(shp.Outer, P2);
                    var (holes, _) = PlateMeasure.Holes(shp, P2, u);
                    nh = holes.Count;
                    if (holes.Count > 0)
                    {
                        var pn = new List<string>();
                        var openings = shp.Inner.Where(l => !holes.Any(h => (Geo2.Centroid(l) - h.C).Length < 1e-4)).ToList();
                        (hg, hgaps, _) = PlateMeasure.Pattern(holes, o.Frame.Tolerance, shp.Outer, openings, raw, u, P2, pn);
                    }
                }
                plane.Layers.Add(new PlaneLayer
                {
                    Mark = sh.Part.Mark, Assembly = sh.Part.Assembly, Category = sh.Part.Category.ToString(), Neighbour = sh.Neighbour, Leg = sh.Leg,
                    Thickness = Len.Of(sh.C.Thickness), NearFace = Len.Of(sh.Lo - dOff), FarFace = Len.Of(sh.Hi - dOff),
                    Width = sh.Shape != null ? Len.Of(sh.Box.Width) : null, Height = sh.Shape != null ? Len.Of(sh.Box.Height) : null,
                    Holes = nh, Outline = sh.Shape?.Outer.Select(P2).ToList(), HoleGroups = hg, HoleGroupGaps = hgaps is { Count: > 0 } ? hgaps : null,
                    Source = sh.Neighbour ? "neighbour (mid-plane cut, clipped)" : sh.Shape?.Source ?? "",
                });
                plane.Drawing.Add((sh.Part.Mark + (sh.Leg > 0 ? $" leg {sh.Leg + 1}" : ""), sh.Neighbour, sh.Shape, sh.Segs));
            }
            Prof("// stack gaps between ove");
            // stack gaps between overlapping layers that are next to each other along the normal
            var all = g.Concat(nbs).OrderBy(x => x.Lo).ToList();
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    var a = all[i]; var b = all[j];
                    if (!Overlap2(a.Box, b.Box, 0.0254 / 2)) continue;
                    // only the nearest overlapping layer above a
                    bool between = all.Skip(i + 1).Take(j - i - 1).Any(c => Overlap2(c.Box, a.Box, 0.0254 / 2) && Overlap2(c.Box, b.Box, 0.0254 / 2) && c.Lo >= a.Hi - 1e-4);
                    if (between) continue;
                    if (b.Lo < a.Hi - 1e-3 && b.Hi > a.Lo + 1e-3 && Math.Abs(b.Lo - a.Lo) < 1e-3) continue;   // side by side in one layer
                    plane.StackGaps.Add(new StackGap { Lower = a.Part.Mark, Upper = b.Part.Mark, Gap = Len.Of(b.Lo - a.Hi) });
                }
            Prof("// bolts through this pla");
            // bolts through this plane
            int bno = 0;
            foreach (var bolt in bolts)
            {
                if (placed.Contains(bolt)) continue;
                var z = bolt.Frame.Z;
                double dz = Vec3.Dot(z, N);
                if (Math.Abs(dz) < 0.996) continue;
                // where the axis crosses the stack mid
                double mid = (lo + hi) / 2;
                double t = (mid - Vec3.Dot(bolt.Frame.Origin - f.Origin, N)) / dz;
                if (t < -0.0254 || t > bolt.Grip + 0.0254) continue;
                var pt = bolt.Frame.Origin + z * t;
                var q = f.To2(pt);
                var through = new List<string>();
                double stack = 0;
                double t0 = Vec3.Dot(bolt.Frame.Origin - f.Origin, N), t1 = Vec3.Dot(bolt.Frame.Origin + z * bolt.Grip - f.Origin, N);
                double blo = Math.Min(t0, t1) - 1e-3, bhi = Math.Max(t0, t1) + 1e-3;
                foreach (var sh in all)
                {
                    bool inside = sh.Shape != null ? sh.Shape.Outer.Count >= 3 && Geo2.Inside(sh.Shape.Outer, q) : sh.Box.Contains(q);
                    if (!inside || sh.Hi < blo || sh.Lo > bhi) continue;
                    through.Add(sh.Part.Mark);
                }
                var thr = all.Where(sh => through.Contains(sh.Part.Mark)).ToList();
                if (thr.Count > 0) stack = thr.Max(x => x.Hi) - thr.Min(x => x.Lo);
                placed.Add(bolt);
                bno++;
                double dia = MeasureModel.BoltDiameter(s, bolt);
                plane.Bolts.Add(new PlaneBolt { No = bno, At = P2(q), Diameter = Len.Of(dia), Grip = Len.Of(bolt.Grip), Through = through, Stack = Len.Of(stack) });
                plane.BoltPts.Add((q, dia));
            }
            Prof("// joint gaps between nei");
            // joint gaps between neighbours of different assemblies + bolts per side
            var nbAsm = nbs.GroupBy(x => x.Part.Assembly).Select(x => x.ToList()).ToList();
            (Sheet A, Sheet B, Vec2 PA, Vec2 PB, double D)? bestJoint = null;
            for (int i = 0; i < nbs.Count; i++)
                for (int j = i + 1; j < nbs.Count; j++)
                {
                    var a = nbs[i]; var b = nbs[j];
                    if (a.Part.Assembly == b.Part.Assembly) continue;
                    if (Math.Abs(a.Lo - b.Lo) > a.C.Thickness) continue;   // not side by side in one layer
                    if (a.Part.Category != b.Part.Category) continue;     // a joint joins like plates (web to web, flange to flange)
                    var (pa, pb, d) = SegSetDistance(a.Segs!, b.Segs!);
                    if (d > 0.3048) continue;
                    plane.JointGaps.Add(new JointGap { A = a.Part.Mark, B = b.Part.Mark, Gap = Len.Of(d), AtA = P2(pa), AtB = P2(pb) });
                    if (bestJoint == null || d < bestJoint.Value.D) bestJoint = (a, b, pa, pb, d);
                }
            if (bestJoint is { } bj && plane.Bolts.Count > 0)
            {
                var ca = Centroid(bj.A.Segs!); var cb = Centroid(bj.B.Segs!);
                var gmid = (bj.PA + bj.PB) * 0.5;
                // joint line = A's end edge nearest to B (its direction), normal pointing toward B
                var jn = (cb - ca).Normalized();
                var nearA = bj.A.Segs!.Where(sg => (sg.B - sg.A).Length > 0.0254).OrderBy(sg => Geo2.SegDist(bj.PB, sg.A, sg.B)).FirstOrDefault();
                if ((nearA.B - nearA.A).Length > 0.0254)
                {
                    var n2 = (nearA.B - nearA.A).Normalized().Left;
                    jn = Vec2.Dot(n2, cb - ca) >= 0 ? n2 : -n2;
                }
                foreach (var pb in plane.Bolts)
                {
                    var q = new Vec2(u.FromDisplay(pb.At[0], LengthKind.Short), u.FromDisplay(pb.At[1], LengthKind.Short));
                    pb.Side = Vec2.Dot(q - gmid, jn) < 0 ? bj.A.Part.Assembly : bj.B.Part.Assembly;
                }
                foreach (var gr in plane.Bolts.GroupBy(x => x.Side!)) plane.BoltsPerSide[gr.Key] = gr.Count();
            }
            Prof("// bolt pattern of the pl");
            // bolt pattern of the plane (per side when a joint is known)
            if (plane.Bolts.Count > 0)
            {
                var bh = plane.BoltPts.Select((b, i) => new HoleResult { No = i + 1, C = b.At, D = b.D, At = P2(b.At), Diameter = Len.Of(b.D) }).ToList();
                var pn = new List<string>();
                var (bg, _, _) = PlateMeasure.Pattern(bh, o.Frame.Tolerance, new List<Vec2>(), new List<List<Vec2>>(), null, u, P2, pn);
                plane.BoltGroups.AddRange(bg);
            }
            Prof("res.Planes.Add(plane);");
            res.Planes.Add(plane);
        }
        int unplaced = bolts.Count - placed.Count;
        if (unplaced > 0) notes.Add($"{unplaced} of {bolts.Count} bolts are not normal to any plane group of the assembly");
        return new AssemblyResult
        {
            Bridge = res.Bridge, Mark = res.Mark, Kind = res.Kind, Description = res.Description, Units = res.Units, GlobalId = res.GlobalId, Parts = res.Parts,
            Bolts = res.Bolts, BoltsPlaced = placed.Count, Planes = res.Planes, NotInPlanes = notIn, Notes = notes,
        };
    }

    /// <summary>The triangles of a mesh whose box meets <paramref name="box"/>.</summary>
    public static Mesh Crop(Mesh m, Box3 box)
    {
        var r = new Mesh();
        var map = new Dictionary<int, int>();
        for (int t = 0; t + 2 < m.Indices.Count; t += 3)
        {
            var a = m.Positions[m.Indices[t]]; var b = m.Positions[m.Indices[t + 1]]; var c = m.Positions[m.Indices[t + 2]];
            if (Math.Max(a.X, Math.Max(b.X, c.X)) < box.Min.X || Math.Min(a.X, Math.Min(b.X, c.X)) > box.Max.X ||
                Math.Max(a.Y, Math.Max(b.Y, c.Y)) < box.Min.Y || Math.Min(a.Y, Math.Min(b.Y, c.Y)) > box.Max.Y ||
                Math.Max(a.Z, Math.Max(b.Z, c.Z)) < box.Min.Z || Math.Min(a.Z, Math.Min(b.Z, c.Z)) > box.Max.Z) continue;
            int I(int k) { int v = m.Indices[t + k]; if (!map.TryGetValue(v, out int n)) { n = r.Add(m.Positions[v]); map[v] = n; } return n; }
            r.Tri(I(0), I(1), I(2));
        }
        return r;
    }

    private static readonly bool Profile = Environment.GetEnvironmentVariable("SBM_MEASURE_PROFILE") == "1";
    private static readonly System.Diagnostics.Stopwatch ProfSw = System.Diagnostics.Stopwatch.StartNew();
    private static void Prof(string s) { if (Profile) Console.Error.WriteLine($"  [{ProfSw.Elapsed.TotalSeconds:0.000}] {s}"); }

    private static string PlaneName(List<Sheet> g, Vec3 n)
    {
        var cats = g.Select(x => x.Part.Category).Distinct().ToList();
        string orient = Math.Abs(n.Z) > 0.9 ? "level-ish plane" : Math.Abs(n.Z) < 0.1 ? "vertical plane" : "inclined plane";
        return $"{orient}: {string.Join(", ", g.Select(x => x.Part.Mark).Distinct().Take(6))}{(g.Count > 6 ? ", ..." : "")}";
    }

    private static bool Overlap2(Box2 a, Box2 b, double m) => a.MinX < b.MaxX - m && b.MinX < a.MaxX - m && a.MinY < b.MaxY - m && b.MinY < a.MaxY - m;

    private static Vec2 Centroid(List<(Vec2 A, Vec2 B)> segs)
    {
        double w = 0; var c = Vec2.Zero;
        foreach (var (a, b) in segs) { double l = (b - a).Length; c += (a + b) * (0.5 * l); w += l; }
        return w > 0 ? c / w : Vec2.Zero;
    }

    private static (Vec2 A, Vec2 B, double D) SegSetDistance(List<(Vec2 A, Vec2 B)> s1, List<(Vec2 A, Vec2 B)> s2)
    {
        double best = 0.3048 + 1e-9; Vec2 pa = default, pb = default;   // joints farther than 1 ft apart are not joints
        var b2 = s2.Select(x => (x0: Math.Min(x.A.X, x.B.X), x1: Math.Max(x.A.X, x.B.X), y0: Math.Min(x.A.Y, x.B.Y), y1: Math.Max(x.A.Y, x.B.Y))).ToArray();
        var all2 = Box2.Empty; foreach (var x in s2) { all2.Add(x.A); all2.Add(x.B); }
        foreach (var (a, b) in s1)
        {
            double ax0 = Math.Min(a.X, b.X), ax1 = Math.Max(a.X, b.X), ay0 = Math.Min(a.Y, b.Y), ay1 = Math.Max(a.Y, b.Y);
            if (Math.Max(0, Math.Max(all2.MinX - ax1, ax0 - all2.MaxX)) > best || Math.Max(0, Math.Max(all2.MinY - ay1, ay0 - all2.MaxY)) > best) continue;
            for (int k = 0; k < s2.Count; k++)
            {
                var bx = b2[k];
                double dx = Math.Max(0, Math.Max(bx.x0 - ax1, ax0 - bx.x1)), dy = Math.Max(0, Math.Max(bx.y0 - ay1, ay0 - bx.y1));
                if (dx * dx + dy * dy > best * best) continue;
                var (c, d) = s2[k];
                if (Geo2.SegmentsCross(a, b, c, d)) return (a, a, 0);
                foreach (var (p, q0, q1, rev) in new[] { (a, c, d, false), (b, c, d, false), (c, a, b, true), (d, a, b, true) })
                {
                    var cp = Geo2.Closest(p, q0, q1);
                    double l = (cp - p).Length;
                    if (l < best) { best = l; (pa, pb) = rev ? (cp, p) : (p, cp); }
                }
            }
        }
        return (pa, pb, best);
    }

    /// <summary>Outline of a component projected into a frame: cut in the component's own plane (frame u projected into it), then mapped to the target frame.</summary>
    public static FlatShape ShapeIn(Mesh mesh, PlateComponent c, PlateFrame target)
    {
        var n = c.Normal;
        var us = target.U - n * Vec3.Dot(target.U, n);
        if (us.Length < 1e-6) us = Vec3.Cross(n, target.V);
        us = us.Normalized();
        var vs = Vec3.Cross(n, us);
        var sh = PlateGeometry.Outline(mesh, c, c.MidPoint, us, vs);
        Vec2 Map(Vec2 q) => target.To2(c.MidPoint + us * q.X + vs * q.Y);
        var outer = sh.Outer.Select(Map).ToList();
        if (outer.Count >= 3 && Geo2.Area(outer) < 0) outer.Reverse();
        var fs = new FlatShape { Outer = outer, Source = sh.Source, Sweep = sh.Sweep };
        foreach (var l in sh.Inner)
        {
            var ml = l.Select(Map).ToList();
            if (Geo2.Area(ml) > 0) ml.Reverse();
            fs.Inner.Add(ml);
        }
        foreach (var (hc, d) in sh.ExactHoles) fs.ExactHoles.Add((Map(hc), d));
        return fs;
    }

    /// <summary>Mid-plane cut segments of a component, in a frame, clipped to a window.</summary>
    public static List<(Vec2 A, Vec2 B)> SegmentsIn(Mesh mesh, PlateComponent c, PlateFrame target, Box2 window)
    {
        var n = c.Normal;
        var vs = target.V - n * Vec3.Dot(target.V, n);
        if (vs.Length < 1e-6) vs = Vec3.Cross(target.U, n);
        var ms = new MeshSection(c.MidPoint, n, vs.Normalized());
        var res = new List<(Vec2, Vec2)>();
        foreach (var (a, b) in ms.Cut(mesh))
        {
            var pa = target.To2(c.MidPoint + ms.XAxis * a.X + ms.YAxis * a.Y);
            var pb = target.To2(c.MidPoint + ms.XAxis * b.X + ms.YAxis * b.Y);
            if (Clip(ref pa, ref pb, window)) res.Add((pa, pb));
        }
        return res;
    }

    /// <summary>Liang-Barsky clip of a segment to a box.</summary>
    private static bool Clip(ref Vec2 a, ref Vec2 b, Box2 w)
    {
        double t0 = 0, t1 = 1; var d = b - a;
        bool Edge(double p, double q)
        {
            if (Math.Abs(p) < 1e-15) return q >= 0;
            double r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
        if (!Edge(-d.X, a.X - w.MinX) || !Edge(d.X, w.MaxX - a.X) || !Edge(-d.Y, a.Y - w.MinY) || !Edge(d.Y, w.MaxY - a.Y)) return false;
        var a0 = a;
        b = a0 + d * t1; a = a0 + d * t0;
        return (b - a).Length > 1e-7;
    }

    private static FlatShape Shift(FlatShape s, Vec2 d)
    {
        var fs = new FlatShape { Outer = s.Outer.Select(p => p - d).ToList(), Source = s.Source, Sweep = s.Sweep };
        fs.Inner.AddRange(s.Inner.Select(l => l.Select(p => p - d).ToList()));
        foreach (var (c, dd) in s.ExactHoles) fs.ExactHoles.Add((c - d, dd));
        return fs;
    }

    public static string ToText(AssemblyResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{r.Mark} ({r.Kind}) {r.Description} - {r.Bridge}");
        sb.AppendLine($"  {r.Parts} parts, {r.Bolts} bolts ({r.BoltsPlaced} placed in planes), {r.Planes.Count} plane group(s)");
        foreach (var p in r.Planes)
        {
            sb.AppendLine($"  plane {p.No}: {p.Name}");
            sb.AppendLine($"    n ({p.Normal[0]:0.####}, {p.Normal[1]:0.####}, {p.Normal[2]:0.####}) {p.NormalFromVerticalDeg:0.###}° from vertical; u = {p.Reference}");
            foreach (var l in p.Layers)
                sb.AppendLine($"    {(l.Neighbour ? "  (adjoining)" : "  layer")} {l.Mark}{(l.Leg > 0 ? $" leg {l.Leg + 1}" : "")}: t {l.Thickness.Text}, faces {l.NearFace.Text} .. {l.FarFace.Text}" +
                    (l.Width is { } w ? $", {w.Text} x {l.Height!.Value.Text}" : "") + (l.Holes > 0 ? $", {l.Holes} holes" : "") +
                    (l.HoleGroups is { Count: > 0 } hg ? " [" + string.Join("; ", hg.Select(g => $"{g.Rows}x{g.Columns} u: {g.SpacingU} v: {g.SpacingV} edge -u {Get(g, "-u")} +u {Get(g, "+u")} -v {Get(g, "-v")} +v {Get(g, "+v")}")) + "]" : ""));
            foreach (var g in p.StackGaps) sb.AppendLine($"    gap {g.Lower} -> {g.Upper}: {g.Gap.Text} ({g.Gap.Mm:0.0} mm)");
            foreach (var j in p.JointGaps) sb.AppendLine($"    joint {j.A} | {j.B}: gap {j.Gap.Text} ({j.Gap.Mm:0.0} mm)");
            sb.AppendLine($"    bolts: {p.Bolts.Count}{(p.BoltsPerSide.Count > 0 ? " (" + string.Join(", ", p.BoltsPerSide.Select(kv => $"{kv.Value} in {kv.Key}")) + ")" : "")}");
            foreach (var g in p.BoltGroups) sb.AppendLine($"      group {g.Group}: {g.Holes} = {g.Rows} row(s) x {g.Columns} column(s); u: {g.SpacingU}; v: {g.SpacingV}");
            foreach (var n in p.Notes) sb.AppendLine($"    note: {n}");
        }
        if (r.NotInPlanes.Count > 0) sb.AppendLine($"  not in a plane: {string.Join(", ", r.NotInPlanes)}");
        foreach (var n in r.Notes) sb.AppendLine($"  note: {n}");
        return sb.ToString();
        static string Get(HoleGroup g, string k) => g.EdgeDistance.TryGetValue(k, out var v) ? v.Text : "-";
    }
}
