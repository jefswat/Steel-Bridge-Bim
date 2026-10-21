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

/// <summary>Pick context for reinforcing and substructure solids.</summary>
public sealed record RebarPick(SBM.Core.Concrete.BarPlacement Bar, SBM.Core.Concrete.BarMark Mark);
public sealed record SubPick(SBM.Core.Substructure.SubstructureUnit Unit, SBM.Core.Substructure.SubSolid Solid);
public sealed record PsBeamPick(SBM.Core.Prestressed.PrestressedModel Model, SBM.Core.Prestressed.PrestressedBeam Beam);
public sealed record PsStrandPick(SBM.Core.Prestressed.PrestressedModel Model, SBM.Core.Prestressed.PrestressedBeam Beam, SBM.Core.Prestressed.StrandInstance Strand);
public sealed record PsPadPick(SBM.Core.Prestressed.PrestressedPad Pad);
public sealed record NamedPick(string Kind, string Name, IReadOnlyList<(string, string)> Properties)
{
    /// <summary>The source mesh (exact measure snaps read its NURBS).</summary>
    public Mesh? Source { get; init; }
}

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
    public int Triangles { get; set; }
    public float Opacity { get; init; } = 1f;
    internal void AddTriangles(int n) => Triangles += n;
    /// <summary>Bridge index of each part (same order as <see cref="Parts"/>); null = not per bridge (-1). Used by the far-bridge LOD.</summary>
    public List<int>? PartBridges { get; set; }
    /// <summary>Per part: source triangles with bolt holes and without them (steel plates; null = no holes to drop).</summary>
    public List<(long withHoles, long withoutHoles)>? PartHoleTriangles { get; set; }
    public int BridgeOf(int part) => PartBridges is { } b && part < b.Count ? b[part] : -1;
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
    /// <summary>Image size (px) of <see cref="ImageFile"/>; the viewport may load it reduced (<see cref="ImageMaxPixels"/>).</summary>
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
    public int ImageMaxPixels { get; init; }
    /// <summary>Terrain raster behind the mesh (null for TINs / references) and the stride / clip the display mesh was made with.</summary>
    public SBM.Core.References.ElevationGrid? Grid { get; init; }
    public int Stride { get; init; } = 1;
    /// <summary>Stride of the mesh made at load (the full display mesh).</summary>
    public int LoadedStride { get; init; } = 1;
    /// <summary>Plan box the display mesh was clipped to (empty = not clipped).</summary>
    public Box2 ClipBox { get; init; } = Box2.Empty;
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
    /// <summary>Bridge names (index = <see cref="SolidLayer.PartBridges"/>).</summary>
    public List<string> BridgeNames { get; } = new();
    /// <summary>Detail layers (bolts, studs, welds, rebar) were not built (low memory).</summary>
    public bool DetailSkipped { get; set; }

    /// <summary>
    /// Adds the solid layers of one bridge (built into <paramref name="part"/>) to this scene: layers with the same name are merged part by
    /// part, each part remembering its bridge.
    /// </summary>
    public void MergeBridge(SceneData part, int bridge)
    {
        foreach (var l in part.Solids)
        {
            var mine = Solids.FirstOrDefault(x => x.Name == l.Name);
            if (mine == null)
            {
                mine = new SolidLayer { Name = l.Name, R = l.R, G = l.G, B = l.B, Opacity = l.Opacity, Parts = new(), PartBridges = new() };
                Solids.Add(mine);
            }
            mine.PartBridges ??= Enumerable.Repeat(-1, mine.Parts.Count).ToList();
            if (l.PartHoleTriangles != null || mine.PartHoleTriangles != null)
            {
                mine.PartHoleTriangles ??= Enumerable.Range(0, mine.Parts.Count).Select(k => ((long)(mine.Parts[k].mesh.Indices!.Count / 3), (long)(mine.Parts[k].mesh.Indices!.Count / 3))).ToList();
                mine.PartHoleTriangles.AddRange(l.PartHoleTriangles ?? l.Parts.Select(x => ((long)(x.mesh.Indices!.Count / 3), (long)(x.mesh.Indices!.Count / 3))).ToList());
            }
            mine.Parts.AddRange(l.Parts);
            mine.PartBridges.AddRange(Enumerable.Repeat(bridge, l.Parts.Count));
            mine.AddTriangles(l.Triangles);
        }
    }
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

    /// <summary>Steel solid layers (names, colors, categories) - shared with the 2D projector (<see cref="SBM.Core.Drawing.Projection.ModelLayers"/>).</summary>
    public static readonly (string name, byte r, byte g, byte b, PartCategory[] cats)[] SolidLayerDefs = SBM.Core.Drawing.Projection.ModelLayers.Steel;

    /// <summary>Detail layers are split into square tiles (m) so the viewport can draw only tiles near the view.</summary>
    public const double TileSize = 40;
    private static (long, long) Tile(Vec3 p) => ((long)Math.Floor(p.X / TileSize), (long)Math.Floor(p.Y / TileSize));
    private static Vec3 Center(Mesh m) { var b = m.Bounds(); return (b.Min + b.Max) * 0.5; }

    /// <summary>Opacity of the railing fence fabric layer (see-through, below the deck's 0.35).</summary>
    public const float FabricOpacity = 0.22f;
    public const string BoltLayer = "3D-BOLTS";
    public const string StudLayer = "3D-STUDS";
    public const string AnchorLayer = "3D-ANCHOR-BOLTS";
    /// <summary>Rivets (BoltKind.Rivet): detail layer like the bolts (tiled, instanced, near the view only, hidden while the camera moves).</summary>
    public const string RivetLayer = "3D-RIVETS";
    /// <summary>Flagged (UNRESOLVED) steel parts, e.g. tied arch details the plans do not dimension.</summary>
    public const string UnresolvedSteelLayer = "3D-UNRESOLVED-STEEL";

    /// <summary>Adds shaded solids for steel models (built in parallel per layer). Bolts become GPU instances of one mesh per length.</summary>
    public static void AddSolids(SceneData scene, IReadOnlyList<SteelModel> models)
    {
        var o = scene.Origin;
        var layers = new SolidLayer?[SolidLayerDefs.Length + 5];
        Parallel.For(0, layers.Length, i =>
        {
            if (i == SolidLayerDefs.Length + 3)
            {
                // Steel parts the plans may not have at all (SteelPart.Assumption, not DimensionOnly): their own orange layer, removed from the normal layers.
                // Parts the plans show with only a dimension open (Assumption.DimensionOnly) stay on their normal layer, flagged in the pick properties.
                var flagged = models.SelectMany(m => m.Parts).Where(p => p.Assumption is { DimensionOnly: false } && !p.Mesh.IsEmpty).ToList();
                if (flagged.Count == 0) return;
                var fm = FlatShaded(flagged.Select(p => p.Mesh), o, flagged);
                layers[i] = new SolidLayer { Name = UnresolvedSteelLayer, R = 255, G = 110, B = 40, Parts = new() { (fm, null) }, Triangles = fm.Indices!.Count / 3 };
                return;
            }
            if (i < SolidLayerDefs.Length)
            {
                var (name, r, g, b, cats) = SolidLayerDefs[i];
                var parts = models.SelectMany(m => m.Parts).Where(p => cats.Contains(p.Category) && (p.Assumption == null || p.Assumption.DimensionOnly)).ToList();
                if (parts.Count == 0) return;
                if (name == "3D-WELDS")
                {
                    if (SkipDetail) return;
                    // Detail layer: one mesh per tile.
                    var tiles = parts.GroupBy(p => Tile(Center(p.Mesh))).Select(gr => FlatShaded(gr.Select(p => p.Mesh), o, gr.ToList())).ToList();
                    layers[i] = new SolidLayer { Name = name, R = r, G = g, B = b, Parts = tiles.Select(m2 => (m2, (List<Matrix4x4>?)null)).ToList(),
                        Triangles = tiles.Sum(m2 => m2.Indices!.Count / 3) };
                    return;
                }
                bool noHoles = PlatesWithoutHoles;
                var mesh = FlatShaded(parts.Select(p => noHoles ? MeshLod.WithoutHoles(p.Mesh) : p.Mesh), o, parts);
                // Triangle counts with / without holes (the memory planner scales the buffer by them).
                long full = parts.Sum(p => (long)p.Mesh.TriangleCount), free = parts.Sum(p => (long)MeshLod.HoleFreeTriangles(p.Mesh));
                layers[i] = new SolidLayer { Name = name, R = r, G = g, B = b, Parts = new() { (mesh, null) }, Triangles = mesh.Indices!.Count / 3,
                    Opacity = name == "3D-PED-RAILING-FABRIC" ? FabricOpacity : 1f,
                    PartHoleTriangles = new() { (full, free) } };
                return;
            }
            // Fasteners: one mesh per distinct size, drawn with GPU instancing.
            if (SkipDetail) return;
            int kind = i == SolidLayerDefs.Length + 4 ? 3 : i - SolidLayerDefs.Length;
            var list = new List<(MeshGeometry3D, List<Matrix4x4>?)>();
            int tris = 0;
            foreach (var m in models)
            {
                    IEnumerable<(Mesh mesh, List<Matrix4x4> inst, List<object> tags)> groups = kind switch
                {
                    0 or 3 => m.Bolts.Where(b => kind == 3 ? b.Kind == BoltKind.Rivet : b.Kind == BoltKind.Structural).GroupBy(b => (b.Kind, Math.Round(b.Diameter > 0 ? b.Diameter : m.Bolt.Diameter, 5), Math.Round(b.Grip, 5), Math.Round(b.Length, 5), b.FlushEnd, Tile(b.Frame.Origin)))
                        .Select(gr => (gr.Key.Kind == BoltKind.Rivet ? SteelModel.RivetMesh(gr.First().Diameter, gr.First().Grip, flushEnd: gr.Key.FlushEnd)
                            : SteelModel.BoltMesh(gr.First().Diameter > 0 ? new BoltGeometry(gr.First().Diameter) : m.Bolt, gr.First().Grip, gr.First().Length), gr.Select(b => ToMatrix(b.Frame, o)).ToList(), gr.Cast<object>().ToList())),
                    1 => m.Studs.GroupBy(s => (Math.Round(s.Length, 5), Tile(s.Frame.Origin)))
                        .Select(gr => (m.StudMesh(gr.First().Length, 10), gr.Select(s => ToMatrix(s.Frame, o)).ToList(), gr.Cast<object>().ToList())),
                    _ => m.Bolts.Where(b => b.Kind == BoltKind.Anchor).GroupBy(b => (Math.Round(b.Diameter, 5), Math.Round(b.Grip, 5), Math.Round(b.Embedment, 5), Tile(b.Frame.Origin)))
                        .Select(gr => (SteelModel.AnchorBoltMesh(gr.First().Diameter, gr.First().Grip, gr.First().Embedment, 12, gr.First().TopHeight), gr.Select(b => ToMatrix(b.Frame, o)).ToList(), gr.Cast<object>().ToList())),
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
                3 => new SolidLayer { Name = RivetLayer, R = 205, G = 190, B = 175, Parts = list, Triangles = tris },
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
                    (m.Deck, new NamedPick("Deck", "Deck slab", new[] { ("Volume", $"{m.DeckVolume / 0.764554857984:N1} CY") }) { Source = m.Deck }),
                    (m.Haunches, new NamedPick("Deck", "Haunches", new[] { ("Volume", $"{m.HaunchVolume / 0.764554857984:N1} CY") }) { Source = m.Haunches }),
                }.Concat(m.DeckBumps.Select(db => ((Mesh?)db.Mesh, (object)new NamedPick("Deck", db.Name, new[] { ("Volume", $"{Math.Abs(db.Mesh.Volume()) / 0.764554857984:N2} CY") }))))
                 .Concat(m.CapHaunches.Select(ch => ((Mesh?)ch.Mesh, (object)new NamedPick("Deck", ch.Name, new[] { ("Volume", $"{ch.Volume / 0.764554857984:N2} CY"),
                     ("Width", $"{ch.Width / 0.0254:0.###} in"), ("Depth", $"{ch.MinDepth / 0.0254:0.##} .. {ch.MaxDepth / 0.0254:0.##} in"), ("Plans", ch.Source) }) { Source = ch.Mesh })))).Where(x => x.Item1 is { IsEmpty: false }).ToList();
                if (items.Count == 0) return null;
                var g = FlatShaded(items.Select(x => x.Item1!), o, items.Select(x => x.Item2).ToList());
                return new SolidLayer { Name = "3D-DECK", R = 180, G = 180, B = 172, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3, Opacity = 0.35f };
            },
            () =>
            {
                var items = models.SelectMany(m => m.Barriers).Where(b => !b.Mesh.IsEmpty).ToList();
                if (items.Count == 0) return null;
                var g = FlatShaded(items.Select(b => b.Mesh), o,
                    items.Select(b => (object)new NamedPick("Barrier", b.Name, new[] { ("Volume", $"{Math.Abs(b.Mesh.Volume()) / 0.764554857984:N1} CY") }) { Source = b.Mesh }).ToList());
                return new SolidLayer { Name = "3D-BARRIER", R = 190, G = 190, B = 182, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3, Opacity = 0.5f };
            },
            () =>
            {
                // Expansion joints: blockout concrete + generic device envelope (manufacturer-designed placeholder).
                var items = models.SelectMany(m => m.Joints).SelectMany(j => new (Mesh?, string, string)[]
                {
                    (j.Blockout, j.BlockoutName, $"{j.BlockoutVolume / 0.764554857984:N2} CY"),
                    (j.Body, j.DeviceName, $"{j.Definition.TypeName}, length {j.Length / 0.3048:N1} ft"),
                    (j.EndBlock?.Mesh, j.EndBlock?.Name ?? "", j.EndBlock == null ? "" : $"{j.EndBlock.Volume / 0.764554857984:N2} CY; girder ends embedded"),
                }).Where(x => x.Item1 is { IsEmpty: false }).ToList();
                if (items.Count == 0) return null;
                var g = FlatShaded(items.Select(x => x.Item1!), o,
                    items.Select(x => (object)new NamedPick("Expansion joint", x.Item2, new[] { ("Info", x.Item3) })).ToList());
                return new SolidLayer { Name = "3D-JOINTS", R = 230, G = 140, B = 40, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3, Opacity = 0.85f };
            },
            () => SegmentLayer(models, o, flagged: false),
            () => SegmentLayer(models, o, flagged: true),
            () =>
            {
                // Segmental box tendons (straight duct runs, equal-area bundle tubes).
                var tubes = new List<Mesh>();
                var tags = new List<object>();
                foreach (var sm in models.Select(m => m.Segmental).OfType<SBM.Core.Segmental.SegmentalModel>())
                    foreach (var t in sm.Tendons)
                    {
                        var tube = new Mesh();
                        AddTube(tube, t.Points.ToArray(), Math.Sqrt(t.Strands * sm.Definition.StrandArea / Math.PI));
                        tubes.Add(tube);
                        var props = new List<(string, string)> { ("Group", t.Group.ToString()), ("Strands", t.Strands.ToString()), ("Plan length", $"{t.PlanLength / 0.3048:N2} ft"),
                            ("Model length", $"{t.Length / 0.3048:N2} ft"), ("Jacking force", $"{t.JackingForceKip:N0} kip"), ("Stressing end", t.StressingEnd), ("Anchors", t.Anchors) };
                        if (t.Assumption != null) { props.Add(("Status", t.Assumption.Status)); props.Add(("Reason", t.Assumption.Reason)); props.Add(("Assumed", t.Assumption.Basis)); }
                        tags.Add(new NamedPick("Tendon", t.Name, props));
                    }
                if (tubes.Count == 0) return null;
                var g = FlatShaded(tubes, o, tags);
                return new SolidLayer { Name = "3D-PT-TENDONS", R = 70, G = 110, B = 190, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3 };
            },
            () =>
            {
                var items = models.Select(m => m.Segmental).OfType<SBM.Core.Segmental.SegmentalModel>().SelectMany(sm => sm.Bearings).Where(b => b.Mesh is { IsEmpty: false }).ToList();
                if (items.Count == 0) return null;
                var g = FlatShaded(items.Select(b => b.Mesh!), o, items.Select(b => (object)new NamedPick("Bearing", b.Name, new[] { ("Info", b.Description), ("Soffit EL", $"{b.SoffitZ / 0.3048:N3} ft") })).ToList());
                return new SolidLayer { Name = "3D-SEGMENTAL-BEARINGS", R = 140, G = 140, B = 150, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3 };
            },
            () => RebarLayer(models, o, "Deck", "3D-REBAR-DECK"),
            () => RebarLayer(models, o, "Rail", "3D-REBAR-RAIL"),
            () => RebarLayer(models, o, "CapHaunch", "3D-REBAR-CAP-HAUNCH"),
        };
        var results = new SolidLayer?[jobs.Length];
        Parallel.For(0, jobs.Length, i => results[i] = jobs[i]());
        scene.Solids.AddRange(results.OfType<SolidLayer>());
    }

    /// <summary>Precast segmental box segments (see-through so the tendons show); segments modeled on an UNRESOLVED item in their own orange layer.</summary>
    private static SolidLayer? SegmentLayer(IReadOnlyList<SBM.Core.Concrete.ConcreteModel> models, Vec3 o, bool flagged)
    {
        const double cy = 0.764554857984;
        var items = new List<(Mesh mesh, object tag)>();
        foreach (var sm in models.Select(m => m.Segmental).OfType<SBM.Core.Segmental.SegmentalModel>())
            foreach (var s in sm.Segments.Where(s => (s.Assumption != null) == flagged && !s.Mesh.IsEmpty))
            {
                var props = new List<(string, string)> { ("Type", s.Type.ToString()), ("Span", s.Span), ("Cantilever", s.Bent), ("Depth", $"{s.Depth0 / 0.3048:N3} / {s.Depth1 / 0.3048:N3} ft"),
                    ("Chord length", $"{s.ChordLength / 0.3048:N3} ft"), ("Volume", $"{s.Volume / cy:N2} CY"), ("Info", s.Description) };
                if (s.Assumption != null) { props.Add(("Status", s.Assumption.Status)); props.Add(("Reason", s.Assumption.Reason)); props.Add(("Assumed", s.Assumption.Basis)); }
                items.Add((s.Mesh, new NamedPick("Segment", s.Name, props)));
            }
        if (items.Count == 0) return null;
        var geo = FlatShaded(items.Select(x => x.mesh), o, items.Select(x => x.tag).ToList());
        return flagged
            ? new SolidLayer { Name = "3D-UNRESOLVED-SEGMENTS", R = 255, G = 110, B = 40, Parts = new() { (geo, null) }, Triangles = geo.Indices!.Count / 3, Opacity = 0.6f }
            : new SolidLayer { Name = "3D-SEGMENTS", R = 205, G = 200, B = 186, Parts = new() { (geo, null) }, Triangles = geo.Indices!.Count / 3, Opacity = 0.55f };
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
                            AddTube(t, s.Points, s.DiameterIn(b.StrandDiameter) / 2);
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
            () =>
            {
                // Tapered steel sole plates on the pads (optional, PrestressedBearingDefinition.SolePlate).
                var soles = models.SelectMany(m => m.Pads).Where(p => p.SolePlate != null).ToList();
                if (soles.Count == 0) return null;
                var g = FlatShaded(soles.Select(p => p.SolePlate!.Mesh), o, soles.Select(p => (object)new PsPadPick(p)).ToList());
                return new SolidLayer { Name = "3D-BEARING-SOLE-PLATES", R = 140, G = 145, B = 155, Parts = new() { (g, null) }, Triangles = g.Indices!.Count / 3 };
            },
            () =>
            {
                // Beam mild reinforcing (optional): tubes tiled like the substructure bars, RebarPick tags (near-view LOD picks them up).
                if (SkipDetail) return null;
                var byTile = new Dictionary<(long, long), (List<Mesh> tubes, List<object> tags)>();
                bool epoxy = false;
                foreach (var m in models)
                {
                    var marks = m.BarMarks.ToDictionary(k => (k.Unit, k.Mark));
                    foreach (var b in m.Beams.SelectMany(x => x.Bars))
                    {
                        var mk = marks[(b.Unit, b.Mark)];
                        epoxy |= mk.Epoxy;
                        var t = new Mesh();
                        AddTube(t, b.Points, mk.Size.Diameter / 2);
                        var key = Tile(b.Points[b.Points.Length / 2]);
                        if (!byTile.TryGetValue(key, out var e)) byTile[key] = e = (new(), new());
                        e.tubes.Add(t);
                        e.tags.Add(new RebarPick(b, mk));
                    }
                }
                if (byTile.Count == 0) return null;
                var gs = byTile.Values.Select(e => FlatShaded(e.tubes, o, e.tags)).ToList();
                return new SolidLayer { Name = "3D-REBAR-BEAM", R = (byte)(epoxy ? 70 : 150), G = (byte)(epoxy ? 170 : 80), B = (byte)(epoxy ? 80 : 50),
                    Parts = gs.Select(g => (g, (List<Matrix4x4>?)null)).ToList(), Triangles = gs.Sum(g => g.Indices!.Count / 3) };
            },
            () => DiaphragmLayer(models, o, "3D-DIAPHRAGMS", 188, 186, 176, flagged: false, accessories: false, 0.6f),
            () => DiaphragmLayer(models, o, "3D-DIAPHRAGM-FILLERS", 235, 232, 200, flagged: false, accessories: true, 1f),
            () => DiaphragmLayer(models, o, "3D-UNRESOLVED-DIAPHRAGMS", 255, 110, 40, flagged: true, accessories: null, 0.6f),
        };
        var results = new SolidLayer?[jobs.Length];
        Parallel.For(0, jobs.Length, i => results[i] = jobs[i]());
        scene.Solids.AddRange(results.OfType<SolidLayer>());
    }

    /// <summary>Concrete end / pier diaphragms and their fillers / keyways; elements modeled on an UNRESOLVED item go to their own orange layer.</summary>
    private static SolidLayer? DiaphragmLayer(IReadOnlyList<SBM.Core.Prestressed.PrestressedModel> models, Vec3 o, string name, byte r, byte g, byte b,
        bool flagged, bool? accessories, float opacity)
    {
        const double cy = 0.764554857984;
        var items = new List<(Mesh mesh, object tag)>();
        foreach (var cd in models.Select(m => m.ConcreteDiaphragms).OfType<SBM.Core.Diaphragms.ConcreteDiaphragmModel>())
        {
            if (accessories != true)
                foreach (var d in cd.Diaphragms.Where(d => (d.Assumption != null) == flagged && !d.Mesh.IsEmpty))
                {
                    var props = new List<(string, string)> { ("Type", d.ObjectType), ("Volume", $"{d.Volume / cy:N2} CY"), ("Info", d.Description) };
                    if (d.UnbuiltParts.Count > 0) props.Add(("Not built (UNRESOLVED)", string.Join(", ", d.UnbuiltParts)));
                    if (d.Assumption != null) { props.Add(("Status", d.Assumption.Status)); props.Add(("Reason", d.Assumption.Reason)); props.Add(("Assumed", d.Assumption.Basis)); }
                    items.Add((d.Mesh, new NamedPick("Diaphragm", d.Name, props)));
                }
            if (accessories != false)
                foreach (var a in cd.Accessories.Where(a => (a.Assumption != null) == flagged && !a.Mesh.IsEmpty))
                {
                    var props = new List<(string, string)> { ("Material", a.Material), ("Volume", $"{a.Volume / cy:N3} CY"), ("Info", a.Description) };
                    if (a.Assumption != null) { props.Add(("Status", a.Assumption.Status)); props.Add(("Reason", a.Assumption.Reason)); props.Add(("Assumed", a.Assumption.Basis)); }
                    items.Add((a.Mesh, new NamedPick(a.Kind.ToString(), a.Name, props)));
                }
        }
        if (items.Count == 0) return null;
        var geo = FlatShaded(items.Select(x => x.mesh), o, items.Select(x => x.tag).ToList());
        return new SolidLayer { Name = name, R = r, G = g, B = b, Parts = new() { (geo, null) }, Triangles = geo.Indices!.Count / 3, Opacity = opacity };
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
            () => Layer("3D-SUB-CAP", 196, 190, 176, x => x.Kind is SBM.Core.Substructure.SubElementKind.Cap or SBM.Core.Substructure.SubElementKind.Backwall or SBM.Core.Substructure.SubElementKind.Wingwall or SBM.Core.Substructure.SubElementKind.Counterfort or SBM.Core.Substructure.SubElementKind.Maskwall, 0.6f),
            () => Layer("3D-SUB-PEDESTAL", 214, 196, 160, x => x.Kind == SBM.Core.Substructure.SubElementKind.Pedestal),
            () => Layer("3D-SUB-COLUMN", 186, 182, 170, x => x.Kind == SBM.Core.Substructure.SubElementKind.Column, 0.6f),
            () => Layer("3D-SUB-FOUNDATION", 160, 150, 135, x => x.Kind is SBM.Core.Substructure.SubElementKind.DrilledShaft or SBM.Core.Substructure.SubElementKind.Footing or SBM.Core.Substructure.SubElementKind.WingFooting
                || (x.Kind == SBM.Core.Substructure.SubElementKind.Pile && x.IsConcrete), 0.6f),
            () => Layer("3D-SUB-PILE", 120, 130, 150, x => x.Kind == SBM.Core.Substructure.SubElementKind.Pile && !x.IsConcrete),
            () => Layer("3D-SUB-COUPLER", 214, 140, 60, x => x.Kind == SBM.Core.Substructure.SubElementKind.Coupler),
            () => Layer("3D-SUB-MOW-STRIP", 170, 165, 150, x => x.Kind == SBM.Core.Substructure.SubElementKind.MowStrip && x.Assumption == null),
            () => Layer("3D-SUB-TRAFFIC-BARRIER", 200, 196, 186, x => x.Kind is SBM.Core.Substructure.SubElementKind.TrafficBarrier or SBM.Core.Substructure.SubElementKind.PavementStrip && x.Assumption == null),
            () => Layer("3D-UNRESOLVED-SUB", 255, 110, 40, x => x.Kind is SBM.Core.Substructure.SubElementKind.MowStrip or SBM.Core.Substructure.SubElementKind.TrafficBarrier or SBM.Core.Substructure.SubElementKind.PavementStrip && x.Assumption != null, 0.8f),
            () => Layer("3D-DRAINAGE", 90, 170, 200, x => x.Kind is SBM.Core.Substructure.SubElementKind.DeckDrain or SBM.Core.Substructure.SubElementKind.DrainPipe),
            () => Layer("3D-SPLASH-BASIN", 140, 120, 90, x => x.Kind == SBM.Core.Substructure.SubElementKind.SplashBasin),
            () => Layer("3D-SUB-FENDER", 150, 115, 75, x => x.Kind == SBM.Core.Substructure.SubElementKind.Fender),
            // Optional misc items: Ramp G drainage, sign supports, end posts, lighting (items by others as translucent placeholders).
            () => MiscLayer(models, o, "3D-DRAIN-PIPES", 90, 170, 200, e => e.Assumption == null && e.Group == SBM.Core.Misc.MiscGroup.Drainage && e.Kind is SBM.Core.Misc.MiscKind.DrainTube or SBM.Core.Misc.MiscKind.DrainPipe or SBM.Core.Misc.MiscKind.PipeFitting or SBM.Core.Misc.MiscKind.PipeHanger or SBM.Core.Misc.MiscKind.PipeSupport),
            () => MiscLayer(models, o, "3D-DRAIN-INLETS", 60, 140, 190, e => e.Assumption == null && e.Kind is SBM.Core.Misc.MiscKind.DrainTrench or SBM.Core.Misc.MiscKind.DrainGrate or SBM.Core.Misc.MiscKind.DrainBox or SBM.Core.Misc.MiscKind.DrainTrough),
            () => MiscLayer(models, o, "3D-DRAIN-STONE", 140, 120, 90, e => e.Assumption == null && e.Kind is SBM.Core.Misc.MiscKind.SplashBasin or SBM.Core.Misc.MiscKind.ErosionMattress),
            () => MiscLayer(models, o, "3D-SIGN-SUPPORT", 150, 160, 190, e => e.Assumption == null && e.Group == SBM.Core.Misc.MiscGroup.SignSupport && !e.Placeholder),
            () => MiscLayer(models, o, "3D-CRASH-CUSHION", 210, 190, 60, e => e.Assumption == null && e.Group == SBM.Core.Misc.MiscGroup.CrashCushion && !e.Placeholder),
            () => MiscLayer(models, o, "3D-LIGHTING", 230, 215, 90, e => e.Assumption == null && e.Group == SBM.Core.Misc.MiscGroup.Lighting && !e.Placeholder),
            () => MiscLayer(models, o, "3D-UNRESOLVED", 255, 110, 40, e => e.Assumption != null),
            () => MiscLayer(models, o, "3D-PLACEHOLDER", 200, 80, 200, e => e.Placeholder && e.Assumption == null, 0.55f),
            // Architectural surface treatments: relief layers of the textured / thin-brick regions (flagged regions keep their layer; reason in the pick)
            () => SurfaceLayer(models, o, "3D-THIN-BRICK", 150, 70, 45, k => k == SBM.Core.Bridge.SurfaceTreatmentKind.ThinBrick),
            () => SurfaceLayer(models, o, "3D-FORM-LINER", 175, 150, 120, k => k != SBM.Core.Bridge.SurfaceTreatmentKind.ThinBrick),
            () =>
            {
                if (SkipDetail) return null;
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

    private static SolidLayer? SurfaceLayer(IReadOnlyList<SBM.Core.Substructure.SubstructureModel> models, Vec3 o, string name, byte r, byte g, byte b,
        Func<SBM.Core.Bridge.SurfaceTreatmentKind, bool> pick)
    {
        var sel = models.Where(m => m.SurfaceTreatments != null).SelectMany(m => m.SurfaceTreatments!.Items).Where(e => !e.Mesh.IsEmpty && pick(e.Treatment.Kind)).ToList();
        if (sel.Count == 0) return null;
        var geo = FlatShaded(sel.Select(e => e.Mesh), o, sel.Select(e => (object)new NamedPick("Surface treatment", e.Name,
            new[] { ("Info", e.Description), ("Area", $"{e.Area / 0.09290304:N1} SF"), ("Pattern", e.Treatment.Pattern), ("Orientation", e.Treatment.Orientation),
                    ("UNRESOLVED", e.Assumption?.Reason ?? ""), ("Assumed", e.Assumption?.Basis ?? "") }.Where(x => x.Item2 != "").ToList())).ToList());
        return new SolidLayer { Name = name, R = r, G = g, B = b, Parts = new() { (geo, null) }, Triangles = geo.Indices!.Count / 3 };
    }

    private static SolidLayer? MiscLayer(IReadOnlyList<SBM.Core.Substructure.SubstructureModel> models, Vec3 o, string name, byte r, byte g, byte b,
        Func<SBM.Core.Misc.MiscElement, bool> pick, float opacity = 1f)
    {
        var sel = models.Where(m => m.Misc != null).SelectMany(m => m.Misc!.Elements).Where(e => !e.Mesh.IsEmpty && pick(e)).ToList();
        if (sel.Count == 0) return null;
        var geo = FlatShaded(sel.Select(e => e.Mesh), o, sel.Select(e => (object)new NamedPick(e.Group.ToString(), e.Name,
            new[] { ("Info", e.Description), ("Placeholder", e.Placeholder ? "by others: " + e.PlaceholderBasis : ""), ("UNRESOLVED", e.Assumption?.Reason ?? ""), ("Assumed", e.Assumption?.Basis ?? "") }.Where(x => x.Item2 != "").ToList())).ToList());
        return new SolidLayer { Name = name, R = r, G = g, B = b, Parts = new() { (geo, null) }, Triangles = geo.Indices!.Count / 3, Opacity = opacity };
    }

    private static SolidLayer? RebarLayer(IReadOnlyList<SBM.Core.Concrete.ConcreteModel> models, Vec3 o, string group, string name)
    {
        if (SkipDetail) return null;
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

    /// <summary>Hexagonal tube along a polyline: one mitred loft per bar (no interior caps; see <see cref="TubeBuilder"/>).</summary>
    private static void AddTube(Mesh mesh, IReadOnlyList<Vec3> pts, double r, int sides = 6) => TubeBuilder.AddMitredTube(mesh, pts, r, sides);

    /// <summary>Steel plates are drawn without their bolt holes (viewport memory level; the bolts fill the holes).</summary>
    public static bool PlatesWithoutHoles;

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
        SBM.Core.References.TerrainModel? terrain, Vec3 o, double contourInterval, Mesh? display = null, int stride = 1, int loadedStride = 1, Box2? clip = null, int imageMaxPixels = 0)
    {
        MeshGeometry3D? mesh = null;
        string? imageFile = null;
        string imageNote = "";
        int imgW = 0, imgH = 0;
        var m = display ?? geo.Mesh;
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
                (imgW, imgH) = (w, h);
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
            var (mi, ma) = display == null ? terrain.Contours(contourInterval) : SBM.Core.References.TerrainModel.ContoursOf(display, contourInterval);
            minor = Lines(mi);
            major = Lines(ma);
        }
        return new ReferenceRender { Definition = def, Mesh = mesh, Lines = lines, MinorContours = minor, MajorContours = major,
            Triangles = m.TriangleCount, ImageFile = imageFile, ImageNote = imageNote, ImageWidth = imgW, ImageHeight = imgH, ImageMaxPixels = imageMaxPixels,
            Grid = geo.Grid, Stride = stride, LoadedStride = loadedStride, ClipBox = clip ?? Box2.Empty };
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

    /// <summary>Size of a georeferenced image (0, 0 when there is none or it cannot be read).</summary>
    public static (int w, int h) ImageSizeOrZero(string? path)
    {
        try { return string.IsNullOrWhiteSpace(path) || !File.Exists(path) || WorldFile(path) == null ? (0, 0) : ImageSize(path); }
        catch (Exception) { return (0, 0); }
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

    /// <summary>
    /// Flat-shaded meshes share a vertex between neighbouring triangles of one source mesh that lie in the same plane (the two
    /// triangles of a quad, fans of a cap): a GPU vertex is 52 bytes (HelixToolkit DefaultVertex), so a plate side drawn as a quad
    /// needs 4 vertices instead of 6. Looked up in a small window of the last emitted vertices (no hashing). Off = one vertex per
    /// triangle corner (the old layout; snapshot option --noshare to compare).
    /// </summary>
    public static bool ShareVertices = true;

    /// <summary>Detail layers (bolts, studs, anchor bolts, welds, rebar) are not built (set after an out-of-memory error).</summary>
    public static bool SkipDetail;

    private static MeshGeometry3D FlatShaded(IEnumerable<Mesh> meshes, Vec3 o)
    {
        int tri = 0;
        var list = meshes.ToList();
        foreach (var m in list) tri += m.TriangleCount;
        bool share = ShareVertices;
        var pos = new Vector3Collection(share ? tri * 2 + 16 : tri * 3);
        var nrm = new Vector3Collection(share ? tri * 2 + 16 : tri * 3);
        var idx = new IntCollection(tri * 3);
        const int W = 16;
        Span<int> ringSrc = stackalloc int[W];
        Span<int> ringOut = stackalloc int[W];
        foreach (var m in list)
        {
            int ringN = 0, ringAt = 0;
            for (int i = 0; i < m.Indices.Count; i += 3)
            {
                var a = V(m.Positions[m.Indices[i]], o);
                var b = V(m.Positions[m.Indices[i + 1]], o);
                var c = V(m.Positions[m.Indices[i + 2]], o);
                var n = Vector3.Cross(b - a, c - a);
                n = n.LengthSquared() > 0 ? Vector3.Normalize(n) : Vector3.UnitZ;
                if (!share)
                {
                    int k = pos.Count;
                    pos.Add(a); pos.Add(b); pos.Add(c);
                    nrm.Add(n); nrm.Add(n); nrm.Add(n);
                    idx.Add(k); idx.Add(k + 1); idx.Add(k + 2);
                    continue;
                }
                for (int corner = 0; corner < 3; corner++)
                {
                    int src = m.Indices[i + corner];
                    int found = -1;
                    for (int r = 0; r < ringN; r++)
                        if (ringSrc[r] == src && Vector3.Dot(nrm[ringOut[r]], n) > 0.999999f) { found = ringOut[r]; break; }
                    if (found < 0)
                    {
                        found = pos.Count;
                        pos.Add(corner == 0 ? a : corner == 1 ? b : c);
                        nrm.Add(n);
                        ringSrc[ringAt] = src;
                        ringOut[ringAt] = found;
                        ringAt = (ringAt + 1) % W;
                        if (ringN < W) ringN++;
                    }
                    idx.Add(found);
                }
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
