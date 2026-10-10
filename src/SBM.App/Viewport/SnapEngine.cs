using System.Numerics;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.Core.Geometry;

namespace SBM.App.Viewport;

[Flags]
public enum SnapModes
{
    None = 0,
    Endpoint = 1,
    Midpoint = 2,
    Center = 4,
    Intersection = 8,
    Perpendicular = 16,
    Nearest = 32,
    /// <summary>Bearing points, work points, splices, girder web lines (from the framing).</summary>
    Domain = 64,
    All = Endpoint | Midpoint | Center | Intersection | Perpendicular | Nearest | Domain,
}

public enum SnapKind { Free, Surface, Endpoint, Midpoint, Center, Intersection, Perpendicular, Nearest, Domain }

/// <summary>A solid (merged mesh with a pick table, an instanced mesh, or an untagged reference surface) that can be snapped to.</summary>
public sealed class SnapSource
{
    public required MeshGeometry3D Mesh { get; init; }
    public PickTable? Table { get; init; }
    /// <summary>Tag of an untagged mesh (reference surfaces).</summary>
    public object? FixedTag { get; init; }
    public string Label { get; init; } = "";
    public string Layer { get; init; } = "";
    public Element3D? Model { get; init; }
    /// <summary>Transparent solids do not hide other geometry from snapping (you can see through them).</summary>
    public bool Opaque { get; init; } = true;
    public List<Matrix4x4>? Instances { get; init; }
    public List<object>? InstanceTags { get; init; }
    internal float LocalRadius;
    internal Vector3 LocalMin, LocalMax;
    internal int Id;
}

/// <summary>Line work: either a Helix line geometry (segment pairs through its indices) or explicit segment pairs.</summary>
public sealed class SnapLines
{
    public LineGeometry3D? Geometry { get; init; }
    public Vector3[]? Pairs { get; init; }
    public Element3D? Model { get; init; }
    public string Layer { get; init; } = "";
    public string Label { get; init; } = "";
    /// <summary>Only snappable in the 3D view (domain lines that duplicate plan linework).</summary>
    public bool Only3D { get; init; }
    /// <summary>Domain lines are not hidden by solids in front of them (web lines inside girders).</summary>
    public bool Domain { get; init; }
    internal Vector3[] Segs = Array.Empty<Vector3>();
    internal int Id;
}

public sealed record SnapPoint(Vector3 P, string Label, bool Only3D = false);

public sealed class SnapEnvironment
{
    public bool Plan;
    /// <summary>Snap to geometry hidden behind opaque solids too.</summary>
    public bool XRay;
    public Func<Element3D?, bool> ModelVisible = _ => true;
    public Func<object, bool> IsHidden = _ => false;
    public Func<Vector3, bool> Kept = _ => true;
}

public sealed class SnapRequest
{
    public required Vector3 Origin;
    /// <summary>Unit view direction (orthographic: every pixel ray is parallel).</summary>
    public required Vector3 Dir;
    public required Vector3 ViewRight;
    public required Vector3 ViewUp;
    /// <summary>World size of one pixel, and the capture tolerance in pixels.</summary>
    public required float PixelSize;
    public float TolerancePx = 12;
    public SnapModes Modes = SnapModes.All;
    /// <summary>Previous measured point (scene-local) for Perpendicular snaps.</summary>
    public Vector3? Previous;
}

public sealed class SnapHit
{
    public Vector3 P;
    public SnapKind Kind;
    public string Text = "";
    public object? Tag;
    public string Layer = "";
    /// <summary>Distance along the view ray (smaller = nearer the camera).</summary>
    public float Depth;
    public float ScreenDistance;
    public bool OnObject;
}

public sealed class SnapStats
{
    public int Leaves, Triangles, Candidates, OcclusionTests;
    public double Ms;
}

/// <summary>
/// Object-snap engine. A bounding-volume hierarchy over chunks of triangles, line segments and fastener instances (built
/// once per scene, in scene-local float coordinates) answers "what is under this pixel?" in a few milliseconds even for
/// models with millions of triangles: only chunks whose box meets the pixel's tolerance cylinder are examined. Candidates
/// (endpoints, midpoints, centers, intersections, perpendicular feet, nearest points, domain points) within the pixel
/// tolerance are ranked by screen distance with a per-mode bonus, and the best one that is not hidden behind an opaque solid wins.
/// </summary>
public sealed class SnapIndex
{
    private enum LeafKind : byte { Tris, Instances, Segments }

