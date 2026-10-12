namespace SBM.Core.Geometry;

/// <summary>What a GPU buffer holds, for the memory breakdown and the degradation order.</summary>
public enum VramGroup { Terrain, TerrainImage, Contours, Reference, Lines, Text, Deck, Concrete, Substructure, Steel, Welds, Bolts, Studs, AnchorBolts, Rebar, Prestressed, Misc }

/// <summary>
/// One GPU geometry buffer set (a model in the viewport) and its estimated video memory. Byte sizes follow HelixToolkit.SharpDX 3.x:
/// mesh vertex = <c>DefaultVertex</c> (Vector4 position + normal + tangent + bitangent = 52 B) plus 8 B texture coordinates when draped,
/// line vertex = 32 B (Vector4 position + Color4), index = 4 B (32-bit), instance = 64 B (Matrix4x4), billboard glyph = 4 x 96 B.
/// </summary>
public sealed record VramItem(string Layer, VramGroup Group, long Vertices, long Indices, long Instances = 0, long ExtraBytes = 0,
    int Bridge = -1, bool Detail = false, int VertexBytes = VramItem.MeshVertexBytes, Vec2? Center = null)
{
    public const int MeshVertexBytes = 52, TexCoordBytes = 8, LineVertexBytes = 32, IndexBytes = 4, InstanceBytes = 64, GlyphBytes = 4 * 96;
    public long Bytes => Vertices * VertexBytes + Indices * IndexBytes + Instances * InstanceBytes + ExtraBytes;
    /// <summary>Steel plates: buffer size with bolt holes / without them, relative to the buffer as built (1 = no holes to drop).</summary>
    public double WithHolesFactor { get; init; } = 1;
    public double WithoutHolesFactor { get; init; } = 1;
    public long BytesFor(bool platesWithoutHoles) => (long)(Bytes * (platesWithoutHoles ? WithoutHolesFactor : WithHolesFactor));
    /// <summary>Triangles drawn (instanced meshes count once per instance).</summary>
    public long Triangles => VertexBytes == LineVertexBytes ? 0 : Indices / 3 * Math.Max(1, Instances);

    /// <summary>RGBA8 texture with a full mip chain.</summary>
    public static long TextureBytes(long width, long height) => width * height * 4 * 4 / 3;
}

/// <summary>Terrain cost model: a raster DEM drawn every <c>stride</c>-th node, optionally clipped to a plan box.</summary>
public sealed record VramTerrain(string Layer, long Nx, long Ny, double X0, double Y0, double Cell, int LoadedStride, long ImageWidth, long ImageHeight)
{
    /// <summary>Contour lines (plan view) of the full display mesh; they scale with 1 / stride and the clipped area.</summary>
    public long ContourBytes { get; init; }

    public Box2 Extent => new() { MinX = X0, MinY = Y0, MaxX = X0 + (Nx - 1) * Cell, MaxY = Y0 + (Ny - 1) * Cell };

    /// <summary>Share of the grid's area inside <paramref name="clip"/> (1 when empty).</summary>
    public double ClipFraction(Box2 clip)
    {
        if (clip.IsEmpty) return 1;
        var e = Extent;
        double w = Math.Max(0, Math.Min(e.MaxX, clip.MaxX) - Math.Max(e.MinX, clip.MinX));
        double h = Math.Max(0, Math.Min(e.MaxY, clip.MaxY) - Math.Max(e.MinY, clip.MinY));
        return e.Width * e.Height > 0 ? w * h / (e.Width * e.Height) : 1;
    }

    /// <summary>Bytes of the display mesh at a stride, clipped to <paramref name="clip"/> (empty = whole grid), with texture coordinates when draped,
    /// plus its contour lines.</summary>
    public long MeshBytes(int stride, Box2 clip)
    {
        double f = ClipFraction(clip);
        double nodes = (double)((Nx - 1) / stride + 1) * ((Ny - 1) / stride + 1) * f;
        int vb = VramItem.MeshVertexBytes + (ImageWidth > 0 ? VramItem.TexCoordBytes : 0);
        return (long)(nodes * vb + nodes * 2 * 3 * VramItem.IndexBytes + ContourBytes * f * LoadedStride / Math.Max(1, stride));
    }

