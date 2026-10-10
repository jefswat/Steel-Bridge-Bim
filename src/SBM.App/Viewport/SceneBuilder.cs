using System.IO;
using System.Numerics;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using SBM.Core.Drawing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.App.Viewport;

/// <summary>Maps triangles of a merged (batched) mesh back to the objects they came from, for picking.</summary>
public sealed class PickTable
{
    public List<int> TriStart { get; } = new();
    public List<object> Tags { get; } = new();

    public object? Find(int triangle, out int start, out int end)
    {
        int i = TriStart.BinarySearch(triangle);
        if (i < 0) i = ~i - 1;
        start = end = 0;
        if (i < 0 || i >= Tags.Count) return null;
        start = TriStart[i];
        end = i + 1 < TriStart.Count ? TriStart[i + 1] : int.MaxValue;
        return Tags[i];
    }
}

/// <summary>Pick context for reinforcing and substructure solids.</summary>
public sealed record RebarPick(SBM.Core.Concrete.BarPlacement Bar, SBM.Core.Concrete.BarMark Mark);
public sealed record SubPick(SBM.Core.Substructure.SubstructureUnit Unit, SBM.Core.Substructure.SubSolid Solid);
public sealed record PsBeamPick(SBM.Core.Prestressed.PrestressedModel Model, SBM.Core.Prestressed.PrestressedBeam Beam);
public sealed record PsStrandPick(SBM.Core.Prestressed.PrestressedModel Model, SBM.Core.Prestressed.PrestressedBeam Beam, SBM.Core.Prestressed.StrandInstance Strand);
public sealed record PsPadPick(SBM.Core.Prestressed.PrestressedPad Pad);
public sealed record NamedPick(string Kind, string Name, IReadOnlyList<(string, string)> Properties);

/// <summary>GPU-ready geometry for one drawing layer (one draw call for lines, one for text).</summary>
public sealed class LayerGeometry
{
    public required LayerStyle Style { get; init; }
    public LineGeometry3D? Lines { get; init; }
    public BillboardText3D? Text { get; init; }
    public int SegmentCount { get; init; }
}

/// <summary>One shaded solid layer: a merged flat-shaded mesh, optionally drawn many times via GPU instancing.</summary>
public sealed class SolidLayer
{
    public required string Name { get; init; }
    public required byte R { get; init; }
    public required byte G { get; init; }
    public required byte B { get; init; }
    public required List<(MeshGeometry3D mesh, List<Matrix4x4>? instances)> Parts { get; init; }
    public int Triangles { get; init; }
    public float Opacity { get; init; } = 1f;
}

public sealed class ReferenceRender
{
    public required SBM.Core.References.ReferenceModelDefinition Definition { get; init; }
    public MeshGeometry3D? Mesh { get; init; }
    public LineGeometry3D? Lines { get; init; }
    public LineGeometry3D? MinorContours { get; init; }
    public LineGeometry3D? MajorContours { get; init; }
    /// <summary>Draped image (with texture coordinates on <see cref="Mesh"/>), or null.</summary>
    public string? ImageFile { get; init; }
    public string ImageNote { get; init; } = "";
    public int Triangles { get; init; }
    public string LayerName => "REF-" + Definition.Name.ToUpperInvariant();
    public string ContourLayerName => LayerName + "-CONTOURS";
}

public sealed class SceneData
{
    public required List<LayerGeometry> Layers { get; init; }
    public List<SolidLayer> Solids { get; } = new();
    /// <summary>World coordinates subtracted from every vertex so single-precision GPU math stays accurate.</summary>
    public required Vec3 Origin { get; init; }
    public required Box2 Extents { get; init; }
    /// <summary>Region to frame on zoom-extents (the bridges when present, otherwise everything).</summary>
    public Box2 FocusExtents { get; set; }
}

