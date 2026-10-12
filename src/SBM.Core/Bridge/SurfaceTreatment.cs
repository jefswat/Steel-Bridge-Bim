using System.Text.Json.Serialization;

namespace SBM.Core.Bridge;

/// <summary>Kind of architectural surface treatment on concrete.</summary>
public enum SurfaceTreatmentKind
{
    /// <summary>Texture cast by a form liner (e.g. Iowa "brick pattern form liner", Nebraska form liner): no separate material.</summary>
    FormLiner,
    /// <summary>Integral thin veneer brick (or terra cotta) cast into the face: a real cladding layer with its own pay quantity.</summary>
    ThinBrick,
    /// <summary>Rustication strips / reveals (when they are not modeled in the solid).</summary>
    Rustication,
    Other,
}

/// <summary>Which model elements a <see cref="SurfaceRegion"/> takes its faces from.</summary>
public enum SurfaceHost
{
    /// <summary>The custom barrier faces carrying <see cref="SurfaceRegion.Tag"/> (<see cref="BarrierProfilePoint.Tag"/>).</summary>
    Barrier,
    /// <summary>Solids of a substructure unit (<see cref="SurfaceRegion.Support"/>, filtered by <see cref="SurfaceRegion.Elements"/> / <see cref="SurfaceRegion.NameContains"/>).</summary>
    Substructure,
    /// <summary>The concrete end / pier diaphragm at <see cref="SurfaceRegion.Support"/>.</summary>
    Diaphragm,
}

public enum SurfaceLevelRef
{
    /// <summary>Elevation from <see cref="SurfaceLevel.Points"/>: one point = level; several = linear in S along the support line (held beyond the ends).</summary>
    Elevation,
    /// <summary>Finished top of deck at the point (extended beyond the deck) + <see cref="SurfaceLevel.Offset"/> - "parallel to grade".</summary>
    DeckTop,
}

/// <summary>A limit line of a region on a face: elevation (level or sloped along the support line) or parallel to the top of deck.</summary>
public sealed class SurfaceLevel
{
    public SurfaceLevelRef Ref { get; set; }
    /// <summary>(S along the support line, elevation) points for <see cref="SurfaceLevelRef.Elevation"/>.</summary>
    public List<CapProfilePoint> Points { get; set; } = new();
    /// <summary>Added to the reference (e.g. -8 3/4" below the top of deck).</summary>
    public double Offset { get; set; }
    public static SurfaceLevel Level(double z) => new() { Ref = SurfaceLevelRef.Elevation, Points = { new CapProfilePoint(0, z) } };
    public static SurfaceLevel Along(params (double S, double Z)[] pts) => new() { Ref = SurfaceLevelRef.Elevation, Points = pts.Select(p => new CapProfilePoint(p.S, p.Z)).ToList() };
    public static SurfaceLevel BelowDeck(double depth) => new() { Ref = SurfaceLevelRef.DeckTop, Offset = -depth };
    public SurfaceLevel Clone() { var c = (SurfaceLevel)MemberwiseClone(); c.Points = Points.Select(p => new CapProfilePoint(p.S, p.Elevation)).ToList(); return c; }
}

/// <summary>A horizontal band left out of a region (e.g. the plain concrete feature bands between thin-brick panels).</summary>
public sealed class SurfaceBand
{
    public SurfaceLevel Lower { get; set; } = new();
    public SurfaceLevel Upper { get; set; } = new();
    public string Source { get; set; } = "";
    public SurfaceBand Clone() => new() { Lower = Lower.Clone(), Upper = Upper.Clone(), Source = Source };
}