    /// <summary>Draped image bytes when its longer side is limited to <paramref name="maxPixels"/> (0 = full size).</summary>
    public long ImageBytes(int maxPixels)
    {
        if (ImageWidth <= 0 || ImageHeight <= 0) return 0;
        double s = maxPixels > 0 ? Math.Min(1.0, maxPixels / (double)Math.Max(ImageWidth, ImageHeight)) : 1;
        return VramItem.TextureBytes((long)(ImageWidth * s), (long)(ImageHeight * s));
    }
}

/// <summary>Everything the viewport would put on the GPU, as built at full detail.</summary>
public sealed class VramInventory
{
    public List<VramItem> Items { get; } = new();
    public List<VramTerrain> Terrains { get; } = new();
    /// <summary>Plan box of the bridges (terrain is clipped around it).</summary>
    public Box2 Bridges = Box2.Empty;
    /// <summary>Bridge names by index (<see cref="VramItem.Bridge"/>).</summary>
    public List<string> BridgeNames { get; } = new();

    public long Total => Items.Sum(i => i.Bytes);
    /// <summary>Total as built at full detail (steel with bolt holes).</summary>
    public long FullTotal => Items.Sum(i => i.BytesFor(false));

    /// <summary>Bytes per group, largest first.</summary>
    public IEnumerable<(VramGroup group, long bytes, long triangles, int buffers)> ByGroup() =>
        Items.GroupBy(i => i.Group).Select(g => (g.Key, g.Sum(i => i.Bytes), g.Sum(i => i.Triangles), g.Count())).OrderByDescending(x => x.Item2);

    /// <summary>Bytes per layer, largest first.</summary>
    public IEnumerable<(string layer, long bytes, long triangles, int buffers)> ByLayer() =>
        Items.GroupBy(i => i.Layer).Select(g => (g.Key, g.Sum(i => i.Bytes), g.Sum(i => i.Triangles), g.Count())).OrderByDescending(x => x.Item2);
}

/// <summary>User overrides for the viewport memory manager (app settings, not saved in the project).</summary>
public sealed class VramSettings
{
    /// <summary>GPU memory the viewport may use (MB); 0 = automatic (a share of the adapter's budget reported by DXGI).</summary>
    public double BudgetMb { get; set; }
    /// <summary>Share of the free DXGI budget used when <see cref="BudgetMb"/> is 0.</summary>
    public double AutoFraction { get; set; } = 0.8;
    /// <summary>Reduce the drawing automatically when the estimate (or the measured usage, or a GPU out-of-memory error) exceeds the budget.</summary>
    public bool AutoReduce { get; set; } = true;
    /// <summary>Terrain is clipped to the bridges' plan box grown by this distance (m) once terrain reduction starts.</summary>
    public double TerrainClipMargin { get; set; } = 400;
    /// <summary>Largest terrain stride the manager uses before turning the terrain off.</summary>
    public int TerrainMaxStride { get; set; } = 8;
    /// <summary>Detail (bolts, studs, welds, rebar) drawing radius from the line of sight (m) in the first detail step; later steps halve it down to <see cref="MinDetailRadius"/>.</summary>
    public double DetailRadius { get; set; } = 120;
    public double MinDetailRadius { get; set; } = 30;
    /// <summary>Bridges farther than this (m) from the line of sight are drawn simplified (primary members only) when that step is active. 0 = never.</summary>
    public double FarBridgeDistance { get; set; } = 150;
    /// <summary>Simplify far bridges even when the budget is not exceeded.</summary>
    public bool AlwaysSimplifyFarBridges { get; set; }

    public VramSettings Clone() => (VramSettings)MemberwiseClone();
}

/// <summary>One rung of the degradation ladder: a complete drawing configuration and its estimated video memory.</summary>
public sealed record VramLevel
{
    public int Index { get; init; }
    /// <summary>Terrain drawn every n-th DEM node (relative to the full grid); 0 = terrain not drawn.</summary>
    public int TerrainStride { get; init; } = 1;
    /// <summary>Terrain clipped to the bridges grown by this margin (m); 0 = not clipped.</summary>
    public double TerrainClip { get; init; }
    /// <summary>Draped image limited to this many pixels on its longer side; 0 = full size.</summary>
    public int ImageMaxPixels { get; init; }
    /// <summary>Detail tiles beyond <see cref="DetailRadius"/> are released from the GPU (not only hidden).</summary>
    public bool DetailResidency { get; init; }
    public double DetailRadius { get; init; }
    /// <summary>Steel plates drawn without bolt holes (the bolts drawn in them hide the difference); needs a scene rebuild.</summary>
    public bool PlatesWithoutHoles { get; init; }
    /// <summary>Far bridges show primary members only (deck, barriers, girders, caps, columns, foundations).</summary>
    public bool FarBridgeLod { get; init; }
    /// <summary>Detail layers (bolts, studs, welds, rebar) are not drawn at all.</summary>
    public bool DropDetail { get; init; }
    public long Estimated { get; init; }
    /// <summary>What this level reduces, in words (for the status bar).</summary>
    public string Description { get; init; } = "";
    public bool IsFull => Index == 0;
}