/// <summary>
/// Converts renderer-neutral <see cref="PlanDrawing"/> linework into batched GPU buffers. Runs off the UI thread;
/// layers are built in parallel. Coordinates are rebased to a local origin (state-plane coordinates exceed
/// float precision otherwise).
/// </summary>
public static class SceneBuilder
{
    public static SceneData Build(PlanDrawing drawing, Vec3? origin = null)
    {
        var ext = drawing.Extents();
        var o = origin ?? (ext.IsEmpty ? Vec3.Zero : new Vec3(Math.Round(ext.Center.X / 100) * 100, Math.Round(ext.Center.Y / 100) * 100, 0));
        double zRef = 0;
        int zCount = 0;
        foreach (var l in drawing.Layers)
            foreach (var p in l.Polylines)
                if (p.Points.Length > 0) { zRef += p.Points[0].Z; zCount++; }
        if (origin == null && zCount > 0) o = o with { Z = Math.Round(zRef / zCount) };

        var layers = drawing.Layers.ToArray();
        var result = new LayerGeometry[layers.Length];
        Parallel.For(0, layers.Length, i => result[i] = BuildLayer(layers[i], o));
        var focus = Box2.Empty;
        foreach (var l in drawing.Layers.Where(l => l.Style == Layers.Deck || l.Style == Layers.GirderFlange))
            foreach (var p in l.Polylines)
                foreach (var v in p.Points) focus.Add(v.XY);
        return new SceneData { Layers = result.ToList(), Origin = o, Extents = ext, FocusExtents = focus.IsEmpty ? ext : focus };
    }

    public static readonly (string name, byte r, byte g, byte b, PartCategory[] cats)[] SolidLayerDefs =
    {
        ("3D-GIRDER-PLATES", 70, 130, 200, new[] { PartCategory.FlangePlate, PartCategory.WebPlate }),
        ("3D-STIFFENERS", 200, 110, 210, new[] { PartCategory.BearingStiffener, PartCategory.ConnectionStiffener, PartCategory.IntermediateStiffener }),
        ("3D-CROSSFRAME-MEMBERS", 80, 200, 110, new[] { PartCategory.Angle, PartCategory.Channel }),
        ("3D-GUSSETS", 235, 185, 70, new[] { PartCategory.Gusset, PartCategory.FillerPlate }),
        ("3D-SPLICE-PLATES", 120, 210, 230, new[] { PartCategory.SplicePlate }),
        ("3D-BEARINGS", 150, 150, 155, new[] { PartCategory.SolePlate, PartCategory.BearingPlate, PartCategory.MasonryPlate, PartCategory.GuideBar, PartCategory.ShearPin }),
        ("3D-BEARING-ELASTOMER", 40, 40, 45, new[] { PartCategory.BearingPad, PartCategory.BearingDisc, PartCategory.Ptfe }),
        ("3D-WELDS", 240, 80, 60, new[] { PartCategory.Weld }),
    };

    /// <summary>Detail layers are split into square tiles (m) so the viewport can draw only tiles near the view.</summary>
    public const double TileSize = 40;
    private static (long, long) Tile(Vec3 p) => ((long)Math.Floor(p.X / TileSize), (long)Math.Floor(p.Y / TileSize));
    private static Vec3 Center(Mesh m) { var b = m.Bounds(); return (b.Min + b.Max) * 0.5; }

    public const string BoltLayer = "3D-BOLTS";
    public const string StudLayer = "3D-STUDS";
    public const string AnchorLayer = "3D-ANCHOR-BOLTS";