/// <summary>
/// Faces that carry a treatment: from a host, the exposed (not covered by another solid of the same support) near-vertical faces, optionally facing one
/// direction of the support frame (S along the support line + right, N normal + up-station), between two limit lines and an S range, minus excluded bands.
/// Barrier regions take the tagged faces as they are.
/// </summary>
public sealed class SurfaceRegion
{
    public string Name { get; set; } = "";
    public SurfaceHost Host { get; set; }
    /// <summary>Support name (Substructure / Diaphragm hosts; also gives the S / N frame).</summary>
    public string Support { get; set; } = "";
    /// <summary>Barrier host: the surface tag of the faces.</summary>
    public string Tag { get; set; } = "";
    /// <summary>Substructure host: element kinds (SubElementKind names, e.g. "Column", "Cap", "Wingwall", "WingFooting", "Footing"); empty = all.</summary>
    public List<string> Elements { get; set; } = new();
    /// <summary>Only solids whose name contains this text ("" = any).</summary>
    public string NameContains { get; set; } = "";
    /// <summary>Face direction in the support frame (S, N); both 0 = any near-vertical face (e.g. all sides of a column).</summary>
    public double FacingS { get; set; }
    public double FacingN { get; set; }
    /// <summary>Largest angle between the face normal and the facing direction (plan), deg.</summary>
    public double MaxAngleDeg { get; set; } = 30;
    /// <summary>Optional facing direction as an alignment-normal sense: +1 = faces RIGHT of the alignment (outboard on the right), -1 = left; 0 = use FacingS / N.</summary>
    public int FacingOffsetSide { get; set; }
    public double SMin { get; set; } = double.NaN;
    public double SMax { get; set; } = double.NaN;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SurfaceLevel? Lower { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SurfaceLevel? Upper { get; set; }
    public List<SurfaceBand> Exclude { get; set; } = new();
    /// <summary>Plan reference of the region (sheet / detail).</summary>
    public string Source { get; set; } = "";
    /// <summary>Non-empty: an extent of the region the plans do not fix (page + reason): the region is modeled on <see cref="Basis"/> and flagged UNRESOLVED.</summary>
    public string Unresolved { get; set; } = "";
    public string Basis { get; set; } = "";
    public SurfaceRegion Clone()
    {
        var c = (SurfaceRegion)MemberwiseClone();
        c.Elements = Elements.ToList();
        c.Lower = Lower?.Clone(); c.Upper = Upper?.Clone();
        c.Exclude = Exclude.Select(e => e.Clone()).ToList();
        return c;
    }
}

/// <summary>
/// OPTIONAL architectural surface treatment (form liner texture, integral thin brick, ...) on concrete faces (<see cref="BridgeDefinition.SurfaceTreatments"/>):
/// regions on the model faces get a thin RELIEF LAYER solid (display / IFC IfcCovering with the treatment as properties) and an AREA that is compared with
/// the plan quantity. The texture itself is not modeled (pattern name, unit size, depth are properties). Generic: any bridge (Woodbury form liner + thin
/// brick; Nebraska St. Ramp B form liner / veneer brick / terra cotta).
/// </summary>
public sealed class SurfaceTreatmentDefinition
{
    public string Name { get; set; } = "";
    public SurfaceTreatmentKind Kind { get; set; }
    /// <summary>Pattern as specified, e.g. "smooth-faced brick, running bond, 2 1/4 x 7 5/8".</summary>
    public string Pattern { get; set; } = "";
    /// <summary>Approved products / pattern numbers as listed on the plans.</summary>
    public string Products { get; set; } = "";
    /// <summary>Depth of the relief layer drawn on the face (texture depth / veneer projection), m.</summary>
    public double Relief { get; set; }
    /// <summary>Pattern orientation ("level", "parallel to grade", ...).</summary>
    public string Orientation { get; set; } = "";
    /// <summary>Plan quantity (m2), NaN = none (e.g. included in the concrete item).</summary>
    public double PlanQuantity { get; set; } = double.NaN;
    /// <summary>Plan pay item of the quantity.</summary>
    public string QuantityItem { get; set; } = "";
    public string Source { get; set; } = "";
    public List<SurfaceRegion> Regions { get; set; } = new();
    /// <summary>Plan items of the treatment not modeled (page + reason), raised as UNRESOLVED warnings.</summary>
    public List<string> Unresolved { get; set; } = new();
    public SurfaceTreatmentDefinition Clone()
    {
        var c = (SurfaceTreatmentDefinition)MemberwiseClone();
        c.Regions = Regions.Select(r => r.Clone()).ToList();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}