/// <summary>
/// Plans how the viewport degrades when the scene would not fit in video memory. Order (agreed with the user): (a) terrain - clip to the
/// bridges and shrink the draped image, then coarser grids; (b) detail layers (bolts, studs, welds, rebar) - far tiles are released, then the
/// radius shrinks; (c) far-away bridges - primary members only; then the last resorts (coarsest terrain, terrain off, detail off).
/// Pure function of the inventory: the app measures, this decides.
/// </summary>
public static class VramPlanner
{
    /// <summary>Layers that make up the simplified (far) bridge: everything else of a far bridge is released.</summary>
    public static readonly HashSet<string> PrimaryLayers = new(StringComparer.Ordinal)
    {
        "3D-DECK", "3D-BARRIER", "3D-GIRDER-PLATES", "3D-SUB-CAP", "3D-SUB-PEDESTAL", "3D-SUB-COLUMN", "3D-SUB-FOUNDATION", "3D-PS-GIRDERS", "3D-DIAPHRAGMS",
    };

    public static bool IsDetailGroup(VramGroup g) => g is VramGroup.Welds or VramGroup.Bolts or VramGroup.Studs or VramGroup.AnchorBolts or VramGroup.Rebar;

    /// <summary>The degradation ladder, full detail first. Each level includes the reductions of the levels before it.</summary>
    public static List<VramLevel> Ladder(VramInventory inv, VramSettings s)
    {
        var levels = new List<VramLevel>();
        bool hasTerrain = inv.Terrains.Count > 0;
        bool hasImage = inv.Terrains.Any(t => t.ImageWidth > 0);
        bool hasDetail = inv.Items.Any(i => i.Detail);
        bool multiBridge = inv.Items.Select(i => i.Bridge).Where(b => b >= 0).Distinct().Count() > 1 && s.FarBridgeDistance > 0;
        double r1 = Math.Max(s.DetailRadius, s.MinDetailRadius);
        int maxStride = Math.Max(2, s.TerrainMaxStride);

        var cur = new VramLevel { Description = "full detail", DetailRadius = s.DetailRadius, FarBridgeLod = s.AlwaysSimplifyFarBridges && multiBridge };
        void Add(VramLevel l, string what)
        {
            l = l with { Index = levels.Count, Description = what };
            l = l with { Estimated = Estimate(inv, l, s) };
            levels.Add(l);
            cur = l;
        }
        Add(cur, cur.FarBridgeLod ? "far bridges simplified (setting)" : "full detail");
        if (hasTerrain)
        {
            Add(cur with { TerrainClip = s.TerrainClipMargin, ImageMaxPixels = hasImage ? 2048 : 0 },
                $"terrain clipped to {s.TerrainClipMargin:0} m around the bridges" + (hasImage ? ", image 2048 px" : ""));
            Add(cur with { TerrainStride = 2 }, "terrain every 2nd point");
            Add(cur with { TerrainStride = 3 }, "terrain every 3rd point");
        }
        if (hasDetail)
        {
            Add(cur with { DetailResidency = true, DetailRadius = r1 }, $"bolts / studs / welds / rebar only within {r1:0} m of the view");
            if (inv.Items.Any(it => it.WithoutHolesFactor < it.WithHolesFactor * 0.98))
                Add(cur with { PlatesWithoutHoles = true }, "steel plates drawn without bolt holes (bolts still drawn)");
            double r2 = Math.Max(s.MinDetailRadius, r1 / 2);
            if (r2 < r1) Add(cur with { DetailRadius = r2 }, $"bolts / studs / welds / rebar only within {r2:0} m");
        }
        if (multiBridge && !cur.FarBridgeLod)
            Add(cur with { FarBridgeLod = true }, $"bridges beyond {s.FarBridgeDistance:0} m simplified (primary members only)");
        if (hasTerrain && maxStride > 3)
            Add(cur with { TerrainStride = maxStride, ImageMaxPixels = hasImage ? 1024 : 0 }, $"terrain every {maxStride}th point" + (hasImage ? ", image 1024 px" : ""));
        if (hasDetail && cur.DetailRadius > s.MinDetailRadius)
            Add(cur with { DetailRadius = s.MinDetailRadius }, $"bolts / studs / welds / rebar only within {s.MinDetailRadius:0} m");
        if (hasTerrain) Add(cur with { TerrainStride = 0, ImageMaxPixels = 0 }, "terrain not drawn");
        if (hasDetail) Add(cur with { DropDetail = true }, "bolts / studs / welds / rebar not drawn");
        return levels;
    }