    /// <summary>Adds shaded solids for steel models (built in parallel per layer). Bolts become GPU instances of one mesh per length.</summary>
    public static void AddSolids(SceneData scene, IReadOnlyList<SteelModel> models)
    {
        var o = scene.Origin;
        var layers = new SolidLayer?[SolidLayerDefs.Length + 3];
        Parallel.For(0, layers.Length, i =>
        {
            if (i < SolidLayerDefs.Length)
            {
                var (name, r, g, b, cats) = SolidLayerDefs[i];
                var parts = models.SelectMany(m => m.Parts).Where(p => cats.Contains(p.Category)).ToList();
                if (parts.Count == 0) return;
                if (name == "3D-WELDS")
                {
                    // Detail layer: one mesh per tile.
                    var tiles = parts.GroupBy(p => Tile(Center(p.Mesh))).Select(gr => FlatShaded(gr.Select(p => p.Mesh), o, gr.ToList())).ToList();
                    layers[i] = new SolidLayer { Name = name, R = r, G = g, B = b, Parts = tiles.Select(m2 => (m2, (List<Matrix4x4>?)null)).ToList(),
                        Triangles = tiles.Sum(m2 => m2.Indices!.Count / 3) };
                    return;
                }
                var mesh = FlatShaded(parts.Select(p => p.Mesh), o, parts);
                layers[i] = new SolidLayer { Name = name, R = r, G = g, B = b, Parts = new() { (mesh, null) }, Triangles = mesh.Indices!.Count / 3 };
                return;
            }
            // Fasteners: one mesh per distinct size, drawn with GPU instancing.
            int kind = i - SolidLayerDefs.Length;
            var list = new List<(MeshGeometry3D, List<Matrix4x4>?)>();
            int tris = 0;
            foreach (var m in models)
            {
                    IEnumerable<(Mesh mesh, List<Matrix4x4> inst, List<object> tags)> groups = kind switch
                {
                    0 => m.Bolts.Where(b => b.Kind == BoltKind.Structural).GroupBy(b => (Math.Round(b.Grip, 5), Math.Round(b.Length, 5), Tile(b.Frame.Origin)))
                        .Select(gr => (SteelModel.BoltMesh(m.Bolt, gr.First().Grip, gr.First().Length), gr.Select(b => ToMatrix(b.Frame, o)).ToList(), gr.Cast<object>().ToList())),
                    1 => m.Studs.GroupBy(s => (Math.Round(s.Length, 5), Tile(s.Frame.Origin)))
                        .Select(gr => (m.StudMesh(gr.First().Length, 10), gr.Select(s => ToMatrix(s.Frame, o)).ToList(), gr.Cast<object>().ToList())),
                    _ => m.Bolts.Where(b => b.Kind == BoltKind.Anchor).GroupBy(b => (Math.Round(b.Diameter, 5), Math.Round(b.Grip, 5), Math.Round(b.Embedment, 5), Tile(b.Frame.Origin)))
                        .Select(gr => (SteelModel.AnchorBoltMesh(gr.First().Diameter, gr.First().Grip, gr.First().Embedment), gr.Select(b => ToMatrix(b.Frame, o)).ToList(), gr.Cast<object>().ToList())),
                };
                foreach (var (mesh0, inst, itags) in groups)
                {
                    var mesh = FlatShaded(new[] { mesh0 }, Vec3.Zero);
                    InstanceTags.AddOrUpdate(mesh, itags);
                    tris += mesh.Indices!.Count / 3 * inst.Count;
                    list.Add((mesh, inst));
                }
            }
            if (list.Count == 0) return;
            layers[i] = kind switch
            {
                0 => new SolidLayer { Name = BoltLayer, R = 220, G = 220, B = 225, Parts = list, Triangles = tris },
                1 => new SolidLayer { Name = StudLayer, R = 200, G = 200, B = 160, Parts = list, Triangles = tris },
                _ => new SolidLayer { Name = AnchorLayer, R = 180, G = 160, B = 120, Parts = list, Triangles = tris },
            };
        });
        scene.Solids.AddRange(layers.OfType<SolidLayer>());
    }

