using HelixToolkit.SharpDX;
using SBM.Core.Geometry;

namespace SBM.App.Viewport;

/// <summary>GPU memory inventory of a built scene (see <see cref="VramInventory"/>): one item per model / buffer set.</summary>
public static class SceneInventory
{
    public static VramGroup GroupOf(string layer) => layer switch
    {
        "3D-DECK" => VramGroup.Deck,
        "3D-BARRIER" or "3D-JOINTS" => VramGroup.Concrete,
        _ when layer.StartsWith("3D-DIAPHRAGM", StringComparison.Ordinal) || layer == "3D-UNRESOLVED-DIAPHRAGMS" => VramGroup.Concrete,
        _ when layer.StartsWith("3D-SUB-", StringComparison.Ordinal) || layer is "3D-DRAINAGE" or "3D-SPLASH-BASIN" => VramGroup.Substructure,
        "3D-WELDS" => VramGroup.Welds,
        SceneBuilder.BoltLayer or SceneBuilder.RivetLayer => VramGroup.Bolts,
        SceneBuilder.StudLayer => VramGroup.Studs,
        SceneBuilder.AnchorLayer => VramGroup.AnchorBolts,
        _ when layer.StartsWith("3D-REBAR", StringComparison.Ordinal) => VramGroup.Rebar,
        _ when layer.StartsWith("3D-PS-", StringComparison.Ordinal) => VramGroup.Prestressed,
        _ when layer.StartsWith("3D-DRAIN-", StringComparison.Ordinal) || layer is "3D-SIGN-SUPPORT" or "3D-CRASH-CUSHION" or "3D-LIGHTING" or "3D-EXPANSION-JOINTS" or "3D-UNRESOLVED" or "3D-UNRESOLVED-STEEL" or "3D-PLACEHOLDER" => VramGroup.Misc,
        _ => VramGroup.Steel,
    };

    public static VramInventory Build(SceneData scene, IReadOnlyList<ReferenceRender> refs, Func<string, bool> isDetail)
    {
        var inv = new VramInventory { Bridges = scene.FocusExtents };
        inv.BridgeNames.AddRange(scene.BridgeNames);
        var o = scene.Origin;
        foreach (var l in scene.Layers)
        {
            if (l.Lines is { } lg)
                inv.Items.Add(new VramItem(l.Style.Name, VramGroup.Lines, lg.Positions?.Count ?? 0, lg.Indices?.Count ?? 0, VertexBytes: VramItem.LineVertexBytes));
            if (l.Text is { } tx)
                inv.Items.Add(new VramItem(l.Style.Name, VramGroup.Text, 0, 0, ExtraBytes: tx.TextInfo.Sum(t => (long)(t.Text?.Length ?? 0)) * VramItem.GlyphBytes));
        }
        foreach (var s in scene.Solids)
        {
            var group = GroupOf(s.Name);
            bool detail = isDetail(s.Name);
            for (int i = 0; i < s.Parts.Count; i++)
            {
                var (mesh, inst) = s.Parts[i];
                Vec2? center = null;
                if (inst is { Count: > 0 })
                {
                    double x = 0, y = 0;
                    foreach (var m in inst) { x += m.Translation.X; y += m.Translation.Y; }
                    center = new Vec2(x / inst.Count + o.X, y / inst.Count + o.Y);
                }
                else
                {
                    var b = mesh.Bound;
                    center = new Vec2((b.Minimum.X + b.Maximum.X) / 2 + o.X, (b.Minimum.Y + b.Maximum.Y) / 2 + o.Y);
                }
                var item = new VramItem(s.Name, group, mesh.Positions?.Count ?? 0, mesh.Indices?.Count ?? 0, inst?.Count ?? 0,
                    Bridge: s.BridgeOf(i), Detail: detail, Center: center);
                if (s.PartHoleTriangles is { } ht && i < ht.Count && ht[i].withHoles > 0)
                {
                    double built = SceneBuilder.PlatesWithoutHoles ? ht[i].withoutHoles : ht[i].withHoles;
                    item = item with { WithHolesFactor = ht[i].withHoles / Math.Max(1, built), WithoutHolesFactor = ht[i].withoutHoles / Math.Max(1, built) };
                }
                inv.Items.Add(item);
            }
        }
        foreach (var r in refs)
        {
            bool terrain = r.Definition.Kind == SBM.Core.References.ReferenceKind.Terrain;
            if (r.Mesh is { } m)
                inv.Items.Add(new VramItem(r.LayerName, terrain ? VramGroup.Terrain : VramGroup.Reference, m.Positions?.Count ?? 0, m.Indices?.Count ?? 0,
                    VertexBytes: VramItem.MeshVertexBytes + (m.TextureCoordinates != null ? VramItem.TexCoordBytes : 0)));
            if (r.ImageFile != null && r.ImageWidth > 0)
            {
                double f = r.ImageMaxPixels > 0 ? Math.Min(1.0, r.ImageMaxPixels / (double)Math.Max(r.ImageWidth, r.ImageHeight)) : 1;
                inv.Items.Add(new VramItem(r.LayerName, VramGroup.TerrainImage, 0, 0, ExtraBytes: VramItem.TextureBytes((long)(r.ImageWidth * f), (long)(r.ImageHeight * f))));
            }
            if (r.Lines is { } rl)
                inv.Items.Add(new VramItem(r.LayerName, VramGroup.Reference, rl.Positions?.Count ?? 0, rl.Indices?.Count ?? 0, VertexBytes: VramItem.LineVertexBytes));
            foreach (var c in new[] { r.MinorContours, r.MajorContours })
                if (c != null)
                    inv.Items.Add(new VramItem(r.ContourLayerName, VramGroup.Contours, c.Positions?.Count ?? 0, c.Indices?.Count ?? 0, VertexBytes: VramItem.LineVertexBytes));
            if (terrain && r.Grid is { } g)
            {
                var t = new VramTerrain(r.LayerName, g.Nx, g.Ny, g.X0, g.Y0, g.Cell, r.LoadedStride, r.ImageFile != null ? r.ImageWidth : 0, r.ImageFile != null ? r.ImageHeight : 0);
                // Contours were made from the current display mesh: scale back to the full (loaded) mesh.
                long contours = new[] { r.MinorContours, r.MajorContours }.Where(c => c != null)
                    .Sum(c => (long)(c!.Positions?.Count ?? 0) * VramItem.LineVertexBytes + (long)(c.Indices?.Count ?? 0) * VramItem.IndexBytes);
                double area = t.ClipFraction(r.ClipBox);
                inv.Terrains.Add(t with { ContourBytes = (long)(contours * (r.Stride / (double)Math.Max(1, r.LoadedStride)) / Math.Max(area, 1e-3)) });
            }
        }
        return inv;
    }
}