    private sealed class Leaf
    {
        public LeafKind Kind;
        public SnapSource? Src;
        public SnapLines? Lines;
        public int I0, I1;
        public Vector3 Min = new(float.MaxValue), Max = new(float.MinValue);
    }

    private struct Node
    {
        public Vector3 Min, Max;
        public int Left, Right; // children, or Left = -1 and Right = leaf index
    }

    private const int TrisPerLeaf = 96, SegsPerLeaf = 64, InstPerLeaf = 24;

    private readonly List<SnapSource> _sources = new();
    private readonly List<SnapLines> _lines = new();
    private readonly List<Leaf> _leaves = new();
    private Node[] _nodes = Array.Empty<Node>();
    private int _root = -1;
    private SnapPoint[] _points = Array.Empty<SnapPoint>();
    private readonly SBM.Core.Geometry.Vec3 _origin;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<object, (Vector3 p, SnapKind k, string label)[]> _extrasCache = new();

    /// <summary>Extra snap points of a picked object (bar vertices, column centers, strand ends), in world coordinates.</summary>
    public Func<object, IEnumerable<(SBM.Core.Geometry.Vec3 p, SnapKind kind, string label)>?>? ExtraPoints { get; set; }
    /// <summary>Tags whose mesh vertices and edges are noise (tubes): they snap through <see cref="ExtraPoints"/> only.</summary>
    public Func<object, bool>? MeshIsNoise { get; set; }

    /// <summary>Per-layer summary (chunks) for diagnostics.</summary>
    public IEnumerable<string> Describe()
    {
        foreach (var g in _leaves.Where(l => l.Src != null).GroupBy(l => l.Src!.Layer))
            yield return $"{g.Key}: {g.Count()} chunks";
        foreach (var g in _leaves.Where(l => l.Lines != null).GroupBy(l => "lines " + l.Lines!.Layer))
            yield return $"{g.Key}: {g.Count()} chunks";
    }

    public int LeafCount => _leaves.Count;
    public long TriangleCount { get; private set; }
    public double BuildMs { get; private set; }

    public SnapIndex(SBM.Core.Geometry.Vec3 sceneOrigin) => _origin = sceneOrigin;

    public void Add(SnapSource s) { s.Id = _sources.Count; _sources.Add(s); }
    public void Add(SnapLines l) { l.Id = _lines.Count; _lines.Add(l); }
    public void SetPoints(IEnumerable<SnapPoint> pts) => _points = pts.ToArray();

    // ------------------------------------------------------------------ build

    public void Build()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // Leaf shells (cheap, sequential), then bounds in parallel.
        foreach (var s in _sources)
        {
            var g = s.Mesh;
            if (g.Positions == null || g.Indices == null) continue;
            if (s.Instances != null)
            {
                // Local bounds / radius of the instanced mesh.
                var lo = new Vector3(float.MaxValue);
                var hi = new Vector3(float.MinValue);
                for (int i = 0; i < g.Positions.Count; i++) { lo = Vector3.Min(lo, g.Positions[i]); hi = Vector3.Max(hi, g.Positions[i]); }
                s.LocalMin = lo; s.LocalMax = hi;
                float r = 0;
                for (int i = 0; i < g.Positions.Count; i++) r = Math.Max(r, g.Positions[i].Length());
                s.LocalRadius = r;
                for (int i = 0; i < s.Instances.Count; i += InstPerLeaf)
                    _leaves.Add(new Leaf { Kind = LeafKind.Instances, Src = s, I0 = i, I1 = Math.Min(i + InstPerLeaf, s.Instances.Count) });
                TriangleCount += (long)(g.Indices.Count / 3) * s.Instances.Count;
            }
            else
            {
                int tris = g.Indices.Count / 3;
                TriangleCount += tris;
                for (int i = 0; i < tris; i += TrisPerLeaf)
                    _leaves.Add(new Leaf { Kind = LeafKind.Tris, Src = s, I0 = i, I1 = Math.Min(i + TrisPerLeaf, tris) });
            }
        }
        foreach (var l in _lines)
        {
            if (l.Pairs != null) l.Segs = l.Pairs;
            else if (l.Geometry?.Positions is { } pos && l.Geometry.Indices is { } ind)
            {
                var segs = new Vector3[ind.Count / 2 * 2];
                for (int i = 0; i + 1 < ind.Count; i += 2) { segs[i] = pos[ind[i]]; segs[i + 1] = pos[ind[i + 1]]; }
                l.Segs = segs;
            }
            int n = l.Segs.Length / 2;
            for (int i = 0; i < n; i += SegsPerLeaf)
                _leaves.Add(new Leaf { Kind = LeafKind.Segments, Lines = l, I0 = i, I1 = Math.Min(i + SegsPerLeaf, n) });
        }