    /// <summary>Concrete (deck + haunches, barriers) as see-through solids and reinforcement as tubes; identical shaped bars are instanced.</summary>
    public static void AddConcrete(SceneData scene, IReadOnlyList<SBM.Core.Concrete.ConcreteModel> models)
    {
        var o = scene.Origin;
        var jobs = new Func<SolidLayer?>[]
        {
            () =>
            {
                var items = models.SelectMany(m => new (Mesh?, object)[]
                {
                    (m.Deck, new NamedPick("Deck", "Deck slab", new[] { ("Volume", $"{m.DeckVolume / 0.764554857984:N1} CY") })),
                    (m.Haunches, new NamedPick("Deck", "Haunches", new[] { ("Volume", $"{m.HaunchVolume / 0.764554857984:N1} CY") })),
                }).Where(x => x.Item1 is { IsEmpty: false }).ToList();
                if (items.Count == 0) return null;
                var g = FlatShaded(items.Select(x => x.Item1!), o, items.Select(x => x.Item2).ToList());
                return new SolidLayer { Name = "3D-DECK", R = 180, G = 180, B = 172, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3, Opacity = 0.35f };
            },
            () =>
            {
                var items = models.SelectMany(m => m.Barriers).Where(b => !b.Mesh.IsEmpty).ToList();
                if (items.Count == 0) return null;
                var g = FlatShaded(items.Select(b => b.Mesh), o,
                    items.Select(b => (object)new NamedPick("Barrier", b.Name, new[] { ("Volume", $"{Math.Abs(b.Mesh.Volume()) / 0.764554857984:N1} CY") })).ToList());
                return new SolidLayer { Name = "3D-BARRIER", R = 190, G = 190, B = 182, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3, Opacity = 0.5f };
            },
            () => RebarLayer(models, o, "Deck", "3D-REBAR-DECK"),
            () => RebarLayer(models, o, "Rail", "3D-REBAR-RAIL"),
        };
        var results = new SolidLayer?[jobs.Length];
        Parallel.For(0, jobs.Length, i => results[i] = jobs[i]());
        scene.Solids.AddRange(results.OfType<SolidLayer>());
    }

    /// <summary>TxDOT prestressed girders (see-through concrete so the strands show), strands as tubes, elastomeric pads.</summary>
    public static void AddPrestressed(SceneData scene, IReadOnlyList<SBM.Core.Prestressed.PrestressedModel> models)
    {
        var o = scene.Origin;
        var jobs = new Func<SolidLayer?>[]
        {
            () =>
            {
                var sel = models.SelectMany(m => m.Beams.Select(b => (m, b))).ToList();
                if (sel.Count == 0) return null;
                var g = FlatShaded(sel.Select(x => x.b.Mesh), o, sel.Select(x => (object)new PsBeamPick(x.m, x.b)).ToList());
                return new SolidLayer { Name = "3D-PS-GIRDERS", R = 205, G = 200, B = 186, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3, Opacity = 0.55f };
            },
            () =>
            {
                var tubes = new List<Mesh>();
                var tags = new List<object>();
                foreach (var m in models)
                    foreach (var b in m.Beams)
                        foreach (var s in b.Strands)
                        {
                            var t = new Mesh();
                            AddTube(t, s.Points, b.StrandDiameter / 2);
                            tubes.Add(t);
                            tags.Add(new PsStrandPick(m, b, s));
                        }
                if (tubes.Count == 0) return null;
                var g = FlatShaded(tubes, o, tags);
                return new SolidLayer { Name = "3D-PS-STRANDS", R = 70, G = 110, B = 190, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3 };
            },
            () =>
            {
                var pads = models.SelectMany(m => m.Pads).ToList();
                if (pads.Count == 0) return null;
                var g = FlatShaded(pads.Select(p => p.Mesh), o, pads.Select(p => (object)new PsPadPick(p)).ToList());
                return new SolidLayer { Name = "3D-BEARING-ELASTOMER", R = 40, G = 40, B = 45, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3 };
            },
        };
        var results = new SolidLayer?[jobs.Length];
        Parallel.For(0, jobs.Length, i => results[i] = jobs[i]());
        scene.Solids.AddRange(results.OfType<SolidLayer>());
    }

