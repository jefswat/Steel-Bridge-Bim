namespace SBM.Core.Project;

/// <summary>
/// Project-level geometry options (saved in the .sbm as "Geometry"; absent = defaults). With <see cref="ExactCurvedGeometry"/> off
/// (the default) every generator behaves exactly as before.
/// </summary>
public sealed class GeometrySettings
{
    /// <summary>Curved parts (girder flange / web plates on curved or graded girders, field splice plates, deck slab, haunches,
    /// barriers) are built as exact NURBS solids fitted to the generator's own exact geometry (alignment offset curves, vertical
    /// profile, cross slope) and their meshes are tessellated FROM the NURBS. Default off.</summary>
    public bool ExactCurvedGeometry { get; set; }

    /// <summary>Maximum deviation of the NURBS from the exact geometry (m). 0 = 1/256" in US projects, 0.1 mm in SI.</summary>
    public double FitTolerance { get; set; }

    /// <summary>Chord tolerance of the tessellated meshes (m). 0 = today's sample density (the mesh then has the same vertex layout
    /// as without NURBS, its vertices on the NURBS).</summary>
    public double ChordTolerance { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDefault => !ExactCurvedGeometry && FitTolerance == 0 && ChordTolerance == 0;

    /// <summary>Fit tolerance in meters for the given units.</summary>
    public double FitToleranceFor(Units.UnitSystem u) => FitTolerance > 0 ? FitTolerance : u.IsUS ? 0.0254 / 256 : 1e-4;

    /// <summary>Settings for the generators: null when exact geometry is off.</summary>
    public static GeometrySettings? Active(BridgeProject p) => p.Geometry is { ExactCurvedGeometry: true } g ? g : null;
}