        Parallel.For(0, _leaves.Count, k =>
        {
            var lf = _leaves[k];
            var lo = new Vector3(float.MaxValue);
            var hi = new Vector3(float.MinValue);
            switch (lf.Kind)
            {
                case LeafKind.Tris:
                {
                    var pos = lf.Src!.Mesh.Positions!;
                    var ind = lf.Src.Mesh.Indices!;
                    for (int i = lf.I0 * 3; i < lf.I1 * 3; i++)
                    {
                        var p = pos[ind[i]];
                        lo = Vector3.Min(lo, p);
                        hi = Vector3.Max(hi, p);
                    }
                    break;
                }
                case LeafKind.Instances:
                    for (int i = lf.I0; i < lf.I1; i++)
                    {
                        var (a, b) = SnapMath.TransformBox(lf.Src!.LocalMin, lf.Src.LocalMax, lf.Src.Instances![i]);
                        lo = Vector3.Min(lo, a);
                        hi = Vector3.Max(hi, b);
                    }
                    break;
                default:
                    for (int i = lf.I0 * 2; i < lf.I1 * 2; i++)
                    {
                        lo = Vector3.Min(lo, lf.Lines!.Segs[i]);
                        hi = Vector3.Max(hi, lf.Lines.Segs[i]);
                    }
                    break;
            }
            lf.Min = lo;
            lf.Max = hi;
        });