    /// <summary>Caps, pedestals, backwalls, columns, foundations (concrete), steel piles, and substructure rebar.</summary>
    public static void AddSubstructure(SceneData scene, IReadOnlyList<SBM.Core.Substructure.SubstructureModel> models)
    {
        var o = scene.Origin;
        var solids = models.SelectMany(m => m.Units).SelectMany(u => u.Solids.Select(s => (u, s))).ToList();
        var drainUnit = new SBM.Core.Substructure.SubstructureUnit { SupportIndex = -1, Name = "Deck drainage", Type = SBM.Core.Bridge.SupportType.Pier,
            Definition = new SBM.Core.Bridge.SubstructureDefinition(), Origin = default, D = new Vec2(1, 0), N = new Vec2(0, 1), Station = 0 };
        solids.AddRange(models.SelectMany(m => m.Drainage).Select(s => (drainUnit, s)));
        SolidLayer? Layer(string name, byte r, byte g, byte b, Func<SBM.Core.Substructure.SubSolid, bool> pick, float opacity = 1f)
        {
            var sel = solids.Where(x => pick(x.s) && !x.s.Mesh.IsEmpty).ToList();
            if (sel.Count == 0) return null;
            var geo = FlatShaded(sel.Select(x => x.s.Mesh), o, sel.Select(x => (object)new SubPick(x.u, x.s)).ToList());
            return new SolidLayer { Name = name, R = r, G = g, B = b, Parts = new() { (geo, null) }, Triangles = geo.Indices!.Count / 3, Opacity = opacity };
        }
        var jobs = new Func<SolidLayer?>[]
        {
            () => Layer("3D-SUB-CAP", 196, 190, 176, x => x.Kind is SBM.Core.Substructure.SubElementKind.Cap or SBM.Core.Substructure.SubElementKind.Backwall, 0.6f),
            () => Layer("3D-SUB-PEDESTAL", 214, 196, 160, x => x.Kind == SBM.Core.Substructure.SubElementKind.Pedestal),
            () => Layer("3D-SUB-COLUMN", 186, 182, 170, x => x.Kind == SBM.Core.Substructure.SubElementKind.Column, 0.6f),
            () => Layer("3D-SUB-FOUNDATION", 160, 150, 135, x => x.Kind is SBM.Core.Substructure.SubElementKind.DrilledShaft or SBM.Core.Substructure.SubElementKind.Footing
                || (x.Kind == SBM.Core.Substructure.SubElementKind.Pile && x.IsConcrete), 0.6f),
            () => Layer("3D-SUB-PILE", 120, 130, 150, x => x.Kind == SBM.Core.Substructure.SubElementKind.Pile && !x.IsConcrete),
            () => Layer("3D-DRAINAGE", 90, 170, 200, x => x.Kind is SBM.Core.Substructure.SubElementKind.DeckDrain or SBM.Core.Substructure.SubElementKind.DrainPipe),
            () => Layer("3D-SPLASH-BASIN", 140, 120, 90, x => x.Kind == SBM.Core.Substructure.SubElementKind.SplashBasin),
            () =>
            {
                var byTile = new Dictionary<(long, long), (List<Mesh> tubes, List<object> tags)>();
                foreach (var m in models)
                {
                    var marks = m.Marks.ToDictionary(k => (k.Unit, k.Mark));
                    foreach (var b in m.Bars)
                    {
                        var mk = marks[(b.Unit, b.Mark)];
                        var t = new Mesh();
                        AddTube(t, b.Points, mk.Size.Diameter / 2);
                        var key = Tile(b.Points[0]);
                        if (!byTile.TryGetValue(key, out var e)) byTile[key] = e = (new(), new());
                        e.tubes.Add(t);
                        e.tags.Add(new RebarPick(b, mk));
                    }
                }
                if (byTile.Count == 0) return null;
                var gs = byTile.Values.Select(e => FlatShaded(e.tubes, o, e.tags)).ToList();
                return new SolidLayer { Name = "3D-REBAR-SUB", R = 150, G = 80, B = 50, Parts = gs.Select(g => (g, (List<Matrix4x4>?)null)).ToList(),
                    Triangles = gs.Sum(g => g.Indices!.Count / 3) };
            },
        };
        var results = new SolidLayer?[jobs.Length];
        Parallel.For(0, jobs.Length, i => results[i] = jobs[i]());
        scene.Solids.AddRange(results.OfType<SolidLayer>());
    }