    /// <summary>The first (most detailed) level whose estimate fits the budget, else the last level.</summary>
    public static VramLevel Choose(IReadOnlyList<VramLevel> ladder, long budget, int minimumIndex = 0)
    {
        for (int i = Math.Clamp(minimumIndex, 0, ladder.Count - 1); i < ladder.Count; i++)
            if (ladder[i].Estimated <= budget) return ladder[i];
        return ladder[^1];
    }

    /// <summary>All reductions active at a level, in words (empty for full detail).</summary>
    public static string Summary(IReadOnlyList<VramLevel> ladder, VramLevel level) =>
        string.Join("; ", ladder.Where(l => l.Index > 0 && l.Index <= level.Index).Select(l => l.Description));

    /// <summary>Estimated video memory of a level.</summary>
    public static long Estimate(VramInventory inv, VramLevel l, VramSettings s)
    {
        long total = 0;
        // Bridge geometry (and the per-tile detail) as built.
        var farBridges = new HashSet<int>();
        if (l.FarBridgeLod)
        {
            // The view is assumed to be on the largest bridge; the others are far.
            var sizes = inv.Items.Where(i => i.Bridge >= 0).GroupBy(i => i.Bridge).Select(g => (g.Key, g.Sum(i => i.Bytes))).OrderByDescending(x => x.Item2).ToList();
            foreach (var (b, _) in sizes.Skip(1)) farBridges.Add(b);
        }
        var terrainLayers = inv.Terrains.SelectMany(t => new[] { t.Layer, t.Layer + "-CONTOURS" }).ToHashSet();
        foreach (var it in inv.Items)
        {
            if (it.Group is VramGroup.Terrain or VramGroup.TerrainImage or VramGroup.Contours && terrainLayers.Contains(it.Layer)) continue; // modelled below
            if (it.Group == VramGroup.Contours && l.TerrainStride == 0) continue;
            if (it.Detail && l.DropDetail) continue;
            if (farBridges.Contains(it.Bridge) && !PrimaryLayers.Contains(it.Layer)) continue;
            if (it.Detail && l.DetailResidency) continue; // modelled below
            total += it.BytesFor(l.PlatesWithoutHoles);
        }
        if (l.DetailResidency && !l.DropDetail)
            total += ResidentDetail(inv.Items.Where(i => i.Detail && !farBridges.Contains(i.Bridge)), l.DetailRadius);
        foreach (var t in inv.Terrains)
        {
            if (l.TerrainStride == 0) continue;
            var clip = l.TerrainClip > 0 && !inv.Bridges.IsEmpty ? inv.Bridges.Inflate(l.TerrainClip) : Box2.Empty;
            total += t.MeshBytes(Math.Max(t.LoadedStride, l.TerrainStride), clip) + t.ImageBytes(l.ImageMaxPixels);
        }
        return total;
    }

    /// <summary>
    /// Detail tiles kept on the GPU when only those within <paramref name="radius"/> of the line of sight are resident: per layer the
    /// largest tiles that fit in a plan circle of that radius (a near / top-down view; oblique views are caught by the usage monitor).
    /// </summary>
    public static long ResidentDetail(IEnumerable<VramItem> tiles, double radius, double tileSize = 40)
    {
        int k = (int)Math.Ceiling(Math.PI * Math.Pow(radius / tileSize + 1, 2));
        long sum = 0;
        foreach (var g in tiles.GroupBy(t => t.Layer))
        {
            // Several buffers can share a tile (one per bolt size): sum per tile first.
            int n = 0;
            var perTile = g.GroupBy(t => t.Center is { } c ? ((long)Math.Floor(c.X / tileSize), (long)Math.Floor(c.Y / tileSize)) : (long.MinValue, n++))
                .Select(x => x.Sum(t => t.Bytes));
            sum += perTile.OrderByDescending(b => b).Take(k).Sum();
        }
        return sum;
    }

    public static string Mb(long bytes) => $"{bytes / 1048576.0:N0} MB";
}
