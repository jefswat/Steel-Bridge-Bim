using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Drawing.Projection;

/// <summary>
/// One model element handed to <see cref="ModelProjector"/>: a solid (triangle mesh, optionally with exact <see cref="ExtrudedSolid"/>
/// recipes) or a set of centerlines (rebar, strands), the 3D layer it lives on, and the keys that identify it in the output.
/// </summary>
public sealed class ProjectionItem
{
    /// <summary>Solid; null for line-only items (<see cref="Lines"/>).</summary>
    public Mesh? Mesh { get; init; }
    /// <summary>Centerlines drawn as they are (rebar / strands); never occlude.</summary>
    public IReadOnlyList<Vec3[]>? Lines { get; init; }
    /// <summary>3D layer name (same as the viewport, e.g. "3D-ARCH-RIBS"); the output layers are this name and "&lt;name&gt;-HIDDEN".</summary>
    public required string Layer { get; init; }
    /// <summary>Unique element key (pick tag) written on every output polyline (<see cref="Polyline3.Tag"/>).</summary>
    public required string Key { get; init; }
    /// <summary>Category / kind (part category, substructure element kind, "Deck"...), used by category filters.</summary>
    public string Kind { get; init; } = "";
    /// <summary>Display name (part mark, element name).</summary>
    public string Name { get; init; } = "";
    public string Bridge { get; init; } = "";
    /// <summary>Girder index of the element's assembly (-1 = none), for girder elevation filters.</summary>
    public int Girder { get; init; } = -1;
    /// <summary>Source object (SteelPart, SubSolid, BarPlacement ...) so a picked line maps back to the model.</summary>
    public object? Source { get; init; }
    /// <summary>Hides what is behind it (false: drawn, but transparent for the hidden-line pass).</summary>
    public bool Occludes { get; init; } = true;
    /// <summary>False: the solid hides only its own back edges (a deck drawn as its outline over the framing it covers: transparent for the others).</summary>
    public bool OccludesOthers { get; init; } = true;
}

/// <summary>What a projected polyline belongs to (<see cref="ProjectionResult.Elements"/>, keyed by <see cref="ProjectionItem.Key"/>).</summary>
public sealed record ProjectedElement(string Key, string Layer, string Kind, string Name, string Bridge, object? Source);

public enum HiddenLines
{
    /// <summary>Hidden pieces are kept on the "-HIDDEN" layers (dashed).</summary>
    Show,
    /// <summary>Hidden pieces are dropped.</summary>
    None,
    /// <summary>No hidden-line removal: every edge is drawn visible (fastest).</summary>
    Off,
}

public enum HlrMethod
{
    /// <summary>Exact segment splitting against the occluding triangles (2D BVH, parallel).</summary>
    Exact,
    /// <summary>Depth buffer (z-buffer raster of the occluders, segments sampled per pixel): fast, approximate at pixel size.</summary>
    DepthBuffer,
}