    private static SolidLayer? RebarLayer(IReadOnlyList<SBM.Core.Concrete.ConcreteModel> models, Vec3 o, string group, string name)
    {
        var parts = new List<(MeshGeometry3D, List<Matrix4x4>?)>();
        int tris = 0;
        var byTile = new Dictionary<(long, long), (List<Mesh> tubes, List<object> tags)>();
        foreach (var m in models)
        {
            var marks = m.Marks.ToDictionary(k => k.Mark);
            var bars = m.Bars.Where(b => b.Group == group).ToList();
            // Shaped bars with a frame: one tube mesh per mark in local coordinates, instanced.
            foreach (var g in bars.Where(b => b.Frame != null).GroupBy(b => (b.Mark, Tile(b.Frame!.Value.Origin))))
            {
                var first = g.First();
                var f = first.Frame!.Value;
                var local = first.Points.Select(p => { var d = p - f.Origin; return new Vec3(Vec3.Dot(d, f.X), Vec3.Dot(d, f.Y), Vec3.Dot(d, f.Z)); }).ToArray();
                var tube = new Mesh();
                AddTube(tube, local, marks[g.Key.Mark].Size.Diameter / 2);
                var mesh = FlatShaded(new[] { tube }, Vec3.Zero);
                var inst = g.Select(b => ToMatrix(b.Frame!.Value, o)).ToList();
                InstanceTags.AddOrUpdate(mesh, g.Select(b => (object)new RebarPick(b, marks[b.Mark])).ToList());
                tris += mesh.Indices!.Count / 3 * inst.Count;
                parts.Add((mesh, inst));
            }
            foreach (var b in bars.Where(b => b.Frame == null))
            {
                var t = new Mesh();
                AddTube(t, b.Points, marks[b.Mark].Size.Diameter / 2);
                var key = Tile(b.Points[b.Points.Length / 2]);
                if (!byTile.TryGetValue(key, out var e)) byTile[key] = e = (new(), new());
                e.tubes.Add(t);
                e.tags.Add(new RebarPick(b, marks[b.Mark]));
            }
        }
        foreach (var e in byTile.Values)
        {
            var g = FlatShaded(e.tubes, o, e.tags);
            tris += g.Indices!.Count / 3;
            parts.Add((g, null));
        }
        if (parts.Count == 0) return null;
        bool epoxy = models.SelectMany(m => m.Marks).Any(mk => mk.Epoxy);
        return new SolidLayer { Name = name, R = (byte)(epoxy ? 70 : 150), G = (byte)(epoxy ? 170 : 80), B = (byte)(epoxy ? 80 : 50), Parts = parts, Triangles = tris };
    }