        // Hierarchy: median split on the longest axis of the leaf centers.
        var order = Enumerable.Range(0, _leaves.Count).ToArray();
        var nodes = new List<Node>(_leaves.Count * 2);
        _root = _leaves.Count == 0 ? -1 : BuildNode(order, 0, order.Length, nodes);
        _nodes = nodes.ToArray();
        BuildMs = sw.Elapsed.TotalMilliseconds;
    }

    private int BuildNode(int[] order, int start, int count, List<Node> nodes)
    {
        var lo = new Vector3(float.MaxValue);
        var hi = new Vector3(float.MinValue);
        var clo = new Vector3(float.MaxValue);
        var chi = new Vector3(float.MinValue);
        for (int i = start; i < start + count; i++)
        {
            var l = _leaves[order[i]];
            lo = Vector3.Min(lo, l.Min);
            hi = Vector3.Max(hi, l.Max);
            var c = (l.Min + l.Max) * 0.5f;
            clo = Vector3.Min(clo, c);
            chi = Vector3.Max(chi, c);
        }
        int idx = nodes.Count;
        nodes.Add(new Node { Min = lo, Max = hi, Left = -1, Right = -1 });
        if (count == 1)
        {
            nodes[idx] = new Node { Min = lo, Max = hi, Left = -1, Right = order[start] };
            return idx;
        }
        var ext = chi - clo;
        int axis = ext.X >= ext.Y && ext.X >= ext.Z ? 0 : ext.Y >= ext.Z ? 1 : 2;
        var keys = new float[count];
        var sub = new int[count];
        for (int i = 0; i < count; i++)
        {
            var l = _leaves[order[start + i]];
            var c = (l.Min + l.Max) * 0.5f;
            keys[i] = axis == 0 ? c.X : axis == 1 ? c.Y : c.Z;
            sub[i] = order[start + i];
        }
        Array.Sort(keys, sub);
        Array.Copy(sub, 0, order, start, count);
        int half = count / 2;
        int left = BuildNode(order, start, half, nodes);
        int right = BuildNode(order, start + half, count - half, nodes);
        nodes[idx] = new Node { Min = lo, Max = hi, Left = left, Right = right };
        return idx;
    }

    // ------------------------------------------------------------------ query

    private sealed class Cand
    {
        public Vector3 P;
        public SnapKind Kind;
        public object? Tag;
        public string Layer = "";
        public string Label = "";
        public float PxDist;
        public float Score;
        public float Depth;
        public bool CheckOcclusion;
        public SnapSource? Src;
    }

    private sealed class Ctx
    {
        public required SnapRequest Req;
        public required SnapEnvironment Env;
        public SnapStats Stats = new();
        public Vector3 InvDir;
        public float Radius, R2;
        public List<Cand> Cands = new();
        public HashSet<(int, int, int)> Seen = new();
        public byte[] SrcVis = Array.Empty<byte>(), LineVis = Array.Empty<byte>();
        public float SurfaceT = float.MaxValue;
        public Vector3 SurfaceP;
        public object? SurfaceTag;
        public SnapSource? SurfaceSrc;
        public readonly List<(Vector3 a, Vector3 b, float depthA, object? tag, string layer)> InterSegs = new();
        public readonly HashSet<object> Touched = new();
    }

    private static float Bonus(SnapKind k) => k switch
    {
        SnapKind.Domain => 9f, SnapKind.Endpoint => 6f, SnapKind.Center => 6f, SnapKind.Intersection => 5f,
        SnapKind.Midpoint => 4f, SnapKind.Perpendicular => 3f, _ => 0f,
    };

    /// <summary>Best snap under the pixel, or the surface point under it, or null.</summary>
    public SnapHit? Find(SnapRequest req, SnapEnvironment env, out SnapStats stats)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cx = new Ctx { Req = req, Env = env };
        stats = cx.Stats;
        cx.InvDir = SnapMath.Reciprocal(req.Dir);
        cx.Radius = req.TolerancePx * req.PixelSize;
        cx.R2 = cx.Radius * cx.Radius;
        cx.SrcVis = new byte[_sources.Count];
        cx.LineVis = new byte[_lines.Count];

        if (_root >= 0) Visit(_root, cx);

        // Surface point under the pixel (the "Nearest" snap on solids).
        if (cx.SurfaceT < float.MaxValue)
            cx.Cands.Add(new Cand
            {
                P = cx.SurfaceP, Kind = SnapKind.Surface, Tag = cx.SurfaceTag, Layer = cx.SurfaceSrc?.Layer ?? "", Label = cx.SurfaceSrc?.Label ?? "",
                PxDist = 0, Score = req.TolerancePx + 1, Depth = cx.SurfaceT, Src = cx.SurfaceSrc,
            });

        // Domain points (bearings, work points, splices ...).
        if ((req.Modes & SnapModes.Domain) != 0)
            foreach (var dp in _points)
            {
                if (dp.Only3D && env.Plan) continue;
                float d2 = SnapMath.DistanceToRaySq(dp.P, req.Origin, req.Dir, out float t);
                if (d2 > cx.R2 || !env.Kept(dp.P)) continue;
                float px = MathF.Sqrt(d2) / req.PixelSize;
                cx.Cands.Add(new Cand { P = dp.P, Kind = SnapKind.Domain, Label = dp.Label, PxDist = px, Score = px - Bonus(SnapKind.Domain), Depth = t });
            }

        if ((req.Modes & SnapModes.Intersection) != 0) Intersections(cx);

        cx.Stats.Candidates = cx.Cands.Count;
        cx.Cands.Sort((a, b) => a.Score.CompareTo(b.Score));
        SnapHit? result = null;
        int tested = 0;
        foreach (var c in cx.Cands)
        {
            if (c.CheckOcclusion && !env.XRay)
            {
                if (tested++ >= 24) break;
                cx.Stats.OcclusionTests++;
                if (IsOccluded(cx, c.P, c.Depth)) continue;
            }
            result = new SnapHit
            {
                P = c.P, Kind = c.Kind, Tag = c.Tag, Layer = c.Layer, Text = c.Label, Depth = c.Depth, ScreenDistance = c.PxDist,
                OnObject = c.Kind != SnapKind.Free,
            };
            break;
        }
        cx.Stats.Ms = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    private void Visit(int ni, Ctx cx)
    {
        ref readonly var n = ref _nodes[ni];
        if (!SnapMath.LineHitsBox(cx.Req.Origin, cx.InvDir, n.Min, n.Max, cx.Radius)) return;
        if (n.Left < 0) { ProcessLeaf(_leaves[n.Right], cx); return; }
        Visit(n.Left, cx);
        Visit(n.Right, cx);
    }

    private bool SrcVisible(SnapSource s, Ctx cx)
    {
        ref byte v = ref cx.SrcVis[s.Id];
        if (v == 0) v = (byte)(cx.Env.ModelVisible(s.Model) ? 1 : 2);
        return v == 1;
    }

    private bool LinesVisible(SnapLines l, Ctx cx)
    {
        ref byte v = ref cx.LineVis[l.Id];
        if (v == 0) v = (byte)((l.Only3D && cx.Env.Plan) || !cx.Env.ModelVisible(l.Model) ? 2 : 1);
        return v == 1;
    }

    private void ProcessLeaf(Leaf lf, Ctx cx)
    {
        cx.Stats.Leaves++;
        switch (lf.Kind)
        {
            case LeafKind.Tris:
                if (SrcVisible(lf.Src!, cx)) ProcessTris(lf, cx);
                break;
            case LeafKind.Instances:
                if (SrcVisible(lf.Src!, cx)) ProcessInstances(lf, cx);
                break;
            default:
                if (LinesVisible(lf.Lines!, cx)) ProcessSegments(lf, cx);
                break;
        }
    }

    private static object? TagOf(SnapSource s, int tri) =>
        s.Table != null ? s.Table.Find(tri, out _, out _) : s.FixedTag;

    private void AddCand(Ctx cx, Vector3 p, SnapKind kind, float d2, float depth, object? tag, SnapSource? src, string layer, string label, bool occlusion = true)
    {
        if (cx.Cands.Count > 3000) return;
        var key = ((int)kind, (int)MathF.Round(p.X * 2000), (int)MathF.Round(p.Y * 2000) * 31 + (int)MathF.Round(p.Z * 2000));
        if (!cx.Seen.Add(key)) return;
        float px = MathF.Sqrt(d2) / cx.Req.PixelSize;
        cx.Cands.Add(new Cand { P = p, Kind = kind, Tag = tag, Layer = layer, Label = label, PxDist = px, Score = px - Bonus(kind), Depth = depth, CheckOcclusion = occlusion, Src = src });
    }

    // ----- triangles

    private void ProcessTris(Leaf lf, Ctx cx)
    {
        var s = lf.Src!;
        var pos = s.Mesh.Positions!;
        var ind = s.Mesh.Indices!;
        var req = cx.Req;
        var env = cx.Env;
        var modes = req.Modes;
        bool wantVerts = (modes & SnapModes.Endpoint) != 0;
        bool wantMids = (modes & SnapModes.Midpoint) != 0;
        bool wantPerp = (modes & SnapModes.Perpendicular) != 0 && req.Previous != null;
        float pad = cx.Radius;
        for (int t = lf.I0; t < lf.I1; t++)
        {
            var a = pos[ind[3 * t]];
            var b = pos[ind[3 * t + 1]];
            var c = pos[ind[3 * t + 2]];
            cx.Stats.Triangles++;
            // Triangle box vs the pixel cylinder: skips most triangles of a leaf quickly.
            var lo = Vector3.Min(a, Vector3.Min(b, c));
            var hi = Vector3.Max(a, Vector3.Max(b, c));
            if (!SnapMath.LineHitsBox(req.Origin, cx.InvDir, lo, hi, pad)) continue;

            var tag = TagOf(s, t);
            if (tag != null && env.IsHidden(tag)) continue;
            if (tag != null) Touch(cx, tag);

            if (SnapMath.RayTriangle(req.Origin, req.Dir, a, b, c, out float th) && th < cx.SurfaceT)
            {
                var hp = req.Origin + req.Dir * th;
                if (env.Kept(hp)) { cx.SurfaceT = th; cx.SurfaceP = hp; cx.SurfaceTag = tag; cx.SurfaceSrc = s; }
            }
            bool noise = tag != null && MeshIsNoise != null && MeshIsNoise(tag);
            if (noise) continue;

            if (wantVerts)
                for (int k = 0; k < 3; k++)
                {
                    var v = k == 0 ? a : k == 1 ? b : c;
                    float d2 = SnapMath.DistanceToRaySq(v, req.Origin, req.Dir, out float tv);
                    if (d2 > cx.R2 || !env.Kept(v)) continue;
                    AddCand(cx, v, SnapKind.Endpoint, d2, tv, tag, s, s.Layer, s.Label);
                }
            if (wantMids || wantPerp)
                for (int k = 0; k < 3; k++)
                {
                    var p0 = k == 0 ? a : k == 1 ? b : c;
                    var p1 = k == 0 ? b : k == 1 ? c : a;
                    if (wantMids)
                    {
                        var m = (p0 + p1) * 0.5f;
                        float d2 = SnapMath.DistanceToRaySq(m, req.Origin, req.Dir, out float tm);
                        if (d2 <= cx.R2 && env.Kept(m) && IsFeatureEdge(s, lf, t, p0, p1))
                            AddCand(cx, m, SnapKind.Midpoint, d2, tm, tag, s, s.Layer, s.Label);
                    }
                    if (wantPerp && SnapMath.PerpendicularFoot(req.Previous!.Value, p0, p1, out var foot))
                    {
                        float d2 = SnapMath.DistanceToRaySq(foot, req.Origin, req.Dir, out float tf);
                        if (d2 <= cx.R2 && env.Kept(foot) && IsFeatureEdge(s, lf, t, p0, p1))
                            AddCand(cx, foot, SnapKind.Perpendicular, d2, tf, tag, s, s.Layer, s.Label);
                    }
                }
        }
        AddExtras(cx);
    }

    private void Touch(Ctx cx, object tag)
    {
        if (cx.Touched.Count < 64) cx.Touched.Add(tag);
    }

    /// <summary>Per-object extra points (bar vertices, column centers) for tags near the pixel.</summary>
    private void AddExtras(Ctx cx)
    {
        if (cx.Touched.Count == 0 || ExtraPoints == null) return;
        var req = cx.Req;
        foreach (var tag in cx.Touched)
        {
            var pts = _extrasCache.GetOrAdd(tag, t =>
                (ExtraPoints(t) ?? Enumerable.Empty<(SBM.Core.Geometry.Vec3, SnapKind, string)>())
                .Select(e => (new Vector3((float)(e.p.X - _origin.X), (float)(e.p.Y - _origin.Y), (float)(e.p.Z - _origin.Z)), e.kind, e.label)).ToArray());
            for (int i = 0; i < pts.Length; i++)
            {
                var (p, kind, label) = pts[i];
                if ((req.Modes & KindMode(kind)) == 0) continue;
                float d2 = SnapMath.DistanceToRaySq(p, req.Origin, req.Dir, out float t);
                if (d2 > cx.R2 || !cx.Env.Kept(p)) continue;
                AddCand(cx, p, kind, d2, t, tag, null, "", label);
            }
            // Centerline segments of bars / strands: midpoint, nearest, perpendicular.
            if (pts.Length > 1 && pts[0].label.EndsWith(" vertex", StringComparison.Ordinal))
                for (int i = 0; i + 1 < pts.Length; i++)
                    SegmentCandidates(cx, pts[i].p, pts[i + 1].p, tag, null, "", pts[0].label[..^7] + " centerline", true);
        }
        cx.Touched.Clear();
    }

    private static SnapModes KindMode(SnapKind k) => k switch
    {
        SnapKind.Endpoint => SnapModes.Endpoint, SnapKind.Midpoint => SnapModes.Midpoint, SnapKind.Center => SnapModes.Center,
        SnapKind.Intersection => SnapModes.Intersection, SnapKind.Perpendicular => SnapModes.Perpendicular, SnapKind.Domain => SnapModes.Domain,
        _ => SnapModes.Nearest,
    };

    /// <summary>An edge is a feature edge unless a coplanar neighbor in the same chunk shares it (quad diagonals, fan seams).</summary>
    private static bool IsFeatureEdge(SnapSource s, Leaf lf, int tri, Vector3 e0, Vector3 e1)
    {
        var pos = s.Mesh.Positions!;
        var ind = s.Mesh.Indices!;
        var n0 = TriNormal(pos[ind[3 * tri]], pos[ind[3 * tri + 1]], pos[ind[3 * tri + 2]]);
        int lo = Math.Max(0, lf.I0 - 8), hi = lf.I1 + 8;
        int total = ind.Count / 3;
        for (int t = lo; t < Math.Min(hi, total); t++)
        {
            if (t == tri) continue;
            var a = pos[ind[3 * t]];
            var b = pos[ind[3 * t + 1]];
            var c = pos[ind[3 * t + 2]];
            if (!(SharesEdge(a, b, c, e0, e1))) continue;
            if (MathF.Abs(Vector3.Dot(n0, TriNormal(a, b, c))) > 0.9995f) return false;
        }
        return true;
    }

    private static bool Near(Vector3 p, Vector3 q) => Vector3.DistanceSquared(p, q) < 1e-8f;

    private static bool SharesEdge(Vector3 a, Vector3 b, Vector3 c, Vector3 e0, Vector3 e1)
    {
        bool h0 = Near(a, e0) || Near(b, e0) || Near(c, e0);
        bool h1 = Near(a, e1) || Near(b, e1) || Near(c, e1);
        return h0 && h1;
    }

    private static Vector3 TriNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        float l = n.Length();
        return l > 0 ? n / l : Vector3.UnitZ;
    }

    // ----- instances (bolts, studs, shaped bars)

    private void ProcessInstances(Leaf lf, Ctx cx)
    {
        var s = lf.Src!;
        var req = cx.Req;
        var env = cx.Env;
        var pos = s.Mesh.Positions!;
        var ind = s.Mesh.Indices!;
        for (int i = lf.I0; i < lf.I1; i++)
        {
            var m = s.Instances![i];
            var org = m.Translation;
            float d2o = SnapMath.DistanceToRaySq(org, req.Origin, req.Dir, out float to);
            float reach = s.LocalRadius + cx.Radius;
            if (d2o > reach * reach) continue;
            var tag = s.InstanceTags != null && i < s.InstanceTags.Count ? s.InstanceTags[i] : null;
            if (tag != null && env.IsHidden(tag)) continue;
            if (!env.Kept(org)) continue;
            // Center of a fastener (frame origin on its axis).
            if ((req.Modes & SnapModes.Center) != 0 && d2o <= cx.R2 && tag is SBM.Core.Steel.BoltInstance or SBM.Core.Steel.StudInstance)
            {
                string what = tag is SBM.Core.Steel.StudInstance ? "Stud" : "Bolt";
                AddCand(cx, org, SnapKind.Center, d2o, to, tag, s, s.Layer, what + " center");
            }
            if (tag != null) Touch(cx, tag);
            // Surface hit: transform the ray into the instance's frame.
            if (!Matrix4x4.Invert(m, out var inv)) continue;
            var lo = Vector3.Transform(req.Origin, inv);
            var ld = Vector3.TransformNormal(req.Dir, inv);
            for (int t = 0; t < ind.Count / 3; t++)
            {
                cx.Stats.Triangles++;
                if (SnapMath.RayTriangle(lo, ld, pos[ind[3 * t]], pos[ind[3 * t + 1]], pos[ind[3 * t + 2]], out float th) && th < cx.SurfaceT)
                {
                    var hp = req.Origin + req.Dir * th;
                    if (!env.Kept(hp)) continue;
                    cx.SurfaceT = th; cx.SurfaceP = hp; cx.SurfaceTag = tag; cx.SurfaceSrc = s;
                }
            }
        }
        AddExtras(cx);
    }

    // ----- line segments

    private void ProcessSegments(Leaf lf, Ctx cx)
    {
        var l = lf.Lines!;
        var req = cx.Req;
        for (int i = lf.I0; i < lf.I1; i++)
        {
            var a = l.Segs[2 * i];
            var b = l.Segs[2 * i + 1];
            var lo = Vector3.Min(a, b);
            var hi = Vector3.Max(a, b);
            if (!SnapMath.LineHitsBox(req.Origin, cx.InvDir, lo, hi, cx.Radius)) continue;
            SegmentCandidates(cx, a, b, null, null, l.Layer, l.Label.Length > 0 ? l.Label : l.Layer, !l.Domain, l.Domain);
        }
    }

    private void SegmentCandidates(Ctx cx, Vector3 a, Vector3 b, object? tag, SnapSource? src, string layer, string label, bool occlusion, bool domain = false)
    {
        var req = cx.Req;
        var env = cx.Env;
        var modes = req.Modes;
        var ends = domain ? SnapKind.Domain : SnapKind.Endpoint;
        SnapModes endMode = domain ? SnapModes.Domain : SnapModes.Endpoint;
        if (tag != null && env.IsHidden(tag)) return;
        if ((modes & endMode) != 0)
        {
            foreach (var v in new[] { a, b })
            {
                float d2 = SnapMath.DistanceToRaySq(v, req.Origin, req.Dir, out float t);
                if (d2 <= cx.R2 && env.Kept(v)) AddCand(cx, v, ends, d2, t, tag, src, layer, label, occlusion);
            }
        }
        var mid = (a + b) * 0.5f;
        if ((modes & SnapModes.Midpoint) != 0)
        {
            float d2 = SnapMath.DistanceToRaySq(mid, req.Origin, req.Dir, out float t);
            if (d2 <= cx.R2 && env.Kept(mid)) AddCand(cx, mid, SnapKind.Midpoint, d2, t, tag, src, layer, label, occlusion);
        }
        if ((modes & SnapModes.Perpendicular) != 0 && req.Previous is { } prev && SnapMath.PerpendicularFoot(prev, a, b, out var foot))
        {
            float d2 = SnapMath.DistanceToRaySq(foot, req.Origin, req.Dir, out float t);
            if (d2 <= cx.R2 && env.Kept(foot)) AddCand(cx, foot, SnapKind.Perpendicular, d2, t, tag, src, layer, label, occlusion);
        }
        var q = SnapMath.ClosestOnSegmentToRay(req.Origin, req.Dir, a, b, out float dist, out float rt);
        if (dist <= cx.Radius && env.Kept(q))
        {
            if ((modes & SnapModes.Nearest) != 0) AddCand(cx, q, SnapKind.Nearest, dist * dist, rt, tag, src, layer, label, occlusion);
            if ((modes & SnapModes.Intersection) != 0 && cx.InterSegs.Count < 48) cx.InterSegs.Add((a, b, rt, tag, layer));
        }
    }

    /// <summary>Apparent intersections (in the view plane) of the line segments near the pixel.</summary>
    private void Intersections(Ctx cx)
    {
        var req = cx.Req;
        var s = cx.InterSegs;
        for (int i = 0; i < s.Count; i++)
            for (int j = i + 1; j < s.Count; j++)
            {
                Vector2 P(Vector3 v) => new(Vector3.Dot(v, req.ViewRight), Vector3.Dot(v, req.ViewUp));
                var (a1, b1, _, _, l1) = s[i];
                var (a2, b2, _, _, l2) = s[j];
                if (!SnapMath.SegmentIntersect2D(P(a1), P(b1), P(a2), P(b2), out float u, out float v)) continue;
                var p1 = a1 + (b1 - a1) * u;
                var p2 = a2 + (b2 - a2) * v;
                // The apparent intersection lies on the segment nearer the camera.
                float t1 = Vector3.Dot(p1 - req.Origin, req.Dir), t2 = Vector3.Dot(p2 - req.Origin, req.Dir);
                var p = t1 <= t2 ? p1 : p2;
                float t = Math.Min(t1, t2);
                float d2 = SnapMath.DistanceToRaySq(p, req.Origin, req.Dir, out _);
                if (d2 > cx.R2 || !cx.Env.Kept(p)) continue;
                AddCand(cx, p, SnapKind.Intersection, d2, t, s[i].tag ?? s[j].tag, null, l1.Length > 0 ? l1 : l2, "Apparent intersection");
            }
    }

    // ----- occlusion

    /// <summary>Is there an opaque triangle between the camera and the candidate (nearer than the candidate by more than a pixel)?</summary>
    private bool IsOccluded(Ctx cx, Vector3 p, float depth)
    {
        var req = cx.Req;
        // The ray through the candidate (same direction), starting on the camera plane: depth is measured the same way.
        var o = p - req.Dir * depth;
        float eps = MathF.Max(0.004f, req.PixelSize * 1.2f);
        float limit = depth - eps;
        var inv = cx.InvDir;
        return Occluded(_root, o, req.Dir, inv, limit, cx);
    }

    private bool Occluded(int ni, Vector3 o, Vector3 d, Vector3 inv, float limit, Ctx cx)
    {
        if (ni < 0) return false;
        ref readonly var n = ref _nodes[ni];
        if (!SnapMath.LineHitsBox(o, inv, n.Min, n.Max, 0.002f)) return false;
        if (n.Left >= 0) return Occluded(n.Left, o, d, inv, limit, cx) || Occluded(n.Right, o, d, inv, limit, cx);
        var lf = _leaves[n.Right];
        if (lf.Kind == LeafKind.Segments) return false;
        var s = lf.Src!;
        if (!s.Opaque || !SrcVisible(s, cx)) return false;
        var pos = s.Mesh.Positions!;
        var ind = s.Mesh.Indices!;
        if (lf.Kind == LeafKind.Tris)
        {
            for (int t = lf.I0; t < lf.I1; t++)
            {
                if (!SnapMath.RayTriangle(o, d, pos[ind[3 * t]], pos[ind[3 * t + 1]], pos[ind[3 * t + 2]], out float th) || th >= limit) continue;
                if (!cx.Env.Kept(o + d * th)) continue;
                var tag = TagOf(s, t);
                if (tag != null && cx.Env.IsHidden(tag)) continue;
                return true;
            }
            return false;
        }
        for (int i = lf.I0; i < lf.I1; i++)
        {
            var m = s.Instances![i];
            if (!Matrix4x4.Invert(m, out var im)) continue;
            var (bmin, bmax) = SnapMath.TransformBox(s.LocalMin, s.LocalMax, m);
            if (!SnapMath.LineHitsBox(o, inv, bmin, bmax, 0.001f)) continue;
            var lo = Vector3.Transform(o, im);
            var ld = Vector3.TransformNormal(d, im);
            for (int t = 0; t < ind.Count / 3; t++)
                if (SnapMath.RayTriangle(lo, ld, pos[ind[3 * t]], pos[ind[3 * t + 1]], pos[ind[3 * t + 2]], out float th) && th < limit)
                {
                    var tag = s.InstanceTags != null && i < s.InstanceTags.Count ? s.InstanceTags[i] : null;
                    if (tag != null && cx.Env.IsHidden(tag)) break;
                    if (cx.Env.Kept(o + d * th)) return true;
                }
        }
        return false;
    }
}