/// <summary>Options of <see cref="ModelProjector.Project"/>. Lengths in meters.</summary>
public sealed class ProjectionOptions
{
    public HiddenLines Hidden { get; set; } = HiddenLines.Show;
    public HlrMethod Method { get; set; } = HlrMethod.Exact;
    /// <summary>Dihedral angle (degrees) above which (default 30: a 16-sided bolt hole, 22.5 deg per facet, stays smooth) an edge between two faces is a feature edge; smoother edges are drawn only as silhouettes.</summary>
    public double SharpAngleDegrees { get; set; } = 30;
    /// <summary>A point is hidden only if an occluding face is this much closer to the viewer (contact faces stay visible).</summary>
    public double DepthTolerance { get; set; } = 2e-4;
    /// <summary>Depth buffer pixel size (0 = longest side of the view / <see cref="DepthBufferPixels"/>).</summary>
    public double PixelSize { get; set; }
    public int DepthBufferPixels { get; set; } = 4096;
    /// <summary>Use the exact <see cref="ExtrudedSolid"/> recipes for edges when they describe the whole mesh (no tessellation lines).</summary>
    public bool UseRecipes { get; set; } = true;
    /// <summary>Draw bolt-hole edges (circles, hole walls); off for very large plan views.</summary>
    public bool Holes { get; set; } = true;
    /// <summary>Sides of a recipe hole circle.</summary>
    public int HoleSides { get; set; } = 16;
    /// <summary>Optional 2D window in view coordinates (x0, y0, x1, y1): everything outside is dropped (developed elevations of one splice ...).</summary>
    public Box2? Window { get; set; }
    /// <summary>Output Z = height toward the viewer (-depth) instead of 0 (the app's plan view keeps world elevations this way).</summary>
    public bool KeepDepth { get; set; }
    /// <summary>Coincident (collinear overlapping) lines are drawn once; visible wins over hidden.</summary>
    public bool Merge { get; set; } = true;
    /// <summary>Join pieces of one element / layer that share end points into polylines.</summary>
    public bool Chain { get; set; } = true;
    /// <summary>Max length of an edge piece under a non-linear (developed) map, so curves follow the path.</summary>
    public double MaxSegment { get; set; } = 0.5;
    /// <summary>Degree of parallelism (0 = all cores).</summary>
    public int Threads { get; set; }
}

/// <summary>Counts and timings of one projection.</summary>
public sealed class ProjectionStats
{
    public int Items { get; set; }
    public long Triangles { get; set; }
    public long Occluders { get; set; }
    public long Edges { get; set; }
    public long VisiblePieces { get; set; }
    public long HiddenPieces { get; set; }
    public double VisibleLength { get; set; }
    public double HiddenLength { get; set; }
    public TimeSpan EdgeTime { get; set; }
    public TimeSpan OccluderTime { get; set; }
    public TimeSpan HlrTime { get; set; }
    public TimeSpan Total { get; set; }
    public override string ToString() =>
        $"{Items:N0} items, {Triangles:N0} triangles ({Occluders:N0} occluders), {Edges:N0} edges -> {VisiblePieces:N0} visible / {HiddenPieces:N0} hidden pieces; " +
        $"edges {EdgeTime.TotalSeconds:F2} s, occluders {OccluderTime.TotalSeconds:F2} s, HLR {HlrTime.TotalSeconds:F2} s, total {Total.TotalSeconds:F2} s";
}

/// <summary>Projected linework: layers named like the 3D layers (+ "-HIDDEN"), every polyline tagged with its element key.</summary>
public sealed class ProjectionResult
{
    public required PlanDrawing Drawing { get; init; }
    public Dictionary<string, ProjectedElement> Elements { get; } = new();
    public required ProjectionStats Stats { get; init; }
    public required IViewMap View { get; init; }

    /// <summary>Removes the lines of the layers whose names start with any of the prefixes (e.g. "3D-DECK-HIDDEN": a framing plan keeps the
    /// deck outline but not the deck's own hidden soffit lines). Returns the drawing.</summary>
    public PlanDrawing DropLayers(params string[] prefixes)
    {
        foreach (var l in Drawing.Layers)
            if (prefixes.Any(p => l.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase))) l.Polylines.Clear();
        return Drawing;
    }

    /// <summary>The element nearest to a view point (x, y) within <paramref name="radius"/> (picking / QC measure), or null.</summary>
    public ProjectedElement? Pick(Vec2 p, double radius)
    {
        string? best = null;
        double bd = radius;
        foreach (var l in Drawing.Layers)
            foreach (var pl in l.Polylines)
            {
                if (pl.Tag == null) continue;
                for (int i = 0; i + 1 < pl.Points.Length; i++)
                {
                    double d = SegDist(p, pl.Points[i].XY, pl.Points[i + 1].XY);
                    if (d < bd) { bd = d; best = pl.Tag; }
                }
            }
        return best != null && Elements.TryGetValue(best, out var e) ? e : null;
    }

    internal static double SegDist(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        double l2 = ab.LengthSquared;
        double t = l2 < 1e-24 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
        return (p - (a + ab * t)).Length;
    }
}