    /// <summary>Hexagonal tube along a polyline (one prism per segment; joints overlap slightly, which is invisible at bar scale).</summary>
    private static void AddTube(Mesh mesh, IReadOnlyList<Vec3> pts, double r, int sides = 6)
    {
        var sec = Enumerable.Range(0, sides).Select(i => Vec2.FromAngle(2 * Math.PI * i / sides) * r).ToArray();
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            var axis = b - a;
            if (axis.Length < 1e-6) continue;
            var x = Vec3.Cross(Math.Abs(axis.Normalized().Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0), axis).Normalized();
            var y = Vec3.Cross(axis.Normalized(), x);
            Vec3[] Ring(Vec3 c) => sec.Select(q => c + x * q.X + y * q.Y).ToArray();
            MeshBuilder.AddLoft(mesh, sec, new[] { Ring(a), Ring(b) });
        }
    }

    private static Matrix4x4 ToMatrix(Frame3 f, Vec3 o) => new(
        (float)f.X.X, (float)f.X.Y, (float)f.X.Z, 0,
        (float)f.Y.X, (float)f.Y.Y, (float)f.Y.Z, 0,
        (float)f.Z.X, (float)f.Z.Y, (float)f.Z.Z, 0,
        (float)(f.Origin.X - o.X), (float)(f.Origin.Y - o.Y), (float)(f.Origin.Z - o.Z), 1);

    /// <summary>
    /// GPU buffers for a reference model: smooth-shaded indexed mesh (shared vertices, area-weighted normals — compact
    /// for large TINs) and batched lines. Contours are generated for terrain in parallel.
    /// </summary>
    public static ReferenceRender BuildReference(SBM.Core.References.ReferenceModelDefinition def, SBM.Core.References.ReferenceGeometry geo,
        SBM.Core.References.TerrainModel? terrain, Vec3 o, double contourInterval)
    {
        MeshGeometry3D? mesh = null;
        string? imageFile = null;
        string imageNote = "";
        var m = geo.Mesh;
        if (!m.IsEmpty)
        {
            var pos = new Vector3Collection(m.Positions.Count);
            foreach (var p in m.Positions) pos.Add(V(p, o));
            var nrm = new Vector3[pos.Count];
            var idx = new IntCollection(m.Indices.Count);
            for (int i = 0; i < m.Indices.Count; i += 3)
            {
                int a = m.Indices[i], b = m.Indices[i + 1], c = m.Indices[i + 2];
                var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
                if (n.Z < 0 && terrain != null) { n = -n; (b, c) = (c, b); } // terrain faces up
                nrm[a] += n; nrm[b] += n; nrm[c] += n;
                idx.Add(a); idx.Add(b); idx.Add(c);
            }
            var nc = new Vector3Collection(nrm.Length);
            foreach (var n in nrm) nc.Add(n.LengthSquared() > 0 ? Vector3.Normalize(n) : Vector3.UnitZ);
            mesh = new MeshGeometry3D { Positions = pos, Indices = idx, Normals = nc };
            // Draped image: texture coordinates from the image's world file (project coordinates).
            if (!string.IsNullOrWhiteSpace(def.ImagePath) && File.Exists(imageFile = def.ImagePath) && WorldFile(def.ImagePath) is var wfo && wfo != null)
            {
                var wf = wfo.Value;
                var (w, h) = ImageSize(def.ImagePath);
                double left = wf.c - wf.a / 2, top = wf.f - wf.e / 2;
                var tc = new Vector2Collection(m.Positions.Count);
                foreach (var p in m.Positions) tc.Add(new Vector2((float)((p.X - left) / (wf.a * w)), (float)((p.Y - top) / (wf.e * h))));
                mesh.TextureCoordinates = tc;
                imageNote = $"image {w} × {h} px at {wf.a:0.###} m/px";
            }
            else imageFile = null;
            mesh.UpdateBounds();
        }

        LineGeometry3D? Lines(IEnumerable<(Vec3 a, Vec3 b)> segs)
        {
            var pos = new Vector3Collection();
            var idx = new IntCollection();
            foreach (var (a, b) in segs)
            {
                idx.Add(pos.Count); pos.Add(V(a, o));
                idx.Add(pos.Count); pos.Add(V(b, o));
            }
            if (pos.Count == 0) return null;
            var g = new LineGeometry3D { Positions = pos, Indices = idx };
            g.UpdateBounds();
            return g;
        }

        var lines = Lines(geo.Polylines.SelectMany(pl => pl.Zip(pl.Skip(1))));
        LineGeometry3D? minor = null, major = null;
        if (terrain != null && contourInterval > 0)
        {
            var (mi, ma) = terrain.Contours(contourInterval);
            minor = Lines(mi);
            major = Lines(ma);
        }
        return new ReferenceRender { Definition = def, Mesh = mesh, Lines = lines, MinorContours = minor, MajorContours = major,
            Triangles = m.TriangleCount, ImageFile = imageFile, ImageNote = imageNote };
    }

    /// <summary>World file beside an image (.jgw/.pgw/.tfw/.wld or ext + "w"): pixel size x (a), y (e, negative), center of the UL pixel (c, f).</summary>
    public static (double a, double e, double c, double f)? WorldFile(string image)
    {
        string ext = Path.GetExtension(image);
        var candidates = new[]
        {
            Path.ChangeExtension(image, ext.Length >= 3 ? $"{ext[..2]}{ext[^1]}w" : ".wld"), Path.ChangeExtension(image, ext + "w"), Path.ChangeExtension(image, ".wld"),
        };
        foreach (var c in candidates)
        {
            if (!File.Exists(c)) continue;
            var v = File.ReadAllLines(c).Where(l => l.Trim().Length > 0)
                .Select(l => double.Parse(l.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (v.Length >= 6) return (v[0], v[3], v[4], v[5]);
        }
        return null;
    }

    private static (int w, int h) ImageSize(string path)
    {
        using var fs = File.OpenRead(path);
        var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(fs, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
            System.Windows.Media.Imaging.BitmapCacheOption.None);
        return (dec.Frames[0].PixelWidth, dec.Frames[0].PixelHeight);
    }

    /// <summary>Unindexed (per-face vertices) mesh with face normals, for crisp CAD-style shading.</summary>
    /// <summary>Pick tables for merged meshes and per-instance tags for instanced meshes (same order as the matrices).</summary>
    public static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MeshGeometry3D, PickTable> Picks = new();
    public static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MeshGeometry3D, List<object>> InstanceTags = new();

    private static MeshGeometry3D FlatShaded(IEnumerable<Mesh> meshes, Vec3 o, IReadOnlyList<object>? tags)
    {
        var list = meshes.ToList();
        var g = FlatShaded(list, o);
        if (tags != null)
        {
            var table = new PickTable();
            int t = 0;
            for (int i = 0; i < list.Count; i++)
            {
                table.TriStart.Add(t);
                table.Tags.Add(tags[i]);
                t += list[i].TriangleCount;
            }
            Picks.AddOrUpdate(g, table);
        }
        return g;
    }

    private static MeshGeometry3D FlatShaded(IEnumerable<Mesh> meshes, Vec3 o)
    {
        int tri = 0;
        var list = meshes.ToList();
        foreach (var m in list) tri += m.TriangleCount;
        var pos = new Vector3Collection(tri * 3);
        var nrm = new Vector3Collection(tri * 3);
        var idx = new IntCollection(tri * 3);
        foreach (var m in list)
        {
            for (int i = 0; i < m.Indices.Count; i += 3)
            {
                var a = V(m.Positions[m.Indices[i]], o);
                var b = V(m.Positions[m.Indices[i + 1]], o);
                var c = V(m.Positions[m.Indices[i + 2]], o);
                var n = Vector3.Cross(b - a, c - a);
                n = n.LengthSquared() > 0 ? Vector3.Normalize(n) : Vector3.UnitZ;
                int k = pos.Count;
                pos.Add(a); pos.Add(b); pos.Add(c);
                nrm.Add(n); nrm.Add(n); nrm.Add(n);
                idx.Add(k); idx.Add(k + 1); idx.Add(k + 2);
            }
        }
        var g = new MeshGeometry3D { Positions = pos, Normals = nrm, Indices = idx };
        g.UpdateBounds();
        return g;
    }

    /// <summary>Pixel height of the default billboard font glyphs; text is scaled so it is model-sized like CAD text.</summary>
    private const float FontPixelHeight = 24f;

    private static Vector3 V(Vec3 p, Vec3 o) => new((float)(p.X - o.X), (float)(p.Y - o.Y), (float)(p.Z - o.Z));

    private static LayerGeometry BuildLayer(DrawingLayer layer, Vec3 o)
    {
        LineGeometry3D? lines = null;
        int segs = 0;
        if (layer.Polylines.Count > 0)
        {
            int nPts = 0, nIdx = 0;
            foreach (var pl in layer.Polylines)
            {
                if (pl.Points.Length < 2) continue;
                nPts += pl.Points.Length;
                nIdx += 2 * (pl.Points.Length - 1 + (pl.Closed ? 1 : 0));
            }
            var positions = new Vector3Collection(nPts);
            var indices = new IntCollection(nIdx);
            foreach (var pl in layer.Polylines)
            {
                if (pl.Points.Length < 2) continue;
                int start = positions.Count;
                foreach (var p in pl.Points) positions.Add(V(p, o));
                for (int k = 0; k < pl.Points.Length - 1; k++)
                {
                    indices.Add(start + k);
                    indices.Add(start + k + 1);
                }
                if (pl.Closed)
                {
                    indices.Add(start + pl.Points.Length - 1);
                    indices.Add(start);
                }
            }
            segs = indices.Count / 2;
            lines = new LineGeometry3D { Positions = positions, Indices = indices };
            lines.UpdateBounds();
        }

        BillboardText3D? text = null;
        if (layer.Texts.Count > 0)
        {
            text = new BillboardText3D();
            var color = new Color4(layer.Style.R / 255f, layer.Style.G / 255f, layer.Style.B / 255f, 1f);
            foreach (var t in layer.Texts)
            {
                text.TextInfo.Add(new TextInfo(t.Text, V(t.Position, o))
                {
                    Foreground = color,
                    Background = new Color4(0, 0, 0, 0),
                    Scale = (float)(t.Height / FontPixelHeight),
                });
            }
        }

        return new LayerGeometry { Style = layer.Style, Lines = lines, Text = text, SegmentCount = segs };
    }
}
